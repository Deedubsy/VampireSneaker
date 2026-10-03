using NUnit.Framework;
using Vespertine.Data;
using Vespertine.Progression;

namespace Vespertine.Tests
{
    public class CampaignStateTests
    {
        [Test]
        public void AwakeningFollowsVitaeThresholds()
        {
            Assert.AreEqual(1, CampaignState.AwakeningFor(0));
            Assert.AreEqual(1, CampaignState.AwakeningFor(119));
            Assert.AreEqual(2, CampaignState.AwakeningFor(120));
            Assert.AreEqual(9, CampaignState.AwakeningFor(3799));
            Assert.AreEqual(10, CampaignState.AwakeningFor(3800));
            Assert.AreEqual(10, CampaignState.AwakeningFor(99999));
        }

        [Test]
        public void AddVitaeReportsLevelsGained()
        {
            var c = CampaignState.NewGame(Difficulty.Hunter);
            Assert.AreEqual(0, c.AddVitae(100));
            Assert.AreEqual(2, c.AddVitae(300)); // 400 -> Awakening 3
            Assert.AreEqual(3, c.Awakening);
            Assert.AreEqual(700, c.NextThreshold);
            c.AddVitae(-10000);
            Assert.AreEqual(0, c.Vitae, "never negative");
        }

        [Test]
        public void StatsAndSlotsScaleWithAwakening()
        {
            Assert.AreEqual(40f, CampaignState.MaxHpFor(1), 1e-4f);
            Assert.AreEqual(120f, CampaignState.MaxHpFor(10), 1e-4f);
            Assert.AreEqual(60f, CampaignState.MaxBloodFor(1), 1e-4f);
            Assert.AreEqual(1, CampaignState.SlotsFor(1));
            Assert.AreEqual(2, CampaignState.SlotsFor(2));
            Assert.AreEqual(3, CampaignState.SlotsFor(4));
            Assert.AreEqual(6, CampaignState.SlotsFor(10));
        }

        [Test]
        public void UnlockRequiresMarksParentAndAwakening()
        {
            var c = CampaignState.NewGame(Difficulty.Hunter);
            Assert.IsFalse(c.Unlock("predator.stalker"), "no marks");
            c.Marks = 5;
            Assert.IsTrue(c.Unlock("predator.stalker"));
            Assert.AreEqual(4, c.Marks);
            Assert.IsFalse(c.Unlock("predator.stalker"), "already owned");
            Assert.IsFalse(c.CanUnlock("predator.gorge", out var reason), "tier 2 needs Awakening 3");
            StringAssert.Contains("Awakening", reason);
            c.AddVitae(CampaignState.VitaeThresholds[2]);
            Assert.IsTrue(c.Unlock("predator.gorge"));
            Assert.IsFalse(c.CanUnlock("predator.pounce_silent", out reason), "parent missing");
            StringAssert.Contains("Requires", reason);
        }

        [Test]
        public void UnlockingAnAbilityEquipsItWhenASlotIsFree()
        {
            var c = CampaignState.NewGame(Difficulty.Hunter);
            c.Marks = 4;
            Assert.IsTrue(c.Unlock("predator.pounce"));
            CollectionAssert.Contains(c.Loadout, "predator.pounce");
        }

        [Test]
        public void RespecRefundsEverythingExceptStoryGrants()
        {
            var c = CampaignState.NewGame(Difficulty.Hunter);
            c.AddVitae(CampaignState.VitaeThresholds[2]);
            c.Marks = 6;
            c.Unlock("predator.stalker");
            c.Unlock("predator.gorge");
            c.Unlock("predator.pounce");
            foreach (var n in Skills.Nodes) if (n.Tier == 0) { c.Grant(n.Id); break; }
            int granted = c.Nodes.Count - 3;
            c.Respec();
            Assert.AreEqual(6, c.Marks);
            Assert.AreEqual(granted, c.Nodes.Count);
            foreach (var id in c.Loadout) Assert.IsTrue(c.Has(id), "loadout only holds owned abilities");
        }

        [Test]
        public void HigherTierStoryGiftsAreFreeAndSurviveRespec()
        {
            var c = CampaignState.NewGame(Difficulty.Hunter);
            c.Marks = 3;
            c.Grant("dominion.thrall");             // the Abbess's gift in M06 (a tier-2 node, outside the tree rules)
            Assert.AreEqual(0, c.SpentMarks(), "a gift costs nothing");
            CollectionAssert.Contains(c.Loadout, "dominion.thrall");
            Assert.IsTrue(c.CanUnlock("dominion.false_orders", out _) || c.Awakening < Skills.Get("dominion.false_orders").RequiredAwakening,
                "a gift opens its branch");
            c.Respec();
            Assert.AreEqual(3, c.Marks, "respec refunds nothing for a gift");
            Assert.IsTrue(c.Has("dominion.thrall"));
            // a node already bought is not turned into a gift by a later grant
            var d = CampaignState.NewGame(Difficulty.Hunter);
            d.AddVitae(300); d.Marks = 5;
            d.Unlock("dominion.mesmerize");
            d.Grant("dominion.mesmerize");
            CollectionAssert.DoesNotContain(d.Gifts, "dominion.mesmerize");
        }

        [Test]
        public void ClampLoadoutRespectsSlots()
        {
            var c = CampaignState.NewGame(Difficulty.Hunter);
            c.Loadout.Add("ghost");
            c.ClampLoadout();
            Assert.IsEmpty(c.Loadout);
        }

        [Test]
        public void AnOldSavesShadowstepBecomesUmbralStep()
        {
            var c = CampaignState.NewGame(Difficulty.Hunter);
            c.Nodes.Add("shade.shadowstep"); c.Nodes.Add("shade.shadowstep_bars");
            c.Loadout.Add("shade.shadowstep");
            c.ClampLoadout();
            CollectionAssert.Contains(c.Nodes, "shade.umbral");
            CollectionAssert.Contains(c.Nodes, "shade.dash_bars");
            CollectionAssert.DoesNotContain(c.Nodes, "shade.shadowstep");
            CollectionAssert.DoesNotContain(c.Loadout, "shade.shadowstep", "a passive takes no slot");
            Assert.IsNotNull(Skills.Get("shade.umbral"));
            Assert.AreEqual("shade.umbral", Skills.Get("shade.gloom").Parent);
        }

        [Test]
        public void DossierAnswersFromM03TwoAtMostOnePerArt()
        {
            var c = CampaignState.NewGame(Difficulty.Hunter);
            c.AddHabit(Habits.Snuff, 10);   // 10, Shade
            c.AddHabit(Habits.Lethal, 10);  // 15, Predator
            c.AddHabit(Habits.Mist, 12);    // 12, Shade too
            Assert.IsEmpty(c.UpdateCountermeasures(0), "not after M01");
            c.AddHabit(Habits.Lethal, 1); c.AddHabit(Habits.Snuff, 1); c.AddHabit(Habits.Mist, 1);
            var added = c.UpdateCountermeasures(CampaignState.DossierStartsAt - 1);
            CollectionAssert.AreEqual(new[] { "cm_paired", "cm_censer" }, added, "heaviest first; snuffing is Shade, already answered by the censers");
            Assert.AreEqual(8.25f, c.Habit(Habits.Lethal), 1e-4f, "adapted habits are halved");
            Assert.AreEqual("You left 11 dead", c.AnswerSource("cm_paired"), "the briefing names the deed");
            c.AddHabit(Habits.Dominion, 10);
            c.AddHabit(Habits.Lethal, 1); c.AddHabit(Habits.Mist, 1);
            Assert.IsEmpty(c.UpdateCountermeasures(CampaignState.DossierStartsAt), "two in force: no third");
            Assert.IsNull(c.NextCountermeasure());
        }

        [Test]
        public void AnAnswerLapsesAfterTwoNightsWithoutItsHabit()
        {
            var c = CampaignState.NewGame(Difficulty.Hunter);
            c.AddHabit(Habits.Mist, 6);
            CollectionAssert.AreEqual(new[] { "cm_censer" }, c.UpdateCountermeasures(3));
            c.UpdateCountermeasures(4);
            CollectionAssert.Contains(c.Countermeasures, "cm_censer", "one quiet night: still in force");
            c.AddHabit(Habits.Mist, 1);
            c.UpdateCountermeasures(5);
            c.UpdateCountermeasures(6);
            CollectionAssert.Contains(c.Countermeasures, "cm_censer", "the habit came back: the clock restarted");
            c.UpdateCountermeasures(7);
            CollectionAssert.IsEmpty(c.Countermeasures, "two quiet nights: lapsed");
            Assert.IsEmpty(c.Answers);
        }

        [Test]
        public void AnOldSaveOverTheCapIsTrimmed()
        {
            var c = CampaignState.NewGame(Difficulty.Hunter);
            c.Countermeasures.AddRange(new[] { "cm_caged", "cm_paired", "cm_ward", "cm_salt" });
            c.AddHabit(Habits.Dominion, 1); c.AddHabit(Habits.Blood, 1);
            c.UpdateCountermeasures(8);
            CollectionAssert.AreEqual(new[] { "cm_ward", "cm_salt" }, c.Countermeasures, "the newest two stay, and their habits were repeated");
            Assert.AreEqual("You bent a mind", Habits.Source(Habits.Dominion, 1));
        }

        [Test]
        public void VanesFullDossierCountersEveryRealHabit()
        {
            var c = CampaignState.NewGame(Difficulty.Hunter);
            c.AddHabit(Habits.Snuff, 6);
            c.AddHabit(Habits.Lethal, 2);       // 3
            c.AddHabit(Habits.BodiesFound, 2);  // 4: the same counter as lethal
            c.AddHabit(Habits.Mist, 3);
            c.AddHabit(Habits.Dominion, 1);     // 1.5: a single use is not a habit
            c.AddHabit(Habits.Rooftops, 4);     // 1
            var all = c.FullDossier();
            CollectionAssert.AreEqual(new[] { "cm_caged", "cm_paired", "cm_censer" }, all, "heaviest first, no duplicates");
            CollectionAssert.IsEmpty(c.Countermeasures, "the full Dossier is for one night; the campaign keeps its own list");
            CollectionAssert.IsEmpty(CampaignState.NewGame(Difficulty.Hunter).FullDossier());
        }

        [Test]
        public void LightHabitsBelowThresholdDoNothing()
        {
            var c = CampaignState.NewGame(Difficulty.Hunter);
            c.AddHabit(Habits.Rooftops, 8); // 0.25 each -> 2
            Assert.IsEmpty(c.UpdateCountermeasures(10));
        }

        [Test]
        public void FlagsAndLore()
        {
            var c = CampaignState.NewGame(Difficulty.Hunter);
            c.SetFlag("a");
            c.SetFlag("a");
            Assert.AreEqual(1, c.Flags.Count);
            c.SetFlag("a", false);
            Assert.IsFalse(c.Flag("a"));
            Assert.IsTrue(c.DiscoverLore("l1", "T", "B"));
            Assert.IsFalse(c.DiscoverLore("l1", "T", "B"));
        }

        [Test]
        public void EndingsFollowChoiceAndReputation()
        {
            var c = CampaignState.NewGame(Difficulty.Hunter);
            c.Terror = 10; c.Rumour = 2;
            Assert.AreEqual("endless_night", c.Ending("free"));
            Assert.AreEqual("new_abbess", c.Ending("consume"));
            Assert.AreEqual("ashes", c.Ending("dawn"));
            c.Terror = 1; c.Rumour = 9;
            Assert.AreEqual("night_court", c.Ending("free"));
            Assert.AreEqual("pale_lady", c.Ending("consume"));
            Assert.AreEqual("dawn_tobias", c.Ending("dawn"));
            c.Tobias = "lost";
            Assert.AreEqual("dawn", c.Ending("dawn"));
        }

        [Test]
        public void MissionRecordIsCreatedOnce()
        {
            var c = CampaignState.NewGame(Difficulty.Hunter);
            var r = c.Record("m01");
            r.Completed = true;
            Assert.AreSame(r, c.Record("m01"));
            Assert.AreEqual(1, c.Records.Count);
        }
    }
}
