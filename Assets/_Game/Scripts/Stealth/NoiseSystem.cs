using UnityEngine;
using Vespertine.Core;

namespace Vespertine.Stealth
{
    public enum NoiseKind { Footstep, Rush, Body, Object, Voice, Scream, Gunshot, Bell, Lure, Heartbeat, Splash, Door }

    /// <summary>Dispatches noise events to NPCs and records them for UI rings.</summary>
    public class NoiseSystem
    {
        public System.Action<Vector3, float, NoiseKind> OnNoise;

        public void Emit(Vector3 pos, float radius, NoiseKind kind, object source = null)
        {
            if (radius <= 0f) return;
            // the orchestra's crescendo drowns everything but the bells (and a lure, which is meant to be heard)
            radius = Heard(radius, kind, Game.Mission != null && Game.Mission.Hushed);
            if (Game.AI != null) Game.AI.HearNoise(pos, radius, kind, source);
            OnNoise?.Invoke(pos, radius, kind);
            GameEvents.RaiseNoise(pos, radius, kind.ToString());
        }

        /// <summary>How far a noise carries, under the crescendo or not (pure; unit-tested).</summary>
        public static float Heard(float radius, NoiseKind kind, bool hushed)
        {
            if (!hushed || kind == NoiseKind.Bell || kind == NoiseKind.Lure) return radius;
            return radius * Mission.MissionController.HushFactor;
        }

        public static bool Occluded(Vector3 a, Vector3 b)
        {
            a.y += 1.2f; b.y += 1.2f;
            return Physics.Linecast(a, b, Layers.SightMask, QueryTriggerInteraction.Ignore);
        }
    }
}
