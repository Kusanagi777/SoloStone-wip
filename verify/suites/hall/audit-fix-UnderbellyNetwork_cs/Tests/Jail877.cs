// Regression: JAIL_877 Underbelly Network refreshes on its owner's next turn (no extended cooldown).
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Audit.H;

namespace Audit
{
    internal static class Jail877
    {
        static int Rats(Controller p) => p.BoardZone.Count(m => m.Card.Id == "JAIL_877t");
        static string Tags(IPlayable l) => $"EXH {l[GameTag.EXHAUSTED]} CD {l[GameTag.LOCATION_ACTION_COOLDOWN]} DMG {l[GameTag.DAMAGE]}";

        [AuditTest]
        static void JAIL_877_EveryOwnerTurn()
        {
            var g = NewGame(); var p = P1(g);
            var loc = PlayNew(g, p, "JAIL_877");
            Check("on board", loc.Zone?.Type == Zone.PLAY, Describe(g));
            Check("turn 1 activate", g.Process(new ActivateLocationTask(p, loc)), Tags(loc));
            Eq("one rat", 1, Rats(p));
            var rat = p.BoardZone.First(m => m.Card.Id == "JAIL_877t");
            Eq("rat atk", 2, Atk(rat)); Eq("rat hp", 1, Hp(rat));
            Eq("no cooldown tag set", 0, loc[GameTag.LOCATION_ACTION_COOLDOWN]);
            Check("not again this turn", !g.Process(new ActivateLocationTask(p, loc)) && Rats(p) == 1, Tags(loc));
            EndTurn(g);
            Check("opponent turn: still exhausted", loc[GameTag.EXHAUSTED] != 0, Tags(loc));
            EndTurn(g);
            Check("owner turn 3 activate", g.Process(new ActivateLocationTask(p, loc)), Tags(loc));
            Eq("two rats", 2, Rats(p));
            Check("2 charges spent: location gone", loc.Zone?.Type != Zone.PLAY, $"zone {loc.Zone?.Type} " + Tags(loc));
        }

        [AuditTest]
        static void JAIL_877_CloneKeepsReadiness()
        {
            var g = NewGame(); var p = P1(g);
            var loc = PlayNew(g, p, "JAIL_877");
            g.Process(new ActivateLocationTask(p, loc));
            Round(g);
            var c = Clone(g); var cp = At(c, p); var cl = (IPlayable)At(c, loc);
            Check("clone activates on owner's next turn", c.Process(new ActivateLocationTask(cp, cl)), Tags(cl));
            Eq("clone two rats", 2, Rats(cp));
        }

        [AuditTest]
        static void JAIL_877t_DeathrattleDraws()
        {
            var g = NewGame(); var p = P1(g);
            var r = Board(p, "JAIL_877t");
            int deck = p.DeckZone.Count;
            Kill(g, r);
            Eq("drew a card", 1, p.HandZone.Count);
            Eq("deck -1", deck - 1, p.DeckZone.Count);
        }
    }
}
