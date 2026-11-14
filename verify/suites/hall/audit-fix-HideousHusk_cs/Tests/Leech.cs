// Regression tests for EDR_810t Bloated Leech (HideousHusk.ConfigureLeech) and the cards that make it.
// "At the end of your turn, your hero steals 1 Health from the lowest Health enemy."
using System.Linq;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Audit.H;

namespace Audit
{
    internal static class Leech
    {
        static Minion PlayMinion(Game g, Controller c, string id)
        {
            var e = Hand(c, id);
            Check(id + " play accepted", Play(g, e), Describe(g));
            return e as Minion;
        }

        // ---- the reported defect: damaged victims kept their current Health
        [AuditTest]
        static void EDR_810t_StealsFromDamagedMinion()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            Board(p, "EDR_810t");
            var foe = Board(o, Yeti);
            Damage(g, foe, 2); // 4/3 (max 5)
            EndTurn(g);
            Eq("damaged victim current Health 3->2", 2, Hp(foe));
            Eq("damaged victim max Health 5->4", 4, MaxHp(foe));
            Eq("damage kept", 2, foe.Damage);
            Eq("hero gained 1", 31, Hp(p.Hero));
            var c = Clone(g);
            Eq("clone keeps 2 Health", 2, At(c, foe).Health);
        }

        [AuditTest]
        static void EDR_810t_KillsOneHealthDamagedMinion()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            Board(p, "EDR_810t");
            var foe = Board(o, Yeti);
            Damage(g, foe, 4); // 4/1 (max 5)
            EndTurn(g);
            Check("1-Health victim dies from steal", foe.IsDead || foe.Zone != o.BoardZone, "hp " + Hp(foe) + " max " + MaxHp(foe));
            Check("victim in graveyard", GraveIds(o).Contains(Yeti), string.Join(",", GraveIds(o)));
            Eq("hero gained 1", 31, Hp(p.Hero));
        }

        [AuditTest]
        static void EDR_810t_StealsFromDamagedHero()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            Board(p, "EDR_810t");
            Damage(g, o.Hero, 27); // boss at 3 of 30, no enemy minions
            EndTurn(g);
            Eq("damaged boss hero current Health 3->2", 2, Hp(o.Hero));
            Eq("boss max 29", 29, MaxHp(o.Hero));
            Eq("player hero 31", 31, Hp(p.Hero));
        }

        [AuditTest]
        static void EDR_810t_BossLeechStealsFromDamagedPlayerMinion()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            Board(o, "EDR_810t");
            var mine = Board(p, Yeti);
            Damage(g, mine, 3); // 4/2
            EndTurn(g); EndTurn(g); // boss's end of turn
            Eq("player yeti 2->1", 1, Hp(mine));
            Eq("boss hero 31", 31, Hp(o.Hero));
        }

        [AuditTest]
        static void EDR_810_HuskBonusOnDamagedMinion()
        {
            // Husk + its two leeches: each leech steals 2. A 4/3 damaged Yeti is lowest: first steal 3->1, second kills it.
            var g = NewGame(); var p = P1(g); var o = P2(g);
            PlayMinion(g, p, "EDR_810");
            var foe = Board(o, Yeti);
            Damage(g, foe, 2);
            EndTurn(g);
            Check("damaged yeti dies to two 2-steals", foe.IsDead || foe.Zone != o.BoardZone, "hp " + Hp(foe) + " max " + MaxHp(foe));
            Eq("hero +4", 34, Hp(p.Hero));
        }

        [AuditTest]
        static void EDR_814_LeechStealsAfterDamage()
        {
            // Infested Breath deals 2 then summons a leech; the leech then steals from the damaged target.
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var foe = Board(o, Yeti);
            Check("play ok", Play(g, Hand(p, "EDR_814"), foe), Describe(g));
            Eq("2 damage", 3, Hp(foe));
            EndTurn(g);
            Eq("leech steal 3->2", 2, Hp(foe));
            Eq("hero 31", 31, Hp(p.Hero));
        }

        // ---- behaviour that already worked and must stay
        [AuditTest]
        static void EDR_810t_StealsFromFullHealthMinion()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var leech = Board(p, "EDR_810t");
            Eq("0/2", "0/2", Atk(leech) + "/" + Hp(leech));
            var foe = Board(o, Yeti);
            EndTurn(g);
            Eq("victim current Health 5->4", 4, Hp(foe));
            Eq("victim max 4", 4, MaxHp(foe));
            Eq("hero 31", 31, Hp(p.Hero));
        }

        [AuditTest]
        static void EDR_810t_KillsFullHealthOneHealthMinion()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            Board(p, "EDR_810t");
            var foe = Board(o, Wisp);
            EndTurn(g);
            Check("1/1 dies", foe.IsDead || foe.Zone != o.BoardZone, "hp " + Hp(foe));
            Eq("hero 31", 31, Hp(p.Hero));
        }

        [AuditTest]
        static void EDR_810t_StealsFromHeroWhenLowest()
        {
            var g = NewGame(s => s.BossHealth = 3); var p = P1(g); var o = P2(g);
            Board(p, "EDR_810t");
            var foe = Board(o, Yeti);
            EndTurn(g);
            Eq("boss hero 3->2", 2, Hp(o.Hero));
            Eq("yeti untouched", 5, Hp(foe));
            Eq("player hero 31", 31, Hp(p.Hero));
        }

        [AuditTest]
        static void EDR_810t_OnlyOnOwnTurnEnd()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            EndTurn(g);
            Board(p, "EDR_810t");
            var foe = Board(o, Yeti);
            EndTurn(g);
            Eq("no steal on enemy turn end", 5, Hp(foe));
            Eq("hero 30", 30, Hp(p.Hero));
            EndTurn(g);
            Eq("steal on own turn end", 4, Hp(foe));
        }

        [AuditTest]
        static void EDR_810t_LowestPicksSmallest()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            Board(p, "EDR_810t");
            var big = Board(o, Yeti); var small = Board(o, "CS2_120");
            EndTurn(g);
            Eq("lowest (croc 3) hit", 2, Hp(small));
            Eq("yeti untouched", 5, Hp(big));
            Eq("clone keeps", 2, At(Clone(g), small).Health);
        }

        [AuditTest]
        static void EDR_810_HuskBattlecryAndBonus()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var husk = PlayMinion(g, p, "EDR_810");
            Eq("husk 3/5", "3/5", Atk(husk) + "/" + Hp(husk));
            Eq("two leeches summoned", 2, BoardIds(p).Count(x => x == "EDR_810t"));
            var foe = Board(o, Golem);
            EndTurn(g);
            Eq("each leech steals 2: 7 -> 3", 3, Hp(foe));
            Eq("hero +4", 34, Hp(p.Hero));
            Check("enchant EDR_810e on victim", EnchantIds(foe).Contains("EDR_810e"), string.Join(",", EnchantIds(foe)));
            Check("enchant EDR_810e2 on hero", EnchantIds(p.Hero).Contains("EDR_810e2"), string.Join(",", EnchantIds(p.Hero)));
        }

        [AuditTest]
        static void EDR_810_SilencedHuskNoBonus()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var husk = Board(p, "EDR_810");
            Board(p, "EDR_810t");
            var foe = Board(o, Golem);
            Silence(g, husk);
            EndTurn(g);
            Eq("silenced husk: steal 1", 6, Hp(foe));
        }

        [AuditTest]
        static void EDR_810e_SilenceVictimRestoresMax()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            Board(p, "EDR_810t");
            var foe = Board(o, Yeti);
            EndTurn(g);
            Eq("max 4", 4, MaxHp(foe));
            Silence(g, foe);
            Eq("silence removes Reduced Health: max 5", 5, MaxHp(foe));
            Eq("hero keeps max 31", 31, MaxHp(p.Hero));
        }

        [AuditTest]
        static void EDR_817_DrawTwoSummonTwo()
        {
            var g = NewGame(); var p = P1(g);
            int deck = p.DeckZone.Count;
            Check("play ok", Play(g, Hand(p, "EDR_817")), Describe(g));
            Eq("drew 2", 2, p.HandZone.Count);
            Eq("deck -2", deck - 2, p.DeckZone.Count);
            Eq("two leeches", 2, BoardIds(p).Count(x => x == "EDR_810t"));
        }
    }
}
