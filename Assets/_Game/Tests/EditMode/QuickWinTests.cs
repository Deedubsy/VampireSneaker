using NUnit.Framework;
using UnityEngine;
using Vespertine.AI;
using Vespertine.Data;
using Vespertine.Level;
using Vespertine.Progression;
using Vespertine.Stealth;

namespace Vespertine.Tests
{
    /// <summary>Gameplay Redesign quick wins (D124–D128): shout range in metres, sneak that out-paces a walk,
    /// the Blood Arts after M01 with Beckon in M02, and challenges that pay.</summary>
    public class QuickWinTests
    {
        [Test]
        public void ShoutReachesTheDifficultyRadiusInMetres()
        {
            Assert.AreEqual(AIDirector.BaseShoutRadius, Difficulties.Get(Difficulty.Hunter).ShoutRadius, "Hunter is the reference radius");
            foreach (Difficulty d in System.Enum.GetValues(typeof(Difficulty)))
            {
                float r = Difficulties.Get(d).ShoutRadius;
                Assert.AreEqual(r, AIDirector.ShoutRange(r), $"{d}: a shout carries its radius, not radius squared");
                Assert.Less(AIDirector.ShoutRange(r), 30f, $"{d}: a shout is local, never map-wide");
            }
        }

        [Test]
        public void OnlyVoicesScaleWithDifficulty()
        {
            Assert.AreEqual(18f, AIDirector.NoiseRange(18f, NoiseKind.Scream, 15f), 1e-4f, "Hunter leaves a scream as authored");
            Assert.AreEqual(12f, AIDirector.NoiseRange(18f, NoiseKind.Scream, 10f), 1e-4f, "Merciful shrinks it by 10/15");
            Assert.AreEqual(18f * 22f / 15f, AIDirector.NoiseRange(18f, NoiseKind.Voice, 22f), 1e-4f, "Apex grows it");
            Assert.AreEqual(5f, AIDirector.NoiseRange(5f, NoiseKind.Footstep, 10f), "footsteps never scale");
            Assert.AreEqual(40f, AIDirector.NoiseRange(40f, NoiseKind.Gunshot, 22f), "nor do gunshots");
        }

        [Test]
        public void SneakOutpacesEveryWalkingHuman()
        {
            foreach (var a in Archetypes.All)
            {
                if (a.Has(ArchFlags.Undead) || a.Has(ArchFlags.Quadruped)) continue;
                Assert.Greater(Player.Vampire.SneakSpeed, a.WalkSpeed * 1.1f, $"{a.Id}: she cannot close on a wary walker at a sneak");
            }
            Assert.Less(Player.Vampire.SneakSpeed, Player.Vampire.GlideSpeed);
        }

        [Test]
        public void BloodArtsOpenAfterTheFirstNight()
        {
            var c = CampaignState.NewGame(Difficulty.Hunter);
            c.MissionIndex = 0;
            Assert.IsFalse(c.ArtsOpen, "not before M01 is won");
            c.MissionIndex = 1;
            Assert.IsTrue(c.ArtsOpen, "open for the refuge after M01");
        }

        static bool Grants(string mission, string node)
        {
            var t = Resources.Load<TextAsset>("Missions/" + mission);
            Assert.IsNotNull(t, mission);
            foreach (var r in MapParser.Parse(t.text).Script)
                foreach (var act in r.Actions)
                    if (act.Count > 1 && act[0] == "grant" && act[1] == node) return true;
            return false;
        }

        [Test]
        public void BeckonIsGrantedInTheSecondNight()
        {
            Assert.IsTrue(Grants("m02", "dominion.beckon"), "the Abbess's first gift comes in M02");
            Assert.IsTrue(Grants("m03", "dominion.beckon"), "M03 re-grants it for saves made before the move");
            Assert.IsFalse(Grants("m01", "dominion.beckon"));
        }

        [Test]
        public void ChallengesPayAndUnbrokenMeansNoAlarm()
        {
            Assert.AreEqual(1, Challenges.MarkReward);
            var r = new Mission.MissionResult { Won = true, NeverSpotted = false, TimesSpotted = 7, Alarms = 2, Loads = 0, Time = 300f };
            Assert.IsFalse(Challenges.Earned(r, 600f, Difficulty.Hunter).Contains(Challenges.Unbroken), "spotted seven times with two alarms is not unbroken");
        }
    }
}
