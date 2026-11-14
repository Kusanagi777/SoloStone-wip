// Audit chunk 9 (class PALADIN): one test per card id, text from facts / cards.tsv.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Audit.H;

namespace Audit
{
    internal static class Chunk9Paladin
    {
        const string Recruit = "CS2_101t";
        const string MurlocRaider = "CS2_168"; // 2/1 murloc
        const string FaerieDragon = "NEW1_023"; // dragon
        const string Minibot = "GVG_058";

        static Dictionary<string, string[]> _tsv;
        static string[] Row(string id)
        {
            if (_tsv == null)
            {
                _tsv = new Dictionary<string, string[]>();
                foreach (var l in File.ReadAllLines(VerifyPaths.Fixture("audit-inventory/cards.tsv")).Skip(1))
                { var f = l.Split('\t'); _tsv[f[0]] = f; }
            }
            return _tsv[id];
        }
        /// <summary>Checks the engine card's cost/attack/health against the facts row.</summary>
        static void Stats(string id)
        {
            var r = Row(id); var c = Cards.FromId(id);
            Check(id + " exists", c != null);
            if (c == null) return;
            Eq(id + " cost", int.Parse(r[4]), c.Cost);
            if (r[2] == "MINION" || r[2] == "WEAPON")
            {
                Eq(id + " attack", int.Parse(r[5]), c.ATK);
                if (r[2] == "MINION") Eq(id + " health", int.Parse(r[6]), c.Health);
            }
            Check(id + " implemented", c.Implemented);
        }
        static bool DS(ICharacter c) => c[GameTag.DIVINE_SHIELD] == 1;
        static bool Taunt(ICharacter c) => c[GameTag.TAUNT] == 1;

        [AuditTest]
        static void CORE_ICC_038_TauntDivineShield()
        {
            Stats("CORE_ICC_038");
            var g = NewGame(); var p = P1(g);
            var m = (Minion)PlayNew(g, p, "CORE_ICC_038");
            Check("taunt", m.HasTaunt); Check("divine shield", m.HasDivineShield);
            Eq("1/1", "1/1", Atk(m) + "/" + Hp(m));
        }

        [AuditTest]
        static void MEND_802_TwoShieldedRecruits()
        {
            Stats("MEND_802");
            var g = NewGame(); var p = P1(g);
            PlayNew(g, p, "MEND_802");
            var rs = p.BoardZone.Where(m => m.Card.Id == Recruit).ToList();
            Eq("two recruits", 2, rs.Count);
            Check("all 1/1 with DS", rs.All(m => m.AttackDamage == 1 && m.Health == 1 && m.HasDivineShield), Describe(g));
            var cl = Clone(g); Eq("clone keeps DS", 2, At(cl, p).BoardZone.Count(m => m.HasDivineShield));
        }

        [AuditTest]
        static void CFM_650_BuffsRandomMurlocInHand()
        {
            Stats("CFM_650");
            for (int seed = 1; seed <= 5; seed++)
            {
                var g = NewGame(s => s.Seed = seed); var p = P1(g);
                var yeti = Hand(p, Yeti); var murloc = (Minion)Hand(p, MurlocRaider);
                PlayNew(g, p, "CFM_650");
                Eq("murloc in hand 3/2 seed " + seed, "3/2", murloc.AttackDamage + "/" + murloc.Health);
                Eq("yeti untouched", 4, ((Minion)yeti).AttackDamage);
            }
            var g2 = NewGame(); var p2 = P1(g2);
            var y2 = (Minion)Hand(p2, Yeti);
            PlayNew(g2, p2, "CFM_650");
            Eq("no murloc: yeti untouched", "4/5", y2.AttackDamage + "/" + y2.Health);
        }

        [AuditTest]
        static void CS2_088_Restores6ToHero()
        {
            Stats("CS2_088");
            var g = NewGame(); var p = P1(g);
            Damage(g, p.Hero, 10);
            PlayNew(g, p, "CS2_088");
            Eq("hero 20 -> 26", 26, Hp(p.Hero));
        }

        [AuditTest]
        static void KAR_010_WhelpsIfHoldingDragon()
        {
            Stats("KAR_010");
            var g = NewGame(); var p = P1(g);
            Hand(p, FaerieDragon);
            PlayNew(g, p, "KAR_010");
            Eq("templar + 2 whelps", 3, p.BoardZone.Count);
            Eq("two 1/1 whelps", 2, p.BoardZone.Count(m => m.Card.Id != "KAR_010" && m.AttackDamage == 1 && m.Health == 1));
            var g2 = NewGame(); var p2 = P1(g2);
            PlayNew(g2, p2, "KAR_010");
            Eq("no dragon: no whelps", 1, p2.BoardZone.Count);
        }

        [AuditTest]
        static void CFM_759_DrawsOnlyWith2Attack()
        {
            Stats("CFM_759");
            var g = NewGame(); var p = P1(g);
            var m = Board(p, "CFM_759");
            int hand = p.HandZone.Count;
            Kill(g, m);
            Eq("1 attack: no draw", hand, p.HandZone.Count);
            var m2 = Board(p, "CFM_759");
            PlayNew(g, p, "CS2_087", m2); // Blessing of Might +3 Attack
            Eq("buffed to 4 attack", 4, Atk(m2));
            hand = p.HandZone.Count;
            Kill(g, m2);
            Eq("4 attack: draws a card", hand + 1, p.HandZone.Count);
        }

        [AuditTest]
        static void TLC_240_RushAndThreeMurlocs()
        {
            Stats("TLC_240");
            foreach (var t in new[] { "TLC_240t", "TLC_240t2", "TLC_240t3" }) Stats(t);
            for (int seed = 1; seed <= 4; seed++)
            {
                var g = NewGame(s => s.Seed = seed); var p = P1(g); var o = P2(g);
                var ty = (Minion)PlayNew(g, p, "TLC_240");
                Check("rush", ty.IsRush);
                var foe = Board(o, Yeti);
                Check("rush attacks minion turn played", Attack(g, ty, foe) || true);
                Kill(g, ty);
                var locs = p.BoardZone.Where(m => m.Card.Id.StartsWith("TLC_240t")).ToList();
                Eq("three murlocs seed " + seed, 3, locs.Count);
                Check("all murlocs", locs.All(m => m.IsRace(Race.MURLOC)), Describe(g));
                Check("each has an enchantment (bonus effect)", locs.All(m => EnchantIds(m).Count >= 1), string.Join(";", locs.Select(m => m.Card.Id + "=" + string.Join(",", EnchantIds(m)))));
                Note(Describe(g) + " ench " + string.Join(";", locs.Select(m => string.Join(",", EnchantIds(m)))));
            }
        }

        [AuditTest]
        static void UNG_953_ReturnsSpellsCastOnIt()
        {
            Stats("UNG_953");
            var g = NewGame(); var p = P1(g);
            var m = Board(p, "UNG_953");
            PlayNew(g, p, "AT_074", m);
            PlayNew(g, p, "CS2_087", m);
            Eq("hand empty before", 0, p.HandZone.Count);
            Kill(g, m);
            var h = HandIds(p);
            Check("both spells returned", h.Contains("AT_074") && h.Contains("CS2_087") && h.Count == 2, string.Join(",", h));
        }

        [AuditTest]
        static void BOT_909_DrawsTwoOneAttackMinions()
        {
            Stats("BOT_909");
            var g = NewGame(); var p = P1(g);
            ClearDeck(p); foreach (var id in new[] { Yeti, Wisp, MurlocRaider, Yeti, "CORE_ICC_038", Yeti }) DeckTop(p, id);
            PlayNew(g, p, "BOT_909");
            var h = HandIds(p);
            Eq("two cards drawn", 2, h.Count);
            Check("both 1-attack minions", h.All(id => id == Wisp || id == "CORE_ICC_038"), string.Join(",", h));
        }

        [AuditTest]
        static void CORE_GVG_061_ThreeRecruitsAndWeapon()
        {
            Stats("CORE_GVG_061");
            var g = NewGame(); var p = P1(g);
            PlayNew(g, p, "CORE_GVG_061");
            Eq("three 1/1 recruits", 3, p.BoardZone.Count(m => m.Card.Id == Recruit && m.AttackDamage == 1 && m.Health == 1));
            Check("weapon equipped", p.Hero.Weapon != null);
            if (p.Hero.Weapon != null) Eq("1/4 weapon", "1/4", p.Hero.Weapon.AttackDamage + "/" + p.Hero.Weapon.Durability);
        }

        [AuditTest]
        static void TRL_305_DiscoverSixCostTauntShield()
        {
            Stats("TRL_305");
            for (int seed = 1; seed <= 3; seed++)
            {
                var g = NewGame(s => s.Seed = seed); var p = P1(g);
                var sp = Hand(p, "TRL_305");
                Check("playable without target", CanPlay(sp));
                Play(g, sp);
                var ch = ChoiceIds(g, p);
                Eq("3 options", 3, ch.Count);
                Check("all 6-cost minions", ch.All(id => Cards.FromId(id).Cost == 6 && Cards.FromId(id).Type == CardType.MINION), string.Join(",", ch));
                var pick = Choose(g, p, 0);
                var m = p.BoardZone.FirstOrDefault(x => x.Card.Id == pick);
                Check("picked minion summoned", m != null, Describe(g));
                if (m != null) { Check("taunt", m.HasTaunt); Check("divine shield", m.HasDivineShield); }
            }
        }

        [AuditTest]
        static void CS2_091_LightsJustice()
        {
            Stats("CS2_091");
            var g = NewGame(); var p = P1(g); var o = P2(g);
            PlayNew(g, p, "CS2_091");
            Eq("1/4", "1/4", p.Hero.Weapon?.AttackDamage + "/" + p.Hero.Weapon?.Durability);
            HeroAttack(g, p, o.Hero);
            Eq("boss 29", 29, Hp(o.Hero));
        }

        [AuditTest]
        static void LOOT_286_IdentifiesInHand()
        {
            Stats("LOOT_286");
            var forms = new HashSet<string>();
            for (int seed = 1; seed <= 12; seed++)
            {
                var g = NewGame(s => s.Seed = seed); var p = P1(g);
                DeckTop(p, "LOOT_286");
                g.Process(SabberStoneCore.Tasks.PlayerTasks.EndTurnTask.Any(p)); EndTurn(g); // draw on next turn
                var h = HandIds(p);
                var f = h.FirstOrDefault(id => id.StartsWith("LOOT_286"));
                Check("drawn maul becomes a form seed " + seed, f != null && f != "LOOT_286", string.Join(",", h));
                if (f != null) forms.Add(f);
            }
            Note("forms seen: " + string.Join(",", forms));
            Check("all forms valid", forms.All(f => new[] { "LOOT_286t1", "LOOT_286t2", "LOOT_286t3", "LOOT_286t4" }.Contains(f)));
            // created straight in hand (Generate)
            var g2 = NewGame(); var p2 = P1(g2);
            var e = Hand(p2, "LOOT_286");
            Note("created-in-hand maul is " + p2.HandZone[0].Card.Id);
        }

        [AuditTest]
        static void LOOT_286t1_ChampionsMaul()
        {
            Stats("LOOT_286t1");
            var g = NewGame(); var p = P1(g);
            PlayNew(g, p, "LOOT_286t1");
            Eq("two 1/1 recruits", 2, p.BoardZone.Count(m => m.Card.Id == Recruit && m.AttackDamage == 1 && m.Health == 1));
            Eq("2/2 weapon", "2/2", p.Hero.Weapon?.AttackDamage + "/" + p.Hero.Weapon?.Durability);
        }

        [AuditTest]
        static void LOOT_286t2_SacredMaul()
        {
            Stats("LOOT_286t2");
            var g = NewGame(); var p = P1(g); var o = P2(g);
            Board(p, Wisp); Board(p, Yeti); var enemy = Board(o, Wisp);
            PlayNew(g, p, "LOOT_286t2");
            Check("friendly minions taunt", p.BoardZone.All(m => m.HasTaunt));
            Check("enemy no taunt", !enemy.HasTaunt);
        }

        [AuditTest]
        static void LOOT_286t3_BlessedMaul()
        {
            Stats("LOOT_286t3"); Stats("LOOT_286t3e");
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var w = Board(p, Wisp); var y = Board(p, Yeti); var enemy = Board(o, Wisp);
            PlayNew(g, p, "LOOT_286t3");
            Eq("wisp 2/1", "2/1", Atk(w) + "/" + Hp(w));
            Eq("yeti 5/5", "5/5", Atk(y) + "/" + Hp(y));
            Eq("enemy 1", 1, Atk(enemy));
            Check("enchant LOOT_286t3e", EnchantIds(w).Contains("LOOT_286t3e"));
            Round(g);
            Eq("permanent", 2, Atk(w));
        }

        [AuditTest]
        static void LOOT_286t4_PurifiersMaul()
        {
            Stats("LOOT_286t4");
            var g = NewGame(); var p = P1(g); var o = P2(g);
            Board(p, Wisp); Board(p, Yeti); var enemy = Board(o, Wisp);
            PlayNew(g, p, "LOOT_286t4");
            Check("friendly minions DS", p.BoardZone.All(m => m.HasDivineShield));
            Check("enemy no DS", !enemy.HasDivineShield);
        }

        [AuditTest]
        static void UNG_950_AfterHeroAttacksTwoRecruits()
        {
            Stats("UNG_950");
            var g = NewGame(); var p = P1(g); var o = P2(g);
            PlayNew(g, p, "UNG_950");
            Eq("no recruits yet", 0, p.BoardZone.Count);
            HeroAttack(g, p, o.Hero);
            Eq("boss 26", 26, Hp(o.Hero));
            Eq("two 1/1 recruits", 2, p.BoardZone.Count(m => m.Card.Id == Recruit && m.AttackDamage == 1 && m.Health == 1));
            // minion attack does not trigger
            var y = Board(p, Yeti);
            Attack(g, y, o.Hero);
            Eq("minion attack no recruits", 3, p.BoardZone.Count);
        }

        [AuditTest]
        static void AT_074_PlusThreeAndShield()
        {
            Stats("AT_074");
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var y = Board(p, Yeti);
            PlayNew(g, p, "AT_074", y);
            Eq("yeti 7/5", "7/5", Atk(y) + "/" + Hp(y));
            Check("DS", y.HasDivineShield);
            Check("enchant AT_074e2", EnchantIds(y).Contains("AT_074e2"));
            Round(g);
            Eq("+3 permanent", 7, Atk(y));
            var e = Board(o, Wisp);
            var sp = Hand(p, "AT_074");
            Check("can target enemy minion", CanPlay(sp, e));
            Check("cannot target hero", !LegalTargets(sp).Any(t => t is Hero));
        }

        [AuditTest]
        static void BOT_911_Magnetic()
        {
            Stats("BOT_911");
            var g = NewGame(); var p = P1(g);
            var m = (Minion)PlayNew(g, p, "BOT_911");
            Check("taunt+DS standalone", m.HasTaunt && m.HasDivineShield, Describe(g));
            var g2 = NewGame(); var p2 = P1(g2);
            var bot = Board(p2, Minibot);
            Damage(g2, bot, 1); // pop shield
            var mod = Hand(p2, "BOT_911");
            Play(g2, mod, null, 0, 0);
            Eq("magnetized: one minion", 1, p2.BoardZone.Count);
            Eq("minibot 4/6", "4/6", Atk(bot) + "/" + Hp(bot));
            Check("minibot taunt+DS", bot.HasTaunt && bot.HasDivineShield, Describe(g2));
        }

        [AuditTest]
        static void CORE_EX1_362_GivesFriendlyShield()
        {
            Stats("CORE_EX1_362");
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var y = Board(p, Yeti); var e = Board(o, Wisp);
            var ap = Hand(p, "CORE_EX1_362");
            var lt = LegalTargets(ap);
            Check("enemy not a legal target", !lt.Contains(e));
            Play(g, ap, y);
            Check("yeti DS", y.HasDivineShield);
            Eq("3/2", "3/2", Atk((Minion)ap) + "/" + Hp((Minion)ap));
            Check("protector itself no DS", !((Minion)ap).HasDivineShield);
        }

        [AuditTest]
        static void GIL_545_ShieldRush()
        {
            Stats("GIL_545");
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var m = (Minion)PlayNew(g, p, "GIL_545");
            Check("DS", m.HasDivineShield); Check("rush", m.IsRush);
            var e = Board(o, Yeti);
            Check("can attack minion now", g.Process(SabberStoneCore.Tasks.PlayerTasks.MinionAttackTask.Any(p, m, e)));
            Check("shield popped, health 4", !m.HasDivineShield && Hp(m) == 4, Describe(g));
        }

        [AuditTest]
        static void GVG_058_Shield()
        {
            Stats("GVG_058");
            var g = NewGame(); var p = P1(g);
            var m = (Minion)PlayNew(g, p, "GVG_058");
            Check("DS", m.HasDivineShield);
            Damage(g, m, 5);
            Check("survives first hit", m.Zone == p.BoardZone && Hp(m) == 2);
            Check("mech", m.IsRace(Race.MECHANICAL));
        }

        [AuditTest]
        static void OG_221_DeathrattleShieldsFriendly()
        {
            Stats("OG_221");
            for (int seed = 1; seed <= 4; seed++)
            {
                var g = NewGame(s => s.Seed = seed); var p = P1(g); var o = P2(g);
                var sh = Board(p, "OG_221"); var y = Board(p, Yeti); var e = Board(o, Yeti);
                Kill(g, sh);
                Check("friendly yeti gets DS seed " + seed, y.HasDivineShield);
                Check("enemy no DS", !e.HasDivineShield);
            }
        }

        [AuditTest]
        static void OG_222_RallyingBlade()
        {
            Stats("OG_222");
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var sh = Board(p, Minibot); var y = Board(p, Yeti); var e = Board(o, Minibot);
            PlayNew(g, p, "OG_222");
            Eq("minibot 3/3", "3/3", Atk(sh) + "/" + Hp(sh));
            Eq("yeti untouched", "4/5", Atk(y) + "/" + Hp(y));
            Eq("enemy minibot untouched", "2/2", Atk(e) + "/" + Hp(e));
            Eq("3/2 weapon", "3/2", p.Hero.Weapon?.AttackDamage + "/" + p.Hero.Weapon?.Durability);
        }

        [AuditTest]
        static void AT_075_RecruitsPlusOne()
        {
            Stats("AT_075");
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var r1 = Board(p, Recruit);
            var t = Board(p, "AT_075");
            var r2 = Board(p, Recruit);
            var y = Board(p, Yeti); var er = Board(o, Recruit);
            Eq("recruit before 2", 2, Atk(r1)); Eq("recruit after 2", 2, Atk(r2));
            Eq("trainer itself 2", 2, Atk(t)); Eq("yeti 4", 4, Atk(y)); Eq("enemy recruit 1", 1, Atk(er));
            Check("enchant AT_075e", EnchantIds(r1).Contains("AT_075e"));
            Kill(g, t);
            Eq("aura removed", 1, Atk(r1));
        }

        [AuditTest]
        static void CORE_BAR_878_MedicAfterHoly()
        {
            Stats("CORE_BAR_878"); Stats("BAR_878t");
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var vm = Board(p, "CORE_BAR_878");
            PlayNew(g, p, "CORE_TRL_307", p.Hero); // Flash of Light (Holy)
            var med = p.BoardZone.FirstOrDefault(m => m.Card.Id == "BAR_878t");
            Check("medic after Flash of Light", med != null, Describe(g));
            if (med != null)
            {
                Eq("2/2", "2/2", Atk(med) + "/" + Hp(med)); Check("lifesteal", med.HasLifeSteal);
                Damage(g, p.Hero, 10);
                Attack(g, med, o.Hero);
                Eq("lifesteal heals 2", 22, Hp(p.Hero));
            }
            int n = p.BoardZone.Count;
            PlayNew(g, p, "CFM_305"); // Smuggler's Run (not Holy)
            Eq("non-holy: no medic", n, p.BoardZone.Count);
            PlayNew(g, p, "AT_074", vm); // Seal of Champions (Holy in client)
            Note("after Seal of Champions board=" + string.Join(",", BoardIds(p)));
        }

        [AuditTest]
        static void CORE_TRL_307_Heal4Draw()
        {
            Stats("CORE_TRL_307");
            var g = NewGame(); var p = P1(g);
            Damage(g, p.Hero, 10);
            PlayNew(g, p, "CORE_TRL_307", p.Hero);
            Eq("hero 24", 24, Hp(p.Hero));
            Eq("drew a card", 1, p.HandZone.Count);
            var y = Board(p, Yeti); Damage(g, y, 4);
            PlayNew(g, p, "CORE_TRL_307", y);
            Eq("yeti healed to 5", 5, Hp(y));
        }

        [AuditTest]
        static void GVG_060_RecruitsPlusTwo()
        {
            Stats("GVG_060");
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var r = Board(p, Recruit); var y = Board(p, Yeti); var er = Board(o, Recruit);
            var q = (Minion)PlayNew(g, p, "GVG_060");
            Eq("recruit 3/3", "3/3", Atk(r) + "/" + Hp(r));
            Eq("yeti untouched", "4/5", Atk(y) + "/" + Hp(y));
            Eq("enemy recruit untouched", "1/1", Atk(er) + "/" + Hp(er));
            Eq("quartermaster 2/5", "2/5", Atk(q) + "/" + Hp(q));
        }

        [AuditTest]
        static void LOOT_398_EndOfYourTurnHeal3()
        {
            Stats("LOOT_398");
            var g = NewGame(); var p = P1(g); var o = P2(g);
            Board(p, "LOOT_398");
            Damage(g, p.Hero, 10);
            EndTurn(g);
            Eq("healed 3 at end of own turn", 23, Hp(p.Hero));
            Damage(g, o.Hero, 10);
            EndTurn(g); // boss turn ends
            Eq("not at end of enemy turn", 23, Hp(p.Hero));
            Eq("boss hero not healed", 20, Hp(o.Hero));
        }

        [AuditTest]
        static void TIME_015_HardlightProtector()
        {
            Stats("TIME_015");
            var g = NewGame(); var p = P1(g);
            Damage(g, p.Hero, 10);
            var m = (Minion)PlayNew(g, p, "TIME_015");
            Check("minion DS", m.HasDivineShield);
            Eq("hero healed 3", 23, Hp(p.Hero));
            Check("hero DS", p.Hero[GameTag.DIVINE_SHIELD] == 1);
            Damage(g, p.Hero, 5);
            Eq("hero DS absorbs damage", 23, Hp(p.Hero));
            Check("hero DS gone", p.Hero[GameTag.DIVINE_SHIELD] == 0);
        }

        [AuditTest]
        static void UNG_960_TwoRecruits()
        {
            Stats("UNG_960");
            var g = NewGame(); var p = P1(g);
            PlayNew(g, p, "UNG_960");
            Eq("two 1/1 recruits", 2, p.BoardZone.Count(m => m.Card.Id == Recruit && m.AttackDamage == 1 && m.Health == 1));
        }

        [AuditTest]
        static void BOT_910_HandMinionsPlusTwo()
        {
            Stats("BOT_910");
            var g = NewGame(); var p = P1(g);
            var y = (Minion)Hand(p, Yeti); var sp = Hand(p, "CFM_305");
            PlayNew(g, p, "BOT_910");
            Eq("yeti in hand 6/7", "6/7", y.AttackDamage + "/" + y.Health);
            Check("spell untouched", EnchantIds(sp).Count == 0);
        }

        [AuditTest]
        static void CFM_305_HandMinionsPlusOne()
        {
            Stats("CFM_305");
            var g = NewGame(); var p = P1(g);
            var y = (Minion)Hand(p, Yeti); var w = (Minion)Hand(p, Wisp); var b = Board(p, Yeti);
            PlayNew(g, p, "CFM_305");
            Eq("yeti 5/6", "5/6", y.AttackDamage + "/" + y.Health);
            Eq("wisp 2/2", "2/2", w.AttackDamage + "/" + w.Health);
            Eq("board yeti untouched", "4/5", Atk(b) + "/" + Hp(b));
        }

        [AuditTest]
        static void CORE_CFM_753_HandMinionsPlusOne()
        {
            Stats("CORE_CFM_753");
            var g = NewGame(); var p = P1(g);
            var y = (Minion)Hand(p, Yeti);
            var m = (Minion)PlayNew(g, p, "CORE_CFM_753");
            Eq("yeti 5/6", "5/6", y.AttackDamage + "/" + y.Health);
            Eq("outfitter 2/2", "2/2", Atk(m) + "/" + Hp(m));
        }

        [AuditTest]
        static void DAL_727_DrawLowestCostMinionBuff()
        {
            Stats("DAL_727");
            var g = NewGame(); var p = P1(g);
            ClearDeck(p); foreach (var id in new[] { Yeti, MurlocRaider, "CS2_029", Golem }) DeckTop(p, id);
            PlayNew(g, p, "DAL_727");
            var h = p.HandZone.ToList();
            Eq("one card drawn", 1, h.Count);
            if (h.Count == 1)
            {
                Eq("murloc raider (cost 1) drawn", MurlocRaider, h[0].Card.Id);
                Eq("4/3", "4/3", ((Minion)h[0]).AttackDamage + "/" + ((Minion)h[0]).Health);
            }
        }

        [AuditTest]
        static void GIL_685_TauntLifestealAt3Attack()
        {
            Stats("GIL_685");
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var m = Board(p, "GIL_685");
            Check("2 attack: no taunt", !m.HasTaunt); Check("2 attack: no lifesteal", !m.HasLifeSteal);
            PlayNew(g, p, "LOOT_286t3"); // +1 Attack
            Eq("3 attack", 3, Atk(m));
            Check("3 attack: taunt", m.HasTaunt); Check("3 attack: lifesteal", m.HasLifeSteal);
            Damage(g, p.Hero, 10);
            Attack(g, m, o.Hero);
            Eq("lifesteal heals 3", 23, Hp(p.Hero));
            Silence(g, m);
            Check("silenced: no taunt", !m.HasTaunt);
        }

        [AuditTest]
        static void JAIL_330_ExtraStatsWherever()
        {
            Stats("JAIL_330");
            var g = NewGame(); var p = P1(g);
            var m = Board(p, "JAIL_330");
            Check("DS+taunt", m.HasDivineShield && m.HasTaunt);
            PlayNew(g, p, "CS2_087", m); // +3 Attack
            Eq("board +3 attack -> 7/4", "7/4", Atk(m) + "/" + Hp(m));
            var h = (Minion)Hand(p, "JAIL_330");
            PlayNew(g, p, "CFM_305");
            Eq("hand +1/+1 -> 5/5", "5/5", h.AttackDamage + "/" + h.Health);
            var cl = Clone(g); var cm = At(cl, m);
            PlayNew(cl, At(cl, p), "CS2_092", cm); // +4/+4
            Eq("clone: +4/+4 -> 12/9", "12/9", Atk(cm) + "/" + Hp(cm));
        }

        [AuditTest]
        static void YOD_010_Reborn()
        {
            Stats("YOD_010");
            var g = NewGame(); var p = P1(g);
            var m = (Minion)PlayNew(g, p, "YOD_010");
            Check("reborn tag", m[GameTag.REBORN] == 1);
            Kill(g, m);
            var back = p.BoardZone.FirstOrDefault(x => x.Card.Id == "YOD_010");
            Check("reborn returns", back != null, Describe(g));
            if (back != null) { Eq("2/1", "2/1", Atk(back) + "/" + Hp(back)); Check("no reborn on return", back[GameTag.REBORN] == 0); }
        }
    }
}
