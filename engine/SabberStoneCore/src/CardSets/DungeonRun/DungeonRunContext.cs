using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.CardSets.DungeonRun
{
	/// <summary>
	/// The run facts a Dungeon Run match may read (phase 6, design section 3): set once by the run layer on
	/// <see cref="Config.GameConfig.RunContext"/> and never changed, so every clone and AI simulation shares the same values.
	/// Null outside a run match; a card script reads it through <see cref="RunHelpers"/>, which treats null as an empty run.
	/// </summary>
	public sealed class RunContext
	{
		public static readonly RunContext Empty = new RunContext();

		/// <summary>Bosses the run has defeated before this fight (an act run counts its won fights, a legacy run Floor - 1).</summary>
		public int BossesDefeated { get; }
		/// <summary>Per run deck slot (index-aligned with <see cref="RunMatchPlan.PlayerDeck"/>): the slot's run log, oldest first.</summary>
		public IReadOnlyList<IReadOnlyList<string>> SlotLogs { get; }
		/// <summary>The scripted, admitted active treasures in the player's scope (the treasure-Discover pool), ordinal order.</summary>
		public IReadOnlyList<string> TreasurePool { get; }
		/// <summary>A one-fight bonus a treasure granted last fight (ULDA_504); null when none.</summary>
		public RunNextFight NextFight { get; }
		/// <summary>Per run deck slot: the card id the run deck holds there (as the match plan deals it).</summary>
		public IReadOnlyList<string> RunDeckIds { get; }
		/// <summary>Per run deck slot: where the card came from (starter, draft, treasure, signature, ...); null entries on a legacy run.</summary>
		public IReadOnlyList<string> RunDeckOrigins { get; }
		/// <summary>
		/// The adventure tag (DungeonRunTreasure.Pool: "MonsterHunt", "Tombs", ...) of each <see cref="TreasurePool"/> id that has
		/// one; an id with no tag is absent. The treasure-Discover helper (H2, <see cref="RunHelpers.TreasurePool"/>) filters on it.
		/// </summary>
		public IReadOnlyDictionary<string, string> TreasurePoolTags { get; }
		/// <summary>
		/// The run row's classes, primary first (a dual-class row such as Elise's DRUID_PRIEST holds both); empty when the run
		/// layer gives none. A class Discover of the run player's (<see cref="RunHelpers.RunPlayerClasses"/>) offers all of them.
		/// </summary>
		public IReadOnlyList<CardClass> PlayerClasses { get; }

		private RunContext()
		{
			PlayerClasses = Array.Empty<CardClass>();
			SlotLogs = Array.Empty<IReadOnlyList<string>>();
			TreasurePool = Array.Empty<string>();
			RunDeckIds = Array.Empty<string>();
			RunDeckOrigins = Array.Empty<string>();
			TreasurePoolTags = new Dictionary<string, string>(StringComparer.Ordinal);
		}

		public RunContext(int bossesDefeated, IEnumerable<IEnumerable<string>> slotLogs, IEnumerable<string> treasurePool, RunNextFight nextFight,
			IEnumerable<string> runDeckIds, IEnumerable<string> runDeckOrigins)
			: this(bossesDefeated, slotLogs, treasurePool, nextFight, runDeckIds, runDeckOrigins, null)
		{
		}

		/// <param name="treasurePoolTags">The adventure tag of each treasure pool id that has one (<see cref="TreasurePoolTags"/>);
		/// a tag for an id outside <paramref name="treasurePool"/> is an error.</param>
		public RunContext(int bossesDefeated, IEnumerable<IEnumerable<string>> slotLogs, IEnumerable<string> treasurePool, RunNextFight nextFight,
			IEnumerable<string> runDeckIds, IEnumerable<string> runDeckOrigins, IEnumerable<KeyValuePair<string, string>> treasurePoolTags)
			: this(bossesDefeated, slotLogs, treasurePool, nextFight, runDeckIds, runDeckOrigins, treasurePoolTags, null)
		{
		}

		/// <param name="playerClasses">The run row's classes, primary first (<see cref="PlayerClasses"/>); duplicates and
		/// INVALID are dropped.</param>
		public RunContext(int bossesDefeated, IEnumerable<IEnumerable<string>> slotLogs, IEnumerable<string> treasurePool, RunNextFight nextFight,
			IEnumerable<string> runDeckIds, IEnumerable<string> runDeckOrigins, IEnumerable<KeyValuePair<string, string>> treasurePoolTags,
			IEnumerable<CardClass> playerClasses)
		{
			PlayerClasses = Array.AsReadOnly((playerClasses ?? Enumerable.Empty<CardClass>()).Where(x => x != CardClass.INVALID).Distinct().ToArray());
			if (bossesDefeated < 0) throw new ArgumentOutOfRangeException(nameof(bossesDefeated));
			BossesDefeated = bossesDefeated;
			SlotLogs = Array.AsReadOnly((slotLogs ?? Enumerable.Empty<IEnumerable<string>>())
				.Select(l => (IReadOnlyList<string>)Array.AsReadOnly((l ?? Enumerable.Empty<string>()).ToArray())).ToArray());
			TreasurePool = Array.AsReadOnly((treasurePool ?? Enumerable.Empty<string>()).Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray());
			NextFight = nextFight;
			RunDeckIds = Array.AsReadOnly((runDeckIds ?? Enumerable.Empty<string>()).ToArray());
			RunDeckOrigins = Array.AsReadOnly((runDeckOrigins ?? Enumerable.Empty<string>()).ToArray());
			if (SlotLogs.Count != 0 && RunDeckIds.Count != 0 && SlotLogs.Count != RunDeckIds.Count)
				throw new ArgumentException("Run context slot logs must align with the run deck.");
			if (RunDeckOrigins.Count != 0 && RunDeckOrigins.Count != RunDeckIds.Count)
				throw new ArgumentException("Run context origins must align with the run deck.");
			var tags = new Dictionary<string, string>(StringComparer.Ordinal);
			foreach (var pair in treasurePoolTags ?? Enumerable.Empty<KeyValuePair<string, string>>())
			{
				if (pair.Value == null) continue;
				if (!TreasurePool.Contains(pair.Key)) throw new ArgumentException("Run context pool tag for " + pair.Key + ", which is not in the treasure pool.");
				tags[pair.Key] = pair.Value;
			}
			TreasurePoolTags = tags;
		}
	}

	/// <summary>A one-fight bonus (ULDA_504): extra starting Mana Crystals and extra cards drawn.</summary>
	public sealed class RunNextFight
	{
		public int Mana { get; }
		public int Draws { get; }
		public RunNextFight(int mana, int draws)
		{
			if (mana < 0 || draws < 0) throw new ArgumentOutOfRangeException(mana < 0 ? nameof(mana) : nameof(draws));
			Mana = mana; Draws = draws;
		}
		public bool IsEmpty => Mana == 0 && Draws == 0;
	}

	/// <summary>One run deck change a match asks for (H7): add a card, or replace or remove the card of a run deck slot.</summary>
	public sealed class RunEdit
	{
		public const string OpAdd = "add", OpReplace = "replace", OpRemove = "remove";
		/// <summary><see cref="OpAdd"/>, <see cref="OpReplace"/> or <see cref="OpRemove"/>.</summary>
		public string Op { get; }
		/// <summary>The run deck slot (index into <see cref="RunMatchPlan.PlayerDeck"/>); -1 for an add.</summary>
		public int SlotIndex { get; }
		/// <summary>The card id an add or a replace puts in the run deck; null for a remove.</summary>
		public string CardId { get; }
		public RunEdit(string op, int slotIndex, string cardId)
		{
			if (op != OpAdd && op != OpReplace && op != OpRemove) throw new ArgumentException("Unknown run edit " + op + ".", nameof(op));
			if (op == OpAdd ? slotIndex != -1 : slotIndex < 0) throw new ArgumentOutOfRangeException(nameof(slotIndex));
			if ((op == OpRemove) != (cardId == null)) throw new ArgumentException("A remove names no card; an add or a replace names one.", nameof(cardId));
			Op = op; SlotIndex = slotIndex; CardId = cardId;
		}
		public override string ToString() => Op + (SlotIndex >= 0 ? "@" + SlotIndex : "") + (CardId != null ? ":" + CardId : "");
	}

	/// <summary>
	/// What one run match asks the run layer to keep (design section 3), produced by card scripts through
	/// <see cref="RunHelpers"/>. Per game: a clone (an AI simulation) gets its own copy, so only the real game's result is
	/// harvested. The run layer applies it after a won fight only.
	/// </summary>
	public sealed class RunMatchResult
	{
		private readonly List<RunEdit> _edits = new List<RunEdit>();
		private readonly List<KeyValuePair<int, string>> _slotLogs = new List<KeyValuePair<int, string>>();

		/// <summary>Run deck changes, in the order the match made them.</summary>
		public IReadOnlyList<RunEdit> Edits => _edits;
		/// <summary>Run log lines per run deck slot (slot, line), in order (DALA_718, DALA_719).</summary>
		public IReadOnlyList<KeyValuePair<int, string>> SlotLogAppends => _slotLogs;
		/// <summary>A hero power that replaces the run's for the rest of the run (ULDA_306); null when none.</summary>
		public string PowerOverride { get; internal set; }
		/// <summary>A one-fight bonus for the next fight (ULDA_504); null when none.</summary>
		public RunNextFight NextFight { get; internal set; }
		public bool IsEmpty => _edits.Count == 0 && _slotLogs.Count == 0 && PowerOverride == null && NextFight == null;

		internal void Add(RunEdit edit) => _edits.Add(edit ?? throw new ArgumentNullException(nameof(edit)));
		internal void AddLog(int slot, string line)
		{
			if (slot < 0) throw new ArgumentOutOfRangeException(nameof(slot));
			_slotLogs.Add(new KeyValuePair<int, string>(slot, line ?? throw new ArgumentNullException(nameof(line))));
		}

		internal RunMatchResult Clone()
		{
			var copy = new RunMatchResult { PowerOverride = PowerOverride, NextFight = NextFight };
			copy._edits.AddRange(_edits);
			copy._slotLogs.AddRange(_slotLogs);
			return copy;
		}
	}

	/// <summary>The hidden-zone beliefs a <see cref="RunHelpers.BeliefReset"/> clears.</summary>
	[Flags]
	public enum RunBeliefZones { None = 0, Hand = 1, Deck = 2, HandAndDeck = Hand | Deck }

	/// <summary>
	/// One belief reset (H8): a card rewrote <see cref="ControllerId"/>'s hidden <see cref="Zones"/> so that what an opponent
	/// learned about those cards before no longer holds. <see cref="EntityIds"/> are the zone's cards at the reset.
	/// </summary>
	public sealed class RunBeliefReset
	{
		public int ControllerId { get; }
		public RunBeliefZones Zones { get; }
		public int Turn { get; }
		/// <summary>The game's history record count at the reset: a public fact with a higher sequence is a newer observation.</summary>
		public long HistorySequence { get; }
		public IReadOnlyList<int> EntityIds { get; }
		internal RunBeliefReset(int controller, RunBeliefZones zones, int turn, long sequence, IEnumerable<int> ids)
		{
			ControllerId = controller; Zones = zones; Turn = turn; HistorySequence = sequence; EntityIds = Array.AsReadOnly(ids.ToArray());
		}
	}

	/// <summary>
	/// The shared helpers phase-6 card families use to read the run and to report back to it (design section 9: H1 run context
	/// reads, H7 run edits and slot logs, H8 belief resets). Not a framework: each is one call over <see cref="RunContext"/>,
	/// <see cref="Game.RunResult"/> or <see cref="Game.RunBeliefResets"/>.
	/// </summary>
	public static class RunHelpers
	{
		// ---------------------------------------------------------------- H1 run context reads

		/// <summary>The run context of the game, never null (<see cref="RunContext.Empty"/> outside a run match).</summary>
		public static RunContext Context(Game game) => game?.RunContextOrNull ?? RunContext.Empty;

		/// <summary>Bosses the run has defeated before this fight.</summary>
		public static int Bosses(Game game) => Context(game).BossesDefeated;

		/// <summary>
		/// The classes a class Discover of <paramref name="c"/>'s draws from: its hero's class, and for the run player (Player1 of
		/// a run match) every class of the run row too (<see cref="RunContext.PlayerClasses"/>: both classes of a dual row).
		/// </summary>
		public static IReadOnlyList<CardClass> RunPlayerClasses(Controller c)
		{
			var classes = new List<CardClass> { c.HeroClass };
			if (c.Game != null && c == c.Game.Player1)
				foreach (var x in Context(c.Game).PlayerClasses) if (!classes.Contains(x)) classes.Add(x);
			return classes;
		}

		/// <summary>The one-fight bonus granted for this fight, or null.</summary>
		public static RunNextFight NextFight(Game game) => Context(game).NextFight;

		/// <summary>
		/// The run's treasure-Discover pool (H2): <see cref="RunContext.TreasurePool"/>, narrowed to the adventure tag
		/// <paramref name="pool"/> when one is given (null: every adventure). Ordinal order; empty outside a run match.
		/// </summary>
		public static IReadOnlyList<string> TreasurePool(Game game, string pool = null)
		{
			var context = Context(game);
			if (pool == null) return context.TreasurePool;
			return context.TreasurePool.Where(id => context.TreasurePoolTags.TryGetValue(id, out var tag) && tag == pool).ToArray();
		}

		/// <summary>
		/// The run deck slot of <paramref name="entity"/> (its index in <see cref="RunMatchPlan.PlayerDeck"/>), or -1 when the
		/// entity is not one of the run player's dealt deck cards (a generated card, a boss card).
		/// </summary>
		public static int SlotOf(IPlayable entity)
		{
			if (entity?.Game == null) return -1;
			var ids = entity.Game.Player1DeckSlotIds;
			for (int i = 0; i < ids.Count; i++) if (ids[i] == entity.Id) return i;
			return -1;
		}

		/// <summary>The run log of <paramref name="entity"/>'s slot (empty when it has none or is not a run deck card).</summary>
		public static IReadOnlyList<string> SlotLog(IPlayable entity)
		{
			int slot = SlotOf(entity);
			var logs = Context(entity?.Game).SlotLogs;
			return slot >= 0 && slot < logs.Count ? logs[slot] : Array.Empty<string>();
		}

		/// <summary>
		/// The run log of <paramref name="entity"/>'s slot "this run": the kept log (<see cref="SlotLog"/>, earlier won fights)
		/// followed by the lines this match appended so far (<see cref="RunMatchResult.SlotLogAppends"/>), oldest first (DALA_719).
		/// </summary>
		public static IReadOnlyList<string> SlotLogThisRun(IPlayable entity)
		{
			int slot = SlotOf(entity);
			if (slot < 0) return Array.Empty<string>();
			var lines = new List<string>(SlotLog(entity));
			var pending = entity.Game.RunResult?.SlotLogAppends;
			if (pending != null) lines.AddRange(pending.Where(p => p.Key == slot).Select(p => p.Value));
			return lines;
		}

		/// <summary>The run deck origin of <paramref name="entity"/>'s slot ("starter", "draft", ...), or null.</summary>
		public static string SlotOrigin(IPlayable entity)
		{
			int slot = SlotOf(entity);
			var origins = Context(entity?.Game).RunDeckOrigins;
			return slot >= 0 && slot < origins.Count ? origins[slot] : null;
		}

		// ---------------------------------------------------------------- H7 run edits and slot logs

		/// <summary>The real game's result to fill (created on first use).</summary>
		private static RunMatchResult Result(Game game) => game.RunResult ?? (game.RunResult = new RunMatchResult());

		/// <summary>Adds <paramref name="cardId"/> to the run deck after a won fight (LOOTA_805, LOOTA_813).</summary>
		public static void EditAdd(Game game, string cardId) => Result(game).Add(new RunEdit(RunEdit.OpAdd, -1, cardId));

		/// <summary>Replaces the run deck card of <paramref name="entity"/>'s slot after a won fight; false when it has no slot.</summary>
		public static bool EditReplace(IPlayable entity, string cardId)
		{
			int slot = SlotOf(entity);
			if (slot < 0) return false;
			Result(entity.Game).Add(new RunEdit(RunEdit.OpReplace, slot, cardId));
			return true;
		}

		/// <summary>
		/// Replaces the run deck card of slot <paramref name="slot"/> (an index into <see cref="RunContext.RunDeckIds"/>) after a
		/// won fight, wherever that card is in the match (DALA_721 replaces every minion of the run deck); false when the run
		/// deck has no such slot.
		/// </summary>
		public static bool EditReplaceSlot(Game game, int slot, string cardId)
		{
			if (game == null || slot < 0 || slot >= Context(game).RunDeckIds.Count) return false;
			Result(game).Add(new RunEdit(RunEdit.OpReplace, slot, cardId));
			return true;
		}

		/// <summary>Removes the run deck card of <paramref name="entity"/>'s slot after a won fight; false when it has no slot.</summary>
		public static bool EditRemove(IPlayable entity)
		{
			int slot = SlotOf(entity);
			if (slot < 0) return false;
			Result(entity.Game).Add(new RunEdit(RunEdit.OpRemove, slot, null));
			return true;
		}

		/// <summary>Appends <paramref name="line"/> to <paramref name="entity"/>'s slot log (DALA_718, DALA_719); false when it has no slot.</summary>
		public static bool AppendSlotLog(IPlayable entity, string line)
		{
			int slot = SlotOf(entity);
			if (slot < 0) return false;
			Result(entity.Game).AddLog(slot, line);
			return true;
		}

		/// <summary>The run's hero power becomes <paramref name="powerId"/> for the rest of the run (ULDA_306).</summary>
		public static void OverridePower(Game game, string powerId) =>
			Result(game).PowerOverride = powerId ?? throw new ArgumentNullException(nameof(powerId));

		/// <summary>The next fight starts with these bonuses (ULDA_504); a second call in one match replaces the first.</summary>
		public static void GrantNextFight(Game game, int mana, int draws) => Result(game).NextFight = new RunNextFight(mana, draws);

		// ---------------------------------------------------------------- H8 belief resets

		/// <summary>
		/// Records that <paramref name="controller"/>'s hidden <paramref name="zones"/> were rewritten (LOOTA_820, DALA_709):
		/// the fair search (FairBotState) drops what the opponent learned about the cards there before this point.
		/// </summary>
		public static void BeliefReset(Controller controller, RunBeliefZones zones)
		{
			if (controller == null) throw new ArgumentNullException(nameof(controller));
			if (zones == RunBeliefZones.None) return;
			var ids = new List<int>();
			if ((zones & RunBeliefZones.Hand) != 0) ids.AddRange(controller.HandZone.Select(c => c.Id));
			if ((zones & RunBeliefZones.Deck) != 0) ids.AddRange(controller.DeckZone.Select(c => c.Id));
			controller.Game.AddBeliefReset(new RunBeliefReset(controller.PlayerId, zones, controller.Game.Turn, controller.Game.HistoryFacts.RecordCount, ids));
		}
	}
}

namespace SabberStoneCore.Model
{
	using SabberStoneCore.CardSets.DungeonRun;

	public partial class Game
	{
		/// <summary>
		/// What this match asks the Dungeon Run layer to keep (<see cref="RunHelpers"/> fills it); null when nothing was asked.
		/// A clone copies it, so a simulation never writes into the real game's result. Not part of the snapshot schema
		/// (NonSerialized, so the snapshot fingerprint is unchanged): the run layer harvests it from the live game at its end.
		/// </summary>
		[field: NonSerialized]
		public RunMatchResult RunResult { get; internal set; }

		// Not part of the snapshot schema either: a fair view reads the live game's resets (FairBotState.ForgetResetHand).
		[NonSerialized]
		private List<RunBeliefReset> _runBeliefResets;

		/// <summary>The belief resets (<see cref="RunHelpers.BeliefReset"/>) of this match, in order.</summary>
		public IReadOnlyList<RunBeliefReset> RunBeliefResets => _runBeliefResets ?? (IReadOnlyList<RunBeliefReset>)Array.Empty<RunBeliefReset>();

		internal void AddBeliefReset(RunBeliefReset reset) => (_runBeliefResets ?? (_runBeliefResets = new List<RunBeliefReset>())).Add(reset);

		internal RunContext RunContextOrNull => _gameConfig?.RunContext;

		// The copy constructor's share of the run state: the result is copied, the resets (immutable records) are shared by value.
		private void CopyRunState(Game game)
		{
			RunResult = game.RunResult?.Clone();
			_runBeliefResets = game._runBeliefResets == null ? null : new List<RunBeliefReset>(game._runBeliefResets);
			_runDeckFacts = game._runDeckFacts == null ? null : new List<RunDeckFact>(game._runDeckFacts);
		}
	}
}
