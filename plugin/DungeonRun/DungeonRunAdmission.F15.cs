// Phase 8, family 15 (Dalaran Heist bosses B: fourteen Heist encounters and their two boss cards): the admission rows of the
// cards it scripts (a method the table concatenates: DungeonRunAdmission.BuildRules). The hero powers are a boss's only; the
// boss cards are listed by a boss deck or made by a boss power, and are admitted in the run player's hands too, where a steal,
// a copy or a control change can put one. A Small Rock (GILA_500p2t, Tess Greymane's Scavenge token) is a run treasure card
// that Mama Diggs's Excavate also finds: admitted for a boss on catalog runs (an encounter is fought on catalog runs only). The
// family names are the ones families 08 and 09 introduced. The tests named in the reasons are the suite c-enc-15
// (verify/suites/p8f15/c-enc-15) and the enc15 bosspilot modes (verify/bin/bp.sh).
internal static partial class DungeonRunAdmission
{
    /// <summary>The lift of A Small Rock for a boss: nothing is pending.</summary>
    internal const string SmallRockBossLift = "none: a run treasure token; among bosses only Mama Diggs's Excavate finds it";

    private static AdmissionRule[] RulesF15() => new[]
    {
        new AdmissionRule
        {
            Family = "boss-auto-cast", Admit = AdmitScope.Boss, Lift = NoneBossPower,
            Reason = "Alchemist Wendy's Equivalent Exchange, \"Swap a random card in each player's hand\" (1), cast by rule and never an "
                + "option of the boss's AI (DungeonRunEncounters.AutoCast: the wiki's auto-cast): at the start of the boss's turn, when it "
                + "has 1 Mana and both hands hold a card (hand counts, public), one random card of each hand changes sides by the "
                + "hand-to-hand transfer rule of Impetuous Companion (DungeonRunEncounters.ExchangeRandomCards: a CardTransferredFact "
                + "each, so the side that gave a card knows it; enchantments and Cost stay with the card; both leave first, so a full "
                + "hand never burns one). The boss then holds the run player's card exactly in its own hand and knows the one it gave; "
                + "its fair view masks every other card of the run player's hand as before. The trigger is on the hero power, so every "
                + "clone and simulation replays it, with a sampled hand there (c-enc-15 section A; the enc15 bosspilot modes)",
            Ids = new[] { "DALA_BOSS_16p" },
        },
        new AdmissionRule
        {
            Family = "boss-auto-cast", Admit = AdmitScope.Boss, Lift = NoneBossPower,
            Reason = "Disidra Stormglory's Totemic Summons, \"Summon ANY random Totem\" (2), cast by rule and never an option of the "
                + "boss's AI (DungeonRunEncounters.AutoCast: the wiki's auto-cast): at the start of the boss's turn, when it has 2 Mana "
                + "and board room, one random scripted Totem of any class (the basic Totems included) from the pool the run's "
                + "generation filter allows for the boss is summoned. Public board and Mana state only (c-enc-15 section C; the enc15 "
                + "bosspilot modes)",
            Ids = new[] { "DALA_BOSS_18p" },
        },
        new AdmissionRule
        {
            Family = "boss-public-power", Admit = AdmitScope.Boss, Lift = NoneBossPower,
            Reason = "a clickable Dalaran Heist boss hero power whose whole effect is public board, hero and Mana state the engine "
                + "resolves: Lil' Eviscerate deals 1 damage to a character, 2 with the boss's Combo active (DALA_BOSS_19p), Shhh! "
                + "silences every minion of both boards (DALA_BOSS_22p), Sharkbite destroys a random damaged minion of either board "
                + "(DALA_BOSS_23p; a run shrine goes dormant as from any destroy effect), Ar'ha's Call attaches a public player "
                + "enchantment that makes the boss's next Battlecry (a minion's, a weapon's or a hero card's) resolve one more time "
                + "(DALA_BOSS_24p, EXTRA_BATTLECRIES_ADDITIONAL; that card uses it up and the enchantment is removed), Masterpiece! spends all the boss's remaining Mana and "
                + "summons a random scripted minion of that Cost from the pool the run's generation filter allows (DALA_BOSS_27p) and A "
                + "Tale of Kings summons an Underbelly Rat (DALA_BOSS_30p). A power that would do nothing is not offered, by a condition "
                + "that reads only the public boards and the boss's own player enchantments; every random pick uses the game RNG and is "
                + "reported as a random event; no hidden zone is read (c-enc-15 sections D, F, G, H, K, N; the enc15 bosspilot modes)",
            Ids = new[] { "DALA_BOSS_19p", "DALA_BOSS_22p", "DALA_BOSS_23p", "DALA_BOSS_24p", "DALA_BOSS_27p", "DALA_BOSS_30p" },
        },
        new AdmissionRule
        {
            Family = "boss-passive-trigger", Admit = AdmitScope.Boss, Lift = NoneBossPower,
            Reason = "a passive Dalaran Heist boss hero power (registered with the passive powers, started at match start; never an "
                + "option) whose rule follows public events or the boss's own cards: Twin Paths gives the boss's Choose One cards and "
                + "powers both effects (DALA_BOSS_17p, Fandral Staghelm's CHOOSE_BOTH controller aura), Stormswell casts a copy of each "
                + "spell with Overload the boss casts again on the same target (DALA_BOSS_20p, RecastRules.Recast: the copy overloads too, as any spell cast by an effect; a copy never casts "
                + "again), Cold Water freezes the first enemy minion each turn that attacks the boss's hero (DALA_BOSS_26p; the turn is a "
                + "tag of the power) and Scroll Savvy makes a spell either player draws cost (0) that turn (DALA_BOSS_28p: the text names "
                + "no player; an enchantment on the drawn card in its drawer's own hand, which the boss's fair view masks on the run "
                + "player's side like every other card of that hand). No hidden zone is read (c-enc-15 sections B, E, J, L; the enc15 "
                + "bosspilot modes)",
            Ids = new[] { "DALA_BOSS_17p", "DALA_BOSS_20p", "DALA_BOSS_26p", "DALA_BOSS_28p" },
        },
        new AdmissionRule
        {
            Family = "boss-hidden-reveal", Admit = AdmitScope.Boss, Lift = NoneBossPower,
            Reason = "Jepetto Joybuzz's Assembly, \"After a player draws a minion, they summon a 1/1 copy of it\" (passive, both "
                + "players): after either player's draw of a minion (not a mulligan replacement), the drawer summons a 1/1 copy of it "
                + "(Dollmaster Dorian's rule, Toyification). The copy is a public summon, so the run player's drawn minion is revealed "
                + "as Dorian's copy reveals it; nothing else of the hand or deck is shown, and the boss's fair view does not use the "
                + "reveal (that hand stays masked there: the search never reads the hidden card). In the boss's simulations the run "
                + "player's draws are samples, so the copies there are sampled too, a random event re-planned after (c-enc-15 section "
                + "I; the enc15 bosspilot modes)",
            Ids = new[] { "DALA_BOSS_25p" },
        },
        new AdmissionRule
        {
            Family = "boss-treasure-find", Admit = AdmitScope.Boss, Lift = NoneBossPower,
            Reason = "Mama Diggs's Excavate, \"Deal 2 damage to a friendly Elemental. Find out what's inside.\" (1): 2 damage to an "
                + "Elemental of the boss, then one of the wiki's six excavations at random (the game RNG, a random event): nothing, A "
                + "Small Rock to its hand (GILA_500p2t, the run-treasure-boss-find row), one or two draws from its own deck, a random "
                + "scripted Shaman card from the pool the run's generation filter allows for the boss, a copy of the damaged minion's "
                + "card, or one of the sixteen Dungeon Run treasures King Togwaggle's Magic Candle finds (admitted for a boss on "
                + "catalog runs). Everything lands in the boss's own hand, which it knows exactly and the run player sees as a count; "
                + "the target is a public board minion (c-enc-15 section M; the enc15 bosspilot modes)",
            Ids = new[] { "DALA_BOSS_29p" },
        },
        new AdmissionRule
        {
            // Player | Boss: no player role offers one (Jepetto's deck lists Hoard of Toys, A Tale of Kings summons the rat), but the
            // run player can hold one (a steal, a copy, a control change), and it resolves the same there. Never generated.
            NeverGenerated = true,
            Family = "boss-card", Admit = AdmitScope.Player | AdmitScope.Boss, Lift = NoneBossOnly,
            Reason = "a Dalaran Heist boss card whose effect is public board and hero resolution, the same in either player's hands: "
                + "Hoard of Toys summons 1/1 copies of five random scripted minions from the generation pool its caster's filter allows "
                + "(DALA_BOSS_25t, Toyification) and Underbelly Rat replaces its controller's dead hero, the eldest (first to enter play) first, giving the "
                + "hero its Health; a Silenced rat replaces nothing (DALA_BOSS_30t, EncounterRules.ResolveUnderbellyRats: for The Rat King a new hero through his row's "
                + "script form; a run player who controls one keeps the hero entity). No hidden zone is read or written (c-enc-15 "
                + "sections I and N; the enc15 bosspilot modes)",
            Ids = new[] { "DALA_BOSS_25t", "DALA_BOSS_30t" },
        },
        new AdmissionRule
        {
            // The legacy verdict stays the run player's only (no legacy boss holds one); a catalog run admits a boss too. The id
            // left the core run-treasure row (Tess Greymane's tokens) for this one.
            Family = "run-treasure-boss-find", Admit = AdmitScope.Player, Lift = RunTreasureLift,
            Reason = "treasure: only the player can hold it; public effects the engine resolves, so the boss's legal moves and search see the result (c-treasures-F3 T5-T22)",
            CatalogAdmit = AdmitScope.Player | AdmitScope.Boss, CatalogLift = SmallRockBossLift,
            CatalogReason = "A Small Rock, Tess Greymane's Scavenge token (\"Deal 1 damage.\"), which Mama Diggs's Excavate also finds (the "
                + "wiki's list): a targeted 1-damage spell, public resolution, the same for a boss as for the run player (c-enc-15 "
                + "section M; c-treasures-F3 T4 and T17 for the run player)",
            Ids = new[] { "GILA_500p2t" },
        },
    };
}
