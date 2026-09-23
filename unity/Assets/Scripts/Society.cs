using System;
using System.Collections.Generic;
using System.Linq;

namespace LittleGeorgies
{
    public enum Role { Gatherer, Chief, Farmer, Builder }
    public enum Mood { Broken, Tired, Happy }
    public enum Job { Harvest, Rest, Administer, Baskets, Houses }
    public enum FoodRule { SharedStore, PersonalHarvest }
    public enum WorkPlan { Policy, Work, Rest, Baskets, Houses }

    public sealed class HarvestSettlement
    {
        public int Value, Tax, Rent;
    }

    [Serializable]
    public sealed class Policies
    {
        public FoodRule Food = FoodRule.SharedStore;
        public bool RestWhenFed = true;
        public int LevyPercent = 25;
        public bool FeedHungry = true;
        public bool AssignBaskets = true;
        public bool AssignHomes = true;
        public bool BuildHomes;
        public Policies Copy() => (Policies)MemberwiseClone();
    }

    [Serializable]
    public sealed class Georgie
    {
        public int Id;
        public string Name;
        public Role Role;
        public Mood Mood = Mood.Happy;
        public Job Job;
        public WorkPlan Plan;
        public int Apples;
        public bool Basket;
        public bool House;
        public double TaxRemainder;
        public List<bool> HappyHistory = new List<bool>();
        public int Yield => Role == Role.Gatherer ? (Mood == Mood.Broken ? 1 : 2)
            : Role == Role.Farmer ? (Mood == Mood.Broken ? 1 : (Mood == Mood.Happy ? 3 : 2) + (Basket ? 2 : 0)) : 0;
        public double HappyRate => HappyHistory.Count == 0 ? 1 : HappyHistory.Count(h => h) / (double)HappyHistory.Count;
        public string Title => Role == Role.Chief ? "Chief " + Name : Name;
        public void RecordMood()
        {
            HappyHistory.Add(Mood == Mood.Happy);
            if (HappyHistory.Count > 10) HappyHistory.RemoveAt(0);
        }
    }

    [Serializable]
    public sealed class DayReport
    {
        public int Day, Harvested, Levied, RentCollected, Distributed, Ate, Hungry, BasketsMade, HousesMade;
        public string Summary => $"Day {Day}: {Harvested} harvested, {Ate} fed, {Hungry} hungry."
            + (Levied > 0 || RentCollected > 0 || Distributed > 0 ? $" Tax {Levied}; rent {RentCollected}; relief {Distributed}." : "");
    }

    // Economy is independent of frames, sprites and Unity. Dawn snapshots policy; dusk settles dinner.
    [Serializable]
    public sealed class Society
    {
        static readonly string[] Names = { "Ada", "Mara", "Nell", "Bo", "Ira", "Sol", "June", "Fern", "Lio" };
        static readonly int[] GrowthTargets = { 0, 4, 10, 18, 26 };
        public int Day = 1;
        public int CommonApples, TotalHarvest, FreeBaskets, FreeHomes, HouseProgress;
        public bool Specialist;
        public bool HousingUnlocked;
        public List<Georgie> People = new List<Georgie>();
        public Policies Policy = new Policies();
        public Policies Today;
        public DayReport Report = new DayReport();
        public List<string> Ledger = new List<string>();
        bool produced;
        bool dayOpen;
        public int AllApples => CommonApples + People.Sum(p => p.Apples);
        public int HappyCount => People.Count(p => p.Mood == Mood.Happy);
        public int TotalHomes => FreeHomes + People.Count(p => p.House);
        public int TotalBaskets => FreeBaskets + People.Count(p => p.Basket);
        public int NextGrowthTarget => Specialist ? 0 : People.Count < 5 ? GrowthTargets[People.Count] : 30;
        public string StageName => !Specialist ? People.Count == 1 ? "A lone gatherer" : "A gathering band"
            : HousingUnlocked ? "Building a village" : "Farmers and builders";
        public double HappyRate
        {
            get
            {
                int turns = People.Sum(p => p.HappyHistory.Count);
                return turns == 0 ? 1 : People.Sum(p => p.HappyHistory.Count(h => h)) / (double)turns;
            }
        }

        public static Society Create(bool village = false)
        {
            var s = new Society();
            s.People.Add(new Georgie { Id = 1, Name = village ? "Henry" : "Little Georgie", Role = village ? Role.Chief : Role.Gatherer });
            if (village)
            {
                s.Specialist = true;
                s.HousingUnlocked = true;
                for (int i = 0; i < 7; i++)
                    s.People.Add(new Georgie { Id = i + 2, Name = Names[i], Role = i < 5 ? Role.Farmer : Role.Builder });
                s.CommonApples = 12;
                s.FreeBaskets = 2;
                s.FreeHomes = 2;
                s.Note("A small village is ready to try your policies.");
            }
            else s.Note("One gatherer has arrived. The orchard is open.");
            s.BeginDay();
            return s;
        }

        public void BeginDay()
        {
            if (dayOpen) throw new InvalidOperationException("Finish the current day before beginning another.");
            dayOpen = true;
            produced = false;
            ReplanDay();
        }

        // Saves are made only at dawn, before production or dinner has been applied.
        public void ResumeMorning()
        {
            dayOpen = true;
            produced = false;
            ReplanDay();
        }

        public void ReplanDay()
        {
            if (!dayOpen || produced) throw new InvalidOperationException("This day can no longer be replanned.");
            Today = Policy.Copy();
            if (!Specialist)
            {
                Today.Food = FoodRule.SharedStore;
                Today.BuildHomes = Today.AssignBaskets = Today.AssignHomes = false;
            }
            if (!HousingUnlocked) Today.BuildHomes = false;
            Report = new DayReport { Day = Day };
            foreach (var p in People)
                p.Job = p.Role == Role.Builder ? (Today.BuildHomes && p.Mood != Mood.Broken ? Job.Houses : Job.Baskets)
                    : p.Role == Role.Chief ? Job.Administer : Job.Harvest;

            int projected = AllApples + People.Sum(p => p.Yield);
            foreach (var p in People)
            {
                if (p.Plan == WorkPlan.Rest && p.Mood != Mood.Broken) { p.Job = Job.Rest; projected -= p.Yield; }
                if (p.Role == Role.Builder && p.Plan == WorkPlan.Baskets) p.Job = Job.Baskets;
                if (p.Role == Role.Builder && p.Plan == WorkPlan.Houses && HousingUnlocked && p.Mood != Mood.Broken) p.Job = Job.Houses;
            }
            if (!Today.RestWhenFed) return;
            // Rest is an autonomous choice, reserved only when the forecast can still cover dinner.
            foreach (var p in People.OrderBy(p => p.Role == Role.Chief ? 1 : 0).ThenBy(p => p.Id))
            {
                if (p.Plan != WorkPlan.Policy || p.Mood != Mood.Tired || p.House) continue;
                bool canEat = Today.Food == FoodRule.SharedStore ? projected - p.Yield >= People.Count
                    : p.Apples > 0 || (Today.FeedHungry && CommonApples >= People.Count);
                if (!canEat) continue;
                p.Job = Job.Rest;
                projected -= p.Yield;
            }
        }

        public void Produce(IReadOnlyDictionary<int, HarvestSettlement> land = null)
        {
            if (!dayOpen || produced) return;
            produced = true;
            foreach (var p in People)
            {
                if (p.Job == Job.Harvest)
                {
                    HarvestSettlement award = null;
                    if (land != null) land.TryGetValue(p.Id, out award);
                    int output = land == null ? p.Yield : award == null ? 0 : award.Value;
                    Report.Harvested += output;
                    if (land != null)
                    {
                        int tax = award == null ? 0 : award.Tax;
                        int rent = award == null ? 0 : award.Rent;
                        if (tax < 0 || rent < 0 || tax + rent > output) throw new InvalidOperationException("Invalid land settlement.");
                        Report.Levied += tax;
                        Report.RentCollected += rent;
                        CommonApples += tax + rent;
                        if (Today.Food == FoodRule.SharedStore) CommonApples += output - tax - rent;
                        else p.Apples += output - tax - rent;
                    }
                    else if (Today.Food == FoodRule.SharedStore) CommonApples += output;
                    else
                    {
                        // Carry fractions so a 25% levy still applies to small two-apple harvests over time.
                        double due = p.TaxRemainder + output * Today.LevyPercent / 100.0;
                        int levy = Specialist && People.Any(g => g.Role == Role.Chief && g.Job == Job.Administer)
                            ? (int)Math.Floor(due + 0.000001) : 0;
                        p.TaxRemainder = levy > 0 || Specialist ? due - Math.Floor(due + 0.000001) : 0;
                        CommonApples += levy;
                        p.Apples += output - levy;
                        Report.Levied += levy;
                    }
                }
                if (p.Job == Job.Baskets)
                {
                    int made = p.Mood == Mood.Happy ? 2 : 1;
                    FreeBaskets += made;
                    Report.BasketsMade += made;
                }
                if (p.Job == Job.Houses && p.Mood != Mood.Broken)
                    HouseProgress += p.Mood == Mood.Happy ? 7 : 5;
            }
            TotalHarvest += Report.Harvested;
            while (HouseProgress >= 35)
            {
                HouseProgress -= 35;
                FreeHomes++;
                Report.HousesMade++;
            }
            var chief = People.FirstOrDefault(p => p.Role == Role.Chief && p.Job == Job.Administer);
            if (chief == null) return;
            int capacity = chief.Mood == Mood.Happy ? 2 : 1;
            if (Today.AssignBaskets)
            {
                foreach (var p in People.Where(p => p.Role == Role.Farmer && !p.Basket).OrderBy(p => p.Mood).ThenBy(p => p.Id).Take(capacity))
                {
                    if (FreeBaskets == 0) break;
                    p.Basket = true;
                    FreeBaskets--;
                }
            }
            if (Today.AssignHomes)
            {
                foreach (var p in People.Where(p => p.Role != Role.Chief && !p.House).OrderBy(p => p.Mood).ThenBy(p => p.Id).Take(capacity))
                {
                    if (FreeHomes == 0) break;
                    p.House = true;
                    FreeHomes--;
                }
            }
            if (!HousingUnlocked && People.Any(p => p.Role == Role.Farmer)
                && People.Where(p => p.Role == Role.Farmer).All(p => p.Basket))
            {
                HousingUnlocked = true;
                Note("Every farmer has a basket. Builders can now start making homes.");
            }
        }

        public void FinishDay()
        {
            if (!dayOpen) throw new InvalidOperationException("This day has already finished.");
            Produce();
            bool relief = Today.FeedHungry && People.Any(p => p.Role == Role.Chief && p.Job == Job.Administer);
            // Rotate equal-priority food access rather than always feeding the first person in the list.
            foreach (var p in People.OrderBy(p => (p.Id + Day) % People.Count))
            {
                bool ate = false;
                if (p.Apples > 0) { p.Apples--; ate = true; }
                else if (CommonApples > 0 && (Today.Food == FoodRule.SharedStore || relief))
                {
                    CommonApples--;
                    ate = true;
                    if (Today.Food == FoodRule.PersonalHarvest) Report.Distributed++;
                }
                if (ate)
                {
                    Report.Ate++;
                    p.Mood = p.Job == Job.Rest || p.House ? Mood.Happy : Mood.Tired;
                }
                else { Report.Hungry++; p.Mood = Mood.Broken; }
                p.RecordMood();
            }
            Note(Report.Summary);
            MaybeGrow();
            Day++;
            dayOpen = false;
        }

        void MaybeGrow()
        {
            if (Specialist || Report.Hungry > 0 || HappyRate < 0.5) return;
            if (People.Count < 5 && TotalHarvest >= GrowthTargets[People.Count])
            {
                People[0].Name = "Henry";
                var newcomer = new Georgie { Id = People.Count + 1, Name = Names[People.Count - 1], Role = Role.Gatherer };
                People.Add(newcomer);
                Note(newcomer.Name + " has joined the settlement.");
            }
            if (People.Count >= 5 && TotalHarvest >= 30)
            {
                Specialist = true;
                People[0].Role = Role.Chief;
                for (int i = 1; i < People.Count; i++) People[i].Role = i == People.Count - 1 ? Role.Builder : Role.Farmer;
                Note("Henry becomes chief. Farmers gather for everyone; builders begin weaving baskets.");
            }
        }

        public void Note(string text)
        {
            Ledger.Insert(0, text);
            if (Ledger.Count > 30) Ledger.RemoveAt(Ledger.Count - 1);
        }
    }
}
