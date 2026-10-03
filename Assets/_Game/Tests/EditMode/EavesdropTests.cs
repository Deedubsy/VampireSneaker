using NUnit.Framework;
using Vespertine.Level;

namespace Vespertine.Tests
{
    public class EavesdropTests
    {
        static Eavesdrop Make()
        {
            var e = new Eavesdrop(1f) { Rest = 5f, Gap = 0.5f };
            e.Lines.Add(Eavesdrop.ParseLine("crane: The Bishop is late.", "x"));
            e.Lines.Add(Eavesdrop.ParseLine("lowell: The Bishop is never late.", "x"));
            return e;
        }

        // ticks in 0.1 s steps; returns how many lines started
        static int Run(Eavesdrop e, float secs, bool ready, bool inRange)
        {
            int started = 0;
            for (float t = 0; t < secs; t += 0.1f) if (e.Tick(0.1f, ready, inRange) >= 0) started++;
            return started;
        }

        [Test]
        public void ParsesSpeakerPrefixes()
        {
            var l = Eavesdrop.ParseLine("crane: Yes: quite.", "lowell");
            Assert.AreEqual("crane", l.Who);
            Assert.AreEqual("Yes: quite.", l.Text);
            var plain = Eavesdrop.ParseLine("No prefix here", "lowell");
            Assert.AreEqual("lowell", plain.Who);
            Assert.AreEqual("No prefix here", plain.Text);
            // a colon after the first space is part of the sentence, not a speaker
            Assert.AreEqual("lowell", Eavesdrop.ParseLine("Listen to me: now", "lowell").Who);
            Assert.GreaterOrEqual(l.Dur, 2.2f);
        }

        [Test]
        public void HeardOnlyWhenPresentForTheWholeRound()
        {
            var e = Make();
            Assert.AreEqual(2, Run(e, 1.2f + 30f, true, true));
            Assert.IsTrue(e.Heard);
            Assert.AreEqual(0, Run(e, 30f, true, true), "a heard conversation is not repeated");
        }

        [Test]
        public void ArrivingLateWaitsForTheNextRound()
        {
            var e = Make();
            Run(e, 1.2f, true, false);              // first line starts without her
            Assert.AreEqual(0, e.Index);
            Assert.IsFalse(e.Listening);
            Run(e, 4f, true, true);                 // she arrives mid-round
            Assert.IsFalse(e.Listening);
            Assert.AreEqual(0f, e.Progress);
            Run(e, 30f, true, true);                // the next round, from the top
            Assert.IsTrue(e.Heard);
        }

        [Test]
        public void LeavingOrDisturbingBreaksTheRound()
        {
            var e = Make();
            Run(e, 1.2f, true, true);
            Assert.IsTrue(e.Listening);
            Assert.Greater(e.Progress, 0f);
            Run(e, 0.3f, true, false);              // steps out
            Assert.IsFalse(e.Listening);
            Run(e, 30f, true, false);
            Assert.IsFalse(e.Heard);

            var d = Make();
            Run(d, 1.2f, true, true);
            Run(d, 0.2f, false, true);              // a speaker grows suspicious
            Assert.AreEqual(-1, d.Index, "the conversation stops");
            Assert.AreEqual(0, Run(d, 4.5f, true, true), "and rests before starting again");
            Run(d, 30f, true, true);
            Assert.IsTrue(d.Heard);
        }

        [Test]
        public void ValidatorChecksSpeakers()
        {
            const string map = @"@mission
id = ml
title = Listen
@map
#######
#.....#
#######
@entities
player 1 1
npc crane notable 3 1
npc lowell priest 4 1
listen c1 3 1 speakers=crane,lowell ""Crane and Lowell"" ""crane: Late."" ""lowell: Never.""
listen c2 3 1 speakers=crane ""Ghost"" ""ghost: Boo.""
listen c3 3 1 speakers=crane ""Silent""
@objectives
primary a ""Overhear"" interact_all c1 c2
";
            var all = string.Join("\n", MissionValidator.Validate(MapParser.Parse(map)));
            StringAssert.Contains("speaker 'ghost' is not an npc", all);
            StringAssert.Contains("(listen c3): a listen point needs", all);
            StringAssert.DoesNotContain("listen c1", all);
        }
    }
}
