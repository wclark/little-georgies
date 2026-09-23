using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LittleGeorgies.Economy
{
    public sealed class AuctionDesk : MonoBehaviour
    {
        public AuctionScenario Scenario { get; private set; }
        public AuctionResult Result => displayed?.Result;
        public AuctionTaxPolicy Taxes { get; private set; } = new AuctionTaxPolicy();
        public AuctionTaxReport TaxReport => displayed?.TaxReport;
        public bool IsResultCurrent => Result != null && displayedRevision == inputRevision;
        public bool IsOpen => canvas != null && canvas.gameObject.activeSelf;
        public bool IsBusy => task != null;
        public bool HasPendingAuction => auctionPending;
        public Canvas RenderCanvas => canvas;
        public string LastError { get; private set; }
        public string Folder { get; private set; }
        VillageGame game;
        Canvas canvas;
        Font font;
        RectTransform root, resultPane, resultContent;
        ScrollRect resultsScroll;
        Text status, hint, taxLabel;
        Task<AuctionResult> task;
        AuctionDocument displayed;
        AuctionScenario taskScenario, cachedScenario;
        AuctionResult cachedResult;
        int inputRevision, auctionRevision, taskRevision, cachedRevision = -1, displayedRevision = -1;
        string selected, detail;
        string importNote;
        bool plotsTab;
        bool auctionPending;
        float auctionAt;
        int openedFrame;
        readonly HashSet<string> invalid = new HashSet<string>();
        readonly Dictionary<string, Text> personLabels = new Dictionary<string, Text>();
        readonly Dictionary<string, Text> bidLabels = new Dictionary<string, Text>();
        readonly Dictionary<string, Text> taxLabels = new Dictionary<string, Text>();
        static readonly Color Ink = new Color(.12f, .18f, .17f);
        static readonly Color Muted = new Color(.36f, .43f, .43f);
        static readonly Color Background = new Color(.94f, .96f, .96f);
        static readonly Color Line = new Color(.82f, .87f, .86f);
        static readonly Color Green = new Color(.16f, .40f, .29f);
        static readonly Color Blue = new Color(.14f, .36f, .51f);
        static readonly Color Red = new Color(.68f, .19f, .22f);

        public void Initialize(VillageGame owner)
        {
            game = owner;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Folder = Path.Combine(Application.persistentDataPath, "EconomyAdmin");
            bool smoke = Environment.GetCommandLineArgs().Contains("-lg-auction-smoke") || Environment.GetCommandLineArgs().Contains("-lg-dossier-smoke");
            if (smoke)
            {
                var args = Environment.GetCommandLineArgs();
                int at = Array.IndexOf(args, "-lg-capture");
                if (at >= 0 && at + 1 < args.Length) Folder = Path.Combine(args[at + 1], "EconomyAdmin");
            }
            Scenario = IndividualLandValues.Example();
            if (!smoke && File.Exists(Path.Combine(Folder, "scenario.json")))
            {
                try { Scenario = ReadScenario(); } catch (Exception e) { LastError = "Saved scenario was not loaded: " + e.Message; }
            }
            selected = Scenario.Georgies.FirstOrDefault()?.Id;
            canvas = new GameObject("Economy admin", typeof(RectTransform)).AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            canvas.gameObject.AddComponent<GraphicRaycaster>();
            var scaler = canvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1440, 900);
            scaler.matchWidthOrHeight = 0;
            root = canvas.transform as RectTransform;
            Rebuild();
            canvas.gameObject.SetActive(false);
        }

        public void Open()
        {
            if (canvas == null) return;
            canvas.gameObject.SetActive(true);
            openedFrame = Time.frameCount;
            if (game.Desk != null) { game.Desk.StopRunning(); game.Desk.SetVisible(false); }
            else game.Hud.SetVisible(false);
            EventSystem.current.SetSelectedGameObject(null);
            if (Result == null && !auctionPending && !IsBusy && LastError == null) RunAuction();
        }
        public void Close()
        {
            canvas.gameObject.SetActive(false);
            if (game.Desk != null) game.Desk.SetVisible(true);
            else game.Hud.SetVisible(true);
            EventSystem.current.SetSelectedGameObject(null);
        }
        void Update()
        {
            if (task != null && task.IsCompleted)
            {
                var completed = task; task = null;
                try
                {
                    var solved = completed.GetAwaiter().GetResult();
                    // Edits remain enabled during a solve. Only the latest bid revision may be published.
                    if (taskRevision == auctionRevision)
                    {
                        cachedResult = solved; cachedScenario = taskScenario; cachedRevision = taskRevision;
                    }
                }
                catch (Exception e)
                {
                    if (taskRevision == auctionRevision) RefreshFailed(e);
                }
            }
            ProcessRefresh();
            if (!IsOpen) return;
            var focused = EventSystem.current.currentSelectedGameObject;
            bool editing = focused != null && focused.GetComponent<InputField>() != null;
            if (!editing && Time.frameCount > openedFrame && (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.F4))) Close();
        }
        void OnDestroy() { if (canvas != null) Destroy(canvas.gameObject); }

        void MarkChanged() => QueueRefresh(true);

        void QueueRefresh(bool bidsChanged)
        {
            inputRevision++;
            if (bidsChanged) auctionRevision++;
            LastError = null;
            auctionPending = true; auctionAt = Time.unscaledTime + .2f;
            SetStatus(Result == null ? "Computing results..." : "Updating results...");
        }

        void SetStatus(string message, bool error = false)
        {
            if (status == null) return;
            status.text = message + (string.IsNullOrEmpty(importNote) ? "" : "  |  " + importNote);
            status.color = error ? Red : Muted;
        }

        public void RunAuction()
        {
            cachedResult = null;
            auctionPending = true; auctionAt = Time.unscaledTime;
            ProcessRefresh();
        }

        void ProcessRefresh()
        {
            if (!auctionPending || IsBusy || Time.unscaledTime < auctionAt) return;
            try
            {
                if (invalid.Count > 0) throw new ArgumentException("Correct the highlighted values: whole apples from 0 to 1,000,000.");
                IndividualLandValues.Validate(Scenario); Taxes.Validate();
                if (cachedResult != null && cachedRevision == auctionRevision)
                {
                    var policy = new AuctionTaxPolicy { ValuePercent = Taxes.ValuePercent };
                    var snapshot = new AuctionDocument { FormatVersion = 6, Solved = true, Scenario = cachedScenario,
                        Result = cachedResult, Taxes = policy, TaxReport = AuctionTaxes.Calculate(cachedResult, policy) };
                    displayed = snapshot; displayedRevision = inputRevision;
                    auctionPending = false; LastError = null;
                    DrawResults();
                    SetStatus("Exact optimum  |  " + Result.Solves + " solves  |  " + Result.Milliseconds + " ms");
                    try { WriteSnapshot("last-run.json"); }
                    catch (Exception e) { SetStatus("Results updated; snapshot not saved: " + e.Message, true); }
                    return;
                }
                var inputs = JsonUtility.FromJson<AuctionScenario>(JsonUtility.ToJson(Scenario));
                taskScenario = inputs; taskRevision = auctionRevision;
                int valueTaxPercent = Taxes.ValuePercent;
                LastError = null;
                SetStatus(Result == null ? "Computing results..." : "Updating results...");
                task = Task.Run(() => LandAuction.SolveWithValueTax(inputs, valueTaxPercent));
            }
            catch (Exception e) { RefreshFailed(e); }
        }

        void RefreshFailed(Exception error)
        {
            auctionPending = false; LastError = error.Message;
            SetStatus(error.Message + (Result == null ? "" : " Previous results retained."), true);
        }

        void Rebuild()
        {
            Clear(root); personLabels.Clear(); bidLabels.Clear(); taxLabels.Clear();
            Stretch("Background", root, 0, 0, 0, 0, Background);
            var bar = Stretch("Auction header", root, 0, 0, 0, 0, Ink, 1, 1);
            bar.sizeDelta = new Vector2(0, 100); bar.pivot = new Vector2(.5f, 1);
            var portrait = At("Little Georgie", bar, 16, 15, 48, 48).gameObject.AddComponent<RawImage>();
            portrait.texture = Resources.Load<Texture2D>("Art/GeorgieIcon"); portrait.raycastTarget = false;
            Label("Little Georgies", bar, 76, 12, 300, 26, 22, Color.white, true);
            Label("ECONOMY ADMIN  /  LAND AUCTION", bar, 76, 42, 420, 24, 15, new Color(.70f, .83f, .79f));
            Label("Sandbox  |  Village paused", bar, 510, 18, 330, 30, 17, Color.white);
            Button("Run auction", bar, 814, 16, 144, RunAuction, Blue, "Auction run");
            Button("Save", bar, 970, 16, 80, Save, Green, "Auction save");
            Button("Load", bar, 1060, 16, 80, () => Confirm("Load the saved scenario? Unsaved edits will be replaced.", Load), Green, "Auction load");
            Button("Example", bar, 1150, 16, 104, () => Confirm("Replace these values with the example auction?", Example), Green, "Auction example");
            Button("Village", bar, 1266, 16, 154, Close, Green, "Auction close");
            Label("Bid = value minus tax. Surplus = bid minus rent. Amounts in apples.", bar, 18, 72, 1380, 22, 15, Color.white);

            var people = Stretch("Georgies", root, 16, 112, -1212, -74, Color.white);
            Label("Georgies", people, 14, 10, 175, 30, 22, Ink, true);
            Button("+ Add Georgie", people, 12, 48, 188, () =>
            {
                if (Scenario.Georgies.Count >= 16) { SetStatus("The admin limit is 16 Georgies.", true); return; }
                var g = AuctionScenario.Bidder(NewId("g", Scenario.Georgies.Select(p => p.Id)), "New Georgie");
                g.Alternatives.AddRange(Scenario.Plots.Select(p => AuctionScenario.Bid(p.Id, 0, p.Id)));
                Scenario.Georgies.Add(g); selected = detail = g.Id; plotsTab = false; MarkChanged(); Rebuild();
            }, Green, "Auction add Georgie");
            var list = Scroll(people, 0, 100, 0, 0, Scenario.Georgies.Count * 70);
            for (int i = 0; i < Scenario.Georgies.Count; i++)
            {
                var g = Scenario.Georgies[i];
                var b = Button("", list, 10, i * 70, 188, () => { selected = detail = g.Id; plotsTab = false; Rebuild(); },
                    g.Id == selected ? new Color(.84f, .92f, .88f) : Background, "Auction Georgie " + g.Id, 62);
                personLabels[g.Id] = Label(g.Name, b.transform as RectTransform, 10, 4, 164, 30, 18, Ink, true);
                Label(Scenario.Plots.Count + " plot values", b.transform as RectTransform, 10, 32, 164, 22, 14, Muted);
            }
            var editor = Stretch("Valuations", root, 244, 112, -612, -74, Color.white);
            Button("Values", editor, 14, 10, 104, () => { plotsTab = false; Rebuild(); }, plotsTab ? Muted : Green, "Auction values tab");
            Button("Plots (" + Scenario.Plots.Count + ")", editor, 126, 10, 112, () => { plotsTab = true; Rebuild(); }, plotsTab ? Blue : Muted, "Auction plots tab");
            if (plotsTab) DrawPlots(editor); else DrawValues(editor);
            resultPane = Stretch("Auction results", root, 844, 112, -16, -74, Color.white);
            DrawResults();
            var footer = Stretch("Auction status", root, 16, 0, -16, -8, Color.clear, 0, 0);
            footer.sizeDelta = new Vector2(-32, 56); footer.pivot = new Vector2(.5f, 0);
            status = Label("", footer, 0, 0, 1210, 30, 16, Muted);
            hint = Label("Saved inputs: scenario.json   |   Last solved inputs + results: last-run.json", footer, 0, 32, 1210, 20, 13, Muted);
            Button("Files", footer, 1304, 6, 88, () =>
            {
                Directory.CreateDirectory(Folder); Application.OpenURL(new Uri(Folder + Path.DirectorySeparatorChar).AbsoluteUri);
            }, Muted, "Auction files");
            SetStatus(LastError ?? (auctionPending || IsBusy ? "Updating results..." : IsResultCurrent ? "Exact optimum  |  " + Result.Milliseconds + " ms" : "Ready"), LastError != null);
        }

        void DrawValues(RectTransform parent)
        {
            DrawTaxControls(parent);
            var person = Scenario.Georgies.FirstOrDefault(g => g.Id == selected);
            if (person == null) { Label("No Georgies", parent, 16, 82, 520, 40, 22, Muted); return; }
            Label("Name", parent, 16, 62, 75, 30, 16, Muted);
            Field(person.Name, parent, 80, 62, 322, false, value =>
            {
                person.Name = value; if (personLabels.TryGetValue(person.Id, out var label)) label.text = value; MarkChanged();
            }, "Auction name " + person.Id, 32);
            Button("Remove", parent, 414, 62, 152, () => Confirm("Remove " + person.Name + " and all their plot values?", () =>
            {
                Scenario.Georgies.Remove(person); invalid.RemoveWhere(k => k.StartsWith(person.Id + ":", StringComparison.Ordinal));
                selected = Scenario.Georgies.FirstOrDefault()?.Id; detail = selected; MarkChanged(); Rebuild();
            }), Red, "Auction remove Georgie");
            Label("PLOT", parent, 16, 113, 222, 28, 14, Muted, true);
            Label("VALUE", parent, 246, 113, 94, 28, 14, Muted, true);
            Label("TAX", parent, 354, 113, 86, 28, 14, Muted, true);
            Label("BID", parent, 452, 113, 100, 28, 14, Muted, true);
            var rows = Scroll(parent, 10, 148, -10, -116, Scenario.Plots.Count * 58);
            for (int i = 0; i < Scenario.Plots.Count; i++)
            {
                var plot = Scenario.Plots[i];
                var bid = person.Alternatives.Single(b => b.Plots[0] == plot.Id);
                var row = At("Value for " + plot.Id, rows, 0, i * 58, 548, 54);
                row.gameObject.AddComponent<Image>().color = Background;
                Label(plot.Name, row, 8, 2, 222, 27, 18, Ink, true);
                Label(plot.Id + "  /  " + plot.Kind, row, 8, 29, 222, 23, 14, Muted);
                var input = Field(bid.Value.ToString(), row, 236, 10, 94, true, null, "Auction value " + person.Id + " " + plot.Id, 7);
                long tax = AuctionTaxes.TaxOnValue(bid.Value, Taxes.ValuePercent);
                taxLabels[plot.Id] = Label(tax.ToString(), row, 344, 10, 86, 36, 18, Ink);
                taxLabels[plot.Id].name = "Auction tax " + person.Id + " " + plot.Id;
                bidLabels[plot.Id] = Label((bid.Value - tax).ToString(), row, 442, 10, 98, 36, 18, Blue, true);
                bidLabels[plot.Id].name = "Auction bid " + person.Id + " " + plot.Id;
                string key = person.Id + ":" + plot.Id;
                input.onValueChanged.AddListener(text =>
                {
                    if (int.TryParse(text, out var value) && value >= 0 && value <= 1000000) { bid.Value = value; invalid.Remove(key); input.textComponent.color = Ink; }
                    else { invalid.Add(key); input.textComponent.color = Red; }
                    MarkChanged();
                    RefreshBids();
                });
                if (invalid.Contains(key)) { input.SetTextWithoutNotify(""); input.textComponent.color = Red; }
            }
            RefreshBids();
        }

        void RefreshBids()
        {
            var person = Scenario.Georgies.FirstOrDefault(g => g.Id == selected);
            if (person == null) return;
            foreach (var offer in person.Alternatives)
            {
                bool valid = !invalid.Contains(person.Id + ":" + offer.Plots[0]);
                long amount = AuctionTaxes.TaxOnValue(offer.Value, Taxes.ValuePercent);
                if (bidLabels.TryGetValue(offer.Plots[0], out var label))
                    label.text = valid ? (offer.Value - amount).ToString() : "-";
                if (taxLabels.TryGetValue(offer.Plots[0], out var tax))
                    tax.text = valid ? amount.ToString() : "-";
            }
        }

        void DrawTaxControls(RectTransform parent)
        {
            var panel = Stretch("Tax policy", parent, 16, 0, -16, -12, Color.white, 0, 0);
            panel.sizeDelta = new Vector2(-32, 94); panel.pivot = new Vector2(.5f, 0);
            taxLabel = Label("Tax rate: " + Taxes.ValuePercent + "%", panel, 0, 8, 530, 26, 17, Ink);
            TaxSlider(panel, 44, Taxes.ValuePercent, "Auction tax rate", value =>
            {
                Taxes.ValuePercent = value; taxLabel.text = "Tax rate: " + value + "%";
                MarkChanged(); RefreshBids();
            });
        }

        void TaxSlider(RectTransform parent, float y, int value, string name, Action<int> action)
        {
            var r = At(name, parent, 0, y, 540, 34);
            r.gameObject.AddComponent<Image>().color = Color.clear;
            var track = At("Track", r, 12, 14, 516, 6);
            track.gameObject.AddComponent<Image>().color = Line;
            var area = At("Handle area", r, 12, 0, 516, 34);
            var handle = At("Handle", area, 0, 0, 24, 0);
            handle.pivot = new Vector2(.5f, .5f);
            handle.gameObject.AddComponent<Image>().color = Blue;
            var slider = r.gameObject.AddComponent<Slider>();
            slider.minValue = 0; slider.maxValue = 100; slider.wholeNumbers = true;
            slider.handleRect = handle; slider.targetGraphic = handle.GetComponent<Image>();
            slider.SetValueWithoutNotify(value);
            slider.onValueChanged.AddListener(v => action(Mathf.RoundToInt(v)));
        }

        void DrawPlots(RectTransform parent)
        {
            Label("Land inventory", parent, 16, 65, 352, 32, 22, Ink, true);
            Button("+ Plot", parent, 424, 62, 142, () =>
            {
                if (Scenario.Plots.Count >= 12) { SetStatus("The admin limit is 12 plots.", true); return; }
                var plot = new LandPlot { Id = NewId("P", Scenario.Plots.Select(p => p.Id)), Name = "New plot", Kind = "Field" };
                Scenario.Plots.Add(plot);
                foreach (var person in Scenario.Georgies) person.Alternatives.Add(AuctionScenario.Bid(plot.Id, 0, plot.Id));
                MarkChanged(); Rebuild();
            }, Blue, "Auction add plot");
            Label("Each plot can be assigned to one Georgie, or left unused.", parent, 16, 112, 530, 40, 17, Muted);
            var rows = Scroll(parent, 12, 164, -12, -10, Scenario.Plots.Count * 128);
            for (int i = 0; i < Scenario.Plots.Count; i++)
            {
                var p = Scenario.Plots[i]; float y = i * 128;
                var row = At("Plot " + p.Id, rows, 0, y, 546, 118); row.gameObject.AddComponent<Image>().color = Background;
                Label(p.Id, row, 10, 8, 70, 36, 18, Ink, true);
                Field(p.Name, row, 86, 8, 344, false, value => { p.Name = value; MarkChanged(); }, "Auction plot name " + p.Id, 32);
                Button("x", row, 490, 8, 40, () => Confirm("Remove " + p.Name + " and its values for every Georgie?", () =>
                {
                    foreach (var g in Scenario.Georgies) g.Alternatives.RemoveAll(b => b.Plots.Contains(p.Id));
                    invalid.RemoveWhere(k => k.EndsWith(":" + p.Id, StringComparison.Ordinal));
                    Scenario.Plots.Remove(p); MarkChanged(); Rebuild();
                }), Red, "Auction remove plot " + p.Id, 36, "Remove this plot and its values");
                Choice(row, 10, 64, 240, new[] { "Residential", "Orchard", "Field" }, p.Kind, value => { p.Kind = value; MarkChanged(); }, "Auction plot kind " + p.Id);
                int count = Scenario.Georgies.Count(g => g.Alternatives.Any(b => b.Plots.Contains(p.Id) && b.Value > 0));
                Label(count + " positive values", row, 276, 64, 242, 36, 16, Muted);
            }
        }

        void DrawResults()
        {
            if (resultPane == null) return;
            float position = resultsScroll == null ? 1 : resultsScroll.verticalNormalizedPosition;
            var previous = resultContent;
            var next = Stretch("Displayed auction", resultPane, 0, 0, 0, 0, Color.white);
            next.gameObject.SetActive(false);
            var scroll = BuildResults(next);
            // Build the complete replacement offscreen, then swap it within this frame.
            next.gameObject.SetActive(true);
            if (previous != null) { previous.gameObject.SetActive(false); Destroy(previous.gameObject); }
            resultContent = next; resultsScroll = scroll;
            Canvas.ForceUpdateCanvases();
            if (resultsScroll != null) resultsScroll.verticalNormalizedPosition = position;
            Canvas.ForceUpdateCanvases();
        }

        ScrollRect BuildResults(RectTransform parent)
        {
            Label("Auction results", parent, 16, 12, 338, 32, 22, Ink, true);
            if (Result == null)
            {
                Label("Waiting for first results...", parent, 16, 72, 528, 40, 22, Muted);
                return null;
            }
            var taxes = TaxReport;
            int unallocated = displayed.Scenario.Plots.Count - Result.Allocation.Sum(a => a.Plots.Count);
            Label("Unallocated: " + unallocated, parent, 376, 14, 188, 30, 16, Muted);
            Metric(parent, "TOTAL VALUE", taxes.TotalValue.ToString(), 16, 58, Ink);
            Metric(parent, "TOTAL TAX", FineMoney(taxes.TotalTaxTenThousandths), 300, 58, Ink);
            Metric(parent, "TOTAL RENT", Money(taxes.TotalRentHundredths), 16, 138, Blue);
            Metric(parent, "SURPLUS", FineMoney(taxes.TotalSurplusTenThousandths), 300, 138, taxes.TotalSurplusTenThousandths < 0 ? Red : Green);
            var rows = Scroll(parent, 10, 226, -10, -12, 48 + Result.Georgies.Count * 48 + 530);
            var scroll = rows.GetComponentInParent<ScrollRect>(true);
            Label("GEORGIE", rows, 8, 0, 92, 28, 13, Muted, true);
            Label("PLOT", rows, 106, 0, 44, 28, 13, Muted, true);
            Label("VALUE", rows, 156, 0, 60, 28, 13, Muted, true);
            Label("TAX", rows, 222, 0, 70, 28, 13, Muted, true);
            Label("BID", rows, 298, 0, 64, 28, 13, Muted, true);
            Label("RENT", rows, 368, 0, 64, 28, 13, Muted, true);
            Label("SURPLUS", rows, 448, 0, 90, 28, 13, Muted, true);
            string requested = detail ?? selected;
            string current = Result.Georgies.Any(g => g.GeorgieId == requested) ? requested : Result.Georgies.FirstOrDefault()?.GeorgieId;
            for (int i = 0; i < Result.Georgies.Count; i++)
            {
                var r = Result.Georgies[i];
                var tax = taxes.Georgies.Single(g => g.GeorgieId == r.GeorgieId);
                var row = Button("", rows, 0, 34 + i * 48, 546, () => { detail = r.GeorgieId; DrawResults(); },
                    current == r.GeorgieId ? new Color(.84f, .92f, .96f) : r.Won ? new Color(.91f, .96f, .93f) : Background,
                    "Auction result " + r.GeorgieId, 42).transform as RectTransform;
                Label(PersonName(r.GeorgieId), row, 8, 0, 92, 42, 17, Ink, true);
                string plot = r.Won ? r.Award.Plots.Single() : "None";
                Label(plot, row, 106, 0, 44, 42, 16, Ink);
                Tip(row, PersonName(r.GeorgieId) + " | Plot: " + plot + " | Value " + tax.Value + " | Tax " + FineMoney(tax.TaxTenThousandths)
                    + " | Bid " + Money(tax.BidHundredths) + " | Rent " + Money(tax.RentHundredths)
                    + " | Surplus " + FineMoney(tax.SurplusTenThousandths));
                Label(tax.Value.ToString(), row, 156, 0, 60, 42, 17, Ink);
                Label(FineMoney(tax.TaxTenThousandths), row, 222, 0, 70, 42, 17, Ink).name = "Auction tax result " + r.GeorgieId;
                Label(Money(tax.BidHundredths), row, 298, 0, 64, 42, 17, Blue);
                Label(Money(tax.RentHundredths), row, 368, 0, 64, 42, 17, Blue, true);
                Label(FineMoney(tax.SurplusTenThousandths), row, 448, 0, 90, 42, 17, tax.SurplusTenThousandths < 0 ? Red : Green)
                    .name = "Auction surplus result " + r.GeorgieId;
            }
            float y = 54 + Result.Georgies.Count * 48;
            var person = Result.Georgies.FirstOrDefault(r => r.GeorgieId == current);
            if (person == null) { rows.sizeDelta = new Vector2(0, y); return scroll; }
            var personalTax = taxes.Georgies.Single(g => g.GeorgieId == current);
            Label(PersonName(person.GeorgieId) + " / bid, rent and tax", rows, 8, y, 532, 34, 21, Ink, true);
            Label("Bid: " + personalTax.Value + " - " + FineMoney(personalTax.TaxTenThousandths) + " = " + Money(personalTax.BidHundredths),
                rows, 8, y + 42, 526, 28, 17, Blue);
            Label("Others' best bids without " + PersonName(person.GeorgieId), rows, 8, y + 86, 388, 28, 17, Ink);
            Label(AuctionMoney(person.WithoutWelfare), rows, 408, y + 86, 126, 28, 18, Ink, true);
            Label("Others' bids in this allocation", rows, 8, y + 122, 388, 28, 17, Ink);
            Label("- " + AuctionMoney(person.OthersWelfare), rows, 408, y + 122, 126, 28, 18, Ink, true);
            var rule = At("Rent rule", rows, 8, y + 160, 526, 1).gameObject.AddComponent<Image>(); rule.color = Line;
            Label("Rent (external cost)", rows, 8, y + 170, 390, 30, 18, Blue, true);
            Label("= " + Money(personalTax.RentHundredths), rows, 408, y + 170, 126, 30, 21, Blue, true);
            Label("Tax: " + taxes.ValuePercent + "% of " + personalTax.Value + ", rounded = " + FineMoney(personalTax.TaxTenThousandths),
                rows, 8, y + 214, 526, 28, 17, Muted);
            Label("Surplus: " + personalTax.Value + " - " + FineMoney(personalTax.TaxTenThousandths) + " - " + Money(personalTax.RentHundredths)
                + " = " + FineMoney(personalTax.SurplusTenThousandths), rows, 8, y + 250, 526, 28, 17,
                personalTax.SurplusTenThousandths < 0 ? Red : Green, true);
            Label("ALLOCATION WITHOUT " + PersonName(person.GeorgieId).ToUpperInvariant() + " / BIDS", rows, 8, y + 300, 526, 26, 14, Muted, true);
            float cy = y + 338;
            foreach (var award in person.WithoutAllocation)
            {
                string description = PersonName(award.GeorgieId) + ": " + string.Join(", ", award.Plots);
                Label(description, rows, 8, cy, 388, 36, 17, Ink);
                Label(AuctionMoney(award.Bid), rows, 408, cy, 126, 36, 17, Ink); cy += 38;
            }
            if (person.WithoutAllocation.Count == 0) { Label("No land allocated", rows, 8, cy, 520, 30, 17, Muted); cy += 38; }
            if (unallocated > 0)
            {
                var assigned = Result.Allocation.SelectMany(a => a.Plots).ToHashSet();
                Label("Unallocated: " + string.Join(", ", displayed.Scenario.Plots.Where(p => !assigned.Contains(p.Id)).Select(p => p.Id)), rows, 8, cy + 16, 520, 60, 16, Muted);
                cy += 90;
            }
            rows.sizeDelta = new Vector2(0, cy + 12);
            return scroll;
        }

        void Metric(RectTransform parent, string title, string value, float x, float y, Color color)
        {
            Label(title, parent, x, y, 258, 22, 14, Muted, true);
            Label(value, parent, x, y + 28, 258, 40, 30, color, true);
        }
        static string Money(long hundredths) => (hundredths / 100m).ToString("0", System.Globalization.CultureInfo.InvariantCulture);
        static string FineMoney(long units) => (units / 10000m).ToString("0", System.Globalization.CultureInfo.InvariantCulture);
        string AuctionMoney(long units) => (units / (decimal)Result.AmountScale).ToString("0", System.Globalization.CultureInfo.InvariantCulture);

        string PersonName(string id) => displayed.Scenario.Georgies.First(g => g.Id == id).Name;
        static string NewId(string prefix, IEnumerable<string> taken)
        {
            var ids = new HashSet<string>(taken); int i = 1; while (ids.Contains(prefix + i)) i++; return prefix + i;
        }
        void Example()
        {
            Scenario = IndividualLandValues.Example(); Taxes = new AuctionTaxPolicy(); importNote = null;
            invalid.Clear(); selected = detail = Scenario.Georgies[0].Id;
            plotsTab = false; MarkChanged(); Rebuild(); RunAuction();
        }
        void Save()
        {
            try
            {
                if (invalid.Count > 0) throw new ArgumentException("Correct invalid values before saving.");
                IndividualLandValues.Validate(Scenario); Taxes.Validate(); WriteSnapshot("scenario.json"); SetStatus("Saved scenario.json  |  " + Folder);
            }
            catch (Exception e) { SetStatus("Save failed: " + e.Message, true); }
        }
        void Load()
        {
            try
            {
                var candidate = ReadScenario(); Scenario = candidate; invalid.Clear(); selected = detail = Scenario.Georgies.FirstOrDefault()?.Id;
                MarkChanged(); Rebuild(); RunAuction();
            }
            catch (Exception e) { SetStatus("Load failed; current edits kept: " + e.Message, true); }
        }
        AuctionScenario ReadScenario()
        {
            string path = Path.Combine(Folder, "scenario.json");
            if (new FileInfo(path).Length > 1024 * 1024) throw new ArgumentException("Scenario file exceeds 1 MB.");
            var doc = JsonUtility.FromJson<AuctionDocument>(File.ReadAllText(path));
            if (doc?.Scenario == null) throw new ArgumentException("File needs a Scenario object.");
            doc.Scenario.Validate();
            var policy = doc.Taxes ?? new AuctionTaxPolicy(); policy.Validate();
            int omitted = 0;
            if (doc.FormatVersion == 0 || doc.FormatVersion == 1) omitted = IndividualLandValues.Normalize(doc.Scenario);
            else if (doc.FormatVersion < 2 || doc.FormatVersion > 6) throw new ArgumentException("Unsupported auction document version.");
            IndividualLandValues.Validate(doc.Scenario);
            importNote = omitted == 0 ? null : "Excluded " + omitted + " combination bids; original saved file kept.";
            Taxes = policy;
            return doc.Scenario;
        }
        public void WriteSnapshot(string filename)
        {
            Directory.CreateDirectory(Folder);
            string path = Path.Combine(Folder, filename);
            string temp = path + ".tmp";
            File.WriteAllText(temp, JsonUtility.ToJson(new AuctionDocument { FormatVersion = 6, Solved = IsResultCurrent,
                Scenario = Scenario, Result = IsResultCurrent ? Result : null, Taxes = Taxes, TaxReport = IsResultCurrent ? TaxReport : null }, true));
            if (File.Exists(path)) File.Replace(temp, path, path + ".bak"); else File.Move(temp, path);
        }
        void Confirm(string title, Action action)
        {
            var shade = Stretch("Auction confirmation", root, 0, 0, 0, 0, new Color(0, 0, 0, .5f));
            var box = At("Confirm", shade, 440, 230, 560, 210); box.gameObject.AddComponent<Image>().color = Color.white;
            Label(title, box, 24, 16, 512, 95, 22, Ink);
            Button("Cancel", box, 24, 144, 150, () => { shade.gameObject.SetActive(false); Destroy(shade.gameObject); }, Muted, "Auction cancel");
            Button("Confirm", box, 384, 144, 150, () => { shade.gameObject.SetActive(false); Destroy(shade.gameObject); action(); }, Green, "Auction confirm");
        }

        static void Clear(RectTransform parent)
        {
            foreach (Transform child in parent) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
        }
        RectTransform At(string name, RectTransform parent, float x, float y, float w, float h)
        {
            var r = new GameObject(name, typeof(RectTransform)).transform as RectTransform;
            r.SetParent(parent, false); r.anchorMin = r.anchorMax = r.pivot = new Vector2(0, 1);
            r.anchoredPosition = new Vector2(x, -y); r.sizeDelta = new Vector2(w, h); return r;
        }
        RectTransform Stretch(string name, RectTransform parent, float left, float top, float right, float bottom, Color color, float minY = 0, float maxY = 1)
        {
            var r = At(name, parent, 0, 0, 0, 0); r.anchorMin = new Vector2(0, minY); r.anchorMax = new Vector2(1, maxY);
            r.offsetMin = new Vector2(left, -bottom); r.offsetMax = new Vector2(right, -top);
            var image = r.gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = color.a > 0; return r;
        }
        Text Label(string value, RectTransform parent, float x, float y, float w, float h, int size, Color color, bool bold = false)
        {
            var t = At("Text", parent, x, y, w, h).gameObject.AddComponent<Text>();
            t.font = font; t.fontSize = size; t.color = color; t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            t.supportRichText = false; t.text = value; t.alignment = TextAnchor.MiddleLeft; t.raycastTarget = false;
            t.resizeTextForBestFit = true; t.resizeTextMinSize = Math.Min(10, size); t.resizeTextMaxSize = size;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Truncate; return t;
        }
        Button Button(string title, RectTransform parent, float x, float y, float w, Action action, Color color, string name, float h = 40, string tooltip = null)
        {
            var r = At(name, parent, x, y, w, h); var image = r.gameObject.AddComponent<Image>(); image.color = color;
            var button = r.gameObject.AddComponent<Button>(); button.targetGraphic = image; button.onClick.AddListener(() => action());
            var text = Label(title, r, 4, 0, w - 8, h, 17, Color.white, true); text.alignment = TextAnchor.MiddleCenter;
            if (tooltip != null) Tip(r, tooltip); return button;
        }
        void Tip(RectTransform r, string text)
        {
            var events = r.gameObject.AddComponent<EventTrigger>();
            var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter }; enter.callback.AddListener(_ => { if (hint != null) hint.text = text; });
            var leave = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit }; leave.callback.AddListener(_ => { if (hint != null) hint.text = "Saved inputs: scenario.json   |   Last solved inputs + results: last-run.json"; });
            events.triggers.Add(enter); events.triggers.Add(leave);
        }
        InputField Field(string value, RectTransform parent, float x, float y, float w, bool number, Action<string> action, string name, int limit)
        {
            var r = At(name, parent, x, y, w, 36); var bg = r.gameObject.AddComponent<Image>(); bg.color = Color.white;
            var outline = r.gameObject.AddComponent<Outline>(); outline.effectColor = Line; outline.effectDistance = Vector2.one;
            var field = r.gameObject.AddComponent<InputField>(); field.targetGraphic = bg;
            field.textComponent = Label("", r, 8, 0, w - 16, 36, 18, Ink);
            field.contentType = number ? InputField.ContentType.IntegerNumber : InputField.ContentType.Standard;
            field.characterLimit = limit; field.text = value; field.caretColor = Ink;
            if (action != null) field.onValueChanged.AddListener(text => action(text)); return field;
        }
        RectTransform Scroll(RectTransform parent, float left, float top, float right, float bottom, float height)
        {
            var r = Stretch("Scroll", parent, left, top, right, bottom, Color.white);
            var scroll = r.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = false; scroll.scrollSensitivity = 34;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            var viewport = Stretch("Viewport", r, 0, 0, -12, 0, Color.white); viewport.gameObject.AddComponent<RectMask2D>();
            var content = At("Content", viewport, 0, 0, 0, height); content.anchorMax = new Vector2(1, 1); content.sizeDelta = new Vector2(0, height);
            scroll.viewport = viewport; scroll.content = content;
            var track = Stretch("Scrollbar", r, 0, 0, 0, 0, Background); track.anchorMin = new Vector2(1, 0); track.offsetMin = new Vector2(-8, 0);
            var handle = Stretch("Handle", track, 0, 0, 0, 0, Line);
            var scrollbar = track.gameObject.AddComponent<Scrollbar>(); scrollbar.handleRect = handle; scrollbar.targetGraphic = handle.GetComponent<Image>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop; scroll.verticalScrollbar = scrollbar;
            return content;
        }
        void Choice(RectTransform parent, float x, float y, float w, string[] options, string selectedValue, Action<string> action, string name)
        {
            var r = At(name, parent, x, y, w, 36); r.gameObject.AddComponent<Image>().color = Blue;
            var dropdown = r.gameObject.AddComponent<Dropdown>();
            dropdown.captionText = Label("", r, 10, 0, w - 42, 36, 17, Color.white);
            Label("v", r, w - 26, 0, 20, 36, 17, Color.white);
            var template = At("Template", r, 0, -116, w, 112); template.gameObject.AddComponent<Image>().color = Color.white;
            var scroll = template.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = false;
            var viewport = Stretch("Viewport", template, 0, 0, 0, 0, Color.white); viewport.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            var content = At("Content", viewport, 0, 0, w, 36);
            var item = At("Item", content, 0, 0, w, 36); var image = item.gameObject.AddComponent<Image>(); image.color = Color.white;
            var toggle = item.gameObject.AddComponent<Toggle>(); toggle.targetGraphic = image;
            dropdown.itemText = Label("Option", item, 10, 0, w - 20, 36, 17, Ink);
            scroll.viewport = viewport; scroll.content = content; dropdown.template = template;
            dropdown.AddOptions(options.ToList()); dropdown.SetValueWithoutNotify(Array.IndexOf(options, selectedValue)); template.gameObject.SetActive(false);
            dropdown.onValueChanged.AddListener(i => action(options[i]));
        }
    }
}
