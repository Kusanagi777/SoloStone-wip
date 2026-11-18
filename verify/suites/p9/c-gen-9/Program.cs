using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using SabberStoneCore.Actions;
using SabberStoneCore.CardSets.DungeonRun;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;

// c-gen-9: step 9 lane G, generation hygiene (handoff/phase9/plan.md D15 and lane G's tests), on the shipped data (encounters
// loaded), real engine games through the run setup path (DungeonRun.BuildPlan -> DungeonRunSetup.Configure / ApplyAfterStart).
//   U  the unscripted set: the test's own definition (a card of a generation pool with no engine script, keyword-only cards and
//      the fixed-choice mechanism cards excepted) against DungeonRunAdmission.UnscriptedGenerated and the run's generation
//      exclusions (GeneratedRejects, both kinds of run)
//   P  every Dungeon Run random pool enumerated in run matches (legacy and catalog run, the run player and the boss
//      generating): the frozen legacy pools, the Cards.cs pools built on them, the Death Knight and Demon Hunter pools, and the
//      run's own encounter and treasure pools (found by reflection: every *Pool, *Candidates and *Parts method): 0 unscripted ids; an unfiltered game gets every legacy pool
//      back as the same list (selfplay and randplay draw from the same arrays)
//   C  every routed call site (AddLackeyTask, DiscoverTask x5, ReplaceTask, AdaptTask, Kazakus, Un'Goro pack, both
//      Build-A-Beasts, F16 Import "Pet", past Legendaries, RandomMinion, RandomMinionNumber, TransformRandom): in a run match
//      whose exclusions leave a few cards of the pool, every result is one of them (the five Discover types are also filtered by
//      DiscoverTask's own run filter, so only both filters off make those five fail)
//   L  the Lackeys: the pool has 7 cards, all scripted; Titanic Lackey (ULD_616) and Draconic Lackey (DRG_052) do what their text says
//   K  Kingsbane (LOOT_542): the shuffled copy keeps the Durability its enchantments added, not the Durability it lost
//   G  200 seeded run games (LocalBot both sides; v4 defaults, every loadout, encounter bosses): 0 unscripted cards generated
// Typhoon's InterceptLeave is not re-tested here: lane G changed nothing (it is there since a95b97e; c-enc-engine E7-E10).
// C_GEN_9_ONLY=U,P,... runs only those sections; C_GEN_9_GAMES=n plays n games in G (default 200).
int failures = 0, passes = 0;
void Check(string name, bool ok, string detail = "")
{
    if (ok) passes++; else failures++;
    Console.WriteLine((ok ? "PASS " : "FAIL ") + name + (detail.Length > 0 ? "  (" + detail + ")" : ""));
}
void Note(string text) => Console.WriteLine("NOTE " + text);
var only = Environment.GetEnvironmentVariable("C_GEN_9_ONLY") is string o && o.Length > 0 ? o.Split(',') : null;
void Guard(string name, Action body)
{
    if (only != null && !only.Contains(name)) return;
    try { body(); }
    catch (Exception e) { failures++; Console.WriteLine("FAIL " + name + " threw " + e.GetType().Name + ": " + e.Message.Split('\n')[0] + " @ " + (e.StackTrace ?? "").Split('\n').FirstOrDefault()?.Trim()); }
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
Card C(string id) => Cards.FromId(id);
string Hash(IEnumerable<string> ids) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(string.Join("\n", ids)))).Substring(0, 16);
string Few(IEnumerable<string> ids, int n = 12) { var l = ids.ToList(); return l.Count + (l.Count == 0 ? "" : ": " + string.Join(",", l.Take(n)) + (l.Count > n ? ",..." : "")); }
var facts = System.Text.Json.JsonDocument.Parse(File.ReadAllText(VerifyPaths.Repo("facts/violet-hold-2026-09.json")));
var factText = new Dictionary<string, string>(StringComparer.Ordinal);
foreach (var c in facts.RootElement.GetProperty("cards").EnumerateArray())
    if (c.TryGetProperty("text", out var t) && t.ValueKind == System.Text.Json.JsonValueKind.String) factText[c.GetProperty("id").GetString()] = t.GetString();
string root = VerifyPaths.Data;
string R(string f) => File.ReadAllText(Path.Combine(root, f));
var data = DungeonRunData.Parse(R("dungeon-run/run-v1.json"), R("dungeon-run/pool-v1.json"), R("npc/decks-v1.json"), R("dungeon-run/catalog-v1.json"),
    R("dungeon-run/schedule-v1.json"), R("dungeon-run/tavern-v1.json"), R("dungeon-run/encounters-v1.json"));
var run = new DungeonRun(data);
var all = new DungeonRunProfile(); all.Unlocked.AddRange(data.Unlocks.Keys);

const string Yeti = "CS2_182", Wisp = "CS2_231", Croc = "CS2_120";
string[] Thirty(string id) => Enumerable.Repeat(id, 30).ToArray();

// ------------------------------------------------------------------ the test's own unscripted definition
// A card generation may yield that has no engine script (Card.Implemented off), less the cards that work without one: the
// fixed-choice mechanism cards (Kazakus's potion bases, written by KazakusPower; Kobold Hermit's four basic Totems, the whole
// choice) and cards whose whole text is keywords their own tags carry (Murmy's "Reborn"). The mechanism list is frozen here,
// independent of DungeonRunAdmission.GenerationMechanismIds; the keyword exemption is the admission's PlaysFromTags, whose
// result set is pinned below (U4), so a change to the heuristic shows.
var mechanismIds = new HashSet<string>(StringComparer.Ordinal) { "AT_132_SHAMANa", "AT_132_SHAMANb", "AT_132_SHAMANc", "AT_132_SHAMANd", "CFM_621t", "CFM_621t14", "CFM_621t15" };
bool Unscripted(Card card) => card != null && !card.Implemented && !mechanismIds.Contains(card.Id) && !DungeonRunAdmission.PlaysFromTags(card);
var unscriptedAll = new HashSet<string>(Cards.All.Where(Unscripted).Select(c => c.Id), StringComparer.Ordinal);
var legacyIds = OfflineCardPools.LegacyGeneratedIds();
var legacyNames = OfflineCardPools.LegacyPoolNames();
var legacySet = new HashSet<string>(legacyIds, StringComparer.Ordinal);

// ------------------------------------------------------------------ run games
// A run on the given schedule at its first pre-match (modifiers declined or the first taken), with the boss under test on that floor.
DungeonRunState At(DungeonRunClass c, string deck, long seed, string bossId = null, string schedule = null)
{
    var s = run.NewRun(all, c.Id, deck, c.Powers[0], seed, schedule: schedule, signatureId: run.SignatureOffer(all, c, seed).FirstOrDefault());
    int guard = 0;
    while (s.Phase != DungeonRunPhase.PreMatch)
    {
        if (++guard > 20 || s.Phase != DungeonRunPhase.Modifier) throw new Exception("walk stopped at " + s.Phase);
        run.PickModifier(s, s.ModifierOffer.Kind == DungeonRunGlobalRule.KindAnomaly ? DungeonRunGlobalRule.None : s.ModifierOffer.Offered[0]);
    }
    if (bossId != null) s.BossSequence[s.Floor - 1] = bossId;
    return s;
}
RunMatchPlan BasePlan(bool catalog, IReadOnlyCollection<string> excluded = null)
{
    var plan = run.BuildPlan(At(data.Class("MAGE"), data.Class("MAGE").Decks[0], 4400, schedule: "acts-v3h"), all);
    plan.ExcludedGenerated = excluded ?? DungeonRunAdmission.GeneratedRejects(catalog);
    plan.ExcludedGeneratedBoss = excluded != null ? new string[0] : DungeonRunAdmission.GeneratedRejectsBoss(catalog);
    plan.PlayerGeneratedSecrets = catalog;
    plan.BossDeck = Thirty(Yeti).Select(C).ToList();
    plan.PlayerDeck = Thirty(Yeti).Select(C).ToList();
    plan.PlayerDeckSlots = null;
    return plan;
}
// History on: the engine then keeps every enchantment as an entity (with history off a tag-only enchantment is applied to the tags).
Game Ready(RunMatchPlan plan, int seed = 7, FormatType format = FormatType.FT_WILD, bool history = false)
{
    var cfg = new GameConfig { RandomSeed = seed, Shuffle = true, SkipMulligan = false, History = history, Logging = false, FormatType = format };
    if (plan != null) DungeonRunSetup.Configure(cfg, plan);
    else { cfg.Player1HeroClass = CardClass.MAGE; cfg.Player2HeroClass = CardClass.WARRIOR; cfg.Player1Deck = Thirty(Yeti).Select(C).ToList(); cfg.Player2Deck = Thirty(Yeti).Select(C).ToList(); }
    var g = new Game(cfg);
    g.StartGame();
    if (plan != null) DungeonRunSetup.ApplyAfterStart(g, plan);
    g.Process(ChooseTask.Mulligan(g.Player1, new List<int>()));
    g.Process(ChooseTask.Mulligan(g.Player2, new List<int>()));
    g.MainBegin();
    return g;
}
void Mana(Controller c, int n = 10) { c.BaseMana = n; c.UsedMana = 0; c.OverloadLocked = 0; c.OverloadOwed = 0; }
void EndTurn(Game g) => g.Process(EndTurnTask.Any(g.CurrentPlayer));
void ToSide(Game g, Controller c) { if (g.CurrentPlayer != c) EndTurn(g); Mana(c); }
IPlayable ToHand(Game g, Controller c, string id)
{
    while (c.HandZone.Count >= 9) { var old = c.HandZone[0]; c.HandZone.Remove(old); c.SetasideZone.Add(old); }
    var e = Entity.FromCard(c, C(id)); Generic.AddHandPhase.Invoke(c, e); g.DeathProcessingAndAuraUpdate(); return e;
}
void EmptyHand(Controller c) { foreach (var p in c.HandZone.ToArray()) { c.HandZone.Remove(p); c.SetasideZone.Add(p); } }
Minion Board(Game g, Controller c, string id) { var m = (Minion)Entity.FromCard(c, C(id)); Generic.SummonBlock(g, m, -1, c.Hero); g.DeathProcessingAndAuraUpdate(); return m; }
void ClearBoard(Controller c) { foreach (var m in c.BoardZone.ToArray()) { c.BoardZone.Remove(m); c.SetasideZone.Add(m); } }
bool Play(Game g, IPlayable card, ICharacter target = null) { Mana(g.CurrentPlayer); return g.Process(PlayCardTask.Any(g.CurrentPlayer, card, target, -1, 0)); }
List<Card> Offer(Controller c) => c.Choice?.Choices?.Select(id => c.Game.IdEntityDic[id].Card).ToList() ?? new List<Card>();
void Pick(Game g, Controller c, int index = 0) { if (c.Choice != null) g.Process(ChooseTask.Pick(c, c.Choice.Choices[index])); }
void Drop(Controller c) => c.Choice = null;

// The run kinds the pools are read under: a legacy run (GeneratedRejects(false), the run player's Secrets off) and a catalog run.
var kinds = new[] { (Name: "legacy", Catalog: false), (Name: "catalog", Catalog: true) };

// ============================================================ U  the unscripted set
Guard("U", () =>
{
    var listed = DungeonRunAdmission.UnscriptedGenerated();
    var mine = legacyIds.Where(id => Unscripted(C(id))).OrderBy(id => id, StringComparer.Ordinal).ToList();
    Check("U1 UnscriptedGenerated is exactly the legacy pools' unscripted ids by the test's own definition (ordinal order)",
        listed.SequenceEqual(mine), "listed " + listed.Count + ", test " + mine.Count + "; only listed " + Few(listed.Except(mine)) + "; only test " + Few(mine.Except(listed)));
    Check("U2 the list holds 230 ids (the implementation record)", listed.Count == 230, listed.Count + " " + Hash(listed));
    // A pin (the implementation record's numbers): a change to the legacy pool set moves it. (LegacyGeneratedIds is distinct and
    // LegacyPoolNames leaves the Entourage pools out by construction, so only the counts can fail.)
    Check("U3 the legacy pools hold 2924 distinct ids over 88 pools (pinned)", legacyIds.Count == 2924 && legacyNames.Count == 88,
        legacyIds.Count + " ids, " + legacyNames.Count + " pools");
    var keywordOnly = legacyIds.Select(C).Where(c => c != null && !c.Implemented && !mechanismIds.Contains(c.Id) && DungeonRunAdmission.PlaysFromTags(c)).Select(c => c.Id).ToList();
    Note("keyword-only unscripted pool cards exempted (" + keywordOnly.Count + ", " + Hash(keywordOnly) + "): " + string.Join(",", keywordOnly));
    Check("U4 the keyword-only exemptions are the 6 pool cards whose whole text is keywords (DRG_061 DRG_065 DRG_239 ULD_193 ULD_450 ULD_723); each carries every keyword it names and is not a hero",
        keywordOnly.SequenceEqual(new[] { "DRG_061", "DRG_065", "DRG_239", "ULD_193", "ULD_450", "ULD_723" }) && keywordOnly.All(id => { var c = C(id); string tx = (c.Text ?? "").ToLowerInvariant();
            return c.Type != CardType.HERO && (!tx.Contains("taunt") || c[GameTag.TAUNT] > 0) && (!tx.Contains("rush") || c[GameTag.RUSH] > 0) && (!tx.Contains("reborn") || c[GameTag.REBORN] > 0)
                && (!tx.Contains("divine shield") || c[GameTag.DIVINE_SHIELD] > 0) && (!tx.Contains("charge") || c[GameTag.CHARGE] > 0) && (!tx.Contains("windfury") || c[GameTag.WINDFURY] > 0)
                && (!tx.Contains("battlecry") && !tx.Contains("deathrattle") && !tx.Contains("discover") && !tx.Contains("whenever") && !tx.Contains("after ")); }));
    Check("U5 the fixed-choice exemptions are pool cards with no script (the admission's list equals the test's frozen 7)",
        DungeonRunAdmission.GenerationMechanismIds.OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(mechanismIds.OrderBy(x => x, StringComparer.Ordinal))
        && mechanismIds.All(id => legacyIds.Contains(id) && !C(id).Implemented));
    foreach (var k in kinds)
    {
        var rejects = new HashSet<string>(DungeonRunAdmission.GeneratedRejects(k.Catalog), StringComparer.Ordinal);
        Check("U6 " + k.Name + " run: every listed unscripted id is in GeneratedRejects (" + rejects.Count + ")", listed.All(rejects.Contains), Few(listed.Where(id => !rejects.Contains(id))));
    }
    int legacyRejects = DungeonRunAdmission.GeneratedRejects(false).Count, catalogRejects = DungeonRunAdmission.GeneratedRejects(true).Count;
    Check("U7 GeneratedRejects 883 (legacy run) and 873 (catalog run), the implementation record's sizes", legacyRejects == 883 && catalogRejects == 873, legacyRejects + " / " + catalogRejects);
    Check("U8 Titanic and Draconic Lackey are scripted and not listed", C("ULD_616").Implemented && C("DRG_052").Implemented && C("ULD_616e").Implemented
        && !listed.Contains("ULD_616") && !listed.Contains("DRG_052"));
});

// ============================================================ P  every Dungeon Run random pool, enumerated in run matches
Guard("P", () =>
{
    var raw = legacyNames.Select(n => (Name: n, Pool: (IReadOnlyList<Card>)LegacyGenerationV1.Get(n))).ToList();
    int rawUnscripted = raw.SelectMany(p => p.Pool).Where(c => unscriptedAll.Contains(c.Id)).Select(c => c.Id).Distinct().Count();
    Check("P0 the raw legacy pools hold unscripted ids (what the filter takes out)", rawUnscripted >= 200, rawUnscripted + " distinct");
    // The Cards.cs pools: built from the legacy inputs (class, format and cost pools), and the live Death Knight and Demon Hunter pools.
    var derived = new List<(string Name, IReadOnlyList<Card> Pool)>();
    foreach (var f in new[] { FormatType.FT_STANDARD, FormatType.FT_WILD })
    {
        foreach (var kv in Cards.FormatTypeClassCards(f).OrderBy(kv => kv.Key)) derived.Add((f + "/class/" + kv.Key, kv.Value));
        derived.Add((f + "/all", Cards.FormatTypeCards(f).ToList()));
        foreach (var kv in Cards.CostMinionCards(f).OrderBy(kv => kv.Key)) derived.Add((f + "/cost/" + kv.Key, kv.Value));
    }
    derived.Add(("Lackeys (AddLackeyTask)", AddLackeyTask.Lackeys));
    derived.Add(("BasicTotems", Cards.BasicTotems));
    foreach (var k in kinds)
        foreach (var side in new[] { 1, 2 })
        {
            var g = Ready(BasePlan(k.Catalog));
            var c = side == 1 ? g.Player1 : g.Player2;
            ToSide(g, c);
            var leftL = raw.Select(p => (p.Name, Bad: OfflineCardPools.ForGame(g, p.Pool).Where(x => unscriptedAll.Contains(x.Id)).Select(x => x.Id).Distinct().ToList())).Where(p => p.Bad.Count > 0).ToList();
            Check("P1 " + k.Name + " run, " + (side == 1 ? "player" : "boss") + " generating: the " + raw.Count + " legacy pools through the generation filter hold 0 unscripted ids",
                leftL.Count == 0, Few(leftL.Select(p => p.Name + "[" + string.Join(" ", p.Bad.Take(4)) + "]")));
            var leftD = derived.Select(p => (p.Name, Bad: OfflineCardPools.ForGame(g, p.Pool).Where(x => unscriptedAll.Contains(x.Id)).Select(x => x.Id).Distinct().ToList())).Where(p => p.Bad.Count > 0).ToList();
            Check("P2 " + k.Name + " run, " + (side == 1 ? "player" : "boss") + " generating: the " + derived.Count + " Cards.cs pools (class, format, cost, Death Knight, Demon Hunter, Lackeys, totems) hold 0 unscripted ids",
                leftD.Count == 0, Few(leftD.Select(p => p.Name + "[" + string.Join(" ", p.Bad.Take(4)) + "]")));
        }
    // The Entourage lists (a card's own fixed set, LegacyGenerationV1 "Entourage/<id>": Animal Companion's three, Totemic Call's
    // four): not routed through the filter (implementation record, deviation 13), so they must hold no unscripted card themselves.
    var entourage = LegacyGenerationV1.Names.Where(nm => nm.StartsWith("Entourage/", StringComparison.Ordinal)).OrderBy(nm => nm, StringComparer.Ordinal).ToList();
    var entBad = entourage.SelectMany(nm => LegacyGenerationV1.Ids(nm).Where(unscriptedAll.Contains).Select(id => nm.Substring(10) + ":" + id)).Distinct().ToList();
    var entSourcesBad = entourage.Where(nm => LegacyGenerationV1.Ids(nm).Any(unscriptedAll.Contains)).Select(nm => nm.Substring(10)).ToList();
    var rejL = new HashSet<string>(DungeonRunAdmission.GeneratedRejects(false), StringComparer.Ordinal); var rejC = new HashSet<string>(DungeonRunAdmission.GeneratedRejects(true), StringComparer.Ordinal);
    Check("P5 the " + entourage.Count + " Entourage lists: an unscripted card sits only in the list of a card that is itself unscripted and never generated in a run (Elite Tauren Chieftain's three songs)",
        entSourcesBad.All(id => unscriptedAll.Contains(id) && rejL.Contains(id) && rejC.Contains(id)) && entourage.Count >= 30, "with one: " + Few(entBad, 30));
    // An unfiltered game: the same list back, so a non-run game's random draws read the same arrays as before step 9.
    var plain = Ready(null);
    Check("P4 outside a run match every legacy pool and Cards.cs pool is returned as the same list (no filtering, no copy)",
        raw.All(p => ReferenceEquals(OfflineCardPools.ForGame(plain, p.Pool), p.Pool)) && derived.All(p => ReferenceEquals(OfflineCardPools.ForGame(plain, p.Pool), p.Pool))
        && ReferenceEquals(OfflineCardPools.ForGame(plain, AddLackeyTask.Lackeys), AddLackeyTask.Lackeys));

    // The run's own pools (encounters, treasures, shrines): every static method of the Dungeon Run card sets (nested types
    // included) that returns cards or card ids, whatever its name, called with every argument shape it takes, in a run match,
    // for each side. A method whose arguments cannot be built, or a call that throws, fails P3 (it would be a pool not checked).
    var asm = typeof(DungeonRunContent).Assembly;
    var methods = asm.GetTypes().Where(t => t.Namespace == "SabberStoneCore.CardSets.DungeonRun")
        .SelectMany(t => t.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
        .Where(m => !m.IsGenericMethodDefinition && !m.IsSpecialName && !m.Name.Contains("<") && !m.DeclaringType.Name.Contains("<")
            && (typeof(IEnumerable<Card>).IsAssignableFrom(m.ReturnType) || typeof(IEnumerable<string>).IsAssignableFrom(m.ReturnType)))
        .OrderBy(m => m.DeclaringType.FullName + "." + m.Name + "/" + m.GetParameters().Length, StringComparer.Ordinal).ToList();
    var classes = new[] { CardClass.MAGE, CardClass.WARRIOR, CardClass.HUNTER, CardClass.PRIEST, CardClass.ROGUE, CardClass.DRUID, CardClass.PALADIN, CardClass.SHAMAN, CardClass.WARLOCK, CardClass.DEMONHUNTER, CardClass.DEATHKNIGHT };
    var treasurePools = new[] { null, "Heist", "MonsterHunt", "Tombs", "Classic", "Loot" };
    foreach (var k in kinds)
    {
        int calls = 0, failedCalls = 0; var called = new SortedSet<string>(StringComparer.Ordinal); var bad = new SortedSet<string>(StringComparer.Ordinal); var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var side in new[] { 1, 2 })
        {
            var g = Ready(BasePlan(k.Catalog));
            var c = side == 1 ? g.Player1 : g.Player2;
            ToSide(g, c);
            var minion = Board(g, c, Yeti);
            foreach (var m in methods)
            {
                var ps = m.GetParameters();
                List<object[]> argSets = new List<object[]> { new object[0] };
                bool unknown = false;
                foreach (var p in ps)
                {
                    IEnumerable<object> vals;
                    var pt = p.ParameterType;
                    if (pt == typeof(Game)) vals = new object[] { g };
                    else if (pt == typeof(Controller)) vals = new object[] { c };
                    else if (pt == typeof(Minion)) vals = new object[] { minion };
                    else if (pt == typeof(IEntity) || pt == typeof(IPlayable)) vals = new object[] { c.Hero };
                    else if (pt == typeof(int)) vals = p.HasDefaultValue ? new object[] { p.DefaultValue } : new object[] { 1, 2, 3, 5, 8 };
                    else if (pt == typeof(bool)) vals = new object[] { false, true };
                    else if (pt == typeof(CardType)) vals = new object[] { CardType.MINION, CardType.SPELL, CardType.WEAPON };
                    else if (pt == typeof(CardClass)) vals = classes.Cast<object>();
                    else if (pt == typeof(string)) vals = treasurePools.Cast<object>();
                    else if (pt == typeof(string[])) vals = new object[] { new string[0] };
                    else if (pt == typeof(Race)) vals = new object[] { Race.INVALID, Race.BEAST, Race.DRAGON, Race.MURLOC, Race.DEMON, Race.MECHANICAL, Race.ELEMENTAL, Race.PIRATE, Race.TOTEM };
                    else if (pt == typeof(Card)) vals = new object[] { C(Yeti), C("EX1_165"), C("CFM_621") };
                    else if (pt == typeof(Func<Card, bool>)) vals = p.HasDefaultValue ? new object[] { null, (Func<Card, bool>)(x => true) } : new object[] { (Func<Card, bool>)(x => true) };
                    else { unknown = true; break; }
                    argSets = argSets.SelectMany(a => vals.Select(v => a.Append(v).ToArray())).ToList();
                }
                string key = m.DeclaringType.Name + "." + m.Name + "(" + string.Join(",", ps.Select(p => p.ParameterType.Name)) + ")";
                if (unknown) { failedCalls++; called.Add(key + " [arguments not built]"); continue; }
                foreach (var args in argSets)
                {
                    object result;
                    try { result = m.Invoke(null, args); calls++; called.Add(key); }
                    catch (TargetInvocationException e) { failedCalls++; called.Add(key + " [threw " + e.InnerException?.GetType().Name + "]"); continue; }
                    var ids = result is IEnumerable<Card> cs ? cs.Where(x => x != null).Select(x => x.Id) : ((IEnumerable<string>)result).Where(x => x != null);
                    foreach (var id in ids) { seen.Add(id); if (unscriptedAll.Contains(id)) bad.Add(key + ":" + id); }
                }
            }
        }
        if (k.Name == "legacy") Note("run pools called (" + called.Count + "): " + string.Join(" ", called));
        Check("P3 " + k.Name + " run: the " + methods.Count + " run pool methods (" + calls + " calls, both sides) yield " + seen.Count + " distinct ids, 0 unscripted; every method called",
            bad.Count == 0 && failedCalls == 0 && methods.Count >= 30 && calls >= 100 && seen.Count >= 500,
            Few(bad) + "; calls not made or that threw " + failedCalls + ": " + Few(called.Where(x => x.Contains(" ["))));
    }
});

// ============================================================ C  every routed call site filters
// The run's exclusions are replaced by "everything in the pool but these few", so a call site that reads its pool through the
// filter can only yield those; one that does not yields the rest of the pool (the pools hold dozens to thousands of cards).
Guard("C", () =>
{
    Game Allow(IEnumerable<Card> pool, IEnumerable<string> keep, int seed)
    {
        var keepSet = new HashSet<string>(keep, StringComparer.Ordinal);
        var excluded = pool.Select(c => c.Id).Where(id => !keepSet.Contains(id)).Distinct().ToArray();
        var g = Ready(BasePlan(false, excluded), seed);
        return g;
    }
    IEnumerable<string> Scripted(IEnumerable<Card> pool, int n) => pool.Where(c => c.Implemented && !c.IsSecret).Select(c => c.Id).Distinct().OrderBy(x => x, StringComparer.Ordinal).Take(n);
    void Site(string name, IReadOnlyList<Card> pool, int keepN, Func<Game, IEnumerable<string>> once, int tries = 12, bool anyCard = false)
    {
        var keep = (anyCard ? pool.Select(c => c.Id).Distinct().OrderBy(x => x, StringComparer.Ordinal).Take(keepN) : Scripted(pool, keepN)).ToList();
        var got = new SortedSet<string>(StringComparer.Ordinal);
        for (int s = 0; s < tries; s++)
        {
            var g = Allow(pool, keep, 100 + s);
            foreach (var id in once(g)) got.Add(id);
        }
        var stray = got.Where(id => !keep.Contains(id)).ToList();
        Check("C " + name + ": a run match's filter leaves " + keep.Count + " of " + pool.Select(c => c.Id).Distinct().Count() + "; " + tries + " runs yield only those",
            got.Count > 0 && stray.Count == 0, "yielded " + got.Count + ", stray " + Few(stray));
    }
    IEnumerable<string> HandAdds(Controller c, int before) => c.HandZone.Skip(before).Select(p => p.Card.Id).ToList();
    IEnumerable<string> RunTask(Game g, ISimpleTask task, IPlayable source = null, IPlayable target = null)
    {
        var c = g.Player1; Mana(c); EmptyHand(c);
        task.Process(g, c, source ?? c.Hero, target, null);
        g.DeathProcessingAndAuraUpdate();
        var r = HandAdds(c, 0).Concat(Offer(c).Select(x => x.Id)).Concat(c.BoardZone.Where(m => m.Card.Id != Yeti && m.Card.Id != Wisp).Select(m => m.Card.Id)).ToList();
        Drop(c);
        return r;
    }

    Site("AddLackeyTask", AddLackeyTask.Lackeys, 2, g => RunTask(g, new AddLackeyTask(3)));
    foreach (var (type, pool) in new[] { (DiscoverType.ARTIFACT, "Discover/ARTIFACT"), (DiscoverType.ELEMENTAL_INVOCATION, "Discover/ELEMENTAL_INVOCATION"),
                 (DiscoverType.BASIC_TOTEM, "Discover/BASIC_TOTEM"), (DiscoverType.BRANCHING_PATHS, "Discover/BRANCHING_PATHS"), (DiscoverType.HEISTBARON_TOGWAGGLE, "Discover/HEISTBARON_TOGWAGGLE") })
    {
        var p = LegacyGenerationV1.Get(pool);
        Site("DiscoverTask " + type, p, Math.Min(3, p.Count - 1), g => RunTask(g, new DiscoverTask(type)), anyCard: true);
    }
    foreach (var rarity in new[] { Rarity.COMMON, Rarity.RARE, Rarity.EPIC, Rarity.LEGENDARY })
    {
        if (!LegacyGenerationV1.Names.Contains("Replace/" + rarity)) continue;
        Site("ReplaceTask " + rarity, LegacyGenerationV1.Get("Replace/" + rarity), 2, g =>
        {
            var c = g.Player1; var before = c.DeckZone.Select(x => x.Id).ToHashSet();
            new ReplaceTask(EntityType.DECK, rarity).Process(g, c, c.Hero, null, null);
            return c.DeckZone.Select(x => x.Card.Id).Distinct().Where(id => id != Yeti).ToList();
        }, 4);
    }
    var adaptPool = (Card[])typeof(AdaptTask).GetField("TotalAdaptCards", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
    Site("AdaptTask", adaptPool, 4, g => { var m = Board(g, g.Player1, Yeti); return RunTask(g, new AdaptTask(EntityType.SOURCE), m); });
    var potionCards = (Card[][])typeof(KazakusPower).GetField("PotionCards", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
    Site("Kazakus (PotionGenerating) first choice", potionCards[0], 2, g =>
    {
        var c = g.Player1; Mana(c); EmptyHand(c);
        KazakusPower.CreateCostChoices(c, c.Hero);
        var r = Offer(c).Select(x => x.Id).ToList(); Drop(c); return r;
    }, anyCard: true);
    // The ingredient choices (PotionGenerating's second and third ForGame sites): the cost-1 potion picked, then its first
    // ingredient; both offers come from the cost-1 ingredient pool (Potion/1), of which the filter leaves four.
    Site("Kazakus (PotionGenerating) second and third choices", potionCards[1], 4, g =>
    {
        var c = g.Player1; Mana(c); EmptyHand(c);
        KazakusPower.CreateCostChoices(c, c.Hero);
        // The picks go through the engine's choice path (ChooseTask.Pick -> KazakusPower.Action), as a player makes them.
        Pick(g, c, c.Choice.Choices.FindIndex(id => g.IdEntityDic[id].Card.Cost == 1));
        var second = Offer(c).Select(x => x.Id).ToList();
        Pick(g, c, 0);
        var third = Offer(c).Select(x => x.Id).ToList(); Drop(c);
        return second.Count == 0 || third.Count == 0 ? new List<string>() : second.Concat(third).ToList();
    }, anyCard: true);
    var ungoro = SpecificTask.UngoroPack;
    { var warm = Ready(null); ungoro.Process(warm, warm.Player1, warm.Player1.Hero, null, null); }   // the pack pools are built on first use
    var packMemory = (System.Collections.ObjectModel.ReadOnlyDictionary<Rarity, Card[]>)typeof(SpecificTask).GetField("_ungoroPackMemory", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
    var packPool = packMemory.Values.SelectMany(x => x).ToList();
    Site("Un'Goro pack (SpecificTask.UngoroPack)", packPool, 30, g => RunTask(g, ungoro), 4);
    var pastLeg = (Card[])typeof(SpecificTask.GetRandomPastLegendary).GetField("PastLegendaryMinions", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
    Site("GetRandomPastLegendary", pastLeg, 2, g => { var r = new List<string>(); for (int i = 0; i < 6; i++) r.AddRange(RunTask(g, new SpecificTask.GetRandomPastLegendary())); return r; });
    var cost2 = Cards.CostMinionCards(FormatType.FT_WILD)[2];
    Site("RandomMinionTask (Cost 2)", cost2, 2, g =>
    {
        var r = new List<string>(); var c = g.Player1;
        for (int i = 0; i < 3; i++) { var stack = new TaskStack(); new RandomMinionTask(GameTag.COST, 2).Process(g, c, c.Hero, null, stack); r.AddRange(stack.Playables?.Select(x => x.Card.Id) ?? new string[0]); }
        return r;
    });
    var cost3 = Cards.CostMinionCards(FormatType.FT_WILD)[3];
    Site("TransformMinionTask (TransformRandomTask.cs; Cost 2 -> 3)", cost3, 2, g =>
    {
        var r = new List<string>(); var c = g.Player1;
        for (int i = 0; i < 3; i++) { ClearBoard(c); var m = Board(g, c, "CS2_120"); new TransformMinionTask(EntityType.TARGET, 1).Process(g, c, c.Hero, m, null); g.DeathProcessingAndAuraUpdate(); r.AddRange(c.BoardZone.Select(x => x.Card.Id)); }
        return r;
    });
    Site("RandomMinionNumberTask (COST 3)", cost3, 2, g =>
    {
        var r = new List<string>(); var c = g.Player1;
        for (int i = 0; i < 3; i++) { ClearBoard(c); var stack = new TaskStack { Number = 3 }; new RandomMinionNumberTask(GameTag.COST).Process(g, c, c.Hero, null, stack); r.AddRange(stack.Playables?.Select(x => x.Card.Id) ?? new string[0]); }
        return r;
    });
    string fmt = LegacyGenerationV1.Format(FormatType.FT_WILD);
    var beastsFirst = LegacyGenerationV1.Get(fmt + "/Beasts/First");
    var beastsSecond = LegacyGenerationV1.Get(fmt + "/Beasts/Second");
    var beastBoth = beastsFirst.Concat(beastsSecond).ToList();
    var keepBeasts = Scripted(beastsFirst, 3).Concat(Scripted(beastsSecond, 3)).ToList();
    var beastBuilds = new SortedSet<string>(StringComparer.Ordinal);
    for (int s = 0; s < 6; s++)
    {
        var g = Allow(beastBoth, keepBeasts, 300 + s);
        var c = g.Player1; Mana(c); EmptyHand(c);
        SpecificTask.BuildABeast.Process(g, c, c.Hero, null, null);
        foreach (var id in Offer(c).Select(x => x.Id)) beastBuilds.Add(id);
        Drop(c);
    }
    Check("C Build-A-Beast (SpecificTask): the offered first parts are only the parts the filter leaves", beastBuilds.Count > 0 && beastBuilds.All(keepBeasts.Contains), Few(beastBuilds.Where(id => !keepBeasts.Contains(id))));
    // The client Build-A-Beast (CardSets/Client/BuildABeast.cs, EffectContext.Generatable) and Import "Pet" (F16, ExoticPetParts).
    var clientBegin = typeof(DungeonRunContent).Assembly.GetType("SabberStoneCore.CardSets.Client.BuildABeast").GetMethod("Begin", BindingFlags.Static | BindingFlags.NonPublic);
    var clientParts = new SortedSet<string>(StringComparer.Ordinal); var petParts = new SortedSet<string>(StringComparer.Ordinal);
    for (int s = 0; s < 6; s++)
    {
        var g = Allow(beastBoth, keepBeasts, 400 + s);
        var c = g.Player1; Mana(c); EmptyHand(c);
        clientBegin.Invoke(null, new object[] { new SabberStoneCore.EffectSystem.EffectContext(g, c.Id, c.Hero.Id) });
        foreach (var id in Offer(c).Select(x => x.Id)) clientParts.Add(id);
        Drop(c);
        foreach (bool first in new[] { true, false }) foreach (var x in DungeonRunEncounters.ExoticPetParts(c, first)) petParts.Add(x.Id);
    }
    Check("C Build-A-Beast (client, EffectContext.Generatable): the offered parts are only the parts the filter leaves", clientParts.Count > 0 && clientParts.All(keepBeasts.Contains), Few(clientParts.Where(id => !keepBeasts.Contains(id))));
    Check("C Import \"Pet\" (F16 ExoticPetParts): both part pools are only the parts the filter leaves", petParts.Count > 0 && petParts.All(keepBeasts.Contains), Few(petParts.Where(id => !keepBeasts.Contains(id))));
});

// ============================================================ L  the Lackeys
Guard("L", () =>
{
    var seven = new[] { "DAL_613", "DAL_614", "DAL_615", "DAL_739", "DAL_741", "DRG_052", "ULD_616" };
    var ids = AddLackeyTask.Lackeys.Select(c => c.Id).OrderBy(x => x, StringComparer.Ordinal).ToList();
    Check("L1 the engine's Lackey pool (AddLackeyTask, LegacyGenerationV1 \"Lackeys\") holds the 7 Lackeys, every one scripted",
        ids.SequenceEqual(seven) && AddLackeyTask.Lackeys.All(c => c.Implemented), string.Join(",", ids));
    Check("L2 the run's Lackey lists (E.V.I.L. Promotion, E.V.I.L. Conscription) are the same 7", DungeonRunEncounters.TombsA.Lackeys.OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(seven)
        && DungeonRunEncounters.TombsC.Lackeys.OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(seven));
    foreach (var k in kinds)
    {
        var g = Ready(BasePlan(k.Catalog));
        var boss = g.Player2;
        var pool = DungeonRunEncounters.TombsC.LackeyPool(g, boss);
        ToSide(g, g.Player1);
        int forPlayer = OfflineCardPools.ForGame(g, AddLackeyTask.Lackeys).Length;
        ToSide(g, boss);
        int forBoss = OfflineCardPools.ForGame(g, AddLackeyTask.Lackeys).Length;
        Check("L3 " + k.Name + " run: the filter keeps all 7 for either side, and E.V.I.L. Conscription's pool for the boss has 7", pool.Count == 7 && forPlayer == 7 && forBoss == 7, pool.Count + " / " + forPlayer + " / " + forBoss);
    }
    var gl = Ready(BasePlan(false), 11);
    var kinds7 = new SortedSet<string>(StringComparer.Ordinal);
    for (int i = 0; i < 60; i++) { EmptyHand(gl.Player1); new AddLackeyTask(1).Process(gl, gl.Player1, gl.Player1.Hero, null, null); foreach (var p in gl.Player1.HandZone) kinds7.Add(p.Card.Id); }
    Check("L4 in a run match AddLackeyTask hands out all 7 kinds over 60 draws", kinds7.SequenceEqual(seven), string.Join(",", kinds7));

    Check("L5 the scripted texts are the client's (Titanic: +2 Health and Taunt to a friendly minion; Draconic: Discover a Dragon)",
        C("ULD_616").Text == factText["ULD_616"] && C("DRG_052").Text == factText["DRG_052"] && C("ULD_616e").Text == factText["ULD_616e"]);
    // Titanic Lackey
    var g1 = Ready(BasePlan(false), history: true);
    var friend = Board(g1, g1.Player1, Wisp);
    var foe = Board(g1, g1.Player2, Yeti);
    var titanic = ToHand(g1, g1.Player1, "ULD_616");
    bool foeTarget = g1.Player1.Options().OfType<PlayCardTask>().Any(t => t.Source == titanic && t.Target == foe);
    Check("L6 Titanic Lackey cannot target an enemy minion", !foeTarget);
    bool played = Play(g1, titanic, friend);
    var lackeyMinion = g1.Player1.BoardZone.FirstOrDefault(m => m.Card.Id == "ULD_616");
    Check("L7 Titanic Lackey gives a friendly minion +2 Health and Taunt (Hardened, ULD_616e), its Attack unchanged; it is a 1/1",
        played && friend.Health == 3 && friend.AttackDamage == 1 && friend.HasTaunt && friend.AppliedEnchantments.Any(e => e.Card.Id == "ULD_616e")
        && lackeyMinion != null && lackeyMinion.AttackDamage == 1 && lackeyMinion.Health == 1 && !lackeyMinion.HasTaunt,
        "wisp " + friend.AttackDamage + "/" + friend.Health + " base " + friend.BaseHealth + " taunt " + friend.HasTaunt + " ench " + string.Join(",", friend.AppliedEnchantments?.Select(e => e.Card.Id) ?? new string[0])
        + "; lackey " + (lackeyMinion == null ? "none" : lackeyMinion.AttackDamage + "/" + lackeyMinion.Health + " taunt " + lackeyMinion.HasTaunt));
    new SilenceTask(EntityType.TARGET).Process(g1, g1.Player2, g1.Player2.Hero, friend, null);
    g1.DeathProcessingAndAuraUpdate();
    Check("L7b silenced, the minion loses Hardened: a 1/1 without Taunt again", friend.Health == 1 && !friend.HasTaunt && friend.AppliedEnchantments?.Any(e => e.Card.Id == "ULD_616e") != true,
        friend.AttackDamage + "/" + friend.Health + " taunt " + friend.HasTaunt);
    var g2 = Ready(BasePlan(false));
    ClearBoard(g2.Player1);
    var lone = ToHand(g2, g2.Player1, "ULD_616");
    Check("L8 with no friendly minion Titanic Lackey is played with no effect", Play(g2, lone) && g2.Player1.BoardZone.Count == 1 && !g2.Player1.BoardZone[0].HasTaunt && g2.Player1.BoardZone[0].Health == 1);
    // Draconic Lackey: Discover a Dragon (three distinct Dragons; the pick goes to the hand), in run matches.
    int offers = 0; bool allDragons = true, distinct = true, picked = true; var dragonIds = new SortedSet<string>(StringComparer.Ordinal); var badDragon = new SortedSet<string>(StringComparer.Ordinal);
    foreach (var k in kinds)
        for (int s = 0; s < 20; s++)
        {
            var g = Ready(BasePlan(k.Catalog), 40 + s);
            var c = g.Player1; EmptyHand(c);
            var dl = ToHand(g, c, "DRG_052");
            if (!Play(g, dl)) { picked = false; continue; }
            var offer = Offer(c);
            if (offer.Count == 0) { picked = false; continue; }
            offers++;
            allDragons &= offer.All(x => x.IsRace(Race.DRAGON));
            distinct &= offer.Select(x => x.Id).Distinct().Count() == offer.Count && offer.Count == 3;
            foreach (var x in offer) { dragonIds.Add(x.Id); if (unscriptedAll.Contains(x.Id)) badDragon.Add(x.Id); }
            string want = offer[s % offer.Count].Id;
            Pick(g, c, s % offer.Count);
            picked &= c.Choice == null && c.HandZone.Count == 1 && c.HandZone[0].Card.Id == want && g.Player1.BoardZone.Any(m => m.Card.Id == "DRG_052");
        }
    Check("L9 Draconic Lackey offers three distinct Dragons and the pick goes to the hand (40 run matches)", offers == 40 && allDragons && distinct && picked, offers + " offers, " + dragonIds.Count + " Dragons seen");
    Check("L10 no unscripted Dragon is offered in a run match", badDragon.Count == 0, Few(badDragon));
});

// ============================================================ K  Kingsbane keeps the Durability its enchantments added
Guard("K", () =>
{
    (Weapon w, IPlayable copy) Shuffled(bool buff, int hits)
    {
        var g = Ready(BasePlan(false), history: true);
        var c = g.Player1;
        Play(g, ToHand(g, c, "LOOT_542"));
        if (buff) Play(g, ToHand(g, c, "NEW1_024"));
        var w = c.Hero.Weapon;
        for (int i = 0; i < hits; i++)
        {
            Mana(c); c.Hero.IsExhausted = false; c.Hero.NumAttacksThisTurn = 0;
            g.Process(HeroAttackTask.Any(c, g.Player2.Hero));
        }
        int deckBefore = c.DeckZone.Count;
        var weaponNow = c.Hero.Weapon;
        if (buff) Note("K: the buffed Kingsbane before it breaks: " + weaponNow.AttackDamage + "/" + weaponNow.Durability + " damage " + weaponNow.Damage + ", enchantments " + string.Join(",", weaponNow.AppliedEnchantments?.Select(e => e.Card.Id) ?? new string[0]));
        new DestroyTask(EntityType.WEAPON).Process(g, c, c.Hero, null, null);
        g.DeathProcessingAndAuraUpdate();
        var copy = c.DeckZone.FirstOrDefault(p => p.Card.Id == "LOOT_542");
        return (weaponNow, copy);
    }
    var (w0, plain) = Shuffled(false, 1);
    Check("K1 a plain Kingsbane that lost 1 Durability is shuffled back as a 1/3 with no damage", plain != null && plain[GameTag.ATK] == 1 && plain[GameTag.DURABILITY] == 3 && plain[GameTag.DAMAGE] == 0,
        plain == null ? "no copy" : plain[GameTag.ATK] + "/" + plain[GameTag.DURABILITY] + " dmg " + plain[GameTag.DAMAGE]);
    var (w1, buffed) = Shuffled(true, 2);
    Check("K2 Captain Greenskin's +1/+1 is kept: the copy is a 2/4 with no damage (it lost 2 before)",
        buffed != null && buffed[GameTag.ATK] == 2 && buffed[GameTag.DURABILITY] == 4 && buffed[GameTag.DAMAGE] == 0,
        buffed == null ? "no copy" : buffed[GameTag.ATK] + "/" + buffed[GameTag.DURABILITY] + " dmg " + buffed[GameTag.DAMAGE] + " ench " + string.Join(",", buffed.AppliedEnchantments?.Select(e => e.Card.Id) ?? new string[0]));
    // Equip the copy again: still a 2/4, and destroyed again it comes back once more as a 2/4 (no growth per trip).
    var g2 = Ready(BasePlan(false));
    var c2 = g2.Player1;
    Play(g2, ToHand(g2, c2, "LOOT_542"));
    Play(g2, ToHand(g2, c2, "NEW1_024"));
    int[] trip = new int[6]; int ti = 0;
    for (int round = 0; round < 3; round++)
    {
        new DestroyTask(EntityType.WEAPON).Process(g2, c2, c2.Hero, null, null);
        g2.DeathProcessingAndAuraUpdate();
        var copy = c2.DeckZone.FirstOrDefault(p => p.Card.Id == "LOOT_542");
        if (copy == null) break;
        c2.DeckZone.Remove(copy); Generic.AddHandPhase.Invoke(c2, copy);
        Play(g2, copy);
        var w = c2.Hero.Weapon;
        if (w == null) break;
        trip[ti++] = w.AttackDamage; trip[ti++] = w.Durability;
    }
    Check("K3 the copy equipped again is a 2/4, and stays a 2/4 over three trips (the enchantment is counted once)", ti == 6 && trip[0] == 2 && trip[1] == 4 && trip[2] == 2 && trip[3] == 4 && trip[4] == 2 && trip[5] == 4,
        string.Join(",", trip.Take(ti)));
});

// ============================================================ G  200 seeded run games: 0 unscripted cards generated
Guard("G", () =>
{
    int n = int.TryParse(Environment.GetEnvironmentVariable("C_GEN_9_GAMES"), out int gn) ? gn : 200;
    var loadouts = data.Classes.SelectMany(c => c.Decks.Select(d => (Row: c, Deck: d))).ToList();
    var bosses = data.Encounters.Where(b => data.BossDecks.ContainsKey(b.Deck)).Select(b => b.Id).OrderBy(x => x, StringComparer.Ordinal).ToList();
    var generatedAll = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var unscriptedMade = new SortedDictionary<string, int>(StringComparer.Ordinal);
    int setupMade = 0;
    var notImplementedMade = new SortedSet<string>(StringComparer.Ordinal);
    int games = 0, faults = 0, generatedEntities = 0, ended = 0; var faultLines = new List<string>(); var gameLines = new System.Text.StringBuilder();
    var types = new HashSet<CardType> { CardType.MINION, CardType.SPELL, CardType.WEAPON, CardType.HERO, CardType.LOCATION };
    for (int i = 0; i < n; i++)
    {
        var lo = loadouts[(i * 7) % loadouts.Count];
        string boss = bosses[(i * 13 + 5) % bosses.Count];
        try
        {
            // Every other game on acts-v4t; a loadout whose hero is acts-v4t's own finale character (FinaleExcludes) plays acts-v4h.
            DungeonRunState st;
            try { st = At(lo.Row, lo.Deck, 9000 + i, boss, schedule: i % 2 == 0 ? "acts-v4h" : "acts-v4t"); }
            catch (InvalidOperationException e) when (i % 2 == 1 && e.Message.Contains("finale")) { st = At(lo.Row, lo.Deck, 9000 + i, boss, schedule: "acts-v4h"); }
            // A deck of the run's own cards: the starter deck plus two bundles the class is offered (a run in its second act).
            var plan = run.BuildPlan(st, all);
            var bundles = data.Bundles.Where(b => b.Cards.Count > 0 && (b.Class == "NEUTRAL" || lo.Row.ClassSet.Contains(b.Class))).ToList();
            if (bundles.Count > 0)
            {
                var extra = bundles[(i * 5) % bundles.Count].Cards.Concat(bundles[(i * 11 + 3) % bundles.Count].Cards).Select(C).Where(x => x != null);
                plan.PlayerDeck = plan.PlayerDeck.Concat(extra).ToList();
                plan.PlayerDeckSlots = null;
            }
            var g = Ready(plan, 5000 + i * 31);
            var known = new HashSet<(int, string)>();
            // What the plan itself puts in the game (both decks and heroes): at the start of play every other entity was made by
            // the setup (Start of Game treasures and passives, encounter and global rules), and is checked as a generated card too.
            var planned = new HashSet<string>(plan.PlayerDeck.Concat(plan.BossDeck).Where(x => x != null).Select(x => x.Id)
                .Concat(new[] { plan.PlayerHero?.Id, plan.BossHero?.Id }.Where(x => x != null)), StringComparer.Ordinal);
            void Scan(bool record)
            {
                foreach (var e in g.IdEntityDic.Values)
                {
                    if (e?.Card == null || !types.Contains(e.Card.Type)) continue;
                    if (!known.Add((e.Id, e.Card.Id))) continue;
                    if (!record)
                    {
                        if (planned.Contains(e.Card.Id)) continue;
                        setupMade++;
                        if (unscriptedAll.Contains(e.Card.Id) && (e.Card.Type != CardType.HERO || legacySet.Contains(e.Card.Id))) unscriptedMade[e.Card.Id + " (" + boss + ", game " + i + ", setup)"] = 1;
                        continue;
                    }
                    generatedEntities++;
                    generatedAll[e.Card.Id] = generatedAll.TryGetValue(e.Card.Id, out int v) ? v + 1 : 1;
                    // A hero card counts only when a generation pool holds it: an encounter's form heroes (George and Karl, Xatma's
                    // second form) are the fight's own heroes, swapped in by its rules, not generated.
                    if (unscriptedAll.Contains(e.Card.Id) && (e.Card.Type != CardType.HERO || legacySet.Contains(e.Card.Id))) unscriptedMade[e.Card.Id + " (" + boss + ", game " + i + ")"] = 1;
                    if (!e.Card.Implemented) notImplementedMade.Add(e.Card.Id);
                }
            }
            Scan(false);
            int actions = 0;
            while (g.State != State.COMPLETE && g.Turn <= 60)
            {
                if (++actions > 2500) throw new Exception("stuck");
                var cp = g.CurrentPlayer;
                var task = LocalBot.Choose(cp.Options());
                if (task is ChooseTask pick)
                {
                    var decision = g.GetPendingDecisions().First(x => x.ControllerId == task.Controller.Id);
                    if (!g.TryAnswer(new SabberStoneCore.Model.Decisions.DecisionAnswer(decision.Id, decision.ControllerId, pick.Choices.ToArray()), out var why)) throw new Exception("choice rejected " + why);
                }
                else if (!g.Process(task)) throw new Exception("task rejected " + task);
                Scan(true);
            }
            games++;
            if (g.State == State.COMPLETE) ended++;
            gameLines.Append(i).Append(' ').Append(lo.Row.Id).Append(' ').Append(lo.Deck).Append(' ').Append(boss).Append(' ').Append(st.Schedule).Append(" -> ")
                .Append(g.Player1.PlayState).Append(" t").Append(g.Turn).Append(" hp ").Append(g.Player1.Hero.Health).Append('/').Append(g.Player2.Hero.Health)
                .Append(" acts ").Append(actions).Append(" made ").Append(known.Count).Append('\n');
        }
        catch (Exception e) { faults++; if (faultLines.Count < 5) faultLines.Add(i + " " + boss + " " + e.GetType().Name + ": " + e.Message.Split('\n')[0]); }
    }
    Note("G: " + games + " games (" + ended + " ended), " + generatedEntities + " cards made during play, " + generatedAll.Count + " distinct; " + setupMade + " made by the setup (not the plan's); with no script: " + Few(notImplementedMade, 40));
    foreach (var f in faultLines) Note("G fault " + f);
    Note("G games digest " + Hash(new[] { gameLines.ToString() }) + ", made-card digest " + Hash(generatedAll.Select(kv => kv.Key + "=" + kv.Value)));
    File.WriteAllText(VerifyPaths.Out("tmp/c-gen-9-games.txt"), gameLines.ToString());
    Check("G1 " + n + " seeded run games are played (v4 defaults, every loadout in turn, encounter bosses), no fault", games == n && faults == 0, games + " games, " + faults + " faults");
    Check("G2 the games make many cards during play (the check below has something to see)", generatedEntities >= 15 * n && generatedAll.Count >= 300, generatedEntities + " / " + generatedAll.Count);
    Check("G3 0 unscripted cards are generated in the " + n + " games (during play and by the setup)", unscriptedMade.Count == 0, Few(unscriptedMade.Keys, 20));
});

Console.WriteLine("RESULT " + passes + " passed, " + failures + " failed");
return failures == 0 ? 0 : 1;
