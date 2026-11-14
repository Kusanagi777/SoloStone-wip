// Phase 8, family 10 (Monster Hunt bosses A: fifteen Witchwood encounters with a clickable hero power and their boss cards):
// the admission rows of the cards it scripts (a method the table concatenates: DungeonRunAdmission.BuildRules). The hero
// powers are a boss's only; the boss deck cards and the tokens are listed by boss decks only and are admitted in the run
// player's hands too, where a steal or a copy of a boss card can put one. The tests named in the reasons are the suite
// c-enc-10 and the enc10 bosspilot modes (scratchpad p8f10).
internal static partial class DungeonRunAdmission
{
    private static AdmissionRule[] RulesF10() => new[]
    {
        new AdmissionRule
        {
            Family = "boss-board-power", Admit = AdmitScope.Boss, Lift = NoneBossPower,
            Reason = "a clickable Monster Hunt boss hero power whose whole effect is public board and hero state the engine resolves "
                + "(and the boss's own ordinary draws): Fading Bite gives the boss's hero +2 Attack this turn (GILA_BOSS_21p), Shank "
                + "deals 1 damage to a minion and draws the boss a card (GILA_BOSS_22p), Chuck destroys a minion of the boss and deals "
                + "its Attack to the enemy hero (GILA_BOSS_23p), Consume destroys a minion of the boss and draws it 3 cards "
                + "(GILA_BOSS_27p), Splinter deals 2 damage to the boss's own hero and summons a 2/2 Treant (GILA_BOSS_31p), Unstable "
                + "Growth gives all minions of both boards +1/+1 (GILA_BOSS_32p), Harvest destroys a minion of the boss and restores 8 "
                + "Health to its hero (GILA_BOSS_33p), Devour Soul halves the enemy hero's Health, rounded in the hero's favour and never "
                + "below 1, without damage (GILA_BOSS_34p), Bloodthirst gives a minion of the boss Lifesteal (GILA_BOSS_35p) and Witch's "
                + "Kiss transforms a random minion of either board into a 0/1 Frog with Taunt (GILA_BOSS_36p; a run shrine picked is "
                + "destroyed instead and goes dormant: ShrineRules.InterceptTransform). A power that would do nothing is not offered, by "
                + "a condition that reads only the public boards and heroes; every random pick uses the game RNG and is reported as a "
                + "random event; no hidden zone is read (c-enc-10 B1-B3, C1-C2, D1-D2, F1-F2, H1-H3, I1-I4, J1-J2, K1-K4, L1-L3, "
                + "M1-M5; the enc10a and enc10b bosspilot modes)",
            Ids = new[] { "GILA_BOSS_21p", "GILA_BOSS_22p", "GILA_BOSS_23p", "GILA_BOSS_27p", "GILA_BOSS_31p", "GILA_BOSS_32p", "GILA_BOSS_33p",
                "GILA_BOSS_34p", "GILA_BOSS_35p", "GILA_BOSS_36p" },
        },
        new AdmissionRule
        {
            Family = "boss-own-hand-power", Admit = AdmitScope.Boss, Lift = NoneBossPower,
            Reason = "a clickable Monster Hunt boss hero power that writes the boss's own hand, which the boss knows exactly and the run "
                + "player sees as a count: Guttural Howl gives a random minion the boss holds +1/+1 (GILA_BOSS_20p; offered only while "
                + "it holds a minion), It's Raining Fin draws up to three random Murlocs from the boss's own deck by ordinary draws of "
                + "those cards (GILA_BOSS_37p; offered by hand room only, never by what the deck holds, which the boss's search only "
                + "samples) and Call of the Raven adds a random scripted 1-Cost minion to its hand from the pool the run's generation "
                + "filter allows for the boss (GILA_BOSS_38p). The run player's hidden zones are neither read nor written "
                + "(c-enc-10 A1-A4, N1-N4, O1-O3; the enc10a and enc10b bosspilot modes)",
            Ids = new[] { "GILA_BOSS_20p", "GILA_BOSS_37p", "GILA_BOSS_38p" },
        },
        new AdmissionRule
        {
            Family = "boss-control-all", Admit = AdmitScope.Boss, Lift = NoneBossPower,
            Reason = "Sazzmi Gentlehorn's Infatuation, \"Gain control of enemy minions with 1 or less Attack\": a control change of every "
                + "such public board minion, in board order (the engine's ControlTask on each: one taken while the boss's board is full "
                + "is destroyed). A run shrine never changes sides and is left as it is, awake or dormant (ShrineRules.CanChangeController); "
                + "the power is offered only while a minion it can take is on the enemy board, which is public "
                + "(c-enc-10 G1-G5; the enc10a bosspilot mode)",
            Ids = new[] { "GILA_BOSS_29p" },
        },
        new AdmissionRule
        {
            Family = "boss-setaside-store", Admit = AdmitScope.Boss, Lift = NoneBossPower,
            Reason = "Gnomenapper's Nab, \"Put an enemy minion into a sack\": a public board minion of the run player leaves play for the "
                + "set-aside store of a Stuffed Sack summoned for the boss (EncounterRules.Store: the minion itself, its damage and "
                + "enchantments kept, in its own controller's set-aside zone; the link is one private tag on the sack, "
                + "EncounterRules.SetAsideStore, that game clones and the boss's fair view carry). The stored minion was public and "
                + "stays known to both sides; no hidden zone is read. Nab needs a free slot on the boss's board, and a run shrine is "
                + "never a legal target, awake or dormant (a shrine never enters a store) "
                + "(c-enc-10 E1-E14; the enc10a and enc10-nab bosspilot modes)",
            Ids = new[] { "GILA_BOSS_26p" },
        },
        new AdmissionRule
        {
            // Player | Boss: no player role offers one (boss decks list them, a boss power or card makes the tokens, the Scarecrow
            // starts with the Scythe), but the run player can take one from a boss (a deck steal, a copy, a control change of the
            // sack), and its effect resolves the same there. Never generated.
            NeverGenerated = true,
            Family = "boss-hunt-card", Admit = AdmitScope.Player | AdmitScope.Boss, Lift = NoneBossOnly,
            Reason = "a Monster Hunt boss deck card, start weapon or token whose effect is public board and hero resolution, the same in "
                + "either player's hands: Chuck, the spell, discards every minion its caster holds and deals the Attack of each to the "
                + "enemy hero (GILA_BOSS_23t: the caster's own hand), Stuffed Sack returns the minion it holds to that minion's own "
                + "controller when it dies (GILA_BOSS_26t, EncounterRules.Release; a silenced, transformed or bounced sack returns "
                + "nothing), Sack of Gnomes fills its caster's board with random Gnomes of a fixed list behind the generation filter "
                + "(GILA_BOSS_26t2), Amalgamate destroys all minions and summons one Amalgamation with their combined Attack and Health "
                + "(GILA_BOSS_27t; a run shrine goes dormant as from any destroy effect) and the Amalgamation is every minion type "
                + "(GILA_BOSS_27t2), Enchanting Tune takes control of all enemy minions and sets them to 1/1 (GILA_BOSS_29t; a run "
                + "shrine never changes sides), the Scythe never loses Durability from an attack and gains +1 Attack when its hero kills "
                + "a minion with it (GILA_BOSS_33t), Vampiric Fangs destroys a minion and restores its Health to the caster's hero "
                + "(GILA_BOSS_35t) and Murloc Infestation gives the caster's minions \"Deathrattle: Summon a random Murloc\" "
                + "(GILA_BOSS_37t: a scripted Murloc behind the generation filter). No hidden zone of the other side is read or written "
                + "(c-enc-10 D3-D5, E3-E16, F3-F8, G6-G9, J3-J11, L4-L6, N5-N7; the enc10a, enc10b, enc10-nab and enc10-cards "
                + "bosspilot modes)",
            Ids = new[] { "GILA_BOSS_23t", "GILA_BOSS_26t", "GILA_BOSS_26t2", "GILA_BOSS_27t", "GILA_BOSS_27t2", "GILA_BOSS_29t", "GILA_BOSS_33t",
                "GILA_BOSS_35t", "GILA_BOSS_37t" },
        },
    };
}
