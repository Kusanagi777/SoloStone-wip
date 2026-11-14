// Phase 8, family 14 (Dalaran Heist bosses A: DALA_BOSS_01 to 15 and their boss-only cards): the admission rows of the cards it
// scripts (a method the table concatenates: DungeonRunAdmission.BuildRules). The hero powers are a boss's only; the magical
// feats, "Scourgelord" Dazzik and the "Kind" Waitress are listed by boss decks or made by boss powers only, and are admitted in
// the run player's hands too, where a steal or a copy of a boss card can put one. The tests named in the reasons are the suite
// c-enc-14 (verify/suites/p8f14) and the enc14 bosspilot modes.
internal static partial class DungeonRunAdmission
{
    private static AdmissionRule[] RulesF14() => new[]
    {
        new AdmissionRule
        {
            Family = "heist-board-power", Admit = AdmitScope.Boss, Lift = NoneBossPower,
            Reason = "a clickable Dalaran Heist boss hero power whose whole effect is public board, hero and graveyard state the engine "
                + "resolves: Chomp deals 1 damage to a minion with the power's own Lifesteal (DALA_BOSS_01p), \"Armor Up\" gains 2 Armor "
                + "(DALA_BOSS_06p), Apple Toss deals 1 damage to the lowest Health enemy and repeats while its target dies (DALA_BOSS_08p), "
                + "Repeat History summons a new copy of a random friendly minion that died this turn, never a run shrine's corpse "
                + "(DALA_BOSS_09p), Forbidden Love destroys a random minion of each board (DALA_BOSS_12p), Slice and Dice deals 1 to 3 "
                + "damage to three random minions (DALA_BOSS_13p) and Order Up! summons a 2/2 Kind Waitress (DALA_BOSS_14p). A power that "
                + "would do nothing is not offered, by a condition that reads only the public boards and graveyards, so an option of the "
                + "search is always an option of the real game; every random pick uses the game RNG and is reported as a random event; a "
                + "run shrine is never picked once dormant and goes dormant when destroyed (c-enc-14 A1-A3, F1, H1-H5, I1-I6, L1-L5, "
                + "M1-M4, N1; the enc14 and enc14-seeds bosspilot modes)",
            Ids = new[] { "DALA_BOSS_01p", "DALA_BOSS_06p", "DALA_BOSS_08p", "DALA_BOSS_09p", "DALA_BOSS_12p", "DALA_BOSS_13p", "DALA_BOSS_14p" },
        },
        new AdmissionRule
        {
            Family = "heist-own-hand-power", Admit = AdmitScope.Boss, Lift = NoneBossPower,
            Reason = "The Great Akazamzarak's Prestidigitation, \"Add a random magical feat to your hand\": one of four known boss cards "
                + "(the feats DALA_BOSS_03t to 03t4) joins the boss's own hand, which it knows exactly and the run player sees as a count; "
                + "offered only while that hand has room (c-enc-14 C1-C3; the enc14 bosspilot mode)",
            Ids = new[] { "DALA_BOSS_03p" },
        },
        new AdmissionRule
        {
            Family = "heist-hidden-deck-power", Admit = AdmitScope.Boss, Lift = NoneBossPower,
            Reason = "Kaye Toogie's Open Wormhole, \"Both players summon a minion from their deck. They fight.\": Duel!'s rule (DAL_731, "
                + "row hidden-deck-summon): a random minion of each deck becomes a public board entity as it leaves its deck and the "
                + "boss's attacks the other. In the boss's fair-view projection the run player's deck holds no known minion, so there "
                + "only the boss's own minion arrives: the search never reads the hidden deck, and the real summon is a random event it "
                + "re-plans after. Offered always: whether a minion is left is the search's estimate, never a rule (c-enc-14 D1-D4; the "
                + "enc14 bosspilot mode)",
            Ids = new[] { "DALA_BOSS_04p" },
        },
        new AdmissionRule
        {
            Family = "heist-recast-power", Admit = AdmitScope.Boss, Lift = NoneBossPower,
            Reason = "The Amazing \"Bonepaw\"'s Mana Echoes, \"Cast a random spell that was cast this game (targets chosen randomly)\": a "
                + "new copy of a spell either player played from hand this game (the public play history), cast by the engine's effect "
                + "play rule (AutoPlayRules: random targets and choices). Only a scripted spell the run's generation filter allows for the "
                + "boss, never a Secret or a Quest; offered only after such a spell was played, a condition on the public play history "
                + "that the boss's fair view holds exactly (c-enc-14 E1-E5; the enc14 bosspilot mode)",
            Ids = new[] { "DALA_BOSS_05p" },
        },
        new AdmissionRule
        {
            Family = "heist-passive", Admit = AdmitScope.Boss, Lift = NoneBossPower,
            Reason = "a passive Dalaran Heist boss hero power, started at match start and at a form change (EncounterRules.ActivatePower), "
                + "never clickable, whose trigger or aura answers public events: The Feast restores 3 Health to the boss's hero after it "
                + "casts a spell (DALA_BOSS_02p), Street Smarts draws the boss a card when it plays a Coin (DALA_BOSS_10p: The Coin or "
                + "Counterfeit Coin), Backstabber gives every Stealthed minion summoned on either side +1 Attack (DALA_BOSS_11p), "
                + "Blessing of Elune doubles all healing of both players (DALA_BOSS_15p: the ALL_HEALING_DOUBLE controller aura; healing "
                + "turned into damage by Auchenai Soulpriest or Bwonsamdi's Covenant deals the doubled amount), and Flight Master "
                + "Belnaara's pair: Take Flight! counts the damage her hero takes down from 15 on the power and then swaps itself for "
                + "Flying! and makes her hero Immune; Flying! counts 2 of her turns and then lands her: Immune ends and Take Flight! "
                + "returns (DALA_BOSS_07p / 07p2, a power-only script form: EncounterRules.SwapPowerForm, the model's FormIndex and "
                + "FormChanges tags; both counters are public tags of the power the client shows). No hidden zone is read "
                + "(c-enc-14 B1-B4, G1-G11, J1-J4, K1-K4, O1-O6; the enc14, enc14-seeds and enc14-flight bosspilot modes)",
            Ids = new[] { "DALA_BOSS_02p", "DALA_BOSS_07p", "DALA_BOSS_07p2", "DALA_BOSS_10p", "DALA_BOSS_11p", "DALA_BOSS_15p" },
        },
        new AdmissionRule
        {
            // Player | Boss: no player role offers one (boss decks list "Scourgelord" Dazzik, Prestidigitation makes the feats and
            // Order Up! the Waitress), but the run player can take one from a boss (a steal, a copy, a control change of the
            // Waitress), and its effect resolves the same there. Never generated.
            NeverGenerated = true,
            Family = "heist-boss-card", Admit = AdmitScope.Player | AdmitScope.Boss, Lift = NoneBossOnly,
            Reason = "a Dalaran Heist boss card whose effect the engine resolves the same in either player's hands: Reductomara puts "
                + "Reductomara'd (Costs (1) less) on every spell of both hands and decks, never bringing an Echo or Repeatable spell below "
                + "(1), and records each owner's deck as rewritten for its own-deck ledger (DALA_BOSS_03t; the definitions are unchanged, "
                + "the boss's sampled deck shows printed Costs, so its search only ever overestimates them), Bunnifitronus transforms a "
                + "random minion of either board into a random critter (DALA_BOSS_03t2; a run shrine goes dormant instead), Presto "
                + "Legendaro gives each player a random scripted collectible Legendary minion through that side's generation filter "
                + "(DALA_BOSS_03t3), Yoggers Poggers shuffles three Scrolls of Wonder into each deck and records each owner's deck "
                + "(DALA_BOSS_03t4), \"Scourgelord\" Dazzik is a hero card that becomes its player's hero with Bladestorm and equips "
                + "Shadowmourne (DALA_BOSS_06dk, Scourgelord Garrosh's rule) and the \"Kind\" Waitress gives an enemy attacking it a 50% "
                + "chance to attack another character of its side (DALA_BOSS_14t, EncounterRules.WrongTarget50). Each is a public "
                + "board, hand-count or deck-count change; no hidden zone of the other side is read (c-enc-14 C4-C14, F2-F4, N2-N4; the "
                + "enc14, enc14-seeds and enc14-cards bosspilot modes)",
            Ids = new[] { "DALA_BOSS_03t", "DALA_BOSS_03t2", "DALA_BOSS_03t3", "DALA_BOSS_03t4", "DALA_BOSS_06dk", "DALA_BOSS_14t" },
        },
    };
}
