// c-admission (phase 2 design D3): the admission table in C:\SoloStone\plugin\DungeonRun\DungeonRunAdmission.cs, the
// validator's use of it, and the Generated-scope list the run plan hands the engine.
// usage: AiHarness [ledger-before.csv]   (the pre-phase-2 ledger; its blocked/fixed-boss-deck rows are the ids under test)
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.CardSets.DungeonRun;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;

int failures = 0, passes = 0;
void Check(string name, bool ok, string detail = "")
{
    if (ok) passes++; else failures++;
    if (!ok || Environment.GetEnvironmentVariable("VERBOSE") == "1")
        Console.WriteLine((ok ? "PASS " : "FAIL ") + name + (detail.Length > 0 ? "  (" + detail + ")" : ""));
}

static object FromJson(System.Text.Json.JsonElement e)
{
    switch (e.ValueKind)
    {
        case System.Text.Json.JsonValueKind.Object: { var d = new Dictionary<string, object>(); foreach (var p in e.EnumerateObject()) d[p.Name] = FromJson(p.Value); return d; }
        case System.Text.Json.JsonValueKind.Array: { var l = new List<object>(); foreach (var x in e.EnumerateArray()) l.Add(FromJson(x)); return l; }
        case System.Text.Json.JsonValueKind.String: return e.GetString();
        case System.Text.Json.JsonValueKind.Number: return e.TryGetInt64(out long v) ? (object)v : e.GetDouble();
        case System.Text.Json.JsonValueKind.True: return true;
        case System.Text.Json.JsonValueKind.False: return false;
        default: return null;
    }
}
NpcWorldJson.Parser = text => { using var doc = System.Text.Json.JsonDocument.Parse(text); return FromJson(doc.RootElement); };

string root = VerifyPaths.Data;
string ledger = args.Length > 0 ? args[0] : VerifyPaths.Fixture("ledger-before.csv");
var data = DungeonRunData.Parse(File.ReadAllText(Path.Combine(root, "dungeon-run", "run-v1.json")),
    File.ReadAllText(Path.Combine(root, "dungeon-run", "pool-v1.json")), File.ReadAllText(Path.Combine(root, "npc", "decks-v1.json")));
Card Engine(string id) { try { return Cards.FromId(id); } catch { return null; } }
void Note(string text) => Console.WriteLine("NOTE " + text);
Game NewRun(string playerHero, string bossHero, List<string> pdeck, List<string> bdeck, int seed, IReadOnlyCollection<string> excluded, IReadOnlyCollection<string> excludedBoss)
{
    var plan = new RunMatchPlan { PlayerHero = Cards.FromId(playerHero), PlayerDeck = (pdeck ?? Enumerable.Repeat("CS2_182", 10).ToList()).Select(Cards.FromId).ToList(),
        BossHero = Cards.FromId(bossHero), BossDeck = (bdeck ?? Enumerable.Repeat("CS2_182", 30).ToList()).Select(Cards.FromId).ToList(), BossHealth = 30,
        ExcludedGenerated = excluded, ExcludedGeneratedBoss = excludedBoss };
    var cfg = new GameConfig { RandomSeed = seed, Shuffle = true, SkipMulligan = false, History = true, Logging = false, FormatType = FormatType.FT_WILD };
    DungeonRunSetup.Configure(cfg, plan);
    var g = new Game(cfg); g.StartGame(); DungeonRunSetup.ApplyAfterStart(g, plan);
    g.Process(ChooseTask.Mulligan(g.Player1, new List<int>())); g.Process(ChooseTask.Mulligan(g.Player2, new List<int>())); g.MainBegin();
    return g;
}

// ---------------------------------------------------------------- 1. every ledger row the old rules blocked has one rule
// The pre-phase-2 ledger's ai_support: blocked:secret (84), blocked:deck-dependent (54), blocked:turn-rewind (2) and
// ok:fixed boss deck (9). Read the CSV minimally: card_id is the first field, ai_support the 15th.
var header = File.ReadLines(ledger).First().Split(',');
int aiCol = Array.IndexOf(header, "ai_support");
var byAi = new Dictionary<string, List<string>>();
foreach (var fields in ReadCsv(ledger).Skip(1))
{
    string ai = fields[aiCol];
    if (!byAi.TryGetValue(ai, out var list)) byAi[ai] = list = new List<string>();
    list.Add(fields[0]);
}
List<string> Rows(string key) => byAi.Where(p => p.Key.StartsWith(key, StringComparison.Ordinal)).SelectMany(p => p.Value).ToList();
var secretRows = Rows("blocked:secret"); var deckRows = Rows("blocked:deck-dependent"); var rewindRows = Rows("blocked:turn-rewind"); var fixedRows = Rows("ok:fixed boss deck");
Check("the old ledger has 84 + 54 + 2 + 9 rows under test", secretRows.Count == 84 && deckRows.Count == 54 && rewindRows.Count == 2 && fixedRows.Count == 9,
    $"{secretRows.Count}/{deckRows.Count}/{rewindRows.Count}/{fixedRows.Count}");
var ledgerIds = secretRows.Concat(deckRows).Concat(rewindRows).Concat(fixedRows).ToList();
foreach (string id in ledgerIds)
{
    int rules = DungeonRunAdmission.Rules.Count(r => r.Ids != null && r.Ids.Contains(id));
    Check($"{id}: exactly one admission rule", rules == 1, rules + " rules");
}
var tableIds = DungeonRunAdmission.Rules.Where(r => r.Ids != null).SelectMany(r => r.Ids).ToList();
Check("the table lists no id twice", tableIds.Count == tableIds.Distinct().Count(), string.Join(",", tableIds.GroupBy(x => x).Where(g => g.Count() > 1).Select(g => g.Key)));
// Phase 6 F2 adds a row for each of its 30 active treasures (player only, family run-treasure*): they were never AI-blocked
// ledger rows, and each names its regression test (c-treasures-F2).
// Phase 6 F3 adds the Monster Hunt Tess and Darius rows: 14 run-treasure ids (treasures and tokens), the two powers
// (run-hero-power) and the Cannon (run-hero-board), all player only (c-treasures-F3).
var runTreasureIds = DungeonRunAdmission.Rules.Where(r => r.Ids != null && r.Family.StartsWith("run-treasure", StringComparison.Ordinal)).SelectMany(r => r.Ids).ToList();
var runHeroIds = DungeonRunAdmission.Rules.Where(r => r.Ids != null && r.Family.StartsWith("run-hero-", StringComparison.Ordinal)).SelectMany(r => r.Ids).ToList();
// Phase 6 F4 adds Shaw and Toki: 11 run-treasure* ids (9 run-treasure, GILA_901 hand-cast, GILA_906 hand-swap) and Dog Whistle (run-hero-power).
// Phase 6 F5 adds the Monster Hunt shared treasures: 34 run-treasure* ids (12 run-treasure, 18 run-treasure-evolving stages and
// tokens, GILA_802 discover, GILA_811/812/827 hidden-copy); GILA_806 moves to a player-only from-own-deck row.
// Phase 6 F7 adds the Heist active treasures: 30 run-treasure* ids (DALA_727 is the deck-wide-change row); Fly-By's Kadoom Bot
// reaches the boss's deck, so it is a run-deck-token row (both sides), outside the run-treasure* families.
// Phase 6 F8 adds the Heist passive treasures: 15 run-treasure* ids; DALA_740 is a treasure-data-gap row (rejected, outside
// the run-treasure* families).
// Phase 6 F9 adds ULDA_039 Uldum Treasure Cache (run-treasure-discover, player only).
// Phase 6 F11 adds 26 Tombs signature ids and Pack Mule (run-treasure, run-treasure-hand-steal ULDA_201, run-treasure-discover
// ULDA_002); ULDA_016, ULDA_021 (secret-from-deck) and ULDA_024 (deck-wide-change) keep their rows.
// Phase 6 F12 adds the Tombs active treasures: 28 run-treasure* ids (21 run-treasure with the Twinspell copies and tokens, ULDA_110
// hand-steal, ULDA_035/035ts hand-escrow, ULDA_301/306/504 run-state, ULDA_118 burn); ULDA_044 (deck-wide-change) and ULDA_503
// (from-own-deck) keep their rows; ULDA_115 was the one finale-state row (rejected, phase 8) until phase 8 family 24 admitted it
// (run-treasure-plague-lord, player only).
// Phase 6 F13 adds the Tombs passive treasures: 15 run-treasure* ids (8 run-treasure, ULDA_042/103 paid-cost, ULDA_009/117
// match-start, ULDA_102 forced-pass, ULDA_205 discover, ULDA_011 run-flag).
Check("phase 6 F2 + F3 + F4 + F5 + F7 + F8 + F9 + F11 + F12 + F13 (+ phase 8 family 24: ULDA_115): 30 LOOTA_8, 59 GILA, 45 DALA_7 and 71 ULDA run-treasure rows, every one admitted for the player only",
    runTreasureIds.Count(id => id.StartsWith("LOOTA_8", StringComparison.Ordinal)) == 30 && runTreasureIds.Count(id => id.StartsWith("GILA_", StringComparison.Ordinal)) == 59
    && runTreasureIds.Count(id => id.StartsWith("DALA_7", StringComparison.Ordinal)) == 45 && runTreasureIds.Count(id => id.StartsWith("ULDA_", StringComparison.Ordinal)) == 71
    && runTreasureIds.Contains("ULDA_039") && runTreasureIds.Contains("ULDA_201") && runTreasureIds.Contains("ULDA_002") && runTreasureIds.Count == 205
    && DungeonRunAdmission.RuleFor("ULDA_115")?.Family == "run-treasure-plague-lord" && DungeonRunAdmission.RuleFor("ULDA_115").Admit == AdmitScope.Player
    && DungeonRunAdmission.RuleFor("DALA_740")?.Family == "treasure-data-gap" && DungeonRunAdmission.RuleFor("DALA_740").Admit == AdmitScope.None
    && DungeonRunAdmission.RuleFor("DALA_716t")?.Family == "run-deck-token" && DungeonRunAdmission.RuleFor("DALA_716t").Admit == AdmitScope.Both
    && DungeonRunAdmission.Rules.Where(r => r.Family.StartsWith("run-treasure", StringComparison.Ordinal)).All(r => r.Admit == AdmitScope.Player && DungeonRunAdmission.IsNoneLift(r.Lift)));
// Phase 6 F6 adds the 18 Heist powers (run-hero-power), the Shifting Chameleon and Devious Imp (run-hero-power-token, player
// only) and Undermine's Improvised Explosive (run-hero-power-deck-token: shuffled into the boss's deck, so both sides).
// Phase 6 F9 adds 11 of the 12 Tombs explorer powers (run-hero-power, Arcane Craftiness run-hero-power-loop; Dino Tracking is a
// player-only from-own-deck row) and their three cards (the Amalgam Explorer, Elise's Might's two choices: run-hero-power-token).
Check("phase 6 F3 + F4 + F6 + F9: the run-hero-* rows (GILA_400p, GILA_500p2, GILA_600p, GILA_601, the 18 DALA_<Class>_HP1/HP2, 11 ULDA explorer powers and five tokens) admit the player only; the Explosive both sides",
    runHeroIds.OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(new[] { "GILA_400p", "GILA_500p2", "GILA_600p", "GILA_601", "DALA_Hunter_HP2t", "DALA_Warlock_HP2t", "DALA_Warrior_HP2t",
            "ULDA_Finley_HP1t", "ULDA_Elise_HP1a", "ULDA_Elise_HP1b" }
        .Concat(DungeonRunContent.HeistPowers).Concat(DungeonRunContent.TombsExplorerPowers.Where(p => p != "ULDA_Brann_HP3")).OrderBy(x => x, StringComparer.Ordinal))
    && DungeonRunAdmission.Rules.Where(r => r.Family.StartsWith("run-hero-", StringComparison.Ordinal) && r.Family != "run-hero-power-deck-token").All(r => r.Admit == AdmitScope.Player && DungeonRunAdmission.IsNoneLift(r.Lift))
    && DungeonRunAdmission.RuleFor("DALA_Warrior_HP2t").Admit == AdmitScope.Both && DungeonRunAdmission.IsNoneLift(DungeonRunAdmission.RuleFor("DALA_Warrior_HP2t").Lift));
// GILA_903 Enhance-a-matic (phase 6 F4) is a secret-generated row: a spell of each class may be a Secret.
// Phase 6 F7: Fly-By's Kadoom Bot (DALA_716t) is the run-deck-token row.
var runDeckTokenIds = DungeonRunAdmission.Rules.Where(r => r.Ids != null && r.Family == "run-deck-token").SelectMany(r => r.Ids).ToList();
// Phase 6 F8: DALA_740 Wondrous Wisdomball is the one treasure-data-gap row (a family's stated rejection).
var dataGapIds = DungeonRunAdmission.Rules.Where(r => r.Ids != null && r.Family == "treasure-data-gap").SelectMany(r => r.Ids).ToList();
// Phase 6 F9: ULDA_Brann_HP3 Dino Tracking is a player-only from-own-deck row (a named hero's passive power).
// Phase 7: the board-shrine, board-shrine-form, shrine-token (S2: TRLA_129s), hidden-steal (S3: TRLA_186), shrine-upgrade and
// teammate rows (Rumble Run cards the phase-2 row lists predate).
var phase7Ids = new HashSet<string>(DungeonRunAdmission.Rules.Where(r => r.Ids != null && (r.Family == "board-shrine" || r.Family == "board-shrine-form"
    || r.Family == "shrine-token" || r.Family == "hidden-steal" || r.Family == "shrine-upgrade" && r.Ids[0] != "TRLA_809" || r.Family == "teammate"
    // Phase 8 family 04: the Uldum collectibles ULD_282..ULD_728 each have a row (DungeonRunAdmission.F04.cs).
    || r.Family == "quest-hero-power" || r.Family == "own-deck-insert" || r.Family == "own-deck-pull" || r.Family == "own-hand-rewrite" || r.Family == "scripted-generation" || r.Family == "public-board"
    // Phase 8 family 01: the boss-shrine row (the three Warrior shrines moved out of board-shrine), the global rules and the fight-only hero cards.
    || r.Family == "boss-shrine" || r.Family.StartsWith("global-rule", StringComparison.Ordinal) || r.Family == "fight-hero-card")).SelectMany(r => r.Ids), StringComparer.Ordinal);
// Phase 8 family 02: the scripted collectibles' rows (admitted in every scope; DungeonRunAdmission.F02.cs).
phase7Ids.UnionWith(DungeonRunAdmission.Rules.Where(r => r.Ids != null && r.Lift == DungeonRunAdmission.NoneCollectible).SelectMany(r => r.Ids));
// Phase 8 family 03: the rows of the Uldum collectibles ULD_131..ULD_274 (cards no phase-2 detector flags; each row names its test).
phase7Ids.UnionWith(DungeonRunAdmission.Rules.Where(r => r.Ids != null && new[] { "keyword-only", "public-effect", "own-hand", "own-graveyard", "generated-pool",
    "random-cast", "hidden-reveal", "quest-hero-power" }.Contains(r.Family)).SelectMany(r => r.Ids));
// Phase 8 family 07: the Dungeon Run boss powers, the Rat and Growing Ooze (DungeonRunAdmission.F07.cs; boss rows no phase-2 detector flags).
phase7Ids.UnionWith(DungeonRunAdmission.Rules.Where(r => r.Ids != null && new[] { "boss-power-public", "boss-power-own-hand", "boss-token" }.Contains(r.Family)).SelectMany(r => r.Ids));
// Phase 8 family 08: the rows of the Dungeon Run boss powers and boss deck cards it scripts (DungeonRunAdmission.F08.cs; each row names its test).
phase7Ids.UnionWith(DungeonRunAdmission.Rules.Where(r => r.Ids != null && new[] { "boss-public-power", "boss-form", "boss-both-players", "boss-passive-trigger",
    "boss-hidden-reveal", "boss-both-draw", "boss-control", "boss-card" }.Contains(r.Family)).SelectMany(r => r.Ids));
// Phase 8 family 09: the rows of the Dungeon Run final bosses' and special rooms' powers and cards (DungeonRunAdmission.F09.cs; each row names its test),
// and Eater of Secrets for Mushhuckster Max's deck (secret-destroy, a boss deck only).
phase7Ids.UnionWith(DungeonRunAdmission.Rules.Where(r => r.Ids != null && new[] { "boss-room", "boss-auto-cast", "boss-craft", "boss-extra-turn", "boss-treasure-find",
    "boss-player-deck-insert", "boss-run-edit", "secret-destroy" }.Contains(r.Family)).SelectMany(r => r.Ids));
// Phase 8 family 10: the rows of the Monster Hunt boss powers and boss cards it scripts (DungeonRunAdmission.F10.cs; each row names its test).
phase7Ids.UnionWith(DungeonRunAdmission.Rules.Where(r => r.Ids != null && new[] { "boss-board-power", "boss-own-hand-power", "boss-control-all", "boss-setaside-store",
    "boss-hunt-card" }.Contains(r.Family)).SelectMany(r => r.Ids));
// Phase 8 family 24: the rows of the Plague Lord and Tekahn powers and boss cards it scripts (DungeonRunAdmission.F24.cs; each row names its test).
phase7Ids.UnionWith(DungeonRunAdmission.Rules.Where(r => r.Ids != null && new[] { "boss-plague-power", "boss-plague-passive", "boss-plague-own-deck",
    "boss-plague-card" }.Contains(r.Family)).SelectMany(r => r.Ids));
Check("the table lists only ledger rows (and the phase 6 F2/F3/F4/F7/F8/F9 run-treasure, run-hero, run-deck-token and treasure-data-gap rows, GILA_903 and ULDA_Brann_HP3)",
    runDeckTokenIds.SequenceEqual(new[] { "DALA_716t" }) && dataGapIds.SequenceEqual(new[] { "DALA_740" })
    && tableIds.All(id => ledgerIds.Contains(id) || runTreasureIds.Contains(id) || runHeroIds.Contains(id) || runDeckTokenIds.Contains(id) || dataGapIds.Contains(id) || id == "GILA_903" || id == "ULDA_Brann_HP3" || id == "ULDA_115" || phase7Ids.Contains(id)),
    string.Join(",", tableIds.Where(id => !phase7Ids.Contains(id) && !ledgerIds.Contains(id) && !runTreasureIds.Contains(id) && !runHeroIds.Contains(id) && !runDeckTokenIds.Contains(id) && !dataGapIds.Contains(id) && id != "GILA_903" && id != "ULDA_Brann_HP3" && id != "ULDA_115")));
Check("every table id is a card in the engine", tableIds.All(id => Engine(id) != null), string.Join(",", tableIds.Where(id => Engine(id) == null)));

// The design's row assignment (B2): families and counts.
int Count(string family, AdmitScope? admit = null) => DungeonRunAdmission.Rules.Where(r => r.Family == family && r.Ids != null && (admit == null || r.Admit == admit)).Sum(r => r.Ids.Length);
Check("Secret rows: 42 secret-card, 29 secret-from-deck, 1 secret-named, 11 secret-generated (GILA_903 from phase 6 F4; TRLA_110 a board shrine from phase 7 S1), 1 text-only",
    Count("secret-card") == 42 && Count("secret-from-deck") == 29 && Count("secret-named") == 1 && Count("secret-generated") == 11 && Count("text-only") == 1
    && secretRows.All(id => new[] { "secret-card", "secret-from-deck", "secret-named", "secret-generated", "text-only" }.Contains(DungeonRunAdmission.RuleFor(id)?.Family)
        || id == "TRLA_110" && DungeonRunAdmission.RuleFor(id)?.Family == "board-shrine"));
Check("deck rows: 6 highlander, 5 not-started-in-deck, 17 from-own-deck, 9 deck-wide-change, 15 start-of-game-draw, 2 empty-deck",
    deckRows.Count(id => DungeonRunAdmission.RuleFor(id).Family == "highlander") == 6 && deckRows.Count(id => DungeonRunAdmission.RuleFor(id).Family == "not-started-in-deck") == 5
    && deckRows.Count(id => DungeonRunAdmission.RuleFor(id).Family == "from-own-deck") == 17 && deckRows.Count(id => DungeonRunAdmission.RuleFor(id).Family == "deck-wide-change") == 9
    && deckRows.Count(id => DungeonRunAdmission.RuleFor(id).Family == "start-of-game-draw") == 15 && deckRows.Count(id => DungeonRunAdmission.RuleFor(id).Family == "empty-deck") == 2);
Check("turn-rewind: GILA_900p and GILA_911, rejected everywhere", rewindRows.All(id => DungeonRunAdmission.RuleFor(id).Family == "turn-rewind" && DungeonRunAdmission.RuleFor(id).Admit == AdmitScope.None));
var deckRejected = deckRows.Concat(rewindRows).Where(id => DungeonRunAdmission.RuleFor(id).Admit == AdmitScope.None).OrderBy(x => x, StringComparer.Ordinal).ToList();
var pendingRows = deckRows.Concat(rewindRows).Where(id => DungeonRunAdmission.RuleFor(id).Requires != null).OrderBy(x => x, StringComparer.Ordinal).ToList();
// Phase 6 F15 lifted ULD_003, GILA_511 and the five treasure start-of-game draws; phase 7 T2 lifted TRLA_191 (scripted with
// SelfCondition.IsNotStartInDeck); phase 7 D lifted TRLA_809 (deck-wide-change) and the nine teammate start-of-game draws: no
// pending row remains, and the rejected ones are the three phase 9 rows.
Check("of the 56 deck and rewind rows, the rejected ones are ULDA_111, GILA_900p and GILA_911 (phase 9); none has a pending requirement (phase 7 D lifted TRLA_809 and the nine companions)",
    deckRejected.SequenceEqual(new[] { "GILA_900p", "GILA_911", "ULDA_111" }) && pendingRows.Count == 0
    && new[] { "TRLA_809", "TRLA_121", "TRLA_135", "TRLA_139", "TRLA_148", "TRLA_156", "TRLA_164", "TRLA_172", "TRLA_180", "TRLA_188" }.All(id => DungeonRunAdmission.RuleFor(id).Admit == AdmitScope.Player
        && deckRows.Contains(id)), string.Join(",", deckRejected) + " / pending " + pendingRows.Count);
Check("Gral and the deck-wide-change player cards: player yes, boss no (phase 5 lift)",
    new[] { "TRL_409", "BOT_257", "GIL_694", "GIL_840", "TRL_500", "UNG_840" }.All(id => DungeonRunAdmission.RuleFor(id).Admit == AdmitScope.Player && DungeonRunAdmission.RuleFor(id).Lift == DungeonRunAdmission.BossDeckLedgerLift));
Check("the fixed boss deck rows are admitted for bosses", fixedRows.All(id => (DungeonRunAdmission.RuleFor(id).Admit & AdmitScope.Boss) != 0));

// ---------------------------------------------------------------- 2. lifts
foreach (var rule in DungeonRunAdmission.Rules)
{
    string name = rule.Family + (rule.Ids == null ? " (default)" : " " + rule.Ids[0]);
    Check($"{name}: has a reason", !string.IsNullOrWhiteSpace(rule.Reason));
    if (rule.Admit != AdmitScope.Any) Check($"{name}: a rule that rejects a scope names its lift", !string.IsNullOrWhiteSpace(rule.Lift));
}
// Phase 3 I2: the generated-Secret lift is in for catalog runs. The ten player-side ids keep the phase-3 lift for legacy runs
// and admit Player and Generated in a catalog run (the boss side waits on phase 8); the boss power DALA_BOSS_21p is phase 8's.
{
    var sg = DungeonRunAdmission.Rules.Where(r => r.Family == "secret-generated").ToList();
    var playerSide = sg.Single(r => r.Lift == DungeonRunAdmission.GeneratedSecretsLift && r.BossNeeds == null);
    var bossSide = sg.Single(r => r.Lift == DungeonRunAdmission.BossGeneratedSecretsLift);
    // Phase 8 family 01: the two cards that name "a random Hunter Secret" have their own row, admitted for a boss too.
    var named = sg.Single(r => r.Lift == DungeonRunAdmission.GeneratedSecretsLift && r.BossNeeds != null);
    Check("secret-generated: 10 player-side ids lifted for catalog runs (Player + Generated; GILA_903 from phase 6 F4; TRLA_110 moved to board-shrine in phase 7 S1); phase 8 lifts DALA_BOSS_21p for a boss on a catalog run and moves ICC_204 and TRLA_168 to their own row (Player, Boss with the generated-Secrets need, Generated)",
        sg.Count == 3 && playerSide.Ids.Length == 8 && named.Ids.SequenceEqual(new[] { "ICC_204", "TRLA_168" }) && named.Admit == AdmitScope.None
        && named.CatalogAdmit == (AdmitScope.Player | AdmitScope.Boss | AdmitScope.Generated) && named.BossNeeds == DungeonRunAdmission.BossNeedsGeneratedSecrets && named.BossSecretClass == CardClass.HUNTER && playerSide.Admit == AdmitScope.None && playerSide.CatalogAdmit == (AdmitScope.Player | AdmitScope.Generated)
        && playerSide.CatalogLift == DungeonRunAdmission.BossGeneratedSecretsLift && bossSide.Ids.SequenceEqual(new[] { "DALA_BOSS_21p" }) && bossSide.Admit == AdmitScope.None
        && bossSide.CatalogAdmit == AdmitScope.Boss && bossSide.BossNeeds == DungeonRunAdmission.BossNeedsGeneratedSecrets);
    // Phase 8 family 09: the run treasures King Togwaggle's Magic Candle finds (and A. F. Kay's deck lists) are admitted for a boss on catalog runs:
    // LOOTA_839's own row and the run-treasure-boss-find row (the legacy verdict of both stays the run player's only).
    var bossTreasures = DungeonRunAdmission.Rules.Where(r => r.Family == "run-treasure-side-shuffle" || r.Family == "run-treasure-boss-find").ToList();
    Check("only the three secret-generated rules carry a catalog lift (the player side; since phase 8 the boss power DALA_BOSS_21p and the two named-Hunter-Secret cards), and since family 09 the two rows of the treasures a boss finds (Player to Player + Boss)",
        new HashSet<AdmissionRule>(DungeonRunAdmission.Rules.Where(r => r.CatalogAdmit != null)).SetEquals(new[] { playerSide, bossSide, named }.Concat(bossTreasures)) && DungeonRunAdmission.Rules.Count(r => r.CatalogAdmit != null) == 5 && bossTreasures.Count == 2
        && bossTreasures.All(r => r.Admit == AdmitScope.Player && r.CatalogAdmit == (AdmitScope.Player | AdmitScope.Boss) && r.CatalogLift == DungeonRunAdmission.RunTreasureBossLift)
        && bossTreasures.SelectMany(r => r.Ids).Count() == 15);
    foreach (string id in new[] { "BOT_402", "DAL_141", "GIL_116", "ICC_204", "UNG_011", "LOOTA_833", "GILA_509", "GILA_903", "TRLA_168", "ULD_429" })
    {
        var legacyV = DungeonRunAdmission.Classify(Engine(id), id, AdmitScope.Player);
        var catalogV = DungeonRunAdmission.Classify(Engine(id), id, AdmitScope.Player | AdmitScope.Generated, null, catalogRun: true);
        var catalogBoss = DungeonRunAdmission.Classify(Engine(id), id, AdmitScope.Boss, null, catalogRun: true);
        // Phase 8 family 01: ICC_204 and TRLA_168 are admitted for a boss too (a row with bossGeneratedSecrets); the others never.
        bool bossToo = id == "ICC_204" || id == "TRLA_168";
        Check($"{id}: rejected for a legacy run; Player and Generated for a catalog run, " + (bossToo ? "and a boss whose row turns its generated Secrets on" : "never the boss"), !legacyV.Admitted && !legacyV.CatalogLifted
            && catalogV.Admitted && catalogV.CatalogLifted && catalogBoss.Admitted == bossToo
            && (bossToo ? catalogBoss.Rule.BossNeeds == DungeonRunAdmission.BossNeedsGeneratedSecrets : catalogV.Lift == DungeonRunAdmission.BossGeneratedSecretsLift));
    }
    // Phase 7 S1: TRLA_110 Halazzi's Trap is a board shrine (scripted), admitted for the run player and never generated.
    var trap = DungeonRunAdmission.Classify(Engine("TRLA_110"), "TRLA_110", AdmitScope.Player, null, catalogRun: true);
    Check("TRLA_110 is a board shrine (phase 7 S1): Player, never the boss, never generated",
        trap.Admitted && trap.Family == "board-shrine" && trap.Rule.NeverGenerated && !DungeonRunAdmission.Classify(Engine("TRLA_110"), "TRLA_110", AdmitScope.Boss, null, catalogRun: true).Admitted
        && DungeonRunAdmission.GeneratedRejects(true).Contains("TRLA_110"));
    var boss21 = DungeonRunAdmission.Classify(Engine("DALA_BOSS_21p"), "DALA_BOSS_21p", AdmitScope.Any, null, catalogRun: true);
    Check("DALA_BOSS_21p (phase 8): a boss power on a catalog run only (never the run player's, never generated); rejected in every scope on a legacy run",
        boss21.AdmittedSome && boss21.Admit == AdmitScope.Boss && boss21.Rule.BossNeeds == DungeonRunAdmission.BossNeedsGeneratedSecrets
        && !DungeonRunAdmission.Classify(Engine("DALA_BOSS_21p"), "DALA_BOSS_21p", AdmitScope.Any, null).AdmittedSome
        && DungeonRunAdmission.Classify(Engine("DALA_BOSS_21p"), "DALA_BOSS_21p", AdmitScope.Any, null).Lift == DungeonRunAdmission.BossGeneratedSecretsLift);
    // Phase 6 F10 scripts Hunter's Pack: the catalog lift now makes it playable for a catalog run's player (Brann's Deck3).
    Check("ULD_429 is scripted (phase 6 F10), so the lift makes it playable in a catalog run; a legacy run still rejects it",
        Engine("ULD_429").Implemented && DungeonRunValidator.PoolCardReason("ULD_429", new[] { "HUNTER" }, false, null, catalogRun: true) == null
        && DungeonRunValidator.PoolCardReason("ULD_429", new[] { "HUNTER" }, false, null, catalogRun: false) == "admission:secret-generated");
    var zephrys = Engine("ULD_003");
    var zv = DungeonRunAdmission.Classify(zephrys, "ULD_003", AdmitScope.Any, null, catalogRun: true);
    // Phase 6 F15 scripted the wish (c-deferrals): Zephrys is admitted for the run player, never the boss or the boss's generation.
    var zp = DungeonRunAdmission.Classify(zephrys, "ULD_003", AdmitScope.Player, null, catalogRun: true);
    var zb = DungeonRunAdmission.Classify(zephrys, "ULD_003", AdmitScope.Boss, null, catalogRun: true);
    Check("Zephrys (ULD_003) is scripted and its highlander (wish) row admits the run player only, no pending requirement (phase 6 F15)",
        zephrys.Implemented && zv.Family == "highlander" && zp.Admitted && !zb.Admitted && zv.Requires == null && zv.Lift == DungeonRunAdmission.WishBossLift
        && DungeonRunAdmission.IsNoneLift(DungeonRunAdmission.WishBossLift));
}
Check("turn-rewind is lifted by the turn restart, ULDA_111 by the match exit (both deferred to phase 9 by phase 6)",
    DungeonRunAdmission.RuleFor("GILA_900p").Lift == DungeonRunAdmission.TurnRestartLift && DungeonRunAdmission.RuleFor("GILA_911").Lift == DungeonRunAdmission.TurnRestartLift
    && DungeonRunAdmission.RuleFor("ULDA_111").Lift == DungeonRunAdmission.MatchExitLift);
Check("TRLA_809 is admitted for the run player by phase 7 D; its boss side waits on the own-deck ledger (BossDeckLedgerLift, phase 8)",
    DungeonRunAdmission.RuleFor("TRLA_809").Lift == DungeonRunAdmission.BossDeckLedgerLift && DungeonRunAdmission.RuleFor("TRLA_809").Admit == AdmitScope.Player
    && DungeonRunAdmission.RuleFor("TRLA_809").NeverGenerated);
// Every pending lift names a numbered goal phase after this one, with that phase's name; "none:" lifts name no work.
var phaseNames = new Dictionary<int, string> { [3] = "shared draft catalog", [4] = "acts", [5] = "deck mutation/Bob", [6] = "heroes/powers/treasures",
    [7] = "shrines/teammates", [8] = "encounters/anomalies", [9] = "economy" };
foreach (var rule in DungeonRunAdmission.Rules.Where(r => r.Lift != null && !DungeonRunAdmission.IsNoneLift(r.Lift) && r.Lift != DungeonRunAdmission.AddRule))
{
    var m = System.Text.RegularExpressions.Regex.Match(rule.Lift, @"^phase (\d) \(([^)]*)\): \S");
    string name = rule.Family + " " + (rule.Ids?[0] ?? "(default)");
    Check($"{name}: lift names a later numbered phase with its own name", m.Success && phaseNames.TryGetValue(int.Parse(m.Groups[1].Value), out var phase)
        && phase == m.Groups[2].Value, rule.Lift);
}
Check("no lift names a phase-2 step", !DungeonRunAdmission.Rules.Any(r => r.Lift != null && r.Lift.Contains("phase 2")));
// A pending requirement is a rejection: no rule with Requires admits any scope, and its verdict admits nothing.
foreach (var rule in DungeonRunAdmission.Rules.Where(r => r.Requires != null))
{
    string id = rule.Ids?[0];
    Check($"{rule.Family} {id}: a pending requirement admits no scope", rule.Admit == AdmitScope.None
        && !DungeonRunAdmission.Classify(id == null ? null : Engine(id), id, AdmitScope.Any).AdmittedSome, rule.Admit.ToString());
}
Check("phase 7 D lifted the nine teammate start-of-game draws for the run player only (scripted Start of Game draws; never the boss's, never generated)",
    new[] { "TRLA_121", "TRLA_135", "TRLA_139", "TRLA_148", "TRLA_156", "TRLA_164", "TRLA_172", "TRLA_180", "TRLA_188" }.All(id =>
        DungeonRunAdmission.Classify(Engine(id), id, AdmitScope.Player).Admitted && !DungeonRunAdmission.Classify(Engine(id), id, AdmitScope.Boss).AdmittedSome
        && !DungeonRunAdmission.Classify(Engine(id), id, AdmitScope.Generated).AdmittedSome && DungeonRunAdmission.RuleFor(id).Requires == null
        && SabberStoneCore.Actions.StartOfGameRules.HasEffect(Engine(id))));
Check("phase 7 T2 lifted TRLA_191 for the run player only (scripted with SelfCondition.IsNotStartInDeck; never the boss's, never generated)",
    DungeonRunAdmission.Classify(Engine("TRLA_191"), "TRLA_191", AdmitScope.Player).Admitted && !DungeonRunAdmission.Classify(Engine("TRLA_191"), "TRLA_191", AdmitScope.Boss).AdmittedSome
    && !DungeonRunAdmission.Classify(Engine("TRLA_191"), "TRLA_191", AdmitScope.Generated).AdmittedSome && DungeonRunAdmission.RuleFor("TRLA_191").Requires == null);
Check("phase 6 F15 lifted ULDA_045t, ULD_003, GILA_511 and the treasure start-of-game draws for the run player (c-deferrals)",
    new[] { "ULDA_045t", "ULD_003", "GILA_511", "DALA_707", "ULDA_013", "ULDA_040", "ULDA_302", "ULDA_407" }.All(id => DungeonRunAdmission.Classify(Engine(id), id, AdmitScope.Player).Admitted));

// ---------------------------------------------------------------- 3. verdicts per family and scope
foreach (var rule in DungeonRunAdmission.Rules.Where(r => r.Ids != null))
    foreach (string id in rule.Ids)
        foreach (var scope in new[] { AdmitScope.Player, AdmitScope.Boss, AdmitScope.Generated })
        {
            var v = DungeonRunAdmission.Classify(Engine(id), id, scope);
            bool expect = (rule.Admit & scope) != 0;
            if (v.Admitted != expect || v.Family != rule.Family || !v.Subject)
                Check($"{id} {scope}: verdict follows its rule ({rule.Family}, admitted {expect})", false, $"{v.Family} admitted {v.Admitted}");
            else passes++;
        }
// A rejected verdict always carries a lift, whatever the scope.
Check("every rejected verdict of a table id carries a lift", tableIds.All(id => new[] { AdmitScope.Player, AdmitScope.Boss, AdmitScope.Generated, AdmitScope.Both }
    .Select(s => DungeonRunAdmission.Classify(Engine(id), id, s)).All(v => v.Admitted || !string.IsNullOrWhiteSpace(v.Lift))));
Check("Both asks both sides: a player-only rule is AdmittedSome but not Admitted", DungeonRunAdmission.Classify(Engine("TRL_409"), "TRL_409", AdmitScope.Both) is var gral && gral.AdmittedSome && !gral.Admitted);

// Synthetic data through the validator: per family, one pool card (player) and one boss deck card (boss).
var empty = new DungeonRunData { DataVersion = "admission", Rules = data.Rules };
var baseline = new HashSet<string>(DungeonRunValidator.Validate(empty, null));
var poolData = new DungeonRunData { DataVersion = "admission", Rules = data.Rules };
var bossData = new DungeonRunData { DataVersion = "admission", Rules = data.Rules };
var probes = new List<(string id, AdmissionRule rule)>();
foreach (var rule in DungeonRunAdmission.Rules.Where(r => r.Ids != null))
{
    string id = rule.Ids.FirstOrDefault(x => Engine(x) is Card c && (c.Type == CardType.MINION || c.Type == CardType.SPELL || c.Type == CardType.WEAPON));
    if (id == null) continue;
    probes.Add((id, rule));
    var card = Engine(id);
    poolData.Bundles.Add(new DungeonRunBundle { Id = "probe|" + id, Class = card.Class == CardClass.NEUTRAL ? "NEUTRAL" : card.Class.ToString(), Theme = "probe", Cards = new List<string> { id, id, id } });
    bossData.BossDecks["probe|" + id] = new[] { id }.Concat(Enumerable.Repeat("CS2_182", 29)).ToList();
    bossData.Bosses.Add(new DungeonRunBoss { Id = "probe|" + id, Name = "probe", Band = 1, Archetype = "midrange", Deck = "probe|" + id, HeroCard = "HERO_01" });
}
var poolErrors = DungeonRunValidator.Validate(poolData, null).Where(e => !baseline.Contains(e)).ToList();
var bossErrors = DungeonRunValidator.Validate(bossData, null).Where(e => !baseline.Contains(e)).ToList();
Check("a probe card for most rules", probes.Count >= 20, probes.Count.ToString());
foreach (var (id, rule) in probes)
{
    bool playerRejected = poolErrors.Any(e => e.StartsWith(id + ": admission " + rule.Family + ":"));
    bool bossRejected = bossErrors.Any(e => e.StartsWith(id + ": admission " + rule.Family + ":"));
    Check($"{id} ({rule.Family}): validator pool verdict is the table's player scope", playerRejected == ((rule.Admit & AdmitScope.Player) == 0),
        string.Join(" | ", poolErrors.Where(e => e.StartsWith(id))));
    Check($"{id} ({rule.Family}): validator boss-deck verdict is the table's boss scope", bossRejected == ((rule.Admit & AdmitScope.Boss) == 0),
        string.Join(" | ", bossErrors.Where(e => e.StartsWith(id + ": admission"))));
    if (playerRejected)
        Check($"{id}: message format", poolErrors.Any(e => e == $"{id}: admission {rule.Family}: {rule.Reason}; lifted by {rule.Lift} (probe|{id})"),
            string.Join(" | ", poolErrors.Where(e => e.StartsWith(id))));
}
Check("no pool error uses the old Secret or exclusion-list rules", !poolErrors.Concat(bossErrors).Any(e => e.Contains("is a Secret") || e.Contains("run exclusion list")));
// Player powers: GILA_900p (turn-rewind) is now rejected by the validator too.
var powerData = new DungeonRunData { DataVersion = "admission", Rules = data.Rules };
powerData.Classes.Add(new DungeonRunClass { Id = "HUNTER", Name = "probe", HeroCard = "HERO_05", Powers = new List<string> { "GILA_900p" } });
var powerErrors = DungeonRunValidator.Validate(powerData, null);
Check("GILA_900p as a player power: admission turn-rewind", powerErrors.Any(e => e.StartsWith("GILA_900p: admission turn-rewind:") && e.Contains(DungeonRunAdmission.TurnRestartLift)), string.Join(" | ", powerErrors.Where(e => e.StartsWith("GILA"))));
// Boss powers: a secret-generated boss power is rejected, a secret-named one admitted.
var bossPowerData = new DungeonRunData { DataVersion = "admission", Rules = data.Rules };
bossPowerData.BossDecks["d"] = Enumerable.Repeat("CS2_182", 30).ToList();
bossPowerData.Bosses.Add(new DungeonRunBoss { Id = "p1", Name = "probe", Band = 1, Archetype = "midrange", Deck = "d", HeroCard = "HERO_01", Power = "DALA_BOSS_21p" });
bossPowerData.Bosses.Add(new DungeonRunBoss { Id = "p2", Name = "probe", Band = 1, Archetype = "midrange", Deck = "d", HeroCard = "HERO_01", Power = "LOOTA_BOSS_35p" });
var bossPowerErrors = DungeonRunValidator.Validate(bossPowerData, null);
Check("boss power DALA_BOSS_21p: admission secret-generated", bossPowerErrors.Any(e => e.StartsWith("DALA_BOSS_21p: admission secret-generated:")), string.Join(" | ", bossPowerErrors.Where(e => e.StartsWith("DALA"))));
Check("boss power LOOTA_BOSS_35p: no admission error (secret-named, boss)", !bossPowerErrors.Any(e => e.StartsWith("LOOTA_BOSS_35p: admission")), string.Join(" | ", bossPowerErrors.Where(e => e.StartsWith("LOOTA"))));

// ---------------------------------------------------------------- 4. new content cannot slip in
var unruledSecret = Cards.All.Where(c => c.IsSecret && DungeonRunAdmission.RuleFor(c.Id) == null).OrderBy(c => c.Id, StringComparer.Ordinal).FirstOrDefault();
Check("a Secret with no rule exists to probe", unruledSecret != null);
if (unruledSecret != null)
{
    var v = DungeonRunAdmission.Classify(unruledSecret, unruledSecret.Id, AdmitScope.Player);
    Check($"{unruledSecret.Id} (a Secret with no rule): rejected as unclassified, lift names the table", v.Subject && v.Family == "unclassified" && !v.Admitted && v.Lift == DungeonRunAdmission.AddRule, v.Family);
}
var unruledPhrase = Cards.All.Where(c => !c.IsSecret && DungeonRunAdmission.RuleFor(c.Id) == null && DungeonRunValidator.MatchText(c.Text).Contains("secret")
    && c[(GameTag)997] == 0).OrderBy(c => c.Id, StringComparer.Ordinal).FirstOrDefault();
Check("a Secret-phrase card with no rule exists to probe", unruledPhrase != null);
if (unruledPhrase != null)
{
    var v = DungeonRunAdmission.Classify(unruledPhrase, unruledPhrase.Id, AdmitScope.Both);
    Check($"{unruledPhrase.Id} (Secret phrase, no rule): rejected as unclassified in every scope", v.Subject && v.Family == "unclassified" && !v.AdmittedSome, v.Family);
}
var textOnly = DungeonRunAdmission.Classify(null, "PROBE_001", AdmitScope.Player, "Battlecry: Put a <b>Secret</b> from your\ndeck into the battlefield.");
Check("a card known only by its text (no engine card) is classified from the text: unclassified", textOnly.Subject && textOnly.Family == "unclassified" && !textOnly.Admitted);
var plain = DungeonRunAdmission.Classify(Engine("CS2_182"), "CS2_182", AdmitScope.Both);
Check("a vanilla card is not a subject and is admitted", !plain.Subject && plain.Admitted);
var sizeCard = Cards.All.Where(c => c[(GameTag)997] != 0 && DungeonRunAdmission.RuleFor(c.Id) == null).OrderBy(c => c.Id, StringComparer.Ordinal).FirstOrDefault();
if (sizeCard != null)
{
    var v = DungeonRunAdmission.Classify(sizeCard, sizeCard.Id, AdmitScope.Both);
    Check($"{sizeCard.Id} (tag 997): rejected as deck-size, outside the five adventures", v.Family == "deck-size" && !v.AdmittedSome && v.Lift == DungeonRunAdmission.Outside, v.Family);
}
var sideboard = DungeonRunAdmission.Classify(Engine("JAIL_831"), "JAIL_831", AdmitScope.Both);
Check("JAIL_831 (sideboard): rejected as deckbuilding, outside the five adventures", sideboard.Family == "deckbuilding" && !sideboard.AdmittedSome && sideboard.Lift == DungeonRunAdmission.Outside, sideboard.Family);
var questline = Cards.All.Where(c => DungeonRunValidator.MatchText(c.Text).Contains("questline") && DungeonRunAdmission.RuleFor(c.Id) == null && c[GameTag.SECRET] == 0 && c[(GameTag)997] == 0)
    .OrderBy(c => c.Id, StringComparer.Ordinal).FirstOrDefault();
if (questline != null)
{
    var v = DungeonRunAdmission.Classify(questline, questline.Id, AdmitScope.Both);
    Check($"{questline.Id} (questline text): rejected as questline, outside the five adventures", v.Family == "questline" && !v.AdmittedSome && v.Lift == DungeonRunAdmission.Outside, v.Family);
}
var questText = DungeonRunAdmission.Classify(null, "PROBE_Q", AdmitScope.Both, "Questline: Play 3 cards. Reward: a card.");
Check("questline text (no engine card has it today): rejected as questline, outside the five adventures", questText.Family == "questline" && !questText.AdmittedSome && questText.Lift == DungeonRunAdmission.Outside, questText.Family);

// ---------------------------------------------------------------- 5. Fatespinner
var fate = DungeonRunAdmission.Classify(Engine("ICC_047"), "ICC_047", AdmitScope.Any);
Check("Fatespinner (ICC_047, \"Secretly\") is admitted everywhere as text-only", fate.Subject && fate.Family == "text-only" && fate.Admitted);

// ---------------------------------------------------------------- 6. the real data
var errors = DungeonRunValidator.Validate(data, null);
Check("the real run data validates with 0 errors", errors.Count == 0, string.Join(" | ", errors.Take(10)));
var bossDeckSubjects = data.BossDecks.Values.SelectMany(d => d).Distinct().Where(id => DungeonRunAdmission.Classify(Engine(id), id, AdmitScope.Boss).Subject).OrderBy(x => x, StringComparer.Ordinal).ToList();
Check("the real boss decks' subjects are fixed-boss-deck rows, admitted for bosses", bossDeckSubjects.Count >= 8 && bossDeckSubjects.All(fixedRows.Contains)
    && bossDeckSubjects.All(id => DungeonRunAdmission.Classify(Engine(id), id, AdmitScope.Boss).Admitted), string.Join(",", bossDeckSubjects));
var poolSubjects = data.Decks.SelectMany(d => d.Cards).Concat(data.Bundles.SelectMany(b => b.Cards)).Distinct()
    .Where(id => DungeonRunAdmission.Classify(Engine(id), id, AdmitScope.Player).Subject).ToList();
Check("the real starter decks and bundles hold no admission subject yet", poolSubjects.Count == 0, string.Join(",", poolSubjects));
foreach (var cls in data.Classes.Select(c => Enum.Parse<CardClass>(c.Id)))
    Check($"SecretPrior({cls}) on the real data is null (no Secret in the run pool yet: the open pool, no settlement)", DungeonRunAdmission.SecretPrior(data, cls) == null);

// SecretPrior on synthetic data: admitted Secrets only, weight 1 + copies.
var priorData = DungeonRunData.Parse(File.ReadAllText(Path.Combine(root, "dungeon-run", "run-v1.json")),
    File.ReadAllText(Path.Combine(root, "dungeon-run", "pool-v1.json")), File.ReadAllText(Path.Combine(root, "npc", "decks-v1.json")));
priorData.Bundles.Add(new DungeonRunBundle { Id = "probe-mage", Class = "MAGE", Theme = "probe", Cards = new List<string> { "EX1_295", "EX1_295", "EX1_287" } });
priorData.Bundles.Add(new DungeonRunBundle { Id = "probe-mage2", Class = "MAGE", Theme = "probe", Cards = new List<string> { "BOT_402", "UNG_011", "EX1_295" } });
var prior = DungeonRunAdmission.SecretPrior(priorData, CardClass.MAGE);
Check("SecretPrior counts admitted Secrets only: Ice Block 4, Counterspell 2", prior.Count == 2 && prior["EX1_295"] == 4 && prior["EX1_287"] == 2, string.Join(",", prior.Select(p => p.Key + "=" + p.Value)));
Check("SecretPrior ignores other classes' bundles", DungeonRunAdmission.SecretPrior(priorData, CardClass.HUNTER) == null);
// Neutral bundles are offered to every class (DungeonRun.OpenBundleDraft), so their Secrets join every class's pool.
priorData.Bundles.Add(new DungeonRunBundle { Id = "probe-neutral", Class = "NEUTRAL", Theme = "probe", Cards = new List<string> { "EX1_610", "CS2_182", "CS2_182" } });
var hunterPrior = DungeonRunAdmission.SecretPrior(priorData, CardClass.HUNTER);
Check("SecretPrior counts NEUTRAL bundles for every class", hunterPrior != null && hunterPrior.ContainsKey("EX1_610") && DungeonRunAdmission.SecretPrior(priorData, CardClass.MAGE).ContainsKey("EX1_610"),
    hunterPrior == null ? "null" : string.Join(",", hunterPrior.Keys));
Check("legacy runs: the bundle predicate SecretPrior reads is the one the legacy draft uses (OfferedTo)",
    File.ReadAllText(VerifyPaths.Repo("plugin/DungeonRun/DungeonRun.cs")).Contains("b.OfferedTo(cls.Id)")
    && File.ReadAllText(VerifyPaths.Repo("plugin/DungeonRun/DungeonRunAdmission.cs")).Contains("b.OfferedTo(classId)"));
Check("catalog runs: the entry predicate CatalogSecretPrior reads is the one OpenCatalogDraft uses (EligibleFor)",
    File.ReadAllText(VerifyPaths.Repo("plugin/DungeonRun/DungeonRun.cs")).Contains(".Where(b => b.EligibleFor(classes, mechanic))")
    && File.ReadAllText(VerifyPaths.Repo("plugin/DungeonRun/DungeonRunAdmission.cs")).Contains("data.Bundles.Where(b => b.EligibleFor(runClasses, mechanicHolds))"));
{
    // The widened prior (catalog runs) against the closed one (legacy runs).
    foreach (var cls in priorData.Classes)
    {
        var closed = DungeonRunAdmission.SecretPrior(priorData, Enum.Parse<CardClass>(cls.Id));
        var legacyDispatch = DungeonRunAdmission.SecretPrior(priorData, new[] { cls.Id }, false);
        var wide = DungeonRunAdmission.CatalogSecretPrior(priorData, new[] { cls.Id });
        bool superset = closed == null || wide != null && closed.Keys.All(wide.ContainsKey);
        bool samePrior = closed == null ? legacyDispatch == null : legacyDispatch != null && closed.OrderBy(p => p.Key).SequenceEqual(legacyDispatch.OrderBy(p => p.Key));
        var pool = DungeonRunAdmission.GeneratedSecretPool(new[] { cls.Id });
        // Generation also yields the Secrets of each Secret generator's own class (SecretGeneratorClasses), at weight 1 too.
        var genPool = DungeonRunAdmission.GeneratedSecretPool(DungeonRunAdmission.SecretGeneratorClasses(priorData, new[] { cls.Id }, m => true)
            .Union(DungeonRunAdmission.SecretGeneratorClasses(priorData, new[] { cls.Id }, m => false)).ToList());
        bool weights = wide == null || wide.All(p => (pool.Contains(p.Key) || genPool.Contains(p.Key)) && p.Value == 1.0 || p.Value >= 8.0 && p.Value % 4 == 0);
        Check($"{cls.Id}: the widened prior is a superset of the closed prior; the legacy dispatch is the closed prior unchanged; weights 1 (generation) or 4 x (1 + count)",
            superset && samePrior && weights && (pool.Count == 0 || wide != null && pool.All(wide.ContainsKey)),
            $"closed {closed?.Count ?? 0} wide {wide?.Count ?? 0} pool {pool.Count}");
    }
    var magePool = DungeonRunAdmission.GeneratedSecretPool(new[] { "MAGE" });
    var blocked = new HashSet<string>(DungeonRunAdmission.GeneratedRejects(catalogRun: true));
    Check("the generation pool: Secrets a run class may hold, minus what the player's generation never yields (unscripted Secrets included; Pressure Plate is scripted since phase 8 family 01 and in the Hunter pool)",
        magePool.Count > 0 && magePool.All(id => Engine(id).IsSecret && !blocked.Contains(id)
            && SabberStoneCore.Actions.CardClassRules.IsAllowedInDeck(Engine(id), CardClass.MAGE)) && magePool.Contains("EX1_287")
        && DungeonRunAdmission.GeneratedSecretPool(new[] { "WARRIOR" }).Count == 0 && DungeonRunAdmission.GeneratedSecretPool(new[] { "HUNTER" }).Contains("ULD_152")
        && new[] { "HUNTER", "MAGE", "PALADIN", "ROGUE" }.All(c => DungeonRunAdmission.GeneratedSecretPool(new[] { c }).All(id => Engine(id).Implemented)),
        string.Join(",", magePool));
    Check("a dual-class pool is the union of its classes' pools", DungeonRunAdmission.GeneratedSecretPool(new[] { "MAGE", "ROGUE" })
        .SequenceEqual(magePool.Union(DungeonRunAdmission.GeneratedSecretPool(new[] { "ROGUE" })).OrderBy(x => x, StringComparer.Ordinal)));
    // The catalog's widened prior on the real catalog data: every Secret a seeded catalog run drafts, or its player's
    // generation can yield in its class, is inside it.
    var catData = DungeonRunData.Parse(File.ReadAllText(Path.Combine(root, "dungeon-run", "run-v1.json")), File.ReadAllText(Path.Combine(root, "dungeon-run", "pool-v1.json")),
        File.ReadAllText(Path.Combine(root, "npc", "decks-v1.json")), File.ReadAllText(Path.Combine(root, "dungeon-run", "catalog-v1.json")));
    var catRun = new DungeonRun(catData);
    var catAll = new DungeonRunProfile(); catAll.Unlocked.AddRange(catData.Unlocks.Keys);
    int catRuns = 0, catSecrets = 0; var catOutside = new SortedSet<string>(StringComparer.Ordinal);
    foreach (var cls in catData.Classes)
    {
        var wide = DungeonRunAdmission.SecretPrior(catData, cls.ClassSet, true) ?? new Dictionary<string, double>();
        Check($"{cls.Id}: MechanicHolds(secret) exactly when the base prior (no Secret-requiring entry; the generation pool) is non-empty",
            DungeonRunAdmission.MechanicHolds(catData, cls.ClassSet, "secret") == (DungeonRunAdmission.GeneratedSecretPool(cls.ClassSet).Count > 0
                || DungeonRunAdmission.CatalogDeckSourceCards(catData, cls.ClassSet, m => false)
                .Any(id => Engine(id)?.IsSecret == true && DungeonRunAdmission.Classify(Engine(id), id, AdmitScope.Player, null, true).Admitted)));
        for (long seed = 1; seed <= 8; seed++)
        {
            var st = catRun.NewRun(catAll, cls.Id, cls.Decks[(int)(seed % cls.Decks.Count)], cls.Powers[(int)(seed % cls.Powers.Count)], seed * 104729 + catRuns, signatureId: catRun.SignatureOffer(catAll, catRun.Data.FindClass(cls.Id), seed * 104729 + catRuns).FirstOrDefault());
            var rng = new Random((int)(seed * 17 + catRuns));
            for (int guard = 0; guard < 200 && !st.Finished; guard++)
            {
                if (st.Phase == DungeonRunPhase.PreMatch) { catRun.BeginMatch(st); catRun.RecordOutcome(st, new DungeonRunProfile(), DungeonRunOutcome.Win); }
                else if (st.Phase == DungeonRunPhase.TreasureDraft) catRun.PickTreasure(st, st.OpenDraft.Offered[rng.Next(st.OpenDraft.Offered.Count)]);
                else if (st.Phase == DungeonRunPhase.BundleDraft)
                {
                    // Prefer a Secret offer, so the Secret entries are exercised.
                    var secretOffer = st.OpenDraft.Offers.FirstOrDefault(o => o.Cards.Any(id => Engine(id)?.IsSecret == true));
                    catRun.PickBundle(st, secretOffer?.Id ?? st.OpenDraft.Offered[rng.Next(st.OpenDraft.Offered.Count)]);
                }
                else break;
            }
            foreach (string id in st.Deck.Distinct())
            {
                var card = Engine(id);
                if (card == null || !card.IsSecret) continue;
                catSecrets++;
                if (!wide.ContainsKey(id)) catOutside.Add(cls.Id + ":" + id);
            }
            catRuns++;
        }
    }
    Check($"catalog deck sources: {catRuns} seeded catalog runs, every Secret drafted into a run deck is in the run's widened prior", catOutside.Count == 0 && catSecrets > 0,
        "seen " + catSecrets + "; outside " + string.Join(",", catOutside));
    // Secret generators the run can draft (grants included) yield Secrets of their own class: every such Secret is a key of the
    // run's prior. Professor Putricide (ICC_204, Hunter) is granted to Rogue by Tess Greymane, so Rogue and Mage/Rogue runs
    // hold Hunter Secrets in their priors.
    {
        int generators = 0; var genMissing = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var cls in catData.Classes)
        {
            var wide = DungeonRunAdmission.SecretPrior(catData, cls.ClassSet, true) ?? new Dictionary<string, double>();
            bool holds = DungeonRunAdmission.MechanicHolds(catData, cls.ClassSet, "secret");
            foreach (var entry in catData.Bundles.Where(b => b.IsPool && b.EligibleFor(cls.ClassSet, m => m == "secret" && holds)))
                foreach (var c in entry.Candidates)
                {
                    var card = Engine(c.Id);
                    var v = DungeonRunAdmission.Classify(card, c.Id, AdmitScope.Player, null, true);
                    if (card == null || v.Family != "secret-generated" || !v.Admitted) continue;
                    generators++;
                    foreach (string id in DungeonRunAdmission.GeneratedSecretPool(new[] { card.Class.ToString() }).Concat(DungeonRunAdmission.GeneratedSecretPool(cls.ClassSet)))
                        if (!wide.ContainsKey(id)) genMissing.Add($"{cls.Id}:{entry.Id}:{c.Id}->{id}");
                }
        }
        Check($"catalog priors hold every Secret an eligible Secret generator yields in its own class ({generators} generator candidates)", generators > 0 && genMissing.Count == 0,
            string.Join(",", genMissing.Take(8)));
        var rogue = DungeonRunAdmission.SecretPrior(catData, new[] { "ROGUE" }, true);
        var mageRogue = DungeonRunAdmission.SecretPrior(catData, new[] { "MAGE", "ROGUE" }, true);
        var hunterSecrets = new[] { "EX1_554", "ICC_200", "LOOT_079", "KAR_004" };
        Check("Professor Putricide's Hunter Secrets (Snake Trap, Venomstrike Trap, Wandering Monster, Cat Trick) are in the Rogue and Mage/Rogue priors",
            rogue != null && mageRogue != null && hunterSecrets.All(rogue.ContainsKey) && hunterSecrets.All(mageRogue.ContainsKey)
            && DungeonRunAdmission.SecretGeneratorClasses(catData, new[] { "ROGUE" }, m => true).Contains("HUNTER"),
            string.Join(",", hunterSecrets.Where(id => rogue == null || !rogue.ContainsKey(id))));
    }
}
// The closed pool against the deck sources DungeonRun actually offers: seeded runs through the real draft code, every
// class, deck and power; every Secret that ever reaches a run deck must be in that class's prior.
{
    var all = new DungeonRunProfile();
    all.Unlocked.AddRange(priorData.Unlocks.Keys);
    // A Secret in a class starter deck and in an active treasure's reach would also be sources; bundles cover the offer path.
    priorData.Bundles.Add(new DungeonRunBundle { Id = "probe-hunter", Class = "HUNTER", Theme = "probe", Cards = new List<string> { "EX1_611", "EX1_611", "CS2_182" } });
    var srcRun = new DungeonRun(priorData);
    int runs = 0, secretsSeen = 0; var outside = new SortedSet<string>(StringComparer.Ordinal);
    foreach (var cls in priorData.Classes)
        foreach (var deck in cls.Decks)
            foreach (var power in cls.Powers)
                for (long seed = 1; seed <= 6; seed++)
                {
                    var st = srcRun.NewRun(all, cls.Id, deck, power, seed * 7919 + runs, signatureId: srcRun.SignatureOffer(all, srcRun.Data.FindClass(cls.Id), seed * 7919 + runs).FirstOrDefault());
                    var classPrior = DungeonRunAdmission.SecretPrior(priorData, Enum.Parse<CardClass>(cls.Id)) ?? new Dictionary<string, double>();
                    var rng = new Random((int)(seed * 31 + runs));
                    for (int guard = 0; guard < 200 && !st.Finished; guard++)
                    {
                        if (st.Phase == DungeonRunPhase.PreMatch) { srcRun.BeginMatch(st); srcRun.RecordOutcome(st, all, DungeonRunOutcome.Win); }
                        else if (st.Phase == DungeonRunPhase.TreasureDraft) srcRun.PickTreasure(st, st.OpenDraft.Offered[rng.Next(st.OpenDraft.Offered.Count)]);
                        else if (st.Phase == DungeonRunPhase.BundleDraft) srcRun.PickBundle(st, st.OpenDraft.Offered[rng.Next(st.OpenDraft.Offered.Count)]);
                        else break;
                        foreach (string id in st.Deck.Distinct())
                        {
                            var card = Engine(id);
                            if (card == null || !card.IsSecret) continue;
                            secretsSeen++;
                            if (!classPrior.ContainsKey(id)) outside.Add(cls.Id + ":" + id);
                        }
                    }
                    runs++;
                }
    Check($"deck sources: {runs} seeded runs, every Secret drafted into a run deck is in its class's prior", outside.Count == 0 && secretsSeen > 0,
        "seen " + secretsSeen + "; outside " + string.Join(",", outside));
}
var priorErrors = DungeonRunValidator.Validate(priorData, null).Where(e => (e.StartsWith("BOT_402") || e.StartsWith("UNG_011") || e.StartsWith("EX1_295") || e.StartsWith("EX1_287")) && e.Contains(": admission ")).ToList();
Check("the validator rejects the Secret generators (secret-generated) and admits the Secrets", priorErrors.Count == 2
    && priorErrors.All(e => e.Contains("admission secret-generated:") && e.Contains(DungeonRunAdmission.GeneratedSecretsLift)), string.Join(" | ", priorErrors));

// ---------------------------------------------------------------- 7. the Generated scope
var rejects = DungeonRunAdmission.GeneratedRejects().ToList();
var bossRejects = DungeonRunAdmission.GeneratedRejectsBoss().ToList();
Check("GeneratedRejects() and GeneratedRejectsBoss() are sorted ordinal, distinct and disjoint",
    rejects.SequenceEqual(rejects.Distinct().OrderBy(x => x, StringComparer.Ordinal)) && bossRejects.SequenceEqual(bossRejects.Distinct().OrderBy(x => x, StringComparer.Ordinal))
    && !rejects.Intersect(bossRejects).Any());
// The whole generatable pool: no card that Classify rejects in the Generated scope is missing from the lists.
var rejectSet = new HashSet<string>(rejects, StringComparer.Ordinal); var bossSet = new HashSet<string>(bossRejects, StringComparer.Ordinal);
var missing = new List<string>(); var wrongSide = new List<string>(); var extra = new List<string>();
foreach (var card in Cards.All.Where(c => c?.Id != null).GroupBy(c => c.Id).Select(g => g.First()))
{
    bool rejected = !DungeonRunAdmission.Classify(card, card.Id, AdmitScope.Generated).Admitted;
    bool player = DungeonRunAdmission.Classify(card, card.Id, AdmitScope.Player).Admitted;
    bool listed = rejectSet.Contains(card.Id) || bossSet.Contains(card.Id);
    if (rejected && !listed) missing.Add(card.Id);
    if (!rejected && listed) extra.Add(card.Id);
    // Phase 7: a NeverGenerated row (a run shrine, shrine upgrade or teammate) is blocked for both sides even where it admits the player.
    bool never = DungeonRunAdmission.RuleFor(card.Id)?.NeverGenerated == true;
    if (rejected && listed && (player && !never ? !bossSet.Contains(card.Id) : !rejectSet.Contains(card.Id))) wrongSide.Add(card.Id);
}
Check("no card of the engine pool is rejected by Classify(Generated) without being listed", missing.Count == 0, missing.Count + ": " + string.Join(",", missing.Take(20)));
Check("no listed card is admitted in the Generated scope", extra.Count == 0, string.Join(",", extra.Take(20)));
Check("player-admitted rejects are boss-only, the rest apply to both sides", wrongSide.Count == 0, string.Join(",", wrongSide.Take(20)));
var collectibleRejects = Cards.All.Where(c => c.Collectible && c.Implemented && !c.IsSecret && rejectSet.Contains(c.Id)).Select(c => c.Id).Distinct().Count();
Note("Generated lists: " + rejects.Count + " for both sides (" + collectibleRejects + " collectible, scripted, non-Secret), " + bossRejects.Count + " boss-only");
Check("the reviewed unclassified cards are blocked for both sides (ICC_851, KAR_096, GIL_692, GIL_826, EX1_186, GIL_648, KAR_037, OG_254, ICC_086)",
    new[] { "ICC_851", "KAR_096", "GIL_692", "GIL_826", "EX1_186", "GIL_648", "KAR_037", "OG_254", "ICC_086" }.All(rejectSet.Contains),
    string.Join(",", new[] { "ICC_851", "KAR_096", "GIL_692", "GIL_826", "EX1_186", "GIL_648", "KAR_037", "OG_254", "ICC_086" }.Where(id => !rejectSet.Contains(id))));
Check("deck-wide-change and Gral, admitted for the player, are blocked for the boss's generation only",
    new[] { "BOT_257", "GIL_694", "GIL_840", "TRL_500", "UNG_840", "DALA_727", "ULDA_024", "ULDA_044", "TRL_409" }.All(id => bossSet.Contains(id) && !rejectSet.Contains(id)),
    string.Join(",", bossRejects));
Check("every secret-generated and turn-rewind id is blocked for both sides, and no secret-from-deck id at all",
    DungeonRunAdmission.Rules.Where(r => r.Family == "secret-generated" || r.Family == "turn-rewind").SelectMany(r => r.Ids).All(rejectSet.Contains)
    && !DungeonRunAdmission.Rules.Where(r => r.Family == "secret-from-deck").SelectMany(r => r.Ids).Any(id => rejectSet.Contains(id) || bossSet.Contains(id)));
var plan = new RunMatchPlan { PlayerHero = Cards.FromId("HERO_08"), PlayerDeck = Enumerable.Repeat(Cards.FromId("CS2_182"), 10).ToList(),
    BossHero = Cards.FromId("HERO_01"), BossDeck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(), ExcludedGenerated = rejects, ExcludedGeneratedBoss = bossRejects };
var cfg = new GameConfig { FormatType = FormatType.FT_WILD, RandomSeed = 1 };
DungeonRunSetup.Configure(cfg, plan);
Check("DungeonRunSetup.Configure copies the lists into GameConfig (both sides; player 2, the boss)",
    cfg.ExcludedGeneratedIds.SequenceEqual(rejects) && cfg.Player2ExcludedGeneratedIds.SequenceEqual(bossRejects) && cfg.Player1ExcludedGeneratedIds.Count == 0);
var run = new DungeonRun(data);
string runSource = File.ReadAllText(VerifyPaths.Repo("plugin/DungeonRun/DungeonRun.cs"));
Check("DungeonRun.BuildPlan source carries both lists, per run kind, and the player-generation flag (checked in DungeonRun.cs)",
    runSource.Contains("ExcludedGenerated = DungeonRunAdmission.GeneratedRejects(state.IsCatalogRun),") && runSource.Contains("ExcludedGeneratedBoss = DungeonRunAdmission.GeneratedRejectsBoss(state.IsCatalogRun),")
    && runSource.Contains("PlayerGeneratedSecrets = state.IsCatalogRun,"));
{
    // Catalog-run generation lists: the lifted Secret generators move from both sides to the boss's list only; nothing else moves.
    var crej = DungeonRunAdmission.GeneratedRejects(catalogRun: true); var cboss = DungeonRunAdmission.GeneratedRejectsBoss(catalogRun: true);
    var moved = rejects.Except(crej).OrderBy(x => x, StringComparer.Ordinal).ToList();
    // Phase 8: DALA_BOSS_21p carries a catalog lift for the Boss scope only; it stays in the shared list (never generated by either side).
    var lifted = DungeonRunAdmission.Rules.Where(r => r.CatalogAdmit != null && (r.CatalogAdmit.Value & AdmitScope.Generated) != 0).SelectMany(r => r.Ids).OrderBy(x => x, StringComparer.Ordinal).ToList();
    // And every unscripted Secret joins the shared list: the engine's Secret generation does not check Implemented.
    var unscriptedSecrets = Cards.All.Where(c => c?.Id != null && c.IsSecret && !c.Implemented).Select(c => c.Id).Distinct().ToList();
    var added = crej.Except(rejects).OrderBy(x => x, StringComparer.Ordinal).ToList();
    // The lifts admit scripted cards only: the unscripted lifted ids (Hunter's Pack ULD_429 and four adventure cards) stay in
    // the shared list.
    var liftedScripted = lifted.Where(id => Cards.FromId(id)?.Implemented == true).ToList();
    Check("catalog generation lists: exactly the scripted lifted ids leave the shared list for the boss's; unscripted Secrets join the shared list",
        moved.SequenceEqual(liftedScripted) && added.All(id => unscriptedSecrets.Contains(id) && bossRejects.Contains(id)) && unscriptedSecrets.All(crej.Contains)
        && cboss.Except(bossRejects).OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(liftedScripted) && bossRejects.Except(cboss).OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(added),
        "moved " + string.Join(",", moved) + "; added " + string.Join(",", added));
    var unscriptedLegacy = rejects.Where(id => Cards.FromId(id) is var c && c != null && !c.Implemented).ToList();
    Check("GeneratedRejects(true) holds every unscripted id of GeneratedRejects(false); ULD_429, scripted since phase 6 F10, is on the boss-only list",
        unscriptedLegacy.Count > 0 && unscriptedLegacy.All(crej.Contains) && !crej.Contains("ULD_429") && cboss.Contains("ULD_429"),
        string.Join(",", unscriptedLegacy.Where(id => !crej.Contains(id))));
    // The engine reads each generating controller's own Secret flag: in a catalog run match the player's random Mage spells
    // include Secrets, the boss's never do; a legacy run match blocks both.
    var cplan = new RunMatchPlan { PlayerHero = Cards.FromId("HERO_08"), PlayerDeck = Enumerable.Repeat(Cards.FromId("CS2_182"), 10).ToList(),
        BossHero = Cards.FromId("HERO_08"), BossDeck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(), BossHealth = 30,
        ExcludedGenerated = crej, ExcludedGeneratedBoss = cboss, PlayerGeneratedSecrets = true };
    var ccfg = new GameConfig { RandomSeed = 5, Shuffle = true, SkipMulligan = false, History = false, Logging = false, FormatType = FormatType.FT_WILD };
    DungeonRunSetup.Configure(ccfg, cplan);
    var cg = new Game(ccfg); cg.StartGame(); DungeonRunSetup.ApplyAfterStart(cg, cplan);
    cg.Process(ChooseTask.Mulligan(cg.Player1, new List<int>())); cg.Process(ChooseTask.Mulligan(cg.Player2, new List<int>())); cg.MainBegin();
    bool playerSecrets = RandomCardTask.GetCardList(cg.Player1.Hero, CardType.SPELL, CardClass.MAGE).Any(x => x.IsSecret);
    bool flags = !cg.ExcludesGeneratedSecretsFor(1) && cg.ExcludesGeneratedSecretsFor(2) && !cg.ExcludeGeneratedSecrets;
    cg.Process(EndTurnTask.Any(cg.CurrentPlayer));
    bool bossSecrets = RandomCardTask.GetCardList(cg.Player2.Hero, CardType.SPELL, CardClass.MAGE).Any(x => x.IsSecret);
    Check("engine (catalog run): the player's generated Mage spells include Secrets, the boss's do not", playerSecrets && !bossSecrets && flags,
        $"player {playerSecrets} boss {bossSecrets} flags {flags}");
    var lg = NewRun("HERO_08", "HERO_08", null, null, 5, rejects, bossRejects);
    bool legacyPlayer = RandomCardTask.GetCardList(lg.Player1.Hero, CardType.SPELL, CardClass.MAGE).Any(x => x.IsSecret);
    Check("engine (legacy run): neither side's generation yields a Secret", !legacyPlayer && lg.ExcludeGeneratedSecrets && lg.ExcludesGeneratedSecretsFor(1) && lg.ExcludesGeneratedSecretsFor(2));
}
// The engine reads the generating controller: the player's random Mage spells keep Luna's Pocket Galaxy, the boss's do not.
{
    var g = NewRun("HERO_08", "HERO_08", null, null, 5, rejects, bossRejects);
    bool PoolHas(Controller c, string id) => RandomCardTask.GetCardList(c.Hero, CardType.SPELL, CardClass.MAGE).Any(x => x.Id == id);
    bool playerTurn = PoolHas(g.Player1, "BOT_257"); bool playerKeleseth = !g.BlocksGenerated(Cards.FromId("ICC_851"));
    g.Process(EndTurnTask.Any(g.CurrentPlayer));
    bool bossTurn = PoolHas(g.Player2, "BOT_257"); bool bossKeleseth = !g.BlocksGenerated(Cards.FromId("ICC_851"));
    Check("engine: the player's generation keeps BOT_257, the boss's drops it", playerTurn && !bossTurn, "player " + playerTurn + " boss " + bossTurn);
    Check("engine: ICC_851 (unclassified) is dropped for both sides", !playerKeleseth && !bossKeleseth, "player " + playerKeleseth + " boss " + bossKeleseth);
    var plainGame = new Game(new GameConfig { FormatType = FormatType.FT_WILD, RandomSeed = 5, StartPlayer = 1, Player1HeroClass = CardClass.MAGE, Player2HeroClass = CardClass.MAGE,
        Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(), Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(), Shuffle = false, SkipMulligan = true, History = false });
    plainGame.StartGame();
    Check("engine: a non-run game filters nothing", !plainGame.FiltersGenerated && !plainGame.BlocksGenerated(Cards.FromId("ICC_851")) && !plainGame.BlocksGenerated(Cards.FromId("BOT_257"))
        && RandomCardTask.GetCardList(plainGame.Player1.Hero, CardType.SPELL, CardClass.MAGE).Any(x => x.Id == "BOT_257"));
}

// ---------------------------------------------------------------- 8. validator coverage: passive treasures, legacy powers, content shells
{
    var tdata = new DungeonRunData { DataVersion = "admission", Rules = data.Rules };
    // Phase 6 F15 admitted GILA_511 and ULDA_045t; phase 7 T2 admitted TRLA_191 and phase 7 D TRLA_121 (teammates): GILA_911
    // (turn-rewind, phase 9) and ULDA_111 (start-of-game-draw, a match exit, phase 9) are rejected ids that stand in as passive treasures.
    tdata.Treasures.Add(new DungeonRunTreasure { Id = "GILA_911", Rarity = "common", Universal = true });
    tdata.Treasures.Add(new DungeonRunTreasure { Id = "ULDA_111", Rarity = "common", Universal = true });
    var terr = DungeonRunValidator.Validate(tdata, null);
    Check("a passive treasure the table rejects (GILA_911) fails admission", terr.Any(e => e.StartsWith("GILA_911: admission turn-rewind:") && e.Contains("phase 9")), string.Join(" | ", terr.Where(e => e.StartsWith("GILA_911"))));
    Check("a passive treasure the table rejects (ULDA_111) fails admission", terr.Any(e => e.StartsWith("ULDA_111: admission start-of-game-draw:")), string.Join(" | ", terr.Where(e => e.StartsWith("ULDA_111"))));
    var ldata = new DungeonRunData { DataVersion = "admission", Rules = data.Rules };
    ldata.Classes.Add(new DungeonRunClass { Id = "HUNTER", Name = "probe", HeroCard = "HERO_05", Powers = new List<string> { "HERO_05bp" } });
    ldata.LegacyHeroes["probe"] = new DungeonRunLegacyHero { Class = "HUNTER", Power = "GILA_900p" };
    var lerr = DungeonRunValidator.Validate(ldata, null);
    Check("a legacy hero power the table rejects (GILA_900p) fails admission", lerr.Any(e => e.StartsWith("GILA_900p: admission turn-rewind:")), string.Join(" | ", lerr.Where(e => e.StartsWith("GILA"))));
    // Synthetic content entries: a shrine power whose shell the table rejects, and a passive treasure with a rejected effect shell.
    var shrine = new RunContent { Key = "probe_shrine_1", Kind = RunContentKind.PassivePower, ShellId = "GILA_900p", Name = "probe", Text = "probe" };
    var fx = new RunContent { Key = "probe_fx", Kind = RunContentKind.Effect, ShellId = "ULDA_111", Name = "probe", Text = "probe" }; // TRLA_809 until phase 7 D admitted it
    var passive = new RunContent { Key = "probe_passive", Kind = RunContentKind.PassiveTreasure, EffectKey = "probe_fx", Name = "probe", Text = "probe" };
    var cdata = new DungeonRunData { DataVersion = "admission", Rules = data.Rules };
    cdata.Treasures.Add(new DungeonRunTreasure { Id = "probe_passive", Rarity = "common", Universal = true });
    var content = DungeonRunContent.All.Concat(new[] { shrine, fx, passive }).ToList();
    var cerr = DungeonRunValidator.Validate(cdata, null, content);
    Check("a shrine content shell the table rejects fails admission (content loop, player scope)", cerr.Any(e => e.StartsWith("GILA_900p: admission turn-rewind:") && e.Contains("content probe_shrine_1")),
        string.Join(" | ", cerr.Where(e => e.Contains("probe"))));
    Check("a passive treasure's rejected effect shell fails admission", cerr.Any(e => e.StartsWith("ULDA_111: admission start-of-game-draw:") && e.Contains("probe_passive effect")),
        string.Join(" | ", cerr.Where(e => e.Contains("probe"))));
    var realErr = DungeonRunValidator.Validate(data, null, DungeonRunContent.All);
    Check("the real data still validates with every content shell classified", realErr.Count == 0, string.Join(" | ", realErr.Take(10)));
}

// ---------------------------------------------------------------- 9. admitted deck-reading cards: real outcome == the bot's fair-view outcome
// The bot's own starting deck list is its own knowledge (FairBotState keeps DeckCards for the owner); every admitted
// deck-condition, highlander and empty-deck card must resolve in the boss's fair view as it does in the real game.
{
    var bossRecord = data.Bosses.First(b => b.Archetype != null);
    var bossInfo = DungeonRunBossPilot.Build(bossRecord, new RunMatchPlan { PlayerHero = Cards.FromId("HERO_08"), BossHero = Cards.FromId("HERO_01"), BossDeck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList() }, null);
    var golden = new GoldenPilot(bossInfo.Profile, bossInfo.Plan, null, null);
    FairBotState View(Game g) => golden.Options.FairViewFactory(g, g.Player2.PlayerId, 12345, bossInfo.Deck.Style);
    var singles = Enumerable.Range(0, 11).Select(cost => Cards.All.Where(c => c.Collectible && c.Implemented && c.Type == CardType.MINION && c.Class == CardClass.NEUTRAL && c.Cost == cost)
        .OrderBy(c => c.Id, StringComparer.Ordinal).First().Id).ToList();
    var eliseDeck = singles.Concat(singles.Take(4)).ToList();
    var eg = NewRun("HERO_08", "HERO_05", null, eliseDeck, 99, null, null);
    eg.Process(EndTurnTask.Any(eg.CurrentPlayer));
    bool realEligible = CompositeLocationRules.Eligible(eg.Player2), fairEligible = CompositeLocationRules.Eligible(View(eg).FingerprintGame.Player2);
    Check("TLC_100 eligibility (11 distinct costs) in the boss's fair view equals the real game", realEligible && realEligible == fairEligible, "real " + realEligible + " fair " + fairEligible);
    var opp = View(eg).FingerprintGame.Player1;
    Check("the opponent's starting deck list stays hidden in the boss's fair view", opp.DeckCards.All(c => c.Id == Unknown.UnknownCard.Id), string.Join(",", opp.DeckCards.Take(3).Select(c => c.Id)));
    var hunterOnly = new List<string> { "EX1_611", "EX1_610", "DS1_178", "DS1_178", "CS2_084", "CS2_084", "DS1_070", "DS1_070", "EX1_543", "DS1_175", "DS1_183", "DS1_183" };
    var deckCards = DungeonRunAdmission.Rules.Where(r => r.Ids != null && (r.Family == "deck-condition" || r.Family == "highlander" || r.Family == "empty-deck")
        && (r.Admit & AdmitScope.Boss) != 0).SelectMany(r => r.Ids).Where(id => Engine(id) is Card c && c.Implemented && (c.Type == CardType.MINION || c.Type == CardType.SPELL)).ToList();
    foreach (string id in deckCards)
    {
        var card = Engine(id);
        string hero = card.Class == CardClass.HUNTER ? "HERO_05" : card.Class == CardClass.MAGE ? "HERO_08" : card.Class == CardClass.WARRIOR ? "HERO_01"
            : card.Class == CardClass.PALADIN ? "HERO_04" : card.Class == CardClass.ROGUE ? "HERO_03" : card.Class == CardClass.PRIEST ? "HERO_09"
            : card.Class == CardClass.WARLOCK ? "HERO_07" : card.Class == CardClass.SHAMAN ? "HERO_02" : card.Class == CardClass.DRUID ? "HERO_06" : "HERO_05";
        foreach (var (deckName, deck) in new[] { ("singles", singles), ("elise", eliseDeck), ("hunter", hunterOnly), ("empty", new List<string>()) })
        {
            var g = NewRun("HERO_08", hero, null, deck, 99, null, null);
            g.Process(EndTurnTask.Any(g.CurrentPlayer));
            var boss = g.Player2;
            while (boss.HandZone.Count > 0) boss.HandZone.Remove(boss.HandZone[0]);
            if (deckName == "empty") while (boss.DeckZone.Count > 0) boss.DeckZone.Remove(boss.DeckZone[0]);
            boss.BaseMana = 10; boss.UsedMana = 0; boss.OverloadLocked = 0; boss.Hero.Damage = 20;
            var played = Entity.FromCard(boss, card); Generic.AddHandPhase(boss, played);
            var view = View(g);
            var act = view.Actions.FirstOrDefault(a => a.Type == PlayerTaskType.PLAY_CARD && a.Source == played.Id);
            if (act == null) { Note(id + "/" + deckName + ": not playable here"); continue; }
            var real = g.CloneExact();
            real.Process(PlayCardTask.Any(real.Player2, real.IdEntityDic[played.Id], null, -1, act.ChooseOne));
            var after = view.Apply(act).FingerprintGame;
            string Measure(Game x) => x.Player2.BoardZone.Count + "/" + x.Player2.HandZone.Count + "/" + x.Player2.Hero.Health + "/" + x.Player2.Hero.Armor
                + "/" + (x.Player2.Choice != null ? "choice:" + x.Player2.Choice.Choices.Count : "no-choice");
            Check($"{id} ({deckName} deck): the boss's fair-view outcome equals the real one", Measure(real) == Measure(after), "real " + Measure(real) + " fair " + Measure(after));
        }
    }
}

Console.WriteLine($"{passes} passed, {failures} failed");
return failures == 0 ? 0 : 1;

static IEnumerable<string[]> ReadCsv(string path)
{
    using var reader = new StreamReader(path);
    var row = new List<string>(); var field = new System.Text.StringBuilder(); bool quoted = false;
    int ch;
    while ((ch = reader.Read()) != -1)
    {
        char c = (char)ch;
        if (quoted)
        {
            if (c == '"') { if (reader.Peek() == '"') { field.Append('"'); reader.Read(); } else quoted = false; }
            else field.Append(c);
        }
        else if (c == '"') quoted = true;
        else if (c == ',') { row.Add(field.ToString()); field.Clear(); }
        else if (c == '\n') { row.Add(field.ToString().TrimEnd('\r')); field.Clear(); yield return row.ToArray(); row.Clear(); }
        else field.Append(c);
    }
    if (field.Length > 0 || row.Count > 0) { row.Add(field.ToString()); yield return row.ToArray(); }
}
