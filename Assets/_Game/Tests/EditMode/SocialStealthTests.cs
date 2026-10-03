using NUnit.Framework;
using UnityEngine;
using Vespertine.Level;
using Vespertine.Mission;

namespace Vespertine.Tests
{
    /// <summary>Act II social stealth: arranged accidents, feeding conduct, homes with several thresholds.</summary>
    public class SocialStealthTests
    {
        [Test]
        public void TrapTakesOnlyListedVictimsWithinReach()
        {
            var at = new Vector3(10, 3, 10);
            var victims = new[] { "ashcombe" };
            Assert.IsTrue(Trap.Takes(victims, "ashcombe", "notable", at, 1.2f, at + new Vector3(0.8f, 0, 0.5f)));
            Assert.IsFalse(Trap.Takes(victims, "vane", "vane", at, 1.2f, at), "not on the list");
            Assert.IsFalse(Trap.Takes(victims, "ashcombe", "notable", at, 1.2f, at + new Vector3(2f, 0, 0)), "out of reach");
            Assert.IsFalse(Trap.Takes(victims, "ashcombe", "notable", at, 1.2f, at + new Vector3(0, -3f, 0)), "on the floor below");
            Assert.IsTrue(Trap.Takes(null, "anyone", "guest", at, 1.2f, at), "no list: anyone");
            Assert.IsTrue(Trap.Takes(new[] { "guest" }, "g7", "guest", at, 1.2f, at), "an archetype may be listed");
        }

        const string Map = @"@mission
id = ms
title = Social
@map
#########
#...+...#
#...#...#
#########
@entities
player 1 1
door d_a 4 1 threshold
door d_b 4 2 threshold
zone house 5 1 w=3 h=2 home=d_a,d_b
zone bad 5 1 w=1 h=1 home=d_a,d_nowhere
npc lord notable 6 1
trap rail 6 2 radius=1 victims=lord drop=6,1 ""Loose rail"" verb=Loosen
@objectives
primary a ""Kill the lord, cleanly"" kill lord accident
optional b ""Drink only wine"" feedonly drunk
optional c ""Nonsense"" feedonly claret
optional d ""Nothing"" feedonly
";

        [Test]
        public void ValidatorUnderstandsHomesAccidentsAndBloodTypes()
        {
            var d = MapParser.Parse(Map);
            var all = string.Join("\n", MissionValidator.Validate(d));
            StringAssert.Contains("home= references unknown id 'd_nowhere'", all);
            StringAssert.DoesNotContain("'d_b'", all);
            StringAssert.DoesNotContain("objective a", all, "accident is a kill option, not an id");
            StringAssert.DoesNotContain("objective b", all);
            StringAssert.Contains("objective c: unknown blood type 'claret'", all);
            StringAssert.Contains("objective d: feedonly needs at least one blood type", all);
            StringAssert.DoesNotContain("unknown entity kind", all);
            Assert.IsTrue(new Objective(d.Objectives[1]).IsConduct, "feedonly is judged at the end");
            Assert.IsFalse(new Objective(d.Objectives[0]).IsConduct);
        }
    }
}
