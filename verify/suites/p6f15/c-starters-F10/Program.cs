// c-starters-F10: phase 6 family F10 of the unified Dungeon Run, the adventure starter-deck collectibles: the 28 collectible
// cards (ULD_145 ... TRL_535) that were the last unscripted cards of the authentic starters, so that the Tombs of Terror
// explorer rows PALADIN_SHAMAN, DRUID_PRIEST and HUNTER_WARRIOR ship enabled with ULDA_<Explorer>_Deck1-3, MAGE_ROGUE gains
// ULDA_Reno_Deck2-3, Ol' Barkeye gains DALA_Hunter_Deck3, and the Rumble Run shrine starters TRLA_106/109/112/155 pass the
// pool checks (their Rikkar rows are family F14). For every id: the engine script (Implemented, as the client prints it), the
// admission verdict (both sides, or the row it has), the run data, and a scripted regression game through the run setup path
// (DungeonRun.NewRun on the row and the starter that holds the card -> BuildPlan -> DungeonRunSetup.Configure -> Game ->
// ApplyAfterStart -> mulligan -> MainBegin -> the card played); then full games against the real boss AI on every new
// starter (the player side), and boss games whose deck is the F10 cards (the boss AI plays them), which must end without a
// fault or an AI fallback. Headless: compiles C:\SoloStone\plugin\DungeonRun\*.cs and the NPC AI against the engine build.
// Run it twice and compare.
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
DungeonRunData Load(string json) => DungeonRunData.Parse(json, poolJson, npcJson, catalogJson, scheduleJson, tavernJson);
var data = Load(runJson);
var run = new DungeonRun(data);
var all = new DungeonRunProfile(); all.Unlocked.AddRange(data.Unlocks.Keys);
var facts = System.Text.Json.JsonDocument.Parse(File.ReadAllText(VerifyPaths.Repo("facts/violet-hold-2026-09.json")));
var factById = new Dictionary<string, System.Text.Json.JsonElement>(StringComparer.Ordinal);
foreach (var c in facts.RootElement.GetProperty("cards").EnumerateArray()) factById[c.GetProperty("id").GetString()] = c;
foreach (var c in facts.RootElement.GetProperty("overlay").GetProperty("cards").EnumerateArray()) factById[c.GetProperty("id").GetString()] = c;
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
var clientDeckId = new Dictionary<string, int>(StringComparer.Ordinal);
foreach (var r in tables.GetProperty("DECK").EnumerateArray()) clientDeckId[r.GetProperty("noteName").GetString()] = r.GetProperty("ID").GetInt32();
var clientDecks = new Dictionary<int, List<string>>();
foreach (var r in tables.GetProperty("DECK_CARD").EnumerateArray())
{
    int d = r.GetProperty("deckId").GetInt32();
    if (!clientDecks.TryGetValue(d, out var l)) clientDecks[d] = l = new List<string>();
    l.Add(r.GetProperty("cardId_card").GetString());
}
List<string> ClientDeck(string note) => clientDecks[clientDeckId[note]];
CardType FactType(string id) => FactTag(id, 202) switch { 3 => CardType.HERO, 4 => CardType.MINION, 5 => CardType.SPELL, 6 => CardType.ENCHANTMENT, 7 => CardType.WEAPON, 10 => CardType.HERO_POWER, _ => CardType.INVALID };
string ClassOfTag(int v) => v switch { 2 => "DRUID", 3 => "HUNTER", 4 => "MAGE", 5 => "PALADIN", 6 => "PRIEST", 7 => "ROGUE", 8 => "SHAMAN", 9 => "WARLOCK", 10 => "WARRIOR", 12 => "NEUTRAL", _ => "?" + v };
var transport = new Dictionary<string, string>
{
    ["CS2_034"] = "HERO_08bp", ["CS2_102"] = "HERO_01bp", ["CS1h_001"] = "HERO_09bp", ["CS2_101"] = "HERO_04bp", ["CS2_017"] = "HERO_06bp",
    ["DS1h_292"] = "HERO_05bp", ["CS2_083b"] = "HERO_03bp", ["CS2_056"] = "HERO_07bp", ["CS2_049"] = "HERO_02bp",
    ["AT_132_WARRIOR"] = "HERO_01bp2", ["AT_132_SHAMAN"] = "HERO_02bp2", ["AT_132_ROGUE"] = "HERO_03bp2", ["AT_132_PALADIN"] = "HERO_04bp2",
    ["AT_132_HUNTER"] = "HERO_05bp2", ["AT_132_DRUID"] = "HERO_06bp2", ["AT_132_WARLOCK"] = "HERO_07bp2", ["AT_132_MAGE"] = "HERO_08bp2", ["AT_132_PRIEST"] = "HERO_09bp2",
};
bool ClientKnows(string id) => clientIds.Contains(transport.TryGetValue(id, out var sent) ? sent : id);

// ---------------------------------------------------------------- the family
string[] f10 = "ULD_145 ULD_439 ULD_207 ULD_275 ULD_196 ULD_265 ULD_134 ULD_185 ULD_250 ULD_136 ULD_188 ULD_266 ULD_269 ULD_309 ULD_174 ULD_430 GVG_046 ULD_429 ULD_231 ULD_328 ULD_186 ULD_240 ULD_285 DAL_377 NEW1_016 LOE_046 TRL_522 TRL_535".Split(' ');
string[] tokens = { "ULD_439t", "ULD_215t", "ULD_174t", "ULD_430t", "ULD_134t" };
// The new starters: deck id, row, client deck, unlock, and the reprint resolutions the run data carries.
var starters = new (string Deck, string Row, string Client, string Unlock, Dictionary<string, string> Reprints)[]
{
    ("tombs_reno_spell_seeker", "MAGE_ROGUE", "ULDA_Reno_Deck2", "mage_rogue_floor3", new Dictionary<string, string>()),
    ("tombs_reno_secret_traps", "MAGE_ROGUE", "ULDA_Reno_Deck3", "mage_rogue_floor5", new Dictionary<string, string>()),
    ("tombs_finley_murloc_crusade", "PALADIN_SHAMAN", "ULDA_Finley_Deck1", null, new Dictionary<string, string> { ["ULD_723"] = "CORE_ULD_723" }),
    ("tombs_finley_mighty_morphing", "PALADIN_SHAMAN", "ULDA_Finley_Deck2", "paladin_shaman_floor3", new Dictionary<string, string>()),
    ("tombs_finley_back_in_action", "PALADIN_SHAMAN", "ULDA_Finley_Deck3", "paladin_shaman_floor5", new Dictionary<string, string>()),
    ("tombs_elise_healing_help", "DRUID_PRIEST", "ULDA_Elise_Deck1", null, new Dictionary<string, string> { ["ULD_271"] = "CORE_ULD_271", ["EX1_154"] = "CORE_EX1_154" }),
    ("tombs_elise_swarms", "DRUID_PRIEST", "ULDA_Elise_Deck2", "druid_priest_floor3", new Dictionary<string, string> { ["EX1_160"] = "CORE_EX1_160" }),
    ("tombs_elise_insight", "DRUID_PRIEST", "ULDA_Elise_Deck3", "druid_priest_floor5", new Dictionary<string, string>()),
    ("tombs_brann_taming_dinos", "HUNTER_WARRIOR", "ULDA_Brann_Deck1", null, new Dictionary<string, string>()),
    ("tombs_brann_beastly_bodyguards", "HUNTER_WARRIOR", "ULDA_Brann_Deck2", "hunter_warrior_floor3", new Dictionary<string, string>()),
    ("tombs_brann_call_for_help", "HUNTER_WARRIOR", "ULDA_Brann_Deck3", "hunter_warrior_floor5", new Dictionary<string, string>()),
    ("heist_hunter_3", "DALA_Barkeye", "DALA_Hunter_Deck3", "dala_barkeye_floor5", new Dictionary<string, string>()),
};
// The Rumble Run shrine starters (Rikkar's rows are family F14): their class and reprint resolutions.
var rumble = new (string Client, string Class, Dictionary<string, string> Reprints)[]
{
    ("TRLA_106_Rogue_Player", "ROGUE", new Dictionary<string, string>()),
    ("TRLA_109_Shaman_Player", "SHAMAN", new Dictionary<string, string>()),
    ("TRLA_112_Druid_Player", "DRUID", new Dictionary<string, string> { ["EX1_154"] = "CORE_EX1_154" }),
    ("TRLA_155_Shaman_Player", "SHAMAN", new Dictionary<string, string>()),
};
// Where each card's regression game starts: the row and starter that hold it (the four Rumble cards ride a Heist hero row
// of their class, or a neutral one, until F14 adds Rikkar's rows).
var home = new Dictionary<string, (string Row, string Deck)>
{
    ["ULD_145"] = ("PALADIN_SHAMAN", "tombs_finley_murloc_crusade"), ["ULD_439"] = ("PALADIN_SHAMAN", "tombs_finley_mighty_morphing"),
    ["ULD_207"] = ("PALADIN_SHAMAN", "tombs_finley_back_in_action"), ["ULD_275"] = ("PALADIN_SHAMAN", "tombs_finley_back_in_action"),
    ["ULD_265"] = ("DRUID_PRIEST", "tombs_elise_healing_help"), ["ULD_196"] = ("DRUID_PRIEST", "tombs_elise_healing_help"),
    ["ULD_185"] = ("DRUID_PRIEST", "tombs_elise_swarms"), ["ULD_134"] = ("DRUID_PRIEST", "tombs_elise_swarms"), ["ULD_250"] = ("DRUID_PRIEST", "tombs_elise_swarms"),
    ["ULD_136"] = ("DRUID_PRIEST", "tombs_elise_insight"), ["ULD_309"] = ("DRUID_PRIEST", "tombs_elise_insight"), ["ULD_188"] = ("DRUID_PRIEST", "tombs_elise_insight"),
    ["ULD_269"] = ("DRUID_PRIEST", "tombs_elise_insight"), ["ULD_266"] = ("DRUID_PRIEST", "tombs_elise_insight"),
    ["ULD_174"] = ("HUNTER_WARRIOR", "tombs_brann_taming_dinos"), ["ULD_430"] = ("HUNTER_WARRIOR", "tombs_brann_taming_dinos"),
    ["GVG_046"] = ("HUNTER_WARRIOR", "tombs_brann_beastly_bodyguards"), ["ULD_429"] = ("HUNTER_WARRIOR", "tombs_brann_call_for_help"),
    ["ULD_328"] = ("MAGE_ROGUE", "tombs_reno_spell_seeker"), ["ULD_231"] = ("MAGE_ROGUE", "tombs_reno_spell_seeker"),
    ["ULD_186"] = ("MAGE_ROGUE", "tombs_reno_secret_traps"), ["ULD_240"] = ("MAGE_ROGUE", "tombs_reno_secret_traps"), ["ULD_285"] = ("MAGE_ROGUE", "tombs_reno_secret_traps"),
    ["DAL_377"] = ("DALA_Barkeye", "heist_hunter_3"),
    ["NEW1_016"] = ("DALA_Eudora", "heist_rogue_1"), ["LOE_046"] = ("DALA_Vessina", "heist_shaman_1"), ["TRL_522"] = ("DALA_Vessina", "heist_shaman_1"),
    ["TRL_535"] = ("DALA_Squeamlish", "heist_druid_1"),
};
List<string> Resolved(List<string> client, Dictionary<string, string> reprints) => client.Select(id => reprints.TryGetValue(id, out var to) ? to : id).ToList();

// ---------------------------------------------------------------- T1 content
Check("T1 28 F10 ids, each with a regression game home", f10.Length == 28 && f10.Distinct().Count() == 28 && f10.All(home.ContainsKey));
foreach (string id in f10.Concat(tokens))
{
    var card = C(id);
    bool minion = card.Type == CardType.MINION;
    Check($"T1 {id} ({card.Name}): scripted (Implemented), the client's {FactType(id)} of class {ClassOfTag(FactTag(id, 199))}, cost {FactTag(id, 48)}"
        + (minion ? $", {FactTag(id, 47)}/{FactTag(id, 45)}" : ""),
        card.Implemented && clientIds.Contains(id) && card.Type == FactType(id) && card.Class.ToString() == ClassOfTag(FactTag(id, 199)) && card.Cost == FactTag(id, 48)
        && (!minion || card.ATK == FactTag(id, 47) && card.Health == FactTag(id, 45)) && (card.Type != CardType.WEAPON || card.ATK == FactTag(id, 47)),
        $"{card.Implemented} {card.Type} {card.Class} {card.Cost} {card.ATK}/{card.Health}");
}
Check("T1 the collectibles are collectible and no CORE_ reprint of them exists (the run data lists them by their own id)",
    f10.All(id => C(id).Collectible && !clientIds.Contains("CORE_" + id)));
Check("T1 keyword cards carry their printed keywords: Ancestral Guardian Lifesteal + Reborn, Bone Wraith Taunt + Reborn, Temple Berserker and Grandmummy Reborn, King of Beasts and Infested Goblin Taunt, the Scarab token Taunt, the Locust Rush",
    C("ULD_207")[GameTag.LIFESTEAL] == 1 && C("ULD_207")[GameTag.REBORN] == 1 && C("ULD_275")[GameTag.TAUNT] == 1 && C("ULD_275")[GameTag.REBORN] == 1
    && C("ULD_185")[GameTag.REBORN] == 1 && C("ULD_266")[GameTag.REBORN] == 1 && C("GVG_046")[GameTag.TAUNT] == 1 && C("ULD_250")[GameTag.TAUNT] == 1
    && C("ULD_215t")[GameTag.TAUNT] == 1 && C("ULD_430t")[GameTag.RUSH] == 1);

// ---------------------------------------------------------------- T2 data, validator, admission
{
    var errors = DungeonRunValidator.Validate(data, ClientKnows);
    Check("T2 shipped data validates with 0 errors (client check through the transported id)", errors.Count == 0, string.Join(" | ", errors.Take(5)));
    Check("T2 no catalog row ships disabled: PALADIN_SHAMAN, DRUID_PRIEST and HUNTER_WARRIOR are playable hero rows", data.DisabledClasses.Count == 0 && data.DisabledDecks.Count == 0
        && new[] { "PALADIN_SHAMAN", "DRUID_PRIEST", "HUNTER_WARRIOR" }.All(r => data.FindClass(r) is DungeonRunClass c && c.IsHero && c.IsDual && c.Enabled && c.DisabledReason == null));
    foreach (var (row, decks) in new[]
    {
        ("MAGE_ROGUE", new[] { "tombs_reno_spellslinger", "tombs_reno_spell_seeker", "tombs_reno_secret_traps", "tombs_reno_random" }),
        ("PALADIN_SHAMAN", new[] { "tombs_finley_murloc_crusade", "tombs_finley_mighty_morphing", "tombs_finley_back_in_action", "tombs_finley_random" }),
        ("DRUID_PRIEST", new[] { "tombs_elise_healing_help", "tombs_elise_swarms", "tombs_elise_insight", "tombs_elise_random" }),
        ("HUNTER_WARRIOR", new[] { "tombs_brann_taming_dinos", "tombs_brann_beastly_bodyguards", "tombs_brann_call_for_help", "tombs_brann_random" }),
        ("DALA_Barkeye", new[] { "heist_hunter_1", "heist_hunter_2", "heist_hunter_3", "heist_hunter_random" }),
    })
        Check($"T2 row {row}: decks [{string.Join(", ", decks)}], the first open, the Random deck last", data.Class(row).Decks.SequenceEqual(decks) && data.Class(row).DefaultDeck == decks[0]);
    foreach (var s in starters)
    {
        var deck = data.FindDeck(s.Deck);
        var want = Resolved(ClientDeck(s.Client), s.Reprints);
        Check($"T2 {s.Deck}: {s.Client} (client DECK {clientDeckId[s.Client]}) card for card, reprints resolved [{string.Join(", ", s.Reprints.Select(p => p.Key + "->" + p.Value))}], class {s.Row}, unlock {s.Unlock ?? "none"}, named as the client",
            deck != null && deck.Class == s.Row && !deck.IsGenerated && deck.Cards.OrderBy(x => x).SequenceEqual(want.OrderBy(x => x)) && deck.Cards.Count == 10
            && deck.Unlock == s.Unlock && deck.Name == tables.GetProperty("DECK").EnumerateArray().First(r => r.GetProperty("noteName").GetString() == s.Client).GetProperty("name").GetString()
            && runJson.Contains("\"source\": \"" + s.Client + " (client DECK " + clientDeckId[s.Client] + ")"),
            string.Join(",", deck?.Cards ?? new List<string>()));
        var classes = data.Class(s.Row).ClassSet;
        var fails = deck.Cards.SelectMany(id => DungeonRunValidator.PoolCardFailures(id, s.Deck, classes, false, OfflinePoolPolicy.Load(), ClientKnows, catalogRun: true)).ToList();
        Check($"T2 {s.Deck}: every card passes the pool checks of a catalog run of {s.Row}", fails.Count == 0, string.Join(" | ", fails.Select(f => f.Message).Take(3)));
    }
    Check("T2 the F10 cards each sit in a new starter, except the four Rumble shrine cards (NEW1_016, LOE_046, TRL_522, TRL_535)",
        f10.Except(new[] { "NEW1_016", "LOE_046", "TRL_522", "TRL_535" }).All(id => starters.Any(s => data.FindDeck(s.Deck).Cards.Contains(id))));
    var heistScoped = data.Treasures.Where(t => t.Scope.Decks.Contains("heist_hunter_2")).ToList();
    Check("T2 every Heist treasure scoped to Ol' Barkeye's decks lists heist_hunter_3 too (36 Heist starters)",
        heistScoped.Count > 40 && heistScoped.All(t => t.Scope.Decks.Contains("heist_hunter_3"))
        && data.Decks.Count(d => d.Id.StartsWith("heist_", StringComparison.Ordinal)) == 36, heistScoped.Count.ToString());
    foreach (var r in rumble)
    {
        var cards = Resolved(ClientDeck(r.Client), r.Reprints);
        var fails = cards.SelectMany(id => DungeonRunValidator.PoolCardFailures(id, r.Client, new[] { r.Class }, false, OfflinePoolPolicy.Load(), ClientKnows, catalogRun: true)).ToList();
        Check($"T2 {r.Client} (Rumble shrine starter, its Rikkar row is family F14): its 10 cards pass the pool checks of a {r.Class} catalog run", cards.Count == 10 && fails.Count == 0,
            string.Join(" | ", fails.Select(f => f.Message).Take(3)));
    }
    // Validator branch: a dual hero row's starters get the catalog lifts (Hydrologist UNG_011, Hunter's Pack ULD_429).
    var e1 = DungeonRunValidator.Validate(Load(runJson.Replace("\"cards\": [\"ULD_145\", \"UNG_960\", \"CORE_ULD_723\", \"UNG_011\"", "\"cards\": [\"ULD_145\", \"UNG_960\", \"CORE_ULD_723\", \"UNG_011x\"")), null);
    Check("T2 validator: an unknown card in a Tombs starter is refused", e1.Any(e => e.Contains("UNG_011x")), string.Join(" | ", e1.Take(2)));
}
foreach (string id in f10.Concat(tokens))
{
    var p = DungeonRunAdmission.Classify(C(id), id, AdmitScope.Player, null, catalogRun: true);
    var b = DungeonRunAdmission.Classify(C(id), id, AdmitScope.Boss, null, catalogRun: true);
    var lp = DungeonRunAdmission.Classify(C(id), id, AdmitScope.Both, null, catalogRun: false);
    if (id == "ULD_429")
        Check("T2 admission ULD_429 Hunter's Pack: its secret-generated row admits the run player in a catalog run, never the boss (the boss side's generated Secrets stay off), nothing in a legacy run",
            p.Subject && p.Admitted && !b.Admitted && !lp.AdmittedSome && DungeonRunAdmission.RuleFor(id)?.Family == "secret-generated"
            && !DungeonRunAdmission.GeneratedRejects(true).Contains(id) && DungeonRunAdmission.GeneratedRejectsBoss(true).Contains(id), p.Family + " " + p.Admit);
    else if (id == "ULD_240")
        Check("T2 admission ULD_240 Arcane Flakmage: its secret-from-deck row admits both sides and generation (it reacts to Secrets, generates none)",
            p.Subject && p.Admitted && b.Admitted && lp.Admitted && DungeonRunAdmission.RuleFor(id)?.Family == "secret-from-deck" && !DungeonRunAdmission.GeneratedRejectsBoss(true).Contains(id));
    else
        Check($"T2 admission {id}: no family flags it, so it needs no row and is admitted for both sides and generation, in legacy and catalog runs",
            !p.Subject && p.Admitted && b.Admitted && lp.Admitted && DungeonRunAdmission.RuleFor(id) == null
            && !DungeonRunAdmission.GeneratedRejects(false).Contains(id) && !DungeonRunAdmission.GeneratedRejectsBoss(true).Contains(id));
}

// ---------------------------------------------------------------- the run setup path
GameConfig Configured(RunMatchPlan plan, int seed)
{
    var cfg = new GameConfig { RandomSeed = seed, Shuffle = true, SkipMulligan = false, History = true, Logging = false, FormatType = FormatType.FT_WILD };
    DungeonRunSetup.Configure(cfg, plan);
    return cfg;
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
(DungeonRunState s, RunMatchPlan plan, Game g) Fight(string card, long seed)
{
    var (row, deckId) = home[card];
    var cls = data.Class(row);
    var s = run.NewRun(all, row, deckId, cls.DefaultPower, seed, signatureId: run.SignatureOffer(all, run.Data.FindClass(row), seed).FirstOrDefault());
    if (!s.IsActRun || !s.IsCatalogRun || s.Phase != DungeonRunPhase.PreMatch || s.Floor != 1) throw new Exception("not an act catalog run at floor 1");
    var plan = run.BuildPlan(s);
    var g = StartFight(plan, run.MatchSeed(s));
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
Minion Board(Game g, Controller c, string id, int pos = -1)
{
    var m = (Minion)Entity.FromCard(c, C(id));
    Generic.SummonBlock(g, m, pos, c.Hero);
    g.DeathProcessingAndAuraUpdate();
    return m;
}
bool Play(Game g, IPlayable card, ICharacter target = null, int pos = -1) => g.Process(PlayCardTask.Any(g.CurrentPlayer, card, target, pos));
bool Pick(Game g, int id)
{
    var decision = g.GetPendingDecisions().FirstOrDefault(d => d.ControllerId == g.Player1.Id && d.Kind != SabberStoneCore.Model.Decisions.DecisionKind.Options);
    if (decision != null) return g.TryAnswer(new SabberStoneCore.Model.Decisions.DecisionAnswer(decision.Id, decision.ControllerId, new[] { id }), out _);
    return g.Process(ChooseTask.Pick(g.Player1, id));
}
void EndTurn(Game g) => g.Process(EndTurnTask.Any(g.CurrentPlayer));
void Clear(Controller c) { foreach (var e in c.HandZone.ToArray()) { c.HandZone.Remove(e); c.SetasideZone.Add(e); } }
void ClearBoards(Game g) { foreach (var c in new[] { g.Player1, g.Player2 }) foreach (var m in c.BoardZone.ToArray()) { c.BoardZone.Remove(m); c.SetasideZone.Add(m); } }
void Kill(Game g, Minion m) { m.Damage += m.Health; g.DeathProcessingAndAuraUpdate(); }
void NextTurn(Game g) { EndTurn(g); if (g.CurrentPlayer == g.Player2) EndTurn(g); }
bool CanTarget(Game g, IPlayable card, ICharacter t) => g.Player1.Options().OfType<PlayCardTask>().Any(o => o.Source == card && o.Target == t);
bool CanPlay(Game g, IPlayable card) => g.Player1.Options().OfType<PlayCardTask>().Any(o => o.Source == card);
List<string> HandIds(Controller c) => c.HandZone.Select(e => e.Card.Id).ToList();
(DungeonRunState s, RunMatchPlan plan, Game g, Controller p1, Controller p2) Setup(string card, long seed)
{
    var (s, plan, g) = Fight(card, seed);
    var p1 = g.Player1; var p2 = g.Player2;
    Mana(p1, 10); Clear(p1); ClearBoards(g);
    return (s, plan, g, p1, p2);
}

// ---------------------------------------------------------------- T3 NewRun / BuildPlan on every new starter
foreach (var st in starters)
{
    var cls = data.Class(st.Row);
    var s = run.NewRun(all, st.Row, st.Deck, cls.DefaultPower, 7, signatureId: run.SignatureOffer(all, run.Data.FindClass(st.Row), 7).FirstOrDefault());
    var plan = run.BuildPlan(s);
    var g = new Game(Configured(plan, 43));
    g.StartGame();
    var inGame = g.Player1.DeckZone.Select(e => e.Card.Id).Concat(g.Player1.HandZone.Select(e => e.Card.Id)).Where(id => id != "GAME_005").OrderBy(x => x);
    Check($"T3 {st.Deck}: NewRun on {st.Row} is an act catalog run with the starter in the run deck (origin starter); its match starts {cls.HeroCard} at 30 Health holding the ten cards",
        s.Class == st.Row && s.IsActRun && s.IsCatalogRun && s.StarterDeck == st.Deck
        && s.Cards.Where(c => c.Origin == DungeonRunDeck.OriginStarter).Select(c => c.Id).SequenceEqual(data.FindDeck(st.Deck).Cards)
        // Since F11 a Tombs run also holds its picked signature treasure (origin signature); a Heist run holds none.
        && s.Cards.Count(c => c.Origin != DungeonRunDeck.OriginStarter) == (data.Class(st.Row).Signatures.Count > 0 ? 1 : 0)
        && s.Cards.Where(c => c.Origin != DungeonRunDeck.OriginStarter).All(c => c.Origin == DungeonRunDeck.OriginSignature) && plan.PlayerHeroHealth == 30 && plan.PlayerHero.Id == cls.HeroCard
        && plan.PlayerGeneratedSecrets && g.Player1.Hero.Card.Id == cls.HeroCard && g.Player1.Hero.Health == 30 && inGame.SequenceEqual(s.Deck.OrderBy(x => x)),
        string.Join(",", inGame));
    bool Refused(Func<DungeonRunState> f) { try { f(); return false; } catch (InvalidOperationException) { return true; } }
    var fresh = new DungeonRunProfile();
    Check($"T3 {st.Deck}: {(st.Unlock == null ? "open on a new profile" : "locked on a new profile until " + st.Unlock)}",
        st.Unlock == null ? run.NewRun(fresh, st.Row, st.Deck, cls.DefaultPower, 3, signatureId: run.SignatureOffer(fresh, run.Data.FindClass(st.Row), 3).FirstOrDefault()).StarterDeck == st.Deck : Refused(() => run.NewRun(fresh, st.Row, st.Deck, cls.DefaultPower, 3, signatureId: run.SignatureOffer(fresh, run.Data.FindClass(st.Row), 3).FirstOrDefault())));
}
{
    var fresh = new DungeonRunProfile();
    foreach (var (primary, row, hero) in new[] { ("PALADIN", "PALADIN_SHAMAN", "ULDA_Finley"), ("DRUID", "DRUID_PRIEST", "ULDA_Elise"), ("HUNTER", "HUNTER_WARRIOR", "ULDA_Brann") })
    {
        var h = DungeonRunCatalog.Heroes(run, fresh, primary).FirstOrDefault(x => x.Id == row);
        var decks = DungeonRunCatalog.Decks(run, fresh, row);
        Check($"T3 picker: Heroes({primary}) offers {row} ({hero}, Tombs, dual, 30 Health, 3 powers, 4 decks); its first deck open, the rest locked",
            h != null && h.IsHero && h.IsDual && h.Adventure == "Tombs" && h.HeroCardId == hero && h.Health == 30 && h.PowerCount == 3 && h.DeckCount == 4
            && decks.Count == 4 && decks[0].Unlocked && decks[0].IsDefault && decks.Skip(1).All(d => !d.Unlocked) && decks[0].CardIds.Count == 10,
            string.Join(" | ", decks.Select(d => d.Name + ":" + d.Unlocked)));
    }
    var floor5 = new DungeonRunProfile(); floor5.Heroes.Add(new DungeonRunHeroStats { Class = "PALADIN_SHAMAN", BestFloor = 5 }); run.RefreshUnlocks(floor5);
    Check("T3 reaching floor 5 with Finley opens Mighty Morphing and Back in Action; the Random deck still needs a win",
        run.AvailableDecks(data.Class("PALADIN_SHAMAN"), floor5).Select(d => d.Id).SequenceEqual(new[] { "tombs_finley_murloc_crusade", "tombs_finley_mighty_morphing", "tombs_finley_back_in_action" }),
        string.Join(",", run.AvailableDecks(data.Class("PALADIN_SHAMAN"), floor5).Select(d => d.Id)));
}

// ---------------------------------------------------------------- T4 per-card regression games (the run setup path, the card played)
// Paladin
{
    var (s, plan, g, p1, p2) = Setup("ULD_145", 101);
    Check("T4 ULD_145 is in the match's run deck (Murloc Crusade)", plan.PlayerDeck.Any(c => c.Id == "ULD_145"));
    var zealot = ToHand(g, p1, "ULD_145");
    Play(g, zealot);
    var z = (Minion)zealot;
    Check("T4 Brazen Zealot does not count its own summon", z.AttackDamage == 2, z.AttackDamage.ToString());
    Play(g, ToHand(g, p1, "CS2_168"));
    Board(g, p1, "CS2_168");
    Check("T4 Brazen Zealot: +1 Attack for each friendly minion summoned (played or summoned): 4", z.AttackDamage == 4 && Has(z, "ULD_145e"), z.AttackDamage.ToString());
    Board(g, p2, "CS2_168");
    Check("T4 Brazen Zealot: an enemy summon does not count", z.AttackDamage == 4);
}
{
    var (s, plan, g, p1, p2) = Setup("ULD_439", 102);
    Play(g, ToHand(g, p1, "ULD_439"));
    var hand = HandIds(p1);
    Check("T4 Sandwasp Queen: two 2/1 Sandwasps (ULD_439t) to the hand", hand.Count(x => x == "ULD_439t") == 2 && C("ULD_439t").ATK == 2 && C("ULD_439t").Health == 1, string.Join(",", hand));
}
{
    var (s, plan, g, p1, p2) = Setup("ULD_207", 103);
    var guardian = (Minion)ToHand(g, p1, "ULD_207");
    Play(g, guardian);
    var yeti = Board(g, p2, "CS2_182");
    p1.Hero.Damage = 10;
    NextTurn(g); Mana(p1, 10);
    g.Process(MinionAttackTask.Any(p1, guardian, yeti));
    var back = p1.BoardZone.FirstOrDefault(m => m.Card.Id == "ULD_207");
    Check("T4 Ancestral Guardian: Lifesteal heals its hero by the damage it deals (4); it dies and Reborn returns it with 1 Health, once",
        p1.Hero.Damage == 6 && back != null && back != guardian && back.Health == 1 && back[GameTag.REBORN] == 0 && yeti.Health == 1, $"hero damage {p1.Hero.Damage} back {back?.Health}");
}
{
    var (s, plan, g, p1, p2) = Setup("ULD_275", 104);
    var wraith = Board(g, p1, "ULD_275");
    Check("T4 Bone Wraith has Taunt and Reborn", wraith.HasTaunt && wraith[GameTag.REBORN] == 1);
    Kill(g, wraith);
    var back = p1.BoardZone.FirstOrDefault(m => m.Card.Id == "ULD_275");
    Check("T4 Bone Wraith dies and returns with 1 Health and Taunt (Reborn spent)", back != null && back.Health == 1 && back.HasTaunt && back[GameTag.REBORN] == 0);
    Kill(g, back);
    Check("T4 the reborn Bone Wraith dies for good", p1.BoardZone.All(m => m.Card.Id != "ULD_275"));
}
// Druid / Priest
{
    var (s, plan, g, p1, p2) = Setup("ULD_196", 105);
    var a = Board(g, p1, "CS2_182"); var b = Board(g, p1, "CS2_182"); var far = Board(g, p1, "CS2_182");
    a.Damage = 3; b.Damage = 2; far.Damage = 4;
    var ritualist = ToHand(g, p1, "ULD_196");
    Play(g, ritualist, null, 1);
    Check("T4 Neferset Ritualist played between two damaged minions restores both to full Health; the minion not adjacent stays damaged",
        p1.BoardZone[1] == ritualist && a.Damage == 0 && b.Damage == 0 && far.Damage == 4, $"{a.Damage} {b.Damage} {far.Damage}");
}
{
    var (s, plan, g, p1, p2) = Setup("ULD_265", 106);
    var yeti = Board(g, p1, "CS2_182");
    var ritual = ToHand(g, p1, "ULD_265");
    Check("T4 Embalming Ritual targets minions only", CanTarget(g, ritual, yeti) && !CanTarget(g, ritual, p1.Hero) && !CanTarget(g, ritual, p2.Hero));
    Play(g, ritual, yeti);
    Check("T4 Embalming Ritual gives the minion Reborn", yeti[GameTag.REBORN] == 1);
    Kill(g, yeti);
    var back = p1.BoardZone.FirstOrDefault(m => m.Card.Id == "CS2_182");
    Check("T4 the minion dies and Reborn returns it with 1 Health", back != null && back.Health == 1 && back[GameTag.HAS_BEEN_REBORN] == 1);
    Play(g, ToHand(g, p1, "ULD_265"), back);
    Kill(g, back);
    var again = p1.BoardZone.FirstOrDefault(m => m.Card.Id == "CS2_182");
    Check("T4 a minion that was already reborn gets Reborn again and comes back a second time", again != null && again != back && again.Health == 1);
}
{
    var (s, plan, g, p1, p2) = Setup("ULD_134", 107);
    var yeti = Board(g, p2, "CS2_182");
    var bees = ToHand(g, p1, "ULD_134");
    Check("T4 BEEEES!!! targets minions only (either side)", CanTarget(g, bees, yeti) && !CanTarget(g, bees, p2.Hero));
    Play(g, bees, yeti);
    int beesDead = p1.GraveyardZone.Count(e => e.Card.Id == "ULD_134t");
    Check("T4 BEEEES!!!: four 1/1 Bees attack the 4/5 one by one: it is left at 1 Health and kills all four", yeti.Health == 1 && beesDead == 4 && p1.BoardZone.Count == 0,
        $"yeti {yeti.Health} bees dead {beesDead} board {p1.BoardZone.Count}");
    NextTurn(g); Mana(p1, 10); Clear(p1);
    ClearBoards(g);
    var wisp = Board(g, p2, "CS2_231");
    var small = Board(g, p2, "CS2_120");
    Play(g, ToHand(g, p1, "ULD_134"), small);
    int alive = p1.BoardZone.Count(m => m.Card.Id == "ULD_134t");
    Check("T4 BEEEES!!! at a 2/3: three Bees attack until it dies, the fourth stays on the board undamaged", (small.IsDead || small.Zone != p2.BoardZone) && p1.BoardZone.All(m => m.Damage == 0), $"alive {alive}");
    Check("T4 the Bees are 1/1 Beasts", C("ULD_134t").IsRace(Race.BEAST) && alive == 1 && p1.BoardZone.All(m => m.Card.Id == "ULD_134t"), $"alive {alive}");
    var bees7 = ToHand(g, p1, "ULD_134");
    for (int i = 0; i < 5; i++) Board(g, p1, "CS2_231");
    Board(g, p1, "CS2_231");
    Check("T4 ... and with a full board cannot be played", !CanPlay(g, bees7));
    var lastWisp = p1.BoardZone.Last(); lastWisp.Damage = 1; g.DeathProcessingAndAuraUpdate();
    Play(g, bees7, wisp);
    Check("T4 BEEEES!!! with one free slot summons one Bee, which kills the 1/1 and dies", wisp.IsDead && p1.BoardZone.Count(m => m.Card.Id == "ULD_134t") == 1 && p1.BoardZone.Count == 6,
        string.Join(",", p1.BoardZone.Select(m => m.Card.Id)));
}
{
    var (s, plan, g, p1, p2) = Setup("ULD_185", 108);
    var b = Board(g, p1, "ULD_185");
    Check("T4 Temple Berserker: 1 Attack undamaged", b.AttackDamage == 1);
    g.Process(PlayCardTask.Any(p1, ToHand(g, p1, "CS2_008"), b));
    Check("T4 Temple Berserker: +2 Attack while damaged (3)", b.AttackDamage == 3 && b.Health == 1, b.AttackDamage.ToString());
    Kill(g, b);
    var back = p1.BoardZone.FirstOrDefault(m => m.Card.Id == "ULD_185");
    Check("T4 Temple Berserker is reborn with 1 of its 2 Health, so damaged: 3 Attack", back != null && back.Health == 1 && back.AttackDamage == 3, back?.AttackDamage.ToString());
}
{
    var (s, plan, g, p1, p2) = Setup("ULD_250", 109);
    var goblin = Board(g, p1, "ULD_250");
    Check("T4 Infested Goblin has Taunt", goblin.HasTaunt);
    Kill(g, goblin);
    Check("T4 Infested Goblin's Deathrattle: two 1/1 Scarabs with Taunt (ULD_215t) to the hand", HandIds(p1).Count(x => x == "ULD_215t") == 2 && C("ULD_215t").ATK == 1 && C("ULD_215t").Health == 1);
}
{
    var (s, plan, g, p1, p2) = Setup("ULD_136", 110);
    Play(g, ToHand(g, p1, "ULD_136"));
    var offers = p1.Choice?.Choices.Select(id => g.IdEntityDic[id].Card).ToList() ?? new List<Card>();
    Check("T4 Worthy Expedition: three distinct scripted Choose One cards offered", offers.Count == 3 && offers.All(c => c[GameTag.CHOOSE_ONE] == 1 && c.ChooseOne && c.Implemented) && offers.Select(c => c.Id).Distinct().Count() == 3,
        string.Join(",", offers.Select(c => c.Id)));
    string picked = offers.Count > 0 ? offers[0].Id : null;
    if (picked != null) Pick(g, p1.Choice.Choices[0]);
    Check("T4 Worthy Expedition: the pick goes to the hand", picked != null && HandIds(p1).Contains(picked));
}
{
    var (s, plan, g, p1, p2) = Setup("ULD_188", 111);
    Play(g, ToHand(g, p1, "ULD_188"));
    var offers = p1.Choice?.Choices.Select(id => g.IdEntityDic[id].Card).ToList() ?? new List<Card>();
    Check("T4 Golden Scarab: three distinct scripted 4-Cost cards offered", offers.Count == 3 && offers.All(c => c.Cost == 4 && c.Implemented) && offers.Select(c => c.Id).Distinct().Count() == 3, string.Join(",", offers.Select(c => c.Id + ":" + c.Cost)));
    var chosen = g.IdEntityDic[p1.Choice.Choices[1]];
    Pick(g, chosen.Id);
    Check("T4 Golden Scarab: the pick goes to the hand at its printed Cost (no Archaeologist)", p1.HandZone.Contains(chosen) && chosen.Cost == 4);
}
{
    var (s, plan, g, p1, p2) = Setup("ULD_309", 112);
    var arch = Board(g, p1, "ULD_309");
    Play(g, ToHand(g, p1, "ULD_188"));
    var chosen = g.IdEntityDic[p1.Choice.Choices[0]];
    Pick(g, chosen.Id);
    Check("T4 Dwarven Archaeologist: the Discovered 4-Cost card costs (3) in hand", p1.HandZone.Contains(chosen) && chosen.Cost == 3, chosen.Cost.ToString());
    var arch2 = Board(g, p1, "ULD_309");
    Play(g, ToHand(g, p1, "ULD_136"));
    var second = g.IdEntityDic[p1.Choice.Choices[0]];
    int printed = second.Card.Cost;
    Pick(g, second.Id);
    Check("T4 two Archaeologists reduce a Discovered card by (2) (Worthy Expedition)", second.Cost == Math.Max(0, printed - 2), $"{printed} -> {second.Cost}");
    g.Process(PlayCardTask.Any(p1, ToHand(g, p1, "EX1_332"), arch)); // Silence
    g.Process(PlayCardTask.Any(p1, ToHand(g, p1, "EX1_332"), arch2));
    Play(g, ToHand(g, p1, "ULD_188"));
    var third = g.IdEntityDic[p1.Choice.Choices[0]];
    Pick(g, third.Id);
    Check("T4 a silenced Archaeologist reduces nothing", third.Cost == 4, third.Cost.ToString());
}
{
    var (s, plan, g, p1, p2) = Setup("ULD_269", 113);
    var yeti = Board(g, p1, "CS2_182");
    yeti.Damage = 4;
    g.Process(PlayCardTask.Any(p1, ToHand(g, p1, "CS2_092"), yeti)); // Blessing of Kings +4/+4
    var reclaimer = ToHand(g, p1, "ULD_269");
    Check("T4 Wretched Reclaimer targets friendly minions only", CanTarget(g, reclaimer, yeti) && !CanTarget(g, reclaimer, p2.Hero) && !CanTarget(g, reclaimer, p1.Hero));
    Play(g, reclaimer, yeti);
    var back = p1.BoardZone.FirstOrDefault(m => m.Card.Id == "CS2_182");
    Check("T4 Wretched Reclaimer destroys it and a fresh copy returns with full Health (enchantments gone)",
        yeti.IsDead && back != null && back != yeti && back.Health == 5 && back.AttackDamage == 4 && back.Damage == 0, $"{back?.AttackDamage}/{back?.Health}");
    NextTurn(g); Mana(p1, 10); ClearBoards(g); Clear(p1);
    var lone = ToHand(g, p1, "ULD_269");
    Check("T4 Wretched Reclaimer with no friendly minion is played without a target", CanPlay(g, lone) && !CanTarget(g, lone, p2.Hero));
    Play(g, lone);
    Check("T4 ... and only summons itself", p1.BoardZone.Count == 1 && p1.BoardZone[0] == lone);
}
{
    var (s, plan, g, p1, p2) = Setup("ULD_266", 114);
    var yeti = Board(g, p1, "CS2_182");
    var mummy = Board(g, p1, "ULD_266");
    Kill(g, mummy);
    var back = p1.BoardZone.FirstOrDefault(m => m.Card.Id == "ULD_266");
    Check("T4 Grandmummy's Deathrattle gives the other friendly minion +1/+1 (ULD_266e); Reborn returns Grandmummy with 1 Health",
        yeti.AttackDamage == 5 && yeti.Health == 6 && Has(yeti, "ULD_266e") && back != null && back.Health == 1, $"{yeti.AttackDamage}/{yeti.Health}");
    Kill(g, back);
    Check("T4 the reborn Grandmummy's Deathrattle buffs again", yeti.AttackDamage == 6 && yeti.Health == 7);
}
// Hunter / Warrior
{
    var (s, plan, g, p1, p2) = Setup("ULD_174", 115);
    var egg = Board(g, p1, "ULD_174");
    Kill(g, egg);
    var serpent = p1.BoardZone.FirstOrDefault(m => m.Card.Id == "ULD_174t");
    Check("T4 Serpent Egg's Deathrattle summons a 3/4 Sea Serpent", serpent != null && serpent.AttackDamage == 3 && serpent.Health == 4);
}
{
    var (s, plan, g, p1, p2) = Setup("ULD_430", 116);
    Play(g, ToHand(g, p1, "ULD_430"));
    Check("T4 Desert Spear equips as a 1/3", p1.Hero.Weapon?.Card.Id == "ULD_430" && p1.Hero.Weapon.AttackDamage == 1 && p1.Hero.Weapon.Durability == 3);
    int hp = p2.Hero.Health;
    g.Process(HeroAttackTask.Any(p1, p2.Hero));
    var locust = p1.BoardZone.FirstOrDefault(m => m.Card.Id == "ULD_430t");
    Check("T4 Desert Spear: after the hero attacks, a 1/1 Locust with Rush (it can attack minions at once)", p2.Hero.Health == hp - 1 && locust != null && locust[GameTag.RUSH] == 1 && p1.Hero.Weapon.Durability == 2);
    var yeti = Board(g, p2, "CS2_182");
    Check("T4 the Locust may attack an enemy minion this turn", p1.Options().OfType<MinionAttackTask>().Any(o => o.Source == locust && o.Target == yeti));
}
{
    var (s, plan, g, p1, p2) = Setup("GVG_046", 117);
    Board(g, p1, "CS2_120"); Board(g, p1, "CS2_172"); Board(g, p1, "CS2_182");
    var king = (Minion)ToHand(g, p1, "GVG_046");
    Play(g, king);
    Check("T4 King of Beasts: +1 Attack for each other Beast (River Crocolisk and Bloodfen Raptor; the Yeti is no Beast): 4, Taunt", king.AttackDamage == 4 && king.HasTaunt && Has(king, "GVG_046e"), king.AttackDamage.ToString());
    ClearBoards(g);
    var lonely = (Minion)ToHand(g, p1, "GVG_046");
    Play(g, lonely);
    Check("T4 King of Beasts with no other Beast keeps its 2 Attack", lonely.AttackDamage == 2);
}
{
    var (s, plan, g, p1, p2) = Setup("ULD_429", 118);
    Check("T4 the run plan of Brann's catalog run lets the player generate Secrets", plan.PlayerGeneratedSecrets);
    Play(g, ToHand(g, p1, "ULD_429"));
    var got = p1.HandZone.Select(e => e.Card).ToList();
    Check("T4 Hunter's Pack: a random Hunter Beast, a Hunter Secret and a Hunter weapon to the hand",
        got.Count == 3 && got[0].Type == CardType.MINION && got[0].Class == CardClass.HUNTER && got[0].IsRace(Race.BEAST)
        && got[1].IsSecret && got[1].Class == CardClass.HUNTER && got[2].Type == CardType.WEAPON && got[2].Class == CardClass.HUNTER && got.All(c => c.Implemented),
        string.Join(",", got.Select(c => c.Id)));
    // A side that excludes generated Secrets still gets the Beast and the weapon.
    var cfg = new GameConfig { RandomSeed = 5, StartPlayer = 1, Player1HeroClass = CardClass.HUNTER, Player2HeroClass = CardClass.MAGE, Shuffle = false, FormatType = FormatType.FT_WILD,
        Player1ExcludeGeneratedSecrets = true };
    var lg = new Game(cfg); lg.StartGame(); lg.Process(ChooseTask.Mulligan(lg.Player1, new List<int>())); lg.Process(ChooseTask.Mulligan(lg.Player2, new List<int>())); lg.MainBegin();
    Mana(lg.Player1, 10); Clear(lg.Player1);
    lg.Process(PlayCardTask.Any(lg.Player1, ToHand(lg, lg.Player1, "ULD_429")));
    var lgot = lg.Player1.HandZone.Select(e => e.Card).ToList();
    Check("T4 Hunter's Pack on a side that excludes generated Secrets: the Beast and the weapon, no Secret", lgot.Count == 2 && lgot[0].IsRace(Race.BEAST) && lgot[1].Type == CardType.WEAPON && !lgot.Any(c => c.IsSecret),
        string.Join(",", lgot.Select(c => c.Id)));
}
// Generated cards are scripted: the random and Discover F10 cards over 40 games each.
{
    var bad = new List<string>();
    foreach (string card in new[] { "ULD_186", "ULD_429", "ULD_328", "ULD_136", "ULD_188" })
        for (int seed = 0; seed < 40; seed++)
        {
            var (s, plan, g, p1, p2) = Setup(card, 1000 + seed);
            Play(g, ToHand(g, p1, card));
            var made = p1.HandZone.Select(e => e.Card).Concat(p1.Choice?.Choices.Select(id => g.IdEntityDic[id].Card) ?? Enumerable.Empty<Card>()).ToList();
            bad.AddRange(made.Where(c => !c.Implemented).Select(c => card + ":" + c.Id));
            if (made.Count == 0) bad.Add(card + ":none");
        }
    var w = Setup("ULD_231", 2000);
    var kick = Board(w.g, w.p1, "ULD_231");
    for (int i = 0; i < 40; i++) { Mana(w.p1, 10); Clear(w.p1); Play(w.g, ToHand(w.g, w.p1, "EX1_124"), w.p2.Hero); bad.AddRange(w.p1.HandZone.Where(e => !e.Card.Implemented || !e.Card.Combo).Select(e => "ULD_231:" + e.Card.Id)); }
    Check("T4 over 40 games each, Pharaoh Cat, Hunter's Pack, Clever Disguise, Worthy Expedition, Golden Scarab and Whirlkick Master generate only scripted cards (the legacy pools still list unscripted ids)",
        bad.Count == 0, string.Join(",", bad.Distinct().Take(8)));
}
// Mage / Rogue
{
    var (s, plan, g, p1, p2) = Setup("ULD_328", 119);
    Play(g, ToHand(g, p1, "ULD_328"));
    var got = p1.HandZone.Select(e => e.Card).ToList();
    Check($"T4 Clever Disguise: two random spells of classes other than the hero's ({p1.HeroClass}), never neutral",
        got.Count == 2 && got.All(c => c.Type == CardType.SPELL && c.Class != p1.HeroClass && c.Class != CardClass.NEUTRAL && c.Implemented), string.Join(",", got.Select(c => c.Id + ":" + c.Class)));
}
{
    var (s, plan, g, p1, p2) = Setup("ULD_231", 120);
    var kick = Board(g, p1, "ULD_231");
    Play(g, ToHand(g, p1, "EX1_124"), p2.Hero); // Eviscerate: a Combo spell
    var got = p1.HandZone.Select(e => e.Card).ToList();
    Check("T4 Whirlkick Master: playing a Combo card adds a random Combo card", got.Count == 1 && got[0].Combo && got[0].Implemented, string.Join(",", got.Select(c => c.Id)));
    Play(g, ToHand(g, p1, "CS2_008"), p2.Hero);
    Check("T4 Whirlkick Master: a card without Combo adds nothing", p1.HandZone.Count == 1);
}
{
    var (s, plan, g, p1, p2) = Setup("ULD_186", 121);
    Play(g, ToHand(g, p1, "ULD_186"));
    var got = p1.HandZone.Select(e => e.Card).ToList();
    Check("T4 Pharaoh Cat: a random Reborn minion to the hand", got.Count == 1 && got[0].Type == CardType.MINION && got[0][GameTag.REBORN] == 1 && got[0].Implemented, string.Join(",", got.Select(c => c.Id)));
}
{
    var (s, plan, g, p1, p2) = Setup("ULD_240", 122);
    var flak = Board(g, p1, "ULD_240");
    var y1 = Board(g, p2, "CS2_182"); var y2 = Board(g, p2, "CS2_182"); var mine = Board(g, p1, "CS2_182");
    Play(g, ToHand(g, p1, "EX1_289")); // Ice Barrier
    Check("T4 Arcane Flakmage: after its owner plays a Secret, 2 damage to every enemy minion (not to friendly ones)", y1.Damage == 2 && y2.Damage == 2 && mine.Damage == 0 && p1.SecretZone.Count == 1);
    Play(g, ToHand(g, p1, "CS2_008"), p2.Hero);
    Check("T4 Arcane Flakmage: a spell that is no Secret does nothing", y1.Damage == 2);
    NextTurn(g);
    Check("T4 Arcane Flakmage: the enemy's turn starts with no damage from it", y1.Damage == 2);
}
{
    var (s, plan, g, p1, p2) = Setup("ULD_285", 123);
    p1.IsComboActive = false;
    Play(g, ToHand(g, p1, "ULD_285"));
    Check("T4 Hooked Scimitar as the turn's first card: a 2/2, no Combo", p1.Hero.Weapon?.Card.Id == "ULD_285" && p1.Hero.Weapon.AttackDamage == 2 && p1.Hero.Weapon.Durability == 2);
    NextTurn(g); Mana(p1, 10); Clear(p1);
    Play(g, ToHand(g, p1, "GAME_005"));
    Play(g, ToHand(g, p1, "ULD_285"));
    Check("T4 Hooked Scimitar after another card (Combo): +2 Attack (ULD_285e), a 4/2", p1.Hero.Weapon.AttackDamage == 4 && Has(p1.Hero.Weapon, "ULD_285e"), p1.Hero.Weapon.AttackDamage.ToString());
}
// Heist Hunter Deck3
{
    var (s, plan, g, p1, p2) = Setup("DAL_377", 124);
    Check("T4 DAL_377 is in the match's run deck (Tech and Death)", plan.PlayerDeck.Any(c => c.Id == "DAL_377"));
    var nine = ToHand(g, p1, "DAL_377");
    Check("T4 Nine Lives cannot be played before a friendly Deathrattle minion died", !CanPlay(g, nine));
    var plain = Board(g, p1, "CS2_182"); Kill(g, plain);
    Check("T4 ... a dead minion without Deathrattle does not count", !CanPlay(g, nine));
    var golem = Board(g, p1, "EX1_556"); Kill(g, golem);
    var hoarder = Board(g, p1, "EX1_096"); Kill(g, hoarder);
    ClearBoards(g);
    int hand = p1.HandZone.Count;
    Check("T4 Nine Lives is playable once Harvest Golem and Loot Hoarder died", CanPlay(g, nine));
    Play(g, nine);
    var offers = p1.Choice?.Choices.Select(id => g.IdEntityDic[id].Card.Id).OrderBy(x => x).ToList() ?? new List<string>();
    Check("T4 Nine Lives offers each friendly Deathrattle minion that died, once (not the Yeti)", offers.SequenceEqual(new[] { "EX1_096", "EX1_556" }), string.Join(",", offers));
    int golemOffer = p1.Choice.Choices.First(id => g.IdEntityDic[id].Card.Id == "EX1_556");
    Pick(g, golemOffer);
    g.DeathProcessingAndAuraUpdate();
    Check("T4 Nine Lives: the chosen Harvest Golem goes to the hand and its Deathrattle runs: a 2/1 Damaged Golem is summoned",
        p1.HandZone.Any(e => e.Id == golemOffer) && p1.BoardZone.Count(m => m.Card.Id == "skele21") == 1, string.Join(",", p1.BoardZone.Select(m => m.Card.Id)));
    NextTurn(g); Mana(p1, 10); Clear(p1);
    int deck = p1.DeckZone.Count;
    var nine2 = ToHand(g, p1, "DAL_377");
    Play(g, nine2);
    int hoarderOffer = p1.Choice.Choices.First(id => g.IdEntityDic[id].Card.Id == "EX1_096");
    Pick(g, hoarderOffer);
    Check("T4 Nine Lives on Loot Hoarder: it goes to the hand and its Deathrattle draws a card", p1.HandZone.Any(e => e.Id == hoarderOffer) && p1.DeckZone.Count == deck - 1 && p1.HandZone.Count == 2,
        $"deck {deck}->{p1.DeckZone.Count} hand {p1.HandZone.Count}");
}
// Rumble shrine cards (a Heist row of their class until F14)
{
    var (s, plan, g, p1, p2) = Setup("NEW1_016", 125);
    var pirate = Entity.FromCard(p1, C("CS2_146"));
    foreach (var e in p1.DeckZone.ToArray()) { p1.DeckZone.Remove(e); p1.SetasideZone.Add(e); }
    p1.DeckZone.Add(Entity.FromCard(p1, C("CS2_182"))); p1.DeckZone.Add(pirate); p1.DeckZone.Add(Entity.FromCard(p1, C("CS2_120")));
    Play(g, ToHand(g, p1, "NEW1_016"));
    Check("T4 Captain's Parrot draws the Pirate from the deck (not the other cards)", p1.HandZone.Contains(pirate) && p1.DeckZone.Count == 2 && p1.HandZone.Count == 1);
    Play(g, ToHand(g, p1, "NEW1_016"));
    Check("T4 Captain's Parrot with no Pirate in the deck draws nothing", p1.DeckZone.Count == 2 && p1.HandZone.Count == 1);
}
{
    var (s, plan, g, p1, p2) = Setup("LOE_046", 126);
    var toad = Board(g, p1, "LOE_046");
    int hp = p2.Hero.Health;
    Kill(g, toad);
    Check("T4 Huge Toad's Deathrattle deals 1 damage to a random enemy (here only the enemy hero)", p2.Hero.Health == hp - 1 && p1.Hero.Damage == 0);
    var yeti = Board(g, p2, "CS2_182");
    var toads = Enumerable.Range(0, 6).Select(_ => Board(g, p1, "LOE_046")).ToList();
    hp = p2.Hero.Health;
    foreach (var t in toads) Kill(g, t);
    Check("T4 six Huge Toads hit only enemies (the hero or the Yeti), 6 damage in all", (hp - p2.Hero.Health) + yeti.Damage == 6 && p1.Hero.Damage == 0, $"{hp - p2.Hero.Health} + {yeti.Damage}");
}
{
    var (s, plan, g, p1, p2) = Setup("TRL_522", 127);
    var yeti = Board(g, p2, "CS2_182");
    var wart = ToHand(g, p1, "TRL_522");
    Check("T4 Wartbringer with fewer than 2 spells played this turn has no target", CanPlay(g, wart) && !CanTarget(g, wart, yeti) && !CanTarget(g, wart, p2.Hero));
    Play(g, ToHand(g, p1, "GAME_005"));
    var wart1 = ToHand(g, p1, "TRL_522");
    Check("T4 ... after one spell, still none", !CanTarget(g, wart1, yeti));
    Play(g, wart);
    Check("T4 ... and it deals no damage", yeti.Damage == 0 && p2.Hero.Damage == 0);
    Play(g, ToHand(g, p1, "GAME_005"));
    Check("T4 after 2 spells this turn Wartbringer must target a character", CanTarget(g, wart1, yeti) && CanTarget(g, wart1, p2.Hero) && !g.Player1.Options().OfType<PlayCardTask>().Any(o => o.Source == wart1 && o.Target == null));
    Play(g, wart1, yeti);
    Check("T4 Wartbringer: 2 damage", yeti.Damage == 2);
    NextTurn(g); Mana(p1, 10);
    var wart2 = ToHand(g, p1, "TRL_522");
    Check("T4 the count is per turn: next turn no target again", !CanTarget(g, wart2, yeti));
}
{
    var (s, plan, g, p1, p2) = Setup("TRL_535", 128);
    var a = Board(g, p1, "CS2_182"); var shell = Board(g, p1, "TRL_535"); var b = Board(g, p1, "CS2_182"); var far = Board(g, p1, "CS2_182");
    Check("T4 board: Yeti, Snapjaw Shellfighter, Yeti, Yeti", p1.BoardZone[1] == shell);
    Play(g, ToHand(g, p1, "CS2_008"), a);
    Check("T4 Snapjaw Shellfighter: a hit on its left neighbour is dealt to it instead", a.Damage == 0 && shell.Damage == 1, $"{a.Damage} {shell.Damage}");
    Play(g, ToHand(g, p1, "CS2_008"), far);
    Check("T4 a minion not adjacent takes its own hit", far.Damage == 1 && shell.Damage == 1);
    Play(g, ToHand(g, p1, "CS2_008"), shell);
    Check("T4 a hit on the Shellfighter itself is its own", shell.Damage == 2);
    Play(g, ToHand(g, p1, "EX1_400")); // Whirlwind: 1 damage to all minions
    Check("T4 Whirlwind: the Shellfighter takes its own hit and both neighbours' (3); the neighbours none; the far Yeti its own",
        shell.Damage == 5 && a.Damage == 0 && b.Damage == 0 && far.Damage == 2, $"shell {shell.Damage} a {a.Damage} b {b.Damage} far {far.Damage}");
    var raptor = Board(g, p2, "CS2_172");
    NextTurn(g); EndTurn(g);
    // The enemy's 3/2 attacks the Yeti right of the Shellfighter: the Shellfighter takes the 3, the Yeti hits back.
    g.Process(MinionAttackTask.Any(p2, raptor, b));
    Check("T4 combat: an enemy minion attacking a neighbour deals its damage to the Shellfighter; the neighbour still strikes back",
        b.Damage == 0 && shell.IsDead && raptor.IsDead, $"b {b.Damage} shell {shell.Damage} raptor dead {raptor.IsDead}");
    Check("T4 once the Shellfighter is gone its neighbours take their own damage", p1.BoardZone.All(m => m.Card.Id != "TRL_535"));
    var (s2, plan2, g2, q1, q2) = Setup("TRL_535", 129);
    var x = Board(g2, q1, "CS2_182"); var sh = Board(g2, q1, "TRL_535");
    g2.Process(PlayCardTask.Any(q1, ToHand(g2, q1, "EX1_332"), sh)); // Silence
    g2.Process(PlayCardTask.Any(q1, ToHand(g2, q1, "CS2_008"), x));
    Check("T4 a silenced Snapjaw Shellfighter redirects nothing", x.Damage == 1 && sh.Damage == 0);
    var sh2 = Board(g2, q1, "TRL_535", 0);
    g2.Process(PlayCardTask.Any(q1, ToHand(g2, q1, "CS2_008"), x));
    Check("T4 between a fresh Shellfighter and the silenced one, the Yeti's hit goes to the active Shellfighter",
        x.ZonePosition == 1 && x.Damage == 1 && sh2.Damage == 1 && sh.Damage == 0, $"x {x.Damage} sh2 {sh2.Damage} sh {sh.Damage} order {string.Join(",", q1.BoardZone.Select(m => m.Card.Id))}");
}

// ---------------------------------------------------------------- T5 full games against the real boss AI
var testBudget = new Budget(6000, 2000, 300, 6, 3, 100, 8, turnSearchSeconds: 6, turnWallSeconds: 8);
var f10Set = new HashSet<string>(f10, StringComparer.Ordinal);
// The boss's fixture deck: the 27 F10 cards the boss is admitted for (not Hunter's Pack), and three Chillwind Yetis.
var bossFixture = f10.Where(id => id != "ULD_429").Concat(new[] { "CS2_182", "CS2_182", "CS2_182" }).ToList();
string AiGame(string row, string deckId, long seed, bool bossPlaysF10)
{
    var cls = data.Class(row);
    var s = run.NewRun(all, row, deckId, cls.DefaultPower, seed, signatureId: run.SignatureOffer(all, run.Data.FindClass(row), seed).FirstOrDefault());
    for (int i = 0; i < 2; i++) s.Matches.Add(new DungeonRunMatchSummary { Floor = 1, Boss = s.BossSequence[0], Outcome = DungeonRunOutcome.Win });
    var boss = run.CurrentBoss(s);
    var plan = run.BuildPlan(s);
    if (bossPlaysF10) plan.BossDeck = bossFixture.Select(C).ToList();
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
    int decisionIndex = 0, actions = 0, decisions = 0, fallbacks = 0;
    void Act(PlayerTask task, string who)
    {
        if (++actions > 1500) throw new Exception("stuck after 1500 actions");
        if (task is ChooseTask pick && game.Step != Step.BEGIN_MULLIGAN)
        {
            var decision = game.GetPendingDecisions().Single(dd => dd.ControllerId == task.Controller.Id);
            if (!game.TryAnswer(new SabberStoneCore.Model.Decisions.DecisionAnswer(decision.Id, decision.ControllerId, pick.Choices.ToArray()), out var why))
                throw new Exception($"engine rejected {who} choice ({why})");
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
        else Act(LocalBot.Choose(game.Player1.Options()), "player");
    }
    string result = game.State != State.COMPLETE ? "turn-cap" : game.Player2.PlayState == PlayState.WON ? "boss" : game.Player1.PlayState == PlayState.WON ? "player" : "tie";
    var mine = game.Player1.PlayHistory.Select(h => h.SourceCard.Id).Where(f10Set.Contains).Distinct().OrderBy(x => x).ToList();
    var theirs = game.Player2.PlayHistory.Select(h => h.SourceCard.Id).Where(f10Set.Contains).Distinct().OrderBy(x => x).ToList();
    return $"{result} turns {game.Turn} decisions {decisions} fallbacks {fallbacks} player-f10 {mine.Count} [{string.Join(" ", mine)}] boss-f10 {theirs.Count} [{string.Join(" ", theirs)}] boss {boss.Id}";
}
var playerPlayed = new HashSet<string>(StringComparer.Ordinal);
foreach (var st in starters)
{
    string line = null;
    // Whether an F10 card is drawn and played is up to the shuffle: since F11 (the signature joins a Tombs run deck) a game in
    // which the player plays none is followed by one on the next seed, at most three; a fault or a fallback in any game fails.
    for (long seed = 281; seed < 284; seed++)
    {
        try { line = AiGame(st.Row, st.Deck, seed, false); }
        catch (Exception e) { line = "FAULT " + e.GetType().Name + ": " + e.Message.Split('\n')[0]; }
        Console.WriteLine($"T5 player {st.Deck,-32} {line}");
        if (line.StartsWith("FAULT") || !line.Contains("fallbacks 0 ") || !line.Contains("player-f10 0 ")) break;
    }
    var f10InDeck = data.FindDeck(st.Deck).Cards.Where(f10Set.Contains).ToList();
    int i0 = line.IndexOf("player-f10 [", StringComparison.Ordinal);
    foreach (string id in f10InDeck) if (line.Contains(" " + id) || line.Contains("[" + id)) playerPlayed.Add(id);
    Check($"T5 {st.Deck} ({st.Row}) against the boss AI: the game ends without a fault or an AI fallback, and the player plays an F10 card",
        !line.StartsWith("FAULT") && line.Contains("fallbacks 0 ") && !line.Contains("player-f10 0 ") && !line.StartsWith("turn-cap"), line);
}
var bossPlayed = new HashSet<string>(StringComparer.Ordinal);
foreach (var (row, deck, seed) in new[] { ("PALADIN_SHAMAN", "tombs_finley_murloc_crusade", 301L), ("DRUID_PRIEST", "tombs_elise_insight", 302L), ("HUNTER_WARRIOR", "tombs_brann_taming_dinos", 303L),
    ("MAGE_ROGUE", "tombs_reno_secret_traps", 304L), ("DALA_Barkeye", "heist_hunter_3", 305L), ("PALADIN_SHAMAN", "tombs_finley_back_in_action", 306L),
    ("DRUID_PRIEST", "tombs_elise_swarms", 307L), ("HUNTER_WARRIOR", "tombs_brann_beastly_bodyguards", 308L) })
{
    string line;
    try { line = AiGame(row, deck, seed, true); }
    catch (Exception e) { line = "FAULT " + e.GetType().Name + ": " + e.Message.Split('\n')[0]; }
    Console.WriteLine($"T5 boss   {deck,-32} {line}");
    int at = line.IndexOf("boss-f10 ", StringComparison.Ordinal);
    if (at >= 0)
    {
        string list = line.Substring(line.IndexOf('[', at) + 1);
        list = list.Substring(0, list.IndexOf(']'));
        foreach (string id in list.Split(' ', StringSplitOptions.RemoveEmptyEntries)) bossPlayed.Add(id);
    }
    Check($"T5 the boss AI plays a deck of the F10 cards against {deck}: no fault, no AI fallback, at least three F10 cards played",
        !line.StartsWith("FAULT") && line.Contains("fallbacks 0 ") && at >= 0 && int.Parse(line.Substring(at + 9).Split(' ')[0]) >= 3 && !line.StartsWith("turn-cap"), line);
}
Console.WriteLine("T5 boss played " + bossPlayed.Count + ": " + string.Join(" ", bossPlayed.OrderBy(x => x)));
Check("T5 across the boss games the boss AI plays at least 15 distinct F10 cards", bossPlayed.Count >= 15, bossPlayed.Count.ToString());

Console.WriteLine($"{passes} passed, {failures} failed");
return failures == 0 ? 0 : 1;
