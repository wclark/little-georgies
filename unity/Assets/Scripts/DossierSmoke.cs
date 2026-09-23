using System;
using System.Collections;
using System.IO;
using System.Linq;
using LittleGeorgies.Economy;
using UnityEngine;
using UnityEngine.UI;

namespace LittleGeorgies
{
    public sealed partial class VillageGame
    {
        IEnumerator WaitForForecast()
        {
            float until = Time.unscaledTime + 8;
            while (!Desk.IsCurrent && Time.unscaledTime < until) yield return null;
            if (!Desk.IsCurrent) { Debug.LogError("Dossier forecast timed out"); Application.Quit(1); }
            yield return null;
        }

        Button DossierButton(string name) => FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Single(b => b.name == name);
        IEnumerator DossierSmokeRun()
        {
            int at = Array.IndexOf(Args(), "-lg-capture"); string folder = Args()[at + 1];
            Directory.CreateDirectory(folder);
            yield return WaitForForecast();
            var report = new DossierReport { Width = Screen.width, Height = Screen.height };
            report.NoAnimation = View == null && Hud == null && FindObjectsByType<GeorgieActor>(FindObjectsSortMode.None).Length == 0;
            report.CleanOpening = Desk.Session.Society.People.Count == 1 && Desk.Session.Society.AllApples == 0;
            report.FramesCaptured = CaptureFrame(Path.Combine(folder, "opening.png"));
            PointerClick(DossierButton("Plot O1").gameObject);
            report.FramesCaptured &= CaptureFrame(Path.Combine(folder, "plot.png"));
            PointerClick(DossierButton("Dossier plot bidder 1").gameObject);
            int old = Desk.Session.Value(Desk.Session.Society.People[0], Desk.Session.Plots[0]);
            PointerClick(DossierButton("Dossier increase O1").gameObject);
            yield return WaitForForecast();
            report.ValuationEdited = Desk.Session.Value(Desk.Session.Society.People[0], Desk.Session.Plots[0]) == old + 1;
            PointerClick(DossierButton("Dossier decrease O1").gameObject);
            yield return WaitForForecast();
            Desk.SelectPerson(1); Desk.SelectTab(2);
            var slider = FindObjectsByType<Slider>(FindObjectsSortMode.None).Single(s => s.name == "Dossier tax");
            var previous = Desk.Result;
            slider.value = 30;
            report.ContinuousResults = Desk.Result == previous && !Desk.IsCurrent && !DossierButton("Dossier next day").interactable;
            slider.value = 20; slider.value = 10;
            yield return WaitForForecast();
            report.LatestTax = Desk.Result.ValueTaxPercent == 10 && Desk.Report.TotalValue == Desk.Report.TotalTaxTenThousandths / 10000 + Desk.Report.TotalRentHundredths / 100 + Desk.Report.TotalSurplusTenThousandths / 10000;
            report.Layout = DossierBounds();
            report.FramesCaptured &= CaptureFrame(Path.Combine(folder, "policies.png"));
            slider.value = 0;
            yield return WaitForForecast();
            PointerClick(DossierButton("Dossier next day").gameObject);
            yield return WaitForForecast();
            report.DaySettled = Desk.Session.Society.Day == 2 && Desk.Session.Society.AllApples == 1 && Desk.Session.Society.People[0].Job == Job.Rest;
            PointerClick(DossierButton("Dossier autoplay").gameObject);
            yield return new WaitForSecondsRealtime(4.2f);
            yield return WaitForForecast();
            PointerClick(DossierButton("Dossier autoplay").gameObject);
            report.AutoDays = !Desk.Running && Desk.Session.Society.Day == 3;
            Desk.SelectTab(1); Desk.SelectPerson(1);
            PointerClick(DossierButton("Dossier plan Work").gameObject);
            yield return WaitForForecast();
            report.PersonalPlan = Desk.Session.Society.People[0].Plan == WorkPlan.Work && Desk.Session.Society.People[0].Job == Job.Harvest;
            PointerClick(DossierButton("Dossier plan Policy").gameObject);
            yield return WaitForForecast();
            while (!Desk.Session.Society.Specialist && Desk.Session.Society.Day < 70)
            {
                Desk.AdvanceDay(); yield return WaitForForecast();
                if (Desk.Session.Society.People.Count == 2) report.FramesCaptured &= CaptureFrame(Path.Combine(folder, "band.png"));
            }
            report.Progression = Desk.Session.Society.Specialist && Desk.Session.Society.People[0].Role == Role.Chief;
            Desk.SelectTab(1); Desk.SelectPerson(2);
            yield return null;
            report.FramesCaptured &= CaptureFrame(Path.Combine(folder, "georgies.png"));
            var people = FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Where(b => b.name.StartsWith("Dossier person ")).ToArray();
            report.CompactPeople = people.Length == 5 && people.All(b => b.GetComponent<RectTransform>().rect.height <= 90);
            report.Layout &= DossierBounds();
            Desk.SelectTab(0); Desk.SelectPlot("O1");
            report.FramesCaptured &= CaptureFrame(Path.Combine(folder, "village-land.png"));
            report.Layout &= DossierBounds();
            Desk.Save();
            var restored = JsonUtility.FromJson<SettlementEconomy>(File.ReadAllText(Desk.SavePath)); restored.Restore();
            report.SaveRestored = restored.Society.Day == Desk.Session.Society.Day && restored.TaxPercent == Desk.Session.TaxPercent
                && restored.Society.AllApples == Desk.Session.Society.AllApples;
            var restoredResult = LandAuction.SolveWithValueTax(restored.Scenario(), restored.TaxPercent);
            restored.Advance(restoredResult, restored.Revision);
            report.SaveRestored &= restored.Society.Day == Desk.Session.Society.Day + 1;
            var before = JsonUtility.ToJson(Desk.Session);
            Auction.Open(); yield return null; Auction.Close(); yield return null;
            report.AdminIsolated = before == JsonUtility.ToJson(Desk.Session) && Desk.RenderCanvas.gameObject.activeSelf;
            report.Passed = report.NoAnimation && report.CleanOpening && report.FramesCaptured && report.ValuationEdited && report.ContinuousResults && report.LatestTax
                && report.DaySettled && report.AutoDays && report.PersonalPlan && report.Progression && report.CompactPeople && report.Layout && report.SaveRestored && report.AdminIsolated;
            File.WriteAllText(Path.Combine(folder, "smoke.json"), JsonUtility.ToJson(report, true));
            Debug.Log("LITTLE_GEORGIES_DOSSIER " + JsonUtility.ToJson(report));
            Application.Quit(report.Passed ? 0 : 1);
        }

        bool DossierBounds()
        {
            Canvas.ForceUpdateCanvases(); bool good = true;
            foreach (var text in Desk.RenderCanvas.GetComponentsInChildren<Text>())
            {
                var corners = new Vector3[4]; text.rectTransform.GetWorldCorners(corners);
                bool bounded = corners.All(c => c.x >= -1 && c.x <= Screen.width + 1 && c.y >= -1 && c.y <= Screen.height + 1);
                bool fits = text.preferredHeight <= text.rectTransform.rect.height + 2;
                if (!bounded || !fits) { Debug.LogError("Dossier text bounds: " + text.text + " size " + text.rectTransform.rect.size + " needs " + text.preferredHeight); good = false; }
            }
            foreach (var slider in Desk.RenderCanvas.GetComponentsInChildren<Slider>())
            {
                var parent = slider.GetComponent<RectTransform>();
                var corners = new Vector3[4]; slider.handleRect.GetWorldCorners(corners);
                bool contained = corners.Select(c => parent.InverseTransformPoint(c)).All(p => p.x >= parent.rect.xMin - .5f
                    && p.x <= parent.rect.xMax + .5f && p.y >= parent.rect.yMin - .5f && p.y <= parent.rect.yMax + .5f);
                if (!contained) { Debug.LogError("Dossier slider handle exceeds its control: " + slider.name); good = false; }
            }
            return good;
        }

        [Serializable] public sealed class DossierReport
        {
            public bool Passed, NoAnimation, CleanOpening, FramesCaptured, ValuationEdited, ContinuousResults, LatestTax, DaySettled, AutoDays, PersonalPlan, Progression, CompactPeople, Layout, SaveRestored, AdminIsolated;
            public int Width, Height;
        }
    }
}
