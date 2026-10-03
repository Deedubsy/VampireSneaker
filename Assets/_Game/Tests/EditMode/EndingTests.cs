using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Vespertine.Progression;
using Vespertine.Stealth;

namespace Vespertine.Tests
{
    public class EndingTests
    {
        [Test]
        public void SunbeamWaitsForItsHourThenCrossesTheFloor()
        {
            Assert.IsFalse(SunbeamMath.Up(99f, 100f));
            Assert.IsTrue(SunbeamMath.Up(100f, 100f));
            Assert.AreEqual(0f, SunbeamMath.Travel(50f, 100f, 200f));
            Assert.AreEqual(0.5f, SunbeamMath.Travel(200f, 100f, 200f), 1e-4f);
            Assert.AreEqual(1f, SunbeamMath.Travel(900f, 100f, 200f));
            Assert.AreEqual(1f, SunbeamMath.Travel(101f, 100f, 0f));
            Assert.AreEqual(0f, SunbeamMath.Strength(99f, 100f));
            Assert.AreEqual(0.5f, SunbeamMath.Strength(100f + SunbeamMath.FadeIn / 2f, 100f), 1e-4f);
            Assert.AreEqual(1f, SunbeamMath.Strength(500f, 100f));
        }

        [Test]
        public void SunbeamPathIsWalkedByDistance()
        {
            var path = new List<Vector2> { new Vector2(0, 0), new Vector2(10, 0), new Vector2(10, 30) };   // 40 long
            Assert.AreEqual(new Vector2(0, 0), SunbeamMath.Along(path, 0f));
            Assert.AreEqual(5f, SunbeamMath.Along(path, 0.125f).x, 1e-4f);
            var mid = SunbeamMath.Along(path, 0.5f);   // 20 along: 10 on the first leg, 10 up the second
            Assert.AreEqual(10f, mid.x, 1e-4f); Assert.AreEqual(10f, mid.y, 1e-4f);
            Assert.AreEqual(new Vector2(10, 30), SunbeamMath.Along(path, 1f));
            Assert.AreEqual(new Vector2(3, 4), SunbeamMath.Along(new List<Vector2> { new Vector2(3, 4) }, 0.7f));
        }

        static CampaignState Camp(int terror, int rumour, string tobias = "alive", params string[] flags)
        {
            var c = CampaignState.NewGame(Vespertine.Data.Difficulty.Hunter);
            c.Terror = terror; c.Rumour = rumour; c.Tobias = tobias;
            foreach (var f in flags) c.SetFlag(f);
            return c;
        }

        [Test]
        public void TheVaultChoiceAndTheCitysFearPickTheEnding()
        {
            Assert.AreEqual("endless_night", Camp(5, 2).Ending("free"));
            Assert.AreEqual("night_court", Camp(2, 5).Ending("free"));
            Assert.AreEqual("new_abbess", Camp(5, 2).Ending("consume"));
            Assert.AreEqual("pale_lady", Camp(2, 2).Ending("consume"));
            Assert.AreEqual("ashes", Camp(5, 2).Ending("destroy"));
            Assert.AreEqual("dawn_tobias", Camp(1, 4, "alive").Ending("destroy"));
            Assert.AreEqual("dawn", Camp(1, 4, "thrall").Ending("destroy"));
        }

        [Test]
        public void EpilogueFollowsWhatSheDid()
        {
            var a = Camp(1, 4, "alive", "candle_row_held", "vane_dead", "hollin_thrall", "saule_dead", "saule_purged", "clement_saved", "faithful_bastion");
            var e = a.Epilogue("dawn_tobias");
            Assert.IsTrue(e[0].Contains("Candle Row") && e[0].Contains("unbarred"), e[0]);
            Assert.IsTrue(e.Exists(x => x.Contains("no name")));
            Assert.IsTrue(e.Exists(x => x.Contains("wrong way")));
            Assert.IsTrue(e.Exists(x => x.Contains("purge chamber")));
            Assert.IsTrue(e.Exists(x => x.Contains("Clement")));
            Assert.IsTrue(e.Exists(x => x.Contains("cellars under Candle Row")));   // not a reign: the Faithful stay hidden

            var b = Camp(6, 1, "thrall", "vane_alive", "faithful_bastion");
            var r = b.Epilogue("endless_night");
            Assert.IsTrue(r[0].Contains("keeps her door"), r[0]);
            Assert.IsTrue(r.Exists(x => x.Contains("lamp burning in his window")));   // fear: Vane hides
            Assert.IsTrue(r.Exists(x => x.Contains("in the open")));
            Assert.IsFalse(r.Exists(x => x.Contains("Saule")));
            Assert.IsFalse(r.Exists(x => x.Contains("Hollin")));

            var c = Camp(0, 0, "alive", "saule_dead").Epilogue("dawn");             // dead, but not by his own purge
            Assert.IsTrue(c.Exists(x => x.Contains("sunstones")) && !c.Exists(x => x.Contains("purge chamber")));

            var lost = Camp(0, 0, "lost").Epilogue("dawn");
            Assert.IsTrue(lost[0].Contains("not among the living"));
        }

        [Test]
        public void EpilogueCountsTheShrines()
        {
            var c = Camp(0, 0, "alive", "found_shrine2", "found_shrine5", "found_shrine9");
            Assert.AreEqual(3, c.ShrinesFound());
            Assert.IsTrue(c.Epilogue("dawn").Exists(x => x.Contains("3 of the old order's shrines")));
            for (int i = 2; i <= 10; i++) c.SetFlag("found_shrine" + i);
            Assert.IsFalse(c.Epilogue("dawn").Exists(x => x.Contains("every shrine")), "the docks altar (M03) is the first");
            c.SetFlag("found_shrine1");
            Assert.AreEqual(CampaignState.TotalShrines, c.ShrinesFound());
            Assert.IsTrue(c.Epilogue("dawn").Exists(x => x.Contains("every shrine")));
        }
    }
}
