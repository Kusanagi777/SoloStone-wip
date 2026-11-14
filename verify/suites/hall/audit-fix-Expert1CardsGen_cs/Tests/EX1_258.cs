using System.Linq;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Audit.H;

namespace Audit
{
    internal static class UnboundElementalTests
    {
        static Game G() => NewGame(s => s.PlayerHero = "HERO_02");

        [AuditTest] static void EX1_258_GrowsOnOverloadCards()
        {
            var g = G(); var p = P1(g); var o = P2(g);
            var u = Board(p, "EX1_258");
            Eq("2/4", 2, Atk(u)); Eq("hp4", 4, Hp(u));
            PlayNew(g, p, Yeti);
            Eq("non-overload: no buff", 2, Atk(u));
            PlayNew(g, p, "EX1_238", o.Hero);
            Eq("spell w/ overload +1 atk", 3, Atk(u)); Eq("+1 hp", 5, Hp(u));
            PlayNew(g, p, "AT_052");
            Eq("minion w/ overload +1", 4, Atk(u));
            PlayNew(g, p, "EX1_247");
            Eq("weapon w/ overload +1", 5, Atk(u)); Eq("hp 7", 7, Hp(u));
            Check("EX1_258e present", EnchantIds(u).Contains("EX1_258e"));
            EndTurn(g); ClearHand(o);
            PlayNew(g, o, "EX1_238", o.Hero);
            Eq("enemy overload spell: no buff", 5, Atk(u));
            PlayNew(g, o, "AT_052");
            Eq("enemy overload minion: no buff", 5, Atk(u));
        }

        [AuditTest] static void EX1_258_EnemyCopyOnlyGrowsForItsOwner()
        {
            var g = G(); var p = P1(g); var o = P2(g);
            var mine = Board(p, "EX1_258");
            var theirs = Board(o, "EX1_258");
            PlayNew(g, p, "EX1_238", o.Hero);
            Eq("mine +1", 3, Atk(mine));
            Eq("theirs unchanged", 2, Atk(theirs));
            EndTurn(g); ClearHand(o);
            PlayNew(g, o, "EX1_238", p.Hero);
            Eq("theirs +1", 3, Atk(theirs));
            Eq("mine unchanged", 3, Atk(mine));
            var c = Clone(g);
            PlayNew(c, At(c, o), "EX1_238", At(c, p).Hero);
            Eq("clone: theirs +1", 4, Atk(At(c, theirs)));
            Eq("clone: mine unchanged", 3, Atk(At(c, mine)));
        }

        [AuditTest] static void EX1_258e_PlusOnePlusOne()
        {
            var g = G(); var p = P1(g); var o = P2(g);
            var u = Board(p, "EX1_258");
            PlayNew(g, p, "EX1_238", o.Hero);
            Eq("max hp 5", 5, MaxHp(u));
            Round(g);
            Eq("persists", 3, Atk(u));
            var c = Clone(g); Eq("clone", 3, Atk(At(c, u)));
        }
    }
}
