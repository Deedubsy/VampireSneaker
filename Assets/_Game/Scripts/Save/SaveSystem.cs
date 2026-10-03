using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Vespertine.Progression;

namespace Vespertine.Save
{
    /// <summary>Summary of a stored file, for menus.</summary>
    public class SaveInfo
    {
        public string Slot, Label, MissionId, Timestamp, Path;
        public int MissionIndex, Difficulty, Awakening;
        public float PlayTime;
        public DateTime Written;
    }

    /// <summary>
    /// File IO for campaigns (three profiles) and in-mission snapshots.
    /// Layout: persistentDataPath/profile_N/campaign.json and profile_N/m_{slot}.json.
    /// Mission slots: "quick0".."quick2" (rotating; "quick" from older saves still loads), "auto0".."auto2" (rotating),
    /// "s0".."s7" (manual).
    /// </summary>
    public static class SaveSystem
    {
        public const int Profiles = 3;
        public const int ManualSlots = 8;
        public const int AutoSlots = 3;
        public const int QuickSlots = 3;

        public static int Profile = 0;
        public static string LastError;

        /// <summary>Redirects every profile to another folder (the player smoke test uses a scratch one).</summary>
        public static string RootOverride;

        /// <summary>Editor only: while on (for the rest of the editor session), saves and settings go to the project's
        /// Temp/DevSaves instead of the player's persistent data folder, so dev harnesses never touch real profiles.</summary>
        public static bool DevSandbox
        {
#if UNITY_EDITOR
            get => UnityEditor.SessionState.GetBool("Vespertine.DevSandbox", false);
            set => UnityEditor.SessionState.SetBool("Vespertine.DevSandbox", value);
#else
            get => false;
            set { }
#endif
        }

        /// <summary>The folder profiles and settings live in when nothing overrides it.</summary>
        public static string BaseDir => DevSandbox ? Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/DevSaves")) : Application.persistentDataPath;

        static string RootDir => RootOverride ?? BaseDir;
        static string ProfileDir(int p) => Path.Combine(RootDir, "profile_" + p);
        static string CampaignPath(int p) => Path.Combine(ProfileDir(p), "campaign.json");
        static string MissionPath(int p, string slot) => Path.Combine(ProfileDir(p), "m_" + slot + ".json");

        // ------------------------------------------------------------------ atomic text IO
        static bool WriteText(string path, string text)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                var tmp = path + ".tmp";
                File.WriteAllText(tmp, text);
                if (File.Exists(path))
                {
                    var bak = path + ".bak";
                    if (File.Exists(bak)) File.Delete(bak);
                    File.Move(path, bak);
                }
                File.Move(tmp, path);
                LastError = null;
                return true;
            }
            catch (Exception e)
            {
                LastError = e.Message;
                Debug.LogWarning("Save failed: " + e.Message);
                return false;
            }
        }

        static string ReadText(string path)
        {
            try
            {
                if (File.Exists(path)) return File.ReadAllText(path);
                if (File.Exists(path + ".bak")) return File.ReadAllText(path + ".bak");
            }
            catch (Exception e) { LastError = e.Message; Debug.LogWarning("Load failed: " + e.Message); }
            return null;
        }

        // ------------------------------------------------------------------ campaign
        public static bool ProfileExists(int p) => File.Exists(CampaignPath(p)) || File.Exists(CampaignPath(p) + ".bak");

        public static bool SaveCampaign(CampaignState c, int profile = -1)
        {
            if (c == null) return false;
            if (profile < 0) profile = Profile;
            return WriteText(CampaignPath(profile), JsonUtility.ToJson(c, true));
        }

        public static CampaignState LoadCampaign(int profile)
        {
            var json = ReadText(CampaignPath(profile));
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                var c = JsonUtility.FromJson<CampaignState>(json);
                c?.ClampLoadout();
                return c;
            }
            catch (Exception e) { LastError = e.Message; return null; }
        }

        public static void DeleteProfile(int p)
        {
            try { if (Directory.Exists(ProfileDir(p))) Directory.Delete(ProfileDir(p), true); }
            catch (Exception e) { LastError = e.Message; }
        }

        public static int MostRecentProfile()
        {
            int best = -1; DateTime bt = DateTime.MinValue;
            for (int i = 0; i < Profiles; i++)
            {
                if (!ProfileExists(i)) continue;
                var t = File.GetLastWriteTime(CampaignPath(i));
                foreach (var s in ListMissionSaves(i)) if (s.Written > t) t = s.Written;
                if (t > bt) { bt = t; best = i; }
            }
            return best;
        }

        // ------------------------------------------------------------------ mission snapshots
        public static bool WriteMission(MissionSave s, string slot, int profile = -1)
        {
            if (s == null) return false;
            if (profile < 0) profile = Profile;
            s.Slot = slot;
            s.Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            return WriteText(MissionPath(profile, slot), JsonUtility.ToJson(s));
        }

        public static MissionSave ReadMission(string slot, int profile = -1)
        {
            if (profile < 0) profile = Profile;
            var json = ReadText(MissionPath(profile, slot));
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                var s = JsonUtility.FromJson<MissionSave>(json);
                if (s == null || s.Version > MissionSave.CurrentVersion) return null;
                return s;
            }
            catch (Exception e) { LastError = e.Message; return null; }
        }

        public static bool HasMission(string slot, int profile = -1) => File.Exists(MissionPath(profile < 0 ? Profile : profile, slot));

        public static void DeleteMission(string slot, int profile = -1)
        {
            var p = MissionPath(profile < 0 ? Profile : profile, slot);
            try { if (File.Exists(p)) File.Delete(p); if (File.Exists(p + ".bak")) File.Delete(p + ".bak"); }
            catch (Exception e) { LastError = e.Message; }
        }

        /// <summary>Deletes every in-mission snapshot (on mission completion or restart from the debrief).</summary>
        public static void ClearMissionSaves(int profile = -1)
        {
            if (profile < 0) profile = Profile;
            var dir = ProfileDir(profile);
            if (!Directory.Exists(dir)) return;
            try { foreach (var f in Directory.GetFiles(dir, "m_*")) File.Delete(f); }
            catch (Exception e) { LastError = e.Message; }
        }

        public static List<SaveInfo> ListMissionSaves(int profile = -1)
        {
            if (profile < 0) profile = Profile;
            var list = new List<SaveInfo>();
            var dir = ProfileDir(profile);
            if (!Directory.Exists(dir)) return list;
            foreach (var f in Directory.GetFiles(dir, "m_*.json"))
            {
                try
                {
                    var s = JsonUtility.FromJson<MissionSave>(File.ReadAllText(f));
                    if (s == null) continue;
                    int aw = 1;
                    if (!string.IsNullOrEmpty(s.CampaignJson))
                    {
                        var c = JsonUtility.FromJson<CampaignState>(s.CampaignJson);
                        if (c != null) aw = c.Awakening;
                    }
                    list.Add(new SaveInfo
                    {
                        Slot = s.Slot, Label = s.Label, MissionId = s.MissionId, Timestamp = s.Timestamp, Path = f,
                        MissionIndex = s.MissionIndex, Difficulty = s.Difficulty, PlayTime = s.PlayTime, Awakening = aw,
                        Written = File.GetLastWriteTime(f)
                    });
                }
                catch { /* skip unreadable */ }
            }
            list.Sort((a, b) => b.Written.CompareTo(a.Written));
            return list;
        }

        public static SaveInfo Latest(int profile = -1)
        {
            var l = ListMissionSaves(profile);
            return l.Count > 0 ? l[0] : null;
        }

        /// <summary>The rotating autosave slot to write next (oldest of auto0..auto2).</summary>
        public static string NextAutoSlot(int profile = -1) => Oldest("auto", AutoSlots, Written(profile));

        /// <summary>The rotating quicksave slot to write next (oldest of quick0..quick2), so a quicksave made in a lost
        /// position never overwrites the last two.</summary>
        public static string NextQuickSlot(int profile = -1) => Oldest("quick", QuickSlots, Written(profile));

        /// <summary>The newest quicksave (quick0..quick2, or an older save's "quick"), or null.</summary>
        public static string NewestQuickSlot(int profile = -1)
        {
            var w = Written(profile);
            string best = null; DateTime bt = DateTime.MinValue;
            for (int i = -1; i < QuickSlots; i++)
            {
                var slot = i < 0 ? "quick" : "quick" + i;
                var t = w(slot);
                if (t.HasValue && t.Value > bt) { bt = t.Value; best = slot; }
            }
            return best;
        }

        /// <summary>Of the slots prefix0..prefix(count-1): the first that is empty, else the one written longest ago.
        /// Pure: <paramref name="written"/> gives a slot's write time, or null if it is empty.</summary>
        public static string Oldest(string prefix, int count, Func<string, DateTime?> written)
        {
            string best = prefix + "0"; DateTime bt = DateTime.MaxValue;
            for (int i = 0; i < count; i++)
            {
                var slot = prefix + i;
                var t = written(slot);
                if (!t.HasValue) return slot;
                if (t.Value < bt) { bt = t.Value; best = slot; }
            }
            return best;
        }

        static Func<string, DateTime?> Written(int profile)
        {
            if (profile < 0) profile = Profile;
            return slot =>
            {
                var p = MissionPath(profile, slot);
                return File.Exists(p) ? File.GetLastWriteTime(p) : (DateTime?)null;
            };
        }

        /// <summary>The HUD's save-age line (QW8): how long ago, in mission time, she last saved or loaded.</summary>
        public static string Age(float seconds)
        {
            if (seconds < 0f) return "Not saved";
            if (seconds < 10f) return "Saved just now";
            if (seconds < 60f) return $"Saved {(int)seconds} s ago";
            int m = (int)(seconds / 60f);
            return m < 60 ? $"Saved {m} min ago" : $"Saved {m / 60} h {m % 60} min ago";
        }

        public static string SlotName(string slot)
        {
            if (slot != null && slot.StartsWith("quick")) return "Quick save";
            if (slot != null && slot.StartsWith("auto")) return "Autosave";
            if (slot != null && slot.StartsWith("s") && int.TryParse(slot.Substring(1), out var n)) return "Slot " + (n + 1);
            return slot;
        }
    }
}
