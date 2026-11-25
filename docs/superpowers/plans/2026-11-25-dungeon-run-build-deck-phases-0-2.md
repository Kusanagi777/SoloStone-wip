# Dungeon Run Build Deck (phases 0-2) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Every Random starting deck in Dungeon Run becomes "Build Deck": the player builds a legal 10-card starter from
their collection (run-menu plank builder for now), picks a hero power on the run menu's Power step, and the run starts
with that deck. A client-reading tool prepares phase 3 (the Collection Manager editor, a second plan).

**Architecture:** The rules (which cards, copies, ownership, 10 cards, runes) are one pure module in `plugin/DungeonRun`
used by the builder screen and re-checked at run start. Each row gets exactly one "build" deck option: a new data deck
(`"build": true`) on class, Rikkar and Monster Hunt rows; the existing generated Random deck on Heist/Tombs rows. A built
run records that deck id, a `BuiltDeck` save flag, and counts as all of its row's decks for treasures. The native picker
hands a Build Deck choice to the run menu, which hosts the plank builder.

**Tech Stack:** C# (net48 plugin, netstandard engine, net10 verify suites), System.Reflection.Metadata (launcher-test tool).

**Spec:** `docs/superpowers/specs/2026-11-25-dungeon-run-build-deck-design.md` (approved 2026-11-25). Phases 3-4 of the
spec (Collection Manager editor mode, dual/DK editor work) are a second plan written after Task 1's output. Deviation
from the spec's phase 2: instead of a stub list, phase 2 builds the real plank builder (the spec's fallback, section 7),
so Build Deck works end to end before the editor exists; phase 3 then moves rows to the Collection Manager where the
client allows it.

## Global Constraints

- Starter size: exactly `Rules.StarterSize` (10); never a literal 10 outside tests.
- Copies: at most 2 per copy group, 1 for a legendary; never more than owned per card id (golden + normal together).
- Card source: the hero row's `ClassSet` or neutral; a dual row any mix; admitted by
  `DungeonRunValidator.PoolCardReason(id, classSet, false, policy, catalogRun: true) == null`; no hero cards, no Fabled
  primaries or companions; rune-cost cards only on DEATHKNIGHT rows, whose deck holds at most 3 runes.
- Build Deck is always unlocked; nothing is saved for reuse; built runs count toward stats and unlocks.
- A built run counts as every deck of its row for treasure scope and deck treasure tags.
- `plugin/DungeonRun/*.cs` must not reference `LocalProfile`, `LocalCardCatalog` or client types (verify suites compile
  that folder alone): profile facts come in as `Func<string,int>` / `Func<string,string>`.
- The plugin cannot be compiled here: client-facing edits copy existing usage patterns only; files LF, no BOM.
- Commits: Conventional Commits ending with the session trailers (Co-Authored-By / Claude-Session lines).
- Run every build/run through `scratchpad/hrun.sh`, then `dotnet build-server shutdown`.

## Review Focus

1. A row whose owned, legal cards cannot reach 10: the builder says so, Done stays disabled, Back works (Task 6 check).
2. A golden and a normal copy, or a Core reprint and its original, of one card: one copy limit, ownership summed per id
   (Task 2 check BD5).
3. The collection changes between building and Play (a card no longer owned): the run start refuses with a reason
   (Task 5 runtime re-check, Task 4 check BD12 for the pure refusal).
4. A built run resumed after a restart: `BuiltDeck` and the generated/build StarterDeck id survive the save round trip
   and resume is not refused (Task 4 check BD11).
5. A Death Knight build with 4 runes, and a dual Tombs build that is all one class: refused and allowed respectively
   (Task 2 checks BD6, BD7).

---

### Task 1: Client member dump tool (phase 0)

**Files:**
- Create: `installer/SoloStone.Launcher.Tests/MemberDump.cs`
- Create: `installer/build-deck-members.txt`
- Modify: `installer/SoloStone.Launcher.Tests/ToolMode.cs:57-59` (dispatch + usage line)
- Modify: `installer/SoloStone.Launcher.Tests/Program.cs` (call the new check before `BoundaryTests()`)

**Interfaces:**
- Produces: `tool members <assembly.dll> <list.txt>`; `MemberDump.Describe(string assemblyPath, string typePattern, IReadOnlyList<string> members) -> string`.

- [ ] **Step 1: Write the failing check** `MemberDumpTests()` in `MemberDump.cs` (partial class Program): describes
  `typeof(Program).Assembly.Location` with type `SoloStone.Minisign` and member `Verify`; asserts the text contains
  `"Verify("`, `"IL "` and `"calls:"`; and with pattern `"SoloStone.Minisign*"` lists at least one type name. Call it from
  `Program.Main` next to `MinisignTests(temp)`.
- [ ] **Step 2: Run** the full suite `dotnet run --project installer/SoloStone.Launcher.Tests -c Release`. Expected: build
  error (MemberDump missing).
- [ ] **Step 3: Implement** `MemberDump` with `PEReader`/`MetadataReader` and BindingScan's signature provider
  (`installer/SoloStone.Launcher.Tests/BindingScan.cs:39-66`). A pattern ending in `*` lists matching full type names;
  otherwise print every field (type name), property and method (decoded signature) of the type, and for each member
  named in `members`: `IL <size>`, `calls:` the distinct called member references (`call`/`callvirt`/`newobj`
  operands) and `ints:` the distinct `ldc.i4*` values. `tool members` reads the list file: one line per type, optional
  space-separated member names; `#` comments.
- [ ] **Step 4: Write `installer/build-deck-members.txt`** (types and members the spec's editor mode needs):
  `CollectionManagerDisplay ShowDeck Exit`, `CollectionManager OnDeckCreated GetDeck GetEditedDeck`,
  `CollectionDeck GetClasses HasClass GetRuleset AddCard get_IsValidForRuleset GetMaxCardCount`,
  `DeckRuleset IsDeckValid`, `DeckRule_*`, `CollectionDeckTray OnBackOutOfDeckContentsImpl DeleteEditingDeck`,
  `CollectionPageManager UpdateFiltersForDeck`, `CollectibleDisplay UpdateCurrentPageCardLocks`, `CollectibleCard`,
  `DeckRuleset*`.
- [ ] **Step 5: Run the suite.** Expected: `PASS <n> installer checks`.
- [ ] **Step 6: Commit** `feat(installer): tool members dumps client type members and IL summaries (Build Deck phase 0)`.
- [ ] **Step 7 (owner, PC):** `dotnet run --project installer/SoloStone.Launcher.Tests -c Release -- tool members
  references/game/Assembly-CSharp.dll installer/build-deck-members.txt > members.txt` and paste `members.txt`.

### Task 2: Build rules (pure)

**Files:**
- Create: `plugin/DungeonRun/DungeonRunBuild.cs`
- Test: `verify/suites/p9/c-sched-9/Program.cs` (new section `BD`, checks BD1-BD8)

**Interfaces:**
- Produces:
  - `internal static string DungeonRunBuild.CardReason(string id, IReadOnlyList<string> classSet, OfflinePoolPolicy policy)`
    -> null when allowed, else a code: a `PoolCardReason` code, `"hero"`, `"fabled"`, `"rune-class"`.
  - `internal static IReadOnlyList<string> DungeonRunBuild.DeckReasons(IReadOnlyList<string> cards, IReadOnlyList<string> classSet, int size, Func<string,int> owned, Func<string,string> copyGroup, OfflinePoolPolicy policy)`
    -> empty when legal; reasons `"size <n> of <size>"`, `"copies <group>"`, `"legendary <group>"`, `"owned <id>"`,
    `"runes"`, `"<code> <id>"`. `owned == null` skips ownership; `copyGroup == null` groups by id.
  - `internal static string DungeonRunBuild.AddReason(IReadOnlyList<string> cards, string id, IReadOnlyList<string> classSet, int size, Func<string,int> owned, Func<string,string> copyGroup, OfflinePoolPolicy policy)`
    -> the first reason adding `id` breaks (size counted as "full" at `size`), or null.

- [ ] **Step 1: Write failing checks** (section `BD`, data from the shipped run rows):
  - BD1 a MAGE row allows a scripted, admitted mage card and a neutral; refuses a warrior card (`class`).
  - BD2 a hero card (CardType.HERO in the engine) -> `"hero"`; a Fabled primary and a companion -> `"fabled"`.
  - BD3 10 legal cards -> no reasons; 9 and 11 -> `"size 9 of 10"` / `"size 11 of 10"`.
  - BD4 3 copies -> `copies`; 2 of a legendary -> `legendary`.
  - BD5 copy group: two ids mapped to one group by the `copyGroup` delegate, 2 + 1 copies -> `copies`; `owned` returning 1
    for an id used twice -> `owned <id>`.
  - BD6 a DEATHKNIGHT row deck with 4 runes -> `runes`; 3 runes -> legal. A rune-cost card on a MAGE row -> `rune-class`.
  - BD7 a dual row (MAGE_ROGUE): 10 mage cards -> legal; a rogue + mage mix -> legal.
  - BD8 `AddReason` on a full deck -> `size`; adding a third copy -> `copies`; a legal add -> null.
- [ ] **Step 2: Run** `bash verify/bin/builds.sh only p9/c-sched-9` -> build error (DungeonRunBuild missing).
- [ ] **Step 3: Implement `DungeonRunBuild`.** Card checks call `DungeonRunValidator.PoolCardReason(..., catalogRun: true)`,
  `CardType.HERO`, `FabledRules.IsFabled/IsCompanion`, `DungeonRunValidator.HasRuneCost`; deck runes use
  `RuneRules.IsLegalForClass(RuneRules.FromCards(...), CardClass.DEATHKNIGHT)` when `classSet` contains DEATHKNIGHT.
- [ ] **Step 4: Run** `bash verify/bin/runs.sh 1 only p9/c-sched-9` -> `RESULT 50 passed, 0 failed` (42 + 8).
- [ ] **Step 5: Commit** `feat(dungeon-run): Build Deck rules (DungeonRunBuild), c-sched-9 BD1-BD8`.

### Task 3: One build deck per row (data, model, validator, catalog option)

**Files:**
- Modify: `plugin/DungeonRun/DungeonRunData.cs:113-134` (`DungeonRunStarterDeck.Build`, parsed from `"build": true`),
  `DungeonRunClass` helper `BuildDeck` (the row's one deck with `Build || IsGenerated`)
- Modify: `data/dungeon-run/run-v1.json` (or the file the class rows' decks live in: follow `FindDeck`'s sources)
- Modify: `plugin/DungeonRun/DungeonRunValidator.cs:300-322, 466-470` (build decks: no cards, a display card, not counted
  as a fixed deck; every row has exactly one build-able deck)
- Modify: `plugin/DungeonRun/DungeonRunCatalog.cs` (`RunDeckOption.IsBuild`; build-able decks get name
  `NativeDungeonRunText.BuildDeckName`, description `NativeDungeonRunText.BuildDeckDescription`, `Unlocked = true`, no cards)
- Modify: `plugin/NativeDungeonRunText.cs` (constants: `BuildDeckName = "Build Deck"`,
  `BuildDeckDescription = "Build a 10-card deck from your collection."`)
- Test: `verify/suites/p9/c-sched-9/Program.cs` (BD9, BD10)

**Interfaces:**
- Produces: `DungeonRunStarterDeck.Build` (bool), `DungeonRunClass.BuildDeck(DungeonRunData) -> DungeonRunStarterDeck`,
  `RunDeckOption.IsBuild` (bool). Build deck ids: `<row id lowercased>_build` (e.g. `druid_build`, `gila_500h3_build`,
  `trla_209h_mage_build`), class = the row, `displayCard` the row's default deck display card or `DALA_800`, no cards;
  Rikkar's build deck copies the row default deck's `shrine`.

- [ ] **Step 1: Write failing checks:** BD9 every enabled row (classes, heroRows, enabled catalogClasses) has exactly one
  build-able deck; class/Rikkar/Monster Hunt rows a `Build` deck, Heist/Tombs rows their generated deck; Rikkar's build
  deck's shrine equals the row default deck's. BD10 `DungeonRunCatalog` decks for each row list the build option last,
  named "Build Deck", unlocked, with `IsBuild`; the row's other decks unchanged.
- [ ] **Step 2: Run** -> BD9/BD10 fail.
- [ ] **Step 3: Implement** the parse, data rows (a small script under `tools/` is fine; commit only the data), validator
  and catalog changes.
- [ ] **Step 4: Run** `c-sched-9` (52 passed) and the validator-driven suites: `bash verify/bin/runs.sh 1 only
  audit-verify/c-catalog audit-verify/c-runlayer p6f15/c-heroes` and every suite whose RESULT changes; each change must be
  a deck count or option list that now includes the build deck. Update those pins in the same commit with the reason.
- [ ] **Step 5: Commit** `feat(dungeon-run): one Build Deck option per row (data, validator, catalog)`.

### Task 4: Run start with a built list (pure)

**Files:**
- Modify: `plugin/DungeonRun/DungeonRun.cs:130-200` (`NewRun`/`NewRunCore` optional `IReadOnlyList<string> builtCards`)
- Modify: `plugin/DungeonRun/DungeonRun.Economy.cs:113,131` and `plugin/DungeonRun/DungeonRun.cs:1205` (built runs count as
  every deck of the row)
- Modify: `plugin/DungeonRun/DungeonRunState.cs:330,518` (`[XmlAttribute] public bool BuiltDeck;` beside `TypedSeed`, in the
  active state and the archived summary, serialized only when true, as the step-9 attributes are)
- Test: `verify/suites/p9/c-sched-9/Program.cs` (BD11-BD13)

**Interfaces:**
- Consumes: `DungeonRunBuild.DeckReasons` (Task 2), `DungeonRunClass.BuildDeck` (Task 3).
- Produces: `DungeonRun.NewRun(DungeonRunProfile profile, string classId, string deckId, string powerId, long seed, bool typedSeed = false, string schedule = null, string signatureId = null, IReadOnlyList<string> builtCards = null)`;
  `DungeonRunState.BuiltDeck`. A built start requires `deckId == cls.BuildDeck(Data).Id`, skips the deck unlock check,
  uses `builtCards` as the starter after `DeckReasons(..., owned: null, ...)` is empty (else
  `InvalidOperationException` with the reasons joined), sets `BuiltDeck = true`. A non-built start on a `Build` deck
  throws (a build deck has no cards).
- [ ] **Step 1: Write failing checks:** BD11 a built MAGE run and a built Heist run: `Deck` equals the list, `StarterDeck`
  the row's build deck id, `BuiltDeck` true; XML round trip keeps both; `Resume` accepts it. BD12 an illegal list (9
  cards; a warrior card) -> refused with the reason. BD13 a built Monster Hunt run's treasure candidates include the
  hunter's deck-scoped treasures (e.g. GILA_501), and deck tags are the union of the row's decks' tags.
- [ ] **Step 2: Run** -> fail.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run** `c-sched-9` (55 passed), `p9/c-econ-9`, `p9/c-gen-9`, `p9/c-longrun-9`: unchanged RESULTs.
- [ ] **Step 5: Commit** `feat(dungeon-run): start a run from a built 10-card list; built runs count as every row deck`.

### Task 5: Runtime: candidates and start (plugin, client-side)

**Files:**
- Modify: `plugin/DungeonRun/DungeonRunRuntime.cs` (not compiled by suites: copy existing patterns)

**Interfaces:**
- Consumes: Task 2, Task 4.
- Produces:
  - `internal sealed class RunBuildCard { public string Id, ClientId, Name; public int Cost; public bool Legendary; public int Owned; public string CopyGroup; }`
    (in `DungeonRunCatalog.cs`, beside `RunDeckOption`, so suites see the type).
  - `DungeonRunRuntime.BuildCandidates(string rowId) -> IReadOnlyList<RunBuildCard>`: every collectible engine card with
    `DungeonRunBuild.CardReason == null` for the row, `ClientId = ClientCardId(id)`, `Owned =
    Profile.TotalOwnedCount(ClientId)` > 0, `CopyGroup = LocalCardCatalog.CopyGroup(ClientId)`; sorted by cost then name.
  - `DungeonRunRuntime.NewRun(string classId, string deckId, string powerId, string signatureId = null, IReadOnlyList<string> builtCards = null)`:
    re-checks `DungeonRunBuild.DeckReasons` with the live owned counts and copy groups before starting; a reason is
    returned as the refusal text.
- [ ] **Step 1: Implement** (no suite compiles the runtime; re-read for correctness; the pure parts are tested).
- [ ] **Step 2: Commit** `feat(plugin): Build Deck candidates from the collection and a checked built start`.

### Task 6: The plank builder (run menu)

**Files:**
- Modify: `plugin/NativeDungeonRunUI.cs` (`Step` gains `Build` after `Deck`; `ChooseDeck` on an `IsBuild` option goes to
  `Build`; Build page; `StartRun` passes the list; `BuildWhenHome`)
- Modify: `plugin/NativeDungeonRunText.cs` (pure builder helpers)
- Test: `verify/suites/p9/c-sched-9/Program.cs` (BD14-BD16)

**Interfaces:**
- Consumes: Task 5 (`BuildCandidates`, runtime `NewRun`), Task 2 (`AddReason`, `DeckReasons`).
- Produces:
  - `NativeDungeonRunText.BuildFilter` (enum `All, Class, Neutral, Cost0to1, Cost2, Cost3, Cost4, Cost5, Cost6, Cost7Plus`)
    and `NativeDungeonRunText.BuildPage(IReadOnlyList<RunBuildCard> all, BuildFilter filter, int page, int perPage) -> IReadOnlyList<RunBuildCard>`,
    `BuildCardLabel(RunBuildCard c, int inDeck) -> "Fireball (4) 1/2"`, `BuildCaption(IReadOnlyList<string> names, int size)`,
    `BuildReachable(IReadOnlyList<RunBuildCard> all, Func<string,string> copyGroup) -> int` (legal copies the collection
    can put in one deck: per copy group min(sum owned, 2 or 1 for a legendary)).
  - `NativeDungeonRunUI.BuildWhenHome(string cls, string row, string deck)`: its own pending slot (like
    `SignatureWhenHome`), opens the menu on `Step.Build` with the row and deck set, never lapses while the menu is closed
    for the build.
  - Build page rows: a filter row cycling `BuildFilter`, up to 6 card planks (`BuildPage`, `perPage` 6), Previous/Next
    (existing `Paging`), "Remove last", "Done" (enabled only when `DeckReasons` is empty), "Back" (to `Step.Deck`, list
    cleared). A card plank adds via `AddReason == null`, otherwise the reason becomes the caption message. Caption: the
    picked names, "n of 10", and, when the row's owned legal copies cannot reach 10, "Not enough cards in your collection
    for this hero (n of 10)".
- [ ] **Step 1: Write failing checks:** BD14 `BuildPage` filters (class vs neutral vs cost buckets) and pages 6 per page in
  cost/name order; BD15 `BuildCardLabel` and `BuildCaption` exact strings; BD16 `BuildReachable` for a fixture with 7
  legal copies -> 7, and the caption line "Not enough cards in your collection for this hero (7 of 10)".
- [ ] **Step 2: Run** -> fail. **Step 3: Implement** helpers then the page. **Step 4: Run** `c-sched-9` (58 passed).
- [ ] **Step 5: Commit** `feat(plugin): run-menu Build Deck builder (plank screens) and Power step start`.

### Task 7: Native picker: Build Deck option and hand-off

**Files:**
- Modify: `plugin/NativeDungeonRunPicker.cs` (`EnsureFeed` ~1000-1030: drop the Random pouch; the row's decks from
  `DecksFor` already end with the build option; placeholders fill the rest *before* it so Build stays last; the deck
  step handler ~680: choosing the build option hands off with `NativeDungeonRunUI.BuildWhenHome(row.ClassId, fedRow,
  deckId)`, `leaveRequested = true; openMenu = false`, as the signature hand-off at ~836 does)

**Interfaces:**
- Consumes: Task 3 (`RunDeckOption.IsBuild`), Task 6 (`BuildWhenHome`).
- [ ] **Step 1: Implement**; remove `FedDeck.Random`, `RandomUnlockedDeck` and the `DALA_800` override (the build option's
  display card comes from data).
- [ ] **Step 2: Commit** `feat(plugin): native picker offers Build Deck and hands it to the run menu`.

### Task 8: Docs and regression

**Files:**
- Modify: `docs/design/dungeon-run.md:200,313` (R12: the deck-building step uses the collection; matches stay isolated),
  the dual-row line (~471), `handoff/phase9/tests.md` (new section), `HANDOFF.md` §3
- [ ] **Step 1:** Update the docs.
- [ ] **Step 2:** Run the regression of every suite that compiles `plugin/DungeonRun` (`grep -rl "plugin/DungeonRun"
  verify/suites --include=*.csproj`) plus `runs.sh 1 all`; every moved RESULT is attributed in the commit.
- [ ] **Step 3: Commit** `docs: Build Deck (phases 0-2)`; push; PR into `feat/dungeon-run-engine`.
- [ ] **Step 4 (owner, PC):** rebuild, install, check: Build Deck shows last on every row in the native picker and the
  run menu, the builder adds/limits cards, Done -> Power step -> run starts with the 10 cards; a Monster Hunt built run
  offers its hunter's treasures.
