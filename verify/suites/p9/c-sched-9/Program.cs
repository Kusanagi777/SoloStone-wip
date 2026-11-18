using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

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

Console.WriteLine($"RESULT {passes} passed, {failures} failed");
