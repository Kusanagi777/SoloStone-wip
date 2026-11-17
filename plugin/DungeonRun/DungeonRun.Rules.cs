using System.Collections.Generic;

// Step 9 lane R (handoff/phase9/plan.md section 3, stage 2): incompatibility prevention. Stage 0 fixes the call sites; every
// method here changes nothing yet. Lane R edits only this file for its run-layer logic (plus the hooks the plan names).
internal sealed partial class DungeonRun
{
    /// <summary>
    /// D7: removes, for the fight against <paramref name="boss"/> only, the knobs of <paramref name="modifier"/> that the boss
    /// row's v4 "incompatibleModifiers" names (read through <see cref="Tuned"/>). Called by <see cref="KnobsFor"/> once per
    /// active modifier, before the sum. Stage 0: changes nothing.
    /// </summary>
    private void NeutraliseKnobs(DungeonRunState state, DungeonRunBoss boss, DungeonRunModifier modifier, DungeonRunKnobs knobs)
    {
    }

    /// <summary>
    /// C1, D17: every forbidden pair the state holds (twists, the act anomaly, run-scope anomalies, modifiers, the act's boss
    /// flags and rules, the run class, the fight-only hero classes, treasures, the board shrine and the run deck's opening-flag
    /// cards), one line each; empty when there is none. A pure predicate, order-independent, checked by the tests at every step.
    /// Stage 0: none.
    /// </summary>
    internal List<string> ForbiddenPairs(DungeonRunState state) => new List<string>();
}
