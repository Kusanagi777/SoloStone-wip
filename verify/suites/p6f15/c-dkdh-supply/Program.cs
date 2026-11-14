// c-dkdh-supply: phase 6 F16. Death Knight and Demon Hunter act runs never run out of drafts: the catalog's SoloStone-curated
// buckets (tools/dungeon-run-catalog/curation.json "customBuckets") give each class four themed pool entries. For 20 fixed seeds
// per class, an act run that wins every fight is offered three bundles on each of its fourteen draft floors. Every custom-bucket
// member is admitted by the same predicate the catalog tool uses. The pre-phase-6 catalog (argument 1, optional) is walked the
// same way to show the gap it had. Headless: compiles the run layer against p6f0/engine-out.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SabberStoneCore.Model;

int failures = 0, passes = 0;
void Check(string name, bool ok, string detail = "")
{
    if (ok) passes++; else failures++;
    if (!ok || Environment.GetEnvironmentVariable("VERBOSE") == "1")
        Console.WriteLine((ok ? "PASS " : "FAIL ") + name + (detail.Length > 0 ? "  (" + detail + ")" : ""));
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
string root = VerifyPaths.Data;
string R(string f) => File.ReadAllText(Path.Combine(root, f));
DungeonRunData Load(string catalog) => DungeonRunData.Parse(R("dungeon-run/run-v1.json"), R("dungeon-run/pool-v1.json"), R("npc/decks-v1.json"),
    catalog, R("dungeon-run/schedule-v1.json"), R("dungeon-run/tavern-v1.json"));
var data = Load(R("dungeon-run/catalog-v1.json"));
var fresh = new DungeonRunProfile();
long[] seeds = Enumerable.Range(1, 20).Select(i => 7919L * i + 13).ToArray();

// One act run that wins every fight: per won fight but the last, the number of bundles offered (0 = no draft) and whether the
// deck was already too full for a pick.
List<(int Floor, int Offers, bool Full, int Options)> Walk(DungeonRun run, string cls, long seed)
{
    var s = run.NewRun(fresh, cls, run.Data.Class(cls).DefaultDeck, run.Data.Class(cls).Powers[0], seed, signatureId: run.SignatureOffer(fresh, run.Data.FindClass(cls), seed).FirstOrDefault());
    var trace = new List<(int, int, bool, int)>();
    int lastFight = run.ScheduleFor(s).FloorCount;
    for (int guard = 0; guard < 500 && !s.Finished; guard++)
    {
        switch (s.Phase)
        {
            case DungeonRunPhase.PreMatch:
                int floor = s.Floor;
                bool full = s.Deck.Count + run.DraftTakes(s) > run.DeckMax(s);
                run.BeginMatch(s); run.RecordOutcome(s, fresh, DungeonRunOutcome.Win);
                if (floor == lastFight) break;
                // The treasure draft (if any) comes first; the bundle draft follows it.
                if (s.Phase == DungeonRunPhase.TreasureDraft) run.PickTreasure(s, s.OpenDraft.Offered[0]);
                // The options the act (and its active modifiers: a draftOptionsDelta) allows this draft.
                trace.Add((floor, s.Phase == DungeonRunPhase.BundleDraft ? s.OpenDraft.Offered.Count : 0, full, s.Phase == DungeonRunPhase.BundleDraft ? run.DraftOptions(s) : 3));
                break;
            case DungeonRunPhase.TreasureDraft: run.PickTreasure(s, s.OpenDraft.Offered[0]); break;
            case DungeonRunPhase.Modifier: run.PickModifier(s, s.ModifierOffer.Offered[0]); break;
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
    if (!s.Finished) throw new Exception("the run did not finish");
    return trace;
}

// ---------------------------------------------------------------- S1 the custom buckets
{
    var custom = data.Bundles.Where(b => b.Provenance.Any(p => p.Adventure == "SoloStone-curated")).ToList();
    // Phase 6 F18 (review): seven per class (four ran an always-pick-last act run short on floors 13-14; c-acts A12 walks it).
    Check("S1 seven SoloStone-curated pool entries per class (Death Knight, Demon Hunter)", custom.Count == 14
        && custom.Count(b => b.ClassSet.SequenceEqual(new[] { "DEATHKNIGHT" })) == 7 && custom.Count(b => b.ClassSet.SequenceEqual(new[] { "DEMONHUNTER" })) == 7,
        string.Join(",", custom.Select(b => b.Id)));
    Check("S1 each is an offerable pool entry of at least 6 candidates, no grant, no class pair, no mechanic", custom.All(b => b.IsPool && b.Offerable && b.Candidates.Count >= 6
        && b.Candidates.All(c => !c.Grant) && b.ClassesAll.Count == 0 && b.RequiresMechanic == null && !b.Legacy && b.Themes.Count > 0));
    var policy = OfflinePoolPolicy.Load();
    var refused = custom.SelectMany(b => b.Candidates.Select(c => (b, c, why: DungeonRunValidator.PoolCardReason(c.Id, b.ClassSet, false, policy, catalogRun: true)
        ?? (DungeonRunValidator.HasRuneCost(c.Id) ? "rune-cost" : null)))).Where(x => x.why != null).ToList();
    Check("S1 every custom-bucket member is admitted (the catalog tool's predicate) and carries no rune cost", refused.Count == 0,
        string.Join(" | ", refused.Select(x => x.b.Id + ":" + x.c.Id + ":" + x.why)));
    Check("S1 every member is a collectible, scripted card of its entry's class", custom.All(b => b.Candidates.All(c => Cards.FromId(c.Id).Collectible && Cards.FromId(c.Id).Implemented
        && Cards.FromId(c.Id).Class.ToString() == b.ClassSet[0])));
    Check("S1 the source tag of a curated offer is SoloStone", custom.All(b => DungeonRun.SourceTag(b) == "SoloStone"));
    Check("S1 the shipped data validates", DungeonRunValidator.Validate(data, null).Count == 0);
}

// ---------------------------------------------------------------- S2 the supply check: every draft floor of 20 seeds per class
var run = new DungeonRun(data);
foreach (string cls in new[] { "DEATHKNIGHT", "DEMONHUNTER" })
{
    int floors = 0, three = 0, full = 0, cut = 0; var dry = new List<string>(); var thin = new List<string>();
    foreach (long seed in seeds)
    {
        var trace = Walk(run, cls, seed);
        if (trace.Count != 14) dry.Add(seed + ":" + trace.Count + " draft floors");
        foreach (var t in trace)
        {
            floors++;
            if (t.Offers == 0 && t.Full) { full++; continue; }
            if (t.Offers == 0) dry.Add(seed + "@" + t.Floor);
            else if (t.Offers < t.Options) thin.Add(seed + "@" + t.Floor + "=" + t.Offers + "/" + t.Options);
            else if (t.Options < 3) cut++;
            else three++;
        }
    }
    Check($"S2 {cls}: 20 act runs, 14 draft floors each, never run dry (no floor without a draft but a full deck)", dry.Count == 0, string.Join(" ", dry.Take(12)));
    Check($"S2 {cls}: every draft offers the full option count (3, or fewer only where an active modifier cuts the act's draft options)", thin.Count == 0 && three + cut + full == floors,
        $"{three} of {floors} with 3 offers, {cut} cut by a modifier, {full} full-deck skips; thin {string.Join(" ", thin.Take(10))}");
    Console.WriteLine($"{cls}: {floors} draft floors over {seeds.Length} seeds, {three} offered 3 bundles, {cut} offered fewer under a draftOptionsDelta modifier, {full} skipped for a full deck");
}

// ---------------------------------------------------------------- S3 the class supply arithmetic and the other classes
{
    foreach (string cls in new[] { "DEATHKNIGHT", "DEMONHUNTER" })
    {
        var classes = new[] { cls };
        int fixedOwn = data.Bundles.Count(b => !b.IsPool && b.Offerable && b.ClassSet.SequenceEqual(classes));
        int poolOwn = data.Bundles.Count(b => b.IsPool && b.Offerable && b.EligibleFor(classes, m => false) && !b.IsNeutral);
        int neutralTakes = data.Bundles.Where(b => b.Offerable && b.IsNeutral).Sum(b => b.IsPool ? run.MaxTakes(b) : 1);
        int takes = fixedOwn + poolOwn * data.Rules.BucketMaxTakes + neutralTakes;
        Check($"S3 {cls}: the class's own takes ({fixedOwn} fixed + {poolOwn} pool x {data.Rules.BucketMaxTakes}) plus the neutral ones cover the 14 draft floors",
            fixedOwn == 4 && poolOwn == 7 && takes >= 14, $"total {takes} (neutral {neutralTakes})");
    }
    // Another class still drafts as before: no curated entry reaches it.
    Check("S3 a Mage run is never offered a curated entry", data.Bundles.Where(b => b.Provenance.Any(p => p.Adventure == "SoloStone-curated")).All(b => !b.EligibleFor(new[] { "MAGE" }, m => true)));
}

// ---------------------------------------------------------------- S4 the gap before (optional: the pre-phase-6 catalog)
if (args.Length > 0 && File.Exists(args[0]))
{
    var before = new DungeonRun(Load(File.ReadAllText(args[0])));
    foreach (string cls in new[] { "DEATHKNIGHT", "DEMONHUNTER" })
    {
        int dryBefore = 0, runsShort = 0;
        foreach (long seed in seeds)
        {
            var trace = Walk(before, cls, seed);
            int d = trace.Count(t => t.Offers == 0 && !t.Full);
            dryBefore += d; if (d > 0) runsShort++;
        }
        Console.WriteLine($"before phase 6, {cls}: {runsShort} of {seeds.Length} runs had draft floors with nothing on offer ({dryBefore} floors)");
        Check($"S4 {cls}: the pre-phase-6 catalog ran dry (the gap this phase closes)", runsShort > 0);
    }
}

Console.WriteLine($"c-dkdh-supply: {passes} passed, {failures} failed");
return failures == 0 ? 0 : 1;
