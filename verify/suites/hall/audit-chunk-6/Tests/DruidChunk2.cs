// Audit chunk class-DRUID-2: one test per card id (enchantments/transform targets covered inside their parent's test).
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Audit.H;

namespace Audit
{
    internal static class DruidChunk2
    {
        static SabberStoneCore.Model.Game G() => NewGame(s => s.PlayerHero = "HERO_06");
        static string St(ICharacter m) => Atk(m) + "/" + Hp(m);

        [AuditTest] static void AT_037_LivingRoots()
        {
            Note(CardText("AT_037"));
            var g = G(); var p = P1(g); var o = P2(g);
            var foe = Board(o, Yeti);
            Play(g, Hand(p, "AT_037"), foe, 1);
            Eq("2 damage to minion", 3, Hp(foe));
            Play(g, Hand(p, "AT_037"), o.Hero, 1);
            Eq("2 damage to hero", 28, Hp(o.Hero));
            var foe2 = Board(o, Golem);
            Board(p, "CS2_142");
            Play(g, Hand(p, "AT_037"), foe2, 1);
            Eq("spell damage +1 -> 3", 4, Hp(foe2));
            var g2 = G(); var p2 = P1(g2);
            Play(g2, Hand(p2, "AT_037"), null, 2);
            Eq("two saplings", "AT_037t,AT_037t", string.Join(",", BoardIds(p2)));
            Check("saplings 1/1", p2.BoardZone.All(m => m.AttackDamage == 1 && m.Health == 1), Describe(g2));
        }

        [AuditTest] static void FIR_906_Overheat()
        {
            Note(CardText("FIR_906"));
            var g = G(); var p = P1(g); var o = P2(g);
            var a = Board(p, Wisp); var b = Board(p, Yeti); var foe = Board(o, Wisp);
            Hand(p, Yeti);
            Play(g, Hand(p, "FIR_906"));
            Eq("no nature spell: wisp atk 2", 2, Atk(a)); Eq("wisp hp 2", 2, Hp(a));
            Eq("yeti hp 6", 6, Hp(b));
            Eq("enemy untouched", 1, Atk(foe));
            Eq("yeti card kept", 1, p.HandZone.Count);
            var nat = Hand(p, "AT_037");
            Note("Living Roots spell school tag 1635=" + nat[(GameTag)1635]);
            Play(g, Hand(p, "FIR_906"));
            Eq("with nature spell: wisp atk +2", 4, Atk(a)); Eq("wisp hp +2", 4, Hp(a));
            Eq("yeti 7/8 (4/5 +1 +2)", "7/8", St(b));
            Check("nature spell discarded, yeti kept", HandIds(p).SequenceEqual(new[] { Yeti }), string.Join(",", HandIds(p)));
            Check("FIR_906e on wisp", EnchantIds(a).Contains("FIR_906e"), string.Join(",", EnchantIds(a)));
            var c = Clone(g); Eq("clone keeps buff", 4, At(c, a).AttackDamage);
        }

        [AuditTest] static void ULD_273_Overflow()
        {
            Note(CardText("ULD_273"));
            var g = G(); var p = P1(g); var o = P2(g);
            var m = Board(p, Golem); var e = Board(o, Golem);
            Damage(g, m, 6); Damage(g, e, 6); Damage(g, p.Hero, 10); Damage(g, o.Hero, 10);
            for (int i = 0; i < 6; i++) DeckTop(p, Wisp);
            int deck = p.DeckZone.Count;
            Play(g, Hand(p, "ULD_273"));
            Eq("friendly minion +5", 6, Hp(m)); Eq("enemy minion +5", 6, Hp(e));
            Eq("own hero +5", 25, Hp(p.Hero)); Eq("enemy hero +5", 25, Hp(o.Hero));
            Eq("drew 5", 5, p.HandZone.Count); Eq("deck -5", deck - 5, p.DeckZone.Count);
        }

        [AuditTest] static void CORE_CS2_009_MarkOfTheWild()
        {
            Note(CardText("CORE_CS2_009") + " | CS2_009e: " + CardText("CS2_009e"));
            var g = G(); var p = P1(g); var o = P2(g);
            var m = Board(p, Wisp); var e = Board(o, Wisp);
            Play(g, Hand(p, "CORE_CS2_009"), m);
            Eq("+2 attack", 3, Atk(m)); Eq("+3 health", 4, Hp(m)); Check("taunt", m.HasTaunt);
            Check("enchant CS2_009e", EnchantIds(m).Contains("CS2_009e"), string.Join(",", EnchantIds(m)));
            Play(g, Hand(p, "CORE_CS2_009"), e);
            Eq("can target enemy minion too", 3, Atk(e));
            var c = Clone(g); Eq("clone keeps", 4, At(c, m).Health);
            Silence(g, m); Eq("silenced back to 1", 1, Atk(m)); Check("taunt removed", !m.HasTaunt);
        }

        [AuditTest] static void CS2_005_Claw()
        {
            Note(CardText("CS2_005"));
            var g = G(); var p = P1(g);
            Play(g, Hand(p, "CS2_005"));
            Eq("+2 attack", 2, Atk(p.Hero)); Eq("2 armor", 2, Armor(p));
            Round(g);
            Eq("attack gone next turn", 0, Atk(p.Hero)); Eq("armor stays", 2, Armor(p));
        }

        [AuditTest] static void GIL_507_BewitchedGuardian()
        {
            Note(CardText("GIL_507"));
            var g = G(); var p = P1(g);
            Hand(p, Wisp); Hand(p, Wisp); Hand(p, Wisp);
            var m = (Minion)Hand(p, "GIL_507");
            Play(g, m);
            Eq("1 + 3 cards in hand", 4, Hp(m)); Eq("attack 4", 4, Atk(m)); Check("taunt", m.HasTaunt);
            var g2 = G(); var p2 = P1(g2);
            var m2 = (Minion)PlayNew(g2, p2, "GIL_507");
            Eq("empty hand: 1 health", 1, Hp(m2));
        }

        [AuditTest] static void GVG_030_AnodizedRoboCub()
        {
            Note(CardText("GVG_030"));
            var g = G(); var p = P1(g);
            Play(g, Hand(p, "GVG_030"), null, 1);
            var a = p.BoardZone[0];
            Eq("opt1 3/2", "3/2", St(a)); Check("taunt", a.HasTaunt);
            Play(g, Hand(p, "GVG_030"), null, 2);
            var b = p.BoardZone[1];
            Eq("opt2 2/3", "2/3", St(b)); Check("taunt", b.HasTaunt);
        }

        [AuditTest] static void ICC_079_Gnash()
        {
            Note(CardText("ICC_079"));
            var g = G(); var p = P1(g);
            Play(g, Hand(p, "ICC_079"));
            Eq("+3 attack", 3, Atk(p.Hero)); Eq("3 armor", 3, Armor(p));
            Round(g); Eq("attack gone", 0, Atk(p.Hero)); Eq("armor stays", 3, Armor(p));
        }

        [AuditTest] static void ICC_808_CryptLord()
        {
            Note(CardText("ICC_808"));
            var g = G(); var p = P1(g); var o = P2(g);
            var cl = Board(p, "ICC_808");
            Eq("base 1/6", "1/6", St(cl)); Check("taunt", cl.HasTaunt);
            Board(p, Wisp);
            Eq("+1 after friendly summon", 7, Hp(cl));
            PlayNew(g, p, Wisp);
            Eq("+1 after playing a minion", 8, Hp(cl)); Eq("max health 8", 8, MaxHp(cl));
            Board(o, Wisp);
            Eq("enemy summon ignored", 8, Hp(cl));
            Play(g, Hand(p, "AT_037"), null, 2);
            Eq("+2 for two saplings", 10, Hp(cl));
            Check("ICC_808e", EnchantIds(cl).Contains("ICC_808e"), string.Join(",", EnchantIds(cl)));
        }

        [AuditTest] static void LOOT_047_Barkskin()
        {
            Note(CardText("LOOT_047"));
            var g = G(); var p = P1(g);
            var m = Board(p, Wisp);
            Play(g, Hand(p, "LOOT_047"), m);
            Eq("+3 health", 4, Hp(m)); Eq("attack same", 1, Atk(m)); Eq("3 armor", 3, Armor(p));
        }

        [AuditTest] static void LOOT_048_IronwoodGolem()
        {
            Note(CardText("LOOT_048"));
            var g = G(); var p = P1(g); var o = P2(g);
            var m = Board(p, "LOOT_048");
            Check("taunt", m.HasTaunt);
            Round(g); Mana(p, 10); ClearHand(o);
            Check("0 armor: cannot attack", !m.CanAttack, Describe(g));
            p.Hero.Armor = 2; g.DeathProcessingAndAuraUpdate();
            Check("2 armor: cannot attack", !m.CanAttack);
            p.Hero.Armor = 3; g.DeathProcessingAndAuraUpdate();
            Check("3 armor: can attack (text: 3 or more Armor)", m.CanAttack, "armor " + p.Hero.Armor);
            p.Hero.Armor = 4; g.DeathProcessingAndAuraUpdate();
            Check("4 armor: can attack", m.CanAttack);
            Check("attack works", g.Process(MinionAttackTask.Any(p, m, o.Hero)));
            Check("hit hero for 3", Hp(o.Hero) <= 27, Describe(g));
        }

        [AuditTest] static void AT_042_DruidOfTheSaber()
        {
            Note(CardText("AT_042"));
            var g = G(); var p = P1(g);
            Play(g, Hand(p, "AT_042"), null, 1);
            var a = p.BoardZone[0];
            Eq("opt1 2/1", "2/1", St(a)); Check("charge", a.HasCharge); Check("not stealth", !a.HasStealth);
            Check("charge can attack now", a.CanAttack);
            Play(g, Hand(p, "AT_042"), null, 2);
            var b = p.BoardZone[1];
            Eq("opt2 3/2", "3/2", St(b)); Check("stealth", b.HasStealth); Check("not charge", !b.HasCharge);
            Note("OG_044c: " + CardText("OG_044c"));
            var c = Board(p, "OG_044c");
            Eq("OG_044c 3/2", "3/2", St(c)); Check("charge+stealth", c.HasCharge && c.HasStealth);
        }

        [AuditTest] static void BRM_010_DruidOfTheFlame()
        {
            Note(CardText("BRM_010"));
            var g = G(); var p = P1(g);
            Play(g, Hand(p, "BRM_010"), null, 1);
            Eq("opt1 5/2", "5/2", St(p.BoardZone[0]));
            Play(g, Hand(p, "BRM_010"), null, 2);
            Eq("opt2 2/5", "2/5", St(p.BoardZone[1]));
            Note("OG_044b: " + CardText("OG_044b"));
            Eq("OG_044b 5/5", "5/5", St(Board(p, "OG_044b")));
        }

        [AuditTest] static void CORE_EX1_154_Wrath()
        {
            Note(CardText("CORE_EX1_154"));
            var g = G(); var p = P1(g); var o = P2(g);
            var foe = Board(o, Yeti);
            var w = Hand(p, "CORE_EX1_154");
            Check("cannot target hero", !LegalTargets(w).Any(t => t is Hero), string.Join(",", LegalTargets(w).Select(t => t.Card.Id)));
            Play(g, w, foe, 1);
            Eq("3 damage", 2, Hp(foe)); Eq("no draw", 0, p.HandZone.Count);
            DeckTop(p, Wisp);
            Play(g, Hand(p, "CORE_EX1_154"), foe, 2);
            Eq("1 damage", 1, Hp(foe)); Eq("drew one", 1, p.HandZone.Count);
        }

        [AuditTest] static void CORE_EX1_160_PowerOfTheWild()
        {
            Note(CardText("CORE_EX1_160"));
            var g = G(); var p = P1(g); var o = P2(g);
            var a = Board(p, Wisp); var e = Board(o, Wisp);
            var card = Hand(p, "CORE_EX1_160");
            var branches = card.ChooseOnePlayables.Select(x => x.Card.Id).ToList();
            Note("branch order (client picks by sub-option entity, NativeMatch.ChooseOneBranch): " + string.Join(",", branches));
            int buff = branches.IndexOf("EX1_160b") + 1, panther = branches.IndexOf("EX1_160a") + 1;
            Check("both branches present", buff > 0 && panther > 0);
            Play(g, card, null, buff);
            Eq("Leader of the Pack: wisp 2/2", "2/2", St(a)); Eq("enemy untouched", 1, Atk(e)); Eq("no panther", 1, p.BoardZone.Count);
            Play(g, Hand(p, "CORE_EX1_160"), null, panther);
            var pan = p.BoardZone[1];
            Eq("panther", "EX1_160t", pan.Card.Id); Eq("3/2", "3/2", St(pan));
            Eq("wisp unchanged by panther branch", 2, Atk(a));
        }

        [AuditTest] static void CORE_ONY_018_Boomkin()
        {
            Note(CardText("CORE_ONY_018"));
            var g = G(); var p = P1(g); var o = P2(g);
            Damage(g, p.Hero, 12);
            Play(g, Hand(p, "CORE_ONY_018"), null, 1);
            Eq("heal hero 8", 26, Hp(p.Hero));
            Eq("boomkin 4/5", "4/5", St(p.BoardZone[0]));
            var foe = Board(o, Yeti);
            Play(g, Hand(p, "CORE_ONY_018"), foe, 2);
            Eq("4 to minion", 1, Hp(foe));
            Play(g, Hand(p, "CORE_ONY_018"), o.Hero, 2);
            Eq("4 to enemy hero", 26, Hp(o.Hero));
        }

        [AuditTest] static void DAL_350_CrystalPower()
        {
            Note(CardText("DAL_350"));
            var g = G(); var p = P1(g); var o = P2(g);
            var foe = Board(o, Golem);
            Play(g, Hand(p, "DAL_350"), foe, 1);
            Eq("2 damage to minion", 5, Hp(foe));
            var x = Hand(p, "DAL_350");
            Mana(p, 10);
            g.Process(PlayCardTask.Any(p, x, o.Hero, -1, 1));
            Eq("opt1 does not hit hero", 30, Hp(o.Hero));
            if (x.Zone == p.HandZone) p.HandZone.Remove(x);
            Board(p, "CS2_142");
            Play(g, Hand(p, "DAL_350"), foe, 1);
            Eq("spell damage +1 -> 3", 2, Hp(foe));
            Damage(g, p.Hero, 10);
            Play(g, Hand(p, "DAL_350"), p.Hero, 2);
            Eq("restore 5 to hero", 25, Hp(p.Hero));
            var mine = Board(p, Golem); Damage(g, mine, 6);
            Play(g, Hand(p, "DAL_350"), mine, 2);
            Eq("restore 5 to minion", 6, Hp(mine));
        }

        [AuditTest] static void EX1_166_KeeperOfTheGrove()
        {
            Note(CardText("EX1_166"));
            var g = G(); var p = P1(g); var o = P2(g);
            var foe = Board(o, "CS2_222"); // Stormwind Champion 6/6 aura
            var other = Board(o, Wisp);
            Play(g, Hand(p, "EX1_166"), o.Hero, 1);
            Eq("2 to hero", 28, Hp(o.Hero));
            Play(g, Hand(p, "EX1_166"), foe, 1);
            Eq("2 to minion", 4, Hp(foe));
            Play(g, Hand(p, "EX1_166"), foe, 2);
            Eq("silenced: aura gone on wisp", 1, Atk(other)); Check("silenced", foe.IsSilenced);
            Eq("keeper 2/4", "2/4", St(p.BoardZone[0]));
        }

        [AuditTest] static void GIL_188_DruidOfTheScythe()
        {
            Note(CardText("GIL_188"));
            var g = G(); var p = P1(g);
            Play(g, Hand(p, "GIL_188"), null, 1);
            var a = p.BoardZone[0]; Eq("opt1 4/2", "4/2", St(a)); Check("rush", a.IsRush); Check("no taunt", !a.HasTaunt);
            Play(g, Hand(p, "GIL_188"), null, 2);
            var b = p.BoardZone[1]; Eq("opt2 2/4", "2/4", St(b)); Check("taunt", b.HasTaunt); Check("no rush", !b.IsRush);
            Note("GIL_188t3: " + CardText("GIL_188t3"));
            var c = Board(p, "GIL_188t3"); Eq("t3 4/4", "4/4", St(c)); Check("rush+taunt", c.IsRush && c.HasTaunt);
        }

        [AuditTest] static void AT_039_SavageCombatant()
        {
            Note(CardText("AT_039"));
            var g = NewGame(s => s.PlayerHero = "HERO_08"); var p = P1(g); var o = P2(g);
            Board(p, "AT_039");
            Eq("no inspire yet", 0, Atk(p.Hero));
            Power(g, p, o.Hero);
            Eq("+2 attack after hero power", 2, Atk(p.Hero));
            Note("hero enchants: " + string.Join(",", EnchantIds(p.Hero)));
            Round(g);
            Eq("gone next turn", 0, Atk(p.Hero));
            Board(o, "AT_039");
            Power(g, p, o.Hero);
            Eq("enemy combatant does not buff me extra", 2, Atk(p.Hero));
            Eq("enemy hero not buffed", 0, Atk(o.Hero));
        }

        [AuditTest] static void CFM_816_VirmenSensei()
        {
            Note(CardText("CFM_816"));
            var g = G(); var p = P1(g); var o = P2(g);
            var beast = Board(p, "CS2_120"); // River Crocolisk 2/3 beast
            var nonbeast = Board(p, Wisp);
            var ebeast = Board(o, "CS2_120");
            var v = Hand(p, "CFM_816");
            Mana(p, 10);
            var lt = LegalTargets(v).Select(t => t.Id).ToList();
            Check("friendly beast targetable", lt.Contains(beast.Id)); Check("non-beast not", !lt.Contains(nonbeast.Id)); Check("enemy beast not", !lt.Contains(ebeast.Id));
            Play(g, v, beast);
            Eq("beast 4/5", "4/5", St(beast));
            Eq("sensei 4/5", "4/5", St((Minion)v));
            var g2 = G(); var p2 = P1(g2);
            PlayNew(g2, p2, "CFM_816");
            Eq("no beast: plays fine", 1, p2.BoardZone.Count);
        }

        [AuditTest] static void LOE_050_MountedRaptor()
        {
            Note(CardText("LOE_050"));
            for (int seed = 1; seed <= 8; seed++)
            {
                int sd = seed;
                var g = NewGame(s => { s.PlayerHero = "HERO_06"; s.Seed = sd; }); var p = P1(g);
                var r = Board(p, "LOE_050");
                Kill(g, r);
                Check($"seed {seed}: Deathrattle summoned one minion", p.BoardZone.Count == 1, Describe(g));
                if (p.BoardZone.Count == 1) Eq($"seed {seed}: 1-cost ({p.BoardZone[0].Card.Id})", 1, p.BoardZone[0].Card.Cost);
            }
            // Played from hand: the text has no Battlecry, so only the Raptor should arrive.
            var g2 = G(); var p2 = P1(g2);
            PlayNew(g2, p2, "LOE_050");
            Eq("played from hand: only the Raptor on board (no Battlecry summon)", 1, p2.BoardZone.Count);
            Note("after play: " + Describe(g2));
        }

        [AuditTest] static void OG_048_MarkOfYShaarj()
        {
            Note(CardText("OG_048"));
            var g = G(); var p = P1(g);
            var beast = Board(p, "CS2_120"); var w = Board(p, Wisp);
            DeckTop(p, Wisp); DeckTop(p, Wisp);
            Play(g, Hand(p, "OG_048"), w);
            Eq("wisp 3/3", "3/3", St(w)); Eq("non-beast: no draw", 0, p.HandZone.Count);
            Play(g, Hand(p, "OG_048"), beast);
            Eq("croc 4/5", "4/5", St(beast)); Eq("beast: drew", 1, p.HandZone.Count);
        }

        [AuditTest] static void OG_313_AddledGrizzly()
        {
            Note(CardText("OG_313"));
            var g = G(); var p = P1(g); var o = P2(g);
            var gz = Board(p, "OG_313");
            Eq("grizzly itself 2/2", "2/2", St(gz));
            var w = Board(p, Wisp);
            Eq("summoned wisp 2/2", "2/2", St(w));
            var w2 = (Minion)PlayNew(g, p, Wisp);
            Eq("played wisp 2/2", "2/2", St(w2));
            Check("OG_313e", EnchantIds(w2).Contains("OG_313e"), string.Join(",", EnchantIds(w2)));
            var e = Board(o, Wisp); Eq("enemy summon ignored", 1, Atk(e));
            Kill(g, gz); var w3 = Board(p, Wisp);
            Eq("buff kept after grizzly dies", 2, Atk(w)); Eq("new one unbuffed", 1, Atk(w3));
        }
    }
}
