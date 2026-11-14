# handoff/phase8: reference material from the original Windows PC

Read-only reference for the phase-8 work (see `HANDOFF.md` at the repository root). `hrun.sh`, `lanes.py`, `lanes.json`,
`LANE-GUIDE.md` and `workflow-encounters-lanes.js` are tools for the original PC only: they hold that PC's absolute
paths (`C:\SoloStone`, the session scratchpad under `C:\Users\user\AppData\Local\Temp\claude\...`) and must not be run
in the cloud or in any other clone. The reports quote the same paths; read `C:\SoloStone` as the repository root and
`...\scratchpad\<folder>` as `verify/suites/<folder>`, `verify/fixtures/<folder>` or `verify/baseline` (`verify/MANIFEST.md`).

- `inventory.json`: the 25 encounter families (ids, notes) and the inventory report.
- `reports/`: the phase-8 design, the encounter model, the family reports and the three wave integrations
  (`integrate-wave3.md` is the current baseline, carried into `verify/README.md` section 4).
- `workflow-encounters-lanes.js`: the old orchestration: lane rules, waves, close-out and review prompts.
