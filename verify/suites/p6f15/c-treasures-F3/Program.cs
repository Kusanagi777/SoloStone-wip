// c-treasures-F3: phase 6 family F3 of the unified Dungeon Run, the Witchwood Monster Hunt heroes Tess Greymane and Darius
// Crowley: their portraits (GILA_500h3, GILA_600h) as hero rows of run-v1.json (heroRows: Tess ROGUE with Scavenge first,
// Darius WARRIOR with Fire! first, each with the class basic power unlocked at floor 4 and one authored starter, an adaptation
// because the original starters are server-side), their powers GILA_500p2 Scavenge and GILA_600p Fire!, Darius's Cannons
// (GILA_601, the Cannons sub-system: a Cannon at each board edge at the Start of Game, minions placed between them, shots at
// the enemies opposite, a shared fire counter) and their hero treasures GILA_501, 503, 506-510, 513, 602-605, 607, 608,
// 610-612, with the tokens GILA_500p2t and GILA_500t/t2/t3. For every id: the engine content (scripted, keyed and shelled by
// its own client id, name and text as facts/violet-hold-2026-09.json has them), the run data (hero rows, starters and treasure
// rows scoped to the hero's starter), the validator and admission checks, a scripted regression game through the run setup
// path (DungeonRun.NewRun on the hero row -> BuildPlan -> DungeonRunSetup.Configure -> Game -> ApplyAfterStart -> mulligan ->
// MainBegin -> the card drawn from the run deck and played), and a full game against the real boss AI (DungeonRunBossPilot +
// GoldenPilot) in which the player plays it; it must end without a fault or an AI fallback. Headless: compiles
// C:\SoloStone\plugin\DungeonRun\*.cs and the NPC AI against p6f3/engine-out. Run it twice and compare the output.
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
var data = DungeonRunData.Parse(runJson, poolJson, npcJson, catalogJson, scheduleJson, tavernJson);
var run = new DungeonRun(data);
var all = new DungeonRunProfile(); all.Unlocked.AddRange(data.Unlocks.Keys);
var facts = System.Text.Json.JsonDocument.Parse(File.ReadAllText(VerifyPaths.Repo("facts/violet-hold-2026-09.json")));
var factById = facts.RootElement.GetProperty("cards").EnumerateArray().ToDictionary(c => c.GetProperty("id").GetString(), c => c);
string FactText(string id) => factById[id].TryGetProperty("text", out var t) ? t.GetString() : null;
int FactTag(string id, int tag)
{
    foreach (var pair in factById[id].GetProperty("tags").EnumerateArray())
        if (pair[0].GetInt32() == tag) return pair[1].GetInt32();
    return 0;
}
Card C(string id) => Cards.FromId(id);
var clientIds = new HashSet<string>(System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "dungeon-run/reference/client-tables.json")))
    .RootElement.GetProperty("tables").GetProperty("CARD").EnumerateArray().Select(r => r.GetProperty("id").GetString()), StringComparer.Ordinal);

const string Tess = "GILA_500h3", Darius = "GILA_600h", TessDeck = "mh_tess_scavenger", DariusDeck = "mh_darius_militia";
string[] f3 = { "GILA_500h3", "GILA_500p2", "GILA_501", "GILA_503", "GILA_506", "GILA_507", "GILA_508", "GILA_509", "GILA_510", "GILA_513",
    "GILA_600h", "GILA_600p", "GILA_601", "GILA_602", "GILA_603", "GILA_604", "GILA_605", "GILA_607", "GILA_608", "GILA_610", "GILA_611", "GILA_612" };
string[] tokens = { "GILA_500p2t", "GILA_500t", "GILA_500t2", "GILA_500t3" };
string[] tessTreasures = { "GILA_501", "GILA_503", "GILA_506", "GILA_507", "GILA_508", "GILA_509", "GILA_510", "GILA_513" };
string[] dariusTreasures = { "GILA_602", "GILA_603", "GILA_604", "GILA_605", "GILA_607", "GILA_608", "GILA_610", "GILA_611", "GILA_612" };
string[] passives = { "GILA_506", "GILA_513", "GILA_602", "GILA_603", "GILA_605", "GILA_607" };
var effectShells = new Dictionary<string, string>
{
    ["fx_GILA_503e"] = "GILA_503e", ["fx_GILA_507e"] = "GILA_507e", ["fx_GILA_506"] = "GILA_506e", ["fx_GILA_513"] = "GILA_513e",
    ["fx_GILA_602"] = "GILA_602e2", ["fx_GILA_602e3"] = "GILA_602e3", ["fx_GILA_603"] = "GILA_603e2", ["fx_GILA_603e3"] = "GILA_603e3",
    ["fx_GILA_604"] = "GILA_604e2", ["fx_GILA_605"] = "GILA_605e2", ["fx_GILA_607"] = "GILA_607e2",
};
CardType FactType(string id) => FactTag(id, 202) switch { 3 => CardType.HERO, 4 => CardType.MINION, 5 => CardType.SPELL, 6 => CardType.ENCHANTMENT, 7 => CardType.WEAPON, 10 => CardType.HERO_POWER, _ => CardType.INVALID };

// ---------------------------------------------------------------- T1 content: scripted, own shells, client name, text, type, cost and stats
Check("T1 22 F3 ids", f3.Length == 22 && f3.Distinct().Count() == 22);
foreach (string id in f3.Concat(tokens))
{
    var content = DungeonRunContent.Get(id);
    var card = C(id);
    RunContentKind want = id == Tess || id == Darius ? RunContentKind.Hero : id == "GILA_500p2" || id == "GILA_600p" ? RunContentKind.HeroPower
        : passives.Contains(id) ? RunContentKind.PassiveTreasure : RunContentKind.ActiveTreasure;
    Check($"T1 {id}: a {want} keyed and shelled by its own id, scripted, the client's {FactType(id)}",
        content.Kind == want && content.ShellId == id && card != null && card.Implemented && card.Type == FactType(id)
        && DungeonRunContent.ForShell(id) == content && clientIds.Contains(id), card == null ? "no card" : content.Kind + " " + card.Type + " " + card.Implemented);
    Check($"T1 ids-text-match {id}: content name and text equal facts/violet-hold-2026-09.json",
        content.Name == factById[id].GetProperty("name").GetString() && content.Text == FactText(id), content.Name + " | " + content.Text);
    if (card.Type == CardType.HERO) continue;
    Check($"T1 {id}: cost {FactTag(id, 48)} as the client prints it" + (card.Type == CardType.MINION || card.Type == CardType.WEAPON ? $", stats {FactTag(id, 47)}/{(card.Type == CardType.WEAPON ? FactTag(id, 187) : FactTag(id, 45))}" : ""),
        card.Cost == FactTag(id, 48) && (want == RunContentKind.PassiveTreasure || content.Cost == FactTag(id, 48))
        && (card.Type == CardType.MINION ? card.ATK == FactTag(id, 47) && card.Health == FactTag(id, 45)
            : card.Type == CardType.WEAPON ? card.ATK == FactTag(id, 47) && card[GameTag.DURABILITY] == FactTag(id, 187) : true),
        $"{card.Cost} {card.ATK}/{card.Health}/{card[GameTag.DURABILITY]}");
}
Check("T1 portraits: Tess (ROGUE) and Darius (WARRIOR) are scripted run heroes at 30 Health",
    C(Tess).Implemented && C(Tess).Class == CardClass.ROGUE && C(Tess).Health == 30 && DungeonRunContent.Get(Tess).Health == 30
    && C(Darius).Implemented && C(Darius).Class == CardClass.WARRIOR && C(Darius).Health == 30);
Check("T1 the Cannon is the client's untouchable, stat-hidden minion; Darius's portrait declares a Start of Game effect, Tess's none",
    C("GILA_601").Untouchable && C("GILA_601")[GameTag.HIDE_STATS] == 1 && SabberStoneCore.Actions.StartOfGameRules.HasEffect(C(Darius))
    && !SabberStoneCore.Actions.StartOfGameRules.HasEffect(C(Tess)));
foreach (var fx in effectShells)
{
    var e = DungeonRunContent.Get(fx.Key);
    Check($"T1 {fx.Value}: content {fx.Key} on the client's own ENCHANTMENT, scripted, name and text word for word",
        e.Kind == RunContentKind.Effect && e.ShellId == fx.Value && C(fx.Value).Implemented && C(fx.Value).Type == CardType.ENCHANTMENT && clientIds.Contains(fx.Value)
        && e.Name == factById[fx.Value].GetProperty("name").GetString() && e.Text == FactText(fx.Value), e.Name + " | " + e.Text);
}
{
    var harbor = DungeonRunContent.Get("fx_GILA_608");
    Check("T1 GILA_608's (0) discount: a borrowed Battlegrounds enchantment shell the client knows, with the effect's own text",
        harbor.Kind == RunContentKind.Effect && harbor.ShellId == "TB_BaconShopBadsongE" && clientIds.Contains("TB_BaconShopBadsongE") && C("TB_BaconShopBadsongE").Implemented
        && harbor.Text == "Costs (0).");
}
Check("T1 tokens: GILA_510 lists Tess's three weapons, GILA_610 the Cannon",
    DungeonRunContent.Get("GILA_510").Tokens.SequenceEqual(new[] { "GILA_500t", "GILA_500t2", "GILA_500t3" }) && DungeonRunContent.Get("GILA_610").Tokens.SequenceEqual(new[] { "GILA_601" }));

// ---------------------------------------------------------------- T2 data, validator, admission
var transport = new Dictionary<string, string>
{
    ["CS2_034"] = "HERO_08bp", ["CS2_102"] = "HERO_01bp", ["CS1h_001"] = "HERO_09bp", ["CS2_101"] = "HERO_04bp", ["CS2_017"] = "HERO_06bp",
    ["DS1h_292"] = "HERO_05bp", ["CS2_083b"] = "HERO_03bp", ["CS2_056"] = "HERO_07bp", ["CS2_049"] = "HERO_02bp",
    ["AT_132_WARRIOR"] = "HERO_01bp2", ["AT_132_SHAMAN"] = "HERO_02bp2", ["AT_132_ROGUE"] = "HERO_03bp2", ["AT_132_PALADIN"] = "HERO_04bp2",
    ["AT_132_HUNTER"] = "HERO_05bp2", ["AT_132_DRUID"] = "HERO_06bp2", ["AT_132_WARLOCK"] = "HERO_07bp2", ["AT_132_MAGE"] = "HERO_08bp2", ["AT_132_PRIEST"] = "HERO_09bp2",
};
{
    var errors = DungeonRunValidator.Validate(data, id => clientIds.Contains(transport.TryGetValue(id, out var sent) ? sent : id));
    Check("T2 shipped data validates with 0 errors (client check through the transported id)", errors.Count == 0, string.Join(" | ", errors.Take(5)));
    var legacy = DungeonRunData.Parse(runJson, poolJson, npcJson);
    var le = DungeonRunValidator.Validate(legacy, null);
    Check("T2 the data without the catalog validates with 0 errors and loads no hero row (hero rows load with the catalog only)",
        le.Count == 0 && legacy.FindClass(Tess) == null && legacy.FindClass(Darius) == null, string.Join(" | ", le.Take(3)));
    foreach (var (row, cls, power, basic, deck) in new[] { (Tess, "ROGUE", "GILA_500p2", "CS2_083b", TessDeck), (Darius, "WARRIOR", "GILA_600p", "CS2_102", DariusDeck) })
    {
        var r = data.FindClass(row);
        Check($"T2 hero row {row}: kind hero, MonsterHunt, its own portrait, 30 Health, classes [{cls}], enabled",
            r != null && r.IsHero && r.Adventure == "MonsterHunt" && r.HeroCard == row && r.Health == 30 && r.ClassSet.SequenceEqual(new[] { cls }) && r.Enabled
            && data.ClassRows.All(c => c.Id != row));
        Check($"T2 hero row {row}: the special power {power} first (no unlock), the {cls} basic power {basic} unlocked at floor 4",
            r.Powers.SequenceEqual(new[] { power, basic }) && r.PowerUnlock(power) == null && r.PowerUnlock(basic) == row.ToLowerInvariant() + "_floor4"
            && data.Unlocks.ContainsKey(row.ToLowerInvariant() + "_floor4"));
        var d = data.FindDeck(deck);
        Check($"T2 hero row {row}: one authored starter {deck} of 10 scripted {cls} or neutral cards",
            r.Decks.SequenceEqual(new[] { deck }) && d.Class == row && d.Cards.Count == 10 && !d.IsGenerated
            && d.Cards.All(id => C(id) != null && C(id).Implemented && (C(id).Class.ToString() == cls || C(id).Class == CardClass.NEUTRAL)),
            string.Join(",", d?.Cards ?? new List<string>()));
    }
    foreach (string id in tessTreasures.Concat(dariusTreasures))
    {
        var t = data.FindTreasure(id);
        bool passive = passives.Contains(id);
        string deck = tessTreasures.Contains(id) ? TessDeck : DariusDeck;
        Check($"T2 {id}: an {(passive ? "passive" : "active")} act-run treasure row scoped to {deck}, MonsterHunt pool",
            t != null && t.Active == !passive && t.Offered && !t.Legacy && !t.Signature && t.Runs == DungeonRunTreasure.RunsAct && t.Pool == "MonsterHunt"
            && t.Scope.Decks.SequenceEqual(new[] { deck }) && t.Scope.Classes.Count == 0 && t.Scope.Powers.Count == 0);
    }
    // Phase 8 family 23: GILA_503 Hunter's Insight is in Lord Godfrey's nemesis deck, so its row (run-treasure-nemesis-deck) admits a
    // boss on a catalog run too; it is checked on its own below, as strictly.
    foreach (string id in f3.Concat(tokens).Where(i => i != Tess && i != Darius && !passives.Contains(i) && i != "GILA_503"))
    {
        var p = DungeonRunAdmission.Classify(C(id), id, AdmitScope.Player, null, catalogRun: true);
        var b = DungeonRunAdmission.Classify(C(id), id, AdmitScope.Boss, null, catalogRun: true);
        // Phase 8 family 15: Mama Diggs's Excavate finds A Small Rock, so a catalog run admits GILA_500p2t for a boss too (its legacy verdict stays the player's).
        bool rockForBoss = id == "GILA_500p2t" && DungeonRunAdmission.RuleFor(id)?.Family == "run-treasure-boss-find" && !DungeonRunAdmission.Classify(C(id), id, AdmitScope.Boss).Admitted;
        Check($"T2 admission {id}: a row admitting the player (catalog run), never the boss{(id == "GILA_500p2t" ? " (but Mama Diggs's Excavate, family 15, on a catalog run)" : "")}",
            p.Subject && p.Admitted && (!b.Admitted || rockForBoss && b.Admitted) && DungeonRunAdmission.RuleFor(id) != null, p.Family + " " + p.Admit);
        Check($"T2 admission {id}: the boss's generation never yields it", DungeonRunAdmission.GeneratedRejectsBoss(true).Contains(id));
    }
    {
        var p = DungeonRunAdmission.Classify(C("GILA_503"), "GILA_503", AdmitScope.Player, null, catalogRun: true);
        var b = DungeonRunAdmission.Classify(C("GILA_503"), "GILA_503", AdmitScope.Boss, null, catalogRun: true);
        Check("T2 admission GILA_503: the run-treasure-nemesis-deck row (phase 8 family 23) admits the player on legacy and catalog runs, a boss on a catalog run only (Lord Godfrey's deck)",
            p.Subject && p.Admitted && DungeonRunAdmission.Classify(C("GILA_503"), "GILA_503", AdmitScope.Player).Admitted && b.Admitted
            && !DungeonRunAdmission.Classify(C("GILA_503"), "GILA_503", AdmitScope.Boss).Admitted && DungeonRunAdmission.RuleFor("GILA_503")?.Family == "run-treasure-nemesis-deck",
            p.Family + " " + p.Admit + " / boss " + b.Admit);
        Check("T2 admission GILA_503: the boss's generation never yields it", DungeonRunAdmission.GeneratedRejectsBoss(true).Contains("GILA_503"));
    }
    Check("T2 admission GILA_509: the secret-generated row, admitted for the player on catalog runs only (legacy runs block generated Secrets)",
        DungeonRunAdmission.RuleFor("GILA_509").Family == "secret-generated" && !DungeonRunAdmission.Classify(C("GILA_509"), "GILA_509", AdmitScope.Player).Admitted
        && DungeonRunAdmission.Classify(C("GILA_509"), "GILA_509", AdmitScope.Player, null, catalogRun: true).Admitted
        && !DungeonRunAdmission.Classify(C("GILA_509"), "GILA_509", AdmitScope.Boss, null, catalogRun: true).Admitted);
    Check("T2 admission families: powers run-hero-power, Cannon run-hero-board, cards run-treasure",
        DungeonRunAdmission.RuleFor("GILA_500p2").Family == "run-hero-power" && DungeonRunAdmission.RuleFor("GILA_600p").Family == "run-hero-power"
        && DungeonRunAdmission.RuleFor("GILA_601").Family == "run-hero-board" && DungeonRunAdmission.RuleFor("GILA_612").Family == "run-treasure");
    var genClasses = DungeonRunAdmission.SecretGeneratorClasses(data, new[] { "ROGUE" }, _ => false);
    var prior = DungeonRunAdmission.CatalogSecretPrior(data, new[] { "ROGUE" });
    var hunterPool = DungeonRunAdmission.GeneratedSecretPool(new[] { "HUNTER" });
    Check("T2 the boss's Secret prior for a Rogue catalog run covers every Hunter Secret GILA_509 can put into play (a neutral generator naming its class)",
        genClasses.Contains("HUNTER") && prior != null && hunterPool.Count > 5 && hunterPool.All(prior.ContainsKey), string.Join(",", genClasses) + " pool " + hunterPool.Count);
    string badScope = runJson.Replace("\"scope\": {\"decks\": [\"mh_tess_scavenger\"]}}", "\"scope\": {\"decks\": [\"mh_tess_nothing\"]}}");
    var eScope = DungeonRunValidator.Validate(DungeonRunData.Parse(badScope, poolJson, npcJson, catalogJson, scheduleJson, tavernJson), null);
    Check("T2 validator: a treasure scoped to an unknown deck is an error", badScope != runJson && eScope.Any(e => e.Contains("unknown deck mh_tess_nothing")), string.Join(" | ", eScope.Take(2)));
    string badPower = runJson.Replace("\"powers\": [\"GILA_600p\", \"CS2_102\"], \"powerUnlocks\": {\"CS2_102\": \"gila_600h_floor4\"}", "\"powers\": [\"CS2_102\", \"GILA_600p\"], \"powerUnlocks\": {}");
    var ePower = DungeonRunValidator.Validate(DungeonRunData.Parse(badPower, poolJson, npcJson, catalogJson, scheduleJson, tavernJson), null);
    Check("T2 validator: a second power with no unlock is an error", badPower != runJson && ePower.Any(e => e.Contains("GILA_600p has no unlock")), string.Join(" | ", ePower.Take(2)));
}

// ---------------------------------------------------------------- the run setup path
GameConfig Configured(RunMatchPlan plan, int seed)
{
    var cfg = new GameConfig { RandomSeed = seed, Shuffle = true, SkipMulligan = false, History = true, Logging = false, FormatType = FormatType.FT_WILD };
    DungeonRunSetup.Configure(cfg, plan);
    return cfg;
}
DungeonRunState NewAct(string row, long seed, string power = null)
{
    var cls = data.Class(row);
    var s = run.NewRun(all, cls.Id, cls.DefaultDeck, power ?? cls.DefaultPower, seed, signatureId: run.SignatureOffer(all, run.Data.FindClass(cls.Id), seed).FirstOrDefault());
    if (!s.IsActRun || s.Phase != DungeonRunPhase.PreMatch || s.Floor != 1) throw new Exception("act run not at floor 1 pre-match");
    return s;
}
// A pick as DungeonRun.PickTreasure records it: an active joins the run deck, a passive the run's treasures.
void Hold(DungeonRunState s, params string[] ids)
{
    foreach (string id in ids)
    {
        if (data.FindTreasure(id).Active) { s.ActiveTreasures.Add(id); DungeonRunDeck.Add(s, DungeonRunContent.Get(id).ShellId, DungeonRunDeck.OriginTreasure); }
        else s.Treasures.Add(id);
    }
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
(DungeonRunState s, RunMatchPlan plan, Game g) Fight(string row, long seed, string[] treasures, string power = null)
{
    var s = NewAct(row, seed, power);
    Hold(s, treasures);
    var plan = run.BuildPlan(s);
    return (s, plan, StartFight(plan, run.MatchSeed(s)));
}
IPlayable Draw(Game g, string id)
{
    var p = g.Player1;
    var e = p.HandZone.FirstOrDefault(x => x.Card.Id == id);
    if (e != null) return e;
    e = p.DeckZone.FirstOrDefault(x => x.Card.Id == id) ?? throw new Exception(id + " is not in the run deck");
    Generic.Draw(p, e);
    g.DeathProcessingAndAuraUpdate();
    return e;
}
void Mana(Controller c, int n) { c.BaseMana = n; c.UsedMana = 0; }
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
bool Play(Game g, IPlayable card, ICharacter target = null, int pos = -1) => g.Process(PlayCardTask.Any(g.Player1, card, target, pos));
bool UsePower(Game g, ICharacter target = null) => g.Process(HeroPowerTask.Any(g.Player1, target));
void EndTurn(Game g) => g.Process(EndTurnTask.Any(g.CurrentPlayer));
void Clear(Controller c) { foreach (var e in c.HandZone.ToArray()) { c.HandZone.Remove(e); c.SetasideZone.Add(e); } }
string Slots(Controller c) => string.Join(" ", c.BoardZone.Slots.Select(e => e.Card.Id));
bool FromRunDeck(DungeonRunState s, RunMatchPlan plan, string id) => plan.PlayerDeck.Any(c => c.Id == id) && s.Deck.Contains(id);

// ---------------------------------------------------------------- T3 NewRun / BuildPlan on the hero rows; offers on hero runs only
{
    foreach (var (row, power, deck, basic) in new[] { (Tess, "GILA_500p2", TessDeck, "CS2_083b"), (Darius, "GILA_600p", DariusDeck, "CS2_102") })
    {
        var s = run.NewRun(all, row, deck, power, 5, signatureId: run.SignatureOffer(all, run.Data.FindClass(row), 5).FirstOrDefault());
        var plan = run.BuildPlan(s);
        Check($"T3 NewRun {row}: an act catalog run of the hero row, its starter in the run deck (origin starter), its power",
            s.Class == row && s.IsActRun && s.IsCatalogRun && s.StarterDeck == deck && s.Power == power && s.Deck.SequenceEqual(data.FindDeck(deck).Cards)
            && s.Cards.All(c => c.Origin == DungeonRunDeck.OriginStarter));
        Check($"T3 BuildPlan {row}: the portrait at 30 Health, {power}, the run player's generated Secrets on (catalog run)",
            plan.PlayerHero.Id == row && plan.PlayerHeroHealth == 30 && plan.PlayerHeroPowerId == power && plan.PlayerGeneratedSecrets
            && plan.PlayerDeck.Select(c => c.Id).OrderBy(x => x).SequenceEqual(data.FindDeck(deck).Cards.OrderBy(x => x)));
        bool locked = false;
        try { run.NewRun(new DungeonRunProfile(), row, deck, basic, 5, signatureId: run.SignatureOffer(new DungeonRunProfile(), run.Data.FindClass(row), 5).FirstOrDefault()); } catch (InvalidOperationException) { locked = true; }
        Check($"T3 {row}: the basic power {basic} needs floor 4 with the hero", locked && run.NewRun(all, row, deck, basic, 5, signatureId: run.SignatureOffer(all, run.Data.FindClass(row), 5).FirstOrDefault()).Power == basic);
        var g = new Game(Configured(plan, 77));
        g.StartGame();
        Check($"T3 {row}: the match starts the portrait at 30 Health with {power}", g.Player1.Hero.Card.Id == row && g.Player1.Hero.Health == 30
            && g.Player1.Hero.HeroPower.Card.Id == power, g.Player1.Hero.Card.Id + " " + g.Player1.Hero.Health + " " + g.Player1.Hero.HeroPower.Card.Id);
    }
    var offered = new Dictionary<string, HashSet<string>>();
    foreach (var cls in data.Classes)
    {
        var set = offered[cls.Id] = new HashSet<string>();
        for (long seed = 1; seed <= 200; seed++)
        {
            var s = run.NewRun(all, cls.Id, cls.DefaultDeck, cls.DefaultPower, seed, signatureId: run.SignatureOffer(all, run.Data.FindClass(cls.Id), seed).FirstOrDefault());
            if (run.OpenTreasureDraft(s)) set.UnionWith(s.OpenDraft.Offered);
        }
    }
    foreach (string id in tessTreasures) Check($"T3 {id} is offered to Tess runs", offered[Tess].Contains(id));
    foreach (string id in dariusTreasures) Check($"T3 {id} is offered to Darius runs", offered[Darius].Contains(id));
    Check("T3 no other run is offered a Tess or Darius treasure (deck scope)",
        offered.Where(p => p.Key != Tess && p.Key != Darius).All(p => !p.Value.Overlaps(tessTreasures.Concat(dariusTreasures)))
        && !offered[Tess].Overlaps(dariusTreasures) && !offered[Darius].Overlaps(tessTreasures),
        string.Join(";", offered.Select(p => p.Key + ":" + string.Join(",", p.Value.Where(x => x.StartsWith("GILA_"))))));
    var sp = NewAct(Darius, 9);
    run.OpenTreasureDraft(sp);
    sp.OpenDraft.Offered[0] = "GILA_612";
    int before = sp.Deck.Count;
    run.PickTreasure(sp, "GILA_612");
    Check("T3 PickTreasure GILA_612 on a Darius run: the run deck gains the card (origin treasure)",
        sp.Deck.Count == before + 1 && sp.Cards.Last().Id == "GILA_612" && sp.Cards.Last().Origin == DungeonRunDeck.OriginTreasure && sp.ActiveTreasures.Contains("GILA_612"));
}

// ---------------------------------------------------------------- T4 GILA_500p2 Scavenge (+ GILA_500p2t A Small Rock)
{
    var (s, plan, g) = Fight(Tess, 101, new string[0]);
    var p1 = g.Player1; var p2 = g.Player2;
    Mana(p1, 10); Clear(p1);
    Check("T4 Scavenge: nothing played yet, no candidates", DungeonRunContent.ScavengeCandidates(p1).Count == 0);
    bool used = UsePower(g);
    var rock = p1.HandZone.FirstOrDefault(e => e.Card.Id == "GILA_500p2t");
    Check("T4 Scavenge with no class spell played this game: A Small Rock, the power exhausted, no Discover", used && rock != null && p1.Choice == null
        && p1.Hero.HeroPower.IsExhausted && p1.HandZone.Count == 1);
    var yeti = Board(g, p2, "CS2_182");
    Play(g, rock, yeti);
    Check("T4 A Small Rock deals 1 damage", yeti.Health == 4 && !DungeonRunContent.ScavengeCandidates(p1).Any(), yeti.Health.ToString());
    var stab = ToHand(g, p1, "CS2_072");
    Check("T4 the Backstab is played (an undamaged target)", Play(g, stab, Board(g, p2, "CS2_182")));
    EndTurn(g);
    Mana(p2, 10);
    var fireball = ToHand(g, p2, "CS2_029");
    g.Process(PlayCardTask.Any(p2, fireball, p1.Hero));
    EndTurn(g);
    Mana(p1, 10); Clear(p1);
    var pool = DungeonRunContent.ScavengeCandidates(p1).Select(c => c.Id).ToList();
    Check("T4 Scavenge's pool: the class spells both players played this game (my Backstab, the boss's Fireball), not the neutral Rock",
        pool.SequenceEqual(new[] { "CS2_029", "CS2_072" }), string.Join(",", pool));
    UsePower(g);
    var offers = p1.Choice?.Choices.Select(id => g.IdEntityDic[id].Card.Id).OrderBy(x => x).ToList() ?? new List<string>();
    Check("T4 Scavenge: a Discover of exactly those spells", offers.SequenceEqual(new[] { "CS2_029", "CS2_072" }), string.Join(",", offers));
    int pick = p1.Choice.Choices.First(id => g.IdEntityDic[id].Card.Id == "CS2_029");
    g.Process(ChooseTask.Pick(p1, pick));
    Check("T4 Scavenge: the picked Fireball is in my hand", p1.HandZone.Any(e => e.Card.Id == "CS2_029") && p1.Choice == null);
}

// ---------------------------------------------------------------- T5 GILA_501 Exterminate
{
    var (s, plan, g) = Fight(Tess, 111, new[] { "GILA_501" });
    Check("T5 GILA_501 is a card of the run deck the match plan deals", FromRunDeck(s, plan, "GILA_501"));
    var ex = Draw(g, "GILA_501"); Mana(g.Player1, 10);
    var yeti = Board(g, g.Player2, "CS2_182");
    int hp = g.Player2.Hero.Health;
    Play(g, ex, yeti);
    var echo = g.Player1.HandZone.FirstOrDefault(e => e.Card.Id == "GILA_501");
    Check("T5 GILA_501: the 5-Health Yeti dies, the 5 excess hits the boss hero", yeti.IsDead && g.Player2.Hero.Health == hp - 5, $"{hp}->{g.Player2.Hero.Health}");
    Check("T5 GILA_501 Echo: a copy is in hand this turn", echo != null && echo.IsEcho);
    var squire = Board(g, g.Player2, "EX1_008");
    int hp2 = g.Player2.Hero.Health;
    Play(g, echo, squire);
    Check("T5 GILA_501: a Divine Shield takes the 10 (no damage dealt, no excess)", g.Player2.Hero.Health == hp2 && !squire.HasDivineShield && !squire.IsDead);
    var last = g.Player1.HandZone.FirstOrDefault(e => e.Card.Id == "GILA_501");
    EndTurn(g);
    Check("T5 GILA_501: the Echo copy leaves the hand at end of turn", last != null && !g.Player1.HandZone.Any(e => e.Card.Id == "GILA_501"));
}

// ---------------------------------------------------------------- T6 GILA_503 Hunter's Insight
{
    var (s, plan, g) = Fight(Tess, 121, new[] { "GILA_503" });
    var insight = Draw(g, "GILA_503"); Mana(g.Player1, 10);
    var a = ToHand(g, g.Player1, "CS2_182"); var b = ToHand(g, g.Player1, "CS2_029"); var c = ToHand(g, g.Player1, "CS2_072");
    var boss = ToHand(g, g.Player2, "CS2_182");
    Play(g, insight);
    Check("T6 GILA_503: every card in my hand costs (3) less (Yeti 4->1, Fireball 4->1, Backstab 0), the boss's hand unchanged",
        a.Cost == 1 && b.Cost == 1 && c.Cost == 0 && boss.Cost == 4 && a.AppliedEnchantments.Any(e => e.Card.Id == "GILA_503e"), $"{a.Cost} {b.Cost} {c.Cost} {boss.Cost}");
    var later = ToHand(g, g.Player1, "CS2_182");
    Check("T6 GILA_503: a card drawn later keeps its cost", later.Cost == 4);
}

// ---------------------------------------------------------------- T7 GILA_506 First Aid Kit (passive)
{
    var (s, plan, g) = Fight(Tess, 131, new[] { "GILA_506" });
    var p1 = g.Player1;
    Check("T7 GILA_506: the player enchantment GILA_506e is attached at match start", p1.AppliedEnchantments.Any(e => e.Card.Id == "GILA_506e"));
    var yeti = Board(g, p1, "CS2_182"); yeti.Damage = 3; p1.Hero.Damage = 5;
    var boss = Board(g, g.Player2, "CS2_182"); boss.Damage = 3; g.Player2.Hero.Damage = 5;
    EndTurn(g);
    Check("T7 GILA_506: at the end of my turn every friendly character gets 2 Health back; the boss's do not",
        yeti.Health == 4 && p1.Hero.Health == 27 && boss.Health == 2 && g.Player2.Hero.Health == g.Player2.Hero.BaseHealth - 5, $"{yeti.Health} {p1.Hero.Health} {boss.Health}");
    EndTurn(g);
    Check("T7 GILA_506: not at the end of the boss's turn", yeti.Health == 4 && p1.Hero.Health == 27);
}

// ---------------------------------------------------------------- T8 GILA_507 Hunter of Old
{
    var (s, plan, g) = Fight(Tess, 141, new[] { "GILA_507" });
    var hunter = Draw(g, "GILA_507"); Mana(g.Player1, 10);
    Play(g, hunter);
    var m = (Minion)hunter;
    int hp = g.Player2.Hero.Health;
    var fb = ToHand(g, g.Player1, "CS2_029");
    Play(g, fb, g.Player2.Hero);
    Check("T8 GILA_507: casting a spell gives it Spell Damage +2 first, so the Fireball deals 8", m.SpellPower == 2 && g.Player2.Hero.Health == hp - 8, $"sp {m.SpellPower} {hp}->{g.Player2.Hero.Health}");
    var stab = ToHand(g, g.Player1, "CS2_072"); var target = Board(g, g.Player2, "CS2_182");
    Play(g, stab, target);
    Check("T8 GILA_507: each spell adds +2 (Spell Damage 4)", m.SpellPower == 4 && g.Player1.CurrentSpellPower == 4 && m.AppliedEnchantments.Count(e => e.Card.Id == "GILA_507e") == 2);
    EndTurn(g);
    var bossSpell = ToHand(g, g.Player2, "CS2_029"); Mana(g.Player2, 10); g.Process(PlayCardTask.Any(g.Player2, bossSpell, g.Player1.Hero));
    Check("T8 GILA_507: the boss's spells give it nothing", m.SpellPower == 4);
}

// ---------------------------------------------------------------- T9 GILA_508 The Exorcisor
{
    var (s, plan, g) = Fight(Tess, 151, new[] { "GILA_508" });
    var axe = Draw(g, "GILA_508"); Mana(g.Player1, 10);
    Play(g, axe);
    Check("T9 GILA_508: the 4/4 weapon is equipped", g.Player1.Hero.Weapon?.Card.Id == "GILA_508" && g.Player1.Hero.AttackDamage == 4 && g.Player1.Hero.Weapon.Durability == 4);
    var squire = Board(g, g.Player2, "EX1_008");
    g.Process(HeroAttackTask.Any(g.Player1, squire));
    Check("T9 GILA_508: the attacked Divine Shield minion is silenced first, so the 4 damage kills it", squire.IsDead && squire.IsSilenced, $"dead {squire.IsDead} silenced {squire.IsSilenced}");
    EndTurn(g); EndTurn(g);
    int hp = g.Player2.Hero.Health;
    g.Process(HeroAttackTask.Any(g.Player1, g.Player2.Hero));
    Check("T9 GILA_508: attacking the hero is a plain attack", g.Player2.Hero.Health == hp - 4);
}

// ---------------------------------------------------------------- T10 GILA_509 Frenzied Trapper
{
    string[] Secrets(long seed, bool generated)
    {
        var s = NewAct(Tess, seed); Hold(s, "GILA_509");
        var plan = run.BuildPlan(s); plan.PlayerGeneratedSecrets = generated;
        var g = StartFight(plan, run.MatchSeed(s));
        var trapper = Draw(g, "GILA_509"); Mana(g.Player1, 10);
        Play(g, trapper);
        Check($"T10 GILA_509 ({(generated ? "catalog" : "generated Secrets off")}): the 5/5 is on the board", g.Player1.BoardZone.Contains((Minion)trapper));
        var cands = new HashSet<string>(DungeonRunContent.RandomSecretCandidates(g.Player1, CardClass.HUNTER).Select(c => c.Id));
        Check($"T10 GILA_509 ({(generated ? "catalog" : "off")}): every Secret put into play is a scripted Hunter Secret of the candidates, distinct, not cast (no Secret-cast count)",
            g.Player1.SecretZone.All(x => x.Card.Class == CardClass.HUNTER && x.Card.IsSecret && x.Card.Implemented && cands.Contains(x.Card.Id))
            && g.Player1.SecretZone.Select(x => x.Card.Id).Distinct().Count() == g.Player1.SecretZone.Count);
        return g.Player1.SecretZone.Select(x => x.Card.Id).ToArray();
    }
    var a = Secrets(161, true); var b = Secrets(161, true); var off = Secrets(161, false);
    Check("T10 GILA_509: 5 Hunter Secrets on a Rogue (Tess) run", a.Length == 5, string.Join(",", a));
    Check("T10 GILA_509: the same seed puts the same Secrets (game RNG)", a.SequenceEqual(b));
    Check("T10 GILA_509: a match that blocks the player's generated Secrets puts none (a legacy run never offers it)", off.Length == 0);
    var prior = DungeonRunAdmission.CatalogSecretPrior(data, new[] { "ROGUE" });
    Check("T10 GILA_509: every Secret it put into play is in the boss's Secret prior for the run", a.All(prior.ContainsKey));
}

// ---------------------------------------------------------------- T11 GILA_510 Tools of the Trade (+ GILA_500t, t2, t3)
{
    var (s, plan, g) = Fight(Tess, 171, new[] { "GILA_510" });
    var tools = Draw(g, "GILA_510"); Mana(g.Player1, 10);
    Play(g, tools);
    var choice = g.Player1.Choice;
    var offers = choice?.Choices.Select(id => g.IdEntityDic[id].Card.Id).OrderBy(x => x).ToList() ?? new List<string>();
    Check("T11 GILA_510: a Discover of Tess's three weapons", offers.SequenceEqual(new[] { "GILA_500t", "GILA_500t2", "GILA_500t3" }), string.Join(",", offers));
    Check("T11 GILA_510: it returns to my hand", g.Player1.HandZone.Any(e => e.Card.Id == "GILA_510"));
    g.Process(ChooseTask.Pick(g.Player1, choice.Choices.First(id => g.IdEntityDic[id].Card.Id == "GILA_500t3")));
    Check("T11 GILA_510: the picked Blunderbuss (2/2) is equipped", g.Player1.Hero.Weapon?.Card.Id == "GILA_500t3" && g.Player1.Hero.AttackDamage == 2 && g.Player1.Hero.Weapon.Durability == 2);
    var l = Board(g, g.Player2, "CS2_182"); var mid = Board(g, g.Player2, "CS2_182"); var r = Board(g, g.Player2, "CS2_182");
    g.Process(HeroAttackTask.Any(g.Player1, mid));
    Check("T11 GILA_500t3 Blunderbuss: the hero's attack also damages the minions next to the target", mid.Health == 3 && l.Health == 3 && r.Health == 3, $"{l.Health} {mid.Health} {r.Health}");
    var again = g.Player1.HandZone.First(e => e.Card.Id == "GILA_510");
    Play(g, again);
    g.Process(ChooseTask.Pick(g.Player1, g.Player1.Choice.Choices.First(id => g.IdEntityDic[id].Card.Id == "GILA_500t")));
    Check("T11 GILA_500t Stake Thrower: 1/8 Windfury replaces the Blunderbuss", g.Player1.Hero.Weapon?.Card.Id == "GILA_500t" && g.Player1.Hero.Weapon.Durability == 8 && g.Player1.Hero.HasWindfury);
    EndTurn(g); EndTurn(g);
    int hp = g.Player2.Hero.Health;
    g.Process(HeroAttackTask.Any(g.Player1, g.Player2.Hero)); g.Process(HeroAttackTask.Any(g.Player1, g.Player2.Hero));
    Check("T11 GILA_500t: the hero attacks twice a turn", g.Player2.Hero.Health == hp - 2);
    Play(g, g.Player1.HandZone.First(e => e.Card.Id == "GILA_510"));
    g.Process(ChooseTask.Pick(g.Player1, g.Player1.Choice.Choices.First(id => g.IdEntityDic[id].Card.Id == "GILA_500t2")));
    Check("T11 GILA_500t2 Handgonne: a plain 4/2", g.Player1.Hero.Weapon?.Card.Id == "GILA_500t2" && g.Player1.Hero.Weapon.Durability == 2 && g.Player1.Hero.Weapon.AttackDamage == 4);
}

// ---------------------------------------------------------------- T12 GILA_600h / GILA_601 / GILA_600p: Darius's Cannons and Fire!
{
    var (s, plan, g) = Fight(Darius, 181, new string[0]);
    var p1 = g.Player1; var p2 = g.Player2;
    var cannons = DungeonRunContent.Get("GILA_601");
    var slots = p1.BoardZone.Slots;
    Check("T12 GILA_600h Start of Game: a Cannon at each edge of Darius's board after the mulligan, untouchable, not minions for targeting",
        slots.Count == 2 && slots.All(e => e.Card.Id == "GILA_601") && slots[0][Cannons.EdgeTag] == Cannons.Left && slots[1][Cannons.EdgeTag] == Cannons.Right
        && p1.BoardZone.GetAll().Length == 0 && ((Minion)slots[0]).Untouchable, Slots(p1));
    Check("T12 GILA_601: the Cannon cannot attack and no option targets it",
        !p1.Options().OfType<MinionAttackTask>().Any() && p2.Options().All(o => o.Target == null || !Cannons.IsCannon(o.Target as IPlayable)));
    var (_, _, gt) = Fight(Tess, 181, new string[0]);
    Check("T12 Tess's portrait has no Cannons", gt.Player1.BoardZone.Count == 0);
    // Placement: minions enter between the Cannons, wherever they are dropped.
    Mana(p1, 10);
    var wisp = ToHand(g, p1, "CS2_231"); Play(g, wisp, null, 0);
    var wisp2 = ToHand(g, p1, "CS2_231"); Play(g, wisp2, null, 3);
    var wisp3 = Board(g, p1, "CS2_231");
    var order = p1.BoardZone.Slots.ToList();
    Check("T12 placement: a minion dropped left of the left Cannon, one right of the right Cannon and a summoned token all land between the Cannons",
        order.Count == 5 && Cannons.IsCannon(order[0]) && Cannons.IsCannon(order[4]) && order[1] == wisp && order[2] == wisp2 && order[3] == wisp3, Slots(p1));
    // Fire! geometry, kills and refresh (fresh fights).
    var (_, _, g1) = Fight(Darius, 182, new string[0]);
    Mana(g1.Player1, 10);
    var yeti = Board(g1, g1.Player2, "CS2_182");
    Check("T12 geometry: with my two Cannons and one enemy minion, both Cannons are opposite it", Cannons.Of(g1.Player1).All(c => Cannons.Opposite(c).SequenceEqual(new[] { yeti })));
    UsePower(g1);
    Check("T12 GILA_600p Fire!: each Cannon deals 1 (the Cannon's printed @) to the Yeti; nothing died, so Fire! is spent; the fire is counted",
        yeti.Health == 3 && g1.Player1.Hero.HeroPower.IsExhausted && g1.Player1[Cannons.FiredTag] == 1 && Cannons.Damage(g1.Player1) == 1, $"{yeti.Health} ex {g1.Player1.Hero.HeroPower.IsExhausted}");
    var (_, _, g2) = Fight(Darius, 183, new string[0]);
    Mana(g2.Player1, 10);
    var w1 = Board(g2, g2.Player2, "CS2_231"); var w2 = Board(g2, g2.Player2, "CS2_231");
    UsePower(g2);
    Check("T12 Fire!: two enemy Wisps, one opposite each Cannon, both die, so Fire! is refreshed", w1.IsDead && w2.IsDead && !g2.Player1.Hero.HeroPower.IsExhausted
        && g2.Player2.BoardZone.Count == 0, $"{w1.IsDead} {w2.IsDead} ex {g2.Player1.Hero.HeroPower.IsExhausted}");
    Check("T12 Fire! refreshed: it can be used again (no target: it misses, is spent, and counts)", UsePower(g2) && g2.Player1.Hero.HeroPower.IsExhausted && g2.Player1[Cannons.FiredTag] == 2
        && g2.Player1.RemainingMana == 6);
    var (_, _, g3) = Fight(Darius, 184, new string[0]);
    Mana(g3.Player1, 10);
    var e1 = Board(g3, g3.Player2, "CS2_182"); var e2 = Board(g3, g3.Player2, "CS2_182"); var e3 = Board(g3, g3.Player2, "CS2_182");
    UsePower(g3);
    Check("T12 geometry: three enemy minions, the middle one is opposite both Cannons (half a slot each way), the outer ones one each",
        e1.Health == 4 && e2.Health == 3 && e3.Health == 4, $"{e1.Health} {e2.Health} {e3.Health}");
    Board(g3, g3.Player1, "CS2_231"); Board(g3, g3.Player1, "CS2_231");
    EndTurn(g3); EndTurn(g3);
    UsePower(g3);
    Check("T12 geometry: my two minions push the Cannons out to x=-1.5/+1.5; the outer enemies (x=-1/+1) are hit, the middle one is not",
        e1.Health == 3 && e2.Health == 3 && e3.Health == 3, $"{e1.Health} {e2.Health} {e3.Health}");
    Check("T12 the Cannons are never damaged or destroyed by a board clear", Cannons.Of(g3.Player1).Count == 2);
    var clear = ToHand(g3, g3.Player1, "CS2_062"); // Hellfire: 3 damage to all characters
    Play(g3, clear);
    Check("T12 Hellfire leaves both Cannons at full Health on the board", Cannons.Of(g3.Player1).Count == 2 && Cannons.Of(g3.Player1).All(c => c.Health == 3));
}

// ---------------------------------------------------------------- T13 GILA_602, GILA_603, GILA_604, GILA_605 (Cannon treasures)
{
    var (s, plan, g) = Fight(Darius, 191, new[] { "GILA_602", "GILA_603", "GILA_605", "GILA_604" });
    var p1 = g.Player1;
    Check("T13 the three passives attach their player enchantments at match start (GILA_602e2, GILA_603e2, GILA_605e2)",
        new[] { "GILA_602e2", "GILA_603e2", "GILA_605e2" }.All(id => p1.AppliedEnchantments.Count(e => e.Card.Id == id) == 1));
    Check("T13 GILA_605 Dragonfire Ammo: the Cannons do 2 (1 + 1)", Cannons.Damage(p1) == 2);
    Mana(p1, 10);
    var mine = Board(g, p1, "CS2_182");
    var powder = Draw(g, "GILA_604");
    Clear(p1);
    var a = ToHand(g, p1, "CS2_182");
    var l = Board(g, g.Player2, "CS2_182"); var m = Board(g, g.Player2, "CS2_182"); var r = Board(g, g.Player2, "CS2_182");
    UsePower(g);
    Check("T13 Fire! with Dragonfire: my Yeti puts the Cannons at x=-1/+1, opposite the outer enemies only; each takes 2",
        l.Health == 3 && m.Health == 5 && r.Health == 3, $"{l.Health} {m.Health} {r.Health}");
    Check("T13 GILA_602 Victorious Rally: firing gives my minions +1 Attack this turn", mine.AttackDamage == 5 && mine.AppliedEnchantments.Any(e => e.Card.Id == "GILA_602e3"));
    Check("T13 GILA_603 Tactical Reinforcements: firing reduces a card in my hand by (2)", a.Cost == 2 && a.AppliedEnchantments.Any(e => e.Card.Id == "GILA_603e3"));
    p1.SetasideZone.Remove(powder); Generic.AddHandPhase(p1, powder);
    Play(g, powder);
    Check("T13 GILA_604 Extra Powder: the Cannons do 2 more this turn (4)", Cannons.Damage(p1) == 4 && p1.AppliedEnchantments.Any(e => e.Card.Id == "GILA_604e2"));
    EndTurn(g);
    Check("T13 GILA_602: the +1 Attack wears off at the end of the turn; GILA_604: so does the extra powder",
        mine.AttackDamage == 4 && Cannons.Damage(p1) == 2 && !p1.AppliedEnchantments.Any(e => e.Card.Id == "GILA_604e2"));
}

// ---------------------------------------------------------------- T14 GILA_610, GILA_611, GILA_612
{
    var (s, plan, g) = Fight(Darius, 201, new[] { "GILA_610", "GILA_611", "GILA_612" });
    var p1 = g.Player1;
    var goliath = Draw(g, "GILA_612");
    Check("T14 GILA_612: costs (10) before any fire", goliath.Cost == 10);
    var grizzled = Draw(g, "GILA_610"); Mana(p1, 10);
    Play(g, grizzled);
    var order = p1.BoardZone.Slots.ToList();
    Check("T14 GILA_610: a third Cannon at the left edge (the edges were even), the 4/3 between the Cannons",
        order.Count == 4 && Cannons.IsCannon(order[0]) && Cannons.IsCannon(order[1]) && order[2] == grizzled && Cannons.IsCannon(order[3])
        && order[1][Cannons.EdgeTag] == Cannons.Left, Slots(p1));
    var tusk = Draw(g, "GILA_611");
    Play(g, tusk);
    var yeti = Board(g, g.Player2, "CS2_182");
    int fired = p1[Cannons.FiredTag];
    g.Process(MinionAttackTask.Any(p1, tusk, yeti));
    Check("T14 GILA_611 Tuskarr Raider: Rush; after it attacks, the Cannons fire (counted; with five board slots none is opposite the lone Yeti)",
        p1[Cannons.FiredTag] == fired + 1 && yeti.Health == 2, $"{yeti.Health} {p1[Cannons.FiredTag]}");
    Check("T14 GILA_612: costs (1) less per fire this game", goliath.Cost == 9);
    UsePower(g);
    Check("T14 GILA_612: Fire! counts too (8)", goliath.Cost == 8);
}

// ---------------------------------------------------------------- T15 GILA_607 Pillage the Fallen
{
    var (s, plan, g) = Fight(Darius, 211, new[] { "GILA_607" });
    var p1 = g.Player1;
    Check("T15 GILA_607: the player enchantment is attached", p1.AppliedEnchantments.Any(e => e.Card.Id == "GILA_607e2"));
    Mana(p1, 10);
    var axe = ToHand(g, p1, "CS2_106"); Play(g, axe);
    ((Weapon)axe).Damage = 1;
    g.Process(HeroAttackTask.Any(p1, g.Player2.Hero));
    var w = p1.Hero.Weapon;
    Check("T15 GILA_607: the War Axe breaks and a random scripted weapon is equipped", axe.Zone?.Type == Zone.GRAVEYARD
        && w != null && w != axe && w.Card.Implemented && w.Card.Type == CardType.WEAPON, w?.Card.Id ?? "none");
    var before = p1.Hero.Weapon;
    var other = ToHand(g, p1, "CS2_106"); Play(g, other);
    Check("T15 GILA_607: replacing a weapon is not a break: the new weapon stays", p1.Hero.Weapon == other && before.Zone?.Type == Zone.GRAVEYARD, p1.Hero.Weapon?.Card.Id);
    var (_, _, g2) = Fight(Darius, 211, new[] { "GILA_607" });
    Mana(g2.Player1, 10);
    var axe2 = ToHand(g2, g2.Player1, "CS2_106"); Play(g2, axe2); ((Weapon)axe2).Damage = 1;
    g2.Process(HeroAttackTask.Any(g2.Player1, g2.Player2.Hero));
    Check("T15 GILA_607: the same seed equips the same weapon (game RNG)", g2.Player1.Hero.Weapon?.Card.Id == w?.Card.Id);
}

// ---------------------------------------------------------------- T16 GILA_608 Safe Harbor
{
    var (s, plan, g) = Fight(Darius, 221, new[] { "GILA_608" });
    var p1 = g.Player1;
    var harbor = Draw(g, "GILA_608"); Mana(p1, 10);
    var mine = Board(g, p1, "CS2_182"); mine.Damage = 2;
    Play(g, harbor, mine);
    Check("T16 GILA_608 on my minion: it goes to my hand at full Health, costing (0)", mine.Zone == p1.HandZone && mine.Cost == 0 && mine.Health == 5
        && p1.BoardZone.GetAll().Length == 0);
    var (_, _, g2) = Fight(Darius, 222, new[] { "GILA_608" });
    var h2 = Draw(g2, "GILA_608"); Mana(g2.Player1, 10);
    var gnome = Board(g2, g2.Player2, "EX1_029");
    int myHp = g2.Player1.Hero.Health, n = g2.Player1.HandZone.Count;
    Play(g2, h2, gnome);
    var stolen = g2.Player1.HandZone.LastOrDefault();
    Check("T16 GILA_608 on an enemy minion: it leaves the boss's board (no Deathrattle) and its card is in my hand at (0)",
        g2.Player2.BoardZone.Count == 0 && g2.Player1.Hero.Health == myHp && stolen?.Card.Id == "EX1_029" && stolen.Cost == 0 && stolen.Controller == g2.Player1
        && g2.Player1.HandZone.Count == n, stolen?.Card.Id);
    Check("T16 GILA_608 needs a minion target", !g2.Player1.Options().OfType<PlayCardTask>().Any(o => o.Source.Card.Id == "GILA_608"));
}

// ---------------------------------------------------------------- T17 GILA_513 Cult of the Wolf
{
    var (s, plan, g) = Fight(Tess, 231, new[] { "GILA_513" });
    var p1 = g.Player1;
    Mana(p1, 10); Clear(p1);
    Check("T17 GILA_513: Scavenge costs (1)", p1.Hero.HeroPower.Cost == 1);
    bool one = UsePower(g), two = UsePower(g);
    Check("T17 GILA_513: the power can be used twice this turn (two Rocks)", one && two && p1.HandZone.Count(e => e.Card.Id == "GILA_500p2t") == 2 && p1.RemainingMana == 8);
    Check("T17 GILA_513: not a third time", !p1.Options().OfType<HeroPowerTask>().Any() && p1.Hero.HeroPower.IsExhausted);
    EndTurn(g); EndTurn(g);
    Check("T17 GILA_513: twice again next turn", UsePower(g) && UsePower(g) && p1.Hero.HeroPower.IsExhausted);
    var (_, _, g0) = Fight(Tess, 231, new string[0]);
    Mana(g0.Player1, 10);
    Check("T17 without it: once a turn at (2)", g0.Player1.Hero.HeroPower.Cost == 2 && UsePower(g0) && !g0.Player1.Options().OfType<HeroPowerTask>().Any());
}

// ---------------------------------------------------------------- T18 full games against the real boss AI, the player playing the id
var testBudget = new Budget(6000, 2000, 300, 6, 3, 100, 8, turnSearchSeconds: 6, turnWallSeconds: 8);
string AiGame(string id, long seed)
{
    string row = id == "GILA_500p2" || tessTreasures.Contains(id) || id == Tess ? Tess : Darius;
    var s = NewAct(row, seed);
    bool isPower = id == "GILA_500p2" || id == "GILA_600p";
    bool isPassive = passives.Contains(id);
    bool isHero = id == Tess || id == Darius || id == "GILA_601";
    if (!isPower && !isHero) Hold(s, id);
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
    if (!isPower && !isHero && !isPassive)
    {
        var e = game.Player1.DeckZone.FirstOrDefault(x => x.Card.Id == id);
        if (e != null) Generic.Draw(game.Player1, e);
    }
    game.DeathProcessingAndAuraUpdate();
    int decisionIndex = 0, actions = 0, decisions = 0, fallbacks = 0, boostedTurn = -1, powerUses = 0, cannons = Cannons.Of(game.Player1).Count;
    int playedTurn = isPassive || isHero ? 0 : -1;
    void Act(PlayerTask task, string who)
    {
        if (++actions > 1500) throw new Exception("stuck after 1500 actions");
        if (task is ChooseTask pick && game.Step != Step.BEGIN_MULLIGAN)
        {
            var decision = game.GetPendingDecisions().Single(d => d.ControllerId == task.Controller.Id);
            if (!game.TryAnswer(new SabberStoneCore.Model.Decisions.DecisionAnswer(decision.Id, decision.ControllerId, pick.Choices.ToArray()), out var why))
                throw new Exception($"engine rejected {who} choice ({why})");
        }
        else if (!game.Process(task)) throw new Exception($"engine rejected {who} task {task}");
    }
    while (game.State != State.COMPLETE && game.Turn <= 60)
    {
        if (game.CurrentPlayer == game.Player2)
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
            PlayerTask mine = null;
            if (isPower) mine = options.OfType<HeroPowerTask>().FirstOrDefault();
            else if (playedTurn < 0)
            {
                var held = game.Player1.HandZone.FirstOrDefault(e => e.Card.Id == id);
                if (held != null && boostedTurn != game.Turn && game.Player1.RemainingMana < held.Cost) { boostedTurn = game.Turn; game.Player1.BaseMana = Math.Min(10, held.Cost); game.Player1.UsedMana = 0; options = game.Player1.Options(); }
                mine = options.OfType<PlayCardTask>().Where(o => o.Source.Card.Id == id)
                    .OrderByDescending(o => o.Target != null && o.Target.Controller != game.Player1 ? 1 : 0).FirstOrDefault();
            }
            if (mine != null)
            {
                if (mine is HeroPowerTask) { powerUses++; if (playedTurn < 0) playedTurn = game.Turn; }
                else playedTurn = game.Turn;
                Act(mine, "player");
            }
            else Act(LocalBot.Choose(options), "player");
        }
    }
    string result = game.State != State.COMPLETE ? "turn-cap" : game.Player2.PlayState == PlayState.WON ? "boss" : game.Player1.PlayState == PlayState.WON ? "player" : "tie";
    return $"{result} turns {game.Turn} played-turn {playedTurn} power-uses {powerUses} cannons {cannons} fired {game.Player1[Cannons.FiredTag]} decisions {decisions} fallbacks {fallbacks} boss {boss.Id}";
}
foreach (string id in f3)
{
    string row;
    try { row = AiGame(id, 251); }
    catch (Exception e) { row = "FAULT " + e.GetType().Name + ": " + e.Message.Split('\n')[0]; }
    Console.WriteLine($"T18 {id,-11} {row}");
    bool dariusSide = id == Darius || id == "GILA_600p" || id == "GILA_601" || dariusTreasures.Contains(id);
    Check($"T18 {id}: the player plays it in a full game against the boss AI, which ends without a fault or an AI fallback",
        !row.StartsWith("FAULT") && row.Contains("fallbacks 0 ") && !row.Contains("played-turn -1") && (!dariusSide || row.Contains("cannons 2 "))
        && (id != "GILA_500p2" && id != "GILA_600p" || !row.Contains("power-uses 0 ")), row);
}

Console.WriteLine($"{passes} passed, {failures} failed");
return failures == 0 ? 0 : 1;
