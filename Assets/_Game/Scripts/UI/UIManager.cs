using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Vespertine.AI;
using Vespertine.Controls;
using Vespertine.Core;
using Vespertine.Data;

namespace Vespertine.UI
{
    /// <summary>
    /// Every screen of the game, built in code with UI Toolkit. Split across partial files:
    /// UIManager (core, layers, toasts, subtitles, modals, input routing), UIHud (in-mission HUD and world markers),
    /// UIMenus (main menu, new game, load, pause, settings, save slots), UIHub (the Refuge between missions),
    /// UIScreens (mission intro, debrief, death, failure, endings, debug console).
    /// </summary>
    public partial class UIManager : MonoBehaviour
    {
        UIDocument _doc;
        PanelSettings _panel;
        VisualElement _root;
        VisualElement _hudLayer, _markerLayer, _overlayLayer, _screenLayer, _modalLayer, _topLayer;
        VisualElement _fade;

        // ---------------------------------------------------------------- lifecycle
        void Awake()
        {
            Game.UI = this;
            _panel = Resources.Load<PanelSettings>("UI/PanelSettings");
            if (_panel == null)
            {
                _panel = ScriptableObject.CreateInstance<PanelSettings>();
                _panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
                _panel.referenceResolution = new Vector2Int(1920, 1080);
                _panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
                _panel.match = 0.5f;
                Debug.LogWarning("[UI] Resources/UI/PanelSettings missing; using a runtime panel without a theme.");
            }
            else _panel = Instantiate(_panel);
            _panel.sortingOrder = 10;
            _doc = gameObject.AddComponent<UIDocument>();
            _doc.panelSettings = _panel;
            ApplyScale();

            _root = _doc.rootVisualElement;
            var uss = Resources.Load<StyleSheet>("UI/Vespertine");
            if (uss) _root.styleSheets.Add(uss);
            else Debug.LogWarning("[UI] Resources/UI/Vespertine.uss missing");
            _root.AddToClassList("root");
            _root.pickingMode = PickingMode.Ignore;

            _hudLayer = Layer("hud");
            _markerLayer = Layer("markers");
            _overlayLayer = Layer("overlay");
            _screenLayer = Layer("screens");
            _modalLayer = Layer("modals");
            _topLayer = Layer("top");
            // markers sit below the HUD panels so prompts never cover the ability bar
            _markerLayer.SendToBack();

            _fade = E("fade");
            _fade.pickingMode = PickingMode.Ignore;
            _fade.style.opacity = 0f;
            _topLayer.Add(_fade);

            BuildOverlay();
            BuildHud();
            SetHudVisible(false);

            GameEvents.Bark += OnBark;
            GameEvents.Toast += Toast;
            GameEvents.SpottedCaption += OnSpotted;
        }

        void OnDestroy()
        {
            GameEvents.Bark -= OnBark;
            GameEvents.Toast -= Toast;
            GameEvents.SpottedCaption -= OnSpotted;
            if (Game.UI == this) Game.UI = null;
        }

        public void ApplyScale()
        {
            if (_panel == null) return;
            _panel.scale = Game.Settings != null ? Mathf.Clamp(Game.Settings.UiScale, 0.6f, 1.6f) : 1f;
        }

        VisualElement Layer(string name)
        {
            var v = new VisualElement { name = name, pickingMode = PickingMode.Ignore };
            v.AddToClassList("layer");
            _root.Add(v);
            return v;
        }

        // ---------------------------------------------------------------- element helpers
        public static VisualElement E(params string[] classes)
        {
            var v = new VisualElement();
            foreach (var c in classes) if (!string.IsNullOrEmpty(c)) v.AddToClassList(c);
            return v;
        }

        public static Label L(string text, params string[] classes)
        {
            var l = new Label(text);
            foreach (var c in classes) if (!string.IsNullOrEmpty(c)) l.AddToClassList(c);
            return l;
        }

        public static Button B(string text, Action onClick, params string[] classes)
        {
            var b = new Button(() =>
            {
                Game.Audio?.Play2D("ui_click", 0.5f);
                onClick?.Invoke();
            }) { text = text };
            b.AddToClassList("btn");
            foreach (var c in classes) if (!string.IsNullOrEmpty(c)) b.AddToClassList(c);
            b.RegisterCallback<MouseEnterEvent>(_ => { if (b.enabledSelf) Game.Audio?.Play2D("ui_hover", 0.25f); });
            return b;
        }

        public static VisualElement Row(params string[] classes) { var v = E(classes); v.AddToClassList("row"); return v; }
        public static VisualElement Col(params string[] classes) { var v = E(classes); v.AddToClassList("col"); return v; }
        public static VisualElement Space(float px) { var v = new VisualElement(); v.style.height = px; v.style.width = px; v.style.flexShrink = 0; return v; }

        public static VisualElement Bar(string fillClass, out VisualElement fill, float width = -1)
        {
            var bar = E("bar");
            if (width > 0) bar.style.width = width;
            fill = E("bar-fill", fillClass);
            bar.Add(fill);
            return bar;
        }

        public static void SetFill(VisualElement fill, float t) => fill.style.width = Length.Percent(Mathf.Clamp01(t) * 100f);

        public static VisualElement StatRow(string k, string v)
        {
            var r = Row("stat-row");
            r.Add(L(k, "stat-k"));
            r.Add(L(v, "stat-v"));
            return r;
        }

        public static void Show(VisualElement v, bool on)
        {
            if (v == null) return;
            v.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // ---------------------------------------------------------------- state queries used by gameplay
        int _pointerFrame = -1;
        bool _pointerOver;

        /// <summary>True when the mouse is over an interactive UI element (clicks should not reach the world).</summary>
        public bool PointerOverUI
        {
            get
            {
                if (_pointerFrame == Time.frameCount) return _pointerOver;
                _pointerFrame = Time.frameCount;
                _pointerOver = false;
                if (_root?.panel == null || Game.Input == null) return false;
                var mp = Game.Input.MousePos;
                var pp = RuntimePanelUtils.ScreenToPanel(_root.panel, new Vector2(mp.x, Screen.height - mp.y));
                var picked = _root.panel.Pick(pp);
                _pointerOver = picked != null && picked != _root;
                return _pointerOver;
            }
        }

        /// <summary>A menu, modal or console has focus: gameplay input must be ignored.</summary>
        public bool BlocksGameplay => _screens.Count > 0 || _modals.Count > 0 || _consoleOpen || (Game.Input != null && Game.Input.Rebinding);

        /// <summary>A modal dialog (document, choice, confirmation) or full-screen menu is open.</summary>
        public bool ModalOpen => _modals.Count > 0 || _screens.Count > 0;

        // ---------------------------------------------------------------- screens (stacked full-screen panels)
        class ScreenEntry { public VisualElement El; public Action OnBack; public bool PausesGame; public string Name; }
        readonly List<ScreenEntry> _screens = new List<ScreenEntry>();

        /// <summary>The document's root (dev tools walk it to press buttons by label).</summary>
        public VisualElement RootElement => _root;

        public string TopScreen => _screens.Count > 0 ? _screens[_screens.Count - 1].Name : null;

        /// <summary>Pushes a full-screen panel. onBack runs on Esc (null = Esc does nothing).</summary>
        public VisualElement PushScreen(string name, VisualElement el, Action onBack, bool pausesGame = true, bool hideBelow = true)
        {
            if (hideBelow) foreach (var s in _screens) Show(s.El, false);
            el.name = name;
            el.AddToClassList("layer");
            _screenLayer.Add(el);
            _screens.Add(new ScreenEntry { El = el, OnBack = onBack, PausesGame = pausesGame, Name = name });
            RefreshPause();
            return el;
        }

        public void PopScreen()
        {
            if (_screens.Count == 0) return;
            var s = _screens[_screens.Count - 1];
            _screens.RemoveAt(_screens.Count - 1);
            s.El.RemoveFromHierarchy();
            if (_screens.Count > 0) Show(_screens[_screens.Count - 1].El, true);
            RefreshPause();
        }

        public void PopScreen(string name)
        {
            for (int i = _screens.Count - 1; i >= 0; i--)
                if (_screens[i].Name == name) { while (_screens.Count > i) PopScreen(); return; }
        }

        public void ClearScreens()
        {
            while (_screens.Count > 0) PopScreen();
        }

        public bool HasScreen(string name) => _screens.Exists(s => s.Name == name);

        void RefreshPause()
        {
            bool pause = _screens.Exists(s => s.PausesGame);
            if (Game.InMission && !Game.Mission.Ended) Game.MenuPaused = pause;
            Game.ModalPaused = _modals.Count > 0;
            if (Game.Input != null) Game.Input.SetGameplayEnabled(!BlocksGameplay);
        }

        // ---------------------------------------------------------------- modals (document / choice / confirm)
        class ModalEntry { public VisualElement El; public Action OnBack; }
        readonly List<ModalEntry> _modals = new List<ModalEntry>();

        VisualElement PushModal(VisualElement content, Action onBack)
        {
            var scrim = E("layer", "scrim", "center");
            scrim.Add(content);
            _modalLayer.Add(scrim);
            _modals.Add(new ModalEntry { El = scrim, OnBack = onBack });
            RefreshPause();
            content.style.opacity = 0f;
            content.schedule.Execute(() => content.style.opacity = 1f).StartingIn(10);
            return scrim;
        }

        void CloseModal(VisualElement scrim)
        {
            var m = _modals.Find(x => x.El == scrim);
            if (m == null) return;
            _modals.Remove(m);
            scrim.RemoveFromHierarchy();
            RefreshPause();
        }

        public void HideModals()
        {
            foreach (var m in _modals) m.El.RemoveFromHierarchy();
            _modals.Clear();
            RefreshPause();
        }

        public void ShowDocument(string title, string body)
        {
            Game.Audio?.Play2D("page", 0.7f);
            var p = Col("panel", "modal");
            p.Add(L(title, "h1"));
            p.Add(E("divider"));
            var sv = new ScrollView(ScrollViewMode.Vertical);
            sv.style.maxHeight = 560;
            sv.Add(L(body.Replace("\\n", "\n"), "doc-body"));
            p.Add(sv);
            p.Add(Space(12));
            VisualElement scrim = null;
            var close = B("Close", () => CloseModal(scrim), "btn-primary");
            close.style.alignSelf = Align.FlexEnd;
            p.Add(close);
            scrim = PushModal(p, () => CloseModal(scrim));
        }

        /// <summary>Development seam (the campaign run): answers a scripted choice at once with the returned key.</summary>
        public static Func<List<KeyValuePair<string, string>>, string> AutoPick;

        public void ShowChoice(string prompt, List<KeyValuePair<string, string>> opts, Action<string> cb)
        {
            if (AutoPick != null) { cb?.Invoke(AutoPick(opts)); return; }
            var p = Col("panel", "modal");
            p.Add(L(prompt, "h2"));
            p.Add(E("divider"));
            VisualElement scrim = null;
            int i = 1;
            foreach (var kv in opts)
            {
                var key = kv.Key;
                var b = B($"{i}.  {kv.Value}", () => { CloseModal(scrim); cb?.Invoke(key); });
                b.style.unityTextAlign = TextAnchor.MiddleLeft;
                b.style.whiteSpace = WhiteSpace.Normal;
                p.Add(b);
                i++;
            }
            scrim = PushModal(p, null); // a choice must be made
        }

        public void Confirm(string title, string text, string yes, Action onYes, string no = "Cancel", Action onNo = null)
        {
            var p = Col("panel", "modal");
            p.Add(L(title, "h2"));
            if (!string.IsNullOrEmpty(text)) p.Add(L(text, "body"));
            p.Add(Space(14));
            var r = Row();
            r.style.justifyContent = Justify.FlexEnd;
            VisualElement scrim = null;
            r.Add(B(no, () => { CloseModal(scrim); onNo?.Invoke(); }));
            r.Add(B(yes, () => { CloseModal(scrim); onYes?.Invoke(); }, "btn-primary"));
            p.Add(r);
            scrim = PushModal(p, () => { CloseModal(scrim); onNo?.Invoke(); });
        }

        public void Alert(string title, string text)
        {
            var p = Col("panel", "modal");
            p.Add(L(title, "h2"));
            p.Add(L(text, "body"));
            p.Add(Space(14));
            VisualElement scrim = null;
            var ok = B("OK", () => CloseModal(scrim), "btn-primary");
            ok.style.alignSelf = Align.FlexEnd;
            p.Add(ok);
            scrim = PushModal(p, () => CloseModal(scrim));
        }

        // ---------------------------------------------------------------- overlay: toasts, subtitles, hints, barks, save indicator
        VisualElement _toastBox, _subBox, _hintBox, _saveInd;
        Label _hintKey, _hintText;
        float _hintUntil, _saveIndUntil;

        class TimedEl { public VisualElement El; public float Until; }
        readonly List<TimedEl> _toasts = new List<TimedEl>();

        class SubLine { public string Who, Text; public float Dur; }
        readonly Queue<SubLine> _subQueue = new Queue<SubLine>();
        float _subUntil;
        Label _subLabel;

        class Bark { public VisualElement El; public Transform Who; public Vector3 Pos; public float Until; }
        readonly List<Bark> _barks = new List<Bark>();

        void BuildOverlay()
        {
            _toastBox = E("toasts"); _toastBox.pickingMode = PickingMode.Ignore;
            _overlayLayer.Add(_toastBox);

            _subBox = E("subtitle-box"); _subBox.pickingMode = PickingMode.Ignore;
            _subLabel = L("", "subtitle-line");
            _subLabel.enableRichText = true;
            _subBox.Add(_subLabel);
            Show(_subBox, false);
            _overlayLayer.Add(_subBox);

            _hintBox = E("hint"); _hintBox.pickingMode = PickingMode.Ignore;
            _hintKey = L("", "hint-key");
            _hintText = L("", "body", "small");
            _hintBox.Add(_hintKey);
            _hintBox.Add(_hintText);
            Show(_hintBox, false);
            _overlayLayer.Add(_hintBox);

            _saveInd = L("SAVING…", "save-ind");
            Show(_saveInd, false);
            _overlayLayer.Add(_saveInd);
        }

        public void Toast(string text) => Toast(text, null);

        public void Toast(string text, string cls)
        {
            if (string.IsNullOrEmpty(text)) return;
            // collapse duplicates
            foreach (var t in _toasts)
                if (t.El is Label l && l.text == text) { t.Until = Time.unscaledTime + 3.5f; return; }
            var lbl = L(text, "toast");
            if (cls != null) lbl.AddToClassList(cls);
            lbl.pickingMode = PickingMode.Ignore;
            _toastBox.Add(lbl);
            _toasts.Add(new TimedEl { El = lbl, Until = Time.unscaledTime + 3.5f });
            while (_toasts.Count > 5) { _toasts[0].El.RemoveFromHierarchy(); _toasts.RemoveAt(0); }
        }

        public void ObjectiveToast(Mission.Objective o, bool revealed)
        {
            if (o == null) return;
            string prefix = o.Failed ? "Objective failed" : o.Complete ? (o.Primary ? "Objective complete" : "Optional complete") : revealed ? (o.Primary ? "New objective" : "New optional objective") : "Objective";
            Toast($"{prefix}: {o.Spec.Text}", "objective");
            Game.Audio?.Play2D(o.Failed ? "fail" : o.Complete ? "objective" : "ui_confirm", 0.6f);
            o.FlashT = 2.5f;
            _objDirty = true;
        }

        public void Subtitle(string who, string text, float dur)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (dur <= 0f) dur = Mathf.Clamp(1.6f + text.Length * 0.055f, 2.2f, 9f);
            _subQueue.Enqueue(new SubLine { Who = who, Text = text, Dur = dur });
            if (Time.unscaledTime >= _subUntil) NextSubtitle();
        }

        void NextSubtitle()
        {
            if (_subQueue.Count == 0) { Show(_subBox, false); return; }
            var s = _subQueue.Dequeue();
            _subLabel.text = string.IsNullOrEmpty(s.Who) ? s.Text : SpeakerLine(s.Who, s.Text);
            _subUntil = Time.unscaledTime + s.Dur;
            Show(_subBox, true);
        }

        /// <summary>Ilse in blood red; the Abbess (a voice in the blood) in Dominion violet and italics; everyone else in bone.</summary>
        static string SpeakerLine(string who, string text)
        {
            if (who == "Abbess" || who == "The Abbess") return $"<b><color=#a070e0>The Abbess</color></b>   <i><color=#d8c8f0>{text}</color></i>";
            return $"<b><color={(who == "Ilse" ? "#d0283a" : "#d8c8a0")}>{who}</color></b>   {text}";
        }

        public bool SubtitlesBusy => _subQueue.Count > 0 || Time.unscaledTime < _subUntil;

        public void ClearSubtitles() { _subQueue.Clear(); _subUntil = 0; Show(_subBox, false); }

        public void Hint(string text, string key)
        {
            if (Game.Settings != null && !Game.Settings.TutorialHints) return;
            if (string.IsNullOrEmpty(text)) return;
            _hintKey.text = string.IsNullOrEmpty(key) ? "TIP" : KeyLabel(key);
            _hintText.text = ExpandKeys(text.Replace("\\n", "\n"));
            _hintUntil = Time.unscaledTime + Mathf.Clamp(5f + text.Length * 0.05f, 7f, 14f);
            Show(_hintBox, true);
            Game.Audio?.Play2D("ui_confirm", 0.35f);
        }

        /// <summary>"{Interact}" in tip text becomes the key it is bound to now ("[E]"), so tips follow rebinding.</summary>
        public static string ExpandKeys(string text) =>
            text.IndexOf('{') < 0 ? text : System.Text.RegularExpressions.Regex.Replace(text, @"\{([A-Za-z0-9]+)\}", m => KeyLabel(m.Groups[1].Value));

        /// <summary>Turns an action name ("Sip", "Ability1", "Traverse") or literal key into display text.</summary>
        public static string KeyLabel(string key)
        {
            var inp = Game.Input;
            if (inp != null)
            {
                var a = inp.Asset.FindAction(key, false);
                if (a != null) return "[" + GameInput.Key(a) + "]";
            }
            return "[" + key.ToUpperInvariant() + "]";
        }

        public void SaveIndicator()
        {
            _saveIndUntil = Time.unscaledTime + 1.8f;
            Show(_saveInd, true);
        }

        void OnBark(string who, string text)
        {
            if (string.IsNullOrEmpty(text) || !Game.InMission) return;
            Transform t = null;
            var e = Game.Level != null ? Game.Level.Get(who) : null;
            if (e) t = e.transform;
            if (t == null && Game.AI != null) { var n = Game.AI.Npcs.Find(x => x && x.Id == who); if (n) t = n.transform; }
            if (t == null) return;
            // a speaker well off screen is not heard; one just past the edge is, pinned to that edge
            var cam = Game.Cam != null ? Game.Cam.Cam : null;
            if (cam && !OnScreen(t.position + Vector3.up * 2.4f, cam, BarkMargin)) return;
            // replace an existing bark from the same speaker
            for (int i = _barks.Count - 1; i >= 0; i--) if (_barks[i].Who == t) { _barks[i].El.RemoveFromHierarchy(); _barks.RemoveAt(i); }
            var lbl = L(text, "worldbark");
            lbl.pickingMode = PickingMode.Ignore;
            _markerLayer.Add(lbl);
            _barks.Add(new Bark { El = lbl, Who = t, Pos = t.position, Until = Time.unscaledTime + Mathf.Clamp(1.8f + text.Length * 0.05f, 2.2f, 5f) });
        }

        /// <summary>
        /// The Spotted caption (QW18, SR.10): one line under the guard who saw her, naming the band, the numbers and
        /// what sped it up ("Lit by the gas lamp: light 0.52 (exposed above 0.35), 9.1 m away. Running ×2."). On every
        /// difficulty; the first guard only while it shows; 4 s.
        /// </summary>
        void OnSpotted(Npc n)
        {
            if (!n || !Game.InMission) return;
            // the first sighting is the one that explains the mistake: guards who join in while it shows add nothing
            if (_captions.Count > 0) return;
            var text = n.ExplainSpotted();
            if (string.IsNullOrEmpty(text)) return;
            var lbl = L(text, "spotcaption");
            lbl.pickingMode = PickingMode.Ignore;
            _markerLayer.Add(lbl);
            _captions.Add(new Bark { El = lbl, Who = n.transform, Pos = n.transform.position, Until = Time.unscaledTime + Stealth.MeterRead.PictureTime });
            LastSpottedCaption = text;
        }

        readonly List<Bark> _captions = new List<Bark>();
        /// <summary>The last Spotted caption shown (for tests and the debrief).</summary>
        public string LastSpottedCaption;

        void TickOverlay()
        {
            float now = Time.unscaledTime;
            for (int i = _toasts.Count - 1; i >= 0; i--)
            {
                var t = _toasts[i];
                float left = t.Until - now;
                t.El.style.opacity = Mathf.Clamp01(left / 0.5f);
                if (left <= 0) { t.El.RemoveFromHierarchy(); _toasts.RemoveAt(i); }
            }
            if (_subBox.style.display == DisplayStyle.Flex && now >= _subUntil) NextSubtitle();
            if (_hintBox.style.display == DisplayStyle.Flex && now >= _hintUntil) Show(_hintBox, false);
            if (_saveInd.style.display == DisplayStyle.Flex)
            {
                _saveInd.style.opacity = 0.5f + 0.5f * Mathf.Sin(now * 6f);
                if (now >= _saveIndUntil) Show(_saveInd, false);
            }
            var cam = Game.Cam != null ? Game.Cam.Cam : null;
            for (int i = _barks.Count - 1; i >= 0; i--)
            {
                var b = _barks[i];
                if (now >= b.Until || !Game.InMission) { b.El.RemoveFromHierarchy(); _barks.RemoveAt(i); continue; }
                if (b.Who) b.Pos = b.Who.position;
                var above = b.Pos + Vector3.up * 2.4f;
                if (cam && !OnScreen(above, cam, BarkMargin)) { SetVisible(b.El, false); continue; }
                if (cam && PlaceAt(b.El, above, cam))
                {
                    ClampToPanel(b.El, 10f, 190f);
                    b.El.style.opacity = Mathf.Clamp01((b.Until - now) / 0.4f);
                }
            }
            for (int i = _captions.Count - 1; i >= 0; i--)
            {
                var b = _captions[i];
                if (now >= b.Until || !Game.InMission) { b.El.RemoveFromHierarchy(); _captions.RemoveAt(i); continue; }
                if (b.Who) b.Pos = b.Who.position;
                // hung from his feet: the label sits below him (its translate is top-anchored)
                if (cam && PlaceAt(b.El, b.Pos, cam))
                {
                    float h = b.El.resolvedStyle.height;
                    ClampToPanel(b.El, 10f, 190f + (float.IsNaN(h) ? 0f : h));
                    b.El.style.opacity = Mathf.Clamp01((b.Until - now) / 0.5f);
                }
            }
        }

        /// <summary>Keeps a world-anchored element (centred, bottom-anchored by its translate) fully on screen and above the
        /// bottom HUD band, so a speaker just off the edge is still read at the edge nearest them.</summary>
        const float BarkMargin = 250f;

        void ClampToPanel(VisualElement el, float pad, float bottomBand)
        {
            var size = _root.layout.size;
            float w = el.resolvedStyle.width, h = el.resolvedStyle.height;
            if (float.IsNaN(w) || w <= 0f || size.x <= 0f) return;
            float x0 = el.style.left.value.value, y0 = el.style.top.value.value;
            float x = Mathf.Clamp(x0, w * 0.5f + pad, Mathf.Max(w * 0.5f + pad, size.x - w * 0.5f - pad));
            float y = Mathf.Clamp(y0, h + pad, Mathf.Max(h + pad, size.y - bottomBand));
            if (x != x0) el.style.left = x;
            if (y != y0) el.style.top = y;
        }

        /// <summary>Positions an element at a world point. Returns false (and hides it) when behind the camera.</summary>
        public bool PlaceAt(VisualElement el, Vector3 world, Camera cam)
        {
            var sp = cam.WorldToScreenPoint(world);
            if (sp.z < 0) { SetVisible(el, false); return false; }
            // one projection; style writes only when the element really moved, so a still camera costs no relayout
            var pp = RuntimePanelUtils.ScreenToPanel(_root.panel, new Vector2(sp.x, Screen.height - sp.y));
            var s = el.style;
            if (Mathf.Abs(s.left.value.value - pp.x) > 0.25f || s.left.keyword != StyleKeyword.Undefined) s.left = pp.x;
            if (Mathf.Abs(s.top.value.value - pp.y) > 0.25f || s.top.keyword != StyleKeyword.Undefined) s.top = pp.y;
            SetVisible(el, true);
            return true;
        }

        /// <summary>True when a world point projects inside the screen, give or take a margin in pixels.</summary>
        public static bool OnScreen(Vector3 world, Camera cam, float margin)
        {
            var sp = cam.WorldToScreenPoint(world);
            return sp.z >= 0 && sp.x > -margin && sp.x < Screen.width + margin && sp.y > -margin && sp.y < Screen.height + margin;
        }

        static void SetVisible(VisualElement el, bool on)
        {
            var want = on ? Visibility.Visible : Visibility.Hidden;
            if (el.style.visibility.keyword != StyleKeyword.Undefined || el.style.visibility.value != want) el.style.visibility = want;
        }

        // ---------------------------------------------------------------- fades
        public void FadeOut(float seconds = 0.5f, Action then = null)
        {
            _fade.style.transitionDuration = new List<TimeValue> { new TimeValue(seconds, TimeUnit.Second) };
            _fade.style.opacity = 1f;
            if (then != null) _fade.schedule.Execute(then).StartingIn((long)(seconds * 1000) + 30);
        }

        public void FadeIn(float seconds = 0.8f)
        {
            _fade.style.transitionDuration = new List<TimeValue> { new TimeValue(seconds, TimeUnit.Second) };
            _fade.style.opacity = 0f;
        }

        public void SetFadeInstant(float a)
        {
            _fade.style.transitionDuration = new List<TimeValue> { new TimeValue(0, TimeUnit.Second) };
            _fade.style.opacity = a;
        }

        // ---------------------------------------------------------------- input routing
        void Update()
        {
            var inp = Game.Input;
            if (inp == null || _root == null) return;

            if (inp.DebugConsole.WasPressedThisFrame()) ToggleConsole();
            else if (inp.Menu.WasPressedThisFrame() && !inp.Rebinding) OnEscape();
            else if (!BlocksGameplay && Game.InMission && !Game.Mission.Ended)
            {
                if (inp.Pause.WasPressedThisFrame())
                {
                    Game.TacticalPaused = !Game.TacticalPaused;
                    Game.Audio?.Play2D(Game.TacticalPaused ? "plan_in" : "plan_out", 0.4f);
                }
            }
            // Enter / Space dismisses the mission title card (UI Toolkit key events need focus, so poll here)
            if (_modals.Count == 0 && TopScreen == "intro" && Keyboard.current != null &&
                (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame || Keyboard.current.spaceKey.wasPressedThisFrame))
                PopScreen("intro");
            // keyboard shortcuts in choice modals (1..9)
            if (_modals.Count > 0 && Keyboard.current != null)
            {
                var top = _modals[_modals.Count - 1].El;
                var buttons = top.Query<Button>().ToList();
                for (int i = 0; i < Mathf.Min(9, buttons.Count); i++)
                {
                    var k = Keyboard.current[Key.Digit1 + i];
                    if (k != null && k.wasPressedThisFrame && buttons[i].text.StartsWith((i + 1) + ".")) { SendClick(buttons[i]); break; }
                }
            }

            UnityEngine.Profiling.Profiler.BeginSample("UI.Overlay"); TickOverlay(); UnityEngine.Profiling.Profiler.EndSample();
            UnityEngine.Profiling.Profiler.BeginSample("UI.Hud"); TickHud(); UnityEngine.Profiling.Profiler.EndSample();
            UnityEngine.Profiling.Profiler.BeginSample("UI.Console"); TickConsole(); UnityEngine.Profiling.Profiler.EndSample();
        }

        static void SendClick(Button b)
        {
            using (var e = NavigationSubmitEvent.GetPooled()) { e.target = b; b.SendEvent(e); }
        }

        void OnEscape()
        {
            if (_consoleOpen) { ToggleConsole(); return; }
            if (_modals.Count > 0)
            {
                var m = _modals[_modals.Count - 1];
                m.OnBack?.Invoke();
                return;
            }
            if (_screens.Count > 0)
            {
                var s = _screens[_screens.Count - 1];
                s.OnBack?.Invoke();
                return;
            }
            if (Game.InMission && !Game.Mission.Ended)
            {
                if (Game.Player != null && Game.Player.Aiming != null) { Game.Player.CancelAim(); return; }
                ShowPauseMenu();
            }
        }
    }
}
