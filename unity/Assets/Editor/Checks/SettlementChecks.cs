using System;
using System.Linq;
using LittleGeorgies;
using LittleGeorgies.Economy;

public static class SettlementChecks
{
    static int count;
    static void Check(bool passed, string message) { count++; if (!passed) throw new Exception("Settlement: " + message); }
    static AuctionResult Solve(SettlementEconomy s) => LandAuction.SolveWithValueTax(s.Scenario(), s.TaxPercent);
    static void Advance(SettlementEconomy s) => s.Advance(Solve(s), s.Revision);
    public static int Validate()
    {
        count = 0;
        var s = SettlementEconomy.Create();
        Check(s.Society.People.Count == 1 && s.Society.AllApples == 0 && s.Society.TotalBaskets == 0 && s.Society.TotalHomes == 0, "clean opening");
        Check(s.Plots.Count == 7 && s.Plots.All(p => p.Kind != "Residential"), "orchards and fields only");
        var first = Solve(s);
        Check(first.Allocation.Count == 1 && first.Allocation[0].Value == 2 && first.Revenue == 0, "one gatherer wins at most one plot");
        Advance(s);
        Check(s.Society.Day == 2 && s.Society.AllApples == 1 && s.Society.People[0].Mood == Mood.Tired, "first day harvest and dinner");
        Check(s.Society.People[0].Job == Job.Rest, "policy chooses affordable rest");
        Advance(s);
        Check(s.Society.People[0].Mood == Mood.Happy && s.Society.AllApples == 0, "rest plus dinner restores mood");
        for (int i = 0; i < 70 && !s.Society.Specialist; i++) Advance(s);
        Check(s.Society.Specialist && s.Society.People.Count == 5, "default policies earn specialties");
        Check(s.Society.People[0].Name == "Henry" && s.Society.People[0].Role == Role.Chief, "Henry becomes chief");
        Check(s.Society.TotalHomes == 0 && s.Society.TotalBaskets == 0, "no free equipment at transition");
        for (int i = 0; i < 20 && !s.Society.HousingUnlocked; i++) Advance(s);
        Check(s.Society.HousingUnlocked, "earned baskets unlock housing");
        s.Society.Policy.BuildHomes = true; s.Changed();
        for (int i = 0; i < 25 && s.Society.TotalHomes == 0; i++) Advance(s);
        Check(s.Society.TotalHomes > 0, "homes can be earned");

        var solo = SettlementEconomy.Create();
        solo.Society.People[0].Plan = WorkPlan.Rest; solo.Changed(); Advance(solo);
        Check(solo.Society.People[0].Mood == Mood.Broken && solo.Society.People[0].Job == Job.Harvest, "broken cannot rest");
        Check(solo.Scenario().Georgies[0].Alternatives.All(v => v.Value <= 1), "broken minimal yield");
        for (int i = 0; i < 25; i++) Advance(solo);
        Check(solo.Society.Day == 27, "no game over or day limit");
        Check(solo.Society.People.All(p => p.HappyHistory.Count <= 10), "rolling mood histories");

        for (int rate = 0; rate <= 100; rate++)
        {
            var taxed = SettlementEconomy.Create(true);
            taxed.TaxPercent = rate; taxed.Society.Policy.Food = FoodRule.PersonalHarvest;
            taxed.Society.Policy.RestWhenFed = false; taxed.Changed();
            int apples = taxed.Society.AllApples;
            var result = Solve(taxed);
            var report = AuctionTaxes.Calculate(result, new AuctionTaxPolicy { ValuePercent = rate });
            Check(report.TotalValue == report.TotalTaxTenThousandths / 10000 + report.TotalRentHundredths / 100 + report.TotalSurplusTenThousandths / 10000, "integer accounting at " + rate);
            Check(result.Allocation.SelectMany(a => a.Plots).Distinct().Count() == result.Allocation.Count, "unique allocation at " + rate);
            taxed.Advance(result, taxed.Revision);
            Check(taxed.Society.AllApples == apples + taxed.LastDay.Harvested - taxed.LastDay.Ate, "food conserved at " + rate);
            Check(taxed.LastDay.Levied == report.TotalTaxTenThousandths / 10000 && taxed.LastDay.RentCollected == report.TotalRentHundredths / 100, "tax and rent collected once at " + rate);
        }

        var edit = SettlementEconomy.Create();
        edit.SetValue(1, "F1", 8);
        var edited = Solve(edit);
        Check(edited.Allocation[0].Plots.Single() == "F1" && edited.Allocation[0].Value == 8, "editable per-person plot valuation");
        int stale = edit.Revision; edit.TaxPercent = 40; edit.Changed();
        bool rejected = false; try { edit.Advance(edited, stale); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected && edit.Society.Day == 1, "stale forecast cannot settle");
        edit.Society.People[0].Plan = WorkPlan.Work; edit.Changed(); Advance(edit);
        Check(edit.Society.People[0].Plan == WorkPlan.Work && edit.Society.People[0].Job == Job.Harvest, "personal plan persists");
        edit.Restore();
        Check(edit.Society.Day == 2 && edit.Scenario().Georgies.Count == edit.Society.People.Count, "dawn restoration");
        return count;
    }
}
