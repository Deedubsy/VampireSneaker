using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Vespertine.Core;
using Vespertine.Level;
using Vespertine.Visual;

namespace Vespertine.Stealth
{
    public enum LightKind { GasLamp, WallLamp, Lantern, Brazier, Candle, Chandelier, Sunstone, Window, Moon, Holy, Fire, Searchlight, Sunbeam }

    /// <summary>A gameplay light: contributes to the light field and drives a matching Unity light.</summary>
    public class GameLight : Entity
    {
        public LightKind Kind;
        public float Radius = 7f;
        public float Intensity = 1f;
        public bool On = true;
        public bool Caged;          // cannot be snuffed by hand
        public bool Burns;          // sunstone / holy: harms the vampire inside its radius
        public bool Snuffable = true;
        public string LightGroup;
        public bool GasCut;         // its gas main is shut: dark until someone reopens the valve
        public float Height = 3.2f;
        public Color Color = Mats.Pal.Ember;
        public bool Portable;       // carried lantern (owned by an NPC)
        public float SnuffedAt = -999f;
        public bool WasSnuffedByPlayer;
        /// <summary>Smashed by a thrall (sunstone): dark for good; nothing relights it.</summary>
        public bool Broken;

        Light _light;
        LightShadows _shadowMode;
        public bool WantsShadow => _light != null && _shadowMode != LightShadows.None && On;
        /// <summary>The light whose shadow the budget hands out: a point light's pool spot when it has one.</summary>
        Light Shadowed => _pool ? _pool : _light;
        public void AllowShadow(bool on) { if (Shadowed != null) Shadowed.shadows = on ? _shadowMode : LightShadows.None; }
        Renderer _glass;
        float _flickerSeed;
        float _baseUnityIntensity;
        GameObject _visual;
        GameObject _burnRing;
        bool _burnRingBuilt;

        public Vector3 SourcePos => transform.position + Vector3.up * Height;

        /// <summary>A light's name in a sentence ("Lit by the gas lamp").</summary>
        public static string KindName(LightKind k)
        {
            switch (k)
            {
                case LightKind.GasLamp: return "gas lamp";
                case LightKind.WallLamp: return "wall lamp";
                case LightKind.Lantern: return "lantern";
                case LightKind.Brazier: return "brazier";
                case LightKind.Candle: return "candles";
                case LightKind.Chandelier: return "chandelier";
                case LightKind.Sunstone: return "sunstone";
                case LightKind.Window: return "window";
                case LightKind.Moon: return "moonlight";
                case LightKind.Holy: return "holy light";
                case LightKind.Fire: return "fire";
                case LightKind.Searchlight: return "searchlight";
                case LightKind.Sunbeam: return "sunbeam";
            }
            return "light";
        }

        public static LightKind ParseKind(string s)
        {
            switch (s)
            {
                case "gaslamp": case "lamp": return LightKind.GasLamp;
                case "walllamp": case "sconce": return LightKind.WallLamp;
                case "lantern": return LightKind.Lantern;
                case "brazier": return LightKind.Brazier;
                case "candle": case "candles": return LightKind.Candle;
                case "chandelier": return LightKind.Chandelier;
                case "sunstone": return LightKind.Sunstone;
                case "window": return LightKind.Window;
                case "moon": case "moonbeam": return LightKind.Moon;
                case "holy": case "votive": return LightKind.Holy;
                case "fire": case "bonfire": return LightKind.Fire;
                case "searchlight": return LightKind.Searchlight;
                case "sunbeam": case "dawn": return LightKind.Sunbeam;
            }
            return LightKind.GasLamp;
        }

        public override void Init(EntitySpec spec)
        {
            base.Init(spec);
            Setup(ParseKind(spec.Type));
            Radius = spec.OptFloat("radius", Radius);
            Intensity = spec.OptFloat("intensity", Intensity);
            LightGroup = spec.Opt("group");
            if (spec.Has("off")) On = false;
            if (spec.Has("caged")) Caged = true;
            if (spec.Opts.TryGetValue("color", out var c)) Color = Util.Hex(c, Color);
        }

        public void Setup(LightKind kind)
        {
            Kind = kind;
            switch (kind)
            {
                case LightKind.GasLamp: Radius = 7f; Intensity = 1f; Height = 3.2f; Color = Util.Hex("#ffb35c"); break;
                case LightKind.WallLamp: Radius = 6f; Intensity = 1f; Height = 2.6f; Color = Util.Hex("#ffa850"); break;
                case LightKind.Lantern: Radius = 4.5f; Intensity = 0.9f; Height = 1.1f; Color = Util.Hex("#ffbe6a"); Portable = true; break;
                case LightKind.Brazier: Radius = 6.5f; Intensity = 1.1f; Height = 1.3f; Color = Util.Hex("#ff8a3a"); break;
                case LightKind.Candle: Radius = 3.5f; Intensity = 0.8f; Height = 1.2f; Color = Util.Hex("#ffc078"); break;
                case LightKind.Chandelier: Radius = 9f; Intensity = 1f; Height = 4.5f; Color = Util.Hex("#ffcf8a"); Caged = true; break;
                case LightKind.Sunstone: Radius = 8f; Intensity = 1.3f; Height = 3.4f; Color = Util.Hex("#fff0b0"); Caged = true; Burns = true; break;
                case LightKind.Window: Radius = 4f; Intensity = 0.7f; Height = 2.2f; Color = Util.Hex("#ffb060"); Snuffable = false; break;
                case LightKind.Moon: Radius = 5f; Intensity = 0.55f; Height = 12f; Color = Util.Hex("#9fb8e8"); Snuffable = false; break;
                case LightKind.Holy: Radius = 5f; Intensity = 1f; Height = 1.4f; Color = Util.Hex("#fff1b8"); Burns = true; break;
                case LightKind.Fire: Radius = 8f; Intensity = 1.2f; Height = 1f; Color = Util.Hex("#ff7a30"); break;
                // the pool of a tower searchlight (see Searchlight): the lamp is out of reach, so nothing snuffs it by hand
                case LightKind.Searchlight: Radius = 4.6f; Intensity = 1.5f; Height = 2.4f; Color = Util.Hex("#dde8ff"); Caged = true; Snuffable = false; break;
                // dawn through a high window (see Sunbeam): real sunlight, and nothing to snuff
                case LightKind.Sunbeam: Radius = 3.4f; Intensity = 1.6f; Height = 1.6f; Color = Util.Hex("#ffe2a0"); Caged = true; Snuffable = false; Burns = true; break;
            }
            _flickerSeed = Random.value * 100f;
        }

        GameObject _cage;
        /// <summary>Caged by the Dossier (or authored): an iron cage around the flame; it can't be snuffed by hand.</summary>
        /// <summary>A thrall smashes the lamp: it goes dark for the rest of the mission (counts as her doing: guards notice it).</summary>
        public void Smash(bool silent = false)
        {
            if (Broken) return;
            if (!silent)
            {
                Game.Audio?.PlayAt("glass", SourcePos, 1f);
                Fx.Sparks(SourcePos, Color);
            }
            if (On) SetOn(false, true);
            Broken = true;
            WasSnuffedByPlayer = true;
            Apply();
        }

        /// <summary>Fixtures the Dossier's cm_caged can cage: the lights she puts out by hand on a street or in a room.</summary>
        public static bool CageEligible(LightKind k) => k == LightKind.GasLamp || k == LightKind.WallLamp || k == LightKind.Brazier || k == LightKind.Candle;

        /// <summary>cm_caged: exactly half the eligible lights (rounded up), chosen by a hash of their cell so the same ones
        /// are caged on every load and the choice is spread over the map. Returns indices into <paramref name="cells"/>. Pure.</summary>
        public static List<int> PickCaged(IList<Vector2Int> cells)
        {
            var order = new List<int>();
            for (int i = 0; i < cells.Count; i++) order.Add(i);
            order.Sort((a, b) =>
            {
                int c = Util.Hash(cells[a].x, cells[a].y, 7).CompareTo(Util.Hash(cells[b].x, cells[b].y, 7));
                if (c != 0) return c;
                c = cells[a].x.CompareTo(cells[b].x);
                return c != 0 ? c : cells[a].y.CompareTo(cells[b].y);
            });
            order.RemoveRange((cells.Count + 1) / 2, cells.Count - (cells.Count + 1) / 2);
            order.Sort();
            return order;
        }

        public void SetCaged(bool on)
        {
            Caged = on;
            if (!on) { if (_cage) Destroy(_cage); return; }
            if (_cage) return;
            _cage = new GameObject("Cage");
            _cage.transform.SetParent(transform, false);
            _cage.transform.position = SourcePos;
            var mat = Mats.Lit("iron_cage", Util.Hex("#1a1a1e"), null, 0.4f, 0.6f);
            // sized to the fixture: a squat grille over a brazier, a small one over a candle
            float size = Kind == LightKind.Brazier ? 1.6f : Kind == LightKind.Candle ? 0.5f : 1f;
            _cage.transform.localScale = Vector3.one * size;
            for (int i = 0; i < 4; i++)
            {
                var bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(bar.GetComponent<Collider>());
                bar.transform.SetParent(_cage.transform, false);
                float a = i * Mathf.PI * 0.5f + Mathf.PI * 0.25f;
                bar.transform.localPosition = new Vector3(Mathf.Cos(a) * 0.24f, 0f, Mathf.Sin(a) * 0.24f);
                bar.transform.localScale = new Vector3(0.04f, 0.62f, 0.04f);
                bar.GetComponent<MeshRenderer>().sharedMaterial = mat;
                bar.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            var hoop = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(hoop.GetComponent<Collider>());
            hoop.transform.SetParent(_cage.transform, false);
            hoop.transform.localPosition = new Vector3(0f, 0.33f, 0f);
            hoop.transform.localScale = new Vector3(0.52f, 0.05f, 0.52f);
            hoop.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        public bool CanSnuffByHand => On && Snuffable && !Caged && Kind != LightKind.Sunstone;
        public bool CanSmother => On && Snuffable && Kind != LightKind.Sunstone && Kind != LightKind.Moon && Kind != LightKind.Sunbeam;

        /// <summary>Create the Unity light and lamp model. wallDir = direction to an adjacent wall (zero for free-standing).</summary>
        public void BuildVisual(Vector3 wallDir)
        {
            if (_visual) Destroy(_visual);
            if (Kind == LightKind.Searchlight || Kind == LightKind.Sunbeam) return;   // Searchlight/Sunbeam build the lamp, the beam and the Unity light
            _visual = new GameObject("visual");
            _visual.transform.SetParent(transform, false);
            var mb = new MeshBuilder();
            var iron = Mats.Lit("lamp_iron", Util.Hex("#17181c"), null, 0.5f, 0.6f);
            var glassMat = Mats.Lit("lamp_glass_" + Kind, Color * 0.4f, null, 0.8f, 0, Color * 2.2f);
            var lightPos = Vector3.up * Height;
            switch (Kind)
            {
                case LightKind.GasLamp:
                    mb.Cylinder(iron, Vector3.zero, 0.07f, Height - 0.2f, 6);
                    mb.Box(iron, new Vector3(0, 0.15f, 0), new Vector3(0.3f, 0.3f, 0.3f));
                    break;
                case LightKind.WallLamp:
                    if (wallDir != Vector3.zero)
                    {
                        transform.position += wallDir * 0.82f;
                        mb.Box(iron, wallDir * 0.12f + Vector3.up * (Height - 0.15f), new Vector3(0.12f, 0.08f, 0.12f) + new Vector3(Mathf.Abs(wallDir.x), 0, Mathf.Abs(wallDir.z)) * 0.25f);
                    }
                    break;
                case LightKind.Brazier:
                case LightKind.Fire:
                    mb.Cylinder(iron, Vector3.zero, Kind == LightKind.Fire ? 0.6f : 0.35f, Kind == LightKind.Fire ? 0.3f : 0.9f, 8);
                    break;
                case LightKind.Candle:
                    mb.Box(Mats.Lit("candle_table", Util.Hex("#3a2a20"), "planks", 0.2f), Vector3.up * 0.45f, new Vector3(0.7f, 0.9f, 0.7f));
                    break;
                case LightKind.Sunstone:
                    mb.Cylinder(Mats.Lit("sun_post", Util.Hex("#8a7a50"), null, 0.6f, 0.8f), Vector3.zero, 0.12f, Height - 0.3f, 8);
                    break;
                case LightKind.Holy:
                    mb.Box(Mats.Lit("altar", Util.Hex("#cfc6b0"), "stone", 0.2f), Vector3.up * 0.5f, new Vector3(1.2f, 1f, 0.6f));
                    break;
                case LightKind.Chandelier:
                    mb.Cylinder(iron, Vector3.up * (Height + 0.1f), 0.02f, 3f, 4, false);
                    break;
            }
            mb.Build(_visual.transform, "lamp", true, Layers.Prop);
            // a fixture right under its own light (a post's top, a brazier's bowl, an altar) threw itself, many times
            // enlarged, as a dark patch on the most exposed ground
            foreach (var r in _visual.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
            if (Kind != LightKind.Moon && Kind != LightKind.Window)
            {
                var glass = GameObject.CreatePrimitive(Kind == LightKind.Chandelier || Kind == LightKind.Sunstone ? PrimitiveType.Sphere : PrimitiveType.Cube);
                Destroy(glass.GetComponent<Collider>());
                glass.name = "glass";
                glass.transform.SetParent(_visual.transform, false);
                glass.transform.localPosition = lightPos - Vector3.up * 0.15f;
                float gs = Kind == LightKind.Chandelier ? 0.7f : Kind == LightKind.Sunstone ? 0.45f : Kind == LightKind.Candle ? 0.12f : Kind == LightKind.Fire ? 0.8f : 0.26f;
                glass.transform.localScale = Vector3.one * gs;
                _glass = glass.GetComponent<Renderer>();
                _glass.sharedMaterial = glassMat;
                _glass.shadowCastingMode = ShadowCastingMode.Off;
            }

            var lgo = new GameObject("light");
            lgo.transform.SetParent(transform, false);
            lgo.transform.localPosition = lightPos;
            _light = lgo.AddComponent<Light>();
            _light.color = Color;
            bool spot = Kind == LightKind.GasLamp || Kind == LightKind.WallLamp || Kind == LightKind.Sunstone || Kind == LightKind.Chandelier || Kind == LightKind.Moon || Kind == LightKind.Window;
            if (spot)
            {
                _light.type = LightType.Spot;
                lgo.transform.localRotation = Quaternion.Euler(90, 0, 0);
                float h = Height;
                // cone covering the gameplay radius on the ground
                float ang = Mathf.Atan2(Radius, h) * Mathf.Rad2Deg * 2f;
                _light.spotAngle = Mathf.Clamp(ang + 10f, 30f, 170f);
                _light.innerSpotAngle = _light.spotAngle * 0.35f;
                _light.range = Mathf.Sqrt(Radius * Radius + h * h) * 1.15f;
            }
            else
            {
                _light.type = LightType.Point;
                _light.range = Radius * 1.25f;
            }
            _baseUnityIntensity = (Kind == LightKind.Moon ? 2.0f : 3.2f) * Intensity * (spot ? 1.3f : 1f);
            _light.intensity = _baseUnityIntensity;
            var q = Game.Settings != null ? Game.Settings.ShadowQuality : 2;
            _shadowMode = Portable || q == 0 ? LightShadows.None : (q >= 2 ? LightShadows.Soft : LightShadows.Hard);
            _light.shadows = LightShadows.None;   // LightSystem.BudgetShadows hands shadows to the lights nearest the camera
            _light.shadowBias = 0.04f;
            _light.shadowNormalBias = 0.3f;
            Apply();
        }

        public void SetOn(bool on, bool byPlayer = false)
        {
            if (on && Broken) return;
            if (On == on) return;
            On = on;
            if (!on) { SnuffedAt = Time.time; WasSnuffedByPlayer = byPlayer; if (byPlayer) AI.Squad.NotifyDark(SourcePos); }
            else WasSnuffedByPlayer = false;
            Apply();
            if (Game.Audio != null) Game.Audio.PlayAt(on ? "ignite" : "snuff", SourcePos, 0.8f);
        }

        /// <summary>
        /// A floor ring and a faint wash marking where a burning light (holy, sunstone) hurts. The ring follows the
        /// LightSystem.BurnAt radius and is pulled in where walls block the light. Built on the first Update so the
        /// level colliders exist.
        /// </summary>
        void BuildBurnRing()
        {
            _burnRingBuilt = true;
            if (_burnRing) Destroy(_burnRing);
            if (!Burns || Portable || Kind == LightKind.Sunbeam) return;   // a sunbeam draws its own moving ring
            _burnRing = new GameObject("burnring");
            _burnRing.transform.SetParent(transform, false);
            const int N = 56;
            const float width = 0.14f, y = 0.06f;
            var src = SourcePos;
            float reach = Radius * 0.85f, dy = Height - 1f;
            float ground = Mathf.Sqrt(Mathf.Max(0.1f, reach * reach - dy * dy));
            var rv = new List<Vector3>(); var rt = new List<int>();
            var fv = new List<Vector3> { new Vector3(0, y - 0.01f, 0) }; var ft = new List<int>();
            for (int i = 0; i <= N; i++)
            {
                float a = i * Mathf.PI * 2f / N;
                var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                float r = ground;
                if (Physics.Linecast(src, transform.position + dir * ground + Vector3.up, out var hit, Layers.LightBlockMask, QueryTriggerInteraction.Ignore))
                    r = Mathf.Max(0.3f, Util.FlatDistance(hit.point, transform.position) - 0.05f);
                rv.Add(dir * r + Vector3.up * y);
                rv.Add(dir * Mathf.Max(0f, r - width) + Vector3.up * y);
                fv.Add(dir * r + Vector3.up * (y - 0.01f));
                if (i == 0) continue;
                int o = rv.Count - 4;
                rt.AddRange(new[] { o, o + 2, o + 1, o + 1, o + 2, o + 3 });
                ft.AddRange(new[] { 0, fv.Count - 1, fv.Count - 2 });
            }
            void Part(string name, List<Vector3> v, List<int> t, Material m)
            {
                var go = new GameObject(name);
                go.layer = Layers.Overlay;
                go.transform.SetParent(_burnRing.transform, false);
                var mesh = new Mesh { name = name };
                mesh.SetVertices(v); mesh.SetTriangles(t, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = m;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }
            Part("ring", rv, rt, Mats.Overlay("burn_ring", new Color(1f, 0.86f, 0.5f, 0.5f), null, false, true));
            Part("wash", fv, ft, Mats.Overlay("burn_wash", new Color(1f, 0.86f, 0.5f, 0.06f), null, false, true));
            _burnRing.SetActive(On);
        }

        /// <summary>How far past the exposure contour the drawn light runs before it ends (D132).</summary>
        public const float Feather = LightKnee.Feather;
        /// <summary>Art-check switch: false keeps the authored (pre-QW17) Unity light shapes. Read on mission start.</summary>
        public static bool FitToExposure = true;
        /// <summary>Ground brightness inside a fitted pool (lux-like, before the light's colour), per unit Intensity.</summary>
        public static float PoolBright = 2.2f;
        public const int CookieSize = 128;
        Texture2D _cookie;
        /// <summary>The exposure contour this light's pool was fitted to (0 when not fitted).</summary>
        public float FittedContour { get; private set; }

        /// <summary>
        /// Cuts the Unity light to the gameplay one (QW17, SR.4, K34): a down-facing spot gets a radial cookie
        /// (<see cref="LightKnee"/>) so the ground is flat and bright where the light exposes her and ends
        /// <see cref="Feather"/> past the contour. URP has no cookies for point lights, so a point light gets a pool spot
        /// at its flame for the ground and keeps a short glow. Runs on the first Update, once the night's ambient is
        /// known (a blackout that changes <c>GlobalScale</c> later does not refit). Walls are not baked into the cookie: one cookie direction lights both a wall's face and the
        /// ground behind it, so real-time shadows (budgeted) stay the only occlusion.
        /// </summary>
        void FitUnityLight()
        {
            if (!FitToExposure || !_light || Kind == LightKind.Searchlight || Kind == LightKind.Sunbeam) return;   // these draw their own pools
            var ls = Game.Lights;
            float c = ls != null ? ls.ExposureRadius(this) : 0f;
            if (c <= 0.1f) return;   // never exposes her on the ground (a high moon): keep the authored look
            float h = Mathf.Max(0.3f, Height);
            if (_light.type == LightType.Spot) { if (FitSpot(_light, c, h, out _baseUnityIntensity)) FittedContour = c; return; }
            // a point light can't take a cookie: a down-facing pool spot at the flame lights the ground, and the point
            // light shrinks to a glow that keeps the nearby walls and the fixture warm
            if (_pool) return;   // fitted already
            var pgo = new GameObject("pool");
            pgo.transform.SetParent(transform, false);
            pgo.transform.localPosition = Vector3.up * Height;
            pgo.transform.localRotation = Quaternion.Euler(90, 0, 0);
            _pool = pgo.AddComponent<Light>();
            _pool.type = LightType.Spot;
            _pool.color = Color;
            _pool.shadows = LightShadows.None;
            _pool.shadowBias = _light.shadowBias;
            _pool.shadowNormalBias = _light.shadowNormalBias;
            if (!FitSpot(_pool, c, h, out _poolBase)) { Destroy(pgo); _pool = null; return; }
            _pool.enabled = On;
            _light.shadows = LightShadows.None;
            _light.range = Mathf.Min(_light.range, GlowRange);
            _baseUnityIntensity *= GlowShare;
            _light.intensity = _baseUnityIntensity;
            FittedContour = c;
        }

        /// <summary>A point light's glow once its pool spot lights the ground: reach and share of its old intensity.</summary>
        public const float GlowRange = 2.5f, GlowShare = 0.4f;
        Light _pool;
        float _poolBase;

        /// <summary>Gives a down-facing spot the knee cookie for contour <paramref name="c"/> at height
        /// <paramref name="h"/> (<see cref="LightKnee"/>) and returns the intensity that makes the pool
        /// <see cref="PoolBright"/>.</summary>
        bool FitSpot(Light l, float c, float h, out float intensity)
        {
            var ls = Game.Lights;
            LightKnee.Shape(c, h, out float inner, out float outer, out float range);
            float ambient = ls.Ambient, scale = ls.GlobalScale, dy = Height - 1f;
            var vals = new float[CookieSize * CookieSize];
            float k = LightKnee.Cookie(vals, CookieSize, c, h, outer, range,
                r => ambient + DetectionMath.LightFalloff(Intensity, Radius, Mathf.Sqrt(r * r + dy * dy)) * scale);
            intensity = l.intensity;
            if (k <= 0f) return false;
            if (_cookie) Destroy(_cookie);
            _cookie = new Texture2D(CookieSize, CookieSize, TextureFormat.RGBAHalf, false, true)
                { name = "knee_" + name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color[vals.Length];
            for (int i = 0; i < vals.Length; i++) px[i] = new Color(vals[i], vals[i], vals[i], 1f);
            _cookie.SetPixels(px);
            _cookie.Apply(false, true);
            l.cookie = _cookie;
            // outer first: Unity clamps the inner angle to the outer one already set
            float innerDeg = Mathf.Clamp(2f * Mathf.Atan2(inner, h) * Mathf.Rad2Deg, 1f, 177f);
            l.spotAngle = Mathf.Clamp(2f * Mathf.Atan2(outer, h) * Mathf.Rad2Deg, innerDeg + 1f, 178f);
            l.innerSpotAngle = innerDeg;
            l.range = range;
            intensity = PoolBright * Intensity / k;
            l.intensity = intensity;
            return true;
        }

        void OnDestroy() { if (_cookie) Destroy(_cookie); }

        void Apply()
        {
            if (_burnRing) _burnRing.SetActive(On);
            if (_light) _light.enabled = On;
            if (_pool) _pool.enabled = On;
            if (_glass) _glass.enabled = !Broken && (On || Kind == LightKind.GasLamp || Kind == LightKind.WallLamp);
            if (_glass && !On) _glass.sharedMaterial = Mats.Lit("lamp_glass_off", Util.Hex("#1a1a1e"), null, 0.8f);
            else if (_glass) _glass.sharedMaterial = Mats.Lit("lamp_glass_" + Kind, Color * 0.4f, null, 0.8f, 0, Color * 2.2f);
        }

        void Update()
        {
            if (!_burnRingBuilt) { BuildBurnRing(); FitUnityLight(); }
            if (!_light || !On) return;
            if (Kind == LightKind.Brazier || Kind == LightKind.Fire || Kind == LightKind.Candle || Kind == LightKind.Lantern || Kind == LightKind.GasLamp)
            {
                float amt = Kind == LightKind.GasLamp ? 0.04f : 0.15f;
                float f = 1f + (Mathf.PerlinNoise(_flickerSeed, Time.time * 3f) - 0.5f) * 2f * amt;
                _light.intensity = _baseUnityIntensity * f;
                if (_pool) _pool.intensity = _poolBase * f;
            }
        }

        void OnEnable() { Game.Lights?.Register(this); }
        void OnDisable() { Game.Lights?.Unregister(this); }
    }
}
