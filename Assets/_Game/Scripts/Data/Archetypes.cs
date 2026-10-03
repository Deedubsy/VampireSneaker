using System.Collections.Generic;
using UnityEngine;

namespace Vespertine.Data
{
    public enum Morale { Civilian, Low, Brave, Fearless }
    public enum Weapon { None, Cudgel, Musket, Pistol, Crossbow, Bite, Halberd, Censer }
    public enum BloodType { Common, Drunk, Fevered, Soldier, Priest, Occult, Notable, Animal }
    public enum Faction { None, Institute, Watch, Guild, Church, Vigil }

    [System.Flags]
    public enum ArchFlags
    {
        None = 0, LooksUp = 1, Lantern = 2, Relights = 4, HolyAura = 8, Ward = 16, Smell = 32,
        Armored = 64, Officer = 128, Flares = 256, Censer = 512, Silver = 1024, Quadruped = 2048, Undead = 4096
    }

    public class Archetype
    {
        public string Id, Name;
        public Faction Faction;
        public int HP;
        public float Fov, Near, Far, Hear = 1f;
        public Weapon Weapon;
        public Morale Morale;
        public ArchFlags Flags;
        public BloodType Blood;
        public float WalkSpeed = 1.6f, RunSpeed = 3.6f;
        public Color Coat, Accent;
        public string Hat; // silhouette key: stovepipe, cap, brim, hood, helm, mitre, beak, mortar, none
        public string Description;

        public bool Has(ArchFlags f) => (Flags & f) != 0;
        public bool Armed => Weapon != Weapon.None && Weapon != Weapon.Censer;
        public bool Ranged => Weapon == Weapon.Musket || Weapon == Weapon.Pistol || Weapon == Weapon.Crossbow;
    }

    public static class Archetypes
    {
        static readonly Dictionary<string, Archetype> _all = new Dictionary<string, Archetype>();
        public static IEnumerable<Archetype> All => _all.Values;

        static Color C(string h) => Core.Util.Hex(h);

        static void Add(string id, string name, Faction f, int hp, float fov, float near, float far, Weapon w, Morale m,
            ArchFlags flags, BloodType blood, string coat, string accent, string hat, string desc, float walk = 1.6f, float run = 3.6f, float hear = 1f)
        {
            _all[id] = new Archetype
            {
                Id = id, Name = name, Faction = f, HP = hp, Fov = fov, Near = near, Far = far, Weapon = w, Morale = m,
                Flags = flags, Blood = blood, Coat = C(coat), Accent = C(accent), Hat = hat, Description = desc,
                WalkSpeed = walk, RunSpeed = run, Hear = hear
            };
        }

        static Archetypes()
        {
            Add("civilian", "Citizen", Faction.None, 1, 100, 4, 10, Weapon.None, Morale.Civilian, 0, BloodType.Common, "#4a4038", "#8a7a60", "cap", "Screams and flees toward light or the Watch.");
            Add("drunk", "Drunk", Faction.None, 1, 80, 3, 7, Weapon.None, Morale.Civilian, 0, BloodType.Drunk, "#3c4a3a", "#7a6040", "cap", "Wanders. Slow to notice anything.", 1.1f, 2.6f, 0.7f);
            Add("sailor", "Sailor", Faction.None, 2, 90, 4, 9, Weapon.None, Morale.Low, 0, BloodType.Drunk, "#2a3a4a", "#c8b890", "cap", "Ashore and in drink. Brawls, shouts for the Watch, and staggers.", 1.2f, 3f, 0.75f);
            Add("beggar", "Fevered Beggar", Faction.None, 1, 90, 4, 9, Weapon.None, Morale.Civilian, 0, BloodType.Fevered, "#4a4438", "#5a5040", "hood", "Sits and coughs. Fevered blood sharpens the senses.", 1.2f, 2.8f);
            Add("orderly", "Orderly", Faction.Institute, 2, 90, 5, 13, Weapon.Cudgel, Morale.Low, ArchFlags.Lantern, BloodType.Common, "#c8c2b0", "#6a6050", "none", "Institute orderly. Slow; fights with a cudgel.", 1.5f, 3.4f);
            Add("watchman", "Watchman", Faction.Watch, 2, 90, 6, 15, Weapon.Musket, Morale.Low, ArchFlags.Lantern, BloodType.Soldier, "#26304a", "#b08a40", "stovepipe", "City Watch. Musket, slow reload.");
            Add("sergeant", "Watch Sergeant", Faction.Watch, 3, 100, 6, 16, Weapon.Pistol, Morale.Brave, ArchFlags.Lantern | ArchFlags.Officer, BloodType.Soldier, "#1e2840", "#d0a040", "stovepipe", "Leads searches.");
            Add("lamplighter", "Lamplighter", Faction.Guild, 1, 90, 5, 12, Weapon.None, Morale.Low, ArchFlags.Relights | ArchFlags.Lantern, BloodType.Common, "#5a4a2a", "#e0a040", "cap", "Walks a lamp circuit and relights dark lamps.");
            Add("priest", "Priest", Faction.Church, 2, 100, 6, 14, Weapon.None, Morale.Brave, ArchFlags.HolyAura, BloodType.Priest, "#18161a", "#e8d8a0", "mitre", "Holy aura: abilities fail and Ilse burns within 5 m. Rings bells.");
            Add("acolyte", "Acolyte", Faction.Church, 1, 90, 5, 12, Weapon.None, Morale.Low, ArchFlags.Lantern, BloodType.Priest, "#8a7a6a", "#e8d8a0", "hood", "Carries a censer light.");
            Add("guest", "Guest", Faction.None, 1, 100, 4, 11, Weapon.None, Morale.Civilian, 0, BloodType.Drunk, "#4a2038", "#d0b070", "brim", "A guest at the revel.", 1.3f, 3f, 0.8f);
            Add("servant", "Servant", Faction.None, 1, 100, 5, 12, Weapon.None, Morale.Civilian, 0, BloodType.Common, "#2a2a2e", "#e0e0e0", "none", "Moves between rooms. An ideal thrall.");
            Add("soldier", "Militia Soldier", Faction.Watch, 3, 90, 7, 17, Weapon.Musket, Morale.Brave, 0, BloodType.Soldier, "#5a1e22", "#c0c0c0", "cap", "Holds checkpoints.");
            Add("hunter", "Vigil Hunter", Faction.Vigil, 3, 90, 7, 18, Weapon.Crossbow, Morale.Fearless, ArchFlags.LooksUp | ArchFlags.Flares | ArchFlags.Silver, BloodType.Soldier, "#2c2a28", "#c8c8d0", "brim", "Looks up. Throws flares into suspicious darkness. Silver bolts.");
            Add("hound", "Vigil Hound", Faction.Vigil, 2, 360, 5, 5, Weapon.Bite, Morale.Fearless, ArchFlags.Smell | ArchFlags.Quadruped, BloodType.Animal, "#3a3230", "#6a5a50", "none", "Smells you within 5 m in any light. Follows blood trails.", 2.2f, 5.2f, 1.5f);
            Add("tracker", "Sister Hollin", Faction.Vigil, 5, 100, 8, 20, Weapon.Crossbow, Morale.Fearless, ArchFlags.LooksUp | ArchFlags.Smell | ArchFlags.Ward | ArchFlags.Officer | ArchFlags.Silver, BloodType.Notable, "#3a2a22", "#d8d0c0", "brim", "The Vigil's tracker. Starts wary.", 1.8f, 4f, 1.3f);
            Add("alchemist", "Alchemist", Faction.Institute, 2, 90, 6, 14, Weapon.Censer, Morale.Low, ArchFlags.Censer, BloodType.Occult, "#3a4a3a", "#a0c080", "beak", "Garlic smoke (3 m) blocks mist, Shadow Dash and Dominion.");
            Add("inquisitor", "Inquisitor", Faction.Vigil, 4, 100, 7, 18, Weapon.Pistol, Morale.Fearless, ArchFlags.LooksUp | ArchFlags.Officer | ArchFlags.Ward | ArchFlags.Silver, BloodType.Priest, "#141418", "#c03040", "brim", "Examines bodies. Spots thralls.");
            Add("bulwark", "Bulwark", Faction.Vigil, 6, 80, 6, 14, Weapon.Halberd, Morale.Fearless, ArchFlags.Armored | ArchFlags.Lantern, BloodType.Soldier, "#4a4a50", "#9a9aa0", "helm", "Armored: cannot be fed on from the front.", 1.3f, 2.8f);
            Add("sentry", "Rooftop Sentry", Faction.Watch, 2, 100, 7, 18, Weapon.Musket, Morale.Brave, ArchFlags.LooksUp | ArchFlags.Lantern, BloodType.Soldier, "#26304a", "#b08a40", "stovepipe", "Posted on rooftops. Looks up.");
            Add("notable", "Notable", Faction.None, 2, 90, 6, 14, Weapon.None, Morale.Civilian, 0, BloodType.Notable, "#2a1a30", "#d0a060", "brim", "A named target.");
            Add("scholar", "Institute Scholar", Faction.Institute, 1, 90, 4, 11, Weapon.None, Morale.Civilian, 0, BloodType.Occult, "#4a4a42", "#c8c0a0", "mortar", "Takes notes. Screams for the orderlies.", 1.4f, 3.2f);
            Add("engineer", "Generator Engineer", Faction.Institute, 2, 90, 5, 13, Weapon.Cudgel, Morale.Low, ArchFlags.Relights | ArchFlags.Lantern, BloodType.Common, "#3a3228", "#d09040", "cap", "Tends the sunstone generator. Restores the current when the lamps fail.");
            Add("worker", "Gasworker", Faction.Guild, 2, 90, 4, 11, Weapon.None, Morale.Civilian, 0, BloodType.Common, "#3a3a34", "#8a6a3a", "cap", "Night shift at the gasworks. Shouts for the foreman and runs; frightened badly enough, runs off the site for good.", 1.5f, 3.4f, 0.8f);
            Add("fledgling", "Fledgling", Faction.None, 3, 60, 3, 6, Weapon.None, Morale.Civilian, ArchFlags.Undead, BloodType.Animal, "#6a6a70", "#8a2028", "none", "A starved experimental subject. Cannot climb; sunstone kills it.", 1.4f, 3.3f, 0.8f);
            Add("saule", "Dr. Emeric Saule", Faction.Institute, 5, 100, 7, 16, Weapon.Pistol, Morale.Low, ArchFlags.Ward | ArchFlags.Censer | ArchFlags.Lantern | ArchFlags.Officer, BloodType.Occult, "#d8d4c8", "#2a2a30", "none", "The architect of the eternal vitae. Warded, with a censer and a pistol; lives inside his sunstone ring.", 1.3f, 3f);
            Add("vane", "Inquisitor-Captain Vane", Faction.Vigil, 6, 110, 8, 22, Weapon.Pistol, Morale.Fearless, ArchFlags.LooksUp | ArchFlags.Officer | ArchFlags.Ward | ArchFlags.HolyAura | ArchFlags.Silver, BloodType.Notable, "#0e0e12", "#e0e0f0", "brim", "The Pale Vigil's commander.");
        }

        public static Archetype Get(string id)
        {
            if (id != null && _all.TryGetValue(id, out var a)) return a;
            Debug.LogWarning($"Unknown archetype '{id}', using civilian");
            return _all["civilian"];
        }

        public static bool Exists(string id) => id != null && _all.ContainsKey(id);
    }

    public class BloodQuality
    {
        public BloodType Type;
        public float Amount;
        public string Humour;      // null = none
        public string HumourName, HumourDesc;
    }

    public static class BloodQualities
    {
        public static BloodQuality Get(BloodType t)
        {
            switch (t)
            {
                case BloodType.Drunk: return new BloodQuality { Type = t, Amount = 25, Humour = "languid", HumourName = "Languid", HumourDesc = "-15% noise, -10% speed" };
                case BloodType.Fevered: return new BloodQuality { Type = t, Amount = 15, Humour = "fever", HumourName = "Fever-sight", HumourDesc = "Blood Sense is free for 60 s" };
                case BloodType.Soldier: return new BloodQuality { Type = t, Amount = 40, Humour = "iron", HumourName = "Iron", HumourDesc = "+25% max health until next feed" };
                case BloodType.Priest: return new BloodQuality { Type = t, Amount = 30, Humour = "scalding", HumourName = "Scalding", HumourDesc = "-10 HP now; Sanguis abilities half cost for 60 s" };
                case BloodType.Occult: return new BloodQuality { Type = t, Amount = 30, Humour = "lucid", HumourName = "Lucid", HumourDesc = "Dominion abilities half cost for 60 s" };
                case BloodType.Notable: return new BloodQuality { Type = t, Amount = 60, Humour = null };
                case BloodType.Animal: return new BloodQuality { Type = t, Amount = 10, Humour = null };
                default: return new BloodQuality { Type = BloodType.Common, Amount = 30, Humour = null };
            }
        }
    }
}
