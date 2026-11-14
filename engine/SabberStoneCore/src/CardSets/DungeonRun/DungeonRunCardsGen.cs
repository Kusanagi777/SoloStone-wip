using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.src.Loader;

namespace SabberStoneCore.CardSets.DungeonRun
{
	/// <summary>
	/// Card scripts for the Dungeon Run mode (docs/design/dungeon-run.md). Every script sits on an existing,
	/// unscripted client entity (a "shell") listed in <see cref="DungeonRunContent"/>; this set adds behaviour,
	/// never new cards, and gives each shell its run cost and stats.
	/// </summary>
	public static class DungeonRunCardsGen
	{
		/// <summary>
		/// Hero power ids that are passive (cannot be clicked) and run their power once at the start of the
		/// game when a hero starts with them. See <see cref="Model.Entities.HeroPower.ActivatesAtGameStart"/>.
		/// Built from content metadata only, so reading it never forces the card database to load.
		/// </summary>
		public static readonly IReadOnlyCollection<string> PassiveHeroPowers = new HashSet<string>(
			DungeonRunContent.All.Where(c => c.Kind == RunContentKind.PassivePower).Select(c => c.ShellId)
				// Phase 8: the encounters' passive boss powers, keyed by their own client ids (DungeonRunEncounters.Passive).
				.Concat(DungeonRunEncounters.PassivePowers));

		/// <summary>Registers the Dungeon Run scripts. Called by the CardDefs constructor.</summary>
		public static void AddAll(Dictionary<string, CardDef> cards)
		{
			foreach (RunContent content in DungeonRunContent.All)
			{
				if (content.ShellId == null || content.Script == null) continue;
				CardDef def = content.Script();
				var shape = content.Shape;
				def.Configure = card =>
				{
					card.Implemented = true;
					shape?.Invoke(card);
				};
				cards.Add(content.ShellId, def);
			}
			// Phase 7: the Rumble Run shrines, shrine upgrades and teammates, scripted on their own client cards.
			DungeonRunShrines.AddAll(cards);
			// Phase 8: the encounter families' boss powers, boss cards, tokens and global rules, on their own client cards.
			DungeonRunEncounters.AddAll(cards);
		}
	}
}
