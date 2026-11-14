using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Auras;
using SabberStoneCore.Conditions;
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
	public static partial class DungeonRunContent
	{
		// ---------------------------------------------------------------- TombsPassives
		// The Tombs of Terror passive treasures (phase 6, family F13), keyed and shelled by their own client ids, so the treasure
		// picker shows the real card; every name and text is the client's own, word for word (facts/violet-hold-2026-09.json).
		// Each aura or trigger is an Effect attached to the run player at match start (DungeonRunSetup.ApplyAfterStart) on the
		// client's own "<id>e" player enchantment (ULDA_007e2 for Crook and Flail, the client's only one); the buffs they give are
		// the client's own "<id>e2" enchantments. Two are match-start changes (OnMatchStart, before the mulligan): ULDA_009 sets the
		// Attack of every minion in the run deck and opening hand to its Health (ULDA_009e), ULDA_117 attaches its decaying Spell
		// Damage. Three are engine rules keyed by their player enchantment (RunPlayerRules), so the options list, the client's
		// view and every clone (the bot's search included) see them: ULDA_102's opponent must pass its first two turns (the options
		// list holds only End Turn and Game.Process refuses any other action), ULDA_205 copies a Discovered card, and ULDA_042 and
		// ULDA_103 read the Cost the played card was paid at (TAG_LAST_KNOWN_COST_IN_HAND). ULDA_011 VIP Membership has no match
		// effect: it is a run-layer flag that Bob's tavern reads (DungeonRunTavernNode). The treasures are the run player's only
		// (DungeonRunAdmission); no boss deck lists one. Every random choice uses the game RNG; every piece of state is an entity
		// tag or enchantment, so clones and AI simulations replay it.

		/// <summary>The 15 Tombs passive treasure ids of family F13, in id order.</summary>
		public static readonly IReadOnlyList<string> TombsPassiveIds = new[]
		{
			"ULDA_005", "ULDA_007", "ULDA_009", "ULDA_011", "ULDA_014", "ULDA_042", "ULDA_046", "ULDA_101", "ULDA_102", "ULDA_103",
			"ULDA_116", "ULDA_117", "ULDA_205", "ULDA_208", "ULDA_508",
		};

		/// <summary>ULDA_011 VIP Membership: the run-layer treasure id Bob's tavern reads (no engine effect).</summary>
		public const string VipMembership = "ULDA_011";
		/// <summary>ULDA_102 Disks of Swiftness: the opponent's turns that are passes.</summary>
		public const int SwiftnessPassedTurns = 2;
		/// <summary>ULDA_117 Scroll of Nonsense: the Spell Damage it starts with (it loses 1 at each of its owner's turn ends).</summary>
		public const int NonsenseSpellDamage = 10;
		/// <summary>ULDA_205 Lucky Spade: the copies added and the Cost all of them lose.</summary>
		public const int LuckySpadeCopies = 2, LuckySpadeDiscount = 2;
		/// <summary>ULDA_046 Band of Bees: the highest Cost of a Poisonous minion.</summary>
		public const int BeesMaxCost = 2;

		/// <summary>ULDA_009 Unlocked Potential: every minion in its owner's deck and hand gets Attack equal to its Health (ULDA_009e).</summary>
		private static void UnlockedPotential(Controller c)
		{
			Card buff = Cards.FromId("ULDA_009e");
			foreach (IPlayable p in c.DeckZone.ToArray().Concat(c.HandZone.ToArray()))
				if (p is Minion m) Generic.AddEnchantmentBlock(c.Game, buff, c.Hero, m, m.Health);
		}

		/// <summary>ULDA_117 Scroll of Nonsense: its player enchantment, carrying the Spell Damage left (TAG_SCRIPT_DATA_NUM_1).</summary>
		private static void ScrollOfNonsense(Controller c)
		{
			Card card = Cards.FromId("ULDA_117e");
			Enchantment e = Enchantment.GetInstance(c, c.Hero, c, card, NonsenseSpellDamage);
			card.Power.Trigger?.Activate(e);
			card.Power.Enchant.ActivateTo(c, NonsenseSpellDamage);
		}

		/// <summary>ULDA_117's turn end: one Spell Damage less, down to none.</summary>
		private static void ScrollDecay(Game g, Controller c, IEntity s)
		{
			if (!(s is Enchantment e)) return;
			int left = e[GameTag.TAG_SCRIPT_DATA_NUM_1];
			if (left <= 0) return;
			e[GameTag.TAG_SCRIPT_DATA_NUM_1] = left - 1;
			new SabberStoneCore.Enchants.Effect(GameTag.SPELLPOWER, EffectOperator.SUB, 1).ApplyTo(c);
			if (g.History) g.PowerHistory.Add(PowerHistoryBuilder.TagChange(c.Id, GameTag.SPELLPOWER, c[GameTag.SPELLPOWER]));
		}

		/// <summary>ULDA_116's turn end: a random friendly minion evolves (DALA_Shaman_HP1's rule: a minion that costs (1) more).</summary>
		private static void EverChanging(Game g, Controller c, IEntity s)
		{
			var minions = c.BoardZone.Where(m => !m.ToBeDestroyed && !m.IsDead && !m.Untouchable && !m.IsDormant).ToList();
			if (minions.Count == 0) return;
			Minion pick = minions[g.Random.Next(minions.Count)];
			g.OnRandomHappened(true);
			Evolution(g, c, s, pick);
		}

		/// <summary>The Cost a played card was paid at (the engine's TAG_LAST_KNOWN_COST_IN_HAND), as Darklight Torch and Alchemist's Stone read it.</summary>
		private static bool PaidEven(IPlayable p) => p[GameTag.TAG_LAST_KNOWN_COST_IN_HAND] % 2 == 0;

		private static void TombsPassives()
		{
			// ---------------- effects (the client's names; a null text keeps the client's own, which has none)
			Effect("fx_ULDA_005", "ULDA_005e", "Mummy Magic Player Enchantment", null,
				() => new Power
				{
					// A minion that was already reborn gets Reborn again (HAS_BEEN_REBORN cleared), as Embalming Ritual (ULD_265).
					Trigger = Trig(TriggerType.AFTER_PLAY_MINION, ComplexTask.Create(
						new ConditionTask(EntityType.TARGET, SelfCondition.IsNotDead),
						new FlagTask(true, ComplexTask.Create(
							new SetGameTagTask(GameTag.REBORN, 1, EntityType.TARGET),
							new SetGameTagTask(GameTag.HAS_BEEN_REBORN, 0, EntityType.TARGET)))),
						TriggerSource.FRIENDLY, SelfCondition.IsDeathrattleMinion)
				});
			Effect("fx_ULDA_007", "ULDA_007e2", "Crook and Flail Player Enchantment", null,
				() => new Power { Trigger = Trig(TriggerType.AFTER_CAST, new RecruitTask(1)) });
			Effect("fx_ULDA_009e", "ULDA_009e", "Unlocked Potential", "Has Attack equal to its Health.",
				() => new Power { Enchant = Enchants.Enchants.SetAttackScriptTag });
			Effect("fx_ULDA_014", "ULDA_014e", "Disks of Legend Player Enchantment", null,
				() => new Power
				{
					Trigger = Trig(TriggerType.AFTER_PLAY_MINION, new SummonCopyTask(EntityType.TARGET), TriggerSource.FRIENDLY,
						new SelfCondition(p => p.Card.Rarity == Rarity.LEGENDARY))
				});
			Effect("fx_ULDA_042", "ULDA_042e", "Darklight Torch Player Enchantment", null,
				() => new Power
				{
					Trigger = Trig(TriggerType.AFTER_PLAY_CARD, ComplexTask.Create(
						new SetGameTagTask(GameTag.EXHAUSTED, 0, EntityType.HERO_POWER),
						new AddEnchantmentTask("ULDA_042e2", EntityType.HERO_POWER)), TriggerSource.FRIENDLY, new SelfCondition(PaidEven))
				});
			Effect("fx_ULDA_042e2", "ULDA_042e2", "Darklight Torch", null,
				() => new Power { Enchant = new Enchant(Effects.SetCost(0)) { IsOneTurnEffect = true } }, oneTurn: true);
			Effect("fx_ULDA_046", "ULDA_046e", "Band of Bees Player Enchantment", null,
				() => new Power { Aura = new Aura(AuraType.BOARD, "ULDA_046e2") { Condition = SelfCondition.IsCost(BeesMaxCost, RelaSign.LEQ) } });
			Buff("fx_ULDA_046e2", "ULDA_046e2", "Band of Bees", "<b>Poisonous</b>.", new SabberStoneCore.Enchants.Effect(GameTag.POISONOUS, EffectOperator.SET, 1));
			Effect("fx_ULDA_101", "ULDA_101e", "Band of Scarabs Player Enchantment", null,
				() => new Power { Aura = new Aura(AuraType.OP_BOARD, "ULDA_101e2") });
			Buff("fx_ULDA_101e2", "ULDA_101e2", "Band of Scarabs", "-1 Attack.", Effects.Attack_N(-1));
			// The opponent's turn starts are counted on this enchantment (RunPlayerRules.OpponentTurnStarts); while one of its
			// first two turns runs, RunPlayerRules.MustPass holds for the opponent.
			Effect("fx_ULDA_102", "ULDA_102e", "Platinum Disks Player Enchantment", null,
				() => new Power
				{
					Trigger = new Trigger(TriggerType.TURN_START)
					{
						TriggerSource = TriggerSource.ENEMY, EitherTurn = true,
						SingleTask = new CustomTask((g, c, s, t, stack) => RunPlayerRules.OpponentTurnStarts(g, s as Enchantment)),
					}
				});
			WithShape("fx_ULDA_102", card => RunPlayerRules.DeclareForcedPass(card, SwiftnessPassedTurns));
			Effect("fx_ULDA_103", "ULDA_103e", "Alchemist's Stone Player Enchantment", null,
				() => new Power
				{
					Trigger = Trig(TriggerType.AFTER_PLAY_CARD, new AddEnchantmentTask("ULDA_103e2", EntityType.HAND), TriggerSource.FRIENDLY,
						new SelfCondition(p => !PaidEven(p)))
				});
			Buff("fx_ULDA_103e2", "ULDA_103e2", "Alchemy's First Law", "Costs (1) less.", Effects.ReduceCost(1));
			Effect("fx_ULDA_116", "ULDA_116e", "Ever-Changing Player Enchantment", null,
				() => new Power { Trigger = Trig(TriggerType.TURN_END, new CustomTask((g, c, s, t, stack) => EverChanging(g, c, s))) });
			Effect("fx_ULDA_117", "ULDA_117e", "Scroll Player Enchantment", null,
				() => new Power
				{
					Enchant = new Enchant(GameTag.SPELLPOWER, EffectOperator.ADD),
					Trigger = Trig(TriggerType.TURN_END, new CustomTask((g, c, s, t, stack) => ScrollDecay(g, c, s))),
				});
			// The Discover hook is the engine's (DiscoverRealRules.Completed -> RunPlayerRules.Discovered); the copies are added
			// once the chosen card has reached its zone (RunPlayerRules.DeliverDiscovered).
			Effect("fx_ULDA_205", "ULDA_205e", "Lucky Spade Player Enchantment", null, () => new Power());
			WithShape("fx_ULDA_205", RunPlayerRules.DeclareLuckySpade);
			Effect("fx_ULDA_208", "ULDA_208e", "Titanic Ring Player Enchantment", null,
				() => new Power { Aura = new Aura(AuraType.BOARD, "ULDA_208e2") });
			Buff("fx_ULDA_208e2", "ULDA_208e2", "Titanic Ring", "+1 Health and <b>Taunt</b>.", Effects.Health_N(1), Effects.TauntEff);
			// Mulligan replacements are not draws of the game (the retail rule); every other draw of a spell is.
			Effect("fx_ULDA_508", "ULDA_508e", "Fleeting Knowledge Player Enchantment", null,
				() => new Power
				{
					Trigger = Trig(TriggerType.DRAW, new AddEnchantmentTask("ULDA_508e2", EntityType.TARGET), TriggerSource.FRIENDLY,
						new SelfCondition(p => p is Spell && p.Game.Step != Step.BEGIN_MULLIGAN))
				});
			Effect("fx_ULDA_508e2", "ULDA_508e2", "Fleeting Knowledge", null,
				() => new Power { Enchant = new Enchant(Effects.SetCost(0)) { IsOneTurnEffect = true } }, oneTurn: true);

			// ---------------- the treasures (name and text as the client prints them)
			Treasure("ULDA_005", "Mummy Magic", "<b>Passive</b>\nAfter you play a <b>Deathrattle</b> minion,\ngive it <b>Reborn</b>.",
				effect: "fx_ULDA_005", ownShell: true);
			Treasure("ULDA_007", "Crook and Flail", "<b>Passive</b>\nAfter you cast a spell, put a minion from your deck into the battlefield.",
				effect: "fx_ULDA_007", ownShell: true);
			Treasure("ULDA_009", "Unlocked Potential", "<b>Passive</b>\nAt the start of the game, set the Attack of each of your minions equal to its Health.",
				onStart: UnlockedPotential, ownShell: true);
			Treasure(VipMembership, "VIP Membership", "<b>Passive</b>\nTaverns you visit are upgraded!", ownShell: true);
			Treasure("ULDA_014", "Disks of Legend", "<b>Passive</b>\nAfter you play a Legendary minion, summon a copy of it.",
				effect: "fx_ULDA_014", ownShell: true);
			Treasure("ULDA_042", "Darklight Torch", "<b>Passive</b>\nAfter you play an even-Cost card, refresh your hero power. It costs (0) this turn.",
				effect: "fx_ULDA_042", ownShell: true);
			Treasure("ULDA_046", "Band of Bees", "<b>Passive</b>\nYour minions that cost (2) or less have <b>Poisonous</b>.",
				effect: "fx_ULDA_046", ownShell: true);
			Treasure("ULDA_101", "Band of Scarabs", "<b>Passive</b>\nEnemy minions have\n-1 Attack.", effect: "fx_ULDA_101", ownShell: true);
			Treasure("ULDA_102", "Disks of Swiftness", "<b>Passive</b>\nYour opponent must pass their first 2 turns.", effect: "fx_ULDA_102", ownShell: true);
			Treasure("ULDA_103", "Alchemist's Stone", "<b>Passive</b>\nAfter you play an odd-Cost card, reduce the Cost of cards in your hand by (1).",
				effect: "fx_ULDA_103", ownShell: true);
			Treasure("ULDA_116", "Ever-Changing Elixir", "<b>Passive</b>\nAt the end of your turn, transform a friendly minion into one that costs (1) more.",
				effect: "fx_ULDA_116", ownShell: true);
			Treasure("ULDA_117", "Scroll of Nonsense", "<b>Passive</b>\n<b>Spell Damage +10</b>. At the end of your turn, this loses 1 <b>Spell Damage</b>.",
				onStart: ScrollOfNonsense, ownShell: true);
			Treasure("ULDA_205", "Lucky Spade", "<b>Passive</b>\nAfter you <b>Discover</b> a card, add 2 copies of it to your hand. They all cost (2) less.",
				effect: "fx_ULDA_205", ownShell: true);
			Treasure("ULDA_208", "Titanic Ring", "<b>Passive</b>\nYour minions have +1 Health and <b>Taunt</b>.", effect: "fx_ULDA_208", ownShell: true);
			Treasure("ULDA_508", "Robes of Diminishing", "<b>Passive</b>\nAfter you draw a spell, reduce its Cost to (0) this turn.",
				effect: "fx_ULDA_508", ownShell: true);
		}
	}

	public static partial class RunPlayerRules
	{
		// ULDA_102 Disks of Swiftness: the enchantment's count of the opponent's turn starts and the turn of the latest pass.
		// Kept in the enchantment's own tag store (never published), so clones and the bot's search replay them.
		private const GameTag PassCount = (GameTag)33640, PassTurn = (GameTag)33641;
		// ULDA_205 Lucky Spade: the Discovered cards still waiting for their copies (entity ids, in order).
		private const GameTag LuckyFirst = (GameTag)33642;
		private const int LuckySlots = 8;
		private static readonly Dictionary<int, int> ForcedPass = new Dictionary<int, int>();
		private static readonly HashSet<int> LuckySpade = new HashSet<int>();

		/// <summary>The private tags these rules keep on their own player enchantments.</summary>
		public static bool IsPrivateTag(GameTag tag) => (int)tag >= 33640 && (int)tag < 33642 + LuckySlots;

		internal static void DeclareForcedPass(Card enchantment, int turns) => ForcedPass[enchantment.AssetId] = turns;
		internal static void DeclareLuckySpade(Card enchantment) => LuckySpade.Add(enchantment.AssetId);

		private static int Tag(IEntity e, GameTag tag) => e.NativeTags.TryGetValue(tag, out int v) ? v : 0;

		/// <summary>ULDA_102: an opponent's turn starts; while it is one of its first passes, record this turn as a pass.</summary>
		internal static void OpponentTurnStarts(Game g, Enchantment e)
		{
			if (e == null || !ForcedPass.TryGetValue(e.Card.AssetId, out int turns)) return;
			int n = Tag(e, PassCount);
			if (n >= turns) return;
			e.NativeTags[PassCount] = n + 1;
			e.NativeTags[PassTurn] = g.Turn;
		}

		/// <summary>
		/// ULDA_102: <paramref name="c"/> is the current player and this turn is one its opponent's Disks of Swiftness makes a
		/// pass: its options are End Turn only and Game.Process refuses any other action (a choice already open still resolves).
		/// </summary>
		public static bool MustPass(Controller c)
		{
			if (ForcedPass.Count == 0 || c == null || c.Game == null || c.Game.CurrentPlayer != c) return false;
			var list = c.Opponent?.AppliedEnchantments;
			if (list == null || list.Count == 0) return false;
			for (int i = 0; i < list.Count; i++)
				if (ForcedPass.ContainsKey(list[i].Card.AssetId) && Tag(list[i], PassCount) > 0 && Tag(list[i], PassTurn) == c.Game.Turn) return true;
			return false;
		}

		private static Enchantment LuckySpadeOf(Controller c)
		{
			var list = c?.AppliedEnchantments;
			if (LuckySpade.Count == 0 || list == null || list.Count == 0) return null;
			for (int i = 0; i < list.Count; i++) if (LuckySpade.Contains(list[i].Card.AssetId)) return list[i];
			return null;
		}

		/// <summary>
		/// ULDA_205: <paramref name="c"/> Discovered <paramref name="chosen"/>: it costs (2) less, and two copies are queued for
		/// <see cref="DeliverDiscovered"/>, which adds them once the chosen card has reached its zone.
		/// </summary>
		internal static void Discovered(Controller c, IPlayable chosen)
		{
			Enchantment spade = LuckySpadeOf(c);
			if (spade == null || !(chosen is Playable card)) return;
			CostRules.AddModifier(card, EffectOperator.SUB, DungeonRunContent.LuckySpadeDiscount);
			for (int i = 0; i < LuckySlots; i++)
				if (Tag(spade, LuckyFirst + i) == 0) { spade.NativeTags[LuckyFirst + i] = chosen.Id; return; }
		}

		/// <summary>ULDA_205: adds the queued copies (each costs (2) less) to <paramref name="c"/>'s hand, while there is room.</summary>
		public static void DeliverDiscovered(Controller c)
		{
			Enchantment spade = LuckySpadeOf(c);
			if (spade == null || c.Choice != null || c.Game.Resolution.HasSuspendedEffect || c.Game.State == State.COMPLETE) return;
			for (int i = 0; i < LuckySlots; i++)
			{
				int id = Tag(spade, LuckyFirst + i);
				if (id == 0) continue;
				spade.NativeTags.Remove(LuckyFirst + i);
				if (!c.Game.IdEntityDic.TryGetValue(id, out IPlayable chosen)) continue;
				for (int k = 0; k < DungeonRunContent.LuckySpadeCopies && !c.HandZone.IsFull; k++)
				{
					IPlayable copy = Entity.FromCard(c, chosen.Card);
					copy[GameTag.DISPLAYED_CREATOR] = chosen[GameTag.DISPLAYED_CREATOR];
					Generic.AddHandPhase.Invoke(c, copy);
					if (copy.Zone != c.HandZone || !(copy is Playable p)) continue;
					CostRules.AddModifier(p, EffectOperator.SUB, DungeonRunContent.LuckySpadeDiscount);
					if (c.Game.History) c.Game.PowerHistory.Add(PowerHistoryBuilder.TagChange(copy.Id, GameTag.COST, copy.Cost));
				}
			}
		}
	}
}
