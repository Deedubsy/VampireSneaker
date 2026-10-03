using UnityEngine;
using Vespertine.Data;

namespace Vespertine.AI
{
    /// <summary>The numbers behind a hunter squad (M13): its nerve, what shakes it, and where each man walks.
    /// Pure; unit-tested.</summary>
    public static class SquadMath
    {
        // ---- nerve
        /// <summary>A squad's nerve at the start of the night. The Vigil are trained for her; the Watch and militia less so.
        /// The city's fear of her (Terror ahead of Rumour) has reached the men too.</summary>
        public static float StartNerve(Faction f, int terror, int rumour)
        {
            float b = f == Faction.Vigil ? 80f : f == Faction.Watch ? 60f : 50f;
            return b - 6f * Mathf.Clamp(terror - rumour, 0, 5);
        }

        public const float VaneBonus = 30f;       // Vane leads them in person
        public const float DownSeen = 25f;        // a man taken in front of them (dazed, enthralled or killed)
        public const float DownUnseen = 20f;      // a man who simply isn't there any more
        public const float FedSeen = 20f;         // watching her feed (on anyone)
        public const float DrainedExtra = 15f;    // ...to the death
        public const float TerrorExtra = 20f;     // Predator: Terror. The Vigil don't flee alone, but squads still feel it
        public const float DreadFeastExtra = 25f;
        public const float LightOut = 10f;        // a lamp near them goes dark
        public const float RoutNearby = 20f;      // another squad breaks and runs past them
        public const float Apex = 30f;            // Apex Hunt within reach
        public const float Dread = 12f;           // seeing her at Awakening 9+ (at most every DreadCooldown seconds)
        public const float DreadCooldown = 8f;
        public const float WitnessRange = 18f;

        /// <summary>What watching a feed costs a squad (0 if they didn't see it).</summary>
        public static float FedShock(bool seen, bool drained, bool terror, bool dreadFeast)
        {
            if (!seen) return 0f;
            float s = FedSeen;
            if (drained) s += DrainedExtra;
            if (terror) s += TerrorExtra;
            if (drained && dreadFeast) s += DreadFeastExtra;
            return s;
        }

        /// <summary>Nerve comes back slowly once nothing has happened for a while, but never past 60% of the start:
        /// a squad that has been shaken stays shaken.</summary>
        public const float RecoverDelay = 25f, RecoverRate = 1.5f, RecoverCap = 0.6f;
        public static float Recover(float nerve, float start, float sinceShock, float dt)
        {
            float cap = start * RecoverCap;
            if (sinceShock < RecoverDelay || nerve >= cap) return nerve;
            return Mathf.Min(cap, nerve + RecoverRate * dt);
        }

        public static bool Breaks(float nerve) => nerve <= 0f;

        /// <summary>How long a squad stands back to back after a shock before it hunts again.</summary>
        public const float RingTime = 14f;
        public const float RingRadius = 1.6f;

        // ---- formation
        /// <summary>A wedge: slot 0 leads, 1 and 2 flank behind him, 3 brings up the rear, then further ranks.
        /// x = metres to the leader's right, y = metres ahead of him (negative = behind).</summary>
        public static Vector2 SlotOffset(int slot)
        {
            switch (slot)
            {
                case 0: return Vector2.zero;
                case 1: return new Vector2(-1.5f, -1.3f);
                case 2: return new Vector2(1.5f, -1.3f);
                case 3: return new Vector2(0f, -2.6f);
                default:
                {
                    int rank = (slot - 2) / 2 + 2;      // 4,5 → rank 3; 6,7 → rank 4
                    return new Vector2(slot % 2 == 0 ? -1.5f : 1.5f, -1.3f * rank);
                }
            }
        }

        /// <summary>The rearguard: the last man in the wedge, who keeps turning to watch the street behind.</summary>
        public static bool IsRear(int slot, int size) => size >= 3 && slot == size - 1;
        public const float GlanceEvery = 7f, GlanceTime = 1.6f;

        /// <summary>Where member <paramref name="i"/> of <paramref name="n"/> stands in a ring, facing out.</summary>
        public static Vector3 RingPoint(Vector3 centre, int i, int n, float radius = RingRadius)
        {
            float a = (n <= 0 ? 0f : i / (float)n) * Mathf.PI * 2f;
            return centre + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * radius;
        }
    }
}
