// Phase 8, family 11 (Monster Hunt bosses B: thirteen Witchwood encounters with a clickable hero power, Crooked Pete's form
// change and their boss cards): the admission rows of the cards it scripts (a method the table concatenates:
// DungeonRunAdmission.BuildRules). The hero powers are a boss's only; the boss deck cards and the Wisp token are listed by boss
// decks or made by a boss power and are admitted in the run player's hands too, where a steal or a copy of a boss card can
// put one. The tests named in the reasons are the suite c-enc-11 (verify/suites/p8f11) and the enc11 bosspilot modes.
internal static partial class DungeonRunAdmission
{
    private static AdmissionRule[] RulesF11() => new[]
    {
        new AdmissionRule
        {
            Family = "boss-hunt-power", Admit = AdmitScope.Boss, Lift = NoneBossPower,
            Reason = "a clickable Monster Hunt boss hero power whose whole effect is public board, graveyard and hero state the engine "
                + "resolves: Survival of the Fittest makes every minion of both boards, in play order, attack a random enemy minion "
                + "(GILA_BOSS_41p; EncounterRules.AllAttackRandomEnemyMinions on family 06's ForcedAttack), Bulldoze spends the boss's "
                + "Armor as one hit on a random minion (GILA_BOSS_42p), Grave Mistake resolves every minion's Deathrattles twice for "
                + "its own controller (GILA_BOSS_43p), Hollow Needle deals 1 damage to a minion and heals the boss 3 if it dies "
                + "(GILA_BOSS_44p), Death and Taxes deals the run hero 1 damage per minion it controls (GILA_BOSS_51p), Beast Within "
                + "gives the boss's minions +1 Attack (GILA_BOSS_52p2, Beastly Pete's), Unfinished Business summons three 1/1 Wisps "
                + "(GILA_BOSS_55p), A New Face transforms a minion into a random scripted minion that costs (2) more behind the "
                + "generation filter of its controller's side (GILA_BOSS_56p; a run shrine chosen goes dormant: "
                + "ShrineRules.InterceptTransform), Desecrate deals 1 damage to all minions and is readied again when one dies "
                + "(GILA_BOSS_57p; EncounterRules.PowerRefresh, one private tag on the power) and Cull the Meek destroys a minion with "
                + "the lowest Attack (GILA_BOSS_62p). A power that would do nothing is not offered, by a condition that reads only the "
                + "public boards, heroes and Armor; every random pick uses the game RNG and is reported as a random event; no hidden "
                + "zone is read (c-enc-11 sections B, C, D, E, F, G, I, J, K and M; the enc11a and enc11b bosspilot modes)",
            Ids = new[] { "GILA_BOSS_41p", "GILA_BOSS_42p", "GILA_BOSS_43p", "GILA_BOSS_44p", "GILA_BOSS_51p", "GILA_BOSS_52p2", "GILA_BOSS_55p",
                "GILA_BOSS_56p", "GILA_BOSS_57p", "GILA_BOSS_62p" },
        },
        new AdmissionRule
        {
            Family = "boss-hunt-own-hand", Admit = AdmitScope.Boss, Lift = NoneBossPower,
            Reason = "a clickable Monster Hunt boss hero power that writes the boss's own hand, which the boss knows exactly and the run "
                + "player sees as a count: Grafted Bark adds to the boss's hand a 2/2 copy that costs (2) of a public board minion whose "
                + "card the run's generation filter lets the boss be given (GILA_BOSS_40p; offered only while the hand has room) and "
                + "One with the Trees makes the minions the boss holds cost (1) less (GILA_BOSS_54p; offered only while it holds a "
                + "minion that costs (1) or more). The run player's hidden zones are neither read nor written (c-enc-11 sections A and "
                + "H; the enc11a and enc11b bosspilot modes)",
            Ids = new[] { "GILA_BOSS_40p", "GILA_BOSS_54p" },
        },
        new AdmissionRule
        {
            Family = "boss-hunt-form", Admit = AdmitScope.Boss, Lift = NoneBossPower,
            Reason = "Crooked Pete's Soot Up, \"Give a friendly minion Stealth until your next turn\" (a public board enchantment), and "
                + "its start-of-turn trigger: at the start of his turn at half his maximum Health or less Pete becomes Beastly Pete, "
                + "the row's script form (EncounterRules.SwapForm: hero GILA_BOSS_52h2, power Beast Within; the encounter model's "
                + "form index, a private tag). His hand goes to the set-aside zone, his deck is replaced by Beastly Pete's list (the "
                + "own-deck fact EncounterRules.DeckReplaced keeps the boss's own-deck ledger exact) and he draws 6 from it, gains 15 "
                + "Armor and keeps his Health; the run player's beliefs about his hand and deck are reset (RunHelpers.BeliefReset). "
                + "No hidden zone of the run player is read or written (c-enc-11 section G; the enc11a and enc11-pete bosspilot modes)",
            Ids = new[] { "GILA_BOSS_52p" },
        },
        new AdmissionRule
        {
            Family = "boss-hunt-deck-shuffle", Admit = AdmitScope.Boss, Lift = NoneBossPower,
            Reason = "Grum's Terrify, \"Shuffle a minion into its owner's deck. It costs (4) less\": a public board minion of either "
                + "side goes into its own controller's deck through the engine's MoveToDeck (Recycle's path: a shuffle fact; the card "
                + "that entered the deck was public) and is Terrified there (Costs (4) less). A run shrine chosen is destroyed instead "
                + "and goes dormant (ShrineRules.InterceptLeave); a minion whose owner's deck is full (a public count) is not a choice. "
                + "The boss's own-deck ledger learns a card it shuffled into its own deck from the ordinary shuffle facts (c-enc-11 "
                + "section L; the enc11b bosspilot mode)",
            Ids = new[] { "GILA_BOSS_58p" },
        },
        new AdmissionRule
        {
            // Player | Boss: no player role offers one (boss decks list them, a boss power makes the Wisp, Grave Mistake the spell
            // is the hunt's treasure that no run offers yet), but the run player can take one from a boss (a deck steal, a copy,
            // a control change), and its effect resolves the same there. Never generated.
            NeverGenerated = true,
            Family = "boss-hunt-card-b", Admit = AdmitScope.Player | AdmitScope.Boss, Lift = NoneBossOnly,
            Reason = "a Monster Hunt boss deck card or token whose effect is public board, graveyard and hero resolution, the same in "
                + "either player's hands: Hack deals 1 damage to a minion five times, each hit with Spell Damage (GILA_BOSS_41t), Grave "
                + "Mistake (the spell) resolves every minion's Deathrattles twice (GILA_BOSS_43t), the Wisp is a vanilla 1/1 "
                + "(GILA_BOSS_55t), Soul Assimilation destroys its caster's Wisps and takes control of a random enemy minion for each "
                + "(GILA_BOSS_55t2; a run shrine never changes sides) and Shallow Graves summons up to seven random minions of either "
                + "side that died this game (GILA_BOSS_57t; the public graveyards, never a dead run shrine: ShrineRules.IsCorpse, and "
                + "never a card the generation filter keeps from the caster). No hidden zone of the other side is read or written "
                + "(c-enc-11 sections B, D, I and K; the enc11a, enc11b and enc11-cards bosspilot modes)",
            Ids = new[] { "GILA_BOSS_41t", "GILA_BOSS_43t", "GILA_BOSS_55t", "GILA_BOSS_55t2", "GILA_BOSS_57t" },
        },
    };
}
