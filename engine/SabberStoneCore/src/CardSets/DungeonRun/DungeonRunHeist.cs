using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Actions;
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
	public static partial class DungeonRunContent
	{
		// ---------------------------------------------------------------- HeistHeroes
		// The Dalaran Heist villains' special powers (phase 6, family F6): two per class, DALA_<Class>_HP1 and _HP2, each beside
		// the class's basic power on the villain's hero row (run-v1.json heroRows; the portraits are NamedHeroes). Every power,
		// token and enchantment is keyed and shelled by its own client id, so the match shows the real card; every name and text
		// is the client's own, word for word (facts/violet-hold-2026-09.json). The powers are the run player's only
		// (DungeonRunAdmission); no boss deck lists one. Every random choice uses the game RNG over the scripted cards of the
		// generation pool for the receiving side (run exclusions honoured); every piece of state is an entity tag or
		// enchantment, so clones and AI simulations replay it. The DALA_800-808 "Random Deck" cards are run-layer loadouts
		// (pool-v1 generator decks built by DungeonRun.GenerateStarter), never match cards, so they have no entry here.

		/// <summary>DALA_Hunter_HP2 Pet Training's card: the Shifting Chameleon, a 1/1 that shifts each turn while in hand.</summary>
		public const string ShiftingChameleon = "DALA_Hunter_HP2t";
		/// <summary>The Cost the Shifting Chameleon (and the minion it became) transforms into each turn in hand.</summary>
		public const int ChameleonCost = 1;
		/// <summary>DALA_Warrior_HP2 Undermine's card: the Improvised Explosive, shuffled into the opponent's deck.</summary>
		public const string ImprovisedExplosive = "DALA_Warrior_HP2t";
		/// <summary>How many Improvised Explosives Undermine shuffles.</summary>
		public const int UndermineExplosives = 2;
		/// <summary>DALA_Warlock_HP2 The Pact's minion: Devious Imp, a 2/2 Demon.</summary>
		public const string DeviousImp = "DALA_Warlock_HP2t";
		/// <summary>DALA_Paladin_HP1 Backup's card and how many it adds.</summary>
		public const string SilverHandRecruit = "CS2_101t";
		public const int BackupRecruits = 3;

		/// <summary>The 18 Heist special powers, by class, HP1 then HP2 (the client's ADVENTURE_HERO_POWER order).</summary>
		public static readonly IReadOnlyList<string> HeistPowers = new[]
		{
			"DALA_Mage_HP1", "DALA_Mage_HP2", "DALA_Hunter_HP1", "DALA_Hunter_HP2", "DALA_Shaman_HP1", "DALA_Shaman_HP2",
			"DALA_Priest_HP1", "DALA_Priest_HP2", "DALA_Rogue_HP1", "DALA_Rogue_HP2", "DALA_Warrior_HP1", "DALA_Warrior_HP2",
			"DALA_Druid_HP1", "DALA_Druid_HP2", "DALA_Paladin_HP1", "DALA_Paladin_HP2", "DALA_Warlock_HP1", "DALA_Warlock_HP2",
		};

		/// <summary>
		/// The scripted minions of the generation pool <paramref name="receiver"/> may be given (run exclusions honoured), as
		/// Evolve (OG_027) and Shifter Zerus (OG_123) read it; distinct, ordinal by id.
		/// </summary>
		private static List<Card> ScriptedMinions(Game g, Controller receiver, IEntity source)
		{
			using (g.GenerateFor(receiver))
				return RandomCardTask.GetCardList(source, CardType.MINION).Where(card => card.Implemented && card.Type == CardType.MINION)
					.GroupBy(card => card.Id, StringComparer.Ordinal).Select(x => x.First()).OrderBy(card => card.Id, StringComparer.Ordinal).ToList();
		}

		/// <summary>
		/// DALA_Shaman_HP1 Evolution: the friendly minion becomes a random scripted minion of the generation pool that costs (1)
		/// more than its card (the Evolve rule, OG_027); a minion with no such minion stays.
		/// </summary>
		private static void Evolution(Game g, Controller c, IEntity s, IPlayable t)
		{
			if (!(t is Minion minion) || minion.Zone?.Type != Zone.PLAY || minion.ToBeDestroyed) return;
			int cost = minion.Card.Cost + 1;
			var options = ScriptedMinions(g, minion.Controller, s).Where(card => card.Cost == cost).ToList();
			if (options.Count == 0) return;
			Generic.ChangeEntityBlock(c, minion, options[g.Random.Next(options.Count)], true);
			g.OnRandomHappened(true);
		}

		/// <summary>
		/// The Shifting Chameleon's turn in hand: the card (<paramref name="type"/>: the Chameleon itself, or the card its
		/// Shifting enchantment is on) becomes a random scripted 1-Cost minion of the generation pool (the Shifter Zerus rule,
		/// OG_123, at Cost 1), then the Shifting enchantment (DALA_Hunter_HP2e) goes on the new card so it shifts again next turn.
		/// </summary>
		private static ISimpleTask Shift(EntityType type) => new CustomTask((g, c, s, t, stack) =>
		{
			foreach (IPlayable card in IncludeTask.GetEntities(type, in c, s, t, stack?.Playables).ToArray())
			{
				if (card.Zone?.Type != Zone.HAND) continue;
				var options = ScriptedMinions(g, card.Controller, s).Where(x => x.Cost == ChameleonCost).ToList();
				if (options.Count == 0) continue;
				Generic.ChangeEntityBlock(card.Controller, card, options[g.Random.Next(options.Count)], true);
				Generic.AddEnchantmentBlock(g, Cards.FromId("DALA_Hunter_HP2e"), card, card);
				g.OnRandomHappened(true);
			}
		});

		/// <summary>
		/// DALA_Rogue_HP1 Yoink!: a random scripted card of the generation pool from another class than the owner's hero (no
		/// neutral card, no card of the owner's class), into the owner's hand.
		/// </summary>
		private static void Yoink(Game g, Controller c, IEntity s)
		{
			List<Card> pool;
			using (g.GenerateFor(c))
				pool = RandomCardTask.GetCardList(s).Where(card => card.Implemented && card.Class != CardClass.NEUTRAL && card.Class != CardClass.INVALID
						&& card.Class != c.HeroClass && card.Type != CardType.HERO && card.Type != CardType.ENCHANTMENT && card.Type != CardType.HERO_POWER)
					.GroupBy(card => card.Id, StringComparer.Ordinal).Select(x => x.First()).OrderBy(card => card.Id, StringComparer.Ordinal).ToList();
			if (pool.Count == 0) return;
			var pick = pool[g.Random.Next(pool.Count)];
			Generic.AddHandPhase(c, Entity.FromCard(c, pick, new EntityData { [GameTag.DISPLAYED_CREATOR] = s.Id }));
			g.OnRandomHappened(true);
		}

		private static void HeistHeroes()
		{
			// ---------------- tokens and enchantments
			Buff("fx_DALA_Mage_HP1e", "DALA_Mage_HP1e", "Witty", "Costs (2) less.", Effects.ReduceCost(2));
			Effect("fx_DALA_Hunter_HP1e", "DALA_Hunter_HP1e", "Opportunist", "+2 Attack this turn.",
				() => new Power { Enchant = new Enchant(Effects.Attack_N(2)) { IsOneTurnEffect = true } }, oneTurn: true);
			Effect("fx_DALA_Hunter_HP2e", "DALA_Hunter_HP2e", "Shifting", "Transforming into a random 1-cost minion.",
				() => new Power
				{
					Enchant = new Enchant(GameTag.SHIFTING_MINION, EffectOperator.SET, 1) { RemoveWhenPlayed = true },
					Trigger = new Trigger(TriggerType.TURN_START) { SingleTask = Shift(EntityType.TARGET) },
				});
			Active(ShiftingChameleon, "Shifting Chameleon", "[x]Each turn this is in your\nhand, transform into a\nrandom 1-cost minion.", 1,
				() => new CardDef(new Power
				{
					Trigger = new Trigger(TriggerType.TURN_START) { TriggerActivation = TriggerActivation.HAND, SingleTask = Shift(EntityType.SOURCE) },
				}),
				card => card.SetPrintedStats(1, 1));
			Effect("fx_DALA_Priest_HP1e", "DALA_Priest_HP1e", "Distorted", "Attack and Health have been swapped by Distort.",
				() => new Power { Enchant = SabberStoneCore.Enchants.Enchants.SetAttackHealthScriptTag });
			// "Until your next turn": the enchantment is the power owner's (Enchantment.GetInstance takes the creator's
			// controller), so its turn-start trigger fires at the start of the owner's next turn, whichever side the minion is on.
			Effect("fx_DALA_Priest_HP2e", "DALA_Priest_HP2e", "Soothed", "-2 Attack until your next turn.",
				() => new Power
				{
					Enchant = new Enchant(Effects.Attack_N(-2)),
					Trigger = new Trigger(TriggerType.TURN_START) { SingleTask = RemoveEnchantmentTask.Task, RemoveAfterTriggered = true },
				});
			Buff("fx_DALA_Warrior_HP1e", "DALA_Warrior_HP1e", "Invigorated", "+2 Attack.", Effects.Attack_N(2));
			// Casts When Drawn (TOPDECK): whoever draws it takes the 3 damage, and the next card is drawn in its place (the
			// engine's Casts When Drawn rule, as Ancient Curse LOE_110t); played from a hand it only deals the damage.
			Active(ImprovisedExplosive, "Improvised Explosive", "<b>Casts When Drawn</b>\nTake 3 damage.", 2,
				() => new CardDef(new Power
				{
					PowerTask = new DamageTask(3, EntityType.HERO),
					TopdeckTask = ComplexTask.Create(new DamageTask(3, EntityType.HERO), new DrawTask()),
				}));
			Buff("fx_DALA_Druid_HP2e", "DALA_Druid_HP2e", "Barky", "+1/+1.", Effects.AttackHealth_N(1));
			Active(DeviousImp, "Devious Imp", null, 2, () => new CardDef(), card => card.SetPrintedStats(2, 2));

			// ---------------- the powers (name, text and cost as the client prints them). A power's tokens are the minions it
			// summons (The Pact's Imp); the cards Pet Training and Backup put in hand are not summoned, so they list none.
			Power("DALA_Mage_HP1", "DALA_Mage_HP1", "Burning Wit", "[x]<b>Hero Power</b>\nReduce the cost of a\nrandom card in your\nhand by (2).", 1, null,
				() => ComplexTask.Create(new IncludeTask(EntityType.HAND), new RandomTask(1, EntityType.STACK),
					new AddEnchantmentTask("DALA_Mage_HP1e", EntityType.STACK)));
			Power("DALA_Mage_HP2", "DALA_Mage_HP2", "Frostburn", "<b>Hero Power</b>\n<b>Freeze</b> a character.\nIf it's already <b>Frozen</b>, deal $2 damage.", 2,
				AnyTarget, () => ComplexTask.Create(
					new ConditionTask(EntityType.TARGET, SelfCondition.IsFrozen),
					new FlagTask(true, new DamageTask(2, EntityType.TARGET)),
					ComplexTask.Freeze(EntityType.TARGET)));
			Power("DALA_Hunter_HP1", "DALA_Hunter_HP1", "Opportunist", "<b>Hero Power</b>\nGive a minion +2_Attack this turn.", 2,
				AnyMinion, () => new AddEnchantmentTask("DALA_Hunter_HP1e", EntityType.TARGET));
			Power("DALA_Hunter_HP2", "DALA_Hunter_HP2", "Pet Training", "<b>Hero Power</b>\nAdd a 1/1 Shifting Chameleon to your hand.", 1, null,
				() => new AddCardTo(ShiftingChameleon, EntityType.HAND));
			Power("DALA_Shaman_HP1", "DALA_Shaman_HP1", "Evolution", "<b>Hero Power</b>\nTransform a friendly minion into one that costs (1) more.", 2,
				FriendlyMinion, () => new CustomTask((g, c, s, t, stack) => Evolution(g, c, s, t)));
			// The engine applies Overload when a card is played, not when a power is used: the power applies its own (the
			// client's OVERLOAD 1 on the power card) through the engine's Overload block, so Overload triggers see it.
			Power("DALA_Shaman_HP2", "DALA_Shaman_HP2", "Refresh", "<b>Hero Power</b>\nDraw a card.\n<b>Overload:</b> (1).", 1, null,
				() => ComplexTask.Create(new DrawTask(),
					new CustomTask((g, c, s, t, stack) => Generic.OverloadBlock(c, (IPlayable)s, g.History))));
			Power("DALA_Priest_HP1", "DALA_Priest_HP1", "Distort", "<b>Hero Power</b>\nSwap a minion's Attack and Health.", 2,
				AnyMinion, () => new SwapAttackHealthTask(EntityType.TARGET, "DALA_Priest_HP1e"));
			Power("DALA_Priest_HP2", "DALA_Priest_HP2", "Soothe", "<b>Hero Power</b>\nGive a minion -2_Attack until your next turn.", 1,
				AnyMinion, () => new AddEnchantmentTask("DALA_Priest_HP2e", EntityType.TARGET));
			Power("DALA_Rogue_HP1", "DALA_Rogue_HP1", "Yoink!", "[x]<b>Hero Power</b>\nAdd a random\ncard to your hand\n<i>(from another class).</i>", 2, null,
				() => new CustomTask((g, c, s, t, stack) => Yoink(g, c, s)));
			Power("DALA_Rogue_HP2", "DALA_Rogue_HP2", "Cut-less", "<b>Hero Power</b>\nDeal $2 damage to an undamaged minion.", 2,
				new[] { PlayReq.REQ_TARGET_TO_PLAY, PlayReq.REQ_MINION_TARGET, PlayReq.REQ_UNDAMAGED_TARGET }, () => new DamageTask(2, EntityType.TARGET));
			Power("DALA_Warrior_HP1", "DALA_Warrior_HP1", "Invigorating Brew", "[x]<b>Hero Power</b>\nDeal $1 damage to\na minion and give\nit +2_Attack.", 2,
				AnyMinion, () => ComplexTask.Create(new DamageTask(1, EntityType.TARGET), new AddEnchantmentTask("DALA_Warrior_HP1e", EntityType.TARGET)));
			Power("DALA_Warrior_HP2", "DALA_Warrior_HP2", "Undermine", "[x]<b>Hero Power</b>\nShuffle two Explosives\ninto your opponent's\ndeck.", 2, null,
				() => new AddCardTo(ImprovisedExplosive, EntityType.OP_DECK, UndermineExplosives));
			Power("DALA_Druid_HP1", "DALA_Druid_HP1", "Lifebloom", "<b>Hero Power</b>\nRestore a minion to full Health.", 2,
				AnyMinion, () => new HealFullTask(EntityType.TARGET));
			Power("DALA_Druid_HP2", "DALA_Druid_HP2", "Touch of Bark", "<b>Hero Power</b>\nGive a minion +1/+1.", 2,
				AnyMinion, () => new AddEnchantmentTask("DALA_Druid_HP2e", EntityType.TARGET));
			Power("DALA_Paladin_HP1", "DALA_Paladin_HP1", "Backup", "<b>Hero Power</b>\nAdd three 1/1 Silver Hand Recruits to your hand.", 2, null,
				() => new AddCardTo(SilverHandRecruit, EntityType.HAND, BackupRecruits));
			Power("DALA_Paladin_HP2", "DALA_Paladin_HP2", "Boon of Light", "<b>Hero Power</b>\nGive a friendly minion <b>Divine Shield</b>.", 2,
				FriendlyMinion, () => ComplexTask.DivineShield(EntityType.TARGET));
			Power("DALA_Warlock_HP1", "DALA_Warlock_HP1", "Pain Split", "<b>Hero Power</b>\nTake $2 damage.\nDeal $2 damage.", 2,
				AnyTarget, () => ComplexTask.Create(new DamageTask(2, EntityType.HERO), new DamageTask(2, EntityType.TARGET)));
			Power("DALA_Warlock_HP2", "DALA_Warlock_HP2", "The Pact", "<b>Hero Power</b>\nTake $2 damage.\nSummon a 2/2 Imp.", 2,
				Summon, () => ComplexTask.Create(new DamageTask(2, EntityType.HERO), new SummonTask(DeviousImp, SummonSide.DEFAULT)), DeviousImp);
		}
	}
}
