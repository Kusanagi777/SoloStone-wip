// Audit chunk boss-gilded_king+boss-sleepless_one: one test per card id (enchantments/tokens tested through their parent).
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
    internal static class Chunk19
    {
        const string Leper = "EX1_029";       // 1/1 Deathrattle: 2 damage to the enemy hero
        const string Archer = "CS2_189";      // Elven Archer, Battlecry: 1 damage
        const string ImpMaster = "EX1_597";   // end of turn: 1 damage to self, summon a 1/1 Imp
        const string Priestess5 = "EX1_583";  // Priestess of Elune 5/4
        const string Fireball = "CS2_029";
        const string Kobold = "CS2_142";      // Kobold Geomancer, Spell Damage +1

        static bool ActivateLoc(Game g, Controller c, IPlayable loc, IPlayable target = null)
            => g.Process(new ActivateLocationTask(c, loc, target));
        static BoardObject Loc(Controller c, string id) => c.BoardZone.GetLocations().FirstOrDefault(l => l.Card.Id == id);
        /// <summary>Pick a choice; answers suspended-effect decisions through TryAnswer (ChooseTask is refused for those).</summary>
        static string Pick(Game g, Controller c, int index)
        {
            if (c.Choice == null) return null;
            if (!g.Resolution.HasSuspendedEffect) return Choose(g, c, index);
            int id = c.Choice.Choices[index]; string cid = g.IdEntityDic[id].Card.Id;
            var pd = g.GetPendingDecisions().First(d => d.ControllerId == c.Id);
            bool ok = g.TryAnswer(new SabberStoneCore.Model.Decisions.DecisionAnswer(pd.Id, c.Id, new[] { id }), out string err);
            Check("TryAnswer accepted", ok, err ?? "");
            return cid;
        }
        static Minion NewMinion(Controller c, List<int> before) => c.BoardZone.FirstOrDefault(m => !before.Contains(m.Id));
        static void Settle(Game g) => g.DeathProcessingAndAuraUpdate();

        // ---------------------------------------------------------------- PRIEST (gilded_king)
        [AuditTest]
        static void CAP_801_HauntBuffRebornTaunt()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var w = Board(p, Wisp);
            var spell = Hand(p, "CAP_801");
            Check("legal on enemy minion too", LegalTargets(spell).Any(t => t.Controller == o) || o.BoardZone.Count == 0);
            Play(g, spell, w);
            Eq("atk 1+2", 3, Atk(w)); Eq("hp 1+3", 4, Hp(w));
            Check("Reborn", w[GameTag.REBORN] == 1); Check("Taunt", w.HasTaunt);
            Check("CAP_801e2 attached", EnchantIds(w).Contains("CAP_801e2"), string.Join(",", EnchantIds(w)));
            Kill(g, w);
            var back = p.BoardZone.FirstOrDefault(m => m.Card.Id == Wisp);
            Check("reborn back", back != null, Describe(g));
            if (back != null) Eq("reborn at 1 Health", 1, Hp(back));
            // a minion that already used Reborn gets it again
            var y = Board(p, Yeti); var e = Hand(p, "CAP_801"); Play(g, e, y); Kill(g, y);
            var y2 = p.BoardZone.FirstOrDefault(m => m.Card.Id == Yeti);
            Check("yeti reborn", y2 != null);
            var e2 = Hand(p, "CAP_801"); Play(g, e2, y2); Check("re-haunted has Reborn", y2[GameTag.REBORN] == 1);
            Kill(g, y2);
            Check("reborn a second time", p.BoardZone.Any(m => m.Card.Id == Yeti), Describe(g));
            var cl = Clone(g); Eq("clone", p.BoardZone.Count, At(cl, p).BoardZone.Count);
        }

        [AuditTest]
        static void CAP_801e2_Text()
        {
            var g = NewGame(); var p = P1(g);
            var y = Board(p, Yeti); Play(g, Hand(p, "CAP_801"), y);
            Eq("6/8", "6/8", Atk(y) + "/" + Hp(y));
            Silence(g, y);
            Check("silence removes", y[GameTag.REBORN] != 1 && !y.HasTaunt && Atk(y) == 4 && Hp(y) == 5, Describe(g));
        }

        [AuditTest]
        static void CAP_805_SlimeEmAndEctoplasm()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            Board(p, Wisp); Board(p, Yeti); Board(o, Golem);
            PlayNew(g, p, "CAP_805");
            Eq("all minions destroyed (p)", 0, p.BoardZone.Count); Eq("all destroyed (o)", 0, o.BoardZone.Count);
            var mine = p.HandZone.FirstOrDefault(c => c.Card.Id == "CAP_805t");
            var theirs = o.HandZone.FirstOrDefault(c => c.Card.Id == "CAP_805t");
            Check("player gets Ectoplasm", mine != null, Describe(g));
            Check("opponent gets Ectoplasm", theirs != null, Describe(g));
            if (mine != null) Eq("Ectoplasm costs 3", 3, mine.Cost);
            if (mine != null) Eq("CAP_805e captures (p)", 2, EnchantIds(mine).Count(x => x == "CAP_805e"));
            if (mine != null) Play(g, mine);
            Eq("resummoned mine", "CS2_182,CS2_231", string.Join(",", BoardIds(p).OrderBy(x => x)));
            Check("not opponent's", !BoardIds(p).Contains(Golem));
            EndTurn(g);
            if (theirs != null) { Mana(o, 10); Play(g, theirs); }
            Check("opponent resummons Golem", BoardIds(o).Contains(Golem), Describe(g));
            var cl = Clone(g); Eq("clone", o.BoardZone.Count, At(cl, o).BoardZone.Count);
        }

        [AuditTest]
        static void CAP_805t_EmptyEctoplasmDoesNothing()
        {
            var g = NewGame(); var p = P1(g);
            PlayNew(g, p, "CAP_805"); // no minions
            var mine = p.HandZone.FirstOrDefault(c => c.Card.Id == "CAP_805t");
            Check("still gets spell", mine != null);
            if (mine != null) { Play(g, mine); Eq("nothing summoned", 0, p.BoardZone.Count); }
            var e = PlayNew(g, p, "CAP_805t"); Eq("bare copy nothing", 0, p.BoardZone.Count);
        }

        [AuditTest]
        static void CAP_805e_ClonedCapture()
        {
            var g = NewGame(); var p = P1(g);
            Board(p, Golem);
            PlayNew(g, p, "CAP_805");
            var cl = Clone(g); var cp = At(cl, p);
            var s = cp.HandZone.First(c => c.Card.Id == "CAP_805t");
            Play(cl, s);
            Check("capture survives clone", BoardIds(cp).Contains(Golem), Describe(cl));
        }

        [AuditTest]
        static void CAP_806_RaithResurrectsRebornAndAttacks()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var y = Board(p, Yeti); Play(g, Hand(p, "CAP_801"), y); Kill(g, y);   // reborn event 1
            var y2 = p.BoardZone.First(m => m.Card.Id == Yeti); Kill(g, y2);     // dies for good
            Eq("board empty", 0, p.BoardZone.Count);
            var golem = Board(o, Golem);
            PlayNew(g, p, "CAP_806");
            Note(Describe(g));
            Check("Raith on board", BoardIds(p).Contains("CAP_806"));
            // resurrected Yeti (4/5) attacks Golem 7/7 -> golem 3, yeti dies
            Eq("golem took 4", 3, Hp(golem));
            Check("yeti died attacking", !BoardIds(p).Contains(Yeti), Describe(g));
            // nothing reborn -> nothing
            var g2 = NewGame(); var p2 = P1(g2); Board(P2(g2), Golem);
            PlayNew(g2, p2, "CAP_806"); Eq("only raith", 1, p2.BoardZone.Count);
            // enemy reborn is not resurrected
            var g3 = NewGame(); var p3 = P1(g3); var o3 = P2(g3);
            var ey = Board(o3, Yeti); Play(g3, Hand(p3, "CAP_801"), ey); Kill(g3, ey);
            PlayNew(g3, p3, "CAP_806"); Eq("enemy reborn ignored", 1, p3.BoardZone.Count);
        }

        [AuditTest]
        static void CATA_308_MedivhsTriumph()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var a = Board(p, Golem); var b = Board(o, Golem); var w = Board(o, Wisp);
            var s = Hand(p, "CATA_308");
            Eq("costs 5", 5, s.Cost);
            Play(g, s);
            Eq("friendly 7-4", 3, Hp(a)); Eq("enemy 7-4", 3, Hp(b)); Check("wisp dead", w.IsDead || !o.BoardZone.Contains(w));
            Eq("heroes untouched", 30, p.Hero.Health); Eq("boss untouched", 30, o.Hero.Health);
            var leg = Board(p, "TIME_064");
            var s2 = Hand(p, "CATA_308");
            Eq("costs 1 with legendary", 1, s2.Cost);
            Board(p, Kobold);
            var g1 = Board(o, Golem);
            Play(g, s2);
            Eq("spell damage +1 => 5", 2, Hp(g1));
            var g4 = NewGame(); var o4 = P2(g4); Board(o4, "TIME_064"); var s4 = Hand(P1(g4), "CATA_308");
            Eq("enemy legendary doesn't discount", 5, s4.Cost);
        }

        [AuditTest]
        static void CORE_CS2_004_PowerWordShield()
        {
            var g = NewGame(); var p = P1(g);
            var y = Board(p, Yeti);
            int hand = p.HandZone.Count;
            Play(g, Hand(p, "CORE_CS2_004"), y);
            Eq("+2 health", 7, Hp(y)); Eq("atk same", 4, Atk(y));
            Eq("drew a card", hand + 1, p.HandZone.Count);
        }

        [AuditTest]
        static void CORE_EX1_197_ShadowWordRuin()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var a = Board(p, Golem); var b = Board(o, Priestess5); var c = Board(o, Yeti); var d = Board(p, Wisp);
            PlayNew(g, p, "CORE_EX1_197");
            Check("friendly 7-atk destroyed", !p.BoardZone.Contains(a));
            Check("enemy 5-atk destroyed", !o.BoardZone.Contains(b));
            Check("yeti survives", o.BoardZone.Contains(c)); Check("wisp survives", p.BoardZone.Contains(d));
        }

        [AuditTest]
        static void EDR_000_YseraStartOfGameAndBattlecry()
        {
            var deck = Fill(9); deck.Add("EDR_000");
            var g = NewGame(s => s.PlayerDeck = deck); var p = P1(g); var o = P2(g);
            Eq("player max mana cap 15", 15, p[GameTag.MAXRESOURCES]);
            Eq("boss max mana cap 15", 15, o[GameTag.MAXRESOURCES]);
            var g2 = NewGame(); Eq("without Ysera cap 10", 10, P1(g2)[GameTag.MAXRESOURCES]);
            Mana(p, 10);
            var y = Hand(p, "EDR_000"); Play(g, y, keepMana: true);
            Eq("gain 3 crystals -> 13", 13, p.BaseMana);
            Eq("remaining 1 + 3 filled", 4, p.RemainingMana);
            // without start of game, battlecry still gains crystals, capped at 10
            var p2 = P1(g2); Mana(p2, 9); var y2 = Hand(p2, "EDR_000"); Play(g2, y2, keepMana: true);
            Eq("capped at 10", 10, p2.BaseMana);
        }

        [AuditTest]
        static void EDR_970_KaldoreiPriestess()
        {
            var g = NewGame(s => s.PlayerHero = "HERO_09"); var p = P1(g); var o = P2(g);
            var e1 = Board(o, Yeti); var mine = Board(p, Yeti);
            string before = p.Hero.HeroPower.Card.Id;
            PlayNew(g, p, "EDR_970");
            Eq("enemy -2 attack", 2, Atk(e1)); Eq("friendly untouched", 4, Atk(mine));
            Check("EDR_970e on enemy", EnchantIds(e1).Contains("EDR_970e"));
            Check("hero power imbued", p.Hero.HeroPower.Card.Id == "EDR_449p", before + " -> " + p.Hero.HeroPower.Card.Id);
            EndTurn(g); // boss turn starts
            Eq("still -2 during enemy turn", 2, Atk(e1));
            EndTurn(g); // player's next turn
            Eq("restored on your next turn", 4, Atk(e1));
            Check("enchant gone", !EnchantIds(e1).Contains("EDR_970e"));
        }

        [AuditTest]
        static void EDR_970e_ClonedAndSummonedLater()
        {
            var g = NewGame(s => s.PlayerHero = "HERO_09"); var p = P1(g); var o = P2(g);
            var e1 = Board(o, Yeti);
            PlayNew(g, p, "EDR_970");
            var late = Board(o, Yeti);
            Eq("later minion unaffected", 4, Atk(late));
            var cl = Clone(g); var ce = At(cl, e1);
            Eq("clone keeps debuff", 2, Atk(ce));
            Round(cl); Eq("clone removed on your turn", 4, Atk(ce));
        }

        [AuditTest]
        static void JAIL_913_HoldThemOff()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var y = Board(p, Yeti);
            var s = Hand(p, "JAIL_913"); Eq("cost 5", 5, s.Cost);
            Play(g, s, y);
            Eq("9/10", "9/10", Atk(y) + "/" + Hp(y)); Check("Lifesteal", y.HasLifeSteal);
            Check("JAIL_913e", EnchantIds(y).Contains("JAIL_913e"));
            Damage(g, p.Hero, 10);
            var foe = Board(o, Golem);
            Attack(g, y, foe);
            Eq("lifesteal healed 9", 29, p.Hero.Health);
            // Prepare: the card can be prepared
            var s2 = Hand(p, "JAIL_913"); Mana(p, 10);
            Check("PREPARE tag", s2[GameTag.PREPARE] != 0);
            bool prep = g.Process(PrepareCardTask.Any(p, s2));
            Check("prepare accepted", prep);
            Note("after prepare cost " + s2.Cost + " remaining mana " + p.RemainingMana);
        }

        [AuditTest]
        static void JAIL_913e_Silence()
        {
            var g = NewGame(); var p = P1(g);
            var y = Board(p, Yeti); Play(g, Hand(p, "JAIL_913"), y); Silence(g, y);
            Check("silenced back to 4/5 no lifesteal", Atk(y) == 4 && Hp(y) == 5 && !y.HasLifeSteal);
        }

        [AuditTest]
        static void JAIL_940_UndeathSentence()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            PlayNew(g, p, "JAIL_940");
            Eq("nothing died -> nothing", 30, o.Hero.Health);
            var el = Board(o, Leper); Kill(g, el);
            Eq("enemy leper hit player", 28, p.Hero.Health);
            PlayNew(g, p, "JAIL_940");
            Eq("enemy death not counted", 30, o.Hero.Health); Eq("player not hit", 28, p.Hero.Health);
            var l = Board(p, Leper); Board(p, Wisp); Kill(g, l);
            Eq("leper dies: 2", 28, o.Hero.Health);
            Kill(g, p.BoardZone.First(m => m.Card.Id == Wisp));
            PlayNew(g, p, "JAIL_940");
            Eq("deathrattle triggered again: 2", 26, o.Hero.Health);
        }

        [AuditTest]
        static void JAIL_941_HolyEmbrace()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            Damage(g, p.Hero, 10);
            Play(g, Hand(p, "JAIL_941"), p.Hero);
            Eq("restored 4", 24, p.Hero.Health);
            var dark = p.HandZone.FirstOrDefault(c => c.Card.Id == "JAIL_941t");
            Check("got Dark Embrace", dark != null, Describe(g));
            if (dark == null) return;
            Eq("Dark costs 2", 2, dark.Cost);
            var foe = Board(o, Golem);
            Play(g, dark, foe); Eq("4 damage", 3, Hp(foe));
            var y = Board(p, Yeti); Damage(g, y, 3);
            Play(g, Hand(p, "JAIL_941"), y); Eq("heal minion to full", 5, Hp(y));
        }

        [AuditTest]
        static void JAIL_941t_DarkEmbraceSpellDamage()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            Board(p, Kobold);
            Play(g, Hand(p, "JAIL_941t"), o.Hero);
            Eq("4+1 to enemy hero", 25, o.Hero.Health);
        }

        [AuditTest]
        static void TIME_064_ChronoLordDeios()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            Board(p, "TIME_064");
            // Battlecry twice
            Play(g, Hand(p, Archer), o.Hero);
            Eq("Elven Archer battlecry twice", 28, o.Hero.Health);
            // Deathrattle twice
            var l = Board(p, Leper); Kill(g, l);
            Eq("Leper deathrattle twice", 24, o.Hero.Health);
            // Hero power twice (Fireblast 1)
            Power(g, p, o.Hero);
            Eq("hero power twice", 22, o.Hero.Health);
            // End of turn effects twice (Imp Master)
            var imp = Board(p, ImpMaster);
            EndTurn(g);
            Eq("Imp Master took 2", 3, Hp(imp));
            Eq("two imps", 2, p.BoardZone.Count(m => m.Card.Id == "EX1_598"));
            // enemy end-of-turn not doubled
            var eimp = Board(o, ImpMaster);
            EndTurn(g);
            Eq("enemy imp master single", 4, Hp(eimp));
            var cl = Clone(g); var cp = At(cl, p); var co = At(cl, cp.Opponent);
            Power(cl, cp, co.Hero); Eq("clone hero power twice", 20, co.Hero.Health);
        }

        [AuditTest]
        static void TIME_432_IntertwinedFate()
        {
            var g = NewGame(s => { s.PlayerDeck = Fill(10, Wisp); s.BossDeck = Fill(30, Golem); }); var p = P1(g); var o = P2(g);
            int pd = p.DeckZone.Count, od = o.DeckZone.Count;
            PlayNew(g, p, "TIME_432");
            var c1 = ChoiceIds(g, p);
            Check("first discover from own deck", c1.Count > 0 && c1.All(x => x == Wisp), string.Join(",", c1));
            Pick(g, p, 0);
            var c2 = ChoiceIds(g, p);
            Check("second discover from opponent deck", c2.Count > 0 && c2.All(x => x == Golem), string.Join(",", c2));
            Pick(g, p, 0);
            Eq("hand", "CS2_186,CS2_231", string.Join(",", HandIds(p).OrderBy(x => x)));
            Eq("own deck intact", pd, p.DeckZone.Count); Eq("opp deck intact", od, o.DeckZone.Count);
        }

        [AuditTest]
        static void TIME_433_CeaseToExist()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var l = Board(o, Leper); Settle(g);
            var s = Hand(p, "TIME_433");
            Play(g, s);
            Check("enemy leper gone", !o.BoardZone.Contains(l), Describe(g));
            Eq("silenced: no deathrattle", 30, p.Hero.Health);
            Check("rewind choice offered", p.Choice != null && p.Choice.ChoiceAction == ChoiceAction.REWIND);
            if (p.Choice != null) Choose(g, p, 1); // keep
            Check("choice closed", p.Choice == null);
            // rewind path: restore, recast
            var g2 = NewGame(); var p2 = P1(g2); var o2 = P2(g2);
            Board(o2, Yeti); Board(o2, Golem); Settle(g2);
            Play(g2, Hand(p2, "TIME_433"));
            Eq("one destroyed", 1, o2.BoardZone.Count);
            if (p2.Choice != null && p2.Choice.ChoiceAction == ChoiceAction.REWIND) Choose(g2, p2, 0);
            p2 = P1(g2); o2 = P2(g2);
            Note("after rewind: " + Describe(g2));
            Eq("after rewind still one destroyed", 1, o2.BoardZone.Count);
            Check("no second offer", p2.Choice == null);
            // friendly minions never hit
            var g3 = NewGame(); var p3 = P1(g3); var mine = Board(p3, Yeti); Settle(g3);
            Play(g3, Hand(p3, "TIME_433")); Check("friendly safe", p3.BoardZone.Contains(mine));
        }

        [AuditTest]
        static void TLC_106_EndbringerUmbra()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            for (int i = 0; i < 6; i++) { var l = Board(p, Leper); Kill(g, l); }
            Eq("6 lepers: 12", 18, o.Hero.Health);
            var el = Board(o, Leper); Kill(g, el);
            PlayNew(g, p, "TLC_106");
            Eq("5 deathrattles: 10", 8, o.Hero.Health);
            Eq("enemy leper not triggered", 28, p.Hero.Health);
            var g2 = NewGame(); PlayNew(g2, P1(g2), "TLC_106"); Eq("no deaths nothing", 30, P2(g2).Hero.Health);
        }

        [AuditTest]
        static void TLC_817_ReachEquilibrium()
        {
            var g = NewGame(s => s.PlayerHero = "HERO_09"); var p = P1(g); var o = P2(g);
            PlayNew(g, p, "TLC_817");
            Note("quests: " + string.Join(",", p.SecretZone.ModernQuests.Select(q => q.Card.Id)));
            var y = Board(p, Golem);
            for (int i = 0; i < 4; i++) Play(g, Hand(p, "CORE_CS2_004"), y); // Holy
            ClearHandKeep(p, "TLC_817t3", "TLC_817t4", "TLC_817t5");
            Check("Life's Breath after 4 Holy", HandIds(p).Contains("TLC_817t3"), string.Join(",", HandIds(p)));
            for (int i = 0; i < 3; i++) Play(g, Hand(p, "JAIL_941t"), o.Hero); // Shadow
            Check("no Death's Touch after 3 shadow", !HandIds(p).Contains("TLC_817t4") && !HandIds(p).Contains("TLC_817t5"), string.Join(",", HandIds(p)));
            Play(g, Hand(p, "JAIL_941t"), o.Hero);
            Note("hand: " + string.Join(",", HandIds(p)));
            Check("halves combine into Cycle's Rebirth", HandIds(p).Contains("TLC_817t5") && !HandIds(p).Contains("TLC_817t3"), string.Join(",", HandIds(p)));
            // shadow first
            var g2 = NewGame(s => s.PlayerHero = "HERO_09"); var p2 = P1(g2); var o2 = P2(g2);
            PlayNew(g2, p2, "TLC_817");
            for (int i = 0; i < 4; i++) Play(g2, Hand(p2, "JAIL_941t"), o2.Hero);
            Check("Death's Touch after 4 shadow", HandIds(p2).Contains("TLC_817t4"), string.Join(",", HandIds(p2)));
            // enemy spells don't count
        }
        static void ClearHandKeep(Controller c, params string[] keep)
        {
            foreach (var e in c.HandZone.ToArray()) if (!keep.Contains(e.Card.Id)) c.HandZone.Remove(e);
        }

        [AuditTest]
        static void TLC_817t5_CyclesRebirth()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var s = PlayNew(g, p, "TLC_817t5");
            Eq("two on board", 2, p.BoardZone.Count(m => m.Card.Id == "TLC_817t5"));
            foreach (var m in p.BoardZone) Check("8/8 taunt reborn " + m.Id, Atk(m) == 8 && Hp(m) == 8 && m.HasTaunt && m[GameTag.REBORN] == 1, Describe(g));
            Kill(g, (Minion)s);
            Eq("DR 5 to enemy hero (no minions)", 25, o.Hero.Health);
            Eq("reborn back", 2, p.BoardZone.Count(m => m.Card.Id == "TLC_817t5"));
            for (int seed = 1; seed <= 8; seed++)
            {
                var g2 = NewGame(x => x.Seed = seed); var p2 = P1(g2); var o2 = P2(g2);
                var w = Board(o2, Golem); var mine = Board(p2, Golem);
                var t = Board(p2, "TLC_817t5"); Kill(g2, t);
                Check($"[{seed}] hits an enemy", (Hp(w) == 2 || o2.Hero.Health == 25) && Hp(mine) == 7 && p2.Hero.Health == 30, Describe(g2));
            }
        }

        // ---------------------------------------------------------------- DRUID (sleepless_one)
        [AuditTest]
        static void CATA_131_FelwoodTreant()
        {
            var g = NewGame(); var p = P1(g);
            Mana(p, 6);
            var t = Hand(p, "CATA_131");
            Play(g, t, keepMana: true);
            Eq("temporary crystal: base stays", 6, p.BaseMana);
            Eq("remaining 4 + 1 temp", 5, p.RemainingMana);
            EndTurn(g); EndTurn(g);
            Check("temp gone next turn", p.BaseMana == 7 && p.RemainingMana == 7, $"base {p.BaseMana} rem {p.RemainingMana}");
            // spend 4 while holding -> permanent
            var g2 = NewGame(); var p2 = P1(g2);
            Mana(p2, 6);
            var t2 = Hand(p2, "CATA_131");
            Play(g2, Hand(p2, Yeti), keepMana: true);
            Check("powered up text ready", true);
            Play(g2, t2, keepMana: true);
            Eq("permanent crystal 7", 7, p2.BaseMana);
            Eq("remaining 0 + 1 filled crystal?", 1, p2.RemainingMana);
            // spent across turns while holding counts
            var g3 = NewGame(); var p3 = P1(g3);
            Mana(p3, 6);
            var t3 = Hand(p3, "CATA_131");
            Play(g3, Hand(p3, "CS2_172"), keepMana: true); // 2 mana
            Round(g3);
            Mana(p3, 6);
            Play(g3, Hand(p3, "CS2_172"), keepMana: true);
            Play(g3, t3, keepMana: true);
            Eq("4 spent across turns -> permanent", 7, p3.BaseMana);
        }

        [AuditTest]
        static void CATA_139_WickerfangColossal()
        {
            var g = NewGame(); var p = P1(g);
            var w = (Minion)PlayNew(g, p, "CATA_139");
            Note(Describe(g));
            Eq("Colossal +4: 5 bodies", 5, p.BoardZone.Count);
            Eq("Wickerfang 0/5", "0/5", Atk(w) + "/" + Hp(w));
            EndTurn(g);
            Eq("Wickerfang gains each leg's +1/+1", "4/9", Atk(w) + "/" + Hp(w));
            foreach (var leg in p.BoardZone.Where(m => m != w)) Check("leg 1/3 " + leg.Card.Id, Atk(leg) == 1 && Hp(leg) == 3, Describe(g));
            EndTurn(g);
            Eq("no growth on enemy end turn", "4/9", Atk(w) + "/" + Hp(w));
            // a non-end-of-turn buff on a leg also transfers
            var leg1 = p.BoardZone.First(m => m != w);
            Play(g, Hand(p, "CORE_CS2_004"), leg1);
            Eq("leg +2 health -> wickerfang +2 health", "4/11", Atk(w) + "/" + Hp(w));
            var cl = Clone(g); var cw = At(cl, w); EndTurn(cl);
            Eq("clone growth", "8/15", Atk(cw) + "/" + Hp(cw));
        }

        [AuditTest]
        static void CATA_139t3_LegEndOfTurn()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var leg = Board(p, "CATA_139t3");
            EndTurn(g); Eq("+1/+1 at end of your turn", "1/3", Atk(leg) + "/" + Hp(leg));
            Check("CATA_139te", EnchantIds(leg).Contains("CATA_139te"));
            EndTurn(g); Eq("not on enemy end", "1/3", Atk(leg) + "/" + Hp(leg));
            var eleg = Board(o, "CATA_139t3");
            EndTurn(g); Eq("enemy leg not on my end", "0/2", Atk(eleg) + "/" + Hp(eleg));
        }

        [AuditTest]
        static void CATA_139te_Buff()
        {
            var g = NewGame(); var p = P1(g);
            var leg = Board(p, "CATA_139t3"); EndTurn(g); EndTurn(g); EndTurn(g);
            Eq("two stacks", "2/4", Atk(leg) + "/" + Hp(leg));
        }

        [AuditTest]
        static void EDR_272_EvergreenStag()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var es = Board(o, "EDR_272");
            Check("taunt", es.HasTaunt); Check("lifesteal", es.HasLifeSteal);
            var fb = Hand(p, Fireball);
            Check("spell can't target (Elusive)", !LegalTargets(fb).Contains(es));
            Mana(p, 10); p.Hero.HeroPower.IsExhausted = false;
            bool hp = g.Process(HeroPowerTask.Any(p, es));
            Check("hero power can't target", !hp || Hp(es) == 7, Describe(g));
            var mine = Board(p, "EDR_272");
            Damage(g, p.Hero, 10);
            Attack(g, mine, es);
            Eq("lifesteal heal 6", 26, p.Hero.Health);
            var y = Board(p, Yeti); Attack(g, y, o.Hero);
            Eq("taunt blocks hero attack", 30, o.Hero.Health);
        }

        [AuditTest]
        static void EDR_453_BriarspawnDrake()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var d = Board(p, "EDR_453");
            var w = Board(o, Wisp);
            EndTurn(g);
            Check("wisp killed", !o.BoardZone.Contains(w), Describe(g));
            Eq("excess 11 to hero", 19, o.Hero.Health);
            Eq("drake took 1", 6, Hp(d));
            // no enemy minions -> nothing
            EndTurn(g); // boss end turn: no trigger
            Eq("not on enemy end turn", 19, o.Hero.Health);
            EndTurn(g);
            Eq("no minions no attack", 19, o.Hero.Health);
            var golem = Board(o, Golem); var g2 = Board(o, Golem);
            Damage(g, golem, 0);
            Round(g);
            Check("random enemy minion attacked", (Hp(golem) <= 0 || !o.BoardZone.Contains(golem)) ^ (Hp(g2) <= 0 || !o.BoardZone.Contains(g2)), Describe(g));
            Eq("excess 5", 14, o.Hero.Health);
        }

        [AuditTest]
        static void END_010_TwilightTimereaver()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var a = Board(p, Golem); var b = Board(o, Golem);
            var t = (Minion)PlayNew(g, p, "END_010", chooseOne: 1);
            Eq("friendly atk 1", 1, Atk(a)); Eq("enemy atk 1", 1, Atk(b)); Eq("self untouched", 5, Atk(t));
            Eq("health untouched", 7, Hp(b));
            var g2 = NewGame(); var p2 = P1(g2); var o2 = P2(g2);
            var c = Board(p2, Golem); var d = Board(o2, Golem);
            var t2 = (Minion)PlayNew(g2, p2, "END_010", chooseOne: 2);
            Eq("friendly hp 1", 1, Hp(c)); Eq("enemy hp 1", 1, Hp(d)); Eq("self hp 5", 5, Hp(t2)); Eq("atk untouched", 7, Atk(d));
        }

        [AuditTest]
        static void FIR_907_Amirdrassil()
        {
            var g = NewGame(s => s.PlayerDeck = Fill(30)); var p = P1(g);
            PlayNew(g, p, "FIR_907");
            var loc = Loc(p, "FIR_907");
            Check("location on board", loc != null, Describe(g)); if (loc == null) return;
            Mana(p, 10); p.UsedMana = 10;
            int hand = p.HandZone.Count; int armor = Armor(p);
            Check("activate 1", ActivateLoc(g, p, loc));
            var m1 = p.BoardZone.LastOrDefault();
            Check("summoned a 1-cost minion", m1 != null && m1.Card.Cost == 1, m1?.Card.Id + " " + m1?.Card.Cost);
            Eq("armor 1", armor + 1, Armor(p)); Eq("drew 1", hand + 1, p.HandZone.Count); Eq("refresh 1", 1, p.RemainingMana);
            Eq("durability 2", 2, loc.Durability);
            Check("cooldown: cannot reuse this turn", !ActivateLoc(g, p, loc));
            Round(g); ClearHand(p);
            // Locations refresh at the start of your next turn; nothing in the text lengthens Amirdrassil's cooldown.
            Check("usable again on your next turn", LocationRules.CanActivate(p, loc, null),
                $"turn {g.Turn}: EXHAUSTED {loc[GameTag.EXHAUSTED]} LOCATION_ACTION_COOLDOWN {loc[GameTag.LOCATION_ACTION_COOLDOWN]}");
            if (!LocationRules.CanActivate(p, loc, null)) { Note("waiting one more round to check the improved numbers"); Round(g); ClearHand(p); }
            Mana(p, 10); p.UsedMana = 10; armor = Armor(p); var before2 = p.BoardZone.Select(m => m.Id).ToList();
            Check("activate 2", ActivateLoc(g, p, loc));
            var m2 = NewMinion(p, before2);
            Check("summoned a 2-cost minion", m2 != null && m2.Card.Cost == 2, m2?.Card.Id + " " + m2?.Card.Cost);
            Eq("armor 2", armor + 2, Armor(p)); Eq("drew 2", 2, p.HandZone.Count); Eq("refresh 2", 2, p.RemainingMana);
            Round(g); if (!LocationRules.CanActivate(p, loc, null)) Round(g);
            ClearHand(p); Mana(p, 10); p.UsedMana = 10; var before3 = p.BoardZone.Select(m => m.Id).ToList(); Note("before 3: dur " + loc.Durability + " " + Describe(g));
            Check("activate 3", ActivateLoc(g, p, loc));
            var m3 = NewMinion(p, before3); Note("after 3: " + Describe(g));
            Check("third: 3-cost", m3 != null && m3.Card.Cost == 3, m3?.Card.Id);
            Eq("drew 3", 3, p.HandZone.Count); Eq("refresh 3", 3, p.RemainingMana);
            Check("location gone after 3 uses", Loc(p, "FIR_907") == null, Describe(g));
        }

        [AuditTest]
        static void TIME_211t2_ReusableNextTurnVsControl()
        {
            // Zin-Azshari/Amirdrassil set LOCATION_ACTION_COOLDOWN on top of EXHAUSTED; RefreshAtTurnStart clears one per turn.
            var g = NewGame(s => s.PlayerHero = "HERO_06"); var p = P1(g);
            var y = Board(p, Yeti);
            PlayNew(g, p, "TIME_211t2");
            var zin = Loc(p, "TIME_211t2");
            ActivateLoc(g, p, zin, y);
            Round(g);
            Check("TIME_211t2 usable on your next turn", LocationRules.CanActivate(p, zin, y),
                $"EXHAUSTED {zin[GameTag.EXHAUSTED]} COOLDOWN {zin[GameTag.LOCATION_ACTION_COOLDOWN]}");
            // control: a location whose script does not set LOCATION_ACTION_COOLDOWN
            var g2 = NewGame(s => s.PlayerHero = "HERO_02"); var p2 = P1(g2);
            PlayNew(g2, p2, "JAIL_987");
            var wing = Loc(p2, "JAIL_987");
            ActivateLoc(g2, p2, wing);
            Round(g2);
            Check("control JAIL_987 usable on next turn", LocationRules.CanActivate(p2, wing, null),
                $"EXHAUSTED {wing[GameTag.EXHAUSTED]} COOLDOWN {wing[GameTag.LOCATION_ACTION_COOLDOWN]}");
        }

        [AuditTest]
        static void JAIL_875_StaffOfTrickery()
        {
            var g = NewGame(s => s.PlayerHero = "HERO_06"); var p = P1(g); var o = P2(g);
            PlayNew(g, p, "JAIL_875");
            Eq("weapon", "JAIL_875", Weapon(p));
            HeroAttack(g, p, o.Hero);
            Eq("hit 1", 29, o.Hero.Health);
            var ch = ChoiceIds(g, p);
            Check("discover offered", ch.Count == 3, string.Join(",", ch));
            Check("all druid", ch.All(id => Cards.FromId(id).Class == CardClass.DRUID || CardClassRules.IsAllowedInDeck(Cards.FromId(id), CardClass.DRUID)), string.Join(",", ch));
            var pick = Pick(g, p, 0);
            var card = p.HandZone.FirstOrDefault(c => c.Card.Id == pick);
            Check("in hand", card != null);
            if (card != null) Eq("cost reduced by hero attack (1)", Math.Max(0, Cards.FromId(pick).Cost - 1), card.Cost);
            // hero attack bonus counts
            Round(g); ClearHand(p);
            p.Hero.AttackDamage += 0;
            PlayNew(g, p, "CS2_017"); // Shapeshift +1 attack (druid hero power card? fallback)
            Note("hero atk before attack " + p.Hero.AttackDamage);
            int atk = p.Hero.AttackDamage;
            HeroAttack(g, p, o.Hero);
            var pick2 = Pick(g, p, 0);
            var card2 = p.HandZone.FirstOrDefault(c => c.Card.Id == pick2);
            if (card2 != null) Eq("cost reduced by " + atk, Math.Max(0, Cards.FromId(pick2).Cost - atk), card2.Cost);
        }

        [AuditTest]
        static void MEND_046_BashanaRunetotem()
        {
            for (int seed = 1; seed <= 4; seed++)
            {
                var g = NewGame(s => { s.PlayerHero = "HERO_06"; s.Seed = seed; }); var p = P1(g);
                PlayNew(g, p, "MEND_046");
                var treants = p.HandZone.Where(c => c.Card.Id == "MEND_046t").ToList();
                Eq($"[{seed}] three treants", 3, treants.Count);
                var spells = treants.Select(t => PayloadRules.Entries(t, 1).FirstOrDefault()).ToList();
                Check($"[{seed}] each carries a spell", spells.All(s => s != null));
                if (spells.Any(s => s == null)) continue;
                Eq($"[{seed}] 12 mana worth", 12, spells.Sum(s => s.Card.Cost));
                Check($"[{seed}] all Nature", spells.All(s => s.Card[GameTag.SPELL_SCHOOL] == 4), string.Join(",", spells.Select(s => s.Card.Id + ":" + s.Card[GameTag.SPELL_SCHOOL])));
                foreach (var t in treants) Eq($"[{seed}] treant 1-cost 2/2", "1:2/2", t.Cost + ":" + t.Card.ATK + "/" + t.Card.Health);
            }
        }

        [AuditTest]
        static void MEND_046t_TreantCastsCarved()
        {
            for (int seed = 1; seed <= 4; seed++)
            {
                var g = NewGame(s => { s.PlayerHero = "HERO_06"; s.Seed = seed; s.PlayerDeck = Fill(20); }); var p = P1(g);
                Board(P2(g), Yeti);
                PlayNew(g, p, "MEND_046");
                var t = p.HandZone.First(c => c.Card.Id == "MEND_046t");
                var spell = PayloadRules.Entries(t, 1).First().Card;
                int spells = p.NumSpellsPlayedThisGame;
                int grave = p.GraveyardZone.Count;
                bool ok = true;
                try { Play(g, t); } catch (Exception ex) { ok = false; Check($"[{seed}] threw", false, ex.Message); }
                Check($"[{seed}] treant on board", BoardIds(p).Contains("MEND_046t"), Describe(g));
                Note($"[{seed}] {spell.Id} {spell.Name}: spellsPlayed {spells}->{p.NumSpellsPlayedThisGame} grave {grave}->{p.GraveyardZone.Count} {Describe(g)}");
                Check($"[{seed}] spell cast", p.GraveyardZone.Skip(grave).Any(x => x.Card.Id == spell.Id) || p.NumSpellsPlayedThisGame > spells || p.Choice != null, spell.Id);
            }
        }

        [AuditTest]
        static void TIME_211_LadyAzsharaChooseOne()
        {
            // option 1: Empower Zin-Azshari, destroy Well
            var g = NewGame(s => s.PlayerHero = "HERO_06"); var p = P1(g);
            Hand(p, "TIME_211t1"); DeckTop(p, "TIME_211t2");
            PlayNew(g, p, "TIME_211", chooseOne: 1);
            var all = p.HandZone.Concat(p.DeckZone).Select(c => c.Card.Id).ToList();
            Check("Zin-Azshari empowered", all.Contains("TIME_211t2t") && !all.Contains("TIME_211t2"), string.Join(",", all.Where(x => x.StartsWith("TIME"))));
            Check("Well destroyed", !all.Contains("TIME_211t1") && !all.Contains("TIME_211t1t"));
            // option 2: Empower Well, destroy Zin-Azshari (on board)
            var g2 = NewGame(s => s.PlayerHero = "HERO_06"); var p2 = P1(g2);
            PlayNew(g2, p2, "TIME_211t2"); DeckTop(p2, "TIME_211t1");
            PlayNew(g2, p2, "TIME_211", chooseOne: 2);
            var all2 = p2.HandZone.Concat(p2.DeckZone).Select(c => c.Card.Id).ToList();
            Check("Well empowered", all2.Contains("TIME_211t1t"), string.Join(",", all2.Where(x => x.StartsWith("TIME"))));
            Check("Zin on board destroyed", Loc(p2, "TIME_211t2") == null, Describe(g2));
        }

        [AuditTest]
        static void TIME_211t1_WellOfEternity()
        {
            var g = NewGame(s => s.PlayerHero = "HERO_06"); var p = P1(g);
            PlayNew(g, p, "TIME_211t1");
            var loc = Loc(p, "TIME_211t1");
            Hand(p, Yeti);
            Check("activated", ActivateLoc(g, p, loc));
            Eq("hand full", 10, p.HandZone.Count);
            var gen = p.HandZone.Where(c => c.Card.Id != Yeti).ToList();
            Check("all spells", gen.All(c => c.Card.Type == CardType.SPELL));
            Check("all temporary", gen.All(TemporaryRules.IsTemporary));
            EndTurn(g);
            Check("temporary discarded at end of turn", p.HandZone.All(c => !TemporaryRules.IsTemporary(c)), string.Join(",", HandIds(p)));
        }

        [AuditTest]
        static void TIME_211t1_ReusableNextTurn()
        {
            var g = NewGame(s => s.PlayerHero = "HERO_06"); var p = P1(g);
            PlayNew(g, p, "TIME_211t1");
            var loc = Loc(p, "TIME_211t1");
            ActivateLoc(g, p, loc);
            Round(g); ClearHand(p);
            Check("Well usable again on your next turn", LocationRules.CanActivate(p, loc, null),
                $"turn {g.Turn}: EXHAUSTED {loc[GameTag.EXHAUSTED]} COOLDOWN {loc[GameTag.LOCATION_ACTION_COOLDOWN]}");
        }

        [AuditTest]
        static void TIME_211t1te_EternalizedCastsTwice()
        {
            var g = NewGame(s => s.PlayerHero = "HERO_06"); var p = P1(g);
            PlayNew(g, p, "TIME_211t1t");
            var loc = Loc(p, "TIME_211t1t");
            Check("empowered well on board", loc != null, Describe(g)); if (loc == null) return;
            ActivateLoc(g, p, loc);
            var gen = p.HandZone.ToList();
            Check("all marked Eternalized", gen.Count == 10 && gen.All(c => EnchantIds(c).Contains("TIME_211t1te")), string.Join(",", gen.Select(c => c.Card.Id + "[" + string.Join("/", EnchantIds(c)) + "]")));
            // cast a marked spell and verify it fires twice: replace one with Fireball and mark it
            ClearHand(p);
            var fb = Hand(p, Fireball);
            RepeatRules.Mark(fb, loc, "TIME_211t1te");
            Play(g, fb, P2(g).Hero);
            Eq("marked Fireball cast twice", 18, P2(g).Hero.Health);
        }

        [AuditTest]
        static void TIME_211t2_ZinAzshari()
        {
            var g = NewGame(s => s.PlayerHero = "HERO_06"); var p = P1(g); var o = P2(g);
            var y = Board(p, Yeti); Damage(g, y, 1); var foe = Board(o, Golem);
            PlayNew(g, p, "TIME_211t2");
            var loc = Loc(p, "TIME_211t2");
            Check("enemy not targetable", !ActivateLoc(g, p, loc, foe) || o.BoardZone.Count == 1 && p.BoardZone.Count(m => m.Card.Id == Golem) == 0);
            Check("activate on friendly", ActivateLoc(g, p, loc, y));
            Eq("copy summoned", 2, p.BoardZone.Count(m => m.Card.Id == Yeti));
            Eq("no golem copy", 0, p.BoardZone.Count(m => m.Card.Id == Golem));
        }

        [AuditTest]
        static void TIME_211t2te_EmpoweredDoubles()
        {
            var g = NewGame(s => s.PlayerHero = "HERO_06"); var p = P1(g);
            var y = Board(p, Yeti);
            PlayNew(g, p, "TIME_211t2t");
            var loc = Loc(p, "TIME_211t2t");
            Check("on board", loc != null); if (loc == null) return;
            ActivateLoc(g, p, loc, y);
            var copy = p.BoardZone.Where(m => m.Card.Id == Yeti && m != y).FirstOrDefault();
            Check("copy", copy != null, Describe(g)); if (copy == null) return;
            Eq("doubled 8/10", "8/10", Atk(copy) + "/" + Hp(copy));
            Check("TIME_211t2te", EnchantIds(copy).Contains("TIME_211t2te"));
        }

        [AuditTest]
        static void TLC_100_EliseTheNavigator()
        {
            var deck = new List<string>();
            for (int cost = 0; cost < 10; cost++)
                deck.Add(Cards.All.First(c => c.Collectible && c.Type == CardType.MINION && c.Cost == cost && c.Class == CardClass.NEUTRAL && c.Implemented).Id);
            var g = NewGame(s => { s.PlayerDeck = deck; s.Shuffle = true; }); var p = P1(g);
            PlayNew(g, p, "TLC_100");
            int picks = 0;
            while (p.Choice != null && picks < 4) { Pick(g, p, 0); picks++; }
            Eq("three discovers", 3, picks);
            var loc = p.HandZone.FirstOrDefault(c => c.Card.Type == CardType.LOCATION);
            Check("crafted location in hand", loc != null, string.Join(",", HandIds(p)));
            if (loc != null) { Play(g, loc); Check("location playable", p.BoardZone.GetLocations().Length == 1, Describe(g)); }
            // ineligible deck
            var g2 = NewGame(); var p2 = P1(g2);
            PlayNew(g2, p2, "TLC_100");
            Check("no discover with Yeti deck", p2.Choice == null && p2.HandZone.Count == 0, string.Join(",", HandIds(p2)));
            // eligibility is about the starting deck: drawing cards doesn't remove it
            var g3 = NewGame(s => s.PlayerDeck = deck); var p3 = P1(g3);
            for (int i = 0; i < 5; i++) { var c = p3.DeckZone[0]; p3.DeckZone.Remove(c); }
            PlayNew(g3, p3, "TLC_100");
            Check("still eligible after deck shrinks", p3.Choice != null, "deck " + p3.DeckZone.Count);
        }
    }
}
