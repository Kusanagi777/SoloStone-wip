using System;

// Phase 8, family 01 (the encounter foundation): the admission rows of the cards it scripts. One file per encounter family
// (DungeonRunAdmission.F<NN>.cs), each a method the table concatenates (DungeonRunAdmission.BuildRules).
internal static partial class DungeonRunAdmission
{
    private static AdmissionRule[] RulesF01() => new[]
    {
        new AdmissionRule
        {
            NeverGenerated = true,
            Family = "boss-shrine", Admit = AdmitScope.Player | AdmitScope.Boss, Lift = NoneBossShrine,
            Reason = "a board shrine on either side: the run player's (phase 7) or a Rumble Run champion's (the encounter row's shrine "
                + "variant, armed for the boss by ShrineRules.Arm exactly as the player's: it lands after both mulligans, goes dormant when "
                + "it dies or would be transformed, bounced or shuffled, and revives after three of its owner's turns). The boss holds a "
                + "public minion whose effect is public engine resolution it plans with through the endpoint projection: Attack equal to "
                + "its owner's Armor (TRLA_104), Dragon cost cuts after its owner summons a Dragon (TRLA_170), aura-granted Overkill on its "
                + "owner's minions (TRLA_171); no hidden zone is read or written (c-enc-start B1-B6, enc01)",
            Ids = new[] { "TRLA_104", "TRLA_170", "TRLA_171" },
        },
        // ---------------------------------------------------------------- the four Secret bosses and boss-side generated Secrets
        new AdmissionRule
        {
            Family = "secret-named", Admit = AdmitScope.Boss, Lift = NoneBossPower,
            Reason = "Elder Brandlemar's Dampen Magic: puts one fixed, known Secret (Counterspell) into play for the boss, who knows its "
                + "own Secrets; put, not cast, never a second copy (the power is not offered while Counterspell is in play or the "
                + "Secret zone is full, read from the boss's own public Secret count and its own Secrets). The run player sees a "
                + "face-down Secret as from any Secret card (c-enc-secrets S1-S6, the enc01 bosspilot mode)",
            Ids = new[] { "LOOTA_BOSS_35p" },
        },
        new AdmissionRule
        {
            // Admitted in every scope, as the family is (phase 2): they generate nothing. Only an encounter row names them.
            Family = "secret-from-deck", Admit = AdmitScope.Any, Lift = NoneBossPower,
            Reason = "a boss hero power that works on the boss's own Secrets and generates none, so the closed Secret pool stays "
                + "complete: Trapped Room's passive Alarm summons a 3/3 Sawblade when one of the boss's Secrets is revealed (a public "
                + "event; started at match start by EncounterRules.ActivatePower), Manhunter Ivan's Trap Preparation puts a random "
                + "Secret of the boss's deck into play (Mad Scientist's rule), and Trap Room's Tricks and Traps plays one from the "
                + "boss's hand or deck (an effect cast). The boss's deck is a known multiset with a sampled order, as for every "
                + "from-own-deck card, and its own-deck ledger drops the Secret it saw leave (OwnDeckLedger: a deck card that went into "
                + "its owner's Secret zone). Both powers are offered whenever the Secret zone has room, never by what the deck holds "
                + "(the search samples the deck: an option legal only in a sample could not be taken), and do nothing with no "
                + "Secret left (c-enc-secrets S7-S27, the enc01 bosspilot mode)",
            Ids = new[] { "GILA_BOSS_25p", "LOOTA_BOSS_48p", "ULDA_BOSS_44p" },
        },
        new AdmissionRule
        {
            Family = "secret-generated", Admit = AdmitScope.None, Lift = GeneratedSecretsLift,
            Reason = "generates a Secret; legacy run matches block generated Secrets (GameConfig.ExcludeGeneratedSecrets), so it would do nothing",
            CatalogAdmit = AdmitScope.Player | AdmitScope.Boss | AdmitScope.Generated,
            CatalogReason = "names \"a random Hunter Secret\" and puts it into play (Professor Putricide after its owner plays a Secret, "
                + "Streetsmuggler after a friendly Secret is revealed). A catalog run lets the run player generate Secrets and the "
                + "boss's Secret prior holds every Secret that generation can yield. In a boss deck (phase 8) the encounter row must "
                + "turn the boss side's generated Secrets on (RunMatchPlan.BossGeneratedSecrets), and the validator closes the yield "
                + "pool: every scripted Hunter Secret a catalog run's generation lists leave (DungeonRunValidator.BossSecretPool) is "
                + "admitted for a boss; the engine reads that pool for the boss (Game.BossNamesSecrets, "
                + "DungeonRunContent.RandomSecretCandidates), never the boss's Discovers or random cards, and reports the roll as a "
                + "random event. The boss's own generation never yields either card (c-enc-secrets S34-S50, the enc01 and "
                + "secretfix bosspilot modes)",
            CatalogLift = "none: admitted for the run player and, in an encounter row with bossGeneratedSecrets, for a boss",
            BossNeeds = BossNeedsGeneratedSecrets,
            BossSecretClass = SabberStoneCore.Enums.CardClass.HUNTER,
            Ids = new[] { "ICC_204", "TRLA_168" },
        },
        new AdmissionRule
        {
            NeverGenerated = true,
            Family = "global-rule-symmetric", Admit = AdmitScope.Rule, Lift = NoneRule,
            Reason = "a global rule both players play under for the whole match: its player enchantment is attached to each controller at "
                + "match start (DungeonRunGlobalRules.Apply), a public, board-visible effect the engine resolves for both sides, so the "
                + "boss's legal moves and its search see it: each player's hero is healed 2 at that player's turn start (DALA_866), each "
                + "player's minions gain +1/+1 at the end of that player's turn (DALA_864), spells in each hand cost (2) less (DALA_850); no "
                + "hidden zone changes (c-enc-rules R1-R9, the rules01 bosspilot mode)",
            Ids = new[] { "DALA_850", "DALA_864", "DALA_866" },
        },
        new AdmissionRule
        {
            NeverGenerated = true,
            Family = "fight-hero-card", Admit = AdmitScope.Player, Lift = NoneFightHero,
            Reason = "a hero card the Hagatha encounter gives the run player for that fight only (the other monster hunters): playing it is "
                + "the engine's own hero card play (a public hero and power change), its Battlecry a public Discover among the hunter's "
                + "three passive treasures (a player enchantment for this match, never a run treasure), public damage to all minions and "
                + "the hunter's own cards shuffled into the run player's deck (the boss sees a deck count, as always). The run's hero, "
                + "power and deck records are never written (DungeonRunState.HeroSwitch; c-enc-model H1-H9, the hagatha bosspilot mode)",
            Ids = new[] { "GILA_500h4", "GILA_600h2" },
        },
        new AdmissionRule
        {
            NeverGenerated = true,
            Family = "fight-hero-card", Admit = AdmitScope.None, Lift = TurnRestartLift,
            Reason = "Toki's hero card for the Hagatha fight: its hero power is GILA_900p Temporal Loop (\"Start your turn over\"), a turn "
                + "restart rejected for good in step 9 (NativeMatch has no client history re-sync); the encounter leaves the card out",
            Ids = new[] { "GILA_900h2" },
        },
    };
}
