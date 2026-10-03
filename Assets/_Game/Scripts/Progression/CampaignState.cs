using System;
using System.Collections.Generic;
using Vespertine.Data;

namespace Vespertine.Progression
{
    /// <summary>A recorded habit: <see cref="Value"/> is its weight (halved when the Vigil answers it), <see cref="Count"/>
    /// how often she did it in all, <see cref="Tonight"/> how often this mission.</summary>
    [Serializable] public class Counter { public string Key; public float Value, Count, Tonight; }
    /// <summary>A countermeasure in force (QW13): the habit that called it, how often she had done it then, and how many
    /// missions have passed without it.</summary>
    [Serializable] public class Answer { public string Cm, Habit; public int Count, Idle; }
    [Serializable] public class LoreEntry { public string Id, Title, Body, Mission; }

    [Serializable]
    public class MissionRecord
    {
        public string Id;
        public bool Completed;
        public float BestTime;
        public List<string> Optionals = new List<string>();
        public List<string> Secrets = new List<string>();
        public int Kills, Feeds, TimesSpotted;
        public bool NeverSpotted;
        public List<string> Challenges = new List<string>();   // every challenge ever earned here (Challenges.All ids)
        public List<string> Notables = new List<string>();     // notables whose drain has paid its Mark (once each, ever)
    }

    public static class Habits
    {
        public const string Rooftops = "rooftops", Snuff = "snuff", Lethal = "lethal", Sips = "sips", Sightings = "sightings",
            Dominion = "dominion", Mist = "mist", Blood = "blood", BodiesFound = "bodies_found";

        public static readonly string[] All = { Rooftops, Snuff, Lethal, Sips, Sightings, Dominion, Mist, Blood, BodiesFound };

        /// <summary>Habit → countermeasure group id (activated in later level files).</summary>
        public static string Countermeasure(string habit)
        {
            switch (habit)
            {
                case Rooftops: return "cm_rooftop";
                case Snuff: return "cm_caged";
                case Lethal: case BodiesFound: return "cm_paired";
                case Sips: case Sightings: return "cm_inquest";
                case Dominion: return "cm_ward";
                case Mist: return "cm_censer";
                case Blood: return "cm_salt";
            }
            return null;
        }

        /// <summary>The art a countermeasure answers: never two at once against one tree, so no build is shut down.
        /// Null for habits every build shares (the roofs).</summary>
        public static Tree? CounterTree(string cm)
        {
            switch (cm)
            {
                case "cm_caged": case "cm_censer": return Tree.Shade;
                case "cm_ward": return Tree.Dominion;
                case "cm_salt": return Tree.Sanguis;
                case "cm_paired": case "cm_inquest": return Tree.Predator;
            }
            return null;
        }

        /// <summary>The briefing's reason for an answer, in her own deeds: "You put out 9 lights".</summary>
        public static string Source(string habit, int n)
        {
            switch (habit)
            {
                case Rooftops: return $"You were marked on the rooftops {Times(n)}";
                case Snuff: return n == 1 ? "You put out a light" : $"You put out {n} lights";
                case Lethal: return n == 1 ? "You left a body" : $"You left {n} dead";
                case BodiesFound: return n == 1 ? "They found one of your dead" : $"They found {n} of your dead";
                case Sips: return n == 1 ? "You fed and let one live to talk" : $"You fed and let {n} live to talk";
                case Sightings: return $"You were seen {Times(n)}";
                case Dominion: return n == 1 ? "You bent a mind" : $"You bent {n} minds";
                case Mist: return $"You took the mist {Times(n)}";
                case Blood: return $"You worked blood {Times(n)}";
            }
            return $"{habit} {Times(n)}";
        }

        static string Times(int n) => n == 1 ? "once" : n == 2 ? "twice" : n + " times";

        /// <summary>How much one occurrence counts towards the Dossier.</summary>
        public static float Weight(string habit)
        {
            switch (habit)
            {
                case Rooftops: return 0.25f;
                case Snuff: return 1f;
                case Lethal: return 1.5f;
                case BodiesFound: return 2f;
                case Sips: return 1f;
                case Sightings: return 2f;
                case Dominion: return 1.5f;
                case Mist: return 1f;
                case Blood: return 1.5f;
            }
            return 1f;
        }

        public static string CountermeasureName(string cm)
        {
            switch (cm)
            {
                case "cm_rooftop": return "Rooftop sentries";
                case "cm_caged": return "Caged lamps";
                case "cm_paired": return "Paired patrols";
                case "cm_inquest": return "Inquisitors examine the living";
                case "cm_ward": return "Ward charms";
                case "cm_censer": return "Censer-bearers";
                case "cm_salt": return "Salt lines";
            }
            return cm;
        }

        public static string CountermeasureText(string cm)
        {
            switch (cm)
            {
                case "cm_rooftop": return "\"She walks the roofs like a cat. Post men up there and teach the hunters to look up.\" Everyone looks up; rooftop sentries where the city allows.";
                case "cm_caged": return "\"Every dead lamp is her signature. Cage them.\" Half the lamps, braziers and candles are caged against her hand, and every Watch and Vigil man carries a taper to relight the rest.";
                case "cm_paired": return "\"She takes men who walk alone. No one walks alone again.\" Two bodies found bring a Lockdown (was three); patrols walk in pairs.";
                case "cm_inquest": return "\"The ones she leaves alive have seen her. Question them, and quickly.\" Dazed victims wake in 25 s (was 45).";
                case "cm_ward": return "\"Men forget their orders around her. Issue ward charms.\" Every Watch, Vigil, Church and Institute man wears a ward: Dominion fails on them.";
                case "cm_censer": return "\"She comes as fog. Garlic smoke stops the fog.\" Vigil hunters swing censers: no mist or Shadow Dash within 4.5 m of them.";
                case "cm_salt": return "\"Blood-craft. Salt the doorways and the stairs.\" Vigil and Church feet break Blood Snares and raise the alarm.";
            }
            return "";
        }
    }

    /// <summary>All persistent campaign progress. Pure C#; serialised with JsonUtility.</summary>
    [Serializable]
    public class CampaignState
    {
        public const int MaxAwakening = 10;
        public static readonly int[] VitaeThresholds = { 0, 120, 400, 700, 1050, 1400, 1800, 2250, 3000, 3800 };   // tuned against CampaignEconomy (X6)
        /// <summary>QW13 (§27): the Vigil answers from M03, at most <see cref="MaxCountermeasures"/> at once, and an answer
        /// lapses once its habit has gone <see cref="AnswerLapse"/> missions without being repeated.</summary>
        public const int DossierStartsAt = 2; // M03 index
        public const int MaxCountermeasures = 2, AnswerLapse = 2;
        /// <summary>The first mission whose level carries dormant bodies for the countermeasures (M07); before it they are
        /// systemic only (lamps, wards, salt, inquest, look-up).</summary>
        public const int DossierBodiesFrom = 6;

        public Difficulty Difficulty = Difficulty.Hunter;
        public int MissionIndex;                 // next mission to play
        public int Vitae;
        public int Marks;                        // unspent
        public List<string> Nodes = new List<string>();
        public List<string> Gifts = new List<string>();     // story grants: free, kept through a respec
        public List<string> Loadout = new List<string>();
        public List<MissionRecord> Records = new List<MissionRecord>();
        public List<Counter> Dossier = new List<Counter>();          // accumulated habit weights
        public List<string> Countermeasures = new List<string>();
        public List<Answer> Answers = new List<Answer>();              // why each countermeasure is in force
        public List<string> Flags = new List<string>();
        public List<LoreEntry> Lore = new List<LoreEntry>();
        public List<string> Bestiary = new List<string>();  // archetype ids Ilse has laid eyes on: the Codex
        public int Terror, Rumour;
        public string Tobias = "alive";          // alive | thrall | lost
        public bool Ironblood;                   // no reloads mode (unused yet)
        public string Created;
        public float PlayTime;
        public int TotalKills, TotalSips, TotalDrains;

        /// <summary>Records a first sighting for the Codex. True only the first time.</summary>
        public bool MarkSeen(string archetypeId)
        {
            if (string.IsNullOrEmpty(archetypeId) || Bestiary.Contains(archetypeId)) return false;
            Bestiary.Add(archetypeId);
            return true;
        }

        public static CampaignState NewGame(Difficulty d)
        {
            var c = new CampaignState { Difficulty = d, Created = DateTime.Now.ToString("yyyy-MM-dd HH:mm") };
            return c;
        }

        // ------------------------------------------------------------------ awakening & stats
        public static int AwakeningFor(int vitae)
        {
            int lvl = 1;
            for (int i = 1; i < VitaeThresholds.Length; i++) if (vitae >= VitaeThresholds[i]) lvl = i + 1;
            return Math.Min(lvl, MaxAwakening);
        }

        public int Awakening => AwakeningFor(Vitae);
        public int NextThreshold => Awakening >= MaxAwakening ? -1 : VitaeThresholds[Awakening];

        public static int SlotsFor(int awakening) => awakening >= 10 ? 6 : awakening >= 8 ? 5 : awakening >= 6 ? 4 : awakening >= 4 ? 3 : awakening >= 2 ? 2 : 1;
        public int Slots => SlotsFor(Awakening);
        public static float MaxHpFor(int a) => 40f + (a - 1) * 80f / 9f;
        public static float MaxBloodFor(int a) => 60f + (a - 1) * 10f;
        public float MaxHP => MaxHpFor(Awakening);
        public float MaxBlood => MaxBloodFor(Awakening) + (Has("sanguis.vessel") ? 50 : 0);

        /// <summary>Adds vitae; returns the number of Awakening levels gained.</summary>
        public int AddVitae(int amount)
        {
            int before = Awakening;
            Vitae = Math.Max(0, Vitae + amount);
            return Awakening - before;
        }

        // ------------------------------------------------------------------ skill tree
        public bool Has(string node) => Nodes.Contains(node);

        public bool CanUnlock(string id, out string reason)
        {
            reason = null;
            var n = Skills.Get(id);
            if (n == null) { reason = "Unknown"; return false; }
            if (Has(id)) { reason = "Owned"; return false; }
            if (n.Tier == 0) { reason = "Granted by the story"; return false; }
            if (Awakening < n.RequiredAwakening) { reason = $"Requires Awakening {n.RequiredAwakening}"; return false; }
            if (n.Parent != null && !Has(n.Parent)) { reason = $"Requires {Skills.Get(n.Parent)?.Name}"; return false; }
            if (n.Cross != null && !Has(n.Cross)) { reason = $"Requires {Skills.Get(n.Cross)?.Name}"; return false; }
            if (Marks < n.Cost) { reason = $"Needs {n.Cost} Mark{(n.Cost > 1 ? "s" : "")}"; return false; }
            return true;
        }

        public bool Unlock(string id)
        {
            if (!CanUnlock(id, out _)) return false;
            var n = Skills.Get(id);
            Marks -= n.Cost;
            Nodes.Add(id);
            if (Skills.IsAbility(id) && !Skills.Ability(id).ThrallCommand && Loadout.Count < Slots) Loadout.Add(id);
            return true;
        }

        /// <summary>The skill trees open after M01 (Gameplay Redesign P4: the first chosen power before minute 15), or with any node owned.</summary>
        public bool ArtsOpen => MissionIndex >= ArtsOpenAt || Nodes.Count > 0;
        /// <summary>Missions completed before the Blood Arts open in the refuge (was 3).</summary>
        public const int ArtsOpenAt = 1;

        /// <summary>Story grant (e.g. Beckon in M02): free and outside the tier rules.</summary>
        public void Grant(string id)
        {
            if (Has(id)) return;
            Nodes.Add(id);
            if (!Gifts.Contains(id)) Gifts.Add(id);
            if (Skills.IsAbility(id) && !Skills.Ability(id).ThrallCommand && Loadout.Count < Slots) Loadout.Add(id);
        }

        public int SpentMarks()
        {
            int s = 0;
            foreach (var id in Nodes) { var n = Skills.Get(id); if (n != null && n.Tier > 0 && !Gifts.Contains(id)) s += n.Cost; }
            return s;
        }

        public void Respec()
        {
            Marks += SpentMarks();
            Nodes.RemoveAll(id => { var n = Skills.Get(id); return n != null && n.Tier > 0 && !Gifts.Contains(id); });
            Loadout.RemoveAll(id => !Has(id));
        }

        public IEnumerable<string> OwnedAbilities()
        {
            foreach (var id in Nodes) { var a = Skills.Ability(id); if (a != null && !a.ThrallCommand) yield return id; }
        }

        public bool Equip(string id)
        {
            if (!Has(id) || !Skills.IsAbility(id) || Loadout.Contains(id) || Loadout.Count >= Slots) return false;
            Loadout.Add(id);
            return true;
        }

        public void Unequip(string id) => Loadout.Remove(id);

        /// <summary>Node ids that were renamed: an old save keeps what it paid for (D155: Shadowstep became Umbral Step).</summary>
        static readonly (string From, string To)[] Renamed = { ("shade.shadowstep", "shade.umbral"), ("shade.shadowstep_bars", "shade.dash_bars") };

        /// <summary>Run on every load: renamed nodes carried over, the loadout held to what she owns and her slots.</summary>
        public void ClampLoadout()
        {
            foreach (var (from, to) in Renamed)
            {
                if (Nodes.Remove(from) && !Nodes.Contains(to)) Nodes.Add(to);
                if (Gifts.Remove(from) && !Gifts.Contains(to)) Gifts.Add(to);
            }
            Loadout.RemoveAll(id => !Has(id));
            while (Loadout.Count > Slots) Loadout.RemoveAt(Loadout.Count - 1);
        }

        // ------------------------------------------------------------------ dossier
        public float Habit(string key)
        {
            var c = Dossier.Find(x => x.Key == key);
            return c?.Value ?? 0f;
        }

        public void AddHabit(string key, float count)
        {
            var c = Dossier.Find(x => x.Key == key);
            if (c == null) Dossier.Add(c = new Counter { Key = key });
            c.Value += count * Habits.Weight(key);
            c.Count += count;
            c.Tonight += count;
        }

        /// <summary>Every countermeasure her recorded habits call for (weight at least <paramref name="minWeight"/>),
        /// heaviest first: Vane's whole Dossier, for M12. Pure; unit-tested.</summary>
        public List<string> FullDossier(float minWeight = 2f)
        {
            var ranked = new List<Counter>(Dossier);
            ranked.Sort((a, b) => b.Value.CompareTo(a.Value));
            var l = new List<string>();
            foreach (var c in ranked)
            {
                if (c.Value < minWeight) break;
                var cm = Habits.Countermeasure(c.Key);
                if (cm != null && !l.Contains(cm)) l.Add(cm);
            }
            return l;
        }

        /// <summary>The countermeasure the Vigil would add next if the night ended now (null if none is due).</summary>
        public string NextCountermeasure(float minWeight = 4f) => Candidate(minWeight, null);

        /// <summary>Why <paramref name="cm"/> is in force, for the briefing (null for one with no record, e.g. an old save).</summary>
        public string AnswerSource(string cm)
        {
            var a = Answers.Find(x => x.Cm == cm);
            return a != null && a.Habit != null && a.Count > 0 ? Habits.Source(a.Habit, a.Count) : null;
        }

        /// <summary>The heaviest habit's answer that may still be added: not in force, a free slot, and no answer already
        /// against its tree.</summary>
        Counter CandidateHabit(float minWeight, List<string> extra)
        {
            if (Countermeasures.Count + (extra?.Count ?? 0) >= MaxCountermeasures) return null;
            var ranked = new List<Counter>(Dossier);
            ranked.Sort((a, b) => b.Value.CompareTo(a.Value));
            foreach (var c in ranked)
            {
                if (c.Value < minWeight) return null;
                var cm = Habits.Countermeasure(c.Key);
                if (cm == null || Countermeasures.Contains(cm) || (extra != null && extra.Contains(cm))) continue;
                var tree = Habits.CounterTree(cm);
                if (tree != null && (Countermeasures.Exists(x => Habits.CounterTree(x) == tree) || (extra != null && extra.Exists(x => Habits.CounterTree(x) == tree)))) continue;
                return c;
            }
            return null;
        }

        string Candidate(float minWeight, List<string> extra)
        {
            var c = CandidateHabit(minWeight, extra);
            return c != null ? Habits.Countermeasure(c.Key) : null;
        }

        /// <summary>M12, the burn: Vane's archive is ash. Every countermeasure he built is forgotten and every habit he
        /// recorded is unread; the Vigil starts again from what it sees of her from now on. Applied once (true then).</summary>
        public bool BurnArchive()
        {
            if (!Flag("archive_burned") || Flag("archive_burned_applied")) return false;
            Countermeasures.Clear();
            Answers.Clear();
            foreach (var c in Dossier) c.Value = c.Count = c.Tonight = 0f;
            SetFlag("archive_burned_applied");
            return true;
        }

        /// <summary>After a mission: answers whose habit she didn't repeat tonight age, and lapse after
        /// <see cref="AnswerLapse"/> missions; then, from M03, the heaviest habits fill the free slots (at most
        /// <see cref="MaxCountermeasures"/> in force, one per tree). Tonight's tallies are cleared.
        /// Returns the newly added countermeasure ids.</summary>
        public List<string> UpdateCountermeasures(int completedMissionIndex, float minWeight = 4f)
        {
            var added = new List<string>();
            // an older save may hold more than the cap, or answers with no record
            while (Countermeasures.Count > MaxCountermeasures) Countermeasures.RemoveAt(0);
            Answers.RemoveAll(a => !Countermeasures.Contains(a.Cm));
            foreach (var cm in Countermeasures)
                if (!Answers.Exists(a => a.Cm == cm))
                {
                    var h = HabitFor(cm);
                    Answers.Add(new Answer { Cm = cm, Habit = h?.Key, Count = h != null ? (int)Math.Round(h.Count) : 0 });
                }
            foreach (var a in Answers.ToArray())
            {
                bool repeated = Dossier.Exists(c => c.Tonight > 0f && Habits.Countermeasure(c.Key) == a.Cm);
                a.Idle = repeated ? 0 : a.Idle + 1;
                if (a.Idle < AnswerLapse) continue;
                // lapsed: the habit has cooled with it, so it isn't answered again the same night
                Answers.Remove(a); Countermeasures.Remove(a.Cm);
                foreach (var c in Dossier) if (Habits.Countermeasure(c.Key) == a.Cm) c.Value *= 0.5f;
            }
            if (completedMissionIndex + 1 >= DossierStartsAt)
            {
                for (var c = CandidateHabit(minWeight, null); c != null; c = CandidateHabit(minWeight, null))
                {
                    var cm = Habits.Countermeasure(c.Key);
                    Countermeasures.Add(cm);
                    Answers.Add(new Answer { Cm = cm, Habit = c.Key, Count = Math.Max(1, (int)Math.Round(c.Count)) });
                    added.Add(cm);
                    c.Value *= 0.5f; // the Vigil adapted; the habit must be repeated to escalate again
                }
            }
            foreach (var c in Dossier) c.Tonight = 0f;
            return added;
        }

        /// <summary>The heaviest recorded habit that calls for <paramref name="cm"/>.</summary>
        Counter HabitFor(string cm)
        {
            Counter best = null;
            foreach (var c in Dossier)
                if (Habits.Countermeasure(c.Key) == cm && (best == null || c.Value > best.Value)) best = c;
            return best;
        }

        // ------------------------------------------------------------------ records, flags, lore
        public MissionRecord Record(string id)
        {
            var r = Records.Find(x => x.Id == id);
            if (r == null) Records.Add(r = new MissionRecord { Id = id });
            return r;
        }

        public bool Flag(string f) => Flags.Contains(f);
        public void SetFlag(string f, bool on = true)
        {
            if (on && !Flags.Contains(f)) Flags.Add(f);
            if (!on) Flags.Remove(f);
        }

        public bool DiscoverLore(string id, string title, string body, string mission = null)
        {
            if (Lore.Exists(l => l.Id == id)) return false;
            Lore.Add(new LoreEntry { Id = id, Title = title, Body = body, Mission = mission });
            return true;
        }

        public string Ending(string choice)
        {
            bool terror = Terror > Rumour;
            switch (choice)
            {
                case "free": return terror ? "endless_night" : "night_court";
                case "consume": return terror ? "new_abbess" : "pale_lady";
                default: return terror ? "ashes" : (Tobias == "alive" ? "dawn_tobias" : "dawn");
            }
        }

        public const int TotalShrines = 10;
        public int ShrinesFound()
        {
            // the net-shed altar on the docks (M03) is the first; the Sisters' ossuary under St Vesper's (M14) the last.
            // The anchoress's cell (M04) is the order's history, not a shrine: it is a Journal entry only.
            int n = 0;
            for (int i = 1; i <= TotalShrines; i++) if (Flag("found_shrine" + i)) n++;
            return n;
        }

        /// <summary>What became of everyone she touched, one line each, in the order the ending screen shows them.
        /// Pure over the campaign's flags; unit-tested.</summary>
        public List<string> Epilogue(string ending)
        {
            var l = new List<string>();
            bool reign = ending == "endless_night" || ending == "night_court" || ending == "new_abbess" || ending == "pale_lady";
            bool fear = Terror > Rumour;

            // Tobias
            if (Tobias == "thrall")
                l.Add(reign ? "Tobias keeps her door. He has forgotten what he wanted before he wanted only to serve her, and he is happier than he ever was."
                            : "Tobias woke on the cathedral steps with the bond gone out of him. He remembers everything. He lights a candle for her anyway.");
            else if (Tobias == "alive")
                l.Add(Flag("candle_row_held")
                    ? "Tobias still keeps the candle shop on Candle Row, behind the barricade nobody ever took down. He leaves the back door unbarred at night."
                    : "Tobias kept the candle shop until the winter, then sold it and went north. He writes, once a year, to an address he knows no one reads.");
            else l.Add("Candle Row burned the night the militia came. Tobias was not among the bodies, and he was not among the living.");

            // Vane
            if (Flag("vane_dead")) l.Add("Inquisitor-Captain Vane is buried in the Vigil's plot under a stone with no name. His men say he died at the door, as he always said he would.");
            else if (Flag("vane_alive")) l.Add(fear
                ? "Vane lived. He resigned his commission the week after, and keeps a lamp burning in his window every night. He has never once looked out of it."
                : "Vane lived. He burned his book on her, page by page, and took a parish in the fens where nobody has heard of the Pale Vigil.");

            // Hollin
            if (Flag("hollin_thrall")) l.Add("Sister Maud Hollin still tracks for the Vigil. Every trail she follows leads, somehow, the wrong way."
                + (Flag("hollin_journal") ? " She wanted to ask Ilse how she bears the call. She has stopped needing to." : ""));
            else if (Flag("hollin_dead")) l.Add("Sister Maud Hollin's hounds were given to a farmer outside the walls. They will not go near the city after dark."
                + (Flag("hollin_journal") ? " Her field book, with Ilse's name in it, was never found." : ""));
            else if (Flag("heard_hollin") || Flag("hollin_past")) l.Add("Sister Maud Hollin never found her. She never stopped looking, either.");

            // Saule and Clement
            if (Flag("saule_purged")) l.Add("Dr. Emeric Saule was found in his own purge chamber. The Institute's report calls it an accident of method.");
            else if (Flag("saule_dead")) l.Add("Dr. Emeric Saule died among his sunstones. The Institute shut the undercroft and bricked up the stair again.");
            else if (Flag("saule_spared")) l.Add("Dr. Emeric Saule fled the city before the Council could ask him where their eternity had gone. He has not stopped running.");
            if (Flag("clement_saved")) l.Add(Flag("clement_testified")
                ? "Clement gave the magistrates everything: the ledgers, the names, the vault. Seven councillors stood trial. None of them were hanged."
                : "Clement took a post at a hospital in the south, and sends his wages, every month, to the families on the Institute's ledgers.");

            // the city
            if (Flag("council_names")) l.Add("The names of the Council of Lanterns were printed in the Ostmere Courier. Three of them left the city. Two of them were found in the canal.");
            if (Flag("watch_vigil_split")) l.Add("The City Watch never took the Vigil's orders again. The Bishop's hunters went back to their cloisters, and stayed there.");
            if (Flag("faithful_bastion")) l.Add(reign
                ? "The Faithful went from cellars to chapels. They sing her name now, in the open."
                : "The Faithful still meet in the cellars under Candle Row. They leave the candles burning, in case she comes back.");
            if (Flag("archive_burned")) l.Add("Whatever the Vigil knew about her went up with the Lantern House archive. They have had to learn her all over again.");

            int shrines = ShrinesFound();
            if (shrines >= TotalShrines) l.Add("She found every shrine her mother's order left in the city, and she remembers every name on them.");
            else if (shrines > 0) l.Add($"She found {shrines} of the old order's shrines. The rest are still out there, under the city, waiting.");
            return l;
        }
    }
}
