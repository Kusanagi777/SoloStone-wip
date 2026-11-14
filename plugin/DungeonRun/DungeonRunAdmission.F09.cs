// Phase 8, family 09 (Dungeon Run bosses C: the five final bosses, the special rooms, the special starts): the admission rows
// of the cards it scripts (a method the table concatenates: DungeonRunAdmission.BuildRules). The hero powers are a boss's
// only; the boss cards are listed by boss decks or made by a boss power, and are admitted in the run player's hands too,
// where a steal, a copy or the encounter's own start block (the Luminous Candles) can put one. The Treasures King Togwaggle
// finds and A. F. Kay's deck lists are the run player's treasure cards, admitted for a boss on catalog runs (an encounter is
// fought on catalog runs only). The tests named in the reasons are the suite c-enc-09 and the enc09 bosspilot modes
// (scratchpad p8f09).
internal static partial class DungeonRunAdmission
{
    /// <summary>The lift of a run treasure that a boss of family 09 also holds: nothing is pending.</summary>
    internal const string RunTreasureBossLift = "none: a run treasure; among bosses only King Togwaggle's Magic Candle finds it and A. F. Kay's deck lists it";

    private static AdmissionRule[] RulesF09() => new[]
    {
        new AdmissionRule
        {
            Family = "boss-public-power", Admit = AdmitScope.Boss, Lift = NoneBossPower,
            Reason = "a clickable boss hero power whose whole effect is public board and hero state the engine resolves, with no hidden "
                + "zone read: Vindictive Breath deals 1 damage to all enemies for each Master Chest the boss is missing of its five "
                + "(LOOTA_BOSS_25p), Encroaching Darkness summons a 5/5 Darkspawn (LOOTA_BOSS_49p). A power that would do nothing is "
                + "not offered, by a condition that reads only the public boards, so an option of the search is always an option of "
                + "the real game (c-enc-09 C1-C8, F1-F7; the enc09, enc09-chests and enc09-seeds bosspilot modes)",
            Ids = new[] { "LOOTA_BOSS_25p", "LOOTA_BOSS_49p" },
        },
        new AdmissionRule
        {
            Family = "boss-auto-cast", Admit = AdmitScope.Boss, Lift = NoneBossPower,
            Reason = "Brimstone Warden's Dispel Ward, \"Silence your minions\" (5), cast by rule and never an option of the boss's AI "
                + "(DungeonRunEncounters.AutoCast: the wiki's auto-cast): at the start of the boss's turn, when it has 5 Mana and a "
                + "minion that is not silenced, the Cost is paid and its minions are silenced (a silenced Stone Golem can attack). "
                + "Public board and Mana state only; the trigger is on the hero power, so every clone and simulation replays it "
                + "(c-enc-09 A1-A9; the enc09 and enc09-auto bosspilot modes)",
            Ids = new[] { "LOOTA_BOSS_20p" },
        },
        new AdmissionRule
        {
            Family = "boss-room", Admit = AdmitScope.Boss, Lift = NoneBossPower,
            Reason = "a special room's hero power: the room's rules land on the boss at the first turn start of the match as bits of one "
                + "private controller tag (EncounterRules.RoomRules), which the engine's draw and playability checks read and search "
                + "clones carry. Tad (LOOTA_BOSS_22p Catch and Release) and the Treasure Vault (LOOTA_BOSS_52p Doors Are Closing) hold "
                + "no cards: the dealt hand goes back into the deck (a deck fact tells the boss's own-deck ledger the exact list), they "
                + "never draw, never fatigue and play no card. Catch and Release summons the deck minion whose Cost equals Tad's Mana "
                + "Crystals (else a random deck minion): the boss's own deck, a known multiset, by a public summon; it is offered by "
                + "board room only, never by what the deck holds; at the start of his eleventh turn an undamaged Tad summons the Sunken "
                + "Chest. Doors Are Closing is auto-cast by rule, never an option: at the start of the Vault's turn with 5 Mana Crystals "
                + "the fight ends with no loss for the run player (EncounterRules.EndWithNoLoss; the run records a win with no boss kill "
                + "credit). A. F. Kay (LOOTA_BOSS_54p Idle, \"Do nothing\") plays no card from her hand before her sixth turn; her "
                + "attacks and her power stay. No hidden zone of the run player is read (c-enc-09 B1-B14, H1-H12, J1-J7; the enc09 "
                + "and enc09-rooms bosspilot modes)",
            Ids = new[] { "LOOTA_BOSS_22p", "LOOTA_BOSS_52p", "LOOTA_BOSS_54p" },
        },
        new AdmissionRule
        {
            Family = "boss-passive-trigger", Admit = AdmitScope.Boss, Lift = NoneBossPower,
            Reason = "Xol's Beam Me Up!, a passive boss hero power started at match start (EncounterRules.ActivatePower): at the end of "
                + "the boss's turn one of the six Beam cards joins its own hand at random (the game RNG; a full hand gets none). The "
                + "boss knows its own hand; the run player sees the count (c-enc-09 D1-D4; the enc09-finals bosspilot mode)",
            Ids = new[] { "LOOTA_BOSS_26p8" },
        },
        new AdmissionRule
        {
            Family = "boss-hidden-reveal", Admit = AdmitScope.Boss, Lift = NoneBossPower,
            Reason = "Azari's Devour, \"Remove the top 2 cards of your opponent's deck\", cast by rule at the start of each of the boss's "
                + "turns while that deck holds a card (DungeonRunEncounters.AutoCast: the wiki's auto-cast; never an option of the "
                + "boss's AI, whose evaluation cannot value burning unknown cards): each card moves from the top of the run player's "
                + "deck to the run player's graveyard, a public zone (Gnomeferatu's rule, twice), so the two burned cards are public "
                + "facts for both sides and nothing else of the deck is shown: the boss's fair view keeps every other card of that "
                + "deck and the whole hand unknown, and the deck is two cards shorter. It reads the deck's public count only "
                + "(c-enc-09 E1-E7; the enc09 and enc09-auto bosspilot modes)",
            Ids = new[] { "LOOTA_BOSS_34p" },
        },
        new AdmissionRule
        {
            Family = "boss-craft", Admit = AdmitScope.Boss, Lift = NoneBossPower,
            Reason = "Mushhuckster Max's Mushroom, Mushroom, \"Craft a custom Mushroom Potion\": two Discovers among the nine 1-Cost Kazakus "
                + "effects (three random offers, then three of the others; the engine's own choice chain, which the boss's AI answers "
                + "as any Discover), and the potion of the two joins the boss's own hand. The offers are fixed client cards, never a "
                + "generation pool; offered by the boss's own hand count (c-enc-09 G1-G6; the enc09 bosspilot mode)",
            Ids = new[] { "LOOTA_BOSS_50p" },
        },
        new AdmissionRule
        {
            Family = "boss-extra-turn", Admit = AdmitScope.Boss, Lift = NoneBossPower,
            Reason = "Chronomancer Inara's Stolen Time, \"Destroy 3 of your Mana Crystals. Take an extra turn\" (10), cast by rule at the "
                + "start of the boss's turn when it has 10 Mana (DungeonRunEncounters.AutoCast: the wiki's auto-cast; never an option "
                + "of the boss's AI): the Cost is paid, three of the boss's own Mana Crystals (public) are destroyed and the engine's "
                + "extra-turn rule gives it the next turn (ExtraTurnEffectTask, the rule of the run player's Shifting Hourglass), "
                + "which the turn order and every clone carry (c-enc-09 I1-I5; the enc09 and enc09-auto bosspilot modes)",
            Ids = new[] { "LOOTA_BOSS_53p" },
        },
        new AdmissionRule
        {
            Family = "boss-treasure-find", Admit = AdmitScope.Boss, Lift = NoneBossPower,
            Reason = "King Togwaggle's Magic Candle, \"Find a Treasure!\": one of the sixteen Treasures the wiki lists for it joins the "
                + "boss's own hand at random (DungeonRunEncounters.TogwaggleTreasures; the game RNG), each a card the table admits for "
                + "a boss on catalog runs (the run-treasure-boss-find row, LOOTA_839's row, Golden Kobold); at the start of his turn the "
                + "power casts Rakanishu with a chance of 1 in 4. Offered by the boss's own hand count (c-enc-09 K1-K8; the "
                + "enc09-finals and enc09-treasures bosspilot modes)",
            Ids = new[] { "LOOTA_BOSS_99p" },
        },
        new AdmissionRule
        {
            // Player | Boss: no player role offers one (boss decks list them, a boss power or the start block makes them), but the
            // run player can hold one (the three Luminous Candles of The Darkness's start block; a hand steal or a copy of a boss
            // card), and its public effect resolves the same there. Never generated.
            NeverGenerated = true,
            Family = "boss-card", Admit = AdmitScope.Player | AdmitScope.Boss, Lift = NoneBossOnly,
            Reason = "a boss card of the Dungeon Run's final bosses and special rooms whose effect is public board, hero and Mana "
                + "resolution, the same in either player's hands: Stone Golem cannot attack and costs its owner a Mana Crystal when "
                + "it dies (LOOTA_BOSS_20t), the Beams freeze a random enemy minion and its neighbours (LOOTA_BOSS_26p2), destroy "
                + "all damaged enemy minions (LOOTA_BOSS_26p3), deal 2 damage to all enemy minions (LOOTA_BOSS_26p6) and destroy one "
                + "of the opponent's Mana Crystals (LOOTA_BOSS_26p7), Darkspawn is a 5/5 (LOOTA_BOSS_49t) and Luminous Candle "
                + "destroys every Darkspawn in play (LOOTA_BOSS_49t2: the run player starts The Darkness's fight with three), "
                + "Rakanishu deals 4 damage to a random enemy (LOOTA_BOSS_99t: cast by Magic Candle only, the client card cannot be played from a hand), Chromatic Dragonkin gains +2/+2 whenever its "
                + "owner's opponent casts a spell (BRMA12_8t), Archmage's Insight makes its owner's spells cost (0) this turn "
                + "(KARA_00_05) and Astral Portal summons a random Legendary minion from the generation pool its owner's filter "
                + "allows (KARA_00_07). Mushroom Potion (LOOTA_BOSS_50t) resolves its two ingredients, two tags of the entity that "
                + "every clone carries, and takes a target only when one is Heart of Fire. No hidden zone is read or written "
                + "(c-enc-09 A3-A8, C9-C10, D5-D14, F3-F7, G7-G16, I6-I9, K6-K8; the enc09, enc09-finals and enc09-cards bosspilot modes)",
            Ids = new[] { "LOOTA_BOSS_20t", "LOOTA_BOSS_26p2", "LOOTA_BOSS_26p3", "LOOTA_BOSS_26p6", "LOOTA_BOSS_26p7", "LOOTA_BOSS_49t",
                "LOOTA_BOSS_49t2", "LOOTA_BOSS_50t", "LOOTA_BOSS_99t", "BRMA12_8t", "KARA_00_05", "KARA_00_07" },
        },
        new AdmissionRule
        {
            NeverGenerated = true,
            Family = "boss-control", Admit = AdmitScope.Player | AdmitScope.Boss, Lift = NoneBossOnly,
            Reason = "Beam of Confusion, \"Take control of a random enemy minion\": a control change of a public board minion picked by "
                + "the game RNG (the engine's ControlTask). A run shrine never changes sides, so it is never the pick "
                + "(ShrineRules.CanChangeController), awake or dormant; nothing happens with a full board (c-enc-09 D9-D10; the "
                + "enc09-finals and enc09-cards bosspilot modes)",
            Ids = new[] { "LOOTA_BOSS_26p4" },
        },
        new AdmissionRule
        {
            NeverGenerated = true,
            Family = "boss-player-deck-insert", Admit = AdmitScope.Player | AdmitScope.Boss, Lift = NoneBossOnly,
            Reason = "a boss card that puts a publicly known card into a deck: Beam of Fear shuffles a random enemy minion from the board "
                + "into its owner's deck (LOOTA_BOSS_26p5: the minion was public, so both sides know which card joined that deck; a "
                + "run shrine is destroyed instead and goes dormant, ShrineRules.InterceptLeave), and Archmage's Apprentice shuffles "
                + "a copy of each spell its owner casts into its owner's deck (KARA_00_08: the cast was public and the copy is shown "
                + "as it is shuffled, so its owner's own-deck ledger holds it). No other card of a deck is shown "
                + "(c-enc-09 D11-D12, I8-I9; the enc09-finals and enc09-cards bosspilot modes)",
            Ids = new[] { "LOOTA_BOSS_26p5", "KARA_00_08" },
        },
        new AdmissionRule
        {
            // Not NeverGenerated: the card joins the run deck by a run deck edit, and the run's deck checks refuse a card that
            // generation may never yield. No generation pool holds it (it is no collectible card).
            Family = "boss-hidden-reveal", Admit = AdmitScope.Player | AdmitScope.Boss, Lift = NoneBossOnly,
            Reason = "Tad's Pole, \"At the end of your turn, summon a minion from your opponent's deck\": the run player's reward for "
                + "breaking the Sunken Chest (a weapon in the run deck). A random minion of the opponent's deck leaves that deck and "
                + "joins the weapon owner's board by a public summon with a card-transfer fact, so the pulled card is public and "
                + "the deck's owner sees its own card leave (its own-deck ledger removes that definition); nothing else of the deck "
                + "is shown. In a boss's hands (a weapon steal) the run player's card becomes public the same way "
                + "(c-enc-09 B15-B19)",
            Ids = new[] { "LOOTA_BOSS_22t" },
        },
        new AdmissionRule
        {
            NeverGenerated = true,
            Family = "boss-run-edit", Admit = AdmitScope.Player | AdmitScope.Boss, Lift = NoneBossOnly,
            Reason = "a boss minion whose Deathrattle asks the run for a deck edit (RunHelpers.EditAdd, applied by "
                + "DungeonRun.ApplyMatchResult through the run's deck checks after a won fight; a simulation writes into its own "
                + "clone's result): Treasure Coffer adds a random treasure of the run's own treasure pool to the run deck, whoever "
                + "controls the Coffer (LOOTA_BOSS_52t), and Sunken Chest adds Tad's Pole to the run deck when its owner's opponent "
                + "is the run player, and its owner loses the game (LOOTA_103). The match itself changes by public board events "
                + "only (c-enc-09 B10-B14, H5-H12, N1-N6; the enc09-rooms bosspilot mode)",
            Ids = new[] { "LOOTA_BOSS_52t", "LOOTA_103" },
        },
        new AdmissionRule
        {
            // The legacy verdict stays the run player's only (no legacy boss holds one); a catalog run admits a boss too. The
            // ids left the core run-treasure row for this one; LOOTA_839 keeps its own row with the same catalog lift.
            Family = "run-treasure-boss-find", Admit = AdmitScope.Player, Lift = RunTreasureLift,
            Reason = "treasure: only the player can hold it; public effects the engine resolves, so the boss's legal moves and search see the result (c-treasures-F2 T4-T33)",
            CatalogAdmit = AdmitScope.Player | AdmitScope.Boss, CatalogLift = RunTreasureBossLift,
            CatalogReason = "a Kobolds and Catacombs active treasure that King Togwaggle's Magic Candle finds (the wiki's list) and, for "
                + "Bag of Stuffing and Boots of Haste, that A. F. Kay's deck lists: a card whose effect is written for its owner and "
                + "resolves by public board, hero and Mana events or on its owner's own hand and deck, the same for a boss as for the "
                + "run player (Wand of Disintegration and THE CANDLE destroy or damage the opponent's minions, a run shrine going "
                + "dormant as from any such effect; THE CANDLE shuffles itself into its owner's deck, a card its owner knows; Archmage "
                + "Staff adds a random scripted Mage spell from the generation pool its owner's filter allows; Bag of Stuffing draws "
                + "from its owner's own deck). Each is played by the boss in an engine game (c-enc-09 L1-L16; the enc09-treasures "
                + "bosspilot mode), on top of the run player's tests (c-treasures-F2 T4-T33)",
            Ids = new[] { "LOOTA_806", "LOOTA_812", "LOOTA_816", "LOOTA_819", "LOOTA_821", "LOOTA_823", "LOOTA_826", "LOOTA_827", "LOOTA_835",
                "LOOTA_836", "LOOTA_838", "LOOTA_842b", "LOOTA_843", "LOOTA_847" },
        },
        new AdmissionRule
        {
            // Boss only: the catalog, the run player's pools and every generation pool keep their verdict (the card was
            // unclassified, rejected everywhere), so no offer and no random pool changes.
            Family = "secret-destroy", Admit = AdmitScope.Boss, Lift = "none: boss decks only (Mushhuckster Max's list); the run player's pools and generation keep it out",
            Reason = "Eater of Secrets, \"Battlecry: Destroy all enemy Secrets. Gain +1/+1 for each\", in Mushhuckster Max's deck: the run "
                + "player's Secrets leave the Secret zone by a public event and the buff counts them, a public number; the boss's "
                + "Secret worlds follow the zone as after any triggered Secret. It generates no Secret (c-enc-09 G17-G18; the "
                + "enc09 bosspilot mode)",
            Ids = new[] { "OG_254" },
        },
    };
}
