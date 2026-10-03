using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Vespertine.Core;

namespace Vespertine.Controls
{
    /// <summary>All input actions, defined in code. Binding overrides persist in settings.</summary>
    public class GameInput
    {
        public readonly InputActionAsset Asset;
        public readonly InputActionMap Gameplay, Cam, UI;

        // gameplay
        public InputAction Click, Cancel, Point, Move, Run, Sneak, Traverse, Dash, Sip, Drain, Interact, Carry, ShowCones, BloodSense,
            CycleThrall, ThrallFollow, Center, Pause, Menu, QuickSave, QuickLoad, DebugConsole, Inspect;
        public readonly InputAction[] Abilities = new InputAction[6];
        // camera
        public InputAction Pan, Rotate, Zoom, DragPan, Delta;

        /// <summary>Human-readable names for the rebinding screen, in display order.</summary>
        public readonly List<(string label, InputAction action, int binding)> Rebindable = new List<(string, InputAction, int)>();

        public GameInput()
        {
            Asset = ScriptableObject.CreateInstance<InputActionAsset>();
            Asset.name = "VespertineInput";
            Gameplay = Asset.AddActionMap("Gameplay");
            Cam = Asset.AddActionMap("Camera");
            UI = Asset.AddActionMap("UI");

            Click = Btn(Gameplay, "Click", "<Mouse>/leftButton", null);
            Cancel = Btn(Gameplay, "Cancel", "<Mouse>/rightButton", null);
            Point = Gameplay.AddAction("Point", InputActionType.PassThrough, "<Mouse>/position");
            Point.expectedControlType = "Vector2";
            Inspect = Btn(Gameplay, "Inspect", "<Mouse>/middleButton", null);
            Move = Gameplay.AddAction("Move", InputActionType.Value);
            Move.expectedControlType = "Vector2";
            Move.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s").With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            Move.AddBinding("<Gamepad>/leftStick");
            Rebindable.Add(("Move forward", Move, 1)); Rebindable.Add(("Move back", Move, 2)); Rebindable.Add(("Move left", Move, 3)); Rebindable.Add(("Move right", Move, 4));
            Run = Btn(Gameplay, "Run", "<Keyboard>/leftShift", "Run (hold)");
            Sneak = Btn(Gameplay, "Sneak", "<Keyboard>/leftCtrl", "Sneak (hold)");
            Traverse = Btn(Gameplay, "Traverse", "<Keyboard>/space", "Climb / drop / leap");
            Dash = Btn(Gameplay, "Dash", "<Keyboard>/q", "Shadow Dash");
            Dash.AddBinding("<Gamepad>/buttonEast");
            Sip = Btn(Gameplay, "Sip", "<Keyboard>/f", "Feed: Sip");
            Drain = Btn(Gameplay, "Drain", "<Keyboard>/r", "Feed: Drain");
            Interact = Btn(Gameplay, "Interact", "<Keyboard>/e", "Interact / open door");
            Carry = Btn(Gameplay, "Carry", "<Keyboard>/c", "Carry / Drop body");
            for (int i = 0; i < 6; i++) Abilities[i] = Btn(Gameplay, "Ability" + (i + 1), "<Keyboard>/" + (i + 1), "Ability " + (i + 1));
            ShowCones = Btn(Gameplay, "ShowCones", "<Keyboard>/leftAlt", "Show all vision cones (hold)");
            BloodSense = Btn(Gameplay, "BloodSense", "<Keyboard>/v", "Blood Sense (hold)");
            CycleThrall = Btn(Gameplay, "CycleThrall", "<Keyboard>/t", "Take control: next thrall / Ilse");
            ThrallFollow = Btn(Gameplay, "ThrallFollow", "<Keyboard>/h", "Thralls: follow Ilse / hold");
            Center = Btn(Gameplay, "Center", "<Keyboard>/home", "Re-centre camera");
            Pause = Btn(Gameplay, "Pause", "<Keyboard>/p", "Pause");
            Menu = Btn(UI, "Menu", "<Keyboard>/escape", null);
            QuickSave = Btn(Gameplay, "QuickSave", "<Keyboard>/f5", "Quick save");
            QuickLoad = Btn(Gameplay, "QuickLoad", "<Keyboard>/f9", "Quick load");
            DebugConsole = Btn(UI, "Console", "<Keyboard>/backquote", null);

            Pan = Cam.AddAction("Pan", InputActionType.Value);
            Pan.expectedControlType = "Vector2";
            Pan.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow").With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            Rebindable.Add(("Look ahead: up", Pan, 1)); Rebindable.Add(("Look ahead: down", Pan, 2)); Rebindable.Add(("Look ahead: left", Pan, 3)); Rebindable.Add(("Look ahead: right", Pan, 4));
            Rotate = Cam.AddAction("Rotate", InputActionType.Value);
            Rotate.expectedControlType = "Axis";
            Rotate.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/z").With("Positive", "<Keyboard>/x");
            Rebindable.Add(("Rotate camera left", Rotate, 1)); Rebindable.Add(("Rotate camera right", Rotate, 2));
            Zoom = Cam.AddAction("Zoom", InputActionType.PassThrough, "<Mouse>/scroll/y");
            Zoom.expectedControlType = "Axis";
            DragPan = Btn(Cam, "DragPan", "<Mouse>/middleButton", null);
            Delta = Cam.AddAction("Delta", InputActionType.PassThrough, "<Mouse>/delta");
            Delta.expectedControlType = "Vector2";

            LoadOverrides();
            Asset.Enable();
        }

        InputAction Btn(InputActionMap map, string name, string path, string label)
        {
            var a = map.AddAction(name, InputActionType.Button, path);
            if (label != null) Rebindable.Add((label, a, 0));
            return a;
        }

        public void SetGameplayEnabled(bool on)
        {
            if (on) { Gameplay.Enable(); Cam.Enable(); }
            else { Gameplay.Disable(); Cam.Disable(); }
        }

        public Vector2 MousePos => Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;

        public static string Display(InputAction a, int binding)
        {
            if (a == null || binding < 0 || binding >= a.bindings.Count) return "?";
            return a.GetBindingDisplayString(binding, InputBinding.DisplayStringOptions.DontIncludeInteractions);
        }

        /// <summary>Short key label for the primary binding of an action (HUD prompts).</summary>
        public static string Key(InputAction a)
        {
            if (a == null) return "";
            // the HUD asks for a dozen labels every frame; the display string is only rebuilt when the binding changes
            var path = a.bindings.Count > 0 ? a.bindings[0].effectivePath : null;
            if (_keyCache.TryGetValue(a, out var c) && c.Path == path) return c.Label;
            var label = Display(a, 0).ToUpperInvariant();
            _keyCache[a] = (path, label);
            return label;
        }
        static readonly System.Collections.Generic.Dictionary<InputAction, (string Path, string Label)> _keyCache = new System.Collections.Generic.Dictionary<InputAction, (string, string)>();

        InputActionRebindingExtensions.RebindingOperation _op;
        public bool Rebinding => _op != null;

        public void StartRebind(InputAction a, int binding, System.Action done)
        {
            _op?.Dispose();
            bool wasEnabled = a.enabled;
            string oldPath = a.bindings[binding].effectivePath;
            a.Disable();
            _op = a.PerformInteractiveRebinding(binding)
                .WithCancelingThrough("<Keyboard>/escape")
                .WithControlsExcluding("<Mouse>/position").WithControlsExcluding("<Mouse>/delta")
                .OnMatchWaitForAnother(0.08f)
                .OnComplete(op => { SwapConflict(a, binding, oldPath); Finish(a, wasEnabled); done?.Invoke(); })
                .OnCancel(op => { Finish(a, wasEnabled); done?.Invoke(); })
                .Start();
        }

        /// <summary>A key can only do one thing: whatever else was bound to the new key takes the old one.</summary>
        void SwapConflict(InputAction a, int binding, string oldPath)
        {
            string path = a.bindings[binding].effectivePath;
            if (string.IsNullOrEmpty(path) || path == oldPath) return;
            foreach (var (label, other, idx) in Rebindable)
            {
                if (other == a && idx == binding) continue;
                if (!string.Equals(other.bindings[idx].effectivePath, path, System.StringComparison.OrdinalIgnoreCase)) continue;
                other.ApplyBindingOverride(idx, oldPath);
                Game.UI?.Toast($"{label} moved to {other.GetBindingDisplayString(idx)}");
            }
        }

        void Finish(InputAction a, bool reenable)
        {
            _op?.Dispose();
            _op = null;
            if (reenable) a.Enable();
            SaveOverrides();
        }

        public void ResetBindings()
        {
            foreach (var m in Asset.actionMaps) m.RemoveAllBindingOverrides();
            SaveOverrides();
        }

        void SaveOverrides()
        {
            if (Game.Settings == null) return;
            Game.Settings.BindingOverrides = Asset.SaveBindingOverridesAsJson();
            Game.Settings.Save();
        }

        void LoadOverrides()
        {
            var json = Game.Settings?.BindingOverrides;
            if (string.IsNullOrEmpty(json)) return;
            try { Asset.LoadBindingOverridesFromJson(json); }
            catch (System.Exception e) { Debug.LogWarning("Binding overrides invalid: " + e.Message); }
        }
    }
}
