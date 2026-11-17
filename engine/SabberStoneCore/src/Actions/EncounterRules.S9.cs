using System.Linq;
using SabberStoneCore.CardSets.DungeonRun;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.Actions
{
	// Step 9 lane X (handoff/phase9/plan.md D11, D13, D14): the match ends a run fight may take besides a win or a loss, and the
	// Tekahn form ratchet. An escape (ULDA_111 Hearthstone, "Escape to the Tavern and remove this from your Adventure deck")
	// and a restart (GILA_911 Rewind, "When your hero dies, restart the game"; GILA_BOSS_48t Alternate Timeline, "Start a new
	// game, but your opponent's Health stays the same") are marks on the game (the tags below, in the encounter tag table's
	// reserved 32638-32640), so clones and AI simulations replay them; the harvest (EncounterRules.Harvest) reports them and the
	// run layer (plugin DungeonRun.Outcomes.cs) records them. What a fight may do is the match's RunMatchEnds
	// (GameConfig.MatchEnds), set only on a tuned run (a schedule of stamp 4 and above): every other match has none, and every
	// hook here returns at once.
	public static partial class EncounterRules
	{
		/// <summary>On the game: 1 once the run player escaped the fight (ULDA_111 Hearthstone). Reserved by step 9 stage 0.</summary>
		public const GameTag Escape = (GameTag)32638;
		/// <summary>
		/// On the game: the restart the fight ended with (<see cref="RestartRewind"/>, <see cref="RestartAlternateTimeline"/>); 0
		/// for none. Reserved by step 9 stage 0.
		/// </summary>
		public const GameTag Restart = (GameTag)32639;
		/// <summary><see cref="Restart"/>: GILA_911 Rewind (the run player's hero died).</summary>
		public const int RestartRewind = 1;
		/// <summary><see cref="Restart"/>: GILA_BOSS_48t Alternate Timeline (cast by the boss).</summary>
		public const int RestartAlternateTimeline = 2;

		/// <summary>The card ids of the restart sources, by their <see cref="Restart"/> value.</summary>
		public static string RestartSourceOf(int restart) =>
			restart == RestartRewind ? RunMatchEnds.Rewind : restart == RestartAlternateTimeline ? RunMatchEnds.AlternateTimeline : null;

		private static RunMatchEnds Ends(Game game) => game?.MatchEndsOrNull;

		/// <summary>A script ended the fight as an escape or a restart: the forms, the relay and Rewind no longer apply.</summary>
		public static bool EndedByScript(Game game) => game != null && (game[Escape] != 0 || game[Restart] != 0);

		/// <summary>
		/// ULDA_111 Hearthstone may be cast by <paramref name="c"/> now: the run player of a match whose plan allows the escape
		/// (RunMatchPlan.EscapeAllowed: a tuned run, the deck above its minimum, not a finale act, not the last node). False in
		/// every other match, so the card is unplayable there (a card play requirement: checked when it is cast).
		/// </summary>
		public static bool EscapeAllowed(Controller c) =>
			c?.Game != null && c == c.Game.Player1 && Ends(c.Game)?.EscapeAllowed == true && !EndedByScript(c.Game);

		/// <summary>
		/// <paramref name="source"/> (a restart card id) may still restart this fight: the match lists it (the run layer's
		/// per-source caps: once per floor each) and no escape or restart has ended it yet.
		/// </summary>
		public static bool RestartAllowed(Game game, string source) =>
			Ends(game)?.Allows(source) == true && !EndedByScript(game);

		/// <summary>
		/// ULDA_111 Hearthstone's cast: the fight ends as an escape. The game is marked (<see cref="Escape"/>), the card is used up
		/// (RunMatchResult.ConsumedTreasures: the run layer removes it from the run deck in its escape branch), and the boss
		/// leaves the table as a hero that is destroyed (the Treasure Vault's ending, EndWithNoLoss): the engine reports the run
		/// player's win, which the run layer records as an escape (no major or kill credit, no drafts; the boss keeps the Health
		/// it has, which the harvest reports).
		/// </summary>
		public static void EscapeToTavern(Controller c)
		{
			Game game = c.Game;
			if (game.State == State.COMPLETE || !EscapeAllowed(c)) return;
			game[Escape] = 1;
			RunHelpers.ConsumeTreasure(game, RunMatchEnds.Hearthstone);
			c.Opponent.Hero.ToBeDestroyed = true;
		}

		/// <summary>
		/// GILA_BOSS_48t Alternate Timeline's cast: the fight ends as a restart (<see cref="Restart"/> =
		/// <see cref="RestartAlternateTimeline"/>). Both heroes leave the table (a tie in the engine; nothing is decided), and the
		/// run layer restages the floor with the run player's damage of this game carried into the new one ("your opponent's
		/// Health stays the same"). Does nothing when the match does not allow it.
		/// </summary>
		public static void RestartGame(Controller caster)
		{
			Game game = caster.Game;
			if (game.State == State.COMPLETE || !RestartAllowed(game, RunMatchEnds.AlternateTimeline)) return;
			game[Restart] = RestartAlternateTimeline;
			game.Player1.Hero.ToBeDestroyed = true;
			game.Player2.Hero.ToBeDestroyed = true;
		}

		/// <summary>
		/// GILA_911 Rewind (a passive treasure, "When your hero dies, restart the game and remove this from your Dungeon Deck"):
		/// called beside the boss forms and the relay (<see cref="ResolveHeroForms"/>), after them, so a relay hero that enters
		/// first keeps the fight going. When the run player's hero is dead (not a concession), the match allows Rewind and nothing
		/// ended the fight yet, the game is marked (<see cref="Restart"/> = <see cref="RestartRewind"/>) and Rewind is used up
		/// (RunMatchResult.ConsumedTreasures); the game then ends as the loss it is, and the run layer restages the floor from the
		/// fight's start.
		/// </summary>
		private static void ResolveRewind(Game game)
		{
			if (Ends(game) == null || !RestartAllowed(game, RunMatchEnds.Rewind)) return;
			Controller player = game.Player1;
			Hero hero = player.Hero;
			if (player.PlayState == PlayState.CONCEDED || !(hero.ToBeDestroyed || hero.Health <= 0)) return;
			game[Restart] = RestartRewind;
			RunHelpers.ConsumeTreasure(game, RunMatchEnds.Rewind);
		}

		/// <summary>
		/// D11 (RunMatchPlan.FormRatchet, Tekahn on acts-v4t): the maximum Health of a boss entering <paramref name="form"/>,
		/// given the Health it would otherwise keep (<paramref name="max"/>): a form with a Health threshold and no Health of its
		/// own is clamped to the threshold, so the boss cannot heal back above a phase it has passed. Unchanged without the ratchet.
		/// </summary>
		private static int RatchetMax(Game game, RunForm form, int max) =>
			Ends(game)?.FormRatchet == true && form.Health == 0 && form.HealthAtMost > 0 ? System.Math.Min(max, form.HealthAtMost) : max;
	}
}

namespace SabberStoneCore.Model
{
	using SabberStoneCore.CardSets.DungeonRun;

	public partial class Game
	{
		/// <summary>The match ends of this run fight (GameConfig.MatchEnds), or null when it has none.</summary>
		internal RunMatchEnds MatchEndsOrNull => _gameConfig?.MatchEnds;
	}
}
