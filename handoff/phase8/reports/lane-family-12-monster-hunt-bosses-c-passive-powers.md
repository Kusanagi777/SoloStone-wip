FAMILY 12 (Monster Hunt bosses C) REPORT, worktree /home/user/wt/f12, branch wip/f12

Everything is committed in one commit, 5598b73 `feat(dungeon-run): family 12 Monster Hunt bosses C (passive powers)`, on top of a95b97e. Nothing was pushed, stashed or deleted, and nothing was downloaded. I read 14 wiki pages as raw wikitext into verify/.out/tmp/wiki (gitignored). All 28 ids are done and none is rejected.

FLAGS FOR THE INTEGRATOR
1. **One shared engine edit.** `engine/SabberStoneCore/src/Actions/CostRules.cs` `AdjustCost` now ends with `return EncounterRules.EchoCopyFloor(source, amount);` instead of `return amount;`.
   - Why: the client's Echo rule ("Copies can't cost less than (1)") is not in the engine. Without it, under The Whisperer's "All spells have Echo" the boss's Coin and other 0-Cost spells repeat for free.
   - Scope: it only acts on a GHOSTLY (Echo copy) card whose cost would be below 1, in a game where a hero's power is GILA_BOSS_50p. Every other game gets its number back unchanged.
2. **No private tag taken.** My reserved 32647 is unused; the Echo limit counts Game.GhostlyCards instead of keeping a counter.
3. **Two judgement calls**, each recorded as an adaptation note in F12.json:
   - At most 10 Echo copies per player per turn (`EncounterRules.EchoCopiesPerTurn`). The client has no such number. It stands in for the client's turn timer: The Coin's copy costs (1) and gives that Mana back, so the chain never ends otherwise.
   - Hypnotize removes the enchantments of the hand cards it shuffles back (as HS resets them). This differs from the engine's own `MoveToDeck` / Plot Twist, which keeps enchantments on cards moved into a deck; I did not touch MoveToDeck.
4. **Other shared edits:** curation.json (E12 entry); suites.sh and bp.sh registrations, where I also changed bp.sh's usage print `sed -n 2,29p` to `2,30p` because of the added usage line; pins in five existing suites. Details below.
5. **Not touched:** schedule defaults, finales, acts, pool sets, save version, ReleasedShapes, golden sequences, HANDOFF.md, verify/README.md, verify/baseline, dungeon-run.md, verify/MANIFEST.md.

PER ID (all done)
- **Heroes** GILA_BOSS_24h, 30h, 39h, 45h, 46h, 47h, 50h, 63h, 64h, 65h, 66h, 67h, 68h: rows only. Tier comes from printed Health, as in family 10: 10 → tier 1; 25 and 30 → tier 2; 40 → tier 3; 50 and 70 → tier 4.
- **Passive powers** (registered with `Passive`, started at match start):
  - 24p From the Swamp: a DEATH trigger on enemy minions summons Bloated Zombie 24t, whose Deathrattle deals 1 damage to all characters. A dying run shrine counts as a death.
  - 30p Blood Red Apple: the SPELLS_COST_HEALTH controller aura (Blood Pact's rule), boss side only.
  - 39p Together Forever: a fresh copy of each friendly minion that dies is shuffled into the boss deck, then `EncounterRules.DeckRewritten` records the exact deck for the boss's own-deck ledger. Shrines are excluded.
  - 45p Poisoned Drink: a DRAW trigger on both players deals 1 damage to the drawer. The opening hand and mulligan are dealt, not drawn; a burned card counts; Fatigue does not.
  - 46p Monstrous Aura: a board aura with enchantment 46e.
  - 47p Frumiousity: `BattlecriesTwice(bothPlayers)`.
  - 50p Echoes of the Witchwood: a restless HANDS aura giving Echo to spells, plus the copy floor and the per-turn limit.
  - 63p Tangled Wrath: when the enemy casts a spell, the boss draws a random spell from its own deck.
  - 65p Starving: a friendly Beast dies, the boss draws.
  - 67p Cannibalism: a fast DEATH trigger. `EncounterRules.DeathNeighbours` rebuilds the board as it stood before the deaths (a dead shrine's dormant form stands in its slot); dying, dormant and untouchable neighbours get nothing; enchantment 67e. Approximation: in the engine's mixed path when a location dies at the same time, minions already removed are not rebuilt.
- **Clickable powers:**
  - 64p Hypnotize (2): boss first, then the player. Hand goes into the deck as plain cards, then the same number is drawn. A `RunHelpers.BeliefReset(HandAndDeck)` is recorded for both players, plus `DeckRewritten` for the boss. Offered while either hand holds a card.
  - 66p "..." (1): Silence a minion.
  - 68p Poison Flask (2): 2 damage to a minion; if it survives it gets enchantment 68e (Poisonous).
- **Boss cards:**
  - 24t Bloated Zombie.
  - 68t Infected Quillflinger: whenever it takes damage, 1 damage to a random enemy minion, including on the killing hit. Rancel's deck lists 3.
- **Also registered** (not in the id list, needed by the scripts): enchantments GILA_BOSS_46e, 67e, 68e.
- **Decks** (wiki pages read 2026-11-14): 5 are the wiki's list unchanged, 3 extended, 5 substituted.
  - Witchwood's Touch (2), GILA_BOSS_99t2 (family 23's card), is Shield Block in Groddo, Sephira and Hannigul.
  - The original printings of Wrath and Power of the Wild are replaced by Lesser Jasper Spellstone and Mark of the Lotus (Sephira, Whisperer).
  - Shudderwock's 31st card (Tortollan Primalist) is dropped.
- **Flags:** Whisperer has `recast`, so Wild Magic stays away; Shudderwock has `doubleBattlecry`; Hannigul has `position`; Winslow has `hiddenZone`.

FILES
- Own (new or filled):
  - engine/SabberStoneCore/src/CardSets/DungeonRun/DungeonRunEncounters.F12.cs
  - engine/SabberStoneCore/src/Actions/EncounterRules.F12.cs (new)
  - plugin/DungeonRun/DungeonRunAdmission.F12.cs: 6 new families. boss-passive-public 5 ids, boss-passive-both 4, boss-deck-rewrite 1, boss-hidden-shuffle 1, boss-minion-power 2, boss-hunt-minion 2 (Player|Boss, NeverGenerated).
  - data/dungeon-run/reference/encounters/F12.json, written by verify/tools/gen/p8f12/make_f12.py (reproducible).
  - verify/suites/p8f12/c-enc-12/ (Enc12.csproj, Program.cs).
- Shared:
  - CostRules.cs (flag 1).
  - tools/restoration-ledger/curation.json: landed entry E12 appended after E10; the tool-owned lists rewritten by the generator.
  - verify/lib/suites.sh: p8f12/c-enc-12 appended to BUILD_LIST and RUN_LIST.
  - verify/bin/bp.sh: variable HID12, a usage line, modes enc12, enc12-seeds, enc12-hidden.
- Regenerated: encounters-v1.json, restoration-ledger.csv and .md. catalog-v1.json and dungeon-run-catalog.md are unchanged.

PINS MOVED (suite: old -> new, cause)
- c-content: passive registry list + the ten family 12 passives in registration order. Still 904.
- c-admission: exemption statement for the six new families. Total 3167 -> 3226. NOTE generated lists 426 -> 441.
- c-deckops T5: ledger rows 4704 -> 4727.
- c-catalog ledger golden:
  - old: 4704 rows, 295 partial, 538 missing.
  - new: 4727 rows, 333 partial, 523 missing.
  - cause: +23 partial rows (13 run:boss rows, 10 deck cards new to the ledger); 15 rows go missing -> partial (13 powers, 24t, 68t). The 13 heroes stay missing (run:default-schedule).
- c-enc-model: A4 family counts gain "12:13"; J13 passive list + the ten. Still 141.

GENERATOR LINES (gen.sh run twice, all six files byte-identical)
- encounters: "82 rows, 86 decks (48 authentic, 38 changed), 191 adaptations, 1 rejected"
- catalog: "506 entries (452 offerable pool, 2 withdrawn, 52 fixed), 6508 admitted and 1 excluded" (unchanged)
- ledger: rows 4727, implemented 2813, partial 333, missing 523, duplicate/support 1058, adapted 0

TESTS
- **c-enc-12: 102 checks**, real engine games through DungeonRun.BuildPlan -> DungeonRunSetup. It covers every power and card, these cases, and the rows, admission and validator (P1-P11):
  - Echo copy floor and limit;
  - Hannigul's adjacency with deaths at the same time and a shrine;
  - Forlorn Lovers' own-deck ledger sample;
  - Hypnotize's belief resets, deck fact, and the boss's fair view forgetting a sapped card;
  - Gretta paying Health with 0 Mana;
  - shrine deaths for Groddo and Hannigul.
- **Bosspilot** (shared c-bosspilot-encounter harness). Each mode ran twice through the lock: identical, stderr 0 bytes, 0 faults, 0 fallbacks, 0 failed checks, 0 swallowed.

| Mode | Games | Boss wins | Power-unused | Notes |
|---|---|---|---|---|
| enc12 (FAMILY=12) | 26 | 18 | 0 | Quillflinger played from hand x5 |
| enc12-seeds (SEED_BASE=19000) | 26 | 16 | 0 | Quillflinger x1 |
| enc12-hidden (Lovers, Whisperer, Winslow, 6 games each) | 18 | 10 | 0 | Hypnotize used 34 times |

LANE VERIFICATION
- Engine build: 0 warnings, 0 errors (Linux md5 0c1430d436b64202baa8be54ab22ed5b).
- Suites, each run twice with identical logs:

| Suite | Result | Differing lines vs baseline |
|---|---|---|
| c-content | 904 | 0 |
| c-admission | 3226 | 4 (the total and NOTE lines) |
| c-runlayer | 244 | 0 |
| c-acts | 181 | 0 |
| c-deckops | 174 | 0 |
| c-catalog | 127 | 0 |
| c-shrine-core | 131 | 0 |
| c-enc-model | 141 | 6 |
| c-enc-engine | 122 | 0 |
| c-enc-12 | 102 | new suite |

- c-enc-model's 6 lines: the A4 label, plus the two F2 golden-save length lines (13954 vs 14306, 25744 vs 26370). The F2 gaps are exactly one byte per line, so they look like line endings on Linux, not this change; please confirm on the base tree.

FOR THE INTEGRATOR AFTER THE MERGE
- Merge conflicts to expect: suites.sh, bp.sh, curation.json (after E10), and the pinned lists (c-content passives, c-enc-model A4 and J13, c-admission exemptions, T5 and ledger golden). Recompute these from the merged tree.
- Family 11's rows will land between F10 and F12, so my rows' indices and therefore the enc12 seed lines will move; rerun the three enc12 modes then.
- Larger GIL pools may move other families' bosspilot lines (the provenance-mix walk). I did not run those.
- Rebuild the family fixtures (encounters-v1.json changed), e.g. for c-collectibles-C.
- randplay's regkeys line will move: 18 new registrations. Not run here, as the brief asked.
- When family 23 scripts GILA_BOSS_99t2, rerun the encounters step: Groddo, Sephira and Hannigul become less substituted, and c-enc-12 P3 and P7 need new values. The same applies if anyone scripts the original EX1_154 Wrath or EX1_160 Power of the Wild.

PARAGRAPH FOR docs/design/dungeon-run.md
"Family 12 (Monster Hunt bosses C): thirteen Witchwood encounters, ten with a passive power started at match start, tier by the printed Health of the hero card (10 tier 1; 25, 30 tier 2; 40 tier 3; 50, 70 tier 4). Groddo raises a Bloated Zombie when an enemy minion dies; Gretta's spells cost Health (Blood Pact's rule, her side only); Forlorn Lovers shuffles a copy of each friendly minion that dies into her deck and tells her own-deck ledger the exact list (EncounterRules.DeckRewritten); S'thara's Poisoned Drink deals 1 damage per card drawn to either player; Vitus buffs his board; Shudderwock doubles all Battlecries for both players; The Whisperer gives every spell in either hand Echo, with the client's rule that an Echo copy never costs less than (1) (EncounterRules.EchoCopyFloor, called from CostRules.AdjustCost) and at most ten Echo copies per player per turn, standing in for the client's turn timer; Sephira draws a spell when the run player casts one; Gobbles draws when a friendly Beast dies; Hannigul's Cannibalism gives +1 Attack to the minions that stood next to a dying minion, read inside the death pass (EncounterRules.DeathNeighbours). Winslow's Hypnotize shuffles both hands into their decks as plain cards and redraws as many, recording a hand-and-deck belief reset for both players and the boss's deck list; The Mute silences; Rancel's Poison Flask deals 2 and poisons a survivor, and his Infected Quillflinger hits a random enemy minion whenever it takes damage. Decks are the wiki's lists brought to 30 cards, each change recorded."