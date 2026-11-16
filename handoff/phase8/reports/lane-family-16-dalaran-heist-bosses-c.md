FAMILY 16 (Dalaran Heist bosses C) REPORT. Worktree /home/user/wt/f16, branch wip/f16, one commit: a074315 "feat(dungeon-run): family 16 Dalaran Heist bosses C (DALA_BOSS_31-42, 44, 45)". Nothing was pushed. Everything is committed. No process of mine is still running.

## FLAGS FOR THE INTEGRATOR (read first)

1. **Shared engine-side plugin fix: `plugin/FairBotState.cs`, 3 lines, null-guards only.**
   - The defect: a repeated Discover (DiscoverTask `repeat: 2`, for example Griftah TRL_096, which is in Timothy Jones's authentic deck) queues its second Choice with `Choices == null` until the first is answered. `FairBotState.CreateCore` read `next.Choices` at line 825, and `kept.UnionWith(choice.Choices)` / the remap loop near lines 1189 and 1226 read it the same way. When the boss built its fair view in the middle of that chain, this threw ArgumentNullException, which showed up as a bosspilot fallback.
   - It only changes paths that used to throw. Every baseline has 0 fallbacks, so no recorded result can move.
   - Regression check: c-enc-16 A8. I confirmed A8 FAILS when built against the original FairBotState (in a scratch copy).
   - It is a latent defect for any repeated-Discover card met by any boss.
2. **Shared plugin edit: `plugin/DungeonRun/DungeonRunAdmission.cs`.** I removed LOOTA_811, LOOTA_837, LOOTA_841, LOOTA_842 and LOOTA_842a from the core `run-treasure` row, which now holds LOOTA_814 and LOOTA_840. The five ids moved to my row `run-treasure-boss-shuffle`, which has the same legacy verdict (Player only) and on catalog runs admits Player|Boss. This follows family 09's pattern.
3. **Shared suites I re-pinned:** c-admission, c-catalog, c-content, c-deckops, c-enc-model, c-enc-09 and c-treasures-F2 (all listed under "Pins moved" below). c-enc-09 and c-treasures-F2 were not in the lane list. I edited them because the treasure move breaks their pins, and I ran both twice.
4. **No new private tag.** The reserved tag 32651 is unused: Golden is the client's PREMIUM tag and Ray of Suffering's count is the public TAG_SCRIPT_DATA_NUM_1 on the power. c-enc-16 P13 asserts this.
5. **Nothing forbidden was touched:** schedule defaults, finales, acts, pool sets, save version, ReleasedShapes and golden sequences are all unchanged.
6. **The passive registration order now ends with family 16's six passives.** After the merge with families 14 and 15, the order must be recomputed in c-content and c-enc-model J13.
7. **Ledger observation, not fixed (OwnDeckLedger).** When cards shuffled in with a reveal (Tales of Fortune treasures) are later overdrawn (burned with a full hand), the boss's own-deck sample lost some known treasures: the boss knew fewer cards than its deck held. This only under-informs the boss; it never leaks anything. With no burns the sample is exact (c-enc-16 G2, G4 and L5). It comes from the existing tentative-arrival/burn logic in OwnDeckLedger, so I left it alone.

## Per id

All 30 ids are done; none is rejected.

**Heroes:** DALA_BOSS_31h, 32h, 33h, 34h, 35h, 36h, 37h, 38h, 39h, 40h, 41h, 42h, 44h and 45h are rows only; boss heroes need no script.

| Id | Power | What it does |
|---|---|---|
| 31p | Bling it On! (2) | Any minion. Makes it Golden (sets the client PREMIUM tag); if it is already Golden, it gets Blinged Out! 31e, +4/+4. |
| 32p | Makeover (1) | Auto-cast, as the wiki marks it (family 09's AutoCast). At the start of her turn, with 1 Mana and a friendly minion, a random friendly minion gets one haircut from 32e1-e4. |
| 33p | Made of Coins (passive) | A Coin per hit to her hero. A hit Armor absorbs counts, per the wiki's Armor rule. |
| 34p | Merry Go Round (passive) | At the end of the boss's turn: new `EncounterRules.Rotate` in the new own file `Actions/EncounterRules.F16.cs` (details below). |
| 35p | Healing Hands (passive) | At the start of her turn, 2 Health to each friendly minion. |
| 36p | Import "Pet" (2) | Build-A-Beast's craft (wiki: "functionally identical"): two Discovers over the engine's pinned Build-A-Beast part pools, filtered by the boss's generation filter. The composed minion is named "Exotic Pet". |
| 37p | Tales of Fortune (2) | Shuffles one of 20 old Treasures into his deck, revealed so his own-deck ledger knows it (details below). |
| 38p | Portal Party (2) | Adds one of the wiki's seven Portals to hand. |
| 39p | For The Light! (2) | Auto-cast. Every minion in hand gets 39e, +1/+1. |
| 40p | Immolation Aura (passive) | After an enemy minion attacks her hero, it takes 2 damage. |
| 41p | Summon Companion (4) | Summons Huffer, Leokk or Misha. |
| 42p | Pogoshuffle (2) | Any minion. Shuffles three printed copies into his deck, revealed. |
| 44p | Dark Tidings (1) | Picks a random deck minion, reveals it (RevealCardBlock), gives it 44e +2/+2 and moves it to the top in place (details below). |
| 45p | Ray of Suffering (3) | Deals TAG_SCRIPT_DATA_NUM_1 damage (1, 2, 3 ...). |

- **Merry Go Round (34p):** minions move counter-clockwise as the run player sees the board. The client text gives no direction; I followed K'zrath's "counter-clockwise" and recorded it as an adaptation. The two minions that cross change controller the way ControlTask does. Untouchable entities and shrines (ShrineRules.BlocksControl, awake or dormant) never move. `Rotate` returns the minions that crossed, so family 24's K'zrath can reuse it.
- **Tales of Fortune (37p):** the wiki lists 22 treasures. I left out two because they read or rewrite the run player's hidden zones: Gloves of Mugging (LOOTA_834) and Aleatoric Cube (LOOTA_820).
- **Dark Tidings (44p):** the move to the top is an in-place reorder, so no zone-change fact is recorded. It records `EncounterRules.DeckTopKnown`.

**Cards:**
- DALA_BOSS_36t, Exotic Pet: Token. The crafted pet takes its name.
- DALA_BOSS_38t, PARTY PORTAL!: BossCard. It summons one of the wiki's five Partygoers (CFM_659, BRM_019, KARA_13_22, GIL_805, DAL_721); the client text names none, so the list is from the wiki.
- Also registered: the enchantments 31e, 32e1-e4, 39e and 44e, and KARA_13_22 Mime. Mime is a Token; PARTY PORTAL! makes it enter play silenced.

**Tiers** use family 14's rule, plus "a chapter's final boss is level 8":

| Tier | Rows |
|---|---|
| 1 | Kizi |
| 2 | Timothy, Gold Elemental, Carousel, Zoie, Tipsi |
| 3 | Draemus, Toomba, Aki, Oxana, Ungan, P.O.G.O., Erekem |
| 4 | Moragg (level 8) |

**Decks:**
- 7 are the wiki's lists unchanged.
- Timothy's 25-card list and the pool rows are extended to 30.
- P.O.G.O. is substituted: Fel Reaver became Fel Cannon, as family 14 did.
- Left out: Elite Tauren Chieftain (unscripted), Prince Malchezaar (not admitted), Gloom Stag (not admitted).
- The Carousel's "Hyena" is EX1_534t.
- Wiki pages were read 2026-11-14; each row records its URL. The raw pages are in `verify/.out/tmp/wiki16`.

## Files changed

**Own files:**
- `engine/SabberStoneCore/src/CardSets/DungeonRun/DungeonRunEncounters.F16.cs`
- `engine/SabberStoneCore/src/Actions/EncounterRules.F16.cs` (new)
- `plugin/DungeonRun/DungeonRunAdmission.F16.cs` (8 rows: heist-c-board-power, heist-c-auto-cast, heist-c-passive, heist-c-craft, heist-c-own-deck-power, heist-c-own-hand-power, heist-c-boss-card, run-treasure-boss-shuffle)
- `data/dungeon-run/reference/encounters/F16.json`
- `verify/suites/p8f16/c-enc-16/` (Enc16.csproj, Program.cs)
- `verify/fixtures/p8f16/fixtures/encounters-f16.json` (3 fixture rows: fix16_tipsi, fix16_toomba, fix16_quel)
- `verify/tools/gen/p8f16/make_f16.py`, `make_fixture.py`

**Shared files:**
- `plugin/FairBotState.cs` (flag 1)
- `plugin/DungeonRun/DungeonRunAdmission.cs` (flag 2)
- `tools/restoration-ledger/curation.json`: landedFamilies E16 appended after E10, plus the two tool-written lists.
- `verify/lib/suites.sh`: c-enc-16 appended to BUILD_LIST and RUN_LIST.
- `verify/bin/bp.sh`: CARDS16 variable, a usage line, and the enc16 / enc16-seeds / enc16-carousel / enc16-cards case lines.
- Pinned suites: see below.
- Generated, by gen.sh: encounters-v1.json, restoration-ledger.csv/.md.

## Pins moved (suite: old -> new, cause)

- **c-admission:** total 3167 -> 3238, NOTE generated lists 426 -> 443 (new F16 rows). Family names were added to the exemption list. The catalog-lift check now expects 6 rows, including run-treasure-boss-shuffle with 5 ids.
- **c-catalog ledger golden:** 4704 / 2813 / 295 / 538 -> 4738 / 2813 / 345 / 522.
  - +34 rows: 14 run:boss rows and 20 deck cards.
  - 16 rows went missing -> partial: 14 powers, 38t and 36t.
- **c-deckops T5:** 4704 -> 4738 rows.
- **c-content:** passive list gained 32p, 33p, 34p, 35p, 39p and 40p. Still 904 checks.
- **c-enc-model** (still 141 checks):
  - A4 family counts now include `16:14`.
  - E3 counts runs meeting any Heist row: 1000 of 1000 (Whirt alone: 150).
  - J13 passive list gained the six powers.
  - Log differences vs baseline: G1's rolled anomaly pair (DALA_858, ULDA_710) moved, because the new boss rows change the incompatibility pre-roll.
  - The F2 length lines (14306 -> 13954 and 26370 -> 25744) are Linux LF vs the Windows-recorded CRLF baseline: 352 and 626 lines. This is not caused by me.
- **c-enc-09 L0:** the example of a core-row treasure is now LOOTA_814 (was LOOTA_811).
- **c-treasures-F2 T2:** the five moved treasures are now boss-admitted on catalog runs.

**Generator lines:**
- encounters: "83 rows, 87 decks (50 authentic, 37 changed), 201 adaptations, 1 rejected"
- catalog: "506 entries (452 offerable pool, 2 withdrawn, 52 fixed), 6508 admitted and 1 excluded" (unchanged)
- ledger: rows 4738, implemented 2813, partial 345, missing 522, duplicate/support 1058, adapted 0

## Tests added

**c-enc-16: RESULT 122 passed, 0 failed**, real engine games through BuildPlan -> DungeonRunSetup. Sections:

| Section | Boss or area | Notable checks |
|---|---|---|
| A | Timothy | Golden seen in the boss's fair view; A8 Griftah fair-view regression |
| B | Kizi | auto-cast, all four haircuts |
| C | Gold Elemental | — |
| D | Carousel, 14 checks | directions, empty rows, full 7+7 rows, enchantments kept, control facts, awake and dormant shrine stay, clockwise helper |
| E | Zoie | — |
| F | Draemus | pools filtered, Discover chain, Exotic Pet stats |
| G | Toomba | own-deck ledger equals the deck; 20 treasures |
| H | Tipsi | 7 Portals, 5 Partygoers, Mime silenced |
| I | Aki | — |
| J | Oxana | — |
| K | Ungan | — |
| L | P.O.G.O. | ledger knows the copies |
| M | Erekem | no residence fact, TopKnown fact, ledger unchanged, next draw |
| N | Moragg | — |
| T | the 5 treasures played by a boss | — |
| P | rows, admission, validator | — |

**Bosspilot modes** (each call runs twice; all twice identical, 0 faults, 0 fallbacks, 0 failed checks, 0 swallowed, stderr 0 bytes, no recorded baseline):

| Mode | Games | Boss wins | Power-unused | Watched cards played |
|---|---|---|---|---|
| enc16 (FAMILY=16) | 28 | 16 | 0 | 38t x2, 841 x1 |
| enc16-seeds (19000) | 28 | 15 | 1 (Timothy) | 38t x3 |
| enc16-carousel | 8 | 8 | 0 | — |
| enc16-cards (3 fixture rows) | 18 | 14 | 0 | 38t x24, 811 x4, 837 x14, 841 x13, 842 x6: all 5 played |

Timothy's power-unused in enc16-seeds is explained: Bling it On!'s first use only sets the Golden tag, which the AI's evaluation does not value. He used it in enc16.

## Lane verification

- Engine build: 0 warnings, 0 errors (md5 064a8413c34caedd87f8d9cc06f65af6).
- gen.sh ran twice: all six generated files byte-identical, git status empty. make_f16.py and make_fixture.py reproduce their files byte for byte.
- Each suite ran twice; run 1 = run 2 with 0 differing lines for all of them:

| Suite | Result |
|---|---|
| c-content | 904 |
| c-admission | 3238 |
| c-runlayer | 244 |
| c-acts | 181 |
| c-deckops | 174 |
| c-catalog | 127 |
| c-shrine-core | 131 |
| c-enc-model | 141 |
| c-enc-engine | 122 |
| c-enc-16 | 122 |
| c-treasures-F2 | 321 |
| c-enc-09 | 166 |

- Differences vs the wave-3 baseline: only c-admission (4 lines) and c-enc-model (12 lines), as explained under "Pins moved".

## Paragraph for docs/design/dungeon-run.md

"Family 16 (Dalaran Heist bosses C): fourteen Heist encounters, tiers by family 14's level rule (a chapter's final boss is level 8: Moragg, tier 4). New engine rule: the board rotation (Actions/EncounterRules.F16.cs, `EncounterRules.Rotate(bottom, clockwise, source)`): the two rows are one ring; one step moves one minion across each way as a control change, rows keep their size (an empty row gains the crossing minion), untouchable entities and run shrines never move. The Carousel Gryphon rotates counter-clockwise as the run player sees it at the end of its turn; family 24's K'zrath reuses it. Kizi's Makeover and Aki's For The Light! are auto-cast (the wiki) through family 09's AutoCast. Bling it On! uses the client's Golden (PREMIUM) tag. Import \"Pet\" is Build-A-Beast's craft through the boss's generation filter, the pet named Exotic Pet. Tales of Fortune shuffles one of twenty Kobolds treasures into Ol' Toomba's deck, shown (Gloves of Mugging and Aleatoric Cube left out: hidden zones); five more treasures (Orb of Destruction, Horn of Cenarius, Portable Forge, Quel'Delar and its Blade) are admitted for a boss on catalog runs. Pogoshuffle's copies are shown as shuffled; Dark Tidings reveals a deck minion and puts it on top in place, recorded as DeckTopKnown. PARTY PORTAL!'s Partygoers are the wiki's five (Mime enters silenced). FairBotState now tolerates a queued Discover choice that has no offers yet (Griftah)."

## To redo after the merge

- Recompute these pins:
  - c-admission totals and the family-name exemptions (families 14 and 15 add theirs).
  - c-catalog / c-deckops ledger counts.
  - c-content and c-enc-model J13 passive lists, in registration order.
  - c-enc-model A4 counts and the E3 wording.
- Resolve the textual conflicts in suites.sh, bp.sh and curation.json landedFamilies.
- Rerun gen.sh. Then rebuild every family fixture, mine with `python3 -I verify/tools/gen/p8f16/make_fixture.py`. Other families' fixtures built from encounters-v1.json (for example c-collectibles-C's) go stale whenever rows are added.
- Rerun the enc16 modes: seeds depend on row indices.
- Nothing was checked in the real client. This includes how the native client shows PREMIUM and the renamed Zombeast-style pet.
