using NUnit.Framework;
using UnityEngine;
using Vespertine.Level;
using Vespertine.Stealth;

namespace Vespertine.Tests
{
    /// <summary>The Exposure Field's grid (SR.3, D137). Its equality with LightAt needs physics and a level, so it runs in
    /// play (<c>DevExposureCheck</c>); this is the pure part.</summary>
    public class ExposureFieldTests
    {
        [Test]
        public void CellsTileTheLevelGrid()
        {
            Assert.AreEqual(LevelData.CellSize, ExposureGrid.Cell * ExposureGrid.PerTile);
            // world x 0..2 is tile column 0, cells 0..3
            Assert.AreEqual(0, ExposureGrid.CellIndex(0.01f));
            Assert.AreEqual(3, ExposureGrid.CellIndex(1.99f));
            Assert.AreEqual(4, ExposureGrid.CellIndex(2.01f));
            Assert.AreEqual(0, ExposureGrid.TileX(3));
            Assert.AreEqual(1, ExposureGrid.TileX(4));
            Assert.AreEqual(-1, ExposureGrid.CellIndex(-0.01f));
        }

        [Test]
        public void CellRowsMatchTileRowsNorthToSouth()
        {
            // a 10-row map: row 0 is the north edge (largest z), row 9 the south (z 0..2)
            const int rows = 10;
            for (int y = 0; y < rows; y++)
            {
                float zCentre = (rows - 1 - y) * LevelData.CellSize + LevelData.CellSize * 0.5f;
                Assert.AreEqual(y, ExposureGrid.TileY(ExposureGrid.CellIndex(zCentre), rows), $"row {y}");
                Assert.AreEqual(y, ExposureGrid.TileY(ExposureGrid.CellIndex(zCentre - 0.99f), rows), $"row {y} south edge");
                Assert.AreEqual(y, ExposureGrid.TileY(ExposureGrid.CellIndex(zCentre + 0.99f), rows), $"row {y} north edge");
            }
        }

        [Test]
        public void FlatReachCutsTheSphere()
        {
            Assert.AreEqual(7f, ExposureGrid.FlatReach(7f, 0f), 1e-5f);
            Assert.AreEqual(Mathf.Sqrt(49f - 2.2f * 2.2f), ExposureGrid.FlatReach(7f, 2.2f), 1e-5f);
            Assert.AreEqual(Mathf.Sqrt(49f - 2.2f * 2.2f), ExposureGrid.FlatReach(7f, -2.2f), 1e-5f);
            // a lamp 8 m below a roof doesn't reach it
            Assert.AreEqual(0f, ExposureGrid.FlatReach(7f, 8f));
        }

        [Test]
        public void NearestSqToCell()
        {
            // cell (2, 2) spans x 1..1.5, z 1..1.5
            Assert.AreEqual(0f, ExposureGrid.NearestSq(1.2f, 1.3f, 2, 2));
            Assert.AreEqual(1f, ExposureGrid.NearestSq(0f, 1.2f, 2, 2), 1e-5f);
            Assert.AreEqual(0.25f, ExposureGrid.NearestSq(1.2f, 2f, 2, 2), 1e-5f);
            // diagonal from the corner (1.5, 1.5)
            Assert.AreEqual(0.5f, ExposureGrid.NearestSq(2f, 2f, 2, 2), 1e-5f);
        }

        [Test]
        public void SlotEntriesPackLightAndPartialWalls()
        {
            for (int id = 0; id < 3000; id += 997)
                foreach (bool partial in new[] { false, true })
                {
                    var e = ExposureGrid.Pack(id, partial);
                    Assert.AreNotEqual(0, e, "an entry is never the empty slot");
                    Assert.AreEqual(id, ExposureGrid.Light(e));
                    Assert.AreEqual(partial, ExposureGrid.Partial(e));
                }
        }

        [Test]
        public void SlotsFillRemoveAndStayPacked()
        {
            var slots = new ushort[3 * ExposureGrid.Slots];
            for (int id = 0; id < ExposureGrid.Slots; id++) Assert.IsTrue(ExposureGrid.Insert(slots, 1, ExposureGrid.Pack(id, id % 2 == 0)));
            Assert.IsFalse(ExposureGrid.Insert(slots, 1, ExposureGrid.Pack(99, false)), "a full cell says so");
            // other cells untouched
            for (int k = 0; k < ExposureGrid.Slots; k++) { Assert.AreEqual(0, slots[k]); Assert.AreEqual(0, slots[2 * ExposureGrid.Slots + k]); }
            ExposureGrid.Remove(slots, 1, 2);
            ExposureGrid.Remove(slots, 1, 0);
            int o = ExposureGrid.Slots;
            Assert.AreEqual(new[] { 1, 3, 4, 5 }, new[] { ExposureGrid.Light(slots[o]), ExposureGrid.Light(slots[o + 1]), ExposureGrid.Light(slots[o + 2]), ExposureGrid.Light(slots[o + 3]) });
            Assert.AreEqual(0, slots[o + 4]);
            Assert.AreEqual(0, slots[o + 5]);
            Assert.IsTrue(ExposureGrid.Partial(slots[o + 1]) == false && ExposureGrid.Partial(slots[o + 2]));
            // removing a light that isn't there changes nothing; freed slots take new entries
            ExposureGrid.Remove(slots, 1, 42);
            Assert.AreEqual(5, ExposureGrid.Light(slots[o + 3]));
            Assert.IsTrue(ExposureGrid.Insert(slots, 1, ExposureGrid.Pack(7, false)));
            Assert.AreEqual(7, ExposureGrid.Light(slots[o + 4]));
        }
    }
}
