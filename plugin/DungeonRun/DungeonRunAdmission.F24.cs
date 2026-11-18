// Phase 8, family 24 (the Plague Lords and Tekahn: Xatma, K'zrath, Vesh beside family 01's Icarax, and the Tombs finale): the
// admission rows of the cards it scripts (a method the table concatenates: DungeonRunAdmission.BuildRules). The hero powers are
// a boss's only; the boss deck cards and tokens are listed by boss decks only and are admitted in the run player's hands too,
// where a steal, a copy or a control change can put one; ULDA_115 "Gnomebliterator" is a Tombs run treasure, the run player's
// alone. The tests named in the reasons are the suite c-enc-24 (verify/suites/p8f24/c-enc-24) and the enc24 bosspilot modes.
internal static partial class DungeonRunAdmission
{
    private static AdmissionRule[] RulesF24() => new[]
    {
        new AdmissionRule
        {
            Family = "boss-plague-power", Admit = AdmitScope.Boss, Lift = NoneBossPower,
            Reason = "a clickable Plague Lord or Tekahn hero power whose whole effect is public board and hero state the engine resolves: "
                + "Foreboding Fate triggers a friendly minion's Deathrattle (ULDA_BOSS_37p1, Play Dead's rule), Death's Gaze deals 1 "
                + "damage to every minion and adds Untimely Demise to the boss's own hand when one dies (ULDA_BOSS_37p3), Evoke Panic "
                + "makes the lowest-Health minion attack a neighbor in its own row (ULDA_BOSS_38p1; offered while a row holds two "
                + "neighbors), Systematic Betrayal rotates both rows counter-clockwise as the run player sees them and gives the minions "
                + "that switched sides +1/+1 (ULDA_BOSS_38p2: family 16's EncounterRules.Rotate; a run shrine keeps its place and side), "
                + "Ashes Anew destroys a friendly Murloc and summons a Scaly Golem (ULDA_BOSS_39p2), Awaken the Flame turns a Sanctum "
                + "Golem into an Enflamed Golem in place (ULDA_BOSS_67p1) and Curse of Flame marks an enemy minion with a public "
                + "enchantment that deals 5 damage to every character at the boss's next turn if the minion lives (ULDA_BOSS_67p2). "
                + "Every random pick uses the game RNG and is reported; no hidden zone is read (c-enc-24 X, K, V and T checks; the "
                + "enc24 and plague24 bosspilot modes)",
            Ids = new[] { "ULDA_BOSS_37p1", "ULDA_BOSS_37p3", "ULDA_BOSS_38p1", "ULDA_BOSS_38p2", "ULDA_BOSS_39p2", "ULDA_BOSS_67p1", "ULDA_BOSS_67p2" },
        },
        new AdmissionRule
        {
            Family = "boss-plague-passive", Admit = AdmitScope.Boss, Lift = NoneBossPower,
            Reason = "a passive Plague Lord or Tekahn hero power (started by EncounterRules.ActivatePower at the match start and at a form "
                + "change) whose trigger or aura works on public state: Death's Embrace gives Reborn to the first minion of each player "
                + "that dies in a turn (ULDA_BOSS_37p2), Murmurs of Mortality brings a random enemy minion to 1 Health after a friendly "
                + "minion dies (ULDA_BOSS_38p3), Made of Murlocs keeps the boss's hero Immune and makes it lose 3 Health (not damage) "
                + "after any Murloc dies, its next form entering at once (ULDA_BOSS_39p1: EncounterRules.LoseHealth), and Soul Pyre "
                + "deals each player damage equal to the Cost of every card they play (ULDA_BOSS_67p3). Nothing hidden is read "
                + "(c-enc-24 X, K, V and T checks; the enc24 and plague24 bosspilot modes)",
            Ids = new[] { "ULDA_BOSS_37p2", "ULDA_BOSS_38p3", "ULDA_BOSS_39p1", "ULDA_BOSS_67p3" },
        },
        new AdmissionRule
        {
            Family = "boss-plague-own-deck", Admit = AdmitScope.Boss, Lift = NoneBossPower,
            Reason = "Vesh's Mur-nado (ULDA_BOSS_39p3, \"Shuffle all minions into your deck. After a Murloc dies, reduce this Cost by (1)\"): "
                + "every public board minion of both sides moves into the boss's own deck (MoveToDeck: a public shuffle, so the boss "
                + "knows each card it gained and the run player sees them go; a run shrine goes dormant instead), and the Murlocs that "
                + "died while the power was active are counted on the power (one private tag, EncounterRules.PlagueCount, that clones "
                + "and the fair view carry) and lower its Cost at every aura update (c-enc-24 V checks; the plague24 bosspilot mode)",
            Ids = new[] { "ULDA_BOSS_39p3" },
        },
        new AdmissionRule
        {
            // Player | Boss: no player role offers one (boss decks list them, a boss power makes them), but the run player can take
            // one from a boss (a deck steal, a copy, a control change), and its effect resolves the same there. Never generated.
            NeverGenerated = true,
            Family = "boss-plague-card", Admit = AdmitScope.Player | AdmitScope.Boss, Lift = NoneBossOnly,
            Reason = "a Plague Lord or Tekahn boss card or token whose effect is public board and hero resolution, the same in either "
                + "player's hands: Untimely Demise deals 3 damage to every enemy (ULDA_BOSS_37t), Scaly Golem gives every Murloc of both "
                + "boards +1/+1 when it dies (ULDA_BOSS_39m), Sanctum Golem cannot attack (ULDA_031), Enflamed Golem deals 3 damage to "
                + "all minions and summons a Sanctum Golem when it dies (ULDA_032), Rakanishu makes its owner's Lackeys 4/4 for the rest "
                + "of the game (ULDA_033: Dark Pharaoh Tekahn's player enchantment ULD_168e, a hand-and-board aura) and The Final "
                + "Plague?! summons C'Thun, N'Zoth, Y'Shaarj and Yogg-Saron without their Battlecries (ULDA_BOSS_67t); Vesh's special "
                + "cards: Mrgl Mrgl Nyah Nyah summons 3 (LOEA10_5) or 5 (LOEA10_5H) random Murlocs that died this game (Anyfin Can "
                + "Happen's rule over the graveyards), Demonic Presence draws 3 and gains 10 Armor (KARA_13_12H) and Giantfin draws 2 at "
                + "the end of its owner's turn (LOEA16_24H). No hidden zone of "
                + "the other side is read or written (c-enc-24 X, V and T checks; the enc24 bosspilot modes)",
            Ids = new[] { "ULDA_BOSS_37t", "ULDA_BOSS_39m", "ULDA_031", "ULDA_032", "ULDA_033", "ULDA_BOSS_67t", "LOEA10_5", "LOEA10_5H", "KARA_13_12H",
                "LOEA16_24H" },
        },
        new AdmissionRule
        {
            Family = "run-treasure-plague-lord", Admit = AdmitScope.Player, Lift = RunTreasureLift,
            Reason = "treasure: only the player can hold it; offered on act runs of an encounter schedule only (run-v1.json \"runs\": "
                + "\"encounters\"), where a Plague Lord can be met. Its spell deals 10 damage (Spell Damage applies), and as much again: "
                + "to the Plague Lord at once in a Plague Lord fight, otherwise banked on the caster's controller (EncounterRules.PlagueCount; "
                + "a cast by either player counts), reported by the match harvest (RunMatchEnd.GnomebliteratorDamage) and carried by the "
                + "run into its next Plague Lord fight (DungeonRunState.PlagueLordDamage, spent by that fight). Nothing hidden is read "
                + "(c-enc-24 G checks)",
            Ids = new[] { "ULDA_115" },
        },
    };
}
