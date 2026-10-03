using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Tree = Vespertine.Data.Tree;
using UnityEngine.UIElements;
using Vespertine.Controls;
using Vespertine.Core;
using Vespertine.Data;
using Vespertine.Level;
using Vespertine.Progression;
using Vespertine.Save;

namespace Vespertine.UI
{
    /// <summary>The Refuge: between-mission hub. Next night, replays, Blood Arts, loadout, Dossier, journal, record.</summary>
    public partial class UIManager
    {
        string _hubTab = "Next Night";
        string _skillSel, _codexSel;
        VisualElement _hubRoot;

        public void ShowHub(string tab = null)
        {
            ClearScreens();
            HideModals();
            if (tab != null) _hubTab = tab;
            _hubRoot = E("scrim-solid");
            PushScreen("hub", _hubRoot, () => Confirm("Return to the main menu?", "The campaign is saved.", "Main Menu", () => Game.Root.QuitToMenu()), false);
            BuildHub();
        }

        void BuildHub()
        {
            var c = Game.Campaign;
            if (c == null || _hubRoot == null) return;
            _hubRoot.Clear();
            var page = Col();
            page.style.flexGrow = 1;
            page.style.paddingLeft = 48; page.style.paddingRight = 48; page.style.paddingTop = 28; page.style.paddingBottom = 24;

            // ---- top bar
            var top = Row();
            top.style.alignItems = Align.Center;
            top.style.justifyContent = Justify.SpaceBetween;
            var title = Col();
            title.Add(L("THE REFUGE", "h1"));
            title.Add(L(RefugeFlavour(c), "italic", "dim", "small"));
            top.Add(title);

            var stats = Row();
            stats.style.alignItems = Align.Center;
            var aw = Col(); aw.style.marginRight = 28; aw.style.width = 330; // fits "AWAKENING 5   1200/1400 vitae"
            var nt = c.NextThreshold;
            aw.Add(L($"AWAKENING {c.Awakening}" + (nt > 0 ? $"   <color=#9a9088>{c.Vitae}/{nt} vitae</color>" : "   <color=#d6b264>fully awake</color>"), "bold"));
            ((Label)aw[0]).enableRichText = true;
            var vb = Bar("bar-vitae", out var vf);
            int prev = CampaignState.VitaeThresholds[Mathf.Clamp(c.Awakening - 1, 0, CampaignState.VitaeThresholds.Length - 1)];
            SetFill(vf, nt > 0 ? Mathf.InverseLerp(prev, nt, c.Vitae) : 1f);
            aw.Add(vb);
            stats.Add(aw);
            var mk = Col(); mk.style.marginRight = 28;
            mk.Add(L($"{c.Marks}", "h2", "gold"));
            mk.Add(L("MARKS", "tiny", "dim"));
            stats.Add(mk);
            var tr = Col(); tr.style.width = 220;
            tr.Add(L($"TERROR {c.Terror}  ·  RUMOUR {c.Rumour}", "small"));
            var tbar = Row(); tbar.style.height = 8;
            int tot = Mathf.Max(1, c.Terror + c.Rumour);
            var te = E("bar-terror"); te.style.flexGrow = c.Terror + 0.01f; te.style.height = 8;
            var ru = E("bar-rumour"); ru.style.flexGrow = c.Rumour + 0.01f; ru.style.height = 8;
            tbar.Add(te); tbar.Add(ru);
            tr.Add(tbar);
            tr.Add(L(c.Terror > c.Rumour + 2 ? "The city whispers of a monster." : c.Rumour > c.Terror + 2 ? "The city whispers of a mercy." : "The city does not yet know what you are.", "tiny", "dim"));
            stats.Add(tr);
            top.Add(stats);
            top.style.flexShrink = 0;   // a long Missions list must scroll, not squeeze the header
            page.Add(top);
            page.Add(Space(10));

            // ---- tabs
            var tabs = Row();
            foreach (var t in new[] { "Next Night", "Missions", "Blood Arts", "Loadout", "Dossier", "Journal", "Codex", "Record" })
            {
                var tt = t;
                string label = t;
                if (t == "Blood Arts" && c.Marks > 0 && Skills.Nodes.Any(n => c.CanUnlock(n.Id, out _))) label += "  •";
                var b = B(label, () => { _hubTab = tt; BuildHub(); }, "tab");
                if (t == _hubTab) b.AddToClassList("selected");
                if (t == "Blood Arts" && !c.ArtsOpen) { b.SetEnabled(false); b.tooltip = "The Abbess has not yet spoken to you."; }
                if (t == "Dossier" && c.MissionIndex < CampaignState.DossierStartsAt - 1 && c.Dossier.Count == 0) b.SetEnabled(false);
                tabs.Add(b);
            }
            tabs.style.flexShrink = 0;
            page.Add(tabs);
            page.Add(E("divider"));

            if (_hubTab == "Blood Arts" && !c.ArtsOpen) _hubTab = "Next Night";
            var body = Col();
            body.style.flexGrow = 1;
            body.style.flexShrink = 1; body.style.minHeight = 0;
            switch (_hubTab)
            {
                case "Next Night": BuildNextNight(body, c); break;
                case "Missions": BuildMissionList(body, c); break;
                case "Blood Arts": BuildSkillTree(body, c); break;
                case "Loadout": BuildLoadout(body, c); break;
                case "Dossier": BuildDossier(body, c); break;
                case "Journal": BuildJournal(body, c); break;
                case "Codex": BuildCodex(body, c); break;
                case "Record": BuildRecord(body, c); break;
            }
            page.Add(body);

            var foot = Row();
            foot.style.justifyContent = Justify.SpaceBetween;
            var left = Row();
            left.Add(B("Settings", () => ShowSettings(), "btn-small"));
            left.Add(B("Load", ShowLoadMenu, "btn-small"));
            foot.Add(left);
            foot.Add(B("Main Menu", () => Game.Root.QuitToMenu(), "btn-small"));
            foot.style.flexShrink = 0; foot.style.marginTop = 6; // a long list scrolls; it must not squeeze the footer
            page.Add(foot);
            _hubRoot.Add(page);
        }

        static string RefugeFlavour(CampaignState c)
        {
            int i = c.MissionIndex;
            if (i <= 1) return "A flooded cellar beneath the Weirside. It smells of river and rust.";
            if (i <= 4) return "Tobias's cellar. Candle stubs, a borrowed coat, a door that locks from the inside.";
            if (i <= 8) return "The bell loft of a dead chapel. Clement's charts cover the walls.";
            if (i <= 11) return "The fledglings sleep in the crypt below. They listen for your step.";
            if (i >= Missions.All.Count)
            {
                if (c.Flag("abbess_free")) return "St Vesper's. The Abbess walks the nave at night, and the candles are lit for her.";
                if (c.Flag("abbess_consume")) return "St Vesper's. The vault is dry, and the city is quiet in a way it never was.";
                return "The ashes of St Vesper's. The sun comes up over it every morning now.";
            }
            return "Below the cathedral, the Abbess is waiting. Dawn is coming.";
        }

        static bool MapExists(string id) => Resources.Load<TextAsset>("Missions/" + id) != null;

        // ---------------------------------------------------------------- next night
        void BuildNextNight(VisualElement body, CampaignState c)
        {
            if (c.MissionIndex >= Missions.All.Count)
            {
                var done = Col("panel");
                done.Add(L("The long night is over.", "h2"));
                done.Add(L("Every mission is complete. Replay any of them from the Missions tab, or see how it ended again.", "body"));
                done.Add(B("The Ending", () => Game.Root.ShowEnding(Game.Root.EndingId()), "btn-primary"));
                body.Add(done);
                return;
            }
            var m = Missions.All[c.MissionIndex];
            bool built = MapExists(m.Id);
            var row = Row();
            row.style.flexGrow = 1;
            row.style.alignItems = Align.Stretch; // the card takes the body's height: a centred card was measured short and its button fell out of it
            var card = Col("panel");
            card.style.flexGrow = 1;
            card.style.marginRight = 18;
            card.Add(L(Missions.ActName(m.Act).ToUpperInvariant() + $"   ·   NIGHT {c.MissionIndex + 1} OF {Missions.All.Count}", "tiny", "moon"));
            card.Add(L(m.Title, "title"));
            card.Add(L($"{m.Subtitle}  —  {m.Place}", "subtitle"));
            card.Add(Space(10));
            card.Add(L(m.Pitch, "h3"));
            string brief = BriefingFor(m.Id);
            if (!string.IsNullOrEmpty(brief))
            {
                var sv = new ScrollView(ScrollViewMode.Vertical);
                sv.style.maxHeight = 300;
                sv.style.flexGrow = 0; sv.style.flexShrink = 1; // a long briefing scrolls inside the card instead of pushing the button out
                sv.Add(L(brief, "doc-body"));
                card.Add(sv);
            }
            card.Add(Space(14));
            var go = B(built ? "Begin the night" : "Not yet in this build", () => Game.Root.StartMission(m.Id), "btn-big", "btn-primary");
            go.SetEnabled(built);
            go.style.alignSelf = Align.FlexStart;
            card.Add(go);
            if (!built) card.Add(L("This mission's map has not been built yet. Replays of finished missions are available.", "small", "dim"));
            row.Add(card);

            // side: readiness
            var side = Col("panel");
            side.style.width = 400;
            side.Add(L("PREPARATION", "h3"));
            side.Add(L($"Vitality {c.MaxHP:0}   ·   Blood {c.MaxBlood:0}", "small"));
            side.Add(L($"Ability slots {c.Loadout.Count}/{c.Slots}", "small"));
            foreach (var id in c.Loadout) { var a = Skills.Ability(id); if (a != null) side.Add(L("   " + a.Name, "small", "blood")); }
            if (c.Loadout.Count < c.Slots && c.OwnedAbilities().Count() > c.Loadout.Count) side.Add(L("You have empty slots. See Loadout.", "small", "gold"));
            if (c.Marks > 0) side.Add(L($"{c.Marks} unspent Mark{(c.Marks > 1 ? "s" : "")}. See Blood Arts.", "small", "gold"));
            side.Add(Space(10));
            if (c.Countermeasures.Count > 0)
            {
                side.Add(L("THE VIGIL HAS ADAPTED", "h3"));
                foreach (var cm in c.Countermeasures)
                {
                    side.Add(L("• " + Habits.CountermeasureName(cm), "small", "bad"));
                    var why = c.AnswerSource(cm);
                    if (why != null) side.Add(L("   " + why, "tiny", "dim"));
                }
            }
            side.Add(Space(10));
            side.Add(L("DIFFICULTY", "h3"));
            side.Add(L(Difficulties.Get(c.Difficulty).Name + (c.Ironblood ? " · Ironblood" : ""), "small"));
            var latest = SaveSystem.Latest();
            if (latest != null && latest.MissionId == m.Id)
            {
                side.Add(Space(10));
                side.Add(L("An unfinished night", "h3"));
                side.Add(L($"{SaveSystem.SlotName(latest.Slot)}: {latest.Label}", "small", "dim"));
                side.Add(B("Resume from save", () => Game.Root.LoadMissionSave(SaveSystem.Profile, latest.Slot), "btn-small"));
            }
            row.Add(side);
            body.Add(row);
        }

        readonly Dictionary<string, string> _briefCache = new Dictionary<string, string>();

        string BriefingFor(string id)
        {
            if (_briefCache.TryGetValue(id, out var b)) return b;
            b = "";
            var t = Resources.Load<TextAsset>("Missions/" + id);
            if (t != null)
            {
                try { var d = MapParser.Parse(t.text); b = d.Get("briefing", "").Replace("\\n", "\n"); }
                catch (Exception e) { Debug.LogWarning($"[Hub] briefing parse {id}: {e.Message}"); }
            }
            _briefCache[id] = b;
            return b;
        }

        // ---------------------------------------------------------------- missions (replay)
        void BuildMissionList(VisualElement body, CampaignState c)
        {
            var sv = new ScrollView(ScrollViewMode.Vertical);
            sv.style.flexGrow = 1;
            int act = 0;
            for (int i = 0; i < Missions.All.Count; i++)
            {
                var m = Missions.All[i];
                if (m.Act != act) { act = m.Act; var h = L(Missions.ActName(act).ToUpperInvariant(), "h3", "moon"); h.style.marginTop = 10; sv.Add(h); }
                var rec = c.Records.Find(r => r.Id == m.Id);
                bool unlocked = i <= c.MissionIndex;
                var item = Row("list-item");
                item.style.justifyContent = Justify.SpaceBetween;
                item.style.alignItems = Align.Center;
                if (!unlocked) item.AddToClassList("locked");
                var left = Col();
                left.Add(L($"{i + 1:00}.  {(unlocked ? m.Title : "???")}", "bold"));
                if (unlocked) left.Add(L($"{m.Subtitle} — {m.Place}", "tiny", "dim"));
                if (rec != null && rec.Completed)
                {
                    // the challenges, earned in gold: Shadow Tactics-style badges for replays
                    var chs = new System.Text.StringBuilder();
                    int got = 0;
                    foreach (var d in Progression.Challenges.All)
                    {
                        bool has = rec.Challenges != null && rec.Challenges.Contains(d.Id);
                        if (has) got++;
                        string name = d.Id == Progression.Challenges.Swift && m.Par > 0f ? $"{d.Name} {Progression.Challenges.FormatPar(m.Par)}" : d.Name;
                        chs.Append(has ? $"<color=#d6b264>◆ {name}</color>  ·  " : $"<color=#5d5a66>◇ {name}</color>  ·  ");
                    }
                    var cl = L($"{got}/{Progression.Challenges.All.Length}   " + chs.ToString().TrimEnd(' ', '·'), "tiny");
                    cl.enableRichText = true;
                    left.Add(cl);
                }
                item.Add(left);
                var right = Row();
                right.style.alignItems = Align.Center;
                if (rec != null && rec.Completed)
                {
                    right.Add(L($"best {FormatTime(rec.BestTime)}   ·   optionals {rec.Optionals.Count}   ·   secrets {rec.Secrets.Count}", "tiny", "dim"));
                    ((Label)right[0]).enableRichText = true;
                    var id = m.Id;
                    var rb = B("Replay", () => Confirm("Replay " + m.Title + "?", "Replays earn vitae, any optionals or secrets not yet found, and challenges. Story progress is unaffected.", "Replay", () => Game.Root.StartMission(id)), "btn-small");
                    rb.SetEnabled(MapExists(id));
                    right.Add(rb);
                }
                item.Add(right);
                sv.Add(item);
            }
            body.Add(sv);
        }

        // ---------------------------------------------------------------- skill tree
        float _treeScroll;

        void BuildSkillTree(VisualElement body, CampaignState c)
        {
            var row = Row();
            row.style.flexGrow = 1; row.style.minHeight = 0;
            row.style.alignItems = Align.Stretch;
            // four tall columns: they scroll together inside the body, keeping the scroll across the rebuild a click makes
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1; scroll.style.flexShrink = 1; scroll.style.minHeight = 0;
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            var trees = scroll.contentContainer;
            trees.AddToClassList("tree-row");
            trees.style.flexDirection = FlexDirection.Row;
            trees.style.alignItems = Align.FlexStart;
            float keep = _treeScroll;
            scroll.verticalScroller.valueChanged += v => _treeScroll = v;
            scroll.schedule.Execute(() => scroll.scrollOffset = new Vector2(0, keep));
            foreach (Tree t in Enum.GetValues(typeof(Tree)))
            {
                var col = Col("tree", "tree-" + t.ToString().ToLowerInvariant());
                col.Add(L(t.ToString().ToUpperInvariant(), "h2"));
                col.Add(L(TreeBlurb(t), "tiny", "dim"));
                for (int tier = 0; tier <= 4; tier++)
                {
                    var nodes = Skills.Nodes.Where(n => n.Tree == t && n.Tier == tier && (tier > 0 || c.Has(n.Id))).ToList(); // gifts show once given
                    if (nodes.Count == 0) continue;
                    var tl = L(tier == 0 ? "GIFT" : $"TIER {tier}  ·  Awakening {nodes[0].RequiredAwakening}+", "tier-label");
                    col.Add(tl);
                    foreach (var n in nodes)
                    {
                        var nn = n;
                        var el = Col("node");
                        bool owned = c.Has(n.Id);
                        bool can = c.CanUnlock(n.Id, out _);
                        el.AddToClassList(owned ? "owned" : can ? "available" : "locked");
                        if (_skillSel == n.Id) el.AddToClassList("selected");
                        el.Add(L(n.Name, "node-name"));
                        el.Add(L($"{TypeName(n.Type)}{(owned ? "" : $"  ·  {n.Cost} Mark{(n.Cost > 1 ? "s" : "")}")}", "node-meta"));
                        el.RegisterCallback<ClickEvent>(e =>
                        {
                            _skillSel = nn.Id;
                            Game.Audio?.Play2D("ui_click", 0.4f);
                            if (e.clickCount >= 2 && c.CanUnlock(nn.Id, out _)) UnlockSkill(nn.Id);
                            else BuildHub();
                        });
                        col.Add(el);
                    }
                }
                trees.Add(col);
            }
            row.Add(scroll);

            // details pane
            var side = Col("panel");
            side.style.width = 380;
            side.style.marginLeft = 12;
            var sel = Skills.Get(_skillSel);
            if (sel == null)
            {
                side.Add(L("BLOOD ARTS", "h2"));
                side.Add(L("Spend Marks to learn the arts of the blood. Marks are earned for each first clear, each challenge met for the first time, each secret, and each notable drained.\n\nSelect a node to read it; double-click to learn.\n\nHigher tiers open as Ilse awakens.", "body"));
            }
            else
            {
                side.Add(L(sel.Name, "h2"));
                side.Add(L($"{sel.Tree} · {TypeName(sel.Type)} · Tier {sel.Tier}", "small", "dim"));
                side.Add(Space(6));
                side.Add(L(sel.Effect, "body"));
                var ab = Skills.Ability(sel.Id);
                if (ab != null)
                {
                    side.Add(Space(6));
                    var meta = new List<string>();
                    if (ab.Cost > 0) meta.Add($"{ab.Cost:0} blood");
                    if (ab.Upkeep > 0) meta.Add($"{ab.Upkeep:0.#} blood/s");
                    if (ab.Range > 0) meta.Add($"{ab.Range:0} m");
                    if (ab.Cooldown > 0) meta.Add($"{ab.Cooldown:0} s recovery");
                    if (ab.Holy) meta.Add("blocked on holy ground");
                    if (ab.Dominion) meta.Add("fails on the Warded and on animals");
                    side.Add(L(string.Join("  ·  ", meta), "small", "blood"));
                }
                if (sel.Parent != null) side.Add(L($"Requires {Skills.Get(sel.Parent)?.Name}", "tiny", c.Has(sel.Parent) ? "good" : "bad"));
                if (sel.Cross != null) side.Add(L($"Requires {Skills.Get(sel.Cross)?.Name} ({Skills.Get(sel.Cross)?.Tree})", "tiny", c.Has(sel.Cross) ? "good" : "bad"));
                side.Add(Space(12));
                if (c.Has(sel.Id)) side.Add(L("Learned.", "good"));
                else
                {
                    bool can = c.CanUnlock(sel.Id, out var why);
                    var ub = B($"Learn  ({sel.Cost} Mark{(sel.Cost > 1 ? "s" : "")})", () => UnlockSkill(sel.Id), "btn-primary");
                    ub.SetEnabled(can);
                    side.Add(ub);
                    if (!can && why != null) side.Add(L(why, "small", "bad"));
                }
            }
            side.Add(E("grow"));
            side.Add(E("divider"));
            side.Add(L($"Marks: {c.Marks} unspent · {c.SpentMarks()} spent", "small"));
            var rs = B("Unlearn all (refund Marks)", () => Confirm("Unlearn every art?", "All spent Marks are refunded. Story gifts are kept.", "Unlearn", () => { c.Respec(); SaveSystem.SaveCampaign(c); BuildHub(); }), "btn-small");
            rs.SetEnabled(c.SpentMarks() > 0);
            side.Add(rs);
            row.Add(side);
            body.Add(row);
        }

        void UnlockSkill(string id)
        {
            var c = Game.Campaign;
            if (!c.Unlock(id)) { Game.Audio?.Play2D("ui_error", 0.5f); return; }
            SaveSystem.SaveCampaign(c);
            Game.Audio?.Play2D("awaken", 0.6f, 1.2f);
            Toast($"Learned {Skills.Get(id)?.Name}");
            BuildHub();
        }

        static string TypeName(NodeType t) => t == NodeType.Mod ? "Refinement" : t == NodeType.Command ? "Thrall command" : t.ToString();

        static string TreeBlurb(Tree t)
        {
            switch (t)
            {
                case Tree.Predator: return "The body. Speed, the pounce, the kill.";
                case Tree.Shade: return "The dark. Lights die, distances fold.";
                case Tree.Dominion: return "The will. Whispers, stillness, thralls.";
                case Tree.Sanguis: return "The blood. Sight, healing, horror.";
            }
            return "";
        }

        // ---------------------------------------------------------------- loadout
        void BuildLoadout(VisualElement body, CampaignState c)
        {
            body.Add(L($"Ilse can hold {c.Slots} art{(c.Slots > 1 ? "s" : "")} ready at once (more as she awakens). Thrall commands need no slot.", "body"));
            body.Add(Space(8));
            var slots = Row();
            slots.style.alignItems = Align.Stretch; // locked slots line up with filled ones
            for (int i = 0; i < 6; i++)
            {
                var s = Col("card");
                s.style.width = 190; s.style.minHeight = 110;
                if (i >= c.Slots) { s.AddToClassList("locked"); s.Add(L($"Slot {i + 1}", "tiny", "dim")); s.Add(L($"Awakening {SlotAwakening(i)}", "small", "dim")); }
                else if (i < c.Loadout.Count)
                {
                    var a = Skills.Ability(c.Loadout[i]);
                    s.Add(L($"Slot {i + 1}", "tiny", "dim"));
                    s.Add(L(a?.Name ?? c.Loadout[i], "h3", "blood"));
                    s.Add(L(a?.Hint ?? "", "tiny"));
                    var id = c.Loadout[i];
                    int idx = i;
                    var r = Row();
                    r.style.marginTop = 6;
                    if (idx > 0) r.Add(Arrow(B("◂", () => { c.Loadout.RemoveAt(idx); c.Loadout.Insert(idx - 1, id); SaveSystem.SaveCampaign(c); BuildHub(); }, "btn-small")));
                    r.Add(B("Remove", () => { c.Unequip(id); SaveSystem.SaveCampaign(c); BuildHub(); }, "btn-small"));
                    if (idx < c.Loadout.Count - 1) r.Add(Arrow(B("▸", () => { c.Loadout.RemoveAt(idx); c.Loadout.Insert(idx + 1, id); SaveSystem.SaveCampaign(c); BuildHub(); }, "btn-small")));
                    s.Add(E("grow"));
                    s.Add(r);
                }
                else { s.Add(L($"Slot {i + 1}", "tiny", "dim")); s.Add(L("empty", "small", "dim")); }
                slots.Add(s);
            }
            body.Add(slots);
            body.Add(Space(12));
            body.Add(L("KNOWN ARTS", "h3"));
            var owned = c.OwnedAbilities().ToList();
            if (owned.Count == 0) body.Add(L("Ilse knows no arts yet. Learn them in Blood Arts.", "dim"));
            var grid = Row();
            grid.style.flexWrap = Wrap.Wrap;
            foreach (var id in owned)
            {
                var a = Skills.Ability(id);
                bool eq = c.Loadout.Contains(id);
                var card = Col("list-item");
                card.style.flexDirection = FlexDirection.Column; card.style.alignItems = Align.FlexStart; // .list-item is a row
                card.style.width = 300; card.style.marginRight = 8;
                if (eq) card.AddToClassList("selected");
                card.Add(L(a.Name, "bold"));
                card.Add(L(a.Hint, "tiny", "dim"));
                card.Add(L((a.Cost > 0 ? $"{a.Cost:0} blood" : a.Upkeep > 0 ? $"{a.Upkeep:0.#} blood/s" : "free") + (eq ? "   ·   equipped: click to remove" : "   ·   click to equip"), "tiny", "blood"));
                var iid = id;
                card.RegisterCallback<ClickEvent>(_ =>
                {
                    if (c.Loadout.Contains(iid)) c.Unequip(iid);
                    else if (!c.Equip(iid)) { Toast("Every slot is full."); Game.Audio?.Play2D("ui_error", 0.5f); return; }
                    Game.Audio?.Play2D("ui_click", 0.5f);
                    SaveSystem.SaveCampaign(c);
                    BuildHub();
                });
                grid.Add(card);
            }
            body.Add(grid);
        }

        /// <summary>A reorder arrow: narrower than a word button so three fit in a slot card.</summary>
        static Button Arrow(Button b) { b.style.minWidth = 0; b.style.width = 30; b.style.paddingLeft = b.style.paddingRight = 0; return b; }

        static int SlotAwakening(int slot)
        {
            for (int a = 1; a <= CampaignState.MaxAwakening; a++) if (CampaignState.SlotsFor(a) > slot) return a;
            return CampaignState.MaxAwakening;
        }

        // ---------------------------------------------------------------- dossier
        void BuildDossier(VisualElement body, CampaignState c)
        {
            var row = Row();
            row.style.alignItems = Align.FlexStart;
            var left = Col("panel");
            left.style.flexGrow = 1; left.style.marginRight = 12;
            left.Add(L("THE VIGIL'S DOSSIER", "h2"));
            left.Add(L("Captain Vane's people study every night you leave behind. From the third night, they answer your heaviest habits with countermeasures: two at most, never two against one art. An answer lapses after two nights without the habit that called it.", "small", "dim"));
            // the stolen field manual opens their ledger: exact weights and the next answer
            bool manual = c.Flags.Contains("vigil_manual");
            if (!manual) left.Add(L("You only know what the city whispers. Their own ledger would tell you more.", "small", "gold"));
            left.Add(Space(8));
            float max = 1f;
            foreach (var h in Habits.All) max = Mathf.Max(max, c.Habit(h));
            max = Mathf.Max(max, 8f);
            foreach (var h in Habits.All)
            {
                float v = c.Habit(h);
                var r = Row("stat-row");
                r.style.alignItems = Align.Center;
                r.style.justifyContent = Justify.FlexStart; // stat-row spreads its children; these are fixed columns
                var k = L(HabitName(h), "stat-k"); k.style.width = 200; k.style.flexShrink = 0;
                r.Add(k);
                var cm = Habits.Countermeasure(h);
                bool active = cm != null && c.Countermeasures.Contains(cm);
                if (manual)
                {
                    // red once the Vigil would answer it; a gold tick marks where that starts
                    var bar = Bar(v >= 4f ? "bar-blood" : "bar-hp", out var f, 320);
                    bar.style.flexShrink = 0;
                    SetFill(f, v / max);
                    var tick = new VisualElement();
                    tick.style.position = Position.Absolute;
                    tick.style.left = Length.Percent(4f / max * 100f);
                    tick.style.top = -2; tick.style.bottom = -2; tick.style.width = 2;
                    tick.style.backgroundColor = new Color(0.85f, 0.68f, 0.3f);
                    bar.Add(tick);
                    r.Add(bar);
                }
                else
                {
                    var word = L(v < 1f ? "-" : v < 4f ? "whispered of" : "talked about", "small", v >= 4f ? "bad" : "dim");
                    word.style.width = 320; word.style.flexShrink = 0;
                    r.Add(word);
                }
                var tag = L(active ? "ANSWERED" : v >= 4f ? "noticed" : "", "tiny", active ? "bad" : "gold");
                tag.style.marginLeft = 10;
                tag.style.flexGrow = 1;
                tag.style.unityTextAlign = TextAnchor.MiddleRight;
                r.Add(tag);
                left.Add(r);
            }
            row.Add(left);
            var right = Col("panel");
            right.style.width = 460;
            right.Add(L("COUNTERMEASURES", "h2"));
            if (c.Countermeasures.Count == 0) right.Add(L(c.MissionIndex + 1 < CampaignState.DossierStartsAt ? "The Vigil is still learning what you are." : "None yet. They are watching.", "dim"));
            if (manual && c.MissionIndex + 1 >= CampaignState.DossierStartsAt - 1)
            {
                var next = c.NextCountermeasure();
                right.Add(L("NEXT ANSWER (from their manual)", "tiny", "gold"));
                right.Add(L(next != null ? Habits.CountermeasureName(next)
                    : c.Countermeasures.Count >= CampaignState.MaxCountermeasures ? "None while two answers stand." : "Nothing yet: no habit is heavy enough, or its art is already answered.", "small", next != null ? "bad" : "dim"));
                right.Add(Space(8));
            }
            foreach (var cm in c.Countermeasures)
            {
                right.Add(L(Habits.CountermeasureName(cm), "h3", "bad"));
                var why = c.AnswerSource(cm);
                if (why != null) right.Add(L(why + ".", "tiny", "gold"));
                right.Add(L(Habits.CountermeasureText(cm), "small"));
                right.Add(Space(6));
            }
            row.Add(right);
            body.Add(row);
        }

        public static string HabitName(string h)
        {
            switch (h)
            {
                case Habits.Rooftops: return "Rooftop routes";
                case Habits.Snuff: return "Lights put out";
                case Habits.Lethal: return "Killing";
                case Habits.Sips: return "Sipping";
                case Habits.Sightings: return "Being seen";
                case Habits.Dominion: return "Mind arts";
                case Habits.Mist: return "Mist form";
                case Habits.Blood: return "Blood arts";
                case Habits.BodiesFound: return "Bodies found";
            }
            return h;
        }

        // ---------------------------------------------------------------- journal
        string _loreSel;

        void BuildJournal(VisualElement body, CampaignState c)
        {
            var row = Row();
            row.style.flexGrow = 1;
            row.style.alignItems = Align.Stretch;   // list and page from the top, full height
            var list = new ScrollView(ScrollViewMode.Vertical);
            list.style.width = 380;
            list.style.marginRight = 12;
            if (c.Lore.Count == 0) list.Add(L("Nothing yet. Letters, ledgers and confessions found on the hunt are kept here.", "dim"));
            string lastMission = null;
            foreach (var e in c.Lore)
            {
                if (e.Mission != lastMission)
                {
                    lastMission = e.Mission;
                    var mi = Missions.Get(e.Mission);
                    var h = L(mi != null ? mi.Title.ToUpperInvariant() : "OTHER", "tiny", "moon");
                    h.style.marginTop = 8;
                    list.Add(h);
                }
                var ee = e;
                var it = L(e.Title, "list-item");
                if (_loreSel == e.Id) it.AddToClassList("selected");
                it.RegisterCallback<ClickEvent>(_ => { _loreSel = ee.Id; Game.Audio?.Play2D("page", 0.5f); BuildHub(); });
                list.Add(it);
            }
            row.Add(list);
            var page = Col("panel");
            page.style.flexGrow = 1;
            var sel = c.Lore.Find(x => x.Id == _loreSel);
            if (sel != null)
            {
                page.Add(L(sel.Title, "h2"));
                page.Add(E("divider"));
                var sv = new ScrollView(ScrollViewMode.Vertical);
                sv.Add(L(sel.Body.Replace("\\n", "\n"), "doc-body"));
                page.Add(sv);
            }
            else
            {
                page.Add(L("JOURNAL", "h2"));
                page.Add(L($"{c.Lore.Count} entr{(c.Lore.Count == 1 ? "y" : "ies")} found.", "dim"));
                page.Add(Space(10));
                page.Add(L("Tobias: " + (c.Tobias == "alive" ? "alive, and still afraid of you" : c.Tobias == "thrall" ? "bound to your will" : "lost"), "small"));
            }
            row.Add(page);
            body.Add(row);
        }

        // ---------------------------------------------------------------- codex
        void BuildCodex(VisualElement body, CampaignState c)
        {
            var row = Row();
            row.style.flexGrow = 1;
            row.style.alignItems = Align.Stretch;
            var list = new ScrollView(ScrollViewMode.Vertical);
            list.style.width = 300;
            list.style.flexGrow = 0; list.style.flexShrink = 0;
            list.style.marginRight = 12;
            var listed = Codex.Listed.ToList();
            int known = listed.Count(a => c.Bestiary.Contains(a.Id));
            list.Add(L($"{known} of {listed.Count} known", "tiny", "dim"));
            foreach (var f in Codex.Order)
            {
                var group = listed.Where(a => a.Faction == f).ToList();
                if (group.Count == 0) continue;
                var h = L(Codex.FactionName(f).ToUpperInvariant(), "tiny", "moon");
                h.style.marginTop = 8;
                list.Add(h);
                foreach (var a in group)
                {
                    bool seen = c.Bestiary.Contains(a.Id);
                    var aa = a;
                    var it = L(seen ? a.Name : "???", "list-item");
                    if (!seen) it.AddToClassList("locked");
                    if (_codexSel == a.Id) it.AddToClassList("selected");
                    if (seen) it.RegisterCallback<ClickEvent>(_ => { _codexSel = aa.Id; Game.Audio?.Play2D("page", 0.5f); BuildHub(); });
                    list.Add(it);
                }
            }
            row.Add(list);

            var page = Col("panel");
            page.style.flexGrow = 1;
            var sel = c.Bestiary.Contains(_codexSel ?? "") ? Archetypes.Get(_codexSel) : null;
            if (sel == null)
            {
                page.Add(L("CODEX", "h2"));
                page.Add(L("Everyone Ilse has watched from the dark: what they see, what they carry, what their blood is worth. A page is written the first time she lays eyes on one.", "body"));
            }
            else
            {
                page.Add(L(sel.Name, "h2"));
                page.Add(L(Codex.FactionName(sel.Faction), "small", "dim"));
                page.Add(E("divider"));
                var lore = Codex.Lore(sel.Id);
                if (lore != null) { var ll = L(lore, "body"); ll.style.unityFontStyleAndWeight = FontStyle.Italic; page.Add(ll); page.Add(Space(8)); }
                page.Add(L(sel.Description, "small", "dim"));
                page.Add(Space(10));
                page.Add(CodexRow("Sight", Codex.SightText(sel)));
                page.Add(CodexRow("Hearing", Codex.HearingText(sel)));
                page.Add(CodexRow("Armed", Codex.WeaponName(sel.Weapon)));
                page.Add(CodexRow("Nerve", Codex.MoraleText(sel.Morale)));
                var blood = Codex.BloodText(sel);
                if (blood != null) page.Add(CodexRow("Blood", blood));
                var traits = Codex.Traits(sel);
                if (traits.Count > 0)
                {
                    page.Add(Space(10));
                    page.Add(L("WHAT TO KNOW", "h3"));
                    foreach (var t in traits) page.Add(L("•  " + t, "small"));
                }
            }
            row.Add(page);
            body.Add(row);
        }

        /// <summary>A label and a sentence that wraps beside it (StatRow is for short right-aligned numbers).</summary>
        static VisualElement CodexRow(string k, string v)
        {
            var r = Row();
            r.style.alignItems = Align.FlexStart; r.style.marginBottom = 4;
            var kl = L(k, "small", "dim"); kl.style.width = 90; kl.style.flexShrink = 0;
            var vl = L(v, "small"); vl.style.flexGrow = 1; vl.style.flexShrink = 1; vl.style.whiteSpace = WhiteSpace.Normal;
            r.Add(kl); r.Add(vl);
            return r;
        }

        // ---------------------------------------------------------------- record
        void BuildRecord(VisualElement body, CampaignState c)
        {
            var row = Row();
            row.style.alignItems = Align.FlexStart; // panels hang from the top, not the middle
            var left = Col("panel");
            left.style.width = 520; left.style.marginRight = 12;
            left.Add(L("RECORD", "h2"));
            var t = TimeSpan.FromSeconds(c.PlayTime);
            left.Add(StatRow("Time in the dark", $"{(int)t.TotalHours}h {t.Minutes:00}m"));
            left.Add(StatRow("Difficulty", Difficulties.Get(c.Difficulty).Name + (c.Ironblood ? " · Ironblood" : "")));
            left.Add(StatRow("Nights survived", c.Records.Count(r => r.Completed).ToString()));
            left.Add(StatRow("Humans killed", c.TotalKills.ToString()));
            left.Add(StatRow("Sipped from", c.TotalSips.ToString()));
            left.Add(StatRow("Drained", c.TotalDrains.ToString()));
            left.Add(StatRow("Vitae", c.Vitae.ToString()));
            left.Add(StatRow("Awakening", $"{c.Awakening} / {CampaignState.MaxAwakening}"));
            left.Add(StatRow("Terror", c.Terror.ToString()));
            left.Add(StatRow("Rumour", c.Rumour.ToString()));
            left.Add(StatRow("Secrets", c.Records.Sum(r => r.Secrets.Count).ToString()));
            left.Add(StatRow("Challenges", $"{c.Records.Sum(r => r.Challenges != null ? r.Challenges.Count : 0)} / {Missions.All.Count * Progression.Challenges.All.Length}"));
            left.Add(StatRow("Journal entries", c.Lore.Count.ToString()));
            row.Add(left);
            var right = Col("panel");
            right.style.flexGrow = 1;
            right.Add(L("WHAT SHE IS BECOMING", "h2"));
            right.Add(L(BecomingText(c), "body", "italic"));
            row.Add(right);
            body.Add(row);
        }

        static string BecomingText(CampaignState c)
        {
            if (c.TotalKills == 0 && c.TotalDrains == 0) return "She has not yet taken a life. The hunger is patient; she is more patient still.";
            if (c.Terror > c.Rumour + 4) return "Mothers in the Weirside bar their doors at dusk and tell their children about the woman in the river fog. They are right to.";
            if (c.Rumour > c.Terror + 4) return "There is a story in the slums of a pale lady who walks the alleys and takes only what she needs. Some leave their windows open.";
            return "She is still deciding what she is. The city is still deciding with her.";
        }
    }
}
