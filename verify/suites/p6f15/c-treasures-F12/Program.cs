// c-treasures-F12: phase 6 family F12 of the unified Dungeon Run, the Tombs of Terror active treasures (24 offered treasures, the
// Twinspell copies ULDA_035ts, 036ts, 041ts and 307ts, their tokens and enchantments; ULDA_115 stays rejected, phase 8). For every
// id: the engine content (keyed and shelled by its own client id, name and text as facts/violet-hold-2026-09.json), the run data
// (Tombs act-run treasure rows scoped by the explorers' sixteen starters), the validator and admission checks, the real treasure
// draft pick, a scripted regression game through the run setup path (DungeonRun.NewRun on MAGE_ROGUE -> the treasure's card record
// -> BuildPlan -> DungeonRunSetup.Configure -> Game -> ApplyAfterStart -> mulligan -> MainBegin -> the treasure dealt from the run
// deck and played), the run-state results (ULDA_306, ULDA_504) through ApplyMatchResult/RecordOutcome, and a full game against
// the real boss AI (DungeonRunBossPilot + GoldenPilot) in which the player plays it. Run it twice and compare.
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
var transport = new Dictionary<string, string>
{
    ["CS2_034"] = "HERO_08bp", ["CS2_102"] = "HERO_01bp", ["CS1h_001"] = "HERO_09bp", ["CS2_101"] = "HERO_04bp", ["CS2_017"] = "HERO_06bp",
    ["DS1h_292"] = "HERO_05bp", ["CS2_083b"] = "HERO_03bp", ["CS2_056"] = "HERO_07bp", ["CS2_049"] = "HERO_02bp",
    ["AT_132_WARRIOR"] = "HERO_01bp2", ["AT_132_SHAMAN"] = "HERO_02bp2", ["AT_132_ROGUE"] = "HERO_03bp2", ["AT_132_PALADIN"] = "HERO_04bp2",
    ["AT_132_HUNTER"] = "HERO_05bp2", ["AT_132_DRUID"] = "HERO_06bp2", ["AT_132_WARLOCK"] = "HERO_07bp2", ["AT_132_MAGE"] = "HERO_08bp2", ["AT_132_PRIEST"] = "HERO_09bp2",
};
bool ClientKnows(string id) => clientIds.Contains(transport.TryGetValue(id, out var sent) ? sent : id);
string Swap(string json, string from, string to) { if (!json.Contains(from)) throw new Exception("fixture text not found: " + from); return json.Replace(from, to); }
string[] ids = DungeonRunContent.TombsActiveIds.ToArray();
string[] scripted = ids.Where(id => id != "ULDA_115").ToArray();
// The treasure rows: the base cards (a Twinspell copy is the card's in-match copy, never a run row).
string[] rows = scripted.Where(id => !id.EndsWith("ts")).ToArray();
var effectShells = new Dictionary<string, string>
{
    ["fx_ULDA_004e"] = "ULDA_004e", ["fx_ULDA_035e"] = "ULDA_035e", ["fx_ULDA_041e"] = "ULDA_041e", ["fx_ULDA_041e2"] = "ULDA_041e2",
    ["fx_ULDA_043e"] = "ULDA_043e", ["fx_ULDA_044e"] = "ULDA_044e", ["fx_ULDA_113e"] = "ULDA_113e", ["fx_ULDA_403e"] = "ULDA_403e",
    ["fx_ULDA_406e"] = "ULDA_406e", ["fx_ULDA_406e2"] = "ULDA_406e2",
};
var tokens = new Dictionary<string, string[]>
{
    ["ULDA_008"] = new[] { "ULDA_008t" }, ["ULDA_035"] = new[] { "ULDA_035ts" }, ["ULDA_036"] = new[] { "ULDA_036ts", "ULDA_036t" },
    ["ULDA_036ts"] = new[] { "ULDA_036t" }, ["ULDA_041"] = new[] { "ULDA_041ts" }, ["ULDA_307"] = new[] { "ULDA_307ts" },
};
var twins = new Dictionary<string, string> { ["ULDA_035"] = "ULDA_035ts", ["ULDA_036"] = "ULDA_036ts", ["ULDA_041"] = "ULDA_041ts", ["ULDA_307"] = "ULDA_307ts" };

// ---------------------------------------------------------------- T1 content
{
    var family = new[] { "ULDA_001", "ULDA_004", "ULDA_006", "ULDA_008", "ULDA_012", "ULDA_034", "ULDA_035", "ULDA_035ts", "ULDA_036", "ULDA_036ts",
        "ULDA_041", "ULDA_041ts", "ULDA_043", "ULDA_044", "ULDA_110", "ULDA_112", "ULDA_113", "ULDA_114", "ULDA_115", "ULDA_118", "ULDA_202",
        "ULDA_301", "ULDA_306", "ULDA_307", "ULDA_307ts", "ULDA_403", "ULDA_406", "ULDA_503", "ULDA_504" };
    Check("T1 29 F12 ids, the family list (24 treasures, 4 Twinspell copies, ULDA_115)", ids.Length == 29 && ids.SequenceEqual(family) && rows.Length == 24);
}
foreach (string id in scripted.Concat(new[] { "ULDA_008t", "ULDA_036t" }))
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
    Check($"T1 {id}: tokens [{string.Join(",", want)}], each a client card", content.Tokens.SequenceEqual(want) && want.All(clientIds.Contains), string.Join(",", content.Tokens));
}
foreach (var fx in effectShells)
{
    var e = DungeonRunContent.Get(fx.Key);
    Check($"T1 {fx.Value}: content {fx.Key} on the client's own ENCHANTMENT, scripted, name and text word for word",
        e.Kind == RunContentKind.Effect && e.ShellId == fx.Value && C(fx.Value).Implemented && C(fx.Value).Type == CardType.ENCHANTMENT && clientIds.Contains(fx.Value)
        && e.Name == FactName(fx.Value) && e.Text == FactText(fx.Value), e.Name + " | " + e.Text);
}
foreach (var t in twins)
    Check($"T1 {t.Key}: Twinspell, its TWINSPELL_COPY the client's {t.Value}", C(t.Key)[GameTag.TWINSPELL_COPY] == C(t.Value).AssetId && C(t.Value)[GameTag.TWINSPELL_COPY] == 0);
{
    var kw = new (string What, bool Ok)[]
    {
        ("043 Windfury", C("ULDA_043")[GameTag.WINDFURY] == 1), ("034 Rush", C("ULDA_034").Rush), ("110 Rush", C("ULDA_110").Rush),
        ("110 Reborn", C("ULDA_110")[GameTag.REBORN] == 1), ("008t Rush", C("ULDA_008t").Rush), ("008t Poisonous", C("ULDA_008t").Poisonous),
        ("008t Reborn", C("ULDA_008t")[GameTag.REBORN] == 1), ("113 Battlecry", C("ULDA_113")[GameTag.BATTLECRY] == 1),
        ("044 Battlecry", C("ULDA_044")[GameTag.BATTLECRY] == 1), ("034 Deathrattle", C("ULDA_034")[GameTag.DEATHRATTLE] == 1),
        ("114 Deathrattle", C("ULDA_114")[GameTag.DEATHRATTLE] == 1), ("044 Deathrattle", C("ULDA_044")[GameTag.DEATHRATTLE] == 1),
        ("110 Deathrattle", C("ULDA_110")[GameTag.DEATHRATTLE] == 1),
    };
    Check("T1 keywords the text names: Phaoris' Blade Windfury; the Gyrocopter and the Claw Rush (the Claw Reborn); the Snake Rush, Poisonous and Reborn; Battlecries and Deathrattles",
        kw.All(k => k.Ok), string.Join(",", kw.Where(k => !k.Ok).Select(k => k.What)));
}
Check("T1 the borrowed LOOT_998le (Costs (0).) and EX1_407 (Brawl) are the engine's own scripted client cards",
    C("LOOT_998le").Implemented && C("LOOT_998le").Type == CardType.ENCHANTMENT && C("EX1_407").Implemented && DungeonRunContent.ForShell("LOOT_998le") == null);

// ---------------------------------------------------------------- T2 data, validator, admission
var mule = data.FindTreasure("ULDA_019");
{
    var errors = DungeonRunValidator.Validate(data, ClientKnows);
    Check("T2 shipped data validates with 0 errors (client check through the transported id)", errors.Count == 0, string.Join(" | ", errors.Take(5)));
    var legacy = DungeonRunData.Parse(runJson, poolJson, npcJson);
    var le = DungeonRunValidator.Validate(legacy, null);
    Check("T2 the data without the catalog validates with 0 errors and loads no F12 treasure (their rows scope catalog starters)",
        le.Count == 0 && rows.All(id => legacy.FindTreasure(id) == null), string.Join(" | ", le.Take(3)));
    foreach (string id in rows)
    {
        var t = data.FindTreasure(id);
        Check($"T2 {id}: an offered active Tombs treasure of act runs, scoped by the explorers' sixteen starters as Pack Mule, never a signature",
            t != null && t.Active && t.Offered && !t.Signature && !t.Legacy && t.Pool == "Tombs" && t.Runs == DungeonRunTreasure.RunsAct && t.Rarity == "rare"
            && t.Scope.Decks.SequenceEqual(mule.Scope.Decks) && t.Scope.Classes.Count == 0 && t.Scope.Powers.Count == 0 && t.UpgradesTo == null && t.Tags.Count > 0);
    }
    Check("T2 no Twinspell copy, token or ULDA_115 has a treasure row", ids.Concat(new[] { "ULDA_008t", "ULDA_036t" }).Where(id => !rows.Contains(id)).All(id => data.FindTreasure(id) == null));
}
var families = new Dictionary<string, string>
{
    ["ULDA_110"] = "run-treasure-hand-steal", ["ULDA_035"] = "run-treasure-hand-escrow", ["ULDA_035ts"] = "run-treasure-hand-escrow",
    ["ULDA_301"] = "run-treasure-run-state", ["ULDA_306"] = "run-treasure-run-state", ["ULDA_504"] = "run-treasure-run-state",
    ["ULDA_118"] = "run-treasure-burn", ["ULDA_044"] = "deck-wide-change", ["ULDA_503"] = "from-own-deck",
};
foreach (string id in scripted.Concat(new[] { "ULDA_008t", "ULDA_036t" }))
{
    string family = families.TryGetValue(id, out var f) ? f : "run-treasure";
    var p = DungeonRunAdmission.Classify(C(id), id, AdmitScope.Player, null, catalogRun: true);
    var b = DungeonRunAdmission.Classify(C(id), id, AdmitScope.Boss, null, catalogRun: true);
    bool anyScope = family == "from-own-deck";
    Check($"T2 admission {id}: the {family} row admits the player{(anyScope ? " (any scope: the copies are of the owner's own deck)" : ", never the boss; the boss's generation never yields it")}",
        p.Subject && p.Admitted && DungeonRunAdmission.RuleFor(id)?.Family == family && DungeonRunAdmission.Classify(C(id), id, AdmitScope.Player).Admitted
        && (anyScope || !b.Admitted && DungeonRunAdmission.GeneratedRejectsBoss(true).Contains(id)), p.Family + " " + p.Admit);
}

// ---------------------------------------------------------------- the run setup path
GameConfig Configured(RunMatchPlan plan, int seed, bool history = true)
{
    var cfg = new GameConfig { RandomSeed = seed, Shuffle = true, SkipMulligan = false, History = history, Logging = false, FormatType = FormatType.FT_WILD };
    DungeonRunSetup.Configure(cfg, plan);
    return cfg;
}
var juniors = new DungeonRunProfile(); juniors.Unlocked.AddRange(data.Unlocks.Keys.Where(k => !k.EndsWith("_majors4")));
const string Row = "MAGE_ROGUE";
// An act run of Reno (MAGE_ROGUE) at floor 1, its first offered signature picked.
DungeonRunState NewAct(long seed, string row = Row)
{
    var cls = data.Class(row);
    string sig = run.SignatureOffer(juniors, cls, seed)[0];
    var s = run.NewRun(juniors, row, cls.DefaultDeck, cls.DefaultPower, seed, signatureId: sig);
    if (!s.IsActRun || s.Phase != DungeonRunPhase.PreMatch || s.Floor != 1) throw new Exception("act run not at floor 1 pre-match");
    return s;
}
// The treasure joins the run as a picked active treasure does (DungeonRun.AddActiveTreasure: the id and its card record, origin treasure).
void Grant(DungeonRunState s, string id) { s.ActiveTreasures.Add(id); DungeonRunDeck.Add(s, id, DungeonRunDeck.OriginTreasure); }
Game StartFight(RunMatchPlan plan, int seed, bool history = true)
{
    var g = new Game(Configured(plan, seed, history));
    g.StartGame();
    DungeonRunSetup.ApplyAfterStart(g, plan);
    g.Process(ChooseTask.Mulligan(g.Player1, new List<int>()));
    g.Process(ChooseTask.Mulligan(g.Player2, new List<int>()));
    g.MainBegin();
    if (g.CurrentPlayer != g.Player1) throw new Exception("run player not first");
    return g;
}
void Mana(Controller c, int n) { c.BaseMana = n; c.UsedMana = 0; c.OverloadLocked = 0; c.OverloadOwed = 0; }
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
(DungeonRunState s, RunMatchPlan plan, Game g, IPlayable card) Fight(string id, long seed, int wins = 0, bool keepHand = false)
{
    var s = NewAct(seed);
    Grant(s, id);
    for (int i = 0; i < wins; i++) s.Matches.Add(new DungeonRunMatchSummary { Floor = 1, Boss = s.BossSequence[0], Outcome = DungeonRunOutcome.Win });
    var plan = run.BuildPlan(s);
    var g = StartFight(plan, run.MatchSeed(s));
    var card = Dealt(g, id);
    if (!keepHand) foreach (var e in g.Player1.HandZone.ToArray()) if (e != card) { g.Player1.HandZone.Remove(e); g.Player1.SetasideZone.Add(e); }
    if (RunHelpers.SlotOf(card) < 0) throw new Exception(id + " is not a run deck card");
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
bool HeroAttack(Game g, ICharacter target) => g.Process(HeroAttackTask.Any(g.Player1, target));
bool MinionAttack(Game g, Minion m, ICharacter target) => g.Process(MinionAttackTask.Any(g.Player1, m, target));
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
    while (g.Player1.Choice != null) Pick(g, g.Player1.Choice.Choices[0]);
}
void Kill(Game g, Minion m) { m.Destroy(); g.DeathProcessingAndAuraUpdate(); }

// ---------------------------------------------------------------- T3 the treasure draft offers them (the real pick path)
{
    var cls = data.Class(Row);
    var seen = new HashSet<string>();
    for (long seed = 1; seed <= 4000 && !rows.All(seen.Contains); seed++)
    {
        var s = NewAct(seed);
        if (!run.OpenTreasureDraft(s)) continue;
        var offer = s.OpenDraft.Offered;
        var pick = offer.FirstOrDefault(o => rows.Contains(o) && !seen.Contains(o));
        if (pick == null) continue;
        seen.Add(pick);
        int before = s.Deck.Count;
        run.PickTreasure(s, pick);
        var rec = s.Cards.LastOrDefault(c => c.Id == pick);
        Check($"T3 {pick}: offered in a Tombs act run's treasure draft and picked through DungeonRun.PickTreasure: the active treasure and its card record (origin treasure) join the run",
            s.ActiveTreasures.Contains(pick) && s.Deck.Count == before + 1 && s.Deck.Last() == pick && rec?.Origin == DungeonRunDeck.OriginTreasure && DungeonRunDeck.MirrorError(s) == null);
    }
    Check("T3 every F12 treasure is offered to a Tombs act run (4000 seeds)", rows.All(seen.Contains), string.Join(",", rows.Where(r => !seen.Contains(r))));
    var pool = run.TreasurePool(NewAct(7), "Tombs");
    Check("T3 the run's Tombs treasure pool (RunContext.TreasurePool, Tombs) holds the 24 F12 treasures and Pack Mule, and the 3 F15 actives", rows.All(pool.Contains) && pool.Contains("ULDA_019") && pool.Count == 28, string.Join(",", pool));
    // A class row's act run is never offered one (scope: the explorers' starters).
    var mage = data.Class("MAGE");
    bool none = true;
    for (long seed = 1; seed <= 300; seed++)
    {
        var s = run.NewRun(all, "MAGE", mage.DefaultDeck, mage.DefaultPower, seed);
        if (s.IsActRun && run.OpenTreasureDraft(s) && s.OpenDraft.Offered.Any(rows.Contains)) { none = false; break; }
    }
    Check("T3 a plain Mage run is never offered an F12 treasure (300 seeds)", none);
}

// ---------------------------------------------------------------- T4 ULDA_001 Amakir the Light
{
    var (s, plan, g, card) = Fight("ULDA_001", 11);
    var p1 = g.Player1; var p2 = g.Player2; Mana(p1, 10); ClearBoards(g);
    Play(g, card);
    var amakir = (Minion)card;
    Check("T4 Amakir is a 3/6 on the board", amakir.Zone?.Type == Zone.PLAY && amakir.AttackDamage == 3 && amakir.Health == 6);
    NextTurn(g); Mana(p1, 10); Clear(p1);
    MinionAttack(g, amakir, p2.Hero);
    var adds = p1.HandZone.ToList();
    Check("T4 after it attacks: 2 random healing spells (text restores Health) in hand, each costing (0) (LOOT_998le)",
        adds.Count == 2 && adds.All(a => a.Card.Type == CardType.SPELL && a.Card.Text.Contains("Restore") && a.Cost == 0 && Has(a, "LOOT_998le")),
        string.Join(",", adds.Select(a => a.Card.Id + ":" + a.Cost)));
    var pool = DungeonRunContent.HealingSpellPool(g, p1, amakir);
    Check("T4 the healing pool: scripted spells, no Secret, every one restores Health, ordinal", pool.Count > 10 && pool.All(c => c.Implemented && !c.IsSecret && c.Text.Contains("Restore")));
}

// ---------------------------------------------------------------- T5 ULDA_004 Canopic Jars
{
    var (s, plan, g, card) = Fight("ULDA_004", 12);
    var p1 = g.Player1; Mana(p1, 10); ClearBoards(g);
    var a = Board(g, p1, "CS2_231"); var b = Board(g, p1, "CS2_182");
    var foe = Board(g, g.Player2, "CS2_231");
    Play(g, card);
    Check("T5 Canopic Jars: each friendly minion has the Deathrattle enchantment (ULDA_004e), not the enemy's", Has(a, "ULDA_004e") && Has(b, "ULDA_004e") && !Has(foe, "ULDA_004e"));
    Kill(g, a);
    var spawned = p1.BoardZone.Where(m => m != b).ToList();
    Check("T5 a minion with it dies: a random Legendary minion is summoned for its owner", spawned.Count == 1 && spawned[0].Card.Rarity == Rarity.LEGENDARY && spawned[0].Card.Implemented,
        string.Join(",", spawned.Select(m => m.Card.Id)));
}

// ---------------------------------------------------------------- T6 ULDA_006 Book of the Dead
{
    var (s, plan, g, book) = Fight("ULDA_006", 13);
    var p1 = g.Player1; var p2 = g.Player2; Mana(p1, 10); ClearBoards(g);
    int dead0 = DungeonRunContent.MinionsDiedThisGame(p1);
    Check("T6 Book of the Dead costs 12 less the minions that died this game", book.Cost == 12 - dead0, $"{book.Cost} dead {dead0}");
    Kill(g, Board(g, p1, "CS2_231")); Kill(g, Board(g, p2, "CS2_231")); Kill(g, Board(g, p2, "CS2_231"));
    Check("T6 three deaths (one friendly, two enemy): it costs (3) less", book.Cost == 12 - dead0 - 3 && DungeonRunContent.MinionsDiedThisGame(p1) == dead0 + 3, $"{book.Cost}");
    var yeti = Board(g, p2, "CS2_182"); var mine = Board(g, p1, "CS2_182");
    int hp = p2.Hero.Health + p2.Hero.Armor;
    Play(g, book);
    Check("T6 7 damage to every enemy (hero and minions), none to the player's", p2.Hero.Health + p2.Hero.Armor == hp - 7 && yeti.ToBeDestroyed | yeti.Zone?.Type != Zone.PLAY && mine.Health == 5,
        $"{hp}->{p2.Hero.Health + p2.Hero.Armor}");
}

// ---------------------------------------------------------------- T7 ULDA_008 Staff of Scales
{
    var (s, plan, g, card) = Fight("ULDA_008", 14);
    var p1 = g.Player1; Mana(p1, 10); ClearBoards(g);
    Play(g, card);
    var snakes = p1.BoardZone.ToList();
    Check("T7 Staff of Scales: three 1/1 Ancient Snakes with Rush, Poisonous and Reborn", snakes.Count == 3 && snakes.All(m => m.Card.Id == "ULDA_008t" && m.AttackDamage == 1 && m.Health == 1
        && m.IsRush && m.Poisonous && m[GameTag.REBORN] == 1));
    var yeti = Board(g, g.Player2, "CS2_182");
    MinionAttack(g, snakes[0], yeti);
    Check("T7 a Snake attacks a minion at once (Rush): Poisonous kills it, the Snake comes back (Reborn)", yeti.Zone?.Type != Zone.PLAY && p1.BoardZone.Count == 3
        && p1.BoardZone.All(m => m.Card.Id == "ULDA_008t"));
}

// ---------------------------------------------------------------- T8 ULDA_012 Stone Fox Statue
{
    var (s, plan, g, card) = Fight("ULDA_012", 15);
    var p1 = g.Player1; Mana(p1, 10); ClearBoards(g);
    var yeti = Board(g, g.Player2, "CS2_182");
    Check("T8 Stone Fox Statue targets a minion of either side, never a hero", CanPlayOn(g, card, yeti) && !CanPlayOn(g, card, g.Player2.Hero));
    Play(g, card, yeti);
    var copies = p1.HandZone.ToList();
    Check("T8 two copies of the minion's card in hand, each costing (0), now the player's; the minion stays", copies.Count == 2 && copies.All(x => x.Card.Id == "CS2_182" && x.Cost == 0 && x.Controller == p1)
        && yeti.Zone?.Type == Zone.PLAY);
}

// ---------------------------------------------------------------- T9 ULDA_034 Runaway Gyrocopter
{
    var (s, plan, g, card) = Fight("ULDA_034", 16);
    var p1 = g.Player1; var p2 = g.Player2; Mana(p1, 10); ClearBoards(g);
    var y1 = Board(g, p2, "CS2_182"); var y2 = Board(g, p2, "CS2_120");
    Play(g, card);
    var gyro = (Minion)card;
    Check("T9 the Gyrocopter is a 5/1 Mech with Rush (it can attack a minion at once)", gyro.AttackDamage == 5 && gyro.Health == 1 && gyro.IsRush && gyro.Card.IsRace(Race.MECHANICAL)
        && p1.Options().OfType<MinionAttackTask>().Any(o => o.Source == gyro && o.Target == y1));
    int deck = p1.DeckZone.Count;
    MinionAttack(g, gyro, y1);
    Check("T9 it dies: 5 damage to every enemy minion, and a Gyrocopter is shuffled into the deck", y1.Zone?.Type != Zone.PLAY && y2.Zone?.Type != Zone.PLAY
        && p1.DeckZone.Count == deck + 1 && p1.DeckZone.Count(x => x.Card.Id == "ULDA_034") == 1, $"deck {deck}->{p1.DeckZone.Count}");
}

// ---------------------------------------------------------------- T10 ULDA_035 Mystical Mirage (and its Twinspell copy)
{
    var (s, plan, g, mirage) = Fight("ULDA_035", 17);
    var p1 = g.Player1; Mana(p1, 10); ClearBoards(g);
    var kept = new[] { ToHand(g, p1, "CS2_182"), ToHand(g, p1, "CS2_029"), ToHand(g, p1, "ULDA_012") };
    Play(g, ToHand(g, p1, "CS2_182"));
    p1.UsedMana = 7;
    int deck = p1.DeckZone.Count;
    var deckIds = p1.DeckZone.Select(x => x.Id).ToHashSet();
    Play(g, mirage);
    var hand = p1.HandZone.ToList();
    var ts = hand.FirstOrDefault(x => x.Card.Id == "ULDA_035ts");
    Check("T10 Mystical Mirage: Mana Crystals refreshed; the three cards in hand held aside; three drawn from the deck; the Twinspell copy in hand",
        p1.RemainingMana == 10 && kept.All(k => k.Zone == p1.SetasideZone) && hand.Count == 4 && ts != null
        && hand.Where(x => x != ts).All(x => deckIds.Contains(x.Id)) && p1.DeckZone.Count == deck - 3 && p1.AppliedEnchantments.Any(e => e.Card.Id == "ULDA_035e"),
        $"mana {p1.RemainingMana} hand {string.Join(",", hand.Select(x => x.Card.Id))} deck {deck}->{p1.DeckZone.Count}");
    var first = hand.Where(x => x != ts).ToList();
    Play(g, ts);
    var second = p1.HandZone.ToList();
    Check("T10 the Twinspell copy, the same turn: the Mirage hand is held aside too and a new one drawn (three cards, as the Mirage hand held); no copy of the copy",
        first.All(x => x.Zone == p1.SetasideZone) && second.Count == 3 && second.All(x => deckIds.Contains(x.Id) && !first.Contains(x)) && p1.RemainingMana == 10,
        string.Join(",", second.Select(x => x.Card.Id)));
    var keptCost = kept[2].Cost;
    EndTurn(g);
    Check("T10 at the end of the turn the kept hand returns (the same entities, in order, ULDA_012 at its cost); every Mirage card goes back into the deck; the mark is gone",
        p1.HandZone.Select(x => x.Id).SequenceEqual(kept.Select(k => k.Id)) && kept[2].Cost == 5 && keptCost == 5 && first.Concat(second).All(x => x.Zone == p1.DeckZone)
        && p1.DeckZone.Count == deck && !p1.AppliedEnchantments.Any(e => e.Card.Id == "ULDA_035e") && p1.SetasideZone.All(x => x[DungeonRunContent.MirageKeptTag] == 0),
        $"hand {string.Join(",", p1.HandZone.Select(x => x.Card.Id))} deck {p1.DeckZone.Count}/{deck}");
    EndTurn(g);
    Check("T10 the next turn draws normally (the hand holds the kept three and the draw)", p1.HandZone.Count == 4 && p1.HandZone.Take(3).Select(x => x.Id).SequenceEqual(kept.Select(k => k.Id)));
}
{
    var (s, plan, g, mirage) = Fight("ULDA_035", 18);
    var p1 = g.Player1; Mana(p1, 10); ClearBoards(g);
    int deck = p1.DeckZone.Count;
    Play(g, mirage);
    Check("T10 an empty hand: the Mirage refreshes the Mana Crystals and draws nothing (the Twinspell copy alone in hand)", p1.HandZone.Count == 1 && p1.HandZone[0].Card.Id == "ULDA_035ts" && p1.DeckZone.Count == deck);
    EndTurn(g);
    Check("T10 the unplayed Twinspell copy is part of the Mirage hand: it goes into the deck at the turn's end", p1.HandZone.Count == 0 && p1.DeckZone.Count == deck + 1 && p1.DeckZone.Any(x => x.Card.Id == "ULDA_035ts"));
}

// ---------------------------------------------------------------- T11 ULDA_036 LOCUUUUSTS!!!
{
    var (s, plan, g, card) = Fight("ULDA_036", 19);
    var p1 = g.Player1; var p2 = g.Player2; Mana(p1, 10); ClearBoards(g);
    var mine = Board(g, p1, "CS2_231");
    Check("T11 LOCUUUUSTS!!! targets an enemy, never a friendly character", CanPlayOn(g, card, p2.Hero) && !CanPlayOn(g, card, mine) && !CanPlayOn(g, card, p1.Hero));
    int hp = p2.Hero.Health + p2.Hero.Armor;
    Play(g, card, p2.Hero);
    var locusts = p1.BoardZone.Where(m => m.Card.Id == "ULDA_036t").ToList();
    Check("T11 the board fills with Giant Locusts (2/2), and each attacks the chosen hero: 6 Locusts, 12 damage; the Twinspell copy in hand",
        locusts.Count == 6 && locusts.All(m => m.AttackDamage == 2 && m.Health == 2) && p2.Hero.Health + p2.Hero.Armor == hp - 12 && p1.HandZone.Any(x => x.Card.Id == "ULDA_036ts"),
        $"{locusts.Count} {hp}->{p2.Hero.Health + p2.Hero.Armor}");
    ClearBoards(g);
    var yeti = Board(g, p2, "CS2_182");
    var copy = p1.HandZone.First(x => x.Card.Id == "ULDA_036ts");
    Play(g, copy, yeti);
    Check("T11 the copy at a 4/5: Locusts attack until it dies (three trades), the rest stay", yeti.Zone?.Type != Zone.PLAY && p1.BoardZone.Count(m => m.Card.Id == "ULDA_036t") == 4
        && !p1.HandZone.Any(x => x.Card.Id.StartsWith("ULDA_036")), $"{p1.BoardZone.Count}");
}

// ---------------------------------------------------------------- T12 ULDA_041 Staff of Ammunae
{
    var (s, plan, g, card) = Fight("ULDA_041", 20);
    var p1 = g.Player1; var p2 = g.Player2; Mana(p1, 10); ClearBoards(g);
    var a = Board(g, p1, "CS2_182");
    Play(g, card);
    var b = Board(g, p1, "CS2_120");
    Check("T12 Staff of Ammunae: the player's minions (one played after it too) have Windfury and Immune (Royal Fervor); the boss's do not; the Twinspell copy in hand",
        a[GameTag.WINDFURY] == 1 && a.IsImmune && b[GameTag.WINDFURY] == 1 && b.IsImmune && Has(a, "ULDA_041e") && p1.HandZone.Any(x => x.Card.Id == "ULDA_041ts"));
    var foe = Board(g, p2, "CS2_182");
    a.IsExhausted = false;
    MinionAttack(g, a, foe);
    Check("T12 an Immune minion attacks and takes no damage, then attacks again (Windfury)", a.Health == 5 && p1.Options().OfType<MinionAttackTask>().Any(o => o.Source == a));
    EndTurn(g);
    Check("T12 at the end of the turn Windfury and Immune are gone", a[GameTag.WINDFURY] == 0 && !a.IsImmune && !b.IsImmune && !Has(a, "ULDA_041e"));
}

// ---------------------------------------------------------------- T13 ULDA_043 Phaoris' Blade
{
    var (s, plan, g, card) = Fight("ULDA_043", 21);
    var p1 = g.Player1; var p2 = g.Player2; Mana(p1, 10); ClearBoards(g);
    Play(g, card);
    var blade = p1.Hero.Weapon;
    Check("T13 Phaoris' Blade: a 1/3 Windfury weapon", blade?.Card.Id == "ULDA_043" && p1.Hero.AttackDamage == 1 && blade.Durability == 3 && blade.IsWindfury);
    var wisp = Board(g, p2, "CS2_231");
    HeroAttack(g, wisp);
    Check("T13 the hero attacks and kills a minion: +2/+1 (Phaoris' Fury): 3 Attack, Durability 3 again", wisp.Zone?.Type != Zone.PLAY && p1.Hero.AttackDamage == 3 && blade.Durability == 3
        && Has(blade, "ULDA_043e"), $"{p1.Hero.AttackDamage} dur {blade.Durability}");
    var yeti = Board(g, p2, "CS2_182");
    HeroAttack(g, yeti);
    Check("T13 the second attack (Windfury) at a minion that survives: no gain", yeti.Zone?.Type == Zone.PLAY && p1.Hero.AttackDamage == 3 && blade.Durability == 2);
}

// ---------------------------------------------------------------- T14 ULDA_044 Blade of the Burning Sun
{
    var (s, plan, g, card) = Fight("ULDA_044", 22);
    var p1 = g.Player1; Mana(p1, 10); ClearBoards(g);
    var minions = p1.DeckZone.OfType<Minion>().ToList(); var spells = p1.DeckZone.Where(x => !(x is Minion)).ToList();
    var atk = minions.ToDictionary(m => m.Id, m => m.AttackDamage);
    Play(g, card);
    Check("T14 Blade of the Burning Sun: a 4/2 weapon; every minion in the deck +2 Attack (Sun's Flames), nothing else in it", p1.Hero.AttackDamage == 4 && p1.Hero.Weapon.Durability == 2
        && minions.Count > 0 && minions.All(m => m.AttackDamage == atk[m.Id] + 2 && Has(m, "ULDA_044e")) && spells.All(x => !Has(x, "ULDA_044e")));
    p1.Hero.Weapon.Destroy(); g.DeathProcessingAndAuraUpdate();
    Check("T14 the weapon is destroyed: it returns to the hand (a Blade of the Burning Sun)", p1.Hero.Weapon == null && p1.HandZone.Count(x => x.Card.Id == "ULDA_044") == 1, $"weapon {p1.Hero.Weapon?.Card.Id} hand {string.Join(",", p1.HandZone.Select(x => x.Card.Id))} dr {C("ULDA_044")[GameTag.DEATHRATTLE]} grave {string.Join(",", p1.GraveyardZone.Select(x => x.Card.Id))}");
}

// ---------------------------------------------------------------- T15 ULDA_110 Crawling Claw
{
    var (s, plan, g, card) = Fight("ULDA_110", 23);
    var p1 = g.Player1; var p2 = g.Player2; Mana(p1, 10); ClearBoards(g);
    Play(g, card);
    var claw = (Minion)card;
    Check("T15 Crawling Claw: a 2/4 with Rush and Reborn", claw.AttackDamage == 2 && claw.Health == 4 && claw.IsRush && claw[GameTag.REBORN] == 1);
    int bossHand = p2.HandZone.Count, mine = p1.HandZone.Count;
    var before = p2.HandZone.Select(x => x.Id).ToHashSet();
    Kill(g, claw);
    var stolen = p1.HandZone.FirstOrDefault(x => before.Contains(x.Id));
    var reborn = p1.BoardZone.SingleOrDefault(m => m.Card.Id == "ULDA_110");
    Check("T15 it dies: a random card leaves the boss's hand for the player's (now the player's); it comes back with 1 Health (Reborn)",
        bossHand > 0 && p2.HandZone.Count == bossHand - 1 && p1.HandZone.Count == mine + 1 && stolen?.Controller == p1 && reborn != null && reborn.Health == 1,
        $"{bossHand}->{p2.HandZone.Count}");
    Clear(p2);
    mine = p1.HandZone.Count;
    Kill(g, reborn);
    Check("T15 an empty boss hand: its second death steals nothing", p1.HandZone.Count == mine && !p1.BoardZone.Any(m => m.Card.Id == "ULDA_110"));
}

// ---------------------------------------------------------------- T16 ULDA_112 Bob's Bouncer
{
    var (s, plan, g, card) = Fight("ULDA_112", 24);
    var p1 = g.Player1; Mana(p1, 10); ClearBoards(g);
    var yeti = Board(g, p1, "CS2_182");
    Play(g, card, yeti);
    Check("T16 Bob's Bouncer with the chosen minion alone: Brawl destroys nothing and the board fills with copies of it (7 Yetis)",
        p1.BoardZone.Count == 7 && p1.BoardZone.All(m => m.Card.Id == "CS2_182") && yeti.Zone?.Type == Zone.PLAY);
    int filled = 0, cleared = 0;
    for (long seed = 30; seed < 70 && (filled == 0 || cleared == 0); seed++)
    {
        var (_, _, g2, c2) = Fight("ULDA_112", seed);
        var q1 = g2.Player1; Mana(q1, 10); ClearBoards(g2);
        var chosen = Board(g2, g2.Player2, "CS2_182"); Board(g2, g2.Player2, "CS2_120"); Board(g2, q1, "CS2_231"); Board(g2, q1, "CS2_200");
        Play(g2, c2, chosen);
        int standing = q1.BoardZone.Count + g2.Player2.BoardZone.Count;
        if (chosen.Zone?.Type == Zone.PLAY && q1.BoardZone.Count == 7 && q1.BoardZone.All(m => m.Card.Id == "CS2_182") && g2.Player2.BoardZone.Count == 1) filled++;
        else if (chosen.Zone?.Type != Zone.PLAY && standing == 1) cleared++;
        else { Check("T16 Brawl leaves one minion standing", false, $"seed {seed} standing {standing}"); break; }
    }
    Check("T16 with four minions: Brawl (cast by the effect) leaves one at random; the chosen enemy minion survives on some seeds (the player's board fills with copies of it) and dies on others (no copies)",
        filled > 0 && cleared > 0, $"filled {filled} cleared {cleared}");
}

// ---------------------------------------------------------------- T17 ULDA_113 Crusty the Crustacean
{
    var (s, plan, g, card) = Fight("ULDA_113", 25);
    var p1 = g.Player1; var p2 = g.Player2; Mana(p1, 10); ClearBoards(g);
    var yeti = Board(g, p2, "CS2_182");
    Check("T17 Crusty's Battlecry targets a minion (an enemy one here), never a hero", CanPlayOn(g, card, yeti) && !CanPlayOn(g, card, p2.Hero));
    Play(g, card, yeti);
    var crusty = (Minion)card;
    Check("T17 Crusty destroys the 4/5 and gains double its Attack and Health (Om Nom Nom): 3/4 + 8/10 = 11/14", yeti.Zone?.Type != Zone.PLAY && crusty.AttackDamage == 11 && crusty.Health == 14
        && Has(crusty, "ULDA_113e"), $"{crusty.AttackDamage}/{crusty.Health}");
    var (_, _, g2, c2) = Fight("ULDA_113", 26);
    Mana(g2.Player1, 10); ClearBoards(g2);
    Play(g2, c2);
    Check("T17 with no minion to target it is played as a plain 3/4 (a Battlecry target if available)", ((Minion)c2).Zone?.Type == Zone.PLAY && ((Minion)c2).AttackDamage == 3);
}

// ---------------------------------------------------------------- T18 ULDA_114 Aegis of Death
{
    var (s, plan, g, card) = Fight("ULDA_114", 27);
    var p1 = g.Player1; var p2 = g.Player2; Mana(p1, 10); ClearBoards(g);
    Play(g, card);
    var aegis = p1.Hero.Weapon;
    Check("T18 Aegis of Death: a 0/10 weapon; the hero is Immune", aegis?.Card.Id == "ULDA_114" && aegis.Durability == 10 && p1.Hero.IsImmune);
    int hp = p1.Hero.Health;
    Play(g, ToHand(g, p1, "CS2_029"), p1.Hero);
    Check("T18 damage to the Immune hero does nothing", p1.Hero.Health == hp);
    EndTurn(g);
    Check("T18 it loses 1 Durability at the end of the player's turn", aegis.Durability == 9);
    EndTurn(g);
    Check("T18 and at the end of the boss's turn (each turn)", aegis.Durability == 8 && p1.Hero.IsImmune);
    aegis.Destroy(); g.DeathProcessingAndAuraUpdate();
    Check("T18 it is destroyed: its Deathrattle deals 100 damage to the hero, who is no longer Immune: the player loses", p1.Hero.Weapon == null && (p1.Hero.Health <= 0 || p1.PlayState == PlayState.LOST),
        $"{p1.Hero.Health} {p1.PlayState}");
    var (_, _, g2, c2) = Fight("ULDA_114", 28);
    Mana(g2.Player1, 10); ClearBoards(g2); Play(g2, c2);
    for (int i = 0; i < 10 && g2.State != State.COMPLETE; i++) EndTurn(g2);
    Check("T18 ten turn ends wear it out: the hero dies to its Deathrattle", g2.Player1.Hero.Weapon == null && g2.Player1.PlayState == PlayState.LOST, $"{g2.Player1.PlayState}");
}

// ---------------------------------------------------------------- T19 ULDA_118 Flex-plosion
{
    var (s, plan, g, card) = Fight("ULDA_118", 29);
    var p1 = g.Player1; var p2 = g.Player2; Mana(p1, 10); ClearBoards(g);
    for (int i = 0; i < 5; i++) Board(g, p2, "CS2_182");
    p2.BaseMana = 7;
    int hand = p2.HandZone.Count, deck = p2.DeckZone.Count, health = p2.Hero.Health, armor = p2.Hero.Armor, grave = p2.GraveyardZone.Count;
    Play(g, card);
    int expectHp = health + armor - health / 2;
    Check($"T19 Flex-plosion: 3 of 5 enemy minions destroyed, {(hand + 1) / 2} of {hand} cards discarded, {(deck + 1) / 2} of {deck} deck cards destroyed, 3 of 7 Mana Crystals, {health / 2} damage",
        p2.BoardZone.Count == 2 && p2.HandZone.Count == hand - (hand + 1) / 2 && p2.DeckZone.Count == deck - (deck + 1) / 2 && p2.BaseMana == 4
        && p2.Hero.Health + p2.Hero.Armor == expectHp && p2.GraveyardZone.Count == grave + 3 + (hand + 1) / 2 + (deck + 1) / 2,
        $"board {p2.BoardZone.Count} hand {p2.HandZone.Count} deck {p2.DeckZone.Count} mana {p2.BaseMana} hp {p2.Hero.Health}+{p2.Hero.Armor}");
    var ledger = OwnDeckLedger.Observe(g, p2, null);
    var sample = ledger.Sample(g, p2, 1);
    var actual = p2.DeckZone.GroupBy(x => x.Card.Id).ToDictionary(x => x.Key, x => x.Count());
    Check("T19 the boss's own-deck ledger reads the burns: its sampled deck is the deck that is left (multiset)",
        sample.Values.Sum() == p2.DeckZone.Count && actual.All(kv => sample.TryGetValue(kv.Key, out int n) && n == kv.Value),
        $"sample {sample.Values.Sum()} deck {p2.DeckZone.Count}");
    Check("T19 the half rule, precisely: up for cards and minions, down for Crystals and Health", DungeonRunContent.FlexHalfUp(5) == 3 && DungeonRunContent.FlexHalfUp(0) == 0
        && DungeonRunContent.FlexHalfUp(1) == 1 && DungeonRunContent.FlexHalfDown(7) == 3 && DungeonRunContent.FlexHalfDown(1) == 0);
}

// ---------------------------------------------------------------- T20 ULDA_202 Kodo Hide Whip
{
    var (s, plan, g, card) = Fight("ULDA_202", 30);
    var p1 = g.Player1; var p2 = g.Player2; Mana(p1, 10); ClearBoards(g);
    Play(g, card);
    Check("T20 Kodo Hide Whip: a 2/3 weapon", p1.Hero.Weapon?.Card.Id == "ULDA_202" && p1.Hero.AttackDamage == 2 && p1.Hero.Weapon.Durability == 3);
    var yeti = Board(g, p2, "CS2_182");
    Play(g, ToHand(g, p1, "CS2_046"), null);
    int hand = p1.HandZone.Count;
    HeroAttack(g, yeti);
    Check("T20 the hero attacks a 4/5 that survives: it goes to the player's hand (the player's card, its damage gone)", p2.BoardZone.Count == 0 && p1.HandZone.Count == hand + 1
        && p1.HandZone.Last() == yeti && yeti.Controller == p1 && yeti.Health == 5 && yeti.Damage == 0, $"hand {hand}->{p1.HandZone.Count}");
    NextTurn(g); Mana(p1, 10);
    var wisp = Board(g, p2, "CS2_231");
    hand = p1.HandZone.Count;
    HeroAttack(g, wisp);
    Check("T20 a minion the attack kills is not taken", wisp.Zone?.Type != Zone.PLAY && p1.HandZone.Count == hand);
    Mana(p1, 10);
    Play(g, yeti);
    Check("T20 the taken minion is played as the player's own", yeti.Zone == p1.BoardZone);
}

// ---------------------------------------------------------------- T21 ULDA_301 Map of Uldum
{
    var (s, plan, g, card) = Fight("ULDA_301", 31, wins: 3);
    var p1 = g.Player1; Mana(p1, 5); ClearBoards(g);
    Check("T21 the run context says 3 bosses defeated", plan.RunContext.BossesDefeated == 3 && RunHelpers.Bosses(g) == 3);
    Play(g, card);
    int picks = 0;
    while (p1.Choice != null && picks < 10) { Pick(g, p1.Choice.Choices[0]); picks++; }
    Check("T21 Map of Uldum after 3 bosses: three Discovers (three cards added) and three empty Mana Crystals", picks == 3 && p1.HandZone.Count == 3 && p1.BaseMana == 8 && p1.RemainingMana == 5,
        $"picks {picks} hand {p1.HandZone.Count} mana {p1.BaseMana}/{p1.RemainingMana}");
    var (_, _, g0, c0) = Fight("ULDA_301", 32);
    Mana(g0.Player1, 5); Play(g0, c0);
    Check("T21 the first fight (no boss defeated yet): no Discover, no Crystal", g0.Player1.Choice == null && g0.Player1.HandZone.Count == 0 && g0.Player1.BaseMana == 5);
}

// ---------------------------------------------------------------- T22 ULDA_306 Stolen Titan Secrets
{
    var (s, plan, g, card) = Fight("ULDA_306", 33);
    var p1 = g.Player1; var p2 = g.Player2; Mana(p1, 10);
    string theirs = p2.Hero.HeroPower.Card.Id;
    Play(g, card);
    Check("T22 Stolen Titan Secrets: the player's hero power is now a copy of the boss's, in this match", p1.Hero.HeroPower.Card.Id == theirs && p1.Hero.HeroPower != p2.Hero.HeroPower
        && g.RunResult?.PowerOverride == theirs, theirs);
    var notes = run.ApplyMatchResult(s, g.RunResult);
    Check("T22 a won fight keeps it for the rest of the run (the run's power stays recorded)", notes.Count == 0 && s.PowerOverride == theirs && s.Power == data.Class(Row).DefaultPower
        && run.PlayerPower(s) == theirs);
    var lost = NewAct(33); Grant(lost, "ULDA_306"); run.BeginMatch(lost);
    run.RecordOutcome(lost, juniors, DungeonRunOutcome.Loss, g.RunResult);
    Check("T22 a lost fight keeps nothing", lost.PowerOverride == null);
    Check("T22 a power the admission table does not admit for the run player is refused (DALA_BOSS_21p, a boss power that generates a Secret)",
        DungeonRun.PowerOverrideError("DALA_BOSS_21p") != null && DungeonRun.PowerOverrideError("CS2_034") == null && DungeonRun.PowerOverrideError("CS2_182") != null);
    var bad = NewAct(34);
    var gx = StartFight(run.BuildPlan(bad), 5); RunHelpers.OverridePower(gx, "DALA_BOSS_21p");
    var badNotes = run.ApplyMatchResult(bad, gx.RunResult);
    Check("T22 ApplyMatchResult refuses such an override with a note", bad.PowerOverride == null && badNotes.Count == 1 && badNotes[0].StartsWith("power override refused"), string.Join("|", badNotes));
    // A passive boss power copied: it runs now, as at the start of a game.
    var (s2, _, g2, c2) = Fight("ULDA_306", 35);
    var passive = DungeonRunCardsGen.PassiveHeroPowers.First(id => DungeonRunContent.All.Any(c => c.ShellId == id && c.Kind == RunContentKind.PassivePower && c.EffectKey != null && c.Key.StartsWith("passive_")));
    new ReplaceHeroPower(C(passive)).Process(g2, g2.Player2, g2.Player2.Hero, null);
    var effect = DungeonRunContent.Get(DungeonRunContent.All.First(c => c.ShellId == passive).EffectKey).ShellId;
    Mana(g2.Player1, 10); Play(g2, c2); g2.DeathProcessingAndAuraUpdate();
    Check($"T22 a passive boss power ({passive}) copied runs at once: the player gets its effect ({effect})", g2.Player1.Hero.HeroPower.Card.Id == passive
        && (g2.Player1.AppliedEnchantments?.Any(e => e.Card.Id == effect) == true || g2.Player1.Hero.AppliedEnchantments?.Any(e => e.Card.Id == effect) == true), passive);
    // The next fight plays it.
    s.Phase = DungeonRunPhase.PreMatch;
    var next = run.BuildPlan(s);
    Check("T22 the next fight's plan deals the copied power", next.PlayerHeroPowerId == theirs);
}

// ---------------------------------------------------------------- T23 ULDA_307 Bauble of Beetles
{
    var (s, plan, g, card) = Fight("ULDA_307", 36);
    var p1 = g.Player1; Mana(p1, 10); ClearBoards(g);
    var order = new[] { "CS2_231", "CS2_182", "CS2_120", "CS2_200", "CS2_189" };
    foreach (var id in order) Kill(g, Board(g, p1, id));
    Kill(g, Board(g, g.Player2, "CS2_201"));
    Play(g, card);
    var back = p1.BoardZone.ToList();
    Check("T23 Bauble of Beetles: the first 4 friendly minions that died (in order), each with Reborn; not the 5th, not the boss's; the Twinspell copy in hand",
        back.Select(m => m.Card.Id).SequenceEqual(order.Take(4)) && back.All(m => m[GameTag.REBORN] == 1) && p1.HandZone.Any(x => x.Card.Id == "ULDA_307ts"), string.Join(",", back.Select(m => m.Card.Id)));
    Kill(g, back[0]);
    Check("T23 a resurrected Wisp dies and comes back (Reborn)", p1.BoardZone.Count(m => m.Card.Id == "CS2_231") == 1 && p1.BoardZone.Count == 4);
    Play(g, p1.HandZone.First(x => x.Card.Id == "ULDA_307ts"));
    Check("T23 the copy resurrects the same first four, board permitting (three more: 7)", p1.BoardZone.Count == 7 && p1.BoardZone.Skip(4).Select(m => m.Card.Id).SequenceEqual(order.Take(3)));
}

// ---------------------------------------------------------------- T24 ULDA_403 Ancient Reflections
{
    var (s, plan, g, card) = Fight("ULDA_403", 37);
    var p1 = g.Player1; Mana(p1, 10); ClearBoards(g);
    var foe = Board(g, g.Player2, "CS2_186");
    var mine = Board(g, p1, "CS2_231");
    Play(g, card, foe);
    var copies = p1.BoardZone.Where(m => m != mine).ToList();
    Check("T24 Ancient Reflections on an enemy 7/7: the player's board fills with 1/1 copies of it (Titan Hologram); the original stays",
        copies.Count == 6 && copies.All(m => m.Card.Id == "CS2_186" && m.AttackDamage == 1 && m.Health == 1 && Has(m, "ULDA_403e") && m.Controller == p1) && foe.Zone?.Type == Zone.PLAY,
        string.Join(",", copies.Select(m => m.AttackDamage + "/" + m.Health)));
}

// ---------------------------------------------------------------- T25 ULDA_406 Murky's Battle Horn
{
    var (s, plan, g, card) = Fight("ULDA_406", 38);
    var p1 = g.Player1; var p2 = g.Player2; Mana(p1, 10); ClearBoards(g);
    Play(g, card);
    var wave = p1.BoardZone.ToList();
    Check("T25 Murky's Battle Horn: 7 random Murlocs, each with Rush (To Mrrgl!); the player's horn enchantment waits", wave.Count == 7 && wave.All(m => m.Card.IsRace(Race.MURLOC) && m.IsRush
        && Has(m, "ULDA_406e")) && p1.AppliedEnchantments.Any(e => e.Card.Id == "ULDA_406e2"), string.Join(",", wave.Select(m => m.Card.Id)));
    foreach (var m in p1.BoardZone.ToArray()) Kill(g, m);
    NextTurn(g);
    Check("T25 at the start of the next turn it does it again: 7 more", p1.BoardZone.Count == 7 && p1.BoardZone.All(m => m.Card.IsRace(Race.MURLOC) && m.IsRush) && !p1.AppliedEnchantments.Any(e => e.Card.Id == "ULDA_406e2"));
    foreach (var m in p1.BoardZone.ToArray()) Kill(g, m);
    NextTurn(g);
    Check("T25 and only once", p1.BoardZone.Count == 0);
}

// ---------------------------------------------------------------- T26 ULDA_503 Advanced Targeting Monocle
{
    var (s, plan, g, card) = Fight("ULDA_503", 39);
    var p1 = g.Player1; var p2 = g.Player2; Mana(p1, 10); ClearBoards(g);
    foreach (var e in p1.DeckZone.ToArray()) { p1.DeckZone.Remove(e); p1.SetasideZone.Add(e); }
    foreach (var id in new[] { "EX1_277", "CS2_008", "EX1_277", "ULDA_503", "CS2_182" })
    {
        var e = Entity.FromCard(p1, C(id)); p1.DeckZone.Add(e);
    }
    int hp = p2.Hero.Health + p2.Hero.Armor, own = p1.Hero.Health + p1.Hero.Armor;
    Play(g, card);
    int toBoss = hp - (p2.Hero.Health + p2.Hero.Armor), toMe = own - (p1.Hero.Health + p1.Hero.Armor);
    Check("T26 the Monocle casts a copy of each spell in the deck with random targets (two Arcane Missiles at the boss, 6, and a Moonfire at a random hero, 1: 7 in all); not another Monocle; the deck stays",
        toBoss + toMe == 7 && toBoss >= 6 && p1.DeckZone.Count == 5 && p1.DeckZone.Count(x => x.Card.Id == "EX1_277") == 2, $"boss {toBoss} me {toMe}");
}

// ---------------------------------------------------------------- T27 ULDA_504 Tea Time
{
    var (s, plan, g, card) = Fight("ULDA_504", 40);
    var p1 = g.Player1; Mana(p1, 10);
    int mana = p1.BaseMana, hand = p1.HandZone.Count;
    Play(g, card);
    Check("T27 Tea Time changes nothing in this match and asks for 4 Mana Crystals and 2 draws next fight", p1.BaseMana == mana && p1.HandZone.Count == hand - 1
        && g.RunResult?.NextFight?.Mana == 4 && g.RunResult.NextFight.Draws == 2);
    run.BeginMatch(s);
    run.RecordOutcome(s, juniors, DungeonRunOutcome.Win, g.RunResult);
    Check("T27 a won fight keeps it for the next fight", s.NextFight?.Mana == 4 && s.NextFight.Draws == 2);
    // Walk the run to its next pre-match (drafts picked in order).
    int guard = 0;
    while (s.Phase != DungeonRunPhase.PreMatch && guard++ < 40)
    {
        switch (s.Phase)
        {
            case DungeonRunPhase.TreasureDraft: run.PickTreasure(s, s.OpenDraft.Offered[0]); break;
            case DungeonRunPhase.BundleDraft:
            {
                var d = s.OpenDraft;
                if (d.Takes == 0) { run.PickBundle(s, d.Offered[0]); break; }
                string bundle = d.Choosing ?? d.Offered[0];
                var left = run.OfferCards(d, bundle);
                foreach (string k in d.Chosen) left.Remove(k);
                run.PickDraftCard(s, bundle, left[0]);
                break;
            }
            case DungeonRunPhase.Modifier: run.PickModifier(s, s.ModifierOffer.Offered[0]); break;
            case DungeonRunPhase.Tavern: run.LeaveTavern(s); break;
            default: guard = 99; break;
        }
    }
    var next = run.BuildPlan(s);
    var g2 = StartFight(next, run.MatchSeed(s));
    var plain = new RunMatchPlan();
    Check("T27 the next fight's plan carries the bonus: 4 more starting Mana Crystals and 2 more cards in the opening hand",
        s.Phase == DungeonRunPhase.PreMatch && next.NextFight?.Mana == 4 && next.NextFight.Draws == 2 && next.RunContext.NextFight?.Mana == 4
        && g2.Player1.BaseMana >= 5 && g2.Player1.HandZone.Count >= 3 + 2 + 1, $"phase {s.Phase} mana {g2.Player1.BaseMana} hand {g2.Player1.HandZone.Count}");
    run.BeginMatch(s);
    run.RecordOutcome(s, juniors, DungeonRunOutcome.Win, null);
    Check("T27 once that fight is recorded the bonus is spent (for the next boss only)", s.NextFight == null);
}

// ---------------------------------------------------------------- T28 ULDA_115 stays rejected (phase 8)
{
    var rule = DungeonRunAdmission.RuleFor("ULDA_115");
    var v = DungeonRunAdmission.Classify(C("ULDA_115"), "ULDA_115", AdmitScope.Player, null, catalogRun: true);
    Check("T28 ULDA_115 Gnomebliterator: unscripted, no content and no treasure row; its admission row (finale-state) admits no scope and names phase 8",
        !C("ULDA_115").Implemented && !DungeonRunContent.TryGet("ULDA_115", out _) && data.FindTreasure("ULDA_115") == null && rule?.Family == "finale-state"
        && rule.Admit == AdmitScope.None && !v.Admitted && rule.Lift.StartsWith("phase 8") && rule.Reason.Contains("Plague Lord"));
}

// ---------------------------------------------------------------- T29 determinism: the same seed, the same random results
{
    string Trace(long seed)
    {
        var (s, plan, g, card) = Fight("ULDA_406", seed);
        Mana(g.Player1, 10); ClearBoards(g); Play(g, card);
        var (_, _, g2, c2) = Fight("ULDA_118", seed);
        Mana(g2.Player1, 10); Play(g2, c2);
        return string.Join(",", g.Player1.BoardZone.Select(m => m.Card.Id)) + "|" + string.Join(",", g2.Player2.HandZone.Select(x => x.Card.Id)) + "|" + g2.Player2.DeckZone.Count;
    }
    Check("T29 the same seed gives the same Murlocs and the same Flex-plosion; another seed another", Trace(41) == Trace(41) && Trace(41) != Trace(42));
}

// ---------------------------------------------------------------- T30 full games against the real boss AI, the player playing the treasure
var testBudget = new Budget(6000, 2000, 300, 6, 3, 100, 8, turnSearchSeconds: 6, turnWallSeconds: 8);
var friendlyTarget = new HashSet<string> { "ULDA_012", "ULDA_403", "ULDA_112" };
string AiGame(string id, long seed)
{
    var s = NewAct(seed);
    Grant(s, id);
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
    var p1 = game.Player1;
    if (!p1.HandZone.Any(p => p.Card.Id == id))
    {
        var e = p1.DeckZone.First(p => p.Card.Id == id);
        p1.DeckZone.Remove(e);
        if (p1.HandZone.IsFull) { var drop = p1.HandZone.Last(); p1.HandZone.Remove(drop); p1.DeckZone.Add(drop); }
        Generic.AddHandPhase.Invoke(p1, e);
    }
    int cardId = p1.HandZone.First(p => p.Card.Id == id).Id;
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
            bool wantFriendly = friendlyTarget.Contains(id);
            var mine = options.OfType<PlayCardTask>().Where(o => o.Source?.Id == cardId)
                .OrderByDescending(o => o.Target == null ? 0 : (o.Target.Controller == game.Player1) == wantFriendly ? 1 : 0).FirstOrDefault();
            if (mine != null) { played = true; Act(mine, "player"); }
            else Act(LocalBot.Choose(options), "player");
        }
    }
    string result = game.State != State.COMPLETE ? "turn-cap" : game.Player2.PlayState == PlayState.WON ? "boss" : game.Player1.PlayState == PlayState.WON ? "player" : "tie";
    return $"{result} turns {game.Turn} played {(played ? 1 : 0)} player-choices {playerChoices} decisions {decisions} fallbacks {fallbacks} boss {boss.Id}";
}
foreach (string id in rows)
{
    string line;
    try { line = AiGame(id, 281); }
    catch (Exception e) { line = "FAULT " + e.GetType().Name + ": " + e.Message.Split('\n')[0]; }
    Console.WriteLine($"T30 {id,-9} {line}");
    Check($"T30 {id}: the player plays it in a full game against the boss AI, which ends without a fault or an AI fallback",
        !line.StartsWith("FAULT") && line.Contains("fallbacks 0 ") && line.Contains("played 1 "), line);
}

Console.WriteLine($"{passes} passed, {failures} failed");
return failures == 0 ? 0 : 1;
