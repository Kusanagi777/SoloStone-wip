// Regression tests for TIME_211 Lady Azshara locations: one-turn readiness (no extra cooldown), effects intact.
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Audit.H;

namespace Audit
{
    internal static class LadyAzsharaTests
    {
        static bool ActivateLoc(Game g, Controller c, IPlayable loc, IPlayable target = null)
            => g.Process(new ActivateLocationTask(c, loc, target));
        static BoardObject Loc(Controller c, string id) => c.BoardZone.GetLocations().FirstOrDefault(l => l.Card.Id == id);
        static string Tags(Game g, BoardObject l) => $"turn {g.Turn}: EXHAUSTED {l[GameTag.EXHAUSTED]} COOLDOWN {l[GameTag.LOCATION_ACTION_COOLDOWN]} dur {l.Durability}";

        static void WellCase(string id)
        {
            var g = NewGame(s => s.PlayerHero = "HERO_06"); var p = P1(g);
            PlayNew(g, p, id);
            var loc = Loc(p, id);
            Check(id + " on board", loc != null, Describe(g)); if (loc == null) return;
            Check(id + " activates", ActivateLoc(g, p, loc), Tags(g, loc));
            Eq(id + " fills hand", 10, p.HandZone.Count);
            Check(id + " all Temporary spells", p.HandZone.All(c => c.Card.Type == CardType.SPELL), string.Join(",", HandIds(p)));
            if (id == "TIME_211t1t")
                Check(id + " all marked Eternalized", p.HandZone.All(c => EnchantIds(c).Contains("TIME_211t1te")));
            Check(id + " not reusable same turn", !LocationRules.CanActivate(p, loc, null), Tags(g, loc));
            EndTurn(g);
            Eq(id + " Temporary spells discarded at end of turn", 0, p.HandZone.Count);
            Check(id + " not usable on enemy turn", !LocationRules.CanActivate(P2(g), loc, null), Tags(g, loc));
            EndTurn(g); ClearHand(p);
            Check(id + " usable again on your next turn", LocationRules.CanActivate(p, loc, null), Tags(g, loc));
            var clone = Clone(g);
            Check(id + " readiness survives clone", LocationRules.CanActivate(At(clone, p), At(clone, loc) as BoardObject, null));
            Check(id + " second activation", ActivateLoc(g, p, loc), Tags(g, loc));
            Eq(id + " second fill", 10, p.HandZone.Count);
        }

        [AuditTest] static void TIME_211t1_ReusableNextTurn() => WellCase("TIME_211t1");
        [AuditTest] static void TIME_211t1t_ReusableNextTurn() => WellCase("TIME_211t1t");

        static void CityCase(string id)
        {
            var g = NewGame(s => s.PlayerHero = "HERO_06"); var p = P1(g);
            var y = Board(p, Yeti);
            PlayNew(g, p, id);
            var loc = Loc(p, id);
            Check(id + " on board", loc != null, Describe(g)); if (loc == null) return;
            Check(id + " activates on friendly", ActivateLoc(g, p, loc, y), Tags(g, loc));
            Eq(id + " copy summoned", 2, p.BoardZone.Count(m => m.Card.Id == Yeti));
            if (id == "TIME_211t2t")
            {
                var copy = p.BoardZone.Where(m => m.Card.Id == Yeti).Last();
                Eq(id + " copy attack doubled", 8, Atk(copy)); Eq(id + " copy health doubled", 10, MaxHp(copy));
            }
            Check(id + " not reusable same turn", !LocationRules.CanActivate(p, loc, y), Tags(g, loc));
            Round(g);
            Check(id + " usable again on your next turn", LocationRules.CanActivate(p, loc, y), Tags(g, loc));
            Check(id + " second activation", ActivateLoc(g, p, loc, y), Tags(g, loc));
            Eq(id + " second copy", 3, p.BoardZone.Count(m => m.Card.Id == Yeti));
        }

        [AuditTest] static void TIME_211t2_ReusableNextTurn() => CityCase("TIME_211t2");
        [AuditTest] static void TIME_211t2t_ReusableNextTurn() => CityCase("TIME_211t2t");
    }
}
