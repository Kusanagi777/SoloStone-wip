// Audit chunk neutral-bundles (chunk 15): one test per card id.
using System;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Audit.H;

namespace Audit
{
    internal static class Chunk15
    {
        static bool IsRace(IEntity e, Race r) => e.Card.IsRace(r);
        /// <summary>Pick a choice; answers suspended-effect decisions through TryAnswer (ChooseTask is refused for those).</summary>
        static string Pick(Game g, Controller c, int index)
        {
            if (!g.Resolution.HasSuspendedEffect) return Choose(g, c, index);
            int id = c.Choice.Choices[index]; string cid = g.IdEntityDic[id].Card.Id;
            var pd = g.GetPendingDecisions().First(d => d.ControllerId == c.Id);
            bool ok = g.TryAnswer(new SabberStoneCore.Model.Decisions.DecisionAnswer(pd.Id, c.Id, new[] { id }), out string err);
            Check("TryAnswer accepted", ok, err ?? "");
            return cid;
        }

        [AuditTest]
        static void AT_105_DamagesSelf()
        {
            var g = NewGame(); var p = P1(g);
            var m = (Minion)PlayNew(g, p, "AT_105");
            Eq("max 4", 4, MaxHp(m)); Eq("damaged to 1", 1, Hp(m)); Eq("atk 2", 2, Atk(m));
            Eq("still alive", 1, p.BoardZone.Count);
            var c = Clone(g); Eq("clone hp", 1, Hp(At(c, m)));
        }

        [AuditTest]
        static void GVG_064_VanillaMurloc()
        {
            var g = NewGame(); var p = P1(g);
            var m = (Minion)PlayNew(g, p, "GVG_064");
            Eq("atk", 3, Atk(m)); Eq("hp", 2, Hp(m)); Check("murloc", IsRace(m, Race.MURLOC));
            Eq("cost", 2, m.Card.Cost);
        }

        [AuditTest]
        static void CORE_UNG_205_FreezesEnemy()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var foe = Board(o, Yeti); var mine = Board(p, Yeti);
            var shard = Hand(p, "CORE_UNG_205");
            Mana(p, 10);
            var legal = LegalTargets(shard);
            Check("enemy minion legal", legal.Any(x => x.Id == foe.Id));
            Check("enemy hero legal", legal.Any(x => x.Id == o.Hero.Id));
            Check("friendly minion not legal", !legal.Any(x => x.Id == mine.Id), string.Join(",", legal.Select(x => x.Card.Id)));
            Check("friendly hero not legal", !legal.Any(x => x.Id == p.Hero.Id));
            Play(g, shard, foe);
            Check("enemy minion frozen", foe.IsFrozen, Describe(g));
            var g2 = NewGame(); var p2 = P1(g2); var o2 = P2(g2);
            PlayNew(g2, p2, "CORE_UNG_205", o2.Hero);
            Check("enemy hero frozen", o2.Hero.IsFrozen);
            var s3 = (Minion)P1(g).BoardZone.Last();
            Eq("shard 2/1", "2/1", $"{Atk(s3)}/{Hp(s3)}");
        }

        [AuditTest]
        static void TRL_509_AddsTwoBananas()
        {
            var g = NewGame(); var p = P1(g);
            PlayNew(g, p, "TRL_509");
            Eq("two bananas", 2, HandIds(p).Count(x => x == "TRL_509t"));
            Eq("hand count", 2, p.HandZone.Count);
        }

        [AuditTest]
        static void TRL_509t_GivesPlusOne()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var mine = Board(p, Yeti); var foe = Board(o, Yeti);
            PlayNew(g, p, "TRL_509t", mine);
            Eq("friendly 5/6", "5/6", $"{Atk(mine)}/{Hp(mine)}");
            Check("TRL_509te enchant", EnchantIds(mine).Contains("TRL_509te"), string.Join(",", EnchantIds(mine)));
            PlayNew(g, p, "TRL_509t", foe);
            Eq("enemy minion also targetable: 5/6", "5/6", $"{Atk(foe)}/{Hp(foe)}");
            var b = Hand(p, "TRL_509t");
            Check("hero not a legal target", !LegalTargets(b).Any(x => x is Hero));
            var c = Clone(g); Eq("clone keeps buff", 5, Atk(At(c, mine)));
        }

        [AuditTest]
        static void TRL_509te_Enchant()
        {
            var g = NewGame(); var p = P1(g);
            var m = Board(p, Wisp);
            PlayNew(g, p, "TRL_509t", m);
            Silence(g, m);
            Eq("silence removes +1/+1", "1/1", $"{Atk(m)}/{Hp(m)}");
        }

        [AuditTest]
        static void UNG_847_ElementalLastTurnDeals5()
        {
            // no elemental: no damage, no target needed
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var foe = Board(o, Golem);
            var bc = Hand(p, "UNG_847");
            Mana(p, 10);
            Check("playable without target", CanPlay(bc));
            Check("no targets offered without elemental", LegalTargets(bc).Count == 0, string.Join(",", LegalTargets(bc).Select(x => x.Card.Id)));
            Play(g, bc);
            Eq("golem untouched", 7, Hp(foe));

            // elemental played last turn
            var g2 = NewGame(); var p2 = P1(g2); var o2 = P2(g2);
            PlayNew(g2, p2, "UNG_809"); // Fire Fly (Elemental)
            Round(g2); ClearHand(p2); ClearHand(o2);
            var foe2 = Board(o2, Golem);
            var bc2 = Hand(p2, "UNG_847");
            Mana(p2, 10);
            var legal = LegalTargets(bc2);
            Check("enemy hero targetable", legal.Any(x => x.Id == o2.Hero.Id));
            Check("own hero targetable (any character)", legal.Any(x => x.Id == p2.Hero.Id));
            Play(g2, bc2, foe2);
            Eq("golem took 5", 2, Hp(foe2));
            var bc3 = Hand(p2, "UNG_847");
            Play(g2, bc3, o2.Hero);
            Eq("boss hero took 5", 25, o2.Hero.Health);

            // elemental played this turn (not last turn): no effect
            var g3 = NewGame(); var p3 = P1(g3); var o3 = P2(g3);
            PlayNew(g3, p3, "UNG_809");
            var bc4 = Hand(p3, "UNG_847");
            Mana(p3, 10);
            Check("this-turn elemental does not enable", LegalTargets(bc4).Count == 0);
        }

        [AuditTest]
        static void CFM_095_ShufflesIntoOpponentDeck()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var w = Board(p, "CFM_095");
            int before = o.DeckZone.Count, mine = p.DeckZone.Count;
            Kill(g, w);
            Eq("opp deck +1", before + 1, o.DeckZone.Count);
            Check("opp deck contains it", DeckIds(o).Contains("CFM_095"));
            Eq("own deck unchanged", mine, p.DeckZone.Count);
            // boss's tunneler goes to player's deck
            var w2 = Board(o, "CFM_095");
            Kill(g, w2);
            Check("player deck gets boss's tunneler", DeckIds(p).Contains("CFM_095"));
        }

        [AuditTest]
        static void TLC_242_ChooseOne()
        {
            string[] opts = { "UNG_999t6", "UNG_999t13", "UNG_999t14" };
            for (int i = 0; i < 3; i++)
            {
                var g = NewGame(); var p = P1(g);
                var s = (Minion)PlayNew(g, p, "TLC_242");
                var ch = ChoiceIds(g, p);
                Check($"[{i}] three options", ch.Count == 3 && opts.All(ch.Contains), string.Join(",", ch));
                int idx = ch.IndexOf(opts[i]);
                Pick(g, p, idx);
                if (i == 0) Check("taunt", s.HasTaunt && !s.Poisonous, Describe(g));
                if (i == 1) Check("poisonous", s.Poisonous && !s.HasTaunt, Describe(g));
                if (i == 2) Eq("2/6", "2/6", $"{Atk(s)}/{Hp(s)}");
                Check($"[{i}] hand empty after choice", p.HandZone.Count == 0, Describe(g));
                var c = Clone(g); var cs = At(c, s);
                Check($"[{i}] clone", cs.HasTaunt == s.HasTaunt && cs.Poisonous == s.Poisonous && cs.AttackDamage == s.AttackDamage);
            }
        }

        [AuditTest]
        static void UNG_002_AdaptTwice()
        {
            var g = NewGame(); var p = P1(g);
            var m = (Minion)PlayNew(g, p, "UNG_002");
            var c1 = ChoiceIds(g, p);
            Eq("first adapt 3 options", 3, c1.Count);
            Choose(g, p, 0);
            var c2 = ChoiceIds(g, p);
            Eq("second adapt 3 options", 3, c2.Count);
            Choose(g, p, 0);
            Check("no more choices", p.Choice == null);
            Check("two adaptations applied", EnchantIds(m).Count >= 2 || m.AttackDamage != 5 || m.Health != 6, string.Join(",", EnchantIds(m)) + " " + Describe(g));
            Note("adapts " + string.Join(",", c1) + " / " + string.Join(",", c2) + " -> " + string.Join(",", EnchantIds(m)));
        }

        [AuditTest]
        static void BOT_031_DeathrattleHitsEnemyHero()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var b = Board(p, "BOT_031");
            Eq("0/2", "0/2", $"{Atk(b)}/{Hp(b)}");
            Kill(g, b);
            Eq("boss hero -2", 28, o.Hero.Health);
            Eq("player hero untouched", 30, p.Hero.Health);
            var b2 = Board(o, "BOT_031");
            Kill(g, b2);
            Eq("boss's bomb hits player", 28, p.Hero.Health);
        }

        [AuditTest]
        static void TLC_831_EggStealsFromAllOtherMinions()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var egg = Board(p, "TLC_831");
            var myYeti = Board(p, Yeti); var foeYeti = Board(o, Yeti); var foeWisp = Board(o, Wisp);
            Eq("egg 0/3", "0/3", $"{Atk(egg)}/{Hp(egg)}");
            Kill(g, egg);
            var ptero = p.BoardZone.FirstOrDefault(m => m.Card.Id == "TLC_831t");
            Check("pterrordax summoned", ptero != null, Describe(g));
            if (ptero == null) return;
            Eq("friendly yeti -1 health", 4, Hp(myYeti));
            Eq("enemy yeti -1 health", 4, Hp(foeYeti));
            Eq("enemy yeti max -1", 4, myYeti.BaseHealth);
            Check("wisp died", foeWisp.IsDead && !o.BoardZone.Contains(foeWisp), Describe(g));
            Eq("pterrordax 3/6 (3 stolen)", "3/6", $"{Atk(ptero)}/{Hp(ptero)}");
            Check("TLC_831e on yeti", EnchantIds(foeYeti).Contains("TLC_831e"));
            Check("TLC_831e2 on ptero", EnchantIds(ptero).Contains("TLC_831e2"));
            Eq("heroes untouched", 30, o.Hero.Health);
            var c = Clone(g); Eq("clone ptero hp", 6, Hp(At(c, ptero)));
            // alone: 3/3
            var g2 = NewGame(); var p2 = P1(g2);
            var e2 = Board(p2, "TLC_831"); Kill(g2, e2);
            Eq("alone 3/3", "3/3", string.Join(",", p2.BoardZone.Select(m => $"{m.AttackDamage}/{m.Health}")));
        }

        [AuditTest]
        static void TLC_831e_LossSilence()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var y = Board(o, Yeti); var egg = Board(p, "TLC_831");
            Kill(g, egg);
            Eq("yeti 4", 4, Hp(y));
            Silence(g, y);
            Eq("silence restores 5", 5, Hp(y));
        }

        [AuditTest]
        static void TLC_831e2_GainSilence()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            Board(o, Yeti); var egg = Board(p, "TLC_831");
            Kill(g, egg);
            var pt = p.BoardZone.First(m => m.Card.Id == "TLC_831t");
            Eq("ptero 4", 4, Hp(pt));
            Silence(g, pt);
            Eq("silenced ptero 3", 3, Hp(pt));
        }

        [AuditTest]
        static void TLC_831t_Vanilla()
        {
            var g = NewGame(); var p = P1(g);
            var m = (Minion)PlayNew(g, p, "TLC_831t");
            Eq("3/3", "3/3", $"{Atk(m)}/{Hp(m)}");
            Eq("no steal when played directly", 1, p.BoardZone.Count);
        }

        [AuditTest]
        static void UNG_010_SummonsThreeMurlocs()
        {
            var g = NewGame(); var p = P1(g);
            var m = Board(p, "UNG_010");
            Eq("5/7", "5/7", $"{Atk(m)}/{Hp(m)}");
            Kill(g, m);
            var ms = p.BoardZone.ToList();
            Eq("three", 3, ms.Count);
            Check("all 1/1 murlocs", ms.All(x => x.AttackDamage == 1 && x.Health == 1 && IsRace(x, Race.MURLOC)), Describe(g));
        }

        [AuditTest]
        static void CFM_310t_Vanilla()
        {
            var g = NewGame(); var p = P1(g);
            var m = Board(p, "CFM_310t");
            Eq("1/1", "1/1", $"{Atk(m)}/{Hp(m)}"); Check("murloc", IsRace(m, Race.MURLOC));
        }

        [AuditTest]
        static void LOOT_144_GivesOpponentTwoCoins()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var d = Board(p, "LOOT_144");
            Kill(g, d);
            Eq("opp two coins", 2, HandIds(o).Count(x => x == "GAME_005"));
            Eq("me none", 0, p.HandZone.Count);
            Check("dragon", IsRace(d, Race.DRAGON));
        }

        [AuditTest]
        static void TRL_569_HoldingDragonDeals7()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var foe = Board(o, Golem); var mine = Board(p, Golem);
            Hand(p, "EX1_284"); // Azure Drake
            var cr = Hand(p, "TRL_569");
            Mana(p, 10);
            var legal = LegalTargets(cr);
            Check("enemy minion targetable", legal.Any(x => x.Id == foe.Id));
            Check("friendly minion not targetable", !legal.Any(x => x.Id == mine.Id));
            Check("enemy hero not targetable", !legal.Any(x => x.Id == o.Hero.Id));
            Play(g, cr, foe);
            Check("golem destroyed by 7", foe.IsDead || !o.BoardZone.Contains(foe), Describe(g));
            // no dragon
            var g2 = NewGame(); var p2 = P1(g2); var o2 = P2(g2);
            var f2 = Board(o2, Golem);
            var cr2 = Hand(p2, "TRL_569"); Mana(p2, 10);
            Check("no targets without dragon", LegalTargets(cr2).Count == 0);
            Play(g2, cr2);
            Eq("golem fine", 7, Hp(f2));
        }

        [AuditTest]
        static void UNG_813_Windfury()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var s = Board(p, "UNG_813");
            Eq("4/8", "4/8", $"{Atk(s)}/{Hp(s)}"); Check("windfury", s.HasWindfury);
            s.IsExhausted = false; s.NumAttacksThisTurn = 0;
            g.Process(SabberStoneCore.Tasks.PlayerTasks.MinionAttackTask.Any(p, s, o.Hero));
            g.Process(SabberStoneCore.Tasks.PlayerTasks.MinionAttackTask.Any(p, s, o.Hero));
            Eq("attacked twice", 22, o.Hero.Health);
            g.Process(SabberStoneCore.Tasks.PlayerTasks.MinionAttackTask.Any(p, s, o.Hero));
            Eq("no third attack", 22, o.Hero.Health);
        }

        [AuditTest]
        static void BOT_312_DeathrattleAndMagnetic()
        {
            var g = NewGame(); var p = P1(g);
            var r = Board(p, "BOT_312");
            Kill(g, r);
            var ms = p.BoardZone.ToList();
            Eq("three microbots", 3, ms.Count(x => x.Card.Id == "BOT_312t"));
            // Magnetic onto a Mech
            var g2 = NewGame(); var p2 = P1(g2);
            var mech = Board(p2, "GVG_006"); // Mechwarper 3/3 Mech
            int a = Atk(mech), h = Hp(mech);
            var rm = Hand(p2, "BOT_312");
            Play(g2, rm, null, 0, 0);
            Eq("magnetized: one minion", 1, p2.BoardZone.Count);
            Eq("mech +3/+1", $"{a + 3}/{h + 1}", $"{Atk(mech)}/{Hp(mech)}");
            Kill(g2, mech);
            Eq("mech's gained deathrattle summons 3", 3, p2.BoardZone.Count(x => x.Card.Id == "BOT_312t"));
        }

        [AuditTest]
        static void BOT_312t_Vanilla()
        {
            var g = NewGame(); var p = P1(g);
            var m = Board(p, "BOT_312t");
            Eq("1/1", "1/1", $"{Atk(m)}/{Hp(m)}"); Check("mech", IsRace(m, Race.MECHANICAL));
        }

        [AuditTest]
        static void TIME_035_TauntDeathrattleRewind()
        {
            for (int seed = 1; seed <= 5; seed++)
            {
                var g = NewGame(s => s.Seed = seed); var p = P1(g); var o = P2(g);
                var m = Board(p, "TIME_035");
                if (seed == 1) { Eq("6/6", "6/6", $"{Atk(m)}/{Hp(m)}"); Check("taunt", m.HasTaunt); }
                Kill(g, m);
                Eq($"[{seed}] one card", 1, p.HandZone.Count);
                if (p.HandZone.Count == 1)
                {
                    var c = p.HandZone[0];
                    Check($"[{seed}] has Rewind tag", c.Card[(GameTag)3934] > 0, c.Card.Id + " " + CardText(c.Card.Id));
                }
                Eq($"[{seed}] opp none", 0, o.HandZone.Count);
            }
        }

        [AuditTest]
        static void CORE_EX1_509_GainsAttackOnMurlocSummon()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var t = (Minion)PlayNew(g, p, "CORE_EX1_509");
            Eq("1/2 after own play", "1/2", $"{Atk(t)}/{Hp(t)}");
            PlayNew(g, p, "GVG_064");
            Eq("+1 from played murloc", 2, Atk(t));
            // die through a real game action (attack into a 7/7) so summon triggers resolve inside g.Process
            var th = Board(p, "UNG_010"); var wall = Board(o, Golem); Attack(g, th, wall);
            Check("threshadon died", th.IsDead || !p.BoardZone.Contains(th), Describe(g));
            Eq("+3 from three summoned murlocs", 5, Atk(t));
            PlayNew(g, p, Yeti);
            Eq("non-murloc: no", 5, Atk(t));
            var oth = Board(o, "UNG_010"); var myG = Board(p, Golem); Attack(g, myG, oth);
            Check("enemy murlocs summoned", o.BoardZone.Count(x => x.Card.Id == "CFM_310t") == 3, Describe(g));
            Eq("enemy murlocs: no", 5, Atk(t));
            Check("EX1_509e present", EnchantIds(t).Contains("EX1_509e"));
            Eq("health unchanged", 2, Hp(t));
            var c = Clone(g); Eq("clone", 5, Atk(At(c, t)));
            Board(p, "CFM_310t");
            Note("after harness Board() murloc: atk " + Atk(t) + " (Board uses SummonBlock directly)");
        }

        // DEFECT repro: murlocs summoned by a Deathrattle of a minion that died in combat do not buff Tidecaller
        // until a later action; Tidecaller then attacks with its old Attack. A spell kill of the same minion works.
        [AuditTest]
        static void CORE_EX1_509_CombatDeathrattleSummonTiming()
        {
            foreach (var tid in new[] { "CORE_EX1_509", "EX1_509" })
            {
                var g = NewGame(); var p = P1(g); var o = P2(g);
                var t = (Minion)PlayNew(g, p, tid);
                var th = Board(p, "UNG_010"); var wall = Board(o, Golem);
                Attack(g, th, wall);                      // Threshadon dies, summons three 1/1 Murlocs
                Eq(tid + " three murlocs summoned", 3, p.BoardZone.Count(x => x.Card.Id == "CFM_310t"));
                Eq(tid + " +3 Attack right after the summon", 4, Atk(t));
                t.IsExhausted = false;
                g.Process(SabberStoneCore.Tasks.PlayerTasks.MinionAttackTask.Any(p, t, o.Hero));
                Eq(tid + " next attack hits for 4", 4, 30 - o.Hero.Health);
                // enemy-turn combat death of the Threshadon
                var g2 = NewGame(); var p2 = P1(g2); var o2 = P2(g2);
                var t2 = Board(p2, tid); var th2 = Board(p2, "UNG_010");
                EndTurn(g2); ClearHand(o2);
                var bg = Board(o2, Golem); Attack(g2, bg, th2);
                Eq(tid + " enemy-turn combat death: +3", 4, Atk(t2));
                // control: spell kill works
                var g3 = NewGame(); var p3 = P1(g3);
                var t3 = Board(p3, tid); var th3 = Board(p3, "UNG_010");
                Damage(g3, th3, 1); PlayNew(g3, p3, "CS2_029", th3);
                Eq(tid + " spell death: +3 (control)", 4, Atk(t3));
            }
        }

        [AuditTest]
        static void EX1_509e_Enchant()
        {
            var g = NewGame(); var p = P1(g);
            var t = Board(p, "CORE_EX1_509");
            PlayNew(g, p, "CFM_310t");
            Eq("2 atk", 2, Atk(t));
            Silence(g, t);
            Eq("silence removes", 1, Atk(t));
            PlayNew(g, p, "CFM_310t");
            Eq("silenced no longer triggers", 1, Atk(t));
        }

        [AuditTest]
        static void OG_161_DamagesNonMurlocs()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var myY = Board(p, Yeti); var myM = Board(p, "GVG_064");
            var foeY = Board(o, Yeti); var foeM = Board(o, "CFM_310t");
            var seer = (Minion)PlayNew(g, p, "OG_161");
            Eq("my yeti 3", 3, Hp(myY)); Eq("foe yeti 3", 3, Hp(foeY));
            Eq("my murloc untouched", 2, Hp(myM)); Check("foe murloc alive", o.BoardZone.Contains(foeM));
            Eq("heroes untouched", 30, o.Hero.Health); Eq("my hero", 30, p.Hero.Health);
            Note("seer health after own battlecry: " + Hp(seer) + "/3");
        }

        [AuditTest]
        static void AT_097_Taunt()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var m = Board(o, "AT_097");
            Eq("2/1", "2/1", $"{Atk(m)}/{Hp(m)}"); Check("taunt", m.HasTaunt);
            var y = Board(p, Yeti);
            Check("attack on hero blocked", !Attack(g, y, o.Hero) || o.Hero.Health == 30, Describe(g));
            Eq("hero not hit", 30, o.Hero.Health);
        }

        [AuditTest]
        static void EX1_396_Taunt()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var m = Board(o, "EX1_396");
            Eq("1/7", "1/7", $"{Atk(m)}/{Hp(m)}"); Check("taunt", m.HasTaunt);
            var y = Board(p, Yeti);
            Attack(g, y, o.Hero);
            Eq("hero not hit", 30, o.Hero.Health);
        }

        [AuditTest]
        static void TRL_550_RushTaunt()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var foe = Board(o, Yeti);
            var b = (Minion)PlayNew(g, p, "TRL_550");
            Eq("5/7", "5/7", $"{Atk(b)}/{Hp(b)}"); Check("taunt", b.HasTaunt); Check("rush", b.IsRush);
            g.Process(SabberStoneCore.Tasks.PlayerTasks.MinionAttackTask.Any(p, b, o.Hero));
            Eq("rush cannot hit hero on play turn", 30, o.Hero.Health);
            g.Process(SabberStoneCore.Tasks.PlayerTasks.MinionAttackTask.Any(p, b, foe));
            Check("rush can hit minion", !o.BoardZone.Contains(foe) || Hp(foe) == 0, Describe(g));
        }
    }
}
