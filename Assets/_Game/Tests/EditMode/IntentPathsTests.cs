using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Vespertine.Visual;

namespace Vespertine.Tests
{
    /// <summary>The dotted path a suspicious guard walks (SR.5, SR.11).</summary>
    public class IntentPathsTests
    {
        readonly List<Vector3> _out = new List<Vector3>();

        [Test]
        public void DotsAreEvenlySpacedRoundCorners()
        {
            var pts = new[] { Vector3.zero, new Vector3(3, 0, 0), new Vector3(3, 0, 3) };
            Assert.IsTrue(IntentPaths.Dots(pts, 3, 1f, 0.5f, 30f, _out), "reaches the end");
            Assert.AreEqual(6, _out.Count);
            Assert.AreEqual(new Vector3(0.5f, 0, 0), _out[0]);
            Assert.AreEqual(new Vector3(3f, 0, 0.5f), _out[3], "the spacing carries round the corner");
            Assert.AreEqual(new Vector3(3f, 0, 2.5f), _out[5]);
        }

        [Test]
        public void ALongPathStopsAtTheCapWithoutItsEnd()
        {
            var pts = new[] { Vector3.zero, new Vector3(100, 0, 0) };
            Assert.IsFalse(IntentPaths.Dots(pts, 2, 1f, 0f, 10f, _out));
            Assert.AreEqual(11, _out.Count);
            Assert.LessOrEqual(_out[_out.Count - 1].x, 10f);
        }

        [Test]
        public void OnlyTheGivenCornersCountAndRepeatsAreSkipped()
        {
            var pts = new[] { Vector3.zero, Vector3.zero, new Vector3(2, 0, 0), new Vector3(50, 0, 0) };
            Assert.IsTrue(IntentPaths.Dots(pts, 3, 0.5f, 0.25f, 30f, _out));
            Assert.AreEqual(4, _out.Count);
            Assert.IsTrue(IntentPaths.Dots(pts, 1, 0.5f, 0f, 30f, _out), "a single point is its own end");
            Assert.AreEqual(0, _out.Count);
        }
    }
}
