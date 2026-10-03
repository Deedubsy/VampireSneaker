using NUnit.Framework;
using UnityEngine;
using Vespertine.AI;
using Vespertine.Data;
using Vespertine.Level;
using Vespertine.Progression;

namespace Vespertine.Tests
{
    /// <summary>M13 hunter squads: nerve, shocks, recovery, the wedge and the ring, and the map vocabulary.</summary>
    public class SquadTests
    {
        [Test]
        public void StartNerveByFactionAndTheCitysFear()
        {
            Assert.AreEqual(80f, SquadMath.StartNerve(Faction.Vigil, 0, 0));
            Assert.AreEqual(60f, SquadMath.StartNerve(Faction.Watch, 2, 4), "Rumour ahead of Terror doesn't help or hurt");
            Assert.AreEqual(50f, SquadMath.StartNerve(Faction.None, 0, 0));
            Assert.AreEqual(80f - 18f, SquadMath.StartNerve(Faction.Vigil, 5, 2));
            Assert.AreEqual(80f - 30f, SquadMath.StartNerve(Faction.Vigil, 12, 0), "capped at five steps");
        }

        [Test]
        public void FeedShockStacks()
        {
            Assert.AreEqual(0f, SquadMath.FedShock(false, true, true, true), "unseen costs nothing");
            Assert.AreEqual(20f, SquadMath.FedShock(true, false, false, false));
            Assert.AreEqual(35f, SquadMath.FedShock(true, true, false, false));
            Assert.AreEqual(40f, SquadMath.FedShock(true, false, true, true), "dread feast needs a drain");
            Assert.AreEqual(80f, SquadMath.FedShock(true, true, true, true), "a drained man in front of a Terror squad breaks it from full Vigil nerve");
            Assert.IsTrue(SquadMath.Breaks(80f - SquadMath.FedShock(true, true, true, true)));
        }

        [Test]
        public void NerveRecoversSlowlyAndOnlySoFar()
        {
            Assert.AreEqual(10f, SquadMath.Recover(10f, 80f, 5f, 1f), "not while the shock is fresh");
            Assert.AreEqual(11.5f, SquadMath.Recover(10f, 80f, 30f, 1f), 1e-4f);
            Assert.AreEqual(48f, SquadMath.Recover(47.9f, 80f, 30f, 1f), 1e-4f, "capped at 60% of the start");
            Assert.AreEqual(70f, SquadMath.Recover(70f, 80f, 30f, 1f), "never lowered");
        }

        [Test]
        public void WedgeSlotsSitBehindTheLeaderAndRingSurroundsTheCentre()
        {
            Assert.AreEqual(Vector2.zero, SquadMath.SlotOffset(0));
            Assert.Less(SquadMath.SlotOffset(1).x, 0f);
            Assert.Greater(SquadMath.SlotOffset(2).x, 0f);
            for (int i = 1; i < 8; i++) Assert.Less(SquadMath.SlotOffset(i).y, 0f, $"slot {i} behind");
            Assert.Less(SquadMath.SlotOffset(5).y, SquadMath.SlotOffset(3).y + 0.01f);
            Assert.IsTrue(SquadMath.IsRear(3, 4));
            Assert.IsFalse(SquadMath.IsRear(1, 2), "a pair has no rearguard");
            var c = new Vector3(5, 0, 5);
            for (int i = 0; i < 4; i++)
                Assert.AreEqual(SquadMath.RingRadius, Vector3.Distance(c, SquadMath.RingPoint(c, i, 4)), 1e-4f);
            Assert.Greater(Vector3.Distance(SquadMath.RingPoint(c, 0, 4), SquadMath.RingPoint(c, 2, 4)), 3f, "opposite sides");
        }

        [Test]
        public void VaneStiffensHisSquad()
        {
            Assert.GreaterOrEqual(SquadMath.StartNerve(Faction.Vigil, 5, 0) + SquadMath.VaneBonus, SquadMath.StartNerve(Faction.Vigil, 0, 0),
                "Vane at their head cancels the city's full Terror");
        }

        [Test]
        public void BurningTheArchiveWipesTheDossierOnce()
        {
            var c = CampaignState.NewGame(Difficulty.Hunter);
            c.AddHabit("rooftops", 9f);
            c.Countermeasures.Add("cm_rooftop");
            Assert.IsFalse(c.BurnArchive(), "not without the flag");
            c.SetFlag("archive_burned");
            Assert.IsTrue(c.BurnArchive());
            Assert.IsEmpty(c.Countermeasures);
            foreach (var d in c.Dossier) Assert.AreEqual(0f, d.Value);
            c.AddHabit("rooftops", 3f);
            Assert.IsFalse(c.BurnArchive(), "the Vigil only forgets once; what it learns afterwards it keeps");
            Assert.AreEqual(3f * Habits.Weight("rooftops"), c.Dossier.Find(d => d.Key == "rooftops").Value, 1e-4f);
        }

        const string Map = @"@mission
id = sq
title = Squads
@map
##########
#........#
#........#
##########
@entities
player 1 1
squad sq1 8 2 name=The_Third
npc a hunter 3 1 squad=sq1 lead
npc b hunter 4 1 squad=sq1
npc c watchman 5 1 squad=sq1
@objectives
primary rout ""Break the Third"" break sq1 how=rout
@script
on routed sq1: toast They ran.
on broken any: flag gone
on timer t: rout sq1
";

        [Test]
        public void SquadVocabularyValidates()
        {
            Assert.IsEmpty(MissionValidator.Validate(MapParser.Parse(Map)));
            Assert.IsNotEmpty(MissionValidator.Validate(MapParser.Parse(Map.Replace("squad=sq1 lead", "squad=sq9 lead"))), "unknown squad");
            Assert.IsNotEmpty(MissionValidator.Validate(MapParser.Parse(Map.Replace("break sq1", "break a"))), "an npc is not a squad");
            Assert.IsNotEmpty(MissionValidator.Validate(MapParser.Parse(Map.Replace("npc b hunter 4 1 squad=sq1", "npc b hunter 4 1 squad=sq1 lead"))), "two leaders");
            var lone = Map.Replace("npc b hunter 4 1 squad=sq1\n", "").Replace("npc c watchman 5 1 squad=sq1\n", "");
            Assert.IsNotEmpty(MissionValidator.Validate(MapParser.Parse(lone)), "a squad of one");
        }
    }
}
