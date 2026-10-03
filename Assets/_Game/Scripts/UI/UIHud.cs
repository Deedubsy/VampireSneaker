using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;
using Vespertine.AI;
using Vespertine.Controls;
using Vespertine.Core;
using Vespertine.Data;
using Vespertine.Level;
using Vespertine.Mission;
using Vespertine.Player;
using Vespertine.Stealth;
using Vespertine.Visual;

namespace Vespertine.UI
{
    public partial class UIManager
    {
        // vitals
        VisualElement _hud, _vitals, _hpFill, _bloodFill, _eye, _eyePupil, _vignette;
        Label _hpText, _bloodText, _status, _eyeLabel, _portraitGlyph, _saveAge;
        // abilities
        VisualElement _abilityBar;
        readonly List<SlotUI> _slots = new List<SlotUI>();
        SlotUI _dash;
        Label _slotTip;
        int _slotHover = -1;
        // verbs, objectives, alarm, thralls
        VisualElement _verbs, _objBox, _alarmBox, _thrallBox;
        Label _objHeader;
        VisualElement _objList;
        bool _objDirty = true;
        float _objRefresh;
        int _objSig;
        Label _alarmChip, _dawnChip, _timerChip, _hushChip;
        string _thrallSig;
        // pause
        Label _pauseLabel;
        // world markers
        readonly List<DetMarker> _detPool = new List<DetMarker>();
        // off-screen guards: a pip on the screen edge (SR.9, SR.11)
        readonly List<DetMarker> _edgePool = new List<DetMarker>();
        VisualElement _hoverPrompt, _progress, _progressFill, _objPin;
        Label _hoverName, _hoverVerbs, _aimTip;
        readonly List<VisualElement> _objPins = new List<VisualElement>();

        class SlotUI
        {
            public VisualElement Root, Cd, Active;
            public Label Key, Cost, Name, CdText;
            public string Bound;
        }

        class DetMarker
        {
            public VisualElement Root, Fill, Pat;
            public Label Glyph;
            public MeterRead.Feed LastFeed = (MeterRead.Feed)(-1);
            public EdgeArrow Arrow;   // edge pips only
            // what was last written, so an unchanged meter costs no style writes (79 of them in a hunted M11)
            public float LastFill = -1f, LastScale;
            public Color LastFillCol, LastRing, LastGlyphCol;
            public bool Shown;
        }

        // ================================================================== build
        void BuildHud()
        {
            _hud = E("layer");
            _hud.pickingMode = PickingMode.Ignore;
            _hudLayer.Add(_hud);

            _vignette = E("vignette");
            _vignette.pickingMode = PickingMode.Ignore;
            _vignette.style.borderLeftWidth = _vignette.style.borderRightWidth = 90;
            _vignette.style.borderTopWidth = _vignette.style.borderBottomWidth = 70;
            SetBorderColor(_vignette, new Color(0.5f, 0f, 0.04f, 0f));
            _hud.Add(_vignette);

            // ---- vitals (bottom left)
            _vitals = E("hud-vitals");
            var portrait = E("portrait");
            _portraitGlyph = L("I", "portrait-glyph");
            portrait.Add(_portraitGlyph);
            _vitals.Add(portrait);
            var bars = Col();
            bars.style.flexGrow = 1;
            _status = L("", "status-line");
            bars.Add(_status);
            var hpRow = Row(); hpRow.style.justifyContent = Justify.SpaceBetween;
            hpRow.Add(L("VITALITY", "vital-label")); _hpText = L("", "vital-label"); hpRow.Add(_hpText);
            bars.Add(hpRow);
            bars.Add(Bar("bar-hp", out _hpFill));
            var blRow = Row(); blRow.style.justifyContent = Justify.SpaceBetween;
            blRow.Add(L("BLOOD", "vital-label")); _bloodText = L("", "vital-label"); blRow.Add(_bloodText);
            bars.Add(blRow);
            var bloodBar = Bar("bar-blood", out _bloodFill);
            bloodBar.style.height = 20;
            bars.Add(bloodBar);
            _vitals.Add(bars);
            _hud.Add(_vitals);
            foreach (var v in _vitals.Query<VisualElement>().ToList()) v.pickingMode = PickingMode.Ignore;

            // ---- light eye
            _eye = E("eye");
            _eyePupil = E("eye-pupil");
            _eye.Add(_eyePupil);
            _eye.pickingMode = PickingMode.Ignore; _eyePupil.pickingMode = PickingMode.Ignore;
            _hud.Add(_eye);
            _eyeLabel = L("", "eye-label");
            _eyeLabel.pickingMode = PickingMode.Ignore;
            _hud.Add(_eyeLabel);
            // ---- save age (QW8): how long ago she saved, or that she can't while watched
            _saveAge = L("", "save-age");
            _saveAge.pickingMode = PickingMode.Ignore;
            _hud.Add(_saveAge);

            // ---- ability bar
            _abilityBar = E("ability-bar");
            _abilityBar.pickingMode = PickingMode.Ignore;
            _abilityBar.Add(BuildDashSlot());
            for (int i = 0; i < 6; i++) _abilityBar.Add(BuildSlot(i));
            _hud.Add(_abilityBar);
            _slotTip = L("", "aim-tip");
            _slotTip.style.whiteSpace = WhiteSpace.Normal;
            _slotTip.style.maxWidth = 420;
            _slotTip.pickingMode = PickingMode.Ignore;
            _slotTip.style.bottom = 118; _slotTip.style.left = Length.Percent(50); _slotTip.style.translate = new Translate(Length.Percent(-50), 0);
            Show(_slotTip, false);
            _hud.Add(_slotTip);

            // ---- context verbs
            _verbs = E("verbs");
            _verbs.pickingMode = PickingMode.Ignore;
            _hud.Add(_verbs);

            // ---- objectives (top right)
            _objBox = E("objectives");
            _objBox.pickingMode = PickingMode.Ignore;
            _objHeader = L("OBJECTIVES", "obj-header");
            _objBox.Add(_objHeader);
            _objList = Col();
            _objList.pickingMode = PickingMode.Ignore;
            _objBox.Add(_objList);
            _hud.Add(_objBox);

            // ---- alarm + dawn (top centre)
            _alarmBox = E("alarm");
            _alarmBox.pickingMode = PickingMode.Ignore;
            _alarmChip = L("", "alarm-chip");
            _dawnChip = L("", "alarm-chip", "dawn");
            _timerChip = L("", "alarm-chip", "alarm-1");
            _hushChip = L("", "alarm-chip", "hush-chip");
            _alarmBox.Add(_alarmChip); _alarmBox.Add(_timerChip); _alarmBox.Add(_hushChip); _alarmBox.Add(_dawnChip);
            foreach (var c in new[] { _alarmChip, _dawnChip, _timerChip, _hushChip }) c.pickingMode = PickingMode.Ignore;
            _hud.Add(_alarmBox);

            // ---- thralls (top left)
            _thrallBox = E("thralls");
            _hud.Add(_thrallBox);

            // ---- pause label
            _pauseLabel = L("PAUSED", "pause-label");
            _pauseLabel.pickingMode = PickingMode.Ignore;
            _hud.Add(_pauseLabel);

            // ---- world-space prompts (marker layer)
            _hoverPrompt = E("marker");
            _hoverPrompt.pickingMode = PickingMode.Ignore;
            var pr = E("prompt");
            pr.pickingMode = PickingMode.Ignore;
            _hoverName = L("", "body");
            _hoverName.style.fontSize = 16;
            _hoverName.style.unityFontStyleAndWeight = FontStyle.Bold;
            _hoverName.enableRichText = true;
            _hoverVerbs = L("", "small");
            _hoverVerbs.enableRichText = true;
            _hoverName.pickingMode = PickingMode.Ignore; _hoverVerbs.pickingMode = PickingMode.Ignore;
            pr.Add(_hoverName); pr.Add(_hoverVerbs);
            _hoverPrompt.Add(pr);
            _markerLayer.Add(_hoverPrompt);

            _progress = E("marker");
            _progress.pickingMode = PickingMode.Ignore;
            var pbar = E("progress");
            _progressFill = E("progress-fill");
            pbar.Add(_progressFill);
            pbar.pickingMode = PickingMode.Ignore; _progressFill.pickingMode = PickingMode.Ignore;
            _progress.Add(pbar);
            _markerLayer.Add(_progress);

            _aimTip = L("", "aim-tip");
            _aimTip.enableRichText = true;
            _aimTip.pickingMode = PickingMode.Ignore;
            _markerLayer.Add(_aimTip);
        }

        VisualElement BuildSlot(int i)
        {
            var s = new SlotUI();
            s.Root = E("slot");
            s.Name = L("", "slot-name");
            s.Key = L((i + 1).ToString(), "slot-key");
            s.Cost = L("", "slot-cost");
            s.Cd = E("slot-cd");
            s.CdText = L("", "slot-cdtext");
            s.Active = E("slot-active");
            s.Root.Add(s.Name); s.Root.Add(s.Cd); s.Root.Add(s.CdText); s.Root.Add(s.Key); s.Root.Add(s.Cost); s.Root.Add(s.Active);
            foreach (var c in new VisualElement[] { s.Name, s.Key, s.Cost, s.Cd, s.CdText, s.Active }) c.pickingMode = PickingMode.Ignore;
            int idx = i;
            s.Root.RegisterCallback<MouseEnterEvent>(_ => _slotHover = idx);
            s.Root.RegisterCallback<MouseLeaveEvent>(_ => { if (_slotHover == idx) _slotHover = -1; });
            s.Root.RegisterCallback<ClickEvent>(_ => OnSlotClicked(idx));
            _slots.Add(s);
            return s.Root;
        }

        void OnSlotClicked(int i)
        {
            var p = Game.Player;
            if (p == null || p.Dead || BlocksGameplay) return;
            if (p.SelectedThrall != null) { Toast($"Press {i + 1} to give this command, or right-click to return to Ilse."); return; }
            var a = p.SlotAbility(i);
            if (a == null) return;
            if (!p.CanCast(a, out var why)) { Toast($"{a.Name}: {why}"); Game.Audio?.Play2D("ui_error", 0.5f); return; }
            if (p.Aiming == a) { p.CancelAim(); return; }
            p.BeginAbility(a);
        }

        static void SetBorderColor(VisualElement v, Color c)
        {
            v.style.borderLeftColor = v.style.borderRightColor = v.style.borderTopColor = v.style.borderBottomColor = c;
        }

        public void SetHudVisible(bool on)
        {
            Show(_hud, on);
            Show(_markerLayer, on);
            if (!on)
            {
                foreach (var d in _detPool) { Show(d.Root, false); d.Shown = false; }
                foreach (var d in _edgePool) { Show(d.Root, false); d.Shown = false; }
                Show(_hoverPrompt, false);
                Show(_progress, false);
                Show(_aimTip, false);
            }
        }

        // ================================================================== per-frame
        void TickHud()
        {
            bool inMission = Game.InMission && Game.Player != null;
            bool show = inMission && !(Game.Mission.Ended && _screens.Count > 0);
            if ((_hud.style.display == DisplayStyle.Flex) != show) SetHudVisible(show);
            if (!show) return;

            var p = Game.Player;
            UnityEngine.Profiling.Profiler.BeginSample("Hud.Vitals"); TickVitals(p); UnityEngine.Profiling.Profiler.EndSample();
            UnityEngine.Profiling.Profiler.BeginSample("Hud.Slots"); TickSlots(p); UnityEngine.Profiling.Profiler.EndSample();
            UnityEngine.Profiling.Profiler.BeginSample("Hud.Verbs"); TickVerbs(p); UnityEngine.Profiling.Profiler.EndSample();
            UnityEngine.Profiling.Profiler.BeginSample("Hud.Objectives"); TickObjectives(); UnityEngine.Profiling.Profiler.EndSample();
            UnityEngine.Profiling.Profiler.BeginSample("Hud.Alarm"); TickAlarm(); UnityEngine.Profiling.Profiler.EndSample();
            UnityEngine.Profiling.Profiler.BeginSample("Hud.Thralls"); TickThralls(p); UnityEngine.Profiling.Profiler.EndSample();
            TickPause();
            UnityEngine.Profiling.Profiler.BeginSample("Hud.Markers"); TickMarkers(p); UnityEngine.Profiling.Profiler.EndSample();
        }

        void TickVitals(Vampire p)
        {
            SetFill(_hpFill, p.HP / Mathf.Max(1f, p.MaxHP));
            SetFill(_bloodFill, p.Blood / Mathf.Max(1f, p.MaxBlood));
            int hp = Mathf.CeilToInt(p.HP), maxHp = Mathf.RoundToInt(p.MaxHP), bonus = p.BonusHP > 0.5f ? Mathf.RoundToInt(p.BonusHP) : 0;
            if (hp != _vHp || maxHp != _vMaxHp || bonus != _vBonus)
            {
                _vHp = hp; _vMaxHp = maxHp; _vBonus = bonus;
                _hpText.text = $"{hp}/{maxHp}" + (bonus > 0 ? $" +{bonus}" : "");
            }
            int blood = Mathf.FloorToInt(p.Blood), maxBlood = Mathf.RoundToInt(p.MaxBlood);
            if (blood != _vBlood || maxBlood != _vMaxBlood)
            {
                _vBlood = blood; _vMaxBlood = maxBlood;
                _bloodText.text = $"{blood}/{maxBlood}";
            }
            if (p.Starving || _vStarving) _bloodFill.style.backgroundColor = p.Starving ? Color.Lerp(Mats.Pal.Blood, Color.white, 0.25f + 0.25f * Mathf.Sin(Time.unscaledTime * 6f)) : (Color)Mats.Pal.Blood;
            _vStarving = p.Starving;

            var sb = _statusSb.Clear();
            if (p.Dead) sb.Append("Fallen  ");
            if (p.InMist) sb.Append("Mist  ");
            if (p.Concealed) sb.Append("Hidden  ");
            if (p.Masked && !p.Concealed) { var f = p.MaskFault; sb.Append(f == null ? "Masked  " : $"Masked, but {f}  "); }
            if (p.Carrying != null) sb.Append("Carrying a body  ");
            if (p.Feeding != null) sb.Append(p.FeedDrain ? "Draining  " : "Sipping  ");
            if (Game.Level != null)
            {
                if (_listenOf != Game.Level) { _listenOf = Game.Level; _listens.Clear(); foreach (var lp in Game.Level.All<ListenPoint>()) _listens.Add(lp); }
                foreach (var lp in _listens)
                    if (lp && lp.Listening) { sb.Append("Overhearing ").Append(Mathf.RoundToInt(lp.Progress * 100f)).Append("%  "); break; }
            }
            if (p.ApexActive) sb.Append("Apex  ");
            if (p.Sensing) sb.Append("Blood Sense  ");
            if (!string.IsNullOrEmpty(p.Humour) && p.HumourUntil > Time.time) sb.Append(Vampire.HumourName(p.Humour)).Append(' ').Append(Mathf.CeilToInt(p.HumourUntil - Time.time)).Append("s  ");
            if (p.Starving) sb.Append("Starving  ");
            while (sb.Length > 0 && sb[sb.Length - 1] == ' ') sb.Length--;
            if (!Same(sb, _status.text)) _status.text = sb.ToString();
            _portraitGlyph.text = p.InMist ? "~" : "I";
            TickSaveAge();

            // light eye: how visible she is right now
            float l = p.Light;
            string label; Color c;
            bool burning = Game.Lights != null && Game.Lights.BurnAt(p.Feet) > 0f;
            if (burning) { label = "BURNING"; c = Mats.Pal.Sunstone; }
            else if (p.Concealed) { label = "CONCEALED"; c = new Color(0.3f, 0.3f, 0.35f); }
            // one threshold (SR.7): the disc's hidden/exposed line, which the far band, regen and Dash all use
            else if (p.MaskHolds && l >= DetectionMath.ExposedAt) { label = "MASKED"; c = new Color(0.78f, 0.62f, 0.9f); }
            else if (l < DetectionMath.ExposedAt) { label = "DARK"; c = new Color(0.25f, 0.25f, 0.32f); }
            else { label = "LIT"; c = new Color(1f, 0.88f, 0.6f); }
            if (!ReferenceEquals(label, _vEye))
            {
                _vEye = label;
                _eyeLabel.text = label;
                _eyeLabel.style.color = c;
                _eyePupil.style.backgroundColor = c;
            }
            float sz = p.Concealed ? 6f : Mathf.Lerp(8f, 28f, Mathf.Clamp01(l / 0.9f));
            if (Mathf.Abs(sz - _vPupil) > 0.25f)
            {
                _vPupil = sz;
                _eyePupil.style.width = sz; _eyePupil.style.height = sz;
                _eyePupil.style.borderTopLeftRadius = _eyePupil.style.borderTopRightRadius = _eyePupil.style.borderBottomLeftRadius = _eyePupil.style.borderBottomRightRadius = sz * 0.5f;
            }

            // red vignette when hurt, gold when burning
            float hurt = 1f - p.HP / Mathf.Max(1f, p.MaxHP);
            float a = Mathf.Clamp01((hurt - 0.4f) * 1.4f) * (0.35f + 0.1f * Mathf.Sin(Time.unscaledTime * 5f));
            var vc = burning ? new Color(1f, 0.7f, 0.3f, 0.25f + 0.1f * Mathf.Sin(Time.unscaledTime * 9f)) : new Color(0.5f, 0f, 0.04f, a);
            if (vc.a > 0.004f || _vVignetteA > 0.004f) SetBorderColor(_vignette, vc);
            _vVignetteA = vc.a;
        }

        int _vHp = -1, _vMaxHp = -1, _vBonus = -1, _vBlood = -1, _vMaxBlood = -1;
        bool _vStarving = true;
        string _vEye;

        void TickSaveAge()
        {
            var m = Game.Mission;
            bool iron = Game.Campaign != null && Game.Campaign.Ironblood;
            string text = m == null || iron ? ""
                : Game.AI != null && Vespertine.Mission.MissionController.WatchedBy(Game.AI.Npcs) != null ? "Watched: can't save"
                : Vespertine.Save.SaveSystem.Age(m.SavedAt < 0f ? -1f : m.MissionTime - m.SavedAt);
            if (text == _saveAge.text) return;
            _saveAge.text = text;
            _saveAge.EnableInClassList("save-watched", text.StartsWith("Watched"));
        }
        float _vPupil = -1f, _vVignetteA = 1f;
        readonly StringBuilder _statusSb = new StringBuilder(96);
        readonly List<ListenPoint> _listens = new List<ListenPoint>();
        LevelRuntime _listenOf;

        /// <summary>Shadow Dash (D155) sits before the ability slots: its charges as diamonds, the refill as the slot's shade.</summary>
        VisualElement BuildDashSlot()
        {
            var s = _dash = new SlotUI();
            s.Root = E("slot");
            s.Name = L("Dash", "slot-name");
            s.Key = L("Q", "slot-key");
            s.Cost = L("", "slot-cost");
            _dashPips = E("slot-cost");
            _dashPips.style.flexDirection = FlexDirection.Row;
            s.Cd = E("slot-cd");
            s.CdText = L("", "slot-cdtext");
            s.Active = E("slot-active");
            s.Root.Add(s.Name); s.Root.Add(s.Cd); s.Root.Add(s.CdText); s.Root.Add(s.Key); s.Root.Add(_dashPips); s.Root.Add(s.Active);
            foreach (var c in new VisualElement[] { s.Root, s.Name, s.Key, _dashPips, s.Cd, s.CdText, s.Active }) c.pickingMode = PickingMode.Ignore;
            Show(s.Active, false);
            return s.Root;
        }

        void TickDashSlot(Vampire p)
        {
            var s = _dash;
            Show(s.Root, p.SelectedThrall == null);
            if (p.SelectedThrall != null) return;
            s.Key.text = Game.Input != null ? GameInput.Key(Game.Input.Dash) : "Q";
            int have = p.DashCharges, max = p.DashMax;
            if (max != _dashMaxShown)
            {
                _dashMaxShown = max; _dashShown = -1;
                _dashPips.Clear();
                for (int i = 0; i < max; i++)
                {
                    // a small diamond: a square turned 45°
                    var d = new VisualElement { pickingMode = PickingMode.Ignore };
                    d.style.width = d.style.height = 7;
                    d.style.marginLeft = 4;
                    d.style.rotate = new Rotate(45f);
                    d.style.borderTopWidth = d.style.borderBottomWidth = d.style.borderLeftWidth = d.style.borderRightWidth = 1;
                    d.style.borderTopColor = d.style.borderBottomColor = d.style.borderLeftColor = d.style.borderRightColor = DashPip;
                    _dashPips.Add(d);
                }
            }
            if (have != _dashShown)
            {
                _dashShown = have;
                for (int i = 0; i < _dashPips.childCount; i++)
                    _dashPips[i].style.backgroundColor = i < have ? DashPip : Color.clear;
            }
            s.Root.EnableInClassList("unaffordable", have == 0);
            // the next charge fills from the bottom, and only in the dark: in light it waits, and says why
            bool filling = have < max;
            s.Cd.style.height = filling && have == 0 ? Length.Percent((1f - p.DashRefill) * 100f) : 0f;
            s.CdText.text = filling && p.SightLight >= DetectionMath.ExposedAt ? "light" : "";
        }
        int _dashShown = -1, _dashMaxShown = -1;
        VisualElement _dashPips;
        static readonly Color DashPip = new Color(0.86f, 0.2f, 0.24f);

        static bool Same(StringBuilder sb, string s)
        {
            if (s == null || sb.Length != s.Length) return false;
            for (int i = 0; i < s.Length; i++) if (sb[i] != s[i]) return false;
            return true;
        }

        void TickSlots(Vampire p)
        {
            int slots = Game.Campaign != null ? Game.Campaign.Slots : 1;
            var thrall = p.SelectedThrall;
            TickDashSlot(p);
            for (int i = 0; i < _slots.Count; i++)
            {
                var s = _slots[i];
                s.Key.text = Game.Input != null ? GameInput.Key(Game.Input.Abilities[i]) : (i + 1).ToString();
                if (thrall != null)
                {
                    bool exists = i < Vampire.ThrallSlots.Length;
                    Show(s.Root, exists);
                    if (!exists) continue;
                    bool ok = p.ThrallSlotAvailable(i, out _);
                    s.Name.text = Vampire.ThrallSlots[i];
                    s.Cost.text = "";
                    s.Root.EnableInClassList("unaffordable", !ok);
                    s.Root.EnableInClassList("aiming", p.Aiming != null && p.AimThrall == thrall && ((i == 1 && p.Aiming.Id == "dominion.false_orders") || (i == 2 && p.Aiming.Id == "dominion.puppet_strike")));
                    s.Root.EnableInClassList("empty", false);
                    s.Cd.style.height = 0; s.CdText.text = "";
                    Show(s.Active, false);
                    s.Root.style.borderTopColor = s.Root.style.borderBottomColor = s.Root.style.borderLeftColor = s.Root.style.borderRightColor = Mats.Pal.Thrall;
                    continue;
                }
                s.Root.style.borderTopColor = s.Root.style.borderBottomColor = s.Root.style.borderLeftColor = s.Root.style.borderRightColor = StyleKeyword.Null;
                Show(s.Root, i < slots);
                if (i >= slots) continue;
                var a = p.SlotAbility(i);
                s.Root.EnableInClassList("empty", a == null);
                if (a == null) { s.Name.text = "—"; s.Cost.text = ""; s.Cd.style.height = 0; s.CdText.text = ""; Show(s.Active, false); s.Root.EnableInClassList("aiming", false); continue; }
                s.Name.text = a.Name;
                float cost = p.CostOf(a);
                s.Cost.text = cost > 0 ? Mathf.RoundToInt(cost).ToString() : (a.Upkeep > 0 ? "~" : "");
                bool can = p.CanCast(a, out _);
                float cd = p.CooldownLeft(a.Id);
                s.Root.EnableInClassList("unaffordable", !can && cd <= 0f && !(a.Id == "shade.mist" && p.InMist) && !(a.Id == "sanguis.sense" && p.Sensing));
                s.Root.EnableInClassList("aiming", p.Aiming == a && p.AimThrall == null);
                if (cd > 0f && a.Cooldown > 0f)
                {
                    s.Cd.style.height = Length.Percent(Mathf.Clamp01(cd / a.Cooldown) * 100f);
                    s.CdText.text = cd >= 10f ? Mathf.CeilToInt(cd).ToString() : cd.ToString("0.0");
                }
                else { s.Cd.style.height = 0; s.CdText.text = ""; }
                bool active = (a.Id == "shade.mist" && p.InMist) || (a.Id == "sanguis.sense" && p.Sensing) || (a.Id == "predator.apex" && p.ApexActive);
                Show(s.Active, active);
            }

            // tooltip
            if (_slotHover >= 0 && _slotHover < _slots.Count)
            {
                string tip = null;
                if (thrall != null)
                {
                    if (_slotHover < Vampire.ThrallSlots.Length)
                    {
                        p.ThrallSlotAvailable(_slotHover, out var why);
                        tip = $"<b>{Vampire.ThrallSlots[_slotHover]}</b>\n" + ThrallSlotHint(_slotHover) + (why != null ? $"\n<color=#e06a60>{why}</color>" : "");
                    }
                }
                else
                {
                    var a = p.SlotAbility(_slotHover);
                    if (a != null)
                    {
                        p.CanCast(a, out var why);
                        var cost = p.CostOf(a);
                        var meta = new List<string>();
                        if (cost > 0) meta.Add($"{cost:0} blood");
                        if (a.Upkeep > 0) meta.Add($"{a.Upkeep:0.#}/s");
                        if (a.Range > 0) meta.Add($"{a.Range:0} m");
                        if (a.Cooldown > 0) meta.Add($"{a.Cooldown:0}s recovery");
                        if (a.Holy) meta.Add("blocked on holy ground");
                        tip = $"<b>{a.Name}</b>   <color=#9a9088>{string.Join(" · ", meta)}</color>\n{a.Hint}" + (why != null ? $"\n<color=#e06a60>{why}</color>" : "");
                    }
                }
                _slotTip.enableRichText = true;
                _slotTip.text = tip ?? "";
                Show(_slotTip, tip != null);
            }
            else Show(_slotTip, false);
        }

        static string ThrallSlotHint(int i)
        {
            switch (i)
            {
                case 0: return "The thrall strikes up talk with the human under the cursor, or the nearest one: they stand and listen.";
                case 1: return "Click a human, then a place: the thrall relays orders sending them there.";
                case 2: return "Click a human: the thrall strikes them down. Witnesses will see a murderer, not her.";
                case 3: return "Release the thrall. They wake confused, remembering nothing.";
            }
            return "";
        }

        readonly List<Verb> _verbList = new List<Verb>();
        int _verbSig;
        bool _verbSigSet;

        float _verbT;

        void TickVerbs(Vampire p)
        {
            // ten times a second is quicker than the eye reads the bar, and it spares the feed and body scans every frame
            if ((_verbT -= Time.unscaledDeltaTime) > 0f) return;
            _verbT = 0.1f;
            _verbList.Clear();
            if (p.Dead) { SetVerbs(); return; }
            var inp = Game.Input;
            if (p.SelectedThrall != null)
            {
                var t = p.SelectedThrall;
                _verbList.Add(new Verb(null, t.DisplayName, true, "Controlling "));
                _verbList.Add(new Verb(Vampire_MoveKeys(inp), "Move", true));
                _verbList.Add(new Verb("1", "Distract", p.ThrallTarget(t) != null));
                _verbList.Add(new Verb(GameInput.Key(inp.Interact), "Use", true));
                _verbList.Add(new Verb(GameInput.Key(inp.ThrallFollow), t.Order == ThrallOrder.Follow ? "Hold" : "Follow Ilse", true));
                _verbList.Add(new Verb(GameInput.Key(inp.CycleThrall), "Next", true));
                _verbList.Add(new Verb("RMB", "Back to Ilse", true));
                SetVerbs();
                return;
            }
            if (p.TraverseLink != null && p.Feeding == null) _verbList.Add(new Verb(GameInput.Key(inp.Traverse), TraverseVerb(p.TraverseLink.Kind), true));
            var feed = p.Carrying == null ? p.FeedTarget() : null;
            bool canFeed = feed != null && feed.CanBeFedBy(p, out _) && p.FeedGap(feed) <= 0f;   // greyed when out of reach: she will not walk there (P7)
            if (p.Feeding == null)
            {
                _verbList.Add(new Verb(GameInput.Key(inp.Sip), "Sip", canFeed));
                _verbList.Add(new Verb(GameInput.Key(inp.Drain), "Drain", canFeed));
            }
            if (p.Carrying != null) _verbList.Add(new Verb(GameInput.Key(inp.Carry), "Drop body", true));
            else
            {
                var b = p.BodyTarget();
                _verbList.Add(new Verb(GameInput.Key(inp.Carry), "Carry", b != null && p.CarryGap(b) <= 0f));
            }
            var near = p.NearUse;
            if (near != null && near.PlayerCan) _verbList.Add(new Verb(GameInput.Key(inp.Interact), near.Verb, p.InUseReach(near)));
            else _verbList.Add(new Verb(GameInput.Key(inp.Interact), "Use", p.HoverUse != null || p.HoverLight != null));
            _verbList.Add(new Verb(GameInput.Key(inp.Sneak), p.Sneaking ? "Sneaking" : "Sneak", true));
            if (p.Has("sanguis.sense")) _verbList.Add(new Verb(GameInput.Key(inp.BloodSense), "Sense", true));
            if (p.Thralls.Count > 0)
            {
                _verbList.Add(new Verb(GameInput.Key(inp.CycleThrall), "Control thrall", true));
                bool allFollow = p.Thralls.TrueForAll(x => !x || x.Order == ThrallOrder.Follow);
                _verbList.Add(new Verb(GameInput.Key(inp.ThrallFollow), allFollow ? "Thralls hold" : "Thralls follow", true));
            }
            SetVerbs();
        }

        static string Vampire_MoveKeys(GameInput i) => UIManager.MoveKeys(i);

        static string TraverseVerb(LinkKind k)
        {
            switch (k)
            {
                case LinkKind.Jump: return "Drop";
                case LinkKind.Leap: return "Leap";
                case LinkKind.Mist: return "Slip through";
                default: return "Climb";
            }
        }

        readonly struct Verb
        {
            public readonly string Key, Text, Prefix; public readonly bool On;
            public Verb(string key, string text, bool on, string prefix = null) { Key = key; Text = text; On = on; Prefix = prefix; }
            public string Label => Prefix != null ? Prefix + Text : Key != null ? "[" + Key + "] " + Text : Text;
            public int Hash => ((Key?.GetHashCode() ?? 0) * 31 + (Text?.GetHashCode() ?? 0)) * 31 + (Prefix?.GetHashCode() ?? 0) + (On ? 1 : 0);
        }

        void SetVerbs()
        {
            // compared by hash: the labels are only formatted when the bar actually changes
            int sig = _verbList.Count;
            foreach (var v in _verbList) sig = sig * 486187739 + v.Hash;
            if (sig == _verbSig && _verbSigSet) return;
            _verbSig = sig; _verbSigSet = true;
            _verbs.Clear();
            foreach (var v in _verbList)
            {
                var l = L(v.Label, "verb");
                l.pickingMode = PickingMode.Ignore;
                if (!v.On) l.AddToClassList("off");
                _verbs.Add(l);
            }
        }

        void TickObjectives()
        {
            var m = Game.Mission;
            bool flashing = false;
            foreach (var o in m.Objectives) if (o.FlashT > 0f) { o.FlashT -= Time.unscaledDeltaTime; flashing = true; }
            _objRefresh -= Time.unscaledDeltaTime;
            if (!_objDirty && !flashing && _objRefresh > 0f) return;
            _objRefresh = 0.25f;
            // what the list shows: each visible objective's state, its part count and whether it is flashing
            int sig = 17;
            foreach (var o in m.Objectives)
                if (o.Visible) sig = (((sig * 31 + o.Id.GetHashCode()) * 31 + (int)o.State) * 31 + o.Done.Count * 64 + o.PartsTotal) * 2 + (o.FlashT > 0 ? 1 : 0);
            if (!_objDirty && sig == _objSig) return;
            _objDirty = false;
            _objSig = sig;
            _objHeader.text = (m.Info != null ? m.Info.Title : "Objectives").ToUpperInvariant();
            _objList.Clear();
            // primaries first, then optionals
            for (int pass = 0; pass < 2; pass++)
            {
                bool anyOpt = false;
                foreach (var o in m.Objectives)
                {
                    if (!o.Visible || o.Primary != (pass == 0)) continue;
                    if (pass == 1 && !anyOpt) { anyOpt = true; var h = L("OPTIONAL", "obj-header"); h.style.marginTop = 8; h.pickingMode = PickingMode.Ignore; _objList.Add(h); }
                    var row = E("obj");
                    row.pickingMode = PickingMode.Ignore;
                    if (!o.Primary) row.AddToClassList("optional");
                    if (o.Complete) row.AddToClassList("complete");
                    if (o.Failed) row.AddToClassList("failed");
                    if (o.FlashT > 0f) row.AddToClassList("flash");
                    var mark = L(o.Complete ? "√" : o.Failed ? "×" : "•", "obj-mark");
                    var text = L(o.Spec.Text + o.ProgressText, "obj-text");
                    mark.pickingMode = text.pickingMode = PickingMode.Ignore;
                    row.Add(mark); row.Add(text);
                    _objList.Add(row);
                }
            }
        }

        void TickAlarm()
        {
            int alarm = Game.AI != null ? Game.AI.Alarm : 0;
            if (Game.AI != null && Game.AI.Lockdown) alarm = 3;
            if (alarm != _lastAlarm)
            {
                _lastAlarm = alarm;
                _alarmChip.RemoveFromClassList("alarm-1"); _alarmChip.RemoveFromClassList("alarm-2"); _alarmChip.RemoveFromClassList("alarm-3");
                switch (alarm)
                {
                    case 1: _alarmChip.text = "SEARCHING"; _alarmChip.AddToClassList("alarm-1"); break;
                    case 2: _alarmChip.text = "ALARM"; _alarmChip.AddToClassList("alarm-2"); break;
                    case 3: _alarmChip.text = "LOCKDOWN"; _alarmChip.AddToClassList("alarm-3"); break;
                }
                Show(_alarmChip, alarm > 0);
                if (alarm < 2) _alarmChip.style.opacity = 1f;
            }
            if (alarm >= 2) _alarmChip.style.opacity = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 8f);

            var m = Game.Mission;
            if (m.DawnLeft >= 0f)
            {
                int s = Mathf.CeilToInt(m.DawnLeft);
                if (s != _lastDawnS) { _lastDawnS = s; _dawnChip.text = $"DAWN  {s / 60}:{s % 60:00}"; }
                Show(_dawnChip, true);
                _dawnChip.style.opacity = m.DawnLeft < 60f ? 0.7f + 0.3f * Mathf.Sin(Time.unscaledTime * 6f) : 1f;
            }
            else Show(_dawnChip, false);
            float t = m.TimerLeft(m.HudTimer ?? "hud");
            if (t > 0f)
            {
                int s = Mathf.CeilToInt(t);
                if (s != _lastTimerS || !ReferenceEquals(m.HudLabel, _lastTimerLabel))
                {
                    _lastTimerS = s; _lastTimerLabel = m.HudLabel;
                    _timerChip.text = (m.HudLabel != null ? m.HudLabel.ToUpperInvariant() + "  " : "") + $"{s / 60}:{s % 60:00}";
                }
                Show(_timerChip, true);
                _timerChip.style.opacity = t < 15f ? 0.7f + 0.3f * Mathf.Sin(Time.unscaledTime * 8f) : 1f;
            }
            else Show(_timerChip, false);
            float h = m.TimerLeft("#hush");
            if (h > 0f)
            {
                int hs = Mathf.CeilToInt(h);
                if (hs != _lastHushS) { _lastHushS = hs; _hushChip.text = $"\u266A  THE ORCHESTRA SWELLS  {hs}"; }
                Show(_hushChip, true);
                _hushChip.style.opacity = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 5f);
            }
            else Show(_hushChip, false);
        }

        int _lastAlarm = -1, _lastDawnS = -1, _lastTimerS = -1, _lastHushS = -1;
        string _lastTimerLabel;
        float _thrallT;
        Npc _lastSel;
        int _lastThrallN = -1;

        void TickThralls(Vampire p)
        {
            // order labels change at the thralls' pace; a click on a portrait answers at once
            _thrallT -= Time.unscaledDeltaTime;
            if (_thrallT > 0f && p.SelectedThrall == _lastSel && p.Thralls.Count == _lastThrallN) return;
            _thrallT = 0.2f; _lastSel = p.SelectedThrall; _lastThrallN = p.Thralls.Count;
            var sb = new StringBuilder();
            foreach (var t in p.Thralls) if (t) sb.Append(t.Id).Append(t == p.SelectedThrall ? "*" : "").Append(t.OrderLabel).Append('|');
            var sig = sb.ToString();
            if (sig == _thrallSig) return;
            _thrallSig = sig;
            _thrallBox.Clear();
            if (p.Thralls.Count == 0) return;
            var head = L("PARTY", "obj-header");
            head.pickingMode = PickingMode.Ignore;
            _thrallBox.Add(head);
            // Ilse herself heads the party: a click on her takes control back
            {
                var row = E("thrall");
                if (p.SelectedThrall == null) row.AddToClassList("selected");
                var icon = E("thrall-icon");
                icon.Add(L("I", "bold"));
                row.Add(icon);
                var col = Col();
                col.Add(L("Ilse", "thrall-name"));
                col.Add(L(p.SelectedThrall == null ? "Under your hand" : "Waiting", "thrall-order", "tiny", "dim"));
                row.Add(col);
                foreach (var c in row.Query<VisualElement>().ToList()) if (c != row) c.pickingMode = PickingMode.Ignore;
                row.RegisterCallback<ClickEvent>(_ => Game.Player?.SelectThrall(null));
                _thrallBox.Add(row);
            }
            foreach (var t in p.Thralls)
            {
                if (!t) continue;
                var row = E("thrall");
                if (t == p.SelectedThrall) row.AddToClassList("selected");
                var icon = E("thrall-icon");
                icon.Add(L(string.IsNullOrEmpty(t.DisplayName) ? "?" : t.DisplayName.Substring(0, 1), "bold"));
                row.Add(icon);
                var col = Col();
                col.Add(L(t.DisplayName, "thrall-name"));
                col.Add(L(t == p.SelectedThrall && t.Order == ThrallOrder.None ? "Under your hand" : t.OrderLabel, "thrall-order", "tiny", "dim"));
                row.Add(col);
                foreach (var c in row.Query<VisualElement>().ToList()) if (c != row) c.pickingMode = PickingMode.Ignore;
                var thrall = t;
                row.RegisterCallback<ClickEvent>(_ =>
                {
                    if (Game.Player == null) return;
                    Game.Player.SelectThrall(thrall);
                });
                _thrallBox.Add(row);
            }
        }

        void TickPause()
        {
            bool show = Game.TacticalPaused && !Game.MenuPaused;
            Show(_pauseLabel, show);
            if (show) _pauseLabel.style.opacity = 0.7f + 0.3f * Mathf.Sin(Time.unscaledTime * 3f);
        }

        // ================================================================== world markers
        DetMarker GetDet(int i) => GetMeter(_detPool, i, false);

        DetMarker GetMeter(List<DetMarker> pool, int i, bool edge)
        {
            while (pool.Count <= i)
            {
                var d = new DetMarker { Root = E("marker") };
                var ring = E("det");
                d.Fill = E("det-fill");
                // the band's pattern, at the ring's size so it doesn't stretch as the fill grows (SR.9)
                d.Pat = E("det-pat");
                d.Fill.Add(d.Pat);
                d.Glyph = L("", "det-glyph");
                ring.Add(d.Fill); ring.Add(d.Glyph);
                if (edge)
                {
                    d.Root.AddToClassList("edge-pip");
                    d.Arrow = new EdgeArrow();
                    d.Root.Add(d.Arrow);
                }
                d.Root.Add(ring);
                foreach (var c in d.Root.Query<VisualElement>().ToList()) c.pickingMode = PickingMode.Ignore;
                _markerLayer.Add(d.Root);
                pool.Add(d);
            }
            return pool[i];
        }

        /// <summary>Writes a guard's meter into a marker: fill height and colour, the band's pattern, glyph, ring.</summary>
        static void FillMeter(DetMarker d, Npc n, float fill, string glyph, bool ringInState = false)
        {
            var col = Visual.ConeRenderer.StateColor(n);
            if (n.State == NpcState.Relaxed) col = Color.Lerp(Mats.Pal.Suspicious, Mats.Pal.Alerted, fill);
            fill = Mathf.Clamp01(fill);
            if (Mathf.Abs(fill - d.LastFill) > 0.004f) { d.Fill.style.height = Length.Percent(fill * 100f); d.LastFill = fill; }
            var feed = n.MeterFeed;
            bool patterned = MeterPattern(feed) != null;
            var fc = col; fc.a = patterned ? 0.32f : 0.85f;
            if (fc != d.LastFillCol || feed != d.LastFeed)
            {
                d.Fill.style.backgroundColor = fc;
                d.LastFillCol = fc;
                var pc = col; pc.a = 1f;
                d.Pat.style.unityBackgroundImageTintColor = pc;
            }
            if (feed != d.LastFeed)
            {
                d.LastFeed = feed;
                var tex = MeterPattern(feed);
                d.Pat.style.backgroundImage = tex != null ? new StyleBackground(tex) : new StyleBackground(StyleKeyword.None);
            }
            if (d.Glyph.text != glyph) d.Glyph.text = glyph;
            var gc = n.State == NpcState.Relaxed ? (Color)Mats.Pal.Bone : Color.white;
            if (gc != d.LastGlyphCol) { d.Glyph.style.color = gc; d.LastGlyphCol = gc; }
            var rc = n.SeesPlayer || ringInState ? (ringInState ? Visual.ConeRenderer.StateColor(n) : col) : new Color(0, 0, 0, 0.6f);
            if (rc != d.LastRing) { SetBorderColor(d.Root.Q(className: "det"), rc); d.LastRing = rc; }
        }

        static Texture2D[] _patterns;

        /// <summary>Fill patterns by the sense feeding a meter (SR.9): none (solid) for near or touch, diagonal stripes for
        /// the lit far band, waves for heard, dots for smelled. White on clear, tinted by the meter's colour.</summary>
        static Texture2D MeterPattern(MeterRead.Feed f)
        {
            if (f != MeterRead.Feed.Far && f != MeterRead.Feed.Heard && f != MeterRead.Feed.Smelled) return null;
            if (_patterns == null)
            {
                _patterns = new Texture2D[5];
                const int N = 28;
                foreach (var k in new[] { MeterRead.Feed.Far, MeterRead.Feed.Heard, MeterRead.Feed.Smelled })
                {
                    var t = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "meter_" + k };
                    var px = new Color32[N * N];
                    for (int y = 0; y < N; y++)
                        for (int x = 0; x < N; x++)
                        {
                            bool on = k == MeterRead.Feed.Far ? (x + y) % 7 < 3
                                : k == MeterRead.Feed.Heard ? Mathf.Abs(Mathf.Repeat(y + 2.2f * Mathf.Sin(x * 0.5f), 7f) - 3.5f) < 1.3f
                                : (new Vector2(Mathf.Repeat(x + (y / 7 % 2) * 3.5f, 7f), Mathf.Repeat(y, 7f)) - new Vector2(3.5f, 3.5f)).sqrMagnitude < 3.2f;
                            px[y * N + x] = on ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
                        }
                    t.SetPixels32(px);
                    t.Apply(false, true);
                    _patterns[(int)k] = t;
                }
            }
            return _patterns[(int)f];
        }

        void TickMarkers(Vampire p)
        {
            var cam = Game.Cam != null ? Game.Cam.Cam : null;
            if (cam == null) return;

            // ---- detection meters over every aware human; an edge pip for one off screen (SR.9, SR.11)
            int used = 0, edged = 0;
            bool tactical = ConeRenderer.Instance != null && ConeRenderer.Instance.AllShown;
            if (Game.AI != null)
                foreach (var n in Game.AI.Npcs)
                {
                    if (!n || !n.IsAlive || n.Incapacitated || n.IsThrall || !n.gameObject.activeInHierarchy) continue;
                    string glyph = null;
                    float fill = n.Detection;
                    switch (n.State)
                    {
                        case NpcState.Alerted: glyph = "!"; fill = 1f; break;
                        case NpcState.Panicked: glyph = "!!"; fill = 1f; break;
                        case NpcState.Suspicious: case NpcState.Investigating: case NpcState.Searching: glyph = "?"; fill = Mathf.Max(fill, 0.5f); break;
                        case NpcState.Mesmerised: glyph = "~"; fill = 1f; break;
                        default: if (n.Detection > 0.02f) glyph = ""; break;
                    }
                    // Wary, with no meter on her: a small ring at his head (SR.5), within the aware range only
                    bool waryMark = glyph == null && n.Wary && n.State == NpcState.Relaxed && !n.Friendly
                        && Util.FlatDistance(n.transform.position, p.Feet) <= ConeContext.AwareRange;
                    var above = n.transform.position + Vector3.up * 2.35f;
                    if (!OnScreen(above, cam, 0f))
                    {
                        // off screen: a pip on the edge, pointing at him, with his meter and which way he faces
                        if (n.Friendly || n.State == NpcState.Mesmerised) continue;
                        float dist = Util.FlatDistance(n.transform.position, p.Feet);
                        bool pinned = ConeRenderer.Instance != null && ConeRenderer.Instance.IsInspected(n);
                        bool soon = ConeRenderer.Instance != null && ConeRenderer.Instance.ContactIn(n) <= ConeContext.ContactHorizon;
                        if (!MeterRead.EdgePip(dist, glyph != null, pinned, tactical, soon)) continue;
                        var e = GetMeter(_edgePool, edged++, true);
                        if (!e.Shown) { Show(e.Root, true); e.Shown = true; }
                        PlaceEdge(e, n, cam);
                        FillMeter(e, n, fill, glyph ?? "");
                        continue;
                    }
                    if (glyph == null && !waryMark) continue;
                    var d = GetDet(used++);
                    if (!d.Shown) { Show(d.Root, true); d.Shown = true; }
                    if (!PlaceAt(d.Root, above, cam)) continue;
                    FillMeter(d, n, waryMark ? 0f : fill, glyph ?? "", waryMark);
                    float s = waryMark ? 0.45f : n.SeesPlayer ? 1.1f : 0.9f;
                    if (s != d.LastScale) { d.Root.style.scale = new Scale(new Vector2(s, s)); d.LastScale = s; }
                }
            for (int i = edged; i < _edgePool.Count; i++) if (_edgePool[i].Shown) { Show(_edgePool[i].Root, false); _edgePool[i].Shown = false; }
            for (int i = used; i < _detPool.Count; i++) if (_detPool[i].Shown) { Show(_detPool[i].Root, false); _detPool[i].Shown = false; }

            // ---- hover prompt: placed every frame, its text rebuilt only for a new target or ten times a second
            string name = null, verbs = null;
            Vector3 at = default;
            bool pointerFree = !PointerOverUI && !BlocksGameplay;
            object hoverKey = !pointerFree || p.Aiming != null || p.Dead ? null
                : p.HoverNpc ? p.HoverNpc : p.HoverUse != null && p.HoverUse.ShowMarker ? p.HoverUse : p.HoverLight != null && p.HoverLight.On ? (object)p.HoverLight
                : p.SelectedThrall == null && p.Feeding == null ? p.NearUse : null;
            _hoverT -= Time.unscaledDeltaTime;
            bool rebuild = !ReferenceEquals(hoverKey, _hoverKey) || _hoverT <= 0f;
            if (!rebuild && hoverKey != null)
            {
                name = _hoverName.text; verbs = _hoverVerbs.text;
                at = HoverAnchor(p);
            }
            else if (pointerFree && p.Aiming == null && !p.Dead)
            {
                var inp = Game.Input;
                var h = p.HoverNpc;
                if (h)
                {
                    at = h.transform.position + Vector3.up * (h.IsBody ? 1.0f : 2.0f);
                    if (h.IsThrall) { name = $"{h.DisplayName}  <color=#b88ae8>thrall</color>"; verbs = $"{h.OrderLabel}   [LMB on portrait / {GameInput.Key(inp.CycleThrall)}] command"; }
                    else if (h.IsBody)
                    {
                        name = h.DisplayName + (h.State == NpcState.Dazed ? "  <color=#9a9088>unconscious</color>" : "  <color=#9a9088>dead</color>");
                        verbs = p.Carrying == null ? (p.CarryGap(h) <= 0f ? $"[{GameInput.Key(inp.Carry)}] Carry" : FarVerbs($"[{GameInput.Key(inp.Carry)}] Carry", h.transform.position, p)) : "";
                        if (h.State == NpcState.Dazed && h.CanBeFedBy(p, out _)) verbs += $"   [{GameInput.Key(inp.Sip)}] Sip   [{GameInput.Key(inp.Drain)}] Drain";
                    }
                    else
                    {
                        name = h.DisplayName + (h.Notable ? "  <color=#d6b264>notable</color>" : "") + $"  <color=#9a9088>{h.Arch.Name}</color>";
                        verbs = !h.CanBeFedBy(p, out var why) ? $"<color=#c88070>{why}</color>"
                            : p.FeedGap(h) > 0f ? FarVerbs($"[{GameInput.Key(inp.Sip)}] Sip   [{GameInput.Key(inp.Drain)}] Drain", h.transform.position, p)
                            : $"[{GameInput.Key(inp.Sip)}] Sip   [{GameInput.Key(inp.Drain)}] Drain   <color=#9a9088>{BloodLabel(h.Arch.Blood)}</color>";
                        if (ConeRenderer.Instance != null) verbs += $"   [MMB] {(ConeRenderer.Instance.IsInspected(h) ? "unpin" : "pin")} cone";
                    }
                }
                else if (hoverKey is Interactable u)
                {
                    // under the cursor, or (D119) simply beside her: the key is the way in, the mouse is optional
                    at = u.transform.position + Vector3.up * 1.6f;
                    name = u.DisplayName;
                    string keys = u == p.HoverUse ? $"LMB / {GameInput.Key(inp.Interact)}" : GameInput.Key(inp.Interact);
                    verbs = !u.PlayerCan ? $"<color=#c88070>{u.Unavailable ?? "Cannot be used"}</color>"
                        : p.InUseReach(u) ? $"[{keys}] {u.Verb}" : FarVerbs($"[{keys}] {u.Verb}", u.transform.position, p);
                    if (u.ThrallCan && !(u is Door)) verbs += "   <color=#b88ae8>a thrall can do this</color>";
                    if (u is Vespertine.AI.Shackles sh)
                    {
                        at = u.transform.position + Vector3.up * 2.0f;
                        if (sh.Owner.State == NpcState.Escort) name += sh.Owner.EscortWait ? "  <color=#9a9088>waiting</color>" : "  <color=#9a9088>following</color>";
                        else name += "  <color=#9a9088>shackled</color>";
                        if (sh.CanLoose && sh.Owner.State == NpcState.Escort) verbs += $"   [{GameInput.Key(inp.Drain)}] Turn loose";
                    }
                }
                else if (p.HoverLight != null && p.HoverLight.On)
                {
                    var l = p.HoverLight;
                    at = l.SourcePos + Vector3.up * 0.5f;
                    name = LightName(l.Kind);
                    verbs = l.CanSnuffByHand ? (p.InSnuffReach(l) ? $"[{GameInput.Key(inp.Interact)}] Snuff" : FarVerbs($"[{GameInput.Key(inp.Interact)}] Snuff", l.transform.position, p)) : l.Caged ? "<color=#c88070>Caged: cannot be snuffed by hand</color>" : l.Burns ? "<color=#ffcf80>It burns her kind</color>" : "";
                }
            }
            if (rebuild) { _hoverKey = hoverKey; _hoverT = 0.1f; }
            if (name != null)
            {
                if (rebuild) { _hoverName.text = name; _hoverVerbs.text = verbs ?? ""; }
                Show(_hoverVerbs, !string.IsNullOrEmpty(verbs));
                Show(_hoverPrompt, true);
                PlaceAt(_hoverPrompt, at, cam);
            }
            else Show(_hoverPrompt, false);

            // ---- feed / use progress over Ilse
            float prog = p.Feeding != null ? p.FeedProgress : p.ChannelProgress;
            if (prog > 0.001f && !p.Dead)
            {
                Show(_progress, true);
                PlaceAt(_progress, p.Feet + Vector3.up * 2.3f, cam);
                SetFill(_progressFill, prog);
                _progressFill.style.backgroundColor = p.Feeding != null ? (Color)Mats.Pal.BloodBright : (Color)Mats.Pal.Bone;
            }
            else Show(_progress, false);

            // ---- aim tip at the cursor
            if (p.Aiming != null && !p.Dead)
            {
                var mp = Game.Input.MousePos;
                var pp = RuntimePanelUtils.ScreenToPanel(_root.panel, new Vector2(mp.x, Screen.height - mp.y));
                _aimTip.style.left = pp.x + 22;
                _aimTip.style.top = pp.y + 18;
                string step = AimInstruction(p);
                _aimTip.text = $"<b>{p.Aiming.Name}</b>  {step}" + (p.AimError != null ? $"\n<color=#e06a60>{p.AimError}</color>" : "") + "\n<color=#8a8078>RMB cancels</color>";
                _aimTip.EnableInClassList("error", false);
                Show(_aimTip, true);
            }
            else Show(_aimTip, false);

            // ---- objective pins (reach / escape / deliver areas)
            int pin = 0;
            foreach (var o in Game.Mission.Objectives)
            {
                if (!o.Active || !o.Visible || o.IsConduct) continue;
                if (o.Type != "reach" && o.Type != "escape" && o.Type != "deliver") continue;
                if (Game.Level == null || !o.TryRect(out _)) continue;
                var w = o.RectCenterWorld(Game.Level) + Vector3.up * 1.2f;
                while (_objPins.Count <= pin)
                {
                    var pe = E("marker");
                    var dia = new VisualElement();
                    dia.style.width = 14; dia.style.height = 14;
                    dia.style.rotate = new Rotate(new Angle(45f));
                    dia.style.borderLeftWidth = dia.style.borderRightWidth = dia.style.borderTopWidth = dia.style.borderBottomWidth = 2;
                    SetBorderColor(dia, new Color(0.84f, 0.7f, 0.39f));
                    dia.style.backgroundColor = new Color(0.1f, 0.08f, 0.06f, 0.7f);
                    pe.Add(dia);
                    var pl = L("", "tiny");
                    pl.style.color = new Color(0.84f, 0.7f, 0.39f);
                    pl.style.marginTop = 4;
                    pe.Add(pl);
                    foreach (var c in pe.Query<VisualElement>().ToList()) c.pickingMode = PickingMode.Ignore;
                    _markerLayer.Add(pe);
                    _objPins.Add(pe);
                }
                var el = _objPins[pin++];
                Show(el, true);
                PlaceAtClamped(el, w, cam);
                float dist = Vector3.Distance(w, p.Feet);
                int dm = dist > 6f ? Mathf.RoundToInt(dist) : -1;
                while (_pinDist.Count < _objPins.Count) _pinDist.Add(int.MinValue);
                if (dm != _pinDist[pin - 1]) { _pinDist[pin - 1] = dm; ((Label)el[1]).text = dm >= 0 ? dm + " m" : ""; }
            }
            for (int i = pin; i < _objPins.Count; i++) Show(_objPins[i], false);
        }

        readonly List<int> _pinDist = new List<int>();
        object _hoverKey;
        float _hoverT;

        /// <summary>Verbs she cannot reach from here, greyed, with the distance (P7: she does not walk to them).</summary>
        static string FarVerbs(string verbs, Vector3 at, Vampire p) => $"<color=#6f6a78>{verbs}   {Vampire.TooFar(Util.FlatDistance(at, p.Feet))}</color>";

        /// <summary>Where the hover prompt sits for whatever is under the cursor (matches the anchors chosen in TickMarkers).</summary>
        static Vector3 HoverAnchor(Vampire p)
        {
            var h = p.HoverNpc;
            if (h) return h.transform.position + Vector3.up * (h.IsBody ? 1.0f : 2.0f);
            var u = p.HoverUse;
            if (u != null && u.ShowMarker) return u.transform.position + Vector3.up * (u is Vespertine.AI.Shackles ? 2.0f : 1.6f);
            var l = p.HoverLight;
            if (l != null) return l.SourcePos + Vector3.up * 0.5f;
            u = p.NearUse;
            return u != null ? u.transform.position + Vector3.up * 1.6f : p.Feet;
        }

        /// <summary>Puts an edge pip where the line from the screen centre to the guard leaves the screen, and turns its
        /// pointer toward him and its wedge the way he faces.</summary>
        void PlaceEdge(DetMarker e, Npc n, Camera cam)
        {
            var at = n.transform.position + Vector3.up * 1.2f;
            var sp = cam.WorldToScreenPoint(at);
            var pt = MeterRead.EdgePoint(sp, Screen.width, Screen.height, 52f, out var dir);
            var pp = RuntimePanelUtils.ScreenToPanel(_root.panel, new Vector2(pt.x, Screen.height - pt.y));
            if (Mathf.Abs(e.Root.style.left.value.value - pp.x) > 0.25f) e.Root.style.left = pp.x;
            if (Mathf.Abs(e.Root.style.top.value.value - pp.y) > 0.25f) e.Root.style.top = pp.y;
            e.Root.style.visibility = Visibility.Visible;
            // his facing on screen: the step from his feet one metre along it
            var f0 = cam.WorldToScreenPoint(n.transform.position);
            var f1 = cam.WorldToScreenPoint(n.transform.position + n.Forward);
            var face = new Vector2(f1.x - f0.x, f1.y - f0.y);
            if (f0.z < 0f) face = -face;
            var col = Visual.ConeRenderer.StateColor(n);
            // panel space is y down
            e.Arrow.Set(new Vector2(dir.x, -dir.y), face.sqrMagnitude > 1e-4f ? new Vector2(face.x, -face.y).normalized : Vector2.zero, n.Vision.HalfAngle, col);
        }

        /// <summary>Like PlaceAt, but keeps the element inside the screen edge when the point is off screen.</summary>
        void PlaceAtClamped(VisualElement el, Vector3 world, Camera cam)
        {
            var sp = cam.WorldToScreenPoint(world);
            if (sp.z < 0) { sp.x = Screen.width - sp.x; sp.y = Screen.height - sp.y; sp.z = 1; }
            float m = 40f;
            sp.x = Mathf.Clamp(sp.x, m, Screen.width - m);
            sp.y = Mathf.Clamp(sp.y, m + 30f, Screen.height - m);
            var pp = RuntimePanelUtils.ScreenToPanel(_root.panel, new Vector2(sp.x, Screen.height - sp.y));
            el.style.left = pp.x;
            el.style.top = pp.y;
            el.style.visibility = Visibility.Visible;
        }

        static string AimInstruction(Vampire p)
        {
            var a = p.Aiming;
            if (a.Id == "dominion.false_orders") return p.AimStage == 0 ? "Click the human to deceive" : "Click where to send them";
            switch (a.Targeting)
            {
                case Targeting.Npc: return "Click a human";
                case Targeting.Light: return "Click a light";
                case Targeting.Corpse: return "Click a body";
                case Targeting.Point: return "Click a place";
            }
            return "";
        }

        public static string BloodLabel(BloodType t)
        {
            switch (t)
            {
                case BloodType.Drunk: return "wine-thick blood";
                case BloodType.Fevered: return "fevered blood";
                case BloodType.Soldier: return "iron blood";
                case BloodType.Priest: return "consecrated blood";
                case BloodType.Occult: return "occult blood";
                case BloodType.Notable: return "notable blood";
                case BloodType.Animal: return "animal blood";
            }
            return "common blood";
        }

        static string LightName(LightKind k)
        {
            switch (k)
            {
                case LightKind.GasLamp: return "Gas lamp";
                case LightKind.WallLamp: return "Wall lamp";
                case LightKind.Lantern: return "Lantern";
                case LightKind.Brazier: return "Brazier";
                case LightKind.Candle: return "Candles";
                case LightKind.Chandelier: return "Chandelier";
                case LightKind.Sunstone: return "Sunstone";
                case LightKind.Window: return "Lit window";
                case LightKind.Holy: return "Votive light";
                case LightKind.Fire: return "Fire";
                case LightKind.Sunbeam: return "Sunlight";
            }
            return "Light";
        }
    }
}
