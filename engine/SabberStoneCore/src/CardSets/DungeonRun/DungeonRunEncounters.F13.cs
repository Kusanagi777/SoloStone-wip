using SabberStoneCore.Enums;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.DungeonRun
{
	// Phase 8, family 13: the Rumble Run champions (registered by DungeonRunEncounters' constructor). Eight of the nine use a
	// class's basic hero power, which the engine already scripts; Zul'jin's Steady Throw is the one new power. The champions'
	// shrines are the run player's board shrines of phase 7 (DungeonRunShrines, ShrineRules), armed for the boss by the
	// encounter row's shrine variant (RunMatchPlan.BossShrine); every shrine script reads its own controller, so the same
	// scripts serve either side.
	public static partial class DungeonRunEncounters
	{
		/// <summary>TRLA_065p Steady Throw, Zul'jin's hero power (TRLA_204h's tag 380).</summary>
		public const string SteadyThrow = "TRLA_065p";

		private static void F13()
		{
			// ---------------- Zul'jin (TRLA_204h)
			// Steady Throw (2): "Deal 2 damage to the enemy hero." As Steady Shot (DS1h_292) is: no target, the enemy hero takes
			// 2 (a hero power: no Spell Damage). The client's second text ("Deal 2 damage.") is the targeted form a Hunter's
			// "your Hero Power can target minions" card shows; no such card is in Zul'jin's decks, and REQ_STEADY_SHOT names
			// Steady Shot only, so the power never takes a target.
			Power(SteadyThrow, null, () => new DamageTask(2, EntityType.OP_HERO));
		}
	}
}
