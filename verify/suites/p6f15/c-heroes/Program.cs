// c-heroes: phase 6 F0 (the loadout model) of the unified Dungeon Run. Hero rows (run-v1.json "heroRows" and the Tombs dual
// rows, kind "hero"), the 26 named hero portraits (RunContentKind.Hero), validator acceptance and rejection, NewRun and
// BuildPlan for hero rows (Health 30, powers, decks, generated starters), signature offers and the Jr./Sr. profile upgrade,
// and the picker data lists. Every portrait is played in a real engine game. Headless: compiles
// C:\SoloStone\plugin\DungeonRun\*.cs (minus the client-only runtime and boss pilot) against p6f0/engine-out.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using SabberStoneCore.CardSets.DungeonRun;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;

int failures = 0, passes = 0;
void Check(string name, bool ok, string detail = "")
{
    if (ok) passes++; else failures++;
    if (!ok || Environment.GetEnvironmentVariable("VERBOSE") == "1")
        Console.WriteLine((ok ? "PASS " : "FAIL ") + name + (detail.Length > 0 ? "  (" + detail + ")" : ""));
}
void Throws<T>(string name, Action action, string expectContains = null) where T : Exception
{
    try { action(); Check(name, false, "did not throw"); }
    catch (T e) { Check(name, expectContains == null || e.Message.Contains(expectContains), e.Message); }
    catch (Exception e) { Check(name, false, e.GetType().Name + ": " + e.Message); }
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
DungeonRunData Load(string run = null) => DungeonRunData.Parse(run ?? runJson, poolJson, npcJson, catalogJson, scheduleJson, tavernJson);
var data = Load();
var run = new DungeonRun(data);
var fresh = new DungeonRunProfile();
var all = new DungeonRunProfile(); all.Unlocked.AddRange(data.Unlocks.Keys);
Card C(string id) => Cards.FromId(id);
Game StartPlan(RunMatchPlan plan, int seed)
{
    var cfg = new GameConfig { RandomSeed = seed, Shuffle = true, SkipMulligan = false, History = true, Logging = false, FormatType = FormatType.FT_WILD };
    DungeonRunSetup.Configure(cfg, plan);
    var g = new Game(cfg); g.StartGame(); DungeonRunSetup.ApplyAfterStart(g, plan);
    return g;
}
// Plays a few turns of a real match with legal options only (end turn after the first playable option), so the hero, its power
// and the board are exercised; returns the game.
Game PlayTurns(Game g, int turns)
{
    if (g.Step == Step.BEGIN_MULLIGAN)
    {
        foreach (var p in new[] { g.Player1, g.Player2 }) g.Process(SabberStoneCore.Tasks.PlayerTasks.ChooseTask.Mulligan(p, new List<int>()));
    }
    for (int t = 0; t < turns && g.State != State.COMPLETE; t++)
    {
        var options = g.CurrentPlayer.Options();
        var power = options.FirstOrDefault(o => o is SabberStoneCore.Tasks.PlayerTasks.HeroPowerTask && !o.HasTarget);
        if (power != null) g.Process(power);
        if (g.State != State.COMPLETE) g.Process(SabberStoneCore.Tasks.PlayerTasks.EndTurnTask.Any(g.CurrentPlayer));
    }
    return g;
}
var facts = System.Text.Json.JsonDocument.Parse(File.ReadAllText(VerifyPaths.Repo("facts/violet-hold-2026-09.json")));
var factById = facts.RootElement.GetProperty("cards").EnumerateArray().ToDictionary(c => c.GetProperty("id").GetString(), c => c);

// ---------------------------------------------------------------- H1 the 26 named hero portraits
string[] portraits =
{
    "DALA_Rakanishu", "DALA_Barkeye", "DALA_Vessina", "DALA_Kriziki", "DALA_Eudora", "DALA_Chu", "DALA_Squeamlish", "DALA_Tekahn", "DALA_George",
    "GILA_500h3", "GILA_600h", "GILA_400h", "GILA_900h",
    "TRLA_209h_Mage", "TRLA_209h_Druid", "TRLA_209h_Hunter", "TRLA_209h_Paladin", "TRLA_209h_Priest", "TRLA_209h_Rogue", "TRLA_209h_Shaman", "TRLA_209h_Warlock", "TRLA_209h_Warrior",
    "ULDA_Reno", "ULDA_Finley", "ULDA_Elise", "ULDA_Brann",
};
{
    var heroes = DungeonRunContent.All.Where(c => c.Kind == RunContentKind.Hero).ToList();
    Check("H1 exactly the 26 named portraits are Hero content, keyed and shelled by their own id",
        heroes.Select(h => h.ShellId).OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(portraits.OrderBy(x => x, StringComparer.Ordinal))
        && heroes.All(h => h.Key == h.ShellId) && DungeonRunContent.NamedHeroIds.Count() == 26, string.Join(",", heroes.Select(h => h.ShellId)));
    foreach (string id in portraits)
    {
        var card = C(id);
        var content = DungeonRunContent.Get(id);
        Check($"H1 {id}: a scripted HERO at 30 Health (the portrait printed {(factById[id].GetProperty("tags").EnumerateArray().Where(t => t[0].GetInt32() == 45).Select(t => t[1].GetInt32()).FirstOrDefault())})",
            card != null && card.Type == CardType.HERO && card.Implemented && card.Health == 30 && card[GameTag.HEALTH] == 30 && content.Health == 30);
        // ids-text-match: the run's name override and text override are the client's own, word for word.
        var f = factById[id];
        string factText = f.TryGetProperty("text", out var tx) ? tx.GetString() : null;
        Check($"H1 ids-text-match {id}: content name and text equal facts/violet-hold-2026-09.json", content.Name == f.GetProperty("name").GetString() && content.Text == factText,
            content.Name + " | " + content.Text);
        Check($"H1 {id}: ForShell finds its own content", DungeonRunContent.ForShell(id) == content);
    }
    Check("H1 boss variants stay boss-only (GILA_500h4 is not Hero content)", !DungeonRunContent.TryGet("GILA_500h4", out _));
    Check("H1 PassiveHeroPowers is unchanged by the Hero kind", !DungeonRunCardsGen.PassiveHeroPowers.Any(portraits.Contains));
}

// ---------------------------------------------------------------- H2 shipped data: the Tombs rows are hero rows
{
    Check("H2 shipped data validates with 0 errors (catalog, schedules, tavern)", DungeonRunValidator.Validate(data, null).Count == 0,
        string.Join(" | ", DungeonRunValidator.Validate(data, null).Take(5)));
    var mr = data.Class("MAGE_ROGUE");
    Check("H2 MAGE_ROGUE is a Tombs hero row with the ULDA_Reno portrait at 30 Health", mr.IsHero && mr.Adventure == "Tombs" && mr.HeroCard == "ULDA_Reno" && mr.Health == 30
        && mr.ClassSet.SequenceEqual(new[] { "MAGE", "ROGUE" }));
    // Phase 6 F10 scripts their starter collectibles: the three dual rows that shipped disabled ship enabled.
    Check("H2 the three dual rows that shipped disabled until F10 are enabled hero rows with their explorer portraits",
        new[] { ("PALADIN_SHAMAN", "ULDA_Finley"), ("DRUID_PRIEST", "ULDA_Elise"), ("HUNTER_WARRIOR", "ULDA_Brann") }
            .All(p => data.FindClass(p.Item1) is DungeonRunClass c && c.IsHero && c.Enabled && c.HeroCard == p.Item2 && c.Health == 30 && c.DisabledReason == null)
        && data.DisabledClasses.Count == 0);
    // Phase 6 F3 on: the enabled rows of run-v1.json "heroRows" (Tess GILA_500h3, Darius GILA_600h, ...) follow MAGE_ROGUE.
    var shippedHeroRows = (JsonNode.Parse(runJson)["heroRows"]?.AsArray() ?? new JsonArray())
        .Where(r => r["enabled"] == null || (bool)r["enabled"]).Select(r => (string)r["id"]).ToList();
    Check("H2 ClassRows: the eleven class rows; HeroRows: the four Tombs dual rows (F10), then the shipped heroRows", data.ClassRows.Count() == 11 && data.ClassRows.All(c => !c.IsHero)
        && data.HeroRows.Select(h => h.Id).SequenceEqual(new[] { "MAGE_ROGUE", "PALADIN_SHAMAN", "DRUID_PRIEST", "HUNTER_WARRIOR" }.Concat(shippedHeroRows)), string.Join(",", data.HeroRows.Select(h => h.Id)));
    var legacyData = DungeonRunData.Parse(runJson, poolJson, npcJson);
    Check("H2 without the catalog no hero row loads (legacy data is single-class)", !legacyData.Classes.Any(c => c.IsHero) && DungeonRunValidator.Validate(legacyData, null).Count == 0);
    Check("H2 TagsOf: a hero row with its own tags keeps them, plus its treasureTags", data.TagsOf(mr).SequenceEqual(data.ClassTags["MAGE_ROGUE"]));
}

// ---------------------------------------------------------------- H3 heroRows parse (JSON)
string WithHeroRows(Action<JsonObject> edit)
{
    var node = JsonNode.Parse(runJson).AsObject();
    edit(node);
    return node.ToJsonString();
}
{
    string json = WithHeroRows(n =>
    {
        // Appended to the shipped heroRows (phase 6 F3 on), whose treasures are scoped to their starters.
        var probeRows = JsonNode.Parse(@"[
          {""id"": ""TRLA_209h_Mage"", ""name"": ""Rikkar"", ""kind"": ""hero"", ""adventure"": ""Rumble"", ""hero"": ""TRLA_209h_Mage"", ""health"": 30,
           ""classes"": [""MAGE""], ""powers"": [""CS2_034""], ""decks"": [""rikkar_mage_probe""], ""treasureTags"": [""spells""], ""signatureOffer"": 2},
          {""id"": ""TRLA_209h_Druid"", ""name"": ""Rikkar"", ""kind"": ""hero"", ""adventure"": ""Rumble"", ""hero"": ""TRLA_209h_Druid"", ""health"": 30,
           ""classes"": [""DRUID""], ""powers"": [""CS2_017""], ""decks"": [], ""enabled"": false, ""disabledReason"": ""F14: shrine starters not shipped""}
        ]").AsArray();
        var heroRows = n["heroRows"]?.AsArray() ?? (JsonArray)(n["heroRows"] = new JsonArray());
        // Phase 6 F14 ships Rikkar's forms: the shipped rows with a probe id give way to the probe rows, with their starters.
        var probeIds = probeRows.Select(r => (string)r["id"]).ToHashSet();
        foreach (var shipped in heroRows.Where(r => probeIds.Contains((string)r["id"])).ToList()) heroRows.Remove(shipped);
        foreach (var shippedDeck in n["catalogStarterDecks"].AsArray().Where(k => probeIds.Contains((string)k["class"])).ToList()) n["catalogStarterDecks"].AsArray().Remove(shippedDeck);
        foreach (var r in probeRows.ToList()) { probeRows.Remove(r); heroRows.Add(r); }
        var mageDeck = JsonNode.Parse(runJson)["catalogStarterDecks"][0].AsObject();
        var decks = n["catalogStarterDecks"].AsArray();
        var probe = JsonNode.Parse(@"{""id"": ""rikkar_mage_probe"", ""class"": ""TRLA_209h_Mage"", ""name"": ""Rikkar probe"", ""description"": ""probe"", ""cards"": []}").AsObject();
        // The Mage default starter's cards (admitted Mage cards).
        var mageCards = new JsonArray();
        foreach (string id in data.Deck(data.Class("MAGE").DefaultDeck).Cards) mageCards.Add(id);
        probe["cards"] = mageCards;
        decks.Add(probe);
        n["powerUpgrades"] = JsonNode.Parse(@"[{""from"": ""CS2_034"", ""to"": ""mage_rimebite"", ""on"": ""actClear""}]");
    });
    var d = Load(json);
    var rk = d.FindClass("TRLA_209h_Mage");
    Check("H3 heroRows: an enabled row parses kind, adventure, health, classes, treasureTags and signatureOffer",
        rk != null && rk.IsHero && rk.Adventure == "Rumble" && rk.Health == 30 && rk.ClassSet.SequenceEqual(new[] { "MAGE" }) && rk.TreasureTags.SequenceEqual(new[] { "spells" })
        && rk.SignatureOffer == 2 && rk.Signatures.Count == 0);
    Check("H3 heroRows: a disabled row is kept aside with its reason and validates as disabled", d.DisabledClasses.Any(c => c.Id == "TRLA_209h_Druid" && c.DisabledReason.StartsWith("F14")));
    Check("H3 powerUpgrades parse", d.PowerUpgrades.TryGetValue("CS2_034", out var pu) && pu.To == "mage_rimebite" && pu.On == "actClear");
    var errs = DungeonRunValidator.Validate(d, null);
    Check("H3 the synthetic Rikkar row, its deck and the power upgrade validate", errs.Count == 0, string.Join(" | ", errs.Take(5)));
    Check("H3 a hero row with no classTags of its own reads its primary class's tags plus its treasureTags", d.TagsOf(rk).Contains("spells") && d.ClassTags["MAGE"].All(d.TagsOf(rk).Contains), string.Join(",", d.TagsOf(rk)) + " / " + string.Join(",", d.ClassTags["MAGE"]));
    Throws<InvalidDataException>("H3 a heroRows row without kind hero is refused at parse",
        () => Load(WithHeroRows(n => n["heroRows"] = JsonNode.Parse(@"[{""id"": ""X"", ""name"": ""x"", ""hero"": ""HERO_08"", ""powers"": [""CS2_034""], ""decks"": []}]"))), "must have kind");
    var legacyHero = DungeonRunData.Parse(json, poolJson, npcJson);
    Check("H3 heroRows load only with the catalog", legacyHero.FindClass("TRLA_209h_Mage") == null);
}

// ---------------------------------------------------------------- H4 validator: acceptance and rejection of hero rows
DungeonRunData Probe(Action<DungeonRunData, DungeonRunClass, DungeonRunStarterDeck> edit, string portrait = "TRLA_209h_Mage", string cls = "MAGE")
{
    var d = Load();
    // A shipped hero row with the portrait's id (phase 6 F3 on: GILA_500h3, GILA_600h) gives way to the probe row, with its
    // starters and the treasures scoped to them.
    foreach (var shipped in d.Classes.Where(c => c.Id == portrait).ToList())
    {
        d.Classes.Remove(shipped);
        d.Decks.RemoveAll(k => shipped.Decks.Contains(k.Id));
        d.Treasures.RemoveAll(t => t.Scope.Decks.Count > 0 && t.Scope.Decks.All(shipped.Decks.Contains));
    }
    var row = new DungeonRunClass
    {
        Id = portrait, Name = DungeonRunContent.Get(portrait).Name, HeroCard = portrait, Kind = DungeonRunClass.KindHero, Adventure = "Rumble", Health = 30,
        Classes = new List<string> { cls }, Powers = new List<string> { d.Class(cls).Powers[0] }, Decks = new List<string> { "probe_" + portrait },
    };
    var deck = new DungeonRunStarterDeck { Id = "probe_" + portrait, Class = portrait, Name = "Probe", Description = "probe", Cards = d.Deck(d.Class(cls).DefaultDeck).Cards.ToList() };
    d.Classes.Add(row); d.Decks.Add(deck);
    edit?.Invoke(d, row, deck);
    return d;
}
List<string> Errs(DungeonRunData d, string id) => DungeonRunValidator.Validate(d, null).Where(e => e.Contains(id)).ToList();
{
    // Every portrait as a one-power hero row of its class, with a starter of that class's default cards.
    foreach (string id in portraits)
    {
        string cls = C(id).Class.ToString();
        var e = Errs(Probe(null, id, cls), id);
        Check($"H4 {id} as a {cls} hero row validates", e.Count == 0, string.Join(" | ", e.Take(3)));
    }
    Check("H4 a hero row may hold 1 to 3 powers: 3 validate", Errs(Probe((d, r, k) => { r.Powers = d.Class("MAGE").Powers.ToList(); foreach (var p in r.Powers.Skip(1)) r.PowerUnlocks[p] = d.Class("MAGE").PowerUnlock(p); }), "TRLA_209h_Mage").Count == 0);
    Check("H4 a hero row with 0 powers is refused", Errs(Probe((d, r, k) => r.Powers.Clear()), "TRLA_209h_Mage").Any(e => e.Contains("expected 1 to 3")));
    Check("H4 a hero row with 4 powers is refused", Errs(Probe((d, r, k) => { r.Powers = d.Class("MAGE").Powers.Concat(new[] { "trapper_snare" }).ToList(); foreach (var p in r.Powers.Skip(1)) r.PowerUnlocks[p] = "mage_win"; }), "TRLA_209h_Mage").Any(e => e.Contains("expected 1 to 3")));
    Check("H4 a class row still needs exactly 3 powers", Errs(Probe((d, r, k) => d.Class("MAGE").Powers.RemoveAt(2)), "class MAGE").Any(e => e.Contains("expected 3")));
    Check("H4 a hero row must list its classes", Errs(Probe((d, r, k) => r.Classes.Clear()), "TRLA_209h_Mage").Any(e => e.Contains("must list its classes")));
    Check("H4 a hero row cannot take a class name as its id", Errs(Probe((d, r, k) => { d.Classes.Remove(d.Class("MAGE")); r.Id = "MAGE"; k.Class = "MAGE"; }), "class MAGE").Any(e => e.Contains("class name as its id")));
    Check("H4 a hero row needs a known adventure", Errs(Probe((d, r, k) => r.Adventure = "Naxx"), "TRLA_209h_Mage").Any(e => e.Contains("adventure Naxx")));
    Check("H4 only a hero row may set health or adventure", Errs(Probe((d, r, k) => d.Class("MAGE").Health = 30), "class MAGE").Any(e => e.Contains("only a hero row")));
    Check("H4 a negative health is refused", Errs(Probe((d, r, k) => r.Health = -1), "TRLA_209h_Mage").Any(e => e.Contains("health -1")));
    Check("H4 a portrait of another class is refused", Errs(Probe((d, r, k) => r.HeroCard = "TRLA_209h_Druid"), "TRLA_209h_Mage").Any(e => e.Contains("portrait TRLA_209h_Druid is DRUID")));
    Check("H4 a hero row deck of another class's cards is refused", Errs(Probe((d, r, k) => k.Cards = d.Deck(d.Class("WARRIOR").DefaultDeck).Cards.ToList()), "probe_TRLA_209h_Mage").Any(e => e.Contains("not allowed in MAGE")));
    Check("H4 a hero row's unscripted portrait is refused (an unscripted adventure hero, GILA_900h2)",
        !C("GILA_900h2").Implemented && Errs(Probe((d, r, k) => r.HeroCard = "GILA_900h2", "GILA_500h3", "ROGUE"), "GILA_900h2").Any(e => e.Contains("unscripted portrait")));
    // Disabled rows: allowed for a hero row with a reason, refused without.
    var dis = Load();
    // Phase 6 F14 ships the Druid form: it gives way to the disabled probe row, with its starters.
    foreach (var shipped in dis.Classes.Where(c => c.Id == "TRLA_209h_Druid").ToList()) { dis.Classes.Remove(shipped); dis.Decks.RemoveAll(k => shipped.Decks.Contains(k.Id)); foreach (string key in dis.Unlocks.Keys.Where(k => k.StartsWith("trla_209h_druid_")).ToList()) dis.Unlocks.Remove(key); }
    var gone = new DungeonRunClass { Id = "TRLA_209h_Druid", Name = "Rikkar", HeroCard = "TRLA_209h_Druid", Kind = "hero", Adventure = "Rumble", Classes = new List<string> { "DRUID" }, Enabled = false };
    dis.DisabledClasses.Add(gone);
    Check("H4 a disabled hero row without a reason is refused", DungeonRunValidator.Validate(dis, null).Any(e => e.Contains("TRLA_209h_Druid") && e.Contains("no disabledReason")));
    gone.DisabledReason = "F14: the shrine starters are not shipped";
    Check("H4 a disabled hero row with a reason validates (never as playable)", DungeonRunValidator.Validate(dis, null).Count == 0, string.Join(" | ", DungeonRunValidator.Validate(dis, null).Take(4)));
    var notHero = Load(); notHero.DisabledClasses.Add(new DungeonRunClass { Id = "MAGE2", Name = "x", HeroCard = "HERO_08", Enabled = false, DisabledReason = "x" });
    Check("H4 a disabled single-class class row is still refused", DungeonRunValidator.Validate(notHero, null).Any(e => e.Contains("MAGE2") && e.Contains("may ship disabled")));
    // Passive powers: a hero row may hold a PassivePower content entry (the engine activates it at game start); a class row may not.
    Check("H4 a passive power (shrine_dawn_1) on a hero row passes the passive check", !Errs(Probe((d, r, k) => { r.Powers.Add("shrine_dawn_1"); r.PowerUnlocks["shrine_dawn_1"] = "mage_win"; }), "shrine_dawn_1").Any(e => e.Contains("passive")),
        string.Join(" | ", Errs(Probe((d, r, k) => { r.Powers.Add("shrine_dawn_1"); r.PowerUnlocks["shrine_dawn_1"] = "mage_win"; }), "shrine_dawn_1")));
    Check("H4 a passive power on a class row is refused", Errs(Probe((d, r, k) => { var m = d.Class("MAGE"); m.Powers[2] = "shrine_dawn_1"; m.PowerUnlocks.Clear(); m.PowerUnlocks["carto_survey"] = "mage_floor4"; m.PowerUnlocks["shrine_dawn_1"] = "mage_win"; }), "shrine_dawn_1").Any(e => e.Contains("not a clickable hero power") || e.Contains("passive power")));
    // Choose One: refused on a class row; on a hero row the options are checked.
    var choose = Cards.All.Where(c => c != null && c.Type == CardType.HERO_POWER && c.Implemented && c.ChooseOne && DungeonRunSetup.ChooseOneOptionIds(c).Count == 2).Select(c => c.Id).OrderBy(x => x, StringComparer.Ordinal).FirstOrDefault();
    if (choose == null) Console.WriteLine("NOTE no scripted Choose One hero power resolves two options; the hero-row Choose One branch is exercised by family F9 (Elise_HP1)");
    else
    {
        var classErr = Errs(Probe((d, r, k) => { var m = d.Class("MAGE"); m.Powers[2] = choose; m.PowerUnlocks.Remove("mage_rimebite"); m.PowerUnlocks[choose] = "mage_win"; }), choose);
        var heroErr = Errs(Probe((d, r, k) => { r.Powers.Add(choose); r.PowerUnlocks[choose] = "mage_win"; }), choose);
        Check($"H4 Choose One power {choose}: refused on a class row, its options checked on a hero row", classErr.Any(e => e.Contains("choose-one power (")) && !heroErr.Any(e => e.Contains("choose-one power (")), string.Join(" | ", heroErr));
    }
    Check("H4 ChooseOneOptionIds: a non-Choose One card has none", DungeonRunSetup.ChooseOneOptionIds(C("CS2_034")).Count == 0);
}

// ---------------------------------------------------------------- H5 NewRun and BuildPlan for hero rows, played in real games
{
    int n = 0;
    foreach (string id in portraits)
    {
        string cls = C(id).Class.ToString();
        var d = Probe(null, id, cls);
        var r = new DungeonRun(d);
        foreach (string schedule in new[] { null, DungeonRunSchedules.LegacyId })
        {
            var s = r.NewRun(fresh, id, "probe_" + id, d.Class(cls).Powers[0], 1000 + n, schedule: schedule, signatureId: r.SignatureOffer(fresh, r.Data.FindClass(id), 1000 + n).FirstOrDefault());
            var plan = r.BuildPlan(s);
            Check($"H5 {id} {(schedule ?? "acts")}: plan hero {id}, base Health 30, the row's power, the starter cards",
                plan.PlayerHero.Id == id && plan.PlayerHeroHealth == 30 && plan.PlayerHeroPowerId == d.Class(cls).Powers[0] && plan.PlayerDeck.Select(c => c.Id).SequenceEqual(d.Deck("probe_" + id).Cards)
                && s.Loadout?.Id == id && s.Loadout.Signature.Count == 0);
            var g = StartPlan(plan, 4242 + n);
            Check($"H5 {id} {(schedule ?? "acts")}: real game hero {id} at 30/30 Health with power {DungeonRunSetup.PowerCardId(plan.PlayerHeroPowerId)}",
                g.Player1.Hero.Card.Id == id && g.Player1.Hero.Health == 30 && g.Player1.Hero.BaseHealth == 30 && g.Player1.Hero.HeroPower.Card.Id == DungeonRunSetup.PowerCardId(plan.PlayerHeroPowerId)
                && g.Player1.HeroClass == C(id).Class,
                g.Player1.Hero.Card.Id + " " + g.Player1.Hero.Health + "/" + g.Player1.Hero.BaseHealth + " " + g.Player1.Hero.HeroPower.Card.Id);
            PlayTurns(g, 6);
            Check($"H5 {id} {(schedule ?? "acts")}: six turns play with no fault", g.State == State.RUNNING || g.State == State.COMPLETE, g.State.ToString());
        }
        n++;
    }
    // A treasure's Health bonus adds to the row's Health (Hearty Tonic +10 on a 30 Health hero).
    {
        var d = Probe(null); var r = new DungeonRun(d);
        var s = r.NewRun(fresh, "TRLA_209h_Mage", "probe_TRLA_209h_Mage", "CS2_034", 77, schedule: DungeonRunSchedules.LegacyId, signatureId: r.SignatureOffer(fresh, r.Data.FindClass("TRLA_209h_Mage"), 77).FirstOrDefault());
        s.Treasures.Add("hearty_tonic");
        var g = StartPlan(r.BuildPlan(s), 5);
        Check("H5 a hero row's 30 Health plus Hearty Tonic is 40", g.Player1.Hero.BaseHealth == 40 && g.Player1.Hero.Health == 40, g.Player1.Hero.BaseHealth.ToString());
    }
    // A class row keeps the portrait's own Health (null in the plan), exactly as before phase 6.
    {
        var s = run.NewRun(fresh, "MAGE", data.Class("MAGE").DefaultDeck, "CS2_034", 9, schedule: DungeonRunSchedules.LegacyId, signatureId: run.SignatureOffer(fresh, run.Data.FindClass("MAGE"), 9).FirstOrDefault());
        var plan = run.BuildPlan(s);
        var cfg = new GameConfig(); DungeonRunSetup.Configure(cfg, plan);
        Check("H5 a class row: no PlayerHeroHealth, Player1HeroHealth stays null, no Loadout", plan.PlayerHeroHealth == null && cfg.Player1HeroHealth == null && s.Loadout == null);
    }
    // The shipped Tombs row MAGE_ROGUE.
    {
        var s = run.NewRun(fresh, "MAGE_ROGUE", "tombs_reno_spellslinger", "ULDA_Reno_HP1", 31337, signatureId: run.SignatureOffer(fresh, run.Data.FindClass("MAGE_ROGUE"), 31337).FirstOrDefault()); // phase 6 F9: Reno's own powers
        var plan = run.BuildPlan(s);
        var g = StartPlan(plan, 12);
        Check("H5 MAGE_ROGUE: ULDA_Reno at 30 Health in a real game; Loadout MAGE_ROGUE; class MAGE",
            g.Player1.Hero.Card.Id == "ULDA_Reno" && g.Player1.Hero.Health == 30 && s.Loadout?.Id == "MAGE_ROGUE" && g.Player1.HeroClass == CardClass.MAGE);
    }
}

// ---------------------------------------------------------------- H6 generated starters (random-class, random-dual)
{
    var d = Probe((dd, r, k) =>
    {
        k.Cards.Clear(); k.Generator = DungeonRunStarterDeck.GeneratorRandomClass; k.DisplayCard = "DALA_804";
    });
    var e = Errs(d, "probe_TRLA_209h_Mage");
    Check("H6 a random-class generated deck validates (pool big enough, display card known)", e.Count == 0, string.Join(" | ", e.Take(3)));
    var r = new DungeonRun(d);
    var row = d.Class("TRLA_209h_Mage");
    var pool = r.GeneratorPool(row, "MAGE");
    var a = r.NewRun(fresh, "TRLA_209h_Mage", "probe_TRLA_209h_Mage", "CS2_034", 555, signatureId: r.SignatureOffer(fresh, r.Data.FindClass("TRLA_209h_Mage"), 555).FirstOrDefault());
    var b = r.NewRun(fresh, "TRLA_209h_Mage", "probe_TRLA_209h_Mage", "CS2_034", 555, signatureId: r.SignatureOffer(fresh, r.Data.FindClass("TRLA_209h_Mage"), 555).FirstOrDefault());
    var c = r.NewRun(fresh, "TRLA_209h_Mage", "probe_TRLA_209h_Mage", "CS2_034", 556, signatureId: r.SignatureOffer(fresh, r.Data.FindClass("TRLA_209h_Mage"), 556).FirstOrDefault());
    Check("H6 random-class: 10 distinct Mage pool cards, recorded as starter cards", a.Deck.Count == 10 && a.Deck.Distinct().Count() == 10 && a.Deck.All(pool.Contains)
        && a.Cards.All(x => x.Origin == DungeonRunDeck.OriginStarter) && a.Deck.All(id => C(id).Class == CardClass.MAGE), string.Join(",", a.Deck));
    Check("H6 random-class: the same seed repeats, another seed differs", a.Deck.SequenceEqual(b.Deck) && !a.Deck.SequenceEqual(c.Deck));
    Check("H6 the display card never enters the deck", !a.Deck.Contains("DALA_804"));
    var legacyRun = r.NewRun(fresh, "TRLA_209h_Mage", "probe_TRLA_209h_Mage", "CS2_034", 555, schedule: DungeonRunSchedules.LegacyId, signatureId: r.SignatureOffer(fresh, r.Data.FindClass("TRLA_209h_Mage"), 555).FirstOrDefault());
    Check("H6 a legacy-schedule run of the same seed draws the same starter", legacyRun.Deck.SequenceEqual(a.Deck));
    var plan = r.BuildPlan(a); var g = StartPlan(plan, 3);
    Check("H6 a generated starter plays in a real game", g.Player1.DeckZone.Count + g.Player1.HandZone.Count >= 10 && g.State == State.RUNNING);

    // random-dual on the Tombs row: phase 6 F9 ships it (tombs_reno_random, display card ULDA_901).
    var dual = Load();
    var mr = dual.Class("MAGE_ROGUE");
    Check("H6 MAGE_ROGUE ships its random-dual Random deck (phase 6 F9)", mr.Decks.Contains("tombs_reno_random") && dual.FindDeck("tombs_reno_random")?.Generator == DungeonRunStarterDeck.GeneratorRandomDual
        && dual.FindDeck("tombs_reno_random").DisplayCard == "ULDA_901");
    var de = Errs(dual, "tombs_reno_random");
    Check("H6 a random-dual generated deck on MAGE_ROGUE validates", de.Count == 0, string.Join(" | ", de.Take(3)));
    var dr = new DungeonRun(dual);
    var ds = dr.NewRun(all, "MAGE_ROGUE", "tombs_reno_random", "ULDA_Reno_HP1", 999, signatureId: dr.SignatureOffer(all, dr.Data.FindClass("MAGE_ROGUE"), 999).FirstOrDefault());
    // Since F11 the run deck also holds the picked signature (origin signature): the generated starter is the starter records.
    var dst = ds.Cards.Where(c => c.Origin == DungeonRunDeck.OriginStarter).Select(c => c.Id).ToList();
    Check("H6 random-dual: 5 Mage and 5 Rogue cards, distinct", dst.Count(id => C(id).Class == CardClass.MAGE) == 5 && dst.Count(id => C(id).Class == CardClass.ROGUE) == 5 && dst.Distinct().Count() == 10,
        string.Join(",", ds.Deck));
    // Rejections.
    Check("H6 a generated deck that lists cards is refused", Errs(Probe((dd, rr, k) => { k.Generator = DungeonRunStarterDeck.GeneratorRandomClass; k.DisplayCard = "DALA_804"; }), "probe_TRLA_209h_Mage").Any(x => x.Contains("lists cards")));
    Check("H6 an unknown generator is refused", Errs(Probe((dd, rr, k) => { k.Cards.Clear(); k.Generator = "random-any"; k.DisplayCard = "DALA_804"; }), "probe_TRLA_209h_Mage").Any(x => x.Contains("unknown generator")));
    Check("H6 random-dual on a single-class row is refused", Errs(Probe((dd, rr, k) => { k.Cards.Clear(); k.Generator = DungeonRunStarterDeck.GeneratorRandomDual; k.DisplayCard = "DALA_804"; }), "probe_TRLA_209h_Mage").Any(x => x.Contains("needs a dual-class row")));
    Check("H6 a generated deck without a display card is refused", Errs(Probe((dd, rr, k) => { k.Cards.Clear(); k.Generator = DungeonRunStarterDeck.GeneratorRandomClass; }), "probe_TRLA_209h_Mage").Any(x => x.Contains("needs a displayCard")));
    Check("H6 a class row may not have a generated deck", Errs(Probe((dd, rr, k) => { var mdeck = dd.Deck(dd.Class("MAGE").Decks[1]); mdeck.Cards.Clear(); mdeck.Generator = DungeonRunStarterDeck.GeneratorRandomClass; mdeck.DisplayCard = "DALA_804"; }), "class MAGE").Any(x => x.Contains("only a hero row may have one")));
}

// ---------------------------------------------------------------- H7 signatures and the Jr./Sr. profile upgrade
DungeonRunData SigData(out DungeonRunClass row)
{
    var d = Probe(null);
    row = d.Class("TRLA_209h_Mage");
    // The legacy Cartographer signatures stand in as signature treasures (scripted active treasure content), plus a Jr. card
    // (charm_seedling) whose Sr. (charm_sprouting) needs an unlock, and a locked signature (hunting_net).
    foreach (string id in new[] { "sig_field_atlas", "sig_smoke_flask", "sig_star_chart", "charm_seedling", "hunting_net" })
    {
        var t = d.Treasure(id);
        t.Legacy = false; t.Offered = false; t.Signature = true;
        t.Scope = new DungeonRunScope { Classes = new List<string> { row.Id } };
        row.Signatures.Add(id);
    }
    d.Treasure("charm_seedling").UpgradeUnlock = "mage_win";
    d.Treasure("hunting_net").Unlock = "mage_floor4";
    // charm_sprouting stays a draft treasure in the data; as charm_seedling's Sr. it is reached through the loadout.
    return d;
}
{
    var d = SigData(out var row);
    var e = DungeonRunValidator.Validate(d, null).Where(x => x.Contains("TRLA_209h_Mage") || x.Contains("sig_") || x.Contains("charm_") || x.Contains("hunting_net")).ToList();
    Check("H7 the signature fixture validates", e.Count == 0, string.Join(" | ", e.Take(4)));
    var r = new DungeonRun(d);
    var offerA = r.SignatureOffer(fresh, row, 42); var offerB = r.SignatureOffer(fresh, row, 42);
    Check("H7 the offer is 3 of the unlocked signatures, repeatable by seed", offerA.Count == 3 && offerA.SequenceEqual(offerB) && !offerA.Contains("hunting_net") && offerA.Distinct().Count() == 3, string.Join(",", offerA));
    var seen = new HashSet<string>();
    for (long seed = 1; seed <= 40; seed++) foreach (string s in r.SignatureOffer(fresh, row, seed)) seen.Add(s);
    Check("H7 across seeds the offer covers every unlocked signature and never the locked one", seen.SetEquals(new[] { "sig_field_atlas", "sig_smoke_flask", "sig_star_chart", "charm_seedling" }), string.Join(",", seen));
    var unlocked = new DungeonRunProfile(); unlocked.Unlocked.Add("mage_floor4");
    Check("H7 an unlocked signature joins the pool", Enumerable.Range(1, 40).Any(i => r.SignatureOffer(unlocked, row, i).Contains("hunting_net")));
    row.SignatureOffer = 2;
    Check("H7 signatureOffer sets the offer size", r.SignatureOffer(fresh, row, 42).Count == 2);
    row.SignatureOffer = 3;
    Throws<InvalidOperationException>("H7 a row with signatures needs one at NewRun", () => r.NewRun(fresh, row.Id, "probe_TRLA_209h_Mage", "CS2_034", 42), DungeonRun.ChooseSignature);
    string notOffered = new[] { "sig_field_atlas", "sig_smoke_flask", "sig_star_chart", "charm_seedling" }.First(x => !offerA.Contains(x));
    Throws<InvalidOperationException>("H7 a signature not on this seed's offer is refused", () => r.NewRun(fresh, row.Id, "probe_TRLA_209h_Mage", "CS2_034", 42, signatureId: notOffered), DungeonRun.NotOffered);
    Throws<InvalidOperationException>("H7 a signature on a row without signatures is refused", () => run.NewRun(fresh, "MAGE", data.Class("MAGE").DefaultDeck, "CS2_034", 42, signatureId: "sig_field_atlas"), DungeonRun.NotOffered);
    string pick = offerA[0];
    var st = r.NewRun(fresh, row.Id, "probe_TRLA_209h_Mage", "CS2_034", 42, signatureId: pick);
    string shell = DungeonRunContent.Get(pick).ShellId;
    var sigCard = st.Cards.Single(c => c.Origin == DungeonRunDeck.OriginSignature);
    Check("H7 the picked signature: Loadout {row, [sig]}, its card recorded with origin signature, held as an active treasure",
        st.Loadout.Id == row.Id && st.Loadout.Signature.SequenceEqual(new[] { pick }) && sigCard.Id == shell && st.ActiveTreasures.Contains(pick) && st.Deck.Count == 11);
    Check("H7 a signature card is never a deck change's target (as a treasure shell)", DungeonRunDeck.Remove(r, st, sigCard.Uid).Error == DungeonRunDeck.ErrTreasureShell
        && DungeonRunDeck.Copy(r, st, sigCard.Uid, 1).Error == DungeonRunDeck.ErrTreasureShell);
    var before = st.Deck.Count;
    Check("H7 a remove-where never removes a signature card", DungeonRunDeck.RemoveWhere(r, st, c => c.Uid == sigCard.Uid).Ok && st.Deck.Count == before);
    // Draft treasure offers never include a signature.
    Check("H7 a treasure draft never offers a signature", Enumerable.Range(0, 30).All(i =>
    {
        var s2 = r.NewRun(fresh, row.Id, "probe_TRLA_209h_Mage", "CS2_034", 100 + i, signatureId: r.SignatureOffer(fresh, row, 100 + i)[0]);
        s2.Phase = DungeonRunPhase.PreMatch;
        return !r.OpenTreasureDraft(s2) || !s2.OpenDraft.Offered.Any(o => d.Treasure(o).Signature);
    }));
    // Jr./Sr.: charm_seedling starts as charm_sprouting once mage_win holds; never upgrades in the run.
    long jr = Enumerable.Range(1, 200).Select(i => (long)i).First(i => r.SignatureOffer(fresh, row, i).Contains("charm_seedling"));
    var junior = r.NewRun(fresh, row.Id, "probe_TRLA_209h_Mage", "CS2_034", jr, signatureId: "charm_seedling");
    var winner = new DungeonRunProfile(); winner.Unlocked.Add("mage_win");
    var senior = r.NewRun(winner, row.Id, "probe_TRLA_209h_Mage", "CS2_034", jr, signatureId: "charm_seedling");
    Check("H7 Jr.: without the unlock the run starts with charm_seedling and no upgrade record",
        junior.Loadout.Signature.SequenceEqual(new[] { "charm_seedling" }) && junior.Deck.Contains(DungeonRunContent.Get("charm_seedling").ShellId) && junior.Upgrades.Count == 0);
    Check("H7 Sr.: with the unlock the run starts with charm_sprouting (the Sr. stage), recorded as the signature",
        senior.Loadout.Signature.SequenceEqual(new[] { "charm_sprouting" }) && senior.Deck.Contains(DungeonRunContent.Get("charm_sprouting").ShellId) && senior.ActiveTreasures.Contains("charm_sprouting")
        && r.SignatureCard(winner, "charm_seedling") == "charm_sprouting" && r.SignatureCard(fresh, "charm_seedling") == "charm_seedling");
    // Validator rejections of signature data.
    var bad = SigData(out var badRow); bad.Treasure("sig_star_chart").Signature = false;
    Check("H7 a listed signature that is not a signature treasure is refused", Errs(bad, "TRLA_209h_Mage").Any(x => x.Contains("is not a signature treasure")));
    bad = SigData(out badRow); bad.Treasure("sig_star_chart").Scope.Classes = new List<string> { "MAGE" };
    Check("H7 a signature scoped to another row is refused", Errs(bad, "TRLA_209h_Mage").Any(x => x.Contains("must be scoped to [TRLA_209h_Mage] alone")));
    bad = SigData(out badRow); bad.Treasure("sig_star_chart").Offered = true;
    Check("H7 an offered signature is refused", DungeonRunValidator.Validate(bad, null).Any(x => x.StartsWith("sig_star_chart:") && x.Contains("loadout only")));
    bad = SigData(out badRow); badRow.SignatureOffer = 9;
    Check("H7 a signatureOffer above the signature count is refused", Errs(bad, "TRLA_209h_Mage").Any(x => x.Contains("signatureOffer 9")));
    bad = SigData(out badRow); bad.Treasure("charm_seedling").UpgradeUnlock = "no_such_unlock";
    Check("H7 an unknown upgradeUnlock is refused", DungeonRunValidator.Validate(bad, null).Any(x => x.StartsWith("charm_seedling:") && x.Contains("unknown upgradeUnlock")));
    bad = SigData(out badRow); var orphan = bad.Treasure("sig_star_chart"); badRow.Signatures.Remove("sig_star_chart");
    Check("H7 a signature treasure no hero row lists is refused", DungeonRunValidator.Validate(bad, null).Any(x => x.StartsWith("sig_star_chart:") && x.Contains("no hero row lists")));
    var classSig = Load(); classSig.Class("MAGE").Signatures.Add("sig_star_chart");
    Check("H7 only a hero row may list signatures", DungeonRunValidator.Validate(classSig, null).Any(x => x.Contains("class MAGE") && x.Contains("only a hero row")));
}

// ---------------------------------------------------------------- H8 picker data lists
{
    var classes = DungeonRunCatalog.Classes(run, fresh);
    Check("H8 Classes lists the eleven class rows only (no hero row)", classes.Count == 11 && !classes.Any(c => c.Id == "MAGE_ROGUE"), string.Join(",", classes.Select(c => c.Id)));
    var mage = DungeonRunCatalog.Heroes(run, fresh, "MAGE");
    // Phase 6 F4 adds Toki (GILA_900h, Monster Hunt, MAGE) after the dual row; F6 adds Rakanishu (DALA_Rakanishu, Heist) after Toki.
    // Phase 6 F14 adds Rikkar's Mage form (TRLA_209h_Mage, Rumble) after Rakanishu.
    Check("H8 Heroes(MAGE): the class row first, then MAGE_ROGUE (Tombs, Mage / Rogue, 30 Health, dual), then Toki (GILA_900h, MonsterHunt), then Rakanishu (DALA_Rakanishu, Heist), then Rikkar (TRLA_209h_Mage, Rumble)",
        mage.Count == 5 && mage[4].Id == "TRLA_209h_Mage" && mage[4].IsHero && !mage[4].IsDual && mage[4].Adventure == "Rumble" && mage[4].Health == 30 && mage[3].Id == "DALA_Rakanishu" && mage[3].IsHero && mage[3].Adventure == "Heist" && mage[3].Health == 30 && mage[2].Id == "GILA_900h" && mage[2].IsHero && !mage[2].IsDual && mage[2].Adventure == "MonsterHunt" && mage[2].Health == 30 && mage[0].Id == "MAGE" && !mage[0].IsHero && mage[0].Adventure == null && mage[1].Id == "MAGE_ROGUE" && mage[1].IsHero && mage[1].IsDual
        && mage[1].Adventure == "Tombs" && mage[1].Health == 30 && mage[1].ClassName == "Mage / Rogue" && mage[1].HeroCardId == "ULDA_Reno" && mage[0].Health == C(data.Class("MAGE").HeroCard).Health,
        string.Join(" | ", mage.Select(h => h.Id + "/" + h.Health + "/" + h.ClassName)));
    // Phase 6 F3 on: a single-class hero row of Rogue (Tess, GILA_500h3) sits under Rogue, after the class row.
    var rogueHeroes = data.Classes.Where(c => c.IsHero && !c.IsDual && c.ClassSet[0] == "ROGUE").Select(c => c.Id);
    Check("H8 Heroes(ROGUE): a dual row sits under its primary class only (the single-class Rogue hero rows follow the class row)",
        DungeonRunCatalog.Heroes(run, fresh, "ROGUE").Select(h => h.Id).SequenceEqual(new[] { "ROGUE" }.Concat(rogueHeroes)),
        string.Join(",", DungeonRunCatalog.Heroes(run, fresh, "ROGUE").Select(h => h.Id)));
    Check("H8 Heroes of an unknown class or a hero row id is empty", DungeonRunCatalog.Heroes(run, fresh, "NOPE").Count == 0 && DungeonRunCatalog.Heroes(run, fresh, "MAGE_ROGUE").Count == 0);
    Check("H8 Decks and Powers of a hero row id work as for a class row", DungeonRunCatalog.Decks(run, fresh, "MAGE_ROGUE").Count == 4 && DungeonRunCatalog.Powers(run, fresh, "MAGE_ROGUE").Count == 3);
    var transport = new Func<string, string>(id => id == "ULDA_Reno" ? "ULDA_Reno_x" : id);
    Check("H8 Heroes maps the portrait through the client id map", DungeonRunCatalog.Heroes(run, fresh, "MAGE", transport)[1].ClientHeroCardId == "ULDA_Reno_x");
    var d = SigData(out var row); var r = new DungeonRun(d);
    var sigs = DungeonRunCatalog.Signatures(r, fresh, row.Id, 42);
    Check("H8 Signatures: the seed's offer with names, text and the card a run gets", sigs.Select(s => s.Id).SequenceEqual(r.SignatureOffer(fresh, row, 42))
        && sigs.All(s => s.CardId == DungeonRunContent.Get(s.StartsAs).ShellId && s.Name == DungeonRunContent.Get(s.StartsAs).Name));
    Check("H8 RowClassName names a hero row by its classes", DungeonRunCatalog.RowClassName(row) == "Mage" && DungeonRunCatalog.RowClassName(data.Class("MAGE_ROGUE")) == "Mage / Rogue");
    Check("H8 ClassName returns a hero row id unchanged (never 'Dala / Rakanishu')", NativeDungeonRunText.ClassName("DALA_Rakanishu") == "DALA_Rakanishu" && NativeDungeonRunText.ClassName("MAGE_ROGUE") == "Mage / Rogue");
    Check("H8 AvailableHeroes(MAGE) mirrors Heroes", run.AvailableHeroes("MAGE", fresh).Select(c => c.Id).SequenceEqual(new[] { "MAGE", "MAGE_ROGUE", "GILA_900h", "DALA_Rakanishu", "TRLA_209h_Mage" }));
}

// ---------------------------------------------------------------- H9 a passive treasure keyed by its real id (display shell SPELL)
{
    List<string> WithContent(params RunContent[] extra) => DungeonRunValidator.Validate(data, null, DungeonRunContent.All.Concat(extra).ToList());
    var ok = WithContent(new RunContent { Key = "CS2_029", Kind = RunContentKind.PassiveTreasure, ShellId = "CS2_029", Name = "probe", Text = "probe" });
    Check("H9 a passive treasure whose shell is its own id may be a SPELL", !ok.Any(e => e.Contains("CS2_029")), string.Join(" | ", ok.Where(e => e.Contains("CS2_029"))));
    var other = WithContent(new RunContent { Key = "probe_passive", Kind = RunContentKind.PassiveTreasure, ShellId = "CS2_029", Name = "probe", Text = "probe" });
    Check("H9 a passive treasure may not borrow another id as its shell", other.Any(e => e.Contains("probe_passive") && e.Contains("must be its own id")));
    var minion = WithContent(new RunContent { Key = "CS2_182", Kind = RunContentKind.PassiveTreasure, ShellId = "CS2_182", Name = "probe", Text = "probe" });
    Check("H9 a passive treasure's own shell must be a SPELL", minion.Any(e => e.StartsWith("CS2_182:") && e.Contains("needs SPELL")));
    var hero = WithContent(new RunContent { Key = "HERO_01", Kind = RunContentKind.Hero, ShellId = "HERO_01", Name = "probe", Text = null, Health = 0 });
    Check("H9 Hero content needs a run Health", hero.Any(e => e.StartsWith("HERO_01:") && e.Contains("no run Health")));
}

Console.WriteLine($"c-heroes: {passes} passed, {failures} failed");
return failures == 0 ? 0 : 1;
