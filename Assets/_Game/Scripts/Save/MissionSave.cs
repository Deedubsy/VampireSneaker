using System;
using System.Collections.Generic;
using UnityEngine;
using Vespertine.AI;
using Vespertine.Player;
using Vespertine.Progression;

namespace Vespertine.Save
{
    [Serializable]
    public class StainSave
    {
        public Vector3 P;
        public float Size;
        public bool Found, Huge;
        public int Seq = -1;      // blood-trail drop order
    }

    [Serializable]
    public class ObjectiveSave
    {
        public string Id;
        public int State;          // 0 active, 1 complete, 2 failed, 3 hidden-undiscovered
        public float Progress;
        public List<string> Done = new List<string>();
    }

    /// <summary>Mission-controller state: objectives, script rules, timers and stats.</summary>
    [Serializable]
    public class MissionState
    {
        public float Time;
        public List<ObjectiveSave> Objectives = new List<ObjectiveSave>();
        public List<int> FiredRules = new List<int>();
        public List<Counter> Timers = new List<Counter>();
        public List<string> Flags = new List<string>();
        public List<string> Discovered = new List<string>();
        public List<string> Secrets = new List<string>();
        public int Kills, Sips, Drains, TimesSpotted, Alarms, BodiesFound, Saves, Loads, Disposed;
        public List<string> FedTypes = new List<string>();
        public bool Escaping;
    }

    /// <summary>A full in-mission snapshot (quick save, autosave, manual slot).</summary>
    [Serializable]
    public class MissionSave
    {
        public const int CurrentVersion = 1;
        public int Version = CurrentVersion;
        public string MissionId, Label, Timestamp, Slot;
        public int MissionIndex;
        public int Difficulty;
        public float PlayTime;

        /// <summary>Campaign snapshot. Habits, lore and flags gathered during the mission roll back with the save.</summary>
        public string CampaignJson;
        /// <summary>The campaign as the mission began, so Restart after a load rolls back to the night's start (K2).
        /// Empty in saves made before it existed: those fall back to <see cref="CampaignJson"/>.</summary>
        public string CampaignAtStartJson;

        // AI
        public int Alarm;
        public bool Lockdown;
        public int BodiesFound;
        public List<NpcSave> Npcs = new List<NpcSave>();

        // world
        public PlayerSave Player;
        public List<EntityState> Entities = new List<EntityState>();
        public List<string> Groups = new List<string>();
        public List<StainSave> Stains = new List<StainSave>();
        public List<string> DisposedFx = new List<string>();

        public MissionState Mission = new MissionState();

        // camera
        public Vector3 CamPivot;
        public float CamYaw, CamDistance;
    }
}
