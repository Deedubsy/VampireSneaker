using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using Vespertine.Data;
using Vespertine.Level;

namespace Vespertine.Tests
{
    /// <summary>
    /// The campaign's memory, checked end to end over the shipped missions. Every `campaign_flag` a night sets must
    /// matter later: a later mission reads it (a `cf_` condition, or a `flag_` group), or the game's code does (the
    /// epilogue, the endings, the blood-dreams, the AI). Every `cf_` a mission reads must have been set by an earlier
    /// night, because a mission sees the campaign as it stood when the night began.
    /// </summary>
    public class CampaignFlagTests
    {
        // flags that only record what happened (both always set on the critical path): kept for saves and debugging
        static readonly HashSet<string> RecordOnly = new HashSet<string> { "tobias_met", "blackout" };

        class Night
        {
            public string Id;
            public readonly HashSet<string> Sets = new HashSet<string>(), Reads = new HashSet<string>();
        }

        static List<Night> Nights()
        {
            var list = new List<Night>();
            foreach (var m in Missions.All)
            {
                var t = Resources.Load<TextAsset>("Missions/" + m.Id);
                Assert.IsNotNull(t, m.Id);
                var d = MapParser.Parse(t.text);
                var n = new Night { Id = m.Id };
                foreach (var r in d.Script)
                {
                    foreach (var a in r.Actions)
                        if (a.Count > 1 && a[0] == "campaign_flag" && (a.Count < 3 || a[2] != "off")) n.Sets.Add(a[1]);
                    foreach (var c in r.Conditions) Read(n, c);
                }
                foreach (var o in d.Objectives)
                    if (o.Opts.TryGetValue("if", out var oc))
                        foreach (var c in oc.Split(',')) Read(n, c);
                foreach (var e in d.Entities)
                    if (e.Group != null && e.Group.StartsWith("flag_")) n.Reads.Add(e.Group.Substring(5));
                list.Add(n);
            }
            return list;
        }

        static void Read(Night n, string condition)
        {
            var c = condition.Trim().TrimStart('!');
            if (c.StartsWith("cf_")) n.Reads.Add(c.Substring(3));
        }

        static string _code;

        /// <summary>All the game's C# source: flags the code reads appear in it as string literals.</summary>
        static string Code()
        {
            if (_code != null) return _code;
            var sb = new StringBuilder();
            foreach (var f in Directory.GetFiles(Path.Combine(Application.dataPath, "_Game", "Scripts"), "*.cs", SearchOption.AllDirectories))
                if (!f.EndsWith("DevAbilitySweep.cs")) sb.Append(File.ReadAllText(f));
            return _code = sb.ToString();
        }

        static bool CodeReads(string flag)
        {
            if (Code().Contains("\"" + flag + "\"")) return true;
            // counted families, read as a prefix plus a number (the Anchoress's shrines)
            int i = flag.Length;
            while (i > 0 && char.IsDigit(flag[i - 1])) i--;
            return i < flag.Length && Code().Contains("\"" + flag.Substring(0, i) + "\" + ");
        }

        [Test]
        public void EveryCampaignFlagMattersLater()
        {
            var nights = Nights();
            var problems = new List<string>();
            for (int i = 0; i < nights.Count; i++)
                foreach (var f in nights[i].Sets)
                {
                    if (RecordOnly.Contains(f) || CodeReads(f)) continue;
                    bool later = false;
                    for (int j = i + 1; j < nights.Count && !later; j++) later = nights[j].Reads.Contains(f);
                    if (!later) problems.Add($"{nights[i].Id} sets '{f}', but nothing after it reads it");
                }
            Assert.IsEmpty(problems, string.Join("\n", problems));
        }

        [Test]
        public void TheShrinesAreFoundInOrderOncePerNight()
        {
            // found_shrine1..N: one per night, numbered in campaign order (the epilogue counts them, and each shrine's
            // Journal entry names its place in the sequence)
            var nights = Nights();
            int last = 0;
            for (int i = 1; i <= Vespertine.Progression.CampaignState.TotalShrines; i++)
            {
                var at = nights.FindAll(n => n.Sets.Contains("found_shrine" + i));
                Assert.AreEqual(1, at.Count, $"found_shrine{i} should be set by exactly one mission");
                int idx = nights.IndexOf(at[0]);
                Assert.Greater(idx, last - 1, $"found_shrine{i} ({at[0].Id}) comes before an earlier-numbered shrine");
                last = idx + 1;
            }
            foreach (var n in nights)
                foreach (var f in n.Sets)
                    if (f.StartsWith("found_shrine"))
                        Assert.IsTrue(int.TryParse(f.Substring(12), out var k) && k >= 1 && k <= Vespertine.Progression.CampaignState.TotalShrines, $"{n.Id} sets {f}");
        }

        [Test]
        public void EveryCampaignFlagReadWasSetEarlier()
        {
            var nights = Nights();
            var problems = new List<string>();
            for (int i = 0; i < nights.Count; i++)
                foreach (var f in nights[i].Reads)
                {
                    bool earlier = false;
                    for (int j = 0; j < i && !earlier; j++) earlier = nights[j].Sets.Contains(f);
                    if (!earlier) problems.Add($"{nights[i].Id} reads '{f}', which no earlier mission sets");
                }
            Assert.IsEmpty(problems, string.Join("\n", problems));
        }
    }
}
