# verify/lib/suites.sh: the suite registry, sourced by the runners. Paths are relative to verify/suites.
# The order is the wave-3 order (scratchpad p8w3/builds.sh and runs.sh), so entry numbers match the integration report.

# The build stage: 67 suite projects (builds.sh <from> <to>); randplay and the catalog tool are built by their own names.
BUILD_LIST="audit-verify/c-runlayer audit-verify/c-catalog audit-verify/c-content audit-verify/c-admission
 audit-verify/c-secretscan audit-verify/c-secretsearch audit-verify/c-bosspilot audit-verify/c-acts audit-verify/c-deckops
 audit-verify/selfplay audit-verify/c-deferrals
 p6f15/c-dkdh-supply p6f15/c-heroes p6f15/c-heroes-F14 p6f15/c-heroes-F6 p6f15/c-heroes-F9 p6f15/c-runctx
 p6f15/c-starters-F10 p6f15/c-treasures-F1 p6f15/c-treasures-F11 p6f15/c-treasures-F12 p6f15/c-treasures-F13
 p6f15/c-treasures-F2 p6f15/c-treasures-F3 p6f15/c-treasures-F4 p6f15/c-treasures-F5 p6f15/c-treasures-F7
 p6f15/c-treasures-F8 p6f15/c-upgrades
 p6f16/c-dkdh-play p6f18/c-review-F18
 p7/c-shrine-core p7s1/c-shrines-S1 p7s2/c-shrines-S2 p7s3/c-shrines-S3 p7u/c-shrine-upgrades p7t1/c-teammates-T1
 p7t2/c-teammates-T2 p7d/c-companions-D p7f/c-p7fix
 p8f01/c-enc-model p8f01/c-enc-engine p8f01b/c-enc-secrets p8f01/c-bosspilot-encounter
 p7/c-bosspilot-shrine p7s2/c-bosspilot-shrine-S2 p7s3/c-bosspilot-shrine-S3 p7u/c-bosspilot-shrine-U
 p7t1/c-bosspilot-teammates-T1 p7t2/c-bosspilot-teammates-T2 p7d/c-bosspilot-companions-D
 p8f02/c-collectibles-A p8f02/c-bosspilot-collect p8f03/c-collectibles-B p8f04/c-collectibles-C p8f04/c-bosspilot-f04
 p8w1/c-w1fix
 p8f05/c-rules-05 p8f05/c-bosspilot-rules05 p8f07/c-enc-07 p8f08/c-enc-08
 p8f06/c-rules-06 p8f06/c-bosspilot-rules06 p8f09/c-enc-09 p8f09/c-bosspilot-enc09 p8f10/c-enc-10 p8w3/c-w3fix"

# The run stage: 54 entries (runs.sh <k> <from> <to>). How each one is run is in runs.sh; most are plain.
RUN_LIST="audit-verify/c-runlayer audit-verify/c-catalog audit-verify/c-content audit-verify/c-admission
 audit-verify/c-acts audit-verify/c-deferrals audit-verify/c-deckops
 p6f15/c-dkdh-supply p6f15/c-heroes p6f15/c-heroes-F14 p6f15/c-heroes-F6 p6f15/c-heroes-F9 p6f15/c-runctx
 p6f15/c-starters-F10 p6f15/c-treasures-F1 p6f15/c-treasures-F11 p6f15/c-treasures-F12 p6f15/c-treasures-F13
 p6f15/c-treasures-F2 p6f15/c-treasures-F3 p6f15/c-treasures-F4 p6f15/c-treasures-F5 p6f15/c-treasures-F7
 p6f15/c-treasures-F8 p6f15/c-upgrades
 p6f16/c-dkdh-play p6f18/c-review-F18
 p7/c-shrine-core p7s1/c-shrines-S1 p7s2/c-shrines-S2 p7s3/c-shrines-S3 p7u/c-shrine-upgrades p7t1/c-teammates-T1
 p7t2/c-teammates-T2 p7d/c-companions-D p7f/c-p7fix
 p8f01/c-enc-model p8f01/c-enc-engine p8f01b/c-enc-secrets
 p8f02/c-collectibles-A p8f03/c-collectibles-B p8f04/c-collectibles-C p8w1/c-w1fix
 p8f05/c-rules-05 p8f07/c-enc-07 p8f08/c-enc-08 p8f06/c-rules-06 p8f09/c-enc-09 p8f10/c-enc-10 p8w3/c-w3fix
 audit-verify/c-secretscan audit-verify/c-secretsearch audit-verify/selfplay audit-verify/randplay"

# The audit hall (hall.sh): the base harness, then the 38 audit harnesses in this order (hall.sh <from> <to>).
HALL_BASE="hall/audit-harness"
HALL_LIST="audit-chunk-1 audit-chunk-10 audit-chunk-11 audit-chunk-12 audit-chunk-13 audit-chunk-14 audit-chunk-15
 audit-chunk-16 audit-chunk-17 audit-chunk-18 audit-chunk-19 audit-chunk-2 audit-chunk-3 audit-chunk-4 audit-chunk-5
 audit-chunk-6 audit-chunk-7 audit-chunk-8 audit-chunk-9
 audit-fix-Amirdrassil_cs audit-fix-BoomsdayCardsGen_cs audit-fix-DalaranCardsGen_cs audit-fix-EruptingVolcano_cs
 audit-fix-Expert1CardsGen_cs audit-fix-Generic_cs audit-fix-GvgCardsGen_cs audit-fix-HideousHusk_cs
 audit-fix-IcecrownCardsGen_cs audit-fix-LadyAzshara_cs audit-fix-LoeCardsGen_cs audit-fix-LootapaloozaCardsGen_cs
 audit-fix-NaxxCardsGen_cs audit-fix-TgtCardsGen_cs audit-fix-TrollCardsGen_cs audit-fix-UnderbellyNetwork_cs
 audit-fix-UngoroCardsGen_cs audit-fix-_Game_DeathProcessingAndAuraUpdate__ audit-fix-run_v1_json"

# The pilot set (ported and proved first; see README.md).
PILOT_LIST="audit-verify/c-runlayer audit-verify/c-catalog audit-verify/c-content audit-verify/c-admission
 audit-verify/c-acts audit-verify/c-deckops p7/c-shrine-core p8f01/c-enc-model p8f01/c-enc-engine
 p8f01/c-bosspilot-encounter audit-verify/selfplay audit-verify/randplay"
