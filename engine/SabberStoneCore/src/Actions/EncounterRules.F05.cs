using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Auras;
using SabberStoneCore.CardSets.DungeonRun;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.Actions
{
	// Phase 8, family 05 (the global-rule framework and the Dalaran Heist anomalies and twists): the engine rules its rule
	// cards share with later encounter families, each written once.
	//   BattlecriesTwice / DeathrattlesTwice   DALA_852, DALA_853; the boss powers LOOTA_BOSS_16p / 17p / 38p, GILA_BOSS_47p
	//                                          (bothPlayers: one aura on the boss's power covers both sides).
	//   RecastRandom                           DALA_856 Wild Magic ("cast an additional time, targets chosen randomly").
	//   Blockers / PlaceBlockers               DALA_833 The Carts: a board limit made of permanents that take board slots.
	//   Imprison / TickPrison                  DALA_832 The Prisons: a Violet Prison releases its minion after as many of its
	//                                          owner's turns as the minion costs.
	//   SwapStatsOnSummon                      DALA_831 The Stench: Attack and Health are swapped as a minion enters play.
	// All of it reads live board state and entity tags, so clones and AI simulations replay it. The family's private tags
	// (RuleApplied 32660, RuleSpells 32661; RuleSummons 32662 since the wave 2 integration) are declared in the tag table of EncounterRules.cs.
	public static partial class EncounterRules
	{
		// ------------------------------------------------------------------ Battlecries and Deathrattles trigger twice

		/// <summary>
		/// "Battlecries trigger twice": the EXTRA_BATTLECRIES_BASE controller aura (Brann Bronzebeard's and Battle Totem's:
		/// Controller.ExtraBattlecry; twice, however many sources). For its owner's controller, or with
		/// <paramref name="bothPlayers"/> for both ("All Battlecries trigger twice" on one boss power). A global rule attaches its
		/// player enchantment to each player, so each copy is the one-player aura.
		/// </summary>
		public static Aura BattlecriesTwice(bool bothPlayers = false) =>
			new Aura(bothPlayers ? AuraType.CONTROLLERS : AuraType.CONTROLLER, new Effect(GameTag.EXTRA_BATTLECRIES_BASE, EffectOperator.ADD, 1));

		/// <summary>
		/// "Deathrattles trigger twice": the EXTRA_DEATHRATTLES_BASE controller aura (Totem of the Dead's; DeathrattleRules.Repeats
		/// reads it from the live source auras: twice, however many sources). <paramref name="bothPlayers"/> as
		/// <see cref="BattlecriesTwice"/>.
		/// </summary>
		public static Aura DeathrattlesTwice(bool bothPlayers = false) =>
			new Aura(bothPlayers ? AuraType.CONTROLLERS : AuraType.CONTROLLER, new Effect(GameTag.EXTRA_DEATHRATTLES_BASE, EffectOperator.ADD, 1));

		// ------------------------------------------------------------------ a spell casts an additional time, targets chosen randomly

		/// <summary>
		/// Casts a fresh copy of <paramref name="spell"/> for its controller with random targets (the engine's effect play,
		/// AutoPlayRules.Play: a random legal target, a random Choose One option, any choice it opens picked with the game RNG).
		/// The copy carries RecastRules.Recasting, so it never triggers another recast. A Secret or a Quest is not cast again: it
		/// could not enter play twice, and the failed copy would only show the opponent what was played. Returns whether a copy
		/// was cast.
		/// </summary>
		public static bool RecastRandom(Spell spell, IEntity source)
		{
			if (spell == null || RecastRules.IsRecast(spell) || spell.Card.IsSecret || spell.Card.IsQuest) return false;
			Controller c = spell.Controller;
			Game g = c.Game;
			if (g.State == State.COMPLETE) return false;
			var copy = (Spell)Entity.FromCard(c, spell.Card);
			copy.NativeTags[RecastRules.Recasting] = 1;
			g.OnRandomHappened(true);
			return AutoPlayRules.Play(copy, source ?? spell);
		}

		// ------------------------------------------------------------------ a board limit made of permanents

		/// <summary>
		/// The permanents on <paramref name="c"/>'s board that only take board slots (a card with UNTOUCHABLE and no Attack: the
		/// Carts): each one lowers the minions its owner can have by one.
		/// </summary>
		public static int Blockers(Controller c, IReadOnlyCollection<string> blockerIds) =>
			c.BoardZone.Count(m => blockerIds.Contains(m.Card.Id));

		/// <summary>
		/// Puts <paramref name="count"/> of <paramref name="blockers"/> (distinct ones, picked with the game RNG) on
		/// <paramref name="c"/>'s board as permanents; fewer when the board has no room. A permanent is an UNTOUCHABLE card: it
		/// cannot be targeted, attacked, damaged, destroyed, transformed, bounced or taken (the engine's board rules for
		/// untouchable cards), so the slots are gone for the whole match: with three, "both players can only have four minions".
		/// </summary>
		public static void PlaceBlockers(Controller c, IReadOnlyList<Card> blockers, int count, IEntity source)
		{
			Game g = c.Game;
			var left = blockers.ToList();
			if (left.Any(card => !card.Untouchable)) throw new ArgumentException("A board blocker is an untouchable card.");
			for (int i = 0; i < count && left.Count > 0 && !c.BoardZone.IsFull; i++)
			{
				g.OnRandomHappened(true);
				int pick = g.Random.Next(left.Count);
				Generic.SummonBlock(g, (Minion)Entity.FromCard(c, left[pick]), -1, source);
				left.RemoveAt(pick);
			}
		}

		// ------------------------------------------------------------------ a prison that releases its minion

		/// <summary>
		/// Puts <paramref name="prison"/> (an UNTOUCHABLE permanent) on <paramref name="c"/>'s board holding
		/// <paramref name="inside"/>: the held card is the prison's ALTERNATE_MOUSE_OVER_CARD (what the client shows on
		/// mouse-over) and the owner turns left its SCORE_VALUE_1 (the client's Dormant-style counter), both public tags, so both
		/// players and the boss's fair view read the same facts. The minion is released at the start of its owner's turn number
		/// "Cost" counted from when the prison landed (a 0- or 1-Cost minion at the first one). Returns the prison, or null when
		/// the board is full.
		/// </summary>
		public static Minion Imprison(Controller c, Card prison, Card inside, IEntity source)
		{
			if (prison == null || !prison.Untouchable || inside == null || inside.Type != CardType.MINION) throw new ArgumentException("A prison is an untouchable card that holds a minion.");
			if (c.BoardZone.IsFull) return null;
			Game g = c.Game;
			var entity = (Minion)Entity.FromCard(c, prison);
			int turns = Math.Max(1, inside.Cost);
			entity[GameTag.ALTERNATE_MOUSE_OVER_CARD] = inside.AssetId;
			entity[GameTag.SCORE_VALUE_1] = turns;
			entity[GameTag.TAG_SCRIPT_DATA_NUM_2] = turns;
			Generic.SummonBlock(g, entity, -1, source);
			return entity.Zone == c.BoardZone ? entity : null;
		}

		/// <summary>The minion card a prison holds (<see cref="Imprison"/>), or null.</summary>
		public static Card Imprisoned(Minion prison)
		{
			int asset = prison == null ? 0 : prison[GameTag.ALTERNATE_MOUSE_OVER_CARD];
			Card card = asset == 0 || asset == prison.Card.AssetId ? null : Cards.FromAssetId(asset);
			return card != null && card.Type == CardType.MINION ? card : null;
		}

		/// <summary>The owner turns left before a prison releases its minion.</summary>
		public static int PrisonTurns(Minion prison) => prison == null ? 0 : prison[GameTag.SCORE_VALUE_1];

		/// <summary>
		/// The start of the prison owner's turn (the prison card's own trigger): one turn less; at 0 the prison is replaced by its
		/// minion (a transform in place: no Battlecry, no summon event; the minion entered play this turn, so it cannot attack
		/// unless it has Charge, or Rush for minions).
		/// </summary>
		public static void TickPrison(Minion prison)
		{
			if (prison == null || prison.Zone != prison.Controller.BoardZone || prison.Game.State == State.COMPLETE) return;
			int left = PrisonTurns(prison) - 1;
			if (left > 0)
			{
				prison[GameTag.SCORE_VALUE_1] = left;
				prison[GameTag.TAG_SCRIPT_DATA_NUM_2] = left;
				return;
			}
			Card inside = Imprisoned(prison);
			if (inside == null) return;
			Controller c = prison.Controller;
			int position = prison.ZonePosition;
			if (!Generic.TransformBlock(c, inside, prison) || position >= c.BoardZone.Count) return;
			Minion released = c.BoardZone[position];
			if (released.Card.AssetId != inside.AssetId || released.IsDormant) return;
			// It entered play now: summoning sickness as for a summoned minion (Minion.UpdateAttackReadinessAfterKeywordLoss reads
			// the entry turn: Charge is ready, Rush may attack minions, anything else waits a turn).
			released.IsExhausted = !released.HasCharge && !released.IsRush;
			if (released.IsRush && !released.HasCharge && c == c.Game.CurrentPlayer && !released.AttackableByRush)
			{
				released.AttackableByRush = true;
				c.Game.RushMinions.Add(released.Id);
			}
		}

		// ------------------------------------------------------------------ Attack and Health swapped as a minion enters play

		/// <summary>
		/// The body of a SUMMON trigger (the trigger's target is the minion that entered play): its Attack and Health are swapped
		/// once, with <paramref name="enchantmentId"/> as the shown enchantment (Crazed Alchemist's rule; later buffs apply to the
		/// swapped values). A permanent (an untouchable card) is left alone; a minion that enters with 0 Attack has 0 Health and
		/// dies.
		/// </summary>
		public static Tasks.ISimpleTask SwapStatsOnSummon(string enchantmentId)
		{
			var swap = new SwapAttackHealthTask(EntityType.TARGET, enchantmentId);
			return new CustomTask((g, c, s, t, stack) =>
			{
				if (!(t is Minion m) || m.Zone?.Type != Zone.PLAY || m.Card.Untouchable || m.ToBeDestroyed) return;
				swap.Process(g, c, s, t, stack);
			});
		}
	}
}
