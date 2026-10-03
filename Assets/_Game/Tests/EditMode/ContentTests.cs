using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Vespertine.Data;
using Vespertine.Level;
using Vespertine.Mission;

namespace Vespertine.Tests
{
    /// <summary>Data integrity: skill tree, archetypes and every shipped mission file.</summary>
    public class ContentTests
    {
        [Test]
        public void SkillTreeReferencesResolve()
        {
            Assert.IsNotEmpty(Skills.Nodes);
            var seen = new HashSet<string>();
            foreach (var n in Skills.Nodes)
            {
                Assert.IsTrue(seen.Add(n.Id), $"duplicate skill {n.Id}");
                if (n.Parent != null) Assert.IsNotNull(Skills.Get(n.Parent), $"{n.Id}: parent {n.Parent} missing");
                if (n.Cross != null) Assert.IsNotNull(Skills.Get(n.Cross), $"{n.Id}: cross {n.Cross} missing");
                if (n.IsAbility) Assert.IsNotNull(Skills.Ability(n.Id), $"{n.Id}: active node without an AbilityDef");
                if (n.Parent != null) Assert.LessOrEqual(Skills.Get(n.Parent).Tier, n.Tier, $"{n.Id}: parent is in a higher tier");
            }
        }

        [Test]
        public void ArchetypesAreSane()
        {
            int count = 0;
            foreach (var a in Archetypes.All)
            {
                count++;
                Assert.Greater(a.HP, 0, a.Id);
                Assert.LessOrEqual(a.Near, a.Far, a.Id);
                Assert.Greater(a.RunSpeed, a.WalkSpeed, a.Id);
            }
            Assert.GreaterOrEqual(count, 15);
            foreach (var id in new[] { "civilian", "orderly", "watchman", "priest", "hunter", "hound", "inquisitor", "vane" })
                Assert.IsTrue(Archetypes.Exists(id), id);
        }

        static IEnumerable<TestCaseData> MissionFiles()
        {
            foreach (var t in Resources.LoadAll<TextAsset>("Missions"))
                yield return new TestCaseData(t.name).SetName("Mission_" + t.name);
        }

        [TestCaseSource(nameof(MissionFiles))]
        public void MissionFileValidates(string name)
        {
            var t = Resources.Load<TextAsset>("Missions/" + name);
            Assert.IsNotNull(t);
            var d = MapParser.Parse(t.text);
            var problems = MissionValidator.Validate(d);
            Assert.IsEmpty(problems, name + ":\n" + string.Join("\n", problems));
            Assert.AreEqual(name, d.Id, "file name and header id differ");
            Assert.IsNotEmpty(d.Get("briefing", ""), name + ": campaign missions need a briefing (shown on the mission card)");
        }

        [Test]
        public void HiddenObjectivesArePrimaries()
        {
            const string map = @"@mission
id = mh
title = Hidden
@map
#####
#...#
#####
@entities
player 1 1
@objectives
primary a ""A"" reach 1 1 1 1
hidden b ""B"" reach 3 1 1 1 needs=a
optional c ""C"" nokill
@script
on complete a: reveal b
";
            var d = MapParser.Parse(map);
            Assert.IsEmpty(MissionValidator.Validate(d));
            Assert.IsNotEmpty(MissionValidator.Validate(MapParser.Parse(map.Replace("on complete a: reveal b", ""))), "an unrevealed hidden objective");
            Assert.IsFalse(d.Objectives[1].Optional, "a hidden objective must be finished to win");
            Assert.IsTrue(d.Objectives[1].Hidden);
            Assert.IsTrue(d.Objectives[2].Optional);
        }

        [Test]
        public void ValidatorCatchesBrokenReferences()
        {
            const string bad = @"@mission
id = mx
title = Bad
@map
#####
#...#
#####
@entities
player 1 1
npc a1 nosuchthing 2 1 route=r_missing
npc d1 hound 2 1 follow=nobody paired=a1
prop spaceship 3 1
@objectives
primary go ""Go"" interact ghost
@script
on start: bark nobody ""hi"" ; frobnicate
";
            var p = MissionValidator.Validate(MapParser.Parse(bad));
            string all = string.Join("\n", p);
            StringAssert.Contains("unknown archetype", all);
            StringAssert.Contains("unknown route", all);
            StringAssert.Contains("unknown prop type", all);
            StringAssert.Contains("unknown id 'ghost'", all);
            StringAssert.Contains("unknown action 'frobnicate'", all);
            StringAssert.Contains("bark references unknown id", all);
            StringAssert.Contains("follow= references unknown id 'nobody'", all);
            StringAssert.DoesNotContain("paired= references", all);
        }

        [Test]
        public void ValvesObjectiveCountsMainsAndChecksIds()
        {
            const string map = @"@mission
id = mv
title = Mains
@map
#####
#...#
#####
@entities
player 1 1
valve gm1 2 1 group=a ""Works main""
valve gm2 3 1 group=b
@objectives
primary go ""Reach"" reach 3 1 1 1
optional mains ""Shut the mains"" valves gm1 gm2
optional broken ""Shut a ghost"" valves gm1 gm9
";
            var d = MapParser.Parse(map);
            string all = string.Join("\n", MissionValidator.Validate(d));
            StringAssert.Contains("objective broken: references unknown id 'gm9'", all);
            StringAssert.DoesNotContain("objective mains", all);
            Assert.AreEqual(2, new Objective(d.Objectives[1]).PartsTotal);
        }

        [Test]
        public void CountOptionLimitsMultiPartObjectives()
        {
            const string map = @"@mission
id = mc
title = Counts
@map
#####
#...#
#####
@entities
player 1 1
@objectives
primary a ""Three of five"" feed_types common drunk fevered soldier priest count=3
optional b ""All listed"" feed_types common drunk
optional c ""Two bodies"" dispose canal count=2
optional d ""One body"" dispose any
optional e ""Clamped"" feed_types common drunk count=9
@script
on start: grant dominion.beckon
";
            var d = MapParser.Parse(map);
            Assert.IsEmpty(MissionValidator.Validate(d));
            Assert.AreEqual(3, new Objective(d.Objectives[0]).PartsTotal);
            Assert.AreEqual(2, new Objective(d.Objectives[1]).PartsTotal);
            Assert.AreEqual(2, new Objective(d.Objectives[2]).PartsTotal);
            Assert.AreEqual(1, new Objective(d.Objectives[3]).PartsTotal);
            Assert.AreEqual(2, new Objective(d.Objectives[4]).PartsTotal, "count cannot exceed the listed parts");
        }

        [Test]
        public void EscortObjectiveParsesPrisonersAndArea()
        {
            const string map = @"@mission
id = me
title = Escort
@map
##########
#........#
#........#
##########
@entities
player 1 1
npc clem scholar 2 1 prisoner
npc f1 fledgling 3 1 prisoner
npc f2 fledgling 4 1 prisoner
npc g1 watchman 5 1
light sun1 sunstone 6 1
light gl1 walllamp 7 1 group=ward
generator gen 8 2 group=ward
@objectives
primary a ""Lead Clement out"" escort clem 7 2 2 1
primary b ""Free two subjects"" escort f1 f2 7 2 2 1 count=2 loose=1
optional c ""Smash the lamp"" interact sun1.smash
optional d ""Out"" interact clem.out
@script
on interact f1.free: say Ilse ""Go.""
on interact f2.loose: terror 1
";
            var d = MapParser.Parse(map);
            Assert.IsEmpty(MissionValidator.Validate(d));
            var a = new Objective(d.Objectives[0]);
            var b = new Objective(d.Objectives[1]);
            CollectionAssert.AreEqual(new[] { "clem" }, a.EscortIds);
            CollectionAssert.AreEqual(new[] { "f1", "f2" }, b.EscortIds);
            Assert.AreEqual(1, a.PartsTotal);
            Assert.AreEqual(2, b.PartsTotal);
            Assert.IsTrue(b.TryRect(out var r));
            Assert.AreEqual(new UnityEngine.Rect(6.5f, 1.5f, 2f, 1f), r);
        }

        [Test]
        public void ValidatorRejectsEscortOfNonPrisoner()
        {
            const string map = @"@mission
id = me
title = Escort
@map
######
#....#
######
@entities
player 1 1
npc g1 watchman 2 1
generator gen 3 1 group=nowhere
@objectives
primary a ""Lead out"" escort g1 3 1
";
            string all = string.Join("\n", MissionValidator.Validate(MapParser.Parse(map)));
            StringAssert.Contains("not a prisoner", all);
            StringAssert.Contains("group=nowhere", all);
        }

        [Test]
        public void ValidatorCatchesUnknownGrant()
        {
            const string map = @"@mission
id = mg
title = Grant
@map
#####
#...#
#####
@entities
player 1 1
@objectives
primary a ""A"" reach 1 1 1 1
@script
on start: grant dominion.flight
";
            StringAssert.Contains("unknown skill", string.Join("\n", MissionValidator.Validate(MapParser.Parse(map))));
        }

        [Test]
        public void ScriptConditionsSealedItemsAndRoutes()
        {
            const string map = @"@mission
id = mc
title = Conditions
@map
#######
#.....#
#######
@entities
player 1 1
item box doc 3 1 ""Box"" ""Text"" sealed=Not_yet.
npc p priest 5 1
route r_a pingpong | 1 1 | 5 1 |
zone z 2 1 w=2 h=1
@objectives
primary a ""A"" item box
optional h ""Holy"" noholy
@script
on enter z if !a lit: say Ilse ""x""
on enter z: route p r_a ; unseal box ; seal box
on start if nothing: route p r_missing
";
            var d = MapParser.Parse(map);
            var rule = d.Script[0];
            Assert.AreEqual("z", rule.Arg);
            CollectionAssert.AreEqual(new[] { "!a", "lit" }, rule.Conditions);
            Assert.IsEmpty(d.Script[1].Conditions);
            Assert.AreEqual("Not_yet.", d.Entities.Find(e => e.Id == "box").Opt("sealed"));
            Assert.IsTrue(new Objective(d.Objectives[1]).IsConduct, "noholy is a conduct objective");

            Assert.IsTrue(MissionController.ConditionsHold(rule.Conditions, n => n == "lit"));
            Assert.IsFalse(MissionController.ConditionsHold(rule.Conditions, n => n == "lit" || n == "a"));
            Assert.IsFalse(MissionController.ConditionsHold(rule.Conditions, n => false));

            var problems = string.Join("\n", MissionValidator.Validate(d));
            StringAssert.Contains("condition 'lit'", problems);
            StringAssert.Contains("condition 'nothing'", problems);
            StringAssert.Contains("unknown route 'r_missing'", problems);
            StringAssert.DoesNotContain("condition '!a'", problems);
        }
    
        [Test]
        public void LockdownReinforcementsScaleWithDifficultyAndVigil()
        {
            var d = MapParser.Parse("@map\n...\n@entities\nspawn a 0 0 count=2\nspawn b 2 0 type=orderly count=1 vigil\n");
            var pts = d.Entities.FindAll(e => e.Kind == "spawn");
            var hunter = AI.AIDirector.ReinforcementSpecs(pts, false, Difficulty.Hunter);
            CollectionAssert.AreEqual(new[] { "rf_a_0", "rf_a_1", "rf_b_0" }, hunter.ConvertAll(e => e.Id));
            Assert.AreEqual("watchman", hunter[0].Type);
            Assert.AreEqual("orderly", hunter[2].Type);
            Assert.IsTrue(hunter.TrueForAll(e => e.Kind == "npc" && e.Has("wary")));
            // Merciful never drops a point to zero; Apex adds one and a sergeant leads three or more watchmen
            Assert.AreEqual(2, AI.AIDirector.ReinforcementSpecs(pts, false, Difficulty.Merciful).Count);
            var apex = AI.AIDirector.ReinforcementSpecs(pts, false, Difficulty.Apex);
            Assert.AreEqual(5, apex.Count);
            Assert.AreEqual("sergeant", apex[0].Type);
            // once the Vigil hunts her it answers at the point flagged vigil
            var act2 = AI.AIDirector.ReinforcementSpecs(pts, true, Difficulty.Hunter);
            CollectionAssert.AreEqual(new[] { "rf_a_0", "rf_a_1", "rf_b_0", "rf_b_v0", "rf_b_v1" }, act2.ConvertAll(e => e.Id));
            Assert.AreEqual("hound", act2[4].Type);
            Assert.AreEqual(2f, act2[4].X);
            Assert.AreEqual(0, AI.AIDirector.ReinforcementSpecs(new List<EntitySpec>(), true, Difficulty.Apex).Count);
        }

        [Test]
        public void ValidatorChecksDeliverAndFeedTargets()
        {
            const string map = @"@mission
id = md
title = Deliver
@map
#######
#.....#
#######
@entities
player 1 1
npc pen notable 3 1
prop crate crate 4 1
@objectives
primary a ""Carry her"" deliver pen 5 1 1 1
optional b ""Carry a crate"" deliver crate 5 1
optional c ""Carry nobody"" deliver ghost 5 1
optional d ""Feed on her"" feed pen
optional e ""Feed on a priest"" feed priest
optional f ""Feed on a ghost"" feed ghost
optional g ""Nowhere"" reach 5 one
optional h ""No area"" deliver pen
@script
on feed pen: say Ilse ""Mine.""
on feed watchman: say Ilse ""Silver.""
on feed nobody: say Ilse ""?""
";
            var all = string.Join("\n", MissionValidator.Validate(MapParser.Parse(map)));
            StringAssert.DoesNotContain("objective a:", all);
            StringAssert.Contains("objective b: deliver needs an npc id", all);
            StringAssert.Contains("objective c: deliver needs an npc id", all);
            StringAssert.DoesNotContain("objective d:", all);
            StringAssert.DoesNotContain("objective e:", all);
            StringAssert.Contains("objective f: feed references 'ghost'", all);
            StringAssert.Contains("objective g: reach area value 'one' is not a number", all);
            StringAssert.Contains("objective h: deliver needs an area", all);
            StringAssert.Contains("feed references 'nobody'", all);
            StringAssert.DoesNotContain("'watchman'", all);
        }

        [Test]
        public void ObjectiveIfAcceptsOnlyWorldFlags()
        {
            const string map = @"@mission
id = mw
title = World
@map
#####
#...#
#####
@entities
player 1 1
@objectives
primary a ""A"" reach 1 1 1 1
optional b ""Rumour"" reach 3 1 1 1 if=rumour
optional c ""Not caged"" nokill if=!cm_caged,cf_tobias_met
optional d ""Bad"" nokill if=a
";
            var d = MapParser.Parse(map);
            Assert.AreEqual("rumour", d.Objectives[1].Opts["if"]);
            var all = string.Join("\n", MissionValidator.Validate(d));
            StringAssert.Contains("objective d: if='a' is not a world flag", all);
            StringAssert.DoesNotContain("objective b", all);
            StringAssert.DoesNotContain("objective c", all);
            Assert.IsTrue(MissionValidator.IsWorldFlag("cm_salt"));
            Assert.IsFalse(MissionValidator.IsWorldFlag("vigil_manual"));
            var cond = new List<string>(d.Objectives[2].Opts["if"].Split(','));
            Assert.IsTrue(MissionController.ConditionsHold(cond, n => n == "cf_tobias_met"));
            Assert.IsFalse(MissionController.ConditionsHold(cond, n => n == "cf_tobias_met" || n == "cm_caged"));
        }

        [Test]
        public void ValidatorChecksGasworksSystems()
        {
            const string map = @"@mission
id = mgw
title = Gasworks
@map
##########
#........#
#........#
##########
@entities
player 1 1
npc wk1 worker 2 1 evac=8,1
npc wk2 worker 3 1
npc op1 watchman 4 2 sentry post
light sl1 searchlight 5 1 from=4,2,1.6 sweep=6,1,2,2 op=op1
light sl2 searchlight 5 2 sweep=6 op=ghost
prop gasholder 7 2
@objectives
primary a ""Out"" evacuate wk1 wk2
optional b ""Some out"" evacuate wk1 count=1
@script
on start: countdown t 30 ""Fuse""
on timer t: blast 7 2 10 ; swapprop 7 2 gasholder_wreck ; swapprop 1 1 gasholder_wreck ; swapprop 7 2 nonsense
on interact wk1.evac: say Ilse ""One.""
";
            var d = MapParser.Parse(map);
            var all = string.Join("\n", MissionValidator.Validate(d));
            StringAssert.Contains("evacuate 'wk2' is not an npc with evac=x,y", all);
            StringAssert.DoesNotContain("evacuate 'wk1'", all);
            StringAssert.Contains("op= references unknown npc 'ghost'", all);
            StringAssert.Contains("searchlight needs sweep=", all);
            StringAssert.Contains("searchlight needs from=", all);
            StringAssert.DoesNotContain("(light sl1)", all);
            StringAssert.Contains("swapprop: no prop at 1,1", all);
            StringAssert.Contains("swapprop needs x y <prop type>", all);
            StringAssert.DoesNotContain("unknown action", all);
            StringAssert.DoesNotContain("wk1.evac", all);
            Assert.AreEqual(2, new Objective(d.Objectives[0]).PartsTotal);
            Assert.AreEqual(1, new Objective(d.Objectives[1]).PartsTotal);
        }
    }
}
