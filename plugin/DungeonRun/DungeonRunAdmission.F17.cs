// Phase 8, family 17 (Dalaran Heist bosses D: DALA_BOSS_46 to 59, the bank card Infuriated Banker and the Kirin Tor Guard): the
// admission rows of the cards it scripts (a method the table concatenates: DungeonRunAdmission.BuildRules). The hero powers are
// a boss's only; the boss cards are listed by boss decks, made by boss powers or put on the run player's side by a start block
// (The Wire, The Other Wire, Void Shift) or a power (Extortion), and are admitted in the run player's hands too. The family
// names are its own ("heist-d-..."). The tests named in the reasons are the suite c-enc-17 (verify/suites/p8f17/c-enc-17) and
// the enc17 bosspilot modes (verify/bin/bp.sh).
internal static partial class DungeonRunAdmission
{
    private static AdmissionRule[] RulesF17() => new[]
    {
        new AdmissionRule
        {
            Family = "heist-d-board-power", Admit = AdmitScope.Boss, Lift = NoneBossPower,
            Reason = "a clickable Dalaran Heist boss hero power whose whole effect is public board state the engine resolves: Protective "
                + "Bubble gives a minion of the boss Divine Shield (DALA_BOSS_46p; only one without it), Lava Belch summons a 0/3 Molten "
                + "Rock (DALA_BOSS_47p), Tinker summons a 0/4 Elementium Squirrel Bomb (DALA_BOSS_51p), Hold the Gates! gives a minion "
                + "+2 Health and Taunt (DALA_BOSS_52p), Boogie Woogie gives a minion of the boss \"Deathrattle: Resummon this minion\" "
                + "(DALA_BOSS_53p, Ancestral Spirit's rule) and Blast Powder coats a minion so that it is destroyed when it takes damage "
                + "(DALA_BOSS_58p; only one not coated yet; a run shrine destroyed so goes dormant). A power that would do nothing is not "
                + "offered, by a condition on the public boards; no hidden zone is read (c-enc-17 sections A, B, F, G, H, M; the enc17 "
                + "bosspilot modes)",
            Ids = new[] { "DALA_BOSS_46p", "DALA_BOSS_47p", "DALA_BOSS_51p", "DALA_BOSS_52p", "DALA_BOSS_53p", "DALA_BOSS_58p" },
        },
        new AdmissionRule
        {
            Family = "heist-d-void-power", Admit = AdmitScope.Boss, Lift = NoneBossPower,
            Reason = "Zuramat the Obliterator's To The Void, \"Banish a minion to the Void\" (2): a public board minion leaves play with "
                + "no death and no Deathrattle for its own controller's set-aside zone, held by its Voided enchantment (DALA_BOSS_48e) "
                + "through family 10's set-aside store (EncounterRules.Banish on EncounterRules.Store: the store's private link tag, "
                + "which game clones and the boss's fair view carry; the banished minion was public and stays known to both sides). A "
                + "run shrine is never a legal target and never enters the Void; nothing hidden is read (c-enc-17 section C; the enc17 "
                + "bosspilot modes)",
            Ids = new[] { "DALA_BOSS_48p" },
        },
        new AdmissionRule
        {
            Family = "heist-d-passive", Admit = AdmitScope.Boss, Lift = NoneBossPower,
            Reason = "a passive Dalaran Heist boss hero power (registered with the passive powers, started at match start; never an "
                + "option) whose trigger answers public events: Uncontrollable Energy deals 1 damage to all enemies after the boss casts "
                + "a spell (DALA_BOSS_49p), Spell Lock makes every spell either player plays Overload its player for the Cost it was played "
                + "for (DALA_BOSS_54p: EncounterRules.OverloadFor, the engine's Overload bookkeeping and event; each Tunnel Trogg of that "
                + "player gains its Attack for the amount, as for a printed Overload) and Greed is Good puts a "
                + "new copy of the first card the run player plays each turn in the boss's hand and gives the run player a Gallywix's "
                + "Coin (DALA_BOSS_57p: a played card is public; the turn of the last purchase is a public tag of the power). No hidden "
                + "zone is read (c-enc-17 sections D, I, L; the enc17 bosspilot modes)",
            Ids = new[] { "DALA_BOSS_49p", "DALA_BOSS_54p", "DALA_BOSS_57p" },
        },
        new AdmissionRule
        {
            Family = "heist-d-auto-cast", Admit = AdmitScope.Boss, Lift = NoneBossPower,
            Reason = "a Dalaran Heist boss hero power the wiki marks auto-cast, cast by rule and never an option of the boss's AI "
                + "(DungeonRunEncounters.AutoCast, family 09's rule): at the start of the boss's turn, when it can pay, Sand Breath deals "
                + "2 damage to a random enemy (DALA_BOSS_50p: the enemy hero or a minion an effect can pick, the game RNG) and Raise the "
                + "Alarm summons a 1/4 Kirin Tor Guard while its board has room (DALA_BOSS_55p). Public board, hero and Mana state only "
                + "(c-enc-17 sections E, J; the enc17 bosspilot modes)",
            Ids = new[] { "DALA_BOSS_50p", "DALA_BOSS_55p" },
        },
        new AdmissionRule
        {
            Family = "heist-d-hidden-auto-cast", Admit = AdmitScope.Boss, Lift = NoneBossPower,
            Reason = "a Dalaran Heist boss hero power whose worth lies in the run player's hidden zones, which the boss's fair view "
                + "cannot see, so it is cast by rule and never an option of the boss's AI (DungeonRunEncounters.AutoCast, family 09's "
                + "rule for such powers), on public conditions only (Mana, board room, the opponent's deck or hand count): Bribery "
                + "summons a random minion of the run player's deck for the boss (DALA_BOSS_56p: Tad's Pole's rule, "
                + "DungeonRunEncounters.PoleCatch, a public summon of the card, so it becomes known as it arrives; "
                + "Bribed returns a new copy to the run player's hand when it dies) and Blackmail replaces a random card of the run "
                + "player's hand with an Extortion (DALA_BOSS_59p: EncounterRules.Blackmail, the hand escrow of HandZone.DetachForEscrow; "
                + "the taken card stays hidden in the run player's set-aside zone, the link is the private tag "
                + "EncounterRules.ExtortionEscrow, and the boss's fair view masks the card, so the boss never learns it). In the boss's "
                + "simulations the hidden zones are sampled or masked, so there the power moves a stand-in or nothing (c-enc-17 sections "
                + "K, N; the enc17 bosspilot modes)",
            Ids = new[] { "DALA_BOSS_56p", "DALA_BOSS_59p" },
        },
        new AdmissionRule
        {
            // Player | Boss: boss decks list them and boss powers make them, and four reach the run player by rule (The Wire and
            // The Other Wire start on its board at Boommaster Flark, two Void Shifts start in its hand at Zuramat, Blackmail gives
            // it Extortions); the run player can also take one from a boss (a steal, a copy, a control change). Never generated.
            NeverGenerated = true,
            Family = "heist-d-boss-card", Admit = AdmitScope.Player | AdmitScope.Boss, Lift = NoneBossOnly,
            Reason = "a Dalaran Heist boss card whose effect the engine resolves the same in either player's hands: Molten Rock deals 1 "
                + "damage to all characters at the end of its player's turn (DALA_BOSS_47t), Void Shift destroys all minions and summons "
                + "a new copy of every minion in the Void for its caster, then empties the Void (DALA_BOSS_48t, EncounterRules.VoidShift; "
                + "the Void is public), Elementium Squirrel Bomb destroys itself and deals 2 damage to all enemies at the start of its "
                + "player's turn (DALA_BOSS_51t), Thorium Chicken deals 3 damage to the enemy hero after the enemy plays a card "
                + "(DALA_BOSS_51t2), Rival Accomplice is a vanilla 4/4 (DALA_BOSS_56t), The Wire and The Other Wire are 3/3 permanents "
                + "that deal 5 damage to their player's hero at the start of its turn while they touch, once per touching pair "
                + "(DALA_BOSS_58t, 58t2), Lotus Bruiser gains +2/+2 whenever an Extortion is played (DALA_BOSS_59t), Fruit Slasher "
                + "deals 4 damage to an enemy and, with Combo, 1 to all other enemies (DALA_BOSS_59t2), Extortion returns the card it "
                + "holds to its player's hand (DALA_BOSS_59t3: only its own player's escrowed card), Infuriated Banker gains +1/+1 "
                + "whenever a Coin is played (DALA_502: The Coin, Counterfeit Coin, Gallywix's Coin) and Kirin Tor Guard has +1 Attack "
                + "for each other friendly Kirin Tor Guard (DALA_503). Each is public board, hand-count or set-aside state; no hidden "
                + "zone of the other side is read (c-enc-17 sections B, C, F, G, J, K, M, N, O; the enc17 and enc17-cards bosspilot modes)",
            Ids = new[] { "DALA_BOSS_47t", "DALA_BOSS_48t", "DALA_BOSS_51t", "DALA_BOSS_51t2", "DALA_BOSS_56t", "DALA_BOSS_58t", "DALA_BOSS_58t2",
                "DALA_BOSS_59t", "DALA_BOSS_59t2", "DALA_BOSS_59t3", "DALA_502", "DALA_503" },
        },
    };
}
