using UnityEngine;
using Vespertine.Core;

namespace Vespertine.Visual
{
    public struct PropInfo
    {
        public bool Blocks;      // navmesh obstacle
        public Vector3 Size;     // footprint (x,z) and height (y), local
        public bool CanHide;     // a body (or Ilse) can be hidden inside by default
        public bool BlocksVision;
    }

    /// <summary>Builds low-poly prop models from boxes and cylinders.</summary>
    public static class PropFactory
    {
        static Material Wood => Mats.Lit("p_wood", Util.Hex("#5e4430"), "planks", 0.15f);
        static Material DarkWood => Mats.Lit("p_dwood", Util.Hex("#3a2a20"), "planks", 0.2f);
        static Material Stone => Mats.Lit("p_stone", Util.Hex("#6a6870"), "stone", 0.1f);
        static Material Iron => Mats.Lit("p_iron", Util.Hex("#202126"), null, 0.5f, 0.7f);
        static Material Cloth => Mats.Lit("p_cloth", Util.Hex("#b8b2a2"), null, 0.05f);
        static Material Dirty => Mats.Lit("p_dirty", Util.Hex("#7a7262"), null, 0.05f);
        static Material Shroud => Mats.Lit("p_shroud", Util.Hex("#a7a294"), null, 0.04f);
        static Material Red => Mats.Lit("p_red", Util.Hex("#5a1a20"), "carpet", 0.1f);
        static Material Paper => Mats.Lit("p_paper", Util.Hex("#d8ccb0"), null, 0.05f);
        static Material Leaf => Mats.Lit("p_leaf", Util.Hex("#2a3a26"), "grass", 0.05f);

        /// <summary>A body laid out under a sheet, head toward -z, on a surface at <paramref name="top"/> (the Ward C dead, D119).</summary>
        static void ShroudedBody(MeshBuilder mb, float top, System.Random rng)
        {
            float lean = (float)(rng.NextDouble() * 6 - 3);
            mb.Box(Shroud, new Vector3(0, top + 0.03f, 0.05f), new Vector3(0.78f, 0.06f, 1.85f), lean);       // the sheet's skirt
            mb.Box(Shroud, new Vector3(0, top + 0.16f, -0.62f), new Vector3(0.26f, 0.22f, 0.28f), lean);      // head
            mb.Box(Shroud, new Vector3(0, top + 0.15f, -0.2f), new Vector3(0.5f, 0.22f, 0.55f), lean);        // chest
            mb.Box(Shroud, new Vector3(0, top + 0.12f, 0.3f), new Vector3(0.42f, 0.16f, 0.6f), lean);         // hips and thighs
            mb.Box(Shroud, new Vector3(-0.1f, top + 0.1f, 0.78f), new Vector3(0.14f, 0.12f, 0.4f), lean);     // shins
            mb.Box(Shroud, new Vector3(0.1f, top + 0.1f, 0.78f), new Vector3(0.14f, 0.12f, 0.4f), lean);
            mb.Box(Shroud, new Vector3(-0.1f, top + 0.18f, 0.95f), new Vector3(0.1f, 0.14f, 0.08f), lean);    // toes tenting the sheet
            mb.Box(Shroud, new Vector3(0.1f, top + 0.18f, 0.95f), new Vector3(0.1f, 0.14f, 0.08f), lean);
        }

        public static PropInfo Info(string type)
        {
            switch (type)
            {
                case "crate": return new PropInfo { Blocks = true, Size = new Vector3(1.1f, 1.1f, 1.1f), CanHide = true };
                case "barrel": return new PropInfo { Blocks = true, Size = new Vector3(0.8f, 1.1f, 0.8f), CanHide = true };
                case "well": return new PropInfo { Blocks = true, Size = new Vector3(1.6f, 1f, 1.6f), CanHide = true };
                case "bed": case "bed_shroud": return new PropInfo { Blocks = true, Size = new Vector3(1f, 0.6f, 2f) };
                case "slab": case "gurney": case "slab_shroud": case "slab_open": case "gurney_shroud": return new PropInfo { Blocks = true, Size = new Vector3(0.9f, 0.9f, 2f) };
                case "table": case "desk": return new PropInfo { Blocks = true, Size = new Vector3(1.6f, 0.8f, 0.9f) };
                case "shelf": case "bookshelf": case "cabinet": return new PropInfo { Blocks = true, Size = new Vector3(1.8f, 2.2f, 0.5f), BlocksVision = true };
                case "wardrobe": return new PropInfo { Blocks = true, Size = new Vector3(1.4f, 2.2f, 0.7f), CanHide = true, BlocksVision = true };
                case "cart": return new PropInfo { Blocks = true, Size = new Vector3(1.4f, 1.2f, 2.4f), CanHide = true };
                case "privy": return new PropInfo { Blocks = true, Size = new Vector3(1.3f, 2.3f, 1.3f), CanHide = true, BlocksVision = true };
                case "coffin": return new PropInfo { Blocks = true, Size = new Vector3(0.8f, 0.6f, 2f), CanHide = true };
                case "tub": return new PropInfo { Blocks = true, Size = new Vector3(1f, 0.8f, 1.8f) };
                case "pew": case "bench": return new PropInfo { Blocks = true, Size = new Vector3(3f, 0.9f, 0.6f) };
                case "altar": return new PropInfo { Blocks = true, Size = new Vector3(2f, 1.1f, 1f) };
                case "pillar": return new PropInfo { Blocks = true, Size = new Vector3(0.9f, 4f, 0.9f), BlocksVision = true };
                case "statue": return new PropInfo { Blocks = true, Size = new Vector3(1f, 2.6f, 1f), BlocksVision = true };
                case "tree": return new PropInfo { Blocks = true, Size = new Vector3(0.6f, 5f, 0.6f) };
                case "sacks": return new PropInfo { Blocks = true, Size = new Vector3(1.4f, 0.8f, 1f) };
                case "cage": return new PropInfo { Blocks = true, Size = new Vector3(1.8f, 2.2f, 1.8f) };
                case "chest": return new PropInfo { Blocks = true, Size = new Vector3(1f, 0.7f, 0.6f) };
                case "gravestone": return new PropInfo { Blocks = true, Size = new Vector3(0.8f, 1.1f, 0.3f) };
                case "fountain": return new PropInfo { Blocks = true, Size = new Vector3(3f, 1.2f, 3f) };
                case "boat": return new PropInfo { Blocks = false, Size = new Vector3(1.6f, 0.6f, 4f), CanHide = true };
                case "washline": return new PropInfo { Blocks = false, Size = new Vector3(4f, 2.4f, 0.2f) };
                case "chair": return new PropInfo { Blocks = false, Size = new Vector3(0.5f, 1f, 0.5f) };
                case "body_pile": return new PropInfo { Blocks = true, Size = new Vector3(1.8f, 0.6f, 1.8f) };
                case "rug": return new PropInfo { Blocks = false, Size = new Vector3(3f, 0.02f, 2f) };
                case "lampstand": return new PropInfo { Blocks = true, Size = new Vector3(0.4f, 2f, 0.4f) };
                case "pipes": return new PropInfo { Blocks = true, Size = new Vector3(1.8f, 1.6f, 0.8f) };
                case "machine": return new PropInfo { Blocks = true, Size = new Vector3(2.4f, 2.2f, 1.6f), BlocksVision = true };
                // the opera (M11)
                case "seats": return new PropInfo { Blocks = false, Size = new Vector3(3f, 1f, 0.7f) };
                case "curtain": return new PropInfo { Blocks = false, Size = new Vector3(12f, 7f, 0.6f) };
                case "scenery": return new PropInfo { Blocks = true, Size = new Vector3(4f, 3.6f, 0.4f), BlocksVision = true };
                case "balustrade": return new PropInfo { Blocks = false, Size = new Vector3(2f, 1f, 0.2f) };
                // the finale (M14)
                case "abbess": return new PropInfo { Blocks = true, Size = new Vector3(1.4f, 1.6f, 1.4f) };
                case "vat": return new PropInfo { Blocks = true, Size = new Vector3(1.8f, 1.6f, 1.8f), BlocksVision = true };
                case "still": return new PropInfo { Blocks = true, Size = new Vector3(1.4f, 2.6f, 1.4f) };
                case "bricks": return new PropInfo { Blocks = false, Size = new Vector3(1.6f, 0.5f, 1.2f) };
                // dressing over a disc of tower cells (the cells do the blocking): placed at its centre, on top
                case "gasholder":
                case "gasholder_wreck": return new PropInfo { Blocks = false, Size = Vector3.one };
            }
            return new PropInfo { Blocks = true, Size = new Vector3(1, 1, 1) };
        }

        public static GameObject Build(string type, Transform parent, Vector3 pos, float yaw, int seed)
        {
            var go = new GameObject("prop_" + type);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0, yaw, 0);
            var mb = new MeshBuilder();
            var rng = new System.Random(seed);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            switch (type)
            {
                case "crate":
                    mb.Box(Wood, new Vector3(0, 0.55f, 0), new Vector3(1.1f, 1.1f, 1.1f), R(-8, 8));
                    break;
                case "barrel":
                    mb.Cylinder(Wood, Vector3.zero, 0.4f, 1.1f, 10);
                    mb.Cylinder(Iron, new Vector3(0, 0.2f, 0), 0.42f, 0.08f, 10, false);
                    mb.Cylinder(Iron, new Vector3(0, 0.85f, 0), 0.42f, 0.08f, 10, false);
                    break;
                case "well":
                    mb.Cylinder(Stone, Vector3.zero, 0.8f, 0.9f, 10);
                    mb.Cylinder(Mats.Lit("p_black", Util.Hex("#050508")), new Vector3(0, 0.91f, 0), 0.6f, 0.01f, 10);
                    mb.Box(Wood, new Vector3(-0.7f, 1.4f, 0), new Vector3(0.1f, 1.2f, 0.1f));
                    mb.Box(Wood, new Vector3(0.7f, 1.4f, 0), new Vector3(0.1f, 1.2f, 0.1f));
                    mb.Box(Wood, new Vector3(0, 1.95f, 0), new Vector3(1.6f, 0.1f, 0.1f));
                    break;
                case "bed":
                case "bed_shroud":
                    mb.Box(Iron, new Vector3(0, 0.25f, 0), new Vector3(0.95f, 0.5f, 1.95f));
                    mb.Box(Cloth, new Vector3(0, 0.55f, 0.1f), new Vector3(0.9f, 0.12f, 1.7f));
                    mb.Box(Cloth, new Vector3(0, 0.62f, -0.75f), new Vector3(0.6f, 0.12f, 0.3f));
                    if (type == "bed_shroud") ShroudedBody(mb, 0.61f, rng);
                    break;
                case "slab":
                case "slab_shroud":
                case "slab_open":
                    mb.Box(Stone, new Vector3(0, 0.45f, 0), new Vector3(0.9f, 0.9f, 2f));
                    if (type == "slab_shroud") ShroudedBody(mb, 0.9f, rng);
                    else if (type == "slab_open")
                    {
                        // hers: the sheet thrown back off the head into a heap at the foot, a dark smear where she lay
                        mb.Box(Mats.Lit("p_stain", Util.Hex("#2a1414"), null, 0.3f), new Vector3(0, 0.905f, -0.35f), new Vector3(0.45f, 0.01f, 0.9f));
                        mb.Box(Shroud, new Vector3(0.05f, 0.98f, 0.6f), new Vector3(0.86f, 0.16f, 0.7f), R(-8, 8));
                        mb.Box(Shroud, new Vector3(-0.1f, 1.08f, 0.55f), new Vector3(0.55f, 0.1f, 0.45f), R(10, 30));
                        mb.Box(Shroud, new Vector3(0.38f, 0.6f, 0.62f), new Vector3(0.06f, 0.6f, 0.6f));   // hanging over the edge
                    }
                    break;
                case "gurney":
                case "gurney_shroud":
                    mb.Box(Iron, new Vector3(0, 0.75f, 0), new Vector3(0.8f, 0.08f, 1.9f));
                    mb.Box(Dirty, new Vector3(0, 0.82f, 0), new Vector3(0.75f, 0.06f, 1.8f));
                    for (int i = 0; i < 4; i++) mb.Box(Iron, new Vector3(i % 2 == 0 ? -0.35f : 0.35f, 0.37f, i < 2 ? -0.85f : 0.85f), new Vector3(0.05f, 0.75f, 0.05f));
                    if (type == "gurney_shroud") ShroudedBody(mb, 0.85f, rng);
                    break;
                case "table":
                case "desk":
                    mb.Box(type == "desk" ? DarkWood : Wood, new Vector3(0, 0.76f, 0), new Vector3(1.6f, 0.08f, 0.9f));
                    for (int i = 0; i < 4; i++) mb.Box(DarkWood, new Vector3(i % 2 == 0 ? -0.72f : 0.72f, 0.36f, i < 2 ? -0.38f : 0.38f), new Vector3(0.08f, 0.72f, 0.08f));
                    if (type == "desk") mb.Box(Paper, new Vector3(0.2f, 0.81f, 0), new Vector3(0.4f, 0.02f, 0.3f), R(-20, 20));
                    else if (rng.NextDouble() < 0.6) mb.Cylinder(Mats.Lit("p_bottle", Util.Hex("#1a3a2a"), null, 0.9f), new Vector3(R(-0.5f, 0.5f), 0.8f, R(-0.2f, 0.2f)), 0.05f, 0.3f, 6);
                    break;
                case "shelf":
                case "bookshelf":
                case "cabinet":
                    mb.Box(DarkWood, new Vector3(0, 1.1f, 0), new Vector3(1.8f, 2.2f, 0.5f));
                    if (type != "cabinet")
                        for (int s = 0; s < 4; s++)
                            for (int b = 0; b < 6; b++)
                                if (rng.NextDouble() < 0.75)
                                    mb.Box(Mats.Lit("p_book" + (b % 3), b % 3 == 0 ? Util.Hex("#5a2020") : b % 3 == 1 ? Util.Hex("#203a5a") : Util.Hex("#4a4020")),
                                        new Vector3(-0.7f + b * 0.27f, 0.35f + s * 0.5f, 0.27f), new Vector3(0.2f, 0.34f, 0.08f));
                    break;
                case "wardrobe":
                    mb.Box(DarkWood, new Vector3(0, 1.1f, 0), new Vector3(1.4f, 2.2f, 0.7f));
                    mb.Box(Wood, new Vector3(0, 1.1f, 0.36f), new Vector3(0.02f, 2f, 0.02f));
                    break;
                case "cart":
                    mb.Box(Wood, new Vector3(0, 0.75f, 0), new Vector3(1.3f, 0.5f, 2.2f));
                    mb.Cylinder(DarkWood, new Vector3(-0.7f, 0, -0.5f), 0.45f, 0.1f, 10);
                    mb.Cylinder(DarkWood, new Vector3(0.7f, 0, -0.5f), 0.45f, 0.1f, 10);
                    mb.Box(Wood, new Vector3(0, 0.6f, 1.6f), new Vector3(0.1f, 0.1f, 1.2f));
                    break;
                case "privy":
                    mb.Box(Wood, new Vector3(0, 1.1f, 0), new Vector3(1.3f, 2.2f, 1.3f));
                    mb.Box(DarkWood, new Vector3(0, 2.3f, 0), new Vector3(1.5f, 0.15f, 1.5f), 0, false);
                    break;
                case "coffin":
                    mb.Box(DarkWood, new Vector3(0, 0.3f, 0), new Vector3(0.8f, 0.6f, 2f));
                    break;
                case "tub":
                    mb.Box(Mats.Lit("p_enamel", Util.Hex("#a8a8a0"), null, 0.7f), new Vector3(0, 0.4f, 0), new Vector3(1f, 0.8f, 1.8f));
                    mb.Box(Mats.Lit("p_tubwater", Util.Hex("#2a3a3a"), null, 0.9f), new Vector3(0, 0.7f, 0), new Vector3(0.85f, 0.05f, 1.65f));
                    break;
                case "pew":
                case "bench":
                    mb.Box(DarkWood, new Vector3(0, 0.45f, 0), new Vector3(3f, 0.08f, 0.5f));
                    if (type == "pew") mb.Box(DarkWood, new Vector3(0, 0.8f, -0.25f), new Vector3(3f, 0.7f, 0.06f));
                    mb.Box(DarkWood, new Vector3(-1.4f, 0.22f, 0), new Vector3(0.08f, 0.45f, 0.45f));
                    mb.Box(DarkWood, new Vector3(1.4f, 0.22f, 0), new Vector3(0.08f, 0.45f, 0.45f));
                    break;
                case "altar":
                    mb.Box(Stone, new Vector3(0, 0.55f, 0), new Vector3(2f, 1.1f, 1f));
                    mb.Box(Mats.Lit("p_altarcloth", Util.Hex("#d8d0b8"), null, 0.05f), new Vector3(0, 1.11f, 0), new Vector3(2.1f, 0.02f, 0.8f));
                    break;
                case "pillar":
                    mb.Cylinder(Stone, Vector3.zero, 0.45f, 4f, 8);
                    mb.Box(Stone, new Vector3(0, 0.15f, 0), new Vector3(1f, 0.3f, 1f));
                    break;
                case "statue":
                    mb.Box(Stone, new Vector3(0, 0.4f, 0), new Vector3(1f, 0.8f, 1f));
                    mb.Cylinder(Stone, new Vector3(0, 0.8f, 0), 0.3f, 1.3f, 6);
                    mb.Box(Stone, new Vector3(0, 2.3f, 0), new Vector3(0.35f, 0.4f, 0.35f));
                    break;
                case "tree":
                    mb.Cylinder(DarkWood, Vector3.zero, 0.25f, 3f, 6);
                    for (int i = 0; i < 4; i++) mb.Box(Leaf, new Vector3(R(-0.8f, 0.8f), R(3f, 4.6f), R(-0.8f, 0.8f)), Vector3.one * R(1.4f, 2.2f), R(0, 90));
                    break;
                case "sacks":
                    for (int i = 0; i < 3; i++) mb.Box(Mats.Lit("p_sack", Util.Hex("#7a6a4a"), null, 0.02f), new Vector3(-0.4f + i * 0.4f, 0.3f, R(-0.2f, 0.2f)), new Vector3(0.5f, 0.6f, 0.7f), R(-20, 20));
                    break;
                case "cage":
                    for (int i = 0; i < 8; i++)
                    {
                        float a = i / 8f * Mathf.PI * 2;
                        mb.Box(Iron, new Vector3(Mathf.Cos(a) * 0.85f, 1.1f, Mathf.Sin(a) * 0.85f), new Vector3(0.06f, 2.2f, 0.06f));
                    }
                    mb.Cylinder(Iron, new Vector3(0, 2.15f, 0), 0.9f, 0.08f, 8);
                    break;
                case "chest":
                    mb.Box(DarkWood, new Vector3(0, 0.35f, 0), new Vector3(1f, 0.7f, 0.6f));
                    mb.Box(Iron, new Vector3(0, 0.5f, 0.31f), new Vector3(0.15f, 0.15f, 0.02f));
                    break;
                case "gravestone":
                    mb.Box(Stone, new Vector3(0, 0.55f, 0), new Vector3(0.8f, 1.1f, 0.25f), R(-6, 6));
                    break;
                case "fountain":
                    mb.Cylinder(Stone, Vector3.zero, 1.5f, 0.6f, 12);
                    mb.Cylinder(Mats.Lit("t_shallow", Util.Hex("#1d2a35"), "water", 0.85f), new Vector3(0, 0.55f, 0), 1.35f, 0.02f, 12);
                    mb.Cylinder(Stone, Vector3.zero, 0.3f, 1.6f, 8);
                    break;
                case "boat":
                    mb.Box(DarkWood, new Vector3(0, 0.1f, 0), new Vector3(1.4f, 0.4f, 3.6f));
                    mb.Box(DarkWood, new Vector3(0, 0.1f, 1.95f), new Vector3(0.8f, 0.4f, 0.4f));
                    mb.Box(Wood, new Vector3(0, 0.25f, 0.4f), new Vector3(1.3f, 0.06f, 0.3f));
                    break;
                case "washline":
                    mb.Box(DarkWood, new Vector3(-2f, 1.2f, 0), new Vector3(0.1f, 2.4f, 0.1f));
                    mb.Box(DarkWood, new Vector3(2f, 1.2f, 0), new Vector3(0.1f, 2.4f, 0.1f));
                    for (int i = 0; i < 4; i++) mb.Box(Mats.Lit("p_wash" + (i % 2), i % 2 == 0 ? Util.Hex("#a8a090") : Util.Hex("#6a7080")), new Vector3(-1.4f + i * 0.9f, 1.85f, 0), new Vector3(0.6f, 0.8f, 0.02f));
                    break;
                case "chair":
                    mb.Box(Wood, new Vector3(0, 0.45f, 0), new Vector3(0.45f, 0.06f, 0.45f));
                    mb.Box(Wood, new Vector3(0, 0.75f, -0.2f), new Vector3(0.45f, 0.6f, 0.05f));
                    for (int i = 0; i < 4; i++) mb.Box(Wood, new Vector3(i % 2 == 0 ? -0.2f : 0.2f, 0.22f, i < 2 ? -0.2f : 0.2f), new Vector3(0.04f, 0.45f, 0.04f));
                    break;
                case "body_pile":
                    for (int i = 0; i < 4; i++) mb.Box(Dirty, new Vector3(R(-0.5f, 0.5f), 0.15f + i * 0.1f, R(-0.5f, 0.5f)), new Vector3(0.45f, 0.25f, 1.6f), R(0, 180));
                    break;
                case "rug":
                    mb.Box(Red, new Vector3(0, 0.01f, 0), new Vector3(3f, 0.02f, 2f));
                    break;
                case "lampstand":
                    mb.Cylinder(Iron, Vector3.zero, 0.08f, 2f, 6);
                    break;
                case "pipes":
                    for (int i = 0; i < 3; i++) mb.Cylinder(Mats.Lit("p_copper", Util.Hex("#6a4a30"), null, 0.6f, 0.8f), new Vector3(-0.6f + i * 0.6f, 0, 0), 0.22f, 1.6f, 8);
                    break;
                case "machine":
                    mb.Box(Iron, new Vector3(0, 1.1f, 0), new Vector3(2.4f, 2.2f, 1.6f));
                    mb.Cylinder(Mats.Lit("p_copper", Util.Hex("#6a4a30"), null, 0.6f, 0.8f), new Vector3(0.6f, 2.2f, 0), 0.3f, 1.2f, 8);
                    break;
                case "abbess":
                {
                    // the Abbess: kneeling, wasted, grey robe gone to rags, hair to the floor, chained to four rings
                    var skin = Mats.Lit("p_abbess_skin", Util.Hex("#b8b4bc"), null, 0.35f);
                    var rag = Mats.Lit("p_abbess_rag", Util.Hex("#2e2a30"), null, 0.05f);
                    var hair = Mats.Lit("p_abbess_hair", Util.Hex("#0c0b0e"), null, 0.2f);
                    mb.Cylinder(rag, Vector3.zero, 0.5f, 0.35f, 10);                                   // knees, robe spread
                    mb.Box(rag, new Vector3(0, 0.62f, -0.05f), new Vector3(0.48f, 0.6f, 0.3f), 0f);    // torso, bowed
                    mb.Box(skin, new Vector3(0, 1.02f, 0.08f), new Vector3(0.22f, 0.26f, 0.24f));      // head, hanging
                    mb.Box(hair, new Vector3(0, 0.72f, 0.14f), new Vector3(0.3f, 0.62f, 0.1f));        // hair over the face
                    for (int i = 0; i < 2; i++)
                    {
                        float sx = i == 0 ? -1f : 1f;
                        for (int k = 0; k < 3; k++)                                                      // arms drawn out and down by the chains
                            mb.Box(skin, new Vector3(sx * (0.3f + 0.12f * k), 0.82f - 0.07f * k, 0.05f), new Vector3(0.14f, 0.08f, 0.08f));
                    }
                    for (int i = 0; i < 4; i++)
                    {
                        float sx = i % 2 == 0 ? -1f : 1f, sz = i < 2 ? -1f : 1f;
                        var from = new Vector3(sx * 0.58f, i < 2 ? 0.68f : 0.2f, sz * 0.1f);
                        var to = new Vector3(sx * 1.6f, 0.05f, sz * 1.1f);
                        int links = 7;
                        for (int k = 0; k < links; k++)
                        {
                            var c = Vector3.Lerp(from, to, (k + 0.5f) / links);
                            mb.Box(Iron, c, new Vector3(0.07f, 0.07f, 0.16f), Mathf.Atan2(to.x - from.x, to.z - from.z) * Mathf.Rad2Deg + (k % 2) * 90f);
                        }
                        mb.Cylinder(Iron, to - Vector3.up * 0.05f, 0.14f, 0.06f, 8);                   // the floor ring
                    }
                    break;
                }
                case "vat":
                {
                    var copper = Mats.Lit("p_copper", Util.Hex("#6a4a30"), null, 0.6f, 0.8f);
                    mb.Cylinder(copper, Vector3.zero, 0.9f, 1.5f, 12);
                    mb.Cylinder(Iron, new Vector3(0, 0.3f, 0), 0.92f, 0.06f, 12, false);
                    mb.Cylinder(Iron, new Vector3(0, 1.2f, 0), 0.92f, 0.06f, 12, false);
                    // the eternal vitae: black-red, faintly lit from within
                    mb.Cylinder(Mats.Lit("p_vitae", Util.Hex("#1a0206"), null, 0.95f, 0f, Util.Hex("#3a0008")), new Vector3(0, 1.5f, 0), 0.82f, 0.02f, 12);
                    mb.Cylinder(copper, new Vector3(0.95f, 0.2f, 0), 0.08f, 0.5f, 6);
                    break;
                }
                case "still":
                {
                    var copper = Mats.Lit("p_copper", Util.Hex("#6a4a30"), null, 0.6f, 0.8f);
                    var glass = Mats.Lit("p_still_glass", Util.Hex("#3a1418"), null, 0.95f, 0f, Util.Hex("#200004"));
                    mb.Cylinder(Iron, Vector3.zero, 0.6f, 0.5f, 10);                                  // the firebox
                    mb.Cylinder(copper, new Vector3(0, 0.5f, 0), 0.55f, 0.8f, 10);                     // the pot
                    mb.Cylinder(copper, new Vector3(0, 1.3f, 0), 0.14f, 1.2f, 8);                      // the column
                    for (int i = 0; i < 5; i++) mb.Cylinder(copper, new Vector3(0.45f, 0.55f + i * 0.35f, 0.3f), 0.18f, 0.06f, 8, false);   // the coil
                    mb.Cylinder(glass, new Vector3(0.55f, 0f, -0.45f), 0.2f, 0.45f, 8);                 // the receiving flask
                    break;
                }
                case "bricks":
                    for (int i = 0; i < 9; i++) mb.Box(Mats.Lit("p_brick", Util.Hex("#5a3a30"), null, 0.05f), new Vector3(R(-0.7f, 0.7f), 0.1f + (i % 3) * 0.12f, R(-0.5f, 0.5f)), new Vector3(0.4f, 0.12f, 0.2f), R(0, 180));
                    break;
                case "seats":
                {
                    // a row of four velvet stalls seats with gilt arms
                    var velvet = Mats.Lit("p_velvet", Util.Hex("#6a1420"), "carpet", 0.12f);
                    var gilt = Mats.Lit("p_gilt", Util.Hex("#8a6a2a"), null, 0.6f, 0.8f);
                    for (int i = 0; i < 4; i++)
                    {
                        float x = -1.125f + i * 0.75f;
                        mb.Box(velvet, new Vector3(x, 0.42f, 0.05f), new Vector3(0.62f, 0.14f, 0.5f));
                        mb.Box(velvet, new Vector3(x, 0.78f, -0.24f), new Vector3(0.62f, 0.7f, 0.1f), 0f);
                    }
                    for (int i = 0; i < 5; i++) mb.Box(gilt, new Vector3(-1.5f + i * 0.75f, 0.33f, 0f), new Vector3(0.06f, 0.66f, 0.6f));
                    break;
                }
                case "curtain":
                {
                    // proscenium drapes: two gathered swags either side of a 12 m opening and a fringed valance
                    var a = Mats.Lit("p_drape", Util.Hex("#5c0f18"), "carpet", 0.15f);
                    var b = Mats.Lit("p_drape2", Util.Hex("#400a12"), "carpet", 0.15f);
                    var gilt = Mats.Lit("p_gilt", Util.Hex("#8a6a2a"), null, 0.6f, 0.8f);
                    foreach (float side in new[] { -1f, 1f })
                        for (int i = 0; i < 6; i++)
                        {
                            float x = side * (6.3f - i * 0.28f), hgt = 7f - i * 0.25f;
                            mb.Box(i % 2 == 0 ? a : b, new Vector3(x, hgt * 0.5f, R(-0.1f, 0.1f)), new Vector3(0.34f, hgt, 0.22f), R(-2f, 2f));
                        }
                    for (int i = 0; i < 24; i++)
                        mb.Box(i % 2 == 0 ? a : b, new Vector3(-6.4f + i * 0.555f, 6.6f - (i % 3) * 0.12f, 0f), new Vector3(0.58f, 1.0f + (i % 3) * 0.24f, 0.2f));
                    mb.Box(gilt, new Vector3(0, 7.15f, 0f), new Vector3(13.2f, 0.18f, 0.3f));
                    break;
                }
                case "scenery":
                {
                    // a painted flat: a stage castle's wall and tower against a night sky, braced from behind
                    var canvas = Mats.Lit("p_flat", Util.Hex("#2a3040"), null, 0.05f);
                    var paint = Mats.Lit("p_flatpaint", Util.Hex("#4a4a52"), null, 0.05f);
                    mb.Box(canvas, new Vector3(0, 1.8f, 0), new Vector3(4f, 3.6f, 0.08f));
                    mb.Box(paint, new Vector3(R(-0.8f, 0.8f), 1.3f, 0.06f), new Vector3(2.4f, 2.6f, 0.02f));
                    for (int i = 0; i < 4; i++) mb.Box(paint, new Vector3(-1.5f + i * 1f, 2.75f, 0.06f), new Vector3(0.5f, 0.35f, 0.02f));
                    mb.Box(Wood, new Vector3(-1.5f, 1.2f, -0.6f), new Vector3(0.08f, 2.4f, 0.08f), 0f);
                    mb.Box(Wood, new Vector3(1.5f, 1.2f, -0.6f), new Vector3(0.08f, 2.4f, 0.08f), 0f);
                    break;
                }
                case "balustrade":
                {
                    // a box front: velvet ledge on gilt balusters
                    var velvet = Mats.Lit("p_velvet", Util.Hex("#6a1420"), "carpet", 0.12f);
                    var gilt = Mats.Lit("p_gilt", Util.Hex("#8a6a2a"), null, 0.6f, 0.8f);
                    for (int i = 0; i < 7; i++) mb.Cylinder(gilt, new Vector3(-0.9f + i * 0.3f, 0f, 0f), 0.05f, 0.85f, 6);
                    mb.Box(velvet, new Vector3(0, 0.9f, 0f), new Vector3(2f, 0.12f, 0.24f));
                    mb.Box(gilt, new Vector3(0, 0.05f, 0f), new Vector3(2f, 0.1f, 0.2f));
                    break;
                }
                case "gasholder":
                {
                    // a gas holder's iron guide frame round its brick tank, and the bell's crown plates on top.
                    // The prop sits on the tank's top (9 m): the columns run down to the ground and up past it.
                    var frame = Mats.Lit("p_holder", Util.Hex("#2a2f2c"), null, 0.45f, 0.7f);
                    var plate = Mats.Lit("p_bell", Util.Hex("#343b37"), null, 0.35f, 0.6f);
                    const int cols = 12;
                    const float rc = 10.7f;
                    for (int i = 0; i < cols; i++)
                    {
                        float a0 = i * Mathf.PI * 2f / cols, a1 = (i + 1) * Mathf.PI * 2f / cols;
                        var p0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)) * rc;
                        var p1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1)) * rc;
                        mb.Cylinder(frame, p0 + Vector3.down * 9f, 0.32f, 15.5f, 8);
                        float chord = (p1 - p0).magnitude, bay = -Mathf.Atan2(p1.z - p0.z, p1.x - p0.x) * Mathf.Rad2Deg;
                        var mid = (p0 + p1) * 0.5f;
                        foreach (float h in new[] { 0.2f, 3.4f, 6.4f })
                            mb.Box(frame, mid + Vector3.up * h, new Vector3(chord, 0.28f, 0.22f), bay);
                        // a cross-brace in each bay above the tank
                        mb.Box(frame, mid + Vector3.up * 1.8f, new Vector3(0.14f, 3.6f, 0.14f), bay);
                    }
                    mb.Cylinder(plate, Vector3.zero, 9.7f, 0.12f, 32);
                    mb.Cylinder(plate, Vector3.zero, 3.2f, 0.5f, 20);
                    mb.Cylinder(Iron, new Vector3(0, 0.5f, 0), 1.1f, 0.4f, 12);
                    break;
                }
                case "gasholder_wreck":
                {
                    // the same frame after the bell has gone up: columns snapped and leaning outward, girders hanging,
                    // the crown blown off and lying in pieces, the tank top scorched black
                    var frame = Mats.Lit("p_holder_burnt", Util.Hex("#1c1d1b"), null, 0.3f, 0.6f);
                    var plate = Mats.Lit("p_bell_burnt", Util.Hex("#232421"), null, 0.2f, 0.5f);
                    var soot = Mats.Lit("p_soot", Util.Hex("#0a0908"), null, 0.05f);
                    var glow = Mats.Lit("p_ember", Util.Hex("#1a0804"), null, 0.2f, 0, Util.Hex("#ff4a12") * 0.2f);
                    const int cols = 12;
                    const float rc = 10.7f;
                    var tops = new Vector3[cols];
                    for (int i = 0; i < cols; i++)
                    {
                        float a = i * Mathf.PI * 2f / cols;
                        var radial = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                        var foot = radial * rc + Vector3.down * 9f;
                        // standing to the snap, then the broken stump leaning out
                        float snap = R(5f, 14f);
                        var brk = foot + Vector3.up * snap + radial * R(0f, 0.4f);
                        mb.Strut(frame, foot, brk, 0.5f);
                        float lean = R(0.3f, 1f);
                        var end = brk + (Vector3.up * (1f - lean) + radial * lean).normalized * R(1.5f, 5f);
                        mb.Strut(frame, brk, end, 0.44f, R(-20f, 20f));
                        tops[i] = brk;
                    }
                    for (int i = 0; i < cols; i++)
                    {
                        // the low girder survives; higher ones hang from whichever column still holds them
                        var p0 = tops[i]; var p1 = tops[(i + 1) % cols];
                        float a0 = i * Mathf.PI * 2f / cols, a1 = (i + 1) * Mathf.PI * 2f / cols;
                        var l0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)) * rc + Vector3.up * 0.2f;
                        var l1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1)) * rc + Vector3.up * 0.2f;
                        mb.Strut(frame, l0, l1, 0.3f);
                        if (rng.NextDouble() < 0.5)
                        {
                            float h = Mathf.Min(p0.y, 3.4f);
                            var hang = new Vector3(p0.x, h, p0.z);
                            mb.Strut(frame, hang, Vector3.Lerp(hang, new Vector3(p1.x, R(-6f, 0f), p1.z), R(0.4f, 0.9f)), 0.26f);
                        }
                    }
                    // scorched tank top, a burst ring where the crown was, and the crown's plates thrown about
                    mb.Cylinder(soot, Vector3.zero, 9.6f, 0.04f, 32);
                    for (int i = 0; i < 10; i++)
                    {
                        float a = R(0f, Mathf.PI * 2f), d = R(0f, 6.5f);
                        mb.Box(glow, new Vector3(Mathf.Cos(a) * d, 0.05f, Mathf.Sin(a) * d), new Vector3(R(0.3f, 1.1f), 0.04f, R(0.3f, 0.9f)), R(0f, 360f));
                    }
                    for (int i = 0; i < 14; i++)
                    {
                        float a = R(0f, Mathf.PI * 2f), d = R(1.5f, 4f);
                        var c = new Vector3(Mathf.Cos(a) * d, 0.05f, Mathf.Sin(a) * d);
                        var dir = new Vector3(Mathf.Cos(a), R(0.5f, 1.4f), Mathf.Sin(a));
                        mb.Strut(plate, c, c + dir * R(0.8f, 1.6f), 0.12f, R(0f, 90f));
                    }
                    for (int i = 0; i < 9; i++)
                    {
                        float a = R(0f, Mathf.PI * 2f), d = R(3f, 9f);
                        mb.Box(plate, new Vector3(Mathf.Cos(a) * d, 0.08f, Mathf.Sin(a) * d), new Vector3(R(0.8f, 2.4f), 0.1f, R(0.6f, 1.8f)), R(0f, 360f));
                    }
                    break;
                }
                default:
                    mb.Box(Wood, new Vector3(0, 0.5f, 0), Vector3.one);
                    break;
            }
            mb.Build(go.transform, "mesh", true, Layers.Prop);
            var info = Info(type);
            if (info.Blocks || info.CanHide)
            {
                var col = go.AddComponent<BoxCollider>();
                col.center = new Vector3(0, info.Size.y * 0.5f, 0);
                col.size = info.Size;
                go.layer = info.BlocksVision ? Layers.Wall : Layers.Prop;
            }
            return go;
        }
    }
}
