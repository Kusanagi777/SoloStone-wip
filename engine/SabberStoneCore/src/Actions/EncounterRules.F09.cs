using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.Actions
{
	// Phase 8, family 09 (Dungeon Run bosses C: the five final bosses, the special rooms, the special starts). What the
	// special rooms need from the engine beyond a script: a boss that holds no cards (Tad and the Treasure Vault have no hand,
	// draw nothing and never take Fatigue damage), a boss that plays no card from its hand for a while (A. F. Kay's idle
	// turns), and a fight a script ends with no loss for the run player (the Treasure Vault's "End the game"). Each is one bit
	// of one private controller tag (EncounterRules.RoomRules, 32644, in the tag table of EncounterRules.cs) or the game's
	// NoLoss mark, so clones and AI simulations replay it; a controller without the tag takes one tag read per draw and per
	// playability check.
	public static partial class EncounterRules
	{
		/// <summary><see cref="RoomRules"/>: the room's rules were applied (the hand went back into the deck once).</summary>
		public const int RoomEntered = 1;
		/// <summary><see cref="RoomRules"/>: the controller never draws a card and takes no Fatigue damage (Generic.Draw does nothing).</summary>
		public const int RoomNoDraw = 2;
		/// <summary><see cref="RoomRules"/>: the controller plays no card from its hand (PlayLockRules.BlocksPlay); its hero power and attacks stay.</summary>
		public const int RoomNoPlay = 4;

		/// <summary>The controller never draws: a draw of its own does nothing, no card and no Fatigue (Tad, the Treasure Vault).</summary>
		public static bool NeverDraws(Controller c) => c != null && (c[RoomRules] & RoomNoDraw) != 0;

		/// <summary>The controller plays no card from its hand now (A. F. Kay's idle turns; Tad and the Treasure Vault always).</summary>
		public static bool PlaysNoCards(Controller c) => c != null && (c[RoomRules] & RoomNoPlay) != 0;

		/// <summary>
		/// Puts the room's rules on <paramref name="c"/> (bits of <see cref="RoomRules"/>), once: a second call changes nothing.
		/// With <see cref="RoomNoDraw"/> the hand it was dealt goes back into its deck (<see cref="ReturnHandToDeck"/>): the
		/// engine deals every player an opening hand, and a boss with "no hand" must not keep it. Call it after the mulligan
		/// (the room powers call it from the first turn start of the match).
		/// </summary>
		public static void EnterRoom(Controller c, int rules)
		{
			if ((c[RoomRules] & RoomEntered) != 0) return;
			c[RoomRules] = rules | RoomEntered;
			if ((rules & RoomNoDraw) != 0) ReturnHandToDeck(c);
		}

		/// <summary>Sets or clears rule bits of <paramref name="c"/>'s room (A. F. Kay stops being idle on her sixth turn).</summary>
		public static void SetRoomRule(Controller c, int rule, bool on) =>
			c[RoomRules] = on ? c[RoomRules] | rule : c[RoomRules] & ~rule;

		/// <summary>
		/// Every card of <paramref name="c"/>'s hand is shuffled back into its deck (a public count change; the cards stay its
		/// own; the Coin is set aside), and a deck fact tells its own-deck ledger the deck's exact list again (<see cref="DeckRewritten"/>).
		/// </summary>
		public static void ReturnHandToDeck(Controller c)
		{
			IPlayable[] hand = c.HandZone.ToArray();
			if (hand.Length == 0) return;
			foreach (IPlayable card in hand)
			{
				// The Coin (the second player's) is no card of the deck: it leaves the game's zones.
				if (card.Card.Id == "GAME_005") { c.SetasideZone.Add(c.HandZone.Remove(card)); continue; }
				if (c.DeckZone.IsFull) break;
				IPlayable removed = c.HandZone.Remove(card);
				removed.Reset();
				Generic.ShuffleIntoDeck(c, c.Hero, removed);
			}
			DeckRewritten(c, c.DeckZone.Select(p => p.Card.Id));
		}

		/// <summary>
		/// A script ends the fight with no loss for the run player (the Treasure Vault's Doors Are Closing, "End the game"): the
		/// game is marked (<see cref="NoLoss"/>, which the harvest reports) and <paramref name="closing"/>, the side whose script
		/// ends it, leaves the table as a hero that is destroyed. The run layer records a fight the boss closed as won with no
		/// boss kill credit.
		/// </summary>
		public static void EndWithNoLoss(Controller closing)
		{
			Game game = closing.Game;
			if (game.State == State.COMPLETE) return;
			game[NoLoss] = 1;
			closing.Hero.ToBeDestroyed = true;
		}
	}
}
