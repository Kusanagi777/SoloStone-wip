// Regression tests for the NaxxCardsGen.cs fixes: FP1_023 Dark Cultist, FP1_022 Voidcaller.
using System.Linq;
using SabberStoneCore.Model.Entities;
using static Audit.H;

namespace Audit
{
    internal static class NaxxFix
    {
        const string FlameImp = "EX1_319";

        // "Deathrattle: Give a random friendly minion +3 Health."
        [AuditTest]
        static void FP1_023_Plus3HealthFriendly()
        {
            for (int seed = 1; seed <= 4; seed++)
            {
                var g = NewGame(s => s.Seed = seed); var p = P1(g); var o = P2(g);
                var yeti = Board(p, Yeti);
                var foe = Board(o, Yeti);
                var c = Board(p, "FP1_023");
                Kill(g, c);
                Eq($"seed {seed}: friendly +3 max hp", 8, MaxHp(yeti));
                Eq($"seed {seed}: friendly +3 hp", 8, Hp(yeti));
                Check($"seed {seed}: FP1_023e on yeti", EnchantIds(yeti).Contains("FP1_023e"), Describe(g));
                Eq($"seed {seed}: enemy untouched", 5, MaxHp(foe));
            }
            // combat death
            {
                var g = NewGame(); var p = P1(g); var o = P2(g);
                var yeti = Board(p, Yeti); var c = Board(p, "FP1_023"); var golem = Board(o, Golem);
                Attack(g, c, golem);
                Eq("combat death: friendly +3", 8, MaxHp(yeti));
            }
            // random among several friendlies: exactly one gets +3
            for (int seed = 1; seed <= 6; seed++)
            {
                var g = NewGame(s => s.Seed = seed); var p = P1(g);
                var a = Board(p, Yeti); var b = Board(p, Golem); var c = Board(p, "FP1_023");
                Kill(g, c);
                int got = (MaxHp(a) == 8 ? 1 : 0) + (MaxHp(b) == 10 ? 1 : 0);
                Eq($"seed {seed}: exactly one buffed", 1, got);
                var cl = Clone(g);
                Eq($"seed {seed}: clone keeps buff", MaxHp(a) + MaxHp(b), MaxHp(At(cl, a)) + MaxHp(At(cl, b)));
            }
            // alone: nothing, no crash
            {
                var g = NewGame(); var p = P1(g);
                Kill(g, Board(p, "FP1_023"));
                Eq("alone: board empty", 0, p.BoardZone.Count);
            }
        }

        // "Deathrattle: Put a random Demon from your hand into the battlefield."
        [AuditTest]
        static void FP1_022_DeathrattlePutsDemonFromHand()
        {
            for (int seed = 1; seed <= 4; seed++)
            {
                var g = NewGame(s => s.Seed = seed); var p = P1(g);
                Hand(p, Yeti); var imp = Hand(p, FlameImp); Hand(p, Golem);
                Kill(g, Board(p, "FP1_022"));
                Eq($"seed {seed}: imp on board", FlameImp, BoardIds(p).FirstOrDefault());
                Eq($"seed {seed}: no battlecry", 30, Hp(p.Hero));
                Check($"seed {seed}: demon moved, not copied", p.HandZone.Count == 2 && !HandIds(p).Contains(FlameImp) && !p.HandZone.Contains(imp),
                    string.Join(",", HandIds(p)) + " | " + Describe(g));
                Check($"seed {seed}: same entity on board", p.BoardZone.Contains(imp) && imp.Zone == p.BoardZone);
                var cl = Clone(g);
                Eq($"seed {seed}: clone hand 2", 2, At(cl, p).HandZone.Count);
                Eq($"seed {seed}: clone board 1", 1, At(cl, p).BoardZone.Count);
            }
            var g2 = NewGame(); var p2 = P1(g2); Hand(p2, Yeti);
            Kill(g2, Board(p2, "FP1_022"));
            Eq("no demon: nothing summoned", 0, p2.BoardZone.Count);
            Eq("no demon: hand kept", 1, p2.HandZone.Count);
        }

        [AuditTest]
        static void FP1_022_DemonLeavesHandByCombatDeath()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var imp = Hand(p, FlameImp);
            var v = Board(p, "FP1_022");
            var foe = Board(o, Golem);
            Attack(g, v, foe);
            Check("demon summoned", p.BoardZone.FirstOrDefault()?.Card.Id == FlameImp, Describe(g));
            Check("the hand demon left the hand", !p.HandZone.Contains(imp) && p.HandZone.Count == 0, Describe(g));
            // the summoned demon is a normal board minion: it can die and go to graveyard once
            Kill(g, (Minion)imp);
            Eq("demon in graveyard once", 1, GraveIds(p).Count(id => id == FlameImp));
            Eq("hand still empty", 0, p.HandZone.Count);
        }
    }
}
