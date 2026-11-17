// Phase 8, family 18 (Dalaran Heist bosses E and the Heist finale: twelve Heist encounters, the four finale bosses, the boss card
// Kirin Tor Elemental and the finale's rule): the admission rows of the cards it scripts or rejects (a method the table
// concatenates: DungeonRunAdmission.BuildRules). The hero powers are a boss's only; the boss card is made by a boss power and is
// admitted in the run player's hands too, where a steal, a copy or a control change can put one. The family names are the ones
// families 06, 08 and 10 introduced, and the design's boss-hidden-shuffle and boss-rejected-data. The tests named in the reasons
// are the suite c-enc-18 (verify/suites/p8f18/c-enc-18) and the enc18 bosspilot modes (verify/bin/bp.sh).
internal static partial class DungeonRunAdmission
{
    /// <summary>The lift of Commander Bolan's quests (a data gap: no source for the five quests).</summary>
    internal const string BolanQuestsLift = "none: a data gap no phase owns; it needs a source for Commander Bolan's five quests (their requirements) and his summon list; then a script of the quests as run-player enchantments that end the fight with a no-loss win";
    /// <summary>The lift of a card the adventure never used (nothing to restore).</summary>
    internal const string UnusedCardLift = "none: unused in the adventure (no encounter, deck, treasure or reward of The Dalaran Heist names it)";

    private static AdmissionRule[] RulesF18() => new[]
    {
        new AdmissionRule
        {
            Family = "boss-public-power", Admit = AdmitScope.Boss, Lift = NoneBossPower,
            Reason = "a clickable Dalaran Heist boss hero power whose whole effect is public board and hero state the engine resolves: "
                + "Darken gives every minion of both boards -2 Attack until the boss's next turn (DALA_BOSS_62p, Darkened), Bully deals 2 "
                + "damage to a damaged minion (DALA_BOSS_63p), Invest! returns a minion of the boss to its own hand and gives it +4/+4 "
                + "there (DALA_BOSS_64p, Capital Gains; offered only while its hand has room), Bombardment deals 2 damage to every minion "
                + "and is offered again only on the boss's turn after next (DALA_BOSS_70p: the shot's turn is a tag of the power), Summon "
                + "Protectors summons a 2/2 Treant with Taunt (DALA_BOSS_71p), Earthmother's Rage gives a minion of the boss Windfury "
                + "(DALA_BOSS_73p), Dragonwrath gives the boss's hero Spell Damage +1 for the match (DALA_BOSS_69p) and Summon Elemental "
                + "summons a Kirin Tor Elemental (DALA_BOSS_60p). A power that would do nothing is not offered, by a condition that reads "
                + "only the public boards and the boss's own hand count; no hidden zone is read (c-enc-18 sections B, C, D, G, H, J, P, "
                + "Q; the enc18 and enc18-finale bosspilot modes)",
            Ids = new[] { "DALA_BOSS_62p", "DALA_BOSS_63p", "DALA_BOSS_64p", "DALA_BOSS_70p", "DALA_BOSS_71p", "DALA_BOSS_73p", "DALA_BOSS_69p", "DALA_BOSS_60p" },
        },
        new AdmissionRule
        {
            Family = "boss-own-hand-power", Admit = AdmitScope.Boss, Lift = NoneBossPower,
            Reason = "a clickable Dalaran Heist boss hero power that writes the boss's own hand, which it knows exactly and the run player "
                + "sees as a count: Soul Weave discards a random card of the boss's hand (a public discard) and summons a random scripted "
                + "minion of that Cost from the pool the run's generation filter allows for the boss (DALA_BOSS_74p), Arcane Runes adds one "
                + "of the wiki's five Arcane spells to its hand behind the same filter (DALA_BOSS_67p). Every pick uses the game RNG and is "
                + "a reported random event; the run player's hidden zones are neither read nor written (c-enc-18 sections L, O; the enc18 "
                + "and enc18-finale bosspilot modes)",
            Ids = new[] { "DALA_BOSS_74p", "DALA_BOSS_67p" },
        },
        new AdmissionRule
        {
            Family = "boss-passive-trigger", Admit = AdmitScope.Boss, Lift = NoneBossPower,
            Reason = "a passive Dalaran Heist boss hero power (registered with the passive powers, started at match start; never an "
                + "option) whose rule follows public events or a tag: Impervious caps a hit on the boss's hero at 3 damage "
                + "(DALA_BOSS_65p, the family 02 HeroDamageCap rule on the power), Mini Portal summons a random scripted 1-Cost minion "
                + "behind the generation filter after the boss casts a spell (DALA_BOSS_68p), Steelskin prevents the damage the boss's own "
                + "sources deal to its hero (DALA_BOSS_72p, EncounterRules.SelfDamageImmune, read in Character.TakeDamage) and Kaironomaly "
                + "sets the run player's turn timer to 25 seconds at the start of each of that player's turns (DALA_BOSS_43p: the "
                + "client's TIMEOUT tag; NativeMatch ends a turn whose short timer ran out, EncounterRules.ShortTurnSeconds, as for "
                + "Nozdormu the Eternal's 15). No hidden zone is read (c-enc-18 sections E, I, K, N; the enc18 and enc18-finale bosspilot "
                + "modes)",
            Ids = new[] { "DALA_BOSS_65p", "DALA_BOSS_68p", "DALA_BOSS_72p", "DALA_BOSS_43p" },
        },
        new AdmissionRule
        {
            Family = "boss-hidden-reveal", Admit = AdmitScope.Boss, Lift = NoneBossPower,
            Reason = "Sael'orn's Web Grab, \"Your opponent summons a minion from their hand\" (0): Dirty Rat's pull, one random minion of "
                + "the run player's hand summoned to that player's board without its Battlecry (nothing with no minion there or a full "
                + "board). The summoned minion becomes public as any summon does; the rest of the hand stays hidden. In the boss's fair "
                + "view the run player's hidden cards are no minions, so its search pulls nothing and the real pull is a random public "
                + "event; the power is offered by public counts only (that board has room, that hand holds a card) (c-enc-18 section A; "
                + "the enc18 bosspilot mode)",
            Ids = new[] { "DALA_BOSS_61p" },
        },
        new AdmissionRule
        {
            Family = "boss-hidden-shuffle", Admit = AdmitScope.Boss, Lift = NoneBossPower,
            Reason = "Marei Loom's Next..., \"Swap the left-most card in each player's hand with one in their deck\" (0): for each player "
                + "with a card in hand and in deck, a random deck card (the game RNG) takes the left-most hand slot and that hand card is "
                + "shuffled into the deck (EncounterRules.SwapLeftmostWithDeck). Each side sees only its own new card; what the opponent "
                + "believed about either player's hand and deck is reset (RunHelpers.BeliefReset), so the boss's fair view keeps the run "
                + "player's hand masked and never learns the card that came to it. The wiki marks it Auto-cast (the card carries "
                + "AI_MUST_PLAY; its worth lies in hidden decks the boss's AI cannot evaluate): it is cast by rule at the start of the "
                + "boss's turn, after the draw, never an option of the boss's AI (family 09's rule-cast powers), while a player has a card "
                + "in hand and in deck, by zone counts only (c-enc-18 section F; the enc18 bosspilot mode)",
            Ids = new[] { "DALA_BOSS_66p" },
        },
        new AdmissionRule
        {
            // Player | Boss: no player role offers it (Khadgar's power summons it), but the run player can take one (a control
            // change, a copy), and it resolves the same there. Never generated.
            NeverGenerated = true,
            Family = "boss-card", Admit = AdmitScope.Player | AdmitScope.Boss, Lift = NoneBossOnly,
            Reason = "Kirin Tor Elemental, \"Your spells cast an additional time.\" (DALA_504, Archmage Khadgar's Summon Elemental): after "
                + "its controller casts a spell a copy is cast again on the same target (RecastRules.Recast, Pirate's Mark's rule; a copy "
                + "never casts again, a Secret or a Quest is not cast twice). Public board resolution, the same in either player's hands "
                + "(c-enc-18 section Q; the enc18-finale bosspilot mode)",
            Ids = new[] { "DALA_504" },
        },
        new AdmissionRule
        {
            NeverGenerated = true,
            Family = "global-rule-run", Admit = AdmitScope.Rule, Lift = NoneRule,
            Reason = "Twist - The Finale, \"This run has four additional powerful bosses\" (DALA_830): a twist the run layer carries "
                + "(schedule-v1.json effect \"run\": no match script, never in a match plan's rule list). It is the rule of the "
                + "schedule's finale block (acts-v3h: four fixed fights, Norroa, Vargoth, Kalec and Khadgar, after act III's major), "
                + "rolled at NewRun with chance 100 for the finale act and shown as that act's rule (c-enc-18 section R; the "
                + "enc18-finale bosspilot mode on the fixture schedule)",
            Ids = new[] { "DALA_830" },
        },
        new AdmissionRule
        {
            Family = "boss-rejected-data", Admit = AdmitScope.None, Lift = BolanQuestsLift,
            Reason = "Commander Bolan's A Quest!, \"Give your opponent a Quest!\": the five quests it hands out (DALA_BOSS_75e1-75e5) have "
                + "no text, requirement or reward in the client data and the wiki gives none, nor the minions he summons instead of a "
                + "deck; the encounter is rejected (reference/encounters/F18.json \"rejected\")",
            Ids = new[] { "DALA_BOSS_75p" },
        },
        new AdmissionRule
        {
            Family = "unused-card", Admit = AdmitScope.None, Lift = UnusedCardLift,
            Reason = "a client card The Dalaran Heist never used: Phase Spider (DALA_BOSS_61t; the wiki: \"an unused card intended for "
                + "use during Sael'orn encounter\", in neither of her decks) and Cheerful Spirit (DALA_501; the wiki: \"an unused mage "
                + "minion\"). No encounter, deck, treasure or reward of the adventure can put one into a game",
            Ids = new[] { "DALA_BOSS_61t", "DALA_501" },
        },
    };
}
