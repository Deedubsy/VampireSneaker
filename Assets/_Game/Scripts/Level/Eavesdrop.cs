using System.Collections.Generic;
using UnityEngine;

namespace Vespertine.Level
{
    /// <summary>
    /// One overheard conversation: a fixed list of lines spoken in a loop while every speaker is at ease.
    /// It counts as overheard only if Ilse is in range from the first line to the last. Leaving, or a speaker
    /// growing suspicious, breaks it; it starts again after a pause. Pure logic (no scene access) so it is unit-tested.
    /// </summary>
    public class Eavesdrop
    {
        public struct Line { public string Who, Text; public float Dur; }

        public readonly List<Line> Lines = new List<Line>();
        public float Rest = 8f;       // pause between rounds (and after an interruption)
        public float Gap = 0.6f;      // silence after each line

        public int Index { get; private set; } = -1;   // line being spoken, -1 while resting
        public bool Listening { get; private set; }    // Ilse has heard every line of this round so far
        public bool Heard { get; private set; }
        float _t;

        public static float LineDuration(string text) => Mathf.Clamp(1.6f + (text?.Length ?? 0) * 0.055f, 2.2f, 7f);

        /// <summary>"crane: The Lantern-Master won't come." → speaker id + text. Lines without a prefix go to <paramref name="fallback"/>.</summary>
        public static Line ParseLine(string raw, string fallback)
        {
            raw = raw ?? "";
            int c = raw.IndexOf(':');
            string who = fallback, text = raw.Trim();
            if (c > 0 && c < 24 && raw.IndexOf(' ') > c) { who = raw.Substring(0, c).Trim(); text = raw.Substring(c + 1).Trim(); }
            return new Line { Who = who, Text = text, Dur = LineDuration(text) };
        }

        public float Progress => !Listening || Index < 0 || Lines.Count == 0 ? 0f
            : Mathf.Clamp01((Index + Mathf.Clamp01(_t / (Lines[Index].Dur + Gap))) / Lines.Count);

        /// <summary>Starts resting with a short first pause so a conversation is underway soon after the mission starts.</summary>
        public Eavesdrop(float firstDelay = 2f) { _t = Rest - firstDelay; }

        /// <summary>Advance. Returns the index of a line that starts this tick (to be spoken), else -1.</summary>
        public int Tick(float dt, bool speakersReady, bool inRange)
        {
            if (Heard || Lines.Count == 0) return -1;
            if (!speakersReady)
            {
                if (Index >= 0) { Index = -1; _t = 0f; }
                Listening = false;
                return -1;
            }
            _t += dt;
            if (Index < 0)
            {
                if (_t < Rest) return -1;
                Index = 0; _t = 0f; Listening = inRange;
                return 0;
            }
            if (!inRange) Listening = false;
            if (_t < Lines[Index].Dur + Gap) return -1;
            _t = 0f;
            if (Index + 1 < Lines.Count) { Index++; return Index; }
            if (Listening) { Heard = true; Index = -1; return -1; }
            Index = -1;
            return -1;
        }

        public void MarkHeard() { Heard = true; Index = -1; Listening = false; }
    }
}
