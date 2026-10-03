using UnityEngine;

namespace Vespertine.Stealth
{
    public struct VisionParams
    {
        public float HalfAngle;       // degrees
        public float NearRange;       // sees in any light
        public float FarRange;        // sees only lit targets
        public float LitThreshold;    // light needed in the far band
        public float NearRate;        // detection fill per second at the near edge
        public float FarRate;         // detection fill per second at the far band (fully lit)
        public bool LooksUp;
        public float Peripheral;      // radius seen at any angle (bumping into someone)

        public static VisionParams Default => new VisionParams
        {
            HalfAngle = 45f, NearRange = 5.5f, FarRange = 15f, LitThreshold = 0.35f,
            NearRate = 1.6f, FarRate = 0.9f, LooksUp = false, Peripheral = 1.4f
        };
    }

    /// <summary>Pure detection rules (unit-tested). See GAME_DESIGN §Stealth.</summary>
    public static class DetectionMath
    {
        public const float SuspiciousAt = 0.35f;
        public const float SpottedAt = 1f;
        public const float HighTarget = 2.5f;
        /// <summary>The light level at which the far band sees her: every guard's <see cref="VisionParams.LitThreshold"/>
        /// defaults to it, and the exposure rims are drawn at it.</summary>
        public const float ExposedAt = 0.35f;

        public enum Band { None, Peripheral, Near, Far }

        /// <summary>Which band (if any) the target falls in, ignoring line of sight.</summary>
        public static Band Classify(in VisionParams v, Vector3 eyeFeet, Vector3 forward, Vector3 targetFeet, float targetLight)
        {
            var to = targetFeet - eyeFeet;
            float dh = to.y;
            to.y = 0;
            float d = to.magnitude;
            if (d < 0.001f) return Band.Peripheral;
            if (d <= v.Peripheral && Mathf.Abs(dh) < 1.5f) return Band.Peripheral;
            forward.y = 0;
            float ang = Vector3.Angle(forward, to);
            if (ang > v.HalfAngle) return Band.None;
            bool high = dh > HighTarget && !v.LooksUp;
            if (d <= v.NearRange) return Band.Near;
            if (high) return Band.None;
            if (d <= v.FarRange && targetLight >= v.LitThreshold) return Band.Far;
            return Band.None;
        }

        /// <summary>Detection fill rate per second (0 = unseen). Assumes line of sight is clear.</summary>
        public static float Rate(in VisionParams v, Vector3 eyeFeet, Vector3 forward, Vector3 targetFeet, float targetLight, float multiplier = 1f)
        {
            var band = Classify(v, eyeFeet, forward, targetFeet, targetLight);
            float d = Vector3.Distance(new Vector3(eyeFeet.x, 0, eyeFeet.z), new Vector3(targetFeet.x, 0, targetFeet.z));
            float r;
            switch (band)
            {
                case Band.Peripheral: r = v.NearRate * 2.5f; break;
                case Band.Near:
                    // faster when closer; light still helps a little
                    r = v.NearRate * (1f + (1f - d / v.NearRange) * 1.5f) * (0.8f + 0.4f * Mathf.Clamp01(targetLight));
                    break;
                case Band.Far:
                    float lf = Mathf.Clamp01((targetLight - v.LitThreshold) / (1f - v.LitThreshold)) * 0.7f + 0.3f;
                    float df = 1f - Mathf.Clamp01((d - v.NearRange) / Mathf.Max(0.01f, v.FarRange - v.NearRange)) * 0.6f;
                    r = v.FarRate * lf * df;
                    break;
                default: return 0f;
            }
            return r * multiplier;
        }

        /// <summary>Cone-edge grace (SR.5, D138): the outer <see cref="GraceAngle"/>° of a cone and the outer
        /// <see cref="GraceDepth"/> m of the region that sees her fill at <see cref="GraceRate"/>, and the first
        /// <see cref="GraceOnset"/> s of a sighting don't count.</summary>
        public const float GraceAngle = 10f, GraceDepth = 1f, GraceRate = 0.5f, GraceOnset = 0.25f;

        /// <summary>
        /// Is the target in the grace fringe of a cone? Only the near and far bands have one (touch range has none). The
        /// fringe is the outer <see cref="GraceAngle"/>° on either side, and the outer <see cref="GraceDepth"/> m of the
        /// region that sees her: up to the far range when she is lit (and not above a guard who doesn't look up), else up
        /// to the near range. <paramref name="widen"/> scales both widths (Merciful). Pure.
        /// </summary>
        public static bool InGrace(in VisionParams v, Vector3 eyeFeet, Vector3 forward, Vector3 targetFeet, float targetLight, float widen = 1f)
        {
            var band = Classify(v, eyeFeet, forward, targetFeet, targetLight);
            if (band != Band.Near && band != Band.Far) return false;
            var to = targetFeet - eyeFeet;
            float dh = to.y;
            to.y = 0f; forward.y = 0f;
            if (Vector3.Angle(forward, to) > v.HalfAngle - GraceAngle * widen) return true;
            bool farSees = targetLight >= v.LitThreshold && !(dh > HighTarget && !v.LooksUp);
            return to.magnitude > (farSees ? v.FarRange : v.NearRange) - GraceDepth * widen;
        }

        /// <summary>
        /// The sighting onset: the first <see cref="GraceOnset"/> s of a sighting don't count. <paramref name="onset"/> is
        /// how long he has had her in his cone this sighting, <paramref name="unseen"/> how long since he last did. The
        /// allowance comes back only after he has lost her for the meter's <paramref name="hold"/>, so stepping in and out
        /// of an edge doesn't buy a fresh one each time. Returns the rate that counts. Pure.
        /// </summary>
        public static float Onset(ref float onset, ref float unseen, float rate, float dt, float hold = 1.2f)
        {
            if (rate <= 0f)
            {
                unseen += dt;
                if (unseen >= hold) onset = 0f;
                return 0f;
            }
            unseen = 0f;
            onset += dt;
            return onset > GraceOnset ? rate : 0f;
        }

        /// <summary>Advance a detection meter. Decays after <paramref name="sinceSeen"/> exceeds the hold time.</summary>
        public static float Step(float meter, float rate, float dt, float sinceSeen, float decay = 0.22f, float hold = 1.2f)
        {
            if (rate > 0f) return Mathf.Min(1f, meter + rate * dt);
            if (sinceSeen < hold) return meter;
            return Mathf.Max(0f, meter - decay * dt);
        }

        /// <summary>Gameplay light contribution of a single source at distance d (before occlusion).</summary>
        public static float LightFalloff(float intensity, float radius, float d)
        {
            if (d >= radius) return 0f;
            float t = d / radius;
            return intensity * (1f - t * t);
        }

        /// <summary>
        /// How far from a light's base (flat metres) it exposes a standing target: where ambient plus the light's falloff
        /// reaches <paramref name="threshold"/>. Light is measured at the chest (1 m up), as <c>LightSystem.LightAt</c> does,
        /// so <paramref name="sourceAboveChest"/> is the source height minus 1. Returns 0 if the light never exposes her
        /// on her own floor, and the light's full ground reach if the ambient alone does. Pure; the exposure rim and the
        /// Unity light's knee are both cut to it (D132).
        /// </summary>
        public static float ExposureRadius(float intensity, float radius, float sourceAboveChest, float ambient, float scale = 1f, float threshold = ExposedAt)
        {
            float dy = Mathf.Abs(sourceAboveChest);
            float need = threshold - ambient;
            float d;
            if (need <= 0f) d = radius;
            else
            {
                float peak = intensity * scale;
                if (peak <= need) return 0f;
                d = radius * Mathf.Sqrt(1f - need / peak);
            }
            return d <= dy ? 0f : Mathf.Sqrt(d * d - dy * dy);
        }

        /// <summary>Flat distance from a point to a circular sector (a cone's band seen from above): 0 inside. Pure.</summary>
        public static float DistanceToSector(Vector3 apex, Vector3 forward, float halfAngle, float radius, Vector3 p)
        {
            var v = p - apex; v.y = 0f;
            forward.y = 0f;
            float d = v.magnitude;
            if (d < 0.001f || forward.sqrMagnitude < 1e-6f) return 0f;
            float ang = Vector3.Angle(forward, v);
            if (ang <= halfAngle) return Mathf.Max(0f, d - radius);
            // outside the wedge: distance to the nearer straight edge
            float side = Mathf.Sign(Vector3.Cross(forward, v).y);
            var edge = Quaternion.Euler(0f, side * halfAngle, 0f) * forward.normalized;
            float along = Mathf.Clamp(Vector3.Dot(v, edge), 0f, radius);
            return (v - edge * along).magnitude;
        }

        /// <summary>What made a guard's meter fill, kept by <c>Npc.Perceive</c> for the Spotted caption.</summary>
        public struct SightCause
        {
            public Band Band;
            public float Distance, Light, NearRange, Threshold;
            public bool Searchlight, Smell;
            public bool Running, Carrying, Feeding, Wary, Hunting;
            public string Lamp;
        }

        /// <summary>The one-line Spotted caption (SR.10): the band that saw her, its numbers, and any modifier that
        /// sped it up. Pure.</summary>
        public static string Explain(in SightCause c)
        {
            string s;
            if (c.Smell) s = $"Smelled you: {c.Distance:0.0} m.";
            else if (c.Searchlight) s = "Searchlight: you stood in its pool.";
            else switch (c.Band)
            {
                case Band.Peripheral: s = $"Touch range: {c.Distance:0.0} m."; break;
                case Band.Near: s = $"Near band: {c.Distance:0.0} m (limit {c.NearRange:0.0} m). Darkness does not hide you this close."; break;
                case Band.Far:
                    s = (string.IsNullOrEmpty(c.Lamp) ? "Lit" : "Lit by the " + c.Lamp)
                        + $": light {c.Light:0.00} (exposed above {c.Threshold:0.00}), {c.Distance:0.0} m away."; break;
                default: s = "Seen."; break;
            }
            var mods = new System.Collections.Generic.List<string>(4);
            if (c.Running) mods.Add("running ×2");
            if (c.Feeding) mods.Add("feeding ×1.3");
            if (c.Carrying) mods.Add("carrying ×1.2");
            if (c.Hunting) mods.Add("hunting ×1.6");
            else if (c.Wary) mods.Add("wary ×1.25");
            if (mods.Count > 0 && !c.Smell)
            {
                var m = string.Join(", ", mods);
                s += " " + char.ToUpperInvariant(m[0]) + m.Substring(1) + ".";
            }
            return s;
        }

        /// <summary>Musket hit chance by distance and target light (GAME_DESIGN §Combat).</summary>
        public static float HitChance(float distance, float targetLight, float maxRange = 18f)
        {
            float t = Mathf.Clamp01(distance / maxRange);
            float c = Mathf.Lerp(0.7f, 0.25f, t);
            if (targetLight < 0.25f) c -= 0.3f;
            return Mathf.Clamp(c, 0.05f, 0.95f);
        }

        /// <summary>Noise heard? Walls between halve the effective radius.</summary>
        public static bool Hears(float distance, float radius, bool occluded, float hearingMul = 1f)
        {
            float r = radius * hearingMul * (occluded ? 0.55f : 1f);
            return distance <= r;
        }
    }
}
