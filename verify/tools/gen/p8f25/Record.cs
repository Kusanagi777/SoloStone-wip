using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

// The recorder of c-enc-25 section L, built by record_replay.sh inside an export of the pre-close-out tree (wave 6) with the
// suite's own Replay.cs: it writes the eight version-6 saves (Replay.Starts: new runs with no named schedule, then acts-v2,
// walked to a floor) and replay.txt, the replay of the two version-5 golden saves and the eight saves read back from disk.
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
var files = new List<string> { Path.Combine(VerifyPaths.Fixture("p8f01"), "golden-save-legacy-v5.xml"), Path.Combine(VerifyPaths.Fixture("p8f01"), "golden-save-acts-v5.xml") };
for (int i = 0; i < Replay.Starts.Length; i++)
{
    string path = Path.Combine(outDir, $"pre-closeout-{i + 1}.xml");
    var save = Replay.Start(run, i);
    if (save.Active.Schedule != "acts-v2") throw new InvalidOperationException("not the pre-close-out default: " + save.Active.Schedule);
    Replay.Write(path, save);
    files.Add(path);
}
var lines = new List<string>();
foreach (string f in files) lines.AddRange(Replay.Walk(run, f));
File.WriteAllText(Path.Combine(outDir, "replay.txt"), string.Join("\n", lines) + "\n", new System.Text.UTF8Encoding(false));
Console.WriteLine($"recorded {files.Count} replays, {lines.Count} lines, in {outDir}");
