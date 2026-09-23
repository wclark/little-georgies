using System;
using System.Collections.Generic;
using System.Linq;

namespace LittleGeorgies.Economy
{
    [Serializable] public sealed class PlotPreference
    {
        public int GeorgieId;
        public string PlotId;
        public int Adjustment;
    }

    // One dawn snapshot connects the existing society rules to the exact land auction.
    [Serializable] public sealed class SettlementEconomy
    {
        public int Version = 1;
        public Society Society;
        public int TaxPercent;
        public int Revision;
        public List<LandPlot> Plots = new List<LandPlot>();
        public List<PlotPreference> Preferences = new List<PlotPreference>();
        public DayReport LastDay;
        public AuctionTaxReport LastEconomy;

        public static SettlementEconomy Create(bool village = false)
        {
            var session = new SettlementEconomy { Society = Society.Create(village) };
            session.Plots.AddRange(new[] {
                new LandPlot { Id = "O1", Name = "Old orchard", Kind = "Orchard" },
                new LandPlot { Id = "O2", Name = "North grove", Kind = "Orchard" },
                new LandPlot { Id = "O3", Name = "River apples", Kind = "Orchard" },
                new LandPlot { Id = "F1", Name = "East meadow", Kind = "Field" },
                new LandPlot { Id = "O4", Name = "South grove", Kind = "Orchard" },
                new LandPlot { Id = "O5", Name = "West orchard", Kind = "Orchard" },
                new LandPlot { Id = "F2", Name = "Open field", Kind = "Field" }
            });
            return session;
        }

        public void Changed()
        {
            if (TaxPercent < 0 || TaxPercent > 100) throw new ArgumentOutOfRangeException(nameof(TaxPercent));
            Society.ReplanDay();
            Revision++;
        }

        public int Value(Georgie person, LandPlot plot)
        {
            if (person.Role != Role.Gatherer && person.Role != Role.Farmer) return 0;
            var preference = Preferences.FirstOrDefault(p => p.GeorgieId == person.Id && p.PlotId == plot.Id);
            int affinity = person.Id == 1 || plot.Kind == "Field" ? 0 : ((person.Id + Plots.IndexOf(plot)) % 3 == 0 ? 1 : 0);
            int value = Math.Max(0, person.Yield + (plot.Kind == "Field" ? -1 : 0) + affinity + (preference?.Adjustment ?? 0));
            return person.Mood == Mood.Broken ? Math.Min(1, value) : Math.Min(9, value);
        }

        public void SetValue(int personId, string plotId, int value)
        {
            if (value < 0 || value > 9) throw new ArgumentOutOfRangeException(nameof(value));
            var person = Society.People.Single(p => p.Id == personId);
            var plot = Plots.Single(p => p.Id == plotId);
            if (person.Mood == Mood.Broken || (person.Role != Role.Gatherer && person.Role != Role.Farmer))
                throw new InvalidOperationException("This Georgie's productivity cannot be edited now.");
            var preference = Preferences.FirstOrDefault(p => p.GeorgieId == personId && p.PlotId == plotId);
            if (preference == null) { preference = new PlotPreference { GeorgieId = personId, PlotId = plotId }; Preferences.Add(preference); }
            preference.Adjustment = 0;
            preference.Adjustment = value - Value(person, plot);
            Changed();
        }

        public AuctionScenario Scenario()
        {
            var scenario = new AuctionScenario();
            scenario.Plots = Plots.Select(p => new LandPlot { Id = p.Id, Name = p.Name, Kind = p.Kind }).ToList();
            foreach (var person in Society.People)
                scenario.Georgies.Add(AuctionScenario.Bidder("g" + person.Id, person.Title,
                    Plots.Select(plot => AuctionScenario.Bid(plot.Id, person.Job == Job.Harvest ? Value(person, plot) : 0, plot.Id)).ToArray()));
            return scenario;
        }

        public void Advance(AuctionResult result, int solvedRevision)
        {
            if (solvedRevision != Revision || result == null || result.ValueTaxPercent != TaxPercent)
                throw new InvalidOperationException("Wait for the current policy forecast before advancing.");
            var report = AuctionTaxes.Calculate(result, new AuctionTaxPolicy { ValuePercent = TaxPercent });
            var land = new Dictionary<int, HarvestSettlement>();
            foreach (var person in Society.People)
            {
                var row = report.Georgies.Single(p => p.GeorgieId == "g" + person.Id);
                land.Add(person.Id, new HarvestSettlement { Value = checked((int)row.Value),
                    Tax = checked((int)(row.TaxTenThousandths / 10000)), Rent = checked((int)(row.RentHundredths / 100)) });
            }
            Society.Produce(land);
            Society.FinishDay();
            LastDay = Society.Report;
            LastEconomy = report;
            Society.BeginDay();
            Revision++;
        }

        public void Restore()
        {
            if (Version != 1 || Society == null || Society.People == null || Society.People.Count < 1 || Society.People.Count > 16
                || Society.Policy == null || Society.People.Any(p => p == null || p.HappyHistory == null)
                || Plots == null || Preferences == null || TaxPercent < 0 || TaxPercent > 100)
                throw new ArgumentException("Invalid settlement save.");
            Society.ResumeMorning();
            IndividualLandValues.Validate(Scenario());
        }
    }
}
