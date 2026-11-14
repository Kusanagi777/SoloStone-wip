// Phase 8, family 12 (Monster Hunt bosses C: thirteen Witchwood encounters, ten with a passive hero power, and their two boss
// cards): the admission rows of the cards it scripts (a method the table concatenates: DungeonRunAdmission.BuildRules). The
// hero powers are a boss's only; the two boss cards are listed by a boss deck or summoned by a boss power, and are admitted in
// the run player's hands too, where a steal or a copy of a boss card can put one. The tests named in the reasons are the suite
// c-enc-12 (verify/suites/p8f12/c-enc-12) and the enc12 bosspilot modes (verify/bin/bp.sh).
internal static partial class DungeonRunAdmission
{
    private static AdmissionRule[] RulesF12() => new[]
    {
        new AdmissionRule
        {
            Family = "boss-passive-public", Admit = AdmitScope.Boss, Lift = NoneBossPower,
            Reason = "a passive Monster Hunt boss hero power, started at match start (EncounterRules.ActivatePower), whose whole effect "
                + "is public board and hero state or the boss's own ordinary draws, raised by a public event: From the Swamp summons a "
                + "Bloated Zombie for the boss whenever a minion of the run player dies (GILA_BOSS_24p), Blood Red Apple makes the boss's "
                + "spells cost Health instead of Mana (GILA_BOSS_30p: Blood Pact's SPELLS_COST_HEALTH controller aura, the boss's side "
                + "only), Monstrous Aura gives the boss's minions +1/+1 (GILA_BOSS_46p, a board aura), Tangled Wrath draws the boss a "
                + "random spell of its own deck whenever the run player casts a spell (GILA_BOSS_63p: an ordinary draw from the boss's "
                + "own deck, a known multiset with a sampled order) and Starving draws the boss a card whenever one of its Beasts dies "
                + "(GILA_BOSS_65p). The trigger is the death, the cast or the play itself, never the identity of a hidden card "
                + "(c-enc-12 A1-A6, B1-B5, E1-E3, H1-H5, J1-J4; the enc12 and enc12-seeds bosspilot modes)",
            Ids = new[] { "GILA_BOSS_24p", "GILA_BOSS_30p", "GILA_BOSS_46p", "GILA_BOSS_63p", "GILA_BOSS_65p" },
        },
        new AdmissionRule
        {
            Family = "boss-passive-both", Admit = AdmitScope.Boss, Lift = NoneBossPower,
            Reason = "a passive Monster Hunt boss hero power that changes a rule for BOTH players, started at match start: Poisoned Drink "
                + "deals 1 damage to a player's hero for every card that player draws (GILA_BOSS_45p: the draw is the event; the drawn "
                + "card stays hidden), Frumiousity makes all Battlecries trigger twice (GILA_BOSS_47p: Battle Totem's controller aura on "
                + "each player, EncounterRules.BattlecriesTwice), Echoes of the Witchwood gives every spell in either hand Echo "
                + "(GILA_BOSS_50p: a hand aura on both players; an Echo copy is a copy of the card just played, public, and never costs "
                + "less than (1), EncounterRules.EchoCopyFloor; at most EncounterRules.EchoCopiesPerTurn copies per player and turn) and "
                + "Cannibalism gives the minions next to a minion that dies +1 Attack (GILA_BOSS_67p: the board positions are public, "
                + "EncounterRules.DeathNeighbours). Public, symmetric engine resolution the boss's search plays through on both sides "
                + "(c-enc-12 D1-D6, F1-F3, G1-G10, L1-L8; the enc12, enc12-seeds and enc12-hidden bosspilot modes)",
            Ids = new[] { "GILA_BOSS_45p", "GILA_BOSS_47p", "GILA_BOSS_50p", "GILA_BOSS_67p" },
        },
        new AdmissionRule
        {
            Family = "boss-deck-rewrite", Admit = AdmitScope.Boss, Lift = NoneBossPower,
            Reason = "Forlorn Lovers' Together Forever, \"Whenever a friendly minion dies, shuffle a copy into your deck\": a fresh copy of "
                + "a public card (the minion that just died) enters the boss's own deck; after each copy the boss's own-deck ledger is "
                + "told the deck's exact list (EncounterRules.DeckRewritten, consumed by OwnDeckLedger.Sample), so the boss's search "
                + "samples a deck that holds the copies. The run player sees the deck's count and the public copy, as in the client; no "
                + "hidden zone of the run player is read (c-enc-12 C1-C7; the enc12 and enc12-hidden bosspilot modes)",
            Ids = new[] { "GILA_BOSS_39p" },
        },
        new AdmissionRule
        {
            Family = "boss-hidden-shuffle", Admit = AdmitScope.Boss, Lift = NoneBossPower,
            Reason = "Winslow Tobtock's Hypnotize, \"Each player shuffles their hand into their deck and draws that many cards\": the boss's "
                + "hand, then the run player's, goes back into its owner's deck as plain cards and as many are drawn by the ordinary draw "
                + "rules. Both hands and decks change while hidden, so a belief reset of hand and deck is recorded for each player "
                + "(RunHelpers.BeliefReset: what the boss learned about the run player's hand before is dropped, FairBotState.ForgetResetHand) "
                + "and the boss's own-deck ledger is told its deck's exact list (EncounterRules.DeckRewritten). The boss learns only the "
                + "counts of the run player's cards; it is offered while either hand holds a card, which is public "
                + "(c-enc-12 I1-I9; the enc12 and enc12-hidden bosspilot modes)",
            Ids = new[] { "GILA_BOSS_64p" },
        },
        new AdmissionRule
        {
            Family = "boss-minion-power", Admit = AdmitScope.Boss, Lift = NoneBossPower,
            Reason = "a clickable Monster Hunt boss hero power aimed at any minion, whose whole effect is public board state the engine "
                + "resolves: ... silences a minion (GILA_BOSS_66p: the engine's Silence) and Poison Flask deals 2 damage to a minion and "
                + "gives it Poisonous if it survives (GILA_BOSS_68p: a hero power, no Spell Damage). No hidden zone is read "
                + "(c-enc-12 K1-K3, M1-M4; the enc12 and enc12-seeds bosspilot modes)",
            Ids = new[] { "GILA_BOSS_66p", "GILA_BOSS_68p" },
        },
        new AdmissionRule
        {
            // Player | Boss: no player role offers one (From the Swamp raises the Zombie, Rancel's deck lists the Quillflinger),
            // but the run player can take one from a boss (a control change, a deck steal, a copy), and its effect resolves the same
            // there. Never generated.
            NeverGenerated = true,
            Family = "boss-hunt-minion", Admit = AdmitScope.Player | AdmitScope.Boss, Lift = NoneBossOnly,
            Reason = "a Monster Hunt boss minion whose effect is public board and hero resolution, the same in either player's hands: "
                + "Bloated Zombie deals 1 damage to all characters when it dies (GILA_BOSS_24t, raised by From the Swamp) and Infected "
                + "Quillflinger deals 1 damage to a random enemy minion whenever it takes damage (GILA_BOSS_68t: an enemy minion an "
                + "effect can pick, by the game RNG, reported as a random event). No hidden zone is read or written "
                + "(c-enc-12 A2-A6, M5-M9; the enc12 and enc12-seeds bosspilot modes)",
            Ids = new[] { "GILA_BOSS_24t", "GILA_BOSS_68t" },
        },
    };
}
