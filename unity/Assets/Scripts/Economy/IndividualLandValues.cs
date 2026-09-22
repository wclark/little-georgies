using System;
using System.Linq;

namespace LittleGeorgies.Economy
{
    public static class IndividualLandValues
    {
        public static AuctionScenario Example()
        {
            var scenario = AuctionScenario.Example();
            scenario.Georgies.Single(g => g.Id == "g4").Alternatives.Clear();
            scenario.Georgies.Single(g => g.Id == "g4").Alternatives.AddRange(new[] {
                AuctionScenario.Bid("O1", 15, "O1"), AuctionScenario.Bid("O2", 12, "O2")
            });
            Normalize(scenario);
            return scenario;
        }

        // Retain only explicit singleton values. Joint values cannot be split into inferred plot values.
        public static int Normalize(AuctionScenario scenario)
        {
            scenario.Validate();
            int omitted = 0;
            foreach (var person in scenario.Georgies)
            {
                omitted += person.Alternatives.Count(b => b.Plots.Count != 1);
                var old = person.Alternatives;
                person.Alternatives = scenario.Plots.Select(p => AuctionScenario.Bid(p.Id,
                    old.Where(b => b.Plots.Count == 1 && b.Plots[0] == p.Id).Select(b => b.Value).DefaultIfEmpty(0).Max(), p.Id)).ToList();
            }
            return omitted;
        }

        public static void Validate(AuctionScenario scenario)
        {
            scenario.Validate();
            foreach (var person in scenario.Georgies)
                if (person.Alternatives.Count != scenario.Plots.Count
                    || person.Alternatives.Any(b => b.Plots.Count != 1)
                    || person.Alternatives.Select(b => b.Plots[0]).Distinct().Count() != scenario.Plots.Count)
                    throw new ArgumentException(person.Name + ": enter exactly one value per plot; combinations are not enabled.");
        }
    }
}
