using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.CardSets.DungeonRun;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.Actions
{
	// Phase 8, family 12 (Monster Hunt bosses C, passive powers): the engine rules two of its boss powers need beyond a trigger
	// or an aura, each read from live game state only (no tag of its own; every clone and AI simulation replays it).
	//   EchoCopiesLeft                   The Whisperer's "All spells have Echo" (GILA_BOSS_50p): a per-turn limit on the copies
	//                                    (see below). The client's Echo rule that a copy never costs less than (1)
	//                                    (EchoCopyFloor) began here and holds in every game since the wave 4 integration.
	//   DeathNeighbours                  Ratcatcher Hannigul's Cannibalism (GILA_BOSS_67p): the minions that stood next to a
	//                                    minion in its board when it died.
	public static partial class EncounterRules
	{
		/// <summary>GILA_BOSS_50p Echoes of the Witchwood: "All spells have Echo."</summary>
		public const string WhispererPower = "GILA_BOSS_50p";

		/// <summary>
		/// The Echo copies (Game.GhostlyCards) a player may receive in one turn while The Whisperer's rule gives every spell Echo:
		/// once a player has received this many this turn, its spells have no Echo until the turn ends. The client has no such
		/// number; it stands in for the client's turn timer, which is what ends a turn of endlessly repeated (1)-Cost copies (The
		/// Coin returns the Mana its copy costs: The Coin, then its copy, then the copy's copy, without end). A recorded
		/// adaptation of the encounter row.
		/// </summary>
		public const int EchoCopiesPerTurn = 10;

		/// <summary>The Whisperer's rule is in play: a hero of the match has GILA_BOSS_50p as its power (it is passive: always live).</summary>
		public static bool EchoRuleActive(Game game) =>
			game != null && (game.Player1?.Hero?.HeroPower?.Card.Id == WhispererPower || game.Player2?.Hero?.HeroPower?.Card.Id == WhispererPower);

		/// <summary>
		/// The Echo copies <paramref name="c"/> received this turn: the copies Game.GhostlyCards lists for it (until the turn ends:
		/// Game.MainCleanUp), and a copy in its hand that is not listed yet (PlayCard updates the auras between creating a copy
		/// and listing it, so the copy counts from the moment it is in hand).
		/// </summary>
		public static int EchoCopiesThisTurn(Controller c)
		{
			Game game = c.Game;
			int n = 0;
			foreach (int id in game.GhostlyCards)
				if (game.IdEntityDic.TryGetValue(id, out IPlayable p) && p.Controller == c) n++;
			foreach (IPlayable p in c.HandZone)
				if (p[GameTag.GHOSTLY] == 1 && !game.GhostlyCards.Contains(p.Id)) n++;
			return n;
		}

		/// <summary>The Whisperer's rule may still give <paramref name="c"/>'s spells Echo this turn (<see cref="EchoCopiesPerTurn"/>).</summary>
		public static bool EchoCopiesLeft(Controller c) => EchoCopiesThisTurn(c) < EchoCopiesPerTurn;

		/// <summary>
		/// CostRules.AdjustCost, last: "Copies can't cost less than (1)" (the client's Echo rule since Descent of Dragons, keyword
		/// tooltip on hearthstone.wiki.gg/wiki/Echo) for every Echo copy (GHOSTLY: the copy an Echo or "Repeatable this turn" card
		/// puts into the hand) in every game. Family 12 added it for The Whisperer's fight only; the wave 4 integration made it
		/// general, after family 11 met a (0)-Cost Sound the Bells! loop under a Mage shrine without The Whisperer. The per-turn
		/// copy limit (<see cref="EchoCopiesPerTurn"/>) stays The Whisperer's. Unstable Evolution's copy (LOOT_504t) is no
		/// GHOSTLY card in this engine, but the client prints the card with Echo since patch 23.0.3 (hearthstone.wiki.gg/wiki/
		/// Unstable_Evolution: changed from "Repeatable this turn" to stop a (0)-Cost loop), so its copy has the floor too (the
		/// wave 5 integration, after the run player's copies under Millhouse Manastorm's (0) looped without end). For any
		/// other card <paramref name="amount"/> is returned unchanged.
		/// </summary>
		internal static int EchoCopyFloor(IPlayable source, int amount)
		{
			if (amount >= 1 || source is HeroPower || source[GameTag.GHOSTLY] != 1 && source.Card.Id != UnstableEvolutionCopy) return amount;
			return 1;
		}

		/// <summary>LOOT_504t, the hand copy Unstable Evolution (LOOT_504) adds; see <see cref="EchoCopyFloor"/>.</summary>
		private const string UnstableEvolutionCopy = "LOOT_504t";

		/// <summary>
		/// The minions that stood next to <paramref name="dead"/> in its board when it died, inside the engine's death pass
		/// (Game.GraveYard raises each death while that minion is still in its zone, after the minions processed before it left
		/// theirs): the board is put back together from the slots now and the dying minions that already left, each at its last
		/// board position (a dead run shrine's dormant form stands in its own slot). A neighbour that dies in the same pass, a
		/// dormant minion and an untouchable permanent are not returned. Empty when <paramref name="dead"/> is not on a board.
		/// </summary>
		public static List<Minion> DeathNeighbours(Minion dead)
		{
			var result = new List<Minion>(2);
			Controller c = dead?.Controller;
			if (c == null || dead.Zone != c.BoardZone) return result;
			Game game = c.Game;
			var board = c.BoardZone.Slots.ToList();
			foreach (Minion gone in game.DeadMinions.Where(m => m.Controller == c && m.Zone != c.BoardZone && !ShrineRules.IsMarked(m))
				.OrderBy(m => m.LastBoardPosition))
				board.Insert(System.Math.Min(System.Math.Max(0, gone.LastBoardPosition), board.Count), gone);
			int at = board.IndexOf(dead);
			foreach (int i in new[] { at - 1, at + 1 })
			{
				if (i < 0 || i >= board.Count || !(board[i] is Minion m)) continue;
				if (m.Zone != c.BoardZone || m.ToBeDestroyed || m.IsDormant || m.Untouchable || game.DeadMinions.Contains(m)) continue;
				result.Add(m);
			}
			return result;
		}
	}
}
