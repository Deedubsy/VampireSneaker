using NUnit.Framework;
using Vespertine.AI;
using Vespertine.Level;
using Vespertine.Mission;

namespace Vespertine.Tests
{
    public class HuntTests
    {
        [Test]
        public void ScentFixTightensWithBloodAndRepetition()
        {
            float far = HuntMath.Radius(60f, false, false, 0);
            Assert.AreEqual(HuntMath.MaxRadius, far, 1e-4f, "a far fix is vague");
            Assert.Less(HuntMath.Radius(60f, true, false, 0), far * 0.5f, "fresh blood gives her away");
            Assert.Greater(HuntMath.Radius(20f, false, true, 0), HuntMath.Radius(20f, false, false, 0), "rain drowns the scent");
            float prev = float.MaxValue;
            for (int i = 0; i < 8; i++)
            {
                float r = HuntMath.Radius(30f, false, false, i);
                Assert.LessOrEqual(r, prev, "each fix in a row lands closer");
                prev = r;
            }
            Assert.GreaterOrEqual(HuntMath.Radius(0f, true, false, 20), HuntMath.MinRadius, "never a perfect fix");
        }

        [Test]
        public void ValidatorKnowsTheHuntVocabulary()
        {
            const string map = @"@mission
id = mh2
title = Hunt
@map
#####
#...#
#####
@entities
player 1 1
npc hollin tracker 3 1 hunts=12
npc dog hound 2 1 follow=hollin
@objectives
primary take ""Kill or enthrall Hollin"" take hollin
optional hp ""Keep half your vitality"" hpfloor 50
optional bad ""Take a ghost"" take ghost
@script
on start: weather rain ; hunt hollin on
on thrall hollin: campaign_flag hollin_thrall
every timer 60: weather clear ; hunt ghost off
";
            var d = MapParser.Parse(map);
            var all = string.Join("\n", MissionValidator.Validate(d));
            StringAssert.Contains("objective bad: references unknown id 'ghost'", all);
            StringAssert.Contains("hunt references unknown id 'ghost'", all);
            StringAssert.DoesNotContain("objective take", all);
            StringAssert.DoesNotContain("objective hp", all);
            StringAssert.DoesNotContain("unknown action", all);
            StringAssert.DoesNotContain("unknown event", all);
            Assert.IsTrue(new Objective(d.Objectives[1]).IsConduct, "hpfloor is judged as conduct");
            Assert.IsFalse(new Objective(d.Objectives[0]).IsConduct);
        }
    }
}
