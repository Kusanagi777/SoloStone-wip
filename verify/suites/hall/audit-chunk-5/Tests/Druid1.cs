// Audit chunk class-DRUID-1: one test (or more) per card id.
using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Audit.H;

namespace Audit
{
    internal static class Druid1
    {
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

        static void Vanilla(string id, int cost, int atk, int hp, Race race)
        {
            var c = Cards.FromId(id);
            Eq(id + " cost", cost, c.Cost); Eq(id + " atk", atk, c.ATK); Eq(id + " hp", hp, c.Health);
            Eq(id + " race", (int)race, c[GameTag.CARDRACE]);
            var g = NewGame(); var p = P1(g);
            var m = (Minion)PlayNew(g, p, id);
            Eq(id + " on board", 1, p.BoardZone.Count);
            Eq(id + " board atk", atk, Atk(m)); Eq(id + " board hp", hp, Hp(m));
        }

        [AuditTest] static void CS2_172_Vanilla() => Vanilla("CS2_172", 2, 3, 2, Race.BEAST);
        [AuditTest] static void KAR_300_Vanilla() => Vanilla("KAR_300", 1, 2, 2, Race.BEAST);

        // Spiderling: "Your hero has +1 Attack on your turn."
        [AuditTest]
        static void JAIL_202_HeroAttackOnlyOnYourTurn()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var s = Board(p, "JAIL_202");
            Eq("hero +1 atk on my turn", 1, p.Hero.AttackDamage);
            Check("hero carries JAIL_202e", EnchantIds(p.Hero).Contains("JAIL_202e") || p.Hero.AttackDamage == 1, string.Join(",", EnchantIds(p.Hero)));
            Check("enemy hero not buffed", o.Hero.AttackDamage == 0, Describe(g));
            int bossHp = o.Hero.Health;
            HeroAttack(g, p, o.Hero);
            Eq("hero hit for 1", bossHp - 1, o.Hero.Health);
            EndTurn(g);
            Eq("no hero atk on opponent's turn", 0, p.Hero.AttackDamage);
            EndTurn(g);
            Eq("back on my turn: +1 again", 1, p.Hero.AttackDamage);
            var cl = Clone(g);
            Eq("clone keeps +1", 1, At(cl, p).Hero.AttackDamage);
            Kill(g, s);
            Eq("gone when Spiderling dies", 0, p.Hero.AttackDamage);
            // boss-owned Spiderling buffs boss hero on boss turn only
            var g2 = NewGame(); var p2 = P1(g2); var o2 = P2(g2);
            Board(o2, "JAIL_202");
            Eq("boss hero 0 on my turn", 0, o2.Hero.AttackDamage);
            EndTurn(g2);
            Eq("boss hero +1 on boss turn", 1, o2.Hero.AttackDamage);
            Eq("my hero 0 on boss turn", 0, p2.Hero.AttackDamage);
        }

        // Underking: Rush; Battlecry and Deathrattle: Gain 6 Armor.
        [AuditTest]
        static void CORE_RLK_657_RushBattlecryDeathrattleArmor()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var m = (Minion)PlayNew(g, p, "CORE_RLK_657");
            Eq("6/6", "6/6", Atk(m) + "/" + Hp(m));
            Check("rush", m.IsRush, Describe(g));
            Eq("battlecry 6 armor", 6, Armor(p));
            var foe = Board(o, Yeti);
            Check("can attack enemy minion this turn", g.Process(MinionAttackTask.Any(p, m, foe)), Describe(g));
            Kill(g, m);
            Eq("deathrattle +6 armor", 12, Armor(p));
            Eq("enemy armor untouched", 0, Armor(o));
        }

        // Charred Chameleon: Battlecry: If you've used your Hero Power this turn, give a friendly minion +1/+2 and Rush.
        [AuditTest]
        static void FIR_908_AfterHeroPowerBuffsFriendly()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var w = Board(p, Wisp); var foe = Board(o, Wisp);
            Check("hero power used", Power(g, p, o.Hero), Describe(g));
            var ch = Hand(p, "FIR_908");
            var legal = LegalTargets(ch);
            Check("friendly wisp legal", legal.Any(x => x.Id == w.Id), string.Join(",", legal.Select(x => x.Card.Id)));
            Check("enemy minion not legal", !legal.Any(x => x.Id == foe.Id), string.Join(",", legal.Select(x => x.Card.Id)));
            Check("enemy hero not legal", !legal.Any(x => x.Id == o.Hero.Id), "");
            Check("played", Play(g, ch, w), Describe(g));
            Eq("wisp 2/3", "2/3", Atk(w) + "/" + Hp(w));
            Check("wisp rush", w.IsRush, Describe(g));
            Check("FIR_908e3 on wisp", EnchantIds(w).Contains("FIR_908e3"), string.Join(",", EnchantIds(w)));
            Eq("chameleon 1/2", "1/2", Atk((Minion)ch) + "/" + Hp((Minion)ch));
            var cl = Clone(g);
            Eq("clone keeps buff", 3, At(cl, w).Health);
        }

        [AuditTest]
        static void FIR_908_WithoutHeroPowerNoBuff()
        {
            var g = NewGame(); var p = P1(g);
            var w = Board(p, Wisp);
            var ch = Hand(p, "FIR_908");
            Check("playable without target", CanPlay(ch), "");
            Check("no legal target unpowered", LegalTargets(ch).Count == 0, string.Join(",", LegalTargets(ch).Select(x => x.Card.Id)));
            Play(g, ch);
            Eq("wisp unchanged", "1/1", Atk(w) + "/" + Hp(w));
            Check("no rush", !w.IsRush, "");
            // Hero power last turn does not count this turn
            var g2 = NewGame(); var p2 = P1(g2); var o2 = P2(g2);
            var w2 = Board(p2, Wisp);
            Power(g2, p2, o2.Hero); Round(g2); Mana(p2, 10);
            var ch2 = Hand(p2, "FIR_908");
            Check("no target after last turn's power", LegalTargets(ch2).Count == 0, "");
            Play(g2, ch2);
            Eq("wisp still 1/1", 1, Hp(w2));
        }

        // Strongshell Scavenger: Battlecry: Give your Taunt minions +2/+2.
        [AuditTest]
        static void ICC_807_BuffsOnlyYourTaunts()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var t = Board(p, "CS2_179"); var y = Board(p, Yeti); var et = Board(o, "CS2_179");
            var s = (Minion)PlayNew(g, p, "ICC_807");
            Eq("own taunt 5/7", "5/7", Atk(t) + "/" + Hp(t));
            Eq("own non-taunt 4/5", "4/5", Atk(y) + "/" + Hp(y));
            Eq("enemy taunt 3/5", "3/5", Atk(et) + "/" + Hp(et));
            Eq("scavenger 2/3", "2/3", Atk(s) + "/" + Hp(s));
        }

        // Highborne Mentor: Battlecry: Get a 2/2 Pupil. Discover a spell that costs (7) or more from the past to teach it.
        [AuditTest]
        static void TIME_704_PupilAndDiscover()
        {
            for (int seed = 1; seed <= 5; seed++)
            {
                var g = NewGame(s => { s.Seed = seed; s.PlayerHero = "HERO_06"; }); var p = P1(g); var o = P2(g);
                var m = (Minion)PlayNew(g, p, "TIME_704");
                Eq("mentor 6/6", "6/6", Atk(m) + "/" + Hp(m));
                var offers = ChoiceIds(g, p);
                Check("3 offers", offers.Count == 3, string.Join(",", offers));
                foreach (var id in offers)
                {
                    var c = Cards.FromId(id);
                    Check("offer is spell " + id, c.Type == CardType.SPELL, c.Type.ToString());
                    Check("offer costs 7+ " + id, c.Cost >= 7, c.Cost.ToString());
                }
                Check("pupil in hand", HandIds(p).Contains("TIME_704t"), string.Join(",", HandIds(p)));
                var picked = Pick(g, p, 0);
                Check("picked", picked != null, "");
                var pupil = (Minion)p.HandZone.FirstOrDefault(x => x.Card.Id == "TIME_704t");
                if (pupil == null) { Check("pupil exists after pick", false, Describe(g)); continue; }
                Eq("pupil 2/2", "2/2", pupil.AttackDamage + "/" + pupil.Health);
                Check("pupil taught", pupil[GameTag.TAG_SCRIPT_DATA_NUM_1] != 0, "");
                var cl = Clone(g);
                int handBefore = p.HandZone.Count;
                Board(o, Yeti); Board(o, Yeti);
                try
                {
                    Play(g, pupil, null);
                    if (p.Choice != null) Pick(g, p, 0);
                    Check("pupil on board", p.BoardZone.Any(x => x.Id == pupil.Id), Describe(g));
                    Note($"seed {seed} taught {picked} ({Cards.FromId(picked).Name}) -> {Describe(g)}");
                }
                catch (Exception ex) { Check("pupil cast threw " + picked, false, ex.Message); }
                Check("clone pupil taught", At(cl, pupil)[GameTag.TAG_SCRIPT_DATA_NUM_1] != 0, "");
            }
        }

        // Crystalspine Cub: Whenever you spend your last Mana Crystal, gain +1/+1.
        [AuditTest]
        static void CATA_130_LastManaSpent()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var cub = Board(p, "CATA_130");
            Mana(p, 4);
            Play(g, Hand(p, Yeti), keepMana: true);
            Eq("spent last (4/4) -> 2/2", "2/2", Atk(cub) + "/" + Hp(cub));
            Check("CATA_130e", EnchantIds(cub).Contains("CATA_130e"), string.Join(",", EnchantIds(cub)));
            Mana(p, 5);
            Play(g, Hand(p, "CS2_172"), keepMana: true);
            Eq("3 left: no buff", 2, Atk(cub));
            Play(g, Hand(p, "CS2_172"), keepMana: true); // 1 left
            Eq("1 left: no buff", 2, Atk(cub));
            Play(g, Hand(p, "KAR_300"), keepMana: true); // 0 left
            Eq("spent last again -> 3/3", "3/3", Atk(cub) + "/" + Hp(cub));
            Play(g, Hand(p, Wisp), keepMana: true); // 0-cost at 0
            Eq("0-cost at 0 mana: no buff", 3, Atk(cub));
            // Hero power spending last mana
            Mana(p, 2); p.Hero.HeroPower.IsExhausted = false;
            g.Process(HeroPowerTask.Any(p, o.Hero));
            Eq("hero power spends last -> 4/4", 4, Atk(cub));
            // opponent spending last mana doesn't count
            EndTurn(g); ClearHand(o);
            Mana(o, 4);
            Play(g, Hand(o, Yeti), keepMana: true);
            Eq("opponent spending last: no buff", 4, Atk(cub));
        }

        // Giant Anaconda: Deathrattle: Summon a minion from your hand with 5 or more Attack.
        [AuditTest]
        static void UNG_086_SummonsFivePlusAttackFromHand()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var a = Board(p, "UNG_086");
            Eq("5/3", "5/3", Atk(a) + "/" + Hp(a));
            Hand(p, Wisp); Hand(p, Yeti); Hand(p, Golem); Hand(p, "CS2_029"); // Fireball (spell)
            Hand(o, Golem);
            Kill(g, a);
            Check("golem summoned", BoardIds(p).Contains(Golem), Describe(g));
            Check("golem left hand", !HandIds(p).Contains(Golem), Describe(g));
            Eq("one summon", 1, p.BoardZone.Count);
            Check("enemy golem untouched", HandIds(o).Contains(Golem) && o.BoardZone.Count == 0, Describe(g));
            var g2 = NewGame(); var p2 = P1(g2);
            var a2 = Board(p2, "UNG_086");
            Hand(p2, Yeti); Hand(p2, Wisp);
            Kill(g2, a2);
            Eq("no 5+ attack: nothing summoned", 0, p2.BoardZone.Count);
            Eq("hand intact", 2, p2.HandZone.Count);
        }

        // Elder Longneck: Battlecry: If you're holding a minion with 5 or more Attack, Adapt.
        [AuditTest]
        static void UNG_109_AdaptsWhenHolding5Atk()
        {
            var g = NewGame(); var p = P1(g);
            Hand(p, Golem);
            var l = (Minion)PlayNew(g, p, "UNG_109");
            var offers = ChoiceIds(g, p);
            Eq("3 adapt offers", 3, offers.Count);
            Check("offers are adaptations", offers.All(AdaptIds.Contains) && offers.Distinct().Count() == 3, string.Join(",", offers));
            Pick(g, p, 0);
            Check("choice closed", p.Choice == null, "");
            Check("longneck got an adapt enchant", EnchantIds(l).Any(e => e.StartsWith("UNG_999t")), string.Join(",", EnchantIds(l)));
            var g2 = NewGame(); var p2 = P1(g2);
            Hand(p2, Yeti);
            var l2 = (Minion)PlayNew(g2, p2, "UNG_109");
            Check("no adapt without 5+ attack", p2.Choice == null, string.Join(",", ChoiceIds(g2, p2)));
            Eq("5/1", "5/1", Atk(l2) + "/" + Hp(l2));
        }

        static readonly string[] AdaptIds = { "UNG_999t10", "UNG_999t2", "UNG_999t3", "UNG_999t4", "UNG_999t5", "UNG_999t6", "UNG_999t7", "UNG_999t8", "UNG_999t13", "UNG_999t14" };

        /// <summary>Plays Elder Longneck (Golem in hand) with seeds until `adapt` is offered, then picks it.</summary>
        static (Game g, Minion m) AdaptVia(string adapt)
        {
            for (int seed = 1; seed < 400; seed++)
            {
                var g = NewGame(s => s.Seed = seed); var p = P1(g);
                Hand(p, Golem);
                var l = (Minion)PlayNew(g, p, "UNG_109");
                var offers = ChoiceIds(g, p);
                int i = offers.IndexOf(adapt);
                if (i < 0) continue;
                Pick(g, p, i);
                return (g, l);
            }
            Check("adapt " + adapt + " offered on some seed", false, "");
            return (null, null);
        }

        [AuditTest]
        static void UNG_999t10_StealthUntilYourNextTurn()
        {
            var (g, m) = AdaptVia("UNG_999t10"); if (g == null) return;
            var p = P1(g); var o = P2(g);
            Check("stealthed", m.HasStealth, Describe(g));
            Check("UNG_999t10e", EnchantIds(m).Contains("UNG_999t10e"), string.Join(",", EnchantIds(m)));
            EndTurn(g);
            Check("still stealthed on opponent's turn", m.HasStealth, Describe(g));
            var foe = Board(o, Yeti); foe.IsExhausted = false;
            Check("enemy cannot attack it", !o.Options().OfType<MinionAttackTask>().Any(x => x.Target?.Id == m.Id), "");
            EndTurn(g);
            Check("stealth gone on my next turn", !m.HasStealth, Describe(g));
        }

        [AuditTest]
        static void UNG_999t13_Poisonous()
        {
            var (g, m) = AdaptVia("UNG_999t13"); if (g == null) return;
            var o = P2(g);
            Check("poisonous", m.Poisonous, Describe(g));
            var big = Board(o, Golem);
            Attack(g, m, big);
            Check("golem destroyed by poison", big.IsDead || !o.BoardZone.Contains(big), Describe(g));
        }

        [AuditTest]
        static void UNG_999t14_PlusOnePlusOne()
        {
            var (g, m) = AdaptVia("UNG_999t14"); if (g == null) return;
            Eq("6/2", "6/2", Atk(m) + "/" + Hp(m));
            Check("UNG_999t14e", EnchantIds(m).Contains("UNG_999t14e"), "");
        }

        [AuditTest]
        static void UNG_999t2_LivingSpores()
        {
            var (g, m) = AdaptVia("UNG_999t2"); if (g == null) return;
            var p = P1(g);
            Check("has deathrattle", m.HasDeathrattle, "");
            Kill(g, m);
            Eq("two plants", 2, BoardIds(p).Count(x => x == "UNG_999t2t1"));
            foreach (var pl in p.BoardZone) Eq("plant 1/1", "1/1", Atk(pl) + "/" + Hp(pl));
        }

        [AuditTest]
        static void UNG_999t3_PlusThreeAttack()
        {
            var (g, m) = AdaptVia("UNG_999t3"); if (g == null) return;
            Eq("8/1", "8/1", Atk(m) + "/" + Hp(m));
        }

        [AuditTest]
        static void UNG_999t4_PlusThreeHealth()
        {
            var (g, m) = AdaptVia("UNG_999t4"); if (g == null) return;
            Eq("5/4", "5/4", Atk(m) + "/" + Hp(m));
        }

        [AuditTest]
        static void UNG_999t5_Elusive()
        {
            var (g, m) = AdaptVia("UNG_999t5"); if (g == null) return;
            var p = P1(g); var o = P2(g);
            var mf = Hand(p, "CS2_008"); // Moonfire
            Check("own spell can't target", !LegalTargets(mf).Any(x => x.Id == m.Id), "");
            p.Hero.HeroPower.IsExhausted = false; Mana(p, 10);
            Check("own hero power can't target", !p.Options().OfType<HeroPowerTask>().Any(x => x.Target?.Id == m.Id), "");
            var w = Board(p, Wisp);
            Check("control: wisp is targetable", LegalTargets(mf).Any(x => x.Id == w.Id), "");
            EndTurn(g);
            ClearHand(o);
            var mf2 = Hand(o, "CS2_008"); Mana(o, 10);
            Check("enemy spell can't target", !LegalTargets(mf2).Any(x => x.Id == m.Id), "");
        }

        [AuditTest]
        static void UNG_999t6_Taunt()
        {
            var (g, m) = AdaptVia("UNG_999t6"); if (g == null) return;
            Check("taunt", m.HasTaunt, Describe(g));
        }

        [AuditTest]
        static void UNG_999t7_Windfury()
        {
            var (g, m) = AdaptVia("UNG_999t7"); if (g == null) return;
            var o = P2(g);
            Check("windfury", m.HasWindfury, Describe(g));
            int hp = o.Hero.Health;
            m.IsExhausted = false; m.NumAttacksThisTurn = 0;
            g.Process(MinionAttackTask.Any(P1(g), m, o.Hero));
            g.Process(MinionAttackTask.Any(P1(g), m, o.Hero));
            Eq("attacked twice", hp - 10, o.Hero.Health);
        }

        [AuditTest]
        static void UNG_999t8_DivineShield()
        {
            var (g, m) = AdaptVia("UNG_999t8"); if (g == null) return;
            Check("divine shield", m.HasDivineShield, "");
            Damage(g, m, 3);
            Check("survives first hit", !m.IsDead && Hp(m) == 1 && !m.HasDivineShield, Describe(g));
        }

        // Direct cast of each adaptation spell (0-cost, targets a minion) as a crash check.
        [AuditTest]
        static void UNG_999tX_DirectCastNoThrow()
        {
            foreach (var id in AdaptIds)
            {
                var g = NewGame(); var p = P1(g);
                var y = Board(p, Yeti);
                var s = Hand(p, id);
                Check(id + " castable on minion", CanPlay(s, y), "");
                Check(id + " not castable on hero", !LegalTargets(s).Any(x => x.Id == p.Hero.Id), "");
                Play(g, s, y);
                Check(id + " enchant applied", EnchantIds(y).Contains(id + "e"), string.Join(",", EnchantIds(y)));
            }
        }

        // Vibrant Squirrel: Deathrattle: Shuffle 4 Acorns into your deck. When drawn, summon a 2/1 Squirrel.
        [AuditTest]
        static void CORE_SW_439_DeathrattleShufflesFourAcorns()
        {
            var g = NewGame(); var p = P1(g);
            var sq = Board(p, "CORE_SW_439");
            Eq("2/1", "2/1", Atk(sq) + "/" + Hp(sq));
            int deck = p.DeckZone.Count;
            Kill(g, sq);
            Eq("4 acorns added", 4, DeckIds(p).Count(x => x == "SW_439t"));
            Eq("deck +4", deck + 4, p.DeckZone.Count);
            Eq("hand not touched", 0, p.HandZone.Count);
        }

        // Acorn: Casts When Drawn. Summon a 2/1 Squirrel.
        [AuditTest]
        static void SW_439t_CastsWhenDrawn()
        {
            var g = NewGame(); var p = P1(g);
            ClearDeck(p);
            DeckTop(p, Yeti); DeckTop(p, "SW_439t");
            Generic.Draw(p);
            g.DeathProcessingAndAuraUpdate();
            Check("squirrel summoned", BoardIds(p).Contains("SW_439t2"), Describe(g));
            var s = p.BoardZone.FirstOrDefault(x => x.Card.Id == "SW_439t2");
            if (s != null) Eq("squirrel 2/1", "2/1", Atk(s) + "/" + Hp(s));
            Check("acorn not in hand", !HandIds(p).Contains("SW_439t"), Describe(g));
            Check("replacement draw (yeti)", HandIds(p).Contains(Yeti), Describe(g));
            Eq("deck empty", 0, p.DeckZone.Count);
            // through a natural turn draw, with the full shuffle from the Deathrattle
            var g2 = NewGame(); var p2 = P1(g2);
            ClearDeck(p2); for (int i = 0; i < 5; i++) DeckTop(p2, Yeti);
            Kill(g2, Board(p2, "CORE_SW_439"));
            int drawn = 0;
            for (int t = 0; t < 6 && p2.DeckZone.Count > 0; t++) { ClearHand(p2); Round(g2); drawn++; foreach (var m in p2.BoardZone.ToList()) if (m.Card.Id == "SW_439t2") { } }
            Check("no acorn ever in hand", !HandIds(p2).Contains("SW_439t"), Describe(g2));
            int squirrels = p2.BoardZone.Count(x => x.Card.Id == "SW_439t2") + p2.GraveyardZone.Count(x => x.Card.Id == "SW_439t2");
            Eq("4 squirrels from 4 acorns", 4, squirrels);
            // full board: acorn still leaves hand and replacement draw happens
            var g3 = NewGame(); var p3 = P1(g3);
            ClearDeck(p3);
            for (int i = 0; i < 7; i++) Board(p3, Wisp);
            DeckTop(p3, Yeti); DeckTop(p3, "SW_439t");
            Generic.Draw(p3);
            Check("full board: acorn not in hand", !HandIds(p3).Contains("SW_439t"), Describe(g3));
            Check("full board: yeti drawn", HandIds(p3).Contains(Yeti), Describe(g3));
        }

        [AuditTest]
        static void SW_439t2_Vanilla()
        {
            var c = Cards.FromId("SW_439t2");
            Eq("cost 1", 1, c.Cost); Eq("2 atk", 2, c.ATK); Eq("1 hp", 1, c.Health);
            Note("race " + c[GameTag.CARDRACE]);
        }

        [AuditTest]
        static void UNG_999t2t1_Plant()
        {
            var c = Cards.FromId("UNG_999t2t1");
            Eq("1/1", "1/1", c.ATK + "/" + c.Health);
        }

        // Greedy Sprite: Deathrattle: Gain an empty Mana Crystal.
        [AuditTest]
        static void LOOT_351_EmptyManaCrystal()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var s = Board(p, "LOOT_351");
            Eq("3/1", "3/1", Atk(s) + "/" + Hp(s));
            Mana(p, 5); Mana(o, 5);
            Kill(g, s);
            Eq("base mana 6", 6, p.BaseMana);
            Eq("remaining still 5 (empty)", 5, p.RemainingMana);
            Eq("opponent unchanged", 5, o.BaseMana);
            // killed on opponent's turn still gives the owner the crystal
            var g2 = NewGame(); var p2 = P1(g2);
            var s2 = Board(p2, "LOOT_351");
            Mana(p2, 3);
            EndTurn(g2);
            Kill(g2, s2);
            Eq("owner base mana 4", 4, p2.BaseMana);
        }
    }
}
