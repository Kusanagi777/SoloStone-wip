using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using Subscene = Assets.AdventureData.Adventuresubscene;

// Dungeon Run on the client's own screens (spike, owner 2026-11-09): with no active run, Fan-made modes opens the retail
// Adventure dungeon-crawl flow of a borrowed adventure (Dalaran Heist, AdventureDbId 438, mode DUNGEON_CRAWL 5) instead of
// the plank menu: the guest-hero portrait tray (ADVENTURER_PICKER, owner 2026-11-20: every run row, the class heroes and the
// named Heist, Monster Hunt, Rumble and Tombs heroes, each on its client GUEST_HERO record; paged 12 at a time with Previous and
// Next, owner 2026-11-22), then the playmat, reordered to
// deck -> hero power for the chosen row. Play never queues a server game: it starts the run (DungeonRunRuntime.NewRun),
// returns to the HUB and opens NativeDungeonRunUI on the run hub; a row with signature treasures (a Tombs explorer) opens the
// menu on its Signature step with the hero, deck and power already chosen.
// The playmat shows Dungeon Run's own decks and powers (DecksFor/PowersFor), fed through the adventure adapter as detached
// AdventureDeck/AdventureHeroPower records that never enter GameDbf; deck contents come from a LoadDeckFromDBF hook for the
// fed (reserved) deck ids only, and the power cards' names, text and cost from DungeonRunRuntime's picker overrides.
// The server's part is played locally and narrowly: only this adventure's two save keys are answered (empty, a new run),
// and only the subkeys the class tray and playmat write before Play are kept (in the client's memory cache, never on disk).
// Every hook here acts only while a picker session is active; any failure, refused request or NATIVE_UI_BLOCKED on the
// path falls back to the HUB and the existing menu (Abort). A session never ends while the scene is ADVENTURE, and when it
// ends the back-navigation handlers the borrowed screens pushed and the borrowed save keys are cleared again.
// Diagnostics are LogWarning lines prefixed DUNGEON_RUN_PICKER.
internal static class NativeDungeonRunPicker
{
    private const AdventureDbId Adventure = AdventureDbId.DALARAN;
    private const AdventureModeDbId Mode = AdventureModeDbId.DUNGEON_CRAWL;
    // The keys the borrowed AdventureData row names (verified against the row at run time before anything is entered).
    private const GameSaveKeyId ServerKey = GameSaveKeyId.ADVENTURE_DATA_SERVER_DALARAN, ClientKey = GameSaveKeyId.ADVENTURE_DATA_CLIENT_DALARAN;
    // The one server-key subkey written before Play: the class tray's Choose (OnPlayButtonPressed_SaveHeroAndAdvanceToDungeonRunIfNecessary
    // IL 003E). DUNGEON_CRAWL_PLAYER_SELECTED_HERO_CARD_DB_ID (818) is only read on this path (ValidLoadoutIsLockedIn), so it is not kept.
    private const GameSaveKeySubkeyId ServerSave = GameSaveKeySubkeyId.DUNGEON_CRAWL_PLAYER_SELECTED_HERO_CLASS;
    // Client-key subkeys are the adventure's UI flags only: "has seen" VO/tutorial flags and the newly-unlocked deck/power/treasure
    // markers the playmat clears (e.g. DUNGEON_CRAWL_NEWLY_UNLOCKED_DECKS 298, ADVENTURE_HAS_SEEN_FIRST_TIME_FLOW 384). Others are denied.
    private static readonly string[] ClientSaveMarks = { "_HAS_SEEN_", "NEWLY_UNLOCKED", "TUTORIAL" };
    // GoToNextLoadoutState's enum (AdventureDungeonCrawlDisplay+DungeonRunLoadoutState).
    private const int LoadoutInvalid = 0, LoadoutHeroPower = 1, LoadoutTreasure = 2, LoadoutDeck = 3;
    private const float RequestWindow = 15, SceneWindow = 90;

    private enum Phase { Idle, Requesting, Entering, InAdventure, Leaving, Aborting }

    private static ManualLogSource log;
    private static Phase phase;
    private static float phaseSince, lastNavigate;
    private static bool left, openMenu, leaveRequested, forcedHub, dumped, guestHooks;
    private static int recoveries;
    private static HashSet<Navigation.NavigateBackHandler> navAtEntry;
    private static string lastSubscene = "";

    private static FieldInfo heroChosenField, selectedHeroButtonField, heroClassField, loadoutStateField, crawlDataField, playMatField,
        crawlInstanceField, backStackField, historyField, deckSelectField, deckOptionsField, powerOptionsField, optionDbIdField,
        cachedNameField, entityDefCacheField, bonesByCountField, trayPlayField;
    private static MethodInfo saveHeroAndAdvance, setPlayButtonEnabled, setHeroButtonsEnabled, showDecks, showHeroPowers,
        goBackFromDeck, goBackFromHeroPower, setHeroPower, crawlNavigateBack, loadoutNavigateBack, setSelectedAdventure, setSelectedMode,
        setAdventureProperties, goBackToTreasure, setUpDeckList;

    // ---- what the playmat is fed during a session (null outside one; never added to GameDbf)
    // Reserved ids for the fed decks (AdventureDeck.DECK_ID, answered only by FedDeckContents) and the fed records' own IDs.
    private const int FedDeckIdBase = 0x5D520000, FedPowerRecordIdBase = 0x5D521000;
    // The retail "Random <Class> Deck" pouch's single card (DALA_800); shown as our Random deck's list when the client has it.
    private const string RandomDeckCard = "DALA_800";
    private sealed class FedDeck
    {
        internal int DeckId;
        internal RunDeckOption Option; // null for Random and for a Placeholder
        internal bool Random, Placeholder, Unlocked;
        internal string Name, Description, Hint;
        internal List<long> Cards;
        internal AdventureDeckDbfRecord Record;
    }
    private sealed class FedPower
    {
        internal RunPowerOption Option;
        internal int CardDbId;
        internal bool Placeholder; // a locked filler slot (EnsureFeed); never chosen, never first for its card
        internal AdventureHeroPowerDbfRecord Record;
    }
    // The run row the playmat is fed for (a class row "MAGE" or a named hero row "DALA_Rakanishu").
    private static string fedRow;
    // ---- the portrait tray (owner 2026-11-20): the rows it shows, in button order, and the row its Choose took
    private sealed class TrayRow
    {
        internal RunHeroOption Option;
        internal TAG_CLASS Class;
        internal int CardDbId, GuestHeroId;
    }
    private static List<TrayRow> trayRows;
    private static TrayRow chosenRow;
    // ---- the tray's pages (owner 2026-11-22): the prefab lays out at most 12 heroes (TrayCapacity), so with more rows the tray
    // shows one page of them (NativeDungeonRunText.TrayPageRows) and Previous/Next clones of its Back button change the page. The
    // tray only reads its heroes when it is built, so a page change rebuilds it the way the picker was opened: back to the HUB
    // (as the tray's Back does), then the picker again on the new page (NativeDungeonRunUI.PickerWhenHome -> Open(page)).
    // trayPage is the session's page (0 at a plain Open, the asked page after a page change); trayPerPage is 0 with one page.
    private static int trayPage, trayPerPage, pagerFailures;
    private static List<TrayRow> trayShown;
    private static GuestHeroPickerTrayDisplay trayDisplay;
    private static UIBButton pagePrevious, pageNext, pageSource;
    // A page change is on its way to the HUB: at the HUB the picker opens again on trayPage (any fallback opens the menu).
    private static bool reopenTray, pagerDisabled, pagerWaitWarned;
    private static string pagerState = "";
    // Since when the controls wait for the tray's own Back, camera or hero grid (-1: not waiting); past PagerWaitWindow a WARNING.
    private static float pagerWaitSince = -1;
    private const float PagerWaitWindow = 10;
    // The full-capacity hero grid, remembered from a full page (NativeDungeonRunText.TrayPagerGrid), so the plates sit at the same
    // place on every page, the short last page included.
    private static NativeDungeonRunText.TrayGridMemo trayGrid = new NativeDungeonRunText.TrayGridMemo();
    // Choose was taken on this visit of the tray: no page change until the tray subscene is left (the playmat's way back to the
    // tray is a new visit).
    private static bool trayChosen;
    // What a page change carries into the session that reopens the tray (Tick -> NativeDungeonRunUI.PickerWhenHome -> Open(page,
    // true)): the pager's failures (so "disabled for the session" holds across pages), the remembered grid, and that the borrowed
    // client key's in-memory "has seen" flags are kept for that one reopen (ClearBorrowedSaves). Null otherwise; any other open
    // drops it (Open), as does a reopen the menu side gives up on (DropPageChange).
    private sealed class PageCarry
    {
        internal int Failures;
        internal bool Disabled;
        internal NativeDungeonRunText.TrayGridMemo Grid;
    }
    private static PageCarry pageCarry;
    private static List<FedDeck> fedDecks;
    private static List<FedPower> fedPowers;
    // Only our power -> deck back path may dismiss the hero power options (KeepPowerOptions).
    private static bool allowPowerOutro;
    private static string menuNotice;
    // The deck the deck step accepted (an unlocked fed deck): what the power step and Play use, whatever a stray deck click
    // later wrote to SelectedDeckId (OnDeckOptionSelected 0000-0026 always writes it). 0 until the deck step completes.
    private static long confirmedDeckId;
    // The client card ids whose names the session overrides (BeginPicker): their cached EntityDef names are reset on both ends.
    private static List<string> overrideIds;

    /// <summary>A session is open: the hooks below act, and only then.</summary>
    internal static bool Active => phase != Phase.Idle;
    /// <summary>NativeUIProbe.Navigate admits ADVENTURE only for the navigation this class starts.</summary>
    internal static bool Entering => phase == Phase.Entering;
    /// <summary>The picker's own path: entering, on the borrowed screens, or on the way back from them to the HUB. Only here
    /// are blocks, unknown caches and save writes the picker's business; the HUB phases keep the normal fail-closed rules.</summary>
    internal static bool OnPath => phase == Phase.Entering || phase == Phase.InAdventure
        || (phase == Phase.Leaving || phase == Phase.Aborting) && left && !AtStableHub;
    private static bool AtStableHub => SceneMgr.IsInitialized() && SceneMgr.Get().GetMode() == SceneMgr.Mode.HUB && !SceneMgr.Get().IsTransitioning();

    internal static void Initialize(Harmony harmony, ManualLogSource logger)
    {
        log = logger;
        heroChosenField = RequiredField(typeof(DeckPickerTrayDisplay), "m_heroChosen");
        selectedHeroButtonField = RequiredField(typeof(AbsDeckPickerTrayDisplay), "m_selectedHeroButton");
        heroClassField = RequiredField(typeof(HeroPickerButton), "m_heroClass");
        loadoutStateField = RequiredField(typeof(AdventureDungeonCrawlDisplay), "m_currentLoadoutState");
        crawlDataField = RequiredField(typeof(AdventureDungeonCrawlDisplay), "m_dungeonCrawlData");
        playMatField = RequiredField(typeof(AdventureDungeonCrawlDisplay), "m_playMat");
        crawlInstanceField = RequiredField(typeof(AdventureDungeonCrawlDisplay), "m_instance");
        backStackField = RequiredField(typeof(AdventureConfig), "m_SubSceneBackStack");
        historyField = RequiredField(typeof(Navigation), "m_history");
        deckSelectField = RequiredField(typeof(AdventureDungeonCrawlDisplay), "m_dungeonCrawlDeckSelect");
        deckOptionsField = RequiredField(typeof(AdventureDungeonCrawlPlayMat), "m_deckOptions");
        powerOptionsField = RequiredField(typeof(AdventureDungeonCrawlPlayMat), "m_heroPowerOptions");
        saveHeroAndAdvance = Required(typeof(AbsDeckPickerTrayDisplay), "OnPlayButtonPressed_SaveHeroAndAdvanceToDungeonRunIfNecessary", Type.EmptyTypes);
        setPlayButtonEnabled = Required(typeof(AbsDeckPickerTrayDisplay), "SetPlayButtonEnabled", new[] { typeof(bool) });
        setHeroButtonsEnabled = Required(typeof(AbsDeckPickerTrayDisplay), "SetHeroButtonsEnabled", new[] { typeof(bool) });
        showDecks = Required(typeof(AdventureDungeonCrawlDisplay), "ShowDeckOptionsWhenReady", Type.EmptyTypes);
        showHeroPowers = Required(typeof(AdventureDungeonCrawlDisplay), "ShowHeroPowerOptionsWhenReady", Type.EmptyTypes);
        goBackFromDeck = Required(typeof(AdventureDungeonCrawlDisplay), "GoBackFromDeckTemplateSelection", Type.EmptyTypes);
        goBackFromHeroPower = Required(typeof(AdventureDungeonCrawlDisplay), "GoBackFromHeroPower", Type.EmptyTypes);
        setHeroPower = Required(typeof(AdventureDungeonCrawlDisplay), "SetHeroPower", new[] { typeof(string) });
        crawlNavigateBack = Required(typeof(AdventureDungeonCrawlDisplay), "OnNavigateBack", Type.EmptyTypes);
        loadoutNavigateBack = Required(typeof(AdventureDungeonCrawlDisplay), "OnNavigateBackFromCurrentLoadoutState", Type.EmptyTypes);
        setSelectedAdventure = Required(typeof(AdventureConfig), "set_SelectedAdventure", new[] { typeof(AdventureDbId) });
        setSelectedMode = Required(typeof(AdventureConfig), "set_SelectedMode", new[] { typeof(AdventureModeDbId) });
        setAdventureProperties = Required(typeof(AdventureConfig), "SetPropertiesForAdventureAndMode", Type.EmptyTypes);
        goBackToTreasure = Required(typeof(AdventureDungeonCrawlDisplay), "GoBackToTreasureLoadoutSelection", Type.EmptyTypes);
        optionDbIdField = RequiredField(typeof(AdventureOptionWidget), "m_databaseId");
        // EntityDef.GetName returns m_cachedEntityName.Name while IsValidEntityName holds (tag 781 and card id unchanged, IL
        // 0000-0045), and DefLoader.m_entityDefCache shares one EntityDef per card id for the launch (GetEntityDef 001C-0024).
        cachedNameField = RequiredField(typeof(EntityDef), "m_cachedEntityName");
        // SetupHeroLayout (0000-005A): the tray lays out n heroes only when this prefab list has a non-null entry at n.
        bonesByCountField = RequiredField(typeof(AbsDeckPickerTrayDisplay), "m_heroPickerButtonBonesByHeroCount");
        // The tray's Choose: the page controls keep clear of it (the field NativeDeckRecord and NativeDeckPickerGuide read).
        trayPlayField = RequiredField(typeof(AbsDeckPickerTrayDisplay), "m_playButton");
        entityDefCacheField = RequiredField(typeof(DefLoader), "m_entityDefCache");
        // The one overload: (List<CardWithPremiumStatus>, bool useLoadoutOfActiveRun, bool playGlowAnimation, List<long> deckCardIndices,
        // List<long> deckCardEnchantments, RuneType[] runeOrder, Dictionary<string, SideboardDeck> sideboardMap).
        setUpDeckList = Required(typeof(AdventureDungeonCrawlDisplay), "SetUpDeckList", null);

        Patch(harmony, Required(typeof(AdventureConfig), "ShouldDisplayAdventure", new[] { typeof(AdventureDbId) }), nameof(DisplayBorrowed));
        Patch(harmony, Required(typeof(AdventureConfig), "IsChapterSelectedBeforeDungeonCrawlScreenForSelectedAdventure", Type.EmptyTypes), nameof(SkipChapterSelect));
        Patch(harmony, Required(typeof(AdventureConfig), "SubSceneGoBack", new[] { typeof(bool) }), nameof(BackFromTray));
        harmony.Patch(Required(typeof(AdventureConfig), "OnAdventureSceneAwake", Type.EmptyTypes),
            postfix: new HarmonyMethod(typeof(NativeDungeonRunPicker), nameof(KeepSelection)));
        Patch(harmony, Required(typeof(DeckPickerTrayDisplay), "OnPlayGameButtonReleased", new[] { typeof(UIEvent) }), nameof(ChooseClass));
        Patch(harmony, Required(typeof(AdventureDungeonCrawlDisplay), "GoToNextLoadoutState", Type.EmptyTypes), nameof(NextLoadout));
        Patch(harmony, loadoutNavigateBack, nameof(PreviousLoadout));
        Patch(harmony, Required(typeof(AdventureDungeonCrawlDisplay), "OnPlayButtonPress", new[] { typeof(UIEvent) }), nameof(PlayPressed));
        // Our content on the playmat (ShowDeckOptionsWhenReady 006A and GetHeroPowersForDungeonCrawlHero 002C reach these
        // through the adventure adapter; ShowDecks 012A / ShowHeroPowers 0156 ask the adapter's unlock checks).
        var adapter = typeof(Hearthstone.DungeonCrawl.DungeonCrawlDataAdventureAdapter);
        Patch(harmony, Required(adapter, "GetDecksForClass", new[] { typeof(TAG_CLASS) }), nameof(FeedDecks));
        Patch(harmony, Required(adapter, "GetHeroPowersForClass", new[] { typeof(TAG_CLASS) }), nameof(FeedPowers));
        Patch(harmony, Required(adapter, "AdventureDeckIsUnlocked", new[] { typeof(GameSaveKeyId), typeof(AdventureDeckDbfRecord),
            typeof(long).MakeByRefType(), typeof(bool).MakeByRefType() }), nameof(FedDeckUnlocked));
        Patch(harmony, Required(adapter, "AdventureHeroPowerIsUnlocked", new[] { typeof(GameSaveKeyId), typeof(AdventureHeroPowerDbfRecord),
            typeof(long).MakeByRefType(), typeof(bool).MakeByRefType() }), nameof(FedPowerUnlocked));
        // AdventureDungeonCrawlDeckOption.Init (003F) reads the deck's name and card list here.
        Patch(harmony, Required(typeof(CollectionManager), "LoadDeckFromDBF", new[] { typeof(int), typeof(string).MakeByRefType(), typeof(string).MakeByRefType() }),
            nameof(FedDeckContents));
        // OnHeroPowerOptionSelected (0055) dismisses the power options because retail's next step is the decks; ours ends there.
        Patch(harmony, Required(typeof(AdventureDungeonCrawlPlayMat), "PlayHeroPowerOptionSelected", Type.EmptyTypes), nameof(KeepPowerOptions));
        // FindGame / FindGameWithHero show the matchmaking popup after FindGameInternal (IL 0025/0031, 002B/0037), so they are
        // blocked themselves; RestartGame also reaches FindGameInternal, which stays hooked as the backstop.
        Patch(harmony, Required(typeof(GameMgr), "FindGame", null), nameof(BlockServerGame));
        Patch(harmony, Required(typeof(GameMgr), "FindGameWithHero", null), nameof(BlockServerGame));
        Patch(harmony, Required(typeof(GameMgr), "RestartGame", Type.EmptyTypes), nameof(BlockServerGame));
        Patch(harmony, Required(typeof(GameMgr), "FindGameInternal", null), nameof(BlockServerGame));
        // Dalaran Heist has 9 AdventureGuestHeroes rows (one hero per class). During a session the tray reads Dungeon Run's rows
        // instead (FeedGuestHeroes), so ADVENTURER_PICKER opens; the playmat still sees no guest-hero list (NoGuestHeroList) and
        // maps class -> the chosen row's hero (VanillaHeroForClass).
        Patch(harmony, Required(typeof(AdventureConfig), "GuestHeroesExistForCurrentAdventure", Type.EmptyTypes), nameof(GuestTray));
        Patch(harmony, Required(typeof(GuestHeroPickerTrayDisplay), "GetSortedAdventureGuestHeroes", Type.EmptyTypes), nameof(FeedGuestHeroes));
        Patch(harmony, Required(typeof(GuestHeroPickerTrayDisplay), "HandleAdventureGuestHeroUnlockData",
            new[] { typeof(AdventureGuestHeroesDbfRecord), typeof(HeroPickerButton) }), nameof(FedGuestHeroUnlock));
        Patch(harmony, Required(typeof(AbsDeckPickerTrayDisplay), "OnPlayGameButtonReleased", new[] { typeof(UIEvent) }), nameof(ChooseHero));
        harmony.Patch(Required(typeof(GuestHeroPickerButton), "UpdateDisplay", new[] { typeof(DefLoader.DisposableFullDef), typeof(TAG_PREMIUM) }),
            postfix: new HarmonyMethod(typeof(NativeDungeonRunPicker), nameof(FedHeroLabel)));
        Patch(harmony, Required(typeof(GuestHeroPickerTrayDisplay), "UpdateHeroInfo",
            new[] { typeof(DefLoader.DisposableFullDef), typeof(string), typeof(string), typeof(TAG_PREMIUM) }), nameof(FedHeroInfo));
        Patch(harmony, Required(typeof(AdventureUtils), "GetGuestHeroesForAdventure", new[] { typeof(AdventureDbId) }), nameof(NoGuestHeroList));
        Patch(harmony, Required(typeof(AdventureUtils), "GetGuestHeroIdFromHeroCardDbId",
            new[] { typeof(Hearthstone.DungeonCrawl.IDungeonCrawlData), typeof(int) }), nameof(NoGuestHeroId));
        Patch(harmony, Required(typeof(AdventureUtils), "GetHeroCardIdFromClassForDungeonCrawl",
            new[] { typeof(Hearthstone.DungeonCrawl.IDungeonCrawlData), typeof(TAG_CLASS) }), nameof(VanillaHeroForClass));
        Patch(harmony, Required(typeof(AdventureDungeonCrawlDisplay), "SetHero", new[] { typeof(string), typeof(TAG_PREMIUM) }), nameof(PlainHeroPortrait));
        guestHooks = true;
    }

    private static MethodInfo Required(Type type, string name, Type[] args) =>
        AccessTools.Method(type, name, args) ?? throw new MissingMethodException(type.FullName, name);
    private static FieldInfo RequiredField(Type type, string name) =>
        AccessTools.Field(type, name) ?? throw new MissingFieldException(type.FullName, name);
    private static void Patch(Harmony harmony, MethodInfo target, string prefix) =>
        harmony.Patch(target, prefix: new HarmonyMethod(typeof(NativeDungeonRunPicker), prefix));

    private static void Log(string message) => log?.LogWarning("DUNGEON_RUN_PICKER " + message);
    private static void Set(Phase next) { if (phase != next) Log("phase " + phase + " -> " + next); phase = next; phaseSince = Time.realtimeSinceStartup; }
    private static float Elapsed => Time.realtimeSinceStartup - phaseSince;

    // ------------------------------------------------------------------ entry

    /// <summary>Fan-made modes: the native picker when there is no run, otherwise (or when it cannot start) the menu. A page
    /// change opens it again on that tray page (NativeDungeonRunUI.PickerWhenHome, pageChange true: it takes what the page
    /// change carried, PageCarry).</summary>
    internal static void Open(int page = 0, bool pageChange = false)
    {
        var carry = pageChange ? pageCarry : null;
        pageCarry = null;
        if (Active) { Log("open ignored: a picker session is already " + phase); return; }
        bool started = false;
        try { started = TryBegin(page, carry); }
        catch (Exception error) { Log("exception before entering: " + error); End("begin failed"); }
        if (started) return;
        // A page change that cannot reopen the tray lets go of the "has seen" flags it kept (a normal open never kept any).
        if (carry != null) Safe("save cleanup", ClearBorrowedSaves);
        NativeDungeonRunUI.Open();
    }

    /// <summary>NativeDungeonRunUI dropped a pending page-change reopen (another hand-off replaced it, or the HUB was not idle
    /// in time): what the page change kept for it goes.</summary>
    internal static void DropPageChange()
    {
        if (pageCarry == null) return;
        pageCarry = null;
        if (!Active) Safe("save cleanup", ClearBorrowedSaves);
        Log("page change dropped: its kept state cleared");
    }

    private static bool TryBegin(int page, PageCarry carry)
    {
        if (!DungeonRunRuntime.Available) return false;
        var save = DungeonRunRuntime.Save;
        if (save == null) return false;
        if (save.Active != null) { Log("active run (class " + (save.Active.Class ?? save.Active.Hero) + ", floor " + save.Active.Floor + "): opening the run menu"); return false; }
        // Progress already earned shows as unlocked on the playmat (the next commit writes it).
        DungeonRunRuntime.RefreshUnlocks();
        DumpBorrowed();
        var config = AdventureConfig.Get();
        if (config == null) { Log("refused: AdventureConfig is not loaded"); return false; }
        if (AdventureProgressMgr.Get() == null) { Log("refused: AdventureProgressMgr service is missing"); return false; }
        var record = GameUtils.GetAdventureDataRecord((int)Adventure, (int)Mode);
        if (record == null) { Log("refused: no AdventureData row for " + Adventure + "/" + Mode); return false; }
        int guests = GuestHeroRows();
        if (guests != 0 && !guestHooks) { Log("refused: " + Adventure + " has " + guests + " AdventureGuestHeroes rows and the guest-hero hooks are not in place (the tray would be ADVENTURER_PICKER)"); return false; }
        // The portrait tray offers every playable run row the client can draw (BuildTrayRows); a row it cannot is left out with
        // the reason logged, and with no row at all the menu opens instead.
        trayRows = BuildTrayRows();
        chosenRow = null;
        // The page is clamped once the tray's capacity is known (FeedGuestHeroes).
        trayPage = Math.Max(0, page); trayPerPage = 0; trayShown = null; trayDisplay = null;
        reopenTray = pagerWaitWarned = trayChosen = false; pagerState = ""; pagerWaitSince = -1;
        // A page change carries the pager's failures and the remembered grid on; any other open starts them afresh.
        pagerFailures = carry?.Failures ?? 0; pagerDisabled = carry?.Disabled ?? false;
        trayGrid = carry?.Grid ?? new NativeDungeonRunText.TrayGridMemo();
        if (carry != null)
            Log("page change to tray page " + (trayPage + 1) + ": carried " + pagerFailures + " page-control failure(s)" + (pagerDisabled ? " (disabled)" : "")
                + ", grid " + (trayGrid.FromBack == null ? "not remembered" : "remembered") + ", client-key \"has seen\" flags kept");
        if (trayRows.Count == 0) { Log("refused: no run row has a client hero card and GUEST_HERO record: opening the run menu"); return false; }
        if (record.GameSaveDataServerKey != (int)ServerKey || record.GameSaveDataClientKey != (int)ClientKey)
        {
            Log("refused: AdventureData keys server=" + record.GameSaveDataServerKey + " client=" + record.GameSaveDataClientKey
                + " are not the allowlisted " + ServerKey + "(" + (int)ServerKey + ")/" + ClientKey + "(" + (int)ClientKey + ")");
            return false;
        }
        left = openMenu = leaveRequested = forcedHub = false; recoveries = 0; lastSubscene = ""; menuNotice = null;
        ClearFeed();
        // A new run: nothing an earlier session kept in memory is answered, except on a page change's reopen the client key's
        // "has seen" flags its own session just wrote (so a first-entry intro or VO does not replay on every page).
        ClearBorrowedSaves(carry != null);
        Set(Phase.Requesting);
        config.SetSelectedAdventureMode(Adventure, Mode);
        Log("selected " + Adventure + "/" + Mode + " options[189]=" + Options.Get().GetInt(Option.SELECTED_ADVENTURE)
            + " options[190]=" + Options.Get().GetInt(Option.SELECTED_ADVENTURE_MODE) + " mission=" + config.GetMission());
        // The hooks act from here (Active): the borrowed adventure must now read as having guest heroes (the tray) and an empty
        // guest-hero list (the playmat's class path).
        bool exist = config.GuestHeroesExistForCurrentAdventure();
        int listed = AdventureUtils.GetGuestHeroesForAdventure(Adventure).Count;
        var heroes = PickerClasses.Select(c => (cls: c, card: AdventureUtils.GetHeroCardIdFromClassForDungeonCrawl(null, c))).ToList();
        var notVanilla = heroes.Where(h => string.IsNullOrEmpty(h.card) || !GameUtils.IsVanillaHero(h.card)).Select(h => h.cls + "=" + h.card).ToList();
        Log("guest heroes replaced: rows=" + guests + " GuestHeroesExistForCurrentAdventure=" + exist + " GetGuestHeroesForAdventure=" + listed
            + " vanilla " + string.Join(" ", heroes.Select(h => h.cls + "=" + h.card)) + "; tray rows " + trayRows.Count + ": "
            + string.Join(" ", trayRows.Select(r => r.Option.Id + "(" + r.Class + " card=" + r.CardDbId + " guest=" + r.GuestHeroId + ")")));
        if (!exist || listed != 0) { Abort("guest heroes not replaced for " + Adventure + " (exist=" + exist + ", listed=" + listed + ")"); return true; }
        // GetHeroCardIdFromClassForDungeonCrawl falls back to the original (a random favourite skin) when GetVanillaHero fails.
        if (heroes.Count == 0 || notVanilla.Count != 0) { Abort("class -> hero is not the vanilla hero for " + Adventure + " (" + (heroes.Count == 0 ? "no classes" : string.Join(" ", notVanilla)) + ")"); return true; }
        config.RequestGameSaveDataKeysForSelectedAdventure(OnKeysReady);
        return true;
    }

    // RequestGameSaveDataKeysForSelectedAdventure's answer (NativeUIProbe.Preferences queues it to the next Update).
    private static void OnKeysReady(bool success)
    {
        try
        {
            if (phase != Phase.Requesting) { Log("late save-key answer ignored in " + phase); return; }
            if (!success) { Abort("save keys refused"); return; }
            var config = AdventureConfig.Get();
            Subscene sub = AdventureConfig.GetSubSceneFromMode(Adventure, Mode);
            Log("save keys ready; first subscene=" + sub + " (expected " + Tray + ")");
            if (sub != Tray) { Abort("borrowed adventure would open " + sub + ", not the portrait tray"); return; }
            config.ResetSubScene(sub); // empty back stack: Back on the tray is ours (BackFromTray)
            navAtEntry = new HashSet<Navigation.NavigateBackHandler>(NavigationStack()?.ToArray() ?? new Navigation.NavigateBackHandler[0]);
            Set(Phase.Entering);
            NativeUIProbe.Navigate(SceneMgr.Mode.ADVENTURE);
            if (!NativeUIProbe.IsLoading(SceneMgr.Mode.ADVENTURE)) { Abort("navigation to ADVENTURE did not start"); return; }
            left = true;
        }
        catch (Exception error) { Abort("entering: " + error); }
    }

    // ------------------------------------------------------------------ per frame (NativeUIProbe.Update)

    internal static void Update()
    {
        if (phase == Phase.Idle) return;
        try { Tick(); }
        catch (Exception error) { Abort("update: " + error); }
    }

    private static void Tick()
    {
        if (!SceneMgr.IsInitialized()) return;
        var scene = SceneMgr.Get();
        SceneMgr.Mode mode = scene.GetMode();
        bool busy = scene.IsTransitioning() || NativeUIProbe.SceneWaiting;
        TraceSubscene(mode);
        switch (phase)
        {
            case Phase.Requesting:
                if (Elapsed > RequestWindow) Abort("save keys were not answered in " + RequestWindow + "s");
                break;
            case Phase.Entering:
                if (mode == SceneMgr.Mode.ADVENTURE && !busy) { Set(Phase.InAdventure); Log("in ADVENTURE subscene=" + AdventureConfig.Get()?.CurrentSubScene); }
                else if (Elapsed > SceneWindow) Abort("ADVENTURE did not load in " + SceneWindow + "s");
                break;
            case Phase.InAdventure:
                if (busy) { HidePager(); break; }
                if (mode != SceneMgr.Mode.ADVENTURE) { End("left ADVENTURE for " + mode); break; }
                if (leaveRequested) { leaveRequested = false; HidePager(); Set(Phase.Leaving); GoHub(); break; }
                UpdatePager();
                break;
            case Phase.Leaving:
            case Phase.Aborting:
                if (busy) break;
                if (mode == SceneMgr.Mode.HUB || !left)
                {
                    // A page change (FlipPage) opens the picker again on its page; a fallback on the way opened the menu instead.
                    int again = reopenTray && !openMenu && phase == Phase.Leaving ? trayPage : -1;
                    var carry = again < 0 ? null : new PageCarry { Failures = pagerFailures, Disabled = pagerDisabled, Grid = trayGrid };
                    if (openMenu) NativeDungeonRunUI.OpenWhenHome(menuNotice);
                    End(phase == Phase.Aborting ? "fallback done" : again >= 0 ? "back at HUB for tray page " + (again + 1) : "back at HUB", carry != null);
                    if (again >= 0) { pageCarry = carry; NativeDungeonRunUI.PickerWhenHome(again); }
                }
                else if (Elapsed <= SceneWindow) GoHub();
                // Never End in ADVENTURE: the tray's back stack is empty (ResetSubScene) and retail SubSceneGoBack does
                // nothing once BackFromTray stands down. Past the window, switch scene directly, once, and keep waiting.
                else if (mode == SceneMgr.Mode.ADVENTURE)
                {
                    if (!forcedHub) { forcedHub = true; Log("HUB not reached in " + SceneWindow + "s: SceneMgr.SetNextMode(HUB) directly"); SceneMgr.Get().SetNextMode(SceneMgr.Mode.HUB); }
                    else GoHub();
                }
                else { Log("HUB not reached in " + SceneWindow + "s, now in " + mode); End("gave up outside ADVENTURE"); }
                break;
        }
    }

    private static void GoHub()
    {
        if (Time.realtimeSinceStartup - lastNavigate < 2) return;
        lastNavigate = Time.realtimeSinceStartup;
        Log("navigating to HUB from " + SceneMgr.Get().GetMode());
        NativeUIProbe.Navigate(SceneMgr.Mode.HUB);
    }

    private static void TraceSubscene(SceneMgr.Mode mode)
    {
        var config = AdventureConfig.Get();
        string now = config == null ? "none" : mode + "/" + config.CurrentSubScene;
        if (now == lastSubscene) return;
        Log("subscene " + (lastSubscene.Length == 0 ? "start" : lastSubscene) + " -> " + now + " backStack=" + BackStackCount(config));
        lastSubscene = now;
    }

    private static int BackStackCount(AdventureConfig config) => (backStackField.GetValue(config) as ICollection)?.Count ?? -1;

    // keepSeen: a page change's End (Tick), which keeps the borrowed client key's "has seen" flags for its reopen (ClearBorrowedSaves).
    private static void End(string why, bool keepSeen = false)
    {
        if (phase != Phase.Idle) Log("session ended: " + why);
        phase = Phase.Idle; leaveRequested = false; openMenu = false; menuNotice = null;
        Safe("feed cleanup", ClearFeed);
        trayRows = null; chosenRow = null;
        Safe("page controls cleanup", RetirePager);
        trayPage = 0; trayPerPage = 0; trayShown = null; trayDisplay = null; reopenTray = false; trayChosen = false;
        Safe("navigation cleanup", CleanNavigation);
        Safe("save cleanup", () => ClearBorrowedSaves(keepSeen));
        // The playmat's picks (the adapter's set_SelectedDeckId / set_SelectedHeroPowerDbId and OnHeroPowerOptionSelected
        // 0010-0016) live in AdventureConfig: a reserved fed deck id must not outlive the session (ResetLoadout 0000-0029).
        Safe("loadout cleanup", () => AdventureConfig.Get()?.ResetLoadout());
        navAtEntry = null;
    }

    private static Stack<Navigation.NavigateBackHandler> NavigationStack() => historyField.GetValue(null) as Stack<Navigation.NavigateBackHandler>;

    // The borrowed screens push back handlers (OnNavigateBackFromCurrentLoadoutState, the static OnNavigateBack, the tray's)
    // that retail pops in LockInNewRunSelectionsAndTransition or by backing out; scene changes do not clear Navigation, and at
    // the HUB a stale OnNavigateBackFromCurrentLoadoutState throws on every Escape (m_instance is null). Remove every handler
    // that was not on the stack at entry and belongs to the adventure screens or to a destroyed object; the HUB's own stay.
    private static void CleanNavigation()
    {
        var stack = NavigationStack();
        if (stack == null) return;
        var keep = new List<Navigation.NavigateBackHandler>();
        var removed = new List<string>();
        foreach (var handler in stack.ToArray()) // top first
        {
            bool before = navAtEntry != null && navAtEntry.Contains(handler);
            if (!before && handler != null && FromBorrowedScreens(handler)) removed.Add(handler.Method.DeclaringType?.Name + "." + handler.Method.Name);
            else keep.Add(handler);
        }
        if (removed.Count == 0) return;
        keep.Reverse(); // Stack(IEnumerable) pushes in order: bottom first, as retail RemoveHandler rebuilds it
        historyField.SetValue(null, new Stack<Navigation.NavigateBackHandler>(keep));
        Log("navigation cleanup removed " + removed.Count + " handler(s): " + string.Join(", ", removed) + "; " + keep.Count + " kept");
    }

    private static bool FromBorrowedScreens(Navigation.NavigateBackHandler handler)
    {
        if (handler.Target is UnityEngine.Object owner && owner == null) return true; // destroyed with the ADVENTURE scene
        Type type = handler.Method.DeclaringType;
        while (type?.DeclaringType != null) type = type.DeclaringType; // compiler closures belong to their outer class
        return type != null && (type.Name.StartsWith("Adventure", StringComparison.Ordinal) || type.Name.StartsWith("DungeonCrawl", StringComparison.Ordinal)
            || typeof(AbsDeckPickerTrayDisplay).IsAssignableFrom(type));
    }

    // GameSaveDataManager.ClearLocalData drops the in-memory map entry, so the next request is answered empty (EnsurePreference).
    // keepClient (a page change only: End from Tick, then the reopen's TryBegin) leaves the client key's entry in memory, so the
    // reopened tray reads the "has seen" flags (ClientSaveMarks) its previous page's session wrote (EnsurePreference fills only a
    // missing key). The session answered that entry empty, so it holds only the client marks the session wrote: the tray's "has seen"
    // flags, and, when the page changes after the playmat's way back to the tray, the playmat's NEWLY_UNLOCKED / tutorial marks
    // too (kept as well; unchecked in the client). The server key (the chosen class) is always cleared.
    private static void ClearBorrowedSaves() => ClearBorrowedSaves(false);
    private static void ClearBorrowedSaves(bool keepClient)
    {
        var manager = GameSaveDataManager.Get();
        if (manager == null) return;
        bool had = manager.IsDataReady(ServerKey) || !keepClient && manager.IsDataReady(ClientKey);
        manager.ClearLocalData(ServerKey);
        if (!keepClient) manager.ClearLocalData(ClientKey);
        if (had) Log("cleared in-memory save data " + ServerKey + (keepClient ? " (kept " + ClientKey + " for the page change)" : "/" + ClientKey));
    }

    /// <summary>Back to the HUB and the plank menu, never stuck in ADVENTURE. Never throws.</summary>
    internal static void Abort(string why)
    {
        try
        {
            log?.LogError("DUNGEON_RUN_PICKER fallback to HUB + Dungeon Run menu: " + why);
            if (phase == Phase.Idle) return;
            Safe("page controls hide", HidePager); // the fallback leaves the tray: no page change from here
            // A page change that fails on its way to the HUB ends on the menu, never on a second picker session.
            if (reopenTray) { reopenTray = false; openMenu = true; }
            if (phase == Phase.Leaving || phase == Phase.Aborting) return; // already on the way back: keep its menu choice
            openMenu = true;
            Set(Phase.Aborting);
        }
        catch (Exception) { }
    }

    /// <summary>NativeUIProbe.Fail: a block raised on the picker's path (OnPath) becomes its fallback, and every later one is
    /// swallowed until the HUB is reached, so the way back never stops ticking. Elsewhere the block stands.</summary>
    internal static bool Recover(Exception error)
    {
        if (!OnPath) return false;
        recoveries++;
        if (recoveries <= 3) log?.LogError("DUNGEON_RUN_PICKER NATIVE_UI_BLOCKED during the picker, recovered (" + recoveries + "): " + error);
        else if (recoveries == 4) log?.LogError("DUNGEON_RUN_PICKER further NATIVE_UI_BLOCKED reports are suppressed until the HUB");
        Abort("native UI block: " + error.GetType().Name + ": " + error.Message);
        return true;
    }

    /// <summary>NativeUIProbe.Fail raised a real block (off the picker's path, so at the HUB): the native UI stops ticking,
    /// so the session stands down instead of waiting for an answer that will never be read.</summary>
    internal static void Blocked()
    {
        if (!Active) return;
        try { End("native UI blocked in " + phase); }
        catch (Exception) { }
    }

    // ------------------------------------------------------------------ save data (NativeUIProbe.Preferences / SavePreferences)

    private static bool BorrowedKey(GameSaveKeyId key) => key == ServerKey || key == ClientKey;

    internal static bool AllowsKey(GameSaveKeyId key) => (phase == Phase.Requesting || OnPath) && BorrowedKey(key);

    internal static bool AllowsSave(GameSaveKeyId key, GameSaveKeySubkeyId subkey) => OnPath
        && (key == ServerKey && subkey == ServerSave || key == ClientKey && ClientSaveMarks.Any(mark => subkey.ToString().Contains(mark)));

    /// <summary>NativeUIProbe.Preferences: true when the request is the picker's own (it names a borrowed key while the keys
    /// are being requested, or it comes from the borrowed screens). Then a refusal is its fallback, not a block.</summary>
    internal static bool ClaimRequest(ICollection<GameSaveKeyId> keys, bool valid)
    {
        if (!Active || keys == null) return false;
        if (!OnPath && !(phase == Phase.Requesting && keys.Any(BorrowedKey))) return false;
        Log("save-data request " + string.Join(",", keys.Select(k => k + "(" + (int)k + ")")) + (valid ? " answered (empty, new run)" : " REFUSED"));
        if (!valid) Abort("unknown save-data key requested");
        return true;
    }

    internal static void NoteSave(GameSaveDataManager.SubkeySaveRequest request, bool kept)
    {
        if (!OnPath || request == null) return;
        Log("save-data write " + request.Key + "/" + request.Subkey + "(" + (int)request.Subkey + ")="
            + string.Join(",", (request.Long_Values ?? new long[0]).Select(v => v.ToString())) + (kept ? " kept in memory" : " denied"));
    }

    /// <summary>NativeUIProbe.Batch / RequestCache: an unknown or missing NetCache on the picker's path is a fallback, not a block.</summary>
    internal static bool RefuseCache(Type type)
    {
        if (!OnPath) return false;
        Abort("unknown NetCache batch " + type.FullName);
        return true;
    }

    // ------------------------------------------------------------------ hooks

    private static bool Borrowed(AdventureConfig config) => config != null && config.GetSelectedAdventure() == Adventure && config.GetSelectedMode() == Mode;
    private static bool InAdventure => Active && SceneMgr.IsInitialized() && SceneMgr.Get().GetMode() == SceneMgr.Mode.ADVENTURE;

    // AdventureConfig.ShouldDisplayAdventure (static): offline the wing/event checks fail and OnAdventureSceneAwake resets the selection.
    private static bool DisplayBorrowed(AdventureDbId __0, ref bool __result)
    {
        if (!Active || __0 != Adventure) return true;
        __result = true; return false;
    }

    // AdventureConfig.IsChapterSelectedBeforeDungeonCrawlScreenForSelectedAdventure: no chapter/location page (LOCATION_SELECT).
    private static bool SkipChapterSelect(AdventureConfig __instance, ref bool __result)
    {
        if (!Active || !Borrowed(__instance)) return true;
        __result = false; return false;
    }

    // AdventureConfig.OnAdventureSceneAwake reloads the selection from Options 189/190: keep ours if that did not stick.
    private static void KeepSelection(AdventureConfig __instance)
    {
        if (!Active) return;
        try
        {
            if (Borrowed(__instance)) { Log("scene awake kept " + Adventure + "/" + Mode); return; }
            Log("scene awake read " + __instance.GetSelectedAdventure() + "/" + __instance.GetSelectedMode() + "; restoring " + Adventure + "/" + Mode);
            setSelectedAdventure.Invoke(__instance, new object[] { Adventure });
            setSelectedMode.Invoke(__instance, new object[] { Mode });
            // Retail ran SetPropertiesForAdventureAndMode (wings owned, completion rewards) at IL 0047 for the reset selection.
            setAdventureProperties.Invoke(__instance, null);
        }
        catch (Exception error) { Abort("scene awake: " + error); }
    }

    // The subscene a session opens on: the guest-hero portrait tray (GuestTray gives the borrowed adventure guest heroes).
    private const Subscene Tray = Subscene.ADVENTURER_PICKER;

    // AdventureConfig.SubSceneGoBack: Back/Escape on the portrait tray (empty back stack) leaves for the HUB.
    private static bool BackFromTray(AdventureConfig __instance)
    {
        if (!InAdventure) return true;
        try
        {
            Subscene current = __instance.CurrentSubScene;
            if (phase != Phase.InAdventure || leaveRequested)
            {
                // Already on the way back (after Play or a fallback): keep its menu choice and never start a subscene change
                // while the HUB is being loaded; once leaving, retry the HUB now.
                Log("back from " + current + " while leaving (" + phase + "): ignored" + (phase != Phase.InAdventure ? ", retrying HUB" : ""));
                if (phase != Phase.InAdventure) lastNavigate = 0;
                return false;
            }
            if (current != Tray && BackStackCount(__instance) != 0) return true;
            Log("back from " + current + ": returning to HUB");
            leaveRequested = true; openMenu = false;
            return false;
        }
        catch (Exception error) { Abort("back: " + error); return false; }
    }

    // DeckPickerTrayDisplay.OnPlayGameButtonReleased: the tray's Choose. Retail starts with Network.IsLoggedIn (reconnect
    // dialog offline) and set-rotation/migration checks; here only the dungeon-crawl branch of PlayGame runs.
    private static bool ChooseClass(DeckPickerTrayDisplay __instance)
    {
        if (!InAdventure) return true;
        try
        {
            var button = selectedHeroButtonField.GetValue(__instance) as HeroPickerButton;
            if (button == null) { Log("Choose with no hero selected: ignored"); return false; }
            heroChosenField.SetValue(__instance, true);
            setPlayButtonEnabled.Invoke(__instance, new object[] { false });
            setHeroButtonsEnabled.Invoke(__instance, new object[] { false });
            Log("class chosen " + heroClassField.GetValue(button) + " heroCardDbId=" + button.HeroCardDbId
                + " card=" + GameUtils.TranslateDbIdToCardId(button.HeroCardDbId, false));
            bool advanced = (bool)saveHeroAndAdvance.Invoke(__instance, null);
            if (!advanced) Abort("Choose did not advance to the playmat (DungeonCrawlPickHeroFirst false?)");
        }
        catch (Exception error) { Abort("Choose: " + error); }
        return false;
    }

    // AdventureDungeonCrawlDisplay.GoToNextLoadoutState: retail [treasure] -> HEROPOWER -> DECKTEMPLATE; here [treasure] -> DECK -> POWER.
    private static bool NextLoadout(AdventureDungeonCrawlDisplay __instance)
    {
        if (!InAdventure) return true;
        // After Play (or a fallback) the playmat stays clickable until the HUB loads: no step runs any more.
        if (phase != Phase.InAdventure || leaveRequested) { Log("loadout next while leaving (" + phase + "): ignored"); return false; }
        try
        {
            int state = Convert.ToInt32(loadoutStateField.GetValue(__instance));
            var data = crawlDataField.GetValue(__instance) as Hearthstone.DungeonCrawl.IDungeonCrawlData;
            Log("loadout next from state " + state + " heroPowersAndDecks=" + data?.SelectableHeroPowersAndDecksExist()
                + " treasures=" + data?.SelectableLoadoutTreasuresExist() + " deck=" + data?.SelectedDeckId + " power=" + data?.SelectedHeroPowerDbId
                + " hero=" + (data == null ? "-" : CardId(data.SelectedHeroCardDbId)) + " slots " + Slots(__instance));
            if (data == null) return true;
            if (state == LoadoutInvalid && data.SelectableLoadoutTreasuresExist()) return true; // retail: the satchel first
            if (state == LoadoutInvalid || state == LoadoutTreasure)
            {
                if (data.SelectableHeroPowersAndDecksExist()) Run(__instance, showDecks, "decks");
                return false;
            }
            var mat = playMatField.GetValue(__instance) as AdventureDungeonCrawlPlayMat;
            if (state == LoadoutDeck)
            {
                SetPlay(__instance, false); // OnDeckOptionSelected enabled it: Play waits for the power
                var deck = FedDeckById(data.SelectedDeckId);
                if (deck == null) { Abort("selected deck " + data.SelectedDeckId + " is not one the picker fed"); return false; }
                // OnDeckOptionSelected (0000-0026) already showed its list: a locked deck is previewed, never taken on.
                if (!deck.Unlocked) { Log("deck " + deck.Name + " is locked (" + deck.Hint + "): staying on the deck step"); return false; }
                if (mat == null) { Abort("deck step done but the playmat is gone"); return false; }
                confirmedDeckId = deck.DeckId;
                // The mirror of retail's power -> deck step (OnHeroPowerOptionSelected 0055 PlayHeroPowerOptionSelected): the deck
                // options play their outro (PlayWidgetOptionSelected: state TRANSITIONING, PlayOutro: Clickable off, then on
                // CODE_OPTION_OUTRO_COMPLETE OnOutroFinished SetActive(false)), so they stop catching clicks over the powers.
                // ShowHeroPowerOptionsWhenReady waits (MoveNext 0045-0051) until Update sees every deck outro done (0086-00F9).
                Log("deck step done: " + deck.Name + (deck.Random ? " (random)" : "") + "; hiding deck options " + OptionStates(deckOptionsField, mat));
                mat.PlayDeckOptionSelected();
                Run(__instance, showHeroPowers, "hero powers");
                // ShowHeroPowerOptionsWhenReady pushes the step's back handler only with treasures (MoveNext 0099-00AC);
                // the power step always needs it, also after a power -> deck back popped it.
                Navigation.PushUnique(LoadoutBackHandler());
                return false;
            }
            if (state == LoadoutHeroPower)
            {
                // A hero-power pick (OnHeroPowerOptionSelected 0061; a locked one returns at 0003). The options stay shown
                // (KeepPowerOptions), so the pick moves like retail's deck pick: deselect the others (as DeselectAllDeckOptionsWithoutId).
                if (data.SelectedDeckId != confirmedDeckId)
                {
                    // A deck option reached OnDeckOptionSelected during the power step (they should be hidden): the deck step
                    // did not run for that pick, so the accepted deck, its selection and its list (as 0019-0026) are restored.
                    var accepted = FedDeckById(confirmedDeckId);
                    Log("deck changed during power step: " + data.SelectedDeckId + " -> restoring " + confirmedDeckId + ":" + accepted?.Name
                        + "; deck options " + OptionStates(deckOptionsField, mat));
                    data.SelectedDeckId = confirmedDeckId;
                    if (mat != null) mat.DeselectAllDeckOptionsWithoutId((int)confirmedDeckId);
                    if (accepted != null)
                        setUpDeckList.Invoke(__instance, new object[] { CardWithPremiumStatus.ConvertList(new List<long>(accepted.Cards)), false, true, null, null, null, null });
                }
                long picked = data.SelectedHeroPowerDbId;
                if (mat != null) foreach (var option in OptionList(powerOptionsField, mat))
                        if (option != null && Convert.ToInt64(optionDbIdField.GetValue(option)) != picked) option.Deselect();
                var deck = FedDeckById(confirmedDeckId);
                var power = FedPowerByCard(picked);
                bool complete = deck != null && deck.Unlocked && power != null && power.Option.Unlocked;
                SetPlay(__instance, complete);
                Log((complete ? "loadout complete" : "loadout incomplete") + ": deck=" + confirmedDeckId + ":" + deck?.Name
                    + " power=" + picked + ":" + CardId(picked) + ":" + power?.Option.Id);
                return false;
            }
        }
        catch (Exception error) { Abort("loadout next: " + error); return false; }
        return true;
    }

    // AdventureDungeonCrawlDisplay.OnNavigateBackFromCurrentLoadoutState (static, the handler ShowDeckOptionsWhenReady pushes):
    // from the power step back to the decks; from the deck step (now the first) out of the playmat, to the class tray.
    private static bool PreviousLoadout(ref bool __result)
    {
        // Retail reads m_instance.m_playMat before its null check (IL 0000/0005, 001F): a handler left behind after the scene
        // unloaded throws on every Escape. In or out of a session, a stale one removes itself and reports "not handled".
        try
        {
            var live = crawlInstanceField.GetValue(null) as AdventureDungeonCrawlDisplay;
            if (live == null)
            {
                Navigation.RemoveHandler(LoadoutBackHandler());
                log?.LogWarning("DUNGEON_RUN_PICKER stale loadout back handler removed (no AdventureDungeonCrawlDisplay)");
                __result = false; return false;
            }
        }
        catch (Exception error) { log?.LogWarning("DUNGEON_RUN_PICKER stale-handler check failed: " + error.Message); }
        if (!InAdventure) return true;
        // After Play (or a fallback): Back no longer replays steps or leaves the playmat for the tray while the HUB loads.
        if (phase != Phase.InAdventure || leaveRequested) { Log("loadout back while leaving (" + phase + "): ignored"); __result = true; return false; }
        try
        {
            var display = crawlInstanceField.GetValue(null) as AdventureDungeonCrawlDisplay;
            var mat = display == null ? null : playMatField.GetValue(display) as AdventureDungeonCrawlPlayMat;
            if (display == null || mat == null || (int)mat.GetPlayMatState() == 2) return true;
            int state = Convert.ToInt32(loadoutStateField.GetValue(display));
            var data = crawlDataField.GetValue(display) as Hearthstone.DungeonCrawl.IDungeonCrawlData;
            Log("loadout back from state " + state);
            if (state == LoadoutHeroPower)
            {
                // Retail's deck -> power back (0091-009B) is GoBackFromDeckTemplateSelection (clear the deck, outro the decks)
                // then GoBackToHeroPower (clear the power, show the powers). Mirrored: outro the powers (GoBackFromHeroPower, the
                // one PlayHeroPowerOptionSelected KeepPowerOptions lets through), clear the power and the deck, show the decks.
                // The deck options are already hidden (their outro ran on the way here), so GoBackFromDeckTemplateSelection's
                // second PlayDeckOptionSelected is not called: it would replay an outro on inactive widgets.
                Log("power step back: hiding hero power options " + OptionStates(powerOptionsField, mat));
                allowPowerOutro = true;
                try { goBackFromHeroPower.Invoke(display, null); }
                finally { allowPowerOutro = false; }
                if (data != null) { data.SelectedHeroPowerDbId = 0; data.SelectedDeckId = 0; }
                confirmedDeckId = 0;
                setHeroPower.Invoke(display, new object[] { null });
                // GoBackFromDeckTemplateSelection 0008-0019 and 0095-009C: an empty deck list, no deck selected.
                setUpDeckList.Invoke(display, new object[] { new List<CardWithPremiumStatus>(), false, true, null, null, null, null });
                mat.DeselectAllDeckOptionsWithoutId(0);
                SetPlay(display, false);
                Run(display, showDecks, "decks");
                // Navigation.GoBack pops this handler when it returns true (IL 0029-0045) and ShowDeckOptionsWhenReady's
                // PushUnique saw it still there: push a second copy so the deck step keeps its back handler.
                Navigation.Push(LoadoutBackHandler());
                __result = true; return false;
            }
            if (state == LoadoutDeck)
            {
                goBackFromDeck.Invoke(display, null);
                SetPlay(display, false);
                if (data != null && data.SelectableLoadoutTreasuresExist())
                {
                    // Retail's first post-satchel step goes back to the satchel (OnNavigateBackFromCurrentLoadoutState 0059-0079);
                    // GoBack then pops this handler, as retail's satchel step has none.
                    Log("deck step back to the treasure satchel");
                    goBackToTreasure.Invoke(display, null);
                    __result = true; return false;
                }
                Navigation.RemoveHandler(LoadoutBackHandler());
                __result = Navigation.GoBack() || (bool)crawlNavigateBack.Invoke(null, null);
                return false;
            }
        }
        catch (Exception error) { Abort("loadout back: " + error); __result = true; return false; }
        return true;
    }

    // AdventureDungeonCrawlDisplay.OnPlayButtonPress: no lock-in and no server game. The playmat's choice is mapped back to
    // Dungeon Run ids and the run is started (DungeonRunRuntime.NewRun); then the HUB and the run hub, or on a refusal the
    // menu with the refusal as its caption.
    private static bool PlayPressed(AdventureDungeonCrawlDisplay __instance)
    {
        if (!InAdventure) return true;
        if (phase != Phase.InAdventure || leaveRequested) { Log("Play while already leaving: ignored"); return false; }
        try
        {
            var data = crawlDataField.GetValue(__instance) as Hearthstone.DungeonCrawl.IDungeonCrawlData;
            var config = AdventureConfig.Get();
            // The deck the deck step accepted, not SelectedDeckId (a stray deck click could have rewritten it).
            long hero = data?.SelectedHeroCardDbId ?? 0, deck = confirmedDeckId, power = data?.SelectedHeroPowerDbId ?? 0;
            long heroClass = 0;
            GameSaveDataManager.Get()?.GetSubkeyValue(ServerKey, GameSaveKeySubkeyId.DUNGEON_CRAWL_PLAYER_SELECTED_HERO_CLASS, out heroClass);
            Log("PLAY class=" + (TAG_CLASS)heroClass + "(" + heroClass + ")"
                + " hero=" + hero + ":" + CardId(hero) + " configHero=" + config?.SelectedHeroCardDbId
                + " deck=" + deck + ":" + FedDeckById(deck)?.Name + " selectedDeck=" + data?.SelectedDeckId + " configDeck=" + config?.SelectedDeckId
                + " power=" + power + ":" + CardId(power) + " configPower=" + config?.SelectedHeroPowerDbId);
            if (deck == 0 || power == 0) { Log("Play before deck and power were chosen: ignored"); return false; }
            var fedDeck = FedDeckById(deck);
            var fedPower = FedPowerByCard(power);
            if (fedRow == null || fedDeck == null || fedPower == null || fedDeck.Placeholder || fedPower.Placeholder)
            {
                Log("Play with a selection the picker did not feed (row=" + fedRow + " deck=" + fedDeck?.Name + " power=" + fedPower?.Option.Id + "): ignored");
                return false;
            }
            string deckId = fedDeck.Random ? RandomUnlockedDeck() : fedDeck.Option.Id;
            SetPlay(__instance, false);
            var row = chosenRow != null && chosenRow.Option.Id == fedRow ? chosenRow.Option : null;
            if (deckId != null && row != null && row.SignatureCount > 0)
            {
                // A Tombs explorer starts with a signature treasure: the menu's Signature step takes it, the rest already chosen.
                Log("row " + fedRow + " has " + row.SignatureCount + " signature treasures: HUB then the menu's Signature step (deck="
                    + deckId + " power=" + fedPower.Option.Id + ")");
                NativeDungeonRunUI.SignatureWhenHome(row.ClassId, fedRow, deckId, fedPower.Option.Id);
                leaveRequested = true; openMenu = false;
                return false;
            }
            string refusal = deckId == null ? "No unlocked deck to pick at random." : DungeonRunRuntime.NewRun(fedRow, deckId, fedPower.Option.Id);
            Log("NewRun row=" + fedRow + " deck=" + deckId + (fedDeck.Random ? " (random among " + string.Join(",", fedDecks.Where(d => !d.Random && d.Unlocked).Select(d => d.Option.Id)) + ")" : "")
                + " power=" + fedPower.Option.Id + " -> " + (refusal == null ? "started; HUB then the run hub" : "refused: " + refusal + "; HUB then the menu"));
            // A refusal is shown as the menu's caption (stay-on-playmat would need a native dialog in the borrowed scene).
            menuNotice = refusal;
            leaveRequested = true; openMenu = true;
        }
        catch (Exception error) { Abort("Play: " + error); }
        return false;
    }

    // ------------------------------------------------------------------ our content on the playmat
    //
    // Hooks, not injected DBF rows: the playmat reads its rows through the adventure adapter (GetDecksForClass ->
    // AdventureUtils.GetDecksForAdventureAndClass, GetHeroPowersForClass -> GetHeroPowersForAdventureAndClass), its lock state
    // through the adapter's unlock checks, and a deck's name and list through CollectionManager.LoadDeckFromDBF. Answering
    // those for the session keeps DALARAN's own rows untouched, needs no save subkeys for our unlocks, and leaves nothing in
    // GameDbf: the fed records live only in this class's lists, which End clears, and the reserved deck ids are answered only
    // while they are fed. Injected rows would also need DALARAN's rows hidden (the same adapter hook) and removing after.

    private static bool Feeding => InAdventure && Borrowed(AdventureConfig.Get());

    // DungeonCrawlDataAdventureAdapter.GetDecksForClass (ShowDeckOptionsWhenReady 006A; also the run-end graveyard, never reached here).
    private static bool FeedDecks(TAG_CLASS __0, ref List<AdventureDeckDbfRecord> __result)
    {
        if (!Feeding) return true;
        try
        {
            EnsureFeed(__0);
            __result = fedDecks.Select(d => d.Record).ToList();
            return false;
        }
        catch (Exception error) { Abort("feeding decks: " + error); __result = new List<AdventureDeckDbfRecord>(); return false; }
    }

    // DungeonCrawlDataAdventureAdapter.GetHeroPowersForClass (GetHeroPowersForDungeonCrawlHero 002C, from ShowHeroPowerOptionsWhenReady 0065).
    private static bool FeedPowers(TAG_CLASS __0, ref List<AdventureHeroPowerDbfRecord> __result)
    {
        if (!Feeding) return true;
        try
        {
            EnsureFeed(__0);
            __result = fedPowers.Select(p => p.Record).ToList();
            var display = crawlInstanceField.GetValue(null) as AdventureDungeonCrawlDisplay;
            var mat = display == null ? null : playMatField.GetValue(display) as AdventureDungeonCrawlPlayMat;
            // The deck outro has finished by now (the coroutine waited for it): these should all read active=False.
            Log("showing " + __result.Count + " hero powers; deck options now " + OptionStates(deckOptionsField, mat));
            return false;
        }
        catch (Exception error) { Abort("feeding hero powers: " + error); __result = new List<AdventureHeroPowerDbfRecord>(); return false; }
    }

    // DungeonCrawlDataAdventureAdapter.AdventureDeckIsUnlocked: ShowDecks asks it only for UnlockValue > 0 (0109-0116), i.e. our
    // locked decks; the locked text is then String.Format(UnlockCriteriaText, progress, UnlockValue) (0141-015B), our hint.
    private static bool FedDeckUnlocked(AdventureDeckDbfRecord __1, ref long __2, ref bool __3, ref bool __result)
    {
        var fed = Active ? fedDecks?.FirstOrDefault(d => ReferenceEquals(d.Record, __1)) : null;
        if (fed == null) return true;
        __2 = 0; __3 = true; __result = fed.Unlocked;
        return false;
    }

    // DungeonCrawlDataAdventureAdapter.AdventureHeroPowerIsUnlocked (ShowHeroPowers 0144-0156, same shape as the decks).
    private static bool FedPowerUnlocked(AdventureHeroPowerDbfRecord __1, ref long __2, ref bool __3, ref bool __result)
    {
        var fed = Active ? fedPowers?.FirstOrDefault(p => ReferenceEquals(p.Record, __1)) : null;
        if (fed == null) return true;
        __2 = 0; __3 = true; __result = fed.Option.Unlocked;
        return false;
    }

    // CollectionManager.LoadDeckFromDBF: only the reserved ids of the decks fed in this session. The deck option keeps the list
    // (m_deckContents) and hands it to OnDeckOptionSelected, which fills the deck list panel (SetUpDeckList).
    private static bool FedDeckContents(int __0, ref string __1, ref string __2, ref List<long> __result)
    {
        var fed = Active ? fedDecks?.FirstOrDefault(d => d.DeckId == __0) : null;
        if (fed == null) return true;
        __1 = fed.Name; __2 = fed.Description ?? ""; __result = new List<long>(fed.Cards);
        return false;
    }

    // AdventureDungeonCrawlPlayMat.PlayHeroPowerOptionSelected: callers are OnHeroPowerOptionSelected (0055) and GoBackFromHeroPower.
    // A pick keeps the powers shown (our last step, like retail's decks); only our back path dismisses them.
    private static bool KeepPowerOptions()
    {
        if (!InAdventure || allowPowerOutro) return true;
        Log("hero power picked: the hero power options stay shown (last step)");
        return false;
    }

    private static void ClearFeed()
    {
        fedRow = null; fedDecks = null; fedPowers = null; allowPowerOutro = false; confirmedDeckId = 0;
        var ids = overrideIds; overrideIds = null;
        DungeonRunRuntime.EndPicker();
        // After EndPicker: the next GetName rebuilds the client's own name.
        if (ids != null) ResetCachedNames(ids, "end");
    }

    // EntityDef.m_cachedEntityName keeps the first name built (UpdateEntityName 0029-007B, through CardTextBuilder.GetDefaultCardName,
    // the hooked method) for the launch. Reset for the override ids so the picker's names show (a def named earlier, e.g. a
    // basic hero power in the collection) and do not outlive it. Only defs already in the cache: none is created here.
    private static void ResetCachedNames(IEnumerable<string> ids, string when)
    {
        try
        {
            var loader = DefLoader.Get();
            if (loader == null) return;
            var cache = entityDefCacheField.GetValue(loader) as Dictionary<string, EntityDef>;
            if (cache == null) { Log("name cache reset (" + when + "): no DefLoader entity-def cache"); return; }
            object empty = Activator.CreateInstance(cachedNameField.FieldType);
            var reset = new List<string>();
            foreach (string id in ids)
                if (id != null && cache.TryGetValue(id, out EntityDef def) && def != null) { cachedNameField.SetValue(def, empty); reset.Add(id); }
            Log("name cache reset (" + when + "): " + reset.Count + " [" + string.Join(",", reset) + "]");
        }
        catch (Exception error) { Log("name cache reset (" + when + ") failed: " + error.GetType().Name + ": " + error.Message); }
    }

    private static FedDeck FedDeckById(long deckId) => deckId == 0 ? null : fedDecks?.FirstOrDefault(d => d.DeckId == deckId);
    private static FedPower FedPowerByCard(long cardDbId) => cardDbId == 0 ? null : fedPowers?.FirstOrDefault(p => p.CardDbId == cardDbId);

    private static string RandomUnlockedDeck()
    {
        var unlocked = fedDecks?.Where(d => !d.Random && d.Unlocked).ToList();
        if (unlocked == null || unlocked.Count == 0) return null;
        return unlocked[new System.Random(Guid.NewGuid().GetHashCode()).Next(unlocked.Count)].Option.Id;
    }

    // The row the playmat shows for a class: the row the tray's Choose took when it is of that class, else the class row.
    private static string RowFor(TAG_CLASS cls) => chosenRow != null && chosenRow.Class == cls ? chosenRow.Option.Id : cls.ToString();

    // Builds the session's rows for the chosen run row once (both steps and every back/forward reuse them).
    private static void EnsureFeed(TAG_CLASS cls)
    {
        string classId = RowFor(cls);
        if (fedRow == classId && fedDecks != null && fedPowers != null) return;
        ClearFeed();
        var decks = DungeonRunRuntime.DecksFor(classId);
        var powers = DungeonRunRuntime.PowersFor(classId);
        if (decks.Count == 0 || powers.Count == 0) throw new InvalidOperationException("Dungeon Run has no decks/powers for " + classId + " (decks=" + decks.Count + " powers=" + powers.Count + ")");
        var display = crawlInstanceField.GetValue(null) as AdventureDungeonCrawlDisplay;
        var mat = display == null ? null : playMatField.GetValue(display) as AdventureDungeonCrawlPlayMat;
        int deckSlots = mat == null ? -1 : OptionList(deckOptionsField, mat).Count, powerSlots = mat == null ? -1 : OptionList(powerOptionsField, mat).Count;
        string className = NativeDungeonRunText.ClassName(cls.ToString());
        var overrides = new Dictionary<string, DungeonRunRuntime.PickerCard>(StringComparer.Ordinal);
        var missing = new List<string>();

        // Borrowed pouch art: DALARAN's decks of this class, in their order (the Random pouch last).
        var art = GameDbf.AdventureDeck.GetRecords().Where(d => d.AdventureId == (int)Adventure && d.ClassId == (int)cls).OrderBy(d => d.SortOrder).ToList();
        var feed = new List<FedDeck>();
        foreach (var deck in decks)
        {
            feed.Add(new FedDeck
            {
                Option = deck, Unlocked = deck.Unlocked, Name = deck.Name, Description = deck.Description, Hint = deck.UnlockHint,
                Cards = ClientCards(deck.CardIds, overrides, missing),
            });
        }
        // The prefab has a 4th pouch (retail's "Random <Class> Deck"). ShowDecks breaks out of its loop when the rows run out
        // (00A6-00E7) and leaves that slot un-Init'd, and the playmat's TRANSITIONING wait (Update 0086-00AB) needs every deck
        // option ready and not mid-outro, so an empty slot risks a stuck deck -> power step. A 4th row keeps every slot
        // Init'd: Random, a pick among the UNLOCKED decks made at Play.
        // A row with fewer decks than pouches (a Monster Hunt hunter has one deck: with Random that is 2 rows for 4 pouches) left the
        // other pouches un-Init'd, and the playmat showed no deck options at all (owner's PC, GILA_500h3). Every pouch is fed: the
        // ones between the row's decks and Random (which stays last, on the Random pouch art) are locked placeholders whose hint
        // says there is no other deck, so they can never be picked (as a class row's locked decks, which the playmat already shows).
        bool unlockedDeck = feed.Any(d => d.Unlocked);
        while (deckSlots > feed.Count + 1)
            feed.Add(new FedDeck
            {
                Placeholder = true, Unlocked = false, Name = NativeDungeonRunText.NoOtherDeckName, Description = "",
                Hint = NativeDungeonRunText.NoOtherDeckHint, Cards = new List<long>(),
            });
        if (deckSlots > feed.Count)
        {
            int randomCard = CardDbId(RandomDeckCard);
            string name = "Random " + className + " Deck";
            string text = "Starts the run with one of your unlocked " + className + " decks, picked when you press Play.";
            if (randomCard > 0) overrides[RandomDeckCard] = new DungeonRunRuntime.PickerCard { Name = name, Text = text };
            feed.Add(new FedDeck
            {
                Random = true, Unlocked = unlockedDeck, Name = name, Description = text, Hint = NativeDungeonRunText.LockedWord,
                Cards = randomCard > 0 ? new List<long> { randomCard } : new List<long>(),
            });
        }
        if (deckSlots >= 0 && deckSlots != feed.Count) Log("WARNING deck slots=" + deckSlots + " but " + feed.Count + " decks fed");
        for (int i = 0; i < feed.Count; i++)
        {
            var d = feed[i];
            d.DeckId = FedDeckIdBase + i;
            if (GameDbf.Deck.GetRecord(d.DeckId) != null) throw new InvalidOperationException("reserved deck id " + d.DeckId + " is a client Deck row");
            var record = new AdventureDeckDbfRecord();
            record.SetID(d.DeckId);
            Set(record, "ADVENTURE_ID", (int)Adventure); Set(record, "CLASS_ID", (int)cls); Set(record, "DECK_ID", d.DeckId); Set(record, "SORT_ORDER", i);
            Set(record, "UNLOCK_GAME_SAVE_SUBKEY", 0); Set(record, "UNLOCK_VALUE", d.Unlocked ? 0 : 1);
            Set(record, "UNLOCK_CRITERIA_TEXT", FormatSafe(d.Unlocked ? "" : d.Hint ?? NativeDungeonRunText.LockedWord));
            Set(record, "UNLOCKED_DESCRIPTION_TEXT", d.Description ?? "");
            var pouch = art.Count == 0 ? null : art[d.Random ? art.Count - 1 : Math.Min(i, art.Count - 1)];
            if (pouch != null && !string.IsNullOrEmpty(pouch.DisplayTexture)) Set(record, "DISPLAY_TEXTURE", pouch.DisplayTexture);
            d.Record = record;
        }

        var powerFeed = new List<FedPower>();
        for (int i = 0; i < powers.Count; i++)
        {
            var p = powers[i];
            int card = CardDbId(p.ClientCardId);
            if (card <= 0) throw new InvalidOperationException("the client does not know power card " + p.ClientCardId + " (" + p.Id + ")");
            if (powerFeed.Any(f => f.CardDbId == card)) Log("WARNING powers share client card " + p.ClientCardId + ": Play maps it to the first");
            var record = new AdventureHeroPowerDbfRecord();
            record.SetID(FedPowerRecordIdBase + i);
            Set(record, "ADVENTURE_ID", (int)Adventure); Set(record, "CLASS_ID", (int)cls); Set(record, "GUEST_HERO_ID", 0); Set(record, "CARD_ID", card);
            Set(record, "SORT_ORDER", i); Set(record, "IS_DEFAULT", p.IsDefault);
            Set(record, "UNLOCK_GAME_SAVE_SUBKEY", 0); Set(record, "UNLOCK_VALUE", p.Unlocked ? 0 : 1); Set(record, "UNLOCK_ACHIEVEMENT", 0);
            Set(record, "UNLOCK_CRITERIA_TEXT", FormatSafe(p.Unlocked ? "" : p.UnlockHint ?? NativeDungeonRunText.LockedWord));
            Set(record, "UNLOCKED_DESCRIPTION_TEXT", "");
            powerFeed.Add(new FedPower { Option = p, CardDbId = card, Record = record });
            overrides[p.ClientCardId] = new DungeonRunRuntime.PickerCard { Name = p.Name, Text = p.Text, Cost = p.Cost };
        }
        // The same for the power slots (Rikkar has one power for three slots, a Monster Hunt hunter one or two): the rest are locked
        // placeholders on the first power's card. FedPowerByCard maps that card to the real power, which comes first.
        for (int i = powerFeed.Count; i < powerSlots && powerFeed.Count > 0; i++)
        {
            var first = powerFeed[0];
            var record = new AdventureHeroPowerDbfRecord();
            record.SetID(FedPowerRecordIdBase + i);
            Set(record, "ADVENTURE_ID", (int)Adventure); Set(record, "CLASS_ID", (int)cls); Set(record, "GUEST_HERO_ID", 0); Set(record, "CARD_ID", first.CardDbId);
            Set(record, "SORT_ORDER", i); Set(record, "IS_DEFAULT", false);
            Set(record, "UNLOCK_GAME_SAVE_SUBKEY", 0); Set(record, "UNLOCK_VALUE", 1); Set(record, "UNLOCK_ACHIEVEMENT", 0);
            Set(record, "UNLOCK_CRITERIA_TEXT", FormatSafe(NativeDungeonRunText.NoOtherPowerHint));
            Set(record, "UNLOCKED_DESCRIPTION_TEXT", "");
            var option = new RunPowerOption
            {
                Id = first.Option.Id, ShellCardId = first.Option.ShellCardId, ClientCardId = first.Option.ClientCardId, Name = first.Option.Name,
                Text = first.Option.Text, PlainText = first.Option.PlainText, Cost = first.Option.Cost, Targeted = first.Option.Targeted,
                IsDefault = false, Unlocked = false, UnlockHint = NativeDungeonRunText.NoOtherPowerHint, SortOrder = i,
            };
            powerFeed.Add(new FedPower { Option = option, CardDbId = first.CardDbId, Placeholder = true, Record = record });
        }
        if (powerSlots >= 0 && powerSlots != powerFeed.Count) Log("WARNING hero power slots=" + powerSlots + " but " + powerFeed.Count + " powers fed");

        // The tray's hero names stay on the playmat: its portrait shows the chosen row's name.
        foreach (var name in TrayNames()) if (!overrides.ContainsKey(name.Key)) overrides[name.Key] = name.Value;
        fedRow = classId; fedDecks = feed; fedPowers = powerFeed;
        DungeonRunRuntime.BeginPicker(overrides);
        overrideIds = overrides.Keys.ToList();
        ResetCachedNames(overrideIds, "begin");
        Log("fed decks row=" + classId + " slots=" + deckSlots + ": " + string.Join(" | ", feed.Select(d => "deckId=" + d.DeckId + " " + (d.Random ? "random" : d.Placeholder ? "placeholder" : d.Option.Id)
            + " name=" + d.Name + " " + (d.Unlocked ? "unlocked" : "locked(" + d.Hint + ")") + " cards=" + d.Cards.Count)));
        Log("fed powers row=" + classId + " slots=" + powerSlots + ": " + string.Join(" | ", powerFeed.Select(p => (p.Placeholder ? "placeholder " : "") + p.Option.Id + " card=" + p.Option.ClientCardId + "(" + p.CardDbId + ")"
            + " name=" + p.Option.Name + " cost=" + p.Option.Cost + " " + (p.Option.Unlocked ? "unlocked" : "locked(" + p.Option.UnlockHint + ")"))));
        Log("picker card overrides: " + overrides.Count + " [" + string.Join(",", overrides.Keys) + "]"
            + (missing.Count == 0 ? "" : "; deck cards the client does not know (left out): " + string.Join(",", missing)));
    }

    // A deck's engine ids as the client's card dbIds (as a run match sends them); shells get their run name and text.
    private static List<long> ClientCards(IEnumerable<string> engineIds, Dictionary<string, DungeonRunRuntime.PickerCard> overrides, List<string> missing)
    {
        var cards = new List<long>();
        foreach (string id in engineIds ?? Enumerable.Empty<string>())
        {
            string client = DungeonRunRuntime.ClientCardId(id);
            int dbId = CardDbId(client);
            if (dbId <= 0) { missing.Add(id); continue; }
            cards.Add(dbId);
            var shell = SabberStoneCore.CardSets.DungeonRun.DungeonRunContent.ForShell(client) ?? SabberStoneCore.CardSets.DungeonRun.DungeonRunContent.ForShell(id);
            if (shell != null && !overrides.ContainsKey(client)) overrides[client] = new DungeonRunRuntime.PickerCard { Name = shell.Name, Text = shell.Text };
        }
        return cards;
    }

    private static int CardDbId(string cardId)
    {
        if (string.IsNullOrEmpty(cardId)) return 0;
        int id = GameUtils.TranslateCardIdToDbId(cardId, false);
        return id > 0 && GameDbf.Card.GetRecord(id) != null ? id : 0;
    }

    // ShowDecks/ShowHeroPowers pass the text through String.Format.
    private static string FormatSafe(string text) => (text ?? "").Replace("{", "{{").Replace("}", "}}");

    private static void Set(DbfRecord record, string column, object value)
    {
        Type type = record.GetVarType(column) ?? throw new MissingFieldException(record.GetType().Name, column);
        if (type == typeof(DbfLocValue)) { var text = new DbfLocValue(); text.SetString((string)value ?? ""); value = text; }
        else if (type.IsEnum) value = Enum.ToObject(type, value);
        else if (value != null && value.GetType() != type) value = Convert.ChangeType(value, type);
        record.SetVar(column, value);
    }

    private static List<AdventureOptionWidget> OptionList(FieldInfo field, AdventureDungeonCrawlPlayMat mat) =>
        mat == null ? new List<AdventureOptionWidget>() : ((field.GetValue(mat) as IEnumerable) ?? new object[0]).OfType<AdventureOptionWidget>().ToList();

    // "[id active/outro, ...]": what blocks clicks is an active option with its Clickable on (PlayOutro turns it off at 0000-0002,
    // OnOutroFinished deactivates the GameObject at 0007-000E).
    private static string OptionStates(FieldInfo field, AdventureDungeonCrawlPlayMat mat)
    {
        try
        {
            return "[" + string.Join(", ", OptionList(field, mat).Select(o => o == null ? "null" : optionDbIdField.GetValue(o)
                + (o.gameObject.activeSelf ? " active" : " inactive") + (o.IsOutroPlaying ? " outro" : ""))) + "]";
        }
        catch (Exception error) { return "?" + error.GetType().Name; }
    }

    // GameMgr.FindGameInternal (FindGame and FindGameWithHero both end here): never a server game from the borrowed screens.
    private static bool BlockServerGame()
    {
        if (!InAdventure) return true;
        Abort("the client tried to queue a server game");
        return false;
    }

    // ------------------------------------------------------------------ guest heroes (only the borrowed adventure, only in a session)

    private static bool GuestsHidden(AdventureDbId adventure) => Active && adventure == Adventure;

    private static bool GuestsHidden(Hearthstone.DungeonCrawl.IDungeonCrawlData data)
    {
        if (!Active) return false;
        try { return GuestsHidden(data != null ? data.GetSelectedAdventure() : AdventureConfig.Get()?.GetSelectedAdventure() ?? 0); }
        catch (Exception) { return false; }
    }

    // AdventureConfig.GuestHeroesExistForCurrentAdventure (AdventureGuestHeroes.HasRecord for the selected adventure, IL 0000-0016):
    // true in a session, so SubSceneForPickingHeroForCurrentAdventure (via GetCorrectSubSceneWhenLoadingDungeonCrawlMode 0036) picks
    // ADVENTURER_PICKER (6), the portrait tray, and the adapter's copy sends the playmat's GoToHeroSelectSubscene (0006-0022) back
    // to it. Its other reader, SetUpHeroPortrait (0076), then passes NORMAL (no favourite-skin premium), as PlainHeroPortrait does.
    private static bool GuestTray(AdventureConfig __instance, ref bool __result)
    {
        if (!Active || __instance == null || !GuestsHidden(__instance.GetSelectedAdventure())) return true;
        __result = true; return false;
    }

    // ---- the portrait tray (owner 2026-11-20)

    // Every playable run row, class by class (the class row, then its named heroes, as the menu's Hero step lists them), with
    // the client card the tray draws and one of the client's GUEST_HERO records for that card (every class hero and every Heist,
    // Monster Hunt, Rumble and Tombs hero has at least one). A row the client cannot draw is left out with the reason logged.
    private static List<TrayRow> BuildTrayRows()
    {
        var rows = new List<TrayRow>();
        var skipped = new List<string>();
        var guests = GameDbf.GuestHero.GetRecords();
        foreach (var cls in DungeonRunRuntime.Classes)
        {
            if (!Enum.TryParse(cls.Id, out TAG_CLASS tag) || tag == TAG_CLASS.INVALID) { skipped.Add(cls.Id + "(no TAG_CLASS)"); continue; }
            foreach (var hero in DungeonRunRuntime.HeroesFor(cls.Id))
            {
                if (rows.Any(r => r.Option.Id == hero.Id)) continue;
                int card = CardDbId(hero.ClientHeroCardId);
                var guest = card <= 0 ? null : guests.FirstOrDefault(g => g.CardId == card);
                int decks = DungeonRunRuntime.DecksFor(hero.Id).Count, powers = DungeonRunRuntime.PowersFor(hero.Id).Count;
                if (card <= 0) skipped.Add(hero.Id + "(client card " + hero.ClientHeroCardId + " unknown)");
                else if (guest == null) skipped.Add(hero.Id + "(no GUEST_HERO for " + hero.ClientHeroCardId + ")");
                else if (decks == 0 || powers == 0) skipped.Add(hero.Id + "(decks=" + decks + " powers=" + powers + ")");
                else if (rows.Any(r => r.CardDbId == card)) skipped.Add(hero.Id + "(shares card " + hero.ClientHeroCardId + ")");
                else rows.Add(new TrayRow { Option = hero, Class = tag, CardDbId = card, GuestHeroId = guest.ID });
            }
        }
        if (skipped.Count != 0) Log("tray rows left out: " + string.Join(" ", skipped));
        return rows;
    }

    // The tray hooks act for the whole session (the tray may be built while the scene change to ADVENTURE is still pending).
    private static bool TrayFed => Active && trayRows != null;

    // Across every row, not only the page shown: BuildTrayRows gives each row its own card, so a card names one row whatever the
    // page (a repeated row on a short last page included). ChooseHero also checks that the row is on the page the tray was fed.
    private static TrayRow TrayRowByCard(int cardDbId) => cardDbId <= 0 ? null : trayRows?.FirstOrDefault(r => r.CardDbId == cardDbId);

    // The tray prefab's layouts by hero count (SetupHeroLayout needs a bone set at that index); null when it cannot be read.
    private static IList TrayBones(AbsDeckPickerTrayDisplay tray) => bonesByCountField.GetValue(tray) as IList;
    private static bool LaysOut(IList bones, int count) => bones != null && count > 0 && count < bones.Count && bones[count] is UnityEngine.Object bone && bone != null;

    // The largest hero count up to wanted that the tray's prefab lays out; -1 when the list cannot be read.
    private static int TrayCapacity(IList bones, int wanted)
    {
        if (bones == null) return -1;
        for (int n = Math.Min(wanted, bones.Count - 1); n > 0; n--)
            if (LaysOut(bones, n)) return n;
        return 0;
    }

    // GuestHeroPickerTrayDisplay.GetSortedAdventureGuestHeroes (GetGuestHeroes 0022; ValidateHeroCount through it): the rows, as
    // detached AdventureGuestHeroes records (never in GameDbf) that name the client's GUEST_HERO record of each row's card;
    // GetGuestHeroes(List) then reads those GUEST_HERO records from GameDbf. No wing, so no wing ownership or event lock. When the
    // prefab lays out fewer heroes than there are rows, the tray shows the session's page of them (class rows first, then the
    // named heroes in data order: NativeDungeonRunText.TrayPageRows) and the page controls reach the others (UpdatePager).
    private static bool FeedGuestHeroes(GuestHeroPickerTrayDisplay __instance, ref List<AdventureGuestHeroesDbfRecord> __result)
    {
        if (!TrayFed || !Borrowed(AdventureConfig.Get())) return true;
        try
        {
            var shown = trayRows;
            var bones = TrayBones(__instance);
            int capacity = TrayCapacity(bones, trayRows.Count);
            trayDisplay = __instance;
            trayPerPage = capacity > 0 && capacity < trayRows.Count ? capacity : 0;
            if (capacity == 0)
            {
                // No layout at all: nothing can be shown, so every row is truly out of reach.
                shown = new List<TrayRow>();
                Log("WARNING the tray lays out no hero count up to " + trayRows.Count + "; " + trayRows.Count + " rows left out: "
                    + string.Join(" ", trayRows.Select(r => r.Option.Id)));
            }
            else if (trayPerPage > 0)
            {
                int pages = NativeDungeonRunText.Pages(trayRows.Count, trayPerPage);
                trayPage = NativeDungeonRunText.ClampPage(trayPage, trayRows.Count, trayPerPage);
                var rows = NativeDungeonRunText.TrayPageRows(trayRows.Select(r => r.Option.IsHero).ToList(), trayPerPage, trayPage, n => LaysOut(bones, n));
                shown = rows.Select(i => trayRows[i]).ToList();
                Log(NativeDungeonRunText.TrayPageLog(trayPage, pages) + ": " + shown.Count + " of " + trayRows.Count + " rows (the tray lays out at most "
                    + capacity + "; Previous/Next reach the rest): " + string.Join(" ", shown.Select(r => r.Option.Id)));
            }
            var records = new List<AdventureGuestHeroesDbfRecord>();
            for (int i = 0; i < shown.Count; i++)
            {
                var record = new AdventureGuestHeroesDbfRecord();
                record.SetID(FedGuestRecordIdBase + i);
                record.SetAdventureId((int)Adventure); record.SetWingId(0); record.SetGuestHeroId(shown[i].GuestHeroId);
                record.SetBaseGuestHeroId(0); record.SetSortOrder(i); record.SetCustomScenario(0);
                records.Add(record);
            }
            trayShown = shown;
            Log("tray fed " + records.Count + " heroes (layout capacity " + capacity + " for " + trayRows.Count + " rows"
                + (trayPerPage > 0 ? ", page " + (trayPage + 1) + " of " + NativeDungeonRunText.Pages(trayRows.Count, trayPerPage) : "") + ")");
            __result = records;
            return false;
        }
        catch (Exception error) { Abort("feeding the tray: " + error); __result = new List<AdventureGuestHeroesDbfRecord>(); return false; }
    }
    private const int FedGuestRecordIdBase = 0x5D522000;

    // GuestHeroPickerTrayDisplay.HandleAdventureGuestHeroUnlockData: retail locks a button whose wing event is inactive (0103-010E)
    // and marks an unowned wing (00A0-00B5). A fed row is playable (the tray lists only available rows): no lock, no lock text,
    // no "new" badge. InitHeroPickerButtons has already unlocked and activated the button (012F-0139).
    private static bool FedGuestHeroUnlock(HeroPickerButton __1)
    {
        if (!TrayFed || __1 == null) return true;
        try
        {
            __1.SetLockReasonText("");
            var model = __1.GetDataModel();
            if (model != null) { model.IsTimelocked = false; model.IsUnowned = false; model.IsNewlyUnlocked = false; }
        }
        catch (Exception error) { Log("tray unlock data failed: " + error.GetType().Name + ": " + error.Message); }
        return false;
    }

    // GuestHeroPickerButton.UpdateDisplay sets the label to the GUEST_HERO name (0044-0057): the run row's name instead.
    private static void FedHeroLabel(GuestHeroPickerButton __instance)
    {
        if (!TrayFed || __instance == null) return;
        try
        {
            var row = TrayRowByCard(__instance.GetGuestHero()?.CardId ?? 0);
            if (row != null) __instance.SetClassname(row.Option.Name);
        }
        catch (Exception error) { Log("tray label failed: " + error.GetType().Name + ": " + error.Message); }
    }

    // GuestHeroPickerTrayDisplay.UpdateHeroInfo(fullDef, heroName, heroDescription, premium): the selected hero's big name and its
    // GUEST_HERO flavor text; the run row's name and what it plays instead.
    private static void FedHeroInfo(DefLoader.DisposableFullDef __0, ref string __1, ref string __2)
    {
        if (!TrayFed || __0 == null) return;
        try
        {
            var row = TrayRowByCard(CardDbId(__0.EntityDef?.GetCardId()));
            if (row == null) return;
            __1 = row.Option.Name;
            __2 = NativeDungeonRunText.TrayDescription(row.Option);
        }
        catch (Exception error) { Log("tray info failed: " + error.GetType().Name + ": " + error.Message); }
    }

    // The tray rows' hero cards answer with the run rows' names on the playmat (EnsureFeed's DungeonRunRuntime.BeginPicker).
    private static Dictionary<string, DungeonRunRuntime.PickerCard> TrayNames()
    {
        var names = new Dictionary<string, DungeonRunRuntime.PickerCard>(StringComparer.Ordinal);
        foreach (var row in trayRows ?? new List<TrayRow>())
            if (!string.IsNullOrEmpty(row.Option.ClientHeroCardId)) names[row.Option.ClientHeroCardId] = new DungeonRunRuntime.PickerCard { Name = row.Option.Name };
        return names;
    }

    // AbsDeckPickerTrayDisplay.OnPlayGameButtonReleased: the portrait tray's Choose (GuestHeroPickerTrayDisplay does not override
    // it). Retail starts with Network.IsLoggedIn (reconnect dialog offline, 0000-0020); here the row is taken from the button's
    // GUEST_HERO card, the button's class is set to the row's (a dual explorer's card may name another), and the dungeon-crawl
    // save-and-advance runs (it writes SelectedHeroCardDbId and DUNGEON_CRAWL_PLAYER_SELECTED_HERO_CLASS, then opens the playmat).
    private static bool ChooseHero(AbsDeckPickerTrayDisplay __instance)
    {
        if (!InAdventure || !(__instance is GuestHeroPickerTrayDisplay)) return true;
        try
        {
            if (phase != Phase.InAdventure || leaveRequested) { Log("Choose while leaving (" + phase + "): ignored"); return false; }
            var button = selectedHeroButtonField.GetValue(__instance) as HeroPickerButton;
            if (button == null) { Log("Choose with no hero selected: ignored"); return false; }
            int card = button.GetGuestHero()?.CardId ?? 0;
            var row = TrayRowByCard(card);
            if (row == null || trayShown != null && !trayShown.Contains(row))
            {
                Log("Choose on a hero the tray was not fed on this page (card " + card + (row == null ? "" : ", row " + row.Option.Id) + "): ignored");
                return false;
            }
            chosenRow = row;
            // No page change once a hero is chosen: the tray is on its way to the playmat (TickPager, FlipPage).
            trayChosen = true;
            HidePager();
            heroClassField.SetValue(button, row.Class);
            setPlayButtonEnabled.Invoke(__instance, new object[] { false });
            setHeroButtonsEnabled.Invoke(__instance, new object[] { false });
            Log("hero chosen " + row.Option.Id + " (" + row.Option.Name + ", " + row.Class + ") heroCardDbId=" + button.HeroCardDbId
                + " card=" + GameUtils.TranslateDbIdToCardId(button.HeroCardDbId, false));
            bool advanced = (bool)saveHeroAndAdvance.Invoke(__instance, null);
            if (!advanced) Abort("Choose did not advance to the playmat (DungeonCrawlPickHeroFirst false?)");
        }
        catch (Exception error) { Abort("Choose: " + error); }
        return false;
    }

    // ---- the tray's page controls (owner 2026-11-22)
    //
    // Two clones of the tray's own Back (AbsDeckPickerTrayDisplay.m_backButton, a UIBButton), made the way NativeDeckPickerGuide
    // makes its Guide button, shown only while the rows need more than one page, on the tray subscene, before Choose, with no
    // dialog up and the session not leaving: Previous (not on page 1) at the hero grid's left end and Next (not on the last page)
    // at its right end, on the empty plate under the grid, else level with Back (NativeDungeonRunText.TrayPagerPlace), never over
    // the grid, the buttons shown, Back or Choose. The grid is the full-capacity one (a full page's, remembered from Back's rect for
    // the short last page: TrayPagerGrid), so the plates sit at the same place on every page; they are placed from the live rects
    // every frame and, once up, checked against them (as NativeDeckPickerGuide's Touches). A failure retires them (rebuilt on the
    // next frame); the third disables them for the session and the page changes that follow it (PageCarry), and with no controls
    // the rows on the other pages cannot be chosen (a WARNING names them).

    private static void UpdatePager()
    {
        if (pagerDisabled) return;
        try
        {
            string state = TickPager();
            PagerState(state);
            // Waiting this long on the tray's own Back, camera or grid means the controls never come: the other pages are lost.
            bool stuck = state.StartsWith("waiting (no Back", StringComparison.Ordinal) || state.StartsWith("waiting (no camera", StringComparison.Ordinal)
                || state.StartsWith("waiting (Back not", StringComparison.Ordinal) || state.StartsWith("waiting (hero grid", StringComparison.Ordinal)
                || state.StartsWith("waiting (Back art", StringComparison.Ordinal);
            if (!stuck) pagerWaitSince = -1;
            else if (pagerWaitSince < 0) pagerWaitSince = Time.realtimeSinceStartup;
            else if (!pagerWaitWarned && Time.realtimeSinceStartup - pagerWaitSince > PagerWaitWindow)
            {
                pagerWaitWarned = true;
                PagerLost(state + " for " + PagerWaitWindow + "s");
            }
        }
        catch (Exception error)
        {
            Safe("page controls retire", RetirePager);
            pagerDisabled = ++pagerFailures >= 3;
            Log("page controls failed (" + pagerFailures + "): " + error.GetType().Name + ": " + error.Message
                + (pagerDisabled ? "; disabled for this session (and its page changes)" : "; rebuilt on the next frame"));
            if (pagerDisabled) PagerState("disabled after " + pagerFailures + " failures");
        }
    }

    private static string TickPager()
    {
        var tray = trayDisplay;
        int pages = trayPerPage > 0 && trayRows != null ? NativeDungeonRunText.Pages(trayRows.Count, trayPerPage) : 1;
        UIBButton back = pages > 1 && tray != null ? tray.m_backButton : null;
        bool onTray = AdventureConfig.Get()?.CurrentSubScene == Tray;
        if (!onTray) trayChosen = false; // the next visit of the tray (the playmat's way back) may page again
        string why = pages <= 1 ? "not needed (one page)" : tray == null ? "waiting (no tray)"
            : !onTray ? "waiting (not on the tray)"
            : trayChosen ? "hidden (a hero was chosen)"
            : back == null || !back.gameObject.activeInHierarchy ? "waiting (no Back button)"
            : UniversalInputManager.Get()?.IsDialogActive() == true ? "waiting (a dialog is up)" : "";
        if (why.Length != 0) { HidePager(); return why; }
        if (pageSource != back || pagePrevious == null || pageNext == null)
        {
            RetirePager();
            pageSource = back;
            pagePrevious = PageButton(back, NativeDungeonRunText.TrayPreviousLabel, -1);
            pageNext = PageButton(back, NativeDungeonRunText.TrayNextLabel, 1);
        }
        Camera camera = CameraUtils.FindFirstByLayerMask(1 << back.gameObject.layer);
        if (camera == null) { HidePager(); return "waiting (no camera)"; }
        var backRect = PagerRect(camera, back);
        if (backRect == null) { HidePager(); return "waiting (Back not laid out)"; }
        // The buttons shown: the union of this tray's portrait buttons on screen, and each of them (kept clear of the plates).
        (float X, float Y, float W, float H)? shown = null;
        var heroes = new List<(float X, float Y, float W, float H)>();
        foreach (var hero in tray.GetComponentsInChildren<HeroPickerButton>(false))
        {
            var rect = PagerRect(camera, hero);
            if (rect == null) continue;
            heroes.Add(rect.Value);
            if (shown == null) { shown = rect; continue; }
            var a = shown.Value; var b = rect.Value;
            float x = Math.Min(a.X, b.X), y = Math.Min(a.Y, b.Y);
            shown = (x, y, Math.Max(a.X + a.W, b.X + b.W) - x, Math.Max(a.Y + a.H, b.Y + b.H) - y);
        }
        // The plates are placed against the full-capacity grid (a full page's, remembered from Back's rect for the short last
        // page), so they sit at the same place on every page.
        var grid = NativeDungeonRunText.TrayPagerGrid(trayGrid, shown, heroes.Count, trayPerPage, backRect.Value, Screen.width, Screen.height, out string gridSource);
        if (grid == null) { HidePager(); return "waiting (hero grid not laid out)"; }
        // Back's art reach past its body: unknown art means the plates cannot be kept off the controls yet (as NativeDeckPickerGuide.Place).
        FrameMargin? frame = NativeButtonKit.FrameMarginOf(back);
        if (frame == null) { HidePager(); return "waiting (Back art unknown)"; }
        FrameMargin margin = frame.Value;
        var avoid = new List<(float X, float Y, float W, float H)>();
        var choose = PagerRect(camera, trayPlayField.GetValue(tray) as Component);
        if (choose != null) avoid.Add(choose.Value);
        if (shown != null) avoid.Add(shown.Value);
        bool needPrevious = trayPage > 0, needNext = trayPage < pages - 1;
        var place = NativeDungeonRunText.TrayPagerPlace(grid.Value, backRect.Value, Math.Max(margin.Left, margin.Right), margin.Vertical, avoid, Screen.height,
            needPrevious, needNext);
        string where = "tray " + gridSource + " " + Fmt(grid.Value) + (gridSource == "full" || shown == null ? "" : " shown " + Fmt(shown.Value))
            + " back " + Fmt(backRect.Value) + (choose == null ? "" : " choose " + Fmt(choose.Value));
        if (place == null) { HidePager(); return "no free place | " + where; }
        var spot = place.Value;
        // The clones' pivots sit where Back's does, so each moves by its plate's offset from Back's (as NativeDeckPickerGuide.Place).
        Vector3 from = camera.WorldToScreenPoint(back.transform.position);
        float backX = backRect.Value.X + backRect.Value.W / 2, backY = backRect.Value.Y + backRect.Value.H / 2;
        float plateY = spot.Y + backRect.Value.H / 2 - backY;
        bool wasShown = pagePrevious.gameObject.activeSelf || pageNext.gameObject.activeSelf;
        ShowPageButton(pagePrevious, NativeDungeonRunText.TrayPreviousLabel, needPrevious, camera, back,
            from + new Vector3(spot.PreviousX + backRect.Value.W / 2 - backX, plateY, 0));
        ShowPageButton(pageNext, NativeDungeonRunText.TrayNextLabel, needNext, camera, back,
            from + new Vector3(spot.NextX + backRect.Value.W / 2 - backX, plateY, 0));
        // Once a plate has been up a frame, its own bounds must clear Back, Choose and every button shown (NativeDeckPickerGuide's
        // Touches): a pivot or scale off from the plan fails closed (UpdatePager retires the plates) rather than sit on a control.
        if (wasShown)
        {
            var controls = new List<(float X, float Y, float W, float H)>(heroes) { backRect.Value };
            if (choose != null) controls.Add(choose.Value);
            foreach (var (plate, label) in new[] { (pagePrevious, NativeDungeonRunText.TrayPreviousLabel), (pageNext, NativeDungeonRunText.TrayNextLabel) })
            {
                var mine = plate.gameObject.activeSelf ? PagerRect(camera, plate) : null;
                if (mine != null && controls.Any(c => NativeDungeonRunText.TrayRectsOverlap(mine.Value, c)))
                    throw new InvalidOperationException("tray page control " + label + " " + Fmt(mine.Value) + " overlaps a tray control (" + spot.Where + ")");
            }
        }
        return "shown on " + NativeDungeonRunText.TrayPageLog(trayPage, pages) + " " + spot.Where + " | " + where;
    }

    private static (float X, float Y, float W, float H)? PagerRect(Camera camera, Component control)
    {
        if (control == null || !control.gameObject.activeInHierarchy) return null;
        Rect rect = NativeRematchUI.ScreenRect(camera, control.gameObject);
        return rect.width > 0 && rect.height > 0 ? (rect.x, rect.y, rect.width, rect.height) : ((float X, float Y, float W, float H)?)null;
    }

    private static string Fmt((float X, float Y, float W, float H) r) => "[" + (int)r.X + "," + (int)r.Y + "," + (int)r.W + "," + (int)r.H + "]";

    // A Back clone that turns the page by delta (NativeDeckPickerGuide.Create's steps; NativeButtonKit.Fit keeps Back's plate and
    // bounds the label to its cream window, and never throws: an unsliced plate keeps its art).
    private static UIBButton PageButton(UIBButton back, string label, int delta)
    {
        UIBButton clone = null;
        try
        {
            clone = UnityEngine.Object.Instantiate(back, back.transform.parent);
            clone.name = "SoloStoneTrayPage" + label;
            clone.ClearEventListeners();
            clone.BubbleUpEvent = "";
            clone.SetText(label);
            var fit = NativeButtonKit.Fit(clone);
            clone.gameObject.SetActive(false);
            clone.SetEnabled(true);
            clone.AddEventListener(UIEventType.RELEASE, _ => FlipPage(delta));
            Log("page control " + label + " created from " + back.name + " (" + (fit.Sliced ? "3-slice plate" : "unsliced plate")
                + (fit.Detail.Length == 0 ? "" : ", " + fit.Detail) + ")");
            return clone;
        }
        catch
        {
            if (clone != null) UnityEngine.Object.Destroy(clone.gameObject);
            throw;
        }
    }

    private static void ShowPageButton(UIBButton button, string label, bool shown, Camera camera, UIBButton back, Vector3 screen)
    {
        if (button == null) return;
        if (!shown) { if (button.gameObject.activeSelf) button.gameObject.SetActive(false); return; }
        button.transform.localRotation = back.transform.localRotation;
        button.transform.position = camera.ScreenToWorldPoint(screen);
        if (!button.gameObject.activeSelf)
        {
            button.gameObject.SetActive(true);
            button.SetText(label); // in case the cloned label re-localized while hidden (as NativeDeckPickerGuide)
        }
    }

    private static void HidePager()
    {
        foreach (var button in new[] { pagePrevious, pageNext })
            if (button != null && button.gameObject.activeSelf) button.gameObject.SetActive(false);
    }

    private static void RetirePager()
    {
        foreach (var button in new[] { pagePrevious, pageNext })
            if (button != null) UnityEngine.Object.Destroy(button.gameObject);
        pagePrevious = pageNext = pageSource = null;
    }

    // Logged when the state changes (the rects after " | " are logged with it but are not part of it, so a tray that is still
    // sliding in logs once). With no controls the rows on the other pages are out of reach: those states are WARNINGs naming them.
    private static void PagerState(string state)
    {
        int cut = state.IndexOf(" | ", StringComparison.Ordinal);
        string key = cut < 0 ? state : state.Substring(0, cut);
        if (key == pagerState) return;
        pagerState = key;
        bool lost = state.StartsWith("no free place", StringComparison.Ordinal) || state.StartsWith("disabled", StringComparison.Ordinal);
        if (lost) PagerLost(state); else Log("tray page controls " + state);
    }

    private static void PagerLost(string state)
    {
        var reachable = new HashSet<TrayRow>(trayShown ?? new List<TrayRow>());
        Log("WARNING tray page controls " + state + ": the rows on the other pages cannot be chosen this session: "
            + string.Join(" ", (trayRows ?? new List<TrayRow>()).Where(r => !reachable.Contains(r)).Select(r => r.Option.Id)));
    }

    // Previous (-1) or Next (+1) on the tray. The tray reads its heroes only while it is built, so the new page is shown by
    // rebuilding it through the picker's own entry: this session leaves for the HUB as the tray's Back does (Tick: Leaving),
    // and at the HUB the picker opens again on the new page (NativeDungeonRunUI.PickerWhenHome -> Open(page, true)), which also
    // clears the selection and Hero Info. Any fallback on the way opens the menu instead (Abort). The reopen keeps the borrowed
    // client key's in-memory "has seen" flags (ClearBorrowedSaves), so a first-entry intro does not replay on each page; the
    // rest of the entry (save-key request, scene load, tray build) runs again on every page change: the cost of the round trip.
    private static void FlipPage(int delta)
    {
        try
        {
            if (!InAdventure || phase != Phase.InAdventure || leaveRequested || trayRows == null || trayPerPage <= 0) return;
            // Only from the tray itself, before Choose, with no dialog up, and only through a plate that is up and enabled.
            var plate = delta < 0 ? pagePrevious : pageNext;
            string refused = pagerDisabled ? "the page controls are disabled"
                : AdventureConfig.Get()?.CurrentSubScene != Tray ? "not on the tray"
                : trayChosen ? "a hero was chosen on this visit of the tray"
                : UniversalInputManager.Get()?.IsDialogActive() == true ? "a dialog is up"
                : plate == null || !plate.gameObject.activeInHierarchy || !plate.IsEnabled() ? "the control is not up" : "";
            if (refused.Length != 0) { Log("tray page " + (delta < 0 ? "Previous" : "Next") + " ignored: " + refused); return; }
            int pages = NativeDungeonRunText.Pages(trayRows.Count, trayPerPage);
            int target = NativeDungeonRunText.ClampPage(trayPage + delta, trayRows.Count, trayPerPage);
            if (target == trayPage) return;
            Log(NativeDungeonRunText.TrayPageLog(target, pages) + " asked on page " + (trayPage + 1) + ": rebuilding the tray (HUB, then the picker on that page)");
            trayPage = target; reopenTray = true;
            leaveRequested = true; openMenu = false;
            HidePager();
            var tray = trayDisplay;
            if (tray != null)
            {
                setPlayButtonEnabled.Invoke(tray, new object[] { false });
                setHeroButtonsEnabled.Invoke(tray, new object[] { false });
            }
        }
        catch (Exception error) { Abort("tray page: " + error); }
    }

    // AdventureUtils.GetGuestHeroesForAdventure: AdventureConfig.GetGuestHeroesForCurrentAdventure and so IDungeonCrawlData's
    // GetGuestHeroesForCurrentAdventure. With none, GetHeroCardIdFromClassForDungeonCrawl leaves its class -> guest card loop (0058).
    private static bool NoGuestHeroList(AdventureDbId __0, ref List<Hearthstone.DungeonCrawl.GuestHero> __result)
    {
        if (!GuestsHidden(__0)) return true;
        __result = new List<Hearthstone.DungeonCrawl.GuestHero>(); return false;
    }

    // AdventureUtils.GetGuestHeroIdFromHeroCardDbId: "not a guest hero" (0) without its Debug.LogError for an empty list (000F-0051);
    // DungeonCrawlBossKillCounter.UpdateLayout calls it unconditionally (0077) and drops the result.
    private static bool NoGuestHeroId(Hearthstone.DungeonCrawl.IDungeonCrawlData __0, ref int __result)
    {
        if (!GuestsHidden(__0)) return true;
        __result = 0; return false;
    }

    // AdventureUtils.GetHeroCardIdFromClassForDungeonCrawl: the class's vanilla hero (the playmat's PlayerHeroData.UpdateHeroDataFromClass
    // and InitializeFromGameSaveData 0422 set the portrait and SelectedHeroCardDbId from it; ShowHeroPowerOptionsWhenReady maps that
    // card back to its class). Retail without guest heroes takes CollectionManager.GetRandomFavoriteHero (0028), a random skin.
    private static bool VanillaHeroForClass(Hearthstone.DungeonCrawl.IDungeonCrawlData __0, TAG_CLASS __1, ref string __result)
    {
        if ((int)__1 == 0 || !GuestsHidden(__0)) return true;
        try
        {
            // The tray's chosen row of this class draws its own hero (a named hero's portrait), else the class's vanilla hero.
            if (chosenRow != null && chosenRow.Class == __1 && !string.IsNullOrEmpty(chosenRow.Option.ClientHeroCardId))
            {
                __result = chosenRow.Option.ClientHeroCardId; return false;
            }
            string vanilla = CollectionManager.GetVanillaHero(__1);
            if (string.IsNullOrEmpty(vanilla)) return true;
            __result = vanilla; return false;
        }
        catch (Exception error) { Log("vanilla hero for " + __1 + " failed: " + error.Message); return true; }
    }

    // AdventureDungeonCrawlDisplay.SetHero(cardID, premium), called only from SetUpHeroPortrait (00A6). With guest heroes hidden that
    // passes GOLDEN when the class's favourite hero is a skin (004A-0096), but the card shown is the vanilla hero, so it would render
    // a gold vanilla hero. Retail with a vanilla favourite passes NORMAL; SetHero's m_AlwaysRenderGoldenPortrait (0076) is unchanged.
    private static void PlainHeroPortrait(ref TAG_PREMIUM __1)
    {
        if (!GuestsHidden((Hearthstone.DungeonCrawl.IDungeonCrawlData)null)) return;
        __1 = TAG_PREMIUM.NORMAL;
    }

    // The tray's classes. A run class row whose id is no TAG_CLASS name (a dual-class row, "MAGE_ROGUE") is never one of them,
    // so the tray never offers it: dual rows start from the plank setup flow (NativeDungeonRunUI) only.
    private static IEnumerable<TAG_CLASS> PickerClasses =>
        GameDbf.AdventureDeck.GetRecords().Where(d => d.AdventureId == (int)Adventure).Select(d => (TAG_CLASS)d.ClassId).Distinct().OrderBy(c => (int)c);

    private static Navigation.NavigateBackHandler LoadoutBackHandler() =>
        (Navigation.NavigateBackHandler)Delegate.CreateDelegate(typeof(Navigation.NavigateBackHandler), loadoutNavigateBack);

    // The playmat's Play, and on phone layouts also the deck-select tray's (OnDeckOptionSelected IL 00E9-010A enables that one).
    private static void SetPlay(AdventureDungeonCrawlDisplay display, bool enabled)
    {
        var buttons = new List<PlayButton> { (playMatField.GetValue(display) as AdventureDungeonCrawlPlayMat)?.PlayButton };
        if ((bool)UniversalInputManager.UsePhoneUI) buttons.Add((deckSelectField.GetValue(display) as DungeonCrawlDeckSelect)?.playButton);
        foreach (var button in buttons)
        {
            if (button == null) continue;
            if (enabled) button.Enable(); else button.Disable(false);
        }
    }

    // The playmat prefab's option visuals (ShowDecks/ShowHeroPowers fill min(rows, visuals) and warn on a mismatch).
    private static string Slots(AdventureDungeonCrawlDisplay display)
    {
        try
        {
            var mat = playMatField.GetValue(display);
            if (mat == null) return "no-playmat";
            return "decks=" + ((deckOptionsField.GetValue(mat) as ICollection)?.Count ?? -1) + " powers=" + ((powerOptionsField.GetValue(mat) as ICollection)?.Count ?? -1);
        }
        catch (Exception error) { return "?" + error.GetType().Name; }
    }

    private static void Run(AdventureDungeonCrawlDisplay display, MethodInfo coroutine, string what)
    {
        Log("showing " + what);
        display.StartCoroutine((IEnumerator)coroutine.Invoke(display, null));
    }

    // ------------------------------------------------------------------ diagnostics (GameDbf at run time, once per launch)

    private static string CardId(long dbId)
    {
        try { return dbId == 0 ? "-" : GameUtils.TranslateDbIdToCardId((int)dbId, false) ?? "?"; }
        catch (Exception) { return "?"; }
    }

    private static string DeckName(int deckId)
    {
        try { var deck = GameDbf.Deck.GetRecord(deckId); return deck == null ? "-" : (string)deck.Name; }
        catch (Exception) { return "?"; }
    }

    private static List<string> DeckCards(int deckId)
    {
        var cards = new List<string>();
        var deck = GameDbf.Deck.GetRecord(deckId);
        for (int id = deck?.TopCardId ?? 0, guard = 0; id != 0 && guard < 80; guard++)
        {
            var card = GameDbf.DeckCard.GetRecord(id);
            if (card == null) { cards.Add("missing#" + id); break; }
            cards.Add(CardId(card.CardId));
            id = card.NextCard;
        }
        return cards;
    }

    private static void Safe(string what, Action action)
    {
        try { action(); }
        catch (Exception error) { Log("diagnostic " + what + " failed: " + error.GetType().Name + ": " + error.Message); }
    }

    // AdventureConfig.GuestHeroesExistForCurrentAdventure's test, for the borrowed adventure whatever is selected: with guest heroes
    // SubSceneForPickingHeroForCurrentAdventure opens ADVENTURER_PICKER (GuestHeroPickerTrayDisplay), which ChooseClass does not cover.
    private static int GuestHeroRows() => GameDbf.AdventureGuestHeroes.GetRecords().Count(r => r.AdventureId == (int)Adventure);

    private static void DumpBorrowed()
    {
        if (dumped) return;
        dumped = true;
        Safe("adventure-data", () => {
            var r = GameUtils.GetAdventureDataRecord((int)Adventure, (int)Mode);
            if (r == null) { Log("adventure-data none for " + Adventure + "/" + Mode); return; }
            Log("adventure-data id=" + r.ID + " name=" + (string)r.Name + " startingSubscene=" + r.StartingSubscene
                + " serverKey=" + (GameSaveKeyId)r.GameSaveDataServerKey + "(" + r.GameSaveDataServerKey + ")"
                + " clientKey=" + (GameSaveKeyId)r.GameSaveDataClientKey + "(" + r.GameSaveDataClientKey + ")"
                + " DungeonCrawlSelectChapter=" + r.DungeonCrawlSelectChapter + " DungeonCrawlPickHeroFirst=" + r.DungeonCrawlPickHeroFirst
                + " skipHeroSelect=" + r.DungeonCrawlSkipHeroSelect + " mustPickShrine=" + r.DungeonCrawlMustPickShrine
                + " saveHeroUsingHeroDbId=" + r.DungeonCrawlSaveHeroUsingHeroDbId + " retire=" + r.DungeonCrawlIsRetireSupported
                + " deckFromScenario=" + r.DungeonCrawlDefaultToDeckFromUpcomingScenario + " ignoreHeroUnlock=" + r.IgnoreHeroUnlockRequirement
                + " bossCardBack=" + r.BossCardBack);
        });
        Safe("decks", () => {
            var rows = GameDbf.AdventureDeck.GetRecords().Where(d => d.AdventureId == (int)Adventure).ToList();
            Log("adventure-deck rows=" + rows.Count);
            foreach (var group in rows.GroupBy(d => d.ClassId).OrderBy(g => g.Key))
                Log("adventure-deck class=" + (TAG_CLASS)group.Key + " count=" + group.Count() + " " + string.Join(" | ", group.OrderBy(d => d.SortOrder).Select(d =>
                {
                    var cards = DeckCards(d.DeckId);
                    return "deckId=" + d.DeckId + " name=" + DeckName(d.DeckId) + " sort=" + d.SortOrder + " unlock=" + (GameSaveKeySubkeyId)d.UnlockGameSaveSubkey
                        + ">=" + d.UnlockValue + " cards=" + cards.Count + "[" + string.Join(",", cards) + "]";
                })));
        });
        Safe("hero-powers", () => {
            var rows = GameDbf.AdventureHeroPower.GetRecords().Where(p => p.AdventureId == (int)Adventure).ToList();
            Log("adventure-hero-power rows=" + rows.Count);
            foreach (var group in rows.GroupBy(p => p.ClassId).OrderBy(g => g.Key))
                Log("adventure-hero-power class=" + (TAG_CLASS)group.Key + " count=" + group.Count() + " " + string.Join(" | ", group.OrderBy(p => p.SortOrder).Select(p =>
                    "card=" + p.CardId + ":" + CardId(p.CardId) + " sort=" + p.SortOrder + " guestHero=" + p.GuestHeroId
                    + " unlock=" + (GameSaveKeySubkeyId)p.UnlockGameSaveSubkey + ">=" + p.UnlockValue + " achievement=" + p.UnlockAchievement)));
        });
        Safe("scenarios", () => {
            foreach (var s in GameDbf.Scenario.GetRecords().Where(s => s.AdventureId == (int)Adventure).OrderBy(s => s.ID))
                Log("scenario id=" + s.ID + " mode=" + (AdventureModeDbId)s.ModeId + " wing=" + s.WingId + " name=" + (string)s.Name
                    + " ClassExclusions=[" + string.Join(",", (s.ClassExclusions ?? new List<ClassExclusionsDbfRecord>()).Select(c => ((TAG_CLASS)c.ClassId).ToString())) + "]");
        });
        Safe("guest-heroes", () => Log("adventure-guest-heroes rows=" + GuestHeroRows()
            + " GuestHeroesExistForCurrentAdventure(selected " + AdventureConfig.Get()?.GetSelectedAdventure() + ")=" + AdventureConfig.Get()?.GuestHeroesExistForCurrentAdventure()));
        Safe("client-state", () => Log("client-state HasUnlockedAllDefaultHeroes=" + GameModeUtils.HasUnlockedAllDefaultHeroes()
            + " rotated=" + GameUtils.IsAdventureRotated(Adventure) + " phoneUI=" + (bool)UniversalInputManager.UsePhoneUI));
        Safe("event", () => Log("client-state IsAdventureEventActive(original)=" + AdventureConfig.IsAdventureEventActive(Adventure)));
    }
}
