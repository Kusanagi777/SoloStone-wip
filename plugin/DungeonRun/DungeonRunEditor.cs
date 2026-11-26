using System;
using System.Collections.Generic;
using System.Linq;

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

    /// <summary>Phase gate (design M): false in the phase-3 build; Task 6 sets it to true after F's and G's PC checks pass.</summary>
    internal static readonly bool DualAndDkInEditor = false;

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
}
