// c-treasures-F5: phase 6 family F5 of the unified Dungeon Run, the Witchwood Monster Hunt treasures every hunter is offered
// (GILA_801-806, 810-814, 816a-c, 817-819, 820, 821a-c, 824-827, 850b, 851a-c, 852a-c, 853b, 854), including the five evolving
// artifacts (Coin Pouch, Curio, Mob, Militia Horn: "upgrade this and shuffle it into your deck"; the Tracker transforms itself).
// For every id: the engine content (scripted, keyed and shelled by its own client id, name and text as
// facts/violet-hold-2026-09.json has them), the run data (run-v1.json rows scoped to the four Monster Hunt starters, the in-match
// stages never offered), the validator and admission checks, a scripted regression game through the run setup path
// (DungeonRun.NewRun on a hunter's hero row -> BuildPlan -> DungeonRunSetup.Configure -> Game -> ApplyAfterStart -> mulligan ->
// MainBegin -> the card drawn from the run deck and played), and a full game against the real boss AI (DungeonRunBossPilot +
// GoldenPilot) in which the player plays it; it must end without a fault or an AI fallback. GILA_811, GILA_827 and GILA_853b
// also check that the boss's public hand knowledge does not learn the hidden copies. T30: games in which Impetuous Companion
// hands the boss F5 treasures, which the boss AI then plays. Headless: compiles C:\SoloStone\plugin\DungeonRun\*.cs and the NPC
// AI against p6f5/engine-out. Run it twice and compare the output.
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

CardType FactType(string id) => FactTag(id, 202) switch { 3 => CardType.HERO, 4 => CardType.MINION, 5 => CardType.SPELL, 6 => CardType.ENCHANTMENT, 7 => CardType.WEAPON, 10 => CardType.HERO_POWER, _ => CardType.INVALID };
var transport = new Dictionary<string, string>
{
    ["CS2_034"] = "HERO_08bp", ["CS2_102"] = "HERO_01bp", ["CS1h_001"] = "HERO_09bp", ["CS2_101"] = "HERO_04bp", ["CS2_017"] = "HERO_06bp",
    ["DS1h_292"] = "HERO_05bp", ["CS2_083b"] = "HERO_03bp", ["CS2_056"] = "HERO_07bp", ["CS2_049"] = "HERO_02bp",
    ["AT_132_WARRIOR"] = "HERO_01bp2", ["AT_132_SHAMAN"] = "HERO_02bp2", ["AT_132_ROGUE"] = "HERO_03bp2", ["AT_132_PALADIN"] = "HERO_04bp2",
    ["AT_132_HUNTER"] = "HERO_05bp2", ["AT_132_DRUID"] = "HERO_06bp2", ["AT_132_WARLOCK"] = "HERO_07bp2", ["AT_132_MAGE"] = "HERO_08bp2", ["AT_132_PRIEST"] = "HERO_09bp2",
};
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


const string Tess = "GILA_500h3", Darius = "GILA_600h", Shaw = "GILA_400h", Toki = "GILA_900h";
string[] hunters = { Tess, Darius, Shaw, Toki };
string[] mhDecks = { "mh_tess_scavenger", "mh_darius_militia", "mh_shaw_kennel", "mh_toki_workshop" };
string[] f5 = { "GILA_801", "GILA_802", "GILA_803", "GILA_804", "GILA_805", "GILA_806", "GILA_810", "GILA_811", "GILA_812", "GILA_813",
    "GILA_814", "GILA_816a", "GILA_816b", "GILA_816c", "GILA_817", "GILA_818", "GILA_819", "GILA_820", "GILA_821a", "GILA_821b",
    "GILA_821c", "GILA_824", "GILA_825", "GILA_826", "GILA_827", "GILA_850b", "GILA_851a", "GILA_851b", "GILA_851c", "GILA_852a",
    "GILA_852b", "GILA_852c", "GILA_853b", "GILA_854" };
string[] tokens = { "GILA_817t", "GILA_818t", "GILA_819t", "GILA_854t" };
string[] passives = { "GILA_805", "GILA_813", "GILA_814" };
string[] stages = { "GILA_816b", "GILA_816c", "GILA_818", "GILA_819", "GILA_821b", "GILA_821c", "GILA_851b", "GILA_851c", "GILA_852b", "GILA_852c" };
string[] offeredF5 = f5.Where(id => !stages.Contains(id)).ToArray();
var effectShells = new Dictionary<string, string>
{
    ["fx_GILA_804e"] = "GILA_804e", ["fx_GILA_805e2"] = "GILA_805e2", ["fx_GILA_805"] = "GILA_805e", ["fx_GILA_810e"] = "GILA_810e",
    ["fx_GILA_813"] = "GILA_813e", ["fx_GILA_814"] = "GILA_814e", ["fx_GILA_824e"] = "GILA_824e", ["fx_GILA_825e"] = "GILA_825e",
    ["fx_GILA_852ae"] = "GILA_852ae", ["fx_GILA_852be"] = "GILA_852be", ["fx_GILA_852ce"] = "GILA_852ce",
};

// ---------------------------------------------------------------- T1 content
Check("T1 34 F5 ids", f5.Length == 34 && f5.Distinct().Count() == 34 && offeredF5.Length == 24);
foreach (string id in f5.Concat(tokens))
{
    var content = DungeonRunContent.Get(id);
    var card = C(id);
    RunContentKind want = passives.Contains(id) ? RunContentKind.PassiveTreasure : RunContentKind.ActiveTreasure;
    Check($"T1 {id}: a {want} keyed and shelled by its own id, scripted, the client's {FactType(id)}",
        content.Kind == want && content.ShellId == id && card != null && card.Implemented && card.Type == FactType(id)
        && DungeonRunContent.ForShell(id) == content && clientIds.Contains(id), card == null ? "no card" : content.Kind + " " + card.Type + " " + card.Implemented);
    Check($"T1 ids-text-match {id}: content name and text equal facts/violet-hold-2026-09.json",
        content.Name == factById[id].GetProperty("name").GetString() && content.Text == FactText(id), content.Name + " | " + content.Text);
    Check($"T1 {id}: cost {FactTag(id, 48)} as the client prints it" + (card.Type == CardType.MINION ? $", stats {FactTag(id, 47)}/{FactTag(id, 45)}" : ""),
        card.Cost == FactTag(id, 48) && (want == RunContentKind.PassiveTreasure || content.Cost == FactTag(id, 48))
        && (card.Type != CardType.MINION || card.ATK == FactTag(id, 47) && card.Health == FactTag(id, 45)),
        $"{card.Cost} {card.ATK}/{card.Health}");
}
foreach (var fx in effectShells)
{
    var e = DungeonRunContent.Get(fx.Key);
    Check($"T1 {fx.Value}: content {fx.Key} on the client's own ENCHANTMENT, scripted, name and text word for word",
        e.Kind == RunContentKind.Effect && e.ShellId == fx.Value && C(fx.Value).Implemented && C(fx.Value).Type == CardType.ENCHANTMENT && clientIds.Contains(fx.Value)
        && e.Name == factById[fx.Value].GetProperty("name").GetString() && e.Text == FactText(fx.Value), e.Name + " | " + e.Text);
}
Check("T1 keywords: Nalaa has Taunt and Divine Shield; the Trackers and Beastly Beauty have Rush; Worgen Tracker has Windfury; the Cursed Curio Ghost has Lifesteal",
    C("GILA_826")[GameTag.TAUNT] == 1 && C("GILA_826")[GameTag.DIVINE_SHIELD] == 1 && C("GILA_851a")[GameTag.RUSH] == 1 && C("GILA_851b")[GameTag.RUSH] == 1
    && C("GILA_851c")[GameTag.RUSH] == 1 && C("GILA_851c")[GameTag.WINDFURY] == 1 && C("GILA_854")[GameTag.RUSH] == 1 && C("GILA_819t")[GameTag.LIFESTEAL] == 1
    && C("GILA_817t")[GameTag.LIFESTEAL] == 0);
Check("T1 Spyglass has Echo; Cartographer and Gilnean Vigilante have Battlecry",
    C("GILA_811")[GameTag.ECHO] == 1 && C("GILA_802")[GameTag.BATTLECRY] == 1 && C("GILA_803")[GameTag.BATTLECRY] == 1);
Check("T1 the evolving chains: 816a>816b>816c, 817>818>819, 821a>821b>821c, 852a>852b>852c, 851a>851b>851c",
    DungeonRunContent.EvolvesTo.Count == 10 && DungeonRunContent.EvolvesTo["GILA_816a"] == "GILA_816b" && DungeonRunContent.EvolvesTo["GILA_816b"] == "GILA_816c"
    && DungeonRunContent.EvolvesTo["GILA_817"] == "GILA_818" && DungeonRunContent.EvolvesTo["GILA_818"] == "GILA_819"
    && DungeonRunContent.EvolvesTo["GILA_821a"] == "GILA_821b" && DungeonRunContent.EvolvesTo["GILA_821b"] == "GILA_821c"
    && DungeonRunContent.EvolvesTo["GILA_852a"] == "GILA_852b" && DungeonRunContent.EvolvesTo["GILA_852b"] == "GILA_852c"
    && DungeonRunContent.EvolvesTo["GILA_851a"] == "GILA_851b" && DungeonRunContent.EvolvesTo["GILA_851b"] == "GILA_851c"
    && DungeonRunContent.EvolvesTo.Keys.Concat(DungeonRunContent.EvolvesTo.Values).All(f5.Contains));
Check("T1 tokens: the Curios list their Ghosts, the Tracker stages and Beastly Beauty list what they become",
    DungeonRunContent.Get("GILA_817").Tokens.Contains("GILA_817t") && DungeonRunContent.Get("GILA_818").Tokens.Contains("GILA_818t")
    && DungeonRunContent.Get("GILA_819").Tokens.Contains("GILA_819t") && DungeonRunContent.Get("GILA_854").Tokens.Contains("GILA_854t")
    && DungeonRunContent.Get("GILA_851a").Tokens.Contains("GILA_851b") && DungeonRunContent.Get("GILA_851b").Tokens.Contains("GILA_851c"));

// ---------------------------------------------------------------- T2 data, validator, admission
{
    var errors = DungeonRunValidator.Validate(data, id => clientIds.Contains(transport.TryGetValue(id, out var sent) ? sent : id));
    Check("T2 shipped data validates with 0 errors (client check through the transported id)", errors.Count == 0, string.Join(" | ", errors.Take(5)));
    var legacy = DungeonRunData.Parse(runJson, poolJson, npcJson);
    var le = DungeonRunValidator.Validate(legacy, null);
    Check("T2 the data without the catalog validates with 0 errors", le.Count == 0, string.Join(" | ", le.Take(3)));
    foreach (string id in f5)
    {
        var t = data.FindTreasure(id);
        bool passive = passives.Contains(id), stage = stages.Contains(id);
        Check($"T2 {id}: an {(passive ? "passive" : "active")} act-run treasure row, {(stage ? "never offered (an in-match stage)" : "offered")}, MonsterHunt pool, scoped to the four Monster Hunt starters",
            t != null && t.Active == !passive && t.Offered == !stage && !t.Legacy && !t.Signature && t.Runs == DungeonRunTreasure.RunsAct && t.Pool == "MonsterHunt"
            && t.Scope.Decks.OrderBy(x => x).SequenceEqual(mhDecks.OrderBy(x => x)) && t.Scope.Classes.Count == 0 && t.Scope.Powers.Count == 0 && t.UpgradesTo == null);
    }
    var families = new Dictionary<string, string>
    {
        ["GILA_801"] = "run-treasure", ["GILA_803"] = "run-treasure", ["GILA_804"] = "run-treasure", ["GILA_810"] = "run-treasure", ["GILA_820"] = "run-treasure",
        ["GILA_824"] = "run-treasure", ["GILA_825"] = "run-treasure", ["GILA_826"] = "run-treasure", ["GILA_850b"] = "run-treasure", ["GILA_853b"] = "run-treasure",
        ["GILA_854"] = "run-treasure", ["GILA_854t"] = "run-treasure", ["GILA_802"] = "run-treasure-discover",
        ["GILA_811"] = "run-treasure-hidden-copy", ["GILA_812"] = "run-treasure-hidden-copy", ["GILA_827"] = "run-treasure-hidden-copy",
    };
    foreach (string id in DungeonRunContent.EvolvesTo.Keys.Concat(DungeonRunContent.EvolvesTo.Values).Concat(new[] { "GILA_817t", "GILA_818t", "GILA_819t" }).Distinct())
        families[id] = "run-treasure-evolving";
    foreach (var pair in families.OrderBy(p => p.Key, StringComparer.Ordinal))
    {
        string id = pair.Key;
        var p = DungeonRunAdmission.Classify(C(id), id, AdmitScope.Player, null, catalogRun: true);
        var b = DungeonRunAdmission.Classify(C(id), id, AdmitScope.Boss, null, catalogRun: true);
        Check($"T2 admission {id}: the {pair.Value} row admits the player (catalog run), never the boss",
            p.Subject && p.Admitted && !b.Admitted && DungeonRunAdmission.RuleFor(id)?.Family == pair.Value, p.Family + " " + p.Admit);
        Check($"T2 admission {id}: the boss's generation never yields it", DungeonRunAdmission.GeneratedRejectsBoss(true).Contains(id));
    }
    Check("T2 admission GILA_806: a from-own-deck row for the player only (design section 6), never the boss; the boss's generation never yields it",
        DungeonRunAdmission.RuleFor("GILA_806").Family == "from-own-deck" && DungeonRunAdmission.RuleFor("GILA_806").Admit == AdmitScope.Player
        && DungeonRunAdmission.Classify(C("GILA_806"), "GILA_806", AdmitScope.Player, null, catalogRun: true).Admitted
        && !DungeonRunAdmission.Classify(C("GILA_806"), "GILA_806", AdmitScope.Boss, null, catalogRun: true).Admitted
        && DungeonRunAdmission.GeneratedRejectsBoss(true).Contains("GILA_806"));
    Check("T2 every active F5 id has an admission row; the passives (run content entries) have none",
        f5.Where(id => !passives.Contains(id)).All(id => DungeonRunAdmission.RuleFor(id) != null) && passives.All(id => DungeonRunAdmission.RuleFor(id) == null));
}

// ---------------------------------------------------------------- T3 offers: every hunter, never another run; the stages never
{
    var offered = new Dictionary<string, HashSet<string>>();
    foreach (var cls in data.Classes)
    {
        var set = offered[cls.Id] = new HashSet<string>();
        int seeds = hunters.Contains(cls.Id) ? 400 : 60;
        for (long seed = 1; seed <= seeds; seed++)
        {
            var s = run.NewRun(all, cls.Id, cls.DefaultDeck, cls.DefaultPower, seed, signatureId: run.SignatureOffer(all, run.Data.FindClass(cls.Id), seed).FirstOrDefault());
            for (int k = 0; k < 3 && run.OpenTreasureDraft(s); k++)
            {
                set.UnionWith(s.OpenDraft.Offered);
                run.PickTreasure(s, s.OpenDraft.Offered[0]);
            }
        }
    }
    var mh = new HashSet<string>(hunters.SelectMany(h => offered[h]));
    foreach (string id in offeredF5) Check($"T3 {id} is offered to Monster Hunt runs", mh.Contains(id));
    foreach (string h in hunters)
        Check($"T3 {h}: offered shared Monster Hunt treasures (at least 18 of the 24)", offered[h].Count(offeredF5.Contains) >= 18, offered[h].Count(offeredF5.Contains).ToString());
    Check("T3 no in-match stage (816b/c, 818, 819, 821b/c, 851b/c, 852b/c) is ever offered", !offered.Values.Any(v => v.Overlaps(stages)));
    Check("T3 no other run is offered a shared Monster Hunt treasure (deck scope)",
        offered.Where(p => !hunters.Contains(p.Key)).All(p => !p.Value.Overlaps(f5)),
        string.Join(";", offered.Where(p => !hunters.Contains(p.Key) && p.Value.Overlaps(f5)).Select(p => p.Key)));
    var sp = NewAct(Darius, 9);
    run.OpenTreasureDraft(sp);
    sp.OpenDraft.Offered[0] = "GILA_816a";
    int before = sp.Deck.Count;
    run.PickTreasure(sp, "GILA_816a");
    Check("T3 PickTreasure GILA_816a on a Darius run: the run deck gains the first stage (origin treasure)",
        sp.Deck.Count == before + 1 && sp.Cards.Last().Id == "GILA_816a" && sp.Cards.Last().Origin == DungeonRunDeck.OriginTreasure && sp.ActiveTreasures.Contains("GILA_816a"));
    var pool = run.TreasurePool(sp, "MonsterHunt");
    var ctx = run.BuildContext(sp);
    Check("T3 the run context's Monster Hunt pool: every offered active F5 id and Darius's own, no stage, no other hunter's, no Dungeon Run treasure",
        offeredF5.Where(id => !passives.Contains(id)).All(pool.Contains) && !stages.Any(pool.Contains) && pool.Contains("GILA_610") && !pool.Contains("GILA_401")
        && pool.All(id => id.StartsWith("GILA_")) && ctx.TreasurePool.Any(id => id.StartsWith("LOOTA_"))
        && pool.OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(ctx.TreasurePool.Where(id => ctx.TreasurePoolTags.TryGetValue(id, out var t) && t == "MonsterHunt")),
        string.Join(",", pool));
}

// ---------------------------------------------------------------- T4 GILA_801 Gattling Gunner
{
    var (s, plan, g) = Fight(Tess, 301, new[] { "GILA_801" });
    Check("T4 GILA_801 is a card of the run deck the match plan deals", FromRunDeck(s, plan, "GILA_801"));
    var p1 = g.Player1; var p2 = g.Player2;
    var gun = Draw(g, "GILA_801"); Mana(p1, 10);
    var a = Board(g, p2, "CS2_182"); var b = Board(g, p2, "CS2_182");
    Play(g, gun);
    Check("T4 GILA_801: playing the Gunner itself deals nothing", a.Damage == 0 && b.Damage == 0 && ((Minion)gun).AttackDamage == 5 && ((Minion)gun).Health == 3);
    Play(g, ToHand(g, p1, "CS2_168"));
    Check("T4 GILA_801: a 1-Cost card deals 1 to one random enemy minion", a.Damage + b.Damage == 1 && (a.Damage == 0 || b.Damage == 0), $"{a.Damage} {b.Damage}");
    Play(g, ToHand(g, p1, "CS2_182"));
    Check("T4 GILA_801: a 4-Cost card deals 4 to one random enemy minion", a.Damage + b.Damage == 5, $"{a.Damage} {b.Damage}");
    Play(g, ToHand(g, p1, "CS2_231"));
    Check("T4 GILA_801: a 0-Cost card deals nothing", a.Damage + b.Damage == 5);
    EndTurn(g); Mana(p2, 10);
    int dmg = p1.BoardZone.GetAll().Sum(m => m.Damage);
    g.Process(PlayCardTask.Any(p2, ToHand(g, p2, "CS2_182")));
    Check("T4 GILA_801: the opponent's plays do not fire it", p1.BoardZone.GetAll().Sum(m => m.Damage) == dmg);
}

// ---------------------------------------------------------------- T5 GILA_802 Cartographer (H2 treasure Discover)
{
    var (s, plan, g) = Fight(Shaw, 311, new[] { "GILA_802" });
    var p1 = g.Player1;
    var cart = Draw(g, "GILA_802"); Mana(p1, 10);
    var mhPool = run.TreasurePool(s, "MonsterHunt");
    Check("T5 the match's run context carries the Monster Hunt treasure pool",
        RunHelpers.TreasurePool(g, "MonsterHunt").SequenceEqual(mhPool) && mhPool.Count >= 20 && RunHelpers.TreasurePool(g).Count > mhPool.Count);
    Play(g, cart);
    var offers = p1.Choice?.Choices.Select(id => g.IdEntityDic[id].Card.Id).ToList() ?? new List<string>();
    Check("T5 GILA_802: a 1/5 Battlecry Discovers 3 distinct Monster Hunt treasures of the run's pool, never itself or an in-match stage",
        ((Minion)cart).AttackDamage == 1 && offers.Count == 3 && offers.Distinct().Count() == 3 && offers.All(mhPool.Contains)
        && !offers.Contains("GILA_802") && !offers.Any(stages.Contains), string.Join(",", offers));
    int pick = p1.Choice.Choices[1];
    string picked = g.IdEntityDic[pick].Card.Id;
    g.Process(ChooseTask.Pick(p1, pick));
    Check("T5 GILA_802: the picked treasure is in my hand", p1.HandZone.Any(e => e.Card.Id == picked) && p1.Choice == null, picked);
    string Offers(int seed)
    {
        var (_, _, gs) = Fight(Shaw, seed, new[] { "GILA_802" });
        var c2 = Draw(gs, "GILA_802"); Mana(gs.Player1, 10);
        Play(gs, c2);
        return string.Join(",", gs.Player1.Choice.Choices.Select(id => gs.IdEntityDic[id].Card.Id));
    }
    Check("T5 GILA_802: the same seed offers the same treasures", Offers(312) == Offers(312));
    // The boss holds a Cartographer (GILA_906's hand swap): its generation rejects every run treasure, so it discovers none.
    EndTurn(g);
    var p2 = g.Player2; Mana(p2, 10);
    var bossCart = ToHand(g, p2, "GILA_802");
    var bossCandidates = DungeonRunContent.TreasureCandidates(p2, "MonsterHunt").Select(c => c.Id).ToList();
    var bossRejects = DungeonRunAdmission.GeneratedRejectsBoss(true);
    Check("T5 GILA_802 in the boss's hand: it may discover only what the boss's generation admits (no player-only treasure)",
        bossCandidates.All(id => !bossRejects.Contains(id)), string.Join(",", bossCandidates));
    bool bossPlayed = g.Process(PlayCardTask.Any(p2, bossCart));
    if (p2.Choice != null) g.Process(ChooseTask.Pick(p2, p2.Choice.Choices[0]));
    Check("T5 GILA_802 played by the boss: no fault, no pending choice", bossPlayed && p2.Choice == null);
    var bare = new Game(new GameConfig { StartPlayer = 1, Player1HeroClass = CardClass.ROGUE, Player2HeroClass = CardClass.MAGE, FillDecks = true, Shuffle = false, History = false, Logging = false });
    bare.StartGame();
    Check("T5 GILA_802 outside a run match: the pool is empty, nothing to Discover", DungeonRunContent.TreasureCandidates(bare.Player1, "MonsterHunt").Count == 0);
}

// ---------------------------------------------------------------- T6 GILA_803 Gilnean Vigilante
{
    var (s, plan, g) = Fight(Darius, 321, new[] { "GILA_803" });
    var p1 = g.Player1;
    var vig = Draw(g, "GILA_803"); Mana(p1, 10);
    var yeti = Board(g, g.Player2, "CS2_182");
    int hand = p1.HandZone.Count;
    Play(g, vig, yeti);
    Check("T6 GILA_803: the chosen minion is destroyed and my hand is filled with Coins (10 cards)",
        yeti.IsDead && p1.HandZone.Count == 10 && p1.HandZone.Count(e => e.Card.Id == "GAME_005") == 10 - (hand - 1), $"{p1.HandZone.Count} {p1.HandZone.Count(e => e.Card.Id == "GAME_005")}");
    var (_, _, g2) = Fight(Darius, 322, new[] { "GILA_803" });
    var v2 = Draw(g2, "GILA_803"); Mana(g2.Player1, 10);
    Check("T6 GILA_803 with no minion in play: playable without a target; Coins only", Play(g2, v2) && g2.Player1.HandZone.Count == 10);
}

// ---------------------------------------------------------------- T7 GILA_804 Holy Book
{
    var (s, plan, g) = Fight(Tess, 331, new[] { "GILA_804" });
    var p1 = g.Player1; var p2 = g.Player2;
    var book = Draw(g, "GILA_804"); Mana(p1, 10);
    var hoarder = Board(g, p2, "EX1_096");
    Generic.AddEnchantmentBlock(g, C("CS2_092e"), p2.Hero, hoarder);
    int p2Hand = p2.HandZone.Count;
    Play(g, book, hoarder);
    var copy = p1.BoardZone.GetAll().FirstOrDefault(m => m.Card.Id == "EX1_096");
    Check("T7 GILA_804: the enemy Loot Hoarder is silenced and destroyed (no Deathrattle draw)", hoarder.IsDead && p2.HandZone.Count == p2Hand, $"{p2.HandZone.Count} vs {p2Hand}");
    Check("T7 GILA_804: I summon a silenced 10/10 copy of it (Holy Essence)", copy != null && copy.AttackDamage == 10 && copy.Health == 10 && copy.IsSilenced
        && copy.AppliedEnchantments.Any(e => e.Card.Id == "GILA_804e"), copy == null ? "none" : $"{copy.AttackDamage}/{copy.Health} {copy.IsSilenced}");
    int myHand = p1.HandZone.Count;
    Kill(g, copy);
    Check("T7 GILA_804: the copy has no Deathrattle", p1.HandZone.Count == myHand);
    var book2 = ToHand(g, p1, "GILA_804"); Mana(p1, 10); Board(g, p1, "CS2_231"); Board(g, p2, "CS2_231");
    var bookOptions = p1.Options().OfType<PlayCardTask>().Where(o => o.Source == book2).ToList();
    Check("T7 GILA_804 needs a minion target", bookOptions.Count > 0 && bookOptions.All(o => o.Target is Minion));
}

// ---------------------------------------------------------------- T8 GILA_805 Entrenchment (passive)
{
    var (s, plan, g) = Fight(Toki, 341, new[] { "GILA_805" });
    var p1 = g.Player1;
    Check("T8 GILA_805: the player enchantment GILA_805e is attached at match start", p1.AppliedEnchantments.Any(e => e.Card.Id == "GILA_805e"));
    Mana(p1, 10);
    var yeti = ToHand(g, p1, "CS2_182");
    Play(g, yeti);
    var enemy = Board(g, g.Player2, "CS2_182");
    Check("T8 GILA_805: my minions have +2 Health (Yeti 4/7); the enemy's do not", ((Minion)yeti).AttackDamage == 4 && ((Minion)yeti).Health == 7 && enemy.Health == 5,
        $"{((Minion)yeti).Health} {enemy.Health}");
}

// ---------------------------------------------------------------- T9 GILA_806 Brass Lantern
{
    var (s, plan, g) = Fight(Darius, 351, new[] { "GILA_806" });
    var p1 = g.Player1;
    var lantern = Draw(g, "GILA_806"); Mana(p1, 10); Clear(p1); Generic.AddHandPhase(p1, lantern);
    var deckIds = new HashSet<string>(p1.DeckZone.Select(e => e.Card.Id));
    int deck = p1.DeckZone.Count;
    Play(g, lantern);
    var picked = new List<string>();
    int rounds = 0;
    bool fine = true;
    while (p1.Choice != null && rounds < 5)
    {
        var offer = p1.Choice.Choices.Select(id => g.IdEntityDic[id].Card.Id).ToList();
        fine &= offer.Count == Math.Min(3, deckIds.Count) && offer.Distinct().Count() == offer.Count && offer.All(deckIds.Contains);
        picked.Add(offer[0]);
        g.Process(ChooseTask.Pick(p1, p1.Choice.Choices[0]));
        rounds++;
    }
    Check("T9 GILA_806: three Discovers, each of 3 distinct cards of my deck", rounds == 3 && fine, rounds + " " + string.Join(",", picked));
    Check("T9 GILA_806: three copies in my hand; the deck is unchanged", p1.HandZone.Count == 3 && p1.HandZone.Select(e => e.Card.Id).SequenceEqual(picked) && p1.DeckZone.Count == deck);
}

// ---------------------------------------------------------------- T10 GILA_811 Spyglass (hidden hand copy)
{
    var (s, plan, g) = Fight(Tess, 361, new[] { "GILA_811" });
    var p1 = g.Player1; var p2 = g.Player2;
    var glass = Draw(g, "GILA_811"); Mana(p1, 10); Clear(p1); Clear(p2); Generic.AddHandPhase(p1, glass);
    var x = ToHand(g, p2, "CS2_182"); var y = ToHand(g, p2, "CS2_029"); var z = ToHand(g, p2, "EX1_279");
    Play(g, glass);
    var copies = p1.HandZone.Where(e => e.Card.Id != "GILA_811").ToList();
    var echo = p1.HandZone.FirstOrDefault(e => e.Card.Id == "GILA_811");
    var c0 = copies.FirstOrDefault();
    Check("T10 GILA_811: one copy of a card of the opponent's hand, costing (3) less; the opponent keeps its hand",
        copies.Count == 1 && new[] { "CS2_182", "CS2_029", "EX1_279" }.Contains(c0.Card.Id) && c0.Cost == Math.Max(0, c0.Card.Cost - 3)
        && c0.Controller == p1 && p2.HandZone.SequenceEqual(new[] { x, y, z }), c0 == null ? "none" : c0.Card.Id + " " + c0.Cost);
    Check("T10 GILA_811: Echo leaves a copy of Spyglass in my hand this turn", echo != null && echo.IsEcho);
    var sim = g.CloneForSimulation(1);
    var known = PublicHandKnowledge.Read(g, p1, sim.HistoryFacts.ProjectForFairView(sim, p2.PlayerId));
    Check("T10 GILA_811: the boss's public hand knowledge does not know which of its cards was copied", !known.ContainsKey(c0.Id),
        string.Join(",", known.Select(k => k.Key + "=" + k.Value.Id)));
    var fair = FairBotState.Create(g, p2.PlayerId, 7, DeckStyle.Tempo, new FairOptions { RemoveOpponentSecrets = true });
    Check("T10 GILA_811: the boss's fair view builds over the copy", fair != null);
    Play(g, echo);
    Check("T10 GILA_811: the Echo copy copies again", p1.HandZone.Count(e => e.Card.Id != "GILA_811") == 2);
    EndTurn(g);
    Check("T10 GILA_811: the Echo copy leaves at the end of the turn; the copied cards stay", !p1.HandZone.Any(e => e.Card.Id == "GILA_811") && p1.HandZone.Count >= 2);
    var (_, _, g2) = Fight(Tess, 362, new[] { "GILA_811" });
    var gl2 = Draw(g2, "GILA_811"); Mana(g2.Player1, 10); Clear(g2.Player2); int h2 = g2.Player1.HandZone.Count;
    Check("T10 GILA_811 against an empty hand: cast, nothing copied", Play(g2, gl2) && g2.Player1.HandZone.Count(e => e.Card.Id != "GILA_811") == h2 - 1);
}

// ---------------------------------------------------------------- T11 GILA_812 Bestiary (hidden deck copy)
{
    var (s, plan, g) = Fight(Toki, 371, new[] { "GILA_812" });
    var p1 = g.Player1; var p2 = g.Player2;
    var book = Draw(g, "GILA_812"); Mana(p1, 10);
    var deckMinions = new HashSet<string>(p2.DeckZone.OfType<Minion>().Select(m => m.Card.Id));
    int oppDeck = p2.DeckZone.Count;
    Board(g, p1, "CS2_231");
    Play(g, book);
    var mine = p1.BoardZone.GetAll().Skip(1).ToList();
    Check("T11 GILA_812: my board is filled (7) with copies of minions of the boss's deck; the boss's deck is unchanged",
        p1.BoardZone.Count == 7 && mine.Count == 6 && mine.All(m => deckMinions.Contains(m.Card.Id) && m.Controller == p1) && p2.DeckZone.Count == oppDeck,
        string.Join(",", mine.Select(m => m.Card.Id)));
    var (_, _, g2) = Fight(Toki, 372, new[] { "GILA_812" });
    var b2 = Draw(g2, "GILA_812"); Mana(g2.Player1, 10);
    Thin(g2, g2.Player2, e => !(e is Minion));
    Check("T11 GILA_812 against a deck with no minion: cast, nothing summoned", Play(g2, b2) && g2.Player1.BoardZone.Count == 0);
}

// ---------------------------------------------------------------- T12 GILA_813 Expedite, T13 GILA_814 Caltrops (passives)
{
    var (s, plan, g) = Fight(Shaw, 381, new[] { "GILA_813", "GILA_814" });
    var p1 = g.Player1; var p2 = g.Player2;
    Check("T12 GILA_813, GILA_814: their player enchantments are attached at match start",
        p1.AppliedEnchantments.Any(e => e.Card.Id == "GILA_813e") && p1.AppliedEnchantments.Any(e => e.Card.Id == "GILA_814e"));
    var yeti = ToHand(g, p1, "CS2_182"); var bolt = ToHand(g, p1, "CS2_029");
    var enemyYeti = ToHand(g, p2, "CS2_182");
    Check("T12 GILA_813: my minions cost (1) less; my spells and the boss's minions do not", yeti.Cost == 3 && bolt.Cost == 4 && enemyYeti.Cost == 4);
    Mana(p1, 10);
    Play(g, yeti);
    Check("T13 GILA_814: my own minion is not hit", ((Minion)yeti).Damage == 0);
    EndTurn(g); Mana(p2, 10);
    g.Process(PlayCardTask.Any(p2, enemyYeti));
    Check("T13 GILA_814: after the boss plays a minion it takes 1 damage", ((Minion)enemyYeti).Damage == 1);
    var shield = ToHand(g, p2, "EX1_008");
    g.Process(PlayCardTask.Any(p2, shield));
    Check("T13 GILA_814: a Divine Shield minion loses its shield, no damage", !((Minion)shield).HasDivineShield && ((Minion)shield).Damage == 0);
    var wisp = ToHand(g, p2, "CS2_231");
    g.Process(PlayCardTask.Any(p2, wisp));
    g.DeathProcessingAndAuraUpdate();
    Check("T13 GILA_814: a 1-Health minion dies", wisp.Zone?.Type == Zone.GRAVEYARD || ((Minion)wisp).IsDead);
}

// ---------------------------------------------------------------- T14 GILA_816a-c Coin Pouch chain
{
    string Chain(int seed)
    {
        var (s, plan, g) = Fight(Darius, seed, new[] { "GILA_816a" });
        var p1 = g.Player1;
        var pouch = Draw(g, "GILA_816a"); Mana(p1, 10);
        Play(g, pouch);
        var m3 = p1.BoardZone.GetAll().Where(m => !Cannons.IsCannon(m)).ToList();
        var sack = p1.DeckZone.FirstOrDefault(e => e.Card.Id == "GILA_816b");
        Check($"T14 GILA_816a ({seed}): a random scripted 3-Cost minion is summoned; Sack of Coins is shuffled into my deck",
            m3.Count == 1 && m3[0].Card.Cost == 3 && m3[0].Card.Implemented && sack != null && !p1.DeckZone.Any(e => e.Card.Id == "GILA_816a"),
            string.Join(",", m3.Select(m => m.Card.Id + ":" + m.Card.Cost)));
        Draw(g, "GILA_816b"); Mana(p1, 10);
        Play(g, sack);
        var m6 = p1.BoardZone.GetAll().Where(m => !Cannons.IsCannon(m)).Skip(1).ToList();
        var hefty = p1.DeckZone.FirstOrDefault(e => e.Card.Id == "GILA_816c");
        Check($"T14 GILA_816b ({seed}): a random 6-Cost minion; Hefty Sack of Coins is shuffled into my deck",
            m6.Count == 1 && m6[0].Card.Cost == 6 && hefty != null, string.Join(",", m6.Select(m => m.Card.Id + ":" + m.Card.Cost)));
        foreach (var m in p1.BoardZone.GetAll().Where(m => !Cannons.IsCannon(m)).ToArray()) { p1.BoardZone.Remove(m); p1.SetasideZone.Add(m); }
        Draw(g, "GILA_816c"); Mana(p1, 10);
        int deck = p1.DeckZone.Count;
        Play(g, hefty);
        var m369 = p1.BoardZone.GetAll().Where(m => !Cannons.IsCannon(m)).Select(m => m.Card.Cost).ToList();
        Check($"T14 GILA_816c ({seed}): a random 3-, 6- and 9-Cost minion; nothing more is shuffled", m369.SequenceEqual(new[] { 3, 6, 9 }) && p1.DeckZone.Count == deck,
            string.Join(",", m369));
        return string.Join(",", p1.GraveyardZone.Select(e => e.Card.Id)) + "|" + string.Join(",", p1.BoardZone.GetAll().Select(m => m.Card.Id));
    }
    Check("T14 Coin Pouch: the same seed summons the same minions", Chain(391) == Chain(391));
}

// ---------------------------------------------------------------- T15 GILA_817-819 Curio chain
{
    var (s, plan, g) = Fight(Tess, 401, new[] { "GILA_817" });
    var p1 = g.Player1;
    var curio = Draw(g, "GILA_817"); Mana(p1, 10);
    Play(g, curio);
    var ghosts = p1.BoardZone.GetAll().ToList();
    var haunted = p1.DeckZone.FirstOrDefault(e => e.Card.Id == "GILA_818");
    Check("T15 GILA_817: three 1/1 Ghosts; Haunted Curio is shuffled into my deck",
        ghosts.Count == 3 && ghosts.All(m => m.Card.Id == "GILA_817t" && m.AttackDamage == 1 && m.Health == 1) && haunted != null);
    foreach (var m in ghosts) Kill(g, m);
    Draw(g, "GILA_818"); Mana(p1, 10);
    Play(g, haunted);
    var g2 = p1.BoardZone.GetAll().ToList();
    var cursed = p1.DeckZone.FirstOrDefault(e => e.Card.Id == "GILA_819");
    Check("T15 GILA_818: three 2/2 Ghosts; Cursed Curio is shuffled into my deck", g2.Count == 3 && g2.All(m => m.Card.Id == "GILA_818t" && m.AttackDamage == 2 && m.Health == 2) && cursed != null);
    foreach (var m in g2) Kill(g, m);
    Draw(g, "GILA_819"); Mana(p1, 10);
    int deck = p1.DeckZone.Count;
    Play(g, cursed);
    var g3 = p1.BoardZone.GetAll().ToList();
    Check("T15 GILA_819: three 3/3 Ghosts with Lifesteal; nothing more is shuffled",
        g3.Count == 3 && g3.All(m => m.Card.Id == "GILA_819t" && m.AttackDamage == 3 && m.Health == 3 && m.HasLifeSteal) && p1.DeckZone.Count == deck);
    p1.Hero.Damage = 10;
    EndTurn(g); EndTurn(g);
    int hp = p1.Hero.Health;
    g.Process(MinionAttackTask.Any(p1, g3[0], g.Player2.Hero));
    Check("T15 GILA_819t: a Ghost's attack heals my hero (Lifesteal)", p1.Hero.Health == hp + 3, $"{hp} -> {p1.Hero.Health}");
}

// ---------------------------------------------------------------- T16 GILA_821a-c Mob chain
{
    var (s, plan, g) = Fight(Shaw, 411, new[] { "GILA_821a" });
    var p1 = g.Player1; var p2 = g.Player2;
    var mob = Draw(g, "GILA_821a"); Mana(p1, 10);
    Check("T16 GILA_821a: not playable with no enemy minion", !p1.Options().OfType<PlayCardTask>().Any(o => o.Source == mob));
    var e1 = Board(g, p2, "CS2_182"); var e2 = Board(g, p2, "CS2_182"); var mine = Board(g, p1, "CS2_182");
    Play(g, mob);
    var angry = p1.DeckZone.FirstOrDefault(e => e.Card.Id == "GILA_821b");
    Check("T16 GILA_821a: one random enemy minion is destroyed (mine stays); Angry Mob is shuffled into my deck",
        (e1.IsDead ^ e2.IsDead) && !mine.IsDead && angry != null);
    var e3 = Board(g, p2, "CS2_182"); var e4 = Board(g, p2, "CS2_182");
    Draw(g, "GILA_821b"); Mana(p1, 10);
    int alive = p2.BoardZone.Count;
    Play(g, angry);
    var crazed = p1.DeckZone.FirstOrDefault(e => e.Card.Id == "GILA_821c");
    Check("T16 GILA_821b: two random enemy minions are destroyed; Crazed Mob is shuffled into my deck", p2.BoardZone.Count == alive - 2 && crazed != null, $"{alive} -> {p2.BoardZone.Count}");
    var hoarder = Board(g, p2, "EX1_096"); Board(g, p2, "CS2_182");
    Draw(g, "GILA_821c"); Mana(p1, 10);
    int hand = p2.HandZone.Count, deck = p1.DeckZone.Count;
    Play(g, crazed);
    Check("T16 GILA_821c: every enemy minion is silenced and destroyed (Loot Hoarder draws nothing); mine stays; nothing more is shuffled",
        p2.BoardZone.Count == 0 && p2.HandZone.Count == hand && !mine.IsDead && p1.DeckZone.Count == deck);
    var (_, _, g2) = Fight(Shaw, 412, new[] { "GILA_821c" });
    var c2 = Draw(g2, "GILA_821c"); Mana(g2.Player1, 10);
    Check("T16 GILA_821c: playable on an empty board", Play(g2, c2));
}

// ---------------------------------------------------------------- T17 GILA_851a-c Tracker chain (self-transforming minion)
{
    var (s, plan, g) = Fight(Darius, 421, new[] { "GILA_851a" });
    var p1 = g.Player1;
    var tracker = Draw(g, "GILA_851a"); Mana(p1, 10);
    var enemy = Board(g, g.Player2, "CS2_120");
    Play(g, tracker);
    var t1 = (Minion)tracker;
    Check("T17 GILA_851a: a 3/3 Rush that can attack a minion at once", t1.AttackDamage == 3 && t1.Health == 3
        && p1.Options().OfType<MinionAttackTask>().Any(o => o.Source == t1 && o.Target == enemy));
    g.Process(MinionAttackTask.Any(p1, t1, enemy));
    EndTurn(g);
    Minion Stage(string id) => p1.BoardZone.GetAll().FirstOrDefault(m => m.Card.Id == id);
    Check("T17 GILA_851a: it does not change on the boss's turn", Stage("GILA_851a") != null);
    EndTurn(g);
    var t2 = Stage("GILA_851b");
    Check("T17 GILA_851a: at the start of my turn it transforms into a 5/5 Transforming Tracker (damage gone)", t2 != null && Stage("GILA_851a") == null && t2.AttackDamage == 5 && t2.Health == 5
        && t2.IsRush, t2 == null ? string.Join(",", p1.BoardZone.GetAll().Select(m => m.Card.Id)) : "");
    EndTurn(g); EndTurn(g);
    var t3 = Stage("GILA_851c");
    Check("T17 GILA_851b: next turn a 7/7 Worgen Tracker with Rush and Windfury", t3 != null && t3.AttackDamage == 7 && t3.Health == 7 && t3.HasWindfury && t3.IsRush);
    EndTurn(g); EndTurn(g);
    Check("T17 GILA_851c: the last stage stays", Stage("GILA_851c") != null && p1.BoardZone.GetAll().Count(m => !Cannons.IsCannon(m)) == 1);
    var (_, _, g2) = Fight(Darius, 422, new[] { "GILA_851a" });
    var tr2 = Draw(g2, "GILA_851a"); Mana(g2.Player1, 10); Play(g2, tr2);
    g2.Player1.BoardZone.GetAll().First(m => m.Card.Id == "GILA_851a").Silence();
    EndTurn(g2); EndTurn(g2);
    Check("T17 GILA_851a silenced: it no longer transforms", g2.Player1.BoardZone.GetAll().Any(m => m.Card.Id == "GILA_851a"));
}

// ---------------------------------------------------------------- T18 GILA_852a-c Militia Horn chain
{
    var (s, plan, g) = Fight(Tess, 431, new[] { "GILA_852a" });
    var p1 = g.Player1;
    var horn = Draw(g, "GILA_852a"); Mana(p1, 10);
    var a = Board(g, p1, "CS2_231"); var b = Board(g, p1, "CS2_182"); var enemy = Board(g, g.Player2, "CS2_231");
    Play(g, horn);
    var next = p1.DeckZone.FirstOrDefault(e => e.Card.Id == "GILA_852b");
    Check("T18 GILA_852a: my minions get +1/+1 and Taunt (Morale Boost); the enemy's do not; Militia Horn is shuffled into my deck",
        a.AttackDamage == 2 && a.Health == 2 && a.HasTaunt && b.AttackDamage == 5 && b.Health == 6 && b.HasTaunt && !enemy.HasTaunt && enemy.Health == 1 && next != null);
    Draw(g, "GILA_852b"); Mana(p1, 10);
    Play(g, next);
    var vet = p1.DeckZone.FirstOrDefault(e => e.Card.Id == "GILA_852c");
    Check("T18 GILA_852b: +2/+2 and Taunt (Rallied); Veteran's Militia Horn is shuffled into my deck", a.AttackDamage == 4 && a.Health == 4 && vet != null);
    Draw(g, "GILA_852c"); Mana(p1, 10);
    int deck = p1.DeckZone.Count;
    Play(g, vet);
    Check("T18 GILA_852c: +4/+4, Taunt and Divine Shield (United); nothing more is shuffled",
        a.AttackDamage == 8 && a.Health == 8 && a.HasDivineShield && a.HasTaunt && b.HasDivineShield && p1.DeckZone.Count == deck && !enemy.HasDivineShield);
    Generic.DamageCharFunc(enemy, a, 3, false);
    Check("T18 GILA_852c: the shield pops on the first damage", !a.HasDivineShield && a.Health == 8);
}

// ---------------------------------------------------------------- T19 evolving artifacts are in-match only: the run deck keeps the first stage
{
    var (s, plan, g) = Fight(Toki, 441, new[] { "GILA_816a", "GILA_817", "GILA_821a", "GILA_852a", "GILA_851a" });
    var p1 = g.Player1; Mana(p1, 10);
    Board(g, g.Player2, "CS2_182");
    foreach (string id in new[] { "GILA_816a", "GILA_817", "GILA_821a", "GILA_852a", "GILA_851a" })
    {
        var e = Draw(g, id); Mana(p1, 10);
        if (p1.BoardZone.IsFull) foreach (var m in p1.BoardZone.GetAll().Take(4).ToArray()) Kill(g, m);
        Play(g, e);
    }
    EndTurn(g); EndTurn(g);
    Check("T19 the evolved stages are in the match (816b, 818, 821b, 852b in the deck; 851b on the board)",
        new[] { "GILA_816b", "GILA_818", "GILA_821b", "GILA_852b" }.All(id => p1.DeckZone.Any(e => e.Card.Id == id)) && p1.BoardZone.GetAll().Any(m => m.Card.Id == "GILA_851b"));
    Check("T19 the match asks the run to keep nothing (no run edit, no slot log)", g.RunResult == null || g.RunResult.IsEmpty);
    var before = s.Deck.ToList();
    var plan2 = run.BuildPlan(s);
    Check("T19 the run deck still holds the first stages, and the next match plan deals them",
        s.Deck.SequenceEqual(before) && new[] { "GILA_816a", "GILA_817", "GILA_821a", "GILA_852a", "GILA_851a" }.All(id => s.Deck.Contains(id) && plan2.PlayerDeck.Any(c => c.Id == id))
        && !s.Deck.Any(stages.Contains));
}

// ---------------------------------------------------------------- T20 GILA_820 Ricochet Shot
{
    string Shots(int seed, bool minions)
    {
        var (s, plan, g) = Fight(Shaw, seed, new[] { "GILA_820" });
        var p1 = g.Player1; var p2 = g.Player2;
        var shot = Draw(g, "GILA_820"); Mana(p1, 10);
        p1.Hero.BaseHealth = 200; p1.Hero.Damage = 0; p2.Hero.BaseHealth = 200; p2.Hero.Damage = 0;
        var mine = minions ? Board(g, p1, "CS2_200") : null; var theirs = minions ? Board(g, p2, "CS2_200") : null;
        Play(g, shot);
        int total = p1.Hero.Damage + p2.Hero.Damage + (mine?.Damage ?? 0) + (theirs?.Damage ?? 0);
        if (!minions)
            Check($"T20 GILA_820 ({seed}): 13 hits of 3 at random heroes (39 damage)", total == 39 && p1.Hero.Damage % 3 == 0, $"{p1.Hero.Damage}+{p2.Hero.Damage}");
        else
            Check($"T20 GILA_820 ({seed}): 13 hits of 3 among both heroes and the two 6/7s (a dying minion is no longer a target)",
                total == 39 && (mine.Damage <= 9 && theirs.Damage <= 9), $"{p1.Hero.Damage} {p2.Hero.Damage} {mine.Damage} {theirs.Damage}");
        return $"{p1.Hero.Damage},{p2.Hero.Damage},{mine?.Damage},{theirs?.Damage}";
    }
    Shots(451, false);
    Check("T20 GILA_820: the same seed hits the same targets", Shots(452, true) == Shots(452, true));
    var (_, _, gk) = Fight(Shaw, 453, new[] { "GILA_820", "LOOTA_825" });
    var sk = Draw(gk, "GILA_820"); Mana(gk.Player1, 10);
    gk.Player1.Hero.BaseHealth = 300; gk.Player1.Hero.Damage = 0; gk.Player2.Hero.BaseHealth = 300; gk.Player2.Hero.Damage = 0;
    Play(gk, sk);
    Check("T20 GILA_820 with Spell Damage +3 (Robe of the Magi, LOOTA_825): 13 hits of 6 (78)", gk.Player1.Hero.Damage + gk.Player2.Hero.Damage == 78,
        $"{gk.Player1.Hero.Damage + gk.Player2.Hero.Damage}");
    var (_, _, gd) = Fight(Shaw, 454, new[] { "GILA_820" });
    var sd = Draw(gd, "GILA_820"); Mana(gd.Player1, 10);
    gd.Player2.Hero.Damage = gd.Player2.Hero.Health - 3; gd.Player1.Hero.Damage = gd.Player1.Hero.Health - 3;
    Play(gd, sd);
    Check("T20 GILA_820: it stops when a hero dies (one hero at 0 or below, the other untouched or dead last)", gd.Player1.Hero.Health <= 0 ^ gd.Player2.Hero.Health <= 0);
}

// ---------------------------------------------------------------- T21 GILA_824 Royal Gift
{
    var (s, plan, g) = Fight(Darius, 461, new[] { "GILA_824" });
    var p1 = g.Player1;
    var gift = Draw(g, "GILA_824"); Mana(p1, 10);
    var a = Board(g, p1, "CS2_231"); Board(g, p1, "CS2_231"); Board(g, p1, "CS2_231");
    var enemy = Board(g, g.Player2, "CS2_231");
    Check("T21 Darius's Cannons are on my board (untouchable, not counted)", Cannons.Of(p1).Count == 2);
    Play(g, gift, a);
    Check("T21 GILA_824: +2/+2 per minion I control (3 Wisps: +6/+6, Royalty)", a.AttackDamage == 7 && a.Health == 7 && a.AppliedEnchantments.Any(e => e.Card.Id == "GILA_824e"),
        $"{a.AttackDamage}/{a.Health}");
    var gift2 = ToHand(g, p1, "GILA_824"); Mana(p1, 10);
    Play(g, gift2, enemy);
    Check("T21 GILA_824 on an enemy minion: counts my minions (+6/+6)", enemy.AttackDamage == 7 && enemy.Health == 7);
}

// ---------------------------------------------------------------- T22 GILA_825 Gentleman's Top Hat
{
    var (s, plan, g) = Fight(Tess, 471, new[] { "GILA_825" });
    var p1 = g.Player1; var p2 = g.Player2;
    var hat = Draw(g, "GILA_825"); Mana(p1, 10);
    var a = Board(g, p1, "CS2_231"); var b = Board(g, p1, "CS2_231");
    Play(g, hat, a);
    Check("T22 GILA_825: +2/+2 and the Top Hat's Deathrattle", a.AttackDamage == 3 && a.Health == 3 && a.HasDeathrattle && a.AppliedEnchantments.Any(e => e.Card.Id == "GILA_825e"));
    Kill(g, a);
    Check("T22 GILA_825: when it dies the Top Hat moves to my other minion (+2/+2, Deathrattle)", b.AttackDamage == 3 && b.Health == 3 && b.HasDeathrattle);
    Kill(g, b);
    Check("T22 GILA_825: with no other friendly minion the hat is gone", p1.BoardZone.Count == 0);
    var e1 = Board(g, p2, "CS2_231"); var e2 = Board(g, p2, "CS2_182");
    var hat2 = ToHand(g, p1, "GILA_825"); Mana(p1, 10);
    Play(g, hat2, e1);
    Kill(g, e1);
    Check("T22 GILA_825 on an enemy minion: the hat moves to another of the enemy's minions", e2.AttackDamage == 6 && e2.Health == 7 && e2.HasDeathrattle);
}

// ---------------------------------------------------------------- T23 GILA_826 Nalaa the Redeemer
{
    var (s, plan, g) = Fight(Toki, 481, new[] { "GILA_826" });
    var p1 = g.Player1;
    var nalaa = Draw(g, "GILA_826"); Mana(p1, 10);
    Play(g, nalaa);
    var n = (Minion)nalaa;
    Check("T23 GILA_826: a 9/9 with Divine Shield and Taunt", n.AttackDamage == 9 && n.Health == 9 && n.HasDivineShield && n.HasTaunt);
    var hit = Board(g, g.Player2, "CS2_182");
    Generic.DamageCharFunc(hit, n, 3, false); g.DeathProcessingAndAuraUpdate();
    Check("T23 GILA_826: a hit on the shield pops it (no damage taken: no new shield)", !n.HasDivineShield && n.Damage == 0);
    Generic.DamageCharFunc(hit, n, 3, false); g.DeathProcessingAndAuraUpdate();
    Check("T23 GILA_826: after it survives damage it gains Divine Shield", n.HasDivineShield && n.Damage == 3);
    n.HasDivineShield = false;
    Generic.DamageCharFunc(hit, n, 20, false); g.DeathProcessingAndAuraUpdate();
    Check("T23 GILA_826: lethal damage: it dies, no shield", n.IsDead || n.Zone?.Type == Zone.GRAVEYARD);
}

// ---------------------------------------------------------------- T24 GILA_827 Murloc Holmes (hidden draw copy)
{
    var (s, plan, g) = Fight(Shaw, 491, new[] { "GILA_827" });
    var p1 = g.Player1; var p2 = g.Player2;
    var holmes = Draw(g, "GILA_827"); Mana(p1, 10); Clear(p1); Generic.AddHandPhase(p1, holmes);
    Play(g, holmes);
    Check("T24 GILA_827: a 3/4 Murloc", ((Minion)holmes).AttackDamage == 3 && ((Minion)holmes).Health == 4 && ((Minion)holmes).IsRace(Race.MURLOC));
    var top = p2.DeckZone.ToArray();
    EndTurn(g);
    var drawn = p2.HandZone.Last();
    var copy = p1.HandZone.LastOrDefault();
    Check("T24 GILA_827: when the boss draws its turn card, a copy goes to my hand", copy != null && copy.Card.Id == drawn.Card.Id && copy.Controller == p1 && p1.HandZone.Count == 1,
        copy?.Card.Id + " vs " + drawn.Card.Id);
    var sim = g.CloneForSimulation(1);
    var known = PublicHandKnowledge.Read(g, p1, sim.HistoryFacts.ProjectForFairView(sim, p2.PlayerId));
    Check("T24 GILA_827: the boss's public hand knowledge does not name the copy", !known.ContainsKey(copy.Id), string.Join(",", known.Select(k => k.Key + "=" + k.Value.Id)));
    Check("T24 GILA_827: the boss's fair view builds over the copy", FairBotState.Create(g, p2.PlayerId, 7, DeckStyle.Tempo, new FairOptions { RemoveOpponentSecrets = true }) != null);
    int mine = p1.HandZone.Count;
    EndTurn(g);
    Check("T24 GILA_827: my own draw is not copied", p1.HandZone.Count == mine + 1);
}

// ---------------------------------------------------------------- T25 GILA_850b Hallowed Water
{
    var (s, plan, g) = Fight(Darius, 501, new[] { "GILA_850b" });
    var p1 = g.Player1;
    var water = Draw(g, "GILA_850b"); Mana(p1, 10); Clear(p1); Generic.AddHandPhase(p1, water);
    p1.Hero.Damage = 22;
    int deck = p1.DeckZone.Count;
    Play(g, water);
    Check("T25 GILA_850b: my hero is restored to full Health (30) and I draw 5", p1.Hero.Health == 30 && p1.Hero.Damage == 0 && p1.HandZone.Count == 5 && p1.DeckZone.Count == deck - 5,
        $"{p1.Hero.Health} {p1.HandZone.Count}");
}

// ---------------------------------------------------------------- T26 GILA_853b Pristine Compass
{
    var (s, plan, g) = Fight(Tess, 511, new[] { "GILA_853b" });
    var p1 = g.Player1;
    var compass = Draw(g, "GILA_853b"); Mana(p1, 10); Clear(p1); Generic.AddHandPhase(p1, compass);
    Play(g, compass);
    var hand = p1.HandZone.ToList();
    Check("T26 GILA_853b: I draw a card and get 2 more copies of it; the copies cost (0), the drawn card keeps its cost",
        hand.Count == 3 && hand.All(e => e.Card.Id == hand[0].Card.Id) && hand[0].Cost == hand[0].Card.Cost && hand[1].Cost == 0 && hand[2].Cost == 0,
        string.Join(",", hand.Select(e => e.Card.Id + ":" + e.Cost)));
    var sim = g.CloneForSimulation(1);
    var known = PublicHandKnowledge.Read(g, p1, sim.HistoryFacts.ProjectForFairView(sim, g.Player2.PlayerId));
    Check("T26 GILA_853b: the boss does not learn the drawn card or its copies", hand.All(e => !known.ContainsKey(e.Id)));
}

// ---------------------------------------------------------------- T27 GILA_854 Beastly Beauty
{
    var (s, plan, g) = Fight(Shaw, 521, new[] { "GILA_854" });
    var p1 = g.Player1; var p2 = g.Player2;
    var beauty = Draw(g, "GILA_854"); Mana(p1, 10);
    var wisp = Board(g, p2, "CS2_231"); var ogre = Board(g, p2, "CS2_200");
    Play(g, beauty);
    var b = (Minion)beauty;
    Check("T27 GILA_854: a 2/5 Rush", b.AttackDamage == 2 && b.Health == 5 && b.IsRush);
    g.Process(MinionAttackTask.Any(p1, b, wisp));
    var beast = p1.BoardZone.GetAll().FirstOrDefault(m => m.Card.Id == "GILA_854t");
    Check("T27 GILA_854: after it attacks a minion and survives it becomes an 8/8 Beautiful Beast", beast != null && beast.AttackDamage == 8 && beast.Health == 8 && wisp.IsDead,
        string.Join(",", p1.BoardZone.GetAll().Select(m => m.Card.Id)));
    var b2 = ToHand(g, p1, "GILA_854"); Mana(p1, 10); Play(g, b2);
    g.Process(MinionAttackTask.Any(p1, (Minion)b2, ogre));
    g.DeathProcessingAndAuraUpdate();
    Check("T27 GILA_854: it dies attacking a 6/7: no transform", ((Minion)b2).IsDead && p1.BoardZone.GetAll().Count(m => m.Card.Id == "GILA_854t") == 1);
    var b3 = ToHand(g, p1, "GILA_854"); Mana(p1, 10); Play(g, b3);
    EndTurn(g); EndTurn(g);
    g.Process(MinionAttackTask.Any(p1, (Minion)b3, p2.Hero));
    Check("T27 GILA_854: attacking the hero: no transform", p1.BoardZone.GetAll().Any(m => m.Card.Id == "GILA_854"));
}

// ---------------------------------------------------------------- T28 GILA_810 Sneak Attack
{
    var (s, plan, g) = Fight(Toki, 531, new[] { "GILA_810" });
    var p1 = g.Player1;
    var sneak = Draw(g, "GILA_810"); Mana(p1, 10); Clear(p1); Generic.AddHandPhase(p1, sneak);
    var n1 = ToHand(g, p1, "EX1_015"); var n2 = ToHand(g, p1, "EX1_015"); var n3 = ToHand(g, p1, "EX1_015"); ToHand(g, p1, "CS2_029");
    Play(g, sneak);
    var board = p1.BoardZone.GetAll().ToList();
    Check("T28 GILA_810: two of the three hand minions are put into the battlefield with Charge (Ambush!); no Battlecry draws",
        board.Count == 2 && board.All(m => m.Card.Id == "EX1_015" && m.HasCharge && m.AppliedEnchantments.Any(e => e.Card.Id == "GILA_810e"))
        && p1.HandZone.Count == 2 && p1.HandZone.Count(e => e.Card.Id == "EX1_015") == 1, $"board {board.Count} hand {p1.HandZone.Count}");
    Check("T28 GILA_810: they can attack the enemy hero at once", p1.Options().OfType<MinionAttackTask>().Count(o => o.Target == g.Player2.Hero) == 2);
    string Picks(int seed)
    {
        var (_, _, gs) = Fight(Toki, seed, new[] { "GILA_810" });
        var c = Draw(gs, "GILA_810"); Mana(gs.Player1, 10); Clear(gs.Player1); Generic.AddHandPhase(gs.Player1, c);
        foreach (string id in new[] { "CS2_231", "CS2_182", "CS2_200", "EX1_015" }) ToHand(gs, gs.Player1, id);
        Play(gs, c);
        return string.Join(",", gs.Player1.BoardZone.GetAll().Select(m => m.Card.Id));
    }
    Check("T28 GILA_810: the same seed picks the same minions", Picks(532) == Picks(532));
}

// ---------------------------------------------------------------- T29 full games against the real boss AI, the player playing the id
var testBudget = new Budget(6000, 2000, 300, 6, 3, 100, 8, turnSearchSeconds: 6, turnWallSeconds: 8);
string AiGame(string id, long seed, string row, string[] swapIn = null)
{
    var s = NewAct(row, seed);
    bool isPower = false;
    bool isPassive = passives.Contains(id);
    bool isHero = false;
    Hold(s, id);
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
    var mineGila = game.HistoryFacts.Query.Plays().Where(f => f.ControllerId == game.Player1.Id && f.DefinitionKnown && Cards.FromAssetId(f.DbfId).Id.StartsWith("GILA_8"))
        .Select(f => Cards.FromAssetId(f.DbfId).Id).ToList();
    return $"{result} turns {game.Turn} played-turn {playedTurn} power-uses {powerUses} decisions {decisions} fallbacks {fallbacks} boss-gila [{string.Join(",", bossGila)}] mine [{string.Join(",", mineGila)}] boss {boss.Id}";
}
foreach (string id in f5)
{
    string row = id == "GILA_816a" || id == "GILA_816b" || id == "GILA_816c" || id == "GILA_824" ? Darius
        : id.StartsWith("GILA_81") ? Tess : id.StartsWith("GILA_82") ? Shaw : Toki;
    string line;
    try { line = AiGame(id, 551, row); }
    catch (Exception e) { line = "FAULT " + e.GetType().Name + ": " + e.Message.Split('\n')[0]; }
    Console.WriteLine($"T29 {id,-10} {row,-10} {line}");
    Check($"T29 {id}: the player plays it in a full game against the boss AI, which ends without a fault or an AI fallback",
        !line.StartsWith("FAULT") && line.Contains("fallbacks 0 ") && !line.Contains("played-turn -1"), line);
}
// ---------------------------------------------------------------- T30 Impetuous Companion hands the boss F5 treasures; the boss AI plays them
foreach (long seed in new long[] { 561, 562, 563 })
{
    string line;
    try
    {
        line = AiGame("GILA_906", seed, Toki, new[] { "GILA_802", "GILA_816a", "GILA_821a", "GILA_811", "GILA_852a", "GILA_851a", "GILA_825", "GILA_853b" });
    }
    catch (Exception e) { line = "FAULT " + e.GetType().Name + ": " + e.Message.Split('\n')[0]; }
    Console.WriteLine($"T30 GILA_906/{seed} {line}");
    Check($"T30 GILA_906 ({seed}): the boss receives F5 treasures in the swap and the AI plays them in a full game without a fault or fallback",
        !line.StartsWith("FAULT") && line.Contains("fallbacks 0 ") && !line.Contains("played-turn -1") && !line.Contains("boss-gila []"), line);
}

Console.WriteLine($"{passes} passed, {failures} failed");
return failures == 0 ? 0 : 1;
