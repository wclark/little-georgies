using System;
using System.Collections;
using System.IO;
using System.Linq;
using LittleGeorgies.Economy;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LittleGeorgies
{
    public sealed partial class VillageGame
    {
        T AuctionControl<T>(string name) where T : Component =>
            FindObjectsByType<T>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Single(c => c.name == name);
        void AuctionClick(string name) => PointerClick(AuctionControl<Button>(name).gameObject);

        void AuctionSetRate(string name, int percent)
        {
            var slider = AuctionControl<Slider>(name);
            Canvas.ForceUpdateCanvases();
            var area = slider.handleRect.parent as RectTransform;
            var local = new Vector2(area.rect.xMin + area.rect.width * percent / 100f, area.rect.center.y);
            var screen = RectTransformUtility.WorldToScreenPoint(null, area.TransformPoint(local));
            var pointer = new PointerEventData(EventSystem.current) { position = screen, button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute(slider.gameObject, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(slider.gameObject, pointer, ExecuteEvents.pointerUpHandler);
        }

        bool continuousResults;
        int refreshFrames;

        IEnumerator AwaitAuction()
        {
            while (Auction.HasPendingAuction || Auction.IsBusy)
            {
                CheckVisibleAuction();
                yield return null;
            }
            CheckVisibleAuction();
        }

        void CheckVisibleAuction()
        {
            refreshFrames++;
            var roots = FindObjectsByType<RectTransform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(r => r.name == "Displayed auction").ToArray();
            if (Auction.Result == null || roots.Length != 1) { continuousResults = false; return; }
            var labels = roots[0].GetComponentsInChildren<Text>().ToList();
            foreach (var row in Auction.TaxReport.Georgies)
            {
                continuousResults &= row.TaxTenThousandths % 10000 == 0 && row.BidHundredths % 100 == 0
                    && row.RentHundredths % 100 == 0 && row.SurplusTenThousandths % 10000 == 0;
                string valueTax = (row.TaxTenThousandths / 10000m).ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);
                string surplus = (row.SurplusTenThousandths / 10000m).ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);
                continuousResults &= labels.Count(t => t.name == "Auction tax result " + row.GeorgieId && t.text == valueTax) == 1
                    && labels.Count(t => t.name == "Auction surplus result " + row.GeorgieId && t.text == surplus) == 1;
            }
        }

        IEnumerator AuctionSmokeRun()
        {
            int index = Array.IndexOf(Args(), "-lg-capture");
            string folder = index >= 0 ? Args()[index + 1] : Path.Combine(Application.persistentDataPath, "AuctionSmoke");
            Directory.CreateDirectory(folder);
            yield return null;
            AuctionClick("Economy admin");
            string village = JsonUtility.ToJson(Society);
            float progress = Progress;
            while (Auction.IsBusy || Auction.HasPendingAuction) yield return null;
            yield return new WaitForSecondsRealtime(.25f);
            continuousResults = true; refreshFrames = 0;
            var report = new AuctionSmokeReport { Width = Screen.width, Height = Screen.height };
            report.NativeSolver = Auction.Result != null && Auction.Result.Welfare == 46 && Auction.Result.Revenue == 19 && Auction.Result.AmountScale == 1;
            report.VillageIsolated = village == JsonUtility.ToJson(Society) && Progress == progress && Auction.IsOpen;
            report.IndividualValues = FindObjectsByType<InputField>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Count(f => f.name.StartsWith("Auction value ")) == 4;
            report.FramesCaptured = CaptureFrame(Path.Combine(folder, "auction-overview.png"));

            var result = Auction.Result;
            var oldTaxCell = AuctionControl<Text>("Auction tax result g1");
            AuctionSetRate("Auction tax rate", 10);
            report.ResultsRetained = ReferenceEquals(result, Auction.Result) && Auction.HasPendingAuction && !Auction.IsResultCurrent
                && oldTaxCell == AuctionControl<Text>("Auction tax result g1") && oldTaxCell.text == "0";
            report.FramesCaptured &= CaptureFrame(Path.Combine(folder, "auction-refresh-pending.png"));
            yield return AwaitAuction();
            report.TaxControls = Auction.IsResultCurrent && Auction.Taxes.ValuePercent == 10
                && !ReferenceEquals(result, Auction.Result) && Auction.Result.Welfare == 42 && Auction.Result.Revenue == 16
                && Auction.TaxReport.TotalSurplusTenThousandths == 260000;
            report.BidDisplay = AuctionControl<Text>("Auction bid g1 R1").text == "11"
                && AuctionControl<Text>("Auction tax g1 R1").text == "1";
            report.ColumnOrder = AuctionControl<Text>("Auction tax result g1").text == "1"
                && AuctionControl<Text>("Auction surplus result g1").text == "3";
            var editor = AuctionControl<InputField>("Auction value g1 R1").GetComponentInParent<ScrollRect>().transform.parent;
            var resultRows = AuctionControl<Button>("Auction result g1").transform.parent;
            report.ColumnOrder &= editor.GetComponentsInChildren<Text>().Where(t => t.transform.parent == editor && t.rectTransform.anchoredPosition.y == -113)
                .OrderBy(t => t.rectTransform.anchoredPosition.x).Select(t => t.text).SequenceEqual(new[] { "PLOT", "VALUE", "TAX", "BID" })
                && resultRows.GetComponentsInChildren<Text>().Where(t => t.transform.parent == resultRows && t.rectTransform.anchoredPosition.y == 0)
                .OrderBy(t => t.rectTransform.anchoredPosition.x).Select(t => t.text).SequenceEqual(new[] { "GEORGIE", "PLOT", "VALUE", "TAX", "BID", "RENT", "SURPLUS" });
            report.SingleTaxRate = FindObjectsByType<Slider>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Count(s => s.name.StartsWith("Auction ")) == 1
                && !Auction.RenderCanvas.GetComponentsInChildren<Text>().Any(t => t.text.IndexOf("LVT", StringComparison.OrdinalIgnoreCase) >= 0
                    || t.text.IndexOf("rent tax", StringComparison.OrdinalIgnoreCase) >= 0 || t.text.IndexOf("value tax", StringComparison.OrdinalIgnoreCase) >= 0);
            var summary = Auction.RenderCanvas.GetComponentsInChildren<Text>()
                .Where(t => t.transform.parent.name == "Displayed auction" && (t.text.StartsWith("TOTAL ") || t.text == "SURPLUS"))
                .OrderByDescending(t => t.rectTransform.anchoredPosition.y).ThenBy(t => t.rectTransform.anchoredPosition.x);
            report.SummaryTotals = summary.Select(t => t.text).SequenceEqual(new[] { "TOTAL VALUE", "TOTAL TAX", "TOTAL RENT", "SURPLUS" });
            report.WholeAmounts = Auction.TaxReport.Georgies.All(g => g.BidHundredths % 100 == 0 && g.RentHundredths % 100 == 0
                && g.TaxTenThousandths % 10000 == 0 && g.SurplusTenThousandths % 10000 == 0);
            report.SliderBounds = FindObjectsByType<Slider>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(s => s.name.StartsWith("Auction ")).All(s => s.handleRect.rect.height <= (s.transform as RectTransform).rect.height + .01f);
            var snapshot = JsonUtility.FromJson<AuctionDocument>(File.ReadAllText(Path.Combine(Auction.Folder, "last-run.json")));
            report.TaxSnapshot = snapshot.FormatVersion == 6 && snapshot.Solved && snapshot.Taxes.ValuePercent == 10
                && snapshot.Result.ValueTaxPercent == 10 && snapshot.Result.Revenue == 16 && snapshot.Result.AmountScale == 1
                && snapshot.TaxReport.TotalTaxTenThousandths == 40000
                && snapshot.TaxReport.TotalSurplusTenThousandths == 260000;
            report.TaxSnapshot &= !File.ReadAllText(Path.Combine(Auction.Folder, "last-run.json")).Contains("RentPercent")
                && !File.ReadAllText(Path.Combine(Auction.Folder, "last-run.json")).Contains("RentTax");
            report.FramesCaptured &= CaptureFrame(Path.Combine(folder, "auction-taxes.png"));
            AuctionSetRate("Auction tax rate", 100);
            yield return AwaitAuction();
            report.FullTax = Auction.Result.Allocation.Count == 0 && Auction.Result.Revenue == 0
                && Auction.TaxReport.TotalValue == 0 && Auction.TaxReport.TotalSurplusTenThousandths == 0;
            report.FramesCaptured &= CaptureFrame(Path.Combine(folder, "auction-zero-bids.png"));
            AuctionSetRate("Auction tax rate", 10);
            yield return AwaitAuction();

            var value = AuctionControl<InputField>("Auction value g1 R1");
            var scroll = AuctionControl<Button>("Auction result g1").GetComponentInParent<ScrollRect>();
            scroll.verticalNormalizedPosition = .37f;
            yield return null;
            result = Auction.Result;
            value.text = "30";
            AuctionClick("Auction run");
            report.EditableDuringSolve = Auction.IsBusy && value.IsInteractable() && AuctionControl<Slider>("Auction tax rate").IsInteractable();
            // The first solve cannot publish before the next frame. Supersede its captured inputs now.
            value.text = "31";
            report.ResultsRetained &= ReferenceEquals(result, Auction.Result) && !Auction.IsResultCurrent;
            string savePath = Path.Combine(Auction.Folder, "scenario.json");
            string lastRunBefore = File.ReadAllText(Path.Combine(Auction.Folder, "last-run.json"));
            AuctionClick("Auction save");
            var pendingSave = JsonUtility.FromJson<AuctionDocument>(File.ReadAllText(savePath));
            report.PendingSave = !pendingSave.Solved && pendingSave.Scenario.Georgies[0].Alternatives.Single(b => b.Plots[0] == "R1").Value == 31
                && File.ReadAllText(Path.Combine(Auction.Folder, "last-run.json")) == lastRunBefore;
            report.LatestEditWins = true;
            while (Auction.HasPendingAuction || Auction.IsBusy)
            {
                CheckVisibleAuction();
                report.LatestEditWins &= Auction.TaxReport.TotalValue == 46 || Auction.TaxReport.TotalValue == 64;
                yield return null;
            }
            CheckVisibleAuction();
            report.LatestEditWins &= Auction.IsResultCurrent && Auction.TaxReport.TotalValue == 64 && Auction.Result.Welfare == 58;
            report.ScrollPreserved = Mathf.Abs(AuctionControl<Button>("Auction result g1").GetComponentInParent<ScrollRect>().verticalNormalizedPosition - .37f) < .01f;

            value.text = "30";
            yield return AwaitAuction();
            report.ValueEditing = Auction.Result.Welfare == 57 && Auction.TaxReport.TotalValue == 63
                && AuctionControl<Text>("Auction bid g1 R1").text == "27" && AuctionControl<Text>("Auction tax g1 R1").text == "3";
            result = Auction.Result;
            value.text = "";
            yield return AwaitAuction();
            report.InvalidInputRejected = !Auction.IsBusy && ReferenceEquals(result, Auction.Result) && !Auction.IsResultCurrent
                && Auction.LastError != null && AuctionControl<Text>("Auction bid g1 R1").text == "-"
                && AuctionControl<Text>("Auction tax g1 R1").text == "-";
            value.text = "30";
            yield return AwaitAuction();
            AuctionClick("Auction save");
            value.text = "2";
            AuctionSetRate("Auction tax rate", 45);
            AuctionClick("Auction load");
            yield return null;
            AuctionClick("Auction confirm");
            yield return AwaitAuction();
            report.SaveLoad = Auction.Scenario.Georgies.First(g => g.Id == "g1").Alternatives.Single(b => b.Plots[0] == "R1").Value == 30
                && Auction.IsResultCurrent && Auction.Result.Welfare == 57 && Auction.Taxes.ValuePercent == 10;

            report.LegacySave = true;
            foreach (int version in new[] { 2, 3, 4, 5 })
            {
                var legacy = JsonUtility.FromJson<LegacyAuctionDocument>(File.ReadAllText(savePath));
                legacy.Taxes.RentPercent = 99;
                legacy.FormatVersion = version; legacy.Result.Welfare = 999999; legacy.Result.AmountScale = 100;
                string legacyText = JsonUtility.ToJson(legacy, true);
                File.WriteAllText(savePath, legacyText);
                AuctionClick("Auction load");
                yield return null;
                AuctionClick("Auction confirm");
                yield return AwaitAuction();
                report.LegacySave &= Auction.IsResultCurrent && Auction.Result.Welfare == 57 && Auction.Result.ValueTaxPercent == 10
                    && Auction.Result.AmountScale == 1 && Auction.TaxReport.TotalSurplusTenThousandths == 400000
                    && File.ReadAllText(savePath) == legacyText;
            }

            AuctionClick("Auction Georgie g4");
            yield return null;
            AuctionControl<InputField>("Auction value g4 O1").text = "30";
            yield return AwaitAuction();
            report.IndividualValues &= Auction.Result.Welfare == 69 && Auction.TaxReport.TotalValue == 77
                && Auction.Result.Allocation.Single(a => a.GeorgieId == "g4").Plots.SequenceEqual(new[] { "O1" })
                && Auction.Result.Allocation.All(a => a.Plots.Count == 1);
            AuctionClick("Auction result g4");
            AuctionControl<Button>("Auction result g4").GetComponentInParent<ScrollRect>().verticalNormalizedPosition = 1;
            yield return new WaitForSecondsRealtime(.25f);
            report.FramesCaptured &= CaptureFrame(Path.Combine(folder, "auction-individual-edited.png"));
            AuctionControl<Button>("Auction result g4").GetComponentInParent<ScrollRect>().verticalNormalizedPosition = 0;
            yield return new WaitForSecondsRealtime(.1f);
            report.FramesCaptured &= CaptureFrame(Path.Combine(folder, "auction-counterfactual.png"));
            AuctionClick("Auction plots tab");
            yield return null;
            report.FramesCaptured &= CaptureFrame(Path.Combine(folder, "auction-land-inventory.png"));
            int plots = Auction.Scenario.Plots.Count;
            AuctionClick("Auction remove plot R1");
            report.PlotDeletionConfirmed = Auction.Scenario.Plots.Count == plots;
            yield return null;
            AuctionClick("Auction cancel");
            AuctionClick("Auction add plot");
            yield return null;
            report.AddRemovePlot = Auction.Scenario.Plots.Count == plots + 1
                && Auction.Scenario.Georgies.All(g => g.Alternatives.Single(b => b.Plots[0] == "P1").Value == 0);
            var kind = AuctionControl<Dropdown>("Auction plot kind P1");
            kind.GetComponentInParent<ScrollRect>().verticalNormalizedPosition = 0;
            yield return null;
            PointerClick(kind.gameObject);
            yield return new WaitForSecondsRealtime(.1f);
            report.FramesCaptured &= CaptureFrame(Path.Combine(folder, "auction-plot-menu.png"));
            var residential = FindObjectsByType<Toggle>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Single(t => t.GetComponentsInChildren<Text>().Any(text => text.text == "Residential"));
            PointerClick(residential.gameObject);
            report.PlotKindMenu = Auction.Scenario.Plots.Single(p => p.Id == "P1").Kind == "Residential";
            yield return new WaitForSecondsRealtime(.2f);
            AuctionClick("Auction remove plot P1");
            yield return null;
            report.PlotDeletionConfirmed &= Auction.Scenario.Plots.Count == plots + 1;
            AuctionClick("Auction confirm");
            yield return null;
            report.AddRemovePlot &= Auction.Scenario.Plots.Count == plots
                && Auction.Scenario.Georgies.All(g => g.Alternatives.All(b => b.Plots[0] != "P1"));
            AuctionClick("Auction add Georgie");
            yield return null;
            report.AddRemoveGeorgie = Auction.Scenario.Georgies.Count == 6
                && Auction.Scenario.Georgies.Last().Alternatives.Count == plots
                && Auction.Scenario.Georgies.Last().Alternatives.All(b => b.Value == 0);
            AuctionClick("Auction remove Georgie");
            yield return null;
            AuctionClick("Auction confirm");
            yield return AwaitAuction();
            report.AddRemoveGeorgie &= Auction.Scenario.Georgies.Count == 5;
            report.VillageIsolated &= village == JsonUtility.ToJson(Society) && Progress == progress;
            AuctionClick("Auction close");
            yield return new WaitForSecondsRealtime(.2f);
            report.ReturnToVillage = !Auction.IsOpen && Progress > progress && Society.People.Count == 1;
            report.ContinuousResults = continuousResults && refreshFrames > 5;
            report.RefreshFrames = refreshFrames;
            report.Passed = report.NativeSolver && report.VillageIsolated && report.ResultsRetained && report.ValueEditing
                && report.InvalidInputRejected && report.SaveLoad && report.IndividualValues && report.TaxControls && report.TaxSnapshot
                && report.BidDisplay && report.ColumnOrder && report.SummaryTotals && report.WholeAmounts && report.SingleTaxRate && report.FullTax && report.LegacySave
                && report.ContinuousResults && report.EditableDuringSolve && report.LatestEditWins && report.PendingSave && report.ScrollPreserved
                && report.SliderBounds && report.PlotDeletionConfirmed && report.AddRemovePlot && report.PlotKindMenu && report.AddRemoveGeorgie
                && report.ReturnToVillage && report.FramesCaptured;
            File.WriteAllText(Path.Combine(folder, "smoke.json"), JsonUtility.ToJson(report, true));
            Debug.Log("LITTLE_GEORGIES_AUCTION_SMOKE " + JsonUtility.ToJson(report));
            Application.Quit(report.Passed ? 0 : 1);
        }

        [Serializable] public sealed class AuctionSmokeReport
        {
            public bool Passed, NativeSolver, VillageIsolated, ResultsRetained, ValueEditing, InvalidInputRejected,
                BidDisplay, ColumnOrder, SummaryTotals, WholeAmounts, SingleTaxRate, FullTax, LegacySave, ContinuousResults,
                EditableDuringSolve, LatestEditWins, PendingSave, ScrollPreserved,
                SaveLoad, IndividualValues, TaxControls, TaxSnapshot, SliderBounds, PlotDeletionConfirmed, AddRemovePlot, PlotKindMenu,
                AddRemoveGeorgie, ReturnToVillage, FramesCaptured;
            public int Width, Height, RefreshFrames;
        }

        // Old saves may contain LVT settings. Import only their original values and value-based rate.
        [Serializable] public sealed class LegacyAuctionDocument
        {
            public int FormatVersion;
            public bool Solved;
            public AuctionScenario Scenario;
            public AuctionResult Result;
            public LegacyAuctionTaxes Taxes;
        }
        [Serializable] public sealed class LegacyAuctionTaxes
        {
            public int ValuePercent;
            public int RentPercent;
        }
    }
}
