using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model.Zones;

namespace SabberStoneCore.Actions
{
	// Phase 8, family 10 (Monster Hunt bosses A): the set-aside store. A holder (Gnomenapper's Stuffed Sack; family 17's
	// Zuramat reuses it) keeps ONE minion out of play: the minion itself, with its damage and enchantments, leaves its board
	// for its own controller's set-aside zone and comes back to that controller's board when the holder releases it. The link
	// is one private tag on the holder (EncounterRules.SetAsideStore, in the tag table of EncounterRules.cs) that holds the
	// stored minion's entity id, so clones and AI simulations replay it; a copy of the holder carries the same link, and the
	// first release consumes the minion (the other holder then releases nothing). The stored minion was a public board
	// minion: nothing hidden is read or written. A run shrine never enters a store.
	public static partial class EncounterRules
	{
		/// <summary>
		/// <paramref name="m"/> can be put into a store: a minion on a board that an effect can pick (not dormant, not a
		/// permanent, not already dying) and not a run shrine (awake or dormant: a shrine never leaves its board).
		/// </summary>
		public static bool CanStore(Minion m) =>
			m != null && m.Zone is BoardZone && !m.Untouchable && !m.ToBeDestroyed && !ShrineRules.IsMarked(m);

		/// <summary>The minion <paramref name="holder"/> keeps out of play right now, or null (none, or already released).</summary>
		public static Minion StoredBy(IEntity holder)
		{
			if (holder == null) return null;
			int id = holder[SetAsideStore];
			return id != 0 && holder.Game.IdEntityDic.TryGetValue(id, out IPlayable p) && p is Minion m
				&& m.Zone != null && m.Zone == m.Controller.SetasideZone ? m : null;
		}

		/// <summary>
		/// Puts <paramref name="stored"/> into <paramref name="holder"/>'s store: it leaves its board as it is (its triggers and
		/// auras stop, its damage and enchantments stay on it) for its controller's set-aside zone. False, and nothing happens,
		/// when the holder already keeps a minion or the minion cannot be stored (<see cref="CanStore"/>).
		/// </summary>
		public static bool Store(IEntity holder, Minion stored)
		{
			if (holder == null || holder[SetAsideStore] != 0 || !CanStore(stored)) return false;
			Game game = stored.Game;
			Controller owner = stored.Controller;
			owner.BoardZone.Remove(stored);
			owner.SetasideZone.Add(stored);
			holder[SetAsideStore] = stored.Id;
			game.AuraUpdate();
			return true;
		}

		/// <summary>
		/// Releases the minion <paramref name="holder"/> keeps: it returns to its own controller's board, right-most, as a
		/// summoned minion (whoever controls the holder by now). With that board full the minion stays out of play for good.
		/// The link is consumed either way. Returns the minion when it is back on a board, else null.
		/// </summary>
		public static Minion Release(IEntity holder, IEntity source)
		{
			Minion stored = StoredBy(holder);
			if (holder != null && holder[SetAsideStore] != 0) holder[SetAsideStore] = 0;
			if (stored == null || stored.Game.State == State.COMPLETE) return null;
			Controller owner = stored.Controller;
			if (owner.BoardZone.IsFull) return null;
			owner.SetasideZone.Remove(stored);
			Generic.SummonBlock(stored.Game, stored, -1, source ?? owner.Hero);
			return stored.Zone == owner.BoardZone ? stored : null;
		}
	}
}
