using System;
using System.Collections.Generic;
using System.Linq;
using LittleGeorgies.Economy;

public static class PortableAuctionChecks
{
    static int checks;
    static void Check(bool value, string message)
    {
        checks++;
        if (!value) throw new Exception("Portable auction: " + message);
    }

    public static int Validate()
    {
        checks = 0;
        var example = IndividualLandValues.Example();
        for (int rate = 0; rate <= 100; rate++) Verify(example, rate);
        var taxed = LandAuction.SolveWithValueTax(example, 10);
        Check(taxed.Welfare == 42 && taxed.Revenue == 16, "Known rounded-tax result");
        var report = AuctionTaxes.Calculate(taxed, new AuctionTaxPolicy { ValuePercent = 10 });
        Check(report.TotalValue == 46 && report.TotalTaxTenThousandths == 40000
            && report.TotalSurplusTenThousandths == 260000, "Value, tax, rent and surplus remain unchanged");

        var random = new Random(22092026);
        for (int sample = 0; sample < 200; sample++)
        {
            var s = Scenario(random.Next(0, 5));
            int count = random.Next(0, 7);
            for (int i = 0; i < count; i++)
            {
                var person = AuctionScenario.Bidder("g" + i, "Georgie " + i);
                foreach (var plot in s.Plots)
                    if (random.Next(4) != 0)
                        person.Alternatives.Add(AuctionScenario.Bid(plot.Id, random.Next(0, 30), plot.Id));
                s.Georgies.Add(person);
            }
            Verify(s, random.Next(101));
        }
        var duplicate = Scenario(1);
        duplicate.Georgies.Add(AuctionScenario.Bidder("a", "Ada", AuctionScenario.Bid("a", 5, "P0"), AuctionScenario.Bid("b", 8, "P0")));
        duplicate.Georgies.Add(AuctionScenario.Bidder("b", "Bill", AuctionScenario.Bid("a", 6, "P0")));
        Verify(duplicate, 0);
        Check(LandAuction.Solve(duplicate).Allocation.Single().AlternativeId == "b", "Duplicate offers retain the best value");
        duplicate.Georgies[0].Alternatives[0].Value = 8;
        Check(LandAuction.Solve(duplicate).Allocation.Single().AlternativeId == "a", "Equal alternatives use stable ID");
        Verify(duplicate, 50);

        var maximum = Scenario(12);
        for (int i = 0; i < 16; i++) maximum.Georgies.Add(AuctionScenario.Bidder("g" + i.ToString("D2"), "Georgie " + i,
            maximum.Plots.Select(p => AuctionScenario.Bid(p.Id, 1000000, p.Id)).ToArray()));
        var full = LandAuction.SolveWithValueTax(maximum, 1);
        Check(full.Welfare == 11880000 && full.Revenue == full.Welfare, "Maximum input sizes and integer totals");
        Check(full.Allocation.Count == 12 && full.Georgies.All(g => g.Utility == 0), "Full competition rents equal bids");
        Reject<TimeoutException>(() => LandAuction.Solve(example, 0));
        Reject<TimeoutException>(() => LandAuction.Solve(example, double.NaN));
        Reject<ArgumentException>(() => LandAuction.SolveWithValueTax(example, -1));
        Reject<ArgumentException>(() => LandAuction.SolveWithValueTax(example, 101));
        Reject<ArgumentException>(() => AuctionTaxes.Calculate(taxed, new AuctionTaxPolicy()));
        example.Georgies[0].Alternatives[0].Value = -1;
        Reject<ArgumentException>(() => LandAuction.Solve(example));
        return checks;
    }

    static AuctionScenario Scenario(int plots)
    {
        var s = new AuctionScenario();
        for (int i = 0; i < plots; i++) s.Plots.Add(new LandPlot { Id = "P" + i, Name = "Plot " + i, Kind = "Field" });
        return s;
    }

    static void Verify(AuctionScenario scenario, int rate)
    {
        var result = LandAuction.SolveWithValueTax(scenario, rate);
        var oracle = Oracle(scenario, rate, null);
        Check(result.Welfare == oracle.Value, "Independent exhaustive optimum");
        Check(Key(result.Allocation) == oracle.Key, "Independent canonical tie break");
        Check(result.Allocation.SelectMany(a => a.Plots).Distinct().Count() == result.Allocation.Count, "Plots not assigned twice");
        Check(result.Allocation.Select(a => a.GeorgieId).Distinct().Count() == result.Allocation.Count, "One plot per Georgie");
        foreach (var row in result.Georgies)
        {
            long bid = row.Award == null ? 0 : row.Award.Bid;
            var without = Oracle(scenario, rate, row.GeorgieId);
            Check(row.WithoutWelfare == without.Value && row.WithoutAllocation.Sum(a => a.Bid) == without.Value, "Exact counterfactual witness");
            Check(row.Payment == without.Value - (result.Welfare - bid), "VCG externality rent");
            Check(row.Payment >= 0 && row.Payment <= bid && row.Utility == bid - row.Payment, "Nonnegative after-tax surplus");
        }
        var report = AuctionTaxes.Calculate(result, new AuctionTaxPolicy { ValuePercent = rate });
        Check(report.TotalValue * 10000 == report.TotalTaxTenThousandths + report.TotalRentHundredths * 100
            + report.TotalSurplusTenThousandths, "Aggregate accounting identity");
        Check(report.Georgies.All(g => g.TaxTenThousandths % 10000 == 0 && g.RentHundredths % 100 == 0), "Whole-apple amounts");
        string key = Key(result.Allocation);
        scenario.Plots.Reverse(); scenario.Georgies.Reverse();
        foreach (var person in scenario.Georgies) person.Alternatives.Reverse();
        var reordered = LandAuction.SolveWithValueTax(scenario, rate);
        Check(Key(reordered.Allocation) == key && reordered.Revenue == result.Revenue, "Input order has no effect");
        scenario.Plots.Reverse(); scenario.Georgies.Reverse();
        foreach (var person in scenario.Georgies) person.Alternatives.Reverse();
    }

    static string Key(IEnumerable<LandAward> awards) => string.Join("|", awards.Select(a => a.GeorgieId + ":" + a.AlternativeId));

    // Small exhaustive oracle is deliberately independent of the production assignment algorithm.
    static (long Value, string Key) Oracle(AuctionScenario scenario, int rate, string excluded)
    {
        var people = scenario.Georgies.Where(g => g.Id != excluded).OrderBy(g => g.Id, StringComparer.Ordinal).ToList();
        var used = new HashSet<string>();
        var choices = new List<string>();
        long best = -1;
        string bestKey = "";
        void Search(int index, long value)
        {
            if (index == people.Count)
            {
                if (value > best) { best = value; bestKey = string.Join("|", choices); }
                return;
            }
            var person = people[index];
            foreach (var offer in person.Alternatives.OrderBy(b => b.Id, StringComparer.Ordinal))
            {
                long bid = offer.Value - (offer.Value * (long)rate + 50) / 100;
                if (bid <= 0 || !used.Add(offer.Plots[0])) continue;
                choices.Add(person.Id + ":" + offer.Id);
                Search(index + 1, value + bid);
                choices.RemoveAt(choices.Count - 1);
                used.Remove(offer.Plots[0]);
            }
            Search(index + 1, value);
        }
        Search(0, 0);
        return (best, bestKey);
    }

    static void Reject<T>(Action action) where T : Exception
    {
        bool rejected = false;
        try { action(); } catch (T) { rejected = true; }
        Check(rejected, "Invalid request rejected: " + typeof(T).Name);
    }
}
