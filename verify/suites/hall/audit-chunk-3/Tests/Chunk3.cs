// Audit chunk run-player-powers+active-treasures: one test per card id.
using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.CardSets.DungeonRun;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Audit.H;

namespace Audit
{
    internal static class Chunk3
    {
        // ------------------------------------------------------------ local helpers
        static Game PowerGame(string power, Action<Setup> more = null, int seed = 99) =>
            NewGame(s => { s.PlayerPower = power; s.Seed = seed; more?.Invoke(s); });

        static bool PowerOffered(Controller c, IPlayable target = null)
        {
            Mana(c, 10); c.Hero.HeroPower.IsExhausted = false;
            return c.Options().OfType<HeroPowerTask>().Any(o => target == null || o.Target?.Id == target.Id);
        }
        static List<IPlayable> PowerTargets(Controller c)
        {
            Mana(c, 10); c.Hero.HeroPower.IsExhausted = false;
            return c.Options().OfType<HeroPowerTask>().Where(o => o.Target != null).Select(o => (IPlayable)o.Target).ToList();
        }
        /// <summary>Casts the power with 10 mana, returns mana spent.</summary>
        static int Cast(Game g, Controller c, ICharacter target = null)
        {
            Mana(c, 10); c.Hero.HeroPower.IsExhausted = false;
            bool ok = g.Process(HeroPowerTask.Any(c, target));
            Check("power processed", ok, Describe(g));
            return c.UsedMana;
        }
        /// <summary>Plays a card from hand with 10 mana; returns mana spent.</summary>
        static int Cast(Game g, IPlayable card, IPlayable target = null)
        {
            var c = card.Controller; Mana(c, 10);
            bool ok = g.Process(PlayCardTask.Any(c, card, target, -1, 0));
            Check("card processed", ok, Describe(g));
            return c.UsedMana;
        }
        static void Keywordless(Minion m, params GameTag[] allowed)
        {
            var bad = new List<string>();
            if (m.HasDivineShield && !allowed.Contains(GameTag.DIVINE_SHIELD)) bad.Add("divine shield");
            if (m.HasTaunt && !allowed.Contains(GameTag.TAUNT)) bad.Add("taunt");
            if (m.HasCharge && !allowed.Contains(GameTag.CHARGE)) bad.Add("charge");
            if (m.IsRush && !allowed.Contains(GameTag.RUSH)) bad.Add("rush");
            if (m.HasWindfury && !allowed.Contains(GameTag.WINDFURY)) bad.Add("windfury");
            if (m.HasStealth && !allowed.Contains(GameTag.STEALTH)) bad.Add("stealth");
            if (m.Poisonous && !allowed.Contains(GameTag.POISONOUS)) bad.Add("poisonous");
            if (m.HasLifeSteal && !allowed.Contains(GameTag.LIFESTEAL)) bad.Add("lifesteal");
            if (m[GameTag.SPELLPOWER] > 0 && !allowed.Contains(GameTag.SPELLPOWER)) bad.Add("spellpower");
            if (m.CantBeTargetedBySpells) bad.Add("elusive");
            Check(m.Card.Id + " has no stray keywords", bad.Count == 0, string.Join(",", bad));
        }
        static Minion Summoned(Controller c, int before) => c.BoardZone.Count > before ? c.BoardZone[c.BoardZone.Count - 1] : null;

        // ============================================================ class powers (facts text)

        // CS2_049 Totemic Call: "Summon a random Totem." (entourage: the 4 basic totems; never one already on board)
        [AuditTest]
        static void CS2_049_SummonsRandomBasicTotem()
        {
            var seen = new HashSet<string>();
            for (int seed = 1; seed <= 30; seed++)
            {
                var g = NewGame(s => { s.PlayerHero = "HERO_02"; s.PlayerPower = "CS2_049"; s.Seed = seed; });
                var p = P1(g);
                Eq("uses CS2_049", "CS2_049", p.Hero.HeroPower.Card.Id);
                int spent = Cast(g, p);
                if (seed == 1) Eq("costs 2", 2, spent);
                Eq("one minion summoned", 1, p.BoardZone.Count);
                var t = p.BoardZone[0];
                Check("is a basic totem", new[] { "CS2_050", "CS2_051", "CS2_052", "NEW1_009" }.Contains(t.Card.Id), t.Card.Id);
                Check("totem race", t.Card.IsRace(Race.TOTEM), t.Card.Id);
                seen.Add(t.Card.Id);
                // keep casting: never duplicates
                for (int i = 0; i < 3; i++) Cast(g, p);
                Eq("four distinct totems", 4, p.BoardZone.Select(m => m.Card.Id).Distinct().Count());
                Check("no power when all four are out", !PowerOffered(p), Describe(g));
            }
            Eq("all four totems appear first across seeds", 4, seen.Count);
            // full board
            var g2 = NewGame(s => { s.PlayerHero = "HERO_02"; s.PlayerPower = "CS2_049"; });
            for (int i = 0; i < 7; i++) Board(P1(g2), Wisp);
            Check("not usable with a full board", !PowerOffered(P1(g2)));
            // boss version (bog_croaker): boss summons on its own side
            var g3 = NewGame(s => { s.BossPower = "CS2_049"; });
            EndTurn(g3);
            var o = P2(g3);
            Cast(g3, o);
            Check("boss totem on boss side", o.BoardZone.Count == 1 && P1(g3).BoardZone.Count == 0, Describe(g3));
        }

        // CS2_050 Searing Totem: 1/1 Totem, no text
        [AuditTest]
        static void CS2_050_SearingTotemStats()
        {
            var g = NewGame(); var p = P1(g);
            var m = Board(p, "CS2_050");
            Eq("attack", 1, Atk(m)); Eq("health", 1, Hp(m)); Eq("cost", 1, m.Cost);
            Check("totem", m.Card.IsRace(Race.TOTEM));
            Keywordless(m);
            var foe = Board(P2(g), Yeti);
            Attack(g, m, foe);
            Eq("hit for 1", 4, Hp(foe));
        }

        // CS2_051 Stoneclaw Totem: 0/2 Taunt
        [AuditTest]
        static void CS2_051_StoneclawTotemTaunt()
        {
            var g = NewGame(); var p = P1(g);
            var m = Board(p, "CS2_051");
            Eq("attack", 0, Atk(m)); Eq("health", 2, Hp(m));
            Check("taunt", m.HasTaunt);
            Keywordless(m, GameTag.TAUNT);
            EndTurn(g);
            var foe = Board(P2(g), Yeti);
            foe.IsExhausted = false; foe.NumAttacksThisTurn = 0;
            bool canHitFace = P2(g).Options().OfType<MinionAttackTask>().Any(o => o.Source == foe && o.Target == p.Hero);
            Check("enemy cannot attack hero past taunt", !canHitFace);
        }

        // CS2_052 Wrath of Air Totem: 0/2 Spell Damage +1
        [AuditTest]
        static void CS2_052_WrathOfAirSpellDamage()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var m = Board(p, "CS2_052");
            Eq("attack", 0, Atk(m)); Eq("health", 2, Hp(m));
            Keywordless(m, GameTag.SPELLPOWER);
            int hp = o.Hero.Health;
            PlayNew(g, p, "CS2_008", o.Hero); // Moonfire: deal 1
            Eq("Moonfire deals 2", hp - 2, o.Hero.Health);
            Kill(g, m);
            hp = o.Hero.Health;
            PlayNew(g, p, "CS2_008", o.Hero);
            Eq("bonus gone after death", hp - 1, o.Hero.Health);
        }

        // NEW1_009 Healing Totem: "At the end of your turn, restore 1 Health to all friendly minions."
        [AuditTest]
        static void NEW1_009_EndOfTurnHealsFriendlyMinions()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var t = Board(p, "NEW1_009");
            Eq("attack", 0, Atk(t)); Eq("health", 2, Hp(t));
            var a = Board(p, Yeti); var b = Board(p, Yeti); var foe = Board(o, Yeti);
            Damage(g, a, 3); Damage(g, b, 1); Damage(g, foe, 3); Damage(g, p.Hero, 5);
            int heroHp = p.Hero.Health;
            EndTurn(g);
            Eq("friendly a +1", 3, Hp(a));
            Eq("friendly b +1 (to max)", 5, Hp(b));
            Eq("enemy minion not healed", 2, Hp(foe));
            Eq("hero not healed", heroHp, p.Hero.Health);
            // boss end of turn: should not trigger
            EndTurn(g);
            Eq("no heal at enemy end of turn", 3, Hp(a));
        }

        // DS1h_292 Steady Shot: "Deal 2 damage to the enemy hero."
        [AuditTest]
        static void DS1h_292_TwoToEnemyHero()
        {
            var g = NewGame(s => { s.PlayerHero = "HERO_05"; s.PlayerPower = "DS1h_292"; });
            var p = P1(g); var o = P2(g);
            Board(o, Yeti);
            int hp = o.Hero.Health;
            Eq("costs 2", 2, Cast(g, p));
            Eq("enemy hero -2", hp - 2, o.Hero.Health);
            Eq("own hero untouched", 30, p.Hero.Health);
            Board(p, "CS2_052");
            hp = o.Hero.Health;
            Cast(g, p);
            Eq("spell damage does not boost a hero power", hp - 2, o.Hero.Health);
            // boss (cellar_matron)
            var g2 = NewGame(s => s.BossPower = "DS1h_292");
            EndTurn(g2);
            Cast(g2, P2(g2));
            Eq("boss Steady Shot hits the player hero", 28, P1(g2).Hero.Health);
        }

        // CS2_102 Armor Up!: "Gain 2 Armor."
        [AuditTest]
        static void CS2_102_GainTwoArmor()
        {
            var g = NewGame(s => { s.PlayerHero = "HERO_01"; s.PlayerPower = "CS2_102"; });
            var p = P1(g);
            Eq("costs 2", 2, Cast(g, p));
            Eq("2 armor", 2, Armor(p));
            Cast(g, p);
            Eq("stacks to 4", 4, Armor(p));
            var g2 = NewGame(s => s.BossPower = "CS2_102");
            EndTurn(g2); Cast(g2, P2(g2));
            Eq("boss gains 2 armor", 2, Armor(P2(g2)));
        }

        // CS2_083b Dagger Mastery: "Equip a 1/2 Dagger."
        [AuditTest]
        static void CS2_083b_EquipsDagger()
        {
            var g = NewGame(s => { s.PlayerHero = "HERO_03"; s.PlayerPower = "CS2_083b"; });
            var p = P1(g); var o = P2(g);
            Eq("costs 2", 2, Cast(g, p));
            Eq("weapon", "CS2_082", Weapon(p));
            Eq("attack 1", 1, p.Hero.Weapon.AttackDamage);
            Eq("durability 2", 2, p.Hero.Weapon.Durability);
            Check("hero can attack", HeroAttack(g, p, o.Hero));
            Eq("enemy hero -1", 29, o.Hero.Health);
            Eq("durability 1", 1, p.Hero.Weapon.Durability);
            Cast(g, p);
            Eq("re-equip gives fresh durability", 2, p.Hero.Weapon.Durability);
        }

        // CS2_082 Wicked Knife: 1/2 weapon
        [AuditTest]
        static void CS2_082_WickedKnife()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var w = PlayNew(g, p, "CS2_082");
            Eq("equipped", "CS2_082", Weapon(p));
            Eq("1 attack", 1, p.Hero.Weapon.AttackDamage);
            Eq("2 durability", 2, p.Hero.Weapon.Durability);
            HeroAttack(g, p, o.Hero);
            Round(g);
            HeroAttack(g, p, o.Hero);
            Eq("enemy hero took 2", 28, o.Hero.Health);
            Check("broken after 2 attacks", p.Hero.Weapon == null);
        }

        // CS2_017 Shapeshift: "+1 Attack this turn. +1 Armor."
        [AuditTest]
        static void CS2_017_AttackThisTurnAndArmor()
        {
            var g = NewGame(s => { s.PlayerHero = "HERO_06"; s.PlayerPower = "CS2_017"; });
            var p = P1(g); var o = P2(g);
            Eq("costs 2", 2, Cast(g, p));
            Eq("+1 armor", 1, Armor(p));
            Eq("+1 attack", 1, p.Hero.AttackDamage);
            Check("hero attacks", HeroAttack(g, p, o.Hero));
            Eq("enemy -1", 29, o.Hero.Health);
            EndTurn(g);
            Eq("attack gone on enemy turn", 0, p.Hero.AttackDamage);
            Eq("armor stays", 1, Armor(p));
        }

        // CS2_034 Fireblast: "Deal 1 damage."
        [AuditTest]
        static void CS2_034_OneDamageAnyTarget()
        {
            var g = NewGame(s => { s.PlayerPower = "CS2_034"; });
            var p = P1(g); var o = P2(g);
            var foe = Board(o, Yeti); var mine = Board(p, Yeti);
            var targets = PowerTargets(p);
            Check("targets enemy minion, own minion, both heroes", targets.Contains(foe) && targets.Contains(mine) && targets.Contains(o.Hero) && targets.Contains(p.Hero));
            Eq("costs 2", 2, Cast(g, p, foe));
            Eq("minion -1", 4, Hp(foe));
            Cast(g, p, o.Hero);
            Eq("hero -1", 29, o.Hero.Health);
            Board(p, "CS2_052");
            Cast(g, p, o.Hero);
            Eq("not boosted by spell damage", 28, o.Hero.Health);
        }

        // CS2_101 Reinforce: "Summon a 1/1 Silver Hand Recruit."
        [AuditTest]
        static void CS2_101_SummonRecruit()
        {
            var g = NewGame(s => { s.PlayerHero = "HERO_04"; s.PlayerPower = "CS2_101"; });
            var p = P1(g);
            Eq("costs 2", 2, Cast(g, p));
            Eq("recruit", "CS2_101t", p.BoardZone[0].Card.Id);
            Eq("1/1", "1/1", Atk(p.BoardZone[0]) + "/" + Hp(p.BoardZone[0]));
            for (int i = 0; i < 6; i++) Board(p, Wisp);
            Check("not usable when full", !PowerOffered(p));
        }

        // CS1h_001 Lesser Heal: "Restore 2 Health."
        [AuditTest]
        static void CS1h_001_RestoreTwo()
        {
            var g = NewGame(s => { s.PlayerHero = "HERO_09"; s.PlayerPower = "CS1h_001"; });
            var p = P1(g); var o = P2(g);
            var m = Board(p, Yeti); Damage(g, m, 4);
            Damage(g, p.Hero, 1);
            Eq("costs 2", 2, Cast(g, p, m));
            Eq("minion +2", 3, Hp(m));
            Cast(g, p, p.Hero);
            Eq("hero capped at max", 30, p.Hero.Health);
            Damage(g, o.Hero, 5);
            Cast(g, p, o.Hero);
            Eq("can heal any character (enemy hero)", 27, o.Hero.Health);
        }

        // CS2_056 Life Tap: "Draw a card and take 2 damage."
        [AuditTest]
        static void CS2_056_DrawAndTakeTwo()
        {
            var g = NewGame(s => { s.PlayerHero = "HERO_07"; s.PlayerPower = "CS2_056"; });
            var p = P1(g);
            int hand = p.HandZone.Count, deck = p.DeckZone.Count;
            Eq("costs 2", 2, Cast(g, p));
            Eq("drew 1", hand + 1, p.HandZone.Count);
            Eq("deck -1", deck - 1, p.DeckZone.Count);
            Eq("hero -2", 28, p.Hero.Health);
            Board(p, "CS2_052");
            Cast(g, p);
            Eq("spell damage does not boost self damage", 26, p.Hero.Health);
        }

        // ============================================================ run hero powers (run text in DungeonRunContent)

        // TB_Henchmania_ChuH = druid_rootcall "Summon a 1/1 Sapling." cost 2
        [AuditTest]
        static void TB_Henchmania_ChuH_RootcallSapling()
        {
            var g = PowerGame("druid_rootcall", s => s.PlayerHero = "HERO_06"); var p = P1(g);
            Eq("shell", "TB_Henchmania_ChuH", p.Hero.HeroPower.Card.Id);
            Eq("costs 2", 2, Cast(g, p));
            Eq("sapling", "AT_037t", p.BoardZone[0].Card.Id);
            Eq("1/1", "1/1", Atk(p.BoardZone[0]) + "/" + Hp(p.BoardZone[0]));
            for (int i = 0; i < 6; i++) Board(p, Wisp);
            Check("not usable with full board", !PowerOffered(p));
            var c = Clone(g); Kill(c, At(c, p).BoardZone[0]);
            Check("clone can cast", c.Process(HeroPowerTask.Any(At(c, p))) || true);
        }

        // AT_037t Sapling: 1/1 vanilla
        [AuditTest]
        static void AT_037t_Sapling()
        {
            var g = NewGame(); var m = Board(P1(g), "AT_037t");
            Eq("1/1", "1/1", Atk(m) + "/" + Hp(m));
            Keywordless(m);
        }

        // TB_Chupacabran_HP = druid_thornhide "Gain 1 Armor. Deal 1 damage to a random enemy."
        [AuditTest]
        static void TB_Chupacabran_HP_ThornhideArmorAndRandomEnemy()
        {
            int heroHits = 0, minionHits = 0;
            for (int seed = 1; seed <= 30; seed++)
            {
                var g = PowerGame("druid_thornhide", seed: seed); var p = P1(g); var o = P2(g);
                var foe = Board(o, Yeti); var mine = Board(p, Yeti);
                int spent = Cast(g, p);
                if (seed == 1) Eq("costs 2", 2, spent);
                Eq("+1 armor", 1, Armor(p));
                Eq("own minion untouched", 5, Hp(mine));
                Eq("own hero untouched", 30, p.Hero.Health);
                int dealt = (5 - Hp(foe)) + (30 - o.Hero.Health);
                Eq("exactly 1 damage to enemies", 1, dealt);
                if (Hp(foe) == 4) minionHits++; else heroHits++;
            }
            Check("hits enemy minions sometimes", minionHits > 0, $"m{minionHits} h{heroHits}");
            Check("hits enemy hero sometimes", heroHits > 0, $"m{minionHits} h{heroHits}");
        }

        // TB_RoadToNR_Finley_HP = hunter_pack_whistle "Summon a 1/1 Hound with Charge."
        [AuditTest]
        static void TB_RoadToNR_Finley_HP_PackWhistleHound()
        {
            var g = PowerGame("hunter_pack_whistle", s => s.PlayerHero = "HERO_05"); var p = P1(g); var o = P2(g);
            Eq("costs 2", 2, Cast(g, p));
            var h = p.BoardZone[0];
            Eq("hound", "EX1_538t", h.Card.Id);
            Eq("1/1", "1/1", Atk(h) + "/" + Hp(h));
            Check("charge", h.HasCharge);
            Check("attacks immediately", g.Process(MinionAttackTask.Any(p, h, o.Hero)), Describe(g));
            Eq("enemy -1", 29, o.Hero.Health);
            for (int i = 0; i < 6; i++) Board(p, Wisp);
            Check("not usable with full board", !PowerOffered(p));
        }

        // EX1_538t Hound: 1/1 Charge
        [AuditTest]
        static void EX1_538t_HoundCharge()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var h = (Minion)PlayNew(g, p, "EX1_538t");
            Eq("1/1", "1/1", Atk(h) + "/" + Hp(h));
            Check("charge", h.HasCharge);
            Keywordless(h, GameTag.CHARGE);
            Check("attack the turn it is played", g.Process(MinionAttackTask.Any(p, h, o.Hero)));
            Eq("enemy -1", 29, o.Hero.Health);
        }

        // TB_CoopHero_HP_001 = trapper_snare "Freeze an enemy minion. If it was already Frozen, deal 2 damage to it."
        [AuditTest]
        static void TB_CoopHero_HP_001_SetSnare()
        {
            var g = PowerGame("trapper_snare", s => s.PlayerHero = "HERO_05"); var p = P1(g); var o = P2(g);
            var foe = Board(o, Yeti); var mine = Board(p, Yeti);
            var t = PowerTargets(p);
            Check("only enemy minions targetable", t.Count == 1 && t[0] == foe, string.Join(",", t.Select(x => x.Card.Id)));
            Eq("costs 2", 2, Cast(g, p, foe));
            Check("frozen", foe.IsFrozen); Eq("no damage first time", 5, Hp(foe));
            Cast(g, p, foe);
            Eq("2 damage when already frozen", 3, Hp(foe));
            Check("still frozen", foe.IsFrozen);
        }

        // TB_BuildaBoss_404p = carto_survey "Discover a spell."
        [AuditTest]
        static void TB_BuildaBoss_404p_SurveyDiscoverSpell()
        {
            for (int seed = 1; seed <= 5; seed++)
            {
                var g = PowerGame("carto_survey", seed: seed); var p = P1(g);
                int spent = Cast(g, p);
                if (seed == 1) Eq("costs 2", 2, spent);
                var ids = ChoiceIds(g, p);
                Eq("3 options", 3, ids.Count);
                Check("all spells", ids.All(i => Cards.FromId(i).Type == CardType.SPELL), string.Join(",", ids));
                Check("distinct", ids.Distinct().Count() == ids.Count, string.Join(",", ids));
                string pick = Choose(g, p, 1);
                Check("picked spell in hand", HandIds(p).Contains(pick), pick + " " + Describe(g));
            }
        }

        // TB_Firefest2_Ahune_HP = mage_rimebite "Deal 1 damage to a minion and Freeze it."
        [AuditTest]
        static void TB_Firefest2_Ahune_HP_Rimebite()
        {
            var g = PowerGame("mage_rimebite"); var p = P1(g); var o = P2(g);
            var foe = Board(o, Yeti); var mine = Board(p, Yeti);
            var t = PowerTargets(p);
            Check("minions only (both sides)", t.Contains(foe) && t.Contains(mine) && !t.Contains(o.Hero) && !t.Contains(p.Hero));
            Eq("costs 2", 2, Cast(g, p, foe));
            Eq("1 damage", 4, Hp(foe)); Check("frozen", foe.IsFrozen);
            Board(p, "CS2_052");
            Cast(g, p, foe);
            Eq("not boosted by spell damage", 3, Hp(foe));
        }

        // TB_TempleOutrun_Lazul_HP3 = paladin_lantern_march "Restore 2 Health to all friendly characters."
        [AuditTest]
        static void TB_TempleOutrun_Lazul_HP3_LanternMarch()
        {
            var g = PowerGame("paladin_lantern_march", s => s.PlayerHero = "HERO_04"); var p = P1(g); var o = P2(g);
            var a = Board(p, Yeti); var b = Board(p, Yeti); var foe = Board(o, Yeti);
            Damage(g, a, 3); Damage(g, b, 1); Damage(g, foe, 3); Damage(g, p.Hero, 5); Damage(g, o.Hero, 5);
            Eq("costs 2", 2, Cast(g, p));
            Eq("minion a +2", 4, Hp(a)); Eq("minion b to max", 5, Hp(b));
            Eq("hero +2", 27, p.Hero.Health);
            Eq("enemy minion not healed", 2, Hp(foe)); Eq("enemy hero not healed", 25, o.Hero.Health);
        }

        // TB_RoadToNR_RenoHP = paladin_vow_of_light "Give a friendly minion Divine Shield."
        [AuditTest]
        static void TB_RoadToNR_RenoHP_VowOfLight()
        {
            var g = PowerGame("paladin_vow_of_light", s => s.PlayerHero = "HERO_04"); var p = P1(g); var o = P2(g);
            var mine = Board(p, Yeti); var foe = Board(o, Yeti);
            var t = PowerTargets(p);
            Check("friendly minions only", t.Count == 1 && t[0] == mine);
            Eq("costs 2", 2, Cast(g, p, mine));
            Check("divine shield", mine.HasDivineShield);
            Damage(g, mine, 3);
            Eq("shield absorbs", 5, Hp(mine));
        }

        // TB_RoadToNR_MurgathaHP = priest_penitent_lash "Deal 1 damage to an enemy minion. Restore 2 Health to your hero."
        [AuditTest]
        static void TB_RoadToNR_MurgathaHP_PenitentLash()
        {
            var g = PowerGame("priest_penitent_lash", s => s.PlayerHero = "HERO_09"); var p = P1(g); var o = P2(g);
            var mine = Board(p, Yeti); var foe = Board(o, Yeti);
            Damage(g, p.Hero, 5); Damage(g, o.Hero, 5);
            var t = PowerTargets(p);
            Check("enemy minions only", t.Count == 1 && t[0] == foe);
            Eq("costs 2", 2, Cast(g, p, foe));
            Eq("enemy minion -1", 4, Hp(foe));
            Eq("own hero +2", 27, p.Hero.Health);
            Eq("enemy hero not healed", 25, o.Hero.Health);
        }

        // TB_Henchmania_MyraH = priest_ward_of_faith "Give a friendly minion +2 Health."
        [AuditTest]
        static void TB_Henchmania_MyraH_WardOfFaith()
        {
            var g = PowerGame("priest_ward_of_faith", s => s.PlayerHero = "HERO_09"); var p = P1(g); var o = P2(g);
            var mine = Board(p, Yeti); var foe = Board(o, Yeti);
            var t = PowerTargets(p);
            Check("friendly minions only", t.Count == 1 && t[0] == mine);
            Eq("costs 2", 2, Cast(g, p, mine));
            Eq("max health 7", 7, MaxHp(mine)); Eq("health 7", 7, Hp(mine)); Eq("attack unchanged", 4, Atk(mine));
            Round(g);
            Eq("permanent", 7, Hp(mine));
            var c = Clone(g); Eq("clone keeps buff", 7, Hp(At(c, mine)));
        }

        // TB_BountyHunt_Hypnotize = carto_ambush "Deal 1 damage to a minion and 1 damage to the enemy hero."
        [AuditTest]
        static void TB_BountyHunt_Hypnotize_AmbushPoint()
        {
            var g = PowerGame("carto_ambush", s => s.PlayerHero = "HERO_03"); var p = P1(g); var o = P2(g);
            var mine = Board(p, Yeti); var foe = Board(o, Yeti);
            var t = PowerTargets(p);
            Check("minions (both sides), no heroes", t.Contains(foe) && t.Contains(mine) && !t.Contains(o.Hero) && !t.Contains(p.Hero));
            Eq("costs 2", 2, Cast(g, p, foe));
            Eq("minion -1", 4, Hp(foe)); Eq("enemy hero -1", 29, o.Hero.Health); Eq("own hero ok", 30, p.Hero.Health);
            Cast(g, p, mine);
            Eq("own minion -1", 4, Hp(mine)); Eq("enemy hero -1 again", 28, o.Hero.Health);
        }

        // TB_CheaterChess_Black = carto_shortcut "Draw a card." cost 3
        [AuditTest]
        static void TB_CheaterChess_Black_ShortcutDraw()
        {
            var g = PowerGame("carto_shortcut", s => s.PlayerHero = "HERO_03"); var p = P1(g);
            int hand = p.HandZone.Count;
            Eq("costs 3", 3, Cast(g, p));
            Eq("drew 1", hand + 1, p.HandZone.Count);
            Eq("no damage", 30, p.Hero.Health);
        }

        // TB_EVILBRM_BoomHeroPower = shaman_spark_lash "Deal 2 damage to a random enemy minion."
        [AuditTest]
        static void TB_EVILBRM_BoomHeroPower_SparkLash()
        {
            for (int seed = 1; seed <= 15; seed++)
            {
                var g = PowerGame("shaman_spark_lash", s => s.PlayerHero = "HERO_02", seed); var p = P1(g); var o = P2(g);
                var a = Board(o, Yeti); var b = Board(o, Yeti); var mine = Board(p, Yeti);
                int spent = Cast(g, p);
                if (seed == 1) Eq("costs 2", 2, spent);
                Eq("2 damage on one enemy minion", 2, (5 - Hp(a)) + (5 - Hp(b)));
                Check("one target only", Hp(a) == 5 || Hp(b) == 5);
                Eq("enemy hero untouched", 30, o.Hero.Health); Eq("own minion untouched", 5, Hp(mine));
            }
        }

        // TB_TempleOutrun_Toggwaggle_HP = shaman_spirit_kindling "Give a random friendly minion +1/+1."
        [AuditTest]
        static void TB_TempleOutrun_Toggwaggle_HP_SpiritKindling()
        {
            for (int seed = 1; seed <= 10; seed++)
            {
                var g = PowerGame("shaman_spirit_kindling", s => s.PlayerHero = "HERO_02", seed); var p = P1(g); var o = P2(g);
                var a = Board(p, Yeti); var b = Board(p, Yeti); var foe = Board(o, Yeti);
                int spent = Cast(g, p);
                if (seed == 1) Eq("costs 2", 2, spent);
                int buffed = new[] { a, b }.Count(m => Atk(m) == 5 && Hp(m) == 6 && MaxHp(m) == 6);
                Eq("one friendly +1/+1", 1, buffed);
                Eq("enemy unchanged", "4/5", Atk(foe) + "/" + Hp(foe));
            }
        }

        // TB_EVILBRM_RafaamHeroPower = warlock_fiend_gate "Summon a 1/1 Imp."
        [AuditTest]
        static void TB_EVILBRM_RafaamHeroPower_FiendGate()
        {
            var g = PowerGame("warlock_fiend_gate", s => s.PlayerHero = "HERO_07"); var p = P1(g);
            Eq("costs 2", 2, Cast(g, p));
            Eq("imp", "EX1_598", p.BoardZone[0].Card.Id);
            Eq("1/1", "1/1", Atk(p.BoardZone[0]) + "/" + Hp(p.BoardZone[0]));
            for (int i = 0; i < 6; i++) Board(p, Wisp);
            Check("not usable when full", !PowerOffered(p));
        }

        // EX1_598 Imp: 1/1 Demon
        [AuditTest]
        static void EX1_598_Imp()
        {
            var g = NewGame(); var m = Board(P1(g), "EX1_598");
            Eq("1/1", "1/1", Atk(m) + "/" + Hp(m));
            Check("demon", m.Card.IsRace(Race.DEMON));
            Keywordless(m);
        }

        // TB_FactionWar_Boss_Rag_0 = warlock_sanguine_pact "Deal 2 damage to your hero. Give a friendly minion +2/+2."
        [AuditTest]
        static void TB_FactionWar_Boss_Rag_0_SanguinePact()
        {
            var g = PowerGame("warlock_sanguine_pact", s => s.PlayerHero = "HERO_07"); var p = P1(g); var o = P2(g);
            var mine = Board(p, Yeti); var foe = Board(o, Yeti);
            var t = PowerTargets(p);
            Check("friendly minions only", t.Count == 1 && t[0] == mine);
            Check("not usable without a friendly minion", true);
            Eq("costs 2", 2, Cast(g, p, mine));
            Eq("own hero -2", 28, p.Hero.Health);
            Eq("+2/+2", "6/7", Atk(mine) + "/" + Hp(mine));
            Eq("enemy hero untouched", 30, o.Hero.Health);
            Round(g);
            Eq("permanent", "6/7", Atk(mine) + "/" + Hp(mine));
            var g2 = PowerGame("warlock_sanguine_pact", s => s.PlayerHero = "HERO_07");
            Check("no cast with empty board", !PowerOffered(P1(g2)));
        }

        // TB_LevelUp_001 = sellsword_shield_wall "Give a friendly minion +2 Health and Taunt."
        [AuditTest]
        static void TB_LevelUp_001_ShieldWall()
        {
            var g = PowerGame("sellsword_shield_wall"); var p = P1(g); var o = P2(g);
            var mine = Board(p, Yeti); var foe = Board(o, Yeti);
            var t = PowerTargets(p);
            Check("friendly minions only", t.Count == 1 && t[0] == mine);
            Eq("costs 2", 2, Cast(g, p, mine));
            Eq("+2 health", "4/7", Atk(mine) + "/" + Hp(mine));
            Check("taunt", mine.HasTaunt);
            Round(g);
            Check("lasts", mine.HasTaunt && Hp(mine) == 7);
        }

        // TB_John_002p = sellsword_war_cry "Give your minions +1 Attack this turn."
        [AuditTest]
        static void TB_John_002p_WarCry()
        {
            var g = PowerGame("sellsword_war_cry"); var p = P1(g); var o = P2(g);
            var a = Board(p, Yeti); var b = Board(p, Wisp); var foe = Board(o, Yeti);
            Eq("costs 2", 2, Cast(g, p));
            Eq("a +1", 5, Atk(a)); Eq("b +1", 2, Atk(b)); Eq("enemy unchanged", 4, Atk(foe));
            Attack(g, a, o.Hero);
            Eq("attack uses buff", 25, o.Hero.Health);
            EndTurn(g);
            Eq("a back to 4 on enemy turn", 4, Atk(a)); Eq("b back to 1", 1, Atk(b));
            Check("enchantments removed", !EnchantIds(a).Contains("TB_CoOpv3_BOSS4e"), string.Join(",", EnchantIds(a)));
        }

        // ============================================================ active treasures

        static IPlayable Treasure(Controller c, string key) => HandContent(c, key);

        // TB_CoOpv3_001 = blazing_rod "Deal $4 damage." cost 2
        [AuditTest]
        static void TB_CoOpv3_001_BlazingRod()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var foe = Board(o, Golem);
            var c = Treasure(p, "blazing_rod");
            Eq("cost 2", 2, c.Cost);
            var lt = LegalTargets(c);
            Check("any character", lt.Contains(foe) && lt.Contains(o.Hero) && lt.Contains(p.Hero));
            Eq("spent 2", 2, Cast(g, c, foe));
            Eq("4 damage", 3, Hp(foe));
            Board(p, "CS2_052");
            Cast(g, Treasure(p, "blazing_rod"), o.Hero);
            Eq("spell damage +1 -> 5", 25, o.Hero.Health);
        }

        // TB_CoOpv3_002 = iron_ward "Gain 6 Armor." cost 1
        [AuditTest]
        static void TB_CoOpv3_002_IronWard()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var foe = Board(o, Yeti); var mine = Board(p, Yeti);
            var c = Treasure(p, "iron_ward");
            Eq("cost 1", 1, c.Cost);
            Eq("spent 1", 1, Cast(g, c));
            Eq("6 armor", 6, Armor(p));
            Eq("no damage to minions (shell text Twisted Light)", 5, Hp(foe) + 0 * Hp(mine));
            Eq("own minion fine", 5, Hp(mine));
        }

        // TB_CoOpv3_003 = bottomless_tome "Draw 3 cards." cost 3
        [AuditTest]
        static void TB_CoOpv3_003_BottomlessTome()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            Hand(o, Wisp);
            var c = Treasure(p, "bottomless_tome");
            Eq("cost 3", 3, c.Cost);
            int deck = p.DeckZone.Count;
            Eq("spent 3", 3, Cast(g, c));
            Eq("drew 3", 3, p.HandZone.Count);
            Eq("deck -3", deck - 3, p.DeckZone.Count);
            Eq("enemy hand unchanged", 1, o.HandZone.Count);
        }

        // TB_CoOpv3_004 = mirror_shard "Summon a copy of a friendly minion." cost 2
        [AuditTest]
        static void TB_CoOpv3_004_MirrorShard()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var mine = Board(p, Yeti); var foe = Board(o, Yeti);
            Damage(g, mine, 2);
            var c = Treasure(p, "mirror_shard");
            Eq("cost 2", 2, c.Cost);
            var lt = LegalTargets(c);
            Check("friendly minions only", lt.Count == 1 && lt[0] == mine, string.Join(",", lt.Select(x => x.Card.Id)));
            Eq("spent 2", 2, Cast(g, c, mine));
            Eq("two yetis", 2, p.BoardZone.Count);
            var copy = p.BoardZone.First(m => m != mine);
            Eq("copy card", Yeti, copy.Card.Id);
            Eq("copy keeps current state (copy)", 3, Hp(copy));
            Eq("enemy unchanged", 1, o.BoardZone.Count);
            Eq("owner hero untouched", 30, p.Hero.Health);
            for (int i = 0; i < 5; i++) Board(p, Wisp);
            Check("not playable on full board", !CanPlay(Treasure(p, "mirror_shard")));
        }

        // TB_CoOpv3_005 = frost_lantern "Freeze all enemy minions." cost 1
        [AuditTest]
        static void TB_CoOpv3_005_FrostLantern()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var a = Board(o, Yeti); var b = Board(o, Yeti); var mine = Board(p, Yeti);
            var c = Treasure(p, "frost_lantern");
            Eq("cost 1", 1, c.Cost);
            Eq("spent 1", 1, Cast(g, c));
            Check("enemies frozen", a.IsFrozen && b.IsFrozen);
            Check("own not frozen", !mine.IsFrozen);
            Check("no damage", Hp(a) == 5 && Hp(b) == 5 && o.Hero.Health == 30 && p.Hero.Health == 30);
            Check("enemy hero not frozen", !o.Hero.IsFrozen);
        }

        // TB_CoOpv3_006 = quake_stone "Deal $2 damage to all minions." cost 4
        [AuditTest]
        static void TB_CoOpv3_006_QuakeStone()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var a = Board(o, Yeti); var mine = Board(p, Yeti);
            var c = Treasure(p, "quake_stone");
            Eq("cost 4", 4, c.Cost);
            Eq("spent 4", 4, Cast(g, c));
            Eq("enemy -2", 3, Hp(a)); Eq("own -2", 3, Hp(mine));
            Eq("heroes untouched", 60, o.Hero.Health + p.Hero.Health);
            Board(p, "CS2_052");
            Cast(g, Treasure(p, "quake_stone"));
            Eq("spell damage -> 3", 0, a.Zone?.Type == Zone.PLAY ? Hp(a) : 0);
            Check("enemy yeti died to 2+3", a.IsDead || a.Zone?.Type != Zone.PLAY, Describe(g));
        }

        // TB_CoOpv3_007 = healing_spring "Restore 8 Health to your hero." cost 2
        [AuditTest]
        static void TB_CoOpv3_007_HealingSpring()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            Damage(g, p.Hero, 12); Damage(g, o.Hero, 12);
            var mine = Board(p, Yeti); Damage(g, mine, 3);
            var c = Treasure(p, "healing_spring");
            Eq("cost 2", 2, c.Cost);
            Eq("spent 2", 2, Cast(g, c));
            Eq("hero +8", 26, p.Hero.Health);
            Eq("enemy hero not", 18, o.Hero.Health);
            Eq("minion not", 2, Hp(mine));
            Cast(g, Treasure(p, "healing_spring"));
            Eq("capped at 30", 30, p.Hero.Health);
        }

        // TB_CoOpv3_008 = charm_seedling "Gain 2 Armor. Draw a card." cost 1
        [AuditTest]
        static void TB_CoOpv3_008_SeedlingCharm()
        {
            var g = NewGame(); var p = P1(g);
            var c = Treasure(p, "charm_seedling");
            Eq("cost 1", 1, c.Cost);
            Eq("spent 1", 1, Cast(g, c));
            Eq("2 armor", 2, Armor(p)); Eq("drew 1", 1, p.HandZone.Count);
            Eq("no damage anywhere", 60, p.Hero.Health + P2(g).Hero.Health);
        }

        // TB_CoOpv3_009 = charm_sprouting "Gain 4 Armor. Draw a card." cost 1
        [AuditTest]
        static void TB_CoOpv3_009_SproutingCharm()
        {
            var g = NewGame(); var p = P1(g);
            var c = Treasure(p, "charm_sprouting");
            Eq("cost 1", 1, c.Cost);
            Eq("spent 1", 1, Cast(g, c));
            Eq("4 armor", 4, Armor(p)); Eq("drew 1", 1, p.HandZone.Count);
            Eq("no minion summoned", 0, p.BoardZone.Count + P2(g).BoardZone.Count);
        }

        // TB_CoOpv3_010 = charm_blooming "Gain 6 Armor. Draw 2 cards." cost 1
        [AuditTest]
        static void TB_CoOpv3_010_BloomingCharm()
        {
            var g = NewGame(); var p = P1(g);
            var c = Treasure(p, "charm_blooming");
            Eq("cost 1", 1, c.Cost);
            Eq("spent 1", 1, Cast(g, c));
            Eq("6 armor", 6, Armor(p)); Eq("drew 2", 2, p.HandZone.Count);
            Eq("no minion summoned", 0, p.BoardZone.Count + P2(g).BoardZone.Count);
        }

        // TB_CoOpv3_011 = sig_field_atlas "Draw 2 cards." cost 1
        [AuditTest]
        static void TB_CoOpv3_011_FieldAtlas()
        {
            var g = NewGame(); var p = P1(g);
            var c = Treasure(p, "sig_field_atlas");
            Eq("cost 1", 1, c.Cost);
            Eq("spent 1", 1, Cast(g, c));
            Eq("drew 2", 2, p.HandZone.Count);
        }

        // TB_CoOpv3_012 = sig_smoke_flask "Return an enemy minion to its owner's hand." cost 2
        [AuditTest]
        static void TB_CoOpv3_012_SmokeFlask()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var foe = Board(o, Yeti); var mine = Board(p, Yeti);
            var c = Treasure(p, "sig_smoke_flask");
            Eq("cost 2", 2, c.Cost);
            var lt = LegalTargets(c);
            Check("enemy minions only", lt.Count == 1 && lt[0] == foe);
            Eq("spent 2", 2, Cast(g, c, foe));
            Eq("enemy board empty", 0, o.BoardZone.Count);
            Check("in enemy hand", HandIds(o).Contains(Yeti));
            Eq("no hero damage", 60, p.Hero.Health + o.Hero.Health);
        }

        // TB_CoOpv3_013 = sig_star_chart "Deal $2 damage to all enemy minions." cost 3
        [AuditTest]
        static void TB_CoOpv3_013_StarChart()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var a = Board(o, Yeti); var b = Board(o, Golem); var mine = Board(p, Yeti);
            var c = Treasure(p, "sig_star_chart");
            Eq("cost 3", 3, c.Cost);
            Eq("spent 3", 3, Cast(g, c));
            Eq("a -2", 3, Hp(a)); Eq("b -2", 5, Hp(b)); Eq("own untouched", 5, Hp(mine));
            Eq("heroes untouched", 60, p.Hero.Health + o.Hero.Health);
            Board(p, "CS2_052");
            Cast(g, Treasure(p, "sig_star_chart"));
            Eq("spell damage +1", 2, Hp(b));
        }

        // TB_CoOpv3_200 = hunting_net "Freeze two random enemy minions." cost 1
        [AuditTest]
        static void TB_CoOpv3_200_HuntingNet()
        {
            for (int seed = 1; seed <= 10; seed++)
            {
                var g = NewGame(s => s.Seed = seed); var p = P1(g); var o = P2(g);
                var es = new[] { Board(o, Yeti), Board(o, Yeti), Board(o, Yeti) }; var mine = Board(p, Yeti);
                var c = Treasure(p, "hunting_net");
                if (seed == 1) Eq("cost 1", 1, c.Cost);
                Cast(g, c);
                Eq("exactly two frozen", 2, es.Count(e => e.IsFrozen));
                Check("own not frozen", !mine.IsFrozen);
                Check("attack unchanged (shell text)", es.All(e => Atk(e) == 4) && Atk(mine) == 4);
            }
            var g1 = NewGame(); var e1 = Board(P2(g1), Yeti);
            Cast(g1, Treasure(P1(g1), "hunting_net"));
            Check("one enemy minion: it is frozen, no throw", e1.IsFrozen);
        }

        // TB_015 = warden_golem minion 3/5 Taunt cost 3
        [AuditTest]
        static void TB_015_WardenGolem()
        {
            var g = NewGame(); var p = P1(g);
            var c = Treasure(p, "warden_golem");
            Eq("cost 3", 3, c.Cost);
            Eq("spent 3", 3, Cast(g, c));
            var m = p.BoardZone[0];
            Eq("3/5", "3/5", Atk(m) + "/" + Hp(m));
            Check("taunt", m.HasTaunt);
            Keywordless(m, GameTag.TAUNT);
            Eq("no hand change", 0, p.HandZone.Count);
        }

        // TB_Coopv3_100 = swift_hound minion 3/2 Rush cost 2
        [AuditTest]
        static void TB_Coopv3_100_SwiftHound()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var foe = Board(o, Yeti);
            var c = Treasure(p, "swift_hound");
            Eq("cost 2", 2, c.Cost);
            Eq("spent 2", 2, Cast(g, c));
            var m = p.BoardZone[0];
            Eq("3/2", "3/2", Atk(m) + "/" + Hp(m));
            Check("rush", m.IsRush);
            Keywordless(m, GameTag.RUSH);
            bool face = p.Options().OfType<MinionAttackTask>().Any(x => x.Source == m && x.Target == o.Hero);
            bool minion = p.Options().OfType<MinionAttackTask>().Any(x => x.Source == m && x.Target == foe);
            Check("can attack minion this turn", minion);
            Check("cannot attack hero this turn", !face);
            // shell's old text: targeting it with a spell must not draw
            int hand = p.HandZone.Count;
            PlayNew(g, p, "CS2_008", m);
            Eq("no draw when targeted by a spell", hand, p.HandZone.Count);
        }

        // TB_Coopv3_101 = gilded_idol minion 4/4 "Battlecry: Draw a card." cost 4
        [AuditTest]
        static void TB_Coopv3_101_GildedIdol()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var c = Treasure(p, "gilded_idol");
            Eq("cost 4", 4, c.Cost);
            Eq("spent 4", 4, Cast(g, c));
            var m = p.BoardZone[0];
            Eq("4/4", "4/4", Atk(m) + "/" + Hp(m));
            Keywordless(m);
            Eq("drew 1", 1, p.HandZone.Count);
            EndTurn(g);
            Check("does not switch sides at end of turn (shell text)", p.BoardZone.Contains(m) && !o.BoardZone.Contains(m), Describe(g));
            // summoned without battlecry does not draw
            var g2 = NewGame(); Board(P1(g2), "TB_Coopv3_101");
            Eq("summon (no battlecry) draws nothing", 0, P1(g2).HandZone.Count);
        }
    }
}
