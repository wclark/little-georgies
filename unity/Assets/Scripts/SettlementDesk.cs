using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LittleGeorgies.Economy;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LittleGeorgies
{
    public sealed class SettlementDesk : MonoBehaviour
    {
        public SettlementEconomy Session { get; private set; }
        public AuctionResult Result { get; private set; }
        public AuctionTaxReport Report { get; private set; }
        public Canvas RenderCanvas { get; private set; }
        public Camera Camera { get; private set; }
        public bool IsCurrent => Result != null && solvedRevision == Session.Revision;
        public string SavePath { get; private set; }
        public bool Running { get; private set; }
        VillageGame game;
        Font font;
        RectTransform root, body, details;
        Text status, totals, day, forecast;
        Button next, play;
        readonly Dictionary<string, Text> mapLabels = new Dictionary<string, Text>();
        readonly Dictionary<string, HexTile> mapTiles = new Dictionary<string, HexTile>();
        readonly Dictionary<string, Texture2D> portraits = new Dictionary<string, Texture2D>();
        Task<AuctionResult> pending;
        int taskRevision, solvedRevision = -1, startedRevision = -1;
        int personId = 1, tab, lastWidth, lastHeight;
        string plotId = "O1", error;
        bool inspectPlot, valuesTab, saveBlocked;
        float width, leftWidth, rightWidth, contentHeight, solveAt, runAt;
        static readonly Color Ink = new Color(.12f, .17f, .18f);
        static readonly Color Muted = new Color(.39f, .45f, .46f);
        static readonly Color Paper = new Color(.96f, .97f, .97f);
        static readonly Color Line = new Color(.82f, .87f, .87f);
        static readonly Color Green = new Color(.15f, .38f, .29f);
        static readonly Color Blue = new Color(.15f, .38f, .55f);
        static readonly Color Red = new Color(.70f, .24f, .25f);

        public void Initialize(VillageGame owner)
        {
            game = owner;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var cameraObject = new GameObject("Dossier camera");
            Camera = cameraObject.AddComponent<Camera>();
            Camera.clearFlags = CameraClearFlags.SolidColor; Camera.backgroundColor = Paper;
            Camera.orthographic = true; Camera.transform.position = new Vector3(0, 0, -10);
            Camera.tag = "MainCamera";
            if (EventSystem.current == null) new GameObject("Input events", typeof(EventSystem), typeof(StandaloneInputModule));
            var canvas = new GameObject("Settlement dossiers", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            RenderCanvas = canvas.GetComponent<Canvas>(); RenderCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            RenderCanvas.sortingOrder = 20;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 800); scaler.matchWidthOrHeight = 1;
            root = Node("Safe area", canvas.transform, 0, 0, 1, 1);
            root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one; root.offsetMin = root.offsetMax = Vector2.zero;
            SavePath = Path.Combine(Application.persistentDataPath, "PolicyVillage", "settlement.json");
            var args = Environment.GetCommandLineArgs();
            if (args.Contains("-lg-dossier-smoke"))
            {
                int index = Array.IndexOf(args, "-lg-capture");
                SavePath = Path.Combine(args[index + 1], "PolicyVillage", "settlement.json");
            }
            Session = SettlementEconomy.Create();
            if (!args.Contains("-lg-dossier-smoke") && File.Exists(SavePath))
            {
                try
                {
                    if (new FileInfo(SavePath).Length > 1000000) throw new InvalidDataException("Save is too large.");
                    var restored = JsonUtility.FromJson<SettlementEconomy>(File.ReadAllText(SavePath));
                    restored.Restore(); Session = restored;
                }
                catch (Exception e) { error = "Save not loaded; original kept. " + e.Message; saveBlocked = true; }
            }
            Build(); Queue();
        }

        public void SetVisible(bool visible) => RenderCanvas.gameObject.SetActive(visible);
        public void StopRunning() { Running = false; RefreshHeader(); }

        void Update()
        {
            if (lastWidth != Screen.width || lastHeight != Screen.height || Mathf.Abs(root.rect.width - width) > .5f) Build();
            if (pending != null && pending.IsCompleted)
            {
                var finished = pending; pending = null;
                try
                {
                    var answer = finished.GetAwaiter().GetResult();
                    if (taskRevision == Session.Revision)
                    {
                        Result = answer; Report = AuctionTaxes.Calculate(answer, new AuctionTaxPolicy { ValuePercent = Session.TaxPercent });
                        solvedRevision = taskRevision;
                        UpdateForecast(); DrawDetails(); RefreshHeader();
                    }
                }
                catch (Exception e) { error = "Forecast failed: " + e.Message; Running = false; RefreshHeader(); }
            }
            if (pending == null && startedRevision != Session.Revision && Time.unscaledTime >= solveAt)
            {
                var scenario = Session.Scenario(); int tax = Session.TaxPercent;
                taskRevision = startedRevision = Session.Revision;
                pending = Task.Run(() => LandAuction.SolveWithValueTax(scenario, tax));
            }
            if (game.Auction != null && game.Auction.IsOpen) return;
            bool editing = EventSystem.current.currentSelectedGameObject != null
                && EventSystem.current.currentSelectedGameObject.GetComponent<InputField>() != null;
            if (!editing && Input.GetKeyDown(KeyCode.F4)) { StopRunning(); game.Auction.Open(); }
            if (Running && IsCurrent && !editing && Time.unscaledTime >= runAt) AdvanceDay();
        }

        public void Change(Action<SettlementEconomy> edit)
        {
            edit(Session); Session.Changed(); Queue(); Save();
            RefreshHeader();
        }

        void Queue() { solveAt = Time.unscaledTime + .15f; RefreshHeader(); }

        public void AdvanceDay()
        {
            if (!IsCurrent) return;
            Session.Advance(Result, solvedRevision);
            runAt = Time.unscaledTime + 4;
            Save(); Build(); Queue();
        }

        public void Save()
        {
            if (saveBlocked) return;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SavePath));
                File.WriteAllText(SavePath + ".tmp", JsonUtility.ToJson(Session, true));
                if (File.Exists(SavePath)) File.Replace(SavePath + ".tmp", SavePath, SavePath + ".bak");
                else File.Move(SavePath + ".tmp", SavePath);
            }
            catch (Exception e) { error = "Save failed: " + e.Message; }
        }

        public void SelectPerson(int id, bool valuations = false)
        {
            personId = id; inspectPlot = false; valuesTab = valuations; DrawDetails();
        }
        public void SelectPlot(string id) { plotId = id; inspectPlot = true; UpdateForecast(); DrawDetails(); }
        public void SelectTab(int index) { tab = index; Build(); }

        void Build()
        {
            if (root == null) return;
            lastWidth = Screen.width; lastHeight = Screen.height;
            RenderCanvas.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1066, Mathf.Max(800, 1066f * Screen.height / Screen.width));
            var safe = Screen.safeArea;
            root.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
            root.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
            Canvas.ForceUpdateCanvases();
            width = root.rect.width; leftWidth = (width - 60) * .59f; rightWidth = width - leftWidth - 60;
            contentHeight = root.rect.height - 290;
            Clear(root); mapLabels.Clear(); mapTiles.Clear();
            Box("Background", root, 0, 0, width, root.rect.height, Paper);
            Box("Header", root, 0, 0, width, 78, Ink);
            Portrait(root, Session.Society.People[0], 20, 13, 50);
            Label(root, "Little Georgies", 82, 12, 310, 32, 27, Color.white, true);
            day = Label(root, "", 82, 46, 450, 23, 15, new Color(.74f, .85f, .81f));
            totals = Label(root, "", width - 600, 16, 420, 40, 18, Color.white);
            Cmd(root, "Auction admin", width - 158, 19, 138, 40, () => { StopRunning(); game.Auction.Open(); }, Blue, "Dossier admin");
            string[] names = { "Land", "Georgies", "Policies" };
            for (int i = 0; i < names.Length; i++)
            {
                int index = i;
                Cmd(root, names[i], 20 + i * 132, 92, 124, 40, () => SelectTab(index), tab == i ? Ink : Color.white, "Dossier tab " + i, tab == i ? Color.white : Ink);
            }
            Cmd(root, "New settlement", width - 174, 92, 154, 40, ConfirmReset, Color.white, "Dossier reset", Muted);
            forecast = Label(root, "", 20, 142, width - 40, 59, 20, Ink, true);
            Box("Rule", root, 20, 205, width - 40, 1, Line);
            body = Node("Main view", root, 20, 220, leftWidth, contentHeight);
            details = Node("Dossier", root, 40 + leftWidth, 220, rightWidth, contentHeight);
            Box("Divider", root, 30 + leftWidth, 220, 1, contentHeight, Line);
            if (tab == 0) DrawMap(); else if (tab == 1) DrawPeople(); else DrawPolicies();
            DrawDetails();
            float foot = root.rect.height - 62;
            Box("Footer", root, 0, foot - 14, width, 76, Color.white);
            status = Label(root, "", 20, foot - 3, width - 340, 54, 15, Muted);
            play = Cmd(root, Running ? "Pause days" : "Run days", width - 308, foot, 130, 44, () => { Running = !Running; runAt = Time.unscaledTime + 4; RefreshHeader(); }, Blue, "Dossier autoplay");
            next = Cmd(root, "Next day", width - 166, foot, 146, 44, AdvanceDay, Green, "Dossier next day");
            RefreshHeader(); UpdateForecast();
        }

        void RefreshHeader()
        {
            if (day == null) return;
            var society = Session.Society;
            day.text = "DAY " + society.Day + "  /  " + society.StageName;
            totals.text = society.People.Count + " " + (society.People.Count == 1 ? "Georgie" : "Georgies") + "    " + society.AllApples + " apples    " + Math.Round(society.HappyRate * 100) + "% happy";
            if (next != null) next.interactable = IsCurrent;
            if (play != null) play.GetComponentInChildren<Text>().text = Running ? "Pause days" : "Run days";
            if (status != null) status.text = error ?? (!IsCurrent ? "Updating the land allocation..." :
                Session.LastDay != null ? Session.LastDay.Summary : "Pick apples, eat one, and rest outdoors. A fed, rested Georgie is happy.");
        }

        void UpdateForecast()
        {
            if (forecast == null) return;
            forecast.text = Report == null ? "Today's forecast" : "TODAY'S FORECAST" + (IsCurrent ? "" : "  /  updating")
                + "\nValue  " + Report.TotalValue + "      Tax  " + Report.TotalTaxTenThousandths / 10000
                + "      Rent  " + Report.TotalRentHundredths / 100 + "      Surplus  " + Report.TotalSurplusTenThousandths / 10000;
            foreach (var plot in Session.Plots)
            {
                if (!mapLabels.TryGetValue(plot.Id, out var label)) continue;
                var award = Result?.Allocation.FirstOrDefault(a => a.Plots.Contains(plot.Id));
                var person = award == null ? null : Session.Society.People.FirstOrDefault(p => "g" + p.Id == award.GeorgieId);
                label.text = plot.Id + "\n" + plot.Name + "\n" + (person == null ? "Unassigned" : person.Title);
                var tile = mapTiles[plot.Id];
                tile.color = inspectPlot && plotId == plot.Id ? new Color(.68f, .84f, .90f)
                    : person != null ? new Color(.69f, .84f, .70f) : plot.Kind == "Field" ? new Color(.90f, .88f, .69f) : new Color(.85f, .91f, .84f);
            }
        }

        void DrawMap()
        {
            Label(body, "The commons", 0, 0, leftWidth, 31, 24, Ink, true);
            Label(body, "Daily land use", 0, 34, leftWidth, 24, 16, Muted);
            float radius = Mathf.Min(70, (leftWidth - 28) / 5.3f);
            int[,] cells = { { 0, 0 }, { 0, -1 }, { 1, -1 }, { 1, 0 }, { 0, 1 }, { -1, 1 }, { -1, 0 } };
            float cy = 66 + radius * 2.6f;
            for (int i = 0; i < Session.Plots.Count; i++)
            {
                var plot = Session.Plots[i];
                float x = leftWidth * .5f + 1.53f * radius * cells[i, 0];
                float y = cy + 1.77f * radius * (cells[i, 1] + cells[i, 0] * .5f);
                var rect = Node("Plot " + plot.Id, body, x - radius, y - radius * .866f, radius * 2, radius * 1.732f);
                var graphic = rect.gameObject.AddComponent<HexTile>(); mapTiles[plot.Id] = graphic;
                var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = graphic;
                button.onClick.AddListener(() => SelectPlot(plot.Id));
                mapLabels[plot.Id] = Label(rect, "", radius * .23f, radius * .35f, radius * 1.54f, radius * 1.18f, 16, Ink, true, TextAnchor.MiddleCenter);
            }
            float noteY = Mathf.Max(406, cy + radius * 2.72f);
            Label(body, "Orchards & open fields  /  No private owners", 0, noteY, leftWidth, 25, 15, Muted);
            if (!Session.Society.Specialist)
                Label(body, "Next growth: " + Session.Society.TotalHarvest + " / " + Session.Society.NextGrowthTarget + " gathered, 50% happy.", 0, noteY + 30, leftWidth, 40, 16, Ink);
            else Label(body, Session.Society.TotalBaskets + " baskets    " + Session.Society.TotalHomes + " homes    " + Session.Society.CommonApples + " common apples", 0, noteY + 30, leftWidth, 30, 16, Ink);
        }

        void DrawPeople()
        {
            Label(body, "The Georgies", 0, 0, leftWidth - 270, 32, 24, Ink, true);
            float start = leftWidth - 261;
            int n = 0;
            foreach (var plan in new[] { WorkPlan.Policy, WorkPlan.Work, WorkPlan.Rest })
            {
                var choice = plan;
                Cmd(body, plan == WorkPlan.Policy ? "Auto" : plan.ToString(), start + n++ * 88, 0, 82, 34, () =>
                {
                    Change(s => { foreach (var p in s.Society.People) if (choice != WorkPlan.Rest || p.Mood != Mood.Broken) p.Plan = choice; }); Build();
                }, Color.white, "Dossier group " + plan, Ink);
            }
            float rowHeight = Mathf.Min(87, (contentHeight - 50) / Mathf.Min(5, Session.Society.People.Count));
            RectTransform list = Session.Society.People.Count <= 5 ? body : Scroll(body, 0, 44, leftWidth, contentHeight - 44, Session.Society.People.Count * rowHeight);
            for (int i = 0; i < Session.Society.People.Count; i++)
            {
                var person = Session.Society.People[i];
                float y = (list == body ? 48 : 0) + i * rowHeight;
                var row = Cmd(list, "", 0, y, leftWidth, rowHeight - 7, () => SelectPerson(person.Id), Color.white, "Dossier person " + person.Id);
                Portrait(row.transform, person, 9, 7, rowHeight - 21);
                Label(row.transform, person.Title, rowHeight - 3, 7, leftWidth - rowHeight - 175, 28, 20, Ink, true);
                Label(row.transform, person.Mood + "  /  " + Math.Round(person.HappyRate * 100) + "% happy", rowHeight - 3, 37, leftWidth - rowHeight - 175, 24, 15, Muted);
                Label(row.transform, person.Job == Job.Harvest ? "Harvest" : person.Job.ToString(), leftWidth - 155, 9, 141, 24, 16, Green, true, TextAnchor.MiddleRight);
                Label(row.transform, person.Plan == WorkPlan.Policy ? "Following policy" : "Personal plan", leftWidth - 155, 38, 141, 24, 13, Muted, false, TextAnchor.MiddleRight);
            }
        }

        void DrawPolicies()
        {
            var s = Session.Society;
            Label(body, "Policy desk", 0, 0, leftWidth, 32, 24, Ink, true);
            Label(body, "Applies to the coming day", 0, 35, leftWidth, 25, 16, Muted);
            Check(body, "Rest when tired and food is available", 0, 78, leftWidth, s.Policy.RestWhenFed, value => Change(e => e.Society.Policy.RestWhenFed = value), "Dossier rest policy");
            Label(body, "Tax rate", 0, 144, 130, 26, 18, Ink, true);
            var taxLabel = Label(body, Session.TaxPercent + "%", leftWidth - 76, 144, 76, 26, 21, Blue, true, TextAnchor.MiddleRight);
            var slider = Slider(body, 0, 189, leftWidth - 8, Session.TaxPercent, value =>
            {
                taxLabel.text = value + "%"; Change(e => e.TaxPercent = value);
            });
            slider.gameObject.name = "Dossier tax";
            Label(body, "Value - tax = bid. Bid - rent = surplus.\nTax and rent enter the common store.", 0, 226, leftWidth, 48, 16, Muted);
            if (s.Specialist)
            {
                Check(body, "Pool harvests in the common store", 0, 279, leftWidth, s.Policy.Food == FoodRule.SharedStore,
                    value => Change(e => e.Society.Policy.Food = value ? FoodRule.SharedStore : FoodRule.PersonalHarvest), "Dossier sharing");
                Check(body, "Feed hungry Georgies from common apples", 0, 330, leftWidth, s.Policy.FeedHungry, value => Change(e => e.Society.Policy.FeedHungry = value), "Dossier relief");
                Check(body, "Give available baskets to farmers", 0, 381, leftWidth, s.Policy.AssignBaskets, value => Change(e => e.Society.Policy.AssignBaskets = value), "Dossier baskets");
                if (s.HousingUnlocked)
                    Check(body, "Build and assign homes", 0, 432, leftWidth, s.Policy.BuildHomes && s.Policy.AssignHomes,
                        value => Change(e => { e.Society.Policy.BuildHomes = value; e.Society.Policy.AssignHomes = value; }), "Dossier homes");
            }
            else
                Label(body, "Apples are shared. Each Georgie eats one a day.\n\nNo apple: broken. Fed after work: tired.\nFed after rest: happy.", 0, 286, leftWidth, 144, 19, Ink);
        }

        void DrawDetails()
        {
            if (details == null) return;
            Clear(details);
            if (inspectPlot) DrawPlotDossier(); else DrawPersonDossier();
        }

        void DrawPersonDossier()
        {
            var person = Session.Society.People.FirstOrDefault(p => p.Id == personId) ?? Session.Society.People[0];
            personId = person.Id;
            Label(details, "GEORGIE DOSSIER", 0, 0, rightWidth, 23, 13, Muted, true);
            Portrait(details, person, 0, 38, 90);
            Label(details, person.Title, 104, 38, rightWidth - 108, 34, 25, Ink, true);
            Label(details, person.Mood + "  /  " + Math.Round(person.HappyRate * 100) + "% happy", 104, 76, rightWidth - 108, 26, 16, MoodColor(person.Mood), true);
            Label(details, person.Role == Role.Gatherer ? "Gatherer" : person.Role.ToString(), 104, 105, rightWidth - 108, 23, 15, Muted);
            Cmd(details, "Daily life", 0, 148, 122, 34, () => { valuesTab = false; DrawDetails(); }, valuesTab ? Paper : Ink, "Dossier life", valuesTab ? Ink : Color.white);
            Cmd(details, "Land values", 130, 148, 132, 34, () => { valuesTab = true; DrawDetails(); }, valuesTab ? Ink : Paper, "Dossier values", valuesTab ? Color.white : Ink);
            if (valuesTab)
            {
                Label(details, "PLOT", 0, 199, rightWidth - 240, 22, 13, Muted, true);
                Label(details, "VALUE / TAX / BID", rightWidth - 219, 199, 215, 22, 13, Muted, true, TextAnchor.MiddleRight);
                for (int i = 0; i < Session.Plots.Count; i++)
                {
                    var plot = Session.Plots[i]; int value = Session.Value(person, plot);
                    float y = 232 + i * 35;
                    Cmd(details, plot.Name, 0, y, rightWidth - 237, 30, () => SelectPlot(plot.Id), Color.white, "Dossier value plot " + plot.Id, Ink);
                    var minus = Cmd(details, "-", rightWidth - 232, y, 30, 30, () => EditValue(person, plot, -1), Paper, "Dossier decrease " + plot.Id, Ink);
                    var plus = Cmd(details, "+", rightWidth - 92, y, 30, 30, () => EditValue(person, plot, 1), Paper, "Dossier increase " + plot.Id, Ink);
                    int tax = (int)AuctionTaxes.TaxOnValue(value, Session.TaxPercent);
                    Label(details, value + " / " + tax + " / " + (value - tax), rightWidth - 198, y, 100, 30, 17, Ink, true, TextAnchor.MiddleCenter);
                    Label(details, "apples", rightWidth - 58, y, 58, 30, 12, Muted);
                    minus.interactable = value > 0 && person.Mood != Mood.Broken && person.Yield > 0;
                    plus.interactable = value < 9 && person.Mood != Mood.Broken && person.Yield > 0;
                }
                return;
            }
            var row = Report?.Georgies.FirstOrDefault(g => g.GeorgieId == "g" + person.Id);
            string[] labels = { "Value", "Tax", "Bid", "Rent", "Surplus" };
            long[] amounts = row == null ? new long[5] : new[] { row.Value, row.TaxTenThousandths / 10000, row.BidHundredths / 100, row.RentHundredths / 100, row.SurplusTenThousandths / 10000 };
            for (int i = 0; i < 5; i++)
            {
                float x = i * rightWidth / 5;
                Label(details, labels[i], x, 205, rightWidth / 5 - 2, 22, 13, Muted);
                Label(details, amounts[i].ToString(), x, 230, rightWidth / 5 - 2, 32, 24, Ink, true);
            }
            var award = Result?.Allocation.FirstOrDefault(a => a.GeorgieId == "g" + person.Id);
            var assigned = award == null ? null : Session.Plots.FirstOrDefault(p => award.Plots.Contains(p.Id));
            Label(details, "Land: " + (assigned == null ? "None allocated" : assigned.Name), 0, 279, rightWidth, 28, 18, Ink);
            Label(details, "Daily plan", 0, 321, rightWidth, 25, 17, Ink, true);
            var options = person.Role == Role.Builder ? new[] { WorkPlan.Policy, WorkPlan.Baskets, WorkPlan.Houses, WorkPlan.Rest }
                : new[] { WorkPlan.Policy, WorkPlan.Work, WorkPlan.Rest };
            for (int i = 0; i < options.Length; i++)
            {
                var choice = options[i]; float w = (rightWidth - (options.Length - 1) * 7) / options.Length;
                var button = Cmd(details, choice == WorkPlan.Policy ? "Auto" : choice.ToString(), i * (w + 7), 356, w, 42,
                    () => { Change(e => person.Plan = choice); Build(); }, person.Plan == choice ? Green : Color.white, "Dossier plan " + choice, person.Plan == choice ? Color.white : Ink);
                button.interactable = !(choice == WorkPlan.Rest && person.Mood == Mood.Broken) && !(choice == WorkPlan.Houses && (!Session.Society.HousingUnlocked || person.Mood == Mood.Broken));
            }
            Label(details, "Today: " + person.Job + (person.Mood == Mood.Broken ? "  /  must work" : ""), 0, 412, rightWidth, 25, 16, Muted);
            string belongings = Session.Society.Specialist ? person.Apples + " personal apples  /  " + (person.Basket ? "Basket" : "No basket") : "Apples come from the shared store.";
            if (Session.Society.HousingUnlocked) belongings += "\n" + (person.House ? "Housed: wakes rested when fed." : "Sleeping outdoors");
            Label(details, belongings, 0, 447, rightWidth, 54, 16, Ink);
        }

        void EditValue(Georgie person, LandPlot plot, int change)
        {
            Session.SetValue(person.Id, plot.Id, Session.Value(person, plot) + change);
            Queue(); Save(); DrawDetails();
        }

        void DrawPlotDossier()
        {
            var plot = Session.Plots.Single(p => p.Id == plotId);
            Label(details, "LAND DOSSIER  /  " + plot.Id, 0, 0, rightWidth, 24, 13, Muted, true);
            Label(details, plot.Name, 0, 40, rightWidth, 40, 29, Ink, true);
            Label(details, plot.Kind + "  /  Common land", 0, 84, rightWidth, 26, 17, Green, true);
            var award = Result?.Allocation.FirstOrDefault(a => a.Plots.Contains(plot.Id));
            var holder = award == null ? null : Session.Society.People.FirstOrDefault(p => "g" + p.Id == award.GeorgieId);
            Label(details, holder == null ? "Unassigned today" : "Today's use: " + holder.Title, 0, 126, rightWidth, 32, 20, Ink, true);
            Label(details, "GEORGIE", 0, 184, rightWidth - 165, 26, 13, Muted, true);
            Label(details, "VALUE / TAX / BID", rightWidth - 160, 184, 160, 26, 13, Muted, true, TextAnchor.MiddleRight);
            for (int i = 0; i < Session.Society.People.Count; i++)
            {
                var person = Session.Society.People[i]; int value = Session.Value(person, plot);
                int tax = (int)AuctionTaxes.TaxOnValue(value, Session.TaxPercent);
                var button = Cmd(details, "", 0, 224 + i * 51, rightWidth, 45, () => SelectPerson(person.Id, true), Color.white, "Dossier plot bidder " + person.Id);
                Portrait(button.transform, person, 4, 4, 37);
                Label(button.transform, person.Title, 49, 8, rightWidth - 223, 29, 17, Ink, true);
                Label(button.transform, person.Job != Job.Harvest ? "Not bidding" : value + " / " + tax + " / " + (value - tax), rightWidth - 170, 8, 160, 29, 17, Ink, false, TextAnchor.MiddleRight);
            }
        }

        void ConfirmReset()
        {
            StopRunning();
            var modal = Box("Reset confirmation", root, 0, 0, width, root.rect.height, new Color(0, 0, 0, .65f));
            modal.GetComponent<Image>().raycastTarget = true;
            var panel = Box("Confirmation", modal, width / 2 - 240, root.rect.height / 2 - 110, 480, 220, Color.white);
            Label(panel, "Start a new settlement?", 22, 18, 436, 34, 24, Ink, true);
            Label(panel, "Your current policy-village save will be replaced.\nThe auction-admin scenarios are kept separately.", 22, 69, 436, 70, 17, Muted);
            Cmd(panel, "Cancel", 22, 158, 134, 42, () => { modal.gameObject.SetActive(false); Destroy(modal.gameObject); }, Paper, "Dossier reset cancel", Ink);
            Cmd(panel, "Start again", 294, 158, 164, 42, () =>
            {
                Session = SettlementEconomy.Create(); startedRevision = solvedRevision = -1; pending = null; Result = null; Report = null;
                personId = 1; tab = 0; inspectPlot = false; error = null; saveBlocked = false; Save(); Build(); Queue();
            }, Red, "Dossier reset confirm");
        }

        static Color MoodColor(Mood mood) => mood == Mood.Happy ? Green : mood == Mood.Tired ? new Color(.55f, .38f, .10f) : Red;
        void Portrait(Transform parent, Georgie person, float x, float y, float size)
        {
            string role = person.Role == Role.Gatherer ? "georgie" : person.Role.ToString().ToLowerInvariant();
            string key = role + "-" + person.Mood.ToString().ToLowerInvariant() + (person.Role == Role.Gatherer ? "" : "-avatar");
            if (!portraits.TryGetValue(key, out var texture)) { texture = Resources.Load<Texture2D>("Portraits/" + key); portraits[key] = texture; }
            var image = Node("Portrait " + person.Id, parent, x, y, size, size).gameObject.AddComponent<RawImage>();
            image.texture = texture; image.raycastTarget = false;
        }

        static RectTransform Node(string name, Transform parent, float x, float y, float w, float h)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(w, h); return rect;
        }
        static RectTransform Box(string name, Transform parent, float x, float y, float w, float h, Color color)
        {
            var rect = Node(name, parent, x, y, w, h); var image = rect.gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = false; return rect;
        }
        Text Label(Transform parent, string text, float x, float y, float w, float h, int size, Color color, bool bold = false, TextAnchor alignment = TextAnchor.UpperLeft)
        {
            var label = Node(text, parent, x, y, w, h).gameObject.AddComponent<Text>();
            label.text = text; label.font = font; label.fontSize = size; label.color = color; label.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            label.alignment = alignment; label.raycastTarget = false; label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate; return label;
        }
        Button Cmd(Transform parent, string text, float x, float y, float w, float h, Action action, Color color, string name, Color? ink = null)
        {
            var rect = Box(name, parent, x, y, w, h, color); rect.GetComponent<Image>().raycastTarget = true;
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = rect.GetComponent<Image>(); button.onClick.AddListener(() => action());
            Label(rect, text, 6, 0, w - 12, h, 16, ink ?? Color.white, true, TextAnchor.MiddleCenter); return button;
        }
        void Check(Transform parent, string text, float x, float y, float w, bool value, Action<bool> change, string name)
        {
            var rect = Node(name, parent, x, y, w, 38);
            var toggle = rect.gameObject.AddComponent<Toggle>();
            var box = Box("Box", rect, 0, 5, 28, 28, Color.white).GetComponent<Image>(); box.raycastTarget = true;
            toggle.targetGraphic = box; toggle.graphic = Box("Checked", box.transform, 5, 5, 18, 18, Green).GetComponent<Image>();
            var textLabel = Label(rect, text, 43, 0, w - 46, 38, 17, Ink, false, TextAnchor.MiddleLeft); textLabel.raycastTarget = true;
            toggle.SetIsOnWithoutNotify(value); toggle.onValueChanged.AddListener(v => change(v));
        }
        Slider Slider(Transform parent, float x, float y, float w, int value, Action<int> change)
        {
            var rect = Node("Tax slider", parent, x, y, w, 30); var slider = rect.gameObject.AddComponent<Slider>();
            var track = Box("Track", rect, 0, 11, w, 8, Line); track.GetComponent<Image>().raycastTarget = true;
            var area = Node("Handle area", rect, 12, 0, w - 24, 30);
            var handle = Box("Handle", area, 0, 0, 24, 0, Blue); handle.pivot = new Vector2(.5f, .5f); handle.GetComponent<Image>().raycastTarget = true;
            slider.handleRect = handle; slider.targetGraphic = handle.GetComponent<Image>(); slider.minValue = 0; slider.maxValue = 100; slider.wholeNumbers = true;
            slider.SetValueWithoutNotify(value); slider.onValueChanged.AddListener(v => change(Mathf.RoundToInt(v))); return slider;
        }
        static RectTransform Scroll(Transform parent, float x, float y, float w, float h, float length)
        {
            var viewport = Node("List viewport", parent, x, y, w, h); viewport.gameObject.AddComponent<RectMask2D>();
            var image = viewport.gameObject.AddComponent<Image>(); image.color = new Color(1, 1, 1, .01f);
            var content = Node("List contents", viewport, 0, 0, w, length);
            var scroll = viewport.gameObject.AddComponent<ScrollRect>(); scroll.viewport = viewport; scroll.content = content; scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 28; return content;
        }
        static void Clear(Transform parent)
        {
            foreach (Transform child in parent) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
        }
        void OnApplicationQuit() { Save(); }
    }

    public sealed class HexTile : MaskableGraphic, ICanvasRaycastFilter
    {
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); var rect = rectTransform.rect;
            vh.AddVert(new Vector3(rect.center.x, rect.center.y), color, Vector2.zero);
            for (int i = 0; i < 6; i++)
            {
                float angle = i * Mathf.PI / 3;
                vh.AddVert(new Vector3(rect.center.x + Mathf.Cos(angle) * rect.width * .5f,
                    rect.center.y + Mathf.Sin(angle) * rect.height / 1.732f), color, Vector2.zero);
            }
            for (int i = 0; i < 6; i++) vh.AddTriangle(0, i + 1, (i + 1) % 6 + 1);
        }
        public bool IsRaycastLocationValid(Vector2 point, Camera camera)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, point, camera, out var local);
            var rect = rectTransform.rect; local -= rect.center;
            float x = Mathf.Abs(local.x) / (rect.width * .5f), y = Mathf.Abs(local.y) / (rect.height * .5f);
            return x <= 1 && y <= 1 && x + .5f * y <= 1;
        }
    }
}
