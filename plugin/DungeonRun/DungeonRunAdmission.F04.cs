// Phase 8, family 04 (collectibles C: Uldum ULD_282..ULD_728): the admission rows of the cards it scripts
// (a method the table concatenates: DungeonRunAdmission.BuildRules). ULD_293 Cloud Prince and ULD_726 Ancient Mysteries
// keep their phase-2 row (secret-from-deck, DungeonRunAdmission.cs).
internal static partial class DungeonRunAdmission
{
    /// <summary>The six Uldum Quests' boss scope: no pending work, a boss simply never holds one.</summary>
    private const string NoneBossQuest = "none: a boss's hero power is its encounter's identity (forms, passives, shrines read it); no boss deck "
        + "lists a Quest and the boss's generation never yields one";

    private static AdmissionRule[] RulesF04() => new[]
    {
        new AdmissionRule
        {
            Family = "quest-hero-power", Admit = AdmitScope.Player, Lift = NoneBossQuest,
            Reason = "an Uldum Quest whose reward replaces its owner's hero power (Heart of Vir'naal, Ancient Blades, Emperor Wraps, "
                + "Ascendant Scroll, Anraphet's Core, Obelisk's Eye): the run player's only. The Quest sits revealed in its owner's Quest "
                + "slot with public progress tags, so the boss's fair view and its search clones carry it; every progress event is "
                + "public (a card played, a spell cast, a hero attack, Health restored) or a count of cards entering the owner's hand "
                + "that the engine resolves and the boss never reads by id (Bazaar Burglary). The reward is the engine's hero power "
                + "replacement for this match only: the run's power record is never written (UldumRules.Advance; c-collectibles-C "
                + "Q1-Q22, the f04-player bosspilot mode)",
            Ids = new[] { "ULD_291", "ULD_326", "ULD_431", "ULD_433", "ULD_711", "ULD_724" },
        },
        new AdmissionRule
        {
            Family = "own-deck-insert", Admit = AdmitScope.Any, Lift = Outside,
            Reason = "shuffles cards the owner chose from public information into the owner's own deck: Shadow of Death three Shadows of "
                + "the targeted board minion's card (each summons a plain copy when drawn, then draws: UldumRules.SummonWhenDrawn), Body "
                + "Wrapper a Discovered copy of one of the owner's own dead minions (the graveyard is public). The opponent sees a deck "
                + "count, as for every shuffle; a boss knows what it shuffled into its own deck and samples the order "
                + "(c-collectibles-C D1-D9, the f04 bosspilot modes)",
            Ids = new[] { "ULD_286", "ULD_727" },
        },
        new AdmissionRule
        {
            Family = "own-deck-pull", Admit = AdmitScope.Any, Lift = Outside,
            Reason = "draws or summons minions from the owner's own deck by a printed property (Salhet's Pride: two 1-Health minions; "
                + "Tip the Scales: up to seven Murlocs); the deck is a known multiset with a sampled order for its owner, as for every "
                + "from-own-deck card, and the result is public (c-collectibles-C P1-P4, the f04 bosspilot modes)",
            Ids = new[] { "ULD_438", "ULD_716" },
        },
        new AdmissionRule
        {
            Family = "own-hand-rewrite", Admit = AdmitScope.Any, Lift = Outside,
            Reason = "enchants cards in its owner's own hand (Anka: each Deathrattle minion becomes a 1/1 that costs (1); History Buff: "
                + "+1/+1 to a random minion; Scarlet Webweaver: a random Beast costs (5) less; Naga Sand Witch: spells cost (5)); the "
                + "owner knows its own hand, the opponent sees no card of it and learns the change when the card is played "
                + "(c-collectibles-C H1-H8, the f04 bosspilot modes)",
            Ids = new[] { "ULD_288", "ULD_290", "ULD_410", "ULD_435" },
        },
        new AdmissionRule
        {
            Family = "scripted-generation", Admit = AdmitScope.Any, Lift = Outside,
            Reason = "generates random cards from the run's own generation pool: the format's cards behind the match's generation "
                + "filters (RunMatchPlan.ExcludedGenerated and the boss's list), scripted cards only (UldumRules.ScriptedPool), with the "
                + "roll reported as a random event so the boss's search re-plans after it: a 1-Cost minion (Jar Dealer), a minion of "
                + "each hand spell's Cost summoned (King Phaoris), a minion of another class (Bazaar Mugger), a Mage minion (Dune "
                + "Sculptor) (c-collectibles-C G1-G8, the f04 bosspilot modes)",
            Ids = new[] { "ULD_282", "ULD_304", "ULD_327", "ULD_329" },
        },
        new AdmissionRule
        {
            Family = "public-board", Admit = AdmitScope.Any, Lift = Outside,
            Reason = "a public board effect the engine resolves for either side with no hidden zone read or written: Oasis Surger's "
                + "Choose One, Plague of Wrath, Armored Goon, Swarm of Locusts, Penance, Plague of Death, Bloodsworn Mercenary's copy, "
                + "Colossus of the Moon (Divine Shield, Reborn) and Subdue (c-collectibles-C B1-B16, the f04 bosspilot modes; Plague of "
                + "Wrath and Bloodsworn Mercenary also in Icarax's shipped deck)",
            Ids = new[] { "ULD_292", "ULD_707", "ULD_709", "ULD_713", "ULD_714", "ULD_718", "ULD_720", "ULD_721", "ULD_728" },
        },
    };
}
