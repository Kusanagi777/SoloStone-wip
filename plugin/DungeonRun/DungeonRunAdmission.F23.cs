// Phase 8, family 23 (the Monster Hunt nemeses: Infinite Toki, Glinda Crowskin, Lord Godfrey, Captain Shivers, beside Hagatha
// the Witch, whose rows family 01 wrote): the admission rows of the cards it scripts (a method the table concatenates:
// DungeonRunAdmission.BuildRules). The hero powers are a boss's only; the boss deck cards are listed by boss decks only and
// are admitted in the run player's hands too, where a steal, a copy or a control change can put one. Alternate Timeline is
// rejected (a game restart, phase 9). The tests named in the reasons are the suite c-enc-23 and the enc23 bosspilot modes.
internal static partial class DungeonRunAdmission
{
    internal const string RunTreasureNemesisLift = "none: a run treasure; among bosses only a Monster Hunt nemesis's deck lists it";

    private static AdmissionRule[] RulesF23() => new[]
    {
        new AdmissionRule
        {
            Family = "nemesis-board-power", Admit = AdmitScope.Boss, Lift = NoneBossPower,
            Reason = "a nemesis's clickable hero power that resolves on the public boards: Chronoacceleration gives a friendly minion "
                + "Mega-Windfury and destroys it at the end of the turn (GILA_BOSS_48p with its client enchantment GILA_BOSS_48e: Power "
                + "Overwhelming's end-of-turn rule), Pistol Barrage deals 2 damage to a minion and the minions next to it (GILA_BOSS_59p, "
                + "Cone of Cold's adjacency, no Spell Damage). Offered by the play requirements only (a friendly or any minion), which "
                + "read the exact boards; no hidden zone is read or written (c-enc-23 A1-A8, C1-C5; the enc23 bosspilot mode)",
            Ids = new[] { "GILA_BOSS_48p", "GILA_BOSS_59p" },
        },
        new AdmissionRule
        {
            Family = "nemesis-passive-immune", Admit = AdmitScope.Boss, Lift = NoneBossPower,
            Reason = "Glinda Crowskin's passive False Heart: her hero is Immune while she controls a minion that is not a permanent or "
                + "dormant one (family 06's hero aura EncounterRules.HeroImmuneWhile, the rule of ULDA_706 No-Face for one side, "
                + "re-checked at every aura update), a public board fact both sides' searches replay; started at match start by "
                + "EncounterRules.ActivatePower, never offered as an option. The row carries the flag heroImmune, which keeps ULDA_706 "
                + "away from her fight (c-enc-23 B1-B6; the enc23 bosspilot mode)",
            Ids = new[] { "GILA_BOSS_49p" },
        },
        new AdmissionRule
        {
            Family = "nemesis-auto-cast", Admit = AdmitScope.Boss, Lift = NoneBossPower,
            Reason = "Captain Shivers' Plunder (0), \"Draw a weapon from your deck\", auto-cast as on the wiki (the client card is "
                + "AI_MUST_PLAY): at the start of his turn, while his deck holds a weapon and his hand has room, one weapon of his own "
                + "deck is drawn by the game RNG (family 09's AutoCast rule: never an option of the AI, so no choice of it depends on "
                + "the deck order the search samples); the run player sees a draw. In practice it draws back Kingsbane, which his row "
                + "equips at the start and whose Deathrattle shuffles it in (c-enc-23 D1-D6; the enc23 bosspilot mode)",
            Ids = new[] { "GILA_BOSS_60p" },
        },
        new AdmissionRule
        {
            // Player | Boss: no player role offers one (boss decks list them), but the run player can take one from a boss (a
            // steal, a copy, Crowskin Pact's Faithful changing sides), and its effect resolves the same there. Never generated.
            NeverGenerated = true,
            Family = "nemesis-hunt-card", Admit = AdmitScope.Player | AdmitScope.Boss, Lift = NoneBossOnly,
            Reason = "a Monster Hunt boss deck card whose effect is public board and hero resolution or a change to its caster's own hand "
                + "and deck, the same in either player's hands: Crowskin Faithful draws its controller a card and shuffles a new "
                + "Faithful into that controller's deck when it dies (GILA_BOSS_49t), Crowskin Pact transforms a minion into a "
                + "Faithful and takes control of it (GILA_BOSS_49t2; a run shrine picked goes dormant instead and never changes "
                + "sides), Cursed Crewmember draws two cards and shuffles a copy of its caster's hand into that caster's deck "
                + "(GILA_BOSS_60t: Dead Man's Hand's rule; the caster knows its own hand, the opponent sees a deck count), and "
                + "Witchwood's Touch draws 1, 2 or 3 cards and gains 6, 8 or 10 Armor (GILA_BOSS_99t, 99t2, 99t3). No hidden zone of "
                + "the other side is read or written (c-enc-23 B7-B12, D7-D10, E1-E9; the enc23 and enc23-cards bosspilot modes)",
            Ids = new[] { "GILA_BOSS_49t", "GILA_BOSS_49t2", "GILA_BOSS_60t", "GILA_BOSS_99t", "GILA_BOSS_99t2", "GILA_BOSS_99t3" },
        },
        new AdmissionRule
        {
            // The legacy verdict stays the run player's only (no legacy boss holds one); a catalog run admits a boss too. The two
            // ids left the core run-treasure rows (Tess and Darius's, Shaw and Toki's) for this one, as family 09's Kobolds rows did.
            Family = "run-treasure-nemesis-deck", Admit = AdmitScope.Player, Lift = RunTreasureLift,
            Reason = "treasure: only the player can hold it; public effects the engine resolves, so the boss's legal moves and search see the result (c-treasures-F3, c-treasures-F4)",
            CatalogAdmit = AdmitScope.Player | AdmitScope.Boss, CatalogLift = RunTreasureNemesisLift,
            CatalogReason = "a Monster Hunt treasure that a nemesis's wiki deck lists (Hunter's Insight in Lord Godfrey's, Clockwork "
                + "Assistant in Infinite Toki's): a card whose effect is written for its owner and resolves the same for a boss as for "
                + "the run player: Hunter's Insight reduces the Cost of the cards in its owner's own hand by (3) (an enchantment on cards "
                + "its owner holds), Clockwork Assistant has +1/+1 for each spell its owner has cast this game (a public count). Each is "
                + "played by a boss in an engine game (c-enc-23 F1-F4; the enc23-cards bosspilot mode), on top of the run player's "
                + "tests (c-treasures-F3, c-treasures-F4)",
            Ids = new[] { "GILA_503", "GILA_907" },
        },
        new AdmissionRule
        {
            NeverGenerated = true,
            Family = "nemesis-rejected-restart", Admit = AdmitScope.None, Lift = TurnRestartLift,
            Reason = "Infinite Toki's Alternate Timeline, \"Start a new game, but your opponent's Health stays the same\": a game restart "
                + "inside a fight needs a run-owned restart that rebuilds the match, the client history and the bot state; left out of "
                + "her deck (data/dungeon-run/reference/encounters/F23.json \"rejected\")",
            Ids = new[] { "GILA_BOSS_48t" },
        },
    };
}
