using UnityEngine;
using Vespertine.AI;
using Vespertine.Controls;
using Vespertine.Core;
using Vespertine.Level;

namespace Vespertine.Player
{
    /// <summary>
    /// Direct control (D114). The move keys steer whoever she controls (Ilse, or the thrall whose portrait was clicked),
    /// relative to the camera; Shift runs. Pushing into a climbable wall, a pipe, a roof edge or a leap gap for a moment
    /// takes it (the traverse key takes it at once). Feeding, carrying, using and abilities still walk her into range
    /// on their own, and any push on the keys takes the walk back.
    /// </summary>
    public partial class Vampire
    {
        /// <summary>Dev / sweep hook: when set, stands in for the move keys (a world direction, 0..1).</summary>
        public Vector3? DebugMove;
        public bool DebugRun, DebugSneak;

        Vector3 _moveIn, _moveDir = Vector3.forward;
        bool _runIn, _sneakIn;
        bool _frameLocked;
        Vector2 _frameInput;
        float _frameYaw;

        /// <summary>Holding the sneak key (D119): slow, and silent on dry stone. Running wins if both are held.</summary>
        public bool Sneaking => _sneakIn && !Rushing;
        public Gait Gait => Rushing ? Gait.Run : Sneaking ? Gait.Sneak : Gait.Walk;
        float _directSpeed, _actualSpeed;
        float _pushT;
        NavLink _pushLink;
        float _travScanT;

        /// <summary>A climb, drop, leap or mist passage within reach in the direction she faces or pushes (HUD prompt).</summary>
        public NavLink TraverseLink { get; private set; }
        Vector3 _travTo;

        /// <summary>Speed she is actually covering under direct control (walls stop her even with a key held).</summary>
        public float DirectSpeed => _actualSpeed;
        /// <summary>A shut door held her step this frame (the Movement Test counts door stops apart from wall-sticks).</summary>
        public bool DoorHeld { get; private set; }
        public bool DirectMoving => _actualSpeed > 0.3f;

        /// <summary>Where the move keys point this frame (world, length 0..1). The toe looks ahead along it (SR.8), so it
        /// answers from a standstill the moment a key goes down.</summary>
        public Vector3 MoveIntent => _moveIn;
        /// <summary>The speed the keys ask for (run, sneak or glide), whether or not she has reached it yet.</summary>
        public float IntentSpeed => (_runIn && Carrying == null && !InMist ? RushSpeed : _sneakIn ? SneakSpeed : GlideSpeed) * SpeedMul * Mathf.Clamp01(_moveIn.magnitude);

        static readonly LinkKind[] TraversePriority = { LinkKind.Mist, LinkKind.Leap, LinkKind.Ladder, LinkKind.Climb, LinkKind.ClimbAny, LinkKind.Jump };

        /// <summary>What the move keys ask for this frame. Goes to Ilse, or to the thrall she controls (Ilse then holds).</summary>
        void ReadMoveInput()
        {
            Vector3 dir;
            bool run;
            _sneakIn = DebugSneak || (Game.Input != null && Game.Input.Sneak.IsPressed() && (Game.UI == null || !Game.UI.BlocksGameplay) && SelectedThrall == null);
            if (DebugMove.HasValue) { dir = DebugMove.Value; run = DebugRun; }
            else
            {
                var inp = Game.Input;
                var v = inp != null && (Game.UI == null || !Game.UI.BlocksGameplay) ? inp.Move.ReadValue<Vector2>() : Vector2.zero;
                dir = MoveMath.CameraRelative(v, MoveMath.FrameYaw(ref _frameLocked, ref _frameInput, ref _frameYaw, v, Game.Cam ? Game.Cam.Yaw : 45f));
                run = Rushy;
            }
            var t = SelectedThrall;
            if (t)
            {
                t.DirectMove = dir;
                t.DirectRun = run;
                _moveIn = Vector3.zero;
                _runIn = false;
            }
            else { _moveIn = dir; _runIn = run; }
        }

        void TickDirect(float dt)
        {
            DoorHeld = false;
            if (DashPressed() && Feeding == null && _link == null) TryDash();
            if (Dashing) { StepDash(dt); return; }
            var dir = _moveIn;
            bool wants = dir.sqrMagnitude > 0.0004f && Feeding == null && _link == null;
            if (wants)
            {
                // her own hand overrides anything queued: an approach, a use in progress, a channel, a hiding place
                if (_pending != null || _useTarget != null || _channel > 0f || (Agent && Agent.enabled && Agent.isOnNavMesh && Agent.hasPath))
                {
                    _pending = null;
                    _useTarget = null;
                    if (_channel > 0f) { _channel = 0f; _channelDone = null; }
                    Stop();
                }
                if (Concealed) LeaveHideSpot();
                _moveDir = dir.normalized;
            }
            bool run = wants && _runIn && Carrying == null && !InMist;
            float target = wants ? (run ? RushSpeed : _sneakIn ? SneakSpeed : GlideSpeed) * SpeedMul * Mathf.Clamp01(dir.magnitude) : 0f;
            _directSpeed = MoveMath.Ease(_directSpeed, target, dt);

            if (_directSpeed > 0.01f && Agent && Agent.enabled && Agent.isOnNavMesh && Agent.updatePosition && !Agent.hasPath)
            {
                Rushing = run;
                var before = Agent.nextPosition;
                var step = _moveDir * (_directSpeed * dt) + Separation();
                // a shut door stops her (she opens it with the interact key); mist slips under it
                if (!InMist)
                {
                    var free = step;
                    step = Door.ClampStep(Feet, step);
                    DoorHeld = step.sqrMagnitude < free.sqrMagnitude * 0.25f;
                }
                Agent.Move(step);
                var moved = Agent.nextPosition - before;
                moved.y = 0f;
                _actualSpeed = Mathf.Lerp(_actualSpeed, dt > 0f ? moved.magnitude / dt : 0f, 1f - Mathf.Exp(-18f * dt));
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(_moveDir), 900f * dt);
            }
            else
            {
                _actualSpeed = Mathf.Lerp(_actualSpeed, 0f, 1f - Mathf.Exp(-18f * dt));
                if (_actualSpeed < 0.05f) _actualSpeed = 0f;
                if (!wants && !(Agent && Agent.enabled && Agent.hasPath)) Rushing = false;
            }

            // climbing, drops and leaps: push into one for a moment, or press the traverse key
            _travScanT -= dt;
            if (wants || _travScanT <= 0f)
            {
                _travScanT = 0.1f;
                TraverseLink = Busy ? null : FindTraverse(wants ? dir : transform.forward, out _travTo);
            }
            bool key = Game.Input != null && Game.Input.Traverse.WasPressedThisFrame() && (Game.UI == null || !Game.UI.BlocksGameplay) && SelectedThrall == null;
            if (TraverseLink != null && key) { StartTraverse(TraverseLink, _travTo); return; }
            if (wants && TraverseLink != null)
            {
                if (_pushLink != TraverseLink) { _pushLink = TraverseLink; _pushT = 0f; }
                _pushT += dt;
                // stepping off a roof should be meant: a drop waits a little longer than a climb
                if (_pushT >= (TraverseLink.Kind == LinkKind.Jump ? 0.3f : 0.18f)) StartTraverse(TraverseLink, _travTo);
            }
            else { _pushLink = null; _pushT = 0f; }
        }

        /// <summary>Living humans are not walked through: she slides round them.</summary>
        Vector3 Separation()
        {
            var ai = Game.AI;
            if (ai == null) return Vector3.zero;
            var feet = Feet;
            var push = Vector3.zero;
            foreach (var n in ai.Living())
            {
                if (n.Carried || n == Feeding) continue;
                var d = feet - n.transform.position;
                if (Mathf.Abs(d.y) > 1.2f) continue;
                d.y = 0f;
                float m = d.magnitude;
                if (m > 0.62f || m < 0.001f) continue;
                push += d / m * (0.62f - m) * 0.5f;
            }
            return push;
        }

        /// <summary>The best link she can take from here heading in <paramref name="dir"/>: a leap before a climb before a drop.</summary>
        /// <param name="dash">Mid-dash: only a climb (the Rise) or, with Between Bars, a barred gap.</param>
        NavLink FindTraverse(Vector3 dir, out Vector3 to, bool dash = false)
        {
            to = Vector3.zero;
            var nav = Game.Level != null ? Game.Level.Nav : null;
            if (nav == null || !Agent || !Agent.enabled || Feeding != null || _link != null) return null;
            int mask = AreaMask();
            if (dash && Has("shade.dash_bars")) mask |= 1 << NavAreas.Mist;
            var feet = Feet;
            NavLink best = null;
            int bestRank = int.MaxValue;
            float bestDist = float.MaxValue;
            foreach (var l in nav.Links)
            {
                if (l.Agent != NavAreas.VampireAgent || (mask & (1 << NavBuilder.AreaFor(l.Kind))) == 0) continue;
                if (dash && !(l.Kind == LinkKind.Climb || l.Kind == LinkKind.ClimbAny || l.Kind == LinkKind.Mist && BarredGap(l))) continue;
                int rank = System.Array.IndexOf(TraversePriority, l.Kind);
                if (rank > bestRank) continue;
                // links laid only every other cell (leaps) are reached from further along the edge
                float reach = l.Kind == LinkKind.Leap ? 2.2f : 1.1f;
                for (int pass = 0; pass < (l.Bidirectional ? 2 : 1); pass++)
                {
                    var s = pass == 0 ? l.Start : l.End;
                    var e = pass == 0 ? l.End : l.Start;
                    if (!MoveMath.PushesInto(feet, dir, s, e, l.Width, out var f, out var t, reach)) continue;
                    float d = (f - feet).sqrMagnitude;
                    if (rank < bestRank || d < bestDist) { best = l; bestRank = rank; bestDist = d; to = t; }
                }
            }
            return best;
        }

        /// <summary>Takes a link from where she stands (outside the agent's own path following).</summary>
        void StartTraverse(NavLink link, Vector3 to)
        {
            _pushLink = null; _pushT = 0f;
            TraverseLink = null;
            if (Concealed) LeaveHideSpot();
            Stop();
            _pending = null;
            _directSpeed = _actualSpeed = 0f;
            if (Agent) Agent.updatePosition = false;
            SetupLink(link, transform.position, to);
        }
    }
}
