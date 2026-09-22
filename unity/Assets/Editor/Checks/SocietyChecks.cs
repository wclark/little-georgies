using System;
using System.Linq;
using LittleGeorgies;

public static class SocietyChecks
{
    static int checks;
    public static int Validate()
    {
        checks = 0;
        var s = Society.Create();
        Require(s.People.Count == 1 && s.People[0].Role == Role.Gatherer && s.People[0].Mood == Mood.Happy, "Default start is one happy gatherer");
        Require(!s.Specialist && !s.HousingUnlocked && s.AllApples == 0 && s.TotalBaskets == 0 && s.TotalHomes == 0, "Default start has no village infrastructure");
        s.FinishDay();
        Require(s.People[0].Mood == Mood.Tired && s.CommonApples == 1, "Harvest, then dinner");
        s.BeginDay();
        Require(s.People[0].Job == Job.Rest, "Autonomous rest with food available");
        s.FinishDay();
        Require(s.People[0].Mood == Mood.Happy, "Food plus rest recovers");
        for (int i = 0; i < 26; i++) { s.BeginDay(); s.FinishDay(); }
        Require(s.Specialist && s.People.Count >= 5, "Natural progression reaches specialties");
        Require(s.People.All(p => p.HappyHistory.Count <= 10), "Personal happiness is bounded");
        Require(s.People.All(p => p.Apples >= 0) && s.CommonApples >= 0, "Food balances never negative");

        var broken = Society.Create(true);
        broken.FinishDay();
        foreach (var p in broken.People) { p.Mood = Mood.Broken; p.House = false; }
        broken.CommonApples = 0;
        broken.Policy.BuildHomes = true;
        broken.BeginDay();
        Require(broken.People.All(p => p.Job != Job.Rest && p.Job != Job.Houses), "Broken Georgies work; broken builders make baskets");
        Require(broken.Day == 2, "No game over on deprivation");

        var personal = Society.Create(true);
        personal.FinishDay();
        personal.Policy.Food = FoodRule.PersonalHarvest;
        personal.Policy.LevyPercent = 50;
        personal.Policy.RestWhenFed = false;
        personal.Policy.FeedHungry = false;
        personal.CommonApples = 0;
        personal.BeginDay();
        int before = personal.AllApples;
        personal.Produce();
        int harvested = personal.Report.Harvested;
        Require(personal.AllApples == before + harvested, "Levy conserves apples");
        personal.Produce();
        Require(personal.AllApples == before + harvested, "Delivery cannot produce twice");
        personal.FinishDay();
        Require(personal.Report.Hungry > 0 && personal.AllApples > 0, "Private harvest can leave non-farmers hungry despite surplus");
        Require(personal.AllApples == before + harvested - personal.Report.Ate, "Dinner conserves apples");
        personal.Policy.FeedHungry = true;
        personal.BeginDay(); personal.FinishDay();
        Require(personal.Report.Distributed > 0, "Relief actually reaches hungry people");

        var house = Society.Create();
        house.People[0].House = true;
        house.FinishDay();
        Require(house.People[0].Mood == Mood.Happy, "A fed housed worker wakes rested");
        Require(new Georgie { Role = Role.Farmer, Mood = Mood.Broken, Basket = true }.Yield == 1, "Broken farmer cannot use basket bonus");
        var policy = Society.Create();
        policy.Policy.RestWhenFed = false;
        Require(policy.Today.RestWhenFed, "Policy changes wait for the next dawn");

        var recovering = new Georgie();
        for (int i = 0; i < 30; i++) { recovering.Mood = Mood.Broken; recovering.RecordMood(); }
        for (int i = 0; i < 10; i++) { recovering.Mood = Mood.Happy; recovering.RecordMood(); }
        Require(recovering.HappyRate == 1 && recovering.HappyHistory.Count == 10, "Old unhappiness ages out completely");

        var progression = Society.Create();
        while (!progression.Specialist && progression.Day < 60)
        {
            Require(progression.TotalBaskets == 0 && progression.TotalHomes == 0, "Gathering band has no advanced goods");
            int applesBefore = progression.AllApples;
            progression.FinishDay();
            Require(progression.AllApples == applesBefore + progression.Report.Harvested - progression.Report.Ate, "Growth does not grant free apples");
            if (!progression.Specialist) progression.BeginDay();
        }
        Require(progression.Specialist && progression.People[0].Name == "Henry", "Band naturally develops specialties");
        Require(progression.TotalBaskets == 0 && progression.TotalHomes == 0 && !progression.HousingUnlocked, "Specialties do not grant goods or unlock homes immediately");
        progression.Policy.BuildHomes = true;
        progression.BeginDay();
        Require(progression.People.Single(p => p.Role == Role.Builder).Job == Job.Baskets, "Builders learn baskets before houses");
        progression.Policy.BuildHomes = false;
        for (int i = 0; i < 30 && !progression.HousingUnlocked; i++)
        {
            progression.FinishDay();
            Require(progression.TotalHomes == 0, "No house appears before construction");
            if (!progression.HousingUnlocked) progression.BeginDay();
        }
        Require(progression.HousingUnlocked && progression.People.Where(p => p.Role == Role.Farmer).All(p => p.Basket), "Equipping farmers unlocks housing");
        progression.Policy.RestWhenFed = false;
        progression.Policy.BuildHomes = true;
        for (int i = 0; i < 8 && progression.TotalHomes == 0; i++) { progression.BeginDay(); progression.FinishDay(); }
        Require(progression.TotalHomes > 0, "An actual builder completes the first house");
        var hungryHouse = Society.Create(true);
        hungryHouse.FinishDay();
        hungryHouse.Policy.Food = FoodRule.PersonalHarvest;
        hungryHouse.Policy.FeedHungry = false;
        hungryHouse.People[0].House = true;
        hungryHouse.BeginDay(); hungryHouse.FinishDay();
        Require(hungryHouse.People[0].Mood == Mood.Broken, "Housing does not replace food");

        foreach (FoodRule food in Enum.GetValues(typeof(FoodRule)))
        {
            var longRun = Society.Create(true);
            for (int day = 0; day < 120; day++)
            {
                longRun.FinishDay();
                Require(longRun.AllApples >= 0 && longRun.People.All(p => p.Apples >= 0), "Long-run food balances");
                Require(longRun.People.All(p => p.HappyHistory.Count <= 10), "Long-run history bounds");
                longRun.Policy.Food = food;
                longRun.Policy.BuildHomes = day % 12 < 8;
                longRun.Policy.FeedHungry = day % 7 != 0;
                longRun.BeginDay();
            }
            Require(longRun.Day == 121, "Simulation has no fixed end date");
        }
        return checks;
    }

    static void Require(bool value, string explanation) { checks++; if (!value) throw new Exception("Economy validation: " + explanation); }

}

