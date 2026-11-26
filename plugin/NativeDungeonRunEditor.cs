using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Logging;
using HarmonyLib;
using PegasusShared;
using UnityEngine;

// Build Deck editor mode (design docs/superpowers/specs/2026-11-26-dungeon-run-build-deck-editor-design.md, A-C, I-M): the
// client's Collection Manager edits a temporary, session-only native deck (ids DungeonRunEditor.FirstRunBuildId..Last) for a
// Dungeon Run starter. The deck is created at the HUB (OnDeckCreated, never in CM), CM opens on it in card view, Done hands the
// list to the run menu's Power step and Back returns to its Deck step; both leave CM through the native Exit and remove the deck
// at the HUB (CollectionManager.RemoveDeck). Nothing is persisted: every SendChanges of a band id is swallowed (NativeDeckEditor
// SaveLocalDraft). Any failure recovers once per session (DungeonRunEditor.RecoverAction): to the plank Build step while still at
// the HUB, else to the Deck step after leaving CM; after that Build Deck uses the plank until restart (editorFailed).
// The pure decisions live in DungeonRunEditor. Every hook body is guarded and never throws into Harmony; a failure schedules the
// recovery on the next tick (Abort). Diagnostics are LogWarning lines prefixed DUNGEON_RUN_EDITOR.
internal static class NativeDungeonRunEditor
{
    // O5: CM must hold still this long before ShowDeck; O7: ShowDeck has this long to open the editor; O5 timeout.
    private const float EnterSettle = 1, VerifyWindow = 10, EnterWindow = 60;
    // K: after Exit, Navigate(HUB) once past HubFallback; past ExitWindow the leave fails (Recover).
    private const float HubFallback = 20, ExitWindow = 90, IdleSweepEvery = 2;

    private sealed class EditorSession
    {
        internal string ClassId, RowId, DeckId;
        internal DungeonRunClass Row;
        internal IReadOnlyList<string> Seed;               // engine ids of a re-open (J2), else null
        internal IReadOnlyDictionary<string, int> SeedCounts; // client id -> copies of Seed
        internal RuneType[] SeedRunes;                     // a Death Knight re-open's header and template runes, else null
        internal DungeonRunEditor.EditorCards Cards;
        internal SabberStoneCore.Model.OfflinePoolPolicy Policy;
        internal Func<string, string> CardReason;           // DungeonRunBuild.CardReason, memoized for the session
        internal int Size;
        internal long TempId;
        internal IReadOnlyList<string> Heroes;              // A2: header hero candidates (client ids), in order
        internal string ClassHero, Hero;
        internal DungeonRunEditor.Stage Stage;
        internal DungeonRunEditor.Leave Leave;
        internal float StageSince, ReadySince = -1, ShownAt, ExitedAt;
        internal bool Shown, EditEnded, Exited, ForcedHub, RecoveredOnce;
    }

    private static ManualLogSource log;
    private static EditorSession session;
    private static Exception pendingFailure;
    // L: process-level; after any editor recovery Build Deck uses the plank until restart.
    private static bool editorFailed;
    private static float refusalSince, lastIdleSweep;
    private static readonly HashSet<string> swallowedLogged = new HashSet<string>(StringComparer.Ordinal);

    private static MethodInfo onDeckCreated, removeDeck, doneEditing, deckClass, deckHeroClasses, showDeck, exitDisplay,
        saveAndList, navigationClear;

    // ------------------------------------------------------------------ what Tasks 4-5 and the wiring read

    /// <summary>A session exists (from Begin until the temporary deck is removed).</summary>
    internal static bool Active => session != null;
    /// <summary>K1 L1: Done or Back was taken (or a recovery leaves CM): every hook is inert; NativeMenuFlow's Exit gate admits the leave.</summary>
    internal static bool Leaving => session?.Stage == DungeonRunEditor.Stage.Leaving;
    /// <summary>L: an editor session failed since the client started; Build Deck uses the plank.</summary>
    internal static bool EditorFailed => editorFailed;
    /// <summary>J2: Back on the Power step may re-open the editor (no failure latched, no session still leaving).</summary>
    internal static bool Available => !editorFailed && session == null && removeDeck != null;
    /// <summary>The temporary deck's id, or 0 with no session.</summary>
    internal static long TempId => session?.TempId ?? 0;
    internal static DungeonRunEditor.EditorCards CurrentCards => session?.Cards;
    internal static DungeonRunClass CurrentRow => session?.Row;
    internal static DungeonRunEditor.Stage CurrentStage => session?.Stage ?? DungeonRunEditor.Stage.None;
    /// <summary>The session's memoized DungeonRunBuild.CardReason (engine or client id -> code, or null), or null with no session.</summary>
    internal static Func<string, string> CardReason => session?.CardReason;
    /// <summary>The refusal popup's debounce (Done here, the add refusals in Task 4): set while one is shown.</summary>
    internal static bool RefusalOpen { get; set; }

    /// <summary>The design preamble's ownership test: the live session's temporary deck.</summary>
    internal static bool Owns(long id)
    {
        var s = session;
        return s != null && DungeonRunEditor.IsRunBuildId(id) && id == s.TempId;
    }

    // ------------------------------------------------------------------ setup

    internal static void Initialize(Harmony harmony, ManualLogSource logger)
    {
        log = logger;
        onDeckCreated = Required(typeof(CollectionManager), "OnDeckCreated", new[] { typeof(NetCache.DeckHeader), typeof(int?) });
        removeDeck = Required(typeof(CollectionManager), "RemoveDeck", new[] { typeof(long) });
        doneEditing = Required(typeof(CollectionManager), "DoneEditing", Type.EmptyTypes);
        deckClass = Required(typeof(CollectionDeck), "GetClass", Type.EmptyTypes);
        deckHeroClasses = Required(typeof(CollectionDeck), "GetHeroClasses", Type.EmptyTypes);
        showDeck = Required(typeof(CollectionManagerDisplay), "ShowDeck",
            new[] { typeof(long), typeof(bool), typeof(bool), typeof(CollectionUtils.ViewMode?) });
        exitDisplay = Required(typeof(CollectionManagerDisplay), "Exit", Type.EmptyTypes);
        saveAndList = Required(typeof(CollectionDeckTray), "SaveCurrentDeckAndEnterDeckListMode", Type.EmptyTypes);
        navigationClear = Required(typeof(Navigation), "Clear", Type.EmptyTypes);
        // I: the only member that tells Done from Back (native Done never validates or saves; it ends in Navigation.GoBack).
        Patch(harmony, Required(typeof(CollectionDeckTray), "DoneButtonPress", new[] { typeof(UIEvent) }), nameof(RunBuildDone));
    }

    private static MethodInfo Required(Type type, string name, Type[] args) =>
        AccessTools.Method(type, name, args) ?? throw new MissingMethodException(type.FullName, name);
    private static void Patch(Harmony harmony, MethodInfo target, string prefix) =>
        harmony.Patch(target, prefix: new HarmonyMethod(typeof(NativeDungeonRunEditor), prefix));

    private static void Log(string message) => log?.LogWarning("DUNGEON_RUN_EDITOR " + message);
    private static float Now => Time.realtimeSinceStartup;
    private static bool AtHub => SceneMgr.IsInitialized() && SceneMgr.Get().GetMode() == SceneMgr.Mode.HUB;
    private static bool AtStableHub => AtHub && !SceneMgr.Get().IsTransitionNowOrPending();
    private static bool InCollection => SceneMgr.IsInitialized() && SceneMgr.Get() != null
        && SceneMgr.Get().GetMode() == SceneMgr.Mode.COLLECTIONMANAGER;
    private static void Set(EditorSession s, DungeonRunEditor.Stage next)
    {
        if (s.Stage != next) Log("stage " + s.TempId + " " + s.Stage + " -> " + next);
        s.Stage = next; s.StageSince = Now;
    }

    // ------------------------------------------------------------------ begin (C O0-O4 and Seed; at the HUB only)

    /// <summary>
    /// Opens the editor on a temporary deck for the run row (the menu's Build Deck, the native picker's Build Deck hand-off, or
    /// Power Back after an editor Done with <paramref name="seed"/> = the list). False when the editor is not used (M's plank
    /// conditions, a precondition of O0, a seed that is not this row's candidates, or no header hero accepted): nothing was left
    /// behind and the caller opens the plank. True when the session took over: the caller closes the menu; it reopens on Power
    /// (Done), Deck (Back) or, after a failure, the plank Build step with a caption.
    /// </summary>
    internal static bool Begin(string classId, string rowId, string deckId, IReadOnlyList<string> seed)
    {
        if (session != null) { Log("begin refused: a session is " + session.Stage); return false; }
        if (removeDeck == null) { Log("begin refused: not initialized"); return false; }
        EditorSession s;
        try { s = Prepare(classId, rowId, deckId, seed); }
        catch (Exception error) { Log("begin refused: " + error); return false; }
        if (s == null) return false;
        session = s;
        try
        {
            if (!Create(s))
            {
                End("no header hero accepted for " + rowId + " (" + string.Join(",", s.Heroes) + ")");
                return false;
            }
            if (pendingFailure != null) return true;
            if (s.SeedCounts != null) SeedDecks(s);
            if (pendingFailure != null) return true;
            Enter(s);
        }
        catch (Exception error) { Abort(error); }
        return true;
    }

    // O0-O1: preconditions, who builds (M) and the session, before anything native changes. Null: the plank.
    private static EditorSession Prepare(string classId, string rowId, string deckId, IReadOnlyList<string> seed)
    {
        // O0. The run menu is open over the HUB when the menu routes here, so the menu-closed terms of ReopenReady cannot hold;
        // the HUB terms do (and Navigate refuses outside them).
        if (!NativeUIProbe.Active || !AtStableHub || NativeUIProbe.SceneWaiting) { Log("begin: plank, the HUB is not idle"); return null; }
        var rules = DungeonRunRuntime.Rules;
        var row = rules?.Data.FindClass(rowId);
        var manager = CollectionManager.Get();
        if (row == null || row.ClassSet.Count == 0 || manager == null) { Log("begin: plank, no row " + rowId + " or no collection"); return null; }
        Sweep("before an editor session");
        if (manager.GetDecks().Keys.Any(DungeonRunEditor.IsRunBuildId)) { Log("begin: plank, a band deck is still in the collection"); return null; }
        var candidates = DungeonRunRuntime.BuildCandidates(rowId);
        int size = rules.Rules.StarterSize;
        int reachable = NativeDungeonRunText.BuildReachable(candidates, null);
        int nativeDecks = manager.GetDecks().Count;
        var builder = DungeonRunEditor.Builder(row, candidates.Count, reachable, size, nativeDecks, NativeUIProjection.NativeDeckLimit,
            editorFailed, DungeonRunEditor.DualAndDkInEditor);
        if (builder != DungeonRunEditor.BuilderKind.Editor)
        {
            Log("begin: plank for " + rowId + " (candidates " + candidates.Count + ", reachable " + reachable + "/" + size + ", decks "
                + nativeDecks + "/" + NativeUIProjection.NativeDeckLimit + ", failed " + editorFailed + ", dual " + row.IsDual + ")");
            return null;
        }
        long? id = DungeonRunEditor.NextRunBuildId(manager.GetDecks().Keys);
        if (id == null) { Log("begin: plank, the run-build id band is full"); return null; }
        var cards = new DungeonRunEditor.EditorCards(candidates);
        IReadOnlyDictionary<string, int> counts = null;
        if (seed != null)
        {
            try { counts = DungeonRunEditor.SeedCounts(seed, cards); }
            catch (ArgumentException error) { Log("begin: plank, the list is not this row's candidates: " + error.Message); return null; }
        }
        var heroes = DungeonRunEditor.HeaderHeroes(row, rules.Data, DungeonRunRuntime.ClientCardId);
        string classHero = DungeonRunRuntime.ClientCardId(rules.Data.FindClass(row.ClassSet[0])?.HeroCard);
        if (heroes.Count == 0 || string.IsNullOrEmpty(classHero)) { Log("begin: plank, no header hero for " + rowId); return null; }
        var policy = SabberStoneCore.Model.OfflinePoolPolicy.Load();
        var classSet = row.ClassSet;
        var memo = new Dictionary<string, string>(StringComparer.Ordinal);
        Func<string, string> cardReason = card =>
        {
            if (card == null) return DungeonRunBuild.CardReason(card, classSet, policy);
            if (!memo.TryGetValue(card, out string reason)) memo[card] = reason = DungeonRunBuild.CardReason(card, classSet, policy);
            return reason;
        };
        if (cards.Collisions.Count > 0) Log("begin: client ids shared by several engine ids (the lowest maps): " + string.Join(",", cards.Collisions));
        return new EditorSession
        {
            ClassId = classId, RowId = rowId, DeckId = deckId, Row = row, Seed = seed?.ToList(), SeedCounts = counts,
            // A2: a Death Knight re-open carries its runes on the header and the template, whatever the header hero is.
            SeedRunes = seed != null && row.ClassSet.Contains("DEATHKNIGHT") ? NativeDeckRunes.Order("HERO_11", seed) : null,
            Cards = cards, Policy = policy, CardReason = cardReason, Size = size, TempId = id.Value, Heroes = heroes,
            ClassHero = classHero, Stage = DungeonRunEditor.Stage.Creating, StageSince = Now,
        };
    }

    // O2-O3 and A2's hero test: the header (named hero first, then the class hero), OnDeckCreated in the HUB, both decks marked
    // loaded, the NetCache asserts (A4). A rejected hero's deck is removed and the next hero tried. False: none was accepted.
    private static bool Create(EditorSession s)
    {
        var manager = CollectionManager.Get() ?? throw new InvalidOperationException("Native collection unavailable");
        foreach (string hero in s.Heroes)
        {
            if (DefLoader.Get()?.GetEntityDef(hero) == null) { Log("hero " + hero + " skipped: no EntityDef"); continue; }
            string name = NativeDungeonRunText.BuildDeckName;
            // The 5-arg header: NORMAL_DECK, FT_STANDARD, source NORMAL; ranked past every real deck (nothing ranks it).
            var header = NativeUIProbe.ExpectedHeader(s.TempId, new LocalDeck(name, hero, new string[0]), name,
                NativeUIProjection.NativeDeckLimit + 1, null);
            // ExpectedHeader writes hero + "bp", which is no card for a named hero; the class hero's power is (P11).
            header.HeroPower = s.ClassHero + "bp";
            if (s.SeedRunes != null) { header.Rune1 = s.SeedRunes[0]; header.Rune2 = s.SeedRunes[1]; header.Rune3 = s.SeedRunes[2]; }
            onDeckCreated.Invoke(manager, new object[] { header, null });
            CollectionDeck working = manager.GetDeck(s.TempId), baseDeck = manager.GetBaseDeck(s.TempId);
            if (working == null || baseDeck == null)
                throw new InvalidOperationException("OnDeckCreated did not project deck " + s.TempId + " (working " + (working != null) + ", base " + (baseDeck != null) + ")");
            foreach (var native in new[] { working, baseDeck }.Distinct()) native.MarkNetworkContentsLoaded();
            if (pendingFailure != null) return true;
            var cls = (TAG_CLASS)deckClass.Invoke(working, null);
            var heroClasses = ((deckHeroClasses.Invoke(working, null) as List<TAG_CLASS>) ?? new List<TAG_CLASS>()).Select(c => c.ToString()).ToList();
            bool accepted = DungeonRunEditor.HeroAccepted(cls.ToString(), heroClasses, s.Row);
            Log("created " + s.TempId + " row=" + s.RowId + " hero=" + hero + " class=" + cls + " heroClasses=[" + string.Join(",", heroClasses)
                + "] " + (accepted ? "accepted" : "rejected") + "; " + DeckState(manager, s.TempId));
            if (accepted) { s.Hero = hero; return true; }
            RemoveTemp(s, "hero " + hero + " rejected");
        }
        return false;
    }

    // C "Seed" (J2 re-open): both decks filled from a template of the list (client id -> copies), as ProjectMissingDraftCards does.
    private static void SeedDecks(EditorSession s)
    {
        var manager = CollectionManager.Get() ?? throw new InvalidOperationException("Native collection unavailable");
        var template = new CollectionManager.TemplateDeck { m_title = NativeDungeonRunText.BuildDeckName };
        if (s.SeedRunes != null) { template.m_rune1 = s.SeedRunes[0]; template.m_rune2 = s.SeedRunes[1]; template.m_rune3 = s.SeedRunes[2]; }
        foreach (var pair in s.SeedCounts) template.m_cardIds.Add(pair.Key, pair.Value);
        foreach (var native in new[] { manager.GetDeck(s.TempId), manager.GetBaseDeck(s.TempId) }.Distinct())
        {
            if (native == null) throw new InvalidOperationException("Seed: deck " + s.TempId + " is missing");
            native.FillFromTemplateDeck(template);
        }
        Log("seeded " + s.TempId + " with " + s.SeedCounts.Values.Sum() + " cards: "
            + string.Join(",", manager.GetDeck(s.TempId).GetCardsWithCardID()));
    }

    // O4: into CM the way the home Collection button goes. Navigate returns silently on refusal.
    private static void Enter(EditorSession s)
    {
        Set(s, DungeonRunEditor.Stage.Entering);
        CollectionManager.Get().NotifyOfBoxTransitionStart();
        NativeUIProbe.Navigate(SceneMgr.Mode.COLLECTIONMANAGER);
        if (!NativeUIProbe.IsLoading(SceneMgr.Mode.COLLECTIONMANAGER))
            throw new InvalidOperationException("navigation to COLLECTIONMANAGER did not start");
        Log("entering " + s.TempId);
    }

    // ------------------------------------------------------------------ per frame (NativeUIProbe.Update, after the picker)

    internal static void Update()
    {
        if (pendingFailure != null)
        {
            var error = pendingFailure;
            pendingFailure = null;
            // A failure the session cannot take (a second one) goes to NativeUIProbe.Update's catch: the normal Blocked path.
            if (!RunRecovery(error)) throw new InvalidOperationException("Build Deck editor failed again after a recovery", error);
            return;
        }
        if (session == null) { Step(IdleSweep); return; }
        Step(Tick);
    }

    private static void Step(Action step)
    {
        try { step(); }
        catch (Exception error) { Abort(error); }
    }

    // K6: a band deck at an idle HUB with no session is a failed leave's.
    private static void IdleSweep()
    {
        if (Now - lastIdleSweep < IdleSweepEvery) return;
        lastIdleSweep = Now;
        if (AtStableHub && !NativeUIProbe.SceneWaiting) Sweep("HUB idle");
    }

    private static void Tick()
    {
        var s = session;
        if (!SceneMgr.IsInitialized()) return;
        var scene = SceneMgr.Get();
        SceneMgr.Mode mode = scene.GetMode();
        bool busy = scene.IsTransitioning() || NativeUIProbe.SceneWaiting;
        switch (s.Stage)
        {
            case DungeonRunEditor.Stage.Creating:
                // Begin runs Creating to its end within one call; a tick that still sees it lost track of the deck.
                throw new InvalidOperationException("session " + s.TempId + " left in Creating");
            case DungeonRunEditor.Stage.Entering:
                TickEntering(s, mode, busy);
                break;
            case DungeonRunEditor.Stage.Editing:
                // J1: CM left by another path (a scene change) is a Back.
                if (!busy && mode != SceneMgr.Mode.COLLECTIONMANAGER)
                {
                    Log("back " + s.TempId + ": Collection left for " + mode);
                    NativeDungeonRunUI.DeckWhenHome(s.ClassId, s.RowId, s.DeckId, null);
                    s.Leave = DungeonRunEditor.Leave.Back;
                    Set(s, DungeonRunEditor.Stage.Leaving);
                }
                break;
            case DungeonRunEditor.Stage.Leaving:
                TickLeaving(s, mode, busy);
                break;
        }
    }

    // O5-O9: wait for CM to settle, ShowDeck on the temporary deck, verify edit mode, the rune indicator, Editing.
    private static void TickEntering(EditorSession s, SceneMgr.Mode mode, bool busy)
    {
        if (Now - s.StageSince > EnterWindow) throw new TimeoutException("the editor did not open in " + EnterWindow + "s (mode " + mode + ")");
        if (busy || mode != SceneMgr.Mode.COLLECTIONMANAGER) { s.ReadySince = -1; return; }
        var manager = CollectionManager.Get();
        var display = manager?.GetCollectibleDisplay() as CollectionManagerDisplay;
        var tray = CollectionDeckTray.Get();
        if (!s.Shown)
        {
            // O5. The ready flag is unknown (P3): these must hold for EnterSettle seconds.
            if (display == null || tray == null || manager.IsInEditMode()) { s.ReadySince = -1; return; }
            if (s.ReadySince < 0) { s.ReadySince = Now; return; }
            if (Now - s.ReadySince < EnterSettle) return;
            if (manager.GetDeck(s.TempId) == null) throw new InvalidOperationException("deck " + s.TempId + " is gone on entering Collection");
            // O6: the bools copy ShowCurrentEditedDeck's constants (their meaning is unknown, P2).
            showDeck.Invoke(display, new object[] { s.TempId, false, false, null });
            s.Shown = true; s.ShownAt = Now;
            return;
        }
        // O7.
        var edited = manager?.GetEditedDeck();
        bool editMode = manager != null && manager.IsInEditMode();
        bool contents = tray != null && tray.IsShowingDeckContents();
        if (editMode && edited != null && edited.ID == s.TempId && contents)
        {
            // O8: not in ShowDeckHelper's call list.
            tray.InitializeRuneIndicatorVisual(edited);
            Set(s, DungeonRunEditor.Stage.Editing);
            Log("opened " + s.TempId + " row=" + s.RowId + " hero=" + s.Hero + " cards=" + edited.GetCardsWithCardID().Count
                + (s.Seed != null ? " (re-open)" : ""));
        }
        else if (Now - s.ShownAt > VerifyWindow)
            throw new InvalidOperationException("ShowDeck did not open the editor on " + s.TempId + " (editMode " + editMode + ", edited "
                + (edited?.ID.ToString() ?? "none") + ", contents " + contents + ")");
    }

    // K L3-L6: end edit mode, then (next tick) clear Navigation and Exit natively; at the HUB remove the deck and end the session.
    private static void TickLeaving(EditorSession s, SceneMgr.Mode mode, bool busy)
    {
        if (busy) return;
        if (mode == SceneMgr.Mode.COLLECTIONMANAGER)
        {
            if (!s.EditEnded) { EndEditMode(s, true); s.EditEnded = true; return; }
            if (!s.Exited) { ExitCollection(s); s.Exited = true; s.ExitedAt = Now; return; }
            float waited = Now - s.ExitedAt;
            if (waited > ExitWindow) throw new TimeoutException("Collection was not left in " + ExitWindow + "s");
            if (waited > HubFallback && !s.ForcedHub)
            {
                s.ForcedHub = true;
                Log("leave " + s.TempId + ": still in Collection after " + HubFallback + "s, Navigate(HUB)");
                NativeUIProbe.Navigate(SceneMgr.Mode.HUB);
            }
            return;
        }
        // Out of CM (the HUB after Exit, or another scene a different path reached): the service-level CollectionManager must not
        // keep the temporary deck as its edited deck.
        if (!s.EditEnded)
        {
            s.EditEnded = true;
            try { EndEditMode(s, false); }
            catch (Exception error) { Log("leave " + s.TempId + ": DoneEditing failed: " + error.GetBaseException().Message); }
        }
        try { RemoveTemp(s, s.Leave + " at " + mode); }
        catch (Exception error) { Log("leave " + s.TempId + ": remove failed (the sweep retries): " + error.GetBaseException().Message); }
        End(s.Leave + " left Collection");
    }

    // L3: SaveCurrentDeckAndEnterDeckListMode (its SendChanges is swallowed) runs DoneEditing; DoneEditing alone on a failure or
    // outside CM.
    private static void EndEditMode(EditorSession s, bool inCollection)
    {
        var manager = CollectionManager.Get();
        if (manager == null || !manager.IsInEditMode()) return;
        if (inCollection)
        {
            try
            {
                var tray = CollectionDeckTray.Get() ?? throw new InvalidOperationException("no deck tray");
                saveAndList.Invoke(tray, null);
                return;
            }
            catch (Exception error) { Log("leave " + s.TempId + ": SaveCurrentDeckAndEnterDeckListMode failed, DoneEditing instead: " + error.GetBaseException().Message); }
        }
        else if (manager.GetEditedDeck()?.ID != s.TempId) return;
        doneEditing.Invoke(manager, null);
    }

    // L4-L5: native Exit (its teardown, the login gate admits it: NativeMenuFlow.CollectionExitAllowed with Leaving), after
    // clearing the back handlers ShowDeck and CM pushed; Navigate(HUB) when Exit throws.
    private static void ExitCollection(EditorSession s)
    {
        try { navigationClear.Invoke(null, null); }
        catch (Exception error) { Log("leave " + s.TempId + ": Navigation.Clear failed: " + error.GetBaseException().Message); }
        try
        {
            var display = CollectionManager.Get()?.GetCollectibleDisplay() as CollectionManagerDisplay
                ?? throw new InvalidOperationException("no CollectionManagerDisplay");
            Log("leaving " + s.TempId + " (" + s.Leave + "): Exit from prev mode " + SceneMgr.Get().GetPrevMode()
                + " (HUB=" + (int)SceneMgr.Mode.HUB + ")");
            exitDisplay.Invoke(display, null);
        }
        catch (Exception error)
        {
            Log("leave " + s.TempId + ": Exit failed, Navigate(HUB) instead: " + error.GetBaseException().Message);
            NativeUIProbe.Navigate(SceneMgr.Mode.HUB);
            if (!NativeUIProbe.IsLoading(SceneMgr.Mode.HUB)) Log("leave " + s.TempId + ": Navigate(HUB) did not start");
        }
    }

    // ------------------------------------------------------------------ Done (I) and Back (J1)

    // Prefix on CollectionDeckTray.DoneButtonPress(UIEvent). Always false for the temporary deck: Done never reaches native.
    private static bool RunBuildDone(CollectionDeckTray __instance)
    {
        if (!OwnsTray(__instance)) return true;
        var s = session;
        try
        {
            // Leaving (and the card-back coroutine's re-entry): inert. Before Editing: nothing to hand off yet.
            if (s.Stage != DungeonRunEditor.Stage.Editing) return false;
            var edited = CollectionManager.Get().GetEditedDeck();
            List<string> slots = edited.GetCardsWithCardID();
            var reasons = DungeonRunEditor.DoneReasons(s.Cards, slots, s.Row, s.Size, s.Policy, s.CardReason);
            if (reasons.Count > 0)
            {
                Log("done refused " + s.TempId + ": " + string.Join("; ", reasons));
                ShowRefusal(NativeDungeonRunText.EditorDoneHeader, NativeDungeonRunText.EditorDoneRefusal(reasons, id => CardName(s, id)));
                return false;
            }
            var list = s.Cards.EngineList(slots, out _);
            // L2: the hand-off first; the leave runs on the next tick, never inside the UI callback.
            NativeDungeonRunUI.BuiltWhenHome(s.ClassId, s.RowId, s.DeckId, list);
            s.Leave = DungeonRunEditor.Leave.Done;
            Set(s, DungeonRunEditor.Stage.Leaving);
            Log("done " + s.TempId + ": " + string.Join(",", list));
        }
        catch (Exception error) { Abort(error); }
        return false;
    }

    /// <summary>
    /// J1, from NativeDeckEditor's PrepareDiscardDeck (the OnBackOutOfDeckContentsImpl prefix) for the temporary deck: Back to
    /// the run menu's Deck step with the list dropped. Native is skipped (no DeleteDeck, no Options 188/288 write, no save),
    /// except where its two early exits would fire.
    /// </summary>
    internal static bool BackOut(CollectionDeckTray tray, ref bool result)
    {
        var s = session;
        try
        {
            if (s == null) return true;
            // Leaving or still entering: inert.
            if (s.Stage != DungeonRunEditor.Stage.Editing) { result = true; return false; }
            if (!tray.IsShowingDeckContents() || (tray.GetCurrentContentType() != DeckTray.DeckContentTypes.INVALID
                && tray.GetCurrentContent() != null && !tray.GetCurrentContent().IsModeActive())) return true;
            NativeDungeonRunUI.DeckWhenHome(s.ClassId, s.RowId, s.DeckId, null);
            s.Leave = DungeonRunEditor.Leave.Back;
            Set(s, DungeonRunEditor.Stage.Leaving);
            Log("back " + s.TempId);
        }
        catch (Exception error) { Abort(error); }
        result = true;
        return false;
    }

    /// <summary>The hook guard for a deck tray: the probe is active, CM is the scene, this is the current tray and it edits the
    /// temporary deck. Never throws.</summary>
    internal static bool OwnsTray(CollectionDeckTray tray)
    {
        try
        {
            return session != null && NativeUIProbe.Active && InCollection && tray != null && ReferenceEquals(tray, CollectionDeckTray.Get())
                && Owns(CollectionManager.Get()?.GetEditedDeck()?.ID ?? 0);
        }
        catch (Exception) { return false; }
    }

    /// <summary>An AlertPopup behind the <see cref="RefusalOpen"/> debounce (a stale flag, with no dialog showing, is reset).</summary>
    internal static void ShowRefusal(string header, string text)
    {
        var dialogs = DialogManager.Get();
        if (RefusalOpen && (dialogs?.ShowingDialog() == true || Now - refusalSince < 1)) return;
        if (dialogs == null) { Log("refusal not shown (no DialogManager): " + text); return; }
        RefusalOpen = true; refusalSince = Now;
        dialogs.ShowPopup(new AlertPopup.PopupInfo {
            m_headerText = header, m_text = text, m_showAlertIcon = true, m_responseDisplay = AlertPopup.ResponseDisplay.OK,
            m_responseCallback = (response, data) => RefusalOpen = false
        });
    }

    private static string CardName(EditorSession s, string id)
    {
        string name = s.Cards.Name(id);
        if (name != null) return name;
        try { return SabberStoneCore.Model.Cards.FromId(id)?.Name; } catch (Exception) { return null; }
    }

    /// <summary>B8: a SendChanges of a band id was swallowed (logged once per id and source).</summary>
    internal static void Swallowed(long id, CollectionDeck.ChangeSource source)
    {
        try { if (swallowedLogged.Add(id + ":" + source)) Log("swallowed save " + id + " (" + source + ")"); }
        catch (Exception) { /* never escape Harmony */ }
    }

    // ------------------------------------------------------------------ removal (K8) and the sweep (K6)

    // Removes the temporary deck (working, base and NetCache header go with RemoveDeck), with the A4/K8 asserts logged.
    private static void RemoveTemp(EditorSession s, string why)
    {
        var manager = CollectionManager.Get();
        if (manager == null || s.TempId == 0) { Log("remove " + s.TempId + " skipped (" + why + "): no collection"); return; }
        string before = DeckState(manager, s.TempId);
        if (manager.GetDeck(s.TempId) == null && manager.GetBaseDeck(s.TempId) == null) { Log("remove " + s.TempId + " (" + why + "): already gone; " + before); return; }
        removeDeck.Invoke(manager, new object[] { s.TempId });
        string after = DeckState(manager, s.TempId);
        Log("removed " + s.TempId + " (" + why + "): before " + before + "; after " + after);
    }

    // A4: the deck, the base deck and the header in both NetCacheDecks views (the probe's published one and NetCache's own).
    private static string DeckState(CollectionManager manager, long id)
    {
        var published = NativeUIProbe.PublishedDeckHeaders;
        NetCache.NetCacheDecks net = null;
        try { net = NetCache.Get()?.GetNetObject<NetCache.NetCacheDecks>(); } catch (Exception) { }
        bool InList(NetCache.NetCacheDecks headers) => headers?.Decks != null && headers.Decks.Any(h => h != null && h.ID == id);
        return "deck=" + (manager.GetDeck(id) != null) + " base=" + (manager.GetBaseDeck(id) != null)
            + " header(published)=" + InList(published) + " header(net)=" + InList(net)
            + (ReferenceEquals(published, net) ? "" : " (the two NetCacheDecks differ)");
    }

    /// <summary>K6: removes every band deck but the live session's (a failed leave's), with a warning. Never throws.</summary>
    internal static void Sweep(string why)
    {
        try
        {
            var manager = CollectionManager.Get();
            if (manager == null || removeDeck == null) return;
            long live = session?.TempId ?? 0;
            var stale = manager.GetDecks().Keys.Where(id => DungeonRunEditor.IsRunBuildId(id) && id != live).ToList();
            foreach (long id in stale)
            {
                removeDeck.Invoke(manager, new object[] { id });
                Log("swept " + id + " (" + why + "): " + DeckState(manager, id));
            }
        }
        catch (Exception error) { Log("sweep failed (" + why + "): " + error.GetBaseException().Message); }
    }

    // ------------------------------------------------------------------ failure (L)

    /// <summary>A hook or step failed: the recovery runs on the next tick (once per session; a second failure is Blocked).</summary>
    internal static void Abort(Exception error)
    {
        try
        {
            Log("abort " + (session?.TempId.ToString() ?? "(no session)") + ": " + error);
            if (session != null && pendingFailure == null) pendingFailure = error;
        }
        catch (Exception) { /* never escape Harmony */ }
    }

    /// <summary>NativeUIProbe.Fail: true when the session takes the failure (its recovery runs on the next tick), so the probe
    /// does not block; false with no session or after the session already recovered once.</summary>
    internal static bool Recover(Exception error)
    {
        var s = session;
        if (s == null) return false;
        try
        {
            var kind = DungeonRunEditor.RecoverAction(s.Stage, AtHub, s.RecoveredOnce);
            Log("recover " + s.TempId + ": " + kind + " for " + s.Stage + " after " + error.GetType().Name + ": " + error.Message);
            if (kind == DungeonRunEditor.RecoverKind.None) return false;
            if (pendingFailure == null) pendingFailure = error;
            return true;
        }
        catch (Exception) { return false; }
    }

    private static bool RunRecovery(Exception error)
    {
        var s = session;
        if (s == null) return true;
        var kind = DungeonRunEditor.RecoverAction(s.Stage, AtHub, s.RecoveredOnce);
        Log("recover " + s.TempId + " " + kind + " (stage " + s.Stage + ", HUB " + AtHub + "): " + error);
        if (kind == DungeonRunEditor.RecoverKind.None) return false;
        editorFailed = true;
        s.RecoveredOnce = true;
        if (kind == DungeonRunEditor.RecoverKind.RemoveAndPlank)
        {
            try { RemoveTemp(s, "recover"); }
            catch (Exception again) { Log("recover " + s.TempId + ": remove failed (the sweep retries): " + again.GetBaseException().Message); }
            End("recovered to the plank Build step");
            NativeDungeonRunUI.BuildWhenHome(s.ClassId, s.RowId, s.DeckId, NativeDungeonRunText.EditorFailedCaption);
            return true;
        }
        // DeckAndLeave: in CM; the leave (L1, L3-L7) with each step guarded.
        NativeDungeonRunUI.DeckWhenHome(s.ClassId, s.RowId, s.DeckId, NativeDungeonRunText.EditorFailedCaption);
        s.Leave = DungeonRunEditor.Leave.Recover;
        s.EditEnded = s.Exited = s.ForcedHub = false;
        Set(s, DungeonRunEditor.Stage.Leaving);
        return true;
    }

    // K8 step 5 / RemoveAndPlank / no hero: the session flag is cleared on every exit.
    private static void End(string why)
    {
        Log("end " + (session?.TempId ?? 0) + ": " + why);
        session = null;
        pendingFailure = null;
        RefusalOpen = false;
        swallowedLogged.Clear();
    }
}
