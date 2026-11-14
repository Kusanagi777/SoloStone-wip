// Regression tests for UngoroCardsGen.cs fixes.
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Audit.H;

namespace Audit
{
    internal static class UngoroFix
    {
        static Game G() => NewGame(s => s.PlayerHero = "HERO_02");

        [AuditTest] static void UNG_208_ElementalLastTurn_SummonsTwoRockElementals()
        {
            {
                var g = G(); var p = P1(g);
                var e = Hand(p, "UNG_208");
                Check("playable without a target", Play(g, e), Describe(g));
                Eq("no Elemental last turn: only Sentinel", 1, p.BoardZone.Count);
            }
            {
                var g = G(); var p = P1(g);
                PlayNew(g, p, "UNG_809t1");
                Round(g); ClearHand(p);
                foreach (var m in p.BoardZone.ToList()) Kill(g, m);
                var e = Hand(p, "UNG_208");
                Check("played", Play(g, e), Describe(g));
                var others = p.BoardZone.Where(m => m != e).ToList();
                Eq("two Elementals summoned", 2, others.Count);
                Check("both are UNG_208t", others.All(m => m.Card.Id == "UNG_208t"), string.Join(",", others.Select(m => m.Card.Id)));
                Check("each 2/3", others.All(m => m.AttackDamage == 2 && m.Health == 3), Describe(g));
                Check("each has Taunt", others.All(m => m.HasTaunt), Describe(g));
                Check("they are Elementals", others.All(m => m.Card.IsRace(Race.ELEMENTAL)), "");
                Check("Sentinel is in the middle", p.BoardZone[1] == e, Describe(g));
                var c = Clone(g);
                Eq("clone keeps 3 minions", 3, At(c, p).BoardZone.Count);
            }
        }

        // Other UNG_809t1 users (add to hand) unchanged: UNG_809 Fire Fly battlecry.
        [AuditTest] static void UNG_809_StillAddsFlameElemental()
        {
            var g = G(); var p = P1(g);
            PlayNew(g, p, "UNG_809");
            Eq("Fire Fly adds UNG_809t1", "UNG_809t1", string.Join(",", HandIds(p)));
        }
    }
}
