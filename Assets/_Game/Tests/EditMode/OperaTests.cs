using NUnit.Framework;
using UnityEngine;
using Vespertine.AI;
using Vespertine.Data;
using Vespertine.Level;
using Vespertine.Mission;
using Vespertine.Stealth;

namespace Vespertine.Tests
{
    /// <summary>M11 The Opera of Lanterns: the crescendo, framing the Vigil, and the Watch/Vigil split it causes.</summary>
    public class OperaTests
    {
        [Test]
        public void CrescendoDrownsAllButBellsAndLures()
        {
            Assert.AreEqual(10f, NoiseSystem.Heard(10f, NoiseKind.Body, false), 1e-4f, "no swell: unchanged");
            Assert.AreEqual(10f * MissionController.HushFactor, NoiseSystem.Heard(10f, NoiseKind.Body, true), 1e-4f);
            Assert.AreEqual(10f * MissionController.HushFactor, NoiseSystem.Heard(10f, NoiseKind.Scream, true), 1e-4f, "even a scream");
            Assert.AreEqual(10f, NoiseSystem.Heard(10f, NoiseKind.Bell, true), 1e-4f, "the bells carry");
            Assert.AreEqual(10f, NoiseSystem.Heard(10f, NoiseKind.Lure, true), 1e-4f, "a lure is meant to be heard");
        }

        [Test]
        public void VigilManTakesTheBlameOnlyStandingOverTheBody()
        {
            var death = new Vector3(10, 3, 10);
            Assert.IsTrue(MissionController.StandsOver(death + new Vector3(4f, 0, 3f), death), "5 m away");
            Assert.IsFalse(MissionController.StandsOver(death + new Vector3(6f, 0, 1f), death), "too far");
            Assert.IsFalse(MissionController.StandsOver(death + new Vector3(1f, -3f, 0), death), "the floor below");
            Assert.IsTrue(MissionController.StandsOver(death + new Vector3(1f, 1f, 0), death), "a step up");
        }

        [Test]
        public void WatchAndVigilSplitOnlyAfterTheFrameInActThree()
        {
            Assert.IsTrue(AIDirector.Split(true, 3, Faction.Watch, Faction.Vigil));
            Assert.IsTrue(AIDirector.Split(true, 3, Faction.Vigil, Faction.Watch));
            Assert.IsFalse(AIDirector.Split(false, 3, Faction.Watch, Faction.Vigil), "never framed");
            Assert.IsFalse(AIDirector.Split(true, 2, Faction.Watch, Faction.Vigil), "not Act III");
            Assert.IsFalse(AIDirector.Split(true, 3, Faction.Watch, Faction.Watch), "the Watch still answer each other");
            Assert.IsFalse(AIDirector.Split(true, 3, Faction.Vigil, Faction.Vigil));
            Assert.IsFalse(AIDirector.Split(true, 3, Faction.Church, Faction.Vigil));
        }

        [Test]
        public void OperaScriptVocabularyValidates()
        {
            const string map = @"@mission
id = mo
title = Opera
act = 3
@map
########
#......#
#......#
########
@entities
player 1 1
npc g1 watchman 2 1
npc v1 hunter 4 1
prop curtain 3 2
prop seats 5 2 face=N
prop scenery 2 2
prop balustrade 6 2 yaw=90
@objectives
primary a ""Kill him"" kill g1
@script
on start: countdown t_swell 45 ""The swell""
every timer t_swell: crescendo 10 ; timer t_swell 80
every crescendo any: toast ""The orchestra swells.""
on framed g1: flag framed ; evacuate g1
";
            Assert.IsEmpty(MissionValidator.Validate(MapParser.Parse(map)));
        }
    }
}
