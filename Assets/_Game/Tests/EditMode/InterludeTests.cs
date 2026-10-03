using System.Linq;
using NUnit.Framework;
using Vespertine.Data;
using Vespertine.Progression;

namespace Vespertine.Tests
{
    public class InterludeTests
    {
        static CampaignState Camp(params string[] flags)
        {
            var c = CampaignState.NewGame(Difficulty.Hunter);
            foreach (var f in flags) c.SetFlag(f);
            return c;
        }

        static string All(Interludes.Dream d) => Interludes.AsText(d);

        [Test]
        public void EveryNightButTheLastHasADream()
        {
            var c = Camp();
            for (int i = 0; i < Missions.All.Count; i++)
            {
                var id = Missions.All[i].Id;
                var d = Interludes.For(id, c);
                if (i == Missions.All.Count - 1) { Assert.IsNull(d, "the ending plays after the last night"); continue; }
                Assert.IsNotNull(d, id);
                Assert.IsFalse(string.IsNullOrEmpty(d.Title), id);
                Assert.GreaterOrEqual(d.Lines.Count, 4, id);
                Assert.IsTrue(d.Lines.All(l => !string.IsNullOrEmpty(l.Text)), id);
                Assert.IsTrue(d.Lines.Any(l => l.Speaker == Interludes.Abbess), id + ": the Abbess speaks in every dream");
            }
            Assert.IsNull(Interludes.For("nope", c));
        }

        [Test]
        public void DreamsFollowWhatSheDid()
        {
            Assert.That(All(Interludes.For("m08", Camp("hollin_thrall"))), Does.Contain("Hollin:"));
            Assert.That(All(Interludes.For("m08", Camp("hollin_dead"))), Does.Contain("bury Sister Hollin"));
            Assert.That(All(Interludes.For("m08", Camp("hollin_dead", "marrow_locket"))), Does.Contain("locket"));
            Assert.That(All(Interludes.For("m08", Camp("hollin_dead"))), Does.Not.Contain("locket"));

            Assert.That(All(Interludes.For("m12", Camp("vane_dead"))), Does.Contain("closed his book"));
            Assert.That(All(Interludes.For("m12", Camp("vane_alive", "archive_burned"))), Does.Contain("archive burning"));

            var c = Camp();
            c.Tobias = "lost";
            Assert.That(All(Interludes.For("m13", c)), Does.Contain("Do not look back"));
            c.Tobias = "alive";
            Assert.That(All(Interludes.For("m13", c)), Does.Contain("Tobias: Whatever's down there"));
            c.Terror = 5; c.Rumour = 1;
            Assert.That(All(Interludes.For("m13", c)), Does.Contain("bolted against you"));
            c.Terror = 1; c.Rumour = 5;
            Assert.That(All(Interludes.For("m13", c)), Does.Contain("candles in their windows"));
        }

        [Test]
        public void ADreamIsShownOnceAndKeptInTheJournal()
        {
            var c = Camp();
            var d = Interludes.For("m01", c);
            Assert.IsTrue(Interludes.Record(d, c));
            Assert.IsTrue(c.Flag(Interludes.FlagFor("m01")));
            var page = c.Lore.Single(l => l.Id == d.Id);
            Assert.AreEqual("m01", page.Mission);
            Assert.That(page.Title, Does.Contain(d.Title));
            Assert.That(page.Body, Does.Contain("Abbess: You woke among the failures"));
            Assert.IsFalse(Interludes.Record(Interludes.For("m01", c), c), "a replay doesn't dream again");
            Assert.AreEqual(1, c.Lore.Count);
        }
    }
}
