using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using LittleGeorgies.Economy;

namespace LittleGeorgies
{
    public sealed partial class VillageGame : MonoBehaviour
    {
        public Society Society { get; private set; }
        public VillageView View { get; private set; }
        public VillageHud Hud { get; private set; }
        public AuctionDesk Auction { get; private set; }
        public bool Paused;
        public int Speed = 1;
        public int Selected;
        public bool DebugMode { get; private set; }
        public float Progress => dayClock / 32f;
        public string DayPhase => Progress < .2f ? "Morning" : Progress < .54f ? "At work" : Progress < .75f ? (Society.Specialist ? "Deliveries" : "Back to the clearing") : Progress < .9f ? "Dinner" : "Evening";
        float dayClock, elapsed;
        bool fed;
        bool captureMode;

        void Start()
        {
            Application.targetFrameRate = 60;
            captureMode = Args().Contains("-lg-smoke") || Args().Contains("-lg-opening-smoke") || Args().Contains("-lg-auction-smoke");
            DebugMode = captureMode || Args().Contains("-lg-debug") || Args().Contains("-lg-village");
            ResetSettlement(Args().Contains("-lg-village"));
            Hud = gameObject.AddComponent<VillageHud>();
            Hud.Initialize(this);
            Auction = gameObject.AddComponent<AuctionDesk>();
            Auction.Initialize(this);
            var build = BuildInfo.Load();
            Debug.Log("LITTLE_GEORGIES_BUILD_INFO: " + JsonUtility.ToJson(build));
            if (Args().Contains("-lg-economy") || (!captureMode && build.openEconomy)) Auction.Open();
            if (captureMode) StartCoroutine(Args().Contains("-lg-auction-smoke") ? AuctionSmokeRun() : Args().Contains("-lg-opening-smoke") ? OpeningSmokeRun() : SmokeRun());
        }

        public void ResetSettlement(bool village)
        {
            if (View != null) { View.gameObject.SetActive(false); Destroy(View.gameObject); }
            View = new GameObject("Illustrated village").AddComponent<VillageView>();
            View.Initialize();
            Society = Society.Create(village);
            View.Sync(Society);
            dayClock = elapsed = 0;
            fed = false;
            Paused = false;
            Selected = 0;
        }

        void Update()
        {
            if (Society == null) return;
            if (Auction != null && Auction.IsOpen) return;
            if (Input.GetKeyDown(KeyCode.F4)) { Auction.Open(); return; }
            if (Input.GetKeyDown(KeyCode.Space)) Paused = !Paused;
            if (Input.GetKeyDown(KeyCode.Escape)) { Selected = 0; Hud.Refresh(); }
            if (Input.GetKeyDown(KeyCode.F3)) ExportDebug();
            if (Input.GetMouseButtonDown(0) && !EventSystem.current.IsPointerOverGameObject()
                && !(Input.touchCount > 0 && EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId)))
            {
                var point = View.Camera.ScreenToWorldPoint(Input.mousePosition);
                var hits = Physics2D.OverlapPointAll(point);
                var actor = hits.Select(h => h.GetComponent<GeorgieActor>()).Where(a => a != null).OrderBy(a => a.transform.position.y).FirstOrDefault();
                Selected = actor == null ? 0 : actor.Person.Id;
                Hud.Refresh();
            }
            float dt = Paused ? 0 : Mathf.Min(Time.unscaledDeltaTime, .1f) * Speed;
            dayClock += dt;
            elapsed += dt;
            if (Progress >= .75f) Society.Produce();
            if (Progress >= .9f && !fed) { Society.FinishDay(); View.Sync(Society); fed = true; }
            if (dayClock >= 32)
            {
                dayClock -= 32;
                Society.BeginDay();
                fed = false;
                View.Sync(Society);
            }
            View.Tick(Society, Progress, elapsed, dt, Selected);
        }

        string[] Args() => Environment.GetCommandLineArgs();
        public void ExportDebug()
        {
            string folder = Path.Combine(Application.persistentDataPath, "Debug");
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "society.json"), JsonUtility.ToJson(Society, true));
            Society.Note("Debug snapshot exported: " + folder);
        }

        IEnumerator SmokeRun()
        {
            int index = Array.IndexOf(Args(), "-lg-capture");
            string folder = index >= 0 ? Args()[index + 1] : Path.Combine(Application.persistentDataPath, "Smoke");
            Directory.CreateDirectory(folder);
            Speed = 3;
            yield return new WaitForSecondsRealtime(1);
            var pause = FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Single(b => b.name == "Pause");
            PointerClick(pause.gameObject);
            bool pauseWorked = Paused;
            PointerClick(pause.gameObject);
            yield return new WaitForSecondsRealtime(2);
            bool morningCaptured = CaptureFrame(Path.Combine(folder, "village-morning.png"));
            var food = FindObjectsByType<Dropdown>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Single(d => d.options[0].text == "Shared apple store");
            PointerClick(food.gameObject);
            yield return new WaitForSecondsRealtime(.2f);
            bool menuCaptured = CaptureFrame(Path.Combine(folder, "policy-menu.png"));
            var personal = FindObjectsByType<Toggle>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Single(t => t.GetComponentsInChildren<Text>().Any(label => label.text == "Personal harvests"));
            PointerClick(personal.gameObject);
            bool policyControls = Society.Policy.Food == FoodRule.PersonalHarvest && Society.Today.Food == FoodRule.SharedStore;
            var rest = FindObjectsByType<Toggle>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Single(t => t.name == "Rest when tired and fed");
            PointerClick(rest.gameObject);
            policyControls &= !Society.Policy.RestWhenFed;
            PointerClick(rest.gameObject);
            var slider = FindFirstObjectByType<Slider>();
            slider.value = 37;
            policyControls &= Society.Policy.LevyPercent == 37;
            food.value = 0;
            slider.value = 25;
            yield return new WaitForSecondsRealtime(4.8f);
            Selected = Society.People[1].Id;
            Hud.Refresh();
            bool deliveriesCaptured = CaptureFrame(Path.Combine(folder, "village-deliveries.png"));
            yield return new WaitForSecondsRealtime(6);
            bool moved = View.Actors.Values.All(a => a.DistanceWalked > 1 && a.FrameChanges > 2);
            bool advanced = Society.Day >= 2;
            File.WriteAllText(Path.Combine(folder, "society.json"), JsonUtility.ToJson(Society, true));
            var report = new SmokeReport { Passed = pauseWorked && !Paused && moved && advanced && morningCaptured && deliveriesCaptured && menuCaptured && policyControls, PolicyControls = policyControls, FramesCaptured = morningCaptured && deliveriesCaptured && menuCaptured, PauseControl = pauseWorked, AllActorsMovedAndAnimated = moved, DayAdvanced = advanced,
                Day = Society.Day, Width = Screen.width, Height = Screen.height,
                Actors = View.Actors.Values.Select(a => new ActorReport { Name = a.Person.Title, DistanceWalked = a.DistanceWalked, FrameChanges = a.FrameChanges }).ToArray() };
            ResetSettlement(false);
            Paused = true;
            View.Tick(Society, 0, 0, 0, 0);
            Hud.Refresh();
            yield return null;
            report.Passed &= CaptureFrame(Path.Combine(folder, "single-georgie.png"));
            File.WriteAllText(Path.Combine(folder, "smoke.json"), JsonUtility.ToJson(report, true));
            Debug.Log("LITTLE_GEORGIES_SMOKE " + JsonUtility.ToJson(report));
            Application.Quit(report.Passed ? 0 : 1);
        }

        IEnumerator OpeningSmokeRun()
        {
            int index = Array.IndexOf(Args(), "-lg-capture");
            string folder = index >= 0 ? Args()[index + 1] : Path.Combine(Application.persistentDataPath, "OpeningSmoke");
            Directory.CreateDirectory(folder);
            Paused = true;
            View.Tick(Society, 0, 0, 0, 0);
            Hud.Refresh();
            yield return null;
            bool clean = Society.People.Count == 1 && Society.AllApples == 0 && Society.TotalHomes == 0 && Society.TotalBaskets == 0
                && !View.WorkshopVisible && View.VisibleHomes == 0;
            bool simple = FindObjectsByType<Dropdown>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length == 0
                && !FindObjectsByType<Text>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Any(t => t.text == "COMMON GOODS" || t.text == "Village sandbox");
            bool rendered = CaptureFrame(Path.Combine(folder, "opening.png"));
            Speed = 3;
            Paused = false;
            yield return new WaitForSecondsRealtime(3);
            rendered &= CaptureFrame(Path.Combine(folder, "picking.png"));
            while (Society.Report.Day == 1 && Progress < .97f) yield return null;
            Paused = true;
            View.Tick(Society, Progress, elapsed, 0, 0);
            bool sleepsOutside = View.Actors[1].Activity == "Sleeping outdoors";
            rendered &= CaptureFrame(Path.Combine(folder, "sleeping.png"));
            Paused = false;
            while (Society.Report.Day < 2 || Progress < .25f) yield return null;
            View.Tick(Society, Progress, elapsed, 0, 0);
            bool restsOutside = Society.People[0].Job == Job.Rest && View.Actors[1].Activity == "Resting outdoors";
            bool ate = Society.People[0].HappyHistory.Count > 0 && Society.CommonApples == 1;
            bool moved = View.Actors[1].DistanceWalked > 1 && View.Actors[1].FrameChanges > 2;
            rendered &= CaptureFrame(Path.Combine(folder, "resting.png"));
            Paused = true;
            dayClock = 0;
            fed = false;
            bool band = false;
            while (!Society.Specialist && Society.Day < 60)
            {
                AdvancePreviewDay();
                yield return null;
                if (!band && Society.People.Count == 2)
                {
                    band = Society.People[0].Name == "Henry" && !View.WorkshopVisible && View.VisibleHomes == 0;
                    rendered &= CaptureFrame(Path.Combine(folder, "gathering-band.png"));
                }
            }
            bool earnedSpecialties = Society.Specialist && Society.TotalHomes == 0 && Society.TotalBaskets == 0 && View.WorkshopVisible && !Society.HousingUnlocked;
            rendered &= CaptureFrame(Path.Combine(folder, "first-specialists.png"));
            while (!Society.HousingUnlocked && Society.Day < 100) { AdvancePreviewDay(); yield return null; }
            bool housingGate = Society.HousingUnlocked && Society.TotalHomes == 0 && View.VisibleHomes == 0;
            Society.Policy.BuildHomes = true;
            Society.Policy.RestWhenFed = false;
            while (Society.TotalHomes == 0 && Society.Day < 130) { AdvancePreviewDay(); yield return null; }
            bool earnedHouse = housingGate && Society.TotalHomes == 1 && View.VisibleHomes == 1;
            rendered &= CaptureFrame(Path.Combine(folder, "first-built-home.png"));
            var report = new OpeningReport { Passed = clean && simple && rendered && moved && sleepsOutside && restsOutside && ate && band && earnedSpecialties && earnedHouse,
                CleanOpening = clean, SimpleUI = simple, FramesCaptured = rendered, MovedAndAnimated = moved, SleptOutdoors = sleepsOutside,
                RestedOutdoors = restsOutside, AteAnApple = ate, NamedBand = band, SpecialtiesWithoutFreeGoods = earnedSpecialties, BuiltFirstHome = earnedHouse,
                Width = Screen.width, Height = Screen.height, FinalDay = Society.Day };
            File.WriteAllText(Path.Combine(folder, "smoke.json"), JsonUtility.ToJson(report, true));
            File.WriteAllText(Path.Combine(folder, "society.json"), JsonUtility.ToJson(Society, true));
            Debug.Log("LITTLE_GEORGIES_OPENING " + JsonUtility.ToJson(report));
            Application.Quit(report.Passed ? 0 : 1);
        }

        void AdvancePreviewDay()
        {
            Society.FinishDay();
            Society.BeginDay();
            View.Sync(Society);
            View.Tick(Society, 0, elapsed, 0, 0);
            Hud.Refresh();
        }

        static void PointerClick(GameObject target) => ExecuteEvents.Execute(target, new PointerEventData(EventSystem.current), ExecuteEvents.pointerClickHandler);

        bool CaptureFrame(string path)
        {
            // Render the same scene and canvas offscreen so hidden-window checks still capture pixels.
            var camera = View.Camera;
            var canvas = Auction.IsOpen ? Auction.RenderCanvas : Hud.RenderCanvas;
            int oldSortingOrder = canvas.sortingOrder;
            var target = RenderTexture.GetTemporary(Screen.width, Screen.height, 24, RenderTextureFormat.ARGB32);
            var oldActive = RenderTexture.active;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.sortingOrder = 3000;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1;
            camera.targetTexture = target;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = target;
            var pixels = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            pixels.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
            pixels.Apply();
            File.WriteAllBytes(path, pixels.EncodeToPNG());
            var colors = pixels.GetPixels32();
            bool varied = colors.Select(c => (c.r / 16) * 256 + (c.g / 16) * 16 + c.b / 16).Distinct().Count() > 100
                && colors.Count(c => c.r > 230 && c.b > 230 && c.g < 25) < colors.Length / 100;
            if (Auction.IsOpen)
            {
                foreach (var slider in FindObjectsByType<Slider>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Where(t => t.name.StartsWith("Auction ")))
                {
                    var rect = slider.handleRect;
                    var point = camera.WorldToScreenPoint(rect.TransformPoint(rect.rect.center));
                    var color = pixels.GetPixel(Mathf.RoundToInt(point.x), Mathf.RoundToInt(point.y));
                    bool visible = color.b > color.r * 1.3f && color.g > color.r * 1.3f;
                    if (!visible) Debug.LogError("Auction slider pixel missing: " + slider.name + " at " + point + " color " + color);
                    varied &= visible;
                }
            }
            Destroy(pixels);
            camera.targetTexture = null;
            RenderTexture.active = oldActive;
            RenderTexture.ReleaseTemporary(target);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = oldSortingOrder;
            Canvas.ForceUpdateCanvases();
            return varied;
        }

        [Serializable] public class ActorReport { public string Name; public float DistanceWalked; public int FrameChanges; }
        [Serializable] public class OpeningReport
        {
            public bool Passed, CleanOpening, SimpleUI, FramesCaptured, MovedAndAnimated, SleptOutdoors, RestedOutdoors, AteAnApple, NamedBand, SpecialtiesWithoutFreeGoods, BuiltFirstHome;
            public int Width, Height, FinalDay;
        }
        [Serializable] public class SmokeReport { public bool Passed, FramesCaptured, PauseControl, PolicyControls, AllActorsMovedAndAnimated, DayAdvanced; public int Day, Width, Height; public ActorReport[] Actors; }
    }
}
