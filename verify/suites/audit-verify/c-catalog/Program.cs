// c-catalog: the shared draft catalog (phase 3 I1): tools/dungeon-run-catalog, data/dungeon-run/catalog-v1.json and its
// loader/validator in the run layer (DungeonRunData.Parse with a catalog, DungeonRunValidator pool entries,
// DropClientUnknown). Compiles C:\SoloStone\plugin\DungeonRun\*.cs (minus the client-only runtime and boss pilot) against
// audit-verify/engine-out, the engine the tool was run with.
// usage: AiHarness [toolExe] [scratchDir]
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using SabberStoneCore.CardSets.DungeonRun;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;

int failures = 0, passes = 0;
void Check(string name, bool ok, string detail = "")
{
    if (ok) passes++; else failures++;
    if (!ok || Environment.GetEnvironmentVariable("VERBOSE") == "1")
        Console.WriteLine((ok ? "PASS " : "FAIL ") + name + (detail.Length > 0 ? "  (" + detail + ")" : ""));
}

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

string repo = VerifyPaths.Root;
string dataDir = Path.Combine(repo, "data", "dungeon-run");
// verify port: the tool is the build of `verify/bin/builds.sh tool` (verify/.out/tools/dungeon-run-catalog), started
// through the running dotnet host; its scratch output goes to verify/.out/tmp/c-catalog-tool-out.
string tool = args.Length > 0 ? args[0] : VerifyPaths.Out("tools/dungeon-run-catalog/DungeonRunCatalogTool.dll");
string scratch = args.Length > 1 ? args[1] : VerifyPaths.Out("tmp/c-catalog-tool-out");
Directory.CreateDirectory(scratch);
string runJson = File.ReadAllText(Path.Combine(dataDir, "run-v1.json"));
string poolJson = File.ReadAllText(Path.Combine(dataDir, "pool-v1.json"));
string npcJson = File.ReadAllText(Path.Combine(repo, "data", "npc", "decks-v1.json"));
string catalogPath = Path.Combine(dataDir, "catalog-v1.json");
byte[] catalogBytes = File.ReadAllBytes(catalogPath);
string catalogJson = Encoding.UTF8.GetString(catalogBytes);
using var catalogDoc = JsonDocument.Parse(catalogJson);
var cat = catalogDoc.RootElement;
var summary = cat.GetProperty("summary");
long S(string key) => summary.GetProperty(key).GetInt64();

// ---------------------------------------------------------------- 1. the tool is deterministic; outputs are LF, no BOM
{
    string mdPath = Path.Combine(repo, "docs", "design", "dungeon-run-catalog.md");
    var outs = new List<(byte[] json, byte[] md, int exit)>();
    for (int i = 0; i < 2; i++)
    {
        string j = Path.Combine(scratch, $"catalog-{i}.json"), m = Path.Combine(scratch, $"catalog-{i}.md");
        var psi = tool.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
            ? new ProcessStartInfo(Environment.ProcessPath ?? "dotnet", $"\"{tool}\" --out \"{j}\" --out-md \"{m}\"") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true }
            : new ProcessStartInfo(tool, $"--out \"{j}\" --out-md \"{m}\"") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        using var p = Process.Start(psi);
        p.StandardOutput.ReadToEnd(); p.StandardError.ReadToEnd();
        p.WaitForExit();
        outs.Add((File.ReadAllBytes(j), File.ReadAllBytes(m), p.ExitCode));
    }
    Check("tool exits 0 both runs (its self-check validates the catalog)", outs.All(o => o.exit == 0), string.Join(",", outs.Select(o => o.exit)));
    Check("tool: two runs give byte-identical catalog and report", outs[0].json.SequenceEqual(outs[1].json) && outs[0].md.SequenceEqual(outs[1].md));
    Check("tool: the repository catalog is the tool's current output (not stale)", outs[0].json.SequenceEqual(catalogBytes));
    Check("tool: the repository report is the tool's current output", outs[0].md.SequenceEqual(File.ReadAllBytes(mdPath)));
    foreach (var (name, bytes) in new[] { ("catalog-v1.json", catalogBytes), ("dungeon-run-catalog.md", File.ReadAllBytes(mdPath)) })
    {
        Check(name + ": no BOM", !(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF));
        Check(name + ": LF line endings only", !bytes.Contains((byte)'\r'));
        Check(name + ": ends with one LF", bytes.Length > 1 && bytes[^1] == '\n' && bytes[^2] != '\n');
    }
}

// ---------------------------------------------------------------- 2. loader and validator
var data = DungeonRunData.Parse(runJson, poolJson, npcJson, catalogJson);
var legacyData = DungeonRunData.Parse(runJson, poolJson, npcJson);
{
    var errors = DungeonRunValidator.Validate(data, null);
    Check("catalog data validates (client check not run headless)", errors.Count == 0, errors.Count + ": " + string.Join(" | ", errors.Take(8)));
    Check("catalogVersion 1, schemaVersion 1", data.CatalogVersion == 1 && cat.GetProperty("schemaVersion").GetInt32() == 1 && cat.GetProperty("catalogVersion").GetInt32() == 1);
    Check("without a catalog, CatalogVersion 0 and Bundles are the 52 pool bundles", legacyData.CatalogVersion == 0 && legacyData.Bundles.Count == 52 && legacyData.Bundles.All(b => b.Legacy && !b.IsPool));
    Check("entries = 52 fixed + pool entries", data.Bundles.Count == S("entries") && data.Bundles.Count(b => !b.IsPool) == 52 && data.Bundles.Count(b => b.IsPool) == S("poolEntries"));
    Check("fixed entries come first, then pool entries in ordinal id order", data.Bundles.Take(52).All(b => b.Legacy && !b.IsPool)
        && data.Bundles.Skip(52).Select(b => b.Id).SequenceEqual(data.Bundles.Skip(52).Select(b => b.Id).OrderBy(x => x, StringComparer.Ordinal)));
    // The 52 fixed entries equal pool-v1.json, in pool order.
    var legacy = data.LegacyBundles.ToList();
    bool same = legacy.Count == legacyData.Bundles.Count && legacy.Zip(legacyData.Bundles, (a, b) => a.Id == b.Id && a.Class == b.Class && a.Theme == b.Theme
        && a.Cards.SequenceEqual(b.Cards) && a.Classes.SequenceEqual(new[] { b.Class }) && a.Themes.SequenceEqual(new[] { b.Theme }) && a.Label == b.Label && a.LabelKey == b.LabelKey).All(x => x);
    Check("LegacyBundles equal pool-v1.json's 52 bundles (id, class, theme, cards, label) in pool order", same);
    Check("poolBundlesHash is the hash of pool-v1.json's bundles", cat.GetProperty("poolBundlesHash").GetString() == DungeonRunData.PoolBundlesHash(legacyData.Bundles));
    Check("no legacy pool entry; no non-legacy fixed entry", data.Bundles.All(b => b.Legacy == !b.IsPool));
    Check("legacy fixed entries keep the bundle_* ids", legacy.All(b => b.Id.StartsWith("bundle_", StringComparison.Ordinal)));
    Check("fixed entries: provenance SoloStone pool-v1.json", legacy.All(b => b.Provenance.Count == 1 && b.Provenance[0].Adventure == "SoloStone"));
}

// ---------------------------------------------------------------- 3. the summary is the predicate, recomputed here
using var bucketsDoc = JsonDocument.Parse(File.ReadAllText(Path.Combine(dataDir, "reference", "buckets.json")));
var engine = Cards.All.Where(c => c?.Id != null).GroupBy(c => c.Id).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
var policy = OfflinePoolPolicy.Load();
string Reason(string id, List<string> classes, bool granting, out bool grant)
{
    grant = false;
    if (!engine.TryGetValue(id, out var card)) return "engine:missing";
    var v = DungeonRunAdmission.Classify(card, id, AdmitScope.Player, null, catalogRun: true);
    if (!card.Implemented) return "engine:unscripted" + (v.Admitted ? "" : "; admission:" + v.Family);
    if (!v.Admitted) return "admission:" + v.Family;
    string r = DungeonRunValidator.PoolCardReason(id, classes, false, policy, catalogRun: true);
    if (r == "class" && granting && DungeonRunValidator.PoolCardReason(id, classes, true, policy, catalogRun: true) == null) { grant = true; r = null; }
    if (r != null) return "validator:" + r;
    if (DungeonRunValidator.HasRuneCost(id)) return "rune-cost";
    return null;
}
var pools = data.Bundles.Where(b => b.IsPool).ToList();
var byProvenance = new Dictionary<(string, string, string), DungeonRunBundle>();
foreach (var e in pools) foreach (var p in e.Provenance) byProvenance[(p.Adventure, p.Hero, p.Label)] = e;
var excludedByEntry = new Dictionary<string, List<(string id, string wiki, string reason)>>(StringComparer.Ordinal);
foreach (var e in cat.GetProperty("entries").EnumerateArray())
    if (e.TryGetProperty("excluded", out var ex))
        excludedByEntry[e.GetProperty("id").GetString()] = ex.EnumerateArray().Select(x => (x.GetProperty("id").GetString(), x.GetProperty("wikiId").GetString(), x.GetProperty("reason").GetString())).ToList();
string[] grantHeroes = { "Houndmaster Shaw", "Tess Greymane" };
{
    int buckets = 0, refs = 0, admitted = 0, excluded = 0, via = 0, unknownBuckets = 0, mismatched = 0, grants = 0;
    var excludedDistinct = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
    var detail = new List<string>();
    foreach (var ad in bucketsDoc.RootElement.GetProperty("adventures").EnumerateArray())
    {
        string adv = ad.GetProperty("adventure").GetString();
        foreach (var b in ad.GetProperty("buckets").EnumerateArray())
        {
            buckets++;
            string hero = b.GetProperty("hero").GetString(), label = b.GetProperty("label").GetString();
            if (b.GetProperty("membership").GetString() != "published") unknownBuckets++;
            var classes = b.GetProperty("class").GetString().Split('/').Select(s => s.Trim().ToUpperInvariant()).ToList();
            if (!byProvenance.TryGetValue((adv, hero, label), out var entry)) { mismatched++; detail.Add("no entry for " + adv + "/" + hero + "/" + label); continue; }
            bool primaryProvenance = entry.Provenance[0].Adventure == adv && entry.Provenance[0].Hero == hero && entry.Provenance[0].Label == label;
            var expAdmitted = new List<(string id, bool grant)>();
            var expExcluded = new List<(string id, string wiki, string reason)>();
            foreach (var m in b.GetProperty("members").EnumerateArray())
            {
                refs++;
                string wiki = m.GetProperty("cardId").GetString();
                var ids = new List<string> { wiki };
                foreach (var alt in m.GetProperty("alternates").EnumerateArray()) if (!ids.Contains(alt.GetString())) ids.Add(alt.GetString());
                (string id, string reason, bool grant)? first = null, pass = null;
                foreach (string id in ids)
                {
                    string r = Reason(id, classes, adv == "Monster Hunt" && grantHeroes.Contains(hero), out bool g);
                    if (r == null) { pass = (id, null, g); break; }
                    if (first == null || first.Value.reason == "engine:missing" && r != "engine:missing") first = (id, r, false);
                }
                if (pass != null) { admitted++; expAdmitted.Add((pass.Value.id, pass.Value.grant)); if (pass.Value.id != wiki) via++; if (pass.Value.grant) grants++; }
                else
                {
                    excluded++; expExcluded.Add((first.Value.id, wiki, first.Value.reason));
                    string fam = first.Value.reason.Split(';')[0];
                    if (!excludedDistinct.TryGetValue(fam, out var set)) excludedDistinct[fam] = set = new HashSet<string>();
                    set.Add(wiki);
                }
            }
            // A merged entry carries the first provenance's members; the merged-in bucket must have the same admitted set.
            bool membersOk = primaryProvenance
                ? entry.Candidates.Select(c => (c.Id, c.Grant)).SequenceEqual(expAdmitted)
                : new HashSet<string>(entry.Candidates.Select(c => c.Id)).SetEquals(expAdmitted.Select(x => x.id));
            bool excludedOk = !primaryProvenance || excludedByEntry[entry.Id].SequenceEqual(expExcluded);
            if (!membersOk || !excludedOk) { mismatched++; detail.Add($"{entry.Id}: members {membersOk} excluded {excludedOk}"); }
        }
    }
    Check("every bucket of buckets.json maps to a catalog entry whose members and exclusions the predicate recomputes", mismatched == 0, string.Join(" | ", detail.Take(6)));
    Check($"440 buckets, 2 with unknown membership", buckets == 440 && unknownBuckets == 2, $"{buckets}/{unknownBuckets}");
    // The summary counts every pool entry's own members (a merged-in bucket's members are not counted twice).
    var mergedIn = pools.SelectMany(e => e.Provenance.Skip(1)).ToList();
    long mergedRefs = 0;
    foreach (var ad in bucketsDoc.RootElement.GetProperty("adventures").EnumerateArray())
        foreach (var b in ad.GetProperty("buckets").EnumerateArray())
            if (mergedIn.Any(p => p.Adventure == ad.GetProperty("adventure").GetString() && p.Hero == b.GetProperty("hero").GetString() && p.Label == b.GetProperty("label").GetString()))
                mergedRefs += b.GetProperty("members").GetArrayLength();
    // Phase 6 F16: the SoloStone-curated buckets (curation.json customBuckets) are members of no buckets.json bucket.
    long customRefs = pools.Where(e => e.Provenance.Any(p => p.Adventure == "SoloStone-curated")).Sum(e => e.Candidates.Count);
    Check("summary memberReferences = recomputed refs minus merged-in buckets, plus the curated buckets' members", S("memberReferences") == refs - mergedRefs + customRefs
        && S("customBucketCards") == customRefs && S("customBuckets") == 14, $"{S("memberReferences")} vs {refs}-{mergedRefs}+{customRefs}");
    Check("summary admittedReferences + excludedReferences = memberReferences", S("admittedReferences") + S("excludedReferences") == S("memberReferences"));
    Check("summary admittedReferences = candidates over all pool entries", S("admittedReferences") == pools.Sum(e => e.Candidates.Count), $"{S("admittedReferences")} vs {pools.Sum(e => e.Candidates.Count)}");
    Check("summary excludedReferences = excluded lists over all pool entries", S("excludedReferences") == excludedByEntry.Values.Sum(l => l.Count));
    Check("summary viaAlternate = members drawn through a reprint id", S("viaAlternate") == pools.Sum(e => e.Candidates.Count(c => c.Id != c.WikiId)));
    Check("summary grantReferences = grant candidates", S("grantReferences") == pools.Sum(e => e.Candidates.Count(c => c.Grant)));
    var byReasonDistinct = summary.GetProperty("excludedDistinctIdsByReason").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetInt64());
    Check("summary excludedDistinctIdsByReason matches the recomputed predicate", byReasonDistinct.Count == excludedDistinct.Count
        && excludedDistinct.All(p => byReasonDistinct.TryGetValue(p.Key, out long n) && n == p.Value.Count),
        string.Join(",", excludedDistinct.Select(p => p.Key + "=" + p.Value.Count)) + " vs " + string.Join(",", byReasonDistinct.Select(p => p.Key + "=" + p.Value)));
    // Goldens of this import (engine-out of 2026-11-10; they move when the engine scripts more members or a lift lands).
    // Phase 3 review: the merge key now holds the adventure, label and flags, so Rumble Run Blessings stays its own entry.
    // Phase 6 F16: +8 SoloStone-curated pool entries (4 Death Knight, 4 Demon Hunter; 59 admitted members).
    // Phase 6 F18 (review): +6 more (3 per class, 7 each; 104 admitted members), so no pick order runs an act run short.
    Check("golden: 506 entries = 452 offerable pool + 2 withdrawn + 52 fixed",
        S("entries") == 506 && S("offerablePoolEntries") == 452 && S("withdrawnEntries") == 2 && S("fixedEntries") == 52,
        $"{S("entries")} {S("offerablePoolEntries")} {S("withdrawnEntries")} {S("fixedEntries")}");
    // Phase 6 F10 enables the three Tombs pairs (their 66 entries reachable) and scripts the 28 starter collectibles (+40
    // admitted references, 25 fewer engine:unscripted distinct ids).
    // Phase 6 F15 scripts and admits Zephrys the Great (ULD_003): its 3 Unique-entry references move from excluded to admitted.
    // Phase 8 wave 1 (families 02, 03, 04) scripts the 92 collectibles the buckets still listed as unscripted: their 190 member
    // references move from excluded to admitted (family 02: 90 references of 33 cards; 03: 56 of 29; 04: 44 of 29).
    Check("golden: 452 reachable and 0 unreachable offerable entries (phase 6 F10: every Tombs pair ships); 6508 reachable admitted references (F15: + Zephrys; F18: + 45 curated; phase 8 family 01: + Pressure Plate in two Brann entries; wave 1: + 90 (family 02) + 56 (03) + 44 (04))",
        S("reachableOfferablePoolEntries") == 452 && S("unreachableOfferablePoolEntries") == 0 && S("admittedReferencesReachable") == 6508,
        $"{S("reachableOfferablePoolEntries")} {S("unreachableOfferablePoolEntries")} {S("admittedReferencesReachable")}");
    // Phase 3 I2: the generated-Secret lift admits the 21 references of the 5 draft cards (I1: 6138 admitted, 257 excluded).
    Check("golden: 6508 admitted and 1 excluded reference (phase 8 wave 1: the 190 references of families 02, 03 and 04 admitted); 30 via a reprint id; 0 merges",
        S("admittedReferences") == 6508 && S("excludedReferences") == 1 && S("viaAlternate") == 30 && S("merges") == 0,
        $"{S("admittedReferences")} {S("excludedReferences")} {S("viaAlternate")} {S("merges")}");
    Check("golden: excluded distinct ids: no engine:unscripted id left (F15: Zephrys scripted; phase 8 family 01: Pressure Plate scripted; wave 1: 33 (family 02) + 29 (03) + 29 (04) bucket members scripted, 91 in all), no admission reason left, 1 validator:class",
        byReasonDistinct.GetValueOrDefault("engine:unscripted") == 0 && byReasonDistinct.GetValueOrDefault("admission:secret-generated") == 0
        && byReasonDistinct.GetValueOrDefault("validator:class") == 1 && byReasonDistinct.Count == 1);
}

// ---------------------------------------------------------------- 4. exclusions, withdrawals, candidates
{
    var allowed = new[] { "engine:missing", "engine:unscripted", "admission:", "validator:", "rune-cost", "curation:" };
    var excl = excludedByEntry.SelectMany(p => p.Value.Select(x => (entry: p.Key, x.id, x.wiki, x.reason))).ToList();
    Check("every excluded member has a reason of a known family", excl.All(x => !string.IsNullOrWhiteSpace(x.reason) && allowed.Any(a => x.reason.StartsWith(a, StringComparison.Ordinal))),
        string.Join(",", excl.Where(x => string.IsNullOrWhiteSpace(x.reason)).Select(x => x.entry + "/" + x.wiki).Take(5)));
    var withdrawn = pools.Where(e => !e.Offerable).ToList();
    Check("2 withdrawn entries: Rumble Run Shaman Jade Lotus and Mechanized, membership:unknown",
        withdrawn.Select(e => e.Id).OrderBy(x => x).SequenceEqual(new[] { "rr.shaman.jade-lotus", "rr.shaman.mechanized" }) && withdrawn.All(e => e.Withdrawn == "membership:unknown" && e.Candidates.Count == 0),
        string.Join(",", withdrawn.Select(e => e.Id + "=" + e.Withdrawn)));
    Check("every offerable pool entry has at least 3 candidates (distinct for a distinct entry)", pools.Where(e => e.Offerable)
        .All(e => (e.Distinct ? e.Candidates.Select(c => c.Id).Distinct().Count() : e.Candidates.Count) >= 3));
    Check("every candidate passes the predicate of its entry's class set (PoolCardReason, grant included)",
        pools.All(e => e.Candidates.All(c => DungeonRunValidator.PoolCardReason(c.Id, e.Classes, c.Grant, policy, catalogRun: true) == null && !DungeonRunValidator.HasRuneCost(c.Id))));
    Check("no candidate repeats within an entry", pools.All(e => e.Candidates.Select(c => c.Id).Distinct().Count() == e.Candidates.Count));
    Check("avgCost is the candidates' mean cost (to 0.01)", pools.All(e => Math.Abs(DungeonRunValidator.CandidateAverageCost(e) - e.AvgCost) <= 0.01));
    Check("ULD_429 Hunter's Pack is admitted (scripted since phase 6 F10; its secret-generated rule's catalog lift admits it)",
        !excl.Any(x => x.wiki == "ULD_429") && pools.Any(e => e.Candidates.Any(c => c.Id == "ULD_429")));
    Check("ULD_152 Pressure Plate is admitted (scripted in phase 8 family 01): no exclusion or note left, a candidate of its two Brann entries", !excl.Any(x => x.wiki == "ULD_152")
        && pools.Count(e => e.Candidates.Any(c => c.Id == "ULD_152")) == 2
        && !catalogJson.Contains("Pressure Plate as scripted text"));
    // Phase 3 I2: the generated-Secret lift is in for catalog runs; the 5 draft cards are admitted where their class allows.
    var liftedDraft = new[] { "BOT_402", "DAL_141", "GIL_116", "ICC_204", "UNG_011" };
    Check("the 5 generated-Secret draft cards are admitted after the lift; no member is excluded as admission:secret-generated",
        !excl.Any(x => x.reason.Contains("secret-generated")) && liftedDraft.All(id => pools.Any(e => e.Offerable && e.Candidates.Any(c => c.Id == id))),
        string.Join(",", liftedDraft.Where(id => !pools.Any(e => e.Offerable && e.Candidates.Any(c => c.Id == id)))));
    Check("HERO-type members get no blanket pass: each admitted hero passes the predicate", pools.SelectMany(e => e.Candidates.Select(c => (e, c)))
        .Where(x => engine[x.c.Id].Type == CardType.HERO).All(x => DungeonRunValidator.PoolCardReason(x.c.Id, x.e.Classes, x.c.Grant, policy, catalogRun: true) == null));
}

// ---------------------------------------------------------------- 5. ids, labels, dedup
{
    var prefixes = new Dictionary<string, string> { ["Dungeon Run"] = "dr.", ["Monster Hunt"] = "mh.", ["Rumble Run"] = "rr.", ["The Dalaran Heist"] = "dh.", ["Tombs of Terror"] = "tot.",
        ["SoloStone-curated"] = "ss." };
    Check("pool entry ids match ^[a-z0-9][a-z0-9._-]*$", pools.All(e => DungeonRunData.EntryIdPattern.IsMatch(e.Id)), string.Join(",", pools.Where(e => !DungeonRunData.EntryIdPattern.IsMatch(e.Id)).Select(e => e.Id).Take(5)));
    Check("no entry id holds '@', ':' or a space", data.Bundles.All(e => e.Id.IndexOfAny(new[] { '@', ':', ' ' }) < 0));
    Check("entry ids are unique", data.Bundles.Select(e => e.Id).Distinct().Count() == data.Bundles.Count);
    Check("pool entry ids carry their adventure's prefix", pools.All(e => e.Id.StartsWith(prefixes[e.Provenance[0].Adventure], StringComparison.Ordinal)));
    Check("labelKey is the lower-cased, punctuation-stripped label (curation fixes typos)", pools.All(e => e.LabelKey == DungeonRunData.LabelKeyOf(e.Label)
        || e.Label == "Minature Army" && e.LabelKey == "miniature army"), string.Join(",", pools.Where(e => e.LabelKey != DungeonRunData.LabelKeyOf(e.Label)).Select(e => e.Id + "=" + e.LabelKey)));
    Check("\"Minature Army\" is grouped as \"miniature army\"", pools.Any(e => e.Label == "Minature Army" && e.LabelKey == "miniature army"));
    Check("C'Thun spellings share one labelKey", pools.Where(e => e.Label.StartsWith("Cult of C'", StringComparison.Ordinal)).Select(e => e.LabelKey).Distinct().SequenceEqual(new[] { "cult of cthun" }));
    // The merge key: everything but the hero and provenance (adventure, label, classes, requirements, flags, quest, themes,
    // members with their grant flag). Entries of different adventures with the same members stay apart.
    string MergeKey(DungeonRunBundle e) => string.Join("|", e.Provenance[0].Adventure, e.LabelKey, string.Join("/", e.Classes), string.Join("/", e.ClassesAll), e.RequiresMechanic ?? "",
        e.Distinct, e.Quest?.CardId ?? "", e.Quest?.OnFirstTake ?? false, string.Join(",", e.Themes), string.Join(",", e.Candidates.Select(c => c.Id + (c.Grant ? "+grant" : "")).OrderBy(x => x, StringComparer.Ordinal)));
    var keys = pools.Where(e => e.Offerable).GroupBy(MergeKey).Where(g => g.Count() > 1).ToList();
    Check("dedup: no two offerable entries share the merge key (all but hero and provenance)", keys.Count == 0, string.Join(",", keys.Select(g => string.Join("+", g.Select(e => e.Id)))));
    var sameDraw = pools.Where(e => e.Offerable).GroupBy(e => string.Join("/", e.Classes) + "|" + string.Join(",", e.Candidates.Select(c => c.Id).OrderBy(x => x, StringComparer.Ordinal)))
        .Where(g => g.Count() > 1).Select(g => g.Select(e => e.Id).OrderBy(x => x, StringComparer.Ordinal).ToList()).ToList();
    Check("the Rumble Run 'Blessings' bucket keeps its identity: its own label, weight and takes, beside Dungeon Run 'Restoration' (same draw)",
        sameDraw.Count == 1 && sameDraw[0].SequenceEqual(new[] { "dr.shaman.restoration", "rr.shaman.blessings" })
        && pools.Single(e => e.Id == "rr.shaman.blessings").Label == "Blessings" && pools.Single(e => e.Id == "dr.shaman.restoration").Label == "Restoration"
        && DungeonRun.SameDraw(pools.Single(e => e.Id == "rr.shaman.blessings"), pools.Single(e => e.Id == "dr.shaman.restoration")),
        string.Join(" ; ", sameDraw.Select(g => string.Join("+", g))));
    var merged = pools.Where(e => e.Provenance.Count > 1).ToList();
    Check("merged entries carry every provenance (summary merges)", merged.Count == S("merges") && merged.All(e => e.Provenance.Count == 2), string.Join(",", merged.Select(e => e.Id)));
    Check("every entry has a primary theme; secret-themed entries require the secret mechanic", data.Bundles.All(e => !string.IsNullOrEmpty(e.PrimaryTheme))
        && pools.All(e => (e.Themes.Contains("secret")) == (e.RequiresMechanic == "secret")));
    Check("no theme falls back to label:<key> (every label tagged)", pools.All(e => !e.Themes.Any(t => t.StartsWith("label:", StringComparison.Ordinal))) && S("themeFallbackLabels") == 0);
}

// ---------------------------------------------------------------- 6. Unique, grants, Tombs pairs, quests
{
    var unique = pools.Where(e => e.Distinct).ToList();
    Check("3 Unique entries (Mage, Priest, Warlock), distinct, offerable", unique.Select(e => e.Id).OrderBy(x => x).SequenceEqual(new[] { "dr.mage.unique", "dr.priest.unique", "dr.warlock.unique" })
        && unique.All(e => e.Offerable), string.Join(",", unique.Select(e => e.Id)));
    // Phase 6 F15 scripts the wish: Zephrys the Great (ULD_003) is now an admitted candidate of every Unique entry.
    Check("Unique entries hold ULD_003 and 4 distinct candidates (phase 6 F15)", unique.All(e => e.Candidates.Any(c => c.Id == "ULD_003") && e.Candidates.Select(c => c.Id).Distinct().Count() == 4),
        string.Join(",", unique.Select(e => e.Candidates.Count)));
    Check("ULD_003 Zephrys is excluded from no Unique entry", unique.All(e => !excludedByEntry[e.Id].Any(x => x.wiki == "ULD_003")));
    var grantEntries = pools.Where(e => e.Candidates.Any(c => c.Grant)).ToList();
    Check("grants occur only in Monster Hunt Shaw and Tess entries", grantEntries.Count > 0 && grantEntries.All(e => e.Provenance.All(p => p.Adventure == "Monster Hunt" && grantHeroes.Contains(p.Hero))),
        string.Join(",", grantEntries.Where(e => !e.Id.StartsWith("mh.houndmaster-shaw.") && !e.Id.StartsWith("mh.tess-greymane.")).Select(e => e.Id)));
    Check("every grant is off-class for its entry (fails the class check without the grant)", pools.SelectMany(e => e.Candidates.Where(c => c.Grant).Select(c => (e, c)))
        .All(x => DungeonRunValidator.PoolCardReason(x.c.Id, x.e.Classes, false, policy, catalogRun: true) == "class"));
    Check("Shaw grants Druid cards; Tess grants Hunter cards", pools.Where(e => e.Id.StartsWith("mh.houndmaster-shaw.")).SelectMany(e => e.Candidates.Where(c => c.Grant)).Any(c => engine[c.Id].Class == CardClass.DRUID)
        && pools.Where(e => e.Id.StartsWith("mh.tess-greymane.")).SelectMany(e => e.Candidates.Where(c => c.Grant)).Any(c => engine[c.Id].Class == CardClass.HUNTER));
    var tombs = pools.Where(e => e.Id.StartsWith("tot.", StringComparison.Ordinal)).ToList();
    var pairs = tombs.Select(e => string.Join("+", e.ClassesAll)).Distinct().OrderBy(x => x).ToList();
    Check("88 Tombs entries with classesAll = classes, the four guest-hero pairs", tombs.Count == 88 && tombs.All(e => e.ClassesAll.SequenceEqual(e.Classes) && e.ClassesAll.Count == 2)
        && pairs.SequenceEqual(new[] { "DRUID+PRIEST", "HUNTER+WARRIOR", "MAGE+ROGUE", "PALADIN+SHAMAN" }), string.Join(",", pairs));
    Check("only Tombs entries have classesAll", pools.Where(e => e.ClassesAll.Count > 0).All(e => e.Id.StartsWith("tot.")) && S("dualClassEntries") == 88);
    var quests = pools.Where(e => e.Quest != null).ToList();
    Check("quest entries: the quest is a candidate; onFirstTake false exactly for Tombs", quests.Count == S("questEntries") && quests.Count > 0
        && quests.All(e => e.Candidates.Any(c => c.Id == e.Quest.CardId) && engine[e.Quest.CardId][GameTag.QUEST] != 0 && e.Quest.OnFirstTake == !e.Id.StartsWith("tot.")));
    Check("quest entries carry the quest theme", quests.All(e => e.Themes.Contains("quest")));
}

// ---------------------------------------------------------------- 7. eligibility (EligibleFor)
{
    bool None(string m) => false;
    var druid = pools.Where(e => e.EligibleFor(new[] { "DRUID" }, None)).ToList();
    Check("a Druid run is eligible for Druid pool entries of every adventure but Tombs", druid.Count > 0 && druid.All(e => e.Classes.Contains("DRUID") && e.ClassesAll.Count == 0)
        && new[] { "dr.", "rr.", "dh." }.All(p => druid.Any(e => e.Id.StartsWith(p))));
    Check("a single-class run is never eligible for a Tombs entry", new[] { "MAGE", "ROGUE", "HUNTER", "PRIEST", "SHAMAN" }.All(c => !pools.Any(e => e.Id.StartsWith("tot.") && e.EligibleFor(new[] { c }, None))));
    var reno = pools.Where(e => e.EligibleFor(new[] { "MAGE", "ROGUE" }, None)).ToList();
    Check("a Mage/Rogue run is eligible for Reno's Tombs entries plus Mage and Rogue entries", reno.Any(e => e.Id.StartsWith("tot.reno-jackson."))
        && !reno.Any(e => e.Id.StartsWith("tot.") && !e.Id.StartsWith("tot.reno-jackson.")) && reno.Any(e => e.Id.StartsWith("dr.mage.")) && reno.Any(e => e.Id.StartsWith("dr.rogue.")));
    Check("a Hunter run sees Shaw's buckets (no hero requirement)", pools.Where(e => e.Id.StartsWith("mh.houndmaster-shaw.") && e.RequiresMechanic == null).All(e => e.EligibleFor(new[] { "HUNTER" }, None)));
    Check("withdrawn entries are never eligible", pools.Where(e => !e.Offerable).All(e => !e.EligibleFor(new[] { "SHAMAN" }, m => true)));
    var secret = pools.Where(e => e.RequiresMechanic == "secret").ToList();
    Check("secret entries need the mechanic: ineligible when it does not hold, eligible when it does", secret.Count == S("secretEntries") && secret.Count > 0
        && secret.All(e => !e.EligibleFor(e.Classes, None) && e.EligibleFor(e.Classes, m => m == "secret")));
    // Phase 3 I2: MechanicHolds reads the catalog run's widened prior (its base: no Secret-requiring entry), never the closed one.
    Check("MechanicHolds(secret) is the widened prior: true for a class exactly when the catalog's base Secret pool is non-empty",
        new[] { "MAGE", "HUNTER", "PALADIN", "ROGUE", "DRUID" }.All(c => DungeonRunAdmission.MechanicHolds(data, new[] { c }, "secret")
            == (DungeonRunAdmission.CatalogSecretPrior(data, new[] { c }) != null))
        && new[] { "MAGE", "HUNTER", "PALADIN", "ROGUE" }.All(c => DungeonRunAdmission.MechanicHolds(data, new[] { c }, "secret"))
        && !DungeonRunAdmission.MechanicHolds(data, new[] { "MAGE" }, "unknown"));
    Check("fixed entries keep OfferedTo: neutral or own class", data.LegacyBundles.All(b => b.OfferedTo(b.Class) && (b.Class == "NEUTRAL" ? b.OfferedTo("MAGE") : !b.OfferedTo("NEUTRAL_X"))));
}

{
    // Reachability: an offerable entry no playable class row can be offered carries its reason, and the counts exclude it.
    var reachable = new HashSet<string>(StringComparer.Ordinal);
    foreach (var cls in data.Classes)
    {
        bool secret = DungeonRunAdmission.MechanicHolds(data, cls.ClassSet, "secret");
        foreach (var e in pools.Where(e => e.EligibleFor(cls.ClassSet, m => m == "secret" && secret))) reachable.Add(e.Id);
    }
    string md = File.ReadAllText(Path.Combine(repo, "docs", "design", "dungeon-run-catalog.md"));
    var raw = cat.GetProperty("entries").EnumerateArray().ToDictionary(e => e.GetProperty("id").GetString(), e => e);
    var marked = raw.Where(p => p.Value.TryGetProperty("unreachable", out _)).Select(p => p.Key).ToHashSet(StringComparer.Ordinal);
    var expectUnreachable = pools.Where(e => e.Offerable && !reachable.Contains(e.Id)).Select(e => e.Id).ToHashSet(StringComparer.Ordinal);
    Check("`unreachable` is written on exactly the offerable entries no playable class row reaches (none since phase 6 F10)", marked.SetEquals(expectUnreachable) && marked.Count == 0 && data.DisabledClasses.Count == 0,
        $"{marked.Count} marked, {expectUnreachable.Count} expected");
    Check("every unreachable entry is a Tombs entry of a pair whose catalogClasses row ships disabled, and says so",
        marked.All(id => pools.Single(e => e.Id == id) is var e && e.ClassesAll.Count == 2 && data.DisabledClasses.Any(c => c.ClassSet.SequenceEqual(e.ClassesAll))
            && raw[id].GetProperty("unreachable").GetString() == $"run:dual-class ({string.Join("_", e.ClassesAll)} ships disabled)"));
    Check("summary reachable counts exclude unreachable entries", S("reachableOfferablePoolEntries") == S("offerablePoolEntries") - marked.Count
        && S("unreachableOfferablePoolEntries") == marked.Count && S("admittedReferencesReachable") == pools.Where(e => e.Offerable && !marked.Contains(e.Id)).Sum(e => e.Candidates.Count));
    Check("the report lists every unreachable entry with its reason", marked.All(id => md.Contains($"| `{id}` | run:dual-class (")) && md.Contains("## Unreachable entries"));
}

// ---------------------------------------------------------------- 8. cards index and custom cards
{
    var cards = cat.GetProperty("cards");
    var index = cards.EnumerateObject().ToDictionary(p => p.Name, p => p.Value);
    bool indexOk = pools.Where(e => e.Offerable).All(e => e.Candidates.All(c => index.TryGetValue(c.Id, out var row) && row.GetProperty("entries").EnumerateArray().Any(x => x.GetString() == e.Id)))
        && data.LegacyBundles.All(b => b.Cards.All(c => index.TryGetValue(c, out var row) && row.GetProperty("entries").EnumerateArray().Any(x => x.GetString() == b.Id)));
    Check("cards index lists every candidate and fixed card with its entries", indexOk);
    Check("cards index lists every starter card with its starter decks", data.Decks.All(d => d.Cards.All(c => index.TryGetValue(c, out var row) && row.TryGetProperty("starters", out var st) && st.EnumerateArray().Any(x => x.GetString() == d.Id))));
    Check("every card is marked clientChecked: false (the game checks the client)", index.Values.All(r => r.GetProperty("clientChecked").GetBoolean() == false));
    var shells = new HashSet<string>(DungeonRunContent.All.Where(c => c.ShellId != null).Select(c => c.ShellId));
    Check("custom flag = a SoloStone content shell, with SoloStone custom-card provenance", index.All(p => p.Value.GetProperty("custom").GetBoolean() == shells.Contains(p.Key)
        && (!shells.Contains(p.Key) || p.Value.GetProperty("provenance").GetProperty("source").GetString() == "custom-card")) && S("customCards") == index.Keys.Count(shells.Contains));
}

// ---------------------------------------------------------------- 9. client-unknown drop and stale catalogs
{
    // A card only pool entries reach (no fixed bundle, no starter): the client is stubbed not to know it.
    var poolOnly = pools.Where(e => e.Offerable).SelectMany(e => e.Candidates.Select(c => c.Id)).Distinct(StringComparer.Ordinal)
        .Where(id => !data.LegacyBundles.Any(b => b.Cards.Contains(id)) && !data.Decks.Any(d => d.Cards.Contains(id)))
        .OrderBy(id => id, StringComparer.Ordinal).First();
    var stub = DungeonRunData.Parse(runJson, poolJson, npcJson, catalogJson);
    var holders = stub.Bundles.Where(b => b.IsPool && b.Candidates.Any(c => c.Id == poolOnly)).Select(b => b.Id).ToList();
    Func<string, bool> knows = id => id != poolOnly;
    var lines = DungeonRunValidator.DropClientUnknown(stub, knows);
    Check($"client-unknown {poolOnly}: dropped with one log line per entry", holders.Count > 0 && holders.All(h => lines.Contains($"catalog: {h} dropped {poolOnly} client-unknown"))
        && lines.Count(l => l.Contains(" dropped ")) == holders.Count, string.Join(" | ", lines.Take(4)));
    Check("client-unknown: the card left every entry; avgCost recomputed", stub.Bundles.All(b => !b.Candidates.Any(c => c.Id == poolOnly))
        && stub.Bundles.Where(b => holders.Contains(b.Id)).All(b => Math.Abs(b.AvgCost - DungeonRunValidator.CandidateAverageCost(b)) < 1e-9));
    var errs = DungeonRunValidator.Validate(stub, knows);
    Check("client-unknown: the mode stays up (no validation error with the stubbed client)", errs.Count == 0, string.Join(" | ", errs.Take(5)));
    // Without the drop the same client check is a hard error: the drop is what keeps the mode up.
    var undropped = DungeonRunData.Parse(runJson, poolJson, npcJson, catalogJson);
    Check("client-unknown: without the drop the validator reports it", DungeonRunValidator.Validate(undropped, knows).Any(e => e.StartsWith(poolOnly + ": no client entity definition")));
    // Drop enough of one entry to withdraw it.
    var victim = DungeonRunData.Parse(runJson, poolJson, npcJson, catalogJson);
    var target = victim.Bundles.First(b => b.IsPool && b.Offerable && b.Candidates.Count >= 3);
    var gone = new HashSet<string>(target.Candidates.Skip(2).Select(c => c.Id));
    var wlines = DungeonRunValidator.DropClientUnknown(victim, id => !gone.Contains(id));
    Check($"client-unknown: {target.Id} below 3 candidates is withdrawn and logged", !target.Offerable && target.Withdrawn == "client-unknown"
        && wlines.Contains($"catalog: {target.Id} withdrawn (2 candidates left)"), string.Join(" | ", wlines.Where(l => l.Contains(target.Id)).Take(3)));
    var werrs = DungeonRunValidator.Validate(victim, id => !gone.Contains(id) || victim.LegacyBundles.Any(b => b.Cards.Contains(id)) || victim.Decks.Any(d => d.Cards.Contains(id)));
    Check("client-unknown: a withdrawn entry keeps the mode up", werrs.Count == 0, string.Join(" | ", werrs.Take(4)));
    Check("client-unknown: the withdrawn entry is not eligible", !target.EligibleFor(target.Classes, m => true));
    Check("DropClientUnknown with no client check changes nothing", DungeonRunValidator.DropClientUnknown(DungeonRunData.Parse(runJson, poolJson, npcJson, catalogJson), null).Count == 0);
    // A client missing one quest card: the quest entries keep validating (the dropped quest is no longer their quest), the
    // drop is logged, and the mode stays up.
    {
        var qdata = DungeonRunData.Parse(runJson, poolJson, npcJson, catalogJson);
        string questId = "UNG_116";
        var questEntries = qdata.Bundles.Where(b => b.Quest?.CardId == questId).Select(b => b.Id).ToList();
        Func<string, bool> noQuest = id => id != questId;
        var qlines = DungeonRunValidator.DropClientUnknown(qdata, noQuest);
        var qerrs = DungeonRunValidator.Validate(qdata, id => noQuest(id) || qdata.LegacyBundles.Any(b => b.Cards.Contains(id)) || qdata.Decks.Any(d => d.Cards.Contains(id)));
        Check($"client missing quest {questId}: {questEntries.Count} entries validate (offerable or withdrawn), quest cleared and logged",
            questEntries.Count > 0 && qerrs.Count == 0 && questEntries.All(id => qdata.Bundles.Single(b => b.Id == id) is var e && e.Quest == null && (e.Offerable || e.Withdrawn == "client-unknown"))
            && questEntries.All(id => qlines.Contains($"catalog: {id} quest {questId} dropped client-unknown")),
            string.Join(" | ", qerrs.Take(4)) + " / " + string.Join(" | ", qlines.Where(l => l.Contains("quest")).Take(4)));
        // Every quest card of the catalog, one at a time.
        int questsChecked = 0, questFailures = 0;
        foreach (string q in pools.Where(e => e.Quest != null).Select(e => e.Quest.CardId).Distinct(StringComparer.Ordinal))
        {
            var d2 = DungeonRunData.Parse(runJson, poolJson, npcJson, catalogJson);
            DungeonRunValidator.DropClientUnknown(d2, id => id != q);
            var qe = DungeonRunValidator.Validate(d2, id => id != q || d2.LegacyBundles.Any(b => b.Cards.Contains(id)) || d2.Decks.Any(d => d.Cards.Contains(id)));
            questsChecked++;
            if (qe.Count > 0) questFailures++;
        }
        Check($"a client missing any one quest card keeps the mode up ({questsChecked} quest cards)", questsChecked > 0 && questFailures == 0, questFailures.ToString());
    }

    // A stale poolBundlesHash disables the mode with the stale message (the runtime reports Parse's exception).
    string staleHash = catalogJson.Replace(cat.GetProperty("poolBundlesHash").GetString(), new string('0', 64));
    string Thrown(Func<object> f) { try { f(); return null; } catch (Exception e) { return e.GetType().Name + ": " + e.Message; } }
    string m1 = Thrown(() => DungeonRunData.Parse(runJson, poolJson, npcJson, staleHash));
    Check("stale poolBundlesHash: Parse throws the stale-catalog error", m1 != null && m1.StartsWith("InvalidDataException: " + DungeonRunData.StaleCatalog), m1 ?? "no throw");
    // pool-v1.json changed after the catalog was generated.
    string changedPool = poolJson.Replace("\"AT_037\", \"FIR_906\", \"ULD_273\"", "\"AT_037\", \"FIR_906\", \"AT_037\"");
    string m2 = changedPool == poolJson ? "pool edit did not apply" : Thrown(() => DungeonRunData.Parse(runJson, changedPool, npcJson, catalogJson));
    Check("pool-v1.json edited after generation: Parse throws the stale-catalog error", m2 != null && m2.StartsWith("InvalidDataException: " + DungeonRunData.StaleCatalog), m2 ?? "no throw");
    // The catalog's own fixed entries edited (the hash field kept): also stale.
    var firstFixed = data.LegacyBundles.First();
    string editedFixed = catalogJson.Replace("\"cards\": [\"" + string.Join("\", \"", firstFixed.Cards) + "\"]", "\"cards\": [\"" + firstFixed.Cards[0] + "\", \"" + firstFixed.Cards[0] + "\", \"" + firstFixed.Cards[0] + "\"]");
    string m3 = editedFixed == catalogJson ? "catalog edit did not apply" : Thrown(() => DungeonRunData.Parse(runJson, poolJson, npcJson, editedFixed));
    Check("catalog fixed entries edited: Parse throws the stale-catalog error", m3 != null && m3.StartsWith("InvalidDataException: " + DungeonRunData.StaleCatalog), m3 ?? "no throw");
    string m4 = Thrown(() => DungeonRunData.Parse(runJson, poolJson, npcJson, catalogJson.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 9")));
    Check("unknown catalog schemaVersion is refused", m4 != null && m4.Contains("unsupported schemaVersion"), m4 ?? "no throw");

    // A catalog stale against the engine or admission rules fails validation (a hard error, not a silent drop).
    var bad = DungeonRunData.Parse(runJson, poolJson, npcJson, catalogJson);
    var e1 = bad.Bundles.First(b => b.Id == "dr.mage.unique");
    e1.Candidates.Add(new DungeonRunCandidate { Id = "BOT_914", WikiId = "BOT_914" });   // unscripted (Whizbang; Zephrys is scripted since phase 6 F15, Pressure Plate since phase 8 family 01)
    e1.Candidates.Add(new DungeonRunCandidate { Id = "ULDA_111", WikiId = "ULDA_111" }); // admission start-of-game-draw (phase 9)
    var e2 = bad.Bundles.First(b => b.IsPool && b.Offerable && b.Id != e1.Id && b.Classes.SequenceEqual(new[] { "DRUID" }));
    e2.Candidates.Add(new DungeonRunCandidate { Id = "CS2_024", WikiId = "CS2_024" });   // Frostbolt in a Druid entry
    var e3 = bad.Bundles.First(b => b.IsPool && b.Offerable && b.Id != e1.Id && b.Id != e2.Id);
    e3.AvgCost += 1;
    var e4 = bad.Bundles.First(b => b.IsPool && b.Offerable && b.Id != e1.Id && b.Id != e2.Id && b.Id != e3.Id);
    e4.Candidates.RemoveRange(2, e4.Candidates.Count - 2); e4.AvgCost = DungeonRunValidator.CandidateAverageCost(e4);
    var e5 = bad.Bundles.First(b => b.IsPool && b.Offerable && !new[] { e1, e2, e3, e4 }.Contains(b));
    string e5Old = e5.Id; e5.Id = "DR:Bad Id@2";
    var e6 = bad.Bundles.First(b => b.IsPool && b.Offerable && !new[] { e1, e2, e3, e4, e5 }.Contains(b));
    e6.Candidates.Add(new DungeonRunCandidate { Id = "RLK_024", WikiId = "RLK_024" });  // Death Strike: a rune cost
    var berrs = DungeonRunValidator.Validate(bad, null);
    Check("stale catalog: an unscripted candidate and a candidate the table rejects are validation errors", berrs.Any(e => e.StartsWith("BOT_914: not scripted (dr.mage.unique)"))
        && berrs.Any(e => e.StartsWith("ULDA_111: admission start-of-game-draw")), string.Join(" | ", berrs.Where(e => e.Contains("BOT_914") || e.Contains("ULDA_111")).Take(4)));
    Check("stale catalog: an off-class candidate is a validation error", berrs.Any(e => e.StartsWith("CS2_024: class MAGE, not allowed in DRUID (" + e2.Id + ")")), string.Join(" | ", berrs.Where(e => e.Contains("CS2_024")).Take(3)));
    Check("stale catalog: a wrong avgCost is a validation error", berrs.Any(e => e.StartsWith(e3.Id + ": avgCost")));
    Check("stale catalog: an offerable entry with 2 candidates is a validation error", berrs.Any(e => e == e4.Id + ": has 2 candidates, needs at least 3"), string.Join(" | ", berrs.Where(e => e.StartsWith(e4.Id)).Take(2)));
    Check("an entry id breaking the pattern is a validation error", berrs.Any(e => e.StartsWith("DR:Bad Id@2: catalog entry id must match")));
    Check("a candidate with a rune cost is a validation error", berrs.Any(e => e.StartsWith(e6.Id + ": candidate RLK_024 has a rune cost")) || berrs.Any(e => e.StartsWith("RLK_024")), string.Join(" | ", berrs.Where(e => e.Contains("RLK_024")).Take(3)));
    var dup = DungeonRunData.Parse(runJson, poolJson, npcJson, catalogJson);
    dup.Bundles.Add(dup.Bundles.First(b => b.IsPool));
    Check("an entry listed twice is a validation error", DungeonRunValidator.Validate(dup, null).Any(e => e.EndsWith(": listed twice")));
}

// ---------------------------------------------------------------- 10. legacy runs are unchanged by the catalog
{
    // Same runs on the catalog-loaded data and on the pool-only data: identical drafts, picks and decks (CatalogVersion 0
    // behaviour: OpenBundleDraft and PickBundle read LegacyBundles only).
    var runA = new DungeonRun(data);
    var runB = new DungeonRun(legacyData);
    int compared = 0, differ = 0, nonLegacy = 0;
    // Phase 3 I2: a new run on catalog data is a catalog run; a legacy run (CatalogVersion 0, every run of an older save) is
    // pinned to the pre-catalog draft, so the catalog-data run is set to 0 here. Legacy runs are single-class (legacyData).
    foreach (var cls in legacyData.Classes)
        foreach (long seed in new long[] { 11, 2024, 987654321 })
        {
            string Play(DungeonRun run, DungeonRunData d)
            {
                var profile = new DungeonRunProfile();
                var s = run.NewRun(profile, cls.Id, cls.DefaultDeck, cls.DefaultPower, seed, signatureId: run.SignatureOffer(profile, run.Data.FindClass(cls.Id), seed).FirstOrDefault());
                s.CatalogVersion = 0;
                int guard = 0;
                while (!s.Finished && guard++ < 200)
                {
                    if (s.Phase == DungeonRunPhase.PreMatch) { run.BeginMatch(s); run.RecordOutcome(s, profile, DungeonRunOutcome.Win); }
                    else if (s.Phase == DungeonRunPhase.TreasureDraft) run.PickTreasure(s, s.OpenDraft.Offered[0]);
                    else if (s.Phase == DungeonRunPhase.BundleDraft)
                    {
                        if (s.OpenDraft.Offered.Any(id => !d.LegacyBundles.Any(b => b.Id == id))) nonLegacy++;
                        run.PickBundle(s, s.OpenDraft.Offered[s.Floor % s.OpenDraft.Offered.Count]);
                    }
                }
                return string.Join(";", s.Drafts.Select(x => x.Floor + x.Kind + ":" + string.Join(",", x.Offered) + ">" + x.Picked)) + "|" + string.Join(",", s.Deck);
            }
            compared++;
            if (Play(runA, data) != Play(runB, legacyData)) differ++;
        }
    Check($"legacy runs: identical drafts and decks with and without the catalog ({compared} runs)", differ == 0 && compared == 33, $"{differ} differ");
    Check("legacy runs: only legacy bundles are offered", nonLegacy == 0, nonLegacy + " drafts offered a pool entry");
    Check("DeckSourceCards reads only legacy bundles (closed prior unchanged by the catalog)", legacyData.Classes.All(c =>
        DungeonRunAdmission.DeckSourceCards(data, c.Id).SequenceEqual(DungeonRunAdmission.DeckSourceCards(legacyData, c.Id))));
    Check("SecretPrior unchanged by the catalog", legacyData.Classes.All(c =>
    {
        var a = DungeonRunAdmission.SecretPrior(data, Enum.Parse<CardClass>(c.Id));
        var b = DungeonRunAdmission.SecretPrior(legacyData, Enum.Parse<CardClass>(c.Id));
        return a == null ? b == null : b != null && a.OrderBy(p => p.Key).SequenceEqual(b.OrderBy(p => p.Key));
    }));
}

// ---------------------------------------------------------------- 11. dual-class rows (validator)
{
    // Phase 3 I2 ships one enabled dual row (MAGE_ROGUE, loaded with the catalog); the 11 class rows list no classes.
    // Phase 6 F3 on: a single-class named hero row (heroRows: GILA_500h3 ROGUE, GILA_600h WARRIOR) names its one class.
    Check("existing rows: ClassSet = [Id] (no classes field); the shipped dual row MAGE_ROGUE lists [MAGE, ROGUE]; a single-class hero row its class",
        data.Classes.Where(c => c.Id != "MAGE_ROGUE" && !c.IsHero).All(c => c.Classes.Count == 0 && c.ClassSet.SequenceEqual(new[] { c.Id }))
        && data.Classes.Where(c => c.IsHero && !c.IsDual).All(c => c.ClassSet.Count == 1 && c.ClassSet[0] == Cards.FromId(c.HeroCard).Class.ToString())
        // Phase 6 F10: the other three Tombs dual rows ship too, each listing its pair, the portrait's class first.
        && data.Classes.Where(c => c.IsDual).Select(c => c.Id).OrderBy(x => x).SequenceEqual(new[] { "DRUID_PRIEST", "HUNTER_WARRIOR", "MAGE_ROGUE", "PALADIN_SHAMAN" })
        && data.Classes.Where(c => c.IsDual).All(c => c.ClassSet.SequenceEqual(c.Id.Split('_')) && c.ClassSet[0] == Cards.FromId(c.HeroCard).Class.ToString())
        && data.Class("MAGE_ROGUE").ClassSet.SequenceEqual(new[] { "MAGE", "ROGUE" }) && legacyData.FindClass("MAGE_ROGUE") == null);
    List<string> Validate(Action<DungeonRunData, DungeonRunClass> edit)
    {
        var d = DungeonRunData.Parse(runJson, poolJson, npcJson, catalogJson);
        // The probe replaces the shipped MAGE_ROGUE row (phase 3 I2) with three synthetic starters.
        d.Classes.RemoveAll(c => c.Id == "MAGE_ROGUE"); d.Decks.RemoveAll(x => x.Class == "MAGE_ROGUE"); d.ClassTags.Remove("MAGE_ROGUE");
        var mage = d.Class("MAGE");
        var mageCards = d.Deck(mage.DefaultDeck).Cards.Take(5).ToList();
        var rogueCards = d.Deck(d.Class("ROGUE").DefaultDeck).Cards.Take(5).ToList();
        var row = new DungeonRunClass
        {
            Id = "MAGE_ROGUE", Name = "Reno probe", HeroCard = mage.HeroCard, Classes = new List<string> { "MAGE", "ROGUE" },
            Powers = mage.Powers.ToList(), PowerUnlocks = new Dictionary<string, string>(mage.PowerUnlocks),
        };
        for (int i = 0; i < 3; i++)
        {
            string id = "probe_dual_" + i;
            d.Decks.Add(new DungeonRunStarterDeck { Id = id, Class = "MAGE_ROGUE", Name = "Dual probe " + i, Description = "probe", Unlock = i == 0 ? null : d.Deck(mage.Decks[i]).Unlock,
                Cards = mageCards.Concat(rogueCards).ToList() });
            row.Decks.Add(id);
        }
        d.Classes.Add(row);
        d.ClassTags["MAGE_ROGUE"] = d.ClassTags["MAGE"].ToList();
        edit?.Invoke(d, row);
        return DungeonRunValidator.Validate(d, null).Where(e => e.Contains("MAGE_ROGUE") || e.Contains("ROGUE_MAGE") || e.Contains("probe_dual")).ToList();
    }
    var ok = Validate(null);
    Check("a dual-class row MAGE_ROGUE with Mage and Rogue starter cards validates", ok.Count == 0, string.Join(" | ", ok.Take(4)));
    var badId = Validate((d, r) => { r.Id = "ROGUE_MAGE"; foreach (var deck in d.Decks.Where(x => x.Class == "MAGE_ROGUE")) deck.Class = "ROGUE_MAGE"; d.ClassTags["ROGUE_MAGE"] = d.ClassTags["MAGE"]; d.ClassTags.Remove("MAGE_ROGUE"); });
    Check("a dual-class id must be A_B in class order", badId.Any(e => e.Contains("a dual-class id must be MAGE_ROGUE")), string.Join(" | ", badId.Take(3)));
    var same = Validate((d, r) => r.Classes = new List<string> { "MAGE", "MAGE" });
    Check("a dual-class row needs two distinct classes", same.Any(e => e.Contains("must be two distinct class names")), string.Join(" | ", same.Take(3)));
    var portrait = Validate((d, r) => r.Classes = new List<string> { "ROGUE", "MAGE" });
    Check("the portrait class must be Classes[0]", portrait.Any(e => e.Contains("portrait") && e.Contains("is MAGE")), string.Join(" | ", portrait.Take(3)));
    var offClass = Validate((d, r) => d.Deck("probe_dual_0").Cards[0] = "CS2_062");   // Hellfire, Warlock
    Check("a dual-class starter card outside both classes fails", offClass.Any(e => e.StartsWith("CS2_062: class WARLOCK, not allowed in MAGE/ROGUE")), string.Join(" | ", offClass.Take(3)));
    var single = DungeonRunData.Parse(runJson, poolJson, npcJson, catalogJson);
    single.Class("MAGE").Classes = new List<string> { "ROGUE" };
    Check("a single-class row's classes must be [Id]", DungeonRunValidator.Validate(single, null).Any(e => e == "class MAGE: classes [ROGUE] must be [MAGE]"));
}

// ---------------------------------------------------------------- 12. phase 3 I2: the restoration ledger reads the catalog
{
    string ledgerPath = Path.Combine(repo, "docs", "design", "restoration-ledger.csv");
    var rows = ReadLedger(ledgerPath);
    // Reachable entries, recomputed here: eligible for a playable class row (the draft's predicate, with MechanicHolds).
    var reachable = new HashSet<string>(StringComparer.Ordinal);
    foreach (var cls in data.Classes)
    {
        bool secret = DungeonRunAdmission.MechanicHolds(data, cls.ClassSet, "secret");
        foreach (var e in pools.Where(e => e.EligibleFor(cls.ClassSet, m => m == "secret" && secret))) reachable.Add(e.Id);
    }
    var admittedIn = new Dictionary<string, List<DungeonRunBundle>>(StringComparer.Ordinal);
    foreach (var e in pools.Where(e => e.Offerable))
        foreach (var c in e.Candidates)
            foreach (string key in new[] { c.WikiId ?? c.Id, c.Id }.Distinct())
            {
                if (!admittedIn.TryGetValue(key, out var list)) admittedIn[key] = list = new List<DungeonRunBundle>();
                if (!list.Contains(e)) list.Add(e);
            }
    var members = rows.Where(r => r["roles"].Split(';').Contains("bucket member")).ToList();
    int offered = 0, wrongOffer = 0, dualBlocked = 0, wrongDual = 0, excludedOnly = 0;
    var detail = new List<string>();
    foreach (var r in members)
    {
        string id = r["card_id"];
        bool reached = admittedIn.TryGetValue(id, out var es) && es.Any(e => reachable.Contains(e.Id));
        bool listsBuckets = r["player_availability"].StartsWith("offered:") && r["player_availability"].Contains("bucket ");
        if (reached) { offered++; if (!listsBuckets || !es.Where(e => reachable.Contains(e.Id)).All(e => r["player_availability"].Contains("bucket " + e.Id))) { wrongOffer++; detail.Add(id + " not offered as its buckets"); } }
        else if (listsBuckets) { wrongOffer++; detail.Add(id + " offered by an unreachable bucket"); }
        if (!reached && es != null && es.Count > 0 && es.All(e => e.ClassesAll.Count == 2))
        {
            dualBlocked++;
            // Unless another role reaches it (a SoloStone starter or bundle card).
            if (r["player_availability"] == "not offered" && !r["blocked_by"].Split(';').Contains("run:dual-class")) { wrongDual++; detail.Add(id + " lacks run:dual-class"); }
        }
        if (es == null) excludedOnly++;
    }
    Note($"ledger: {members.Count} bucket-member rows; {offered} offered by a reachable catalog entry, {dualBlocked} admitted only for a disabled dual pair, {excludedOnly} not admitted in any entry");
    Check("ledger: every bucket member a reachable catalog entry admits is offered as `bucket <entry>` for each such entry; no other is", wrongOffer == 0 && offered > 0, string.Join(" | ", detail.Take(6)));
    Check("ledger: a member admitted only in entries of a disabled dual pair is blocked by run:dual-class (since phase 6 F10 no pair is disabled: none)", wrongDual == 0 && dualBlocked == 0, dualBlocked + " rows; " + string.Join(" | ", detail.Where(d => d.Contains("dual")).Take(4)));
    Check("ledger: run:bucket-pools is gone (every bucket member has a catalog fact)", !rows.Any(r => r["blocked_by"].Split(';').Contains("run:bucket-pools")));
    Check("ledger: catalog-admitted members validate as passes:catalog; excluded-only members carry the catalog's reason",
        members.Where(r => admittedIn.ContainsKey(r["card_id"])).All(r => r["validation"].StartsWith("passes:catalog") || r["validation"].StartsWith("passes:current data") || r["validation"].StartsWith("passes"))
        && members.Where(r => !admittedIn.ContainsKey(r["card_id"]) && excludedByEntry.Values.Any(l => l.Any(x => x.wiki == r["card_id"] || x.id == r["card_id"]))).All(r => r["validation"].StartsWith("rejected:") || r["validation"].StartsWith("passes:current data")));
    var zeph = rows.Single(r => r["card_id"] == "ULD_003");
    Check("ledger: Zephrys (phase 6 F15) is implemented, offered from the Unique entries, ai ok:highlander, unblocked", zeph["player_availability"].StartsWith("offered:bucket dr.")
        && zeph["classification"] == "implemented" && zeph["ai_support"] == "ok:highlander" && zeph["blocked_by"] == "");
    Check("ledger: the 5 lifted generated-Secret draft cards are offered and admitted for the player",
        new[] { "BOT_402", "DAL_141", "GIL_116", "ICC_204", "UNG_011" }.All(id => rows.Single(r => r["card_id"] == id) is var x && x["player_availability"].StartsWith("offered:")
            && x["ai_support"].StartsWith("ok:secret-generated") && x["classification"] == "implemented"));
    Check("ledger: DALA_BOSS_21p is Whirt's scripted power since phase 8 (no AI block left; waiting only for its schedule to be a default); ULD_429 (scripted since phase 6 F10) is implemented, offered and unblocked", rows.Single(r => r["card_id"] == "DALA_BOSS_21p") is var p21
        && p21["blocked_by"] == "run:default-schedule" && p21["ai_support"] == "ok:secret-generated" && p21["classification"] == "partial"
        && rows.Single(r => r["card_id"] == "ULD_429") is var hp && hp["blocked_by"] == "" && hp["classification"] == "implemented" && hp["player_availability"].StartsWith("offered:"));
    Check("ledger: the dual-class row and its starter are rows of their own", rows.Any(r => r["card_id"] == "run:class:MAGE_ROGUE") && rows.Any(r => r["card_id"] == "run:starter:tombs_reno_spellslinger"));
    // Dual-class blockers: only an explorer whose pairs have no playable row (ULDA_Reno's MAGE_ROGUE ships), and a row of its
    // own for every disabled dual row, blocked by run:dual-class and its disabledReason's capability.
    var byId = rows.ToDictionary(r => r["card_id"], r => r, StringComparer.Ordinal);
    Check("ledger: no explorer is blocked by run:dual-class since phase 6 F10 (all four rows ship), and each is its row's offered portrait",
        new[] { ("ULDA_Reno", "MAGE_ROGUE"), ("ULDA_Finley", "PALADIN_SHAMAN"), ("ULDA_Elise", "DRUID_PRIEST"), ("ULDA_Brann", "HUNTER_WARRIOR") }
            .All(p => !byId[p.Item1]["blocked_by"].Split(';').Contains("run:dual-class") && byId[p.Item1]["player_availability"] == "offered:hero portrait:" + p.Item2));
    Check("ledger: Reno's original powers are not blocked by run:dual-class", rows.Where(r => r["card_id"].StartsWith("ULDA_Reno_HP", StringComparison.Ordinal))
        .All(r => !r["blocked_by"].Split(';').Contains("run:dual-class")) && rows.Any(r => r["card_id"].StartsWith("ULDA_Reno_HP", StringComparison.Ordinal)));
    Check("ledger: the run:class row of every dual row is implemented and unblocked since phase 6 F10 (none ships disabled)",
        data.DisabledClasses.Count == 0 && data.Classes.Where(c => c.IsDual).All(c => byId.TryGetValue("run:class:" + c.Id, out var r) && r["classification"] == "implemented"
            && r["blocked_by"] == "" && r["player_availability"] == "offered:hero row"));
    // Validation and ai_support agree: no row admitted for some scope (ai ok:F) is blocked by the same family (ai:F).
    var contradictions = rows.Where(r => r["blocked_by"].Split(';').Any(t => t.StartsWith("ai:", StringComparison.Ordinal) && r["ai_support"].StartsWith("ok:" + t.Substring(3), StringComparison.Ordinal))).Select(r => r["card_id"]).ToList();
    // Phase 6 F3 scripts GILA_509 (a Tess treasure, catalog runs only): it validates against the current data now. Phase 7 T1
    // scripts TRLA_168 (a teammate the Hunter teammate draft offers): it passes too.
    Check("ledger: no row has ai_support ok:F together with blocked_by ai:F (GILA_509, TRLA_168)", contradictions.Count == 0
        && byId["GILA_509"]["validation"] == "passes:current data" && byId["TRLA_168"]["validation"] == "passes", string.Join(",", contradictions));
    // A player hero validates as the run's portrait would. Phase 6 F0 scripts the 26 named portraits as run heroes (30 Health):
    // the Tombs explorers pass as heroes, and ULDA_Reno, MAGE_ROGUE's portrait, is offered and implemented.
    Check("ledger: the Tombs explorers validate as run heroes (phase 6); ULDA_Reno is MAGE_ROGUE's offered portrait",
        new[] { "ULDA_Reno", "ULDA_Finley", "ULDA_Elise", "ULDA_Brann" }.All(id => byId[id]["validation"] == "passes:hero")
        && byId["ULDA_Reno"]["player_availability"] == "offered:hero portrait:MAGE_ROGUE" && byId["ULDA_Reno"]["classification"] == "implemented"
        && !rows.Any(r => r["validation"] == "rejected:unscripted-portrait"));
    var classes = rows.GroupBy(r => r["classification"]).ToDictionary(g => g.Key, g => g.Count());
    // Phase 5 (I2) moved the 22 active tavern option rows (21 options and the ULDA_912 alias, tavern-v1.json) from missing
    // to implemented: phase 3's golden was 2090 implemented and 1225 missing; nothing else changed classification.
    // Phase 6 F0: ULDA_Reno (MAGE_ROGUE's own portrait now) moved from missing to implemented; nothing else changed classification.
    // Phase 6 F1's regeneration (4442 rows): 2127 implemented, 72 partial, 1192 missing, 1051 duplicate/support, 0 adapted.
    // Phase 6 F2: the 30 Dungeon Run active treasures moved from missing to implemented (LOOTA_842 reachable as the token
    // the blade and the hilt form in hand); 2157 implemented, 72 partial, 1162 missing.
    // Phase 6 F3: Tess and Darius's 22 ids moved from missing to implemented, and 5 rows were added: the hero rows
    // run:class:GILA_500h3 and run:class:GILA_600h and their starters (implemented), and the borrowed enchantment shell
    // TB_BaconShopBadsongE (support); 2183 implemented, 72 partial, 1140 missing, 1052 duplicate/support.
    // Phase 6 F5: the 34 Monster Hunt shared treasure ids moved from missing to implemented (no row added: their tokens and
    // enchantment shells were already support rows); 2239 implemented, 72 partial, 1088 missing, 1052 duplicate/support.
    // Phase 6 F6: the 36 Heist ids moved to implemented (9 portraits, 18 powers, the 9 Random-deck display cards as run
    // services), 14 partial starter cards were completed by their shipped starters, and 45 rows were added: the 9 Heist hero
    // rows, their 35 starters and CORE_WON_096 (Dark Peddler's reprint in DALA_Warlock_Deck2); 2334 implemented, 58 partial,
    // 1052 missing, 1052 duplicate/support.
    // Phase 6 F7: the 31 Heist active treasures move from missing to implemented (no row added): 2365 implemented, 1021 missing.
    // Phase 6 F8: the 15 shipped Heist passive treasures move from missing to implemented (DALA_740 stays missing, rejected);
    // the five borrowed effect shells (GBL_001e, TB_BaconShop_1P_PlayerE, _3ofKindChecke, _8P_PlayerE, TB_DeckRecipe_MyDeckID)
    // become SoloStone support rows: 4501 rows, 2380 implemented, 1006 missing, 1057 duplicate/support.
    // Phase 6 F9: + run:starter:tombs_reno_random; the explorer powers, ULDA_039 and the Random-deck cards scripted (partial while their rows ship disabled).
    // Phase 6 F10: the 28 starter collectibles scripted (missing -> implemented), the three dual rows enabled (their portraits,
    // powers and Random-deck cards partial -> implemented) and 17 rows added (the 12 new starters, the 3 Random decks and 2 more).
    // Phase 6 F11: the 29 Tombs signature treasures, Sr. upgrades and Pack Mule scripted (missing -> implemented; no row added).
    // Phase 6 F12: the 28 Tombs active treasures, Twinspell copies and their tokens' rows scripted (missing -> implemented); ULDA_115 stays missing.
    // Phase 6 F13: the 15 Tombs passive treasures scripted (missing -> implemented; no row added).
    // Phase 6 F14: Rikkar's nine class forms (missing -> implemented) with 9 run:class and 27 run:starter rows added (36 rows,
    // implemented); the 21 cards only the shrine starters held (partial -> implemented); TRLA_209h a support row of the forms.
    // Phase 6 F15: the nine deferrals scripted (ULD_003, ULDA_013, ULDA_040, DALA_707, ULDA_407, ULDA_302, ULDA_045, ULDA_045t,
    // GILA_511: missing -> implemented; no row added); ULDA_111, GILA_900p and GILA_911 stay missing (phase 9).
    // Phase 6 F18 (review): ULDA_039 (the Tomb Divers' power) and Elise's Choose One options ULDA_Elise_HP1a/b are reached
    // (partial -> implemented); the Monster Hunt rows' added powers are recorded adaptations (adapted_by; no class change);
    // the class-row passive powers ULDA_Elise_HP3 and ULDA_Brann_HP3 are judged as hero powers, not boss powers (-> implemented).
    // Phase 7 S1: the three scripted Druid shrines (TRLA_112, 115, 116), the three scripted Druid teammates (TRLA_124, 125, 127)
    // and the two scripted shrine upgrades (TRLA_804, 805) are reached (missing -> implemented, 8 rows); the rest of S1's
    // shrines (TRLA_104, 110, 162, 163, 170, 171) six more rows (2644 / 851).
    // Phase 7 S2: the nine Mage/Paladin/Priest shrines and TRLA_129s Frostfire (made by TRLA_129) are reached (10 rows).
    // Phase 7 S3: the nine Rogue/Shaman/Warlock shrines are reached (9 rows; their forms and enchantments stay support).
    // Phase 7 U: the six shrine upgrades whose grant sits on the shrine (TRLA_801, 802, 803, 806, 807, 808) are scripted, admitted
    // and offered by the shrine-upgrade draft (6 rows; their player enchantments and grants stay support; TRLA_809 is phase 7 D).
    // Phase 7 T1: the 17 Druid/Hunter/Mage/Paladin teammates it scripts are admitted and offered by the teammate draft (17 rows;
    // their enchantments TRLA_131e/169e stay support; the companions TRLA_121/135/139/164 are phase 7 D).
    // Phase 7 T2: the 25 Priest/Rogue/Shaman/Warlock/Warrior teammates are scripted, admitted (TRLA_152/190 hidden-steal, TRLA_191
    // not-started-in-deck, the rest teammate) and offered by the teammate draft (25 rows; TRLA_153e/174e/176e stay support; the
    // companions TRLA_148/156/172/180/188 are phase 7 D).
    // Phase 7 D: the nine start-of-game companions (offered by the teammate draft) and TRLA_809 (offered by the shrine-upgrade
    // draft) are scripted and admitted (10 rows; TRLA_148e/164e/172e/180e and TRLA_809e/e2/e3 stay support): the design's end
    // state, 2721 implemented and 774 missing.
    // Phase 8 family 01 (the encounter foundation): 4567 rows after its first part (2721 implemented, 23 partial, 765 missing);
    // the four Secret bosses add 12 rows (their 4 run:boss rows and 8 basic cards of Elder Brandlemar's deck), script Pressure
    // Plate (ULD_152: implemented) and move the four powers and the Sawblade to partial (their schedule is not a default yet).
    // Phase 8 wave 1 (unscripted collectibles A, B, C): 92 rows leave "missing": 91 are implemented (33 of family 02, 29 of
    // family 03, 29 of family 04) and ULD_216 Puzzle Box of Yogg-Saron is partial (scripted; in no bucket, only in the Tekahn
    // finale decks: blocked by run:special-finales).
    // Phase 8 wave 2 (families 05, 07, 08): + 65 rows, all partial (35 run:boss rows: 18 of family 07, 17 of family 08; 30 cards of
    // their decks that are new to the ledger), and 60 rows leave "missing" for "partial" (18 rules and tokens of family 05, 19 of
    // family 07, 23 of family 08): scripted and admitted, on no default schedule yet.
    // Phase 8 wave 3 (families 06, 09, 10): + 60 rows, all "partial" (26 run:boss rows: 11 of family 09, 15 of family 10; 34
    // cards of their decks that are new to the ledger: 15 and 19), and 70 rows leave "missing" for "partial" (21 rules and
    // tokens of family 06; 25 powers, cards and tokens of family 09; 15 boss powers and 9 boss cards of family 10).
    // Phase 8 family 11: + 19 rows, all "partial" (13 run:boss rows; 6 cards of their decks new to the ledger), and 19 rows leave
    // "missing" for "partial" (14 boss powers and 5 boss cards and tokens).
    // Phase 8 family 12: + 23 rows, all "partial" (13 run:boss rows; 10 cards of their decks that are new to the ledger), and 15
    // rows leave "missing" for "partial" (13 boss powers and the 2 boss cards; the 13 boss heroes stay missing, run:default-schedule).
    // Phase 8 family 13: + 8 run:boss rows (the champions) and + 7 cards of their decks (all partial), TRLA_065p missing -> partial.
    // Wave 4 merged (11, 12, 13): + 56 rows, not 57: FP1_029 Dancing Swords is new in both family 12's and family 13's decks.
    // Phase 8 family 14 (Dalaran Heist bosses A): + 36 rows, all "partial" (15 run:boss rows; 21 cards of their decks that are
    // new to the ledger), and 21 rows leave "missing" for "partial" (16 boss powers and 5 boss cards; "Scourgelord" Dazzik is a
    // hero card and stays "missing" with the boss heroes, blocked by run:default-schedule).
    // Phase 8 family 15: + 29 rows, all partial (14 run:boss, 15 deck cards new to the ledger); 16 rows missing -> partial (14 powers, 2 boss cards).
    // Phase 8 family 16 (Dalaran Heist bosses C): + 34 rows, all "partial" (14 run:boss rows; 20 cards of their decks that are
    // new to the ledger), and 16 rows leave "missing" for "partial" (14 boss powers, PARTY PORTAL! and the Exotic Pet).
    // Phase 8 family 17 (Dalaran Heist bosses D): + 29 rows, all "partial" (14 run:boss rows; 15 cards of their decks that are
    // new to the ledger), and 26 rows leave "missing" for "partial" (14 boss powers and 12 boss cards, Infuriated Banker and the
    // Kirin Tor Guard among them).
    // Phase 8 family 18: + 30 rows (16 run:boss rows, 14 cards of their decks new to the ledger), all partial; 18 rows missing -> partial (16 powers,
    // Kirin Tor Elemental and DALA_830, a run-service rule).
    // Phase 8 family 19 (Tombs bosses A): + 31 rows, all "partial" (18 run:boss rows; 13 cards of its decks that are new to the
    // ledger), and 19 rows leave "missing" for "partial" (the 18 boss powers and Sand Trap).
    // Wave 5 merged (14-19): + 158 rows, not the families' 189: 18 cards are new in the decks of two or three of these families
    // (20 repeats) and 11 were new in wave 4 already; all 158 partial, and 116 rows leave "missing" for "partial".
    // Phase 8 family 20 (Tombs bosses B): + 26 rows, all "partial" (18 run:boss rows; 8 cards of its decks that are new to the
    // ledger, Mogu Cultist ULD_705 among them), and 18 rows leave "missing" for "partial" (the 18 boss powers).
    // Phase 8 family 21 (Tombs bosses C): + 22 rows, all "partial" (17 run:boss rows; 5 cards of its decks that are new to the
    // ledger), and 18 rows leave "missing" for "partial" (its 18 scripted powers).
    // Phase 8 family 22 (Tombs bosses D): + 23 rows, all "partial" (18 run:boss rows; 5 cards of its decks that are new to the
    // ledger: DAL_614, EX1_025, EX1_179, GVG_093, ICC_023), and 19 rows leave "missing" for "partial" (the 18 boss powers and
    // Servant of Siamat).
    // Phase 8 family 23: + 4 rows, all "partial" (the four nemeses' run:boss rows; every card of their decks was in the ledger
    // already), and 10 rows leave "missing" for "partial" (4 boss powers, Crowskin Faithful, Crowskin Pact, Cursed Crewmember and
    // the three Witchwood's Touch).
    Check("ledger golden (phase 8 wave 3: families 06, 09, 10; wave 4: families 11, 12, 13; wave 5: families 14-19): 4918 rows; 2813 implemented, 660 partial, 387 missing, 1058 duplicate/support, 0 intentionally adapted",
        rows.Count == 4918 && classes.GetValueOrDefault("implemented") == 2813 && classes.GetValueOrDefault("partial") == 660 && classes.GetValueOrDefault("missing") == 387
        && classes.GetValueOrDefault("duplicate/support") == 1058 && classes.GetValueOrDefault("intentionally adapted") == 0, string.Join(",", classes.Select(p => p.Key + "=" + p.Value)));
}

static List<Dictionary<string, string>> ReadLedger(string path)
{
    var result = new List<Dictionary<string, string>>();
    var records = new List<List<string>>();
    string text = File.ReadAllText(path);
    var field = new StringBuilder(); var record = new List<string>(); bool quoted = false;
    for (int i = 0; i < text.Length; i++)
    {
        char ch = text[i];
        if (quoted) { if (ch == '"') { if (i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; } else quoted = false; } else field.Append(ch); }
        else if (ch == '"') quoted = true;
        else if (ch == ',') { record.Add(field.ToString()); field.Clear(); }
        else if (ch == '\n') { record.Add(field.ToString()); field.Clear(); records.Add(record); record = new List<string>(); }
        else if (ch != '\r') field.Append(ch);
    }
    if (field.Length > 0 || record.Count > 0) { record.Add(field.ToString()); records.Add(record); }
    var header = records[0];
    foreach (var rec in records.Skip(1).Where(r => r.Count == header.Count))
        result.Add(header.Select((h, i) => (h, i)).ToDictionary(p => p.h, p => rec[p.i]));
    return result;
}
void Note(string text) => Console.WriteLine("NOTE " + text);

Console.WriteLine($"{passes} passed, {failures} failed");
return failures;
