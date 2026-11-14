// Regression: SUMMON ("whenever you summon") triggers for minions summoned by Deathrattles during combat death
// processing must resolve before the attack's Process returns.
using System.Linq;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Audit.H;

namespace Audit
{
    internal static class SummonTiming
    {
        [AuditTest]
        static void CORE_EX1_509_GainsAttackOnMurlocSummon()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var t = (Minion)PlayNew(g, p, "CORE_EX1_509");
            Eq("1/2 after own play", "1/2", $"{Atk(t)}/{Hp(t)}");
            PlayNew(g, p, "GVG_064");
            Eq("+1 from played murloc", 2, Atk(t));
            // die through a real game action (attack into a 7/7) so summon triggers resolve inside g.Process
            var th = Board(p, "UNG_010"); var wall = Board(o, Golem); Attack(g, th, wall);
            Check("threshadon died", th.IsDead || !p.BoardZone.Contains(th), Describe(g));
            Eq("+3 from three summoned murlocs", 5, Atk(t));
            PlayNew(g, p, Yeti);
            Eq("non-murloc: no", 5, Atk(t));
            var oth = Board(o, "UNG_010"); var myG = Board(p, Golem); Attack(g, myG, oth);
            Check("enemy murlocs summoned", o.BoardZone.Count(x => x.Card.Id == "CFM_310t") == 3, Describe(g));
            Eq("enemy murlocs: no", 5, Atk(t));
            Check("EX1_509e present", EnchantIds(t).Contains("EX1_509e"));
            Eq("health unchanged", 2, Hp(t));
            var c = Clone(g); Eq("clone", 5, Atk(At(c, t)));
            Board(p, "CFM_310t");
            Note("after harness Board() murloc: atk " + Atk(t) + " (Board uses SummonBlock directly)");
        }

        // DEFECT repro: murlocs summoned by a Deathrattle of a minion that died in combat do not buff Tidecaller
        // until a later action; Tidecaller then attacks with its old Attack. A spell kill of the same minion works.
        [AuditTest]
        static void CORE_EX1_509_CombatDeathrattleSummonTiming()
        {
            foreach (var tid in new[] { "CORE_EX1_509", "EX1_509" })
            {
                var g = NewGame(); var p = P1(g); var o = P2(g);
                var t = (Minion)PlayNew(g, p, tid);
                var th = Board(p, "UNG_010"); var wall = Board(o, Golem);
                Attack(g, th, wall);                      // Threshadon dies, summons three 1/1 Murlocs
                Eq(tid + " three murlocs summoned", 3, p.BoardZone.Count(x => x.Card.Id == "CFM_310t"));
                Eq(tid + " +3 Attack right after the summon", 4, Atk(t));
                t.IsExhausted = false;
                g.Process(SabberStoneCore.Tasks.PlayerTasks.MinionAttackTask.Any(p, t, o.Hero));
                Eq(tid + " next attack hits for 4", 4, 30 - o.Hero.Health);
                // enemy-turn combat death of the Threshadon
                var g2 = NewGame(); var p2 = P1(g2); var o2 = P2(g2);
                var t2 = Board(p2, tid); var th2 = Board(p2, "UNG_010");
                EndTurn(g2); ClearHand(o2);
                var bg = Board(o2, Golem); Attack(g2, bg, th2);
                Eq(tid + " enemy-turn combat death: +3", 4, Atk(t2));
                // control: spell kill works
                var g3 = NewGame(); var p3 = P1(g3);
                var t3 = Board(p3, tid); var th3 = Board(p3, "UNG_010");
                Damage(g3, th3, 1); PlayNew(g3, p3, "CS2_029", th3);
                Eq(tid + " spell death: +3 (control)", 4, Atk(t3));
            }
        }
    }
}
