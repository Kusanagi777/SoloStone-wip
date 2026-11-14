// Chunk run-passive-treasure-and-buff-effects: one test per shell card, testing the run content text it carries
// (DungeonRunContent.cs), since the client shows the run name/text over the borrowed shell.
using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.CardSets.DungeonRun;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Audit.H;

namespace Audit
{
    internal static class Chunk2
    {
        static Game Boss(string passive, Action<Setup> more = null) => NewGame(s => { s.BossPower = passive; more?.Invoke(s); });
        static Game Treas(string key, Action<Setup> more = null) => NewGame(s => { s.Treasures = new List<string> { key }; more?.Invoke(s); });
        static Game Shrine(string key, Action<Setup> more = null) => NewGame(s => { s.PlayerPower = key; more?.Invoke(s); });

        static void ShellIs(string key, string shell) => Eq(key + " shell", shell, DungeonRunContent.Get(key).ShellId);

        // ---------------------------------------------------------------- boss passives
        [AuditTest]
        static void TB_BlingBrawl_Blade1e_VigilHeals4AtBossTurnStart()
        {
            ShellIs("fx_vigil", "TB_BlingBrawl_Blade1e");
            var g = Boss("passive_vigil", s => s.BossHealth = 40);
            var o = P2(g);
            Damage(g, o.Hero, 10, P1(g).Hero);
            Eq("boss at 30 after damage", 30, o.Hero.Health);
            EndTurn(g); // boss turn start
            Eq("boss restored 4", 34, o.Hero.Health);
            EndTurn(g); // player turn start: no heal
            Eq("no heal on player turn start", 34, o.Hero.Health);
            var c = Clone(g); EndTurn(c);
            Eq("clone heals too", 38, c.Player2.Hero.Health);
            // player-side heal does not apply to the player hero
            Damage(g, P1(g).Hero, 5);
            EndTurn(g);
            Eq("player hero not healed", 25, P1(g).Hero.Health);
        }

        [AuditTest]
        static void TB_DoubleMinions_ench_Devour2ToAllEnemyMinionsAtBossTurnStart()
        {
            ShellIs("fx_devour", "TB_DoubleMinions_ench");
            var g = Boss("passive_devour");
            var p = P1(g); var o = P2(g);
            var a = Board(p, Yeti); var b = Board(p, Wisp);
            var own = Board(o, Yeti);
            int heroHp = p.Hero.Health;
            EndTurn(g);
            Eq("player yeti took 2", 3, Hp(a));
            Check("player wisp died", b.Zone == null || b.Zone.Type != Zone.PLAY, Describe(g));
            Eq("boss minion untouched", 5, Hp(own));
            Eq("player hero untouched", heroHp, p.Hero.Health);
            EndTurn(g);
            Eq("no damage on player turn start", 3, Hp(a));
        }

        [AuditTest]
        static void TB_DeathrattleYog_ench_HoardDrawsAndArmor3AtBossTurnStart()
        {
            ShellIs("fx_hoard", "TB_DeathrattleYog_ench");
            var g = Boss("passive_hoard");
            var o = P2(g);
            int deck = o.DeckZone.Count;
            EndTurn(g);
            Eq("boss drew 2 (normal + hoard)", 2, o.HandZone.Count);
            Eq("deck -2", deck - 2, o.DeckZone.Count);
            Eq("boss armor 3", 3, Armor(o));
            EndTurn(g);
            Eq("no armor on player turn", 3, Armor(o));
            Eq("player armor 0", 0, Armor(P1(g)));
        }

        [AuditTest]
        static void TB_BlingBrawl_Hero1e_DisciplineRandomFriendlyPlus1Plus1()
        {
            ShellIs("fx_discipline", "TB_BlingBrawl_Hero1e");
            for (int seed = 1; seed <= 6; seed++)
            {
                var g = Boss("passive_discipline", s => s.Seed = seed);
                var p = P1(g); var o = P2(g);
                var mine = Board(p, Wisp);
                var x = Board(o, Wisp); var y = Board(o, Yeti);
                EndTurn(g);
                int buffed = (Atk(x) - 1) + (Atk(y) - 4);
                int buffedHp = (Hp(x) - 1) + (Hp(y) - 5);
                Eq($"seed {seed}: exactly +1 attack on one boss minion", 1, buffed);
                Eq($"seed {seed}: exactly +1 health on one boss minion", 1, buffedHp);
                Check($"seed {seed}: same minion got both", (Atk(x) == 2 && Hp(x) == 2) || (Atk(y) == 5 && Hp(y) == 6), Describe(g));
                Eq($"seed {seed}: player minion unbuffed", 1, Atk(mine));
            }
        }

        [AuditTest]
        static void TB_Amalgam_Ench_OvergrowthRandomFriendlyPlus1Health()
        {
            ShellIs("fx_overgrowth", "TB_Amalgam_Ench");
            var g = Boss("passive_overgrowth");
            var p = P1(g); var o = P2(g);
            var mine = Board(p, Wisp);
            var x = Board(o, Wisp);
            EndTurn(g);
            Eq("boss minion +1 health", 2, MaxHp(x));
            Eq("boss minion attack unchanged", 1, Atk(x));
            Eq("player minion unchanged", 1, MaxHp(mine));
            EndTurn(g);
            Eq("nothing on player turn", 2, MaxHp(x));
        }

        [AuditTest]
        static void TB_207MaskedBallE_CommandAllFriendlyPlus1Plus1()
        {
            ShellIs("fx_command", "TB_207MaskedBallE");
            var g = Boss("passive_command");
            var p = P1(g); var o = P2(g);
            var mine = Board(p, Wisp);
            var x = Board(o, Wisp); var y = Board(o, Yeti);
            EndTurn(g);
            Eq("wisp 2 atk", 2, Atk(x)); Eq("wisp 2 hp", 2, Hp(x));
            Eq("yeti 5 atk", 5, Atk(y)); Eq("yeti 6 hp", 6, Hp(y));
            Eq("player wisp unbuffed", 1, Atk(mine));
            EndTurn(g);
            Eq("nothing on player turn", 2, Atk(x));
        }

        [AuditTest]
        static void TB_006e_SovereignBuffsAllAndHits1Face()
        {
            ShellIs("fx_sovereign", "TB_006e");
            var g = Boss("passive_sovereign");
            var p = P1(g); var o = P2(g);
            var mine = Board(p, Wisp);
            var x = Board(o, Wisp); var y = Board(o, Yeti);
            int hp = p.Hero.Health;
            EndTurn(g);
            Eq("boss wisp 2/2", "2/2", Atk(x) + "/" + Hp(x));
            Eq("boss yeti 5/6", "5/6", Atk(y) + "/" + Hp(y));
            Eq("player hero -1", hp - 1, p.Hero.Health);
            Eq("player minion untouched", "1/1", Atk(mine) + "/" + Hp(mine));
            Eq("boss hero not damaged", 30, o.Hero.Health);
            EndTurn(g);
            Eq("nothing on player turn start", hp - 1, p.Hero.Health);
        }

        [AuditTest]
        static void TB_207TolBaradE_Bramble1ToAllEnemyMinionsAtBossTurnEnd()
        {
            ShellIs("fx_bramble", "TB_207TolBaradE");
            var g = Boss("passive_bramble");
            var p = P1(g); var o = P2(g);
            var a = Board(p, Yeti);
            EndTurn(g); // player end: nothing
            Eq("no damage at player's end of turn", 5, Hp(a));
            var own = Board(o, Yeti);
            ClearHand(o);
            EndTurn(g); // boss end
            Eq("player minion took 1", 4, Hp(a));
            Eq("boss minion untouched", 5, Hp(own));
            Eq("player hero untouched", 30, p.Hero.Health);
        }

        [AuditTest]
        static void TB_ArchivistEnch_PilferAddsCoinAtBossTurnEnd()
        {
            ShellIs("fx_pilfer", "TB_ArchivistEnch");
            var g = Boss("passive_pilfer");
            var o = P2(g);
            EndTurn(g);
            ClearHand(o);
            EndTurn(g);
            Eq("boss hand holds a Coin", "GAME_005", string.Join(",", HandIds(o)));
            Check("player hand has no coin", !HandIds(P1(g)).Contains("GAME_005"), Describe(g));
        }

        [AuditTest]
        static void TB_010e_UndyingSummonsRecruitAfterFriendlyMinionDies()
        {
            ShellIs("fx_undying", "TB_010e");
            var g = Boss("passive_undying");
            var p = P1(g); var o = P2(g);
            var x = Board(o, Wisp);
            var mine = Board(p, Wisp);
            Kill(g, x);
            Eq("boss board: one Recruit", "CS2_101t", string.Join(",", BoardIds(o)));
            Kill(g, mine);
            Eq("player minion death summons nothing for player", 0, p.BoardZone.Count);
            Eq("player minion death summons nothing for boss", 1, o.BoardZone.Count);
            // Recruit dying summons another
            Kill(g, o.BoardZone[0]);
            Eq("recruit death -> recruit", "CS2_101t", string.Join(",", BoardIds(o)));
            // full board: no throw
            for (int i = 0; i < 6; i++) Board(o, Wisp);
            Damage(g, o.BoardZone[0], 1, p.Hero);
            Check("full-board death handled", o.BoardZone.Count >= 6, Describe(g));
            var c = Clone(g); Kill(c, c.Player2.BoardZone[1]);
            Check("clone summons recruit", c.Player2.BoardZone.Any(m => m.Card.Id == "CS2_101t"), Describe(c));
        }

        [AuditTest]
        static void CS2_101t_SilverHandRecruitIs1x1()
        {
            var g = NewGame();
            var r = (Minion)PlayNew(g, P1(g), "CS2_101t");
            Eq("1 attack", 1, Atk(r)); Eq("1 health", 1, Hp(r));
            Eq("cost 1", 1, r.Card.Cost);
            Eq("on board", 1, P1(g).BoardZone.Count);
        }

        [AuditTest]
        static void TB_BlingBrawl_Blade2e_Tithe2ToEnemyHeroAtBossTurnEnd()
        {
            ShellIs("fx_tithe", "TB_BlingBrawl_Blade2e");
            var g = Boss("passive_tithe");
            var p = P1(g); var o = P2(g);
            var a = Board(p, Yeti);
            EndTurn(g);
            Eq("nothing at player end", 30, p.Hero.Health);
            EndTurn(g);
            Eq("player hero -2", 28, p.Hero.Health);
            Eq("minions untouched", 5, Hp(a));
            Eq("boss hero untouched", 30, o.Hero.Health);
        }

        // ---------------------------------------------------------------- buff enchantments
        static Minion WithEnch(Game g, string ench, string id = Wisp)
        {
            var m = Board(P1(g), id);
            SabberStoneCore.Actions.Generic.AddEnchantmentBlock(g, SabberStoneCore.Model.Cards.FromId(ench), P1(g).Hero, m, 0, 0, 0);
            return m;
        }

        [AuditTest]
        static void TB_CoOpv3_009e_Blessed1x1()
        {
            ShellIs("buff_1_1", "TB_CoOpv3_009e");
            var g = NewGame(); var m = WithEnch(g, "TB_CoOpv3_009e");
            Eq("2/2", "2/2", Atk(m) + "/" + Hp(m));
            Round(g); Eq("lasts", "2/2", Atk(m) + "/" + Hp(m));
            var c = Clone(g); Eq("clone", "2/2", Atk(At(c, m)) + "/" + Hp(At(c, m)));
        }

        [AuditTest]
        static void TB_CoOpv3_101e_Overclocked2x2_ViaOverclockPower()
        {
            ShellIs("buff_2_2", "TB_CoOpv3_101e");
            var g = NewGame(s => s.PlayerPower = "boss_overclock");
            var m = Board(P1(g), Wisp);
            Check("power ok", Power(g, P1(g), m), Describe(g));
            Eq("3/3", "3/3", Atk(m) + "/" + Hp(m));
            Round(g); Eq("lasts", "3/3", Atk(m) + "/" + Hp(m));
            var g2 = NewGame(s => s.PlayerPower = "warlock_sanguine_pact");
            var m2 = Board(P1(g2), Wisp);
            Power(g2, P1(g2), m2);
            Eq("sanguine pact 3/3", "3/3", Atk(m2) + "/" + Hp(m2));
            Eq("sanguine pact hero -2", 28, P1(g2).Hero.Health);
        }

        [AuditTest]
        static void TB_CoOpv3_104e_Honed1Attack_ViaWhetstone()
        {
            ShellIs("buff_atk_1", "TB_CoOpv3_104e");
            var g = NewGame(); var m = WithEnch(g, "TB_CoOpv3_104e");
            Eq("2/1", "2/1", Atk(m) + "/" + Hp(m));
            Round(g); Eq("lasts", "2/1", Atk(m) + "/" + Hp(m));
        }

        [AuditTest]
        static void TB_CoOpv3_BOSS4e_RalliedPlus1AttackThisTurn_ViaWarCry()
        {
            ShellIs("buff_atk_1_turn", "TB_CoOpv3_BOSS4e");
            var g = NewGame(s => s.PlayerPower = "sellsword_war_cry");
            var p = P1(g);
            var a = Board(p, Wisp); var b = Board(p, Yeti);
            var foe = Board(P2(g), Wisp);
            Check("power ok", Power(g, p, null), Describe(g));
            Eq("wisp 2 atk", 2, Atk(a)); Eq("yeti 5 atk", 5, Atk(b));
            Eq("health unchanged", 5, Hp(b));
            Eq("enemy minion not buffed", 1, Atk(foe));
            EndTurn(g);
            Eq("wisp back to 1 at end of turn", 1, Atk(a));
            Eq("yeti back to 4", 4, Atk(b));
            EndTurn(g);
            Eq("still 4 next turn", 4, Atk(b));
        }

        [AuditTest]
        static void TB_CoOpv3_BOSS2e_Fortified1Health_ViaBanner()
        {
            ShellIs("buff_hp_1", "TB_CoOpv3_BOSS2e");
            var g = NewGame(); var m = WithEnch(g, "TB_CoOpv3_BOSS2e");
            Eq("1/2", "1/2", Atk(m) + "/" + Hp(m));
            Round(g); Eq("lasts", "1/2", Atk(m) + "/" + Hp(m));
        }

        [AuditTest]
        static void TB_CoOpv3_BOSS3e_Braced2Health_ViaWardOfFaith()
        {
            ShellIs("buff_hp_2", "TB_CoOpv3_BOSS3e");
            var g = NewGame(s => s.PlayerPower = "priest_ward_of_faith");
            var m = Board(P1(g), Wisp);
            Check("power ok", Power(g, P1(g), m), Describe(g));
            Eq("1/3", "1/3", Atk(m) + "/" + Hp(m));
            Round(g); Eq("lasts", "1/3", Atk(m) + "/" + Hp(m));
            var g2 = NewGame(s => s.PlayerPower = "sellsword_shield_wall");
            var m2 = Board(P1(g2), Wisp);
            Power(g2, P1(g2), m2);
            Eq("shield wall 1/3", "1/3", Atk(m2) + "/" + Hp(m2));
            Check("shield wall taunt", m2.HasTaunt, Describe(g2));
        }

        [AuditTest]
        static void TB_CoOpv3_BOSSe_MappedCostsOneLess()
        {
            ShellIs("cost_minus_1", "TB_CoOpv3_BOSSe");
            var g = NewGame(); var p = P1(g);
            var y = Hand(p, Yeti);
            SabberStoneCore.Actions.Generic.AddEnchantmentBlock(g, SabberStoneCore.Model.Cards.FromId("TB_CoOpv3_BOSSe"), p.Hero, y, 0, 0, 0);
            Eq("yeti costs 3", 3, y.Cost);
            Round(g); Eq("still 3 next turn", 3, y.Cost);
            var w = Hand(p, "GAME_005");
            SabberStoneCore.Actions.Generic.AddEnchantmentBlock(g, SabberStoneCore.Model.Cards.FromId("TB_CoOpv3_BOSSe"), p.Hero, w, 0, 0, 0);
            Eq("0-cost stays 0", 0, w.Cost);
        }

        // ---------------------------------------------------------------- shrines
        [AuditTest]
        static void TB_Carousel_EnchB_Bulwark1Gain2ArmorEndOfTurn()
        {
            ShellIs("fx_bulwark_1", "TB_Carousel_EnchB");
            var g = Shrine("shrine_bulwark_1"); var p = P1(g);
            EndTurn(g); Eq("2 armor at own end", 2, Armor(p));
            EndTurn(g); Eq("none at boss end", 2, Armor(p));
            Eq("boss no armor", 0, Armor(P2(g)));
        }

        [AuditTest]
        static void TB_BoomAnnoy_001e_Bulwark2Gain4Armor()
        {
            ShellIs("fx_bulwark_2", "TB_BoomAnnoy_001e");
            var g = Shrine("shrine_bulwark_2"); var p = P1(g);
            EndTurn(g); Eq("4 armor", 4, Armor(p));
            EndTurn(g); Eq("none at boss end", 4, Armor(p));
        }

        [AuditTest]
        static void TB_BoomBotFestival_001e_Bulwark3Gain6Armor()
        {
            ShellIs("fx_bulwark_3", "TB_BoomBotFestival_001e");
            var g = Shrine("shrine_bulwark_3"); var p = P1(g);
            EndTurn(g); Eq("6 armor", 6, Armor(p));
            EndTurn(g); Eq("none at boss end", 6, Armor(p));
            var c = Clone(g); EndTurn(c); Eq("clone 12", 12, Armor(c.Player1));
        }

        static void DawnRandom(string key, int n)
        {
            for (int seed = 1; seed <= 6; seed++)
            {
                var g = Shrine(key, s => s.Seed = seed); var p = P1(g); var o = P2(g);
                var x = Board(p, Wisp); var y = Board(p, Yeti);
                var foe = Board(o, Wisp);
                Round(g);
                int da = (Atk(x) - 1) + (Atk(y) - 4), dh = (Hp(x) - 1) + (Hp(y) - 5);
                Eq($"seed {seed}: +{n} attack total", n, da);
                Eq($"seed {seed}: +{n} health total", n, dh);
                Check($"seed {seed}: one minion got all", (Atk(x) == 1 + n && Hp(x) == 1 + n) || (Atk(y) == 4 + n && Hp(y) == 5 + n), Describe(g));
                Eq($"seed {seed}: enemy not buffed", 1, Atk(foe));
            }
            // boss turn start does not fire it
            var g2 = Shrine(key); var m = Board(P1(g2), Wisp);
            EndTurn(g2);
            Eq("no buff at boss turn start", 1, Atk(m));
        }

        [AuditTest] static void TB_DALA_Minion1e_Dawn1RandomFriendlyPlus1() { ShellIs("fx_dawn_1", "TB_DALA_Minion1e"); DawnRandom("shrine_dawn_1", 1); }
        [AuditTest] static void TB_DALA_Minion3e_Dawn2RandomFriendlyPlus2() { ShellIs("fx_dawn_2", "TB_DALA_Minion3e"); DawnRandom("shrine_dawn_2", 2); }

        [AuditTest]
        static void TB_Carousel_EnchA_Dawn3AllFriendlyPlus1()
        {
            ShellIs("fx_dawn_3", "TB_Carousel_EnchA");
            var g = Shrine("shrine_dawn_3"); var p = P1(g);
            var x = Board(p, Wisp); var y = Board(p, Yeti); var foe = Board(P2(g), Wisp);
            EndTurn(g);
            Eq("no buff at boss start", 1, Atk(x));
            EndTurn(g);
            Eq("wisp 2/2", "2/2", Atk(x) + "/" + Hp(x));
            Eq("yeti 5/6", "5/6", Atk(y) + "/" + Hp(y));
            Eq("enemy unbuffed", 1, Atk(foe));
        }

        static void Embers(string key, int n)
        {
            for (int seed = 1; seed <= 6; seed++)
            {
                var g = Shrine(key, s => s.Seed = seed); var p = P1(g); var o = P2(g);
                var foe = Board(o, Golem);
                int before = o.Hero.Health + foe.Health;
                var mine = (Minion)PlayNew(g, p, Yeti);
                Eq($"seed {seed}: {n} damage to an enemy", n, before - (o.Hero.Health + foe.Health));
                Eq($"seed {seed}: played minion not damaged", 5, Hp(mine));
                Eq($"seed {seed}: player hero not damaged", 30, p.Hero.Health);
            }
            // summon (not play) does not trigger; boss play does not trigger
            var g2 = Shrine(key); var o2 = P2(g2);
            Board(P1(g2), Yeti);
            Eq("summon does not trigger", 30, o2.Hero.Health);
            EndTurn(g2);
            int ph = P1(g2).Hero.Health;
            PlayNew(g2, o2, Wisp);
            Eq("boss play does not hit player", ph, P1(g2).Hero.Health);
            Eq("boss play does not hit boss", 30, o2.Hero.Health);
            // only enemy hero available: hits face
            var g3 = Shrine(key); PlayNew(g3, P1(g3), Wisp);
            Eq("hits enemy hero when no minions", 30 - n, P2(g3).Hero.Health);
        }

        [AuditTest] static void TB_Blizzcon2016_GoonsEnchant_Embers1() { ShellIs("fx_embers_1", "TB_Blizzcon2016_GoonsEnchant"); Embers("shrine_embers_1", 1); }
        [AuditTest] static void TB_Blizzcon2016_KabalEnchant_Embers2() { ShellIs("fx_embers_2", "TB_Blizzcon2016_KabalEnchant"); Embers("shrine_embers_2", 2); }
        [AuditTest] static void TB_Blizzcon2016_LotusEnchant_Embers3() { ShellIs("fx_embers_3", "TB_Blizzcon2016_LotusEnchant"); Embers("shrine_embers_3", 3); }

        // ---------------------------------------------------------------- passive treasures
        [AuditTest]
        static void TB_007e_WhetstoneRandomFriendlyPlus1Attack()
        {
            ShellIs("fx_whetstone", "TB_007e");
            for (int seed = 1; seed <= 6; seed++)
            {
                var g = Treas("whetstone_charm", s => s.Seed = seed); var p = P1(g);
                var x = Board(p, Wisp); var y = Board(p, Yeti); var foe = Board(P2(g), Wisp);
                EndTurn(g);
                Eq($"seed {seed}: nothing at boss start", 5, Atk(x) + Atk(y));
                EndTurn(g);
                Eq($"seed {seed}: +1 attack total", 6, Atk(x) + Atk(y));
                Eq($"seed {seed}: health unchanged", 6, Hp(x) + Hp(y));
                Eq($"seed {seed}: enemy unbuffed", 1, Atk(foe));
            }
        }

        [AuditTest]
        static void TB_Champs_EX1_145e_IronSkin2ArmorAtEndOfTurn()
        {
            ShellIs("fx_iron_skin", "TB_Champs_EX1_145e");
            var g = Treas("iron_skin"); var p = P1(g);
            EndTurn(g); Eq("2 armor", 2, Armor(p));
            EndTurn(g); Eq("none at boss end", 2, Armor(p));
            Eq("boss none", 0, Armor(P2(g)));
        }

        [AuditTest]
        static void TB_Chupacabran_HP_E_EmberBrandAfterSpell1RandomEnemy()
        {
            ShellIs("fx_ember_brand", "TB_Chupacabran_HP_E");
            for (int seed = 1; seed <= 6; seed++)
            {
                var g = Treas("ember_brand", s => s.Seed = seed); var p = P1(g); var o = P2(g);
                var foe = Board(o, Golem);
                PlayNew(g, p, "CS2_023");
                Eq($"seed {seed}: 1 damage to an enemy", 1, (30 - o.Hero.Health) + (7 - foe.Health));
                Eq($"seed {seed}: player hero untouched", 30, p.Hero.Health);
            }
            var g2 = Treas("ember_brand"); var p2 = P1(g2); var o2 = P2(g2);
            var f2 = Board(o2, Golem);
            PlayNew(g2, p2, "CS2_023"); // Arcane Intellect: no damage of its own
            Eq("1 damage to an enemy", 1, (30 - o2.Hero.Health) + (7 - f2.Health));
            Eq("player hero untouched", 30, p2.Hero.Health);
            // minion play does not trigger
            PlayNew(g2, p2, Wisp);
            Eq("minion play no extra", 1, (30 - o2.Hero.Health) + (7 - f2.Health));
            // boss spell does not trigger
            EndTurn(g2);
            int ph = p2.Hero.Health;
            PlayNew(g2, o2, "CS2_023");
            Eq("boss spell no damage to player", ph, p2.Hero.Health);
            Eq("boss spell no damage to boss side", 1, (30 - o2.Hero.Health) + (7 - f2.Health));
        }

        [AuditTest]
        static void TB_DiscoverMyDeck_Enchantment_GraveCandleBuffsSurvivor()
        {
            ShellIs("fx_grave_candle", "TB_DiscoverMyDeck_Enchantment");
            for (int seed = 1; seed <= 6; seed++)
            {
                var g = Treas("grave_candle", s => s.Seed = seed); var p = P1(g); var o = P2(g);
                var dying = Board(p, Wisp); var y = Board(p, Yeti); var foe = Board(o, Wisp);
                Kill(g, dying);
                Eq($"seed {seed}: survivor 5/6", "5/6", Atk(y) + "/" + Hp(y));
                Eq($"seed {seed}: enemy unbuffed", 1, Atk(foe));
                Kill(g, foe);
                Eq($"seed {seed}: enemy death no buff", "5/6", Atk(y) + "/" + Hp(y));
            }
            var g2 = Treas("grave_candle"); var lone = Board(P1(g2), Wisp);
            Kill(g2, lone);
            Eq("lone death ok", 0, P1(g2).BoardZone.Count);
        }

        [AuditTest]
        static void TB_FoxBlessing2_KeenEyeDamagesFrozenEnemyMinion()
        {
            ShellIs("fx_keen_eye", "TB_FoxBlessing2");
            var g = Treas("keen_eye"); var p = P1(g); var o = P2(g);
            var a = Board(o, Yeti); var b = Board(o, Yeti);
            var mine = Board(p, Yeti);
            PlayNew(g, p, "CS2_026"); // Frost Nova
            Eq("a frozen and -1", 4, Hp(a)); Eq("b -1", 4, Hp(b));
            Check("frozen", a.IsFrozen && b.IsFrozen, Describe(g));
            Eq("own minion untouched", 5, Hp(mine));
            // Frostbolt the enemy hero: not a minion
            PlayNew(g, p, "CS2_024", o.Hero);
            Eq("hero took only frostbolt 3", 27, o.Hero.Health);
            // Freeze own minion via power (rimebite) no extra
            var g2 = Treas("keen_eye", s => s.PlayerPower = "mage_rimebite"); var m2 = Board(P1(g2), Yeti);
            Power(g2, P1(g2), m2);
            Eq("own frozen minion: only rimebite 1", 4, Hp(m2));
            var f2 = Board(P2(g2), Yeti);
            Power(g2, P1(g2), f2);
            Eq("enemy: rimebite 1 + keen eye 1", 3, Hp(f2));
        }

        [AuditTest]
        static void TB_DrawnDiscovery_Ench_MendingRestore3AtEndOfTurn()
        {
            ShellIs("fx_mending", "TB_DrawnDiscovery_Ench");
            var g = Treas("mending_charm"); var p = P1(g); var o = P2(g);
            Damage(g, p.Hero, 10);
            Damage(g, o.Hero, 10, p.Hero);
            EndTurn(g);
            Eq("player restored 3", 23, p.Hero.Health);
            EndTurn(g);
            Eq("none at boss end", 23, p.Hero.Health);
            Eq("boss not healed", 20, o.Hero.Health);
            Damage(g, p.Hero, 1); EndTurn(g);
            Eq("capped at max", 25, p.Hero.Health);
        }

        [AuditTest]
        static void TB_FoxBlessing1_OldMapReducesRandomHandCard()
        {
            ShellIs("fx_old_map", "TB_FoxBlessing1");
            for (int seed = 1; seed <= 6; seed++)
            {
                var g = Treas("old_map", s => { s.Seed = seed; s.PlayerDeck = Fill(10, "CS2_029"); });
                var p = P1(g);
                var y1 = Hand(p, Yeti); var y2 = Hand(p, Golem);
                EndTurn(g);
                Eq($"seed {seed}: no change at boss start", 4 + 7, y1.Cost + y2.Cost);
                var bossHand = P2(g).HandZone.Sum(c => c.Cost);
                EndTurn(g);
                int total = p.HandZone.Sum(c => c.Cost);
                // hand: yeti 4, golem 7, drawn fireball 4 (drawn after the trigger)
                Eq($"seed {seed}: total cost reduced by 1", 4 + 7 + 4 - 1, total);
                Check($"seed {seed}: reduced card was in hand before the draw", y1.Cost + y2.Cost == 10, Describe(g));
            }
            var g2 = Treas("old_map", s => s.PlayerDeck = new List<string>());
            Round(g2);
            Check("empty hand, no throw", true);
        }

        [AuditTest]
        static void TB_EndlessMinions01_QuietSatchelDrawsAt3OrFewer()
        {
            ShellIs("fx_quiet_satchel", "TB_EndlessMinions01");
            var g = Treas("quiet_satchel"); var p = P1(g);
            for (int i = 0; i < 3; i++) Hand(p, Wisp);
            Round(g);
            Eq("3 in hand -> satchel draw + turn draw", 5, p.HandZone.Count);
            var g2 = Treas("quiet_satchel"); var p2 = P1(g2);
            for (int i = 0; i < 4; i++) Hand(p2, Wisp);
            Round(g2);
            Eq("4 in hand -> only turn draw", 5, p2.HandZone.Count);
            var g3 = Treas("quiet_satchel"); var p3 = P1(g3);
            EndTurn(g3);
            Eq("boss turn start: player does not draw", 0, p3.HandZone.Count);
            var c = Clone(g3); EndTurn(c);
            Eq("clone: 0 in hand -> 2 cards", 2, c.Player1.HandZone.Count);
        }

        [AuditTest]
        static void TB_Firefest2_005e_SparkLantern1ToRandomEnemyMinion()
        {
            ShellIs("fx_spark_lantern", "TB_Firefest2_005e");
            for (int seed = 1; seed <= 6; seed++)
            {
                var g = Treas("spark_lantern", s => s.Seed = seed); var p = P1(g); var o = P2(g);
                var a = Board(o, Yeti); var b = Board(o, Yeti); var mine = Board(p, Yeti);
                EndTurn(g);
                Eq($"seed {seed}: nothing at boss start", 10, Hp(a) + Hp(b));
                ClearHand(o);
                EndTurn(g);
                Eq($"seed {seed}: 1 damage to an enemy minion", 9, Hp(a) + Hp(b));
                Eq($"seed {seed}: enemy hero untouched", 30, o.Hero.Health);
                Eq($"seed {seed}: own minion untouched", 5, Hp(mine));
            }
            var g2 = Treas("spark_lantern"); Round(g2);
            Eq("no enemy minions: no face damage", 30, P2(g2).Hero.Health);
        }

        [AuditTest]
        static void TB_FireFestEnch_StoneskinAllFriendlyPlus1Health()
        {
            ShellIs("fx_stoneskin", "TB_FireFestEnch");
            var g = Treas("stoneskin_idol"); var p = P1(g);
            var x = Board(p, Wisp); var y = Board(p, Yeti); var foe = Board(P2(g), Wisp);
            EndTurn(g);
            Eq("nothing at boss start", 1, MaxHp(x));
            EndTurn(g);
            Eq("wisp 1/2", "1/2", Atk(x) + "/" + Hp(x));
            Eq("yeti 4/6", "4/6", Atk(y) + "/" + Hp(y));
            Eq("enemy unbuffed", 1, MaxHp(foe));
        }

        [AuditTest]
        static void TB_Firefest2_003e_ThriftyGlovesHeroPowerCostsOneLess()
        {
            ShellIs("fx_thrifty", "TB_Firefest2_003e");
            var g = Treas("thrifty_gloves"); var p = P1(g);
            Eq("mage power 2 -> 1", 1, p.Hero.HeroPower.Cost);
            Eq("boss power unchanged", 2, P2(g).Hero.HeroPower.Cost);
            Mana(p, 1); p.Hero.HeroPower.IsExhausted = false;
            bool ok = g.Process(SabberStoneCore.Tasks.PlayerTasks.HeroPowerTask.Any(p, P2(g).Hero));
            Check("castable with 1 mana", ok && P2(g).Hero.Health == 29, Describe(g));
            Round(g);
            Eq("still 1 next turn", 1, p.Hero.HeroPower.Cost);
            var c = Clone(g); Eq("clone 1", 1, c.Player1.Hero.HeroPower.Cost);
            var g2 = Treas("thrifty_gloves", s => s.PlayerPower = "carto_shortcut");
            Eq("run power 3 -> 2", 2, P1(g2).Hero.HeroPower.Cost);
        }

        [AuditTest]
        static void TB_Face_Ench1_BannerSummonedMinionPlus1Health()
        {
            ShellIs("fx_banner", "TB_Face_Ench1");
            var g = Treas("champions_banner"); var p = P1(g); var o = P2(g);
            var played = (Minion)PlayNew(g, p, Wisp);
            Eq("played minion 1/2", "1/2", Atk(played) + "/" + Hp(played));
            var summoned = Board(p, Yeti);
            Eq("summoned yeti 4/6", "4/6", Atk(summoned) + "/" + Hp(summoned));
            Eq("existing minion not re-buffed", 2, Hp(played));
            var foe = Board(o, Wisp);
            Eq("enemy summon unbuffed", 1, MaxHp(foe));
            var g2 = Treas("champions_banner", s => s.PlayerPower = "druid_rootcall");
            Power(g2, P1(g2));
            var sap = P1(g2).BoardZone[0];
            Eq("power-summoned sapling 1/2", "1/2", Atk(sap) + "/" + Hp(sap));
        }

        // ---------------------------------------------------------------- the Coin
        [AuditTest]
        static void GAME_005_CoinGivesOneManaThisTurn()
        {
            var g = NewGame(); var p = P1(g);
            Mana(p, 3);
            var coin = Hand(p, "GAME_005");
            Play(g, coin, keepMana: true);
            Eq("4 mana available", 4, p.RemainingMana);
            Eq("base mana stays 3", 3, p.BaseMana);
            var y = Hand(p, Yeti);
            Check("can play 4-cost", Play(g, y, keepMana: true), Describe(g));
            Round(g);
            Eq("next turn: 4 crystals (normal +1), no lingering coin mana", 4, p.RemainingMana);
            var g2 = Treas("lucky_coin", s => s.ClearHands = false);
            Check("lucky coin: coin in opening hand", HandIds(P1(g2)).Contains("GAME_005"), Describe(g2));
            var g3 = NewGame(s => s.ClearHands = false);
            Check("no lucky coin: player has no coin (goes first)", !HandIds(P1(g3)).Contains("GAME_005"), Describe(g3));
        }
    }
}
