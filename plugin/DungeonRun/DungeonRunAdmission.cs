using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.CardSets.DungeonRun;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
// The client assembly has a global Card type that shadows the engine's (see LocalNativeText.cs).
using EngineCard = SabberStoneCore.Model.Card;

/// <summary>Where a card may appear in a run match: in the run player's cards, in a boss's cards, or from generation.</summary>
[Flags]
internal enum AdmitScope
{
    None = 0,
    /// <summary>Run player: starter decks, bundles, powers, treasures and their tokens.</summary>
    Player = 1,
    /// <summary>Boss decks and boss hero powers.</summary>
    Boss = 2,
    /// <summary>Generated during a run match (Discover, random cards, random casts, transforms): GameConfig.ExcludedGeneratedIds.</summary>
    Generated = 4,
    Both = Player | Boss,
    Any = Player | Boss | Generated,
    /// <summary>
    /// Phase 8: a global rule (an anomaly or a twist) active on both players for a whole match (schedule-v1.json "rules").
    /// Outside <see cref="Any"/>: a rule card is never a deck card, a boss card or a generation result.
    /// </summary>
    Rule = 8,
}

/// <summary>One row of the admission table: a family of cards, where it is admitted, and what lifts a rejection.</summary>
internal sealed class AdmissionRule
{
    internal string Family, Reason, Lift, Requires;
    internal AdmitScope Admit;
    /// <summary>
    /// The scopes a catalog run (DungeonRunState.CatalogVersion 1 or later) admits, when its lift is in for catalog runs
    /// only; null = <see cref="Admit"/> in every run. A legacy run (CatalogVersion 0) always reads <see cref="Admit"/>.
    /// </summary>
    internal AdmitScope? CatalogAdmit;
    /// <summary>Why a catalog run admits more (<see cref="CatalogAdmit"/>); null when it does not.</summary>
    internal string CatalogReason;
    /// <summary>What still lifts the scopes a catalog run rejects (<see cref="CatalogAdmit"/>); null = <see cref="Lift"/>.</summary>
    internal string CatalogLift;
    /// <summary>The card ids the rule covers. Null for a family default (a detected family with no id rule).</summary>
    internal string[] Ids;
    /// <summary>
    /// A loop guard the admitted script must carry (phase 6, design section 6: "RepeatUntil max=60" for LOOTA_822, Reno_HP3 and
    /// DALA_718's recasts; DALA_705, which the design listed, has no loop: the board bounds its copies, see its row), stated on
    /// the row so the ledger and the family's regression test name the same bound; null for none.
    /// </summary>
    internal string Guard { get; set; }
    /// <summary>
    /// Phase 7: generation never yields the card for either side, even where the row admits the run player (a run board
    /// shrine, a shrine upgrade or a teammate is a run reward, never a random card): RunMatchPlan.ExcludedGenerated holds it.
    /// </summary>
    internal bool NeverGenerated { get; set; }
    /// <summary>
    /// Phase 8: what the boss's own row must turn on before the card does anything for a boss
    /// (<see cref="DungeonRunAdmission.BossNeedsGeneratedSecrets"/>: the encounter row's "bossGeneratedSecrets"); null for none.
    /// The validator refuses the card on a boss row without it, and checks the row's yield pool with it.
    /// </summary>
    internal string BossNeeds { get; set; }
    /// <summary>
    /// Phase 8, with <see cref="BossNeeds"/> = generated Secrets: the class whose Secrets the card names ("a random Hunter
    /// Secret": Professor Putricide, Streetsmuggler), whatever the boss's portrait is; null = the boss portrait's class (a boss
    /// power that says "a random class Secret"). The validator closes the yield pool of that class (BossSecretPool).
    /// </summary>
    internal CardClass? BossSecretClass { get; set; }
}

/// <summary>What <see cref="DungeonRunAdmission.Classify"/> decided for one card in one scope.</summary>
internal sealed class AdmissionVerdict
{
    /// <summary>The card needs an admission rule (a Secret, a deck or Secret phrase, tag 997, or listed in the table).</summary>
    internal bool Subject;
    internal string Family, Reason, Lift, Requires;
    internal AdmitScope Asked, Admit;
    internal AdmissionRule Rule;
    /// <summary>The verdict is a catalog run's and its rule's catalog lift applied (<see cref="AdmissionRule.CatalogAdmit"/>).</summary>
    internal bool CatalogLifted;
    /// <summary>Not a subject, or admitted in every scope asked. A rule with pending Requires admits no scope (BuildIndex).</summary>
    internal bool Admitted => !Subject || Asked != AdmitScope.None && (Admit & Asked) == Asked;
    /// <summary>Not a subject, or admitted in at least one scope asked.</summary>
    internal bool AdmittedSome => !Subject || (Admit & Asked) != AdmitScope.None;
}

/// <summary>
/// What the run lets into a match, and the public facts the boss AI may use about it (docs/design phase 2).
/// One table, read by the validator (DungeonRunValidator), the engine's generation block (RunMatchPlan.ExcludedGenerated)
/// and the restoration ledger's ai_support column (tools/restoration-ledger), so the three never disagree.
/// </summary>
internal static partial class DungeonRunAdmission
{
    /// <summary>AdmissionRule.BossNeeds: the boss side's generated Secrets (RunMatchPlan.BossGeneratedSecrets).</summary>
    internal const string BossNeedsGeneratedSecrets = "generatedSecrets";
    /// <summary>Phase 8 lifts that name no pending work.</summary>
    internal const string NoneBossShrine = "none: a board shrine; no deck or generation pool offers it";
    internal const string NoneRule = "none: a global rule card; never a deck card, a boss card or a generation result";
    internal const string NoneFightHero = "none: a fight-only hero card the encounter gives the run player; no deck, boss or generation pool offers it";
    // ------------------------------------------------------------------ lifts (what would admit a rejected scope)
    // Every pending lift names the numbered goal phase that owns the work (1 ledger, 2 Secret-aware AI, 3 shared draft
    // catalog, 4 acts, 5 deck mutation/Bob, 6 heroes/powers/treasures, 7 shrines/teammates, 8 encounters/anomalies,
    // 9 economy): the phase whose content first needs the card, and whose scope the work belongs to.
    /// <summary>
    /// Generated Secrets: the boss's Secret worlds must cover a Secret that no run deck source lists. In for catalog runs
    /// (phase 3): the run player's generation may yield Secrets (GameConfig.Player1ExcludeGeneratedSecrets off) and the
    /// boss's prior adds every Secret the player's generation can yield (<see cref="CatalogSecretPrior"/>). Legacy runs keep
    /// it off.
    /// </summary>
    internal const string GeneratedSecretsLift = "phase 3 (shared draft catalog): generated Secrets join the boss's Secret worlds before a generator enters the pool";
    /// <summary>A boss's own generated Secrets: the boss side keeps them off (GameConfig.Player2ExcludeGeneratedSecrets).</summary>
    internal const string BossGeneratedSecretsLift = "phase 8 (encounters/anomalies): the boss and finale encounters (run:special-finales) turn the boss side's generated Secrets on";
    /// <summary>Deck-wide rewrites and deck removals on the boss's side: OwnDeckLedger must carry them.</summary>
    /// <remarks>
    /// Relabelled from phase 5 when phase 5 shipped: phase 5's deck changes are the run player's (DungeonRunDeck), while this
    /// lift is the boss AI's knowledge of its own deck, and no boss deck through phase 5 holds Gral or a deck-wide-change card.
    /// The first boss decks that use them are phase 8's encounter families, which therefore own the ledger work.
    /// </remarks>
    internal const string BossDeckLedgerLift = "phase 8 (encounters/anomalies): the first boss decks that use deck-wide rewrites (encounter families) bring the boss's own-deck ledger for deck-wide rewrites and removals";
    internal const string ShrineLift = "phase 7 (shrines/teammates): board shrines and their upgrades";
    /// <summary>A board shrine or shrine upgrade on the boss side: boss shrine rosters are encounter content.</summary>
    internal const string BossShrineLift = "phase 8 (encounters/anomalies): boss shrine rosters (BossShrineLift); the run player's only until then";
    internal const string ShrineScriptLift = "phase 7 (shrines/teammates): the S1/S2/S3 shrine families script the shrine and its dormant form";
    internal const string ShrineFormLift = "none: engine-made dormant forms and the boss-side test fixture; no player role, boss deck or generation pool offers them";
    internal const string RunTeammateLift = "none: a run teammate; no boss deck or generation pool offers it";
    internal const string TeammateDraftLift = "phase 7 (shrines/teammates): the T1/T2 teammate families script the teammate";
    // Phase 6 decided these stay rejected (design section 8): restoring a turn needs a rewind or full re-sync of the client's
    // history stream, and a restart after the hero dies must run before the defeat screen; escaping a fight needs an "escape"
    // match outcome in the native end-game flow and an acts-v1 rule for a skipped fight node. Neither exists in phase 6.
    internal const string TurnRestartLift = "phase 9 (economy): a run-owned turn or game restart that rebuilds the client history and the bot state (deferred there by phase 6)";
    internal const string MatchExitLift = "phase 9 (economy): a run-owned match exit (an escape outcome and a skipped fight node) that edits the run deck (deferred there by phase 6)";
    internal const string OwnTurnSecretLift = "phase 6 (heroes/powers/treasures): ReplyModel keeps the replier's own Secret live on its owner's turn";
    internal const string TreasureScriptLift = "phase 6 (heroes/powers/treasures): script the treasure's start-of-game or started-in-deck rule";
    internal const string TeammateScriptLift = "phase 7 (shrines/teammates): script the teammate's start-of-game or started-in-deck rule";
    // Zephrys (ULD_003) is unscripted in the engine as well; the wish is shared with the Tombs treasure ULDA_013, so the
    // phase that scripts the Tombs treasures owns it (phase 3 cannot lift it: it is engine scripting, not catalog work).
    internal const string WishLift = "phase 6 (heroes/powers/treasures): script the wish from public information and the owner's own cards (with ULDA_013)";
    /// <summary>ULD_003's remaining scopes once phase 6 F15 scripted the wish: no boss deck lists Zephrys, and the boss's generation never yields it.</summary>
    internal const string WishBossLift = "none: the run player's wish; no boss deck lists Zephrys and the boss's generation never yields it";
    /// <summary>ULDA_045t's remaining scope (Generated) once phase 6 F15 kept it live on its owner's turn.</summary>
    internal const string OwnTurnSecretDoneLift = "none: Primordial Bulwark's Secret; only ULDA_045 puts it into play, no generation pool offers it";
    internal const string Outside = "none: no ledger row needs it (outside the five adventures)";
    // Lifts for a scope no run content reaches: nothing is waiting on them.
    internal const string NoneBossOnly = "none: boss decks only; no player role offers it";
    internal const string NoneBossPower = "none: a boss hero power";
    internal const string AddRule = "add a rule to DungeonRunAdmission";
    /// <summary>A run treasure's boss and generation scopes: no boss deck and no generation pool holds a run treasure.</summary>
    internal const string RunTreasureLift = "none: a run treasure; no boss deck or generation pool offers it";
    /// <summary>ULDA_115 "Gnomebliterator" (phase 6 F12 left it rejected): its Plague Lord damage is phase 8 finale state.</summary>
    internal const string PlagueLordLift = "phase 8 (encounters/anomalies): the Plague Lord finale encounters and the run state that carries damage to a Plague Lord into its fight";

    /// <summary>True when a lift names no pending work (the rejected scope is simply not used).</summary>
    internal static bool IsNoneLift(string lift) => lift != null && lift.StartsWith("none:", StringComparison.Ordinal);

    /// <summary>
    /// The whole table: the rows below (<see cref="CoreRules"/>), then each encounter family's rows (phase 8:
    /// DungeonRunAdmission.F&lt;NN&gt;.cs, one file per family, each a method so no static initialization order is involved).
    /// </summary>
    internal static readonly AdmissionRule[] Rules = BuildRules();

    private static AdmissionRule[] BuildRules() => CoreRules()
        .Concat(RulesF01()).Concat(RulesF02()).Concat(RulesF03()).Concat(RulesF04()).Concat(RulesF05()).Concat(RulesF06()).Concat(RulesF07())
        .Concat(RulesF08()).Concat(RulesF09()).Concat(RulesF10()).Concat(RulesF11()).Concat(RulesF12()).Concat(RulesF13()).Concat(RulesF14())
        .Concat(RulesF15()).Concat(RulesF16()).Concat(RulesF17()).Concat(RulesF18()).Concat(RulesF19()).Concat(RulesF20()).Concat(RulesF21())
        .Concat(RulesF22()).Concat(RulesF23()).Concat(RulesF24()).Concat(RulesF25()).ToArray();

    private static AdmissionRule[] CoreRules() => new AdmissionRule[]
    {
        // ---------------------------------------------------------------- Secrets (84 ledger rows)
        new AdmissionRule
        {
            Family = "secret-card", Admit = AdmitScope.Both, Lift = GeneratedSecretsLift,
            Reason = "a Secret; the boss's fair search samples the run player's hidden Secrets from the closed run pool (FairBotState Secret worlds) and knows its own",
            Ids = new[] { "AT_002", "AT_060", "AT_073", "BOT_908", "CFM_026", "CFM_620", "CFM_800", "DAL_570", "EX1_130", "EX1_132", "EX1_136",
                "EX1_287", "EX1_289", "EX1_294", "EX1_295", "EX1_379", "EX1_533", "EX1_554", "EX1_594", "EX1_609", "EX1_610", "EX1_611",
                "FP1_018", "FP1_020", "GIL_577", "GIL_903", "ICC_082", "ICC_200", "KAR_004", "LOE_021", "LOE_027", "LOOT_079", "LOOT_101",
                "LOOT_204", "LOOT_210", "LOOT_214", "TRL_400", "ULD_152", "UNG_024", "tt_010", "ULD_239" },
        },
        new AdmissionRule
        {
            Family = "secret-card", Admit = AdmitScope.Both, Lift = OwnTurnSecretDoneLift,
            Reason = "a Secret that can trigger on its owner's turn (Primordial Bulwark, which ULDA_045 puts into play): its owner's turn "
                + "start does not exhaust it (OwnTurnSecretRules, read from the card), in the game, every clone, and the boss's sampled "
                + "Secret worlds and replies (FairBotState.WithOpponentSecrets), and the boss's Secret prior holds it through ULDA_045's "
                + "token (c-deferrals T9, T10)",
            Ids = new[] { "ULDA_045t" },
        },
        new AdmissionRule
        {
            Family = "secret-from-deck", Admit = AdmitScope.Any, Lift = Outside,
            Reason = "draws, pulls or conditions on Secrets from the owner's own cards; generates none, so the closed Secret pool stays complete",
            Ids = new[] { "BOT_573", "CFM_066", "CFM_760", "DAL_086", "DAL_571", "EX1_080", "EX1_274", "EX1_536", "EX1_544", "EX1_612", "FP1_004",
                "GIL_634", "GVG_089", "KAR_006", "KAR_092", "LOOT_080", "TRL_530", "UNG_020",
                "AT_079", "DAL_573", "ULDA_016", "ULDA_021", "ULD_154", "ULD_240", "ULD_293", "ULD_726" },
        },
        // The four Secret boss powers (GILA_BOSS_25p, LOOTA_BOSS_48p, ULDA_BOSS_44p: secret-from-deck; LOOTA_BOSS_35p:
        // secret-named) are scripted in phase 8 family 01: their rows are in DungeonRunAdmission.F01.cs.
        new AdmissionRule
        {
            Family = "secret-generated", Admit = AdmitScope.None, Lift = GeneratedSecretsLift,
            Reason = "generates a Secret; legacy run matches block generated Secrets (GameConfig.ExcludeGeneratedSecrets), so it would do nothing",
            CatalogAdmit = AdmitScope.Player | AdmitScope.Generated,
            CatalogReason = "a catalog run lets the run player generate Secrets (GameConfig.Player1ExcludeGeneratedSecrets off) and the boss's "
                + "Secret prior holds every Secret that generation can yield; the boss's own generation of it stays blocked",
            CatalogLift = BossGeneratedSecretsLift,
            // ICC_204 Professor Putricide and TRLA_168 Streetsmuggler (the two that name "a random Hunter Secret") have their
            // own row since phase 8 family 01, which admits them for a boss too (DungeonRunAdmission.F01.cs).
            Ids = new[] { "BOT_402", "DAL_141", "GILA_509", "GILA_903", "GIL_116", "LOOTA_833", "ULD_429", "UNG_011" },
        },
        new AdmissionRule
        {
            Family = "secret-generated", Admit = AdmitScope.None, Lift = BossGeneratedSecretsLift,
            Reason = "a boss hero power that generates a Secret; a legacy run keeps the boss side's generated Secrets off "
                + "(GameConfig.Player2ExcludeGeneratedSecrets), so it would do nothing",
            // Phase 8: admitted for a boss on a catalog run whose encounter row turns the boss side's generated Secrets on.
            CatalogAdmit = AdmitScope.Boss,
            CatalogReason = "a boss hero power that puts a random Secret of the boss's class into play (Whirt's Prediction): its encounter row "
                + "turns the boss side's generated Secrets on (RunMatchPlan.BossGeneratedSecrets), and the validator closes the yield pool: "
                + "every scripted Secret of the boss's class that a catalog run's generation lists leave (DungeonRunValidator.BossSecretPool) "
                + "is admitted for a boss. The boss knows its own Secrets; the roll is reported as a random event, so the boss's search "
                + "re-plans after it; the run player sees a face-down Secret as from any Secret card (c-enc-secrets, enc01)",
            CatalogLift = NoneBossPower,
            BossNeeds = BossNeedsGeneratedSecrets,
            Ids = new[] { "DALA_BOSS_21p" },
        },
        new AdmissionRule
        {
            Family = "text-only", Admit = AdmitScope.Any, Lift = Outside,
            Reason = "the matched word is not a mechanic (\"Secretly\" chooses a Deathrattle)",
            Ids = new[] { "ICC_047" },
        },

        // ---------------------------------------------------------------- deck-dependent (54 ledger rows)
        new AdmissionRule
        {
            Family = "highlander", Admit = AdmitScope.Any, Lift = Outside,
            Reason = "a no-duplicates condition on the owner's own deck; the bot knows its own deck list and the engine evaluates the condition",
            Ids = new[] { "LOE_011", "CFM_621", "CFM_020", "CFM_687", "CFM_750" },
        },
        new AdmissionRule
        {
            Family = "highlander", Admit = AdmitScope.Player, Lift = WishBossLift,
            Reason = "a no-duplicates condition on the owner's own deck with a wish: three Discover options from a curated list of scripted "
                + "cards chosen from public information and the owner's own cards only, no hidden zone and no RNG (DungeonRunContent.WishOffer, "
                + "shared with ULDA_013) (c-deferrals T3, T4)",
            Ids = new[] { "ULD_003" },
        },
        new AdmissionRule
        {
            Family = "not-started-in-deck", Admit = AdmitScope.Any, Lift = Outside,
            Reason = "reads which cards started in the owner's deck from the history (SelfCondition.IsNotStartInDeck), right for any run deck size",
            Ids = new[] { "LOOT_537", "UNG_028", "TRL_259" },
        },
        new AdmissionRule
        {
            Family = "not-started-in-deck", Admit = AdmitScope.Player, Lift = RunTreasureLift,
            Reason = "treasure: only the player can hold it; a public hand aura on its player enchantment: cards that did not start in "
                + "the owner's deck (SelfCondition.IsNotStartInDeck, the history's frozen starting decks) cost (1) less (c-deferrals T11)",
            Ids = new[] { "GILA_511" },
        },
        new AdmissionRule
        {
            NeverGenerated = true,
            Family = "not-started-in-deck", Admit = AdmitScope.Player, Lift = RunTeammateLift,
            Reason = "a Rumble Run teammate (TRLA_191 Sharktoothed Harpooner, phase 7 T2): its Battlecry cuts (4) from each card in the "
                + "run player's hand that did not start in their deck, read from the history's frozen starting decks "
                + "(SelfCondition.IsNotStartInDeck, right for any run deck size, never an entity-id bound); only the run player holds "
                + "it, and the boss sees the public hand-cost change (c-teammates-T2 O9-O12)",
            Ids = new[] { "TRLA_191" },
        },
        new AdmissionRule
        {
            Family = "from-own-deck", Admit = AdmitScope.Any, Lift = Outside,
            Reason = "copies, discovers or reads minions or spells of the owner's own deck; the bot's own deck is a known multiset with a sampled order",
            Ids = new[] { "KAR_114", "ICC_235", "ICC_415", "DAL_059", "DAL_070", "BOT_567", "DAL_052", "LOOT_520", "UNG_029", "ICC_912",
                "CFM_672", "TRL_339", "GILA_411", "ULD_236", "ULDA_503" },
        },
        new AdmissionRule
        {
            Family = "from-own-deck", Admit = AdmitScope.Player, Lift = BossDeckLedgerLift,
            Reason = "eats a minion from the deck; the boss's own-deck ledger (OwnDeckLedger) does not remove the eaten card yet",
            Ids = new[] { "TRL_409" },
        },
        new AdmissionRule
        {
            Family = "deck-wide-change", Admit = AdmitScope.Player, Lift = BossDeckLedgerLift,
            Reason = "rewrites every card in the owner's deck; the boss's own-deck ledger (OwnDeckLedger.Sample) does not carry the rewrite yet",
            Ids = new[] { "BOT_257", "GIL_694", "GIL_840", "TRL_500", "UNG_840", "DALA_727", "ULDA_024", "ULDA_044" },
        },
        new AdmissionRule
        {
            NeverGenerated = true,
            Family = "deck-wide-change", Admit = AdmitScope.Player, Lift = BossDeckLedgerLift,
            Reason = "a shrine upgrade (phase 7 D): a run reward of a board-shrine run (the shrine-upgrade draft), applied at match start as "
                + "its client player enchantment (TRLA_809e), never a hero power, a passive treasure or a generation result; every run "
                + "shrine of the run player carries its grant TRLA_809e2, whose Deathrattle, as the shrine goes dormant, gives each "
                + "minion in the run player's in-game deck +1/+1 (TRLA_809e3; the run deck records are unchanged; the Deathrattle "
                + "doublers repeat it). Only the run player holds it; the boss's search reads the buffs as engine state of the run "
                + "player's cards when they are drawn, and the boss side waits for its own-deck ledger (OwnDeckLedger.Sample does not "
                + "carry a deck-wide rewrite yet) (c-companions-D U1-U9)",
            Ids = new[] { "TRLA_809" },
        },
        new AdmissionRule
        {
            NeverGenerated = true,
            Family = "start-of-game-draw", Admit = AdmitScope.Player, Lift = RunTeammateLift,
            Reason = "a Rumble Run start-of-game companion (phase 7 D), the sixth teammate of its class: a run deck card the teammate "
                + "draft adds on a board-shrine run, only the run player holds it (never a boss card or a generation result); drawn at "
                + "the start of the game (StartOfGameRules, a self-drawing source never dealt in an opening hand), after both "
                + "mulligans, after the board shrine and the Start of Game treasures and before the first decision, so the draw is "
                + "revealed to both players; then a public card: 2 full Mana Crystals on death (TRLA_121), Rush for the owner's "
                + "Beasts (TRLA_164), the basic Hero Power upgraded (TRLA_135), a Truesilver Champion equipped (TRLA_139), +1/+1 to a "
                + "random friendly minion on death (TRLA_148), Immune (TRLA_188), the Overloaded Mana Crystals unlocked (TRLA_156), "
                + "the owner's hero Immune on the owner's turn (TRLA_180), Taunt for its neighbours (TRLA_172); the boss sees each "
                + "result as a public board, hero, weapon, Mana or hero-power change (c-companions-D)",
            Ids = new[] { "TRLA_121", "TRLA_135", "TRLA_139", "TRLA_148", "TRLA_156", "TRLA_164", "TRLA_172", "TRLA_180", "TRLA_188" },
        },

        // ---------------------------------------------------------------- Rumble Run shrines and teammates (phase 7)
        // A board shrine is the run player's board entity (ShrineRules places it at Start of Game; dormancy and revival are
        // engine state the boss's search reproduces through the endpoint projection, FairBotState.NeedsEndpointProjection), a
        // shrine upgrade a run reward, a teammate a run deck card. None is a boss card or a generation result. Each family
        // (S1/S2/S3 shrines, U upgrades, T1/T2 teammates, D companions) moves its ids to the admitted rows as it scripts them; the
        // D ids sit in their admission families' rows above (TRLA_809 deck-wide-change, the companions start-of-game-draw).
        new AdmissionRule
        {
            NeverGenerated = true,
            Family = "board-shrine", Admit = AdmitScope.Player, Lift = BossShrineLift,
            Reason = "the run player's board shrine: placed at Start of Game by ShrineRules, sent dormant (the slot it left) when it dies "
                + "or would be transformed, bounced or shuffled, revived at full Health after three owner's turns; the boss sees a public "
                + "minion and its search reproduces dormancy and revival through the engine projection (c-shrine-core S1-S12); its "
                + "effects are public engine resolution: Armor-equal Attack (TRLA_104), Dragon cost cuts (TRLA_170), aura-granted Overkill "
                + "(TRLA_162, TRLA_171: OverkillRules), a random keyword on summoned Beasts (TRLA_163), and TRLA_110's Hunter Secrets put "
                + "into play by the run player (a board-shrine run is a catalog run, whose player may generate Secrets; the boss's "
                + "Secret prior holds every Hunter Secret that generation can yield, GeneratedSecretPool) (c-shrines-S1); Spell Damage "
                + "from the spells in hand (TRLA_108), a free Hero Power refreshed by a kill (TRLA_128), a Frostfire per freeze the run "
                + "player causes (TRLA_129), Divine Shield after surviving damage (TRLA_105), 5 damage to the boss's hero after the run "
                + "player's hero takes damage (TRLA_137, recursion-guarded), a copy of a spell cast on a friendly minion cast on the "
                + "shrine (TRLA_138: RecastRules), Deathrattles two extra times (TRLA_114), a random Priest spell after each spell "
                + "(TRLA_146: the run's generation filter), any healing of the boss's characters damages them, the boss's own heals, Lifesteal and restore passives included (TRLA_147) (c-shrines-S2, c-p7fix); "
                + "Stealth on the run player's other minions and a +2 Attack Deathrattle when it goes dormant (TRLA_106), each spell "
                + "cast again on the same target (TRLA_187: RecastRules, never recursive), the Overload amount as Attack for the run "
                + "player's minions (TRLA_109), Battlecries two extra times (TRLA_154: EXTRA_BATTLECRIES_ADDITIONAL, BattlecryRules), "
                + "spells (2) cheaper (TRLA_155), spells paid with Health (TRLA_113: SPELLS_COST_HEALTH), a random Demon per discard "
                + "through the run's generation filter (TRLA_178), damage to the run player's hero on their own turn dealt to the boss's "
                + "hero instead (TRLA_179); the boss sees each result as a public board, hand-count or Health change (c-shrines-S3)",
            // Phase 8 moves a shrine to a boss-shrine row (DungeonRunAdmission.F<NN>.cs) as its boss-side test lands: TRLA_104,
            // TRLA_170 and TRLA_171 (the Warrior shrines, family 01); the others follow in family 13.
            Ids = new[] { "TRLA_105", "TRLA_106", "TRLA_108", "TRLA_109", "TRLA_110", "TRLA_112", "TRLA_113", "TRLA_114", "TRLA_115",
                "TRLA_116", "TRLA_128", "TRLA_129", "TRLA_137", "TRLA_138", "TRLA_146", "TRLA_147", "TRLA_154", "TRLA_155", "TRLA_162", "TRLA_163",
                "TRLA_178", "TRLA_179", "TRLA_187" },
        },
        new AdmissionRule
        {
            NeverGenerated = true,
            Family = "hidden-steal", Admit = AdmitScope.Player, Lift = BossShrineLift,
            Reason = "takes a hidden card from the opponent's deck: TRLA_186 Treasure from Below, the run player's board shrine, steals a "
                + "random card of the boss's deck at the start of the run player's turn (it costs (2) less). Only the run player holds "
                + "it (never a boss card or a generation result). The boss learns only what is public: a card left its deck (its "
                + "own-deck ledger, OwnDeckLedger, drops an unknown slot by the deck count, never the stolen definition) and the run "
                + "player's hand grew by a card its fair view masks like every other card there; the search never sees the stolen "
                + "identity (c-shrines-S3 G9-G15)",
            Ids = new[] { "TRLA_186" },
        },
        new AdmissionRule
        {
            NeverGenerated = true,
            Family = "hidden-steal", Admit = AdmitScope.Player, Lift = RunTeammateLift,
            Reason = "a Rumble Run teammate (phase 7 T2) that takes hidden cards from the opponent, a run deck card only the run player "
                + "holds (never a boss card or a generation result): TRLA_152 Soulsapper's Deathrattle steals 2 random spells of the "
                + "boss's deck by TRLA_186's deck-steal rule (DungeonRunShrines.TakeFromDeck: the boss's own-deck ledger, OwnDeckLedger, "
                + "drops unknown slots by the deck count, never a stolen definition, and the boss's fair view masks the stolen cards in "
                + "the run player's hand; c-teammates-T2 E10-E16), and TRLA_190 Salty Looter's Battlecry and Deathrattle each move a "
                + "random card of the boss's hand to the run player's by the hand-to-hand theft rule of Gloves of Mugging (LOOTA_834: "
                + "the hand-transfer facts, so the boss knows what left its own hand; c-teammates-T2 O3-O8)",
            Ids = new[] { "TRLA_152", "TRLA_190" },
        },
        new AdmissionRule
        {
            NeverGenerated = true,
            Family = "shrine-token", Admit = AdmitScope.Player, Lift = BossShrineLift,
            Reason = "a card a run board shrine makes for its owner during the game (TRLA_129s Frostfire: TRLA_129 Jan'alai's Progeny puts "
                + "one into the run player's hand per freeze they cause); a public 0-cost 'Deal 1 damage' spell in the run player's hand, "
                + "never a boss card or a generation result (c-shrines-S2)",
            Ids = new[] { "TRLA_129s" },
        },
        new AdmissionRule
        {
            Family = "board-shrine-form", Admit = AdmitScope.None, Lift = ShrineFormLift,
            Reason = "a shrine's dormant form (made by ShrineRules when its shrine dies, never a card) or the TRLA_107 Tribal Shrine fixture "
                + "(a scripted enemy shrine that tests place on the boss side; boss shrine rosters are phase 8): never offered, dealt or generated",
            Ids = new[] { "TRLA_104t", "TRLA_105t", "TRLA_106t", "TRLA_107", "TRLA_107t", "TRLA_108t", "TRLA_109t", "TRLA_110t", "TRLA_112t", "TRLA_113t",
                "TRLA_114t", "TRLA_115t", "TRLA_116t", "TRLA_128t", "TRLA_129t", "TRLA_137t", "TRLA_138t", "TRLA_146t", "TRLA_147t", "TRLA_154t",
                "TRLA_155t", "TRLA_162t", "TRLA_163t", "TRLA_170t", "TRLA_171t", "TRLA_178t", "TRLA_179t", "TRLA_186t", "TRLA_187t" },
        },
        new AdmissionRule
        {
            NeverGenerated = true,
            Family = "shrine-upgrade", Admit = AdmitScope.Player, Lift = BossShrineLift,
            Reason = "a shrine upgrade: a run reward of a board-shrine run (the shrine-upgrade draft), applied at match start as its client "
                + "player enchantment, never a hero power or a passive treasure: TRLA_804 a second shrine, TRLA_805 revival one turn sooner; "
                + "TRLA_801/802/803/806/807/808 put their grant on every run shrine of the owner as it lands or revives (DungeonRunShrines.Grant): "
                + "spells cost (1) less per shrine, Taunt on the shrines' neighbours, a full heal at each turn start, and the granted "
                + "Deathrattles (destroy the enemy Shrines, draw until the hand is full, hand cards cost (2) less). All public board, hand-cost "
                + "and Deathrattle facts the engine resolves, so the boss's legal moves and its endpoint projection (NeedsEndpointProjection "
                + "on a shrine board) see them; the boss never holds one (c-shrine-core U1-U4, c-shrine-upgrades)",
            Ids = new[] { "TRLA_801", "TRLA_802", "TRLA_803", "TRLA_804", "TRLA_805", "TRLA_806", "TRLA_807", "TRLA_808" },
        },
        new AdmissionRule
        {
            NeverGenerated = true,
            Family = "teammate", Admit = AdmitScope.Player, Lift = RunTeammateLift,
            Reason = "a Rumble Run teammate: a run deck card the teammate draft adds on a board-shrine run; public effects the engine "
                + "resolves (a hero-attack trigger, Overkill), so the boss's legal moves and search see the result (c-shrine-core T1-T4); "
                + "phase 7 T1: an Overkill summon of the owner's highest-Attack deck minion (TRLA_122) or a spell draw (TRLA_134), a "
                + "discounted draw per Armor gain (TRLA_123), draws from the owner's own deck until the hand is full (TRLA_165 Beasts, "
                + "TRLA_140 spells), a random Hunter spell per minion death through the run's generation filter (TRLA_166), a "
                + "Poisonous Deathrattle that hits every enemy (TRLA_167), the Attack of the other minions doubled (TRLA_169), a hand "
                + "of Arcane Missiles (TRLA_130), Spell Damage per freeze (TRLA_131), a hero power that also Freezes or destroys an "
                + "already Frozen minion (TRLA_132), a Fireball at a random enemy after each spell (TRLA_133), a random enemy minion "
                + "destroyed per heal (TRLA_141), hits capped at 1 (TRLA_142), hits redirected to the owner's hero (TRLA_143), Divine "
                + "Shield again on Overkill (TRLA_144); the boss sees each result as a public board, hand-count or Health change, never "
                + "the cards drawn (c-teammates-T1); phase 7 T2: random Deathrattle minions (TRLA_149) or Pirates until the board is "
                + "full (TRLA_193) summoned on death, random healing spells (TRLA_150 Overkill), Shaman spells (TRLA_161 Overkill), "
                + "Warlock spells per hit on the owner's hero (TRLA_185) and a hand of random spells (TRLA_184), every one through the "
                + "run's generation filter; every minion restored to full Health (TRLA_151), spells (2) cheaper (TRLA_153), a copy of "
                + "each card played this turn (TRLA_189 Combo), a spell drawn from the owner's deck and discounted after each spell "
                + "(TRLA_192), 3 damage to random enemy minions repeated on each kill, at most 8 hits (TRLA_157), 2 damage to every "
                + "enemy minion after each Overload (TRLA_158), a spell Discover kept whole while Overloaded (TRLA_159), 3 of the "
                + "boss's Mana Crystals Overloaded (TRLA_160: public Overload owed), the lowest-Cost card discarded and a card drawn "
                + "(TRLA_181 Overkill), a Lifesteal Battlecry on every enemy minion (TRLA_182), a Hero Power usable any number of "
                + "times (TRLA_183), a Dragon Discover kept whole beside a Dragon (TRLA_173 Overkill), the owner's minions held at 1 "
                + "Health on the owner's turn (TRLA_174), Armor gains doubled (TRLA_175), Rush and a Dragon draw per Dragon played "
                + "(TRLA_176), every enemy minion destroyed for 2 Armor each (TRLA_177); the boss sees each result as a public board, "
                + "hand-count, Mana, Armor or Health change, never the cards drawn or generated (c-teammates-T2)",
            Ids = new[] { "TRLA_122", "TRLA_123", "TRLA_124", "TRLA_125", "TRLA_127", "TRLA_130", "TRLA_131", "TRLA_132", "TRLA_133",
                "TRLA_134", "TRLA_140", "TRLA_141", "TRLA_142", "TRLA_143", "TRLA_144", "TRLA_165", "TRLA_166", "TRLA_167", "TRLA_169",
                "TRLA_149", "TRLA_150", "TRLA_151", "TRLA_153", "TRLA_157", "TRLA_158", "TRLA_159", "TRLA_160", "TRLA_161",
                "TRLA_173", "TRLA_174", "TRLA_175", "TRLA_176", "TRLA_177", "TRLA_181", "TRLA_182", "TRLA_183", "TRLA_184",
                "TRLA_185", "TRLA_189", "TRLA_192", "TRLA_193" },
        },
        new AdmissionRule
        {
            Family = "start-of-game-draw", Admit = AdmitScope.Player, Lift = RunTreasureLift,
            Reason = "treasure: only the player can hold it; drawn at the start of the game (StartOfGameRules, a self-drawing source never "
                + "dealt in an opening hand), after both mulligans and before the first decision, so the draw is revealed to both players; "
                + "then a public card: a Taunt that grows in hand, draws of given Costs from the owner's deck, Mana Crystals that grow as "
                + "friendly minions die, the wish (ULD_003's), a hand filled with Lamps (c-deferrals T2, T5-T8)",
            Ids = new[] { "DALA_707", "ULDA_407", "ULDA_013", "ULDA_040", "ULDA_302" },
        },
        new AdmissionRule
        {
            Family = "start-of-game-draw", Admit = AdmitScope.None, Lift = MatchExitLift,
            Reason = "ends the fight and edits the run deck",
            Ids = new[] { "ULDA_111" },
        },
        new AdmissionRule
        {
            Family = "empty-deck", Admit = AdmitScope.Any, Lift = Outside,
            Reason = "a condition on the owner's own deck being empty or minion-free; the engine evaluates it and the bot knows its own deck",
            Ids = new[] { "BOT_424", "LOOT_085" },
        },
        new AdmissionRule
        {
            Family = "turn-rewind", Admit = AdmitScope.None, Lift = TurnRestartLift,
            Reason = "restarts the turn or the game, which rebuilds the controllers; bot state and plan cache must restart",
            Ids = new[] { "GILA_900p", "GILA_911" },
        },

        // ---------------------------------------------------------------- fixed boss decks (current run data)
        new AdmissionRule
        {
            Family = "start-of-game", Admit = AdmitScope.Boss | AdmitScope.Generated, Lift = NoneBossOnly,
            Reason = "a start-of-game effect resolved by the engine before the first decision, in a fixed boss deck",
            Ids = new[] { "EDR_000", "JAIL_384", "JAIL_860" },
        },
        new AdmissionRule
        {
            Family = "deck-condition", Admit = AdmitScope.Boss | AdmitScope.Generated, Lift = NoneBossOnly,
            Reason = "a condition on the owner's own deck list; a fixed boss deck satisfies it statically",
            Ids = new[] { "JAIL_035", "JAIL_328", "TLC_100" },
        },
        new AdmissionRule
        {
            Family = "not-started-in-deck", Admit = AdmitScope.Boss | AdmitScope.Generated, Lift = NoneBossOnly,
            Reason = "draws a card that did not start in the deck; read from the history",
            Ids = new[] { "JAIL_380" },
        },
        new AdmissionRule
        {
            Family = "deck-wide-change", Admit = AdmitScope.Boss, Lift = NoneBossOnly,
            Reason = "buffs the minions in a fixed boss deck; the engine applies it and the buff only raises stats, so the search "
                + "stays legal while OwnDeckLedger's stat beliefs lag (the ledger carries deck-wide changes in phase 8)",
            Ids = new[] { "TIME_609t2" },
        },
        new AdmissionRule
        {
            Family = "deckbuilding", Admit = AdmitScope.Boss, Lift = NoneBossOnly,
            Reason = "a deckbuilding pick; boss deck lists are fixed and checked by DeckbuildingRules and FabledRules",
            Ids = new[] { "JAIL_397" },
        },

        // ---------------------------------------------------------------- Dungeon Run active treasures (phase 6 F2)
        // The 30 Kobolds and Catacombs active treasures are the run player's deck cards only: no boss deck lists one and the
        // boss's generation never yields one. Each row names what the AI needs from it and its regression test (c-treasures-F2).
        new AdmissionRule
        {
            Family = "run-treasure", Admit = AdmitScope.Player, Lift = RunTreasureLift,
            Reason = "treasure: only the player can hold it; public effects the engine resolves, so the boss's legal moves and search see the result (c-treasures-F2 T4-T33)",
            // Phase 8 family 09: the fourteen of these that King Togwaggle's Magic Candle finds (and A. F. Kay's deck lists) are
            // in DungeonRunAdmission.F09.cs (run-treasure-boss-find: the same legacy verdict, a boss too on catalog runs).
            Ids = new[] { "LOOTA_811", "LOOTA_814", "LOOTA_837", "LOOTA_840", "LOOTA_841", "LOOTA_842", "LOOTA_842a" },
        },
        new AdmissionRule
        {
            Family = "run-treasure-deck-edit", Admit = AdmitScope.Player, Lift = RunTreasureLift,
            Reason = "treasure: only the player can hold it; adds the taken or copied minion to the run deck after a won fight "
                + "(RunHelpers.EditAdd, applied by DungeonRun.ApplyMatchResult through the run's deck checks)",
            Ids = new[] { "LOOTA_805", "LOOTA_813" },
        },
        new AdmissionRule
        {
            Family = "run-treasure-run-read", Admit = AdmitScope.Player, Lift = RunTreasureLift,
            Reason = "treasure: only the player can hold it; reads the bosses defeated this run from the match's RunContext, a public "
                + "run fact shared by every clone",
            Ids = new[] { "LOOTA_817", "LOOTA_829", "LOOTA_830" },
        },
        new AdmissionRule
        {
            Family = "run-treasure-belief-reset", Admit = AdmitScope.Player, Lift = RunTreasureLift,
            Reason = "treasure: only the player can hold it; rewrites both hidden decks, recorded as deck moves and a "
                + "RunHelpers.BeliefReset for each side, so the boss's own-deck ledger treats the cards it received as unknown",
            Ids = new[] { "LOOTA_820" },
        },
        new AdmissionRule
        {
            Family = "run-treasure-hand-steal", Admit = AdmitScope.Player, Lift = RunTreasureLift,
            Reason = "treasure: only the player can hold it; moves cards from the boss's hand by the hand-transfer facts, so the "
                + "boss knows what left its hand and the player's fair view sees only what it was shown",
            Ids = new[] { "LOOTA_834" },
        },
        new AdmissionRule
        {
            Family = "run-treasure-side-shuffle", Admit = AdmitScope.Player, Lift = RunTreasureLift,
            Reason = "treasure: only the player can hold it; every minion picks a side with the game RNG by the control rule "
                + "(ControlTask), a public board change the boss's next decision reads",
            // Phase 8 family 09: King Togwaggle's Magic Candle finds it too (a catalog run's boss; c-enc-09 L12).
            CatalogAdmit = AdmitScope.Player | AdmitScope.Boss, CatalogLift = RunTreasureBossLift,
            CatalogReason = "a treasure King Togwaggle's Magic Candle finds: every minion picks a side with the game RNG by the control "
                + "rule (ControlTask; a run shrine never changes sides), a public board change, the same whoever casts it (c-enc-09 L12)",
            Ids = new[] { "LOOTA_839" },
        },
        new AdmissionRule
        {
            Family = "run-treasure-loop", Admit = AdmitScope.Player, Lift = RunTreasureLift, Guard = "RepeatUntil max=60",
            Reason = "treasure: only the player can hold it; casts Pyroblast at random targets until a hero dies, guarded at 60 casts "
                + "(DungeonRunContent.RodOfRoastingMaxCasts)",
            Ids = new[] { "LOOTA_822" },
        },

        // ---------------------------------------------------------------- Monster Hunt Tess and Darius (phase 6 F3)
        // Tess Greymane's and Darius Crowley's powers, Darius's Cannons and the two heroes' active treasures are the run player's
        // only: no boss deck lists one and the boss's generation never yields one (GILA_509 is the secret-generated row above).
        // Each row names what the AI needs from it and its regression test (c-treasures-F3).
        new AdmissionRule
        {
            Family = "run-hero-power", Admit = AdmitScope.Player, Lift = RunTreasureLift,
            Reason = "a named hero's power: only the run player starts with it; Scavenge discovers from the public play history of both "
                + "players, Fire! fires the owner's Cannons, a public board effect the boss's next decision reads (c-treasures-F3 T4, T12)",
            Ids = new[] { "GILA_500p2", "GILA_600p" },
        },
        new AdmissionRule
        {
            Family = "run-treasure", Admit = AdmitScope.Player, Lift = RunTreasureLift,
            Reason = "treasure: only the player can hold it; public effects the engine resolves, so the boss's legal moves and search see the result (c-treasures-F3 T5-T22)",
            // Phase 8 family 15: GILA_500p2t A Small Rock left this row for run-treasure-boss-find in DungeonRunAdmission.F15.cs
            // (the same legacy verdict; on catalog runs Mama Diggs's Excavate finds it for a boss).
            Ids = new[] { "GILA_500t", "GILA_500t2", "GILA_500t3", "GILA_501", "GILA_503", "GILA_507", "GILA_508", "GILA_510",
                "GILA_604", "GILA_608", "GILA_610", "GILA_611", "GILA_612" },
        },
        new AdmissionRule
        {
            Family = "run-hero-board", Admit = AdmitScope.Player, Lift = RunTreasureLift,
            Reason = "Darius's Cannon: an untouchable board entity of the run player's (never a target, never in a deck); its shots are "
                + "public damage at the enemy minions opposite it (DungeonRunContent Cannons, c-treasures-F3 T12-T14)",
            Ids = new[] { "GILA_601" },
        },

        // ---------------------------------------------------------------- Monster Hunt Shaw and Toki (phase 6 F4)
        // Houndmaster Shaw's power and Bloodhound and the two heroes' active treasures are the run player's: no boss deck lists
        // one and the boss's generation never yields one. GILA_906 can hand the boss the player's cards (its hand), which the
        // boss then plays through the engine like any card in its hand (c-treasures-F4 T19). GILA_411 is the from-own-deck row
        // above; GILA_903 is the secret-generated row above. Each row names its regression test (c-treasures-F4).
        new AdmissionRule
        {
            Family = "run-hero-power", Admit = AdmitScope.Player, Lift = RunTreasureLift,
            Reason = "a named hero's power: only the run player starts with it; Dog Whistle summons a public 1/1 Rush Bloodhound (c-treasures-F4 T4)",
            Ids = new[] { "GILA_400p" },
        },
        new AdmissionRule
        {
            Family = "run-treasure", Admit = AdmitScope.Player, Lift = RunTreasureLift,
            Reason = "treasure: only the player can hold it; public effects the engine resolves, so the boss's legal moves and search see the result (c-treasures-F4 T4-T18)",
            Ids = new[] { "GILA_400t", "GILA_401", "GILA_403", "GILA_410", "GILA_412", "GILA_413", "GILA_904", "GILA_907", "GILA_910" },
        },
        new AdmissionRule
        {
            Family = "run-treasure-hand-cast", Admit = AdmitScope.Player, Lift = RunTreasureLift,
            Reason = "treasure: only the player can hold it; casts every spell of both hands with random modes and targets, each a public "
                + "cast (a cast Secret is revealed), so the boss sees what left its hand and the player's (c-treasures-F4 T13)",
            Ids = new[] { "GILA_901" },
        },
        new AdmissionRule
        {
            Family = "run-treasure-hand-swap", Admit = AdmitScope.Player, Lift = RunTreasureLift,
            Reason = "treasure: only the player can hold it; swaps the hands by the hand-transfer facts, so the boss knows the cards it "
                + "gave (PublicHandKnowledge) and holds the player's as its own hand (c-treasures-F4 T15, T19)",
            Ids = new[] { "GILA_906" },
        },

        // ---------------------------------------------------------------- Monster Hunt shared treasures (phase 6 F5)
        // The treasures every Monster Hunt hunter is offered, the in-match stages of the evolving artifacts (Coin Pouch, Curio,
        // Mob, Militia Horn, Tracker) and their tokens are the run player's: no boss deck lists one and the boss's generation
        // never yields one. The boss can still hold one through GILA_906's hand swap and plays it through the engine like any
        // card in its hand (c-treasures-F5 T30). Each row names its regression test (c-treasures-F5).
        new AdmissionRule
        {
            Family = "run-treasure", Admit = AdmitScope.Player, Lift = RunTreasureLift,
            Reason = "treasure: only the player can hold it; public effects the engine resolves, so the boss's legal moves and search see the result (c-treasures-F5 T4-T27)",
            Ids = new[] { "GILA_801", "GILA_803", "GILA_804", "GILA_810", "GILA_820", "GILA_824", "GILA_825", "GILA_826", "GILA_850b", "GILA_853b",
                "GILA_854", "GILA_854t" },
        },
        new AdmissionRule
        {
            Family = "run-treasure-evolving", Admit = AdmitScope.Player, Lift = RunTreasureLift,
            Reason = "treasure: only the player can hold it; an evolving artifact whose next stage is shuffled into the owner's deck (a "
                + "public shuffle of a fixed card) or a minion that transforms itself in play; nothing is kept by the run (c-treasures-F5 T14-T19)",
            Ids = new[] { "GILA_816a", "GILA_816b", "GILA_816c", "GILA_817", "GILA_818", "GILA_819", "GILA_817t", "GILA_818t", "GILA_819t",
                "GILA_821a", "GILA_821b", "GILA_821c", "GILA_851a", "GILA_851b", "GILA_851c", "GILA_852a", "GILA_852b", "GILA_852c" },
        },
        new AdmissionRule
        {
            Family = "from-own-deck", Admit = AdmitScope.Player, Lift = RunTreasureLift,
            Reason = "treasure: only the player can hold it (phase 6 design section 6); discovers copies of cards of the owner's own deck, "
                + "which the owner knows; the copies go face down to its hand (c-treasures-F5 T9)",
            Ids = new[] { "GILA_806" },
        },
        new AdmissionRule
        {
            Family = "run-treasure-discover", Admit = AdmitScope.Player, Lift = RunTreasureLift,
            Reason = "treasure: only the player can hold it; discovers from the run's Monster Hunt treasure pool (RunContext.TreasurePool, "
                + "a run fact shared by every clone); the boss's generation rejects every run treasure, so a boss that holds it discovers none (c-treasures-F5 T5)",
            Ids = new[] { "GILA_802" },
        },
        new AdmissionRule
        {
            Family = "run-treasure-hidden-copy", Admit = AdmitScope.Player, Lift = RunTreasureLift,
            Reason = "treasure: only the player can hold it; copies cards out of the boss's hidden hand or deck: GILA_811 and GILA_827 put "
                + "the copy face down in the player's hand (no public hand-entry identity, so the boss learns only that a card arrived, "
                + "PublicHandKnowledge), GILA_812 summons public copies; the boss's own hand and deck are unchanged (c-treasures-F5 T10, T11, T25)",
            Ids = new[] { "GILA_811", "GILA_812", "GILA_827" },
        },

        // ---------------------------------------------------------------- Dalaran Heist heroes (phase 6 F6)
        // The nine Heist villains' special powers (two per class) and the cards they make are the run player's: no boss deck
        // lists one and the boss's generation never yields one. Every power is a public, single-target or token effect the
        // engine resolves, so the boss's legal moves and search read the result. Undermine's Improvised Explosive is the one
        // card that reaches the boss: it is shuffled into the boss's deck and casts itself when drawn, so neither side ever
        // chooses to play it; the boss learns it only when it is drawn. Each row names its regression test (c-heroes-F6).
        new AdmissionRule
        {
            Family = "run-hero-power", Admit = AdmitScope.Player, Lift = RunTreasureLift,
            Reason = "a named hero's power: only the run player starts with it; public single-target or token effects (a cost cut, Freeze "
                + "and damage, a buff, a transform, a card to hand, cards shuffled into the boss's deck), each resolved by the engine "
                + "with the game RNG over the run's generation pool (c-heroes-F6 T4-T21)",
            Ids = DungeonRunContent.HeistPowers.ToArray(),
        },
        new AdmissionRule
        {
            Family = "run-hero-power-token", Admit = AdmitScope.Player, Lift = RunTreasureLift,
            Reason = "a Heist power's card for the run player: the Shifting Chameleon (Pet Training) shifts into a random 1-Cost minion "
                + "each turn in hand, the Devious Imp (The Pact) is a public 2/2 (c-heroes-F6 T7, T21)",
            Ids = new[] { DungeonRunContent.ShiftingChameleon, DungeonRunContent.DeviousImp },
        },
        new AdmissionRule
        {
            Family = "run-hero-power-deck-token", Admit = AdmitScope.Both, Lift = RunTreasureLift,
            Reason = "Undermine's Improvised Explosive: the run player's power shuffles it into the boss's deck; it casts when drawn (3 "
                + "damage to whoever draws it, no choice for either side), so the boss AI never plays it; its own-deck ledger meets it "
                + "only when it is drawn (c-heroes-F6 T15, T22)",
            Ids = new[] { DungeonRunContent.ImprovisedExplosive },
        },

        // ---------------------------------------------------------------- Tombs of Terror explorers (phase 6 F9)
        // The four explorers' powers (three each), the cards they make and ULDA_039 Uldum Treasure Cache (Jr. and Sr. Tomb
        // Diver's power) are the run player's: no boss deck lists one and the boss's generation never yields one. Each power
        // is a public effect the engine resolves, so the boss's legal moves and search read the result. Each row names its
        // regression test (c-heroes-F9).
        new AdmissionRule
        {
            Family = "run-hero-power", Admit = AdmitScope.Player, Lift = RunTreasureLift,
            Reason = "a named hero's power: only the run player starts with it; public single-target, token or self effects (damage with "
                + "a Combo bonus, a one-spell Spell Damage aura, a 2/1 token, a Discover of a Shaman or Paladin Battlecry minion with Overload, "
                + "Divine Shield and Windfury, a Choose One of +2 Attack or 2 Armor whose two choices the engine lists as options, a heal "
                + "that draws on full Health, a passive that adds a Moonfire after two spells in a turn, a random weapon at Durability 1), "
                + "each resolved by the engine with the game RNG over the run's generation pool (c-heroes-F9 T4-T14)",
            Ids = new[] { "ULDA_Reno_HP1", "ULDA_Reno_HP2", "ULDA_Finley_HP1", "ULDA_Finley_HP2", "ULDA_Finley_HP3", "ULDA_Elise_HP1",
                "ULDA_Elise_HP2", "ULDA_Elise_HP3", "ULDA_Brann_HP1", "ULDA_Brann_HP2" },
        },
        new AdmissionRule
        {
            Family = "run-hero-power-loop", Admit = AdmitScope.Player, Lift = RunTreasureLift, Guard = "RepeatUntil max=60",
            Reason = "a named hero's power: only the run player starts with it; Arcane Craftiness fires two public 1-damage missiles at "
                + "random enemies and fires again while a minion died, guarded at 60 volleys (DungeonRunContent.ArcaneCraftinessMaxVolleys; c-heroes-F9 T6)",
            Ids = new[] { "ULDA_Reno_HP3" },
        },
        new AdmissionRule
        {
            Family = "from-own-deck", Admit = AdmitScope.Player, Lift = RunTreasureLift,
            Reason = "a named hero's passive power (phase 6 design section 6): the turn's draw becomes a Discover of three distinct cards of "
                + "the owner's own deck, which the owner knows; the chosen card is drawn face down and the others stay in the deck "
                + "(DiscoverRealRules, as Commander Geddon), so the boss sees one card drawn (c-heroes-F9 T15)",
            Ids = new[] { "ULDA_Brann_HP3" },
        },
        new AdmissionRule
        {
            Family = "run-treasure-discover", Admit = AdmitScope.Player, Lift = RunTreasureLift,
            Reason = "Jr. and Sr. Tomb Diver's power: only the run player can hold it; discovers from the run's Tombs treasure pool (the "
                + "run's whole pool while it offers no Tombs treasure; RunContext.TreasurePool, a run fact shared by every clone); the pool is the "
                + "run player's, so a boss that came to hold it discovers none (c-heroes-F9 T16)",
            Ids = new[] { DungeonRunContent.UldumTreasureCache },
        },
        new AdmissionRule
        {
            Family = "run-hero-power-token", Admit = AdmitScope.Player, Lift = RunTreasureLift,
            Reason = "an explorer power's card for the run player: the Amalgam Explorer (New \"Recruits\") is a public 2/1 of every minion "
                + "type; Defensive Posture and Offensive Posture are Elise's Might's two choices (2 Armor, +2 Attack this turn) (c-heroes-F9 T7, T10)",
            Ids = new[] { DungeonRunContent.AmalgamExplorer, DungeonRunContent.DefensivePosture, DungeonRunContent.OffensivePosture },
        },

        // ---------------------------------------------------------------- Tombs of Terror signature treasures (phase 6 F11)
        // The explorers' signature treasures (one picked at the start of a run, run-v1.json signatures; never offered in a draft),
        // their Sr. upgrades and Pack Mule are the run player's: no boss deck lists one and the boss's generation never yields one.
        // ULDA_016 and ULDA_021 (Tomb Divers) are the secret-from-deck row above and ULDA_024 the deck-wide-change row; ULDA_302 is
        // family F15's start-of-game-draw row. Each row names its regression test (c-treasures-F11).
        new AdmissionRule
        {
            Family = "run-treasure", Admit = AdmitScope.Player, Lift = RunTreasureLift,
            Reason = "treasure: only the player can hold it; public effects the engine resolves (a buff with Spell Damage, targeted and "
                + "split damage, a recast of the player's own spell on the same target, copies of a spell, Discovers of the player's class "
                + "pool, resurrections, transforms, summons, weapon triggers after the hero attacks, destroys with Armor, draws that cut "
                + "Costs, a Deathrattle buff that moves between Beasts, enemy minions shuffled into the player's deck, a shuffled improved "
                + "copy of the player's own card, Cost cuts on the player's own starter cards), so the boss's legal moves and search see "
                + "the result (c-treasures-F11 T4-T30)",
            Ids = new[] { "ULDA_203", "ULDA_204", "ULDA_206", "ULDA_207", "ULDA_502", "ULDA_501", "ULDA_505", "ULDA_015", "ULDA_020",
                "ULDA_507", "ULDA_010", "ULDA_303", "ULDA_305", "ULDA_003", "ULDA_017", "ULDA_022", "ULDA_304", "ULDA_401", "ULDA_402",
                "ULDA_404", "ULDA_018", "ULDA_023", "ULDA_405", "ULDA_019" },
        },
        new AdmissionRule
        {
            Family = "run-treasure-hand-steal", Admit = AdmitScope.Player, Lift = RunTreasureLift,
            Reason = "treasure: only the player can hold it; after the player's hero attacks it moves a random card from the boss's hand "
                + "by the hand-transfer facts (Gloves of Mugging's rule), so the boss knows what left its hand and the player's fair view "
                + "sees only what it was shown (c-treasures-F11 T5)",
            Ids = new[] { "ULDA_201" },
        },
        new AdmissionRule
        {
            Family = "run-treasure-discover", Admit = AdmitScope.Player, Lift = RunTreasureLift,
            Reason = "treasure: only the player can hold it; adds random treasures of the run's Tombs treasure pool (the run's whole pool "
                + "while the Tombs pool holds fewer than three; RunContext.TreasurePool, a run fact shared by every clone); the pool is the "
                + "run player's, so a boss that came to hold it adds none (c-treasures-F11 T27)",
            Ids = new[] { "ULDA_002" },
        },

        // ---------------------------------------------------------------- Tombs of Terror active treasures (phase 6 F12)
        // The Tombs active treasures offered to an explorer's act run (run-v1.json: the explorers' sixteen starters, pool Tombs),
        // their Twinspell copies and their tokens are the run player's: no boss deck lists one and the boss's generation never
        // yields one. ULDA_044 Blade of the Burning Sun is the deck-wide-change row and ULDA_503 Advanced Targeting Monocle the
        // from-own-deck row above. ULDA_115 stays rejected (phase 8). Each row names its regression test (c-treasures-F12).
        new AdmissionRule
        {
            Family = "run-treasure", Admit = AdmitScope.Player, Lift = RunTreasureLift,
            Reason = "treasure: only the player can hold it; public effects the engine resolves (random healing spells and Legendary "
                + "minions of the player's generation pool, damage to every enemy with a Cost that falls with the public death count, "
                + "summoned tokens, copies of a minion in hand or on the board, a Deathrattle that shuffles the card back, forced "
                + "attacks of summoned Locusts, a one-turn Windfury and Immune aura, weapons that grow, keep the hero Immune or move an "
                + "attacked minion that survives into the player's hand, a cast Brawl, a Battlecry that destroys a minion, the first "
                + "friendly minions that died resurrected with Reborn, two waves of Murlocs with Rush), so the boss's legal moves and "
                + "search see the result (c-treasures-F12 T4-T9, T11-T14, T16-T18, T20, T23-T25)",
            Ids = new[] { "ULDA_001", "ULDA_004", "ULDA_006", "ULDA_008", DungeonRunContent.AncientSnake, "ULDA_012", "ULDA_034",
                "ULDA_036", "ULDA_036ts", DungeonRunContent.GiantLocust, "ULDA_041", "ULDA_041ts", "ULDA_043", "ULDA_112", "ULDA_113",
                "ULDA_114", "ULDA_202", "ULDA_307", "ULDA_307ts", "ULDA_403", "ULDA_406" },
        },
        new AdmissionRule
        {
            Family = "run-treasure-hand-steal", Admit = AdmitScope.Player, Lift = RunTreasureLift,
            Reason = "treasure: only the player can hold it; its Deathrattle moves a random card from the boss's hand by the hand-transfer "
                + "facts (Reno's Crafty Lasso's rule), so the boss knows what left its hand and the player's fair view sees only what it "
                + "was shown (c-treasures-F12 T15)",
            Ids = new[] { "ULDA_110" },
        },
        new AdmissionRule
        {
            Family = "run-treasure-hand-escrow", Admit = AdmitScope.Player, Lift = RunTreasureLift,
            Reason = "treasure: only the player can hold it; refreshes the player's Mana Crystals and holds the player's hand aside in the "
                + "engine's hand escrow (HandTimelineRules') for the rest of the turn while as many cards are drawn from the player's own "
                + "deck; at the turn's end the Mirage hand is shuffled back and the same hand entities return, so what the boss knew about "
                + "them still holds and nothing hidden is rewritten (c-treasures-F12 T10)",
            Ids = new[] { "ULDA_035", "ULDA_035ts" },
        },
        new AdmissionRule
        {
            Family = "run-treasure-run-state", Admit = AdmitScope.Player, Lift = RunTreasureLift,
            Reason = "treasure: only the player can hold it; reads or writes run state through RunHelpers: Discovers and Mana Crystals "
                + "per boss defeated (RunContext.BossesDefeated, a run fact shared by every clone), the boss's public hero power copied "
                + "for the rest of the run (RunMatchResult.PowerOverride) and a one-fight bonus for the next fight (RunMatchResult.NextFight), "
                + "both kept after a won fight only (c-treasures-F12 T21, T22, T27)",
            Ids = new[] { "ULDA_301", "ULDA_306", "ULDA_504" },
        },
        new AdmissionRule
        {
            Family = "run-treasure-burn", Admit = AdmitScope.Player, Lift = RunTreasureLift,
            Reason = "treasure: only the player can hold it; destroys half the boss's minions, discards half its hand, destroys half its "
                + "deck as revealed burns (the boss's own-deck ledger reads the SHOW before each exit), then halves its Mana Crystals and "
                + "deals half its hero's Health; every part is public, so the boss's own-deck sample and search see the result "
                + "(c-treasures-F12 T19)",
            Ids = new[] { "ULDA_118" },
        },
        new AdmissionRule
        {
            Family = "finale-state", Admit = AdmitScope.None, Lift = PlagueLordLift,
            Reason = "its second half damages the Plague Lord of the act finale whenever the run fights it; the run has no Plague Lord "
                + "finale and no state that carries damage to it until phase 8, and a Gnomebliterator that only dealt its first half "
                + "would misstate the card (c-treasures-F12 T28)",
            Requires = "the phase 8 Plague Lord finale encounters, and run state that carries damage dealt to a Plague Lord into its fight",
            Ids = new[] { DungeonRunContent.Gnomebliterator },
        },

        // ---------------------------------------------------------------- Dalaran Heist active treasures (phase 6 F7)
        // The Heist active treasures (and THE... Candles?'s in-match Twinspell stages) are the run player's: no boss deck lists one
        // and the boss's generation never yields one. DALA_727 Sow the Seeds is the deck-wide-change row above. Fly-By's Kadoom Bot
        // is the one card that reaches the boss: it is shuffled into the boss's deck and casts itself when drawn. Each row names
        // its regression test (c-treasures-F7).
        new AdmissionRule
        {
            Family = "run-treasure", Admit = AdmitScope.Player, Lift = RunTreasureLift,
            Reason = "treasure: only the player can hold it; public effects the engine resolves (extra turns in the turn queue, control, "
                + "transforms, random cards, Discovers, Twinspell and Scheme stages, a one-turn replay of the player's own plays with random "
                + "targets), so the boss's legal moves and search see the result (c-treasures-F7 T4-T31)",
            Ids = new[] { "DALA_700", "DALA_702", "DALA_703", "DALA_704", "DALA_706", "DALA_711", "DALA_712", "DALA_713", "DALA_714",
                "DALA_714a", "DALA_714b", "DALA_714c", "DALA_715", "DALA_716", "DALA_717", "DALA_720", "DALA_722", "DALA_723", "DALA_724",
                "DALA_725", "DALA_726", "DALA_729", "DALA_730" },
        },
        new AdmissionRule
        {
            Family = "run-treasure-copy", Admit = AdmitScope.Player, Lift = RunTreasureLift,
            Reason = "treasure: only the player can hold it; each Patron summons one public copy of itself at its owner's turn end, and a "
                + "copy summoned during that turn end first triggers at the next one, so the board (7) bounds it with no loop (1, 2, 4, 7 "
                + "Patrons; c-treasures-F7 T9)",
            Ids = new[] { "DALA_705" },
        },
        new AdmissionRule
        {
            Family = "run-treasure-discover", Admit = AdmitScope.Player, Lift = RunTreasureLift,
            Reason = "treasure: only the player can hold it; transforms into, or shuffles, treasures of the run's Heist treasure pool "
                + "(RunContext.TreasurePool, a run fact shared by every clone); the boss's generation rejects every run treasure, so a "
                + "boss that holds one draws none (c-treasures-F7 T5, T11)",
            Ids = new[] { "DALA_701", "DALA_708" },
        },
        new AdmissionRule
        {
            Family = "run-treasure-belief-reset", Admit = AdmitScope.Player, Lift = RunTreasureLift,
            Reason = "treasure: only the player can hold it; rewrites every other card of the owner's hidden hand and deck, recorded as a "
                + "RunHelpers.BeliefReset of both zones, so what the boss learned about those cards before is dropped (FairBotState.ForgetResetHand; c-treasures-F7 T12)",
            Ids = new[] { "DALA_709" },
        },
        new AdmissionRule
        {
            Family = "run-treasure-run-log", Admit = AdmitScope.Player, Lift = RunTreasureLift, Guard = "RepeatUntil max=60",
            Reason = "treasure: only the player can hold it; reads and appends its run deck slot's run log (RunContext.SlotLogs, kept "
                + "after a won fight only): Elistra recasts the spells cast on it as public effect casts, guarded at 60 recasts "
                + "(DungeonRunContent.ElistraMaxRecasts); the Scythe's Deathrattle summons the minions it killed, bounded by the board "
                + "(c-treasures-F7 T21, T22)",
            Ids = new[] { "DALA_718", "DALA_719" },
        },
        new AdmissionRule
        {
            Family = "run-treasure-deck-edit", Admit = AdmitScope.Player, Lift = RunTreasureLift,
            Reason = "treasure: only the player can hold it; replaces every minion of the run deck with the chosen minion's card after a "
                + "won fight (RunHelpers.EditReplaceSlot, applied all or none by DungeonRun.ApplyMatchResult through the run's deck checks); "
                + "nothing changes in the match (c-treasures-F7 T24)",
            Ids = new[] { "DALA_721" },
        },
        new AdmissionRule
        {
            Family = "run-deck-token", Admit = AdmitScope.Both, Lift = RunTreasureLift,
            Reason = "Fly-By's Kadoom Bot: the run player's treasure shuffles it into the boss's deck; it casts when drawn (50 damage to "
                + "whoever draws it, no choice for either side), so the boss AI never plays it; its own-deck ledger meets it only when it "
                + "is drawn (c-treasures-F7 T19)",
            Ids = new[] { DungeonRunContent.KadoomBot },
        },

        // ---------------------------------------------------------------- Dalaran Heist passive treasures (phase 6 F8)
        // The Heist passive treasures are the run player's: no boss deck lists one and the boss's generation never yields one.
        // Each is a player enchantment the engine resolves (an aura, a trigger, a Start of Game effect or an engine rule), so
        // the options list, the boss's fair view and its search read the result; none reads a hidden zone. The Hand of
        // Rafaam's Cursed! (LOE_007t) reaches the boss's hand as a public card the boss AI plays like any other. Each row names
        // its regression test (c-treasures-F8).
        new AdmissionRule
        {
            Family = "run-treasure", Admit = AdmitScope.Player, Lift = RunTreasureLift,
            Reason = "treasure: only the player can hold it; a public player aura or trigger the engine resolves (Armor after a friendly "
                + "death, Rush, the left-most card's discount, the Hero Power twice, a hand buff each turn, spells paid with Health through "
                + "the engine's cost-in-Health rule, two (1)-Cost copies of a played minion shuffled into the owner's deck, a public shuffle), so the "
                + "boss's legal moves and search see the result (c-treasures-F8 T4-T6, T8, T14, T16, T18)",
            Ids = new[] { "DALA_728", "DALA_731", "DALA_733", "DALA_736", "DALA_744", "DALA_746", "DALA_748" },
        },
        new AdmissionRule
        {
            Family = "run-treasure-rule", Admit = AdmitScope.Player, Lift = RunTreasureLift,
            Reason = "treasure: only the player can hold it; an engine rule keyed by its player enchantment (RunPlayerRules): half Cost "
                + "and two plays a turn are read by the Cost getter and the play check, so the options list never offers a third card; "
                + "Elixir of Vim's draws are a turn-start trigger and its Fatigue immunity is read by the draw (c-treasures-F8 T7, T17)",
            Ids = new[] { "DALA_735", "DALA_747" },
        },
        new AdmissionRule
        {
            Family = "run-treasure-start-of-game", Admit = AdmitScope.Player, Lift = RunTreasureLift,
            Reason = "treasure: only the player can hold it; a Start of Game effect declared through StartOfGameRules on its player "
                + "enchantment, resolved after both mulligans before the first decision: a random scripted weapon equipped with +1/+1, ten "
                + "Scrolls of Wonder shuffled (a public shuffle; each casts a random spell when drawn), three Boom Bots, two Cursed! given to "
                + "the boss (public cards it plays like any other) (c-treasures-F8 T9, T11, T13, T15, T21)",
            Ids = new[] { "DALA_737", "DALA_739", "DALA_743", "DALA_745" },
        },
        new AdmissionRule
        {
            Family = "run-treasure-random-cost", Admit = AdmitScope.Player, Lift = RunTreasureLift,
            Reason = "treasure: only the player can hold it; at its owner's turn end each card of the owner's hand gets a random Cost (0 "
                + "to 10, game RNG); the boss never knew those hidden cards' Costs, so its beliefs lose nothing (c-treasures-F8 T12)",
            Ids = new[] { "DALA_741" },
        },
        new AdmissionRule
        {
            Family = "run-treasure-power-swap", Admit = AdmitScope.Player, Lift = RunTreasureLift,
            Reason = "treasure: only the player can hold it; before the mulligan the run hero's power becomes a random scripted Death "
                + "Knight power (DungeonRunContent.PrincesRingPowers, each checked as a run player power), a public hero power (c-treasures-F8 T10)",
            Ids = new[] { "DALA_738" },
        },
        new AdmissionRule
        {
            Family = "treasure-data-gap", Admit = AdmitScope.None,
            Lift = "none: a data gap no phase owns; the client data does not state the hidden effects' triggers or chances",
            Reason = "rejected (" + DungeonRunContent.WisdomballRejection + ") (c-treasures-F8 T19)",
            Ids = new[] { "DALA_740" },
        },

        // ---------------------------------------------------------------- Tombs of Terror passive treasures (phase 6 F13)
        // The Tombs passive treasures are the run player's: no boss deck lists one and the boss's generation never yields one.
        // Each is a player enchantment the engine resolves (an aura, a trigger, a match-start change or an engine rule keyed by
        // the enchantment, RunPlayerRules), so the options list, the boss's fair view and its search read the result; none
        // reads a hidden zone. ULDA_011 VIP Membership has no match effect (a run-layer tavern flag). Each row names its
        // regression test (c-treasures-F13).
        new AdmissionRule
        {
            Family = "run-treasure", Admit = AdmitScope.Player, Lift = RunTreasureLift,
            Reason = "treasure: only the player can hold it; a public player aura or trigger the engine resolves (Reborn for a played "
                + "Deathrattle minion, a minion recruited from the owner's own deck after a spell, a copy of a played Legendary minion, "
                + "Poisonous for the owner's minions costing (2) or less, -1 Attack for the boss's minions, +1 Health and Taunt for the "
                + "owner's minions, a random friendly minion transformed into one costing (1) more at the owner's turn end, a drawn spell "
                + "at (0) for the turn), so the boss's legal moves and search see the result (c-treasures-F13 T4, T5, T7, T9, T10, T14, T17, T18)",
            Ids = new[] { "ULDA_005", "ULDA_007", "ULDA_014", "ULDA_046", "ULDA_101", "ULDA_116", "ULDA_208", "ULDA_508" },
        },
        new AdmissionRule
        {
            Family = "run-treasure-paid-cost", Admit = AdmitScope.Player, Lift = RunTreasureLift,
            Reason = "treasure: only the player can hold it; a trigger after the owner plays a card, read from the Cost the card was paid "
                + "at (the engine's TAG_LAST_KNOWN_COST_IN_HAND): an even Cost refreshes the hero power at (0) for the turn, an odd Cost "
                + "makes the owner's hand cost (1) less; both are public results the boss's legal moves and search see (c-treasures-F13 T8, T12)",
            Ids = new[] { "ULDA_042", "ULDA_103" },
        },
        new AdmissionRule
        {
            Family = "run-treasure-match-start", Admit = AdmitScope.Player, Lift = RunTreasureLift,
            Reason = "treasure: only the player can hold it; a change before the mulligan: every minion of the owner's deck and hand gets "
                + "Attack equal to its Health (the owner's own hidden cards; the boss never knew them, so its beliefs lose nothing), or the "
                + "owner starts with Spell Damage +10 that falls by 1 at each of its turn ends (a public player tag) (c-treasures-F13 T6, T15)",
            Ids = new[] { "ULDA_009", "ULDA_117" },
        },
        new AdmissionRule
        {
            Family = "run-treasure-forced-pass", Admit = AdmitScope.Player, Lift = RunTreasureLift,
            Reason = "treasure: only the player can hold it; an engine rule keyed by its player enchantment (RunPlayerRules.MustPass): on "
                + "the boss's first two turns the options list holds only End Turn and Game.Process refuses any other action, so the boss "
                + "AI (DungeonRunBossPilot + GoldenPilot) is given one legal move and ends its turn; its turn-start draws and effects still "
                + "happen (c-treasures-F13 T11, T20, T21)",
            Ids = new[] { "ULDA_102" },
        },
        new AdmissionRule
        {
            Family = "run-treasure-discover", Admit = AdmitScope.Player, Lift = RunTreasureLift,
            Reason = "treasure: only the player can hold it; after the owner Discovers a card (the engine's Discover completion), it costs "
                + "(2) less and two copies costing (2) less join the owner's hand once it has landed; the copies are of a card the owner "
                + "was shown, so no hidden zone is read (c-treasures-F13 T16)",
            Ids = new[] { "ULDA_205" },
        },
        new AdmissionRule
        {
            Family = "run-treasure-run-flag", Admit = AdmitScope.Player, Lift = RunTreasureLift,
            Reason = "treasure: only the player can hold it; no match effect: a run-layer flag that Bob's tavern reads (an upgraded "
                + "tavern: DungeonRunTavernNode), so no match, boss or search ever sees it (c-treasures-F13 T13)",
            Ids = new[] { DungeonRunContent.VipMembership },
        },

        // ---------------------------------------------------------------- family defaults (no ledger row needs them)
        new AdmissionRule { Family = "questline", Admit = AdmitScope.None, Lift = Outside, Reason = "a questline (multi-stage quest)" },
        new AdmissionRule { Family = "deckbuilding", Admit = AdmitScope.None, Lift = Outside, Reason = "needs deckbuilding selections or a sideboard" },
        new AdmissionRule { Family = "deck-size", Admit = AdmitScope.None, Lift = Outside, Reason = "changes the deck size (tag 997)" },
    };

    /// <summary>The old run exclusion phrases, kept as a detector only: a match makes a card a subject; the verdict is the table's.</summary>
    internal static readonly (string family, string[] phrases)[] Detect =
    {
        ("secret", new[] { "secret" }),
        ("deck-dependent", new[] { "no duplicates", "start of game", "if your deck", "in your deck" }),
        ("questline", new[] { "questline" }),
        ("deckbuilding", new[] { "deckbuilding", "sideboard" }),
        ("turn-rewind", new[] { "start your turn over", "restart the game" }),
    };

    private static readonly Dictionary<string, AdmissionRule> ById = BuildIndex();
    private static readonly Dictionary<string, AdmissionRule> FamilyDefaults =
        Rules.Where(r => r.Ids == null).ToDictionary(r => r.Family, StringComparer.Ordinal);

    private static Dictionary<string, AdmissionRule> BuildIndex()
    {
        var map = new Dictionary<string, AdmissionRule>(StringComparer.Ordinal);
        // A pending requirement is a rejection: the rule names its lift and admits nothing until the work is done.
        foreach (var rule in Rules.Where(r => r.Requires != null && r.Admit != AdmitScope.None))
            throw new InvalidOperationException("DungeonRunAdmission: the " + rule.Family + " rule for " + (rule.Ids?[0] ?? "(default)")
                + " admits " + rule.Admit + " while its requirement is pending: " + rule.Requires);
        foreach (var rule in Rules.Where(r => r.Ids != null))
            foreach (string id in rule.Ids)
            {
                if (map.ContainsKey(id)) throw new InvalidOperationException("DungeonRunAdmission: " + id + " has two rules");
                map[id] = rule;
            }
        return map;
    }

    /// <summary>The id rule for a card id, or null.</summary>
    internal static AdmissionRule RuleFor(string id) => id != null && ById.TryGetValue(id, out var rule) ? rule : null;

    /// <summary>The detected families of a card: tags first, then the phrases of <see cref="Detect"/> in its text.</summary>
    internal static List<string> Detected(EngineCard card, string id, string text = null)
    {
        var found = new List<string>();
        if (card != null && card[GameTag.SECRET] != 0) found.Add("secret");
        if (card != null && card[(GameTag)997] != 0) found.Add("deck-size");
        if (id != null && SabberStoneCore.Actions.DeckbuildingRules.HasSideboard(id)) found.Add("deckbuilding");
        string match = DungeonRunValidator.MatchText(card != null ? card.Text : text);
        foreach (var (family, phrases) in Detect)
            if (!found.Contains(family) && phrases.Any(match.Contains)) found.Add(family);
        return found;
    }

    /// <summary>
    /// The admission verdict of a card in <paramref name="scope"/>. A card is a subject when it is a Secret, has tag 997 or a
    /// sideboard, has a Secret or deck phrase in its text, or is listed in <see cref="Rules"/>. A listed card follows its rule;
    /// a subject card with no rule is rejected as its family when the family has a default rule (the families outside the
    /// five adventures), else as <c>unclassified</c>, so new content cannot slip in. <paramref name="text"/> is read only
    /// when <paramref name="card"/> is null.
    /// </summary>
    /// <param name="catalogRun">The card is asked for a catalog run (CatalogVersion 1 or later): a rule's
    /// <see cref="AdmissionRule.CatalogAdmit"/> replaces its <see cref="AdmissionRule.Admit"/>.</param>
    internal static AdmissionVerdict Classify(EngineCard card, string id, AdmitScope scope, string text = null, bool catalogRun = false)
    {
        id = id ?? card?.Id;
        var verdict = new AdmissionVerdict { Asked = scope };
        var rule = RuleFor(id);
        if (rule == null)
        {
            var detected = Detected(card, id, text);
            if (detected.Count == 0) return verdict;
            string family = detected.FirstOrDefault(FamilyDefaults.ContainsKey);
            if (family == null)
            {
                verdict.Subject = true;
                verdict.Family = "unclassified";
                verdict.Admit = AdmitScope.None;
                verdict.Reason = "detected as " + string.Join("/", detected) + " with no admission rule";
                verdict.Lift = AddRule;
                return verdict;
            }
            rule = FamilyDefaults[family];
        }
        verdict.Subject = true;
        verdict.Rule = rule;
        verdict.Family = rule.Family;
        verdict.Admit = rule.Admit;
        verdict.Reason = rule.Reason;
        verdict.Lift = rule.Lift;
        verdict.Requires = rule.Requires;
        if (catalogRun && rule.CatalogAdmit is AdmitScope lifted)
        {
            verdict.Admit = lifted;
            verdict.CatalogLifted = true;
            if (rule.CatalogReason != null) verdict.Reason = rule.CatalogReason;
            if (rule.CatalogLift != null) verdict.Lift = rule.CatalogLift;
        }
        return verdict;
    }

    /// <summary>The validator's error text for a rejected verdict: "admission {family}: {reason}; lifted by {lift} ({where})".</summary>
    internal static string Message(AdmissionVerdict v, string where) => $"admission {v.Family}: {v.Reason}; lifted by {v.Lift} ({where})";

    // The Generated scope over the whole card pool, computed once: every card generation could yield, classified like a
    // listed card, so a detected card with no rule (unclassified) or a family default is blocked too, never only table ids.
    private static readonly Lazy<(string[] All, string[] Boss)> GeneratedLists = new Lazy<(string[] All, string[] Boss)>(() => BuildGeneratedLists(false));
    private static readonly Lazy<(string[] All, string[] Boss)> CatalogGeneratedLists = new Lazy<(string[] All, string[] Boss)>(() => BuildGeneratedLists(true));

    private static (string[] All, string[] Boss) BuildGeneratedLists(bool catalogRun)
    {
        var all = new SortedSet<string>(StringComparer.Ordinal);
        var boss = new SortedSet<string>(StringComparer.Ordinal);
        var ids = new SortedSet<string>(Cards.All.Where(c => c?.Id != null).Select(c => c.Id), StringComparer.Ordinal);
        ids.UnionWith(ById.Keys);
        foreach (string id in ids)
        {
            EngineCard card;
            try { card = Cards.FromId(id); } catch { card = null; }
            if (card == null) continue;
            // A catalog run's player may generate Secrets, and the engine's Secret generation does not check Implemented
            // (Pressure Plate ULD_152 was generated for a Hunter): an unscripted Secret is never generated, by either side.
            if (catalogRun && card.IsSecret && !card.Implemented) { all.Add(id); continue; }
            // A catalog lift admits scripted cards only: an unscripted card (Hunter's Pack ULD_429) keeps its legacy verdict, so
            // a catalog run never generates one the legacy lists keep out (GeneratedRejects(true) holds every unscripted id of
            // GeneratedRejects(false)).
            bool lifts = catalogRun && card.Implemented;
            var generated = Classify(card, id, AdmitScope.Generated, null, lifts);
            if (generated.Rule?.NeverGenerated == true) { all.Add(id); continue; }
            // A catalog lift admits the run player's generation only: the boss side keeps generated Secrets off, so the
            // boss's own generation never yields a Secret generator (it would do nothing).
            if (generated.Admitted) { if (generated.CatalogLifted) boss.Add(id); continue; }
            // Admitted for the run player: the Generated rejection is a boss AI limit (its lift is boss-side work), so the
            // player's own Discovers and random cards keep the card and only the boss's generation drops it.
            if (Classify(card, id, AdmitScope.Player, null, lifts).Admitted) boss.Add(id); else all.Add(id);
        }
        return (all.ToArray(), boss.ToArray());
    }

    /// <summary>
    /// Cards generation never yields in a run match, whoever generates (RunMatchPlan.ExcludedGenerated): every card of the
    /// engine's pool that <see cref="Classify"/> rejects in the Generated scope and does not admit for the run player.
    /// Ordinal order.
    /// </summary>
    internal static IReadOnlyList<string> GeneratedRejects(bool catalogRun = false) => (catalogRun ? CatalogGeneratedLists : GeneratedLists).Value.All;

    /// <summary>
    /// Cards generation never yields when the boss generates, in addition to <see cref="GeneratedRejects"/>
    /// (RunMatchPlan.ExcludedGeneratedBoss): rejected in the Generated scope but admitted for the run player. Ordinal order.
    /// </summary>
    internal static IReadOnlyList<string> GeneratedRejectsBoss(bool catalogRun = false) => (catalogRun ? CatalogGeneratedLists : GeneratedLists).Value.Boss;

    /// <summary>
    /// Every card id a version-2 run of <paramref name="classId"/> can put in the player's deck, one entry per copy: the
    /// class's starter decks, every legacy bundle offered to it (<see cref="DungeonRunBundle.OfferedTo"/>, the predicate
    /// DungeonRun.OpenBundleDraft uses), and the active-treasure shells (with their upgrade stages) and tokens whose scope
    /// the class can match with any of its decks or powers. A superset of any one run's deck; public, design-time data
    /// only (<see cref="SecretPrior"/>). A migrated version-1 run keeps a deck from no listed source (DungeonRunBossPilot).
    /// </summary>
    internal static IEnumerable<string> DeckSourceCards(DungeonRunData data, string classId)
    {
        var cls = data.FindClass(classId);
        var decks = data.Decks.Where(d => string.Equals(d.Class, classId, StringComparison.Ordinal)
            || cls != null && cls.Decks.Contains(d.Id)).ToList();
        foreach (var deck in decks) foreach (string id in deck.Cards) yield return id;
        foreach (var bundle in data.LegacyBundles.Where(b => b.OfferedTo(classId))) foreach (string id in bundle.Cards) yield return id;
        foreach (string id in TreasureSourceCards(data, classId, decks.Select(d => d.Id), cls?.Powers ?? new List<string>())) yield return id;
    }

    // The active-treasure shells (with their upgrade stages) and tokens a run of classId with these decks and powers can be
    // offered, in key order.
    private static IEnumerable<string> TreasureSourceCards(DungeonRunData data, string classId, IEnumerable<string> deckList, IEnumerable<string> powerList)
    {
        var deckIds = new HashSet<string>(deckList, StringComparer.Ordinal);
        var powers = new HashSet<string>(powerList, StringComparer.Ordinal);
        var reachable = new HashSet<string>(StringComparer.Ordinal);
        // A passive treasure counts by its tokens only (ULDA_045 Primordial Bulwark puts its Secret ULDA_045t into play).
        var pending = new Queue<DungeonRunTreasure>(data.Treasures.Where(t => (t.Active || PassiveTokens(t.Id).Count > 0) && !t.Legacy && (t.Universal || t.Scope == null
            || t.Scope.IsEmpty || t.Scope.Classes.Contains(classId) || t.Scope.Decks.Any(deckIds.Contains) || t.Scope.Powers.Any(powers.Contains))));
        while (pending.Count > 0)
        {
            var treasure = pending.Dequeue();
            if (!reachable.Add(treasure.Id)) continue;
            var next = treasure.UpgradesTo == null ? null : data.Treasures.FirstOrDefault(t => t.Id == treasure.UpgradesTo);
            if (next != null) pending.Enqueue(next);
        }
        foreach (string key in reachable.OrderBy(k => k, StringComparer.Ordinal))
        {
            if (!DungeonRunContent.TryGet(key, out var content)) continue;
            if (content.Kind == RunContentKind.PassiveTreasure) { foreach (string token in content.Tokens) yield return token; continue; }
            if (content.ShellId != null) yield return content.ShellId;
            foreach (string token in content.Tokens) yield return token;
        }
    }

    // The cards a passive treasure puts into play (its content's tokens); empty for every other treasure.
    private static IReadOnlyList<string> PassiveTokens(string key) =>
        DungeonRunContent.TryGet(key, out var content) && content.Kind == RunContentKind.PassiveTreasure ? content.Tokens : Array.Empty<string>();

    /// <summary>
    /// Whether a catalog entry's required mechanic (DungeonRunBundle.RequiresMechanic) holds for a catalog run of
    /// <paramref name="runClasses"/>. "secret": the table admits player Secrets and the run's Secret prior is non-empty
    /// (<see cref="CatalogSecretPrior"/>, its deck sources read over the entries that require no mechanic, so the check never
    /// reads the entries it gates). Any other mechanic does not hold.
    /// </summary>
    internal static bool MechanicHolds(DungeonRunData data, IReadOnlyList<string> runClasses, string mechanic)
    {
        if (data == null || runClasses == null || mechanic != "secret") return false;
        return SecretsHold(data, runClasses, CatalogSecretCopies(data, runClasses, _ => false), GeneratedSecretPool(runClasses));
    }

    // "secret" holds: player Secrets are admitted and the base prior (deck sources without the Secret-requiring entries, plus
    // the player's generation pool) holds one.
    private static bool SecretsHold(DungeonRunData data, IReadOnlyList<string> runClasses, SortedDictionary<string, int> baseCopies, IReadOnlyList<string> pool) =>
        PlayerSecretsAdmitted() && (baseCopies.Count > 0 || pool.Count > 0);

    // The table admits some Secret for the run player (a Secret rule admits Player).
    private static bool PlayerSecretsAdmitted() =>
        Rules.Any(r => r.Family == "secret-card" && ((r.CatalogAdmit ?? r.Admit) & AdmitScope.Player) != 0);

    /// <summary>
    /// Every card id a catalog run (CatalogVersion 1 or later) of <paramref name="runClasses"/> can put in the player's deck,
    /// one entry per source: the run row's starter decks (each copy), every catalog entry eligible for the run
    /// (<see cref="DungeonRunBundle.EligibleFor"/>, the predicate DungeonRun.OpenCatalogDraft uses; a fixed entry's cards per
    /// copy, a pool entry's candidates once per entry), and the active-treasure shells and tokens any run class can be
    /// offered. Public, design-time data only.
    /// </summary>
    internal static IEnumerable<string> CatalogDeckSourceCards(DungeonRunData data, IReadOnlyList<string> runClasses, Func<string, bool> mechanicHolds)
    {
        if (data == null || runClasses == null) yield break;
        var rows = data.Classes.Where(c => c.ClassSet.SequenceEqual(runClasses)).ToList();
        var deckIds = new HashSet<string>(rows.SelectMany(r => r.Decks), StringComparer.Ordinal);
        foreach (var deck in data.Decks.Where(d => deckIds.Contains(d.Id))) foreach (string id in deck.Cards) yield return id;
        foreach (var entry in data.Bundles.Where(b => b.EligibleFor(runClasses, mechanicHolds)))
        {
            if (entry.IsPool) foreach (var c in entry.Candidates) yield return c.Id;
            else foreach (string id in entry.Cards) yield return id;
        }
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string cls in runClasses)
            foreach (string id in TreasureSourceCards(data, cls, data.Decks.Where(d => deckIds.Contains(d.Id) || d.Class == cls).Select(d => d.Id),
                rows.SelectMany(r => r.Powers).Concat(data.FindClass(cls)?.Powers ?? new List<string>())))
                if (seen.Add(id)) yield return id;
    }

    // Copies of each admitted player Secret across a catalog run's deck sources.
    private static SortedDictionary<string, int> CatalogSecretCopies(DungeonRunData data, IReadOnlyList<string> runClasses, Func<string, bool> mechanicHolds)
    {
        var copies = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (string id in CatalogDeckSourceCards(data, runClasses, mechanicHolds))
        {
            EngineCard card;
            try { card = id == null ? null : Cards.FromId(id); } catch { card = null; }
            if (card == null || !card.IsSecret || !Classify(card, id, AdmitScope.Player, null, catalogRun: true).Admitted) continue;
            copies.TryGetValue(id, out int n);
            copies[id] = n + 1;
        }
        return copies;
    }

    /// <summary>
    /// Every Secret the run player's own generation can yield in a catalog run match of <paramref name="runClasses"/>: each
    /// Secret of the engine's generation pools (OfflinePoolPolicy members and the engine's wild class lists) that a run class
    /// may hold (CardClassRules.IsAllowedInDeck), minus what generation never yields for the player
    /// (<see cref="GeneratedRejects"/> of a catalog run; RunMatchPlan.ExcludedGenerated, which holds every unscripted Secret).
    /// Ordinal order.
    /// </summary>
    internal static IReadOnlyList<string> GeneratedSecretPool(IReadOnlyList<string> runClasses)
    {
        var result = new SortedSet<string>(StringComparer.Ordinal);
        if (runClasses == null || runClasses.Count == 0) return result.ToArray();
        var blocked = new HashSet<string>(GeneratedRejects(catalogRun: true), StringComparer.Ordinal);
        var ids = new SortedSet<string>(StringComparer.Ordinal);
        try { foreach (var m in OfflinePoolPolicy.Load().Members) if (m?.CardId != null) ids.Add(m.CardId); } catch { }
        foreach (string name in runClasses)
        {
            if (!Enum.TryParse(name, out CardClass cls) || cls == CardClass.NEUTRAL || cls == CardClass.INVALID) continue;
            if (Cards.FormatTypeClassCards(FormatType.FT_WILD).TryGetValue(cls, out var classCards))
                foreach (var c in classCards) if (c?.Id != null) ids.Add(c.Id);
        }
        foreach (string id in ids)
        {
            EngineCard card;
            try { card = Cards.FromId(id); } catch { card = null; }
            if (card == null || !card.IsSecret || blocked.Contains(id)) continue;
            foreach (string name in runClasses)
                if (Enum.TryParse(name, out CardClass cls) && cls != CardClass.NEUTRAL && cls != CardClass.INVALID
                    && SabberStoneCore.Actions.CardClassRules.IsAllowedInDeck(card, cls)) { result.Add(id); break; }
        }
        return result.ToArray();
    }

    /// <summary>
    /// Neutral Secret generators that draw from named classes whatever the run's classes (GILA_509 Frenzied Trapper puts 5
    /// random Hunter Secrets into play; GILA_903 Enhance-a-matic adds a random spell of each class, which may be a Secret):
    /// <see cref="SecretGeneratorClasses"/> counts each as those classes.
    /// </summary>
    internal static readonly IReadOnlyDictionary<string, string[]> NeutralSecretGeneratorClasses = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        ["GILA_509"] = new[] { "HUNTER" },
        ["GILA_903"] = DungeonRunContent.EnhanceAMaticClasses.Select(c => c.ToString()).ToArray(),
    };

    /// <summary>
    /// The card classes the Secret generators of a catalog run's deck sources draw from: the class of each card among
    /// <see cref="CatalogDeckSourceCards"/> (grants included) that the table admits for the player in the
    /// secret-generated family. A generator can yield Secrets of its own class whatever the run's classes are (Professor
    /// Putricide ICC_204 casts a random Hunter Secret, granted to Rogue by Tess Greymane); a Discover generator's run-class
    /// Secrets are in <see cref="GeneratedSecretPool"/> already. Neutral and run classes are left out. Ordinal order.
    /// </summary>
    internal static IReadOnlyList<string> SecretGeneratorClasses(DungeonRunData data, IReadOnlyList<string> runClasses, Func<string, bool> mechanicHolds)
    {
        var classes = new SortedSet<string>(StringComparer.Ordinal);
        if (data == null || runClasses == null) return classes.ToArray();
        foreach (string id in new HashSet<string>(CatalogDeckSourceCards(data, runClasses, mechanicHolds).Where(x => x != null), StringComparer.Ordinal))
        {
            EngineCard card;
            try { card = Cards.FromId(id); } catch { card = null; }
            if (card == null) continue;
            // A neutral generator that names its Secrets' classes (GILA_509 Frenzied Trapper: Hunter Secrets; GILA_903
            // Enhance-a-matic: every class) counts as those classes.
            string[] named = NeutralSecretGeneratorClasses.TryGetValue(id, out string[] fixedClasses) ? fixedClasses : null;
            if (named == null && (card.Class == CardClass.NEUTRAL || card.Class == CardClass.INVALID)) continue;
            var verdict = Classify(card, id, AdmitScope.Player, null, catalogRun: true);
            if (!verdict.Subject || verdict.Family != "secret-generated" || !verdict.Admitted) continue;
            foreach (string name in named ?? new[] { card.Class.ToString() })
                if (!runClasses.Contains(name)) classes.Add(name);
        }
        return classes.ToArray();
    }

    /// <summary>
    /// The boss's Secret prior for a catalog run of <paramref name="runClasses"/>, whose player may generate Secrets
    /// (RunMatchPlan.PlayerGeneratedSecrets): the union of (a) every admitted player Secret of the run's deck sources
    /// (<see cref="CatalogDeckSourceCards"/>, with the Secret-requiring entries once the base pool holds a Secret), weight
    /// 4 x (1 + count), count = the eligible entries listing it plus its copies in fixed entries and starters; and (b) every
    /// Secret the player's generation can yield: the run classes' (<see cref="GeneratedSecretPool"/>) and those of the
    /// classes its deck sources' Secret generators draw from (<see cref="SecretGeneratorClasses"/>), weight 1 (a Secret in
    /// both keeps (a)). Public information only. Null when both are empty.
    /// </summary>
    internal static IReadOnlyDictionary<string, double> CatalogSecretPrior(DungeonRunData data, IReadOnlyList<string> runClasses)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        var copies = CatalogSecretCopies(data, runClasses, _ => false);
        var pool = GeneratedSecretPool(runClasses);
        Func<string, bool> holds = _ => false;
        if (SecretsHold(data, runClasses, copies, pool)) { holds = m => m == "secret"; copies = CatalogSecretCopies(data, runClasses, holds); }
        var prior = new SortedDictionary<string, double>(StringComparer.Ordinal);
        foreach (string id in pool) prior[id] = 1.0;
        foreach (string id in GeneratedSecretPool(SecretGeneratorClasses(data, runClasses, holds))) prior[id] = 1.0;
        foreach (var p in copies) prior[p.Key] = 4.0 * (1 + p.Value);
        if (prior.Count == 0) return null;
        return new System.Collections.ObjectModel.ReadOnlyDictionary<string, double>(new Dictionary<string, double>(prior, StringComparer.Ordinal));
    }

    /// <summary>
    /// The closed, design-time pool of Secrets a run player of <paramref name="playerClass"/> can hold, with prior weights
    /// for the boss's fair search (FairOptions.OpponentSecretPrior). It reads every source a version-2 run can put in the
    /// player's deck (<see cref="DeckSourceCards"/>): the class's starter decks, the neutral and class bundles,
    /// and the active-treasure shells and tokens the class can be offered. Weight = 1 + the copies of the Secret across
    /// those sources. A card counts when it is a Secret the table admits for the player. Public information only: it never
    /// reads the player's drafted deck. Null while the run pool holds no Secret (an empty closed pool would certify
    /// nothing a Secret could refute; the boss keeps the open class pool, and secret-worlds settlement stays off).
    /// </summary>
    internal static IReadOnlyDictionary<string, double> SecretPrior(DungeonRunData data, CardClass playerClass)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        var copies = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (string id in DeckSourceCards(data, playerClass.ToString()))
        {
            EngineCard card;
            try { card = id == null ? null : Cards.FromId(id); } catch { card = null; }
            if (card == null || !card.IsSecret || !Classify(card, id, AdmitScope.Player).Admitted) continue;
            copies.TryGetValue(id, out int n);
            copies[id] = n + 1;
        }
        if (copies.Count == 0) return null;
        return new System.Collections.ObjectModel.ReadOnlyDictionary<string, double>(
            copies.ToDictionary(p => p.Key, p => 1.0 + p.Value, StringComparer.Ordinal));
    }

    /// <summary>
    /// The boss's Secret prior for a run match: a catalog run's (<paramref name="playerGeneratedSecrets"/>,
    /// RunMatchPlan.PlayerGeneratedSecrets) is <see cref="CatalogSecretPrior"/> over the run's classes; a legacy run's is the
    /// closed <see cref="SecretPrior(DungeonRunData, CardClass)"/> of its (only) class, byte for byte.
    /// </summary>
    internal static IReadOnlyDictionary<string, double> SecretPrior(DungeonRunData data, IReadOnlyList<string> runClasses, bool playerGeneratedSecrets)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        if (runClasses == null || runClasses.Count == 0) return null;
        if (playerGeneratedSecrets) return CatalogSecretPrior(data, runClasses);
        return Enum.TryParse(runClasses[0], out CardClass cls) ? SecretPrior(data, cls) : null;
    }
}
