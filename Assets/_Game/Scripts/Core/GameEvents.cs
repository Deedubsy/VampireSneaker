using System;
using UnityEngine;

namespace Vespertine.Core
{
    /// <summary>Global event bus for cross-module notifications (stats, objectives, dossier, audio, UI).</summary>
    public static class GameEvents
    {
        public static event Action<AI.Npc> NpcKilled;             // any death (drain, combat, accident)
        public static event Action<AI.Npc, bool> NpcFed;           // (victim, drained)
        public static event Action<AI.Npc> NpcDowned;              // dazed, enthralled or killed: out of the fight
        public static event Action<AI.Npc> NpcSpottedPlayer;       // full detection
        public static event Action<AI.Npc, string> NpcFoundEvidence;
        public static event Action<string> Interacted;             // entity id
        public static event Action<string> ZoneEntered;            // zone id (player)
        public static event Action<string> ObjectiveCompleted;
        public static event Action<string> ObjectiveFailed;
        public static event Action<string> AbilityUsed;            // ability id
        public static event Action<int> AlarmChanged;              // 0 calm,1 searching,2 alarm,3 lockdown
        public static event Action<string, string> Bark;           // speaker id, text
        public static event Action<string> Toast;                  // short notification
        public static event Action<AI.Npc> SpottedCaption;         // full detection, for the persistent UI's Spotted caption
        public static event Action<string> FlagSet;                // mission script flag
        public static event Action<Vector3, float, string> NoiseMade; // pos, radius, tag
        public static event Action<string> SecretFound;
        public static event Action<string> BodyDisposed;           // how: canal, hide, mist

        public static void RaiseNpcKilled(AI.Npc n) => NpcKilled?.Invoke(n);
        public static void RaiseNpcFed(AI.Npc n, bool drained) => NpcFed?.Invoke(n, drained);
        public static void RaiseNpcDowned(AI.Npc n) => NpcDowned?.Invoke(n);
        public static void RaiseSpotted(AI.Npc n) { NpcSpottedPlayer?.Invoke(n); SpottedCaption?.Invoke(n); }
        public static void RaiseEvidence(AI.Npc n, string kind) => NpcFoundEvidence?.Invoke(n, kind);
        public static void RaiseInteracted(string id) => Interacted?.Invoke(id);
        public static void RaiseZoneEntered(string id) => ZoneEntered?.Invoke(id);
        public static void RaiseObjectiveCompleted(string id) => ObjectiveCompleted?.Invoke(id);
        public static void RaiseObjectiveFailed(string id) => ObjectiveFailed?.Invoke(id);
        public static void RaiseAbilityUsed(string id) => AbilityUsed?.Invoke(id);
        public static void RaiseAlarm(int level) => AlarmChanged?.Invoke(level);
        public static void RaiseBark(string who, string text) => Bark?.Invoke(who, text);
        public static void RaiseToast(string text) => Toast?.Invoke(text);
        public static void RaiseFlag(string flag) => FlagSet?.Invoke(flag);
        public static void RaiseNoise(Vector3 p, float r, string tag) => NoiseMade?.Invoke(p, r, tag);
        public static void RaiseSecret(string id) => SecretFound?.Invoke(id);
        public static void RaiseBodyDisposed(string how) => BodyDisposed?.Invoke(how);

        /// <summary>Drop all subscribers belonging to mission objects (called on mission teardown).</summary>
        public static void ClearMissionSubscribers()
        {
            NpcKilled = null; NpcFed = null; NpcDowned = null; NpcSpottedPlayer = null; NpcFoundEvidence = null; Interacted = null;
            ZoneEntered = null; ObjectiveCompleted = null; ObjectiveFailed = null; AbilityUsed = null; AlarmChanged = null;
            FlagSet = null; NoiseMade = null; SecretFound = null; BodyDisposed = null;
            // Bark, Toast and SpottedCaption are owned by the persistent UI and stay subscribed.
        }
    }
}
