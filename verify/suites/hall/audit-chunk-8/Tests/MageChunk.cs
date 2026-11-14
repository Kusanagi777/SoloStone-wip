// Audit chunk class-MAGE: one or more tests per card, named <CardId>_<Behaviour>.
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
    internal static class MageChunk
    {
        const string Frostbolt = "CS2_024";
        const string Geomancer = "CS2_142";       // Kobold Geomancer, Spell Damage +1
        const string FlameElemental = "UNG_809t1"; // 1/2 Elemental
        const string Intellect = "CORE_CS2_023";

        static Card C(string id) => Cards.FromId(id);
        static Dictionary<string, string> _schools;
        // Card data often lacks SPELL_SCHOOL for old cards; the offline pool catalog carries it.
        static string School(Card c)
        {
            if (_schools == null)
            {
                _schools = new Dictionary<string, string>();
                var doc = System.Xml.Linq.XDocument.Load(VerifyPaths.Repo("engine/SabberStoneCore/resources/Data/OfflinePoolCatalog.xml"));
                foreach (var e in doc.Descendants("Card"))
                {
                    var id = (string)e.Attribute("cardId"); var sc = (string)e.Attribute("spellSchool");
                    if (id != null && sc != null) _schools[id] = sc;
                }
            }
            return c[GameTag.SPELL_SCHOOL] == 1 ? "ARCANE" : _schools.TryGetValue(c.Id, out var s) ? s : "?";
        }
        /// <summary>Answers either a legacy choice or a suspended-effect decision.</summary>
        static string Pick(Game g, Controller c, int index = 0)
        {
            if (c.Choice == null || index < 0 || index >= c.Choice.Choices.Count) return null;
            int id = c.Choice.Choices[index]; string cardId = g.IdEntityDic[id].Card.Id;
            if (g.Resolution.HasSuspendedEffect)
            {
                if (!g.TryAnswer(new SabberStoneCore.Model.Decisions.DecisionAnswer(c.Choice.DecisionId, c.Id, new[] { id }), out string err))
                    Check("answer accepted", false, err);
            }
            else g.Process(SabberStoneCore.Tasks.PlayerTasks.ChooseTask.Pick(c, id));
            return cardId;
        }
        static void NoteText(params string[] ids) { foreach (var i in ids) Note(CardText(i)); }
        static Game G(Action<Setup> t = null) => NewGame(t);

        // ---------------------------------------------------------------- CATA_483 Unstable Spellcaster
        [AuditTest]
        static void CATA_483_NoSpellDamage_NoCopy()
        {
            NoteText("CATA_483");
            var g = G(); var p = P1(g);
            PlayNew(g, p, "CATA_483");
            Eq("one Spellcaster", 1, BoardIds(p).Count(x => x == "CATA_483"));
            var m = (Minion)p.BoardZone[0];
            Eq("Spell Damage +1", 1, m.SpellPower);
            // spell damage only via non-damaging spell this turn
            var g2 = G(); var p2 = P1(g2);
            PlayNew(g2, p2, Intellect);
            PlayNew(g2, p2, "CATA_483");
            Eq("non-damaging spell: no copy", 1, BoardIds(p2).Count(x => x == "CATA_483"));
        }

        [AuditTest]
        static void CATA_483_AfterSpellDamage_SummonsCopy()
        {
            var g = G(); var p = P1(g); var o = P2(g);
            PlayNew(g, p, Frostbolt, o.Hero);
            Eq("frostbolt hit face", 27, o.Hero.Health);
            PlayNew(g, p, "CATA_483");
            Eq("two Spellcasters", 2, BoardIds(p).Count(x => x == "CATA_483"));
            Eq("total spell damage 2", 2, p.CurrentSpellPower);
            PlayNew(g, p, Frostbolt, o.Hero);
            Eq("frostbolt now 5", 22, o.Hero.Health);
            var c = Clone(g); Eq("clone keeps 2", 2, BoardIds(At(c, p)).Count(x => x == "CATA_483"));
        }

        [AuditTest]
        static void CATA_483_LastTurnSpellDamage_NoCopy()
        {
            var g = G(); var p = P1(g); var o = P2(g);
            PlayNew(g, p, Frostbolt, o.Hero);
            Round(g); ClearHand(p);
            PlayNew(g, p, "CATA_483");
            Eq("spell damage last turn: no copy", 1, BoardIds(p).Count(x => x == "CATA_483"));
        }

        // ---------------------------------------------------------------- CATA_484 Winterspring Whelp
        [AuditTest]
        static void CATA_484_DiscoverOneCostSpellAnyClass()
        {
            NoteText("CATA_484");
            var classes = new HashSet<CardClass>();
            for (int seed = 1; seed <= 6; seed++)
            {
                var g = G(s => s.Seed = seed); var p = P1(g);
                PlayNew(g, p, "CATA_484");
                var ch = ChoiceIds(g, p);
                Eq($"seed {seed}: 3 options", 3, ch.Count);
                Check($"seed {seed}: all 1-cost spells", ch.All(i => C(i).Type == CardType.SPELL && C(i).Cost == 1), string.Join(",", ch));
                Eq($"seed {seed}: distinct", ch.Count, ch.Distinct().Count());
                foreach (var i in ch) classes.Add(C(i).Class);
                var pick = Pick(g, p, 0);
                Check($"seed {seed}: picked in hand", HandIds(p).Contains(pick), string.Join(",", HandIds(p)));
                Eq($"seed {seed}: on board", 1, p.BoardZone.Count);
            }
            Note("classes seen: " + string.Join(",", classes));
            Check("any class: more than mage+neutral seen", classes.Any(c => c != CardClass.MAGE && c != CardClass.NEUTRAL), string.Join(",", classes));
        }

        // ---------------------------------------------------------------- TIME_858 Temporal Construct
        [AuditTest]
        static void TIME_858_ExcessDamageDraws()
        {
            NoteText("TIME_858");
            var g = G(); var p = P1(g); var o = P2(g);
            var wisp = Board(o, Wisp);
            var tc = Hand(p, "TIME_858");
            var legal = LegalTargets(tc);
            Check("only enemy minions targetable", legal.All(x => x.Controller == o && x is Minion) && legal.Count == 1, string.Join(",", legal.Select(x => x.Card.Id + "@" + (x.Controller == o ? "o" : "p"))));
            int hand = p.HandZone.Count - 1;
            Play(g, tc, wisp);
            Check("wisp dead", wisp.IsDead || !o.BoardZone.Contains(wisp), Describe(g));
            Eq("drew 4 (excess)", hand + 4, p.HandZone.Count);
        }

        [AuditTest]
        static void TIME_858_NoExcess_NoDraw()
        {
            var g = G(); var p = P1(g); var o = P2(g);
            var yeti = Board(o, Yeti);
            Board(p, Wisp);
            var tc = Hand(p, "TIME_858");
            Check("friendly minion not targetable", !CanPlay(tc, p.BoardZone[0]), "");
            Check("enemy hero not targetable", !CanPlay(tc, o.Hero), "");
            int hand = p.HandZone.Count - 1;
            Play(g, tc, yeti);
            Check("yeti dead (5 dmg)", !o.BoardZone.Contains(yeti), Describe(g));
            Eq("drew 0", hand, p.HandZone.Count);
            var g2 = G(); var p2 = P1(g2); var o2 = P2(g2);
            var golem = Board(o2, Golem);
            var tc2 = Hand(p2, "TIME_858"); int h2 = p2.HandZone.Count - 1;
            Play(g2, tc2, golem);
            Eq("golem at 2", 2, Hp(golem));
            Eq("no draw on 7/7", h2, p2.HandZone.Count);
            var g3 = G(); var p3 = P1(g3);
            var tc3 = Hand(p3, "TIME_858");
            Check("playable with no enemy minion", CanPlay(tc3), "");
        }

        // ---------------------------------------------------------------- DAL_182 Magic Dart Frog
        [AuditTest]
        static void DAL_182_AfterSpellPingsEnemyMinion()
        {
            NoteText("DAL_182");
            for (int seed = 1; seed <= 5; seed++)
            {
                var g = G(s => s.Seed = seed); var p = P1(g); var o = P2(g);
                Board(p, "DAL_182"); var mine = Board(p, Yeti);
                var a = Board(o, Yeti); var b = Board(o, Yeti);
                PlayNew(g, p, Intellect);
                Eq($"seed {seed}: exactly 1 dmg on enemy minions", 1, (5 - Hp(a)) + (5 - Hp(b)));
                Eq($"seed {seed}: enemy hero untouched", 30, o.Hero.Health);
                Eq($"seed {seed}: own minion untouched", 5, Hp(mine));
            }
            // no enemy minion: hero untouched
            var g2 = G(); var p2 = P1(g2); var o2 = P2(g2);
            Board(p2, "DAL_182"); PlayNew(g2, p2, Intellect);
            Eq("no enemy minion: hero untouched", 30, o2.Hero.Health);
            // opponent's spell doesn't trigger
            var g3 = G(); var p3 = P1(g3); var o3 = P2(g3);
            Board(p3, "DAL_182"); EndTurn(g3); ClearHand(o3);
            var target = Board(p3, Yeti);
            var enemyYeti = Board(o3, Yeti);
            PlayNew(g3, o3, Intellect);
            Eq("opponent spell: no ping on their minion", 5, Hp(enemyYeti));
        }

        // ---------------------------------------------------------------- GIL_664 Vex Crow
        [AuditTest]
        static void GIL_664_SpellSummonsTwoCost()
        {
            NoteText("GIL_664");
            for (int seed = 1; seed <= 5; seed++)
            {
                var g = G(s => s.Seed = seed); var p = P1(g);
                Board(p, "GIL_664");
                PlayNew(g, p, Intellect);
                Eq($"seed {seed}: one minion summoned", 2, p.BoardZone.Count);
                if (p.BoardZone.Count == 2) Eq($"seed {seed}: 2-cost", 2, p.BoardZone[1].Card.Cost);
            }
            var g2 = G(); var p2 = P1(g2); var o2 = P2(g2);
            Board(p2, "GIL_664"); EndTurn(g2); ClearHand(o2);
            PlayNew(g2, o2, Intellect);
            Eq("opponent spell: no summon", 1, p2.BoardZone.Count);
            var g3 = G(); var p3 = P1(g3);
            Board(p3, "GIL_664"); PlayNew(g3, p3, Wisp);
            Eq("minion play: no summon", 2, p3.BoardZone.Count);
        }

        // ---------------------------------------------------------------- MEND_506 Mystic Runesaber
        [AuditTest]
        static void MEND_506_ElusiveAndLeylineBoost()
        {
            NoteText("MEND_506", "MEND_500", "MEND_502", "MEND_504");
            var g = G(); var p = P1(g); var o = P2(g);
            var ley = Hand(p, "MEND_500");
            int before = LeylineRules.Magnitude(ley);
            var rs = (Minion)PlayNew(g, p, "MEND_506");
            Check("Elusive: can't be targeted by spells", rs.CantBeTargetedBySpells, "");
            Check("Elusive: can't be targeted by hero powers", rs.CantBeTargetedByHeroPowers, "");
            Eq("leyline magnitude +1", before + 1, LeylineRules.Magnitude(ley));
            var fb = Hand(p, Frostbolt);
            Check("own Frostbolt can't target it", !CanPlay(fb, rs), "");
            // persists after death ("this game")
            Kill(g, rs);
            Eq("still +1 after death", before + 1, LeylineRules.Magnitude(ley));
            var ley2 = Hand(p, "MEND_502");
            Eq("other leylines too", C("MEND_502")[GameTag.TAG_SCRIPT_DATA_NUM_1] + 1, LeylineRules.Magnitude(ley2));
            var c = Clone(g); Eq("clone keeps boost", before + 1, LeylineRules.Magnitude(At(c, ley)));
            Note("MEND_500 base " + before + " -> " + LeylineRules.Magnitude(ley));
        }

        [AuditTest]
        static void MEND_506_LeylineDealsMore()
        {
            // Bursting Leyline: play it with and without Runesaber and compare total enemy damage.
            int Run(bool boost)
            {
                var g = G(s => s.Seed = 5); var p = P1(g); var o = P2(g);
                if (boost) PlayNew(g, p, "MEND_506");
                Board(o, Wisp);
                var ley = Hand(p, "MEND_500");
                Play(g, ley, null);
                while (p.Choice != null) Pick(g, p, 0);
                int dmg = 30 - o.Hero.Health;
                Note($"boost={boost}: {Describe(g)}");
                return dmg + p.BoardZone.Sum(m => m.AttackDamage) * 0;
            }
            int a = Run(false), b = Run(true);
            Note($"enemy hero damage without/with Runesaber: {a}/{b}");
            Eq("Bursting Leyline excess: 4 dmg on a 1-Health wisp -> 3 to hero", 3, a);
            Eq("with Runesaber: 5 dmg -> 4 to hero", 4, b);
        }

        // ---------------------------------------------------------------- CORE_DRG_107 Violet Spellwing
        [AuditTest]
        static void CORE_DRG_107_DeathrattleAddsArcaneMissiles()
        {
            NoteText("CORE_DRG_107");
            var g = G(); var p = P1(g);
            var m = Board(p, "CORE_DRG_107");
            Kill(g, m);
            Eq("one card in hand", 1, p.HandZone.Count);
            Check("is Arcane Missiles", p.HandZone.Count == 1 && p.HandZone[0].Card.Name == "Arcane Missiles", string.Join(",", HandIds(p)));
        }

        // ---------------------------------------------------------------- EX1_277 Arcane Missiles
        [AuditTest]
        static void EX1_277_ThreeSplitAmongEnemies()
        {
            NoteText("EX1_277");
            bool hitHero = false, hitMinion = false;
            for (int seed = 1; seed <= 12; seed++)
            {
                var g = G(s => s.Seed = seed); var p = P1(g); var o = P2(g);
                var mine = Board(p, Golem); var a = Board(o, Golem); var b = Board(o, Golem);
                PlayNew(g, p, "EX1_277");
                int dh = 30 - o.Hero.Health, da = 7 - Hp(a), db = 7 - Hp(b);
                Eq($"seed {seed}: total 3", 3, dh + da + db);
                Eq($"seed {seed}: own minion untouched", 7, Hp(mine));
                Eq($"seed {seed}: own hero untouched", 30, p.Hero.Health);
                hitHero |= dh > 0; hitMinion |= da + db > 0;
            }
            Check("hits enemy hero sometimes", hitHero, ""); Check("hits enemy minions sometimes", hitMinion, "");
            var g2 = G(); var p2 = P1(g2); var o2 = P2(g2);
            Board(p2, Geomancer);
            PlayNew(g2, p2, "EX1_277");
            Eq("spell damage +1 -> 4 missiles", 26, o2.Hero.Health);
        }

        // ---------------------------------------------------------------- TLC_226 Conjured Bookkeeper
        [AuditTest]
        static void TLC_226_DeathrattleDrawsSpell()
        {
            NoteText("TLC_226");
            var g = G(); var p = P1(g);
            DeckTop(p, Frostbolt); DeckTop(p, Yeti);
            var m = (Minion)PlayNew(g, p, "TLC_226");
            Kill(g, m);
            Eq("drew the spell", "CS2_024", string.Join(",", HandIds(p)));
            Eq("no copy without Kindred", 0, BoardIds(p).Count(x => x == "TLC_226"));
        }

        [AuditTest]
        static void TLC_226_KindredSummonsCopy()
        {
            var g = G(); var p = P1(g);
            PlayNew(g, p, FlameElemental);
            Round(g); ClearHand(p);
            DeckTop(p, Frostbolt);
            var m = (Minion)PlayNew(g, p, "TLC_226");
            Note("enchants: " + string.Join(",", EnchantIds(m)));
            Kill(g, m);
            Check("drew a spell", HandIds(p).Contains("CS2_024"), string.Join(",", HandIds(p)));
            Eq("Kindred: copy summoned", 1, BoardIds(p).Count(x => x == "TLC_226"));
        }

        // ---------------------------------------------------------------- TRL_311 Arcanosaur
        [AuditTest]
        static void TRL_311_ElementalLastTurn_AoE3()
        {
            NoteText("TRL_311");
            var g = G(); var p = P1(g); var o = P2(g);
            PlayNew(g, p, FlameElemental);
            Round(g); ClearHand(p);
            var mine = Board(p, Yeti); var foe = Board(o, Yeti);
            var a = (Minion)PlayNew(g, p, "TRL_311");
            Eq("enemy minion -3", 2, Hp(foe));
            Eq("own minion -3", 2, Hp(mine));
            Eq("self untouched", 3, Hp(a));
            Eq("heroes untouched", 30, o.Hero.Health);
        }

        [AuditTest]
        static void TRL_311_NoElementalLastTurn_NoAoE()
        {
            var g = G(); var p = P1(g); var o = P2(g);
            var foe = Board(o, Yeti);
            PlayNew(g, p, FlameElemental); // this turn, not last
            PlayNew(g, p, "TRL_311");
            Eq("elemental this turn only: no AoE", 5, Hp(foe));
            var g2 = G(); var p2 = P1(g2); var o2 = P2(g2);
            PlayNew(g2, p2, FlameElemental); Round(g2); Round(g2); ClearHand(p2);
            var foe2 = Board(o2, Yeti);
            PlayNew(g2, p2, "TRL_311");
            Eq("elemental two turns ago: no AoE", 5, Hp(foe2));
        }

        // ---------------------------------------------------------------- AT_004 Arcane Blast
        [AuditTest]
        static void AT_004_DoubleSpellDamage()
        {
            NoteText("AT_004");
            var g = G(); var p = P1(g); var o = P2(g);
            var foe = Board(o, Golem);
            var ab = Hand(p, "AT_004");
            Check("hero not targetable", !CanPlay(ab, o.Hero), "");
            Play(g, ab, foe);
            Eq("2 damage", 5, Hp(foe));
            Board(p, Geomancer);
            PlayNew(g, p, "AT_004", foe);
            Eq("with SD+1: 4 damage", 1, Hp(foe));
        }

        // ---------------------------------------------------------------- CS2_026 Frost Nova
        [AuditTest]
        static void CS2_026_FreezesEnemyMinions()
        {
            NoteText("CS2_026");
            var g = G(); var p = P1(g); var o = P2(g);
            var mine = Board(p, Yeti); var a = Board(o, Yeti); var b = Board(o, Wisp);
            PlayNew(g, p, "CS2_026");
            Check("enemy minions frozen", a.IsFrozen && b.IsFrozen, Describe(g));
            Check("own minion not frozen", !mine.IsFrozen, "");
            Check("enemy hero not frozen", !o.Hero.IsFrozen, "");
            Eq("no damage", 5, Hp(a));
        }

        // ---------------------------------------------------------------- TLC_334 Relic of Kings
        [AuditTest]
        static void TLC_334_DiscoverExpensiveSpellCostsOne()
        {
            NoteText("TLC_334");
            var classes = new HashSet<CardClass>();
            for (int seed = 1; seed <= 5; seed++)
            {
                var g = G(s => s.Seed = seed); var p = P1(g);
                PlayNew(g, p, "TLC_334");
                var ch = ChoiceIds(g, p);
                Eq($"seed {seed}: 3 options", 3, ch.Count);
                Check($"seed {seed}: spells costing 8+", ch.All(i => C(i).Type == CardType.SPELL && C(i).Cost >= 8), string.Join(",", ch.Select(i => i + ":" + C(i).Cost)));
                foreach (var i in ch) classes.Add(C(i).Class);
                var pick = Pick(g, p, seed % 3);
                var card = p.HandZone.FirstOrDefault(x => x.Card.Id == pick);
                Check($"seed {seed}: in hand", card != null, string.Join(",", HandIds(p)));
                if (card != null)
                {
                    // A self-reducing card (e.g. JAIL_514 "costs less per card in hand") applies its own reduction on top of the set cost.
                    bool selfReducing = C(pick).Power?.Aura != null;
                    if (selfReducing) { Note($"{pick} self-reducing: {CardText(pick)} -> cost {card.Cost}"); Check($"seed {seed}: {pick} cost <= 1", card.Cost <= 1, card.Cost.ToString()); }
                    else Eq($"seed {seed}: {pick} (base {C(pick).Cost}) costs 1", 1, card.Cost);
                }
            }
            Note("classes: " + string.Join(",", classes));
            Check("any class seen", classes.Any(c => c != CardClass.MAGE && c != CardClass.NEUTRAL), "");
        }

        // ---------------------------------------------------------------- CATA_487 Raincaller
        [AuditTest]
        static void CATA_487_FirstSpellDamageEachTurnPlus2()
        {
            NoteText("CATA_487");
            var g = G(); var p = P1(g); var o = P2(g);
            var rc = (Minion)PlayNew(g, p, "CATA_487");
            PlayNew(g, p, Intellect);
            Eq("non-damaging spell: no gain", 1, Atk(rc));
            PlayNew(g, p, Frostbolt, o.Hero);
            Eq("first spell damage: +2", 3, Atk(rc));
            PlayNew(g, p, Frostbolt, o.Hero);
            Eq("second spell damage same turn: no gain", 3, Atk(rc));
            EndTurn(g); ClearHand(o);
            Eq("permanent through end of turn", 3, Atk(rc));
            PlayNew(g, o, Frostbolt, p.Hero);
            Eq("opponent's spell damage: no gain", 3, Atk(rc));
            EndTurn(g); ClearHand(p);
            PlayNew(g, p, Frostbolt, o.Hero);
            Eq("next own turn: +2 again", 5, Atk(rc));
            var c = Clone(g); Eq("clone keeps attack", 5, Atk(At(c, rc)));
        }

        [AuditTest]
        static void CATA_487_OpponentTurnOwnSpellDamage()
        {
            // Raincaller played then spell on the turn it is played (no fatigue of trigger) and minion-target damage counts
            var g = G(); var p = P1(g); var o = P2(g);
            var rc = (Minion)PlayNew(g, p, "CATA_487");
            var foe = Board(o, Golem);
            PlayNew(g, p, "CORE_SW_108", foe);
            Eq("minion spell damage counts", 3, Atk(rc));
        }

        // ---------------------------------------------------------------- CORE_SW_108 First Flame / SW_108t Second Flame
        [AuditTest]
        static void CORE_SW_108_DamageAndSecondFlame()
        {
            NoteText("CORE_SW_108", "SW_108t");
            var g = G(); var p = P1(g); var o = P2(g);
            var foe = Board(o, Golem);
            var ff = Hand(p, "CORE_SW_108");
            Check("hero not targetable", !CanPlay(ff, o.Hero), "");
            Play(g, ff, foe);
            Eq("2 damage", 5, Hp(foe));
            Eq("Second Flame added", "SW_108t", string.Join(",", HandIds(p)));
        }

        [AuditTest]
        static void SW_108t_Damage2()
        {
            var g = G(); var p = P1(g); var o = P2(g);
            var foe = Board(o, Golem);
            var sf = Hand(p, "SW_108t");
            Check("hero not targetable", !CanPlay(sf, o.Hero), "");
            Play(g, sf, foe);
            Eq("2 damage", 5, Hp(foe));
            Eq("adds nothing", 0, p.HandZone.Count);
            Eq("costs 1", 1, C("SW_108t").Cost);
        }

        // ---------------------------------------------------------------- GIL_645 Bonfire Elemental
        [AuditTest]
        static void GIL_645_ElementalLastTurnDraws()
        {
            NoteText("GIL_645");
            var g = G(); var p = P1(g);
            PlayNew(g, p, FlameElemental); Round(g); ClearHand(p);
            PlayNew(g, p, "GIL_645");
            Eq("drew 1", 1, p.HandZone.Count);
            var g2 = G(); var p2 = P1(g2);
            PlayNew(g2, p2, "GIL_645");
            Eq("no elemental: no draw", 0, p2.HandZone.Count);
        }

        // ---------------------------------------------------------------- LOE_002 Forgotten Torch
        [AuditTest]
        static void LOE_002_DamageAndShuffleRoaringTorch()
        {
            NoteText("LOE_002", "LOE_002t");
            var g = G(); var p = P1(g); var o = P2(g);
            int deck = p.DeckZone.Count;
            PlayNew(g, p, "LOE_002", o.Hero);
            Eq("3 damage", 27, o.Hero.Health);
            Eq("deck +1", deck + 1, p.DeckZone.Count);
            Check("Roaring Torch in deck", DeckIds(p).Contains("LOE_002t"), "");
            PlayNew(g, p, "LOE_002t", o.Hero);
            Eq("Roaring Torch deals 6", 21, o.Hero.Health);
            var foe = Board(o, Golem);
            PlayNew(g, p, "LOE_002", foe);
            Eq("can hit minions", 4, Hp(foe));
        }

        // ---------------------------------------------------------------- UNG_018 Flame Geyser
        [AuditTest]
        static void UNG_018_DamageAndElemental()
        {
            NoteText("UNG_018");
            var g = G(); var p = P1(g); var o = P2(g);
            PlayNew(g, p, "UNG_018", o.Hero);
            Eq("2 damage", 28, o.Hero.Health);
            Eq("hand", "UNG_809t1", string.Join(",", HandIds(p)));
            var e = C("UNG_809t1");
            Check("1/2 Elemental", e.ATK == 1 && e.Health == 2 && e[GameTag.CARDRACE] == (int)Race.ELEMENTAL, e.ATK + "/" + e.Health + " " + e[GameTag.CARDRACE]);
        }

        // ---------------------------------------------------------------- UNG_846 Shimmering Tempest
        [AuditTest]
        static void UNG_846_DeathrattleRandomMageSpell()
        {
            NoteText("UNG_846");
            for (int seed = 1; seed <= 6; seed++)
            {
                var g = G(s => s.Seed = seed); var p = P1(g);
                var m = Board(p, "UNG_846"); Kill(g, m);
                Eq($"seed {seed}: one card", 1, p.HandZone.Count);
                if (p.HandZone.Count == 1)
                    Check($"seed {seed}: mage spell", p.HandZone[0].Card.Type == CardType.SPELL && p.HandZone[0].Card.Class == CardClass.MAGE, p.HandZone[0].Card.Id);
            }
        }

        // ---------------------------------------------------------------- CFM_671 Cryomancer
        [AuditTest]
        static void CFM_671_FrozenEnemyBuff()
        {
            NoteText("CFM_671");
            var g = G(); var p = P1(g); var o = P2(g);
            var cr = (Minion)PlayNew(g, p, "CFM_671");
            Check("no frozen enemy: 5/5", Atk(cr) == 5 && Hp(cr) == 5, Describe(g));
            var g2 = G(); var p2 = P1(g2); var o2 = P2(g2);
            PlayNew(g2, p2, Frostbolt, o2.Hero);
            var cr2 = (Minion)PlayNew(g2, p2, "CFM_671");
            Check("frozen enemy hero: 7/7", Atk(cr2) == 7 && Hp(cr2) == 7, Describe(g2));
            var g3 = G(); var p3 = P1(g3); var o3 = P2(g3);
            var foe = Board(o3, Golem); PlayNew(g3, p3, Frostbolt, foe);
            var cr3 = (Minion)PlayNew(g3, p3, "CFM_671");
            Check("frozen enemy minion: 7/7", Atk(cr3) == 7 && Hp(cr3) == 7, Describe(g3));
            var g4 = G(); var p4 = P1(g4);
            var mine = Board(p4, Golem); PlayNew(g4, p4, Frostbolt, mine);
            var cr4 = (Minion)PlayNew(g4, p4, "CFM_671");
            Check("frozen friendly only: 5/5", Atk(cr4) == 5 && Hp(cr4) == 5, Describe(g4));
        }

        // ---------------------------------------------------------------- CS2_024 Frostbolt
        [AuditTest]
        static void CS2_024_DamageAndFreeze()
        {
            NoteText("CS2_024");
            var g = G(); var p = P1(g); var o = P2(g);
            PlayNew(g, p, Frostbolt, o.Hero);
            Eq("hero -3", 27, o.Hero.Health); Check("hero frozen", o.Hero.IsFrozen, "");
            var foe = Board(o, Golem);
            PlayNew(g, p, Frostbolt, foe);
            Eq("minion -3", 4, Hp(foe)); Check("minion frozen", foe.IsFrozen, "");
            Board(p, Geomancer);
            PlayNew(g, p, Frostbolt, foe);
            Eq("spell damage +1", 0, Math.Max(0, Hp(foe)));
            PlayNew(g, p, Frostbolt, p.Hero);
            Eq("own hero targetable", 26, p.Hero.Health);
        }

        // ---------------------------------------------------------------- CS2_033 Water Elemental / GVG_002 Snowchugger
        static void FreezeOnDamage(string id, int atk)
        {
            NoteText(id);
            var g = G(); var p = P1(g); var o = P2(g);
            var we = Board(p, id); var foe = Board(o, Golem);
            Attack(g, we, foe);
            Check("attacked minion frozen", foe.IsFrozen, Describe(g));
            var gh = G(); var ph = P1(gh); var oh = P2(gh);
            var weh = Board(ph, id);
            Attack(gh, weh, oh.Hero);
            Check("attacked hero frozen", oh.Hero.IsFrozen, Describe(gh));
            Eq("hero dmg", 30 - atk, oh.Hero.Health);
            // defending: attacker damaged by it gets frozen
            var g2 = G(); var p2 = P1(g2); var o2 = P2(g2);
            var we2 = Board(p2, id); EndTurn(g2); ClearHand(o2);
            var att = Board(o2, Golem);
            Attack(g2, att, we2);
            Check("attacker damaged by it frozen", att.IsFrozen, Describe(g2));
            // not damaged -> not frozen (divine shield)
            var g3 = G(); var p3 = P1(g3); var o3 = P2(g3);
            var we3 = Board(p3, id);
            var ds = Board(o3, "EX1_008"); // Argent Squire 1/1 divine shield
            Attack(g3, we3, ds);
            Check("divine shield absorbed: not frozen", !ds.IsFrozen, Describe(g3));
        }
        [AuditTest] static void CS2_033_FreezeAnyCharacterDamaged() => FreezeOnDamage("CS2_033", 3);
        [AuditTest] static void GVG_002_FreezeAnyCharacterDamaged() => FreezeOnDamage("GVG_002", 2);

        // ---------------------------------------------------------------- DAL_577 Ray of Frost / DAL_577ts
        [AuditTest]
        static void DAL_577_TwinspellFreezeThenDamage()
        {
            NoteText("DAL_577", "DAL_577ts");
            var g = G(); var p = P1(g); var o = P2(g);
            var foe = Board(o, Golem);
            var rf = Hand(p, "DAL_577");
            Check("hero not targetable", !CanPlay(rf, o.Hero), "");
            Play(g, rf, foe);
            Check("frozen", foe.IsFrozen, ""); Eq("no damage when not frozen", 7, Hp(foe));
            Eq("twinspell copy added", "DAL_577ts", string.Join(",", HandIds(p)));
            var ts = p.HandZone[0];
            Play(g, ts, foe);
            Eq("already frozen: 2 damage", 5, Hp(foe)); Check("still frozen", foe.IsFrozen, "");
            Eq("ts adds no further copy", 0, p.HandZone.Count);
            Board(p, Geomancer);
            PlayNew(g, p, "DAL_577ts", foe);
            Eq("spell damage +1 -> 3", 2, Hp(foe));
        }

        [AuditTest]
        static void DAL_577ts_FreezeUnfrozen()
        {
            var g = G(); var p = P1(g); var o = P2(g);
            var foe = Board(o, Golem);
            PlayNew(g, p, "DAL_577ts", foe);
            Check("frozen", foe.IsFrozen, ""); Eq("no damage", 7, Hp(foe));
        }

        // ---------------------------------------------------------------- ICC_068 Ice Walker
        [AuditTest]
        static void ICC_068_HeroPowerFreezes()
        {
            NoteText("ICC_068");
            var g = G(); var p = P1(g); var o = P2(g);
            Note("hero power: " + p.Hero.HeroPower.Card.Id + " " + CardText(p.Hero.HeroPower.Card.Id));
            Board(p, "ICC_068");
            var foe = Board(o, Golem);
            Power(g, p, foe);
            Check("target frozen", foe.IsFrozen, Describe(g));
            Eq("target took 1", 6, Hp(foe));
            Power(g, p, o.Hero);
            Check("hero frozen", o.Hero.IsFrozen, Describe(g));
            var g2 = G(); var p2 = P1(g2); var o2 = P2(g2);
            var foe2 = Board(o2, Golem);
            Power(g2, p2, foe2);
            Check("without Ice Walker: no freeze", !foe2.IsFrozen, "");
        }

        // ---------------------------------------------------------------- ICC_252 Coldwraith
        [AuditTest]
        static void ICC_252_FrozenEnemyDraws()
        {
            NoteText("ICC_252");
            var g = G(); var p = P1(g); var o = P2(g);
            var foe = Board(o, Golem);
            PlayNew(g, p, Frostbolt, foe);
            PlayNew(g, p, "ICC_252");
            Eq("drew 1", 1, p.HandZone.Count);
            var g2 = G(); var p2 = P1(g2); var o2 = P2(g2);
            PlayNew(g2, p2, Frostbolt, o2.Hero);
            PlayNew(g2, p2, "ICC_252");
            Eq("frozen hero counts", 1, p2.HandZone.Count);
            var g3 = G(); var p3 = P1(g3);
            PlayNew(g3, p3, "ICC_252");
            Eq("no frozen: no draw", 0, p3.HandZone.Count);
        }

        // ---------------------------------------------------------------- ICC_836 Breath of Sindragosa
        [AuditTest]
        static void ICC_836_RandomEnemyMinionDamageFreeze()
        {
            NoteText("ICC_836");
            for (int seed = 1; seed <= 5; seed++)
            {
                var g = G(s => s.Seed = seed); var p = P1(g); var o = P2(g);
                var mine = Board(p, Golem); var a = Board(o, Golem); var b = Board(o, Golem);
                PlayNew(g, p, "ICC_836");
                var hit = new[] { a, b }.Where(x => Hp(x) < 7).ToList();
                Eq($"seed {seed}: exactly one enemy minion hit", 1, hit.Count);
                if (hit.Count == 1) { Eq($"seed {seed}: 2 dmg", 5, Hp(hit[0])); Check($"seed {seed}: frozen", hit[0].IsFrozen, ""); }
                Eq($"seed {seed}: hero untouched", 30, o.Hero.Health);
                Check($"seed {seed}: own untouched", Hp(mine) == 7 && !mine.IsFrozen, "");
            }
            var g2 = G(); var p2 = P1(g2); var o2 = P2(g2);
            var a2 = Board(o2, Golem); Board(p2, Geomancer);
            PlayNew(g2, p2, "ICC_836");
            Eq("spell damage +1", 4, Hp(a2));
        }

        // ---------------------------------------------------------------- CORE_BAR_541 Runed Orb
        [AuditTest]
        static void CORE_BAR_541_DamageAndDiscoverSpell()
        {
            NoteText("CORE_BAR_541");
            var g = G(); var p = P1(g); var o = P2(g);
            PlayNew(g, p, "CORE_BAR_541", o.Hero);
            Eq("2 damage", 28, o.Hero.Health);
            var ch = ChoiceIds(g, p);
            Eq("3 options", 3, ch.Count);
            Check("all spells", ch.All(i => C(i).Type == CardType.SPELL), string.Join(",", ch));
            Note("classes " + string.Join(",", ch.Select(i => C(i).Class)));
            var pick = Pick(g, p, 1);
            Check("picked in hand", HandIds(p).Contains(pick), "");
            var foe = Board(o, Golem);
            PlayNew(g, p, "CORE_BAR_541", foe);
            Eq("minion targetable", 5, Hp(foe));
        }

        // ---------------------------------------------------------------- CORE_CS2_023 Arcane Intellect
        [AuditTest]
        static void CORE_CS2_023_DrawTwo()
        {
            NoteText("CORE_CS2_023");
            var g = G(); var p = P1(g);
            int deck = p.DeckZone.Count;
            PlayNew(g, p, Intellect);
            Eq("hand 2", 2, p.HandZone.Count);
            Eq("deck -2", deck - 2, p.DeckZone.Count);
        }

        // ---------------------------------------------------------------- Discover family
        static void Discover(string id, Func<Card, bool> pred, string what, bool mageOnly)
        {
            NoteText(id);
            for (int seed = 1; seed <= 6; seed++)
            {
                var g = G(s => s.Seed = seed); var p = P1(g);
                PlayNew(g, p, id);
                var ch = ChoiceIds(g, p);
                Eq($"seed {seed}: 3 options", 3, ch.Count);
                Eq($"seed {seed}: distinct", ch.Count, ch.Distinct().Count());
                Check($"seed {seed}: {what}", ch.All(i => pred(C(i))), string.Join(",", ch.Select(i => i + ":" + C(i).Type + ":" + C(i).Cost + ":" + C(i).Class)));
                if (mageOnly) Check($"seed {seed}: class cards (mage/neutral)", ch.All(i => C(i).Class == CardClass.MAGE || C(i).Class == CardClass.NEUTRAL), string.Join(",", ch.Select(i => i + ":" + C(i).Class)));
                var pick = Pick(g, p, seed % 3);
                Check($"seed {seed}: picked in hand", HandIds(p).Contains(pick), string.Join(",", HandIds(p)));
                Check($"seed {seed}: choice closed", p.Choice == null, "");
            }
        }
        [AuditTest] static void DAL_163_DiscoverMageMinion() => Discover("DAL_163", c => c.Type == CardType.MINION && c.Class == CardClass.MAGE, "Mage minions", true);
        [AuditTest] static void DAL_608_DiscoverSpellCost3OrLess() => Discover("DAL_608", c => c.Type == CardType.SPELL && c.Cost <= 3, "spells costing <=3", true);
        [AuditTest] static void LOE_003_DiscoverSpell() => Discover("LOE_003", c => c.Type == CardType.SPELL, "spells", true);

        // ---------------------------------------------------------------- LOOT_170 Raven Familiar
        [AuditTest]
        static void LOOT_170_RevealDrawIfMoreExpensive()
        {
            NoteText("LOOT_170");
            // ours costs more -> drawn
            var g = G(s => { s.PlayerDeck = Fill(9).Concat(new[] { "EX1_279" }).ToList(); s.BossDeck = Fill(29).Concat(new[] { Frostbolt }).ToList(); });
            var p = P1(g);
            Check("pyroblast in deck", DeckIds(p).Contains("EX1_279"), "");
            PlayNew(g, p, "LOOT_170");
            Eq("drew Pyroblast", "EX1_279", string.Join(",", HandIds(p)));
            // ours costs less -> not drawn
            var g2 = G(s => { s.PlayerDeck = Fill(9).Concat(new[] { Frostbolt }).ToList(); s.BossDeck = Fill(29).Concat(new[] { "EX1_279" }).ToList(); });
            var p2 = P1(g2);
            bool inDeck = DeckIds(p2).Contains(Frostbolt);
            PlayNew(g2, p2, "LOOT_170");
            Eq("cheaper: nothing drawn", 0, p2.HandZone.Count);
            Check("frostbolt still in deck", !inDeck || DeckIds(p2).Contains(Frostbolt), "");
            // equal -> not drawn
            var g3 = G(s => { s.PlayerDeck = Fill(9).Concat(new[] { Frostbolt }).ToList(); s.BossDeck = Fill(29).Concat(new[] { Frostbolt }).ToList(); });
            var p3 = P1(g3);
            PlayNew(g3, p3, "LOOT_170");
            Eq("equal: nothing drawn", 0, p3.HandZone.Count);
            // opponent has no spell -> ours costs more than nothing? text: reveal in each deck; HS draws if opponent has none
            var g4 = G(s => { s.PlayerDeck = Fill(9).Concat(new[] { Frostbolt }).ToList(); });
            var p4 = P1(g4);
            PlayNew(g4, p4, "LOOT_170");
            Note("opponent has no spell: hand=" + string.Join(",", HandIds(p4)));
            Check("never draws a non-spell", p4.HandZone.All(h => h.Card.Type == CardType.SPELL), string.Join(",", HandIds(p4)));
            Eq("opponent has no spell: ours (Frostbolt) wins and is drawn", "CS2_024", string.Join(",", HandIds(p4)));
        }

        // ---------------------------------------------------------------- TIME_857 Alter Time
        [AuditTest]
        static void TIME_857_DiscoverTwoArcaneSpellsCost2Less()
        {
            NoteText("TIME_857");
            for (int seed = 1; seed <= 4; seed++)
            {
                var g = G(s => s.Seed = seed); var p = P1(g);
                PlayNew(g, p, "TIME_857");
                var picks = new List<string>();
                for (int k = 0; k < 2; k++)
                {
                    var ch = ChoiceIds(g, p);
                    Eq($"seed {seed} pick {k}: 3 options", 3, ch.Count);
                    Check($"seed {seed} pick {k}: arcane spells", ch.All(i => C(i).Type == CardType.SPELL && School(C(i)) == "ARCANE"), string.Join(",", ch.Select(i => i + ":" + School(C(i)) + ":" + C(i).Set)));
                    var pick = Pick(g, p, k);
                    if (pick != null) picks.Add(pick);
                }
                Eq($"seed {seed}: 2 cards in hand", 2, p.HandZone.Count);
                foreach (var h in p.HandZone)
                    Eq($"seed {seed}: {h.Card.Id} costs 2 less", Math.Max(0, h.Card.Cost - 2), h.Cost);
                Check($"seed {seed}: choice closed", p.Choice == null, "");
                Note($"seed {seed}: " + string.Join(",", picks.Select(i => i + "(" + C(i).Set + ")")));
            }
        }

        // ---------------------------------------------------------------- TLC_461 Scrappy Scavenger
        [AuditTest]
        static void TLC_461_DiscoverCostEqualsRemainingMana()
        {
            NoteText("TLC_461");
            foreach (int mana in new[] { 10, 5, 3 })
            {
                var g = G(); var p = P1(g);
                Mana(p, mana);
                var sc = Hand(p, "TLC_461");
                Play(g, sc, keepMana: true);
                int remaining = mana - 1;
                var ch = ChoiceIds(g, p);
                Eq($"mana {mana}: 3 options", 3, ch.Count);
                Check($"mana {mana}: cost == {remaining}", ch.All(i => C(i).Cost == remaining), string.Join(",", ch.Select(i => i + ":" + C(i).Cost)));
                Eq($"mana {mana}: remaining after play", remaining, p.RemainingMana);
                Pick(g, p, 0);
                Eq($"mana {mana}: one card in hand", 1, p.HandZone.Count);
            }
            // 1 mana -> 0 remaining -> 0-cost card
            var g2 = G(); var p2 = P1(g2); Mana(p2, 1);
            var sc2 = Hand(p2, "TLC_461"); Play(g2, sc2, keepMana: true);
            var ch2 = ChoiceIds(g2, p2);
            Note("1 mana choices: " + string.Join(",", ch2.Select(i => i + ":" + C(i).Cost)) + " remaining " + p2.RemainingMana + " " + Describe(g2));
            // The offline Standard pool holds no 0-cost Mage/Neutral card, so no offer at 0 remaining is consistent with the pool.
            Check("1 mana: any offer is 0-cost", ch2.All(i => C(i).Cost == 0), string.Join(",", ch2.Select(i => i + ":" + C(i).Cost)));
            Check("1 mana: nothing thrown, minion on board", p2.BoardZone.Count == 1, Describe(g2));
        }
    }
}
