using System.Linq;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Audit.H;

namespace Audit
{
    internal static class StormChaserTests
    {
        static Game G(int seed = 99) => NewGame(s => { s.PlayerHero = "HERO_02"; s.Seed = seed; });

        [AuditTest] static void BOT_291_DrawsSpellCost5Plus()
        {
            for (int seed = 1; seed <= 8; seed++)
            {
                var g = G(seed); var p = P1(g);
                ClearDeck(p);
                DeckTop(p, "ULD_181"); DeckTop(p, "EX1_238"); DeckTop(p, Golem); DeckTop(p, "CS2_039");
                var m = (Minion)PlayNew(g, p, "BOT_291");
                Eq("3/4", 3, Atk(m)); Eq("hp4", 4, Hp(m));
                Eq("seed " + seed + ": drew exactly Earthquake", "ULD_181", string.Join(",", HandIds(p)));
                Eq("deck left 3", 3, p.DeckZone.Count);
                var c = Clone(g);
                Eq("clone keeps hand", "ULD_181", string.Join(",", HandIds(At(c, p))));
            }
            var g2 = G(); var p2 = P1(g2); ClearDeck(p2); DeckTop(p2, "EX1_238"); DeckTop(p2, Golem);
            PlayNew(g2, p2, "BOT_291");
            Eq("no qualifying spell: nothing drawn", 0, p2.HandZone.Count);
            Eq("deck untouched", 2, p2.DeckZone.Count);
            var g3 = G(); var p3 = P1(g3); ClearDeck(p3); DeckTop(p3, "CS2_039"); DeckTop(p3, "EX1_238");
            PlayNew(g3, p3, "BOT_291");
            Eq("cheap spells only: nothing drawn", 0, p3.HandZone.Count);
        }
    }
}
