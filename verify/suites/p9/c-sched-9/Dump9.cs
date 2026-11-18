using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

/// <summary>
/// c-sched-9's dump of the parsed released objects. This file uses only APIs the tree before step 9 (f62275b) has: it is
/// compiled both into c-sched-9 and, by verify/tools/gen/p9/record_sched9.sh, against the f62275b export, where
/// <see cref="Record"/> writes the reference verify/fixtures/p9/sched9-f62275b.txt.
/// </summary>
internal static class Sched9
{
    internal static readonly string[] Released = { "acts-v1", "acts-v2", "act1", "acts-v3h", "acts-v3t" };

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

    internal static string R(string f) => File.ReadAllText(Path.Combine(VerifyPaths.Data, f));

    internal static DungeonRunData Load()
    {
        NpcWorldJson.Parser = text => { using var doc = JsonDocument.Parse(text); return FromJson(doc.RootElement); };
        return DungeonRunData.Parse(R("dungeon-run/run-v1.json"), R("dungeon-run/pool-v1.json"), R("npc/decks-v1.json"),
            R("dungeon-run/catalog-v1.json"), R("dungeon-run/schedule-v1.json"), R("dungeon-run/tavern-v1.json"), R("dungeon-run/encounters-v1.json"));
    }

    static string Inv(object o) => o switch
    {
        null => "null",
        string s => s.Replace("\\", "\\\\").Replace("\n", "\\n").Replace("\r", "\\r"),
        bool b => b ? "true" : "false",
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => o.ToString(),
    };

    static bool Scalar(Type t) => t.IsPrimitive || t.IsEnum || t == typeof(string) || t == typeof(decimal) || Nullable.GetUnderlyingType(t) != null;

    /// <summary>
    /// One line per scalar: ".Field=value"; a list of scalars on one line (".Pools=[a|b]"); a list of objects as ".X#=count" and
    /// ".X[i]..."; a dictionary as ".X{#}=count" and ".X{key}...". Instance fields only (an auto-property's backing field by the
    /// property's name), in ordinal name order; private fields (caches such as a schedule's flattened nodes) are left out.
    /// </summary>
    internal static void Dump(object o, string path, List<string> lines, int depth)
    {
        if (depth > 12) { lines.Add(path + "=(depth)"); return; }
        if (o == null) { lines.Add(path + "=null"); return; }
        var t = o.GetType();
        if (Scalar(t)) { lines.Add(path + "=" + Inv(o)); return; }
        if (o is IDictionary dict)
        {
            var byKey = dict.Keys.Cast<object>().ToDictionary(k => Inv(k), k => dict[k], StringComparer.Ordinal);
            lines.Add(path + "{#}=" + byKey.Count);
            foreach (string k in byKey.Keys.OrderBy(k => k, StringComparer.Ordinal)) Dump(byKey[k], path + "{" + k + "}", lines, depth + 1);
            return;
        }
        if (o is IEnumerable seq)
        {
            var items = seq.Cast<object>().ToList();
            Type et = t.IsArray ? t.GetElementType() : t.IsGenericType ? t.GetGenericArguments()[0] : typeof(object);
            if (Scalar(et)) { lines.Add(path + "=[" + string.Join("|", items.Select(Inv)) + "]"); return; }
            lines.Add(path + "#=" + items.Count);
            for (int i = 0; i < items.Count; i++) Dump(items[i], path + "[" + i + "]", lines, depth + 1);
            return;
        }
        foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(f => !f.IsPrivate || f.Name.Contains("k__BackingField")).OrderBy(f => f.Name, StringComparer.Ordinal))
        {
            string name = f.Name.Contains("k__BackingField") ? f.Name.Substring(1, f.Name.IndexOf('>') - 1) : f.Name;
            Dump(f.GetValue(o), path + "." + name, lines, depth + 1);
        }
    }

    internal static List<string> DumpOf(object o) { var l = new List<string>(); Dump(o, "", l, 0); return l; }

    /// <summary>A dump line's field path with every list index and dictionary key removed ("Acts[2].BossHealth" to "Acts[].BossHealth").</summary>
    internal static string Strip(string line)
    {
        string path = line.Substring(0, line.IndexOf('='));
        var sb = new StringBuilder();
        for (int i = 0; i < path.Length; i++)
        {
            char c = path[i];
            if (c == '[' && i + 1 < path.Length && path[i + 1] != ']') { sb.Append("[]"); i = path.IndexOf(']', i); continue; }
            if (c == '{' && i + 1 < path.Length && path[i + 1] != '#') { sb.Append("{}"); i = path.IndexOf('}', i); continue; }
            sb.Append(c);
        }
        return sb.ToString();
    }

    /// <summary>The first 8 bytes of SHA-256 over the lines (LF-joined, LF-terminated), as 16 hex digits.</summary>
    internal static string Hash(IEnumerable<string> lines)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(string.Join("\n", lines) + "\n"));
        return string.Concat(bytes.Take(8).Select(b => b.ToString("X2", CultureInfo.InvariantCulture)));
    }

    internal static string Kind(string key) => key.Substring(0, key.IndexOf(':'));

    /// <summary>
    /// The parsed objects by "kind:id": each released schedule (with its flattened nodes as ".Nodes"), the pool sets, every
    /// modifier, every tavern option, the tavern table (without its options), every treasure and every boss row.
    /// </summary>
    internal static SortedDictionary<string, List<string>> Objects(DungeonRunData data)
    {
        var objects = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (string id in Released)
        {
            DungeonRunSchedule sc;
            if (!data.Schedules.Schedules.TryGetValue(id, out sc)) continue;
            var lines = DumpOf(sc);
            var nodes = sc.Nodes.ToList();
            lines.Add(".Nodes#=" + nodes.Count);
            for (int i = 0; i < nodes.Count; i++) Dump(nodes[i], ".Nodes[" + i + "]", lines, 1);
            objects["schedule:" + id] = lines;
        }
        objects["poolsets:"] = DumpOf(data.Schedules.PoolSets);
        foreach (var m in data.Schedules.Modifiers) objects["modifier:" + m.Id] = DumpOf(m);
        foreach (var o in data.Tavern.Options) objects["tavern:" + o.Id] = DumpOf(o);
        objects["tavern-table:"] = DumpOf(data.Tavern).Where(l => !l.StartsWith(".Options", StringComparison.Ordinal)).ToList();
        foreach (var t in data.Treasures) objects["treasure:" + t.Id] = DumpOf(t);
        foreach (var b in data.Bosses) objects["boss:" + b.Id] = DumpOf(b);
        return objects;
    }

    static string Canon(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.Object => "{" + string.Join(",", e.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal).Select(p => JsonSerializer.Serialize(p.Name) + ":" + Canon(p.Value))) + "}",
        JsonValueKind.Array => "[" + string.Join(",", e.EnumerateArray().Select(Canon)) + "]",
        _ => e.GetRawText(),
    };

    /// <summary>The hash of each released schedule object of schedule-v1.json, canonical (keys sorted, no whitespace), "version" left out.</summary>
    internal static SortedDictionary<string, string> Raw(string scheduleJson)
    {
        var raw = new SortedDictionary<string, string>(StringComparer.Ordinal);
        using var doc = JsonDocument.Parse(scheduleJson);
        foreach (var p in doc.RootElement.GetProperty("schedules").EnumerateObject().Where(p => Released.Contains(p.Name)))
            raw[p.Name] = Hash(new[] { "{" + string.Join(",", p.Value.EnumerateObject().Where(x => x.Name != "version").OrderBy(x => x.Name, StringComparer.Ordinal)
                .Select(x => JsonSerializer.Serialize(x.Name) + ":" + Canon(x.Value))) + "}" });
        return raw;
    }

    /// <summary>Writes the reference: the field paths per kind, each object's hash, each raw schedule's hash, the stamps and ReleasedShapes.</summary>
    internal static void Record(string file)
    {
        var data = Load();
        var objects = Objects(data);
        var raw = Raw(R("dungeon-run/schedule-v1.json"));
        var outLines = new List<string> { "# c-sched-9 reference: the parsed released objects of the tree before step 9 (written by verify/tools/gen/p9/record_sched9.sh; never hand-edited)" };
        foreach (var g in objects.GroupBy(p => Kind(p.Key)).OrderBy(g => g.Key, StringComparer.Ordinal))
            foreach (string f in g.SelectMany(p => p.Value.Select(Strip)).Distinct().OrderBy(x => x, StringComparer.Ordinal)) outLines.Add("field " + g.Key + " " + f);
        foreach (var p in objects) outLines.Add("hash " + p.Key + " " + Hash(p.Value));
        foreach (var p in raw) outLines.Add("raw " + p.Key + " " + p.Value);
        foreach (string id in Released) outLines.Add("stamp " + id + " " + data.Schedules.Schedules[id].StampVersion.ToString(CultureInfo.InvariantCulture));
        foreach (var v in DungeonRunSchedules.ReleasedShapes.OrderBy(v => v.Key))
            foreach (var s in v.Value.OrderBy(s => s.Key, StringComparer.Ordinal)) outLines.Add("shape " + v.Key.ToString(CultureInfo.InvariantCulture) + " " + s.Key + " " + s.Value);
        File.WriteAllText(file, string.Join("\n", outLines) + "\n", new UTF8Encoding(false));
        Console.WriteLine("recorded " + objects.Count + " objects, " + raw.Count + " raw schedules into " + file);
    }
}
