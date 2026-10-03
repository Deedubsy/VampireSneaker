using UnityEngine;
using UnityEngine.UIElements;

namespace Vespertine.UI
{
    /// <summary>
    /// The pointer and cone wedge behind an off-screen guard's edge pip (SR.11): a triangle on the ring toward him, and
    /// a translucent wedge the way he faces, as wide as his cone. A wedge pointing into the screen means he is looking
    /// her way. Drawn with Painter2D; repaints only when something moved.
    /// </summary>
    public class EdgeArrow : VisualElement
    {
        public const float Size = 76f, Ring = 17f;
        Vector2 _dir = Vector2.right, _face;
        float _half;
        Color _col;

        public EdgeArrow()
        {
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;
            style.width = Size; style.height = Size;
            style.left = Ring - Size * 0.5f; style.top = Ring - Size * 0.5f;
            generateVisualContent += Draw;
        }

        /// <summary>Panel-space (y down) unit direction to the guard and his facing (zero to hide the wedge).</summary>
        public void Set(Vector2 dir, Vector2 face, float halfAngle, Color col)
        {
            if ((dir - _dir).sqrMagnitude < 1e-4f && (face - _face).sqrMagnitude < 1e-3f && Mathf.Abs(halfAngle - _half) < 0.5f && col == _col) return;
            _dir = dir; _face = face; _half = halfAngle; _col = col;
            MarkDirtyRepaint();
        }

        void Draw(MeshGenerationContext ctx)
        {
            var p = ctx.painter2D;
            var c = new Vector2(Size * 0.5f, Size * 0.5f);
            if (_face.sqrMagnitude > 0.5f)
            {
                float a0 = Mathf.Atan2(_face.y, _face.x), h = Mathf.Min(_half, 70f) * Mathf.Deg2Rad;
                var w = _col; w.a = 0.4f;
                p.fillColor = w;
                p.BeginPath();
                p.MoveTo(c);
                for (int i = 0; i <= 12; i++)
                {
                    float a = a0 - h + 2f * h * i / 12f;
                    p.LineTo(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (Size * 0.5f));
                }
                p.ClosePath();
                p.Fill();
            }
            var side = new Vector2(-_dir.y, _dir.x);
            var tri = _col; tri.a = 0.95f;
            p.fillColor = tri;
            p.BeginPath();
            p.MoveTo(c + _dir * (Ring + 11f));
            p.LineTo(c + _dir * (Ring + 1f) + side * 7f);
            p.LineTo(c + _dir * (Ring + 1f) - side * 7f);
            p.ClosePath();
            p.Fill();
        }
    }
}
