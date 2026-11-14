// c-heroes-F9: phase 6 family F9 of the unified Dungeon Run, the Tombs of Terror explorers: their four portraits (ULDA_Reno,
// ULDA_Finley, ULDA_Elise, ULDA_Brann) on the dual-class rows of run-v1.json (catalogClasses MAGE_ROGUE, PALADIN_SHAMAN,
// DRUID_PRIEST, HUNTER_WARRIOR; kind hero, Tombs, 30 Health, primary class = the portrait's class), each with its three
// explorer powers (ULDA_<Explorer>_HP1-3, the client's ADVENTURE_HERO_POWER order, unlocked at floor 4 and a win), the
// Random decks ULDA_900-903 (generated random-dual starters; the display card never a match card) and ULDA_039 Uldum
// Treasure Cache (the power Jr. and Sr. Tomb Diver give). MAGE_ROGUE shipped enabled; the other three shipped disabled until
// family F10 scripted their fixed starters (since F10 all four ship enabled; c-starters-F10 tests the starters). For every id: the engine
// content (scripted, keyed and shelled by its own client id, name and text as facts/violet-hold-2026-09.json has them), the
// run data, the validator and admission checks, a scripted regression game through the run setup path (DungeonRun.NewRun
// on the hero row -> BuildPlan -> DungeonRunSetup.Configure -> Game -> ApplyAfterStart -> mulligan -> MainBegin -> the
// power used; the disabled rows through a test fixture that enables them with a fixed deck), and a full game against the
// real boss AI (DungeonRunBossPilot + GoldenPilot) in which the player uses the power; it must end without a fault or an AI
// fallback. Headless: compiles C:\SoloStone\plugin\DungeonRun\*.cs and the NPC AI against the engine build. Run it twice
// and compare.
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
bool HasC(Controller p, string ench) => p.AppliedEnchantments?.Any(e => e.Card.Id == ench) == true;
var clientIds = new HashSet<string>(System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "dungeon-run/reference/client-tables.json")))
    .RootElement.GetProperty("tables").GetProperty("CARD").EnumerateArray().Select(r => r.GetProperty("id").GetString()), StringComparer.Ordinal);
// The client's ADVENTURE_HERO_POWER rows for the Tombs (adventure 513): power id -> sortOrder.
var clientPowerOrder = new Dictionary<string, int>(StringComparer.Ordinal);
foreach (var r in System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "dungeon-run/reference/client-tables.json")))
    .RootElement.GetProperty("tables").GetProperty("ADVENTURE_HERO_POWER").EnumerateArray())
    if (r.GetProperty("adventureId").GetInt32() == 513 && r.GetProperty("cardId_card").GetString().StartsWith("ULDA_"))
        clientPowerOrder[r.GetProperty("cardId_card").GetString()] = r.GetProperty("sortOrder").GetInt32();
CardType FactType(string id) => FactTag(id, 202) switch { 3 => CardType.HERO, 4 => CardType.MINION, 5 => CardType.SPELL, 6 => CardType.ENCHANTMENT, 7 => CardType.WEAPON, 10 => CardType.HERO_POWER, _ => CardType.INVALID };
string ClassOfTag(int v) => v switch { 2 => "DRUID", 3 => "HUNTER", 4 => "MAGE", 5 => "PALADIN", 6 => "PRIEST", 7 => "ROGUE", 8 => "SHAMAN", 9 => "WARLOCK", 10 => "WARRIOR", 12 => "NEUTRAL", _ => "?" + v };

// explorer, row, portrait, the row's power order (client sortOrder), display card of the Random deck, Random deck id, enabled
var explorers = new (string X, string Row, string Hero, string[] Powers, string Display, string RandomDeck, bool Enabled)[]
{
    ("Reno", "MAGE_ROGUE", "ULDA_Reno", new[] { "ULDA_Reno_HP1", "ULDA_Reno_HP2", "ULDA_Reno_HP3" }, "ULDA_901", "tombs_reno_random", true),
    ("Finley", "PALADIN_SHAMAN", "ULDA_Finley", new[] { "ULDA_Finley_HP1", "ULDA_Finley_HP2", "ULDA_Finley_HP3" }, "ULDA_902", "tombs_finley_random", true),
    ("Elise", "DRUID_PRIEST", "ULDA_Elise", new[] { "ULDA_Elise_HP1", "ULDA_Elise_HP3", "ULDA_Elise_HP2" }, "ULDA_900", "tombs_elise_random", true),
    ("Brann", "HUNTER_WARRIOR", "ULDA_Brann", new[] { "ULDA_Brann_HP1", "ULDA_Brann_HP2", "ULDA_Brann_HP3" }, "ULDA_903", "tombs_brann_random", true),
};
string[] powers = explorers.SelectMany(e => e.Powers).OrderBy(p => Array.IndexOf(DungeonRunContent.TombsExplorerPowers.ToArray(), p)).ToArray();
string[] passives = { "ULDA_Elise_HP3", "ULDA_Brann_HP3" };
string[] tokens = { "ULDA_Finley_HP1t", "ULDA_Elise_HP1a", "ULDA_Elise_HP1b" };
string RowOf(string power) => explorers.First(e => e.Powers.Contains(power)).Row;
var effectShells = new Dictionary<string, string>
{
    ["fx_ULDA_Reno_HP2e"] = "ULDA_Reno_HP2e", ["fx_ULDA_Elise_HP3"] = "ULDA_Elise_HPe", ["fx_ULDA_Brann_HP3"] = "ULDA_Brann_HPe",
};
var allIds = explorers.Select(e => e.Hero).Concat(powers).Concat(new[] { "ULDA_039" }).Concat(explorers.Select(e => e.Display)).ToArray();

// ---------------------------------------------------------------- T1 content
Check("T1 21 F9 ids: 4 portraits, 12 powers, ULDA_039, 4 Random-deck cards", allIds.Length == 21 && allIds.Distinct().Count() == 21);
Check("T1 DungeonRunContent.TombsExplorerPowers lists the 12 powers, explorer by explorer, HP1 to HP3",
    DungeonRunContent.TombsExplorerPowers.SequenceEqual(explorers.SelectMany(e => new[] { 1, 2, 3 }.Select(i => $"ULDA_{e.X}_HP{i}"))));
foreach (var e in explorers)
{
    var content = DungeonRunContent.Get(e.Hero);
    var card = C(e.Hero);
    string primary = e.Row.Split('_')[0];
    Check($"T1 {e.Hero}: a Hero keyed and shelled by its own id, scripted (Implemented), the client's {primary} hero at 30 Health",
        content.Kind == RunContentKind.Hero && content.ShellId == e.Hero && card.Implemented && card.Type == CardType.HERO && card.Class.ToString() == primary
        && ClassOfTag(FactTag(e.Hero, 199)) == primary && card.Health == 30 && content.Health == 30 && FactTag(e.Hero, 45) == 15 && clientIds.Contains(e.Hero)
        && DungeonRunContent.ForShell(e.Hero) == content, $"{card.Class} {card.Health} {card.Implemented}");
    Check($"T1 ids-text-match {e.Hero}: name and text equal the client's", content.Name == FactName(e.Hero) && content.Text == FactText(e.Hero));
}
foreach (string id in powers.Concat(new[] { "ULDA_039" }).Concat(tokens))
{
    var content = DungeonRunContent.Get(id);
    var card = C(id);
    bool power = card.Type == CardType.HERO_POWER;
    var want = passives.Contains(id) ? RunContentKind.PassivePower : power ? RunContentKind.HeroPower : RunContentKind.ActiveTreasure;
    Check($"T1 {id}: a {want} keyed and shelled by its own id, scripted, the client's {FactType(id)} of class {ClassOfTag(FactTag(id, 199))}",
        content.Kind == want && content.ShellId == id && card.Implemented && card.Type == FactType(id) && DungeonRunContent.ForShell(id) == content && clientIds.Contains(id)
        && card.Class.ToString() == ClassOfTag(FactTag(id, 199)), content.Kind + " " + card.Type + " " + card.Implemented + " " + card.Class);
    Check($"T1 ids-text-match {id}: name and text equal facts/violet-hold-2026-09.json", content.Name == FactName(id) && content.Text == FactText(id), content.Name + " | " + content.Text);
    Check($"T1 {id}: cost {FactTag(id, 48)}" + (card.Type == CardType.MINION ? $", stats {FactTag(id, 47)}/{FactTag(id, 45)}" : "") + " as the client prints it",
        card.Cost == FactTag(id, 48) && content.Cost == FactTag(id, 48) && (card.Type != CardType.MINION || card.ATK == FactTag(id, 47) && card.Health == FactTag(id, 45)),
        $"{card.Cost} {card.ATK}/{card.Health}");
}
foreach (var fx in effectShells)
{
    var e = DungeonRunContent.Get(fx.Key);
    Check($"T1 {fx.Value}: content {fx.Key} on the client's own ENCHANTMENT, scripted, name and text word for word",
        e.Kind == RunContentKind.Effect && e.ShellId == fx.Value && C(fx.Value).Implemented && C(fx.Value).Type == CardType.ENCHANTMENT && clientIds.Contains(fx.Value)
        && e.Name == FactName(fx.Value) && e.Text == FactText(fx.Value), e.Name + " | " + e.Text);
}
Check("T1 the two passive powers are engine passives activated at game start (DungeonRunCardsGen.PassiveHeroPowers) and attach their watcher",
    passives.All(p => DungeonRunCardsGen.PassiveHeroPowers.Contains(p) && HeroPower.ActivatesAtGameStart(C(p)))
    && DungeonRunContent.Get("ULDA_Elise_HP3").EffectKey == "fx_ULDA_Elise_HP3" && DungeonRunContent.Get("ULDA_Brann_HP3").EffectKey == "fx_ULDA_Brann_HP3");
Check("T1 ULDA_Elise_HP1 is a Choose One power whose choices resolve to Defensive Posture (+2 Armor) and Offensive Posture (+2 Attack)",
    C("ULDA_Elise_HP1").ChooseOne && DungeonRunSetup.ChooseOneOptionIds(C("ULDA_Elise_HP1")).SequenceEqual(new[] { "ULDA_Elise_HP1a", "ULDA_Elise_HP1b" }));
Check("T1 tokens: New \"Recruits\" lists the Amalgam Explorer it summons (a 2/1 of every type); no other power summons", DungeonRunContent.Get("ULDA_Finley_HP1").Tokens.SequenceEqual(new[] { "ULDA_Finley_HP1t" })
    && powers.Concat(new[] { "ULDA_039" }).Except(new[] { "ULDA_Finley_HP1" }).All(p => DungeonRunContent.Get(p).Tokens.Count == 0)
    && C("ULDA_Finley_HP1t").IsRace(Race.MURLOC) && C("ULDA_Finley_HP1t").IsRace(Race.DEMON) && C("ULDA_Finley_HP1t").IsRace(Race.MECHANICAL));
Check("T1 Bubble Blower carries the Overload (1) its text names (the client's card has no OVERLOAD tag)", C("ULDA_Finley_HP2").HasOverload && C("ULDA_Finley_HP2").Overload == 1);
Check("T1 the client's power order (ADVENTURE_HERO_POWER sortOrder) is each row's order: Elise is HP1, HP3, HP2",
    explorers.All(e => e.Powers.Select(p => clientPowerOrder[p]).SequenceEqual(new[] { 0, 1, 2 })), string.Join(",", clientPowerOrder.Select(p => p.Key + ":" + p.Value)));
foreach (var e in explorers)
    Check($"T1 {e.Display} ({FactName(e.Display)}): a run-layer loadout card, no engine content entry, the display card of {e.RandomDeck}",
        DungeonRunContent.ForShell(e.Display) == null && !DungeonRunContent.TryGet(e.Display, out _) && clientIds.Contains(e.Display)
        && ClassOfTag(FactTag(e.Display, 199)) == e.Row.Split('_')[0]);

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
{
    var errors = DungeonRunValidator.Validate(data, ClientKnows);
    Check("T2 shipped data validates with 0 errors (client check through the transported id)", errors.Count == 0, string.Join(" | ", errors.Take(5)));
    var legacy = DungeonRunData.Parse(runJson, poolJson, npcJson);
    var le = DungeonRunValidator.Validate(legacy, null);
    Check("T2 the data without the catalog validates with 0 errors and loads no Tombs row", le.Count == 0 && explorers.All(e => legacy.FindClass(e.Row) == null), string.Join(" | ", le.Take(3)));
    foreach (var e in explorers)
    {
        var r = e.Enabled ? data.FindClass(e.Row) : data.DisabledClasses.FirstOrDefault(c => c.Id == e.Row);
        string low = e.Row.ToLowerInvariant();
        string[] cls = e.Row.Split('_');
        Check($"T2 row {e.Row}: kind hero, Tombs, portrait {e.Hero} (primary class {cls[0]}), 30 Health, classes [{cls[0]}, {cls[1]}], {(e.Enabled ? "enabled" : "disabled with a reason naming family F10")}",
            r != null && r.IsHero && r.IsDual && r.Adventure == "Tombs" && r.HeroCard == e.Hero && r.Health == 30 && r.ClassSet.SequenceEqual(cls) && r.Enabled == e.Enabled
            && (e.Enabled ? data.DisabledClasses.All(c => c.Id != e.Row) : data.FindClass(e.Row) == null && r.DisabledReason.StartsWith("run:adventure-loadouts:") && r.DisabledReason.Contains("F10")));
        Check($"T2 row {e.Row}: powers [{string.Join(", ", e.Powers)}]: the first open, the second at floor 4, the third at a win (the stand-in powers are gone)",
            r.Powers.SequenceEqual(e.Powers) && r.PowerUnlock(e.Powers[0]) == null && r.PowerUnlock(e.Powers[1]) == low + "_floor4" && r.PowerUnlock(e.Powers[2]) == low + "_win"
            && r.PowerUnlocks.Count == 2 && (!e.Enabled || data.Unlocks.ContainsKey(low + "_floor4") && data.Unlocks.ContainsKey(low + "_win")));
        var rd = (e.Enabled ? data.Decks : data.DisabledDecks).FirstOrDefault(d => d.Id == e.RandomDeck);
        Check($"T2 {e.RandomDeck}: the Random deck, random-dual, display card {e.Display} named '{FactName(e.Display)}', no cards, unlocked by a win; its pool holds at least 5 cards of each class",
            rd != null && rd.Class == e.Row && rd.IsGenerated && rd.Generator == DungeonRunStarterDeck.GeneratorRandomDual && rd.DisplayCard == e.Display && rd.Cards.Count == 0
            && rd.Unlock == low + "_win" && rd.Name == FactName(e.Display) && rd.Description == FactText(e.Display) && r.Decks.Last() == e.RandomDeck
            && cls.All(c => DungeonRunValidator.GeneratorPool(data, r, c).Count >= 5),
            string.Join(",", cls.Select(c => c + ":" + DungeonRunValidator.GeneratorPool(data, r, c).Count)));
    }
    Check("T2 MAGE_ROGUE keeps its first deck, F10 adds Deck2 and Deck3, the Random deck last",
        data.Class("MAGE_ROGUE").Decks.SequenceEqual(new[] { "tombs_reno_spellslinger", "tombs_reno_spell_seeker", "tombs_reno_secret_traps", "tombs_reno_random" }) && data.Class("MAGE_ROGUE").DefaultDeck == "tombs_reno_spellslinger");

    // Validator branches
    var e1 = DungeonRunValidator.Validate(Load(Swap(runJson, "\"powers\": [\"ULDA_Elise_HP1\", \"ULDA_Elise_HP3\", \"ULDA_Elise_HP2\"]",
        "\"powers\": [\"ULDA_Elise_HP1\", \"ULDA_Elise_HP3\", \"LOOTA_801\"]")), null);
    Check("T2 validator: a hero row ships whole: a non-power in its power list is refused", e1.Any(x => x.Contains("DRUID_PRIEST") && x.Contains("LOOTA_801")), string.Join(" | ", e1.Take(3)));
    var e2 = DungeonRunValidator.Validate(Load(Swap(runJson, "\"powers\": [\"ULDA_Reno_HP1\", \"ULDA_Reno_HP2\", \"ULDA_Reno_HP3\"], \"powerUnlocks\": {\"ULDA_Reno_HP2\": \"mage_rogue_floor4\", \"ULDA_Reno_HP3\": \"mage_rogue_win\"}",
        "\"powers\": [\"ULDA_Elise_HP1\", \"ULDA_Reno_HP2\", \"ULDA_Reno_HP3\"], \"powerUnlocks\": {\"ULDA_Reno_HP2\": \"mage_rogue_floor4\", \"ULDA_Reno_HP3\": \"mage_rogue_win\"}")), null);
    Check("T2 validator: the Choose One power is accepted on a hero row (both choices scripted and admitted)", e2.Count == 0, string.Join(" | ", e2.Take(3)));
    var e3 = DungeonRunValidator.Validate(Load(Swap(runJson, "\"powers\": [\"CS2_034\", \"carto_survey\", \"mage_rimebite\"]", "\"powers\": [\"CS2_034\", \"carto_survey\", \"ULDA_Elise_HP1\"]")
        .Replace("\"mage_rimebite\": \"mage_win\"", "\"ULDA_Elise_HP1\": \"mage_win\"")), null);
    Check("T2 validator: on a class row the Choose One power is still refused (validation:choose-one-power), as is a passive power",
        e3.Any(x => x.StartsWith("ULDA_Elise_HP1") && x.Contains("choose-one")), string.Join(" | ", e3.Take(3)));
    var e4 = DungeonRunValidator.Validate(Load(Swap(runJson, "\"powers\": [\"CS2_034\", \"carto_survey\", \"mage_rimebite\"]", "\"powers\": [\"CS2_034\", \"carto_survey\", \"ULDA_Brann_HP3\"]")
        .Replace("\"mage_rimebite\": \"mage_win\"", "\"ULDA_Brann_HP3\": \"mage_win\"")), null);
    Check("T2 validator: on a class row a passive power is refused", e4.Any(x => x.StartsWith("ULDA_Brann_HP3") && (x.Contains("passive") || x.Contains("PassivePower"))), string.Join(" | ", e4.Take(3)));
}

// ---------------------------------------------------------------- T2 admission
foreach (string id in powers.Concat(new[] { "ULDA_039" }).Concat(tokens))
{
    var p = DungeonRunAdmission.Classify(C(id), id, AdmitScope.Player, null, catalogRun: true);
    var b = DungeonRunAdmission.Classify(C(id), id, AdmitScope.Boss, null, catalogRun: true);
    string family = id == "ULDA_Reno_HP3" ? "run-hero-power-loop" : id == "ULDA_Brann_HP3" ? "from-own-deck" : id == "ULDA_039" ? "run-treasure-discover"
        : tokens.Contains(id) ? "run-hero-power-token" : "run-hero-power";
    Check($"T2 admission {id}: the {family} row admits the player, never the boss; the boss's generation never yields it",
        p.Subject && p.Admitted && !b.Admitted && DungeonRunAdmission.RuleFor(id)?.Family == family
        && DungeonRunAdmission.Classify(C(id), id, AdmitScope.Player).Admitted && DungeonRunAdmission.GeneratedRejectsBoss(true).Contains(id), p.Family + " " + p.Admit);
}
Check("T2 admission ULDA_Reno_HP3 carries the loop guard 'RepeatUntil max=60', the bound the script uses",
    DungeonRunAdmission.RuleFor("ULDA_Reno_HP3")?.Guard == "RepeatUntil max=60" && DungeonRunContent.ArcaneCraftinessMaxVolleys == 60);
foreach (var e in explorers)
    Check($"T2 admission {e.Hero}: the portrait needs no row (no rules text) and is admitted for the player", DungeonRunAdmission.Classify(C(e.Hero), e.Hero, AdmitScope.Player, null, true).Admitted);

// ---------------------------------------------------------------- the run setup path
// The three disabled rows play through a fixture that enables each with a fixed deck of ten cards of its own generator pool
// (five of each class), beside its shipped Random deck: the shipped rows, powers and decks, only the enable flag and a deck added.
var fixtureDecks = new Dictionary<string, List<string>>();
string fixtureJson = runJson;
foreach (var e in explorers.Where(x => !x.Enabled))
{
    var r = data.DisabledClasses.First(c => c.Id == e.Row);
    var cards = r.ClassSet.SelectMany(c => DungeonRunValidator.GeneratorPool(data, r, c).Take(5)).ToList();
    fixtureDecks[e.Row] = cards;
    fixtureJson = Swap(fixtureJson, $"\"decks\": [\"{e.RandomDeck}\"], \"enabled\": false, \"disabledReason\": \"{r.DisabledReason}\"", $"\"decks\": [\"fx_{e.X.ToLowerInvariant()}_1\", \"{e.RandomDeck}\"]");
    fixtureJson = Swap(fixtureJson, "  \"catalogStarterDecks\": [\n", "  \"catalogStarterDecks\": [\n"
        + $"    {{\"id\": \"fx_{e.X.ToLowerInvariant()}_1\", \"class\": \"{e.Row}\", \"name\": \"Fixture {e.X}\", \"description\": \"Test fixture.\", \"cards\": [{string.Join(", ", cards.Select(x => "\"" + x + "\""))}]}},\n");
}
var fixture = Load(fixtureJson);
var fixtureRun = new DungeonRun(fixture);
var fixtureAll = new DungeonRunProfile(); fixtureAll.Unlocked.AddRange(fixture.Unlocks.Keys);
{
    var fe = DungeonRunValidator.Validate(fixture, ClientKnows);
    Check("T3 the fixture (the three rows enabled, each with a fixed deck of its own pool) validates with 0 errors: the shipped powers and Random decks are whole",
        fe.Count == 0 && explorers.All(e => fixture.FindClass(e.Row) != null), string.Join(" | ", fe.Take(5)));
}
(DungeonRunData D, DungeonRun Run, DungeonRunProfile All) For(string row) =>
    explorers.First(e => e.Row == row).Enabled ? (data, run, all) : (fixture, fixtureRun, fixtureAll);

GameConfig Configured(RunMatchPlan plan, int seed)
{
    var cfg = new GameConfig { RandomSeed = seed, Shuffle = true, SkipMulligan = false, History = true, Logging = false, FormatType = FormatType.FT_WILD };
    DungeonRunSetup.Configure(cfg, plan);
    return cfg;
}
DungeonRunState NewAct(string row, long seed, string power = null, string deck = null)
{
    var (d, r, a) = For(row);
    var cls = d.Class(row);
    var s = r.NewRun(a, cls.Id, deck ?? cls.DefaultDeck, power ?? cls.DefaultPower, seed, signatureId: r.SignatureOffer(a, r.Data.FindClass(cls.Id), seed).FirstOrDefault());
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
(DungeonRunState s, RunMatchPlan plan, Game g) Fight(string power, long seed, string row = null)
{
    row = row ?? RowOf(power);
    var s = NewAct(row, seed, power);
    var plan = For(row).Run.BuildPlan(s);
    var g = StartFight(plan, For(row).Run.MatchSeed(s));
    if (g.Player1.Hero.HeroPower.Card.Id != power) throw new Exception("the match did not start with " + power);
    return (s, plan, g);
}
void Mana(Controller c, int n) { c.BaseMana = n; c.UsedMana = 0; c.OverloadLocked = 0; c.OverloadOwed = 0; }
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
bool UsePower(Game g, ICharacter target = null) => g.Process(HeroPowerTask.Any(g.Player1, target));
bool CanTarget(Game g, ICharacter target) => g.Player1.Options().OfType<HeroPowerTask>().Any(o => o.Target == target);
// A choice answered as the native flow does: through the pending decision (a suspended effect, Dino Tracking), else the task.
bool Pick(Game g, int id)
{
    var decision = g.GetPendingDecisions().FirstOrDefault(d => d.ControllerId == g.Player1.Id && d.Kind != SabberStoneCore.Model.Decisions.DecisionKind.Options);
    if (decision != null) return g.TryAnswer(new SabberStoneCore.Model.Decisions.DecisionAnswer(decision.Id, decision.ControllerId, new[] { id }), out _);
    return g.Process(ChooseTask.Pick(g.Player1, id));
}
void EndTurn(Game g) => g.Process(EndTurnTask.Any(g.CurrentPlayer));
void Clear(Controller c) { foreach (var e in c.HandZone.ToArray()) { c.HandZone.Remove(e); c.SetasideZone.Add(e); } }
void ClearBoards(Game g) { foreach (var c in new[] { g.Player1, g.Player2 }) foreach (var m in c.BoardZone.ToArray()) { c.BoardZone.Remove(m); c.SetasideZone.Add(m); } }
// Pass both turns, answering a pending Dino Tracking Discover with its first offer.
void NextTurn(Game g)
{
    EndTurn(g);
    if (g.CurrentPlayer == g.Player2) EndTurn(g);
    if (g.Player1.Choice != null) Pick(g, g.Player1.Choice.Choices[0]);
}

// ---------------------------------------------------------------- T3 NewRun / BuildPlan on every explorer row, power and deck
foreach (var e in explorers)
{
    var (d, r, a) = For(e.Row);
    var cls = d.Class(e.Row);
    string[] classes = e.Row.Split('_');
    foreach (string deckId in cls.Decks)
    {
        var deck = d.FindDeck(deckId);
        foreach (string power in cls.Powers)
        {
            var s = r.NewRun(a, e.Row, deckId, power, 5, signatureId: r.SignatureOffer(a, r.Data.FindClass(e.Row), 5).FirstOrDefault());
            var plan = r.BuildPlan(s);
            // Since F11 the run deck also holds the picked signature treasure (a card record of origin signature).
            var st = s.Cards.Where(c => c.Origin == DungeonRunDeck.OriginStarter).Select(c => c.Id).ToList();
            bool deckOk = deck.IsGenerated
                ? st.Count == 10 && st.Distinct().Count() == 10 && !st.Contains(e.Display)
                    && st.Take(5).All(id => C(id).Class.ToString() == classes[0] && DungeonRunValidator.GeneratorPool(d, cls, classes[0]).Contains(id))
                    && st.Skip(5).All(id => C(id).Class.ToString() == classes[1] && DungeonRunValidator.GeneratorPool(d, cls, classes[1]).Contains(id))
                : st.SequenceEqual(deck.Cards);
            Check($"T3 NewRun {e.Row} on {deckId} with {power}: an act catalog run of the hero row, its starter in the run deck (origin starter), the power, and (F11) one signature",
                s.Class == e.Row && s.IsActRun && s.IsCatalogRun && s.StarterDeck == deckId && s.Power == power && deckOk && s.Cards.Count == 11
                && s.Cards.Count(c => c.Origin == DungeonRunDeck.OriginSignature) == 1
                && s.Loadout?.Id == e.Row && s.Loadout.Signature.Count == 1 && s.Cards.Single(c => c.Origin == DungeonRunDeck.OriginSignature).Id == s.Loadout.Signature[0], string.Join(",", s.Deck));
            Check($"T3 BuildPlan {e.Row} {deckId} {power}: the portrait {e.Hero} at 30 Health, the power, the run deck",
                plan.PlayerHero.Id == e.Hero && plan.PlayerHeroHealth == 30 && plan.PlayerHeroPowerId == power && plan.PlayerGeneratedSecrets
                && plan.PlayerDeck.Select(c => c.Id).OrderBy(x => x).SequenceEqual(s.Deck.OrderBy(x => x)));
        }
        if (deck.IsGenerated)
        {
            var x1 = r.NewRun(a, e.Row, deckId, e.Powers[0], 77, signatureId: r.SignatureOffer(a, r.Data.FindClass(e.Row), 77).FirstOrDefault());
            var x2 = r.NewRun(a, e.Row, deckId, e.Powers[0], 77, signatureId: r.SignatureOffer(a, r.Data.FindClass(e.Row), 77).FirstOrDefault());
            var x3 = r.NewRun(a, e.Row, deckId, e.Powers[0], 78, signatureId: r.SignatureOffer(a, r.Data.FindClass(e.Row), 78).FirstOrDefault());
            Check($"T3 {deckId} ({e.Display}): the Random deck is repeatable by seed and changes with it; the shipped row draws the same deck as the fixture's",
                x1.Deck.SequenceEqual(x2.Deck) && !x1.Deck.SequenceEqual(x3.Deck)
                && (e.Enabled || run.GenerateStarter(data.DisabledClasses.First(c => c.Id == e.Row), data.DisabledDecks.First(dd => dd.Id == deckId), 77).SequenceEqual(x1.Deck)),
                string.Join(",", x1.Deck) + " / " + string.Join(",", x3.Deck));
        }
    }
    var fresh = new DungeonRunProfile();
    bool Refused(Func<DungeonRunState> f) { try { f(); return false; } catch (InvalidOperationException) { return true; } }
    Check($"T3 {e.Row}: a new profile starts with {e.Powers[0]} on the first deck only; the other powers and the Random deck are locked",
        r.NewRun(fresh, e.Row, cls.DefaultDeck, e.Powers[0], 3, signatureId: r.SignatureOffer(fresh, r.Data.FindClass(e.Row), 3).FirstOrDefault()).Power == e.Powers[0]
        && Refused(() => r.NewRun(fresh, e.Row, cls.DefaultDeck, e.Powers[1], 3, signatureId: r.SignatureOffer(fresh, r.Data.FindClass(e.Row), 3).FirstOrDefault())) && Refused(() => r.NewRun(fresh, e.Row, cls.DefaultDeck, e.Powers[2], 3, signatureId: r.SignatureOffer(fresh, r.Data.FindClass(e.Row), 3).FirstOrDefault()))
        && Refused(() => r.NewRun(fresh, e.Row, e.RandomDeck, e.Powers[0], 3, signatureId: r.SignatureOffer(fresh, r.Data.FindClass(e.Row), 3).FirstOrDefault()))
        && r.AvailablePowers(cls, fresh).SequenceEqual(new[] { e.Powers[0] }) && r.AvailableDecks(cls, fresh).Select(x => x.Id).SequenceEqual(new[] { cls.DefaultDeck }));
    var floor5 = new DungeonRunProfile(); floor5.Heroes.Add(new DungeonRunHeroStats { Class = e.Row, BestFloor = 5 }); r.RefreshUnlocks(floor5);
    Check($"T3 {e.Row}: reaching floor 5 with the explorer unlocks {e.Powers[1]}; {e.Powers[2]} and the Random deck still need a win",
        r.AvailablePowers(cls, floor5).SequenceEqual(e.Powers.Take(2)) && !r.AvailableDecks(cls, floor5).Any(x => x.IsGenerated), string.Join(",", floor5.Unlocked));
    var g = new Game(Configured(r.BuildPlan(r.NewRun(a, e.Row, cls.DefaultDeck, e.Powers[2], 9, signatureId: r.SignatureOffer(a, r.Data.FindClass(e.Row), 9).FirstOrDefault())), 41));
    g.StartGame();
    Check($"T3 {e.Row}: the match starts the portrait {e.Hero} at 30 Health with {e.Powers[2]}", g.Player1.Hero.Card.Id == e.Hero && g.Player1.Hero.Health == 30
        && g.Player1.Hero.BaseHealth == 30 && g.Player1.Hero.HeroPower.Card.Id == e.Powers[2] && g.Player1.Hero.Card.Class.ToString() == classes[0]);
}

// ---------------------------------------------------------------- T4 ULDA_Reno_HP1 Amateur Mage
{
    var (s, plan, g) = Fight("ULDA_Reno_HP1", 101);
    var p1 = g.Player1; var p2 = g.Player2; Mana(p1, 10); Clear(p1);
    var yeti = Board(g, p2, "CS2_182");
    Check("T4 Amateur Mage targets any character, costs (2)", CanTarget(g, yeti) && CanTarget(g, p2.Hero) && CanTarget(g, p1.Hero) && p1.Hero.HeroPower.Cost == 2);
    UsePower(g, yeti);
    Check("T4 Amateur Mage without a Combo: 1 damage", yeti.Health == 4 && p1.RemainingMana == 8, yeti.Health.ToString());
    NextTurn(g); Mana(p1, 10);
    Play(g, ToHand(g, p1, "GAME_005"));
    int hp = p2.Hero.Health;
    UsePower(g, p2.Hero);
    Check("T4 Amateur Mage after a card this turn (Combo): 2 damage", p2.Hero.Health == hp - 2 && p1.IsComboActive, $"{hp}->{p2.Hero.Health}");
}

// ---------------------------------------------------------------- T5 ULDA_Reno_HP2 "Relicologist"
{
    var (s, plan, g) = Fight("ULDA_Reno_HP2", 111);
    var p1 = g.Player1; var p2 = g.Player2; Mana(p1, 10); Clear(p1);
    Check("T5 \"Relicologist\" costs (0) and has no target", p1.Hero.HeroPower.Cost == 0 && !CanTarget(g, p2.Hero) && p1.Options().OfType<HeroPowerTask>().Count() == 1);
    UsePower(g);
    Check("T5 \"Relicologist\": its owner gets Relic Power (ULDA_Reno_HP2e)", HasC(p1, "ULDA_Reno_HP2e"));
    int hp = p2.Hero.Health;
    Play(g, ToHand(g, p1, "CS2_008"), p2.Hero);
    Check("T5 the next spell has Spell Damage +2: Moonfire deals 3; Relic Power is gone", p2.Hero.Health == hp - 3 && !HasC(p1, "ULDA_Reno_HP2e"), $"{hp}->{p2.Hero.Health}");
    hp = p2.Hero.Health;
    Play(g, ToHand(g, p1, "CS2_008"), p2.Hero);
    Check("T5 the spell after it is normal: Moonfire deals 1", p2.Hero.Health == hp - 1, $"{hp}->{p2.Hero.Health}");
    NextTurn(g); Mana(p1, 10); Clear(p1);
    UsePower(g);
    EndTurn(g);
    Check("T5 Relic Power ends with the turn", !HasC(p1, "ULDA_Reno_HP2e"));
    EndTurn(g); Mana(p1, 10);
    hp = p2.Hero.Health;
    Play(g, ToHand(g, p1, "CS2_008"), p2.Hero);
    Check("T5 a spell next turn is normal: Moonfire deals 1", p2.Hero.Health == hp - 1, $"{hp}->{p2.Hero.Health}");
}

// ---------------------------------------------------------------- T6 ULDA_Reno_HP3 Arcane Craftiness (repeat while a minion dies, guarded)
{
    var (s, plan, g) = Fight("ULDA_Reno_HP3", 121);
    var p1 = g.Player1; var p2 = g.Player2; Mana(p1, 10); ClearBoards(g);
    int hp = p2.Hero.Health;
    UsePower(g);
    Check("T6 Arcane Craftiness with no minion: two missiles, 2 damage to the boss hero, no repeat", p2.Hero.Health == hp - 2, $"{hp}->{p2.Hero.Health}");
    int Dead(Game x) => x.Player1.GraveyardZone.Count(p => p is Minion) + x.Player2.GraveyardZone.Count(p => p is Minion);
    string Volleys(long seed, int raiders, out int damage, out int deaths, out bool lastKilledNone)
    {
        var (_, _, gg) = Fight("ULDA_Reno_HP3", seed);
        var q1 = gg.Player1; var q2 = gg.Player2; Mana(q1, 10); ClearBoards(gg);
        var list = Enumerable.Range(0, raiders).Select(_ => Board(gg, q2, "CS2_168")).ToList();
        int h0 = q2.Hero.Health, d0 = Dead(gg);
        UsePower(gg);
        damage = h0 - q2.Hero.Health + list.Sum(m => m.Damage);
        deaths = Dead(gg) - d0;
        lastKilledNone = list.Any(m => m.Zone?.Type == Zone.PLAY) || deaths == raiders;
        return string.Join(",", list.Select(m => m.Zone?.Type.ToString() + ":" + m.Health)) + " hero " + q2.Hero.Health;
    }
    var line1 = Volleys(122, 3, out int dmg1, out int deaths1, out bool stop1);
    var line2 = Volleys(122, 3, out int dmg2, out int deaths2, out bool stop2);
    Check("T6 Arcane Craftiness on three 2/1s: it repeats while a minion dies (an even number of missiles, at least one volley per death plus the last), and stops",
        dmg1 % 2 == 0 && dmg1 >= 2 && dmg1 / 2 >= deaths1 && dmg1 / 2 <= deaths1 + 1 && stop1, $"damage {dmg1} deaths {deaths1} {line1}");
    Check("T6 Arcane Craftiness: the same seed fires the same missiles (game RNG)", dmg1 == dmg2 && deaths1 == deaths2 && line1 == line2);
    // H9: seven Wax Ragers (5/1, Deathrattle: resummon this minion) die and come back each volley: only the guard stops it.
    {
        var (_, _, gg) = Fight("ULDA_Reno_HP3", 131);
        var q1 = gg.Player1; var q2 = gg.Player2; Mana(q1, 10); ClearBoards(gg);
        for (int i = 0; i < 7; i++) Board(gg, q2, "LOOTA_840");
        q2.Hero.IsImmune = true;
        int d0 = Dead(gg);
        var watch = System.Diagnostics.Stopwatch.StartNew();
        UsePower(gg);
        int deaths = Dead(gg) - d0;
        Check("T6 Arcane Craftiness against minions that resummon themselves: the loop guard ends it (at most 60 volleys, 120 deaths), the game goes on",
            deaths > 7 && deaths <= 2 * DungeonRunContent.ArcaneCraftinessMaxVolleys && gg.State == State.RUNNING && watch.Elapsed.TotalSeconds < 30 && q1.Hero.HeroPower.IsExhausted,
            $"deaths {deaths} in {watch.Elapsed.TotalSeconds:0.0}s");
    }
}

// ---------------------------------------------------------------- T7 ULDA_Finley_HP1 New "Recruits"
{
    var (s, plan, g) = Fight("ULDA_Finley_HP1", 131);
    var p1 = g.Player1; Mana(p1, 10); ClearBoards(g);
    UsePower(g);
    var amalgam = p1.BoardZone.SingleOrDefault(m => m.Card.Id == "ULDA_Finley_HP1t");
    Check("T7 New \"Recruits\": a 2/1 Amalgam Explorer, every minion type", amalgam != null && amalgam.AttackDamage == 2 && amalgam.Health == 1
        && new[] { Race.ELEMENTAL, Race.MECHANICAL, Race.DEMON, Race.MURLOC, Race.DRAGON, Race.BEAST, Race.PIRATE, Race.TOTEM }.All(x => amalgam.Card.IsRace(x)));
    while (!p1.BoardZone.IsFull) Board(g, p1, "CS2_182");
    NextTurn(g); Mana(p1, 10);
    Check("T7 New \"Recruits\" needs a free board slot", !p1.Options().OfType<HeroPowerTask>().Any());
}

// ---------------------------------------------------------------- T8 ULDA_Finley_HP2 Bubble Blower
{
    var (s, plan, g) = Fight("ULDA_Finley_HP2", 141);
    var p1 = g.Player1; Mana(p1, 10); Clear(p1);
    Check("T8 Bubble Blower costs (1)", p1.Hero.HeroPower.Cost == 1);
    UsePower(g);
    var choice = p1.Choice;
    var offers = choice?.Choices.Select(id => g.IdEntityDic[id].Card).ToList() ?? new List<Card>();
    var pool = DungeonRunContent.BubbleBlowerPool(g, p1, p1.Hero.HeroPower);
    Check("T8 Bubble Blower: Discover three distinct Shaman or Paladin minions with Battlecry, all scripted", offers.Count == 3 && offers.Select(c => c.Id).Distinct().Count() == 3
        && offers.All(c => c.Type == CardType.MINION && c[GameTag.BATTLECRY] != 0 && (c.Class == CardClass.SHAMAN || c.Class == CardClass.PALADIN) && c.Implemented && pool.Contains(c))
        && pool.Count >= 10, string.Join(",", offers.Select(c => c.Id)) + " pool " + pool.Count);
    Check("T8 Bubble Blower: Overload (1) is owed", p1.OverloadOwed == 1 && p1.OverloadThisGame >= 1);
    string picked = offers[1].Id;
    Pick(g, choice.Choices[1]);
    Check("T8 Bubble Blower: the chosen minion goes to the hand", p1.HandZone.Count == 1 && p1.HandZone[0].Card.Id == picked);
    EndTurn(g); EndTurn(g);
    Check("T8 Bubble Blower: next turn one Mana Crystal is locked", p1.OverloadLocked == 1);
}

// ---------------------------------------------------------------- T9 ULDA_Finley_HP3 Power Up!
{
    var (s, plan, g) = Fight("ULDA_Finley_HP3", 151);
    var p1 = g.Player1; Mana(p1, 10);
    var yeti = Board(g, p1, "CS2_182"); var boss = Board(g, g.Player2, "CS2_182");
    Check("T9 Power Up! targets minions of both sides, never a hero", CanTarget(g, yeti) && CanTarget(g, boss) && !CanTarget(g, p1.Hero) && !CanTarget(g, g.Player2.Hero));
    UsePower(g, yeti);
    Check("T9 Power Up!: Divine Shield and Windfury", yeti.HasDivineShield && yeti.HasWindfury);
}

// ---------------------------------------------------------------- T10 ULDA_Elise_HP1 Elise's Might (Choose One)
{
    var (s, plan, g) = Fight("ULDA_Elise_HP1", 161);
    var p1 = g.Player1; Mana(p1, 10);
    var options = p1.Options().OfType<HeroPowerTask>().ToList();
    var choices = p1.Hero.HeroPower.ChooseOnePlayables;
    Check("T10 the bot's option generator lists Elise's Might as two hero power options, one per choice (ChooseOne 1 and 2)",
        options.Count == 2 && options.Select(o => o.ChooseOne).OrderBy(x => x).SequenceEqual(new[] { 1, 2 }) && choices?.Length == 2
        && choices[0].Card.Id == "ULDA_Elise_HP1a" && choices[1].Card.Id == "ULDA_Elise_HP1b", string.Join(",", options.Select(o => o.ChooseOne)));
    var all1 = PowerOptionsBuilder.AllOptions(g, p1.Options());
    var hpOption = all1.PowerOptionList.Single(o => o.MainOption?.EntityId == p1.Hero.HeroPower.Id);
    Check("T10 the native option list gives the power two sub-options, on the two choices' entities (Defensive Posture, Offensive Posture)",
        hpOption.SubOptions.Count == 2 && hpOption.SubOptions[0].EntityId == choices[0].Id && hpOption.SubOptions[1].EntityId == choices[1].Id && hpOption.MainOption.Targets == null);
    int armor = p1.Hero.Armor;
    g.Process(options.Single(o => o.ChooseOne == 1));
    Check("T10 choice 1, Defensive Posture: +2 Armor, no Attack", p1.Hero.Armor == armor + 2 && p1.Hero.AttackDamage == 0 && p1.Hero.HeroPower.IsExhausted);
    NextTurn(g); Mana(p1, 10);
    armor = p1.Hero.Armor;
    g.Process(p1.Options().OfType<HeroPowerTask>().Single(o => o.ChooseOne == 2));
    Check("T10 choice 2, Offensive Posture: +2 Attack this turn (Dire Claws), no Armor", p1.Hero.AttackDamage == 2 && p1.Hero.Armor == armor && Has(p1.Hero, "AT_132_DRUIDe"));
    EndTurn(g);
    Check("T10 Offensive Posture's Attack is gone at the end of the turn", p1.Hero.AttackDamage == 0 && !Has(p1.Hero, "AT_132_DRUIDe"));
}

// ---------------------------------------------------------------- T11 ULDA_Elise_HP2 Druidic Teaching
{
    var (s, plan, g) = Fight("ULDA_Elise_HP2", 171);
    var p1 = g.Player1; Mana(p1, 10); Clear(p1);
    p1.Hero.Damage = 5;
    int deck = p1.DeckZone.Count;
    UsePower(g, p1.Hero);
    Check("T11 Druidic Teaching on a hero 5 below full: restores 2, no card (not at full Health)", p1.Hero.Damage == 3 && p1.HandZone.Count == 0 && p1.DeckZone.Count == deck);
    NextTurn(g); Mana(p1, 10); Clear(p1);
    var yeti = Board(g, p1, "CS2_182"); yeti.Damage = 1;
    deck = p1.DeckZone.Count;
    UsePower(g, yeti);
    Check("T11 Druidic Teaching on a minion 1 below full: restored to full, draws a card", yeti.Damage == 0 && p1.HandZone.Count == 1 && p1.DeckZone.Count == deck - 1);
}

// ---------------------------------------------------------------- T12 ULDA_Elise_HP3 Starseeker (passive)
{
    var (s, plan, g) = Fight("ULDA_Elise_HP3", 181);
    var p1 = g.Player1; var p2 = g.Player2; Mana(p1, 10); Clear(p1);
    Check("T12 Starseeker is a passive power: active from game start (the Elise Hero Power Watcher on its owner), never an option",
        p1.Hero.HeroPower.IsPassiveHeroPower && HasC(p1, "ULDA_Elise_HPe") && !p1.Options().OfType<HeroPowerTask>().Any());
    int Moons() => p1.HandZone.Count(c => c.Card.Id == "CS2_008");
    Play(g, ToHand(g, p1, "CS2_008"), p2.Hero);
    Check("T12 Starseeker: one spell adds nothing", Moons() == 0);
    Play(g, ToHand(g, p1, "CS2_008"), p2.Hero);
    Check("T12 Starseeker: the second spell of the turn adds a Moonfire", Moons() == 1);
    Play(g, ToHand(g, p1, "CS2_008"), p2.Hero);
    Check("T12 Starseeker: the third spell of the turn adds nothing more (two Moonfires cast: the generated one stays)", Moons() == 1);
    NextTurn(g); Mana(p1, 10); Clear(p1);
    Play(g, ToHand(g, p1, "CS2_008"), p2.Hero);
    Play(g, ToHand(g, p1, "CS2_008"), p2.Hero);
    Check("T12 Starseeker: the count starts again next turn: two spells add a Moonfire", Moons() == 1);
}

// ---------------------------------------------------------------- T13 ULDA_Brann_HP1 Spread Shot
{
    var (s, plan, g) = Fight("ULDA_Brann_HP1", 191);
    var p1 = g.Player1; var p2 = g.Player2; Mana(p1, 10);
    var yeti = Board(g, p2, "CS2_182");
    int hp = p2.Hero.Health;
    Check("T13 Spread Shot targets any character", CanTarget(g, yeti) && CanTarget(g, p2.Hero) && CanTarget(g, p1.Hero));
    UsePower(g, yeti);
    Check("T13 Spread Shot: 1 damage to the minion, then 1 to the enemy hero", yeti.Health == 4 && p2.Hero.Health == hp - 1, $"{yeti.Health} {hp}->{p2.Hero.Health}");
}

// ---------------------------------------------------------------- T14 ULDA_Brann_HP2 Well Equipped
{
    var (s, plan, g) = Fight("ULDA_Brann_HP2", 201);
    var p1 = g.Player1; Mana(p1, 10);
    UsePower(g);
    var weapon = p1.Hero.Weapon;
    var pool = DungeonRunContent.WellEquippedPool(g, p1, p1.Hero.HeroPower);
    Check("T14 Well Equipped: a random scripted weapon of the generation pool, equipped at Durability 1", weapon != null && weapon.Durability == 1 && weapon.Card.Implemented
        && pool.Contains(weapon.Card) && pool.Count >= 20, weapon?.Card.Id + " pool " + pool.Count);
    var (_, _, g2) = Fight("ULDA_Brann_HP2", 201);
    Mana(g2.Player1, 10); UsePower(g2);
    Check("T14 Well Equipped: the same seed equips the same weapon (game RNG)", g2.Player1.Hero.Weapon?.Card.Id == weapon?.Card.Id);
}

// ---------------------------------------------------------------- T15 ULDA_Brann_HP3 Dino Tracking (passive, the draw becomes a Discover)
{
    var (s, plan, g) = Fight("ULDA_Brann_HP3", 211);
    var p1 = g.Player1;
    Check("T15 Dino Tracking is a passive power on its owner (the Brann Hero Power Watcher), never an option",
        p1.Hero.HeroPower.IsPassiveHeroPower && HasC(p1, "ULDA_Brann_HPe"));
    var choice = p1.Choice;
    int hand = p1.HandZone.Count, deck = p1.DeckZone.Count;
    var deckIds = p1.DeckZone.Select(c => c.Card.Id).ToList();
    var offers = choice?.Choices.Select(id => g.IdEntityDic[id].Card.Id).ToList() ?? new List<string>();
    Check("T15 Dino Tracking: the first turn's draw is a Discover of three distinct cards of the owner's own deck (no normal draw yet)",
        choice != null && offers.Count == 3 && offers.Distinct().Count() == 3 && offers.All(deckIds.Contains), string.Join(",", offers));
    string picked = offers[2];
    Pick(g, choice.Choices[2]);
    Check("T15 Dino Tracking: the chosen card is drawn (hand +1, deck -1), the other two stay in the deck",
        p1.HandZone.Count == hand + 1 && p1.DeckZone.Count == deck - 1 && p1.HandZone.Last().Card.Id == picked
        && offers.Where((x, i) => i != 2).All(x => p1.DeckZone.Any(c => c.Card.Id == x)), $"hand {hand}->{p1.HandZone.Count} deck {deck}->{p1.DeckZone.Count}");
    EndTurn(g);
    Check("T15 Dino Tracking is its owner's alone: the boss's turn draws normally", g.Player2.Choice == null);
    int deck2 = p1.DeckZone.Count;
    EndTurn(g);
    Check("T15 Dino Tracking: the next turn starts with another Discover of the deck", p1.Choice != null && p1.DeckZone.Count == deck2);
}

// ---------------------------------------------------------------- T16 ULDA_039 Uldum Treasure Cache (Jr. and Sr. Tomb Diver's power)
{
    var (s, plan, g) = Fight("ULDA_Reno_HP1", 221, "MAGE_ROGUE");
    var p1 = g.Player1; Mana(p1, 10); Clear(p1);
    // Phase 6 F12: the Tombs actives join the explorers' pool (24 and Pack Mule), so the Cache discovers Tombs treasures.
    var poolAll = run.TreasurePool(s, "Tombs");
    Check("T16 the Reno run's Tombs treasure pool holds the 24 Tombs actives and Pack Mule (family F12), and the 3 F15 actives: Uldum Treasure Cache discovers from it",
        poolAll.Count == 28 && poolAll.Contains("ULDA_019"), poolAll.Count.ToString());
    new ReplaceHeroPower(C("ULDA_039")).Process(g, p1, p1.Hero, null);
    Check("T16 Uldum Treasure Cache (as Jr. Tomb Diver gives it): a clickable 2-Cost power", p1.Hero.HeroPower.Card.Id == "ULDA_039" && p1.Hero.HeroPower.Cost == 2
        && p1.Options().OfType<HeroPowerTask>().Count() == 1);
    UsePower(g);
    var offers = p1.Choice?.Choices.Select(id => g.IdEntityDic[id].Card.Id).ToList() ?? new List<string>();
    Check("T16 Uldum Treasure Cache: Discover three distinct treasures of the run's Tombs treasure pool", offers.Count == 3 && offers.Distinct().Count() == 3 && offers.All(poolAll.Contains),
        string.Join(",", offers));
    Pick(g, p1.Choice.Choices[0]);
    Check("T16 Uldum Treasure Cache: the chosen treasure goes to the hand", p1.HandZone.Count == 1 && p1.HandZone[0].Card.Id == offers[0]);
    EndTurn(g);
    var p2 = g.Player2; Mana(p2, 10);
    new ReplaceHeroPower(C("ULDA_039")).Process(g, p2, p2.Hero, null);
    bool bossUsed = g.Process(HeroPowerTask.Any(p2));
    Check("T16 a boss holding it discovers none on its turn (its generation rejects every run treasure)", bossUsed && p2.Choice == null && p2.Hero.HeroPower.IsExhausted, $"used {bossUsed} current {g.CurrentPlayer == p2} choice {string.Join(",", p2.Choice?.Choices.Select(id => g.IdEntityDic[id].Card.Id) ?? new string[0])} exhausted {p2.Hero.HeroPower.IsExhausted}");
    var plain = new Game(new GameConfig { StartPlayer = 1, Player1HeroClass = CardClass.MAGE, Player2HeroClass = CardClass.MAGE, Shuffle = false, FormatType = FormatType.FT_WILD });
    Check("T16 outside a run match the pool is empty", DungeonRunContent.TreasureCandidates(plain.Player1, null).Count == 0);
}

// ---------------------------------------------------------------- T17 picker data lists (the plank menu's Hero, Deck and Power steps)
{
    var fresh = new DungeonRunProfile();
    var hero = DungeonRunCatalog.Heroes(run, fresh, "MAGE").FirstOrDefault(x => x.Id == "MAGE_ROGUE");
    Check("T17 Heroes(MAGE) lists MAGE_ROGUE (Reno Jackson, Tombs, dual, portrait ULDA_Reno, 30 Health, 3 powers, 2 decks)",
        hero != null && hero.IsHero && hero.IsDual && hero.Adventure == "Tombs" && hero.HeroCardId == "ULDA_Reno" && hero.Health == 30 && hero.PowerCount == 3 && hero.DeckCount == 4);
    Check("T17 since F10 the other three rows are offered under their primary class", new[] { ("PALADIN", "PALADIN_SHAMAN"), ("DRUID", "DRUID_PRIEST"), ("HUNTER", "HUNTER_WARRIOR") }
        .All(p => DungeonRunCatalog.Heroes(run, fresh, p.Item1).Count(x => x.IsDual && x.Id == p.Item2) == 1));
    var ps = DungeonRunCatalog.Powers(run, fresh, "MAGE_ROGUE", id => transport.TryGetValue(id, out var sent) ? sent : id);
    Check("T17 Powers(MAGE_ROGUE): HP1 open; HP2 and HP3 locked; each named, costed and texted as the client prints it, sent as its own id",
        ps.Count == 3 && ps[0].Unlocked && !ps[1].Unlocked && !ps[2].Unlocked && ps.Select(o => o.Id).SequenceEqual(explorers[0].Powers)
        && ps.All(o => o.Name == FactName(o.Id) && o.Text == FactText(o.Id) && o.Cost == FactTag(o.Id, 48) && o.ClientCardId == o.Id)
        && ps[0].Targeted && !ps[1].Targeted && !ps[2].Targeted && ps.Skip(1).All(o => o.UnlockHint.Contains("Mage / Rogue") || o.UnlockHint.Contains("Reno")),
        string.Join(" | ", ps.Select(o => o.Name + ":" + o.UnlockHint)));
    var decks = DungeonRunCatalog.Decks(run, fresh, "MAGE_ROGUE");
    Check("T17 Decks(MAGE_ROGUE): Novice Spellslinger open, Spell Seeker, Secret Traps and Reno Random Deck locked (a generated deck: no listed cards)",
        decks.Count == 4 && decks[0].Unlocked && decks[0].IsDefault && decks.Skip(1).All(d => !d.Unlocked) && decks[3].Name == "Reno Random Deck" && decks[3].CardIds.Count == 0,
        string.Join(" | ", decks.Select(d => d.Name + ":" + d.UnlockHint)));
    foreach (var e in explorers.Where(x => !x.Enabled))
    {
        var fps = DungeonRunCatalog.Powers(fixtureRun, fresh, e.Row, id => id);
        Check($"T17 Powers({e.Row}) once enabled (fixture): {string.Join(", ", e.Powers)}, named, costed and texted as the client prints them",
            fps.Select(o => o.Id).SequenceEqual(e.Powers) && fps.All(o => o.Name == FactName(o.Id) && o.Text == FactText(o.Id) && o.Cost == FactTag(o.Id, 48)));
    }
}

// ---------------------------------------------------------------- T18 a pre-F9 MAGE_ROGUE save keeps its power
{
    // A run started before F9 (the Mage stand-in powers) holds a power the row no longer lists: only NewRun checks the row's
    // powers, so the saved run builds its plan and plays with the power it was started with.
    var pre = Load(File.ReadAllText(VerifyPaths.Fixture("p6f9/run-v1.before.json")));
    var preAll = new DungeonRunProfile(); preAll.Unlocked.AddRange(pre.Unlocks.Keys);
    foreach (string old in new[] { "CS2_034", "carto_survey", "mage_rimebite" })
    {
        var old1 = new DungeonRun(pre).NewRun(preAll, "MAGE_ROGUE", "tombs_reno_spellslinger", old, 4242);
        var ser = new System.Xml.Serialization.XmlSerializer(typeof(DungeonRunState));
        var w = new StringWriter(); ser.Serialize(w, old1);
        var back = (DungeonRunState)ser.Deserialize(new StringReader(w.ToString()));
        bool ok; string why = "";
        try
        {
            var plan = run.BuildPlan(back);
            var g = StartFight(plan, run.MatchSeed(back));
            ok = plan.PlayerHeroPowerId == old && g.Player1.Hero.HeroPower.Card.Id == DungeonRunSetup.PowerCardId(old) && g.Player1.Hero.Card.Id == "ULDA_Reno";
        }
        catch (Exception e) { ok = false; why = e.Message; }
        Check($"T18 a pre-F9 MAGE_ROGUE save on {old} loads, builds its plan and starts its fight with {old} on the current data", ok, why);
    }
}

// ---------------------------------------------------------------- T22 full games against the real boss AI, the player using the power
var testBudget = new Budget(6000, 2000, 300, 6, 3, 100, 8, turnSearchSeconds: 6, turnWallSeconds: 8);
string[] enemyFirst = { "ULDA_Reno_HP1", "ULDA_Brann_HP1" };
string AiGame(string power, string deckId, long seed, bool cache = false)
{
    string row = RowOf(power);
    var (d, r, a) = For(row);
    var s = NewAct(row, seed, power, deckId);
    for (int i = 0; i < 2; i++) s.Matches.Add(new DungeonRunMatchSummary { Floor = 1, Boss = s.BossSequence[0], Outcome = DungeonRunOutcome.Win });
    var boss = r.CurrentBoss(s);
    var plan = r.BuildPlan(s);
    var pilot = DungeonRunBossPilot.Build(boss, plan, d, runClasses: r.RunClasses(s));
    var profile = pilot.Profile.WithBudget(testBudget);
    var golden = new GoldenPilot(profile, pilot.Plan, null, pilot.SecretPrior);
    var cfg = new GameConfig { RandomSeed = r.MatchSeed(s), Shuffle = true, SkipMulligan = false, History = false, Logging = false, FormatType = FormatType.FT_WILD };
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
    if (cache) new ReplaceHeroPower(C("ULDA_039")).Process(game, game.Player1, game.Player1.Hero, null);
    // Starseeker needs two spells in a turn: the player starts with two Moonfires and two Innervates (all 0-Cost) in hand.
    if (power == "ULDA_Elise_HP3")
        foreach (string id in new[] { "CS2_008", "CS2_008", "EX1_169", "EX1_169" })
            Generic.AddHandPhase.Invoke(game.Player1, Entity.FromCard(game.Player1, C(id)));
    int decisionIndex = 0, actions = 0, decisions = 0, fallbacks = 0, powerUses = 0, playerChoices = 0;
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
            var mine = options.OfType<HeroPowerTask>()
                .OrderByDescending(o => o.Target == null ? 0 : (o.Target.Controller != game.Player1) == enemyFirst.Contains(power) ? 1 : 0).FirstOrDefault();
            if (mine != null) { powerUses++; Act(mine, "player"); }
            else Act(LocalBot.Choose(options), "player");
        }
    }
    string result = game.State != State.COMPLETE ? "turn-cap" : game.Player2.PlayState == PlayState.WON ? "boss" : game.Player1.PlayState == PlayState.WON ? "player" : "tie";
    int moonfires = game.Player1.HandZone.Concat(game.Player1.GraveyardZone).Concat(game.Player1.SetasideZone)
        .Count(e => e.Card.Id == "CS2_008" && e[GameTag.DISPLAYED_CREATOR] != 0);
    return $"{result} turns {game.Turn} power-uses {powerUses} player-choices {playerChoices} moonfires {moonfires} decisions {decisions} fallbacks {fallbacks} boss {boss.Id}";
}
foreach (string power in powers.Concat(new[] { "ULDA_039" }))
{
    bool cache = power == "ULDA_039";
    string row = cache ? "MAGE_ROGUE" : RowOf(power);
    var cls = For(row).D.Class(row);
    // HP1 and ULDA_039 play on the first deck, HP2 and HP3 on the Random deck, so every Random deck plays a full game.
    string deckId = power.EndsWith("HP1") || cache ? cls.DefaultDeck : cls.Decks.Last();
    string line;
    try { line = AiGame(cache ? "ULDA_Reno_HP1" : power, deckId, 281, cache); }
    catch (Exception e) { line = "FAULT " + e.GetType().Name + ": " + e.Message.Split('\n')[0]; }
    Console.WriteLine($"T22 {power,-17} {deckId,-24} {line}");
    bool used = power == "ULDA_Elise_HP3" ? !line.Contains("moonfires 0 ") : power == "ULDA_Brann_HP3" ? !line.Contains("player-choices 0 ") : !line.Contains("power-uses 0 ");
    Check($"T22 {power} on {deckId}: the player uses it in a full game against the boss AI, which ends without a fault or an AI fallback",
        !line.StartsWith("FAULT") && line.Contains("fallbacks 0 ") && used && (!cache || !line.Contains("player-choices 0 ")), line);
}

Console.WriteLine($"{passes} passed, {failures} failed");
return failures == 0 ? 0 : 1;
