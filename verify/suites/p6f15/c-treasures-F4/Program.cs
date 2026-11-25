// c-treasures-F4: phase 6 family F4 of the unified Dungeon Run, the Witchwood Monster Hunt heroes Houndmaster Shaw and Toki,
// Time-Tinker: their portraits (GILA_400h, GILA_900h) as hero rows of run-v1.json (heroRows: Shaw HUNTER with Dog Whistle first
// and the Hunter basic power unlocked at floor 4; Toki MAGE with the Mage basic power as a stand-in for Temporal Loop, which
// stays rejected until phase 9), one authored starter each, Shaw's power GILA_400p Dog Whistle with its Bloodhound (GILA_400t)
// and the hero treasures GILA_401, 403, 410-415, 901, 903, 904, 906, 907, 910, 913. For every id: the engine content
// (scripted, keyed and shelled by its own client id, name and text as facts/violet-hold-2026-09.json has them), the run data,
// the validator and admission checks, a scripted regression game through the run setup path (DungeonRun.NewRun on the hero
// row -> BuildPlan -> DungeonRunSetup.Configure -> Game -> ApplyAfterStart -> mulligan -> MainBegin -> the card drawn from the
// run deck and played), and a full game against the real boss AI (DungeonRunBossPilot + GoldenPilot) in which the player
// plays it; it must end without a fault or an AI fallback. T19: a game in which Impetuous Companion hands the boss the
// player's treasures, which the boss AI then plays. Headless: compiles C:\SoloStone\plugin\DungeonRun\*.cs and the NPC AI
// against p6f4/engine-out. Run it twice and compare the output.
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

const string Shaw = "GILA_400h", Toki = "GILA_900h", ShawDeck = "mh_shaw_kennel", TokiDeck = "mh_toki_workshop", Hound = "GILA_400t";
string[] f4 = { "GILA_400h", "GILA_400p", "GILA_401", "GILA_403", "GILA_410", "GILA_411", "GILA_412", "GILA_413", "GILA_414", "GILA_415",
    "GILA_900h", "GILA_901", "GILA_903", "GILA_904", "GILA_906", "GILA_907", "GILA_910", "GILA_913" };
string[] tokens = { Hound };
string[] shawTreasures = { "GILA_401", "GILA_403", "GILA_410", "GILA_411", "GILA_412", "GILA_413", "GILA_414", "GILA_415" };
string[] tokiTreasures = { "GILA_901", "GILA_903", "GILA_904", "GILA_906", "GILA_907", "GILA_910", "GILA_913" };
string[] passives = { "GILA_414", "GILA_415", "GILA_913" };
var effectShells = new Dictionary<string, string>
{
    ["fx_GILA_411e"] = "GILA_411e", ["fx_GILA_412e"] = "GILA_412e", ["fx_GILA_414e2"] = "GILA_414e2", ["fx_GILA_414"] = "GILA_414e",
    ["fx_GILA_415"] = "GILA_415e", ["fx_GILA_913"] = "GILA_913e",
};
CardType FactType(string id) => FactTag(id, 202) switch { 3 => CardType.HERO, 4 => CardType.MINION, 5 => CardType.SPELL, 6 => CardType.ENCHANTMENT, 7 => CardType.WEAPON, 10 => CardType.HERO_POWER, _ => CardType.INVALID };

// ---------------------------------------------------------------- T1 content
Check("T1 18 F4 ids", f4.Length == 18 && f4.Distinct().Count() == 18);
foreach (string id in f4.Concat(tokens))
{
    var content = DungeonRunContent.Get(id);
    var card = C(id);
    RunContentKind want = id == Shaw || id == Toki ? RunContentKind.Hero : id == "GILA_400p" ? RunContentKind.HeroPower
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
Check("T1 portraits: Shaw (HUNTER) and Toki (MAGE) are scripted run heroes at 30 Health, no Start of Game effect",
    C(Shaw).Implemented && C(Shaw).Class == CardClass.HUNTER && C(Shaw).Health == 30 && DungeonRunContent.Get(Shaw).Health == 30
    && C(Toki).Implemented && C(Toki).Class == CardClass.MAGE && C(Toki).Health == 30
    && !StartOfGameRules.HasEffect(C(Shaw)) && !StartOfGameRules.HasEffect(C(Toki)));
Check("T1 the Bloodhound is the client's 1/1 Beast with Rush; Butch has Rush; Bubba, Princess and the Companion have Battlecry",
    C(Hound).IsRace(Race.BEAST) && C(Hound)[GameTag.RUSH] == 1 && C("GILA_403")[GameTag.RUSH] == 1
    && C("GILA_410")[GameTag.BATTLECRY] == 1 && C("GILA_411")[GameTag.BATTLECRY] == 1 && C("GILA_906")[GameTag.BATTLECRY] == 1 && C("GILA_904")[GameTag.ECHO] == 1);
foreach (var fx in effectShells)
{
    var e = DungeonRunContent.Get(fx.Key);
    Check($"T1 {fx.Value}: content {fx.Key} on the client's own ENCHANTMENT, scripted, name and text word for word",
        e.Kind == RunContentKind.Effect && e.ShellId == fx.Value && C(fx.Value).Implemented && C(fx.Value).Type == CardType.ENCHANTMENT && clientIds.Contains(fx.Value)
        && e.Name == factById[fx.Value].GetProperty("name").GetString() && e.Text == FactText(fx.Value), e.Name + " | " + e.Text);
}
Check("T1 tokens: Dog Whistle and Bubba list the Bloodhound",
    DungeonRunContent.Get("GILA_400p").Tokens.SequenceEqual(new[] { Hound }) && DungeonRunContent.Get("GILA_410").Tokens.SequenceEqual(new[] { Hound }));
// Both were F15's, rejected until phase 9. Step 9 (lane X, D13) scripted Rewind (GILA_911) as a run-owned restart with content;
// Temporal Loop (GILA_900p) stays unscripted, a final rejection.
Check("T1 Temporal Loop (GILA_900p) stays unscripted (a final rejection, step 9); Rewind (GILA_911) is scripted with content (step 9's restart)",
    !C("GILA_900p").Implemented && C("GILA_911").Implemented && !DungeonRunContent.TryGet("GILA_900p", out _) && DungeonRunContent.TryGet("GILA_911", out _));

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
    Check("T2 the data without the catalog validates with 0 errors and loads no Shaw or Toki row", le.Count == 0 && legacy.FindClass(Shaw) == null && legacy.FindClass(Toki) == null,
        string.Join(" | ", le.Take(3)));
    var rs = data.FindClass(Shaw);
    Check("T2 hero row GILA_400h: kind hero, MonsterHunt, its own portrait, 30 Health, classes [HUNTER], enabled",
        rs != null && rs.IsHero && rs.Adventure == "MonsterHunt" && rs.HeroCard == Shaw && rs.Health == 30 && rs.ClassSet.SequenceEqual(new[] { "HUNTER" }) && rs.Enabled
        && data.ClassRows.All(c => c.Id != Shaw));
    Check("T2 hero row GILA_400h: Dog Whistle first (no unlock), the Hunter basic power DS1h_292 unlocked at floor 4",
        rs.Powers.SequenceEqual(new[] { "GILA_400p", "DS1h_292" }) && rs.PowerUnlock("GILA_400p") == null && rs.PowerUnlock("DS1h_292") == "gila_400h_floor4"
        && data.Unlocks.ContainsKey("gila_400h_floor4"));
    var rt = data.FindClass(Toki);
    Check("T2 hero row GILA_900h: kind hero, MonsterHunt, its own portrait, 30 Health, classes [MAGE], enabled, one power: the Mage basic power (stand-in)",
        rt != null && rt.IsHero && rt.Adventure == "MonsterHunt" && rt.HeroCard == Toki && rt.Health == 30 && rt.ClassSet.SequenceEqual(new[] { "MAGE" }) && rt.Enabled
        && rt.Powers.SequenceEqual(new[] { "CS2_034" }) && rt.PowerUnlock("CS2_034") == null && !rt.Powers.Contains("GILA_900p"));
    foreach (var (row, cls, deck) in new[] { (Shaw, "HUNTER", ShawDeck), (Toki, "MAGE", TokiDeck) })
    {
        var r = data.FindClass(row);
        var d = data.FindDeck(deck);
        Check($"T2 hero row {row}: one authored starter {deck} of 10 scripted, admitted {cls} or neutral cards",
            // Build Deck: the row's build deck follows its one authored starter.
            r.Decks.SequenceEqual(new[] { deck, row.ToLowerInvariant() + "_build" }) && data.FindDeck(r.Decks[1])?.Build == true && d.Class == row && d.Cards.Count == 10 && !d.IsGenerated
            && d.Cards.All(id => C(id) != null && C(id).Implemented && (C(id).Class.ToString() == cls || C(id).Class == CardClass.NEUTRAL)
                && DungeonRunAdmission.Classify(C(id), id, AdmitScope.Player, null, catalogRun: true).Admitted),
            string.Join(",", d?.Cards ?? new List<string>()));
    }
    foreach (string id in shawTreasures.Concat(tokiTreasures))
    {
        var t = data.FindTreasure(id);
        bool passive = passives.Contains(id);
        string deck = shawTreasures.Contains(id) ? ShawDeck : TokiDeck;
        Check($"T2 {id}: an {(passive ? "passive" : "active")} act-run treasure row scoped to {deck}, MonsterHunt pool",
            t != null && t.Active == !passive && t.Offered && !t.Legacy && !t.Signature && t.Runs == DungeonRunTreasure.RunsAct && t.Pool == "MonsterHunt"
            && t.Scope.Decks.SequenceEqual(new[] { deck }) && t.Scope.Classes.Count == 0 && t.Scope.Powers.Count == 0);
    }
    var families = new Dictionary<string, string>
    {
        ["GILA_400p"] = "run-hero-power", [Hound] = "run-treasure", ["GILA_401"] = "run-treasure", ["GILA_403"] = "run-treasure", ["GILA_410"] = "run-treasure",
        ["GILA_412"] = "run-treasure", ["GILA_413"] = "run-treasure", ["GILA_904"] = "run-treasure", ["GILA_910"] = "run-treasure",
        ["GILA_901"] = "run-treasure-hand-cast", ["GILA_906"] = "run-treasure-hand-swap",
    };
    foreach (var pair in families)
    {
        string id = pair.Key;
        var p = DungeonRunAdmission.Classify(C(id), id, AdmitScope.Player, null, catalogRun: true);
        var b = DungeonRunAdmission.Classify(C(id), id, AdmitScope.Boss, null, catalogRun: true);
        Check($"T2 admission {id}: the {pair.Value} row admits the player (catalog run), never the boss",
            p.Subject && p.Admitted && !b.Admitted && DungeonRunAdmission.RuleFor(id)?.Family == pair.Value, p.Family + " " + p.Admit);
        Check($"T2 admission {id}: the boss's generation never yields it", DungeonRunAdmission.GeneratedRejectsBoss(true).Contains(id));
    }
    // Phase 8 family 23: GILA_907 Clockwork Assistant is in Infinite Toki's nemesis deck, so its row (run-treasure-nemesis-deck)
    // admits a boss on a catalog run too; it left the families map above and is checked here as strictly.
    {
        var p = DungeonRunAdmission.Classify(C("GILA_907"), "GILA_907", AdmitScope.Player, null, catalogRun: true);
        var b = DungeonRunAdmission.Classify(C("GILA_907"), "GILA_907", AdmitScope.Boss, null, catalogRun: true);
        Check("T2 admission GILA_907: the run-treasure-nemesis-deck row (phase 8 family 23) admits the player on legacy and catalog runs, a boss on a catalog run only (Infinite Toki's deck)",
            p.Subject && p.Admitted && DungeonRunAdmission.Classify(C("GILA_907"), "GILA_907", AdmitScope.Player).Admitted && b.Admitted
            && !DungeonRunAdmission.Classify(C("GILA_907"), "GILA_907", AdmitScope.Boss).Admitted && DungeonRunAdmission.RuleFor("GILA_907")?.Family == "run-treasure-nemesis-deck",
            p.Family + " " + p.Admit + " / boss " + b.Admit);
        Check("T2 admission GILA_907: the boss's generation never yields it", DungeonRunAdmission.GeneratedRejectsBoss(true).Contains("GILA_907"));
    }
    Check("T2 admission GILA_411: the from-own-deck row (the bot knows its own deck; a player card here)",
        DungeonRunAdmission.RuleFor("GILA_411").Family == "from-own-deck" && DungeonRunAdmission.Classify(C("GILA_411"), "GILA_411", AdmitScope.Player, null, catalogRun: true).Admitted);
    Check("T2 admission GILA_903: the secret-generated row, admitted for the player on catalog runs only (legacy runs block generated Secrets)",
        DungeonRunAdmission.RuleFor("GILA_903").Family == "secret-generated" && !DungeonRunAdmission.Classify(C("GILA_903"), "GILA_903", AdmitScope.Player).Admitted
        && DungeonRunAdmission.Classify(C("GILA_903"), "GILA_903", AdmitScope.Player, null, catalogRun: true).Admitted
        && !DungeonRunAdmission.Classify(C("GILA_903"), "GILA_903", AdmitScope.Boss, null, catalogRun: true).Admitted
        && DungeonRunAdmission.GeneratedRejectsBoss(true).Contains("GILA_903"));
    var genClasses = DungeonRunAdmission.SecretGeneratorClasses(data, new[] { "MAGE" }, _ => false);
    var prior = DungeonRunAdmission.CatalogSecretPrior(data, new[] { "MAGE" });
    var others = DungeonRunContent.EnhanceAMaticClasses.Select(c => c.ToString()).Where(c => c != "MAGE").ToList();
    Check("T2 the boss's Secret prior for a Mage catalog run covers the Secrets of every class Enhance-a-matic draws from",
        others.All(genClasses.Contains) && prior != null && others.All(cls => DungeonRunAdmission.GeneratedSecretPool(new[] { cls }).All(prior.ContainsKey)),
        string.Join(",", genClasses));
    Check("T2 the boss's Secret prior for a Rogue catalog run still holds GILA_509's Hunter Secrets (Tess)",
        DungeonRunAdmission.SecretGeneratorClasses(data, new[] { "ROGUE" }, _ => false).Contains("HUNTER"));
    {
        var v = DungeonRunAdmission.Classify(C("GILA_900p"), "GILA_900p", AdmitScope.Player, null, catalogRun: true);
        Check("T2 GILA_900p: still rejected (turn-rewind, a final rejection in step 9)", v.Subject && !v.AdmittedSome && v.Family == "turn-rewind" && v.Lift.StartsWith("none: a final rejection (step 9"));
        var r = DungeonRunAdmission.Classify(C("GILA_911"), "GILA_911", AdmitScope.Player, null, catalogRun: true);
        var rb = DungeonRunAdmission.Classify(C("GILA_911"), "GILA_911", AdmitScope.Boss, null, catalogRun: true);
        Check("T2 GILA_911: admitted for the run player only (game-restart, step 9)", r.Subject && r.Admitted && !rb.AdmittedSome && r.Family == "game-restart");
    }
    string badPower = runJson.Replace("\"powers\": [\"CS2_034\"], \"decks\": [\"mh_toki_workshop\", \"gila_900h_build\"]", "\"powers\": [\"GILA_900p\"], \"decks\": [\"mh_toki_workshop\", \"gila_900h_build\"]");
    var ePower = DungeonRunValidator.Validate(DungeonRunData.Parse(badPower, poolJson, npcJson, catalogJson, scheduleJson, tavernJson), null);
    Check("T2 validator: Toki with Temporal Loop (unscripted, rejected) is an error", badPower != runJson && ePower.Any(e => e.Contains("GILA_900p")), string.Join(" | ", ePower.Take(2)));
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
bool FromRunDeck(DungeonRunState s, RunMatchPlan plan, string id) => plan.PlayerDeck.Any(c => c.Id == id) && s.Deck.Contains(id);
void Kill(Game g, Minion m) { m.Damage = m.Health; g.DeathProcessingAndAuraUpdate(); }
// Draws every card of the deck that the predicate rejects, so the rest of the deck is known.
void Thin(Game g, Controller c, Func<IPlayable, bool> keep)
{
    foreach (var e in c.DeckZone.ToArray()) if (!keep(e)) { c.DeckZone.Remove(e); c.SetasideZone.Add(e); }
    g.DeathProcessingAndAuraUpdate();
}

// ---------------------------------------------------------------- T3 NewRun / BuildPlan on the hero rows; offers on hero runs only
{
    foreach (var (row, power, deck) in new[] { (Shaw, "GILA_400p", ShawDeck), (Toki, "CS2_034", TokiDeck) })
    {
        var s = run.NewRun(all, row, deck, power, 5, signatureId: run.SignatureOffer(all, run.Data.FindClass(row), 5).FirstOrDefault());
        var plan = run.BuildPlan(s);
        Check($"T3 NewRun {row}: an act catalog run of the hero row, its starter in the run deck (origin starter), its power",
            s.Class == row && s.IsActRun && s.IsCatalogRun && s.StarterDeck == deck && s.Power == power && s.Deck.SequenceEqual(data.FindDeck(deck).Cards)
            && s.Cards.All(c => c.Origin == DungeonRunDeck.OriginStarter));
        Check($"T3 BuildPlan {row}: the portrait at 30 Health, {power}, the run player's generated Secrets on (catalog run)",
            plan.PlayerHero.Id == row && plan.PlayerHeroHealth == 30 && plan.PlayerHeroPowerId == power && plan.PlayerGeneratedSecrets
            && plan.PlayerDeck.Select(c => c.Id).OrderBy(x => x).SequenceEqual(data.FindDeck(deck).Cards.OrderBy(x => x)));
        var g = new Game(Configured(plan, 77));
        g.StartGame();
        Check($"T3 {row}: the match starts the portrait at 30 Health with {power}", g.Player1.Hero.Card.Id == row && g.Player1.Hero.Health == 30
            && g.Player1.Hero.HeroPower.Card.Id == power, g.Player1.Hero.Card.Id + " " + g.Player1.Hero.Health + " " + g.Player1.Hero.HeroPower.Card.Id);
    }
    bool locked = false;
    try { run.NewRun(new DungeonRunProfile(), Shaw, ShawDeck, "DS1h_292", 5, signatureId: run.SignatureOffer(new DungeonRunProfile(), run.Data.FindClass(Shaw), 5).FirstOrDefault()); } catch (InvalidOperationException) { locked = true; }
    Check("T3 Shaw: the Hunter basic power needs floor 4 with the hero", locked && run.NewRun(all, Shaw, ShawDeck, "DS1h_292", 5, signatureId: run.SignatureOffer(all, run.Data.FindClass(Shaw), 5).FirstOrDefault()).Power == "DS1h_292");
    bool loop = false;
    try { run.NewRun(all, Toki, TokiDeck, "GILA_900p", 5, signatureId: run.SignatureOffer(all, run.Data.FindClass(Toki), 5).FirstOrDefault()); } catch (Exception) { loop = true; }
    Check("T3 Toki: Temporal Loop cannot be picked (not a power of the row)", loop);
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
    foreach (string id in shawTreasures) Check($"T3 {id} is offered to Shaw runs", offered[Shaw].Contains(id));
    foreach (string id in tokiTreasures) Check($"T3 {id} is offered to Toki runs", offered[Toki].Contains(id));
    Check("T3 no other run is offered a Shaw or Toki treasure (deck scope)",
        offered.Where(p => p.Key != Shaw && p.Key != Toki).All(p => !p.Value.Overlaps(shawTreasures.Concat(tokiTreasures)))
        && !offered[Shaw].Overlaps(tokiTreasures) && !offered[Toki].Overlaps(shawTreasures),
        string.Join(";", offered.Select(p => p.Key + ":" + string.Join(",", p.Value.Where(x => x.StartsWith("GILA_4") || x.StartsWith("GILA_9"))))));
    var sp = NewAct(Toki, 9);
    run.OpenTreasureDraft(sp);
    sp.OpenDraft.Offered[0] = "GILA_906";
    int before = sp.Deck.Count;
    run.PickTreasure(sp, "GILA_906");
    Check("T3 PickTreasure GILA_906 on a Toki run: the run deck gains the card (origin treasure)",
        sp.Deck.Count == before + 1 && sp.Cards.Last().Id == "GILA_906" && sp.Cards.Last().Origin == DungeonRunDeck.OriginTreasure && sp.ActiveTreasures.Contains("GILA_906"));
}

// ---------------------------------------------------------------- T4 GILA_400p Dog Whistle (+ GILA_400t Bloodhound)
{
    var (s, plan, g) = Fight(Shaw, 101, new string[0]);
    var p1 = g.Player1;
    Mana(p1, 10);
    Check("T4 Dog Whistle costs (2)", p1.Hero.HeroPower.Cost == 2);
    bool used = UsePower(g);
    var hound = p1.BoardZone.GetAll().FirstOrDefault(m => m.Card.Id == Hound);
    Check("T4 Dog Whistle: a 1/1 Bloodhound with Rush on my board, the power spent", used && hound != null && hound.AttackDamage == 1 && hound.Health == 1 && hound[GameTag.RUSH] == 1
        && p1.Hero.HeroPower.IsExhausted && p1.RemainingMana == 8);
    var yeti = Board(g, g.Player2, "CS2_182");
    Check("T4 the Bloodhound can attack an enemy minion at once (Rush), not the hero",
        p1.Options().OfType<MinionAttackTask>().Any(o => o.Source == hound && o.Target == yeti) && !p1.Options().OfType<MinionAttackTask>().Any(o => o.Source == hound && o.Target == g.Player2.Hero));
    g.Process(MinionAttackTask.Any(p1, hound, yeti));
    Check("T4 the Bloodhound trades 1 damage into the Yeti and dies", hound.IsDead && yeti.Health == 4);
    for (int i = 0; i < 7; i++) Board(g, p1, "CS2_231");
    EndTurn(g); EndTurn(g);
    Check("T4 Dog Whistle needs a free board slot", !p1.Options().OfType<HeroPowerTask>().Any());
}

// ---------------------------------------------------------------- T5 GILA_401 Sic 'Em
{
    var (s, plan, g) = Fight(Shaw, 111, new[] { "GILA_401" });
    Check("T5 GILA_401 is a card of the run deck the match plan deals", FromRunDeck(s, plan, "GILA_401"));
    var p1 = g.Player1;
    var sic = Draw(g, "GILA_401"); Mana(p1, 10);
    var deckMinions = p1.DeckZone.OfType<Minion>().ToList();
    var top = deckMinions.Select(m => m.AttackDamage).OrderByDescending(a => a).Take(3).ToList();
    Play(g, sic);
    var summoned = p1.BoardZone.GetAll().ToList();
    Check("T5 GILA_401: the 3 deck minions with the highest Attack are summoned from the deck",
        summoned.Count == Math.Min(3, deckMinions.Count) && summoned.Select(m => m.AttackDamage).OrderByDescending(a => a).SequenceEqual(top)
        && summoned.All(m => deckMinions.Contains(m) && m.Zone == p1.BoardZone) && p1.DeckZone.OfType<Minion>().Count() == deckMinions.Count - summoned.Count,
        string.Join(",", summoned.Select(m => m.Card.Id + ":" + m.AttackDamage)) + " want " + string.Join(",", top));
    // Ties: a deck of three 1/1s and one 6/5; the 6/5 and two of the 1/1s, the same two for the same seed.
    string[] Tie(int seed)
    {
        var (_, _, gt) = Fight(Shaw, seed, new[] { "GILA_401" });
        var card = Draw(gt, "GILA_401"); Mana(gt.Player1, 10);
        Thin(gt, gt.Player1, e => e.Card.Id == "FP1_011" || e.Card.Id == "KAR_005" || e.Card.Id == "DS1_175" || e.Card.Id == "EX1_534");
        gt.Process(PlayCardTask.Any(gt.Player1, card));
        return gt.Player1.BoardZone.GetAll().Select(m => m.Id + ":" + m.Card.Id).ToArray();
    }
    var t1 = Tie(112); var t2 = Tie(112);
    Check("T5 GILA_401 ties: the highest first, the rest broken with the game RNG (the same seed summons the same minions)", t1.SequenceEqual(t2) && t1.Length >= 1, string.Join(" ", t1));
    var (_, _, ge) = Fight(Shaw, 113, new[] { "GILA_401" });
    var c2 = Draw(ge, "GILA_401"); Mana(ge.Player1, 10);
    Thin(ge, ge.Player1, e => !(e is Minion));
    Check("T5 GILA_401 with no minion in the deck: cast, nothing summoned", ge.Process(PlayCardTask.Any(ge.Player1, c2)) && ge.Player1.BoardZone.Count == 0);
}

// ---------------------------------------------------------------- T6 GILA_403 Butch
{
    var (s, plan, g) = Fight(Shaw, 121, new[] { "GILA_403" });
    var p1 = g.Player1;
    var butch = Draw(g, "GILA_403"); Mana(p1, 10);
    Check("T6 GILA_403 in hand: 4-mana 1/1", butch.Cost == 4);
    Play(g, butch);
    var m = (Minion)butch;
    Check("T6 Butch on the board with no Beast dead: 1/1 Rush", m.AttackDamage == 1 && m.Health == 1 && m[GameTag.RUSH] == 1);
    Kill(g, Board(g, p1, Hound)); Kill(g, Board(g, p1, "CS2_172"));
    Kill(g, Board(g, p1, "CS2_231"));
    Kill(g, Board(g, g.Player2, "CS2_172"));
    Check("T6 Butch: +1/+1 per friendly Beast that died this game (2); a non-Beast and an enemy Beast do not count",
        m.AttackDamage == 3 && m.Health == 3 && DungeonRunContent.FriendlyBeastDeaths(m) == 2, $"{m.AttackDamage}/{m.Health}");
    var (_, _, g2) = Fight(Shaw, 122, new[] { "GILA_403" });
    var b2 = Draw(g2, "GILA_403"); Mana(g2.Player1, 10);
    Kill(g2, Board(g2, g2.Player1, Hound));
    Play(g2, b2);
    Check("T6 Butch: a Beast that died before Butch was played counts too (2/2)", ((Minion)b2).AttackDamage == 2 && ((Minion)b2).Health == 2);
}

// ---------------------------------------------------------------- T7 GILA_410 Bubba (+ GILA_414 Companionship)
{
    var (s, plan, g) = Fight(Shaw, 131, new[] { "GILA_410" });
    var p1 = g.Player1;
    var bubba = Draw(g, "GILA_410"); Mana(p1, 10);
    var yeti = Board(g, g.Player2, "CS2_182");
    Play(g, bubba, yeti);
    var hounds = p1.BoardZone.GetAll().Where(x => x.Card.Id == Hound).ToList();
    Check("T7 GILA_410: a 9/9 Battlecry; six Bloodhounds attack the Yeti one by one: five die killing it, the sixth stays and has not attacked",
        ((Minion)bubba).AttackDamage == 9 && yeti.IsDead && hounds.Count == 1 && hounds[0].NumAttacksThisTurn == 0
        && g.Player1.GraveyardZone.Count(x => x.Card.Id == Hound) == 5, $"yeti {yeti.Health} dead {yeti.IsDead} hounds {hounds.Count}");
    var (_, _, g2) = Fight(Shaw, 132, new[] { "GILA_410" });
    var b2 = Draw(g2, "GILA_410"); Mana(g2.Player1, 10);
    Check("T7 GILA_410 with no enemy minion: playable without a target, six Bloodhounds", Play(g2, b2) && g2.Player1.BoardZone.GetAll().Count(x => x.Card.Id == Hound) == 6
        && g2.Player2.Hero.Health == g2.Player2.Hero.BaseHealth);
    var (_, _, g3) = Fight(Shaw, 133, new[] { "GILA_410", "GILA_414" });
    var b3 = Draw(g3, "GILA_410"); Mana(g3.Player1, 10);
    Check("T7 GILA_414 Companionship: the player enchantment GILA_414e is attached at match start", g3.Player1.AppliedEnchantments.Any(e => e.Card.Id == "GILA_414e"));
    var y3 = Board(g3, g3.Player2, "CS2_182");
    Play(g3, b3, y3);
    var h3 = g3.Player1.BoardZone.GetAll().Where(x => x.Card.Id == Hound).ToList();
    Check("T7 GILA_410 + GILA_414: 3/1 Bloodhounds kill the Yeti in two attacks, four stay (3 Attack each)",
        y3.IsDead && h3.Count == 4 && h3.All(x => x.AttackDamage == 3), $"{h3.Count} {string.Join(",", h3.Select(x => x.AttackDamage))}");
    var bubbaBoard = (Minion)b3;
    Check("T7 GILA_414: only Bloodhounds have +2 Attack (Bubba keeps 9)", bubbaBoard.AttackDamage == 9);
    UsePower(g3);
    var whistled = g3.Player1.BoardZone.GetAll().Where(x => x.Card.Id == Hound).Except(h3).ToList();
    Check("T7 GILA_414: Dog Whistle's Bloodhound is a 3/1 too", whistled.Count == 1 && whistled[0].AttackDamage == 3 && whistled[0].Health == 1);
    var (_, _, g4) = Fight(Shaw, 134, new[] { "GILA_414" });
    Mana(g4.Player1, 10); UsePower(g4);
    var h4 = g4.Player1.BoardZone.GetAll().First(x => x.Card.Id == Hound);
    Check("T7 GILA_414: a whistled Bloodhound is 3/1 with the GILA_414e2 buff", h4.AttackDamage == 3 && h4.Health == 1 && h4.AppliedEnchantments.Any(e => e.Card.Id == "GILA_414e2"));
    var w4 = Board(g4, g4.Player1, "CS2_231");
    Check("T7 GILA_414: another minion has no buff", w4.AttackDamage == 1);
    EndTurn(g4);
    Check("T7 GILA_414: the boss's minions are not buffed", Board(g4, g4.Player2, Hound).AttackDamage == 1);
}

// ---------------------------------------------------------------- T8 GILA_411 Princess
{
    var (s, plan, g) = Fight(Shaw, 141, new[] { "GILA_411" });
    var p1 = g.Player1;
    var princess = Draw(g, "GILA_411"); Mana(p1, 10);
    var deckDr = p1.DeckZone.OfType<Minion>().Where(m => m.Card.Power?.DeathrattleTask != null).Select(m => m.Card.Id).ToList();
    Play(g, princess);
    var bones = princess.AppliedEnchantments?.Where(e => e.Card.Id == "GILA_411e").ToList() ?? new List<Enchantment>();
    Check("T8 GILA_411: a 5/5 that gains min(3, deck Deathrattle minions) Princess's Bones, each capturing a Deathrattle minion of my deck",
        bones.Count == Math.Min(3, deckDr.Count) && bones.All(b => b.CapturedCard != null && deckDr.Contains(b.CapturedCard.Id)) && ((Minion)princess).AttackDamage == 5,
        string.Join(",", bones.Select(b => b.CapturedCard?.Id)) + " deck " + string.Join(",", deckDr));
    var (_, _, g2) = Fight(Shaw, 142, new[] { "GILA_411" });
    var pr2 = Draw(g2, "GILA_411"); Mana(g2.Player1, 10);
    Thin(g2, g2.Player1, e => e.Card.Id == "EX1_534");
    if (!g2.Player1.DeckZone.Any(e => e.Card.Id == "EX1_534")) new AddCardTo("EX1_534", EntityType.DECK).Process(g2, g2.Player1, g2.Player1.Hero, null);
    Play(g2, pr2);
    var b2 = pr2.AppliedEnchantments.Where(e => e.Card.Id == "GILA_411e").ToList();
    Check("T8 GILA_411 with only Savannah Highmane in the deck: one Bone, capturing it", b2.Count == 1 && b2[0].CapturedCard.Id == "EX1_534");
    Kill(g2, (Minion)pr2);
    Check("T8 GILA_411: when the Princess dies, the copied Deathrattle runs (two 2/2 Hyenas)",
        g2.Player1.BoardZone.GetAll().Count(m => m.Card.Id == "EX1_534t") == 2, string.Join(" ", g2.Player1.BoardZone.GetAll().Select(m => m.Card.Id)));
    var (_, _, g3) = Fight(Shaw, 143, new[] { "GILA_411" });
    var pr3 = Draw(g3, "GILA_411"); Mana(g3.Player1, 10);
    Thin(g3, g3.Player1, e => !(e is Minion));
    Play(g3, pr3);
    Check("T8 GILA_411 with no Deathrattle minion in the deck: no Bone", !(pr3.AppliedEnchantments?.Any(e => e.Card.Id == "GILA_411e") ?? false));
}

// ---------------------------------------------------------------- T9 GILA_412 Blood Moon
{
    var (s, plan, g) = Fight(Shaw, 151, new[] { "GILA_412" });
    var p1 = g.Player1;
    var moon = Draw(g, "GILA_412"); Mana(p1, 10);
    var a = Board(g, p1, "CS2_182"); var b = Board(g, p1, "CS2_231");
    var enemy = Board(g, g.Player2, "CS2_182");
    Play(g, moon);
    Check("T9 GILA_412: my minions get +1/+1 and Lifesteal (GILA_412e); the boss's do not",
        a.AttackDamage == 5 && a.Health == 6 && a.HasLifeSteal && b.AttackDamage == 2 && b.HasLifeSteal && enemy.AttackDamage == 4 && !enemy.HasLifeSteal
        && a.AppliedEnchantments.Any(e => e.Card.Id == "GILA_412e"));
    p1.Hero.Damage = 10;
    EndTurn(g); EndTurn(g);
    g.Process(MinionAttackTask.Any(p1, a, g.Player2.Hero));
    Check("T9 GILA_412: Lifesteal heals my hero by the damage dealt", p1.Hero.Health == 25, p1.Hero.Health.ToString());
}

// ---------------------------------------------------------------- T10 GILA_413 Shaw's Shank
{
    var (s, plan, g) = Fight(Shaw, 161, new[] { "GILA_413" });
    var p1 = g.Player1;
    var shank = Draw(g, "GILA_413"); Mana(p1, 10);
    Play(g, shank);
    Check("T10 GILA_413: a 1/5 weapon with no friendly minion", p1.Hero.Weapon?.Card.Id == "GILA_413" && p1.Hero.AttackDamage == 1 && p1.Hero.Weapon.Durability == 5);
    Board(g, p1, "CS2_231"); var y = Board(g, p1, "CS2_182");
    Check("T10 GILA_413: +1 Attack per friendly minion (3)", p1.Hero.AttackDamage == 3);
    Board(g, g.Player2, "CS2_182");
    Check("T10 GILA_413: enemy minions do not count", p1.Hero.AttackDamage == 3);
    int hp = g.Player2.Hero.Health;
    g.Process(HeroAttackTask.Any(p1, g.Player2.Hero));
    Check("T10 GILA_413: the hero hits for 3", g.Player2.Hero.Health == hp - 3);
    Kill(g, y);
    Check("T10 GILA_413: a minion dying lowers it (2)", p1.Hero.AttackDamage == 2);
}

// ---------------------------------------------------------------- T11 GILA_415 Lunar Signet
{
    var (s, plan, g) = Fight(Shaw, 171, new[] { "GILA_415" });
    var p1 = g.Player1;
    Check("T11 GILA_415: the player enchantment GILA_415e is attached at match start", p1.AppliedEnchantments.Any(e => e.Card.Id == "GILA_415e"));
    Mana(p1, 10); Clear(p1);
    int hp = g.Player2.Hero.Health;
    var gnome = ToHand(g, p1, "EX1_029");
    Play(g, gnome);
    Check("T11 GILA_415: playing Leper Gnome triggers its Deathrattle (2 damage to the boss); the Gnome stays", g.Player2.Hero.Health == hp - 2 && gnome.Zone == p1.BoardZone);
    var highmane = ToHand(g, p1, "EX1_534");
    Play(g, highmane);
    Check("T11 GILA_415: playing Savannah Highmane summons its two Hyenas", p1.BoardZone.GetAll().Count(m => m.Card.Id == "EX1_534t") == 2);
    int hp2 = g.Player2.Hero.Health;
    Board(g, p1, "EX1_029");
    Check("T11 GILA_415: a summoned (not played) minion does not trigger", g.Player2.Hero.Health == hp2);
    var yeti = ToHand(g, p1, "CS2_182"); Mana(p1, 10);
    Check("T11 GILA_415: a minion with no Deathrattle is played normally", Play(g, yeti) && g.Player2.Hero.Health == hp2);
    EndTurn(g);
    Mana(g.Player2, 10);
    var bossGnome = ToHand(g, g.Player2, "EX1_029");
    g.Process(PlayCardTask.Any(g.Player2, bossGnome));
    Check("T11 GILA_415: the boss's minions do not trigger it", p1.Hero.Health == p1.Hero.BaseHealth);
}

// ---------------------------------------------------------------- T12 Toki's stand-in power
{
    var (s, plan, g) = Fight(Toki, 181, new string[0]);
    Mana(g.Player1, 10);
    int hp = g.Player2.Hero.Health;
    Check("T12 Toki: the Mage basic power (Fireblast, the stand-in) deals 1", g.Player1.Hero.HeroPower.Card.Id == "CS2_034" && UsePower(g, g.Player2.Hero) && g.Player2.Hero.Health == hp - 1);
}

// ---------------------------------------------------------------- T13 GILA_901 Chaos Theory
{
    string Chaos(int seed)
    {
        var (s, plan, g) = Fight(Toki, seed, new[] { "GILA_901" });
        var p1 = g.Player1; var p2 = g.Player2;
        var chaos = Draw(g, "GILA_901"); Mana(p1, 10);
        Clear(p1); Clear(p2);
        Generic.AddHandPhase(p1, chaos);
        var mine1 = ToHand(g, p1, "CS2_029"); var mine2 = ToHand(g, p1, "CS2_023"); var keep = ToHand(g, p1, "CS2_182");
        var theirs1 = ToHand(g, p2, "CS2_024"); var theirs2 = ToHand(g, p2, "CS2_025"); var theirMinion = ToHand(g, p2, "CS2_182");
        var secret = ToHand(g, p2, "EX1_287");
        var enemy = Board(g, p2, "CS2_182");
        int deck = p1.DeckZone.Count, myHp = p1.Hero.Health, theirHp = p2.Hero.Health;
        Play(g, chaos);
        Check($"T13 GILA_901 ({seed}): every spell of both hands is cast (they leave the hands), the minions stay",
            mine1.Zone?.Type == Zone.GRAVEYARD && mine2.Zone?.Type == Zone.GRAVEYARD && theirs1.Zone?.Type == Zone.GRAVEYARD && theirs2.Zone?.Type != Zone.HAND
            && keep.Zone == p1.HandZone && theirMinion.Zone == p2.HandZone && secret.Zone?.Type != Zone.HAND,
            $"{mine1.Zone?.Type} {mine2.Zone?.Type} {theirs1.Zone?.Type} {theirs2.Zone?.Type} {secret.Zone?.Type}");
        Check($"T13 GILA_901 ({seed}): my Arcane Intellect drew 2 for me; the boss's Counterspell went to its Secret zone, revealed",
            p1.DeckZone.Count == deck - 2 && p2.SecretZone.Any(x => x.Card.Id == "EX1_287"));
        Check($"T13 GILA_901 ({seed}): the boss's Arcane Explosion hit my side (my minion-free board, so nothing) and its Frostbolt hit one of my characters or its own",
            p1.Hero.Health <= myHp);
        return $"{p1.Hero.Health} {p2.Hero.Health} {enemy.Health} {string.Join(",", p1.HandZone.Select(x => x.Card.Id))}";
    }
    string a = Chaos(191), b = Chaos(191);
    Check("T13 GILA_901: the same seed picks the same targets (game RNG)", a == b, a + " | " + b);
}

// ---------------------------------------------------------------- T14 GILA_903 Enhance-a-matic
{
    string[] Enhance(int seed)
    {
        var (s, plan, g) = Fight(Toki, seed, new[] { "GILA_903" });
        var p1 = g.Player1;
        var e = Draw(g, "GILA_903"); Mana(p1, 10);
        Clear(p1); Generic.AddHandPhase(p1, e);
        Play(g, e);
        return p1.HandZone.Select(x => x.Card.Id).ToArray();
    }
    var hand = Enhance(201);
    var classes = hand.Select(id => C(id).Class).ToList();
    Check("T14 GILA_903: a scripted spell of each class, in class order, until the hand is full (10 of the 11 classes)",
        hand.Length == 10 && hand.All(id => C(id).Type == CardType.SPELL && C(id).Implemented)
        && classes.SequenceEqual(DungeonRunContent.EnhanceAMaticClasses.Take(10)), string.Join(",", hand.Select(id => id + ":" + C(id).Class)));
    Check("T14 GILA_903: the same seed adds the same spells", hand.SequenceEqual(Enhance(201)));
}

// ---------------------------------------------------------------- T15 GILA_906 Impetuous Companion
{
    var (s, plan, g) = Fight(Toki, 211, new[] { "GILA_906" });
    var p1 = g.Player1; var p2 = g.Player2;
    var comp = Draw(g, "GILA_906"); Mana(p1, 10);
    Clear(p1); Clear(p2);
    Generic.AddHandPhase(p1, comp);
    var a = ToHand(g, p1, "CS2_029"); var b = ToHand(g, p1, "GILA_901");
    var x = ToHand(g, p2, "CS2_182"); var y = ToHand(g, p2, "CS2_024"); var z = ToHand(g, p2, "EX1_029");
    Generic.AddEnchantmentBlock(g, C("GILA_503e"), p2.Hero, x);
    int xCost = x.Cost;
    Play(g, comp);
    Check("T15 GILA_906: a 5/5; the hands are swapped, every card changes controller, in order",
        ((Minion)comp).AttackDamage == 5 && p1.HandZone.SequenceEqual(new[] { x, y, z }) && p2.HandZone.SequenceEqual(new[] { a, b })
        && p1.HandZone.All(c => c.Controller == p1) && p2.HandZone.All(c => c.Controller == p2),
        string.Join(",", p1.HandZone.Select(c => c.Card.Id)) + " | " + string.Join(",", p2.HandZone.Select(c => c.Card.Id)));
    Check("T15 GILA_906: a swapped card keeps its enchantments and cost", x.Cost == xCost && xCost == 1 && x.AppliedEnchantments.Any(e => e.Card.Id == "GILA_503e"));
    var copy = g.CloneForSimulation(1);
    var known = PublicHandKnowledge.Read(g, p1, copy.HistoryFacts.ProjectForFairView(copy, p2.PlayerId));
    Check("T15 GILA_906: the boss's public hand knowledge knows every card it gave the player (the transfer facts)",
        known.Count == 3 && known[x.Id].Id == "CS2_182" && known[y.Id].Id == "CS2_024" && known[z.Id].Id == "EX1_029", string.Join(",", known.Select(k => k.Key + "=" + k.Value.Id)));
    var fair = FairBotState.Create(g, p2.PlayerId, 7, DeckStyle.Tempo, new FairOptions { RemoveOpponentSecrets = true });
    Check("T15 GILA_906: the boss's fair view builds over the swapped hands", fair != null);
    Mana(p1, 10);
    Check("T15 GILA_906: I can play the boss's former cards", Play(g, y, p2.Hero));
    EndTurn(g);
    Mana(p2, 10);
    Check("T15 GILA_906: the boss can play my former cards (Fireball)", g.Process(PlayCardTask.Any(p2, a, p1.Hero)));
    var (_, _, g2) = Fight(Toki, 212, new[] { "GILA_906" });
    var c2 = Draw(g2, "GILA_906"); Mana(g2.Player1, 10); Clear(g2.Player1); Clear(g2.Player2); Generic.AddHandPhase(g2.Player1, c2);
    for (int i = 0; i < 10; i++) ToHand(g2, g2.Player2, "CS2_231");
    for (int i = 0; i < 9; i++) ToHand(g2, g2.Player1, "CS2_029");
    Play(g2, c2);
    Check("T15 GILA_906 with ten cards against nine: no card burns (10 and 9 after the swap)",
        g2.Player1.HandZone.Count == 10 && g2.Player2.HandZone.Count == 9 && g2.Player1.HandZone.All(c => c.Card.Id == "CS2_231"));
}

// ---------------------------------------------------------------- T16 GILA_907 Clockwork Assistant
{
    var (s, plan, g) = Fight(Toki, 221, new[] { "GILA_907" });
    var p1 = g.Player1;
    var cw = Draw(g, "GILA_907"); Mana(p1, 10);
    Play(g, ToHand(g, p1, "CS2_025"));
    Play(g, cw);
    var m = (Minion)cw;
    Check("T16 GILA_907: +1/+1 per spell I cast this game (one before it was played: 2/2)", m.AttackDamage == 2 && m.Health == 2, $"{m.AttackDamage}/{m.Health}");
    Play(g, ToHand(g, p1, "CS2_023")); Play(g, ToHand(g, p1, "CS2_027"));
    Check("T16 GILA_907: three spells: 4/4", m.AttackDamage == 4 && m.Health == 4);
    EndTurn(g);
    Mana(g.Player2, 10); g.Process(PlayCardTask.Any(g.Player2, ToHand(g, g.Player2, "CS2_023")));
    Check("T16 GILA_907: the boss's spells do not count", m.AttackDamage == 4);
}

// ---------------------------------------------------------------- T17 GILA_910 Puzzle Box
{
    string Box(int seed)
    {
        var (s, plan, g) = Fight(Toki, seed, new[] { "GILA_910" });
        var p1 = g.Player1;
        var box = Draw(g, "GILA_910"); Mana(p1, 10);
        var wisp = Board(g, p1, "CS2_231"); var yeti = Board(g, p1, "CS2_182");
        var enemy = Board(g, g.Player2, "CS2_172"); var big = Board(g, g.Player2, "NEW1_030");
        Play(g, box);
        var mine = p1.BoardZone.GetAll(); var theirs = g.Player2.BoardZone.GetAll();
        Check($"T17 GILA_910 ({seed}): every minion becomes a scripted minion costing (3) more (Wisp 0->3, Yeti 4->7, Raptor 2->5); Deathwing (10) has no 13-cost minion and stays",
            mine.Length == 2 && mine[0].Card.Cost == 3 && mine[1].Card.Cost == 7 && theirs.Length == 2 && theirs[0].Card.Cost == 5 && theirs[0].Controller == g.Player2
            && (theirs[1].Card.Id == "NEW1_030" || theirs[1].Card.Cost == 13) && mine.Concat(theirs).All(x => x.Card.Implemented),
            string.Join(",", mine.Concat(theirs).Select(x => x.Card.Id + ":" + x.Card.Cost)));
        return string.Join(",", mine.Concat(theirs).Select(x => x.Card.Id));
    }
    string a = Box(231), b = Box(231);
    Check("T17 GILA_910: the same seed makes the same minions", a == b, a + " | " + b);
}

// ---------------------------------------------------------------- T18 GILA_913 Double Time, GILA_904 Stroke of Midnight
{
    var (s, plan, g) = Fight(Toki, 241, new[] { "GILA_913" });
    var p1 = g.Player1;
    Check("T18 GILA_913: the player enchantment GILA_913e is attached at match start", p1.AppliedEnchantments.Any(e => e.Card.Id == "GILA_913e"));
    Mana(p1, 10); Clear(p1);
    int deck = p1.DeckZone.Count;
    Play(g, ToHand(g, p1, "CS2_023"));
    Check("T18 GILA_913: my first spell of the turn (Arcane Intellect) is cast again: 4 cards drawn", p1.DeckZone.Count == deck - 4, (deck - p1.DeckZone.Count).ToString());
    int d2 = p1.DeckZone.Count;
    Play(g, ToHand(g, p1, "CS2_023"));
    Check("T18 GILA_913: the second spell is not copied", p1.DeckZone.Count == d2 - 2);
    EndTurn(g); EndTurn(g);
    Mana(p1, 10);
    var enemy = Board(g, g.Player2, "CS2_182");
    int hp = g.Player2.Hero.Health, myHp = p1.Hero.Health;
    var fb = ToHand(g, p1, "CS2_029");
    Play(g, fb, enemy);
    Check("T18 GILA_913: next turn again: the Fireball kills the Yeti, the copy hits a random legal target (4 damage somewhere)",
        enemy.IsDead && (g.Player2.Hero.Health == hp - 6 || p1.Hero.Health == myHp - 6), $"{g.Player2.Hero.Health} {p1.Hero.Health}");
    EndTurn(g); EndTurn(g);
    Mana(p1, 10);
    var cs = ToHand(g, p1, "EX1_287");
    Play(g, cs);
    Check("T18 GILA_913: a Secret's copy fails as a duplicate (one Counterspell)", p1.SecretZone.Count(x => x.Card.Id == "EX1_287") == 1);

    var (_, _, g2) = Fight(Toki, 242, new[] { "GILA_904" });
    var q = g2.Player1;
    var stroke = Draw(g2, "GILA_904"); Mana(q, 10);
    Check("T18 GILA_904: not playable with no enemy minion", !q.Options().OfType<PlayCardTask>().Any(o => o.Source == stroke));
    var e1 = Board(g2, g2.Player2, "CS2_182"); var e2 = Board(g2, g2.Player2, "CS2_231");
    var mine = Board(g2, q, "CS2_182");
    Play(g2, stroke);
    var echo = q.HandZone.FirstOrDefault(x => x.Card.Id == "GILA_904");
    Check("T18 GILA_904: one random enemy minion is destroyed (mine stays); Echo leaves a copy in hand",
        (e1.IsDead ^ e2.IsDead) && !mine.IsDead && echo != null && echo.IsEcho);
    Play(g2, echo);
    Check("T18 GILA_904: the Echo copy destroys the other", e1.IsDead && e2.IsDead && g2.Player2.BoardZone.Count == 0);
}

// ---------------------------------------------------------------- T21 GVG_041 Dark Wispers (an engine fix GILA_903's reach exposed)
// Enhance-a-matic can add any class's spell; c-secretscan played its Druid spell Dark Wispers, whose buff option enchanted the
// spell itself (EntityType.SOURCE) and faulted. The option now buffs its target.
{
    var (s, plan, g) = Fight(Toki, 271, new string[0]);
    var p1 = g.Player1; Mana(p1, 10);
    var yeti = Board(g, p1, "CS2_182");
    var wispers = ToHand(g, p1, "GVG_041");
    var wOpts = p1.Options().OfType<PlayCardTask>().Where(o => o.Source == wispers).ToList();
    var buff = wOpts.FirstOrDefault(o => o.Target == yeti);
    Check("T21 GVG_041 option 1 (+5/+5 and Taunt) on a minion: the minion is buffed, no fault",
        buff != null && g.Process(buff) && yeti.AttackDamage == 9 && yeti.Health == 10 && yeti.HasTaunt, $"{yeti.AttackDamage}/{yeti.Health} taunt {yeti.HasTaunt}");
    var w2 = ToHand(g, p1, "GVG_041"); Mana(p1, 10);
    Check("T21 GVG_041 option 2 summons five Wisps", g.Process(p1.Options().OfType<PlayCardTask>().First(o => o.Source == w2 && o.Target == null)) && p1.BoardZone.GetAll().Count(m => m.Card.Id == "CS2_231") == 5);
}

// ---------------------------------------------------------------- T20 full games against the real boss AI, the player playing the id
var testBudget = new Budget(6000, 2000, 300, 6, 3, 100, 8, turnSearchSeconds: 6, turnWallSeconds: 8);
string AiGame(string id, long seed, string[] swapIn = null)
{
    string row = id == "GILA_400p" || shawTreasures.Contains(id) || id == Shaw ? Shaw : Toki;
    var s = NewAct(row, seed);
    bool isPower = id == "GILA_400p";
    bool isPassive = passives.Contains(id);
    bool isHero = id == Shaw || id == Toki;
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
    if (swapIn != null)
        foreach (string give in swapIn) Generic.AddHandPhase(game.Player1, Entity.FromCard(game.Player1, Cards.FromId(give)));
    game.DeathProcessingAndAuraUpdate();
    int decisionIndex = 0, actions = 0, decisions = 0, fallbacks = 0, boostedTurn = -1, powerUses = 0;
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
    var bossGila = game.HistoryFacts.Query.Plays().Where(f => f.ControllerId == game.Player2.Id && f.DefinitionKnown && Cards.FromAssetId(f.DbfId).Id.StartsWith("GILA_"))
        .Select(f => Cards.FromAssetId(f.DbfId).Id).ToList();
    return $"{result} turns {game.Turn} played-turn {playedTurn} power-uses {powerUses} decisions {decisions} fallbacks {fallbacks} boss-gila [{string.Join(",", bossGila)}] boss {boss.Id}";
}
foreach (string id in f4)
{
    string row;
    try { row = AiGame(id, 251); }
    catch (Exception e) { row = "FAULT " + e.GetType().Name + ": " + e.Message.Split('\n')[0]; }
    Console.WriteLine($"T20 {id,-11} {row}");
    Check($"T20 {id}: the player plays it in a full game against the boss AI, which ends without a fault or an AI fallback",
        !row.StartsWith("FAULT") && row.Contains("fallbacks 0 ") && !row.Contains("played-turn -1") && (id != "GILA_400p" || !row.Contains("power-uses 0 ")), row);
}
// ---------------------------------------------------------------- T19 Impetuous Companion hands the boss the player's treasures; the boss AI plays them
foreach (long seed in new long[] { 261, 262 })
{
    string row;
    try { row = AiGame("GILA_906", seed, new[] { "GILA_901", "GILA_904", "GILA_910", "GILA_903", "GILA_412", "GILA_401" }); }
    catch (Exception e) { row = "FAULT " + e.GetType().Name + ": " + e.Message.Split('\n')[0]; }
    Console.WriteLine($"T19 GILA_906/{seed} {row}");
    Check($"T19 GILA_906 ({seed}): the boss receives the player's treasures in the swap and the AI plays them in a full game without a fault or fallback",
        !row.StartsWith("FAULT") && row.Contains("fallbacks 0 ") && !row.Contains("played-turn -1") && !row.Contains("boss-gila []"), row);
}

Console.WriteLine($"{passes} passed, {failures} failed");
return failures == 0 ? 0 : 1;
