using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Vespertine.Stealth;

namespace Vespertine.Tests
{
    public class BloodTrailTests
    {
        static readonly List<Vector3> Pos = new List<Vector3>();
        static readonly List<int> Seq = new List<int>();

        static void Trail(params (float x, float z, int seq)[] drops)
        {
            Pos.Clear(); Seq.Clear();
            foreach (var d in drops) { Pos.Add(new Vector3(d.x, 0f, d.z)); Seq.Add(d.seq); }
        }

        [Test]
        public void FollowsDropsForwardInSpillOrder()
        {
            // listed out of order on purpose: the walk is by Seq, not by list position
            Trail((0, 0, 1), (4, 0, 3), (2, 0, 2), (6, 0, 4), (8, 0, 5));
            Assert.AreEqual(4, Evidence.FollowTrail(Pos, Seq, 0, 4.5f, 10), "reaches the freshest drop");
            Assert.AreEqual(2, Evidence.FollowTrail(Pos, Seq, 0, 4.5f, 1), "one step goes to the next drop, not the nearest-by-index");
            Assert.AreEqual(4, Evidence.FollowTrail(Pos, Seq, 4, 4.5f, 10), "the freshest drop is the end of the trail");
        }

        [Test]
        public void StopsAtAGapAndNeverDoublesBack()
        {
            // she crossed back over her own trail, then misted 12 m (no drops)
            Trail((0, 0, 1), (2, 0, 2), (4, 0, 3), (2, 0.5f, 4), (14, 0, 5));
            Assert.AreEqual(3, Evidence.FollowTrail(Pos, Seq, 0, 4.5f, 10), "the gap ends the trail");
            Assert.AreEqual(3, Evidence.FollowTrail(Pos, Seq, 3, 4.5f, 10));
        }

        [Test]
        public void SkipsAheadWhenTheNextDropIsOutOfReach()
        {
            // drop 2 is far off (dripped on a roof she leapt from); 3 is within reach of 1
            Trail((0, 0, 1), (0, 20, 2), (3, 0, 3));
            Assert.AreEqual(2, Evidence.FollowTrail(Pos, Seq, 0, 4.5f, 10));
        }
    }
}
