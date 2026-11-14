// Phase 8, family 08 (Dungeon Run bosses B: passive and trigger powers, both-player effects, boss deck cards): the admission
// rows of the cards it scripts (a method the table concatenates: DungeonRunAdmission.BuildRules). The hero powers and Tag
// Team are a boss's only; the boss deck minions are listed by boss decks only, and are admitted in the run player's hands
// too, where a steal or a copy of a boss card can put one. The tests named in the reasons are the suite c-enc-08 and the
// enc08 bosspilot modes (scratchpad p8f08).
internal static partial class DungeonRunAdmission
{
    private static AdmissionRule[] RulesF08() => new[]
    {
        new AdmissionRule
        {
            Family = "boss-public-power", Admit = AdmitScope.Boss, Lift = NoneBossPower,
            Reason = "a clickable boss hero power whose whole effect is public board, hero or own-hand state the engine resolves, with no "
                + "hidden zone read: Divinity gives the boss's minions Divine Shield (LOOTA_BOSS_15p), Sprouting Spore summons a Deadly "
                + "Spore (LOOTA_BOSS_36p), Arcane Infusion adds one known card, Arcane Missiles, to the boss's own hand (LOOTA_BOSS_37p), "
                + "Giant Stomp deals 1 damage to the run player's minions (LOOTA_BOSS_39p), Fading Light gives a minion -1 Attack "
                + "(LOOTA_BOSS_40p), Mystic Barrier gains 3 Armor (LOOTA_BOSS_42p), Fungal Infection gives the boss's minions +1/+1 "
                + "(LOOTA_BOSS_46p). A power that would do nothing is not offered, by a condition that reads only the public boards or "
                + "the boss's own hand count, so an option of the search is always an option of the real game "
                + "(c-enc-08 A2, A2b, E1-E2, F1-F2, G1-G2, H1-H2, J1, L1-L2; the enc08 bosspilot mode)",
            Ids = new[] { "LOOTA_BOSS_15p", "LOOTA_BOSS_36p", "LOOTA_BOSS_37p", "LOOTA_BOSS_39p", "LOOTA_BOSS_40p", "LOOTA_BOSS_42p", "LOOTA_BOSS_46p" },
        },
        new AdmissionRule
        {
            Family = "boss-form", Admit = AdmitScope.Boss, Lift = NoneBossOnly,
            Reason = "Tag Team, the tag team's own deck card (George and Karl / Karl and George): it swaps the boss's active hero with the "
                + "benched one, hero and power (EncounterRules.TagTeam on EncounterRules.SwapForm, the row's script form). The whole state "
                + "is public or the boss's own: the active hero and power are board entities, the benched hero's damage is one private "
                + "tag on the boss's controller (EncounterRules.TagTeamBench) that search clones carry, and a dead active hero is "
                + "replaced by the benched one inside the engine's death check. In any other hands, or once the benched hero is "
                + "defeated, the card does nothing (c-enc-08 A1, A3-A14; the enc08 and enc08-tag bosspilot modes)",
            Ids = new[] { "LOOTA_BOSS_110" },
        },
        new AdmissionRule
        {
            Family = "boss-both-players", Admit = AdmitScope.Boss, Lift = NoneBossPower,
            Reason = "a passive boss hero power that changes a rule for BOTH players, started at match start "
                + "(EncounterRules.ActivatePower): all Deathrattles trigger twice (LOOTA_BOSS_16p Totem of the Dead), all Battlecries "
                + "trigger twice (LOOTA_BOSS_17p Battle Totem), both (LOOTA_BOSS_38p Totem of Chaos) - one controller aura on each "
                + "player, read by the engine's own Deathrattle and Battlecry repeat rules, so it never stacks with a doubling a side "
                + "already has - and The Floor is Lava deals 2 damage to every minion either player plays from hand "
                + "(LOOTA_BOSS_47p). Public, symmetric engine resolution the boss's search plays through on both sides; no hidden zone "
                + "is read (c-enc-08 B1-B11, M1-M4; the enc08 bosspilot mode)",
            Ids = new[] { "LOOTA_BOSS_16p", "LOOTA_BOSS_17p", "LOOTA_BOSS_38p", "LOOTA_BOSS_47p" },
        },
        new AdmissionRule
        {
            Family = "boss-passive-trigger", Admit = AdmitScope.Boss, Lift = NoneBossPower,
            Reason = "a passive boss hero power that reacts to a public event, started at match start: the run player casting a spell "
                + "summons a Tunnel Trogg for the boss (LOOTA_BOSS_19p Digest Magic) or draws the boss a card that costs (1) "
                + "(LOOTA_BOSS_33p Metabolized Magic: the boss's own draw from its own deck, a known multiset with a sampled order), and "
                + "a minion the boss plays from hand gets Charge (LOOTA_BOSS_31p Charrrrrge!). The trigger is the cast or the play "
                + "itself, never the identity of a hidden card (c-enc-08 C1-C7, D1-D3; the enc08 bosspilot mode)",
            Ids = new[] { "LOOTA_BOSS_19p", "LOOTA_BOSS_31p", "LOOTA_BOSS_33p" },
        },
        new AdmissionRule
        {
            Family = "boss-hidden-reveal", Admit = AdmitScope.Boss, Lift = NoneBossPower,
            Reason = "Whompwhisker's Join the Fray, \"Both players Recruit a minion\": a random minion of each deck is summoned, the boss's "
                + "first. The run player's card becomes a public board minion by a public summon as it leaves the deck (the deck stays "
                + "a hidden zone that is one card shorter; nothing else of it is shown), and the boss's own-deck ledger sees its own "
                + "card leave. In the boss's fair-view projection the run player's deck holds no known minion, so there only the "
                + "boss's own minion arrives: the search never reads the hidden deck, and the real summon is a random event it "
                + "re-plans after. Offered by board room only, never by what a deck holds (c-enc-08 I1-I6; the enc08 bosspilot mode)",
            Ids = new[] { "LOOTA_BOSS_41p" },
        },
        new AdmissionRule
        {
            Family = "boss-both-draw", Admit = AdmitScope.Boss, Lift = NoneBossPower,
            Reason = "Jeeru's Three Wee Wishes, \"Each player draws 3 cards\": the boss draws three from its own deck, then the run player "
                + "three from theirs, by the ordinary draw rules on each side (fatigue from an empty deck, a card burned by a full hand, "
                + "which is then public as from any overdraw). The run player's drawn cards go from one hidden zone to another: the "
                + "boss learns the counts only, and its fair view masks them like every other card of that hand "
                + "(c-enc-08 K1-K5; the enc08 bosspilot mode)",
            Ids = new[] { "LOOTA_BOSS_43p" },
        },
        new AdmissionRule
        {
            Family = "boss-control", Admit = AdmitScope.Boss, Lift = NoneBossPower,
            Reason = "Russell the Bard's Alluring Tune, \"Gain control of an enemy minion with 2 or less Attack\": a targeted control change "
                + "of a public board minion (the engine's ControlTask on the target). A run shrine never changes sides, so it is never a "
                + "legal target (ShrineRules.TakesControlOfTarget), awake or dormant; the power needs a free slot on the boss's board "
                + "(c-enc-08 N1-N6; the enc08 bosspilot mode)",
            Ids = new[] { "LOOTA_BOSS_51p" },
        },
        new AdmissionRule
        {
            // Player | Boss: no player role offers one (boss decks list them, a boss power summons the Spore), but the run player
            // can take one from a boss (a deck steal, a copy), and its public effect resolves the same there. Never generated.
            NeverGenerated = true,
            Family = "boss-card", Admit = AdmitScope.Player | AdmitScope.Boss, Lift = NoneBossOnly,
            Reason = "a Dungeon Run boss deck minion (or the token a boss power summons) whose effect is public board and hero resolution "
                + "from public events, the same in either player's hands: Starving Crab deals 5 damage to its owner's hero at the start "
                + "of its owner's turn (LOOTA_102), Reverberating Ooze summons a copy of itself after it survives damage (LOOTA_104), "
                + "Deadly Spore is Poisonous and also destroys a hero it deals damage to (LOOTA_105, "
                + "DungeonRunEncounters.PoisonousToHeroes), Shroomsayer destroys all minions after it survives damage (LOOTA_107: a run "
                + "shrine goes dormant as from any destroy effect), Gluttonous Trogg gains +1/+1 whenever its owner's opponent casts a "
                + "spell (LOOTA_109). No hidden zone is read or written. A boss deck lists them or a boss power summons them; the run "
                + "player holds one only by taking it from a boss (c-enc-08 C8, E3-E8, O1-O9; the enc08, enc08-cards and "
                + "enc08-trapped bosspilot modes)",
            Ids = new[] { "LOOTA_102", "LOOTA_104", "LOOTA_105", "LOOTA_107", "LOOTA_109" },
        },
    };
}
