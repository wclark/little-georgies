using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace LittleGeorgies
{
    public sealed class VillageHud : MonoBehaviour
    {
        VillageGame game;
        Font font;
        Text day, totals, notice, ledger, inspectorText, phase, policyNote, taxLabel, earlyLesson, basketWork;
        RectTransform canvas, policies, inspector, foodPanel, commonPanel, starterPanel, message;
        Button pauseButton, censusButton, policyButton;
        Image dayFill;
        Dropdown foodChoice, buildChoice;
        Toggle rest, relief, baskets, homes;
        Slider levy;
        bool refreshing;
        bool census;
        float refreshAt;
        static readonly Color Ink = new Color(.09f, .16f, .13f);
        static readonly Color Paper = new Color(.96f, .98f, .95f, .97f);
        static readonly Color White = new Color(.96f, .98f, .95f);
        static readonly Color Accent = new Color(.25f, .48f, .34f);
        public Canvas RenderCanvas => canvas.GetComponent<Canvas>();
        public void SetVisible(bool visible) => canvas.gameObject.SetActive(visible);

        public void Initialize(VillageGame owner)
        {
            game = owner;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var c = new GameObject("Settlement HUD").AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 10;
            c.gameObject.AddComponent<GraphicRaycaster>();
            var scaler = c.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.matchWidthOrHeight = .5f;
            canvas = c.transform as RectTransform;
            var events = new GameObject("Input events").AddComponent<EventSystem>();
            events.gameObject.AddComponent<StandaloneInputModule>();

            var bar = Panel("Topbar", canvas, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -84), Vector2.zero, Ink);
            Label("Little Georgies", bar, 24, 8, 242, 32, 28, White, FontStyle.Bold);
            var economy = Button("Economy admin", bar, 24, 46, 220, () => game.Auction.Open());
            (economy.transform as RectTransform).sizeDelta = new Vector2(220, 28);
            economy.GetComponentInChildren<Text>().rectTransform.sizeDelta = new Vector2(212, 28);
            economy.GetComponentInChildren<Text>().fontSize = 15;
            day = Label("", bar, 278, 15, 430, 26, 21, White, FontStyle.Bold);
            totals = Label("", bar, 278, 43, 630, 24, 17, White);
            var time = Panel("Day progress", bar, Vector2.zero, Vector2.one, new Vector2(0, 0), new Vector2(0, -79), Accent);
            dayFill = Panel("Elapsed", time, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(.93f, .73f, .28f)).GetComponent<Image>();
            var controls = Panel("Time controls", bar, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-466, -67), new Vector2(-18, -15), Color.clear);
            pauseButton = Button("Pause", controls, 0, 3, 90, () => { game.Paused = !game.Paused; Refresh(); });
            Button("1x", controls, 98, 3, 54, () => SetSpeed(1));
            Button("3x", controls, 158, 3, 54, () => SetSpeed(3));
            Button("6x", controls, 218, 3, 54, () => SetSpeed(6));
            censusButton = Button("Census", controls, 280, 3, 78, () => { census = !census; game.Selected = 0; Refresh(); });
            policyButton = Button("Policies", controls, 366, 3, 82, () => policies.gameObject.SetActive(!policies.gameObject.activeSelf));

            policies = Panel("Policy desk", canvas, Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, 190), Paper);
            var work = Column(policies, "Work", 0, .30f);
            Label("WORK & REST", work, 20, 12, 230, 24, 17, Ink, FontStyle.Bold);
            rest = Toggle("Rest when tired and fed", work, 20, 42, 340, value => Change(p => p.RestWhenFed = value));
            buildChoice = Choice(work, 20, 94, 310, new[] { "Builders: make baskets", "Builders: build houses" }, value => Change(p => p.BuildHomes = value == 1));
            basketWork = Label("Builders are weaving baskets", work, 20, 94, 340, 42, 17, Ink);
            policyNote = Label("Policies begin next dawn", work, 20, 150, 400, 28, 15, Ink);

            var food = Column(policies, "Food", .30f, .64f);
            foodPanel = food;
            Label("FOOD & LEVY", food, 16, 12, 230, 24, 17, Ink, FontStyle.Bold);
            foodChoice = Choice(food, 16, 42, 300, new[] { "Shared apple store", "Personal harvests" }, value => Change(p => p.Food = (FoodRule)value));
            taxLabel = Label("Levy", food, 16, 98, 120, 30, 17, Ink);
            levy = MakeSlider(food, 130, 102, 180, value => Change(p => p.LevyPercent = Mathf.RoundToInt(value)));
            relief = Toggle("Use common apples for hungry Georgies", food, 16, 138, 450, value => Change(p => p.FeedHungry = value));

            var common = Column(policies, "Common goods", .64f, 1);
            commonPanel = common;
            Label("COMMON GOODS", common, 16, 12, 250, 24, 17, Ink, FontStyle.Bold);
            baskets = Toggle("Give baskets to unequipped farmers", common, 16, 42, 460, value => Change(p => p.AssignBaskets = value));
            homes = Toggle("Give homes to unhoused Georgies", common, 16, 91, 460, value => Change(p => p.AssignHomes = value));
            Button("New settlement", common, 16, 142, 156, () => ConfirmReset(false));
            Button("Village sandbox", common, 180, 142, 160, () => ConfirmReset(true)).gameObject.SetActive(game.DebugMode);

            starterPanel = Column(policies, "Early settlement", .30f, 1);
            Label("DAILY LIFE", starterPanel, 16, 12, 230, 24, 17, Ink, FontStyle.Bold);
            earlyLesson = Label("", starterPanel, 16, 40, 660, 48, 17, Ink);
            var restart = Panel("Restart controls", starterPanel, new Vector2(1, 0), Vector2.one, new Vector2(-190, 0), Vector2.zero, Color.clear);
            Button("New settlement", restart, 12, 42, 156, () => ConfirmReset(false));

            message = Panel("Daily ledger", canvas, new Vector2(0, 0), new Vector2(1, 0), new Vector2(18, 200), new Vector2(-18, 258), new Color(.09f, .16f, .13f, .87f));
            ledger = Label("", message, 14, 7, 1040, 44, 17, White);
            notice = Label("", canvas, 24, 101, 730, 35, 19, Ink, FontStyle.Bold);
            phase = Label("", canvas, 24, 139, 900, 46, 16, Ink);

            inspector = Panel("Georgie inspector", canvas, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-356, -454), new Vector2(-18, -102), Paper);
            inspectorText = Label("", inspector, 18, 18, 298, 270, 18, Ink);
            inspectorText.alignment = TextAnchor.UpperLeft;
            Button("Close", inspector, 18, 294, 92, () => { game.Selected = 0; census = false; Refresh(); });
            inspector.gameObject.SetActive(false);
            Refresh();
        }

        RectTransform Column(RectTransform parent, string name, float left, float right) =>
            Panel(name, parent, new Vector2(left, 0), new Vector2(right, 1), Vector2.zero, Vector2.zero, Color.clear);

        void ConfirmReset(bool village)
        {
            game.Paused = true;
            var dialog = Panel("New settlement confirmation", canvas, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0, 0, 0, .55f));
            var box = Panel("Confirmation", dialog, Vector2.one * .5f, Vector2.one * .5f, new Vector2(-250, -95), new Vector2(250, 95), Paper);
            Label(village ? "Start a village sandbox?" : "Start again with one Georgie?", box, 22, 18, 454, 35, 22, Ink, FontStyle.Bold);
            Label("This replaces the current unsaved settlement.", box, 22, 60, 454, 30, 17, Ink);
            Button("Cancel", box, 22, 119, 120, () => { Destroy(dialog.gameObject); Refresh(); });
            Button("Start", box, 320, 119, 140, () => { Destroy(dialog.gameObject); game.ResetSettlement(village); census = false; Refresh(); });
        }

        void SetSpeed(int speed) { game.Speed = speed; game.Paused = false; Refresh(); }
        void Change(Action<Policies> change)
        {
            if (refreshing) return;
            change(game.Society.Policy);
            Refresh();
        }
        void Update() { if (game != null && Time.unscaledTime >= refreshAt) { refreshAt = Time.unscaledTime + .2f; Refresh(); } }

        public void Refresh()
        {
            if (day == null) return;
            var s = game.Society;
            refreshing = true;
            float deskHeight = s.Specialist ? 190 : 104;
            policies.offsetMax = new Vector2(0, deskHeight);
            float messageBottom = policies.gameObject.activeSelf ? deskHeight + 10 : 18;
            message.offsetMin = new Vector2(18, messageBottom);
            message.offsetMax = new Vector2(-18, messageBottom + 58);
            foodPanel.gameObject.SetActive(s.Specialist);
            commonPanel.gameObject.SetActive(s.Specialist);
            starterPanel.gameObject.SetActive(!s.Specialist);
            buildChoice.gameObject.SetActive(s.Specialist && s.HousingUnlocked);
            basketWork.gameObject.SetActive(s.Specialist && !s.HousingUnlocked);
            homes.gameObject.SetActive(s.HousingUnlocked);
            policyNote.gameObject.SetActive(s.Specialist);
            censusButton.gameObject.SetActive(s.People.Count > 1);
            policyButton.GetComponentInChildren<Text>().text = s.Specialist ? "Policies" : "Routine";
            day.text = $"Day {s.Report.Day}  /  {game.DayPhase}  /  {(game.Paused ? "Paused" : game.Speed + "x")}";
            totals.text = $"{s.People.Count} {(s.People.Count == 1 ? "Georgie" : "Georgies")}     {s.AllApples} apples     {Math.Round(s.HappyRate * 100)}% happy";
            dayFill.rectTransform.anchorMax = new Vector2(game.Progress, 1);
            pauseButton.GetComponentInChildren<Text>().text = game.Paused ? "Play" : "Pause";
            policyNote.text = s.HousingUnlocked ? "Policies begin next dawn" : "Baskets first; homes come next.";
            earlyLesson.text = s.People.Any(p => p.Mood == Mood.Broken)
                ? "An unfed Georgie becomes broken and must keep working."
                : s.People.Any(p => p.Mood == Mood.Tired)
                    ? "Eat an apple and rest in the grass to become happy again."
                    : "Pick apples, eat one each day, and sleep under the trees.";
            rest.SetIsOnWithoutNotify(s.Policy.RestWhenFed);
            foodChoice.SetValueWithoutNotify((int)s.Policy.Food);
            buildChoice.SetValueWithoutNotify(s.Policy.BuildHomes ? 1 : 0);
            levy.SetValueWithoutNotify(s.Policy.LevyPercent);
            taxLabel.text = $"Levy {s.Policy.LevyPercent}%";
            relief.SetIsOnWithoutNotify(s.Policy.FeedHungry);
            baskets.SetIsOnWithoutNotify(s.Policy.AssignBaskets);
            homes.SetIsOnWithoutNotify(s.Policy.AssignHomes);
            foodChoice.interactable = s.Specialist;
            buildChoice.interactable = s.HousingUnlocked;
            levy.interactable = s.Specialist && s.Policy.Food == FoodRule.PersonalHarvest;
            relief.interactable = s.Specialist && s.Policy.Food == FoodRule.PersonalHarvest;
            baskets.interactable = homes.interactable = s.Specialist;
            notice.text = s.StageName;
            phase.text = s.Specialist
                ? $"Common apples {s.CommonApples}  /  Baskets available {s.FreeBaskets}"
                    + (s.HousingUnlocked ? $"  /  Homes {s.TotalHomes}  /  House work {s.HouseProgress}/35" : "  /  Equip the farmers to unlock homes")
                : $"{(s.People.Count < 5 ? "Next arrival" : "Specialties")}: {s.TotalHarvest}/{s.NextGrowthTarget} apples gathered; at least 50% happy.";
            ledger.text = s.Ledger.FirstOrDefault() ?? "";
            inspector.gameObject.SetActive(census || game.Selected != 0);
            if (census)
                inspectorText.text = "THE SETTLEMENT\n\n" + string.Join("\n", s.People.Select(p => $"{p.Title}: {p.Mood}, {p.Apples} apples"));
            else if (game.Selected != 0)
            {
                var p = s.People.FirstOrDefault(p => p.Id == game.Selected);
                if (p != null)
                {
                    var activity = game.View.Actors[p.Id].Activity;
                    inspectorText.text = $"{p.Title}\n{p.Role}  /  {p.Mood}\n\n{activity}\n\nHappy: {Math.Round(p.HappyRate * 100)}%"
                        + (s.Specialist ? $"\nPersonal apples: {p.Apples}\nBasket: {(p.Basket ? "equipped" : "none")}" : $"\nApples: {s.AllApples}")
                        + (s.HousingUnlocked ? $"\nHome: {(p.House ? "housed" : "none")}" : "");
                }
            }
            refreshing = false;
        }

        RectTransform Panel(string name, RectTransform parent, Vector2 min, Vector2 max, Vector2 lo, Vector2 hi, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = go.transform as RectTransform;
            rect.SetParent(parent, false); rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = lo; rect.offsetMax = hi;
            go.GetComponent<Image>().color = color;
            go.GetComponent<Image>().raycastTarget = color.a > 0;
            return rect;
        }
        RectTransform At(string name, RectTransform parent, float x, float y, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = go.transform as RectTransform;
            rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(w, h);
            return rect;
        }
        Text Label(string text, RectTransform parent, float x, float y, float w, float h, int size, Color color, FontStyle style = FontStyle.Normal)
        {
            var t = At(text.Length < 40 ? text : "Label", parent, x, y, w, h).gameObject.AddComponent<Text>();
            t.font = font; t.fontSize = size; t.color = color; t.fontStyle = style; t.text = text;
            t.alignment = TextAnchor.MiddleLeft; t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Truncate;
            return t;
        }
        Button Button(string title, RectTransform parent, float x, float y, float w, Action action)
        {
            var rect = At(title, parent, x, y, w, 42);
            rect.gameObject.AddComponent<Image>().color = Accent;
            var button = rect.gameObject.AddComponent<Button>();
            button.onClick.AddListener(() => action());
            var t = Label(title, rect, 4, 0, w - 8, 42, 17, White, FontStyle.Bold); t.alignment = TextAnchor.MiddleCenter;
            return button;
        }
        Toggle Toggle(string title, RectTransform parent, float x, float y, float w, Action<bool> action)
        {
            var rect = At(title, parent, x, y, w, 42);
            rect.gameObject.AddComponent<Image>().color = Color.clear;
            var check = Panel("Checkbox", rect, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(0, -13), new Vector2(26, 13), Accent);
            var tick = Label("\u2713", check, 0, 0, 26, 26, 18, White, FontStyle.Bold); tick.alignment = TextAnchor.MiddleCenter;
            var toggle = rect.gameObject.AddComponent<Toggle>(); toggle.targetGraphic = check.GetComponent<Image>(); toggle.graphic = tick;
            Label(title, rect, 36, 0, w - 36, 42, 17, Ink);
            toggle.onValueChanged.AddListener(value => action(value));
            return toggle;
        }
        Slider MakeSlider(RectTransform parent, float x, float y, float w, Action<float> action)
        {
            var rect = At("Levy slider", parent, x, y, w, 32);
            Panel("Track", rect, new Vector2(0, .4f), new Vector2(1, .6f), Vector2.zero, Vector2.zero, Accent);
            var area = Panel("Handle area", rect, Vector2.zero, Vector2.one, new Vector2(12, 0), new Vector2(-12, 0), Color.clear);
            var handle = Panel("Handle", area, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(-12, -16), new Vector2(12, 16), Ink);
            var slider = rect.gameObject.AddComponent<Slider>(); slider.handleRect = handle; slider.targetGraphic = handle.GetComponent<Image>();
            slider.minValue = 0; slider.maxValue = 50; slider.wholeNumbers = true; slider.onValueChanged.AddListener(value => action(value));
            return slider;
        }
        Dropdown Choice(RectTransform parent, float x, float y, float w, string[] options, Action<int> action)
        {
            var rect = At(options[0], parent, x, y, w, 42);
            rect.gameObject.AddComponent<Image>().color = Accent;
            var dropdown = rect.gameObject.AddComponent<Dropdown>();
            dropdown.captionText = Label(options[0], rect, 12, 0, w - 42, 42, 17, White);
            Label("v", rect, w - 28, 0, 20, 42, 17, White);
            var template = Panel("Template", rect, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 42), new Vector2(0, 146), Paper);
            template.pivot = new Vector2(.5f, 0);
            var scroll = template.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = false;
            var viewport = Panel("Viewport", template, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Paper);
            viewport.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            var content = Panel("Content", viewport, new Vector2(0, 1), Vector2.one, new Vector2(0, -46), Vector2.zero, Color.clear);
            content.pivot = new Vector2(.5f, 1);
            var item = Panel("Item", content, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Paper);
            var toggle = item.gameObject.AddComponent<Toggle>(); toggle.targetGraphic = item.GetComponent<Image>();
            dropdown.itemText = Label("Option", item, 12, 0, w - 24, 46, 17, Ink);
            scroll.viewport = viewport; scroll.content = content;
            dropdown.template = template; dropdown.AddOptions(options.ToList());
            template.gameObject.SetActive(false);
            dropdown.onValueChanged.AddListener(value => action(value));
            return dropdown;
        }
    }
}
