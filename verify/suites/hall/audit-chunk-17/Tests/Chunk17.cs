// Chunk 17: boss-clockwork_tyrant + boss-hollow_sovereign deck cards.
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
    internal static class Chunk17
    {
        const GameTag HeraldProgress = (GameTag)4327;
        const string Fireball = "CS2_029";
        const string Kobold = "CS2_142"; // Spell Damage +1
        const string Ysera = "EX1_572";
        const string Onyxia = "EX1_562"; // 9-cost legendary dragon

        static string Pick(Game g, Controller c, int index)
        {
            if (c.Choice == null || index < 0 || index >= c.Choice.Choices.Count) return null;
            if (!g.Resolution.HasSuspendedEffect) return Choose(g, c, index);
            int id = c.Choice.Choices[index]; string cid = g.IdEntityDic[id].Card.Id;
            var pd = g.GetPendingDecisions().First(d => d.ControllerId == c.Id);
            bool ok = g.TryAnswer(new SabberStoneCore.Model.Decisions.DecisionAnswer(pd.Id, c.Id, new[] { id }), out string err);
            Check("TryAnswer accepted", ok, err ?? "");
            return cid;
        }
        static BoardObject Loc(Controller c, string id) => c.BoardZone.GetLocations().FirstOrDefault(l => l.Card.Id == id);
        static bool Activate(Game g, Controller c, BoardObject loc, IPlayable target = null) => g.Process(new ActivateLocationTask(c, loc, target));
        static List<IPlayable> SpellTargets(Game g, Controller c)
        {
            var fb = Hand(c, Fireball);
            Mana(c, 10);
            var t = LegalTargets(fb);
            c.HandZone.Remove(fb);
            return t;
        }
        static List<int> PowerTargetIds(Controller c)
        {
            Mana(c, 10); c.Hero.HeroPower.IsExhausted = false;
            return c.Options().OfType<HeroPowerTask>().Where(o => o.Target != null).Select(o => o.Target.Id).ToList();
        }
        static int EnemyTotalHp(Controller o) => o.Hero.Health + o.BoardZone.Sum(m => m.Health);

        // ------------------------------------------------------------------ CATA_190h Deathwing, Worldbreaker
        static Game DeathwingGame(int herald, out Controller p, out Controller o, out Minion golem, out Minion yeti, out IPlayable dw)
        {
            var g = NewGame(s => s.PlayerHero = "HERO_07");
            p = P1(g); o = P2(g);
            golem = Board(o, Golem); yeti = Board(o, Yeti);
            p[HeraldProgress] = herald;
            dw = Hand(p, "CATA_190h");
            Play(g, dw);
            return g;
        }

        [AuditTest]
        static void CATA_190h_NoHerald_OneCataclysm_Raze()
        {
            var g = DeathwingGame(0, out var p, out var o, out var golem, out var yeti, out var dw);
            Eq("hero replaced by Deathwing", "CATA_190h", p.Hero.Card.Id);
            Eq("hero power Ruthless", "CATA_190p", p.Hero.HeroPower.Card.Id);
            Eq("12 armor from hero card", 12, Armor(p));
            var opts = ChoiceIds(g, p);
            Check("offers the 4 cataclysms", opts.OrderBy(x => x).SequenceEqual(new[] { "CATA_190t10", "CATA_190t11", "CATA_190t12", "CATA_190t13" }), string.Join(",", opts));
            int raze = opts.IndexOf("CATA_190t12");
            Pick(g, p, raze);
            Eq("Raze: golem 7->3", 3, Hp(golem));
            Eq("Raze: yeti 5->1", 1, Hp(yeti));
            Check("only one Cataclysm at Herald 0", p.Choice == null, Describe(g));
            Eq("Raze does not hit enemy hero", 30, o.Hero.Health);
        }

        [AuditTest]
        static void CATA_190h_TwoHeralds_TwoCataclysms_ToppleAndReign()
        {
            var g = DeathwingGame(2, out var p, out var o, out var golem, out var yeti, out var dw);
            var opts = ChoiceIds(g, p);
            Pick(g, p, opts.IndexOf("CATA_190t11"));
            Check("Topple destroyed the highest-Health enemy (golem)", golem.IsDead || golem.Zone != o.BoardZone, Describe(g));
            Check("yeti survives Topple", yeti.Zone == o.BoardZone, Describe(g));
            var opts2 = ChoiceIds(g, p);
            Eq("second offer has 3 remaining", 3, opts2.Count);
            Check("chosen one removed", !opts2.Contains("CATA_190t11"), string.Join(",", opts2));
            Pick(g, p, opts2.IndexOf("CATA_190t10"));
            Check("12/12 dragon summoned", p.BoardZone.Any(m => m.Card.Id == "CATA_190t14" && m.AttackDamage == 12 && m.Health == 12), Describe(g));
            Check("done after two", p.Choice == null, Describe(g));
        }

        [AuditTest]
        static void CATA_190h_FourHeralds_AllFour()
        {
            var g = DeathwingGame(4, out var p, out var o, out var golem, out var yeti, out var dw);
            int deckBefore = p.DeckZone.Count;
            int picks = 0;
            while (p.Choice != null && picks < 6)
            {
                var ids = ChoiceIds(g, p);
                int idx = ids.IndexOf("CATA_190t13"); if (idx < 0) idx = 0;
                Pick(g, p, idx); picks++;
            }
            Eq("four picks at Herald 4", 4, picks);
            var added = p.DeckZone.Where(c => c.Card.Id != Yeti).ToList();
            Eq("Enthrall shuffled 5 cards", 5, added.Count);
            Check("all are legendary dragons costing 1", added.All(c => c.Card.Rarity == Rarity.LEGENDARY && c.Card.IsRace(Race.DRAGON) && c.Cost == 1),
                string.Join(",", added.Select(c => c.Card.Id + ":" + c.Cost)));
            Check("12/12 on board", p.BoardZone.Any(m => m.Card.Id == "CATA_190t14"), Describe(g));
            Check("golem gone (Topple and/or Raze)", !o.BoardZone.Contains(golem), Describe(g));
            Check("yeti at most 1 Health or dead", !o.BoardZone.Contains(yeti) || Hp(yeti) == 1, Describe(g));
        }

        // ------------------------------------------------------------------ CATA_190p / CATA_190pe Ruthless
        [AuditTest]
        static void CATA_190p_PlusFiveAttackThisTurn()
        {
            var g = DeathwingGame(0, out var p, out var o, out _, out _, out _);
            while (p.Choice != null) Pick(g, p, 0);
            Eq("power cost 2", 2, p.Hero.HeroPower.Cost);
            Check("power used", Power(g, p), Describe(g));
            Eq("hero +5 attack", 5, p.Hero.AttackDamage);
            Check("CATA_190pe on hero", EnchantIds(p.Hero).Contains("CATA_190pe"), string.Join(",", EnchantIds(p.Hero)));
            var cl = Clone(g);
            Eq("clone hero 5 attack", 5, At(cl, p).Hero.AttackDamage);
            Check("hero attacks face", HeroAttack(g, p, o.Hero), Describe(g));
            Eq("boss took 5", 25, o.Hero.Health);
            EndTurn(g);
            Eq("attack gone on boss turn", 0, p.Hero.AttackDamage);
        }

        // ------------------------------------------------------------------ CATA_492 Shrine of Twilight
        [AuditTest]
        static void CATA_492_HeraldAndDraw()
        {
            var g = NewGame(s => s.PlayerHero = "HERO_07");
            var p = P1(g);
            var shrine = Hand(p, "CATA_492");
            Check("played", Play(g, shrine), Describe(g));
            var loc = Loc(p, "CATA_492");
            Check("on board", loc != null, Describe(g));
            Eq("durability 2", 2, loc.Durability);
            int hand = p.HandZone.Count, deck = p.DeckZone.Count;
            Check("activated", Activate(g, p, loc), Describe(g));
            Eq("drew a card", hand + 1, p.HandZone.Count);
            Eq("deck -1", deck - 1, p.DeckZone.Count);
            Check("Soldier of Cho'gall summoned", BoardIds(p).Contains("CATA_725t"), Describe(g));
            Eq("herald progress 1", 1, p[HeraldProgress]);
            Eq("durability 1", 1, loc.Durability);
            Check("cannot reactivate same turn", !Activate(g, p, loc), Describe(g));
            Round(g);
            Check("activated again", Activate(g, p, loc), Describe(g));
            Eq("herald progress 2", 2, p[HeraldProgress]);
            Check("shrine gone after 2 uses", Loc(p, "CATA_492") == null, Describe(g));
        }

        // ------------------------------------------------------------------ CATA_496 Cursed Chains
        [AuditTest]
        static void CATA_496_StealUntilEndOfTheirTurn_CantAttack()
        {
            var g = NewGame(s => s.PlayerHero = "HERO_07");
            var p = P1(g); var o = P2(g);
            var foe = Board(o, Yeti);
            var mine = Board(p, Wisp);
            var cc = Hand(p, "CATA_496");
            Check("cannot target own minion", !LegalTargets(cc).Contains(mine), "");
            Check("played", Play(g, cc, foe), Describe(g));
            Check("now mine", foe.Controller == p && p.BoardZone.Contains(foe), Describe(g));
            bool attacked = Attack(g, (Minion)foe, o.Hero);
            Check("can't attack this turn", !attacked && o.Hero.Health == 30, Describe(g));
            EndTurn(g);
            Check("still mine during their turn", foe.Controller == p, Describe(g));
            EndTurn(g);
            Check("returned at the end of their turn", foe.Controller == o && o.BoardZone.Contains(foe), Describe(g));
        }

        // ------------------------------------------------------------------ CATA_725 Shadowsworn Disciple
        [AuditTest]
        static void CATA_725_HeraldAndDeathrattleHeal()
        {
            var g = NewGame(s => s.PlayerHero = "HERO_07");
            var p = P1(g);
            Damage(g, p.Hero, 10);
            var d = (Minion)PlayNew(g, p, "CATA_725");
            Check("Soldier summoned", BoardIds(p).Contains("CATA_725t"), Describe(g));
            Eq("herald progress 1", 1, p[HeraldProgress]);
            Kill(g, d);
            Eq("deathrattle restores 3", 23, p.Hero.Health);
        }

        // ------------------------------------------------------------------ CORE_CS2_062 Hellfire
        [AuditTest]
        static void CORE_CS2_062_ThreeToAllCharacters_SpellDamage()
        {
            var g = NewGame();
            var p = P1(g); var o = P2(g);
            var a = Board(p, Golem); var b = Board(o, Golem);
            PlayNew(g, p, "CORE_CS2_062");
            Eq("own minion -3", 4, Hp(a)); Eq("enemy minion -3", 4, Hp(b));
            Eq("own hero -3", 27, p.Hero.Health); Eq("enemy hero -3", 27, o.Hero.Health);
            Board(p, Kobold);
            PlayNew(g, p, "CORE_CS2_062");
            Eq("with spell damage enemy hero -4", 23, o.Hero.Health);
            Eq("with spell damage own golem -4", 0, Math.Max(0, Hp(a)));
        }

        // ------------------------------------------------------------------ CORE_EX1_302 Mortal Coil
        [AuditTest]
        static void CORE_EX1_302_KillDraws_SurviveNoDraw()
        {
            var g = NewGame();
            var p = P1(g); var o = P2(g);
            var w = Board(o, Wisp); var y = Board(o, Yeti);
            var mc = Hand(p, "CORE_EX1_302");
            Check("hero not a legal target", !LegalTargets(mc).Contains(o.Hero), "");
            Play(g, mc, y);
            Eq("yeti -1", 4, Hp(y)); Eq("no draw", 0, p.HandZone.Count);
            PlayNew(g, p, "CORE_EX1_302", w);
            Check("wisp dead", w.Zone != o.BoardZone, Describe(g));
            Eq("drew 1", 1, p.HandZone.Count);
        }

        // ------------------------------------------------------------------ CORE_EX1_309 Siphon Soul
        [AuditTest]
        static void CORE_EX1_309_DestroyAndHeal3()
        {
            var g = NewGame();
            var p = P1(g); var o = P2(g);
            Damage(g, p.Hero, 10);
            var y = Board(o, Golem);
            PlayNew(g, p, "CORE_EX1_309", y);
            Check("destroyed", y.Zone != o.BoardZone, Describe(g));
            Eq("hero healed 3", 23, p.Hero.Health);
        }

        // ------------------------------------------------------------------ EDR_489 Agamaggan
        [AuditTest]
        static void EDR_489_NextCardCostsOpponentHealth()
        {
            var g = NewGame();
            var p = P1(g); var o = P2(g);
            PlayNew(g, p, "EDR_489");
            Eq("mana spent on Agamaggan", 0, p.RemainingMana);
            var golem = Hand(p, Golem);
            Check("Golem playable at 0 mana", CanPlay(golem), Describe(g));
            Check("played", Play(g, golem, keepMana: true), Describe(g));
            Check("golem on board", p.BoardZone.Contains(golem), Describe(g));
            Eq("opponent lost 7 Health", 23, o.Hero.Health);
            Eq("own hero untouched", 30, p.Hero.Health);
            var y = Hand(p, Yeti);
            Check("only the next card: Yeti not playable at 0 mana", !CanPlay(y), Describe(g));
        }

        [AuditTest]
        static void EDR_489_CapAtTen()
        {
            var g = NewGame();
            var p = P1(g); var o = P2(g);
            PlayNew(g, p, "EDR_489");
            var giant = Hand(p, "EX1_620"); // Molten Giant
            Note("Molten Giant cost " + giant.Cost);
            bool played = Play(g, giant, keepMana: true);
            Note("cost-" + giant.Cost + " card with the grant: played=" + played + " boss hp " + o.Hero.Health + " (engine reads '(up to 10)' as: only cards costing <=10 can be paid with Health)");
        }

        // ------------------------------------------------------------------ EDR_846 Shaladrassil
        [AuditTest]
        static void EDR_846_GetsFiveDreams()
        {
            var g = NewGame();
            var p = P1(g);
            PlayNew(g, p, "EDR_846");
            var ids = HandIds(p).OrderBy(x => x).ToList();
            Check("5 dreams", ids.SequenceEqual(new[] { "DREAM_01", "DREAM_02", "DREAM_03", "DREAM_04", "DREAM_05" }), string.Join(",", ids));
        }

        [AuditTest]
        static void EDR_846_CorruptedAfterHigherCost()
        {
            var g = NewGame();
            var p = P1(g);
            var sh = Hand(p, "EDR_846");
            PlayNew(g, p, Onyxia);
            Play(g, sh);
            var ids = HandIds(p).OrderBy(x => x).ToList();
            Check("5 corrupted dreams", ids.SequenceEqual(new[] { "EDR_846t1", "EDR_846t2", "EDR_846t3", "EDR_846t4", "EDR_846t5" }), string.Join(",", ids));
        }

        [AuditTest]
        static void EDR_846_NotCorruptedAfterLowerCost()
        {
            var g = NewGame();
            var p = P1(g);
            var sh = Hand(p, "EDR_846");
            PlayNew(g, p, Golem);
            Play(g, sh);
            Check("normal dreams", HandIds(p).All(x => x.StartsWith("DREAM_")) && p.HandZone.Count == 5, string.Join(",", HandIds(p)));
        }

        // ------------------------------------------------------------------ DREAM_01 Laughing Sister
        [AuditTest]
        static void DREAM_01_UntargetableBySpellsAndPowers()
        {
            var g = NewGame();
            var p = P1(g); var o = P2(g);
            var s = Board(o, "DREAM_01");
            var y = Board(o, Yeti);
            Eq("3/5", "3/5", Atk(s) + "/" + Hp(s));
            var t = SpellTargets(g, p);
            Check("spell cannot target", !t.Contains(s) && t.Contains(y), "");
            var pt = PowerTargetIds(p);
            Check("hero power cannot target", !pt.Contains(s.Id) && pt.Contains(y.Id), string.Join(",", pt));
        }

        // ------------------------------------------------------------------ DREAM_02 Ysera Awakens
        [AuditTest]
        static void DREAM_02_FiveToAllExceptYsera()
        {
            var g = NewGame();
            var p = P1(g); var o = P2(g);
            var ys = Board(p, Ysera);
            var a = Board(p, Golem); var b = Board(o, Golem);
            PlayNew(g, p, "DREAM_02");
            Eq("Ysera untouched", ys.BaseHealth, Hp(ys));
            Eq("own golem -5", 2, Hp(a)); Eq("enemy golem -5", 2, Hp(b));
            Eq("own hero -5", 25, p.Hero.Health); Eq("enemy hero -5", 25, o.Hero.Health);
            Board(p, Kobold);
            PlayNew(g, p, "DREAM_02");
            Eq("spell damage: enemy hero -6", 19, o.Hero.Health);
        }

        // ------------------------------------------------------------------ DREAM_03 / EDR_846t5
        [AuditTest]
        static void DREAM_03_EDR_846t5_Bodies()
        {
            var g = NewGame();
            var p = P1(g);
            var d = (Minion)PlayNew(g, p, "DREAM_03");
            Eq("Emerald Drake 7/6", "7/6", Atk(d) + "/" + Hp(d));
            Check("dragon", d.Card.IsRace(Race.DRAGON), "");
            var c = (Minion)PlayNew(g, p, "EDR_846t5");
            Eq("Corrupted Drake 14/12", "14/12", Atk(c) + "/" + Hp(c));
            Check("on board", p.BoardZone.Contains(d) && p.BoardZone.Contains(c), Describe(g));
        }

        // ------------------------------------------------------------------ DREAM_04 Dream
        [AuditTest]
        static void DREAM_04_ReturnToOwnersHand()
        {
            var g = NewGame();
            var p = P1(g); var o = P2(g);
            var y = Board(o, Yeti);
            PlayNew(g, p, "DREAM_04", y);
            Check("enemy yeti in enemy hand", HandIds(o).Contains(Yeti) && !o.BoardZone.Contains(y), Describe(g));
            Eq("not in my hand", 0, p.HandZone.Count);
            var m = Board(p, Golem);
            PlayNew(g, p, "DREAM_04", m);
            Check("own golem in own hand", HandIds(p).Contains(Golem), Describe(g));
        }

        // ------------------------------------------------------------------ DREAM_05 / DREAM_05e Nightmare
        [AuditTest]
        static void DREAM_05_OwnMinion_DestroyedStartOfYourNextTurn()
        {
            var g = NewGame();
            var p = P1(g);
            var y = Board(p, Yeti);
            PlayNew(g, p, "DREAM_05", y);
            Eq("+5/+5", "9/10", Atk(y) + "/" + Hp(y));
            Check("DREAM_05e applied", EnchantIds(y).Contains("DREAM_05e"), string.Join(",", EnchantIds(y)));
            EndTurn(g);
            Check("alive on the opponent's turn", p.BoardZone.Contains(y), Describe(g));
            EndTurn(g);
            Check("destroyed at the start of caster's next turn", !p.BoardZone.Contains(y), Describe(g));
        }

        [AuditTest]
        static void DREAM_05_EnemyMinion_DestroyedStartOfCastersNextTurn()
        {
            var g = NewGame();
            var p = P1(g); var o = P2(g);
            var y = Board(o, Yeti);
            PlayNew(g, p, "DREAM_05", y);
            Eq("+5/+5", "9/10", Atk(y) + "/" + Hp(y));
            EndTurn(g);
            Check("enemy keeps it during its own turn (destroy is at the start of YOUR next turn)", o.BoardZone.Contains(y), Describe(g));
            EndTurn(g);
            Check("destroyed at the start of caster's next turn", !o.BoardZone.Contains(y), Describe(g));
        }

        // ------------------------------------------------------------------ EDR_846t1 Corrupted Nightmare
        [AuditTest]
        static void EDR_846t1_ImmuneThisTurnAndPlus5()
        {
            var g = NewGame();
            var p = P1(g);
            var y = Board(p, Yeti);
            PlayNew(g, p, "EDR_846t1", y);
            Eq("+5/+5", "9/10", Atk(y) + "/" + Hp(y));
            Check("immune", y.IsImmune, "");
            Damage(g, y, 3);
            Eq("no damage while immune", 10, Hp(y));
            EndTurn(g);
            Check("immune gone next turn", !y.IsImmune, "");
            Eq("buff stays", "9/10", Atk(y) + "/" + Hp(y));
            Check("not destroyed", p.BoardZone.Contains(y), "");
            Round(g);
            Check("still alive after own next turn start", p.BoardZone.Contains(y), Describe(g));
        }

        // ------------------------------------------------------------------ EDR_846t2 Corrupted Dream
        [AuditTest]
        static void EDR_846t2_ShuffleIntoOwnersDeck()
        {
            var g = NewGame();
            var p = P1(g); var o = P2(g);
            var y = Board(o, "CS2_231");
            int od = o.DeckZone.Count, pd = p.DeckZone.Count;
            PlayNew(g, p, "EDR_846t2", y);
            Check("left board", !o.BoardZone.Contains(y), Describe(g));
            Eq("enemy deck +1", od + 1, o.DeckZone.Count);
            Check("wisp in enemy deck", DeckIds(o).Contains("CS2_231"), "");
            Eq("my deck unchanged", pd, p.DeckZone.Count);
        }

        // ------------------------------------------------------------------ EDR_846t3 / EDR_846t3e Corrupted Laughing Sister
        [AuditTest]
        static void EDR_846t3_ElusiveSelfAndHero()
        {
            var g = NewGame();
            var p = P1(g); var o = P2(g);
            var s = Board(p, "EDR_846t3");
            Eq("6/10", "6/10", Atk(s) + "/" + Hp(s));
            Check("sister elusive (spells)", s.CantBeTargetedBySpells, "");
            Check("sister elusive (powers)", s.CantBeTargetedByHeroPowers, "");
            Check("hero elusive (spells)", p.Hero.CantBeTargetedBySpells, "");
            Check("hero elusive (powers)", p.Hero.CantBeTargetedByHeroPowers, "");
            var t = SpellTargets(g, p);
            Check("my spells can't target hero/sister", !t.Contains(p.Hero) && !t.Contains(s), "");
            var cl = Clone(g);
            Check("clone keeps hero elusive", At(cl, p).Hero.CantBeTargetedBySpells, "");
            Kill(g, s);
            Check("hero targetable after sister dies", !p.Hero.CantBeTargetedBySpells, "");
        }

        // ------------------------------------------------------------------ EDR_846t4 Corrupted Awakening
        [AuditTest]
        static void EDR_846t4_FiveToAllEnemies()
        {
            var g = NewGame();
            var p = P1(g); var o = P2(g);
            var a = Board(p, Golem); var b = Board(o, Golem); var ys = Board(o, Ysera);
            PlayNew(g, p, "EDR_846t4");
            Eq("own golem untouched", 7, Hp(a));
            Eq("own hero untouched", 30, p.Hero.Health);
            Eq("enemy golem -5", 2, Hp(b));
            Eq("enemy hero -5", 25, o.Hero.Health);
            Check("enemy Ysera dealt with", !o.BoardZone.Contains(ys) || Hp(ys) == ys.BaseHealth - 5, Describe(g));
            Board(p, Kobold);
            PlayNew(g, p, "EDR_846t4");
            Eq("spell damage: enemy hero -6", 19, o.Hero.Health);
        }

        // ------------------------------------------------------------------ EDR_971 Critter Caretaker
        [AuditTest]
        static void EDR_971_EndOfYourTurnHealsBoth()
        {
            var g = NewGame();
            var p = P1(g); var o = P2(g);
            Damage(g, p.Hero, 10); Damage(g, o.Hero, 10);
            Board(p, "EDR_971");
            EndTurn(g);
            Eq("own hero +3", 23, p.Hero.Health);
            Eq("enemy hero +3", 23, o.Hero.Health);
            EndTurn(g);
            Eq("not on opponent's turn end (own)", 23, p.Hero.Health);
            Eq("not on opponent's turn end (enemy)", 23, o.Hero.Health);
        }

        // ------------------------------------------------------------------ END_020 Eternal Toil
        [AuditTest]
        static void END_020_SurviveDraw_DieSummonOneCost()
        {
            var g = NewGame();
            var p = P1(g); var o = P2(g);
            var y = Board(o, Yeti); var w = Board(o, Wisp);
            PlayNew(g, p, "END_020", y);
            Eq("yeti -1", 4, Hp(y));
            Eq("drew", 1, p.HandZone.Count);
            Eq("nothing summoned", 0, p.BoardZone.Count);
            ClearHand(p);
            PlayNew(g, p, "END_020", w);
            Check("wisp died", !o.BoardZone.Contains(w), "");
            Eq("no draw", 0, p.HandZone.Count);
            Eq("summoned one minion for me", 1, p.BoardZone.Count);
            if (p.BoardZone.Count == 1) Eq("its cost is 1", 1, p.BoardZone[0].Card.Cost);
        }

        // ------------------------------------------------------------------ TIME_030 Divergence
        [AuditTest]
        static void TIME_030_SplitMinionInHalves()
        {
            for (int seed = 1; seed <= 4; seed++)
            {
                var g = NewGame(s => s.Seed = seed);
                var p = P1(g);
                Hand(p, Golem);
                PlayNew(g, p, "TIME_030");
                var halves = p.HandZone.OfType<Minion>().ToList();
                Eq($"[{seed}] two halves", 2, halves.Count);
                if (halves.Count != 2) continue;
                Eq($"[{seed}] cost sums to 7", 7, halves.Sum(h => h.Cost));
                Eq($"[{seed}] attack sums to 7", 7, halves.Sum(h => h.AttackDamage));
                Eq($"[{seed}] health sums to 7", 7, halves.Sum(h => h.Health));
                Check($"[{seed}] each half 3 or 4", halves.All(h => (h.Cost == 3 || h.Cost == 4) && (h.AttackDamage == 3 || h.AttackDamage == 4) && (h.Health == 3 || h.Health == 4)),
                    string.Join(",", halves.Select(h => $"{h.Cost}:{h.AttackDamage}/{h.Health}")));
                Check($"[{seed}] TIME_030e2 on halves", halves.All(h => EnchantIds(h).Contains("TIME_030e2")), "");
                var h0 = halves[0]; int c0 = h0.Cost, a0 = h0.AttackDamage, hp0 = h0.Health;
                Play(g, h0);
                Eq($"[{seed}] played half keeps attack", a0, ((Minion)h0).AttackDamage);
                Eq($"[{seed}] played half keeps health", hp0, ((Minion)h0).Health);
            }
            var g2 = NewGame();
            PlayNew(g2, P1(g2), "TIME_030");
            Eq("empty hand: nothing", 0, P1(g2).HandZone.Count);
        }

        // ------------------------------------------------------------------ TIME_038 Mister Clocksworth
        [AuditTest]
        static void TIME_038_TwoLegendaries_ThreeRewinds()
        {
            var g = NewGame();
            var p = P1(g);
            var mc = Hand(p, "TIME_038");
            Check("played", Play(g, mc), Describe(g));
            var others = p.BoardZone.Where(m => m != mc).ToList();
            Eq("summoned 2", 2, others.Count);
            Check("both legendary minions", others.All(m => m.Card.Rarity == Rarity.LEGENDARY), string.Join(",", others.Select(m => m.Card.Id)));
            int rewinds = 0;
            // A Rewind restores the game in place; controller/entity objects are re-created, so re-read P1(g).
            while (P1(g).Choice != null && P1(g).Choice.ChoiceAction == ChoiceAction.REWIND && rewinds < 5)
            {
                Pick(g, P1(g), 0); rewinds++;
                var q = P1(g);
                Check($"after rewind {rewinds}: 3 minions", q.BoardZone.Count == 3, Describe(g));
                Check($"after rewind {rewinds}: 2 legendaries", q.BoardZone.Count(m => m.Card.Rarity == Rarity.LEGENDARY && !m.Card.Id.StartsWith("TIME_038")) == 2, Describe(g));
            }
            Eq("three rewinds offered", 3, rewinds);
            Check("no further offer", P1(g).Choice == null, Describe(g));
            Check("state ok", g.State == State.RUNNING, "");
        }

        // ------------------------------------------------------------------ TIME_EVENT_999 Sands of Time
        [AuditTest]
        static void TIME_EVENT_999_AnyClassThenOwnClass()
        {
            var g = NewGame();
            var p = P1(g);
            var st = Hand(p, "TIME_EVENT_999");
            Play(g, st);
            var opts = ChoiceIds(g, p);
            Eq("3 spells offered", 3, opts.Count);
            Check("all spells", opts.All(i => Cards.FromId(i).Type == CardType.SPELL), string.Join(",", opts));
            Note("first offer: " + string.Join(",", opts.Select(i => i + ":" + Cards.FromId(i).Class)));
            var first = Pick(g, p, 0);
            Check("first pick in hand", HandIds(P1(g)).Contains(first), Describe(g));
            Check("rewind offered", P1(g).Choice?.ChoiceAction == ChoiceAction.REWIND, Describe(g));
            Pick(g, P1(g), 0);
            p = P1(g);
            Check("rewound: first pick gone", !HandIds(p).Contains(first) || opts.Count(x => x == first) > 1, Describe(g));
            var opts2 = ChoiceIds(g, p);
            Check("after rewind: offer from own class (mage)", opts2.Count > 0 && opts2.All(i => Cards.FromId(i).Class == CardClass.MAGE && Cards.FromId(i).Type == CardType.SPELL),
                string.Join(",", opts2.Select(i => i + ":" + Cards.FromId(i).Class)));
            var picked = Pick(g, p, 0);
            p = P1(g);
            Check("picked spell in hand", HandIds(p).Contains(picked) && p.HandZone.Count == 1, Describe(g));
            Check("no second rewind", p.Choice == null, Describe(g));
        }

        // Multi-sample: the first offer must not be restricted to the hero's class.
        [AuditTest]
        static void TIME_EVENT_999_AnyClassSpread()
        {
            var classes = new HashSet<CardClass>();
            for (int seed = 1; seed <= 6; seed++)
            {
                var g = NewGame(s => s.Seed = seed);
                var p = P1(g);
                PlayNew(g, p, "TIME_EVENT_999");
                foreach (var i in ChoiceIds(g, p)) classes.Add(Cards.FromId(i).Class);
            }
            Check("offers span several classes", classes.Count >= 3, string.Join(",", classes));
        }

        // ------------------------------------------------------------------ TLC_451 Cursed Catacombs
        [AuditTest]
        static void TLC_451_DiscoverFromDeckTemporary()
        {
            var g = NewGame(s => s.PlayerDeck = new List<string> { Yeti, Wisp, Golem, "TLC_451", Yeti });
            var p = P1(g);
            int deck = p.DeckZone.Count;
            PlayNew(g, p, "TLC_451");
            var opts = ChoiceIds(g, p);
            Check("offers from deck, not Catacombs", opts.Count > 0 && opts.All(i => i != "TLC_451" && DeckIds(p).Contains(i)), string.Join(",", opts));
            Eq("distinct offers", opts.Count, opts.Distinct().Count());
            var pick = Pick(g, p, 0);
            Eq("deck -1", deck - 1, p.DeckZone.Count);
            var card = p.HandZone.FirstOrDefault(c => c.Card.Id == pick);
            Check("in hand", card != null, Describe(g));
            Check("temporary", card != null && TemporaryRules.IsTemporary(card), "");
            EndTurn(g);
            Check("discarded at end of turn", card != null && !p.HandZone.Contains(card), Describe(g));
        }

        // ------------------------------------------------------------------ CAP_105 Hook n' Heave
        [AuditTest]
        static void CAP_105_DiscoverPirateAndTwoCannoneers()
        {
            var g = NewGame(s => s.PlayerHero = "HERO_01");
            var p = P1(g);
            PlayNew(g, p, "CAP_105");
            var opts = ChoiceIds(g, p);
            Eq("3 offers", 3, opts.Count);
            Check("all pirates", opts.All(i => Cards.FromId(i).IsRace(Race.PIRATE)), string.Join(",", opts));
            var pick = Pick(g, p, 0);
            Check("pirate in hand", HandIds(p).Contains(pick), Describe(g));
            var cann = p.BoardZone.Where(m => m.Card.Id == "CAP_107t").ToList();
            Eq("two cannoneers", 2, cann.Count);
            Check("1/1", cann.All(m => m.AttackDamage == 1 && m.Health == 1), Describe(g));
        }

        // ------------------------------------------------------------------ CATA_582 Searing Fissure
        [AuditTest]
        static void CATA_582_OneToAllMinions_HeroPlus3()
        {
            var g = NewGame(s => s.PlayerHero = "HERO_01");
            var p = P1(g); var o = P2(g);
            var a = Board(p, Yeti); var b = Board(o, Yeti);
            PlayNew(g, p, "CATA_582");
            Eq("own -1", 4, Hp(a)); Eq("enemy -1", 4, Hp(b));
            Eq("heroes untouched", 60, p.Hero.Health + o.Hero.Health);
            Eq("hero +3", 3, p.Hero.AttackDamage);
            Board(p, Kobold);
            PlayNew(g, p, "CATA_582");
            Eq("spell damage: enemy yeti -2", 2, Hp(b));
            Eq("hero +6 stacked", 6, p.Hero.AttackDamage);
            EndTurn(g);
            Eq("attack gone", 0, p.Hero.AttackDamage);
        }

        // ------------------------------------------------------------------ CATA_584 Erupting Volcano
        [AuditTest]
        static void CATA_584_ThreeSplitAmongEnemies_SixAfterFire()
        {
            for (int seed = 1; seed <= 4; seed++)
            {
                foreach (bool fire in new[] { false, true })
                {
                    var g = NewGame(s => { s.PlayerHero = "HERO_01"; s.Seed = seed; });
                    var p = P1(g); var o = P2(g);
                    var mine = Board(p, Golem);
                    Board(o, Golem); Board(o, Golem);
                    PlayNew(g, p, "CATA_584");
                    var loc = Loc(p, "CATA_584");
                    Check($"[{seed}] on board, 3 durability", loc != null && loc.Durability == 3, Describe(g));
                    if (fire)
                    {
                        var fs = Hand(p, "CATA_582"); // Searing Fissure, Fire school
                        Eq($"[{seed}] Fissure is Fire", 2, fs.Card[GameTag.SPELL_SCHOOL]);
                        Play(g, fs);
                    }
                    int before = EnemyTotalHp(o), mineBefore = Hp(mine);
                    Check($"[{seed}] activated", Activate(g, p, loc), Describe(g));
                    Eq($"[{seed}] fire={fire}: damage to enemies", fire ? 6 : 3, before - EnemyTotalHp(o));
                    Eq($"[{seed}] own golem untouched", mineBefore, Hp(mine));
                    Eq($"[{seed}] own hero untouched", 30, p.Hero.Health);
                }
            }
            // a Fire spell on the previous turn does not count
            var g2 = NewGame(s => s.PlayerHero = "HERO_01");
            var p2 = P1(g2); var o2 = P2(g2);
            Board(o2, Golem);
            PlayNew(g2, p2, "CATA_582");
            Round(g2);
            PlayNew(g2, p2, "CATA_584");
            int b2 = EnemyTotalHp(o2);
            Activate(g2, p2, Loc(p2, "CATA_584"));
            Eq("fire spell last turn: only 3", 3, b2 - EnemyTotalHp(o2));
        }

        // Locations refresh at the start of their owner's next turn (1-turn cooldown).
        [AuditTest]
        static void CATA_584_UsableAgainNextTurn()
        {
            var g = NewGame(s => s.PlayerHero = "HERO_01");
            var p = P1(g); var o = P2(g);
            Board(o, Golem);
            PlayNew(g, p, "CATA_584");
            var loc = Loc(p, "CATA_584");
            Check("activated", Activate(g, p, loc), Describe(g));
            Round(g);
            Check("can activate on my next turn", LocationRules.CanActivate(p, loc, null),
                $"exhausted={loc.IsExhausted} EXHAUSTED={loc[GameTag.EXHAUSTED]} cooldown={loc[GameTag.LOCATION_ACTION_COOLDOWN]} durability={loc.Durability}");
            Round(g);
            Check("can activate two turns later", LocationRules.CanActivate(p, loc, null), "");
            // control: Shrine of Twilight (same activation framework) is usable on the next turn
            var g3 = NewGame(s => s.PlayerHero = "HERO_07");
            var p3 = P1(g3);
            PlayNew(g3, p3, "CATA_492");
            var sh = Loc(p3, "CATA_492");
            Activate(g3, p3, sh); Round(g3);
            Check("control: Shrine usable next turn", LocationRules.CanActivate(p3, sh, null), "");
        }

        // ------------------------------------------------------------------ CATA_585 Torch
        [AuditTest]
        static void CATA_585_EightToDamaged_ReturnsExcess()
        {
            var g = NewGame(s => s.PlayerHero = "HERO_01");
            var p = P1(g); var o = P2(g);
            var y = Board(o, Yeti); var full = Board(o, Golem);
            var t = Hand(p, "CATA_585");
            Check("undamaged not a target", !LegalTargets(t).Contains(full), "");
            Check("hero not a target", !LegalTargets(t).Contains(o.Hero), "");
            Damage(g, y, 1); // 4/4
            Check("damaged is target", LegalTargets(t).Contains(y), "");
            Play(g, t, y);
            Check("yeti died", !o.BoardZone.Contains(y), Describe(g));
            var back = p.HandZone.FirstOrDefault(c => c.Card.Id == "CATA_585");
            Check("returned to hand", back != null, Describe(g));
            if (back == null) return;
            Eq("excess = 8-4 = 4", 4, back[GameTag.TAG_SCRIPT_DATA_NUM_1]);
            Damage(g, full, 1); // 7/6
            Play(g, back, full);
            Eq("golem 6-4", 2, Hp(full));
            Check("no excess, not returned", !HandIds(p).Contains("CATA_585"), Describe(g));
        }

        // ------------------------------------------------------------------ EDR_456 Darkrider
        [AuditTest]
        static void EDR_456_HoldingDragon_DiscoverDarkGiftDragon()
        {
            var g = NewGame(s => s.PlayerHero = "HERO_01");
            var p = P1(g);
            Hand(p, "DREAM_03");
            PlayNew(g, p, "EDR_456");
            var opts = ChoiceIds(g, p);
            Check("offers dragons", opts.Count > 0 && opts.All(i => Cards.FromId(i).IsRace(Race.DRAGON) && Cards.FromId(i).Type == CardType.MINION), string.Join(",", opts));
            var pick = Pick(g, p, 0);
            var card = p.HandZone.LastOrDefault(c => c.Card.Id == pick);
            Check("in hand", card != null, Describe(g));
            Note("dark gift enchants: " + (card == null ? "" : string.Join(",", EnchantIds(card))));
            Check("has a Dark Gift", card != null && EnchantIds(card).Count > 0, "");
        }

        [AuditTest]
        static void EDR_456_NoDragon_NoDiscover()
        {
            var g = NewGame(s => s.PlayerHero = "HERO_01");
            var p = P1(g);
            Hand(p, Yeti);
            PlayNew(g, p, "EDR_456");
            Check("no choice", p.Choice == null, Describe(g));
            Eq("hand unchanged", 1, p.HandZone.Count);
        }

        // ------------------------------------------------------------------ EDR_457 / EDR_457t Brood Keeper
        [AuditTest]
        static void EDR_457_HoldingDragon_Equip22()
        {
            var g = NewGame(s => s.PlayerHero = "HERO_01");
            var p = P1(g);
            PlayNew(g, p, "EDR_457");
            Check("no dragon -> no weapon", Weapon(p) == null, Describe(g));
            Hand(p, "DREAM_03");
            PlayNew(g, p, "EDR_457");
            Eq("weapon equipped", "EDR_457t", Weapon(p));
            if (p.Hero.Weapon != null) Eq("2/2", "2/2", p.Hero.Weapon.AttackDamage + "/" + p.Hero.Weapon.Durability);
            Eq("hero attack 2", 2, p.Hero.AttackDamage);
        }

        // ------------------------------------------------------------------ EDR_492 / EDR_492t Mother Duck
        [AuditTest]
        static void EDR_492_ThreeRushDucklings()
        {
            var g = NewGame();
            var p = P1(g); var o = P2(g);
            var md = PlayNew(g, p, EDR_492_Id);
            var d = p.BoardZone.Where(m => m.Card.Id == "EDR_492t").ToList();
            Eq("three ducklings", 3, d.Count);
            Check("1/1 rush", d.All(m => m.AttackDamage == 1 && m.Health == 1 && m.IsRush), Describe(g));
            var foe = Board(o, Wisp);
            Check("duckling attacks a minion immediately", g.Process(MinionAttackTask.Any(p, d[0], foe)), Describe(g));
            Check("duckling cannot attack hero", !p.Options().OfType<MinionAttackTask>().Any(t => t.Source == d[1] && t.Target == o.Hero), "");
        }
        const string EDR_492_Id = "EDR_492";

        // ------------------------------------------------------------------ END_033 Prescient Slitherdrake
        [AuditTest]
        static void END_033_CostAndElusive()
        {
            var g = NewGame();
            var p = P1(g);
            var s = Hand(p, "END_033");
            Eq("7 without another dragon", 7, s.Cost);
            var dr = Hand(p, "DREAM_03");
            g.AuraUpdate();
            Eq("4 holding another dragon", 4, s.Cost);
            p.HandZone.Remove(dr); g.AuraUpdate();
            Eq("back to 7", 7, s.Cost);
            var m = Board(p, "END_033");
            Check("elusive spells", m.CantBeTargetedBySpells, "");
            Check("elusive powers", m.CantBeTargetedByHeroPowers, "");
        }

        // ------------------------------------------------------------------ FIR_939 Shadowflame Suffusion
        [AuditTest]
        static void FIR_939_TwoDamage_DiscoverWarriorDarkGift()
        {
            var g = NewGame(s => s.PlayerHero = "HERO_01");
            var p = P1(g); var o = P2(g);
            var f = Hand(p, "FIR_939");
            Check("hero is a legal target", LegalTargets(f).Contains(o.Hero), "");
            Play(g, f, o.Hero);
            Eq("enemy hero -2", 28, o.Hero.Health);
            var opts = ChoiceIds(g, p);
            Check("warrior minions", opts.Count > 0 && opts.All(i => Cards.FromId(i).Class == CardClass.WARRIOR && Cards.FromId(i).Type == CardType.MINION), string.Join(",", opts));
            var pick = Pick(g, p, 0);
            var card = p.HandZone.LastOrDefault(c => c.Card.Id == pick);
            Check("in hand with Dark Gift", card != null && EnchantIds(card).Count > 0, card == null ? Describe(g) : string.Join(",", EnchantIds(card)));
            var y = Board(o, Yeti);
            Board(p, Kobold);
            PlayNew(g, p, "FIR_939", y);
            Eq("spell damage: yeti -3", 2, Hp(y));
        }

        // ------------------------------------------------------------------ JAIL_384 Chainbreaker Hogger
        [AuditTest]
        static void JAIL_384_DuplicatesLegendaries()
        {
            var deck = new List<string> { "JAIL_384", Onyxia, Ysera }; deck.AddRange(Fill(12));
            var g = NewGame(s => { s.PlayerDeck = deck; s.ClearHands = false; s.PlayerHero = "HERO_01"; });
            var p = P1(g);
            var all = p.DeckZone.Concat(p.HandZone).Select(c => c.Card.Id).Where(i => i != "GAME_005").ToList();
            Eq("Onyxia duplicated", 2, all.Count(i => i == Onyxia));
            Eq("Ysera duplicated", 2, all.Count(i => i == Ysera));
            Eq("Hogger not duplicated", 1, all.Count(i => i == "JAIL_384"));
            Eq("Yetis untouched", 12, all.Count(i => i == Yeti));
            var h = Board(p, "JAIL_384");
            Check("taunt 10/10", h.HasTaunt && h.AttackDamage == 10 && h.Health == 10, Describe(g));
        }

        // ------------------------------------------------------------------ JAIL_421 Warptooth
        [AuditTest]
        static void JAIL_421_FourFriendlyDamaged_SummonFromDeck()
        {
            var g = NewGame(s => s.PlayerHero = "HERO_01");
            var p = P1(g);
            Board(p, Golem); Board(p, Golem); Board(p, Golem);
            var wt = DeckTop(p, "JAIL_421");
            PlayNew(g, p, "CORE_CS2_062"); // Hellfire: own hero + 3 golems
            Check("Warptooth summoned from deck", p.BoardZone.Contains(wt), Describe(g));
            if (p.BoardZone.Contains(wt)) Check("charge", ((Minion)wt).HasCharge, "");
        }

        [AuditTest]
        static void JAIL_421_FromHand_AcrossActions()
        {
            var g = NewGame(s => s.PlayerHero = "HERO_01");
            var p = P1(g);
            var a = Board(p, Golem); var b = Board(p, Golem); var c = Board(p, Golem);
            var wt = Hand(p, "JAIL_421");
            Damage(g, a, 1); Damage(g, b, 1); Damage(g, p.Hero, 1);
            Check("3 damaged: not yet", p.HandZone.Contains(wt), Describe(g));
            Damage(g, c, 1);
            PlayNew(g, p, Wisp); // a later action boundary
            Check("4th character damaged -> summoned from hand", p.BoardZone.Contains(wt), Describe(g));
        }

        [AuditTest]
        static void JAIL_421_NotOnOpponentsTurn()
        {
            var g = NewGame(s => s.PlayerHero = "HERO_01");
            var p = P1(g); var o = P2(g);
            Board(p, Golem); Board(p, Golem); Board(p, Golem);
            var wt = Hand(p, "JAIL_421");
            EndTurn(g);
            ClearHand(o);
            PlayNew(g, o, "CORE_CS2_062");
            Check("not summoned on opponent's turn", p.HandZone.Contains(wt), Describe(g));
        }

        // ------------------------------------------------------------------ TIME_034 / TIME_034e Stadium Announcer
        [AuditTest]
        static void TIME_034_BothEquipRandomWeapon_YoursPlusOne()
        {
            for (int seed = 1; seed <= 4; seed++)
            {
                var g = NewGame(s => { s.PlayerHero = "HERO_01"; s.Seed = seed; });
                var p = P1(g); var o = P2(g);
                PlayNew(g, p, "TIME_034");
                Check($"[{seed}] I have a weapon", p.Hero.Weapon != null, Describe(g));
                Check($"[{seed}] opponent has a weapon", o.Hero.Weapon != null, Describe(g));
                if (p.Hero.Weapon == null) continue;
                var w = p.Hero.Weapon;
                Eq($"[{seed}] mine +1 attack", w.Card.ATK + 1, w.AttackDamage);
                Eq($"[{seed}] mine +1 durability", w.Card[GameTag.DURABILITY] + 1, w.Durability);
                Check($"[{seed}] TIME_034e on mine", EnchantIds(w).Contains("TIME_034e"), "");
                if (o.Hero.Weapon != null) Check($"[{seed}] theirs not buffed", o.Hero.Weapon.AttackDamage == o.Hero.Weapon.Card.ATK && !EnchantIds(o.Hero.Weapon).Contains("TIME_034e"), Describe(g));
                Check($"[{seed}] rewind offered", p.Choice?.ChoiceAction == ChoiceAction.REWIND, Describe(g));
                if (p.Choice != null)
                {
                    Pick(g, p, 0); p = P1(g); o = P2(g);
                    Check($"[{seed}] after rewind weapons again", p.Hero.Weapon != null && o.Hero.Weapon != null, Describe(g));
                    if (p.Hero.Weapon != null) Check($"[{seed}] after rewind mine buffed", EnchantIds(p.Hero.Weapon).Contains("TIME_034e"), Describe(g));
                }
                Check($"[{seed}] single rewind", P1(g).Choice == null, Describe(g));
            }
        }

        // ------------------------------------------------------------------ TLC_600 Windpeak Wyrm
        [AuditTest]
        static void TLC_600_Battlecry_And_Kindred()
        {
            var g = NewGame(s => s.PlayerHero = "HERO_01");
            var p = P1(g); var o = P2(g);
            var w = Hand(p, "TLC_600");
            Eq("8 without kindred", 8, w.Cost);
            Play(g, w, o.Hero);
            Eq("enemy hero -5", 25, o.Hero.Health);
            Eq("5 armor", 5, Armor(p));
            PlayNew(g, p, "DREAM_03");
            Round(g);
            var w2 = Hand(p, "TLC_600");
            g.AuraUpdate();
            Eq("6 after playing a dragon last turn", 6, w2.Cost);
            var y = Board(o, Golem);
            Play(g, w2, y);
            Eq("golem -5", 2, Hp(y));
        }
    }
}
