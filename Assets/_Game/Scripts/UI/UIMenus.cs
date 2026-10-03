using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Vespertine.Controls;
using Vespertine.Core;
using Vespertine.Data;
using Vespertine.Progression;
using Vespertine.Save;

namespace Vespertine.UI
{
    /// <summary>Main menu, new game, load, pause, settings, save slots.</summary>
    public partial class UIManager
    {
        // ================================================================== main menu
        public void ShowMainMenu()
        {
            ClearScreens();
            HideModals();
            var root = E("scrim-solid", "center");
            var wrap = E("menu-title-wrap");
            wrap.Add(L("VESPERTINE", "title"));
            wrap.Add(L("a vampire stealth tactics game", "subtitle"));
            root.Add(wrap);

            var col = E("menu-col");
            int recent = SaveSystem.MostRecentProfile();
            if (recent >= 0)
            {
                var c = SaveSystem.LoadCampaign(recent);
                string sub = c != null ? $"{ProfileLine(c)}" : "";
                var cont = B("Continue", () => Game.Root.ContinueProfile(recent), "btn-big", "btn-primary");
                col.Add(cont);
                if (sub != "") { var s = L(sub, "small", "dim"); s.style.marginLeft = 18; s.style.marginBottom = 10; col.Add(s); }
            }
            col.Add(B("New Night", ShowNewGame, "btn-big"));
            var load = B("Load", ShowLoadMenu, "btn-big");
            load.SetEnabled(AnyProfile());
            col.Add(load);
            if (Game.Settings != null && (Game.Settings.ReadabilityTest || Game.Settings.MovementTest))
                col.Add(B("Test maps", ShowTestMaps, "btn-big"));
            col.Add(B("Settings", () => ShowSettings(), "btn-big"));
            col.Add(B("Credits", ShowCredits, "btn-big"));
            col.Add(B("Quit", () => Game.Root.QuitGame(), "btn-big"));
            root.Add(col);

            var ver = L($"v{Vespertine.Core.GameRoot.Version}   ·   ` debug console", "tiny", "dim");
            ver.style.position = Position.Absolute; ver.style.right = 18; ver.style.bottom = 12;
            root.Add(ver);
            var quote = L("“I begin as something hiding from humans. By the end, humans are hiding from me.”", "quote");
            quote.style.position = Position.Absolute; quote.style.left = 80; quote.style.bottom = 48; quote.style.maxWidth = 640;
            root.Add(quote);
            PushScreen("main", root, null, false);
        }

        static bool AnyProfile()
        {
            for (int i = 0; i < SaveSystem.Profiles; i++) if (SaveSystem.ProfileExists(i)) return true;
            return false;
        }

        static string ProfileLine(CampaignState c)
        {
            var next = c.MissionIndex < Missions.All.Count ? Missions.All[c.MissionIndex].Title : "The end";
            var t = TimeSpan.FromSeconds(c.PlayTime);
            return $"{Difficulties.Get(c.Difficulty).Name}{(c.Ironblood ? " · Ironblood" : "")} · Awakening {c.Awakening} · Next: {next} · {(int)t.TotalHours}h {t.Minutes:00}m";
        }

        // ================================================================== new game
        int _ngProfile = -1;
        Difficulty _ngDifficulty = Difficulty.Hunter;
        bool _ngIronblood;

        /// <summary>The playtest maps (§44), shown on the main menu while a test mode is on. Each is played on a throwaway
        /// campaign in a sandbox folder (<see cref="Vespertine.Core.GameRoot.StartPlaytest"/>), so a tester's own nights are untouched.</summary>
        void ShowTestMaps()
        {
            var root = E("scrim-solid");
            PushScreen("testmaps", root, PopScreen, false);
            var p = Col("panel", "modal-wide");
            p.style.alignSelf = Align.Center;
            p.style.marginTop = 60;
            p.Add(L("TEST MAPS", "h1"));
            var intro = L("Played on a fresh, throwaway campaign: your own saves are not touched. Each map's log goes to the readability/ or movement/ folder beside your saves.", "dim");
            intro.style.whiteSpace = WhiteSpace.Normal;
            p.Add(intro);
            p.Add(E("divider"));
            foreach (var (id, name, use) in new[]
            {
                ("gym", "Readability Gym", "Readability Test: lamps, cones and their edges"),
                ("movegym", "Movement Gym", "Movement Test: corners, doors and clutter"),
                ("m02", "Lantern Street (M02)", "both tests"),
                ("m05", "The Guildhall of Lamps (M05)", "Readability Test"),
            })
            {
                var row = Row();
                var b = B(name, () => Game.Root.StartPlaytest(id), "btn-big");
                b.style.width = 340;
                row.Add(b);
                var u = L(use, "dim");
                u.style.alignSelf = Align.Center; u.style.marginLeft = 14;
                row.Add(u);
                p.Add(row);
            }
            p.Add(E("divider"));
            p.Add(B("Back", PopScreen, "btn-big"));
            root.Add(p);
        }

        void ShowNewGame()
        {
            _ngProfile = -1;
            for (int i = 0; i < SaveSystem.Profiles; i++) if (!SaveSystem.ProfileExists(i)) { _ngProfile = i; break; }
            if (_ngProfile < 0) _ngProfile = 0;
            _ngDifficulty = Difficulty.Hunter;
            _ngIronblood = false;
            var root = E("scrim-solid");
            PushScreen("newgame", root, PopScreen, false);
            BuildNewGame(root);
        }

        void BuildNewGame(VisualElement root)
        {
            root.Clear();
            var p = Col("panel", "modal-wide");
            p.style.alignSelf = Align.Center;
            p.style.marginTop = 60;
            p.Add(L("A NEW NIGHT", "h1"));
            p.Add(L("Coldwater Institute, the lower ward. Something wakes among the drowned.", "italic", "dim"));
            p.Add(E("divider"));

            p.Add(L("PROFILE", "h3"));
            var prow = Row();
            for (int i = 0; i < SaveSystem.Profiles; i++)
            {
                int idx = i;
                var c = SaveSystem.ProfileExists(i) ? SaveSystem.LoadCampaign(i) : null;
                var card = Col("list-item");
                card.style.flexGrow = 1; card.style.marginRight = 8;
                card.Add(L($"Profile {i + 1}", "bold"));
                card.Add(L(c != null ? ProfileLine(c) : "Empty", "tiny", c != null ? "bad" : "dim"));
                if (_ngProfile == i) card.AddToClassList("selected");
                card.RegisterCallback<ClickEvent>(_ => { _ngProfile = idx; Game.Audio?.Play2D("ui_click", 0.5f); BuildNewGame(root); });
                prow.Add(card);
            }
            p.Add(prow);
            if (SaveSystem.ProfileExists(_ngProfile)) p.Add(L("This profile's campaign will be overwritten.", "small", "bad"));

            p.Add(Space(10));
            p.Add(L("DIFFICULTY", "h3"));
            var drow = Row();
            foreach (Difficulty d in Enum.GetValues(typeof(Difficulty)))
            {
                var def = Difficulties.Get(d);
                var card = Col("card");
                card.style.flexGrow = 1; card.style.flexBasis = 0; // the three share the panel's width
                if (_ngDifficulty == d) card.AddToClassList("selected");
                card.Add(L(def.Name.ToUpperInvariant(), "h2"));
                card.Add(L(def.Description, "small"));
                card.Add(Space(6));
                card.Add(L($"Detection ×{def.Detection:0.##}   Blood ×{def.BloodGain:0.##}", "tiny", "dim"));
                card.Add(L(def.ConesAwareOnly ? "Unaware guards: near sight only" : def.ConeNear > 4f ? "Cones show from 6 m" : "Cones show from 4 m", "tiny", "dim"));
                card.Add(L(def.AutosaveMode == 0 ? "Frequent autosaves" : def.AutosaveMode == 1 ? "Autosave at objectives" : "Autosave at mission start only", "tiny", "dim"));
                var dd = d;
                card.RegisterCallback<ClickEvent>(_ => { _ngDifficulty = dd; Game.Audio?.Play2D("ui_click", 0.5f); BuildNewGame(root); });
                drow.Add(card);
            }
            p.Add(drow);
            p.Add(Space(8));
            var iron = Row("setting-row");
            iron.Add(L("Ironblood (no saving or loading; death ends the night and restarts the mission)", "setting-label"));
            iron.Add(Toggle(() => _ngIronblood, v => { _ngIronblood = v; }));
            p.Add(iron);

            p.Add(Space(14));
            var r = Row(); r.style.justifyContent = Justify.SpaceBetween;
            r.Add(B("Back", PopScreen));
            r.Add(B("Begin", () =>
            {
                Action go = () => Game.Root.NewGame(_ngProfile, _ngDifficulty, _ngIronblood);
                if (SaveSystem.ProfileExists(_ngProfile)) Confirm("Overwrite profile?", $"Profile {_ngProfile + 1} will be erased.", "Overwrite", go);
                else go();
            }, "btn-primary", "btn-big"));
            p.Add(r);
            root.Add(p);
        }

        // ================================================================== load
        int _loadProfile = -1;

        public void ShowLoadMenu()
        {
            _loadProfile = Game.Campaign != null && Game.InMission ? SaveSystem.Profile : SaveSystem.MostRecentProfile();
            if (_loadProfile < 0) _loadProfile = 0;
            var root = E("scrim-solid");
            PushScreen("load", root, PopScreen);
            BuildLoad(root);
        }

        void BuildLoad(VisualElement root)
        {
            root.Clear();
            var p = Col("panel", "modal-wide");
            p.style.alignSelf = Align.Center; p.style.marginTop = 50;
            p.Add(L("LOAD", "h1"));
            var tabs = Row();
            bool inMission = Game.InMission;
            for (int i = 0; i < SaveSystem.Profiles; i++)
            {
                int idx = i;
                var t = B($"Profile {i + 1}", () => { _loadProfile = idx; BuildLoad(root); }, "tab");
                if (i == _loadProfile) t.AddToClassList("selected");
                t.SetEnabled(SaveSystem.ProfileExists(i) && !(inMission && i != SaveSystem.Profile));
                tabs.Add(t);
            }
            p.Add(tabs);
            p.Add(E("divider"));

            var camp = SaveSystem.ProfileExists(_loadProfile) ? SaveSystem.LoadCampaign(_loadProfile) : null;
            if (camp == null) p.Add(L("Empty profile.", "dim"));
            else
            {
                var head = Row(); head.style.justifyContent = Justify.SpaceBetween; head.style.alignItems = Align.Center;
                head.Add(L(ProfileLine(camp), "small"));
                if (!inMission) head.Add(B("Go to the Refuge", () => Game.Root.ContinueProfile(_loadProfile, true), "btn-small"));
                p.Add(head);
                p.Add(Space(8));
                var sv = new ScrollView(ScrollViewMode.Vertical);
                sv.style.maxHeight = 520;
                var saves = SaveSystem.ListMissionSaves(_loadProfile);
                if (saves.Count == 0) sv.Add(L("No mission saves. Saves are cleared when a mission is completed.", "dim", "small"));
                foreach (var s in saves)
                {
                    var info = s;
                    var item = Row("list-item");
                    item.style.justifyContent = Justify.SpaceBetween;
                    var left = Col();
                    var mi = Missions.Get(s.MissionId);
                    left.Add(L($"{SaveSystem.SlotName(s.Slot)} — {(mi != null ? mi.Title : s.MissionId)}", "bold"));
                    left.Add(L($"{s.Label}  ·  {s.Written.ToLocalTime():yyyy-MM-dd HH:mm}  ·  Awakening {s.Awakening}", "tiny", "dim"));
                    item.Add(left);
                    var btns = Row();
                    btns.Add(B("Load", () => DoLoad(info), "btn-small", "btn-primary"));
                    btns.Add(B("Delete", () => Confirm("Delete save?", SaveSystem.SlotName(info.Slot), "Delete", () => { SaveSystem.DeleteMission(info.Slot, _loadProfile); BuildLoad(root); }), "btn-small"));
                    item.Add(btns);
                    sv.Add(item);
                }
                p.Add(sv);
            }
            p.Add(Space(12));
            var back = B("Back", PopScreen);
            back.style.alignSelf = Align.FlexStart;
            p.Add(back);
            root.Add(p);
        }

        void DoLoad(SaveInfo s)
        {
            if (Game.Campaign != null && Game.Campaign.Ironblood && Game.InMission) { Alert("Ironblood", "Loading is disabled on an Ironblood night."); return; }
            Action go = () => Game.Root.LoadMissionSave(_loadProfile, s.Slot);
            if (Game.InMission && !Game.Mission.Ended) Confirm("Load this save?", "Progress since your last save will be lost.", "Load", go);
            else go();
        }

        // ================================================================== save
        public void ShowSaveMenu()
        {
            string why = null;
            if (Game.Mission == null || !Game.Mission.CanSave(out why)) { Alert("Cannot save", why ?? "Not in a mission."); return; }
            var root = E("scrim-solid");
            PushScreen("save", root, PopScreen);
            BuildSave(root);
        }

        void BuildSave(VisualElement root)
        {
            root.Clear();
            var p = Col("panel", "modal-wide");
            p.style.alignSelf = Align.Center; p.style.marginTop = 50;
            p.Add(L("SAVE", "h1"));
            p.Add(E("divider"));
            var existing = SaveSystem.ListMissionSaves();
            for (int i = 0; i < SaveSystem.ManualSlots; i++)
            {
                string slot = "s" + i;
                var info = existing.Find(x => x.Slot == slot);
                var item = Row("list-item");
                item.style.justifyContent = Justify.SpaceBetween;
                var left = Col();
                if (info != null)
                {
                    var mi = Missions.Get(info.MissionId);
                    left.Add(L($"Slot {i + 1} — {(mi != null ? mi.Title : info.MissionId)}", "bold"));
                    left.Add(L($"{info.Label}  ·  {info.Written.ToLocalTime():yyyy-MM-dd HH:mm}", "tiny", "dim"));
                }
                else left.Add(L($"Slot {i + 1} — empty", "dim"));
                item.Add(left);
                item.Add(B(info != null ? "Overwrite" : "Save", () =>
                {
                    Action go = () =>
                    {
                        var label = $"{Game.Mission.Info.Title}, {FormatTime(Game.Mission.MissionTime)}";
                        if (Game.Mission.SaveTo(slot, label)) { PopScreen("save"); SaveIndicator(); Toast("Saved to slot " + (int.Parse(slot.Substring(1)) + 1)); }
                        else Alert("Save failed", SaveSystem.LastError ?? "Unknown error");
                    };
                    if (info != null) Confirm("Overwrite?", $"Slot {i + 1} will be replaced.", "Overwrite", go);
                    else go();
                }, "btn-small", "btn-primary"));
                p.Add(item);
            }
            p.Add(Space(12));
            var back = B("Back", PopScreen);
            back.style.alignSelf = Align.FlexStart;
            p.Add(back);
            root.Add(p);
        }

        public static string FormatTime(float seconds)
        {
            var t = TimeSpan.FromSeconds(seconds);
            return t.TotalHours >= 1 ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}" : $"{t.Minutes}:{t.Seconds:00}";
        }

        // ================================================================== pause
        public void ShowPauseMenu()
        {
            if (HasScreen("pause") || !Game.InMission || Game.Mission.Ended) return;
            Game.Audio?.Play2D("ui_confirm", 0.4f, 0.8f);
            var root = E("scrim");
            var col = E("menu-col");
            var m = Game.Mission;
            var head = L(m.Info != null ? m.Info.Title.ToUpperInvariant() : "PAUSED", "h1");
            head.style.marginBottom = 2;
            col.Add(head);
            col.Add(L($"{(m.Info != null ? m.Info.Place : "")}  ·  {FormatTime(m.MissionTime)}  ·  {Difficulties.Current.Name}", "small", "dim"));
            col.Add(Space(18));
            col.Add(B("Resume", ResumeFromPause, "btn-big", "btn-primary"));
            bool iron = Game.Campaign != null && Game.Campaign.Ironblood;
            var save = B("Save", ShowSaveMenu, "btn-big"); save.SetEnabled(!iron); col.Add(save);
            var load = B("Load", ShowLoadMenu, "btn-big"); load.SetEnabled(!iron); col.Add(load);
            col.Add(B("Objectives & Briefing", () => ShowBriefingDoc(), "btn-big"));
            col.Add(B("Restart Mission", () => Confirm("Restart the mission?", "The campaign returns to how it was when this night began.", "Restart", () => { ClearScreens(); Game.Root.RestartMission(); }), "btn-big"));
            col.Add(B("Settings", () => ShowSettings(), "btn-big"));
            col.Add(B("Abandon to the Refuge", () => Confirm("Abandon the mission?", "Everything gained tonight is lost.", "Abandon", () => Game.Root.ReturnToHub(true)), "btn-big"));
            col.Add(B("Quit to Main Menu", () => Confirm("Quit to the main menu?", "Unsaved progress in this mission is lost.", "Quit", () => Game.Root.QuitToMenu()), "btn-big"));
            root.Add(col);
            var tips = Col("panel");
            tips.style.position = Position.Absolute; tips.style.right = 40; tips.style.bottom = 40; tips.style.width = 420;
            tips.Add(L("CONTROLS", "h3"));
            foreach (var line in ControlSummary()) tips.Add(L(line, "small"));
            root.Add(tips);
            PushScreen("pause", root, ResumeFromPause);
        }

        void ResumeFromPause()
        {
            PopScreen("pause");
            Game.Audio?.Play2D("ui_back", 0.4f);
        }

        internal static string MoveKeys(GameInput i)
        {
            var sb = new System.Text.StringBuilder();
            foreach (int b in new[] { 1, 3, 2, 4 }) if (b < i.Move.bindings.Count) sb.Append(GameInput.Display(i.Move, b).ToUpperInvariant());
            return sb.ToString();
        }

        IEnumerable<string> ControlSummary()
        {
            var i = Game.Input;
            if (i == null) yield break;
            yield return $"{MoveKeys(i)} move    hold {GameInput.Key(i.Sneak)} sneak (quiet)    hold {GameInput.Key(i.Run)} run (loud)";
            yield return $"{GameInput.Key(i.Traverse)} climb / drop / leap (or push into the wall or edge)    {GameInput.Key(i.Interact)} open doors, hide, use";
            yield return "LMB  use what is under the cursor / confirm a target    RMB  cancel, or back to Ilse";
            yield return $"{GameInput.Key(i.Sip)} sip    {GameInput.Key(i.Drain)} drain    {GameInput.Key(i.Interact)} use / snuff    {GameInput.Key(i.Carry)} carry body";
            yield return $"1–6 abilities    {GameInput.Key(i.BloodSense)} blood sense    {GameInput.Key(i.Pause)} pause";
            yield return $"Thralls: click a portrait or {GameInput.Key(i.CycleThrall)} to take control    {GameInput.Key(i.ThrallFollow)} follow Ilse / hold";
            yield return $"{(Game.Settings != null && Game.Settings.ConesKeyToggles ? "Press" : "Hold")} {GameInput.Key(i.ShowCones)} all cones    MMB click watch one cone    {GameInput.Key(i.Center)} re-centre camera";
            yield return $"Arrows / MMB drag look ahead    {GameInput.Display(i.Rotate, 1).ToUpperInvariant()}/{GameInput.Display(i.Rotate, 2).ToUpperInvariant()} rotate    wheel zoom";
            yield return $"{GameInput.Key(i.QuickSave)} quick save    {GameInput.Key(i.QuickLoad)} quick load";
        }

        void ShowBriefingDoc()
        {
            var m = Game.Mission;
            if (m == null || m.Info == null) return;
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(m.Info.Place);
            sb.AppendLine();
            var brief = m.Data != null ? m.Data.Get("briefing", m.Info.Pitch) : m.Info.Pitch;
            sb.AppendLine(brief.Replace("\\n", "\n"));
            sb.AppendLine();
            foreach (var o in m.Objectives)
            {
                if (!o.Visible) continue;
                sb.AppendLine($"{(o.Complete ? "√" : o.Failed ? "×" : "•")} {o.Spec.Text}{o.ProgressText}{(o.Primary ? "" : "  (optional)")}");
            }
            var cms = Game.ActiveCountermeasures;
            if (cms.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine(Game.MissionCounters.Count > 0 ? "THE DOSSIER — Vane knows every habit she has:" : "THE DOSSIER — the Vigil has adapted:");
                foreach (var cm in cms)
                {
                    var why = Game.MissionCounters.Count > 0 ? null : Game.Campaign?.AnswerSource(cm);
                    sb.AppendLine($"  {Progression.Habits.CountermeasureName(cm)}{(why != null ? $" ({why})" : "")}: {Progression.Habits.CountermeasureText(cm)}");
                }
            }
            ShowDocument(m.Info.Title, sb.ToString());
        }

        // ================================================================== settings
        string _settingsTab = "Audio";

        public void ShowSettings(string tab = null)
        {
            if (tab != null) _settingsTab = tab;
            var root = E("scrim-solid");
            PushScreen("settings", root, CloseSettings);
            BuildSettings(root);
        }

        void CloseSettings()
        {
            if (Game.Input != null && Game.Input.Rebinding) return;
            Game.Settings?.Save();
            PopScreen("settings");
        }

        void BuildSettings(VisualElement root)
        {
            root.Clear();
            var s = Game.Settings;
            var p = Col("panel", "modal-wide");
            p.style.alignSelf = Align.Center; p.style.marginTop = 40;
            p.Add(L("SETTINGS", "h1"));
            var tabs = Row();
            foreach (var t in new[] { "Audio", "Graphics", "Gameplay", "Camera", "Controls" })
            {
                var tt = t;
                var b = B(t, () => { _settingsTab = tt; BuildSettings(root); }, "tab");
                if (t == _settingsTab) b.AddToClassList("selected");
                tabs.Add(b);
            }
            p.Add(tabs);
            p.Add(E("divider"));
            var sv = new ScrollView(ScrollViewMode.Vertical);
            sv.style.maxHeight = 600;
            sv.style.minHeight = 400;
            p.Add(sv);

            switch (_settingsTab)
            {
                case "Audio":
                    sv.Add(SliderRow("Master", () => s.MasterVolume, v => s.MasterVolume = v));
                    sv.Add(SliderRow("Music", () => s.MusicVolume, v => s.MusicVolume = v));
                    sv.Add(SliderRow("Effects", () => s.SfxVolume, v => s.SfxVolume = v));
                    sv.Add(SliderRow("Ambience", () => s.AmbienceVolume, v => s.AmbienceVolume = v));
                    sv.Add(SliderRow("Voices & barks", () => s.VoiceVolume, v => s.VoiceVolume = v));
                    sv.Add(BoolRow("Subtitles", () => s.Subtitles, v => s.Subtitles = v));
                    break;
                case "Graphics":
                {
                    var res = Screen.resolutions;
                    sv.Add(ChoiceRow("Resolution", () =>
                    {
                        if (s.ResolutionIndex < 0 || s.ResolutionIndex >= res.Length) return $"{Screen.width}×{Screen.height}";
                        var r = res[s.ResolutionIndex]; return $"{r.width}×{r.height} @{r.refreshRateRatio.value:0}";
                    }, d => { if (res.Length > 0) { s.ResolutionIndex = Mathf.Clamp((s.ResolutionIndex < 0 ? res.Length - 1 : s.ResolutionIndex) + d, 0, res.Length - 1); s.ApplyGraphics(); } }));
                    sv.Add(BoolRow("Fullscreen", () => s.Fullscreen, v => { s.Fullscreen = v; s.ApplyGraphics(); }));
                    sv.Add(BoolRow("V-Sync", () => s.VSync, v => { s.VSync = v; s.ApplyGraphics(); }));
                    sv.Add(ChoiceRow("Frame cap (no v-sync)", () => s.TargetFps <= 0 ? "Unlimited" : s.TargetFps.ToString(), d =>
                    {
                        int[] caps = { 30, 60, 90, 120, 144, 165, 240, -1 };
                        int i = Array.IndexOf(caps, s.TargetFps); if (i < 0) i = 3;
                        s.TargetFps = caps[Mathf.Clamp(i + d, 0, caps.Length - 1)]; s.ApplyGraphics();
                    }));
                    sv.Add(ChoiceRow("Quality preset", () => s.QualityLevel < 0 ? QualitySettings.names[QualitySettings.GetQualityLevel()] : QualitySettings.names[Mathf.Clamp(s.QualityLevel, 0, QualitySettings.names.Length - 1)], d =>
                    {
                        int cur = s.QualityLevel < 0 ? QualitySettings.GetQualityLevel() : s.QualityLevel;
                        s.QualityLevel = Mathf.Clamp(cur + d, 0, QualitySettings.names.Length - 1);
                        QualitySettings.SetQualityLevel(s.QualityLevel, true);
                        Game.Root?.ApplyRenderSettings();
                    }));
                    sv.Add(ChoiceRow("Shadows", () => s.ShadowQuality == 0 ? "Off" : s.ShadowQuality == 1 ? "Hard" : "Soft", d => { s.ShadowQuality = Mathf.Clamp(s.ShadowQuality + d, 0, 2); Game.Root?.ApplyRenderSettings(); }));
                    sv.Add(BoolRow("Post-processing", () => s.PostProcessing, v => { s.PostProcessing = v; Game.Root?.ApplyRenderSettings(); }));
                    sv.Add(SliderRow("Brightness", () => (s.Brightness + 1f) * 0.5f, v => { s.Brightness = v * 2f - 1f; Game.Root?.ApplyRenderSettings(); }, v => $"{(v * 2f - 1f):+0.0;-0.0;0.0}"));
                    sv.Add(SliderRow("Interface scale", () => Mathf.InverseLerp(0.6f, 1.6f, s.UiScale), v => { s.UiScale = Mathf.Round(Mathf.Lerp(0.6f, 1.6f, v) * 20f) / 20f; ApplyScale(); }, v => $"{Mathf.Lerp(0.6f, 1.6f, v):0.00}×"));
                    break;
                }
                case "Gameplay":
                    sv.Add(BoolRow("Tutorial hints", () => s.TutorialHints, v => s.TutorialHints = v));
                    sv.Add(BoolRow("Hold key shows all vision cones", () => s.ShowAllConesHotkey, v => s.ShowAllConesHotkey = v));
                    sv.Add(BoolRow("Show nearby vision cones automatically", () => s.ContextualCones, v => s.ContextualCones = v));
                    sv.Add(BoolRow("Show where light exposes you (light rims)", () => s.ExposureRims, v => s.ExposureRims = v));
                    sv.Add(BoolRow("Ground disc: exposed, watchers, next step", () => s.IlseDisc, v => s.IlseDisc = v));
                    sv.Add(BoolRow("High-contrast vision cones", () => s.HighContrastCones, v => s.HighContrastCones = v));
                    sv.Add(BoolRow("Always show all vision cones", () => s.AlwaysAllCones, v => s.AlwaysAllCones = v));
                    sv.Add(BoolRow("Cone key toggles instead of hold", () => s.ConesKeyToggles, v => s.ConesKeyToggles = v));
                    sv.Add(BoolRow("Shape-coded cone states (colour-blind)", () => s.ShapeCodedCones, v => s.ShapeCodedCones = v));
                    sv.Add(BoolRow("Readability test mode (probes and a log)", () => s.ReadabilityTest, v => s.ReadabilityTest = v));
                    sv.Add(BoolRow("Movement test mode (a log and two questions)", () => s.MovementTest, v => s.MovementTest = v));
                    sv.Add(BoolRow("Screen shake", () => s.ScreenShake, v => s.ScreenShake = v));
                    if (Game.Campaign != null)
                    {
                        var d = Difficulties.Current;
                        sv.Add(Space(12));
                        sv.Add(L("DIFFICULTY", "h3"));
                        sv.Add(L($"{d.Name}{(Game.Campaign.Ironblood ? " · Ironblood" : "")} — {d.Description}", "small", "dim"));
                        if (!Game.InMission)
                        {
                            var r = Row();
                            foreach (Difficulty dd in Enum.GetValues(typeof(Difficulty)))
                            {
                                var x = dd;
                                var b = B(Difficulties.Get(dd).Name, () => { Game.Campaign.Difficulty = x; SaveSystem.SaveCampaign(Game.Campaign); BuildSettings(root); }, "btn-small");
                                if (Game.Campaign.Difficulty == dd) b.AddToClassList("selected");
                                r.Add(b);
                            }
                            sv.Add(r);
                        }
                        else sv.Add(L("Difficulty can be changed in the Refuge.", "tiny", "dim"));
                    }
                    break;
                case "Camera":
                    sv.Add(BoolRow("Edge panning", () => s.EdgePan, v => s.EdgePan = v));
                    sv.Add(SliderRow("Pan speed", () => Mathf.InverseLerp(0.3f, 2.5f, s.CameraPanSpeed), v => s.CameraPanSpeed = Mathf.Lerp(0.3f, 2.5f, v), v => $"{Mathf.Lerp(0.3f, 2.5f, v):0.0}×"));
                    sv.Add(SliderRow("Rotate speed", () => Mathf.InverseLerp(0.3f, 2.5f, s.CameraRotateSpeed), v => s.CameraRotateSpeed = Mathf.Lerp(0.3f, 2.5f, v), v => $"{Mathf.Lerp(0.3f, 2.5f, v):0.0}×"));
                    sv.Add(BoolRow("Invert rotation", () => s.InvertRotate, v => s.InvertRotate = v));
                    break;
                case "Controls":
                {
                    var inp = Game.Input;
                    if (inp == null) break;
                    foreach (var (label, action, binding) in inp.Rebindable)
                    {
                        var row = Row("setting-row");
                        row.Add(L(label, "setting-label"));
                        var a = action; int bi = binding;
                        Button btn = null;
                        btn = B(GameInput.Display(a, bi), () =>
                        {
                            if (inp.Rebinding) return;
                            btn.text = "press a key…  (Esc cancels)";
                            btn.AddToClassList("selected");
                            inp.StartRebind(a, bi, () => BuildSettings(root));
                        }, "btn-small");
                        btn.style.minWidth = 220;
                        row.Add(btn);
                        sv.Add(row);
                    }
                    sv.Add(Space(10));
                    sv.Add(B("Reset to defaults", () => Confirm("Reset all bindings?", null, "Reset", () => { inp.ResetBindings(); BuildSettings(root); }), "btn-small"));
                    break;
                }
            }

            p.Add(Space(10));
            var back = B("Back", CloseSettings, "btn-primary");
            back.style.alignSelf = Align.FlexEnd;
            p.Add(back);
            root.Add(p);
        }

        // ---------------------------------------------------------------- setting widgets (built from buttons, so they share the theme)
        VisualElement SliderRow(string label, Func<float> get, Action<float> set, Func<float, string> fmt = null)
        {
            var row = Row("setting-row");
            row.Add(L(label, "setting-label"));
            var ctrl = Row("setting-ctrl");
            var track = E("bar");
            track.style.width = 260; track.style.height = 14;
            var fill = E("bar-fill", "bar-blood");
            track.Add(fill);
            var val = L("", "setting-value");
            Action refresh = () => { float v = Mathf.Clamp01(get()); fill.style.width = Length.Percent(v * 100f); val.text = fmt != null ? fmt(v) : $"{Mathf.RoundToInt(v * 100f)}%"; };
            Action<float> apply = v => { set(Mathf.Clamp01(v)); refresh(); };
            ctrl.Add(B("−", () => apply(Mathf.Round((get() - 0.05f) * 20f) / 20f), "btn-small"));
            ctrl.Add(track);
            ctrl.Add(B("+", () => apply(Mathf.Round((get() + 0.05f) * 20f) / 20f), "btn-small"));
            ctrl.Add(val);
            // click / drag on the track
            bool drag = false;
            track.RegisterCallback<PointerDownEvent>(e => { drag = true; track.CapturePointer(e.pointerId); apply(e.localPosition.x / track.resolvedStyle.width); });
            track.RegisterCallback<PointerMoveEvent>(e => { if (drag) apply(e.localPosition.x / track.resolvedStyle.width); });
            track.RegisterCallback<PointerUpEvent>(e => { drag = false; track.ReleasePointer(e.pointerId); Game.Audio?.Play2D("ui_click", 0.4f); });
            row.Add(ctrl);
            refresh();
            return row;
        }

        VisualElement BoolRow(string label, Func<bool> get, Action<bool> set)
        {
            var row = Row("setting-row");
            row.Add(L(label, "setting-label"));
            row.Add(Toggle(get, set));
            return row;
        }

        static Button Toggle(Func<bool> get, Action<bool> set)
        {
            Button b = null;
            b = B(get() ? "On" : "Off", () => { set(!get()); b.text = get() ? "On" : "Off"; b.EnableInClassList("selected", get()); }, "btn-small");
            b.style.minWidth = 90;
            b.EnableInClassList("selected", get());
            return b;
        }

        VisualElement ChoiceRow(string label, Func<string> get, Action<int> step)
        {
            var row = Row("setting-row");
            row.Add(L(label, "setting-label"));
            var ctrl = Row("setting-ctrl");
            var val = L(get(), "setting-value");
            val.style.minWidth = 200;
            val.style.unityTextAlign = TextAnchor.MiddleCenter;
            ctrl.Add(B("◂", () => { step(-1); val.text = get(); }, "btn-small"));
            ctrl.Add(val);
            ctrl.Add(B("▸", () => { step(1); val.text = get(); }, "btn-small"));
            row.Add(ctrl);
            return row;
        }

        // ================================================================== credits
        void ShowCredits()
        {
            ShowDocument("Credits",
                "VESPERTINE\n\n" +
                "Direction, design, code, writing, sound and art: generated in-engine.\n" +
                "All sound is synthesised at startup; all geometry is built from the mission maps.\n\n" +
                "Made with Unity 6 · Universal Render Pipeline · Input System · UI Toolkit · AI Navigation.\n\n" +
                "Inspired by the real-time tactics tradition: Commandos, Desperados, Shadow Tactics.\n\n" +
                "Thank you for playing.");
        }
    }
}
