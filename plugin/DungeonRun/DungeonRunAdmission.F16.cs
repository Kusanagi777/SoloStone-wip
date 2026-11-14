// Phase 8, family 16 (Dalaran Heist bosses C: DALA_BOSS_31 to 42, 44 and 45 and their boss-only cards): the admission rows of
// the cards it scripts (a method the table concatenates: DungeonRunAdmission.BuildRules). The hero powers are a boss's only;
// PARTY PORTAL!, the Exotic Pet and the Mime are made by boss powers or listed by a boss deck only, and are admitted in the run
// player's hands too, where a steal or a copy of a boss card can put one. Five Kobolds and Catacombs treasures that Ol'
// Toomba's Tales of Fortune shuffles into his deck are admitted for a boss on catalog runs (they left the core run-treasure
// row for this file, as family 09's did). The tests named in the reasons are the suite c-enc-16 (verify/suites/p8f16) and the
// enc16 bosspilot modes.
internal static partial class DungeonRunAdmission
{
    /// <summary>The lift of a run treasure that Ol' Toomba also shuffles into his deck: nothing is pending.</summary>
    internal const string RunTreasureToombaLift = "none: a run treasure; among bosses only Ol' Toomba's Tales of Fortune shuffles it into his own deck";

    private static AdmissionRule[] RulesF16() => new[]
    {
        new AdmissionRule
        {
            Family = "heist-c-board-power", Admit = AdmitScope.Boss, Lift = NoneBossPower,
            Reason = "a clickable Dalaran Heist boss hero power whose whole effect is public board and hero state the engine resolves: "
                + "Bling it On! makes a minion Golden (the client's PREMIUM tag, a public tag of the board entity) or, when it already "
                + "is, gives it +4/+4 (DALA_BOSS_31p), Summon Companion summons one of Animal Companion's three (DALA_BOSS_41p) and Ray "
                + "of Suffering deals the number on the power to the enemy hero and raises it by 1 (DALA_BOSS_45p; the count is the "
                + "power's public TAG_SCRIPT_DATA_NUM_1). A power that would do nothing is not offered (a minion target, a free board "
                + "slot), so an option of the search is always an option of the real game; every random pick uses the game RNG and is "
                + "reported as a random event (c-enc-16 A1-A6, K1-K4, N1-N4; the enc16 and enc16-seeds bosspilot modes)",
            Ids = new[] { "DALA_BOSS_31p", "DALA_BOSS_41p", "DALA_BOSS_45p" },
        },
        new AdmissionRule
        {
            Family = "heist-c-auto-cast", Admit = AdmitScope.Boss, Lift = NoneBossPower,
            Reason = "a Dalaran Heist boss hero power the wiki marks 'Auto-cast', cast by rule at the start of the boss's turn and never "
                + "an option of the boss's AI (family 09's DungeonRunEncounters.AutoCast: its Cost is paid when the boss has the Mana "
                + "and the condition holds): Makeover gives a random friendly minion one of four haircuts, Stealth, Taunt, +2 Health "
                + "or +2 Attack (DALA_BOSS_32p, with (1) Mana and a friendly minion), and For The Light! gives every minion in the "
                + "boss's hand +1/+1 (DALA_BOSS_39p, with (2) Mana and a minion in hand). Public board state and the boss's own hand "
                + "only; the trigger is on the hero power, so every clone and simulation replays it (c-enc-16 B1-B6, I1-I5; the enc16 "
                + "and enc16-seeds bosspilot modes)",
            Ids = new[] { "DALA_BOSS_32p", "DALA_BOSS_39p" },
        },
        new AdmissionRule
        {
            Family = "heist-c-passive", Admit = AdmitScope.Boss, Lift = NoneBossPower,
            Reason = "a passive Dalaran Heist boss hero power, started at match start (EncounterRules.ActivatePower), never clickable, "
                + "whose trigger answers public events: Made of Coins adds a Coin to the boss's hand whenever its hero takes damage "
                + "(DALA_BOSS_33p), Merry Go Round rotates every minion one place around the ring of both rows at the end of the boss's "
                + "turn, the two that cross changing controller as a control change does, a run shrine never moving "
                + "(DALA_BOSS_34p: EncounterRules.Rotate, ShrineRules.BlocksControl), Healing Hands restores 2 Health to the boss's "
                + "minions at the start of its turn (DALA_BOSS_35p) and Immolation Aura deals 2 damage to a minion after it attacks the "
                + "boss's hero (DALA_BOSS_40p). No hidden zone is read (c-enc-16 C1-C4, D1-D14, E1-E3, J1-J5; the enc16, "
                + "enc16-seeds and enc16-carousel bosspilot modes)",
            Ids = new[] { "DALA_BOSS_33p", "DALA_BOSS_34p", "DALA_BOSS_35p", "DALA_BOSS_40p" },
        },
        new AdmissionRule
        {
            Family = "heist-c-craft", Admit = AdmitScope.Boss, Lift = NoneBossPower,
            Reason = "Draemus's Import \"Pet\", \"Craft a custom Exotic Pet\": Build-A-Beast's craft (the wiki: functionally identical), "
                + "two Discovers among the engine's pinned Build-A-Beast part pools, each limited to the cards the run's generation "
                + "filter allows the boss (Game.GenerateFor / BlocksGenerated), answered by the boss's AI as any Discover (the offers "
                + "are private to the boss); the composed minion (the Zombeast rule) joins the boss's own hand named Exotic Pet. "
                + "Offered by the boss's own hand count (c-enc-16 F1-F6; the enc16 bosspilot mode)",
            Ids = new[] { "DALA_BOSS_36p" },
        },
        new AdmissionRule
        {
            Family = "heist-c-own-deck-power", Admit = AdmitScope.Boss, Lift = NoneBossPower,
            Reason = "a Dalaran Heist boss hero power that changes the boss's own deck by a public event: Tales of Fortune shuffles one "
                + "of twenty known treasures into it (DALA_BOSS_37p) and Pogoshuffle three new copies of a chosen board minion "
                + "(DALA_BOSS_42p), each shown as it is shuffled, so the boss's own-deck ledger (OwnDeckLedger) holds the cards and "
                + "the run player sees them; Dark Tidings reveals a random minion of the boss's deck to both players, gives it +2/+2 "
                + "and moves it to the top without leaving the deck (DALA_BOSS_44p; a deck reorder records no zone change, the top card "
                + "is recorded as EncounterRules.DeckTopKnown). Nothing of the run player's hidden zones is read; the boss's own deck is "
                + "read only by the effect's random pick, a random event its search re-plans after. Offered by the deck's public count "
                + "(c-enc-16 G1-G6, L1-L5, M1-M7; the enc16 and enc16-seeds bosspilot modes)",
            Ids = new[] { "DALA_BOSS_37p", "DALA_BOSS_42p", "DALA_BOSS_44p" },
        },
        new AdmissionRule
        {
            Family = "heist-c-own-hand-power", Admit = AdmitScope.Boss, Lift = NoneBossPower,
            Reason = "Tipsi Wobblerune's Portal Party, \"Add a random Portal to your hand\": one of seven known cards (the wiki's Portals, "
                + "PARTY PORTAL! among them) joins the boss's own hand, which it knows exactly and the run player sees as a count; "
                + "offered only while that hand has room (c-enc-16 H1-H3; the enc16 bosspilot mode)",
            Ids = new[] { "DALA_BOSS_38p" },
        },
        new AdmissionRule
        {
            // Player | Boss: no player role offers one (Tipsi's deck lists PARTY PORTAL!, Import "Pet" makes the Exotic Pet,
            // PARTY PORTAL! the Mime), but the run player can take one from a boss (a steal, a copy, a control change or the
            // Carousel's rotation of a Mime or a Pet), and its effect resolves the same there. Never generated.
            NeverGenerated = true,
            Family = "heist-c-boss-card", Admit = AdmitScope.Player | AdmitScope.Boss, Lift = NoneBossOnly,
            Reason = "a Dalaran Heist boss card whose effect the engine resolves the same in either player's hands: PARTY PORTAL! "
                + "summons one of the five Partygoers the wiki lists (DALA_BOSS_38t; the game RNG), the Mime is a 5/6 that enters play "
                + "silenced (KARA_13_22, One Night in Karazhan's Partygoer) and the Exotic Pet is the client's untexted card the "
                + "crafted pet is named after (DALA_BOSS_36t). Public board changes only (c-enc-16 F4, H4-H8; the enc16 and "
                + "enc16-cards bosspilot modes)",
            Ids = new[] { "DALA_BOSS_38t", "DALA_BOSS_36t", "KARA_13_22" },
        },
        new AdmissionRule
        {
            // The legacy verdict stays the run player's only (no legacy boss holds one); a catalog run admits a boss too. The
            // ids left the core run-treasure row for this one (family 09's run-treasure-boss-find did the same).
            Family = "run-treasure-boss-shuffle", Admit = AdmitScope.Player, Lift = RunTreasureLift,
            Reason = "treasure: only the player can hold it; public effects the engine resolves, so the boss's legal moves and search see the result (c-treasures-F2 T4-T33)",
            CatalogAdmit = AdmitScope.Player | AdmitScope.Boss, CatalogLift = RunTreasureToombaLift,
            CatalogReason = "a Kobolds and Catacombs active treasure that Ol' Toomba's Tales of Fortune shuffles into his own deck (the "
                + "wiki's list), whose effect is written for its owner and resolves by public board, hero and Mana events or on its "
                + "owner's own deck, the same for a boss as for the run player: Orb of Destruction destroys two of the opponent's Mana "
                + "Crystals and makes it discard two random cards, public discards (LOOTA_811), Horn of Cenarius recruits three minions "
                + "of its owner's own deck (LOOTA_837), Portable Forge Discovers a scripted Legendary weapon through its owner's "
                + "generation filter and equips it (LOOTA_841), the Blade of Quel'Delar and its Hilt combine in their owner's hand "
                + "into Quel'Delar, whose owner's hero attack deals 6 damage to all enemies (LOOTA_842a, LOOTA_842). Each is played by "
                + "a boss in an engine game (c-enc-16 T1-T8; the enc16-cards bosspilot mode), on top of the run player's tests "
                + "(c-treasures-F2 T4-T33)",
            Ids = new[] { "LOOTA_811", "LOOTA_837", "LOOTA_841", "LOOTA_842", "LOOTA_842a" },
        },
    };
}
