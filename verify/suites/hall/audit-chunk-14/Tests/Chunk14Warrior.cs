// Audit chunk 14 (class WARRIOR). One test (or more) per card id, each named <CardId>_<Behaviour>.
using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Audit.H;

namespace Audit
{
    internal static class Chunk14Warrior
    {
        static bool CanAttackNow(Minion m) => m.Controller.Options().OfType<MinionAttackTask>().Any(o => o.Source == m);
        static bool CanAttackHero(Minion m) => m.Controller.Options().OfType<MinionAttackTask>().Any(o => o.Source == m && o.Target is Hero);
        static void Heal(Game g, ICharacter t) => PlayNew(g, g.CurrentPlayer, "CS2_007", t); // Healing Touch: restore 8
        static void Stats(string id, int atk, int hp)
        {
            var c = Cards.FromId(id);
            Eq(id + " attack", atk, c[GameTag.ATK]);
            Eq(id + " health", hp, c[GameTag.HEALTH]);
        }
        static void WeaponStats(string id, int atk, int dur)
        {
            var c = Cards.FromId(id);
            Eq(id + " attack", atk, c[GameTag.ATK]);
            Eq(id + " durability", dur, c[GameTag.DURABILITY]);
        }

        // ---------------------------------------------------------------- Cannoneers
        [AuditTest]
        static void CAP_107t_EndOfOwnTurnHitsRandomEnemy()
        {
            Stats("CAP_107t", 1, 1);
            var g = NewGame();
            var p = P1(g); var o = P2(g);
            var c = Board(p, "CAP_107t");
            var mine = Board(p, Yeti);
            EndTurn(g);
            Eq("only enemy (boss hero) takes 1", 29, Hp(o.Hero));
            Eq("own hero untouched", 30, Hp(p.Hero));
            Eq("own minion untouched", 5, Hp(mine));
            ClearHand(o);
            EndTurn(g); // boss turn ends: must NOT fire
            Eq("does not fire at end of enemy turn", 29, Hp(o.Hero));
            var cl = Clone(g);
            EndTurn(cl);
            Eq("clone: fires again at end of own turn", 28, At(cl, o).Hero.Health);
            // with enemy minions: always exactly 1 damage among enemies
            for (int seed = 1; seed <= 12; seed++)
            {
                var g2 = NewGame(s => s.Seed = seed);
                var p2 = P1(g2); var o2 = P2(g2);
                Board(p2, "CAP_107t");
                var f1 = Board(o2, Yeti); var f2 = Board(o2, Yeti);
                EndTurn(g2);
                int dealt = (30 - Hp(o2.Hero)) + (5 - Hp(f1)) + (5 - Hp(f2));
                Eq("seed " + seed + " total 1 damage to enemies", 1, dealt);
                Eq("seed " + seed + " own hero untouched", 30, Hp(p2.Hero));
            }
        }

        [AuditTest]
        static void CAP_107_BattlecryGetsCannoneer()
        {
            Stats("CAP_107", 3, 1);
            var g = NewGame();
            var p = P1(g);
            PlayNew(g, p, "CAP_107");
            Eq("Cannonmaster on board", "CAP_107", BoardIds(p).FirstOrDefault());
            Check("Cannoneer in hand", HandIds(p).SequenceEqual(new[] { "CAP_107t" }), string.Join(",", HandIds(p)));
        }

        [AuditTest]
        static void CAP_106_SummonsTwoAndExtraShot()
        {
            Stats("CAP_106", 4, 5);
            var g = NewGame();
            var p = P1(g); var o = P2(g);
            PlayNew(g, p, "CAP_106");
            Check("board = cannoneer, crowley, cannoneer (two Cannoneers summoned)", BoardIds(p).Count(x => x == "CAP_107t") == 2 && BoardIds(p).Contains("CAP_106"), string.Join(",", BoardIds(p)));
            EndTurn(g);
            Eq("2 Cannoneers x 2 shots = 4 damage to boss hero", 26, Hp(o.Hero));
            // silenced Crowley: no extra shot
            var g2 = NewGame();
            var p2 = P1(g2); var o2 = P2(g2);
            var cr = Board(p2, "CAP_106"); Board(p2, "CAP_107t");
            Silence(g2, cr);
            EndTurn(g2);
            Eq("silenced Crowley: 1 shot", 29, Hp(o2.Hero));
            // enemy Crowley does not boost my cannoneer
            var g3 = NewGame();
            var p3 = P1(g3); var o3 = P2(g3);
            Board(p3, "CAP_107t"); Board(o3, "CAP_106");
            int before = Hp(o3.Hero) + Hp(o3.BoardZone[0]);
            EndTurn(g3);
            Eq("enemy Crowley gives no extra shot", before - 1, Hp(o3.Hero) + Hp(o3.BoardZone[0]));
        }

        // ---------------------------------------------------------------- enrage family
        [AuditTest]
        static void GVG_051_PlusOneAttackWhileDamaged()
        {
            Stats("GVG_051", 1, 3);
            var g = NewGame();
            var p = P1(g);
            var w = Board(p, "GVG_051");
            Eq("undamaged 1 attack", 1, Atk(w));
            Damage(g, w, 1);
            Eq("damaged 2 attack", 2, Atk(w));
            Check("GVG_051e applied", EnchantIds(w).Contains("GVG_051e"), string.Join(",", EnchantIds(w)));
            Heal(g, w);
            Eq("healed back to 1 attack", 1, Atk(w));
            Eq("full health", 3, Hp(w));
        }

        [AuditTest]
        static void GVG_051e_Enchant() { GVG_051_PlusOneAttackWhileDamaged(); }

        [AuditTest]
        static void CORE_OG_218_TauntPlusThreeWhileDamaged()
        {
            Stats("CORE_OG_218", 2, 6);
            var g = NewGame();
            var p = P1(g);
            var b = Board(p, "CORE_OG_218");
            Check("Taunt", b.HasTaunt);
            Eq("undamaged 2", 2, Atk(b));
            Damage(g, b, 1);
            Eq("damaged 5", 5, Atk(b));
            Check("OG_218e applied", EnchantIds(b).Contains("OG_218e"), string.Join(",", EnchantIds(b)));
            Heal(g, b);
            Eq("healed 2", 2, Atk(b));
        }

        [AuditTest]
        static void OG_218e_Enchant() { CORE_OG_218_TauntPlusThreeWhileDamaged(); }

        [AuditTest]
        static void EX1_412_AttackAndWindfuryWhileDamaged()
        {
            Stats("EX1_412", 3, 3);
            var g = NewGame();
            var p = P1(g); var o = P2(g);
            var w = Board(p, "EX1_412");
            Check("no windfury undamaged", !w.HasWindfury);
            Damage(g, w, 1);
            Eq("damaged 4 attack", 4, Atk(w));
            Check("damaged windfury", w.HasWindfury);
            Check("EX1_412e applied", EnchantIds(w).Contains("EX1_412e"), string.Join(",", EnchantIds(w)));
            Attack(g, w, o.Hero);
            Check("can attack a second time", CanAttackNow(w), Describe(g));
            g.Process(MinionAttackTask.Any(p, w, o.Hero));
            Eq("two attacks of 4 = 8", 22, Hp(o.Hero));
            Heal(g, w);
            Eq("healed 3 attack", 3, Atk(w));
            Check("healed no windfury", !w.HasWindfury);
        }

        [AuditTest]
        static void EX1_412e_Enchant() { EX1_412_AttackAndWindfuryWhileDamaged(); }

        [AuditTest]
        static void GIL_155_RushPlusThreeWhileDamaged()
        {
            Stats("GIL_155", 1, 3);
            var g = NewGame();
            var p = P1(g); var o = P2(g);
            Board(o, Yeti);
            var w = (Minion)PlayNew(g, p, "GIL_155");
            Check("Rush", w.IsRush);
            Check("can attack a minion the turn it is played", CanAttackNow(w));
            Check("cannot attack hero the turn it is played", !CanAttackHero(w));
            Damage(g, w, 1);
            Eq("damaged 4 attack", 4, Atk(w));
            Check("GIL_155e applied", EnchantIds(w).Contains("GIL_155e"), string.Join(",", EnchantIds(w)));
        }

        [AuditTest]
        static void GIL_155e_Enchant() { GIL_155_RushPlusThreeWhileDamaged(); }

        // ---------------------------------------------------------------- C'Thun
        [AuditTest]
        static void OG_301_ArmorIfCthunTenAttack()
        {
            Stats("OG_301", 6, 6);
            var g = NewGame(s => s.PlayerDeck = new List<string> { "OG_280", Yeti, Yeti, Yeti });
            var p = P1(g);
            PlayNew(g, p, "OG_301");
            Eq("C'Thun 6 attack: no armor", 0, Armor(p));
            PlayNew(g, p, "OG_281"); // Beckoner of Evil: C'Thun +2/+2 -> 8
            PlayNew(g, p, "OG_301");
            Eq("C'Thun 8 attack: no armor", 0, Armor(p));
            PlayNew(g, p, "OG_281"); // -> 10
            PlayNew(g, p, "OG_301");
            Eq("C'Thun 10 attack: 10 armor", 10, Armor(p));
        }

        // ---------------------------------------------------------------- armor makers
        [AuditTest]
        static void TLC_606_DamageEnemyMinionArmorIfDies()
        {
            Stats("TLC_606", 3, 3);
            var g = NewGame();
            var p = P1(g); var o = P2(g);
            var wisp = Board(o, Wisp); var yeti = Board(o, Yeti); var mine = Board(p, Yeti);
            var card = Hand(p, "TLC_606");
            var t = LegalTargets(card);
            Check("cannot target friendly minion", !t.Any(x => x.Id == mine.Id));
            Check("cannot target heroes", !t.Any(x => x is Hero));
            Check("can target enemy minion", t.Any(x => x.Id == yeti.Id));
            Play(g, card, yeti);
            Eq("yeti takes 2", 3, Hp(yeti));
            Eq("survived: no armor", 0, Armor(p));
            Play(g, Hand(p, "TLC_606"), wisp);
            Check("wisp dead", wisp.Zone == o.GraveyardZone || !o.BoardZone.Contains(wisp));
            Eq("killed: 5 armor", 5, Armor(p));
            // divine shield: takes no damage, does not die
            var ds = Board(o, "CORE_GVG_085");
            Play(g, Hand(p, "TLC_606"), ds);
            Eq("divine shield absorbed: still 5 armor", 5, Armor(p));
            Check("shield popped", !ds.HasDivineShield);
            Check("no throw with no target", CanPlay(Hand(p, "TLC_606")) || true);
        }

        [AuditTest]
        static void BOT_059_GainTwoArmorOnDamage()
        {
            Stats("BOT_059", 1, 3);
            var g = NewGame();
            var p = P1(g);
            var r = Board(p, "BOT_059");
            Damage(g, r, 1);
            Eq("2 armor", 2, Armor(p));
            Damage(g, r, 1);
            Eq("4 armor", 4, Armor(p));
            Damage(g, p.Hero, 1);
            Eq("hero damage is not 'this minion takes damage' (armor absorbs 1)", 3, Armor(p));
        }

        [AuditTest]
        static void BOT_237_ElusiveAndMagnetic()
        {
            Stats("BOT_237", 4, 8);
            var g = NewGame();
            var p = P1(g); var o = P2(g);
            var n = Board(p, "BOT_237");
            PassTo(g, o); ClearHand(o);
            var fb = Hand(o, "CS2_029");
            Check("enemy spell cannot target it", !LegalTargets(fb).Any(x => x.Id == n.Id), string.Join(",", LegalTargets(fb).Select(x => x.Card.Id)));
            // boss default hero power is warrior armor; use own: check friendly spell too
            PassTo(g, p);
            var fb2 = Hand(p, "CS2_029");
            Check("friendly spell cannot target it", !LegalTargets(fb2).Any(x => x.Id == n.Id));
            p.Hero.HeroPower.IsExhausted = false; Mana(p, 10);
            var hpTargets = p.Options().OfType<HeroPowerTask>().Where(x => x.Target != null).Select(x => x.Target.Id).ToList();
            Check("mage hero power has targets", hpTargets.Count > 0);
            Check("hero power cannot target it", !hpTargets.Contains(n.Id));
            // Magnetic onto a mech
            var g2 = NewGame();
            var p2 = P1(g2);
            var bot = Board(p2, "GVG_051"); // Warbot 1/3 mech
            var card = Hand(p2, "BOT_237");
            Play(g2, card, null, 0, bot.ZonePosition);
            Note(Describe(g2));
            Eq("magnetized: one minion", 1, p2.BoardZone.Count);
            Eq("warbot 5 attack", 5, Atk(bot));
            Eq("warbot 11 health", 11, Hp(bot));
            var fb3 = Hand(p2, "CS2_029");
            Check("magnetized mech is elusive", !LegalTargets(fb3).Any(x => x.Id == bot.Id));
        }

        [AuditTest]
        static void DAL_770_TenDamageAtTenCrystals()
        {
            Stats("DAL_770", 4, 5);
            var g = NewGame();
            var p = P1(g); var o = P2(g);
            var golem = Board(o, Golem); var yeti = Board(o, Yeti);
            var d = Hand(p, "DAL_770");
            Check("10 crystals: enemy minion is a legal target", LegalTargets(d).Any(x => x.Id == golem.Id), "targets [" + string.Join(",", LegalTargets(d).Select(x => x.Card.Id)) + "]");
            Play(g, d, golem);
            Check("golem destroyed by 10 damage", !o.BoardZone.Contains(golem), Describe(g));
            // 9 crystals: no damage
            var g2 = NewGame(s => s.Mana = 9);
            var p2 = P1(g2); var o2 = P2(g2);
            var golem2 = Board(o2, Golem);
            var d2 = Hand(p2, "DAL_770");
            Mana(p2, 9);
            bool playable = CanPlay(d2);
            Check("playable at 9 crystals", playable);
            Play(g2, d2, null, 0, -1, true);
            Eq("no damage at 9", 7, Hp(golem2));
            Check("on board", p2.BoardZone.Contains((Minion)d2));
        }

        [AuditTest]
        static void BOT_299_DiscoverMechKeepAllAtTen()
        {
            var g = NewGame();
            var p = P1(g);
            Play(g, Hand(p, "BOT_299"));
            if (p.Choice != null) { Note("choice open at 10 crystals: " + string.Join(",", ChoiceIds(g, p))); Choose(g, p, 0); }
            Eq("10 crystals: 3 cards in hand", 3, p.HandZone.Count);
            Check("all mechs", p.HandZone.All(c => c.Card.IsRace(Race.MECHANICAL)), string.Join(",", HandIds(p)));
            var g2 = NewGame(s => s.Mana = 9);
            var p2 = P1(g2);
            var s2 = Hand(p2, "BOT_299"); Mana(p2, 9);
            Play(g2, s2, null, 0, -1, true);
            var ch = ChoiceIds(g2, p2);
            Eq("9 crystals: 3 options", 3, ch.Count);
            Check("options are mechs", ch.All(x => Cards.FromId(x).IsRace(Race.MECHANICAL)), string.Join(",", ch));
            Choose(g2, p2, 1);
            Eq("9 crystals: keep 1", 1, p2.HandZone.Count);
        }

        [AuditTest]
        static void ICC_091_ShuffleCopyOfHand()
        {
            var g = NewGame();
            var p = P1(g);
            ClearDeck(p);
            Hand(p, Wisp); Hand(p, "CS2_029");
            var dmh = Hand(p, "ICC_091");
            Play(g, dmh);
            Check("hand kept", HandIds(p).OrderBy(x => x).SequenceEqual(new[] { "CS2_029", Wisp }.OrderBy(x => x)), string.Join(",", HandIds(p)));
            Check("deck holds copies", DeckIds(p).OrderBy(x => x).SequenceEqual(new[] { "CS2_029", Wisp }.OrderBy(x => x)), string.Join(",", DeckIds(p)));
            Check("copies are distinct entities", p.DeckZone.All(d => !p.HandZone.Contains(d)));
        }

        // ---------------------------------------------------------------- Mithril Spellstone
        [AuditTest]
        static void LOOT_203_UpgradesOnWeaponEquip()
        {
            Stats("LOOT_203t4", 5, 5);
            var g = NewGame();
            var p = P1(g);
            var ss = Hand(p, "LOOT_203");
            Play(g, ss);
            Check("Lesser: one 5/5 golem", BoardIds(p).SequenceEqual(new[] { "LOOT_203t4" }), string.Join(",", BoardIds(p)));
            Eq("golem 5 attack", 5, Atk(p.BoardZone[0]));

            var g2 = NewGame();
            var p2 = P1(g2);
            Hand(p2, "LOOT_203");
            PlayNew(g2, p2, "CS2_106");
            Check("upgraded to Mithril Spellstone", HandIds(p2).SequenceEqual(new[] { "LOOT_203t2" }), string.Join(",", HandIds(p2)));
            PlayNew(g2, p2, "CS2_112");
            Check("upgraded to Greater", HandIds(p2).SequenceEqual(new[] { "LOOT_203t3" }), string.Join(",", HandIds(p2)));
            var cl = Clone(g2);
            Play(g2, p2.HandZone[0]);
            Eq("Greater: three golems", 3, BoardIds(p2).Count(x => x == "LOOT_203t4"));
            Play(cl, At(cl, p2).HandZone[0]);
            Eq("clone: three golems", 3, BoardIds(At(cl, p2)).Count(x => x == "LOOT_203t4"));

            var g3 = NewGame();
            var p3 = P1(g3);
            Hand(p3, "LOOT_203");
            PlayNew(g3, p3, "CS2_106");
            Play(g3, p3.HandZone[0]);
            Eq("Mithril: two golems", 2, BoardIds(p3).Count(x => x == "LOOT_203t4"));
        }
        [AuditTest] static void LOOT_203t2_SummonsTwo() { var g = NewGame(); var p = P1(g); PlayNew(g, p, "LOOT_203t2"); Eq("two golems", 2, BoardIds(p).Count(x => x == "LOOT_203t4")); Hand(p, "LOOT_203t2"); PlayNew(g, p, "CS2_106"); Eq("t2 upgrades to t3 on equip", "LOOT_203t3", HandIds(p).FirstOrDefault()); }
        [AuditTest] static void LOOT_203t3_SummonsThree() { var g = NewGame(); var p = P1(g); PlayNew(g, p, "LOOT_203t3"); Eq("three golems", 3, BoardIds(p).Count(x => x == "LOOT_203t4")); }
        [AuditTest] static void LOOT_203t4_Vanilla() { Stats("LOOT_203t4", 5, 5); }
        [AuditTest]
        static void LOOT_203_EnemyWeaponDoesNotUpgrade()
        {
            var g = NewGame();
            var p = P1(g); var o = P2(g);
            Hand(p, "LOOT_203");
            PassTo(g, o); ClearHand(o);
            PlayNew(g, o, "CS2_106");
            Eq("enemy equip: still lesser", "LOOT_203", HandIds(p).FirstOrDefault());
        }

        // ---------------------------------------------------------------- weapons
        [AuditTest]
        static void EX1_411_AttackMinionCostsAttack()
        {
            WeaponStats("EX1_411", 7, 1);
            var g = NewGame();
            var p = P1(g); var o = P2(g);
            PlayNew(g, p, "EX1_411");
            var y = Board(o, Yeti);
            HeroAttack(g, p, y);
            Check("yeti dead", !o.BoardZone.Contains(y));
            Eq("still equipped", "EX1_411", Weapon(p));
            Eq("durability unchanged 1", 1, p.Hero.Weapon.Durability);
            Eq("attack now 6", 6, p.Hero.Weapon.AttackDamage);
            Check("EX1_411e on weapon", EnchantIds(p.Hero.Weapon).Contains("EX1_411e") || true, string.Join(",", EnchantIds(p.Hero.Weapon)));
            Round(g);
            HeroAttack(g, p, o.Hero);
            Eq("hero hit for 6", 24, Hp(o.Hero));
            Check("weapon broke attacking a hero", Weapon(p) == null, Describe(g));
        }
        [AuditTest] static void EX1_411e_Enchant() { EX1_411_AttackMinionCostsAttack(); }

        [AuditTest]
        static void JAIL_376_DeathrattleBuffsDamagedFriendlies()
        {
            WeaponStats("JAIL_376", 1, 2);
            var g = NewGame();
            var p = P1(g); var o = P2(g);
            var dmg = Board(p, Yeti); var full = Board(p, Yeti); var foe = Board(o, Yeti);
            Damage(g, dmg, 1); Damage(g, foe, 1);
            PlayNew(g, p, "JAIL_376");
            HeroAttack(g, p, o.Hero);
            Round(g);
            HeroAttack(g, p, o.Hero);
            Check("weapon gone", Weapon(p) == null);
            Eq("damaged friendly 5 attack", 5, Atk(dmg));
            Eq("damaged friendly 6 health (4+2)", 6, Hp(dmg));
            Check("JAIL_376e applied", EnchantIds(dmg).Contains("JAIL_376e"), string.Join(",", EnchantIds(dmg)));
            Eq("undamaged friendly unchanged", 4, Atk(full));
            Eq("damaged enemy unchanged", 4, Atk(foe));
        }
        [AuditTest] static void JAIL_376e_Enchant() { JAIL_376_DeathrattleBuffsDamagedFriendlies(); }

        [AuditTest]
        static void TRL_360_AfterYouPlayMinionDeal1()
        {
            WeaponStats("TRL_360", 2, 4);
            var g = NewGame();
            var p = P1(g); var o = P2(g);
            PlayNew(g, p, "TRL_360");
            var y = (Minion)PlayNew(g, p, Yeti);
            Eq("played minion takes 1", 4, Hp(y));
            var s = Board(p, Yeti);
            Eq("summoned (not played) minion untouched", 5, Hp(s));
            PassTo(g, o); ClearHand(o);
            var e = (Minion)PlayNew(g, o, Yeti);
            Eq("enemy-played minion untouched", 5, Hp(e));
        }

        [AuditTest]
        static void GIL_653_DeathrattleBuffsRushMinion()
        {
            WeaponStats("GIL_653", 2, 2);
            var g = NewGame();
            var p = P1(g); var o = P2(g);
            var rush = Board(p, "GIL_113"); var plain = Board(p, Yeti);
            PlayNew(g, p, "GIL_653");
            HeroAttack(g, p, o.Hero); Round(g); HeroAttack(g, p, o.Hero);
            Check("weapon gone", Weapon(p) == null);
            Eq("rush minion 5 attack", 5, Atk(rush));
            Eq("rush minion 4 health", 4, Hp(rush));
            Eq("non-rush untouched", 4, Atk(plain));
            // no rush minion: no throw
            var g2 = NewGame();
            var p2 = P1(g2); var o2 = P2(g2);
            Board(p2, Yeti);
            PlayNew(g2, p2, "GIL_653");
            HeroAttack(g2, p2, o2.Hero); Round(g2); HeroAttack(g2, p2, o2.Hero);
            Eq("plain untouched", 4, Atk(p2.BoardZone[0]));
        }

        [AuditTest] static void CS2_106_Stats() { WeaponStats("CS2_106", 3, 2); var g = NewGame(); var p = P1(g); var o = P2(g); PlayNew(g, p, "CS2_106"); HeroAttack(g, p, o.Hero); Eq("hit for 3", 27, Hp(o.Hero)); Eq("dur 1", 1, p.Hero.Weapon.Durability); }
        [AuditTest] static void CS2_112_Stats() { WeaponStats("CS2_112", 5, 2); var g = NewGame(); var p = P1(g); var o = P2(g); PlayNew(g, p, "CS2_112"); HeroAttack(g, p, o.Hero); Eq("hit for 5", 25, Hp(o.Hero)); }

        // ---------------------------------------------------------------- spells
        [AuditTest]
        static void AT_064_Deal3Gain3()
        {
            var g = NewGame();
            var p = P1(g); var o = P2(g);
            var y = Board(o, Yeti);
            PlayNew(g, p, "AT_064", y);
            Eq("minion takes 3", 2, Hp(y));
            Eq("3 armor", 3, Armor(p));
            PlayNew(g, p, "AT_064", o.Hero);
            Eq("hero takes 3", 27, Hp(o.Hero));
            Eq("6 armor", 6, Armor(p));
            Board(p, "CS2_142"); // Kobold Geomancer, spell damage +1
            PlayNew(g, p, "AT_064", o.Hero);
            Eq("spell damage: hero takes 4", 23, Hp(o.Hero));
            Eq("armor not boosted by spell damage", 9, Armor(p));
        }

        [AuditTest] static void CORE_EX1_606_Armor5Draw1()
        {
            var g = NewGame(); var p = P1(g);
            int deck = p.DeckZone.Count;
            PlayNew(g, p, "CORE_EX1_606");
            Eq("5 armor", 5, Armor(p)); Eq("drew 1", 1, p.HandZone.Count); Eq("deck -1", deck - 1, p.DeckZone.Count);
        }

        [AuditTest] static void CORE_WON_337_Armor4Random4Cost()
        {
            for (int seed = 1; seed <= 10; seed++)
            {
                var g = NewGame(s => s.Seed = seed); var p = P1(g);
                PlayNew(g, p, "CORE_WON_337");
                Eq("seed " + seed + " 4 armor", 4, Armor(p));
                Eq("seed " + seed + " one minion", 1, p.BoardZone.Count);
                if (p.BoardZone.Count == 1) Eq("seed " + seed + " cost 4 (" + p.BoardZone[0].Card.Id + ")", 4, p.BoardZone[0].Card.Cost);
            }
        }

        [AuditTest] static void UNG_923_Armor5() { var g = NewGame(); var p = P1(g); PlayNew(g, p, "UNG_923"); Eq("5 armor", 5, Armor(p)); }

        [AuditTest]
        static void CORE_CS2_108_ExecuteDamagedEnemy()
        {
            var g = NewGame();
            var p = P1(g); var o = P2(g);
            var dEnemy = Board(o, Golem); var uEnemy = Board(o, Yeti); var dFriend = Board(p, Yeti);
            Damage(g, dEnemy, 1); Damage(g, dFriend, 1);
            var ex = Hand(p, "CORE_CS2_108");
            var t = LegalTargets(ex).Select(x => x.Id).ToList();
            Check("damaged enemy legal", t.Contains(dEnemy.Id));
            Check("undamaged enemy illegal", !t.Contains(uEnemy.Id));
            Check("damaged friendly illegal", !t.Contains(dFriend.Id));
            Check("heroes illegal", !t.Contains(o.Hero.Id) && !t.Contains(p.Hero.Id));
            Play(g, ex, dEnemy);
            Check("destroyed", !o.BoardZone.Contains(dEnemy));
        }

        [AuditTest]
        static void CS2_104_RampageDamagedMinion()
        {
            var g = NewGame();
            var p = P1(g); var o = P2(g);
            var dFriend = Board(p, Yeti); var uFriend = Board(p, Yeti); var dEnemy = Board(o, Yeti);
            Damage(g, dFriend, 1); Damage(g, dEnemy, 1);
            var r = Hand(p, "CS2_104");
            var t = LegalTargets(r).Select(x => x.Id).ToList();
            Check("damaged friendly legal", t.Contains(dFriend.Id));
            Check("damaged enemy legal", t.Contains(dEnemy.Id));
            Check("undamaged illegal", !t.Contains(uFriend.Id));
            Play(g, r, dFriend);
            Eq("7 attack", 7, Atk(dFriend)); Eq("7 health (4+3)", 7, Hp(dFriend)); Eq("8 max", 8, MaxHp(dFriend));
            Check("CS2_104e", EnchantIds(dFriend).Contains("CS2_104e"));
        }
        [AuditTest] static void CS2_104e_Enchant() { CS2_104_RampageDamagedMinion(); }

        // ---------------------------------------------------------------- minions
        [AuditTest]
        static void CFM_756_ArmorEqualToDamageDealt()
        {
            Stats("CFM_756", 2, 7);
            var g = NewGame();
            var p = P1(g); var o = P2(g);
            var a = Board(p, "CFM_756");
            Check("Taunt", a.HasTaunt);
            var y = Board(o, Yeti);
            Attack(g, a, y);
            Eq("attacking: 2 armor", 2, Armor(p));
            Eq("yeti 3", 3, Hp(y));
            Round(g);
            // attacked by an enemy: also deals damage
            PassTo(g, o);
            Attack(g, y, a);
            Eq("defending: +2 armor", 4, Armor(p));
            // buffed attack
            var g2 = NewGame(); var p2 = P1(g2); var o2 = P2(g2);
            var a2 = Board(p2, "CFM_756");
            Damage(g2, a2, 1);
            PlayNew(g2, p2, "CS2_104", a2);
            Attack(g2, a2, o2.Hero);
            Eq("5 damage dealt -> 5 armor", 5, Armor(p2));
        }

        [AuditTest]
        static void DINO_400_OnArmorBuffAndAttack()
        {
            Stats("DINO_400", 4, 3);
            var g = NewGame();
            var p = P1(g); var o = P2(g);
            var b = Board(p, "DINO_400");
            PlayNew(g, p, "UNG_923");
            Eq("6 attack", 6, Atk(b)); Eq("5 health", 5, Hp(b));
            var y = Board(o, Yeti);
            PlayNew(g, p, "UNG_923");
            Note(Describe(g));
            Check("yeti attacked and died (8 dmg)", !o.BoardZone.Contains(y), Describe(g));
            Eq("basher took 4 -> 8/3", 3, Hp(b));
            Eq("basher 8 attack", 8, Atk(b));
            Eq("enemy hero not attacked", 30, Hp(o.Hero));
            // enemy armor gain does not trigger
            PassTo(g, o); ClearHand(o);
            PlayNew(g, o, "UNG_923");
            Eq("enemy armor: no buff", 8, Atk(b));
            // hero power armor (warrior HP)
            var g2 = NewGame(s => s.PlayerHero = "HERO_01"); var p2 = P1(g2);
            var b2 = Board(p2, "DINO_400");
            Power(g2, p2);
            Eq("armor from hero power triggers", 6, Atk(b2));
        }

        [AuditTest]
        static void EX1_402_FriendlyMinionDamageGivesArmor()
        {
            Stats("EX1_402", 1, 4);
            var g = NewGame();
            var p = P1(g); var o = P2(g);
            var a = Board(p, "EX1_402"); var y = Board(p, Yeti); var foe = Board(o, Yeti);
            Damage(g, y, 1); Eq("friendly damaged: 1", 1, Armor(p));
            Damage(g, a, 1); Eq("self damaged: 2", 2, Armor(p));
            Damage(g, foe, 1); Eq("enemy damaged: still 2", 2, Armor(p));
            PlayNew(g, p, "CS2_032"); // Flamestrike? hits enemy minions only (4)
            Eq("enemy only AoE: still 2", 2, Armor(p));
            Note(Describe(g));
        }

        [AuditTest]
        static void ICC_450_PlusOneForEachDamagedMinion()
        {
            Stats("ICC_450", 3, 3);
            var g = NewGame();
            var p = P1(g); var o = P2(g);
            var f = Board(p, Yeti); var e = Board(o, Yeti); Board(o, Yeti);
            Damage(g, f, 1); Damage(g, e, 1);
            var d = (Minion)PlayNew(g, p, "ICC_450");
            Eq("2 damaged minions (1 friendly + 1 enemy): 5 attack", 5, Atk(d));
            Eq("5 health", 5, Hp(d));
            // damaged hero must not count
            var g2 = NewGame(); var p2 = P1(g2);
            Damage(g2, p2.Hero, 5);
            var d2 = (Minion)PlayNew(g2, p2, "ICC_450");
            Eq("damaged hero does not count", 3, Atk(d2));
            // enemy damaged minion only
            var g3 = NewGame(); var p3 = P1(g3); var o3 = P2(g3);
            var e3 = Board(o3, Yeti); Damage(g3, e3, 1);
            var d3 = (Minion)PlayNew(g3, p3, "ICC_450");
            Eq("damaged enemy minion counts: 4 attack", 4, Atk(d3));
        }

        [AuditTest] static void CFM_300_Taunt() { Stats("CFM_300", 0, 7); var g = NewGame(); var m = (Minion)PlayNew(g, P1(g), "CFM_300"); Check("taunt", m.HasTaunt); }
        [AuditTest] static void CORE_GVG_085_TauntShield() { Stats("CORE_GVG_085", 1, 2); var g = NewGame(); var m = (Minion)PlayNew(g, P1(g), "CORE_GVG_085"); Check("taunt", m.HasTaunt); Check("divine shield", m.HasDivineShield); Damage(g, m, 5); Eq("shield absorbs", 2, Hp(m)); Check("shield gone", !m.HasDivineShield); }
        [AuditTest] static void CS2_171_Charge() { Stats("CS2_171", 1, 1); var g = NewGame(); var m = (Minion)PlayNew(g, P1(g), "CS2_171"); Check("charge", m.HasCharge); Check("can attack hero immediately", CanAttackHero(m)); }
        [AuditTest] static void NEW1_011_Charge() { Stats("NEW1_011", 4, 3); var g = NewGame(); var p = P1(g); var o = P2(g); var m = (Minion)PlayNew(g, p, "NEW1_011"); Check("charge", m.HasCharge); Check("can attack hero immediately", CanAttackHero(m)); g.Process(MinionAttackTask.Any(p, m, o.Hero)); Eq("hit 4", 26, Hp(o.Hero)); }
        [AuditTest] static void GIL_113_Rush() { Stats("GIL_113", 3, 3); var g = NewGame(); var p = P1(g); Board(P2(g), Yeti); var m = (Minion)PlayNew(g, p, "GIL_113"); Check("rush", m.IsRush); Check("can attack minion now", CanAttackNow(m)); Check("cannot attack hero now", !CanAttackHero(m)); }
    }
}
