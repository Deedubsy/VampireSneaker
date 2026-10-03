using UnityEngine;
using UnityEngine.UIElements;
using Vespertine.Mission;
using Vespertine.Stealth;
using Vespertine.Visual;

namespace Vespertine.UI
{
    /// <summary>
    /// A top-down sketch of one Spotted event for the debrief's Detections page (SR.10): the lamp's rim that lit her, his
    /// cone as walls cut it (near sector stronger), the band she crossed drawn bright, a sight-line, him and her. North
    /// up, a 5 m scale bar. Painter2D, drawn once.
    /// </summary>
    public class DetectionSketch : VisualElement
    {
        public const float W = 260f, H = 190f;
        readonly SpottedRecord _r;

        public DetectionSketch(SpottedRecord r)
        {
            _r = r;
            pickingMode = PickingMode.Ignore;
            style.width = W; style.height = H; style.flexShrink = 0;
            style.backgroundColor = new Color(0.04f, 0.05f, 0.08f, 1f);
            style.borderTopLeftRadius = style.borderTopRightRadius = style.borderBottomLeftRadius = style.borderBottomRightRadius = 4;
            generateVisualContent += Draw;
        }

        static Color A(Color c, float a) { c.a = a; return c; }

        void Draw(MeshGenerationContext ctx)
        {
            var r = _r;
            var p = ctx.painter2D;
            var box = r.Bounds();
            float s = SpottedRecord.Fit(box, W, H, 16f, 22f);
            var centre = box.center;
            Vector2 M(Vector2 w) => SpottedRecord.Map(w, centre, s, W, H);

            void Circle(Vector2 c, float rad, Color fill, Color stroke, float width)
            {
                p.BeginPath();
                p.Arc(M(c), rad * s, Angle.Degrees(0f), Angle.Degrees(360f));
                if (fill.a > 0f) { p.fillColor = fill; p.Fill(); }
                if (stroke.a > 0f) { p.strokeColor = stroke; p.lineWidth = width; p.Stroke(); }
            }

            // the lamp that lit her
            if (r.HasLamp)
            {
                bool lit = r.Band == DetectionMath.Band.Far && !r.Smell && !r.Searchlight;
                Circle(r.Lamp, r.LampR, A(Mats.Pal.Ember, 0.12f), A(Mats.Pal.Ember, lit ? 0.95f : 0.5f), lit ? 2.5f : 1.2f);
                Circle(r.Lamp, 0.35f, A(Mats.Pal.Ember, 1f), new Color(0, 0, 0, 0), 0f);
            }

            var red = Mats.Pal.Alerted;
            // far fan, cut at walls
            p.BeginPath();
            p.MoveTo(M(r.Guard));
            for (int i = 0; i <= SpottedRecord.Rays; i++) p.LineTo(M(r.Guard + r.RayDir(i) * r.ConeR[i]));
            p.ClosePath();
            p.fillColor = A(red, 0.14f); p.Fill();
            p.strokeColor = A(red, 0.55f); p.lineWidth = 1f; p.Stroke();
            // near sector
            p.BeginPath();
            p.MoveTo(M(r.Guard));
            for (int i = 0; i <= SpottedRecord.Rays; i++) p.LineTo(M(r.Guard + r.RayDir(i) * Mathf.Min(r.Near, r.ConeR[i])));
            p.ClosePath();
            p.fillColor = A(red, 0.3f); p.Fill();
            // the band she crossed, bright
            if (!r.Smell && !r.Searchlight)
            {
                if (r.Band == DetectionMath.Band.Near)
                {
                    p.BeginPath();
                    for (int i = 0; i <= SpottedRecord.Rays; i++)
                    {
                        var q = M(r.Guard + r.RayDir(i) * Mathf.Min(r.Near, r.ConeR[i]));
                        if (i == 0) p.MoveTo(q); else p.LineTo(q);
                    }
                    p.strokeColor = Color.white; p.lineWidth = 2.5f; p.Stroke();
                }
                else if (r.Band == DetectionMath.Band.Peripheral) Circle(r.Guard, r.Touch, new Color(0, 0, 0, 0), Color.white, 2.5f);
            }
            if (r.Smell) Circle(r.Her, 1.2f, new Color(0, 0, 0, 0), A(Mats.Pal.Bone, 0.8f), 1.5f);
            if (r.Searchlight) Circle(r.Her, 2f, A(Mats.Pal.Holy, 0.2f), A(Mats.Pal.Holy, 0.9f), 2f);

            // sight-line, him (with his facing) and her
            p.BeginPath(); p.MoveTo(M(r.Guard)); p.LineTo(M(r.Her));
            p.strokeColor = A(red, 0.9f); p.lineWidth = 1.5f; p.Stroke();
            Circle(r.Guard, 0.45f, red, Color.white, 1.5f);
            p.BeginPath(); p.MoveTo(M(r.Guard)); p.LineTo(M(r.Guard + r.Facing * 1.2f));
            p.strokeColor = Color.white; p.lineWidth = 2f; p.Stroke();
            Circle(r.Her, 0.45f, Mats.Pal.BloodBright, Color.white, 1.5f);

            // scale bar: 5 m
            float bar = 5f * s;
            var b0 = new Vector2(W - 12f - bar, H - 10f);
            p.BeginPath(); p.MoveTo(b0); p.LineTo(b0 + new Vector2(bar, 0f));
            p.MoveTo(b0 + new Vector2(0f, -4f)); p.LineTo(b0);
            p.MoveTo(b0 + new Vector2(bar, -4f)); p.LineTo(b0 + new Vector2(bar, 0f));
            p.strokeColor = A(Mats.Pal.Bone, 0.7f); p.lineWidth = 1f; p.Stroke();
        }
    }
}
