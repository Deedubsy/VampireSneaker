using System.Collections.Generic;
using UnityEngine;
using Vespertine.Level;

namespace Vespertine.Mission
{
    public enum ObjectiveState { Active = 0, Complete = 1, Failed = 2 }

    /// <summary>Runtime state of one objective from the map's @objectives section.</summary>
    public class Objective
    {
        public readonly ObjectiveSpec Spec;
        public ObjectiveState State;
        public bool Discovered;
        public float Progress;               // 0..1 for multi-part objectives
        public readonly HashSet<string> Done = new HashSet<string>();
        public float FlashT;                 // UI highlight timer

        public Objective(ObjectiveSpec spec)
        {
            Spec = spec;
            Discovered = !spec.Hidden;
        }

        public string Id => Spec.Id;
        string _heldFlag;
        /// <summary>The mission flag set while a take/interact objective's item is held: `held.&lt;id&gt;`.</summary>
        public string HeldFlag => _heldFlag ??= "held." + Spec.Id;
        public string Type => Spec.Type;
        public bool Primary => !Spec.Optional;
        public bool Hidden => Spec.Hidden;
        public bool Active => State == ObjectiveState.Active;
        public bool Complete => State == ObjectiveState.Complete;
        public bool Failed => State == ObjectiveState.Failed;
        public bool Visible => Discovered || State != ObjectiveState.Active;

        /// <summary>Objectives judged at mission end (succeed unless broken).</summary>
        public bool IsConduct => Type == "nokill" || Type == "nokill_type" || Type == "noalert" || Type == "nodetect" || Type == "nobodies"
            || Type == "nofeed" || Type == "feedonly" || Type == "nolockdown" || Type == "noholy" || Type == "protect" || Type == "hpfloor";

        /// <summary>Required parts for interact_all / feed_types / kill_all (all listed, or <c>count=N</c> of them) and dispose (count=N).</summary>
        public int PartsTotal
        {
            get
            {
                bool multi = Type == "interact_all" || Type == "feed_types" || Type == "kill_all" || Type == "valves" || Type == "escort" || Type == "evacuate" || Type == "break";
                int all = Type == "escort" ? EscortIds.Count : multi ? Spec.Args.Count : 1;
                if (!(multi || Type == "dispose") || !Spec.Opts.TryGetValue("count", out var c) || !int.TryParse(c, out var n)) return all;
                return multi ? Mathf.Clamp(n, 1, Mathf.Max(1, all)) : Mathf.Max(1, n);
            }
        }

        public string ProgressText
        {
            get
            {
                if (PartsTotal > 1) return $" ({Done.Count}/{PartsTotal})";
                return "";
            }
        }

        /// <summary>Rect objectives (reach, escape, deliver): x y w h in cells. deliver has the npc id first.</summary>
        public bool TryRect(out Rect r)
        {
            r = default;
            int o = Type == "deliver" ? 1 : Type == "escort" ? EscortIds.Count : 0;
            if (Spec.Args.Count < o + 2) return false;
            float x = Parse(Spec.Args[o]), y = Parse(Spec.Args[o + 1]);
            float w = Spec.Args.Count > o + 2 ? Parse(Spec.Args[o + 2]) : 1f;
            float h = Spec.Args.Count > o + 3 ? Parse(Spec.Args[o + 3]) : 1f;
            r = new Rect(x - 0.5f, y - 0.5f, Mathf.Max(1f, w), Mathf.Max(1f, h));
            return true;
        }

        /// <summary>escort &lt;npc...&gt; x y [w h] [count=N] [loose=1]: the prisoners named before the area.</summary>
        public System.Collections.Generic.List<string> EscortIds
        {
            get
            {
                var l = new System.Collections.Generic.List<string>();
                foreach (var a in Spec.Args) { if (IsNumber(a)) break; l.Add(a); }
                return l;
            }
        }

        static bool IsNumber(string s) => float.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _);

        public Vector3 RectCenterWorld(LevelRuntime lvl)
        {
            if (!TryRect(out var r) || lvl == null) return Vector3.zero;
            float cx = r.x + r.width * 0.5f, cy = r.y + r.height * 0.5f;
            return lvl.Data.CellToWorld(cx, cy, lvl.SurfaceHeightCell(cx, cy));
        }

        static float Parse(string s) => float.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0f;
    }

    /// <summary>The tally shown on the debrief screen.</summary>
    public class MissionResult
    {
        public string MissionId, Title, Cause;
        public bool Won;
        public float Time;
        public int Kills, Sips, Drains, TimesSpotted, Alarms, BodiesFound, Disposed, Loads;
        public int VitaeEarned, MarksEarned, AwakeningBefore, AwakeningAfter;
        public int TerrorDelta, RumourDelta;
        public bool NeverSpotted, FirstClear;
        public readonly List<string> Optionals = new List<string>();
        public readonly List<string> OptionalsMissed = new List<string>();
        public readonly List<string> Secrets = new List<string>();
        public readonly List<string> NewCountermeasures = new List<string>();
        public readonly List<string> Notes = new List<string>();
        public readonly List<string> Challenges = new List<string>();      // earned this night
        public readonly List<string> NewChallenges = new List<string>();   // ...for the first time
        public readonly List<SpottedRecord> Detections = new List<SpottedRecord>();   // SR.10 debrief page
        public float Par;
        public float BestTime;
    }
}
