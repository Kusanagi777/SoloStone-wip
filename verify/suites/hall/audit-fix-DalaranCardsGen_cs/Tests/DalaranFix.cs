// Regression tests for DalaranCardsGen.cs fixes.
using System.Linq;
using SabberStoneCore.Model.Entities;
using static Audit.H;

namespace Audit
{
    internal static class DalaranFix
    {
        // Omega Devastator (DAL_770): "Battlecry: If you have 10 Mana Crystals, deal 10 damage to a minion."
        [AuditTest]
        static void DAL_770_TenDamageAtTenCrystals()
        {
            var g = NewGame();
            var p = P1(g); var o = P2(g);
            var golem = Board(o, Golem);
            var own = Board(p, Yeti);
            var d = Hand(p, "DAL_770");
            var targets = LegalTargets(d);
            string list = "targets [" + string.Join(",", targets.Select(x => x.Card.Id)) + "]";
            Check("10 crystals: enemy minion is a legal target", targets.Any(x => x.Id == golem.Id), list);
            Check("10 crystals: friendly minion is a legal target", targets.Any(x => x.Id == own.Id), list);
            Check("heroes are not targets", !targets.Any(x => x is Hero), list);
            Check("target is optional", CanPlay(d), "untargeted play refused");

            var clone = Clone(g);
            var cd = At(clone, d); var cgolem = At(clone, golem);
            Check("play on golem", Play(g, d, golem), Describe(g));
            Check("golem destroyed by 10 damage", !o.BoardZone.Contains(golem), Describe(g));
            Eq("own yeti untouched", 5, Hp(own));
            Check("devastator on board", p.BoardZone.Contains((Minion)d));

            Check("clone: play on golem", Play(clone, cd, cgolem), Describe(clone));
            Check("clone: golem destroyed", !P2(clone).BoardZone.Contains(cgolem), Describe(clone));
        }

        [AuditTest]
        static void DAL_770_NoTargetBelowTen()
        {
            var g = NewGame(s => s.Mana = 9);
            var p = P1(g); var o = P2(g);
            var golem = Board(o, Golem);
            var d = Hand(p, "DAL_770");
            Mana(p, 9);
            Eq("9 crystals: no legal targets", 0, LegalTargets(d).Count);
            Check("9 crystals: playable", CanPlay(d));
            Check("9 crystals: target rejected", !CanPlay(d, golem));
            Play(g, d, null, 0, -1, true);
            Eq("no damage at 9", 7, Hp(golem));
            Check("on board", p.BoardZone.Contains((Minion)d), Describe(g));
        }

        [AuditTest]
        static void DAL_770_EmptyBoardAtTen()
        {
            var g = NewGame();
            var p = P1(g); var o = P2(g);
            var d = Hand(p, "DAL_770");
            Eq("no minions: no targets", 0, LegalTargets(d).Count);
            Check("no minions: playable untargeted", CanPlay(d));
            int bossHp = o.Hero.Health;
            Play(g, d);
            Check("on board", p.BoardZone.Contains((Minion)d), Describe(g));
            Eq("boss hero untouched", bossHp, o.Hero.Health);
        }
    }
}
