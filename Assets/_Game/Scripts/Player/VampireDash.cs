using UnityEngine;
using Vespertine.AI;
using Vespertine.Controls;
using Vespertine.Core;
using Vespertine.Level;
using Vespertine.Stealth;
using Vespertine.Visual;

namespace Vespertine.Player
{
    /// <summary>
    /// Shadow Dash (D155, GAMEPLAY_REDESIGN §11), baseline from M01: a 0.2 s burst of 6 m along the move keys (facing,
    /// if she stands), stepped through the navmesh so water, walls, fences and thresholds still hold. Silent. Unseen in
    /// the dark; in light every guard who has her in a band takes +0.3 on his meter. Two charges (three with Umbral Step),
    /// each refilling in 5 s only while she is dark: light is the cooldown. Dashing into a climb in the dark carries her up
    /// one storey (the Rise); Between Bars takes her through a barred gap.
    /// </summary>
    public partial class Vampire
    {
        int _dashCharges = -1;
        float _dashRefill, _dashLeft, _dashMoved, _umbralT;
        bool _dashBlurred;
        Vector3 _dashDir;

        /// <summary>Dev / sweep: dash on the next frame as if Q were pressed.</summary>
        public bool DebugDash;

        /// <summary>Dev sweep: every charge back.</summary>
        public void DevRefillDash() { _dashCharges = DashMax; _dashRefill = 0f; }

        public int DashMax => MoveMath.DashCharges + (Has("shade.umbral") ? 1 : 0);
        public int DashCharges => _dashCharges < 0 ? DashMax : _dashCharges;
        /// <summary>Progress toward the next charge, 0..1.</summary>
        public float DashRefill => _dashRefill / MoveMath.DashRechargeTime;
        public bool Dashing => _dashLeft > 0f;
        /// <summary>The same threshold the disc, the far band and regen use.</summary>
        bool DashDark => SightLight < DetectionMath.ExposedAt;
        /// <summary>No one sees her: mid-dash in the dark, or the Umbral Step second after one.</summary>
        public bool DashVeiled => (Dashing || _umbralT > 0f) && DashDark;

        /// <summary>Charges refill and the Umbral second runs out, whatever else she is doing.</summary>
        void TickDashClock(float dt)
        {
            if (_dashCharges < 0) _dashCharges = DashMax;
            _dashRefill = MoveMath.DashRecharge(ref _dashCharges, DashMax, _dashRefill, dt, DashDark);
            if (_umbralT > 0f && !Dashing) _umbralT -= dt;
        }

        bool DashPressed()
        {
            if (DebugDash) { DebugDash = false; return true; }
            return Game.Input != null && Game.Input.Dash.WasPressedThisFrame() && (Game.UI == null || !Game.UI.BlocksGameplay) && SelectedThrall == null;
        }

        bool TryDash()
        {
            if (!Agent || !Agent.enabled || !Agent.isOnNavMesh || !Agent.updatePosition) return false;
            var dir = _moveIn.sqrMagnitude > 0.0004f ? _moveIn : transform.forward;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return false;
            dir.Normalize();
            bool censer = false;
            if (Game.AI != null)
                foreach (var n in Game.AI.Living())
                    if (n.CarriesCenser && Vector3.Distance(n.transform.position, Feet) < Npc.CenserRadius) { censer = true; break; }
            var why = MoveMath.DashProblem(DashCharges, Feeding != null || _link != null, Carrying != null, InMist, censer);
            if (why != null) { Say(why); return false; }

            // her own hand overrides anything queued, as the move keys do
            _pending = null;
            _useTarget = null;
            if (_channel > 0f) { _channel = 0f; _channelDone = null; }
            Stop();
            if (Concealed) LeaveHideSpot();
            _dashCharges = DashCharges - 1;
            _dashDir = _moveDir = dir;
            _dashLeft = MoveMath.DashDistance;
            _dashMoved = 0f;
            _umbralT = 0f;
            _dashBlurred = false;
            Rushing = false;
            transform.rotation = Quaternion.LookRotation(dir);
            Fx.Smoke(Feet + Vector3.up, new Color(0.05f, 0.05f, 0.1f, 0.8f), 1.2f, 0.5f);
            Game.Audio?.PlayAt("swish", Feet, 0.25f, 1.3f);
            return true;
        }

        /// <summary>One frame of the burst, in half-metre steps so no door edge or corner is skipped.</summary>
        void StepDash(float dt)
        {
            if (!Agent || !Agent.enabled || !Agent.isOnNavMesh || !Agent.updatePosition) { EndDash(); return; }
            // seen as a blur the moment she is in light
            if (!DashDark && !_dashBlurred) { _dashBlurred = true; Blur(); }

            // the Rise, and Between Bars: a climb or a barred gap ahead takes the dash over
            var link = FindTraverse(_dashDir, out var to, true);
            if (link != null && (link.Kind == LinkKind.Mist || MoveMath.DashRises(Feet.y, to.y, DashDark)))
            {
                EndDash();
                StartTraverse(link, to);
                return;
            }

            float want = Mathf.Min(_dashLeft, MoveMath.DashDistance / MoveMath.DashTime * dt);
            int steps = Mathf.Max(1, Mathf.CeilToInt(want / 0.5f));
            float moved = 0f;
            for (int i = 0; i < steps; i++)
            {
                var before = Agent.nextPosition;
                Agent.Move(Door.ClampStep(before, _dashDir * (want / steps)));
                var m = Agent.nextPosition - before;
                m.y = 0f;
                moved += m.magnitude;
            }
            _dashLeft -= want;
            _dashMoved += moved;
            _actualSpeed = dt > 0f ? moved / dt : 0f;
            // a wall, a door, the water's edge: the burst dies there
            if (moved < want * 0.25f || _dashLeft <= 0.001f) EndDash();
        }

        void EndDash()
        {
            // a dash straight into a wall goes nowhere and costs nothing
            if (_dashMoved < 0.3f) _dashCharges = Mathf.Min(DashMax, DashCharges + 1);
            _dashLeft = 0f;
            _actualSpeed = Mathf.Min(_actualSpeed, RushSpeed * SpeedMul);
            if (DashDark && Has("shade.umbral")) _umbralT = MoveMath.UmbralTime;
        }

        /// <summary>In light the dash is a blur: +0.3 on the meter of every guard who has her in a band with line of sight.</summary>
        void Blur()
        {
            if (Game.AI == null) return;
            var feet = Feet;
            foreach (var n in Game.AI.Living())
            {
                if (n.Friendly || n.SeenBand(feet, SightLight) == DetectionMath.Band.None) continue;
                if (n.Overlooks(this, feet, Util.FlatDistance(feet, n.transform.position))) continue;
                n.Glimpse(feet, MoveMath.DashBlur);
            }
        }

        /// <summary>Between Bars: the mist passage is through bars (a vent stays mist-only).</summary>
        static bool BarredGap(NavLink l)
        {
            var lv = Game.Level;
            if (lv == null || lv.Grid == null) return false;
            var c = lv.CellOf((l.Start + l.End) * 0.5f);
            return lv.Grid.Kind(c.x, c.y) == TileKind.Bars;
        }
    }
}
