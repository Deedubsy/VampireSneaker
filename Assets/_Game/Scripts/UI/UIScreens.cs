using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Vespertine.AI;
using Vespertine.Controls;
using Vespertine.Core;
using Vespertine.Data;
using Vespertine.Level;
using Vespertine.Mission;
using Vespertine.Player;
using Vespertine.Progression;
using Vespertine.Save;

namespace Vespertine.UI
{
    /// <summary>Mission intro card, debrief, death / failure, endings and the debug console.</summary>
    public partial class UIManager
    {
        // ================================================================== mission intro
        public void MissionIntro(MissionInfo info, LevelData data)
        {
            if (info == null) return;
            var root = E("scrim");
            root.style.justifyContent = Justify.Center;
            root.style.alignItems = Align.Center;
            var c = Col();
            c.style.alignItems = Align.Center;
            c.style.maxWidth = 960;
            int idx = Missions.IndexOf(info.Id);
            c.Add(L(idx >= 0 ? $"{Missions.ActName(info.Act).ToUpperInvariant()}   ·   NIGHT {idx + 1}" : "", "tiny", "moon"));
            c.Add(L(info.Title, "title"));
            c.Add(L(data != null ? data.Get("subtitle", info.Place) : info.Place, "subtitle"));   // the level file names the night
            c.Add(Space(14));
            c.Add(L(info.Pitch, "h3"));
            var brief = data != null ? data.Get("briefing", "") : "";
            if (!string.IsNullOrEmpty(brief))
            {
                var b = L(brief.Replace("\\n", "\n"), "body");
                b.style.unityTextAlign = TextAnchor.MiddleCenter;
                b.style.maxWidth = 820;
                b.style.marginTop = 10;
                c.Add(b);
            }
            c.Add(Space(14));
            int shown = 0;
            foreach (var o in Game.Mission.Objectives)
            {
                if (!o.Visible || !o.Primary || shown >= 4) continue;
                c.Add(L("•  " + o.Spec.Text, "body", "gold"));
                shown++;
            }
            var cms = Game.ActiveCountermeasures;
            if (cms.Count > 0)
            {
                c.Add(Space(10));
                if (Game.MissionCounters.Count > 0) c.Add(L("Vane knows her: " + string.Join(", ", cms.Select(Habits.CountermeasureName)), "small", "bad"));
                else
                {
                    // QW13: each answer with the deed that called it
                    c.Add(L("THE VIGIL HAS ADAPTED", "tiny", "bad"));
                    foreach (var cm in cms)
                    {
                        var why = Game.Campaign?.AnswerSource(cm);
                        c.Add(L((why != null ? why + ": tonight, " : "") + Habits.CountermeasureName(cm).ToLowerInvariant() + ".", "small", "bad"));
                    }
                }
            }
            c.Add(Space(22));
            var go = B("Begin", () => PopScreen("intro"), "btn-primary", "btn-big");
            c.Add(go);
            c.Add(L("Esc / Enter", "tiny", "dim"));
            root.Add(c);
            root.RegisterCallback<KeyDownEvent>(e => { if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.Space || e.keyCode == KeyCode.KeypadEnter) PopScreen("intro"); });
            PushScreen("intro", root, () => PopScreen("intro"));
            go.schedule.Execute(() => go.Focus()).StartingIn(50);
            Game.Audio?.Play2D("sting_intro", 0.7f);
        }

        // ================================================================== debrief
        public void ShowDebrief(MissionResult r)
        {
            ClearScreens();
            HideModals();
            ClearSubtitles();
            var root = E("scrim-solid", "center");
            var p = Col("panel", "modal-wide");
            p.Add(L("DAWN COMES. SHE IS GONE.", "tiny", "moon"));
            p.Add(L(r.Title, "title"));
            p.Add(L($"Survived in {FormatTime(r.Time)}" + (r.BestTime > 0 && r.BestTime < r.Time - 0.5f ? $"   ·   best {FormatTime(r.BestTime)}" : r.FirstClear ? "" : "   ·   new best"), "subtitle"));
            p.Add(E("divider"));

            var row = Row();
            row.style.alignItems = Align.FlexStart;
            // ---- left: what happened
            var left = Col();
            left.style.width = Length.Percent(31); left.style.marginRight = 24;
            left.Add(L("THE NIGHT", "h3"));
            left.Add(StatRow("Killed", r.Kills.ToString()));
            left.Add(StatRow("Sipped from", r.Sips.ToString()));
            left.Add(StatRow("Drained", r.Drains.ToString()));
            left.Add(StatRow("Times spotted", r.TimesSpotted.ToString()));
            left.Add(StatRow("Alarms raised", r.Alarms.ToString()));
            left.Add(StatRow("Bodies found", r.BodiesFound.ToString()));
            if (r.Disposed > 0) left.Add(StatRow("Bodies hidden", r.Disposed.ToString()));
            left.Add(StatRow("Loads", r.Loads.ToString()));
            if (r.Won)
            {
                // the night's challenges: earned in gold (NEW the first time), the rest dim
                left.Add(Space(8));
                left.Add(L("CHALLENGES", "tiny", "moon"));
                foreach (var d in Progression.Challenges.All)
                {
                    bool got = r.Challenges.Contains(d.Id), fresh = r.NewChallenges.Contains(d.Id);
                    string text = d.Id == Progression.Challenges.Swift && r.Par > 0f ? $"{d.Text} ({Progression.Challenges.FormatPar(r.Par)})" : d.Text;
                    var cl = L((got ? "◆  " : "◇  ") + d.Name + (fresh ? $"  NEW  +{Progression.Challenges.MarkReward} Mark" : "") + "  <color=#7d7a86>—  " + text + "</color>", "small", got ? "gold" : "dim");
                    cl.enableRichText = true;
                    left.Add(cl);
                }
                if (r.Kills >= 10) left.Add(L("Reaper", "gold", "bold"));
            }
            row.Add(left);

            // ---- middle: objectives & secrets
            var mid = Col();
            mid.style.width = Length.Percent(34); mid.style.marginRight = 24;
            mid.Add(L("OBJECTIVES", "h3"));
            foreach (var o in Game.Mission.Objectives)
            {
                if (!o.Primary || !o.Visible) continue;
                mid.Add(L((o.Complete ? "√  " : "×  ") + o.Spec.Text, "small", o.Complete ? "good" : "bad"));
            }
            foreach (var o in r.Optionals) mid.Add(L("√  " + o + "  (optional)", "small", "good"));
            foreach (var o in r.OptionalsMissed) mid.Add(L("·  " + o + "  (optional)", "small", "dim"));
            if (r.Secrets.Count > 0)
            {
                mid.Add(Space(8));
                mid.Add(L("SECRETS", "h3"));
                foreach (var s in r.Secrets) mid.Add(L("◆  " + s, "small", "gold"));
            }
            foreach (var mc in mid.Children()) if (mc is Label ml) ml.style.whiteSpace = WhiteSpace.Normal;   // long objectives wrap in their column
            row.Add(mid);

            // ---- right: what she gained
            var right = Col();
            right.style.flexGrow = 1; right.style.flexShrink = 1; right.style.minWidth = 0;
            right.Add(L("WHAT SHE TOOK", "h3"));
            var c = Game.Campaign;
            right.Add(StatRow("Vitae", $"+{r.VitaeEarned}"));
            right.Add(StatRow("Marks", $"+{r.MarksEarned}" + (c != null ? $"   ({c.Marks} unspent)" : "")));
            if (c != null)
            {
                var nt = c.NextThreshold;
                int prev = CampaignState.VitaeThresholds[Mathf.Clamp(c.Awakening - 1, 0, CampaignState.VitaeThresholds.Length - 1)];
                var bar = Bar("bar-vitae", out var vf);
                vf.style.width = Length.Percent(0);
                float target = nt > 0 ? Mathf.InverseLerp(prev, nt, c.Vitae) : 1f;
                vf.schedule.Execute(() => vf.style.width = Length.Percent(target * 100f)).StartingIn(400);
                right.Add(Space(4));
                right.Add(bar);
                right.Add(L($"Awakening {c.Awakening}" + (nt > 0 ? $" — {nt - c.Vitae} vitae to the next" : ""), "tiny", "dim"));
            }
            if (r.AwakeningAfter > r.AwakeningBefore)
            {
                right.Add(Space(8));
                var aw = L($"SHE AWAKENS — {r.AwakeningAfter}", "h2", "blood");
                right.Add(aw);
                right.Add(L(AwakeningText(r.AwakeningBefore, r.AwakeningAfter), "small"));
                root.schedule.Execute(() => Game.Audio?.Play2D("awaken", 0.8f)).StartingIn(700);
            }
            right.Add(Space(8));
            right.Add(L("THE CITY", "h3"));
            right.Add(L($"Terror +{r.TerrorDelta}   ·   Rumour +{r.RumourDelta}", "small"));
            if (r.NewCountermeasures.Count > 0)
            {
                right.Add(Space(8));
                right.Add(L("THE VIGIL ADAPTS", "h3", "bad"));
                foreach (var cm in r.NewCountermeasures)
                {
                    right.Add(L(Habits.CountermeasureName(cm), "small", "bad", "bold"));
                    right.Add(L(Habits.CountermeasureText(cm), "tiny"));
                }
            }
            foreach (var n in r.Notes) { var nl = L(n, "small", "italic"); nl.style.whiteSpace = WhiteSpace.Normal; right.Add(nl); }
            row.Add(right);
            p.Add(row);

            p.Add(Space(18));
            var foot = Row();
            foot.style.justifyContent = Justify.SpaceBetween;
            var replay = B("Replay this night", () => Confirm("Replay?", "Play the mission again from the start. This result is kept.", "Replay", () => Game.Root.StartMission(r.MissionId)), "btn-small");
            foot.Add(replay);
            if (r.Detections.Count > 0)
                foot.Add(B($"Detections ({r.Detections.Count})", () => ShowDetections(r), "btn-small"));
            foot.Add(B("Continue", () => Game.Root.AfterDebrief(r), "btn-primary", "btn-big"));
            p.Add(foot);
            root.Add(p);
            PushScreen("debrief", root, null);
        }

        /// <summary>The debrief's Detections page (SR.10): a top-down sketch and the caption for each time she was spotted.</summary>
        void ShowDetections(MissionResult r)
        {
            var p = Col("panel", "modal-wide");
            p.Add(L("DETECTIONS", "h2"));
            p.Add(L("Each time a guard spotted her: where he stood, his cone as the walls cut it, the edge she crossed in white.", "small", "dim"));
            p.Add(E("divider"));
            var sv = new ScrollView(ScrollViewMode.Vertical);
            sv.style.maxHeight = 520;
            sv.style.flexGrow = 0; sv.style.flexShrink = 1;
            foreach (var d in r.Detections)
            {
                var row = Row();
                row.style.alignItems = Align.FlexStart;
                row.style.marginBottom = 12;
                row.Add(new DetectionSketch(d));
                var text = Col();
                text.style.marginLeft = 16; text.style.flexShrink = 1; text.style.minWidth = 0;
                text.Add(L($"{FormatTime(d.Time)}   ·   {d.Who}", "small", "moon"));
                var cap = L(d.Caption ?? "Seen.", "body");
                cap.style.whiteSpace = WhiteSpace.Normal;
                text.Add(cap);
                row.Add(text);
                sv.Add(row);
            }
            if (r.TimesSpotted > r.Detections.Count)
                sv.Add(L(r.Detections.Count >= SpottedRecord.Max ? $"…and {r.TimesSpotted - r.Detections.Count} more." : "Sightings before a load are not shown.", "small", "dim"));
            p.Add(sv);
            p.Add(Space(10));
            VisualElement scrim = null;
            var close = B("Close", () => CloseModal(scrim), "btn-primary");
            close.style.alignSelf = Align.FlexEnd;
            p.Add(close);
            scrim = PushModal(p, () => CloseModal(scrim));
        }

        static string AwakeningText(int before, int after)
        {
            var lines = new List<string>();
            lines.Add($"Vitality {CampaignState.MaxHpFor(before):0} → {CampaignState.MaxHpFor(after):0}.  Blood {CampaignState.MaxBloodFor(before):0} → {CampaignState.MaxBloodFor(after):0}.");
            if (CampaignState.SlotsFor(after) > CampaignState.SlotsFor(before)) lines.Add($"A new art slot opens ({CampaignState.SlotsFor(after)}).");
            int[] tierAt = { 3, 5, 8 };
            for (int i = 0; i < tierAt.Length; i++) if (before < tierAt[i] && after >= tierAt[i]) lines.Add($"Tier {i + 2} of the Blood Arts opens.");
            return string.Join("\n", lines);
        }

        // ================================================================== death / failure
        public void ShowDeath(string cause)
        {
            ShowFailScreen("SHE FALLS", DeathLine(cause));
        }

        public void ShowMissionFailed(string reason)
        {
            ShowFailScreen("THE NIGHT IS LOST", reason);
        }

        static string DeathLine(string cause)
        {
            if (string.IsNullOrEmpty(cause)) return "The dark takes her back.";
            switch (cause)
            {
                case "light": return "The light found her. What is left is ash and a scorched coat.";
                case "sun": case "dawn": return "Dawn came. She did not reach the dark in time.";
                case "starved": case "hunger": return "The hunger ate her from within.";
                case "wounds": return "Too many wounds, too little blood.";
                case "drowned": return "The river does not give back what it takes twice.";
            }
            return $"Struck down by {cause}.";
        }

        void ShowFailScreen(string title, string line)
        {
            ClearScreens();
            HideModals();
            ClearSubtitles();
            var root = E("scrim");
            root.style.backgroundColor = new Color(0.12f, 0f, 0.02f, 0.82f);
            root.style.justifyContent = Justify.Center;
            root.style.alignItems = Align.Center;
            var c = Col();
            c.style.alignItems = Align.Center;
            c.Add(L(title, "title", "blood"));
            c.Add(L(line, "subtitle"));
            c.Add(Space(26));
            bool iron = Game.Campaign != null && Game.Campaign.Ironblood;
            var latest = iron ? null : SaveSystem.Latest();
            if (latest != null && Game.Mission.Info != null && latest.MissionId == Game.Mission.Info.Id)
            {
                var lb = B($"Load last save  ({SaveSystem.SlotName(latest.Slot)})", () => Game.Root.LoadMissionSave(SaveSystem.Profile, latest.Slot), "btn-big", "btn-primary");
                c.Add(lb);
                var sub = L(latest.Label, "tiny", "dim"); sub.style.marginBottom = 8; c.Add(sub);
            }
            if (!iron) c.Add(B("Load…", ShowLoadMenu, "btn-big"));
            c.Add(B("Restart mission", () => { ClearScreens(); Game.Root.RestartMission(); }, "btn-big", latest == null ? "btn-primary" : "btn"));
            c.Add(B("Return to the Refuge", () => Game.Root.ReturnToHub(true), "btn-big"));
            c.Add(B("Main menu", () => Game.Root.QuitToMenu(), "btn-big"));
            if (iron) c.Add(L("Ironblood: the night begins again.", "small", "dim"));
            root.Add(c);
            PushScreen("fail", root, null);
        }

        // ================================================================== ending
        public void ShowEnding(string id)
        {
            ClearScreens();
            HideModals();
            SetHudVisible(false);
            var (title, text) = EndingText(id);
            var root = E("scrim-solid");
            root.style.justifyContent = Justify.Center;
            root.style.alignItems = Align.Center;
            var c = Col();
            c.style.alignItems = Align.Center;
            c.style.maxWidth = 980;
            c.Add(L("VESPERTINE", "tiny", "moon"));
            c.Add(L(title, "title"));
            c.Add(Space(20));
            var body = L(text, "body", "italic");
            body.style.unityTextAlign = TextAnchor.MiddleCenter;
            body.style.fontSize = 20;
            c.Add(body);
            c.Add(Space(30));
            var camp = Game.Campaign;
            if (camp != null)
            {
                // what became of them: one line at a time, after the ending itself has been read
                int k = 0;
                foreach (var line in camp.Epilogue(id))
                {
                    var el = L(line, "small");
                    el.style.unityTextAlign = TextAnchor.MiddleCenter;
                    el.style.whiteSpace = WhiteSpace.Normal;
                    el.style.marginBottom = 6;
                    el.style.opacity = 0f;
                    el.style.transitionProperty = new List<StylePropertyName> { new StylePropertyName("opacity") };
                    el.style.transitionDuration = new List<TimeValue> { new TimeValue(1.2f, TimeUnit.Second) };
                    el.schedule.Execute(() => el.style.opacity = 1f).StartingIn(3500 + 1600 * k++);
                    c.Add(el);
                }
                c.Add(Space(20));
                var t = TimeSpan.FromSeconds(camp.PlayTime);
                c.Add(L($"Terror {camp.Terror}  ·  Rumour {camp.Rumour}  ·  Killed {camp.TotalKills}  ·  Drained {camp.TotalDrains}  ·  Sipped {camp.TotalSips}  ·  {(int)t.TotalHours}h {t.Minutes:00}m", "small", "dim"));
            }
            c.Add(Space(20));
            c.Add(B("Return to the Refuge", () => Game.Root.ReturnToHub(false), "btn-big", "btn-primary"));
            c.Add(B("Main menu", () => Game.Root.QuitToMenu(), "btn-big"));
            root.Add(c);
            body.style.opacity = 0f;
            body.schedule.Execute(() => body.style.opacity = 1f).StartingIn(800);
            PushScreen("ending", root, null, false);
        }

        /// <summary>A blood-dream between missions: the lines surface one at a time over a slow heartbeat. The button
        /// first shows every line at once, then wakes her into the Refuge.</summary>
        public void ShowDream(Interludes.Dream d, Action onWake)
        {
            ClearScreens();
            HideModals();
            SetHudVisible(false);
            var root = E("scrim-solid");
            root.style.backgroundColor = new Color(0.07f, 0.015f, 0.025f);
            root.style.justifyContent = Justify.Center;
            root.style.alignItems = Align.Center;
            var c = Col();
            c.style.alignItems = Align.Center;
            c.style.maxWidth = 900;
            c.style.paddingLeft = c.style.paddingRight = 24;
            var mi = Missions.Get(d.MissionId);
            int n = mi != null ? Missions.IndexOf(d.MissionId) + 1 : 0;
            c.Add(L(n > 0 ? $"BLOOD-DREAM  ·  AFTER NIGHT {n}" : "BLOOD-DREAM", "tiny", "moon"));
            var title = L(d.Title, "title");
            title.style.fontSize = 44;
            title.style.color = new Color(0.86f, 0.62f, 0.6f);
            c.Add(title);
            c.Add(Space(26));
            var shown = new List<VisualElement>();
            foreach (var line in d.Lines)
            {
                bool nar = string.IsNullOrEmpty(line.Speaker);
                var el = L(nar || line.Speaker == Interludes.Abbess ? line.Text : $"<color=#8d8378>{line.Speaker.ToUpperInvariant()}  —</color>  {line.Text}", "body");
                el.enableRichText = true;
                el.style.whiteSpace = WhiteSpace.Normal;
                el.style.unityTextAlign = TextAnchor.MiddleCenter;
                el.style.marginBottom = 12;
                el.style.fontSize = 19;
                if (nar) { el.AddToClassList("italic"); el.style.color = new Color(0.62f, 0.58f, 0.62f); }
                else if (line.Speaker == Interludes.Abbess) { el.AddToClassList("italic"); el.style.color = new Color(0.86f, 0.36f, 0.36f); el.style.fontSize = 21; }
                else el.style.color = new Color(0.9f, 0.87f, 0.8f);
                el.style.opacity = 0f;
                el.style.transitionProperty = new List<StylePropertyName> { new StylePropertyName("opacity") };
                el.style.transitionDuration = new List<TimeValue> { new TimeValue(1.1f, TimeUnit.Second) };
                c.Add(el);
                shown.Add(el);
            }
            int next = 0;
            Button wake = null;
            void Reveal(bool all)
            {
                while (next < shown.Count)
                {
                    var line = d.Lines[next];
                    shown[next++].style.opacity = 1f;
                    if (line.Speaker == Interludes.Abbess) Game.Audio?.Play2D("whisper", 0.25f, 0.8f);
                    if (!all) break;
                }
                if (next >= shown.Count && wake != null) wake.text = "Wake";
            }
            c.Add(Space(24));
            wake = B("Show all", () =>
            {
                if (next < shown.Count) { Reveal(true); return; }
                if (Game.Audio != null) { Game.Audio.HeartRate = 0f; Game.Audio.HeartVolume = 0f; }
                onWake?.Invoke();
            }, "btn-big", "btn-primary");
            c.Add(wake);
            root.Add(c);
            if (Game.Audio != null) { Game.Audio.HeartRate = 0.8f; Game.Audio.HeartVolume = 0.35f; }
            // one line every 2.6 s, after a beat of red silence
            root.schedule.Execute(() => Reveal(false)).StartingIn(900).Every(2600).Until(() => next >= shown.Count);
            PushScreen("dream", root, null, false);
        }

        public static (string, string) EndingText(string id)
        {
            switch (id)
            {
                case "endless_night":
                    return ("Endless Night", "The chains fall. The Abbess rises, and the lamps of Ostmere go out one district at a time.\nNo one hunts the dark any more. The dark hunts them.\nIlse walks beside her maker, and the city learns at last to hide.");
                case "night_court":
                    return ("The Night Court", "The Abbess is freed, and the Faithful come out of the cellars to kneel.\nOstmere is ruled after dusk by a court of pale things who keep the old bargains: blood for protection, silence for mercy.\nThe city fears the night. It also leaves its doors unlocked.");
                case "new_abbess":
                    return ("The New Abbess", "She drinks the Abbess dry, and something ancient settles behind her eyes.\nThe Vigil is broken. In its place grows a cult that leaves offerings on rooftops and whispers her name to frighten children.\nOstmere has a new god. It is hungry.");
                case "pale_lady":
                    return ("The Pale Lady", "She drinks the Abbess dry and does not become her.\nIn the slums they tell of a lady in grey who walks the alleys after midnight. Thieves vanish. Debts are forgiven. The sick wake rested.\nShe takes only what she needs. Mostly.");
                case "ashes":
                    return ("Ashes at Dawn", "She burns the vault, the vitae, the Abbess, and the trade that made her.\nThere is no one left who will speak her name kindly, and she does not ask them to.\nWhen the sun clears the cathedral roof, she is standing in it.");
                case "dawn_tobias":
                    return ("Dawn", "The vault burns. The vitae burns. The Abbess is ash.\nTobias is waiting on the cathedral steps with a borrowed coat and nothing to say.\nThey watch the sky go pale together. She does not move into the shade.");
                case "dawn":
                    return ("Dawn", "The vault burns. The vitae burns. The Abbess is ash.\nShe walks out into the fens before first light, and the reeds close behind her.\nSome say she is still out there. Some say she found the sun.");
            }
            return ("The End", "The long night is over.");
        }

        // ================================================================== debug console
        bool _consoleOpen;
        VisualElement _console;
        TextField _consoleInput;
        Label _consoleLog;
        readonly List<string> _consoleLines = new List<string>();
        readonly List<string> _consoleHistory = new List<string>();
        int _historyIdx = -1;
        bool _fpsOn;
        Label _fps;
        float _fpsT, _fpsAcc; int _fpsN;

        void ToggleConsole()
        {
            if (_console == null) BuildConsole();
            _consoleOpen = !_consoleOpen;
            Show(_console, _consoleOpen);
            if (_consoleOpen)
            {
                _consoleInput.value = "";
                _consoleInput.schedule.Execute(() => _consoleInput.Focus()).StartingIn(20);
            }
            RefreshPause();
        }

        void BuildConsole()
        {
            _console = E("console");
            _consoleLog = L("", "console-log");
            _consoleLog.enableRichText = true;
            var sv = new ScrollView(ScrollViewMode.Vertical);
            sv.style.flexGrow = 1;
            sv.Add(_consoleLog);
            _console.Add(sv);
            _consoleInput = new TextField();
            _consoleInput.isDelayed = false;
            _consoleInput.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
                {
                    var cmd = _consoleInput.value.Trim();
                    _consoleInput.value = "";
                    if (cmd.Length > 0) { _consoleHistory.Add(cmd); _historyIdx = -1; RunCommand(cmd); }
                    sv.schedule.Execute(() => sv.scrollOffset = new Vector2(0, float.MaxValue)).StartingIn(10);
                    e.StopPropagation();
                }
                else if (e.keyCode == KeyCode.UpArrow && _consoleHistory.Count > 0)
                {
                    _historyIdx = _historyIdx < 0 ? _consoleHistory.Count - 1 : Mathf.Max(0, _historyIdx - 1);
                    _consoleInput.value = _consoleHistory[_historyIdx];
                }
                else if (e.keyCode == KeyCode.BackQuote || e.keyCode == KeyCode.F1) { e.StopPropagation(); }
            }, TrickleDown.TrickleDown);
            _console.Add(_consoleInput);
            Show(_console, false);
            _topLayer.Add(_console);
            Log("<b>Vespertine console</b> — type <b>help</b>.");
        }

        void Log(string s)
        {
            _consoleLines.Add(s);
            while (_consoleLines.Count > 120) _consoleLines.RemoveAt(0);
            if (_consoleLog != null) _consoleLog.text = string.Join("\n", _consoleLines);
        }

        void TickConsole()
        {
            if (_fpsOn)
            {
                if (_fps == null) { _fps = L("", "tiny"); _fps.style.position = Position.Absolute; _fps.style.right = 8; _fps.style.top = 4; _fps.pickingMode = PickingMode.Ignore; _topLayer.Add(_fps); }
                _fpsAcc += Time.unscaledDeltaTime; _fpsN++;
                if (Time.unscaledTime > _fpsT) { _fpsT = Time.unscaledTime + 0.5f; _fps.text = $"{_fpsN / Mathf.Max(0.001f, _fpsAcc):0} fps  {_fpsAcc / Mathf.Max(1, _fpsN) * 1000f:0.0} ms"; _fpsAcc = 0; _fpsN = 0; }
            }
            if (_fps != null) Show(_fps, _fpsOn);
        }

        static float Num(string[] a, int i, float fallback) => a.Length > i && float.TryParse(a[i], NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : fallback;

        void RunCommand(string line)
        {
            Log("> " + line);
            var a = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var c = Game.Campaign;
            var p = Game.Player;
            try
            {
                switch (a[0].ToLowerInvariant())
                {
                    case "help":
                        Log("mission <id> · win · lose · restart · hub · menu · ending <id>\n" +
                            "vitae <n> · marks <n> · unlock <id|all> · grant <id> · awaken\n" +
                            "god · notarget · heal · blood [n] · alarm <0-3> · calm · tp (to cursor) · kill (hovered)\n" +
                            "reveal (all objectives) · complete <objective> · flag <name> · habit <key> <n>\n" +
                            "timescale <x> · fps · save <slot> · load <slot> · clear");
                        break;
                    case "clear": _consoleLines.Clear(); Log(""); break;
                    case "mission": if (c == null) Game.Root.NewGame(SaveSystem.Profile, Difficulty.Hunter, false, false); Game.Root.StartMission(a.Length > 1 ? a[1] : "m01"); ToggleConsole(); break;
                    case "win": if (Game.InMission) { Game.Mission.Win(); ToggleConsole(); } break;
                    case "lose": if (Game.InMission) { Game.Mission.Lose("Debug"); ToggleConsole(); } break;
                    case "restart": if (Game.InMission) { ClearScreens(); Game.Root.RestartMission(); ToggleConsole(); } break;
                    case "hub": Game.Root.ReturnToHub(true); ToggleConsole(); break;
                    case "menu": Game.Root.QuitToMenu(); ToggleConsole(); break;
                    case "ending": Game.Root.ShowEnding(a.Length > 1 ? a[1] : "dawn"); ToggleConsole(); break;
                    case "vitae": if (c != null) { int g = c.AddVitae((int)Num(a, 1, 100)); Log($"vitae {c.Vitae}, awakening {c.Awakening}" + (g > 0 ? $" (+{g})" : "")); } break;
                    case "marks": if (c != null) { c.Marks += (int)Num(a, 1, 5); Log($"marks {c.Marks}"); } break;
                    case "awaken": if (c != null) { c.Vitae = CampaignState.VitaeThresholds[CampaignState.VitaeThresholds.Length - 1]; Log($"awakening {c.Awakening}"); } break;
                    case "unlock":
                        if (c == null) break;
                        if (a.Length > 1 && a[1] == "all") { foreach (var n in Skills.Nodes) if (!c.Has(n.Id)) c.Nodes.Add(n.Id); c.ClampLoadout(); foreach (var id in c.OwnedAbilities()) c.Equip(id); Log("all arts learned"); }
                        else if (a.Length > 1) { c.Grant(a[1]); Log(c.Has(a[1]) ? "learned " + a[1] : "unknown node"); }
                        break;
                    case "grant": if (c != null && a.Length > 1) { c.Grant(a[1]); Log("granted " + a[1]); } break;
                    case "god": Vampire.GodMode = !Vampire.GodMode; Log("god " + (Vampire.GodMode ? "on" : "off")); break;
                    case "notarget": Vampire.NoTarget = !Vampire.NoTarget; Log("notarget " + (Vampire.NoTarget ? "on" : "off")); break;
                    case "heal": if (p) { p.Heal(9999); Log("healed"); } break;
                    case "blood": if (p) { p.AddBlood(Num(a, 1, 999)); Log("blood " + p.Blood.ToString("0")); } break;
                    case "alarm":
                    {
                        int lv = (int)Num(a, 1, 2);
                        if (lv >= 3) Game.AI?.RaiseLockdown("debug"); else GameEvents.RaiseAlarm(lv);
                        Log("alarm " + lv);
                        break;
                    }
                    case "calm":
                        if (Game.AI != null) { foreach (var n in Game.AI.Npcs) if (n && n.IsAlive) n.Detection = 0f; GameEvents.RaiseAlarm(0); Log("calmed"); }
                        break;
                    case "tp":
                        if (p && Game.Cam != null && Physics.Raycast(Game.Cam.MouseRay(), out var hit, 500f, Layers.GroundMask))
                        {
                            if (p.Agent && p.Agent.enabled) p.Agent.Warp(hit.point); else p.transform.position = hit.point;
                            Log($"teleported to {hit.point}");
                        }
                        break;
                    case "kill": if (p && p.HoverNpc && p.HoverNpc.IsAlive) { p.HoverNpc.Die("debug"); Log("killed"); } break;
                    case "reveal": if (Game.InMission) { foreach (var o in Game.Mission.Objectives) Game.Mission.Reveal(o.Id); Log("revealed"); } break;
                    case "complete": if (Game.InMission && a.Length > 1) Game.Mission.CompleteObjective(a[1]); break;
                    case "flag": if (c != null && a.Length > 1) { c.SetFlag(a[1]); Log("flag " + a[1]); } break;
                    case "habit": if (c != null && a.Length > 2) { c.AddHabit(a[1], Num(a, 2, 1)); Log($"{a[1]} = {c.Habit(a[1]):0.0}"); } break;
                    case "timescale": Game.SlowMo = Mathf.Clamp(Num(a, 1, 1f), 0.05f, 8f); Game.ApplyTime(); Log("timescale " + Game.SlowMo); break;
                    case "fps": _fpsOn = !_fpsOn; break;
                    case "save": if (Game.InMission) Log(Game.Mission.SaveTo(a.Length > 1 ? a[1] : "s7", "Debug save") ? "saved" : "failed: " + SaveSystem.LastError); break;
                    case "load": { var s = SaveSystem.ReadMission(a.Length > 1 ? a[1] : "s7"); if (s != null) { Game.Mission.LoadSave(s); ToggleConsole(); } else Log("no save"); break; }
                    default: Log("unknown command"); break;
                }
            }
            catch (Exception e) { Log("<color=#e06a60>" + e.Message + "</color>"); Debug.LogException(e); }
        }
    }
}
