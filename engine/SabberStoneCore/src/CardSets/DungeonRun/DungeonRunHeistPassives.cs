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
	public static partial class DungeonRunContent
	{
		// ---------------------------------------------------------------- HeistPassives
		// The Dalaran Heist passive treasures (phase 6, family F8), keyed and shelled by their own client ids, so the treasure
		// picker shows the real card; every name and text is the client's own, word for word (facts/violet-hold-2026-09.json).
		// Each aura or trigger is an Effect attached to the run player at match start (DungeonRunSetup.ApplyAfterStart), on the
		// client's own "<id>e" player enchantment where the client has one, else on a borrowed, unscripted Battlegrounds or
		// Tavern Brawl player-enchantment shell with the treasure's own name and text. The four "At the start of the game"
		// treasures (737, 739, 743, 745) are Start of Game effects declared on their effect shell (StartOfGameRules): the
		// attached enchantment is a pending source that StartOfGameRules.Resolve runs after both mulligans. The rule changes
		// (735 half cost and two plays a turn, 747 no Fatigue) are engine legality and cost checks (RunPlayerRules), so the
		// options list and the bot's own search see them; 748 is the engine's spells-cost-Health rule. DALA_736 Stargazing is
		// GILA_513 Cult of the Wolf's rule (H6 HeroPowerTwice). The treasures are the run player's only (DungeonRunAdmission);
		// no boss deck lists one. Every random choice uses the game RNG; every piece of state is an entity tag or enchantment,
		// so clones and AI simulations replay it. DALA_740 Wondrous Wisdomball is rejected (no content): see
		// <see cref="WisdomballRejection"/>.

		/// <summary>The 16 Heist passive treasure ids of family F8, in id order (DALA_740 is rejected and has no content).</summary>
		public static readonly IReadOnlyList<string> HeistPassiveIds = new[]
		{
			"DALA_728", "DALA_731", "DALA_733", "DALA_735", "DALA_736", "DALA_737", "DALA_738", "DALA_739", "DALA_740", "DALA_741",
			"DALA_743", "DALA_744", "DALA_745", "DALA_746", "DALA_747", "DALA_748",
		};

		/// <summary>
		/// Why DALA_740 Wondrous Wisdomball ships no content. Its client text ("Occasionally gives helpful advice.") is not its
		/// whole effect: the client also carries hidden helper cards (DALA_740ts-ts5: copies of a card, five random cards on an
		/// empty hand, three random minions, a random Legendary transform, three random Taunt minions; DALA_740e2 Ice Block,
		/// DALA_740e3 an extra spellcast) whose triggers and chances the client data does not state. A no-op passive would offer a
		/// card that does nothing and misstate the original; reproducing it would invent the trigger rules.
		/// </summary>
		public const string WisdomballRejection = "data-gap: hidden random effects (DALA_740ts-ts5, DALA_740e2, DALA_740e3) whose triggers and chances the client data does not state; a no-op passive would waste a draft and misstate the card";

		/// <summary>DALA_739 Book of Wonders: the card it shuffles (Scroll of Wonder, Casts When Drawn) and how many.</summary>
		public const string ScrollOfWonder = "LOOT_106t";
		public const int BookOfWondersScrolls = 10;
		/// <summary>DALA_745 The Hand of Rafaam: the card the opponent is given (Cursed!) and how many.</summary>
		public const string RafaamCurse = "LOE_007t";
		public const int RafaamCurses = 2;
		/// <summary>DALA_743 Dr. Boom's Remote: how many Boom Bots (<see cref="BoomBot"/>) it summons.</summary>
		public const int RemoteBoomBots = 3;
		/// <summary>DALA_735 Robes of Gaudiness: the cards its owner may play each turn.</summary>
		public const int GaudyPlaysPerTurn = 2;
		/// <summary>DALA_741 Togwaggle's Dice: each card's new Cost is drawn from 0 to this, inclusive.</summary>
		public const int TogwaggleMaxCost = 10;
		/// <summary>DALA_747 Elixir of Vim: the extra cards drawn at the start of each of its owner's turns.</summary>
		public const int VimExtraDraws = 2;
		/// <summary>DALA_746 Elixir of Vigor: the copies shuffled after each minion played, and their Cost.</summary>
		public const int VigorCopies = 2;
		/// <summary>DALA_746's cost enchantment: a borrowed, unscripted client enchantment ("Cost = 1", "Costs (1).").</summary>
		public const string VigorCostShell = "GBL_001e";

		/// <summary>
		/// DALA_738 A Prince's Ring: the Death Knight hero powers a run may get (the Knights of the Frozen Throne Death Knight
		/// heroes' powers), each scripted, clickable and admitted for the run player (DungeonRunValidator checks each one).
		/// Left out: Build-A-Beast (ICC_828p, a crafted Zombeast that is a run-time combined card the client cannot be sent),
		/// Death's Shadow (ICC_827p, a passive power, which a replacement after game start never activates) and Plague Lord
		/// (ICC_832p, a Choose One power the engine does not script).
		/// </summary>
		public const string PrincesRingId = "DALA_738";
		/// <summary>The powers <see cref="PrincesRingId"/> may swap in.</summary>
		public static readonly IReadOnlyList<string> PrincesRingPowers = new[]
		{
			"ICC_481p", "ICC_829p", "ICC_830p", "ICC_831p", "ICC_833h", "ICC_834h",
		};

		/// <summary>DALA_737 Resourcefulness: the weapons its Start of Game effect may equip (scripted, generation pool, ordinal).</summary>
		public static IReadOnlyList<Card> ResourcefulnessWeapons(Controller c, IEntity source)
		{
			using (c.Game.GenerateFor(c))
				return RandomCardTask.GetCardList(source, CardType.WEAPON).Where(card => card.Implemented && card.Type == CardType.WEAPON)
					.GroupBy(card => card.Id, StringComparer.Ordinal).Select(x => x.First()).OrderBy(card => card.Id, StringComparer.Ordinal).ToArray();
		}

		/// <summary>
		/// DALA_737's Start of Game effect: a random weapon of <see cref="ResourcefulnessWeapons"/>, equipped, with +1/+1. Built on
		/// first use: a task that names a card reads the card database, which must not load while this content is built.
		/// </summary>
		private static ISimpleTask ResourcefulnessTask() => ComplexTask.Create(
			new CustomTask((g, c, s, t, stack) =>
			{
				var pool = ResourcefulnessWeapons(c, s);
				stack.Playables = pool.Count == 0 ? Array.Empty<IPlayable>() : new[] { Entity.FromCard(c, pool[g.Random.Next(pool.Count)]) };
				if (pool.Count > 0) g.OnRandomHappened(true);
			}),
			new WeaponTask(),
			new AddEnchantmentTask("DALA_737e", EntityType.WEAPON));

		/// <summary>DALA_738's start: the run hero's power becomes a random one of <see cref="PrincesRingPowers"/> (game RNG).</summary>
		private static void PrincesRing(Controller c)
		{
			string id = PrincesRingPowers[c.Game.Random.Next(PrincesRingPowers.Count)];
			new ReplaceHeroPower(Cards.FromId(id)).Process(c.Game, c, c.Hero, null);
			c.Game.OnRandomHappened(true);
		}

		/// <summary>
		/// DALA_741's end of turn: every card in its owner's hand gets a random Cost from 0 to <see cref="TogwaggleMaxCost"/> (a
		/// set Cost, as a card's printed Cost changes; later discounts and auras still apply), marked with Roll the Dice (DALA_741e2).
		/// </summary>
		private static void RollTheDice(Game g, Controller c, IEntity s)
		{
			Card dice = Cards.FromId("DALA_741e2");
			foreach (IPlayable card in c.HandZone.ToArray())
			{
				card.Cost = g.Random.Next(TogwaggleMaxCost + 1);
				if (card.AppliedEnchantments?.Any(e => e.Card == dice) != true) Generic.AddEnchantmentBlock(g, dice, (IPlayable)s, card);
			}
			if (c.HandZone.Count > 0) g.OnRandomHappened(true);
		}

		/// <summary>
		/// An effect shell whose card is also a Start of Game source: the attached enchantment runs the task <paramref name="task"/>
		/// builds (once, on first use: a task that names a card reads the card database, which must not load while this content is
		/// built).
		/// </summary>
		private static void StartOfGameEffect(string key, string shell, string name, string text, Func<ISimpleTask> task)
		{
			Effect(key, shell, name, text, () => new Power());
			var built = new Lazy<ISimpleTask>(task);
			WithShape(key, card =>
			{
				if (!StartOfGameRules.HasEffect(card))
					StartOfGameRules.Declare(card, source => source.Game.TaskQueue.Enqueue(built.Value, source.Controller, source, null));
			});
		}

		/// <summary>Adds <paramref name="more"/> to a content entry's card shape (run once per card load).</summary>
		private static void WithShape(string key, Action<Card> more)
		{
			RunContent content = Get(key);
			Action<Card> shape = content.Shape;
			content.Shape = card => { shape?.Invoke(card); more(card); };
		}

		private static void HeistPassives()
		{
			// ---------------- effects (the client's names; a null text keeps the client's own, which has none)
			Effect("fx_DALA_728", "DALA_728e", "Recycling Player Enchantment", null,
				() => new Power { Trigger = Trig(TriggerType.DEATH, new ArmorTask(2), TriggerSource.FRIENDLY, SelfCondition.IsMinion) });
			Effect("fx_DALA_731", "DALA_731e", "Rocket Backpacks Player Enchantment", null,
				() => new Power { Aura = new Aura(AuraType.BOARD, new SabberStoneCore.Enchants.Effect(GameTag.RUSH, EffectOperator.SET, 1)) });
			// The left-most card: a restless hand aura, re-checked on every aura update, so the discount moves when that card leaves.
			Effect("fx_DALA_733", "DALA_733e", "Emerald Goggles Player Enchantment", null,
				() => new Power
				{
					Aura = new Aura(AuraType.HAND, Effects.ReduceCost(2))
					{
						Restless = true,
						Condition = new SelfCondition(p => p.Zone == p.Controller.HandZone && p.ZonePosition == 0),
					}
				});
			// Half cost and the play limit are engine checks keyed by this enchantment (RunPlayerRules): the Cost getter halves
			// (rounded up) its owner's cards in hand, and the play check refuses a third card in a turn.
			Effect("fx_DALA_735", "DALA_735e", "Robes of Gaudiness Player Enchantment", "Your cards cost half, but you can only play two cards each turn.",
				() => new Power());
			WithShape("fx_DALA_735", card => { RunPlayerRules.DeclareHalfCost(card); RunPlayerRules.DeclarePlayLimit(card, GaudyPlaysPerTurn); });
			Effect("fx_DALA_736", "DALA_736e", "Stargazing Player Enchant", null, () => HeroPowerTwice(1));
			StartOfGameEffect("fx_DALA_737", "TB_BaconShop_1P_PlayerE", "Resourcefulness",
				"At the start of the game, equip a random weapon. Give it +1/+1.", ResourcefulnessTask);
			Buff("fx_DALA_737e", "DALA_737e", "E.V.I.L. Upgrade", "+1 Attack and +1 Durability.",
				Effects.Attack_N(1), new SabberStoneCore.Enchants.Effect(GameTag.DURABILITY, EffectOperator.ADD, 1));
			StartOfGameEffect("fx_DALA_739", "TB_BaconShop_8P_PlayerE", "Book of Wonders",
				"At the start of the game, shuffle 10 'Scrolls of Wonder' into your deck.", () => new AddCardTo(ScrollOfWonder, EntityType.DECK, BookOfWondersScrolls));
			Effect("fx_DALA_741", "DALA_741e", "Togwaggle's Dice Player Enchantment", "At the end of your turn, randomize the cost of all cards in your hand.",
				() => new Power { Trigger = Trig(TriggerType.TURN_END, new CustomTask((g, c, s, t, stack) => RollTheDice(g, c, s))) });
			Effect("fx_DALA_741e2", "DALA_741e2", "Roll the Dice", "Cost manipulated by Togwaggle's Dice.", () => new Power());
			StartOfGameEffect("fx_DALA_743", "TB_BaconShop_3ofKindChecke", "Dr. Boom's Remote",
				"At the start of the game, summon three 1/1 Boom Bots.", () => new SummonTask(BoomBot, RemoteBoomBots));
			Effect("fx_DALA_744", "DALA_744e", "Hagatha's Embrace Player Enchantment", "At the start of your turn, give a random minion in your hand +1/+1.",
				() => new Power
				{
					Trigger = Trig(TriggerType.TURN_START, ComplexTask.Create(
						new IncludeTask(EntityType.HAND), new FilterStackTask(SelfCondition.IsMinion), new RandomTask(1, EntityType.STACK),
						new AddEnchantmentTask("DALA_744e2", EntityType.STACK)))
				});
			Buff("fx_DALA_744e2", "DALA_744e2", "Hagatha's Embrace", "+1/+1.", Effects.AttackHealth_N(1));
			StartOfGameEffect("fx_DALA_745", "TB_DeckRecipe_MyDeckID", "The Hand of Rafaam",
				"At the start of the game, give your opponent two 'Cursed!' cards.", () => new AddCardTo(RafaamCurse, EntityType.OP_HAND, RafaamCurses));
			// "Copies of it": the engine's copy rule, shuffled into the owner's deck, each then set to cost (1).
			Effect("fx_DALA_746", "DALA_746e", "Elixir of Vigor Player Enchantment", "After you play a minion, shuffle two copies of it into your deck. They cost (1).",
				() => new Power
				{
					Trigger = Trig(TriggerType.AFTER_PLAY_MINION, ComplexTask.Create(
						new CopyTask(EntityType.TARGET, Zone.DECK, VigorCopies, addToStack: true),
						new AddEnchantmentTask(VigorCostShell, EntityType.STACK)))
				});
			Buff("fx_DALA_746_cost", VigorCostShell, "Cost = 1", "Costs (1).", Effects.SetCost(1));
			Effect("fx_DALA_747", "DALA_747e", "Elixir of Vim Player Enchantment", "Draw 2 additional cards each turn.",
				() => new Power { Trigger = Trig(TriggerType.TURN_START, new DrawTask(VimExtraDraws)) });
			WithShape("fx_DALA_747", RunPlayerRules.DeclareFatigueImmune);
			Effect("fx_DALA_748", "DALA_748e", "Elixir of Vile Player Enchantment", "Your spells cost Health instead of Mana.",
				() => new Power { Aura = new Aura(AuraType.CONTROLLER, new SabberStoneCore.Enchants.Effect(GameTag.SPELLS_COST_HEALTH, EffectOperator.SET, 1)) });

			// ---------------- the treasures (name and text as the client prints them)
			Treasure("DALA_728", "Recycling", "<b>Passive</b>\nAfter a friendly minion dies, gain 2 armor.", effect: "fx_DALA_728", ownShell: true);
			Treasure("DALA_731", "Rocket Backpacks", "<b>Passive</b>\nYour minions have <b>Rush</b>.", effect: "fx_DALA_731", ownShell: true);
			Treasure("DALA_733", "Emerald Goggles", "<b>Passive</b>\nThe left-most card in your hand costs (2) less.", effect: "fx_DALA_733", ownShell: true);
			Treasure("DALA_735", "Robes of Gaudiness", "<b>Passive</b>\nYour cards cost half, but you can only play two cards each turn.",
				effect: "fx_DALA_735", ownShell: true);
			Treasure("DALA_736", "Stargazing", "<b>Passive</b>\nYou can use your Hero Power twice each turn.\nIt costs (1) less.", effect: "fx_DALA_736", ownShell: true);
			Treasure("DALA_737", "Resourcefulness", "<b>Passive</b>\nAt the start of the game, equip a random weapon. Give it +1/+1.",
				effect: "fx_DALA_737", ownShell: true);
			Treasure("DALA_738", "A Prince's Ring", "<b>Passive</b>\nReplace your starting Hero Power with a random Death Knight one.",
				onStart: PrincesRing, ownShell: true);
			Treasure("DALA_739", "Book of Wonders", "<b>Passive</b>\nAt the start of the game, shuffle 10 'Scrolls of Wonder' into your deck.",
				effect: "fx_DALA_739", ownShell: true);
			Treasure("DALA_741", "Togwaggle's Dice", "<b>Passive</b>\nAt the end of your turn, randomize the cost of all cards in your hand.",
				effect: "fx_DALA_741", ownShell: true);
			Treasure("DALA_743", "Dr. Boom's Remote", "<b>Passive</b>\nAt the start of the game, summon three 1/1 Boom Bots.", effect: "fx_DALA_743", ownShell: true);
			Treasure("DALA_744", "Hagatha's Embrace", "<b>Passive</b>\nAt the start of your turn, give a random minion in your hand +1/+1.",
				effect: "fx_DALA_744", ownShell: true);
			Treasure("DALA_745", "The Hand of Rafaam", "<b>Passive</b>\nAt the start of the game, give your opponent two 'Cursed!' cards.",
				effect: "fx_DALA_745", ownShell: true);
			Treasure("DALA_746", "Elixir of Vigor", "<b>Passive</b>\nAfter you play a minion, shuffle two copies of it into your deck. They cost (1).",
				effect: "fx_DALA_746", ownShell: true);
			Treasure("DALA_747", "Elixir of Vim", "<b>Passive</b>\nYou draw an additional two cards each turn. You are <b>Immune</b> to <b>Fatigue</b>.",
				effect: "fx_DALA_747", ownShell: true);
			Treasure("DALA_748", "Elixir of Vile", "<b>Passive</b>\nYour spells cost Health instead of Mana.", effect: "fx_DALA_748", ownShell: true);
		}
	}

	/// <summary>
	/// Run passive rules that the engine's own cost, play and draw checks read (phase 6 F8): each is keyed by a player
	/// enchantment card that a treasure attaches, so the options list, the client's view and every clone (the bot's search
	/// included) see the same rule. Declared once when the card loads (the content's card shape); a game with none of these
	/// enchantments takes one empty-set check per read.
	/// </summary>
	public static partial class RunPlayerRules
	{
		private static readonly HashSet<int> HalfCost = new HashSet<int>();
		private static readonly Dictionary<int, int> PlayLimits = new Dictionary<int, int>();
		private static readonly HashSet<int> FatigueImmune = new HashSet<int>();

		internal static void DeclareHalfCost(Card enchantment) => HalfCost.Add(enchantment.AssetId);
		internal static void DeclarePlayLimit(Card enchantment, int perTurn) => PlayLimits[enchantment.AssetId] = perTurn;
		internal static void DeclareFatigueImmune(Card enchantment) => FatigueImmune.Add(enchantment.AssetId);

		private static bool Holds(Controller c, HashSet<int> set)
		{
			var list = c?.AppliedEnchantments;
			if (set.Count == 0 || list == null || list.Count == 0) return false;
			for (int i = 0; i < list.Count; i++) if (set.Contains(list[i].Card.AssetId)) return true;
			return false;
		}

		/// <summary>DALA_735: a card in its owner's hand costs half, rounded up (the treasure's "Costs half").</summary>
		internal static int AdjustHandCost(IPlayable source, int amount)
		{
			if (amount <= 0 || HalfCost.Count == 0 || source is HeroPower) return amount;
			Controller c = source.Controller;
			if (c == null || source.Zone != c.HandZone || !Holds(c, HalfCost)) return amount;
			return (amount + 1) / 2;
		}

		/// <summary>The cards <paramref name="c"/> may still play this turn under a play limit; null when no limit holds.</summary>
		public static int? PlaysLeft(Controller c)
		{
			var list = c?.AppliedEnchantments;
			if (PlayLimits.Count == 0 || list == null || list.Count == 0) return null;
			int? limit = null;
			for (int i = 0; i < list.Count; i++)
				if (PlayLimits.TryGetValue(list[i].Card.AssetId, out int n)) limit = limit == null ? n : Math.Min(limit.Value, n);
			return limit == null ? (int?)null : Math.Max(0, limit.Value - c.NumCardsPlayedThisTurn);
		}

		/// <summary>DALA_735: a card in hand cannot be played once its owner has played its limit this turn.</summary>
		internal static bool BlocksPlay(IPlayable target) =>
			!(target is HeroPower) && PlaysLeft(target.Controller) == 0;

		/// <summary>DALA_747: drawing from an empty deck deals no Fatigue damage to its owner.</summary>
		public static bool IsFatigueImmune(Controller c) => Holds(c, FatigueImmune);
	}
}
