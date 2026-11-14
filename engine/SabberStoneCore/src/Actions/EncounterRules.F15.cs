using System.Linq;
using SabberStoneCore.CardSets.DungeonRun;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.Actions
{
	// Phase 8, family 15 (Dalaran Heist bosses B): The Rat King's Underbelly Rat, "When your hero dies, the eldest Underbelly Rat
	// replaces your hero." ResolveHeroForms calls ResolveUnderbellyRats first, before the boss's forms and the run player's
	// relay: a dead hero whose controller has a living Underbelly Rat on its board is replaced by the eldest one (the rat
	// summoned first: the lowest entity id). The rat leaves the board without dying (no death, no Deathrattle; it goes to its
	// controller's set-aside zone) and the hero comes back with the rat's Health: its remaining Health out of its maximum.
	// For the boss of an encounter that states The Rat King as a script form (the row dala_30_rat_king), a new Rat King hero
	// enters through EncounterRules.SwapForm (hero and power fresh; Armor, weapon and the attacks made this turn stay); a
	// run player who controls a rat keeps the hero entity. As many times as there are rats. Everything is board state and
	// entity tags (the form index and count of the boss), so clones and AI simulations replay it; outside an encounter match
	// no Underbelly Rat exists (the card is a boss's only and never generated) and the scan finds none.
	public static partial class EncounterRules
	{
		/// <summary>DALA_BOSS_30t Underbelly Rat (the minion A Tale of Kings summons) and the hero it brings back.</summary>
		public const string UnderbellyRat = "DALA_BOSS_30t", RatKingHero = "DALA_BOSS_30h";

		/// <summary>The rat that would replace <paramref name="c"/>'s hero now: its eldest living Underbelly Rat in play, or null.</summary>
		public static Minion EldestUnderbellyRat(Controller c) =>
			c?.BoardZone.Where(m => m.Card.Id == UnderbellyRat && !m.ToBeDestroyed && m.Health > 0 && !m.IsDormant).OrderBy(m => m.Id).FirstOrDefault();

		/// <summary>The index of the encounter's Rat King script form (the hero a rat brings back for the boss), or -1.</summary>
		private static int RatKingForm(RunEncounter enc)
		{
			for (int k = 1; k < enc.BossForms.Count; k++)
				if (enc.BossForms[k].Hero?.Id == RatKingHero) return k;
			return -1;
		}

		/// <summary>ResolveHeroForms, first: every dead hero whose controller has a living Underbelly Rat is replaced by the eldest.</summary>
		internal static void ResolveUnderbellyRats(Game game)
		{
			RunEncounter enc = Of(game);
			if (enc == null) return;
			foreach (Controller c in new[] { game.Player1, game.Player2 })
			{
				Hero hero = c.Hero;
				if (c.PlayState == PlayState.CONCEDED || !(hero.ToBeDestroyed || hero.Health <= 0)) continue;
				Minion rat = EldestUnderbellyRat(c);
				if (rat != null) RatReplacesHero(c, rat, c == game.Player2 ? RatKingForm(enc) : -1);
			}
		}

		private static void RatReplacesHero(Controller c, Minion rat, int form)
		{
			int max = rat.BaseHealth, remaining = rat.Health;
			c.BoardZone.Remove(rat);
			c.SetasideZone.Add(rat);
			Hero hero = c.Hero;
			if (form > 0) { SwapForm(c, form); hero = c.Hero; }
			else
			{
				hero.ToBeDestroyed = false;
				c.PlayState = PlayState.PLAYING;
			}
			hero.BaseHealth = max;
			hero.Damage = max - remaining;
		}
	}
}
