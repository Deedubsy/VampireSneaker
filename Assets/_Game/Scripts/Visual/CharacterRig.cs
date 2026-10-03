using UnityEngine;
using Vespertine.Core;
using Vespertine.Data;

namespace Vespertine.Visual
{
    public enum Pose { Stand, Walk, Run, Sneak, Climb, Feed, Victim, Lying, Carried, Mesmerised, Aim, Panic, Sit, Sleep, Interact }

    /// <summary>Procedural low-poly character built from boxes with a simple limb-swing animation.</summary>
    public class CharacterRig : MonoBehaviour
    {
        public Transform Body, Torso, Head, ArmL, ArmR, LegL, LegR, Hand, Prop;
        public bool Quadruped;
        public Pose Pose;
        public float Speed;
        float _phase, _lie, _bob;
        Renderer[] _renderers;
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        MaterialPropertyBlock _mpb;

        static Material M(string key, Color c, float s = 0.15f) => Mats.Lit(key, c, null, s);

        static Transform Part(Transform parent, string name, Vector3 local)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = local;
            return t;
        }

        static void Mesh(Transform t, System.Action<MeshBuilder> build, int layer)
        {
            var mb = new MeshBuilder();
            build(mb);
            mb.Build(t, "m", true, layer);
        }

        public static CharacterRig BuildHuman(Transform parent, Archetype a, int seed, int layer = Layers.Character)
        {
            var root = Part(parent, "rig", Vector3.zero);
            var rig = root.gameObject.AddComponent<CharacterRig>();
            var rng = new System.Random(seed);
            float tall = 1f + ((float)rng.NextDouble() - 0.5f) * 0.08f;
            float wide = 1f + ((float)rng.NextDouble() - 0.5f) * 0.14f;
            var coat = M("coat_" + a.Id, a.Coat);
            var accent = M("acc_" + a.Id, a.Accent, 0.4f);
            var skinTone = a.Has(ArchFlags.Undead) ? "#9a9894" : new[] { "#c8a088", "#a07860", "#d8b8a0", "#7a5a48" }[rng.Next(4)];
            if (a.Has(ArchFlags.Undead)) wide *= 0.82f;   // starved
            var skin = M("skin_" + skinTone, Util.Hex(skinTone), 0.3f);
            var trousers = M("trousers", Util.Hex("#1e1e22"));
            bool robe = a.Id == "priest" || a.Id == "acolyte" || a.Id == "alchemist" || a.Id == "inquisitor" || a.Id == "vane" || a.Id == "tracker";

            rig.Body = Part(root, "body", Vector3.zero);
            rig.LegL = Part(rig.Body, "legL", new Vector3(-0.12f * wide, 0.85f * tall, 0));
            rig.LegR = Part(rig.Body, "legR", new Vector3(0.12f * wide, 0.85f * tall, 0));
            Mesh(rig.LegL, mb => { mb.Box(trousers, new Vector3(0, -0.42f * tall, 0), new Vector3(0.17f, 0.85f * tall, 0.2f)); mb.Box(M("boot", Util.Hex("#141414"), 0.4f), new Vector3(0, -0.8f * tall, 0.04f), new Vector3(0.18f, 0.12f, 0.28f)); }, layer);
            Mesh(rig.LegR, mb => { mb.Box(trousers, new Vector3(0, -0.42f * tall, 0), new Vector3(0.17f, 0.85f * tall, 0.2f)); mb.Box(M("boot", Util.Hex("#141414"), 0.4f), new Vector3(0, -0.8f * tall, 0.04f), new Vector3(0.18f, 0.12f, 0.28f)); }, layer);
            rig.Torso = Part(rig.Body, "torso", new Vector3(0, 0.85f * tall, 0));
            Mesh(rig.Torso, mb =>
            {
                mb.Box(coat, new Vector3(0, 0.33f * tall, 0), new Vector3(0.46f * wide, 0.66f * tall, 0.28f));
                if (robe) mb.Box(coat, new Vector3(0, -0.4f * tall, 0), new Vector3(0.5f * wide, 0.8f * tall, 0.34f));
                else mb.Box(coat, new Vector3(0, -0.12f, -0.02f), new Vector3(0.48f * wide, 0.3f, 0.3f));
                mb.Box(accent, new Vector3(0, 0.05f, 0), new Vector3(0.48f * wide, 0.06f, 0.3f));
                // the Pale Vigil: a pale shoulder mantle (what the camera sees from above) and a long coat-tail behind
                if (a.Faction == Faction.Vigil)
                {
                    mb.Box(accent, new Vector3(0, 0.6f * tall, -0.01f), new Vector3(0.62f * wide, 0.12f, 0.36f));
                    if (!robe) mb.Box(coat, new Vector3(0, -0.42f * tall, -0.15f), new Vector3(0.46f * wide, 0.62f * tall, 0.05f));
                }
            }, layer);
            rig.Head = Part(rig.Torso, "head", new Vector3(0, 0.66f * tall + 0.05f, 0));
            Mesh(rig.Head, mb =>
            {
                mb.Box(skin, new Vector3(0, 0.13f, 0), new Vector3(0.24f, 0.27f, 0.25f));
                switch (a.Hat)
                {
                    case "stovepipe": mb.Box(M("hat_dark", Util.Hex("#121216")), new Vector3(0, 0.3f, 0), new Vector3(0.36f, 0.04f, 0.38f)); mb.Box(M("hat_dark", Util.Hex("#121216")), new Vector3(0, 0.45f, 0), new Vector3(0.24f, 0.3f, 0.25f)); break;
                    case "cap": mb.Box(accent, new Vector3(0, 0.28f, 0.02f), new Vector3(0.27f, 0.08f, 0.3f)); mb.Box(accent, new Vector3(0, 0.25f, 0.16f), new Vector3(0.24f, 0.03f, 0.12f)); break;
                    case "brim": mb.Box(M("hat_dark", Util.Hex("#121216")), new Vector3(0, 0.29f, 0), new Vector3(0.52f, 0.03f, 0.52f)); mb.Box(M("hat_dark", Util.Hex("#121216")), new Vector3(0, 0.36f, 0), new Vector3(0.25f, 0.13f, 0.26f)); break;
                    case "hood": mb.Box(coat, new Vector3(0, 0.17f, -0.03f), new Vector3(0.32f, 0.36f, 0.32f)); break;
                    case "helm": mb.Box(M("steel", Util.Hex("#7a7a82"), 0.6f), new Vector3(0, 0.22f, 0), new Vector3(0.3f, 0.22f, 0.3f)); break;
                    // the Church: a tall pale mitre, so a priest's aura is never mistaken for a servant's black coat
                    case "mitre": mb.Box(accent, new Vector3(0, 0.38f, 0), new Vector3(0.24f, 0.3f, 0.2f)); mb.Box(accent, new Vector3(0, 0.58f, 0), new Vector3(0.14f, 0.14f, 0.12f)); break;
                    // the alchemist's smoke-mask: a hood with a leather beak
                    case "beak": mb.Box(coat, new Vector3(0, 0.17f, -0.03f), new Vector3(0.32f, 0.36f, 0.32f)); mb.Box(M("leather", Util.Hex("#8a7050"), 0.3f), new Vector3(0, 0.08f, 0.24f), new Vector3(0.1f, 0.1f, 0.24f)); break;
                    case "mortar": mb.Box(M("hat_dark", Util.Hex("#121216")), new Vector3(0, 0.29f, 0), new Vector3(0.26f, 0.06f, 0.26f)); mb.Box(M("hat_dark", Util.Hex("#121216")), new Vector3(0, 0.33f, 0), new Vector3(0.42f, 0.025f, 0.42f)); break;
                }
            }, layer);
            rig.ArmL = Part(rig.Torso, "armL", new Vector3(-0.29f * wide, 0.6f * tall, 0));
            rig.ArmR = Part(rig.Torso, "armR", new Vector3(0.29f * wide, 0.6f * tall, 0));
            Mesh(rig.ArmL, mb => { mb.Box(coat, new Vector3(0, -0.3f, 0), new Vector3(0.13f, 0.62f, 0.15f)); mb.Box(skin, new Vector3(0, -0.66f, 0), new Vector3(0.1f, 0.1f, 0.1f)); }, layer);
            Mesh(rig.ArmR, mb => { mb.Box(coat, new Vector3(0, -0.3f, 0), new Vector3(0.13f, 0.62f, 0.15f)); mb.Box(skin, new Vector3(0, -0.66f, 0), new Vector3(0.1f, 0.1f, 0.1f)); }, layer);
            rig.Hand = Part(rig.ArmL, "hand", new Vector3(0, -0.7f, 0.05f));

            // weapon / tool in the right hand
            var tool = Part(rig.ArmR, "tool", new Vector3(0, -0.68f, 0.05f));
            switch (a.Weapon)
            {
                case Weapon.Musket: Mesh(tool, mb => { mb.Box(M("gunwood", Util.Hex("#3a2618")), new Vector3(0, 0, 0.1f), new Vector3(0.06f, 0.08f, 0.5f)); mb.Box(M("steel", Util.Hex("#7a7a82"), 0.6f), new Vector3(0, 0.02f, 0.65f), new Vector3(0.03f, 0.03f, 0.7f)); }, layer); tool.localRotation = Quaternion.Euler(80, 0, 0); break;
                case Weapon.Pistol: Mesh(tool, mb => mb.Box(M("steel", Util.Hex("#7a7a82"), 0.6f), new Vector3(0, 0, 0.1f), new Vector3(0.05f, 0.08f, 0.26f)), layer); break;
                case Weapon.Crossbow: Mesh(tool, mb => { mb.Box(M("gunwood", Util.Hex("#3a2618")), new Vector3(0, 0, 0.15f), new Vector3(0.06f, 0.06f, 0.45f)); mb.Box(M("silver", Util.Hex("#d0d0e0"), 0.8f), new Vector3(0, 0, 0.3f), new Vector3(0.5f, 0.03f, 0.04f)); }, layer); break;
                case Weapon.Cudgel: Mesh(tool, mb => mb.Box(M("gunwood", Util.Hex("#3a2618")), new Vector3(0, -0.2f, 0.05f), new Vector3(0.07f, 0.5f, 0.07f)), layer); break;
                case Weapon.Censer: Mesh(tool, mb => { mb.Box(M("chain", Util.Hex("#6a6050"), 0.6f), new Vector3(0, -0.15f, 0.05f), new Vector3(0.02f, 0.3f, 0.02f)); mb.Box(M("brass", Util.Hex("#a08040"), 0.7f), new Vector3(0, -0.36f, 0.05f), new Vector3(0.16f, 0.16f, 0.16f)); }, layer); break;
                case Weapon.Halberd: Mesh(tool, mb => { mb.Box(M("gunwood", Util.Hex("#3a2618")), new Vector3(0, 0.6f, 0), new Vector3(0.06f, 2.2f, 0.06f)); mb.Box(M("steel", Util.Hex("#7a7a82"), 0.6f), new Vector3(0.12f, 1.6f, 0), new Vector3(0.25f, 0.3f, 0.03f)); }, layer); break;
            }
            if (a.Id == "lamplighter") Mesh(tool, mb => mb.Box(M("gunwood", Util.Hex("#3a2618")), new Vector3(0, 0.7f, 0), new Vector3(0.04f, 2.4f, 0.04f)), layer);
            rig.Prop = tool;
            rig.Finish();
            return rig;
        }

        public static CharacterRig BuildHound(Transform parent, Archetype a, int seed, int layer = Layers.Character)
        {
            var root = Part(parent, "rig", Vector3.zero);
            var rig = root.gameObject.AddComponent<CharacterRig>();
            rig.Quadruped = true;
            var fur = M("fur_" + a.Id, a.Coat, 0.05f);
            var dark = M("fur_dark", Util.Hex("#1a1614"), 0.05f);
            rig.Body = Part(root, "body", Vector3.zero);
            rig.Torso = Part(rig.Body, "torso", new Vector3(0, 0.55f, 0));
            Mesh(rig.Torso, mb => { mb.Box(fur, Vector3.zero, new Vector3(0.32f, 0.32f, 0.95f)); mb.Box(dark, new Vector3(0, 0.08f, -0.55f), new Vector3(0.06f, 0.06f, 0.3f), 0); }, layer);
            rig.Head = Part(rig.Torso, "head", new Vector3(0, 0.18f, 0.5f));
            Mesh(rig.Head, mb => { mb.Box(fur, new Vector3(0, 0.05f, 0.08f), new Vector3(0.26f, 0.24f, 0.28f)); mb.Box(dark, new Vector3(0, 0, 0.3f), new Vector3(0.14f, 0.12f, 0.22f)); mb.Box(dark, new Vector3(-0.09f, 0.2f, 0), new Vector3(0.06f, 0.12f, 0.06f)); mb.Box(dark, new Vector3(0.09f, 0.2f, 0), new Vector3(0.06f, 0.12f, 0.06f)); }, layer);
            rig.LegL = Part(rig.Body, "legFL", new Vector3(-0.12f, 0.45f, 0.35f));
            rig.LegR = Part(rig.Body, "legFR", new Vector3(0.12f, 0.45f, 0.35f));
            rig.ArmL = Part(rig.Body, "legBL", new Vector3(-0.12f, 0.45f, -0.35f));
            rig.ArmR = Part(rig.Body, "legBR", new Vector3(0.12f, 0.45f, -0.35f));
            foreach (var l in new[] { rig.LegL, rig.LegR, rig.ArmL, rig.ArmR })
                Mesh(l, mb => mb.Box(dark, new Vector3(0, -0.22f, 0), new Vector3(0.09f, 0.45f, 0.1f)), layer);
            rig.Hand = Part(rig.Head, "mouth", new Vector3(0, 0, 0.35f));
            rig.Finish();
            return rig;
        }

        public static CharacterRig BuildVampire(Transform parent, int layer = Layers.Character)
        {
            var root = Part(parent, "rig", Vector3.zero);
            var rig = root.gameObject.AddComponent<CharacterRig>();
            var cloak = M("ilse_cloak", Util.Hex("#15121c"), 0.25f);
            var dress = M("ilse_dress", Util.Hex("#3a0e18"), 0.3f);
            var skin = M("ilse_skin", Util.Hex("#d8d4dc"), 0.45f);
            var hair = M("ilse_hair", Util.Hex("#0e0c10"), 0.4f);
            var eyes = Mats.Lit("ilse_eyes", Mats.Pal.BloodBright, null, 0.8f, 0, Mats.Pal.BloodBright * 3f);
            rig.Body = Part(root, "body", Vector3.zero);
            rig.LegL = Part(rig.Body, "legL", new Vector3(-0.1f, 0.85f, 0));
            rig.LegR = Part(rig.Body, "legR", new Vector3(0.1f, 0.85f, 0));
            Mesh(rig.LegL, mb => mb.Box(dress, new Vector3(0, -0.42f, 0), new Vector3(0.15f, 0.85f, 0.18f)), layer);
            Mesh(rig.LegR, mb => mb.Box(dress, new Vector3(0, -0.42f, 0), new Vector3(0.15f, 0.85f, 0.18f)), layer);
            rig.Torso = Part(rig.Body, "torso", new Vector3(0, 0.85f, 0));
            Mesh(rig.Torso, mb =>
            {
                mb.Box(dress, new Vector3(0, 0.3f, 0), new Vector3(0.38f, 0.6f, 0.24f));
                mb.Box(cloak, new Vector3(0, 0.15f, -0.13f), new Vector3(0.52f, 1.0f, 0.08f));
                mb.Box(cloak, new Vector3(0, -0.45f, -0.05f), new Vector3(0.56f, 0.85f, 0.3f));
                mb.Box(cloak, new Vector3(0, 0.62f, -0.05f), new Vector3(0.5f, 0.12f, 0.3f));
            }, layer);
            rig.Head = Part(rig.Torso, "head", new Vector3(0, 0.68f, 0));
            Mesh(rig.Head, mb =>
            {
                mb.Box(skin, new Vector3(0, 0.13f, 0), new Vector3(0.21f, 0.26f, 0.23f));
                mb.Box(hair, new Vector3(0, 0.22f, -0.05f), new Vector3(0.25f, 0.16f, 0.22f));
                mb.Box(hair, new Vector3(0, 0.02f, -0.12f), new Vector3(0.24f, 0.32f, 0.06f));
                mb.Box(eyes, new Vector3(-0.05f, 0.15f, 0.115f), new Vector3(0.04f, 0.025f, 0.01f));
                mb.Box(eyes, new Vector3(0.05f, 0.15f, 0.115f), new Vector3(0.04f, 0.025f, 0.01f));
            }, layer);
            rig.ArmL = Part(rig.Torso, "armL", new Vector3(-0.25f, 0.56f, 0));
            rig.ArmR = Part(rig.Torso, "armR", new Vector3(0.25f, 0.56f, 0));
            Mesh(rig.ArmL, mb => { mb.Box(cloak, new Vector3(0, -0.3f, 0), new Vector3(0.12f, 0.6f, 0.14f)); mb.Box(skin, new Vector3(0, -0.64f, 0), new Vector3(0.08f, 0.1f, 0.08f)); }, layer);
            Mesh(rig.ArmR, mb => { mb.Box(cloak, new Vector3(0, -0.3f, 0), new Vector3(0.12f, 0.6f, 0.14f)); mb.Box(skin, new Vector3(0, -0.64f, 0), new Vector3(0.08f, 0.1f, 0.08f)); }, layer);
            rig.Hand = Part(rig.ArmR, "hand", new Vector3(0, -0.7f, 0.05f));
            rig.Finish();
            return rig;
        }

        void Finish()
        {
            _renderers = GetComponentsInChildren<Renderer>();
            _phase = Random.value * 10f;
        }

        public void SetVisible(bool v)
        {
            if (_renderers == null) return;
            foreach (var r in _renderers) if (r) r.enabled = v;
        }

        /// <summary>Tint emission for state feedback (thrall violet, burn, hit flash). Color.black clears.</summary>
        public void SetGlow(Color c)
        {
            if (_renderers == null) return;
            _mpb ??= new MaterialPropertyBlock();
            foreach (var r in _renderers)
            {
                if (!r) continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor(EmissionId, c);
                r.SetPropertyBlock(_mpb);
            }
        }

        public void ClearGlow()
        {
            if (_renderers == null) return;
            foreach (var r in _renderers) if (r) r.SetPropertyBlock(null);
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0) return;
            bool lying = Pose == Pose.Lying || Pose == Pose.Carried || Pose == Pose.Sleep;
            _lie = Mathf.MoveTowards(_lie, lying ? 1f : 0f, dt * 3f);

            float swing = 0, armSwing = 0, lean = 0;
            float rate = Quadruped ? 5f : 3.2f;
            switch (Pose)
            {
                case Pose.Walk: swing = 28; armSwing = 22; _phase += dt * Mathf.Max(1.2f, Speed) * rate; break;
                case Pose.Sneak: swing = 20; armSwing = 8; lean = 18; _phase += dt * Mathf.Max(1.2f, Speed) * rate; break;
                case Pose.Run: swing = 45; armSwing = 45; lean = 12; _phase += dt * Mathf.Max(2f, Speed) * rate * 0.8f; break;
                case Pose.Panic: swing = 45; armSwing = 70; lean = 6; _phase += dt * Mathf.Max(2f, Speed) * rate * 0.8f; break;
                case Pose.Climb: swing = 30; armSwing = 0; _phase += dt * 8f; break;
                default: _phase += dt; break;
            }
            float s = Mathf.Sin(_phase);
            if (LegL) LegL.localRotation = Quaternion.Euler(s * swing, 0, 0);
            if (LegR) LegR.localRotation = Quaternion.Euler(-s * swing, 0, 0);

            if (Quadruped)
            {
                if (ArmL) ArmL.localRotation = Quaternion.Euler(-s * swing, 0, 0);
                if (ArmR) ArmR.localRotation = Quaternion.Euler(s * swing, 0, 0);
                if (Head) Head.localRotation = Quaternion.Euler(Pose == Pose.Sneak ? 20 : Pose == Pose.Aim ? -10 : 0, 0, 0);
            }
            else
            {
                Quaternion la, ra;
                switch (Pose)
                {
                    case Pose.Climb:
                        la = Quaternion.Euler(-160 + s * 25, 0, 0); ra = Quaternion.Euler(-160 - s * 25, 0, 0); break;
                    case Pose.Feed:
                        la = Quaternion.Euler(-70, 20, 0); ra = Quaternion.Euler(-70, -20, 0); lean = 25; break;
                    case Pose.Victim:
                        la = Quaternion.Euler(-30, 0, -20); ra = Quaternion.Euler(-30, 0, 20); lean = -20; break;
                    case Pose.Aim:
                        la = Quaternion.Euler(-80, 15, 0); ra = Quaternion.Euler(-85, 0, 0); break;
                    case Pose.Interact:
                        la = Quaternion.Euler(-60 + s * 10, 0, 0); ra = Quaternion.Euler(-60 - s * 10, 0, 0); break;
                    case Pose.Mesmerised:
                        la = Quaternion.Euler(0, 0, -6); ra = Quaternion.Euler(0, 0, 6); lean = -6; break;
                    case Pose.Panic:
                        la = Quaternion.Euler(-120 + s * armSwing * 0.3f, 0, -20); ra = Quaternion.Euler(-120 - s * armSwing * 0.3f, 0, 20); break;
                    case Pose.Sit:
                        la = Quaternion.Euler(-20, 0, 0); ra = Quaternion.Euler(-20, 0, 0); break;
                    default:
                        la = Quaternion.Euler(-s * armSwing, 0, 0); ra = Quaternion.Euler(s * armSwing, 0, 0); break;
                }
                if (ArmL) ArmL.localRotation = la;
                if (ArmR) ArmR.localRotation = ra;
                if (Pose == Pose.Sit)
                {
                    if (LegL) LegL.localRotation = Quaternion.Euler(-80, 0, 0);
                    if (LegR) LegR.localRotation = Quaternion.Euler(-80, 0, 0);
                }
                if (Torso) Torso.localRotation = Quaternion.Euler(lean, 0, 0);
                if (Head) Head.localRotation = Quaternion.Euler(Pose == Pose.Mesmerised ? 10 : 0, 0, 0);
            }
            _bob = (Pose == Pose.Walk || Pose == Pose.Run || Pose == Pose.Sneak) ? Mathf.Abs(Mathf.Cos(_phase)) * 0.04f : 0f;
            float sit = Pose == Pose.Sit ? -0.45f : 0f;
            float crouch = Pose == Pose.Sneak && !Quadruped ? -0.12f : 0f;
            if (Body)
            {
                Body.localPosition = new Vector3(0, _bob + sit + crouch + _lie * (Quadruped ? 0.05f : 0.15f), 0);
                Body.localRotation = Quaternion.Euler(0, 0, _lie * 90f * (Quadruped ? 1 : 0)) * Quaternion.Euler(_lie * (Quadruped ? 0 : -90f), 0, 0);
            }
        }
    }
}
