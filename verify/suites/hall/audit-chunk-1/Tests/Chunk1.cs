// Audit chunk 1: leech family, tunnel_rat / ashen_abbess boss decks, band-2 misc.
using System;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Audit.H;

namespace Audit
{
    internal static class Chunk1
    {
        static Minion PlayMinion(Game g, Controller c, string id, IPlayable target = null, int chooseOne = 0)
        {
            var e = Hand(c, id);
            bool ok = Play(g, e, target, chooseOne);
            Check(id + " play accepted", ok, Describe(g));
            return e as Minion;
        }

        // ---------------------------------------------------------------- vanilla stat cards
        static void Vanilla(string id, int cost, int atk, int hp)
        {
            var g = NewGame();
            var p = P1(g);
            var m = PlayMinion(g, p, id);
            Eq(id + " cost", cost, m.Card.Cost);
            Eq(id + " attack", atk, Atk(m));
            Eq(id + " health", hp, Hp(m));
            Check(id + " no stray keywords", !m.HasTaunt && !m.HasDivineShield && !m.HasCharge && !m.IsRush && !m.HasWindfury && !m.HasStealth && !m.Poisonous && !m.HasLifeSteal && !m.IsFrozen && !m.IsDormant, Describe(g));
            Eq(id + " on board", 1, p.BoardZone.Count);
            Round(g);
            Check(id + " can attack next turn", m.CanAttack, Describe(g));
            Attack(g, m, P2(g).Hero);
            Eq(id + " hits face", 30 - atk, Hp(P2(g).Hero));
        }
        [AuditTest] static void LOOT_258_Vanilla() => Vanilla("LOOT_258", 1, 1, 3);
        [AuditTest] static void AT_092_Vanilla() => Vanilla("AT_092", 3, 5, 2);
        [AuditTest] static void BOT_309_Vanilla() => Vanilla("BOT_309", 2, 1, 5);
        [AuditTest] static void CFM_665_Vanilla() => Vanilla("CFM_665", 4, 6, 3);
        [AuditTest] static void CS2_118_Vanilla() => Vanilla("CS2_118", 3, 5, 1);
        [AuditTest] static void CS2_119_Vanilla() => Vanilla("CS2_119", 4, 2, 7);
        [AuditTest] static void CS2_120_Vanilla() => Vanilla("CS2_120", 2, 2, 3);
        [AuditTest] static void CS2_168_Vanilla() => Vanilla("CS2_168", 1, 2, 1);
        [AuditTest] static void CS2_182_Vanilla() => Vanilla("CS2_182", 4, 4, 5);
        [AuditTest] static void DAL_092_Vanilla() => Vanilla("DAL_092", 2, 2, 3);
        [AuditTest] static void GVG_044_Vanilla() => Vanilla("GVG_044", 3, 3, 4);
        [AuditTest] static void GVG_071_Vanilla() => Vanilla("GVG_071", 4, 5, 4);
        [AuditTest] static void OG_248_Vanilla() => Vanilla("OG_248", 3, 1, 5);

        // ---------------------------------------------------------------- keyword cards
        [AuditTest]
        static void EX1_405_Taunt()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var sb = PlayMinion(g, p, "EX1_405");
            Eq("0/4", "0/4", Atk(sb) + "/" + Hp(sb));
            Check("taunt", sb.HasTaunt);
            EndTurn(g);
            var foe = Board(o, Yeti);
            foe.IsExhausted = false; foe.NumAttacksThisTurn = 0;
            Check("enemy can't attack face past taunt", !g.Process(SabberStoneCore.Tasks.PlayerTasks.MinionAttackTask.Any(o, foe, p.Hero)) || Hp(p.Hero) == 30, Describe(g));
            Eq("player hero untouched", 30, Hp(p.Hero));
        }

        static void CantAttack(string id, int atk, int hp, bool taunt)
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var m = PlayMinion(g, p, id);
            Eq(id + " stats", atk + "/" + hp, Atk(m) + "/" + Hp(m));
            Eq(id + " taunt", taunt, m.HasTaunt);
            Round(g);
            Board(o, Wisp);
            Check(id + " cannot attack", !m.CanAttack, Describe(g));
            m.IsExhausted = false; m.NumAttacksThisTurn = 0;
            g.Process(SabberStoneCore.Tasks.PlayerTasks.MinionAttackTask.Any(p, m, o.Hero));
            Eq(id + " boss untouched", 30, Hp(o.Hero));
            Check(id + " survives clone", !At(Clone(g), m).CanAttack);
        }
        [AuditTest] static void EX1_045_CantAttack() => CantAttack("EX1_045", 4, 5, false);
        [AuditTest] static void UNG_844_CantAttack() => CantAttack("UNG_844", 4, 8, false);
        [AuditTest] static void JAIL_942_TauntCantAttack() => CantAttack("JAIL_942", 2, 6, true);

        [AuditTest]
        static void JAIL_942_SilenceLetsAttack()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var m = PlayMinion(g, p, "JAIL_942");
            Round(g);
            Silence(g, m);
            Check("silenced can attack", m.CanAttack && !m.HasTaunt, Describe(g));
        }

        [AuditTest]
        static void TRL_363t_Taunt()
        {
            var g = NewGame(); var p = P1(g);
            var m = Board(p, "TRL_363t");
            Eq("0/3", "0/3", Atk(m) + "/" + Hp(m));
            Check("taunt", m.HasTaunt);
        }

        // ---------------------------------------------------------------- battlecries / deathrattles
        [AuditTest]
        static void CATA_612_FreezesSelf()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var imp = PlayMinion(g, p, "CATA_612");
            Eq("5/3", "5/3", Atk(imp) + "/" + Hp(imp));
            Check("frozen after battlecry", imp.IsFrozen, Describe(g));
            EndTurn(g);
            Check("still frozen on boss turn", imp.IsFrozen, Describe(g));
            EndTurn(g);
            Check("frozen on player's next turn (can't attack)", imp.IsFrozen && !imp.CanAttack, Describe(g));
            EndTurn(g); EndTurn(g);
            Check("thawed the turn after", !imp.IsFrozen && imp.CanAttack, Describe(g));
            // summoned without battlecry: not frozen
            var g2 = NewGame();
            var b = Board(P1(g2), "CATA_612");
            Check("no freeze without battlecry", !b.IsFrozen);
        }

        [AuditTest]
        static void FP1_001_DeathrattleHealsEnemyHero()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var chow = PlayMinion(g, p, "FP1_001");
            Eq("2/3", "2/3", Atk(chow) + "/" + Hp(chow));
            Damage(g, o.Hero, 10);
            Damage(g, p.Hero, 10);
            Kill(g, chow);
            Eq("enemy hero restored 5", 25, Hp(o.Hero));
            Eq("own hero not healed", 20, Hp(p.Hero));
            // boss-owned chow heals the player
            var g2 = NewGame();
            var c2 = Board(P2(g2), "FP1_001");
            Damage(g2, P1(g2).Hero, 10);
            Kill(g2, c2);
            Eq("boss chow heals player hero", 25, Hp(P1(g2).Hero));
        }

        [AuditTest]
        static void LOOT_154_SummonsOneCostForOpponent()
        {
            for (int seed = 1; seed <= 6; seed++)
            {
                var g = NewGame(s => s.Seed = seed); var p = P1(g); var o = P2(g);
                var k = PlayMinion(g, p, "LOOT_154");
                Eq("2/3", "2/3", Atk(k) + "/" + Hp(k));
                Eq($"seed {seed}: own board only the knight", 1, p.BoardZone.Count);
                Eq($"seed {seed}: opponent got one minion", 1, o.BoardZone.Count);
                if (o.BoardZone.Count == 1)
                    Eq($"seed {seed}: it costs 1 ({o.BoardZone[0].Card.Id})", 1, o.BoardZone[0].Card.Cost);
            }
            // full opponent board: no throw
            var g3 = NewGame(); var o3 = P2(g3);
            for (int i = 0; i < 7; i++) Board(o3, Wisp);
            PlayMinion(g3, P1(g3), "LOOT_154");
            Eq("full enemy board stays 7", 7, o3.BoardZone.Count);
        }

        [AuditTest]
        static void TRL_363_DeathrattleFreeAgentForOpponent()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var t = PlayMinion(g, p, "TRL_363");
            Eq("2/3", "2/3", Atk(t) + "/" + Hp(t));
            Kill(g, t);
            Eq("own board empty", 0, p.BoardZone.Count);
            Eq("opponent got Free Agent", "TRL_363t", string.Join(",", BoardIds(o)));
            if (o.BoardZone.Count == 1) Check("free agent 0/3 taunt", o.BoardZone[0].HasTaunt && o.BoardZone[0].Health == 3 && o.BoardZone[0].AttackDamage == 0);
        }

        [AuditTest]
        static void JAIL_703_DeathrattleEnablesSorry()
        {
            var g = NewGame(); var p = P1(g);
            var gg = PlayMinion(g, p, "JAIL_703");
            Eq("1/3", "1/3", Atk(gg) + "/" + Hp(gg));
            Eq("sorry off before", 0, p[NativeTailRules.SorryEnabled]);
            Kill(g, gg);
            Eq("sorry enabled for controller", 1, p[NativeTailRules.SorryEnabled]);
            Eq("not for opponent", 0, P2(g)[NativeTailRules.SorryEnabled]);
        }

        // ---------------------------------------------------------------- auras / passives
        [AuditTest]
        static void UNG_085_YourMinionsCostTwoMore()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var yeti = Hand(p, Yeti); var bolt = Hand(p, "CS2_029"); var oy = Hand(o, Yeti);
            var q = PlayMinion(g, p, "UNG_085");
            Eq("2/3", "2/3", Atk(q) + "/" + Hp(q));
            Eq("own minion +2", 6, yeti.Cost);
            Eq("own spell unchanged", 4, bolt.Cost);
            Eq("enemy minion unchanged", 4, oy.Cost);
            Kill(g, q);
            Eq("cost back after death", 4, yeti.Cost);
        }

        [AuditTest]
        static void TIME_060_DoubleDamage()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var q = PlayMinion(g, p, "TIME_060");
            Eq("4/9", "4/9", Atk(q) + "/" + Hp(q));
            Damage(g, q, 3);
            Eq("3 damage becomes 6", 3, Hp(q));
            var bolt = Hand(o, "CS2_029"); // 4 dmg fireball? CS2_029 = Fireball (6)
            var g2 = NewGame(); var p2 = P1(g2);
            var q2 = PlayMinion(g2, p2, "TIME_060");
            var foe = Board(P2(g2), Wisp);
            Attack(g2, q2, foe);
            Eq("wisp attack 1 -> 2 damage", 7, Hp(q2));
            Eq("Destabilizer's own attack not doubled (wisp dead)", 0, P2(g2).BoardZone.Count);
            var g3 = NewGame(); var p3 = P1(g3);
            var q3 = Board(p3, "TIME_060");
            var fb = Hand(p3, "CS2_029");
            Play(g3, fb, q3);
            Eq("fireball 6 doubled kills 9-health", 0, p3.BoardZone.Count);
        }

        // ---------------------------------------------------------------- dormant cards
        [AuditTest]
        static void CORE_BT_156_DormantTwoTurnsThenRush()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var v = PlayMinion(g, p, "CORE_BT_156");
            Check("dormant when played", v.IsDormant, Describe(g));
            Damage(g, v, 0);
            EndTurn(g);
            var foe = Board(o, Wisp);
            Check("dormant on boss turn", v.IsDormant);
            EndTurn(g);
            Check("still dormant on player's next turn", v.IsDormant, Describe(g));
            Round(g);
            Check("awake on second following turn", !v.IsDormant, Describe(g));
            Eq("3/5", "3/5", Atk(v) + "/" + Hp(v));
            Check("rush", v.IsRush);
            bool ok = g.Process(SabberStoneCore.Tasks.PlayerTasks.MinionAttackTask.Any(p, v, foe));
            Check("can attack a minion on awaken turn (Rush)", ok && o.BoardZone.Count == 0, Describe(g));
        }

        [AuditTest]
        static void CORE_BT_156_RushAfterAwakenCannotHitHero()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var v = PlayMinion(g, p, "CORE_BT_156");
            Round(g); Round(g);
            Check("awake", !v.IsDormant, Describe(g));
            g.Process(SabberStoneCore.Tasks.PlayerTasks.MinionAttackTask.Any(p, v, o.Hero));
            Eq("rush can't hit face on wake turn", 30, Hp(o.Hero));
        }

        [AuditTest]
        static void TIME_046_DormantThreeTurnsTaunt()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var c = PlayMinion(g, p, "TIME_046");
            Check("dormant when played", c.IsDormant, Describe(g));
            Check("has TIME_046e", EnchantIds(c).Contains("TIME_046e"), string.Join(",", EnchantIds(c)));
            Round(g);
            Check("dormant +1", c.IsDormant);
            Round(g);
            Check("dormant +2", c.IsDormant);
            Round(g);
            Check("awake on third following turn", !c.IsDormant, Describe(g));
            Eq("3/12", "3/12", Atk(c) + "/" + Hp(c));
            Check("taunt once awake", c.HasTaunt);
            Check("enchantment gone", !EnchantIds(c).Contains("TIME_046e"), string.Join(",", EnchantIds(c)));
        }

        [AuditTest]
        static void TIME_046_DormantIsUntargetable()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var c = PlayMinion(g, p, "TIME_046");
            EndTurn(g);
            var bolt = Hand(o, "CS2_029");
            Check("enemy can't target dormant", !LegalTargets(bolt).Any(t => t.Id == c.Id));
            var yeti = Board(o, Yeti);
            Check("no taunt while dormant", !c.HasTaunt || c.IsDormant);
            yeti.IsExhausted = false; yeti.NumAttacksThisTurn = 0;
            g.Process(SabberStoneCore.Tasks.PlayerTasks.MinionAttackTask.Any(o, yeti, p.Hero));
            Eq("dormant taunt doesn't block face", 26, Hp(p.Hero));
        }

        [AuditTest]
        static void EDR_469_DormantUntilHeroPower()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var s = PlayMinion(g, p, "EDR_469");
            Check("dormant when played", s.IsDormant, Describe(g));
            Round(g); Round(g); Round(g);
            Check("still dormant after turns", s.IsDormant);
            // opponent hero power doesn't wake it
            EndTurn(g);
            Power(g, o, p.Hero);
            Check("enemy hero power doesn't wake", s.IsDormant);
            EndTurn(g);
            Power(g, p, o.Hero);
            Check("awake after own hero power", !s.IsDormant, Describe(g));
            Eq("3/3", "3/3", Atk(s) + "/" + Hp(s));
            Check("survives clone", !At(Clone(g), s).IsDormant);
        }

        // ---------------------------------------------------------------- magnetic / either side
        [AuditTest]
        static void BOT_906_Magnetic()
        {
            var g = NewGame(); var p = P1(g);
            var mech = Board(p, "BOT_309"); // Upgradeable Framebot is a Mech 1/5
            var gt = Hand(p, "BOT_906");
            Play(g, gt, null, 0, mech.ZonePosition);
            Eq("magnetized: board 1 minion", 1, p.BoardZone.Count);
            Eq("framebot 2/8", "2/8", Atk(mech) + "/" + Hp(mech));
            // alone: plain 1/3
            var g2 = NewGame();
            var lone = PlayMinion(g2, P1(g2), "BOT_906");
            Eq("lone glow-tron 1/3", "1/3", Atk(lone) + "/" + Hp(lone));
            // non-mech left: no magnetize
            var g3 = NewGame(); var p3 = P1(g3);
            var y = Board(p3, Yeti);
            var gt3 = Hand(p3, "BOT_906");
            Play(g3, gt3, null, 0, y.ZonePosition);
            Eq("no magnetize to non-mech", 2, p3.BoardZone.Count);
        }

        [AuditTest]
        static void JAIL_452_EitherSideOverload()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var d = Hand(p, "JAIL_452");
            bool ok = Play(g, d, null, 1, 0);
            Check("own side play ok", ok, Describe(g));
            Eq("on own board", 1, p.BoardZone.Count);
            Eq("own overload 2", 2, p.OverloadOwed);
            Eq("opponent not overloaded", 0, o.OverloadOwed);

            var g2 = NewGame(); var p2 = P1(g2); var o2 = P2(g2);
            var d2 = Hand(p2, "JAIL_452");
            bool ok2 = Play(g2, d2, null, 2, 0);
            Check("opponent side play ok", ok2, Describe(g2));
            Eq("on opponent board", 1, o2.BoardZone.Count);
            Eq("own board empty", 0, p2.BoardZone.Count);
            Eq("opponent overloaded 2", 2, o2.OverloadOwed);
            Eq("player not overloaded", 0, p2.OverloadOwed);
            // Phase 7 review: no more crystals are locked than the player has (Game.MainRessources drops the excess); with
            // 2 crystals on its turn the opponent locks both, with 1 it would lock 1.
            o2.BaseMana = System.Math.Max(o2.BaseMana, 1);
            EndTurn(g2);
            Eq("opponent locked 2 on its turn", 2, o2.OverloadLocked);
            Eq("opponent mana never below 0", 0, o2.RemainingMana);
            if (o2.BoardZone.Count == 1) Eq("4/3", "4/3", o2.BoardZone[0].AttackDamage + "/" + o2.BoardZone[0].Health);
        }

        // ---------------------------------------------------------------- leech family
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
        static void EDR_810t_StealsFromDamagedMinion()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            Board(p, "EDR_810t");
            var foe = Board(o, Yeti);
            Damage(g, foe, 2); // 4/3 (max 5)
            EndTurn(g);
            Note(Describe(g) + " victim max " + MaxHp(foe) + " dmg " + foe.Damage);
            Eq("damaged victim current Health 3->2", 2, Hp(foe));
            Eq("hero gained 1", 31, Hp(p.Hero));
        }

        [AuditTest]
        static void EDR_810t_KillsOneHealthDamagedMinion()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            Board(p, "EDR_810t");
            var foe = Board(o, Yeti);
            Damage(g, foe, 4); // 4/1 (max 5)
            EndTurn(g);
            Note(Describe(g));
            Check("1-Health victim dies from steal", foe.IsDead || foe.Zone != o.BoardZone, "hp " + Hp(foe) + " max " + MaxHp(foe));
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
            EndTurn(g); // boss turn
            Board(p, "EDR_810t");
            var foe = Board(o, Yeti);
            EndTurn(g); // boss's end of turn: no trigger
            Eq("no steal on enemy turn end", 5, Hp(foe));
            Eq("hero 30", 30, Hp(p.Hero));
            EndTurn(g);
            Eq("steal on own turn end", 4, Hp(foe));
        }

        [AuditTest]
        static void EDR_810t_BossOwnedLeechStealsFromPlayerMinion()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            Board(o, "EDR_810t");
            var mine = Board(p, Yeti);
            EndTurn(g); EndTurn(g);
            Eq("player yeti 5->4", 4, Hp(mine));
            Eq("boss hero 31", 31, Hp(o.Hero));
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
            var c = Clone(g);
            Eq("clone keeps", 2, At(c, small).Health);
        }

        [AuditTest]
        static void EDR_810_HuskBattlecryAndBonus()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var husk = PlayMinion(g, p, "EDR_810");
            Eq("husk 3/5", "3/5", Atk(husk) + "/" + Hp(husk));
            Eq("two leeches summoned", 2, BoardIds(p).Count(x => x == "EDR_810t"));
            var foe = Board(o, Golem); // 7/7, lowest enemy minion
            EndTurn(g);
            Eq("each leech steals 2: 7 -> 3", 3, Hp(foe));
            Eq("hero +4", 34, Hp(p.Hero));
            Check("enchant EDR_810e on victim", EnchantIds(foe).Contains("EDR_810e"), string.Join(",", EnchantIds(foe)));
            Check("enchant EDR_810e2 on hero", EnchantIds(p.Hero).Contains("EDR_810e2"), string.Join(",", EnchantIds(p.Hero)));
            // silenced husk: no bonus
            Round(g);
            int before = Hp(foe);
            Note("before silence round " + Describe(g));
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
        static void EDR_810e_e2_SilenceVictimRestoresHealth()
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
        static void EDR_814_DamageAndLeech()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var foe = Board(o, Yeti);
            var sp = Hand(p, "EDR_814");
            Eq("cost 2", 2, sp.Cost);
            Check("play ok", Play(g, sp, foe), Describe(g));
            Eq("2 damage", 3, Hp(foe));
            Eq("leech summoned", "EDR_810t", string.Join(",", BoardIds(p)));
            // face target
            var g2 = NewGame(); var sp2 = Hand(P1(g2), "EDR_814");
            Play(g2, sp2, P2(g2).Hero);
            Eq("hero 2 damage", 28, Hp(P2(g2).Hero));
            // spell damage +1
            var g3 = NewGame(); var p3 = P1(g3);
            Board(p3, "CS2_197"); // Ogre Magi spell damage +1
            var f3 = Board(P2(g3), Yeti);
            var sp3 = Hand(p3, "EDR_814");
            Play(g3, sp3, f3);
            Eq("spell damage: 3", 2, Hp(f3));
        }

        [AuditTest]
        static void EDR_817_DrawTwoSummonTwo()
        {
            var g = NewGame(); var p = P1(g);
            var sp = Hand(p, "EDR_817");
            Eq("cost 5", 5, sp.Cost);
            int deck = p.DeckZone.Count;
            Check("play ok", Play(g, sp), Describe(g));
            Eq("drew 2", 2, p.HandZone.Count);
            Eq("deck -2", deck - 2, p.DeckZone.Count);
            Eq("two leeches", 2, BoardIds(p).Count(x => x == "EDR_810t"));
        }

        [AuditTest]
        static void EDR_810t_StealsFromDamagedHero()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            Board(p, "EDR_810t");
            Damage(g, o.Hero, 27); // boss at 3 of 30, no enemy minions
            EndTurn(g);
            Eq("damaged boss hero current Health 3->2", 2, Hp(o.Hero));
            Eq("player hero 31", 31, Hp(p.Hero));
        }
    }
}
