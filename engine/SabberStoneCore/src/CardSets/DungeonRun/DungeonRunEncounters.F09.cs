using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.src.Loader;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.DungeonRun
{
	// Phase 8, family 09: Dungeon Run bosses C. The five final bosses (Vustrasz the Ancient, Xol the Unscathed, Azari the
	// Devourer, The Darkness, King Togwaggle), the special rooms (Treasure Vault, Tad, Brimstone Warden, A. F. Kay,
	// Chronomancer Inara) and Mushhuckster Max, with the cards they bring. The texts are the client's
	// (facts/violet-hold-2026-09.json); what the client text does not say is the wiki's (hearthstone.wiki.gg, recorded per row
	// in data/dungeon-run/reference/encounters/F09.json). A clickable power that would do nothing is not offered, by a condition
	// that reads only public boards, public counts or the boss's own hand count. Every random choice uses the game RNG and
	// reports a random event; every piece of state is an entity tag (the room rules: EncounterRules.RoomRules; a power's own
	// counters: TAG_SCRIPT_DATA_NUM_1 / _2 on the hero power entity), so clones and AI simulations replay it.
	public static partial class DungeonRunEncounters
	{
		/// <summary>Vustrasz starts with this many Master Chests (LOOT_357l); Vindictive Breath counts the ones that are missing.</summary>
		public const int MasterChests = 5;
		public const string MasterChest = "LOOT_357l";
		/// <summary>The Treasure Vault closes when it has this many Mana Crystals at the start of its turn.</summary>
		public const int VaultClosesAtMana = 5;
		/// <summary>A. F. Kay plays no card before her turn of this number.</summary>
		public const int KayFirstActiveTurn = 6;
		/// <summary>Tad summons the Sunken Chest at the start of his turn of this number, if he has taken no damage.</summary>
		public const int TadChestTurn = 11;
		/// <summary>King Togwaggle casts Rakanishu at the start of his turn with a chance of 1 in this.</summary>
		public const int RakanishuOneIn = 4;

		/// <summary>Xol's six Beams (Beam Me Up! adds one at random at the end of each of her turns).</summary>
		public static readonly IReadOnlyList<string> Beams = new[]
		{
			"LOOTA_BOSS_26p2", "LOOTA_BOSS_26p3", "LOOTA_BOSS_26p4", "LOOTA_BOSS_26p5", "LOOTA_BOSS_26p6", "LOOTA_BOSS_26p7",
		};

		/// <summary>
		/// The Treasures King Togwaggle's Magic Candle can find (the wiki's list of sixteen; "not every Treasure card can be
		/// generated"), ordinal by id.
		/// </summary>
		public static readonly IReadOnlyList<string> TogwaggleTreasures = new[]
		{
			"LOOTA_806", "LOOTA_812", "LOOTA_816", "LOOTA_819", "LOOTA_821", "LOOTA_823", "LOOTA_826", "LOOTA_827", "LOOTA_835", "LOOTA_836",
			"LOOTA_838", "LOOTA_839", "LOOTA_842b", "LOOTA_843", "LOOTA_847", "LOOT_998k",
		};

		/// <summary>
		/// The ingredients of a Mushroom Potion: the nine effects of the 1-Cost Kazakus Potion (the wiki: Max's potions "are
		/// 1-Mana Kazakus Potions"), in the client's id order. Two different ones make a potion.
		/// </summary>
		public static readonly IReadOnlyList<string> MushroomIngredients = new[]
		{
			"CFM_621t2", "CFM_621t3", "CFM_621t4", "CFM_621t5", "CFM_621t6", "CFM_621t8", "CFM_621t9", "CFM_621t10", "CFM_621t37",
		};
		public const string MushroomPotion = "LOOTA_BOSS_50t", HeartOfFire = "CFM_621t2";

		// What each ingredient does inside a Mushroom Potion: the ingredient cards' own texts, built from plain engine tasks so a
		// potion resolves the same for either player (the client's CFM_621t9 script draws from a class-bound offline pool).
		// Built on first use, never while this class initializes: a task constructor may read the card database, and the card
		// database registers this class's entries while it loads.
		private static IReadOnlyDictionary<string, ISimpleTask> mushroomEffects;
		private static IReadOnlyDictionary<string, ISimpleTask> MushroomEffects => mushroomEffects ?? (mushroomEffects = BuildMushroomEffects());

		private static IReadOnlyDictionary<string, ISimpleTask> BuildMushroomEffects() => new Dictionary<string, ISimpleTask>(StringComparer.Ordinal)
		{
			// Heart of Fire: "Deal $3 damage." (the potion's target)
			["CFM_621t2"] = new DamageTask(3, EntityType.TARGET, true),
			// Stonescale Oil: "Gain 4 Armor."
			["CFM_621t3"] = new ArmorTask(4),
			// Felbloom: "Deal $2 damage to all minions."
			["CFM_621t4"] = new DamageTask(2, EntityType.ALLMINIONS, true),
			// Icecap: "Freeze a random enemy minion."
			["CFM_621t5"] = new CustomTask((g, c, s, t, stack) =>
			{
				Minion pick = RandomOf(g, Touchable(c.Opponent).ToList());
				if (pick != null) new SetGameTagTask(GameTag.FROZEN, 1, EntityType.TARGET).Process(g, c, s, pick, stack);
			}),
			// Goldthorn: "Give your minions +2 Health."
			["CFM_621t6"] = new AddEnchantmentTask("CFM_621e", EntityType.MINIONS),
			// Kingsblood: "Draw a card."
			["CFM_621t8"] = new DrawTask(),
			// Shadow Oil: "Add a random Demon to your hand."
			["CFM_621t9"] = ComplexTask.Create(new RandomMinionTask(GameTag.CARDRACE, (int)Race.DEMON), new AddStackTo(EntityType.HAND)),
			// Netherbloom: "Summon a 2/2 Demon."
			["CFM_621t10"] = new SummonTask("CFM_621_m4"),
			// Ichor of Undeath: "Summon a friendly minion that died this game."
			["CFM_621t37"] = ComplexTask.SummonRandomMinionThatDied(),
		};

		/// <summary>One of <paramref name="list"/> by the game RNG (a random event when there is a choice), or null for an empty list.</summary>
		private static T RandomOf<T>(Game g, IReadOnlyList<T> list) where T : class
		{
			if (list.Count == 0) return null;
			if (list.Count == 1) return list[0];
			g.OnRandomHappened(true);
			return list[g.Random.Next(list.Count)];
		}

		/// <summary>The Master Chests Vustrasz is missing: <see cref="MasterChests"/> less the ones on his board.</summary>
		public static int MissingChests(Controller c) => Math.Max(0, MasterChests - c.BoardZone.Count(m => m.Card.Id == MasterChest));

		/// <summary>
		/// A special room's rules, on its hero power: at the first turn start of the match (either player's) the room's rules land
		/// on the power's owner (EncounterRules.EnterRoom), and at the start of each of its owner's turns <paramref name="ownTurn"/>
		/// runs with that turn's number (the power's TAG_SCRIPT_DATA_NUM_1 counts them).
		/// </summary>
		private static Trigger Room(int rules, Action<Game, Controller, IEntity, int> ownTurn = null) => new Trigger(TriggerType.TURN_START)
		{
			EitherTurn = true,
			SingleTask = new CustomTask((g, c, s, t, stack) =>
			{
				EncounterRules.EnterRoom(c, rules);
				if (g.CurrentPlayer != c) return;
				int turn = s[GameTag.TAG_SCRIPT_DATA_NUM_1] + 1;
				s[GameTag.TAG_SCRIPT_DATA_NUM_1] = turn;
				ownTurn?.Invoke(g, c, s, turn);
			}),
		};

		/// <summary>
		/// A hero power the boss uses by rule, not by a choice of its AI (the wiki's "auto-cast", for the powers whose worth the
		/// AI's evaluation cannot see: burning unknown cards, an extra turn, a Silence of its own minions): at the start of its
		/// owner's turn, when the owner can pay its Cost and <paramref name="when"/> holds, the Cost is paid and
		/// <paramref name="effect"/> runs. The engine never offers it (it is registered with the passive powers); the power's
		/// TAG_SCRIPT_DATA_NUM_2 counts its uses.
		/// </summary>
		private static void AutoCast(string id, Func<Controller, bool> when, Action<Game, Controller, IEntity, TaskStack> effect) =>
			Passive(id, () => new Power
			{
				Trigger = new Trigger(TriggerType.TURN_START)
				{
					SingleTask = new CustomTask((g, c, s, t, stack) =>
					{
						int cost = (s as HeroPower)?.Cost ?? 0;
						if (c.RemainingMana < cost || !when(c)) return;
						c.UsedMana += cost;
						s[GameTag.TAG_SCRIPT_DATA_NUM_2] = s[GameTag.TAG_SCRIPT_DATA_NUM_2] + 1;
						effect(g, c, s, stack);
					}),
				},
			});

		/// <summary>How often a rule-cast power (<see cref="AutoCast"/>) was used this match.</summary>
		public static int AutoCasts(HeroPower power) => power == null ? 0 : power[GameTag.TAG_SCRIPT_DATA_NUM_2];

		/// <summary>
		/// LOOTA_BOSS_22p Catch and Release: "Summon a random minion from your deck." As the wiki states it, "despite the wording"
		/// the minion pulled is the one whose Cost matches Tad's Mana Crystals (Emerald Hive Queen at 1 ... Tyrantus at 10); with
		/// no such minion left in the deck, a random minion of the deck (the card's text). A public summon; nothing with a full
		/// board or no minion left.
		/// </summary>
		public static Minion CatchAndRelease(Controller c, IEntity source)
		{
			Game g = c.Game;
			if (c.BoardZone.IsFull) return null;
			var minions = c.DeckZone.OfType<Minion>().ToList();
			var matching = minions.Where(m => m.Card.Cost == c.BaseMana).ToList();
			Minion pick = RandomOf(g, matching.Count > 0 ? matching : minions);
			if (pick == null) return null;
			Generic.RemoveFromZone.Invoke(c, pick);
			Generic.SummonBlock.Invoke(g, pick, -1, source);
			return pick.Zone == c.BoardZone ? pick : null;
		}

		/// <summary>
		/// LOOTA_BOSS_22t Tad's Pole: "At the end of your turn, summon a minion from your opponent's deck." A random minion of
		/// the opponent's deck leaves it and joins the weapon owner's board (a public summon of a card that changes sides);
		/// nothing with a full board or no minion in that deck.
		/// </summary>
		public static Minion PoleCatch(Controller c, IEntity source)
		{
			Game g = c.Game;
			if (c.BoardZone.IsFull) return null;
			Minion pick = RandomOf(g, c.Opponent.DeckZone.OfType<Minion>().ToList());
			if (pick == null) return null;
			using (g.Facts.CardTransfer(pick, c.Opponent.Id, Zone.DECK, source))
			{
				Generic.RemoveFromZone.Invoke(c.Opponent, pick);
				pick.Controller = c;
				pick[GameTag.CONTROLLER] = c.PlayerId;
				if (pick.AppliedEnchantments != null)
					foreach (Enchantment effect in pick.AppliedEnchantments)
					{
						effect.Controller = c;
						effect[GameTag.CONTROLLER] = c.PlayerId;
					}
				Generic.SummonBlock.Invoke(g, pick, -1, source);
			}
			return pick.Zone == c.BoardZone ? pick : null;
		}

		/// <summary>
		/// LOOTA_BOSS_52t Treasure Coffer: "Deathrattle: Add a random Treasure to the Player's Dungeon Deck." A random card of the
		/// run's treasure pool (RunHelpers.TreasurePool: the active treasures the run could be offered) joins the run deck after
		/// the fight (RunHelpers.EditAdd, applied by the run layer through its deck checks); the Player is the run player
		/// whoever controls the Coffer. Returns the treasure's id, or null with an empty pool (outside a run match).
		/// </summary>
		public static string CofferTreasure(Game g)
		{
			var pool = RunHelpers.TreasurePool(g);
			if (pool.Count == 0) return null;
			if (pool.Count > 1) g.OnRandomHappened(true);
			string id = pool[g.Random.Next(pool.Count)];
			RunHelpers.EditAdd(g, id);
			return id;
		}

		// ---------------------------------------------------------------- Mushroom Potion (Mushhuckster Max)

		/// <summary>
		/// LOOTA_BOSS_50p Mushroom, Mushroom: "Craft a custom Mushroom Potion." Kazakus's craft with the 1-Cost table: a Discover
		/// among three random ingredients, then among three of the others; the potion made of the two joins the hand.
		/// </summary>
		private static readonly ISimpleTask MushroomCraft = new CustomTask((g, c, s, t, stack) =>
		{
			if (c.HandZone.IsFull) return;
			var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
			g.OnRandomHappened(true);
			ctx.Discover(ctx.SampleDistinct(MushroomIngredients.Select(Cards.FromId).ToArray(), 3), ChoiceAction.STACK, MushroomSecond);
		});

		private static readonly ISimpleTask MushroomSecond = new CustomTask((g, c, s, first, stack) =>
		{
			if (first == null) return;
			var rest = MushroomIngredients.Where(id => id != first.Card.Id).Select(Cards.FromId).ToArray();
			var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
			g.OnRandomHappened(true);
			ctx.Discover(ctx.SampleDistinct(rest, 3), ChoiceAction.STACK, MushroomFinish);
		});

		private static readonly ISimpleTask MushroomFinish = new CustomTask((g, c, s, second, stack) =>
		{
			Choice choice = c.Choice;
			if (second == null || choice == null || !g.IdEntityDic.TryGetValue(choice.LastChoice, out IPlayable first)) return;
			GivePotion(c, s, first.Card.Id, second.Card.Id);
		});

		/// <summary>
		/// A Mushroom Potion of two ingredients into <paramref name="c"/>'s hand (nothing with a full hand). The ingredients are
		/// two tags of the potion entity (the definition's modular parts, which the client's text builder names), so the one
		/// card definition plays every potion and every clone carries what it is made of.
		/// </summary>
		public static IPlayable GivePotion(Controller c, IEntity source, string first, string second)
		{
			if (!MushroomIngredients.Contains(first) || !MushroomIngredients.Contains(second) || first == second)
				throw new ArgumentException("A Mushroom Potion is made of two different ingredients of the 1-Cost table.");
			if (c.HandZone.IsFull) return null;
			var tags = new EntityData
			{
				[GameTag.CREATOR] = source.Id, [GameTag.DISPLAYED_CREATOR] = source.Id,
				[GameTag.MODULAR_ENTITY_PART_1] = Cards.FromId(first).AssetId, [GameTag.MODULAR_ENTITY_PART_2] = Cards.FromId(second).AssetId,
			};
			IPlayable potion = Entity.FromCard(c, Cards.FromId(MushroomPotion), tags);
			Generic.AddHandPhase.Invoke(c, potion);
			return potion.Zone == c.HandZone ? potion : null;
		}

		/// <summary>The ingredient ids of a Mushroom Potion entity, first then second (empty for any other entity).</summary>
		public static IReadOnlyList<string> PotionParts(IEntity potion)
		{
			var parts = new List<string>();
			if (potion == null) return parts;
			foreach (GameTag tag in new[] { GameTag.MODULAR_ENTITY_PART_1, GameTag.MODULAR_ENTITY_PART_2 })
			{
				int dbf = potion[tag];
				Card card = dbf == 0 ? null : Cards.FromAssetId(dbf);
				if (card != null && MushroomIngredients.Contains(card.Id)) parts.Add(card.Id);
			}
			return parts;
		}

		private static void F09()
		{
			// ---------------- Brimstone Warden (LOOTA_BOSS_20h): starts with four Stone Golems (the row's start block)
			// Dispel Ward (5): "Silence your minions." Auto-cast (the wiki: "he uses his hero power to silence the Stone Golems
			// once he reaches 5 mana crystals"): at the start of his turn with 5 Mana and a minion that is not silenced yet.
			// The engine's Silence keeps a card's own Can't attack tag, so Dispel Ward lifts it on the minions it silences.
			AutoCast("LOOTA_BOSS_20p", c => c.BoardZone.Any(m => !m.Untouchable && !m.IsSilenced), (g, c, s, stack) =>
			{
				new SilenceTask(EntityType.MINIONS).Process(g, c, s, null, stack);
				foreach (Minion m in c.BoardZone.Where(m => m.IsSilenced && m.CantAttack && !m.Untouchable).ToArray()) m.CantAttack = false;
			});
			// Stone Golem (15/5): "Can't attack. Deathrattle: Lose a Mana Crystal." (Can't attack is the card's own tag; the
			// Mana Crystal is its owner's.)
			BossCard("LOOTA_BOSS_20t", () => new CardDef(new Power
			{
				DeathrattleTask = new CustomTask((g, c, s, t, stack) => Generic.ChangeManaCrystal.Invoke(c, -1, true)),
			}));

			// ---------------- Tad (LOOTA_BOSS_22h): no hand, no draw, no Fatigue; ten minions in his deck
			// Catch and Release (0): "Summon a random minion from your deck." The room: Tad holds no cards and plays none; at the
			// start of his eleventh turn, if he has taken no damage, he summons the Sunken Chest (once).
			Add("LOOTA_BOSS_22p", EncounterCardKind.Power, () => new CardDef(new Power
			{
				PowerTask = new CustomTask((g, c, s, t, stack) => CatchAndRelease(c, s)),
				Trigger = Room(EncounterRules.RoomNoDraw | EncounterRules.RoomNoPlay, (g, c, s, turn) =>
				{
					if (turn < TadChestTurn || s[GameTag.TAG_SCRIPT_DATA_NUM_2] != 0 || c.Hero.Damage > 0 || c.BoardZone.IsFull) return;
					s[GameTag.TAG_SCRIPT_DATA_NUM_2] = 1;
					new SummonTask("LOOTA_103").Process(g, c, s, null, null);
				}),
			}), card => card.PlayAvailabilityPredicate += (c, _) => !c.BoardZone.IsFull, activate: true);
			// Sunken Chest (0/1): "Deathrattle: Add Tad's Pole to your opponent's Dungeon Deck and lose the game." Its owner
			// loses; the Pole joins the run deck when that opponent is the run player.
			BossCard("LOOTA_103", () => new CardDef(new Power
			{
				DeathrattleTask = new CustomTask((g, c, s, t, stack) =>
				{
					if (c.Opponent == g.Player1) RunHelpers.EditAdd(g, "LOOTA_BOSS_22t");
					c.Hero.ToBeDestroyed = true;
				}),
			}));
			// Tad's Pole (weapon): "At the end of your turn, summon a minion from your opponent's deck."
			BossCard("LOOTA_BOSS_22t", () => new CardDef(new Power
			{
				Trigger = new Trigger(TriggerType.TURN_END) { SingleTask = new CustomTask((g, c, s, t, stack) => PoleCatch(c, s)) },
			}));

			// ---------------- Vustrasz the Ancient (LOOTA_BOSS_25h): starts with five Master Chests (the row's start block)
			// Vindictive Breath (0): "Deal 1 damage to all enemies for each missing Master Chest." Not offered while none is missing.
			Power("LOOTA_BOSS_25p", null, () => new CustomTask((g, c, s, t, stack) =>
			{
				int missing = MissingChests(c);
				if (missing > 0) new DamageTask(missing, EntityType.ENEMIES).Process(g, c, s, t, stack);
			}), usable: c => MissingChests(c) > 0);
			// Chromatic Dragonkin (a Blackrock Mountain adventure card his deck lists): "Whenever your opponent casts a spell,
			// gain +2/+2." (Draconic Lineage, BRMA12_8te)
			BossCard("BRMA12_8t", () => new CardDef(new Power
			{
				Trigger = new Trigger(TriggerType.CAST_SPELL) { TriggerSource = TriggerSource.ENEMY, SingleTask = new AddEnchantmentTask("BRMA12_8te", EntityType.SOURCE) },
			}));
			Enchantment("BRMA12_8te", () => new Power { Enchant = new Enchant(Effects.AttackHealth_N(2)) });

			// ---------------- Xol the Unscathed (LOOTA_BOSS_26h)
			// Beam Me Up! (passive): "At the end of your turn, add a Beam to your hand." One of the six at random; a full hand gets none.
			Passive("LOOTA_BOSS_26p8", () => new Power
			{
				Trigger = new Trigger(TriggerType.TURN_END)
				{
					SingleTask = new CustomTask((g, c, s, t, stack) =>
					{
						if (c.HandZone.IsFull) return;
						g.OnRandomHappened(true);
						Generic.AddHandPhase.Invoke(c, Entity.FromCard(c, Cards.FromId(Beams[g.Random.Next(Beams.Count)]),
							new EntityData { [GameTag.CREATOR] = s.Id, [GameTag.DISPLAYED_CREATOR] = s.Id }));
					}),
				},
			});
			// Beam of Frost (3): "Freeze a random enemy minion and the minions next to it."
			BossCard("LOOTA_BOSS_26p2", () => new CardDef(new Power
			{
				PowerTask = new CustomTask((g, c, s, t, stack) =>
				{
					Minion pick = RandomOf(g, Touchable(c.Opponent).ToList());
					if (pick == null) return;
					var freeze = new SetGameTagTask(GameTag.FROZEN, 1, EntityType.TARGET);
					foreach (Minion m in new[] { pick }.Concat(pick.GetAdjacentMinions())) freeze.Process(g, c, s, m, stack);
				}),
			}));
			// Beam of Death (3): "Destroy all damaged enemy minions."
			BossCard("LOOTA_BOSS_26p3", () => new CardDef(new Power
			{
				PowerTask = new CustomTask((g, c, s, t, stack) =>
				{
					foreach (Minion m in Touchable(c.Opponent).Where(m => m.Damage > 0).ToArray())
					{
						DeathContextRules.RecordDestroy(m, s);
						m.Destroy();
					}
				}),
			}));
			// Beam of Confusion (3): "Take control of a random enemy minion." A run shrine never changes sides, so it is never
			// the pick (ShrineRules.CanChangeController); nothing with a full board.
			BossCard("LOOTA_BOSS_26p4", () => new CardDef(new Power
			{
				PowerTask = new CustomTask((g, c, s, t, stack) =>
				{
					if (c.BoardZone.IsFull) return;
					Minion pick = RandomOf(g, Touchable(c.Opponent).Where(ShrineRules.CanChangeController).ToList());
					if (pick != null) new ControlTask(EntityType.TARGET).Process(g, c, s, pick, stack);
				}),
			}));
			// Beam of Fear (3): "Shuffle a random enemy minion into your opponent's deck." A run shrine is destroyed instead (it
			// goes dormant: MoveToDeck asks ShrineRules.InterceptLeave).
			BossCard("LOOTA_BOSS_26p5", () => new CardDef(new Power
			{
				PowerTask = new CustomTask((g, c, s, t, stack) =>
				{
					Minion pick = RandomOf(g, Touchable(c.Opponent).ToList());
					if (pick != null) new MoveToDeck(EntityType.TARGET, opponent: true).Process(g, c, s, pick, stack);
				}),
			}));
			// Beam of Fire (3): "Deal $2 damage to all enemy minions."
			BossCard("LOOTA_BOSS_26p6", () => new CardDef(new Power { PowerTask = new DamageTask(2, EntityType.OP_MINIONS, true) }));
			// Beam of Decay (3): "Destroy 1 of your opponent's Mana Crystal."
			BossCard("LOOTA_BOSS_26p7", () => new CardDef(new Power
			{
				PowerTask = new CustomTask((g, c, s, t, stack) => Generic.ChangeManaCrystal.Invoke(c.Opponent, -1, true)),
			}));

			// ---------------- Azari, the Devourer (LOOTA_BOSS_34h)
			// Devour (0): "Remove the top 2 cards of your opponent's deck." Auto-cast: at the start of each of his turns while the
			// opponent's deck holds a card. Gnomeferatu's rule twice: each card goes from the top of the deck to its owner's
			// graveyard, a public zone (both players see what was burned; the rest of the deck stays hidden and two cards shorter).
			AutoCast("LOOTA_BOSS_34p", c => !c.Opponent.DeckZone.IsEmpty, (g, c, s, stack) =>
			{
				var burn = new MoveToGraveYard(EntityType.OP_TOPDECK);
				for (int i = 0; i < 2; i++) burn.Process(g, c, s, null, stack);
			});

			// ---------------- The Darkness (LOOTA_BOSS_49h): the run player starts with three Luminous Candles (the row's start block)
			// Encroaching Darkness (0): "Summon a 5/5 Darkspawn."
			Power("LOOTA_BOSS_49p", null, () => new SummonTask("LOOTA_BOSS_49t"), usable: c => !c.BoardZone.IsFull);
			Token("LOOTA_BOSS_49t");
			// Luminous Candle (0): "Destroy all Darkspawns." Every Darkspawn in play, on either board.
			BossCard("LOOTA_BOSS_49t2", () => new CardDef(new Power
			{
				PowerTask = new CustomTask((g, c, s, t, stack) =>
				{
					foreach (Minion m in Touchable(c).Concat(Touchable(c.Opponent)).Where(m => m.Card.Id == "LOOTA_BOSS_49t").ToArray())
					{
						DeathContextRules.RecordDestroy(m, s);
						m.Destroy();
					}
				}),
			}));

			// ---------------- Mushhuckster Max (LOOTA_BOSS_50h)
			// Mushroom, Mushroom (2): "Craft a custom Mushroom Potion." Not offered with a full hand.
			Power("LOOTA_BOSS_50p", null, () => MushroomCraft, usable: c => !c.HandZone.IsFull);
			// Mushroom Potion (1): "{0} {1}", its two ingredients in order. It takes a target when one of them is Heart of Fire.
			BossCard(MushroomPotion, () => new CardDef(Reqs(PlayReq.REQ_TARGET_IF_AVAILABLE), new Power
			{
				PowerTask = new CustomTask((g, c, s, t, stack) =>
				{
					foreach (string part in PotionParts(s))
					{
						if (g.State == State.COMPLETE) break;
						if (part == HeartOfFire && !(t is ICharacter)) continue;
						MushroomEffects[part].Process(g, c, s, t, new TaskStack());
					}
				}),
			}), card => card.SourceTargetingPredicate = (source, target) => PotionParts(source).Contains(HeartOfFire));

			// ---------------- Treasure Vault (LOOTA_BOSS_52h): starts with seven Treasure Coffers (the row's start block)
			// Doors Are Closing (5): "End the game." Auto-cast, as the wiki states it ("when the boss reaches 5 mana, it activates
			// its Hero Power, and the level ends in the player's victory"): no choice of the boss's AI, so it is registered as a
			// power the engine never offers; its trigger closes the Vault at the start of its turn once it has 5 Mana Crystals.
			// The room: the Vault holds no cards and plays none.
			Passive("LOOTA_BOSS_52p", () => new Power
			{
				Trigger = Room(EncounterRules.RoomNoDraw | EncounterRules.RoomNoPlay, (g, c, s, turn) =>
				{
					if (c.BaseMana < VaultClosesAtMana) return;
					s[GameTag.TAG_SCRIPT_DATA_NUM_2] = s[GameTag.TAG_SCRIPT_DATA_NUM_2] + 1;
					EncounterRules.EndWithNoLoss(c);
				}),
			});
			// Treasure Coffer (0/10): "Deathrattle: Add a random Treasure to the Player's Dungeon Deck."
			BossCard("LOOTA_BOSS_52t", () => new CardDef(new Power { DeathrattleTask = new CustomTask((g, c, s, t, stack) => CofferTreasure(g)) }));

			// ---------------- Chronomancer Inara (LOOTA_BOSS_53h)
			// Stolen Time (10): "Destroy 3 of your Mana Crystals. Take an extra turn." Auto-cast (the wiki: at 10 Mana "Inara will
			// automatically use her Hero Power"; it "spends all her Mana", and she "drops back to 8 Mana" for the extra turn): at
			// the start of her turn with 10 Mana the Cost is paid, three Mana Crystals are destroyed and she takes an extra turn.
			AutoCast("LOOTA_BOSS_53p", c => true, (g, c, s, stack) =>
			{
				new ManaCrystalEmptyTask(-3).Process(g, c, s, null, stack);
				new ExtraTurnEffectTask().Process(g, c, s, null, stack);
			});
			// Medivh's cards her deck lists (One Night in Karazhan's prologue).
			// Archmage's Insight (3): "Your spells cost (0) this turn." (Insightful, KARA_00_05e, on the player until the turn ends)
			BossCard("KARA_00_05", () => new CardDef(new Power { PowerTask = new AddEnchantmentTask("KARA_00_05e", EntityType.CONTROLLER) }));
			Enchantment("KARA_00_05e", () => new Power
			{
				Aura = new Auras.Aura(Auras.AuraType.HAND, Effects.SetCost(0))
				{
					Condition = Conditions.SelfCondition.IsSpell,
					RemoveTrigger = (TriggerType.TURN_END, null),
				},
			});
			// Astral Portal (1): "Summon a random Legendary minion."
			BossCard("KARA_00_07", () => new CardDef(Reqs(PlayReq.REQ_NUM_MINION_SLOTS), new Power
			{
				PowerTask = ComplexTask.Create(new RandomMinionTask(GameTag.RARITY, (int)Rarity.LEGENDARY), new SummonTask()),
			}));
			// Archmage's Apprentice (2/4): "Whenever you cast a spell, shuffle a copy of it into your deck."
			BossCard("KARA_00_08", () => new CardDef(new Power
			{
				Trigger = new Trigger(TriggerType.CAST_SPELL)
				{
					TriggerSource = TriggerSource.FRIENDLY,
					SingleTask = new CustomTask((g, c, s, t, stack) =>
					{
						if (!(t is Spell cast) || c.DeckZone.IsFull) return;
						Generic.ShuffleIntoDeck(c, s, Entity.FromCard(c, Cards.FromId(cast.Card.Id),
							new EntityData { [GameTag.CREATOR] = s.Id, [GameTag.DISPLAYED_CREATOR] = s.Id }), revealGenerated: true);
					}),
				},
			}));

			// ---------------- A. F. Kay (LOOTA_BOSS_54h)
			// Idle (2): "Do nothing." The room: she plays no card from her hand before her sixth turn (the wiki: "her AI prevents
			// her from playing any of her cards until turn 6", but "it won't stop her from attacking"); her hero power and her
			// attacks stay.
			Add("LOOTA_BOSS_54p", EncounterCardKind.Power, () => new CardDef(new Power
			{
				PowerTask = new CustomTask((g, c, s, t, stack) => { }),
				Trigger = Room(EncounterRules.RoomNoPlay, (g, c, s, turn) =>
				{
					if (turn >= KayFirstActiveTurn) EncounterRules.SetRoomRule(c, EncounterRules.RoomNoPlay, false);
				}),
			}), activate: true);

			// ---------------- King Togwaggle (LOOTA_BOSS_99h)
			// Magic Candle (3): "Find a Treasure!" One of his sixteen Treasures at random into his hand; not offered with a full
			// hand. At the start of his turn he "occasionally" (the wiki) casts Rakanishu: here with a chance of 1 in 4.
			Add("LOOTA_BOSS_99p", EncounterCardKind.Power, () => new CardDef(new Power
			{
				PowerTask = new CustomTask((g, c, s, t, stack) =>
				{
					if (c.HandZone.IsFull) return;
					g.OnRandomHappened(true);
					Generic.AddHandPhase.Invoke(c, Entity.FromCard(c, Cards.FromId(TogwaggleTreasures[g.Random.Next(TogwaggleTreasures.Count)]),
						new EntityData { [GameTag.CREATOR] = s.Id, [GameTag.DISPLAYED_CREATOR] = s.Id }));
				}),
				Trigger = new Trigger(TriggerType.TURN_START)
				{
					SingleTask = new CustomTask((g, c, s, t, stack) =>
					{
						g.OnRandomHappened(true);
						if (g.Random.Next(RakanishuOneIn) != 0) return;
						var spell = (Spell)Entity.FromCard(c, Cards.FromId("LOOTA_BOSS_99t"),
							new EntityData { [GameTag.CREATOR] = s.Id, [GameTag.DISPLAYED_CREATOR] = s.Id });
						new EffectContext(g, c.Id, s.Id, stack: stack).CastSpell(spell.Id, HistoryCastKind.Effect);
					}),
				},
			}), card => card.PlayAvailabilityPredicate += (c, _) => !c.HandZone.IsFull, activate: true);
			// Rakanishu (0): "Deal $4 damage to a random enemy."
			BossCard("LOOTA_BOSS_99t", () => new CardDef(new Power { PowerTask = ComplexTask.DamageRandomTargets(1, EntityType.ENEMIES, 4, true) }));
		}
	}
}
