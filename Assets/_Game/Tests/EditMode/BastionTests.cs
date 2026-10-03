using NUnit.Framework;
using UnityEngine;
using Vespertine.Level;

namespace Vespertine.Tests
{
    /// <summary>M12 Vane's Bastion: straight stair flights, the Dossier choice and the full-Dossier header.</summary>
    public class BastionTests
    {
        static LevelGrid Grid(string rows) => new LevelGrid(MapParser.Parse("@map\n" + rows + "\n"));

        [Test]
        public void StairBetweenTallWallsClimbsToTheFloorAhead()
        {
            // a stair (2,1) between two 6 m buildings, a 3 m gallery ahead (north) and the street behind it
            var g = Grid("..u..\n.HsH.\n.....");
            Assert.IsTrue(LevelBuilder.FindUpNeighbour(g, 2, 1, out var dir, out float top));
            Assert.AreEqual(new Vector2Int(0, -1), dir, "up the flight, not onto the wall top beside it");
            Assert.AreEqual(3f, top, 1e-4f);
        }

        [Test]
        public void StairWithNoStraightFlightTakesTheHighestNeighbour()
        {
            // raised on every side, so no neighbour has open floor behind it: the stair climbs to the tallest neighbour
            var g = Grid("..h..\n.Hs#.\n..#..");
            Assert.IsTrue(LevelBuilder.FindUpNeighbour(g, 2, 1, out var dir, out float top));
            Assert.AreEqual(new Vector2Int(-1, 0), dir, "the 6 m building over the 4.5 m house");
            Assert.AreEqual(6f, top, 1e-4f);
        }

        [Test]
        public void DossierChoiceAndFullDossierHeaderValidate()
        {
            const string map = @"@mission
id = mb
title = Bastion
act = 3
dossier = all
@map
########
#......#
#......#
########
@entities
player 1 1
npc vane hunter 4 1
use dossier 5 2 ""Vane's Dossier"" verb=Take_the_Dossier time=3
use burn 6 2 ""The presses"" sealed=Not_yet.
@objectives
primary dossier ""Take it"" interact dossier
hidden deed ""Settle with Vane"" flag deed needs=dossier
@script
on kill vane: flag vane_down
on interact dossier if !vane_down: reveal deed ; choice vane ""Kill him or burn it"" kill ""Kill him"" burn ""Burn it""
on choice vane.kill: say Ilse ""No more Captain.""
on choice vane.burn: unseal burn
on interact burn: flag deed
";
            var d = MapParser.Parse(map);
            Assert.AreEqual("all", d.Get("dossier", ""));
            Assert.IsEmpty(MissionValidator.Validate(d));
            var bad = MapParser.Parse(map.Replace("on choice vane.burn", "on choice vane.spare"));
            Assert.IsNotEmpty(MissionValidator.Validate(bad), "an answer the choice never offers");
        }
    }
}
