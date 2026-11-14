using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Auras;
using SabberStoneCore.Conditions;
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
	// Phase 8, family 06: the Tombs of Terror anomalies and plague twists (global rules both players play under for a whole
	// match, on family 05's framework) and the tokens they place. The texts are the client's (facts/violet-hold-2026-09.json).
	// A rule with a client "Player Enchantment" card is that enchantment's trigger or aura, attached to each player (a FRIENDLY
	// trigger answers that player's own events, an aura covers that player's own zones); a rule with none is hooks only
	// (RuleHooks): Bountiful, Marin's Return, Aleatory, Plague of Death. Every random pick uses the game RNG and is reported as
	// a random event; every piece of state is an entity tag (the Attack-equals-Health aura keeps its values as an aura does).
	// ULDA_720 Battle Scars and ULDA_729 Multiclassing are run-layer rules (plugin DungeonRun: the run hero's Health and
	// carried damage; the classes a card draft offers): they have no match script and are not registered here. The client's
	// "Dummy" cards (ULDA_702d, 705d, 707d, 708d, 712d, 713d, 721d, 724d, 801d, 803d) are the client's own script holders for
	// the player enchantments and are never created: the enchantment's script is here.
	public static partial class DungeonRunEncounters
	{
		/// <summary>ULDA_705 Booms Away: the Bomb ("Casts When Drawn: You take 5 damage"; Seaforium Bomber's).</summary>
		public const string Bomb = "BOT_511t";
		/// <summary>ULDA_708 Dizzying Blows: a randomized Cost is 0 to this (a card that can be played again and again in one turn: 1 to this).</summary>
		public const int DizzyingCostMax = 10;
		public const string DizzyingEnchantment = "ULDA_708e2";
		/// <summary>ULDA_712 Holograms: the enchantment that makes the copy a 1/1 (Barnes's "Attack and Health set to 1."; the client has none of its own for the anomaly).</summary>
		public const string HologramEnchantment = "KAR_114e";
		/// <summary>ULDA_715 Bountiful: "Both players start with 9 cards in their hand."</summary>
		public const int BountifulHand = 9;
		/// <summary>ULDA_719 Marin's Return: the Treasure Chest (Marin the Fox's 0/8 Master Chest: "Deathrattle: Give your opponent a fantastic treasure!").</summary>
		public const string TreasureChest = "LOOT_357l";
		/// <summary>ULDA_724 Spellshields: "they gain 2 Armor".</summary>
		public const int SpellshieldArmor = 2;
		/// <summary>ULDA_801 Plague of Murlocs: the card it shuffles into each deck, how many Murlocs one summons, and the most copies a deck gets.</summary>
		public const string SurpriseMurlocs = "ULDA_801t";
		public const int SurpriseMurlocsMin = 1, SurpriseMurlocsMax = 3, MurlocPlagueMax = 8;
		/// <summary>ULDA_804 Plague of Death: the Eternal Tomb, and how many minions one Tomb summons in one turn (the bound on death chains).</summary>
		public const string EternalTomb = "ULDA_804t";
		public const int TombSummonsPerTurn = 7;

		// A minion whose death a rule answers: a minion, not a permanent (a Cart, a prison, a Tomb, a dormant shrine form), and not
		// a run board shrine (it goes dormant where it stood: it does not die for good, and a copy of it is no shrine).
		private static bool Died(IPlayable t, out Minion minion)
		{
			minion = t as Minion;
			return minion != null && !minion.Card.Untouchable && !ShrineRules.IsMarked(minion);
		}

		/// <summary>
		/// ULDA_801 Plague of Murlocs: how many Surprise! Murlocs! each deck gets in this match. The wiki: one in round 1, one more
		/// each round; a run fights more rounds than a chapter's eight, so the count is the run's fight number capped at
		/// <see cref="MurlocPlagueMax"/> (the run context's defeated bosses; 1 outside a run).
		/// </summary>
		public static int MurlocPlagueCopies(Game g) => Math.Max(1, Math.Min(MurlocPlagueMax, 1 + RunHelpers.Bosses(g)));

		/// <summary>
		/// Shuffles <paramref name="copies"/> Surprise! Murlocs! into <paramref name="c"/>'s deck as a public generated shuffle
		/// (both sides see what the cards are, neither learns a position). Returns how many went in (a full deck takes none).
		/// Family 24 carries the plague between Vesh's phases with it.
		/// </summary>
		public static int ShuffleSurpriseMurlocs(Controller c, int copies, IEntity source)
		{
			Card card = Cards.FromId(SurpriseMurlocs);
			int done = 0;
			for (int i = 0; i < copies; i++)
				if (Generic.ShuffleIntoDeck(c, source, Entity.FromCard(c, card), revealGenerated: true)) done++;
			return done;
		}

		// Surprise! Murlocs!: "Summon a random amount of Murlocs." SurpriseMurlocsMin to SurpriseMurlocsMax random scripted
		// Murlocs the run's generation filter allows for the summoning side (the pool of DALA_858 Fishy), while the board has room.
		private static ISimpleTask SummonSurpriseMurlocs => new CustomTask((g, c, s, t, stack) =>
		{
			g.OnRandomHappened(true);
			int count = SurpriseMurlocsMin + g.Random.Next(SurpriseMurlocsMax - SurpriseMurlocsMin + 1);
			var pool = RuleMinions(g, c, s, "murloc", card => card.IsRace(Race.MURLOC));
			for (int i = 0; i < count && g.State != State.COMPLETE; i++) SummonRandom(g, c, s, pool);
		});

		// The Eternal Tomb's next minion: a random scripted minion the run's generation filter allows for its side, held as the
		// Tomb's public mouse-over card with the deaths left (EncounterRules.Imprison's tags).
		private static void Entomb(Minion tomb)
		{
			Game g = tomb.Game;
			Controller c = tomb.Controller;
			var pool = RuleMinions(g, c, tomb, "tomb", card => true);
			if (pool.Count == 0)
			{
				tomb[GameTag.ALTERNATE_MOUSE_OVER_CARD] = tomb.Card.AssetId;
				tomb[GameTag.SCORE_VALUE_1] = 0;
				tomb[GameTag.TAG_SCRIPT_DATA_NUM_2] = 0;
				return;
			}
			g.OnRandomHappened(true);
			Card inside = pool[g.Random.Next(pool.Count)];
			int deaths = Math.Max(1, inside.Cost);
			tomb[GameTag.ALTERNATE_MOUSE_OVER_CARD] = inside.AssetId;
			tomb[GameTag.SCORE_VALUE_1] = deaths;
			tomb[GameTag.TAG_SCRIPT_DATA_NUM_2] = deaths;
		}

		/// <summary>ULDA_804 Plague of Death: the minion card <paramref name="tomb"/> holds, or null (EncounterRules.Imprisoned).</summary>
		public static Card Entombed(Minion tomb) => EncounterRules.Imprisoned(tomb);

		/// <summary>ULDA_804 Plague of Death: the friendly deaths left before <paramref name="tomb"/> summons its minion.</summary>
		public static int TombDeathsLeft(Minion tomb) => EncounterRules.PrisonTurns(tomb);

		// A friendly minion died: one death less; at 0 the Tomb summons its minion (when its side has room) and holds a new one.
		private static void TombDeath(Minion tomb)
		{
			if (tomb == null || tomb.Zone != tomb.Controller.BoardZone || tomb.Game.State == State.COMPLETE) return;
			Card inside = Entombed(tomb);
			if (inside == null) return;
			int left = TombDeathsLeft(tomb) - 1;
			if (left > 0)
			{
				tomb[GameTag.SCORE_VALUE_1] = left;
				tomb[GameTag.TAG_SCRIPT_DATA_NUM_2] = left;
				return;
			}
			Game g = tomb.Game;
			Controller c = tomb.Controller;
			int summoned = tomb[EncounterRules.RuleTombSummons];
			if (summoned >= TombSummonsPerTurn)
			{
				// The turn's bound is reached: the minion waits for the next death of a later turn.
				tomb[GameTag.SCORE_VALUE_1] = 1;
				tomb[GameTag.TAG_SCRIPT_DATA_NUM_2] = 1;
				return;
			}
			if (!c.BoardZone.IsFull)
			{
				tomb[EncounterRules.RuleTombSummons] = summoned + 1;
				Generic.SummonBlock(g, (Minion)Entity.FromCard(c, inside), -1, tomb);
			}
			Entomb(tomb);
		}

		private static void F06()
		{
			// ================================================================ anomalies
			// Loot-Filled: "After a minion is played, it gives the opposing player a Coin." Played, not summoned; a full hand takes
			// none.
			Rule("ULDA_702", "ULDA_702e", () => new Power
			{
				Trigger = new Trigger(TriggerType.AFTER_PLAY_MINION)
				{
					TriggerSource = TriggerSource.FRIENDLY,
					SingleTask = new CustomTask((g, c, s, t, stack) =>
					{
						Controller other = c.Opponent;
						if (!(t is Minion) || other.HandZone.IsFull) return;
						Generic.AddHandPhase.Invoke(other, Entity.FromCard(other, Cards.FromId(Coin)));
					}),
				},
			});

			// Booms Away: "Each time a minion dies, shuffle a bomb into its owner's deck. When drawn, it explodes for 5 damage."
			// Seaforium Bomber's Bomb, into the deck of the player who controlled the minion when it died; a public generated
			// shuffle (both sides see a Bomb go in).
			Rule("ULDA_705", "ULDA_705e", () => new Power
			{
				Trigger = new Trigger(TriggerType.DEATH)
				{
					TriggerSource = TriggerSource.MINIONS,
					SingleTask = new CustomTask((g, c, s, t, stack) =>
					{
						if (Died(t, out _)) Generic.ShuffleIntoDeck(c, s, Entity.FromCard(c, Cards.FromId(Bomb)), revealGenerated: true);
					}),
				},
			});

			// No-Face: "Heroes are Immune while they control minions." (EncounterRules.HeroImmuneWhile: permanents and dormant
			// minions do not count.)
			Rule("ULDA_706", "ULDA_706e", () => new Power { Aura = EncounterRules.HeroImmuneWhile() });

			// Eternal Army: "After a minion dies, shuffle a copy of it into its owner's deck." A new card of the dead minion's card
			// (no enchantments), into the deck of the player who controlled it when it died; a public generated shuffle.
			Rule("ULDA_707", "ULDA_707e", () => new Power
			{
				Trigger = new Trigger(TriggerType.DEATH)
				{
					TriggerSource = TriggerSource.MINIONS,
					SingleTask = new CustomTask((g, c, s, t, stack) =>
					{
						if (Died(t, out Minion dead)) Generic.ShuffleIntoDeck(c, s, Entity.FromCard(c, dead.Card), revealGenerated: true);
					}),
				},
			});

			// Dizzying Blows: "After a hero is damaged, randomize the Cost of all cards in that player's hand." Each card's Cost is
			// set to a random 0 to DizzyingCostMax (the client's "Cost Randomized." enchantment; an earlier one on the card is
			// replaced); a card that can be played again and again in one turn is never set below (1), as DALA_850 never brings
			// one there.
			Rule("ULDA_708", "ULDA_708e", () => new Power
			{
				Trigger = new Trigger(TriggerType.TAKE_DAMAGE)
				{
					TriggerSource = TriggerSource.FRIENDLY,
					Condition = new SelfCondition(p => p is Hero),
					SingleTask = new CustomTask((g, c, s, t, stack) =>
					{
						if (c.HandZone.IsEmpty || g.State == State.COMPLETE) return;
						Card enchantment = Cards.FromId(DizzyingEnchantment);
						g.OnRandomHappened(true);
						foreach (IPlayable card in c.HandZone.ToArray())
						{
							int low = Repeatable(card) ? 1 : 0;
							int cost = low + g.Random.Next(DizzyingCostMax - low + 1);
							if (card.AppliedEnchantments != null)
								foreach (Enchantment old in card.AppliedEnchantments.Where(e => e.Card.Id == DizzyingEnchantment).ToArray()) old.Remove();
							Generic.AddEnchantmentBlock(g, enchantment, (IPlayable)s, card, cost);
						}
					}),
				},
			});
			Enchantment(DizzyingEnchantment, () => new Power { Enchant = new Enchant(Effects.SetCost(0)) { UseScriptTag = true } });

			// Angelic Presence: "Minions' Attack is ALWAYS equal to their Health." (EncounterRules.AttackEqualsHealth: Lightspawn's
			// rule for each player's board.)
			Rule("ULDA_709", "ULDA_709e", () => new Power { Aura = EncounterRules.AttackEqualsHealth() });

			// CHARGE!: "All minions have Charge." Relentless: "All minions have Windfury." Board auras with the client's
			// enchantments, each player's copy for that player's minions.
			Rule("ULDA_710", "ULDA_710e", () => new Power { Aura = new Aura(AuraType.BOARD, "ULDA_710e2") });
			Enchantment("ULDA_710e2", () => new Power { Enchant = new Enchant(Effects.Charge) });
			Rule("ULDA_711", "ULDA_711e", () => new Power { Aura = new Aura(AuraType.BOARD, "ULDA_711e2") });
			Enchantment("ULDA_711e2", () => new Power { Enchant = new Enchant(Effects.Windfury) });

			// Holograms: "After a minion is played, summon a 1/1 copy of it for the opposing side." Played, not summoned; the copy
			// is a new minion of the same card at 1/1 (no Battlecry: it is summoned); nothing when the played minion has left play
			// or the other board is full.
			Rule("ULDA_712", "ULDA_712e", () => new Power
			{
				Trigger = new Trigger(TriggerType.AFTER_PLAY_MINION)
				{
					TriggerSource = TriggerSource.FRIENDLY,
					SingleTask = new CustomTask((g, c, s, t, stack) =>
					{
						Controller other = c.Opponent;
						if (!OnBoard(c, t, out Minion played) || other.BoardZone.IsFull) return;
						var copy = (Minion)Entity.FromCard(other, played.Card);
						Generic.SummonBlock(g, copy, -1, s);
						if (copy.Zone == other.BoardZone) Generic.AddEnchantmentBlock(g, Cards.FromId(HologramEnchantment), (IPlayable)s, copy);
					}),
				},
			});

			// Whirling Winds: "At the start of every turn, deal 1 damage to all minions." Each player's copy answers that player's
			// own turn start, so every turn has one gust.
			Rule("ULDA_713", "ULDA_713e", () => new Power
			{
				Trigger = new Trigger(TriggerType.TURN_START)
				{
					TriggerSource = TriggerSource.FRIENDLY,
					SingleTask = new DamageTask(1, EntityType.ALLMINIONS),
				},
			});

			// In the Face: "Heroes take double damage." The mark is a controller tag Character.TakeDamage reads
			// (EncounterRules.HeroDamageTaken); the player enchantment shows the rule.
			Rule("ULDA_714", "ULDA_714e", () => new Power(), new RuleHooks
			{
				OnApply = g =>
				{
					foreach (Controller c in new[] { g.Player1, g.Player2 }) c[EncounterRules.HeroDamageDouble] = 1;
				},
			});

			// Bountiful: "Both players start with 9 cards in their hand." Dealt before the mulligan; the boss's Coin is one of its nine.
			Rule("ULDA_715", null, null, new RuleHooks { OpeningHand = BountifulHand });

			// Marin's Return: "Both players start with a Treasure Chest. (Break it for awesome loot!)" Marin the Fox's Master
			// Chest on each player's own side, placed after both mulligans: breaking the enemy's chest gives its breaker a treasure.
			Rule("ULDA_719", null, null, new RuleHooks
			{
				Cards = new[] { TreasureChest },
				OnStart = g =>
				{
					Card chest = Cards.FromId(TreasureChest);
					foreach (Controller c in new[] { g.FirstPlayer, g.FirstPlayer.Opponent })
						if (!c.BoardZone.IsFull) Generic.SummonBlock(g, (Minion)Entity.FromCard(c, chest), -1, c.Hero);
				},
			});

			// Aleatory: "At the start of the game, shuffle both decks together and give half to each player." After both
			// mulligans, with every encounter deck add (EncounterRules.MixDecks: a card a side may not be given stays).
			Rule("ULDA_721", null, null, new RuleHooks { OnStart = g => EncounterRules.MixDecks(g) });

			// Spellshields: "After a player casts a spell, they gain 2 Armor." A countered spell was not cast.
			Rule("ULDA_724", "ULDA_724e", () => new Power
			{
				Trigger = new Trigger(TriggerType.AFTER_CAST)
				{
					TriggerSource = TriggerSource.FRIENDLY,
					SingleTask = new CustomTask((g, c, s, t, stack) =>
					{
						if (t is Spell spell && !spell.IsCountered && g.State != State.COMPLETE) c.Hero.GainArmor((IPlayable)s, SpellshieldArmor);
					}),
				},
			});

			// ================================================================ twists
			// Plague of Murlocs: "Murlocs have invaded each deck!" Surprise! Murlocs! are shuffled into each deck after both
			// mulligans (MurlocPlagueCopies of them), and each owner's deck is recorded for its own-deck ledger (a public effect:
			// both sides know the cards). The player enchantment shows the twist.
			Rule("ULDA_801", "ULDA_801e", () => new Power(), new RuleHooks
			{
				Cards = new[] { SurpriseMurlocs },
				OnStart = g =>
				{
					int copies = MurlocPlagueCopies(g);
					foreach (Controller c in new[] { g.FirstPlayer, g.FirstPlayer.Opponent })
					{
						ShuffleSurpriseMurlocs(c, copies, c.Hero);
						EncounterRules.DeckRewritten(c, c.DeckZone.Select(p => p.Card.Id).OrderBy(id => id, StringComparer.Ordinal));
					}
				},
			});
			RuleToken(SurpriseMurlocs, () => new CardDef(new Power
			{
				PowerTask = SummonSurpriseMurlocs,
				TopdeckTask = ComplexTask.Create(SummonSurpriseMurlocs, new DrawTask()),
			}));

			// Plague of Madness: "At the end of each player's turn, their right-most minion attacks a random enemy." Mass
			// Hysteria's forced attack (EncounterRules.ForcedAttackRandomEnemy: 0 Attack, Frozen and "Can't attack" do not matter).
			Rule("ULDA_802", "ULDA_802e", () => new Power
			{
				Trigger = new Trigger(TriggerType.TURN_END)
				{
					TriggerSource = TriggerSource.FRIENDLY,
					SingleTask = new CustomTask((g, c, s, t, stack) => EncounterRules.ForcedAttackRandomEnemy(EncounterRules.RightmostMinion(c))),
				},
			});

			// Plague of Wrath: "After a minion takes damage, it gains +1 Attack." The client's Rage enchantment, for a minion that
			// is still in play; each player's copy for that player's minions.
			Rule("ULDA_803", "ULDA_803e", () => new Power
			{
				Trigger = new Trigger(TriggerType.TAKE_DAMAGE)
				{
					TriggerSource = TriggerSource.MINIONS,
					SingleTask = new CustomTask((g, c, s, t, stack) =>
					{
						if (OnBoard(c, t, out Minion m) && m.Health > 0) Generic.AddEnchantmentBlock(g, Cards.FromId("ULDA_803e2"), (IPlayable)s, m);
					}),
				},
			});
			Enchantment("ULDA_803e2", () => new Power { Enchant = new Enchant(Effects.Attack_N(1)) });

			// Plague of Death: "Both players start the game with an 'Eternal Tomb.'" ("When @ friendly minions die, summon a
			// random minion.") The wiki: a permanent that holds a designated minion and summons it once as many friendly minions
			// have died as the minion costs, then starts again with a new one. Each player's Tomb holds a random scripted minion
			// the run's generation filter allows for that side; the minion and the deaths left are public tags of the Tomb.
			Rule("ULDA_804", null, null, new RuleHooks
			{
				Cards = new[] { EternalTomb },
				OnStart = g =>
				{
					Card tomb = Cards.FromId(EternalTomb);
					foreach (Controller c in new[] { g.FirstPlayer, g.FirstPlayer.Opponent })
					{
						var pool = RuleMinions(g, c, c.Hero, "tomb", card => true);
						if (pool.Count == 0 || c.BoardZone.IsFull) continue;
						g.OnRandomHappened(true);
						EncounterRules.Imprison(c, tomb, pool[g.Random.Next(pool.Count)], c.Hero);
					}
				},
			});
			RuleToken(EternalTomb, () => new CardDef(new Power
			{
				Trigger = new MultiTrigger(
					new Trigger(TriggerType.DEATH)
					{
						TriggerSource = TriggerSource.MINIONS,
						SingleTask = new CustomTask((g, c, s, t, stack) => { if (Died(t, out _)) TombDeath(s as Minion); }),
					},
					new Trigger(TriggerType.TURN_END)
					{
						EitherTurn = true,
						SingleTask = new CustomTask((g, c, s, t, stack) => { if (s[EncounterRules.RuleTombSummons] != 0) s[EncounterRules.RuleTombSummons] = 0; }),
					}),
			}));
		}
	}
}
