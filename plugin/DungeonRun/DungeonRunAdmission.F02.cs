// Phase 8, family 02 (unscripted collectibles A): the admission rows of the collectible cards it scripts. All are admitted in
// every scope (the run player's drafts and buckets, an imported boss deck, generation): each row states what the boss's fair
// search may read and names the regression test (the c-collectibles-A suite and the collectA bosspilot fixture mode, where
// the fair AI plays the cards as the boss). AT_079 Mysterious Challenger and DAL_573 Commander Rhyssa keep their rows in the
// core table (secret-from-deck: they work on the owner's own Secrets and generate none).
internal static partial class DungeonRunAdmission
{
    /// <summary>The lift of a row that rejects no scope.</summary>
    internal const string NoneCollectible = "none: a scripted collectible, admitted in every scope";

    private static AdmissionRule[] RulesF02() => new[]
    {
        new AdmissionRule
        {
            Family = "collectible-public", Admit = AdmitScope.Any, Lift = NoneCollectible,
            Reason = "a collectible whose whole effect is public engine resolution on the boards, the heroes or its owner's own hand, so the "
                + "boss's legal moves and its search read the result and no hidden zone of the other side is read or written: all enemy "
                + "minions' Attack set to 1 (AT_081), the next Hero Power (2) cheaper (AT_115, a player enchantment), +1 Attack after a "
                + "friendly minion attacks (GIL_655), a Health swap with a chosen minion (GVG_014), Stealth when its owner summons a Pirate "
                + "(GVG_025), Attack doubled when damaged (GVG_049), 1 damage to random minions until one dies with the original's bound of "
                + "80 hits (GVG_050), +1/+1 per enemy Deathrattle minion (GVG_097), a random keyword for each other friendly minion "
                + "(GVG_107), three Mechs merged into V-07-TR-0N at turn start (GVG_111), combat damage to the defender's neighbours "
                + "(GVG_113), a Scarab at turn end (LOE_009), a vanilla Taunt (LOE_022), +3/+3 on death (LOE_061), no attack unless it "
                + "is the only minion in play (LOE_107), and a random invention with its public turn triggers (EX1_112: Mekka1-4). "
                + "Every random pick uses the game RNG and is reported as a random event, so the boss's search re-plans after it "
                + "(c-collectibles-A; the collectA bosspilot fixture mode)",
            Ids = new[] { "AT_081", "AT_115", "EX1_112", "GIL_655", "GVG_014", "GVG_025", "GVG_049", "GVG_050", "GVG_097", "GVG_107", "GVG_111",
                "GVG_113", "LOE_009", "LOE_022", "LOE_061", "LOE_107" },
        },
        new AdmissionRule
        {
            Family = "collectible-own-deck", Admit = AdmitScope.Any, Lift = NoneCollectible,
            Reason = "draws from its owner's own deck and changes the drawn cards' Costs in its owner's hand: a card drawn by the owner's "
                + "Hero Power costs (0) (AT_027: the draw is read from the executing task's source, never from a card name), a minion and "
                + "a spell are drawn and their Costs swapped (BOT_436), a drawn Beast costs (4) less (GVG_017). The boss's own deck is a "
                + "known multiset with a sampled order, as for every draw; the run player's draws stay hidden cards whose Cost the boss "
                + "never reads (c-collectibles-A AT_027, BOT_436, GVG_017; the collectA bosspilot fixture mode)",
            Ids = new[] { "AT_027", "BOT_436", "GVG_017" },
        },
        new AdmissionRule
        {
            Family = "collectible-generated", Admit = AdmitScope.Any, Lift = NoneCollectible,
            Reason = "yields cards through the run's generation filter (Game.BlocksGenerated, per receiving side) from scripted cards only: "
                + "a Discover of a 3-Cost card (LOE_029) or a Beast (LOE_047) of the owner's classes and neutral, a Discover of one of "
                + "three fixed artifacts (LOE_092: Lantern of Power, Timepiece of Horror, Mirror of Doom), a transform into a random minion "
                + "of the same Cost (GVG_108; a board shrine goes dormant instead, ShrineRules.InterceptTransform), a random collectible "
                + "weapon for each player (GVG_119, each side under its own list). A boss's Discover is its own choice among public "
                + "offers; the results are public board, weapon or own-hand changes (c-collectibles-A LOE_029, LOE_047, LOE_092, GVG_108, "
                + "GVG_119; the collectA bosspilot fixture mode)",
            Ids = new[] { "GVG_108", "GVG_119", "LOE_029", "LOE_047", "LOE_092" },
        },
        new AdmissionRule
        {
            Family = "hidden-hand-discover", Admit = AdmitScope.Any, Lift = NoneCollectible,
            Reason = "Madame Lazul: a Discover among copies of up to three different cards of the opponent's hand. Its owner is shown exactly "
                + "the cards the text allows (the real game's Discover; the opponent's hand itself is unchanged, the picked copy is a new "
                + "card in the owner's hand). In the boss's fair-view projection the run player's hand holds no known card, so the "
                + "Battlecry offers nothing there: the search never reads the hidden hand, and the offers the boss later sees are the "
                + "revealed result of a random event it re-plans after. Played by the run player, the boss learns only that a card "
                + "joined the player's hand (c-collectibles-A DAL_729; the collectA bosspilot fixture mode)",
            Ids = new[] { "DAL_729" },
        },
        new AdmissionRule
        {
            Family = "hidden-deck-summon", Admit = AdmitScope.Any, Lift = NoneCollectible,
            Reason = "Duel!: a random minion of each player's deck is summoned and the caster's attacks the other. Both minions become "
                + "public board entities as they leave the decks (the boss's own-deck ledger sees its own card leave; the run player's "
                + "deck stays a hidden zone that is one card shorter). In the boss's fair-view projection the run player's deck holds no "
                + "known minion, so there only the boss's own minion arrives: the search never reads the hidden deck, and the real "
                + "summon is a random event it re-plans after (c-collectibles-A DAL_731; the collectA bosspilot fixture mode)",
            Ids = new[] { "DAL_731" },
        },
        new AdmissionRule
        {
            Family = "wrong-target", Admit = AdmitScope.Any, Lift = NoneCollectible,
            Reason = "Ogre Warmaul, \"50% chance to attack the wrong enemy\": when its wielder's attack is prepared and another enemy "
                + "character could be hit, a coin of the game RNG decides and a miss moves the attack to one of the others "
                + "(Actions.EncounterRules.WrongTarget50, the rule DALA_861 and DALA_BOSS_14t reuse). All inputs are public boards; the "
                + "roll is reported as a random event, so the boss's search treats the attack as a chance node and re-plans after it "
                + "(c-collectibles-A GVG_054; the collectA bosspilot fixture mode)",
            Ids = new[] { "GVG_054" },
        },
        new AdmissionRule
        {
            Family = "hero-damage-cap", Admit = AdmitScope.Any, Lift = NoneCollectible,
            Reason = "Animated Armor, \"Your hero can only take 1 damage at a time\": an engine rule read from the active minion on its "
                + "owner's board (Actions.EncounterRules.HeroDamageCap, the rule DALA_BOSS_65p reuses), applied in Character.TakeDamage "
                + "before Armor, in the game and in every clone, so the boss's search and its lethal checks see the cap on either side "
                + "(c-collectibles-A LOE_119; the collectA bosspilot fixture mode)",
            Ids = new[] { "LOE_119" },
        },
        new AdmissionRule
        {
            Family = "spell-recast", Admit = AdmitScope.Any, Lift = NoneCollectible,
            Reason = "Djinni of Zephyrs: after its owner casts a spell on another friendly minion, a copy is cast on it by the engine's "
                + "recast rule (Actions.RecastRules, as TRLA_138: a fresh copy, target locked, a recast never recasts). The spell and "
                + "both targets are public (c-collectibles-A LOE_053; the collectA bosspilot fixture mode)",
            Ids = new[] { "LOE_053" },
        },
        new AdmissionRule
        {
            Family = "opening-hand", Admit = AdmitScope.Any, Lift = NoneCollectible,
            Reason = "Hex Lord Malacrass: copies of its owner's own opening hand (recorded after both mulligans in every run match, "
                + "Actions.EncounterRules.CapturesOpening) join its owner's hand. The boss knows its own opening hand; the run player's "
                + "is removed from the boss's fair view (TurnQueueRules.RedactOpening), so the search adds nothing for the player and "
                + "the boss later sees only a hand count (c-collectibles-A TRL_318; the collectA bosspilot fixture mode)",
            Ids = new[] { "TRL_318" },
        },
        new AdmissionRule
        {
            Family = "generated-definition", Admit = AdmitScope.Any, Lift = NoneCollectible,
            Reason = "Swampqueen Hagatha: two Discovers among the scripted Shaman spells the run's generation filter leaves (a targeted "
                + "first spell leaves untargeted ones for the second), then a Drustvar Horror joins its owner's hand: a generated, "
                + "immutable definition of the client's DAL_431t whose Battlecry casts the two spells (DungeonRunEncounters.Horror; the "
                + "spells are its modular parts, so clones and the search carry them). The casts are effect casts of public spells "
                + "(c-collectibles-A DAL_431; the collectA bosspilot fixture mode)",
            Ids = new[] { "DAL_431" },
        },
    };
}
