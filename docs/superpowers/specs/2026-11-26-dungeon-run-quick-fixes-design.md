# Dungeon Run quick fixes: design

Approved by the owner on 2026-11-26 ("yes proceed, Rafaam should never speak"). This is the first of three projects
from the owner's 2026-11-26 test report. The other two are the anomaly list and a content pass (hero powers, decks,
archetypes); each gets its own design.

## Intent

The run setup should feel native and quick, and all heroes should start the same way. Four fixes:

1. **Editor lock banner.** In the Build Deck editor, a full 10-card deck locks every other card. The owner called it
   "not critical".
2. **The Rafaam line.** The borrowed Dalaran Heist deck screen plays its intro line, about 15 s, on every run. It
   should never play, not even the first time.
3. **Native hero power with Build Deck.** After Build Deck the hero power is picked on the plank fallback page. It
   should be picked on the client's own playmat, as it is for premade decks.
4. **No Tombs starting treasure.** The four Tombs explorers pick a signature treasure before the run starts. Remove
   the step, so every hero starts the same way.

## Goals, non-goals, anti-goals

- **Goals:** the four fixes above, each covered by a Linux test where one is possible and by one PC check.
- **Non-goals:**
  - other borrowed-screen lines, unless the same fix silences them for free;
  - the anomaly list, the anomaly UI, hero powers, decks and archetypes (later projects);
  - giving every hero a starting treasure.
- **Anti-goals:**
  - a stalled playmat (for example, a skipped line whose caller waits for it to finish);
  - a run that starts without a hero power;
  - any change to the player's real collection or save data.

## Current state (recon, 2026-11-26)

- **Lock banner.** `NativeDungeonRunEditor.RunBuildCanAdd`/`RunBuildCanAddList` (the A1 hooks) refuse every add at
  10/10 with code `size`, because `DungeonRunEditor.AddCode` checks fullness before anything else
  (`plugin/DungeonRun/DungeonRunEditor.cs:190-192`; `DungeonRunBuild.AddReason` line 80). The page's lock pass reads
  that verdict, so every card shows a lock. Test BD29 pins the code.
- **Rafaam.**
  - The picker always borrows Dalaran Heist (`NativeDungeonRunPicker.cs:30-33`).
  - The plugin answers the borrowed save keys empty, and erases the in-memory "has seen" marks on every session
    (`ClearBorrowedSaves`, `:489-498`).
  - Nothing in the plugin touches VO or quotes.
  - The line's gate is unknown: a save flag, a per-launch list, or none.
- **Hero power.**
  - On the native path the playmat order is "deck, then power" (`NextLoadout`, `:655-739`), and the power is already
    native.
  - The Build Deck pouch hands off at the deck step, so after the editor's Done the menu opens the plank Power step
    (`NativeDungeonRunUI.cs:169-180`).
- **Tombs.**
  - The four explorer rows in `data/dungeon-run/run-v1.json` (lines 33-36) have `signatures` and
    `signatureOffer: 3`.
  - `DungeonRun.SignatureOffer` (`:253`) and `NewRun(signatureId)` (`:138`) apply the signature.
  - Play hands off to the plank "Choose a signature treasure" page (`SignatureWhenHome`).
  - Signature treasures are `offered: false`, so they appear nowhere else.

## Approaches

- **Lock banner:**
  - **A (chosen):** the A1 lock verdict ignores fullness, so a card locks only for its own reason (copy limit,
    legendary, class, ownership), as a full native deck does. Adds at 10/10 stay refused by A2, A4 and the run-start
    check, with "the deck is full".
  - B: return a reason the page ignores; this needs a PC test to know whether it works.
  - C: a `ShowLock` patch, kept only as a fallback.
- **Rafaam:**
  - **B (chosen):** skip the line's quote or VO call while a picker session is in the borrowed adventure. It is the
    only option that also covers the first run.
    - It needs a member dump (`installer/quick-fixes-members.txt`).
    - A skipped call must not stall the deck coroutine; a stall falls back through the picker's Abort.
  - A: persist the "has seen" flag. Rejected: the line would still play once.
  - A2: pre-seed the flag from the owner's log. This is used if the dump shows the line is gated by a known subkey.
    It is the smallest change that also covers the first run.
  - C: skippable by click. Rejected: the owner wants the line gone.
- **Hero power:**
  - **B (chosen):** retail order on the playmat, power then deck, so the Build Deck pouch already carries the power.
    After the editor's Done the run goes straight to Confirm. Mostly removals in `NextLoadout`, `PreviousLoadout` and
    `KeepPowerOptions`.
  - A: re-enter the playmat for the power only. Rejected: it costs an extra scene load and more code.
- **Tombs:**
  - **(chosen):** the explorer rows offer no signature (no offer, no step, no plank page).
  - Signature treasures then appear nowhere, and they stay in the data, inactive.
  - Test records and digests that pin Tombs runs are re-recorded on purpose, and the records say so.

## Decided by the owner

- Rafaam never speaks, not even on a first run.
- The hero power is picked natively before Build Deck.
- Tombs heroes start with no starting treasure, like every other hero.
- At 10/10 the banner is fixed as in approach A. This was proposed and approved with the rest.

## Left to the builder

- The exact client members for the line, from the dump.
- Where Back in the editor lands under the new order: the plank Deck step as today, unless the dump makes a playmat
  re-entry cheap.
- How to switch off signatures:
  - a validator-checked data flag; or
  - removing `signatureOffer`, with `SignatureOffer` returning empty.
- Which test records move, named in the plan, with the reason in each record.
