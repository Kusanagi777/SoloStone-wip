// Audit chunk 4: run boss powers, boss passives, shrines and heroes.
// The shells' run text is in DungeonRunContent.cs (the client card's own text is not what the run shows).
using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.CardSets.DungeonRun;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Audit.H;

namespace Audit
{
    internal static class Chunk4
    {
        // ------------------------------------------------------------ helpers
        static Game BossGame(string bossPower, int seed = 99, Action<Setup> more = null) =>
            NewGame(s => { s.BossPower = bossPower; s.Seed = seed; more?.Invoke(s); });
        static Game PlayerGame(string power, int seed = 99) => NewGame(s => { s.PlayerPower = power; s.Seed = seed; });

        static List<int> PowerTargetIds(Controller c)
        {
            Mana(c, 10); c.Hero.HeroPower.IsExhausted = false;
            return c.Options().OfType<HeroPowerTask>().Where(o => o.Target != null).Select(o => o.Target.Id).ToList();
        }
        static bool PowerOffered(Controller c)
        {
            Mana(c, 10); c.Hero.HeroPower.IsExhausted = false;
            return c.Options().OfType<HeroPowerTask>().Any();
        }
        static void ShellAndText(Controller c, string key, string shell)
        {
            var rc = DungeonRunContent.Get(key);
            Eq(key + " shell", shell, rc.ShellId);
            Eq("hero power in play is the shell", shell, c.Hero.HeroPower.Card.Id);
            Note(key + ": " + rc.Text.Replace("\n", " "));
        }
        static void Passive(Controller c, string key)
        {
            Check("passive is not clickable", !PowerOffered(c), "power " + c.Hero.HeroPower.Card.Id);
            string fx = DungeonRunContent.Get(key).EffectKey;
            Check("effect attached to owner", EnchantIds(c).Contains(DungeonRunContent.Get(fx).ShellId) || (c.AppliedEnchantments?.Any(e => e.Card.Id == DungeonRunContent.Get(fx).ShellId) ?? false),
                "applied " + string.Join(",", c.AppliedEnchantments?.Select(e => e.Card.Id) ?? new string[0]));
        }
        static int Sum(IEnumerable<ICharacter> cs) => cs.Sum(x => x.Health);

        // ============================================================ boss passives (owner = boss, P2)

        // Pilfer (TB_HeadlessHorseman_HP1): At the end of your turn, add a Coin to your hand.
        [AuditTest]
        static void TB_HeadlessHorseman_HP1_PilferCoinAtEndOfBossTurn()
        {
            var g = BossGame("passive_pilfer"); var p = P1(g); var o = P2(g);
            ShellAndText(o, "passive_pilfer", "TB_HeadlessHorseman_HP1"); Passive(o, "passive_pilfer");
            EndTurn(g); // end of player turn: nothing
            Eq("no coin after the player's turn", 0, HandIds(o).Count(x => x == "GAME_005"));
            Eq("player got no coin", 0, HandIds(p).Count(x => x == "GAME_005"));
            EndTurn(g); // end of boss turn
            Eq("boss gets 1 coin at its end of turn", 1, HandIds(o).Count(x => x == "GAME_005"));
            Eq("player still no coin", 0, HandIds(p).Count(x => x == "GAME_005"));
            var c = Clone(g); EndTurn(c); EndTurn(c);
            Eq("clone: second coin", 2, HandIds(At(c, o)).Count(x => x == "GAME_005"));
        }

        // Overgrowth (TB_HeadlessHorseman_HP3): At the start of your turn, give a random friendly minion +1 Health.
        [AuditTest]
        static void TB_HeadlessHorseman_HP3_OvergrowthHealthAtBossTurnStart()
        {
            for (int seed = 1; seed <= 6; seed++)
            {
                var g = BossGame("passive_overgrowth", seed); var p = P1(g); var o = P2(g);
                if (seed == 1) { ShellAndText(o, "passive_overgrowth", "TB_HeadlessHorseman_HP3"); Passive(o, "passive_overgrowth"); }
                var a = Board(o, Yeti); var b = Board(o, Wisp); var mine = Board(p, Yeti);
                EndTurn(g); // boss turn starts
                int gained = (MaxHp(a) - 5) + (MaxHp(b) - 1);
                Eq($"seed {seed}: exactly +1 Health on one boss minion", 1, gained);
                Eq($"seed {seed}: no Attack change", 5, Atk(a) + Atk(b));
                Eq($"seed {seed}: player minion untouched", 5, MaxHp(mine));
                EndTurn(g); // player's turn start: no trigger
                Eq($"seed {seed}: nothing on player's turn start", 1, (MaxHp(a) - 5) + (MaxHp(b) - 1));
            }
        }

        // Vigil (TB_HeadlessHorseman_HP4): At the start of your turn, restore 4 Health to your hero.
        [AuditTest]
        static void TB_HeadlessHorseman_HP4_VigilHeals4AtBossTurnStart()
        {
            var g = BossGame("passive_vigil"); var p = P1(g); var o = P2(g);
            ShellAndText(o, "passive_vigil", "TB_HeadlessHorseman_HP4"); Passive(o, "passive_vigil");
            Damage(g, o.Hero, 10, p.Hero); Damage(g, p.Hero, 10, o.Hero);
            Eq("boss at 20", 20, Hp(o.Hero));
            EndTurn(g);
            Eq("boss healed 4 at its turn start", 24, Hp(o.Hero));
            Eq("player not healed", 20, Hp(p.Hero));
            EndTurn(g);
            Eq("no heal on the player's turn start", 24, Hp(o.Hero));
            EndTurn(g);
            Eq("second heal", 28, Hp(o.Hero));
            EndTurn(g); EndTurn(g);
            Eq("capped at max health", 30, Hp(o.Hero));
        }

        // Tithe (TB_HeadlessHorseman_HP5): At the end of your turn, deal 2 damage to the enemy hero.
        [AuditTest]
        static void TB_HeadlessHorseman_HP5_TitheHits2AtEndOfBossTurn()
        {
            var g = BossGame("passive_tithe"); var p = P1(g); var o = P2(g);
            ShellAndText(o, "passive_tithe", "TB_HeadlessHorseman_HP5"); Passive(o, "passive_tithe");
            var mine = Board(p, Yeti);
            EndTurn(g);
            Eq("no damage at end of the player's turn", 30, Hp(p.Hero));
            EndTurn(g);
            Eq("player hero takes 2 at end of boss turn", 28, Hp(p.Hero));
            Eq("boss hero unharmed", 30, Hp(o.Hero));
            Eq("player minion unharmed", 5, Hp(mine));
            p.Hero.Armor = 1; Round(g);
            Eq("armor absorbs first", 0, Armor(p)); Eq("then health", 27, Hp(p.Hero));
        }

        // Stone Discipline (TB_HeadlessHorseman_HP6): At the start of your turn, give a random friendly minion +1/+1.
        [AuditTest]
        static void TB_HeadlessHorseman_HP6_DisciplineBuffAtBossTurnStart()
        {
            for (int seed = 1; seed <= 6; seed++)
            {
                var g = BossGame("passive_discipline", seed); var p = P1(g); var o = P2(g);
                if (seed == 1) { ShellAndText(o, "passive_discipline", "TB_HeadlessHorseman_HP6"); Passive(o, "passive_discipline"); }
                var a = Board(o, Yeti); var b = Board(o, Wisp); var mine = Board(p, Wisp);
                EndTurn(g);
                Eq($"seed {seed}: +1 Attack total on boss side", 6, Atk(a) + Atk(b));
                Eq($"seed {seed}: +1 Health total on boss side", 7, MaxHp(a) + MaxHp(b));
                Check($"seed {seed}: on one minion", (Atk(a) == 5 && MaxHp(a) == 6) || (Atk(b) == 2 && MaxHp(b) == 2), Describe(g));
                Eq($"seed {seed}: player minion untouched", 1, Atk(mine));
            }
            var g2 = BossGame("passive_discipline"); EndTurn(g2);
            Check("no minion: nothing throws", g2.State == State.RUNNING, Describe(g2));
        }

        // Hollow Command (TB_BRMA01_2H_2): At the start of your turn, give your minions +1/+1.
        [AuditTest]
        static void TB_BRMA01_2H_2_CommandBuffsAllAtBossTurnStart()
        {
            var g = BossGame("passive_command"); var p = P1(g); var o = P2(g);
            ShellAndText(o, "passive_command", "TB_BRMA01_2H_2"); Passive(o, "passive_command");
            var a = Board(o, Yeti); var b = Board(o, Wisp); var mine = Board(p, Wisp);
            EndTurn(g);
            Eq("yeti 5/6 attack", 5, Atk(a)); Eq("yeti 5/6 health", 6, MaxHp(a));
            Eq("wisp 2/2 attack", 2, Atk(b)); Eq("wisp 2/2 health", 2, MaxHp(b));
            Eq("player minion untouched", 1, Atk(mine));
            EndTurn(g);
            Eq("no buff on player's turn start", 5, Atk(a));
            var c = Clone(g); EndTurn(c);
            Eq("clone: second buff", 6, Atk(At(c, a)));
        }

        // Bramble Field (TB_BRMA10_3H): At the end of your turn, deal 1 damage to all enemy minions.
        [AuditTest]
        static void TB_BRMA10_3H_BrambleHitsEnemyMinionsAtEndOfBossTurn()
        {
            var g = BossGame("passive_bramble"); var p = P1(g); var o = P2(g);
            ShellAndText(o, "passive_bramble", "TB_BRMA10_3H"); Passive(o, "passive_bramble");
            var m1 = Board(p, Yeti); var m2 = Board(p, Wisp); var own = Board(o, Yeti);
            EndTurn(g);
            Eq("nothing at end of player's turn", 5, Hp(m1));
            EndTurn(g);
            Eq("player yeti takes 1", 4, Hp(m1));
            Check("player wisp died", m2.Zone?.Type != Zone.PLAY, Describe(g));
            Eq("player hero not hit", 30, Hp(p.Hero));
            Eq("boss minion not hit", 5, Hp(own));
        }

        // Devour (TB_MechWar_Boss2_HeroPower): At the start of your turn, deal 2 damage to all enemy minions.
        [AuditTest]
        static void TB_MechWar_Boss2_HeroPower_DevourHitsEnemyMinionsAtBossTurnStart()
        {
            var g = BossGame("passive_devour"); var p = P1(g); var o = P2(g);
            ShellAndText(o, "passive_devour", "TB_MechWar_Boss2_HeroPower"); Passive(o, "passive_devour");
            var m1 = Board(p, Yeti); var m2 = Board(p, "CS2_120"); /* River Crocolisk 2/3 */ var own = Board(o, Yeti);
            EndTurn(g);
            Eq("player yeti takes 2", 3, Hp(m1));
            Eq("player croc takes 2", 1, Hp(m2));
            Eq("player hero not hit", 30, Hp(p.Hero));
            Eq("boss minion not hit", 5, Hp(own));
            EndTurn(g);
            Eq("nothing at player's turn start", 3, Hp(m1));
        }

        // Hoard (TB_LOEA13_2): At the start of your turn, draw a card and gain 3 Armor.
        [AuditTest]
        static void TB_LOEA13_2_HoardDrawAndArmorAtBossTurnStart()
        {
            var g = BossGame("passive_hoard"); var p = P1(g); var o = P2(g);
            ShellAndText(o, "passive_hoard", "TB_LOEA13_2"); Passive(o, "passive_hoard");
            int deck = o.DeckZone.Count;
            EndTurn(g);
            Eq("boss hand = normal draw + 1", 2, o.HandZone.Count);
            Eq("boss deck -2", deck - 2, o.DeckZone.Count);
            Eq("boss gains 3 armor", 3, Armor(o));
            Eq("player no armor", 0, Armor(p));
            int ph = p.HandZone.Count;
            EndTurn(g);
            Eq("player draws normally only", ph + 1, p.HandZone.Count);
            Eq("no armor on player's turn", 3, Armor(o));
        }

        // Undying (TB_BountyHunt_Consume): After a friendly minion dies, summon a 1/1 Recruit.
        [AuditTest]
        static void TB_BountyHunt_Consume_UndyingSummonsRecruit()
        {
            var g = BossGame("passive_undying"); var p = P1(g); var o = P2(g);
            ShellAndText(o, "passive_undying", "TB_BountyHunt_Consume"); Passive(o, "passive_undying");
            var own = Board(o, Wisp); var mine = Board(p, Wisp);
            Kill(g, mine);
            Eq("enemy death: no recruit for boss", 0, BoardIds(o).Count(x => x == "CS2_101t"));
            Eq("enemy death: no recruit for player", 0, BoardIds(p).Count(x => x == "CS2_101t"));
            Kill(g, own); // on the player's turn
            Eq("friendly death on player's turn: one recruit", 1, BoardIds(o).Count(x => x == "CS2_101t"));
            var r = o.BoardZone.First(x => x.Card.Id == "CS2_101t");
            Eq("recruit 1 attack", 1, Atk(r)); Eq("recruit 1 health", 1, Hp(r));
            EndTurn(g);
            Kill(g, r);
            Eq("recruit death also summons a recruit", 1, BoardIds(o).Count(x => x == "CS2_101t"));
            Check("player hero death not counted (minion only)", true);
        }

        // Sovereign's Due (TB_BlingBrawl_Hero1p): At the start of your turn, give your minions +1/+1 and deal 1 damage to the enemy hero.
        [AuditTest]
        static void TB_BlingBrawl_Hero1p_SovereignBuffAndPing()
        {
            var g = BossGame("passive_sovereign"); var p = P1(g); var o = P2(g);
            ShellAndText(o, "passive_sovereign", "TB_BlingBrawl_Hero1p"); Passive(o, "passive_sovereign");
            var a = Board(o, Yeti); var b = Board(o, Wisp); var mine = Board(p, Wisp);
            EndTurn(g);
            Eq("yeti +1/+1 atk", 5, Atk(a)); Eq("yeti +1/+1 hp", 6, MaxHp(a));
            Eq("wisp +1/+1 atk", 2, Atk(b)); Eq("wisp +1/+1 hp", 2, MaxHp(b));
            Eq("player hero takes 1", 29, Hp(p.Hero));
            Eq("player minion untouched", 1, Hp(mine));
            Eq("boss hero untouched", 30, Hp(o.Hero));
            EndTurn(g);
            Eq("nothing at the player's turn start", 29, Hp(p.Hero));
            var g2 = BossGame("passive_sovereign"); EndTurn(g2);
            Eq("no minions: still pings", 29, Hp(P1(g2).Hero));
        }

        // ============================================================ clickable boss powers

        // Stomp (TB_BossBattle_KoboldHP): (2) Deal 1 damage to all enemy minions.
        [AuditTest]
        static void TB_BossBattle_KoboldHP_StompHitsAllEnemyMinions()
        {
            var g = BossGame("boss_stomp"); var p = P1(g); var o = P2(g);
            ShellAndText(o, "boss_stomp", "TB_BossBattle_KoboldHP");
            Eq("cost 2", 2, o.Hero.HeroPower.Cost);
            var m1 = Board(p, Yeti); var m2 = Board(p, Wisp); var own = Board(o, Yeti);
            PassTo(g, o);
            Mana(o, 2); o.Hero.HeroPower.IsExhausted = false;
            Check("power used", g.Process(HeroPowerTask.Any(o)), Describe(g));
            Eq("2 mana spent", 0, o.RemainingMana);
            Eq("player yeti 4", 4, Hp(m1));
            Check("player wisp dead", m2.Zone?.Type != Zone.PLAY);
            Eq("player hero not hit", 30, Hp(p.Hero));
            Eq("own minion not hit", 5, Hp(own));
            Eq("own hero not hit", 30, Hp(o.Hero));
            // player-side use (as it would work if given to a player)
            var g2 = NewGame(s => s.PlayerPower = "boss_stomp"); var e = Board(P2(g2), Yeti);
            Power(g2, P1(g2)); Eq("player-side: enemy minion hit", 4, Hp(e));
        }

        // Chain Spark (TB_BossRumble_001hp): (2) Deal 1 damage to two random enemies.
        [AuditTest]
        static void TB_BossRumble_001hp_ChainSparkTwoRandomEnemies()
        {
            bool heroHit = false, minionHit = false;
            for (int seed = 1; seed <= 12; seed++)
            {
                var g = BossGame("boss_chain_spark", seed); var p = P1(g); var o = P2(g);
                if (seed == 1) { ShellAndText(o, "boss_chain_spark", "TB_BossRumble_001hp"); Eq("cost 2", 2, o.Hero.HeroPower.Cost); }
                var m1 = Board(p, Yeti); var m2 = Board(p, Yeti); var own = Board(o, Yeti);
                PassTo(g, o);
                Check($"seed {seed}: used", Power(g, o));
                int lost = (30 - Hp(p.Hero)) + (5 - Hp(m1)) + (5 - Hp(m2));
                Eq($"seed {seed}: 2 damage total on enemies", 2, lost);
                Check($"seed {seed}: two different targets", new[] { 30 - Hp(p.Hero), 5 - Hp(m1), 5 - Hp(m2) }.Count(x => x == 1) == 2, Describe(g));
                Eq($"seed {seed}: own minion untouched", 5, Hp(own)); Eq($"seed {seed}: own hero untouched", 30, Hp(o.Hero));
                heroHit |= Hp(p.Hero) < 30; minionHit |= Hp(m1) < 5 || Hp(m2) < 5;
            }
            Check("enemy hero is a possible target", heroHit); Check("enemy minions are possible targets", minionHit);
            var g1 = BossGame("boss_chain_spark"); PassTo(g1, P2(g1)); Power(g1, P2(g1));
            Eq("only the enemy hero: it takes 1 (one target)", 29, Hp(P1(g1).Hero));
        }

        // Chill (TB_BossRumble_002hp): (2) Freeze an enemy minion.
        [AuditTest]
        static void TB_BossRumble_002hp_ChillFreezesEnemyMinion()
        {
            var g = BossGame("boss_chill"); var p = P1(g); var o = P2(g);
            ShellAndText(o, "boss_chill", "TB_BossRumble_002hp"); Eq("cost 2", 2, o.Hero.HeroPower.Cost);
            var foe = Board(p, Yeti); var own = Board(o, Yeti);
            PassTo(g, o);
            var t = PowerTargetIds(o);
            Check("enemy minion is a target", t.Contains(foe.Id));
            Check("own minion is not a target", !t.Contains(own.Id));
            Check("enemy hero is not a target", !t.Contains(p.Hero.Id));
            Check("own hero is not a target", !t.Contains(o.Hero.Id));
            Check("used", Power(g, o, foe));
            Check("enemy minion frozen", foe.IsFrozen);
            Eq("no damage", 5, Hp(foe));
            EndTurn(g);
            Check("frozen during the player's turn", foe.IsFrozen);
            Check("cannot attack while frozen", !p.Options().OfType<MinionAttackTask>().Any(x => x.Source == foe));
            var g2 = BossGame("boss_chill"); PassTo(g2, P2(g2));
            Check("no enemy minion: power not offered", !PowerOffered(P2(g2)));
        }

        // Overclock (TB_MechWar_Boss1_HeroPower): (2) Give a friendly minion +2/+2.
        [AuditTest]
        static void TB_MechWar_Boss1_HeroPower_OverclockFriendlyPlus2()
        {
            var g = BossGame("boss_overclock"); var p = P1(g); var o = P2(g);
            ShellAndText(o, "boss_overclock", "TB_MechWar_Boss1_HeroPower"); Eq("cost 2", 2, o.Hero.HeroPower.Cost);
            var foe = Board(p, Yeti); var own = Board(o, Wisp);
            PassTo(g, o);
            var t = PowerTargetIds(o);
            Check("own minion is a target", t.Contains(own.Id));
            Check("enemy minion is not a target", !t.Contains(foe.Id));
            Check("heroes are not targets", !t.Contains(o.Hero.Id) && !t.Contains(p.Hero.Id));
            Check("used", Power(g, o, own));
            Eq("wisp 3 attack", 3, Atk(own)); Eq("wisp 3 health", 3, MaxHp(own));
            Round(g);
            Eq("permanent", 3, Atk(own));
            var c = Clone(g); Eq("clone keeps buff", 3, Atk(At(c, own)));
        }

        // Cinder Rain (TB_BossRumble_003hp): (2) Deal 2 damage to a random enemy.
        [AuditTest]
        static void TB_BossRumble_003hp_CinderRainRandomEnemy()
        {
            bool heroHit = false, minionHit = false;
            for (int seed = 1; seed <= 12; seed++)
            {
                var g = BossGame("boss_cinder_rain", seed); var p = P1(g); var o = P2(g);
                if (seed == 1) { ShellAndText(o, "boss_cinder_rain", "TB_BossRumble_003hp"); Eq("cost 2", 2, o.Hero.HeroPower.Cost); }
                var m1 = Board(p, Yeti); var own = Board(o, Yeti);
                PassTo(g, o);
                Check($"seed {seed}: used", Power(g, o));
                int lost = (30 - Hp(p.Hero)) + (5 - Hp(m1));
                Eq($"seed {seed}: 2 damage on one enemy", 2, lost);
                Check($"seed {seed}: single target", Hp(p.Hero) == 28 || Hp(m1) == 3, Describe(g));
                Eq($"seed {seed}: own side untouched", 35, Hp(own) + Hp(o.Hero));
                heroHit |= Hp(p.Hero) < 30; minionHit |= Hp(m1) < 5;
            }
            Check("enemy hero is a possible target", heroHit); Check("enemy minion is a possible target", minionHit);
        }

        // ============================================================ shrines (player hero powers, passive)

        static void DawnRandom(string key, string shell, int n)
        {
            for (int seed = 1; seed <= 6; seed++)
            {
                var g = PlayerGame(key, seed); var p = P1(g); var o = P2(g);
                if (seed == 1) { ShellAndText(p, key, shell); Passive(p, key); }
                var a = Board(p, Yeti); var b = Board(p, Wisp); var foe = Board(o, Wisp);
                EndTurn(g);
                Eq($"seed {seed}: nothing on boss turn start", 5, Atk(a) + Atk(b));
                Eq($"seed {seed}: boss minion untouched", 1, Atk(foe));
                EndTurn(g);
                Eq($"seed {seed}: +{n} Attack total", 5 + n, Atk(a) + Atk(b));
                Eq($"seed {seed}: +{n} Health total", 6 + n, MaxHp(a) + MaxHp(b));
                Check($"seed {seed}: on one minion", (Atk(a) == 4 + n && MaxHp(a) == 5 + n) || (Atk(b) == 1 + n && MaxHp(b) == 1 + n), Describe(g));
            }
        }
        [AuditTest] static void TB_SPT_BossHeroPower_ShrineDawn1() => DawnRandom("shrine_dawn_1", "TB_SPT_BossHeroPower", 1);
        [AuditTest] static void TB_SPT_DALA_BossHeroPower_ShrineDawn2() => DawnRandom("shrine_dawn_2", "TB_SPT_DALA_BossHeroPower", 2);
        [AuditTest]
        static void TB_SPT_MTH_BossHeroPower_ShrineDawn3()
        {
            var g = PlayerGame("shrine_dawn_3"); var p = P1(g); var o = P2(g);
            ShellAndText(p, "shrine_dawn_3", "TB_SPT_MTH_BossHeroPower"); Passive(p, "shrine_dawn_3");
            var a = Board(p, Yeti); var b = Board(p, Wisp); var foe = Board(o, Wisp);
            EndTurn(g);
            Eq("nothing on boss turn start", 4, Atk(a));
            EndTurn(g);
            Eq("yeti 5 atk", 5, Atk(a)); Eq("yeti 6 hp", 6, MaxHp(a));
            Eq("wisp 2 atk", 2, Atk(b)); Eq("wisp 2 hp", 2, MaxHp(b));
            Eq("boss minion untouched", 1, Atk(foe));
        }

        static void Bulwark(string key, string shell, int armor)
        {
            var g = PlayerGame(key); var p = P1(g); var o = P2(g);
            ShellAndText(p, key, shell); Passive(p, key);
            EndTurn(g);
            Eq($"+{armor} Armor at end of player's turn", armor, Armor(p));
            Eq("boss no armor", 0, Armor(o));
            EndTurn(g);
            Eq("none at end of boss turn", armor, Armor(p));
            EndTurn(g);
            Eq("stacks", 2 * armor, Armor(p));
        }
        [AuditTest] static void TB_SPT_DPromoHP_ShrineBulwark1() => Bulwark("shrine_bulwark_1", "TB_SPT_DPromoHP", 2);
        [AuditTest] static void TB_SPT_DPromoHP2_ShrineBulwark2() => Bulwark("shrine_bulwark_2", "TB_SPT_DPromoHP2", 4);
        [AuditTest] static void TB_MammothParty_hp001_ShrineBulwark3() => Bulwark("shrine_bulwark_3", "TB_MammothParty_hp001", 6);

        static void Embers(string key, string shell, int n)
        {
            bool heroHit = false, minionHit = false;
            for (int seed = 1; seed <= 12; seed++)
            {
                var g = PlayerGame(key, seed); var p = P1(g); var o = P2(g);
                if (seed == 1) { ShellAndText(p, key, shell); Passive(p, key); }
                var foe = Board(o, Golem); var own = Board(p, Yeti);
                Board(p, Wisp); // summoned, not played: no trigger
                Eq($"seed {seed}: a summon (not a play) does not trigger", 37, Hp(foe) + Hp(o.Hero));
                var w = PlayNew(g, p, Wisp);
                int lost = (7 - Hp(foe)) + (30 - Hp(o.Hero));
                Eq($"seed {seed}: {n} damage to one enemy", n, lost);
                Check($"seed {seed}: one target", Hp(foe) == 7 - n || Hp(o.Hero) == 30 - n, Describe(g));
                Eq($"seed {seed}: friendly side untouched", 30 + 5 + 1, Hp(p.Hero) + Hp(own) + Hp((Minion)w));
                heroHit |= Hp(o.Hero) < 30; minionHit |= Hp(foe) < 7;
                PlayNew(g, p, "CS2_029", o.Hero); // Fireball: a spell, no trigger
                Eq($"seed {seed}: spell does not trigger", lost + 6, (7 - Hp(foe)) + (30 - Hp(o.Hero)));
            }
            Check("enemy hero is a possible target", heroHit); Check("enemy minion is a possible target", minionHit);
            var g2 = PlayerGame(key); EndTurn(g2); var o2 = P2(g2); var p2 = P1(g2);
            PlayNew(g2, o2, Wisp);
            Eq("boss playing a minion does not trigger the player's shrine", 30, Hp(p2.Hero));
        }
        [AuditTest] static void TB_MammothParty_hp002_ShrineEmbers1() => Embers("shrine_embers_1", "TB_MammothParty_hp002", 1);
        [AuditTest] static void TB_RoadToNR_DinoHP_ShrineEmbers2() => Embers("shrine_embers_2", "TB_RoadToNR_DinoHP", 2);
        [AuditTest] static void TB_RoadToNR_OrgrimmarHP_ShrineEmbers3() => Embers("shrine_embers_3", "TB_RoadToNR_OrgrimmarHP", 3);

        // ============================================================ heroes

        // Class heroes: the run hero with each of its class powers, 30 Health, power usable without throwing.
        static void ClassHero(string heroId)
        {
            var cls = Data.Classes.First(c => c.HeroCard == heroId);
            var g0 = NewGame(s => s.PlayerHero = heroId);
            var p0 = P1(g0);
            Eq("hero card", heroId, p0.Hero.Card.Id);
            Eq("30 Health", 30, Hp(p0.Hero));
            Eq("own power is the class basic power", DungeonRunSetup.PowerCardId(cls.DefaultPower), p0.Hero.HeroPower.Card.Id);
            Eq("hero class", cls.Id, p0.HeroClass.ToString());
            foreach (var pw in cls.Powers)
            {
                var g = NewGame(s => { s.PlayerHero = heroId; s.PlayerPower = pw; });
                var p = P1(g); var o = P2(g);
                Eq(pw + ": power card", DungeonRunSetup.PowerCardId(pw), p.Hero.HeroPower.Card.Id);
                Board(p, Yeti); Board(o, Yeti);
                var targets = PowerTargetIds(p);
                bool offered = PowerOffered(p);
                Check(pw + ": power offered", offered);
                ICharacter t = targets.Count > 0 ? (ICharacter)g.IdEntityDic[targets[0]] : null;
                bool ok = false;
                try { ok = Power(g, p, t); if (p.Choice != null) Choose(g, p, 0); Round(g); }
                catch (Exception ex) { Check(pw + ": no throw", false, ex.Message); }
                Check(pw + ": power used", ok, Describe(g));
                Check(pw + ": game still running", g.State == State.RUNNING, Describe(g));
            }
        }
        [AuditTest] static void HERO_01_ClassHero() => ClassHero("HERO_01");
        [AuditTest] static void HERO_02_ClassHero() => ClassHero("HERO_02");
        [AuditTest] static void HERO_03_ClassHero() => ClassHero("HERO_03");
        [AuditTest] static void HERO_04_ClassHero() => ClassHero("HERO_04");
        [AuditTest] static void HERO_05_ClassHero() => ClassHero("HERO_05");
        [AuditTest] static void HERO_06_ClassHero() => ClassHero("HERO_06");
        [AuditTest] static void HERO_07_ClassHero() => ClassHero("HERO_07");
        [AuditTest] static void HERO_08_ClassHero() => ClassHero("HERO_08");
        [AuditTest] static void HERO_09_ClassHero() => ClassHero("HERO_09");

        // Boss heroes: each boss using this hero, with its power, deck, sideboards and health; play 4 rounds where the
        // boss uses its power when it can.
        static void BossHero(string heroId)
        {
            var bosses = Data.Bosses.Where(b => b.HeroCard == heroId).ToList();
            Check("hero used by a boss", bosses.Count > 0);
            foreach (var b in bosses)
            {
                var (deck, sb, _) = BossDeck(b.Id);
                int hp = b.Health ?? (b.Final ? Data.Rules.FinalHealth : Data.Rules.BandHealth[Math.Max(0, Math.Min(Data.Rules.BandHealth.Length - 1, b.Band - 1))]);
                Game g = null;
                try { g = NewGame(s => { s.BossHero = heroId; s.BossPower = b.Power; s.BossDeck = deck; s.BossSideboards = sb; s.BossHealth = hp; }); }
                catch (Exception ex) { Check(b.Id + ": match builds", false, ex.Message); continue; }
                var o = P2(g); var p = P1(g);
                Eq(b.Id + ": hero card", heroId, o.Hero.Card.Id);
                Eq(b.Id + ": health", hp, Hp(o.Hero));
                Eq(b.Id + ": power", DungeonRunSetup.PowerCardId(b.Power), o.Hero.HeroPower.Card.Id);
                bool passive = DungeonRunContent.TryGet(b.Power, out var rc) && rc.Kind == RunContentKind.PassivePower;
                if (passive) Passive(o, b.Power);
                try
                {
                    for (int r = 0; r < 4 && g.State == State.RUNNING; r++)
                    {
                        Board(p, Wisp);
                        EndTurn(g);
                        if (!passive)
                        {
                            Board(o, Wisp);
                            var t = PowerTargetIds(o);
                            if (PowerOffered(o)) Power(g, o, t.Count > 0 ? (ICharacter)g.IdEntityDic[t[0]] : null);
                        }
                        EndTurn(g);
                    }
                }
                catch (Exception ex) { Check(b.Id + ": 4 rounds without throwing", false, ex.ToString()); }
                Check(b.Id + ": game consistent", g.State == State.RUNNING || g.State == State.COMPLETE, Describe(g));
                Note(b.Id + ": " + Describe(g));
            }
        }
        [AuditTest] static void HERO_01b_BossHero() => BossHero("HERO_01b");
        [AuditTest] static void HERO_02a_BossHero() => BossHero("HERO_02a");
        [AuditTest] static void HERO_02c_BossHero() => BossHero("HERO_02c");
        [AuditTest] static void HERO_02d_BossHero() => BossHero("HERO_02d");
        [AuditTest] static void HERO_03a_BossHero() => BossHero("HERO_03a");
        [AuditTest] static void HERO_04a_BossHero() => BossHero("HERO_04a");
        [AuditTest] static void HERO_04b_BossHero() => BossHero("HERO_04b");
        [AuditTest] static void HERO_05a_BossHero() => BossHero("HERO_05a");
        [AuditTest] static void HERO_05c_BossHero() => BossHero("HERO_05c");
        [AuditTest] static void HERO_06a_BossHero() => BossHero("HERO_06a");
        [AuditTest] static void HERO_06c_BossHero() => BossHero("HERO_06c");
        [AuditTest] static void HERO_06d_BossHero() => BossHero("HERO_06d");
        [AuditTest] static void HERO_07a_BossHero() => BossHero("HERO_07a");
        [AuditTest] static void HERO_07b_BossHero() => BossHero("HERO_07b");
        [AuditTest] static void HERO_07c_BossHero() => BossHero("HERO_07c");
        [AuditTest] static void HERO_08c_BossHero() => BossHero("HERO_08c");
        [AuditTest] static void HERO_09a_BossHero() => BossHero("HERO_09a");
        [AuditTest] static void HERO_09c_BossHero() => BossHero("HERO_09c");
    }
}
