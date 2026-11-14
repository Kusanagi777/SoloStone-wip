using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Auras;
using SabberStoneCore.Conditions;
using SabberStoneCore.EffectSystem;
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
	public static partial class DungeonRunContent
	{
		// ---------------------------------------------------------------- TombsExplorers
		// The Tombs of Terror explorers' powers (phase 6, family F9): three per explorer, ULDA_<Explorer>_HP1-3, on the explorer's
		// dual-class row (run-v1.json catalogClasses MAGE_ROGUE, PALADIN_SHAMAN, DRUID_PRIEST, HUNTER_WARRIOR; the portraits are
		// NamedHeroes), and ULDA_039 Uldum Treasure Cache, the power Jr. and Sr. Tomb Diver give. Every power, token and
		// enchantment is keyed and shelled by its own client id, so the match shows the real card; every name and text is the
		// client's own, word for word (facts/violet-hold-2026-09.json). The powers are the run player's only
		// (DungeonRunAdmission); no boss deck lists one. Every random choice uses the game RNG over the scripted cards of the
		// generation pool for the receiving side (run exclusions honoured); every piece of state is an entity tag or
		// enchantment, so clones and AI simulations replay it. The ULDA_900-903 "Random Deck" cards are run-layer loadouts
		// (generated random-dual starters built by DungeonRun.GenerateStarter), never match cards, so they have no entry here.

		/// <summary>The twelve explorer powers, by explorer, HP1 to HP3 (the client's ADVENTURE_HERO_POWER ids).</summary>
		public static readonly IReadOnlyList<string> TombsExplorerPowers = new[]
		{
			"ULDA_Reno_HP1", "ULDA_Reno_HP2", "ULDA_Reno_HP3", "ULDA_Finley_HP1", "ULDA_Finley_HP2", "ULDA_Finley_HP3",
			"ULDA_Elise_HP1", "ULDA_Elise_HP2", "ULDA_Elise_HP3", "ULDA_Brann_HP1", "ULDA_Brann_HP2", "ULDA_Brann_HP3",
		};

		/// <summary>The adventure tag of the Tombs of Terror treasures (DungeonRunTreasure.Pool).</summary>
		public const string TombsPool = "Tombs";
		/// <summary>ULDA_039 Uldum Treasure Cache, the power Jr. Tomb Diver (ULDA_016) and Sr. Tomb Diver (ULDA_021) give.</summary>
		public const string UldumTreasureCache = "ULDA_039";
		/// <summary>ULDA_Finley_HP1 New "Recruits"' minion: the 2/1 Amalgam Explorer (every minion type).</summary>
		public const string AmalgamExplorer = "ULDA_Finley_HP1t";
		/// <summary>ULDA_Elise_HP1 Elise's Might's choices: Defensive Posture (+2 Armor) and Offensive Posture (+2 Attack).</summary>
		public const string DefensivePosture = "ULDA_Elise_HP1a", OffensivePosture = "ULDA_Elise_HP1b";
		/// <summary>Offensive Posture's enchantment: the client's own "+2 Attack this turn." hero enchantment (Dire Claws).</summary>
		public const string OffensivePostureEnchantment = "AT_132_DRUIDe";
		/// <summary>ULDA_Elise_HP3 Starseeker's card: Moonfire.</summary>
		public const string StarseekerCard = "CS2_008";
		/// <summary>ULDA_Elise_HP3 Starseeker: how many spells in a turn add the Moonfire.</summary>
		public const int StarseekerSpells = 2;
		/// <summary>On a controller: the game turn Starseeker's spell count is for, and the spells cast in it.</summary>
		public const GameTag StarseekerTurnTag = (GameTag)34680, StarseekerCountTag = (GameTag)34681;
		/// <summary>
		/// ULDA_Brann_HP3 Dino Tracking's player enchantment (the client's own Brann Hero Power Watcher): while its owner has
		/// it, the turn's draw is replaced by a Discover of three distinct cards of the owner's deck, the chosen one drawn
		/// (DiscoverRealRules.TryReplaceNormalDraw, as Commander Geddon's CATA_591e; the cards not chosen stay in the deck).
		/// </summary>
		public const string DinoTrackingEffect = "ULDA_Brann_HPe";
		/// <summary>ULDA_Reno_HP3 Arcane Craftiness: the most volleys it fires (the admission row's loop guard, "RepeatUntil max=60").</summary>
		public const int ArcaneCraftinessMaxVolleys = 60;
		/// <summary>ULDA_Reno_HP3 Arcane Craftiness: missiles per volley.</summary>
		public const int ArcaneCraftinessMissiles = 2;

		/// <summary>Minions in both graveyards: a volley of Arcane Craftiness "killed a minion" when this grew.</summary>
		private static int MinionsInGraveyards(Game g) =>
			g.Player1.GraveyardZone.Count(p => p is Minion) + g.Player2.GraveyardZone.Count(p => p is Minion);

		/// <summary>
		/// ULDA_Reno_HP3 Arcane Craftiness: two missiles, each 1 damage to a random living enemy character (the Arcane Missiles
		/// rule, EX1_277, the hero power's damage bonuses included); when a minion died in the volley, another volley, at most
		/// <see cref="ArcaneCraftinessMaxVolleys"/> (H9: a Deathrattle that keeps summoning cannot loop it forever), and never
		/// after a hero has died.
		/// </summary>
		private static void ArcaneCraftiness(Game g, Controller c, IEntity s)
		{
			RepeatUntil(() => g.State == State.COMPLETE || HeroDown(c) || HeroDown(c.Opponent), ArcaneCraftinessMaxVolleys, () =>
			{
				int before = MinionsInGraveyards(g);
				SplitHits.Inline(g, c, s, ArcaneCraftinessMissiles, ComplexTask.DamageRandomTargets(1, EntityType.ENEMIES, 1), deathProcessing: false);
				g.DeathProcessingAndAuraUpdate();
				return MinionsInGraveyards(g) > before;
			});
		}

		/// <summary>
		/// ULDA_Finley_HP2 Bubble Blower's Discover pool: the scripted Shaman and Paladin minions with Battlecry of the generation
		/// pool <paramref name="c"/> may be given (run exclusions honoured); distinct, ordinal by id.
		/// </summary>
		internal static IReadOnlyList<Card> BubbleBlowerPool(Game g, Controller c, IEntity source)
		{
			using (g.GenerateFor(c))
				return RandomCardTask.GetCardList(source, CardType.MINION)
					.Where(card => card.Implemented && card.Type == CardType.MINION && card[GameTag.BATTLECRY] != 0
						&& (card.Class == CardClass.SHAMAN || card.Class == CardClass.PALADIN))
					.GroupBy(card => card.Id, StringComparer.Ordinal).Select(x => x.First()).OrderBy(card => card.Id, StringComparer.Ordinal).ToArray();
		}

		/// <summary>
		/// ULDA_Brann_HP2 Well Equipped's pool: the scripted weapons of the generation pool <paramref name="c"/> may be given (any
		/// class; run exclusions honoured); distinct, ordinal by id.
		/// </summary>
		internal static IReadOnlyList<Card> WellEquippedPool(Game g, Controller c, IEntity source)
		{
			using (g.GenerateFor(c))
				return RandomCardTask.GetCardList(source, CardType.WEAPON).Where(card => card.Implemented && card.Type == CardType.WEAPON)
					.GroupBy(card => card.Id, StringComparer.Ordinal).Select(x => x.First()).OrderBy(card => card.Id, StringComparer.Ordinal).ToArray();
		}

		/// <summary>ULDA_Brann_HP2 Well Equipped: a random weapon of <see cref="WellEquippedPool"/>, equipped at Durability 1.</summary>
		private static void WellEquipped(Game g, Controller c, IEntity s)
		{
			var pool = WellEquippedPool(g, c, s);
			if (pool.Count == 0) return;
			var weapon = (Weapon)Entity.FromCard(c, pool[g.Random.Next(pool.Count)], new EntityData { [GameTag.DISPLAYED_CREATOR] = s.Id });
			g.OnRandomHappened(true);
			weapon.Durability = 1;
			weapon.Card.Power?.Aura?.Activate(weapon);
			weapon.Card.Power?.Trigger?.Activate(weapon);
			c.Hero.AddWeapon(weapon);
		}

		/// <summary>
		/// ULDA_Elise_HP3 Starseeker: after its owner casts a spell, the turn's spell count goes up; the second spell of a turn
		/// adds a Moonfire (<see cref="StarseekerCard"/>) to the owner's hand, once a turn.
		/// </summary>
		private static void Starseeker(Game g, Controller c, IEntity s, IPlayable cast)
		{
			if (!(cast is Spell) || cast.Controller != c) return;
			if (c[StarseekerTurnTag] != g.Turn) { c[StarseekerTurnTag] = g.Turn; c[StarseekerCountTag] = 0; }
			c[StarseekerCountTag] = c[StarseekerCountTag] + 1;
			if (c[StarseekerCountTag] != StarseekerSpells) return;
			Generic.AddHandPhase(c, Entity.FromCard(c, Cards.FromId(StarseekerCard), new EntityData { [GameTag.DISPLAYED_CREATOR] = s.Id }));
		}

		/// <summary>
		/// ULDA_039 Uldum Treasure Cache: Discover one of three treasures of the run's Tombs of Terror treasure pool (H2,
		/// RunHelpers.TreasurePool). Adaptation: while the run's Tombs pool holds fewer than three treasures (Pack Mule alone until
		/// the Tombs actives, family F12, join it; TombsTreasureCandidates), the three come from the run's whole treasure pool. The pool is the run player's (Player1, the side RunContext describes):
		/// outside a run match, and for a boss that came to hold the power, it discovers nothing (the whole pool holds SoloStone's
		/// own treasures, which the boss's generation does not reject, so the side is checked here).
		/// </summary>
		private static void UldumTreasureCacheDiscover(Game g, Controller c, IEntity s, TaskStack stack)
		{
			if (c != g.Player1) return;
			var pool = TombsTreasureCandidates(c);
			if (pool.Count == 0) return;
			var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
			ctx.Discover(ctx.SampleDistinct(pool, 3), ChoiceAction.HAND);
		}

		/// <summary>A run power with extra shape (on top of its cost).</summary>
		private static void Shaped(RunContent content, Action<Card> extra)
		{
			Action<Card> shape = content.Shape;
			content.Shape = card =>
			{
				shape?.Invoke(card);
				extra(card);
			};
		}

		/// <summary>
		/// A passive hero power keyed and shelled by its own client id at its printed cost: at game start it attaches
		/// <paramref name="effectKey"/> to its owner (as <see cref="Passive"/>, which prints every passive at cost 0).
		/// </summary>
		private static void PassivePower(string id, string name, string text, int cost, string effectKey) =>
			Add(new RunContent
			{
				Key = id, Kind = RunContentKind.PassivePower, ShellId = id, Name = name, Text = text, EffectKey = effectKey, Cost = cost,
				Script = () => new CardDef(new Power
				{
					PowerTask = new CustomTask((game, controller, source, target, stack) =>
						DungeonRunSetup.AttachEffect(controller, (IPlayable)source, effectKey))
				}),
				Shape = card => card.SetPrintedCost(cost),
			});

		private static void TombsExplorers()
		{
			// ---------------- tokens, choices and enchantments
			// "Your next spell this turn": the Celestial Power rule (BOT_531e): a controller Spell Damage aura removed after the
			// next spell is cast or at the end of the turn.
			Effect("fx_ULDA_Reno_HP2e", "ULDA_Reno_HP2e", "Relic Power", "You have <b>Spell Damage +2</b> for your next spell this turn.",
				() => new Power
				{
					Aura = new Aura(AuraType.CONTROLLER, new SabberStoneCore.Enchants.Effect(GameTag.SPELLPOWER, EffectOperator.ADD, 2))
					{
						RemoveTrigger = (TriggerType.TURN_END, null),
					},
					Trigger = new Trigger(TriggerType.AFTER_CAST) { TriggerSource = TriggerSource.FRIENDLY, SingleTask = RemoveEnchantmentTask.Task },
				}, oneTurn: true);
			Active(AmalgamExplorer, "Amalgam Explorer", "[x]<i>This is an Elemental, Mech,\nDemon, Murloc, Dragon,\nBeast, Pirate and Totem.</i>", 1,
				() => new CardDef(), card => card.SetPrintedStats(2, 1));
			Spell(DefensivePosture, DefensivePosture, "Defensive Posture", "+2 Armor.", 0, null, () => new ArmorTask(2));
			Spell(OffensivePosture, OffensivePosture, "Offensive Posture", "+2 Attack.", 0, null,
				() => new AddEnchantmentTask(OffensivePostureEnchantment, EntityType.HERO));
			Effect("fx_ULDA_Elise_HP3", "ULDA_Elise_HPe", "Elise Hero Power Watcher", null,
				() => new Power { Trigger = Trig(TriggerType.AFTER_CAST, new CustomTask((g, c, s, t, stack) => Starseeker(g, c, s, t))) });
			Effect("fx_ULDA_Brann_HP3", DinoTrackingEffect, "Brann Hero Power Watcher", null,
				() => new Power
				{
					PowerTask = StateTaskList.Suspendable("run/ULDA_Brann_HP3/v1",
						new DiscoverThenContinueTask(DiscoverEntitySource.Deck, DiscoverEntityAction.Draw, RejectedOfferAction.Keep)),
				});

			// ---------------- Reno Jackson (MAGE_ROGUE)
			Power("ULDA_Reno_HP1", "ULDA_Reno_HP1", "Amateur Mage", "<b>Hero Power</b>\nDeal $1 damage.\n<b>Combo:</b> $2 instead.", 2, AnyTarget,
				() => ComplexTask.Create(
					new ConditionTask(EntityType.SOURCE, new SelfCondition(p => p.Controller.IsComboActive)),
					new FlagTask(true, new DamageTask(2, EntityType.TARGET)),
					new FlagTask(false, new DamageTask(1, EntityType.TARGET))));
			Power("ULDA_Reno_HP2", "ULDA_Reno_HP2", "\"Relicologist\"", "[x]<b>Hero Power</b>\n Your next spell this turn\nhas <b>Spell Damage +2</b>.", 0, null,
				() => new AddEnchantmentTask("ULDA_Reno_HP2e", EntityType.CONTROLLER));
			Power("ULDA_Reno_HP3", "ULDA_Reno_HP3", "Arcane Craftiness", "[x]<b>Hero Power</b>\nFire two missiles that deal\n$1 damage each. If any\nminion dies, repeat this.", 2, null,
				() => new CustomTask((g, c, s, t, stack) => ArcaneCraftiness(g, c, s)));

			// ---------------- Sir Finley (PALADIN_SHAMAN)
			Power("ULDA_Finley_HP1", "ULDA_Finley_HP1", "New \"Recruits\"", "<b>Hero Power</b>\nSummon a 2/1 Amalgam Explorer.", 2, Summon,
				() => new SummonTask(AmalgamExplorer, SummonSide.DEFAULT), AmalgamExplorer);
			// The client's power card has no OVERLOAD tag (its text names it); the power applies its own Overload (1) through the
			// engine's Overload block, as DALA_Shaman_HP2 Refresh does, so Overload triggers see it.
			Shaped(Power("ULDA_Finley_HP2", "ULDA_Finley_HP2", "Bubble Blower",
				"[x]<b>Hero Power</b>\n<b>Discover</b> a Shaman or\n Paladin minion with\n  <b>Battlecry</b>. <b>Overload:</b> (1).", 1, null,
				() => ComplexTask.Create(
					new CustomTask((g, c, s, t, stack) =>
					{
						var pool = BubbleBlowerPool(g, c, s);
						if (pool.Count == 0) return;
						var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
						ctx.Discover(ctx.SampleDistinct(pool, 3), ChoiceAction.HAND);
					}),
					new CustomTask((g, c, s, t, stack) => Generic.OverloadBlock(c, (IPlayable)s, g.History)))),
				card =>
				{
					card.Tags[GameTag.OVERLOAD] = 1;
					card.HasOverload = true;
					card.Overload = 1;
				});
			Power("ULDA_Finley_HP3", "ULDA_Finley_HP3", "Power Up!", "[x]<b>Hero Power</b>\nGive a minion\n<b>Divine Shield</b> & <b>Windfury</b>.", 2, AnyMinion,
				() => ComplexTask.Create(ComplexTask.DivineShield(EntityType.TARGET), ComplexTask.WindFury(EntityType.TARGET)));

			// ---------------- Elise Starseeker (DRUID_PRIEST)
			// A Choose One power: the engine lists both choices as hero power options (Controller.Options) and resolves the chosen
			// one's card (Defensive Posture or Offensive Posture); the power itself has no effect of its own.
			Power("ULDA_Elise_HP1", "ULDA_Elise_HP1", "Elise's Might", "[x]<b>Hero Power</b>\n<b>Choose One -</b> +2 Attack\nthis turn; or Gain 2 Armor.", 2, null,
				() => null);
			Power("ULDA_Elise_HP2", "ULDA_Elise_HP2", "Druidic Teaching", "[x]<b>Hero Power</b>\nRestore #2 Health, then\ndraw a card if the target\nis at full Health.", 2, AnyTarget,
				() => ComplexTask.Create(
					new HealTask(2, EntityType.TARGET),
					new ConditionTask(EntityType.TARGET, SelfCondition.IsUndamaged),
					new FlagTask(true, new DrawTask())));
			PassivePower("ULDA_Elise_HP3", "Starseeker", "[x]<b>Passive Hero Power</b>\nAfter you cast 2 spells\nin a turn, add a 'Moonfire'\nto your hand.", 0,
				"fx_ULDA_Elise_HP3");

			// ---------------- Brann Bronzebeard (HUNTER_WARRIOR)
			Power("ULDA_Brann_HP1", "ULDA_Brann_HP1", "Spread Shot", "[x]<b>Hero Power</b>\nDeal $1 damage,\nthen deal $1 damage to\nthe enemy hero.", 2, AnyTarget,
				() => ComplexTask.Create(new DamageTask(1, EntityType.TARGET), new DamageTask(1, EntityType.OP_HERO)));
			Power("ULDA_Brann_HP2", "ULDA_Brann_HP2", "Well Equipped", "[x]<b>Hero Power</b>\nEquip a random\nweapon and set its\nDurability to 1.", 2, null,
				() => new CustomTask((g, c, s, t, stack) => WellEquipped(g, c, s)));
			PassivePower("ULDA_Brann_HP3", "Dino Tracking", "[x]<b>Passive Hero Power</b>\nAt the start of your turn,\n<b>Discover</b> which card\nyou draw.", 2,
				"fx_ULDA_Brann_HP3");

			// ---------------- Jr. and Sr. Tomb Diver's power
			Power(UldumTreasureCache, UldumTreasureCache, "Uldum Treasure Cache", "<b>Hero Power</b>\n<b>Discover</b> a Treasure.", 2, null,
				() => new CustomTask((g, c, s, t, stack) => UldumTreasureCacheDiscover(g, c, s, stack)));
		}
	}
}
