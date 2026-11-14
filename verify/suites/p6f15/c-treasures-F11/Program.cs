// c-treasures-F11: phase 6 family F11 of the unified Dungeon Run, the Tombs of Terror signature treasures: the client's
// ADVENTURE_LOADOUT_TREASURES rows of adventure 513 (six per explorer, keyed by classId 4 Reno, 5 Finley, 2 Elise, 3 Brann;
// ULDA_302 is family F15's), the Jr. to Sr. upgrades (ULDA_016 > 021, 015 > 020, 017 > 022, 018 > 023: a profile unlock applied
// at NewRun, design section 4 kind C) and Pack Mule (ULDA_019 > ULDA_024 when an act is cleared, kind B). For every id: the engine
// content (scripted, keyed and shelled by its own client id, name and text as facts/violet-hold-2026-09.json has them), the run
// data (the dual rows' signature lists, the signature treasure rows, their unlocks), the validator and admission checks, a
// scripted regression game through the run setup path (DungeonRun.NewRun on the explorer's row with the signature picked ->
// BuildPlan -> DungeonRunSetup.Configure -> Game -> ApplyAfterStart -> mulligan -> MainBegin -> the signature drawn from the
// run deck and played), and a full game against the real boss AI (DungeonRunBossPilot + GoldenPilot) in which the player plays
// it; it must end without a fault or an AI fallback. Headless: compiles C:\SoloStone\plugin\DungeonRun\*.cs and the NPC AI
// against the engine build. Run it twice and compare.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.CardSets.DungeonRun;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
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
string R(string f) => File.ReadAllText(Path.Combine(root, f));
string runJson = R("dungeon-run/run-v1.json"), poolJson = R("dungeon-run/pool-v1.json"), npcJson = R("npc/decks-v1.json"),
    catalogJson = R("dungeon-run/catalog-v1.json"), scheduleJson = R("dungeon-run/schedule-v1.json"), tavernJson = R("dungeon-run/tavern-v1.json");
DungeonRunData Load(string json) => DungeonRunData.Parse(json, poolJson, npcJson, catalogJson, scheduleJson, tavernJson);
var data = Load(runJson);
var run = new DungeonRun(data);
var all = new DungeonRunProfile(); all.Unlocked.AddRange(data.Unlocks.Keys);
var facts = System.Text.Json.JsonDocument.Parse(File.ReadAllText(VerifyPaths.Repo("facts/violet-hold-2026-09.json")));
var factById = new Dictionary<string, System.Text.Json.JsonElement>(StringComparer.Ordinal);
foreach (var c in facts.RootElement.GetProperty("cards").EnumerateArray()) factById[c.GetProperty("id").GetString()] = c;
foreach (var c in facts.RootElement.GetProperty("overlay").GetProperty("cards").EnumerateArray()) factById[c.GetProperty("id").GetString()] = c;
string FactText(string id) => factById[id].TryGetProperty("text", out var t) ? t.GetString() : null;
string FactName(string id) => factById[id].GetProperty("name").GetString();
int FactTag(string id, int tag)
{
    foreach (var pair in factById[id].GetProperty("tags").EnumerateArray())
        if (pair[0].GetInt32() == tag) return pair[1].GetInt32();
    return 0;
}
Card C(string id) => Cards.FromId(id);
bool Has(IPlayable p, string ench) => p.AppliedEnchantments?.Any(e => e.Card.Id == ench) == true;
var tables = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "dungeon-run/reference/client-tables.json"))).RootElement.GetProperty("tables");
var clientIds = new HashSet<string>(tables.GetProperty("CARD").EnumerateArray().Select(r => r.GetProperty("id").GetString()), StringComparer.Ordinal);
CardType FactType(string id) => FactTag(id, 202) switch { 3 => CardType.HERO, 4 => CardType.MINION, 5 => CardType.SPELL, 6 => CardType.ENCHANTMENT, 7 => CardType.WEAPON, 10 => CardType.HERO_POWER, _ => CardType.INVALID };
string ClassOfTag(int v) => v switch { 2 => "DRUID", 3 => "HUNTER", 4 => "MAGE", 5 => "PALADIN", 6 => "PRIEST", 7 => "ROGUE", 8 => "SHAMAN", 9 => "WARLOCK", 10 => "WARRIOR", 12 => "NEUTRAL", 0 => "NEUTRAL", _ => "?" + v };

// The client's loadout rows (adventure 513): classId -> [(sortOrder, card, upgraded card)]
var loadout = new Dictionary<int, List<(int Sort, string Card, string Upgraded)>>();
foreach (var r in tables.GetProperty("ADVENTURE_LOADOUT_TREASURES").EnumerateArray())
{
    if (r.GetProperty("adventureId").GetInt32() != 513) continue;
    int cls = r.GetProperty("classId").GetInt32();
    if (!loadout.TryGetValue(cls, out var l)) loadout[cls] = l = new List<(int, string, string)>();
    var up = r.GetProperty("upgradedCardId_card");
    l.Add((r.GetProperty("sortOrder").GetInt32(), r.GetProperty("cardId_card").GetString(), up.ValueKind == System.Text.Json.JsonValueKind.String ? up.GetString() : null));
}
// explorer, client classId, row, Jr, Sr
var explorers = new (string X, int ClassId, string Row, string Jr, string Sr)[]
{
    ("Reno", 4, "MAGE_ROGUE", "ULDA_016", "ULDA_021"),
    ("Finley", 5, "PALADIN_SHAMAN", "ULDA_015", "ULDA_020"),
    ("Elise", 2, "DRUID_PRIEST", "ULDA_017", "ULDA_022"),
    ("Brann", 3, "HUNTER_WARRIOR", "ULDA_018", "ULDA_023"),
};
string RowOf(string id) => id == "ULDA_019" || id == "ULDA_024" ? "MAGE_ROGUE"
    : explorers.First(e => loadout[e.ClassId].Any(x => x.Card == id || x.Upgraded == id)).Row;
string[] ids = DungeonRunContent.TombsSignatureIds.ToArray();
var effectShells = new Dictionary<string, string>
{
    ["fx_ULDA_203e"] = "ULDA_203e", ["fx_ULDA_018e"] = "ULDA_018e", ["fx_ULDA_023e"] = "ULDA_023e", ["fx_ULDA_019e"] = "ULDA_019e", ["fx_ULDA_024e"] = "ULDA_024e",
    ["fx_ULDA_303e"] = "ULDA_303e", ["fx_ULDA_502e"] = "ULDA_502e", ["fx_ULDA_505e"] = "ULDA_505e", ["fx_ULDA_402e"] = "ULDA_402e",
};
var tokens = new Dictionary<string, string[]>
{
    ["ULDA_507"] = new[] { "CS2_101t" }, ["ULDA_010"] = new[] { "EX1_506a" }, ["ULDA_305"] = new[] { "EX1_158t" }, ["ULDA_405"] = new[] { "EX1_543" },
};

// ---------------------------------------------------------------- T1 content
{
    var expected = explorers.SelectMany(e => loadout[e.ClassId].OrderBy(x => x.Sort).Select(x => x.Card).Where(x => x != "ULDA_302")
        .SelectMany(x => x == e.Jr ? new[] { e.Jr, e.Sr } : new[] { x })).Concat(new[] { "ULDA_019", "ULDA_024" }).ToList();
    Check("T1 29 F11 ids: the client's 24 loadout rows of the Tombs less ULDA_302, the four Sr. upgrades, Pack Mule and its upgrade",
        ids.Length == 29 && ids.Distinct().Count() == 29 && loadout.Values.Sum(l => l.Count) == 24 && expected.OrderBy(x => x).SequenceEqual(ids.OrderBy(x => x))
        && explorers.All(e => loadout[e.ClassId].Count == 6 && loadout[e.ClassId].Single(x => x.Card == e.Jr).Upgraded == e.Sr), string.Join(",", expected));
}
foreach (string id in ids)
{
    var content = DungeonRunContent.Get(id);
    var card = C(id);
    Check($"T1 {id}: an ActiveTreasure keyed and shelled by its own id, scripted, the client's {FactType(id)} of class {ClassOfTag(FactTag(id, 199))}",
        content.Kind == RunContentKind.ActiveTreasure && content.ShellId == id && card.Implemented && card.Type == FactType(id) && DungeonRunContent.ForShell(id) == content
        && clientIds.Contains(id) && card.Class.ToString() == ClassOfTag(FactTag(id, 199)), content.Kind + " " + card.Type + " " + card.Implemented + " " + card.Class);
    Check($"T1 ids-text-match {id}: name and text equal facts/violet-hold-2026-09.json", content.Name == FactName(id) && content.Text == FactText(id), content.Name + " | " + content.Text);
    bool stats = card.Type == CardType.MINION ? card.ATK == FactTag(id, 47) && card.Health == FactTag(id, 45)
        : card.Type == CardType.WEAPON ? card.ATK == FactTag(id, 47) && card[GameTag.DURABILITY] == FactTag(id, 187) : true;
    Check($"T1 {id}: cost {FactTag(id, 48)}" + (card.Type == CardType.MINION || card.Type == CardType.WEAPON ? $", {FactTag(id, 47)} Attack" : "") + " as the client prints it",
        card.Cost == FactTag(id, 48) && content.Cost == FactTag(id, 48) && stats, $"{card.Cost} {card.ATK}/{card.Health} dur {card[GameTag.DURABILITY]}");
    var want = tokens.TryGetValue(id, out var tk) ? tk : new string[0];
    Check($"T1 {id}: tokens [{string.Join(",", want)}], each a client card", content.Tokens.SequenceEqual(want) && want.All(clientIds.Contains));
}
foreach (var fx in effectShells)
{
    var e = DungeonRunContent.Get(fx.Key);
    Check($"T1 {fx.Value}: content {fx.Key} on the client's own ENCHANTMENT, scripted, name and text word for word",
        e.Kind == RunContentKind.Effect && e.ShellId == fx.Value && C(fx.Value).Implemented && C(fx.Value).Type == CardType.ENCHANTMENT && clientIds.Contains(fx.Value)
        && e.Name == FactName(fx.Value) && e.Text == FactText(fx.Value), e.Name + " | " + e.Text);
}
foreach (string borrowed in new[] { "LOOT_998le", "DAL_070e", "AT_069e" })
    Check($"T1 {borrowed}: the borrowed enchantment is the engine's own, scripted, a client ENCHANTMENT", C(borrowed).Implemented && C(borrowed).Type == CardType.ENCHANTMENT && clientIds.Contains(borrowed)
        && DungeonRunContent.ForShell(borrowed) == null);
Check("T1 the four Tomb Divers and Excavators carry the BATTLECRY their text names; Jr. and Sr. Scout keep Charge and Windfury, Maxwell Rush and Reborn, the Lance Lifesteal",
    new[] { "ULDA_016", "ULDA_021", "ULDA_018", "ULDA_023" }.All(id => C(id)[GameTag.BATTLECRY] == 1) && C("ULDA_015").Charge && C("ULDA_020").Charge
    && C("ULDA_015")[GameTag.WINDFURY] == 1 && C("ULDA_501").Rush && C("ULDA_501")[GameTag.REBORN] == 1 && C("ULDA_505").LifeSteal);
Check("T1 Reno's Magical Torch prints 4 damage and The Gatling Wand 3 (their script data)", C("ULDA_204")[GameTag.TAG_SCRIPT_DATA_NUM_1] == 4 && C("ULDA_207")[GameTag.TAG_SCRIPT_DATA_NUM_1] == 3);

// ---------------------------------------------------------------- T2 data, validator, admission
var transport = new Dictionary<string, string>
{
    ["CS2_034"] = "HERO_08bp", ["CS2_102"] = "HERO_01bp", ["CS1h_001"] = "HERO_09bp", ["CS2_101"] = "HERO_04bp", ["CS2_017"] = "HERO_06bp",
    ["DS1h_292"] = "HERO_05bp", ["CS2_083b"] = "HERO_03bp", ["CS2_056"] = "HERO_07bp", ["CS2_049"] = "HERO_02bp",
    ["AT_132_WARRIOR"] = "HERO_01bp2", ["AT_132_SHAMAN"] = "HERO_02bp2", ["AT_132_ROGUE"] = "HERO_03bp2", ["AT_132_PALADIN"] = "HERO_04bp2",
    ["AT_132_HUNTER"] = "HERO_05bp2", ["AT_132_DRUID"] = "HERO_06bp2", ["AT_132_WARLOCK"] = "HERO_07bp2", ["AT_132_MAGE"] = "HERO_08bp2", ["AT_132_PRIEST"] = "HERO_09bp2",
};
bool ClientKnows(string id) => clientIds.Contains(transport.TryGetValue(id, out var sent) ? sent : id);
string Swap(string json, string from, string to) { if (!json.Contains(from)) throw new Exception("fixture text not found: " + from); return json.Replace(from, to); }
string[] SigUnlockSuffix = { null, "_bosses5", "_floor5", "_win", "_majors2", "_majors3" };
{
    var errors = DungeonRunValidator.Validate(data, ClientKnows);
    Check("T2 shipped data validates with 0 errors (client check through the transported id)", errors.Count == 0, string.Join(" | ", errors.Take(5)));
    var legacy = DungeonRunData.Parse(runJson, poolJson, npcJson);
    var le = DungeonRunValidator.Validate(legacy, null);
    Check("T2 the data without the catalog validates with 0 errors and loads no F11 treasure and no signature unlock (their rows are catalog rows)",
        le.Count == 0 && ids.All(id => legacy.FindTreasure(id) == null) && !legacy.Unlocks.Keys.Any(k => k.Contains("_bosses5") || k.Contains("_majors")),
        string.Join(" | ", le.Take(3)));
    foreach (var e in explorers)
    {
        var row = data.Class(e.Row);
        string low = e.Row.ToLowerInvariant();
        var rows = loadout[e.ClassId].OrderBy(x => x.Sort).ToList();
        // Phase 6 F15 added ULDA_302 Academic Research (Elise's fifth): every explorer now has the client's six.
        var want = rows.Select(x => x.Card).ToList();
        Check($"T2 row {e.Row}: signatures [{string.Join(", ", want)}] in the client's sortOrder{(e.Row == "DRUID_PRIEST" ? " (ULDA_302 from family F15)" : "")}, three offered",
            row.Signatures.SequenceEqual(want) && row.SignatureOffer == 3, string.Join(",", row.Signatures));
        foreach (var x in rows)
        {
            var t = data.FindTreasure(x.Card);
            string unlock = SigUnlockSuffix[x.Sort] == null ? null : low + SigUnlockSuffix[x.Sort];
            Check($"T2 {x.Card} (sortOrder {x.Sort}): an active signature treasure of {e.Row} alone, never offered in a draft, pool Tombs, unlock {unlock ?? "none"}",
                t != null && t.Active && t.Signature && !t.Offered && t.Pool == "Tombs" && t.Scope.Classes.SequenceEqual(new[] { e.Row }) && t.Scope.Decks.Count == 0
                && t.Scope.Powers.Count == 0 && t.Unlock == unlock && (unlock == null || data.Unlocks.ContainsKey(unlock)), t?.Unlock);
        }
        var jr = data.FindTreasure(e.Jr); var sr = data.FindTreasure(e.Sr);
        Check($"T2 {e.Jr} upgrades to {e.Sr} by the profile unlock {low}_majors4 (4 act majors cleared with the explorer); {e.Sr} is listed only as that upgrade",
            jr.UpgradesTo == e.Sr && jr.UpgradeUnlock == low + "_majors4" && sr.Signature && !sr.Offered && sr.UpgradesTo == null && sr.Unlock == null && !row.Signatures.Contains(e.Sr)
            && data.Unlocks[low + "_majors4"].MajorsCleared == 4 && data.Unlocks[low + "_majors4"].Class == e.Row);
        Check($"T2 {e.Row}: unlock {low}_bosses5 is floor 6 (5 bosses beaten), {low}_majors2/3 are 2/3 act majors, all scoped to the row",
            data.Unlocks[low + "_bosses5"].FloorReached == 6 && data.Unlocks[low + "_majors2"].MajorsCleared == 2 && data.Unlocks[low + "_majors3"].MajorsCleared == 3
            && new[] { "_bosses5", "_majors2", "_majors3" }.All(k => data.Unlocks[low + k].Class == e.Row));
    }
    var mule = data.FindTreasure("ULDA_019"); var mule2 = data.FindTreasure("ULDA_024");
    var duals = explorers.SelectMany(e => data.Class(e.Row).Decks).ToArray();
    Check("T2 ULDA_019 Pack Mule: an offered active Tombs treasure of the four explorer rows (scoped by their 16 starters) on act runs, upgrading to ULDA_024 when an act is cleared (deck slot)",
        mule.Active && mule.Offered && !mule.Signature && mule.Pool == "Tombs" && mule.Runs == "act" && mule.Scope.Decks.SequenceEqual(duals) && duals.Length == 16 && mule.Scope.Classes.Count == 0 && mule.UpgradesTo == "ULDA_024"
        && mule.UpgradeOn == DungeonRunTreasure.UpgradeOnActClear && mule.UpgradeSlot == DungeonRunTreasure.SlotDeck && mule.UpgradeUnlock == null);
    Check("T2 ULDA_024 Upgraded Pack Mule: reached only by the upgrade (never offered), the same scope", mule2.Active && !mule2.Offered && mule2.UpgradesTo == null && mule2.Scope.Decks.SequenceEqual(duals));
    // Validator branches
    var e1 = DungeonRunValidator.Validate(Load(Swap(runJson, "\"id\": \"ULDA_203\", \"rarity\": \"rare\", \"active\": true, \"signature\": true, \"offered\": false,",
        "\"id\": \"ULDA_203\", \"rarity\": \"rare\", \"active\": true, \"signature\": true,")), null);
    Check("T2 validator: a signature treasure offered in drafts is refused", e1.Any(x => x.StartsWith("ULDA_203") && x.Contains("loadout only")), string.Join(" | ", e1.Take(3)));
    var e2 = DungeonRunValidator.Validate(Load(Swap(runJson, "\"pool\": \"Tombs\", \"scope\": {\"classes\": [\"MAGE_ROGUE\"]}}", "\"pool\": \"Tombs\", \"scope\": {\"classes\": [\"MAGE_ROGUE\", \"MAGE\"]}}")), null);
    Check("T2 validator: a signature scoped beyond its row is refused", e2.Any(x => x.Contains("must be scoped to [MAGE_ROGUE] alone")), string.Join(" | ", e2.Take(3)));
}
foreach (string id in ids)
{
    var p = DungeonRunAdmission.Classify(C(id), id, AdmitScope.Player, null, catalogRun: true);
    var b = DungeonRunAdmission.Classify(C(id), id, AdmitScope.Boss, null, catalogRun: true);
    string family = id == "ULDA_201" ? "run-treasure-hand-steal" : id == "ULDA_002" ? "run-treasure-discover" : id == "ULDA_016" || id == "ULDA_021" ? "secret-from-deck"
        : id == "ULDA_024" ? "deck-wide-change" : "run-treasure";
    bool bossOk = family == "secret-from-deck";
    Check($"T2 admission {id}: the {family} row admits the player{(bossOk ? " (any scope: the Secret count is the owner's own)" : ", never the boss; the boss's generation never yields it")}",
        p.Subject && p.Admitted && DungeonRunAdmission.RuleFor(id)?.Family == family && DungeonRunAdmission.Classify(C(id), id, AdmitScope.Player).Admitted
        && (bossOk || !b.Admitted && DungeonRunAdmission.GeneratedRejectsBoss(true).Contains(id)), p.Family + " " + p.Admit);
}
Check("T2 admission ULDA_302 is family F15's start-of-game-draw row, admitted for the player now that F15 scripts it (c-deferrals)", DungeonRunAdmission.Classify(C("ULDA_302"), "ULDA_302", AdmitScope.Player, null, true).Admitted
    && DungeonRunAdmission.RuleFor("ULDA_302")?.Family == "start-of-game-draw");

// ---------------------------------------------------------------- the run setup path
GameConfig Configured(RunMatchPlan plan, int seed)
{
    var cfg = new GameConfig { RandomSeed = seed, Shuffle = true, SkipMulligan = false, History = true, Logging = false, FormatType = FormatType.FT_WILD };
    DungeonRunSetup.Configure(cfg, plan);
    return cfg;
}
// The first seed at or after `from` whose signature offer for `profile` holds `sig`.
long SeedFor(DungeonRunProfile profile, string row, string sig, long from)
{
    var cls = data.Class(row);
    for (long seed = from; seed < from + 5000; seed++) if (run.SignatureOffer(profile, cls, seed).Contains(sig)) return seed;
    throw new Exception("no seed offers " + sig);
}
// A profile with every unlock but the Sr. upgrades (a Jr. signature starts as itself).
var juniors = new DungeonRunProfile(); juniors.Unlocked.AddRange(data.Unlocks.Keys.Where(k => !k.EndsWith("_majors4")));
// The picked signature (a Sr. id picks its Jr. row with the upgrade unlocked).
DungeonRunState NewAct(string row, string sig, long seed, string deck = null)
{
    var cls = data.Class(row);
    var jr = explorers.FirstOrDefault(e => e.Sr == sig);
    var profile = jr.Row != null ? all : juniors;
    string pick = jr.Row != null ? jr.Jr : sig;
    var s = run.NewRun(profile, row, deck ?? cls.DefaultDeck, cls.DefaultPower, SeedFor(profile, row, pick, seed), signatureId: pick);
    if (!s.IsActRun || s.Phase != DungeonRunPhase.PreMatch || s.Floor != 1) throw new Exception("act run not at floor 1 pre-match");
    return s;
}
Game StartFight(RunMatchPlan plan, int seed)
{
    var g = new Game(Configured(plan, seed));
    g.StartGame();
    DungeonRunSetup.ApplyAfterStart(g, plan);
    g.Process(ChooseTask.Mulligan(g.Player1, new List<int>()));
    g.Process(ChooseTask.Mulligan(g.Player2, new List<int>()));
    g.MainBegin();
    if (g.CurrentPlayer != g.Player1) throw new Exception("run player not first");
    return g;
}
void Mana(Controller c, int n) { c.BaseMana = n; c.UsedMana = 0; c.OverloadLocked = 0; c.OverloadOwed = 0; }
// The signature card the run dealt: the run deck card's entity (its slot kept), moved from the deck (or found in hand) to the hand.
IPlayable Dealt(Game g, string id)
{
    var c = g.Player1;
    var inHand = c.HandZone.FirstOrDefault(p => p.Card.Id == id);
    if (inHand != null) return inHand;
    var e = c.DeckZone.First(p => p.Card.Id == id);
    c.DeckZone.Remove(e);
    Generic.AddHandPhase.Invoke(c, e);
    g.DeathProcessingAndAuraUpdate();
    return e;
}
(DungeonRunState s, RunMatchPlan plan, Game g, IPlayable card) Fight(string sig, long seed, bool keepHand = false)
{
    string row = RowOf(sig);
    var s = NewAct(row, sig, seed);
    var plan = run.BuildPlan(s);
    var g = StartFight(plan, run.MatchSeed(s));
    var card = Dealt(g, sig);
    if (!keepHand) foreach (var e in g.Player1.HandZone.ToArray()) if (e != card) { g.Player1.HandZone.Remove(e); g.Player1.SetasideZone.Add(e); }
    if (RunHelpers.SlotOf(card) < 0) throw new Exception(sig + " is not a run deck card");
    return (s, plan, g, card);
}
IPlayable ToHand(Game g, Controller c, string id)
{
    var e = Entity.FromCard(c, C(id));
    Generic.AddHandPhase.Invoke(c, e);
    g.DeathProcessingAndAuraUpdate();
    return e;
}
Minion Board(Game g, Controller c, string id)
{
    var m = (Minion)Entity.FromCard(c, C(id));
    Generic.SummonBlock(g, m, -1, c.Hero);
    g.DeathProcessingAndAuraUpdate();
    return m;
}
bool Play(Game g, IPlayable card, ICharacter target = null) => g.Process(PlayCardTask.Any(g.CurrentPlayer, card, target));
bool CanPlayOn(Game g, IPlayable card, ICharacter target) => g.Player1.Options().OfType<PlayCardTask>().Any(o => o.Source == card && o.Target == target);
bool CanPlay(Game g, IPlayable card) => g.Player1.Options().OfType<PlayCardTask>().Any(o => o.Source == card);
bool HeroAttack(Game g, ICharacter target) => g.Process(HeroAttackTask.Any(g.Player1, target));
bool Pick(Game g, int id)
{
    var decision = g.GetPendingDecisions().FirstOrDefault(d => d.ControllerId == g.Player1.Id && d.Kind != SabberStoneCore.Model.Decisions.DecisionKind.Options);
    if (decision != null) return g.TryAnswer(new SabberStoneCore.Model.Decisions.DecisionAnswer(decision.Id, decision.ControllerId, new[] { id }), out _);
    return g.Process(ChooseTask.Pick(g.Player1, id));
}
void EndTurn(Game g) => g.Process(EndTurnTask.Any(g.CurrentPlayer));
void Clear(Controller c) { foreach (var e in c.HandZone.ToArray()) { c.HandZone.Remove(e); c.SetasideZone.Add(e); } }
void ClearBoards(Game g) { foreach (var c in new[] { g.Player1, g.Player2 }) foreach (var m in c.BoardZone.ToArray()) { c.BoardZone.Remove(m); c.SetasideZone.Add(m); } }
void NextTurn(Game g)
{
    EndTurn(g);
    if (g.CurrentPlayer == g.Player2) EndTurn(g);
    if (g.Player1.Choice != null) Pick(g, g.Player1.Choice.Choices[0]);
}
void Kill(Game g, Minion m) { m.Destroy(); g.DeathProcessingAndAuraUpdate(); }

// ---------------------------------------------------------------- T3 the loadout: offer, pick, Jr./Sr., the run deck
foreach (var e in explorers)
{
    var cls = data.Class(e.Row);
    var fresh = new DungeonRunProfile();
    var first = loadout[e.ClassId].Single(x => x.Sort == 0).Card;
    Check($"T3 {e.Row}: a new profile is offered only {first} (the client's default, no unlock)", run.SignatureOffer(fresh, cls, 1).SequenceEqual(new[] { first })
        && Enumerable.Range(2, 20).All(sd => run.SignatureOffer(fresh, cls, sd).SequenceEqual(new[] { first })));
    bool Refused(Func<DungeonRunState> f, string msg) { try { f(); return false; } catch (InvalidOperationException x) { return x.Message == msg; } }
    Check($"T3 {e.Row}: NewRun needs a signature, and refuses one not on this seed's offer",
        Refused(() => run.NewRun(fresh, e.Row, cls.DefaultDeck, cls.DefaultPower, 3), DungeonRun.ChooseSignature)
        && Refused(() => run.NewRun(fresh, e.Row, cls.DefaultDeck, cls.DefaultPower, 3, signatureId: cls.Signatures[1]), DungeonRun.NotOffered));
    var offers = Enumerable.Range(0, 40).Select(sd => run.SignatureOffer(juniors, cls, 900 + sd)).ToList();
    Check($"T3 {e.Row}: with every signature unlocked, each offer is three distinct of the row's signatures, the same for the same seed, varied by seed, every one offered",
        offers.All(o => o.Count == 3 && o.Distinct().Count() == 3 && o.All(cls.Signatures.Contains)) && run.SignatureOffer(juniors, cls, 900).SequenceEqual(offers[0])
        && offers.Select(o => string.Join(",", o)).Distinct().Count() > 3 && cls.Signatures.All(sg => offers.Any(o => o.Contains(sg))));
    var partial = new DungeonRunProfile(); partial.Heroes.Add(new DungeonRunHeroStats { Class = e.Row, BestFloor = 6 }); run.RefreshUnlocks(partial);
    var po = Enumerable.Range(0, 30).SelectMany(sd => run.SignatureOffer(partial, cls, sd)).Distinct().OrderBy(x => x).ToList();
    var sorts = loadout[e.ClassId].Where(x => x.Sort <= 2 && x.Card != "ULDA_302").Select(x => x.Card).OrderBy(x => x).ToList();
    Check($"T3 {e.Row}: reaching floor 6 with the explorer unlocks the client's sortOrder 1 and 2 signatures (5 bosses, a Plague Lord reached); the rest need act majors",
        po.SequenceEqual(sorts), string.Join(",", po));
    foreach (string sig in cls.Signatures)
    {
        long seed = SeedFor(juniors, e.Row, sig, 100);
        var s = run.NewRun(juniors, e.Row, cls.DefaultDeck, cls.DefaultPower, seed, signatureId: sig);
        var plan = run.BuildPlan(s);
        var sigCards = s.Cards.Where(x => x.Origin == DungeonRunDeck.OriginSignature).ToList();
        Check($"T3 NewRun {e.Row} with {sig}: the loadout names it, its card record (origin signature) joins the ten starters, the plan deals it",
            s.Loadout?.Id == e.Row && s.Loadout.Signature.SequenceEqual(new[] { sig }) && s.ActiveTreasures.SequenceEqual(new[] { sig }) && sigCards.Count == 1 && sigCards[0].Id == sig
            && s.Cards.Count(x => x.Origin == DungeonRunDeck.OriginStarter) == 10 && s.Deck.Count == 11 && plan.PlayerDeck.Count(c => c.Id == sig) == 1 && DungeonRunDeck.MirrorError(s) == null
            && s.Upgrades.Count == 0, string.Join(",", s.Deck));
    }
    long sseed = SeedFor(all, e.Row, e.Jr, 300);
    var sr = run.NewRun(all, e.Row, cls.DefaultDeck, cls.DefaultPower, sseed, signatureId: e.Jr);
    var jrRun = run.NewRun(juniors, e.Row, cls.DefaultDeck, cls.DefaultPower, SeedFor(juniors, e.Row, e.Jr, 300), signatureId: e.Jr);
    Check($"T3 {e.Jr} picked by a profile with {e.Row.ToLowerInvariant()}_majors4: the run starts with {e.Sr} (the Sr. upgrade); without it, with {e.Jr}; neither upgrades in the run",
        sr.Loadout.Signature.SequenceEqual(new[] { e.Sr }) && sr.Deck.Contains(e.Sr) && !sr.Deck.Contains(e.Jr) && sr.ActiveTreasures.SequenceEqual(new[] { e.Sr })
        && jrRun.Deck.Contains(e.Jr) && !jrRun.Deck.Contains(e.Sr) && sr.Upgrades.Count == 0 && jrRun.Upgrades.Count == 0
        && DungeonRunCatalog.Signatures(run, all, e.Row, sseed).Single(o => o.Id == e.Jr).StartsAs == e.Sr
        && DungeonRunCatalog.Signatures(run, all, e.Row, sseed).Single(o => o.Id == e.Jr).Name == FactName(e.Sr));
    var winner = new DungeonRunProfile(); winner.Heroes.Add(new DungeonRunHeroStats { Class = e.Row, MajorsCleared = 4, BestFloor = 15, Wins = 1 }); run.RefreshUnlocks(winner);
    Check($"T3 {e.Row}: 4 act majors cleared with the explorer unlock every signature and the Sr. upgrade", winner.Unlocked.Contains(e.Row.ToLowerInvariant() + "_majors4")
        && cls.Signatures.All(sg => run.IsUnlocked(winner, data.FindTreasure(sg).Unlock)));
    var ser = new System.Xml.Serialization.XmlSerializer(typeof(DungeonRunState));
    var w = new StringWriter(); ser.Serialize(w, sr);
    var back = (DungeonRunState)ser.Deserialize(new StringReader(w.ToString()));
    Check($"T3 {e.Row}: a save with the signature round-trips (loadout, card record origin, deck)", back.Loadout?.Signature.SequenceEqual(sr.Loadout.Signature) == true
        && back.Cards.Select(x => x.Origin + ":" + x.Id).SequenceEqual(sr.Cards.Select(x => x.Origin + ":" + x.Id)) && DungeonRunDeck.MirrorError(back) == null);
    var opts = DungeonRunCatalog.Signatures(run, juniors, e.Row, 900);
    Check($"T3 picker data: Signatures({e.Row}) lists this seed's offer, each named and texted as the client prints it, shown as its own card",
        opts.Select(o => o.Id).SequenceEqual(run.SignatureOffer(juniors, cls, 900)) && opts.All(o => o.Name == FactName(o.Id) && o.Text == FactText(o.Id) && o.CardId == o.Id));
}

// ---------------------------------------------------------------- T4 ULDA_203 Reno's Lucky Hat
{
    var (s, plan, g, hat) = Fight("ULDA_203", 1);
    var p1 = g.Player1; var p2 = g.Player2; Mana(p1, 10); ClearBoards(g);
    var yeti = Board(g, p1, "CS2_182"); var boss = Board(g, p2, "CS2_182");
    Check("T4 Lucky Hat targets a minion of either side, never a hero", CanPlayOn(g, hat, yeti) && CanPlayOn(g, hat, boss) && !CanPlayOn(g, hat, p2.Hero) && !CanPlayOn(g, hat, p1.Hero));
    int sp = p1.CurrentSpellPower;
    Play(g, hat, yeti);
    Check("T4 Lucky Hat: +2/+2 and Spell Damage +2 (Reno's Lucky Hat, ULDA_203e)", yeti.AttackDamage == 6 && yeti.Health == 7 && Has(yeti, "ULDA_203e") && p1.CurrentSpellPower == sp + 2,
        $"{yeti.AttackDamage}/{yeti.Health} sp {p1.CurrentSpellPower}");
    int hp = p2.Hero.Health;
    Play(g, ToHand(g, p1, "CS2_008"), p2.Hero);
    Check("T4 a Moonfire now deals 3", p2.Hero.Health == hp - 3, $"{hp}->{p2.Hero.Health}");
    Kill(g, yeti);
    Check("T4 the Spell Damage goes with the minion", p1.CurrentSpellPower == sp);
}

// ---------------------------------------------------------------- T5 ULDA_201 Reno's Crafty Lasso
{
    var (s, plan, g, lasso) = Fight("ULDA_201", 2);
    var p1 = g.Player1; var p2 = g.Player2; Mana(p1, 10); ClearBoards(g);
    Play(g, lasso);
    Check("T5 Crafty Lasso: a 3/2 weapon", p1.Hero.Weapon?.Card.Id == "ULDA_201" && p1.Hero.AttackDamage == 3 && p1.Hero.Weapon.Durability == 2);
    int bossHand = p2.HandZone.Count, mine = p1.HandZone.Count;
    var before = p2.HandZone.Select(x => x.Id).ToHashSet();
    HeroAttack(g, p2.Hero);
    var stolen = p1.HandZone.FirstOrDefault(x => before.Contains(x.Id));
    Check("T5 after the hero attacks, one random card leaves the boss's hand for the player's (now the player's card)", bossHand > 0 && p2.HandZone.Count == bossHand - 1
        && p1.HandZone.Count == mine + 1 && stolen != null && stolen.Controller == p1, $"{bossHand}->{p2.HandZone.Count} {mine}->{p1.HandZone.Count}");
    NextTurn(g); Mana(p1, 10);
    Clear(p2);
    mine = p1.HandZone.Count;
    HeroAttack(g, p2.Hero);
    Check("T5 an empty boss hand: the attack steals nothing, and the Lasso breaks after its second attack", p1.HandZone.Count == mine && p1.Hero.Weapon == null);
    var (_, _, g2, l2) = Fight("ULDA_201", 2);
    Mana(g2.Player1, 10); ClearBoards(g2); Play(g2, l2); HeroAttack(g2, g2.Player2.Hero);
    Check("T5 the same seed steals the same card (game RNG)", g2.Player1.HandZone.Select(x => x.Card.Id).SequenceEqual(g.Player1.HandZone.Take(g2.Player1.HandZone.Count).Select(x => x.Card.Id))
        || g2.Player1.HandZone.Last().Card.Id == stolen.Card.Id, g2.Player1.HandZone.Last().Card.Id + " vs " + stolen.Card.Id);
}

// ---------------------------------------------------------------- T6 ULDA_204 Reno's Magical Torch (self-modifying)
{
    var (s, plan, g, torch) = Fight("ULDA_204", 3);
    var p1 = g.Player1; var p2 = g.Player2; Mana(p1, 10); ClearBoards(g);
    int hp = p2.Hero.Health, deck = p1.DeckZone.Count;
    Check("T6 Magical Torch needs a target (any character)", !CanPlay(g, torch) || CanPlayOn(g, torch, p2.Hero) && CanPlayOn(g, torch, p1.Hero) && !g.Player1.Options().OfType<PlayCardTask>().Any(o => o.Source == torch && o.Target == null));
    Play(g, torch, p2.Hero);
    Check("T6 Magical Torch without a Combo: 4 damage, nothing shuffled", p2.Hero.Health == hp - 4 && p1.DeckZone.Count == deck, $"{hp}->{p2.Hero.Health} deck {deck}->{p1.DeckZone.Count}");
    NextTurn(g); Mana(p1, 10);
    var t2 = ToHand(g, p1, "ULDA_204");
    Play(g, ToHand(g, p1, "GAME_005"));
    hp = p2.Hero.Health; deck = p1.DeckZone.Count;
    Play(g, t2, p2.Hero);
    var copy = p1.DeckZone.SingleOrDefault(x => x.Card.Id == "ULDA_204");
    Check("T6 with a Combo: 4 damage, and a copy that deals 6 is shuffled into the deck", p2.Hero.Health == hp - 4 && p1.DeckZone.Count == deck + 1 && copy != null
        && DungeonRunContent.MagicalTorchAmount(copy) == 6 && copy[GameTag.TAG_SCRIPT_DATA_NUM_1] == 6, $"{hp}->{p2.Hero.Health} copy {copy?[GameTag.TAG_SCRIPT_DATA_NUM_1]}");
    p1.DeckZone.Remove(copy); Generic.AddHandPhase.Invoke(p1, copy);
    Play(g, ToHand(g, p1, "GAME_005"));
    hp = p2.Hero.Health;
    Play(g, copy, p2.Hero);
    var copy2 = p1.DeckZone.SingleOrDefault(x => x.Card.Id == "ULDA_204");
    Check("T6 the copy deals 6 and, with a Combo, shuffles one that deals 8", p2.Hero.Health == hp - 6 && copy2 != null && copy2[GameTag.TAG_SCRIPT_DATA_NUM_1] == 8, $"{hp}->{p2.Hero.Health}");
    NextTurn(g); Mana(p1, 10);
    var ally = Board(g, p1, "CS2_197");
    hp = p2.Hero.Health;
    Play(g, ToHand(g, p1, "ULDA_204"), p2.Hero);
    Check("T6 Spell Damage adds to it (Ogre Magi +1: 5)", p2.Hero.Health == hp - 5, $"{hp}->{p2.Hero.Health}");
}

// ---------------------------------------------------------------- T7 ULDA_016 Jr. Tomb Diver / ULDA_021 Sr. Tomb Diver
foreach (var (id, need) in new[] { ("ULDA_016", 6), ("ULDA_021", 3) })
{
    var (s, plan, g, diver) = Fight(id, 4);
    var p1 = g.Player1; Mana(p1, 10); ClearBoards(g);
    string power = p1.Hero.HeroPower.Card.Id;
    p1.NumSecretsPlayedThisGame = need - 1;
    Play(g, diver);
    Check($"T7 {id} with {need - 1} Secrets played: a {C(id).ATK}/{C(id).Health}, the power unchanged", p1.BoardZone.Any(m => m.Card.Id == id) && p1.Hero.HeroPower.Card.Id == power);
    var d2 = ToHand(g, p1, id);
    p1.NumSecretsPlayedThisGame = need;
    Play(g, d2);
    Check($"T7 {id} with {need} Secrets played: the power becomes Uldum Treasure Cache (ULDA_039)", p1.Hero.HeroPower.Card.Id == "ULDA_039");
    NextTurn(g); Mana(p1, 10); Clear(p1);
    g.Process(HeroPowerTask.Any(p1));
    var offers = p1.Choice?.Choices.Select(x => g.IdEntityDic[x].Card.Id).ToList() ?? new List<string>();
    var pool = run.TreasurePool(s);
    // Phase 6 F12: the Tombs actives joined the explorers' pool, so the Cache discovers Tombs treasures (25 of them).
    var tombs = run.TreasurePool(s, "Tombs");
    Check($"T7 {id}: the new power discovers three distinct treasures of the run's Tombs pool (25 since family F12, 28 since F15)",
        offers.Count == 3 && offers.Distinct().Count() == 3 && offers.All(tombs.Contains) && tombs.Count == 28 && !offers.Any(o => data.FindTreasure(o)?.Signature == true), string.Join(",", offers));
}

// ---------------------------------------------------------------- T8 ULDA_206 Lei Flamepaw (recast on the same target)
{
    var (s, plan, g, lei) = Fight("ULDA_206", 5);
    var p1 = g.Player1; var p2 = g.Player2; Mana(p1, 10); ClearBoards(g);
    Play(g, lei);
    Check("T8 Lei Flamepaw: a 2/6", p1.BoardZone.Any(m => m == lei) && ((Minion)lei).AttackDamage == 2 && ((Minion)lei).Health == 6);
    int hp = p2.Hero.Health;
    Play(g, ToHand(g, p1, "CS2_008"), p2.Hero);
    Check("T8 a Moonfire at the boss hero is cast again on it: 2 damage", p2.Hero.Health == hp - 2, $"{hp}->{p2.Hero.Health}");
    var yeti = Board(g, p2, "CS2_182");
    Play(g, ToHand(g, p1, "CS2_008"), yeti);
    Check("T8 a Moonfire at a 4/5 is cast again on it: 2 damage", yeti.Health == 3, yeti.Health.ToString());
    var raider = Board(g, p2, "CS2_122"); // Raid Leader 2/2
    hp = p2.Hero.Health;
    Play(g, ToHand(g, p1, "CS2_029"), raider); // Fireball
    Check("T8 a Fireball that kills its target is not cast again (the target is gone): the boss hero is untouched", raider.Zone?.Type != Zone.PLAY && p2.Hero.Health == hp, $"hero {hp}->{p2.Hero.Health}");
    var others = new[] { Board(g, p2, "CS2_168"), Board(g, p2, "CS2_168") }; // two 2/1s
    Play(g, ToHand(g, p1, "CS2_025")); // Arcane Explosion: 1 to all enemy minions
    Check("T8 an untargeted Arcane Explosion is cast again: the 2/1s die, the 4/5 is at 1", others.All(m => m.Zone?.Type != Zone.PLAY) && yeti.Health == 1, yeti.Health.ToString());
    NextTurn(g); Mana(p1, 10); ClearBoards(g);
    var a = Board(g, p1, "ULDA_206"); var b = Board(g, p1, "ULDA_206");
    hp = p2.Hero.Health;
    Play(g, ToHand(g, p1, "CS2_008"), p2.Hero);
    Check("T8 two Leis each cast the original again, never each other's copy: 3 Moonfires", p2.Hero.Health == hp - 3, $"{hp}->{p2.Hero.Health}");
    var nav = Board(g, p1, "ULDA_017");
    Clear(p1);
    Play(g, ToHand(g, p1, "CS2_004"), nav); // Power Word: Shield
    Check("T8 with two Leis, a Power Word: Shield on Jr. Navigator is cast three times on it (+6 Health); the two recasts are effect casts, which (the engine's rule for every cast by an effect) are not the player targeting it, so Navigator adds two copies, for the played one",
        nav.Health == 5 + 6 && p1.HandZone.Count(x => x.Card.Id == "CS2_004") == 2, $"{nav.Health} {p1.HandZone.Count(x => x.Card.Id == "CS2_004")}");
}

// ---------------------------------------------------------------- T9 ULDA_207 The Gatling Wand (improves with spells cast)
{
    var (s, plan, g, wand) = Fight("ULDA_207", 6);
    var p1 = g.Player1; var p2 = g.Player2; Mana(p1, 10); ClearBoards(g);
    Check("T9 The Gatling Wand starts at 3 (no spell cast this game)", DungeonRunContent.GatlingWandAmount(p1) == 3 + p1.NumSpellsPlayedThisGame && wand[GameTag.TAG_SCRIPT_DATA_NUM_1] == 3);
    Play(g, ToHand(g, p1, "GAME_005"));
    Play(g, ToHand(g, p1, "GAME_005"));
    Check("T9 after two spells it shows 5 in hand", wand[GameTag.TAG_SCRIPT_DATA_NUM_1] == 5 && DungeonRunContent.GatlingWandAmount(p1) == 5, wand[GameTag.TAG_SCRIPT_DATA_NUM_1].ToString());
    int hp = p2.Hero.Health;
    Play(g, wand);
    Check("T9 with no enemy minion all 5 hit the boss hero", p2.Hero.Health == hp - 5, $"{hp}->{p2.Hero.Health}");
    var w2 = ToHand(g, p1, "ULDA_207");
    var yeti = Board(g, p2, "CS2_182");
    int total = p2.Hero.Health + yeti.Health;
    Play(g, w2);
    int dealt = total - p2.Hero.Health - yeti.Health;
    Check("T9 three spells later it deals 6, split among all enemies", dealt == 6 && DungeonRunContent.GatlingWandAmount(p1) == 7, dealt.ToString());
}

// ---------------------------------------------------------------- T10 ULDA_502 Finley's Pith Helmet
{
    var (s, plan, g, helmet) = Fight("ULDA_502", 7);
    var p1 = g.Player1; Mana(p1, 10); ClearBoards(g);
    var a = Board(g, p1, "CS2_182"); var b = Board(g, p1, "CS2_168"); var boss = Board(g, g.Player2, "CS2_182");
    int deck = p1.DeckZone.Count;
    Play(g, helmet);
    Check("T10 Pith Helmet: +2 Health to every friendly minion (Desert Explorer), not the boss's", a.Health == 7 && b.Health == 3 && boss.Health == 5 && Has(a, "ULDA_502e"));
    Check("T10 Pith Helmet shuffles a copy of itself into the deck", p1.DeckZone.Count == deck + 1 && p1.DeckZone.Count(x => x.Card.Id == "ULDA_502") == 1);
}

// ---------------------------------------------------------------- T11 ULDA_501 Maxwell, Mighty Steed
{
    var (s, plan, g, max) = Fight("ULDA_501", 8);
    var p1 = g.Player1; var p2 = g.Player2; Mana(p1, 10); ClearBoards(g);
    Board(g, p1, "CS2_168"); var boss = Board(g, p2, "CS2_182"); Board(g, p2, "CS2_168");
    Play(g, max);
    var m = (Minion)max;
    Check("T11 Maxwell: Rush and Reborn; +2 Attack for each other minion on the battlefield (3 others: 9)", m.IsRush && m[GameTag.REBORN] == 1 && m.AttackDamage == 3 + 6 && m.Health == 5, m.AttackDamage.ToString());
    Check("T11 Rush: it may attack a minion the turn it is played, not the hero", g.Player1.Options().OfType<MinionAttackTask>().Any(o => o.Source == m && o.Target == boss)
        && !g.Player1.Options().OfType<MinionAttackTask>().Any(o => o.Source == m && o.Target == p2.Hero));
    Kill(g, boss);
    Check("T11 one fewer minion: 7 Attack", m.AttackDamage == 7, m.AttackDamage.ToString());
    Kill(g, m);
    var reborn = p1.BoardZone.SingleOrDefault(x => x.Card.Id == "ULDA_501");
    Check("T11 Reborn: it comes back at 1 Health, its aura counts again (2 others: 7)", reborn != null && reborn.Health == 1 && reborn.AttackDamage == 7, reborn?.AttackDamage + "/" + reborn?.Health);
}

// ---------------------------------------------------------------- T12 ULDA_505 Truesilver Lance
{
    var (s, plan, g, lance) = Fight("ULDA_505", 9);
    var p1 = g.Player1; var p2 = g.Player2; Mana(p1, 10); ClearBoards(g);
    Play(g, lance);
    var w = p1.Hero.Weapon;
    Check("T12 Truesilver Lance: a 5/1 weapon with Lifesteal", w?.Card.Id == "ULDA_505" && p1.Hero.AttackDamage == 5 && w.Durability == 1 && w.HasLifeSteal);
    Play(g, ToHand(g, p1, "EX1_238"), p2.Hero); // Lightning Bolt, Overload (1)
    Check("T12 after the player Overloads it gains +2 Durability (Mighty!)", w.Durability == 3 && Has(w, "ULDA_505e"), w.Durability + " ench " + Has(w, "ULDA_505e") + " owed " + p1.OverloadOwed + " bolt overload " + C("EX1_238").HasOverload);
    p1.Hero.Damage = 10;
    int hp = p1.Hero.Health;
    HeroAttack(g, p2.Hero);
    Check("T12 Lifesteal: the hero heals 5 when it hits", p1.Hero.Health == hp + 5 && w.Durability == 2, $"{hp}->{p1.Hero.Health}");
}

// ---------------------------------------------------------------- T13 ULDA_015 Jr. Scout / ULDA_020 Sr. Scout
foreach (var (id, dmg) in new[] { ("ULDA_015", 4), ("ULDA_020", 6) })
{
    var (s, plan, g, scout) = Fight(id, 10);
    var p1 = g.Player1; var p2 = g.Player2; Mana(p1, 10); ClearBoards(g);
    Play(g, scout);
    var m = (Minion)scout;
    Check($"T13 {id}: Charge and Windfury, it attacks the hero twice at once", m.HasCharge && m.HasWindfury && g.Player1.Options().OfType<MinionAttackTask>().Any(o => o.Source == m && o.Target == p2.Hero));
    var big = Board(g, p2, "CS2_200"); // Boulderfist Ogre 6/7
    int hp = p2.Hero.Health;
    EndTurn(g);
    Check($"T13 {id}: at the end of the owner's turn {dmg} damage to a random enemy minion, not the hero", big.Damage == dmg && p2.Hero.Health == hp, big.Damage.ToString());
    int bigDamage = big.Damage;
    EndTurn(g);
    Check($"T13 {id}: nothing at the end of the boss's turn", big.Zone?.Type != Zone.PLAY || big.Damage == bigDamage);
}

// ---------------------------------------------------------------- T14 ULDA_507 Karl the Lost
{
    var (s, plan, g, karl) = Fight("ULDA_507", 11);
    var p1 = g.Player1; Mana(p1, 10); ClearBoards(g);
    Play(g, karl);
    Check("T14 Karl the Lost: six 1/1 Silver Hand Recruits, every friendly minion (Karl too) with Taunt and Divine Shield",
        p1.BoardZone.Count == 7 && p1.BoardZone.Count(m => m.Card.Id == "CS2_101t") == 6 && p1.BoardZone.All(m => m.HasTaunt && m.HasDivineShield));
    var (_, _, g2, k2) = Fight("ULDA_507", 11);
    Mana(g2.Player1, 10); ClearBoards(g2);
    var y = Board(g2, g2.Player1, "CS2_182"); Board(g2, g2.Player1, "CS2_182"); Board(g2, g2.Player1, "CS2_182");
    var boss = Board(g2, g2.Player2, "CS2_182");
    Play(g2, k2);
    Check("T14 with three minions on the board, three Recruits fill it; the boss's minion gets nothing", g2.Player1.BoardZone.Count == 7 && g2.Player1.BoardZone.Count(m => m.Card.Id == "CS2_101t") == 3
        && y.HasTaunt && y.HasDivineShield && !boss.HasTaunt && !boss.HasDivineShield, string.Join(",", g2.Player1.BoardZone.Select(m => m.Card.Id + (m.HasTaunt ? "T" : "") + (m.HasDivineShield ? "D" : ""))) + " boss " + boss.HasTaunt + boss.HasDivineShield);
}

// ---------------------------------------------------------------- T15 ULDA_010 Scales of Justice
{
    var (s, plan, g, scales) = Fight("ULDA_010", 12);
    var p1 = g.Player1; var p2 = g.Player2; Mana(p1, 10); ClearBoards(g);
    Board(g, p1, "CS2_182"); Board(g, p2, "CS2_200"); Board(g, p2, "CS2_168");
    Play(g, scales);
    Check("T15 Scales of Justice: every minion of both sides is a 1/1 Murloc Scout", p1.BoardZone.Count == 1 && p2.BoardZone.Count == 2
        && p1.BoardZone.Concat(p2.BoardZone).All(m => m.Card.Id == "EX1_506a" && m.AttackDamage == 1 && m.Health == 1));
    Check("T15 the hand fills (10) with random scripted Murlocs that cost (0)", p1.HandZone.Count == 10 && p1.HandZone.All(x => x.Card.Type == CardType.MINION && x.Card.IsRace(Race.MURLOC)
        && x.Card.Implemented && x.Cost == 0 && Has(x, "LOOT_998le")), string.Join(",", p1.HandZone.Select(x => x.Card.Id + ":" + x.Cost)));
}

// ---------------------------------------------------------------- T16 ULDA_303 Starseeker's Tools
{
    var (s, plan, g, tools) = Fight("ULDA_303", 13);
    var p1 = g.Player1; Mana(p1, 10); ClearBoards(g);
    Play(g, tools);
    var first = p1.Choice;
    var firstOffers = first?.Choices.Select(x => g.IdEntityDic[x].Card).ToList() ?? new List<Card>();
    Check("T16 Starseeker's Tools: Discover three distinct scripted Druid, Priest or neutral minions first", firstOffers.Count == 3 && firstOffers.Select(c => c.Id).Distinct().Count() == 3
        && firstOffers.All(c => c.Type == CardType.MINION && c.Implemented && (c.Class == CardClass.DRUID || c.Class == CardClass.PRIEST || c.Class == CardClass.NEUTRAL)), string.Join(",", firstOffers.Select(c => c.Id)));
    var minion = firstOffers[0];
    Pick(g, first.Choices[0]);
    var second = p1.Choice;
    var spellOffers = second?.Choices.Select(x => g.IdEntityDic[x].Card).ToList() ?? new List<Card>();
    // Phase 6 review: Elise's row is DRUID_PRIEST, so her class Discovers offer both classes (RunHelpers.RunPlayerClasses).
    Check("T16 then three distinct scripted Druid, Priest or neutral spells", spellOffers.Count == 3 && spellOffers.All(c => c.Type == CardType.SPELL && c.Implemented && (c.Class == CardClass.DRUID || c.Class == CardClass.PRIEST || c.Class == CardClass.NEUTRAL)),
        string.Join(",", spellOffers.Select(c => c.Id)));
    var spell = spellOffers[1];
    Pick(g, second.Choices[1]);
    var gotMinion = p1.HandZone.FirstOrDefault(x => x.Card.Id == minion.Id); var gotSpell = p1.HandZone.FirstOrDefault(x => x.Card.Id == spell.Id);
    Check("T16 both chosen cards are in hand and cost (2) less (Planned)", gotMinion != null && gotSpell != null && Has(gotMinion, "ULDA_303e") && Has(gotSpell, "ULDA_303e")
        && gotMinion.Cost == Math.Max(0, minion.Cost - 2) && gotSpell.Cost == Math.Max(0, spell.Cost - 2) && p1.Choice == null, $"{gotMinion?.Cost} {gotSpell?.Cost}");
}

// ---------------------------------------------------------------- T17 ULDA_305 Elise's Machete
{
    var (s, plan, g, machete) = Fight("ULDA_305", 14);
    var p1 = g.Player1; var p2 = g.Player2; Mana(p1, 10); ClearBoards(g);
    var boss = Board(g, p2, "CS2_168");
    Play(g, machete);
    Check("T17 Elise's Machete: a 3/3 weapon", p1.Hero.Weapon?.Card.Id == "ULDA_305" && p1.Hero.AttackDamage == 3 && p1.Hero.Weapon.Durability == 3);
    HeroAttack(g, p2.Hero);
    var treants = p1.BoardZone.Where(m => m.Card.Id == "EX1_158t").ToList();
    Check("T17 after the hero attacks: two 2/2 Treants with Rush (Reaving), which may attack the boss's minion at once", treants.Count == 2
        && treants.All(t => t.AttackDamage == 2 && t.Health == 2 && t.IsRush && Has(t, "DAL_070e")) && g.Player1.Options().OfType<MinionAttackTask>().Count(o => treants.Contains(o.Source) && o.Target == boss) == 2);
}

// ---------------------------------------------------------------- T18 ULDA_003 Addarah
{
    var (s, plan, g, addarah) = Fight("ULDA_003", 15);
    var p1 = g.Player1; var p2 = g.Player2; Mana(p1, 10); ClearBoards(g);
    var a = Board(g, p2, "CS2_182"); var b = Board(g, p2, "FP1_007"); // Nerubian Egg: its Deathrattle must not fire
    int deck = p1.DeckZone.Count, bossDeck = p2.DeckZone.Count;
    Play(g, addarah);
    Check("T18 Addarah: every enemy minion leaves the board without dying (no Deathrattle) and is shuffled into the player's deck as the player's",
        p2.BoardZone.Count == 0 && p1.DeckZone.Count == deck + 2 && p2.DeckZone.Count == bossDeck && new[] { a, b }.All(m => m.Zone == p1.DeckZone && m.Controller == p1)
        && !p2.BoardZone.Any(m => m.Card.Id == "FP1_007t") && !p2.GraveyardZone.Contains(a), $"deck {deck}->{p1.DeckZone.Count}");
    Check("T18 Addarah itself stays: a 3/6", p1.BoardZone.Single().Card.Id == "ULDA_003" && p1.BoardZone.Single().Health == 6);
}

// ---------------------------------------------------------------- T19 ULDA_017 Jr. Navigator / ULDA_022 Sr. Navigator
foreach (string id in new[] { "ULDA_017", "ULDA_022" })
{
    var (s, plan, g, nav) = Fight(id, 16);
    var p1 = g.Player1; var p2 = g.Player2; Mana(p1, 10); ClearBoards(g);
    Play(g, nav);
    var m = (Minion)nav;
    Clear(p1);
    Play(g, ToHand(g, p1, "CS2_004"), m);
    Check($"T19 {id}: a spell its owner targets it with adds two copies to the hand", p1.HandZone.Count(x => x.Card.Id == "CS2_004") == 2 && m.Health == C(id).Health + 2);
    Play(g, ToHand(g, p1, "CS2_008"), p2.Hero);
    Check($"T19 {id}: a spell aimed elsewhere adds nothing", p1.HandZone.Count(x => x.Card.Id == "CS2_008") == 0);
    EndTurn(g); Mana(p2, 10);
    g.Process(PlayCardTask.Any(p2, ToHand(g, p2, "CS2_008"), m));
    Check($"T19 {id}: the boss's spell on it adds nothing to either hand", p1.HandZone.Count(x => x.Card.Id == "CS2_008") == 0 && p2.HandZone.Count(x => x.Card.Id == "CS2_008") == 0);
}

// ---------------------------------------------------------------- T20 ULDA_304 Staff of Renewal
{
    var (s, plan, g, staff) = Fight("ULDA_304", 17);
    var p1 = g.Player1; Mana(p1, 10); ClearBoards(g);
    string[] dead = { "CS2_200", "CS2_200", "CS2_182", "CS2_182", "CS2_182", "CS2_168", "CS2_168", "CS2_168", "CS2_189" }; // 6,6,4,4,4,1,1,1,1
    foreach (string id in dead) Kill(g, Board(g, p1, id));
    Kill(g, Board(g, g.Player2, "EX1_543"));
    Play(g, staff);
    var back = p1.BoardZone.Select(m => m.Card.Id).ToList();
    Check("T20 Staff of Renewal: the seven friendly dead minions with the highest Cost come back (two 6s, three 4s, two of the four 1s), each with Taunt; the boss's King Krush stays dead",
        back.Count == 7 && back.Count(x => x == "CS2_200") == 2 && back.Count(x => x == "CS2_182") == 3 && back.Count(x => x == "CS2_168" || x == "CS2_189") == 2
        && p1.BoardZone.All(m => m.HasTaunt && Has(m, "AT_069e") && m.Health == m.Card.Health) && !back.Contains("EX1_543"), string.Join(",", back));
    var (_, _, g2, st2) = Fight("ULDA_304", 17);
    Mana(g2.Player1, 10); ClearBoards(g2);
    foreach (string id in dead) Kill(g2, Board(g2, g2.Player1, id));
    Play(g2, st2);
    Check("T20 the same seed breaks the tie the same way (game RNG)", g2.Player1.BoardZone.Select(m => m.Card.Id).SequenceEqual(back));
}

// ---------------------------------------------------------------- T21 ULDA_401 Ol' Faithful
{
    var (s, plan, g, faithful) = Fight("ULDA_401", 18);
    var p1 = g.Player1; var p2 = g.Player2; Mana(p1, 10); ClearBoards(g);
    var a = Board(g, p2, "CS2_182"); var b = Board(g, p2, "CS2_182"); var mine = Board(g, p1, "CS2_182");
    Play(g, faithful);
    Check("T21 Ol' Faithful: a 4/2 weapon", p1.Hero.Weapon?.Card.Id == "ULDA_401" && p1.Hero.AttackDamage == 4 && p1.Hero.Weapon.Durability == 2);
    int hp = p2.Hero.Health;
    HeroAttack(g, p2.Hero);
    Check("T21 after the hero attacks: 1 damage to every enemy (the minions, the boss hero), none to the player's minion", a.Health == 4 && b.Health == 4 && p2.Hero.Health == hp - 4 - 1 && mine.Health == 5,
        $"{a.Health} {b.Health} {p2.Hero.Health}");
}

// ---------------------------------------------------------------- T22 ULDA_402 Brann's Saddle (the Deathrattle moves on)
{
    var (s, plan, g, saddle) = Fight("ULDA_402", 19);
    var p1 = g.Player1; var p2 = g.Player2; Mana(p1, 10); ClearBoards(g);
    var raptor = Board(g, p1, "CS2_172"); var cat = Board(g, p1, "CS2_120"); var yeti = Board(g, p1, "CS2_182"); var bossBeast = Board(g, p2, "CS2_172");
    Check("T22 Brann's Saddle targets a friendly Beast only", CanPlayOn(g, saddle, raptor) && CanPlayOn(g, saddle, cat) && !CanPlayOn(g, saddle, yeti) && !CanPlayOn(g, saddle, bossBeast) && !CanPlayOn(g, saddle, p1.Hero));
    Play(g, saddle, raptor);
    Check("T22 Brann's Saddle: +3/+3 (Saddled Up!) on the Beast", raptor.AttackDamage == 6 && raptor.Health == 5 && Has(raptor, "ULDA_402e"));
    Kill(g, raptor);
    Check("T22 when it dies, Saddled Up! moves to another friendly Beast (the River Crocolisk), never the Yeti", Has(cat, "ULDA_402e") && cat.AttackDamage == 5 && cat.Health == 6 && !Has(yeti, "ULDA_402e") && !Has(bossBeast, "ULDA_402e"));
    Kill(g, cat);
    Check("T22 with no other friendly Beast it ends there", !p1.BoardZone.Any(m => Has(m, "ULDA_402e")) && !Has(bossBeast, "ULDA_402e"));
}

// ---------------------------------------------------------------- T23 ULDA_404 Titan-Forged Grapnel
{
    var (s, plan, g, grapnel) = Fight("ULDA_404", 20);
    var p1 = g.Player1; var p2 = g.Player2; Mana(p1, 10); ClearBoards(g);
    var x = new[] { Board(g, p2, "CS2_200"), Board(g, p2, "CS2_182"), Board(g, p2, "CS2_168") };
    int armor = p1.Hero.Armor;
    Play(g, grapnel);
    var gone = x.Where(m => m.Zone?.Type != Zone.PLAY).ToList();
    Check("T23 Titan-Forged Grapnel: two random enemy minions destroyed, Armor equal to their combined Attack", gone.Count == 2 && p1.Hero.Armor == armor + gone.Sum(m => m.Card.ATK) && p2.BoardZone.Count == 1,
        $"armor {armor}->{p1.Hero.Armor} gone {string.Join(",", gone.Select(m => m.Card.Id))}");
    NextTurn(g); Mana(p1, 10); ClearBoards(g);
    var lone = Board(g, p2, "CS2_182");
    armor = p1.Hero.Armor;
    Play(g, ToHand(g, p1, "ULDA_404"));
    Check("T23 with one enemy minion: it alone is destroyed, 4 Armor", lone.Zone?.Type != Zone.PLAY && p1.Hero.Armor == armor + 4);
}

// ---------------------------------------------------------------- T24 ULDA_018 Jr. Excavator / ULDA_023 Sr. Excavator
foreach (var (id, cut, ench) in new[] { ("ULDA_018", 2, "ULDA_018e"), ("ULDA_023", 5, "ULDA_023e") })
{
    var (s, plan, g, ex) = Fight(id, 21);
    var p1 = g.Player1; Mana(p1, 10); ClearBoards(g);
    // The top of the deck: a minion, a spell and a minion (the deck's own cards are kept below).
    var top = new[] { "CS2_200", "CS2_029", "CS2_182" }.Select(c => Entity.FromCard(p1, C(c))).ToList();
    foreach (var t in top.AsEnumerable().Reverse()) p1.DeckZone.Add(t);
    int hand = p1.HandZone.Count;
    Play(g, ex);
    var drawn = p1.HandZone.Skip(hand - 1).ToList();
    Check($"T24 {id}: Battlecry: three cards drawn", p1.HandZone.Count == hand - 1 + 3 && top.All(t => t.Zone == p1.HandZone), string.Join(",", drawn.Select(d => d.Card.Id)));
    Check($"T24 {id}: the minions drawn cost ({cut}) less, the spell does not", top[0].Cost == Math.Max(0, 6 - cut) && top[2].Cost == Math.Max(0, 4 - cut) && top[1].Cost == 4
        && Has(top[0], ench) && Has(top[2], ench) && !Has(top[1], ench), $"{top[0].Cost} {top[1].Cost} {top[2].Cost}");
}

// ---------------------------------------------------------------- T25 ULDA_002 Flo Slatebrand
{
    var (s, plan, g, flo) = Fight("ULDA_002", 22);
    var p1 = g.Player1; Mana(p1, 10); ClearBoards(g);
    var pool = run.TreasurePool(s);
    int hand = p1.HandZone.Count;
    Play(g, flo);
    var added = p1.HandZone.LastOrDefault();
    Check("T25 Flo Slatebrand's Battlecry: a random treasure of the run's pool to the hand", p1.HandZone.Count == hand && added != null && pool.Contains(added.Card.Id) && data.FindTreasure(added.Card.Id)?.Signature != true,
        added?.Card.Id + " pool " + pool.Count);
    Kill(g, (Minion)flo);
    Check("T25 Flo Slatebrand's Deathrattle: another", p1.HandZone.Count == hand + 1 && pool.Contains(p1.HandZone.Last().Card.Id));
    EndTurn(g);
    var p2 = g.Player2;
    var bossFlo = Board(g, p2, "ULDA_002");
    int bossHand = p2.HandZone.Count;
    Kill(g, bossFlo);
    Check("T25 a boss that came to hold it adds none (the pool is the run player's)", p2.HandZone.Count == bossHand);
}

// ---------------------------------------------------------------- T26 ULDA_405 Brann's Epic Egg
{
    var (s, plan, g, egg) = Fight("ULDA_405", 23);
    var p1 = g.Player1; Mana(p1, 10); ClearBoards(g);
    Play(g, egg);
    var e = (Minion)egg;
    Check("T26 Brann's Epic Egg: a 0/5", e.AttackDamage == 0 && e.Health == 5);
    Play(g, ToHand(g, p1, "CS2_008"), e);
    Check("T26 after it takes damage: King Krush (8/8 Charge) is summoned", p1.BoardZone.Count(m => m.Card.Id == "EX1_543") == 1);
    Play(g, ToHand(g, p1, "CS2_008"), e);
    Check("T26 each time: a second King Krush", p1.BoardZone.Count(m => m.Card.Id == "EX1_543") == 2);
    Play(g, ToHand(g, p1, "CS2_029"), e);
    Check("T26 damage that kills it still summons one", e.Zone?.Type != Zone.PLAY && p1.BoardZone.Count(m => m.Card.Id == "EX1_543") == 3);
}

// ---------------------------------------------------------------- T27 ULDA_019 Pack Mule / ULDA_024 Upgraded Pack Mule (in the match)
foreach (string id in new[] { "ULDA_019", "ULDA_024" })
{
    var s = NewAct("MAGE_ROGUE", "ULDA_203", 24);
    // The treasure as AddActiveTreasure records it (origin treasure); T28 picks it from a draft.
    s.ActiveTreasures.Add(id);
    DungeonRunDeck.Add(s, id, DungeonRunDeck.OriginTreasure);
    var plan = run.BuildPlan(s);
    var g = StartFight(plan, run.MatchSeed(s));
    var p1 = g.Player1; Mana(p1, 10);
    var mule = Dealt(g, id);
    string Origin(IPlayable p) => RunHelpers.SlotOrigin(p);
    var cards = p1.HandZone.Concat(p1.DeckZone).Where(p => p != mule).ToList();
    var costs = cards.ToDictionary(p => p.Id, p => p.Cost);
    var generated = ToHand(g, p1, "CS2_182");
    Play(g, mule);
    var starters = cards.Where(p => Origin(p) == "starter").ToList();
    var otherDeck = cards.Where(p => Origin(p) != "starter").ToList();
    Check($"T27 {id}: the run deals it from its own card record (origin treasure)", Origin(mule) == "treasure");
    if (id == "ULDA_019")
        Check("T27 Pack Mule: each of the ten starter cards (in hand or deck, origin starter) costs (1) less (Unencumbered); the signature and a generated card do not",
            starters.Count == 10 && starters.All(p => p.Cost == Math.Max(0, costs[p.Id] - 1) && Has(p, "ULDA_019e")) && otherDeck.Count == 1 && otherDeck.All(p => p.Cost == costs[p.Id] && !Has(p, "ULDA_019e"))
            && generated.Cost == 4 && cards.Count(p => Origin(p) == "signature") == 1, string.Join(",", cards.Select(p => p.Card.Id + ":" + Origin(p) + ":" + costs[p.Id] + ">" + p.Cost)));
    else
        Check("T27 Upgraded Pack Mule: every card in the deck costs (1) less (Unencumbered), the cards in hand do not",
            cards.Where(p => p.Zone == p1.DeckZone).All(p => p.Cost == Math.Max(0, costs[p.Id] - 1) && Has(p, "ULDA_024e")) && cards.Where(p => p.Zone == p1.HandZone).All(p => p.Cost == costs[p.Id])
            && generated.Cost == 4, string.Join(",", cards.Select(p => p.Card.Id + ":" + p.Zone?.Type + ":" + costs[p.Id] + ">" + p.Cost)));
}
{
    var plain = new Game(new GameConfig { StartPlayer = 1, Player1HeroClass = CardClass.MAGE, Player2HeroClass = CardClass.MAGE, Shuffle = false, FormatType = FormatType.FT_WILD });
    var pm = Entity.FromCard(plain.Player1, C("ULDA_019"));
    Check("T27 outside a run match no card has a run slot origin, so Pack Mule finds no starter card", RunHelpers.SlotOrigin(pm) == null && RunHelpers.SlotOf(pm) < 0);
}

// ---------------------------------------------------------------- T28 Pack Mule upgrades when an act is cleared (the run deck record, by uid)
{
    var s = NewAct("MAGE_ROGUE", "ULDA_203", 25);
    s.Drafts.Add(new DungeonRunDraft { Floor = s.Floor, Kind = "treasure", Offered = new List<string> { "ULDA_019" } });
    s.Phase = DungeonRunPhase.TreasureDraft;
    run.PickTreasure(s, "ULDA_019");
    var card = s.Cards.Single(x => x.Id == "ULDA_019");
    var up = s.Upgrades.SingleOrDefault();
    Check("T28 picking Pack Mule records its upgrade on its card record (origin treasure)", card.Origin == DungeonRunDeck.OriginTreasure && up != null && up.Key == "ULDA_019" && up.Uid == card.Uid);
    int major1 = run.ScheduleFor(s).Nodes.Where(n => n.Kind == DungeonRunNodeKind.Major && n.Act == 1).Single().Floor;
    var seen = new List<string>();
    var scratch = new DungeonRunProfile();
    for (int guard = 0; guard < 200 && !s.Finished && !(s.Floor > major1 && s.Phase == DungeonRunPhase.PreMatch); guard++)
    {
        switch (s.Phase)
        {
            case DungeonRunPhase.PreMatch:
                int floor = s.Floor;
                run.BeginMatch(s); run.RecordOutcome(s, scratch, DungeonRunOutcome.Win);
                seen.Add(floor + ":" + DungeonRunDeck.Find(s, card.Uid).Id); break;
            case DungeonRunPhase.TreasureDraft: run.PickTreasure(s, s.OpenDraft.Offered.FirstOrDefault(x => x != "ULDA_019") ?? s.OpenDraft.Offered[0]); break;
            case DungeonRunPhase.Modifier: run.PickModifier(s, s.ModifierOffer.Offered[0]); break;
            case DungeonRunPhase.Tavern: run.LeaveTavern(s); break;
            case DungeonRunPhase.BundleDraft:
                if (s.OpenDraft.Takes == 0) { run.PickBundle(s, s.OpenDraft.Offered[0]); break; }
                string bundle = s.OpenDraft.Choosing ?? s.OpenDraft.Offered[0];
                var left = run.OfferCards(s.OpenDraft, bundle);
                foreach (string kept in s.OpenDraft.Chosen) left.Remove(kept);
                run.PickDraftCard(s, bundle, left[0]);
                break;
            default: throw new Exception("walk stopped at " + s.Phase);
        }
    }
    var at = seen.ToDictionary(x => int.Parse(x.Split(':')[0]), x => x.Split(':')[1]);
    Check("T28 Pack Mule stays Pack Mule through act I's fights and becomes Upgraded Pack Mule when the act's major is beaten; the record then ends",
        Enumerable.Range(1, major1 - 1).Where(at.ContainsKey).All(f => at[f] == "ULDA_019") && at[major1] == "ULDA_024" && s.ActiveTreasures.Contains("ULDA_024") && !s.ActiveTreasures.Contains("ULDA_019")
        && s.Upgrades.Count == 0 && DungeonRunDeck.MirrorError(s) == null, string.Join(" ", seen));
    var plan = run.BuildPlan(s);
    Check("T28 the next fight deals Upgraded Pack Mule", plan.PlayerDeck.Count(c => c.Id == "ULDA_024") == 1 && plan.PlayerDeck.All(c => c.Id != "ULDA_019"));
    var offer = new HashSet<string>();
    for (long seed = 0; seed < 30; seed++)
    {
        var t = NewAct("HUNTER_WARRIOR", "ULDA_401", 400 + seed);
        offer.UnionWith(run.TreasurePool(t, "Tombs"));
    }
    // Phase 6 F12: the 24 Tombs actives join Pack Mule; F15 adds ULDA_013, ULDA_040 and ULDA_407; still no signature.
    Check("T28 the run's Tombs treasure pool holds Pack Mule, the 24 Tombs actives and the 3 F15 actives (offered), no signature",
        offer.SetEquals(DungeonRunContent.TombsActiveIds.Where(x => x != "ULDA_115" && !x.EndsWith("ts")).Concat(new[] { "ULDA_019", "ULDA_013", "ULDA_040", "ULDA_407" }))
        && !offer.Any(o => data.FindTreasure(o)?.Signature == true), string.Join(",", offer));
}

// ---------------------------------------------------------------- T30 full games against the real boss AI, the player playing the signature
var testBudget = new Budget(6000, 2000, 300, 6, 3, 100, 8, turnSearchSeconds: 6, turnWallSeconds: 8);
string AiGame(string sig, long seed)
{
    string row = RowOf(sig);
    bool mule = sig == "ULDA_019" || sig == "ULDA_024";
    var s = NewAct(row, mule ? "ULDA_203" : sig, seed);
    if (mule) { s.ActiveTreasures.Add(sig); DungeonRunDeck.Add(s, sig, DungeonRunDeck.OriginTreasure); }
    for (int i = 0; i < 2; i++) s.Matches.Add(new DungeonRunMatchSummary { Floor = 1, Boss = s.BossSequence[0], Outcome = DungeonRunOutcome.Win });
    var boss = run.CurrentBoss(s);
    var plan = run.BuildPlan(s);
    var pilot = DungeonRunBossPilot.Build(boss, plan, data, runClasses: run.RunClasses(s));
    var profile = pilot.Profile.WithBudget(testBudget);
    var golden = new GoldenPilot(profile, pilot.Plan, null, pilot.SecretPrior);
    var cfg = new GameConfig { RandomSeed = run.MatchSeed(s), Shuffle = true, SkipMulligan = false, History = false, Logging = false, FormatType = FormatType.FT_WILD };
    DungeonRunSetup.Configure(cfg, plan);
    var game = new Game(cfg);
    game.StartGame();
    DungeonRunSetup.ApplyAfterStart(game, plan);
    game.Process(ChooseTask.Mulligan(game.Player1, new List<int>()));
    var choice = game.Player2.Choice;
    var view = new MulliganView(choice.Choices.Select(x => new MulliganCard(x, game.IdEntityDic[x].Card.Id, game.IdEntityDic[x].Card.Cost)),
        false, pilot.Deck.CardIds, game.Player1.Hero.Card.Class);
    game.Process(ChooseTask.Mulligan(game.Player2, BotDriver.Mulligan(view, new DeckPlanMulligan(pilot.Plan)).ToList()));
    game.MainBegin();
    // The signature the run dealt starts in the player's hand (its run deck card).
    var p1 = game.Player1;
    if (!p1.HandZone.Any(p => p.Card.Id == sig))
    {
        var e = p1.DeckZone.First(p => p.Card.Id == sig);
        p1.DeckZone.Remove(e);
        if (p1.HandZone.IsFull) { var drop = p1.HandZone.Last(); p1.HandZone.Remove(drop); p1.DeckZone.Add(drop); }
        Generic.AddHandPhase.Invoke(p1, e);
    }
    int sigId = p1.HandZone.First(p => p.Card.Id == sig).Id;
    int decisionIndex = 0, actions = 0, decisions = 0, fallbacks = 0, playerChoices = 0;
    bool played = false;
    void Act(PlayerTask task, string who)
    {
        if (++actions > 1500) throw new Exception("stuck after 1500 actions");
        if (task is ChooseTask pick && game.Step != Step.BEGIN_MULLIGAN)
        {
            var decision = game.GetPendingDecisions().Single(dd => dd.ControllerId == task.Controller.Id);
            if (!game.TryAnswer(new SabberStoneCore.Model.Decisions.DecisionAnswer(decision.Id, decision.ControllerId, pick.Choices.ToArray()), out var why))
                throw new Exception($"engine rejected {who} choice ({why})");
            if (who == "player") playerChoices++;
        }
        else if (!game.Process(task)) throw new Exception($"engine rejected {who} task {task}");
    }
    while (game.State != State.COMPLETE && game.Turn <= 60)
    {
        if (game.CurrentPlayer == game.Player2 && game.Player1.Choice == null)
        {
            var (task, trace) = BotDriver.Decide(game, game.Player2.PlayerId, pilot.Deck.Style,
                st => golden.DecidePilot(st, new GoldenContext(game.Turn, decisionIndex, unchecked(997 * game.Turn + decisionIndex * 31), profile)),
                ref decisionIndex, golden.Options);
            decisions++;
            if (trace?.Kind == "fallback") fallbacks++;
            Act(task, "boss");
        }
        else
        {
            var options = game.Player1.Options();
            // The signature first (an enemy target when it has one), then a weapon's attack, else the scripted player.
            var mine = options.OfType<PlayCardTask>().Where(o => o.Source?.Id == sigId)
                .OrderByDescending(o => o.Target == null ? 0 : (o.Target.Controller != game.Player1) == !(sig == "ULDA_203" || sig == "ULDA_402") ? 1 : 0).FirstOrDefault();
            if (mine != null) { played = true; Act(mine, "player"); }
            else Act(LocalBot.Choose(options), "player");
        }
    }
    string result = game.State != State.COMPLETE ? "turn-cap" : game.Player2.PlayState == PlayState.WON ? "boss" : game.Player1.PlayState == PlayState.WON ? "player" : "tie";
    return $"{result} turns {game.Turn} played {(played ? 1 : 0)} player-choices {playerChoices} decisions {decisions} fallbacks {fallbacks} boss {boss.Id}";
}
foreach (string sig in ids)
{
    string line;
    try { line = AiGame(sig, 281); }
    catch (Exception e) { line = "FAULT " + e.GetType().Name + ": " + e.Message.Split('\n')[0]; }
    Console.WriteLine($"T30 {sig,-9} {line}");
    Check($"T30 {sig}: the player plays it in a full game against the boss AI, which ends without a fault or an AI fallback",
        !line.StartsWith("FAULT") && line.Contains("fallbacks 0 ") && line.Contains("played 1 "), line);
}

Console.WriteLine($"{passes} passed, {failures} failed");
return failures == 0 ? 0 : 1;
