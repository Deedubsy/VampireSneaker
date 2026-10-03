using System.Collections.Generic;

namespace Vespertine.Data
{
    public enum NodeType { Passive, Active, Mod, Capstone, Command }
    public enum Tree { Predator, Shade, Dominion, Sanguis }
    public enum Targeting { None, Self, Point, Npc, Light, Corpse, Toggle, Hold }

    public class SkillNode
    {
        public string Id, Name, Effect;
        public Tree Tree;
        public int Tier;               // 0 = story-granted, 1..4
        public NodeType Type;
        public int Cost;               // marks
        public string Parent;          // required node (same tree)
        public string Cross;           // cross-tree requirement
        public bool IsAbility => Type == NodeType.Active || Type == NodeType.Capstone || Type == NodeType.Command;
        public int RequiredAwakening => Tier <= 1 ? 1 : Tier == 2 ? 3 : Tier == 3 ? 5 : 8;
    }

    public class AbilityDef
    {
        public string Id, Name, Hint;
        public float Cost;             // blood (upfront)
        public float Upkeep;           // blood / s (toggles, holds)
        public float Range;
        public float Cooldown;
        public Targeting Targeting;
        public float Radius;           // area preview
        public bool Holy;              // blocked inside holy aura (Shade/Dominion/Sanguis)
        public bool Dominion;          // blocked by Ward / animals
        public bool ThrallCommand;
    }

    public static class Skills
    {
        public static readonly List<SkillNode> Nodes = new List<SkillNode>();
        static readonly Dictionary<string, SkillNode> ById = new Dictionary<string, SkillNode>();
        public static readonly Dictionary<string, AbilityDef> Abilities = new Dictionary<string, AbilityDef>();

        static void N(string id, Tree t, int tier, NodeType type, int cost, string name, string effect, string parent = null, string cross = null)
        {
            var n = new SkillNode { Id = id, Tree = t, Tier = tier, Type = type, Cost = cost, Name = name, Effect = effect, Parent = parent, Cross = cross };
            Nodes.Add(n);
            ById[id] = n;
        }

        static void A(string id, string name, float cost, float range, Targeting tg, string hint, float upkeep = 0, float cd = 0, float radius = 0, bool holy = true, bool dominion = false, bool command = false)
        {
            Abilities[id] = new AbilityDef { Id = id, Name = name, Cost = cost, Range = range, Targeting = tg, Hint = hint, Upkeep = upkeep, Cooldown = cd, Radius = radius, Holy = holy, Dominion = dominion, ThrallCommand = command };
        }

        static Skills()
        {
            // PREDATOR
            N("predator.stalker", Tree.Predator, 1, NodeType.Passive, 1, "Stalker", "Gliding within 4 m behind a human gives them no detection. Feeding from behind is 30% faster.");
            N("predator.pounce", Tree.Predator, 1, NodeType.Active, 2, "Pounce", "Leap onto a human within 7 m (12 m from 3 m+ height) and pin them: starts a feed instantly. Noise 4 m.");
            N("predator.pounce_silent", Tree.Predator, 2, NodeType.Mod, 1, "Soft Landing", "Pounce makes no noise.", "predator.pounce");
            N("predator.gorge", Tree.Predator, 2, NodeType.Passive, 1, "Gorge", "Feeding is 40% faster. Drain gives +25% blood.", "predator.stalker");
            N("predator.scent", Tree.Predator, 2, NodeType.Passive, 1, "Scent of Blood", "Dazed and wounded humans, hounds and blood trails show through walls within 30 m.", "predator.stalker");
            N("predator.rend", Tree.Predator, 3, NodeType.Active, 2, "Rend", "Instantly kill one alerted or facing human in melee. Loud (8 m). No blood.", "predator.pounce");
            N("predator.terror", Tree.Predator, 3, NodeType.Passive, 2, "Terror", "Witnesses of a kill or feed panic and flee instead of raising the alarm (not Vigil).", "predator.gorge");
            N("predator.herd", Tree.Predator, 3, NodeType.Mod, 1, "Herding", "Panicked humans flee away from you.", "predator.terror");
            N("predator.bound", Tree.Predator, 3, NodeType.Passive, 1, "Bound", "Roof-leap reaches 3 cells. Climbing speed doubled.", "predator.pounce");
            N("predator.apex", Tree.Predator, 4, NodeType.Capstone, 3, "Apex Hunt", "8 s: everyone else moves at 35% speed. Feeds are instant and every kill refunds 15 blood.", "predator.rend");
            N("predator.dread_feast", Tree.Predator, 4, NodeType.Passive, 2, "Dread Feast", "Draining in view of others makes them panic for 20 s and reveals their heartbeats.", "predator.terror", "sanguis.sense");
            // SHADE
            N("shade.smother", Tree.Shade, 1, NodeType.Active, 2, "Smother", "Silently extinguish a light within 16 m.");
            N("shade.smother_lingering", Tree.Shade, 2, NodeType.Mod, 1, "Lingering Dark", "Smothered lights cannot be relit for 60 s.", "shade.smother");
            N("shade.smother_chain", Tree.Shade, 3, NodeType.Mod, 1, "Black Main", "Smothering a gas lamp puts out its whole gas main.", "shade.smother_lingering");
            N("shade.umbral", Tree.Shade, 1, NodeType.Passive, 2, "Umbral Step", "A third Shadow Dash charge. After a dash that ends in darkness you stay unseen for 1 s.");
            N("shade.dash_bars", Tree.Shade, 2, NodeType.Passive, 1, "Between Bars", "Shadow Dash carries you through bars.", "shade.umbral");
            N("shade.nightblood", Tree.Shade, 2, NodeType.Passive, 1, "Nightblood", "In darkness, regenerate 1 HP/s without spending blood.", "shade.smother");
            N("shade.gloom", Tree.Shade, 2, NodeType.Active, 2, "Gloom", "A 5 m sphere of darkness for 15 s within 18 m. Cones cannot pierce it.", "shade.umbral");
            N("shade.mist", Tree.Shade, 3, NodeType.Active, 2, "Mist Form", "Become mist: pass bars, vents and gaps under doors. Slow. In light you look like strange fog.", "shade.gloom");
            N("shade.eclipse", Tree.Shade, 4, NodeType.Capstone, 3, "Eclipse", "Every light within 25 m dies for 30 s. Humans inside are blinded for 8 s.", "shade.mist");
            N("shade.shroud", Tree.Shade, 4, NodeType.Passive, 2, "Shroud of Sleep", "Humans inside Gloom fall asleep after 3 s.", "shade.gloom", "dominion.mesmerize");
            // DOMINION
            N("dominion.beckon", Tree.Dominion, 0, NodeType.Active, 0, "Beckon", "Whisper to a human within 20 m: they walk alone to a point you choose and look around.");
            N("dominion.beckon_mimic", Tree.Dominion, 1, NodeType.Mod, 1, "Familiar Voice", "Beckoned humans do not become Wary afterwards.", "dominion.beckon");
            N("dominion.mesmerize", Tree.Dominion, 1, NodeType.Active, 2, "Mesmerize", "A human within 10 m freezes for 10 s with their cone closed. Feed from any side.");
            N("dominion.mesmerize_forget", Tree.Dominion, 2, NodeType.Mod, 1, "Lethe", "Mesmerised and sipped humans forget: no report, no Wary.", "dominion.mesmerize");
            N("dominion.thrall", Tree.Dominion, 2, NodeType.Active, 2, "Enthrall", "Make a mesmerised, dazed or unaware human your thrall.", "dominion.mesmerize");
            N("dominion.thrall_second", Tree.Dominion, 3, NodeType.Mod, 1, "Second Puppet", "Up to 2 thralls.", "dominion.thrall");
            N("dominion.false_orders", Tree.Dominion, 3, NodeType.Command, 1, "False Orders", "Your thrall sends a guard of their faction to a point to hold for 30 s.", "dominion.thrall");
            N("dominion.puppet_strike", Tree.Dominion, 3, NodeType.Command, 1, "Puppet Strike", "Your thrall kills an adjacent human. Witnesses blame the thrall.", "dominion.thrall");
            N("dominion.court", Tree.Dominion, 4, NodeType.Capstone, 3, "Court of Night", "Every human within 10 m is mesmerised for 8 s. Up to 3 thralls.", "dominion.thrall_second");
            N("dominion.living_lie", Tree.Dominion, 4, NodeType.Passive, 2, "Living Lie", "Corpse puppets deliver False Orders and pass officer checks.", "dominion.false_orders", "sanguis.puppet");
            // SANGUIS
            N("sanguis.sense", Tree.Sanguis, 1, NodeType.Active, 2, "Blood Sense", "Hold: see every heartbeat within 30 m through walls, with their state and path.");
            N("sanguis.sense_free", Tree.Sanguis, 2, NodeType.Mod, 1, "Hunter's Pulse", "Blood Sense is free while standing still.", "sanguis.sense");
            N("sanguis.mend", Tree.Sanguis, 1, NodeType.Active, 1, "Bloodmend", "Instantly heal 50% HP anywhere.");
            N("sanguis.clean", Tree.Sanguis, 1, NodeType.Passive, 1, "Clean Feeder", "Drain leaves no stain; victims do not bleed. Sipped victims sleep 60% longer.");
            N("sanguis.snare", Tree.Sanguis, 2, NodeType.Active, 2, "Blood Snare", "Place a rune within 8 m (max 2). The first human to cross collapses Dazed.", "sanguis.sense");
            N("sanguis.hemorrhage", Tree.Sanguis, 2, NodeType.Active, 2, "Hemorrhage", "Silent kill within 10 m that leaves a huge, horrifying blood pool.", "sanguis.mend");
            N("sanguis.puppet", Tree.Sanguis, 3, NodeType.Active, 2, "Corpse Puppet", "A corpse within 6 m rises and resumes its routine for 40 s.", "sanguis.hemorrhage");
            N("sanguis.false_trail", Tree.Sanguis, 3, NodeType.Mod, 1, "False Trail", "Blood Snare runes reek of blood: hounds and trackers within 20 m come to sniff them out.", "sanguis.snare");
            N("sanguis.silverblood", Tree.Sanguis, 3, NodeType.Passive, 1, "Silverblood", "Silver and holy damage halved. Holy auras no longer block Sanguis.", "sanguis.mend");
            N("sanguis.communion", Tree.Sanguis, 4, NodeType.Capstone, 3, "Red Communion", "Drain every corpse and dazed human within 12 m at once.", "sanguis.puppet");
            N("sanguis.vessel", Tree.Sanguis, 4, NodeType.Passive, 2, "Vessel", "+50 max blood; overfeeding converts to bonus HP (up to +30).", "sanguis.snare", "predator.gorge");

            A("predator.pounce", "Pounce", 15, 7, Targeting.Npc, "Leap onto a human and feed.", cd: 2, holy: false);
            A("predator.rend", "Rend", 20, 2.2f, Targeting.Npc, "Kill an aware human in melee. Loud.", holy: false);
            A("predator.apex", "Apex Hunt", 45, 0, Targeting.Self, "Time slows for everyone else for 8 s.", cd: 60, holy: false);
            A("shade.smother", "Smother", 8, 16, Targeting.Light, "Silently extinguish a light.");
            A("shade.gloom", "Gloom", 18, 18, Targeting.Point, "A sphere of darkness, 15 s.", radius: 5, cd: 5);
            A("shade.mist", "Mist Form", 6, 0, Targeting.Toggle, "Become mist. Drains 3 blood/s.", upkeep: 3);
            A("shade.eclipse", "Eclipse", 45, 0, Targeting.Self, "Kill every light within 25 m.", radius: 25, cd: 60);
            A("dominion.beckon", "Beckon", 6, 20, Targeting.Npc, "Call a human to a point near them.", dominion: true);
            A("dominion.mesmerize", "Mesmerize", 12, 10, Targeting.Npc, "Freeze a human for 10 s.", dominion: true, cd: 1);
            A("dominion.thrall", "Enthrall", 25, 2.5f, Targeting.Npc, "Take a helpless or unaware human as thrall.", dominion: true);
            A("dominion.false_orders", "False Orders", 0, 0, Targeting.Npc, "Thrall: send a guard away.", dominion: true, command: true);
            A("dominion.puppet_strike", "Puppet Strike", 0, 0, Targeting.Npc, "Thrall: kill an adjacent human.", dominion: true, command: true);
            A("dominion.court", "Court of Night", 40, 0, Targeting.Self, "Mesmerise everyone within 10 m.", radius: 10, dominion: true, cd: 60);
            A("sanguis.sense", "Blood Sense", 0, 30, Targeting.Hold, "Hold to see heartbeats through walls.", upkeep: 1, holy: false);
            A("sanguis.mend", "Bloodmend", 20, 0, Targeting.Self, "Heal 50% HP.", cd: 6);
            A("sanguis.snare", "Blood Snare", 15, 8, Targeting.Point, "A rune that dazes the first human to cross it.", radius: 0.9f);
            A("sanguis.hemorrhage", "Hemorrhage", 30, 10, Targeting.Npc, "Silent kill. Huge evidence.");
            A("sanguis.puppet", "Corpse Puppet", 18, 6, Targeting.Corpse, "A corpse resumes its routine for 40 s.");
            A("sanguis.communion", "Red Communion", 0, 12, Targeting.Self, "Drain every body within 12 m.", radius: 12, cd: 60);
        }

        public static SkillNode Get(string id) => id != null && ById.TryGetValue(id, out var n) ? n : null;
        public static AbilityDef Ability(string id) => id != null && Abilities.TryGetValue(id, out var a) ? a : null;
        public static bool IsAbility(string id) => Abilities.ContainsKey(id);
    }

    public class MissionInfo
    {
        public string Id, Title, Subtitle, Place;
        public int Act;
        public string Pitch;
        public bool Built; // map file exists
        public float Par;  // seconds: the "Before the Bell" challenge (estimates until a full-campaign playtest, X6)
    }

    public static class Missions
    {
        public static readonly List<MissionInfo> All = new List<MissionInfo>
        {
            new MissionInfo { Id = "m01", Par = 6 * 60, Act = 1, Title = "The Drowned Ward", Subtitle = "Escape", Place = "Coldwater Institute, lower ward", Pitch = "Wake among the dead. Get out." },
            new MissionInfo { Id = "m02", Par = 8 * 60, Act = 1, Title = "Lantern Street", Subtitle = "Reach", Place = "Weirside slum", Pitch = "Find Tobias. You cannot enter uninvited." },
            new MissionInfo { Id = "m03", Par = 10 * 60, Act = 1, Title = "The Fishmarket Hunger", Subtitle = "Theft", Place = "Ostmere docks", Pitch = "Steal the Institute's shipping manifest." },
            new MissionInfo { Id = "m04", Par = 12 * 60, Act = 1, Title = "The Bells of Saint Corvin", Subtitle = "Sabotage", Place = "Cathedral Quarter", Pitch = "Silence the bells. Steal the registry." },
            new MissionInfo { Id = "m05", Par = 12 * 60, Act = 2, Title = "The Guildhall of Lamps", Subtitle = "Kidnap", Place = "Lamplighters' Guildhall", Pitch = "Carry Guildmaster Penrose to the boat, alive." },
            new MissionInfo { Id = "m06", Par = 14 * 60, Act = 2, Title = "Masquerade at Ashcombe House", Subtitle = "Intelligence", Place = "Ashcombe estate", Pitch = "Listen. Learn the Council's names." },
            new MissionInfo { Id = "m07", Par = 12 * 60, Act = 2, Title = "The Toll Bridges", Subtitle = "Traverse", Place = "River district", Pitch = "Cross three fortified bridges under curfew." },
            new MissionInfo { Id = "m08", Par = 14 * 60, Act = 2, Title = "Hollin's Hunt", Subtitle = "Duel", Place = "Marrowmere fens", Pitch = "The tracker hunts you. Hunt her back." },
            new MissionInfo { Id = "m09", Par = 15 * 60, Act = 3, Title = "Coldwater, Above", Subtitle = "Rescue", Place = "Institute upper wards", Pitch = "Free Clement and the fledglings." },
            new MissionInfo { Id = "m10", Par = 15 * 60, Act = 3, Title = "The Gasworks", Subtitle = "Sabotage", Place = "Gasworks", Pitch = "Put out the city's lights." },
            new MissionInfo { Id = "m11", Par = 14 * 60, Act = 3, Title = "The Opera of Lanterns", Subtitle = "Assassination", Place = "Grand Opera House", Pitch = "Three Council members. Ninety seconds." },
            new MissionInfo { Id = "m12", Par = 16 * 60, Act = 3, Title = "Vane's Bastion", Subtitle = "Infiltration", Place = "Vigil island fortress", Pitch = "Steal the Dossier. Decide Vane's fate." },
            new MissionInfo { Id = "m13", Par = 18 * 60, Act = 4, Title = "The Long Night", Subtitle = "Hunt", Place = "Three districts under curfew", Pitch = "Break the Vigil cordon before dawn." },
            new MissionInfo { Id = "m14", Par = 14 * 60, Act = 4, Title = "The Abbess Beneath", Subtitle = "Finale", Place = "Cathedral undercroft", Pitch = "The Abbess waits. Dawn is coming." },
        };

        public static MissionInfo Get(string id) => All.Find(m => m.Id == id);
        public static int IndexOf(string id) => All.FindIndex(m => m.Id == id);
        public static string ActName(int act) => act == 1 ? "Act I — Prey" : act == 2 ? "Act II — Hunted" : act == 3 ? "Act III — Predator" : "Act IV — Apex";
    }
}
