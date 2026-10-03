using System.Collections.Generic;
using System.Linq;

namespace Vespertine.Data
{
    /// <summary>
    /// The Codex: what Ilse has learned of the people who hunt her. An archetype enters it the first time she lays eyes
    /// on one (<see cref="Progression.CampaignState.Bestiary"/>); the text here is built from the archetype's own numbers,
    /// so the codex can never disagree with how the game actually plays.
    /// </summary>
    public static class Codex
    {
        /// <summary>Archetypes that get an entry. "notable" is the generic stand-in for named targets, which have their own pages in the Journal.</summary>
        public static IEnumerable<Archetype> Listed => Archetypes.All.Where(a => a.Id != "notable");

        public static readonly Faction[] Order = { Faction.None, Faction.Institute, Faction.Watch, Faction.Guild, Faction.Church, Faction.Vigil };

        public static string FactionName(Faction f)
        {
            switch (f)
            {
                case Faction.Institute: return "The Coldwater Institute";
                case Faction.Watch: return "The City Watch";
                case Faction.Guild: return "The Lamplighters' Guild";
                case Faction.Church: return "The Church";
                case Faction.Vigil: return "The Pale Vigil";
                default: return "The City";
            }
        }

        /// <summary>Ilse's own note on each kind of person, in her voice: the page's opening line.</summary>
        public static string Lore(string id) => _lore.TryGetValue(id, out var l) ? l : null;

        static readonly Dictionary<string, string> _lore = new Dictionary<string, string>
        {
            ["civilian"] = "They lock their doors at the ninth bell now and still walk home the long way, past the lamps. Most never look twice at a shadow. The ones who do run toward the light, and the light has learned to answer.",
            ["drunk"] = "The city's most forgiving witness. By morning he will remember a woman in grey, or a dog, or nothing at all, and nobody will ask him which.",
            ["sailor"] = "Ashore for a night and owed a fight by somebody. They sing too loud to hear a step behind them, but they shout louder still, and the Watch knows the harbour voices.",
            ["beggar"] = "The fever keeps them awake and sharp-eyed in doorways no one else will sit in. Their blood runs hot and strange; for a while afterwards she can hear every heart on the street.",
            ["orderly"] = "Coldwater pays them to hold people down. They walk slowly, they carry their own lanterns, and they do not ask what the screaming is for.",
            ["watchman"] = "A musket, a lantern and a round he has walked for twenty years without once meeting anything worse than a cutpurse. One shot, then a long, clumsy reload. Make him spend it on the dark.",
            ["sergeant"] = "Where a sergeant goes, the Watch stops guessing. He sends men to every corner a body could hide in, and when he falls silent the others notice.",
            ["lamplighter"] = "The Guild's men walk the same circuit every night with a pole and a flame, and every lamp she puts out, they put back. Patient, punctual, and very hard to argue with.",
            ["priest"] = "Their faith is not a story. Near one her gifts gutter like a candle in a draught and her skin begins to blister. When a priest rings the bell, the whole parish wakes.",
            ["acolyte"] = "Boys in borrowed robes, swinging censers to keep their courage up. The smoke is only incense. The light is real enough.",
            ["guest"] = "Silk, wine and a mask. The revel is the one place in the city where a stranger in good clothes draws no notice, and their blood is sweet with the evening's drink.",
            ["servant"] = "Unseen by the people they serve, and so free to go anywhere in the house. Bound to her, a servant can carry a door key, a rumour or a poison where she cannot.",
            ["soldier"] = "The militia was raised the week the papers first printed the word 'vampire'. They hold the checkpoints in pairs, they are braver than the Watch, and they shoot straighter.",
            ["hunter"] = "The Vigil trains its hunters to look where nobody looks: up. They carry silver for the wound and flares for the dark, and they have done this before.",
            ["hound"] = "It does not need to see her. It finds the warm trail she leaves and the blood she spills, and it never sleeps on its post. Its keeper follows the bark.",
            ["tracker"] = "Sister Hollin has hunted kindred across three provinces and kept a journal of every kill. She reads blood on cobbles as other people read a newspaper. She is already wary, and she is never wrong for long.",
            ["alchemist"] = "Coldwater's chemists burn garlic and quicklime in brass censers. It is crude, it stinks, and inside that smoke her mist and her shadow-walking simply stop working.",
            ["inquisitor"] = "An inquisitor reads a corpse the way Ilse reads a street. They know a thrall's eyes when they see them, and they carry a ward that her Dominion cannot cross.",
            ["bulwark"] = "Plate armour and a halberd, chosen for one purpose: there is no throat to reach from the front. Get behind a Bulwark, or do not get near one at all.",
            ["sentry"] = "The Watch learned that the roofs were hers. Now a man with a musket stands on the ridge tiles every few streets, looking along them.",
            ["scholar"] = "They write everything down, and that includes her. A scholar has no weapon but a scream, and the orderlies come running when they hear it.",
            ["engineer"] = "The engineers keep the sunstone current flowing. Cut the lamps and they are out with their tools inside a minute, putting back the light that burns her.",
            ["worker"] = "Night shift at the gasworks, shovelling coke for the lamps that hunt her. They are not soldiers. Frighten them badly enough and they will drop their shovels and run off the site for good.",
            ["fledgling"] = "Coldwater's failures: people they made into something like her and then left to starve. They cannot climb and they cannot think. The sunstone ends them. Their blood is ash.",
            ["saule"] = "Dr. Emeric Saule believes that eternity is a chemistry problem and that Ilse is the answer to it. He lives inside a ring of sunstone, a ward and a censer, and he has a pistol.",
            ["vane"] = "Inquisitor-Captain Vane commands the Pale Vigil, and every countermeasure the city has raised against her was his idea first. Holy, warded, silver-armed. He is not afraid of her. He should be.",
        };

        public static string WeaponName(Weapon w)
        {
            switch (w)
            {
                case Weapon.None: return "Unarmed";
                case Weapon.Cudgel: return "Cudgel (close)";
                case Weapon.Musket: return "Musket (long reload)";
                case Weapon.Pistol: return "Pistol";
                case Weapon.Crossbow: return "Crossbow";
                case Weapon.Bite: return "Teeth";
                case Weapon.Halberd: return "Halberd (close)";
                case Weapon.Censer: return "Censer";
            }
            return w.ToString();
        }

        public static string MoraleText(Morale m)
        {
            switch (m)
            {
                case Morale.Civilian: return "Screams and runs for help.";
                case Morale.Low: return "Fights if cornered; breaks when frightened.";
                case Morale.Brave: return "Stands and fights; hard to frighten.";
                default: return "Never runs.";
            }
        }

        public static string SightText(Archetype a)
        {
            if (a.Has(ArchFlags.Quadruped) && a.Fov >= 360f) return $"Nose rather than eyes: senses Ilse within {a.Near:0} m, all around, in any light.";
            return $"{a.Fov:0}° cone. Sees her in darkness within {a.Near:0} m; in light, out to {a.Far:0} m.";
        }

        public static string HearingText(Archetype a)
        {
            if (a.Hear >= 1.25f) return "Keen ears.";
            if (a.Hear <= 0.85f) return "Dull ears.";
            return "Ordinary ears.";
        }

        /// <summary>Plain-language traits, one per flag that changes how Ilse must play around them.</summary>
        public static List<string> Traits(Archetype a)
        {
            var t = new List<string>();
            if (a.Has(ArchFlags.LooksUp)) t.Add("Looks up: rooftops and ledges are not safe in their sight.");
            if (a.Has(ArchFlags.Lantern)) t.Add("Carries a light that pools around them.");
            if (a.Has(ArchFlags.Relights)) t.Add("Relights lamps that have gone dark.");
            if (a.Has(ArchFlags.Flares)) t.Add("Throws flares into darkness that seems wrong.");
            if (a.Has(ArchFlags.Smell)) t.Add("Smells her close by, in any light, and follows blood trails.");
            if (a.Has(ArchFlags.HolyAura)) t.Add("Holy aura: her arts fail and she burns within 5 m.");
            if (a.Has(ArchFlags.Ward)) t.Add("Warded: Dominion arts fail on them.");
            if (a.Has(ArchFlags.Censer)) t.Add("Censer smoke (4.5 m) blocks mist and Shadow Dash.");
            if (a.Has(ArchFlags.Armored)) t.Add("Armoured: cannot be fed on from the front.");
            if (a.Has(ArchFlags.Officer)) t.Add("Officer: organises searches and rallies others.");
            if (a.Has(ArchFlags.Officer) && a.Has(ArchFlags.Ward)) t.Add("Reads bodies: what they find goes into the Dossier, and a staged accident does not fool them.");
            if (a.Has(ArchFlags.Silver)) t.Add("Silver: wounds that will not close.");
            if (a.Has(ArchFlags.Quadruped)) t.Add("An animal: Dominion arts fail on it.");
            if (a.Has(ArchFlags.Undead)) t.Add("Kindred: its blood is ash in her mouth.");
            return t;
        }

        /// <summary>What drinking from them gives, or null when it gives nothing worth a line.</summary>
        public static string BloodText(Archetype a)
        {
            if (a.Has(ArchFlags.Undead)) return null;
            var q = BloodQualities.Get(a.Blood);
            string amount = $"{q.Amount:0} blood";
            return string.IsNullOrEmpty(q.Humour) ? amount : $"{amount} · {q.HumourName}: {q.HumourDesc}";
        }
    }
}
