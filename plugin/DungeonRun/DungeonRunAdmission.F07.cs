// Phase 8, family 07 (Dungeon Run bosses A: the 18 Kobolds and Catacombs encounters with a simple clickable hero power): the
// admission rows of the cards it scripts (a method the table concatenates: DungeonRunAdmission.BuildRules). Tests: the
// scratchpad suite c-enc-07 (each power's text in engine games through DungeonRunSetup, the shrine rules, the shipped rows)
// and the bosspilot modes enc07a and enc07b (every row played with the fair AI as the boss).
internal static partial class DungeonRunAdmission
{
    private static AdmissionRule[] RulesF07() => new[]
    {
        new AdmissionRule
        {
            Family = "boss-power-public", Admit = AdmitScope.Boss, Lift = NoneBossPower,
            Reason = "a clickable Dungeon Run boss hero power that works on public zones only (the boards, the heroes, the boss's own Mana), "
                + "so the fair AI sees it as an ordinary option and the engine resolves it from what both sides see: Sculpt Wax and "
                + "Harden Sculpture summon a copy (1/1, or as it is) of a minion of either board (LOOTA_BOSS_04p, LOOTA_BOSS_27p; a copy "
                + "of a run shrine is a plain minion), Unstable Explosion and Unstable Demolition damage 2 / 3 different random enemies "
                + "(LOOTA_BOSS_05p, LOOTA_BOSS_28p), Evolvomancy and Greater Evolution transform a minion into a random scripted minion "
                + "that costs (1) / (3) more, from the generation pool both sides' filters allow (LOOTA_BOSS_06p, LOOTA_BOSS_29p; a run "
                + "shrine is destroyed instead and goes dormant: ShrineRules.InterceptTransform), Freeze (LOOTA_BOSS_09p), Light's Will "
                + "heals every minion 2 (LOOTA_BOSS_10p), Cruel Words and Searing Lash deal 1 damage to a minion of the boss and give "
                + "it +2 / +5 Attack (LOOTA_BOSS_11p, LOOTA_BOSS_30p), Charge! (LOOTA_BOSS_12p), Rat Race summons two 1/1 Rats "
                + "(LOOTA_BOSS_18p), Swallow Whole destroys the minion with the highest Attack of both boards, one at random among "
                + "equals, never a dormant one, and is offered only while such a minion is on a board (LOOTA_BOSS_21p), Baby Breath "
                + "deals 2 damage (LOOTA_BOSS_44p) and Coin gives the boss 1 Mana Crystal this turn (LOOTA_BOSS_45p). Every random "
                + "pick uses the game RNG and is reported as a random event; no hidden zone is read or written "
                + "(c-enc-07 A1-A30, D1-D11, E3, E5, the enc07a and enc07b bosspilot modes)",
            Ids = new[] { "LOOTA_BOSS_04p", "LOOTA_BOSS_05p", "LOOTA_BOSS_06p", "LOOTA_BOSS_09p", "LOOTA_BOSS_10p", "LOOTA_BOSS_11p", "LOOTA_BOSS_12p",
                "LOOTA_BOSS_18p", "LOOTA_BOSS_21p", "LOOTA_BOSS_27p", "LOOTA_BOSS_28p", "LOOTA_BOSS_29p", "LOOTA_BOSS_30p", "LOOTA_BOSS_44p", "LOOTA_BOSS_45p" },
        },
        new AdmissionRule
        {
            Family = "boss-power-own-hand", Admit = AdmitScope.Boss, Lift = NoneBossPower,
            Reason = "a clickable Dungeon Run boss hero power that writes the boss's own hand, which the boss knows exactly and the run "
                + "player sees as a count: Tactical Retreat returns a minion of the boss's board to its hand (LOOTA_BOSS_13p: a public "
                + "minion moves, the engine's bounce; a full hand destroys it), Hunter's Call makes every card the boss holds cost "
                + "(1) less (LOOTA_BOSS_23p, shown as BRM_028e) and Gloop gives every minion it holds +1/+1 (LOOTA_BOSS_24p, "
                + "LOOTA_BOSS_24e). The run player's hidden zones are neither read nor written, and the boss's deck is untouched, so "
                + "its own-deck ledger needs no fact (c-enc-07 B1-B9, E3, E5, the enc07a and enc07b bosspilot modes)",
            Ids = new[] { "LOOTA_BOSS_13p", "LOOTA_BOSS_23p", "LOOTA_BOSS_24p" },
        },
        new AdmissionRule
        {
            Family = "boss-token", Admit = AdmitScope.Boss, Lift = NoneBossOnly,
            Reason = "a boss-only minion with public board behaviour: the 1/1 Rat that Rat Race summons (LOOTA_BOSS_18t, no text) and "
                + "Growing Ooze, a Frozen Throne adventure minion The Mothergloop's deck lists (ICCA07_004: +1/+1 at the start of its "
                + "owner's turn, Shade of Naxxramas's rule). No generation pool holds either (c-enc-07 A22, C1-C3, E15, the "
                + "enc07a and enc07b bosspilot modes)",
            Ids = new[] { "LOOTA_BOSS_18t", "ICCA07_004" },
        },
    };
}
