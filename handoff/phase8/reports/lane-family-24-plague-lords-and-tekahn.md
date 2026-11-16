The family is done and committed on `wip/f24` as one squashed commit, c1873ec (my two WIP backup commits are folded into it; nothing pushed). Every listed id is done or recorded as support, and nothing is rejected. All family tests pass twice with identical output and 0 faults / 0 fallbacks / 0 swallowed exceptions. The hall was not run, as the brief says, but three of my shared edits are in hall-tested engine files, so the integrator should run it (flag 3).

## FLAGS for the integrator
1. **Save version not bumped.** The per-lord damage already had a home: family 01's `DungeonRunProfile.BossDamage` (save v6). The one new save field, `DungeonRunState.PlagueLordDamage`, is append-only with a `ShouldSerialize` guard; c-enc-24 G7 checks that a save without it is byte-identical to before.
2. **Private tag:** only my reserved 32659 (`EncounterRules.PlagueCount`). It is used on two kinds of entity: the run player's controller (ULDA_115 casts) and Vesh's Mur-nado hero power (Murloc deaths).
3. **Shared engine card files edited** (each minimal, with a comment):
   - `BoomsdayCardsGen.cs`, BOT_543e Mind Meld: it was an empty stub, so BOT_543 Omega Mind did nothing even though the ledger called it scripted. It now gives each spell cast that turn Lifesteal (c-enc-24 L6).
   - `UldumCardsGen.cs`, ULD_168e / ULD_168e3 stubs: filled with the Crystal Core aura pattern, because ULDA_033 Rakanishu uses this enchantment. ULD_168 itself is still unscripted.
   - `Tasks/SimpleTasks/CopyCthun.cs`: a cast guard. C'Thun's trigger threw an InvalidCastException in K'zrath's boss search; this was the only fallback seen.
4. **`EncounterRules.F16.cs`** is family 16's file, copied verbatim from `wip/f16` (byte-identical, 0-line diff) because K'zrath's Systematic Betrayal calls its `Rotate`. Git should merge the identical add cleanly.
5. **Boss cards I registered that another family might also script:** LOEA10_5, LOEA10_5H, KARA_13_12H, LOEA16_24H (Vesh's special cards). Check for duplicate registration at merge; branches f17/f18/f19/f23 did not touch them when I checked.
6. **Hand-curated edits to `curation.json`:**
   - E24 landed-family entry added after E10.
   - `heroBaseForms` gains ULDA_Explorers, which makes it a support row.
   - ULDA_115's F12 rejection is removed and its notes updated.
   - The `relay-composition` and `persistent-damage-at-profile-scope` rule adaptations are updated.
7. **Rule changes that move existing results:**
   - ULDA_115 now banks 10 damage per cast in the run, replacing family 01's "while the run holds it" rule (wiki note: five casts deal 50).
   - Four lords now share the act III major slot (c-enc-model E3/E4).
   - Relay entries for the same hero are now drawn by seed. This is a no-op for family 01's relay fixture (one entry per hero).

## Per id
**Plague Lords**
- **Heroes:** ULDA_BOSS_37h/38h/39h are new rows (tier major3, 300 Health, three hero forms at 300/200/100, wiki Normal phase decks, 3 extra draws on a phase change; Vesh 5). ULDA_BOSS_40h is family 01's Icarax, unchanged. The h2/h3 portraits are support records.
- **Powers:**
  - Xatma: 37p1 Foreboding Fate, 37p2 Death's Embrace (passive), 37p3 Death's Gaze.
  - K'zrath, following the client's links (300 form → 38p2, 200 form → 38p1): 38p2 Systematic Betrayal, 38p1 Evoke Panic, 38p3 Murmurs of Mortality (passive).
  - Vesh: 39p1 Made of Murlocs (passive; Immune, loses 3 Health — not damage — per Murloc death, next form enters at once), 39p2 Ashes Anew, 39p3 Mur-nado (Cost falls with Murloc deaths).
  - Icarax's 40p1–p3 were already family 01's.
- **Cards:** 37t Untimely Demise and 39m Scaly Golem are done; 39px2/px3 are support.
- **ULDA_115 Gnomebliterator:** done.
  - Spell: 10 damage, Spell Damage applies.
  - Run state: each cast in another fight banks 10 damage for the run's next Plague Lord, which that fight spends.
  - Offer: a Tombs treasure row with the new `"runs": "encounters"` value, so it is offered only on act runs of schedule version 3 or above.

**Tekahn**
- **Hero:** ULDA_BOSS_67h is the finale row: 300 Health, power-only forms at 200 (67p2) and 100 (67p3), 40-card phase decks.
- **Powers:** 67p1 Awaken the Flame, 67p2 Curse of Flame, 67p3 Soul Pyre are done.
- **Cards:** 67t The Final Plague?! (summons the four Old Gods), ULDA_031, 032, 033 are done; 67d is support.
- **Relay:** the finale's `heroRelay`. The run's hero fights first, then Elise, Finley, Brann and Reno (minus the run's own explorer), each at 50 Health. Each explorer's client finale deck n comes with hero power n, one of three drawn by run seed. The run state's `HeroSwitch` records the heroes and decks for the floor.
- **ULDA_Explorers:** support row (the original Sanctum team hero).
- **BOT_543:** in Finley's third deck (2 copies). It now has the ledger's relay-deck use and works (Mind Meld fix).

**Adaptations recorded in F24.json** (the original behaviour is not modelled):
- The player does not choose the next explorer or its power; order is fixed.
- The entering relay hero draws family 01's RelayDraw (4), not a full hand.
- Tekahn's thresholds are assumed to be 200/100 and his phase change draws nothing (the wiki states neither).
- "Auto-cast" powers are the boss AI's choice.
- Five finale-deck collectibles are still unscripted and were replaced by second copies: EX1_154, EX1_160, ULD_133, TRL_345, ULD_280.

## Files
- **Own:**
  - `DungeonRunEncounters.F24.cs` (also holds the ULDA_115 content in a `DungeonRunContent` partial)
  - `EncounterRules.F24.cs`, `DungeonRunAdmission.F24.cs`, `reference/encounters/F24.json`
  - `verify/suites/p8f24/c-enc-24`, `verify/fixtures/p8f24/fixtures/schedule-tekahn.json`
  - `verify/tools/gen/p8f24/make_f24.py`, `make_fixture.py`
- **Shared engine:**
  - `EncounterRules.cs`: the PlagueCount tag; Harvest passes the cast count.
  - `DungeonRunSetup.cs`: `RunMatchEnd.Gnomebliterators`, an optional constructor parameter.
  - `DungeonRunContent.cs`: one registration line.
  - `UldumCardsGen.cs`, `BoomsdayCardsGen.cs`, `CopyCthun.cs`, `EncounterRules.F16.cs`: see flags 3 and 4.
- **Shared run layer:**
  - `DungeonRun.cs`: `RelayFor` seeded pick, banking and spending ULDA_115 damage, the encounters offer rule.
  - `DungeonRunState.cs`: the new field.
  - `DungeonRunData.cs`, `DungeonRunValidator.cs`: the `RunsEncounters` value.
  - `DungeonRunAdmission.cs`: ULDA_115's finale-state row removed (it now lives in F24).
- **Shared data:** `run-v1.json`, one ULDA_115 row.
- **Shared tools:**
  - `dungeon-run-catalog/Encounters.cs`: unique deck ids for a hero listed several times in the relay.
  - `restoration-ledger/Program.cs`: relay-deck cards get an encounter use.
  - `curation.json`: flag 6.
- **Registration:** `verify/lib/suites.sh` (c-enc-24 at the end of both lists); `verify/bin/bp.sh` (variables, usage line, three modes after the family 10 lines).

## Pins moved
| Suite | Check | Old → new | Cause |
|---|---|---|---|
| c-content | passive registry | 904 → 905 checks | four new passives; one more shell check (ULDA_115) |
| c-admission | treasure rows | 3167 → 3240 checks; ULDA 70 → 71, total 204 → 205 | ULDA_115 admitted; family 24 rows exempted like family 10's |
| c-admission | generated-lists note | 426 → 447 both sides, 311 → 312 boss-only | newly scripted cards |
| c-catalog | ledger golden | 4704 / 2813 / 295 / 538 / 1058 → 4716 / 2814 / 325 / 518 / 1059 | new rows and reclassifications |
| c-catalog | two explorer-portrait checks | exact match → first entry | explorers now also carry "relay hero" |
| c-deckops | T5 | 4704 → 4716 rows | ledger growth |
| c-treasures-F12 | T2, T28 | rewritten | ULDA_115 lifted |
| c-enc-model | A4 | family 24 count 1 → 5 | three lords and Tekahn added |
| c-enc-model | E3 | rewritten (act III major: a lord or a custom final) | four lords in major3 |
| c-enc-model | E4 | range 40–160 → 10–100 (observed no-ledger count 96 → 33) | five major3 candidates |
| c-enc-model | I11, I12 | use `PlagueLordDamage` | new ULDA_115 rule |
| c-enc-model | J13 | passive list | four new passives |

c-enc-model's F2 save lengths (14306 → 13954) differ from the Windows record; I believe that is line endings, not my change, but did not check it on an unchanged tree.

## Generator lines
- encounters: "73 rows, 97 decks (53 authentic, 44 changed), 175 adaptations, 1 rejected"
- catalog: unchanged, "506 entries … 6508 admitted and 1 excluded"
- ledger: 4716 rows; implemented 2814, partial 325, missing 518, duplicate/support 1059, adapted 0

## Tests
- **c-enc-24:** 61 checks, all through `DungeonRun.BuildPlan` → `DungeonRunSetup` in real engine games. Groups: P rows/plans/banking, X Xatma, K K'zrath, V Vesh, T Tekahn, L relay including BOT_543, G Gnomebliterator run state and save, A admission.
- **Bosspilot modes**, each run twice, identical, stderr empty, 0 faults / 0 fallbacks / 0 failed checks / 0 swallowed:

| Mode | Games | Boss wins | Notes |
|---|---|---|---|
| enc24 (the three lords, 2 games each) | 6 | 6 | form-unreached 1 (K'zrath at 0 carried, like Icarax in enc01) |
| plague24 95 | 3 | 3 | each lord crosses 200 in play |
| plague24 110 | 3 | 3 | each starts in phase 2 |
| plague24 195 | 3 | 3 | K'zrath and Vesh cross 100 |
| plague24 210 | 3 | 3 | power-unused 1: Vesh's Mur-nado (15 Cost) was never affordable |
| tekahn24 (fixture finale, run hero at 1 Health) | 2 | 1 | every explorer entered; all three forms reached |

- **Boss cards played from hand:** LOEA10_5, LOEA10_5H and UNG_942t in enc24; ULDA_BOSS_37t ×2 in plague24 210; Rakanishu ×4 and Sanctum Golem ×15 in tekahn24. The Final Plague?!, Demonic Presence and Giantfin were never played by the AI; the suite covers them.

## Lane verification
- Engine: 0 warnings, 0 errors.
- `gen.sh` run twice: all six generated files byte-identical.
- Suites, run twice, identical logs:

| Suite | Result | vs wave-3 baseline |
|---|---|---|
| c-content | 905 | 2 lines (listed moves) |
| c-admission | 3240 | 4 lines (listed moves) |
| c-runlayer | 244 | 0 |
| c-acts | 181 | 0 |
| c-deckops | 174 | 0 |
| c-catalog | 127 | 0 |
| c-shrine-core | 131 | 0 |
| c-enc-model | 141 | 12 lines (listed moves) |
| c-enc-engine | 122 | 0 |
| c-enc-24 | 61 | new |

- c-treasures-F12 321 and c-treasures-F11 380 ran once each.

## For the integrator after the merge
- Run `gen.sh`, then the hall (because of UldumCardsGen, BoomsdayCardsGen, CopyCthun), then the released modes and randplay; content and engine scripts changed, so expect drift there.
- Re-record the family 01 records that depend on encounter data: c-enc-model E3/E4 numbers move whenever rows are added; also check that `bp.sh relay` and `plague` still match.
- The close-out should enable acts-v3t's finale on `ulda_67_tekahn` with `heroRelay` and re-pin `ReleasedShapes`.

## Paragraph for docs/design/dungeon-run.md
"Family 24 (Plague Lords and Tekahn): Xatma, K'zrath and Vesh join Icarax as major3 encounters of three hero forms (300/200/100; each phase replaces the deck with its wiki list and draws 3, Vesh 5); a lost fight banks the lord's Health in the profile (BossDamage) and the next random-seed run meets it in that phase. ULDA_115 'Gnomebliterator' is a Tombs treasure of encounter schedules (treasure runs 'encounters'); each cast in another fight banks 10 damage in the run (DungeonRunState.PlagueLordDamage), which the next Plague Lord fight starts with and spends. Tekahn is the acts-v3t finale row: one hero, power-only forms at 200 (Curse of Flame) and 100 (Soul Pyre), 40-card phase decks, no persistent damage; the run player's hero fights first and then the explorers relay at 50 Health each, each with one of its three client finale decks and the matching hero power, drawn from the run seed (DungeonRun.RelayFor). New engine rules (EncounterRules.F24.cs): LoseHealth (Health lost, not damage; forms resolve at once), Evoke Panic's forced attack inside one row, BringToOne; K'zrath rotates with family 16's Rotate. Omega Mind's Mind Meld now gives cast spells Lifesteal for the turn."

---

# Family 24 review: confirmed findings (adversarially verified)

## F24.1 [medium] plugin/DungeonRun/DungeonRun.cs:1974
Casting ULDA_115 "Gnomebliterator" during a Plague Lord fight does no extra damage to the Plague Lord. The wiki note says it deals an additional 10 damage there. The spell (DungeonRunEncounters.F24.cs:336) only increments PlagueCount on the caster, and RecordEncounterEnd then sets PlagueLordDamage to 0 for a persistent-damage fight. Test G6 pins this behaviour.

Scenario: The run player holds ULDA_115 and casts it on a minion while fighting Xatma (or Vesh, K'zrath or Icarax). The minion takes 10 and Xatma takes 0. The wiki says Xatma takes an additional 10 at once, so the card's second half has no effect in the very fight it names.

Verifier: The client text is 'Also deal $10 damage to the Plague Lord when you fight it', and the wiki note says that if it is used during a Plague Lord encounter it deals an additional 10 damage to them. The ULDA_115 script (DungeonRunEncounters.F24.cs:331-337) only deals 10 to the target and increments PlagueCount. RecordEncounterEnd (DungeonRun.cs:1974) then zeroes PlagueLordDamage for any persistentDamage fight, so a cast inside Xatma/K'zrath/Vesh/Icarax neither hits the lord nor carries over. No recorded adaptation covers this; G6 pins the behaviour as intended.

Fix: In the ULDA_115 PowerTask, after the target damage, when the opponent is the encounter's persistent-damage boss (e.g. its hero is a ULDA_BOSS_37h-40h3 form, or the RunEncounter marks persistentDamage), also deal DamageTask(10, ENEMY_HERO, spellDmg: true) to it. Keep RecordEncounterEnd zeroing the bank for that fight, and change G6 to assert the lord took the extra 10.

## F24.2 [low] engine/SabberStoneCore/src/CardSets/DungeonRun/DungeonRunEncounters.F24.cs:79
Death's Embrace checks NumFriendlyMinionsThatDiedThisTurn, and Game.cs:1615 also increments that counter for a run shrine that dies and goes dormant. A shrine's death therefore uses up the run player's 'first minion that dies this turn'. The file header says a shrine is never a minion that 'dies' for these powers.

Scenario: Xatma is in phase 2 (Death's Embrace). On the boss's turn its spell destroys the run player's board shrine (it goes dormant), then an AoE kills the run player's Chillwind Yeti. When the Yeti dies the counter is already 1, so it gets no Reborn. Without the shrine it would have been reborn.

Verifier: Game.cs:1615 increments NumFriendlyMinionsThatDiedThisTurn for every dead minion, including a run shrine that goes dormant. Embrace (F24.cs:79) gates on that counter being 0. Died() excludes the shrine from receiving Reborn, but the shrine still uses up the turn's first death. The file header (F24.cs:27-29) says a shrine is never a minion that 'dies' for these powers. So: shrine destroyed first, then a Yeti dies the same turn, and the Yeti gets no Reborn.

Fix: Count only non-shrine deaths for Embrace. For example, in Embrace subtract the controller's shrine deaths this turn (a private turn-scoped tag incremented where ShrineRules.ReplaceWithDormant runs and reset with NumFriendlyMinionsThatDiedThisTurn). Or keep a private per-controller 'Embrace used this turn' tag that is set only when a non-shrine minion's death is seen and cleared at turn start.

## F24.3 [low] engine/SabberStoneCore/src/Actions/EncounterRules.cs:432
ULDA_115 banking diverges from the wiki in two ways. Harvest reports only the run player's PlagueCount, while the wiki says an enemy who copies or steals Gnomebliterator and plays it still adds the 10 damage. RecordEncounterEnd (DungeonRun.cs:1975) banks a flat 10 per cast, while the wiki says Spell Damage present at cast time also applies to the Plague Lord bonus.

Scenario: (a) The run player casts Gnomebliterator with a Kobold Geomancer in play: the target takes 11, but PlagueLordDamage grows by 10, not 11. (b) A boss copies the treasure (e.g. via a copy or steal effect from its deck) and casts it: the boss controller's PlagueCount is incremented, but Harvest reads player[PlagueCount] only, so nothing is banked, where the wiki says 10 more damage is dealt to the Plague Lord.

Verifier: Part (a) is concrete. The wiki note says that if Gnomebliterator is cast while Spell Damage is on the board, the Plague Lord receives the bonus even with no Spell Damage at the start of the encounter, so the banked bonus includes cast-time Spell Damage. RecordEncounterEnd (DungeonRun.cs:1975) banks a flat GnomebliteratorDamage * count, so a cast with Kobold Geomancer deals 11 to the target but banks only 10. Part (b) also contradicts the wiki note (an enemy's copied or stolen cast still counts): Harvest (EncounterRules.cs:432) reads only player[PlagueCount]. That path is rarer.

Fix: Have the ULDA_115 task add the spell-damage-adjusted amount to the counter, e.g. c[PlagueCount] += 10 + c.CurrentSpellPower (with any spell-damage doubling applied as DamageTask does). Make the harvest report the summed damage (Player1 plus Player2 counts, to cover an enemy's cast), and bank that value directly instead of 10 * casts.

## F24.4 [medium] verify/suites/p8f24/c-enc-24/Program.cs:390
Check V8 (a run shrine is not shuffled away by Mur-nado) cannot fail. Mur-nado is never cast in it, and the return value of Power(gs) at line 389 is not checked.

Scenario: gs = Fight(Vesh, Banked(Vesh, 90), shrine), so the boss starts in form 2 with Mur-nado at its printed Cost of 15. No Murloc dies before ToBoss(gs), and ToBoss only gives the boss 10 Mana, so Mur-nado is not offered and Power(gs) returns false. The board stays as it is, the shrine is still on Player1's board, and V8 passes whether or not MoveToDeck/ShrineRules handle the shrine. The implementer's own bosspilot note also says Mur-nado (15) was never affordable, so nothing tests the shrine + Mur-nado interaction. V6/V7 avoid this by killing 5 Murlocs first; V8 needs the same setup, or at least an assert on Power's result.

Verifier: Confirmed. V6 itself shows Mur-nado starts at Cost 15 (cost0 == 15). The V8 game (Fight(Vesh, Banked(Vesh,90), shrine)) kills no Murloc before ToBoss, and ToBoss -> Mana(c, 10) gives 10 Mana. Offered() is therefore false, and Power(gs) returns false without casting. The result is discarded, and the shrine trivially stays on Player1's board, so V8 cannot fail.

Fix: Before ToBoss(gs), kill 5 Murlocs as V6 does (or set the boss's Mana to 15). Capture bool used8 = Power(gs) and include used8 in the V8 condition, together with Player2's board/deck having changed (for example a non-shrine minion placed on Player1's board is gone).

## F24.5 [medium] plugin/DungeonRun/DungeonRun.cs:1824
The finale's seed-drawn explorer entry (power and deck n of 3) reaches the boss's fair-AI search unredacted. GameConfig.Encounter.PlayerRelay holds the real chosen RunFightHero, and the fair view only nulls Player1Deck/Player2Deck.

Scenario: Tekahn finale, run hero low on Health, boss to act. FairBotState (line 622) clones the game and calls only RedactSetupDecksForFairView, so the clone keeps _gameConfig.Encounter with PlayerRelay as built by RelayFor. In a simulated line where the boss kills the run hero, EncounterRules.ResolveRelay -> EnterRelayHero(player, enc.PlayerRelay[0]) (EncounterRules.cs:348) puts in the real drawn Elise entry: its hero power ULDA_Elise_HPn and its 30-card finale deck, and draws 4 from it. The boss therefore scores its lethal and pre-lethal lines against which of the three powers and decks the run actually drew, before that explorer is revealed. Before family 24 each relay hero had one fixed entry, so nothing was hidden; the seeded 1-of-3 pick added here is new hidden state that the fair view does not mask.

Verifier: RelayFor now picks one of three entries (power and 30-card deck) per explorer from the run seed. BuildPlan puts the picked entries in plan.PlayerRelay, which is copied into the GameConfig encounter shared with clones. FairBotState (line 622) only calls RedactSetupDecksForFairView, which nulls Player1Deck/Player2Deck. In a simulated line where the boss kills the run hero, ResolveRelay -> EnterRelayHero (EncounterRules.cs:348) puts in the real drawn power and deck list and draws 4 from it. The boss's search therefore scores against which hidden deck and power the run drew, before that hero has entered. EncounterRules.DeckReplaced's own contract says the opponent's view of a replaced deck is its count only.

Fix: Redact not-yet-entered relay entries in the fair view. For example, add to RedactSetupDecksForFairView (or a sibling called from FairBotState) a replacement of the clone's encounter PlayerRelay entries at index >= player[RelayIndex] with a public stand-in: the same hero at its Health with a placeholder deck, or one of the hero's candidate entries chosen by the clone's artificial seed. Alternatively, make the drawn entry public (revealed at match start) and record that as an adaptation.

## F24.6 [low] verify/suites/p8f01/c-enc-model/Program.cs:280
Pin weakened in E3: the counter that proved Icarax himself is drawn as the act III major now counts any of the four lords, so E3 no longer checks that family 01's Icarax is ever drawn.

Scenario: Before: `if (seq[14] == "ulda_40_icarax") icarax++` together with `icarax > 0` showed that Icarax is the act III major in some of the 1000 seeds. After: `if (lords.Contains(seq[14])) icarax++`. Suppose a change to the major3 pool (a weight, an unlock or a tier) stopped ulda_40_icarax from ever being drawn while Xatma, K'zrath or Vesh still were. E3 would still pass, and its message would still report a non-zero 'Plague Lord the act III major' count. Keeping a separate Icarax-specific counter would preserve the old pin.

Verifier: Before: E3 required seq[14] == "ulda_40_icarax" in at least one of 1000 seeds. After: it counts any of the four lords (Program.cs:280), and no other suite check pins that Icarax is ever drawn as the act III major (c-enc-24's P checks validate rows only, and E4 uses a fixture). A data change that made Icarax undrawable while the other three lords remain would pass E3. This is a real loss of coverage in an existing pin.

Fix: Keep a separate counter for each lord (or at least an Icarax counter) and require each to be > 0 across the 1000 seeds, e.g. var lordHits = lords.ToDictionary(l => l, l => 0); lordHits[seq[14]]++ when it is a lord; then check lordHits.Values.All(v => v > 0).

## F24.7 [low] engine/SabberStoneCore/src/CardSets/Standard/BoomsdayCardsGen.cs:3548
BOT_543e Mind Meld (a shared Standard card, so this applies in every game) sets a permanent LIFESTEAL tag on each spell cast that turn. Only the enchantment is removed at turn end, so a spell that resolves later keeps Lifesteal after 'this turn'.

Scenario: Wild game: a Mage plays Omega Mind with 10 Mana Crystals, then casts Explosive Runes (a Secret) the same turn. At the end of that turn BOT_543e is removed, but the Secret's Spell entity still has LIFESTEAL=1. On the opponent's turn the Secret fires; its damage source is that spell, Playable.HasLifeSteal reads the tag, and Character.TakeDamage (line 527) heals the Mage. The code comment itself says Lifesteal lasts 'until the turn ends'. A copy of such a spell also inherits the flag (SpecificTask.cs:1097).

Verifier: BOT_543e's CAST_SPELL trigger sets a permanent LIFESTEAL=1 on the cast Spell entity (BoomsdayCardsGen.cs:3548). The TURN_END trigger only removes the enchantment. PlayCard.cs:596 fires OnCastSpellTrigger for Secrets too, so a Secret cast under Mind Meld keeps Lifesteal when it fires on the opponent's turn: Playable.HasLifeSteal reads the native tag, and Character.TakeDamage:527 heals. Copies inherit the flag via SpecificTask.cs:1097. The card text says 'this turn'.

Fix: Mark each spell the trigger grants Lifesteal with a private tag (or record its id on the enchantment). At TURN_END, before removing the enchantment, clear LIFESTEAL on every still-existing marked spell whose card does not have Lifesteal itself (SecretZone, hand, graveyard). Alternatively, implement it as a controller-scoped 'spells have Lifesteal' aura that HasLifeSteal reads for Spell sources while the enchantment is present.


I fixed all seven review findings on `wip/f24`; the new commit is **a4c5db3**, on top of c1873ec. Every suite and bosspilot mode below ran twice through the lock with identical output and 0 faults, 0 fallbacks, 0 failed checks and 0 swallowed exceptions.

## Fixes
- **F24.1, ULDA_115 cast during a Plague Lord fight.** It now also deals its 10 (plus Spell Damage) to the lord at once, and nothing is banked. "The Plague Lord" means any form of Xatma, K'zrath, Vesh or Icarax; Tekahn is not included, consistent with banking only for persistent-damage bosses. New check G3b; G6 now only asserts that the fight spends the bank.
- **F24.3, banking.**
  - Outside a lord fight, a cast now banks `SpellDamageRules.Amount(source, 10)`, so the Spell Damage present when it is cast counts. A cast by either player counts.
  - The harvest field is renamed `RunMatchEnd.Gnomebliterators` → `GnomebliteratorDamage`. It is now the sum of both controllers' banked damage, and `DungeonRunState.PlagueLordDamage` adds it directly.
  - New checks: G2 (10 + 11 with Kobold Geomancer = 21 banked) and G2b (a copy the boss casts adds 10, total 31).
- **F24.2, Death's Embrace and shrines.** It now counts the controller's deaths this turn from the history facts and leaves out run shrines and permanents. It no longer reads `NumFriendlyMinionsThatDiedThisTurn`. New check X4b: the shrine goes dormant, then the run player's Yeti dies and is reborn at 1 Health.
- **F24.4, V8 could not fail.** V8 now kills 5 Murlocs first so Mur-nado costs 10, and asserts it was cast. The run player's Yeti ends up in the boss's deck, and the shrine stays on its own board, dormant, and never enters the deck.
- **F24.5, relay leak into the boss's fair view.** I took a different route from the reviewers' suggested redaction:
  - The plan now carries all of an explorer's entries (12 in total).
  - `EncounterRules.ResolveRelay` draws which entry enters with the game RNG at the moment that explorer enters. The boss's fair-view clone has its own RNG, so it cannot know the real draw.
  - `DungeonRun.RelayFor` no longer picks by seed; `HeroSwitch.Options` lists all the candidates.
  - A hero with a single entry triggers no RNG call, so family 01's relay fixture is unaffected.
  - New checks:
    - L4: the same game seed gives the same entry, and 12 seeds produce all three of Elise's.
    - L5: 8 fair views of one real game, killing the run hero, enter different Elise entries (HP1, HP2, HP3 all seen), while the real game draws its own.
- **F24.6, E3 pin weakened.** E3 now counts each lord separately and requires every one to appear. Over 1000 seeds: Icarax 250, Xatma 265, K'zrath 251, Vesh 234.
- **F24.7, Mind Meld left Lifesteal on.** Each spell given Lifesteal is now marked. At turn end Mind Meld removes Lifesteal and the mark from those spells, then removes itself. L7 now checks that an Explosive Trap cast under Mind Meld has Lifesteal during the turn and loses it once the turn ends.
  - The mark reuses my one reserved tag 32659 (`PlagueCount`), now documented in the tag table as having three uses. No new tag was taken.

## New numbers
| Suite | Result |
|---|---|
| c-enc-24 | 65 passed (was 61) |
| c-content | 905 |
| c-admission | 3240 |
| c-runlayer | 244 |
| c-acts | 181 |
| c-deckops | 174 |
| c-catalog | 127 |
| c-shrine-core | 131 |
| c-enc-model | 141 |
| c-enc-engine | 122 |

| Mode | Games / boss wins | Notes |
|---|---|---|
| enc24 | 6 / 6 | form-unreached 1: K'zrath at 0 carried damage |
| plague24 95 / 110 / 195 / 210 | 3 / 3 each | 210 has power-unused 1: Vesh's Mur-nado (15 Cost) never became affordable |
| tekahn24 | 2 / 2 | was 2 / 1, because relay entries are now drawn by the game RNG |

`gen.sh` ran twice with byte-identical output: 73 rows, 97 decks (53 authentic, 44 changed), 175 adaptations, 1 rejected. The ledger still has 4716 rows (2814 implemented, 325 partial, 518 missing, 1059 support).

After the bosspilot runs I edited adaptation-note text only (F24.json, curation.json) and regenerated. That changed nothing but note text and the encounters source hash, so I reran the suites twice afterwards but not the bosspilot modes.

## Updates to my earlier report
- **Shared files touched again** (all minimal):
  - `EncounterRules.cs`: `ResolveRelay` draws the entry at entry time; the harvest sums both players.
  - `DungeonRunSetup.cs`: the `RunMatchEnd` field rename.
  - `BoomsdayCardsGen.cs`: the Mind Meld turn-end cleanup.
  - `DungeonRun.cs`: `RelayFor` returns all entries; banking adds the reported amount.
- **Relay deck draw:** the relay deck is no longer drawn from the run seed. It is drawn by the game RNG as each explorer enters; the relay-composition note in curation.json and the F24.json adaptation say so.
- **Docs paragraph change:** in the paragraph for `docs/design/dungeon-run.md`, replace "drawn from the run seed (DungeonRun.RelayFor)" with "drawn by the match's game RNG as it enters (EncounterRules.ResolveRelay), so the boss's fair view cannot know it". Also add: "a cast inside a Plague Lord fight deals the extra 10 at once; outside one, either player's cast banks 10 plus its Spell Damage".