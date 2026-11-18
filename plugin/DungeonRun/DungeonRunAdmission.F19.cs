// Phase 8, family 19 (Tombs of Terror bosses A: eighteen encounters ULDA_BOSS_01-18 and Sinkhole's Sand Trap): the admission
// rows of the cards it scripts (a method the table concatenates: DungeonRunAdmission.BuildRules). The hero powers are a boss's
// only; Sand Trap lives in the run player's deck (Sand Surge shuffles it there), so it is admitted on both sides and never
// generated. The family names are the family's own (boss-tombs-*), so later Tombs families can add rows of their own names
// without moving this family's counts. The tests named in the reasons are the suite c-enc-19 and the enc19 bosspilot modes.
internal static partial class DungeonRunAdmission
{
    private static AdmissionRule[] RulesF19() => new[]
    {
        new AdmissionRule
        {
            Family = "boss-tombs-board-power", Admit = AdmitScope.Boss, Lift = NoneBossPower,
            Reason = "a clickable Tombs of Terror boss hero power whose whole effect is public board and hero state the engine resolves: "
                + "Boom Bullets summons a Goblin Bomb and gives every other Goblin Bomb +1 Attack (ULDA_BOSS_01p), Venomous Stinger marks "
                + "a minion that is destroyed when it next takes damage (ULDA_BOSS_05p; a run shrine goes dormant as from any destroy), "
                + "Druid of the Rat gives the boss's minions +1/+1 (ULDA_BOSS_07p), Armor Repairs gives the boss 4 Armor below 10 Armor, "
                + "else 2 (ULDA_BOSS_09p), Snakeshot summons a Poisonous 1/1 Snake (ULDA_BOSS_13p), Puffertrooper gives the boss's "
                + "Murlocs Rush (ULDA_BOSS_15p), Spreading the Curse summons a Leper Gnome with Rush (ULDA_BOSS_17p) and Plagued Horde "
                + "gives a minion \"Deathrattle: Deal 2 damage to the enemy hero\" (ULDA_BOSS_18p). A power that would do nothing is not "
                + "offered, by a condition that reads only the public boards; no hidden zone is read "
                + "(c-enc-19 sections A, E, G, I, M, O, Q, R; the enc19 bosspilot modes)",
            Ids = new[] { "ULDA_BOSS_01p", "ULDA_BOSS_05p", "ULDA_BOSS_07p", "ULDA_BOSS_09p", "ULDA_BOSS_13p", "ULDA_BOSS_15p", "ULDA_BOSS_17p",
                "ULDA_BOSS_18p" },
        },
        new AdmissionRule
        {
            Family = "boss-tombs-graveyard-power", Admit = AdmitScope.Boss, Lift = NoneBossPower,
            Reason = "a clickable Tombs of Terror boss hero power that summons for the boss a new copy of a minion that died this game, "
                + "read from the graveyards, which are public zones: Mummy Ritual a random dead minion of the boss's with Reborn on its "
                + "card, at 1 Health (ULDA_BOSS_02p), Belch a random dead minion of either player (ULDA_BOSS_04p). A dead run shrine is a "
                + "corpse and never comes back (ShrineRules.IsCorpse); each is offered only while such a minion exists and the board has "
                + "room, both public facts (c-enc-19 sections B, D; the enc19 bosspilot modes)",
            Ids = new[] { "ULDA_BOSS_02p", "ULDA_BOSS_04p" },
        },
        new AdmissionRule
        {
            Family = "boss-tombs-own-hand-power", Admit = AdmitScope.Boss, Lift = NoneBossPower,
            Reason = "a clickable Tombs of Terror boss hero power that writes the boss's own hand, which the boss knows exactly and the run "
                + "player sees as a count: Well Equipped adds a random scripted Rogue spell (no Quest, no Secret) or weapon the run's "
                + "generation filter allows for the boss (ULDA_BOSS_10p) and Brick by Brick Discovers an Elemental of the boss's class or "
                + "neutral behind the same filter, the boss's AI picking, and the pick costs (2) less (ULDA_BOSS_12p). Offered while the "
                + "hand has room; the run player's hidden zones are neither read nor written (c-enc-19 sections J, L; the enc19 bosspilot "
                + "modes)",
            Ids = new[] { "ULDA_BOSS_10p", "ULDA_BOSS_12p" },
        },
        new AdmissionRule
        {
            Family = "boss-tombs-passive", Admit = AdmitScope.Boss, Lift = NoneBossPower,
            Reason = "a passive Tombs of Terror boss hero power started at match start (EncounterRules.ActivatePower) that reacts to a "
                + "public event of the boss's: E.V.I.L. Promotion gives a Lackey the boss plays +N/+N, N rising with each one (ULDA_BOSS_03p; "
                + "the count is the power's own public script number), \"Confiscated\" Goods equips a random scripted weapon after the boss's "
                + "hero kills a minion by attacking it (ULDA_BOSS_06p), Unstable Weaponry deals 2 damage to a random enemy after a Mech is "
                + "summoned for the boss (ULDA_BOSS_11p), Sword Dance shuffles a copy of the boss's destroyed weapon, enchantments kept, "
                + "into the boss's own deck as a public shuffle (ULDA_BOSS_14p: the boss knows what joined its deck, the run player sees "
                + "the card go in) and Under Wraps gives Reborn to a minion the boss plays (ULDA_BOSS_16p). The trigger is a public play, "
                + "summon, attack or weapon loss, never the identity of a hidden card (c-enc-19 sections C, F, K, N, P; the enc19 "
                + "bosspilot modes)",
            Ids = new[] { "ULDA_BOSS_03p", "ULDA_BOSS_06p", "ULDA_BOSS_11p", "ULDA_BOSS_14p", "ULDA_BOSS_16p" },
        },
        new AdmissionRule
        {
            Family = "boss-tombs-player-deck-insert", Admit = AdmitScope.Boss, Lift = NoneBossPower,
            Reason = "Sinkhole's Sand Surge, \"Shuffle a Sand Trap into your opponent's deck that swallows cards when drawn\": one Sand "
                + "Trap enters the run player's deck as a public shuffle (the run player sees the card go in; the boss's fair view keeps "
                + "that whole deck masked, the Sand Trap included, so the boss's search under-knows and never learns a hidden card); "
                + "the run deck records never hold it (a match card). The wiki marks it Auto-cast (the card carries AI_MUST_PLAY): it is "
                + "cast by rule at the start of the boss's turn, after the draw, never an option of the boss's AI (family 09's rule-cast "
                + "powers), while that deck, whose count is public, has room (c-enc-19 section H; the enc19 bosspilot modes)",
            Ids = new[] { "ULDA_BOSS_08p" },
        },
        new AdmissionRule
        {
            // Player | Boss: the boss's power puts it into the run player's deck, and the run player draws it (a copy or a steal of
            // a boss's deck card could also hold one). Never generated: no generation pool yields it.
            NeverGenerated = true,
            Family = "boss-tombs-card", Admit = AdmitScope.Player | AdmitScope.Boss, Lift = NoneBossOnly,
            Reason = "Sand Trap (ULDA_030), \"Casts When Drawn: Remove the top 2 cards of your deck\": when its holder draws it, the top two "
                + "cards of the holder's own deck go to the holder's graveyard, a public zone (both sides see the two removed cards; the "
                + "rest of the deck stays hidden and two cards shorter), then the holder draws a card. It never reads the other side's "
                + "hidden zones (c-enc-19 section H; the enc19 bosspilot modes)",
            Ids = new[] { "ULDA_030" },
        },
    };
}
