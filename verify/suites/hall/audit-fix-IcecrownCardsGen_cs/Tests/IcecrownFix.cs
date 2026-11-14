// Regression tests for IcecrownCardsGen.cs fixes.
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Audit.H;

namespace Audit
{
    internal static class IcecrownFix
    {
        [AuditTest]
        static void ICC_450_PlusOneForEachDamagedMinion()
        {
            var c = Cards.FromId("ICC_450");
            Eq("base attack", 3, c[GameTag.ATK]); Eq("base health", 3, c[GameTag.HEALTH]);

            // 1 damaged friendly + 1 damaged enemy minion, 1 undamaged enemy
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var f = Board(p, Yeti); var e = Board(o, Yeti); Board(o, Yeti);
            Damage(g, f, 1); Damage(g, e, 1);
            var d = (Minion)PlayNew(g, p, "ICC_450");
            Eq("2 damaged minions: attack 5", 5, Atk(d));
            Eq("2 damaged minions: health 5", 5, Hp(d));
            var cl = Clone(g); var dc = (Minion)At(cl, d);
            Eq("clone keeps 5 attack", 5, Atk(dc));

            // only own hero damaged
            var g2 = NewGame(); var p2 = P1(g2);
            Damage(g2, p2.Hero, 5);
            var d2 = (Minion)PlayNew(g2, p2, "ICC_450");
            Eq("damaged own hero does not count", 3, Atk(d2));

            // only enemy hero damaged
            var g4 = NewGame(); var p4 = P1(g4); var o4 = P2(g4);
            Damage(g4, o4.Hero, 5);
            var d4 = (Minion)PlayNew(g4, p4, "ICC_450");
            Eq("damaged enemy hero does not count", 3, Atk(d4));

            // only enemy damaged minion
            var g3 = NewGame(); var p3 = P1(g3); var o3 = P2(g3);
            var e3 = Board(o3, Yeti); Damage(g3, e3, 1);
            var d3 = (Minion)PlayNew(g3, p3, "ICC_450");
            Eq("damaged enemy minion counts: attack 4", 4, Atk(d3));
            Eq("damaged enemy minion counts: health 4", 4, Hp(d3));

            // three damaged friendly minions, none enemy
            var g5 = NewGame(); var p5 = P1(g5);
            for (int i = 0; i < 3; i++) { var m = Board(p5, Yeti); Damage(g5, m, 2); }
            var d5 = (Minion)PlayNew(g5, p5, "ICC_450");
            Eq("3 damaged friendly: attack 6", 6, Atk(d5));
        }
    }
}
