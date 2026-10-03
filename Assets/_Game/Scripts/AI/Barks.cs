using UnityEngine;
using Vespertine.Data;

namespace Vespertine.AI
{
    public enum BarkKind { Suspicious, Investigate, GiveUp, Spotted, Search, SearchEnd, Body, Dazed, Stain, Lamp, Panic, Wake, Hit, Partner, Lockdown, Relight, Smell, Idle, Hunt, HuntLost }

    /// <summary>Contextual voice lines. Faction-flavoured; picked at random.</summary>
    public static class Barks
    {
        static readonly string[] Suspicious = { "Hm?", "What was that?", "Who's there?", "Something moved...", "Hello?" };
        static readonly string[] SuspiciousWatch = { "Halt. Who goes there?", "Show yourself.", "I saw that." };
        static readonly string[] SuspiciousChurch = { "Lord, keep me.", "Is someone in the dark?", "Peace be with you... whoever you are." };
        static readonly string[] SuspiciousVigil = { "There. In the dark.", "Something unclean.", "I smell grave-earth." };
        static readonly string[] Investigate = { "I'll take a look.", "Better check.", "Probably rats." };
        static readonly string[] GiveUp = { "Nothing. Just the fog.", "Nerves. Only nerves.", "Must have been a cat.", "I need a drink." };
        static readonly string[] Spotted = { "There!", "Something's here!", "Over there!" };
        static readonly string[] SpottedWatch = { "Intruder!", "Stop right there!", "To me! To me!" };
        static readonly string[] SpottedVigil = { "Leech! Here!", "The thing is here!", "Silver! Now!" };
        static readonly string[] SpottedCiv = { "Gods! Its eyes!", "Help! Help me!", "Monster!" };
        static readonly string[] Search = { "Spread out!", "It can't have gone far.", "Check the shadows!", "Lanterns up!" };
        static readonly string[] SearchVigil = { "It hides in the dark. Light every corner.", "Check the rooftops.", "It's still here. I can feel it." };
        static readonly string[] SearchEnd = { "Lost it. Stay sharp.", "Gone. For now.", "Back to your posts. Eyes open." };
        static readonly string[] Body = { "A body!", "Gods... he's been bled white!", "Murder! Murder!", "Who did this?" };
        static readonly string[] Dazed = { "Hey! Wake up!", "Are you hurt? Get up!", "Drunk on duty again?" };
        static readonly string[] Stain = { "Is that... blood?", "Fresh blood.", "Someone's bleeding." };
        static readonly string[] Lamp = { "Lamp's gone out.", "Who put out the light?", "Darker than it should be." };
        static readonly string[] Panic = { "Run! Run!", "It's the devil!", "Get away from me!", "Mother save me!" };
        static readonly string[] Wake = { "Something... bit me.", "Where... a woman. A pale woman!", "My neck... the alarm!" };
        static readonly string[] Hit = { "Hit it!", "Got it!", "Again!" };
        static readonly string[] Partner = { "Where'd he get to?", "Oi! Where are you?", "He was right behind me..." };
        static readonly string[] Lockdown = { "The bell! Lock it down!", "Gates! Close the gates!", "Everyone to the lights!" };
        static readonly string[] Relight = { "Let there be light.", "Back on you go.", "Can't have it dark here." };
        static readonly string[] Smell = { "*sniff* ...", "*growl*" };
        static readonly string[] Hunt = { "Closer. Heel.", "I have you, girl.", "West wind. She's that way.", "You can't rest, can you? Nor can I.", "Blood on the reeds. This way." };
        static readonly string[] HuntLost = { "Gone to smoke again.", "Clever. Still here, though.", "Lost her. Cast about, dogs." };
        static readonly string[] Idle = { "Cold one tonight.", "Three more hours.", "Fog's thick.", "Heard about the Institute?", "My feet are killing me." };

        public static string Get(BarkKind k, Archetype a)
        {
            if (a != null && a.Has(ArchFlags.Quadruped))
                return k == BarkKind.Spotted || k == BarkKind.Search ? "*snarl*" : k == BarkKind.Panic ? "*whine*" : Pick(Smell);
            var f = a != null ? a.Faction : Faction.None;
            bool civ = a != null && a.Morale == Morale.Civilian;
            switch (k)
            {
                case BarkKind.Suspicious:
                    return f == Faction.Watch ? Pick(SuspiciousWatch, Suspicious) : f == Faction.Church ? Pick(SuspiciousChurch, Suspicious) : f == Faction.Vigil ? Pick(SuspiciousVigil) : Pick(Suspicious);
                case BarkKind.Investigate: return Pick(Investigate);
                case BarkKind.GiveUp: return Pick(GiveUp);
                case BarkKind.Spotted: return civ ? Pick(SpottedCiv) : f == Faction.Watch ? Pick(SpottedWatch, Spotted) : f == Faction.Vigil ? Pick(SpottedVigil) : Pick(Spotted);
                case BarkKind.Search: return f == Faction.Vigil ? Pick(SearchVigil) : Pick(Search);
                case BarkKind.SearchEnd: return Pick(SearchEnd);
                case BarkKind.Body: return Pick(Body);
                case BarkKind.Dazed: return Pick(Dazed);
                case BarkKind.Stain: return Pick(Stain);
                case BarkKind.Lamp: return Pick(Lamp);
                case BarkKind.Panic: return Pick(Panic);
                case BarkKind.Wake: return Pick(Wake);
                case BarkKind.Hit: return Pick(Hit);
                case BarkKind.Partner: return Pick(Partner);
                case BarkKind.Lockdown: return Pick(Lockdown);
                case BarkKind.Relight: return Pick(Relight);
                case BarkKind.Smell: return Pick(Smell);
                case BarkKind.Hunt: return Pick(Hunt);
                case BarkKind.HuntLost: return Pick(HuntLost);
                default: return Pick(Idle);
            }
        }

        static string Pick(string[] a) => a[Random.Range(0, a.Length)];
        static string Pick(string[] a, string[] b) => Random.value < 0.6f ? Pick(a) : Pick(b);
    }
}
