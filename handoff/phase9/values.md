# Step 9: uncalibrated initial values

The scope change leaves every measurement to the user. Each value below is the plan's stated value or one justified from
the data, not a measured one. One section per lane.

## Lane X (match ends, restarts and finales)

| Value | Where | Initial value | Source |
|---|---|---|---|
| Relay fight turn cap (Tekahn on acts-v4t) | `DungeonRun.RelayTurnCap` | 150 | plan D11 (B7 measures "turn caps ≤ 3 of 100") |
| Restart turn budget per floor | `DungeonRun.RestartTurnBudget` | 2 × the fight's turn cap | plan D13 |
| Restarts per source per floor | `DungeonRun.RestartsPerSource` | 1 (Rewind 1, Alternate Timeline 1) | plan D13 |
| ULDA_111 / GILA_911 rarity in the rows handed to lane E | `handoff/phase9/lane-x.md` section 2 | rare / rare | none in the client data (no rarity tag); lane E decides |
