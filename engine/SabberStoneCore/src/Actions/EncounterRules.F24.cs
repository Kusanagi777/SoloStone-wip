using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.Actions
{
	// Phase 8, family 24 (the Plague Lords and Tekahn): the engine rules their powers need beyond the forms of the model.
	//   LoseHealth                 ULDA_BOSS_39p1 Made of Murlocs ("You are Immune. After a Murloc dies, lose 3 Health"): Health
	//                              lost, not damage taken, so Immune does not stop it; the boss's next form enters at once.
	//   PanicAttacker / EvokePanic ULDA_BOSS_38p1 Evoke Panic ("Force the minion with the lowest Health to attack one of its
	//                              neighbors"): a forced attack inside one row (family 06's ForcedAttack is across rows only).
	//   BringToOne                 ULDA_BOSS_38p3 Murmurs of Mortality ("bring an enemy minion to 1 Health").
	// K'zrath's rotation (ULDA_BOSS_38p2) is family 16's EncounterRules.Rotate. All of it reads live board state and entity
	// tags, so clones and AI simulations replay it; the family's private tag (PlagueCount 32659) is declared in the tag table
	// of EncounterRules.cs.
	public static partial class EncounterRules
	{
		// ------------------------------------------------------------------ Health lost, not damage

		/// <summary>
		/// <paramref name="hero"/> loses <paramref name="amount"/> Health (no damage event: Immune, Armor and "when your hero takes
		/// damage" do not apply). A hero brought to 0 dies as from damage. Then the boss's forms are resolved at once (a
		/// "healthAtMost" form enters as soon as its threshold is crossed). Returns the Health actually lost.
		/// </summary>
		public static int LoseHealth(Hero hero, int amount)
		{
			Game g = hero?.Game;
			if (g == null || amount <= 0 || g.State == State.COMPLETE || hero.Health <= 0 || !ReferenceEquals(hero.Controller.Hero, hero)) return 0;
			int lost = System.Math.Min(amount, hero.Health);
			hero.Damage += lost;
			if (hero.Health <= 0) hero.ToBeDestroyed = true;
			else ResolveHeroForms(g);
			return lost;
		}

		// ------------------------------------------------------------------ Evoke Panic

		/// <summary>
		/// The minion Evoke Panic forces to attack: among every minion of both rows that can fight (in play, alive, not a
		/// permanent or a dormant minion), those with the lowest Health; one of them by the game RNG (reported as a random event
		/// when there is a choice). Null when no minion can fight.
		/// </summary>
		public static Minion PanicAttacker(Game g)
		{
			var minions = g.Player1.BoardZone.Concat(g.Player2.BoardZone).Where(m => CanFight(m)).ToList();
			if (minions.Count == 0) return null;
			int lowest = minions.Min(m => m.Health);
			var lows = minions.Where(m => m.Health == lowest).OrderBy(m => m.OrderOfPlay).ThenBy(m => m.Id).ToList();
			if (lows.Count == 1) return lows[0];
			g.OnRandomHappened(true);
			return lows[g.Random.Next(lows.Count)];
		}

		/// <summary>The minions next to <paramref name="m"/> in its row that can fight (left first).</summary>
		public static List<Minion> Neighbors(Minion m) =>
			m.Controller.BoardZone.Where(x => x != m && System.Math.Abs(x.ZonePosition - m.ZonePosition) == 1 && CanFight(x))
				.OrderBy(x => x.ZonePosition).ToList();

		/// <summary>
		/// ULDA_BOSS_38p1 Evoke Panic: the lowest-Health minion (<see cref="PanicAttacker"/>) attacks one of its neighbors (by the
		/// game RNG when it has two), whatever its state (Mass Hysteria's rule, as <see cref="ForcedAttack"/>; no attack option of
		/// the turn is spent). A minion with no neighbor that can fight attacks nobody. Returns the defender, or null.
		/// </summary>
		public static Minion EvokePanic(Game g)
		{
			if (g == null || g.State == State.COMPLETE) return null;
			Minion attacker = PanicAttacker(g);
			if (attacker == null) return null;
			var neighbors = Neighbors(attacker);
			if (neighbors.Count == 0) return null;
			Minion defender = neighbors[0];
			if (neighbors.Count > 1)
			{
				g.OnRandomHappened(true);
				defender = neighbors[g.Random.Next(neighbors.Count)];
			}
			var before = g.CurrentEventData;
			Generic.AttackBlock.Invoke(attacker.Controller, attacker, defender, true, false);
			attacker.Controller.NumOptionsPlayedThisTurn--;
			g.CurrentEventData = before;
			return defender;
		}

		/// <summary>Evoke Panic does something: some row holds two neighbors that can fight (public boards only).</summary>
		public static bool PanicPossible(Game g) =>
			new[] { g.Player1, g.Player2 }.Any(c => c.BoardZone.Any(m => CanFight(m) && Neighbors(m).Count > 0));

		// ------------------------------------------------------------------ bring a minion to 1 Health

		/// <summary>
		/// ULDA_BOSS_38p3 Murmurs of Mortality: one of <paramref name="enemy"/>'s minions above 1 Health (in play, alive, not a
		/// permanent or a dormant minion; by the game RNG) is brought to 1 Health. Health set, not damage dealt: no damage event,
		/// Divine Shield and Immune do not stop it. Returns the minion, or null when none is above 1 Health.
		/// </summary>
		public static Minion BringToOne(Controller enemy)
		{
			Game g = enemy.Game;
			if (g.State == State.COMPLETE) return null;
			var minions = enemy.BoardZone.Where(m => CanFight(m) && m.Health > 1).OrderBy(m => m.ZonePosition).ToList();
			if (minions.Count == 0) return null;
			Minion pick = minions[0];
			if (minions.Count > 1)
			{
				g.OnRandomHappened(true);
				pick = minions[g.Random.Next(minions.Count)];
			}
			pick.Damage += pick.Health - 1;
			return pick;
		}
	}
}
