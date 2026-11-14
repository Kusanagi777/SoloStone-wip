using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Audit.H;

namespace Audit
{
    // Regression tests for Generic.JoustBlock: the revealed card must be one of the matching type.
    internal static class JoustFix
    {
        const string Frostbolt = "CS2_024";   // 2-cost spell
        const string Pyroblast = "EX1_279";   // 10-cost spell
        const string Arcane = "CS2_025";      // Arcane Explosion, 2-cost spell (filler)

        static List<string> Yetis(int n) => Enumerable.Repeat(Yeti, n).ToList();
        static List<string> Of(string id, int n) => Enumerable.Repeat(id, n).ToList();

        // The opening hand can draw a card out of a deck given to NewGame, so build both decks after setup.
        // The odd card is placed at a seed-dependent position so a wrong index cannot land on it by luck.
        static Game Joust(int seed, List<string> mine, List<string> theirs)
        {
            var g = NewGame(s => s.Seed = seed);
            Set(P1(g), mine, seed); Set(P2(g), theirs, seed * 7);
            return g;
        }
        static void Set(Controller c, List<string> ids, int rot)
        {
            ClearDeck(c);
            int n = ids.Count, r = n == 0 ? 0 : rot % n;
            foreach (var id in ids.Skip(r).Concat(ids.Take(r))) DeckTop(c, id);
        }

        [AuditTest]
        static void LOOT_170_RevealsSpellsOnly()
        {
            for (int seed = 1; seed <= 10; seed++)
            {
                // ours costs more -> Pyroblast drawn (9 Yetis around it)
                var g = Joust(seed, Yetis(9).Concat(new[] { Pyroblast }).ToList(), Yetis(29).Concat(new[] { Frostbolt }).ToList());
                var p = P1(g);
                PlayNew(g, p, "LOOT_170");
                Eq($"seed {seed}: drew Pyroblast", Pyroblast, string.Join(",", HandIds(p)));
                Check($"seed {seed}: Pyroblast left deck", !DeckIds(p).Contains(Pyroblast), string.Join(",", DeckIds(p)));

                // ours costs less -> nothing drawn
                var g2 = Joust(seed, Yetis(9).Concat(new[] { Frostbolt }).ToList(), Yetis(29).Concat(new[] { Pyroblast }).ToList());
                var p2 = P1(g2);
                PlayNew(g2, p2, "LOOT_170");
                Eq($"seed {seed}: cheaper, nothing drawn", 0, p2.HandZone.Count);

                // opponent has no spell -> ours wins and is drawn; never a minion
                var g3 = Joust(seed, Yetis(9).Concat(new[] { Frostbolt }).ToList(), Yetis(30));
                var p3 = P1(g3);
                PlayNew(g3, p3, "LOOT_170");
                Eq($"seed {seed}: opponent no spell -> Frostbolt drawn", Frostbolt, string.Join(",", HandIds(p3)));

                // we have no spell -> nothing drawn
                var g4 = Joust(seed, Yetis(10), Yetis(29).Concat(new[] { Frostbolt }).ToList());
                var p4 = P1(g4);
                PlayNew(g4, p4, "LOOT_170");
                Eq($"seed {seed}: we have no spell -> nothing drawn", 0, p4.HandZone.Count);
            }
        }

        [AuditTest]
        static void LOOT_170_WorksAfterClone()
        {
            var g = Joust(3, Yetis(9).Concat(new[] { Pyroblast }).ToList(), Yetis(29).Concat(new[] { Frostbolt }).ToList());
            var c = Clone(g);
            var p = P1(c);
            PlayNew(c, p, "LOOT_170");
            Eq("clone: drew Pyroblast", Pyroblast, string.Join(",", HandIds(p)));
        }

        [AuditTest]
        static void AT_058_MinionJoustRevealsMinionsOnly()
        {
            // King's Elekk: "Battlecry: Reveal a minion in each deck. If yours costs more, draw it."
            for (int seed = 1; seed <= 10; seed++)
            {
                // our only minion is a Golem (7) among 9 spells; boss has a Wisp (1) among 29 spells
                var g = Joust(seed, Of(Arcane, 9).Concat(new[] { Golem }).ToList(), Of(Arcane, 29).Concat(new[] { Wisp }).ToList());
                var p = P1(g);
                PlayNew(g, p, "AT_058");
                Eq($"seed {seed}: drew Golem", Golem, string.Join(",", HandIds(p)));

                // our Wisp vs boss Golem -> nothing drawn, and never a spell
                var g2 = Joust(seed, Of(Arcane, 9).Concat(new[] { Wisp }).ToList(), Of(Arcane, 29).Concat(new[] { Golem }).ToList());
                var p2 = P1(g2);
                PlayNew(g2, p2, "AT_058");
                Eq($"seed {seed}: lost joust, nothing drawn", 0, p2.HandZone.Count);
            }
            // all-minion decks unchanged: Golems vs Wisps always wins
            var g3 = Joust(1, Of(Golem, 10), Of(Wisp, 30));
            var p3 = P1(g3);
            PlayNew(g3, p3, "AT_058");
            Eq("all-minion decks: drew Golem", Golem, string.Join(",", HandIds(p3)));
        }
    }
}
