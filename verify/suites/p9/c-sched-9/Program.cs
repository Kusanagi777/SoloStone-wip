using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using OfflinePoolPolicy = SabberStoneCore.Model.OfflinePoolPolicy;

// c-sched-9: step 9 tripwire F1 (handoff/phase9/plan.md D1 and section 1 F): the released schedules (acts-v1, acts-v2, act1,
// acts-v3h, acts-v3t) and every value they read stay as they were released, on the shipped data (encounters-v1.json loaded).
// The reference is the tree before step 9 (f62275b, the close-out tip), recorded by verify/tools/gen/p9/record_sched9.sh into
// verify/fixtures/p9/sched9-f62275b.txt: this same program, built against that tree's sources and run on its data, writes
// every parsed object's field set and hash ("record <file>").
//   S  the stamps: StampVersion 3 on acts-v3h and acts-v3t (now an explicit "version": 3), 2 on acts-v1, acts-v2 and act1; a
//      new run on a released schedule is stamped with it and is not Tuned, a run on acts-v4h / acts-v4t is (stamp 4)
//   H  the shapes: ReleasedShapes[1..3] unchanged, [4] adds acts-v4h / acts-v4t, every shipped schedule's ShapeDigest equals
//      its pin
//   J  the raw JSON of each released schedule object, minus "version", equals the reference (canonical form: sorted keys)
//   P  the parsed objects (a reflection dump of every field): each released schedule (with its flattened nodes), the pool
//      sets and the modifiers, every tavern option and the tavern table, every treasure, every boss row (encounter rows minus
//      their v4 block). A field the reference had must hold the reference's value; a field step 9 added is either neutral on
//      the released object (its default: the key is absent) or read only through DungeonRun.Tuned, or one of lane R's
//      documented correctness fixes (each named below with its reason); any other field fails as unclassified
//   T  the readers of the Tuned-only fields (tavern weightByAct and maxPerRun, treasure shared) on new runs of the released act
//      schedules: they read the released values (weight, no cap, scope only)
// "record <file>" writes the reference (used only by record_sched9.sh, on the f62275b export).
var data = Sched9.Load();
string[] released = Sched9.Released;
string R(string f) => Sched9.R(f);
string Strip(string line) => Sched9.Strip(line);
string Hash(IEnumerable<string> lines) => Sched9.Hash(lines);
string Kind(string key) => Sched9.Kind(key);
List<string> DumpOf(object o) => Sched9.DumpOf(o);
if (args.Length == 2 && args[0] == "record") { Sched9.Record(args[1]); return; }
var objects = Sched9.Objects(data);
var raw = Sched9.Raw(R("dungeon-run/schedule-v1.json"));

// ------------------------------------------------------------ the checks
int failures = 0, passes = 0;
void Check(string name, bool ok, string detail = "")
{
    if (ok) passes++; else failures++;
    Console.WriteLine((ok ? "PASS " : "FAIL ") + name + (detail.Length > 0 ? "  (" + detail + ")" : ""));
}
void Note(string text) => Console.WriteLine("NOTE " + text);
void Guard(string name, Action body)
{
    try { body(); }
    catch (Exception e) { failures++; Console.WriteLine("FAIL " + name + " threw " + e.GetType().Name + ": " + e.Message.Split('\n')[0] + " @ " + (e.StackTrace ?? "").Split('\n').FirstOrDefault()?.Trim()); }
}
string refPath = args.Length > 0 ? args[0] : VerifyPaths.Fixture("p9/sched9-f62275b.txt");
var reference = File.ReadAllLines(refPath).Where(l => l.Length > 0 && !l.StartsWith("#", StringComparison.Ordinal)).Select(l => l.Split(' ')).ToList();
var refFields = reference.Where(x => x[0] == "field").GroupBy(x => x[1]).ToDictionary(g => g.Key, g => new HashSet<string>(g.Select(x => x[2]), StringComparer.Ordinal), StringComparer.Ordinal);
var refHash = reference.Where(x => x[0] == "hash").ToDictionary(x => x[1], x => x[2], StringComparer.Ordinal);
var refRaw = reference.Where(x => x[0] == "raw").ToDictionary(x => x[1], x => x[2], StringComparer.Ordinal);
var refStamp = reference.Where(x => x[0] == "stamp").ToDictionary(x => x[1], x => int.Parse(x[2], CultureInfo.InvariantCulture), StringComparer.Ordinal);
var refShape = reference.Where(x => x[0] == "shape").Select(x => (Version: int.Parse(x[1], CultureInfo.InvariantCulture), Id: x[2], Digest: x[3])).ToList();
Note($"reference {Path.GetFileName(refPath)}: {refHash.Count} objects, {refFields.Values.Sum(f => f.Count)} field paths, {refRaw.Count} raw schedules");

// The fields step 9 added, by kind: how each one keeps the released objects as released. "neutral": on a released object it
// must hold its default (the key is absent from the file); "tuned": read only through DungeonRun.Tuned (stamp 4), any value;
// "fix": a lane R correctness fix that applies to every schedule (handoff/phase9/implementation.md section 5), any value.
var added = new Dictionary<string, (string Kind, string Why)>(StringComparer.Ordinal)
{
    ["schedule .DrawAlphaPermille"] = ("neutral", "drawTuning: read by the provenance-mix-v2 draw only (stamp 4)"),
    ["schedule .DrawPenaltyPermille"] = ("neutral", "drawTuning: read by the provenance-mix-v2 draw only (stamp 4)"),
    ["schedule .LedgerMajorChance"] = ("neutral", "drawTuning: read by the provenance-mix-v2 draw only (stamp 4)"),
    ["schedule .MaxTrlPerAct"] = ("neutral", "drawTuning: read by the provenance-mix-v2 draw only (stamp 4)"),
    ["schedule .UpgradeEveryFloors"] = ("neutral", "A8: DungeonRun.UpgradeEveryFloors reads it through Tuned"),
    ["schedule .OffsetCap"] = ("neutral", "D4 lever 3: CapKnobs reads it through Tuned"),
    ["schedule .V4Keys"] = ("neutral", "the step-9 keys the row named (the validator refuses them below stamp 4)"),
    ["schedule .Acts[].TreasureRareWeight"] = ("neutral", "D6 act rarity: TunedTreasureOffer (Tuned)"),
    ["schedule .Acts[].TreasureCommonWeight"] = ("neutral", "D6 act rarity: TunedTreasureOffer (Tuned)"),
    ["schedule .Acts[].BundleAfterTreasure"] = ("neutral", "D5 (b): NoBundleAfter reads it through Tuned"),
    ["schedule .Acts[].TurnCap"] = ("neutral", "D11: the finale's turn cap (PlanMatchEnds, Tuned)"),
    ["schedule .Acts[].FormRatchet"] = ("neutral", "D11: the finale's form ratchet (PlanMatchEnds, Tuned)"),
    ["schedule .Acts[].BossDeck"] = ("neutral", "owner 2026-11-20: the boss's starting deck size per floor (SizeBossDeck, Tuned)"),
    ["tavern .WeightByAct"] = ("tuned", "D4/D5: OfferWeight reads it only when Tuned"),
    ["tavern .MaxPerRun"] = ("tuned", "D4: RunCapReached reads it only when Tuned"),
    ["treasure .Shared"] = ("tuned", "D6: TreasureInScope reads it only when Tuned"),
    ["treasure .Reward"] = ("neutral", "D9: a reward row is a new runs-v4 row; every released row keeps false"),
    ["boss .V4"] = ("tuned", "the row's v4 block: read only through Tuned (MemberTuned, CapKnobs, NeutraliseKnobs, GrantEncounterReward)"),
    ["boss .V4.Tier"] = ("tuned", "v4 block"), ["boss .V4.HealthOffset"] = ("tuned", "v4 block"), ["boss .V4.IncompatibleModifiers"] = ("tuned", "v4 block"),
    ["boss .V4.Reward"] = ("tuned", "v4 block"), ["boss .V4.UnknownKeys"] = ("tuned", "v4 block"),
    ["boss .ModifierReasons{#}"] = ("tuned", "C7: data only; the validator reads it for rows a stamp-4 schedule reaches"),
    ["boss .ModifierReasons{}"] = ("tuned", "C7: data only"),
    ["boss .IncompatibleTreasures"] = ("fix", "lane R: ULDA_720 Battle Scars x LOOTA_800 and boss-held treasure exclusions (RuleExcludesTreasure), every schedule with rules"),
};
// Lane R's new boss flags (correctness fixes on every schedule with anomaly or twist nodes): a row's Flags line is compared with
// these taken out; the rest of the line must equal the reference.
string[] newFlags = { "attackRedirect", "charge", "windfury", "statSwap", "fatigue", "handAdd", "manaIdentity", "room" };
// Value changes of reference fields that step 9 made on purpose and that no released run reads as play: (object, dump line now,
// the reference's line, cause). Each is put back before hashing, so everything else of the object must still equal the reference.
var attributed = new List<(string Key, string Now, string Then, string Cause)>
{
    ("boss:gila_48_infinite_toki", ".Source.Substitutions[1].Reason=admission:nemesis-rejected-restart", ".Source.Substitutions[1].Reason=unscripted",
        "lane X: GILA_BOSS_48t Alternate Timeline is scripted now (the restart path) but stays out of Toki's deck until the stage-4 gate, so the deck's "
        + "substitution reason moved from \"unscripted\" to \"admission:nemesis-rejected-restart\"; the deck's cards are the reference's"),
};
string FlagsOut(string line)
{
    if (!line.StartsWith(".Flags=[", StringComparison.Ordinal)) return line;
    var flags = line.Substring(8, line.Length - 9).Split('|', StringSplitOptions.RemoveEmptyEntries).Where(f => !newFlags.Contains(f));
    return ".Flags=[" + string.Join("|", flags) + "]";
}

// ============================================================ S. the stamps
Guard("S", () =>
{
    var stamps = released.ToDictionary(id => id, id => data.Schedules.Find(id)?.StampVersion ?? -1, StringComparer.Ordinal);
    Check("S1 StampVersion 3 on acts-v3h and acts-v3t, 2 on acts-v1, acts-v2 and act1 (the file is scheduleVersion " + data.Schedules.ScheduleVersion + ")",
        stamps["acts-v3h"] == 3 && stamps["acts-v3t"] == 3 && stamps["acts-v1"] == 2 && stamps["acts-v2"] == 2 && stamps["act1"] == 2 && data.Schedules.ScheduleVersion == 4,
        string.Join(" ", stamps.Select(p => p.Key + "=" + p.Value)));
    Check("S2 every released stamp equals the reference's (the tree before step 9, file version 3, where the v3 rows had no explicit version)",
        released.All(id => refStamp.TryGetValue(id, out int v) && v == stamps[id]), string.Join(" ", refStamp.Select(p => p.Key + "=" + p.Value)));
    using var doc = JsonDocument.Parse(R("dungeon-run/schedule-v1.json"));
    var rows = doc.RootElement.GetProperty("schedules");
    Check("S3 acts-v3h and acts-v3t carry an explicit \"version\": 3 (without it a scheduleVersion-4 file would stamp them 4)",
        new[] { "acts-v3h", "acts-v3t" }.All(id => rows.GetProperty(id).TryGetProperty("version", out var v) && v.GetInt32() == 3));
    var run = new DungeonRun(data);
    var profile = new DungeonRunProfile(); profile.Unlocked.AddRange(data.Unlocks.Keys);
    var cls = data.FindClass("MAGE");
    string[] tuned = data.Schedules.Schedules.Values.Where(s => s.StampVersion >= 4).Select(s => s.Id).OrderBy(x => x, StringComparer.Ordinal).ToArray();
    var states = released.Concat(tuned).Distinct().ToDictionary(id => id, id => run.NewRun(profile, cls.Id, cls.Decks[0], cls.Powers[0], 4242, schedule: id), StringComparer.Ordinal);
    Check("S4 a new run on each released schedule is stamped with its StampVersion and is not Tuned", released.All(id => states[id].ScheduleVersion == stamps[id] && !DungeonRun.Tuned(states[id])),
        string.Join(" ", released.Select(id => id + "=" + states[id].ScheduleVersion + (DungeonRun.Tuned(states[id]) ? " tuned" : ""))));
    Check("S5 a new run on acts-v4h and acts-v4t (the only stamp-4 schedules) is stamped 4 and Tuned", tuned.SequenceEqual(new[] { "acts-v4h", "acts-v4t" })
        && tuned.All(id => states[id].ScheduleVersion == 4 && DungeonRun.Tuned(states[id])), string.Join(" ", tuned));
    var legacy = run.NewRun(profile, cls.Id, cls.Decks[0], cls.Powers[0], 4242, schedule: DungeonRunSchedules.LegacyId);
    Check("S6 a legacy run (ScheduleVersion 0) is not Tuned", legacy.ScheduleVersion == 0 && !legacy.IsActRun && !DungeonRun.Tuned(legacy));
});

// ============================================================ H. the shapes
Guard("H", () =>
{
    var now = DungeonRunSchedules.ReleasedShapes.SelectMany(v => v.Value.Select(s => (Version: v.Key, Id: s.Key, Digest: s.Value))).ToList();
    var before = refShape.Select(x => x.Version).Distinct().ToList();
    Check("H1 ReleasedShapes for every version the reference shipped (" + string.Join(",", before) + ") is unchanged",
        before.All(v => refShape.Where(x => x.Version == v).OrderBy(x => x.Id, StringComparer.Ordinal).SequenceEqual(now.Where(x => x.Version == v).OrderBy(x => x.Id, StringComparer.Ordinal))),
        string.Join(" ", now.Where(x => before.Contains(x.Version)).Except(refShape).Select(x => x.Version + ":" + x.Id + "=" + x.Digest)));
    var v4 = now.Where(x => x.Version == 4).ToList();
    var v3 = now.Where(x => x.Version == 3).ToList();
    Check("H2 ReleasedShapes[4] is version 3's five schedules unchanged plus acts-v4h and acts-v4t (whose shapes equal acts-v3h's and acts-v3t's, D2)",
        v3.All(x => v4.Any(y => y.Id == x.Id && y.Digest == x.Digest)) && v4.Count == v3.Count + 2
        && v4.Single(x => x.Id == "acts-v4h").Digest == v3.Single(x => x.Id == "acts-v3h").Digest && v4.Single(x => x.Id == "acts-v4t").Digest == v3.Single(x => x.Id == "acts-v3t").Digest,
        string.Join(" ", v4.Select(x => x.Id + "=" + x.Digest)));
    var shipped = data.Schedules.Schedules.Values.OrderBy(s => s.Id, StringComparer.Ordinal).ToList();
    var bad = shipped.Where(s => !DungeonRunSchedules.ReleasedShapes.TryGetValue(data.Schedules.ScheduleVersion, out var pins) || !pins.TryGetValue(s.Id, out var d) || d != DungeonRunSchedules.ShapeDigest(s)).ToList();
    Check("H3 every shipped schedule's ShapeDigest equals its pin for the file's scheduleVersion (" + shipped.Count + " schedules)", bad.Count == 0 && shipped.Count == 7,
        string.Join(" ", bad.Select(s => s.Id + "=" + DungeonRunSchedules.ShapeDigest(s))));
});

// ============================================================ J. the raw schedule objects
Guard("J", () =>
{
    var differ = released.Where(id => !raw.TryGetValue(id, out var h) || !refRaw.TryGetValue(id, out var r) || h != r).ToList();
    Check("J1 the raw JSON of each released schedule object, minus \"version\", equals the reference (" + released.Length + " schedules)", differ.Count == 0 && refRaw.Count == released.Length,
        string.Join(" ", differ.Select(id => id + " " + (raw.TryGetValue(id, out var h) ? h : "-") + " vs " + (refRaw.TryGetValue(id, out var r) ? r : "-"))));
});

// ============================================================ P. the parsed objects
Guard("P", () =>
{
    // Defaults of the step-9 schedule fields: the dump of a schedule with one default act (the values a row gets with the keys absent).
    var defaults = new DungeonRunSchedule(); defaults.Acts.Add(new DungeonRunAct());
    var defaultValue = DumpOf(defaults).GroupBy(Strip).ToDictionary(g => g.Key, g => g.First().Substring(g.First().IndexOf('=') + 1), StringComparer.Ordinal);
    var report = new Dictionary<string, List<string>>(StringComparer.Ordinal);
    var unclassified = new SortedSet<string>(StringComparer.Ordinal);
    var notNeutral = new List<string>();
    var addedSeen = new SortedSet<string>(StringComparer.Ordinal);
    var applied = new HashSet<string>(StringComparer.Ordinal);
    foreach (var p in objects)
    {
        string kind = Kind(p.Key);
        if (!refHash.ContainsKey(p.Key)) continue;
        var fields = refFields.TryGetValue(kind, out var fs) ? fs : new HashSet<string>(StringComparer.Ordinal);
        var kept = new List<string>();
        foreach (string line in p.Value)
        {
            string f = Strip(line);
            if (fields.Contains(f))
            {
                var fix = attributed.FirstOrDefault(x => x.Key == p.Key && x.Now == line);
                if (fix.Key != null) { applied.Add(fix.Key); kept.Add(fix.Then); continue; }
                kept.Add(kind == "boss" ? FlagsOut(line) : line);
                continue;
            }
            if (added.TryGetValue(kind + " " + f, out var a))
            {
                addedSeen.Add(kind + " " + f);
                if (a.Kind == "neutral")
                {
                    string value = line.Substring(line.IndexOf('=') + 1);
                    string want = kind == "schedule" ? (defaultValue.TryGetValue(f, out var d) ? d : "(no default)") : "false";
                    if (value != want) notNeutral.Add(p.Key + line + " (default " + want + ")");
                }
                continue;
            }
            unclassified.Add(kind + " " + f);
        }
        if (Hash(kept) != refHash[p.Key]) (report.TryGetValue(kind, out var l) ? l : report[kind] = new List<string>()).Add(p.Key.Substring(kind.Length + 1));
    }
    var missing = refHash.Keys.Where(k => !objects.ContainsKey(k)).ToList();
    var fresh = objects.Keys.Where(k => !refHash.ContainsKey(k)).ToList();
    string Moved(string kind) => report.TryGetValue(kind, out var l) ? l.Count + ": " + string.Join(" ", l.Take(12)) : "";
    int Count(string kind) => objects.Keys.Count(k => Kind(k) == kind && refHash.ContainsKey(k));
    Check("P1 every field of the parsed objects is known: a reference field, or a step-9 field classified as neutral, Tuned-only or a lane R fix",
        unclassified.Count == 0, string.Join(" | ", unclassified.Take(12)));
    foreach (var a in added.Where(a => addedSeen.Contains(a.Key)).GroupBy(a => a.Value.Kind).OrderBy(g => g.Key, StringComparer.Ordinal))
        Note("P step-9 fields, " + a.Key + ": " + string.Join(", ", a.Select(x => x.Key)));
    Check("P2 every neutral step-9 field holds its default on every released object (the key is absent from the released row)", notNeutral.Count == 0, string.Join(" | ", notNeutral.Take(8)));
    Check("P3 the five released schedules (with their flattened nodes) equal the reference field for field", Count("schedule") == 5 && Moved("schedule").Length == 0, Moved("schedule"));
    Check("P4 the pool sets and the " + Count("modifier") + " modifiers equal the reference", Count("poolsets") == 1 && Count("modifier") > 0 && Moved("poolsets").Length == 0 && Moved("modifier").Length == 0,
        Moved("poolsets") + " " + Moved("modifier"));
    Check("P5 the tavern table and its " + Count("tavern") + " options equal the reference in every v3-visible field (weightByAct and maxPerRun are read only when Tuned)",
        Count("tavern") > 20 && Moved("tavern").Length == 0 && Moved("tavern-table").Length == 0, Moved("tavern") + " " + Moved("tavern-table"));
    Check("P6 the " + Count("treasure") + " reference treasures equal the reference in every v3-visible field (\"shared\" is read only when Tuned)", Count("treasure") > 200 && Moved("treasure").Length == 0, Moved("treasure"));
    var freshTreasures = fresh.Where(k => Kind(k) == "treasure").Select(k => data.FindTreasure(k.Substring(9))).ToList();
    Check("P7 every treasure row step 9 added is a runs-v4 row (no released schedule offers it): " + string.Join(", ", freshTreasures.Select(t => t.Id)),
        freshTreasures.Count > 0 && freshTreasures.All(t => t.Runs == DungeonRunTreasure.RunsV4 && !t.OfferedOn(true, true, 3)), string.Join(" ", freshTreasures.Where(t => t.Runs != DungeonRunTreasure.RunsV4).Select(t => t.Id + ":" + t.Runs)));
    Check("P8 the " + Count("boss") + " boss rows equal the reference, minus their v4 block and lane R's fixes (the eight new flags, incompatibleTreasures, modifierReasons)",
        Count("boss") > 250 && Moved("boss").Length == 0, Moved("boss"));
    foreach (var x in attributed) Note("P attributed: " + x.Key + " " + x.Then + " -> " + x.Now.Substring(x.Now.IndexOf('=') + 1) + ": " + x.Cause);
    Check("P9 every attributed value change is present (" + attributed.Count + "): the list names no change the data does not make", attributed.All(x => applied.Contains(x.Key)),
        string.Join(" ", attributed.Where(x => !applied.Contains(x.Key)).Select(x => x.Key)));
    Check("P10 no reference object is gone, and step 9 added no schedule-visible object but the runs-v4 treasures", missing.Count == 0 && fresh.All(k => Kind(k) == "treasure"),
        "gone: " + string.Join(" ", missing.Take(8)) + "; new: " + string.Join(" ", fresh.Where(k => Kind(k) != "treasure").Take(8)));
});

// ============================================================ T. the Tuned-only fields are not read on a released run
// P classifies weightByAct, maxPerRun and shared as Tuned-only; T runs the readers on new runs of the released act schedules.
Guard("T", () =>
{
    var run = new DungeonRun(data);
    var profile = new DungeonRunProfile(); profile.Unlocked.AddRange(data.Unlocks.Keys);
    string[] actRuns = { "acts-v1", "acts-v2", "act1", "acts-v3h", "acts-v3t" };
    var rows = data.Classes.Where(c => c.Decks.Count > 0).ToList();
    var options = data.Tavern.Options.ToList();
    var weightBad = new List<string>(); var capBad = new List<string>(); var scopeBad = new List<string>();
    int states = 0, v4Differs = 0;
    foreach (string id in actRuns.Concat(new[] { "acts-v4h" }))
        foreach (var row in rows)
        {
            DungeonRunState st;
            try { st = run.NewRun(profile, row.Id, row.Decks[0], row.Powers[0], 4243, schedule: id); }
            catch (InvalidOperationException) { continue; } // a finale this row is excluded from
            bool v4 = id == "acts-v4h";
            if (!v4) states++;
            st.HealthBonus = 30;
            foreach (var o in options)
            {
                int w = DungeonRunTavernNode.OfferWeight(run, st, o);
                if (v4) { if (w != o.Weight) v4Differs++; continue; }
                if (w != o.Weight) weightBad.Add(id + ":" + o.Id + "=" + w);
                if (DungeonRunTavernNode.RunCapReached(st, o)) capBad.Add(id + ":" + o.Id);
            }
            if (v4) continue;
            var cls = run.RunClass(st);
            foreach (var t in data.Treasures)
                if (DungeonRun.TreasureInScope(t, cls, st) != t.Scope.Matches(cls.ClassSet, st.StarterDeck, st.Power)) scopeBad.Add(id + ":" + row.Id + ":" + t.Id);
        }
    Check($"T1 on new runs of the five released schedules ({states} states) every tavern option weighs its released \"weight\" (weightByAct unread); acts-v4h reads weightByAct ({v4Differs} option weights differ there)",
        states > 100 && weightBad.Count == 0 && v4Differs > 0, string.Join(" ", weightBad.Take(8)));
    Check("T2 and no option is at its maxPerRun on them (HealthBonus 30: maxPerRun unread)", capBad.Count == 0, string.Join(" ", capBad.Take(8)));
    Check("T3 and every treasure is in scope exactly where its scope matches (\"shared\" unread)", scopeBad.Count == 0, scopeBad.Count + ": " + string.Join(" ", scopeBad.Take(8)));
});

// ============================================================ B. boss Health and deck size per round (owner 2026-11-20)
// acts-v4h/v4t: the first boss starts at 10 Health and 15 cards and both grow every fight (bossHealth, bossDeck); a released
// schedule keeps its 30-card decks and its curve. Runs are walked by won fights (first offers, "none" at a modifier node).
Guard("B", () =>
{
    var run = new DungeonRun(data);
    var profile = new DungeonRunProfile(); profile.Unlocked.AddRange(data.Unlocks.Keys);
    var scratch = new DungeonRunProfile();
    // (floor, boss, plan Health, expected Health, plan deck, source deck, size) of every fight on floors 1-15.
    List<(int Floor, DungeonRunBoss Boss, int Health, int? Expected, int Deck, int Source, int? Size, string Cards)> Walk(string schedule, long seed)
    {
        var row = data.Class("MAGE");
        var s = run.NewRun(profile, row.Id, row.Decks[0], row.Powers[0], seed, schedule: schedule);
        var fights = new List<(int, DungeonRunBoss, int, int?, int, int, int?, string)>();
        for (int guard = 0; guard < 800 && s.Floor <= 15 && !s.Finished; guard++)
        {
            switch (s.Phase)
            {
                case DungeonRunPhase.PreMatch:
                    s.ActiveRules.RemoveAll(x => x.Kind != null);
                    var boss = run.CurrentBoss(s);
                    var plan = run.BuildPlan(s, scratch);
                    var act = run.CurrentAct(s);
                    var node = run.CurrentNode(s);
                    int? expected = boss.FixedHealth ? (int?)null : Math.Max(1, act.BossHealth[node.FloorInAct - 1] + run.HealthOffset(boss));
                    string deckId = boss.Deck;
                    fights.Add((s.Floor, boss, plan.BossHealth, expected, plan.BossDeck.Count, data.BossDecks[deckId].Count, run.BossDeckSize(s, boss), string.Join(",", plan.BossDeck.Select(c => c.Id))));
                    run.BeginMatch(s); run.RecordOutcome(s, scratch, DungeonRunOutcome.Win);
                    break;
                case DungeonRunPhase.TreasureDraft: run.PickTreasure(s, s.OpenDraft.Offered[0]); break;
                case DungeonRunPhase.Modifier:
                    run.PickModifier(s, s.ModifierOffer.Kind == DungeonRunGlobalRule.KindAnomaly || s.ModifierOffer.Offered.Contains(DungeonRunGlobalRule.None) ? DungeonRunGlobalRule.None : s.ModifierOffer.Offered[0]);
                    break;
                case DungeonRunPhase.Tavern: run.LeaveTavern(s); break;
                case DungeonRunPhase.BundleDraft:
                    if (s.OpenDraft.Takes == 0) { run.PickBundle(s, s.OpenDraft.Offered[0]); break; }
                    string bundle = s.OpenDraft.Choosing ?? s.OpenDraft.Offered[0];
                    var left = run.OfferCards(s.OpenDraft, bundle);
                    foreach (string kept in s.OpenDraft.Chosen) left.Remove(kept);
                    run.PickDraftCard(s, bundle, left[0]);
                    break;
                default: throw new Exception("walk stopped at " + s.Phase);
            }
        }
        return fights;
    }
    var v4 = new List<(string, (int Floor, DungeonRunBoss Boss, int Health, int? Expected, int Deck, int Source, int? Size, string Cards))>();
    foreach (string id in new[] { "acts-v4h", "acts-v4t" })
        for (long seed = 9300; seed < 9306; seed++)
            foreach (var f in Walk(id, seed)) v4.Add((id + "/" + seed, f));
    var first = v4.Where(x => x.Item2.Floor == 1).ToList();
    Check("B1 on acts-v4h and acts-v4t the first boss starts at 10 Health (plus its own offset) and 15 cards (" + first.Count + " runs)",
        first.Count == 12 && first.All(x => x.Item2.Expected == 10 + run.HealthOffset(x.Item2.Boss) && x.Item2.Health == x.Item2.Expected && x.Item2.Size == 15 && x.Item2.Deck == 15),
        string.Join(" ", first.Where(x => x.Item2.Deck != 15 || x.Item2.Health != x.Item2.Expected).Select(x => x.Item1 + ":" + x.Item2.Boss.Id + "=" + x.Item2.Health + "hp/" + x.Item2.Deck)));
    var sizeBad = v4.Where(x => x.Item2.Size is int n ? x.Item2.Deck != Math.Min(n, x.Item2.Source) || n != 14 + x.Item2.Floor : x.Item2.Deck != x.Item2.Source).ToList();
    Check("B2 every fight on floors 1-15 plays a " + "15-29 card deck, one more card each floor (identity-Health and mirror bosses keep their own) (" + v4.Count + " fights)",
        v4.Count >= 12 * 15 && sizeBad.Count == 0, string.Join(" ", sizeBad.Take(8).Select(x => x.Item1 + "@" + x.Item2.Floor + ":" + x.Item2.Boss.Id + "=" + x.Item2.Deck + "/" + x.Item2.Size)));
    var healthBad = v4.Where(x => x.Item2.Expected is int e && x.Item2.Health != e).ToList();
    var curve = data.Schedules.Schedules["acts-v4h"].Acts.Where(a => !a.IsFinale).SelectMany(a => a.BossHealth).ToList();
    Check("B3 every curve-Health fight plays its floor's Health, and the act curve rises every floor (" + string.Join(" ", curve) + ")",
        healthBad.Count == 0 && curve.First() == 10 && curve.Zip(curve.Skip(1), (a, b) => b > a).All(up => up),
        string.Join(" ", healthBad.Take(8).Select(x => x.Item1 + "@" + x.Item2.Floor + ":" + x.Item2.Boss.Id + "=" + x.Item2.Health + "/" + x.Item2.Expected)));
    var released = new[] { "acts-v3h", "acts-v2" }.SelectMany(id => Walk(id, 9300).Take(3).Select(f => (id, f))).ToList();
    Check("B4 a released schedule keeps whole boss decks and its own curve (acts-v3h and acts-v2 floors 1-3)",
        released.Count == 6 && released.All(x => x.f.Size == null && x.f.Deck == x.f.Source) && released.Where(x => x.f.Floor == 1).All(x => x.f.Expected == 30 + run.HealthOffset(x.f.Boss)),
        string.Join(" ", released.Select(x => x.id + "@" + x.f.Floor + "=" + x.f.Health + "hp/" + x.f.Deck)));
    var again = Walk("acts-v4h", 9300).Select(f => f.Cards).ToList();
    var seeds = v4.Where(x => x.Item2.Floor == 1 && x.Item1.StartsWith("acts-v4h/")).Select(x => x.Item2.Cards).Distinct().Count();
    Check("B5 the cut is seeded: the same run walked twice meets the same boss cards every floor (and 6 seeds give " + seeds + " different first decks)",
        again.Count > 0 && again.SequenceEqual(v4.Where(x => x.Item1 == "acts-v4h/9300").Select(x => x.Item2.Cards)));
});

// ============================================================ TP. the native portrait tray's pages (owner 2026-11-22)
// NativeDungeonRunPicker shows the run rows a page at a time (Dalaran's tray lays out at most 12 heroes) with Previous/Next
// plates; its pure parts are NativeDungeonRunText.TrayPageRows (which rows a page shows) and TrayPagerPlace (where the plates go).
Guard("TP", () =>
{
    var run = new DungeonRun(data);
    var profile = new DungeonRunProfile(); profile.Unlocked.AddRange(data.Unlocks.Keys);
    // The rows in the picker's order (BuildTrayRows: class by class, the class row then its named heroes, each id once). The
    // client's own filters (a known hero card with a GUEST_HERO record, a TAG_CLASS) are not applied here.
    var rows = new List<RunHeroOption>();
    foreach (var cls in DungeonRunCatalog.Classes(run, profile))
        foreach (var hero in DungeonRunCatalog.Heroes(run, profile, cls.Id))
            if (!rows.Any(r => r.Id == hero.Id)) rows.Add(hero);
    var flags = rows.Select(r => r.IsHero).ToList();
    const int cap = 12;
    // The slice the tray showed before paging (136bf1a FeedGuestHeroes): the class rows first, then the heroes, in row order.
    List<int> Before(IList<bool> f)
    {
        var kept = new HashSet<int>(Enumerable.Range(0, f.Count).OrderBy(i => f[i] ? 1 : 0).Take(cap));
        return Enumerable.Range(0, f.Count).Where(kept.Contains).ToList();
    }
    List<List<int>> PagesOf(IList<bool> f, Func<int, bool> laysOut = null) =>
        Enumerable.Range(0, NativeDungeonRunText.Pages(f.Count, cap)).Select(p => NativeDungeonRunText.TrayPageRows(f, cap, p, laysOut)).ToList();
    string Ids(IEnumerable<int> page) => string.Join(" ", page.Select(i => rows[i].Id));
    bool Partition(IList<bool> f, List<List<int>> pages) =>
        pages.SelectMany(p => p).OrderBy(i => i).SequenceEqual(Enumerable.Range(0, f.Count))
        && pages.All(p => p.Count > 0 && p.Count <= cap && p.SequenceEqual(p.OrderBy(i => i)));
    var real = PagesOf(flags);
    Note("TP data rows " + rows.Count + " (" + flags.Count(x => !x) + " class rows, " + flags.Count(x => x) + " named heroes), "
        + real.Count + " pages: " + string.Join(" / ", real.Select(Ids)));
    Check("TP1 on the shipped rows page 1 is the 12 the tray showed before paging, and every row is on exactly one page in row order",
        rows.Count > cap && real[0].SequenceEqual(Before(flags)) && Partition(flags, real) && real.Count == (rows.Count + cap - 1) / cap,
        "page 1 " + Ids(real[0]) + "; before " + Ids(Before(flags)));

    // The owner's tray (2026-11-22 log): 37 rows, 11 class rows, the tray lays out 12 -> pages of 12, 12, 12 and 1.
    var synth = Enumerable.Range(0, 37).Select(i => !(i % 3 == 0 && i <= 30)).ToList();
    var pages37 = PagesOf(synth);
    Check("TP2 37 rows (11 class rows) at 12 a page: 4 pages of 12, 12, 12 and 1, page 1 the 11 class rows and the first hero, every row once",
        synth.Count(x => !x) == 11 && pages37.Select(p => p.Count).SequenceEqual(new[] { 12, 12, 12, 1 }) && pages37[0].SequenceEqual(Before(synth))
        && pages37[0].Count(i => !synth[i]) == 11 && Partition(synth, pages37) && pages37[3].Single() == 36,
        string.Join(" / ", pages37.Select(p => string.Join(",", p))));
    var clampedHigh = NativeDungeonRunText.TrayPageRows(synth, cap, 9);
    var clampedLow = NativeDungeonRunText.TrayPageRows(synth, cap, -1);
    Check("TP3 a page index out of range is clamped (page 10 of 4 is page 4, page 0 of 4 is page 1)",
        clampedHigh.SequenceEqual(pages37[3]) && clampedLow.SequenceEqual(pages37[0]) && NativeDungeonRunText.ClampPage(9, 37, cap) == 3);
    // A prefab without a 1-hero layout: the last page is 2 rows ending on the last row (it repeats one row of page 3).
    var short37 = PagesOf(synth, n => n >= 2 && n <= cap);
    Check("TP4 a last page the prefab cannot lay out takes the smallest size it can, ending on the last row, and no row is lost",
        short37.Count == 4 && short37[3].SequenceEqual(new[] { 35, 36 }) && short37.Take(3).All(p => p.Count == 12)
        && short37.SelectMany(p => p).Distinct().Count() == 37 && short37[0].SequenceEqual(pages37[0]),
        string.Join(",", short37[3]));
    Check("TP5 no paging when the rows fit one page or no capacity is known (every row, in row order)",
        NativeDungeonRunText.TrayPageRows(synth, 0, 2).SequenceEqual(Enumerable.Range(0, 37))
        && NativeDungeonRunText.TrayPageRows(synth, 37, 1).SequenceEqual(Enumerable.Range(0, 37))
        && NativeDungeonRunText.TrayPageRows(synth.Take(5).ToList(), cap, 3).SequenceEqual(Enumerable.Range(0, 5))
        && NativeDungeonRunText.TrayPageRows(new List<bool>(), cap, 0).Count == 0 && NativeDungeonRunText.TrayPageRows(null, cap, 0).Count == 0
        && NativeDungeonRunText.Pages(12, cap) == 1 && NativeDungeonRunText.Pages(13, cap) == 2);
    Check("TP6 the page log and the plate labels",
        NativeDungeonRunText.TrayPageLog(0, 4) == "tray page 1 of 4" && NativeDungeonRunText.TrayPageLog(3, 4) == "tray page 4 of 4"
        && NativeDungeonRunText.TrayPreviousLabel == "Previous" && NativeDungeonRunText.TrayNextLabel == "Next");

    // Plates (screen pixels, y up): Back is 120x40, its art reaches 4 px past the body sideways and 3 px up and down.
    var grid = (X: 100f, Y: 200f, W: 600f, H: 400f);
    var back = (X: 900f, Y: 100f, W: 120f, H: 40f);
    var choose = new List<(float X, float Y, float W, float H)> { (800f, 150f, 200f, 200f) };
    var under = NativeDungeonRunText.TrayPagerPlace(grid, back, 4, 3, choose, 720);
    Check("TP7 the plates go just under the hero grid at its two ends, a quarter plate below it, clear of Back and Choose",
        under != null && under.Value.Where == "under the grid" && under.Value.PreviousX == 104 && under.Value.NextX == 576 && under.Value.Y == 150,
        under == null ? "none" : under.Value.Where + " " + under.Value.PreviousX + "," + under.Value.NextX + "," + under.Value.Y);
    var low = NativeDungeonRunText.TrayPagerPlace((100f, 30f, 600f, 400f), (900f, 470f, 120f, 40f), 4, 3, null, 720);
    Check("TP8 with no room under the grid they go level with Back",
        low != null && low.Value.Where == "level with Back" && low.Value.Y == 470 && low.Value.PreviousX == 104 && low.Value.NextX == 576,
        low == null ? "none" : low.Value.Where + " " + low.Value.Y);
    var blocked = NativeDungeonRunText.TrayPagerPlace(grid, back, 4, 3, new List<(float X, float Y, float W, float H)> { (0f, 0f, 1280f, 199f) }, 720);
    var narrow = NativeDungeonRunText.TrayPagerPlace((100f, 200f, 250f, 400f), back, 4, 3, null, 720);
    var offScreen = NativeDungeonRunText.TrayPagerPlace((100f, 30f, 600f, 400f), (900f, 700f, 120f, 40f), 4, 3, null, 720);
    Check("TP9 no place (null) when every row is covered, the grid is narrower than two plates, or the row is off screen",
        blocked == null && narrow == null && offScreen == null);

    // The owner's last page (page 4 of 4: one portrait). The tray lays the one button out alone (here a 120x160 portrait in the
    // middle of the plate), far narrower than two plates: placed against that, Previous had no place and page 4 was a dead end.
    // The plates are placed against the full-capacity grid instead, remembered from a full page as an offset from Back.
    var one = (X: 340f, Y: 350f, W: 120f, H: 160f);
    var memo = new NativeDungeonRunText.TrayGridMemo();
    var g1 = NativeDungeonRunText.TrayPagerGrid(memo, grid, 12, cap, back, 1280, 720, out string s1);
    var g4 = NativeDungeonRunText.TrayPagerGrid(memo, one, 1, cap, back, 1280, 720, out string s4);
    var avoid4 = new List<(float X, float Y, float W, float H)>(choose) { one };
    var last = g4 == null ? null : NativeDungeonRunText.TrayPagerPlace(g4.Value, back, 4, 3, avoid4, 720, needPrevious: true, needNext: false);
    var oldLast = NativeDungeonRunText.TrayPagerPlace(one, back, 4, 3, choose, 720);
    var bare = NativeDungeonRunText.TrayPagerGrid(new NativeDungeonRunText.TrayGridMemo(), one, 1, cap, back, 1280, 720, out string sBare);
    var bareLast = bare == null ? null : NativeDungeonRunText.TrayPagerPlace(bare.Value, back, 4, 3, avoid4, 720, needPrevious: true, needNext: false);
    Check("TP10 a 1-hero last page places Previous: at page 1's spot against the remembered full grid, or under the one button with nothing remembered (the shown grid alone, both plates asked, has no place)",
        s1 == "full" && g1 == grid && s4 == "remembered" && g4 == grid
        && last != null && last.Value.Where == "under the grid" && last.Value.PreviousX == 104 && last.Value.Y == 150
        && oldLast == null
        && sBare == "shown" && bare == one && bareLast != null && bareLast.Value.Where == "under the grid" && bareLast.Value.PreviousX == 344 && bareLast.Value.Y == 300,
        s1 + " " + s4 + " " + (last == null ? "none" : last.Value.Where + " " + last.Value.PreviousX + "," + last.Value.Y)
        + "; bare " + sBare + " " + (bareLast == null ? "none" : bareLast.Value.Where + " " + bareLast.Value.PreviousX + "," + bareLast.Value.Y));

    // Only the plates that will be shown must be clear: something on Next's spot under the grid (or on Previous's) moves both
    // plates when both show (here to no place at all), but not a lone Previous (last page) or a lone Next (page 1).
    var onNext = new List<(float X, float Y, float W, float H)> { (560f, 140f, 150f, 60f) };
    var onPrevious = new List<(float X, float Y, float W, float H)> { (90f, 140f, 150f, 60f) };
    var bothNext = NativeDungeonRunText.TrayPagerPlace(grid, back, 4, 3, onNext, 720);
    var loneP = NativeDungeonRunText.TrayPagerPlace(grid, back, 4, 3, onNext, 720, needPrevious: true, needNext: false);
    var bothPrevious = NativeDungeonRunText.TrayPagerPlace(grid, back, 4, 3, onPrevious, 720);
    var loneN = NativeDungeonRunText.TrayPagerPlace(grid, back, 4, 3, onPrevious, 720, needPrevious: false, needNext: true);
    var neither = NativeDungeonRunText.TrayPagerPlace(grid, back, 4, 3, null, 720, needPrevious: false, needNext: false);
    var narrowLone = NativeDungeonRunText.TrayPagerPlace((100f, 200f, 250f, 400f), back, 4, 3, null, 720, needPrevious: false, needNext: true);
    Check("TP11 only the plates shown must be clear: a covered Next spot leaves a lone Previous under the grid, a covered Previous spot a lone Next, and a lone plate needs no room for two",
        (bothNext == null || bothNext.Value.Where != "under the grid") && (bothPrevious == null || bothPrevious.Value.Where != "under the grid")
        && loneP != null && loneP.Value.Where == "under the grid" && loneP.Value.PreviousX == 104 && loneP.Value.Y == 150
        && loneN != null && loneN.Value.Where == "under the grid" && loneN.Value.NextX == 576 && loneN.Value.Y == 150
        && neither == null && narrowLone != null && narrowLone.Value.NextX == 226,
        (bothNext == null ? "none" : bothNext.Value.Where) + " / " + (loneP == null ? "none" : loneP.Value.Where) + " / "
        + (loneN == null ? "none" : loneN.Value.Where) + " / " + (narrowLone == null ? "none" : narrowLone.Value.NextX.ToString()));

    // Pages keep one row: with Previous's spot under the grid covered but the row level with Back clear at both ends, pages 2-3
    // (both plates) go level with Back, and so do page 1 (Next only) and the last page (Previous only), not under the grid.
    var lowOnPrevious = new List<(float X, float Y, float W, float H)> { (90f, 150f, 150f, 50f) };
    var keepBoth = NativeDungeonRunText.TrayPagerPlace(grid, back, 4, 3, lowOnPrevious, 720);
    var keepNext = NativeDungeonRunText.TrayPagerPlace(grid, back, 4, 3, lowOnPrevious, 720, needPrevious: false, needNext: true);
    var keepPrevious = NativeDungeonRunText.TrayPagerPlace(grid, back, 4, 3, lowOnPrevious, 720, needPrevious: true, needNext: false);
    Check("TP14 a page with one plate uses the row the two-plate pages use when that row is clear at both ends",
        keepBoth != null && keepBoth.Value.Where == "level with Back" && keepNext != null && keepNext.Value.Where == "level with Back"
        && keepPrevious != null && keepPrevious.Value.Where == "level with Back" && keepNext.Value.Y == keepBoth.Value.Y,
        (keepBoth == null ? "none" : keepBoth.Value.Where) + " / " + (keepNext == null ? "none" : keepNext.Value.Where) + " / "
        + (keepPrevious == null ? "none" : keepPrevious.Value.Where));

    // The plates sit at the same place on every page: pages 1-3 show 12 (the full grid itself), page 4 one portrait (the
    // remembered grid). While the tray slides in, Back and the grid move together, so the plates move with them; at another
    // screen size the remembered grid is not used.
    var memoPages = new NativeDungeonRunText.TrayGridMemo();
    var spots = new List<(float PreviousX, float NextX, float Y, string Where)?>();
    for (int p = 0; p < 4; p++)
    {
        var live = p < 3 ? grid : one;
        var g = NativeDungeonRunText.TrayPagerGrid(memoPages, live, p < 3 ? 12 : 1, cap, back, 1280, 720, out _);
        var a = new List<(float X, float Y, float W, float H)>(choose) { live };
        spots.Add(g == null ? null : NativeDungeonRunText.TrayPagerPlace(g.Value, back, 4, 3, a, 720, needPrevious: p > 0, needNext: p < 3));
    }
    var slid = (X: back.X, Y: back.Y - 30f, W: back.W, H: back.H);
    var gSlid = NativeDungeonRunText.TrayPagerGrid(memoPages, (one.X, one.Y - 30f, one.W, one.H), 1, cap, slid, 1280, 720, out string sSlid);
    var gResized = NativeDungeonRunText.TrayPagerGrid(memoPages, one, 1, cap, back, 1920, 1080, out string sResized);
    Check("TP12 the plates sit at the same place on every page (Previous on pages 2-4, Next on pages 1-3, one row), follow Back while the tray slides, and a new screen size forgets the grid",
        spots.All(x => x != null && x.Value.Where == "under the grid" && x.Value.Y == 150)
        && spots.Skip(1).All(x => x.Value.PreviousX == 104) && spots.Take(3).All(x => x.Value.NextX == 576)
        && sSlid == "remembered" && gSlid == (grid.X, grid.Y - 30f, grid.W, grid.H)
        && sResized == "shown" && gResized == one,
        string.Join(" / ", spots.Select(x => x == null ? "none" : x.Value.PreviousX + "," + x.Value.NextX + "," + x.Value.Y)) + "; " + sSlid + " " + sResized);
    Check("TP13 the post-show overlap check counts shared area only, not touching edges",
        NativeDungeonRunText.TrayRectsOverlap((0f, 0f, 10f, 10f), (5f, 5f, 10f, 10f))
        && !NativeDungeonRunText.TrayRectsOverlap((0f, 0f, 10f, 10f), (10f, 0f, 10f, 10f))
        && !NativeDungeonRunText.TrayRectsOverlap((0f, 0f, 10f, 10f), (0f, 10f, 10f, 10f))
        && NativeDungeonRunText.TrayRectsOverlap((0f, 0f, 10f, 10f), (2f, 2f, 2f, 2f)));
});

// ============================================================ BD. Build Deck rules (DungeonRunBuild)
// Card ids are engine cards, scripted and admitted for a catalog run (each is verified by the predicate in the checks): mage
// AT_003/AT_006/AT_007 and legendary AT_009, neutral AT_082/AT_083, rogue AT_028/AT_029, warrior AT_064, hero BOT_238,
// Fabled TIME_009 with its companion TIME_009t1, Death Knight rune cards RLK_024 (1 blood), RLK_025 (1 frost), RLK_048
// (1 unholy), RLK_061 (2 unholy) and CATA_156 (2 blood). No neutral rune card is admitted, so "rune-class" uses a Death
// Knight card on a Mage row.
Guard("BD", () =>
{
    var pol = OfflinePoolPolicy.Load();
    IReadOnlyList<string> Set(string row) => data.Classes.First(c => c.Id == row).ClassSet;
    var mage = Set("MAGE"); var dk = Set("DEATHKNIGHT"); var dual = Set("MAGE_ROGUE");
    string[] mageCards = { "AT_003", "AT_006", "AT_007" }, neutral = { "AT_082", "AT_083" };
    string[] legal10 = { "AT_006", "AT_006", "AT_007", "AT_007", "AT_003", "AT_003", "AT_082", "AT_082", "AT_083", "AT_083" };

    string warrior = DungeonRunBuild.CardReason("AT_064", mage, pol);
    Check("BD1 a Mage row allows scripted, admitted Mage cards and neutrals, and refuses a Warrior card (class)",
        mageCards.Concat(neutral).All(id => DungeonRunBuild.CardReason(id, mage, pol) == null) && warrior == "class", warrior ?? "null");

    string hero = DungeonRunBuild.CardReason("BOT_238", mage, pol), fabled = DungeonRunBuild.CardReason("TIME_009", mage, pol),
        companion = DungeonRunBuild.CardReason("TIME_009t1", mage, pol);
    // The companion is not collectible, and the collectible rule comes first (BD17).
    Check("BD2 a hero card is \"hero\"; a Fabled primary is \"fabled\"; its companion is refused (\"not-collectible\")",
        hero == "hero" && fabled == "fabled" && companion == "not-collectible",
        hero + " / " + fabled + " / " + companion);

    var r10 = DungeonRunBuild.DeckReasons(legal10, mage, 10, null, null, pol);
    var r9 = DungeonRunBuild.DeckReasons(legal10.Take(9).ToList(), mage, 10, null, null, pol);
    var r11 = DungeonRunBuild.DeckReasons(legal10.Concat(new[] { "AT_084" }).ToList(), mage, 10, null, null, pol);
    Check("BD3 10 legal cards have no reasons; 9 and 11 are \"size 9 of 10\" / \"size 11 of 10\"",
        r10.Count == 0 && r9.Contains("size 9 of 10") && r11.Contains("size 11 of 10"), string.Join("|", r10.Concat(r9).Concat(r11)));

    var triple = DungeonRunBuild.DeckReasons(new[] { "AT_006", "AT_006", "AT_006", "AT_007", "AT_007", "AT_003", "AT_003", "AT_082", "AT_082", "AT_083" }, mage, 10, null, null, pol);
    var twoLegend = DungeonRunBuild.DeckReasons(new[] { "AT_009", "AT_009", "AT_007", "AT_007", "AT_003", "AT_003", "AT_082", "AT_082", "AT_083", "AT_083" }, mage, 10, null, null, pol);
    Check("BD4 3 copies are \"copies <id>\"; 2 of a legendary are \"legendary <id>\"",
        triple.Contains("copies AT_006") && twoLegend.SequenceEqual(new[] { "legendary AT_009" }), string.Join("|", triple.Concat(twoLegend)));

    var grouped = DungeonRunBuild.DeckReasons(new[] { "AT_006", "AT_006", "AT_007", "AT_003", "AT_003", "AT_082", "AT_082", "AT_083", "AT_083", "AT_084" },
        mage, 10, null, id => id == "AT_006" || id == "AT_007" ? "group-a" : id, pol);
    var ownedLow = DungeonRunBuild.DeckReasons(legal10, mage, 10, id => id == "AT_006" ? 1 : 2, null, pol);
    Check("BD5 two ids in one copy group (2 + 1) are \"copies <group>\"; owning 1 of an id used twice is \"owned <id>\"; owned null skips ownership",
        grouped.SequenceEqual(new[] { "copies group-a" }) && ownedLow.SequenceEqual(new[] { "owned AT_006" }) && r10.Count == 0,
        string.Join("|", grouped.Concat(ownedLow)));

    string[] dk3 = { "RLK_024", "RLK_024", "RLK_025", "RLK_025", "RLK_048", "RLK_048", "AT_082", "AT_082", "AT_083", "AT_083" };
    string[] dk4 = { "RLK_024", "RLK_024", "RLK_025", "RLK_025", "RLK_048", "RLK_048", "AT_082", "AT_082", "RLK_061", "RLK_061" };
    var d3 = DungeonRunBuild.DeckReasons(dk3, dk, 10, null, null, pol);
    var d4 = DungeonRunBuild.DeckReasons(dk4, dk, 10, null, null, pol);
    string runeOnMage = DungeonRunBuild.CardReason("CATA_156", mage, pol);
    Check("BD6 a Death Knight deck needing 4 runes is \"runes\", 3 runes is legal; a rune-cost card on a Mage row is \"rune-class\"",
        d3.Count == 0 && d4.SequenceEqual(new[] { "runes" }) && runeOnMage == "rune-class", string.Join("|", d3.Concat(d4)) + " / " + runeOnMage);

    var dualMage = DungeonRunBuild.DeckReasons(legal10, dual, 10, null, null, pol);
    string[] mix = { "AT_006", "AT_006", "AT_007", "AT_007", "AT_028", "AT_028", "AT_029", "AT_029", "AT_082", "AT_082" };
    var dualMix = DungeonRunBuild.DeckReasons(mix, dual, 10, null, null, pol);
    Check("BD7 a Mage/Rogue row: 10 Mage cards are legal, and a Rogue + Mage mix is legal", dual.Count == 2 && dualMage.Count == 0 && dualMix.Count == 0,
        string.Join("|", dualMage.Concat(dualMix)));

    var full = DungeonRunBuild.AddReason(legal10, "AT_084", mage, 10, null, null, pol);
    var nine = legal10.Take(9).ToList();
    var third = DungeonRunBuild.AddReason(nine.Where(x => x != "AT_083").Concat(new[] { "AT_084" }).ToList(), "AT_006", mage, 10, null, null, pol);
    var fine = DungeonRunBuild.AddReason(nine, "AT_083", mage, 10, null, null, pol);
    Check("BD8 AddReason is \"size\" on a full deck, \"copies <id>\" for a third copy, and null for a legal add",
        full == "size" && third == "copies AT_006" && fine == null, full + " / " + third + " / " + (fine ?? "null"));
});

// Build Deck data (Task 3): every enabled row (class rows, hero rows, enabled catalog rows: data.Classes) has exactly one
// build-able deck, its last: a "build" deck (no cards, a display card, no unlock, id "<row>_build") on the class, Rikkar and
// Monster Hunt rows, the generated Random deck on the Heist and Tombs rows. The catalog shows it as "Build Deck", unlocked.
Guard("BDD", () =>
{
    var bad = new List<string>();
    foreach (var row in data.Classes)
    {
        var decks = row.Decks.Select(data.FindDeck).ToList();
        var build = row.BuildDeck(data);
        bool generatedRow = row.Adventure == "Heist" || row.Adventure == "Tombs";
        bool ok = build != null && decks.Count(d => d != null && d.IsBuildable) == 1 && ReferenceEquals(decks.Last(), build)
            && build.Cards.Count == 0 && build.DisplayCard != null && build.Class == row.Id
            && (generatedRow
                ? build.IsGenerated && !build.Build
                : build.Build && !build.IsGenerated && build.Unlock == null && build.Id == row.Id.ToLowerInvariant() + "_build")
            && (row.IsHero || decks.Count(d => d != null && !d.IsBuildable) == data.Rules.DeckOptions)
            && build.Shrine == (row.Adventure == "Rumble" ? data.FindDeck(row.DefaultDeck).Shrine : null)
            && (row.Adventure != "Rumble" || build.Shrine != null);
        if (!ok) bad.Add(row.Id + ":" + (build?.Id ?? "none"));
    }
    var errs = DungeonRunValidator.Validate(data, null).Where(e => e.Contains("build", StringComparison.OrdinalIgnoreCase)).ToList();
    var mage = data.FindClass("MAGE");
    mage.Decks.Remove("mage_build");
    List<string> missing;
    try { missing = DungeonRunValidator.Validate(data, null); } finally { mage.Decks.Add("mage_build"); }
    Check($"BD9 each of the {data.Classes.Count} enabled rows has exactly one build-able deck, its last (a build deck on the class, Rikkar and Monster Hunt rows, "
        + "the generated deck on Heist and Tombs; Rikkar's names the default deck's shrine); the validator accepts them and refuses a row without one",
        data.Classes.Count == 37 && bad.Count == 0 && errs.Count == 0 && missing.Any(e => e.StartsWith("class MAGE: has 0 build-able decks", StringComparison.Ordinal)),
        string.Join(" ", bad) + " | " + string.Join(" | ", errs.Concat(missing.Where(e => e.Contains("MAGE"))).Take(6)));

    var run = new DungeonRun(data);
    var fresh = new DungeonRunProfile();
    var wrong = new List<string>();
    foreach (var row in data.Classes)
    {
        var options = DungeonRunCatalog.Decks(run, fresh, row.Id);
        var last = options.LastOrDefault();
        bool ok = options.Count == row.Decks.Count && last != null && last.IsBuild && last.Id == row.BuildDeck(data)?.Id && last.Name == "Build Deck"
            && last.Description == "Build a 10-card deck from your collection." && last.Unlocked && last.CardIds.Count == 0 && !last.IsDefault
            && last.DisplayCard != null && last.DisplayCard == row.BuildDeck(data).DisplayCard && last.UnlockKey == null && last.UnlockHint == null
            && options.Take(options.Count - 1).Select((o, i) => (o, d: data.FindDeck(row.Decks[i]))).All(p => !p.o.IsBuild && p.o.Id == p.d.Id && p.o.Name == p.d.Name
                && p.o.Description == p.d.Description && p.o.CardIds.SequenceEqual(p.d.Cards) && p.o.Unlocked == run.IsUnlocked(fresh, p.d.Unlock) && p.o.UnlockKey == p.d.Unlock
                && p.o.DisplayCard == p.d.DisplayCard);
        if (!ok) wrong.Add(row.Id);
    }
    Check("BD10 DungeonRunCatalog.Decks lists each row's Build Deck option last (\"Build Deck\", unlocked on a fresh profile, no cards, IsBuild, the build deck's "
        + "display card, no unlock key or hint), the other decks as before",
        wrong.Count == 0, string.Join(" ", wrong));
});

// Build Deck run start (Task 4): NewRun with a built list starts on the row's build deck (the Heist/Tombs generated deck on
// those rows) with exactly the list, skips the deck unlock, marks the run BuiltDeck; a built run counts as every deck of its
// row for treasure scope and deck tags. The fresh profile has nothing unlocked, so heist_mage_random is locked for a plain start.
Guard("BDR", () =>
{
    var run = new DungeonRun(data);
    var fresh = new DungeonRunProfile();
    var xml = new System.Xml.Serialization.XmlSerializer(typeof(DungeonRunSave));
    string Xml(DungeonRunState state) { using var w = new StringWriter(); xml.Serialize(w, new DungeonRunSave { Active = state }); return w.ToString(); }
    string[] mageList = { "AT_006", "AT_006", "AT_007", "AT_007", "AT_003", "AT_003", "AT_082", "AT_082", "AT_083", "AT_083" };
    string[] rogueList = { "AT_028", "AT_028", "AT_029", "AT_029", "AT_082", "AT_082", "AT_083", "AT_083", "AT_084", "AT_084" };
    DungeonRunState Built(string row, IReadOnlyList<string> list, long seed = 4250)
    {
        var cls = data.FindClass(row);
        return run.NewRun(fresh, cls.Id, cls.BuildDeck(data).Id, cls.Powers[0], seed, builtCards: list);
    }
    string Refusal(Action start)
    {
        try { start(); return "started"; }
        catch (InvalidOperationException e) { return e.Message; }
    }

    var mage = Built("MAGE", mageList);
    var heist = Built("DALA_Rakanishu", mageList);
    var plain = run.NewRun(fresh, "MAGE", "mage_rimeward", data.FindClass("MAGE").Powers[0], 4250);
    var bad = new List<string>();
    foreach (var (state, row) in new[] { (mage, "MAGE"), (heist, "DALA_Rakanishu") })
    {
        var back = DungeonRunStore.Clone(new DungeonRunSave { Active = state }).Active;
        string resumed;
        try { run.Resume(back); resumed = null; } catch (Exception e) { resumed = e.Message; }
        string build = data.FindClass(row).BuildDeck(data).Id;
        if (!state.Deck.SequenceEqual(mageList) || state.StarterDeck != build || !state.BuiltDeck) bad.Add(row + " start " + state.StarterDeck + " " + state.BuiltDeck);
        if (!back.Deck.SequenceEqual(mageList) || back.StarterDeck != build || !back.BuiltDeck || !Xml(state).Contains("BuiltDeck=\"true\"")) bad.Add(row + " xml");
        if (resumed != null) bad.Add(row + " resume: " + resumed);
    }
    var lost = DungeonRunStore.Clone(new DungeonRunSave { Profile = new DungeonRunProfile(), Active = Built("MAGE", mageList, 4251) });
    lost.Active.Phase = DungeonRunPhase.InMatch;
    run.RecordOutcome(lost.Active, lost.Profile, DungeonRunOutcome.Loss);
    var entry = DungeonRunStore.Clone(lost).Profile.Archive.LastOrDefault();
    Check("BD11 a built MAGE run and a built Heist run (fresh profile) hold exactly the list, StarterDeck the row's build deck (mage_build, heist_mage_random), "
        + "BuiltDeck true; the XML round trip keeps them and Resume accepts it; a plain run writes no BuiltDeck; the archived summary keeps BuiltDeck",
        bad.Count == 0 && mage.StarterDeck == "mage_build" && heist.StarterDeck == "heist_mage_random" && !plain.BuiltDeck && !Xml(plain).Contains("BuiltDeck")
            && entry != null && entry.BuiltDeck,
        string.Join(" | ", bad) + " | archive " + (entry == null ? "none" : entry.StarterDeck + " " + entry.BuiltDeck));

    string nine = Refusal(() => Built("MAGE", mageList.Take(9).ToList()));
    string warrior = Refusal(() => Built("MAGE", mageList.Take(9).Concat(new[] { "AT_064" }).ToList()));
    string notBuild = Refusal(() => run.NewRun(fresh, "MAGE", "mage_rimeward", data.FindClass("MAGE").Powers[0], 4250, builtCards: mageList));
    string noList = Refusal(() => run.NewRun(fresh, "MAGE", "mage_build", data.FindClass("MAGE").Powers[0], 4250));
    string locked = Refusal(() => run.NewRun(fresh, "DALA_Rakanishu", "heist_mage_random", data.FindClass("DALA_Rakanishu").Powers[0], 4250));
    Check("BD12 a built list of 9 cards is refused with \"size 9 of 10\", one with a Warrior card with \"class AT_064\"; a built list on a non-build deck is "
        + "refused with NotBuildDeck, a plain start on a build deck with NeedsBuiltList; a plain start on the locked Heist generated deck is still LockedDeck",
        nine.Contains("size 9 of 10") && warrior.Contains("class AT_064") && notBuild == DungeonRun.NotBuildDeck && noList == DungeonRun.NeedsBuiltList
            && locked == DungeonRun.LockedDeck,
        nine + " / " + warrior + " / " + notBuild + " / " + noList + " / " + locked);

    var tess = data.FindClass("GILA_500h3");
    var hunt = Built("GILA_500h3", rogueList);
    var scoped = data.Treasures.Where(t => t.Scope.Decks.Any(tess.Decks.Contains)).Select(t => t.Id).OrderBy(id => id, StringComparer.Ordinal).ToList();
    var inScope = scoped.Where(id => DungeonRun.TreasureInScope(data.FindTreasure(id), tess, hunt)).ToList();
    hunt.BuiltDeck = false;
    bool gilaPlain = DungeonRun.TreasureInScope(data.FindTreasure("GILA_501"), tess, hunt);
    hunt.BuiltDeck = true;
    var mageCls = data.FindClass("MAGE");
    var mageUnion = new HashSet<string>(data.TagsOf(mageCls).Concat(mageCls.Decks.SelectMany(d => data.FindDeck(d).TreasureTags)));
    var mageTags = run.TreasureAffinityTags(mage, mageCls);
    var plainTags = run.TreasureAffinityTags(plain, mageCls);
    var huntTags = run.TreasureAffinityTags(hunt, tess);
    Check("BD13 a built Monster Hunt run (Tess) has every treasure scoped to its row's decks in scope (43, GILA_501 among them; not when BuiltDeck is off); "
        + "a built run's deck tags are the union of its row's decks' tags (MAGE: minions from mage_kindled, which a mage_rimeward run lacks)",
        scoped.Count == 43 && scoped.Contains("GILA_501") && inScope.SequenceEqual(scoped) && !gilaPlain && mageTags.SetEquals(mageUnion) && mageTags.Contains("minions")
            && !plainTags.Contains("minions") && huntTags.IsSupersetOf(new[] { "spells", "weapons" }),
        "scoped " + string.Join(",", scoped) + " in " + string.Join(",", inScope) + " plain " + gilaPlain + " | mage " + string.Join(",", mageTags.OrderBy(x => x))
            + " plain " + string.Join(",", plainTags.OrderBy(x => x)) + " hunt " + string.Join(",", huntTags.OrderBy(x => x)));
});

// Build Deck plank builder (Task 6): the run menu's pure helpers (NativeDungeonRunText). The fixture's names, costs and owned
// counts are its own; class vs neutral is the engine card's class (AT_001..AT_009 are Mage, AT_082..AT_090 neutral).
Guard("BDP", () =>
{
    RunBuildCard C(string id, string name, int cost, int owned = 2, bool legendary = false, string group = null) =>
        new RunBuildCard { Id = id, ClientId = id, Name = name, Cost = cost, Owned = owned, Legendary = legendary, CopyGroup = group ?? id };
    // Given out of order: BuildPage sorts by cost, then name.
    var all = new List<RunBuildCard>
    {
        C("AT_009", "Rhonin", 8, 1, true), C("AT_090", "Mukla's Champion", 5), C("AT_001", "Flame Lance", 5), C("AT_082", "Lowly Squire", 1),
        C("AT_007", "Spellslinger", 3), C("AT_089", "Boneguard Lieutenant", 2), C("AT_002", "Effigy", 3), C("AT_086", "Saboteur", 3),
        C("AT_004", "Arcane Blast", 0), C("AT_088", "Mogor's Champion", 6), C("AT_003", "Fallen Hero", 2), C("AT_083", "Dragonhawk Rider", 3),
        C("AT_005", "Polymorph: Boar", 3), C("AT_085", "Maiden of the Lake", 4), C("AT_008", "Coldarra Drake", 6), C("AT_084", "Lance Carrier", 2),
        C("AT_006", "Dalaran Aspirant", 4), C("AT_087", "Argent Horserider", 3),
    };
    var neutralIds = new HashSet<string> { "AT_082", "AT_083", "AT_084", "AT_085", "AT_086", "AT_087", "AT_088", "AT_089", "AT_090" };
    bool classesAsAssumed = all.All(c => (SabberStoneCore.Model.Cards.FromId(c.Id).Class == SabberStoneCore.Enums.CardClass.NEUTRAL) == neutralIds.Contains(c.Id));
    string Names(string filter, int page) => string.Join(",", NativeDungeonRunText.BuildPage(all,
        (NativeDungeonRunText.BuildFilter)Enum.Parse(typeof(NativeDungeonRunText.BuildFilter), filter), page, 6).Select(c => c.Name));
    var want = new (string Filter, int Page, string Names)[]
    {
        ("All", 0, "Arcane Blast,Lowly Squire,Boneguard Lieutenant,Fallen Hero,Lance Carrier,Argent Horserider"),
        ("All", 1, "Dragonhawk Rider,Effigy,Polymorph: Boar,Saboteur,Spellslinger,Dalaran Aspirant"),
        ("All", 2, "Maiden of the Lake,Flame Lance,Mukla's Champion,Coldarra Drake,Mogor's Champion,Rhonin"),
        ("All", 9, "Maiden of the Lake,Flame Lance,Mukla's Champion,Coldarra Drake,Mogor's Champion,Rhonin"),
        ("Class", 0, "Arcane Blast,Fallen Hero,Effigy,Polymorph: Boar,Spellslinger,Dalaran Aspirant"),
        ("Class", 1, "Flame Lance,Coldarra Drake,Rhonin"),
        ("Neutral", 0, "Lowly Squire,Boneguard Lieutenant,Lance Carrier,Argent Horserider,Dragonhawk Rider,Saboteur"),
        ("Neutral", 1, "Maiden of the Lake,Mukla's Champion,Mogor's Champion"),
        ("Cost0to1", 0, "Arcane Blast,Lowly Squire"),
        ("Cost2", 0, "Boneguard Lieutenant,Fallen Hero,Lance Carrier"),
        ("Cost3", 0, "Argent Horserider,Dragonhawk Rider,Effigy,Polymorph: Boar,Saboteur,Spellslinger"),
        ("Cost4", 0, "Dalaran Aspirant,Maiden of the Lake"),
        ("Cost5", 0, "Flame Lance,Mukla's Champion"),
        ("Cost6", 0, "Coldarra Drake,Mogor's Champion"),
        ("Cost7Plus", 0, "Rhonin"),
    };
    var wrongPages = want.Where(w => Names(w.Filter, w.Page) != w.Names).Select(w => w.Filter + " " + w.Page + " = " + Names(w.Filter, w.Page)).ToList();
    var filters = (NativeDungeonRunText.BuildFilter[])Enum.GetValues(typeof(NativeDungeonRunText.BuildFilter));
    string order = string.Join(",", filters);
    var cycle = new List<NativeDungeonRunText.BuildFilter>();
    var f0 = NativeDungeonRunText.BuildFilter.All;
    for (int i = 0; i <= filters.Length; i++) { cycle.Add(f0); f0 = NativeDungeonRunText.NextBuildFilter(f0); }
    int filteredAll = NativeDungeonRunText.BuildFiltered(all, NativeDungeonRunText.BuildFilter.All).Count,
        filteredClass = NativeDungeonRunText.BuildFiltered(all, NativeDungeonRunText.BuildFilter.Class).Count;
    Check("BD14 BuildPage filters class vs neutral (the engine card's class) and the cost buckets (0-1, 2..6, 7+), 6 per page by cost then name; a page past "
        + "the end is the last page; the filter enum and its cycle are All, Class, Neutral, Cost0to1..Cost6, Cost7Plus and back to All",
        classesAsAssumed && wrongPages.Count == 0 && filteredAll == 18 && filteredClass == 9
            && order == "All,Class,Neutral,Cost0to1,Cost2,Cost3,Cost4,Cost5,Cost6,Cost7Plus" && cycle.Take(filters.Length).SequenceEqual(filters)
            && cycle.Last() == NativeDungeonRunText.BuildFilter.All,
        "classes " + classesAsAssumed + " | " + string.Join(" | ", wrongPages) + " | counts " + filteredAll + "/" + filteredClass + " | " + order);

    var fireball = C("CS2_029", "Fireball", 4, 2);
    string[] labels =
    {
        NativeDungeonRunText.BuildCardLabel(fireball, 1), NativeDungeonRunText.BuildCardLabel(fireball, 0),
        NativeDungeonRunText.BuildCardLabel(C("AT_009", "Rhonin", 8, 1, true), 0), NativeDungeonRunText.BuildCardLabel(C("AT_009", "Rhonin", 8, 2, true), 1),
        NativeDungeonRunText.BuildCardLabel(C("AT_007", "Spellslinger", 3, 1), 1), NativeDungeonRunText.BuildCardLabel(C("AT_007", "Spellslinger", 3, 5), 2),
    };
    string[] captions =
    {
        NativeDungeonRunText.BuildCaption(new string[0], 10), NativeDungeonRunText.BuildCaption(new[] { "Fireball", "Frostbolt", "Fireball" }, 10),
    };
    string[] texts =
    {
        NativeDungeonRunText.BuildFilterLabel(NativeDungeonRunText.BuildFilter.All), NativeDungeonRunText.BuildFilterLabel(NativeDungeonRunText.BuildFilter.Class),
        NativeDungeonRunText.BuildFilterLabel(NativeDungeonRunText.BuildFilter.Neutral), NativeDungeonRunText.BuildFilterLabel(NativeDungeonRunText.BuildFilter.Cost0to1),
        NativeDungeonRunText.BuildFilterLabel(NativeDungeonRunText.BuildFilter.Cost4), NativeDungeonRunText.BuildFilterLabel(NativeDungeonRunText.BuildFilter.Cost7Plus),
        NativeDungeonRunText.BuildAddRefusal("Fireball", "size"), NativeDungeonRunText.BuildAddRefusal("Fireball", "copies CS2_029"),
        NativeDungeonRunText.BuildAddRefusal("Rhonin", "legendary AT_009"), NativeDungeonRunText.BuildAddRefusal("Fireball", "owned CS2_029"),
        NativeDungeonRunText.BuildAddRefusal("Bone Breaker", "runes"), NativeDungeonRunText.BuildAddRefusal("Fireball", "class CS2_029"),
    };
    string[] wantLabels = { "Fireball (4) 1/2", "Fireball (4) 0/2", "Rhonin (8) 0/1", "Rhonin (8) 1/1", "Spellslinger (3) 1/1", "Spellslinger (3) 2/2" };
    string[] wantCaptions = { "Picked 0 of 10", "Picked 3 of 10: 2x Fireball, Frostbolt" };
    string[] wantTexts =
    {
        "Filter: All cards", "Filter: Class cards", "Filter: Neutral cards", "Filter: Cost 0-1", "Filter: Cost 4", "Filter: Cost 7+",
        "Cannot add Fireball: the deck is full.", "Cannot add Fireball: at most 2 copies of a card.", "Cannot add Rhonin: at most 1 copy of a legendary.",
        "Cannot add Fireball: you own no more copies.", "Cannot add Bone Breaker: a Death Knight deck needs at most 3 runes.", "Cannot add Fireball: class CS2_029",
    };
    Check("BD15 BuildCardLabel is \"Fireball (4) 1/2\" (copies in the list / copies it may hold: min(owned, 2, 1 for a legendary)); BuildCaption is \"Picked n of size\" "
        + "and the picked names with repeats folded; the filter plank and add-refusal texts are exact",
        labels.SequenceEqual(wantLabels) && captions.SequenceEqual(wantCaptions) && texts.SequenceEqual(wantTexts),
        string.Join(" | ", labels) + " | " + string.Join(" | ", captions) + " | " + string.Join(" | ", texts));

    // 7 legal copies: 3 owned of one card (2), 1 owned (1), a legendary owned twice (1), two ids of one copy group owned once each
    // (2), and 1 owned (1). A copyGroup delegate overrides the cards' own groups: AT_006 and AT_007 in one group hold 2, not 3.
    var seven = new List<RunBuildCard>
    {
        C("AT_006", "Dalaran Aspirant", 4, 3), C("AT_007", "Spellslinger", 3, 1), C("AT_009", "Rhonin", 8, 2, true),
        C("AT_003", "Fallen Hero", 2, 1, false, "group-a"), C("CORE_AT_003", "Fallen Hero", 2, 1, false, "group-a"), C("AT_082", "Lowly Squire", 1, 1),
    };
    int reach = NativeDungeonRunText.BuildReachable(seven, null);
    int regrouped = NativeDungeonRunText.BuildReachable(seven, id => id == "AT_006" || id == "AT_007" ? "group-b" : seven.First(c => c.Id == id).CopyGroup);
    int legendGroup = NativeDungeonRunText.BuildReachable(new[] { C("AT_009", "Rhonin", 8, 1, true, "g"), C("CORE_AT_009", "Rhonin", 8, 1, false, "g") }, null);
    string notEnough = NativeDungeonRunText.BuildNotEnoughLine(reach, 10), enough = NativeDungeonRunText.BuildNotEnoughLine(10, 10);
    Check("BD16 BuildReachable counts legal copies per copy group (min(owned summed over the group, 2, 1 with a legendary in it)): the 7-copy fixture is 7, 6 with "
        + "AT_006 and AT_007 grouped by the delegate, a legendary's group 1; the caption line is \"Not enough cards in your collection for this hero (7 of 10)\", "
        + "none at 10",
        reach == 7 && regrouped == 6 && legendGroup == 1 && notEnough == "Not enough cards in your collection for this hero (7 of 10)" && enough == null,
        reach + " / " + regrouped + " / " + legendGroup + " / " + (notEnough ?? "null") + " / " + (enough ?? "null"));
});

// Build Deck final-review fixes: the collectible rule, the win summary and run hub names, and the run-start refusal in words.
Guard("BDF", () =>
{
    var pol = OfflinePoolPolicy.Load();
    var mage = data.FindClass("MAGE").ClassSet;
    string[] mageList = { "AT_006", "AT_006", "AT_007", "AT_007", "AT_003", "AT_003", "AT_082", "AT_082", "AT_083", "AT_083" };

    // CS2_mirror: Mirror Image's token (a Mage minion), in the engine and not collectible.
    var token = SabberStoneCore.Model.Cards.FromId("CS2_mirror");
    string tokenReason = DungeonRunBuild.CardReason("CS2_mirror", mage, pol);
    var tokenDeck = DungeonRunBuild.DeckReasons(mageList.Take(9).Concat(new[] { "CS2_mirror" }).ToList(), mage, 10, null, null, pol);
    string tokenAdd = NativeDungeonRunText.BuildAddRefusal("Mirror Image", "not-collectible CS2_mirror");
    Check("BD17 a non-collectible card (the token CS2_mirror) is \"not-collectible\" before any other rule, DeckReasons names it, and the add refusal says "
        + "\"Cannot add Mirror Image: not a collectible card.\"",
        token != null && !token.Collectible && tokenReason == "not-collectible" && tokenDeck.SequenceEqual(new[] { "not-collectible CS2_mirror" })
            && tokenAdd == "Cannot add Mirror Image: not a collectible card.",
        (token == null ? "no card" : "collectible " + token.Collectible) + " / " + (tokenReason ?? "null") + " / " + string.Join("|", tokenDeck) + " / " + tokenAdd);

    var run = new DungeonRun(data);
    // Every Heist/Tombs generated deck keeps its unlock key in data; the win summary never names one (it shows as Build Deck).
    var generated = data.Decks.Where(d => d.IsBuildable && d.Unlock != null).ToList();
    var named = generated.Where(d => (DungeonRunCatalog.UnlockName(run, d.Unlock) ?? "").Contains(d.Name)).Select(d => d.Id + ":" + DungeonRunCatalog.UnlockName(run, d.Unlock)).ToList();
    string rakanishu = DungeonRunCatalog.UnlockName(run, "dala_rakanishu_win");
    Check($"BD18 UnlockName names no build-able deck: \"dala_rakanishu_win\" names Rakanishu's power, not \"Random Mage Deck\", and none of the {generated.Count} "
        + "generated decks with an unlock key is named; a fixed deck's unlock is still named",
        generated.Count > 0 && named.Count == 0 && rakanishu != null && !rakanishu.Contains("deck)") && rakanishu.Contains("(Rakanishu power)")
            && DungeonRunCatalog.UnlockName(run, "mage_floor3") == "Kindled Sparks (Mage deck)",
        (rakanishu ?? "null") + " | " + string.Join(" | ", named));

    var fresh = new DungeonRunProfile();
    var heist = run.NewRun(fresh, "DALA_Rakanishu", "heist_mage_random", data.FindClass("DALA_Rakanishu").Powers[0], 4260, builtCards: mageList);
    var builtMage = run.NewRun(fresh, "MAGE", "mage_build", data.FindClass("MAGE").Powers[0], 4260, builtCards: mageList);
    var plain = run.NewRun(fresh, "MAGE", "mage_rimeward", data.FindClass("MAGE").Powers[0], 4260);
    var legacy = new DungeonRunState { Class = "MAGE" };
    string[] deckNames =
    {
        DungeonRunCatalog.RunDeckName(run, heist), DungeonRunCatalog.RunDeckName(run, builtMage), DungeonRunCatalog.RunDeckName(run, plain),
        DungeonRunCatalog.RunDeckName(run, legacy) ?? "null", DungeonRunCatalog.RunDeckName(run, null) ?? "null",
    };
    Check("BD19 RunDeckName: a built Heist run (StarterDeck heist_mage_random) and a built MAGE run are \"Build Deck\", a plain run its deck's name, a run "
        + "with no starter deck id and no run null",
        heist.StarterDeck == "heist_mage_random" && deckNames.SequenceEqual(new[] { "Build Deck", "Build Deck", data.FindDeck("mage_rimeward").Name, "null", "null" }),
        string.Join(" | ", deckNames));

    // The run start re-checks the list against the live collection: a delegate that no longer owns a second Dalaran Aspirant.
    string Name(string id) { try { return SabberStoneCore.Model.Cards.FromId(id)?.Name; } catch (Exception) { return null; } }
    var dropped = DungeonRunBuild.DeckReasons(mageList, mage, 10, id => id == "AT_006" ? 1 : 2, null, pol);
    string droppedText = NativeDungeonRunText.BuildStartRefusal(dropped, Name);
    string aspirant = Name("AT_006");
    string[] texts =
    {
        droppedText,
        NativeDungeonRunText.BuildStartRefusal(new[] { "size 9 of 10", "copies AT_006", "legendary AT_009", "runes" }, Name),
        NativeDungeonRunText.BuildStartRefusal(new[] { "class AT_064", "not-collectible CS2_mirror", "hero BOT_238", "fabled TIME_009", "rune-class CATA_156" }, Name),
        NativeDungeonRunText.BuildStartRefusal(new[] { "missing XX_none", "admission:secret-generated AT_006" }, id => null),
        NativeDungeonRunText.BuildStartRefusal(new string[0], Name) ?? "null",
    };
    string[] wantTexts =
    {
        "That deck cannot start a run: " + aspirant + ": you no longer own that many copies.",
        "That deck cannot start a run: it has 9 of 10 cards; " + aspirant + ": at most 2 copies of a card; " + Name("AT_009") + ": at most 1 copy of a legendary; "
            + "a Death Knight deck needs at most 3 runes.",
        "That deck cannot start a run: " + Name("AT_064") + ": not a card of this hero's class; Mirror Image: not a collectible card; " + Name("BOT_238")
            + ": a hero card; " + Name("TIME_009") + ": a Fabled card; " + Name("CATA_156") + ": a rune card, only in a Death Knight deck.",
        "That deck cannot start a run: a card: an unknown card; a card: not allowed in a run.",
        "null",
    };
    Check("BD20 BuildStartRefusal words the run-start reasons with card names, no engine ids or codes: a collection that no longer owns a second Dalaran "
        + "Aspirant gives \"That deck cannot start a run: Dalaran Aspirant: you no longer own that many copies.\"; every DeckReasons code has words; no reasons is null",
        dropped.SequenceEqual(new[] { "owned AT_006" }) && aspirant == "Dalaran Aspirant" && texts.SequenceEqual(wantTexts)
            && texts.Take(3).All(t => !t.Contains("AT_") && !t.Contains("CS2_") && !t.Contains("BOT_") && !t.Contains("TIME_") && !t.Contains("CATA_")),
        string.Join(" | ", texts));
});

// Build Deck owner fix (2026-11-26): a Core reprint and its original are one plank. The owner's Mage builder showed "Elven Archer
// (1) 0/2" twice (CS2_189 and its reprint CORE_CS2_189, one copy group).
Guard("BDM", () =>
{
    var pol = OfflinePoolPolicy.Load();
    var mage = data.FindClass("MAGE").ClassSet;
    RunBuildCard C(string id, string name, int cost, int owned, string group, bool legendary = false) =>
        new RunBuildCard { Id = id, ClientId = id, Name = name, Cost = cost, Owned = owned, Legendary = legendary, CopyGroup = group };
    var raw = new List<RunBuildCard>
    {
        C("CORE_CS2_189", "Elven Archer", 1, 1, "CS2_189"), C("CS2_189", "Elven Archer", 1, 1, "CS2_189"),
        C("AT_082", "Lowly Squire", 1, 2, "AT_082"),
        C("CORE_X_LEG", "Rhonin", 8, 1, "AT_009", true), C("AT_009", "Rhonin", 8, 1, "AT_009"),
    };
    var planks = NativeDungeonRunText.BuildMergePrintings(raw);
    var archer = planks.First(p => p.Name == "Elven Archer");
    var squire = planks.First(p => p.Name == "Lowly Squire");
    var rhonin = planks.First(p => p.Name == "Rhonin");
    var page = NativeDungeonRunText.BuildPage(planks, NativeDungeonRunText.BuildFilter.All, 0, 6).Select(p => p.Name).ToList();

    // Click the archer plank three times the way the run menu does: pick a printing, then DungeonRunBuild.AddReason with per-id
    // owned counts and the copy groups of the raw candidates.
    var byId = raw.ToDictionary(c => c.Id, StringComparer.Ordinal);
    var built = new List<string>();
    var refusals = new List<string>();
    var labels = new List<string> { NativeDungeonRunText.BuildCardLabel(archer, NativeDungeonRunText.BuildInDeck(archer, built)) };
    for (int i = 0; i < 3; i++)
    {
        string pick = NativeDungeonRunText.BuildPickPrinting(archer, built);
        string reason = DungeonRunBuild.AddReason(built, pick, mage, 10, id => byId.TryGetValue(id, out var c) ? c.Owned : 0,
            id => byId.TryGetValue(id, out var c) ? c.CopyGroup : id, pol);
        if (reason == null) built.Add(pick); else refusals.Add(reason);
        labels.Add(NativeDungeonRunText.BuildCardLabel(archer, NativeDungeonRunText.BuildInDeck(archer, built)));
    }
    Check("BD21 BuildMergePrintings makes one plank per copy group: Elven Archer (CS2_189 + CORE_CS2_189, 1 owned each) is one plank with id CS2_189, owned 2, "
        + "printings CS2_189 then CORE_CS2_189; a one-printing card is unchanged; a group is legendary when any printing is; the page lists each name once. "
        + "Three clicks add CS2_189 then CORE_CS2_189 (labels 0/2, 1/2, 2/2) and the third is refused for the shared copy limit, never for ownership",
        planks.Count == 3 && archer.Id == "CS2_189" && archer.Owned == 2 && archer.CopyGroup == "CS2_189"
            && archer.Printings != null && archer.Printings.Select(p => p.Id).SequenceEqual(new[] { "CS2_189", "CORE_CS2_189" })
            && ReferenceEquals(squire, raw[2]) && squire.Printings == null && rhonin.Legendary && rhonin.Owned == 2 && rhonin.Id == "AT_009"
            && NativeDungeonRunText.BuildCopyLimit(rhonin) == 1
            && page.SequenceEqual(new[] { "Elven Archer", "Lowly Squire", "Rhonin" })
            && built.SequenceEqual(new[] { "CS2_189", "CORE_CS2_189" }) && refusals.Count == 1 && refusals[0].StartsWith("copies ")
            && labels.SequenceEqual(new[] { "Elven Archer (1) 0/2", "Elven Archer (1) 1/2", "Elven Archer (1) 2/2", "Elven Archer (1) 2/2" }),
        string.Join(",", planks.Select(p => p.Id + ":" + p.Owned)) + " | " + string.Join(",", page) + " | built " + string.Join(",", built)
            + " | refused " + string.Join(",", refusals) + " | " + string.Join(" / ", labels));
});

// Build Deck editor decisions (design 2026-11-26-dungeon-run-build-deck-editor-design.md; plugin/DungeonRun/DungeonRunEditor.cs).
Guard("BDE", () =>
{
    // fixture: every literal loaner/test deck id of NativeUIProjection.cs:374-500 (DeckKeys is private to an untested file)
    var nativeFixture = new long[]
    {
        900000001L, 900000002L, 900000010L, 900000011L, 900000012L, 900000013L, 900000014L, 900000015L, 900000016L, 900000017L, 900000018L,
        900000019L, 900000020L, 900000021L, 900000022L, 900000023L, 900000024L, 900000025L, 900000026L, 900000027L, 900000028L, 900000029L,
        900000030L, 900000031L, 900000032L, 900000033L, 900000034L, 900000035L, 900000036L, 900000037L, 900000038L, 900000039L, 900000040L,
        900000041L, 900000042L, 900000043L, 900000044L, 900000045L, 900000046L, 900000047L, 900000048L, 900000049L, 900000050L, 900000051L,
        900000052L, 900000053L, 900000054L, 900000055L, 900000056L, 900000057L, 900000058L, 900000059L, 900000060L, 900000061L, 900000062L,
        900000063L, 900000064L, 900000065L, 900000066L, 900000067L, 900000068L, 900000069L, 900000070L, 900000071L, 900000072L, 900000073L,
        900000074L, 900000075L, 900000076L, 900000077L, 900000078L, 900000079L, 900000130L, 900000131L, 900000132L, 900000133L, 900000134L,
        900000135L, 900000150L, 900000151L, 900000152L, 900000153L, 900000154L, 900000155L, 900000156L, 900000158L, 900000170L, 900000171L,
        900000172L, 900000173L, 900000174L, 900000175L, 900000176L, 900000177L, 900000178L, 900000200L, 900000201L, 900000202L, 900000203L,
        900000204L, 900000205L, 900000206L, 900000207L, 900000208L, 900000209L, 900000210L, 900000211L, 900000212L, 900000213L, 900000214L,
        900000215L, 900000216L, 900000217L, 900000218L, 900000219L, 900000220L, 900000221L, 900000222L, 900000223L, 900000224L, 900000225L
    };
    var band = Enumerable.Range(0, 100).Select(i => 900000300L + i).ToList();
    Check("BD22 (A1) the run-build band is 900000300..900000399: 900000299 and 900000400 are outside, 900000300 and 900000399 inside, the legacy personal ids 910000001 and "
        + "910000010 outside, and none of the " + nativeFixture.Length + " loaner/test ids of NativeUIProjection.cs:374-500 is inside; NextRunBuildId gives the lowest free id "
        + "(900000302 after 300 and 301), null on a full band; NonBandCount([900000300, 910000010, 900000215]) is 2",
        !DungeonRunEditor.IsRunBuildId(900000299) && !DungeonRunEditor.IsRunBuildId(900000400)
            && DungeonRunEditor.IsRunBuildId(900000300) && DungeonRunEditor.IsRunBuildId(900000399)
            && !DungeonRunEditor.IsRunBuildId(910000001) && !DungeonRunEditor.IsRunBuildId(910000010)
            && nativeFixture.Length > 100 && nativeFixture.All(id => !DungeonRunEditor.IsRunBuildId(id))
            && DungeonRunEditor.NextRunBuildId(new long[] { 900000300, 900000301 }) == 900000302
            && DungeonRunEditor.NextRunBuildId(nativeFixture) == 900000300
            && DungeonRunEditor.NextRunBuildId(band) == null
            && DungeonRunEditor.NonBandCount(new long[] { 900000300, 910000010, 900000215 }) == 2,
        "fixture " + nativeFixture.Length);

    var verdicts = new[] { DungeonRunEditor.Stage.None, DungeonRunEditor.Stage.Creating, DungeonRunEditor.Stage.Entering,
        DungeonRunEditor.Stage.Editing, DungeonRunEditor.Stage.Leaving }.Select(DungeonRunEditor.AddVerdict).ToArray();
    Check("BD23 (D3, J, K belt) flow tables: AddVerdict is Refuse, Allow, Refuse, Rule, Refuse for None, Creating, Entering, Editing, Leaving; LeaveTarget sends Done to Power and "
        + "Back and Recover to Deck; PowerBack follows the four rows of J2; ExitAllowed (false,true,false,true) is true, (false,true,false,false) false, (false,false,true,true) false, "
        + "(true,false,false,false) true",
        verdicts.SequenceEqual(new[] { DungeonRunEditor.AddVerdictKind.Refuse, DungeonRunEditor.AddVerdictKind.Allow, DungeonRunEditor.AddVerdictKind.Refuse,
            DungeonRunEditor.AddVerdictKind.Rule, DungeonRunEditor.AddVerdictKind.Refuse })
            && DungeonRunEditor.LeaveTarget(DungeonRunEditor.Leave.Done) == DungeonRunEditor.LeaveStep.Power
            && DungeonRunEditor.LeaveTarget(DungeonRunEditor.Leave.Back) == DungeonRunEditor.LeaveStep.Deck
            && DungeonRunEditor.LeaveTarget(DungeonRunEditor.Leave.Recover) == DungeonRunEditor.LeaveStep.Deck
            && DungeonRunEditor.PowerBack(true, true, true) == DungeonRunEditor.PowerBackTarget.Editor
            && DungeonRunEditor.PowerBack(true, true, false) == DungeonRunEditor.PowerBackTarget.PlankBuild
            && DungeonRunEditor.PowerBack(false, true, true) == DungeonRunEditor.PowerBackTarget.PlankBuild
            && DungeonRunEditor.PowerBack(false, true, false) == DungeonRunEditor.PowerBackTarget.PlankBuild
            && DungeonRunEditor.PowerBack(true, false, true) == DungeonRunEditor.PowerBackTarget.Deck
            && DungeonRunEditor.PowerBack(false, false, false) == DungeonRunEditor.PowerBackTarget.Deck
            && DungeonRunEditor.ExitAllowed(false, true, false, true) && !DungeonRunEditor.ExitAllowed(false, true, false, false)
            && !DungeonRunEditor.ExitAllowed(false, false, true, true) && DungeonRunEditor.ExitAllowed(true, false, false, false),
        string.Join(",", verdicts));

    var recoverBad = new List<string>();
    foreach (DungeonRunEditor.Stage st in Enum.GetValues(typeof(DungeonRunEditor.Stage)))
        foreach (bool hub in new[] { false, true })
            foreach (bool once in new[] { false, true })
            {
                var want = once || st == DungeonRunEditor.Stage.None ? DungeonRunEditor.RecoverKind.None
                    : st == DungeonRunEditor.Stage.Creating || st == DungeonRunEditor.Stage.Entering && hub ? DungeonRunEditor.RecoverKind.RemoveAndPlank
                    : DungeonRunEditor.RecoverKind.DeckAndLeave;
                var got = DungeonRunEditor.RecoverAction(st, hub, once);
                if (got != want) recoverBad.Add(st + "/" + hub + "/" + once + "=" + got);
            }
    Check("BD24 (L) RecoverAction over stage x atHub x recoveredOnce: recoveredOnce and the None stage give None; Creating, and Entering at the HUB, give RemoveAndPlank; "
        + "Entering in CM, Editing and Leaving give DeckAndLeave (20 cases)",
        recoverBad.Count == 0 && DungeonRunEditor.RecoverAction(DungeonRunEditor.Stage.Creating, false, false) == DungeonRunEditor.RecoverKind.RemoveAndPlank
            && DungeonRunEditor.RecoverAction(DungeonRunEditor.Stage.Entering, true, false) == DungeonRunEditor.RecoverKind.RemoveAndPlank
            && DungeonRunEditor.RecoverAction(DungeonRunEditor.Stage.Entering, false, false) == DungeonRunEditor.RecoverKind.DeckAndLeave
            && DungeonRunEditor.RecoverAction(DungeonRunEditor.Stage.Editing, true, false) == DungeonRunEditor.RecoverKind.DeckAndLeave
            && DungeonRunEditor.RecoverAction(DungeonRunEditor.Stage.Leaving, false, false) == DungeonRunEditor.RecoverKind.DeckAndLeave
            && DungeonRunEditor.RecoverAction(DungeonRunEditor.Stage.Editing, false, true) == DungeonRunEditor.RecoverKind.None,
        string.Join(",", recoverBad));

    var mageRow = data.FindClass("MAGE");
    var dualRow = data.FindClass("MAGE_ROGUE");
    var dkRow = data.FindClass("DEATHKNIGHT");
    var tessRow = data.FindClass("GILA_500h3");
    var rows = new[] { mageRow, dualRow, dkRow, tessRow };
    var B = DungeonRunEditor.BuilderKind.Plank;
    var E = DungeonRunEditor.BuilderKind.Editor;
    // (row, candidates, reachable, size, nativeDecks, nativeLimit, editorFailed, gate)
    Check("BD25 (M) Builder: the all-clear Mage row is Editor; each Plank condition alone gives Plank (editorFailed, 0 candidates, reachable 9 of 10, 111 of 111 native decks, "
        + "a dual row and a Death Knight row with the gate off); the dual and Death Knight rows give Editor with the gate on",
        rows.All(r => r != null)
            && DungeonRunEditor.Builder(mageRow, 40, 10, data.Rules.StarterSize, 5, 111, false, false) == E
            && DungeonRunEditor.Builder(mageRow, 40, 10, data.Rules.StarterSize, 5, 111, true, false) == B
            && DungeonRunEditor.Builder(mageRow, 0, 10, data.Rules.StarterSize, 5, 111, false, false) == B
            && DungeonRunEditor.Builder(mageRow, 40, 9, data.Rules.StarterSize, 5, 111, false, false) == B
            && DungeonRunEditor.Builder(mageRow, 40, 10, data.Rules.StarterSize, 111, 111, false, false) == B
            && DungeonRunEditor.Builder(mageRow, 40, 10, data.Rules.StarterSize, 110, 111, false, false) == E
            && dualRow.IsDual && DungeonRunEditor.Builder(dualRow, 40, 10, data.Rules.StarterSize, 5, 111, false, false) == B
            && dkRow.ClassSet.Contains("DEATHKNIGHT") && DungeonRunEditor.Builder(dkRow, 40, 10, data.Rules.StarterSize, 5, 111, false, false) == B
            && DungeonRunEditor.Builder(dualRow, 40, 10, data.Rules.StarterSize, 5, 111, false, true) == E
            && DungeonRunEditor.Builder(dkRow, 40, 10, data.Rules.StarterSize, 5, 111, false, true) == E
            && DungeonRunEditor.Builder(tessRow, 40, 10, data.Rules.StarterSize, 5, 111, false, false) == E
            && !DungeonRunEditor.DualAndDkInEditor,
        "gate " + DungeonRunEditor.DualAndDkInEditor);

    Func<string, string> same = s => s;
    var tessHeroes = DungeonRunEditor.HeaderHeroes(tessRow, data, same);
    var mageHeroes = DungeonRunEditor.HeaderHeroes(mageRow, data, same);
    var dualHeroes = DungeonRunEditor.HeaderHeroes(dualRow, data, same);
    var tessClassHero = data.FindClass(tessRow.ClassSet[0]).HeroCard;
    var mageRogueTags = DungeonRunEditor.ClassTags(dualRow);
    Check("BD26 (A2, F) HeaderHeroes: Tess (GILA_500h3, classes [ROGUE]) is [GILA_500h3, the ROGUE class row's hero]; the MAGE row is its hero only; the MAGE_ROGUE row is "
        + "[ULDA_Reno, the MAGE hero]; a clientId mapping is applied; HeroAccepted(MAGE, [MAGE], MAGE row) is true, NEUTRAL and WARRIOR are false, (MAGE, [MAGE, ROGUE], MAGE_ROGUE row) "
        + "is true; ClassTags of the MAGE_ROGUE row is [MAGE, ROGUE]",
        tessRow.HeroCard == "GILA_500h3" && tessClassHero == data.FindClass("ROGUE").HeroCard && tessClassHero != tessRow.HeroCard
            && tessHeroes.SequenceEqual(new[] { "GILA_500h3", tessClassHero })
            && mageHeroes.SequenceEqual(new[] { mageRow.HeroCard })
            && dualHeroes.SequenceEqual(new[] { dualRow.HeroCard, data.FindClass("MAGE").HeroCard })
            && DungeonRunEditor.HeaderHeroes(mageRow, data, s => "C_" + s).SequenceEqual(new[] { "C_" + mageRow.HeroCard })
            && DungeonRunEditor.HeroAccepted("MAGE", new[] { "MAGE" }, mageRow)
            && !DungeonRunEditor.HeroAccepted("NEUTRAL", new[] { "MAGE" }, mageRow)
            && !DungeonRunEditor.HeroAccepted("WARRIOR", new[] { "MAGE" }, mageRow)
            && !DungeonRunEditor.HeroAccepted("MAGE", new[] { "MAGE", "ROGUE" }, mageRow)
            && DungeonRunEditor.HeroAccepted("MAGE", new[] { "MAGE", "ROGUE" }, dualRow)
            && mageRogueTags.SequenceEqual(new[] { "MAGE", "ROGUE" }) && DungeonRunEditor.ClassTags(mageRow).SequenceEqual(new[] { "MAGE" }),
        string.Join(",", tessHeroes) + " | " + string.Join(",", dualHeroes));

    var all = new[] { true, true, true, true, true, true, true, true };
    bool Owns(bool[] a) => DungeonRunEditor.OwnsEditor(a[0], a[1], a[2], a[3], a[4], a[5], a[6], a[7]);
    bool eachFalse = true;
    for (int i = 0; i < all.Length; i++)
    {
        var a = (bool[])all.Clone();
        a[i] = false;
        if (Owns(a)) eachFalse = false;
    }
    Check("BD27 (H0) OwnsEditor is true with all eight inputs true and false when any single input is false", Owns(all) && eachFalse);
});

Console.WriteLine($"RESULT {passes} passed, {failures} failed");
