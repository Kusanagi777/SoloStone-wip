// Audit chunk class-ROGUE (chunk 11): one or more tests per card id, checked against the facts text.
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Audit.H;

namespace Audit
{
    internal static class RogueChunk11
    {
        static void Combo(Game g, Controller p) { PlayNew(g, p, Wisp); }
        /// <summary>Replaces the deck after the opening hand was drawn (NewGame draws then clears hands).</summary>
        static void SetDeck(Controller c, IEnumerable<string> ids) { ClearDeck(c); foreach (var id in ids) DeckTop(c, id); }
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

        static Minion PlayMinion(Game g, Controller p, string id, IPlayable target = null, int chooseOne = 0, int pos = -1)
            => (Minion)PlayNew(g, p, id, target, chooseOne, pos);

        // ---------------------------------------------------------------- BOT_283 Pogo-Hopper
        [AuditTest]
        static void BOT_283_GainsPerOtherPogoPlayed()
        {
            var g = NewGame(); var p = P1(g);
            var a = PlayMinion(g, p, "BOT_283");
            Eq("1st hopper 1/1 atk", 1, Atk(a)); Eq("1st hopper 1/1 hp", 1, Hp(a));
            var b = PlayMinion(g, p, "BOT_283");
            Eq("2nd hopper 3 atk", 3, Atk(b)); Eq("2nd hopper 3 hp", 3, Hp(b));
            Kill(g, a); Kill(g, b);
            var c = PlayMinion(g, p, "BOT_283");
            Eq("3rd hopper 5 atk (dead ones still count)", 5, Atk(c)); Eq("3rd hopper 5 hp", 5, Hp(c));
            // Summoned (not played) hopper does not count
            Board(p, "BOT_283");
            var d = PlayMinion(g, p, "BOT_283");
            Eq("4th played hopper 7 atk (summoned one not counted)", 7, Atk(d));
            var cl = Clone(g); Eq("clone keeps 7 atk", 7, Atk(At(cl, d)));
        }

        // ---------------------------------------------------------------- EDR_521 Tricky Satyr
        [AuditTest]
        static void EDR_521_CopiesLowestCostEnemyHandCard()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            Hand(o, Golem); Hand(o, "CS2_029"); Hand(o, Yeti); // Fireball 4, yeti 4, golem 7
            Hand(o, "CS2_189"); // Elven Archer cost 1
            var s = PlayMinion(g, p, "EDR_521");
            Eq("satyr 4 atk", 4, Atk(s)); Eq("satyr 3 hp", 3, Hp(s));
            Eq("got copy of the 1-cost Elven Archer", "CS2_189", HandIds(p).FirstOrDefault());
            Eq("one card gained", 1, p.HandZone.Count);
            Eq("opponent hand unchanged", 4, o.HandZone.Count);
            // empty enemy hand: nothing, no throw
            var g2 = NewGame(); var p2 = P1(g2);
            PlayMinion(g2, p2, "EDR_521");
            Eq("no card when enemy hand empty", 0, p2.HandZone.Count);
        }

        // ---------------------------------------------------------------- TRL_077 / TRL_077e Gurubashi Hypemon
        [AuditTest]
        static void TRL_077_DiscoverBattlecryOneOneCostOne()
        {
            for (int seed = 1; seed <= 4; seed++)
            {
                var g = NewGame(s => s.Seed = seed); var p = P1(g);
                var h = PlayMinion(g, p, "TRL_077");
                Eq($"s{seed} hypemon 5/7 atk", 5, Atk(h)); Eq($"s{seed} hypemon 7 hp", 7, Hp(h));
                var ch = ChoiceIds(g, p);
                Eq($"s{seed} 3 options", 3, ch.Count);
                Check($"s{seed} all options battlecry minions", ch.All(id => Cards.FromId(id).Type == CardType.MINION && Cards.FromId(id)[GameTag.BATTLECRY] == 1), string.Join(",", ch));
                Choose(g, p, 0);
                var got = p.HandZone.LastOrDefault() as Minion;
                Check($"s{seed} got a minion in hand", got != null, Describe(g));
                if (got == null) continue;
                Eq($"s{seed} copy is 1 attack", 1, got.AttackDamage);
                Eq($"s{seed} copy is 1 health", 1, got.Health);
                Eq($"s{seed} copy costs 1", 1, got.Cost);
                Check($"s{seed} carries TRL_077e", EnchantIds(got).Contains("TRL_077e"), string.Join(",", EnchantIds(got)));
                var cl = Clone(g); var gc = At(cl, got);
                Eq($"s{seed} clone keeps cost 1", 1, gc.Cost);
            }
        }

        // ---------------------------------------------------------------- BOT_565 / BOT_565t
        [AuditTest]
        static void BOT_565_DeathrattleSummonsPoisonousRushOoze()
        {
            var g = NewGame(); var p = P1(g);
            var cr = PlayMinion(g, p, "BOT_565");
            Eq("crawler 2 atk", 2, Atk(cr)); Eq("crawler 4 hp", 4, Hp(cr));
            Kill(g, cr);
            Eq("ooze summoned", "BOT_565t", BoardIds(p).FirstOrDefault());
            var oz = p.BoardZone.FirstOrDefault();
            if (oz == null) return;
            Eq("ooze 1 atk", 1, Atk(oz)); Eq("ooze 1 hp", 1, Hp(oz));
            Check("ooze poisonous", oz.Poisonous); Check("ooze rush", oz.IsRush);
            // Rush: can attack a minion immediately, poisonous kills it
            var foe = Board(P2(g), Golem);
            Check("ooze can attack minion the turn it is summoned", g.Process(SabberStoneCore.Tasks.PlayerTasks.MinionAttackTask.Any(p, oz, foe)), Describe(g));
            Check("golem died to poison", foe.Zone != P2(g).BoardZone, Describe(g));
        }

        // ---------------------------------------------------------------- CAP_004 Disguised Operator
        [AuditTest]
        static void CAP_004_OwnSide_RushAndOpponentDraws2()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var op = (Minion)Hand(p, "CAP_004");
            Check("play on own side ok", Play(g, op, null, 1, 0), Describe(g));
            Check("on my board", p.BoardZone.Contains(op), Describe(g));
            Eq("4 atk", 4, Atk(op)); Eq("2 hp", 2, Hp(op));
            Check("rush", op.IsRush);
            int oh = o.HandZone.Count, ph = p.HandZone.Count;
            Kill(g, op);
            Eq("opponent drew 2", oh + 2, o.HandZone.Count);
            Eq("I drew 0", ph, p.HandZone.Count);
        }

        [AuditTest]
        static void CAP_004_OpponentSide_ThenIDraw2()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var op = (Minion)Hand(p, "CAP_004");
            Check("play on opponent side ok", Play(g, op, null, 2, 0), Describe(g));
            Check("on opponent board", o.BoardZone.Contains(op), Describe(g));
            Check("controlled by opponent", op.Controller == o);
            int oh = o.HandZone.Count, ph = p.HandZone.Count;
            Kill(g, op);
            Eq("its opponent (me) drew 2", ph + 2, p.HandZone.Count);
            Eq("boss drew 0", oh, o.HandZone.Count);
        }

        // ---------------------------------------------------------------- GIL_557 Cursed Castaway
        [AuditTest]
        static void GIL_557_RushAndDrawComboCard()
        {
            var g = NewGame(); var p = P1(g);
            SetDeck(p, new List<string> { Yeti, Yeti, "CORE_EX1_134", Yeti, Yeti });
            var cc = PlayMinion(g, p, "GIL_557");
            Eq("5 atk", 5, Atk(cc)); Eq("3 hp", 3, Hp(cc)); Check("rush", cc.IsRush);
            Kill(g, cc);
            Eq("drew the combo card", "CORE_EX1_134", HandIds(p).FirstOrDefault());
            Eq("drew exactly one", 1, p.HandZone.Count);
            Check("combo card left the deck", !DeckIds(p).Contains("CORE_EX1_134"));
            // No combo card: draws nothing
            var g2 = NewGame(); var p2 = P1(g2);
            var c2 = PlayMinion(g2, p2, "GIL_557"); Kill(g2, c2);
            Eq("no combo card, no draw", 0, p2.HandZone.Count);
        }

        // ---------------------------------------------------------------- CAP_002 Follow the Footsteps
        [AuditTest]
        static void CAP_002_DiscoverStealthAndChainSameTurn()
        {
            var g = NewGame(); var p = P1(g);
            PlayNew(g, p, "CAP_002");
            var ch = ChoiceIds(g, p);
            Eq("3 options", 3, ch.Count);
            Check("options are Stealth minions", ch.All(id => Cards.FromId(id).Type == CardType.MINION && Cards.FromId(id)[GameTag.STEALTH] == 1), string.Join(",", ch));
            string pick = Pick(g, p, 0);
            var m = p.HandZone.FirstOrDefault(e => e.Card.Id == pick);
            Check("picked minion in hand", m != null, Describe(g));
            if (m == null) return;
            Check("it carries CAP_002e", EnchantIds(m).Contains("CAP_002e"), string.Join(",", EnchantIds(m)));
            if (p.BoardZone.IsFull) return;
            Play(g, m);
            var ch2 = ChoiceIds(g, p);
            Eq("playing it this turn opens another Stealth Discover", 3, ch2.Count);
            Check("second options stealth minions", ch2.All(id => Cards.FromId(id)[GameTag.STEALTH] == 1), string.Join(",", ch2));
            string pick2 = Pick(g, p, 0);
            var m2 = p.HandZone.FirstOrDefault(e => e.Card.Id == pick2);
            Check("second pick carries CAP_002e", m2 != null && EnchantIds(m2).Contains("CAP_002e"), Describe(g));
        }

        [AuditTest]
        static void CAP_002_EffectExpiresAfterTurn()
        {
            var g = NewGame(); var p = P1(g);
            PlayNew(g, p, "CAP_002");
            string pick = Pick(g, p, 0);
            var m = p.HandZone.FirstOrDefault(e => e.Card.Id == pick);
            if (m == null) { Check("picked", false); return; }
            Round(g);
            ClearHand(P2(g));
            Check("grant gone next turn", !EnchantIds(m).Contains("CAP_002e"), string.Join(",", EnchantIds(m)));
            Play(g, m);
            Eq("no discover when played on a later turn", 0, ChoiceIds(g, p).Count);
        }

        // ---------------------------------------------------------------- TIME_711 / TIME_711e Flashback
        [AuditTest]
        static void TIME_711_TwoOneCostMinions_ComboPlusOne()
        {
            for (int seed = 1; seed <= 3; seed++)
            {
                var g = NewGame(s => s.Seed = seed); var p = P1(g);
                PlayNew(g, p, "TIME_711");
                Eq($"s{seed} two summoned", 2, p.BoardZone.Count);
                foreach (var m in p.BoardZone)
                {
                    Eq($"s{seed} {m.Card.Id} costs 1", 1, m.Card.Cost);
                    Eq($"s{seed} {m.Card.Id} no buff without combo", m.Card[GameTag.ATK], m.AttackDamage);
                }
                var g2 = NewGame(s => s.Seed = seed); var p2 = P1(g2);
                Combo(g2, p2);
                PlayNew(g2, p2, "TIME_711");
                var summoned = p2.BoardZone.Where(m => m.Card.Id != Wisp || m.AppliedEnchantments?.Any() == true).Skip(0).ToList();
                var two = p2.BoardZone.Skip(1).ToList();
                Eq($"s{seed} combo: two summoned", 2, two.Count);
                foreach (var m in two)
                {
                    Eq($"s{seed} combo {m.Card.Id} costs 1", 1, m.Card.Cost);
                    Eq($"s{seed} combo {m.Card.Id} +1 attack", m.Card[GameTag.ATK] + 1, m.AttackDamage);
                    Check($"s{seed} combo carries TIME_711e", EnchantIds(m).Contains("TIME_711e"));
                }
            }
        }

        // ---------------------------------------------------------------- TIME_712 Dethrone
        [AuditTest]
        static void TIME_712_DestroyAndComboSummonEight()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var foe = Board(o, Golem);
            PlayNew(g, p, "TIME_712", foe);
            Check("target destroyed", foe.Zone != o.BoardZone, Describe(g));
            Eq("no summon without combo", 0, p.BoardZone.Count);

            var g2 = NewGame(); var p2 = P1(g2); var o2 = P2(g2);
            var foe2 = Board(o2, Golem);
            Combo(g2, p2);
            PlayNew(g2, p2, "TIME_712", foe2);
            Check("combo: target destroyed", foe2.Zone != o2.BoardZone, Describe(g2));
            var extra = p2.BoardZone.Where(m => m.Card.Id != Wisp).ToList();
            Eq("combo: one minion summoned", 1, extra.Count);
            if (extra.Count == 1) Eq("combo: it costs 8", 8, extra[0].Card.Cost);
            // friendly minion is a legal target too
            var g3 = NewGame(); var p3 = P1(g3);
            var mine = Board(p3, Yeti);
            PlayNew(g3, p3, "TIME_712", mine);
            Check("can destroy friendly minion", mine.Zone != p3.BoardZone);
            var sp = Hand(p3, "TIME_712");
            Check("hero is not a legal target", !LegalTargets(sp).Any(t => t is Hero));
        }

        // ---------------------------------------------------------------- CS2_080 Assassin's Blade
        [AuditTest]
        static void CS2_080_ThreeFour()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            PlayNew(g, p, "CS2_080");
            Eq("equipped", "CS2_080", Weapon(p));
            Eq("3 attack", 3, p.Hero.Weapon.AttackDamage);
            Eq("4 durability", 4, p.Hero.Weapon.Durability);
            HeroAttack(g, p, o.Hero);
            Eq("boss at 27", 27, o.Hero.Health);
            Eq("durability 3", 3, p.Hero.Weapon.Durability);
        }

        // ---------------------------------------------------------------- FIR_922 / FIR_922e Cindersword
        [AuditTest]
        static void FIR_922_PlusThreeWhenHoldingDarkGift()
        {
            var g = NewGame(); var p = P1(g);
            PlayNew(g, p, "FIR_922");
            Eq("base 1 attack", 1, p.Hero.Weapon.AttackDamage);
            Eq("2 durability", 2, p.Hero.Weapon.Durability);

            var g2 = NewGame(); var p2 = P1(g2);
            var gifted = Hand(p2, Yeti);
            bool given = DarkGifts.Give(gifted, p2.Hero);
            Check("dark gift given to held minion", given && DarkGifts.Has(gifted), string.Join(",", EnchantIds(gifted)));
            PlayNew(g2, p2, "FIR_922");
            Eq("with dark gift held: 4 attack", 4, p2.Hero.Weapon?.AttackDamage ?? -1);
            Check("FIR_922e on weapon", p2.Hero.Weapon != null && EnchantIds(p2.Hero.Weapon).Contains("FIR_922e"));

            // holding a non-gifted minion: no buff
            var g3 = NewGame(); var p3 = P1(g3);
            Hand(p3, Yeti);
            PlayNew(g3, p3, "FIR_922");
            Eq("plain minion held: 1 attack", 1, p3.Hero.Weapon.AttackDamage);
        }

        // ---------------------------------------------------------------- ICC_850 / ICC_850e Shadowblade
        [AuditTest]
        static void ICC_850_HeroImmuneThisTurnOnly()
        {
            var g = NewGame(); var p = P1(g);
            PlayNew(g, p, "ICC_850");
            Eq("3 attack", 3, p.Hero.Weapon.AttackDamage);
            Eq("2 durability", 2, p.Hero.Weapon.Durability);
            Check("hero immune", p.Hero.IsImmune);
            int hp = p.Hero.Health;
            Damage(g, p.Hero, 5);
            Eq("no damage while immune", hp, p.Hero.Health);
            EndTurn(g);
            Check("immune gone on boss turn", !p.Hero.IsImmune, Describe(g));
            Damage(g, p.Hero, 5);
            Eq("damage taken after the turn", hp - 5, p.Hero.Health);
        }

        // ---------------------------------------------------------------- AT_028 / AT_028e Shado-Pan Rider
        [AuditTest]
        static void AT_028_ComboPlusThreeAttack()
        {
            var g = NewGame(); var p = P1(g);
            var a = PlayMinion(g, p, "AT_028");
            Eq("no combo 3 atk", 3, Atk(a)); Eq("7 hp", 7, Hp(a));
            var b = PlayMinion(g, p, "AT_028");
            Eq("combo 6 atk", 6, Atk(b)); Eq("combo 7 hp", 7, Hp(b));
            Check("AT_028e", EnchantIds(b).Contains("AT_028e"));
        }

        // ---------------------------------------------------------------- AT_030 Undercity Valiant
        [AuditTest]
        static void AT_030_ComboDeal1ToCharacter()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var foe = Board(o, Yeti);
            var a = PlayMinion(g, p, "AT_030");
            Eq("3 atk", 3, Atk(a)); Eq("2 hp", 2, Hp(a));
            Eq("no combo, yeti untouched", 5, Hp(foe));
            var b = (Minion)Hand(p, "AT_030");
            Check("combo can target enemy hero", LegalTargets(b).Contains(o.Hero));
            Play(g, b, foe);
            Eq("combo: yeti takes 1", 4, Hp(foe));
            PlayNew(g, p, "AT_030", o.Hero);
            Eq("combo: hero takes 1", 29, o.Hero.Health);
        }

        // ---------------------------------------------------------------- CAP_006 Tricks of the Trade
        [AuditTest]
        static void CAP_006_Deal1_Or3AfterStealthAttack()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var foe = Board(o, Golem);
            PlayNew(g, p, "CAP_006", foe);
            Eq("deals 1", 6, Hp(foe));
            PlayNew(g, p, "CAP_006", o.Hero);
            Eq("can hit hero for 1", 29, o.Hero.Health);

            var g2 = NewGame(); var p2 = P1(g2); var o2 = P2(g2);
            var t = Hand(p2, "CAP_006");
            var st = Board(p2, "CFM_636"); // stealth 5/1
            Attack(g2, st, o2.Hero);
            Eq("rager hit hero", 25, o2.Hero.Health);
            var foe2 = Board(o2, Golem);
            Play(g2, t, foe2);
            Eq("powered: deals 3", 4, Hp(foe2));

            // stealth minion attacked BEFORE the card was in hand: only 1
            var g3 = NewGame(); var p3 = P1(g3); var o3 = P2(g3);
            var st3 = Board(p3, "CFM_636");
            Attack(g3, st3, o3.Hero);
            var foe3 = Board(o3, Golem);
            PlayNew(g3, p3, "CAP_006", foe3);
            Eq("attack happened before holding: 1", 6, Hp(foe3));

            // spell damage applies
            var g4 = NewGame(); var p4 = P1(g4); var o4 = P2(g4);
            Board(p4, "CS2_142"); // Kobold Geomancer, Spell Damage +1
            var foe4 = Board(o4, Golem);
            PlayNew(g4, p4, "CAP_006", foe4);
            Eq("spell damage +1: deals 2", 5, Hp(foe4));
        }

        // ---------------------------------------------------------------- CORE_EX1_131 / EX1_131t
        [AuditTest]
        static void CORE_EX1_131_ComboSummonsBandit()
        {
            var g = NewGame(); var p = P1(g);
            var a = PlayMinion(g, p, "CORE_EX1_131");
            Eq("3 atk", 3, Atk(a)); Eq("2 hp", 2, Hp(a));
            Eq("no combo: alone", 1, p.BoardZone.Count);
            PlayMinion(g, p, "CORE_EX1_131");
            Check("combo: bandit summoned", BoardIds(p).Contains("EX1_131t"), string.Join(",", BoardIds(p)));
            var bandit = p.BoardZone.FirstOrDefault(m => m.Card.Id == "EX1_131t");
            if (bandit != null) { Eq("bandit 2 atk", 2, Atk(bandit)); Eq("bandit 1 hp", 1, Hp(bandit)); }
        }

        // ---------------------------------------------------------------- CORE_EX1_134 SI:7 Agent
        [AuditTest]
        static void CORE_EX1_134_ComboDeal3()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var foe = Board(o, Golem);
            var a = PlayMinion(g, p, "CORE_EX1_134");
            Eq("3 atk", 3, Atk(a)); Eq("3 hp", 3, Hp(a));
            Eq("no combo: no damage", 7, Hp(foe));
            PlayNew(g, p, "CORE_EX1_134", foe);
            Eq("combo: 3 damage to minion", 4, Hp(foe));
            PlayNew(g, p, "CORE_EX1_134", o.Hero);
            Eq("combo: 3 damage to hero", 27, o.Hero.Health);
        }

        // ---------------------------------------------------------------- LOOT_211 Elven Minstrel
        [AuditTest]
        static void LOOT_211_ComboDrawTwoMinions()
        {
            var deck = new List<string> { "CS2_029", "CS2_029", Wisp, "CS2_029", Golem, "CS2_029" };
            var g = NewGame(); var p = P1(g); SetDeck(p, deck);
            var a = PlayMinion(g, p, "LOOT_211");
            Eq("3 atk", 3, Atk(a)); Eq("2 hp", 2, Hp(a));
            Eq("no combo: no draw", 0, p.HandZone.Count);
            var b = (Minion)Hand(p, "LOOT_211");
            Check("can play with no target", Play(g, b), Describe(g));
            Check("on board", p.BoardZone.Contains(b));
            var hand = HandIds(p);
            Eq("combo: drew 2", 2, hand.Count);
            Check("both drawn are minions", hand.All(id => Cards.FromId(id).Type == CardType.MINION), string.Join(",", hand));
        }

        // ---------------------------------------------------------------- OG_070 / OG_070e Bladed Cultist
        [AuditTest]
        static void OG_070_ComboPlusOnePlusOne()
        {
            var g = NewGame(); var p = P1(g);
            var a = PlayMinion(g, p, "OG_070");
            Eq("1 atk", 1, Atk(a)); Eq("2 hp", 2, Hp(a));
            var b = PlayMinion(g, p, "OG_070");
            Eq("combo 2 atk", 2, Atk(b)); Eq("combo 3 hp", 3, Hp(b));
        }

        // ---------------------------------------------------------------- AT_029 / AT_029e Buccaneer
        [AuditTest]
        static void AT_029_EquipWeaponPlusOne()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var b = PlayMinion(g, p, "AT_029");
            Eq("2 atk", 2, Atk(b)); Eq("1 hp", 1, Hp(b));
            PlayNew(g, p, "CS2_080");
            Eq("weapon 4 attack", 4, p.Hero.Weapon.AttackDamage);
            // enemy equipping does not trigger
            EndTurn(g);
            PlayNew(g, o, "CS2_106"); // Fiery War Axe 3/2
            Eq("enemy weapon untouched", 3, o.Hero.Weapon.AttackDamage);
        }

        // ---------------------------------------------------------------- CORE_KAR_069 Swashburglar
        [AuditTest]
        static void CORE_KAR_069_RandomOtherClassCard()
        {
            for (int seed = 1; seed <= 6; seed++)
            {
                var g = NewGame(s => { s.Seed = seed; s.PlayerHero = "HERO_03"; }); var p = P1(g);
                var m = PlayMinion(g, p, "CORE_KAR_069");
                Eq($"s{seed} 1 atk", 1, Atk(m)); Eq($"s{seed} 2 hp", 2, Hp(m));
                Eq($"s{seed} one card", 1, p.HandZone.Count);
                var c = p.HandZone.FirstOrDefault();
                if (c == null) continue;
                Check($"s{seed} other class card", c.Card.Class != CardClass.ROGUE && c.Card.Class != CardClass.NEUTRAL, c.Card.Id + " " + c.Card.Class);
            }
        }

        // ---------------------------------------------------------------- GIL_902 / GIL_902e Cutthroat Buccaneer
        [AuditTest]
        static void GIL_902_ComboWeaponPlusOne()
        {
            var g = NewGame(); var p = P1(g);
            PlayNew(g, p, "CS2_080"); // weapon, also activates combo
            var a = PlayMinion(g, p, "GIL_902");
            Eq("2 atk", 2, Atk(a)); Eq("4 hp", 4, Hp(a));
            Eq("combo: weapon 4 attack", 4, p.Hero.Weapon.AttackDamage);
            var g2 = NewGame(); var p2 = P1(g2);
            var w = PlayNew(g2, p2, "CS2_080");
            EndTurn(g2); EndTurn(g2);
            PlayMinion(g2, p2, "GIL_902");
            Eq("no combo: weapon 3", 3, p2.Hero.Weapon.AttackDamage);
            // combo with no weapon: no throw
            var g3 = NewGame(); var p3 = P1(g3);
            Combo(g3, p3);
            PlayMinion(g3, p3, "GIL_902");
            Check("no weapon no throw", p3.Hero.Weapon == null);
        }

        // ---------------------------------------------------------------- GVG_023 / GVG_023a Goblin Auto-Barber
        [AuditTest]
        static void GVG_023_BattlecryWeaponPlusOne()
        {
            var g = NewGame(); var p = P1(g);
            PlayNew(g, p, "CS2_080");
            var a = PlayMinion(g, p, "GVG_023");
            Eq("3 atk", 3, Atk(a)); Eq("2 hp", 2, Hp(a));
            Eq("weapon 4 attack", 4, p.Hero.Weapon.AttackDamage);
            var g2 = NewGame(); var p2 = P1(g2);
            PlayMinion(g2, p2, "GVG_023");
            Check("no weapon no throw", p2.Hero.Weapon == null);
        }

        // ---------------------------------------------------------------- LOOT_033 Cavern Shinyfinder
        [AuditTest]
        static void LOOT_033_DrawWeapon()
        {
            var g = NewGame(); var p = P1(g);
            SetDeck(p, new List<string> { Yeti, Yeti, "CS2_080", Yeti });
            var a = PlayMinion(g, p, "LOOT_033");
            Eq("3 atk", 3, Atk(a)); Eq("1 hp", 1, Hp(a));
            Eq("drew the weapon", "CS2_080", HandIds(p).FirstOrDefault());
            Eq("just one", 1, p.HandZone.Count);
            var g2 = NewGame(); var p2 = P1(g2);
            PlayMinion(g2, p2, "LOOT_033");
            Eq("no weapon in deck: no draw", 0, p2.HandZone.Count);
        }

        // ---------------------------------------------------------------- OG_267 / OG_267e Southsea Squidface
        [AuditTest]
        static void OG_267_DeathrattleWeaponPlusTwo()
        {
            var g = NewGame(); var p = P1(g);
            PlayNew(g, p, "CS2_080");
            var a = PlayMinion(g, p, "OG_267");
            Eq("4 atk", 4, Atk(a)); Eq("4 hp", 4, Hp(a));
            Eq("weapon still 3 while alive", 3, p.Hero.Weapon.AttackDamage);
            Kill(g, a);
            Eq("weapon 5 after death", 5, p.Hero.Weapon.AttackDamage);
            // dies on boss turn: still my weapon
            var g2 = NewGame(); var p2 = P1(g2);
            PlayNew(g2, p2, "CS2_080");
            var b = PlayMinion(g2, p2, "OG_267");
            EndTurn(g2);
            Kill(g2, b);
            Eq("dies on enemy turn: my weapon 5", 5, p2.Hero.Weapon.AttackDamage);
        }

        // ---------------------------------------------------------------- TRL_071 Bloodsail Howler
        [AuditTest]
        static void TRL_071_RushAndPerOtherPirate()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            Board(p, "AT_029"); Board(p, "GIL_902"); Board(p, Yeti); // 2 pirates + yeti
            Board(o, "AT_029"); // enemy pirate, not counted
            var h = PlayMinion(g, p, "TRL_071");
            Eq("1+2 atk", 3, Atk(h)); Eq("1+2 hp", 3, Hp(h));
            Check("rush", h.IsRush);
            var g2 = NewGame(); var p2 = P1(g2);
            var h2 = PlayMinion(g2, p2, "TRL_071");
            Eq("alone 1/1", 1, Atk(h2)); Eq("alone hp 1", 1, Hp(h2));
        }

        // ---------------------------------------------------------------- CAP_000 / CAP_000e SI:7 Slayer
        [AuditTest]
        static void CAP_000_StealthedAttackerGetsPlusTwo()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var sl = PlayMinion(g, p, "CAP_000");
            Eq("1 atk", 1, Atk(sl)); Eq("2 hp", 2, Hp(sl)); Check("stealth", sl.HasStealth);
            var rager = Board(p, "CFM_636"); // 5/1 stealth
            var plain = Board(p, Yeti);
            Attack(g, rager, o.Hero);
            Eq("stealthed rager hits for 7 (buff before damage)", 23, o.Hero.Health);
            Eq("rager 7 atk after", 7, Atk(rager)); Eq("rager 3 hp after", 3, Hp(rager));
            Check("rager lost stealth", !rager.HasStealth);
            Attack(g, plain, o.Hero);
            Eq("non-stealthed yeti no buff", 4, Atk(plain));
            Eq("boss at 19", 19, o.Hero.Health);
            // enemy stealthed minion attacking: no buff
            EndTurn(g);
            var es = Board(o, "CFM_636");
            Attack(g, es, p.Hero);
            Eq("enemy stealthed minion not buffed", 5, Atk(es));
        }

        // ---------------------------------------------------------------- CAP_003 SI:7 Supplier
        [AuditTest]
        static void CAP_003_DrawAfterAttack()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var s = PlayMinion(g, p, "CAP_003");
            Eq("1 atk", 1, Atk(s)); Eq("3 hp", 3, Hp(s)); Check("stealth", s.HasStealth);
            int h = p.HandZone.Count;
            Attack(g, s, o.Hero);
            Eq("drew 1 after attacking", h + 1, p.HandZone.Count);
            Eq("boss took 1", 29, o.Hero.Health);
        }

        // ---------------------------------------------------------------- CFM_634 Lotus Assassin
        [AuditTest]
        static void CFM_634_KillRegainsStealth()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var la = PlayMinion(g, p, "CFM_634");
            Eq("5 atk", 5, Atk(la)); Eq("5 hp", 5, Hp(la)); Check("stealth", la.HasStealth);
            var w = Board(o, Wisp);
            Attack(g, la, w);
            Check("wisp dead", w.Zone != o.BoardZone);
            Check("regained stealth after kill", la.HasStealth);
            var gm = Board(o, Golem);
            Attack(g, la, gm);
            Check("no kill: no stealth", !la.HasStealth);
            var g2 = NewGame(); var p2 = P1(g2); var o2 = P2(g2);
            var lb = PlayMinion(g2, p2, "CFM_634");
            Attack(g2, lb, o2.Hero);
            Check("hit hero: no stealth", !lb.HasStealth);
        }

        // ---------------------------------------------------------------- CFM_636 Shadow Rager
        [AuditTest]
        static void CFM_636_Stealth51()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var m = PlayMinion(g, p, "CFM_636");
            Eq("5 atk", 5, Atk(m)); Eq("1 hp", 1, Hp(m)); Check("stealth", m.HasStealth);
            EndTurn(g);
            var bolt = Hand(o, "CS2_029");
            Check("enemy cannot target it", !LegalTargets(bolt).Contains(m));
        }

        // ---------------------------------------------------------------- CFM_694 / CFM_694e Shadow Sensei
        [AuditTest]
        static void CFM_694_BuffStealthedMinion()
        {
            var g = NewGame(); var p = P1(g);
            var st = Board(p, "CFM_636"); var plain = Board(p, Yeti);
            var s = (Minion)Hand(p, "CFM_694");
            var lt = LegalTargets(s);
            Check("stealthed minion legal", lt.Contains(st));
            Check("non-stealthed not legal", !lt.Contains(plain));
            Play(g, s, st);
            Eq("sensei 4 atk", 4, Atk(s)); Eq("sensei 4 hp", 4, Hp(s));
            Eq("rager 7 atk", 7, Atk(st)); Eq("rager 3 hp", 3, Hp(st));
            var g2 = NewGame(); var p2 = P1(g2);
            var s2 = (Minion)Hand(p2, "CFM_694");
            Check("playable with no stealthed target", Play(g2, s2) && p2.BoardZone.Contains(s2));
        }

        // ---------------------------------------------------------------- CORE_BT_701 Spymistress
        [AuditTest]
        static void CORE_BT_701_Stealth31()
        {
            var g = NewGame(); var p = P1(g);
            var m = PlayMinion(g, p, "CORE_BT_701");
            Eq("3 atk", 3, Atk(m)); Eq("1 hp", 1, Hp(m)); Check("stealth", m.HasStealth);
            Eq("costs 1", 1, m.Card.Cost);
        }

        // ---------------------------------------------------------------- EX1_522 Patient Assassin
        [AuditTest]
        static void EX1_522_StealthPoisonous()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var m = PlayMinion(g, p, "EX1_522");
            Eq("1 atk", 1, Atk(m)); Eq("1 hp", 1, Hp(m)); Check("stealth", m.HasStealth); Check("poisonous", m.Poisonous);
            var gm = Board(o, Golem);
            Attack(g, m, gm);
            Check("golem destroyed by poison", gm.Zone != o.BoardZone, Describe(g));
        }

        // ---------------------------------------------------------------- TLC_515 Cultist Map
        [AuditTest]
        static void TLC_515_DiscoverFromDeck_PlayThisTurnPicksAnother()
        {
            var deck = new List<string> { Wisp, "CS2_189", "CS2_142", Yeti, Golem, "CS2_029" };
            var g = NewGame(); var p = P1(g); SetDeck(p, deck);
            int deckBefore = p.DeckZone.Count;
            PlayNew(g, p, "TLC_515");
            var ch = ChoiceIds(g, p);
            Eq("3 offers", 3, ch.Count);
            Check("offers come from deck", ch.All(deck.Contains), string.Join(",", ch));
            string pick = Pick(g, p, 0);
            Eq("drawn into hand", pick, HandIds(p).FirstOrDefault());
            Eq("deck shrank by 1", deckBefore - 1, p.DeckZone.Count);
            var rest = ch.Skip(1).ToList();
            var card = p.HandZone.First();
            Play(g, card, card.Card.Id == "CS2_029" ? P2(g).Hero : null);
            var ch2 = ChoiceIds(g, p);
            Eq("follow-up offers the other 2", 2, ch2.Count);
            Check("follow-up are the others", ch2.OrderBy(x => x).SequenceEqual(rest.OrderBy(x => x)), string.Join(",", ch2) + " vs " + string.Join(",", rest));
            string pick2 = Pick(g, p, 0);
            Check("second card in hand", HandIds(p).Contains(pick2), Describe(g));
            Eq("deck shrank by 2 total", deckBefore - 2, p.DeckZone.Count);
        }

        [AuditTest]
        static void TLC_515_NotPlayedThisTurnNoFollowUp()
        {
            var deck = new List<string> { Wisp, "CS2_189", "CS2_142", Yeti, Golem, "CS2_029" };
            var g = NewGame(); var p = P1(g); SetDeck(p, deck);
            PlayNew(g, p, "TLC_515");
            string pick = Pick(g, p, 0);
            var card = p.HandZone.FirstOrDefault(e => e.Card.Id == pick);
            Round(g);
            int deckNow = p.DeckZone.Count;
            if (card == null || card.Zone != p.HandZone) { Check("pick still in hand", false, Describe(g)); return; }
            Play(g, card, card.Card.Id == "CS2_029" ? P2(g).Hero : null);
            Eq("played next turn: no follow-up", 0, ChoiceIds(g, p).Count);
            Eq("deck untouched by map", deckNow, p.DeckZone.Count);
        }
    }
}
