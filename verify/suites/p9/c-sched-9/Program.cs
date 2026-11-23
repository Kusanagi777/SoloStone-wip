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

Console.WriteLine($"RESULT {passes} passed, {failures} failed");
