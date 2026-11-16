// Phase 8, family 21 (Tombs of Terror bosses C: seventeen encounters ULDA_BOSS_41-59 without 44 and 58): the admission rows of the
// powers it scripts (a method the table concatenates: DungeonRunAdmission.BuildRules). Every power is a boss's only. The family
// names are the family's own (boss-tombs-c-*), so family 19's boss-tombs-* counts do not move. Jythiros of Questions' Riddle of the
// Sphinx is rejected with its lift. The tests named in the reasons are the suite c-enc-21 and the enc21 bosspilot modes.
internal static partial class DungeonRunAdmission
{
    /// <summary>Family 21: the lift of Riddle of the Sphinx (a decision of the non-current player and a Counter for every card type).</summary>
    internal const string RiddleLift = "phase 9 (economy): a decision of the non-current player in the engine's decision loop, NativeMatch (the client prompt) "
        + "and the harnesses, and a Counter for minion, weapon, hero and location plays in PlayCard";

    private static AdmissionRule[] RulesF21() => new[]
    {
        new AdmissionRule
        {
            Family = "boss-tombs-c-board-power", Admit = AdmitScope.Boss, Lift = NoneBossPower,
            Reason = "a clickable Tombs of Terror boss hero power whose whole effect is public board and hero state the engine resolves: "
                + "What's in the Jar? summons a 1/1 copy of any minion for the boss, never of a run shrine (ULDA_BOSS_47p), Demon Emissary "
                + "summons a random scripted Demon of Cost 3 or less the run's generation filter allows (ULDA_BOSS_49p), Cavalry of "
                + "E.V.I.L. deals 1 damage to a minion and gives it Charge (ULDA_BOSS_54p), Fish Food restores 2 Health to every Murloc "
                + "(ULDA_BOSS_55p), Titan's Beast sets a minion's Attack and Health to 3 (ULDA_BOSS_57p) and Food Chain deals 2 damage to "
                + "a minion and restores 5 Health to the boss's hero if it dies (ULDA_BOSS_59p). A power that would do nothing is not "
                + "offered, by a condition that reads only the public boards; no hidden zone is read (c-enc-21 sections G, I, M, N, P, R; "
                + "the enc21 bosspilot modes)",
            Ids = new[] { "ULDA_BOSS_47p", "ULDA_BOSS_49p", "ULDA_BOSS_54p", "ULDA_BOSS_55p", "ULDA_BOSS_57p", "ULDA_BOSS_59p" },
        },
        new AdmissionRule
        {
            Family = "boss-tombs-c-own-zone-power", Admit = AdmitScope.Boss, Lift = NoneBossPower,
            Reason = "a clickable Tombs of Terror boss hero power that reads or writes only the boss's own hand and deck, which the boss "
                + "knows exactly: Running Wild draws a random minion of the boss's own deck and adds a second copy of a Beast "
                + "(ULDA_BOSS_41p), Sand Shapin' shuffles a plain copy of an enemy minion's card (the board is public; never a run shrine) "
                + "into the boss's own deck as a public shuffle, marked \"Summon when Drawn\" (ULDA_BOSS_42p, ULDA_BOSS_42e: drawn, it is "
                + "summoned and its holder draws), E.V.I.L. Conscription adds a random scripted Lackey behind the generation filter "
                + "(ULDA_BOSS_53p). Offered by the public counts of the boss's hand and deck only (c-enc-21 sections A, B, L; the enc21 "
                + "bosspilot modes)",
            Ids = new[] { "ULDA_BOSS_41p", "ULDA_BOSS_42p", "ULDA_BOSS_53p" },
        },
        new AdmissionRule
        {
            Family = "boss-tombs-c-hidden-steal", Admit = AdmitScope.Boss, Lift = NoneBossPower,
            Reason = "Jolene Knottley's Spoil the Ending, \"Copy a spell in your opponent's hand. It costs (2) less\": in the real game a new "
                + "copy of the card of a random spell of the run player's hand goes to the boss's hand (the boss learns exactly that card, "
                + "now its own); nothing links the copy to the source card, so the boss's fair view keeps the run player's hand masked, "
                + "and its search, which sees that hand as stand-ins, finds no spell to copy (it under-knows, never over-knows). Offered "
                + "by the public hand counts only (c-enc-21 section C; the enc21 bosspilot modes)",
            Ids = new[] { "ULDA_BOSS_43p" },
        },
        new AdmissionRule
        {
            Family = "boss-tombs-c-passive", Admit = AdmitScope.Boss, Lift = NoneBossPower,
            Reason = "a passive Tombs of Terror boss hero power started at match start (EncounterRules.ActivatePower) that reacts to a public "
                + "event: Reliquary's Relics copies a spell the run player casts into the boss's hand (ULDA_BOSS_45p; the cast is public), "
                + "Desert Warfare adds a random scripted Hunter card (no Quest, no Secret) after the boss's hero attacks (ULDA_BOSS_50p), "
                + "Tol'vir Necromancy restores a minion that was Reborn to full Health (ULDA_BOSS_51p, either player's), Sandy Surprise "
                + "gives Stealth to a 1-Cost minion the boss plays (ULDA_BOSS_56p) and Broken Phylactery doubles the boss's fatigue damage "
                + "(ULDA_BOSS_52p2, Lich Baz'hial's form 1; Draw.PreDrawPhase). The trigger is a public cast, attack, summon, play or "
                + "fatigue, never the identity of a hidden card (c-enc-21 sections D, J, K, O, H; the enc21 bosspilot modes)",
            Ids = new[] { "ULDA_BOSS_45p", "ULDA_BOSS_50p", "ULDA_BOSS_51p", "ULDA_BOSS_56p", "ULDA_BOSS_52p2" },
        },
        new AdmissionRule
        {
            Family = "boss-tombs-c-rule-cast", Admit = AdmitScope.Boss, Lift = NoneBossPower,
            Reason = "a Tombs of Terror boss hero power the wiki marks Auto-cast, cast by rule at the start of the boss's turn (family 09's "
                + "rule-cast powers; never an option of the boss's AI): Maw of the Desert destroys the minion of either board with the lowest "
                + "Health (ULDA_BOSS_46p; a run shrine goes dormant) and Arena Expert summons a random minion of each player's deck and the "
                + "boss's attacks the run player's (ULDA_BOSS_48p, Duel!'s rule: the summon makes each card public). The condition reads "
                + "only the public boards (c-enc-21 sections E, F; the enc21 bosspilot modes)",
            Ids = new[] { "ULDA_BOSS_46p", "ULDA_BOSS_48p" },
        },
        new AdmissionRule
        {
            Family = "boss-tombs-c-form-power", Admit = AdmitScope.Boss, Lift = NoneBossPower,
            Reason = "Lich Baz'hial's Lich's Phylactery, \"Restore @ Health to your Hero. Reduce by 1 for every friendly minion that dies\": @ "
                + "is the power's own public script number (20, lowered by each friendly minion's death); used once, it breaks: the boss "
                + "enters the row's script form 1 (power only: hero, Health, Armor and weapon stay), Broken Phylactery. Every fact is "
                + "public board and hero state; the form index and change count are the model's tags (c-enc-21 section H; the enc21 "
                + "bosspilot modes)",
            Ids = new[] { "ULDA_BOSS_52p1" },
        },
        new AdmissionRule
        {
            NeverGenerated = true,
            Family = "boss-tombs-c-rejected", Admit = AdmitScope.None, Lift = RiddleLift,
            Reason = "Jythiros of Questions' Riddle of the Sphinx, \"Guess which card the Boss will play to banish it\": the run player guesses "
                + "on the boss's turn among the boss's hand and its draw, and a right guess counters the boss's card of any type. The "
                + "engine's decision loop, NativeMatch and the harnesses answer only the current player's decisions, and the engine "
                + "counters spells only; the encounter is left out (reference/encounters/F21.json \"rejected\")",
            Ids = new[] { "ULDA_BOSS_58p" },
        },
    };
}
