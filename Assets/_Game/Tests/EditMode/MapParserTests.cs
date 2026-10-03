using NUnit.Framework;
using Vespertine.Level;

namespace Vespertine.Tests
{
    public class MapParserTests
    {
        const string Sample = @"#! sample map
@mission
id = m00
title = Test Ward
ambient = 0.05
briefing = |
Line one.
Line two.
|
@map
#####
#...#
#.+.#
#####
@entities
player 1 1 face=E
route r1 pingpong | 1 1 wait=2 look=N | 3 1 wait=1.5
npc w1 watchman 3 1 route=r1 lantern
light l1 lamp 2 1 radius=6 off
group cm_dogs
npc h1 hound 2 2
endgroup
note n1 2 1 ""A Title"" ""Body text, with words"" #! inline comment
@objectives
primary escape ""Leave the ward"" reach 1 2 1 1
optional quiet ""Kill no one"" nokill
@script
on start: say ilse ""Cold. So cold."" ; activate cm_dogs
every timer 30: bark w1 ""Who's there?""
";

        [Test]
        public void ParsesHeaderAndMultiline()
        {
            var d = MapParser.Parse(Sample);
            Assert.AreEqual("m00", d.Id);
            Assert.AreEqual("Test Ward", d.Title);
            Assert.AreEqual(0.05f, d.GetFloat("ambient", 0), 1e-5f);
            Assert.AreEqual("Line one.\nLine two.", d.Get("briefing"));
        }

        [Test]
        public void UnclosedMultilineStopsAtNextSection()
        {
            var d = MapParser.Parse(Sample.Replace("Line two.\n|\n", "Line two.\n").Replace("Line two.\r\n|\r\n", "Line two.\r\n"));
            Assert.AreEqual("Line one.\nLine two.", d.Get("briefing"));
            Assert.AreEqual(4, d.Height, "the @map after the unclosed block still parses");
            Assert.IsTrue(d.Errors.Exists(e => e.Contains("not closed")));
        }

        [Test]
        public void ParsesMapRowsPadded()
        {
            var d = MapParser.Parse(Sample);
            Assert.AreEqual(4, d.Height);
            Assert.AreEqual(5, d.Width);
            Assert.AreEqual('+', d.CellAt(2, 2));
            Assert.AreEqual(' ', d.CellAt(-1, 0));
        }

        [Test]
        public void ParsesEntitiesWithOptsFlagsAndGroups()
        {
            var d = MapParser.Parse(Sample);
            CollectionAssert.IsEmpty(d.Errors, string.Join("\n", d.Errors));
            var w1 = d.FindEntity("w1");
            Assert.AreEqual("watchman", w1.Type);
            Assert.AreEqual(3f, w1.X);
            Assert.AreEqual("r1", w1.Opt("route"));
            Assert.IsTrue(w1.Has("lantern"));
            Assert.IsNull(w1.Group);
            Assert.AreEqual("cm_dogs", d.FindEntity("h1").Group);
            var l1 = d.FindEntity("l1");
            Assert.AreEqual(6f, l1.OptFloat("radius", 0));
            Assert.IsTrue(l1.Has("off"));
            var n1 = d.FindEntity("n1");
            Assert.AreEqual("A Title", n1.Arg(0));
            Assert.AreEqual("Body text, with words", n1.Arg(1));
            Assert.AreEqual("E", d.FindEntity("player").Opt("face"));
        }

        [Test]
        public void ParsesRoutes()
        {
            var d = MapParser.Parse(Sample);
            var r = d.Routes["r1"];
            Assert.AreEqual(RouteMode.PingPong, r.Mode);
            Assert.AreEqual(2, r.Points.Count);
            Assert.AreEqual(2f, r.Points[0].Wait);
            Assert.AreEqual(0f, r.Points[0].Look);
            Assert.IsNull(r.Points[1].Look);
            Assert.AreEqual(1.5f, r.Points[1].Wait);
        }

        [Test]
        public void ParsesObjectivesAndScript()
        {
            var d = MapParser.Parse(Sample);
            Assert.AreEqual(2, d.Objectives.Count);
            var o = d.Objectives[0];
            Assert.AreEqual("escape", o.Id);
            Assert.AreEqual("reach", o.Type);
            Assert.AreEqual("Leave the ward", o.Text);
            CollectionAssert.AreEqual(new[] { "1", "2", "1", "1" }, o.Args);
            Assert.IsTrue(d.Objectives[1].Optional);

            Assert.AreEqual(2, d.Script.Count);
            var s = d.Script[0];
            Assert.AreEqual("start", s.Event);
            Assert.AreEqual(2, s.Actions.Count);
            CollectionAssert.AreEqual(new[] { "say", "ilse", "Cold. So cold." }, s.Actions[0]);
            CollectionAssert.AreEqual(new[] { "activate", "cm_dogs" }, s.Actions[1]);
            Assert.IsTrue(d.Script[1].Repeat);
            Assert.AreEqual("30", d.Script[1].Arg);
        }

        [Test]
        public void ReportsErrors()
        {
            var d = MapParser.Parse("@map\n...\n@entities\nnpc a watchman 1 1 route=missing\nbogus 1 2\n");
            Assert.IsTrue(d.Errors.Exists(e => e.Contains("missing route")));
            Assert.IsTrue(d.Errors.Exists(e => e.Contains("unknown entity kind")));
            Assert.IsTrue(d.Errors.Exists(e => e.Contains("no player")));
        }

        [Test]
        public void CellWorldRoundTrip()
        {
            var d = MapParser.Parse(Sample);
            var w = d.CellToWorld(3, 1);
            Assert.AreEqual(7f, w.x, 1e-4f);
            Assert.AreEqual((4 - 1 - 1) * 2 + 1f, w.z, 1e-4f);
            var c = d.WorldToCellInt(w);
            Assert.AreEqual(3, c.x);
            Assert.AreEqual(1, c.y);
        }

        [Test]
        public void FacingParsing()
        {
            Assert.AreEqual(90f, MapParser.ParseFacing("E"));
            Assert.AreEqual(225f, MapParser.ParseFacing("sw"));
            Assert.AreEqual(12.5f, MapParser.ParseFacing("12.5"));
        }
    }
}
