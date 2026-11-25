// c-heroes-F6: phase 6 family F6 of the unified Dungeon Run, the Dalaran Heist villains: their nine portraits (DALA_Rakanishu,
// Barkeye, Vessina, Kriziki, Eudora, Chu, Squeamlish, Tekahn, George) as hero rows of run-v1.json (heroRows, kind hero, Heist,
// 30 Health, sitting beside the eleven class rows), each with the class basic power and its two special powers
// (DALA_<Class>_HP1/HP2, unlocked at floor 4 and a win), the authentic starters DALA_<Class>_Deck1-3 (Hunter Deck3 waits for
// DAL_377, family F10) and the Random deck (DALA_800-808: a generated starter drawn at NewRun from the shared draft catalog,
// the display card never a match card). For every id: the engine content (scripted, keyed and shelled by its own client id,
// name and text as facts/violet-hold-2026-09.json has them), the run data, the validator and admission checks, a scripted
// regression game through the run setup path (DungeonRun.NewRun on the hero row -> BuildPlan -> DungeonRunSetup.Configure
// -> Game -> ApplyAfterStart -> mulligan -> MainBegin -> the power used), and a full game against the real boss AI
// (DungeonRunBossPilot + GoldenPilot) in which the player uses the power; it must end without a fault or an AI fallback.
// Headless: compiles C:\SoloStone\plugin\DungeonRun\*.cs and the NPC AI against p6f6/engine-out. Run it twice and compare.
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
int Count(IPlayable p, string ench) => p.AppliedEnchantments?.Count(e => e.Card.Id == ench) ?? 0;
var clientIds = new HashSet<string>(System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "dungeon-run/reference/client-tables.json")))
    .RootElement.GetProperty("tables").GetProperty("CARD").EnumerateArray().Select(r => r.GetProperty("id").GetString()), StringComparer.Ordinal);
// The client's Heist starters (adventures.json, linked-list order), by note name.
var clientDecks = new Dictionary<string, List<string>>(StringComparer.Ordinal);
void WalkDecks(System.Text.Json.JsonElement e)
{
    if (e.ValueKind == System.Text.Json.JsonValueKind.Object)
    {
        if (e.TryGetProperty("noteName", out var nn) && nn.ValueKind == System.Text.Json.JsonValueKind.String && nn.GetString().StartsWith("DALA_") && e.TryGetProperty("cards", out var cs))
            clientDecks[nn.GetString()] = cs.EnumerateArray().Select(x => x.GetString()).ToList();
        foreach (var p in e.EnumerateObject()) WalkDecks(p.Value);
    }
    else if (e.ValueKind == System.Text.Json.JsonValueKind.Array) foreach (var x in e.EnumerateArray()) WalkDecks(x);
}
WalkDecks(System.Text.Json.JsonDocument.Parse(R("dungeon-run/reference/adventures.json")).RootElement);

// class, hero row, basic power, display card of the Random deck
var heroes = new (string Cls, string Row, string Basic, string Display)[]
{
    ("Mage", "DALA_Rakanishu", "CS2_034", "DALA_800"), ("Hunter", "DALA_Barkeye", "DS1h_292", "DALA_802"), ("Shaman", "DALA_Vessina", "CS2_049", "DALA_806"),
    ("Priest", "DALA_Kriziki", "CS1h_001", "DALA_804"), ("Rogue", "DALA_Eudora", "CS2_083b", "DALA_805"), ("Warrior", "DALA_Chu", "CS2_102", "DALA_808"),
    ("Druid", "DALA_Squeamlish", "CS2_017", "DALA_801"), ("Warlock", "DALA_Tekahn", "CS2_056", "DALA_807"), ("Paladin", "DALA_George", "CS2_101", "DALA_803"),
};
string RowOf(string power) => heroes.First(h => power.StartsWith("DALA_" + h.Cls + "_")).Row;
string[] powers = heroes.SelectMany(h => new[] { $"DALA_{h.Cls}_HP1", $"DALA_{h.Cls}_HP2" }).ToArray();
string[] displays = heroes.Select(h => h.Display).OrderBy(x => x, StringComparer.Ordinal).ToArray();
string[] tokens = { "DALA_Hunter_HP2t", "DALA_Warrior_HP2t", "DALA_Warlock_HP2t" };
var effectShells = new Dictionary<string, string>
{
    ["fx_DALA_Mage_HP1e"] = "DALA_Mage_HP1e", ["fx_DALA_Hunter_HP1e"] = "DALA_Hunter_HP1e", ["fx_DALA_Hunter_HP2e"] = "DALA_Hunter_HP2e",
    ["fx_DALA_Priest_HP1e"] = "DALA_Priest_HP1e", ["fx_DALA_Priest_HP2e"] = "DALA_Priest_HP2e", ["fx_DALA_Warrior_HP1e"] = "DALA_Warrior_HP1e",
    ["fx_DALA_Druid_HP2e"] = "DALA_Druid_HP2e",
};
CardType FactType(string id) => FactTag(id, 202) switch { 3 => CardType.HERO, 4 => CardType.MINION, 5 => CardType.SPELL, 6 => CardType.ENCHANTMENT, 7 => CardType.WEAPON, 10 => CardType.HERO_POWER, _ => CardType.INVALID };
string ClassOfTag(int v) => v switch { 2 => "DRUID", 3 => "HUNTER", 4 => "MAGE", 5 => "PALADIN", 6 => "PRIEST", 7 => "ROGUE", 8 => "SHAMAN", 9 => "WARLOCK", 10 => "WARRIOR", 12 => "NEUTRAL", _ => "?" + v };

// ---------------------------------------------------------------- T1 content
Check("T1 36 F6 ids: 9 portraits, 18 powers, 9 Random-deck cards", heroes.Length == 9 && powers.Length == 18 && displays.Length == 9
    && heroes.Select(h => h.Row).Concat(powers).Concat(displays).Distinct().Count() == 36);
Check("T1 DungeonRunContent.HeistPowers lists the 18 powers in class order", DungeonRunContent.HeistPowers.SequenceEqual(powers.OrderBy(p => Array.IndexOf(DungeonRunContent.HeistPowers.ToArray(), p)))
    && DungeonRunContent.HeistPowers.OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(powers.OrderBy(x => x, StringComparer.Ordinal)));
foreach (var h in heroes)
{
    var content = DungeonRunContent.Get(h.Row);
    var card = C(h.Row);
    Check($"T1 {h.Row}: a Hero keyed and shelled by its own id, scripted, the client's {h.Cls.ToUpperInvariant()} hero at 30 Health",
        content.Kind == RunContentKind.Hero && content.ShellId == h.Row && card.Implemented && card.Type == CardType.HERO && card.Class.ToString() == h.Cls.ToUpperInvariant()
        && ClassOfTag(FactTag(h.Row, 199)) == h.Cls.ToUpperInvariant() && card.Health == 30 && content.Health == 30 && clientIds.Contains(h.Row) && DungeonRunContent.ForShell(h.Row) == content);
    Check($"T1 ids-text-match {h.Row}: name and text equal the client's", content.Name == FactName(h.Row) && content.Text == FactText(h.Row), content.Name + " | " + content.Text);
}
foreach (string id in powers.Concat(tokens))
{
    var content = DungeonRunContent.Get(id);
    var card = C(id);
    bool power = id.Contains("_HP") && !id.EndsWith("t");
    var want = power ? RunContentKind.HeroPower : RunContentKind.ActiveTreasure;
    Check($"T1 {id}: a {want} keyed and shelled by its own id, scripted, the client's {FactType(id)}",
        content.Kind == want && content.ShellId == id && card.Implemented && card.Type == FactType(id) && DungeonRunContent.ForShell(id) == content && clientIds.Contains(id),
        content.Kind + " " + card.Type + " " + card.Implemented);
    Check($"T1 ids-text-match {id}: name and text equal facts/violet-hold-2026-09.json", content.Name == FactName(id) && content.Text == FactText(id), content.Name + " | " + content.Text);
    Check($"T1 {id}: cost {FactTag(id, 48)}" + (card.Type == CardType.MINION ? $", stats {FactTag(id, 47)}/{FactTag(id, 45)}" : "") + " as the client prints it",
        card.Cost == FactTag(id, 48) && content.Cost == FactTag(id, 48) && (card.Type != CardType.MINION || card.ATK == FactTag(id, 47) && card.Health == FactTag(id, 45)),
        $"{card.Cost} {card.ATK}/{card.Health}");
    if (power) Check($"T1 {id}: the {ClassOfTag(FactTag(id, 199))} class's power, as the client lists it", card.Class.ToString() == ClassOfTag(FactTag(id, 199)) && id.Contains("_" + card.Class.ToString().Substring(0, 1) + card.Class.ToString().Substring(1).ToLowerInvariant() + "_"));
}
foreach (var fx in effectShells)
{
    var e = DungeonRunContent.Get(fx.Key);
    Check($"T1 {fx.Value}: content {fx.Key} on the client's own ENCHANTMENT, scripted, name and text word for word",
        e.Kind == RunContentKind.Effect && e.ShellId == fx.Value && C(fx.Value).Implemented && C(fx.Value).Type == CardType.ENCHANTMENT && clientIds.Contains(fx.Value)
        && e.Name == FactName(fx.Value) && e.Text == FactText(fx.Value), e.Name + " | " + e.Text);
}
Check("T1 tokens: The Pact lists the Devious Imp it summons; the powers that summon nothing (Pet Training and Backup add cards to hand) list none",
    DungeonRunContent.Get("DALA_Warlock_HP2").Tokens.SequenceEqual(new[] { "DALA_Warlock_HP2t" })
    && powers.Except(new[] { "DALA_Warlock_HP2" }).All(p => DungeonRunContent.Get(p).Tokens.Count == 0));
Check("T1 the tokens to verify are scripted: Shifting Chameleon, Explosive (Casts When Drawn), Devious Imp, Silver Hand Recruit",
    tokens.Concat(new[] { "CS2_101t" }).All(id => C(id).Implemented) && C("DALA_Warrior_HP2t").Power?.TopdeckTask != null && C("DALA_Hunter_HP2t").Power?.Trigger != null
    && C("DALA_Warlock_HP2t").IsRace(Race.DEMON));
foreach (string id in displays)
    Check($"T1 {id} ({FactName(id)}): a run-layer loadout card, no engine content entry, the display card of exactly one generated deck of its class",
        DungeonRunContent.ForShell(id) == null && !DungeonRunContent.TryGet(id, out _) && clientIds.Contains(id)
        // Build Deck: the build decks show DALA_800 too (the default display card), so only the generated decks are counted.
        && data.Decks.Count(d => d.DisplayCard == id && !d.Build) == 1 && data.Decks.Single(d => d.DisplayCard == id && !d.Build) is var dd && dd.IsGenerated
        && data.FindClass(dd.Class).ClassSet[0] == ClassOfTag(FactTag(id, 199)) && FactName(id) == dd.Name);

// ---------------------------------------------------------------- T2 data, validator, admission
var transport = new Dictionary<string, string>
{
    ["CS2_034"] = "HERO_08bp", ["CS2_102"] = "HERO_01bp", ["CS1h_001"] = "HERO_09bp", ["CS2_101"] = "HERO_04bp", ["CS2_017"] = "HERO_06bp",
    ["DS1h_292"] = "HERO_05bp", ["CS2_083b"] = "HERO_03bp", ["CS2_056"] = "HERO_07bp", ["CS2_049"] = "HERO_02bp",
    ["AT_132_WARRIOR"] = "HERO_01bp2", ["AT_132_SHAMAN"] = "HERO_02bp2", ["AT_132_ROGUE"] = "HERO_03bp2", ["AT_132_PALADIN"] = "HERO_04bp2",
    ["AT_132_HUNTER"] = "HERO_05bp2", ["AT_132_DRUID"] = "HERO_06bp2", ["AT_132_WARLOCK"] = "HERO_07bp2", ["AT_132_MAGE"] = "HERO_08bp2", ["AT_132_PRIEST"] = "HERO_09bp2",
};
bool ClientKnows(string id) => clientIds.Contains(transport.TryGetValue(id, out var sent) ? sent : id);
var policy = OfflinePoolPolicy.Load();
{
    var errors = DungeonRunValidator.Validate(data, ClientKnows);
    Check("T2 shipped data validates with 0 errors (client check through the transported id)", errors.Count == 0, string.Join(" | ", errors.Take(5)));
    var legacy = DungeonRunData.Parse(runJson, poolJson, npcJson);
    var le = DungeonRunValidator.Validate(legacy, null);
    Check("T2 the data without the catalog validates with 0 errors and loads no Heist row", le.Count == 0 && heroes.All(h => legacy.FindClass(h.Row) == null), string.Join(" | ", le.Take(3)));
    Check("T2 the Heist rows sit beside the class rows: the eleven class rows are unchanged, the nine Heist rows are hero rows after them",
        data.ClassRows.Select(c => c.Id).SequenceEqual(new[] { "DRUID", "HUNTER", "MAGE", "PALADIN", "PRIEST", "ROGUE", "SHAMAN", "WARLOCK", "WARRIOR", "DEATHKNIGHT", "DEMONHUNTER" })
        && data.HeroRows.Where(h => h.Adventure == "Heist").Select(h => h.Id).SequenceEqual(heroes.Select(h => h.Row)));
    foreach (var h in heroes)
    {
        var r = data.FindClass(h.Row);
        string low = h.Row.ToLowerInvariant(), cls = h.Cls.ToUpperInvariant();
        Check($"T2 hero row {h.Row}: kind hero, Heist, its own portrait, 30 Health, classes [{cls}], enabled, no signatures",
            r != null && r.IsHero && r.Adventure == "Heist" && r.HeroCard == h.Row && r.Health == 30 && r.ClassSet.SequenceEqual(new[] { cls }) && r.Enabled && r.Signatures.Count == 0
            && data.ClassRows.All(c => c.Id != h.Row) && run.AvailableHeroes(cls, all).Any(x => x.Id == h.Row));
        Check($"T2 hero row {h.Row}: powers [{h.Basic} basic, DALA_{h.Cls}_HP1 at floor 4, DALA_{h.Cls}_HP2 at a win]",
            r.Powers.SequenceEqual(new[] { h.Basic, $"DALA_{h.Cls}_HP1", $"DALA_{h.Cls}_HP2" }) && r.PowerUnlock(h.Basic) == null
            && r.PowerUnlock($"DALA_{h.Cls}_HP1") == low + "_floor4" && r.PowerUnlock($"DALA_{h.Cls}_HP2") == low + "_win"
            && data.Unlocks.ContainsKey(low + "_floor4") && data.Unlocks[low + "_floor4"].Class == h.Row && data.Unlocks.ContainsKey(low + "_win"));
        // Phase 6 F10 ships DALA_Hunter_Deck3 too (DAL_377 scripted): every villain has Deck1-3.
        int n = 3;
        var want = Enumerable.Range(1, n).Select(i => $"heist_{h.Cls.ToLowerInvariant()}_{i}").Concat(new[] { $"heist_{h.Cls.ToLowerInvariant()}_random" }).ToList();
        Check($"T2 hero row {h.Row}: decks {string.Join(", ", want)}", r.Decks.SequenceEqual(want), string.Join(",", r.Decks));
        for (int i = 1; i <= n; i++)
        {
            var d = data.FindDeck($"heist_{h.Cls.ToLowerInvariant()}_{i}");
            var client = clientDecks[$"DALA_{h.Cls}_Deck{i}"];
            bool same = d.Cards.Count == 10 && client.Count == 10 && d.Cards.Zip(client, (a, b) => a == b || C(a).Name == FactName(b)).All(x => x);
            Check($"T2 {d.Id}: DALA_{h.Cls}_Deck{i} '{d.Name}' card for card (a reprint plays where the original is unscripted), {(i == 1 ? "no unlock" : i == 2 ? "floor 3" : "floor 5")}",
                same && d.Class == h.Row && !d.IsGenerated && d.Unlock == (i == 1 ? null : low + (i == 2 ? "_floor3" : "_floor5")),
                string.Join(",", d.Cards.Zip(client, (a, b) => a == b ? a : a + "<-" + b)));
            Check($"T2 {d.Id}: every card scripted and admitted for a {cls} catalog run",
                d.Cards.All(id => DungeonRunValidator.PoolCardReason(id, new[] { cls }, false, policy, catalogRun: true) == null));
        }
        var rd = data.FindDeck($"heist_{h.Cls.ToLowerInvariant()}_random");
        Check($"T2 {rd.Id}: the Random deck, random-class, display card {h.Display}, no cards, unlocked by a win; its pool holds at least 10 {cls} cards",
            rd.IsGenerated && rd.Generator == DungeonRunStarterDeck.GeneratorRandomClass && rd.DisplayCard == h.Display && rd.Cards.Count == 0 && rd.Unlock == low + "_win"
            && clientDecks[$"DALA_{h.Cls}_Random"].SequenceEqual(new[] { h.Display }) && DungeonRunValidator.GeneratorPool(data, r, cls).Count >= 10,
            DungeonRunValidator.GeneratorPool(data, r, cls).Count.ToString());
    }
    Check("T2 DALA_Hunter_Deck3 (Tech and Death) ships since family F10 scripted DAL_377 Nine Lives: heist_hunter_3, unlocked at floor 5, its client cards",
        data.FindDeck("heist_hunter_3") is DungeonRunStarterDeck h3 && h3.Cards.OrderBy(x => x).SequenceEqual(clientDecks["DALA_Hunter_Deck3"].OrderBy(x => x))
        && h3.Unlock == "dala_barkeye_floor5" && C("DAL_377").Implemented
        && data.FindClass("DALA_Barkeye").Decks.SequenceEqual(new[] { "heist_hunter_1", "heist_hunter_2", "heist_hunter_3", "heist_hunter_random" }));

    // Validator branches
    string Swap(string json, string from, string to) { if (!json.Contains(from)) throw new Exception("fixture text not found: " + from); return json.Replace(from, to); }
    var e1 = DungeonRunValidator.Validate(Load(Swap(runJson, "\"decks\": [\"heist_mage_1\", \"heist_mage_2\", \"heist_mage_3\", \"heist_mage_random\"]",
        "\"decks\": [\"heist_mage_1\", \"heist_mage_2\", \"heist_mage_3\", \"heist_mage_random\", \"heist_hunter_random\"]")), null);
    Check("T2 validator: a hero row with two generated decks is refused", e1.Any(e => e.Contains("class DALA_Rakanishu") && e.Contains("2 generated decks")), string.Join(" | ", e1.Take(3)));
    var e2 = DungeonRunValidator.Validate(Load(Swap(runJson, "\"decks\": [\"heist_mage_1\", \"heist_mage_2\", \"heist_mage_3\", \"heist_mage_random\"]",
        "\"decks\": [\"heist_mage_1\", \"heist_mage_2\", \"heist_mage_3\", \"heist_hunter_1\", \"heist_mage_random\"]")), null);
    Check("T2 validator: four fixed decks are refused (1 to 3, plus one generated)", e2.Any(e => e.Contains("class DALA_Rakanishu") && e.Contains("has 4 fixed decks")), string.Join(" | ", e2.Take(3)));
    var e3 = DungeonRunValidator.Validate(Load(Swap(runJson, "\"cards\": [\"KAR_036\", \"DAL_608\", \"NEW1_023\"", "\"cards\": [\"KAR_036\", \"DAL_377\", \"NEW1_023\"")), null);
    Check("T2 validator: an unscripted starter card (DAL_377) in a Heist deck is refused", e3.Any(e => e.StartsWith("DAL_377") && e.Contains("heist_mage_1")), string.Join(" | ", e3.Take(3)));
    Check("T2 validator: Hydrologist (UNG_011, secret-generated) is admitted in a hero row's starter (catalog run) but not in a class row's (legacy rules)",
        DungeonRunValidator.PoolCardReason("UNG_011", new[] { "PALADIN" }, false, policy, catalogRun: true) == null
        && DungeonRunValidator.PoolCardReason("UNG_011", new[] { "PALADIN" }, false, policy) != null);
}

// ---------------------------------------------------------------- T2 admission
foreach (string id in powers)
{
    var p = DungeonRunAdmission.Classify(C(id), id, AdmitScope.Player, null, catalogRun: true);
    var b = DungeonRunAdmission.Classify(C(id), id, AdmitScope.Boss, null, catalogRun: true);
    Check($"T2 admission {id}: the run-hero-power row admits the player, never the boss; the boss's generation never yields it",
        p.Subject && p.Admitted && !b.Admitted && DungeonRunAdmission.RuleFor(id)?.Family == "run-hero-power"
        && DungeonRunAdmission.Classify(C(id), id, AdmitScope.Player).Admitted && DungeonRunAdmission.GeneratedRejectsBoss(true).Contains(id), p.Family + " " + p.Admit);
}
foreach (string id in new[] { "DALA_Hunter_HP2t", "DALA_Warlock_HP2t" })
    Check($"T2 admission {id}: a Heist power's card for the player only (run-hero-power-token)",
        DungeonRunAdmission.RuleFor(id)?.Family == "run-hero-power-token" && DungeonRunAdmission.Classify(C(id), id, AdmitScope.Player, null, true).Admitted
        && !DungeonRunAdmission.Classify(C(id), id, AdmitScope.Boss, null, true).Admitted);
Check("T2 admission DALA_Warrior_HP2t: Undermine's Explosive is admitted on both sides (it is shuffled into the boss's deck), never generated",
    DungeonRunAdmission.RuleFor("DALA_Warrior_HP2t")?.Family == "run-hero-power-deck-token" && DungeonRunAdmission.Classify(C("DALA_Warrior_HP2t"), "DALA_Warrior_HP2t", AdmitScope.Both, null, true).Admitted
    && !DungeonRunAdmission.Classify(C("DALA_Warrior_HP2t"), "DALA_Warrior_HP2t", AdmitScope.Generated, null, true).Admitted);
foreach (var h in heroes)
    Check($"T2 admission {h.Row}: the portrait needs no row (no rules text) and is admitted for the player", DungeonRunAdmission.Classify(C(h.Row), h.Row, AdmitScope.Player, null, true).Admitted);

// ---------------------------------------------------------------- T2 additive rows: the class rows' runs are unchanged (legacy ScheduleVersion pinning)
{
    string beforeJson = File.ReadAllText(VerifyPaths.Fixture("p6f6/run-v1.before.json"));
    var before = Load(beforeJson);
    var oldRun = new DungeonRun(before);
    var oldAll = new DungeonRunProfile(); oldAll.Unlocked.AddRange(before.Unlocks.Keys);
    Check("T2 the pre-F6 data has no Heist row; the new data adds exactly the nine (and their 18 unlock keys)",
        // Phase 6 F10 enables the three Tombs dual rows: their ladder keys are new too, and not the Heist's.
        heroes.All(h => before.FindClass(h.Row) == null) && data.Unlocks.Keys.Except(before.Unlocks.Keys)
            .Where(k => !k.StartsWith("paladin_shaman_") && !k.StartsWith("druid_priest_") && !k.StartsWith("hunter_warrior_") && !k.StartsWith("mage_rogue_")
                && !k.StartsWith("trla_209h_")).OrderBy(x => x, StringComparer.Ordinal) // phase 6 F14: Rikkar's forms' ladder keys are new too
            .SequenceEqual(heroes.SelectMany(h => new[] { "floor3", "floor4", "floor5", "win" }.Select(s => h.Row.ToLowerInvariant() + "_" + s)).OrderBy(x => x, StringComparer.Ordinal)));
    var ser = new System.Xml.Serialization.XmlSerializer(typeof(DungeonRunState));
    string Xml(DungeonRunState s) { var w = new StringWriter(); ser.Serialize(w, s); return w.ToString(); }
    int same = 0, compared = 0;
    var diffs = new List<string>();
    foreach (var cls in before.ClassRows)
        foreach (string schedule in new[] { null, DungeonRunSchedules.LegacyId })
            for (long seed = 1; seed <= 6; seed++)
            {
                compared++;
                var a = oldRun.NewRun(oldAll, cls.Id, cls.DefaultDeck, cls.DefaultPower, seed, schedule: schedule, signatureId: oldRun.SignatureOffer(oldAll, oldRun.Data.FindClass(cls.Id), seed).FirstOrDefault());
                var b = run.NewRun(all, cls.Id, cls.DefaultDeck, cls.DefaultPower, seed, schedule: schedule, signatureId: run.SignatureOffer(all, run.Data.FindClass(cls.Id), seed).FirstOrDefault());
                oldRun.OpenTreasureDraft(a); run.OpenTreasureDraft(b);
                if (Xml(a) == Xml(b)) same++; else if (diffs.Count < 3) diffs.Add(cls.Id + "/" + (schedule ?? "acts") + "/" + seed);
            }
    Check($"T2 every class row's new run (act and legacy schedules) and first treasure draft are byte for byte the pre-F6 ones ({compared} runs)", same == compared, string.Join(",", diffs));
}

// ---------------------------------------------------------------- the run setup path
GameConfig Configured(RunMatchPlan plan, int seed)
{
    var cfg = new GameConfig { RandomSeed = seed, Shuffle = true, SkipMulligan = false, History = true, Logging = false, FormatType = FormatType.FT_WILD };
    DungeonRunSetup.Configure(cfg, plan);
    return cfg;
}
DungeonRunState NewAct(string row, long seed, string power = null, string deck = null)
{
    var cls = data.Class(row);
    var s = run.NewRun(all, cls.Id, deck ?? cls.DefaultDeck, power ?? cls.DefaultPower, seed, signatureId: run.SignatureOffer(all, run.Data.FindClass(cls.Id), seed).FirstOrDefault());
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
(DungeonRunState s, RunMatchPlan plan, Game g) Fight(string power, long seed)
{
    var s = NewAct(RowOf(power), seed, power);
    var plan = run.BuildPlan(s);
    var g = StartFight(plan, run.MatchSeed(s));
    if (g.Player1.Hero.HeroPower.Card.Id != power) throw new Exception("the match did not start with " + power);
    return (s, plan, g);
}
void Mana(Controller c, int n) { c.BaseMana = n; c.UsedMana = 0; c.OverloadLocked = 0; }
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
void EndTurn(Game g) => g.Process(EndTurnTask.Any(g.CurrentPlayer));
void Clear(Controller c) { foreach (var e in c.HandZone.ToArray()) { c.HandZone.Remove(e); c.SetasideZone.Add(e); } }
void ClearBoards(Game g) { foreach (var c in new[] { g.Player1, g.Player2 }) foreach (var m in c.BoardZone.ToArray()) { c.BoardZone.Remove(m); c.SetasideZone.Add(m); } }

// ---------------------------------------------------------------- T3 NewRun / BuildPlan on every Heist row and deck
foreach (var h in heroes)
{
    var cls = data.Class(h.Row);
    string clsName = h.Cls.ToUpperInvariant();
    foreach (string deckId in cls.Decks)
    {
        var deck = data.FindDeck(deckId);
        foreach (string power in cls.Powers)
        {
            var s = run.NewRun(all, h.Row, deckId, power, 5, signatureId: run.SignatureOffer(all, run.Data.FindClass(h.Row), 5).FirstOrDefault());
            var plan = run.BuildPlan(s);
            bool deckOk = deck.IsGenerated
                ? s.Deck.Count == 10 && s.Deck.Distinct().Count() == 10 && s.Deck.All(DungeonRunValidator.GeneratorPool(data, cls, clsName).Contains) && !s.Deck.Contains(h.Display)
                    && s.Deck.All(id => C(id).Class.ToString() == clsName)
                : s.Deck.SequenceEqual(deck.Cards);
            if (power != cls.Powers[0] && deck.Id != cls.DefaultDeck && !deck.IsGenerated) { Check($"T3 NewRun {h.Row} {deckId} {power}", deckOk && s.Power == power); continue; }
            Check($"T3 NewRun {h.Row} on {deckId} with {power}: an act catalog run of the hero row, its starter in the run deck (origin starter), the power",
                s.Class == h.Row && s.IsActRun && s.IsCatalogRun && s.StarterDeck == deckId && s.Power == power && deckOk && s.Cards.All(c => c.Origin == DungeonRunDeck.OriginStarter)
                && s.Loadout?.Id == h.Row && s.Loadout.Signature.Count == 0, string.Join(",", s.Deck));
            Check($"T3 BuildPlan {h.Row} {deckId} {power}: the portrait at 30 Health, the power, the run deck",
                plan.PlayerHero.Id == h.Row && plan.PlayerHeroHealth == 30 && plan.PlayerHeroPowerId == power && plan.PlayerGeneratedSecrets
                && plan.PlayerDeck.Select(c => c.Id).OrderBy(x => x).SequenceEqual(s.Deck.OrderBy(x => x)));
        }
        if (deck.IsGenerated)
        {
            var a = run.NewRun(all, h.Row, deckId, h.Basic, 77, signatureId: run.SignatureOffer(all, run.Data.FindClass(h.Row), 77).FirstOrDefault());
            var b = run.NewRun(all, h.Row, deckId, h.Basic, 77, signatureId: run.SignatureOffer(all, run.Data.FindClass(h.Row), 77).FirstOrDefault());
            var c2 = run.NewRun(all, h.Row, deckId, h.Basic, 78, signatureId: run.SignatureOffer(all, run.Data.FindClass(h.Row), 78).FirstOrDefault());
            Check($"T3 {deckId}: the Random deck is repeatable by seed and changes with it", a.Deck.SequenceEqual(b.Deck) && !a.Deck.SequenceEqual(c2.Deck),
                string.Join(",", a.Deck) + " / " + string.Join(",", c2.Deck));
        }
    }
    var fresh = new DungeonRunProfile();
    bool Refused(Func<DungeonRunState> f) { try { f(); return false; } catch (InvalidOperationException) { return true; } }
    Check($"T3 {h.Row}: a new profile starts with the basic power on the first deck only; HP1, HP2, the second, third and Random decks are locked",
        run.NewRun(fresh, h.Row, cls.DefaultDeck, h.Basic, 3, signatureId: run.SignatureOffer(fresh, run.Data.FindClass(h.Row), 3).FirstOrDefault()).Power == h.Basic
        && Refused(() => run.NewRun(fresh, h.Row, cls.DefaultDeck, $"DALA_{h.Cls}_HP1", 3, signatureId: run.SignatureOffer(fresh, run.Data.FindClass(h.Row), 3).FirstOrDefault())) && Refused(() => run.NewRun(fresh, h.Row, cls.DefaultDeck, $"DALA_{h.Cls}_HP2", 3, signatureId: run.SignatureOffer(fresh, run.Data.FindClass(h.Row), 3).FirstOrDefault()))
        && cls.Decks.Skip(1).All(d => Refused(() => run.NewRun(fresh, h.Row, d, h.Basic, 3, signatureId: run.SignatureOffer(fresh, run.Data.FindClass(h.Row), 3).FirstOrDefault())))
        && run.AvailablePowers(cls, fresh).SequenceEqual(new[] { h.Basic }) && run.AvailableDecks(cls, fresh).Select(d => d.Id).SequenceEqual(new[] { cls.DefaultDeck }));
    var floor4 = new DungeonRunProfile(); floor4.Heroes.Add(new DungeonRunHeroStats { Class = h.Row, BestFloor = 5 }); run.RefreshUnlocks(floor4);
    Check($"T3 {h.Row}: reaching floor 5 with the hero unlocks HP1 and decks 2 and 3; HP2 and the Random deck still need a win",
        run.AvailablePowers(cls, floor4).SequenceEqual(new[] { h.Basic, $"DALA_{h.Cls}_HP1" })
        && run.AvailableDecks(cls, floor4).Count() == cls.Decks.Count - 1 && !run.AvailableDecks(cls, floor4).Any(d => d.IsGenerated), string.Join(",", floor4.Unlocked));
    var g = new Game(Configured(run.BuildPlan(run.NewRun(all, h.Row, cls.DefaultDeck, $"DALA_{h.Cls}_HP2", 9, signatureId: run.SignatureOffer(all, run.Data.FindClass(h.Row), 9).FirstOrDefault())), 41));
    g.StartGame();
    Check($"T3 {h.Row}: the match starts the portrait at 30 Health with DALA_{h.Cls}_HP2", g.Player1.Hero.Card.Id == h.Row && g.Player1.Hero.Health == 30
        && g.Player1.Hero.BaseHealth == 30 && g.Player1.Hero.HeroPower.Card.Id == $"DALA_{h.Cls}_HP2");
}

// ---------------------------------------------------------------- T4 DALA_Mage_HP1 Burning Wit
{
    var (s, plan, g) = Fight("DALA_Mage_HP1", 101);
    var p1 = g.Player1; Mana(p1, 10); Clear(p1);
    var yeti = ToHand(g, p1, "CS2_182"); var fb = ToHand(g, p1, "CS2_029");
    Check("T4 Burning Wit costs (1)", p1.Hero.HeroPower.Cost == 1);
    UsePower(g);
    var cut = new[] { yeti, fb }.Where(c => Has(c, "DALA_Mage_HP1e")).ToList();
    Check("T4 Burning Wit: exactly one random card in hand costs (2) less (4 -> 2), with Witty", cut.Count == 1 && cut[0].Cost == 2 && new[] { yeti, fb }.Except(cut).Single().Cost == 4
        && p1.RemainingMana == 9, string.Join(",", new[] { yeti, fb }.Select(c => c.Card.Id + ":" + c.Cost)));
    var (_, _, g2) = Fight("DALA_Mage_HP1", 101);
    Mana(g2.Player1, 10); Clear(g2.Player1); var y2 = ToHand(g2, g2.Player1, "CS2_182"); var f2 = ToHand(g2, g2.Player1, "CS2_029"); UsePower(g2);
    Check("T4 Burning Wit: the same seed cuts the same card (game RNG)", (y2.Cost == 2) == (yeti.Cost == 2) && (f2.Cost == 2) == (fb.Cost == 2));
    var (_, _, g3) = Fight("DALA_Mage_HP1", 102);
    Mana(g3.Player1, 10); Clear(g3.Player1);
    Check("T4 Burning Wit with an empty hand: usable, nothing changes", UsePower(g3) && g3.Player1.HandZone.Count == 0 && g3.Player1.Hero.HeroPower.IsExhausted);
}

// ---------------------------------------------------------------- T5 DALA_Mage_HP2 Frostburn
{
    var (s, plan, g) = Fight("DALA_Mage_HP2", 111);
    var p1 = g.Player1; var p2 = g.Player2; Mana(p1, 10);
    var yeti = Board(g, p2, "CS2_182");
    UsePower(g, yeti);
    Check("T5 Frostburn on an unfrozen minion: it is Frozen, no damage", yeti.IsFrozen && yeti.Health == 5);
    EndTurn(g); EndTurn(g); Mana(p1, 10);
    var bolt = ToHand(g, p1, "CS2_024");
    int hp = p2.Hero.Health;
    Play(g, bolt, p2.Hero);
    Check("T5 setup: Frostbolt freezes the boss hero", p2.Hero.IsFrozen && p2.Hero.Health == hp - 3);
    UsePower(g, p2.Hero);
    Check("T5 Frostburn on a Frozen character: 2 damage, still Frozen", p2.Hero.Health == hp - 5 && p2.Hero.IsFrozen, $"{hp}->{p2.Hero.Health}");
}

// ---------------------------------------------------------------- T6 DALA_Hunter_HP1 Opportunist
{
    var (s, plan, g) = Fight("DALA_Hunter_HP1", 121);
    var p1 = g.Player1; Mana(p1, 10);
    var yeti = Board(g, p1, "CS2_182"); var boss = Board(g, g.Player2, "CS2_182");
    Check("T6 Opportunist targets minions of both sides, never a hero", CanTarget(g, yeti) && CanTarget(g, boss) && !CanTarget(g, p1.Hero) && !CanTarget(g, g.Player2.Hero));
    UsePower(g, yeti);
    Check("T6 Opportunist: +2 Attack this turn (4 -> 6)", yeti.AttackDamage == 6 && Has(yeti, "DALA_Hunter_HP1e"));
    EndTurn(g);
    Check("T6 Opportunist: gone at the end of the turn", yeti.AttackDamage == 4 && !Has(yeti, "DALA_Hunter_HP1e"));
}

// ---------------------------------------------------------------- T7 DALA_Hunter_HP2 Pet Training (Shifting Chameleon)
{
    var (s, plan, g) = Fight("DALA_Hunter_HP2", 131);
    var p1 = g.Player1; Mana(p1, 10); Clear(p1);
    UsePower(g);
    var cham = p1.HandZone.SingleOrDefault();
    Check("T7 Pet Training: a 1/1 Shifting Chameleon in hand, costing (1)", cham?.Card.Id == "DALA_Hunter_HP2t" && cham.Cost == 1 && ((Minion)cham).AttackDamage == 1 && p1.RemainingMana == 9);
    EndTurn(g);
    Check("T7 the Chameleon does not shift at the start of the boss's turn", cham.Card.Id == "DALA_Hunter_HP2t");
    EndTurn(g);
    string first = cham.Card.Id;
    Check("T7 at the start of my turn it is a random scripted 1-Cost minion, still shifting (DALA_Hunter_HP2e)",
        cham.Zone == p1.HandZone && first != "DALA_Hunter_HP2t" && cham.Card.Cost == 1 && cham.Card.Type == CardType.MINION && cham.Card.Implemented && cham.Card.Collectible
        && Count(cham, "DALA_Hunter_HP2e") == 1, first);
    var seen = new List<string> { first };
    for (int i = 0; i < 4; i++) { EndTurn(g); EndTurn(g); seen.Add(cham.Card.Id); }
    Check("T7 it shifts again each of my turns, always a 1-Cost minion with one Shifting enchantment",
        seen.All(id => C(id).Cost == 1 && C(id).Type == CardType.MINION) && seen.Distinct().Count() > 1 && Count(cham, "DALA_Hunter_HP2e") == 1, string.Join(",", seen));
    Mana(p1, 10);
    string played = cham.Card.Id;
    bool ok = Play(g, cham);
    EndTurn(g); EndTurn(g);
    Check("T7 once played it keeps its form on the board (Shifting is removed when played)", ok && cham.Zone == p1.BoardZone && cham.Card.Id == played
        && !Has(cham, "DALA_Hunter_HP2e"));
    var (_, _, g2) = Fight("DALA_Hunter_HP2", 131);
    Mana(g2.Player1, 10); Clear(g2.Player1); UsePower(g2); var c2 = g2.Player1.HandZone.Single(); EndTurn(g2); EndTurn(g2);
    Check("T7 the same seed shifts into the same minion (game RNG)", c2.Card.Id == first, c2.Card.Id);
}

// ---------------------------------------------------------------- T8 DALA_Shaman_HP1 Evolution
{
    var (s, plan, g) = Fight("DALA_Shaman_HP1", 141);
    var p1 = g.Player1; Mana(p1, 10);
    var yeti = Board(g, p1, "CS2_182"); var boss = Board(g, g.Player2, "CS2_182");
    Check("T8 Evolution targets a friendly minion only", CanTarget(g, yeti) && !CanTarget(g, boss) && !CanTarget(g, p1.Hero));
    UsePower(g, yeti);
    Check("T8 Evolution: the 4-Cost Yeti becomes a random scripted 5-Cost minion where it stands", yeti.Card.Id != "CS2_182" && yeti.Card.Cost == 5 && yeti.Card.Implemented
        && yeti.Zone == p1.BoardZone && p1.BoardZone.Count == 1, yeti.Card.Id);
    var (_, _, g2) = Fight("DALA_Shaman_HP1", 141);
    Mana(g2.Player1, 10); var y2 = Board(g2, g2.Player1, "CS2_182"); Board(g2, g2.Player2, "CS2_182"); UsePower(g2, y2);
    Check("T8 Evolution: the same seed gives the same minion (game RNG)", y2.Card.Id == yeti.Card.Id, y2.Card.Id);
}

// ---------------------------------------------------------------- T9 DALA_Shaman_HP2 Refresh
{
    var (s, plan, g) = Fight("DALA_Shaman_HP2", 151);
    var p1 = g.Player1; Mana(p1, 10);
    int hand = p1.HandZone.Count, deck = p1.DeckZone.Count;
    UsePower(g);
    Check("T9 Refresh: draws a card and Overloads (1)", p1.HandZone.Count == hand + 1 && p1.DeckZone.Count == deck - 1 && p1.OverloadOwed == 1 && p1.RemainingMana == 9);
    EndTurn(g); EndTurn(g);
    Check("T9 Refresh: next turn one Mana Crystal is locked", p1.OverloadLocked == 1 && p1.RemainingMana == p1.BaseMana - 1, $"{p1.OverloadLocked} {p1.RemainingMana}/{p1.BaseMana}");
}

// ---------------------------------------------------------------- T10 DALA_Priest_HP1 Distort
{
    var (s, plan, g) = Fight("DALA_Priest_HP1", 161);
    var p1 = g.Player1; Mana(p1, 10);
    var boss = Board(g, g.Player2, "CS2_182");
    UsePower(g, boss);
    Check("T10 Distort: the 4/5 Yeti is 5/4 (Distorted)", boss.AttackDamage == 5 && boss.Health == 4 && Has(boss, "DALA_Priest_HP1e"),
        $"{boss.AttackDamage}/{boss.Health}");
}

// ---------------------------------------------------------------- T11 DALA_Priest_HP2 Soothe
{
    var (s, plan, g) = Fight("DALA_Priest_HP2", 171);
    var p1 = g.Player1; Mana(p1, 10);
    var boss = Board(g, g.Player2, "CS2_182");
    Check("T11 Soothe costs (1)", p1.Hero.HeroPower.Cost == 1);
    UsePower(g, boss);
    Check("T11 Soothe: the enemy Yeti has 2 Attack", boss.AttackDamage == 2);
    EndTurn(g);
    Check("T11 Soothe: still 2 Attack on the boss's turn", g.CurrentPlayer == g.Player2 && boss.AttackDamage == 2);
    EndTurn(g);
    Check("T11 Soothe: back to 4 at the start of my next turn, the enchantment gone", boss.AttackDamage == 4 && !Has(boss, "DALA_Priest_HP2e"));
}

// ---------------------------------------------------------------- T12 DALA_Rogue_HP1 Yoink!
{
    var (s, plan, g) = Fight("DALA_Rogue_HP1", 181);
    var p1 = g.Player1; Mana(p1, 10); Clear(p1);
    UsePower(g);
    var got = p1.HandZone.SingleOrDefault();
    Check("T12 Yoink!: a random scripted card of another class (not Rogue, not neutral) in my hand",
        got != null && got.Card.Implemented && got.Card.Class != CardClass.ROGUE && got.Card.Class != CardClass.NEUTRAL && got.Card.Collectible, got?.Card.Id + " " + got?.Card.Class);
    var seen = new HashSet<string> { got.Card.Id };
    for (int i = 0; i < 6; i++) { EndTurn(g); EndTurn(g); Mana(p1, 10); Clear(p1); UsePower(g); seen.Add(p1.HandZone.Single().Card.Id); }
    Check("T12 Yoink!: different cards over seven uses, none of them Rogue", seen.Count > 1 && seen.All(id => C(id).Class != CardClass.ROGUE));
    var (_, _, g2) = Fight("DALA_Rogue_HP1", 181);
    Mana(g2.Player1, 10); Clear(g2.Player1); UsePower(g2);
    Check("T12 Yoink!: the same seed gives the same card (game RNG)", g2.Player1.HandZone.Single().Card.Id == got.Card.Id);
}

// ---------------------------------------------------------------- T13 DALA_Rogue_HP2 Cut-less
{
    var (s, plan, g) = Fight("DALA_Rogue_HP2", 191);
    var p1 = g.Player1; Mana(p1, 10);
    var fresh = Board(g, g.Player2, "CS2_182"); var hurt = Board(g, g.Player2, "CS2_182"); hurt.Damage = 1;
    Check("T13 Cut-less targets an undamaged minion only (not a damaged one, not a hero)", CanTarget(g, fresh) && !CanTarget(g, hurt) && !CanTarget(g, g.Player2.Hero));
    UsePower(g, fresh);
    Check("T13 Cut-less: 2 damage", fresh.Health == 3);
}

// ---------------------------------------------------------------- T14 DALA_Warrior_HP1 Invigorating Brew
{
    var (s, plan, g) = Fight("DALA_Warrior_HP1", 201);
    var p1 = g.Player1; Mana(p1, 10);
    var yeti = Board(g, p1, "CS2_182");
    UsePower(g, yeti);
    Check("T14 Invigorating Brew: 1 damage and +2 Attack (Invigorated, lasting)", yeti.Health == 4 && yeti.AttackDamage == 6 && Has(yeti, "DALA_Warrior_HP1e"));
    EndTurn(g); EndTurn(g);
    Check("T14 Invigorating Brew: the +2 Attack stays", yeti.AttackDamage == 6);
}

// ---------------------------------------------------------------- T15 DALA_Warrior_HP2 Undermine (+ the Improvised Explosive)
{
    var (s, plan, g) = Fight("DALA_Warrior_HP2", 211);
    var p1 = g.Player1; var p2 = g.Player2; Mana(p1, 10);
    int deck = p2.DeckZone.Count, myDeck = p1.DeckZone.Count;
    UsePower(g);
    var bombs = p2.DeckZone.Where(e => e.Card.Id == "DALA_Warrior_HP2t").ToList();
    Check("T15 Undermine: two Improvised Explosives are shuffled into the boss's deck, none into mine", bombs.Count == 2 && p2.DeckZone.Count == deck + 2 && p1.DeckZone.Count == myDeck
        && bombs.All(b => b.Controller == p2));
    // The boss draws it as its turn's draw: the Explosive is put on top of its deck and the turn ends.
    p2.DeckZone.Remove(bombs[0]); p2.DeckZone.Add(bombs[0]);
    int hp = p2.Hero.Health, hand = p2.HandZone.Count, left = p2.DeckZone.Count;
    EndTurn(g);
    Check("T15 the boss draws one at its turn start: it casts itself (3 damage to the boss), leaves the hand, and the boss draws the next card instead",
        p2.Hero.Health == hp - 3 && bombs[0].Zone?.Type != Zone.HAND && p2.HandZone.Count == hand + 1 && p2.DeckZone.Count == left - 2
        && !p2.HandZone.Any(e => e.Card.Id == "DALA_Warrior_HP2t") && p2.DeckZone.Count(e => e.Card.Id == "DALA_Warrior_HP2t") == 1,
        $"{hp}->{p2.Hero.Health} hand {hand}->{p2.HandZone.Count} deck {left}->{p2.DeckZone.Count} zone {bombs[0].Zone?.Type}");
    EndTurn(g); Mana(p1, 10);
    var spare = ToHand(g, p1, "DALA_Warrior_HP2t");
    int mine = p1.Hero.Health;
    Play(g, spare);
    Check("T15 played from a hand, the Explosive deals 3 damage to its owner", p1.Hero.Health == mine - 3);
}

// ---------------------------------------------------------------- T16 DALA_Druid_HP1 Lifebloom
{
    var (s, plan, g) = Fight("DALA_Druid_HP1", 221);
    var p1 = g.Player1; Mana(p1, 10);
    var yeti = Board(g, p1, "CS2_182"); yeti.Damage = 4;
    var boss = Board(g, g.Player2, "CS2_182");
    Check("T16 Lifebloom targets a minion of either side", CanTarget(g, yeti) && CanTarget(g, boss) && !CanTarget(g, p1.Hero));
    UsePower(g, yeti);
    Check("T16 Lifebloom: the Yeti is back to full Health", yeti.Health == 5 && yeti.Damage == 0);
}

// ---------------------------------------------------------------- T17 DALA_Druid_HP2 Touch of Bark
{
    var (s, plan, g) = Fight("DALA_Druid_HP2", 231);
    var p1 = g.Player1; Mana(p1, 10);
    var yeti = Board(g, p1, "CS2_182");
    UsePower(g, yeti);
    Check("T17 Touch of Bark: +1/+1 (Barky)", yeti.AttackDamage == 5 && yeti.Health == 6 && Has(yeti, "DALA_Druid_HP2e"));
}

// ---------------------------------------------------------------- T18 DALA_Paladin_HP1 Backup
{
    var (s, plan, g) = Fight("DALA_Paladin_HP1", 241);
    var p1 = g.Player1; Mana(p1, 10); Clear(p1);
    UsePower(g);
    Check("T18 Backup: three 1/1 Silver Hand Recruits in my hand", p1.HandZone.Count == 3 && p1.HandZone.All(e => e.Card.Id == "CS2_101t"));
    var (_, _, g2) = Fight("DALA_Paladin_HP1", 242);
    var q1 = g2.Player1; Mana(q1, 10); Clear(q1); for (int i = 0; i < 9; i++) ToHand(g2, q1, "CS2_182");
    UsePower(g2);
    Check("T18 Backup with 9 cards in hand: one Recruit fits, the rest are burned (hand of 10)", q1.HandZone.Count == 10 && q1.HandZone.Count(e => e.Card.Id == "CS2_101t") == 1);
}

// ---------------------------------------------------------------- T19 DALA_Paladin_HP2 Boon of Light
{
    var (s, plan, g) = Fight("DALA_Paladin_HP2", 251);
    var p1 = g.Player1; Mana(p1, 10);
    var yeti = Board(g, p1, "CS2_182"); var boss = Board(g, g.Player2, "CS2_182");
    Check("T19 Boon of Light targets a friendly minion only", CanTarget(g, yeti) && !CanTarget(g, boss));
    UsePower(g, yeti);
    Check("T19 Boon of Light: Divine Shield", yeti.HasDivineShield);
}

// ---------------------------------------------------------------- T20 DALA_Warlock_HP1 Pain Split
{
    var (s, plan, g) = Fight("DALA_Warlock_HP1", 261);
    var p1 = g.Player1; Mana(p1, 10);
    var boss = Board(g, g.Player2, "CS2_182");
    int me = p1.Hero.Health, them = g.Player2.Hero.Health, yetiHp = boss.Health;
    UsePower(g, g.Player2.Hero);
    Check("T20 Pain Split on the boss hero: I take 2, the boss takes 2", p1.Hero.Health == me - 2 && g.Player2.Hero.Health == them - 2);
    EndTurn(g); EndTurn(g); Mana(p1, 10);
    UsePower(g, boss);
    Check("T20 Pain Split on a minion: 2 damage to it", boss.Damage == 2 && p1.Hero.Health == me - 4, $"yeti {yetiHp}->{boss.Health} dmg {boss.Damage} base {boss.BaseHealth} hero {me}->{p1.Hero.Health} armor {p1.Hero.Armor}");
}

// ---------------------------------------------------------------- T21 DALA_Warlock_HP2 The Pact
{
    var (s, plan, g) = Fight("DALA_Warlock_HP2", 271);
    var p1 = g.Player1; Mana(p1, 10);
    int me = p1.Hero.Health;
    UsePower(g);
    var imp = p1.BoardZone.SingleOrDefault();
    Check("T21 The Pact: I take 2 and a 2/2 Devious Imp (a Demon) is summoned", p1.Hero.Health == me - 2 && imp?.Card.Id == "DALA_Warlock_HP2t"
        && imp.AttackDamage == 2 && imp.Health == 2 && imp.Card.IsRace(Race.DEMON));
    while (!p1.BoardZone.IsFull) Board(g, p1, "CS2_182");
    EndTurn(g); EndTurn(g); Mana(p1, 10);
    Check("T21 The Pact needs a free board slot", !p1.Options().OfType<HeroPowerTask>().Any());
}

// ---------------------------------------------------------------- T23 picker data lists (the plank menu's Hero, Deck and Power steps)
{
    var fresh = new DungeonRunProfile();
    foreach (var h in heroes)
    {
        string cls = h.Cls.ToUpperInvariant(), low = h.Row.ToLowerInvariant();
        var row = data.Class(h.Row);
        var hero = DungeonRunCatalog.Heroes(run, fresh, cls).FirstOrDefault(x => x.Id == h.Row);
        Check($"T23 Heroes({cls}) lists {h.Row} ({row.Name}, Heist, 30 Health) after the class row",
            hero != null && hero.IsHero && !hero.IsDual && hero.Adventure == "Heist" && hero.Health == 30 && hero.HeroCardId == h.Row
            && DungeonRunCatalog.Heroes(run, fresh, cls)[0].Id == cls);
        var decks = DungeonRunCatalog.Decks(run, fresh, h.Row);
        // Build Deck: the generated deck (listed last) is the row's Build Deck option, always unlocked, named "Build Deck" (c-sched-9 BD10).
        Check($"T23 Decks({h.Row}): {row.Decks.Count} decks, the first open, the fixed rest locked with a hint naming {row.Name} (never its portrait id), the generated deck the open Build Deck",
            decks.Count == row.Decks.Count && decks[0].Unlocked && decks[0].IsDefault && decks.Skip(1).Take(decks.Count - 2).All(d => !d.Unlocked && d.UnlockHint.Contains(row.Name) && !d.UnlockHint.Contains("DALA_"))
            && decks.Last().IsBuild && decks.Last().Unlocked && decks.Last().Name == "Build Deck" && decks.Last().CardIds.Count == 0,
            string.Join(" | ", decks.Select(d => d.Name + ":" + d.UnlockHint)));
        var ps = DungeonRunCatalog.Powers(run, fresh, h.Row, id => transport.TryGetValue(id, out var sent) ? sent : id);
        Check($"T23 Powers({h.Row}): the basic power open (sent as its HERO_ id), HP1 and HP2 locked, named, costed and texted as the client prints them",
            ps.Count == 3 && ps[0].Unlocked && ps[0].ClientCardId == transport[h.Basic] && !ps[1].Unlocked && !ps[2].Unlocked
            && ps.Skip(1).All(o => o.Name == FactName(o.Id) && o.Text == FactText(o.Id) && o.Cost == FactTag(o.Id, 48) && o.ClientCardId == o.Id && o.UnlockHint.Contains(row.Name))
            && ps.Skip(1).All(o => o.Targeted == (C(o.Id).PlayRequirements?.ContainsKey(PlayReq.REQ_TARGET_TO_PLAY) == true)),
            string.Join(" | ", ps.Select(o => o.Name + ":" + o.UnlockHint)));
        string summary = DungeonRunCatalog.UnlockName(run, low + "_floor4") ?? "";
        Check($"T23 the run summary names what floor 4 with {row.Name} opens: {FactName($"DALA_{h.Cls}_HP1")}", summary.Contains(FactName($"DALA_{h.Cls}_HP1")) && summary.Contains(row.Name) && !summary.Contains("DALA_"), summary);
    }
}

// ---------------------------------------------------------------- T22 full games against the real boss AI, the player using the power
var testBudget = new Budget(6000, 2000, 300, 6, 3, 100, 8, turnSearchSeconds: 6, turnWallSeconds: 8);
string[] enemyFirst = { "DALA_Mage_HP2", "DALA_Rogue_HP2", "DALA_Warlock_HP1", "DALA_Priest_HP2", "DALA_Priest_HP1" };
string AiGame(string power, string deckId, long seed)
{
    string row = RowOf(power);
    var s = NewAct(row, seed, power, deckId);
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
    int decisionIndex = 0, actions = 0, decisions = 0, fallbacks = 0, powerUses = 0;
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
            var mine = options.OfType<HeroPowerTask>()
                .OrderByDescending(o => o.Target == null ? 0 : (o.Target.Controller != game.Player1) == enemyFirst.Contains(power) ? 1 : 0).FirstOrDefault();
            if (mine != null) { powerUses++; Act(mine, "player"); }
            else Act(LocalBot.Choose(options), "player");
        }
    }
    string result = game.State != State.COMPLETE ? "turn-cap" : game.Player2.PlayState == PlayState.WON ? "boss" : game.Player1.PlayState == PlayState.WON ? "player" : "tie";
    int explosives = game.Player2.SetasideZone.Count(e => e.Card.Id == "DALA_Warrior_HP2t") + game.Player2.GraveyardZone.Count(e => e.Card.Id == "DALA_Warrior_HP2t");
    return $"{result} turns {game.Turn} power-uses {powerUses} explosives-drawn {explosives} decisions {decisions} fallbacks {fallbacks} boss {boss.Id}";
}
foreach (string power in powers)
{
    string row = RowOf(power);
    var cls = data.Class(row);
    // HP1 plays on the first authentic starter, HP2 on the Random deck, so every Random deck plays a full game.
    string deckId = power.EndsWith("HP1") ? cls.DefaultDeck : cls.Decks.Last();
    string line;
    try { line = AiGame(power, deckId, 281); }
    catch (Exception e) { line = "FAULT " + e.GetType().Name + ": " + e.Message.Split('\n')[0]; }
    Console.WriteLine($"T22 {power,-17} {deckId,-22} {line}");
    Check($"T22 {power} on {deckId}: the player uses it in a full game against the boss AI, which ends without a fault or an AI fallback",
        !line.StartsWith("FAULT") && line.Contains("fallbacks 0 ") && !line.Contains("power-uses 0 ") && (power != "DALA_Warrior_HP2" || !line.Contains("explosives-drawn 0 ")), line);
}


Console.WriteLine($"{passes} passed, {failures} failed");
return failures == 0 ? 0 : 1;
