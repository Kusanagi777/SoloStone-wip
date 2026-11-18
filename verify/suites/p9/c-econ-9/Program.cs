using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;

// c-econ-9: step 9 lane E, the reward economy (handoff/phase9/plan.md section 1 A, criteria A1-A13), on the run layer (walks
// with no engine game), on the shipped data (encounters-v1.json loaded).
//   W  the run set: every loadout (hero row x starter deck) x {acts-v4h, acts-v4t} x 6 seeds, all wins, scored picks, every
//      tavern offer bought that the gold allows; and every loadout x acts-v3h x 2 seeds for the released values ("today").
//      Measures A1 (deck never past deckMax), A2 (deck size entering the finale), A3 (bundle drafts skipped for room; every
//      skip becomes banked gold, spent at the next tavern), A5 (adventure loadouts' offers with an own-adventure treasure),
//      A6 (rare share of offered treasures by act), A7 (DALA_904 buys per run), A9 (tavern offers Heist : Tombs), A12
//      (rewards per run by origin), A13 (Death Knight and Demon Hunter: distinct catalog entries offered per run)
//   C  A4: treasure-draft candidates per loadout (stamp 4 against acts-v3h)
//   G  A8: charm_seedling's floors from pick to full growth (acts-v4h against acts-v3h), on a draft that offers it
//   K  the mechanisms behind A3, A7 and D9 on constructed states: a bundle draft skipped for room becomes 1 banked gold on
//      stamp 4 (never past deckMax, nothing on acts-v3h), the banked gold is spent at the next tavern, no bundle after an act II
//      treasure floor (D5 variant (b)), DALA_904 refused after maxPerRun buys and HealthBonus capped (D4), Damph's reward
//      granted with room and banked without (D9)
//   D  A10 and A11: 600 new runs on the default roll: meetings per row within each tier pool (max/min), distinct
//      provenances per run, every encounter row met (and 600 runs on acts-v3h/acts-v3t for the released values)
// A check line (PASS/FAIL) is an invariant or a mechanism; a MEASURE line reports a criterion's value against its target:
// "[calibration target: met|missed]" for the plan's calibration targets (stage 4 moves the values; a miss is not a failure),
// "[reported]" for A12 and A13. C_ECON_9_ONLY=W,C,... runs only those sections (for the break-and-run proofs).
static object FromJson(JsonElement e)
{
    switch (e.ValueKind)
    {
        case JsonValueKind.Object: { var d = new Dictionary<string, object>(); foreach (var p in e.EnumerateObject()) d[p.Name] = FromJson(p.Value); return d; }
        case JsonValueKind.Array: { var l = new List<object>(); foreach (var x in e.EnumerateArray()) l.Add(FromJson(x)); return l; }
        case JsonValueKind.String: return e.GetString();
        case JsonValueKind.Number: return e.TryGetInt64(out long v) ? (object)v : e.GetDouble();
        case JsonValueKind.True: return true;
        case JsonValueKind.False: return false;
        default: return null;
    }
}
NpcWorldJson.Parser = text => { using var doc = JsonDocument.Parse(text); return FromJson(doc.RootElement); };

int failures = 0, passes = 0;
void Check(string name, bool ok, string detail = "")
{
    if (ok) passes++; else failures++;
    Console.WriteLine((ok ? "PASS " : "FAIL ") + name + (detail.Length > 0 ? "  (" + detail + ")" : ""));
}
void Note(string text) => Console.WriteLine("NOTE " + text);
void Measure(string id, string what, string value, string target, bool? met) =>
    Console.WriteLine("MEASURE " + id + " " + what + ": " + value + " | target " + target + " | " + (met == null ? "[reported]" : met.Value ? "[calibration target: met]" : "[calibration target: missed]"));
var only = Environment.GetEnvironmentVariable("C_ECON_9_ONLY") is string o && o.Length > 0 ? o.Split(',') : null;
void Guard(string name, Action body)
{
    if (only != null && !only.Contains(name)) return;
    try { body(); }
    catch (Exception e) { failures++; Console.WriteLine("FAIL " + name + " threw " + e.GetType().Name + ": " + e.Message.Split('\n')[0] + " @ " + (e.StackTrace ?? "").Split('\n').FirstOrDefault()?.Trim()); }
}
string F1(double x) => x.ToString("F1", CultureInfo.InvariantCulture);
string F2(double x) => x.ToString("F2", CultureInfo.InvariantCulture);
string Pct(int n, int d) => d == 0 ? "-" : F1(100.0 * n / d) + "%";
// Nearest-rank quantiles over integers.
int Q(IEnumerable<int> xs, double q) { var s = xs.OrderBy(x => x).ToList(); return s.Count == 0 ? 0 : s[Math.Max(0, (int)Math.Ceiling(q * s.Count) - 1)]; }
double Median(IEnumerable<double> xs) { var s = xs.OrderBy(x => x).ToList(); return s.Count == 0 ? 0 : s.Count % 2 == 1 ? s[s.Count / 2] : (s[s.Count / 2 - 1] + s[s.Count / 2]) / 2; }

string root = VerifyPaths.Data;
string R(string f) => File.ReadAllText(Path.Combine(root, f));
DungeonRunData Load() => DungeonRunData.Parse(R("dungeon-run/run-v1.json"), R("dungeon-run/pool-v1.json"), R("npc/decks-v1.json"),
    R("dungeon-run/catalog-v1.json"), R("dungeon-run/schedule-v1.json"), R("dungeon-run/tavern-v1.json"), R("dungeon-run/encounters-v1.json"));
var data = Load();
var run = new DungeonRun(data);
DungeonRunProfile All() { var p = new DungeonRunProfile(); p.Unlocked.AddRange(data.Unlocks.Keys); return p; }
var all = All();
string[] adventurePools = { "Heist", "MonsterHunt", "Tombs" };
string Origin(DungeonRunClass c) => c.Adventure ?? "classes";

// The loadouts: every hero row x every starter deck (116; acts-v4t leaves out DALA_Tekahn's four, FinaleExcludes).
var loadouts = data.Classes.SelectMany(c => c.Decks.Select(deck => (Row: c, Deck: deck))).ToList();
DungeonRunState New(DungeonRunClass row, string deck, long seed, string schedule, int power = 0) =>
    run.NewRun(all, row.Id, deck, row.Powers[power % row.Powers.Count], seed, schedule: schedule, signatureId: run.SignatureOffer(all, row, seed).FirstOrDefault());

// ------------------------------------------------------------ the scored pick policy
// Every choice is scored, never drawn in list order: a treasure by rarity (rare first), then the run's own adventure pool,
// then a seeded hash; every other choice (bundle, card, teammate, shrine upgrade, modifier, anomaly, tavern target) by the
// seeded hash alone. The hash keys on the run seed, the node, the floor, a per-choice salt and the option's id (FNV-1a), so
// the pick does not depend on how the offer is ordered.
long Fnv(string id) { uint h = 2166136261; foreach (char c in id ?? "") { h ^= c; h *= 16777619; } return h; }
int Hash(DungeonRunState s, int salt, string id) => StableSeed.Derive(s.Seed, 9100 + salt, s.Node * 16 + s.Floor, Fnv(id));
string Best(DungeonRunState s, IEnumerable<string> options, int salt) =>
    options.OrderByDescending(x => Hash(s, salt, x)).ThenBy(x => x, StringComparer.Ordinal).First();
string BestTreasure(DungeonRunState s, IEnumerable<string> options)
{
    string pool = run.RunClass(s).Adventure;
    return options.OrderByDescending(x => data.FindTreasure(x)?.Rarity == "rare").ThenByDescending(x => pool != null && data.FindTreasure(x)?.Pool == pool)
        .ThenByDescending(x => Hash(s, 1, x)).ThenBy(x => x, StringComparer.Ordinal).First();
}

// ------------------------------------------------------------ the walker
var scratch = new DungeonRunProfile();
int TakesOf(DungeonRunSchedule sc, DungeonRunNode node) => Math.Max(1, Math.Min(3, sc.Acts[node.Act - 1].DraftTakes));
// Walks a run to its end, every fight won. beforeStep (optional) may change the state before each step, and the walk stops
// before the first step at which stop (optional) holds (the constructed cases).
Walk WalkRun(DungeonRunState s, Action<DungeonRunState, Walk> beforeStep = null, Func<DungeonRunState, bool> stop = null)
{
    var w = new Walk { Schedule = s.Schedule, Origin = Origin(run.RunClass(s)), Row = s.Class, Tuned = DungeonRun.Tuned(s), DeckMax = run.DeckMax(s) };
    var sc = run.ScheduleFor(s);
    w.Signatures = s.Loadout?.Signature?.Count ?? 0;
    int bankedPred = Math.Max(0, s.BankedGold);
    (int Floor, DungeonRunNode Node)? pending = null;
    DungeonRunTavern lastTavern = s.Tavern;
    void Bad(string what) { if (w.FirstBad == null) w.FirstBad = s.Schedule + " seed " + s.Seed + " " + s.Class + " floor " + s.Floor + ": " + what; }
    for (int guard = 0; guard < 800 && !s.Finished; guard++)
    {
        if (stop != null && stop(s)) break;
        beforeStep?.Invoke(s, w);
        w.Steps++;
        var node = run.CurrentNode(s);
        switch (s.Phase)
        {
            case DungeonRunPhase.PreMatch:
            {
                if (w.FinaleDeck < 0 && sc.Acts[node.Act - 1].IsFinale) w.FinaleDeck = s.Deck.Count;
                var boss = run.CurrentBoss(s);
                string reward = w.Tuned && boss.IsEncounter ? boss.V4?.Reward : null;
                bool rewardable = reward != null && node.Index != sc.Nodes.Count - 1 && !sc.Acts[node.Act - 1].IsFinale
                    && !s.ActiveTreasures.Contains(reward) && !s.Treasures.Contains(reward);
                bool rewardRoom = s.Deck.Count + 1 <= run.DeckMax(s);
                int floor = s.Floor;
                run.BeginMatch(s);
                run.RecordOutcome(s, scratch, DungeonRunOutcome.Win);
                if (rewardable && !rewardRoom) { bankedPred++; w.BankedEarned++; }
                if (rewardable && rewardRoom) { w.Rewards++; if (!s.ActiveTreasures.Contains(reward) || !s.Deck.Contains(reward)) Bad("reward " + reward + " not granted"); }
                if (!s.Finished && node.Index != sc.Nodes.Count - 1 && !sc.Acts[node.Act - 1].IsFinale) pending = (floor, node);
                break;
            }
            case DungeonRunPhase.TreasureDraft:
            {
                var draft = s.OpenDraft;
                if (draft.Kind == "treasure")
                {
                    bool nonPassive = !node.PassiveOnly && s.Deck.Count + 1 <= run.DeckMax(s);
                    foreach (string t in draft.Offered) w.TreasureOffers.Add((node.Act, t, nonPassive));
                    if (nonPassive) w.NonPassiveOffers.Add(draft.Offered.ToList());
                    string pick = BestTreasure(s, draft.Offered);
                    run.PickTreasure(s, pick);
                    w.TreasurePicks++;
                    if (pick == "charm_seedling" && w.CharmPick < 0) w.CharmPick = s.Floor;
                }
                else
                {
                    if (draft.Kind == "teammate") w.Teammates++;
                    run.PickTreasure(s, Best(s, draft.Offered, 2));
                }
                break;
            }
            case DungeonRunPhase.Modifier:
            {
                var offer = s.ModifierOffer;
                var real = offer.Offered.Where(id => id != DungeonRunGlobalRule.None).ToList();
                bool anomaly = offer.Kind == DungeonRunGlobalRule.KindAnomaly;
                run.PickModifier(s, anomaly || real.Count == 0 ? Best(s, offer.Offered, 3) : Best(s, real, 3));
                break;
            }
            case DungeonRunPhase.Tavern:
            {
                foreach (string option in s.Tavern.Offers.ToList())
                {
                    var bought = DungeonRunTavernNode.Buy(run, s, option);
                    int salt = 10;
                    while (bought.Ok && s.Tavern.Pending != null) bought = DungeonRunTavernNode.Choose(run, s, Best(s, s.Tavern.Pending.Candidates, salt++));
                    if (s.Tavern.Pending != null) DungeonRunTavernNode.Back(s);
                    if (s.Deck.Count > run.DeckMax(s)) Bad("deck " + s.Deck.Count + " after tavern " + option);
                }
                w.Good904 += s.Tavern.Bought.Count(b => b.Option == "DALA_904");
                run.LeaveTavern(s);
                break;
            }
            case DungeonRunPhase.BundleDraft:
            {
                var draft = s.OpenDraft;
                foreach (string id in draft.Offered) w.BundleEntries.Add(id);
                if (draft.Takes == 0) { run.PickBundle(s, Best(s, draft.Offered, 4)); break; }
                string bundle = draft.Choosing ?? Best(s, draft.Offered, 5);
                var left = run.OfferCards(draft, bundle);
                foreach (string kept in draft.Chosen) left.Remove(kept);
                run.PickDraftCard(s, bundle, Best(s, left, 6));
                break;
            }
            default: throw new InvalidOperationException("walk stopped at " + s.Phase);
        }
        // The post-fight sequence is over (the run moved to the next node): was the bundle draft skipped, and why?
        if (pending != null && (s.Finished || s.Phase != DungeonRunPhase.TreasureDraft && s.Phase != DungeonRunPhase.BundleDraft))
        {
            var (floor, won) = pending.Value;
            pending = null;
            bool drafted = s.Drafts.Any(d => d.Kind == "bundle" && d.Floor == floor);
            if (!drafted && !run.NoBundleAfter(s, won))
            {
                if (s.Deck.Count + TakesOf(sc, won) > run.DeckMax(s)) { w.RoomSkips++; if (w.Tuned) { bankedPred++; w.BankedEarned++; } }
                else w.OtherSkips++;
            }
        }
        // A tavern opened in this step spends the banked gold.
        if (s.Phase == DungeonRunPhase.Tavern && s.Tavern != null && !ReferenceEquals(s.Tavern, lastTavern))
        {
            foreach (string id in s.Tavern.Offers) w.TavernOffers.Add(id);
            if (s.Tavern.Gold != run.TavernGold(s) + bankedPred) { w.TavernGoldBad++; Bad($"tavern gold {s.Tavern.Gold}, expected {run.TavernGold(s)} + {bankedPred} banked"); }
            bankedPred = 0;
        }
        lastTavern = s.Tavern;
        if (s.BankedGold != bankedPred) { w.BankedBad++; Bad($"BankedGold {s.BankedGold}, expected {bankedPred}"); bankedPred = s.BankedGold; }
        w.MaxDeck = Math.Max(w.MaxDeck, s.Deck.Count);
        if (s.Deck.Count > run.DeckMax(s)) Bad("deck " + s.Deck.Count + " past deckMax " + run.DeckMax(s));
        if (w.CharmPick >= 0 && w.CharmFull < 0 && FinalCharm(s)) w.CharmFull = s.Floor;
    }
    w.Victory = s.Phase == DungeonRunPhase.Victory;
    w.ShrineUpgrades = s.ShrineUpgrades?.Count ?? 0;
    return w;
}
// charm_seedling's last stage: the end of its upgradesTo chain.
string charmLast = "charm_seedling";
while (data.FindTreasure(charmLast)?.UpgradesTo is string next) charmLast = next;
bool FinalCharm(DungeonRunState s) => charmLast != "charm_seedling" && s.ActiveTreasures.Contains(charmLast);

// ============================================================ W. the run set
var walks = new List<Walk>();
Guard("W", () =>
{
    string[] v4 = { "acts-v4h", "acts-v4t" };
    int excluded = 0;
    foreach (var (schedule, seeds) in new[] { ("acts-v4h", 6), ("acts-v4t", 6), ("acts-v3h", 2) })
        for (int li = 0; li < loadouts.Count; li++)
            for (int k = 0; k < seeds; k++)
            {
                var (row, deck) = loadouts[li];
                long seed = 900000 + 100000 * Array.IndexOf(new[] { "acts-v4h", "acts-v4t", "acts-v3h" }, schedule) + 10 * li + k;
                DungeonRunState s;
                try { s = New(row, deck, seed, schedule, k); }
                catch (InvalidOperationException) { excluded++; continue; } // DALA_Tekahn on acts-v4t (FinaleExcludes)
                walks.Add(WalkRun(s));
            }
    var v4w = walks.Where(w => v4.Contains(w.Schedule)).ToList();
    var v3w = walks.Where(w => w.Schedule == "acts-v3h").ToList();
    Note($"W {walks.Count} walks: acts-v4h {walks.Count(w => w.Schedule == "acts-v4h")}, acts-v4t {walks.Count(w => w.Schedule == "acts-v4t")}, acts-v3h {v3w.Count} ({excluded} loadout seeds left out by FinaleExcludes); "
        + $"{walks.Sum(w => w.Steps)} steps; origins " + string.Join(", ", loadouts.GroupBy(l => Origin(l.Row)).OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => g.Key + " " + g.Count())));
    Check("W1 the run set: 116 loadouts x {acts-v4h, acts-v4t} x 6 seeds (acts-v4t without DALA_Tekahn's 4) and 116 x acts-v3h x 2, every walk to Victory",
        loadouts.Count == 116 && v4w.Count == 116 * 6 + 112 * 6 && v3w.Count == 232 && walks.All(w => w.Victory) && v4w.All(w => w.Tuned) && v3w.All(w => !w.Tuned),
        $"{loadouts.Count} loadouts, {v4w.Count} v4 walks, {walks.Count(w => !w.Victory)} not won");

    // A1
    var over = walks.Where(w => w.MaxDeck > w.DeckMax).ToList();
    Check("W2 A1: no deck ever exceeds deckMax (" + walks.Max(w => w.MaxDeck) + " cards at most, deckMax 50), at any step of any walk", over.Count == 0 && walks.All(w => w.DeckMax == 50),
        string.Join(" | ", over.Take(3).Select(w => w.FirstBad)));
    // A2
    var fin4 = v4w.Select(w => w.FinaleDeck).ToList(); var fin3 = v3w.Select(w => w.FinaleDeck).ToList();
    Check("W3 every walk reaches its finale (its deck size entering the finale is recorded)", walks.All(w => w.FinaleDeck > 0));
    foreach (string sch in v4)
    {
        var f = v4w.Where(w => w.Schedule == sch).Select(w => w.FinaleDeck).ToList();
        Note($"A2 {sch}: deck entering the finale median {Q(f, 0.5)}, p95 {Q(f, 0.95)}, min {f.Min()}, max {f.Max()}");
    }
    Measure("A2", "deck size entering the finale (pooled v4), median / p95", $"{Q(fin4, 0.5)} / {Q(fin4, 0.95)} (acts-v3h today: {Q(fin3, 0.5)} / {Q(fin3, 0.95)})",
        "median 34-42, p95 <= 48", Q(fin4, 0.5) >= 34 && Q(fin4, 0.5) <= 42 && Q(fin4, 0.95) <= 48);
    // A3
    var skips4 = v4w.Select(w => w.RoomSkips).ToList(); var skips3 = v3w.Select(w => w.RoomSkips).ToList();
    Measure("A3", "bundle drafts skipped for room per run (pooled v4), median / p95 / max / total", $"{Q(skips4, 0.5)} / {Q(skips4, 0.95)} / {skips4.Max()} / {skips4.Sum()} "
        + $"(acts-v3h today: {Q(skips3, 0.5)} / {Q(skips3, 0.95)} / {skips3.Max()} / {skips3.Sum()}, {v3w.Count(w => w.RoomSkips > 0)} of {v3w.Count} runs skip)",
        "median 0, p95 <= 1", Q(skips4, 0.5) == 0 && Q(skips4, 0.95) <= 1);
    Note($"A3 bundle drafts skipped with nothing on offer (not for room): v4 {v4w.Sum(w => w.OtherSkips)}, acts-v3h {v3w.Sum(w => w.OtherSkips)}");
    var bankBad = v4w.Where(w => w.BankedBad > 0 || w.TavernGoldBad > 0).ToList();
    Check($"W4 A3: every bundle skip for room (and every reward with no room) becomes 1 banked gold, spent at the next tavern ({v4w.Sum(w => w.BankedEarned)} banked gold over the v4 walks)",
        bankBad.Count == 0, bankBad.Count + " walks; " + string.Join(" | ", bankBad.Take(3).Select(w => w.FirstBad)));
    Check("W5 A3: the released runs (acts-v3h) bank nothing (BankedGold stays 0; their taverns get the act's gold only)", v3w.All(w => w.BankedEarned == 0 && w.BankedBad == 0 && w.TavernGoldBad == 0),
        string.Join(" | ", v3w.Where(w => w.BankedBad > 0 || w.TavernGoldBad > 0).Take(3).Select(w => w.FirstBad)));
    // A5
    int ownShare(List<Walk> ws, out int total)
    {
        var offers = ws.Where(w => adventurePools.Contains(w.Origin)).SelectMany(w => w.NonPassiveOffers.Select(off => (w.Origin, off))).ToList();
        total = offers.Count;
        return offers.Count(x => x.off.Any(t => data.FindTreasure(t)?.Pool == x.Origin));
    }
    int own4 = ownShare(v4w, out int tot4), own3 = ownShare(v3w, out int tot3);
    var perOrigin = adventurePools.Select(p => { int n = ownShare(v4w.Where(w => w.Origin == p).ToList(), out int t); return p + " " + Pct(n, t) + " of " + t; });
    Note("A5 per origin (v4): " + string.Join(", ", perOrigin) + "; acts-v3h today " + Pct(own3, tot3) + " of " + tot3);
    Check($"W6 A5: adventure loadouts' non-passive treasure offers holding at least one own-adventure treasure (D6 slot rule): {Pct(own4, tot4)} of {tot4} (target >= 95%)",
        tot4 > 1000 && own4 * 100 >= 95 * tot4, $"acts-v3h today {Pct(own3, tot3)}");
    // A6
    string Share(IEnumerable<(int Act, string Id, bool NonPassive)> offers, int act) { var l = offers.Where(x => x.Act == act).ToList(); return Pct(l.Count(x => data.FindTreasure(x.Id)?.Rarity == "rare"), l.Count); }
    double ShareOf(IEnumerable<(int Act, string Id, bool NonPassive)> offers, int act) { var l = offers.Where(x => x.Act == act).ToList(); return l.Count == 0 ? 0 : 100.0 * l.Count(x => data.FindTreasure(x.Id)?.Rarity == "rare") / l.Count; }
    var off4 = v4w.SelectMany(w => w.TreasureOffers).ToList(); var off3 = v3w.SelectMany(w => w.TreasureOffers).ToList();
    foreach (string origin in loadouts.Select(l => Origin(l.Row)).Distinct().OrderBy(x => x, StringComparer.Ordinal))
    {
        var o4 = v4w.Where(w => w.Origin == origin).SelectMany(w => w.TreasureOffers).ToList();
        Note($"A6 {origin}: rare share by act I / II / III {Share(o4, 1)} / {Share(o4, 2)} / {Share(o4, 3)} ({o4.Count} offered treasures)");
    }
    double[] band = { 25, 35, 38, 50, 52, 65 };
    bool a6 = Enumerable.Range(1, 3).All(a => ShareOf(off4, a) >= band[2 * a - 2] && ShareOf(off4, a) <= band[2 * a - 1]);
    Measure("A6", "rare share of offered treasures by act I / II / III (pooled v4, every offer)", $"{Share(off4, 1)} / {Share(off4, 2)} / {Share(off4, 3)} ({off4.Count} offered; acts-v3h today "
        + $"{Share(off3, 1)} / {Share(off3, 2)} / {Share(off3, 3)})", "I 25-35%, II 38-50%, III 52-65%", a6);
    // A7
    var buys4 = v4w.Select(w => w.Good904).ToList(); var buys3 = v3w.Select(w => w.Good904).ToList();
    Check($"W7 A7: DALA_904 Good Food is bought at most twice per run on v4 (max {buys4.Max()}, {buys4.Count(b => b == 2)} runs at 2; acts-v3h today max {buys3.Max()}, {buys3.Count(b => b > 2)} runs past 2)",
        buys4.Max() <= 2 && buys4.Any(b => b == 2));
    // A9
    var tav4 = v4w.SelectMany(w => w.TavernOffers).ToList(); var tav3 = v3w.SelectMany(w => w.TavernOffers).ToList();
    bool Heist(string id) => id.StartsWith("DALA_9", StringComparison.Ordinal);
    bool Tombs(string id) => id.StartsWith("ULDA_", StringComparison.Ordinal);
    int h4 = tav4.Count(Heist), t4 = tav4.Count(Tombs), h3 = tav3.Count(Heist), t3 = tav3.Count(Tombs);
    Check("W8 every tavern offer is a Heist (DALA_9xx) or Tombs (ULDA_6xx, ULDA_038) option", tav4.All(id => Heist(id) || Tombs(id)) && tav3.All(id => Heist(id) || Tombs(id)));
    Measure("A9", "tavern offer share Heist : Tombs (pooled v4)", $"{Pct(h4, h4 + t4)} : {Pct(t4, h4 + t4)} ({h4 + t4} offers; acts-v3h today {Pct(h3, h3 + t3)} : {Pct(t3, h3 + t3)})",
        "60-68 : 32-40", 100.0 * h4 / (h4 + t4) >= 60 && 100.0 * h4 / (h4 + t4) <= 68);
    Note("A9 offers per option (v4): " + string.Join(" ", tav4.GroupBy(x => x).OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => g.Key + "=" + g.Count())));
    // A12
    foreach (var g in v4w.GroupBy(w => w.Origin).OrderBy(g => g.Key, StringComparer.Ordinal))
        Measure("A12", "rewards per run, " + g.Key + " (" + g.Count() + " v4 runs): treasures / teammates / shrine upgrades / signatures / banked gold / boss rewards",
            $"{F2(g.Average(w => w.TreasurePicks))} / {F2(g.Average(w => w.Teammates))} / {F2(g.Average(w => w.ShrineUpgrades))} / {F2(g.Average(w => w.Signatures))} / {F2(g.Average(w => w.BankedEarned))} / {F2(g.Average(w => w.Rewards))}",
            "reported (input to B11)", null);
    // A13
    foreach (string cls in new[] { "DEATHKNIGHT", "DEMONHUNTER" })
    {
        var cw = v4w.Where(w => w.Row == cls).Select(w => w.BundleEntries.Count).ToList();
        var c3 = v3w.Where(w => w.Row == cls).Select(w => w.BundleEntries.Count).ToList();
        Measure("A13", cls + ": distinct catalog entries offered per run (" + cw.Count + " v4 runs), median / min / max",
            $"{Q(cw, 0.5)} / {(cw.Count > 0 ? cw.Min() : 0)} / {(cw.Count > 0 ? cw.Max() : 0)} (acts-v3h today {Q(c3, 0.5)}){(cw.Count > 0 && cw.Min() < 10 ? "; follow-up: below 10" : "")}",
            "reported; follow-up if < 10", null);
    }
    Check("W9 A13: the Death Knight and Demon Hunter class rows are in the run set", v4w.Count(w => w.Row == "DEATHKNIGHT") > 0 && v4w.Count(w => w.Row == "DEMONHUNTER") > 0);
});

// ============================================================ C. A4: treasure-draft candidates per loadout
Guard("C", () =>
{
    int Candidates(DungeonRunState s)
    {
        var cls = run.RunClass(s);
        return data.Treasures.Count(t => t.Offered && !t.Legacy && !t.Signature && DungeonRun.TreasureInScope(t, cls, s) && DungeonRun.TreasureOfferedOn(t, s));
    }
    var rows = new List<(string Origin, string Row, string Deck, int V3, int V4)>();
    foreach (var (row, deck) in loadouts)
        rows.Add((Origin(row), row.Id, deck, Candidates(New(row, deck, 31337, "acts-v3h")), Candidates(New(row, deck, 31337, "acts-v4h"))));
    foreach (var g in rows.GroupBy(r => r.Origin).OrderBy(g => g.Key, StringComparer.Ordinal))
        Note($"A4 {g.Key} ({g.Count()} loadouts): candidates acts-v3h {g.Min(r => r.V3)}-{g.Max(r => r.V3)}, stamp 4 {g.Min(r => r.V4)}-{g.Max(r => r.V4)}");
    var low = rows.Where(r => r.V4 < 90).ToList();
    Check($"C1 A4: every loadout drafts from at least 90 treasure candidates on stamp 4 (min {rows.Min(r => r.V4)}, max {rows.Max(r => r.V4)}; acts-v3h today {rows.Min(r => r.V3)}-{rows.Max(r => r.V3)})",
        rows.Count == 116 && low.Count == 0, string.Join(" ", low.Take(6).Select(r => r.Row + "/" + r.Deck + "=" + r.V4)));
    Check("C2 A4: on acts-v3h the candidates are the released scoped ones (no loadout gains a shared treasure there): stamp 4 never offers fewer",
        rows.All(r => r.V4 >= r.V3) && rows.Any(r => r.V4 > r.V3));
});

// ============================================================ G. A8: charm_seedling's growth
Guard("G", () =>
{
    // A run of a class row; at its first treasure draft charm_seedling is put on offer (in place of the first offered treasure)
    // and picked; the run is then walked to its end, every fight won.
    (int Pick, int Full) Grow(string schedule)
    {
        var cls = data.FindClass("MAGE");
        var s = New(cls, cls.Decks[0], 4711, schedule);
        bool placed = false;
        var w = WalkRun(s, (st, wk) =>
        {
            if (placed || st.Phase != DungeonRunPhase.TreasureDraft || st.OpenDraft.Kind != "treasure" || run.CurrentNode(st).PassiveOnly) return;
            st.OpenDraft.Offered[0] = "charm_seedling";
            st.OpenDraft.Offered.Sort(StringComparer.Ordinal);
            placed = true;
        });
        return (w.CharmPick, w.CharmFull);
    }
    var v4 = Grow("acts-v4h"); var v3 = Grow("acts-v3h");
    Note($"A8 charm_seedling ({string.Join(" > ", Chain("charm_seedling"))}): acts-v4h picked on floor {v4.Pick}, full growth on floor {v4.Full}; acts-v3h picked on floor {v3.Pick}, full on floor {v3.Full}");
    Check($"G1 A8: charm_seedling takes 8 floors from pick to full growth on acts-v4h (upgradeEveryFloors 4): {v4.Full - v4.Pick}", v4.Pick > 0 && v4.Full - v4.Pick == 8);
    Check($"G2 A8: and 4 floors on acts-v3h, as released (rules.upgradeEveryFloors {data.Rules.UpgradeEveryFloors}): {v3.Full - v3.Pick}", v3.Pick > 0 && v3.Full - v3.Pick == 4);
    // The walks of the run set that picked charm_seedling early enough to grow it fully by floor 15 (act III's major).
    var natural = walks.Where(w => w.CharmPick > 0 && w.CharmPick + (w.Tuned ? 8 : 4) <= 15).ToList();
    Check($"G3 A8: every walk of the run set that picked charm_seedling by floor {15 - 8} (v4) or {15 - 4} (acts-v3h) grew it fully in 8 floors on v4, 4 on acts-v3h ({natural.Count(w => w.Tuned)} + {natural.Count(w => !w.Tuned)} walks)",
        natural.Count(w => w.Tuned) > 0 && natural.Count(w => !w.Tuned) > 0 && natural.All(w => w.CharmFull - w.CharmPick == (w.Tuned ? 8 : 4)),
        string.Join(" ", natural.Where(w => w.CharmFull - w.CharmPick != (w.Tuned ? 8 : 4)).Take(4).Select(w => w.Schedule + ":" + w.CharmPick + "->" + w.CharmFull)));
});
List<string> Chain(string id) { var l = new List<string> { id }; while (data.FindTreasure(l[l.Count - 1])?.UpgradesTo is string n && l.Count < 8) l.Add(n); return l; }

// ============================================================ K. the mechanisms on constructed states
Guard("K", () =>
{
    var cls = data.FindClass("MAGE");
    // A new MAGE run (no board shrine: no teammate or shrine-upgrade drafts) walked to the PreMatch of a global floor, or to its
    // first tavern; every fight won, scored picks.
    DungeonRunState Stop(string schedule, Func<DungeonRunState, bool> at, long seed = 5150)
    {
        var s = New(cls, cls.Decks[0], seed, schedule);
        WalkRun(s, stop: at);
        return at(s) ? s : throw new InvalidOperationException(schedule + ": the walk never reached the wanted state");
    }
    Func<DungeonRunState, bool> Floor(int floor) => st => st.Phase == DungeonRunPhase.PreMatch && st.Floor == floor;
    Func<DungeonRunState, bool> AtTavern = st => st.Phase == DungeonRunPhase.Tavern;
    void Fill(DungeonRunState s, int size) { string filler = s.Cards.First(c => c.Origin == DungeonRunDeck.OriginStarter).Id; while (s.Deck.Count < size) DungeonRunDeck.Add(s, filler, DungeonRunDeck.OriginStarter); }
    void Win(DungeonRunState s) { run.BeginMatch(s); run.RecordOutcome(s, scratch, DungeonRunOutcome.Win); }
    int Bundles(DungeonRunState s, int floor) => s.Drafts.Count(d => d.Kind == "bundle" && d.Floor == floor);

    // K1-K2 (A3, D5): act I floor 2 (no treasure draft; a bundle of 3 takes; act I's tavern follows floor 3).
    foreach (string schedule in new[] { "acts-v4h", "acts-v3h" })
    {
        bool v4 = schedule == "acts-v4h";
        var s = Stop(schedule, Floor(2));
        Fill(s, 48);
        int banked = s.BankedGold;
        Win(s);
        Check($"K1 {schedule}: a won fight whose bundle draft has no room (48 cards + 3 takes > deckMax 50) opens no draft, keeps the deck at 48 and banks {(v4 ? "1 gold" : "nothing")}",
            Bundles(s, 2) == 0 && s.Phase == DungeonRunPhase.PreMatch && s.Floor == 3 && s.Deck.Count == 48 && s.BankedGold == banked + (v4 ? 1 : 0),
            $"phase {s.Phase} floor {s.Floor} banked {banked} -> {s.BankedGold} deck {s.Deck.Count}");
        int bankedThen = s.BankedGold;
        var w = WalkRun(s, stop: AtTavern);
        int expected = v4 ? bankedThen + w.RoomSkips : 0;
        Check(v4 ? $"K2 {schedule}: the next tavern adds the banked gold ({expected}: floor 2's skip{(w.RoomSkips > 0 ? " and floor 3's" : "")}) to the act's gold and BankedGold is 0 after it opens"
                : $"K2 {schedule}: the next tavern has the act's gold only (nothing was banked)",
            s.Phase == DungeonRunPhase.Tavern && s.Tavern.Gold == run.TavernGold(s) + expected && s.BankedGold == 0 && s.Deck.Count <= 50,
            $"gold {s.Tavern?.Gold}, act gold {run.TavernGold(s)}, banked {s.BankedGold}, room skips on floor 3 {w.RoomSkips}");
    }

    // K3 (D5 variant (b)): which won fights open a bundle draft. acts-v4h: act I keeps a bundle after its treasure floors
    // (floor 3: 3 takes); act II (floors 6-10; floor 6 the passive draft, floor 7 a treasure floor, floor 10 the major) opens
    // none after floors 6, 7 and 10 and a 2-take one after floor 8. acts-v3h: a bundle after floor 7 as released.
    (bool Bundle, int Takes) After(string schedule, int floor)
    {
        var s = Stop(schedule, Floor(floor));
        Win(s);
        while (s.Phase == DungeonRunPhase.TreasureDraft) run.PickTreasure(s, s.OpenDraft.Kind == "treasure" ? BestTreasure(s, s.OpenDraft.Offered) : Best(s, s.OpenDraft.Offered, 2));
        bool open = s.Phase == DungeonRunPhase.BundleDraft && s.OpenDraft.Floor == floor;
        return (open, open ? (s.OpenDraft.Takes == 0 ? 3 : s.OpenDraft.Takes) : 0);
    }
    var cases = new[] { ("acts-v4h", 3, true, 3), ("acts-v4h", 6, false, 0), ("acts-v4h", 7, false, 0), ("acts-v4h", 8, true, 2), ("acts-v4h", 10, false, 0),
        ("acts-v4h", 12, false, 0), ("acts-v4h", 13, true, 2), ("acts-v3h", 7, true, 2), ("acts-v3h", 10, true, 2) };
    var got = cases.Select(c => (c, After(c.Item1, c.Item2))).ToList();
    Check("K3 D5 (b): acts-v4h opens no bundle after an act II or III treasure floor or major (floors 6, 7, 10, 12), keeps act I's (floor 3, 3 takes) and the others (2 takes); acts-v3h as released",
        got.All(x => x.Item2.Bundle == x.c.Item3 && x.Item2.Takes == x.c.Item4), string.Join(" ", got.Select(x => x.c.Item1 + ":" + x.c.Item2 + "=" + (x.Item2.Bundle ? "bundle/" + x.Item2.Takes : "none"))));

    // K4 (A7, D4): DALA_904 Good Food after two buys (HealthBonus 10), and the HealthBonus a fight reads.
    var good = data.Tavern.Option("DALA_904");
    foreach (string schedule in new[] { "acts-v4h", "acts-v3h" })
    {
        bool v4 = schedule == "acts-v4h";
        var s = Stop(schedule, AtTavern);
        s.HealthBonus = 5;
        bool once = DungeonRunTavernNode.Applicable(run, s, good);
        s.HealthBonus = 10;
        bool twice = DungeonRunTavernNode.Applicable(run, s, good);
        s.HealthBonus = 30;
        int knob = run.KnobsFor(s, run.CurrentBoss(s)).HealthBonus;
        Check($"K4 {schedule}: Good Food can be bought after one buy; after two (HealthBonus 10) it is {(v4 ? "refused (maxPerRun 2)" : "still offered (no cap, as released)")}; a save holding 30 fights with {(v4 ? "the cap, 10" : "30")}",
            once && twice == !v4 && knob == (v4 ? 10 : 30) && (v4 ? run.HealthBonusCap() == 10 : true), $"once {once} twice {twice} knob {knob} cap {run.HealthBonusCap()}");
    }

    // K5 (D9): Damph's reward (GILA_BOSS_43t) with room and without.
    var damph = data.Boss("gila_43_damph");
    Check("K5 the reward row: gila_43_damph's v4 reward is GILA_BOSS_43t, a reward row of run-v1.json (offered false, runs v4)",
        damph?.V4?.Reward == "GILA_BOSS_43t" && data.FindTreasure("GILA_BOSS_43t") is DungeonRunTreasure rt && rt.Reward && !rt.Offered && rt.Runs == DungeonRunTreasure.RunsV4);
    foreach (string schedule in new[] { "acts-v4h", "acts-v3h" })
    {
        bool v4 = schedule == "acts-v4h";
        var roomy = Stop(schedule, Floor(2));
        roomy.BossSequence[1] = "gila_43_damph";
        int size = roomy.Deck.Count, banked = roomy.BankedGold;
        Win(roomy);
        bool granted = roomy.ActiveTreasures.Contains("GILA_BOSS_43t") && roomy.Deck.Count(c => c == "GILA_BOSS_43t") == 1;
        var full = Stop(schedule, Floor(2));
        full.BossSequence[1] = "gila_43_damph";
        Fill(full, 50);
        int fullBanked = full.BankedGold;
        Win(full);
        bool kept = !full.ActiveTreasures.Contains("GILA_BOSS_43t") && full.Deck.Count == 50;
        Check($"K6 {schedule}: a won fight against Damph {(v4 ? "grants GILA_BOSS_43t as a deck card with room, and at deckMax banks 1 gold instead (plus 1 for the skipped bundle)" : "grants nothing (released)")}",
            v4 ? granted && roomy.BankedGold == banked && kept && full.BankedGold == fullBanked + 2 : !granted && kept && full.BankedGold == 0,
            $"granted {granted} (deck {size} -> {roomy.Deck.Count}), at deckMax kept {kept}, banked {fullBanked} -> {full.BankedGold}");
    }
});

// ============================================================ D. A10 and A11: the draw on the default roll
Guard("D", () =>
{
    var rows = data.Classes.Where(c => c.Decks.Count > 0).ToList();
    string TierOf(DungeonRunBoss b, bool tuned) => b.IsEncounter ? "t" + ((tuned ? b.V4?.Tier : null) ?? b.Tier) : b.Final ? "final" : b.Challenge ? "challenge" : "band" + b.Band;
    // The rows each act slot of the schedules can draw, by tier group (finale rows are fixed, not drawn).
    (Dictionary<string, int> Met, List<int> Provenances, Dictionary<string, int> Schedules) Roll(int runs, long seed0, Func<int, string> schedule)
    {
        var met = new Dictionary<string, int>(StringComparer.Ordinal);
        var provenances = new List<int>();
        var schedules = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < runs; i++)
        {
            var row = rows[i % rows.Count];
            var s = run.NewRun(all, row.Id, row.Decks[0], row.Powers[0], seed0 + i, schedule: schedule(i), signatureId: run.SignatureOffer(all, row, seed0 + i).FirstOrDefault());
            schedules[s.Schedule] = schedules.GetValueOrDefault(s.Schedule) + 1;
            foreach (string id in s.BossSequence) met[id] = met.GetValueOrDefault(id) + 1;
            provenances.Add(s.BossSequence.Select(id => data.Boss(id).Provenance).Distinct(StringComparer.Ordinal).Count());
        }
        return (met, provenances, schedules);
    }
    string[] tiers = { "t1", "t2", "t3", "t4", "tmajor1", "tmajor2", "tmajor3" };
    // Per tier group of the drawn pools: rows, meetings, max/min (a row never met makes the ratio infinite).
    List<(string Tier, int Rows, int Never, int Max, int Min, double Ratio)> Ratios(Dictionary<string, int> met, bool tuned) =>
        tiers.Select(t =>
        {
            var group = data.Bosses.Where(b => b.IsEncounter && TierOf(b, tuned) == t && (b.Unlock == null || run.IsUnlocked(all, b.Unlock))).Select(b => met.GetValueOrDefault(b.Id)).ToList();
            int mx = group.DefaultIfEmpty(0).Max(), mn = group.DefaultIfEmpty(0).Min();
            return (t, group.Count, group.Count(n => n == 0), mx, mn, mn == 0 ? double.PositiveInfinity : (double)mx / mn);
        }).ToList();
    string Show(List<(string Tier, int Rows, int Never, int Max, int Min, double Ratio)> r) =>
        string.Join(", ", r.Select(x => $"{x.Tier} {x.Rows} rows {x.Min}-{x.Max} = {(double.IsInfinity(x.Ratio) ? "inf" : F2(x.Ratio))}"));
    var v4 = Roll(600, 960000, i => null);
    // Today's default roll was acts-v3h or acts-v3t; a row whose own character is acts-v3t's finale (DALA_Tekahn) always took acts-v3h.
    var v3t = data.Schedules.Find("acts-v3t");
    var v3 = Roll(600, 960000, i => i % 2 == 0 || DungeonRun.FinaleExcludes(v3t, rows[i % rows.Count]) ? "acts-v3h" : "acts-v3t");
    var big = Roll(6000, 970000, i => null);
    var r4 = Ratios(v4.Met, true); var r3 = Ratios(v3.Met, false); var rb = Ratios(big.Met, true);
    Note("A10 default roll (600 runs, " + string.Join(" ", v4.Schedules.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => p.Key + " " + p.Value)) + "): " + Show(r4));
    Note("A10 acts-v3h / acts-v3t today (600 runs): " + Show(r3));
    Note("A10 default roll, 6000 runs (sampling noise reduced): " + Show(rb));
    double worst4 = r4.Where(x => x.Rows >= 2).Max(x => x.Ratio), worst3 = r3.Where(x => x.Rows >= 2).Max(x => x.Ratio), worstB = rb.Where(x => x.Rows >= 2).Max(x => x.Ratio);
    double prov4 = Median(v4.Provenances.Select(x => (double)x)), prov3 = Median(v3.Provenances.Select(x => (double)x));
    Measure("A10", "per-row meeting ratio max/min within each encounter tier pool (600 default-roll runs), worst pool; median distinct provenances per run",
        $"{(double.IsInfinity(worst4) ? "inf" : F2(worst4))}; {F1(prov4)} (6000 runs: {(double.IsInfinity(worstB) ? "inf" : F2(worstB))}; acts-v3h/v3t today: {(double.IsInfinity(worst3) ? "inf" : F2(worst3))}; {F1(prov3)})",
        "<= 2.5; >= 5", worst4 <= 2.5 && prov4 >= 5);
    Check("D1 the 600 runs follow both v4 defaults (acts-v4h and acts-v4t) and draw with provenance-mix-v2", v4.Schedules.Count == 2 && v4.Schedules.Keys.All(k => k == "acts-v4h" || k == "acts-v4t")
        && v4.Schedules.Keys.All(k => data.Schedules.Find(k).MixesProvenanceV2), string.Join(" ", v4.Schedules.Keys));
    // A11: every encounter row is met at least once on the v4 defaults (c-enc-25 R4's rule; the finale rows are fixed).
    var never = data.Encounters.Where(b => !v4.Met.ContainsKey(b.Id)).Select(b => b.Id).ToList();
    Check($"D2 A11: every encounter row is met at least once in the 600 default-roll runs ({data.Encounters.Count()} rows)", never.Count == 0, string.Join(" ", never));
});

Console.WriteLine($"RESULT {passes} passed, {failures} failed");

// One run's record.
internal sealed class Walk
{
    internal string Schedule, Origin, Row;
    internal bool Victory, Tuned;
    internal int Steps, MaxDeck, DeckMax, FinaleDeck = -1, RoomSkips, OtherSkips, BankedEarned, Good904, TreasurePicks, Teammates, ShrineUpgrades, Signatures, Rewards;
    internal int BankedBad, TavernGoldBad;
    internal string FirstBad;
    internal List<(int Act, string Id, bool NonPassive)> TreasureOffers = new List<(int, string, bool)>();
    internal List<List<string>> NonPassiveOffers = new List<List<string>>();
    internal List<string> TavernOffers = new List<string>();
    internal HashSet<string> BundleEntries = new HashSet<string>(StringComparer.Ordinal);
    internal int CharmPick = -1, CharmFull = -1;
}
