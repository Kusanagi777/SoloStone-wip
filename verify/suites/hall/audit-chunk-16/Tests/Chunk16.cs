// Audit chunk 16 (boss-hollow_regent + boss-lady_of_thorns): one test per card id.
using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Audit.H;

namespace Audit
{
    internal static class Chunk16
    {
        static string Pick(Game g, Controller c, int index)
        {
            if (!g.Resolution.HasSuspendedEffect) return Choose(g, c, index);
            int id = c.Choice.Choices[index]; string cid = g.IdEntityDic[id].Card.Id;
            var pd = g.GetPendingDecisions().First(d => d.ControllerId == c.Id);
            bool ok = g.TryAnswer(new SabberStoneCore.Model.Decisions.DecisionAnswer(pd.Id, c.Id, new[] { id }), out string err);
            Check("TryAnswer accepted", ok, err ?? "");
            return cid;
        }
        static List<string> Pal(int n) => Enumerable.Repeat("CS2_087", n).ToList(); // Blessing of Might (paladin spell)
        /// <summary>Replaces the deck: first id = bottom, last id = top.</summary>
        static void SetDeck(Controller c, List<string> ids) { ClearDeck(c); foreach (var id in ids) DeckTop(c, id); }
        static List<Spell> Auras(Controller c) => c.SecretZone.Auras.ToList();

        // ---------------------------------------------------------------- Paladin (hollow_regent)

        [AuditTest]
        static void CATA_472_DeathrattleTriggersFriendlyEndOfTurn()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            PlayNew(g, p, "CATA_472");
            Eq("weapon equipped", "CATA_472", Weapon(p));
            Board(p, "CATA_475"); // Scalebreaker: end of turn 2 to all enemies
            var foe = Board(o, Yeti);
            p.Hero.Weapon.Destroy(); g.DeathProcessingAndAuraUpdate();
            Eq("enemy hero took 2", 28, o.Hero.Health);
            Eq("enemy yeti took 2", 3, Hp(foe));
            Eq("still player turn", p, g.CurrentPlayer);
            // no end-of-turn minion: nothing happens, no throw
            var g2 = NewGame(); var p2 = P1(g2);
            PlayNew(g2, p2, "CATA_472"); Board(p2, Yeti);
            p2.Hero.Weapon.Destroy(); g2.DeathProcessingAndAuraUpdate();
            Check("no crash, weapon gone", Weapon(p2) == null, Describe(g2));
        }

        [AuditTest]
        static void CATA_473_EndOfTurnShieldOrBuff()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var noz = Board(p, "CATA_473");
            var shielded = Board(p, Yeti); shielded.HasDivineShield = true;
            var plain = Board(p, Wisp);
            var foe = Board(o, Yeti);
            EndTurn(g);
            Eq("shielded yeti +3 atk", 7, Atk(shielded)); Eq("shielded yeti +3 hp", 8, Hp(shielded));
            Check("shielded yeti keeps shield", shielded.HasDivineShield);
            Check("wisp gains shield", plain.HasDivineShield); Eq("wisp not buffed", 1, Atk(plain));
            Check("nozdormu gains shield", noz.HasDivineShield); Eq("nozdormu not buffed", 4, Atk(noz));
            Check("enemy not shielded", !foe.HasDivineShield);
            Check("CATA_473e on yeti", EnchantIds(shielded).Contains("CATA_473e"));
            EndTurn(g); // boss end of turn: must not fire
            Eq("no buff on enemy end of turn (noz)", 4, Atk(noz));
            Eq("no buff on enemy end of turn (wisp)", 1, Atk(plain));
            EndTurn(g); // my end of turn again: all shielded -> all buffed
            Eq("nozdormu buffed second turn", 7, Atk(noz));
            Eq("wisp buffed second turn", 4, Atk(plain));
            var c = Clone(g); Eq("clone keeps buff", 7, Atk(At(c, noz)));
        }

        [AuditTest]
        static void CATA_473e_PlusThreeThree()
        {
            var g = NewGame(); var p = P1(g);
            var y = Board(p, Yeti);
            Generic.AddEnchantmentBlock(g, Cards.FromId("CATA_473e"), y, y);
            Eq("atk", 7, Atk(y)); Eq("hp", 8, Hp(y));
        }

        [AuditTest]
        static void CATA_474_EndOfTurnHolySpellDiscounted()
        {
            for (int seed = 1; seed <= 6; seed++)
            {
                var g = NewGame(s => s.Seed = seed); var p = P1(g);
                Board(p, "CATA_474");
                EndTurn(g);
                Eq($"seed {seed}: one card", 1, p.HandZone.Count);
                if (p.HandZone.Count == 1)
                {
                    var c = p.HandZone[0];
                    Check($"seed {seed}: spell", c.Card.Type == CardType.SPELL, c.Card.Id);
                    Eq($"seed {seed}: holy school", 5, c.Card[GameTag.SPELL_SCHOOL]);
                    Eq($"seed {seed}: cost reduced by 3", Math.Max(0, c.Card.Cost - 3), c.Cost);
                }
                ClearHand(P2(g));
                EndTurn(g); // boss end of turn; then my start-of-turn draw (a Yeti)
                Eq($"seed {seed}: no spell on enemy end of turn", 1, p.HandZone.Count(h => h.Card.Type == CardType.SPELL));
            }
        }

        [AuditTest]
        static void CATA_475_EndOfTurnTwoToAllEnemies()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            Board(p, "CATA_475");
            var mine = Board(p, Yeti);
            var foe = Board(o, Yeti); var foe2 = Board(o, Wisp);
            EndTurn(g);
            Eq("enemy hero -2", 28, o.Hero.Health);
            Eq("enemy yeti -2", 3, Hp(foe));
            Check("enemy wisp dead", foe2.IsDead || foe2.Zone != o.BoardZone);
            Eq("own yeti untouched", 5, Hp(mine));
            Eq("own hero untouched", 30, p.Hero.Health);
            EndTurn(g);
            Eq("no damage on enemy end of turn", 3, Hp(foe));
        }

        [AuditTest]
        static void CATA_479_FlightManeuversCombined()
        {
            var g = NewGame(); var p = P1(g);
            var existing = Board(p, Yeti);
            var card = Hand(p, "CATA_479");
            Note("hand after adding CATA_479: " + string.Join(",", HandIds(p)));
            var parent = p.HandZone.FirstOrDefault(c => c.Card.Id == "CATA_479");
            if (parent == null)
            {
                Note("parent split in hand; testing halves");
                Check("halves in hand", HandIds(p).Contains("CATA_479t") && HandIds(p).Contains("CATA_479t2"), string.Join(",", HandIds(p)));
                return;
            }
            Play(g, parent);
            var drakes = p.BoardZone.Where(m => m.Card.Id == "CATA_479t3").ToList();
            Eq("two drakes", 2, drakes.Count);
            foreach (var d in drakes) { Eq("drake atk 4+1", 5, Atk(d)); Eq("drake hp 2", 2, Hp(d)); Check("drake shield", d.HasDivineShield); }
            Eq("yeti +1 atk", 5, Atk(existing)); Check("yeti shield", existing.HasDivineShield);
        }

        [AuditTest]
        static void CATA_479_DrawSplitsAndHalvesWork()
        {
            var g = NewGame(); var p = P1(g);
            DeckTop(p, "CATA_479");
            Generic.Draw(p); g.DeathProcessingAndAuraUpdate();
            Note("hand after draw: " + string.Join(",", HandIds(p)));
            Check("drawn Shatter card in hand (split or merged)", HandIds(p).Any(i => i.StartsWith("CATA_479")), string.Join(",", HandIds(p)));
        }

        [AuditTest]
        static void CATA_479e_AttackAndShield()
        {
            var g = NewGame(); var p = P1(g);
            var y = Board(p, Yeti);
            Generic.AddEnchantmentBlock(g, Cards.FromId("CATA_479e"), y, y);
            Eq("atk", 5, Atk(y)); Check("shield", y.HasDivineShield); Eq("hp", 5, Hp(y));
        }

        [AuditTest]
        static void CATA_479t_SummonsTwoDrakes()
        {
            var g = NewGame(); var p = P1(g);
            var y = Board(p, Yeti);
            Play(g, Hand(p, "CATA_479t"));
            var drakes = p.BoardZone.Where(m => m.Card.Id == "CATA_479t3").ToList();
            Eq("two drakes", 2, drakes.Count);
            foreach (var d in drakes) { Eq("atk 4", 4, Atk(d)); Eq("hp 2", 2, Hp(d)); Check("no shield", !d.HasDivineShield); }
            Eq("yeti unbuffed", 4, Atk(y));
        }

        [AuditTest]
        static void CATA_479t2_BuffsFriendlyMinions()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var y = Board(p, Yeti); var w = Board(p, Wisp); var foe = Board(o, Yeti);
            Play(g, Hand(p, "CATA_479t2"));
            Eq("yeti +1", 5, Atk(y)); Check("yeti shield", y.HasDivineShield);
            Eq("wisp +1", 2, Atk(w)); Check("wisp shield", w.HasDivineShield);
            Eq("enemy unbuffed", 4, Atk(foe)); Check("enemy no shield", !foe.HasDivineShield);
            Eq("no drakes", 0, p.BoardZone.Count(m => m.Card.Id == "CATA_479t3"));
        }

        [AuditTest]
        static void CATA_479t3_SkyDrakeStats()
        {
            var g = NewGame(); var p = P1(g);
            var d = (Minion)PlayNew(g, p, "CATA_479t3");
            Eq("atk", 4, Atk(d)); Eq("hp", 2, Hp(d)); Eq("cost", 3, d.Card.Cost);
        }

        [AuditTest]
        static void EDR_258_ShieldsTakeThreeHits()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var t = Board(p, "EDR_258");
            Check("taunt", t.HasTaunt); Check("shield", t.HasDivineShield);
            var ally = Board(p, Yeti); ally.HasDivineShield = true;
            var foe = Board(o, Yeti); foe.HasDivineShield = true;
            Damage(g, t, 2); Check("toreth shield after hit 1", t.HasDivineShield); Eq("toreth hp after hit 1", 4, Hp(t));
            Damage(g, t, 2); Check("toreth shield after hit 2", t.HasDivineShield); Eq("toreth hp after hit 2", 4, Hp(t));
            Damage(g, t, 2); Check("toreth shield gone after hit 3", !t.HasDivineShield); Eq("toreth hp after hit 3", 4, Hp(t));
            Damage(g, t, 1); Eq("toreth takes damage after shield broke", 3, Hp(t));
            Damage(g, ally, 1); Damage(g, ally, 1);
            Check("ally shield survives 2 hits", ally.HasDivineShield); Eq("ally hp", 5, Hp(ally));
            Damage(g, ally, 1); Check("ally shield breaks on 3rd", !ally.HasDivineShield);
            Damage(g, foe, 1); Check("enemy shield breaks on first hit", !foe.HasDivineShield);
        }

        [AuditTest]
        static void END_011_TemporaryManaEachStartOfTurn()
        {
            var g = NewGame(); var p = P1(g);
            var a = Hand(p, "END_011");
            Play(g, a);
            Eq("aura active", 1, Auras(p).Count(x => x.Card.Id == "END_011"));
            int[] expect = { 1, 1, 1, 0 };
            for (int i = 0; i < 4; i++)
            {
                p.BaseMana = 3; p.UsedMana = 0;
                EndTurn(g); ClearHand(P2(g)); EndTurn(g);
                // at start of my turn base mana becomes 4
                Eq($"turn {i + 1}: base mana", 4, p.BaseMana);
                Eq($"turn {i + 1}: remaining mana (base + temp)", 4 + expect[i], p.RemainingMana);
            }
            Eq("aura gone after 3 turns", 0, Auras(p).Count(x => x.Card.Id == "END_011"));
        }

        [AuditTest]
        static void JAIL_035_SummonsTwoIfNoNeutrals()
        {
            var g = NewGame(s => s.PlayerDeck = Pal(10)); var p = P1(g);
            var s1 = (Minion)PlayNew(g, p, "JAIL_035");
            Check("taunt", s1.HasTaunt);
            Eq("three sentries", 3, p.BoardZone.Count(m => m.Card.Id == "JAIL_035"));
            var g2 = NewGame(); var p2 = P1(g2); // Yetis = neutral
            PlayNew(g2, p2, "JAIL_035");
            Eq("one sentry with neutral deck", 1, p2.BoardZone.Count);
        }

        [AuditTest]
        static void JAIL_327_SummonsCheapMinionAtEndOfTurn()
        {
            var deck = Enumerable.Repeat(Wisp, 10).Concat(Enumerable.Repeat(Yeti, 10)).ToList();
            var g = NewGame(s => { s.PlayerDeck = deck; }); var p = P1(g); SetDeck(p, deck);
            int deckBefore = p.DeckZone.Count;
            Play(g, Hand(p, "JAIL_327"));
            EndTurn(g);
            Eq("one minion summoned", 1, p.BoardZone.Count);
            if (p.BoardZone.Count == 1) Check("costs 2 or less", p.BoardZone[0].Cost <= 2, p.BoardZone[0].Card.Id);
            Eq("removed from deck", deckBefore - 1, p.DeckZone.Count);
            ClearHand(P2(g)); EndTurn(g);
            Eq("nothing on enemy end of turn", 1, p.BoardZone.Count);
            EndTurn(g); EndTurn(g); EndTurn(g);
            Eq("three summons over 3 turns", 3, p.BoardZone.Count);
            EndTurn(g); EndTurn(g);
            Eq("no 4th summon", 3, p.BoardZone.Count);
            Eq("aura gone", 0, Auras(p).Count);
        }

        [AuditTest]
        static void JAIL_397_TauntBody()
        {
            var g = NewGame(); var p = P1(g);
            var m = (Minion)PlayNew(g, p, "JAIL_397");
            Check("taunt", m.HasTaunt); Eq("atk", 5, Atk(m)); Eq("hp", 6, Hp(m));
            Note("deckbuilding clause (pick a 2-Cost minion, ten copies) is outside the engine");
        }

        [AuditTest]
        static void JAIL_516_SummonsTwoCheapWithRush()
        {
            var deck = new List<string> { Wisp, "CS2_120", Yeti, Yeti, Yeti, Yeti, Yeti, Yeti, Yeti, Yeti };
            var g = NewGame(s => { s.PlayerDeck = deck; }); var p = P1(g); SetDeck(p, deck);
            Note("deck after mulligan: " + string.Join(",", DeckIds(p)));
            int before = p.DeckZone.Count;
            var r = PlayNew(g, p, "JAIL_516");
            var others = p.BoardZone.Where(m => m != r).ToList();
            Eq("two summoned", 2, others.Count);
            foreach (var m in others) { Check("cost <= 2", m.Cost <= 2, m.Card.Id); Check("rush", m.IsRush, m.Card.Id); }
            Eq("deck -2", before - 2, p.DeckZone.Count);
            Check("recruiter no rush", !((Minion)r).IsRush);
            var g2 = NewGame(); var p2 = P1(g2); SetDeck(p2, new List<string> { Wisp, Yeti, Yeti, Yeti, Yeti, Yeti, Yeti, Yeti, Yeti });
            PlayNew(g2, p2, "JAIL_516");
            Eq("only one eligible -> one summoned", 2, p2.BoardZone.Count);
        }

        [AuditTest]
        static void JAIL_516e_Rush()
        {
            var g = NewGame(); var p = P1(g);
            var y = Board(p, Yeti);
            Generic.AddEnchantmentBlock(g, Cards.FromId("JAIL_516e"), y, y);
            Check("rush", y.IsRush);
        }

        [AuditTest]
        static void TIME_009_PutsOneOfEachAuraIntoPlay()
        {
            var deck = new List<string> { "TIME_700", "TIME_700", "JAIL_327", Yeti, Yeti, Yeti, Yeti, Yeti, Yeti };
            var g = NewGame(s => { s.PlayerDeck = deck; }); var p = P1(g); SetDeck(p, deck);
            PlayNew(g, p, "TIME_009");
            var ids = Auras(p).Select(a => a.Card.Id).ToList();
            Eq("one TIME_700 active", 1, ids.Count(i => i == "TIME_700"));
            Eq("one JAIL_327 active", 1, ids.Count(i => i == "JAIL_327"));
            Eq("one TIME_700 left in deck", 1, DeckIds(p).Count(i => i == "TIME_700"));
            Eq("JAIL_327 gone from deck", 0, DeckIds(p).Count(i => i == "JAIL_327"));
            EndTurn(g);
            Check("chronological aura summoned a dragon", BoardIds(p).Contains("TIME_700t"), Describe(g));
        }

        [AuditTest]
        static void TIME_009t1_RestoresFourToFriendlies()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var y = Board(p, Yeti);
            Damage(g, p.Hero, 10); Damage(g, y, 4); Damage(g, o.Hero, 10);
            var foe = Board(o, Yeti); Damage(g, foe, 4);
            Play(g, Hand(p, "TIME_009t1"));
            EndTurn(g);
            Eq("hero +4", 24, p.Hero.Health); Eq("yeti +4", 5, Hp(y));
            Eq("enemy hero not healed", 20, o.Hero.Health); Eq("enemy minion not healed", 1, Hp(foe));
            Eq("remaining duration 2", 2, AuraSpellRules.Remaining(Auras(p).First()));
        }

        [AuditTest]
        static void TIME_009t2_BuffsRandomFriendly()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var y = Board(p, Yeti); var foe = Board(o, Yeti);
            Play(g, Hand(p, "TIME_009t2"));
            EndTurn(g);
            Eq("yeti +4 atk", 8, Atk(y)); Eq("yeti +4 hp", 9, Hp(y)); Check("yeti shield", y.HasDivineShield);
            Eq("enemy unchanged", 4, Atk(foe));
            ClearHand(o); EndTurn(g);
            Eq("no buff on enemy end of turn", 8, Atk(y));
        }

        [AuditTest]
        static void TIME_019_DamagesEnemiesIfAura()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var foe = Board(o, Yeti); var mine = Board(p, Yeti);
            PlayNew(g, p, "TIME_019");
            Eq("no aura: enemy hero untouched", 30, o.Hero.Health);
            Play(g, Hand(p, "TIME_700"));
            PlayNew(g, p, "TIME_019");
            Eq("aura: enemy hero -3", 27, o.Hero.Health);
            Eq("aura: enemy yeti -3", 2, Hp(foe));
            Eq("own yeti untouched", 5, Hp(mine));
            Eq("own hero untouched", 30, p.Hero.Health);
        }

        [AuditTest]
        static void TIME_700_SummonsTauntDragonThreeTurns()
        {
            var g = NewGame(); var p = P1(g);
            Play(g, Hand(p, "TIME_700"));
            EndTurn(g);
            var d = p.BoardZone.FirstOrDefault(m => m.Card.Id == "TIME_700t");
            Check("dragon summoned", d != null, Describe(g));
            if (d != null) { Eq("atk 3", 3, Atk(d)); Eq("hp 5", 5, Hp(d)); Check("taunt", d.HasTaunt); Check("dragon", d.Card.IsRace(Race.DRAGON)); }
            ClearHand(P2(g)); EndTurn(g);
            Eq("none on enemy end", 1, p.BoardZone.Count);
            for (int i = 0; i < 4; i++) { EndTurn(g); ClearHand(P2(g)); EndTurn(g); }
            Eq("three dragons total", 3, p.BoardZone.Count(m => m.Card.Id == "TIME_700t"));
        }

        [AuditTest]
        static void CATA_479_SplitsInNonEmptyHandAndHalvesPlay()
        {
            var g = NewGame(); var p = P1(g);
            Hand(p, Yeti); Hand(p, Yeti);
            DeckTop(p, "CATA_479");
            Generic.Draw(p); g.DeathProcessingAndAuraUpdate();
            Note("hand after draw into 2 cards: " + string.Join(",", HandIds(p)));
            Check("split into both halves", HandIds(p).Contains("CATA_479t") && HandIds(p).Contains("CATA_479t2"), string.Join(",", HandIds(p)));
            var first = p.HandZone.FirstOrDefault(h => h.Card.Id == "CATA_479t");
            var second = p.HandZone.FirstOrDefault(h => h.Card.Id == "CATA_479t2");
            if (first == null || second == null) return;
            Play(g, first);
            Eq("first half: two drakes", 2, p.BoardZone.Count(m => m.Card.Id == "CATA_479t3"));
            Check("first half: drakes no shield", p.BoardZone.All(m => !m.HasDivineShield));
            Play(g, second);
            Check("second half: all shielded", p.BoardZone.All(m => m.HasDivineShield));
            Check("second half: drakes 5 atk", p.BoardZone.All(m => m.AttackDamage == 5), Describe(g));
        }

        [AuditTest]
        static void BossSide_EndOfTurnCardsFireOnBossTurn()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var mine = Board(p, Yeti);
            EndTurn(g); // boss turn
            ClearHand(o);
            Board(o, "CATA_475"); Board(o, "CATA_474");
            Mana(o, 10);
            Play(g, Hand(o, "TIME_700"));
            Play(g, Hand(o, "TIME_009t1"));
            Damage(g, o.Hero, 10, p.Hero);
            EndTurn(g); // boss end of turn
            Eq("boss Scalebreaker hits player hero", 28, p.Hero.Health);
            Eq("boss Scalebreaker hits player minion", 3, Hp(mine));
            Eq("boss Spearheart gives boss a spell", 1, o.HandZone.Count(h => h.Card.Type == CardType.SPELL));
            Check("boss Chronological Aura summons dragon", BoardIds(o).Contains("TIME_700t"), Describe(g));
            Eq("boss Gnomish Aura heals boss hero", 24, o.Hero.Health);
            EndTurn(g); // player end of turn: none of them fire
            Eq("no Scalebreaker on player end", 28, p.Hero.Health);
            Eq("no second dragon on player end", 1, BoardIds(o).Count(i => i == "TIME_700t"));
        }

        // ---------------------------------------------------------------- Druid (lady_of_thorns)

        [AuditTest]
        static void CAP_407_DiscoverExpensiveMinionWithPrepare()
        {
            for (int seed = 1; seed <= 4; seed++)
            {
                var g = NewGame(s => s.Seed = seed); var p = P1(g);
                Play(g, Hand(p, "CAP_407"));
                var offers = ChoiceIds(g, p);
                Eq($"seed {seed}: three offers", 3, offers.Count);
                foreach (var id in offers)
                {
                    var c = Cards.FromId(id);
                    Check($"seed {seed}: {id} minion", c.Type == CardType.MINION);
                    Check($"seed {seed}: {id} costs >= 5", c.Cost >= 5, "cost " + c.Cost);
                }
                if (offers.Count == 0) continue;
                string picked = Pick(g, p, 0);
                var inHand = p.HandZone.FirstOrDefault(h => h.Card.Id == picked);
                Check($"seed {seed}: picked in hand", inHand != null, string.Join(",", HandIds(p)));
                if (inHand != null) Check($"seed {seed}: has Prepare", inHand[GameTag.PREPARE] != 0);
            }
        }

        [AuditTest]
        static void CATA_135_SpendsAllManaOnGolems()
        {
            var g = NewGame(); var p = P1(g);
            var c = Hand(p, "CATA_135");
            Play(g, c); // 10 mana, costs 2 -> 8 spent
            var golems = p.BoardZone.Where(m => m.Card.Id == "CATA_135t").ToList();
            Eq("two golems", 2, golems.Count);
            foreach (var m in golems) { Eq("atk 1+8", 9, Atk(m)); Eq("hp 2+8", 10, Hp(m)); }
            Eq("mana 0", 0, p.RemainingMana);
            var g2 = NewGame(); var p2 = P1(g2);
            Mana(p2, 2);
            Play(g2, Hand(p2, "CATA_135"), keepMana: true);
            foreach (var m in p2.BoardZone) { Eq("no extra mana: 1 atk", 1, Atk(m)); Eq("no extra mana: 2 hp", 2, Hp(m)); }
            var cl = Clone(g); Eq("clone keeps buff", 9, Atk(At(cl, golems[0])));
        }

        [AuditTest]
        static void CATA_135e_ScriptStats()
        {
            var g = NewGame(); var p = P1(g);
            var y = Board(p, Yeti);
            Generic.AddEnchantmentBlock(g, Cards.FromId("CATA_135e"), y, y, 3, 3, 0);
            Eq("atk +3", 7, Atk(y)); Eq("hp +3", 8, Hp(y));
        }

        [AuditTest]
        static void CATA_135t_GolemStats()
        {
            var g = NewGame(); var p = P1(g);
            var m = (Minion)PlayNew(g, p, "CATA_135t");
            Eq("atk", 1, Atk(m)); Eq("hp", 2, Hp(m));
        }

        [AuditTest]
        static void CATA_136_ShufflesFiveDoubledExpensiveMinions()
        {
            for (int seed = 1; seed <= 3; seed++)
            {
                var g = NewGame(s => s.Seed = seed); var p = P1(g);
                var before = p.DeckZone.ToList();
                Play(g, Hand(p, "CATA_136"));
                var added = p.DeckZone.Where(e => !before.Contains(e)).ToList();
                Eq($"seed {seed}: five added", 5, added.Count);
                foreach (var e in added)
                {
                    Check($"seed {seed}: {e.Card.Id} minion", e.Card.Type == CardType.MINION);
                    Check($"seed {seed}: {e.Card.Id} cost >= 8", e.Card.Cost >= 8, "cost " + e.Card.Cost);
                    var m = (Minion)e;
                    Eq($"seed {seed}: {e.Card.Id} atk doubled", e.Card.ATK * 2, m.AttackDamage);
                    Eq($"seed {seed}: {e.Card.Id} hp doubled", e.Card.Health * 2, m.Health);
                }
                // draw one of them and check stats kept
                if (added.Count > 0)
                {
                    var e = (Minion)added[0];
                    int idx = p.DeckZone.ToList().IndexOf(e);
                    Generic.Draw(p, idx); g.DeathProcessingAndAuraUpdate();
                    Check($"seed {seed}: drawn", e.Zone == p.HandZone);
                    Eq($"seed {seed}: drawn atk still doubled", e.Card.ATK * 2, e.AttackDamage);
                }
            }
        }

        [AuditTest]
        static void CATA_136e_Doubles()
        {
            var g = NewGame(); var p = P1(g);
            var y = Board(p, Yeti);
            Generic.AddEnchantmentBlock(g, Cards.FromId("CATA_136e"), y, y);
            Eq("atk doubled", 8, Atk(y)); Eq("hp doubled", 10, Hp(y));
        }

        [AuditTest]
        static void CORE_EX1_169_GainOneManaThisTurn()
        {
            var g = NewGame(); var p = P1(g);
            Mana(p, 3);
            Play(g, Hand(p, "CORE_EX1_169"), keepMana: true);
            Eq("3 + 1 mana", 4, p.RemainingMana);
            Eq("base unchanged", 3, p.BaseMana);
            EndTurn(g); EndTurn(g);
            Eq("next turn normal", 4, p.RemainingMana);
        }

        [AuditTest]
        static void CORE_SCH_713_TaxesOpponentSpellsNextTurn()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var oppSpell = Hand(o, "CS2_029"); // Fireball 4
            var oppMinion = Hand(o, Wisp);
            var mySpell = Hand(p, "CS2_029");
            var m = PlayNew(g, p, "CORE_SCH_713");
            Eq("atk", 3, Atk((Minion)m)); Eq("hp", 2, Hp((Minion)m));
            Eq("my spell not taxed", 4, mySpell.Cost);
            EndTurn(g);
            Eq("opponent spell +1 on their turn", 5, oppSpell.Cost);
            Eq("opponent minion not taxed", oppMinion.Card.Cost, oppMinion.Cost);
            EndTurn(g);
            Eq("tax gone after their turn", 4, oppSpell.Cost);
        }

        [AuditTest]
        static void SCH_713e_IsPartOfNeophyte()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            PlayNew(g, p, "CORE_SCH_713");
            var later = Hand(o, "CS2_029"); // spell entering hand after the battlecry
            EndTurn(g);
            Eq("spell drawn later is also taxed", 5, later.Cost);
        }

        [AuditTest]
        static void Core_LOE_115_ChooseOneDiscover()
        {
            for (int mode = 1; mode <= 2; mode++)
            {
                var g = NewGame(); var p = P1(g);
                Play(g, Hand(p, "Core_LOE_115"), chooseOne: mode);
                var offers = ChoiceIds(g, p);
                Eq($"mode {mode}: three offers", 3, offers.Count);
                var want = mode == 1 ? CardType.MINION : CardType.SPELL;
                foreach (var id in offers) Check($"mode {mode}: {id} is {want}", Cards.FromId(id).Type == want);
                if (offers.Count == 0) continue;
                string picked = Pick(g, p, 0);
                Check($"mode {mode}: picked in hand", HandIds(p).Contains(picked), string.Join(",", HandIds(p)));
            }
        }

        [AuditTest]
        static void EDR_270_DiscoverNatureSpellCostsTwoLess()
        {
            for (int seed = 1; seed <= 4; seed++)
            {
                var g = NewGame(s => { s.Seed = seed; s.PlayerHero = "HERO_06"; }); var p = P1(g);
                Play(g, Hand(p, "EDR_270"));
                var offers = ChoiceIds(g, p);
                Eq($"seed {seed}: three offers", 3, offers.Count);
                foreach (var id in offers)
                {
                    var c = Cards.FromId(id);
                    Check($"seed {seed}: {id} spell", c.Type == CardType.SPELL);
                    Eq($"seed {seed}: {id} nature", 4, c[GameTag.SPELL_SCHOOL]);
                }
                if (offers.Count == 0) continue;
                string picked = Pick(g, p, 0);
                var h = p.HandZone.FirstOrDefault(x => x.Card.Id == picked);
                Check($"seed {seed}: in hand", h != null);
                if (h != null) Eq($"seed {seed}: cost -2", Math.Max(0, h.Card.Cost - 2), h.Cost);
            }
        }

        [AuditTest]
        static void END_007_PressTheAdvantage()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var foe = Board(o, Yeti);
            DeckTop(p, Wisp);
            Play(g, Hand(p, "END_007"), foe);
            Eq("1 damage", 4, Hp(foe));
            Eq("hero +1 attack", 1, Atk(p.Hero));
            Eq("drew 1", 1, p.HandZone.Count);
            Eq("armor 1", 1, Armor(p));
            Play(g, Hand(p, "END_007"), o.Hero);
            Eq("can target enemy hero", 29, o.Hero.Health);
            EndTurn(g);
            Eq("attack gone next turn", 0, Atk(p.Hero));
        }

        [AuditTest]
        static void JAIL_507_SummonsTauntByMana()
        {
            for (int seed = 1; seed <= 4; seed++)
            {
                var g = NewGame(s => s.Seed = seed); var p = P1(g);
                Mana(p, 5);
                var c = Hand(p, "JAIL_507"); Play(g, c, keepMana: true);
                var other = p.BoardZone.Where(m => m != c).ToList();
                Eq($"seed {seed}: one summoned at 5 mana", 1, other.Count);
                if (other.Count == 1) { Eq($"seed {seed}: cost 2", 2, other[0].Card.Cost); Check($"seed {seed}: taunt", other[0].HasTaunt, other[0].Card.Id); }
                var g2 = NewGame(s => s.Seed = seed); var p2 = P1(g2);
                var c2 = Hand(p2, "JAIL_507"); Play(g2, c2); // 10 mana
                var other2 = p2.BoardZone.Where(m => m != c2).ToList();
                Eq($"seed {seed}: one summoned at 10", 1, other2.Count);
                if (other2.Count == 1) { Eq($"seed {seed}: cost 6", 6, other2[0].Card.Cost); Check($"seed {seed}: taunt 6", other2[0].HasTaunt, other2[0].Card.Id); }
            }
        }

        [AuditTest]
        static void JAIL_860_SetsManaToTenAfterFiveTurns()
        {
            var deck = Enumerable.Repeat(Wisp, 20).ToList(); deck.Add("JAIL_860");
            var g = NewGame(s => { s.PlayerDeck = deck; s.Mana = 0; }); var p = P1(g);
            var seen = new List<int>();
            for (int turn = 1; turn <= 7; turn++)
            {
                seen.Add(p.BaseMana);
                ClearHand(p);
                EndTurn(g); ClearHand(P2(g)); EndTurn(g);
            }
            Note("base mana at start of my turns 1..7: " + string.Join(",", seen));
            Eq("turn 5 base mana normal", 5, seen[4]);
            Eq("turn 6 base mana 10", 10, seen[5]);
            Eq("turn 7 base mana 10", 10, seen[6]);
            // a 4-cost card in deck: no effect
            var deck2 = Enumerable.Repeat(Wisp, 20).ToList(); deck2.Add("JAIL_860"); deck2.Add(Yeti);
            var g2 = NewGame(s => { s.PlayerDeck = deck2; s.Mana = 0; }); var p2 = P1(g2);
            for (int turn = 1; turn <= 5; turn++) { ClearHand(p2); EndTurn(g2); ClearHand(P2(g2)); EndTurn(g2); }
            Eq("deck with 4-cost: turn 6 base mana 6", 6, p2.BaseMana);
        }

        [AuditTest]
        static void JAIL_866_DrawTwoMinionsBuffAtTen()
        {
            var deck = new List<string> { "CS2_029", Yeti, "CS2_029", Wisp, "CS2_029", "CS2_029", "CS2_029", "CS2_029", "CS2_029", "CS2_029" };
            var g = NewGame(s => { s.PlayerDeck = deck; }); var p = P1(g); SetDeck(p, deck);
            Play(g, Hand(p, "JAIL_866")); // 10 base mana
            Eq("drew 2", 2, p.HandZone.Count);
            Check("both minions", p.HandZone.All(h => h.Card.Type == CardType.MINION), string.Join(",", HandIds(p)));
            foreach (var h in p.HandZone.OfType<Minion>()) { Eq($"{h.Card.Id} +3 atk", h.Card.ATK + 3, h.AttackDamage); Eq($"{h.Card.Id} +3 hp", h.Card.Health + 3, h.Health); }
            var g2 = NewGame(s => { s.PlayerDeck = deck; }); var p2 = P1(g2); SetDeck(p2, deck);
            Mana(p2, 5);
            Play(g2, Hand(p2, "JAIL_866"), keepMana: true);
            Eq("drew 2 at 5", 2, p2.HandZone.Count);
            foreach (var h in p2.HandZone.OfType<Minion>()) Eq($"{h.Card.Id} not buffed", h.Card.ATK, h.AttackDamage);
        }

        [AuditTest]
        static void JAIL_872_DrawAfterHeroAttacks()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var rider = Board(p, "JAIL_872");
            PlayNew(g, p, "CS2_106"); // Fiery War Axe 3/2
            int hand0 = p.HandZone.Count;
            HeroAttack(g, p, o.Hero);
            Eq("enemy hero hit", 27, o.Hero.Health);
            Eq("drew one after hero attack", hand0 + 1, p.HandZone.Count);
            var y = Board(p, Yeti);
            Attack(g, y, o.Hero);
            Eq("minion attack does not draw", hand0 + 1, p.HandZone.Count);
            EndTurn(g);
            ClearHand(o);
            int myHand = p.HandZone.Count;
            PlayNew(g, o, "CS2_106");
            HeroAttack(g, o, p.Hero);
            Eq("enemy hero attack: I don't draw", myHand, p.HandZone.Count);
        }

        [AuditTest]
        static void TIME_701_DiscoverFromDeckOthersToBottom()
        {
            var deck = new List<string> { Wisp, Wisp, Wisp, "CS2_120", "CS2_029", Golem, "CS2_106", Yeti, Yeti, Yeti, Yeti };
            var g = NewGame(s => { s.PlayerDeck = deck; }); var p = P1(g); SetDeck(p, deck);
            int before = p.DeckZone.Count;
            Play(g, Hand(p, "TIME_701"));
            var offerIds = p.Choice?.Choices.ToList() ?? new List<int>();
            Eq("three offers", 3, offerIds.Count);
            if (offerIds.Count == 0) return;
            Note("offers: " + string.Join(",", ChoiceIds(g, p)) + " deck " + string.Join(",", DeckIds(p)));
            var offerCards = ChoiceIds(g, p);
            string picked = Pick(g, p, 0);
            Check("picked in hand", HandIds(p).Contains(picked), string.Join(",", HandIds(p)));
            Eq("deck -1", before - 1, p.DeckZone.Count);
            var rest = offerCards.Skip(1).OrderBy(x => x).ToList();
            var bottomTwo = DeckIds(p).Take(rest.Count).OrderBy(x => x).ToList();
            Note("deck order after (bottom first): " + string.Join(",", DeckIds(p)));
            Check("rejected offers on bottom", rest.SequenceEqual(bottomTwo), "rest " + string.Join(",", rest) + " bottom " + string.Join(",", bottomTwo));
            Check("offers came from deck", offerCards.All(id => deck.Contains(id)));
        }

        [AuditTest]
        static void TIME_702_ArmorIfMinionPlayedWhileHolding()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var ebb = Hand(p, "TIME_702");
            Play(g, ebb, o.Hero);
            Eq("3 damage to hero", 27, o.Hero.Health);
            Eq("no armor without minion", 0, Armor(p));
            var g2 = NewGame(); var p2 = P1(g2); var o2 = P2(g2);
            var foe = Board(o2, Yeti);
            var ebb2 = Hand(p2, "TIME_702");
            PlayNew(g2, p2, Wisp);
            EndTurn(g2); EndTurn(g2); // across turns still counts ("while holding")
            Play(g2, ebb2, foe);
            Eq("3 damage to minion", 2, Hp(foe));
            Eq("5 armor", 5, Armor(p2));
            // minion played before it was in hand does not count
            var g3 = NewGame(); var p3 = P1(g3);
            PlayNew(g3, p3, Wisp);
            var ebb3 = Hand(p3, "TIME_702");
            Play(g3, ebb3, P2(g3).Hero);
            Eq("minion before holding: no armor", 0, Armor(p3));
        }

        [AuditTest]
        static void JAIL_328_DeathrattlePaladinCardDiscounted()
        {
            for (int seed = 1; seed <= 4; seed++)
            {
                var g = NewGame(s => { s.Seed = seed; s.PlayerDeck = Pal(5); s.PlayerHero = "HERO_04"; }); var p = P1(g);
                var m = Board(p, "JAIL_328");
                Kill(g, m);
                Eq($"seed {seed}: got one card", 1, p.HandZone.Count);
                if (p.HandZone.Count == 1)
                {
                    var c = p.HandZone[0];
                    Eq($"seed {seed}: paladin", CardClass.PALADIN, c.Card.Class);
                    Eq($"seed {seed}: cost -2", Math.Max(0, c.Card.Cost - 2), c.Cost);
                }
            }
            var g2 = NewGame(); var p2 = P1(g2); // neutral Yetis
            Kill(g2, Board(p2, "JAIL_328"));
            Eq("neutral deck: nothing", 0, p2.HandZone.Count);
        }
    }
}
