// Example tests: copy the shape. One [AuditTest] per card behaviour; name tests <CardId>_<Behaviour>.
using System.Linq;
using SabberStoneCore.Model.Entities;
using static Audit.H;

namespace Audit
{
    internal static class Examples
    {
        [AuditTest]
        static void Example_Smoke()
        {
            var g = NewGame();
            var p = P1(g);
            Eq("hand starts empty", 0, p.HandZone.Count);
            var yeti = (Minion)PlayNew(g, p, Yeti);
            Eq("yeti on board", 1, p.BoardZone.Count);
            Eq("yeti 4 attack", 4, Atk(yeti));
            Round(g);
            Check("back on player 1", g.CurrentPlayer == p, Describe(g));
            var clone = Clone(g);
            Eq("clone keeps the yeti", yeti.Health, At(clone, yeti).Health);
        }

        // Bloated Leech (EDR_810t): "At the end of your turn, your hero steals 1 Health from the lowest Health enemy."
        // Only an enemy minion with less Health than the enemy hero is the victim here.
        [AuditTest]
        static void Example_EDR_810t_StealsFromLowestMinion()
        {
            var g = NewGame();
            var p = P1(g); var o = P2(g);
            var leech = Board(p, "EDR_810t");
            var foe = Board(o, Yeti); // 4/5 vs boss hero at 30
            int heroBefore = p.Hero.BaseHealth;
            EndTurn(g);
            Note(Describe(g));
            Eq("victim minion loses 1 Health", 4, Hp(foe));
            Eq("player hero max Health +1", heroBefore + 1, p.Hero.BaseHealth);
        }
    }
}
