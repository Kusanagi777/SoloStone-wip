// Phase 8, family 06 (the Tombs of Terror anomalies and plague twists): the admission rows of the rule cards it scripts and
// of the tokens they place. A rule is admitted in scope Rule only (never a deck card, a boss card or a generation result);
// each row states what the boss's fair search may read and names its regression tests: the c-rules-06 suite (each rule's
// text on both sides in real engine games through DungeonRunSetup, and the run layer for the two run-scope rules) and the
// rules06 bosspilot mode (the fair AI pilots the boss under the rule through the run setup path).
internal static partial class DungeonRunAdmission
{
    private static AdmissionRule[] RulesF06() => new[]
    {
        new AdmissionRule
        {
            NeverGenerated = true,
            Family = "global-rule-symmetric", Admit = AdmitScope.Rule, Lift = NoneRule,
            Reason = "a global rule both players play under for the whole match: its player enchantment is attached to each controller at "
                + "match start (DungeonRunGlobalRules.Apply), a public effect the engine resolves for both sides on the boards and the "
                + "heroes, so the boss's legal moves and its search see it and no hidden zone is read or written: a played minion gives "
                + "the opposing player a Coin (ULDA_702: a public card in a hand; a full hand takes none), a hero is Immune while its "
                + "player controls a minion that is not a permanent (ULDA_706: Violet Illusionist's hero aura, checked at every aura "
                + "update; never on a run or a boss with a board shrine, or a boss whose own power makes its hero Immune: "
                + "schedule-v1.json), each minion's Attack is always its Health (ULDA_709: Lightspawn's rule for both boards), all "
                + "minions have Charge (ULDA_710) or Windfury (ULDA_711: board auras), a played minion gives the other side a 1/1 copy "
                + "of its card (ULDA_712: a plain minion of the card, summoned, no Battlecry), every minion takes 1 damage at the start "
                + "of every turn (ULDA_713), heroes take double damage (ULDA_714: a private controller tag the damage step reads before "
                + "Armor; never with a boss whose power caps its hero's damage: schedule-v1.json), a player gains 2 Armor after casting "
                + "a spell (ULDA_724), each player's right-most minion attacks a random enemy at the end of that player's turn "
                + "(ULDA_802: Mass Hysteria's forced attack, a reported random event), and a damaged minion that is still in play gains "
                + "+1 Attack (ULDA_803) (c-rules-06 A, C, F, G, H, I, J, K, P, R, S; the rules06 bosspilot mode)",
            Ids = new[] { "ULDA_702", "ULDA_706", "ULDA_709", "ULDA_710", "ULDA_711", "ULDA_712", "ULDA_713", "ULDA_714", "ULDA_724", "ULDA_802", "ULDA_803" },
        },
        new AdmissionRule
        {
            NeverGenerated = true,
            Family = "global-rule-hidden", Admit = AdmitScope.Rule, Lift = NoneRule,
            Reason = "a global rule that changes a hidden zone by a public effect, so neither side learns a card it could not already "
                + "know: a Bomb (BOT_511t, Seaforium Bomber's) is shuffled into the deck of a dead minion's player (ULDA_705) and a new "
                + "card of the dead minion's card into it (ULDA_707), both as the engine's public generated shuffle (both sides see "
                + "what went in, neither a position; the owner's own-deck ledger takes the card from the shown shuffle); the Cost of "
                + "every card in a damaged hero's hand is set to a random 0 to 10 (ULDA_708: the boss knows its own hand's Costs, the "
                + "run player's hidden cards stay unknown cards in its fair view and their Costs are learnt only when they are played; "
                + "an Echo or repeatable card is never set below (1)); both decks are shuffled together and dealt back in halves after "
                + "both mulligans (ULDA_721: the card entities change sides as hidden residence changes, so each owner's ledger loses "
                + "unknown cards and gains unknown ones, both sides' deck beliefs are reset, and a card the run's generation filter "
                + "does not let a side be given stays with its owner: a boss is never handed a card the table rejects for a boss; "
                + "never with a boss whose deck is replaced, a relay or hero-switch fight, a boss that plays Secrets from its deck, or "
                + "the deck-insert rules: schedule-v1.json); and Surprise! Murlocs! are shuffled into each deck after both mulligans "
                + "(ULDA_801: one more per fight of the run, at most 8; each owner's deck list is recorded for its own-deck ledger: "
                + "EncounterRules.DeckRewritten; never with a boss form that replaces its deck or a relay fight) "
                + "(c-rules-06 B, D, E, N, Q; the rules06 bosspilot mode)",
            Ids = new[] { "ULDA_705", "ULDA_707", "ULDA_708", "ULDA_721", "ULDA_801" },
        },
        new AdmissionRule
        {
            NeverGenerated = true,
            Family = "global-rule-start", Admit = AdmitScope.Rule, Lift = NoneRule,
            Reason = "a global rule that changes how both players start (RuleHooks; no client \"Player Enchantment\" card exists for it): "
                + "each player starts with 9 cards in hand, dealt before the mulligan (ULDA_715 Bountiful: the first player's bonus "
                + "may be 6, GameConfig.MaxFirstOpeningHandBonus; the boss's Coin is one of its nine; never with the opening-hand "
                + "modifier: schedule-v1.json), each player starts with Marin the Fox's Master Chest on its own side (ULDA_719: "
                + "LOOT_357l, a 0/8 whose public Deathrattle gives the other player one of Marin's four treasures, cards the table "
                + "already admits with Marin), and each player starts with an Eternal Tomb (ULDA_804: an untouchable permanent that "
                + "holds a random scripted minion of the run's generation pool for its side and summons it after as many friendly "
                + "deaths as the minion costs, then holds a new one; the held card and the deaths left are public tags, every roll is "
                + "a reported random event, and one Tomb summons at most 7 minions a turn). The boards are placed after both "
                + "mulligans with the encounter's start block; never two board-start rules or a boss with a start board together "
                + "(schedule-v1.json) (c-rules-06 L, M, T; the rules06 bosspilot mode)",
            Ids = new[] { "ULDA_715", "ULDA_719", "ULDA_804" },
        },
        new AdmissionRule
        {
            NeverGenerated = true,
            Family = "global-rule-run", Admit = AdmitScope.Rule, Lift = NoneRule,
            Reason = "a run-scope rule the run layer carries (schedule-v1.json effect \"run\": no match script, never in a match plan's "
                + "rule list): Battle Scars sets the run hero's Health to 100 and carries its damage from fight to fight "
                + "(ULDA_720: DungeonRunState.HeroDamage through RunMatchPlan.PlayerHeroHealth and PlayerCarriedDamage; public hero "
                + "Health the boss reads like any other; never onto a fight-only hero), and Multiclassing lets the card drafts offer "
                + "the loot of every class (ULDA_729: DungeonRun.LootClasses; the boss's Secret prior then covers every class the run "
                + "deck may hold: DungeonRun.MatchClasses) (c-rules-06 U, V; the rules06 bosspilot mode)",
            Ids = new[] { "ULDA_720", "ULDA_729" },
        },
        new AdmissionRule
        {
            NeverGenerated = true,
            Family = "global-rule-token", Admit = AdmitScope.Rule, Lift = NoneRuleToken,
            Reason = "a token only a global rule places, in public: Surprise! Murlocs! (ULDA_801t: \"Casts When Drawn\", summons 1 to 3 "
                + "random scripted Murlocs of the run's generation pool for the player who drew it, a reported random event) and the "
                + "Eternal Tomb (ULDA_804t: an untouchable permanent whose held minion and deaths left are public tags) "
                + "(c-rules-06 Q, T)",
            Ids = new[] { "ULDA_801t", "ULDA_804t" },
        },
    };
}
