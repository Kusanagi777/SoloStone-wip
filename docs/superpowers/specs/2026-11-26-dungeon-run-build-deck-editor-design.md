# Build Deck phases 3-4: the settled editor-mode design

Status: design addendum to `docs/superpowers/specs/2026-11-25-dungeon-run-build-deck-design.md`, settled 2026-11-26 from the owner's two client dumps; corrections from an adversarial check folded in (marked "Verified:"). Owner decisions: the named hero first with the class hero as fallback; Back on the Power step after an editor Done re-opens the editor with the list.

The dumps and the per-topic reports are local only (`.superpowers/client-dumps/`, never committed: they are derived from the client). Citations into them stay as provenance.

Citation keys:
- **D2:N** = `.superpowers/client-dumps/build-deck-members-2-2026-11-26.txt` (the upload `d0be7d2b-members2.txt` is the same text with a BOM).
- **D1:N** = `build-deck-members-2026-11-26.txt`.
- **CT:N** = `data/dungeon-run/reference/client-tables.json`.
- `file:line` is under `plugin/` unless a path is given.
- `spec:N` = `docs/superpowers/specs/2026-11-25-dungeon-run-build-deck-design.md`; `dec:N` = `.superpowers/client-dumps/editor-decisions.md`.
- **R2-rules / R2-flow / R2-display** = this folder's round-2 reports; **critic** = the round-1 critic.

How to read it:
- "Hook" lines name the exact client member and the patch kind.
- "Plugin" names the file:line to change.
- "Pure" names the helper that lives in `plugin/DungeonRun/` or `NativeDungeonRunText.cs`. Both are compiled into `verify/suites/p9/c-sched-9`: Sched9.csproj:14 globs `plugin/DungeonRun/*.cs` except `DungeonRunRuntime.cs`, and :16 includes `NativeDungeonRunText.cs`.
- "PC" is the owner-PC check that proves the item.
- "Unknown" means unknown. Nothing here was compiled.

New files the design adds:
- `plugin/DungeonRun/DungeonRunEditor.cs`: pure. Band, who-builds, heroes, id mapping, stage rules.
- `plugin/NativeDungeonRunEditor.cs`: client. The session, its tick, and the editor-only hooks, with its own `Required`/`Patch` like `NativeDungeonRunPicker.cs:236-241`. `Initialize(harmony, logger)` is called from `NativeUIProbe.Initialize` beside `NativeDungeonRunPicker.Initialize` (NativeUIProbe.cs:175).
- Hooks on members the plugin already patches go into the existing prefixes, so Harmony ordering stays the same (R2-flow §3.2).

Every client hook body follows the same pattern:
- `if (!active || !NativeDungeonRunEditor.Owns(deckId)) return true;`
- `Owns` is `session != null && DungeonRunEditor.IsRunBuildId(id) && id == session.TempId`.
- The body is wrapped in `try`/`catch`. The `catch` calls `NativeDungeonRunEditor.Abort(error)`, which schedules Recover on the next tick (L), and returns the native-safe default. Nothing throws into Harmony (convention: NativeDeckEditor.cs:124-144, :476-480).

---

## A. Temporary deck

### A1. Id band and `IsRunBuildId`

- **Pure:** `DungeonRunEditor.FirstRunBuildId = 900000300`, `LastRunBuildId = 900000399`.
  - `IsRunBuildId(long id) => id >= First && id <= Last`.
  - `NextRunBuildId(IEnumerable<long> nativeIds) -> long?` returns the lowest free id, or null when the band is full.
- **Why this band** (dec:4; critic (c)4):
  - Loaners and test decks are 900000001..900000225 (NativeUIProjection.cs:374-500).
  - The legacy personal id is 910000001, and the pinned legacy loaners fall through to 910000002..009 (:173, :175, :502-504).
  - `IsPersonalId` is `== 910000001 || >= 910000010` (:517-518).
  - `IsLoanerId` is `DeckKeys` membership (:519).
  - The band is disjoint from all of these.
- **Plugin:** `NativeUIProjection.IsRunBuildId` forwards to the pure one. It sits next to :517-519 so the existing projection code can call it. This settles critic B6: the logic is pure and tested, and `NativeUIProjection` is compiled by no test project (critic (a) Phase deliverables).
- **Test:**
  - 900000299 and 900000400 are false; 900000300 and 900000399 are true.
  - 910000001 and 910000010 are false.
  - Every literal deck id in 900000001..900000225 is false. A fixture list in the test mirrors NativeUIProjection.cs:374-500, because `DeckKeys` is private to an untested file.
  - `NextRunBuildId([900000300, 900000301]) == 900000302`, and a full band gives null.
- **PC:** the log line `DUNGEON_RUN_EDITOR created <id>` shows 900000300.

### A2. Header: named hero, with the class hero as fallback

- **Pure:** `DungeonRunEditor.HeaderHeroes(DungeonRunClass row, DungeonRunData data, Func<string,string> clientId) -> IReadOnlyList<string>`.
  - It returns client ids in this order: the row's own `HeroCard` (the named hero on hero rows, dec:2), then the class row's `HeroCard` for `row.ClassSet[0]` (`data.FindClass(row.ClassSet[0]).HeroCard`, DungeonRunData.cs:56, :106), with duplicates removed.
  - `clientId` is `DungeonRunRuntime.ClientCardId` (DungeonRunRuntime.cs:281). This is the same mapping as `ClientHeroCardId`, DungeonRunCatalog.cs:143-144, :165.
- **Pure:** `DungeonRunEditor.HeroAccepted(string classTag, IReadOnlyList<string> heroClassTags, DungeonRunClass row) -> bool`.
  - True when `classTag == row.ClassSet[0]` and every entry of `heroClassTags` is in `row.ClassSet`.
  - The tags are `TAG_CLASS.ToString()`. The engine `CardClass` names (for example `DEATHKNIGHT`) match the client names the plugin already uses (`TAG_CLASS.DEATHKNIGHT`, NativeRecipePicker.cs:204).
- **Plugin (client):**
  - The header is the 5-arg `NativeDeckEditorSave.ExpectedHeader(id, new LocalDeck(name, hero, cards), name, rank, null)` (NativeDeckEditorSave.cs:141-155; `LocalDeck(string,string,string[])`, LocalDecks.cs:40). It gives:
    - NORMAL_DECK;
    - `FormatType = LocalDeckFormat` = FT_STANDARD (NativeDeckEditorSave.cs:12);
    - `SourceType` NORMAL;
    - runes from `NativeDeckRunes.Order` (NativeDeckRunes.cs:9-18).
  - **Override `header.HeroPower`** with the class hero's power (`classHero + "bp"`). `ExpectedHeader` writes `deck.Hero + "bp"` (NativeDeckEditorSave.cs:146), which is not a card for a named hero. Verified: nothing in the dumps shows the header's `HeroPower` reaching `CollectionDeck.HeroPowerCardID` (`AddDeck` never reads it, D2:944-949), so the override is harmless but its effect is unknown; P11 decides.
  - Name: `NativeDungeonRunText.BuildDeckName` ("Build Deck", NativeDungeonRunText.cs:59).
  - Rank: `NativeUIProjection.NativeDeckLimit + 1` (= 112, NativeUIProjection.cs:165). This keeps `SortOrder` off every real deck (round-1 plugin.md §2.4). Nothing ranks the temp deck.
  - **Rejection test, in the HUB right after O3 and before entering CM:**
    1. `DefLoader.Get().GetEntityDef(hero) != null`.
    2. `HeroAccepted(deck.GetClass().ToString(), deck.GetHeroClasses().Select(c => c.ToString()), row)` (`GetClass` D2:1317-1320; `GetHeroClasses` D2:1321-1325).
  - If the test fails: `RemoveDeck(tempId)`, then redo O2-O3 with the next hero in `HeaderHeroes`. If no hero passes, use the plank fallback (M).
  - A rejection that is only visual (a blank deck-box portrait) cannot be detected in code (R2-display §5). It is a PC check; if it fails, the fix is a fixed per-adventure switch to the class hero.
- **Death Knight runes in the header:** `NativeDeckRunes.Order` fills runes only for `"HERO_11"` (NativeDeckRunes.cs:11).
  - For a seeded re-open (C, Seed) on a DEATHKNIGHT row, compute the runes with `NativeDeckRunes.Order("HERO_11", engineIds)` and set `Rune1..3` on the header, whatever the hero is.
  - Unseeded headers have no runes, and `UpdateDeckRunes` sets them on each add (D2:1267-1271; D1:941).
- **Test:**
  - `HeaderHeroes` for a hero row (for example Tess, `GILA_500h3`) is `[GILA_500h3, <hunter class hero>]`; for a class row it is `[HERO_08]` only.
  - For a dual row (`MAGE_ROGUE`) the second entry is the MAGE hero.
  - `HeroAccepted("MAGE", ["MAGE"], MAGE row)` is true. `("NEUTRAL", …)` and `("WARRIOR", …)` are false. `("MAGE", ["MAGE","ROGUE"], MAGE_ROGUE row)` is true.
- **PC:**
  - log `GetClass()` / `GetHeroClasses()` for one hero of each adventure (DALA, ULDA, GILA, TRLA);
  - look at the deck-box portrait;
  - the most useful single log is whether `ULDA_Reno` gives `[MAGE, ROGUE]` natively (R2-display §5).

### A3. Create in the HUB, never in CM

- **Settles critic B2:** create in the HUB (dec:5).
  - Inside CM, `OnDeckCreated` listeners open a creation session. The plugin's live note records "deferred OnDeckCreated -> ShowDeck -> StartEditingDeck" (NativeDeckEditor.cs:74-76, :87-91).
  - Native evidence: `OnDeckCreatedByPlayer` calls `ShowDeck` (D2:274-278).
- **Hook:** none. Call `CollectionManager.OnDeckCreated(NetCache+DeckHeader, Nullable<int>)` (D1:471-474; bound client-bindings.txt:1110) by reflection, exactly as the shop does: `Required(typeof(CollectionManager), "OnDeckCreated", new[] { typeof(NetCache.DeckHeader), typeof(int?) }).Invoke(manager, new object[] { header, null })` (NativeShopProjection.cs:23-25).
- Then call `MarkNetworkContentsLoaded()` (bound :1034) on `GetDeck(tempId)` and `GetBaseDeck(tempId)`, after checking both are non-null (NativeShopProjection.cs:26-31). Verified: `OnDeckCreated` itself already calls `AddDeck` and `MarkNetworkContentsLoaded` and fires the `DelOnDeckCreated` listeners (D1:471-474), so the extra calls are redundant but harmless, and the listeners also run at the HUB (the shop does the same without trouble).
- **Do not** add the deck to `decks`, call `ValidateNative` or call `SynchronizeExpectedNativeRanks` (B1, B3, B6).
- **PC:** the HUB logs `created <id>` with both decks non-null. CM then opens on that deck and no "new deck" naming flow appears.

### A4. NetCache header (settles critic B7)

- `AddDeck(header)` -> `AddDeck(header, true)`, which calls `NetCache.Get()`, `GetNetObject`, `NetCacheDecks.get_Decks` and `List<DeckHeader>.Add` (D2:940-949). The header lands in `NetCacheDecks.Decks` through native code.
- `RemoveDeck(long)` calls `get_Decks`, then `RemoveAt` in a `get_Item`/`get_Count` loop, and removes the deck from `m_decks`/`m_baseDecks` (D2:950-954).
- **So the plugin writes nothing to NetCache.** It asserts instead:
  - after O3, the header is in `DeckHeaders().Decks` (NativeDeckEditor.cs:527-532);
  - after K, it is gone.
- Whether the plugin's `cache[typeof(NetCacheDecks)]` is the same object as `NetCache.Get().GetNetObject<NetCacheDecks>()` is **unknown**. Assert on both, and log when they differ.
- Whether `AddDeck`'s list add depends on its bool is unknown (R2-flow §9). The assert covers it.
- **Nothing reaches disk.** Every `SendChanges` is swallowed (B8). `CreateCollectionDecksFromNetCache` rebuilds from `OfflineDataCache` (D2:962-968), so no band deck comes back at the next boot (R2-flow §7).
- **PC:** the asserts log OK. After a Done, restart the client: there is no band deck, and the sweep (K) logs nothing.

---

## B. Bookkeeping sites

| # | Site | Plugin file:line | Admit / skip | Change |
|---|---|---|---|---|
| B1 | `decks` session dictionary | NativeUIProbe.cs:50; built in Bootstrap 485-506 | **skip** | never add the temp id. `ExpectedHeader(long)`, `ValidateNative`, crafting and reprojection all assume a draft or loaner (round-1 plugin.md §2.1) |
| B2 | `DecksReady` (prefix on `CollectionManager.AreAllDeckContentsReady`) | NativeUIProbe.cs:201, 1225-1238 | **admit** | at :1232 compare `GetDecks().Count(d => !DungeonRunEditor.IsRunBuildId(d.ID))` with `decks.Count`. A band deck with no live session is swept (K6) first. When a session exists, also require `GetDeck(tempId)?.NetworkContentsLoaded() == true`. Without this change CM never becomes ready: the count is `decks.Count + 1` |
| B3 | `ValidateNative` | NativeUIProbe.cs:1383-1402 (throws at 1385-1386) | **skip** | never call it for a band id. Callers `BindPicker` 1415-1418 and `StartPicked` 1442-1444 run on TOURNAMENT: `Navigate(TOURNAMENT)` (NativeUIProbe.cs:1618) first runs the sweep (K6), so no band deck can be selected there |
| B4 | `Bootstrap` validation | NativeUIProbe.cs:580 (per round-1 plugin.md §2.3) | **skip** | sweep band ids before validating (K6) |
| B5 | `ExpectedRanks` / `ExpectedHeader(long)` / `ExactProjectedHeader` / `ExactProjectedDeckProblem` | NativeDeckEditor.cs:541-603 | **skip** | the temp deck uses only the 5-arg `ExpectedHeader` (A2). `ExpectedHeader(long)` reads `decks[id]` (round-1 plugin.md §2.4) |
| B6 | `SynchronizeExpectedNativeRanks` / `ApplyNativeRanks` | NativeDeckEditor.cs:731-758 | **skip** (naturally) | it iterates only `ExpectedRanks` keys. Never call it in the editor flow |
| B7 | `ReorderLocalDrafts` | NativeDeckEditor.cs:634-698 (throw 645-647); `RestoreWorkingDecks` 760-768 | **admit, by exclusion** | at :645 add `.Where(d => !IsRunBuildId(d.ID))`; in `RestoreWorkingDecks` skip band ids, so `CopyFrom(baseDeck)` never wipes the build. Reorder cannot be reached in edit mode; this guards a failed sweep |
| B8 | `SaveLocalDraft` / `DeckSaveAction` (prefix on `CollectionDeck.SendChanges`) | NativeDeckEditor.cs:146-224; NativeUIProjection.cs:719-729 | **swallow** | the first statement after the SideboardDeck mapping at :148 is `if (active && DungeonRunEditor.IsRunBuildId(__instance.ID)) { LogOnce(__0); return false; }`. It comes before the supported-source filter (:149-158), so unsupported sources do not pass through either. Today a known source on an unknown id is rejected and throws "Unknown local deck" (:186-189). `NativeCrafting.PreserveRecipe` runs first at `Priority.First` (round-1 plugin.md §0.4); the swallow must not depend on it. It covers saves S1-S4 of R2-flow §5 |
| B9 | `DeleteLocalDraft` / `preparedDeletes` / `deletedDrafts` | NativeDeckEditor.cs:42-43, 396-405 | **untouched** | removal is `RemoveDeck` (K). `preparedDeletes` is not used (settles critic B5) |
| B10 | `TryPrepareLocalDelete` (every native-delete prefix) | NativeDeckEditor.cs:294-299 (callers 233-292) | **refuse silently** | for a band id return false and log, with no `ReportDeleteFailure` popup. `PrepareLocalDelete` would throw "Loaner decks cannot be deleted" (NativeDeckEditor.cs:309-310). The deck options menu is hidden (H), so this is defence only |
| B11 | Tray limits | NativeDeckLimits.cs:15-23, 40-45; NativeUIProjection.cs:161-172 | **skip + gate** | `GateNewDeckButton` (NativeDeckLimits.cs:40-45) gives false while a session exists. Who-builds (M) uses the plank when `GetDecks().Count >= NativeDeckLimit` (111), so the temp deck is never the 112th: native layout there is unknown (round-1 plugin.md §2.10) |
| B12 | `LocalDeckCopyCount` | NativeDeckEditor.cs:22-28 | **admit** | gate `… && (IsPersonalId(ID) && GetDraft(ID) != null && CustomCards.Any(…) \|\| NativeDungeonRunEditor.Owns(ID))`, so the temp id drops the draft and `CustomCards` terms. `DeckCopyCount` groups by `LocalCardCatalog.CopyGroup` (NativeUIProjection.cs:153-154), the same grouping as the candidates (DungeonRunRuntime.cs:261) and the run start (:332). Native `GetCopiesInDeck` already counts counterparts (D2:1548-1551). This keeps the counters consistent (R2-rules §5) |
| B13 | `LoanerOwnership` / `LoanerRuleset` | NativeUIProbe.cs:1181-1193 | **pass through** | not a loaner. Validity is V1 (D) |
| B14 | `LocalDeckDisplayHero` | NativeCosmetics.cs:12, 25-34 | **pass through** | acts only for personal or loaner ids (:27-28). Native shows the header hero |
| B15 | `BindCosmeticTray` | NativeDeckEditor.cs:124-144 | **pass through** | no draft. The trays are hidden (H) |
| B16 | `AttachRecipeGuide` (ShowDeck postfix) | NativeRecipePicker.cs:27-28, 225-232 | **skip** | add `if (NativeDungeonRunEditor.Owns(manager.GetEditedDeck()?.ID ?? 0)) return;`. It is likely harmless today (R2-flow O6) but explicit |
| B17 | `CreateLocalDraft` (SendCreateDeck) | NativeDeckEditor.cs:53-85 (throws 62-63) | **refuse** | while a session exists: log and return false, with no throw |
| B18 | `HidePersonalDeckTutorial` | NativeDeckEditor.cs:448-453 | **admit** | the sibling predicate (H0) returns false for the temp deck. This guards against the `Options.SetLong(188, id)` write (D2:2127-2150) on any path that still reaches it |
| B19 | Crafting reprojection, deep scan, `NativeCosmeticsState` | NativeUIProbe.cs:1260-1341, 2173; NativeCosmeticsState.cs:135-160 | **pass through** | they iterate `decks` only, or are diagnostics. Crafting is blocked (H) |

- **Pure:** `DungeonRunEditor.NonBandCount(IEnumerable<long> ids)`, used at B2 and in the sweep.
- **Test:** `NonBandCount([900000300, 910000010, 900000215]) == 2`.
- **PC:**
  - CM opens (B2);
  - a Done and a Back leave the log free of `Unknown local deck` and `Unmapped/unloaded native deck`;
  - TOURNAMENT afterwards binds normally.

---

## C. Open into card view

The order is R2-flow §1 (O0-O9). It starts from `NativeDungeonRunEditor.Begin(cls, row, deck, seed)`, which is called only at the HUB (M says who calls it).

| Step | What | Member / plugin |
|---|---|---|
| O0 | Preconditions: `ReopenReady()` (NativeDungeonRunUI.cs:180-193); who-builds = Editor (M); non-empty candidates; `NextRunBuildId` not null; no band deck in `GetDecks()` after the sweep | — |
| O1 | Session: `{cls,row,deck,seed, cards = EditorCards(BuildCandidates(row)), tempId, heroes, stage = Creating}` | `DungeonRunRuntime.BuildCandidates` (DungeonRunRuntime.cs:237-267) |
| O2-O3 | Header (A2) + `OnDeckCreated(header, null)` + `MarkNetworkContentsLoaded` (A3) + hero test (A2) | NativeShopProjection.cs:23-31 pattern |
| Seed | Only on a re-open (J2): fill both decks with `FillFromTemplateDeck(TemplateDeck)` (bound :1019), as `ProjectMissingDraftCards` does (NativeDeckEditorSave.cs:203-213). `m_cardIds` comes from `DungeonRunEditor.SeedCounts(list, cards)` (client id -> count) and the runes from A2. A1/A2/A4 allow every add while `stage == Creating` (the `seeding` belt; whether template filling reaches `AddCard` is unknown) | `CollectionManager+TemplateDeck` (NativeDeckEditorSave.cs:207) |
| O4 | `stage = Entering`; `CollectionManager.Get().NotifyOfBoxTransitionStart()` (bound :1107); `NativeUIProbe.Navigate(SceneMgr.Mode.COLLECTIONMANAGER)` (NativeUIProbe.cs:1548-1552, 1598). `Navigate` returns silently on refusal (:1602-1603), so if `!NativeUIProbe.IsLoading(COLLECTIONMANAGER)` (:2546): `RemoveDeck` + plank fallback | — |
| O5 | Session tick: mode == CM, not transitioning, `!SceneWaiting` (:2545), `GetCollectibleDisplay() is CollectionManagerDisplay`, `CollectionDeckTray.Get() != null`, `!IsInEditMode()` (bound :1105). The ready flag is **unknown**; the candidate is `DeckTrayDeckListContent.IsDoneEntering()` (D2:2389, no IL), else N frames after these hold. 60 s timeout -> Abort | — |
| O6 | `display.ShowDeck(tempId, false, false, null)`: `ShowDeck(System.Int64,System.Boolean,System.Boolean,System.Nullable<CollectionUtils+ViewMode>)` (D2:299; bound :1127). The chain is `GetDeckHeroClasses` -> `GetClasses()` (D2:305-309) -> `ShowDeckHelper` -> `StartEditingDeck` (FT_STANDARD ruleset, `SetEditedDeck`) + `UpdateFiltersForDeck` + `CollectionDeckTray.ShowDeck(ViewMode)` (pushes the deck-contents Back handler) + `CanView*` (D2:300-304, 882-886, 1818-1824). The bools copy `ShowCurrentEditedDeck`'s constant 0 (D2:295-298); what they mean is **unknown** | — |
| O7 | Verify: `IsInEditMode()`, `GetEditedDeck().ID == tempId`, `tray.IsShowingDeckContents()` (bound :1221). Otherwise Abort | — |
| O8 | `tray.InitializeRuneIndicatorVisual(deck)` (bound :1078; recipe pattern NativeRecipePicker.cs:201). It is not in `ShowDeckHelper`'s call list (D2:302). On DK rows, optionally `TutorialDeathKnightDeckBuilding.ShowTutorial(...)` as in NativeRecipePicker.cs:204-205. No extra `UpdateFiltersForDeck`: `ShowDeckHelper` applies it (D2:302; R2-display N2) | — |
| O9 | `stage = Editing` | — |

- **Pure:**
  - `DungeonRunEditor.Stage` enum: None, Creating, Entering, Editing, Leaving.
  - `DungeonRunEditor.SeedCounts(IReadOnlyList<string> engineList, EditorCards cards) -> IReadOnlyDictionary<string,int>`. It maps engine ids to client ids through `RunBuildCard.ClientId`, and throws on an engine id that is not a candidate. The run start re-checks the list anyway (DungeonRunRuntime.cs:303-307).
- **Test:**
  - a 10-card list with 2x one card gives 9 keys summing to 10, with client ids;
  - an unknown id throws.
- **PC:** from the run menu's Deck step choose Build Deck. The HUB goes to CM, and CM opens on "Build Deck" in card view with the row's class page and an empty tray. Repeat from the native picker's Build Deck pouch.

---

## D. 10-card limit and the native-rules bypass

### D1. Size

All of these are guarded by `Owns(deck.ID)`; the rule set is shared (`s_FormatRulesets`, D2:1373, 1382-1387), so it is never mutated (R2-rules F2).

| Id | Hook (prefix) | Patch | Covers |
|---|---|---|---|
| S1 | `DeckRuleset.GetDeckSize(CollectionDeck) : Int32` (D2:1419-1422) | `static bool RunBuildMax(CollectionDeck __0, ref int __result)` -> `__result = size` (`rules.Rules.StarterSize`, 10, as NativeDungeonRunUI.cs:302); return false | `GetMaxCardCount` (D1:873-876), the `GetCardCountRange` max (D2:1194-1198), `CollectionManager.GetDeckSize` (D2:867-871), the extra-cards popup (D2:1784-1789) |
| S2 | `DeckRuleset.GetMinimumAllowedDeckSize(CollectionDeck) : Int32` (D2:1428-1431) | same shape | the `GetCardCountRange` min |
| S3 | `DeckRuleset.GetDeckSizeWhileEditing(CollectionDeck, EntityDef) : Int32` (D2:1424-1427) | same shape | drops the overcap extra and the tag-997 bump; `CollectionManager.GetDeckSizeWhileEditing` routes here (D2:872-876); a hidden tray pre-check (`GetRequiredDeckSpaceForAdd`, D2:2545, no IL) sees 10 |
| S4 (contingent) | `CollectionDeckTray.OnCardCountUpdated(Int32, Int32) : Void` (D2:2256-2261) | `static void RunBuildCount(ref int __1)` -> `__1 = size` when the edited deck is owned | only the "x/10" label; that `__1` is the max is an assumption (R2-rules §1.2, R2-display §3) |

**Hard stop at 10:** native has no visible main-deck size refusal. `DeckRule_DeckSize.CanAddToDeck` checks only the sideboard max (D2:1512-1516; R2-rules F3). The stop is E's `AddReason` -> `"size"` (DungeonRunBuild.cs:78).

### D2. "Complete at 10"

- **Hook V1:** prefix `DeckRuleset.IsDeckValid(CollectionDeck, out IList<DeckRuleViolation>, DeckRule+RuleType[]) : Boolean` (D2:1412; IL D1:1046-1049; the 2-arg form at D1:1042-1045 delegates to it).
  - Patch: `static bool RunBuildValid(CollectionDeck __0, ref IList<DeckRuleViolation> __1, ref bool __result)`.
  - Args: `new[] { typeof(CollectionDeck), typeof(IList<DeckRuleViolation>).MakeByRefType(), typeof(DeckRule.RuleType[]) }`. The `MakeByRefType` precedent is NativeUIProbe.cs:176.
  - Body: `reasons = DungeonRunEditor.DoneReasons(...)` (E). Set `__result = reasons.Count == 0` and always assign `__1`: an empty `List<DeckRuleViolation>`, or one `new DeckRuleViolation(ruleset.GetDeckSizeRule(deck), text)` (ctor D1:1048; `GetDeckSizeRule` D2:1414).
  - Memoize on the slot signature: callers include `get_IsValidForRuleset` (D1:860-863), whose call rate is unknown.
- **V1 is cosmetic** (deck box, `IsValidForRuleset`, the tutorial gate at D2:2107-2126). **Native Done never checks validity** (D2:1851-2020; R2-rules F5), so I owns the refusal.
- **Which member drives the deck-complete glow is unknown** (`ShouldShowDeckCompleteHighlight`, D2:2556, no IL).

### D3. Native rule bypass (settles dec:7)

- **Hook A1:** prefix `DeckRuleset.CanAddToDeck(EntityDef, TAG_PREMIUM, CollectionDeck, RuleInvalidReason&, DeckRule&, DeckRule+RuleType[]) : Boolean` (D2:1401-1405).
  - Patch: `static bool RunBuildCanAdd(EntityDef __0, CollectionDeck __2, ref RuleInvalidReason __3, ref bool __result)`.
  - Args: `new[] { typeof(EntityDef), typeof(TAG_PREMIUM), typeof(CollectionDeck), typeof(RuleInvalidReason).MakeByRefType(), typeof(DeckRule).MakeByRefType(), typeof(DeckRule.RuleType[]) }`.
  - Body: `verdict = DungeonRunEditor.AddVerdict(stage)`.
    - Creating (seeding): `__result = true`.
    - Editing: `code = AddCode(...)` (E). `__result = code == null`, and `__3 = code == null ? null : new RuleInvalidReason(text, 1, false)` (ctor bound :87).
    - Leaving: `__result = false`.
    - Return false in every case. **No popup here**: the call can run per page visual.
  - Same for the list overload, D2:1406-1410 (`ref List<RuleInvalidReason> __3, ref List<DeckRule> __4`).
- **Why A1 and not the ignore list** (dec:7):
  - Every **ruleset** add check funnels into the two `DeckRuleset.CanAddToDeck` overloads (ref and list). `CollectionDeck.CanAddCard` -> `CollectionManager.GetDeckRuleset().CanAddToDeck(…ref…)` (D2:1288-1296); the list overload loops `m_rules` itself (D2:1406-1410). Verified: the tray's tourist limit (`AddCardToDeck`, D2:1777-1781) and `CollectionDeck.CanCardBeAddedAsOwned` (D1:939-942) sit outside them. The ruleset is `StartEditingDeck`'s `m_deckRuleset` (D2:882-886, 892-896).
  - Only `PLAYER_OWNS_EACH_COPY`, `IS_CLASS_CARD_OR_NEUTRAL` and `IS_DUAL_CLASS_CARD_OR_NEUTRAL` are names confirmed in code (NativeUIProbe.cs:1191; NativeDeckEditorSave.cs:61-62). `IS_IN_FORMAT`, `IS_NOT_ROTATED`, `IS_CARD_PLAYABLE` and `IS_IN_CARDSET` are **unknown names** (R2-rules F8, §2.3).
  - A1 needs no enum names and also bypasses `DefaultIgnoreRules` (D2:1296; contents unknown).
  - Update dec:7 to read "A1 replaces the ignore-list idea".
- **A3 (contingent; only if a run-legal candidate is missing from the pages on the PC):**
  - prefix `DeckRuleset.Filter(EntityDef, CollectionDeck, DeckRule&, DeckRule+RuleType[]) : Boolean` (D2:1388-1392);
  - prefix `DeckRuleset.FilterFailsOnShowInvalidRule(EntityDef, CollectionDeck, DeckRule+RuleType[]) : Boolean` (D2:1438-1442);
  - for a candidate client id: `__result = true` (Filter) / `false` (FailsOnShow), and `__2 = null`;
  - non-candidates stay native, so illegal cards still show and are refused with a popup (spec:137-139).
- **Pure** (see E for `AddCode`): `DungeonRunEditor.AddVerdict(Stage) -> Allow | Rule | Refuse`.
- **Test:** `AddVerdict(Creating) == Allow`, `AddVerdict(Editing) == Rule`, Leaving/Entering/None == Refuse.
- **PC:**
  1. the tray label reads `n/10` (else add S4);
  2. the 11th add is refused with E's message;
  3. a 9-card deck box shows as incomplete and a 10-card one as complete;
  4. every candidate card is on the pages (else add A3).

---

## E. Add refusal

### E1. Client -> engine mapping (dec:6; critic W1/G4)

- **Pure:** `sealed class DungeonRunEditor.EditorCards`, built from `IReadOnlyList<RunBuildCard>` (DungeonRunCatalog.cs:89-99).
  - `ClientToEngine`: for each client id, the lowest engine id by ordinal. Collisions are listed in `Collisions` and logged; whether two engine ids share a `TransportCardId` is unknown.
  - `Owned(engineId)` and `Group(engineId)` mirror the plank's `BuildOwned`/`BuildGroup` (NativeDungeonRunUI.cs:290-291). `Name(engineId)`.
  - `EngineList(IEnumerable<string> clientSlots, out IReadOnlyList<string> unmapped)` maps the slots.
- **Plugin:** slots are read with `CollectionDeck.GetCardsWithCardID()` (bound :1022). It repeats ids per copy, as ValidateNative uses it (NativeUIProbe.cs:1387). Whether golden copies appear once per copy is likely but **unknown**; that is a PC check.

### E2. The reason

- **Pure:** `DungeonRunEditor.AddCode(EditorCards cards, IReadOnlyList<string> clientSlots, string clientId, DungeonRunClass row, int size, OfflinePoolPolicy policy, Func<string,string> cardReason) -> (string code, string engineId)`.
  1. If `clientId` is a candidate: return `DungeonRunBuild.AddReason(EngineList(slots), engineId, row.ClassSet, size, cards.Owned, cards.Group, policy, cardReason)` (DungeonRunBuild.cs:75-83).
  2. Otherwise (not a candidate):
     - `"size"` if the deck is full;
     - else, when `Cards.FromId(clientId)` exists and `clientId` maps to itself, its `CardReason` (DungeonRunBuild.cs:17-28), which gives the real code: class, hero, fabled, rune-class, …;
     - else `"owned <id>"` when `CardReason` is null (legal but not owned: candidates require count > 0, DungeonRunRuntime.cs:255);
     - else `"unavailable <clientId>"`.
- **Cost (per page visual through A1):** `AddReason` calls `DeckReasons` twice, and each `DeckReasons` calls `CardReason` per distinct card (DungeonRunBuild.cs:53-57, 81-82). Round-2 display warns against pool checks per visual (§2).
  - **Change:** give `DeckReasons`/`AddReason` an optional last parameter `Func<string,string> cardReason = null` (null = `id => CardReason(id, classSet, policy)`).
  - The session passes a memoized one (a `Dictionary<string,string>` per session).
  - Existing callers (NativeDungeonRunUI.cs:302, 656; DungeonRunRuntime.cs:330) are unchanged.
- **Done:** `DungeonRunEditor.DoneReasons(cards, clientSlots, row, size, policy, cardReason)` = `DeckReasons(EngineList(...), …)` plus `"unavailable <id>"` per unmapped client id.

### E3. Where the popup is shown (settles the R2-rules A2 vs R2-display §4 choice)

- **Hook A4 (popup site):** prefix `DeckTrayCardListContent.AddCard(EntityDef, TAG_PREMIUM, Boolean, Action<String>, Actor, Boolean, Boolean, DeckRule+RuleType[]) : Boolean` (D2:2544).
  - The call list shows every `CollectionDeckTray.AddCard` overload reaching `AddCardToDeck`, whose call list includes it (D2:1765-1779; R2-display N9). Verified: `AddCardToDeck` has no listing, so whether every path through it reaches this call, and in which order relative to the tile work, is unknown; P6 checks it on the PC.
  - Patch: `static bool RunBuildTrayAdd(EntityDef __0, ref bool __result)` with the edited deck from `CollectionManager.Get().GetEditedDeck()`. On a refusal: show the message, set `__result = false`, return false.
- **Hook A2 (silent belt):** prefix `CollectionDeck.AddCard(String, TAG_PREMIUM, Boolean, EntityDef, DeckRule+RuleType[]) : Boolean` (D1:939-942; D2:1264; bound :1014).
  - Same verdict, no message.
  - It covers adds that do not come from the tray, for example `AddCardWithPreferredPremium` (D2:1797), which has no IL.
- **The run start re-check stays the final guarantee** (`BuiltListRefusal`, DungeonRunRuntime.cs:324-338). It covers slot-level writes that skip `AddCard`: `FillFromCardList`, `AddCardsFrom`, `InsertSlotWithCard` (D2:1237, 1359, 1272), and Complete My Deck / DeckHelper (blocked, H).
- **Channel:** `GameplayErrorManager.Get().DisplayMessage(text)`, the native non-modal refusal `AddCardToDeck` uses for the tourist limit (D2:1777-1781). It is transient and never queues, so one popup per click is fine.
  - It is unbound today, and whether it is live in CM is likely but unknown. The tourist error uses it there.
  - Fallback when `Get()` is null: an `AlertPopup` (the `NativeCrafting.ShowError` shape, NativeCrafting.cs:698-705) behind a static `refusalOpen` flag. The flag is set on show and cleared in the response callback; while it is set, further refusals only log (debounce, R2-display §4).

### E4. Text (pure, in `NativeDungeonRunText`)

- `BuildAddRefusal(name, code)` exists (NativeDungeonRunText.cs:420-424). Its words helper `BuildReasonWords` (:428-440) words only size, copies, legendary, owned, runes and not-collectible.
  - Others fall back to the raw code, so "Cannot add X: class AT_064" would leak an engine id.
  - The plank never shows non-candidates, so it never hit this. The editor does.
  - **Extend `BuildReasonWords`:**
    - `class` -> "not a card of this hero's class";
    - `hero` -> "a hero card";
    - `fabled` -> "a Fabled card";
    - `rune-class` -> "rune cards are for Death Knight runs only";
    - `missing` / `unavailable` -> "not a card this run can use";
    - default -> "not allowed in a run".
  - The existing words come from `BuildStartRefusal` (:449-470) and are reused.
- Name: `cards.Name(engineId)`, else `EntityDef.GetName()` for non-candidates.
- New `EditorDoneRefusal(reasons, name)`: "Your deck is not ready: it has 9 of 10 cards; Fireball: at most 2 copies of a card."
  - This is `BuildStartRefusal`'s body with a prefix parameter: refactor it into `BuildReasonsText(prefix, reasons, name)`, keeping BD20's exact texts.
  - Header constants `EditorAddHeader = "Cannot add card"` and `EditorDoneHeader = "Deck not ready"`.

**Test (E):**
1. `EditorCards` maps client to engine, with a deterministic first-by-ordinal choice on a collision fixture, listed in `Collisions`.
2. `AddCode` on a candidate equals `AddReason`.
3. A Warrior card on a Mage row gives `class AT_064`, and its text is "Cannot add Fiery War Axe: not a card of this hero's class." with no id.
4. An unowned legal card gives `owned`; an id unknown to the engine gives `unavailable`.
5. A memoized `cardReason` gives the same lists as null over BD3-BD8's fixtures.
6. `EditorDoneRefusal` exact texts; BD20 still passes.

**PC:**
- a third copy, a second legendary, a card of another class and an 11th card each show one transient message;
- clicking 5 times shows no stacked popups.

---

## F. Dual class (phase 4)

### F1. Settles critic B4 and the round-2 rules-vs-display split (`GetHeroClasses` prefix C1 vs `ShouldShowTab` postfix)

| Hook | Kind | Body | Evidence |
|---|---|---|---|
| F1 `CollectionDeck.GetClasses() : List<TAG_CLASS>` (D1:986-989; D2:1326; bound :1023) | prefix | for an owned deck, `__result = new List<TAG_CLASS>{A, B}`, a **fresh list on every call** (`UpdateTabsForDualClass` calls `List.Add` on lists built from it, D2:1811-1815); return false | It covers `ShowDeck`'s filter (`GetDeckHeroClasses`, D2:305-309), `HasClass` (D1:991-994), the per-card class rules (D2:1556-1586), the page header (D2:2849-2851) and DK detection (D2:2878-2880). It also bypasses the card-derived dual and tourist logic |
| F2 `CollectionPageManager.ShouldShowTab(BookTab) : Boolean` (D2:2790-2795, IL 719) | postfix | for an owned edited deck: a `CollectionClassTab` with `TabInfo.tagClass` in {A, B, NEUTRAL} gets `__result \|\| HasClassCardsAvailable(tagClass)`; other class tabs get false; the cosmetic tabs (`m_heroSkinsTab`/`m_cardBacksTab`/`m_coinsTab`/`m_petsTab`, D2:2665-2668) get false (also H) | tabs read `GetHeroClasses()` + `GetClass()`, not `GetClasses()` (D2:2792; R2-display N3) |
| C1 `CollectionDeck.GetHeroClasses()` (D2:1321-1325) | **not used by default** | contingent: only if, with F1 + F2 + A1 (+A3), a class-B card is still hidden or locked on the PC | it would also change `CardIsClassCardOrNeutral`'s second argument (no IL, D2:1570) and the hero-skin class logic, whose semantics are unknown (R2-display §1) |

- **Decision:** the display way. F2 is needed anyway to hide the cosmetic tabs (H). Legality never depends on the class lists: A1 bypasses the class add rules and V1 the class validity rules.
- `GetClass()` stays the hero's class. `A = row.ClassSet[0]`, which the header hero must have (A2).
- `HasDualClassRule` stays false: it looks for `Type == 20` (D2:1433-1437), and Standard has no type-20 row (CT:63459-63474). So the tray's dual tabs (`UpdateTabsForDualClass`) stay off, likely a no-op.
- **Pure:** `DungeonRunEditor.ClassTags(DungeonRunClass row) -> IReadOnlyList<string>` = `row.ClassSet` (DungeonRunData.cs:106), in order. The client side parses them with `Enum.Parse(typeof(TAG_CLASS), …)` once per session.
- **Test:** a MAGE_ROGUE row gives `["MAGE","ROGUE"]` and a MAGE row `["MAGE"]`.
- **PC** (R2-display §1):
  - a MAGE_ROGUE temp deck (hero `HERO_08`, or `ULDA_Reno` first) shows the Mage, Rogue and Neutral tabs only;
  - a Rogue class card on the Rogue tab is unlocked and can be added;
  - the Mage-page jump at the open lands on Mage.
- **FALLBACK:** if class-B cards stay hidden after A3 and C1, dual rows stay on the plank (M).

---

## G. Death Knight (phase 4)

- **No special hooks** (R2-rules §4).
  - `AddReason` is authoritative for `"runes"` (more than 3 runes) and `"rune-class"` (DungeonRunBuild.cs:26, 59-67).
  - A1 bypasses `DeathKnightRuneLimit.CanAddToDeck` (D2:1591-1594), and V1 its `IsDeckValid` (D2:1595-1599).
  - `UpdateDeckRunes` keeps the native indicator in sync on each add (D2:1267-1271).
- O8 `InitializeRuneIndicatorVisual`. A seeded header carries the runes (A2). With F1, a DK + X row has `HasClass(DEATHKNIGHT)`, so the native DK UI engages (D2:2878-2880).
- **Residual unknown:** whether native rune ghosting (`UpdatePageGhostingForInvalidRunes` -> `ApplyRuneCardGhostEffectsForCurrentPage`, D2:2819-2822) greys exactly the cards `AddReason` refuses. A mismatch is only cosmetic, because A4/A2 decide adds.
- **Test:** BD6 already covers the rules. Add: `AddCode` on a DK row with 3 runes used refuses a 4th-rune card with `runes`, worded "a Death Knight deck needs at most 3 runes".
- **PC:**
  - on a DK row the rune indicator fills as rune cards are added;
  - a 4th-rune card is refused with the message;
  - Back on Power then re-opening (J2) shows the same runes.

---

## H. Blocked actions

- **H0, the sibling predicate.** `OwnsRunBuildEditor(CollectionDeck candidate, object instance)` in NativeDeckEditor.cs, beside `OwnsPersonalDeckEditor` (:499-520). It has the same terms, except that the `persisted` term (:517) becomes `NativeDungeonRunEditor.Owns(edited.ID) && stage == Editing`.
- **Pure:** `DungeonRunEditor.OwnsEditor(bool active, bool currentCollection, bool manager, bool tray, bool mainContext, bool exactDeck, bool exactInstance, bool runBuild)`, the same conjunction as NativeUIProjection.cs:813-816.
- **Test:** each false input gives false.

| Action (spec:140-142) | Hook | Plugin |
|---|---|---|
| New Deck | `GateNewDeckButton` postfix -> false while a session exists; `CreateLocalDraft` refuses without throwing (B17) | NativeDeckLimits.cs:40-45; NativeDeckEditor.cs:53-63 |
| Deck list / options menu (rename, delete, convert, share) | **new** prefix `CollectionManagerDisplay.ShouldShowDeckOptionsMenu() : Boolean` (D2:379-382, constant true) -> `__result = false`. Its callers are unknown, so also the existing `HidePersonalDeckConvert` with `\|\| OwnsRunBuildEditor` | NativeUIProbe.cs:284; NativeDeckEditor.cs:441-446 |
| Delete | B10, plus `PrepareEditingDeckDelete` (`CollectionDeckTray.DeleteEditingDeck(bool)`) returns false for an owned deck | NativeDeckEditor.cs:280-285 |
| Reorder | B7 | — |
| Complete My Deck | `BlockPersonalDeckCompletion` -> `!(OwnsPersonalDeckEditor \|\| OwnsRunBuildEditor)` | NativeDeckEditor.cs:496-497 (hook NativeUIProbe.cs:290) |
| DeckHelper | `HidePersonalDeckHelperChoices` -> false; `BlockPersonalDeckHelper` returns false with no helper for the temp deck (no local helper) | NativeDeckEditor.cs:455-483 |
| Paste | already blocked whenever active | NativeUIProbe.cs:272-273 (`BlockDeckImport`, NativeDeckEditor.cs:407-411) |
| Recipes / templates | the temp deck never enters DECK_TEMPLATE view; guard `ChooseLocalRecipe` for an owned edited id (refuse) | NativeRecipePicker.cs:147-223 |
| Crafting / disenchant (ownership must not change mid-build) | `CanCraft` -> `NotCraftable`; `BeforeCreate` / `BeforeDisenchant` -> false + `ShowError(NativeDungeonRunText.EditorCraftingOff)`. The check goes **before** the `if (!Enabled) return true;` lines | NativeCrafting.cs:138-144, 161-169, 233-243 |
| Rename | **new** prefix `DeckTrayDeckListContent.RenameCurrentlyEditingDeck() : Void` (D2:2388) -> skip. This avoids the spinner `OnRenameFinished` shows (D2:111-116). The save is already swallowed (B8) | — |
| Cosmetics | **new** prefixes `CollectionManagerDisplay.CanViewHeroSkins/CanViewCardBacks/CanViewCoins/CanViewPets() : Boolean` (D2:248-257) -> false, plus F2's cosmetic-tab false. `ShowDeckHelper` reads all four during the open (D2:302). A skin drag would otherwise rewrite `HeroCardID` (NativeDeckEditor.cs:95-121) | — |
| Wild-to-Standard tutorial / `Options` 188/288 | B18; Back never runs native `OnBackOutOfDeckContentsImpl` (J) | — |

**PC:** for an editor deck, check that:
- there is no options menu;
- the deck name cannot be clicked to rename;
- the hero-skin, card-back, coin and pet tabs are absent;
- right-click crafting refuses with the message;
- Complete My Deck and the suggestions do nothing.

---

## I. Done

- **Settles critic B3:** a prefix on `CollectionDeckTray.DoneButtonPress(UIEvent) : Void` (D2:1851; listing D2:1856-2020).
  - It is the only member that tells Done from Back. Native Done never validates and never saves: it ends in `Navigation.GoBack()` (D2:2009-2020), and the save is behind `OnBackOutOfDeckContentsImpl(false)` -> `SaveCurrentDeckAndEnterDeckListMode` (D2:2046-2049, 2198-2199, 2218-2222).
  - The prefix runs before the card-back coroutine branch (D2:1857-1872).
  - The coroutine re-enters `DoneButtonPress` (D2:2041-2045), so the stage guard ignores it once Leaving.
- **Hook:**
  - `Patch(typeof(CollectionDeckTray), "DoneButtonPress", new[] { typeof(UIEvent) }, prefix: nameof(RunBuildDone))`;
  - `static bool RunBuildDone(CollectionDeckTray __instance)`.
  - Guard: `active && CurrentCollectionScene() && ReferenceEquals(__instance, CollectionDeckTray.Get()) && Owns(GetEditedDeck()?.ID)`. Otherwise return true.
  - In stage Leaving, return false and do nothing.
- **Body (stage Editing), always returning false:**
  1. `reasons = DoneReasons(...)` (E2).
  2. Not empty: `AlertPopup` (`EditorDoneHeader`, `EditorDoneRefusal`) behind the `refusalOpen` debounce; stay in the editor.
  3. Empty:
     - `list = EngineList(slots)`;
     - `NativeDungeonRunUI.BuiltWhenHome(cls, row, deck, list)`, a **new** slot `pendingBuilt` like `BuildWhenHome` (NativeDungeonRunUI.cs:111-115);
     - `stage = Leaving`, `leave = Done`.
     - The leave runs on the next session tick (K), never inside the UI callback.
- **Plugin `NativeDungeonRunUI`:**
  - `pendingBuilt` is exempt from the 180 s lapse, like `pendingBuild` (:166-167: `pendingBuild == null && pendingBuilt == null && …`).
  - It is cleared wherever the other slots are (:95, :99, :105, :113, :118).
  - `Open()` gets a third branch after :140-145: `classId/rowId/deckId = …; BeginBuild(); built = list.ToList(); builtByEditor = true; step = Step.Power; pendingBuilt = null;`.
  - The order matters. `BeginBuild` sets `buildRow = rowId` and a new `built` (:203-211). Assigning `built` after it lets `SetupFlow`'s :524 check (`built == null || buildRow != row.Id`) pass.
  - `builtByEditor` (new) is reset in `DropBuild` (:213-217).
- **Pure:** none beyond E2. The hand-off is client state.
- **PC:**
  - Done at 9 shows "Your deck is not ready: it has 9 of 10 cards." and the editor stays;
  - Done at 10 goes CM -> HUB, the run menu opens on Power, and Confirm lists the 10 cards (HANDOFF.md:341 pattern);
  - the run starts with them.

---

## J. Back

### J1. Back from the editor (without Done) -> the Deck step, list discarded (spec:148)

- **Hook:** the existing prefix `PrepareDiscardDeck(CollectionDeckTray __instance, bool deleteDeck, ref bool __result)` on `CollectionDeckTray.OnBackOutOfDeckContentsImpl(Boolean)` (NativeUIProbe.cs:265; NativeDeckEditor.cs:267-278).
  - The temp-id branch goes **at its top, before the `!deleteDeck` return at :269-270**. A second prefix would raise Harmony ordering questions (R2-flow §3.2).
  - It keeps native's two early exits (mirrored at :272-273): return true when they would fire.
  - Otherwise:
    - `NativeDungeonRunUI.DeckWhenHome(cls, row, deck)`, a **new** slot that opens on Step.Deck with class and row kept and is exempt from the lapse like `pendingBuilt`;
    - `stage = Leaving`, `leave = Back`;
    - `__result = true`; return false.
  - Native is skipped. That means:
    - no `DeleteDeck` (which would throw "Loaner decks cannot be deleted", NativeDeckEditor.cs:309-310);
    - no `Options` 188/288 write (D2:2127-2150);
    - no `SaveCurrentDeck`.
- Routes into it: `Navigation.GoBack()` -> the handler that `CollectionDeckTray.ShowDeck` pushed -> `OnBackOutOfContainerContents` -> `Impl(false)` (D2:1818-1824, 2046-2049). In edit mode the tray button says Done (`UpdateDoneButtonText`, D2:2231-2236), so Back is Escape or another `GoBack`.
  - Escape -> `GoBack` in CM is **likely, unknown**. `BeforeEscape` acts only while the run menu is open (NativeDungeonRunUI.cs:86-91).
- **Leaving CM by another path** (scene change with stage Editing): the session tick treats it as Back (R2-flow §4).
- **Pure:** `DungeonRunEditor.LeaveTarget(Leave leave) -> Power | Deck`. Done -> Power; Back and Recover -> Deck.
- **Test:** the mapping.
- **PC:** Escape in the editor goes CM -> HUB -> the run menu's Deck step with class and row kept. The band deck is gone. `Options` 188 is unchanged.

### J2. Back on the Power step after editor Done -> re-open the editor with the 10 cards (owner, dec:3)

- **Plugin:** `NativeDungeonRunUI.Back()`, `case Step.Power` (:247-250). Today it sets `step = built != null ? Step.Build : Step.Deck`.
  - Replace it with `DungeonRunEditor.PowerBack(builtByEditor, built != null, editorAvailable)`.
  - On `Editor`: `NativeDungeonRunEditor.Begin(classId, rowId, deckId, seed: built.ToList()); Close();`. The menu is at the HUB, so A3 holds.
  - On `PlankBuild`: today's `Step.Build` with the list kept.
  - On `Deck`: `Step.Deck`.
- Seeding is C's Seed row. A Back out of the re-opened editor without Done is J1: Deck step, list discarded, as spec:148 says.
- A Confirm refusal (`StartRun` keeps the list, NativeDungeonRunUI.cs:313-323) followed by Back reaches Power and then this rule.
- **Pure:** `PowerBack(bool builtByEditor, bool hasList, bool editorAvailable) -> Editor | PlankBuild | Deck`.
  - `(true, true, true)` -> Editor;
  - `(true, true, false)` -> PlankBuild;
  - `(false, true, _)` -> PlankBuild;
  - `(_, false, _)` -> Deck.
- **Test:** the table above.
- **PC:**
  - after Done, Back on Power re-opens CM on the same 10 cards (and runes on DK);
  - Done again returns to Power with the same list.

---

## K. Removal and leaving CM

### K1-K7. The leave (Done, Back, Recover-in-CM)

The session tick is called from `NativeUIProbe.Update` right after `NativeDungeonRunPicker.Update()` (:387). Each step runs in its own try/catch.

- **L1.** `stage = Leaving`, set by I or J. Every hook is inert, and A1/A2/A4 refuse.
- **L2.** The hand-off slot is written by I/J, then `Reopen()` runs.
  - It needs an internal hook: `NativeDungeonRunUI` exposes `BuiltWhenHome`/`DeckWhenHome`, each ending in `Reopen()` (NativeDungeonRunUI.cs:96).
- **L3.** End edit mode with `tray.SaveCurrentDeckAndEnterDeckListMode()` (D2:2218-2222). Its `SendChanges` is swallowed (B8), and it runs `DoneEditing` -> `ClearEditedDeck` (D2:887-891, 904-908).
  - Without this, the service-level `CollectionManager` keeps `m_editMode` and the temp deck as the edited deck into the HUB.
  - On an exception: `CollectionManager.Get().DoneEditing()` alone.
- **L4.** `Navigation.Clear()`. Native `Exit` does the same on its offline branch (D2:191).
  - Without it, the deck-contents and CM-screen handlers stay on the stack, and an Escape at the HUB would call into a destroyed tray.
- **L5.** `CollectionManagerDisplay.Exit()` (bound :1125). On an exception: `NativeUIProbe.Navigate(SceneMgr.Mode.HUB)`, then check `IsLoading(HUB)`.
- **L6.** At the HUB (mode HUB, not transitioning): remove the temp deck (K8), then clear the session.
- **L7.** `NativeDungeonRunUI.Update` -> `ReopenReady` -> `Open()` on Power (Done) or Deck (Back/Recover).

### Settles critic B1: native `Exit`, not `Navigate(HUB)`

- The Exit listing (D2:135-215) shows:
  - remaps go to 3 (D2:156-179);
  - `if (!IsLoggedIn() && mode != 3) { ShowReconnectHelperDialog; mode = 3; Navigation.Clear(); }` (D2:180-191);
  - then `SetNextMode(mode, …)` (D2:208-214).
- From a HUB entry, prev mode = HUB. If 3 is HUB, there is no dialog. `(int)SceneMgr.Mode.HUB == 3` is inferred, not read (R2-flow §0 note; critic W4).
- `Navigate(HUB)` skips Exit's teardown: `EnableInput(false)`, `DestroyAllPopUps`, `CollectionDeckTray.Exit`, `m_pageManager.Exit`, `SetCraftingUIActive(false)` (D2:135-155).
- This corrects spec:144-145 ("`Navigate(HUB)`, the only offline exit `CollectionExitAllowed` allows") and round-1 plugin.md §6 (critic W2).

### Belt: remove the dependence on the enum value

- **Pure:** `NativeMenuFlow.CollectionExitAllowed(bool loggedIn, bool localActive, bool pickerOrigin, bool runBuildLeaving = false) => loggedIn || localActive && (pickerOrigin || runBuildLeaving)` (NativeMenuFlow.cs:35-36).
- **Plugin:** `CollectionExitLoggedIn` (NativeUIProbe.cs:1574-1575) passes `NativeDungeonRunEditor.Leaving`.
- `NativeMenuFlow.cs` is compiled by no test today (R2-flow §6.2). Either add it to Sched9.csproj (it has no client types: NativeMenuFlow.cs:1-39), or mirror the formula in `DungeonRunEditor.ExitAllowed` and have NativeMenuFlow call that. **Recommend the second:** one pure home, tested.
- **Test:** `ExitAllowed(false, true, false, true)` is true; `(false, true, false, false)` is false; `(false, false, true, true)` is false.

### K8. Removing the temp deck (settles critic B5)

- `CollectionManager.RemoveDeck(System.Int64) : CollectionDeck` (D2:950-954), by reflection, **at the HUB only**.
  - It drops the working deck, the base deck and the NetCache header.
  - It fires no listeners and does nothing to UI or navigation.
- Not used:
  - `DeckTrayDeckListContent.DeleteDeck` / `DeleteEditingDeck`: they need the tray and animate (D2:2373-2384). The plugin's prefix also throws for a non-personal id (NativeDeckEditor.cs:280-292, 309-310).
  - `OnDeckDeleted(long)`: it pops Navigation, can call `SetNextMode`, and shows "GLUE_OFFLINE_DECK_DELETED_REMOTELY_ERROR_BODY" (D2:592-607).
- Steps:
  1. Assert that `GetDeck`, `GetBaseDeck` and the header exist (A4).
  2. `RemoveDeck`.
  3. Assert all three are gone.
  4. Log `DUNGEON_RUN_EDITOR removed <id>`.
  5. Set `tempId = 0` and stage None.
- **K6, the sweep.** At HUB idle with no session, on any CM entry with no session, at Bootstrap and before `Navigate(TOURNAMENT)`: `RemoveDeck` every id in `GetDecks()` for which `IsRunBuildId` holds, with a warning. It is only reachable after a failed leave.
- **PC:**
  - after Done and after Back, the HUB log shows `removed`;
  - entering Collection normally shows no Build Deck box;
  - TOURNAMENT shows the usual decks;
  - Escape at the HUB right after the return does nothing odd.

---

## L. Fail / Recover safety

- **Hook:** in `NativeUIProbe.Fail` (NativeUIProbe.cs:355-364), add `if (NativeDungeonRunEditor.Recover(error)) { sceneWaiting = false; return; }` after the picker line :359 and before `ready = false` at :360.
  - It must run there: once `failure` is set, `Update` stops at :369, and `Navigate` refuses while `sceneWaiting` (:1602).
- **Also routed to Recover:**
  - `Abort(error)` from any hook's catch, run on the next tick;
  - the O5 60 s wait;
  - the scene timeout (:393-394 -> `Fail`).
- **Pure:** `DungeonRunEditor.RecoverAction(Stage stage, bool atHub, bool recoveredOnce) -> None | RemoveAndPlank | DeckAndLeave`.
  - `recoveredOnce` -> None: the second failure in a session takes the normal Blocked path.
  - None stage -> None.
  - Creating, or Entering while still at the HUB -> RemoveAndPlank: `RemoveDeck` if present, clear the session, then `NativeDungeonRunUI.BuildWhenHome(cls,row,deck)` with the caption `NativeDungeonRunText.EditorFailedCaption` (new: "The deck editor could not open. Build your deck here instead.").
  - In CM (Entering, Editing, Leaving) -> DeckAndLeave: `DeckWhenHome` + caption, then L1, L3-L7 with each step guarded. A failing `Exit` falls back to `Navigate(HUB)`.
- Also set a process-level `editorFailed` latch, read by M: after a Recover, Build Deck uses the plank until restart.
- The session flag is cleared on every exit: K8 step 5, RemoveAndPlank, and the sweep.
- **Test:** the full `RecoverAction` table: stage × atHub × recoveredOnce.
- **PC:** a debug switch (log-only build or a config key) throws inside O6 once. The player lands on the plank Build step with the caption, the band deck is removed, and the next Build Deck uses the plank.

---

## M. Plank fallback scope

- **Pure:** `DungeonRunEditor.Builder(DungeonRunClass row, int candidates, int reachable, int size, int nativeDecks, int nativeLimit, bool editorFailed, bool dualAndDkInEditor) -> Editor | Plank`.
  - It returns **Plank** when any of these holds:
    1. `editorFailed` (L latch);
    2. `candidates == 0`, or `reachable < size`. The plank shows "Not enough cards in your collection for this hero (n of 10)" (`BuildReachable` / `BuildNotEnoughLine`, NativeDungeonRunText.cs:404-414), and the editor could never reach Done;
    3. `nativeDecks >= nativeLimit` (B11);
    4. `!dualAndDkInEditor && (row.IsDual || row.ClassSet.Contains("DEATHKNIGHT"))`. This is the phase gate (spec Phases 3/4, spec:198-199): `false` in the phase-3 build, `true` from phase 4 after F's and G's PC checks pass.
  - Otherwise **Editor**.
  - `nativeLimit` is `NativeUIProjection.NativeDeckLimit` (111), passed in.
- **Callers (`NativeDungeonRunUI`):**
  1. the plank Deck step, `ChooseDeck(id, build: true)` (:277-282): on Editor, call `NativeDungeonRunEditor.Begin(classId, rowId, id, null)` and `Close()` instead of `BeginBuild()`;
  2. the native picker's hand-off `BuildWhenHome` -> `Open()`'s `pendingBuild` branch (:140-145): on Editor, call `Begin(...)` and return **before** `popup.Open()` (:148);
  3. Power Back (J2).
  - On Plank, all three keep today's behaviour.
  - `NativeDungeonRunPicker`'s Build Deck pouch (NativeDungeonRunPicker.cs:683-693) is unchanged: it always hands to `BuildWhenHome`, which decides.
- **What still uses the plank, and when:**
  - in phase 3: every dual (Tombs) row and every Death Knight row;
  - always: a collection that cannot reach 10; a tray at 111 native decks; any session after an editor Recover; the RemoveAndPlank path.
- **Test:** each Plank condition alone, plus the all-clear Editor case. The phase-gate flag is tested both ways.
- **PC:**
  - with 111 native decks, Build Deck opens the plank;
  - a Death Knight row in the phase-3 build opens the plank;
  - a profile with fewer than 10 legal copies opens the plank with the "Not enough" line.

---

## N. Doc updates (phase 4 deliverable; critic G3)

- **`docs/design/dungeon-run.md:149` (R12).** Add a bullet after :153: "Build Deck's editor mode (since phases 3-4) never writes a constructed deck, profile draft or collection count. It projects a session-only native deck (ids 900000300..900000399) whose every save is swallowed and which is removed at the HUB. A run match never sees it."
- **`docs/design/dungeon-run.md:200`.** Replace "on the run menu's plank Build step (…; the Collection Manager editor is a later phase)" with: "in the client's Collection Manager editor on a temporary session-only deck (Done hands the list to the run menu's Power step; Back returns to the Deck step), or on the run menu's plank Build step where the editor is not used (Tombs dual and Death Knight rows until phase 4, a collection that cannot reach 10, a full tray, after an editor failure)".
  - Change "no deck id" to "no saved deck id (the temporary native id is never persisted)".
- **`docs/design/dungeon-run.md:313`.** Change "Not dependencies: … card mastery and the deck editor" to keep the deck editor out of the dependency list with a note: "the Collection Manager editor is borrowed by Build Deck in a read-only mode (saves swallowed, temporary deck removed)".
- **`handoff/phase9/tests.md`.** New section "## 8. Build Deck editor (phases 3-4)" after §7 (:338-378). It gets a BD21+ table (the Test lines of A1, A2, B, C, D3, E, F, G, H0, J1, J2, K belt, L, M) and a "Not covered here" paragraph like :373-378: every client hook, the session tick, the leave, and the owner PC checks of P.
- **`HANDOFF.md`.**
  - §3.5 (:313-352): change :331-332 "Not done …" to point at the phases-3-4 plan.
  - Add §3.6 "Build Deck editor (phases 3-4)" with the P checklist as the owner's PC checks (the :333-352 style).
- **Spec corrections** (`docs/superpowers/specs/2026-11-25-dungeon-run-build-deck-design.md`):
  - :124-125: the band is 900000300..900000399 (dec:4);
  - :125-126: the header goes into `NetCacheDecks` through native `AddDeck` (A4); the hero is the named hero with the class-hero fallback (dec:2);
  - :144: removal by `CollectionManager.RemoveDeck` at the HUB, not native delete through `preparedDeletes`;
  - :144-145: native `CollectionManagerDisplay.Exit` after `Navigation.Clear`, not `Navigate(HUB)`;
  - :148: add "Back on the Power step after an editor Done re-opens the editor with the list" (dec:3).
- **`.superpowers/client-dumps/editor-decisions.md:7`.** Superseded by D3 A1.

---

## O. New `client-bindings.txt` rows

- `client-bindings.txt` is generated: `tool bindings --write` over the compiled plugin (client-bindings.txt:1). Run it after the build; do not hand-edit.
- Sites the scanner cannot resolve go in `client-bindings.extra.txt` as `row <kind> <Type> <member> # why` plus `site …` lines (format at its :1).
- Signatures follow the generated style (:1014, :1129). A by-name lookup prints `*` (:1079). A lookup with `MakeByRefType` arguments printed `*` for `Options.GetBoolImpl` (:2136; NativeUIProbe.cs:176), so expect `*` for A1 and V1.

Expected new rows:

```
M	Assembly-CSharp	DeckRuleset	GetDeckSize	System.Int32(CollectionDeck)
M	Assembly-CSharp	DeckRuleset	GetMinimumAllowedDeckSize	System.Int32(CollectionDeck)
M	Assembly-CSharp	DeckRuleset	GetDeckSizeWhileEditing	System.Int32(CollectionDeck,EntityDef)
M	Assembly-CSharp	DeckRuleset	GetDeckSizeRule	DeckRule_DeckSize(CollectionDeck)
M	Assembly-CSharp	DeckRuleset	IsDeckValid	*            (3-arg, out IList<DeckRuleViolation>)
M	Assembly-CSharp	DeckRuleset	CanAddToDeck	*            (ref RuleInvalidReason and ref List overloads)
C	Assembly-CSharp	DeckRuleViolation	.ctor	System.Void(DeckRule,System.String)
T	Assembly-CSharp	DeckRuleViolation
T	Assembly-CSharp	DeckRule_DeckSize
M	Assembly-CSharp	DeckTrayCardListContent	AddCard	System.Boolean(EntityDef,TAG_PREMIUM,System.Boolean,System.Action`1<System.String>,Actor,System.Boolean,System.Boolean,DeckRule+RuleType[])
M	Assembly-CSharp	CollectionDeckTray	DoneButtonPress	System.Void(UIEvent)
M	Assembly-CSharp	CollectionDeckTray	SaveCurrentDeckAndEnterDeckListMode	*
M	Assembly-CSharp	CollectionManager	RemoveDeck	*
M	Assembly-CSharp	CollectionManager	DoneEditing	*
M	Assembly-CSharp	CollectionManagerDisplay	ShouldShowDeckOptionsMenu	*
M	Assembly-CSharp	CollectionManagerDisplay	CanViewHeroSkins	*
M	Assembly-CSharp	CollectionManagerDisplay	CanViewCardBacks	*
M	Assembly-CSharp	CollectionManagerDisplay	CanViewCoins	*
M	Assembly-CSharp	CollectionManagerDisplay	CanViewPets	*
M	Assembly-CSharp	CollectionPageManager	ShouldShowTab	System.Boolean(BookTab)
M	Assembly-CSharp	CollectionPageManager	HasClassCardsAvailable	*
M	Assembly-CSharp	DeckTrayDeckListContent	RenameCurrentlyEditingDeck	*
M	Assembly-CSharp	Navigation	Clear	System.Void()
M	Assembly-CSharp	GameplayErrorManager	Get	*
M	Assembly-CSharp	GameplayErrorManager	DisplayMessage	*
M	Assembly-CSharp	CollectionDeck	GetClass	TAG_CLASS()
M	Assembly-CSharp	CollectionDeck	GetHeroClasses	System.Collections.Generic.List`1<TAG_CLASS>()
M	Assembly-CSharp	DefLoader	GetEntityDef	(System.String)        (only if not already bound)
F	Assembly-CSharp	CollectionPageManager	m_heroSkinsTab / m_cardBacksTab / m_coinsTab / m_petsTab   (if read by name)
M	Assembly-CSharp	DeckTrayDeckListContent	IsDoneEntering	*      (only if O5 uses it)
contingent: CollectionDeckTray OnCardCountUpdated (S4); DeckRuleset Filter / FilterFailsOnShowInvalidRule (A3)
```

- Already bound, and reused: `CollectionDeck.AddCard` :1014, `GetClasses` :1023, `GetCardsWithCardID` :1022, `MarkNetworkContentsLoaded` :1034, `FillFromTemplateDeck` :1019, `InitializeRuneIndicatorVisual` :1078, `OnBackOutOfDeckContentsImpl` :1079, `IsInEditMode` :1105, `NotifyOfBoxTransitionStart` :1107, `OnDeckCreated` :1110, `Exit` :1125, `ShowDeck` :1127, `GetCopiesInDeck` :1199, `RuleInvalidReason..ctor` :87, `DialogManager.ShowPopup` :1281, `DeckTray.IsShowingDeckContents` :1221.
- If `BindingScan` cannot resolve the reflection in `NativeDungeonRunEditor` (its own `Required` helper), add `row` lines to `client-bindings.extra.txt`, as is done for `CollectionDeck CountCardsByStatus` (client-bindings.extra.txt:20, :25).

---

## P. Open risks that need a PC test

| # | Risk / unknown | Evidence | PC check | If it fails |
|---|---|---|---|---|
| P1 | `(int)SceneMgr.Mode.HUB == 3`: the HUB-origin `Exit` skips the dialog | D2:180-184; critic W4 | log the int once; Done offline shows no reconnect dialog | the K belt already covers it |
| P2 | `ShowDeck(id,false,false,null)` opens edit mode; whether `ShowDeckHelper`'s `StartEditingDeck` call is conditional | D2:295-304 (no listing) | O7's verify logs OK | try `true` for the first bool; dump `CollectionManagerDisplay !ShowDeckHelper` |
| P3 | The "CM ready" moment for O5 | D2:2389 no IL | the open never races (10 tries) | wait more frames / use `IsDoneEntering` |
| P4 | `DecksReady` with the band filter lets CM load | NativeUIProbe.cs:1232 | CM loads with the temp deck | — |
| P5 | The "x/10" label after S1 | D2:2256-2261; sender no IL | label reads `n/10` | enable S4 |
| P6 | Every UI add path reaches A4 (`DeckTrayCardListContent.AddCard`): click, drag, right-click | D2:1765-1779 proven; D2:2972-2982 no IL | each path at 10 cards shows one message | A2/A1 still refuse silently; dump `CollectionCardVisual OnRelease OnDrag` |
| P7 | `GameplayErrorManager` is live in CM | D2:1779-1781 | message visible | AlertPopup + debounce |
| P8 | Page-lock pass uses A1, so the native lock overlay shows our reason | R2-display §2 G-a (likely) | illegal cards greyed after 10 / copy limit | optional `ShowLock` postfix (D2:2950) after a `LockType` dump |
| P9 | Candidates missing from the pages under the native FT_STANDARD `Filter` (format, rotation, playable, cardset) | D2:1388-1392, 1622-1658; NativeDeckEditorSave.cs:9-11 | every owned legal card is findable | enable A3 |
| P10 | The `out`-parameter prefix on `IsDeckValid` patches and runs (by-ref precedent: `LocalSideboardSubsetDeckValid`, NativeDeckEditorSave.cs:18-24, :56-64; the interface-typed `IList<>` out is new) | R2-rules §1.3 | the deck box shows complete at 10 | drop V1 (cosmetic only; I owns Done) |
| P11 | Named-hero client class tags; deck-box portrait; the `HeroPower` override | R2-display §5; NativeDeckEditorSave.cs:146 | per adventure: `GetClass`/`GetHeroClasses` log + portrait | the class-hero fallback (A2); a fixed switch per adventure |
| P12 | Dual: class-B tab and cards | D2:2790-2795, 1570 | MAGE_ROGUE: the Rogue tab is shown and a Rogue card can be added | C1 `GetHeroClasses` prefix; else dual stays on the plank (M) |
| P13 | DK rune ghosting matches `AddReason` | D2:2819-2822 | a 4th-rune card is greyed and refused | cosmetic only |
| P14 | Seeding via `FillFromTemplateDeck` (J2) yields the 10 cards, with premiums and runes | NativeDeckEditorSave.cs:203-213 | Power Back re-opens with the same list | seed through `AddCard` while Creating |
| P15 | `GetCardsWithCardID` counts golden copies per copy; `CanCardBeAddedAsOwned` accepts an owned golden | D1:939-942 | add a golden copy; Done list matches | map by `GetSlots()` counts instead |
| P16 | Escape in CM edit mode reaches `OnBackOutOfDeckContentsImpl` | R2-flow §3.1 | Escape -> Deck step | also hook `OnBackOutOfContainerContents` (D2:2046) |
| P17 | `SaveCurrentDeckAndEnterDeckListMode` then `Exit` on the next tick is safe (no order listing) | D2:2218-2222 | Done/Back leave cleanly, no NRE in the log | `DoneEditing` only |
| P18 | `OnScenePreUnload` / `SendChangesIfPending` saves are swallowed | D2:1757-1760, 1243-1247 | log shows only "swallowed" lines, no "Unknown local deck" | — |
| P19 | `NotifyOfBoxTransitionStart` needed before `Navigate(CM)` | critic (d) | open works as the home button does | — |
| P20 | A profile switch while CM is open in editor mode | critic (d) | switch profile mid-build | the sweep at Bootstrap; Recover |
| P21 | Per-visual cost of A1 with a memoized `cardReason` | DungeonRunBuild.cs:53-57, 81-82 | page turns stay smooth | cache `AddCode` per (slot signature, client id) |
| P22 | A run-legal tourist card hits the tray's own tourist limit outside the ruleset | D2:1777-1781 | add a tourist if one is a candidate | the run admits it or not by data; note it |

---

## Contradictions resolved

| Id | Issue | Resolution | Basis |
|---|---|---|---|
| B1 | how to leave CM | **native `Exit` after `Navigation.Clear`**, with `Navigate(HUB)` only as a fallback; the `ExitAllowed` belt | D2:135-215; R2-flow §6 |
| B2 | where to create the deck | **HUB** | NativeDeckEditor.cs:74-76, 87-91; D2:274-278; dec:5 |
| B3 | Done hook | **`DoneButtonPress` prefix** is primary; `IsDeckValid` (V1) is cosmetic; `OnBackOutOfDeckContentsImpl` is the Back hook | D2:1851-2020, 2046-2049 |
| B4 | dual-class readiness | plank in phase 3; editor in phase 4 with `GetClasses` prefix + `ShouldShowTab` postfix, `GetHeroClasses` contingent (the display way over R2-rules C1) | D2:2790-2795, 1321-1325, 1570 |
| B5 | removal | **`CollectionManager.RemoveDeck` at the HUB**; `preparedDeletes` unused | D2:950-954, 592-607, 2373-2384 |
| B6 | where `IsRunBuildId` lives | **`plugin/DungeonRun/DungeonRunEditor.cs`** (tested); `NativeUIProjection` forwards | Sched9.csproj:14 |
| B7 | NetCache header | **native `AddDeck`/`RemoveDeck` add and remove it**; the plugin only asserts | D2:944-954 |
| R2 | popup site (R2-rules A2 vs R2-display) | **`DeckTrayCardListContent.AddCard`** shows it; `CollectionDeck.AddCard` and `DeckRuleset.CanAddToDeck` refuse silently | D2:1765-1779, 2544 |
| R2 | popup channel | **`GameplayErrorManager.DisplayMessage`** for adds; AlertPopup + debounce for Done and as the fallback | D2:1779-1781; NativeCrafting.cs:698-705 |
| dec:7 | native rule ignore list | **A1 bypass**; no unknown enum names | D2:1288-1296, 1401-1410; R2-rules F8 |
