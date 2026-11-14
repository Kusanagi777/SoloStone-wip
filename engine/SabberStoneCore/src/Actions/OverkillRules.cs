using System.Collections.Generic;
using SabberStoneCore.Auras;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.Actions
{
	/// <summary>
	/// Overkill that an aura grants (phase 7 S1: TRLA_171 Akali's Horn, TRLA_162 Halazzi's Hunt, "Your minions have
	/// '<b>Overkill</b>: ...'"). Character.TakeDamage reads the source card's own <see cref="Enchants.Power.OverkillTask"/>; a
	/// minion in play also has every Overkill task of the auras applied to it (the aura's enchantment card carries the task, so
	/// it holds in AI clones and history-free games, where an aura creates no enchantment entity). Weapons and heroes are
	/// unchanged.
	/// </summary>
	public static class OverkillRules
	{
		/// <summary>
		/// Private: 1 on a minion whose Overkill granted "this can attack again" during its current attack; the attack's end
		/// consumes it (that attack does not count against the minion's attacks this turn). Never sent in a native packet
		/// (<see cref="ShrineRules.IsPrivateTag"/> covers it).
		/// </summary>
		public const GameTag AttackAgain = (GameTag)32614;

		/// <summary>The aura-granted Overkill tasks of <paramref name="source"/> (a minion in play), one per applied aura, in aura order.</summary>
		public static IReadOnlyList<ISimpleTask> Granted(IPlayable source)
		{
			List<ISimpleTask> tasks = null;
			if (source is Minion m && m.Zone?.Type == Zone.PLAY)
				foreach (IAura aura in m.Game.Auras)
					if (aura is Aura a && a.EnchantmentCard?.Power?.OverkillTask != null && a.AppliesTo(m.Id))
						(tasks ?? (tasks = new List<ISimpleTask>())).Add(a.EnchantmentCard.Power.OverkillTask);
			return (IReadOnlyList<ISimpleTask>)tasks ?? System.Array.Empty<ISimpleTask>();
		}

		/// <summary>An aura grants <paramref name="source"/> Overkill (Playable.HasOverkill).</summary>
		public static bool HasGranted(IPlayable source)
		{
			if (!(source is Minion m) || m.Zone?.Type != Zone.PLAY) return false;
			foreach (IAura aura in m.Game.Auras)
				if (aura is Aura a && a.EnchantmentCard?.Power?.OverkillTask != null && a.AppliesTo(m.Id)) return true;
			return false;
		}

		/// <summary>TRLA_171e "Overkill: This minion can attack again.": marks the attacking minion (consumed at the attack's end).</summary>
		public static ISimpleTask AttackAgainTask => new CustomTask((g, c, s, t, stack) =>
		{
			if (s is Minion m && m.IsAttacking && m.Zone?.Type == Zone.PLAY) m[AttackAgain] = 1;
		});

		/// <summary>Attack.cs, at the end of an attack: true (and the mark cleared) when an Overkill let the attacker attack again.</summary>
		internal static bool ConsumeAttackAgain(ICharacter source)
		{
			if (!(source is Minion m) || m[AttackAgain] == 0) return false;
			m.NativeTags.Remove(AttackAgain);
			return true;
		}
	}
}
