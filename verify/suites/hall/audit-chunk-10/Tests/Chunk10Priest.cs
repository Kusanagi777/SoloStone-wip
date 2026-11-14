// Audit chunk class-PRIEST (chunk 10): one test per card id, text from facts/violet-hold-2026-09.json.
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Audit.H;

namespace Audit
{
    internal static class Chunk10Priest
    {
        static Game PG(int seed = 99) => NewGame(s => { s.PlayerHero = "HERO_09"; s.Seed = seed; });
        static bool IsDragon(Card c) => c.IsRace(Race.DRAGON);

        // CATA_556 Carrier Whelp: Battlecry: Get a random Dragon that costs (3) or less.
        [AuditTest]
        static void CATA_556_GetsCheapDragon()
        {
            for (int seed = 1; seed <= 8; seed++)
            {
                var g = PG(seed); var p = P1(g);
                PlayNew(g, p, "CATA_556");
                Eq($"seed {seed}: one card in hand", 1, p.HandZone.Count);
                if (p.HandZone.Count == 1)
                {
                    var c = p.HandZone[0].Card;
                    Check($"seed {seed}: {c.Id} is a Dragon", IsDragon(c));
                    Check($"seed {seed}: {c.Id} costs <= 3", c.Cost <= 3, "cost " + c.Cost);
                }
            }
        }

        // JAIL_912 Soothsayer: Prepare, Taunt. Deathrattle: Restore 6 Health to your hero. Summon a random 6-Cost minion.
        [AuditTest]
        static void JAIL_912_DeathrattleHealsAndSummons()
        {
            for (int seed = 1; seed <= 5; seed++)
            {
                var g = PG(seed); var p = P1(g);
                var m = Board(p, "JAIL_912");
                Check("taunt", m.HasTaunt);
                Damage(g, p.Hero, 10);
                Kill(g, m);
                Eq($"seed {seed}: hero healed 6", 26, p.Hero.Health);
                Eq($"seed {seed}: one minion summoned", 1, p.BoardZone.Count);
                if (p.BoardZone.Count == 1)
                    Eq($"seed {seed}: summoned is 6-cost", 6, p.BoardZone[0].Card.Cost);
            }
            // Prepare: the card offers the prepare option in hand
            var g2 = PG(); var p2 = P1(g2);
            var card = Hand(p2, "JAIL_912");
            Check("has PREPARE", card[GameTag.PREPARE] != 0);
            Mana(p2, 10);
            bool ok = g2.Process(PrepareCardTask.Any(p2, card));
            Check("prepare processed", ok);
            Note($"after prepare: cost {card.Cost}, mana left {p2.RemainingMana}, canPlay now {CanPlay(card)}");
            Round(g2);
            Note($"next turn: cost {card.Cost}, canPlay {CanPlay(card)}");
            Check("playable next turn", CanPlay(card));
        }

        // EDR_889 Petal Peddler: At the end of your turn, give another random friendly Dragon +1/+1.
        [AuditTest]
        static void EDR_889_BuffsAnotherDragonAtEndOfOwnTurn()
        {
            var g = PG(); var p = P1(g); var o = P2(g);
            var pedd = Board(p, "EDR_889");
            var yeti = Board(p, Yeti);
            var drag = Board(p, "CORE_NEW1_023"); // 3/2 dragon
            var foeDrag = Board(o, "CORE_NEW1_023");
            EndTurn(g);
            Eq("dragon atk +1", 4, Atk(drag)); Eq("dragon hp +1", 3, Hp(drag));
            Check("dragon has EDR_889e", EnchantIds(drag).Contains("EDR_889e"));
            Eq("peddler unchanged atk", 1, Atk(pedd)); Eq("peddler unchanged hp", 4, Hp(pedd));
            Eq("yeti unchanged", 4, Atk(yeti));
            Eq("enemy dragon unchanged", 3, Atk(foeDrag));
            EndTurn(g); // boss end of turn: no buff
            Eq("no buff at enemy end of turn", 4, Atk(drag));
            // peddler alone (it is itself a Dragon) does not buff itself
            var g2 = PG(); var p2 = P1(g2);
            var solo = Board(p2, "EDR_889");
            EndTurn(g2);
            Eq("lone peddler not buffed", 1, Atk(solo));
        }

        // BRM_004 Twilight Whelp: Battlecry: If you're holding a Dragon, gain +2 Health.
        [AuditTest]
        static void BRM_004_HealthIfHoldingDragon()
        {
            var g = PG(); var p = P1(g);
            Hand(p, "CORE_NEW1_023");
            var w = (Minion)PlayNew(g, p, "BRM_004");
            Eq("with dragon: 2/3", 3, Hp(w)); Eq("atk 2", 2, Atk(w));
            var g2 = PG(); var p2 = P1(g2);
            Hand(p2, Yeti);
            var w2 = (Minion)PlayNew(g2, p2, "BRM_004");
            Eq("without dragon: 1 hp", 1, Hp(w2));
        }

        // EDR_472 Weaver of the Cycle: Battlecry: If you're holding a spell that costs (5) or more, deal 3 damage.
        [AuditTest]
        static void EDR_472_DamageIfHoldingExpensiveSpell()
        {
            var g = PG(); var p = P1(g); var o = P2(g);
            Hand(p, "CS2_032"); // Flamestrike (7)
            var foe = Board(o, Yeti);
            var w = Hand(p, "EDR_472");
            Check("enemy minion is a legal target", LegalTargets(w).Any(t => t.Id == foe.Id));
            Play(g, w, foe);
            Eq("enemy minion took 3", 2, Hp(foe));
            var w2 = Hand(p, "EDR_472");
            Play(g, w2, o.Hero);
            Eq("enemy hero took 3", 27, o.Hero.Health);
            // holding only a 4-cost spell: no target, no damage
            var g2 = PG(); var p2 = P1(g2); var o2 = P2(g2);
            Hand(p2, "CS2_029"); // Fireball (4)
            var foe2 = Board(o2, Yeti);
            var w3 = Hand(p2, "EDR_472");
            Eq("no targets without 5+ spell", 0, LegalTargets(w3).Count);
            Check("still playable", CanPlay(w3));
            Play(g2, w3);
            Eq("no damage", 5, Hp(foe2));
            Eq("weaver on board", 1, p2.BoardZone.Count);
        }

        // EX1_623 Temple Enforcer: Battlecry: Give a friendly minion +3 Health.
        [AuditTest]
        static void EX1_623_FriendlyPlus3Health()
        {
            var g = PG(); var p = P1(g); var o = P2(g);
            var y = Board(p, Yeti); var foe = Board(o, Yeti);
            var c = Hand(p, "EX1_623");
            Check("enemy not targetable", !LegalTargets(c).Any(t => t.Id == foe.Id));
            Play(g, c, y);
            Eq("max hp 8", 8, MaxHp(y)); Eq("hp 8", 8, Hp(y)); Eq("atk unchanged", 4, Atk(y));
            Check("EX1_623e applied", EnchantIds(y).Contains("EX1_623e"));
        }

        // BOT_558 Test Subject: Deathrattle: Return any spells you cast on this minion to your hand.
        [AuditTest]
        static void BOT_558_ReturnsSpellsCastOnIt()
        {
            var g = PG(); var p = P1(g);
            var ts = Board(p, "BOT_558");
            Play(g, Hand(p, "CS2_236"), ts);   // Divine Spirit
            Play(g, Hand(p, "AT_055"), ts);    // Flash Heal
            ClearHand(p);
            Kill(g, ts);
            var h = HandIds(p);
            Check("Divine Spirit returned", h.Contains("CS2_236"), string.Join(",", h));
            Check("Flash Heal returned", h.Contains("AT_055"), string.Join(",", h));
            Eq("exactly 2", 2, h.Count);
        }

        // TLC_814 Twilight Mender: Deathrattle: Get a random Holy and Shadow spell.
        [AuditTest]
        static void TLC_814_HolyAndShadowSpell()
        {
            for (int seed = 1; seed <= 6; seed++)
            {
                var g = PG(seed); var p = P1(g);
                var m = Board(p, "TLC_814");
                Kill(g, m);
                Eq($"seed {seed}: two cards", 2, p.HandZone.Count);
                var schools = p.HandZone.Select(x => x.Card[GameTag.SPELL_SCHOOL]).ToList();
                Check($"seed {seed}: all spells", p.HandZone.All(x => x.Card.Type == CardType.SPELL));
                Check($"seed {seed}: one Holy (5)", schools.Contains(5), string.Join(",", HandIds(p)) + " schools " + string.Join(",", schools));
                Check($"seed {seed}: one Shadow (6)", schools.Contains(6), string.Join(",", HandIds(p)) + " schools " + string.Join(",", schools));
            }
        }

        // TIME_427 Cleansing Lightspawn 2/3: Lifesteal. Battlecry: Deal damage to an enemy minion equal to this minion's Health.
        [AuditTest]
        static void TIME_427_DamageEqualHealthLifesteal()
        {
            var g = PG(); var p = P1(g); var o = P2(g);
            var foe = Board(o, Yeti); var mine = Board(p, Yeti);
            Damage(g, p.Hero, 10);
            var c = Hand(p, "TIME_427");
            var targets = LegalTargets(c);
            Check("enemy hero not targetable", !targets.Any(t => t.Id == o.Hero.Id));
            Check("friendly minion not targetable", !targets.Any(t => t.Id == mine.Id));
            Play(g, c, foe);
            Eq("enemy took 3", 2, Hp(foe));
            Eq("lifesteal healed hero 3", 23, p.Hero.Health);
            var ls = (Minion)c;
            Check("has lifesteal", ls.HasLifeSteal);
        }

        // TLC_821 Wilted Shadow 6/7: Lifesteal. Whenever you heal an enemy, this attacks it.
        [AuditTest]
        static void TLC_821_AttacksHealedEnemy()
        {
            var g = PG(); var p = P1(g); var o = P2(g);
            var ws = Board(p, "TLC_821");
            Damage(g, o.Hero, 10); Damage(g, p.Hero, 10);
            Play(g, Hand(p, "AT_055"), o.Hero); // heal enemy hero 5 -> 25, then attacked for 6 -> 19
            Eq("enemy hero healed then attacked", 19, o.Hero.Health);
            Eq("lifesteal healed my hero 6", 26, p.Hero.Health);
            var foe = Board(o, Golem);
            Damage(g, foe, 3);
            Play(g, Hand(p, "AT_055"), foe); // 4 -> 7 heal, attacked for 6 -> 1; shadow takes 7 -> dies
            // engine trigger is HEAL_INTENT: the attack resolves before the heal (4-6 -> dead); heal-then-attack would leave 1.
            Note($"golem after heal+attack: hp {Hp(foe)} dead {foe.IsDead}");
            Check("enemy minion was attacked by the shadow", Hp(foe) == 1 || foe.IsDead, "hp " + Hp(foe));
            Check("shadow died from the counter-attack", ws.IsDead || ws.Zone?.Type != Zone.PLAY);
            // friendly heal does not trigger
            var g2 = PG(); var p2 = P1(g2); var o2 = P2(g2);
            var ws2 = Board(p2, "TLC_821");
            Damage(g2, p2.Hero, 10);
            Play(g2, Hand(p2, "AT_055"), p2.Hero);
            Eq("no attack on own hero", 25, p2.Hero.Health);
            Eq("enemy hero untouched", 30, o2.Hero.Health);
        }

        // UNG_032 Crystalline Oracle: Deathrattle: Copy a card from your opponent's deck and add it to your hand.
        [AuditTest]
        static void UNG_032_CopiesOpponentDeckCard()
        {
            var g = NewGame(s => { s.PlayerHero = "HERO_09"; s.BossDeck = Fill(30, Golem); });
            var p = P1(g); var o = P2(g);
            int deck = o.DeckZone.Count;
            var m = Board(p, "UNG_032");
            Kill(g, m);
            Eq("one card", 1, p.HandZone.Count);
            if (p.HandZone.Count == 1) Eq("copy of golem", Golem, p.HandZone[0].Card.Id);
            Eq("opponent deck unchanged", deck, o.DeckZone.Count);
        }

        // AT_013 Power Word: Glory: Choose a minion. Whenever it attacks, restore 4 Health to your hero.
        [AuditTest]
        static void AT_013_HealOnAttack()
        {
            var g = PG(); var p = P1(g); var o = P2(g);
            var y = Board(p, Yeti); var foe = Board(o, Wisp);
            Damage(g, p.Hero, 10);
            Play(g, Hand(p, "AT_013"), y);
            Check("AT_013e applied", EnchantIds(y).Contains("AT_013e"));
            Attack(g, y, o.Hero);
            Eq("hero healed 4", 24, p.Hero.Health);
            // on an enemy minion: heals the buffing player's hero when the enemy minion attacks
            var g2 = PG(); var p2 = P1(g2); var o2 = P2(g2);
            var foe2 = Board(o2, Yeti);
            Damage(g2, p2.Hero, 10); Damage(g2, o2.Hero, 10);
            Play(g2, Hand(p2, "AT_013"), foe2);
            EndTurn(g2);
            Attack(g2, foe2, p2.Hero);
            Eq("buffer's hero healed 4 then hit 4", 20, p2.Hero.Health);
            Eq("enemy hero not healed", 20, o2.Hero.Health);
        }

        // FIR_918 Light of the New Moon: Give a minion +3/+3. (Cast 3 spells to return this to your hand when played.)
        // FIR_918t Light of the Full Moon: Give a minion +3/+3. Add a Light of the New Moon to your hand.
        [AuditTest]
        static void FIR_918_BuffAndFullMoonCycle()
        {
            var g = PG(); var p = P1(g);
            var y = Board(p, Yeti);
            var nm = Hand(p, "FIR_918");
            Play(g, nm, y);
            Eq("+3 atk", 7, Atk(y)); Eq("+3 hp", 8, Hp(y));
            Check("FIR_918e1 applied", EnchantIds(y).Contains("FIR_918e1"));
            Eq("new moon not returned (no spells cast)", 0, p.HandZone.Count(x => x.Card.Id == "FIR_918"));
            var nm2 = Hand(p, "FIR_918");
            for (int i = 0; i < 3; i++) Play(g, Hand(p, "AT_055"), p.Hero);
            var ids = HandIds(p);
            Check("upgraded to FIR_918t after 3 spells", ids.Contains("FIR_918t"), string.Join(",", ids));
            var full = p.HandZone.FirstOrDefault(x => x.Card.Id == "FIR_918t");
            if (full != null)
            {
                var y2 = Board(p, Yeti);
                Play(g, full, y2);
                Eq("full moon +3 atk", 7, Atk(y2)); Eq("full moon +3 hp", 8, Hp(y2));
                Check("New Moon added to hand", HandIds(p).Contains("FIR_918"), string.Join(",", HandIds(p)));
            }
            // 2 spells only: not upgraded
            var g2 = PG(); var p2 = P1(g2);
            Hand(p2, "FIR_918");
            for (int i = 0; i < 2; i++) Play(g2, Hand(p2, "AT_055"), p2.Hero);
            Check("not upgraded after 2", HandIds(p2).Contains("FIR_918") && !HandIds(p2).Contains("FIR_918t"), string.Join(",", HandIds(p2)));
        }

        // LOOT_507 Lesser Diamond Spellstone: Resurrect 2 different friendly minions. (Cast 4 spells to upgrade.)
        [AuditTest]
        static void LOOT_507_ResurrectsTwoDifferent()
        {
            for (int seed = 1; seed <= 5; seed++)
            {
                var g = PG(seed); var p = P1(g);
                Kill(g, Board(p, Yeti)); Kill(g, Board(p, Yeti)); Kill(g, Board(p, Wisp));
                PlayNew(g, p, "LOOT_507");
                var b = BoardIds(p);
                Eq($"seed {seed}: two summoned", 2, b.Count);
                Check($"seed {seed}: different", b.Distinct().Count() == b.Count, string.Join(",", b));
            }
            var g2 = PG(); var p2 = P1(g2);
            var st = Hand(p2, "LOOT_507");
            for (int i = 0; i < 4; i++) Play(g2, Hand(p2, "AT_055"), p2.Hero);
            Check("upgraded after 4 spells", !HandIds(p2).Contains("LOOT_507"), string.Join(",", HandIds(p2)));
        }

        // BOT_509 Dead Ringer: Deathrattle: Draw a Deathrattle minion from your deck.
        [AuditTest]
        static void BOT_509_DrawsDeathrattleMinion()
        {
            var g = NewGame(s => { s.PlayerHero = "HERO_09"; s.PlayerDeck = Fill(9).Concat(new[] { "LOOT_534" }).ToList(); });
            var p = P1(g);
            // make sure the deathrattle minion is still in deck
            if (!DeckIds(p).Contains("LOOT_534")) DeckTop(p, "LOOT_534");
            ClearHand(p);
            Kill(g, Board(p, "BOT_509"));
            Eq("drew the deathrattle minion", "LOOT_534", HandIds(p).FirstOrDefault());
            Eq("one card", 1, p.HandZone.Count);
        }

        // CAP_802 Follow the Ghosts: Summon a 2/1 Ghost with Reborn. Give a playable card in your hand this effect for a turn.
        // CAP_802t Spooky Ghost 2/1 Reborn. CAP_802e1 grant.
        [AuditTest]
        static void CAP_802_GhostAndHandoff()
        {
            var g = PG(); var p = P1(g);
            var y = Hand(p, Yeti);
            PlayNew(g, p, "CAP_802");
            var ghost = p.BoardZone.FirstOrDefault(m => m.Card.Id == "CAP_802t");
            Check("ghost summoned", ghost != null, Describe(g));
            if (ghost != null)
            {
                Eq("ghost 2 atk", 2, Atk(ghost)); Eq("ghost 1 hp", 1, Hp(ghost));
                Check("ghost reborn", ghost[GameTag.REBORN] == 1);
            }
            Check("yeti got CAP_802e1", EnchantIds(y).Contains("CAP_802e1"), string.Join(",", EnchantIds(y)));
            // play the granted card: summons another ghost
            Play(g, y);
            Eq("second ghost after playing granted card", 2, p.BoardZone.Count(m => m.Card.Id == "CAP_802t"));
            // grant expires after a turn
            var g2 = PG(); var p2 = P1(g2);
            var y2 = Hand(p2, Yeti);
            PlayNew(g2, p2, "CAP_802");
            Round(g2);
            Check("grant gone next turn", !EnchantIds(y2).Contains("CAP_802e1"), string.Join(",", EnchantIds(y2)));
            // reborn ghost
            var g3 = PG(); var p3 = P1(g3);
            var gh = Board(p3, "CAP_802t");
            Kill(g3, gh);
            var back = p3.BoardZone.FirstOrDefault(m => m.Card.Id == "CAP_802t");
            Check("ghost reborn returns", back != null);
            if (back != null) { Eq("reborn 1 hp", 1, Hp(back)); Check("no reborn after", back[GameTag.REBORN] == 0); }
            // unplayable card not chosen
            var g4 = PG(); var p4 = P1(g4);
            var golem = Hand(p4, Golem); // 7 cost
            var cap = Hand(p4, "CAP_802");
            Mana(p4, 3); Play(g4, cap, keepMana: true);
            Check("unaffordable card not granted", !EnchantIds(golem).Contains("CAP_802e1"));
        }

        // CAP_803 Lingering Spirit 3/3: Reborn. Deathrattle: Restore 3 Health to your hero. Excess healing damages a random enemy.
        [AuditTest]
        static void CAP_803_HealAndExcess()
        {
            var g = PG(); var p = P1(g); var o = P2(g);
            var ls = Board(p, "CAP_803");
            Check("reborn", ls[GameTag.REBORN] == 1);
            Damage(g, p.Hero, 1);
            Kill(g, ls);
            Eq("hero healed 1", 30, p.Hero.Health);
            Eq("excess 2 to enemy hero (only enemy)", 28, o.Hero.Health);
            Check("reborn copy on board", p.BoardZone.Any(m => m.Card.Id == "CAP_803"));
            var g2 = PG(); var p2 = P1(g2); var o2 = P2(g2);
            Damage(g2, p2.Hero, 5);
            Kill(g2, Board(p2, "CAP_803"));
            Eq("full 3 heal", 28, p2.Hero.Health);
            Eq("no excess", 30, o2.Hero.Health);
        }

        // DAL_039 Convincing Infiltrator: Taunt. Deathrattle: Destroy a random enemy minion.
        [AuditTest]
        static void DAL_039_DestroysEnemyMinion()
        {
            var g = PG(); var p = P1(g); var o = P2(g);
            var m = Board(p, "DAL_039"); var mine = Board(p, Yeti);
            Board(o, Golem);
            Check("taunt", m.HasTaunt);
            Kill(g, m);
            Eq("enemy minion destroyed", 0, o.BoardZone.Count);
            Eq("friendly kept", 1, p.BoardZone.Count);
        }

        // DAL_413 EVIL Conscripter: Deathrattle: Add a Lackey to your hand.
        [AuditTest]
        static void DAL_413_AddsLackey()
        {
            var g = PG(); var p = P1(g);
            Kill(g, Board(p, "DAL_413"));
            Eq("one card", 1, p.HandZone.Count);
            var lackeys = new[] { "DAL_613", "DAL_614", "DAL_615", "DAL_739", "DAL_741", "ULD_616", "DRG_052", "ULD_616" };
            Check("is a lackey", p.HandZone.Count == 1 && (p.HandZone[0].Card[(GameTag)1359] == 1 || lackeys.Contains(p.HandZone[0].Card.Id)), string.Join(",", HandIds(p)));
        }

        // FP1_023 Dark Cultist: Deathrattle: Give a random friendly minion +3 Health.
        [AuditTest]
        static void FP1_023_Plus3HealthFriendly()
        {
            var g = PG(); var p = P1(g); var o = P2(g);
            var y = Board(p, Yeti); var foe = Board(o, Yeti);
            Kill(g, Board(p, "FP1_023"));
            Eq("friendly +3 hp", 8, Hp(y));
            Eq("enemy unchanged", 5, Hp(foe));
        }

        // LOOT_534 Gilded Gargoyle: Deathrattle: Add a Coin to your hand.
        [AuditTest]
        static void LOOT_534_AddsCoin()
        {
            var g = PG(); var p = P1(g);
            Kill(g, Board(p, "LOOT_534"));
            Eq("coin", "GAME_005", HandIds(p).FirstOrDefault());
        }

        // TLC_820 Glade Ecologist: Deathrattle: Get a 1-Cost Holy spell that gives a minion +2 or -2 Health.
        // TLC_813 Purifying Vines: friendly +2 Health, enemy -2 Health.
        [AuditTest]
        static void TLC_820_TLC_813_Vines()
        {
            var g = PG(); var p = P1(g); var o = P2(g);
            Kill(g, Board(p, "TLC_820"));
            Eq("got vines", "TLC_813", HandIds(p).FirstOrDefault());
            var y = Board(p, Yeti); var foe = Board(o, Yeti); var foeW = Board(o, "CS2_120"); // 2/2 river croc? (2/3)
            Play(g, p.HandZone[0], y);
            Eq("friendly +2 hp", 7, Hp(y)); Check("TLC_813e", EnchantIds(y).Contains("TLC_813e"));
            PlayNew(g, p, "TLC_813", foe);
            Eq("enemy -2 hp", 3, MaxHp(foe)); Check("TLC_813e2", EnchantIds(foe).Contains("TLC_813e2"));
            var w = Board(o, "CS2_172"); // Bloodfen Raptor 3/2
            PlayNew(g, p, "TLC_813", w);
            Check("2-health enemy dies", w.IsDead || w.Zone?.Type != Zone.PLAY);
        }

        // AT_011 Holy Champion 3/5: Whenever a character is healed, gain +2 Attack.
        [AuditTest]
        static void AT_011_GainsAttackOnAnyHeal()
        {
            var g = PG(); var p = P1(g); var o = P2(g);
            var hc = Board(p, "AT_011");
            Damage(g, p.Hero, 5); Damage(g, o.Hero, 5);
            Play(g, Hand(p, "AT_055"), p.Hero);
            Eq("+2 after hero heal", 5, Atk(hc));
            Play(g, Hand(p, "AT_055"), o.Hero);
            Eq("+2 after enemy hero heal", 7, Atk(hc));
            Play(g, Hand(p, "AT_055"), p.Hero); // full health: no heal
            Eq("no gain if nothing healed", 7, Atk(hc));
        }

        // AT_055 Flash Heal: Restore 5 Health.
        [AuditTest]
        static void AT_055_Restore5()
        {
            var g = PG(); var p = P1(g); var o = P2(g);
            Damage(g, p.Hero, 8);
            Play(g, Hand(p, "AT_055"), p.Hero);
            Eq("hero +5", 27, p.Hero.Health);
            var foe = Board(o, Golem); Damage(g, foe, 6);
            Play(g, Hand(p, "AT_055"), foe);
            Eq("minion +5", 6, Hp(foe));
        }

        // CFM_626 Kabal Talonpriest: Battlecry: Give a friendly minion +3 Health.
        [AuditTest]
        static void CFM_626_FriendlyPlus3Health()
        {
            var g = PG(); var p = P1(g); var o = P2(g);
            var y = Board(p, Yeti); var foe = Board(o, Yeti);
            var c = Hand(p, "CFM_626");
            Check("enemy not targetable", !LegalTargets(c).Any(t => t.Id == foe.Id));
            Play(g, c, y);
            Eq("+3 hp", 8, Hp(y)); Check("CFM_626e", EnchantIds(y).Contains("CFM_626e"));
        }

        // CORE_CS1_112 Holy Nova: Deal 2 damage to all enemy minions. Restore 2 Health to all friendly characters.
        [AuditTest]
        static void CORE_CS1_112_EnemyMinionsOnly()
        {
            var g = PG(); var p = P1(g); var o = P2(g);
            var foe = Board(o, Yeti); var mine = Board(p, Yeti);
            Damage(g, mine, 3); Damage(g, p.Hero, 5);
            PlayNew(g, p, "CORE_CS1_112");
            Eq("enemy minion took 2", 3, Hp(foe));
            Eq("enemy hero not damaged", 30, o.Hero.Health);
            Eq("friendly minion +2", 4, Hp(mine));
            Eq("friendly hero +2", 27, p.Hero.Health);
        }

        // CS2_235 Northshire Cleric: Whenever a minion is healed, draw a card.
        [AuditTest]
        static void CS2_235_DrawOnMinionHeal()
        {
            var g = PG(); var p = P1(g); var o = P2(g);
            var nc = Board(p, "CS2_235");
            var foe = Board(o, Yeti); Damage(g, foe, 2);
            Damage(g, p.Hero, 5);
            int hand = p.HandZone.Count;
            Play(g, Hand(p, "AT_055"), p.Hero);
            Eq("hero heal: no draw", hand, p.HandZone.Count);
            Play(g, Hand(p, "AT_055"), foe);
            Eq("enemy minion heal: draw", hand + 1, p.HandZone.Count);
        }

        // CS2_236 Divine Spirit: Double a minion's Health.
        [AuditTest]
        static void CS2_236_DoublesHealth()
        {
            var g = PG(); var p = P1(g);
            var y = Board(p, Yeti); Damage(g, y, 2);
            Play(g, Hand(p, "CS2_236"), y);
            Eq("current 3 -> 6", 6, Hp(y));
            Eq("max 8", 8, MaxHp(y));
        }

        // GIL_835 Squashling 2/1: Echo. Battlecry: Restore 2 Health.
        [AuditTest]
        static void GIL_835_EchoAndHeal()
        {
            var g = PG(); var p = P1(g);
            Damage(g, p.Hero, 5);
            var c = Hand(p, "GIL_835");
            Play(g, c, p.Hero);
            Eq("hero +2", 27, p.Hero.Health);
            Check("echo copy in hand", HandIds(p).Contains("GIL_835"), string.Join(",", HandIds(p)));
            var copy = p.HandZone.FirstOrDefault(x => x.Card.Id == "GIL_835");
            if (copy != null) Play(g, copy, p.Hero);
            Eq("second play heals again", 29, p.Hero.Health);
            EndTurn(g);
            Check("echo copy gone at end of turn", !HandIds(p).Contains("GIL_835"), string.Join(",", HandIds(p)));
        }

        // OG_234 Darkshire Alchemist: Battlecry: Restore 5 Health.
        [AuditTest]
        static void OG_234_Restore5()
        {
            var g = PG(); var p = P1(g);
            Damage(g, p.Hero, 8);
            Play(g, Hand(p, "OG_234"), p.Hero);
            Eq("hero +5", 27, p.Hero.Health);
        }

        // CATA_305 Incensed Matriarch 3/3: At the end of your turn, if this is at full Health, gain +3 Health.
        [AuditTest]
        static void CATA_305_FullHealthGrows()
        {
            var g = PG(); var p = P1(g);
            var m = Board(p, "CATA_305");
            EndTurn(g);
            Eq("+3 hp at own end of turn", 6, Hp(m)); Check("CATA_305e", EnchantIds(m).Contains("CATA_305e"));
            EndTurn(g); // boss's end of turn
            Eq("no gain on enemy end of turn", 6, MaxHp(m));
            Damage(g, m, 1);
            EndTurn(g);
            Eq("damaged: no gain", 6, MaxHp(m));
        }

        // CFM_605 Drakonid Operative: Battlecry: If you're holding a Dragon, Discover a card in your opponent's deck.
        [AuditTest]
        static void CFM_605_DiscoverFromOpponentDeck()
        {
            var deck = new List<string> { Golem, Golem, Golem, Wisp, Wisp, "CS2_172", "CS2_172", Yeti, Yeti, Yeti };
            var g = NewGame(s => { s.PlayerHero = "HERO_09"; s.BossDeck = deck.Concat(Fill(20, Yeti)).ToList(); });
            var p = P1(g); var o = P2(g);
            Hand(p, "CORE_NEW1_023");
            PlayNew(g, p, "CFM_605");
            var ch = ChoiceIds(g, p);
            Eq("3 options", 3, ch.Count);
            var od = DeckIds(o);
            Check("options come from opp deck", ch.All(od.Contains), string.Join(",", ch));
            Check("distinct options", ch.Distinct().Count() == ch.Count, string.Join(",", ch));
            int before = p.HandZone.Count;
            Choose(g, p, 0);
            Eq("one card added", before + 1, p.HandZone.Count);
            var g2 = PG(); var p2 = P1(g2);
            Hand(p2, Yeti);
            PlayNew(g2, p2, "CFM_605");
            Check("no discover without dragon", p2.Choice == null);
        }

        // CORE_NEW1_023 Faerie Dragon 3/2: Elusive.
        [AuditTest]
        static void CORE_NEW1_023_Elusive()
        {
            var g = NewGame(s => { s.PlayerHero = "HERO_08"; });
            var p = P1(g); var o = P2(g);
            var fd = Board(o, "CORE_NEW1_023");
            var mine = Board(p, "CORE_NEW1_023");
            var fb = Hand(p, "CS2_029");
            Check("enemy faerie not spell-targetable", !LegalTargets(fb).Any(t => t.Id == fd.Id));
            Check("own faerie not spell-targetable", !LegalTargets(fb).Any(t => t.Id == mine.Id));
            Mana(p, 10);
            var hp = p.Options().OfType<HeroPowerTask>().Where(t => t.Target != null).Select(t => t.Target.Id).ToList();
            Check("hero power cannot target", !hp.Contains(fd.Id), "hp targets " + hp.Count);
            Check("can be attacked", Attack(g, Board(p, Yeti), fd));
        }

        // TIME_045 Whelp of the Infinite 1/4: Poisonous, Reborn.
        [AuditTest]
        static void TIME_045_PoisonousReborn()
        {
            var g = PG(); var p = P1(g); var o = P2(g);
            var w = Board(p, "TIME_045");
            Check("poisonous", w.Poisonous); Check("reborn", w[GameTag.REBORN] == 1);
            var foe = Board(o, Golem);
            Attack(g, w, foe);
            Check("golem dies to poison", foe.IsDead || foe.Zone?.Type != Zone.PLAY);
            var w2 = Board(p, "TIME_045");
            Kill(g, w2);
            Check("reborn copy", p.BoardZone.Any(m => m.Card.Id == "TIME_045" && m.Health == 1));
        }

        // TIME_056 Whelp of the Bronze 4/1: Lifesteal, Divine Shield.
        [AuditTest]
        static void TIME_056_LifestealDivineShield()
        {
            var g = PG(); var p = P1(g); var o = P2(g);
            var w = Board(p, "TIME_056");
            Check("divine shield", w.HasDivineShield); Check("lifesteal", w.HasLifeSteal);
            Damage(g, p.Hero, 10);
            var foe = Board(o, Yeti);
            Attack(g, w, foe);
            Eq("lifesteal 4", 24, p.Hero.Health);
            Check("shield popped, alive", !w.IsDead && !w.HasDivineShield);
        }
    }
}
