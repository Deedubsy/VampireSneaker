using System.Collections.Generic;
using System.Text;

namespace Vespertine.Progression
{
    /// <summary>
    /// The blood-dreams between missions: the day Ilse sleeps through after each first clear, when the Abbess talks to
    /// her. They are pure data, built from the campaign state as it stands after the mission (flags, Tobias, Terror
    /// against Rumour), so the same night dreams differently depending on what she did in it. Each one is shown once,
    /// then kept in the Journal. The last mission has no dream: the ending plays instead.
    /// </summary>
    public static class Interludes
    {
        public const string Abbess = "Abbess", Ilse = "Ilse", Narrator = "";

        public struct Line
        {
            public string Speaker, Text;
            public Line(string s, string t) { Speaker = s; Text = t; }
        }

        public class Dream
        {
            public string Id, MissionId, Title;
            public List<Line> Lines = new List<Line>();
        }

        public static string FlagFor(string missionId) => "dream_" + missionId;

        /// <summary>The dream after <paramref name="missionId"/>, or null if that night has none.</summary>
        public static Dream For(string missionId, CampaignState c)
        {
            if (c == null) return null;
            var d = new Dream { Id = FlagFor(missionId), MissionId = missionId };
            var L = d.Lines;
            void N(string t) => L.Add(new Line(Narrator, t));
            void A(string t) => L.Add(new Line(Abbess, t));
            void I(string t) => L.Add(new Line(Ilse, t));
            void S(string who, string t) => L.Add(new Line(who, t));
            bool f(string x) => c.Flag(x);
            bool terror = c.Terror > c.Rumour;

            switch (missionId)
            {
                case "m01":
                    d.Title = "Red Water";
                    N("She sleeps the day in a culvert, curled against the cold iron, and she dreams of red water.");
                    A("You woke among the failures, daughter. You did not stay among them.");
                    I("Who are you?");
                    A("The one whose blood they put in you. Two hundred years in the dark beneath them, and they bleed me by the cup.");
                    A("They will look for you in the river. Let them. You have a brother above the river, have you not?");
                    I("Leave Tobias out of this.");
                    A("I leave no one out of anything. Rest. The sun is a long, slow animal. It will pass over you.");
                    break;

                case "m02":
                    d.Title = "A Door Opened from the Inside";
                    N("His room smells of lamp oil and boiled cabbage. She lies all day under his bed with the curtains pinned shut, and he does not sleep at all.");
                    A("He asked you in. Do you understand what that is? A door, opened from the inside.");
                    I("He's my brother.");
                    A("He is a door. They are all doors, daughter. Most of them will never open for you again.");
                    I("He said the Lamplighters work for the Institute. Penrose's guild.");
                    A("Of course they do. Who else would they trust with the light?");
                    break;

                case "m03":
                    d.Title = "The Drums";
                    N("Fish scales in her hair, and the harbourmaster's manifest folded in her sleeve: glass and iron for the Institute, by sea, paid for by men who sign with initials.");
                    A("You found my shrine. My little candles, left in the dark by the ones who remember.");
                    A("Listen. Not with your ears. There, in the thing that was your heart. Every one of them is a drum.");
                    I("I can hear them. Dozens. Hundreds.");
                    A("Thousands, daughter. And only one of you. Learn to choose which drum to stop.");
                    break;

                case "m04":
                    d.Title = "The Weirside Beast";
                    N("By morning there are broadsheets on every wall: THE WEIRSIDE BEAST, and a drawing of a wolf with a woman's face.");
                    I("They've made me a monster in a penny print.");
                    A("They have made you famous. Fear is a kind of worship. It is the oldest kind.");
                    N("The Watch doubles its patrols. South of the river, men in black coats with brass lanterns are packing their trunks.");
                    A("The Vigil is coming. I remember them. They were young once, too.");
                    break;

                case "m05":
                    d.Title = "The Guildmaster Talks";
                    N("Penrose talks until dawn in a boathouse, with her wrists tied and a lamp she is not allowed to light.");
                    A("She sold them the light. Every lamp in Ostmere, burning for the men who chained me.");
                    I("She kept saying she only lit the lamps. As if that were nothing.");
                    if (f("tobias_letter"))
                    {
                        I("'Come back.' He wrote it, and he never sent it.");
                        A("Then he is braver on paper than in person. Most of them are.");
                    }
                    if (c.Tobias == "thrall")
                        A("And your brother carries your will behind his eyes now. You did that. Do not pretend you did not like it.");
                    A("A masque at Ashcombe House. Masks, daughter. They will hide their faces and show you their names.");
                    break;

                case "m06":
                    d.Title = "Four Names";
                    N("Lord Ashcombe. Magistrate Crane. Bishop Lowell. And a fourth, who seals his letters with thirteen rays and is called only the Lantern-Master.");
                    I("I danced with one of them. He said I had cold hands.");
                    A("You will warm them on him later.");
                    N("By noon the Vigil has her description. By dusk, every bridge in the city is a checkpoint.");
                    A("Running water, daughter. They think it holds you. It does. Find another way across.");
                    break;

                case "m07":
                    d.Title = "The Fen Road";
                    N("She lies in the reeds past the Fen Cut, mud to the shoulders, while the sky goes grey.");
                    if (f("heard_hollin"))
                    {
                        I("Marrowmere. They've sent the hound-woman to Marrowmere.");
                        A("To your mother's village. Sister Hollin knows exactly where to wait for you.");
                    }
                    else A("There is a woman with dogs on the fen road, daughter. She has your scent already.");
                    A("And the Vigil has opened a book with your name on the first page. Every trick you play twice, they write down. Play it a third time and they will be waiting for it.");
                    I("Then I won't play anything twice.");
                    break;

                case "m08":
                    d.Title = "Hollin";
                    if (f("hollin_thrall"))
                    {
                        N("Sister Maud Hollin sits by the drove road with her hands in her lap, listening to a voice that isn't there.");
                        S("Hollin", "The Captain walks his men by the Mercy canal on Tuesdays. I'll give you every road he keeps.");
                        A("A hunter for a hound. You are learning to keep things, daughter.");
                    }
                    else
                    {
                        N("They bury Sister Hollin by the drove road, and her hounds will not leave the grave.");
                        A("She hunted us her whole life. She was very good at it. Now she is very quiet.");
                        I("She knew this village better than I do. She'd been waiting in my mother's house.");
                    }
                    if (f("marrow_locket"))
                    {
                        I("Mother's locket. She kept a curl of my hair in it. From before.");
                        A("Keep it. Before is a country you cannot go back to. It is good to carry a coin from there.");
                    }
                    A("Now, Coldwater. Your Institute, daughter. They are still keeping children in cells.");
                    break;

                case "m09":
                    d.Title = "Number Nine";
                    N("IX. MARROW, I. DECEASED. Record closed.");
                    I("Number nine. They gave me a number before they gave me a grave.");
                    if (f("clement_saved"))
                        S("Clement", "I closed your record so they would stop looking for you. It was the only kindness I knew how to do with a pen.");
                    if (f("saule_notes_burned"))
                    {
                        I("His notes burned well.");
                        A("He will write them again. Men like Saule always keep another copy in their heads.");
                    }
                    if (f("knows_undercroft"))
                        A("And now you know where I am. Under St Vesper's, daughter. I have listened to them sing hymns over my head for two hundred and eleven years.");
                    else A("Closer. You are so much closer.");
                    A("The sunstones come from the gasworks by the river. Every new lamp in the city drinks from the same pipe.");
                    break;

                case "m10":
                    d.Title = "Lights Out";
                    N("Ostmere goes dark at a quarter past two. Every street lamp in every district goes out at once, and in the morning nobody can say when they will come back.");
                    A("Do you hear it? The whole city holding its breath.");
                    I("They'll bring torches. Braziers. Searchlights.");
                    A("Fire is honest, at least. You can see fire coming.");
                    A("The Council will meet at the opera. They always go to the opera when they are frightened. The music helps them pretend.");
                    break;

                case "m11":
                    d.Title = "Three Empty Boxes";
                    N("Three boxes emptied inside a minute and a half. The papers call it a fire. Nobody who sat in the stalls believes them.");
                    if (f("watch_vigil_split"))
                        A("And the Watch blames the Vigil, and the Vigil blames the Watch. Men are very easy, daughter, once they are afraid of each other.");
                    I("Vane will come for me now. Properly.");
                    A("He has been coming for you all along. Now he will stop being polite about it.");
                    A("His bastion is the old Vigil keep. He keeps his books there. Read them before he burns them, or burn them before he reads you.");
                    break;

                case "m12":
                    d.Title = "The End of His Book";
                    if (f("vane_dead"))
                    {
                        N("Inquisitor-Captain Aldric Vane is laid out in the keep chapel. His night book lies open at a page with her name on it.");
                        A("You closed his book, daughter.");
                    }
                    else
                    {
                        N(f("archive_burned")
                            ? "Vane wakes on the keep floor to smoke, and the smell of his own archive burning."
                            : "Vane wakes on the keep floor with a split lip and a page he has not finished writing.");
                        A("You left him alive. He will not thank you for it. He will come to the cathedral himself.");
                    }
                    if (f("read_vane_book"))
                        I("He knew every roof I'd run and every lamp I'd put out. He wanted to see if I'd read him back.");
                    if (f("faithful_bastion"))
                        A("And the Faithful came out of the Wick to fight for you. Nobody has done that for anyone since me.");
                    N("The Bishop gives the city to the Vigil. Curfew from the evening bell.");
                    A("One night left, daughter. Then down. Always down.");
                    break;

                case "m13":
                    d.Title = "The West Doors";
                    N("There is no day to sleep through. She stands inside the west doors of St Vesper's with dawn coming grey through the rose window, and the stair under the chancel is singing.");
                    if (c.Tobias == "lost")
                    {
                        I("Tobias.");
                        A("Do not look back, daughter. There is nothing behind you now but the city.");
                    }
                    else if (c.Tobias == "thrall")
                        N("Somewhere in the Wick, Tobias keeps his candles lit, because she told him to.");
                    else
                    {
                        S("Tobias", "Whatever's down there, Miss Marrow, you come back up. Promise me.");
                        I("I don't make promises in churches.");
                    }
                    if (terror)
                        A("They hide from you now. Do you feel it? Every bolted door in Ostmere is bolted against you.");
                    else
                        A("They leave candles in their windows for you now. I have not yet decided whether that is a good thing.");
                    A("Come down to me. I have waited so long to see your face.");
                    break;

                default:
                    return null;
            }
            return d;
        }

        /// <summary>The dream as a Journal page: narration plain, speech as "Speaker: line".</summary>
        public static string AsText(Dream d)
        {
            var sb = new StringBuilder();
            foreach (var l in d.Lines)
            {
                if (sb.Length > 0) sb.Append("\n\n");
                sb.Append(string.IsNullOrEmpty(l.Speaker) ? l.Text : l.Speaker + ": " + l.Text);
            }
            return sb.ToString();
        }

        /// <summary>Marks the dream seen and files it in the Journal. Returns false if it had been seen already.</summary>
        public static bool Record(Dream d, CampaignState c)
        {
            if (d == null || c == null || c.Flag(d.Id)) return false;
            c.SetFlag(d.Id);
            c.DiscoverLore(d.Id, "Blood-dream: " + d.Title, AsText(d), d.MissionId);
            return true;
        }
    }
}
