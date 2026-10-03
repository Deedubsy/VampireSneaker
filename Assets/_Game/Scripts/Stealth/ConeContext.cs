using System.Collections.Generic;
using UnityEngine;

namespace Vespertine.Stealth
{
    /// <summary>
    /// Which guards' cones show in normal play, with no key held (QW15, SR.6). Pure: the cone renderer measures each
    /// guard and this ranks them.
    /// <list type="bullet">
    /// <item>A guard qualifies if he is aware of her within <see cref="AwareRange"/>, or his detecting region (the
    /// near sector, or the whole cone while she is lit) is within <see cref="NearDanger"/> of her or of where she will
    /// be in 1.5 s, or his detecting region will reach her within <see cref="ContactHorizon"/> s at current velocities
    /// and turn (<see cref="TimeToContact"/>), or he qualified within the last <see cref="Hold"/> seconds.</item>
    /// <item>At most <see cref="Budget"/> full cones: aware guards first and always, then the soonest to reach her, then
    /// the nearest.</item>
    /// <item>Unaware guards past the budget, and unaware guards close enough to step into the near sector
    /// (<c>NearRange + 3 m</c>), still draw their near sector and touch circle alone.</item>
    /// </list>
    /// </summary>
    public static class ConeContext
    {
        public const float NearDanger = 4f, AwareRange = 25f, Hold = 1.5f, FadeTime = 0.25f, LookAhead = 1.5f, NearOnlyExtra = 3f;
        public const int Budget = 4;

        public enum Show { None, NearOnly, Full }

        public struct Candidate
        {
            /// <summary>Flat distance from Ilse (or her projected point) to his detecting region; 0 inside it.</summary>
            public float Gap;
            /// <summary>Flat distance from Ilse to the guard.</summary>
            public float Distance;
            /// <summary>His meter on her is above 0, or he is Suspicious or worse.</summary>
            public bool Aware;
            /// <summary>He qualified within the last <see cref="Hold"/> seconds.</summary>
            public bool Held;
            /// <summary>Ilse is within his near range + <see cref="NearOnlyExtra"/>.</summary>
            public bool NearClose;
            /// <summary>His detecting region reaches her within <see cref="ContactHorizon"/> s, in
            /// <see cref="ContactIn"/> seconds.</summary>
            public bool Soon;
            public float ContactIn;
        }

        public static bool Qualifies(in Candidate c, float nearDanger = NearDanger) =>
            c.Held || c.Soon || c.Gap <= nearDanger || c.Aware && c.Distance <= AwareRange;

        /// <summary>How far ahead the time-to-contact rule looks (SR.6), how finely, and how long a guard's current turn is
        /// assumed to go on.</summary>
        public const float ContactHorizon = 2.5f, ContactStep = 0.1f, TurnAhead = 1f;

        /// <summary>
        /// Seconds until a sector (apex <paramref name="pos"/>, facing <paramref name="fwd"/>, half-angle
        /// <paramref name="half"/> degrees, radius <paramref name="range"/>) holds her, with him moving at
        /// <paramref name="vel"/> and turning at <paramref name="yawRate"/> degrees a second (for at most
        /// <see cref="TurnAhead"/> s) and her moving at <paramref name="herVel"/>. Infinity if not within
        /// <paramref name="horizon"/>. Walls are not considered: this decides what to show, not what he sees.
        /// </summary>
        public static float TimeToContact(Vector3 pos, Vector3 vel, Vector3 fwd, float yawRate, float half, float range,
                                          Vector3 her, Vector3 herVel, float horizon = ContactHorizon)
        {
            vel.y = 0f; herVel.y = 0f;
            int steps = Mathf.CeilToInt(horizon / ContactStep);
            for (int i = 0; i <= steps; i++)
            {
                float t = Mathf.Min(i * ContactStep, horizon);
                var f = Quaternion.Euler(0f, yawRate * Mathf.Min(t, TurnAhead), 0f) * fwd;
                if (DetectionMath.DistanceToSector(pos + vel * t, f, half, range, her + herVel * t) <= 0f) return t;
            }
            return float.PositiveInfinity;
        }

        public const int MaxPins = 3;

        /// <summary>Middle-click on a guard (SR.12): unpins him if he is pinned, otherwise pins him, dropping the oldest pin
        /// past <paramref name="max"/>. True if he is pinned now.</summary>
        public static bool TogglePin<T>(List<T> pins, T who, int max = MaxPins)
        {
            if (pins.Remove(who)) return false;
            pins.Add(who);
            while (pins.Count > max) pins.RemoveAt(0);
            return true;
        }

        /// <summary>
        /// Which shown cone owns an overlap (SR.5): of the guards whose meter is rising on her, the fullest (the first on
        /// a tie). -1 when none is rising; then every cone draws at full strength.
        /// </summary>
        public static int Owner(bool[] rising, float[] meter, int count)
        {
            int best = -1;
            for (int i = 0; i < count; i++)
                if (rising[i] && (best < 0 || meter[i] > meter[best])) best = i;
            return best;
        }

        /// <summary>Strength of a cone that doesn't own the overlap while another does.</summary>
        public const float Yield = 0.5f;

        /// <summary>Can his far band reach her height? Not when she is high above a guard who doesn't look up: then only
        /// his near sector counts toward showing his cone (the rule in <see cref="DetectionMath.Classify"/>).</summary>
        public static bool FarReaches(float herAbove, bool looksUp) => looksUp || herAbove <= DetectionMath.HighTarget;

        /// <summary>Fills <paramref name="result"/> (same length as <paramref name="cands"/>) with what each guard shows.</summary>
        public static void Pick(IReadOnlyList<Candidate> cands, Show[] result, int budget = Budget, float nearDanger = NearDanger)
        {
            var order = new List<int>(cands.Count);
            for (int i = 0; i < cands.Count; i++)
            {
                var c = cands[i];
                bool q = Qualifies(c, nearDanger);
                result[i] = !q && c.NearClose && !c.Aware ? Show.NearOnly : Show.None;
                if (q) order.Add(i);
            }
            order.Sort((a, b) =>
            {
                if (cands[a].Aware != cands[b].Aware) return cands[a].Aware ? -1 : 1;
                if (cands[a].Soon != cands[b].Soon) return cands[a].Soon ? -1 : 1;
                if (cands[a].Soon && cands[a].ContactIn != cands[b].ContactIn) return cands[a].ContactIn.CompareTo(cands[b].ContactIn);
                int g = cands[a].Gap.CompareTo(cands[b].Gap);
                return g != 0 ? g : cands[a].Distance.CompareTo(cands[b].Distance);
            });
            int full = 0;
            foreach (int i in order)
            {
                if (cands[i].Aware || full < budget) { result[i] = Show.Full; full++; }
                else result[i] = Show.NearOnly;
            }
        }
    }
}
