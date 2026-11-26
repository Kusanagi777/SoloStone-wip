using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Model;

// Build Deck editor mode (design 2026-11-26-dungeon-run-build-deck-editor-design.md): the pure decisions the client side
// (NativeDungeonRunEditor and its hooks) calls. No client, LocalProfile or LocalCardCatalog types: verify suites compile it alone.
internal static class DungeonRunEditor
{
    // ---- A1: the temporary deck id band (never persisted; disjoint from loaners 900000001..900000225 and personal ids 910000001+)
    internal const long FirstRunBuildId = 900000300, LastRunBuildId = 900000399;

    internal static bool IsRunBuildId(long id) => id >= FirstRunBuildId && id <= LastRunBuildId;

    /// <summary>The lowest free band id, or null when the band is full.</summary>
    internal static long? NextRunBuildId(IEnumerable<long> nativeIds)
    {
        var used = new HashSet<long>(nativeIds ?? Enumerable.Empty<long>());
        for (long id = FirstRunBuildId; id <= LastRunBuildId; id++)
            if (!used.Contains(id)) return id;
        return null;
    }

    /// <summary>The ids outside the band (B2: the count DecksReady compares with the session's decks).</summary>
    internal static int NonBandCount(IEnumerable<long> ids) => ids == null ? 0 : ids.Count(id => !IsRunBuildId(id));

    // ---- flow state
    internal enum Stage { None, Creating, Entering, Editing, Leaving }
    internal enum Leave { Done, Back, Recover }
    internal enum AddVerdictKind { Allow, Rule, Refuse }
    internal enum LeaveStep { Power, Deck }
    internal enum PowerBackTarget { Editor, PlankBuild, Deck }
    internal enum RecoverKind { None, RemoveAndPlank, DeckAndLeave }
    internal enum BuilderKind { Editor, Plank }

    /// <summary>Phase gate (design M): false in the phase-3 build; true from phase 4, where dual-class (Tombs) and Death Knight rows
    /// use the editor (F, G). If F's or G's PC checks (P12, P13) fail, false puts those rows back on the plank.</summary>
    internal static readonly bool DualAndDkInEditor = true;

    /// <summary>D3: Creating (seeding) allows, Editing applies the run rules, every other stage refuses.</summary>
    internal static AddVerdictKind AddVerdict(Stage stage) =>
        stage == Stage.Creating ? AddVerdictKind.Allow : stage == Stage.Editing ? AddVerdictKind.Rule : AddVerdictKind.Refuse;

    /// <summary>J1/K: Done goes on to the Power step; Back and Recover return to the Deck step.</summary>
    internal static LeaveStep LeaveTarget(Leave leave) => leave == Leave.Done ? LeaveStep.Power : LeaveStep.Deck;

    /// <summary>J2: Back on the Power step.</summary>
    internal static PowerBackTarget PowerBack(bool builtByEditor, bool hasList, bool editorAvailable)
    {
        if (!hasList) return PowerBackTarget.Deck;
        return builtByEditor && editorAvailable ? PowerBackTarget.Editor : PowerBackTarget.PlankBuild;
    }

    /// <summary>L: what a failure does. A second failure in a session, or one with no session, takes the normal Blocked path.</summary>
    internal static RecoverKind RecoverAction(Stage stage, bool atHub, bool recoveredOnce)
    {
        if (recoveredOnce || stage == Stage.None) return RecoverKind.None;
        if (stage == Stage.Creating || stage == Stage.Entering && atHub) return RecoverKind.RemoveAndPlank;
        return RecoverKind.DeckAndLeave;
    }

    /// <summary>M: the editor, or the plank when any fallback condition holds.</summary>
    internal static BuilderKind Builder(DungeonRunClass row, int candidates, int reachable, int size, int nativeDecks, int nativeLimit,
        bool editorFailed, bool dualAndDkInEditor)
    {
        if (editorFailed) return BuilderKind.Plank;
        if (candidates == 0 || reachable < size) return BuilderKind.Plank;
        if (nativeDecks >= nativeLimit) return BuilderKind.Plank;
        if (!dualAndDkInEditor && row != null && (row.IsDual || row.ClassSet.Contains("DEATHKNIGHT"))) return BuilderKind.Plank;
        return BuilderKind.Editor;
    }

    /// <summary>K "Belt": the native Exit gate; the mirror of NativeMenuFlow.CollectionExitAllowed.</summary>
    internal static bool ExitAllowed(bool loggedIn, bool localActive, bool pickerOrigin, bool runBuildLeaving) =>
        loggedIn || localActive && (pickerOrigin || runBuildLeaving);

    // ---- A2, F: heroes and classes
    /// <summary>F1: the row's class tags (engine CardClass names), in order.</summary>
    internal static IReadOnlyList<string> ClassTags(DungeonRunClass row) => row.ClassSet;

    /// <summary>A2: client ids of the header hero candidates: the row's own hero, then the first class's hero, duplicates removed.</summary>
    internal static IReadOnlyList<string> HeaderHeroes(DungeonRunClass row, DungeonRunData data, Func<string, string> clientId)
    {
        var heroes = new List<string>();
        void Add(string card)
        {
            if (string.IsNullOrEmpty(card)) return;
            string id = clientId(card);
            if (!string.IsNullOrEmpty(id) && !heroes.Contains(id)) heroes.Add(id);
        }
        Add(row.HeroCard);
        if (row.ClassSet.Count > 0) Add(data.FindClass(row.ClassSet[0])?.HeroCard);
        return heroes;
    }

    /// <summary>A2: the client accepted the header hero for the row (its class is the primary class; its hero classes are within the row's).</summary>
    internal static bool HeroAccepted(string classTag, IReadOnlyList<string> heroClassTags, DungeonRunClass row) =>
        row.ClassSet.Count > 0 && classTag == row.ClassSet[0] && heroClassTags != null && heroClassTags.All(t => row.ClassSet.Contains(t));

    /// <summary>H0: the same conjunction as NativeUIProjection's OwnsPersonalDeckEditor, with the run-build term in place of "persisted".</summary>
    internal static bool OwnsEditor(bool active, bool currentCollection, bool manager, bool tray, bool mainContext, bool exactDeck,
        bool exactInstance, bool runBuild) =>
        active && currentCollection && manager && tray && mainContext && exactDeck && exactInstance && runBuild;

    // ---- E: client -> engine mapping, add and Done reasons, seed
    /// <summary>
    /// E1: the editor's candidates (one <see cref="RunBuildCard"/> per engine id, never merged planks) seen from the client: a client
    /// card id maps to the lowest engine id by ordinal that carries it. Engine ids that share a client id are listed in
    /// <see cref="Collisions"/> (as client ids); the losing engine id is still a candidate for <see cref="Owned"/>, <see cref="Group"/>
    /// and <see cref="Name"/>.
    /// </summary>
    internal sealed class EditorCards
    {
        private readonly Dictionary<string, RunBuildCard> _byEngine = new Dictionary<string, RunBuildCard>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _toEngine = new Dictionary<string, string>(StringComparer.Ordinal);

        internal EditorCards(IReadOnlyList<RunBuildCard> candidates)
        {
            var collisions = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var card in candidates ?? new RunBuildCard[0])
            {
                if (card == null || string.IsNullOrEmpty(card.Id) || _byEngine.ContainsKey(card.Id)) continue;
                _byEngine[card.Id] = card;
                string client = string.IsNullOrEmpty(card.ClientId) ? card.Id : card.ClientId;
                if (!_toEngine.TryGetValue(client, out var known)) _toEngine[client] = card.Id;
                else
                {
                    collisions.Add(client);
                    if (string.CompareOrdinal(card.Id, known) < 0) _toEngine[client] = card.Id;
                }
            }
            Collisions = collisions.ToList();
        }

        /// <summary>Client ids that more than one engine id carries (sorted).</summary>
        public IReadOnlyList<string> Collisions { get; }

        /// <summary>The lowest engine id (ordinal) with this client id, or null when it is not a candidate.</summary>
        public string Engine(string clientId) => clientId != null && _toEngine.TryGetValue(clientId, out var id) ? id : null;

        /// <summary>The client id of a candidate engine id, or null.</summary>
        public string ClientId(string engineId)
        {
            var card = Find(engineId);
            return card == null ? null : string.IsNullOrEmpty(card.ClientId) ? card.Id : card.ClientId;
        }

        /// <summary>Copies owned of a candidate (0 for any other id): the plank's BuildOwned.</summary>
        public int Owned(string engineId) => Find(engineId)?.Owned ?? 0;

        /// <summary>The copy group of a candidate, else the id itself: the plank's BuildGroup.</summary>
        public string Group(string engineId) => Find(engineId)?.CopyGroup ?? engineId;

        /// <summary>The name of a candidate, or null.</summary>
        public string Name(string engineId) => Find(engineId)?.Name;

        /// <summary>The engine ids of the client slots, in order, without the slots that are not candidates (their client ids go to <paramref name="unmapped"/>).</summary>
        public IReadOnlyList<string> EngineList(IEnumerable<string> clientSlots, out IReadOnlyList<string> unmapped)
        {
            var list = new List<string>();
            var missing = new List<string>();
            foreach (string slot in clientSlots ?? Enumerable.Empty<string>())
            {
                string engine = Engine(slot);
                if (engine != null) list.Add(engine); else missing.Add(slot);
            }
            unmapped = missing;
            return list;
        }

        internal bool IsCandidate(string engineId) => Find(engineId) != null;

        private RunBuildCard Find(string engineId) => engineId != null && _byEngine.TryGetValue(engineId, out var card) ? card : null;
    }

    /// <summary>
    /// E2: why the client's add of <paramref name="clientId"/> is refused: (code, engine id), or (null, engine id) when it is legal.
    /// A candidate gets <see cref="DungeonRunBuild.AddReason"/>'s code. Any other card: "size" on a full deck, else the card's own
    /// <see cref="DungeonRunBuild.CardReason"/> code and id ("class AT_064", the engine id being the client id), else "owned &lt;id&gt;"
    /// (legal but no copy owned); an id the engine does not know is "unavailable &lt;id&gt;" with a null engine id.
    /// <paramref name="cardReason"/> null computes <see cref="DungeonRunBuild.CardReason"/> per call.
    /// </summary>
    internal static (string Code, string EngineId) AddCode(EditorCards cards, IReadOnlyList<string> clientSlots, string clientId, DungeonRunClass row,
        int size, OfflinePoolPolicy policy, Func<string, string> cardReason)
    {
        string engine = cards.Engine(clientId);
        if (engine != null)
        {
            var list = cards.EngineList(clientSlots, out _);
            return (DungeonRunBuild.AddReason(list, engine, row.ClassSet, size, cards.Owned, cards.Group, policy, cardReason), engine);
        }
        if (clientSlots != null && clientSlots.Count >= size) return ("size", null);
        bool known;
        try { known = clientId != null && SabberStoneCore.Model.Cards.FromId(clientId) != null; } catch (Exception) { known = false; }
        if (!known) return ("unavailable " + clientId, null);
        string reason = cardReason != null ? cardReason(clientId) : DungeonRunBuild.CardReason(clientId, row.ClassSet, policy);
        return (reason != null ? reason + " " + clientId : "owned " + clientId, clientId);
    }

    /// <summary>E2/I: every reason the editor's deck is not ready: <see cref="DungeonRunBuild.DeckReasons"/> over the mapped slots, plus "unavailable &lt;id&gt;" per unmapped slot.</summary>
    internal static IReadOnlyList<string> DoneReasons(EditorCards cards, IReadOnlyList<string> clientSlots, DungeonRunClass row, int size,
        OfflinePoolPolicy policy, Func<string, string> cardReason)
    {
        var list = cards.EngineList(clientSlots, out var unmapped);
        var reasons = DungeonRunBuild.DeckReasons(list, row.ClassSet, size, cards.Owned, cards.Group, policy, cardReason).ToList();
        reasons.AddRange(unmapped.Select(id => "unavailable " + id));
        return reasons;
    }

    /// <summary>C "Seed": client card id to copy count of an engine list (the re-open fills the tray from it). Throws <see cref="ArgumentException"/> on an id that is not a candidate.</summary>
    internal static IReadOnlyDictionary<string, int> SeedCounts(IReadOnlyList<string> engineList, EditorCards cards)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (string id in engineList)
        {
            string client = cards.ClientId(id);
            if (client == null) throw new ArgumentException("not a candidate: " + id, nameof(engineList));
            counts[client] = counts.TryGetValue(client, out var n) ? n + 1 : 1;
        }
        return counts;
    }
}
