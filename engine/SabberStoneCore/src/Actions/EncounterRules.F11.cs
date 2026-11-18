using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.Actions
{
	// Phase 8, family 11 (Monster Hunt bosses B): the engine rules its powers need beyond family 06's forced attack.
	//   AllAttackRandomEnemyMinions / ForcedAttackRandomEnemyMinion   GILA_BOSS_41p Survival of the Fittest ("All minions attack
	//                                                                 random enemy minions"): family 06's ForcedAttack, minions only.
	//   MarkRefresh / RefreshIfMarked                                 GILA_BOSS_57p Desecrate ("If any die, refresh this"): a hero
	//                                                                 power is exhausted only after its task, so the use marks the
	//                                                                 power and its own Inspire trigger readies it again.
	// All of it reads live board state and entity tags, so clones and AI simulations replay it. The family's private tag
	// (PowerRefresh 32646) is declared in the tag table of EncounterRules.cs.
	public static partial class EncounterRules
	{
		// ------------------------------------------------------------------ every minion attacks a random enemy minion

		/// <summary>
		/// <paramref name="attacker"/> attacks a random enemy minion (in play, alive, not dormant or a permanent: uniform, game
		/// RNG, reported as a random event when there is a choice), as <see cref="ForcedAttack"/> (whatever its state; Taunt and
		/// Stealth do not matter). The enemy hero is never the defender. Returns the defender, or null when no attack was made.
		/// </summary>
		public static Minion ForcedAttackRandomEnemyMinion(ICharacter attacker)
		{
			Game g = attacker?.Game;
			if (g == null || g.State == State.COMPLETE || !CanFight(attacker)) return null;
			var targets = attacker.Controller.Opponent.BoardZone.Where(m => CanFight(m)).OrderBy(m => m.ZonePosition).ToList();
			if (targets.Count == 0) return null;
			if (targets.Count > 1) g.OnRandomHappened(true);
			Minion defender = targets[targets.Count == 1 ? 0 : g.Random.Next(targets.Count)];
			return ForcedAttack(attacker, defender) ? defender : null;
		}

		/// <summary>
		/// "All minions attack random enemy minions": every minion of both boards that can fight when the effect starts, in the
		/// order they entered play, attacks a random enemy minion (<see cref="ForcedAttackRandomEnemyMinion"/>); each attack's
		/// deaths resolve before the next, and a minion that died or has no enemy minion left to fight is passed over. Returns
		/// how many attacks were made.
		/// </summary>
		public static int AllAttackRandomEnemyMinions(Game g)
		{
			if (g == null || g.State == State.COMPLETE) return 0;
			var attackers = g.Player1.BoardZone.Concat(g.Player2.BoardZone).Where(m => CanFight(m)).OrderBy(m => m.OrderOfPlay).ThenBy(m => m.Id).ToList();
			int made = 0;
			foreach (Minion attacker in attackers)
			{
				if (g.State == State.COMPLETE) break;
				if (ForcedAttackRandomEnemyMinion(attacker) != null) made++;
			}
			return made;
		}

		// ------------------------------------------------------------------ "refresh this" on a hero power

		/// <summary>Marks <paramref name="power"/>'s resolving use as one that refreshes it (<see cref="RefreshIfMarked"/> readies it).</summary>
		public static void MarkRefresh(IEntity power)
		{
			if (power is HeroPower p) p[PowerRefresh] = 1;
		}

		/// <summary>
		/// The power's own Inspire trigger (after its use, once the engine exhausted it): a marked power is readied again and the
		/// mark cleared. Returns whether it was refreshed.
		/// </summary>
		public static bool RefreshIfMarked(IEntity power)
		{
			if (!(power is HeroPower p) || p[PowerRefresh] == 0) return false;
			p[PowerRefresh] = 0;
			p.IsExhausted = false;
			return true;
		}
	}
}
