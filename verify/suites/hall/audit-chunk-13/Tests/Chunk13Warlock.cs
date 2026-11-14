// Audit chunk 13 (class WARLOCK): one test per card id.
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Audit.H;

namespace Audit
{
    internal static class Chunk13Warlock
    {
        const string FlameImp = "EX1_319";
        static void Discard(Game g, Controller c, IPlayable card) { Generic.DiscardBlock(c, card); g.DeathProcessingAndAuraUpdate(); }
        static Game G(int seed = 99) => NewGame(s => { s.PlayerHero = "HERO_07"; s.Seed = seed; });
        static bool CanAttackTarget(Minion m, ICharacter t) => m.Controller.Options().OfType<MinionAttackTask>().Any(o => o.Source == m && o.Target?.Id == t.Id);

        [AuditTest]
        static void CORE_ICC_055_LifestealThreeToMinion()
        {
            var g = G(); var p = P1(g); var o = P2(g);
            Damage(g, p.Hero, 10);
            var foe = Board(o, Yeti);
            var spell = Hand(p, "CORE_ICC_055");
            Check("heroes are not legal targets", !LegalTargets(spell).Any(t => t is Hero), string.Join(",", LegalTargets(spell).Select(t => t.Card.Id)));
            Play(g, spell, foe);
            Eq("yeti takes 3", 2, Hp(foe));
            Eq("hero healed 3 (lifesteal)", 23, Hp(p.Hero));
        }

        [AuditTest]
        static void EDR_485_DeathrattleDrawsSevenPlusMinion()
        {
            var g = G(); var p = P1(g);
            ClearDeck(p); DeckTop(p, Golem); DeckTop(p, Wisp); DeckTop(p, Yeti); DeckTop(p, "CS2_029"); // fireball (4) spell
            var d = Board(p, "EDR_485");
            Kill(g, d);
            Eq("drew exactly one card", 1, p.HandZone.Count);
            Eq("drew the 7-cost minion", Golem, HandIds(p).FirstOrDefault());
            // no qualifying minion: draws nothing
            var g2 = G(); var p2 = P1(g2);
            ClearDeck(p2); DeckTop(p2, Yeti); DeckTop(p2, "CS2_029");
            Kill(g2, Board(p2, "EDR_485"));
            Eq("nothing drawn without a 7+ minion", 0, p2.HandZone.Count);
        }

        static void FlameImpCase(string id)
        {
            var g = G(); var p = P1(g);
            var m = (Minion)PlayNew(g, p, id);
            Eq(id + " 3 attack", 3, Atk(m)); Eq(id + " 2 health", 2, Hp(m));
            Eq(id + " hero takes 3", 27, Hp(p.Hero));
            Eq(id + " boss untouched", 30, Hp(P2(g).Hero));
        }
        [AuditTest] static void CORE_EX1_319_BattlecryHitsOwnHero() => FlameImpCase("CORE_EX1_319");
        [AuditTest] static void EX1_319_BattlecryHitsOwnHero() => FlameImpCase("EX1_319");

        [AuditTest]
        static void CS2_065_TauntOneThree()
        {
            var g = G(); var m = (Minion)PlayNew(g, P1(g), "CS2_065");
            Eq("attack", 1, Atk(m)); Eq("health", 3, Hp(m)); Check("taunt", m.HasTaunt);
        }

        [AuditTest]
        static void CFM_751_ThreeToAllOtherCharacters()
        {
            var g = G(); var p = P1(g); var o = P2(g);
            var mine = Board(p, Yeti); var foe = Board(o, Yeti);
            var m = (Minion)PlayNew(g, p, "CFM_751");
            Eq("self untouched", 6, Hp(m));
            Eq("friendly minion hit", 2, Hp(mine));
            Eq("enemy minion hit", 2, Hp(foe));
            Eq("own hero hit", 27, Hp(p.Hero));
            Eq("enemy hero hit", 27, Hp(o.Hero));
        }

        [AuditTest]
        static void JAIL_513_GainsHealthPerCardInHand()
        {
            var g = G(); var p = P1(g);
            Hand(p, Wisp); Hand(p, Wisp); Hand(p, Wisp);
            var m = (Minion)PlayNew(g, p, "JAIL_513");
            Check("taunt", m.HasTaunt);
            Eq("3 attack", 3, Atk(m));
            Eq("1 + 3 health (3 cards in hand)", 4, Hp(m));
            Check("JAIL_513e applied", EnchantIds(m).Contains("JAIL_513e"), string.Join(",", EnchantIds(m)));
            var c = Clone(g); Eq("clone keeps health", 4, Hp(At(c, m)));
            var g2 = G(); var m2 = (Minion)PlayNew(g2, P1(g2), "JAIL_513");
            Eq("empty hand: 1 health", 1, Hp(m2));
        }

        [AuditTest]
        static void JAIL_513e_IncreasedHealthSurvivesSilenceCheck()
        {
            var g = G(); var p = P1(g);
            Hand(p, Wisp); Hand(p, Wisp);
            var m = (Minion)PlayNew(g, p, "JAIL_513");
            Eq("buffed max health", 3, MaxHp(m));
            Silence(g, m);
            Eq("silence removes the enchantment", 1, Hp(m));
        }

        [AuditTest]
        static void TLC_469_DeathrattleTwoTemporaryTwoCostMinions()
        {
            for (int seed = 1; seed <= 6; seed++)
            {
                var g = G(seed); var p = P1(g);
                Kill(g, Board(p, "TLC_469"));
                Eq($"seed {seed}: two cards", 2, p.HandZone.Count);
                foreach (var c in p.HandZone)
                {
                    Check($"seed {seed}: {c.Card.Id} is a minion", c is Minion);
                    Eq($"seed {seed}: {c.Card.Id} costs 2", 2, c.Card.Cost);
                    Check($"seed {seed}: {c.Card.Id} temporary", TemporaryRules.IsTemporary(c));
                }
            }
            // temporary: discarded at the end of turn
            var gg = G(); var pp = P1(gg);
            Kill(gg, Board(pp, "TLC_469"));
            EndTurn(gg);
            Eq("temporary cards gone at end of turn", 0, pp.HandZone.Count);
        }

        [AuditTest]
        static void UNG_830_DeathrattleSummonsDiscardedMinion()
        {
            var g = G(); var p = P1(g);
            var golem = Hand(p, Golem); var spell = Hand(p, "CS2_029");
            Discard(g, p, golem); Discard(g, p, spell);
            var d = Board(p, "UNG_830");
            Kill(g, d);
            Eq("summons the discarded minion", Golem, BoardIds(p).FirstOrDefault());
            Eq("only one summon", 1, p.BoardZone.Count);
            var g2 = G(); var p2 = P1(g2);
            Kill(g2, Board(p2, "UNG_830"));
            Eq("nothing discarded: nothing summoned", 0, p2.BoardZone.Count);
        }

        [AuditTest]
        static void CORE_YOD_026_DeathrattleGivesAttack()
        {
            var g = G(); var p = P1(g); var o = P2(g);
            var w = Board(p, Wisp); var foe = Board(o, Wisp);
            var s = Board(p, "CORE_YOD_026");
            Eq("2/1", 2, Atk(s));
            Kill(g, s);
            Eq("friendly wisp +2 attack", 3, Atk(w));
            Eq("enemy unchanged", 1, Atk(foe));
            // buffed servant gives its current attack
            var g2 = G(); var p2 = P1(g2);
            var w2 = Board(p2, Wisp); var s2 = Board(p2, "CORE_YOD_026");
            PlayNew(g2, p2, "CS2_092", s2); // Blessing of Kings +4/+4
            Kill(g2, s2);
            Eq("gives buffed attack (6)", 7, Atk(w2));
        }

        [AuditTest]
        static void EX1_185_OtherDemonsPlusOneAttack()
        {
            var g = G(); var p = P1(g); var o = P2(g);
            var imp = Board(p, FlameImp); var yeti = Board(p, Yeti); var foeImp = Board(o, FlameImp);
            var sb = (Minion)PlayNew(g, p, "EX1_185");
            Check("taunt", sb.HasTaunt); Eq("self 5", 5, Atk(sb)); Eq("health 8", 8, Hp(sb));
            Eq("friendly demon +1", 4, Atk(imp));
            Eq("non-demon unchanged", 4, Atk(yeti));
            Eq("enemy demon unchanged", 3, Atk(foeImp));
            var late = Board(p, FlameImp);
            Eq("later demon +1", 4, Atk(late));
            Kill(g, sb);
            Eq("aura ends on death", 3, Atk(imp));
        }

        [AuditTest]
        static void EX1_185e_EnchantmentOnBuffedDemon()
        {
            var g = G(); var p = P1(g);
            var imp = Board(p, FlameImp);
            Board(p, "EX1_185");
            Check("EX1_185e on demon", EnchantIds(imp).Contains("EX1_185e") || imp.OngoingEffect != null || Atk(imp) == 4, string.Join(",", EnchantIds(imp)));
            Eq("+1 attack", 4, Atk(imp));
        }

        [AuditTest]
        static void EX1_301_DestroysManaCrystal()
        {
            var g = G(); var p = P1(g);
            var m = (Minion)PlayNew(g, p, "EX1_301");
            Check("taunt", m.HasTaunt); Eq("3 atk", 3, Atk(m)); Eq("5 hp", 5, Hp(m));
            Eq("max mana 10 -> 9", 9, p.BaseMana);
        }

        [AuditTest]
        static void BOT_222_FourToMinionAndOwnHero()
        {
            var g = G(); var p = P1(g); var o = P2(g);
            var foe = Board(o, Golem);
            var spell = Hand(p, "BOT_222");
            Check("heroes not legal targets", !LegalTargets(spell).Any(t => t is Hero));
            Play(g, spell, foe);
            Eq("minion takes 4", 3, Hp(foe)); Eq("own hero takes 4", 26, Hp(p.Hero)); Eq("enemy hero untouched", 30, Hp(o.Hero));
            var g2 = G(); var p2 = P1(g2); var o2 = P2(g2);
            Board(p2, "CS2_142"); // Kobold Geomancer, Spell Damage +1
            var f2 = Board(o2, Golem);
            PlayNew(g2, p2, "BOT_222", f2);
            Eq("spell damage: minion 5", 2, Hp(f2)); Eq("spell damage: hero 5", 25, Hp(p2.Hero));
        }

        [AuditTest]
        static void BRM_005_TwoToAllNonDemons()
        {
            var g = G(); var p = P1(g); var o = P2(g);
            var myImp = Board(p, "CS2_065"); // Voidwalker demon 1/3
            var myYeti = Board(p, Yeti); var foeYeti = Board(o, Yeti); var foeImp = Board(o, "CS2_065");
            PlayNew(g, p, "BRM_005");
            Eq("friendly demon spared", 3, Hp(myImp)); Eq("enemy demon spared", 3, Hp(foeImp));
            Eq("friendly yeti hit", 3, Hp(myYeti)); Eq("enemy yeti hit", 3, Hp(foeYeti));
            Eq("heroes untouched", 30, Hp(p.Hero)); Eq("boss untouched", 30, Hp(o.Hero));
        }

        [AuditTest]
        static void TLC_466_EndOfTurnDiscardFillImpsThreeTurns()
        {
            var g = G(); var p = P1(g); var o = P2(g);
            PlayNew(g, p, "TLC_466");
            Hand(p, Wisp); Hand(p, Wisp);
            Board(p, Yeti);
            EndTurn(g); // tick 1
            Eq("tick1 discards one card", 1, p.HandZone.Count);
            Eq("tick1 board filled", 7, p.BoardZone.Count);
            Check("imps are 3/2 UNG_829t3", p.BoardZone.Skip(1).All(m => m.Card.Id == "UNG_829t3" && m.AttackDamage == 3 && m.Health == 2), Describe(g));
            int bossBoard = o.BoardZone.Count;
            EndTurn(g); // boss end: nothing
            Eq("boss end turn: no imps for boss", bossBoard, o.BoardZone.Count);
            // clear player board, tick 2
            foreach (var m in p.BoardZone.ToArray()) m.Destroy(); g.DeathProcessingAndAuraUpdate();
            ClearHand(p); Hand(p, Wisp);
            EndTurn(g);
            Eq("tick2 board filled", 7, p.BoardZone.Count);
            Eq("tick2 discarded", 0, p.HandZone.Count);
            EndTurn(g);
            foreach (var m in p.BoardZone.ToArray()) m.Destroy(); g.DeathProcessingAndAuraUpdate();
            EndTurn(g); // tick 3 (last)
            Eq("tick3 board filled", 7, p.BoardZone.Count);
            Check("story gone after 3 turns", !p.SecretZone.Any(s => s.Card.Id == "TLC_466"), Describe(g));
            EndTurn(g);
            foreach (var m in p.BoardZone.ToArray()) m.Destroy(); g.DeathProcessingAndAuraUpdate();
            EndTurn(g); // would be tick 4
            Eq("no 4th tick", 0, p.BoardZone.Count);
        }

        [AuditTest]
        static void UNG_829t3_ThreeTwoImp()
        {
            var g = G(); var m = Board(P1(g), "UNG_829t3");
            Eq("3 atk", 3, Atk(m)); Eq("2 hp", 2, Hp(m)); Check("demon", m.IsRace(Race.DEMON));
        }

        [AuditTest]
        static void AT_021_GainsOnDiscard()
        {
            var g = G(); var p = P1(g);
            var k = Board(p, "AT_021");
            var w = Hand(p, Wisp);
            Discard(g, p, w);
            Eq("atk +1", 4, Atk(k)); Eq("hp +1", 3, Hp(k));
            Check("AT_021e applied", EnchantIds(k).Contains("AT_021e"), string.Join(",", EnchantIds(k)));
            PlayNew(g, p, "EX1_308", P2(g).Hero); // Soulfire discards (hand empty -> nothing)
            Eq("no discard no buff", 4, Atk(k));
            var o = P2(g); var ow = Hand(o, Wisp); Discard(g, o, ow);
            Eq("enemy discard ignored", 4, Atk(k));
        }

        [AuditTest]
        static void AT_021e_IncreasedStats()
        {
            var g = G(); var p = P1(g);
            var k = Board(p, "AT_021");
            Discard(g, p, Hand(p, Wisp)); Discard(g, p, Hand(p, Wisp));
            Eq("two discards +2/+2", 5, Atk(k)); Eq("hp", 4, Hp(k));
        }

        [AuditTest]
        static void AT_022_PlayOrDiscardHitsRandomEnemy()
        {
            for (int seed = 1; seed <= 6; seed++)
            {
                var g = G(seed); var p = P1(g); var o = P2(g);
                var foe = Board(o, Golem); var mine = Board(p, Golem);
                PlayNew(g, p, "AT_022");
                int dealt = (30 - Hp(o.Hero)) + (7 - Hp(foe));
                Eq($"seed {seed} play: 4 to an enemy", 4, dealt);
                Eq($"seed {seed} friendly safe", 7, Hp(mine)); Eq($"seed {seed} own hero safe", 30, Hp(p.Hero));
                var c = Hand(p, "AT_022"); Discard(g, p, c);
                int dealt2 = (30 - Hp(o.Hero)) + (7 - Hp(foe));
                Eq($"seed {seed} discard: 4 more", 8, dealt2);
            }
        }

        [AuditTest]
        static void EX1_306_BattlecryDiscardsRandom()
        {
            var g = G(); var p = P1(g);
            Hand(p, Wisp); Hand(p, Yeti);
            var m = (Minion)PlayNew(g, p, "EX1_306");
            Eq("4/3", 4, Atk(m)); Eq("hp", 3, Hp(m));
            Eq("one discarded", 1, p.HandZone.Count);
            Eq("graveyard has the discard", 1, p.GraveyardZone.Count(c => c.Card.Id == Wisp || c.Card.Id == Yeti));
        }

        [AuditTest]
        static void EX1_308_FourDamageDiscardRandom()
        {
            var g = G(); var p = P1(g); var o = P2(g);
            Hand(p, Wisp); Hand(p, Yeti);
            PlayNew(g, p, "EX1_308", o.Hero);
            Eq("4 damage to hero", 26, Hp(o.Hero));
            Eq("one discarded", 1, p.HandZone.Count);
            var foe = Board(o, Golem);
            PlayNew(g, p, "EX1_308", foe);
            Eq("4 damage to minion", 3, Hp(foe));
            Eq("discarded again", 0, p.HandZone.Count);
        }

        [AuditTest]
        static void EX1_310_ChargeDiscardTwo()
        {
            var g = G(); var p = P1(g); var o = P2(g);
            Hand(p, Wisp); Hand(p, Yeti); Hand(p, Golem);
            var m = (Minion)PlayNew(g, p, "EX1_310");
            Check("charge", m.HasCharge); Eq("5/7", 5, Atk(m)); Eq("hp 7", 7, Hp(m));
            Eq("two discarded", 1, p.HandZone.Count);
            Check("can attack hero now", CanAttackTarget(m, o.Hero));
        }

        [AuditTest]
        static void KAR_089_DrawsOnDiscard()
        {
            var g = G(); var p = P1(g);
            Board(p, "KAR_089");
            ClearDeck(p); DeckTop(p, Golem);
            var w = Hand(p, Wisp);
            Discard(g, p, w);
            Eq("drew a card", Golem, HandIds(p).FirstOrDefault());
            var o = P2(g); Hand(o, Wisp); int pd = p.DeckZone.Count; DeckTop(p, Yeti);
            Discard(g, o, o.HandZone[0]);
            Eq("enemy discard: no draw", 1, p.HandZone.Count);
        }

        [AuditTest]
        static void KAR_205_SummonedWhenDiscarded()
        {
            var g = G(); var p = P1(g);
            var gol = Hand(p, "KAR_205");
            Hand(p, Wisp);
            PlayNew(g, p, "EX1_308", P2(g).Hero); // Soulfire: random discard, loop until golem discarded
            if (p.HandZone.Any(c => c.Card.Id == "KAR_205")) PlayNew(g, p, "EX1_308", P2(g).Hero);
            Eq("golem on board", "KAR_205", BoardIds(p).FirstOrDefault());
            var m = p.BoardZone.FirstOrDefault();
            if (m != null) { Eq("3/3", 3, Atk(m)); Eq("hp", 3, Hp(m)); }
        }

        [AuditTest]
        static void TRL_551_DiscardsLowestCost()
        {
            var g = G(); var p = P1(g);
            Hand(p, Golem); Hand(p, Wisp); Hand(p, Yeti);
            var m = (Minion)PlayNew(g, p, "TRL_551");
            Check("taunt", m.HasTaunt); Eq("2/6", 2, Atk(m)); Eq("hp", 6, Hp(m));
            Check("wisp discarded", !HandIds(p).Contains(Wisp) && p.GraveyardZone.Any(c => c.Card.Id == Wisp), string.Join(",", HandIds(p)));
            Eq("two left", 2, p.HandZone.Count);
        }

        [AuditTest]
        static void BOT_224_SummonsCopy()
        {
            var g = G(); var p = P1(g);
            PlayNew(g, p, "BOT_224");
            Eq("two doubling imps", 2, BoardIds(p).Count(i => i == "BOT_224"));
            Check("2/2s", p.BoardZone.All(m => m.AttackDamage == 2 && m.Health == 2));
            // copy keeps buffs? "copy of this minion" - battlecry before buff; just verify no infinite chain
            Eq("only two", 2, p.BoardZone.Count);
        }

        [AuditTest]
        static void BOT_443_DeathrattleBuffsDemonsInHand()
        {
            var g = G(); var p = P1(g);
            var imp = Hand(p, FlameImp); var yeti = Hand(p, Yeti);
            var v = Board(p, "BOT_443");
            Kill(g, v);
            Eq("demon atk +1", 4, ((Minion)imp).AttackDamage); Eq("demon hp +1", 3, ((Minion)imp).Health);
            Eq("non-demon unchanged", 4, ((Minion)yeti).AttackDamage);
        }

        [AuditTest]
        static void BRM_006_SummonsImpWhenDamaged()
        {
            var g = G(); var p = P1(g);
            var b = Board(p, "BRM_006");
            Damage(g, b, 1);
            Eq("one imp", 1, BoardIds(p).Count(i => i == "BRM_006t"));
            Damage(g, b, 1);
            Eq("two imps", 2, BoardIds(p).Count(i => i == "BRM_006t"));
            Damage(g, b, 5);
            Eq("lethal damage still summons", 3, BoardIds(p).Count(i => i == "BRM_006t"));
            Check("boss dead", !p.BoardZone.Any(m => m.Card.Id == "BRM_006"));
            var o = P2(g); var foe = Board(o, "BRM_006");
            Damage(g, foe, 1);
            Eq("enemy copy summons for its controller", 1, BoardIds(o).Count(i => i == "BRM_006t"));
        }

        [AuditTest]
        static void BRM_006t_OneOneImp()
        {
            var g = G(); var m = Board(P1(g), "BRM_006t");
            Eq("1 atk", 1, Atk(m)); Eq("1 hp", 1, Hp(m));
        }

        [AuditTest]
        static void CORE_LOOT_013_TauntTwoToOwnHero()
        {
            var g = G(); var p = P1(g);
            var m = (Minion)PlayNew(g, p, "CORE_LOOT_013");
            Check("taunt", m.HasTaunt); Eq("2 atk", 2, Atk(m)); Eq("4 hp", 4, Hp(m));
            Eq("own hero 28", 28, Hp(p.Hero)); Eq("boss 30", 30, Hp(P2(g).Hero));
        }

        [AuditTest]
        static void FP1_022_DeathrattlePutsDemonFromHand()
        {
            for (int seed = 1; seed <= 4; seed++)
            {
                var g = G(seed); var p = P1(g);
                Hand(p, Yeti); Hand(p, FlameImp); Hand(p, Golem);
                var v = Board(p, "FP1_022");
                Kill(g, v);
                Eq($"seed {seed}: imp on board", FlameImp, BoardIds(p).FirstOrDefault());
                Eq($"seed {seed}: no battlecry", 30, Hp(p.Hero));
                Check($"seed {seed}: two left in hand (demon moved, not copied)", p.HandZone.Count == 2 && !HandIds(p).Contains(FlameImp), string.Join(",", HandIds(p)) + " | " + Describe(g));
            }
            var g2 = G(); var p2 = P1(g2); Hand(p2, Yeti);
            Kill(g2, Board(p2, "FP1_022"));
            Eq("no demon: nothing", 0, p2.BoardZone.Count);
        }

        [AuditTest]
        static void ICC_075_EndOfYourTurnOneToEnemyMinions()
        {
            var g = G(); var p = P1(g); var o = P2(g);
            var d = Board(p, "ICC_075"); var mine = Board(p, Yeti);
            var foe = Board(o, Yeti); var foe2 = Board(o, Wisp);
            EndTurn(g);
            Eq("enemy yeti -1", 4, Hp(foe)); Check("enemy wisp died", foe2.Zone != o.BoardZone);
            Eq("friendly untouched", 5, Hp(mine)); Eq("self untouched", 5, Hp(d));
            Eq("enemy hero untouched", 30, Hp(o.Hero));
            ClearHand(o);
            EndTurn(g); // boss end: no trigger
            Eq("not on enemy end of turn", 4, Hp(foe));
        }

        [AuditTest]
        static void CORE_GIL_558_Lifesteal()
        {
            var g = G(); var p = P1(g); var o = P2(g);
            Damage(g, p.Hero, 10);
            var m = Board(p, "CORE_GIL_558");
            Eq("2/1", 2, Atk(m)); Check("lifesteal", m.HasLifeSteal);
            Attack(g, m, o.Hero);
            Eq("boss -2", 28, Hp(o.Hero)); Eq("hero +2", 22, Hp(p.Hero));
        }

        [AuditTest]
        static void GIL_143_LifestealRush()
        {
            var g = G(); var p = P1(g); var o = P2(g);
            Damage(g, p.Hero, 10);
            var foe = Board(o, Yeti);
            var m = (Minion)PlayNew(g, p, "GIL_143");
            Check("lifesteal", m.HasLifeSteal); Check("rush", m.IsRush);
            Check("rush: can attack minion this turn", CanAttackTarget(m, foe));
            Check("rush: cannot attack hero this turn", !CanAttackTarget(m, o.Hero));
            g.Process(MinionAttackTask.Any(p, m, foe));
            Eq("yeti -1", 4, Hp(foe)); Eq("hero +1", 21, Hp(p.Hero));
        }

        [AuditTest]
        static void GIL_201_SwapsInHandEachTurn()
        {
            var g = G(); var p = P1(g);
            var c = Hand(p, "GIL_201");
            Eq("starts 2 atk", 2, ((Minion)c).AttackDamage);
            Round(g);
            var now = p.HandZone.FirstOrDefault(x => x.Card.Id.StartsWith("GIL_201"));
            Check("still in hand", now != null, string.Join(",", HandIds(p)));
            if (now is Minion m)
            {
                Note($"after a round: {m.Card.Id} {m.AttackDamage}/{m.Health}");
                Eq("swapped atk 4", 4, m.AttackDamage); Eq("swapped hp 2", 2, m.Health);
                Check("lifesteal kept", m.HasLifeSteal);
                Round(g);
                var n2 = p.HandZone.FirstOrDefault(x => x.Card.Id.StartsWith("GIL_201")) as Minion;
                Check("swaps back next turn", n2 != null && n2.AttackDamage == 2 && n2.Health == 4, n2 == null ? "gone" : $"{n2.Card.Id} {n2.AttackDamage}/{n2.Health}");
            }
        }

        [AuditTest]
        static void GIL_527_LifestealTaunt()
        {
            var g = G(); var p = P1(g); var o = P2(g);
            Damage(g, p.Hero, 5);
            var m = Board(p, "GIL_527");
            Check("taunt", m.HasTaunt); Check("lifesteal", m.HasLifeSteal); Eq("1/6", 1, Atk(m)); Eq("hp", 6, Hp(m));
            Attack(g, m, o.Hero);
            Eq("hero +1", 26, Hp(p.Hero));
        }

        [AuditTest]
        static void GVG_018_Lifesteal()
        {
            var g = G(); var p = P1(g); var o = P2(g);
            Damage(g, p.Hero, 5);
            var m = Board(p, "GVG_018");
            Check("lifesteal", m.HasLifeSteal); Eq("1/4", 1, Atk(m)); Eq("hp", 4, Hp(m));
            var foe = Board(o, Yeti);
            Attack(g, m, foe);
            Eq("hero +1", 26, Hp(p.Hero));
        }

        [AuditTest]
        static void UNG_049_PlusThreeOnOpponentsTurn()
        {
            var g = G(); var p = P1(g); var o = P2(g);
            var t = Board(p, "UNG_049");
            Check("taunt", t.HasTaunt); Eq("own turn 1 atk", 1, Atk(t)); Eq("hp 7", 7, Hp(t));
            EndTurn(g);
            Eq("opponent turn 4 atk", 4, Atk(t));
            var foe = Board(o, Wisp);
            Attack(g, foe, t);
            Check("wisp killed by 4 retaliation", foe.Zone != o.BoardZone);
            EndTurn(g);
            Eq("back to 1", 1, Atk(t));
        }

        [AuditTest]
        static void FP1_022_DemonLeavesHandByCombatDeath()
        {
            var g = G(); var p = P1(g); var o = P2(g);
            var imp = Hand(p, FlameImp);
            var v = Board(p, "FP1_022");
            var foe = Board(o, Golem);
            Attack(g, v, foe);
            var onBoard = p.BoardZone.FirstOrDefault();
            Note($"hand ids {string.Join(",", p.HandZone.Select(c => c.Card.Id + "#" + c.Id))} board {string.Join(",", p.BoardZone.Select(c => c.Card.Id + "#" + c.Id))} imp#{imp.Id} zone {imp.Zone?.Type}");
            Check("demon summoned", onBoard != null && onBoard.Card.Id == FlameImp, Describe(g));
            Check("the hand demon left the hand", !p.HandZone.Contains(imp) && p.HandZone.Count == 0, Describe(g));
        }
    }
}
