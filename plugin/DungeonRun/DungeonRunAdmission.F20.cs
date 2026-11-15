// Phase 8, family 20 (Tombs of Terror bosses B: eighteen encounters ULDA_BOSS_19-36, and Sothis's Mogu Cultist ULD_705 with
// Highkeeper Ra ULD_705t): the admission rows of the cards it scripts (a method the table concatenates:
// DungeonRunAdmission.BuildRules). The hero powers are a boss's only; Mogu Cultist and Highkeeper Ra are a Saviors of Uldum
// collectible and its token, admitted in every scope. The family names are the family's own (boss-tombs-b-*, tombs-b-*), so
// they never move family 19's counts. The tests named in the reasons are the suite c-enc-20 and the enc20 bosspilot modes.
internal static partial class DungeonRunAdmission
{
    /// <summary>A lift that names no pending work: the row admits every scope.</summary>
    private const string NoneAdmittedF20 = "none: admitted in every scope";

    private static AdmissionRule[] RulesF20() => new[]
    {
        new AdmissionRule
        {
            Family = "boss-tombs-b-board-power", Admit = AdmitScope.Boss, Lift = NoneBossPower,
            Reason = "a clickable Tombs of Terror boss hero power whose whole effect is public board and hero state the engine resolves: "
                + "Titan Ritual summons two 1/1 Mogu Cultists (ULDA_BOSS_19p), Power of Wrath deals 1 damage to every damaged minion "
                + "(ULDA_BOSS_22p), Beetle Stampede summons a Plated Beetle (ULDA_BOSS_25p), Gatling Wand deals 2 damage split among "
                + "the enemy minions (ULDA_BOSS_26p), Pillaged Relics gives a chosen minion +1/+1, Divine Shield or Taunt at random "
                + "(ULDA_BOSS_31p), Whispering Sentinel summons a 1/1 Wisp with Lifesteal (ULDA_BOSS_32p), Out of Control deals 1 "
                + "damage to the center-most minions of each board (ULDA_BOSS_33p), Tomb Boomin' deals 4 damage split randomly between "
                + "all characters (ULDA_BOSS_34p) and Vengeful Construct destroys a chosen minion with 2 or less Health and its "
                + "neighbours (ULDA_BOSS_35p; a run shrine goes dormant as from any destroy). Every random pick uses the game RNG; a "
                + "dormant run shrine is never touched. A power that would do nothing is not offered, by a condition that reads only "
                + "the public boards; no hidden zone is read (c-enc-20 sections A, D, G, H, M, N, O, P, Q; the enc20 bosspilot modes)",
            Ids = new[] { "ULDA_BOSS_19p", "ULDA_BOSS_22p", "ULDA_BOSS_25p", "ULDA_BOSS_26p", "ULDA_BOSS_31p", "ULDA_BOSS_32p", "ULDA_BOSS_33p",
                "ULDA_BOSS_34p", "ULDA_BOSS_35p" },
        },
        new AdmissionRule
        {
            Family = "boss-tombs-b-own-hand-power", Admit = AdmitScope.Boss, Lift = NoneBossPower,
            Reason = "a clickable Tombs of Terror boss hero power that writes the boss's own hand, which the boss knows exactly and the run "
                + "player sees as a count: Hatching Time reduces the Cost of the Beasts in the boss's hand by (1) (ULDA_BOSS_23p) and "
                + "Corrupted Oasis adds two 2/2 Treants to it (ULDA_BOSS_24p). Offered while the boss holds a Beast or has room; the "
                + "run player's hidden zones are neither read nor written (c-enc-20 sections E, F; the enc20 bosspilot modes)",
            Ids = new[] { "ULDA_BOSS_23p", "ULDA_BOSS_24p" },
        },
        new AdmissionRule
        {
            Family = "boss-tombs-b-hidden-hand", Admit = AdmitScope.Boss, Lift = NoneBossPower,
            Reason = "a clickable Tombs of Terror boss hero power whose text reads the run player's hand: \"Retired\" Treasure Hunter "
                + "copies a random card of it into the boss's hand (ULDA_BOSS_27p; the boss legitimately learns that one card, which "
                + "the run player sees copied) and Mighty Windstorm makes each player swap a random minion of their hand with a random "
                + "minion of their board (ULDA_BOSS_28p; the run player's hand minion becomes public as it is summoned, the board minion "
                + "goes back to its owner's hand in view; a run shrine is never chosen). In the boss's fair-view projection the run "
                + "player's hand holds masked cards, so there each power uses only cards the boss already saw go into that hand: the "
                + "search never reads the hidden hand, and the real pick is a random event it re-plans after. Offered by public counts "
                + "and the boss's own hand and board only (c-enc-20 sections I, J; the enc20 bosspilot modes)",
            Ids = new[] { "ULDA_BOSS_27p", "ULDA_BOSS_28p" },
        },
        new AdmissionRule
        {
            Family = "boss-tombs-b-forced-attack", Admit = AdmitScope.Boss, Lift = NoneBossPower,
            Reason = "Kasa Softspeaker's Madness Incarnate, \"Gain +1 Attack this turn and attack 3 different random enemy minions\": "
                + "the boss's hero gets +1 Attack this turn (ULDA_BOSS_36e), then attacks up to three different random enemy minions "
                + "(no Stealth, no Immune) one after another as forced attacks, with their triggers, damage and the hero's weapon "
                + "Durability; it stops when a hero dies. The game RNG picks; public board state only, offered while there is such a "
                + "minion (c-enc-20 section R; the enc20 bosspilot modes)",
            Ids = new[] { "ULDA_BOSS_36p" },
        },
        new AdmissionRule
        {
            Family = "boss-tombs-b-passive", Admit = AdmitScope.Boss, Lift = NoneBossPower,
            Reason = "a passive Tombs of Terror boss hero power started at match start (EncounterRules.ActivatePower) that reacts to a "
                + "public event: Spectral Swordsman gives the boss's hero Stealth while it has a weapon equipped (ULDA_BOSS_20p; set on "
                + "an equip, cleared when the last weapon leaves), Staking A Claim gives every minion +1/+1 whenever either player plays "
                + "a Discover card (ULDA_BOSS_21p), Mother of Sand summons a Dust Devil after the boss Overloads (ULDA_BOSS_29p) and "
                + "Fireshaper deals 2 damage randomly split among the enemies after the boss casts a spell (ULDA_BOSS_30p). The trigger "
                + "is a public play, cast, Overload or weapon change, never the identity of a hidden card (c-enc-20 sections B, C, K, L; "
                + "the enc20 bosspilot modes)",
            Ids = new[] { "ULDA_BOSS_20p", "ULDA_BOSS_21p", "ULDA_BOSS_29p", "ULDA_BOSS_30p" },
        },
        new AdmissionRule
        {
            Family = "tombs-b-collectible", Admit = AdmitScope.Any, Lift = NoneAdmittedF20,
            Reason = "Mogu Cultist (ULD_705), \"Battlecry: If your board is full of Mogu Cultists, sacrifice them all and summon Highkeeper "
                + "Ra\", and Highkeeper Ra (ULD_705t), \"At the end of your turn, deal 20 damage to all enemies\": the Battlecry reads "
                + "only its owner's board (seven Mogu Cultists are destroyed, then the 20/20 is summoned) and Ra's damage hits the "
                + "public enemy characters; no hidden zone, no generation (c-enc-20 section A; the enc20 bosspilot modes)",
            Ids = new[] { "ULD_705", "ULD_705t" },
        },
    };
}
