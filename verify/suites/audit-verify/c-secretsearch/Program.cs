// Phase 2 part A (design D2): Secret-aware fair search. Usage: AiHarness.exe [case]   (no argument = every case)
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using SabberStoneCore.Actions;
using SabberStoneCore.CardSets.DungeonRun;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

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
var data = DungeonRunData.Parse(File.ReadAllText(Path.Combine(root, "dungeon-run", "run-v1.json")),
    File.ReadAllText(Path.Combine(root, "dungeon-run", "pool-v1.json")), File.ReadAllText(Path.Combine(root, "npc", "decks-v1.json")));
var run = new DungeonRun(data);
string only = args.Length > 0 ? args[0] : null;

int passes = 0, failures = 0; var failLines = new List<string>(); string current = "";
bool Check(string what, bool ok, string detail = "")
{
    if (ok) passes++; else { failures++; failLines.Add(current + " :: " + what + (detail.Length > 0 ? "  (" + detail + ")" : "")); }
    if (!ok || Environment.GetEnvironmentVariable("VERBOSE") == "1") Console.WriteLine((ok ? "  PASS " : "  FAIL ") + what + (detail.Length > 0 ? "  (" + detail + ")" : ""));
    return ok;
}
void Note(string text) => Console.WriteLine("  NOTE " + text);
void Case(string name, Action body)
{
    if (only != null && only != name) return;
    current = name; Console.WriteLine("CASE " + name);
    var sw = Stopwatch.StartNew();
    try { body(); }
    catch (Exception e) { Check("threw", false, e.GetType().Name + ": " + e.Message.Split('\n')[0] + " @ " + (e.StackTrace ?? "").Split('\n').FirstOrDefault()?.Trim()); }
    Console.WriteLine($"  ({sw.ElapsedMilliseconds / 1000.0:F1}s)");
}

const string Yeti = "CS2_182", Wisp = "CS2_231", Croc = "CS2_120", Raptor = "CS2_172", Ogre = "CS2_200", Moonfire = "CS2_008", Flare = "EX1_544";
List<string> Fill(int n, string id = Yeti) => Enumerable.Repeat(id, n).ToList();
List<Card> Ids(IEnumerable<string> ids) => ids.Select(Cards.FromId).ToList();
var heroOf = new Dictionary<CardClass, string> { [CardClass.MAGE] = "HERO_08", [CardClass.HUNTER] = "HERO_05", [CardClass.PALADIN] = "HERO_04",
    [CardClass.ROGUE] = "HERO_03", [CardClass.DRUID] = "HERO_06", [CardClass.PRIEST] = "HERO_09", [CardClass.SHAMAN] = "HERO_02",
    [CardClass.WARLOCK] = "HERO_07", [CardClass.WARRIOR] = "HERO_01", [CardClass.DEATHKNIGHT] = "HERO_11" };
string HeroFor(Card c) => heroOf.TryGetValue(c.Class, out var h) ? h : "HERO_08";

Game NewRun(string playerHero = "HERO_08", string bossHero = "HERO_01", List<string> pdeck = null, List<string> bdeck = null, int seed = 99)
{
    var plan = new RunMatchPlan { PlayerHero = Cards.FromId(playerHero), PlayerDeck = Ids(pdeck ?? Fill(10)),
        BossHero = Cards.FromId(bossHero), BossDeck = Ids(bdeck ?? Fill(30)), BossHealth = 30 };
    var cfg = new GameConfig { RandomSeed = seed, Shuffle = true, SkipMulligan = false, History = true, Logging = false, FormatType = FormatType.FT_WILD };
    DungeonRunSetup.Configure(cfg, plan);
    var g = new Game(cfg); g.StartGame(); DungeonRunSetup.ApplyAfterStart(g, plan);
    g.Process(ChooseTask.Mulligan(g.Player1, new List<int>())); g.Process(ChooseTask.Mulligan(g.Player2, new List<int>())); g.MainBegin();
    ClearHand(g.Player1); ClearHand(g.Player2); Mana(g.Player1, 10);
    return g;
}
void ClearHand(Controller c) { while (c.HandZone.Count > 0) c.HandZone.Remove(c.HandZone[0]); }
void ClearDeck(Controller c) { while (c.DeckZone.Count > 0) c.DeckZone.Remove(c.DeckZone[0]); }
void Mana(Controller c, int n) { c.BaseMana = n; c.UsedMana = 0; c.OverloadLocked = 0; }
IPlayable Hand(Controller c, string id) { var e = Entity.FromCard(c, Cards.FromId(id)); Generic.AddHandPhase(c, e); return e; }
Minion Board(Controller c, string id, bool ready = false)
{
    var m = (Minion)Entity.FromCard(c, Cards.FromId(id)); Generic.SummonBlock(c.Game, m, -1, c.Hero);
    if (ready) { m.IsExhausted = false; m[GameTag.NUM_TURNS_IN_PLAY] = 1; }
    return m;
}
IPlayable DeckTop(Controller c, string id) { var e = Entity.FromCard(c, Cards.FromId(id)); c.DeckZone.Add(e); return e; }
bool Play(Game g, IPlayable card, ICharacter target = null, int chooseOne = 0) { Mana(card.Controller, 10); return g.Process(PlayCardTask.Any(card.Controller, card, target, -1, chooseOne)); }
void EndTurn(Game g) => g.Process(EndTurnTask.Any(g.CurrentPlayer));
void Kill(Game g, Minion m) { m.Destroy(); g.DeathProcessingAndAuraUpdate(); }
// A run player's Secret comes from the deck (drawn, then played), never generated into the hand.
bool PlayerSecret(Game g, string id) { var s = DeckTop(g.Player1, id); Generic.Draw(g.Player1, s); return s.Zone == g.Player1.HandZone && Play(g, s) && g.Player1.SecretZone.Contains(s); }
List<string> LiveSecrets(Controller c) => c.SecretZone.Where(s => s.IsSecret).Select(s => s.Card.Id).ToList();

// The boss's pilot pieces: a real boss's profile and plan (reduced headless budget, as c-bosspilot).
var bossRecord = data.Bosses.First(b => b.Archetype != null);
var bossInfo = DungeonRunBossPilot.Build(bossRecord, new RunMatchPlan { PlayerHero = Cards.FromId("HERO_08"), BossHero = Cards.FromId("HERO_01"), BossDeck = Ids(Fill(30)) }, null);
var testBudget = new Budget(6000, 2000, 300, 6, 3, 100, 8, turnSearchSeconds: 6, turnWallSeconds: 8);
var profile = bossInfo.Profile.WithBudget(testBudget);
var style = bossInfo.Deck.Style;
var fixOff = new AiFixes(new[] { "secret-worlds" });
GoldenPilot Pilot(IReadOnlyDictionary<string, double> prior, bool fix = true) => new GoldenPilot(fix ? profile : profile.WithFixes(fixOff), bossInfo.Plan, null, prior);
FairBotState View(Game g, IReadOnlyDictionary<string, double> prior, int seed = 12345) => Pilot(prior).Options.FairViewFactory(g, g.Player2.PlayerId, seed, style);
// The reference for install equivalence: the same fair view, but the opponent's real Secret actor kept live in the copy.
FairBotState KeptView(Game g, int seed = 12345) => FairBotState.Create(g, g.Player2.PlayerId, seed, style, new FairOptions {
    KeepOpponentSecrets = true, ShowOwnCarriedCards = true, ShowPublicCaptures = true, TrackOwnDeck = true, MaterializeOwnDeckTriggers = true });
FairBotState WorldView(Game g, string secret, int seed = 12345) => FairBotState.Create(g, g.Player2.PlayerId, seed, style, new FairOptions {
    RemoveOpponentSecrets = true, ShowOwnCarriedCards = true, ShowPublicCaptures = true, TrackOwnDeck = true, MaterializeOwnDeckTriggers = true,
    OpponentSecretPrior = new Dictionary<string, double> { [secret] = 1 } });
IReadOnlyDictionary<string, double> PriorOf(params string[] ids) => ids.ToDictionary(id => id, id => 2.0, StringComparer.Ordinal);

// No entity, enchantment or history record of the view names the hidden Secret.
void LeakChecks(FairBotState v, Card secret, int liveCount, string tag)
{
    var vg = v.FingerprintGame;
    Check(tag + ": public Secret count", v.Facts.OpponentSecretCount == liveCount, "count " + v.Facts.OpponentSecretCount + " live " + liveCount);
    Check(tag + ": one reserved slot per Secret", v.OpponentSecretSlots.Count == liveCount, "slots " + v.OpponentSecretSlots.Count);
    Check(tag + ": every slot is Unknown", v.OpponentSecretSlots.All(id => vg.IdEntityDic.TryGetValue(id, out var e) && e is Unknown), string.Join(",", v.OpponentSecretSlots));
    Check(tag + ": no live opponent Secret in the copy", vg.Player1.SecretZone.All(c => !c.IsSecret), string.Join(",", vg.Player1.SecretZone.Select(c => c.Card.Id)));
    var named = vg.IdEntityDic.Values.Where(e => e.Card != null && (e.Card.Id == secret.Id || e.Card.AssetId == secret.AssetId)).Select(e => e.Id + ":" + e.Zone?.Type).ToList();
    Check(tag + ": no entity carries the Secret's identity", named.Count == 0, string.Join(",", named));
    var leaks = new List<string>();
    foreach (var fact in vg.HistoryFacts.Query.Events<SabberStoneCore.Model.HistoryFact>())
        foreach (var prop in fact.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            object value; try { value = prop.GetValue(fact); } catch { continue; }
            if (value is int n && prop.Name.IndexOf("Dbf", StringComparison.OrdinalIgnoreCase) >= 0 && n == secret.AssetId
                || value is string s && s == secret.Id || value is Card c && c.Id == secret.Id)
                leaks.Add(fact.GetType().Name + "." + prop.Name);
        }
    Check(tag + ": no history record names the Secret", leaks.Count == 0, string.Join(",", leaks.Distinct()));
}

string[] secretCards = { "AT_002", "AT_060", "AT_073", "BOT_908", "CFM_026", "CFM_620", "CFM_800", "DAL_570", "EX1_130", "EX1_132", "EX1_136",
    "EX1_287", "EX1_289", "EX1_294", "EX1_295", "EX1_379", "EX1_533", "EX1_554", "EX1_594", "EX1_609", "EX1_610", "EX1_611", "FP1_018",
    "FP1_020", "GIL_577", "GIL_903", "ICC_082", "ICC_200", "KAR_004", "LOE_021", "LOE_027", "LOOT_079", "LOOT_101", "LOOT_204", "LOOT_210",
    "LOOT_214", "TRL_400", "ULD_152", "UNG_024", "tt_010", "ULD_239", "ULDA_045t" };

// ----------------------------------------------------------------------------------------------- 1 NoLeak
Case("NoLeak", () =>
{
    int tested = 0; var skipped = new List<string>();
    foreach (string id in secretCards)
    {
        var card = Cards.FromId(id);
        if (card == null) { Check(id + " exists", false); continue; }
        var g = NewRun(HeroFor(card));
        if (!PlayerSecret(g, id)) { skipped.Add(id + (card.Implemented ? "(not installable)" : "(unscripted)")); continue; }
        EndTurn(g);
        int live = g.Player1.SecretZone.Count(s => s.IsSecret);
        if (live != 1) { skipped.Add(id + "(left play before the boss turn)"); continue; }
        LeakChecks(View(g, null), card, 1, id);
        LeakChecks(View(g, PriorOf(id)), card, 1, id + " closed");
        tested++;
    }
    Note("tested " + tested + "/" + secretCards.Length + "; skipped " + string.Join(" ", skipped));
    Check("most secret-card rows tested", tested >= secretCards.Length - 4, tested.ToString());

    // Legacy Create with a Rewind pending: the Secret refusal is skipped there, so the leak fix must remove the Secret.
    var rg = NewRun("HERO_08");
    PlayerSecret(rg, "EX1_295");
    EndTurn(rg);
    var boss = rg.Player2; ClearHand(boss);
    var crasher = Hand(boss, "TIME_004");
    Check("boss plays Conflux Crasher", Play(rg, crasher), "");
    bool offered = rg.Player2.Choice?.ChoiceAction == ChoiceAction.REWIND;
    Check("a Rewind offer is pending", offered, rg.Player2.Choice?.ChoiceAction.ToString() ?? "no choice");
    if (offered)
    {
        var legacy = FairBotState.Create(rg, rg.Player2.PlayerId, 777, style);
        Check("legacy Rewind view holds the keep answer", legacy.RewindKeep != null, "");
        LeakChecks(legacy, Cards.FromId("EX1_295"), 1, "legacy rewind");
        LeakChecks(View(rg, null), Cards.FromId("EX1_295"), 1, "golden rewind");
    }
});

// ----------------------------------------------------------------------------------------------- 2 ClosedPoolComplete
var pools = new (string cls, string hero, string[] ids)[] {
    ("HUNTER", "HERO_05", new[] { "EX1_611", "EX1_610", "EX1_554", "EX1_533", "EX1_609" }),
    ("MAGE", "HERO_08", new[] { "EX1_295", "EX1_287", "EX1_294", "EX1_594", "tt_010", "EX1_289" }),
    ("PALADIN", "HERO_04", new[] { "EX1_130", "EX1_136", "EX1_132", "EX1_379", "FP1_020" }) };
Case("ClosedPoolComplete", () =>
{
    // DungeonRunAdmission.SecretPrior: 1 + copies across the class's starter decks and bundles; never another class.
    var syn = new DungeonRunData();
    syn.Classes.Add(new DungeonRunClass { Id = "HUNTER", Name = "Hunter", HeroCard = "HERO_05" });
    syn.Decks.Add(new DungeonRunStarterDeck { Id = "h1", Class = "HUNTER", Cards = { "EX1_611", "EX1_611", "EX1_610", Yeti } });
    syn.Bundles.Add(new DungeonRunBundle { Id = "b1", Class = "HUNTER", Cards = { "EX1_554", Yeti } });
    syn.Decks.Add(new DungeonRunStarterDeck { Id = "m1", Class = "MAGE", Cards = { "EX1_295" } });
    var sp = DungeonRunAdmission.SecretPrior(syn, CardClass.HUNTER);
    Check("synthetic Hunter prior", string.Join(",", sp.Select(p => p.Key + "=" + p.Value)) == "EX1_554=2,EX1_610=2,EX1_611=3", string.Join(",", sp.Select(p => p.Key + "=" + p.Value)));
    Check("synthetic Mage prior is its own", string.Join(",", DungeonRunAdmission.SecretPrior(syn, CardClass.MAGE).Keys) == "EX1_295", "");
    Note("real run data priors: " + string.Join(" ", data.Classes.Select(c => c.Id + "=" + (DungeonRunAdmission.SecretPrior(data, Enum.Parse<CardClass>(c.Id))?.Count.ToString() ?? "null"))));
    Check("real run data: no Secret in the run pool, so every prior is null (open pool, no settlement)",
        data.Classes.All(c => DungeonRunAdmission.SecretPrior(data, Enum.Parse<CardClass>(c.Id)) == null));

    foreach (var pool in pools)
    {
        var prior = PriorOf(pool.ids); int n = pool.ids.Length;
        foreach (var played in new[] { new[] { pool.ids[0] }, new[] { pool.ids[1], pool.ids[3] } })
        {
            var g = NewRun(pool.hero);
            foreach (var id in played) Check(pool.cls + " plays " + id, PlayerSecret(g, id));
            EndTurn(g);
            var real = LiveSecrets(g.Player1);
            var v = View(g, prior);
            var worlds = v.PlausibleOpponentSecretWorlds(out bool complete, out double mass);
            string tag = pool.cls + " x" + played.Length;
            Check(tag + ": complete", complete, "");
            Check(tag + ": world count", worlds.Count == (played.Length == 1 ? n : n * (n - 1)), worlds.Count.ToString());
            Check(tag + ": real Secrets among the worlds", worlds.Any(w => w.SequenceEqual(real)), string.Join(",", real));
            Check(tag + ": only pool ids", worlds.All(w => w.All(prior.ContainsKey)), "");
            var open = View(g, null).PlausibleOpponentSecretWorlds(out bool openComplete, out _);
            Note(tag + ": open pool (no prior) gives " + open.Count + " worlds, complete=" + openComplete);
        }
        // Mad Scientist pulls a deck Secret with no hidden play: the closed pool does not widen.
        var ms = NewRun(pool.hero);
        ClearDeck(ms.Player1); DeckTop(ms.Player1, Yeti); DeckTop(ms.Player1, pool.ids[2]); DeckTop(ms.Player1, Yeti);
        PlayerSecret(ms, pool.ids[0]);
        var sci = Board(ms.Player1, "FP1_004");
        Kill(ms, sci);
        Check(pool.cls + " Mad Scientist pulled the deck Secret", LiveSecrets(ms.Player1).Contains(pool.ids[2]), string.Join(",", LiveSecrets(ms.Player1)));
        EndTurn(ms);
        var mreal = LiveSecrets(ms.Player1);
        var mworlds = View(ms, prior).PlausibleOpponentSecretWorlds(out bool mcomplete, out _);
        Check(pool.cls + " after Mad Scientist: complete", mcomplete, "");
        Check(pool.cls + " after Mad Scientist: world count", mworlds.Count == n * (n - 1), mworlds.Count.ToString());
        Check(pool.cls + " after Mad Scientist: real among worlds", mworlds.Any(w => w.SequenceEqual(mreal)), string.Join(",", mreal));
        var mopen = View(ms, null).PlausibleOpponentSecretWorlds(out bool mopenComplete, out _);
        Note(pool.cls + " after Mad Scientist, open pool: " + mopen.Count + " worlds, complete=" + mopenComplete);
    }
});

// ----------------------------------------------------------------------------------------------- 3 Widen
// The pre-phase-2 world builder, verbatim, over the same view: with no prior the new code must give the same answer.
var secretPoolMethod = typeof(FairBotState).GetMethod("SecretPool", BindingFlags.Static | BindingFlags.NonPublic);
(List<string[]> worlds, bool complete) OldWorlds(FairBotState v)
{
    var game = v.FingerprintGame; var bot = game.Player2; var opp = game.Player1; int count = v.Facts.OpponentSecretCount;
    bool complete = true;
    if (count == 0 || opp.SecretZone.Any(c => c.IsSecret)) return (new List<string[]>(), true);
    var prior = CardFrequencies.ForOrEmpty(opp.BaseClass).Where(f => Cards.FromId(f.CardId).IsSecret).ToDictionary(f => f.CardId, f => f.Weight, StringComparer.Ordinal);
    var pool = new HashSet<string>((string[])secretPoolMethod.Invoke(null, new object[] { opp.BaseClass }), StringComparer.Ordinal);
    pool.UnionWith(prior.Keys);
    bool broad = game.HistoryFacts.RecordCount > FairBotState.RootHistoryLimit;
    if (broad) complete = false;
    else {
        var history = game.HistoryFacts.Query;
        var publicCards = history.Events<HistoryCardFact>().Where(f => f.DefinitionKnown).ToArray();
        pool.UnionWith(publicCards.Where(f => f.ControllerId == opp.Id && Cards.FromAssetId(f.DbfId).IsSecret).Select(f => Cards.FromAssetId(f.DbfId).Id));
        var knownNonSecrets = new HashSet<int>(publicCards.Where(f => f.EntityId != 0 && !Cards.FromAssetId(f.DbfId).IsSecret).Select(f => f.EntityId));
        long firstTurn = history.Events<TurnStartedFact>().Select(f => f.Sequence).DefaultIfEmpty(0).First();
        var hiddenPlays = history.Plays().Where(f => f.ControllerId == opp.Id && !f.DefinitionKnown).ToArray();
        long lastSecretPlay = hiddenPlays.Length == count ? hiddenPlays.Max(f => f.Sequence) : long.MaxValue;
        broad = firstTurn == 0 || hiddenPlays.Length != count
            || history.HandEntries().Any(f => f.Sequence > firstTurn && f.Sequence <= lastSecretPlay && f.ControllerId == opp.Id
                && f.EntryKind == HistoryHandEntryKind.Generated && !knownNonSecrets.Contains(f.EntityId))
            || history.Events<ShuffleOperationFact>().Any(f => f.Sequence > firstTurn && f.Sequence <= lastSecretPlay && f.DestinationControllerId == opp.Id)
            || history.Events<CardTransferredFact>().Any(f => f.ControllerId == opp.Id)
            || publicCards.Any(f => Cards.FromAssetId(f.DbfId).Id == "TIME_860");
    }
    if (broad || pool.Count < count) pool.UnionWith(Cards.All.Where(c => c.Implemented && c.IsSecret).Select(c => c.Id).Distinct(StringComparer.Ordinal));
    var ordered = pool.OrderByDescending(id => prior.TryGetValue(id, out double w) ? w : 0).ThenBy(id => id, StringComparer.Ordinal).ToArray();
    var worlds = new List<string[]>(); var selected = new string[count];
    bool Visit(int depth)
    {
        if (depth == selected.Length) { if (worlds.Count == 128) return false; worlds.Add((string[])selected.Clone()); return true; }
        foreach (string id in ordered) { if (selected.Take(depth).Contains(id)) continue; selected[depth] = id; if (!Visit(depth + 1)) return false; }
        return true;
    }
    complete &= Visit(0) && worlds.Count != 0;
    return (worlds, complete);
}
Case("Widen", () =>
{
    var hunter = pools[0]; var prior = PriorOf(hunter.ids);
    var scenarios = new List<(string name, Action<Game> before)> {
        ("generated hand entry", g => { var portal = Hand(g.Player1, "GVG_003"); Check("Unstable Portal played", Play(g, portal)); }),
        ("transfer", g => { var drawn = Generic.DrawFrom(g.Player1, g.Player2); Check("drew from the boss deck", drawn != null && drawn.Controller == g.Player1); }),
        ("shuffle", g => { var idol = Hand(g.Player1, "CFM_602"); Check("Jade Idol shuffle played", Play(g, idol, null, 2)); }),
    };
    foreach (var (name, before) in scenarios)
    {
        var g = NewRun(hunter.hero);
        before(g);
        Check(name + ": Secret played", PlayerSecret(g, hunter.ids[0]));
        EndTurn(g);
        var v = View(g, prior);
        var worlds = v.PlausibleOpponentSecretWorlds(out bool complete, out _);
        Check(name + ": closed pool widened, not complete", !complete, worlds.Count + " worlds");
        Check(name + ": widened worlds still start with the pool", worlds.Count > 0 && prior.ContainsKey(worlds[0][0]), worlds.FirstOrDefault()?.FirstOrDefault() ?? "none");
        if (name == "shuffle") Check("shuffle recorded a ShuffleOperationFact", g.HistoryFacts.Query.Events<ShuffleOperationFact>().Any(), "");
        if (name == "transfer") Check("transfer recorded a CardTransferredFact", g.HistoryFacts.Query.Events<CardTransferredFact>().Any(), "");
    }
    // No prior (world deck, ladder): identical to the pre-phase-2 builder on the same positions.
    var positions = new List<(string name, Func<Game> make)> {
        ("hunter x1", () => { var g = NewRun("HERO_05"); PlayerSecret(g, "EX1_611"); EndTurn(g); return g; }),
        ("mage x2", () => { var g = NewRun("HERO_08"); PlayerSecret(g, "EX1_295"); PlayerSecret(g, "EX1_287"); EndTurn(g); return g; }),
        ("paladin generated", () => { var g = NewRun("HERO_04"); Play(g, Hand(g.Player1, "GVG_003")); PlayerSecret(g, "EX1_130"); EndTurn(g); return g; }),
        ("mad scientist", () => { var g = NewRun("HERO_08"); ClearDeck(g.Player1); DeckTop(g.Player1, "EX1_294"); Kill(g, Board(g.Player1, "FP1_004")); EndTurn(g); return g; }),
    };
    foreach (var (name, make) in positions)
    {
        var v = View(make(), null);
        var now = v.PlausibleOpponentSecretWorlds(out bool complete, out _);
        var old = OldWorlds(v);
        Check(name + ": no-prior worlds identical to the old builder", now.Count == old.worlds.Count && now.Zip(old.worlds, (a, b) => a.SequenceEqual(b)).All(x => x),
            now.Count + " vs " + old.worlds.Count);
        Check(name + ": no-prior completeness identical", complete == old.complete, complete + " vs " + old.complete);
    }
});

// ----------------------------------------------------------------------------------------------- 4 InstallEquivalence
string Snap(FairBotState s)
{
    var f = s.Facts; var b = new System.Text.StringBuilder();
    string Card(FairCard c) => c == null ? "-" : (c.Unknown ? "?" : c.CardId) + ":" + c.Attack + "/" + c.Health + "+" + c.Armor + (c.DivineShield ? "d" : "") + (c.Frozen ? "f" : "") + (c.Immune ? "i" : "") + ":" + c.Cost;
    foreach (var p in new[] { f.Own, f.Opponent })
        b.Append(p.PlayerId).Append('|').Append(p.PlayState).Append('|').Append(p.Mana).Append('|').Append(p.DeckCount).Append("|hero ").Append(Card(p.Hero))
         .Append("|weapon ").Append(Card(p.Weapon)).Append("|board ").Append(string.Join(",", p.Board.Select(Card)))
         .Append("|hand ").Append(p == f.Own ? string.Join(",", p.Hand.Select(Card)) : p.Hand.Count.ToString()).Append('\n');
    var game = s.FingerprintGame;
    foreach (var c in new[] { game.Player1, game.Player2 })
        b.Append("secrets ").Append(string.Join(",", c.SecretZone.Where(x => x.IsSecret).Select(x => x.Card.Id)))
         .Append(" grave ").Append(string.Join(",", c.GraveyardZone.Where(x => !(x is Unknown)).Select(x => x.Card.Id).OrderBy(x => x, StringComparer.Ordinal))).Append('\n');
    b.Append("turn ").Append(f.Turn).Append(' ').Append(f.CurrentPlayerId);
    return b.ToString();
}
int Src(FairBotState s, string cardId) => s.Facts.Own.Hand.Concat(s.Facts.Own.Board).FirstOrDefault(c => c.CardId == cardId)?.EntityId ?? -1;
BotAction Find(FairBotState s, PlayerTaskType type, int source, int target = 0) =>
    s.Actions.FirstOrDefault(a => a.Type == type && (source == 0 || a.Source == source) && (target == 0 || a.Target == target));
var steps = new List<(string name, Func<FairBotState, BotAction>[] acts)> {
    ("attack-hero", new Func<FairBotState, BotAction>[] { s => Find(s, PlayerTaskType.MINION_ATTACK, Src(s, Yeti), s.Facts.Opponent.Hero.EntityId) }),
    ("attack-minion", new Func<FairBotState, BotAction>[] { s => Find(s, PlayerTaskType.MINION_ATTACK, Src(s, Yeti), s.Facts.Opponent.Board.First(c => c.CardId == Croc).EntityId) }),
    ("play-minion", new Func<FairBotState, BotAction>[] { s => Find(s, PlayerTaskType.PLAY_CARD, Src(s, Wisp)) }),
    ("spell-hero", new Func<FairBotState, BotAction>[] { s => Find(s, PlayerTaskType.PLAY_CARD, Src(s, Moonfire), s.Facts.Opponent.Hero.EntityId) }),
    ("kill-minion", new Func<FairBotState, BotAction>[] { s => Find(s, PlayerTaskType.PLAY_CARD, Src(s, Moonfire), s.Facts.Opponent.Board.First(c => c.CardId == Wisp).EntityId) }),
    ("hero-power", new Func<FairBotState, BotAction>[] { s => Find(s, PlayerTaskType.HERO_POWER, 0) }),
    ("three-cards", new Func<FairBotState, BotAction>[] { s => Find(s, PlayerTaskType.PLAY_CARD, Src(s, Wisp)), s => Find(s, PlayerTaskType.PLAY_CARD, Src(s, Wisp)), s => Find(s, PlayerTaskType.PLAY_CARD, Src(s, Wisp)) }),
    ("flare", new Func<FairBotState, BotAction>[] { s => Find(s, PlayerTaskType.PLAY_CARD, Src(s, Flare)) }),
    ("end-turn", new Func<FairBotState, BotAction>[] { s => Find(s, PlayerTaskType.END_TURN, 0) }),
};
string RunSteps(FairBotState s, Func<FairBotState, BotAction>[] acts)
{
    try {
        foreach (var pick in acts) { var a = pick(s); if (a == null) return Snap(s) + "\ntruncated"; s = s.Apply(a); }
        return Snap(s);
    }
    catch (Exception e) { return "error " + e.GetType().Name; }
}
Game EquivalenceBase(string secret, bool lowHero, int bossPlaysFirst, bool rhyssa)
{
    var card = Cards.FromId(secret);
    var g = NewRun(HeroFor(card));
    Board(g.Player1, Croc); Board(g.Player1, Wisp);
    if (rhyssa) Board(g.Player1, "DAL_573");
    if (!PlayerSecret(g, secret)) return null;
    EndTurn(g);
    if (!g.Player1.SecretZone.Any(c => c.IsSecret)) return null;
    var boss = g.Player2; ClearHand(boss); Mana(boss, 10);
    foreach (var id in new[] { Wisp, Wisp, Wisp, Moonfire, Flare }) Hand(boss, id);
    Board(boss, Yeti, true); Board(boss, Raptor, true);
    if (lowHero) g.Player1.Hero.Damage = g.Player1.Hero.BaseHealth - 3;
    for (int i = 0; i < bossPlaysFirst; i++) { var w = boss.HandZone.First(c => c.Card.Id == Wisp); g.Process(PlayCardTask.Any(boss, w)); }
    // The setup itself (a summon, an early play) can spend the Secret; such a base tests nothing.
    return g.Player1.SecretZone.Count(c => c.IsSecret) == 1 ? g : null;
}
Case("InstallEquivalence", () =>
{
    var bases = new List<(string name, bool low, int first, bool rhyssa, string[] steps)> {
        ("plain", false, 0, false, null),
        ("fatal", true, 0, false, new[] { "attack-hero", "spell-hero" }),
        ("counter-mid-turn", false, 1, false, new[] { "three-cards", "play-minion" }),
    };
    bool rhyssaScripted = Cards.FromId("DAL_573")?.Implemented == true;
    if (rhyssaScripted) bases.Add(("rhyssa", false, 0, true, null));
    else Note("Commander Rhyssa (DAL_573) is unscripted (engine-gated); its double trigger is not exercised");
    int compared = 0, fired = 0; var unequal = new List<string>(); var neverFired = new List<string>(); var skippedBases = new List<string>();
    foreach (string id in secretCards)
    {
        bool anyFire = false;
        foreach (var b in bases)
        {
            var g = EquivalenceBase(id, b.low, b.first, b.rhyssa);
            if (g == null) { skippedBases.Add(id + "/" + b.name); continue; }
            var world = WorldView(g, id).WithOpponentSecrets(new[] { id }, 3);
            var kept = KeptView(g).WithArtificialSeed(3);
            foreach (var st in steps.Where(x => b.steps == null || b.steps.Contains(x.name)))
            {
                string a = RunSteps(world, st.acts), r = RunSteps(kept, st.acts);
                compared++;
                if (a != r) unequal.Add(id + "/" + b.name + "/" + st.name + "\n      world: " + a.Replace("\n", " | ") + "\n      real:  " + r.Replace("\n", " | "));
                if (!r.Contains("secrets " + id) && !r.StartsWith("error")) { anyFire = true; fired++; }
            }
        }
        if (!anyFire) neverFired.Add(id);
    }
    Note("compared " + compared + " scenario runs; " + fired + " consumed the Secret; never consumed: " + string.Join(",", neverFired));
    Note("bases not built (Secret not installable or spent by the setup): " + string.Join(",", skippedBases));
    foreach (var u in unequal) Console.WriteLine("  DIFF " + u);
    Check("installed worlds match the real Secret", unequal.Count == 0, unequal.Count + " differ");
    // The install itself: what the engine's CastSpell leaves behind.
    var g0 = EquivalenceBase("EX1_611", false, 0, false);
    var w0 = WorldView(g0, "EX1_611").WithOpponentSecrets(new[] { "EX1_611" }, 1);
    var installed = w0.FingerprintGame.Player1.SecretZone.Single(c => c.IsSecret);
    var realSecret = g0.Player1.SecretZone.Single(c => c.IsSecret);
    Check("world Secret has the real one's exhaustion on the boss's turn", installed.IsExhausted == realSecret.IsExhausted && !installed.IsExhausted, installed.IsExhausted + " vs " + realSecret.IsExhausted);
    Check("world Secret sits in the reserved slot", installed.Id == w0.OpponentSecretSlots[0], installed.Id + " vs " + w0.OpponentSecretSlots[0]);
});

// ----------------------------------------------------------------------------------------------- 5 Settle / 6 Determinism / 7 Budget
DeckPlanEvaluator Evaluator(FairBotState v)
{
    var e = new DeckPlanEvaluator(bossInfo.Plan, profile.Budget.Brain) { RecurringValue = profile.Fixes.On("recurring-value"), ZeroDoomed = profile.Fixes.On("doomed-minions"),
        DeathFixes = profile.Fixes, TriggerFixes = profile.Fixes, OwnDeckValue = profile.Fixes.On("own-deck-value") };
    e.SetOpponentEvidence(v.ReplyClass, new string[0]);
    if (e.TriggerValue) e.SetTriggerEvidence(v.OwnEvidence, v.OpponentEvidence);
    return e;
}
TurnPlannerJob Plan(FairBotState v, bool fix, int seed = 4242)
{
    var p = fix ? profile : profile.WithFixes(fixOff);
    var job = TurnPlanner.Job(v, p, bossInfo.Plan, seed);
    int guard = 0;
    while (!job.IsDone && guard++ < 1000000) job.StepNode();
    return job;
}
double SettledValue(FairBotState v, IReadOnlyList<BotAction> line, int seed = 4242)
{
    var ev = Evaluator(v);
    var acts = line.Where(a => a.Type != PlayerTaskType.END_TURN).ToList();
    if (acts.Count == 0) return LethalJob.ClampValue(v.Evaluate(ev));
    var job = LethalJob.Settle(v, acts, 100000, s => s.Evaluate(ev), TurnPlannerJob.SettleWorldLimit, seed);
    while (!job.IsDone) job.StepNode();
    return job.SettledValue ?? double.NaN;
}
string Line(IReadOnlyList<BotAction> line) => string.Join(" ", line.Select(a => a.ToString()));
// The lines that can trigger none of the tested Secrets: passing, and the hero power alone when it is playable untargeted.
List<(string name, double settled)> SafeLines(FairBotState v)
{
    var list = new List<(string name, double settled)> { ("pass", SettledValue(v, new List<BotAction>())) };
    var hp = v.Actions.FirstOrDefault(a => a.Type == PlayerTaskType.HERO_POWER && a.Target == 0);
    if (hp != null) list.Add(("hero power", SettledValue(v, new List<BotAction> { hp })));
    return list;
}
var positions = new List<(string name, string hero, string secret, Func<Game, Game> setup)> {
    ("explosive-trap", "HERO_05", "EX1_610", g => { var o = g.Player2; Board(o, Raptor, true); Board(o, Raptor, true); Board(o, Raptor, true); g.Player1.Hero.Damage = 18; return g; }),
    ("freezing-trap", "HERO_05", "EX1_611", g => { var o = g.Player2; Board(o, Ogre, true); g.Player1.Hero.Damage = 10; return g; }),
    ("vaporize", "HERO_08", "EX1_594", g => { var o = g.Player2; Board(o, Ogre, true); g.Player1.Hero.Damage = 10; return g; }),
};
Game Position(string hero, string secret, Func<Game, Game> setup, bool withSecret = true)
{
    var g = NewRun(hero);
    Board(g.Player1, Wisp);
    if (withSecret) PlayerSecret(g, secret);
    EndTurn(g);
    ClearHand(g.Player2); Mana(g.Player2, 10);
    return setup(g);
}
Case("Settle", () =>
{
    foreach (var pos in positions)
    {
        var g = Position(pos.hero, pos.secret, pos.setup);
        var prior = PriorOf(pos.secret);
        var on = Plan(View(g, prior), true); var off = Plan(View(g, prior), false);
        double son = SettledValue(View(g, prior), on.Result.Line), soff = SettledValue(View(g, prior), off.Result.Line);
        Note(pos.name + " on : " + Line(on.Result.Line) + "  value " + on.Result.Value.ToString("F1") + " settled " + son.ToString("F1")
            + "  [settled lines " + on.DiagnosticSettledLines + ", nodes " + on.DiagnosticSettleNodes + "/" + on.DiagnosticSettleReserve + ", worlds " + on.DiagnosticSettleWorlds + "]");
        Note(pos.name + " off: " + Line(off.Result.Line) + "  value " + off.Result.Value.ToString("F1") + " settled " + soff.ToString("F1"));
        Check(pos.name + ": fix on and off choose different lines", Line(on.Result.Line) != Line(off.Result.Line), "");
        Check(pos.name + ": the fix-on line has the higher settled value", son > soff, son.ToString("F1") + " vs " + soff.ToString("F1"));
        var safe = SafeLines(View(g, prior));
        Note(pos.name + " Secret-safe lines: " + string.Join(", ", safe.Select(x => x.name + " " + x.settled.ToString("F1"))));
        Check(pos.name + ": the fix-on line is at least as good as the best Secret-safe line (settled)", son >= safe.Max(x => x.settled) - 1e-6,
            son.ToString("F2") + " vs " + safe.Max(x => x.settled).ToString("F2") + " (" + safe.OrderByDescending(x => x.settled).First().name + ")");
        Check(pos.name + ": a playable hero power beats passing (not a bare END_TURN)", !safe.Any(x => x.name == "hero power") || on.Result.Line.Any(a => a.Type != PlayerTaskType.END_TURN), Line(on.Result.Line));
    }
    // The reviewed bias case: Redemption, a ready boss Raptor against the player's Wisp. The hero power alone triggers
    // nothing; the planner must not pass while the Secret-free twin plays Armor Up and attacks.
    {
        var g = Position("HERO_04", "EX1_136", x => { Board(x.Player2, Raptor, true); return x; });
        var prior = PriorOf("EX1_136");
        var on = Plan(View(g, prior), true);
        double son = SettledValue(View(g, prior), on.Result.Line);
        var safe = SafeLines(View(g, prior));
        var rtwin = Plan(View(Position("HERO_04", "EX1_136", x => { Board(x.Player2, Raptor, true); return x; }, false), null), true);
        Note("redemption on : " + Line(on.Result.Line) + "  value " + on.Result.Value.ToString("F1") + " settled " + son.ToString("F1")
            + " [settled " + on.DiagnosticSettledLines + ", swapped in " + on.DiagnosticSwappedIn.Count + ", defaults " + on.DiagnosticUnsettledDefaults + "]");
        Note("redemption twin: " + Line(rtwin.Result.Line) + "; safe lines " + string.Join(", ", safe.Select(x => x.name + " " + x.settled.ToString("F2"))));
        Check("redemption: the boss does not pass when the hero power alone is Secret-safe", on.Result.Line.Any(a => a.Type != PlayerTaskType.END_TURN), Line(on.Result.Line));
        Check("redemption: the fix-on line is at least as good as the best Secret-safe line (settled)", son >= safe.Max(x => x.settled) - 1e-6,
            son.ToString("F2") + " vs " + safe.Max(x => x.settled).ToString("F2"));
    }
    // Ice Block in the Mage pool refutes an on-board lethal.
    var ib = NewRun("HERO_08"); PlayerSecret(ib, "EX1_295"); EndTurn(ib);
    ClearHand(ib.Player2); Mana(ib.Player2, 10); Board(ib.Player2, Ogre, true); Board(ib.Player2, Ogre, true);
    ib.Player1.Hero.Damage = ib.Player1.Hero.BaseHealth - 6;
    foreach (bool fix in new[] { true, false })
    {
        var job = Plan(View(ib, PriorOf("EX1_295", "EX1_287", "EX1_294")), fix);
        Check("Ice Block (fix " + (fix ? "on" : "off") + "): no verified lethal", !job.HasVerifiedLethal && job.Result.Value < 999000, job.Result.Value.ToString("F0") + " " + Line(job.Result.Line));
    }
    var twin = NewRun("HERO_08"); EndTurn(twin);
    ClearHand(twin.Player2); Mana(twin.Player2, 10); Board(twin.Player2, Ogre, true); Board(twin.Player2, Ogre, true);
    twin.Player1.Hero.Damage = twin.Player1.Hero.BaseHealth - 6;
    Check("Secret-free twin: the same board is a verified lethal", Plan(View(twin, null), true).HasVerifiedLethal, "");
});
// Exposure: every scored line with actions is settled, or carries the worst settled delta; a lone settled leader is never
// compared against unsettled exposed siblings.
Case("Exposure", () =>
{
    Game Wide(bool secrets)
    {
        var g = NewRun("HERO_05");
        Board(g.Player1, Croc); Board(g.Player1, Wisp);
        if (secrets) { PlayerSecret(g, "EX1_610"); PlayerSecret(g, "EX1_611"); }
        EndTurn(g);
        var o = g.Player2; ClearHand(o); Mana(o, 10);
        foreach (var id in new[] { Raptor, Raptor, Yeti, Moonfire, Wisp, Croc }) Hand(o, id);
        Board(o, Raptor, true); Board(o, Yeti, true); Board(o, Ogre, true);
        return g;
    }
    // Five finalists (ReplyL 5) at a larger plan cap, so a fourth and fifth finalist exist to be compared with the leader.
    var fiveProfile = profile.WithBudget(new Budget(20000, 8000, 300, 6, 5, 100, 8, turnSearchSeconds: 6, turnWallSeconds: 8));
    TurnPlannerJob PlanFive(FairBotState v) { var j = TurnPlanner.Job(v, fiveProfile, bossInfo.Plan, 4242); int guard = 0; while (!j.IsDone && guard++ < 1000000) j.StepNode(); return j; }
    foreach (var (name, game, prior) in new[] {
        ("wide", Wide(true), PriorOf(pools[0].ids)),
        ("explosive-trap", Position(positions[0].hero, positions[0].secret, positions[0].setup), PriorOf(positions[0].secret)) })
    {
        var job = PlanFive(View(game, prior));
        var exposed = job.DiagnosticScored.Where(l => l.Actions.Any(a => a.Type != PlayerTaskType.END_TURN)).ToList();
        int unsettled = exposed.Count(l => !job.DiagnosticSecretDelta.ContainsKey(l));
        Note(name + ": scored " + job.DiagnosticScored.Count + ", exposed " + exposed.Count + ", settled " + job.DiagnosticSettledLines + ", unsettled defaults " + job.DiagnosticUnsettledDefaults
            + ", swapped in " + job.DiagnosticSwappedIn.Count + ", settlement " + job.DiagnosticSettleNodes + "/" + job.DiagnosticSettleReserve);
        Check(name + ": at least four lines compared", job.DiagnosticScored.Count >= 4, job.DiagnosticScored.Count.ToString());
        Check(name + ": every unsettled exposed line got the conservative default", unsettled == job.DiagnosticUnsettledDefaults, unsettled + " vs " + job.DiagnosticUnsettledDefaults);
        Check(name + ": every exposed scored line was settled inside the reserve", unsettled == 0, unsettled.ToString());
        Check(name + ": the leader and the fourth finalist are both exposed and both settled", name != "wide" || exposed.Count >= 4 && exposed.Take(4).All(job.DiagnosticSecretDelta.ContainsKey),
            exposed.Count + " exposed");
        Check(name + ": settlement inside its reserve", job.DiagnosticSettleNodes <= job.DiagnosticSettleReserve, job.DiagnosticSettleNodes + "/" + job.DiagnosticSettleReserve);
    }
    // No closed prior: no settlement at all (ladder and world NPCs keep the Secret-blind search).
    var open = Plan(View(Position(positions[0].hero, positions[0].secret, positions[0].setup), null), true);
    Check("open pool (no prior): no settlement reserve, nothing settled", open.DiagnosticSettleReserve == 0 && open.DiagnosticSettledLines == 0,
        open.DiagnosticSettleReserve + "/" + open.DiagnosticSettledLines);
    var openOff = Plan(View(Position(positions[0].hero, positions[0].secret, positions[0].setup), null), false);
    Check("open pool (no prior): the secret-worlds fix changes nothing", Line(open.Result.Line) == Line(openOff.Result.Line) && open.Result.Value == openOff.Result.Value
        && open.Result.Nodes == openOff.Result.Nodes, Line(open.Result.Line) + " vs " + Line(openOff.Result.Line));
});
Case("Determinism", () =>
{
    foreach (var pos in positions)
    {
        var g = Position(pos.hero, pos.secret, pos.setup);
        var a = Plan(View(g, PriorOf(pos.secret)), true); var b = Plan(View(g.CloneExact(), PriorOf(pos.secret)), true);
        Check(pos.name + ": same line twice", Line(a.Result.Line) == Line(b.Result.Line), Line(a.Result.Line) + " vs " + Line(b.Result.Line));
        Check(pos.name + ": same value twice", a.Result.Value == b.Result.Value && a.Result.Nodes == b.Result.Nodes, a.Result.Value + " vs " + b.Result.Value);
        var picks = new List<string>();
        foreach (bool parallel in new[] { false, true, false })
        {
            GoldenPilot.ParallelLethalEnabled = parallel;
            var pilot = Pilot(PriorOf(pos.secret));
            var view = pilot.Options.FairViewFactory(g, g.Player2.PlayerId, 555, style);
            var d = pilot.Decide(view, new GoldenContext(g.Turn, 1, 555, pilot.Profile));
            picks.Add(d.Action + "=" + d.Value.ToString("R"));
        }
        GoldenPilot.ParallelLethalEnabled = false;
        Check(pos.name + ": decision independent of the parallel lethal search", picks.Distinct().Count() == 1, string.Join(" | ", picks));
    }
});
Case("Budget", () =>
{
    foreach (var pos in positions)
    {
        var g = Position(pos.hero, pos.secret, pos.setup);
        var job = Plan(View(g, PriorOf(pos.secret)), true);
        var twin = Plan(View(Position(pos.hero, pos.secret, pos.setup, false), null), true);
        Check(pos.name + ": settlement inside its reserve", job.DiagnosticSettleNodes <= job.DiagnosticSettleReserve, job.DiagnosticSettleNodes + "/" + job.DiagnosticSettleReserve);
        Check(pos.name + ": decision inside the cap", job.Result.Nodes <= profile.Budget.PlanNodes, job.Result.Nodes + "/" + profile.Budget.PlanNodes);
        Check(pos.name + ": root nodes within 1.5x the Secret-free twin", job.Result.Nodes <= 1.5 * Math.Max(1, twin.Result.Nodes), job.Result.Nodes + " vs " + twin.Result.Nodes);
        Note(pos.name + ": nodes " + job.Result.Nodes + " (twin " + twin.Result.Nodes + "), settlement " + job.DiagnosticSettleNodes + "/" + job.DiagnosticSettleReserve + " over " + job.DiagnosticSettledLines + " lines");
    }
    // A wide position: two Hunter Secrets from a five-Secret pool (20 worlds, W = 8), a full hand and board.
    Game Wide(bool secrets)
    {
        var g = NewRun("HERO_05");
        Board(g.Player1, Croc); Board(g.Player1, Wisp);
        if (secrets) { PlayerSecret(g, "EX1_610"); PlayerSecret(g, "EX1_611"); }
        EndTurn(g);
        var o = g.Player2; ClearHand(o); Mana(o, 10);
        foreach (var id in new[] { Raptor, Raptor, Yeti, Moonfire, Wisp, Croc }) Hand(o, id);
        Board(o, Raptor, true); Board(o, Yeti, true); Board(o, Ogre, true);
        return g;
    }
    var wide = Plan(View(Wide(true), PriorOf(pools[0].ids)), true);
    var wideOff = Plan(View(Wide(true), PriorOf(pools[0].ids)), false);
    var wideTwin = Plan(View(Wide(false), null), true);
    Note("wide: nodes " + wide.Result.Nodes + " (fix off " + wideOff.Result.Nodes + ", twin " + wideTwin.Result.Nodes + "), settlement " + wide.DiagnosticSettleNodes + "/" + wide.DiagnosticSettleReserve
        + " over " + wide.DiagnosticSettledLines + " lines, worlds " + wide.DiagnosticSettleWorlds + "; line " + Line(wide.Result.Line));
    Check("wide: settlement inside its reserve", wide.DiagnosticSettleNodes <= wide.DiagnosticSettleReserve, wide.DiagnosticSettleNodes + "/" + wide.DiagnosticSettleReserve);
    Check("wide: decision inside the cap", wide.Result.Nodes <= profile.Budget.PlanNodes, wide.Result.Nodes.ToString());
    Check("wide: root nodes within 1.5x the Secret-free twin", wide.Result.Nodes <= 1.5 * Math.Max(1, wideTwin.Result.Nodes), wide.Result.Nodes + " vs " + wideTwin.Result.Nodes);
    Check("wide: eight worlds settled", wide.DiagnosticSettleWorlds == 8, wide.DiagnosticSettleWorlds.ToString());
    var again = Plan(View(Wide(true), PriorOf(pools[0].ids)), true);
    Check("wide: deterministic", Line(again.Result.Line) == Line(wide.Result.Line) && again.Result.Value == wide.Result.Value, "");
});

// ----------------------------------------------------------------------------------------------- 8 RewindWithSecret
Case("RewindWithSecret", () =>
{
    foreach (bool manualRewind in new[] { true, false })
    {
        string tag = manualRewind ? "rewind answer" : "pilot answer";
        var g = NewRun("HERO_08");
        PlayerSecret(g, "EX1_295"); PlayerSecret(g, "EX1_287");
        EndTurn(g);
        ClearHand(g.Player2); Mana(g.Player2, 10); Hand(g.Player2, "TIME_004");
        var pilot = Pilot(PriorOf("EX1_295", "EX1_287", "EX1_294"));
        int index = 0, actions = 0, offers = 0, views = 0; bool rewound = false;
        while (g.State != State.COMPLETE && g.CurrentPlayer == g.Player2 && actions++ < 40)
        {
            if (g.Player2.Choice?.ChoiceAction == ChoiceAction.REWIND) {
                offers++;
                if (manualRewind && !rewound) {
                    var decision = g.GetPendingDecisions().Single(d => d.ControllerId == g.Player2.Id);
                    var rewindId = g.Player2.Choice.Choices.First(id => g.IdEntityDic[id].Card.Id == "TIME_000tb");
                    Check(tag + ": engine takes the Rewind answer", g.TryAnswer(new SabberStoneCore.Model.Decisions.DecisionAnswer(decision.Id, decision.ControllerId, new[] { rewindId }), out var why), why ?? "");
                    rewound = true;
                    continue;
                }
            }
            // Controllers are rebuilt by a Rewind: read them from the game every time.
            var view = pilot.Options.FairViewFactory(g, g.Player2.PlayerId, 900 + actions, style); views++;
            int live = g.Player1.SecretZone.Count(c => c.IsSecret);
            Check(tag + ": slot count matches the live Secrets", view.OpponentSecretSlots.Count == live && view.Facts.OpponentSecretCount == live, view.OpponentSecretSlots.Count + " vs " + live);
            Check(tag + ": slots are Unknown and unused", view.OpponentSecretSlots.All(id => view.FingerprintGame.IdEntityDic.TryGetValue(id, out var e) && e is Unknown)
                && view.OpponentSecretSlots.Distinct().Count() == view.OpponentSecretSlots.Count, string.Join(",", view.OpponentSecretSlots));
            var (task, trace) = BotDriver.Decide(g, g.Player2.PlayerId, style, st => pilot.DecidePilot(st, new GoldenContext(g.Turn, index, unchecked(997 * g.Turn + index * 31), pilot.Profile)), ref index, pilot.Options);
            Check(tag + ": no fallback", trace?.Kind != "fallback", trace?.FallbackReason ?? "");
            if (task is ChooseTask pick) {
                var decision = g.GetPendingDecisions().Single(d => d.ControllerId == task.Controller.Id);
                Check(tag + ": choice accepted", g.TryAnswer(new SabberStoneCore.Model.Decisions.DecisionAnswer(decision.Id, decision.ControllerId, pick.Choices.ToArray()), out var why), why ?? "");
            }
            else Check(tag + ": task accepted", g.Process(task), task.ToString());
        }
        Note(tag + ": rewind offers " + offers + ", views " + views + ", rewound " + rewound);
        Check(tag + ": a Rewind was offered", offers > 0, "");
    }
});

// ----------------------------------------------------------------------------------------------- 9 BossOwnSecrets
Case("BossOwnSecrets", () =>
{
    var all = new DungeonRunProfile(); all.Unlocked.AddRange(data.Unlocks.Keys);
    var hunterDeck = new List<string> { "EX1_611", "EX1_611", "EX1_610", "EX1_610", "EX1_554", "EX1_554", "EX1_533", "EX1_533", "EX1_609", "EX1_609" };
    hunterDeck.AddRange(Fill(5, Raptor)); hunterDeck.AddRange(Fill(5, Croc)); hunterDeck.AddRange(Fill(6, Yeti)); hunterDeck.AddRange(Fill(4, Ogre));
    var paladinDeck = new List<string> { "EX1_130", "EX1_130", "EX1_136", "EX1_136", "EX1_132", "EX1_132", "EX1_379", "EX1_379", "FP1_020", "FP1_020" };
    paladinDeck.AddRange(Fill(5, Raptor)); paladinDeck.AddRange(Fill(5, Croc)); paladinDeck.AddRange(Fill(6, Yeti)); paladinDeck.AddRange(Fill(4, Ogre));
    int games = 0, faults = 0, fallbacks = 0, secretGames = 0, bossWins = 0;
    for (int k = 0; k < 20; k++)
    {
        bool hunter = k < 10;
        var cls = data.Classes[k % data.Classes.Count];
        var s = run.NewRun(all, cls.Id, cls.Decks[0], cls.Powers[0], 5000 + k, signatureId: run.SignatureOffer(all, run.Data.FindClass(cls.Id), 5000 + k).FirstOrDefault());
        var scratch = new DungeonRunProfile();
        while (s.Floor < 1 + k % 7)
        {
            if (s.Phase == DungeonRunPhase.PreMatch) { run.BeginMatch(s); run.RecordOutcome(s, scratch, DungeonRunOutcome.Win); }
            else if (s.Phase == DungeonRunPhase.TreasureDraft) run.PickTreasure(s, s.OpenDraft.Offered[0]);
            else run.PickBundle(s, s.OpenDraft.Offered[0]);
        }
        string result; int turns = 0, decisions = 0, fb = 0; bool secrets = false;
        try
        {
            var plan = run.BuildPlan(s);
            plan.BossHero = Cards.FromId(hunter ? "HERO_05" : "HERO_04"); plan.BossHeroPowerId = null;
            plan.BossDeck = Ids(hunter ? hunterDeck : paladinDeck); plan.BossSideboards = null;
            var bp = DungeonRunBossPilot.Build(run.CurrentBoss(s), plan, data);
            var prof = bp.Profile.WithBudget(testBudget);
            var golden = new GoldenPilot(prof, bp.Plan, null, bp.SecretPrior);
            var cfg = new GameConfig { RandomSeed = run.MatchSeed(s), Shuffle = true, SkipMulligan = false, History = false, Logging = false, FormatType = FormatType.FT_WILD };
            DungeonRunSetup.Configure(cfg, plan);
            var game = new Game(cfg); game.StartGame(); DungeonRunSetup.ApplyAfterStart(game, plan);
            game.Process(ChooseTask.Mulligan(game.Player1, new List<int>()));
            var choice = game.Player2.Choice;
            var mv = new MulliganView(choice.Choices.Select(id => new MulliganCard(id, game.IdEntityDic[id].Card.Id, game.IdEntityDic[id].Card.Cost)), false, bp.Deck.CardIds, game.Player1.Hero.Card.Class);
            game.Process(ChooseTask.Mulligan(game.Player2, BotDriver.Mulligan(mv, new DeckPlanMulligan(bp.Plan)).ToList()));
            game.MainBegin();
            int index = 0, acts = 0;
            while (game.State != State.COMPLETE && game.Turn <= 100)
            {
                if (++acts > 1500) throw new Exception("stuck after 1500 actions");
                PlayerTask task; string who;
                if (game.CurrentPlayer == game.Player2)
                {
                    var (t, trace) = BotDriver.Decide(game, game.Player2.PlayerId, bp.Deck.Style,
                        st => golden.DecidePilot(st, new GoldenContext(game.Turn, index, unchecked(997 * game.Turn + index * 31), prof)), ref index, golden.Options);
                    decisions++; if (trace?.Kind == "fallback") { fb++; Console.Error.WriteLine("  FALLBACK t" + game.Turn + ": " + trace.FallbackReason); }
                    task = t; who = "boss";
                }
                else { task = LocalBot.Choose(game.Player1.Options()); who = "player"; }
                if (task is ChooseTask pick && game.Step != Step.BEGIN_MULLIGAN)
                {
                    var decision = game.GetPendingDecisions().Single(d => d.ControllerId == task.Controller.Id);
                    if (!game.TryAnswer(new SabberStoneCore.Model.Decisions.DecisionAnswer(decision.Id, decision.ControllerId, pick.Choices.ToArray()), out var why))
                        throw new Exception(who + " choice rejected: " + why);
                }
                else if (!game.Process(task)) throw new Exception(who + " task rejected: " + task);
                secrets |= game.Player2.SecretZone.Any(c => c.IsSecret);
            }
            turns = game.Turn;
            result = game.State != State.COMPLETE ? "turn-cap" : game.Player2.PlayState == PlayState.WON ? "boss" : game.Player1.PlayState == PlayState.WON ? "player" : "tie";
            if (result == "boss") bossWins++;
        }
        catch (Exception e) { result = "FAULT " + e.GetType().Name + ": " + e.Message.Split('\n')[0]; faults++; }
        games++; fallbacks += fb; if (secrets) secretGames++;
        Console.WriteLine($"  {(hunter ? "hunter" : "paladin"),-8} vs {cls.Id,-12} floor {s.Floor} -> {result,-8} turns {turns,3} decisions {decisions,3} fallbacks {fb} secrets {secrets}");
    }
    Note($"games {games}, boss wins {bossWins}, faults {faults}, fallbacks {fallbacks}, games with a boss Secret in play {secretGames}");
    Check("0 faults", faults == 0, faults.ToString());
    Check("0 fallbacks", fallbacks == 0, fallbacks.ToString());
    Check("boss Secrets reached play", secretGames > 0, secretGames.ToString());
});

// ----------------------------------------------------------------------------------------------- 10 CatalogPlayerSecrets
// Phase 3 I2: a catalog run's player may generate Secrets. The boss's prior is the widened catalog prior, and every Secret the
// player holds (drafted or generated in its class) is inside it, so the search never meets a Secret it gave no weight.
Case("CatalogPlayerSecrets", () =>
{
    var cdata = DungeonRunData.Parse(File.ReadAllText(Path.Combine(root, "dungeon-run", "run-v1.json")), File.ReadAllText(Path.Combine(root, "dungeon-run", "pool-v1.json")),
        File.ReadAllText(Path.Combine(root, "npc", "decks-v1.json")), File.ReadAllText(Path.Combine(root, "dungeon-run", "catalog-v1.json")));
    var crun = new DungeonRun(cdata);
    var all = new DungeonRunProfile(); all.Unlocked.AddRange(cdata.Unlocks.Keys);
    // Secret generators per class (the lifted secret-generated draft cards and Discover/random-spell cards).
    var generators = new Dictionary<string, string[]> {
        ["MAGE"] = new[] { "GIL_116", "DAL_608", "LOE_003" }, ["HUNTER"] = new[] { "BOT_402", "ICC_204", "BOT_402" },
        ["PALADIN"] = new[] { "DAL_141", "UNG_011", "UNG_011" }, ["MAGE_ROGUE"] = new[] { "GIL_116", "DAL_608", "LOE_003" },
        // Phase 3 review: Professor Putricide (a Hunter card, granted to Rogue by Tess Greymane) casts Hunter Secrets in a Rogue run.
        ["ROGUE"] = new[] { "ICC_204", "LOOT_204", "ICC_204" } };
    int games = 0, faults = 0, fallbacks = 0, generatedSeen = 0, outside = 0, offClass = 0, generatorClassSeen = 0;
    var outsideIds = new SortedSet<string>(StringComparer.Ordinal);
    int k = 0;
    foreach (var pair in generators)
        for (int g = 0; g < 3; g++, k++)
        {
            var cls = cdata.Class(pair.Key);
            var s = crun.NewRun(all, cls.Id, cls.Decks[0], cls.Powers[0], 7300 + k, signatureId: crun.SignatureOffer(all, crun.Data.FindClass(cls.Id), 7300 + k).FirstOrDefault());
            var scratch = new DungeonRunProfile();
            while (s.Floor < 2 + k % 5)
            {
                if (s.Phase == DungeonRunPhase.PreMatch) { crun.BeginMatch(s); crun.RecordOutcome(s, scratch, DungeonRunOutcome.Win); }
                else if (s.Phase == DungeonRunPhase.TreasureDraft) crun.PickTreasure(s, s.OpenDraft.Offered[0]);
                else crun.PickBundle(s, s.OpenDraft.Offered[0]);
            }
            for (int c = 0; c < 4; c++) s.Deck.AddRange(pair.Value);
            string result; int fb = 0;
            try
            {
                var plan = crun.BuildPlan(s);
                var bp = DungeonRunBossPilot.Build(crun.CurrentBoss(s), plan, cdata, runClasses: crun.RunClasses(s));
                var expected = DungeonRunAdmission.CatalogSecretPrior(cdata, cls.ClassSet);
                var genClasses = DungeonRunAdmission.SecretGeneratorClasses(cdata, cls.ClassSet, m => true);
                Check($"{cls.Id} k{k}: the pilot's prior is the catalog run's widened prior", plan.PlayerGeneratedSecrets && bp.SecretPrior != null && expected != null
                    && bp.SecretPrior.OrderBy(p => p.Key).SequenceEqual(expected.OrderBy(p => p.Key)), "");
                var prof = bp.Profile.WithBudget(testBudget);
                var golden = new GoldenPilot(prof, bp.Plan, null, bp.SecretPrior);
                var cfg = new GameConfig { RandomSeed = crun.MatchSeed(s), Shuffle = true, SkipMulligan = false, History = true, Logging = false, FormatType = FormatType.FT_WILD };
                DungeonRunSetup.Configure(cfg, plan);
                var game = new Game(cfg); game.StartGame(); DungeonRunSetup.ApplyAfterStart(game, plan);
                var startDeck = new HashSet<string>(s.Deck, StringComparer.Ordinal);
                game.Process(ChooseTask.Mulligan(game.Player1, new List<int>()));
                var choice = game.Player2.Choice;
                var mv = new MulliganView(choice.Choices.Select(id => new MulliganCard(id, game.IdEntityDic[id].Card.Id, game.IdEntityDic[id].Card.Cost)), false, bp.Deck.CardIds, game.Player1.Hero.Card.Class);
                game.Process(ChooseTask.Mulligan(game.Player2, BotDriver.Mulligan(mv, new DeckPlanMulligan(bp.Plan)).ToList()));
                game.MainBegin();
                int index = 0, acts = 0;
                var seen = new HashSet<string>(StringComparer.Ordinal);
                while (game.State != State.COMPLETE && game.Turn <= 60)
                {
                    if (++acts > 1500) throw new Exception("stuck after 1500 actions");
                    PlayerTask task; string who;
                    if (game.CurrentPlayer == game.Player2)
                    {
                        var (t, trace) = BotDriver.Decide(game, game.Player2.PlayerId, bp.Deck.Style,
                            st => golden.DecidePilot(st, new GoldenContext(game.Turn, index, unchecked(997 * game.Turn + index * 31), prof)), ref index, golden.Options);
                        if (trace?.Kind == "fallback") { fb++; Console.Error.WriteLine("  FALLBACK t" + game.Turn + ": " + trace.FallbackReason); }
                        task = t; who = "boss";
                    }
                    else { task = LocalBot.Choose(game.Player1.Options()); who = "player"; }
                    if (task is ChooseTask pick && game.Step != Step.BEGIN_MULLIGAN)
                    {
                        var decision = game.GetPendingDecisions().Single(d => d.ControllerId == task.Controller.Id);
                        if (!game.TryAnswer(new SabberStoneCore.Model.Decisions.DecisionAnswer(decision.Id, decision.ControllerId, pick.Choices.ToArray()), out var why))
                            throw new Exception(who + " choice rejected: " + why);
                    }
                    else if (!game.Process(task)) throw new Exception(who + " task rejected: " + task);
                    // Every Secret the player holds in hand or play (drafted or generated).
                    foreach (var e in game.Player1.HandZone.Concat<IPlayable>(game.Player1.SecretZone).Where(e => e.Card.IsSecret))
                    {
                        if (!seen.Add(e.Card.Id + "#" + e.Id)) continue;
                        if (!startDeck.Contains(e.Card.Id)) generatedSeen++;
                        bool inClass = cls.ClassSet.Any(c => SabberStoneCore.Actions.CardClassRules.IsAllowedInDeck(e.Card, Enum.Parse<CardClass>(c)));
                        // A Secret of a Secret generator's own class (SecretGeneratorClasses) must be in the prior too.
                        bool generatorClass = !inClass && genClasses.Any(c => SabberStoneCore.Actions.CardClassRules.IsAllowedInDeck(e.Card, Enum.Parse<CardClass>(c)));
                        if (generatorClass && !startDeck.Contains(e.Card.Id)) generatorClassSeen++;
                        if (!inClass && !generatorClass) { offClass++; continue; }
                        if (!bp.SecretPrior.ContainsKey(e.Card.Id)) { outside++; outsideIds.Add(cls.Id + ":" + e.Card.Id); }
                    }
                }
                result = game.State != State.COMPLETE ? "turn-cap" : game.Player2.PlayState == PlayState.WON ? "boss" : game.Player1.PlayState == PlayState.WON ? "player" : "tie";
            }
            catch (Exception e) { result = "FAULT " + e.GetType().Name + ": " + e.Message.Split('\n')[0]; faults++; }
            games++; fallbacks += fb;
            Console.WriteLine($"  catalog {cls.Id,-11} floor {s.Floor} -> {result,-8} fallbacks {fb}");
        }
    Note($"catalog games {games}, faults {faults}, fallbacks {fallbacks}, generated player Secrets {generatedSeen}, off-class {offClass}, outside the prior {outside}");
    Check("catalog runs: 0 faults", faults == 0, faults.ToString());
    Check("catalog runs: 0 fallbacks", fallbacks == 0, fallbacks.ToString());
    Check("catalog runs: the player generated Secrets (the lift is live)", generatedSeen > 0, generatedSeen.ToString());
    Check("catalog runs: every in-class or generator-class Secret the player held is inside the boss's prior", outside == 0, string.Join(",", outsideIds));
    Check("catalog runs: a Rogue run's Professor Putricide put Hunter Secrets into play (generator-class Secrets were met)", generatorClassSeen > 0, generatorClassSeen.ToString());
});

// ----------------------------------------------------------------------------------------------- 11 ShiftingScrollHiddenHand
// Phase 3 review: a Shifting Scroll (LOOT_104) in the opponent's hidden hand carries a LOOT_104e trigger after it transforms.
// The fair view drops the hidden actor; its registered triggers must go with it, so cloning the view (every search step)
// never meets a trigger whose owner the view does not hold (Game.RestoreCloneSubscriptions: KeyNotFoundException).
Case("ShiftingScrollHiddenHand", () =>
{
    int views = 0, applied = 0, detached = 0, errors = 0; var why = new List<string>();
    for (int seed = 1; seed <= 6; seed++)
    {
        var g = NewRun(seed: 4100 + seed);
        for (int i = 0; i < 1 + seed % 3; i++) Hand(g.Player1, "LOOT_104");
        Hand(g.Player1, Yeti);
        // Two of the player's turn starts: the scrolls transform and carry LOOT_104e; then the boss's turn.
        for (int t = 0; t < 4; t++) EndTurn(g);
        EndTurn(g);
        if (g.CurrentPlayer != g.Player2) EndTurn(g);
        Board(g.Player2, Yeti, ready: true);
        Mana(g.Player2, 10);
        var view = View(g, PriorOf("EX1_287"), 777 + seed);
        views++;
        var vg = view.FingerprintGame;
        detached += vg.Triggers.Count(tr => !tr.HasLiveSource(vg));
        foreach (var action in view.Actions)
        {
            try { var next = view.Apply(action); applied++; var ng = next.FingerprintGame; detached += ng.Triggers.Count(tr => !tr.HasLiveSource(ng)); }
            catch (Exception e) { errors++; if (why.Count < 3) why.Add(action + ": " + (e.InnerException ?? e).GetType().Name + " " + (e.InnerException ?? e).Message.Split('\n')[0]); }
        }
    }
    Note($"views {views}, actions applied {applied}, detached triggers {detached}, errors {errors}");
    Check("a view of a hand holding transformed Shifting Scrolls keeps no detached trigger", detached == 0, detached.ToString());
    Check("every action of such a view applies (the view clones)", errors == 0 && applied > 0, string.Join(" | ", why));
});

// ----------------------------------------------------------------------------------------------- 12 KalecgosHiddenHand
// Step 9 test phase (pairs9 chunk 23 case 1722, dala_07_belnaara vs MAGE): the player's Kalecgos (DAL_609, a SwitchingAura on
// its own hand) applies to the hidden hand when the boss's turn starts. The fair view retires those ids and reissues the hand
// slots under others (CanonicalizePrivateActors); the aura's applied set must lose the retired ids, or the view's end of turn
// (TurnOff -> SwitchingAura.RemoveInternal) looks one up (KeyNotFoundException) or, once reissued, strips another card.
Case("KalecgosHiddenHand", () =>
{
    int views = 0, live = 0, applied = 0, missing = 0, aliased = 0, errors = 0; var why = new List<string>();
    // Kalecgos's aura covers its owner's hand only: an applied id must be a card there, never a retired id or a reissued one.
    void Audit(Game vg)
    {
        foreach (var a in vg.Auras.OfType<SabberStoneCore.Auras.Aura>().Where(a => a.Owner.Card.Id == "DAL_609"))
            for (int id = 0; id < vg.IdEntityDic.Capacity; id++)
            {
                if (!a.AppliesTo(id)) continue;
                if (!vg.IdEntityDic.ContainsKey(id)) missing++;
                else if (vg.IdEntityDic[id].Zone != a.Owner.Controller.HandZone) aliased++;
            }
    }
    for (int seed = 1; seed <= 6; seed++)
    {
        var g = NewRun(seed: 4200 + seed);
        Board(g.Player1, "DAL_609");
        // Odd seeds: the boss's minion first, so the hand holds the highest ids; even seeds: a deck card after the hand, so
        // the view's reissued ids shift past the hand's. Either layout has retired the hand's ids in the field.
        if (seed % 2 == 1) Board(g.Player2, Yeti, ready: true);
        var held = Enumerable.Range(0, 1 + seed % 3).Select(_ => Hand(g.Player1, "CS2_029")).ToList();
        if (seed % 2 == 0) DeckTop(g.Player1, Yeti);
        EndTurn(g);
        if (g.CurrentPlayer != g.Player2) EndTurn(g);
        var aura = g.Auras.OfType<SabberStoneCore.Auras.Aura>().Single(a => a.Owner.Card.Id == "DAL_609");
        if (held.All(c => aura.AppliesTo(c.Id))) live++;
        if (seed % 2 == 0) Board(g.Player2, Yeti, ready: true);
        Mana(g.Player2, 10);
        var view = View(g, PriorOf("EX1_287"), 777 + seed);
        views++;
        Audit(view.FingerprintGame);
        foreach (var action in view.Actions)
        {
            try { var next = view.Apply(action); applied++; Audit(next.FingerprintGame); }
            catch (Exception e) { errors++; if (why.Count < 3) why.Add(action + ": " + (e.InnerException ?? e).GetType().Name + " " + (e.InnerException ?? e).Message.Split('\n')[0]); }
        }
    }
    Note($"views {views}, live auras {live}, actions applied {applied}, missing ids {missing}, aliased ids {aliased}, errors {errors}");
    Check("the player's Kalecgos applies to every hidden hand card on the boss's turn (the case is live)", live == views, $"{live}/{views}");
    Check("a view of that hand keeps the aura on no retired or reissued id", missing == 0 && aliased == 0, $"missing {missing}, aliased {aliased}");
    Check("every action of such a view applies, its end of turn included", errors == 0 && applied > 0, string.Join(" | ", why));
});

Console.WriteLine($"SUMMARY checks passed {passes} failed {failures}");
foreach (var line in failLines) Console.WriteLine("FAILED " + line);
return failures == 0 ? 0 : 1;
