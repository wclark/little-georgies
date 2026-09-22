using System;
using System.Collections.Generic;
using System.Linq;
using LittleGeorgies.Economy;
using UnityEditor;
using UnityEngine;

public static class AuctionChecks
{
    static int checks;
    static void Check(bool value, string message) { checks++; if (!value) throw new Exception("Auction: " + message); }

    [MenuItem("Little Georgies/Validate land auctions")]
    public static void Validate()
    {
        checks = 0;
#if UNITY_EDITOR_WIN
        AuctionNative.Initialize();
        var s = AuctionScenario.Example();
        var r = LandAuction.Solve(s);
        Check(r.Welfare == 43 && r.Revenue == 29, "Known example welfare and revenue");
        Check(r.Georgies.Single(g => g.GeorgieId == "g1").Payment == 7, "Mara pays externality, not her bid");
        Check(r.Georgies.Single(g => g.GeorgieId == "g2").Payment == 9, "Ada pays externality");
        Check(r.Georgies.Single(g => g.GeorgieId == "g4").Payment == 13, "Complementary orchard bundle payment");
        Check(r.Allocation.Single(g => g.GeorgieId == "g1").Plots.SequenceEqual(new[] { "R2" }), "Residential alternatives are substitutes");
        var document = JsonUtility.FromJson<AuctionDocument>(JsonUtility.ToJson(new AuctionDocument { Solved = true, Scenario = s, Result = r }));
        Check(document.Solved && document.Result.Georgies.Single(g => g.GeorgieId == "g1").Won && !document.Result.Georgies.Single(g => g.GeorgieId == "g3").Won, "JSON explicitly distinguishes winners and losers");
        Check(!JsonUtility.FromJson<AuctionDocument>(JsonUtility.ToJson(new AuctionDocument { Scenario = s })).Solved, "Unsolved JSON is not a zero-value result");
        Verify(s, r);
        string canonical = JsonUtility.ToJson(r.AllocationWrapper());
        s.Georgies.Reverse(); s.Plots.Reverse();
        foreach (var g in s.Georgies) g.Alternatives.Reverse();
        Check(JsonUtility.ToJson(LandAuction.Solve(s).AllocationWrapper()) == canonical, "Tie-breaking is independent of input order");

        var tie = Small(1);
        tie.Georgies.Add(AuctionScenario.Bidder("a", "Ada", AuctionScenario.Bid("a", 5, "P0")));
        tie.Georgies.Add(AuctionScenario.Bidder("b", "Bill", AuctionScenario.Bid("a", 5, "P0")));
        var tr = LandAuction.Solve(tie);
        Check(tr.Allocation.Single().GeorgieId == "a" && tr.Revenue == 5, "Stable exact ties and second price");
        tie.Georgies[1].Alternatives[0].Value = 0;
        Check(LandAuction.Solve(tie).Revenue == 0, "No reserve price or invented payment");
        tie.Georgies[0].Alternatives[0].Value = 0;
        Check(LandAuction.Solve(tie).Allocation.Count == 0, "Zero-valued bundles remain unallocated");
        Check(LandAuction.Solve(Small(0)).Welfare == 0, "Empty auction");
        Reject(tie, x => x.Georgies[0].Alternatives[0].Value = -1);
        Reject(tie, x => x.Georgies[0].Alternatives[0].Value = 1000001);
        Reject(tie, x => x.Georgies[0].Alternatives[0].Plots.Clear());
        Reject(tie, x => x.Georgies[0].Alternatives[0].Plots.Add("P0"));
        Reject(tie, x => x.Georgies[0].Alternatives[0].Plots[0] = "missing");
        Reject(tie, x => x.Georgies[1].Id = "a");
        Reject(tie, x => x.Plots[0].Name = "");
        Reject(tie, x => x.Version = 99);
        bool timeout = false;
        try { LandAuction.Solve(AuctionScenario.Example(), 0); } catch (TimeoutException) { timeout = true; }
        Check(timeout, "No partial result on time limit");

        var random = new System.Random(7319);
        for (int test = 0; test < 80; test++)
        {
            var sample = Small(random.Next(1, 5));
            for (int i = 0; i < random.Next(1, 6); i++)
            {
                var person = AuctionScenario.Bidder("g" + i, "Georgie " + i);
                for (int j = 0; j < random.Next(1, 5); j++)
                {
                    var plots = sample.Plots.Where(p => random.Next(2) == 0).Select(p => p.Id).ToArray();
                    if (plots.Length == 0) plots = new[] { sample.Plots[0].Id };
                    person.Alternatives.Add(AuctionScenario.Bid("b" + j, random.Next(0, 25), plots));
                }
                sample.Georgies.Add(person);
            }
            Verify(sample, LandAuction.Solve(sample));
        }
#endif
        IndividualValuesAndTaxes();
        checks += PortableAuctionChecks.Validate();
#if UNITY_EDITOR_WIN
        var portable = IndividualLandValues.Example();
        for (int rate = 0; rate <= 100; rate++)
        {
            var actual = LandAuction.SolveWithValueTax(portable, rate);
            var reference = LandAuction.SolveReference(portable, rate);
            Check(actual.Welfare == reference.Welfare && actual.Revenue == reference.Revenue
                && JsonUtility.ToJson(actual.AllocationWrapper()) == JsonUtility.ToJson(reference.AllocationWrapper())
                && actual.Georgies.Select(g => g.Payment).SequenceEqual(reference.Georgies.Select(g => g.Payment)),
                "Managed solver matches native reference allocation and rents at every tax rate");
        }
#endif
        Debug.Log("LITTLE_GEORGIES_AUCTION: " + checks + " checks passed (120 exhaustive-oracle scenarios, individual values, migration, and taxes)");
    }

    static void IndividualValuesAndTaxes()
    {
        var s = IndividualLandValues.Example();
        IndividualLandValues.Validate(s);
        var baseline = LandAuction.Solve(s);
        Verify(s, baseline);
        var r = LandAuction.SolveWithValueTax(s, 0);
        VerifyTaxedBids(s, r, 0);
        Check(r.Welfare == 46 && r.Revenue == 19 && r.AmountScale == 1, "Zero-tax bids and rents equal untaxed whole-apple amounts");
        Check(r.Allocation.All(a => a.Plots.Count == 1 && a.Bid == a.Value), "Every winner receives one plot and bid equals value at zero tax");
        Check(r.Allocation.Single(a => a.GeorgieId == "g4").Plots.Single() == "O2", "Single-plot substitutes maximize total bid");
        var policy = new AuctionTaxPolicy();
        var zero = AuctionTaxes.Calculate(r, policy);
        Check(zero.TotalSurplusTenThousandths == 270000 && zero.TotalTaxTenThousandths == 0, "Surplus is value minus tax and rent");
        Check(zero.TotalRentHundredths == baseline.Revenue * 100, "Zero-tax rents match the original VCG auction");
        string before = JsonUtility.ToJson(s);
        policy.ValuePercent = 10;
        var taxed = LandAuction.SolveWithValueTax(s, policy.ValuePercent);
        VerifyTaxedBids(s, taxed, 10);
        var tax = AuctionTaxes.Calculate(taxed, policy);
        Check(tax.TotalValue == 46 && tax.TotalBidHundredths == 4200 && tax.TotalRentHundredths == 1600, "Rounded tax reduces bids and VCG rents, without changing gross values");
        Check(tax.TotalTaxTenThousandths == 40000 && tax.TotalSurplusTenThousandths == 260000, "Tax is deducted once from value, rent once from bid");
        Check(tax.TotalTaxTenThousandths != AuctionTaxes.TaxOnValue(tax.TotalValue, 10) * 10000,
            "Tax totals sum individually rounded amounts, not the rounded aggregate");
        Check(tax.Georgies.Single(g => g.GeorgieId == "g3").TaxTenThousandths == 0, "Unallocated Georgies owe no tax");
        Check(JsonUtility.ToJson(s) == before, "Bid adjustment does not overwrite entered values");
        foreach (var row in tax.Georgies)
            Check(10000 * row.Value == 100 * row.RentHundredths + row.TaxTenThousandths + row.SurplusTenThousandths, "Individual value equals tax plus rent plus surplus");
        Check(tax.TotalSurplusTenThousandths == tax.Georgies.Sum(g => g.SurplusTenThousandths), "Aggregate surplus equals sum of individual net surplus");
        Check(tax.TotalTaxTenThousandths == tax.Georgies.Sum(g => g.TaxTenThousandths), "Tax totals aggregate individual tax");
        for (int rate = 0; rate <= 100; rate++)
        {
            var result = LandAuction.SolveWithValueTax(s, rate);
            var report = AuctionTaxes.Calculate(result, new AuctionTaxPolicy { ValuePercent = rate });
            VerifyTaxedBids(s, result, rate);
            Check(report.TotalValue == result.Allocation.Sum(a => a.Value)
                && report.TotalTaxTenThousandths == report.Georgies.Sum(g => g.TaxTenThousandths)
                && report.TotalBidHundredths == result.Welfare * 100 && report.TotalRentHundredths == result.Revenue * 100,
                "Every tax rate reports the exact integer-bid allocation and individual tax sum");
            foreach (var row in report.Georgies)
            {
                Check(row.SurplusTenThousandths == 100 * (row.BidHundredths - row.RentHundredths)
                    && row.Value * 10000 - row.TaxTenThousandths == row.BidHundredths * 100,
                    "Each bidder has value minus tax equals bid, bid minus rent equals surplus");
                Check(row.TaxTenThousandths % 10000 == 0 && row.BidHundredths % 100 == 0
                    && row.RentHundredths % 100 == 0 && row.SurplusTenThousandths % 10000 == 0,
                    "Every amount is a whole apple, not only its displayed text");
            }
        }
        policy.ValuePercent = 100;
        var allTaxed = LandAuction.SolveWithValueTax(s, 100);
        VerifyTaxedBids(s, allTaxed, 100);
        var allTaxReport = AuctionTaxes.Calculate(allTaxed, policy);
        Check(allTaxed.Allocation.Count == 0 && allTaxed.Welfare == 0 && allTaxed.Revenue == 0
            && allTaxReport.TotalValue == 0 && allTaxReport.TotalTaxTenThousandths == 0 && allTaxReport.TotalSurplusTenThousandths == 0,
            "100% tax means zero bids, no allocation, no rent, and no tax on unawarded values");

        var small = Small(1);
        small.Georgies.Add(AuctionScenario.Bidder("a", "Ada", AuctionScenario.Bid("a", 1, "P0")));
        small.Georgies.Add(AuctionScenario.Bidder("b", "Bill", AuctionScenario.Bid("a", 1, "P0")));
        foreach (int percent in new[] { 0, 49, 50, 51, 99, 100 })
        {
            var tiny = LandAuction.SolveWithValueTax(small, percent);
            var report = AuctionTaxes.Calculate(tiny, new AuctionTaxPolicy { ValuePercent = percent });
            long expectedBid = percent < 50 ? 1 : 0;
            Check(AuctionTaxes.TaxOnValue(1, percent) == 1 - expectedBid
                && tiny.Welfare == expectedBid && tiny.Revenue == expectedBid
                && tiny.Allocation.Count == expectedBid && report.TotalSurplusTenThousandths == 0,
                "Tax rounds at half an apple, then bids and VCG rents follow the integer amount");
        }
        var roundedTie = Small(1);
        roundedTie.Georgies.Add(AuctionScenario.Bidder("a", "Ada", AuctionScenario.Bid("a", 1, "P0")));
        roundedTie.Georgies.Add(AuctionScenario.Bidder("b", "Bill", AuctionScenario.Bid("a", 2, "P0")));
        var tiedBids = LandAuction.SolveWithValueTax(roundedTie, 30);
        Check(tiedBids.Allocation.Single().GeorgieId == "a" && tiedBids.Welfare == 1 && tiedBids.Revenue == 1,
            "Rounding affects the real auction and canonical ties, not just labels");
        VerifyTaxedBids(roundedTie, tiedBids, 30);
        Check(AuctionTaxes.Calculate(LandAuction.SolveWithValueTax(Small(0), 25), new AuctionTaxPolicy { ValuePercent = 25 }).TotalSurplusTenThousandths == 0, "Empty auction has zero taxes");
        foreach (var invalidRate in new[] { -1, 101 })
        {
            RejectAction(() => LandAuction.SolveWithValueTax(s, invalidRate), "Invalid tax bid rate rejected");
            RejectAction(() => AuctionTaxes.Calculate(r, new AuctionTaxPolicy { ValuePercent = invalidRate }), "Invalid tax report rate rejected");
        }
        RejectAction(() => AuctionTaxes.Calculate(taxed, new AuctionTaxPolicy { ValuePercent = 9 }), "Stale result at a different value-tax rate rejected");
        RejectAction(() => AuctionTaxes.Calculate(new AuctionResult { AmountScale = 100 }, new AuctionTaxPolicy()),
            "Old fractional results must be recomputed before reporting whole-apple tax");
        var large = new AuctionResult { AmountScale = 1, ValueTaxPercent = 10 };
        for (int i = 0; i < 12; i++) large.Georgies.Add(new BidderResult {
            GeorgieId = "g" + i, Won = true, Award = new LandAward { Value = 1000000, Bid = 900000 }, Payment = 900000 });
        var largeTax = AuctionTaxes.Calculate(large, new AuctionTaxPolicy { ValuePercent = 10 });
        Check(largeTax.TotalTaxTenThousandths == 12000000000L && largeTax.TotalSurplusTenThousandths == 0, "Tax totals use 64-bit precision and rent is deducted once");
        var largest = Small(1);
        largest.Georgies.Add(AuctionScenario.Bidder("a", "Ada", AuctionScenario.Bid("a", 1000000, "P0")));
        Check(LandAuction.SolveWithValueTax(largest, 1).Welfare == 990000, "Maximum legal value supports whole-apple bids without changing input limits");

        var doc = new AuctionDocument { FormatVersion = 6, Solved = true, Scenario = s, Result = taxed,
            Taxes = new AuctionTaxPolicy { ValuePercent = 10 }, TaxReport = tax };
        var restored = JsonUtility.FromJson<AuctionDocument>(JsonUtility.ToJson(doc));
        IndividualLandValues.Validate(restored.Scenario);
        Check(restored.FormatVersion == 6 && restored.Taxes.ValuePercent == 10
            && restored.Result.AmountScale == 1 && restored.Result.ValueTaxPercent == 10
            && restored.Result.Allocation.First().Bid == taxed.Allocation.First().Bid
            && AuctionTaxes.Calculate(restored.Result, restored.Taxes).TotalSurplusTenThousandths == restored.TaxReport.TotalSurplusTenThousandths,
            "Saved values, adjusted bids, rents, and net surplus round-trip including losers");

        var legacy = AuctionScenario.Example();
        Check(IndividualLandValues.Normalize(legacy) == 1, "Legacy migration flags omitted combination bid");
        IndividualLandValues.Validate(legacy);
        Check(legacy.Georgies.Single(g => g.Id == "g4").Alternatives.Single(b => b.Plots[0] == "O1").Value == 7, "Legacy explicit singleton value preserved, not inferred from joint value");
        Check(legacy.Georgies.Single(g => g.Id == "g1").Alternatives.Single(b => b.Plots[0] == "O1").Value == 0, "Missing values become zero");
        legacy.Georgies[0].Alternatives.Add(AuctionScenario.Bid("duplicate", 99, "R1"));
        IndividualLandValues.Normalize(legacy);
        Check(legacy.Georgies[0].Alternatives.Single(b => b.Plots[0] == "R1").Value == 99, "Duplicate legacy singleton offers use highest explicit value");
        var jointOnly = Small(2);
        jointOnly.Georgies.Add(AuctionScenario.Bidder("g", "Georgie", AuctionScenario.Bid("joint", 50, "P0", "P1")));
        Check(IndividualLandValues.Normalize(jointOnly) == 1 && jointOnly.Georgies[0].Alternatives.All(b => b.Value == 0), "Joint-only value is never divided between plots");
        foreach (var mutation in new Action<AuctionScenario>[] {
            x => x.Georgies[0].Alternatives.RemoveAt(0),
            x => x.Georgies[0].Alternatives[0].Plots.Add("R2"),
            x => x.Georgies[0].Alternatives[0].Plots[0] = "R2" })
        {
            var bad = IndividualLandValues.Example(); mutation(bad);
            RejectAction(() => IndividualLandValues.Validate(bad), "Incomplete, duplicate, or joint values rejected in individual mode");
        }
        s.Plots.Add(new LandPlot { Id = "P1", Name = "New plot", Kind = "Field" });
        s.Georgies.Add(AuctionScenario.Bidder("new", "New Georgie"));
        IndividualLandValues.Normalize(s); IndividualLandValues.Validate(s);
        Check(s.Georgies.All(g => g.Alternatives.Single(b => b.Plots[0] == "P1").Value == 0)
            && s.Georgies.Last().Alternatives.All(b => b.Value == 0), "New plots and Georgies start with zero values");

        var random = new System.Random(8421);
        for (int test = 0; test < 40; test++)
        {
            var sample = Small(random.Next(1, 5));
            int count = random.Next(1, 6);
            for (int i = 0; i < count; i++) sample.Georgies.Add(AuctionScenario.Bidder("g" + i, "Georgie " + i));
            IndividualLandValues.Normalize(sample);
            foreach (var georgie in sample.Georgies)
                foreach (var offer in georgie.Alternatives) offer.Value = random.Next(0, 25);
            IndividualLandValues.Validate(sample);
            var result = LandAuction.Solve(sample); Verify(sample, result);
            var rates = new AuctionTaxPolicy { ValuePercent = random.Next(101) };
            var withBids = LandAuction.SolveWithValueTax(sample, rates.ValuePercent);
            VerifyTaxedBids(sample, withBids, rates.ValuePercent);
            var report = AuctionTaxes.Calculate(withBids, rates);
            Check(report.TotalValue * 10000 == report.TotalRentHundredths * 100 + report.TotalTaxTenThousandths + report.TotalSurplusTenThousandths, "Randomized aggregate net accounting");
        }
    }

    static void RejectAction(Action action, string message)
    {
        bool rejected = false;
        try { action(); } catch (ArgumentException) { rejected = true; }
        Check(rejected, message);
    }

    static void VerifyTaxedBids(AuctionScenario values, AuctionResult result, int percent)
    {
        // The independent enumerator receives transformed bids, not the solver's coefficients.
        var bids = JsonUtility.FromJson<AuctionScenario>(JsonUtility.ToJson(values));
        foreach (var georgie in bids.Georgies)
            foreach (var offer in georgie.Alternatives)
                offer.Value -= (int)decimal.Round(offer.Value * percent / 100m, 0, MidpointRounding.AwayFromZero);
        Check(result.AmountScale == 1 && result.ValueTaxPercent == percent, "Bid result records whole-apple scale and tax basis");
        Check(result.Welfare == Oracle(bids, null) && result.Welfare == result.Allocation.Sum(a => a.Bid), "Auction optimizes adjusted bids, independently checked");
        Check(result.Allocation.SelectMany(a => a.Plots).Distinct().Count() == result.Allocation.Sum(a => a.Plots.Count), "Taxed allocation has no overlap");
        Check(result.Allocation.Select(a => a.GeorgieId).Distinct().Count() == result.Allocation.Count, "Taxed allocation permits one offer per Georgie");
        foreach (var person in result.Georgies)
        {
            long bid = person.Award?.Bid ?? 0;
            Check(person.WithoutWelfare == Oracle(bids, person.GeorgieId), "Counterfactual also optimizes adjusted bids");
            Check(person.Payment == person.WithoutWelfare - (result.Welfare - bid) && person.Payment >= 0 && person.Payment <= bid, "VCG rent uses bid externality, not gross value");
            Check(person.WithoutAllocation.All(a => a.GeorgieId != person.GeorgieId) && person.WithoutAllocation.Sum(a => a.Bid) == person.WithoutWelfare, "Counterfactual bid breakdown is inspectable");
            if (!person.Won) continue;
            long original = values.Georgies.Single(g => g.Id == person.GeorgieId).Alternatives.Single(b => b.Id == person.Award.AlternativeId).Value;
            Check(person.Award.Value == original && bid == original - decimal.Round(original * percent / 100m, 0, MidpointRounding.AwayFromZero),
                "Gross value and actual bid remain distinct after whole-apple tax rounding");
        }
        Check(result.Revenue == result.Georgies.Sum(g => g.Payment), "Adjusted rents sum to auction total");
    }

    static AuctionScenario Small(int plots)
    {
        var s = new AuctionScenario();
        for (int i = 0; i < plots; i++) s.Plots.Add(new LandPlot { Id = "P" + i, Name = "Plot " + i, Kind = "Field" });
        return s;
    }
    static void Reject(AuctionScenario source, Action<AuctionScenario> mutate)
    {
        var s = JsonUtility.FromJson<AuctionScenario>(JsonUtility.ToJson(source)); mutate(s);
        bool rejected = false;
        try { LandAuction.Solve(s); } catch (ArgumentException) { rejected = true; }
        Check(rejected, "Invalid input rejected");
    }
    static void Verify(AuctionScenario s, AuctionResult r)
    {
        Check(r.Welfare == Oracle(s, null), "CP-SAT agrees with independent exhaustive welfare");
        Check(r.Welfare == r.Allocation.Sum(a => a.Value), "Allocation accounts for welfare");
        Check(r.Allocation.SelectMany(a => a.Plots).Distinct().Count() == r.Allocation.Sum(a => a.Plots.Count), "No double allocation");
        Check(r.Allocation.Select(a => a.GeorgieId).Distinct().Count() == r.Allocation.Count, "At most one alternative per Georgie");
        Check(r.Revenue == r.Georgies.Sum(g => g.Payment), "Revenue accounting");
        foreach (var g in r.Georgies)
        {
            long value = g.Award?.Value ?? 0;
            Check(g.WithoutWelfare == Oracle(s, g.GeorgieId), "Counterfactual optimum agrees with oracle");
            Check(g.Payment == g.WithoutWelfare - (r.Welfare - value), "Clarke pivot payment");
            Check(g.Payment >= 0 && g.Payment <= value && g.Utility == value - g.Payment, "Nonnegative payment and declared utility");
            Check(g.WithoutAllocation.All(a => a.GeorgieId != g.GeorgieId) && g.WithoutAllocation.Sum(a => a.Value) == g.WithoutWelfare, "Inspectable counterfactual allocation");
            Check(g.WithoutAllocation.SelectMany(a => a.Plots).Distinct().Count() == g.WithoutAllocation.Sum(a => a.Plots.Count), "Counterfactual has no overlap");
        }
    }
    // Deliberately independent exhaustive oracle, used only on tiny validation scenarios.
    static long Oracle(AuctionScenario s, string excluded)
    {
        var people = s.Georgies.Where(g => g.Id != excluded).ToList();
        return Enumerate(people, 0, new HashSet<string>());
    }
    static long Enumerate(List<LandBidder> people, int at, HashSet<string> used)
    {
        if (at == people.Count) return 0;
        long best = Enumerate(people, at + 1, used);
        foreach (var b in people[at].Alternatives.Where(b => !b.Plots.Any(used.Contains)))
        {
            var next = new HashSet<string>(used); next.UnionWith(b.Plots);
            best = Math.Max(best, b.Value + Enumerate(people, at + 1, next));
        }
        return best;
    }
    [Serializable] public sealed class Awards { public List<LandAward> Items; }
    static Awards AllocationWrapper(this AuctionResult r) => new Awards { Items = r.Allocation };
}
