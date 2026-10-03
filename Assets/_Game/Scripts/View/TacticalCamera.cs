using UnityEngine;
using Vespertine.Core;

namespace Vespertine.View
{
    /// <summary>
    /// Isometric-style camera that follows whoever she controls (D114). The arrow keys, a middle-drag or (opt-in) the screen
    /// edges look ahead of them; the look-ahead eases back once they move again. Z/X rotate, the wheel zooms. Clamped to the
    /// map, on unscaled time. With nothing to follow (dev tools, cutscenes) the same inputs pan freely.
    /// </summary>
    public class TacticalCamera : MonoBehaviour
    {
        public Camera Cam;
        public Vector3 Pivot;
        public float Yaw = 45f, Pitch = 52f, Distance = 20f;
        public float MinDist = 9f, MaxDist = 46f;
        public Bounds Limits = new Bounds(Vector3.zero, Vector3.one * 1000);
        public Transform Follow;
        public bool Locked;          // cutscene / menus

        float _tYaw, _tDist;
        Vector3 _tPivot;
        float _shake;
        Vector3 _vel;
        bool _dragging;
        Vector3 _peek, _lastFollowPos;
        bool _hasLastFollow;
        public const float PeekMax = 22f;
        /// <summary>QW11: a press of Z/X shorter than this snaps to the next 45°; held longer, the camera turns smoothly.</summary>
        public const float TapTime = 0.18f, SnapStep = 45f;
        /// <summary>QW12: the camera leads her by her velocity times <see cref="LeadTime"/> (about 2 m walking, 4 m
        /// running), eased over <see cref="LeadEase"/>, at most <see cref="LeadMax"/>; none while she sneaks within
        /// <see cref="CalmRange"/> of a hostile human, where a drifting frame would hurt.</summary>
        public const float LeadTime = 0.6f, LeadEase = 0.4f, LeadMax = 5f, CalmRange = 10f;
        float _rotHeld;
        int _rotDir;
        Vector3 _lead, _leadVel;
        /// <summary>How far the view has been pushed ahead of whoever it follows.</summary>
        public Vector3 Peek => _peek;

        void Awake()
        {
            Game.Cam = this;
            Cam = GetComponent<Camera>();
            if (!Cam) Cam = gameObject.AddComponent<Camera>();
            Cam.fieldOfView = 34f;
            Cam.nearClipPlane = 0.5f;
            Cam.farClipPlane = 260f;
            Cam.clearFlags = CameraClearFlags.SolidColor;
            Cam.backgroundColor = Util.Hex("#05060a");
            _tYaw = Yaw; _tDist = Distance; _tPivot = Pivot;
        }

        public void SetLimits(Bounds b) { Limits = b; }

        public void SnapTo(Vector3 p, float? yaw = null)
        {
            _tPivot = Pivot = Clamp(p);
            if (yaw.HasValue) _tYaw = Yaw = yaw.Value;
            Apply();
        }

        /// <summary>Draws the eye to <paramref name="p"/> (a mission's "look here"): while following, as a look-ahead that
        /// eases back once she moves on.</summary>
        public void FocusOn(Vector3 p)
        {
            if (Follow) { var d = Clamp(p) - Follow.position; d.y = 0f; _peek = d; }
            else _tPivot = Clamp(p);
        }

        /// <summary>Follow <paramref name="t"/> (Ilse or a thrall), dropping any look-ahead.</summary>
        public void FollowTarget(Transform t)
        {
            Follow = t;
            _peek = Vector3.zero;
            _lead = _leadVel = Vector3.zero;
            _hasLastFollow = false;
        }

        /// <summary>Home: back onto whoever is followed, look-ahead dropped.</summary>
        public void Recentre()
        {
            if (!Follow && Game.Player != null) Follow = Game.Player.SelectedThrall ? Game.Player.SelectedThrall.transform : Game.Player.transform;
            _peek = Vector3.zero;
        }

        /// <summary>Eases a look-ahead offset back toward the followed character once they move (pure, for tests).</summary>
        public static Vector3 EasePeek(Vector3 peek, float followSpeed, bool looking, float dt)
        {
            if (looking || followSpeed < 0.5f) return peek;
            return Vector3.Lerp(peek, Vector3.zero, 1f - Mathf.Exp(-2.5f * dt));
        }
        /// <summary>The next multiple of <see cref="SnapStep"/> past <paramref name="yaw"/> in direction
        /// <paramref name="dir"/> (pure, for tests).</summary>
        public static float SnapYaw(float yaw, int dir)
        {
            float k = yaw / SnapStep;
            return (dir > 0 ? Mathf.Floor(k + 0.01f) + 1f : Mathf.Ceil(k - 0.01f) - 1f) * SnapStep;
        }

        /// <summary>One frame of Z/X (QW11): a press released within <see cref="TapTime"/> snaps the target yaw to the next
        /// 45° line; held longer, it turns at <paramref name="speed"/> °/s and stops where it is let go (pure, for tests).</summary>
        public static float RotateStep(float target, ref int dir, ref float held, float rot, float speed, float dt)
        {
            int d = rot > 0.5f ? 1 : rot < -0.5f ? -1 : 0;
            if (d != 0)
            {
                if (d != dir) { dir = d; held = 0f; }
                held += dt;
                return held > TapTime ? target + rot * speed * dt : target;
            }
            if (dir != 0 && held <= TapTime) target = SnapYaw(target, dir);
            dir = 0; held = 0f;
            return target;
        }

        /// <summary>Where the velocity lead wants the frame, relative to her (pure, for tests).</summary>
        public static Vector3 LeadFor(Vector3 velocity, bool calm)
        {
            velocity.y = 0f;
            if (calm) return Vector3.zero;
            return Vector3.ClampMagnitude(velocity * LeadTime, LeadMax);
        }

        /// <summary>She sneaks with a hostile human near: hold the frame still.</summary>
        static bool Calm(Transform followed)
        {
            var p = Game.Player;
            if (p == null || followed != p.transform || !p.Sneaking || Game.AI == null) return false;
            foreach (var n in Game.AI.Npcs)
                if (n && n.IsAlive && !n.Friendly && !n.IsThrall && !n.Incapacitated && Util.FlatDistance(n.transform.position, p.Feet) < CalmRange) return true;
            return false;
        }

        public void Shake(float amount) { if (Game.Settings == null || Game.Settings.ScreenShake) _shake = Mathf.Max(_shake, amount); }

        Vector3 Clamp(Vector3 p)
        {
            p.x = Mathf.Clamp(p.x, Limits.min.x, Limits.max.x);
            p.z = Mathf.Clamp(p.z, Limits.min.z, Limits.max.z);
            p.y = 0;
            return p;
        }

        public Vector3 Forward => Quaternion.Euler(0, Yaw, 0) * Vector3.forward;
        public Vector3 Right => Quaternion.Euler(0, Yaw, 0) * Vector3.right;

        void LateUpdate()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            var input = Game.Input;
            if (!Locked && input != null && input.Cam.enabled)
            {
                var s = Game.Settings;
                float panMul = (s != null ? s.CameraPanSpeed : 1f) * Mathf.Lerp(0.7f, 1.6f, Mathf.InverseLerp(MinDist, MaxDist, Distance));
                Vector2 pan = input.Pan.ReadValue<Vector2>();
                bool overUi = Game.UI != null && Game.UI.PointerOverUI;
                if (s == null || s.EdgePan)
                {
                    var m = input.MousePos;
                    if (Application.isFocused && !overUi && m.x >= 0 && m.y >= 0 && m.x <= Screen.width && m.y <= Screen.height)
                    {
                        const float edge = 6f;
                        if (m.x < edge) pan.x -= 1; else if (m.x > Screen.width - edge) pan.x += 1;
                        if (m.y < edge) pan.y -= 1; else if (m.y > Screen.height - edge) pan.y += 1;
                    }
                }
                bool looking = false;
                if (pan.sqrMagnitude > 0.01f)
                {
                    var d = (Right * pan.x + Forward * pan.y) * 18f * panMul * dt;
                    if (Follow) _peek += d; else _tPivot += d;
                    looking = true;
                }
                // MMB drag
                if (input.DragPan.WasPressedThisFrame() && !overUi) _dragging = true;
                if (input.DragPan.WasReleasedThisFrame()) _dragging = false;
                if (_dragging)
                {
                    var md = input.Delta.ReadValue<Vector2>();
                    float k = Distance * 0.0016f;
                    var d = -(Right * md.x + Forward * md.y) * k;
                    if (Follow) _peek += d; else _tPivot += d;
                    looking = true;
                }
                if (Follow)
                {
                    var fp = Follow.position;
                    var step = _hasLastFollow ? new Vector3(fp.x - _lastFollowPos.x, 0f, fp.z - _lastFollowPos.z) : Vector3.zero;
                    float spd = dt > 0f ? step.magnitude / dt : 0f;
                    _lastFollowPos = fp; _hasLastFollow = true;
                    // the lead reads game-time velocity, holds while paused, and drops on a teleport
                    float gdt = Time.deltaTime;
                    if (step.sqrMagnitude > 9f) { _lead = _leadVel = Vector3.zero; }
                    else if (gdt > 0f) _lead = Vector3.SmoothDamp(_lead, LeadFor(step / gdt, Calm(Follow)), ref _leadVel, LeadEase, Mathf.Infinity, dt);
                    _peek = EasePeek(_peek, spd, looking, dt);
                    if (looking && _peek.sqrMagnitude > PeekMax * PeekMax) _peek = _peek.normalized * PeekMax;
                }
                float rot = input.Rotate.ReadValue<float>();
                if (s != null && s.InvertRotate) rot = -rot;
                _tYaw = RotateStep(_tYaw, ref _rotDir, ref _rotHeld, rot, 110f * (s != null ? s.CameraRotateSpeed : 1f), dt);
                float z = input.Zoom.ReadValue<float>();
                if (Mathf.Abs(z) > 0.01f && !overUi) _tDist = Mathf.Clamp(_tDist * (1f - Mathf.Sign(z) * 0.12f), MinDist, MaxDist);
            }
            if (Follow) _tPivot = Follow.position + _peek + _lead;
            _tPivot = Clamp(_tPivot);
            Pivot = Vector3.SmoothDamp(Pivot, _tPivot, ref _vel, 0.12f, Mathf.Infinity, dt);
            Yaw = Mathf.LerpAngle(Yaw, _tYaw, 1f - Mathf.Exp(-14f * dt));
            Distance = Mathf.Lerp(Distance, _tDist, 1f - Mathf.Exp(-10f * dt));
            Apply();
            if (_shake > 0)
            {
                transform.position += Random.insideUnitSphere * _shake * 0.35f;
                _shake = Mathf.Max(0, _shake - dt * 2.5f);
            }
        }

        void Apply()
        {
            float pitch = Mathf.Lerp(Pitch - 8f, Pitch + 6f, Mathf.InverseLerp(MinDist, MaxDist, Distance));
            var rot = Quaternion.Euler(pitch, Yaw, 0);
            transform.position = Pivot + Vector3.up * 1f - rot * Vector3.forward * Distance;
            transform.rotation = rot;
        }

        public Ray MouseRay() => Cam.ScreenPointToRay(Game.Input != null ? (Vector3)Game.Input.MousePos : Vector3.zero);

        public bool WorldToScreen(Vector3 w, out Vector2 screen)
        {
            var p = Cam.WorldToScreenPoint(w);
            screen = new Vector2(p.x, p.y);
            return p.z > 0;
        }
    }
}
