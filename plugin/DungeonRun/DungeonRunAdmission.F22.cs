// Phase 8, family 22 (Tombs of Terror bosses D: eighteen encounters ULDA_BOSS_60-79 without 64 and 67, and Siamat's Servant
// ULDA_026): the admission rows of the cards it scripts (a method the table concatenates: DungeonRunAdmission.BuildRules). The
// hero powers are a boss's only; the Servant is a boss's token that the run player can take from the board (a copy, a control
// change), so it is admitted on both sides and never generated. The family names are the family's own (boss-tombs-d-*), apart
// from family 19's boss-tombs-* names, so neither family's counts move with the other's rows. The tests named in the reasons are
// the suite c-enc-22 (verify/suites/p8f22) and the enc22 bosspilot modes.
internal static partial class DungeonRunAdmission
{
    private static AdmissionRule[] RulesF22() => new[]
    {
        new AdmissionRule
        {
            Family = "boss-tombs-d-board-power", Admit = AdmitScope.Boss, Lift = NoneBossPower,
            Reason = "a clickable Tombs of Terror boss hero power whose whole effect is public board and hero state the engine resolves: "
                + "Chain Lightning deals 1 damage to a minion and jumps to a random adjacent minion until one dies (ULDA_BOSS_61p), "
                + "Masterful Djinn summons a 1/4 Servant of Siamat with a random one of Taunt, Rush, Windfury or Divine Shield "
                + "(ULDA_BOSS_66p), \"Upgraded\" Fauna deals as much damage to an enemy minion as the boss controls Beasts (ULDA_BOSS_68p), "
                + "Lord of the Sun gives the boss Spell Damage +3 this turn (ULDA_BOSS_70p; offered while the boss's own hand holds a "
                + "spell), Lord of Life restores a damaged minion to full Health (ULDA_BOSS_71p), Burst of Light deals 1 damage to each "
                + "enemy minion with Lifesteal (ULDA_BOSS_74p), Noble Champion deals damage to a random enemy minion equal to its Attack "
                + "(ULDA_BOSS_76p) and Snapping Jaw doubles the boss's hero's Attack this turn (ULDA_BOSS_77p). A power that would do "
                + "nothing is not offered, by a condition that reads only the public boards or the boss's own hand; no hidden zone is "
                + "read (c-enc-22 sections B, F, G, I, J, M, O, P; the enc22 bosspilot modes)",
            Ids = new[] { "ULDA_BOSS_61p", "ULDA_BOSS_66p", "ULDA_BOSS_68p", "ULDA_BOSS_70p", "ULDA_BOSS_71p", "ULDA_BOSS_74p", "ULDA_BOSS_76p",
                "ULDA_BOSS_77p" },
        },
        new AdmissionRule
        {
            Family = "boss-tombs-d-growing-power", Admit = AdmitScope.Boss, Lift = NoneBossPower,
            Reason = "a clickable Tombs of Terror boss hero power whose damage is a number the power keeps in its own public script "
                + "number (the @ of its text, shown by the client): Forbidden Power deals N damage to a random enemy character, N rising "
                + "by 1 whenever a minion of the boss's dies, a public death (ULDA_BOSS_60p), and Hunt the Weak deals N damage to a "
                + "minion, N rising by 1 after each use and back to 1 when the minion dies of it (ULDA_BOSS_75p). The boss's search "
                + "reads the number from the power itself; no hidden zone is read (c-enc-22 sections A, N; the enc22 bosspilot modes)",
            Ids = new[] { "ULDA_BOSS_60p", "ULDA_BOSS_75p" },
        },
        new AdmissionRule
        {
            Family = "boss-tombs-d-forced-attack", Admit = AdmitScope.Boss, Lift = NoneBossPower,
            Reason = "Armadillobot's Mech \"Pilot\" (ULDA_BOSS_79p): a Mech on either board gets +2 Attack this turn and attacks a random "
                + "enemy minion of its own at once (EncounterRules.ForcedAttack, Mass Hysteria's rule: its state does not matter and no "
                + "attack of the turn is spent; game RNG, reported as a random event). Public board resolution only (c-enc-22 section "
                + "R; the enc22 bosspilot modes)",
            Ids = new[] { "ULDA_BOSS_79p" },
        },
        new AdmissionRule
        {
            Family = "boss-tombs-d-auto-cast", Admit = AdmitScope.Boss, Lift = NoneBossPower,
            Reason = "a Tombs of Terror boss hero power the wiki lists as auto-cast, cast by rule at the start of the boss's turn, never an "
                + "option of its AI (DungeonRunEncounters.AutoCast): Lord of Destruction curses a random enemy minion with Cursed! "
                + "(ULDA_BOSS_72e: 2 damage to that minion's controller's hero at the end of each of its turns; curses stack) while the "
                + "run player controls a minion (ULDA_BOSS_72p), and Massive Tentacles destroys every minion on both boards when there "
                + "are 8 or more (ULDA_BOSS_78p; a run shrine goes dormant as from any destroy effect, a dormant or permanent minion "
                + "neither counts nor is destroyed). Both conditions read only the public boards (c-enc-22 sections K, Q; the enc22 "
                + "bosspilot modes)",
            Ids = new[] { "ULDA_BOSS_72p", "ULDA_BOSS_78p" },
        },
        new AdmissionRule
        {
            Family = "boss-tombs-d-passive", Admit = AdmitScope.Boss, Lift = NoneBossPower,
            Reason = "a passive Tombs of Terror boss hero power started at match start (EncounterRules.ActivatePower) that changes only the "
                + "boss's own cards and mana: Plaguebringer makes the spells in the boss's hand cost (2) less and each spell it plays "
                + "Overload (1) more, as any Overload (ULDA_BOSS_63p: a hand aura and a trigger on its own public plays), and Shockwave "
                + "gives every minion on the boss's board Rush (ULDA_BOSS_73p: a board aura, ULDA_BOSS_73e). No hidden zone of the run "
                + "player is read or written (c-enc-22 sections D, L; the enc22 bosspilot modes)",
            Ids = new[] { "ULDA_BOSS_63p", "ULDA_BOSS_73p" },
        },
        new AdmissionRule
        {
            Family = "boss-tombs-d-both-players", Admit = AdmitScope.Boss, Lift = NoneBossPower,
            Reason = "Isiset's Mistress of Magic (ULDA_BOSS_69p), a passive power on both players (\"whenever a player casts their first "
                + "spell, add a copy to their hand\"): when either player plays the first spell of their game from hand, a copy of that "
                + "public spell joins that player's own hand (a full hand takes none). The boss's fair view keeps the run player's copy "
                + "masked like the rest of that hand (it under-knows; it never learns a hidden card) (c-enc-22 section H; the enc22 "
                + "bosspilot modes)",
            Ids = new[] { "ULDA_BOSS_69p" },
        },
        new AdmissionRule
        {
            Family = "boss-tombs-d-hidden-steal", Admit = AdmitScope.Boss, Lift = NoneBossPower,
            Reason = "Thrud's Ogre-Sized Thief (ULDA_BOSS_65p), \"Steal a card from your opponent's deck and put it into your hand\": a "
                + "random card of the run player's deck (game RNG) moves to the boss's hand by the deck-steal rule of TRLA_186 "
                + "(DungeonRunShrines.TakeFromDeck: a deck exit and a transfer whose definition only the new owner sees; a full hand burns "
                + "it). The boss learns exactly the card it took, now a card of its own hand, and nothing else: the run player's other "
                + "deck cards stay unknown slots in its fair view, and a steal its search simulates takes a masked card. The run player "
                + "sees a card leave its deck. Offered while that deck, whose count is public, holds a card (c-enc-22 section E; the "
                + "enc22 bosspilot modes)",
            Ids = new[] { "ULDA_BOSS_65p" },
        },
        new AdmissionRule
        {
            Family = "boss-tombs-d-hidden-illusion", Admit = AdmitScope.Boss, Lift = NoneBossPower,
            Reason = "A Beautiful Oasis's Mirage (ULDA_BOSS_62p), \"Give your opponent an illusion card. If played, they discard a random "
                + "card\", auto-cast by rule at the start of the boss's turn while the run player's hand holds a card and has room (both "
                + "counts public): a copy of a random card of the run player's own hand joins that hand and one of the two is the "
                + "illusion (game RNG; the wiki's notes; private encounter tags EncounterRules.MirageIllusion, never sent to the client). "
                + "Played, the illusion resolves as its card and its player discards a random card of the hand (a public discard); at "
                + "the end of that player's turn it leaves the hand (both still there: the copy leaves). The copied card is a hidden "
                + "card of the run player's hand and stays masked in the boss's fair view; the boss knows only which hand slot it made "
                + "the illusion (c-enc-22 section C; the enc22 bosspilot modes)",
            Ids = new[] { "ULDA_BOSS_62p" },
        },
        new AdmissionRule
        {
            // Player | Boss: Masterful Djinn summons it for the boss; the run player can take it from the board (a copy, a control
            // change) and it is a plain minion there. Never generated: no generation pool yields it.
            NeverGenerated = true,
            Family = "boss-tombs-d-card", Admit = AdmitScope.Player | AdmitScope.Boss, Lift = NoneBossOnly,
            Reason = "Servant of Siamat (ULDA_026), the 1/4 Elemental Masterful Djinn summons, with no text of its own; its random keyword "
                + "(Taunt, Rush, Windfury or Divine Shield) is the minion's own tag. Public board state on either side (c-enc-22 section "
                + "F; the enc22 bosspilot modes)",
            Ids = new[] { "ULDA_026" },
        },
    };
}
