using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Vespertine.Stealth;

namespace Vespertine.Tests
{
    /// <summary>cm_caged: exactly half the lights she could put out by hand are caged, and always the same ones.</summary>
    public class CagedLampsTests
    {
        static List<Vector2Int> Cells(int n, int seed)
        {
            var r = new System.Random(seed);
            var set = new HashSet<Vector2Int>();
            while (set.Count < n) set.Add(new Vector2Int(r.Next(0, 80), r.Next(0, 60)));
            return set.ToList();
        }

        [Test]
        public void CagesExactlyHalfRoundedUp()
        {
            foreach (int n in new[] { 0, 1, 2, 3, 11, 26, 39 })
                Assert.AreEqual((n + 1) / 2, GameLight.PickCaged(Cells(n, n)).Count, $"{n} lights");
        }

        [Test]
        public void TheSameLightsAreCagedWhateverTheOrder()
        {
            var cells = Cells(30, 5);
            var picked = new HashSet<Vector2Int>(GameLight.PickCaged(cells).Select(i => cells[i]));
            var shuffled = cells.OrderBy(c => c.x * 7919 + c.y * 104729 % 31).ToList();
            var again = new HashSet<Vector2Int>(GameLight.PickCaged(shuffled).Select(i => shuffled[i]));
            Assert.IsTrue(picked.SetEquals(again));
        }

        [Test]
        public void ASingleCandleLitNightStillGetsCages()
        {
            // M08's case: almost no street lamps, its light is candles and braziers
            Assert.IsTrue(GameLight.CageEligible(LightKind.Candle));
            Assert.IsTrue(GameLight.CageEligible(LightKind.Brazier));
            Assert.IsTrue(GameLight.CageEligible(LightKind.GasLamp));
            Assert.IsTrue(GameLight.CageEligible(LightKind.WallLamp));
            Assert.IsFalse(GameLight.CageEligible(LightKind.Holy), "the Church's own lights are not the Vigil's to cage");
            Assert.IsFalse(GameLight.CageEligible(LightKind.Fire), "hearths stay open");
        }
    }
}
