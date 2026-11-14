using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Audit.H;

namespace Audit
{
    internal static class EruptingVolcanoTests
    {
        static BoardObject Loc(Controller c, string id) => c.BoardZone.GetLocations().FirstOrDefault(l => l.Card.Id == id);
        static bool Activate(Game g, Controller c, BoardObject loc, IPlayable target = null) => g.Process(new ActivateLocationTask(c, loc, target));
        static int EnemyTotalHp(Controller o) => o.Hero.Health + o.BoardZone.Sum(m => m.Health);
        static string St(BoardObject l) => $"exhausted={l.IsExhausted} EXHAUSTED={l[GameTag.EXHAUSTED]} cooldown={l[GameTag.LOCATION_ACTION_COOLDOWN]} durability={l.Durability}";

        [AuditTest]
        static void CATA_584_UsableAgainNextTurn()
        {
            var g = NewGame(s => s.PlayerHero = "HERO_01");
            var p = P1(g); var o = P2(g);
            Board(o, Golem); Board(o, Golem);
            PlayNew(g, p, "CATA_584");
            var loc = Loc(p, "CATA_584");
            Check("activated", Activate(g, p, loc), Describe(g));
            Check("exhausted for the rest of this turn", !LocationRules.CanActivate(p, loc, null), St(loc));
            Check("second use same turn refused", !Activate(g, p, loc), St(loc));
            EndTurn(g);
            Check("not usable on the opponent's turn", !LocationRules.CanActivate(p, loc, null), St(loc));
            EndTurn(g);
            Check("can activate on my next turn", LocationRules.CanActivate(p, loc, null), St(loc));
            Check("activated again", Activate(g, p, loc), St(loc));
            Eq("durability 3 -> 1", 1, loc.Durability);
            Round(g);
            Check("third activation next turn", Activate(g, p, loc), Describe(g));
            Check("location gone at 0 durability", Loc(p, "CATA_584") == null, Describe(g));
            // clone keeps the refresh behaviour
            var g2 = NewGame(s => s.PlayerHero = "HERO_01");
            var p2 = P1(g2); Board(P2(g2), Golem);
            PlayNew(g2, p2, "CATA_584");
            Activate(g2, p2, Loc(p2, "CATA_584"));
            var c2 = Clone(g2); var cp = At(c2, p2);
            Round(c2);
            Check("clone: usable next turn", LocationRules.CanActivate(cp, Loc(cp, "CATA_584"), null), St(Loc(cp, "CATA_584")));
            // control: Shrine of Twilight
            var g3 = NewGame(s => s.PlayerHero = "HERO_07");
            var p3 = P1(g3);
            PlayNew(g3, p3, "CATA_492");
            var sh = Loc(p3, "CATA_492");
            Activate(g3, p3, sh); Round(g3);
            Check("control: Shrine usable next turn", LocationRules.CanActivate(p3, sh, null), "");
        }

        [AuditTest]
        static void CATA_584_ThreeSplitAmongEnemies_SixAfterFire()
        {
            for (int seed = 1; seed <= 4; seed++)
            {
                foreach (bool fire in new[] { false, true })
                {
                    var g = NewGame(s => { s.PlayerHero = "HERO_01"; s.Seed = seed; });
                    var p = P1(g); var o = P2(g);
                    var mine = Board(p, Golem);
                    Board(o, Golem); Board(o, Golem);
                    PlayNew(g, p, "CATA_584");
                    var loc = Loc(p, "CATA_584");
                    Check($"[{seed}] on board, 3 durability", loc != null && loc.Durability == 3, Describe(g));
                    if (fire) Play(g, Hand(p, "CATA_582"));
                    int before = EnemyTotalHp(o), mineBefore = Hp(mine);
                    Check($"[{seed}] activated", Activate(g, p, loc), Describe(g));
                    Eq($"[{seed}] fire={fire}: damage to enemies", fire ? 6 : 3, before - EnemyTotalHp(o));
                    Eq($"[{seed}] own golem untouched", mineBefore, Hp(mine));
                    Eq($"[{seed}] own hero untouched", 30, p.Hero.Health);
                }
            }
            var g2 = NewGame(s => s.PlayerHero = "HERO_01");
            var p2 = P1(g2); var o2 = P2(g2);
            Board(o2, Golem);
            PlayNew(g2, p2, "CATA_582");
            Round(g2);
            PlayNew(g2, p2, "CATA_584");
            int b2 = EnemyTotalHp(o2);
            Activate(g2, p2, Loc(p2, "CATA_584"));
            Eq("fire spell last turn: only 3", 3, b2 - EnemyTotalHp(o2));
        }
    }
}
