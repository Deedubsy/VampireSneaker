using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Vespertine.Data;
using Vespertine.Level;
using Vespertine.Progression;

namespace Vespertine.Tests
{
    /// <summary>
    /// The earning curve (X6), measured from the shipped mission files with <see cref="CampaignEconomy"/>: a ghost, a
    /// typical player and a predator all open each tree tier on time, nobody reaches the top before the last act, and
    /// nobody can afford every node until the endgame. Editing a mission's population or objectives can move these;
    /// a failure here means the Awakening thresholds or the mission need another look, not that the test is wrong.
    /// </summary>
    public class CampaignEconomyTests
    {
        static List<CampaignEconomy.Supply> _supply;

        static List<CampaignEconomy.Supply> Supply()
        {
            if (_supply != null) return _supply;
            _supply = new List<CampaignEconomy.Supply>();
            foreach (var m in Missions.All)
            {
                var t = Resources.Load<TextAsset>("Missions/" + m.Id);
                Assert.IsNotNull(t, m.Id);
                _supply.Add(CampaignEconomy.Survey(MapParser.Parse(t.text)));
            }
            return _supply;
        }

        static int Index(string id) => Missions.All.FindIndex(m => m.Id == id);

        static int TreeTotal()
        {
            int total = 0;
            foreach (var n in Skills.Nodes) total += n.Cost;
            return total;
        }

        [Test]
        public void EveryNightHasBloodAndSomethingToEarn()
        {
            foreach (var s in Supply())
            {
                Assert.GreaterOrEqual(s.Feedable, 4, $"{s.Mission}: too few people to feed on");
                Assert.GreaterOrEqual(s.Optionals, 1, $"{s.Mission}: no optional objective");
            }
        }

        [Test]
        public void EveryStyleOpensTheTiersOnTime()
        {
            var nights = Supply();
            foreach (var style in CampaignEconomy.Styles)
            {
                var run = CampaignEconomy.Simulate(nights, style);
                // tier II with the Arts (from the fourth night), tier III by the second act's close
                Assert.GreaterOrEqual(run[Index("m04")].AwakeningAtStart, 3, $"{style.Name}: tier II not open by m04");
                Assert.GreaterOrEqual(run[Index("m08")].AwakeningAtStart, 5, $"{style.Name}: tier III not open by m08");
                Assert.GreaterOrEqual(run[Index("m14")].AwakeningAtStart, 8, $"{style.Name}: no capstone for the last night");
            }
            var typical = CampaignEconomy.Simulate(nights, CampaignEconomy.Typical);
            Assert.GreaterOrEqual(typical[Index("m10")].AwakeningAtStart, 8, "Typical: capstones should open in the third act");
        }

        [Test]
        public void NobodyPeaksBeforeTheLastAct()
        {
            var nights = Supply();
            foreach (var style in CampaignEconomy.Styles)
            {
                var run = CampaignEconomy.Simulate(nights, style);
                for (int i = 0; i < Index("m10"); i++)
                    Assert.Less(run[i].AwakeningAtStart, CampaignState.MaxAwakening, $"{style.Name}: Awakening 10 at {run[i].Mission}");
                Assert.Less(run[Index("m12")].MarksAtStart, TreeTotal(), $"{style.Name}: every node affordable before m12");
            }
            Assert.AreEqual(CampaignState.MaxAwakening, CampaignEconomy.Final(CampaignEconomy.Simulate(nights, CampaignEconomy.Typical)).awakening,
                "Typical should finish the campaign fully awakened");
            Assert.Less(CampaignEconomy.Final(CampaignEconomy.Simulate(nights, CampaignEconomy.Ghost)).awakening, CampaignState.MaxAwakening,
                "a ghost who barely feeds should not finish fully awakened");
        }

        [Test]
        public void MarksBuyMostButNotAllOfTheTrees()
        {
            var nights = Supply();
            int tree = TreeTotal();
            var ghost = CampaignEconomy.Final(CampaignEconomy.Simulate(nights, CampaignEconomy.Ghost)).marks;
            var typical = CampaignEconomy.Final(CampaignEconomy.Simulate(nights, CampaignEconomy.Typical)).marks;
            Assert.GreaterOrEqual(ghost, tree / 2, "a ghost should still fill half the trees");
            Assert.Less(typical, tree, "a typical campaign should have to choose");
            Assert.GreaterOrEqual(typical, tree * 2 / 3, "a typical campaign should reach two thirds of the trees");
        }

        [Test]
        public void DifficultyScalesFeedingNotObjectives()
        {
            var nights = Supply();
            foreach (var style in CampaignEconomy.Styles)
            {
                int Total(Difficulty d)
                {
                    var r = CampaignEconomy.Simulate(nights, style, d);
                    return r[r.Count - 1].VitaeAtStart + r[r.Count - 1].VitaeEarned;
                }
                Assert.Greater(Total(Difficulty.Merciful), Total(Difficulty.Hunter), style.Name);
                Assert.Greater(Total(Difficulty.Hunter), Total(Difficulty.Apex), style.Name);
            }
            Assert.AreEqual(CampaignEconomy.PrimaryVitae + 2 * CampaignEconomy.OptionalVitae + CampaignEconomy.SecretVitae,
                CampaignEconomy.ObjectiveVitae(2, 1));
        }
    }
}
