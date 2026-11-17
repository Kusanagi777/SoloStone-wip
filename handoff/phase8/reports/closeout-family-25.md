# Step 8 close-out: family 25 and the HANDOFF 3.2 / 3.3 work

Branch `wip/c25` (worktree `/home/user/wt/c25`), from the wave-6 tree `68e7163`. Stage A did the close-out (HANDOFF
3.2, brief items 1-8); stage B fixed the confirmed findings of the close-out review (HANDOFF 3.3) and wrote this
report. Nothing was pushed. The full regression is not run here: it is the queue file named at the end.

## 1. Stage A (the close-out)

### Shared files edited

- Data: `schedule-v1.json`, `run-v1.json` (DALA_Tekahn `excludeBosses`), `tavern-v1.json`, `curation.json`.
- Plugin: `DungeonRun.cs`, `DungeonRunData.cs`, `DungeonRunState.cs` (new append-only field `PowerChoices` with a
  ShouldSerialize guard, no save-version bump), `DungeonRunTavern.cs`, `DungeonRunValidator.cs`.
- Tool: `tools/restoration-ledger/Program.cs`.
- Existing suites re-pinned: c-acts, c-shrine-core, c-catalog, c-deckops, c-enc-model, c-w5fix, c-rules-05, c-rules-06,
  c-enc-07, c-enc-18, c-collectibles-A, and the bosspilot ACT_RUN labels in 7 harnesses. The base harness
  `p8f01/c-bosspilot-encounter` gained `DEFAULT_SWEEP`; its four generated copies (collect, rules05, rules06, enc09) were
  regenerated and carry the same unused hunk.
- Runners: `bp.sh`, `final.sh` (new stage `c25`), `suites.sh`, `builds.sh`, `runs.sh`, `build-all.sh` comments, and
  README section 4 (one paragraph).
- Generated files (ledger csv/md, `catalog-v1.json`, `dungeon-run-catalog.md`) were regenerated, never hand-edited.

### Things to know before the full regression

- **Most bosspilot records will move.** ULDA_038 Explorer Retraining now joins the tavern draw of every stamp-3 run
  (acts-v3h and acts-v3t). The harnesses buy every tavern offer, so a walk can also change the run's power. For example,
  finale25's DRUID plays druid_rootcall where enc18-finale's record played CS2_017. Every encounter mode that walks past
  a tavern on acts-v3h or acts-v3t will differ from its wave-6 record. Runs saved on older schedules are untouched (L1
  below). The released modes (run, act, fixture, catalog, shrine*) load data without encounters, so they stay on acts-v2.
- **The act I-III twist pools stay empty.** The chance per act and incompatible-modifier prevention are step 9. The 8
  twists stay partial with the blocker `run:twist-pools`, which the ledger describes. Only the Heist finale rolls a twist
  (DALA_830, chance 100).
- **The p8f18 and p8f24 finale fixtures are now the shipped `schedule-v1.json` byte for byte.** Their generators check
  that the shipped finale block equals the family's, then copy the file. So enc18-finale and tekahn24 now test the same
  schedules as finale25 and tekahn25. The p8f05/p8f06 twist fixtures were regenerated with the finales enabled; c-rules-05
  T16 and c-rules-06 W23 count act I-III twists only.
- `verify/tools/gen/p8f25/record_replay.sh` runs `git archive 68e7163` into the scratchpad (an export, not a worktree) to
  record the legacy-replay fixtures there.
- Harness lock: after each `gen.sh` run `dotnet build-server shutdown` inside the same locked command; a lingering
  compiler server holds the lock.

### Per id

| Id | Result |
|---|---|
| GILA_Darius_01-11, GILA_Toki_01-11 | done: each is its bucket's label row (support, role "bucket label", owner = the bucket's members); the 22 duplicate `label:` rows are gone (curation labelRecords) |
| DALA_BOSS_98h, DALA_BOSS_99h, ULDA_BOSS_99h | done: support portrait rows of Bob's tavern, not encounters (supportReclassifications) |
| DALA_740 | rejected: data gap (F8: the client data does not state the hidden effects' triggers or chances); missing with `ai:treasure-data-gap`; phase 9 rechecks it. Its tokens DALA_740ts-ts5 are support rows owned by DALA_740 and inherit the reason |
| ULDA_911 Kindle | done: tavern alias of DALA_911's removeFromSample (weight 0, its own id and cost); c-deckops T2, c-enc-25 A4 |
| ULDA_038 Explorer Retraining | done: tavern option, op `repickPower`, `"runs": "encounters"`. NewRun records the starting powers the row offered (`DungeonRunState.PowerChoices`), only on stamp-3 runs with two or more open powers; RetrainPowers lists those minus the current power's chain or override; RepickPower sets the power, clears any override and reschedules the power upgrade. c-deckops T2 (3 checks), c-enc-25 C1-C5 |
| ULDA_607 House Special | rejected (deferred): Bob's Bombastic Decks were server-side and no source lists them; missing with `data:bombastic-decks-absent` |
| ULDA_111, GILA_900p, GILA_911 | phase 9, each with its F15 reason |

### Finales and the same-character rule

- acts-v3h: enabled. Fixed bosses Norroa, Vargoth, Kalec, Khadgar on floors 16-19, after a tavern, twist DALA_830 at
  chance 100.
- acts-v3t: enabled on ulda_67_tekahn (floor 16) with `heroRelay`: the four explorers with three finale decks each, minus
  the run's own explorer.
- A DALA_Tekahn run: `run-v1.json` gives it `excludeBosses: [ulda_67_tekahn]`. DefaultSchedule leaves out any default
  whose finale fixes an excluded row (so it always follows acts-v3h and meets the Heist finale); NewRun refuses acts-v3t
  named for that row (`DungeonRun.ExcludedFinale`); the validator requires every hero row to keep one default open.
  c-w5fix Q7's tripwire is replaced by Q7-Q11 (600 default-roll runs never meet Tekahn; without the key some do; WARLOCK
  class-row runs meet him as often; the NewRun refusal; the validator negative).
- Bosspilot on the shipped schedule, fair AI as the boss, each twice identical with stderr 0: finale25 (8 games, boss
  wins 6), tekahn25 (2 games, boss wins 1; relay HERO_06>Elise>Finley>Brann>Reno), sweep25 a/b/c (16 games each, boss
  wins 12/13/11), all 0 faults, fallbacks, failed checks and swallowed exceptions. The sweep slices played 22 acts-v3h and
  26 acts-v3t runs across all six adventures, Tekahn on floor 16 included. (Stage B's values are in section 2.4.)

### Defaults and legacy runs

- `default` acts-v2 -> acts-v3h; `defaultsPending` -> `defaults` ["acts-v3h", "acts-v3t"] (a new run rolls one from its
  seed). New key `defaultWithoutEncounters` "acts-v2" applies only to data sets loaded without `encounters-v1.json`
  (tools and older suites, never the plugin). Ledger blocker `run:default-schedule` is gone from every row.
- c-enc-25 L1 replays 10 saves to their end and checks the state digest after every step (341 lines) against a
  recording made on the wave-6 tree: the 2 version-5 golden saves and 8 version-6 saves of new runs on the then-default
  acts-v2 (MAGE, WARRIOR, PRIEST, HUNTER, DALA_Tekahn, MAGE_ROGUE, TRLA_209h_Druid, GILA_600h). All identical. A mutation
  (DALA_911's tavern weight 3 -> 2) makes 321 lines differ.

### Shapes and golden sequences

- ReleasedShapes[3] acts-v3h: FF4FEC05953AA0DE -> 09B43E71F9440039 (the Heist finale: tavern plus 4 fights, floors
  16-19; family 18's predicted digest). acts-v3t: FF4FEC05953AA0DE -> A866AD976CEBBDC8 (the Tekahn finale: tavern plus
  floor 16).
- `golden-sequences.txt` 2700 -> 3500 lines: the 220 "(default)" new-run lines now follow acts-v3h (66) or acts-v3t (154)
  (the defaults flip); 800 new lines (acts-v3h and acts-v3t, 200 seeds x 2 unlock states); the other 2480 lines are
  byte-identical (digest F7CEE3DAF30CDC47), and the old 220 default lines equal today's acts-v2 new runs
  (ED4B7E96C3AA304A).
- c-enc-model 141 -> 144: C1, C5, C6, C9-C13 check the shipped finales; D1 checks 3500 lines with D1a/D1b pinning the
  legacy digest and the old default lines; D2 (default runs are stamp 3, data without encounters gives acts-v2); E1 (19
  bosses per run); E7; G3, G6, G14, G16, J1 count The Finale; I10 (an act III major win leads to the finale).

### Rejections audit (c-enc-25 B)

B1 pins the rows left missing (13) and partial (10); B2 requires a blocker on each and a ledger description of every
blocker named; B3 checks that every rejection the families reported is not implemented, has its reason in the notes and
is recorded in curation (Bolan 75h/75p, Phase Spider 61t, Cheerful Spirit DALA_501, Jythiros 58h/58p, Alternate Timeline
48t, GILA_900h2, DALA_857 Glittering, DALA_740, ULDA_607/111, GILA_900p/911, TRLA_209h, LOOTA_BOSS_53h2); B4 Toki's deck
and the Enhance-a-matic adaptation; B5 the stale F14 TRLA_065p note is deleted; B6 curation family E25 (7 ids, 30
support records, 5 rejections).

- Missing (13): DALA_501, DALA_740, DALA_857, DALA_BOSS_61t, DALA_BOSS_75h, DALA_BOSS_75p, GILA_900p, GILA_911,
  GILA_BOSS_48t, ULDA_111, ULDA_607, ULDA_BOSS_58h, ULDA_BOSS_58p.
- Partial (10): DALA_829, DALA_831, DALA_832, DALA_833, ULDA_801-804 (`run:twist-pools`), GILA_BOSS_43t
  (`run:hunt-reward`), GILA_BOSS_99t (`data:unused-in-adventure`).

(Stage B: B2's claim was not true for three blockers; section 2.1, finding 2.)

### Ledger counts

4992 rows / implemented 2814 / partial 817 / missing 302 / support 1059 -> 4970 / 3880 / 10 / 13 / 1067 / adapted 0:
22 `label:` rows removed; 1066 rows became implemented (807 partial, 259 missing: 1063 blocked only by the default
schedule, plus DALA_830, ULDA_038, ULDA_911); 30 missing rows became support (22 labels, 3 keepers, 5 tokens). Pinned
in c-catalog and c-deckops T5 (4970).

### Reachability (c-enc-25 R)

600 new runs on the default roll (every run row in turn, every unlock held: acts-v3h 327, acts-v3t 273), plus 600 with a
fresh profile.

| Adventure | Rows | Met | Meetings | Met (fresh profile) |
|---|---|---|---|---|
| DALA | 74 | 74 | 2743 | 74 |
| GIL | 47 | 47 | 1467 | 47 |
| LOOT | 48 | 48 | 1179 | 48 |
| TRL | 9 | 9 | 1414 | 9 |
| ULDA | 77 | 77 | 2022 | 77 |
| solostone | 20 | 20 | 1756 | 19 |

No boss row is unreachable with every unlock held (none fails for tier or finale-only). One solostone row sits behind an
unlock (R5). Six rows are excluded only for their own hero row (Barkeye, Vessina, Chu, Squeamlish, Tekahn, George; R6: 0
breaches in 100 runs each, other rows still meet them).

### Pins moved by stage A

c-acts A1/A2; c-shrine-core D5; c-catalog (two ledger checks); c-deckops 174 -> 179: T2 gained **5** checks (ULDA_911 in
the removeFromSample loop, the Kindle alias check, three Explorer Retraining checks; 24 options exercised) and T5 = 4970
(stage A's report said "T2 gained 3 checks": corrected here, finding 5); c-enc-model 141 -> 144; c-w5fix 29 -> 33 (Q2
includes Tekahn; Q7 -> Q7-Q11); c-rules-05 T16 and c-rules-06 W23 (the finale act's own twist excluded); c-enc-07 E11
(skips rows NewRun refuses on a schedule); c-enc-18 R1/R2/R6 (the shipped finale; R2 now requires zero validator errors,
the released digest 09B43E71F9440039 and fixture == shipped file); c-collectibles-A TRL_318 (names acts-v2); new suite
c-enc-25 (33 checks). Stage A's statement that no check was weakened was not true for c-enc-model C12 (finding 3).

### Stage A verification

Engine 0 warnings / 0 errors (md5 317cf8451d56e7eebcb20629de20708f); build-all 126/126 exit 0; gen.sh twice
byte-identical (encounters "255 rows, 296 decks (125 authentic, 171 changed), 876 adaptations, 4 rejected", catalog
"506 entries ... 6508 admitted and 1 excluded"); all 19 data-derived fixture generators plus record_replay.sh twice
byte-identical (28 outputs). Core suites: c-content 905, c-admission 4120, c-runlayer 244, c-acts 181, c-deckops 179,
c-catalog 127, c-shrine-core 131, c-enc-model 144, c-enc-engine 124, c-w4fix 4, c-w5fix 33, c-enc-25 33, all 0 failed;
every other suite once (c-rules-05 170, c-rules-06 224, c-enc-07 161, c-enc-18 109; randplay 1DE562A92D22C987).

Stage A commits: a2ef0c6, ec0fd4a, 8100344, 293e67e, e4dc074, 77cdd2d, f7f5fd2, ff3bba3, 537c99f.

## 2. Stage B: the close-out review (HANDOFF 3.3)

The review raised 7 findings; 5 were confirmed and 2 rejected. Every confirmed finding is fixed, each with a check that
fails without the fix (or, for added coverage, a mutation that the check catches). One fix commit: `387992f`, then one
commit with this report, a README note and a c-enc-25 check label.

### 2.1 Confirmed findings and their fixes

**Finding 1 [medium, fidelity]: four Tombs powers the wiki marks Auto-cast were optional AI clicks with no note.**
Sinkhole's Sand Surge (ULDA_BOSS_08p), Colossus of the Sun's Power of Wrath (22p), Sazin's Mighty Windstorm (28p) and
Runaway Ruby's Out of Control (33p): the wiki's Hero Power tables mark them Auto-cast and the client cards carry
AI_MUST_PLAY (tag 367), but they were registered with `Power(...)` and no row note said so.

- Fix: all four are cast by rule through family 09's `AutoCast` (at the start of the boss's turn, after the draw, with
  the old offer condition as the cast condition; never an option of the AI), as families 15-17 and 21-23 do with the same
  marker. The new check (below) found a fifth power in the same state, **Marei Loom's Next... (DALA_BOSS_66p)**: its
  wiki page (read with `?action=raw`, kept under `verify/.out/tmp/wiki/Marei_Loom.txt`) marks it Auto-cast, and its
  worth lies in hidden decks the AI cannot evaluate, so it is rule-cast too. I chose the rule cast over a "the AI
  chooses" note because the wave 5-6 families that share these waves follow the marker, and Sand Surge and Next... are
  exactly the case the design names for a rule cast (worth the AI's evaluation cannot see).
- Engine: `DungeonRunEncounters.F18.cs`, `F19.cs`, `F20.cs` (registrations and header comments).
- Admission: `DungeonRunAdmission.F18.cs` and `F19.cs` reasons; `F20.cs` has a new family `boss-tombs-b-rule-cast`
  (22p, 28p, 33p), taken out of `boss-tombs-b-board-power` and `boss-tombs-b-hidden-hand`.
- Notes: the five power adaptations in `reference/encounters/F18.json`, `F19.json` and `F20.json` and their generators
  `make_f18.py`, `make_f19.py` and `make_f20.py` (each run twice, output byte-identical to the edited JSON).
  `docs/design/dungeon-run.md` states the rule-cast policy and the three family paragraphs.
- Tests restaged so they exercise the cast through real turn starts: c-enc-18 F1-F7 (F7 calls the swap rule directly: an
  Echo copy lives only in the turn it was played, so the turn-start cast never meets one), c-enc-19 H1/H3/H7 and S5,
  c-enc-20 D1-D3, J1-J7, O1-O4, S5 and S6. The passive-registry lists of c-content and c-enc-model J13 include the five.
- New check c-enc-25 P1: every encounter power (forms included) whose client card carries AI_MUST_PLAY is either cast by
  rule or its row's power note says that the wiki's auto-cast is not followed (37 powers, 19 cast by rule after the fix).
  P2 pins the five. On the stage-A tree P1 and P2 fail, naming exactly dala_66_marei, ulda_08_sinkhole,
  ulda_22_colossus, ulda_28_sazin and ulda_33_ruby.
- Effect in play: the fair-AI bosspilot modes that meet these bosses (enc18a, enc19a, enc20a, enc20b; probed once each
  here, twice identical, 0 faults, fallbacks, failed checks and swallowed exceptions) and sweep25-a (it meets Sinkhole,
  who now loses that game) differ from their earlier records. The harness's `power` column counts clicks only, so it
  reads 0 for a rule-cast power; the suites count the casts.

**Finding 2 [medium, claims]: rejections audit B2 passed although three named blockers had no description.**
`ai:unused-card` (DALA_501, DALA_BOSS_61t), `ai:boss-rejected-data` (DALA_BOSS_75p) and `ai:treasure-data-gap`
(DALA_740) had an empty "What it needs" cell. B2 read only the key column.

- Fix: the ledger generator now describes an admission family whose only refusing rules carry a `none:` lift as
  "rejected for good (no phase owns a lift)", with each rule's reason, admission and lift. Ledger regenerated (rows and
  counts unchanged). B2 now reads the blocker table's fifth column and requires it to be non-empty for every blocker an
  open row names. On the stage-A ledger B2 fails, naming the four rows above.

**Finding 3 [medium, claims]: the close-out dropped the only check of per-fight finale Health.**
Wave 6's C12 asserted Health 75 on the second fight of a fixture finale; the rewrite checked only the phase, floor and
boss.

- Fix: c-enc-model C12b (the shipped [70] gives 70 on floors 16-19, Khadgar included), C12c (an edited
  `"bossHealth": [70, 75, 80, 85]` is read fight by fight: 70, 75, 80, 85) and C12d (a list shorter than the four fights,
  [70, 75], becomes its last value for every fight: 75 x4, the `Curve()` fill rule).
- Teeth: with `DungeonRun.BossHealth` reading `act.BossHealth[0]`, C12c fails (70,70,70,70); the reviewer's scratch root
  with the Heist finale at [70, 75, 80, 85] now fails C12b (it passed every suite before).
- Note: `Curve()` fills a list whose length is not the fight count with its last value for every fight, not "repeat the
  last value after the given ones". C12d pins that as it is; no shipped list is affected (the finales list one value).

**Finding 4 [low, claims]: the close-out's new and relaxed validator rules had no negative test.**

- Fix: c-enc-model B33-B38, each with its expected message: a tavern option with an unknown `runs` (ULDA_038 "bogus"); an
  alias that names runs (ULDA_911 "encounters"); `defaultWithoutEncounters` naming an unknown schedule (acts-v9) and one
  that needs the encounter file (acts-v3h); with the encounter file loaded, a finale boss that is not in the data
  (dala_99_nobody) and `heroRelay` on the Heist finale, whose bosses list no relay hero. A2 already holds the other side
  (the data without the encounter file validates with no error).
- Teeth: with each of the six errors removed from `DungeonRunValidator.cs`, B33-B38 all fail. The mutated files were
  restored from copies and the suites rebuilt.

**Finding 5 [low, claims]: the stage-A report misstated the c-deckops move.**
T2 gained 5 checks (ULDA_911 in the removeFromSample loop, the Kindle alias check, three Explorer Retraining checks), not
3; c-deckops 174 -> 179. Corrected in section 1 above.

### 2.2 Rejected findings

- **"Plague Lord phase powers and Gustave's Cull the Meek are left to the AI, unlike every other wave-5/6 family."**
  Rejected: the facts hold (wiki Auto-cast, AI_MUST_PLAY, clickable registrations: F24.cs 207, 216, 224, 255; F11.cs
  352), but the premise does not. Families 19 and 20 did the same (finding 1, now fixed), and F24 and F11 record their
  choice as a power adaptation in every row ("The wiki marks X as auto-cast. Here the boss's AI chooses...") following a
  documented precedent (families 01, 07, 08, 09, 11). After stage B, c-enc-25 P1 holds the rule the reviewer wanted: no
  auto-cast power is left to the AI without a note saying so.
- **"c-rules-05 T16 keeps its wave-4 lowered threshold (>= 15) although the rolls are now 57 and 71."** Rejected as a
  close-out defect: the slack dates from wave 5, when the counts rose from 25/20 to 57/71; stage A's edit (ff3bba3) only
  excluded the finale act's own twist and moved no number. The brief asks the close-out not to weaken checks, and none
  was weakened here. Restoring `> 20` and the comment is safe cleanup for a later change.

### 2.3 Pins moved by stage B

| Suite | Stage A | Stage B | Why |
|---|---|---|---|
| c-enc-model | 144 | 153 | B33-B38 and C12b-C12d added; J13's passive list holds the five rule-cast powers |
| c-enc-25 | 33 | 35 | B2 reads the description column; P1, P2 added |
| c-admission | 4120 | 4122 | the new admission rule `boss-tombs-b-rule-cast` (2 checks per rule) |
| c-enc-18 | 109 | 109 | F1-F7 restaged for the rule cast, M3's passive list |
| c-enc-19 | 115 | 115 | H1, H3, H7 and S5 restaged |
| c-enc-20 | 111 | 111 | D, J, O, S5 and S6 restaged |
| c-content | 905 | 905 | the passive-registry list holds the five |

Ledger counts are unchanged (4970 / 3880 / 10 / 13 / 1067 / 0). Ledger csv: ULDA_BOSS_22p, 28p and 33p move to
`ok:boss-tombs-b-rule-cast`. No check was weakened. The restaged checks keep every assertion of the old ones (Cost, effect,
fair view, "not cast" conditions) and add the rule-cast facts (registry passive, AI_MUST_PLAY, cast count, Mana paid);
the one behaviour no longer reachable in a game (an Echo copy in hand at the turn-start cast) is tested on the rule itself.

### 2.4 Stage-B verification (on `387992f`; the report commit after it changes only a c-enc-25 check label, rebuilt and rerun: 35 passed)

- Engine: 0 warnings, 0 errors (md5 1b2083a1ebedeac028f6f852d338ffb1; it moved with the F18-F20 engine edits).
- build-all: 126 of 126 entries exit 0 (engine, tool, 84 suites, randplay, 39 hall), no WARNING.
- gen.sh twice byte-identical: encounters "255 rows, 296 decks (125 authentic, 171 changed), 876 adaptations, 4
  rejected"; catalog "506 entries (452 offerable pool, 2 withdrawn, 52 fixed), 6508 admitted and 1 excluded member
  references"; ledger rows 4970, implemented 3880, partial 10, missing 13, duplicate/support 1067, adapted 0.
  `encounters-v1.json` changes only in the F18-F20 source digests, `curation.json` in the five power notes, the ledger in
  the three blocker descriptions and the admission families.
- The 19 data-derived fixture generators twice byte-identical; the 11 encounter fixtures change only in the F18-F20
  source digests. `record_replay.sh` was not rerun: it records on the wave-6 export, which stage B does not change, and
  c-enc-25 L1 passes against its recording.
- Core suites once, all 0 failed: c-content 905, c-admission 4122, c-runlayer 244, c-acts 181, c-deckops 179, c-catalog
  127, c-shrine-core 131, c-enc-model 153, c-enc-engine 124, c-w4fix 4, c-w5fix 33, c-enc-25 35; also c-enc-18 109,
  c-enc-19 115, c-enc-20 111; randplay digest 1DE562A92D22C987 (unchanged).
- The close-out modes, each twice identical, stderr 0, 0 faults / fallbacks / failed checks / swallowed:
  finale25 (8 games, boss wins 6) and tekahn25 (2, 1), byte-identical to stage A; sweep25-b (16, 13) and sweep25-c (16,
  11), byte-identical to stage A; sweep25-a (16, boss wins 11; stage A 12): the one line that moved is Sinkhole's (now
  boss -> player), finding 1's rule cast.
- Probes of the modes finding 1 touches (twice identical, 0 faults / fallbacks): enc18a (12 games, boss wins 7), enc19a
  (18, 11), enc20a (18, 15), enc20b (18, 13). Against their wave-6 records they differ by 24, 26, 22 and 18 lines; that
  mixes finding 1 with stage A's tavern change. enc18a's `power-unused` now lists dala_61_saelorn and dala_64_biggs, whose
  walks changed decks and powers through the tavern (stage A's ULDA_038, not finding 1): a diagnostic, not a failure.

### 2.5 The full regression

Queue: `/tmp/claude-0/-home-user-SoloStone-wip/17cd0c92-5ca0-5f23-99aa-03b6556206e0/scratchpad/c25/all.q` (run it with
`q.sh` in the same folder; generator `mkq.py` there; 72 commands). It starts with `build-all.sh`, so it rebuilds from the committed
tree, then runs every run entry twice in slices with `same.sh`, the close-out modes (finale25, tekahn25, sweep25 a/b/c),
the bp.sh calls of every `final.sh` stage (enc, rules06, cross06, released, released-all, w1, w2, rules05, cross05, w4,
w5, w6) and the hall (harness, 1-38 in slices, same). Expect the bosspilot records to move for the two reasons above (the
tavern's ULDA_038 and the five rule casts); a new baseline needs those differences read first.
