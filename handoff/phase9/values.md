# Step 9: uncalibrated initial values

Under the scope change (implementation only; the user runs every test and measurement), no value below comes from a
measurement. Each is an **uncalibrated initial value**: the plan's stated initial guess, or a value derived from the data
or docs, with its source. Stage 4 (calibration) may move any of them; all are read only on a run of schedule stamp 4
(`DungeonRun.Tuned`: acts-v4h, acts-v4t), so no released schedule moves with them.

## Lane E (economy and scaling)

| Key | Where | Value | Source |
|---|---|---|---|
| draw | acts-v4h, acts-v4t | `provenance-mix-v2` | plan D10 |
| drawTuning.alphaPermille | acts-v4h, acts-v4t | 700 (alpha 0.7) | plan D10 initial guess; (34/2)^(1-0.7) = 2.34, within the A10 bound 2.5 (largest t3 provenance DALA 34, smallest TRL 2) |
| drawTuning.drawnPenaltyPermille | acts-v4h, acts-v4t | 1000 (k 1) | plan D10 initial guess |
| drawTuning.ledgerMajorChance | acts-v4h, acts-v4t | 50 (percent) | plan D10 initial guess (the v1 draw takes the banked lord always) |
| drawTuning.maxTrlPerAct | acts-v4h, acts-v4t | 1 | plan D10 guard ("at most 1 TRL champion per act") |
| upgradeEveryFloors | acts-v4h, acts-v4t | 4 | plan A8 (charm_seedling 8 floors to full growth; rules value 2 gives 4) |
| treasureRarity act I | acts-v4h (shared by acts-v4t) | rare 26 : common 74 | A6 midpoint 30% rare. Pooled over the 116 loadouts, the audited v4 candidate pool holds 11604 rare and 9416 common candidate slots (R/C = 1.232); weights w_r/w_c = s/(1-s) x C/R give 0.348 for s = 0.30 |
| treasureRarity act II | acts-v4h | rare 39 : common 61 | A6 midpoint 44%: 0.786 x 0.812 = 0.638 |
| treasureRarity act III | acts-v4h | rare 53 : common 47 | A6 midpoint 58%: 1.381 x 0.812 = 1.121 |
| bundleAfterTreasure | acts-v4h acts II and III | false | plan D5 variant (b), the stated preference (no bundle after a treasure-draft floor or the major in acts II-III; takes stay 3/2/2) |
| act curves (bossHealth, bossMana) | acts-v4h acts I-III | v3's values | no measurement: acts-v3h's released curves are the starting point (plan D4 levers come in stage 4) |
| finale bossHealth / bossMana | acts-v4h (Heist) | [70, 70, 70, 70] / [3, 3, 3, 3] | v3's single values written per fight (C12c's per-fight shape, values unchanged) |
| finale bossHealth / bossMana | acts-v4t (Tekahn) | [70] / [3] | v3's values |
| twist pools | acts-v4h acts I-III | I [DALA_829, ULDA_801], II [DALA_832, ULDA_802], III [DALA_833, DALA_831, ULDA_803, ULDA_804] | plan D8 (chapter-mapped) |
| twist chance | acts-v4h acts I-III | 100 | plan D8 (in the originals each chapter's twist was always on) |
| finale twist | acts-v4h | DALA_830, 100 | v3's value |
| finale turnCap | acts-v4t | 150 | plan D11 |
| finale formRatchet | acts-v4t | true | plan D11 |
| offsetCap | none | absent | plan D4: the last lever, not used |
| tavern weightByAct | every offered option | [3, 3, 3] | plan A9: one weight for every option, 14 Heist : 8 Tombs options give about 64 : 36 |
| tavern weightByAct | DALA_902 Remove a Card | [3, 3, 5] | plan D5 |
| tavern maxPerRun | DALA_904 Good Food | 2 | plan D4 (DALA_904 capped at 2; CapKnobs caps HealthBonus at 2 x 5 = 10) |
| encounter v4 reward | gila_43_damph | GILA_BOSS_43t | plan D9 |
| BankedGold per skipped bundle (for room) or reward with no room | run layer | 1 | plan D5, D9 |

Not set (left to stage 4, which is the user's): encounter v4 `tier` and `healthOffset` overrides (D4 levers 1-2). The
mechanism is in place (`DungeonRunBossV4`, read by the v2 draw and `CapKnobs`); no row carries one.
