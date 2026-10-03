using System.Collections.Generic;
using Vespertine.Data;
using Vespertine.Mission;

namespace Vespertine.Progression
{
    /// <summary>Per-mission mastery challenges (P2). Each is judged from one night's result, so a single
    /// run can earn several; the mission record keeps every challenge ever earned, and each pays
    /// <see cref="MarkReward"/> the first time (Gameplay Redesign P8). Pure; unit-tested.</summary>
    public static class Challenges
    {
        public struct Def
        {
            public string Id, Name, Text;
            public Def(string id, string name, string text) { Id = id; Name = name; Text = text; }
        }

        public const string Unseen = "unseen", Merciful = "merciful", Silent = "silent", Swift = "swift",
            Unbroken = "unbroken", Thorough = "thorough", ApexId = "apex";

        /// <summary>Marks paid the first time a challenge is earned on a mission.</summary>
        public const int MarkReward = 1;

        public static readonly Def[] All =
        {
            new Def(Unseen, "Unseen", "Never spotted"),
            new Def(Merciful, "Merciful Hunger", "Nobody dies by her hand"),
            new Def(Silent, "Silent Night", "No alarm raised"),
            new Def(Swift, "Before the Bell", "Finish under par"),
            new Def(Unbroken, "Unbroken", "No alarm and no loads"),
            new Def(Thorough, "Every Thread", "Every optional objective in one night"),
            new Def(ApexId, "Apex", "Finish on Apex"),
        };

        public static Def Get(string id)
        {
            foreach (var d in All) if (d.Id == id) return d;
            return new Def(id, id, "");
        }

        /// <summary>The challenges this night earned. A lost night earns none.</summary>
        public static List<string> Earned(MissionResult r, float par, Difficulty difficulty)
        {
            var l = new List<string>();
            if (r == null || !r.Won) return l;
            if (r.NeverSpotted) l.Add(Unseen);
            if (r.Kills == 0) l.Add(Merciful);
            if (r.Alarms == 0) l.Add(Silent);
            if (par > 0f && r.Time <= par) l.Add(Swift);
            if (r.Loads == 0 && r.Alarms == 0) l.Add(Unbroken);   // an unbroken night: no reload, no alarm (becomes "no Hunt" with the Hunt)
            if (r.OptionalsMissed.Count == 0) l.Add(Thorough);
            if (difficulty == Difficulty.Apex) l.Add(ApexId);
            return l;
        }

        /// <summary>Par as m:ss, for the mission list and debrief.</summary>
        public static string FormatPar(float par)
        {
            int s = UnityEngine.Mathf.RoundToInt(par);
            return $"{s / 60}:{s % 60:00}";
        }
    }
}
