using SabberStoneCore.CardSets.DungeonRun;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.Actions
{
	// Phase 8, family 08 (Dungeon Run bosses B): the tag team of George and Karl (LOOTA_BOSS_15h / LOOTA_BOSS_32h).
	// Two heroes share one side of the table, each with its own Health: the active one fights, the other waits on the bench.
	// LOOTA_BOSS_110 Tag Team swaps them (hero and power), and when the active one dies the benched one takes over; the boss
	// loses when both are defeated. The encounter row states the pair as one "script" form (form 0 George and Karl with
	// Divinity, form 1 Karl and George with The Silver Hand), so the swap itself is EncounterRules.SwapForm; what this file
	// adds is the bench: one private controller tag that keeps the waiting hero's damage, so clones and AI simulations
	// replay it.
	public static partial class EncounterRules
	{
		// The bench is one private controller tag, EncounterRules.TagTeamBench (32643, in the tag table of EncounterRules.cs).
		/// <summary>The value of <see cref="TagTeamBench"/> once the benched hero is defeated.</summary>
		public const int TagTeamDefeated = 1000000;
		/// <summary>The tag team's two hero cards: form 0 (George and Karl) and form 1 (Karl and George).</summary>
		public const string TagTeamFirst = "LOOTA_BOSS_15h", TagTeamSecond = "LOOTA_BOSS_32h";

		/// <summary>The match is the tag team's: an encounter whose two forms are George and Karl, then Karl and George.</summary>
		public static bool IsTagTeam(Game game)
		{
			RunEncounter enc = Of(game);
			return enc != null && enc.BossForms.Count == 2 && enc.BossForms[0].Hero?.Id == TagTeamFirst && enc.BossForms[1].Hero?.Id == TagTeamSecond;
		}

		/// <summary>The benched hero's remaining Health (0 once it is defeated, or outside the tag team's match).</summary>
		public static int TagTeamBenchHealth(Controller boss)
		{
			if (boss == null || !IsTagTeam(boss.Game)) return 0;
			int bench = boss[TagTeamBench];
			if (bench >= TagTeamDefeated) return 0;
			RunForm other = Of(boss.Game).BossForms[boss[FormIndex] == 0 ? 1 : 0];
			int max = other.Health > 0 ? other.Health : other.Hero[GameTag.HEALTH];
			return max - (bench > 0 ? bench - 1 : 0);
		}

		/// <summary>
		/// LOOTA_BOSS_110 Tag Team, "Swap George and Karl": the benched hero comes in with the Health it left with (its power
		/// fresh, as after every hero power replacement) and the active one goes to the bench with its damage. Armor, the weapon
		/// and the attacks made this turn stay with the side (EncounterRules.SwapForm). Nothing when the benched hero is
		/// defeated, when the caster is not the tag team's boss (a copy in the run player's hands), or after the game ended.
		/// </summary>
		public static bool TagTeam(Controller c)
		{
			Game game = c.Game;
			if (c != game.Player2 || game.State == State.COMPLETE || !IsTagTeam(game)) return false;
			if (c[TagTeamBench] >= TagTeamDefeated || c.Hero.ToBeDestroyed || c.Hero.Health <= 0) return false;
			TagTeamEnter(c, false);
			return true;
		}

		/// <summary>
		/// ResolveBoss, before the Health and death forms: in the tag team's match a dead active hero is replaced by the benched
		/// one when that one is still standing (which is then the last: the bench is marked defeated). True when the match is the
		/// tag team's (the form loop has nothing to do there: its one form is a script form).
		/// </summary>
		private static bool TagTeamDeath(Controller boss)
		{
			if (!IsTagTeam(boss.Game)) return false;
			Hero hero = boss.Hero;
			if ((hero.ToBeDestroyed || hero.Health <= 0) && boss[TagTeamBench] < TagTeamDefeated) TagTeamEnter(boss, true);
			return true;
		}

		private static void TagTeamEnter(Controller boss, bool death)
		{
			int bench = boss[TagTeamBench];
			int leaving = boss.Hero.Damage;
			SwapForm(boss, boss[FormIndex] == 0 ? 1 : 0);
			// SwapForm brings a form in at its full Health; the tag team's hero keeps the damage it went to the bench with.
			if (bench > 0) boss.Hero.Damage = bench - 1;
			boss[TagTeamBench] = death ? TagTeamDefeated : leaving + 1;
		}
	}
}
