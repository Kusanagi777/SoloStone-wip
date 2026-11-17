# Step 9: uncalibrated initial values

Under the scope change (implementation only) no value below was measured. Each is the plan's stated value or one justified
from the data, with its source; the user's calibration (plan stage 4) may move it. One section per lane.

## Lane R (prevention, ordering, twists)

| Value | Where | Initial value | Source |
|---|---|---|---|
| Twist chance per act, acts-v4h/v4t acts I-III | `handoff/phase9/twist-pools.json` (placed by the integrator in lane E's schedule objects) | 100 | Plan D8: in the originals each chapter's twist was always on (`tools/restoration-ledger/curation.json` notes `twists-per-act`, `plague-twists-per-act`). Lowered only if a measured twist-on/off delta pushes a slot out of its B band. |
| Twist chance, acts-v4h finale (DALA_830) | same | 100 | acts-v3h's finale value, unchanged. |
| Twist weights | `schedule-v1.json` `rules[].weight` | 1 for every twist (unchanged) | The shipped weights; D8 only makes the v4 pick honour them. |

No other lane R value is numeric: the exclusions, flags and v4 modifier lists are derived from the rows' data and the plan's
exclusion list, not from measurement.

## Lane X (match ends, restarts and finales)

| Value | Where | Initial value | Source |
|---|---|---|---|
| Relay fight turn cap (Tekahn on acts-v4t) | `DungeonRun.RelayTurnCap` | 150 | plan D11 (B7 measures "turn caps ≤ 3 of 100") |
| Restart turn budget per floor | `DungeonRun.RestartTurnBudget` | 2 × the fight's turn cap | plan D13 |
| Restarts per source per floor | `DungeonRun.RestartsPerSource` | 1 (Rewind 1, Alternate Timeline 1) | plan D13 |
| ULDA_111 / GILA_911 rarity in the rows handed to lane E | `handoff/phase9/lane-x.md` section 2 | rare / rare | none in the client data (no rarity tag); lane E decides |
