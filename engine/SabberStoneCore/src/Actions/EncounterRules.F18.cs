using System.Linq;
using SabberStoneCore.CardSets.DungeonRun;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.Actions
{
	// Phase 8, family 18 (Dalaran Heist bosses E and the Heist finale): the engine rules three of its powers need.
	//   ShortTurnSeconds     a player's short turn timer (the client's TIMEOUT tag): Nozdormu the Eternal's 15 seconds and
	//                        Magistrix Norroa's Kaironomaly, "Your opponent has 25 seconds to take their turn". NativeMatch reads
	//                        it for the human's and the NPC's turn clock; the engine itself has no clock.
	//   PreventsSelfDamage   Kazamon Steelskin's Steelskin, "You can't damage yourself" (tag SelfDamageImmune on the hero power,
	//                        in the tag table of EncounterRules.cs), read by Character.TakeDamage.
	//   SwapLeftmostWithDeck Marei Loom's Next..., "Swap the left-most card in each player's hand with one in their deck".
	// All of it reads live state or one private card tag, so clones and AI simulations replay it.
	public static partial class EncounterRules
	{
		/// <summary>Kaironomaly (DALA_BOSS_43p): the run player's turn timer, in seconds.</summary>
		public const int KaironomalySeconds = 25;

		/// <summary>The client's ordinary turn timer of a middle-game turn (Game.StartGame sets TIMEOUT 75); a timer below <see cref="ShortTurnBelow"/> is a short one.</summary>
		public const int ShortTurnBelow = 45;

		/// <summary>
		/// The short turn timer <paramref name="c"/> plays under, in seconds, or 0 for the ordinary clock: its own TIMEOUT tag
		/// (the base value an effect wrote, without controller auras) when it is below <see cref="ShortTurnBelow"/> (Nozdormu the
		/// Eternal: 15; Kaironomaly: 25), else 0. A TIMEOUT aura (classic Nozdormu, EX1_560: SET -55) is not read: it never shortened
		/// a turn in NativeMatch and must neither make one short nor cancel Kaironomaly's. NativeMatch ends a turn whose short timer
		/// ran out, whether or not the player's own Turn Timer option is on (as it did for 15 alone).
		/// </summary>
		public static int ShortTurnSeconds(Controller c)
		{
			if (c == null) return 0;
			int t = c[GameTag.TIMEOUT] - c.ControllerAuraEffects[GameTag.TIMEOUT];
			return t > 0 && t < ShortTurnBelow ? t : 0;
		}

		/// <summary>
		/// Kaironomaly at the start of <paramref name="opponent"/>'s turn: its timer becomes <see cref="KaironomalySeconds"/>
		/// unless a shorter one holds already (Nozdormu the Eternal's 15 stays 15).
		/// </summary>
		public static void ShortenTurn(Controller opponent, int seconds)
		{
			if (opponent == null || seconds <= 0) return;
			int current = ShortTurnSeconds(opponent);
			if (current > 0 && current <= seconds) return;
			opponent.TimeOut = seconds;
		}

		/// <summary>
		/// Character.TakeDamage, for a hit on a hero, before its other damage rules: the hit is prevented when the hero's own
		/// hero power carries <see cref="SelfDamageImmune"/> and <paramref name="source"/> is controlled by the hero's controller
		/// (Kazamon Steelskin's Steelskin, "You can't damage yourself"). Nothing is dealt: no damage, no Armor lost, no damage
		/// event. Fatigue is the game's rule, not a source of the player's (the engine deals it with the hero itself as the
		/// source): it still hurts.
		/// </summary>
		public static bool PreventsSelfDamage(Hero hero, IPlayable source)
		{
			if (hero == null || source == null || source == hero || source.Controller != hero.Controller) return false;
			HeroPower power = hero.HeroPower;
			return power != null && power[SelfDamageImmune] > 0;
		}

		/// <summary>
		/// Marei Loom's Next... for one player <paramref name="c"/>: with a card in hand and one in deck, a random card of the
		/// deck (the game RNG, picked first) leaves it, the left-most hand card is shuffled into the deck (as a card returned
		/// to a deck, it loses its hand enchantments; an Echo copy stops being one), and the deck card takes the left-most hand slot. The player sees the
		/// new card (its hand entry is the ordinary one); what the opponent believed about the player's hand and deck is reset
		/// (RunHelpers.BeliefReset), so a fair search never learns a hidden card it did not see. False when nothing moved.
		/// </summary>
		public static bool SwapLeftmostWithDeck(Controller c, IEntity source)
		{
			Game g = c.Game;
			if (g.State == State.COMPLETE || c.HandZone.Count == 0 || c.DeckZone.Count == 0) return false;
			IPlayable pick = c.DeckZone[g.Random.Next(c.DeckZone.Count)];
			g.OnRandomHappened(true);
			IPlayable left = c.HandZone[0];
			IPlayable taken = c.DeckZone.Remove(pick);
			IPlayable back = c.HandZone.Remove(left);
			back.Reset();
			// An Echo copy (a ghostly card, removed from the hand at the end of the turn) that goes into the deck is an ordinary
			// card there: it leaves the game's ghostly list, which names hand cards only.
			g.GhostlyCards.Remove(back.Id);
			Generic.ShuffleIntoDeck(c, source, back);
			using (g.Facts.HandEntry(taken, HistoryHandEntryKind.Other, Zone.DECK))
			{
				if (g.History)
				{
					var show = PowerHistoryBuilder.ShowEntity(taken);
					if (show.Entity.Tags[GameTag.ZONE] == (int)Zone.DECK) show.Entity.Tags[GameTag.ZONE] = (int)Zone.HAND;
					g.PowerHistory.Add(show);
				}
				c.HandZone.Add(taken, 0);
			}
			RunHelpers.BeliefReset(c, RunBeliefZones.HandAndDeck);
			return true;
		}
	}
}
