using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
#if UNITY_EDITOR_WIN
using Google.OrTools.Sat;
#endif

namespace LittleGeorgies.Economy
{
    [Serializable] public sealed class LandPlot
    {
        public string Id;
        public string Name;
        public string Kind;
    }

    [Serializable] public sealed class BundleValue
    {
        public string Id;
        public List<string> Plots = new List<string>();
        public int Value;
    }

    [Serializable] public sealed class LandBidder
    {
        public string Id;
        public string Name;
        public List<BundleValue> Alternatives = new List<BundleValue>();
    }

    [Serializable] public sealed class AuctionScenario
    {
        public int Version = 1;
        public List<LandPlot> Plots = new List<LandPlot>();
        public List<LandBidder> Georgies = new List<LandBidder>();

        public static AuctionScenario Example()
        {
            var s = new AuctionScenario();
            s.Plots.AddRange(new[] {
                new LandPlot { Id = "R1", Name = "Hilltop", Kind = "Residential" },
                new LandPlot { Id = "R2", Name = "Riverside", Kind = "Residential" },
                new LandPlot { Id = "O1", Name = "West orchard", Kind = "Orchard" },
                new LandPlot { Id = "O2", Name = "East orchard", Kind = "Orchard" }
            });
            s.Georgies.Add(Bidder("g1", "Mara", Bid("a1", 12, "R1"), Bid("a2", 10, "R2")));
            s.Georgies.Add(Bidder("g2", "Ada", Bid("a1", 11, "R1"), Bid("a2", 8, "R2")));
            s.Georgies.Add(Bidder("g3", "Nia", Bid("a1", 8, "R1"), Bid("a2", 7, "R2")));
            s.Georgies.Add(Bidder("g4", "Sol", Bid("a1", 22, "O1", "O2"), Bid("a2", 7, "O1"), Bid("a3", 7, "O2")));
            s.Georgies.Add(Bidder("g5", "Ivo", Bid("a1", 13, "O1"), Bid("a2", 9, "O2")));
            return s;
        }

        public static BundleValue Bid(string id, int value, params string[] plots) =>
            new BundleValue { Id = id, Value = value, Plots = plots.ToList() };
        public static LandBidder Bidder(string id, string name, params BundleValue[] bids) =>
            new LandBidder { Id = id, Name = name, Alternatives = bids.ToList() };

        public void Validate()
        {
            if (Version != 1) throw new ArgumentException("Unsupported auction version.");
            if (Plots == null || Plots.Count > 12) throw new ArgumentException("Use at most 12 plots.");
            if (Georgies == null || Georgies.Count > 16) throw new ArgumentException("Use at most 16 Georgies.");
            var plots = new HashSet<string>();
            foreach (var p in Plots)
            {
                if (p == null) throw new ArgumentException("Missing plot.");
                CheckId(p.Id, plots, "plot");
                CheckName(p.Name, "plot");
                if (p.Kind != "Residential" && p.Kind != "Orchard" && p.Kind != "Field")
                    throw new ArgumentException("Plot kind must be Residential, Orchard or Field.");
            }
            var people = new HashSet<string>();
            foreach (var g in Georgies)
            {
                if (g == null) throw new ArgumentException("Missing Georgie.");
                CheckId(g.Id, people, "Georgie");
                CheckName(g.Name, "Georgie");
                if (g.Alternatives == null || g.Alternatives.Count > 16)
                    throw new ArgumentException(g.Name + ": use at most 16 alternatives.");
                var ids = new HashSet<string>();
                foreach (var b in g.Alternatives)
                {
                    if (b == null) throw new ArgumentException(g.Name + ": missing alternative.");
                    CheckId(b.Id, ids, "alternative");
                    if (b.Value < 0 || b.Value > 1000000)
                        throw new ArgumentException(g.Name + ": values must be whole apples from 0 to 1,000,000.");
                    if (b.Plots == null || b.Plots.Count == 0 || b.Plots.Count > 12 || b.Plots.Distinct().Count() != b.Plots.Count || b.Plots.Any(p => !plots.Contains(p)))
                        throw new ArgumentException(g.Name + ": every alternative needs a nonempty bundle of distinct existing plots.");
                }
            }
        }

        static void CheckId(string id, HashSet<string> used, string kind)
        {
            if (string.IsNullOrWhiteSpace(id) || id.Length > 40 || !id.All(c => char.IsLetterOrDigit(c) || c == '-' || c == '_') || !used.Add(id))
                throw new ArgumentException("Each " + kind + " needs a unique ID (letters, numbers, - or _; max 40).");
        }
        static void CheckName(string name, string kind)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Length > 32 || name.Any(char.IsControl))
                throw new ArgumentException("Each " + kind + " needs a name of 1-32 characters.");
        }
    }

    [Serializable] public sealed class LandAward
    {
        public string GeorgieId;
        public string AlternativeId;
        public List<string> Plots = new List<string>();
        public long Value;
        public long Bid;
    }
    [Serializable] public sealed class BidderResult
    {
        public string GeorgieId;
        public bool Won;
        public LandAward Award;
        public long WithoutWelfare;
        public long OthersWelfare;
        public long Payment;
        public long Utility;
        public List<LandAward> WithoutAllocation = new List<LandAward>();
    }
    [Serializable] public sealed class AuctionResult
    {
        public string Solver = "HungarianAlgorithm / managed exact single-plot VCG";
        // Objective, bids, rents, and counterfactual amounts use this many units per apple.
        // LandAward.Value remains the original whole-apple valuation.
        public int AmountScale = 1;
        public int ValueTaxPercent;
        public long Welfare;
        public long Revenue;
        public int Solves;
        public long Milliseconds;
        public List<LandAward> Allocation = new List<LandAward>();
        public List<BidderResult> Georgies = new List<BidderResult>();
    }
    [Serializable] public sealed class AuctionDocument
    {
        public int FormatVersion;
        public bool Solved;
        public AuctionScenario Scenario;
        public AuctionResult Result;
        public AuctionTaxPolicy Taxes;
        public AuctionTaxReport TaxReport;
    }

    public static class LandAuction
    {
#if UNITY_EDITOR_WIN
        sealed class Choice
        {
            public LandBidder Person;
            public BundleValue Bundle;
            public long Bid;
            public BoolVar Variable;
        }
#endif
        sealed class Outcome
        {
            public long Welfare;
            public List<LandAward> Awards = new List<LandAward>();
        }
        sealed class Run
        {
            public readonly Stopwatch Clock = Stopwatch.StartNew();
            public int Solves;
            public readonly double Seconds;
            public readonly int TaxPercent;
            public bool Reference;
            public Run(double seconds, int taxPercent) { Seconds = seconds; TaxPercent = taxPercent; }
            public long Bid(BundleValue offer) => offer.Value - AuctionTaxes.TaxOnValue(offer.Value, TaxPercent);
            public void CheckTime()
            {
                if (Clock.Elapsed.TotalSeconds >= Seconds)
                    throw new TimeoutException("Exact auction timed out. Reduce the auction size; no approximate rents were computed.");
            }
#if UNITY_EDITOR_WIN
            public CpSolver Solve(CpModel model)
            {
                double remaining = Seconds - Clock.Elapsed.TotalSeconds;
                if (remaining <= 0) throw new TimeoutException("Exact auction timed out. Reduce the auction size; no approximate rents were computed.");
                var solver = new CpSolver { StringParameters = "num_search_workers:1 random_seed:0 max_time_in_seconds:"
                    + remaining.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) };
                Solves++;
                var status = solver.Solve(model);
                if (status != CpSolverStatus.Optimal)
                    throw new InvalidOperationException("Exact optimum not established (" + status + "). No VCG results issued.");
                return solver;
            }
#endif
        }

        public static AuctionResult Solve(AuctionScenario scenario, double seconds = 15)
            => SolveCore(scenario, 0, seconds);

        public static AuctionResult SolveWithValueTax(AuctionScenario scenario, int valueTaxPercent, double seconds = 15)
        {
            if (valueTaxPercent < 0 || valueTaxPercent > 100) throw new ArgumentException("Tax must be from 0 to 100 percent.");
            return SolveCore(scenario, valueTaxPercent, seconds);
        }

#if UNITY_EDITOR_WIN
        public static AuctionResult SolveReference(AuctionScenario scenario, int taxPercent = 0)
            => SolveCore(scenario, taxPercent, 15, true);
#endif

        static AuctionResult SolveCore(AuctionScenario scenario, int taxPercent, double seconds, bool reference = false)
        {
            if (scenario == null) throw new ArgumentException("Missing auction scenario.");
            scenario.Validate();
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds <= 0)
                throw new TimeoutException("A finite positive exact-solve budget is required.");
            var run = new Run(seconds, taxPercent) { Reference = reference };
            var chosen = Optimize(scenario, null, run);
            var result = new AuctionResult { Welfare = chosen.Welfare, Allocation = chosen.Awards,
                AmountScale = 1, ValueTaxPercent = taxPercent };
            if (reference || scenario.Georgies.Any(g => g.Alternatives.Any(b => b.Plots.Count != 1)))
                result.Solver = "OR-Tools 9.15 / CP-SAT / editor-only exact XOR VCG";
            foreach (var person in scenario.Georgies.OrderBy(p => p.Id, StringComparer.Ordinal))
            {
                var award = chosen.Awards.SingleOrDefault(a => a.GeorgieId == person.Id);
                // Removing a loser leaves the selected optimum feasible, so no new solve is necessary.
                var without = award == null ? chosen : Optimize(scenario, person.Id, run);
                long bid = award?.Bid ?? 0;
                long others = chosen.Welfare - bid;
                long payment = without.Welfare - others;
                if (payment < 0 || payment > bid) throw new InvalidOperationException("VCG rent invariant failed.");
                result.Georgies.Add(new BidderResult { GeorgieId = person.Id, Won = award != null, Award = award,
                    WithoutWelfare = without.Welfare, OthersWelfare = others, Payment = payment,
                    Utility = bid - payment, WithoutAllocation = without.Awards.ToList() });
                result.Revenue += payment;
            }
            result.Solves = run.Solves;
            result.Milliseconds = run.Clock.ElapsedMilliseconds;
            return result;
        }

        static Outcome Optimize(AuctionScenario scenario, string excluded, Run run)
        {
            run.CheckTime();
            if (!run.Reference && scenario.Georgies.All(g => g.Alternatives.All(b => b.Plots.Count == 1)))
                return OptimizeSinglePlots(scenario, excluded, run);
#if UNITY_EDITOR_WIN
            AuctionNative.Initialize();
            return OptimizeBundles(scenario, excluded, run);
#else
            throw new PlatformNotSupportedException("Combination auctions are editor-only research. The game supports individual plot values.");
#endif
        }

        static Outcome OptimizeSinglePlots(AuctionScenario scenario, string excluded, Run run)
        {
            var people = scenario.Georgies.Where(p => p.Id != excluded).OrderBy(p => p.Id, StringComparer.Ordinal).ToList();
            var available = new HashSet<string>(scenario.Plots.Select(p => p.Id));
            long remaining = BestAssignment(people, available, run);
            var result = new Outcome { Welfare = remaining };
            // Fix canonical choices only when the remaining exact optimum is still attainable.
            // This avoids huge lexicographic weights and preserves the original CP-SAT tie rule.
            for (int i = 0; i < people.Count; i++)
            {
                var person = people[i];
                var later = people.Skip(i + 1).ToList();
                bool selected = false;
                foreach (var offer in person.Alternatives.OrderBy(b => b.Id, StringComparer.Ordinal))
                {
                    long bid = run.Bid(offer);
                    string plot = offer.Plots[0];
                    if (bid <= 0 || !available.Remove(plot)) continue;
                    if (bid + BestAssignment(later, available, run) == remaining)
                    {
                        result.Awards.Add(new LandAward { GeorgieId = person.Id, AlternativeId = offer.Id,
                            Plots = new List<string> { plot }, Value = offer.Value, Bid = bid });
                        remaining -= bid;
                        selected = true;
                        break;
                    }
                    available.Add(plot);
                }
                if (!selected && BestAssignment(later, available, run) != remaining)
                    throw new InvalidOperationException("Canonical allocation lost the exact optimum.");
            }
            return result;
        }

        static long BestAssignment(List<LandBidder> people, HashSet<string> available, Run run)
        {
            run.CheckTime();
            if (people.Count == 0 || available.Count == 0) return 0;
            var plots = available.OrderBy(p => p, StringComparer.Ordinal).ToList();
            // Dummy columns let every bidder decline. Missing offers have zero benefit.
            var bids = new int[people.Count, plots.Count + people.Count];
            var costs = new int[people.Count, plots.Count + people.Count];
            for (int i = 0; i < people.Count; i++)
            {
                foreach (var offer in people[i].Alternatives)
                {
                    int column = plots.IndexOf(offer.Plots[0]);
                    if (column >= 0) bids[i, column] = Math.Max(bids[i, column], checked((int)run.Bid(offer)));
                }
                for (int j = 0; j < costs.GetLength(1); j++) costs[i, j] = 1000000 - bids[i, j];
            }
            var assignment = HungarianAlgorithm.HungarianAlgorithm.FindAssignments(costs);
            run.Solves++;
            run.CheckTime();
            long welfare = 0;
            for (int i = 0; i < people.Count; i++) welfare += bids[i, assignment[i]];
            return welfare;
        }

#if UNITY_EDITOR_WIN
        static Outcome OptimizeBundles(AuctionScenario scenario, string excluded, Run run)
        {
            var model = new CpModel();
            var choices = new List<Choice>();
            foreach (var p in scenario.Georgies.OrderBy(p => p.Id, StringComparer.Ordinal).Where(p => p.Id != excluded))
                foreach (var b in p.Alternatives.OrderBy(b => b.Id, StringComparer.Ordinal).Where(b => run.Bid(b) > 0))
                    choices.Add(new Choice { Person = p, Bundle = b, Bid = run.Bid(b), Variable = model.NewBoolVar(p.Id + ":" + b.Id) });
            if (choices.Count == 0) return new Outcome();
            foreach (var group in choices.GroupBy(c => c.Person.Id)) model.Add(LinearExpr.Sum(group.Select(c => c.Variable)) <= 1);
            foreach (var plot in scenario.Plots)
                model.Add(LinearExpr.Sum(choices.Where(c => c.Bundle.Plots.Contains(plot.Id)).Select(c => c.Variable)) <= 1);
            var welfare = LinearExpr.WeightedSum(choices.Select(c => c.Variable), choices.Select(c => c.Bid));
            model.Maximize(welfare);
            var solver = run.Solve(model);
            long best = solver.Value(welfare);
            model.Add(welfare == best);
            // Canonical tie break: stable Georgie ID, then alternative ID. Welfare always comes first.
            foreach (var group in choices.GroupBy(c => c.Person.Id))
            {
                var list = group.ToList();
                var priority = LinearExpr.WeightedSum(list.Select(c => c.Variable), list.Select((c, i) => (long)(list.Count - i)));
                model.Maximize(priority);
                solver = run.Solve(model);
                model.Add(priority == solver.Value(priority));
            }
            var outcome = new Outcome { Welfare = best };
            foreach (var c in choices.Where(c => solver.Value(c.Variable) == 1))
                outcome.Awards.Add(new LandAward { GeorgieId = c.Person.Id, AlternativeId = c.Bundle.Id,
                    Plots = c.Bundle.Plots.OrderBy(p => p, StringComparer.Ordinal).ToList(), Value = c.Bundle.Value, Bid = c.Bid });
            return outcome;
        }
#endif
    }
}
