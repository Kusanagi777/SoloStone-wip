using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Audit.H;

namespace Audit
{
    internal static class FixTroll
    {
        [AuditTest]
        static void TRL_360_AfterYouPlayMinionDeal1()
        {
            var c = Cards.FromId("TRL_360");
            Eq("attack", 2, c[GameTag.ATK]);
            Eq("durability", 4, c[GameTag.DURABILITY]);
            var g = NewGame();
            var p = P1(g); var o = P2(g);
            PlayNew(g, p, "TRL_360");
            var y = (Minion)PlayNew(g, p, Yeti);
            Eq("played minion takes 1", 4, Hp(y));
            var s = Board(p, Yeti);
            Eq("summoned (not played) minion untouched", 5, Hp(s));
            PassTo(g, o); ClearHand(o);
            var e = (Minion)PlayNew(g, o, Yeti);
            Eq("enemy-played minion untouched", 5, Hp(e));
            // clone keeps the restriction and still hits own plays
            PassTo(g, p);
            var cl = Clone(g);
            var cp = At(cl, p);
            var y2 = (Minion)PlayNew(cl, cp, Yeti);
            Eq("clone: played minion takes 1", 4, Hp(y2));
            PassTo(cl, At(cl, o)); ClearHand(At(cl, o));
            var e2 = (Minion)PlayNew(cl, At(cl, o), Yeti);
            Eq("clone: enemy-played minion untouched", 5, Hp(e2));
        }

        [AuditTest]
        static void TRL_360_BossWieldsWhip()
        {
            // The boss wielding the Whip: it hits the boss's own plays, not the player's.
            var g = NewGame();
            var p = P1(g); var o = P2(g);
            PassTo(g, o); ClearHand(o);
            PlayNew(g, o, "TRL_360");
            var e = (Minion)PlayNew(g, o, Yeti);
            Eq("boss-played minion takes 1", 4, Hp(e));
            PassTo(g, p);
            var y = (Minion)PlayNew(g, p, Yeti);
            Eq("player-played minion untouched", 5, Hp(y));
        }
    }
}
