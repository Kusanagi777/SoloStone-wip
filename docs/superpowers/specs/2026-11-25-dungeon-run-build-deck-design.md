# Dungeon Run: "Build Deck" replaces every Random starting deck

Status: design, approved by the owner 2026-11-25. Date: 2026-11-25. Owner answers to the builder check folded in.

## Intent

On the Dungeon Run deck step, the player can start a run with a deck of their own instead of an authored or random one.
Every "Random" starting deck in Dungeon Run becomes **Build Deck**. Choosing it opens the client's own Collection
Manager deck editor in a Dungeon Run mode. There the player builds a starting deck of exactly 10 cards from their own
collection, then goes on to pick a hero power and the run starts with that deck. The owner's words: players "can use any
card from their collection".

## Decided with the owner

- **Every Random deck is replaced**, everywhere in Dungeon Run:
  - the picker's own "Random <Class> Deck" pouch (`FedDeck.Random`, NativeDungeonRunPicker `EnsureFeed`), which today
    picks one of the row's unlocked decks at Play: classes, Rikkar (TRLA_209h_*) and the Monster Hunt hunters;
  - the data's generated "Random" starter decks of the Heist and Tombs rows (the DALA_80x / ULDA_90x decks with a
    `Generator`, DungeonRun.cs ~392), in the native picker and in the plank menu.
- **Size:** exactly 10 cards, the normal starter size (`rules.starterSize` 10; every authored starter has 10).
- **Card source:** any card from the player's collection that the run may hold (rules below).
- **Built fresh each time, never saved.** Nothing is kept for the next run; leaving the editor discards the list. The
  run's own save keeps the run deck, as it does today for every run.
- **Approach A:** the client's Collection Manager deck editor, not plank screens (approaches below).
- **Return point:** after Done the player picks the hero power and starts the run on **the run menu's Power step**
  (plank screens), hero and 10 cards kept. Back without Done returns to the run menu's Deck step.
- **Always unlocked:** Build Deck is available from the start on every row, including the Heist and Tombs heroes whose
  Random deck needed a `*_win` unlock.
- **Treasures:** a built run counts as **every starting deck of its row** for treasure scope and deck treasure tags (the
  closest match to the old Random, which was one of them). A Monster Hunt built run keeps its hunter's whole pool.
- **Fallback:** where Phase 0 shows the Collection Manager cannot do a part (most likely two-class decks for the Tombs
  dual rows, or the 10-card limit), that part uses approach B's plank deck builder instead; everything else keeps the
  Collection Manager.

## Goals

- Build Deck shows wherever a Random deck showed, as an unlocked option (see "Unlocks" for the Heist/Tombs gate).
- The editor shows the player's collection, only allows legal cards, stops at 10, and Done returns to the hero-power
  choice with the hero kept.
- The run starts with exactly the 10 cards built; everything after the start (drafts, treasures, bosses, saves) is
  unchanged.
- Nothing the player does in this mode creates, changes or deletes one of their own constructed decks.

## Non-goals

- Saving or reusing built decks; deck names, cosmetics (card back, hero skin, coin) for the built deck.
- Changing drafts, treasures, scaling or the boss side.
- Building decks outside Dungeon Run.

## Anti-goals (a failure even if it works)

- A stray deck left in the player's constructed deck list, or a personal deck id spent (Profile.CreateDraft commits to
  disk and burns an id: LocalProfile.cs:1100-1114).
- A card reaching a run match that the run engine cannot play (the match only checks deck size today:
  DungeonRun.cs:846-847, :899).
- The native UI blocked (`Fail`) because the plugin's deck bookkeeping meets a deck it does not know
  (DecksReady/ValidateNative/ReorderLocalDrafts/ExpectedRanks/SaveLocalDraft all throw on an unknown id).
- Copies beyond what the player owns.

## Constraints

- **The plugin cannot be compiled in the cloud container** (no Hearthstone client assemblies). Every client member must
  be read from the owner's client and listed in `plugin/client-bindings.txt` (the launcher test `client bindings` checks
  it against the real client). Several members this design needs are not bound today and their exact signatures are
  unknown (Phase 0).
- **Isolation (R12) changes.** `docs/design/dungeon-run.md` says the run never opens the deck editor or the collection
  (:200, :313). This feature reverses that for the deck-building step only; the match itself stays isolated. The doc is
  updated with the change.
- Verify suites compile `plugin/DungeonRun/*.cs` without the profile or client: new rule code there takes the
  collection as data/delegates, never `LocalProfile`/`LocalCardCatalog` directly.
- Files LF; client tables never committed; the owner tests every client-side step on their PC.

## Approaches considered

| | Approach | Trade-off |
|---|---|---|
| **A (chosen)** | The client's Collection Manager editor in a 10-card Dungeon Run mode | Best look and feel (art, search, mana filters, familiar). Largest client-hooking surface, several unknown client members, a scene round trip out of the borrowed Heist screens and back, dual-class rows unproven; expect fix rounds on the owner's PC. |
| B | The run menu's plank screens (card list, filters, pages) | Low risk, mostly testable here; text only, paging for large collections. |
| C | A guided draft from the collection (pick 1 of 3, ten times) | Not "any card from their collection"; rejected. |

The owner chose A. Design rule for A: keep everything that is not client UI (legality, the 10-card list, the run start)
in pure, tested code, so a client-side failure never produces an illegal run.

## Design

### 1. Card rules (pure, tested: `plugin/DungeonRun/DungeonRunBuild.cs`, new)

A card id `c` may be added to a build for run row `R` when all hold:
- `c` is collectible, not a hero card (CardType.HERO), not a Fabled primary or companion (FabledRules), not a
  sideboard-only card;
- its class is one of `R.ClassSet` or neutral (a dual Tombs row: either class or neutral, any mix);
- the run engine admits it: `DungeonRunValidator.PoolCardReason(c, R.ClassSet, …, catalogRun: true) == null` and the
  card is implemented (the predicate starter validation already uses, DungeonRunValidator.cs:95-96, :166-180, :334);
- rune costs: on a Death Knight row the whole deck needs at most 3 runes (RuneRules); rune-cost cards are not allowed
  on other rows.

The deck: exactly 10 cards; per copy group (`LocalCardCatalog.CopyGroup`: a Core reprint and its original share one
limit) at most 2, 1 for a legendary; per card id never more copies than owned (`LocalProfile.TotalOwnedCount`, golden
and normal counted together). The profile-side facts (owned counts, copy groups) are passed in as data, so verify
suites test the rules without the client.

`DungeonRunBuild.Validate(row, cards, owned, copyGroup) -> reasons` is the single check used by the editor (to refuse
an add and to gate Done) and by the run start (defence in depth).

### 2. The run start (pure, tested)

- `DungeonRun.NewRun` gains an overload taking an explicit starter list. It validates with `DungeonRunBuild` (minus
  ownership, which is the client's) and fails with a reason rather than starting an illegal run.
- `state.StarterDeck` must stay an id the data knows (resume refuses unknown ids: DungeonRun.cs:966-987), and it drives
  treasure scope, tag affinity and Rikkar's board shrine. A built run records **the id of the Random deck it replaced**:
  the generated deck's id on Heist/Tombs rows (unchanged treasure scope), and on rows whose Random was the picker pouch,
  a new data row per class/hero "build" deck (see 4). A new save attribute `BuiltDeck="true"` marks these runs.
- **Unlocks:** the starter-deck unlock check in `NewRun` (DungeonRun.cs:142) does not apply to a built run, so the
  Heist/Tombs generated decks' `*_win` locks never gate Build Deck.
- **Treasures:** for a `BuiltDeck` run, the treasure scope check (DungeonRun.Economy.cs:113, :131) and the deck tag
  affinity (DungeonRun.cs:1205) treat the run as holding every deck of its row (`R.Decks`): a treasure scoped to any of
  them is in scope, and the deck tags are the union. One rule in the scope check, no treasure data edited.
- Save version: the run already stores its full deck list (`DungeonRunState.Deck`); the new attribute is additive and
  guarded like the step-9 attributes. A version bump only if the migration rules require one for a new attribute
  (builder checks DungeonRunMigration).

### 3. The editor mode (client; Phase 0 confirms every member)

- **Temporary deck:** a reserved id band below 910000010 and outside the loaner/test bands (900000010..900000179),
  `IsRunBuildId(id)`. Never a profile draft. Projected like the shop's decks: a `NetCacheDecks` header (NORMAL_DECK,
  FT_STANDARD, the row's hero card), `CollectionManager.OnDeckCreated(header, null)`, `MarkNetworkContentsLoaded`
  (NativeShopProjection.cs:16-33).
- **Bookkeeping:** `DecksReady`, `ValidateNative`, `ReorderLocalDrafts`, `ExpectedRanks/ExpectedHeader`, `DeckSaveAction`
  (new disposition: never persist), `DeleteLocalDraft`, the tray limits and `LocalDeckCopyCount` admit or skip the
  temporary id.
- **Open into card view** for that deck (the native entry Phase 0 identifies: `ShowDeck` or the recipe path's calls,
  NativeRecipePicker.cs:183-205), then the rune indicator and `UpdateFiltersForDeck`.
- **10-card limit:** a hook on the native deck-size rule for the temporary id (Phase 0 finds it: the client already
  varies size per deck, 20/40).
- **Dual-class rows:** `GetClasses`/`HasClass` override for the temporary deck and both classes to the page filters
  (Phase 0 confirms native two-class filtering for a NORMAL_DECK).
- **Illegal cards:** an add of a card the run cannot hold is refused with the reason as a native popup (the copy-rule
  hook pattern); greying them on the collection pages needs an unbound per-card lock hook (Phase 0) and is a later
  step if the owner wants it.
- **Blocked in this mode:** New Deck, deck list tray actions, Complete My Deck/DeckHelper, paste, recipes/templates,
  crafting/disenchant (ownership must not change mid-build), rename, cosmetics trays. The existing
  `OwnsPersonalDeckEditor` blockers are extended to the temporary deck by a sibling predicate.
- **Done:** if `DungeonRunBuild.Validate` passes, the list goes to the run hand-off, the temporary deck is removed
  (header dropped, native delete allowed through `preparedDeletes`), and the editor leaves to the HUB (`Navigate(HUB)`,
  the only offline exit `CollectionExitAllowed` allows). If not, Done is refused with the reasons. Every native
  `SendChanges` source (SaveCurrentDeck, OnScenePreUnload, MarkDeckAsSeen, PocoSetDeckName, ReconcileCardOwnership) for
  the temporary id is swallowed.
- **Back without Done:** discards the list and returns to the run menu's Deck step.
- **Safety:** every hook guarded; any failure removes the temporary deck, clears the session flag on every exit
  (including Fail/Recover) and returns to the run menu with a caption, never a blocked UI.

### 4. Data and the two menus

- Heist/Tombs: the generated "Random" decks stay in the data (they are the StarterDeck id of built runs and the headless
  suites still use them), but every picker shows them as **Build Deck**: name, description, display card. The engine's
  `GenerateStarter` stays for simulations and suites.
- Rows whose Random was the picker pouch (classes, Rikkar, Monster Hunt): one new data deck per row, kind `build`, no
  cards, only as the StarterDeck id of built runs; the validator's deck-count rules (class rows exactly 3 decks,
  DungeonRunValidator.cs:468) count it apart. Rikkar's build deck names the same shrine as the row's default deck.
- Native picker: the Random pouch becomes the Build Deck pouch (same slot, same art, description "Build a 10-card deck
  from your collection"). Choosing it then pressing on: the picker hands off as in 5.
- Plank menu (`NativeDungeonRunUI` Deck step): the Random deck row becomes "Build Deck", same hand-off.

### 5. The round trip (client)

The borrowed picker session ends whenever the scene leaves ADVENTURE (NativeDungeonRunPicker.cs:387), and re-entering
the borrowed playmat at its hero-power step would mean auto-driving the tray and deck steps. So the round trip is
ADVENTURE -> HUB -> COLLECTIONMANAGER (build) -> HUB -> **the run menu's Power step** with row and built list carried
(the `SignatureWhenHome` / `PickerWhenHome` hand-off pattern, its own pending slot, no 180 s lapse while the editor is
open). The run starts from the plank Power step (owner's decision).

### 6. Unlocks, stats

- Build Deck is always unlocked (owner's decision), including on the Heist/Tombs rows whose generated decks carry
  `*_win` unlocks (run-v1.json:66-118); see 2.
- Built runs count toward stats and unlocks like any random-seed run (builder's call, marked: the owner did not ask
  otherwise; a typed-seed-style exclusion is one switch if wanted later).

### 7. Fallback: the plank builder (approach B) where the editor cannot work

If Phase 0 shows a part of A is impossible with the client (two-class filtering for a NORMAL_DECK, a deck-size hook,
refusing a single add), that part uses a plank deck builder in `NativeDungeonRunUI`: legal cards as planks ("Fireball (4)
- 2 owned"), 6 per page, mana-cost and class/neutral filters, add/remove, the picked list in the caption, Done at 10,
then the same Power step. It is driven by the same `DungeonRunBuild` rules. Most likely scope: the Tombs dual rows only.
The decision per part is recorded after Phase 0.

## Phases (each ends with a build on the owner's PC)

0. **Client discovery.** A launcher-test tool mode (`tool members <Type> [method]`, reading the owner's
   `references/game/Assembly-CSharp.dll` with System.Reflection.Metadata, as `tool bindings` does) prints the signatures
   and short IL summaries of: the deck-size rule, `CollectionManagerDisplay.ShowDeck`/edit entry, `OnDeckCreated`,
   `GetClasses/HasClass`, page filter/lock members, `CollectionDeckTray` Done/Back. The owner runs it and pastes the
   output; the design's client steps are then fixed to real members.
1. **Pure core.** `DungeonRunBuild` rules, `NewRun` with a list, `BuiltDeck` save attribute, data build decks,
   validator changes, verify-suite checks (new section in a step-9 suite).
2. **Menus.** Build Deck in the picker feed and the plank Deck step; the hand-off to the editor and back to the plank
   Power step (with a stub "build" that is a fixed legal list, so the round trip is testable before the editor exists).
3. **Editor mode.** Temporary deck, bookkeeping, 10-card limit, blocks, Done/Back.
4. **Dual-class and Death Knight rows; illegal-card popups; doc updates (R12, dungeon-run.md, tests.md).**

## Builder's calls (marked for the owner to check)

- Card rules: no hero cards, no Fabled primaries/companions, no sideboard-only cards; rune-cost cards only on Death
  Knight rows (deck at most 3 runes); copy limits by copy group; golden and normal copies counted together.
- Built runs count toward stats and unlocks.
- Rikkar's build deck names the same board shrine as the row's default deck.
- Illegal cards: refused on add with a reason; greying them on the collection pages only if Phase 0 finds a hook.
- Blocked in the editor mode: New Deck, deck-list actions, Complete My Deck/DeckHelper, paste, recipes, crafting and
  disenchanting, rename, cosmetics.
- The generated Heist/Tombs Random decks stay in the data (built runs' StarterDeck id; headless suites), shown as Build
  Deck.

## Left to the builder

Exact ids for the temporary band and the build data rows; class layout of `DungeonRunBuild`; log lines; which verify
suite hosts the new checks; the wording of captions and refusals.
