// More SUMMON-trigger timing regressions for Game.DeathProcessingAndAuraUpdate.
using System.Linq;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Audit.H;

namespace Audit
{
    internal static class SummonTiming2
    {
        // Tidecaller buffed once (not again on the next action), and the state survives a clone.
        [AuditTest]
        static void CORE_EX1_509_CombatSummonNoDoubleTrigger()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var t = (Minion)PlayNew(g, p, "CORE_EX1_509");
            var th = (Minion)PlayNew(g, p, "UNG_010"); var wall = Board(o, Golem);
            Attack(g, th, wall);
            Eq("+3 right after the attack", 4, Atk(t));
            Eq("SummonedMinions drained", 0, g.SummonedMinions.Count);
            var c = Clone(g); Eq("clone keeps +3", 4, Atk(At(c, t)));
            EndTurn(g);
            Eq("still 4 after end turn (no double trigger)", 4, Atk(t));
            Round(g);
            Eq("still 4 after another round", 4, Atk(t));
        }

        // Another SUMMON trigger: Starving Buzzard draws for each Hyena from a combat-killed Savannah Highmane.
        [AuditTest]
        static void CS2_237_CombatDeathrattleSummonDraws()
        {
            var g = NewGame(s => s.PlayerDeck = Enumerable.Repeat(Wisp, 10).ToList()); var p = P1(g); var o = P2(g);
            PlayNew(g, p, "CS2_237"); var hm = (Minion)PlayNew(g, p, "EX1_534");   // Highmane is a Beast: draws 1
            int before = p.HandZone.Count;
            Board(o, Golem); var wall = Board(o, Golem);
            Attack(g, hm, wall);
            Eq("two hyenas", 2, p.BoardZone.Count(x => x.Card.Id == "EX1_534t"));
            Eq("drew 2 at once", before + 2, p.HandZone.Count);
            // enemy-turn combat: the boss's Golem attacks the Highmane
            var g2 = NewGame(s => s.PlayerDeck = Enumerable.Repeat(Wisp, 10).ToList()); var p2 = P1(g2); var o2 = P2(g2);
            PlayNew(g2, p2, "CS2_237"); var hm2 = (Minion)PlayNew(g2, p2, "EX1_534");
            EndTurn(g2);
            int before2 = p2.HandZone.Count;
            Board(o2, Golem); var bg = Board(o2, Golem);
            Attack(g2, bg, hm2);
            Eq("enemy-turn: two hyenas", 2, p2.BoardZone.Count(x => x.Card.Id == "EX1_534t"));
            Eq("enemy-turn: drew 2 at once", before2 + 2, p2.HandZone.Count);
        }
    }
}
