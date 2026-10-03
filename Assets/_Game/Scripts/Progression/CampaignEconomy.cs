using System.Collections.Generic;
using UnityEngine;
using Vespertine.Data;
using Vespertine.Level;

namespace Vespertine.Progression
{
    /// <summary>
    /// The campaign's earning curve, measured from the shipped mission files (X6). <see cref="Survey"/> counts what a night
    /// offers (blood by archetype, notables, optionals, secrets, scripted vitae and Marks). <see cref="Simulate"/> plays
    /// a whole campaign in a given <see cref="Style"/> and reports Ilse's Awakening and Marks as each night starts. The
    /// tests hold that curve to the design: the trees' tiers open on time for every style, and nobody maxes out early.
    /// </summary>
    public static class CampaignEconomy
    {
        // objective rewards (PROGRESSION.md, Vitae): first completion only. Optionals pay Vitae, not Marks (P8)
        public const int PrimaryVitae = 60, OptionalVitae = 30, SecretVitae = 20;

        public class Supply
        {
            public string Mission;
            public int Feedable, Notables, Optionals, MarkSecrets, LoreSecrets, ScriptVitae, ScriptMarks;
            public float SipVitae, DrainVitae; // if she sipped / drained every feedable soul (Hunter blood gain)
        }

        /// <summary>
        /// One way to play: feeds per night (feeding follows need and opportunity, not how many people a map holds,
        /// so it is a count, capped by the night's feedable people), and the shares of optionals and secrets found.
        /// </summary>
        public class Style
        {
            public string Name;
            public int Sips, Drains, NotableDrains; // notable drains come out of Drains when she has them to spare
            public float Optionals, Secrets;
            public int Challenges;                  // challenges earned on a first clear, each paying a Mark (P8)
        }

        public static readonly Style Ghost = new Style { Name = "Ghost", Sips = 3, Drains = 0, NotableDrains = 0, Optionals = 0.5f, Secrets = 0.3f, Challenges = 3 };
        public static readonly Style Typical = new Style { Name = "Typical", Sips = 6, Drains = 1, NotableDrains = 1, Optionals = 0.6f, Secrets = 0.5f, Challenges = 2 };
        public static readonly Style Predator = new Style { Name = "Predator", Sips = 4, Drains = 6, NotableDrains = 99, Optionals = 0.8f, Secrets = 0.7f, Challenges = 2 };
        public static readonly Style[] Styles = { Ghost, Typical, Predator };

        public struct Night
        {
            public string Mission;
            public int VitaeAtStart, AwakeningAtStart, MarksAtStart; // Marks: earned so far, all of them spendable
            public int VitaeEarned, MarksEarned;
        }

        /// <summary>What a mission offers. Dormant groups (Dossier answers, lockdown, scripted arrivals) are not counted.</summary>
        public static Supply Survey(LevelData d)
        {
            var s = new Supply { Mission = d.Id };
            foreach (var e in d.Entities)
            {
                if (e.Kind == "secret")
                {
                    if (e.Has("lore")) s.LoreSecrets++; else s.MarkSecrets++;
                    continue;
                }
                if (e.Kind != "npc" || e.Group != null || e.Has("friendly")) continue;
                var a = Archetypes.Get(e.Type);
                if (a == null || a.Has(ArchFlags.Undead)) continue;
                float v = Player.Vampire.BloodValue(a.Blood);
                s.Feedable++;
                s.SipVitae += v * 0.6f;
                s.DrainVitae += v;
                if (a.Blood == BloodType.Notable) s.Notables++;
            }
            foreach (var o in d.Objectives) if (o.Optional) s.Optionals++;
            foreach (var r in d.Script)
                foreach (var act in r.Actions)
                {
                    if (act.Count < 1) continue;
                    // a branch's reward (M14's consume) is a choice, not income: count only unconditional event rules
                    if (r.Event == "choice") continue;
                    if (act[0] == "vitae" && act.Count > 1 && int.TryParse(act[1], out var vi)) s.ScriptVitae += vi;
                    if (act[0] == "mark") s.ScriptMarks += act.Count > 1 && int.TryParse(act[1], out var mi) ? mi : 1;
                }
            return s;
        }

        /// <summary>Vitae a first clear pays for its objectives (not counting feeding). Lore-only secrets pay Vitae but no Mark.</summary>
        public static int ObjectiveVitae(int optionals, int secrets) => PrimaryVitae + optionals * OptionalVitae + secrets * SecretVitae;

        public static List<Night> Simulate(IList<Supply> nights, Style style, Difficulty difficulty = Difficulty.Hunter)
        {
            var list = new List<Night>();
            float gain = Difficulties.Get(difficulty).BloodGain;
            int vitae = 0, marks = 0;
            foreach (var s in nights)
            {
                var n = new Night { Mission = s.Mission, VitaeAtStart = vitae, AwakeningAtStart = CampaignState.AwakeningFor(vitae), MarksAtStart = marks };
                int optionals = Mathf.RoundToInt(s.Optionals * style.Optionals);
                int secrets = Mathf.RoundToInt(s.MarkSecrets * style.Secrets), lore = Mathf.RoundToInt(s.LoreSecrets * style.Secrets);
                int notables = Mathf.Min(style.NotableDrains, s.Notables);
                int drains = Mathf.Min(Mathf.Max(style.Drains, notables), s.Feedable), sips = Mathf.Min(style.Sips, s.Feedable - drains);
                float each = s.Feedable > 0 ? s.DrainVitae / s.Feedable : 0f; // the night's average blood
                float feed = (sips * each * 0.6f + Mathf.Max(0, drains - notables) * each
                              + notables * Player.Vampire.BloodValue(BloodType.Notable)) * gain;
                n.VitaeEarned = Mathf.RoundToInt(feed) + s.ScriptVitae + ObjectiveVitae(optionals, secrets + lore);
                n.MarksEarned = 1 + secrets + notables + s.ScriptMarks + style.Challenges * Progression.Challenges.MarkReward;
                vitae += n.VitaeEarned;
                marks += n.MarksEarned;
                list.Add(n);
            }
            return list;
        }

        /// <summary>The final tally after the last night: Awakening and total Marks.</summary>
        public static (int awakening, int marks) Final(List<Night> run)
        {
            var last = run[run.Count - 1];
            return (CampaignState.AwakeningFor(last.VitaeAtStart + last.VitaeEarned), last.MarksAtStart + last.MarksEarned);
        }
    }
}
