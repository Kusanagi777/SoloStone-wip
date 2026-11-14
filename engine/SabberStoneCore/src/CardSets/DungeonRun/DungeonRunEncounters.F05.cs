using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Auras;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.src.Loader;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.DungeonRun
{
	// Phase 8, family 05: the Dalaran Heist anomalies and twists (global rules both players play under for a whole match) and
	// the tokens they place. The texts are the client's (facts/violet-hold-2026-09.json). A rule with a client "Player
	// Enchantment" card is that enchantment's trigger or aura, attached to each player (a FRIENDLY trigger answers that
	// player's own events, an aura covers that player's own zones). A rule with no such card in the client data states a start
	// condition and is hooks only (RuleHooks): The Coffers, The Prisons, The Carts, Gorged, Hallucinating. Every random pick
	// uses the game RNG and is reported as a random event; every piece of state is an entity tag.
	// DALA_850 Arcane, DALA_864 Growing and DALA_866 Rejuvenating are family 01's (DungeonRunEncounters.F01.cs).
	// DALA_857 Glittering is not scripted: its Treasures are the Dalaran Heist active treasures, run rewards the admission table
	// admits for the run player's run deck only (its row in DungeonRunAdmission.F05.cs states what lifts it).
	public static partial class DungeonRunEncounters
	{
		/// <summary>DALA_858 Fishy ("Murlocs occasionally join the battle"): one chance in this many at the start of each player's turn.</summary>
		public const int FishyOneIn = 4;
		/// <summary>DALA_855 Refreshing: "draws until they have 5 cards".</summary>
		public const int RefreshingHand = 5;
		/// <summary>DALA_860 Dragon Soul: the spells in one turn that summon the Dragon, and the Dragon (Dragon Soul's own 5/5 Dragon Spirit).</summary>
		public const int DragonSoulSpells = 3;
		/// <summary>DALA_865 Explosive: how many minions of one player gain its Deathrattle in one turn (the bound on resummoning pairs).</summary>
		public const int ExplosiveMarksPerTurn = 30;
		public const string DragonSoulDragon = "LOOT_209t";
		/// <summary>DALA_863 Hallucinating: how many Hallucinations (UNG_856) join each deck.</summary>
		public const int HallucinationCount = 10;
		public const string Hallucination = "UNG_856";
		/// <summary>DALA_833 The Carts: the four Carts, and how many each player starts with (7 board slots less 3: four minions).</summary>
		public static readonly IReadOnlyList<string> Carts = new[] { "DALA_833t", "DALA_833t2", "DALA_833t3", "DALA_833t4" };
		public const int CartsPerPlayer = 3;
		public const string VioletPrison = "DALA_832t", CacheOfCash = "DALA_829t", Coin = "GAME_005";
		/// <summary>DALA_862 Reductive: the cost enchantment it puts on hand cards (Emperor Thaurissan's "Costs (1) less."; the client has none of its own for the anomaly).</summary>
		public const string ReductiveEnchantment = "BRM_028e";

		// Pools of scripted cards a rule may yield, per generation filter (a run match's lists differ per generating side).
		private static readonly Dictionary<string, IReadOnlyList<Card>> RulePools = new Dictionary<string, IReadOnlyList<Card>>(StringComparer.Ordinal);

		/// <summary>
		/// The scripted minions a rule may summon for <paramref name="c"/> that pass <paramref name="filter"/> and the run's
		/// generation filter for that side (any class; ordinal by id). <paramref name="name"/> names the filter for the cache.
		/// </summary>
		internal static IReadOnlyList<Card> RuleMinions(Game g, Controller c, IEntity source, string name, Func<Card, bool> filter)
		{
			string key;
			using (g.GenerateFor(c)) key = g.FormatType + "|" + name + "|" + g.GeneratedFilterKey;
			lock (RulePools)
				if (RulePools.TryGetValue(key, out var cached)) return cached;
			var pool = ScriptedPool(g, c, source, card => card.Type == CardType.MINION && !card.Untouchable && filter(card), anyClass: true);
			lock (RulePools) RulePools[key] = pool;
			return pool;
		}

		// The minion a rule's trigger answers is on its controller's board and is a minion, not a permanent (a Cart, a prison, a
		// dormant shrine form: untouchable cards). A minion that is dormant for a while (its own keyword) still gains what the
		// rule gives.
		private static bool OnBoard(Controller c, IPlayable t, out Minion minion)
		{
			minion = t as Minion;
			return minion != null && minion.Zone == c.BoardZone && !minion.Card.Untouchable && !minion.ToBeDestroyed;
		}

		private static void SummonRandom(Game g, Controller c, IEntity source, IReadOnlyList<Card> pool)
		{
			if (pool.Count == 0 || c.BoardZone.IsFull) return;
			g.OnRandomHappened(true);
			Generic.SummonBlock(g, (Minion)Entity.FromCard(c, pool[g.Random.Next(pool.Count)]), -1, source);
		}

		// A rule's token with a script of its own (Token registers a card with none).
		private static void RuleToken(string id, Func<CardDef> script) => Add(id, EncounterCardKind.Token, script);

		private static void F05()
		{
			// ================================================================ anomalies
			// Summons: "After a spell is played, a minion of the same cost is summoned." For the player who played it; the Cost is
			// what the spell cost in hand when it was played (Summoning Stone's reading); a random scripted minion of that Cost the
			// run's generation filter allows; nothing when there is none or the board is full.
			Rule("DALA_851", "DALA_851e", () => new Power
			{
				Trigger = new Trigger(TriggerType.AFTER_CAST)
				{
					TriggerSource = TriggerSource.FRIENDLY,
					SingleTask = new CustomTask((g, c, s, t, stack) =>
					{
						if (!(t is Spell spell)) return;
						int cost = Math.Max(0, spell[GameTag.TAG_LAST_KNOWN_COST_IN_HAND]);
						SummonRandom(g, c, s, RuleMinions(g, c, s, "cost" + cost, card => card.Cost == cost));
					}),
				},
			});

			// Crying: "All Battlecries trigger twice." Rattling: "All Deathrattles trigger twice." (each player's copy is the
			// one-player aura: EncounterRules.BattlecriesTwice / DeathrattlesTwice)
			Rule("DALA_852", "DALA_852e", () => new Power { Aura = EncounterRules.BattlecriesTwice() });
			Rule("DALA_853", "DALA_853e", () => new Power { Aura = EncounterRules.DeathrattlesTwice() });

			// Nesting: "After a minion is played, it gains 'Deathrattle: Summon a 1/1 copy of this minion.'" Played, not summoned:
			// the copy is a new minion of the same card at 1/1 (Popped Out) in the dead minion's place, and gains nothing.
			Rule("DALA_854", "DALA_854e", () => new Power
			{
				Trigger = new Trigger(TriggerType.AFTER_PLAY_MINION)
				{
					TriggerSource = TriggerSource.FRIENDLY,
					SingleTask = new CustomTask((g, c, s, t, stack) =>
					{
						if (OnBoard(c, t, out Minion m)) Generic.AddEnchantmentBlock(g, Cards.FromId("DALA_854e2"), (IPlayable)s, m);
					}),
				},
			});
			Enchantment("DALA_854e2", () => new Power
			{
				DeathrattleTask = new CustomTask((g, c, s, t, stack) =>
				{
					if (!(s is Minion dead) || c.BoardZone.IsFull) return;
					var copy = (Minion)Entity.FromCard(c, dead.Card);
					Generic.SummonBlock(g, copy, Math.Min(Math.Max(0, dead.LastBoardPosition), c.BoardZone.SlotCount), dead);
					if (copy.Zone == c.BoardZone) Generic.AddEnchantmentBlock(g, Cards.FromId("DALA_854e3"), dead, copy);
				}),
			});
			Enchantment("DALA_854e3", () => new Power { Enchant = new Enchant(Effects.SetAttackHealth(1)) });

			// Refreshing: "At the end of each player's turn, that player draws until they have 5 cards." Jeeves's count: the cards
			// missing are counted once, then drawn (an empty deck gives Fatigue for each).
			Rule("DALA_855", "DALA_855e", () => new Power
			{
				Trigger = new Trigger(TriggerType.TURN_END)
				{
					TriggerSource = TriggerSource.FRIENDLY,
					SingleTask = new CustomTask((g, c, s, t, stack) =>
					{
						for (int n = RefreshingHand - c.HandZone.Count; n > 0 && g.State != State.COMPLETE && !c.Hero.ToBeDestroyed && c.Hero.Health > 0; n--)
							Generic.Draw(c);
					}),
				},
			});

			// Wild Magic: "All spells cast an additional time. (targets chosen randomly)" After a player plays a spell from hand, a
			// copy is cast for that player with random targets (EncounterRules.RecastRandom); a copy never casts again.
			Rule("DALA_856", "DALA_856e", () => new Power
			{
				Trigger = new Trigger(TriggerType.AFTER_CAST)
				{
					TriggerSource = TriggerSource.FRIENDLY,
					SingleTask = new CustomTask((g, c, s, t, stack) => EncounterRules.RecastRandom(t as Spell, s)),
				},
			});

			// Fishy: "Murlocs occasionally join the battle." The wiki's note: a small chance at the start of each player's turn
			// that one random Murloc is summoned for that player. The chance is not published: 1 in FishyOneIn (an adaptation).
			Rule("DALA_858", "DALA_858e", () => new Power
			{
				Trigger = new Trigger(TriggerType.TURN_START)
				{
					TriggerSource = TriggerSource.FRIENDLY,
					SingleTask = new CustomTask((g, c, s, t, stack) =>
					{
						g.OnRandomHappened(true);
						if (g.Random.Next(FishyOneIn) != 0) return;
						SummonRandom(g, c, s, RuleMinions(g, c, s, "murloc", card => card.IsRace(Race.MURLOC)));
					}),
				},
			});

			// Gorged: "Both players start the game with two extra cards and Mana Crystals." Two more opening-hand cards for each
			// player, dealt before the mulligan (the engine's opening-hand cap applies), and two Mana Crystals each.
			Rule("DALA_859", null, null, new RuleHooks
			{
				OpeningCards = 2,
				OnApply = g =>
				{
					foreach (Controller c in new[] { g.Player1, g.Player2 }) c.BaseMana = Math.Min(c.BaseMana + 2, Controller.MaxResources);
				},
			});

			// Dragon Soul: "After a player casts 3 spells in a turn, that player summons a 5/5 Dragon." Dragon Soul's own rule and
			// its Dragon Spirit: the count is per player and per turn and starts again after a Dragon (six spells: two Dragons).
			Rule("DALA_860", "DALA_860e", () => new Power
			{
				Trigger = new MultiTrigger(
					new Trigger(TriggerType.AFTER_CAST)
					{
						TriggerSource = TriggerSource.FRIENDLY,
						SingleTask = new CustomTask((g, c, s, t, stack) =>
						{
							int cast = s[EncounterRules.RuleSpells] + 1;
							s[EncounterRules.RuleSpells] = cast < DragonSoulSpells ? cast : 0;
							if (cast >= DragonSoulSpells && !c.BoardZone.IsFull)
								Generic.SummonBlock(g, (Minion)Entity.FromCard(c, Cards.FromId(DragonSoulDragon)), -1, s);
						}),
					},
					new Trigger(TriggerType.TURN_END)
					{
						EitherTurn = true,
						SingleTask = new CustomTask((g, c, s, t, stack) => { if (s[EncounterRules.RuleSpells] != 0) s[EncounterRules.RuleSpells] = 0; }),
					}),
			});

			// Ogreish: "All minions have a 50% chance to attack the wrong enemy." Ogre Warmaul's rule for every attacking minion
			// (EncounterRules.WrongTarget50, family 02), each player's copy for that player's minions. The redirect runs as the
			// attack event reaches the rule (FastExecution), not from the task queue: the rule's enchantment is older than every
			// Secret, so a Secret that answers the attack reads the defender the minion really attacks (Splitting Image validated
			// on the aimed minion and resolved after a redirect to the hero would copy a hero).
			Rule("DALA_861", "DALA_861e", () => new Power
			{
				Trigger = new Trigger(TriggerType.ATTACK) { TriggerSource = TriggerSource.MINIONS, FastExecution = true, SingleTask = EncounterRules.WrongTargetTask },
			});

			// Reductive: "At the end of each player's turn, reduce the cost of cards in their hand by (1)." Emperor Thaurissan's
			// rule and enchantment. A card that can be played again and again in one turn (Echo, "Repeatable this turn") is never
			// brought below (1): at (0) it would repeat without end (the same floor as DALA_850).
			Rule("DALA_862", "DALA_862e", () => new Power
			{
				Trigger = new Trigger(TriggerType.TURN_END)
				{
					TriggerSource = TriggerSource.FRIENDLY,
					SingleTask = new CustomTask((g, c, s, t, stack) =>
					{
						Card enchantment = Cards.FromId(ReductiveEnchantment);
						foreach (IPlayable card in c.HandZone.ToArray())
							if (!(Repeatable(card) && card.Cost <= 1)) Generic.AddEnchantmentBlock(g, enchantment, (IPlayable)s, card);
					}),
				},
			});

			// Hallucinating: "Both players start the game with 10 'Hallucinations' in their deck." (UNG_856: "Discover a card from
			// your opponent's class.") They are shuffled into each deck after both mulligans, as every encounter deck add is, and
			// each owner's deck is recorded for its own-deck ledger (a public effect: both sides know the ten cards).
			Rule("DALA_863", null, null, new RuleHooks
			{
				Cards = new[] { Hallucination },
				OnStart = g =>
				{
					Card card = Cards.FromId(Hallucination);
					foreach (Controller c in new[] { g.FirstPlayer, g.FirstPlayer.Opponent })
					{
						for (int i = 0; i < HallucinationCount; i++) Generic.ShuffleIntoDeck(c, c.Hero, Entity.FromCard(c, card));
						EncounterRules.DeckRewritten(c, c.DeckZone.Select(p => p.Card.Id).OrderBy(id => id, StringComparer.Ordinal));
					}
				},
			});

			// Explosive: "After a minion is summoned, it gains 'Deathrattle: Deal 1 damage to all minions.'"
			// At most ExplosiveMarksPerTurn minions of one player gain it in one turn (the count is on that player's copy of the
			// rule, EncounterRules.RuleSummons, and starts again at each turn end). Without the limit two minions that summon
			// each other when they die never stop: Eternal Bloodpetal (5/1, "Deathrattle: Summon a 0/1 Eternal Seedling") and its
			// Seedling ("Deathrattle: Summon an Eternal Bloodpetal") each explode on the other, and the death phase never ends
			// (found in the wave 2 integration: the boss's search hung on that line against Blackseed).
			Rule("DALA_865", "DALA_865e", () => new Power
			{
				Trigger = new MultiTrigger(
					new Trigger(TriggerType.AFTER_SUMMON)
					{
						TriggerSource = TriggerSource.FRIENDLY,
						SingleTask = new CustomTask((g, c, s, t, stack) =>
						{
							if (!OnBoard(c, t, out Minion m)) return;
							int marked = s[EncounterRules.RuleSummons];
							if (marked >= ExplosiveMarksPerTurn) return;
							s[EncounterRules.RuleSummons] = marked + 1;
							Generic.AddEnchantmentBlock(g, Cards.FromId("DALA_865e2"), (IPlayable)s, m);
						}),
					},
					new Trigger(TriggerType.TURN_END)
					{
						EitherTurn = true,
						SingleTask = new CustomTask((g, c, s, t, stack) => { if (s[EncounterRules.RuleSummons] != 0) s[EncounterRules.RuleSummons] = 0; }),
					}),
			});
			Enchantment("DALA_865e2", () => new Power { DeathrattleTask = new DamageTask(1, EntityType.ALLMINIONS) });

			// Infused: "After a minion is summoned, it gains Taunt, Divine Shield, Rush, or Windfury at random."
			Rule("DALA_867", "DALA_867e", () => new Power
			{
				Trigger = new Trigger(TriggerType.AFTER_SUMMON)
				{
					TriggerSource = TriggerSource.FRIENDLY,
					SingleTask = new CustomTask((g, c, s, t, stack) =>
					{
						if (!OnBoard(c, t, out Minion m)) return;
						g.OnRandomHappened(true);
						switch (g.Random.Next(4))
						{
							case 0: m.HasTaunt = true; break;
							case 1: m.HasDivineShield = true; break;
							case 2: Effects.Rush.ApplyTo(m); break;
							default: Effects.Windfury.ApplyTo(m); break;
						}
					}),
				},
			});

			// ================================================================ twists
			// The Coffers: "Your opponent starts the game with a 'Cache of Cash.'" The twist is the run player's, so the boss
			// starts with the 0/3 Cache of Cash ("Deathrattle: Give each player two Coins."), placed after both mulligans.
			Rule("DALA_829", null, null, new RuleHooks
			{
				Cards = new[] { CacheOfCash },
				OnStart = g =>
				{
					Controller boss = g.Player2;
					if (!boss.BoardZone.IsFull) Generic.SummonBlock(g, (Minion)Entity.FromCard(boss, Cards.FromId(CacheOfCash)), -1, boss.Hero);
				},
			});
			RuleToken(CacheOfCash, () => new CardDef(new Power
			{
				DeathrattleTask = new CustomTask((g, c, s, t, stack) =>
				{
					Card coin = Cards.FromId(Coin);
					foreach (Controller player in new[] { c, c.Opponent })
						for (int i = 0; i < 2; i++) Generic.AddHandPhase.Invoke(player, Entity.FromCard(player, coin));
				}),
			}));

			// The Stench: "All minions' Attack and Health are swapped." As a minion enters play (played or summoned, before its
			// Battlecry), once, with the client's Stinky enchantment (EncounterRules.SwapStatsOnSummon); each player's copy for that
			// player's minions.
			Rule("DALA_831", "DALA_831e", () => new Power
			{
				Trigger = new Trigger(TriggerType.SUMMON) { TriggerSource = TriggerSource.FRIENDLY, SingleTask = EncounterRules.SwapStatsOnSummon("DALA_831e2") },
			});
			Enchantment("DALA_831e2", () => new Power { Enchant = Enchants.Enchants.SetAttackHealthScriptTag });

			// The Prisons: "Both players start the game with a 'Violet Prison.'" The prison card has no text; the wiki: the
			// prisons are dormant, each holds a random minion and is replaced by it after a number of turns equal to the minion's
			// Cost. Each player's prison holds a random scripted minion the run's generation filter allows for that side
			// (EncounterRules.Imprison: the minion and the turns left are public tags of the prison).
			Rule("DALA_832", null, null, new RuleHooks
			{
				Cards = new[] { VioletPrison },
				OnStart = g =>
				{
					Card prison = Cards.FromId(VioletPrison);
					foreach (Controller c in new[] { g.FirstPlayer, g.FirstPlayer.Opponent })
					{
						var pool = RuleMinions(g, c, c.Hero, "prison", card => true);
						if (pool.Count == 0 || c.BoardZone.IsFull) continue;
						g.OnRandomHappened(true);
						EncounterRules.Imprison(c, prison, pool[g.Random.Next(pool.Count)], c.Hero);
					}
				},
			});
			RuleToken(VioletPrison, () => new CardDef(new Power
			{
				Trigger = new Trigger(TriggerType.TURN_START) { SingleTask = new CustomTask((g, c, s, t, stack) => EncounterRules.TickPrison(s as Minion)) },
			}));

			// The Carts: "Both players can only have four minions." Each player starts with three random Carts (Fruit, Fish, Meat,
			// Dessert): permanents that only take board slots (EncounterRules.PlaceBlockers), placed after both mulligans.
			Rule("DALA_833", null, null, new RuleHooks
			{
				Cards = Carts.ToArray(),
				OnStart = g =>
				{
					var carts = Carts.Select(Cards.FromId).ToList();
					foreach (Controller c in new[] { g.FirstPlayer, g.FirstPlayer.Opponent }) EncounterRules.PlaceBlockers(c, carts, CartsPerPlayer, c.Hero);
				},
			});
			foreach (string cart in Carts) Token(cart);
		}
	}
}
