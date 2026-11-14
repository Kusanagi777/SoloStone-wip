using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Audit.H;

namespace Audit
{
    internal static class TgtFixTests
    {
        static void Disc(Game g, Controller c, IPlayable card)
        {
            Generic.DiscardBlock.Invoke(c, card);
            g.DeathProcessingAndAuraUpdate();
        }

        [AuditTest]
        static void AT_021_GainsOnlyOnFriendlyDiscard()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var k = Board(p, "AT_021");
            Disc(g, p, Hand(p, Wisp));
            Eq("friendly discard atk", 4, Atk(k)); Eq("friendly discard hp", 3, Hp(k));
            Check("AT_021e applied", EnchantIds(k).Contains("AT_021e"), string.Join(",", EnchantIds(k)));
            Disc(g, o, Hand(o, Wisp));
            Eq("enemy discard ignored atk", 4, Atk(k)); Eq("enemy discard ignored hp", 3, Hp(k));
            Disc(g, p, Hand(p, Wisp));
            Eq("second friendly discard atk", 5, Atk(k)); Eq("hp", 4, Hp(k));
            // enemy Knight reacts only to its own side
            var ek = Board(o, "AT_021");
            Disc(g, p, Hand(p, Wisp));
            Eq("enemy knight ignores my discard", 3, Atk(ek));
            Eq("my knight gains", 6, Atk(k));
            Disc(g, o, Hand(o, Wisp));
            Eq("enemy knight gains on its discard", 4, Atk(ek));
            Eq("my knight unchanged", 6, Atk(k));
            // via a real discard card (Soulfire) on my side
            Hand(p, Wisp);
            PlayNew(g, p, "EX1_308", o.Hero);
            Eq("soulfire discard buffs", 7, Atk(k));
            var c = Clone(g); var ck = (Minion)At(c, k);
            Disc(c, At(c, o), Hand(At(c, o), Wisp));
            Eq("clone: enemy discard ignored", 7, Atk(ck));
            Disc(c, At(c, p), Hand(At(c, p), Wisp));
            Eq("clone: friendly discard buffs", 8, Atk(ck));
        }

        [AuditTest]
        static void KAR_089_StillFriendlyOnly()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            Board(p, "KAR_089"); DeckTop(p, Wisp); DeckTop(p, Wisp);
            Disc(g, o, Hand(o, Wisp));
            Eq("enemy discard: no draw", 0, HandIds(p).Count());
            Disc(g, p, Hand(p, Yeti));
            Eq("friendly discard draws", 1, HandIds(p).Count());
        }
    }
}
