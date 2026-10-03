using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using Vespertine.Audio;

namespace Vespertine.Tests
{
    public class ScoreTests
    {
        [Test]
        public void EveryMissionMusicKeyHasATheme()
        {
            foreach (var ta in Resources.LoadAll<TextAsset>("Missions"))
            {
                var m = Regex.Match(ta.text, @"^music\s*=\s*(\S+)", RegexOptions.Multiline);
                Assert.IsTrue(m.Success, ta.name + " has no music key");
                Assert.IsTrue(Score.Has(m.Groups[1].Value), ta.name + ": no theme '" + m.Groups[1].Value + "'");
            }
            Assert.IsFalse(Score.Has(null));
            Assert.IsFalse(Score.Has("polka"));
            Assert.IsNull(Score.Render("polka"));
        }

        [Test]
        public void ThemesRenderCleanLoopsThatKeepTheBeat()
        {
            foreach (var k in Score.Keys)
            {
                var l = Score.Render(k);
                foreach (var (name, d) in new[] { ("calm", l.Calm), ("tension", l.Tension), ("alert", l.Alert) })
                {
                    Assert.Greater(d.Length, Score.Rate * 2, $"{k} {name}");
                    Assert.IsFalse(d.Any(float.IsNaN), $"{k} {name} NaN");
                    Assert.LessOrEqual(d.Max(Math.Abs), 0.951f, $"{k} {name} peak");
                    double rms = Math.Sqrt(d.Average(x => (double)x * x));
                    Assert.That(rms, Is.InRange(0.15, 0.25), $"{k} {name} loudness");
                    Assert.Less(Math.Abs(d[d.Length - 1] - d[0]), 0.15f, $"{k} {name} clicks at the loop point");
                }
                // tension is 4 bars and alert 2: both divide the calm loop, so the layers stay on one beat
                int bars4 = l.Tension.Length;
                Assert.AreEqual(0, Math.Round(l.Calm.Length / (double)bars4) * bars4 - l.Calm.Length, 4, k + " calm/tension");
                Assert.AreEqual(bars4, l.Alert.Length * 2, 2, k + " tension/alert");
            }
        }
    }
}
