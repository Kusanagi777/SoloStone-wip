using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

// The recorder of c-enc-25 section L4 (step 9, plan D5), built by record_replay_v3.sh inside an export of the tree before step 9
// (f62275b) with the suite's frozen verify/suites/p8f25/c-enc-25/ReplayV3.cs: it writes the eight version-6 saves of
// ReplayV3.Stops (runs of acts-v3h and acts-v3t, each saved at its stop) and replay.txt, the replay of the eight saves read back
// from disk, and exclusions.txt, the export's own exclusion data (every rule's and boss row's exclusion keys as that tree parsed
// them), which c-enc-25 L4 reads to tell a step-9 exclusion from one the released schedule already had.
// Usage (inside the export): dotnet out/AiHarness.dll <output folder>
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
string root = VerifyPaths.Data;
string R(string f) => File.ReadAllText(Path.Combine(root, f));
var data = DungeonRunData.Parse(R("dungeon-run/run-v1.json"), R("dungeon-run/pool-v1.json"), R("npc/decks-v1.json"), R("dungeon-run/catalog-v1.json"),
    R("dungeon-run/schedule-v1.json"), R("dungeon-run/tavern-v1.json"), R("dungeon-run/encounters-v1.json"));
var run = new DungeonRun(data);
string outDir = args[0];
Directory.CreateDirectory(outDir);
var files = new List<string>();
for (int i = 0; i < ReplayV3.Stops.Length; i++)
{
    string path = Path.Combine(outDir, ReplayV3.FileName(i));
    var save = ReplayV3.Start(run, i);
    if (save.Active.Schedule != ReplayV3.Stops[i].Schedule || save.Active.ScheduleVersion != 3)
        throw new InvalidOperationException("not a stamp-3 run of " + ReplayV3.Stops[i].Schedule + ": " + save.Active.Schedule + " v" + save.Active.ScheduleVersion);
    ReplayV3.Write(path, save);
    Console.WriteLine($"stop {i + 1} {ReplayV3.Stops[i].Name}: {save.Active.Schedule} {save.Active.Class} seed {save.Active.Seed} floor {save.Active.Floor} node {save.Active.Node} {save.Active.Phase}");
    files.Add(path);
}
var lines = new List<string>();
foreach (string f in files) lines.AddRange(ReplayV3.Walk(run, f));
File.WriteAllText(Path.Combine(outDir, "replay.txt"), string.Join("\n", lines) + "\n", new System.Text.UTF8Encoding(false));
string J(IEnumerable<string> ids) => string.Join(",", ids.OrderBy(x => x, StringComparer.Ordinal));
var table = new List<string>();
foreach (var r in data.Schedules.Rules)
    table.Add($"rule {r.Id} kind {r.Kind} scope {r.Scope} flags {J(r.IncompatibleFlags)} rules {J(r.IncompatibleRules)} modifiers {J(r.IncompatibleModifiers)} classes {J(r.IncompatibleClasses)} noBoardShrine {r.NoBoardShrine}");
foreach (var b in data.Bosses)
    table.Add($"boss {b.Id} flags {J(b.Flags)} rules {J(b.IncompatibleRules)} modifiers {J(b.IncompatibleModifiers)}");
File.WriteAllText(Path.Combine(outDir, "exclusions.txt"), string.Join("\n", table) + "\n", new System.Text.UTF8Encoding(false));
Console.WriteLine($"recorded {files.Count} replays, {lines.Count} lines, in {outDir}");
