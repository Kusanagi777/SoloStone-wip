// Phase 8, family 05 (the global-rule framework and the Dalaran Heist anomalies and twists): the admission rows of the rule
// cards it scripts and of the tokens they place. A rule is admitted in scope Rule only (never a deck card, a boss card or a
// generation result); each row states what the boss's fair search may read and names its regression tests: the c-rules-05
// suite (each rule's text on both sides in real engine games through DungeonRunSetup) and the rules05 bosspilot mode (the
// fair AI pilots the boss under the rule through the run setup path). DALA_850, DALA_864 and DALA_866 are family 01's rows.
internal static partial class DungeonRunAdmission
{
    /// <summary>
    /// What lifts DALA_857 Glittering: its Treasure pool is run rewards no boss and no generation pool may hold. Step 9 (lane R)
    /// rechecked it: the step shares treasures player-side only (plan D6) and gives no boss a treasure, so no step-9 work lifts it
    /// and the rule stays rejected (schedule-v1.json states the same reason).
    /// </summary>
    internal const string RuleTreasureLift = "a boss-side treasure model (not planned; step 9 rechecked it and shares treasures player-side only): the Dalaran "
        + "Heist active treasures admitted for a boss and as a generation result, each one's run-state writes (DALA_718, DALA_719, DALA_721) "
        + "given a boss meaning";
    /// <summary>A token only a global rule places.</summary>
    internal const string NoneRuleToken = "none: a token a global rule places; no deck, boss or generation pool offers it";

    private static AdmissionRule[] RulesF05() => new[]
    {
        new AdmissionRule
        {
            NeverGenerated = true,
            Family = "global-rule-symmetric", Admit = AdmitScope.Rule, Lift = NoneRule,
            Reason = "a global rule both players play under for the whole match: its player enchantment is attached to each controller at "
                + "match start (DungeonRunGlobalRules.Apply), a public effect the engine resolves for both sides on the boards, the "
                + "heroes or each player's own hand and deck, so the boss's legal moves and its search see it and no hidden zone of the "
                + "other side is read or written: each player's Battlecries (DALA_852) or Deathrattles (DALA_853) trigger twice (the "
                + "controller auras of Battle Totem and Totem of the Dead; an encounter whose boss power is the same rule lists the flag "
                + "the rule row excludes), a played minion gains \"Deathrattle: Summon a 1/1 copy of this minion\" (DALA_854: a fresh 1/1 "
                + "of the card, which gains nothing), each player draws up to 5 cards at the end of that player's turn (DALA_855: the "
                + "boss's own deck is a known multiset with a sampled order, as for every draw), a 5/5 Dragon Spirit after a player casts "
                + "3 spells in a turn (DALA_860: a private per-turn counter on that player's enchantment, an entity tag clones replay), "
                + "each attacking minion has a 50% chance to attack the wrong enemy (DALA_861: Ogre Warmaul's rule, a reported random "
                + "event), the cards in each player's hand cost (1) less at the end of that player's turn (DALA_862: Emperor Thaurissan's "
                + "enchantment; an Echo or repeatable card is never brought below (1)), a summoned minion gains \"Deathrattle: Deal 1 "
                + "damage to all minions\" (DALA_865) or one of Taunt, Divine Shield, Rush and Windfury at random (DALA_867: a reported "
                + "random event), and a minion's Attack and Health are swapped as it enters play (DALA_831 The Stench: once, Crazed "
                + "Alchemist's rule; never on a run or a boss with a board shrine: schedule-v1.json) "
                + "(c-rules-05 B, C, D, E, I, J, K, M, N, P; the rules05 bosspilot mode)",
            Ids = new[] { "DALA_831", "DALA_852", "DALA_853", "DALA_854", "DALA_855", "DALA_860", "DALA_861", "DALA_862", "DALA_865", "DALA_867" },
        },
        new AdmissionRule
        {
            NeverGenerated = true,
            Family = "global-rule-generated", Admit = AdmitScope.Rule, Lift = NoneRule,
            Reason = "a global rule that puts a random minion on the board of the player it acts for, through the run's generation filter "
                + "for that side (Game.BlocksGenerated: scripted minions only, never a card the table rejects for generation or for a "
                + "boss): a minion of the played spell's Cost after a player plays a spell (DALA_851: Summoning Stone's rule), one "
                + "random Murloc with a chance of 1 in 4 at the start of each player's turn (DALA_858: the chance is an adaptation, the "
                + "wiki says \"a small chance\"), and the random minion each player's Violet Prison holds and releases after as many of "
                + "its owner's turns as the minion costs (DALA_832 The Prisons: the held card and the turns left are public tags of the "
                + "prison, so both players and the boss's fair view read the same facts). Every roll uses the game RNG and is reported "
                + "as a random event, so the boss's search re-plans after it; the results are public board changes "
                + "(c-rules-05 A, G, Q; the rules05 bosspilot mode)",
            Ids = new[] { "DALA_832", "DALA_851", "DALA_858" },
        },
        new AdmissionRule
        {
            NeverGenerated = true,
            Family = "global-rule-recast", Admit = AdmitScope.Rule, Lift = NoneRule,
            Guard = "one copy per played spell (RecastRules.Recasting: a copy never casts again)",
            Reason = "Wild Magic: after a player plays a spell from hand, a copy of it is cast for that player with random targets (the "
                + "engine's effect play: a random legal target, a random Choose One option, any choice picked with the game RNG), a "
                + "public cast reported as a random event; the copy carries the recast mark, so it never triggers another cast, and a "
                + "Secret or a Quest is not cast again (it could not enter play twice, and the failed copy would show the opponent what "
                + "was played). An encounter whose boss power recasts spells lists the flag the rule row excludes "
                + "(c-rules-05 F; the rules05 bosspilot mode)",
            Ids = new[] { "DALA_856" },
        },
        new AdmissionRule
        {
            NeverGenerated = true,
            Family = "global-rule-start", Admit = AdmitScope.Rule, Lift = NoneRule,
            Reason = "a global rule that changes how both players start (RuleHooks; no client \"Player Enchantment\" card exists for it): "
                + "two more opening-hand cards for each player before the mulligan and two Mana Crystals each (DALA_859 Gorged: the "
                + "engine's opening-hand cap applies), the boss starts with a 0/3 Cache of Cash whose Deathrattle gives each player two "
                + "Coins (DALA_829 The Coffers), and each player starts with three random Carts, permanents that only take board slots, "
                + "so each can have four minions (DALA_833 The Carts: untouchable cards the engine's board rules protect; never on a run "
                + "or a boss with a board shrine or a start board, or for Darius Crowley's Cannons: schedule-v1.json). The boards are "
                + "placed after both mulligans with the encounter's start block; everything is public and no hidden zone changes "
                + "(c-rules-05 H, O, R; the rules05 bosspilot mode)",
            Ids = new[] { "DALA_829", "DALA_833", "DALA_859" },
        },
        new AdmissionRule
        {
            NeverGenerated = true,
            Family = "global-rule-hidden", Admit = AdmitScope.Rule, Lift = NoneRule,
            Reason = "Hallucinating: ten Hallucinations (UNG_856, a collectible the table admits in every scope: a Discover from the "
                + "opponent's class through the run's generation filter) are shuffled into each player's deck after both mulligans, as "
                + "every encounter deck add is. A public effect on a hidden zone: both sides know the ten cards and neither learns a "
                + "position, and each owner's deck list is recorded for its own-deck ledger (EncounterRules.DeckRewritten), so the "
                + "boss's sampled deck holds them. Never with a boss form that replaces its deck or a relay fight, where one side would "
                + "lose them (schedule-v1.json) (c-rules-05 L; the rules05 bosspilot mode)",
            Ids = new[] { "DALA_863" },
        },
        new AdmissionRule
        {
            NeverGenerated = true,
            Family = "global-rule-rejected", Admit = AdmitScope.None, Lift = RuleTreasureLift,
            Reason = "Glittering (\"After a player plays a card that costs (8) or more, that player adds a Treasure to their hand\"): the "
                + "wiki states the pool is the Dalaran Heist treasures, which are run rewards the table admits for the run player's run "
                + "deck only (no boss and no generation pool may hold one: RunTreasureLift), and several of them write run state "
                + "(DALA_718, DALA_719, DALA_721). A rule for both players needs each treasure admitted for a boss and as a generation "
                + "result; until then the rule is rejected in schedule-v1.json and not scripted",
            Ids = new[] { "DALA_857" },
        },
        new AdmissionRule
        {
            NeverGenerated = true,
            Family = "global-rule-token", Admit = AdmitScope.Rule, Lift = NoneRuleToken,
            Reason = "a token only a global rule places, on the board in public: the Cache of Cash (DALA_829t, a 0/3 with a public "
                + "Deathrattle), the Violet Prison (DALA_832t, an untouchable permanent whose held minion and turns left are public "
                + "tags) and the four Carts (DALA_833t, t2, t3, t4: untouchable permanents with no effect) (c-rules-05 O, Q, R)",
            Ids = new[] { "DALA_829t", "DALA_832t", "DALA_833t", "DALA_833t2", "DALA_833t3", "DALA_833t4" },
        },
    };
}
