# Dungeon Run Build Deck: the Collection Manager editor (phases 3-4) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Choosing Build Deck opens the client's own Collection Manager deck editor on a temporary, never-saved deck. The
player builds 10 legal cards from their collection. Done goes to the run menu's Power step with the list; Back goes to
the Deck step. The plank builder stays as the fallback wherever the editor is not used.

**Architecture:**
- Every decision is a pure function in `plugin/DungeonRun/DungeonRunEditor.cs` (tested in c-sched-9): the id band,
  who builds, the add verdict, client-to-engine mapping, Done reasons, leave target, Power Back, recovery.
- A new client file `plugin/NativeDungeonRunEditor.cs` owns the session:
  - create the deck in the HUB;
  - open CM on it;
  - hook size, rules, adds, Done and Back;
  - leave CM;
  - remove the deck at the HUB.
- Existing plugin bookkeeping admits or skips the temporary id.
- The run start re-check (`BuiltListRefusal`) stays the final guarantee.

**Tech Stack:** C# (net48 plugin, LangVersion latest; net10 verify suites), Harmony patches by reflection.

**Spec:**
- `docs/superpowers/specs/2026-11-25-dungeon-run-build-deck-design.md` (approved 2026-11-25), sections 3, 5 and 7, and
  phases 3-4.
- Settled by the addendum `docs/superpowers/specs/2026-11-26-dungeon-run-build-deck-editor-design.md` (below,
  "design §X"). The addendum is the binding source for every client member, hook signature and plugin line. Its section
  letters (A-P) are cited per task; read the cited sections before a task.
- Where the addendum corrects the spec (id band, removal by `RemoveDeck`, native `Exit`, the Power Back rule), the
  addendum wins. Task 7 folds those corrections into the spec.

## Global Constraints

- Starter size: `Rules.StarterSize` (10); never a literal 10 outside tests.
- The temporary deck id band is 900000300..900000399 (`DungeonRunEditor.IsRunBuildId`). Ids in the band are never
  persisted: every `CollectionDeck.SendChanges` for one is swallowed, and the deck is removed with
  `CollectionManager.RemoveDeck` at the HUB.
- Nothing in the editor mode creates, changes or deletes a constructed deck, a profile draft or a collection count. That
  includes crafting and disenchanting.
- The card rules are `DungeonRunBuild`'s (copy groups, owned per id, class set, runes, collectible). Native FT_STANDARD
  rules never decide a temporary deck: the `DeckRuleset.CanAddToDeck` prefix replaces them.
- `plugin/DungeonRun/*.cs` and `plugin/NativeDungeonRunText.cs` must not reference client, `LocalProfile` or
  `LocalCardCatalog` types: verify suites compile them alone. Profile facts come in as data or delegates.
- Every client hook follows design's preamble pattern:
  - `if (!active || !Owns(id)) return true;`
  - the body is wrapped in try/catch, and the catch calls `NativeDungeonRunEditor.Abort(error)`;
  - nothing throws into Harmony.
- Hooks on members the plugin already patches go into the existing prefix (`PrepareDiscardDeck`, `SaveLocalDraft`,
  `LocalDeckCopyCount`), not a second patch.
- Owner decisions:
  - the header hero is the row's named hero first, then the class hero (`HeaderHeroes`);
  - Back on the Power step after an editor Done re-opens the editor with the list;
  - Back from the editor without Done returns to the Deck step with the list dropped.
- The plugin cannot be compiled here. Client edits copy existing usage patterns and check every member against the
  design or the bindings; files LF, no BOM.
- Never commit the client dumps (`.superpowers/client-dumps/`) or client binaries. The repo is private; never push to
  the public repo.
- Commits: Conventional Commits ending with the session trailers. Run every build/run through `scratchpad/hrun.sh`,
  then `dotnet build-server shutdown`.

## Review Focus

1. A failure anywhere in the editor flow (open timeout, a hook exception, scene timeout):
   - the player lands on the plank Build step or the Deck step with a caption, never a blocked UI;
   - the temporary deck is removed;
   - later Build Decks use the plank (Task 1 `RecoverAction` table, Task 3 wiring).
2. A temporary deck left over from a crash or profile switch:
   - the sweep removes it before Collection, TOURNAMENT or Bootstrap use the deck list;
   - `DecksReady` never hangs on it (Task 1 `NonBandCount`, Task 3 sweep).
3. A card the run cannot hold, offered on the native pages (another class, unowned, hero, Fabled, rune card off a DK
   row, an 11th card):
   - one worded message per click, with no engine id;
   - the deck stays legal (Task 2 `AddCode` and texts).
4. Done with an illegal or short list:
   - refused with worded reasons, and the editor stays open;
   - Done at 10 hands exactly the engine ids of the slots to the Power step (Task 2 `DoneReasons` and `EngineList`).
5. Who builds on each row:
   - the plank for a collection that cannot reach 10, a full deck tray (111), after an editor failure, and for dual and
     Death Knight rows until phase 4;
   - the editor otherwise (Task 1 `Builder` table).

---

### Task 1: Editor decisions (pure)

**Files:**
- Create: `plugin/DungeonRun/DungeonRunEditor.cs`
- Test: `verify/suites/p9/c-sched-9/Program.cs` (new guard "BDE", checks BD22-BD27)

**Interfaces (all `internal static` on `DungeonRunEditor` unless noted; design §A1, §B, §C, §D3, §J, §K belt, §L, §M, §A2,
§F, §H0):**
- Band:
  - `const long FirstRunBuildId = 900000300, LastRunBuildId = 900000399`;
  - `bool IsRunBuildId(long id)`;
  - `long? NextRunBuildId(IEnumerable<long> nativeIds)`;
  - `int NonBandCount(IEnumerable<long> ids)`.
- Enums: `enum Stage { None, Creating, Entering, Editing, Leaving }`, `enum Leave { Done, Back, Recover }`,
  `enum AddVerdictKind { Allow, Rule, Refuse }`, `enum LeaveStep { Power, Deck }`, `enum PowerBackTarget { Editor,
  PlankBuild, Deck }`, `enum RecoverKind { None, RemoveAndPlank, DeckAndLeave }`, `enum BuilderKind { Editor, Plank }`.
- Flow decisions:
  - `AddVerdictKind AddVerdict(Stage stage)`;
  - `LeaveStep LeaveTarget(Leave leave)`;
  - `PowerBackTarget PowerBack(bool builtByEditor, bool hasList, bool editorAvailable)`;
  - `RecoverKind RecoverAction(Stage stage, bool atHub, bool recoveredOnce)`;
  - `BuilderKind Builder(DungeonRunClass row, int candidates, int reachable, int size, int nativeDecks, int nativeLimit,
    bool editorFailed, bool dualAndDkInEditor)`;
  - `bool ExitAllowed(bool loggedIn, bool localActive, bool pickerOrigin, bool runBuildLeaving)`.
- Heroes and classes:
  - `IReadOnlyList<string> ClassTags(DungeonRunClass row)`;
  - `IReadOnlyList<string> HeaderHeroes(DungeonRunClass row, DungeonRunData data, Func<string,string> clientId)`;
  - `bool HeroAccepted(string classTag, IReadOnlyList<string> heroClassTags, DungeonRunClass row)`.
- `bool OwnsEditor(bool active, bool currentCollection, bool manager, bool tray, bool mainContext, bool exactDeck, bool
  exactInstance, bool runBuild)`.
- `const bool DualAndDkInEditor = false` (phase gate; Task 6 sets it to true).

- [ ] **Step 1: Write the failing checks** in a new `Guard("BDE", …)` after BDM. Each check names its design section.
  - BD22, the band (§A1):
    - 900000299 and 900000400 are false;
    - 900000300 and 900000399 are true;
    - 910000001 and 910000010 are false;
    - every id of a fixture list mirroring NativeUIProjection.cs:374-500 (900000001..900000225 literals) is false;
    - `NextRunBuildId([900000300, 900000301]) == 900000302`;
    - a full band gives null;
    - `NonBandCount([900000300, 910000010, 900000215]) == 2`.
  - BD23, flow tables:
    - `AddVerdict`: Creating gives Allow, Editing gives Rule, and Entering, Leaving and None give Refuse;
    - `LeaveTarget`: Done gives Power; Back and Recover give Deck;
    - `PowerBack`: the four rows of design §J2;
    - `ExitAllowed`: (false,true,false,true) is true, (false,true,false,false) is false, (false,false,true,true) is false,
      and (true,false,false,false) is true.
  - BD24, `RecoverAction`, the full table of stage × atHub × recoveredOnce (design §L):
    - recoveredOnce gives None;
    - None stage gives None;
    - Creating, and Entering at the HUB, give RemoveAndPlank;
    - Entering in CM, Editing and Leaving give DeckAndLeave.
  - BD25, `Builder` (design §M):
    - each Plank condition alone: editorFailed; 0 candidates; reachable 9 of 10; nativeDecks 111 of 111; a dual row
      with the gate false; a DEATHKNIGHT row with the gate false;
    - the dual and DK rows with the gate true give Editor;
    - the all-clear row gives Editor.
  - BD26, heroes (design §A2, §F):
    - `HeaderHeroes` for GILA_500h3's row is `[GILA_500h3, <its class row's HeroCard>]`;
    - for the MAGE row it is the MAGE hero only;
    - for a dual row its second entry is the first class's hero;
    - `HeroAccepted("MAGE", ["MAGE"], MAGE row)` is true;
    - `HeroAccepted("NEUTRAL"…)` and `HeroAccepted("WARRIOR"…)` are false;
    - `("MAGE", ["MAGE","ROGUE"], the MAGE_ROGUE row)` is true;
    - `ClassTags` of that row is `["MAGE","ROGUE"]`.
  - BD27, `OwnsEditor`: all true gives true, and each single false input gives false.
- [ ] **Step 2: Run** `bash verify/bin/builds.sh only p9/c-sched-9` and expect a compile failure (no `DungeonRunEditor`).
- [ ] **Step 3: Implement** `DungeonRunEditor.cs`. Data facts come from `DungeonRunData`/`DungeonRunClass` (`HeroCard`,
  `ClassSet`, `IsDual`), with no client types.
- [ ] **Step 4: Run** `runs.sh 1 only p9/c-sched-9`. Expect 63 + 6 passed, 0 failed.
- [ ] **Step 5: Commit** `feat(dungeon-run): Build Deck editor decisions (band, flow, recovery, builder) as pure rules`.

### Task 2: Client-to-engine mapping, add and Done reasons, texts (pure)

**Files:**
- Modify: `plugin/DungeonRun/DungeonRunEditor.cs` (`EditorCards`, `AddCode`, `DoneReasons`, `SeedCounts`)
- Modify: `plugin/DungeonRun/DungeonRunBuild.cs:40-83`: add an optional last parameter `Func<string,string> cardReason =
  null` to `DeckReasons` and `AddReason`. Null keeps today's `CardReason(id, classSet, policy)`; existing callers are
  unchanged.
- Modify: `plugin/NativeDungeonRunText.cs:416-470`:
  - extend `BuildReasonWords` (design §E4 words: class, hero, fabled, rune-class, missing/unavailable, default "not
    allowed in a run");
  - refactor `BuildStartRefusal` into `BuildReasonsText(string prefix, …)`, keeping BD20's exact texts;
  - add `EditorDoneRefusal(IReadOnlyList<string> reasons, Func<string,string> name)`;
  - add the constants `EditorAddHeader = "Cannot add card"`, `EditorDoneHeader = "Deck not ready"`,
    `EditorFailedCaption = "The deck editor could not open. Build your deck here instead."` and
    `EditorCraftingOff = "Crafting is off while you build a Dungeon Run deck."`.
- Test: `verify/suites/p9/c-sched-9/Program.cs` (BD28-BD31)

**Interfaces (design §E1-§E4, §C Seed):**
- `sealed class EditorCards`:
  - built by `EditorCards(IReadOnlyList<RunBuildCard> candidates)`;
  - `string Engine(string clientId)`: the lowest engine id by ordinal, else null;
  - `IReadOnlyList<string> Collisions`;
  - `int Owned(string engineId)`, `string Group(string engineId)`, `string Name(string engineId)`;
  - `IReadOnlyList<string> EngineList(IEnumerable<string> clientSlots, out IReadOnlyList<string> unmapped)`;
  - `string ClientId(string engineId)`.
- `(string Code, string EngineId) AddCode(EditorCards cards, IReadOnlyList<string> clientSlots, string clientId,
  DungeonRunClass row, int size, OfflinePoolPolicy policy, Func<string,string> cardReason)`.
- `IReadOnlyList<string> DoneReasons(EditorCards cards, IReadOnlyList<string> clientSlots, DungeonRunClass row, int size,
  OfflinePoolPolicy policy, Func<string,string> cardReason)`: `DeckReasons` over `EngineList`, plus `"unavailable <id>"`
  per unmapped slot.
- `IReadOnlyDictionary<string,int> SeedCounts(IReadOnlyList<string> engineList, EditorCards cards)`: client id to count;
  throws `ArgumentException` on a non-candidate.

- [ ] **Step 1: Write the failing checks.**
  - BD28, `EditorCards`:
    - a fixture where two engine ids share a client id maps to the lower by ordinal and is listed in `Collisions`;
    - `EngineList` maps a 10-slot list and reports an unknown client id in `unmapped`.
  - BD29, `AddCode` on the MAGE row:
    - a candidate equals `DungeonRunBuild.AddReason`;
    - AT_064 (a Warrior card) gives code `class AT_064`, and its text is exactly "Cannot add " + the engine name of
      AT_064 + ": not a card of this hero's class." with no id;
    - a card legal for the row but not a candidate (the player owns no copy) gives `owned`;
    - an id unknown to the engine gives `unavailable`;
    - an 11th card gives `size`;
    - on a DEATHKNIGHT row with 3 runes used, a 4th-rune card gives `runes`, worded "a Death Knight deck needs at most 3
      runes".
  - BD30, memoized `cardReason`:
    - over BD3-BD8's fixtures, `DeckReasons(..., memo)` equals `DeckReasons(..., null)`, and so does `AddReason`;
    - `DoneReasons` of a 9-slot list contains `size 9 of 10`;
    - `DoneReasons` with an unmapped slot contains `unavailable <id>`.
  - BD31, texts:
    - `EditorDoneRefusal` exact text: "Your deck is not ready: it has 9 of 10 cards; Dalaran Aspirant: at most 2 copies
      of a card.";
    - BD20's texts still pass;
    - `SeedCounts` of a list with 2x one card gives 9 keys summing to 10;
    - `SeedCounts` throws on a non-candidate.
- [ ] **Step 2: Run** and expect the build to fail.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run** c-sched-9. Expect +4 passed, BD1-BD27 unchanged, 0 failed.
- [ ] **Step 5: Commit** `feat(dungeon-run): editor card mapping, add and Done reasons, worded refusals`.

### Task 3: The editor session: create, open, Done, Back, leave, remove, recover (client)

**Files:**
- Create: `plugin/NativeDungeonRunEditor.cs`. It holds:
  - the session state: cls, row, deck, seed, `EditorCards`, tempId, heroes, `Stage`, `Leave`, `refusalOpen`,
    `editorFailed` latch, memoized `cardReason`;
  - `Initialize(Harmony, ManualLogSource)`;
  - `Owns(long)`, `Begin(string classId, string rowId, string deckId, IReadOnlyList<string> seed)`, `Update()` (the
    session tick), `Abort(Exception)`, `bool Recover(Exception)`, `Sweep(string why)`;
  - the properties `Leaving`, `Active`, `EditorFailed`.
- Modify:
  - `plugin/NativeUIProbe.cs`:
    - `Initialize`: call `NativeDungeonRunEditor.Initialize` beside the picker's (:175);
    - `Update`: tick after `NativeDungeonRunPicker.Update()` (:387);
    - `Fail`: `if (NativeDungeonRunEditor.Recover(error)) { sceneWaiting = false; return; }` before `ready = false`
      (:359-360);
    - `DecksReady`: the band filter (:1232; design §B2);
    - `CollectionExitLoggedIn`: pass `NativeDungeonRunEditor.Leaving` (:1574-1575);
    - run the sweep before `Navigate(TOURNAMENT)` and at Bootstrap validation (design §B3-B4, §K6).
  - `plugin/NativeMenuFlow.cs:35-36`: `CollectionExitAllowed` calls `DungeonRunEditor.ExitAllowed` (design §K belt).
  - `plugin/NativeDeckEditor.cs`:
    - `SaveLocalDraft`: the band swallow is the first statement after :148 (§B8);
    - `PrepareDiscardDeck`: the band Back branch at the top, before :269 (§J1);
    - `ReorderLocalDrafts` / `RestoreWorkingDecks`: skip band ids (§B7);
    - `TryPrepareLocalDelete`: refuse band ids silently (§B10);
    - `CreateLocalDraft`: refuse while a session exists (§B17).
  - `plugin/NativeUIProjection.cs:517-519`: `IsRunBuildId` forwards to the pure one.
  - `plugin/NativeDungeonRunUI.cs`:
    - new slots `pendingBuilt` and `pendingDeck`, set by `BuiltWhenHome(cls,row,deck,list)` and
      `DeckWhenHome(cls,row,deck,caption)`;
    - both slots are exempt from the 180 s lapse (:166-167) and cleared with the others (:95, :99, :105, :113, :118);
    - `Open()` gets the branches of design §I. Order: `BeginBuild()`, then `built = list`, then `builtByEditor = true`,
      then `step = Step.Power`;
    - `builtByEditor` is reset in `DropBuild`;
    - `Builder` routing at `ChooseDeck(build:true)` (:277-282) and in `Open()`'s `pendingBuild` branch (:140-145), before
      `popup.Open()` (design §M);
    - Power Back uses `DungeonRunEditor.PowerBack` (:247-250; design §J2).

**Interfaces:**
- Consumes Task 1-2's pure API and `DungeonRunRuntime.BuildCandidates`/`ClientCardId`.
- Produces `NativeDungeonRunEditor.Owns(long)`, `Active`, `Leaving`, `CurrentCards`, `CurrentRow`, `CurrentStage`,
  `CardReason` and `Abort(Exception)` for Tasks 4-5.

Steps follow design §A2-§A4 (the header, with named hero then class hero and the hero test; `OnDeckCreated` in the HUB;
the NetCache asserts), §C O0-O9 plus Seed, §I (the `DoneButtonPress(UIEvent)` prefix), §J1-§J2, §K L1-L7 and K6-K8, and
§L. In phase 3, editor Done and Back run with native add rules still in place; Task 4 adds the run rules.

- [ ] **Step 1: Implement the session** in `NativeDungeonRunEditor.cs`.
  - Every reflective member goes through a `Required` helper that throws at `Initialize` when a member is missing, like
    NativeDungeonRunPicker.cs:236-241.
  - Every tick step and hook has its own try/catch, and the catch calls `Abort`.
  - Log lines are `DUNGEON_RUN_EDITOR created|opened|done|back|removed|swept|recover <id> …`.
- [ ] **Step 2: Wire** the files listed above. Re-read each target line before editing; the line numbers are at
  d4c6fab + Task 1-2.
- [ ] **Step 3: Build** every verify suite (`builds.sh all`) and expect 0 errors. Then run c-sched-9 and expect no
  change, since only compiled shared files may have moved. Hand-check every new client member against design §O and
  the bound rows.
- [ ] **Step 4: Commit** `feat(plugin): Build Deck editor session (temporary deck, open, Done, Back, leave, recover)`.

### Task 4: Run rules inside the editor (client)

**Files:**
- Modify: `plugin/NativeDungeonRunEditor.cs`. New patches:
  - size: S1 `DeckRuleset.GetDeckSize(CollectionDeck)`, S2 `GetMinimumAllowedDeckSize`, S3 `GetDeckSizeWhileEditing`;
  - validity: V1 on the 3-arg `DeckRuleset.IsDeckValid` with an out `IList<DeckRuleViolation>` (memoized on the slot
    signature);
  - rules: A1 on the ref and list overloads of `DeckRuleset.CanAddToDeck`, silent;
  - A2 on `CollectionDeck.AddCard`, silent;
  - A4 on `DeckTrayCardListContent.AddCard`. It shows one message through `GameplayErrorManager.DisplayMessage`, else an
    `AlertPopup` behind `refusalOpen`.
  - The exact signatures and patch-argument types are in design §D1-§D3 and §E3.
- Modify: `plugin/NativeDeckEditor.cs:22-28`: the `LocalDeckCopyCount` gate admits `NativeDungeonRunEditor.Owns(ID)`
  without the draft and `CustomCards` terms (§B12).
- Modify: Done in Task 3's `DoneButtonPress` prefix uses `DoneReasons` and `EditorDoneRefusal` (§I, steps 1-2).

**Interfaces:** Consumes Task 2 (`AddCode`, `DoneReasons`, texts) and Task 3 (`Owns`, `CurrentStage`, `CurrentCards`,
`CardReason`). Contingent hooks S4 (`OnCardCountUpdated`) and A3 (`Filter` / `FilterFailsOnShowInvalidRule`) are written
but registered only behind a `static bool` switch each. Both switches default to false and are named in HANDOFF.

- [ ] **Step 1: Implement** the patches. Each body is guarded, and A1 never shows a message.
- [ ] **Step 2: Build** every verify suite and expect 0 errors. Run c-sched-9 and expect no change.
- [ ] **Step 3: Commit** `feat(plugin): Build Deck editor uses the run's rules (10 cards, adds, validity, Done)`.

### Task 5: Blocked actions in the editor (client)

**Files:**
- Modify: `plugin/NativeDeckEditor.cs`:
  - add `OwnsRunBuildEditor(CollectionDeck candidate, object instance)` beside `OwnsPersonalDeckEditor` (:499-520),
    built on `DungeonRunEditor.OwnsEditor`;
  - extend the blockers at :441-446, :448-453, :455-483 and :496-497 (design §H rows: deck options, tutorial `Options`
    write, DeckHelper, Complete My Deck).
- Modify: `plugin/NativeDeckLimits.cs:40-45`: `GateNewDeckButton` is false while a session exists.
- Modify: `plugin/NativeCrafting.cs:138-144, 161-169, 233-243`. While a session exists, ahead of the `Enabled` early
  returns:
  - `CanCraft` gives `NotCraftable`;
  - `BeforeCreate` and `BeforeDisenchant` return false and show `EditorCraftingOff`.
- Modify: `plugin/NativeRecipePicker.cs`:
  - `AttachRecipeGuide` skips an owned deck (:225-232);
  - `ChooseLocalRecipe` refuses for an owned edited id.
- Modify: `plugin/NativeDungeonRunEditor.cs`:
  - new prefixes `CollectionManagerDisplay.ShouldShowDeckOptionsMenu`, `CanViewHeroSkins`, `CanViewCardBacks`,
    `CanViewCoins` and `CanViewPets`, each returning false;
  - a `DeckTrayDeckListContent.RenameCurrentlyEditingDeck` prefix that skips;
  - an F2 `CollectionPageManager.ShouldShowTab` postfix for the cosmetic tabs only (the class part is Task 6).

- [ ] **Step 1: Implement** (design §H table, every row).
- [ ] **Step 2: Build** every verify suite and expect 0 errors. Run c-sched-9 and expect no change.
- [ ] **Step 3: Commit** `feat(plugin): Build Deck editor blocks deck options, cosmetics, crafting and helpers`.

**Phase-3 checkpoint.** Tasks 1-5 are the phase-3 build: one PR, then the owner's PC checks P1-P11 and P14-P22 (design
§P, §A-§M PC lines). Dual-class and Death Knight rows stay on the plank (`DualAndDkInEditor = false`). The executor does
not stop here: Task 6 is written so that its gate flip is one line to revert if the owner's phase-4 checks fail.

### Task 6: Dual-class and Death Knight rows in the editor (phase 4, client + one pure flag)

**Files:**
- Modify: `plugin/NativeDungeonRunEditor.cs`:
  - an F1 `CollectionDeck.GetClasses()` prefix that returns a fresh `List<TAG_CLASS>` of `ClassTags(row)` for an owned
    deck;
  - the class part of the F2 `ShouldShowTab` postfix: classes A, B and NEUTRAL shown, other class tabs hidden;
  - O8's DK steps: `InitializeRuneIndicatorVisual`, and the seeded-header runes from `NativeDeckRunes.Order("HERO_11",
    …)` (design §A2, §G).
  - C1 (`GetHeroClasses`) is written behind a `static bool` switch, default false.
- Modify: `plugin/DungeonRun/DungeonRunEditor.cs`: `DualAndDkInEditor = true`.
- Test: c-sched-9 BD25 now asserts the gate-true rows through `Builder(..., DungeonRunEditor.DualAndDkInEditor)` as well
  as the explicit flag cases.

- [ ] **Step 1:** Update BD25 to read the constant and expect Editor for the dual and DK rows. Run it and expect it to
  fail.
- [ ] **Step 2: Implement** F1, F2's class part and the DK steps; flip the flag.
- [ ] **Step 3: Run** c-sched-9 and expect all passed. Build every verify suite and expect 0 errors.
- [ ] **Step 4: Commit** `feat(plugin): Build Deck editor for dual-class and Death Knight rows`.

### Task 7: Docs, bindings, spec corrections, regression

**Files:**
- Modify:
  - `docs/design/dungeon-run.md` :149 (R12 bullet), :200 and :313 (design §N);
  - `handoff/phase9/tests.md`: new section 8 with BD22-BD31 and "Not covered here";
  - `HANDOFF.md`: §3.5 "Not done" points at this plan; new §3.6 with the owner's PC checks, design §P as a checklist
    with each "If it fails" switch;
  - `docs/superpowers/specs/2026-11-25-dungeon-run-build-deck-design.md`: the five corrections of design §N.
- Modify: `plugin/client-bindings.extra.txt`: add `row` lines for reflection sites the scanner cannot resolve (design
  §O). Do not hand-edit `client-bindings.txt`: the owner regenerates it with `tool bindings --write` on the PC; HANDOFF
  says so.

- [ ] **Step 1: Write the docs.**
- [ ] **Step 2: Run the full regression** with `runs.sh 1 all`. Expected:
  - every suite matches d4c6fab's results, except c-sched-9, which goes to 73 passed;
  - the known failures are unchanged (c-enc-18 has 1, c-rules-9 has 2);
  - the selfplay and randplay digests are unchanged.
- [ ] **Step 3: Commit** `docs: Build Deck editor (phases 3-4)`.
