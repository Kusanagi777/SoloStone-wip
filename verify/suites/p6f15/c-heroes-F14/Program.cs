// c-heroes-F14: phase 6 family F14 of the unified Dungeon Run, Rumble Run's Rikkar: his nine class forms (TRLA_209h_Mage,
// Druid, Hunter, Paladin, Priest, Rogue, Shaman, Warlock, Warrior) as hero rows of run-v1.json (heroRows, kind hero, Rumble,
// 30 Health, beside the class rows), each with its class's basic power and the client's three shrine starter decks
// TRLA_<n>_<Class>_Player (27 decks, card for card; EX1_154 and EX1_160 play as their CORE_ reprints), the shrine itself left
// to phase 7 (run:shrine-lifecycle). TRLA_209h (the NEUTRAL base form) is no run portrait; TRLA_065p Steady Throw is Zul'jin's
// (TRLA_204h, a Rumble Run encounter) and is not offered. For every id: the engine content, the run data, the validator and
// admission checks, NewRun -> BuildPlan -> Configure -> Game on every row and deck, an act-run walk (drafts never run dry),
// the picker lists, and a full game per shrine deck against the real boss AI (DungeonRunBossPilot + GoldenPilot) in which
// the player uses the power and plays the deck; it must end without a fault or an AI fallback.
// Headless: compiles C:\SoloStone\plugin\DungeonRun\*.cs and the NPC AI against audit-verify/engine-out. Run it twice and compare.
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
string ClassOfTag(int v) => v switch { 2 => "DRUID", 3 => "HUNTER", 4 => "MAGE", 5 => "PALADIN", 6 => "PRIEST", 7 => "ROGUE", 8 => "SHAMAN", 9 => "WARLOCK", 10 => "WARRIOR", 12 => "NEUTRAL", _ => "?" + v };

// The client tables: card ids and dbf ids, and the 27 Rumble Run shrine starters (DECK + DECK_CARD, linked-list order).
var tables = System.Text.Json.JsonDocument.Parse(R("dungeon-run/reference/client-tables.json")).RootElement.GetProperty("tables");
var dbfOf = new Dictionary<string, int>(StringComparer.Ordinal);
foreach (var r in tables.GetProperty("CARD").EnumerateArray()) dbfOf[r.GetProperty("id").GetString()] = r.GetProperty("dbf").GetInt32();
var clientIds = new HashSet<string>(dbfOf.Keys, StringComparer.Ordinal);
var clientDecks = new Dictionary<string, (int DeckId, List<string> Cards)>(StringComparer.Ordinal);
{
    var deckCards = tables.GetProperty("DECK_CARD").EnumerateArray().GroupBy(r => r.GetProperty("deckId").GetInt32())
        .ToDictionary(g => g.Key, g => g.ToDictionary(r => r.GetProperty("ID").GetInt32()));
    foreach (var d in tables.GetProperty("DECK").EnumerateArray())
    {
        string note = d.GetProperty("noteName").GetString();
        if (!note.StartsWith("TRLA_") || !note.EndsWith("_Player")) continue;
        var rows = deckCards[d.GetProperty("ID").GetInt32()];
        var seq = new List<string>();
        for (int cur = d.GetProperty("topCardId").GetInt32(); cur != 0 && rows.ContainsKey(cur); cur = rows[cur].GetProperty("nextCardId").GetInt32())
            seq.Add(rows[cur].GetProperty("cardId_card").GetString());
        if (seq.Count != rows.Count) throw new Exception(note + ": the DECK_CARD list is not one chain");
        clientDecks[note] = (d.GetProperty("ID").GetInt32(), seq);
    }
}
bool InClientTable(string table, string id) => tables.GetProperty(table).EnumerateArray().Any(r => r.EnumerateObject().Any(p =>
    p.Value.ValueKind == System.Text.Json.JsonValueKind.String && p.Value.GetString() == id || p.Value.ValueKind == System.Text.Json.JsonValueKind.Number && dbfOf.TryGetValue(id, out int dbf) && p.Value.TryGetInt32(out int v) && v == dbf));

// form, basic power, the three shrine numbers in data order
var forms = new (string Cls, string Basic, int[] Shrines)[]
{
    ("Mage", "CS2_034", new[] { 108, 128, 129 }), ("Druid", "CS2_017", new[] { 112, 115, 116 }), ("Hunter", "DS1h_292", new[] { 110, 162, 163 }),
    ("Paladin", "CS2_101", new[] { 105, 137, 138 }), ("Priest", "CS1h_001", new[] { 114, 146, 147 }), ("Rogue", "CS2_083b", new[] { 106, 186, 187 }),
    ("Shaman", "CS2_049", new[] { 109, 154, 155 }), ("Warlock", "CS2_056", new[] { 113, 178, 179 }), ("Warrior", "CS2_102", new[] { 104, 170, 171 }),
};
string Row(string cls) => "TRLA_209h_" + cls;
var reprints = new Dictionary<string, string> { ["EX1_154"] = "CORE_EX1_154", ["EX1_160"] = "CORE_EX1_160" };
// Deck names are at most 20 characters (DungeonRunValidator.MaxDeckName): three shrine names are shortened.
var shortNames = new Dictionary<string, string> { ["TRLA_105"] = "Shirvallah's Shield", ["TRLA_137"] = "Shirvallah's Wrath", ["TRLA_154"] = "Tidal Tribute" };
string DeckName(int n) => shortNames.TryGetValue("TRLA_" + n, out var sn) ? sn : FactName("TRLA_" + n);
var policy = OfflinePoolPolicy.Load();
var transport = new Dictionary<string, string>
{
    ["CS2_034"] = "HERO_08bp", ["CS2_102"] = "HERO_01bp", ["CS1h_001"] = "HERO_09bp", ["CS2_101"] = "HERO_04bp", ["CS2_017"] = "HERO_06bp",
    ["DS1h_292"] = "HERO_05bp", ["CS2_083b"] = "HERO_03bp", ["CS2_056"] = "HERO_07bp", ["CS2_049"] = "HERO_02bp",
    ["AT_132_WARRIOR"] = "HERO_01bp2", ["AT_132_SHAMAN"] = "HERO_02bp2", ["AT_132_ROGUE"] = "HERO_03bp2", ["AT_132_PALADIN"] = "HERO_04bp2",
    ["AT_132_HUNTER"] = "HERO_05bp2", ["AT_132_DRUID"] = "HERO_06bp2", ["AT_132_WARLOCK"] = "HERO_07bp2", ["AT_132_MAGE"] = "HERO_08bp2", ["AT_132_PRIEST"] = "HERO_09bp2",
};
bool ClientKnows(string id) => clientIds.Contains(transport.TryGetValue(id, out var sent) ? sent : id);

// ---------------------------------------------------------------- T1 content: the nine forms, the base form, Steady Throw
Check("T1 11 F14 ids: 9 class forms, the base form TRLA_209h, TRLA_065p", forms.Length == 9 && forms.Select(f => Row(f.Cls)).Concat(new[] { "TRLA_209h", "TRLA_065p" }).Distinct().Count() == 11
    && forms.All(f => clientIds.Contains(Row(f.Cls))) && clientIds.Contains("TRLA_209h") && clientIds.Contains("TRLA_065p"));
foreach (var f in forms)
{
    string id = Row(f.Cls), cls = f.Cls.ToUpperInvariant();
    var content = DungeonRunContent.Get(id);
    var card = C(id);
    Check($"T1 {id}: a Hero keyed and shelled by its own id, scripted, the client's {cls} hero, at 30 Health (the client prints {FactTag(id, 45)})",
        content.Kind == RunContentKind.Hero && content.ShellId == id && card.Implemented && card.Type == CardType.HERO && card.Class.ToString() == cls
        && ClassOfTag(FactTag(id, 199)) == cls && FactTag(id, 202) == 3 && card.Health == 30 && content.Health == 30 && DungeonRunContent.ForShell(id) == content,
        $"{content.Kind} {card.Type} {card.Class} {card.Health}");
    Check($"T1 ids-text-match {id}: name and text equal the client's ('{FactName(id)}', no text)", content.Name == FactName(id) && content.Text == FactText(id) && content.Text == null,
        content.Name + " | " + content.Text);
}
Check("T1 TRLA_209h, the base form: a NEUTRAL hero (client class tag 12) with no run content, so no row can use it as a portrait",
    ClassOfTag(FactTag("TRLA_209h", 199)) == "NEUTRAL" && C("TRLA_209h").Class == CardClass.NEUTRAL && C("TRLA_209h").Type == CardType.HERO
    && !DungeonRunContent.TryGet("TRLA_209h", out _) && DungeonRunContent.ForShell("TRLA_209h") == null);
Check("T1 TRLA_065p Steady Throw is the hero power of TRLA_204h Zul'jin (its HERO_POWER tag 380), a Rumble Run encounter hero, not of any Rikkar form",
    FactTag("TRLA_204h", 380) == dbfOf["TRLA_065p"] && FactName("TRLA_204h") == "Zul'jin" && FactTag("TRLA_204h", 202) == 3
    && forms.All(f => FactTag(Row(f.Cls), 380) != dbfOf["TRLA_065p"]) && FactTag("TRLA_209h", 380) != dbfOf["TRLA_065p"]
    && factById.Values.Count(c => c.GetProperty("tags").EnumerateArray().Any(t => t[0].GetInt32() == 380 && t[1].GetInt32() == dbfOf["TRLA_065p"])) == 1);
// Phase 8 family 13 (wave 4) scripts Steady Throw as Zul'jin's encounter power (DungeonRunEncounters.F13.cs); it stays no run content.
Check("T1 TRLA_065p has no client loadout row (ADVENTURE_HERO_POWER, ADVENTURE_LOADOUT_TREASURES, GUEST_HERO) and no run content; since phase 8 family 13 it is scripted only as Zul'jin's encounter power",
    !InClientTable("ADVENTURE_HERO_POWER", "TRLA_065p") && !InClientTable("ADVENTURE_LOADOUT_TREASURES", "TRLA_065p") && !InClientTable("GUEST_HERO", "TRLA_065p")
    && C("TRLA_065p").Implemented && DungeonRunEncounters.Find("TRLA_065p")?.Family == "13" && DungeonRunEncounters.Find("TRLA_065p")?.Kind == EncounterCardKind.Power
    && !DungeonRunContent.TryGet("TRLA_065p", out _) && C("TRLA_065p").Type == CardType.HERO_POWER);

// ---------------------------------------------------------------- T2 data, validator, admission
{
    var errors = DungeonRunValidator.Validate(data, ClientKnows);
    Check("T2 shipped data validates with 0 errors (client check through the transported id)", errors.Count == 0, string.Join(" | ", errors.Take(5)));
    var legacy = DungeonRunData.Parse(runJson, poolJson, npcJson);
    Check("T2 the data without the catalog validates with 0 errors and loads no Rikkar row", DungeonRunValidator.Validate(legacy, null).Count == 0 && forms.All(f => legacy.FindClass(Row(f.Cls)) == null));
    Check("T2 the Rikkar rows sit beside the class rows: the eleven class rows are unchanged, the nine Rumble rows are hero rows in class order",
        data.ClassRows.Select(c => c.Id).SequenceEqual(new[] { "DRUID", "HUNTER", "MAGE", "PALADIN", "PRIEST", "ROGUE", "SHAMAN", "WARLOCK", "WARRIOR", "DEATHKNIGHT", "DEMONHUNTER" })
        && data.HeroRows.Where(h => h.Adventure == "Rumble").Select(h => h.Id).SequenceEqual(forms.Select(f => Row(f.Cls))));
    Check("T2 no row uses TRLA_209h or offers TRLA_065p", data.Classes.All(c => c.HeroCard != "TRLA_209h" && c.Id != "TRLA_209h" && !c.Powers.Contains("TRLA_065p"))
        && data.Bosses.All(b => b.Power != "TRLA_065p"));
    foreach (var f in forms)
    {
        string id = Row(f.Cls), cls = f.Cls.ToUpperInvariant();
        var r = data.FindClass(id);
        Check($"T2 hero row {id}: kind hero, Rumble, its own portrait, 30 Health, classes [{cls}], named Rikkar, enabled, no signatures",
            r != null && r.IsHero && r.Adventure == "Rumble" && r.HeroCard == id && r.Health == 30 && r.ClassSet.SequenceEqual(new[] { cls }) && r.Name == "Rikkar" && r.Enabled
            && r.Signatures.Count == 0 && run.AvailableHeroes(cls, all).Any(x => x.Id == id) && !r.IsDual);
        Check($"T2 hero row {id}: one power, the {f.Cls} basic power {f.Basic}, no unlock", r.Powers.SequenceEqual(new[] { f.Basic }) && r.PowerUnlock(f.Basic) == null && r.PowerUnlocks.Count == 0);
        var want = f.Shrines.Select(n => $"rumble_{f.Cls.ToLowerInvariant()}_{n}").ToList();
        Check($"T2 hero row {id}: decks {string.Join(", ", want)}", r.Decks.SequenceEqual(want), string.Join(",", r.Decks));
        foreach (int n in f.Shrines)
        {
            var d = data.FindDeck($"rumble_{f.Cls.ToLowerInvariant()}_{n}");
            string note = $"TRLA_{n}_{f.Cls}_Player", shrine = $"TRLA_{n}";
            var client = clientDecks[note].Cards;
            bool same = d.Cards.Count == 10 && client.Count == 10 && d.Cards.Zip(client, (a, b) => a == b || reprints.TryGetValue(b, out var rp) && rp == a && C(a).Name == FactName(b)).All(x => x);
            int at = Array.IndexOf(f.Shrines, n);
            string unlock = at == 0 ? null : id.ToLowerInvariant() + (at == 1 ? "_floor3" : "_floor5");
            Check($"T2 {d.Id}: {note} card for card (EX1_154/EX1_160 play as their CORE_ reprints, the originals being unscripted), named for its shrine '{FactName(shrine)}' ('{DeckName(n)}'), {(unlock ?? "open from the start")}, fixed",
                same && d.Class == id && !d.IsGenerated && d.Unlock == unlock && d.Name == DeckName(n) && d.Name.Length <= 20 && ClassOfTag(FactTag(shrine, 199)) == cls
                && (shortNames.ContainsKey(shrine) ? runJson.Contains(shrine + " " + FactName(shrine) + " (the deck name is shortened") : d.Name == FactName(shrine))
                && (unlock == null || data.Unlocks.ContainsKey(unlock) && data.Unlocks[unlock].Class == id)
                && runJson.Contains("\"source\": \"" + note + " (client DECK " + clientDecks[note].DeckId + ")"),
                string.Join(",", d.Cards.Zip(client, (a, b) => a == b ? a : a + "<-" + b)));
            Check($"T2 {d.Id}: every card scripted and admitted for a {cls} catalog run", d.Cards.All(x => DungeonRunValidator.PoolCardReason(x, new[] { cls }, false, policy, catalogRun: true) == null),
                string.Join(",", d.Cards.Where(x => DungeonRunValidator.PoolCardReason(x, new[] { cls }, false, policy, catalogRun: true) != null)));
            foreach (var orig in client.Where(reprints.ContainsKey))
                Check($"T2 {d.Id}: {orig} is unscripted, so {reprints[orig]} plays", !C(orig).Implemented && C(reprints[orig]).Implemented && d.Cards.Contains(reprints[orig]) && !d.Cards.Contains(orig));
        }
    }
    Check("T2 the four decks that waited on family F10 ship: TRLA_106 (Captain's Parrot), TRLA_109 (Huge Toad), TRLA_112 (Snapjaw Shellfighter), TRLA_155 (Wartbringer)",
        data.FindDeck("rumble_rogue_106").Cards.Contains("NEW1_016") && data.FindDeck("rumble_shaman_109").Cards.Contains("LOE_046")
        && data.FindDeck("rumble_druid_112").Cards.Contains("TRL_535") && data.FindDeck("rumble_shaman_155").Cards.Contains("TRL_522")
        && new[] { "NEW1_016", "LOE_046", "TRL_535", "TRL_522" }.All(x => C(x).Implemented));
    Check("T2 the 27 shrine starters are the client's 27 TRLA_<n>_<Class>_Player decks, each shipped once", clientDecks.Count == 27
        && data.Decks.Count(d => d.Id.StartsWith("rumble_")) == 27 && data.Decks.Where(d => d.Id.StartsWith("rumble_")).Select(d => d.Id.Split('_')).Select(p => $"TRLA_{p[2]}_{char.ToUpperInvariant(p[1][0]) + p[1].Substring(1)}_Player").OrderBy(x => x, StringComparer.Ordinal)
            .SequenceEqual(clientDecks.Keys.OrderBy(x => x, StringComparer.Ordinal)));

    // Validator branches
    string Swap(string json, string from, string to) { if (!json.Contains(from)) throw new Exception("fixture text not found: " + from); return json.Replace(from, to); }
    var e1 = DungeonRunValidator.Validate(Load(Swap(runJson, "\"hero\": \"TRLA_209h_Mage\"", "\"hero\": \"TRLA_209h\"")), null);
    Check("T2 validator: the NEUTRAL base form TRLA_209h as a Mage row's portrait is refused", e1.Any(e => e.Contains("class TRLA_209h_Mage") && e.Contains("portrait TRLA_209h is NEUTRAL")), string.Join(" | ", e1.Take(3)));
    var e2 = DungeonRunValidator.Validate(Load(Swap(runJson, "\"hero\": \"TRLA_209h_Hunter\", \"health\": 30, \"classes\": [\"HUNTER\"], \"powers\": [\"DS1h_292\"]",
        "\"hero\": \"TRLA_209h_Hunter\", \"health\": 30, \"classes\": [\"HUNTER\"], \"powers\": [\"TRLA_065p\"]")), null);
    // Phase 8 family 13 (wave 4): scripted now, so refused by the admission table (a boss's power only), no longer as unscripted.
    Check("T2 validator: TRLA_065p as Rikkar's Hunter power is refused (since phase 8 family 13: a boss hero power, admission boss-power-public)",
        e2.Any(e => e.StartsWith("TRLA_065p") && e.Contains("admission boss-power-public") && e.Contains("a boss hero power")), string.Join(" | ", e2.Take(3)));
    var e3 = DungeonRunValidator.Validate(Load(Swap(runJson, "\"cards\": [\"GVG_032\", \"LOOT_048\", \"TRL_010\", \"CORE_EX1_154\"", "\"cards\": [\"GVG_032\", \"LOOT_048\", \"TRL_010\", \"EX1_154\"")), null);
    Check("T2 validator: the unscripted original EX1_154 in a shrine starter is refused", e3.Any(e => e.StartsWith("EX1_154") && e.Contains("rumble_druid_112")), string.Join(" | ", e3.Take(3)));
    var e4 = DungeonRunValidator.Validate(Load(Swap(runJson, "\"hero\": \"TRLA_209h_Warrior\", \"health\": 30, \"classes\": [\"WARRIOR\"]", "\"hero\": \"TRLA_209h_Warrior\", \"health\": 30, \"classes\": [\"MAGE\"]")), null);
    Check("T2 validator: a form whose classes do not match its portrait is refused", e4.Any(e => e.Contains("class TRLA_209h_Warrior") && e.Contains("portrait TRLA_209h_Warrior is WARRIOR")), string.Join(" | ", e4.Take(3)));
}

// ---------------------------------------------------------------- T2 admission
foreach (var f in forms)
{
    string id = Row(f.Cls);
    Check($"T2 admission {id}: the portrait needs no row (no rules text) and is admitted for the player", DungeonRunAdmission.Classify(C(id), id, AdmitScope.Player, null, true).Admitted
        && DungeonRunAdmission.RuleFor(id) == null);
    Check($"T2 admission {f.Basic} (Rikkar's {f.Cls} power): admitted for the player", DungeonRunAdmission.Classify(C(f.Basic), f.Basic, AdmitScope.Player, null, true).Admitted);
}

// ---------------------------------------------------------------- T2 additive rows: every earlier row's runs are unchanged
{
    // Phase 6 F15 changed earlier rows on purpose (Elise's sixth signature, new Tombs, Heist and Tess treasures): the baseline is the
    // pre-F14 data with exactly F15's rows applied (p6f15/mkbase.py), so this check still isolates F14's own additions.
    string beforeJson = File.ReadAllText(VerifyPaths.Fixture("p6f15/run-v1.before-f14-with-f15.json"));
    var before = Load(beforeJson);
    var oldRun = new DungeonRun(before);
    var oldAll = new DungeonRunProfile(); oldAll.Unlocked.AddRange(before.Unlocks.Keys);
    Check("T2 the pre-F14 data has no Rikkar row; the new data adds exactly the nine (and their ladder's unlock keys)",
        forms.All(f => before.FindClass(Row(f.Cls)) == null) && data.Classes.Count == before.Classes.Count + 9
        && data.Unlocks.Keys.Except(before.Unlocks.Keys).All(k => forms.Any(f => k.StartsWith(Row(f.Cls).ToLowerInvariant() + "_")))
        && before.Unlocks.Keys.All(data.Unlocks.ContainsKey));
    var ser = new System.Xml.Serialization.XmlSerializer(typeof(DungeonRunState));
    string Xml(DungeonRunState s) { var w = new StringWriter(); ser.Serialize(w, s); return w.ToString(); }
    int same = 0, compared = 0;
    var diffs = new List<string>();
    foreach (var cls in before.Classes)
        foreach (string schedule in cls.IsHero ? new string[] { null } : new[] { null, DungeonRunSchedules.LegacyId })
            for (long seed = 1; seed <= 3; seed++)
            {
                compared++;
                string sig = cls.Signatures.Count > 0 ? oldRun.SignatureOffer(oldAll, cls, seed)[0] : null;
                var a = oldRun.NewRun(oldAll, cls.Id, cls.DefaultDeck, cls.DefaultPower, seed, schedule: schedule, signatureId: sig);
                var b = run.NewRun(all, cls.Id, cls.DefaultDeck, cls.DefaultPower, seed, schedule: schedule, signatureId: sig);
                oldRun.OpenTreasureDraft(a); run.OpenTreasureDraft(b);
                if (Xml(a) == Xml(b)) same++; else if (diffs.Count < 3) diffs.Add(cls.Id + "/" + (schedule ?? "acts") + "/" + seed);
            }
    Check($"T2 every earlier row's new run (class rows: act and legacy schedules) and first treasure draft are byte for byte the pre-F14 ones ({compared} runs)", same == compared, string.Join(",", diffs));
}

// ---------------------------------------------------------------- the run setup path
GameConfig Configured(RunMatchPlan plan, int seed)
{
    var cfg = new GameConfig { RandomSeed = seed, Shuffle = true, SkipMulligan = false, History = true, Logging = false, FormatType = FormatType.FT_WILD };
    DungeonRunSetup.Configure(cfg, plan);
    return cfg;
}

// ---------------------------------------------------------------- T3 NewRun / BuildPlan / match start on every row and deck
foreach (var f in forms)
{
    string id = Row(f.Cls), cls = f.Cls.ToUpperInvariant();
    var row = data.Class(id);
    foreach (string deckId in row.Decks)
    {
        var deck = data.FindDeck(deckId);
        var s = run.NewRun(all, id, deckId, f.Basic, 5);
        var plan = run.BuildPlan(s);
        Check($"T3 NewRun {id} on {deckId}: an act catalog run of the hero row, the shrine starter in the run deck (origin starter), the basic power",
            s.Class == id && s.IsActRun && s.IsCatalogRun && s.StarterDeck == deckId && s.Power == f.Basic && s.Deck.SequenceEqual(deck.Cards)
            && s.Cards.All(c => c.Origin == DungeonRunDeck.OriginStarter) && s.Loadout?.Id == id && s.Loadout.Signature.Count == 0 && run.RunClasses(s).SequenceEqual(new[] { cls }));
        Check($"T3 BuildPlan {id} {deckId}: the form's portrait at 30 Health, the power, the run deck",
            plan.PlayerHero.Id == id && plan.PlayerHeroHealth == 30 && plan.PlayerHeroPowerId == f.Basic
            && plan.PlayerDeck.Select(c => c.Id).OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(s.Deck.OrderBy(x => x, StringComparer.Ordinal)));
        var g = new Game(Configured(plan, 41 + Array.IndexOf(row.Decks.ToArray(), deckId)));
        g.StartGame();
        DungeonRunSetup.ApplyAfterStart(g, plan);
        var p1 = g.Player1;
        var inMatch = p1.DeckZone.Concat(p1.HandZone).Select(e => e.Card.Id).Where(x => x != "GAME_005").OrderBy(x => x, StringComparer.Ordinal).ToList();
        Check($"T3 {id} {deckId}: the match starts Rikkar ({cls}) at 30 Health with {f.Basic} (sent as {transport[f.Basic]}) and the ten shrine-deck cards",
            p1.Hero.Card.Id == id && p1.Hero.Health == 30 && p1.Hero.BaseHealth == 30 && p1.Hero.Card.Class.ToString() == cls && p1.Hero.HeroPower.Card.Id == f.Basic
            && inMatch.SequenceEqual(deck.Cards.OrderBy(x => x, StringComparer.Ordinal)) && clientIds.Contains(transport[f.Basic]), string.Join(",", inMatch));
    }
    var fresh = new DungeonRunProfile();
    bool Refused(Func<DungeonRunState> fn) { try { fn(); return false; } catch (InvalidOperationException) { return true; } catch (ArgumentException) { return true; } }
    Check($"T3 {id}: a new profile has Rikkar's form, its power and the first shrine starter; the second and third are locked",
        run.AvailableHeroes(cls, fresh).Any(h => h.Id == id) && run.AvailablePowers(row, fresh).SequenceEqual(new[] { f.Basic })
        && run.AvailableDecks(row, fresh).Select(d => d.Id).SequenceEqual(new[] { row.Decks[0] }) && run.NewRun(fresh, id, row.Decks[0], f.Basic, 3).StarterDeck == row.Decks[0]
        && Refused(() => run.NewRun(fresh, id, row.Decks[1], f.Basic, 3)) && Refused(() => run.NewRun(fresh, id, row.Decks[2], f.Basic, 3)));
    var floor5 = new DungeonRunProfile(); floor5.Heroes.Add(new DungeonRunHeroStats { Class = id, BestFloor = 5 }); run.RefreshUnlocks(floor5);
    var floor3 = new DungeonRunProfile(); floor3.Heroes.Add(new DungeonRunHeroStats { Class = id, BestFloor = 3 }); run.RefreshUnlocks(floor3);
    Check($"T3 {id}: reaching floor 3 with the form opens the second shrine starter, floor 5 the third; the class row's progress does not",
        run.AvailableDecks(row, floor3).Select(d => d.Id).SequenceEqual(row.Decks.Take(2)) && run.AvailableDecks(row, floor5).Select(d => d.Id).SequenceEqual(row.Decks),
        string.Join(",", floor5.Unlocked));
    var classOnly = new DungeonRunProfile(); classOnly.Heroes.Add(new DungeonRunHeroStats { Class = cls, BestFloor = 15 }); run.RefreshUnlocks(classOnly);
    Check($"T3 {id}: a {cls} class row's progress does not open Rikkar's shrine starters", run.AvailableDecks(row, classOnly).Count() == 1);
    Check($"T3 {id}: NewRun refuses another class's deck and another class's power", Refused(() => run.NewRun(all, id, "heist_mage_1", f.Basic, 3))
        && Refused(() => run.NewRun(all, id, row.DefaultDeck, f.Cls == "Mage" ? "CS2_102" : "CS2_034", 3)));
}

// ---------------------------------------------------------------- T4 act-run walk: every draft floor offers a full choice, treasures are offered
foreach (var f in forms)
{
    string id = Row(f.Cls);
    var row = data.Class(id);
    int shortFloors = 0, treasureDrafts = 0, picked = 0, runs = 0;
    var notes = new List<string>();
    foreach (string deckId in row.Decks)
        for (long seed = 11; seed <= 13; seed++)
        {
            var s = run.NewRun(all, id, deckId, f.Basic, seed);
            int lastFight = run.ScheduleFor(s).FloorCount;
            for (int guard = 0; guard < 500 && !s.Finished; guard++)
            {
                switch (s.Phase)
                {
                    case DungeonRunPhase.PreMatch:
                        int floor = s.Floor;
                        bool full = s.Deck.Count + run.DraftTakes(s) > run.DeckMax(s);
                        run.BeginMatch(s); run.RecordOutcome(s, all, DungeonRunOutcome.Win);
                        if (floor == lastFight) break;
                        if (s.Phase == DungeonRunPhase.TreasureDraft) { treasureDrafts++; run.PickTreasure(s, s.OpenDraft.Offered[0]); }
                        int offers = s.Phase == DungeonRunPhase.BundleDraft ? s.OpenDraft.Offered.Count : 0, want = s.Phase == DungeonRunPhase.BundleDraft ? run.DraftOptions(s) : 3;
                        if (!full && offers < Math.Min(3, want)) { shortFloors++; if (notes.Count < 3) notes.Add($"{deckId}/{seed}/floor {floor}: {offers}"); }
                        break;
                    case DungeonRunPhase.TreasureDraft: treasureDrafts++; run.PickTreasure(s, s.OpenDraft.Offered[0]); break;
                    case DungeonRunPhase.Modifier: run.PickModifier(s, s.ModifierOffer.Offered[0]); break;
                    case DungeonRunPhase.Tavern: run.LeaveTavern(s); break;
                    case DungeonRunPhase.BundleDraft:
                        if (s.OpenDraft.Takes == 0) { run.PickBundle(s, s.OpenDraft.Offered[0]); picked++; break; }
                        string bundle = s.OpenDraft.Choosing ?? s.OpenDraft.Offered[0];
                        var left = run.OfferCards(s.OpenDraft, bundle);
                        foreach (string kept in s.OpenDraft.Chosen) left.Remove(kept);
                        run.PickDraftCard(s, bundle, left[0]); picked++;
                        break;
                    default: throw new Exception("walk stopped at " + s.Phase);
                }
            }
            if (s.Finished && s.Matches.Count(m => m.Outcome == DungeonRunOutcome.Win) == lastFight) runs++;
            else notes.Add($"{deckId}/{seed} did not finish");
        }
    Check($"T4 {id}: nine act runs (3 decks x 3 seeds) win through all 14 fights; every draft floor offers at least 3 choices; treasure drafts open; cards are drafted",
        runs == 9 && shortFloors == 0 && treasureDrafts >= 9 && picked > 0, $"runs {runs} short {shortFloors} treasures {treasureDrafts} picks {picked} {string.Join("; ", notes)}");
}

// ---------------------------------------------------------------- T5 picker data lists (the plank menu's Hero, Deck and Power steps)
{
    var fresh = new DungeonRunProfile();
    foreach (var f in forms)
    {
        string id = Row(f.Cls), cls = f.Cls.ToUpperInvariant();
        var heroes = DungeonRunCatalog.Heroes(run, fresh, cls);
        var hero = heroes.FirstOrDefault(x => x.Id == id);
        Check($"T5 Heroes({cls}) lists {id} (Rikkar, Rumble, 30 Health, portrait {id}) after the class row",
            hero != null && hero.IsHero && !hero.IsDual && hero.Adventure == "Rumble" && hero.Health == 30 && hero.HeroCardId == id && hero.Name == "Rikkar" && heroes[0].Id == cls);
        var decks = DungeonRunCatalog.Decks(run, fresh, id);
        Check($"T5 Decks({id}): the three shrine starters named for their shrines, ten cards each, the first open, the rest locked with a hint naming Rikkar (never a portrait id)",
            decks.Count == 3 && decks.All(d => d.CardIds.Count == 10) && decks[0].IsDefault && decks[0].Unlocked
            && decks.Skip(1).All(d => !d.Unlocked && d.UnlockHint.Contains("Rikkar") && !d.UnlockHint.Contains("TRLA_"))
            && decks.Select(d => d.Name).SequenceEqual(f.Shrines.Select(DeckName)), string.Join(" | ", decks.Select(d => d.Name + ":" + d.UnlockHint)));
        var ps = DungeonRunCatalog.Powers(run, fresh, id, x => transport.TryGetValue(x, out var sent) ? sent : x);
        Check($"T5 Powers({id}): the {f.Cls} basic power alone, open, sent as {transport[f.Basic]}", ps.Count == 1 && ps[0].Unlocked && ps[0].Id == f.Basic && ps[0].ClientCardId == transport[f.Basic]);
    }
}

// ---------------------------------------------------------------- T6 full games against the real boss AI: the player uses the power and plays the shrine deck
var testBudget = new Budget(6000, 2000, 300, 6, 3, 100, 8, turnSearchSeconds: 6, turnWallSeconds: 8);
string[] friendlyFirst = { "CS2_101", "CS2_017", "CS1h_001", "CS2_083b", "CS2_049", "CS2_056", "CS2_102" };
string AiGame(string id, string power, string deckId, long seed)
{
    var s = run.NewRun(all, id, deckId, power, seed);
    if (!s.IsActRun || s.Phase != DungeonRunPhase.PreMatch || s.Floor != 1) throw new Exception("act run not at floor 1 pre-match");
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
    if (game.Player1.Hero.Card.Id != id || game.Player1.Hero.Health != 30) throw new Exception("the match did not start Rikkar at 30 Health");
    game.Process(ChooseTask.Mulligan(game.Player1, new List<int>()));
    var choice = game.Player2.Choice;
    var view = new MulliganView(choice.Choices.Select(x => new MulliganCard(x, game.IdEntityDic[x].Card.Id, game.IdEntityDic[x].Card.Cost)),
        false, pilot.Deck.CardIds, game.Player1.Hero.Card.Class);
    game.Process(ChooseTask.Mulligan(game.Player2, BotDriver.Mulligan(view, new DeckPlanMulligan(pilot.Plan)).ToList()));
    game.MainBegin();
    var deckCards = new HashSet<string>(data.FindDeck(deckId).Cards, StringComparer.Ordinal);
    int decisionIndex = 0, actions = 0, decisions = 0, fallbacks = 0, powerUses = 0, played = 0;
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
                .OrderByDescending(o => o.Target == null ? 0 : (o.Target.Controller == game.Player1) == friendlyFirst.Contains(power) ? 1 : 0).FirstOrDefault();
            if (mine != null) { powerUses++; Act(mine, "player"); continue; }
            var task = LocalBot.Choose(options);
            if (task is PlayCardTask pc && deckCards.Contains(pc.Source.Card.Id)) played++;
            Act(task, "player");
        }
    }
    string result = game.State != State.COMPLETE ? "turn-cap" : game.Player2.PlayState == PlayState.WON ? "boss" : game.Player1.PlayState == PlayState.WON ? "player" : "tie";
    return $"{result} turns {game.Turn} power-uses {powerUses} deck-cards-played {played} decisions {decisions} fallbacks {fallbacks} boss {boss.Id}";
}
foreach (var f in forms)
{
    string id = Row(f.Cls);
    foreach (string deckId in data.Class(id).Decks)
    {
        string line;
        try { line = AiGame(id, f.Basic, deckId, 281); }
        catch (Exception e) { line = "FAULT " + e.GetType().Name + ": " + e.Message.Split('\n')[0]; }
        Console.WriteLine($"T6 {id,-17} {deckId,-20} {line}");
        Check($"T6 {id} on {deckId}: the player uses {f.Basic} and plays the shrine deck in a full game against the boss AI, which ends without a fault or an AI fallback",
            !line.StartsWith("FAULT") && line.Contains("fallbacks 0 ") && !line.Contains("power-uses 0 ") && !line.Contains("deck-cards-played 0 ") && !line.StartsWith("turn-cap"), line);
    }
}

Console.WriteLine($"{passes} passed, {failures} failed");
return failures == 0 ? 0 : 1;
