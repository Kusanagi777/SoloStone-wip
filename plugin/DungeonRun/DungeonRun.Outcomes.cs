using System.Collections.Generic;
using SabberStoneCore.CardSets.DungeonRun;

// Step 9 lane X (handoff/phase9/plan.md section 3, stage 2): match ends, restarts and finales. Stage 0 fixes the call sites;
// every method here changes nothing yet. Lane X edits only this file for its run-layer logic (plus the hooks the plan names).
internal sealed partial class DungeonRun
{
    /// <summary>
    /// D13, D14: an escape (ULDA_111: <see cref="DungeonRunOutcome.Win"/> with RunMatchEnd.Escaped) or a restart (GILA_911,
    /// GILA_BOSS_48t: <see cref="DungeonRunOutcome.Fault"/> with RunMatchEnd.Restarted) takes its own branch: true when it
    /// recorded the outcome (RecordOutcome then returns <paramref name="notes"/>), false to go on to the fault, loss and win
    /// paths. Called by RecordOutcome after the match summary is added, before any other branch. Stage 0: always false.
    /// </summary>
    private bool EscapeOrRestartBranch(DungeonRunState state, DungeonRunProfile profile, DungeonRunOutcome outcome, RunMatchResult result,
        RunMatchEnd end, DungeonRunMatchSummary summary, List<string> notes) => false;

    /// <summary>
    /// D11, D14: the match-plan knobs of a tuned run (<see cref="Tuned"/>): RunMatchPlan.TurnCap, FormRatchet and
    /// EscapeAllowed. Called at the end of BuildPlan. Stage 0: the plan keeps its defaults (100, false, false).
    /// </summary>
    private void PlanMatchEnds(DungeonRunState state, DungeonRunBoss boss, RunMatchPlan plan)
    {
    }

    /// <summary>
    /// D13: the engine seed of a restarted fight (StableSeed of the run seed, MatchStream, the floor and
    /// <see cref="DungeonRunState.RestartIndex"/>, only when that is above 0), or null to keep the floor's seed (MatchSeed).
    /// Stage 0: always null.
    /// </summary>
    private int? RestartMatchSeed(DungeonRunState state) => null;
}
