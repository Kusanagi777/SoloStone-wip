// Phase 8, family 03 (unscripted collectibles B, Uldum ULD_131..ULD_274): the admission rows of the cards it scripts
// (a method the table concatenates: DungeonRunAdmission.BuildRules). Every card of the family is listed, so the ledger's
// ai_support column names its reason and its test; ULD_154 (secret-from-deck), ULD_236 (from-own-deck) and ULD_239
// (secret-card) keep their phase 2 rows in the core table. Tests: the scratchpad suite c-collectibles-B (B1-B79) and the
// bosspilot mode enc03 (fixture boss decks holding every card, the fair AI playing them).
internal static partial class DungeonRunAdmission
{
    /// <summary>A lift that names no pending work: the row admits every scope.</summary>
    private const string NoneAdmittedF03 = "none: admitted in every scope";

    private static AdmissionRule[] RulesF03() => new[]
    {
        new AdmissionRule
        {
            Family = "keyword-only", Admit = AdmitScope.Any, Lift = NoneAdmittedF03,
            Reason = "no script: the engine's Poisonous (ULD_194), Reborn (ULD_205, ULD_206, ULD_274), Rush (ULD_206) and Stealth "
                + "(ULD_274) keywords, read from the card's tags; public board state the boss's search plays through like any "
                + "minion (c-collectibles-B B2-B6, the enc03 bosspilot mode)",
            Ids = new[] { "ULD_194", "ULD_205", "ULD_206", "ULD_274" },
        },
        new AdmissionRule
        {
            Family = "public-effect", Admit = AdmitScope.Any, Lift = NoneAdmittedF03,
            Reason = "public board, hero and Mana effects resolved by the engine from public state, with no hidden zone read and no "
                + "generation: Hidden Oasis and its two options (a 6/6 Taunt token or 12 Health; ULD_135), Anubisath Defender's Cost "
                + "(0) after its owner played a spell that cost (5) or more this turn (the owner's own plays; ULD_138), Pharaoh's "
                + "Blessing (ULD_143), Sandstorm Elemental (ULD_158), Mogu Fleshshaper's Cost by the minions in play (ULD_169), "
                + "Vessina's Attack aura while her owner is Overloaded (ULD_173), Octosari's Deathrattle draw of 8 (the owner's own "
                + "deck; ULD_177), Phalanx Commander's Taunt aura (ULD_179), Khartut Defender (ULD_208), Wrapped Golem's Scarab "
                + "(ULD_215), Tomb Warden's copy (ULD_253), High Priest Amet's Health set on summon (ULD_262) and Sandhoof "
                + "Waterbearer's end-of-turn heal of a random damaged friendly character (the game RNG; ULD_270) "
                + "(c-collectibles-B B15-B21, B26-B27, B31-B35, B38, B47, B61, B64-B66, B70-B71, the enc03 bosspilot mode)",
            Ids = new[] { "ULD_135", "ULD_135a", "ULD_135b", "ULD_138", "ULD_143", "ULD_158", "ULD_169", "ULD_173", "ULD_177", "ULD_179", "ULD_208", "ULD_215",
                "ULD_253", "ULD_262", "ULD_270" },
        },
        new AdmissionRule
        {
            Family = "own-hand", Admit = AdmitScope.Any, Lift = NoneAdmittedF03,
            Reason = "works on its owner's own hand, which its owner knows: Ramkahen Wildtamer copies a random Beast of it (ULD_151), "
                + "Into the Fray (ULD_256) and Armagedillo at its owner's turn end (ULD_258) give its Taunt minions +2/+2. The "
                + "opponent sees a hand count and, for the buffs, nothing: a buff on a card the boss's fair view masks is not "
                + "applied in its search (Generic.AddEnchantmentBlock skips a masked card), and the stats are public once the "
                + "minion is played (c-collectibles-B B22-B23, B62-B63, the enc03 bosspilot mode)",
            Ids = new[] { "ULD_151", "ULD_256", "ULD_258" },
        },
        new AdmissionRule
        {
            Family = "own-graveyard", Admit = AdmitScope.Any, Lift = NoneAdmittedF03,
            Reason = "Psychopomp summons a random friendly minion that died this game and gives it Reborn: the owner's graveyard, a "
                + "public zone, read with the game RNG (a random event); a dead run board shrine is never in the pool "
                + "(ShrineRules.IsCorpse) (c-collectibles-B B67-B69, the enc03 bosspilot mode)",
            Ids = new[] { "ULD_268" },
        },
        new AdmissionRule
        {
            Family = "generated-pool", Admit = AdmitScope.Any, Lift = NoneAdmittedF03,
            Reason = "generates cards from the engine's generation pool for its side, scripted cards only: Plague of Murlocs "
                + "transforms every minion into a random Murloc, each side's from that side's pool (ULD_172; a run board shrine goes "
                + "dormant instead: ShrineRules.InterceptTransform), Frightened Flunky Discovers a Taunt minion (ULD_195), Vulpera "
                + "Scoundrel Discovers a spell or takes the mystery choice, a random spell of the same pool (ULD_209, ULD_209t). The "
                + "pools are read inside Game.GenerateFor, so a run match's exclusions hold (RunMatchPlan.ExcludedGenerated and the "
                + "boss's own list) and a side that may not generate Secrets is never offered one: a boss's Scoundrel never yields a "
                + "Secret; a catalog run's player may Discover a Secret of its own classes, which the boss's Secret prior already "
                + "holds (GeneratedSecretPool). Each roll is a random event (c-collectibles-B B28-B30, B36-B37, B39-B42, the enc03 "
                + "bosspilot mode)",
            Ids = new[] { "ULD_172", "ULD_195", "ULD_209", "ULD_209t" },
        },
        new AdmissionRule
        {
            Family = "random-cast", Admit = AdmitScope.Any, Lift = NoneAdmittedF03,
            Guard = "10 casts; a Box never casts a Box",
            Reason = "Puzzle Box of Yogg-Saron casts ten random scripted spells of the generation pool for its owner, each a new copy "
                + "with a random Choose One mode and a random legal target (DungeonRunContent.CastRandomly, the random cast of "
                + "Yogg-Saron, OG_134): never a Quest, a hidden-stat card or the Box itself, so one cast is at most ten spells, and it "
                + "stops when a hero is down. The pool is read inside Game.GenerateFor: a run match's exclusions hold, a boss and a "
                + "legacy run's player cast no Secret, and a catalog run's player may put a scripted Secret of any class into play, "
                + "as Yogg-Saron and the other random casts already can (the boss's Secret prior orders its search and never "
                + "certifies on a catalog run). Every cast is a public random event the boss's search samples "
                + "(c-collectibles-B B48-B52, the enc03 bosspilot mode)",
            Ids = new[] { "ULD_216" },
        },
        new AdmissionRule
        {
            Family = "hidden-reveal", Admit = AdmitScope.Any, Lift = NoneAdmittedF03,
            Reason = "Wild Bloodstinger summons a random minion from the opponent's hand for the opponent (Dirty Rat's pull, CFM_790) "
                + "and attacks it: the pulled card becomes a public minion by a public summon, and nothing else of that hand is "
                + "shown. Played by the run player it pulls from the boss's hand, which the boss knows. Played by the boss it pulls "
                + "from the run player's hidden hand: in the boss's fair view those cards are masked and are no minions, so its "
                + "search pulls nothing and reads no hidden identity; the real pull is a random event resolved in the real game, "
                + "after which the boss plans from the public board (c-collectibles-B B43-B46, the enc03 bosspilot mode)",
            Ids = new[] { "ULD_212" },
        },
        new AdmissionRule
        {
            Family = "quest-hero-power", Admit = AdmitScope.Any, Lift = NoneAdmittedF03,
            Reason = "Untapped Potential is a Quest in the legacy quest slot, public from the moment it is played: it counts its owner's "
                + "own turn ends with unspent Mana (public Mana), and at four replaces its owner's hero power with Ossirian Tear, a "
                + "passive power whose controller aura makes its owner's Choose One cards resolve both options (Fandral Staghelm's "
                + "rule; a public hero-power change, engine state in every clone; a hero card played later replaces it). No hidden "
                + "zone and no generation: generation never yields a Quest. A boss that completes it loses its own power for the "
                + "passive one, and the fair AI pilots it with that power (c-collectibles-B B7-B14, B79, the enc03 bosspilot mode)",
            Ids = new[] { "ULD_131", "ULD_131p" },
        },
    };
}
