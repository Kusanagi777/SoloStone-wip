// Phase 8, family 13 (the Rumble Run champions and their boss shrines): the admission rows of the cards it lifts or scripts (a
// method the table concatenates: DungeonRunAdmission.BuildRules). Each of the 27 board shrines is a boss's too: a Rumble Run
// champion's encounter row names the three shrines of its class as variants, armed for the boss by ShrineRules.Arm exactly as
// the run player's (phase 7). Family 01 moved the three Warrior shrines (War Master Voone); this file holds the other 24, which
// left the phase-7 rows board-shrine, hidden-steal (TRLA_186) and shrine-token (TRLA_129s). The tests named in the reasons are
// the suite c-enc-13 (verify/suites/p8f13) and the enc13 bosspilot modes.
internal static partial class DungeonRunAdmission
{
    /// <summary>TRLA_110 Halazzi's Trap on a legacy run's boss: never (a boss holds a shrine only through an encounter row).</summary>
    internal const string BossShrineSecretsLift = "none: a boss holds Halazzi's Trap only through an encounter row (a catalog run) that turns its generated Secrets on";

    private static AdmissionRule[] RulesF13() => new[]
    {
        new AdmissionRule
        {
            NeverGenerated = true,
            Family = "boss-shrine", Admit = AdmitScope.Player | AdmitScope.Boss, Lift = NoneBossShrine,
            Reason = "a board shrine on either side: the run player's (phase 7: the shrine of a rumble_<class>_<n> starter) or a Rumble Run "
                + "champion's (the encounter row's shrine variant, armed for the boss by ShrineRules.Arm exactly as the player's: it lands "
                + "after both mulligans, goes dormant when it dies or would be transformed, bounced or shuffled, and revives at full Health "
                + "after three of its owner's turns; the other side's search reproduces dormancy and revival through the endpoint "
                + "projection). Every script reads its own controller, so its effect is the same public engine resolution on either side: "
                + "Divine Shield after a friendly minion survives damage (TRLA_105), Stealth on its owner's other minions and +2 Attack "
                + "when it goes dormant (TRLA_106), Spell Damage +1 per spell in its owner's hand (TRLA_108: a public Spell Damage number, "
                + "the count of a hand its owner knows), the Overload amount as Attack (TRLA_109), Mana Crystals refreshed on an Armor "
                + "gain (TRLA_112), spells paid with Health (TRLA_113), Deathrattles three times (TRLA_114), +1/+1 on each summon "
                + "(TRLA_115), the hero's Attack added at the end of its owner's turn (TRLA_116), a free hero power refreshed by a kill "
                + "(TRLA_128), a Frostfire per freeze its owner causes (TRLA_129), 5 damage to the enemy hero after its owner's hero takes "
                + "damage (TRLA_137, recursion-guarded), a spell cast on a friendly minion cast again on the shrine (TRLA_138) or again on "
                + "its target (TRLA_187: RecastRules, never recursive), a random Priest spell after each spell (TRLA_146) and a random "
                + "Demon per discard (TRLA_178), both through the generation filter of the side that generates (a boss's: "
                + "RunMatchPlan.ExcludedGeneratedBoss, so never a Secret or a card the table keeps from a boss), healing of its owner's "
                + "enemies dealt as damage (TRLA_147), Battlecries three times (TRLA_154), spells (2) cheaper (TRLA_155), aura-granted "
                + "Overkill cost cuts (TRLA_162), a random keyword on its owner's summoned Beasts (TRLA_163) and damage to its owner's hero "
                + "on its owner's turn dealt to the enemy hero instead (TRLA_179). No hidden zone of the other side is read or written "
                + "(c-shrine-core S1-S12, c-shrines-S1/S2/S3 and c-p7fix for the run player's; c-enc-13 S105-S187 for a boss's, in real engine games through the run setup "
                + "path; the enc13 bosspilot modes)",
            Ids = new[] { "TRLA_105", "TRLA_106", "TRLA_108", "TRLA_109", "TRLA_112", "TRLA_113", "TRLA_114", "TRLA_115", "TRLA_116", "TRLA_128",
                "TRLA_129", "TRLA_137", "TRLA_138", "TRLA_146", "TRLA_147", "TRLA_154", "TRLA_155", "TRLA_162", "TRLA_163", "TRLA_178", "TRLA_179",
                "TRLA_187" },
        },
        new AdmissionRule
        {
            NeverGenerated = true,
            Family = "boss-shrine", Admit = AdmitScope.Player, Lift = BossShrineSecretsLift,
            Reason = "TRLA_110 Halazzi's Trap, a board shrine (phase 7): after its owner casts a spell it puts a random Hunter Secret into "
                + "play for its owner (put, not cast; game RNG; reported as a random event). The run player's on a board-shrine run, which "
                + "is a catalog run whose player may generate Secrets (the boss's Secret prior holds every Hunter Secret that generation "
                + "yields, GeneratedSecretPool) (c-shrines-S1)",
            CatalogAdmit = AdmitScope.Player | AdmitScope.Boss,
            CatalogReason = "a board shrine on either side of a catalog run: the run player's (phase 7) or Zul'jin's late variant (trl_204_zuljin), "
                + "armed for the boss by ShrineRules.Arm. On the boss's side its Hunter Secrets need the boss's generated Secrets: the row turns "
                + "them on (bossGeneratedSecrets, RunMatchPlan.BossGeneratedSecrets) and the validator closes the yield pool (every scripted "
                + "Hunter Secret a catalog run's generation lists leave is admitted for a boss: DungeonRunValidator.BossSecretPool); the "
                + "engine draws from DungeonRunContent.RandomSecretCandidates for the boss, never through the boss's Discovers or random "
                + "cards. The boss knows its own Secrets; the run player sees a face-down Secret (c-enc-13 S110; the enc13 bosspilot modes)",
            CatalogLift = NoneBossShrine,
            BossNeeds = BossNeedsGeneratedSecrets,
            BossSecretClass = SabberStoneCore.Enums.CardClass.HUNTER,
            Ids = new[] { "TRLA_110" },
        },
        new AdmissionRule
        {
            NeverGenerated = true,
            Family = "boss-hidden-steal", Admit = AdmitScope.Player | AdmitScope.Boss, Lift = NoneBossShrine,
            Reason = "TRLA_186 Treasure from Below, a board shrine on either side: at the start of its owner's turn a random card of the "
                + "opponent's deck moves to its owner's hand and costs (2) less (DungeonRunShrines.TakeFromDeck: a deck exit and a "
                + "transfer whose definition only the new owner sees; a full hand burns it). On the run player's side (phase 7) the boss "
                + "learns only that a card left its deck (its own-deck ledger, OwnDeckLedger, drops an unknown slot by the deck count) and "
                + "masks the stolen card like the rest of the player's hand (c-shrines-S3 G9-G15). On a boss's side (Captain Hooktusk's "
                + "middle variant, trl_202_hooktusk) the boss learns exactly the card it took, now in its own hand, and nothing else: the "
                + "run player's other deck cards stay unknown slots in its fair view, the stolen card is a card of its hand like any "
                + "other, and its search never simulates its own next turn start, where the steal happens (c-enc-13 S186; the enc13 "
                + "bosspilot modes)",
            Ids = new[] { "TRLA_186" },
        },
        new AdmissionRule
        {
            NeverGenerated = true,
            Family = "boss-shrine-token", Admit = AdmitScope.Player | AdmitScope.Boss, Lift = NoneBossShrine,
            Reason = "a card a board shrine makes for its owner during the game (TRLA_129s Frostfire: TRLA_129 Jan'alai's Progeny puts one "
                + "into its owner's hand per freeze that owner causes); a public 0-cost 'Deal 1 damage' spell in the run player's hand "
                + "(phase 7, c-shrines-S2) or in Hex Lord Malacrass's (his late variant: the boss knows its own hand, the run player sees "
                + "a hand count), never a deck card or a generation result (c-enc-13 S129; the enc13 bosspilot modes)",
            Ids = new[] { "TRLA_129s" },
        },
        new AdmissionRule
        {
            Family = "boss-power-public", Admit = AdmitScope.Boss, Lift = NoneBossPower,
            Reason = "Zul'jin's hero power Steady Throw (TRLA_065p): 2 damage to the enemy hero, no target, as Steady Shot; public hero "
                + "state the engine resolves, no hidden zone (c-enc-13 Z1-Z4; the enc13 bosspilot modes)",
            Ids = new[] { "TRLA_065p" },
        },
    };
}
