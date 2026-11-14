// Audit chunk 12 (class-SHAMAN): one test (or more) per card id.
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Audit.H;

namespace Audit
{
    internal static class ShamanChunk12
    {
        static Game G() => NewGame(s => s.PlayerHero = "HERO_02");

        static void OverloadCheck(string id, int expected, IPlayable target = null)
        {
            var g = G(); var p = P1(g);
            var e = Hand(p, id);
            bool ok = Play(g, e, target);
            Check(id + " played", ok, Describe(g));
            Eq(id + " overload owed", expected, p.OverloadOwed);
            Round(g);
            Eq(id + " overload locked next turn", expected, p.OverloadLocked);
        }

        // Can attack exactly twice in one turn (Windfury), third attack refused.
        static void WindfuryAttacksTwice(Game g, Minion m, string label)
        {
            var o = P2(g);
            m.IsExhausted = false; m.NumAttacksThisTurn = 0;
            int hp0 = o.Hero.Health;
            bool a1 = g.Process(MinionAttackTask.Any(m.Controller, m, o.Hero));
            bool a2 = g.Process(MinionAttackTask.Any(m.Controller, m, o.Hero));
            int hp2 = o.Hero.Health;
            bool a3 = g.Process(MinionAttackTask.Any(m.Controller, m, o.Hero));
            Check(label + " first attack", a1);
            Check(label + " second attack", a2);
            Eq(label + " two hits of damage", hp0 - 2 * m.AttackDamage, hp2);
            Eq(label + " no third hit", hp2, o.Hero.Health);
        }

        static void VanillaWindfury(string id, int atk, int hp)
        {
            var g = G(); var p = P1(g);
            var m = (Minion)PlayNew(g, p, id);
            Eq(id + " atk", atk, Atk(m)); Eq(id + " hp", hp, Hp(m));
            Check(id + " has Windfury", m.HasWindfury);
            WindfuryAttacksTwice(g, m, id);
        }

        [AuditTest] static void CORE_WC_042_GainsAttackAfterElemental()
        {
            var g = G(); var p = P1(g);
            var v = Board(p, "CORE_WC_042");
            Eq("base 1/3", 1, Atk(v)); Eq("hp 3", 3, Hp(v));
            PlayNew(g, p, Yeti);
            Eq("non-Elemental: no buff", 1, Atk(v));
            PlayNew(g, p, "UNG_809t1");
            Eq("after Elemental +1 Attack", 2, Atk(v));
            Check("WC_042e applied", EnchantIds(v).Contains("WC_042e"), string.Join(",", EnchantIds(v)));
            PlayNew(g, p, "UNG_809t1");
            Eq("stacks: +2", 3, Atk(v));
            var o = P2(g);
            EndTurn(g);
            PlayNew(g, o, "UNG_809t1");
            Eq("enemy Elemental: no buff", 3, Atk(v));
            var c = Clone(g); Eq("clone keeps attack", 3, Atk(At(c, v)));
        }

        [AuditTest] static void WC_042e_PlusOneAttack()
        {
            var g = G(); var p = P1(g);
            var v = Board(p, "CORE_WC_042");
            PlayNew(g, p, "UNG_809t1");
            Eq("health unchanged", 3, MaxHp(v));
            Eq("+1 Attack", 2, Atk(v));
            Round(g);
            Eq("permanent across turns", 2, Atk(v));
        }

        [AuditTest] static void TLC_482_SummonsCinders_KindredTriggers()
        {
            Note("TLC_249: " + CardText("TLC_249"));
            // No kindred
            {
                var g = G(); var p = P1(g); var o = P2(g);
                int hp0 = o.Hero.Health;
                var s = (Minion)PlayNew(g, p, "TLC_482");
                Eq("3/4", 3, Atk(s)); Eq("hp 4", 4, Hp(s));
                var cinders = p.BoardZone.Where(m => m.Card.Id == "TLC_249").ToList();
                Eq("two cinders", 2, cinders.Count);
                Check("cinders 2/1", cinders.All(c => c.AttackDamage == 2 && c.Health == 1), Describe(g));
                Eq("no kindred: no deathrattle damage", hp0, o.Hero.Health);
            }
            // Kindred: Elemental last turn (Slagclaw elemental?)
            {
                var g = G(); var p = P1(g); var o = P2(g);
                Note("TLC_482 elemental " + Cards.FromId("TLC_482").IsRace(Race.ELEMENTAL));
                PlayNew(g, p, "UNG_809t1");
                Round(g); ClearHand(p);
                int hp0 = o.Hero.Health;
                PlayNew(g, p, "TLC_482");
                var cinders = p.BoardZone.Where(m => m.Card.Id == "TLC_249").ToList();
                Eq("two cinders stay (triggered, not killed)", 2, cinders.Count);
                Check("kindred: enemy hero took deathrattle damage", o.Hero.Health < hp0, $"before {hp0} after {o.Hero.Health} " + Describe(g));
                Note("kindred damage dealt " + (hp0 - o.Hero.Health));
            }
        }

        [AuditTest] static void CS3_007_SpellDamageAndOverload()
        {
            var g = G(); var p = P1(g); var o = P2(g);
            var z = (Minion)PlayNew(g, p, "CS3_007");
            Eq("3/2", 3, Atk(z)); Eq("hp2", 2, Hp(z));
            Eq("spell power +1", 1, p.CurrentSpellPower);
            Eq("overload 1", 1, p.OverloadOwed);
            int hp0 = o.Hero.Health;
            PlayNew(g, p, "EX1_238", o.Hero);
            Eq("Lightning Bolt deals 4", hp0 - 4, o.Hero.Health);
        }

        [AuditTest] static void EX1_243_WindfuryOverload2()
        {
            VanillaWindfury("EX1_243", 3, 1);
            OverloadCheck("EX1_243", 2);
        }

        [AuditTest] static void UNG_845_DeathrattleAddsTwoFlameElementals()
        {
            var g = G(); var p = P1(g);
            var m = (Minion)PlayNew(g, p, "UNG_845");
            Eq("2/3", 2, Atk(m)); Eq("hp3", 3, Hp(m));
            ClearHand(p);
            Kill(g, m);
            var h = HandIds(p);
            Eq("two cards", 2, h.Count);
            Check("both UNG_809t1", h.All(x => x == "UNG_809t1"), string.Join(",", h));
        }

        [AuditTest] static void UNG_809t1_FlameElemental_1_2()
        {
            var g = G(); var p = P1(g);
            var m = (Minion)PlayNew(g, p, "UNG_809t1");
            Eq("1 attack", 1, Atk(m)); Eq("2 health", 2, Hp(m));
            Eq("cost 1", 1, m.Card.Cost);
            Check("is Elemental", m.Card.IsRace(Race.ELEMENTAL));
        }

        [AuditTest] static void DAL_433_AddsLackey_Overload1()
        {
            var lackeys = new HashSet<string> { "DAL_613", "DAL_614", "DAL_615", "DAL_739", "DAL_741", "ULD_616", "DRG_052", "ULD_616" };
            var g = G(); var p = P1(g);
            var m = (Minion)PlayNew(g, p, "DAL_433");
            Eq("1/1", 1, Atk(m)); Eq("hp1", 1, Hp(m));
            var h = HandIds(p);
            Eq("one card added", 1, h.Count);
            Check("it is a Lackey", h.Count == 1 && (lackeys.Contains(h[0]) || Cards.FromId(h[0]).Name.Contains("Lackey")), string.Join(",", h.Select(CardText)));
            Eq("overload 1", 1, p.OverloadOwed);
        }

        [AuditTest] static void UNG_208_ElementalLastTurn_SummonsTwoRockElementals()
        {
            // without Elemental last turn
            {
                var g = G(); var p = P1(g);
                var e = Hand(p, "UNG_208");
                bool ok = Play(g, e);
                Check("playable without a target", ok, Describe(g));
                Eq("no Elemental last turn: only Sentinel", 1, p.BoardZone.Count);
            }
            {
                var g = G(); var p = P1(g);
                PlayNew(g, p, "UNG_809t1");
                Round(g); ClearHand(p);
                foreach (var m in p.BoardZone.ToList()) Kill(g, m);
                var e = Hand(p, "UNG_208");
                bool ok = Play(g, e);
                Check("played", ok, Describe(g));
                var others = p.BoardZone.Where(m => m != e).ToList();
                Eq("two Elementals summoned", 2, others.Count);
                Check("each 2/3", others.All(m => m.AttackDamage == 2 && m.Health == 3), Describe(g));
                Check("each has Taunt", others.All(m => m.HasTaunt), Describe(g));
                Check("they are Elementals", others.All(m => m.Card.IsRace(Race.ELEMENTAL)), string.Join(",", others.Select(m => m.Card.Id)));
            }
        }

        [AuditTest] static void UNG_938_TauntRestore3()
        {
            var g = G(); var p = P1(g);
            Damage(g, p.Hero, 10);
            var e = Hand(p, "UNG_938");
            bool ok = Play(g, e, p.Hero);
            Check("played on own hero", ok, Describe(g));
            Eq("hero healed 3", 23, p.Hero.Health);
            var m = (Minion)e;
            Check("Taunt", m.HasTaunt); Eq("2/4", 2, Atk(m)); Eq("hp4", 4, Hp(m));
            // minion target
            var g2 = G(); var p2 = P1(g2);
            var y = Board(p2, Yeti); Damage(g2, y, 4);
            PlayNew(g2, p2, "UNG_938", y);
            Eq("minion healed 3", 4, Hp(y));
        }

        [AuditTest] static void BOT_291_DrawsSpellCost5Plus()
        {
            var g = G(); var p = P1(g);
            ClearDeck(p);
            DeckTop(p, "ULD_181"); DeckTop(p, "EX1_238"); DeckTop(p, Golem); DeckTop(p, "CS2_039");
            var m = (Minion)PlayNew(g, p, "BOT_291");
            Eq("3/4", 3, Atk(m)); Eq("hp4", 4, Hp(m));
            var h = HandIds(p);
            Eq("drew exactly Earthquake", "ULD_181", string.Join(",", h));
            // none qualifying
            var g2 = G(); var p2 = P1(g2); ClearDeck(p2); DeckTop(p2, "EX1_238"); DeckTop(p2, Golem);
            PlayNew(g2, p2, "BOT_291");
            Eq("no qualifying spell: nothing drawn", 0, p2.HandZone.Count);
        }

        [AuditTest] static void GIL_807_SpellDrawsMinion()
        {
            var g = G(); var p = P1(g); var o = P2(g);
            ClearDeck(p);
            DeckTop(p, Wisp); DeckTop(p, "CS2_039"); DeckTop(p, "EX1_238");
            var b = Board(p, "GIL_807");
            Eq("4/8", 4, Atk(b)); Eq("hp8", 8, Hp(b));
            PlayNew(g, p, "EX1_238", o.Hero);
            Eq("drew the minion", "CS2_231", string.Join(",", HandIds(p)));
            ClearHand(p);
            EndTurn(g);
            ClearHand(o);
            PlayNew(g, o, "EX1_238", p.Hero);
            Check("enemy spell does not draw for me", !HandIds(p).Contains("CS2_231"), string.Join(",", HandIds(p)));
        }

        [AuditTest] static void BOT_451_TwoRushSparks_Overload1()
        {
            var g = G(); var p = P1(g); var o = P2(g);
            var foe = Board(o, Yeti);
            PlayNew(g, p, "BOT_451");
            var sparks = p.BoardZone.Where(m => m.Card.Id == "BOT_102t").ToList();
            Eq("two sparks", 2, sparks.Count);
            Check("1/1 with Rush", sparks.All(s => s.AttackDamage == 1 && s.Health == 1 && s.IsRush), Describe(g));
            Eq("overload 1", 1, p.OverloadOwed);
            bool hitMinion = g.Process(MinionAttackTask.Any(p, sparks[0], foe));
            Check("rush: can attack a minion this turn", hitMinion, Describe(g));
            bool hitHero = g.Process(MinionAttackTask.Any(p, sparks[1], o.Hero));
            Check("rush: cannot attack the hero this turn", !hitHero || o.Hero.Health == 30, Describe(g));
        }

        [AuditTest] static void BOT_102t_Spark_Rush()
        {
            var g = G(); var p = P1(g);
            var s = (Minion)PlayNew(g, p, "BOT_102t");
            Eq("1/1", 1, Atk(s)); Eq("hp1", 1, Hp(s)); Check("Rush", s.IsRush);
        }

        [AuditTest] static void CORE_CS2_053_DrawAndDiscount3()
        {
            var g = G(); var p = P1(g);
            ClearDeck(p); DeckTop(p, Yeti);
            PlayNew(g, p, "CORE_CS2_053");
            Eq("one card", 1, p.HandZone.Count);
            var c = p.HandZone[0];
            Eq("Yeti costs 1", 1, c.Cost);
            Check("CS2_053e on it", EnchantIds(c).Contains("CS2_053e"), string.Join(",", EnchantIds(c)));
            // floor at 0
            var g2 = G(); var p2 = P1(g2); ClearDeck(p2); DeckTop(p2, Wisp);
            PlayNew(g2, p2, "CORE_CS2_053");
            Eq("Wisp costs 0", 0, p2.HandZone[0].Cost);
        }

        [AuditTest] static void CS2_053e_Minus3Cost()
        {
            var g = G(); var p = P1(g);
            ClearDeck(p); DeckTop(p, Golem);
            PlayNew(g, p, "CORE_CS2_053");
            var c = p.HandZone[0];
            Eq("Golem 7 -> 4", 4, c.Cost);
            var cl = Clone(g); Eq("clone cost 4", 4, At(cl, c).Cost);
        }

        [AuditTest] static void ULD_181_Earthquake5Then2()
        {
            var g = G(); var p = P1(g); var o = P2(g);
            var ee = Board(o, "CORE_EX1_250");       // 7/9 -> 2 left
            var golem = Board(o, Golem);              // 7/7 -> dies on the 2
            var ign = Board(p, "UNG_845");            // 2/3 dies on 5
            var mine = Board(p, "CORE_EX1_250");
            ClearHand(p);
            int hp0 = o.Hero.Health, myHp0 = p.Hero.Health;
            PlayNew(g, p, "ULD_181");
            Eq("Earth Elemental 9 -> 2", 2, Hp(ee));
            Check("Golem dead", golem.IsDead || golem.Zone != o.BoardZone, Describe(g));
            Eq("my EE 9 -> 2", 2, Hp(mine));
            Eq("heroes unharmed (enemy)", hp0, o.Hero.Health);
            Eq("heroes unharmed (me)", myHp0, p.Hero.Health);
            Eq("Igneous deathrattle fired", 2, HandIds(p).Count(x => x == "UNG_809t1"));
            // spell damage applies to both waves
            var g2 = G(); var p2 = P1(g2); var o2 = P2(g2);
            // Archmage (4/7, Spell Damage +1) survives the 6 of wave one, so wave two is boosted too.
            // (A Novice Zapper dies in wave one; the 2 is then unboosted, which is correct.)
            Board(p2, "CS2_155");
            var big = Board(o2, "CORE_EX1_250"); // 9 hp; 6 + 3 = 9 -> dead
            PlayNew(g2, p2, "ULD_181");
            Check("with +1 spell damage EE takes 6+3", big.IsDead || big.Zone != o2.BoardZone, Describe(g2));
        }

        [AuditTest] static void CORE_EX1_250_TauntOverload2()
        {
            var g = G(); var p = P1(g);
            var m = (Minion)PlayNew(g, p, "CORE_EX1_250");
            Eq("7/9", 7, Atk(m)); Eq("hp9", 9, Hp(m)); Check("Taunt", m.HasTaunt);
            OverloadCheck("CORE_EX1_250", 2);
        }

        [AuditTest] static void GIL_531_TauntAddsShamanSpell()
        {
            for (int seed = 1; seed <= 6; seed++)
            {
                var g = NewGame(s => { s.PlayerHero = "HERO_02"; s.Seed = seed; }); var p = P1(g);
                var m = (Minion)PlayNew(g, p, "GIL_531");
                if (seed == 1) { Eq("0/1", 0, Atk(m)); Eq("hp1", 1, Hp(m)); Check("Taunt", m.HasTaunt); }
                var h = p.HandZone.ToList();
                Eq("seed " + seed + " one card", 1, h.Count);
                if (h.Count == 1) Check("seed " + seed + " Shaman spell", h[0].Card.Type == CardType.SPELL && h[0].Card.Class == CardClass.SHAMAN, CardText(h[0].Card.Id) + " " + h[0].Card.Class);
            }
        }

        [AuditTest] static void OG_028_CostReducedPerTotem()
        {
            var g = G(); var p = P1(g);
            var t = Hand(p, "OG_028");
            Eq("6 base", 6, t.Cost);
            Board(p, "CS2_050"); Board(p, "CS2_051"); g.DeathProcessingAndAuraUpdate();
            Eq("2 totems -> 4", 4, t.Cost);
            foreach (var m in p.BoardZone.ToList()) Kill(g, m);
            Eq("dead totems still count (this game)", 4, t.Cost);
            Power(g, p); // shaman basic totemic call
            Note("power: " + p.Hero.HeroPower.Card.Id + " " + Describe(g));
            Eq("totem from hero power counts -> 3", 3, t.Cost);
            var o = P2(g);
            EndTurn(g); Board(o, "CS2_050"); EndTurn(g);
            Eq("enemy totem does not count", 3, t.Cost);
            Play(g, t);
            var m2 = (Minion)t; Eq("5/5", 5, Atk(m2)); Eq("hp5", 5, Hp(m2)); Check("Taunt", m2.HasTaunt);
        }

        [AuditTest] static void CS2_039_GiveWindfury()
        {
            var g = G(); var p = P1(g); var o = P2(g);
            var y = Board(p, Yeti);
            PlayNew(g, p, "CS2_039", y);
            Check("Yeti has Windfury", y.HasWindfury);
            WindfuryAttacksTwice(g, y, "buffed Yeti");
            var e = Board(o, Yeti);
            var w = Hand(p, "CS2_039");
            Check("can target enemy minion", CanPlay(w, e));
            Check("cannot target hero", !CanPlay(w, o.Hero));
        }

        [AuditTest] static void CS2_169_YoungDragonhawk() => VanillaWindfury("CS2_169", 1, 1);
        [AuditTest] static void EX1_021_ThrallmarFarseer() => VanillaWindfury("EX1_021", 2, 3);
        [AuditTest] static void GVG_037_ZapOMatic() => VanillaWindfury("GVG_037", 3, 2);
        [AuditTest] static void GVG_084_FlyingMachine() => VanillaWindfury("GVG_084", 1, 4);

        [AuditTest] static void EX1_567_DoomhammerWindfuryOverload2()
        {
            var g = G(); var p = P1(g); var o = P2(g);
            PlayNew(g, p, "EX1_567");
            Eq("weapon", "EX1_567", Weapon(p));
            Eq("2 attack", 2, p.Hero.Weapon.AttackDamage);
            Eq("8 durability", 8, p.Hero.Weapon.Durability);
            Eq("overload 2", 2, p.OverloadOwed);
            int hp0 = o.Hero.Health;
            bool a1 = HeroAttack(g, p, o.Hero), a2 = HeroAttack(g, p, o.Hero), a3 = HeroAttack(g, p, o.Hero);
            Check("two hero attacks", a1 && a2, Describe(g));
            Eq("4 damage total", hp0 - 4, o.Hero.Health);
            Eq("durability 6", 6, p.Hero.Weapon.Durability);
        }

        [AuditTest] static void EX1_587_GiveFriendlyWindfury()
        {
            var g = G(); var p = P1(g); var o = P2(g);
            var y = Board(p, Yeti); var e = Board(o, Yeti);
            var w = Hand(p, "EX1_587");
            Check("cannot target enemy minion", !CanPlay(w, e));
            Play(g, w, y);
            Check("Yeti Windfury", y.HasWindfury);
            Eq("3/3", 3, Atk((Minion)w));
            Check("Windspeaker itself no Windfury", !((Minion)w).HasWindfury);
        }

        [AuditTest] static void BOT_533_AddsRandomElemental()
        {
            for (int seed = 1; seed <= 6; seed++)
            {
                var g = NewGame(s => { s.PlayerHero = "HERO_02"; s.Seed = seed; }); var p = P1(g);
                var m = (Minion)PlayNew(g, p, "BOT_533");
                if (seed == 1) { Eq("2/2", 2, Atk(m)); Eq("hp2", 2, Hp(m)); }
                var h = p.HandZone.ToList();
                Eq("seed " + seed + " one card", 1, h.Count);
                if (h.Count == 1) Check("seed " + seed + " Elemental", h[0].Card.IsRace(Race.ELEMENTAL), CardText(h[0].Card.Id));
            }
        }

        [AuditTest] static void BRM_012_Gain1to4Attack_Overload1()
        {
            var seen = new HashSet<int>();
            for (int seed = 1; seed <= 20; seed++)
            {
                var g = NewGame(s => { s.PlayerHero = "HERO_02"; s.Seed = seed; }); var p = P1(g);
                var m = (Minion)PlayNew(g, p, "BRM_012");
                seen.Add(Atk(m));
                Check("seed " + seed + " attack 4..7", Atk(m) >= 4 && Atk(m) <= 7, "atk " + Atk(m));
                if (seed == 1) { Eq("hp 6", 6, Hp(m)); Eq("overload 1", 1, p.OverloadOwed); }
            }
            Note("attacks seen " + string.Join(",", seen.OrderBy(x => x)));
        }

        [AuditTest] static void UNG_019_UntargetableBySpellsAndPowers()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g); // mage: Fireblast
            var air = Board(o, "UNG_019");
            Eq("2/1", 2, Atk(air)); Eq("hp1", 1, Hp(air));
            var bolt = Hand(p, "EX1_238");
            Check("spell cannot target it", !CanPlay(bolt, air));
            Mana(p, 10);
            bool powerTargets = p.Options().OfType<HeroPowerTask>().Any(t => t.Target?.Id == air.Id);
            Check("hero power cannot target it", !powerTargets);
            Check("Fireblast refused", !g.Process(HeroPowerTask.Any(p, air)) || air.Health == 1, Describe(g));
            // battlecry can target
            var mine = Board(p, "UNG_019");
            var ws = Hand(p, "EX1_587");
            Check("battlecry can target it", CanPlay(ws, mine));
        }

        [AuditTest] static void UNG_202_ReduceElementalsInHand()
        {
            var g = G(); var p = P1(g);
            var fe = Hand(p, "CS2_042"); var y = Hand(p, Yeti); var fl = Hand(p, "UNG_809t1");
            var m = (Minion)PlayNew(g, p, "UNG_202");
            Eq("1/1", 1, Atk(m)); Eq("hp1", 1, Hp(m));
            Eq("Fire Elemental 6 -> 5", 5, fe.Cost);
            Eq("Flame Elemental 1 -> 0", 0, fl.Cost);
            Eq("Yeti unchanged", 4, y.Cost);
            ClearDeck(p); DeckTop(p, "CS2_042");
            Round(g);
            var drawn = p.HandZone.Last();
            Eq("card drawn later is not reduced", 6, drawn.Cost);
        }

        [AuditTest] static void AT_052_TotemGolem() { var g = G(); var p = P1(g); var m = (Minion)PlayNew(g, p, "AT_052"); Eq("3/4", 3, Atk(m)); Eq("hp4", 4, Hp(m)); OverloadCheck("AT_052", 1); }

        [AuditTest] static void CFM_061_Restore6_Overload1()
        {
            var g = G(); var p = P1(g);
            Damage(g, p.Hero, 10);
            var e = Hand(p, "CFM_061");
            Check("played on hero", Play(g, e, p.Hero), Describe(g));
            Eq("hero 20 -> 26", 26, p.Hero.Health);
            Eq("3/6", 3, Atk((Minion)e)); Eq("hp 6", 6, Hp((Minion)e));
            Eq("overload 1", 1, p.OverloadOwed);
        }

        [AuditTest] static void EX1_238_LightningBolt()
        {
            var g = G(); var p = P1(g); var o = P2(g);
            var y = Board(o, Yeti);
            PlayNew(g, p, "EX1_238", y);
            Eq("Yeti 5 -> 2", 2, Hp(y));
            Eq("overload 1", 1, p.OverloadOwed);
            PlayNew(g, p, "EX1_238", o.Hero);
            Eq("hero 30 -> 27", 27, o.Hero.Health);
        }

        [AuditTest] static void EX1_247_StormforgedAxe()
        {
            var g = G(); var p = P1(g);
            PlayNew(g, p, "EX1_247");
            Eq("2 atk", 2, p.Hero.Weapon.AttackDamage); Eq("3 dur", 3, p.Hero.Weapon.Durability);
            Eq("overload 1", 1, p.OverloadOwed);
        }

        [AuditTest] static void EX1_248_FeralSpirit()
        {
            var g = G(); var p = P1(g);
            PlayNew(g, p, "EX1_248");
            var w = p.BoardZone.Where(m => m.Card.Id == "EX1_tk11").ToList();
            Eq("two wolves", 2, w.Count);
            Check("2/3 Taunt", w.All(m => m.AttackDamage == 2 && m.Health == 3 && m.HasTaunt), Describe(g));
            Eq("overload 1", 1, p.OverloadOwed);
        }

        [AuditTest] static void EX1_tk11_SpiritWolf() { var g = G(); var p = P1(g); var m = Board(p, "EX1_tk11"); Eq("2/3", 2, Atk(m)); Eq("hp3", 3, Hp(m)); Check("Taunt", m.HasTaunt); }

        [AuditTest] static void EX1_258_GrowsOnOverloadCards()
        {
            var g = G(); var p = P1(g); var o = P2(g);
            var u = Board(p, "EX1_258");
            Eq("2/4", 2, Atk(u)); Eq("hp4", 4, Hp(u));
            PlayNew(g, p, Yeti);
            Eq("non-overload: no buff", 2, Atk(u));
            PlayNew(g, p, "EX1_238", o.Hero);
            Eq("spell w/ overload +1 atk", 3, Atk(u)); Eq("+1 hp", 5, Hp(u));
            PlayNew(g, p, "AT_052");
            Eq("minion w/ overload +1", 4, Atk(u));
            PlayNew(g, p, "EX1_247");
            Eq("weapon w/ overload +1", 5, Atk(u)); Eq("hp 7", 7, Hp(u));
            Check("EX1_258e present", EnchantIds(u).Contains("EX1_258e"));
            EndTurn(g); ClearHand(o);
            PlayNew(g, o, "EX1_238", o.Hero);
            Eq("enemy overload card: no buff", 5, Atk(u));
        }

        [AuditTest] static void EX1_258e_PlusOnePlusOne()
        {
            var g = G(); var p = P1(g); var o = P2(g);
            var u = Board(p, "EX1_258");
            PlayNew(g, p, "EX1_238", o.Hero);
            Eq("max hp 5", 5, MaxHp(u));
            Round(g);
            Eq("persists", 3, Atk(u));
            var c = Clone(g); Eq("clone", 3, Atk(At(c, u)));
        }
    }
}
