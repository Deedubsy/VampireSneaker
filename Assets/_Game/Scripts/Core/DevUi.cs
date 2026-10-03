using System.Collections.Generic;
using System.Reflection;
using UnityEngine.UIElements;

namespace Vespertine.Core
{
    /// <summary>
    /// Development check for menus: lists the buttons a player can see and presses one by its label, through the
    /// button's own click handler (so the wiring is tested, not just the screen builder). Use from an editor eval,
    /// e.g. <c>DevUi.Click("New Night")</c> then <c>DevUi.Buttons()</c>.
    /// </summary>
    public static class DevUi
    {
        static bool Visible(VisualElement v)
        {
            for (var e = v; e != null; e = e.parent)
                if (e.resolvedStyle.display == DisplayStyle.None || e.resolvedStyle.visibility == Visibility.Hidden || e.resolvedStyle.opacity <= 0.01f)
                    return false;
            return v.panel != null;
        }

        static List<Button> All()
        {
            var l = new List<Button>();
            var root = Game.UI != null ? Game.UI.RootElement : null;
            if (root == null) return l;
            root.Query<Button>().ForEach(b => { if (Visible(b)) l.Add(b); });
            return l;
        }

        /// <summary>Visible buttons as "label" (or "label[off]" when disabled), in document order.</summary>
        public static string Buttons()
        {
            var parts = new List<string>();
            foreach (var b in All()) parts.Add(b.text + (b.enabledInHierarchy ? "" : "[off]"));
            return string.Join(" | ", parts);
        }

        /// <summary>Presses the last visible, enabled button with this label (the topmost layer comes last).
        /// <paramref name="nth"/> picks among several with the same label, counting from the first.</summary>
        public static string Click(string label, int nth = -1)
        {
            var hits = All().FindAll(b => b.text == label && b.enabledInHierarchy);
            if (hits.Count == 0) return "no button '" + label + "'";
            var b = nth >= 0 && nth < hits.Count ? hits[nth] : hits[hits.Count - 1];
            var invoke = typeof(Clickable).GetMethod("Invoke", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public, null, new[] { typeof(EventBase) }, null);
            if (b.clickable == null || invoke == null) return "cannot press '" + label + "'";
            using (var e = ClickEvent.GetPooled()) { e.target = b; invoke.Invoke(b.clickable, new object[] { e }); }
            return "pressed '" + label + "'";
        }
    }
}
