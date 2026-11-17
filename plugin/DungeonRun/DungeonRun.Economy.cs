// Step 9 lane E (handoff/phase9/plan.md section 3, stage 2): the reward economy and act-aware scaling. Stage 0 fixes the call
// sites; every method here changes nothing yet. Lane E edits only this file for its run-layer logic (plus the hooks the plan
// names).
internal sealed partial class DungeonRun
{
    /// <summary>
    /// D4: caps the summed knobs of one fight (the DALA_904 count from HealthBonus, an optional offsetCap), read through
    /// <see cref="Tuned"/>. Called by <see cref="KnobsFor"/> after the sum. Stage 0: changes nothing.
    /// </summary>
    private void CapKnobs(DungeonRunState state, DungeonRunBoss boss, DungeonRunKnobs knobs)
    {
    }

    /// <summary>
    /// D9: the won boss's own reward (an encounter row's v4 "reward": GILA_BOSS_43t for Damph), granted after an act run's won
    /// fight that is neither the last node nor a finale fight, before the treasure draft (RecordActWin). Stage 0: grants nothing.
    /// </summary>
    private void GrantEncounterReward(DungeonRunState state, DungeonRunNode node)
    {
    }

    /// <summary>
    /// D5: an act run's bundle draft skipped because the deck has no room (deckMax) may become banked gold
    /// (<see cref="DungeonRunState.BankedGold"/>), read through <see cref="Tuned"/>. Called by OpenBundleDraft on that skip
    /// only. Stage 0: changes nothing.
    /// </summary>
    private void ConvertSkippedDraft(DungeonRunState state)
    {
    }
}
