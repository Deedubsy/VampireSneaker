using NUnit.Framework;
using Vespertine.Data;
using Vespertine.Mission;
using Vespertine.Progression;

namespace Vespertine.Tests
{
    public class ChallengeTests
    {
        static MissionResult Run(bool won = true, int kills = 0, int spotted = 0, int alarms = 0, int loads = 0, float time = 300f, int missed = 0)
        {
            var r = new MissionResult { Won = won, Kills = kills, TimesSpotted = spotted, NeverSpotted = spotted == 0, Alarms = alarms, Loads = loads, Time = time };
            for (int i = 0; i < missed; i++) r.OptionalsMissed.Add("x");
            return r;
        }

        [Test]
        public void CleanFastNightEarnsEverythingButApex()
        {
            var e = Challenges.Earned(Run(), 600f, Difficulty.Hunter);
            CollectionAssert.AreEquivalent(new[] { Challenges.Unseen, Challenges.Merciful, Challenges.Silent, Challenges.Swift, Challenges.Unbroken, Challenges.Thorough }, e);
            Assert.Contains(Challenges.ApexId, Challenges.Earned(Run(), 600f, Difficulty.Apex));
        }

        [Test]
        public void EachChallengeHasItsOwnCondition()
        {
            Assert.IsFalse(Challenges.Earned(Run(kills: 1), 600f, Difficulty.Hunter).Contains(Challenges.Merciful));
            Assert.IsFalse(Challenges.Earned(Run(spotted: 2), 600f, Difficulty.Hunter).Contains(Challenges.Unseen));
            Assert.IsFalse(Challenges.Earned(Run(alarms: 1), 600f, Difficulty.Hunter).Contains(Challenges.Silent));
            Assert.IsFalse(Challenges.Earned(Run(loads: 1), 600f, Difficulty.Hunter).Contains(Challenges.Unbroken));
            Assert.IsFalse(Challenges.Earned(Run(alarms: 1), 600f, Difficulty.Hunter).Contains(Challenges.Unbroken));   // an alarm breaks the night
            Assert.IsFalse(Challenges.Earned(Run(time: 601f), 600f, Difficulty.Hunter).Contains(Challenges.Swift));
            Assert.IsTrue(Challenges.Earned(Run(time: 600f), 600f, Difficulty.Hunter).Contains(Challenges.Swift));   // par is inclusive
            Assert.IsFalse(Challenges.Earned(Run(), 0f, Difficulty.Hunter).Contains(Challenges.Swift));               // no par, no Swift
            Assert.IsFalse(Challenges.Earned(Run(missed: 1), 600f, Difficulty.Hunter).Contains(Challenges.Thorough));
        }

        [Test]
        public void LostNightEarnsNothing()
        {
            Assert.IsEmpty(Challenges.Earned(Run(won: false), 600f, Difficulty.Apex));
            Assert.IsEmpty(Challenges.Earned(null, 600f, Difficulty.Apex));
        }

        [Test]
        public void EveryMissionHasAPar()
        {
            foreach (var m in Missions.All) Assert.Greater(m.Par, 0f, m.Id);
            Assert.AreEqual("6:00", Challenges.FormatPar(360f));
        }
    }
}
