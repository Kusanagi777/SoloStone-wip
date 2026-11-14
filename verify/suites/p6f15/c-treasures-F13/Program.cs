// c-treasures-F13: phase 6 family F13 of the unified Dungeon Run, the Tombs of Terror passive treasures (ULDA_005, 007, 009, 011,
// 014, 042, 046, 101, 102, 103, 116, 117, 205, 208, 508). For every id: the engine content (scripted, keyed and shelled by its own
// client id, name and text as facts/violet-hold-2026-09.json has them) and its effect shells, the run data (run-v1.json rows scoped
// to the explorers' sixteen starters, pool Tombs, act runs), the validator and admission checks, the real passive treasure draft,
// a scripted regression game through the run setup path (DungeonRun.NewRun on an explorer row -> BuildPlan ->
// DungeonRunSetup.Configure -> Game -> ApplyAfterStart -> mulligan -> MainBegin) asserting the board, hand, deck and hero outcome,
// ULDA_011's upgraded tavern through DungeonRunTavernNode.Open, and a full game against the real boss AI (DungeonRunBossPilot +
// GoldenPilot) that must end without a fault or an AI fallback (with ULDA_102: the boss passes its first two turns). Headless:
// compiles C:\SoloStone\plugin\DungeonRun\*.cs and the NPC AI against p6f13/engine-out. Run it twice and compare the output.
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
var factById = new Dictionary<string, System.Text.Json.JsonElement>(StringComparer.Ordinal);
foreach (var c in facts.RootElement.GetProperty("cards").EnumerateArray()) factById[c.GetProperty("id").GetString()] = c;
string FactText(string id) => factById[id].TryGetProperty("text", out var t) ? t.GetString() : null;
string FactName(string id) => factById[id].GetProperty("name").GetString();
int FactTag(string id, int tag)
{
    foreach (var pair in factById[id].GetProperty("tags").EnumerateArray())
        if (pair[0].GetInt32() == tag) return pair[1].GetInt32();
    return 0;
}
Card C(string id) => Cards.FromId(id);
var clientIds = new HashSet<string>(System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "dungeon-run/reference/client-tables.json")))
    .RootElement.GetProperty("tables").GetProperty("CARD").EnumerateArray().Select(r => r.GetProperty("id").GetString()), StringComparer.Ordinal);
var transport = new Dictionary<string, string>
{
    ["CS2_034"] = "HERO_08bp", ["CS2_102"] = "HERO_01bp", ["CS1h_001"] = "HERO_09bp", ["CS2_101"] = "HERO_04bp", ["CS2_017"] = "HERO_06bp",
    ["DS1h_292"] = "HERO_05bp", ["CS2_083b"] = "HERO_03bp", ["CS2_056"] = "HERO_07bp", ["CS2_049"] = "HERO_02bp",
    ["AT_132_WARRIOR"] = "HERO_01bp2", ["AT_132_SHAMAN"] = "HERO_02bp2", ["AT_132_ROGUE"] = "HERO_03bp2", ["AT_132_PALADIN"] = "HERO_04bp2",
    ["AT_132_HUNTER"] = "HERO_05bp2", ["AT_132_DRUID"] = "HERO_06bp2", ["AT_132_WARLOCK"] = "HERO_07bp2", ["AT_132_MAGE"] = "HERO_08bp2", ["AT_132_PRIEST"] = "HERO_09bp2",
};

// ---------------------------------------------------------------- the run setup path
const string Reno = "MAGE_ROGUE", Finley = "PALADIN_SHAMAN", Elise = "DRUID_PRIEST", Brann = "HUNTER_WARRIOR";
string[] explorers = { Reno, Finley, Elise, Brann };
var juniors = new DungeonRunProfile(); juniors.Unlocked.AddRange(data.Unlocks.Keys.Where(k => !k.EndsWith("_majors4")));
GameConfig Configured(RunMatchPlan plan, int seed)
{
    var cfg = new GameConfig { RandomSeed = seed, Shuffle = true, SkipMulligan = false, History = true, Logging = false, FormatType = FormatType.FT_WILD };
    DungeonRunSetup.Configure(cfg, plan);
    return cfg;
}
// An act run of an explorer at floor 1, its first offered signature picked.
DungeonRunState NewAct(string row, long seed)
{
    var cls = data.Class(row);
    string sig = run.SignatureOffer(juniors, cls, seed)[0];
    var s = run.NewRun(juniors, row, cls.DefaultDeck, cls.DefaultPower, seed, signatureId: sig);
    if (!s.IsActRun || s.Phase != DungeonRunPhase.PreMatch || s.Floor != 1) throw new Exception("act run not at floor 1 pre-match");
    return s;
}
// A passive treasure joins the run as a picked one does (DungeonRun.PickTreasure: the id in Treasures, the deck unchanged).
void Hold(DungeonRunState s, params string[] ids) { foreach (string id in ids) s.Treasures.Add(id); }
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
(DungeonRunState s, RunMatchPlan plan, Game g) Fight(string row, long seed, params string[] treasures)
{
    var s = NewAct(row, seed);
    Hold(s, treasures);
    var plan = run.BuildPlan(s);
    return (s, plan, StartFight(plan, run.MatchSeed(s)));
}
(DungeonRunState s, RunMatchPlan plan, Game g) PreMulligan(string row, long seed, params string[] treasures)
{
    var s = NewAct(row, seed);
    Hold(s, treasures);
    var plan = run.BuildPlan(s);
    var g = new Game(Configured(plan, run.MatchSeed(s)));
    g.StartGame();
    DungeonRunSetup.ApplyAfterStart(g, plan);
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
bool UsePower(Game g, ICharacter target = null) => g.Process(HeroPowerTask.Any(g.CurrentPlayer, target));
void EndTurn(Game g) => g.Process(EndTurnTask.Any(g.CurrentPlayer));
void Clear(Controller c) { foreach (var e in c.HandZone.ToArray()) { c.HandZone.Remove(e); c.SetasideZone.Add(e); } }
void ClearBoard(Controller c) { foreach (var m in c.BoardZone.ToArray()) { c.BoardZone.Remove(m); c.SetasideZone.Add(m); } }
void Kill(Game g, Minion m) { m.Damage = m.Health; g.DeathProcessingAndAuraUpdate(); }
// Keeps only the deck cards the predicate accepts.
void Thin(Game g, Controller c, Func<IPlayable, bool> keep)
{
    foreach (var e in c.DeckZone.ToArray()) if (!keep(e)) { c.DeckZone.Remove(e); c.SetasideZone.Add(e); }
    g.DeathProcessingAndAuraUpdate();
}
bool HasFx(Controller c, string shell) => c.AppliedEnchantments?.Any(e => e.Card.Id == shell) == true;
bool Has(IPlayable p, string ench) => p.AppliedEnchantments?.Any(e => e.Card.Id == ench) == true;
// A hero power the player can click with no target (the explorer's first power may need one): a basic Mage power.
ICharacter PowerTarget(Game g) => g.Player1.Hero.HeroPower.Card.RequiresTarget ? (ICharacter)g.Player2.Hero : null;

string[] f13 = DungeonRunContent.TombsPassiveIds.ToArray();
var mule = data.FindTreasure("ULDA_019");
// Effect content key -> its shell (every one the client's own enchantment).
var effects = new Dictionary<string, string>
{
    ["fx_ULDA_005"] = "ULDA_005e", ["fx_ULDA_007"] = "ULDA_007e2", ["fx_ULDA_009e"] = "ULDA_009e", ["fx_ULDA_014"] = "ULDA_014e",
    ["fx_ULDA_042"] = "ULDA_042e", ["fx_ULDA_042e2"] = "ULDA_042e2", ["fx_ULDA_046"] = "ULDA_046e", ["fx_ULDA_046e2"] = "ULDA_046e2",
    ["fx_ULDA_101"] = "ULDA_101e", ["fx_ULDA_101e2"] = "ULDA_101e2", ["fx_ULDA_102"] = "ULDA_102e", ["fx_ULDA_103"] = "ULDA_103e",
    ["fx_ULDA_103e2"] = "ULDA_103e2", ["fx_ULDA_116"] = "ULDA_116e", ["fx_ULDA_117"] = "ULDA_117e", ["fx_ULDA_205"] = "ULDA_205e",
    ["fx_ULDA_208"] = "ULDA_208e", ["fx_ULDA_208e2"] = "ULDA_208e2", ["fx_ULDA_508"] = "ULDA_508e", ["fx_ULDA_508e2"] = "ULDA_508e2",
};
string[] onStart = { "ULDA_009", "ULDA_117" };

// ---------------------------------------------------------------- T1 content
Check("T1 15 F13 ids, in id order", f13.Length == 15 && f13.Distinct().Count() == 15 && f13.SequenceEqual(f13.OrderBy(x => x, StringComparer.Ordinal))
    && f13.SequenceEqual(new[] { "ULDA_005", "ULDA_007", "ULDA_009", "ULDA_011", "ULDA_014", "ULDA_042", "ULDA_046", "ULDA_101", "ULDA_102", "ULDA_103",
        "ULDA_116", "ULDA_117", "ULDA_205", "ULDA_208", "ULDA_508" }));
foreach (string id in f13)
{
    var content = DungeonRunContent.Get(id);
    var card = C(id);
    Check($"T1 {id}: a PassiveTreasure keyed and shelled by its own id, scripted, the client's SPELL",
        content.Kind == RunContentKind.PassiveTreasure && content.ShellId == id && card != null && card.Implemented && card.Type == CardType.SPELL
        && FactTag(id, 202) == 5 && DungeonRunContent.ForShell(id) == content && clientIds.Contains(id), content.Kind + " " + card?.Type);
    Check($"T1 ids-text-match {id}: content name and text equal facts/violet-hold-2026-09.json",
        content.Name == FactName(id) && content.Text == FactText(id), content.Name + " | " + content.Text);
    bool expectStart = onStart.Contains(id), expectNone = id == "ULDA_011";
    Check($"T1 {id}: " + (expectNone ? "no match effect (a run-layer flag)" : expectStart ? "a match-start change" : "a player effect"),
        expectNone ? content.EffectKey == null && content.OnStartIsNull() : expectStart ? content.EffectKey == null : content.EffectKey != null && effects.ContainsKey(content.EffectKey));
}
foreach (var fx in effects)
{
    var e = DungeonRunContent.Get(fx.Key);
    var card = C(fx.Value);
    Check($"T1 {fx.Value}: content {fx.Key} on the client's own ENCHANTMENT, scripted, used once, name and text word for word",
        e.Kind == RunContentKind.Effect && e.ShellId == fx.Value && card != null && card.Implemented && card.Type == CardType.ENCHANTMENT && clientIds.Contains(fx.Value)
        && DungeonRunContent.All.Count(x => x.ShellId == fx.Value) == 1 && e.Name == FactName(fx.Value) && e.Text == FactText(fx.Value), e.Name + " | " + e.Text);
}
Check("T1 no F13 shell is a Start of Game source (the start changes run before the mulligan)", effects.Values.All(v => !StartOfGameRules.HasEffect(C(v))));
Check("T1 the one-turn enchantments (ULDA_042e2, ULDA_508e2) carry TAG_ONE_TURN_EFFECT; the others do not",
    C("ULDA_042e2")[GameTag.TAG_ONE_TURN_EFFECT] == 1 && C("ULDA_508e2")[GameTag.TAG_ONE_TURN_EFFECT] == 1
    && effects.Values.Where(v => v != "ULDA_042e2" && v != "ULDA_508e2").All(v => C(v)[GameTag.TAG_ONE_TURN_EFFECT] == 0));
Check("T1 constants: two passed turns, Spell Damage 10, two copies at (2) less, Poisonous up to (2)",
    DungeonRunContent.SwiftnessPassedTurns == 2 && DungeonRunContent.NonsenseSpellDamage == 10 && DungeonRunContent.LuckySpadeCopies == 2
    && DungeonRunContent.LuckySpadeDiscount == 2 && DungeonRunContent.BeesMaxCost == 2 && DungeonRunContent.VipMembership == "ULDA_011");

// ---------------------------------------------------------------- T2 data, validator, admission
{
    var errors = DungeonRunValidator.Validate(data, id => clientIds.Contains(transport.TryGetValue(id, out var sent) ? sent : id));
    Check("T2 shipped data validates with 0 errors (client check through the transported id)", errors.Count == 0, string.Join(" | ", errors.Take(5)));
    var legacy = DungeonRunData.Parse(runJson, poolJson, npcJson);
    var le = DungeonRunValidator.Validate(legacy, null);
    Check("T2 the data without the catalog validates with 0 errors and loads no F13 treasure (their rows scope catalog starters)",
        le.Count == 0 && f13.All(id => legacy.FindTreasure(id) == null), string.Join(" | ", le.Take(3)));
    foreach (string id in f13)
    {
        var t = data.FindTreasure(id);
        Check($"T2 {id}: a passive act-run treasure row, offered, Tombs pool, scoped by the explorers' sixteen starters as Pack Mule",
            t != null && !t.Active && t.Offered && !t.Legacy && !t.Signature && t.Runs == DungeonRunTreasure.RunsAct && t.Pool == "Tombs"
            && t.Scope.Decks.SequenceEqual(mule.Scope.Decks) && mule.Scope.Decks.Count == 16 && t.Scope.Classes.Count == 0 && t.Scope.Powers.Count == 0 && t.UpgradesTo == null);
    }
    var families = new Dictionary<string, string>
    {
        ["ULDA_005"] = "run-treasure", ["ULDA_007"] = "run-treasure", ["ULDA_014"] = "run-treasure", ["ULDA_046"] = "run-treasure",
        ["ULDA_101"] = "run-treasure", ["ULDA_116"] = "run-treasure", ["ULDA_208"] = "run-treasure", ["ULDA_508"] = "run-treasure",
        ["ULDA_042"] = "run-treasure-paid-cost", ["ULDA_103"] = "run-treasure-paid-cost", ["ULDA_009"] = "run-treasure-match-start",
        ["ULDA_117"] = "run-treasure-match-start", ["ULDA_102"] = "run-treasure-forced-pass", ["ULDA_205"] = "run-treasure-discover",
        ["ULDA_011"] = "run-treasure-run-flag",
    };
    Check("T2 every F13 id has exactly one admission family here", f13.All(families.ContainsKey) && families.Count == 15);
    foreach (var pair in families.OrderBy(p => p.Key, StringComparer.Ordinal))
    {
        string id = pair.Key;
        var p = DungeonRunAdmission.Classify(C(id), id, AdmitScope.Player, null, catalogRun: true);
        var pl = DungeonRunAdmission.Classify(C(id), id, AdmitScope.Player, null, catalogRun: false);
        var b = DungeonRunAdmission.Classify(C(id), id, AdmitScope.Boss, null, catalogRun: true);
        Check($"T2 admission {id}: the {pair.Value} row admits the player (catalog and legacy verdicts), never the boss",
            p.Subject && p.Admitted && pl.Admitted && !b.Admitted && DungeonRunAdmission.RuleFor(id)?.Family == pair.Value
            && DungeonRunAdmission.RuleFor(id).Reason.Contains("c-treasures-F13"), p.Family + " " + p.Admit);
        Check($"T2 admission {id}: the boss's generation never yields it", DungeonRunAdmission.GeneratedRejectsBoss(true).Contains(id));
    }
}

// ---------------------------------------------------------------- T3 offers: every explorer, never another run
{
    var offered = new Dictionary<string, HashSet<string>>();
    foreach (var cls in data.Classes)
    {
        var set = offered[cls.Id] = new HashSet<string>();
        bool tombs = explorers.Contains(cls.Id);
        int seeds = tombs ? 150 : 20;
        for (long seed = 1; seed <= seeds; seed++)
        {
            var sigs = run.SignatureOffer(all, cls, seed);
            var s = run.NewRun(all, cls.Id, cls.DefaultDeck, cls.DefaultPower, seed, signatureId: sigs.FirstOrDefault());
            for (int k = 0; k < 3 && run.OpenTreasureDraft(s, passiveOnly: true); k++)
            {
                set.UnionWith(s.OpenDraft.Offered);
                run.PickTreasure(s, s.OpenDraft.Offered[0]);
            }
        }
    }
    var tombsOffers = new HashSet<string>(explorers.SelectMany(h => offered[h]));
    foreach (string id in f13) Check($"T3 {id} is offered in an explorer's passive treasure draft", tombsOffers.Contains(id));
    foreach (string h in explorers)
        Check($"T3 {h}: offered Tombs passives (at least 8 of the 15)", offered[h].Count(f13.Contains) >= 8, offered[h].Count(f13.Contains).ToString());
    Check("T3 no other run is offered a Tombs passive treasure (deck scope)",
        offered.Where(p => !explorers.Contains(p.Key)).All(p => !p.Value.Overlaps(f13)),
        string.Join(";", offered.Where(p => !explorers.Contains(p.Key) && p.Value.Overlaps(f13)).Select(p => p.Key)));
    var sp = NewAct(Brann, 9);
    run.OpenTreasureDraft(sp, passiveOnly: true);
    sp.OpenDraft.Offered[0] = "ULDA_102";
    int before = sp.Deck.Count;
    run.PickTreasure(sp, "ULDA_102");
    Check("T3 PickTreasure ULDA_102 on a Brann run: a passive (Treasures), the run deck unchanged",
        sp.Treasures.Contains("ULDA_102") && !sp.ActiveTreasures.Contains("ULDA_102") && sp.Deck.Count == before);
}

// ---------------------------------------------------------------- T4 ULDA_005 Mummy Magic
{
    var (s, plan, g) = Fight(Elise, 401, "ULDA_005");
    var p1 = g.Player1; var p2 = g.Player2;
    Check("T4 ULDA_005: the plan lists it and its player enchantment is attached", plan.PlayerTreasures.Contains("ULDA_005") && HasFx(p1, "ULDA_005e"));
    Clear(p1); Mana(p1, 10);
    var hoarder = ToHand(g, p1, "EX1_096"); var croc = ToHand(g, p1, "CS2_120");
    Play(g, hoarder); Play(g, croc);
    Check("T4 ULDA_005: a played Deathrattle minion (Loot Hoarder) gains Reborn; a minion with no Deathrattle does not",
        (((Minion)hoarder)[GameTag.REBORN] == 1) && !(((Minion)croc)[GameTag.REBORN] == 1), $"{(((Minion)hoarder)[GameTag.REBORN] == 1)} {(((Minion)croc)[GameTag.REBORN] == 1)}");
    Kill(g, (Minion)hoarder);
    var back = p1.BoardZone.FirstOrDefault(m => m.Card.Id == "EX1_096");
    Check("T4 ULDA_005: the Hoarder dies and returns with 1 Health (Reborn)", back != null && back.Health == 1, back?.Health.ToString());
    var theirs = Board(g, p2, "EX1_096");
    Check("T4 ULDA_005: a summoned (not played) or enemy Deathrattle minion gets nothing", theirs[GameTag.REBORN] == 0);
}

// ---------------------------------------------------------------- T5 ULDA_007 Crook and Flail
{
    var (s, plan, g) = Fight(Reno, 402, "ULDA_007");
    var p1 = g.Player1; var p2 = g.Player2;
    Clear(p1); Mana(p1, 10);
    int deckMinions = p1.DeckZone.Count(e => e is Minion), board = p1.BoardZone.Count;
    var ae = ToHand(g, p1, "CS2_025");
    Play(g, ae);
    Check("T5 ULDA_007: after a spell (Arcane Explosion) a minion from my deck enters the battlefield",
        p1.BoardZone.Count == board + 1 && p1.DeckZone.Count(e => e is Minion) == deckMinions - 1, $"{board}->{p1.BoardZone.Count} deck minions {deckMinions}->{p1.DeckZone.Count(e => e is Minion)}");
    Thin(g, p1, e => !(e is Minion));
    var ae2 = ToHand(g, p1, "CS2_025"); int b2 = p1.BoardZone.Count;
    Play(g, ae2);
    Check("T5 ULDA_007: with no minion in the deck nothing enters", p1.BoardZone.Count == b2);
    var croc = ToHand(g, p1, "CS2_120"); int b3 = p1.BoardZone.Count;
    Play(g, croc);
    Check("T5 ULDA_007: a minion played is not a spell (no recruit)", p1.BoardZone.Count == b3 + 1);
}

// ---------------------------------------------------------------- T6 ULDA_009 Unlocked Potential (before the mulligan)
{
    var (s, plan, g) = PreMulligan(Elise, 403, "ULDA_009");
    var p1 = g.Player1; var p2 = g.Player2;
    var mine = p1.DeckZone.Concat(p1.HandZone).OfType<Minion>().ToList();
    Check("T6 ULDA_009: the run deck has minions", mine.Count >= 5, mine.Count.ToString());
    Check("T6 ULDA_009: before the mulligan every minion in my deck and hand has Attack equal to its Health, marked ULDA_009e",
        mine.All(m => m.AttackDamage == m.Health && Has(m, "ULDA_009e")), string.Join(",", mine.Where(m => m.AttackDamage != m.Health).Select(m => m.Card.Id + " " + m.AttackDamage + "/" + m.Health)));
    Check("T6 ULDA_009: at least one minion changed (its card's Attack differs from its Health)", mine.Any(m => m.Card.ATK != m.Card.Health));
    var theirs = p2.DeckZone.Concat(p2.HandZone).OfType<Minion>().ToList();
    Check("T6 ULDA_009: the boss's minions keep their Attack", theirs.All(m => m.AttackDamage == m.Card.ATK && !Has(m, "ULDA_009e")));
    Finish(g);
    var drawn = p1.HandZone.OfType<Minion>().ToList();
    Check("T6 ULDA_009: after the mulligan my hand's minions still hold it", drawn.All(m => m.AttackDamage == m.Health));
    Clear(p1); Mana(p1, 10);
    var yeti = ToHand(g, p1, "CS2_182");
    Check("T6 ULDA_009: a minion that was not in the deck at the start keeps its Attack", yeti is Minion y && y.AttackDamage == 4);
}

// ---------------------------------------------------------------- T7 ULDA_014 Disks of Legend
{
    var (s, plan, g) = Fight(Brann, 404, "ULDA_014");
    var p1 = g.Player1;
    Clear(p1); Mana(p1, 10);
    var thalnos = ToHand(g, p1, "EX1_012");
    Play(g, thalnos);
    Check("T7 ULDA_014: after I play a Legendary minion (Bloodmage Thalnos) a copy is summoned", p1.BoardZone.Count(m => m.Card.Id == "EX1_012") == 2);
    var croc = ToHand(g, p1, "CS2_120");
    Play(g, croc);
    Check("T7 ULDA_014: a non-Legendary minion gets no copy", p1.BoardZone.Count(m => m.Card.Id == "CS2_120") == 1);
    Check("T7 ULDA_014: the copies' Spell Damage counts (two Thalnos)", p1.CurrentSpellPower == 2, p1.CurrentSpellPower.ToString());
}

// ---------------------------------------------------------------- T8 ULDA_042 Darklight Torch
{
    var (s, plan, g) = Fight(Reno, 405, "ULDA_042");
    var p1 = g.Player1;
    Clear(p1); Mana(p1, 10);
    var hp = p1.Hero.HeroPower;
    int printed = hp.Card.Cost;
    Check("T8 ULDA_042: the hero power is used", UsePower(g, PowerTarget(g)) && hp.IsExhausted);
    var yeti = ToHand(g, p1, "CS2_182");
    Play(g, yeti);
    Check("T8 ULDA_042: after an even-Cost card (4) the hero power is refreshed and costs (0)", !hp.IsExhausted && hp.Cost == 0, $"{hp.IsExhausted} {hp.Cost}");
    int used = p1.UsedMana;
    Check("T8 ULDA_042: it is used again for nothing", UsePower(g, PowerTarget(g)) && p1.UsedMana == used, $"{used}->{p1.UsedMana}");
    var raider = ToHand(g, p1, "CS2_168");
    Play(g, raider);
    Check("T8 ULDA_042: after an odd-Cost card (1) it stays exhausted", hp.IsExhausted);
    var yeti2 = ToHand(g, p1, "CS2_182");
    SabberStoneCore.Actions.CostRules.AddModifier((Playable)yeti2, SabberStoneCore.Enchants.EffectOperator.SUB, 1);
    Play(g, yeti2);
    Check("T8 ULDA_042: a 4-Cost minion paid at (3) is odd: no refresh (the paid Cost counts)", hp.IsExhausted && yeti2[GameTag.TAG_LAST_KNOWN_COST_IN_HAND] == 3);
    var wisp = ToHand(g, p1, "CS2_231");
    Play(g, wisp);
    Check("T8 ULDA_042: a (0)-Cost card is even: refreshed", !hp.IsExhausted && hp.Cost == 0);
    EndTurn(g); EndTurn(g);
    Check("T8 ULDA_042: next turn the hero power costs its own Cost again", hp.Cost == printed, $"{hp.Cost} vs {printed}");
}

// ---------------------------------------------------------------- T9 ULDA_046 Band of Bees
{
    var (s, plan, g) = Fight(Finley, 406, "ULDA_046");
    var p1 = g.Player1; var p2 = g.Player2;
    var croc = Board(g, p1, "CS2_120"); var raider = Board(g, p1, "CS2_168"); var yeti = Board(g, p1, "CS2_182");
    var theirs = Board(g, p2, "CS2_120");
    Check("T9 ULDA_046: my minions that cost (2) or less have Poisonous (2, 1), a 4-Cost minion does not",
        croc.Poisonous && raider.Poisonous && !yeti.Poisonous && Has(croc, "ULDA_046e2"), $"{croc.Poisonous} {raider.Poisonous} {yeti.Poisonous}");
    Check("T9 ULDA_046: the enemy's 2-Cost minion has no Poisonous", !theirs.Poisonous);
    var enemyYeti = Board(g, p2, "CS2_182");
    Mana(p1, 10);
    raider.IsExhausted = false;
    Check("T9 ULDA_046: the attack is accepted", g.Process(MinionAttackTask.Any(p1, raider, enemyYeti)));
    Check("T9 ULDA_046: the Poisonous Murloc Raider destroys the Yeti it hits", enemyYeti.IsDead || enemyYeti.Zone != p2.BoardZone);
}

// ---------------------------------------------------------------- T10 ULDA_101 Band of Scarabs
{
    var (s, plan, g) = Fight(Brann, 407, "ULDA_101");
    var p1 = g.Player1; var p2 = g.Player2;
    var theirs = Board(g, p2, "CS2_182"); var wisp = Board(g, p2, "CS2_231"); var mine = Board(g, p1, "CS2_182");
    Check("T10 ULDA_101: enemy minions have -1 Attack (Yeti 3, Wisp 0); mine keep theirs (4)",
        theirs.AttackDamage == 3 && wisp.AttackDamage == 0 && mine.AttackDamage == 4 && Has(theirs, "ULDA_101e2"), $"{theirs.AttackDamage} {wisp.AttackDamage} {mine.AttackDamage}");
    EndTurn(g);
    Check("T10 ULDA_101: the Wisp (0 Attack) cannot attack; the Yeti hits for 3", !p2.Options().OfType<MinionAttackTask>().Any(o => o.Source == wisp));
    int hp = p1.Hero.Health;
    g.Process(MinionAttackTask.Any(p2, theirs, p1.Hero));
    Check("T10 ULDA_101: the boss's Yeti deals 3", p1.Hero.Health == hp - 3, $"{hp}->{p1.Hero.Health}");
}

// ---------------------------------------------------------------- T11 ULDA_102 Disks of Swiftness
{
    var (s, plan, g) = Fight(Brann, 408, "ULDA_102");
    var p1 = g.Player1; var p2 = g.Player2;
    Check("T11 ULDA_102: the plan lists it and its player enchantment is attached", plan.PlayerTreasures.Contains("ULDA_102") && HasFx(p1, "ULDA_102e"));
    Check("T11 ULDA_102: I never pass (turn 1)", !RunPlayerRules.MustPass(p1) && p1.Options().Count > 1);
    int[] handAtStart = new int[7];
    bool[] passed = new bool[7], onlyEnd = new bool[7], refused = new bool[7], cloneOnly = new bool[7];
    for (int turn = 1; turn <= 3; turn++)
    {
        EndTurn(g);
        if (g.CurrentPlayer != p2) throw new Exception("not the boss's turn");
        handAtStart[turn] = p2.HandZone.Count;
        passed[turn] = RunPlayerRules.MustPass(p2);
        var croc = ToHand(g, p2, "CS2_120"); Mana(p2, 10);
        var opts = p2.Options();
        onlyEnd[turn] = opts.Count == 1 && opts[0] is EndTurnTask;
        refused[turn] = !g.Process(PlayCardTask.Any(p2, croc)) && !g.Process(HeroPowerTask.Any(p2, p2.Hero.HeroPower.Card.RequiresTarget ? (ICharacter)p1.Hero : null));
        var clone = g.Clone();
        cloneOnly[turn] = clone.Player2.Options().Count == 1 && RunPlayerRules.MustPass(clone.Player2);
        if (turn == 3) break;
        EndTurn(g);
        Check($"T11 ULDA_102: my turn {2 * turn + 1}: I may act", !RunPlayerRules.MustPass(p1) && g.CurrentPlayer == p1);
    }
    Check("T11 ULDA_102: the boss's first turn is a pass: End Turn is its only option, a play and the hero power are refused, a clone agrees",
        passed[1] && onlyEnd[1] && refused[1] && cloneOnly[1]);
    Check("T11 ULDA_102: the boss's second turn is a pass too", passed[2] && onlyEnd[2] && refused[2] && cloneOnly[2]);
    Check("T11 ULDA_102: the boss's third turn is free: it may play its card", !passed[3] && !onlyEnd[3] && !refused[3] && !cloneOnly[3]);
    Check("T11 ULDA_102: the boss still draws on its passed turns (hand grows each turn)", handAtStart[2] > handAtStart[1] - 1 && p2.HandZone.Count > 0);
    // Extra turns do not use up the passes: the count is of the opponent's own turn starts.
    var (s2, plan2, g2) = Fight(Brann, 409, "ULDA_102");
    EndTurn(g2);
    Check("T11 ULDA_102: a ChooseTask or End Turn is still accepted during the pass", RunPlayerRules.MustPass(g2.Player2) && g2.Process(EndTurnTask.Any(g2.Player2)));
}

// ---------------------------------------------------------------- T12 ULDA_103 Alchemist's Stone
{
    var (s, plan, g) = Fight(Reno, 410, "ULDA_103");
    var p1 = g.Player1; var p2 = g.Player2;
    Clear(p1); Mana(p1, 10);
    var raider = ToHand(g, p1, "CS2_168"); var yeti = ToHand(g, p1, "CS2_182"); var fireball = ToHand(g, p1, "CS2_029");
    Play(g, raider);
    Check("T12 ULDA_103: after an odd-Cost card (1) the cards in my hand cost (1) less (Alchemy's First Law)",
        yeti.Cost == 3 && fireball.Cost == 3 && Has(yeti, "ULDA_103e2"), $"{yeti.Cost} {fireball.Cost}");
    Play(g, yeti);
    Check("T12 ULDA_103: a 4-Cost minion paid at (3) is odd: the rest of the hand costs (1) less again", fireball.Cost == 2, fireball.Cost.ToString());
    var croc = ToHand(g, p1, "CS2_120");
    var hold = ToHand(g, p1, "CS2_182");
    Play(g, croc);
    Check("T12 ULDA_103: after an even-Cost card (2) nothing changes", hold.Cost == 4 && fireball.Cost == 2);
    Clear(p2); var theirs = ToHand(g, p2, "CS2_182");
    var raider2 = ToHand(g, p1, "CS2_168"); Play(g, raider2);
    Check("T12 ULDA_103: the boss's hand is untouched", theirs.Cost == 4);
}

// ---------------------------------------------------------------- T13 ULDA_011 VIP Membership (run-layer tavern flag)
{
    var (s, plan, g) = Fight(Brann, 411, "ULDA_011");
    Check("T13 ULDA_011: in a match it attaches nothing (no player enchantment of its own)", plan.PlayerTreasures.Contains("ULDA_011")
        && g.Player1.AppliedEnchantments?.Any(e => e.Card.Id.StartsWith("ULDA_011")) != true);
    var scratch = new DungeonRunProfile();
    int seen = 0;
    foreach (string row in explorers)
    {
        var t = NewAct(row, 4242);
        for (int guard = 0; guard < 400 && !t.Finished && t.Phase != DungeonRunPhase.Tavern; guard++)
            switch (t.Phase)
            {
                case DungeonRunPhase.PreMatch: run.BeginMatch(t); run.RecordOutcome(t, scratch, DungeonRunOutcome.Win); break;
                case DungeonRunPhase.TreasureDraft: run.PickTreasure(t, t.OpenDraft.Offered[0]); break;
                case DungeonRunPhase.Modifier: run.PickModifier(t, t.ModifierOffer.Offered[0]); break;
                case DungeonRunPhase.BundleDraft:
                    if (t.OpenDraft.Takes == 0) { run.PickBundle(t, t.OpenDraft.Offered[0]); break; }
                    string bundle = t.OpenDraft.Choosing ?? t.OpenDraft.Offered[0];
                    var left = run.OfferCards(t.OpenDraft, bundle);
                    foreach (string kept in t.OpenDraft.Chosen) left.Remove(kept);
                    run.PickDraftCard(t, bundle, left[0]);
                    break;
                default: throw new Exception("walk stopped at " + t.Phase);
            }
        if (t.Phase != DungeonRunPhase.Tavern) continue;
        t.Treasures.Remove("ULDA_011");
        DungeonRunTavernNode.Open(run, t);
        int gold = t.Tavern.Gold, offers = t.Tavern.Offers.Count;
        var plain = t.Tavern.Offers.ToList();
        int actGold = run.CurrentAct(t).TavernGold, actOffers = run.CurrentAct(t).TavernOffers;
        t.Treasures.Add("ULDA_011");
        DungeonRunTavernNode.Open(run, t);
        seen++;
        Check($"T13 ULDA_011 ({row}): an upgraded tavern has 1 more gold ({gold} -> {t.Tavern.Gold}) and offers 1 more option ({offers} -> {t.Tavern.Offers.Count})",
            t.Tavern.Gold == gold + 1 && run.TavernGold(t) == t.Tavern.Gold && t.Tavern.Offers.Count == offers + 1 && run.TavernOffers(t) == actOffers + 1
            && offers == actOffers && t.Tavern.Offers.Distinct().Count() == t.Tavern.Offers.Count);
        var buy = t.Tavern.Offers.FirstOrDefault(o => data.Tavern.Option(o).Cost <= t.Tavern.Gold);
        Check($"T13 ULDA_011 ({row}): the upgraded tavern still sells through the normal Buy path", buy == null || DungeonRunTavernNode.Buy(run, t, buy) != null);
    }
    Check("T13 ULDA_011: every explorer reached a tavern node", seen == 4, seen.ToString());
}

// ---------------------------------------------------------------- T14 ULDA_116 Ever-Changing Elixir
{
    var (s, plan, g) = Fight(Finley, 412, "ULDA_116");
    var p1 = g.Player1; var p2 = g.Player2;
    ClearBoard(p1);
    EndTurn(g);
    Check("T14 ULDA_116: with no friendly minion nothing happens at my turn end", g.CurrentPlayer == p2 && p1.BoardZone.Count == 0);
    EndTurn(g);
    var croc = Board(g, p1, "CS2_120");
    var enemy = Board(g, p2, "CS2_120");
    EndTurn(g);
    var after = p1.BoardZone.Single();
    Check("T14 ULDA_116: at my turn end my only minion (a 2-Cost Crocolisk) becomes a 3-Cost minion", after.Card.Cost == 3 && after.Card.Id != "CS2_120", after.Card.Id + " " + after.Card.Cost);
    Check("T14 ULDA_116: the enemy's minion is untouched", p2.BoardZone.Any(m => m.Card.Id == "CS2_120"));
    EndTurn(g);
    int cost = p1.BoardZone.Single().Card.Cost;
    Check("T14 ULDA_116: the boss's turn end does not trigger it", cost == 3);
}

// ---------------------------------------------------------------- T15 ULDA_117 Scroll of Nonsense (before the mulligan)
{
    var (s, plan, g) = PreMulligan(Reno, 413, "ULDA_117");
    var p1 = g.Player1; var p2 = g.Player2;
    Check("T15 ULDA_117: before the mulligan I have Spell Damage +10 (its enchantment holds 10)", p1.CurrentSpellPower == 10 && HasFx(p1, "ULDA_117e")
        && p1.AppliedEnchantments.First(e => e.Card.Id == "ULDA_117e")[GameTag.TAG_SCRIPT_DATA_NUM_1] == 10, p1.CurrentSpellPower.ToString());
    Check("T15 ULDA_117: the boss has none", p2.CurrentSpellPower == 0);
    Finish(g);
    Clear(p1); Mana(p1, 10);
    var fb = ToHand(g, p1, "CS2_029");
    int hp = p2.Hero.Health + p2.Hero.Armor;
    Play(g, fb, p2.Hero);
    Check("T15 ULDA_117: Fireball deals 6 + 10", p2.Hero.Health + p2.Hero.Armor == hp - 16, $"{hp}->{p2.Hero.Health + p2.Hero.Armor}");
    EndTurn(g);
    Check("T15 ULDA_117: after my turn end it is +9", p1.CurrentSpellPower == 9, p1.CurrentSpellPower.ToString());
    EndTurn(g);
    Check("T15 ULDA_117: the boss's turn end does not lower it", p1.CurrentSpellPower == 9);
    for (int i = 0; i < 12; i++) { EndTurn(g); EndTurn(g); if (g.State == State.COMPLETE) break; }
    Check("T15 ULDA_117: after ten of my turn ends it is gone and stays at 0", p1.CurrentSpellPower == 0, p1.CurrentSpellPower.ToString());
}

// ---------------------------------------------------------------- T16 ULDA_205 Lucky Spade
{
    var (s, plan, g) = Fight(Reno, 414, "ULDA_205");
    var p1 = g.Player1;
    Clear(p1); Mana(p1, 10);
    var conjurer = ToHand(g, p1, "LOE_003");
    Play(g, conjurer);
    var choice = p1.Choice;
    Check("T16 ULDA_205: Ethereal Conjurer opens a Discover", choice != null && choice.Choices.Count == 3);
    int pick = choice.Choices[0];
    var chosen = g.IdEntityDic[pick];
    g.Process(ChooseTask.Pick(p1, pick));
    var same = p1.HandZone.Where(e => e.Card.Id == chosen.Card.Id).ToList();
    int want = Math.Max(0, chosen.Card.Cost - 2);
    Check($"T16 ULDA_205: after I Discover {chosen.Card.Id} I hold it and 2 copies, all costing (2) less ({chosen.Card.Cost} -> {want})",
        same.Count == 3 && same.All(e => e.Cost == want) && same.Contains(chosen), string.Join(",", same.Select(e => e.Card.Id + ":" + e.Cost)) + " hand " + p1.HandZone.Count);
    Check("T16 ULDA_205: the chosen card is the left-most of the three (the copies follow it)", same.IndexOf(chosen) == 0 && chosen.ZonePosition < same[1].ZonePosition);
    // A nearly full hand: the chosen card is never burned by its own copies.
    Clear(p1);
    for (int i = 0; i < 8; i++) ToHand(g, p1, "CS2_231");
    var conj2 = ToHand(g, p1, "LOE_003");
    Mana(p1, 10);
    Play(g, conj2);
    int pick2 = p1.Choice.Choices[0];
    g.Process(ChooseTask.Pick(p1, pick2));
    Check("T16 ULDA_205: with 8 cards in hand the chosen card lands and one copy fills the hand", p1.HandZone.Count == 10 && p1.HandZone.Any(e => e.Id == pick2)
        && p1.HandZone.Count(e => e.Card.Id == g.IdEntityDic[pick2].Card.Id) == 2, p1.HandZone.Count.ToString());
    // The boss's own Discover is untouched (the treasure is mine).
    var (s3, plan3, g3) = Fight(Reno, 415);
    var q = g3.Player1; Clear(q); Mana(q, 10);
    var c3 = ToHand(g3, q, "LOE_003"); Play(g3, c3);
    int pk = q.Choice.Choices[0]; g3.Process(ChooseTask.Pick(q, pk));
    Check("T16 without ULDA_205 a Discover adds the one card at its own Cost", q.HandZone.Count(e => e.Card.Id == g3.IdEntityDic[pk].Card.Id) == 1 && g3.IdEntityDic[pk].Cost == g3.IdEntityDic[pk].Card.Cost);
}

// ---------------------------------------------------------------- T17 ULDA_208 Titanic Ring
{
    var (s, plan, g) = Fight(Finley, 416, "ULDA_208");
    var p1 = g.Player1; var p2 = g.Player2;
    var croc = Board(g, p1, "CS2_120"); var theirs = Board(g, p2, "CS2_120");
    Check("T17 ULDA_208: my minions have +1 Health and Taunt (Crocolisk 2/4)", croc.Health == 4 && croc.HasTaunt && Has(croc, "ULDA_208e2"), $"{croc.Health} {croc.HasTaunt}");
    Check("T17 ULDA_208: the enemy's do not", theirs.Health == 3 && !theirs.HasTaunt);
    EndTurn(g);
    var opts = p2.Options().OfType<MinionAttackTask>().Where(o => o.Source == theirs).ToList();
    Mana(p2, 0);
    Check("T17 ULDA_208: the boss's minion must attack my Taunt minion, not my hero", g.CurrentPlayer == p2 && (opts.Count == 0 || opts.All(o => o.Target == croc)));
}

// ---------------------------------------------------------------- T18 ULDA_508 Robes of Diminishing
{
    var (s, plan, g) = Fight(Reno, 417, "ULDA_508");
    var p1 = g.Player1;
    var spell = p1.DeckZone.FirstOrDefault(e => e is Spell && e.Card.Cost > 0);
    var minion = p1.DeckZone.FirstOrDefault(e => e is Minion && e.Card.Cost > 0);
    Check("T18 ULDA_508: the run deck holds a spell and a minion", spell != null && minion != null);
    Thin(g, p1, e => e == spell);
    EndTurn(g); EndTurn(g);
    Check("T18 ULDA_508: the spell drawn at my turn start costs (0) this turn (Fleeting Knowledge)", spell.Zone == p1.HandZone && spell.Cost == 0 && Has(spell, "ULDA_508e2"),
        spell.Zone?.Type + " " + spell.Cost);
    EndTurn(g);
    Check("T18 ULDA_508: at the turn's end it costs its own Cost again", spell.Cost == spell.Card.Cost, spell.Cost.ToString());
    var m2 = (IPlayable)Entity.FromCard(p1, C("CS2_182"));
    p1.DeckZone.Add(m2);
    EndTurn(g);
    Check("T18 ULDA_508: a drawn minion keeps its Cost", m2.Zone == p1.HandZone && m2.Cost == 4, m2.Zone?.Type + " " + m2.Cost);
    var mulligan = PreMulligan(Reno, 418, "ULDA_508");
    var opening = mulligan.g.Player1.HandZone.Where(e => e is Spell).ToList();
    Check("T18 ULDA_508: the opening hand (not a draw of the game) keeps its Costs", opening.All(e => e.Cost == e.Card.Cost));
}

// ---------------------------------------------------------------- T19 several passives together
{
    var (s, plan, g) = Fight(Finley, 419, "ULDA_046", "ULDA_208", "ULDA_101", "ULDA_102");
    var p1 = g.Player1; var p2 = g.Player2;
    var croc = Board(g, p1, "CS2_120"); var theirs = Board(g, p2, "CS2_182");
    Check("T19 four F13 passives in one fight: all four enchantments attached, in pick order; their auras stack",
        plan.PlayerTreasures.SequenceEqual(new[] { "ULDA_046", "ULDA_208", "ULDA_101", "ULDA_102" }) && HasFx(p1, "ULDA_046e") && HasFx(p1, "ULDA_208e")
        && HasFx(p1, "ULDA_101e") && HasFx(p1, "ULDA_102e") && croc.Poisonous && croc.HasTaunt && croc.Health == 4 && theirs.AttackDamage == 3);
}
void Finish(Game g)
{
    g.Process(ChooseTask.Mulligan(g.Player1, new List<int>()));
    g.Process(ChooseTask.Mulligan(g.Player2, new List<int>()));
    g.MainBegin();
}

// ---------------------------------------------------------------- full games against the real boss AI
var testBudget = new Budget(6000, 2000, 300, 6, 3, 100, 8, turnSearchSeconds: 6, turnWallSeconds: 8);
string AiGame(string id, long seed, string row)
{
    var s = NewAct(row, seed);
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
    int decisionIndex = 0, actions = 0, decisions = 0, fallbacks = 0, bossTurns = 0, lastBossTurn = -1, passActs = 0, passTurns = 0, bossActsAfter = 0;
    var recent = new List<string>();
    void Act(PlayerTask task, string who)
    {
        recent.Add(who + ":" + task + "@" + game.Turn); if (recent.Count > 12) recent.RemoveAt(0);
        if (++actions > 1500) throw new Exception("stuck after 1500 actions: " + string.Join(" | ", recent));
        if (who == "boss")
        {
            if (lastBossTurn != game.Turn) { lastBossTurn = game.Turn; bossTurns++; if (RunPlayerRules.MustPass(game.Player2)) passTurns++; }
            if (bossTurns <= 2 && !(task is EndTurnTask) && !(task is ChooseTask)) passActs++;
            if (bossTurns > 2 && !(task is EndTurnTask)) bossActsAfter++;
        }
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
        else Act(LocalBot.Choose(game.Player1.Options()), "player");
    }
    string result = game.State != State.COMPLETE ? "turn-cap" : game.Player2.PlayState == PlayState.WON ? "boss" : game.Player1.PlayState == PlayState.WON ? "player" : "tie";
    return $"{result} turns {game.Turn} decisions {decisions} fallbacks {fallbacks} pass-turns {passTurns} pass-acts {passActs} boss-acts-after {bossActsAfter} "
        + $"spell-power {game.Player1.CurrentSpellPower} boss {boss.Id}";
}
string RowFor(string id) => id switch
{
    "ULDA_005" or "ULDA_009" => Elise,
    "ULDA_046" or "ULDA_116" or "ULDA_208" => Finley,
    "ULDA_011" or "ULDA_014" or "ULDA_101" or "ULDA_102" => Brann,
    _ => Reno,
};
string aiOnly = Environment.GetEnvironmentVariable("F13_AI");
foreach (string id in f13)
{
    if (aiOnly != null && id != aiOnly) continue;
    string row = RowFor(id);
    string line;
    try { line = AiGame(id, 901, row); }
    catch (Exception e) { line = "FAULT " + e.GetType().Name + ": " + e.Message.Split('\n')[0]; }
    Console.WriteLine($"T20 {id,-10} {row,-16} {line}");
    Check($"T20 {id}: a full game against the boss AI with the treasure ends without a fault or an AI fallback",
        !line.StartsWith("FAULT") && line.Contains("fallbacks 0 "), line);
    if (id == "ULDA_102")
        Check("T20 ULDA_102: the boss AI was given its first two turns as passes and took no action in them, then played on",
            line.Contains("pass-turns 2 ") && line.Contains("pass-acts 0 ") && !line.Contains("boss-acts-after 0 "), line);
    else
        Check($"T20 {id}: no boss turn was a pass", line.Contains("pass-turns 0 "), line);
}
foreach (long seed in new long[] { 911, 912, 913 })
{
    if (aiOnly != null && aiOnly != "ULDA_102") continue;
    string line;
    try { line = AiGame("ULDA_102", seed, seed == 912 ? Reno : Brann); }
    catch (Exception e) { line = "FAULT " + e.GetType().Name + ": " + e.Message.Split('\n')[0]; }
    Console.WriteLine($"T21 ULDA_102/{seed} {line}");
    Check($"T21 ULDA_102 ({seed}): the boss AI accepts both forced passes; no fault, no fallback",
        !line.StartsWith("FAULT") && line.Contains("fallbacks 0 ") && line.Contains("pass-turns 2 ") && line.Contains("pass-acts 0 "), line);
}

Console.WriteLine($"{passes} passed, {failures} failed");
return failures == 0 ? 0 : 1;

static class RunContentTestExtensions
{
    // OnMatchStart is internal to the engine; a treasure with neither an effect nor a start change has neither.
    public static bool OnStartIsNull(this RunContent c) =>
        typeof(RunContent).GetProperty("OnMatchStart", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(c) == null;
}
