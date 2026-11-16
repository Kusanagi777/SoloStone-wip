## Family 17 (Dalaran Heist D): all 40 ids done, none rejected

Branch `wip/f17` in worktree `/home/user/wt/f17`, one commit `97d40ac` on base a95b97e. The tree is clean and nothing was pushed. Everything in the lane verification passed, twice with identical logs.

### FLAGS for the integrator
1. **Shared engine edit, one constant:** `engine/SabberStoneCore/src/Actions/EncounterRules.cs` gets `ExtortionEscrow = (GameTag)32652` (my reserved tag) in the tag table, and the "taken" comment now says "32652 by family 17". No other tag was taken.
2. **Shared harness, used but not edited:** the enc17 modes run on family 09's `c-bosspilot-enc09`, because it reports rule-cast powers (`AUTO=`, "auto N", "auto-unused").
3. **Four powers are cast by rule, never chosen by the boss's AI** (family 09's `AutoCast`):
   - Sand Breath (50p) and Raise the Alarm (55p): the wiki marks them auto-cast.
   - Bribery (56p) and Blackmail (59p): the wiki does not, but their value lies in the run player's hidden deck and hand, which the boss's fair view masks. The AI would never pick them. Recorded as adaptations under family 09's rule for such powers.
4. **Two judgement calls (wiki pages have no rules text for these):**
   - Void Shift summons new copies of the banished minions for whoever casts it.
   - Touching wires deal 5 damage once per pair, not 5 per wire. The client's single Wire Watcher player enchantment supports this.
5. **Tiers:**
   - A chapter's final boss is level 8, so tier 4, as family 15 did for Jepetto: seven rows.
   - A 25-card "Possible cards" pool is level 4, so tier 2: Deryl, Zibb.
   - A 30-card counted list with no stated level is level 6, so tier 3: Ichoron, Lavanthor, Nozari, Millificent, Sinclari.
6. **Infuriated Banker (DALA_502) is now reachable:** it replaces Gallywix's two unscripted Fel Reavers. The integrator could rerun the encounters step after merging family 16, whose Gold Elemental Heroic pool lists it.
7. **Not touched:** schedule defaults, finales, acts, pool sets, save version, `ReleasedShapes`, golden sequences.

### Per id (all done)
- **Heroes 46h–59h:** rows only, no script needed.
- **46p Protective Bubble:** gives Divine Shield; offered only on a friendly minion that has none.
- **47p Lava Belch / 47t Molten Rock:** summons the Rock; at the end of its player's turn it deals 1 to all characters.
- **48p To The Void:** banishes a minion. It leaves play with no death and no Deathrattle, held by its Voided enchantment (48e) in family 10's set-aside store (`EncounterRules.Banish`). A run shrine is never a target.
- **48t Void Shift:** destroys all minions, then summons new copies of the Void's minions for the caster, in banishing order, up to a full board, and empties the Void. The run player starts with two in hand (start block, as the wiki says for Normal mode).
- **49p Uncontrollable Energy:** passive; after the boss casts a spell, 1 damage to all enemies.
- **50p Sand Breath:** cast by rule; 2 damage to a random enemy.
- **51p Tinker / 51t Elementium Squirrel Bomb / 51t2 Thorium Chicken:** as their texts.
- **52p Hold the Gates! (52e), DALA_503 Kirin Tor Guard:** +1 Attack per other friendly Guard.
- **53p Boogie Woogie (53e):** Ancestral Spirit's rule.
- **54p Spell Lock:** both players. A played spell Overloads its player for the cost it was played for (`TAG_LAST_KNOWN_COST_IN_HAND`), through the engine's Overload event. The engine's Tunnel Trogg only counts card-printed Overload, so it does not react; Lesser Sapphire Spellstone does.
- **55p Raise the Alarm:** cast by rule.
- **56p Bribery:** cast by rule; uses Tad's Pole's `PoleCatch` (a public summon). Bribed (56e) returns a new copy to the opponent's hand when the minion dies.
- **56t Rival Accomplice:** a Heroic-only card; one copy is shuffled into Wagtoggle's deck by the start block so it is met in this mode.
- **57p Greed is Good:** passive. The first card the run player plays each turn: the boss gets a copy, the run player gets a Gallywix's Coin. The Coin itself buys nothing.
- **58p Blast Powder (58e):** the minion is destroyed when it takes damage. Divine Shield blocks it; a coated shrine goes dormant.
- **58t / 58t2 The Wire / The Other Wire:** start on the run player's board with a Murloc Tinyfin (LOEA10_3) between them; 5 damage at the start of that player's turn while they touch.
- **59p Blackmail:** cast by rule. A random card of the run player's hand goes into hand escrow; an Extortion holds it through tag 32652. The boss's fair view masks the taken card (tested).
- **59t3 Extortion:** returns that same card, buffs kept.
- **59t Lotus Bruiser:** +2/+2 whenever either player plays an Extortion.
- **59t2 Fruit Slasher:** 4 damage to an enemy; with Combo, also 1 to every other enemy.
- **DALA_502 Infuriated Banker:** +1/+1 whenever either player plays a Coin (The Coin, Counterfeit Coin, Gallywix's Coin).

### Decks (wiki, read 2026-11-14)
- 10 authentic.
- Lavanthor substituted: Xavius is not in the client data; filled from his Heroic list.
- Gallywix substituted: two Fel Reavers replaced by two Infuriated Bankers.
- Deryl and Zibb: pools extended to 30.

### Files changed
**Own files:**
- `engine/SabberStoneCore/src/CardSets/DungeonRun/DungeonRunEncounters.F17.cs`
- `engine/SabberStoneCore/src/Actions/EncounterRules.F17.cs` (new)
- `plugin/DungeonRun/DungeonRunAdmission.F17.cs` — six families `heist-d-*`, all distinct from family 14's names
- `data/dungeon-run/reference/encounters/F17.json`
- `verify/suites/p8f17/c-enc-17/` (Enc17.csproj, Program.cs)
- `verify/fixtures/p8f17/fixtures/encounters-f17.json`
- `verify/tools/gen/p8f17/make_f17.py` (wiki tables → F17.json, reads pages from `verify/.out/tmp/wiki/bNN.txt`)
- `verify/tools/gen/p8f17/make_fixture.py`

**Shared files:**
- `EncounterRules.cs`: the tag constant (flag 1).
- `tools/restoration-ledger/curation.json`: landedFamilies E17 appended after E10. The tool-owned lists were regenerated by the tool.
- `verify/lib/suites.sh`: `p8f17/c-enc-17` appended to `BUILD_LIST` and `RUN_LIST`.
- `verify/bin/bp.sh`: usage lines, `CARDS17` / `AUTO17`, and case lines `enc17`, `enc17-seeds`, `enc17-void`, `enc17-cards` (one run per call, k = 1 or 2), plus `enc17-check`.
- Pins only: c-admission (exemption statement), c-catalog, c-content, c-deckops, c-enc-model (see below).

**Generated files** (regenerated): `encounters-v1.json`, `restoration-ledger.csv/.md`, `curation.json` lists.

### Pins moved (suite, old → new, cause)
- **c-catalog ledger golden:** 4704 / 295 partial / 538 missing → 4733 / 350 / 512.
  - +29 rows, all partial: 14 run:boss rows and 15 new deck cards.
  - 26 rows go from missing to partial: 14 powers and 12 boss cards.
- **c-deckops T5:** 4704 → 4733 rows.
- **c-content:** passive registry list gains 49p, 50p, 54p, 55p, 56p, 57p, 59p; still 904.
- **c-enc-model:**
  - A4 family counts gain `17:14`.
  - J13 passive list gains the same seven.
  - E3 rewritten (same change family 14 made): Whirt is no longer every run's Heist row. Whirt is met in 172 of 1000 runs; a Heist row in 1000 of 1000. Hagatha is the act II major in 563 of 1000 (was 506). Still 141.
- **c-admission:** total 3167 → 3259; generated lists 426 → 452.
- **Not run here, expected to move:** randplay regkeys, by the 33 new registrations.

### Generators (twice, byte-identical)
- Encounters: "83 rows, 87 decks (53 authentic, 34 changed), 200 adaptations, 1 rejected".
- Catalog: unchanged (506 entries, 6508 admitted / 1 excluded).
- Ledger: rows 4733, implemented 2813, partial 350, missing 512, duplicate/support 1058, adapted 0.

### Tests
- **c-enc-17:** 119 checks, all passing. Sections A–O cover each boss, the shrine cases, game clones and the fair view (C4b, K5, N2, N6). Section P covers rows, admission and the validator.
- **Bosspilot** (each run twice and identical; 0 faults, fallbacks, failed checks and swallowed leaf exceptions; stderr empty in all four modes):

| mode | games / boss wins | power-unused | auto-unused | boss cards played from hand |
|---|---|---|---|---|
| enc17 | 28 / 18 | 1 (Zuramat) | 0 | 48t×4, 51t2×3, 503×5, 56t×1, 59t×1, 59t2×0, 502×2 |
| enc17-seeds | 28 / 22 | 2 (Zuramat, Deryl) | 0 | — |
| enc17-void (Zuramat ×6) | 6 / 6 | 0 | — | Void Shift ×9 |
| enc17-cards (fixture rows) | 20 / 9 | 0 | — | all 7 boss cards played |

- **The two power-unused are AI choices.** In a controlled fair-view test the AI did use To The Void. Zuramat uses it across six games (enc17-void), and Deryl uses his power in enc17.

### Lane verification
- Engine build: 0 warnings / 0 errors (md5 a7aece7be33e5e429c85ea53e43ee1c0).
- Suites, run 1 = run 2 for every one:
  - c-content 904, c-admission 3259, c-runlayer 244, c-acts 181, c-deckops 174, c-catalog 127
  - c-shrine-core 131, c-enc-model 141, c-enc-engine 122, c-enc-17 119
- Against the wave-3 logs, 0 differing lines except c-admission (4: its result lines) and c-enc-model (12: the changed check text).
- Bosspilot as in the table above.

### Paragraph for docs/design/dungeon-run.md
"Family 17 (Dalaran Heist bosses D): fourteen encounters, DALA_BOSS_46-59. The boss pages have no level table and the hero cards print 30 Health. A chapter's final boss is level 8, so tier 4; a 25-card pool is level 4, tier 2; an unstated 30-card list is level 6, tier 3. Zuramat's To The Void banishes a minion into the Void, built on family 10's set-aside store and held by its Voided enchantment; there is no death, and a run shrine never enters it. Void Shift destroys all minions and summons new copies of the Void for its caster; the run player starts with two. Boommaster Flark: the run player starts with The Wire, a Murloc Tinyfin and The Other Wire; touching wires deal 5 damage once per pair at the start of that player's turn. Madam Goya's Blackmail puts a run player's card in hand escrow, linked by the private tag ExtortionEscrow 32652; the boss never sees it, and Extortion returns the card itself. Queen Wagtoggle's Bribery summons a minion from the run player's deck (Tad's Pole's rule) that returns as a copy when it dies. Spell Lock Overloads both players for the cost each spell was played for. Greed is Good buys the first card the run player plays each turn for a Gallywix's Coin. Sand Breath and Raise the Alarm (wiki auto-cast) and Bribery and Blackmail (value hidden from the boss's fair view) are cast by rule. Infuriated Banker replaces Gallywix's unscripted Fel Reavers."

### For the integrator after the merge
- Resolve the textual conflicts: `bp.sh`, `suites.sh`, the curation landedFamilies list, and the pin edits in c-catalog, c-deckops, c-content and c-enc-model (E3 will clash with family 14's version).
- Regenerate, then rerun `make_fixture.py` for p8f17 (the fixture embeds the generated `encounters-v1.json`), then recompute the pins.
- The enc17 bosspilot lines will move once other families' rows are merged, because seeds depend on each boss's row index.


---

# Family 17 review: confirmed findings (adversarially verified)

## F17.1 [medium] engine/SabberStoneCore/src/Actions/EncounterRules.F17.cs:155
Spell Lock's Overload never reaches Tunnel Trogg, and Zibb's own deck is built around that card. OverloadFor only raises the generic OverloadEvent, but the engine's Tunnel Trogg (LoeCardsGen.cs:494-501) is a PLAY_CARD trigger that requires IsOverloadCard and reads the card's printed OVERLOAD tag. So 'Whenever you Overload, gain +1 Attack per locked Mana Crystal' ignores every Overload that Spell Lock adds. Unbound Elemental (EX1_258) has the same PLAY_CARD/IsOverloadCard shape. The implementer's report notes this ('Tunnel Trogg ... does not react') but leaves it as is.

Scenario: Encounter dala_54_zibb. The resolved deck enc-dala_54_zibb holds 2x LOE_018 Tunnel Trogg and 2x EX1_258 Unbound Elemental. Zibb has a Tunnel Trogg (1/3) on the board and plays Healing Rain (3). The passive at DungeonRunEncounters.F17.cs:208 Overloads him for 3, so the card text says the Trogg should become 4/3. In the engine it stays 1/3, because Healing Rain has no printed Overload. The same happens to the run player's Troggs. The synergy the wiki deck relies on is lost.

Verifier: Confirmed for Tunnel Trogg only. LoeCardsGen.cs:494-501 scripts LOE_018 as a PLAY_CARD trigger with SelfCondition.IsOverloadCard (Card.HasOverload) and reads the played card's printed OVERLOAD tag. EncounterRules.OverloadFor (EncounterRules.F17.cs:150-156) only raises OnOverloadTrigger, so an Overload added by Spell Lock never reaches a Trogg. The family's own recorded design says otherwise: curation.json:431 and F17.json:1782 state 'through the engine's Overload ... event (Tunnel Trogg, Likkim and Lava Shock of the deck see it)'. Zibb's resolved deck has 2x LOE_018. Scenario: Zibb has a Trogg in play and plays Healing Rain (3). He is Overloaded for 3, but the Trogg stays 1/3 when 'Whenever you Overload' makes it 4/3. The Unbound Elemental part is refuted: EX1_258's text is 'Whenever you play a card with Overload', and Healing Rain has none, so not reacting is correct.

Fix: Make Tunnel Trogg react to the Overload event, not to card play. In LoeCardsGen.cs, LOE_018 becomes Trigger(TriggerType.OVERLOAD) { TriggerSource = TriggerSource.FRIENDLY, SingleTask = CustomTask that adds LOE_018e to SOURCE with the amount from g.CurrentEventData.EventNumber }. Generic.OverloadBlock raises the same event for printed Overload, so ordinary Overload cards still work. If editing a shared card is out of lane, do it inside OverloadFor instead: after OnOverloadTrigger, give each non-silenced LOE_018 on c.BoardZone LOE_018e for `amount`. Otherwise correct the curation and F17.json notes to say Tunnel Trogg does not see it.

## F17.2 [low] engine/SabberStoneCore/src/Actions/EncounterRules.F17.cs:116
Blackmail is supposed to 'Replace a random card in your opponent's hand with an Extortion'. Instead it removes the card (DetachForEscrow, line 111) and appends the Extortion at the end of the hand with AddHandPhase. A replacement should take the replaced card's hand slot, so every card to the right of the taken one shifts left.

Scenario: The run player's hand is [A, B, C] and Blackmail picks A. The hand becomes [B, C, Extortion] instead of [Extortion, B, C]. Any position effect now changes: B becomes the leftmost card, so an Outcast or leftmost/rightmost-card effect works on B when it should not, and the Extortion sits at the right edge.

Verifier: The wiki's Replace page says 'Replace is the non-battlefield equivalent of transform', and a transform keeps the slot. Blackmail (EncounterRules.F17.cs:96-118) records `pick`, detaches the card (line 111), then calls Generic.AddHandPhase (line 116). That helper calls HandZone.Add(playable) with position -1, so the Extortion always goes to the right end. Scenario: hand [A, B, C], Blackmail picks A, and the hand becomes [B, C, Extortion] instead of [Extortion, B, C]. Leftmost/rightmost-card effects and the Extortion's place then differ from the card text. Severity is low.

Fix: Put the Extortion in the taken card's slot. Keep `pick`, and after AddHandPhase move the Extortion back to that index, for example victim.HandZone.MoveTo(extortion, pick) inside the same mutation scope. Or give the hand-add helper a zonePosition argument and pass `pick`. The hand cannot be full here, because one card was just removed. Add a c-enc-17 check that the Extortion's ZonePosition equals the taken card's former position.

## F17.3 [medium] verify/bin/bp.sh:227
The enc17-check gate passes even when there is nothing to check. If `$R/bp-enc17-cards-a.txt` is missing, the run crashed, or the last line has no ", watched ..." section, `unplayed` comes back empty and the gate prints "all 7 boss cards played by the boss". It also never checks that the seven watched ids were actually parsed.

Scenario: Run `bp.sh enc17-check` before `bp.sh same enc17-cards` has written the a-file: `tail` fails (only stderr), `unplayed` is empty, and it prints "all 7 boss cards played by the boss". The same happens when c-bosspilot-enc09 throws and its last log line is an exception message: sed leaves the line unchanged, grep finds no "x0", and the gate passes. Compare enc03-check (bp.sh ~170), which exits 1 when a slice file is missing.

Verifier: Confirmed in bp.sh:225-227. enc17-check is a separate command, unlike f04-boss, which checks right after `twice` in the same call. It never tests that $R/bp-enc17-cards-a.txt exists. When the file is missing, tail prints only to stderr, `unplayed` is empty, and the gate prints 'all 7 boss cards played by the boss'. When the last line has no ', watched ...' section (for example the harness crashed and `same` stripped an exception line), grep finds no 'x0' and the gate passes again. It also never checks that the seven CARDS17 ids appear. enc03-check (bp.sh ~170) exits 1 when a slice is missing. The scenario is concrete, but it is a gate false-pass with low impact, since the operator also sees the exit code that `once` prints.

Fix: In enc17-check, first run `[ -f "$R/bp-enc17-cards-a.txt" ] || { echo "enc17-check: run bp.sh same enc17-cards first"; exit 1; }`. Then take the last line and, for each id in ${CARDS17//,/ }, require a match for "$id x[0-9]+". Mark an id missing when there is no match, and unplayed when its count is 0. Print FAIL and exit 1 when any id is missing or unplayed.


All three review findings are fixed in commit `945172a` on `wip/f17`, on top of `97d40ac`. Every rerun came out twice identical, with 0 faults, 0 fallbacks, 0 failed checks and 0 swallowed leaf exceptions; I agreed with all three findings.

That commit is your WIP commit `9c1281d` reworded with a proper message; the content is unchanged. It was pushed as a backup, so the backup branch no longer matches `wip/f17`. Nothing new was pushed.

**F17.1 – Spell Lock now reaches Tunnel Trogg.**
- **Fix:** made inside the lane, in `EncounterRules.OverloadFor` (`EncounterRules.F17.cs`). The engine's Tunnel Trogg only reacts to a card with printed Overload. So after raising the Overload event, the function now gives each Tunnel Trogg of the overloaded player its own Attack buff for the amount, unless it is silenced, dying or a permanent. The shared Tunnel Trogg script (`LOE_018`) is untouched.
- **New checks (c-enc-17):**
  - I10: the run player's Fireball adds 4 Attack to their Trogg; a silenced Trogg and the boss's Trogg get nothing.
  - I11: Lightning Bolt adds 2 in total, 1 from the card's printed Overload and 1 from Spell Lock.
  - I12: the boss's spell buffs only the boss's Trogg.
- **Notes corrected:** the Zibb note in `F17.json` (via `make_f17.py`) and the Spell Lock admission reason now say how Tunnel Trogg sees it.
- **Separate engine issue (left alone, outside my lane):** the shared Tunnel Trogg script has no trigger source, so it also gains Attack when the *opponent* plays a printed-Overload card, although the text says "Whenever you Overload". If that script is later rewritten to react to the Overload event, the code I added in `OverloadFor` must be removed, or the Trogg will gain twice.

**F17.2 – Blackmail keeps the hand slot.** The Extortion now takes the taken card's position, and the other cards keep their order. New check N5b uses the hand [Wisp, Moonfire, Squire, Fireball]; the card taken was slot 0, so the check would have failed before the fix.

**F17.3 – `bp.sh enc17-check` now fails properly.** It fails when the result file is missing, when the last line has no watched counts, or when any of the seven boss cards is missing or was never played. I tested all four failure cases (each exits 1) and the passing case.

**Results**

- **Engine build:** 0 warnings, 0 errors (md5 `e86c70281de61d40c3c194e2c1cbe0d4`).
- **Generators:** run twice, byte-identical. Counts are unchanged: encounters 83 rows / 87 decks (53 authentic) / 200 adaptations / 1 rejected; ledger 4733 rows / 2813 implemented / 350 partial / 512 missing. Only the note text changed, plus the regenerated fixture.
- **Suites, run 1 = run 2:**
  - c-enc-17: 123 passed, 0 failed (was 119; four new checks)
  - c-admission: 3259
  - c-content: 904
  - c-catalog: 127
- **Bosspilot runs:**

| mode | games / boss wins | power-unused | auto-unused | boss cards played from hand |
|---|---|---|---|---|
| enc17 | 28 / 18 | 1 (Zuramat) | 0 | Void Shift 4, Thorium Chicken 3, Kirin Tor Guard 5, Rival Accomplice 1, Lotus Bruiser 0, Fruit Slasher 0, Infuriated Banker 2 |
| enc17-seeds | 28 / 21 (was 22) | 2 (Zuramat, Deryl) | 0 | — |
| enc17-void | 6 / 6 | 0 | — | Void Shift 9 |
| enc17-cards | 20 / 9 | 0 | — | all 7 boss cards played |

- **Why the numbers moved:** the Trogg fix changes play in Zibb's games. In enc17, Lotus Bruiser went from 1 play to 0; in enc17-seeds, boss wins went from 22 to 21. In enc17-cards, Lotus Bruiser went from 13 to 11 and Fruit Slasher from 19 to 16.
- **The power-unused entries are AI choices:** Zuramat uses To The Void in all six enc17-void games, and Deryl uses his power in enc17.

**Shared files touched by this commit:** only `verify/bin/bp.sh`, in the `enc17-check` block. No new tags, pins or shared engine edits.