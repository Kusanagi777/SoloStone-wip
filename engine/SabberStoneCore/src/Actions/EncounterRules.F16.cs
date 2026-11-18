using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.Actions
{
	// Phase 8, family 16 (Dalaran Heist bosses C): the board rotation (the Carousel Gryphon's Merry Go Round; family 24's
	// K'zrath, "Rotate all minions counter-clockwise", reuses it). The two rows are one ring: seen from the player whose row is
	// at the bottom of the screen, a counter-clockwise step moves the bottom row right and the top row left, so the bottom
	// row's rightmost minion crosses to the right end of the top row and the top row's leftmost minion crosses to the left end
	// of the bottom row (clockwise: the mirror image). Every row keeps its size, except that an empty row gains the one minion
	// that crosses to it. A minion that crosses changes controller exactly as a control change does (ControlTask: it keeps its
	// damage, enchantments and age; the control-change history fact is recorded). Only minions an effect can pick are in the
	// ring: a dormant or permanent board entity (Darius's Cannons) and a run shrine, awake or dormant, never move to the other
	// row (ShrineRules.BlocksControl) and keep their place; the ring rotates around them. No hidden zone is read; no state is
	// kept (no tag).
	public static partial class EncounterRules
	{
		/// <summary>The minions of <paramref name="c"/>'s row that rotate, in board order (left to right).</summary>
		public static List<Minion> RotationRing(Controller c) =>
			c.BoardZone.Where(m => !m.Untouchable && !m.ToBeDestroyed && !ShrineRules.BlocksControl(m)).ToList();

		/// <summary>
		/// Rotates every minion one place around the board (<paramref name="clockwise"/> or counter-clockwise as
		/// <paramref name="bottom"/>, the player whose row is at the bottom of the screen, sees it). Returns the minions that
		/// changed sides (at most two: one each way), in the order they crossed. Nothing happens once the game is over.
		/// </summary>
		public static IReadOnlyList<Minion> Rotate(Controller bottom, bool clockwise, IEntity source)
		{
			var crossed = new List<Minion>();
			Game game = bottom.Game;
			if (game.State == State.COMPLETE) return crossed;
			Controller top = bottom.Opponent;
			List<Minion> low = RotationRing(bottom), high = RotationRing(top);
			// Counter-clockwise: the bottom row's rightmost goes up to the top row's right end, the top row's leftmost comes
			// down to the bottom row's left end. Clockwise: the bottom row's leftmost goes up to the top row's left end, the top
			// row's rightmost comes down to the bottom row's right end.
			Minion up = low.Count == 0 ? null : clockwise ? low[0] : low[low.Count - 1];
			Minion down = high.Count == 0 ? null : clockwise ? high[high.Count - 1] : high[0];
			int upAt = clockwise ? 0 : -1, downAt = clockwise ? -1 : 0;
			if (up == null)
			{
				if (down != null && RotateCross(down, bottom, downAt, source)) crossed.Add(down);
			}
			else
			{
				// The minion going up leaves first, so the one coming down always finds room (and the other way round).
				using var dormant = DormantRules.PreserveControl(up);
				using var history = game.Facts.ControlTransfer(up, source);
				int home = up.ZonePosition;
				bottom.BoardZone.Remove(up);
				game.AuraUpdate();
				if (down != null && RotateCross(down, bottom, downAt, source)) crossed.Add(down);
				if (top.BoardZone.IsFull) RotateEnter(up, bottom, home);
				else
				{
					RotateEnter(up, top, upAt);
					crossed.Insert(0, up);
				}
			}
			game.AuraUpdate();
			return crossed;
		}

		// One minion changes sides at a zone position (ControlTask's steps); false when the other row is full.
		private static bool RotateCross(Minion m, Controller to, int position, IEntity source)
		{
			if (to.BoardZone.IsFull) return false;
			Game game = m.Game;
			using var dormant = DormantRules.PreserveControl(m);
			using var history = game.Facts.ControlTransfer(m, source);
			m.Controller.BoardZone.Remove(m);
			game.AuraUpdate();
			RotateEnter(m, to, position);
			return true;
		}

		private static void RotateEnter(Minion m, Controller to, int position)
		{
			m.Controller = to;
			m[GameTag.CONTROLLER] = to.PlayerId;
			to.BoardZone.Add(m, position, applyEntrySetters: false, preserveAge: true);
			if (m.HasCharge)
				m.IsExhausted = false;
			else if (m.IsRush)
			{
				m.IsExhausted = false;
				m.AttackableByRush = true;
			}
		}
	}
}
