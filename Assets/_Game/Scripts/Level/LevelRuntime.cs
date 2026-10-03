using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Vespertine.Core;
using Vespertine.Stealth;
using Vespertine.Visual;

namespace Vespertine.Level
{
    /// <summary>Owns everything that belongs to a loaded mission map: geometry, navmesh, entities and their registry.</summary>
    public class LevelRuntime : MonoBehaviour
    {
        public LevelData Data { get; private set; }
        public LevelGrid Grid { get; private set; }
        public NavBuilder Nav { get; private set; }
        public Transform EntityRoot { get; private set; }
        public Transform DynamicRoot { get; private set; }
        public Bounds WorldBounds { get; private set; }

        readonly Dictionary<string, Entity> _byId = new Dictionary<string, Entity>();
        readonly List<Entity> _all = new List<Entity>();
        readonly Dictionary<string, List<GameObject>> _groups = new Dictionary<string, List<GameObject>>();
        readonly HashSet<string> _activeGroups = new HashSet<string>();
        readonly List<Zone> _zones = new List<Zone>();
        GameObject _geometry;
        Light _moon;
        bool _navDirty;
        float _navDirtyAt;

        public IReadOnlyList<Entity> AllEntities => _all;
        public IEnumerable<string> ActiveGroups => _activeGroups;

        /// <summary>Build the static world (everything except characters). Characters are spawned by the mission.</summary>
        public void Load(LevelData data)
        {
            Unload();
            Data = data;
            Grid = new LevelGrid(data);
            WorldBounds = new Bounds(new Vector3(Grid.W, 0, Grid.H) * LevelData.CellSize * 0.5f, new Vector3(Grid.W, 4, Grid.H) * LevelData.CellSize);
            _geometry = LevelBuilder.Build(Grid, transform);
            EntityRoot = new GameObject("Entities").transform;
            EntityRoot.SetParent(transform, false);
            DynamicRoot = new GameObject("Dynamic").transform;
            DynamicRoot.SetParent(transform, false);

            Nav = new NavBuilder(Grid);
            foreach (var spec in data.Entities)
            {
                if (spec.Kind == "npc" || spec.Kind == "player" || spec.Kind == "corpse") continue; // characters: mission
                try { SpawnStatic(spec); }
                catch (System.Exception ex) { Debug.LogError($"[Level] line {spec.Line} {spec}: {ex.Message}\n{ex.StackTrace}"); }
            }
            RefreshVampireBlockers();
            Nav.BuildAll();
            SetupAtmosphere();
        }

        // Runtime nav data and links outlive the GameObject unless removed explicitly (e.g. on leaving play mode).
        void OnDestroy() { Nav?.Clear(); Nav = null; }

        public void Unload()
        {
            Nav?.Clear();
            Nav = null;
            foreach (Transform c in transform) Destroy(c.gameObject);
            _byId.Clear(); _all.Clear(); _groups.Clear(); _activeGroups.Clear(); _zones.Clear();
            _moon = null;
            Data = null; Grid = null;
        }

        // ------------------------------------------------------------------ spawning
        T Make<T>(EntitySpec spec, string name = null) where T : Entity
        {
            var go = new GameObject(name ?? (spec.Kind + "_" + spec.Id));
            go.transform.SetParent(EntityRoot, false);
            go.transform.position = Data.CellToWorld(spec.X, spec.Y, SurfaceHeightCell(spec.X, spec.Y) + spec.OptFloat("z", 0));
            var e = go.AddComponent<T>();
            e.Init(spec);
            if (spec.Opts.TryGetValue("face", out var f)) go.transform.rotation = Quaternion.Euler(0, MapParser.ParseFacing(f), 0);
            Register(e);
            return e;
        }

        public void Register(Entity e)
        {
            if (string.IsNullOrEmpty(e.Id)) e.Id = e.name + "#" + _all.Count;
            if (_byId.ContainsKey(e.Id)) Debug.LogWarning($"[Level] duplicate id {e.Id}");
            _byId[e.Id] = e;
            _all.Add(e);
            if (!string.IsNullOrEmpty(e.Group))
            {
                if (!_groups.TryGetValue(e.Group, out var l)) _groups[e.Group] = l = new List<GameObject>();
                l.Add(e.gameObject);
                if (!_activeGroups.Contains(e.Group)) e.gameObject.SetActive(false);
            }
        }

        public void Unregister(Entity e)
        {
            if (e == null) return;
            if (e.Id != null && _byId.TryGetValue(e.Id, out var x) && x == e) _byId.Remove(e.Id);
            _all.Remove(e);
        }

        void SpawnStatic(EntitySpec s)
        {
            switch (s.Kind)
            {
                case "light":
                {
                    var l = Make<GameLight>(s);
                    l.BuildVisual(WallDirAt(Mathf.RoundToInt(s.X), Mathf.RoundToInt(s.Y)));
                    if (l.Kind == LightKind.Searchlight) l.gameObject.AddComponent<Searchlight>().Setup(s, Data, SurfaceHeightCell);
                    if (l.Kind == LightKind.Sunbeam) l.gameObject.AddComponent<Sunbeam>().Setup(s, Data, SurfaceHeightCell);
                    // a sunstone lamp can't be snuffed, only smashed by a thrall (or cut at its generator)
                    if (l.Kind == LightKind.Sunstone && !string.IsNullOrEmpty(s.Id) && !s.Has("nosmash"))
                    {
                        var ss = new EntitySpec { Kind = "smash", Id = s.Id + ".smash", X = s.X, Y = s.Y, Group = s.Group, Line = s.Line };
                        if (s.Opts.TryGetValue("z", out var z)) ss.Opts["z"] = z;
                        Make<LampSmash>(ss).Light = l;
                    }
                    break;
                }
                case "prop":
                {
                    var type = s.Type ?? "crate";
                    var pos = Data.CellToWorld(s.X, s.Y, SurfaceHeightCell(s.X, s.Y));
                    float yaw = s.Opts.TryGetValue("face", out var f) ? MapParser.ParseFacing(f) : s.OptFloat("yaw", 0);
                    if (s.Has("hide"))
                    {
                        var h = Make<HideSpot>(s, "hide_" + type);
                        h.transform.rotation = Quaternion.Euler(0, yaw, 0);
                        PropFactory.Build(type, h.transform, pos, yaw, s.Line);
                    }
                    else PropFactory.Build(type, EntityRoot, pos, yaw, s.Line);
                    var info = PropFactory.Info(type);
                    if (info.Blocks)
                    {
                        var size = Quaternion.Euler(0, yaw, 0) * info.Size;
                        size = new Vector3(Mathf.Abs(size.x), info.Size.y, Mathf.Abs(size.z));
                        Nav.ExtraBlockers.Add(new Bounds(pos + Vector3.up * info.Size.y * 0.5f, size + new Vector3(0.1f, 0, 0.1f)));
                    }
                    break;
                }
                case "hide":
                {
                    var type = s.Opt("type", "crate");
                    s.Type = type;
                    var h = Make<HideSpot>(s);
                    PropFactory.Build(type, h.transform, h.transform.position, s.OptFloat("yaw", 0), s.Line);
                    var info = PropFactory.Info(type);
                    if (info.Blocks) Nav.ExtraBlockers.Add(new Bounds(h.transform.position + Vector3.up * info.Size.y * 0.5f, info.Size));
                    break;
                }
                case "door":
                {
                    var d = Make<Door>(s);
                    d.Build(Grid, Mathf.RoundToInt(s.X), Mathf.RoundToInt(s.Y));
                    break;
                }
                case "gate":
                {
                    var g = Make<Gate>(s);
                    g.Build(Grid, Mathf.RoundToInt(s.X), Mathf.RoundToInt(s.Y));
                    break;
                }
                case "valve": Make<Valve>(s); break;
                case "generator": Make<Generator>(s); break;
                case "lever": Make<Lever>(s); break;
                case "note": Make<Note>(s); break;
                case "secret": Make<Secret>(s); break;
                case "item": Make<Item>(s); break;
                case "bell": Make<Bell>(s); break;
                case "window": case "boat": case "use": Make<ScriptedUse>(s); break;
                case "spawn": Make<SpawnPoint>(s); break;
                case "listen": Make<ListenPoint>(s); break;
                case "trap": Make<Trap>(s); break;
                case "squad": Make<AI.Squad>(s); break;
                case "zone":
                {
                    var z = Make<Zone>(s);
                    z.Setup(Data);
                    _zones.Add(z);
                    break;
                }
                case "stain":
                    Evidence.SpawnStain(Data.CellToWorld(s.X, s.Y, SurfaceHeightCell(s.X, s.Y)), EntityRoot, true);
                    break;
            }
        }

        Vector3 WallDirAt(int x, int y)
        {
            if (Grid.Raised(x, y)) return Vector3.zero;
            foreach (var d in LevelGrid.Dirs4)
                if (Grid.Raised(x + d.x, y + d.y) && Grid.Top(x + d.x, y + d.y) >= 3f) return LevelGrid.StepDir(d.x, d.y);
            return Vector3.zero;
        }

        void SetupAtmosphere()
        {
            float ambient = Data.GetFloat("ambient", 0.06f);
            if (Game.Lights != null) Game.Lights.Ambient = ambient;
            var amb = Util.Hex(Data.Get("ambientColor", "#1a2135"));
            RenderSettings.ambientMode = AmbientMode.Trilight;
            // A neutral floor keeps silhouettes readable from the tactical camera even in the darkest maps;
            // the map colour only tints it. Darkness for stealth is a gameplay value (Lights.Ambient), not a render one.
            RenderSettings.ambientSkyColor = amb * 1.6f + new Color(0.14f, 0.15f, 0.17f);
            RenderSettings.ambientEquatorColor = amb + new Color(0.10f, 0.10f, 0.11f);
            RenderSettings.ambientGroundColor = amb * 0.4f + new Color(0.03f, 0.03f, 0.035f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = Data.GetFloat("fog", 0.006f);
            RenderSettings.fogColor = Util.Hex(Data.Get("fogColor", "#0a0d16"));
            var go = new GameObject("Moon");
            go.transform.SetParent(transform, false);
            _moon = go.AddComponent<Light>();
            _moon.type = LightType.Directional;
            _moon.color = Util.Hex(Data.Get("moonColor", "#7f96c8"));
            _moon.intensity = Data.GetFloat("moon", 0.35f);
            _moon.shadows = Game.Settings != null && Game.Settings.ShadowQuality > 0 ? LightShadows.Soft : LightShadows.None;
            _moon.shadowStrength = 0.85f;
            go.transform.rotation = Quaternion.Euler(Data.GetFloat("moonPitch", 48f), Data.GetFloat("moonYaw", 210f), 0);
        }

        // ------------------------------------------------------------------ queries
        public Entity Get(string id) => id != null && _byId.TryGetValue(id, out var e) ? e : null;
        public T Get<T>(string id) where T : Entity => Get(id) as T;

        public IEnumerable<T> All<T>() where T : Entity
        {
            for (int i = 0; i < _all.Count; i++)
                if (_all[i] is T t && t) yield return t;
        }

        public float SurfaceHeightCell(float x, float y) => Grid.Top(Mathf.RoundToInt(x), Mathf.RoundToInt(y));

        public Vector2Int CellOf(Vector3 world) => Data.WorldToCellInt(world);

        public TileDef TileAt(Vector3 world)
        {
            var c = CellOf(world);
            return Grid.Def(c.x, c.y);
        }

        public Surface SurfaceAt(Vector3 feet)
        {
            var c = CellOf(feet);
            var d = Grid.Def(c.x, c.y);
            if (d.Raised && feet.y < d.Height - 0.5f) return Surface.Stone; // climbing face
            return d.Surface;
        }

        /// <summary>True if the position stands in hedge / reeds (concealment).</summary>
        public bool InFoliage(Vector3 feet)
        {
            var c = CellOf(feet);
            return Grid.Kind(c.x, c.y) == TileKind.Hedge && feet.y < 0.5f;
        }

        public bool IsCanal(Vector3 p)
        {
            var c = CellOf(p);
            return Grid.Kind(c.x, c.y) == TileKind.Canal;
        }

        /// <summary>Nearest canal edge point to dump a body, within range.</summary>
        public bool CanalNear(Vector3 p, float range, out Vector3 water)
        {
            water = default;
            var c = CellOf(p);
            int r = Mathf.CeilToInt(range / LevelData.CellSize);
            float best = float.MaxValue;
            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                    if (Grid.Kind(c.x + dx, c.y + dy) == TileKind.Canal)
                    {
                        var w = Data.CellToWorld(c.x + dx, c.y + dy, LevelBuilder.WaterLevel);
                        float d = Util.FlatDistance(w, p);
                        if (d < best && d <= range + 1f) { best = d; water = w; }
                    }
            return best < float.MaxValue;
        }

        public Zone ZoneAt(Vector3 p, bool restrictedOnly = false)
        {
            foreach (var z in _zones)
                if (z && z.gameObject.activeInHierarchy && (!restrictedOnly || z.Restricted) && z.Contains(p)) return z;
            return null;
        }

        public IReadOnlyList<Zone> Zones => _zones;

        // ------------------------------------------------------------------ groups
        public bool GroupActive(string g) => _activeGroups.Contains(g);

        public void ActivateGroup(string g, bool on = true)
        {
            if (on) _activeGroups.Add(g); else _activeGroups.Remove(g);
            if (_groups.TryGetValue(g, out var list))
                foreach (var go in list) if (go) go.SetActive(on);
            if (on) foreach (var q in new List<AI.Squad>(AI.Squad.All)) q.Remuster();
        }

        // ------------------------------------------------------------------ navmesh refresh (doors / thresholds)
        public void RequestVampireNavRebuild()
        {
            _navDirty = true;
            _navDirtyAt = Time.unscaledTime;
        }

        void RefreshVampireBlockers()
        {
            Nav.VampireBlockers.Clear();
            // a home's interior is barred too (no dropping in over an open-top wall) until its door invites her
            foreach (var z in _zones)
            {
                if (z.HomeDoors.Length == 0) continue;
                bool invited = false;
                foreach (var id in z.HomeDoors) { var door = Get<Door>(id); if (door != null && door.Invited) invited = true; }
                if (!invited) { Nav.VampireBlockers.Add(z.WorldBounds); continue; }
                // one invitation opens the whole house: every threshold of the home now admits her
                foreach (var id in z.HomeDoors) { var door = Get<Door>(id); if (door != null && !door.Invited) door.Invited = true; }
            }
            foreach (var d in All<Door>())
                if (d.BlocksVampire) Nav.VampireBlockers.Add(d.CellBounds);
        }

        void Update()
        {
            if (Nav == null) return;
            Nav.Tick();
            if (_navDirty && !Nav.Busy && Time.unscaledTime - _navDirtyAt > 0.05f)
            {
                _navDirty = false;
                RefreshVampireBlockers();
                Nav.RebuildVampire();
            }
            if (Game.Lights != null && Game.Cam != null)
            {
                int q = Game.Settings != null ? Game.Settings.ShadowQuality : 2;
                Game.Lights.BudgetShadows(Game.Cam.Pivot, q >= 2 ? 8 : q == 1 ? 5 : 0);
            }
            // zone enter events for the player
            var p = Game.Player;
            if (p != null)
            {
                var pos = p.transform.position;
                foreach (var z in _zones)
                {
                    if (!z || !z.gameObject.activeInHierarchy) continue;
                    bool inside = z.Contains(pos);
                    if (inside && !z.PlayerInside) GameEvents.RaiseZoneEntered(z.Id);
                    z.PlayerInside = inside;
                }
            }
        }

        // ------------------------------------------------------------------ save
        public void SaveState(List<Save.EntityState> into, List<string> groups)
        {
            groups.AddRange(_activeGroups);
            foreach (var e in _all)
            {
                if (!e) continue;
                var s = new Save.EntityState { Id = e.Id, Active = e.gameObject.activeSelf };
                switch (e)
                {
                    case Door d: s.B0 = d.Locked; s.B1 = d.Invited; s.B2 = d.IsOpen; break;
                    case Gate g: s.B0 = g.Open; break;
                    case Interactable i: i.SaveState(s); break;
                    case GameLight l:
                        s.B0 = l.On; s.B1 = l.WasSnuffedByPlayer; s.B2 = l.Broken;
                        if (l.TryGetComponent<Searchlight>(out var sl)) sl.SaveState(s);
                        break;
                    case ListenPoint lp: s.B0 = lp.Triggered; break;
                    case AI.Squad q: q.SaveState(s); break;
                    default: if (e is AI.Npc || e is Player.Vampire) continue; break;
                }
                into.Add(s);
            }
        }

        public void LoadState(List<Save.EntityState> from, List<string> groups)
        {
            foreach (var g in new List<string>(_activeGroups)) ActivateGroup(g, false);
            foreach (var g in groups) ActivateGroup(g, true);
            foreach (var s in from)
            {
                var e = Get(s.Id);
                if (!e) continue;
                switch (e)
                {
                    case Door d: d.Locked = s.B0; d.Invited = s.B1; d.SetOpen(s.B2, true); break;
                    case Gate g: g.SetOpen(s.B0, true); break;
                    case Interactable i: i.LoadState(s); break;
                    case GameLight l:
                        if (s.B2) l.Smash(true);
                        else { l.Broken = false; l.SetOn(s.B0); }
                        l.WasSnuffedByPlayer = s.B1;
                        if (l.TryGetComponent<Searchlight>(out var sl)) sl.LoadState(s);
                        break;
                    case ListenPoint lp: lp.SetTriggered(s.B0); break;
                    case AI.Squad q: q.LoadState(s); break;
                }
                if (string.IsNullOrEmpty(e.Group)) e.gameObject.SetActive(s.Active);
            }
            RequestVampireNavRebuild();
        }
    }
}
