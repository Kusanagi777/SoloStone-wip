// Audit chunk 7 (class-HUNTER): one test per card id.
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Audit.H;

namespace Audit
{
    internal static class Chunk7Hunter
    {
        static void Stats(string id, int cost, int atk, int hp, Race race = Race.INVALID)
        {
            var c = Cards.FromId(id);
            Check(id + " exists", c != null);
            if (c == null) return;
            Eq(id + " cost", cost, c.Cost);
            Eq(id + " atk", atk, c.ATK);
            Eq(id + " hp", hp, c.Health);
            if (race != Race.INVALID) Check(id + " race " + race, c.IsRace(race), "race " + c[GameTag.CARDRACE]);
        }
        static Minion BoardMinion(Game g, Controller c, string id)
        {
            var m = (Minion)PlayNew(g, c, id);
            return m;
        }
        static string Pick(Game g, Controller c, int index)
        {
            if (c.Choice == null || index < 0 || index >= c.Choice.Choices.Count) return null;
            int id = c.Choice.Choices[index]; string cardId = g.IdEntityDic[id].Card.Id;
            if (!g.TryAnswer(new SabberStoneCore.Model.Decisions.DecisionAnswer(c.Choice.DecisionId, c.Id, new[] { id }), out string err))
                Check("answer accepted", false, err);
            return cardId;
        }
        static int EnemyTotal(Controller o) => o.Hero.Health + o.BoardZone.Sum(m => m.Health);

        [AuditTest]
        static void OG_325_VanillaBeast()
        {
            Stats("OG_325", 3, 2, 5, Race.BEAST);
            var g = NewGame(); var m = BoardMinion(g, P1(g), "OG_325");
            Eq("on board", 1, P1(g).BoardZone.Count); Eq("atk", 2, Atk(m)); Eq("hp", 5, Hp(m));
        }

        [AuditTest]
        static void CATA_554_SetsEnemyHealthTo1()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var a = Board(o, Yeti); var b = Board(o, Yeti); var mine = Board(p, Yeti);
            Damage(g, a, 2);
            var spell = Hand(p, "CATA_554");
            Check("cannot target friendly", !CanPlay(spell, mine));
            Check("cannot target hero", !CanPlay(spell, o.Hero));
            Play(g, spell, a);
            Eq("target health 1", 1, Hp(a)); Eq("target max 1", 1, MaxHp(a));
            Eq("other untouched (no dragon)", 5, Hp(b));
            Check("no choice open", p.Choice == null);
            // with a Dragon in hand: pick another
            var g2 = NewGame(); var p2 = P1(g2); var o2 = P2(g2);
            var a2 = Board(o2, Yeti); var b2 = Board(o2, Golem); var c2 = Board(o2, Wisp);
            Hand(p2, "NEW1_023");
            var s2 = Hand(p2, "CATA_554");
            Play(g2, s2, a2);
            Eq("first set to 1", 1, Hp(a2));
            var ch = ChoiceIds(g2, p2);
            Check("choice offers the other enemy minions", ch.Count == 2 && ch.Contains(Golem) && ch.Contains(Wisp), string.Join(",", ch));
            Pick(g2, p2, ch.IndexOf(Golem));
            Eq("second set to 1", 1, Hp(b2)); Eq("second max 1", 1, MaxHp(b2));
            Check("clone ok", At(Clone(g2), b2).Health == 1);
        }

        [AuditTest]
        static void CORE_UNG_912_AddsRandomBeast()
        {
            Stats("CORE_UNG_912", 1, 1, 2, Race.BEAST);
            for (int seed = 1; seed <= 8; seed++)
            {
                var g = NewGame(s => s.Seed = seed); var p = P1(g);
                PlayNew(g, p, "CORE_UNG_912");
                Eq("seed " + seed + " one card", 1, p.HandZone.Count);
                if (p.HandZone.Count == 1) Check("seed " + seed + " is beast", p.HandZone[0].Card.IsRace(Race.BEAST) && p.HandZone[0].Card.Type == CardType.MINION, p.HandZone[0].Card.Id);
            }
        }

        [AuditTest]
        static void LOE_020_PullsOneCostFromEachDeck()
        {
            Stats("LOE_020", 3, 2, 4, Race.BEAST);
            var g = NewGame();
            var p = P1(g); var o = P2(g);
            Eq("raider is 1-cost", 1, Cards.FromId("CS2_168").Cost); DeckTop(p, "CS2_168"); DeckTop(o, "CS2_168"); DeckTop(p, Wisp);
            int pd = p.DeckZone.Count, od = o.DeckZone.Count;
            PlayNew(g, p, "LOE_020");
            Check("my raider summoned", BoardIds(p).Contains("CS2_168") && !BoardIds(p).Contains(Wisp), Describe(g));
            Check("enemy raider summoned", BoardIds(o).Contains("CS2_168"), Describe(g));
            Eq("my deck -1", pd - 1, p.DeckZone.Count); Eq("op deck -1", od - 1, o.DeckZone.Count);
        }

        [AuditTest]
        static void TIME_605_RushElusiveCopy()
        {
            Stats("TIME_605", 6, 3, 4);
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var m = (Minion)PlayNew(g, p, "TIME_605");
            Eq("two on board", 2, p.BoardZone.Count);
            foreach (Minion x in p.BoardZone)
            {
                Eq("is stalker", "TIME_605", x.Card.Id);
                Check("rush", x.IsRush); Check("elusive spells", x.CantBeTargetedBySpells); Check("elusive hp", x.CantBeTargetedByHeroPowers);
                Eq("atk", 3, Atk(x)); Eq("hp", 4, Hp(x));
            }
            var foe = Board(o, Wisp);
            var copy = (Minion)p.BoardZone.First(x => x != m);
            Check("copy can rush-attack minion", g.Process(SabberStoneCore.Tasks.PlayerTasks.MinionAttackTask.Any(p, copy, foe)), Describe(g));
            var bolt = Hand(o, "CS2_029");
            Check("enemy spell cannot target", !LegalTargets(bolt).Contains(m));
        }

        [AuditTest]
        static void CFM_315_SummonsCat()
        {
            Stats("CFM_315", 1, 1, 1, Race.BEAST);
            Stats("CFM_315t", 1, 1, 1, Race.BEAST);
            var g = NewGame(); var p = P1(g);
            PlayNew(g, p, "CFM_315");
            Check("cat summoned", BoardIds(p).SequenceEqual(new[] { "CFM_315", "CFM_315t" }), string.Join(",", BoardIds(p)));
        }

        [AuditTest]
        static void CFM_315t_Tabbycat()
        {
            var g = NewGame(); var m = Board(P1(g), "CFM_315t");
            Eq("1 atk", 1, Atk(m)); Eq("1 hp", 1, Hp(m)); Check("beast", m.Card.IsRace(Race.BEAST));
        }

        [AuditTest]
        static void CORE_AV_337_TauntTwoCubs()
        {
            Stats("CORE_AV_337", 7, 5, 6, Race.BEAST);
            var g = NewGame(); var p = P1(g);
            var w1 = Board(p, Wisp); var bear = Board(p, "CORE_AV_337"); var w2 = Board(p, Wisp);
            Check("taunt", bear.HasTaunt);
            Kill(g, bear);
            Check("cubs in place", BoardIds(p).SequenceEqual(new[] { Wisp, "AV_337t", "AV_337t", Wisp }), string.Join(",", BoardIds(p)));
            foreach (Minion c in p.BoardZone.Where(x => x.Card.Id == "AV_337t")) { Eq("cub atk", 2, Atk(c)); Eq("cub hp", 4, Hp(c)); Check("cub taunt", c.HasTaunt); }
        }

        [AuditTest]
        static void AV_337t_Cub()
        {
            Stats("AV_337t", 3, 2, 4, Race.BEAST);
            var g = NewGame(); var m = Board(P1(g), "AV_337t"); Check("taunt", m.HasTaunt);
        }

        [AuditTest]
        static void LOOT_078_HitsAdjacent()
        {
            Stats("LOOT_078", 3, 2, 4, Race.BEAST);
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var a = Board(o, Yeti); var b = Board(o, Yeti); var c = Board(o, Yeti); var far = Board(o, Yeti);
            var h = Board(p, "LOOT_078");
            Attack(g, h, b);
            Eq("target", 3, Hp(b)); Eq("left", 3, Hp(a)); Eq("right", 3, Hp(c)); Eq("far untouched", 5, Hp(far));
            // kill-the-target case: adjacent still damaged
            var g2 = NewGame(); var p2 = P1(g2); var o2 = P2(g2);
            var l = Board(o2, Yeti); var w = Board(o2, Wisp); var r = Board(o2, Yeti);
            var h2 = Board(p2, "LOOT_078");
            Attack(g2, h2, w);
            Check("wisp dead", w.IsDead || !o2.BoardZone.Contains(w));
            Eq("left hit when target dies", 3, Hp(l)); Eq("right hit when target dies", 3, Hp(r));
            // attacking hero: nothing else hit
            var g3 = NewGame(); var p3 = P1(g3); var o3 = P2(g3);
            var y = Board(o3, Yeti); var h3 = Board(p3, "LOOT_078");
            Attack(g3, h3, o3.Hero);
            Eq("hero hit", 28, o3.Hero.Health); Eq("minion untouched", 5, Hp(y));
        }

        [AuditTest]
        static void CFM_316_RatsEqualAttack()
        {
            Stats("CFM_316", 3, 2, 2, Race.BEAST);
            var g = NewGame(); var p = P1(g);
            var r = Board(p, "CFM_316");
            Kill(g, r);
            Eq("2 rats", 2, BoardIds(p).Count(x => x == "CFM_316t"));
            var g2 = NewGame(); var p2 = P1(g2);
            var r2 = Board(p2, "CFM_316");
            PlayNew(g2, p2, "CS2_092", r2); // Blessing of Kings +4/+4
            Eq("atk 6", 6, Atk(r2));
            Kill(g2, r2);
            Eq("6 rats", 6, BoardIds(p2).Count(x => x == "CFM_316t"));
            foreach (Minion m in p2.BoardZone) { Eq("rat 1 atk", 1, Atk(m)); Eq("rat 1 hp", 1, Hp(m)); }
        }

        [AuditTest]
        static void CORE_FP1_011_DeathrattleBeast()
        {
            Stats("CORE_FP1_011", 1, 1, 1, Race.BEAST);
            for (int seed = 1; seed <= 6; seed++)
            {
                var g = NewGame(s => s.Seed = seed); var p = P1(g);
                var m = Board(p, "CORE_FP1_011");
                Kill(g, m);
                Eq("seed " + seed + " one card", 1, p.HandZone.Count);
                if (p.HandZone.Count == 1) Check("beast minion", p.HandZone[0].Card.IsRace(Race.BEAST) && p.HandZone[0].Card.Type == CardType.MINION, p.HandZone[0].Card.Id);
            }
        }

        [AuditTest]
        static void ICC_825_ResummonsFriendlyBeast()
        {
            Stats("ICC_825", 7, 6, 7);
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var eb = Board(o, "CFM_315t"); Kill(g, eb); // enemy beast died
            var bow = Board(p, "ICC_825");
            Kill(g, bow);
            Eq("no friendly beast died -> nothing", 0, p.BoardZone.Count);
            var g2 = NewGame(); var p2 = P1(g2);
            var y = Board(p2, Yeti); Kill(g2, y);
            var beast = Board(p2, "OG_325"); Kill(g2, beast);
            var bow2 = Board(p2, "ICC_825"); Kill(g2, bow2);
            Check("summoned the beast", BoardIds(p2).SequenceEqual(new[] { "OG_325" }), string.Join(",", BoardIds(p2)));
        }

        [AuditTest]
        static void MEND_307_RoamFree()
        {
            var g = NewGame(); var p = P1(g);
            PlayNew(g, p, "MEND_307");
            var ch = ChoiceIds(g, p);
            Eq("3 options", 3, ch.Count);
            foreach (var id in ch) { var c = Cards.FromId(id); Check("option beast cost 5: " + id, c.IsRace(Race.BEAST) && c.Cost == 5 && c.Type == CardType.MINION, c.Cost + ""); }
            string pick = Pick(g, p, 1);
            Check("picked summoned", BoardIds(p).SequenceEqual(new[] { pick }), string.Join(",", BoardIds(p)) + " pick " + pick);
            // future Animal Companion uses the new roster
            for (int i = 0; i < 3; i++)
            {
                int before = p.BoardZone.Count;
                PlayNew(g, p, "CORE_NEW1_031");
                if (p.Choice != null) Pick(g, p, 0);
                Eq("companion summoned", before + 1, p.BoardZone.Count);
                var last = p.BoardZone.Last().Card;
                Check("companion from new roster: " + last.Id, ch.Contains(last.Id), string.Join(",", ch));
            }
        }

        [AuditTest]
        static void UNG_917_Dinomancy()
        {
            var g = NewGame(s => s.PlayerHero = "HERO_05"); var p = P1(g);
            PlayNew(g, p, "UNG_917");
            Eq("hero power replaced", "UNG_917t1", p.Hero.HeroPower.Card.Id);
        }

        [AuditTest]
        static void UNG_917e_PlusTwo()
        {
            var g = NewGame(s => { s.PlayerHero = "HERO_05"; s.PlayerPower = "UNG_917t1"; }); var p = P1(g);
            var b = Board(p, "OG_325");
            Power(g, p, b);
            Check("enchant attached", EnchantIds(b).Contains("UNG_917e"), string.Join(",", EnchantIds(b)));
            Eq("atk 4", 4, Atk(b)); Eq("hp 7", 7, Hp(b));
        }

        [AuditTest]
        static void UNG_917t1_BeastOnly()
        {
            var g = NewGame(s => { s.PlayerHero = "HERO_05"; s.PlayerPower = "UNG_917t1"; }); var p = P1(g); var o = P2(g);
            Eq("power", "UNG_917t1", p.Hero.HeroPower.Card.Id);
            var y = Board(p, Yeti); var eb = Board(o, "OG_325");
            Mana(p, 10); p.Hero.HeroPower.IsExhausted = false;
            var opts = p.Options().OfType<SabberStoneCore.Tasks.PlayerTasks.HeroPowerTask>().Select(t => t.Target).ToList();
            Check("yeti not a target", !opts.Contains(y));
            Check("enemy beast is a target (Give a Beast)", opts.Contains(eb));
            Power(g, p, eb);
            Eq("enemy beast +2 atk", 4, Atk(eb)); Eq("enemy beast +2 hp", 7, Hp(eb));
        }

        [AuditTest]
        static void BOT_020_MagneticRush()
        {
            Stats("BOT_020", 1, 1, 1, Race.MECHANICAL);
            var g = NewGame(); var p = P1(g);
            var s = BoardMinion(g, p, "BOT_020");
            Check("rush", s.IsRush);
            var g2 = NewGame(); var p2 = P1(g2);
            var mech = Board(p2, "BOT_445t");
            var sk = Hand(p2, "BOT_020");
            Play(g2, sk, null, 0, 0);
            Eq("merged: one minion", 1, p2.BoardZone.Count);
            Eq("mech 2 atk", 2, Atk(mech)); Eq("mech 2 hp", 2, Hp(mech)); Check("mech gains rush", mech.IsRush);
        }

        [AuditTest]
        static void DS1_175_TimberWolfAura()
        {
            Stats("DS1_175", 1, 1, 1, Race.BEAST);
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var wolf = Board(p, "DS1_175"); var b = Board(p, "OG_325"); var y = Board(p, Yeti); var eb = Board(o, "OG_325");
            Eq("wolf itself", 1, Atk(wolf)); Eq("beast +1", 3, Atk(b)); Eq("yeti none", 4, Atk(y)); Eq("enemy beast none", 2, Atk(eb));
            Check("DS1_175o on beast", EnchantIds(b).Contains("DS1_175o") || b.AttackDamage == 3);
            Kill(g, wolf);
            Eq("aura gone", 2, Atk(b));
        }

        [AuditTest]
        static void DS1_175o_Enchant()
        {
            var g = NewGame(); var p = P1(g);
            Board(p, "DS1_175"); var b = Board(p, "CFM_315t");
            Eq("+1 attack", 2, Atk(b)); Eq("no health", 1, Hp(b));
            Eq("clone keeps", 2, At(Clone(g), b).AttackDamage);
        }

        [AuditTest]
        static void DS1_178_BeastsHaveCharge()
        {
            Stats("DS1_178", 5, 2, 5, Race.BEAST);
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var rhino = Board(p, "DS1_178");
            var b = Board(p, "OG_325"); var y = Board(p, Yeti); var eb = Board(o, "OG_325");
            Check("rhino charge", rhino.HasCharge); Check("beast charge", b.HasCharge); Check("yeti no charge", !y.HasCharge); Check("enemy beast no charge", !eb.HasCharge);
            Check("fresh beast can attack hero", g.Process(SabberStoneCore.Tasks.PlayerTasks.MinionAttackTask.Any(p, b, o.Hero)), Describe(g));
            Eq("hero damaged", 28, o.Hero.Health);
        }

        [AuditTest]
        static void DS1_178e_Enchant()
        {
            var g = NewGame(); var p = P1(g);
            var rhino = Board(p, "DS1_178"); var b = Board(p, "CFM_315t");
            Check("charge on beast", b.HasCharge);
            Kill(g, rhino);
            Check("charge removed after rhino dies", !b.HasCharge);
        }

        [AuditTest]
        static void EX1_531_HyenaGrows()
        {
            Stats("EX1_531", 2, 2, 2, Race.BEAST);
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var hy = Board(p, "EX1_531");
            var b = Board(p, "CFM_315t"); Kill(g, b);
            Eq("atk +2", 4, Atk(hy)); Eq("hp +1", 3, Hp(hy));
            var y = Board(p, Yeti); Kill(g, y);
            Eq("non-beast no", 4, Atk(hy));
            var eb = Board(o, "CFM_315t"); Kill(g, eb);
            Eq("enemy beast no", 4, Atk(hy));
        }

        [AuditTest]
        static void EX1_531e_Enchant()
        {
            var g = NewGame(); var p = P1(g);
            var hy = Board(p, "EX1_531");
            Kill(g, Board(p, "CFM_315t")); Kill(g, Board(p, "CFM_315t"));
            Eq("stacks atk", 6, Atk(hy)); Eq("stacks hp", 4, Hp(hy));
            Eq("clone", 6, At(Clone(g), hy).AttackDamage);
        }

        [AuditTest]
        static void OG_216_TwoSpiders()
        {
            Stats("OG_216", 4, 3, 3, Race.BEAST);
            var g = NewGame(); var p = P1(g);
            var w = Board(p, "OG_216"); Kill(g, w);
            Check("two spiders", BoardIds(p).SequenceEqual(new[] { "OG_216a", "OG_216a" }), string.Join(",", BoardIds(p)));
        }

        [AuditTest]
        static void OG_216a_Spider()
        {
            Stats("OG_216a", 1, 1, 1, Race.BEAST);
        }

        [AuditTest]
        static void UNG_915_AdaptBeast()
        {
            Stats("UNG_915", 2, 3, 2, Race.BEAST);
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var b = Board(p, "OG_325"); var y = Board(p, Yeti); var eb = Board(o, "OG_325");
            var rz = Hand(p, "UNG_915");
            var t = LegalTargets(rz);
            Check("friendly beast target", t.Contains(b)); Check("yeti not", !t.Contains(y)); Check("enemy beast not", !t.Contains(eb));
            Play(g, rz, b);
            var ch = ChoiceIds(g, p);
            Eq("3 adapt options", 3, ch.Count);
            int a0 = Atk(b), h0 = Hp(b), e0 = EnchantIds(b).Count;
            Pick(g, p, 0);
            Check("adapted", EnchantIds(b).Count > e0 || Atk(b) != a0 || Hp(b) != h0 || b.HasTaunt || b.HasDivineShield || b.HasStealth || b.Poisonous || b.HasWindfury, string.Join(",", EnchantIds(b)) + " " + string.Join(",", ch));
            // no beast: playable with no target
            var g2 = NewGame(); var p2 = P1(g2);
            var r2 = Hand(p2, "UNG_915");
            Check("playable without beast", CanPlay(r2));
        }

        static void MechBuff(string id, string ench, int cost, int atk, int hp)
        {
            Stats(id, cost, atk, hp, Race.MECHANICAL);
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var mech = Board(p, "BOT_445t"); var y = Board(p, Yeti); var em = Board(o, "BOT_445t");
            var c = Hand(p, id);
            var t = LegalTargets(c);
            Check("mech target", t.Contains(mech)); Check("yeti not", !t.Contains(y)); Check("enemy mech not", !t.Contains(em));
            Play(g, c, mech);
            Eq("mech +1 atk", 2, Atk(mech)); Eq("mech +1 hp", 2, Hp(mech));
            Check(ench + " attached", EnchantIds(mech).Contains(ench), string.Join(",", EnchantIds(mech)));
            Check("no self buff", Atk((Minion)c) == atk);
        }

        [AuditTest] static void AT_096_ClockworkKnight() => MechBuff("AT_096", "AT_096e", 5, 5, 5);
        [AuditTest] static void AT_096e_Enchant()
        {
            var g = NewGame(); var p = P1(g); var mech = Board(p, "BOT_445t");
            PlayNew(g, p, "AT_096", mech); Eq("clone keeps +1 atk", 2, At(Clone(g), mech).AttackDamage);
        }

        [AuditTest]
        static void BOT_035_MagneticPoisonous()
        {
            Stats("BOT_035", 2, 2, 2, Race.MECHANICAL);
            var g = NewGame(); var p = P1(g);
            var v = BoardMinion(g, p, "BOT_035"); Check("poisonous", v.Poisonous);
            var g2 = NewGame(); var p2 = P1(g2);
            var mech = Board(p2, "BOT_445t");
            Play(g2, Hand(p2, "BOT_035"), null, 0, 0);
            Eq("merged", 1, p2.BoardZone.Count); Eq("3 atk", 3, Atk(mech)); Eq("3 hp", 3, Hp(mech)); Check("mech poisonous", mech.Poisonous);
        }

        [AuditTest] static void BOT_079_FaithfulLumi() => MechBuff("BOT_079", "BOT_079e", 1, 1, 1);
        [AuditTest] static void BOT_079e_Enchant()
        {
            var g = NewGame(); var p = P1(g); var mech = Board(p, "BOT_445t");
            PlayNew(g, p, "BOT_079", mech); Eq("+1 hp", 2, Hp(mech)); Eq("clone", 2, At(Clone(g), mech).Health);
        }

        [AuditTest]
        static void BOT_445_Mecharoo()
        {
            Stats("BOT_445", 1, 1, 1, Race.MECHANICAL);
            var g = NewGame(); var p = P1(g);
            Kill(g, Board(p, "BOT_445"));
            Check("jo-e", BoardIds(p).SequenceEqual(new[] { "BOT_445t" }), string.Join(",", BoardIds(p)));
        }

        [AuditTest] static void BOT_445t_JoEBot() => Stats("BOT_445t", 1, 1, 1, Race.MECHANICAL);

        [AuditTest]
        static void BOT_603_SteelRager()
        {
            Stats("BOT_603", 4, 5, 1, Race.MECHANICAL);
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var m = BoardMinion(g, p, "BOT_603"); Check("rush", m.IsRush);
            var foe = Board(o, Wisp);
            Check("can rush minion", g.Process(SabberStoneCore.Tasks.PlayerTasks.MinionAttackTask.Any(p, m, foe)));
            var m2 = BoardMinion(g, p, "BOT_603");
            Check("cannot hit hero same turn", !g.Process(SabberStoneCore.Tasks.PlayerTasks.MinionAttackTask.Any(p, m2, o.Hero)) || o.Hero.Health == 30, Describe(g));
        }

        [AuditTest]
        static void DAL_604_DrawsMech()
        {
            Stats("DAL_604", 3, 3, 3, Race.MECHANICAL);
            var g = NewGame();
            var p = P1(g); DeckTop(p, "BOT_445"); DeckTop(p, Yeti);
            Kill(g, Board(p, "DAL_604"));
            Check("drew the mech", HandIds(p).SequenceEqual(new[] { "BOT_445" }), string.Join(",", HandIds(p)));
            var g2 = NewGame(); var p2 = P1(g2);
            Kill(g2, Board(p2, "DAL_604"));
            Eq("no mech -> no draw", 0, p2.HandZone.Count);
        }

        [AuditTest]
        static void GVG_048_OtherMechsPlus2()
        {
            Stats("GVG_048", 3, 3, 3, Race.MECHANICAL);
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var mech = Board(p, "BOT_445t"); var y = Board(p, Yeti); var em = Board(o, "BOT_445t");
            var l = (Minion)PlayNew(g, p, "GVG_048");
            Eq("friendly mech +2", 3, Atk(mech)); Eq("yeti none", 4, Atk(y)); Eq("enemy mech none", 1, Atk(em));
            Eq("leaper itself not buffed (other)", 3, Atk(l));
        }

        [AuditTest]
        static void TIME_603_DestroysRandomEnemy()
        {
            Stats("TIME_603", 2, 1, 1);
            for (int seed = 1; seed <= 6; seed++)
            {
                var g = NewGame(s => s.Seed = seed); var p = P1(g); var o = P2(g);
                var mine = Board(p, Golem);
                Board(o, Golem); Board(o, Yeti);
                Kill(g, Board(p, "TIME_603"));
                Eq("seed " + seed + " one enemy destroyed", 1, o.BoardZone.Count);
                Check("friendly safe", p.BoardZone.Contains(mine));
                Eq("hero untouched", 30, o.Hero.Health);
            }
            var g2 = NewGame(); Kill(g2, Board(P1(g2), "TIME_603")); Check("no enemies ok", g2.State == State.RUNNING);
        }

        [AuditTest]
        static void CORE_BT_201_SplitAttack()
        {
            Stats("CORE_BT_201", 3, 2, 4);
            for (int seed = 1; seed <= 6; seed++)
            {
                var g = NewGame(s => s.Seed = seed); var p = P1(g); var o = P2(g);
                var mine = Board(p, Yeti);
                Board(o, Golem); Board(o, Golem);
                var pc = Board(p, "CORE_BT_201");
                PlayNew(g, p, "CS2_092", pc); // +4/+4 -> 6 attack
                int before = EnemyTotal(o);
                Kill(g, pc);
                Eq("seed " + seed + " 6 damage split", before - 6, EnemyTotal(o));
                Eq("friendly untouched", 5, Hp(mine)); Eq("my hero untouched", 30, p.Hero.Health);
            }
        }

        [AuditTest]
        static void GIL_562_PoisonRush()
        {
            Stats("GIL_562", 5, 1, 3, Race.BEAST);
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var m = BoardMinion(g, p, "GIL_562");
            Check("poisonous", m.Poisonous); Check("rush", m.IsRush);
            var foe = Board(o, Golem);
            g.Process(SabberStoneCore.Tasks.PlayerTasks.MinionAttackTask.Any(p, m, foe));
            Check("golem destroyed", !o.BoardZone.Contains(foe), Describe(g));
        }

        [AuditTest]
        static void ICC_021_TwoToEnemyMinions()
        {
            Stats("ICC_021", 4, 2, 1, Race.BEAST);
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var mine = Board(p, Yeti); var a = Board(o, Yeti); var b = Board(o, Golem);
            Kill(g, Board(p, "ICC_021"));
            Eq("enemy a", 3, Hp(a)); Eq("enemy b", 5, Hp(b)); Eq("friendly", 5, Hp(mine)); Eq("enemy hero", 30, o.Hero.Health);
        }

        [AuditTest]
        static void ICC_049_ToxicArrow()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var a = Board(o, Yeti);
            var s = Hand(p, "ICC_049");
            Check("cannot target hero", !CanPlay(s, o.Hero));
            Play(g, s, a);
            Eq("2 damage", 3, Hp(a)); Check("survivor poisonous", a.Poisonous);
            var w = Board(o, "CS2_172"); // Bloodfen Raptor 3/2
            PlayNew(g, p, "ICC_049", w);
            Check("killed", !o.BoardZone.Contains(w));
            // spell damage
            var g2 = NewGame(); var p2 = P1(g2); var o2 = P2(g2);
            Board(p2, "CS2_197"); // Ogre Magi, Spell Damage +1
            var y = Board(o2, Yeti);
            PlayNew(g2, p2, "ICC_049", y);
            Eq("3 damage with spell damage", 2, Hp(y)); Check("poisonous", y.Poisonous);
            var fr = Board(p2, Yeti);
            Check("can target friendly minion", CanPlay(Hand(p2, "ICC_049"), fr));
        }

        [AuditTest]
        static void KAR_005_BigBadWolf()
        {
            Stats("KAR_005", 2, 1, 1, Race.BEAST);
            Stats("KAR_005a", 2, 3, 2, Race.BEAST);
            var g = NewGame(); var p = P1(g);
            Kill(g, Board(p, "KAR_005"));
            Check("wolf", BoardIds(p).SequenceEqual(new[] { "KAR_005a" }), string.Join(",", BoardIds(p)));
            var w = p.BoardZone[0]; Eq("3 atk", 3, Atk(w)); Eq("2 hp", 2, Hp(w));
        }

        [AuditTest] static void KAR_005a_Wolf() { var g = NewGame(); var w = Board(P1(g), "KAR_005a"); Eq("3/2", "3/2", Atk(w) + "/" + Hp(w)); }

        [AuditTest]
        static void OG_179_OneToRandomEnemy()
        {
            Stats("OG_179", 1, 2, 1, Race.BEAST);
            bool hitHero = false, hitMinion = false;
            for (int seed = 1; seed <= 12; seed++)
            {
                var g = NewGame(s => s.Seed = seed); var p = P1(g); var o = P2(g);
                var mine = Board(p, Yeti); Board(o, Yeti);
                int before = EnemyTotal(o);
                Kill(g, Board(p, "OG_179"));
                Eq("seed " + seed + " 1 damage to enemies", before - 1, EnemyTotal(o));
                Eq("friendly safe", 5, Hp(mine)); Eq("own hero safe", 30, p.Hero.Health);
                if (o.Hero.Health == 29) hitHero = true; else hitMinion = true;
            }
            Check("can hit hero and minion", hitHero && hitMinion, $"hero {hitHero} minion {hitMinion}");
        }
    }
}
