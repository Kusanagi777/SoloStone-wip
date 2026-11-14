// Audit chunk 18: boss-ember_matron (npc-zee-shaman) + boss-devouring_maw (npc-hunter-face).
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
    internal static class Chunk18
    {
        static int EnemyTotal(Controller o) => o.Hero.Health + o.BoardZone.Sum(m => m.Health);
        static string Pick(Game g, Controller c, int index)
        {
            if (!g.Resolution.HasSuspendedEffect) return Choose(g, c, index);
            int id = c.Choice.Choices[index]; string cid = g.IdEntityDic[id].Card.Id;
            var pd = g.GetPendingDecisions().First(d => d.ControllerId == c.Id);
            bool ok = g.TryAnswer(new SabberStoneCore.Model.Decisions.DecisionAnswer(pd.Id, c.Id, new[] { id }), out string err);
            Check("TryAnswer accepted", ok, err ?? "");
            return cid;
        }
        /// <summary>Replaces the deck after the game started: first id = bottom, last id = top.</summary>
        static void SetDeck(Controller c, List<string> ids) { ClearDeck(c); foreach (var id in ids) DeckTop(c, id); }

        // ================================================================ devouring_maw (hunter)
        // CATA_557 Sylvanas's Triumph: Deal $3 damage. If you've played another copy of this, hit all enemies instead.
        [AuditTest]
        static void CATA_557_SingleThenAllEnemies()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var a = Board(o, Golem); var b = Board(o, Golem);
            var s1 = Hand(p, "CATA_557");
            Check("first copy playable on enemy minion", Play(g, s1, a), Describe(g));
            Eq("target takes 3", 4, Hp(a));
            Eq("other minion untouched", 7, Hp(b));
            Eq("enemy hero untouched", 30, Hp(o.Hero));
            Eq("own hero untouched", 30, Hp(p.Hero));
            var s2 = Hand(p, "CATA_557");
            Check("powered-up copy offers no target (hits all)", !LegalTargets(s2).Any());
            Check("powered-up copy playable", Play(g, s2), Describe(g));
            Eq("second copy: minion a -3", 1, Hp(a));
            Eq("second copy: minion b -3", 4, Hp(b));
            Eq("second copy: enemy hero -3", 27, Hp(o.Hero));
            Eq("second copy: own hero untouched", 30, Hp(p.Hero));
            var c = Clone(g);
            Eq("clone keeps damage", 4, Hp(At(c, b)));
        }

        // CATA_558 Reinforcement Rallier: 2/2 Elusive (can't be targeted by spells or Hero Powers).
        [AuditTest]
        static void CATA_558_Elusive()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var r = Board(o, "CATA_558");
            Eq("atk", 2, Atk(r)); Eq("hp", 2, Hp(r));
            Check("can't be targeted by spells", r.CantBeTargetedBySpells, Describe(g));
            Check("can't be targeted by hero powers", r.CantBeTargetedByHeroPowers, Describe(g));
            var bolt = Hand(p, "CS2_029"); // Fireball
            Check("Fireball can't target it", !LegalTargets(bolt).Any(t => t.Id == r.Id));
            Check("Fireball can still target hero", LegalTargets(bolt).Any(t => t.Id == o.Hero.Id));
        }

        // CORE_BAR_801 Wound Prey: Deal $1 damage. Summon a 1/1 Hyena with Rush.
        [AuditTest]
        static void CORE_BAR_801_DamageAndHyena()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var y = Board(o, Yeti);
            PlayNew(g, p, "CORE_BAR_801", y);
            Eq("yeti -1", 4, Hp(y));
            Eq("hyena summoned", "BAR_035t", BoardIds(p).SingleOrDefault());
            PlayNew(g, p, "CORE_BAR_801", o.Hero);
            Eq("enemy hero -1", 29, Hp(o.Hero));
            Eq("two hyenas", 2, BoardIds(p).Count(i => i == "BAR_035t"));
        }

        // BAR_035t Swift Hyena: 1/1 Rush.
        [AuditTest]
        static void BAR_035t_Rush()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var y = Board(o, Yeti);
            var h = Board(p, "BAR_035t");
            Eq("atk", 1, Atk(h)); Eq("hp", 1, Hp(h));
            Check("rush", h.IsRush);
            Check("can attack minion the turn it's summoned", g.Process(MinionAttackTask.Any(p, h, y)), Describe(g));
            Eq("yeti hit", 4, Hp(y));
            var h2 = Board(p, "BAR_035t");
            Check("can't attack hero the turn summoned", !g.Process(MinionAttackTask.Any(p, h2, o.Hero)) || Hp(o.Hero) == 30, Describe(g));
            Eq("enemy hero untouched", 30, Hp(o.Hero));
        }

        // CORE_DS1_184 Tracking: Discover a card from your deck.
        [AuditTest]
        static void CORE_DS1_184_DiscoverFromDeck()
        {
            var deck = new List<string> { Yeti, Wisp, Golem, "CS2_029", "CS2_024", "EX1_011", "CS2_120" };
            var g = NewGame(); var p = P1(g); SetDeck(p, deck);
            int deckBefore = p.DeckZone.Count;
            var before = DeckIds(p);
            PlayNew(g, p, "CORE_DS1_184");
            var ch = ChoiceIds(g, p);
            Note("offers " + string.Join(",", ch) + " deck " + string.Join(",", before));
            Eq("3 options", 3, ch.Count);
            Check("options from the deck", ch.All(before.Contains));
            Eq("options distinct", ch.Count, ch.Distinct().Count());
            var picked = Pick(g, p, 1);
            Eq("picked in hand", picked, HandIds(p).SingleOrDefault());
            Eq("deck lost exactly the picked card", deckBefore - 1, p.DeckZone.Count);
            Eq("no discards to graveyard", 0, GraveIds(p).Count(id => id != "CORE_DS1_184"));
        }

        // JAIL_380 Smuggled Shovel: 1/2 weapon. Deathrattle: Draw a spell that didn't start in your deck.
        [AuditTest]
        static void JAIL_380_DrawsForeignSpell()
        {
            var g = NewGame(s => s.PlayerDeck = new List<string> { "CS2_029", "CS2_029", "CS2_029", "CS2_029", "CS2_029", "CS2_029", Yeti, Yeti, Yeti, Yeti }); var p = P1(g);
            Note("deck after start " + string.Join(",", DeckIds(p)));
            var foreign = DeckTop(p, "CS2_024"); // Frostbolt shuffled in later
            PlayNew(g, p, "JAIL_380");
            Eq("weapon equipped", "JAIL_380", Weapon(p));
            Eq("weapon atk", 1, p.Hero.Weapon.AttackDamage);
            Eq("weapon durability", 2, p.Hero.Weapon.Durability);
            p.Hero.Weapon.Destroy(); g.DeathProcessingAndAuraUpdate();
            Eq("drew the foreign spell", "CS2_024", HandIds(p).SingleOrDefault());
            Check("drawn entity is the foreign one", p.HandZone.Any(e => e.Id == foreign.Id));
            // nothing foreign: draw nothing
            PlayNew(g, p, "JAIL_380");
            int hand = p.HandZone.Count;
            p.Hero.Weapon.Destroy(); g.DeathProcessingAndAuraUpdate();
            Eq("no foreign spell: no draw", hand, p.HandZone.Count);
        }

        // JAIL_877 Underbelly Network: Location. Summon a 2/1 Rat with "Deathrattle: Draw a card."
        [AuditTest]
        static void JAIL_877_SummonsRat()
        {
            var g = NewGame(); var p = P1(g);
            var loc = PlayNew(g, p, "JAIL_877");
            Note("location zone " + loc.Zone?.Type);
            Check("location on board", loc.Zone?.Type == Zone.PLAY, Describe(g));
            bool ok = g.Process(new ActivateLocationTask(p, loc));
            Check("activate ok", ok, Describe(g));
            var rat = p.BoardZone.FirstOrDefault(m => m.Card.Id == "JAIL_877t");
            Check("rat summoned", rat != null, Describe(g));
            if (rat != null) { Eq("rat atk", 2, Atk(rat)); Eq("rat hp", 1, Hp(rat)); }
            Note("location health " + ((IPlayable)loc)[GameTag.HEALTH] + " dmg " + ((IPlayable)loc)[GameTag.DAMAGE] + " cooldown " + ((IPlayable)loc)[GameTag.LOCATION_ACTION_COOLDOWN]);
            Check("can't activate again this turn", !g.Process(new ActivateLocationTask(p, loc)) || p.BoardZone.Count(m => m.Card.Id == "JAIL_877t") == 1);
            Round(g);
            // A location refreshes at the start of its owner's next turn. JAIL_877 has no printed extended cooldown
            // (facts carry no tag 2353 for it; BE_780 Uldir Quarantine carries [2353,2]).
            Check("activate again on the owner's next turn", g.Process(new ActivateLocationTask(p, loc)),
                $"EXHAUSTED {loc[GameTag.EXHAUSTED]} LOCATION_ACTION_COOLDOWN {loc[GameTag.LOCATION_ACTION_COOLDOWN]} :: " + Describe(g));
            Eq("two rats after next-turn activation", 2, p.BoardZone.Count(m => m.Card.Id == "JAIL_877t"));
        }

        // JAIL_877t Snoot Hoarder: 2/1. Deathrattle: Draw a card.
        [AuditTest]
        static void JAIL_877t_DeathrattleDraw()
        {
            var g = NewGame(); var p = P1(g);
            var r = Board(p, "JAIL_877t");
            int deck = p.DeckZone.Count;
            Kill(g, r);
            Eq("drew a card", 1, p.HandZone.Count);
            Eq("deck -1", deck - 1, p.DeckZone.Count);
        }

        // JAIL_879 Beast Tripwire: Summon a random 5-Cost Beast. Shuffle 2 spells into your deck that do it again when drawn.
        [AuditTest]
        static void JAIL_879_SummonAndShuffle()
        {
            for (int seed = 1; seed <= 4; seed++)
            {
                var g = NewGame(s => s.Seed = seed); var p = P1(g);
                int deck = p.DeckZone.Count;
                PlayNew(g, p, "JAIL_879");
                var m = p.BoardZone.SingleOrDefault();
                Check($"seed {seed}: one minion", m != null, Describe(g));
                if (m != null)
                {
                    Eq($"seed {seed}: 5-cost", 5, m.Card.Cost);
                    Check($"seed {seed}: beast", m.Card.IsRace(Race.BEAST), m.Card.Id);
                }
                Eq($"seed {seed}: 2 tripped shuffled", 2, DeckIds(p).Count(i => i == "JAIL_879t"));
                Eq($"seed {seed}: deck +2", deck + 2, p.DeckZone.Count);
            }
        }

        // JAIL_879t Tripped Beast Tripwire: Casts When Drawn. Summon a random 5-Cost Beast.
        [AuditTest]
        static void JAIL_879t_CastsWhenDrawn()
        {
            var g = NewGame(); var p = P1(g);
            ClearDeck(p); DeckTop(p, Wisp); DeckTop(p, "JAIL_879t");
            Round(g); // P1 draws at start of turn
            Note(Describe(g));
            var m = p.BoardZone.SingleOrDefault();
            Check("beast summoned on draw", m != null && m.Card.Cost == 5 && m.Card.IsRace(Race.BEAST), Describe(g));
            Check("tripwire not left in hand", !HandIds(p).Contains("JAIL_879t"));
            Check("replacement card drawn", HandIds(p).Contains(Wisp), Describe(g));
        }

        // JAIL_881 Arcane Tripwire: Deal $4 damage split among all enemies. Shuffle 2 spells that do it again when drawn.
        [AuditTest]
        static void JAIL_881_SplitAndShuffle()
        {
            for (int seed = 1; seed <= 5; seed++)
            {
                var g = NewGame(s => s.Seed = seed); var p = P1(g); var o = P2(g);
                Board(o, Golem); Board(o, Golem);
                Board(p, Golem);
                int before = EnemyTotal(o);
                PlayNew(g, p, "JAIL_881");
                Eq($"seed {seed}: 4 total enemy damage", before - 4, EnemyTotal(o));
                Eq($"seed {seed}: own side untouched", 30 + 7, p.Hero.Health + p.BoardZone.Sum(x => x.Health));
                Eq($"seed {seed}: 2 tripped shuffled", 2, DeckIds(p).Count(i => i == "JAIL_881t"));
            }
        }

        // JAIL_881t Tripped Arcane Tripwire: Casts When Drawn. Deal $4 damage split among all enemies.
        [AuditTest]
        static void JAIL_881t_CastsWhenDrawn()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            ClearDeck(p); DeckTop(p, Wisp); DeckTop(p, "JAIL_881t");
            EndTurn(g); ClearHand(o);
            int before = EnemyTotal(o);
            EndTurn(g);
            Eq("4 damage to enemies on draw", before - 4, EnemyTotal(o));
            Check("not left in hand", !HandIds(p).Contains("JAIL_881t"));
            Check("replacement drawn", HandIds(p).Contains(Wisp), Describe(g));
        }

        // TIME_600 Precise Shot: Deal $3 damage. If this is EXACTLY in the center of your hand, deal $5 instead.
        [AuditTest]
        static void TIME_600_CenterDealsFive()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var a = Board(o, Golem); var b = Board(o, Golem);
            Hand(p, Wisp); var ps = Hand(p, "TIME_600"); Hand(p, Wisp);
            Play(g, ps, a);
            Eq("center of 3: 5 damage", 2, Hp(a));
            ClearHand(p);
            var ps2 = Hand(p, "TIME_600"); Hand(p, Wisp);
            Play(g, ps2, b);
            Eq("hand of 2: 3 damage", 4, Hp(b));
            ClearHand(p);
            Hand(p, Wisp); Hand(p, Wisp); var ps3 = Hand(p, "TIME_600");
            Play(g, ps3, o.Hero);
            Eq("edge of 3: 3 damage to hero", 27, Hp(o.Hero));
        }

        // TIME_601 Arrow Retriever: Battlecry: Draw until you have 3 cards.
        [AuditTest]
        static void TIME_601_DrawToThree()
        {
            var g = NewGame(); var p = P1(g);
            PlayNew(g, p, "TIME_601");
            Eq("empty hand -> 3 cards", 3, p.HandZone.Count);
            var m = p.BoardZone.Single(); Eq("atk", 3, Atk(m)); Eq("hp", 1, Hp(m));
            ClearHand(p); Hand(p, Wisp); Hand(p, Wisp);
            PlayNew(g, p, "TIME_601");
            Eq("2 in hand (after play) -> 3", 3, p.HandZone.Count);
            ClearHand(p); for (int i = 0; i < 4; i++) Hand(p, Wisp);
            PlayNew(g, p, "TIME_601");
            Eq("4 in hand: no draw", 4, p.HandZone.Count);
        }

        // TIME_606 Quel'dorei Fletcher: Your Hero Power costs (0) while your hand has 3 or less cards.
        [AuditTest]
        static void TIME_606_HeroPowerFree()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            int baseCost = p.Hero.HeroPower.Cost;
            Board(p, "TIME_606");
            Eq("0 cards: hero power 0", 0, p.Hero.HeroPower.Cost);
            for (int i = 0; i < 3; i++) Hand(p, Wisp);
            g.DeathProcessingAndAuraUpdate();
            Eq("3 cards: hero power 0", 0, p.Hero.HeroPower.Cost);
            Note("hero power enchants " + string.Join(",", EnchantIds(p.Hero.HeroPower)));
            Hand(p, Wisp); g.DeathProcessingAndAuraUpdate();
            Eq("4 cards: normal cost", baseCost, p.Hero.HeroPower.Cost);
            ClearHand(p); g.DeathProcessingAndAuraUpdate();
            Eq("back to 0", 0, p.Hero.HeroPower.Cost);
            // enemy's fletcher doesn't help us
            var g2 = NewGame(); Board(P2(g2), "TIME_606"); g2.DeathProcessingAndAuraUpdate();
            Eq("enemy fletcher: our cost unchanged", baseCost, P1(g2).Hero.HeroPower.Cost);
            // actually use it at 0 mana
            Mana(p, 0); p.Hero.HeroPower.IsExhausted = false;
            Check("usable with 0 mana", g.Process(HeroPowerTask.Any(p, o.Hero)), Describe(g));
        }

        // TIME_609 Ranger General Sylvanas: Battlecry: Deal 2 damage to all enemies. If you've played Alleria or Vereesa, repeat for each.
        [AuditTest]
        static void TIME_609_DamageAndRepeat()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var y = Board(o, Golem);
            PlayNew(g, p, "TIME_609");
            Eq("golem -2", 5, Hp(y)); Eq("hero -2", 28, Hp(o.Hero)); Eq("own hero safe", 30, Hp(p.Hero));
            var g2 = NewGame(); var p2 = P1(g2); var o2 = P2(g2);
            var y2 = Board(o2, Golem);
            PlayNew(g2, p2, "TIME_609t2"); // Vereesa
            PlayNew(g2, p2, "TIME_609");
            Eq("with Vereesa: golem -4", 3, Hp(y2)); Eq("with Vereesa: hero -4", 26, Hp(o2.Hero));
            var g3 = NewGame(); var p3 = P1(g3); var o3 = P2(g3);
            PlayNew(g3, p3, "TIME_609t2");
            PlayNew(g3, p3, "TIME_609t1"); while (p3.Choice != null) Pick(g3, p3, 0);
            PlayNew(g3, p3, "TIME_609");
            Eq("with both: hero -6", 24, Hp(o3.Hero));
        }

        // TIME_609t1 Ranger Captain Alleria: Battlecry: Discover a spell. If you've played Sylvanas or Vereesa, repeat for each.
        [AuditTest]
        static void TIME_609t1_DiscoverSpellRepeat()
        {
            var g = NewGame(); var p = P1(g);
            PlayNew(g, p, "TIME_609t1");
            var ch = ChoiceIds(g, p);
            Eq("3 options", 3, ch.Count);
            Check("all spells", ch.All(i => SabberStoneCore.Model.Cards.FromId(i).Type == CardType.SPELL), string.Join(",", ch));
            Pick(g, p, 0);
            Check("no second discover", p.Choice == null);
            Eq("1 spell in hand", 1, p.HandZone.Count);
            var g2 = NewGame(); var p2 = P1(g2);
            PlayNew(g2, p2, "TIME_609"); PlayNew(g2, p2, "TIME_609t2");
            ClearHand(p2);
            PlayNew(g2, p2, "TIME_609t1");
            int n = 0; while (p2.Choice != null && n < 5) { Pick(g2, p2, 0); n++; }
            Eq("with both partners: 3 discovers", 3, n);
            Eq("3 spells in hand", 3, p2.HandZone.Count(c => c.Card.Type == CardType.SPELL));
        }

        // TIME_609t2 Ranger Initiate Vereesa: Battlecry: Give minions in your deck +1/+1. If you've played Alleria or Sylvanas, repeat for each.
        [AuditTest]
        static void TIME_609t2_BuffDeck()
        {
            var g = NewGame(); var p = P1(g); SetDeck(p, new List<string> { Yeti, Yeti, "CS2_029" });
            PlayNew(g, p, "TIME_609t2");
            foreach (var m in p.DeckZone.OfType<Minion>()) { Eq("deck yeti atk 5", 5, m.AttackDamage); Eq("deck yeti hp 6", 6, m.BaseHealth); }
            Check("enchant id", p.DeckZone.OfType<Minion>().All(m => EnchantIds(m).Contains("TIME_609t2e")));
            Check("spell untouched", p.DeckZone.Where(c => c.Card.Type == CardType.SPELL).All(c => EnchantIds(c).Count == 0));
            var self = p.BoardZone.Single(); Eq("Vereesa itself 2/4", 2, Atk(self));
            var g2 = NewGame(); var p2 = P1(g2); SetDeck(p2, new List<string> { Yeti });
            PlayNew(g2, p2, "TIME_609");
            PlayNew(g2, p2, "TIME_609t2");
            var dm = p2.DeckZone.OfType<Minion>().Single();
            Eq("with Sylvanas: +2 atk", 6, dm.AttackDamage); Eq("with Sylvanas: +2 hp", 7, dm.BaseHealth);
            Round(g2);
            var drawn = p2.HandZone.OfType<Minion>().FirstOrDefault();
            Check("buff kept after draw", drawn != null && drawn.AttackDamage == 6, Describe(g2));
        }

        // TLC_249 Sizzling Cinder: 2/1. Deathrattle: Deal 2 damage randomly split among all enemies.
        [AuditTest]
        static void TLC_249_DeathrattleSplit()
        {
            for (int seed = 1; seed <= 6; seed++)
            {
                var g = NewGame(s => s.Seed = seed); var p = P1(g); var o = P2(g);
                Board(o, Golem); Board(o, Golem);
                var c = Board(p, "TLC_249");
                Eq("atk", 2, Atk(c));
                int before = EnemyTotal(o);
                Kill(g, c);
                Eq($"seed {seed}: 2 enemy damage", before - 2, EnemyTotal(o));
                Eq($"seed {seed}: own hero safe", 30, Hp(p.Hero));
            }
            // enemy-owned cinder hits us
            var g2 = NewGame(); var p2 = P1(g2); var o2 = P2(g2);
            var ec = Board(o2, "TLC_249");
            Kill(g2, ec);
            Eq("enemy cinder damages player side", 28, Hp(p2.Hero));
        }

        // ================================================================ ember_matron (shaman)
        // CATA_153 Al'Akir: Colossal +2, Rush, Windfury. Battlecry: Get 2 minions with Cost equal to this minion's Attack. They cost (1).
        [AuditTest]
        static void CATA_153_ColossalAndBattlecry()
        {
            for (int seed = 1; seed <= 3; seed++)
            {
                var g = NewGame(s => { s.PlayerHero = "HERO_02"; s.Seed = seed; }); var p = P1(g);
                var al = (Minion)PlayNew(g, p, "CATA_153");
                Note($"seed {seed}: " + Describe(g));
                Eq("Al'Akir on board", true, p.BoardZone.Contains(al));
                Eq("2 hands summoned", 2, BoardIds(p).Count(i => i.StartsWith("CATA_153t")));
                Check("rush", al.IsRush); Check("windfury", al.HasWindfury);
                Eq("2 cards generated", 2, p.HandZone.Count);
                int atk = Atk(al);
                foreach (var h in p.HandZone)
                {
                    Check("generated is minion", h.Card.Type == CardType.MINION, h.Card.Id);
                    Eq("generated printed cost == attack " + atk, atk, h.Card.Cost);
                    Eq("generated costs 1", 1, h.Cost);
                }
            }
        }

        // CATA_153t1 Charged Hand of Al'Akir: 1/2. Adjacent minions have +{0} Attack.
        [AuditTest]
        static void CATA_153t1_AdjacentAttack()
        {
            var g = NewGame(); var p = P1(g);
            var a = Board(p, Yeti); var h = Board(p, "CATA_153t1"); var b = Board(p, Yeti); var c = Board(p, Yeti);
            int n = SabberStoneCore.Model.Cards.FromId("CATA_153t1")[GameTag.TAG_SCRIPT_DATA_NUM_1];
            Note("SDN1 = " + n + " " + Describe(g));
            Check("bonus positive", n > 0 || h[GameTag.TAG_SCRIPT_DATA_NUM_1] > 0);
            int bonus = h[GameTag.TAG_SCRIPT_DATA_NUM_1];
            Eq("left neighbor +bonus", 4 + bonus, Atk(a));
            Eq("right neighbor +bonus", 4 + bonus, Atk(b));
            Eq("non-adjacent unchanged", 4, Atk(c));
            Eq("hand itself 1/2", 1, Atk(h));
            Kill(g, h);
            Eq("aura gone on death", 4, Atk(a));
        }

        // CATA_497 Ultraxion: Battlecry: Herald. Reduce Deathwing's Cost by ({1}).
        [AuditTest]
        static void CATA_497_HeraldAndDeathwing()
        {
            var g = NewGame(s => s.PlayerHero = "HERO_02"); var p = P1(g);
            var dw = Hand(p, "NEW1_030");
            int dwCost = dw.Cost;
            int before = HeraldRules.Count(p);
            var u = PlayNew(g, p, "CATA_497");
            Note(Describe(g) + " dw " + dwCost + "->" + dw.Cost);
            Eq("herald count +1", before + 1, HeraldRules.Count(p));
            Check("soldier summoned", BoardIds(p).Contains("CATA_565t"), Describe(g));
            Eq("Deathwing cost -1 (no previous heralds)", dwCost - 1, dw.Cost);
            Eq("7 Health", 7, Hp((Minion)u));
            // second Ultraxion after 1 Herald: improved
            var u2 = PlayNew(g, p, "CATA_497");
            Note("after 2nd: dw " + dw.Cost + " herald " + HeraldRules.Count(p));
            Check("second reduces by more (improved by Herald)", dwCost - dw.Cost >= 3, $"total reduction {dwCost - dw.Cost}");
        }

        // CATA_565 Skywall Sentinel: 0/2 Taunt. Battlecry: Herald.
        [AuditTest]
        static void CATA_565_TauntHerald()
        {
            var g = NewGame(s => s.PlayerHero = "HERO_02"); var p = P1(g);
            var s1 = (Minion)PlayNew(g, p, "CATA_565");
            Check("taunt", s1.HasTaunt); Eq("printed atk 0", 0, s1.Card.ATK); Eq("hp 2", 2, Hp(s1));
            Eq("herald 1", 1, HeraldRules.Count(p));
            Check("soldier summoned", BoardIds(p).Contains("CATA_565t"), Describe(g));
            // soldier next to sentinel
            int idx = BoardIds(p).IndexOf("CATA_565t");
            Eq("soldier right of sentinel", s1.ZonePosition + 1, idx);
        }

        // CATA_722 Envoy of the End: 5/4 Taunt. Battlecry: Herald.
        [AuditTest]
        static void CATA_722_TauntHerald()
        {
            var g = NewGame(s => s.PlayerHero = "HERO_02"); var p = P1(g);
            var e = (Minion)PlayNew(g, p, "CATA_722");
            Check("taunt", e.HasTaunt); Eq("printed atk", 5, e.Card.ATK); Eq("hp", 4, Hp(e));
            Eq("herald 1", 1, HeraldRules.Count(p));
            Check("soldier summoned", BoardIds(p).Contains("CATA_565t"), Describe(g));
        }

        // CORE_GIL_531 Witch's Apprentice: 0/1 Taunt. Battlecry: Add a random Shaman spell to your hand.
        [AuditTest]
        static void CORE_GIL_531_ShamanSpell()
        {
            for (int seed = 1; seed <= 4; seed++)
            {
                var g = NewGame(s => { s.Seed = seed; s.PlayerHero = "HERO_02"; }); var p = P1(g);
                var w = (Minion)PlayNew(g, p, "CORE_GIL_531");
                Check("taunt", w.HasTaunt); Eq("0/1", 1, Hp(w));
                var h = p.HandZone.SingleOrDefault();
                Check($"seed {seed}: shaman spell", h != null && h.Card.Type == CardType.SPELL && h.Card.Class == CardClass.SHAMAN, h?.Card.Id);
            }
        }

        // CORE_ULD_191 Beaming Sidekick: Battlecry: Give a friendly minion +2 Health.
        [AuditTest]
        static void CORE_ULD_191_FriendlyHealth()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var y = Board(p, Yeti); var e = Board(o, Yeti);
            var s = Hand(p, "CORE_ULD_191");
            var legal = LegalTargets(s);
            Check("can't target enemy minion", !legal.Any(t => t.Id == e.Id));
            Check("can't target heroes", !legal.Any(t => t is Hero));
            Play(g, s, y);
            Eq("yeti max hp +2", 7, MaxHp(y)); Eq("yeti atk unchanged", 4, Atk(y));
            Check("enchant ULD_191e", EnchantIds(y).Contains("ULD_191e"));
            var g2 = NewGame(); var p2 = P1(g2);
            Check("playable with no target", Play(g2, Hand(p2, "CORE_ULD_191")) && p2.BoardZone.Count == 1);
        }

        // CORE_UNG_809 Fire Fly: Battlecry: Add a 1/2 Elemental to your hand.
        [AuditTest]
        static void CORE_UNG_809_AddFlame()
        {
            var g = NewGame(); var p = P1(g);
            var f = (Minion)PlayNew(g, p, "CORE_UNG_809");
            Eq("1/2", 2, Hp(f)); Eq("atk 1", 1, Atk(f));
            var h = p.HandZone.SingleOrDefault();
            Check("1/2 Elemental in hand", h is Minion m && m.AttackDamage == 1 && m.Health == 2 && h.Card.IsRace(Race.ELEMENTAL), h?.Card.Id);
        }

        // DINO_411 Holy Eggbearer: Battlecry: Draw a 0-Attack minion.
        [AuditTest]
        static void DINO_411_DrawZeroAttack()
        {
            var g = NewGame(); var p = P1(g); SetDeck(p, new List<string> { Yeti, Yeti, "CATA_565", Yeti, "CS2_029" });
            PlayNew(g, p, "DINO_411");
            Eq("drew the 0-attack minion", "CATA_565", HandIds(p).SingleOrDefault());
            PlayNew(g, p, "DINO_411");
            Eq("none left: no draw", 1, p.HandZone.Count);
        }

        // EDR_856 Nightmare Lord Xavius: Battlecry: Discover a minion from your deck. Give it a Dark Gift.
        [AuditTest]
        static void EDR_856_DiscoverGift()
        {
            var g = NewGame(); var p = P1(g); SetDeck(p, new List<string> { Yeti, Wisp, Golem, "CS2_029", "CS2_029", "CS2_120" });
            int deck = p.DeckZone.Count;
            PlayNew(g, p, "EDR_856");
            var ch = ChoiceIds(g, p);
            Note("offers " + string.Join(",", ch));
            Check("3 offers", ch.Count == 3);
            Check("all minions from deck", ch.All(i => DeckIds(p).Contains(i) && SabberStoneCore.Model.Cards.FromId(i).Type == CardType.MINION));
            var picked = Pick(g, p, 0);
            var inHand = p.HandZone.SingleOrDefault();
            Note("picked " + picked + " -> " + inHand?.Card.Id + " enchants " + (inHand == null ? "" : string.Join(",", EnchantIds(inHand))));
            Check("picked minion in hand", inHand != null && inHand.Card.Type == CardType.MINION);
            Eq("deck -1", deck - 1, p.DeckZone.Count);
            Check("has a Dark Gift enchantment", inHand != null && EnchantIds(inHand).Any(e => e.StartsWith("EDR_")), inHand == null ? "" : string.Join(",", EnchantIds(inHand)));
        }

        // JAIL_457 Hijacked Securitybot: Prepare. Battlecry: Give your other minions +1/+1.
        [AuditTest]
        static void JAIL_457_BuffOthers()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var a = Board(p, Yeti); var e = Board(o, Yeti);
            var card = Hand(p, "JAIL_457");
            Check("has Prepare", card[GameTag.PREPARE] != 0);
            Play(g, card);
            var bot = (Minion)card;
            Eq("friendly yeti 5/6", 5, Atk(a)); Eq("friendly yeti hp 6", 6, Hp(a));
            Check("JAIL_457e", EnchantIds(a).Contains("JAIL_457e"));
            Eq("bot itself 3/3", 3, Atk(bot)); Eq("bot hp 3", 3, Hp(bot));
            Eq("enemy untouched", 4, Atk(e));
        }

        // JAIL_458 Tiny Pal: 2/3 weapon. Battlecry: Choose your elemental ammunition! (After your hero attacks, choose another).
        [AuditTest]
        static void JAIL_458_AmmoFireThenChoose()
        {
            var g = NewGame(s => s.PlayerHero = "HERO_02"); var p = P1(g); var o = P2(g);
            var y = Board(o, Yeti);
            PlayNew(g, p, "JAIL_458");
            var ch = ChoiceIds(g, p);
            Eq("4 ammo choices", 4, ch.Count);
            int fire = ch.IndexOf("JAIL_458t2");
            Pick(g, p, fire);
            Eq("weapon becomes Fire form", "JAIL_458t2", Weapon(p));
            Eq("weapon 2 atk", 2, p.Hero.Weapon.AttackDamage); Eq("weapon 3 dur", 3, p.Hero.Weapon.Durability);
            Check("hero attack ok", HeroAttack(g, p, o.Hero), Describe(g));
            Eq("enemy hero 30-2-1", 27, Hp(o.Hero));
            Eq("enemy yeti -1", 4, Hp(y));
            Eq("own hero safe", 30, Hp(p.Hero));
            var ch2 = ChoiceIds(g, p);
            Note("after attack choices " + string.Join(",", ch2));
            Eq("3 other ammo offered", 3, ch2.Count);
            Check("fire not reoffered", !ch2.Contains("JAIL_458t2"));
            int earth = ch2.IndexOf("JAIL_458t3");
            Pick(g, p, earth);
            Eq("weapon now Earth", "JAIL_458t3", Weapon(p));
            Eq("durability 2", 2, p.Hero.Weapon.Durability);
            Round(g);
            Check("hero attack 2", HeroAttack(g, p, o.Hero), Describe(g));
            var sm = p.BoardZone.SingleOrDefault();
            Check("earth: 3-cost minion with taunt", sm != null && sm.Card.Cost == 3 && sm.HasTaunt, Describe(g));
        }

        [AuditTest]
        static void JAIL_458_AmmoFrostAndAir()
        {
            var g = NewGame(s => s.PlayerHero = "HERO_02"); var p = P1(g); var o = P2(g);
            var y1 = Board(o, Yeti); var y2 = Board(o, Yeti); var y3 = Board(o, Yeti);
            PlayNew(g, p, "JAIL_458");
            Pick(g, p, ChoiceIds(g, p).IndexOf("JAIL_458t1"));
            HeroAttack(g, p, y1);
            int frozen = new[] { y2, y3 }.Count(m => m.IsFrozen) + (o.Hero.IsFrozen ? 1 : 0);
            Note(Describe(g));
            Eq("frost: 2 other enemies frozen", 2, frozen);
            Pick(g, p, ChoiceIds(g, p).IndexOf("JAIL_458t4"));
            Round(g); ClearHand(p);
            HeroAttack(g, p, o.Hero);
            var h = p.HandZone.SingleOrDefault();
            Check("air: battlecry minion in hand costing 2 less", h != null && h.Card.Type == CardType.MINION && h.Card[GameTag.BATTLECRY] != 0
                && h.Cost == Math.Max(0, h.Card.Cost - 2), h == null ? Describe(g) : h.Card.Id + " cost " + h.Cost + "/" + h.Card.Cost);
        }

        // JAIL_800 Mug'Zee: Start of Game: If your deck has no other minions, get Mug's Hero Power. If it has no spells, get Zee's!
        [AuditTest]
        static void JAIL_800_StartOfGame()
        {
            var spells = new List<string> { "JAIL_800" }; spells.AddRange(Fill(9, "CS2_029"));
            var g = NewGame(s => { s.PlayerHero = "HERO_02"; s.PlayerDeck = spells; }); var p = P1(g);
            var hp = p.HeroPowers.Select(h => h.Card.Id).ToList();
            Note("mug deck powers " + string.Join(",", hp));
            Check("Mug's power", hp.Contains("JAIL_800hp1"));
            Check("not Zee's", !hp.Contains("JAIL_800hp2"));

            var mins = new List<string> { "JAIL_800" }; mins.AddRange(Fill(9));
            var g2 = NewGame(s => { s.PlayerHero = "HERO_02"; s.PlayerDeck = mins; }); var p2 = P1(g2);
            var hp2 = p2.HeroPowers.Select(h => h.Card.Id).ToList();
            Note("zee deck powers " + string.Join(",", hp2));
            Check("Zee's power", hp2.Contains("JAIL_800hp2"));
            Check("not Mug's", !hp2.Contains("JAIL_800hp1"));

            var mixed = new List<string> { "JAIL_800", Yeti, "CS2_029" }; mixed.AddRange(Fill(7, "CS2_106"));
            var g3 = NewGame(s => { s.PlayerHero = "HERO_02"; s.PlayerDeck = mixed; }); var p3 = P1(g3);
            var hp3 = p3.HeroPowers.Select(h => h.Card.Id).ToList();
            Check("mixed: neither", !hp3.Contains("JAIL_800hp1") && !hp3.Contains("JAIL_800hp2"), string.Join(",", hp3));

            var only = new List<string> { "JAIL_800" }; only.AddRange(Fill(9, "CS2_106"));
            var g4 = NewGame(s => { s.PlayerHero = "HERO_02"; s.PlayerDeck = only; }); var p4 = P1(g4);
            var hp4 = p4.HeroPowers.Select(h => h.Card.Id).ToList();
            Check("Mug'Zee + weapons only: both", hp4.Contains("JAIL_800hp1") && hp4.Contains("JAIL_800hp2"), string.Join(",", hp4));
        }

        // JAIL_800hp2 functional: every fifth minion's battlecry twice (Zee). Smoke check: 5th Fire Fly gives 2 flames.
        [AuditTest]
        static void JAIL_800_ZeeDoublesFifthBattlecry()
        {
            var mins = new List<string> { "JAIL_800" }; mins.AddRange(Fill(9));
            var g = NewGame(s => { s.PlayerHero = "HERO_02"; s.PlayerDeck = mins; }); var p = P1(g);
            for (int i = 0; i < 4; i++) { var m = PlayNew(g, p, Wisp); }
            ClearHand(p);
            PlayNew(g, p, "CORE_UNG_809");
            Eq("5th minion battlecry twice: 2 elementals", 2, p.HandZone.Count);
        }

        // JAIL_800 Mug: first minion each turn costs 2 less, from turn 3.
        [AuditTest]
        static void JAIL_800_MugDiscount()
        {
            var spells = new List<string> { "JAIL_800" }; spells.AddRange(Fill(9, "CS2_029"));
            var g = NewGame(s => { s.PlayerHero = "HERO_02"; s.PlayerDeck = spells; }); var p = P1(g);
            var y = Hand(p, Yeti); g.DeathProcessingAndAuraUpdate();
            Eq("turn 1: no discount", 4, y.Cost);
            Round(g); Round(g); g.DeathProcessingAndAuraUpdate();
            Note("turn " + g.Turn + " yeti cost " + y.Cost);
            Eq("player turn 3: discount", 2, y.Cost);
            Play(g, y); var y2 = Hand(p, Yeti); g.DeathProcessingAndAuraUpdate();
            Eq("second minion: full cost", 4, y2.Cost);
        }

        // JAIL_802 Gallagio Goon: After you play a Battlecry minion, give it +1/+1.
        [AuditTest]
        static void JAIL_802_BuffsBattlecryMinions()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var goon = Board(p, "JAIL_802");
            Eq("goon 2/3", 3, Hp(goon));
            var ff = (Minion)PlayNew(g, p, "CORE_UNG_809");
            Eq("fire fly 2 atk", 2, Atk(ff)); Eq("fire fly 3 hp", 3, Hp(ff));
            Check("JAIL_802e", EnchantIds(ff).Contains("JAIL_802e"));
            var y = (Minion)PlayNew(g, p, Yeti);
            Eq("non-battlecry yeti unchanged", 4, Atk(y));
            var b = Board(p, "CORE_UNG_809");
            Eq("summoned (not played) battlecry minion unchanged", 1, Atk(b));
            EndTurn(g);
            var eff = (Minion)PlayNew(g, o, "CORE_UNG_809");
            Eq("enemy's played battlecry minion unchanged", 1, Atk(eff));
        }

        // JAIL_850 Warden Maiev: After you play a minion, give it +3/+3 and make it go Dormant for 1 turn.
        [AuditTest]
        static void JAIL_850_BuffAndDormant()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var w = Board(p, "JAIL_850");
            Eq("maiev 1/3", 1, Atk(w));
            var y = (Minion)PlayNew(g, p, Yeti);
            Check("dormant", y.IsDormant, Describe(g));
            Check("enchants JAIL_850e + e2", EnchantIds(y).Contains("JAIL_850e2") && EnchantIds(y).Contains("JAIL_850e"), string.Join(",", EnchantIds(y)));
            EndTurn(g);
            Check("still dormant on boss turn", y.IsDormant, Describe(g));
            EndTurn(g);
            Check("awake on player's next turn", !y.IsDormant, Describe(g));
            Eq("yeti 7 atk", 7, Atk(y)); Eq("yeti 8 hp", 8, Hp(y));
            Check("can attack after waking", Attack(g, y, o.Hero), Describe(g));
            Eq("enemy hero -7", 23, Hp(o.Hero));
            // maiev itself not affected (played via Board), summoned minions not affected
            var s = Board(p, Wisp);
            Check("summoned minion not dormant", !s.IsDormant);
            Eq("summoned minion unbuffed", 1, Atk(s));
            // enemy plays a minion: not affected
            EndTurn(g);
            var ey = (Minion)PlayNew(g, o, Yeti);
            Check("enemy minion not dormant", !ey.IsDormant); Eq("enemy minion unbuffed", 4, Atk(ey));
            Check("clone ok", At(Clone(g), y) != null);
        }
    }
}
