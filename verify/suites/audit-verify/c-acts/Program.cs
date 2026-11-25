// c-acts: phase 4 (I1) of the unified Dungeon Run: the act/floor schedule (schedule-v1.json), act boss selection, run state
// version 4 and its migration, the node walk (combat, major, tavern, modifier), two-card drafts, the majorsCleared ladder,
// save/resume at every node phase, the Deck mirror of the card records, and the act progress line. Headless: compiles
// C:\SoloStone\plugin\DungeonRun\*.cs (minus the client-only runtime and boss pilot) against audit-verify/engine-out.
// usage: AiHarness <p4 golden-out dir> <content2-fixtures dir>
// The golden directory is written by p4/golden from the PRE-CHANGE run layer (p4/before): boss sequences, full legacy-run
// digests and version-3 save files.
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
void Throws<T>(string name, Action action, string expectContains = null) where T : Exception
{
    try { action(); Check(name, false, "did not throw"); }
    catch (T e) { Check(name, expectContains == null || e.Message.Contains(expectContains), e.Message); }
    catch (Exception e) { Check(name, false, e.GetType().Name + ": " + e.Message); }
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

string golden = args.Length > 0 ? args[0] : VerifyPaths.Fixture("p4/golden-out");
string fixtures = args.Length > 1 ? args[1] : VerifyPaths.Fixture("content2-fixtures");
string tmpRoot = args.Length > 2 ? args[2] : Path.Combine(Path.GetTempPath(), "c-acts");
Directory.CreateDirectory(tmpRoot);
string root = VerifyPaths.Data;
string runJson = File.ReadAllText(Path.Combine(root, "dungeon-run", "run-v1.json"));
string poolJson = File.ReadAllText(Path.Combine(root, "dungeon-run", "pool-v1.json"));
string npcJson = File.ReadAllText(Path.Combine(root, "npc", "decks-v1.json"));
string catalogJson = File.ReadAllText(Path.Combine(root, "dungeon-run", "catalog-v1.json"));
string scheduleJson;
using (var stream = typeof(DungeonRun).Assembly.GetManifestResourceStream("SoloStone.DungeonRun.schedule-v1.json"))
using (var reader = new StreamReader(stream ?? throw new InvalidDataException("schedule-v1.json is not embedded")))
    scheduleJson = reader.ReadToEnd();
Check("schedule-v1.json is embedded and equals the data file", scheduleJson == File.ReadAllText(Path.Combine(root, "dungeon-run", "schedule-v1.json")));
var scheduleBytes = File.ReadAllBytes(Path.Combine(root, "dungeon-run", "schedule-v1.json"));
Check("schedule-v1.json: UTF-8 without BOM, LF only", !(scheduleBytes.Length > 2 && scheduleBytes[0] == 0xEF) && !scheduleBytes.Contains((byte)'\r'));

DungeonRunData Load(string schedule = null, bool catalog = true) => DungeonRunData.Parse(runJson, poolJson, npcJson, catalog ? catalogJson : null, schedule ?? scheduleJson);
var data = Load();
var run = new DungeonRun(data);
var legacyData = DungeonRunData.Parse(runJson, poolJson, npcJson);
var catalogLegacy = DungeonRunData.Parse(runJson, poolJson, npcJson, catalogJson);
var acts = data.Schedules.Find("acts-v1");
var ser = new System.Xml.Serialization.XmlSerializer(typeof(DungeonRunSave));
string Xml(DungeonRunSave save) { using var w = new StringWriter(); ser.Serialize(w, save); return w.ToString(); }
// The recorded hashes (golden.txt run lines, the A6 digest) were taken over Windows (CRLF) XML: hash those bytes on every platform.
string Hash(string text) { using var sha = System.Security.Cryptography.SHA256.Create(); return BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(VerifyPaths.WindowsText(text)))).Replace("-", "").Substring(0, 16); }
var fresh = new DungeonRunProfile();
var all = new DungeonRunProfile(); all.Unlocked.AddRange(data.Unlocks.Keys);
int tmpCount = 0;
string TempDir() { string d = Path.Combine(tmpRoot, "t" + System.Diagnostics.Process.GetCurrentProcess().Id + "-" + (tmpCount++)); Directory.CreateDirectory(d); foreach (var f in Directory.GetFiles(d)) File.Delete(f); return d; }

// ---------------------------------------------------------------- A1 schedule parse and flatten
{
    var nodes = acts.Nodes;
    var fights = nodes.Where(n => n.IsFight).ToList();
    Check("A1 acts-v1: 15 combat/major nodes, 5 tavern nodes, 2 modifier nodes",
        fights.Count == 15 && nodes.Count(n => n.Kind == DungeonRunNodeKind.Tavern) == 5 && nodes.Count(n => n.Kind == DungeonRunNodeKind.Modifier) == 2,
        $"{fights.Count}/{nodes.Count(n => n.Kind == DungeonRunNodeKind.Tavern)}/{nodes.Count(n => n.Kind == DungeonRunNodeKind.Modifier)}");
    Check("A1 fight floors run 1..15 strictly increasing", fights.Select(n => n.Floor).SequenceEqual(Enumerable.Range(1, 15)) && acts.FloorCount == 15);
    Check("A1 each act's last fight is its Major, the others Combat", Enumerable.Range(1, 3).All(a =>
    {
        var f = fights.Where(n => n.Act == a).ToList();
        return f.Count == 5 && f.Last().Kind == DungeonRunNodeKind.Major && f.Take(4).All(n => n.Kind == DungeonRunNodeKind.Combat)
            && f.Select(n => n.FloorInAct).SequenceEqual(Enumerable.Range(1, 5));
    }));
    Check("A1 node indices 0..n-1, floors never go back", nodes.Select(n => n.Index).SequenceEqual(Enumerable.Range(0, nodes.Count))
        && nodes.Zip(nodes.Skip(1), (x, y) => y.Floor >= x.Floor).All(b => b));
    string Letter(DungeonRunNodeKind kind) => kind == DungeonRunNodeKind.Combat ? "C" : kind == DungeonRunNodeKind.Major ? "J" : kind == DungeonRunNodeKind.Tavern ? "T" : "X";
    string shape = string.Join(" ", nodes.Select(n => Letter(n.Kind) + n.Act + "." + n.FloorInAct));
    Check("A1 node order: modifier (X) first in acts II and III, taverns (T) before floors 4 / 3,5 / 3,5, the major (J) last",
        shape == "C1.1 C1.2 C1.3 T1.4 C1.4 J1.5 X2.1 C2.1 C2.2 T2.3 C2.3 C2.4 T2.5 J2.5 X3.1 C3.1 C3.2 T3.3 C3.3 C3.4 T3.5 J3.5", shape);
    Check("A1 a tavern or modifier node carries the act, floor in act and floor of the fight it precedes",
        nodes.Where(n => !n.IsFight).All(n => nodes.Skip(n.Index + 1).First(x => x.IsFight) is var next && next.Floor == n.Floor && next.FloorInAct == n.FloorInAct && next.Act == n.Act));
    Check("A1 treasure flags: act I floors 1 and 3; acts II-III floor 1 (passive only) and 2", string.Join(",", fights.Where(n => n.TreasureDraft).Select(n => n.Floor + (n.PassiveOnly ? "p" : "")))
        == "1,3,6p,7,11p,12", string.Join(",", fights.Where(n => n.TreasureDraft).Select(n => n.Floor + (n.PassiveOnly ? "p" : ""))));
    Check("A1 knobs: deckMin 10, deckMax 50; act II-III drafts take 2 of 3; tavern gold 2/2/3 and offers 3/4/4",
        acts.DeckMin == 10 && acts.DeckMax == 50 && acts.Acts.Select(a => a.DraftTakes).SequenceEqual(new[] { 3, 2, 2 })
        && acts.Acts.Select(a => a.TavernGold).SequenceEqual(new[] { 2, 2, 3 }) && acts.Acts.Select(a => a.TavernOffers).SequenceEqual(new[] { 3, 4, 4 }));
    // Phase 7 adds acts-v2 (acts-v1 plus the teammate and shrine-upgrade draft flags) as the default, at scheduleVersion 2.
    // The close-out (family 25) made acts-v3h and acts-v3t the defaults; this data set is loaded without encounters-v1.json, so a
    // new run on it follows "defaultWithoutEncounters", acts-v2 (the phase-7 default, still stamped 2). Step 9 (wave 9b) made
    // acts-v4h and acts-v4t (stamp 4) the defaults and the file version 4; acts-v3h and acts-v3t stay, stamped 3.
    Check("A1 defaults acts-v4h and acts-v4t (step 9; acts-v2 for a data set without the encounter file, as this one: phase 7's default, file version 4, acts-v2 still stamps 2, acts-v3h 3), 4 modifiers, finale disabled; act1 has one act",
        data.Schedules.Default == "acts-v4h" && data.Schedules.Defaults.SequenceEqual(new[] { "acts-v4h", "acts-v4t" }) && data.Schedules.DefaultWithoutEncounters == "acts-v2"
        && new DungeonRun(data).DefaultSchedule(1) == "acts-v2" && data.Schedules.Find("acts-v2").StampVersion == 2 && data.Schedules.Find("acts-v3h").StampVersion == 3
        && data.Schedules.Find("acts-v4h").StampVersion == 4 && data.Schedules.Find("acts-v4t").StampVersion == 4
        && data.Schedules.ScheduleVersion == 4 && acts.StampVersion == 2 && data.Schedules.Modifiers.Count == 4 && !acts.FinaleEnabled
        && data.Schedules.Find("act1").Acts.Count == 1 && data.Schedules.Find("act1").Nodes.Count(n => n.IsFight) == 5);
    Check("A1 boss pools default from band/final; provenance solostone; no fixedHealth in the shipped rows",
        data.Bosses.All(b => b.Pools.SequenceEqual(new[] { b.Final ? "final" : "band" + b.Band }) && b.Provenance == "solostone" && !b.FixedHealth && b.BossManaCap == null));
    Throws<InvalidDataException>("A1 a schedule named \"legacy\" is refused", () => DungeonRunSchedules.Parse(scheduleJson.Replace("\"act1\":", "\"legacy\":")), "reserved");
}

// ---------------------------------------------------------------- A2 validator
{
    var errors = DungeonRunValidator.Validate(data, null);
    Check("A2 the shipped data with schedules validates", errors.Count == 0, string.Join(" | ", errors.Take(8)));
    string First(string text, string old, string replacement)
    {
        int at = text.IndexOf(old, StringComparison.Ordinal);
        if (at < 0) throw new InvalidOperationException("mutation anchor missing: " + old);
        return text.Substring(0, at) + replacement + text.Substring(at + old.Length);
    }
    List<string> Mutated(string json) => DungeonRunValidator.ValidateSchedules(Load(json));
    var shortHealth = Mutated(First(scheduleJson, "\"bossHealth\": [30, 32, 34, 36, 40]", "\"bossHealth\": [30, 32, 34, 36]"));
    Check("A2 bossHealth too short is refused", shortHealth.Any(e => e.StartsWith("schedule acts-v1 act I: bossHealth has 4 values, needs 5")), string.Join(" | ", shortHealth));
    var tavern6 = Mutated(First(scheduleJson, "\"taverns\": [3]", "\"taverns\": [6]"));
    Check("A2 taverns index 6 is refused", tavern6.Any(e => e.StartsWith("schedule acts-v1 act I: taverns 6 is outside 1..4")), string.Join(" | ", tavern6));
    var tavern5 = Mutated(First(scheduleJson, "\"taverns\": [3]", "\"taverns\": [5]"));
    Check("A2 a tavern after the major (index = floors) is refused", tavern5.Any(e => e.Contains("taverns 5 is outside 1..4")), string.Join(" | ", tavern5));
    var big = Mutated(First(scheduleJson, "\"deckMax\": 50", "\"deckMax\": 120"));
    Check("A2 deckMax 120 is refused", big.Any(e => e == "schedule acts-v1: deckMax 120 is above the engine deck capacity 99"), string.Join(" | ", big));
    var small = Mutated(First(scheduleJson, "\"deckMin\": 10", "\"deckMin\": 9"));
    Check("A2 deckMin below the starter size is refused", small.Any(e => e.Contains("deckMin 9 is below the starter size 10")), string.Join(" | ", small));
    var mana = Mutated(First(scheduleJson, "\"bossMana\": [0, 0, 0, 0, 1]", "\"bossMana\": [0, 0, 1]"));
    Check("A2 bossMana too short is refused", mana.Any(e => e.Contains("bossMana has 3 values, needs 5")), string.Join(" | ", mana));
    var counts = Mutated(First(scheduleJson, "{\"pool\": \"band1\", \"count\": 4}", "{\"pool\": \"band1\", \"count\": 3}"));
    Check("A2 combat counts that do not fill floors - 1 are refused", counts.Any(e => e.Contains("combat counts sum to 3, need floors - 1 = 4")), string.Join(" | ", counts));
    var pool = Mutated(First(scheduleJson, "\"major\": {\"pool\": \"band3\"}", "\"major\": {\"pool\": \"band9\"}"));
    Check("A2 an unknown pool is refused", pool.Any(e => e.Contains("unknown major pool band9")), string.Join(" | ", pool));
    var knob = Mutated(scheduleJson.Replace("\"bossHealthDelta\": 5,", "\"bossHealthDelta\": 5, \"bossDeck\": \"x\","));
    Check("A2 a modifier knob outside the whitelist (bossDeck) is refused", knob.Any(e => e == "modifier mod_iron_bosses: unknown knob bossDeck"), string.Join(" | ", knob));
    var twice = Mutated(scheduleJson.Replace("\"id\": \"mod_lean_supply\"", "\"id\": \"mod_iron_bosses\""));
    Check("A2 a duplicate modifier id is refused", twice.Any(e => e == "modifier mod_iron_bosses: listed twice"), string.Join(" | ", twice));
    var defaultBad = Mutated(First(scheduleJson, "\"default\": \"acts-v4h\"", "\"default\": \"acts-v9\""));
    Check("A2 a default that is no schedule is refused", defaultBad.Any(e => e.Contains("default acts-v9 is not a schedule")), string.Join(" | ", defaultBad));
    var finale = Mutated(First(scheduleJson, "\"finale\": {\"enabled\": false", "\"finale\": {\"enabled\": true"));
    Check("A2 an enabled finale is refused until phase 8", finale.Any(e => e.Contains("the finale is enabled")), string.Join(" | ", finale));
    // A pool infeasible after removing three band-1 bosses: act I needs four distinct band-1 bosses.
    var thin = Load();
    foreach (var b in thin.Bosses.Where(b => b.Band == 1 && !b.Final).Take(3).ToList()) thin.Bosses.Remove(b);
    var infeasible = DungeonRunValidator.ValidateSchedules(thin);
    Check("A2 a pool infeasible after removing 3 band-1 bosses is refused", infeasible.Any(e => e == "schedule acts-v1 act I: pool band1 is infeasible: needs 4 distinct bosses, has 3"), string.Join(" | ", infeasible));
    var badMod = Load();
    badMod.Bosses[0].IncompatibleModifiers.Add("mod_nope");
    Check("A2 incompatibleModifiers naming an unknown modifier is refused", DungeonRunValidator.ValidateSchedules(badMod).Any(e => e.Contains("unknown modifier mod_nope")));
    Check("A2 the legacy eight-floor invariant still holds and is checked for the legacy rules", DungeonRunValidator.Validate(legacyData, null).Count == 0);
}

// ---------------------------------------------------------------- A3 act boss sequence
{
    int bad = 0, dupes = 0, rerunDiffer = 0, challenge = 0;
    var notes = new List<string>();
    var pools = new Dictionary<string, HashSet<string>>();
    foreach (var b in data.Bosses) foreach (string p in b.Pools) { if (!pools.ContainsKey(p)) pools[p] = new HashSet<string>(); pools[p].Add(b.Id); }
    for (int i = 0; i < 200; i++)
    {
        long seed = 1000003L * i + 17;
        foreach (var (name, profile) in new[] { ("fresh", fresh), ("all", all) })
        {
            var seq = run.ActBossSequence(profile, seed, acts);
            var again = run.ActBossSequence(profile, seed, acts);
            if (!seq.SequenceEqual(again)) rerunDiffer++;
            if (seq.Distinct().Count() != seq.Count) dupes++;
            var bosses = seq.Select(data.Boss).ToList();
            bool ok = seq.Count == 15
                && bosses.Take(4).All(b => b.Pools.Contains("band1")) && bosses[4].Pools.Contains("band3")
                && bosses.Skip(5).Take(4).All(b => b.Pools.Contains("band2")) && bosses[9].Pools.Contains("band3")
                && bosses.Skip(10).Take(4).All(b => !b.Final && b.Band >= 1 && b.Band <= 3) && bosses[14].Final
                && bosses.Take(14).All(b => !b.Final);
            if (name == "fresh" && bosses[14].Challenge) ok = false;
            if (name == "all" && bosses[14].Challenge) challenge++;
            if (!ok) { bad++; if (notes.Count < 3) notes.Add(name + " " + seed + ": " + string.Join(",", seq)); }
        }
    }
    Check("A3 400 sequences: 4 band1 + band3 major, 4 band2 + band3 major, 4 leftover + final major; 15 bosses", bad == 0, string.Join(" | ", notes));
    Check("A3 0 duplicates over 400 sequences (12 distinct non-final + 3 majors)", dupes == 0, dupes.ToString());
    Check("A3 a rerun gives the same sequence", rerunDiffer == 0);
    Check("A3 Act III major is never the challenge final without the challenge unlock; with it, it can be", challenge > 0, challenge + " of 200");
    var s = run.NewRun(fresh, "MAGE", data.Class("MAGE").DefaultDeck, data.Class("MAGE").DefaultPower, 4242, signatureId: run.SignatureOffer(fresh, run.Data.FindClass("MAGE"), 4242).FirstOrDefault());
    Check("A3 NewRun stores the act sequence (CurrentBoss by Floor - 1 is unchanged)", s.BossSequence.SequenceEqual(run.ActBossSequence(fresh, 4242, acts))
        && run.CurrentBoss(s).Id == s.BossSequence[0]);
    Check("A3 the act sequence uses its own stream (differs from the legacy one for the same seed)",
        !run.ActBossSequence(fresh, 4242, acts).Take(3).SequenceEqual(run.BossSequence(fresh, 4242).Take(3)));
}

// ---------------------------------------------------------------- A4 legacy untouched
{
    var lines = File.ReadAllLines(Path.Combine(golden, "golden.txt"));
    var legacyRun = new DungeonRun(legacyData);
    int seqOk = 0, seqN = 0, viaNewRun = 0;
    foreach (string line in lines.Where(l => l.StartsWith("seq ")))
    {
        var parts = line.Split(' ');
        var profile = parts[1] == "all" ? all : fresh;
        long seed = long.Parse(parts[2]);
        seqN++;
        if (string.Join(",", legacyRun.BossSequence(profile, seed)) == parts[3] && string.Join(",", run.BossSequence(profile, seed)) == parts[3]) seqOk++;
        var cls = data.Class("ROGUE");
        var st = run.NewRun(profile, cls.Id, cls.DefaultDeck, cls.DefaultPower, seed, schedule: DungeonRunSchedules.LegacyId, signatureId: run.SignatureOffer(profile, run.Data.FindClass(cls.Id), seed).FirstOrDefault());
        if (string.Join(",", st.BossSequence) == parts[3] && !st.IsActRun && st.Cards.Count == 0) viaNewRun++;
    }
    Check("A4 legacy BossSequence equals the pre-change output for 200 seeds x 2 profiles", seqN == 400 && seqOk == 400, seqOk + "/" + seqN);
    Check("A4 NewRun(schedule: legacy) on schedule data is a ScheduleVersion-0 run with the golden sequence", viaNewRun == 400, viaNewRun + "/400");
    Check("A4 without schedules loaded, NewRun is a legacy run", new DungeonRun(legacyData).NewRun(fresh, "MAGE", "mage_rimeward", "CS2_034", 9).ScheduleVersion == 0);
    // Full scripted all-win runs exactly as p4/golden played them on the pre-change code: identical final saves (the root
    // Version is the only byte that moves, 3 -> 4).
    // Phase 6 F16 adds eight SoloStone-curated Death Knight and Demon Hunter pool entries to the catalog, so a catalog-data legacy
    // run of those two classes drafts differently by design. Every run is replayed on the current data and on the same data with
    // the pre-phase-6 catalog (scratchpad p6f0/catalog-v1.before.json): the second must match the golden byte for byte (nothing
    // else moved), and on the first only Death Knight and Demon Hunter catalog runs may differ. A MAGE_ROGUE run's save gains its
    // Loadout element (phase 6 F0: a hero row's run records it); that one line is removed before hashing.
    string preP6Catalog = VerifyPaths.Fixture("p6f0/catalog-v1.before.json");
    var catalogLegacyPre = File.Exists(preP6Catalog) ? DungeonRunData.Parse(runJson, poolJson, npcJson, File.ReadAllText(preP6Catalog)) : null;
    var dataPre = File.Exists(preP6Catalog) ? DungeonRunData.Parse(runJson, poolJson, npcJson, File.ReadAllText(preP6Catalog), scheduleJson) : null;
    // Phase 6 F9 replaces MAGE_ROGUE's Mage stand-in powers with Reno's own (its default power is now ULDA_Reno_HP1), so a
    // MAGE_ROGUE catalog run moves by design too; with the pre-F9 run data (scratchpad p6f9/run-v1.before.json) and the
    // pre-phase-6 catalog it must still replay the golden byte for byte.
    string preF9Run = VerifyPaths.Fixture("p6f9/run-v1.before.json");
    string runJsonPreF9 = File.Exists(preF9Run) ? File.ReadAllText(preF9Run) : null;
    var catalogLegacyPreF9 = runJsonPreF9 != null && File.Exists(preP6Catalog) ? DungeonRunData.Parse(runJsonPreF9, poolJson, npcJson, File.ReadAllText(preP6Catalog)) : null;
    var dataPreF9 = runJsonPreF9 != null && File.Exists(preP6Catalog) ? DungeonRunData.Parse(runJsonPreF9, poolJson, npcJson, File.ReadAllText(preP6Catalog), scheduleJson) : null;
    // Phase 6 F10 scripts 28 starter collectibles, which join the catalog's buckets (+40 admitted members), so a catalog-data
    // class run may draft differently too: such a run must replay the golden on the same data with the pre-F10 catalog
    // (scratchpad p6f10/before/catalog-v1.before.json), which proves the new members are the only change.
    string preF10Catalog = VerifyPaths.Fixture("p6f10/before/catalog-v1.before.json");
    var catalogLegacyPreF10 = File.Exists(preF10Catalog) ? DungeonRunData.Parse(runJson, poolJson, npcJson, File.ReadAllText(preF10Catalog)) : null;
    var dataPreF10 = File.Exists(preF10Catalog) ? DungeonRunData.Parse(runJson, poolJson, npcJson, File.ReadAllText(preF10Catalog), scheduleJson) : null;
    string Replay(DungeonRun r, string[] parts)
    {
        long seed = long.Parse(parts[2]);
        var cls = r.Data.Class(parts[3]);
        // Phase 6 F3 on: the single-class named hero rows (run-v1.json heroRows) add their own unlock ladder keys, which the
        // golden profile predates; they unlock nothing a class run uses, so the replay profile leaves them out.
        // Phase 6 F10 enables PALADIN_SHAMAN, DRUID_PRIEST and HUNTER_WARRIOR (disabled when the golden was recorded): their
        // ladder keys join the data, unlock nothing a class run uses, and are left out the same way.
        var heroKeys = new HashSet<string>(r.Data.Classes.Where(c => c.IsHero && (c.ClassSet.Count == 1 || c.Id != "MAGE_ROGUE")).Select(c => c.Id.ToLowerInvariant() + "_"));
        // Phase 6 F11 adds the Tombs signature unlocks (<row>_bosses5, _majors2-4), which the golden profile predates as well.
        var profile = new DungeonRunProfile(); profile.Unlocked.AddRange(r.Data.Unlocks.Keys.Where(k => !heroKeys.Any(k.StartsWith)
            && !(k.EndsWith("_bosses5") || k.EndsWith("_majors2") || k.EndsWith("_majors3") || k.EndsWith("_majors4"))));
        var s = r.NewRun(profile, cls.Id, cls.DefaultDeck, cls.DefaultPower, seed, schedule: DungeonRunSchedules.LegacyId, signatureId: r.SignatureOffer(profile, r.Data.FindClass(cls.Id), seed).FirstOrDefault());
        var save = new DungeonRunSave { Profile = profile, Active = s };
        while (!s.Finished)
        {
            if (s.Phase == DungeonRunPhase.PreMatch) { r.BeginMatch(s); r.RecordOutcome(s, profile, DungeonRunOutcome.Win); }
            else if (s.Phase == DungeonRunPhase.TreasureDraft) r.PickTreasure(s, s.OpenDraft.Offered[(int)(seed % s.OpenDraft.Offered.Count)]);
            else if (s.Phase == DungeonRunPhase.BundleDraft) r.PickBundle(s, s.OpenDraft.Offered[s.Floor % s.OpenDraft.Offered.Count]);
        }
        // Phase 7: the save version is 5; a legacy run's body is unchanged.
        string xml = Xml(save).Replace("Version=\"" + DungeonRunSave.CurrentVersion + "\">", "Version=\"3\">");
        // Phase 6 F0 records a named hero row's loadout (MAGE_ROGUE is a Tombs hero row): the one element the golden predates.
        if (cls.IsHero)
        {
            string loadout = "<Loadout Id=\"" + cls.Id + "\" />";
            int at = xml.IndexOf(loadout, StringComparison.Ordinal);
            if (at < 0) return "no-loadout";
            int lineStart = xml.LastIndexOf((char)10, at), lineEnd = xml.IndexOf((char)10, at);
            xml = xml.Remove(lineStart, lineEnd - lineStart);
        }
        return Hash(xml);
    }
    int runOk = 0, runN = 0, preOk = 0, preN = 0, dkdhMoved = 0, renoMoved = 0, f10Moved = 0;
    var differ = new List<string>(); var otherMoved = new List<string>();
    foreach (string line in lines.Where(l => l.StartsWith("run ")))
    {
        var parts = line.Split(' ');
        bool catalogLine = parts[1] != "legacy";
        var d = catalogLine ? catalogLegacy : legacyData;
        // The same data with the schedules loaded: a legacy run must not notice them.
        var withSchedules = catalogLine ? data : Load(null, catalog: false);
        int variant = 0;
        foreach (var r in new[] { new DungeonRun(d), new DungeonRun(withSchedules) })
        {
            runN++;
            string h = Replay(r, parts);
            var preF10 = variant++ == 0 ? catalogLegacyPreF10 : dataPreF10;
            if (h == parts[4]) runOk++;
            else if (catalogLine && (parts[3] == "DEATHKNIGHT" || parts[3] == "DEMONHUNTER")) dkdhMoved++;
            else if (catalogLine && parts[3] == "MAGE_ROGUE") renoMoved++;
            else if (catalogLine && preF10 != null && Replay(new DungeonRun(preF10), parts) == parts[4]) f10Moved++;
            else { otherMoved.Add(line + " got " + h); if (differ.Count < 3) differ.Add(line + " got " + h); }
        }
        if (catalogLine && catalogLegacyPre != null)
            foreach (var r in parts[3] == "MAGE_ROGUE" && catalogLegacyPreF9 != null ? new[] { new DungeonRun(catalogLegacyPreF9), new DungeonRun(dataPreF9) }
                : new[] { new DungeonRun(catalogLegacyPre), new DungeonRun(dataPre) })
            {
                preN++;
                if (Replay(r, parts) == parts[4]) preOk++;
            }
    }
    Check("A4 100 legacy runs x 2 (pool data, catalog data; with and without schedules loaded) replay byte for byte, but for the Death Knight and Demon Hunter catalog runs (the phase-6 curated buckets), the MAGE_ROGUE catalog runs (phase 6 F9: Reno's own powers) and the catalog runs that replay the golden with the pre-F10 catalog (phase 6 F10: the new bucket members) that move",
        runN == 200 && otherMoved.Count == 0 && runOk + dkdhMoved + renoMoved + f10Moved == 200 && catalogLegacyPreF10 != null,
        runOk + "/" + runN + " (" + dkdhMoved + " DK/DH, " + renoMoved + " MAGE_ROGUE and " + f10Moved + " F10-catalog runs moved) " + string.Join(" | ", differ));
    Check("A4 with the pre-phase-6 catalog (and, for MAGE_ROGUE, the pre-F9 run data) every catalog-data legacy run replays the golden byte for byte (the curated buckets and Reno's powers are the only changes)",
        catalogLegacyPre != null && catalogLegacyPreF9 != null && preN > 0 && preOk == preN, preOk + "/" + preN + (catalogLegacyPre == null ? " (no " + preP6Catalog + ")" : ""));
}

// ---------------------------------------------------------------- scripted act runs
// One action: fights are won; a treasure is Offered[seed % n]; a whole-bundle draft takes Offered[Floor % n]; a two-card draft
// keeps cards of Offered[seed % n] (card (seed + kept) % left); a modifier is Offered[seed % n]; a tavern is left.
void Step(DungeonRun r, DungeonRunState s, DungeonRunProfile p, long seed, Func<DungeonRunState, DungeonRunOutcome> outcome = null)
{
    switch (s.Phase)
    {
        case DungeonRunPhase.PreMatch: r.BuildPlan(s); r.BeginMatch(s); break;
        case DungeonRunPhase.InMatch: r.RecordOutcome(s, p, outcome?.Invoke(s) ?? DungeonRunOutcome.Win); break;
        case DungeonRunPhase.TreasureDraft: r.PickTreasure(s, s.OpenDraft.Offered[(int)(seed % s.OpenDraft.Offered.Count)]); break;
        case DungeonRunPhase.BundleDraft:
        {
            var d = s.OpenDraft;
            if (d.Takes == 0) { r.PickBundle(s, d.Offered[s.Floor % d.Offered.Count]); break; }
            string bundle = d.Choosing ?? d.Offered[(int)(seed % d.Offered.Count)];
            var left = r.OfferCards(d, bundle);
            foreach (string k in d.Chosen) left.Remove(k);
            r.PickDraftCard(s, bundle, left[(int)((seed + d.Chosen.Count) % left.Count)]);
            break;
        }
        case DungeonRunPhase.Modifier: r.PickModifier(s, s.ModifierOffer.Offered[(int)(seed % s.ModifierOffer.Offered.Count)]); break;
        case DungeonRunPhase.Tavern: r.LeaveTavern(s); break;
        default: throw new InvalidOperationException("no step for " + s.Phase);
    }
}
DungeonRunState NewAct(DungeonRun r, DungeonRunProfile p, int k, long seed, string schedule = null)
{
    var cls = r.Data.Classes[k % r.Data.Classes.Count];
    return r.NewRun(p, cls.Id, cls.DefaultDeck, cls.DefaultPower, seed, schedule: schedule, signatureId: r.SignatureOffer(p, r.Data.FindClass(cls.Id), seed).FirstOrDefault());
}

// ---------------------------------------------------------------- A5 health and mana, A6 long seeded runs, A11 mirror
var dryClasses = new SortedSet<string>(StringComparer.Ordinal);
var skipProblems = new List<string>();
int skipReports = 0;
string LongRuns(out List<string> problems, out int victories)
{
    var log = new List<string>();
    var digest = new System.Text.StringBuilder();
    victories = 0;
    for (int k = 0; k < 50; k++)
    {
        long seed = 90001 + 7919L * k;
        var profile = new DungeonRunProfile();
        var s = NewAct(run, profile, k, seed);
        var save = new DungeonRunSave { Profile = profile, Active = s };
        int steps = 0;
        var sizes = new List<int>();
        var reported = new SortedSet<int>();
        while (!s.Finished && steps++ < 400)
        {
            if (s.Phase == DungeonRunPhase.PreMatch)
            {
                var node = run.CurrentNode(s);
                var act = acts.Acts[node.Act - 1];
                var boss = run.CurrentBoss(s);
                var mods = s.ActiveRules.Where(x => x.Act == node.Act).Select(x => data.Schedules.Modifier(x.Id)).ToList();
                int health = act.BossHealth[node.FloorInAct - 1] + run.HealthOffset(boss) + mods.Sum(m => m.BossHealthDelta);
                int mana = Math.Min(act.BossMana[node.FloorInAct - 1] + mods.Sum(m => m.BossManaDelta), 3);
                var plan = run.BuildPlan(s);
                if (plan.BossHealth != health || run.BossHealth(s) != health) log.Add($"{seed} F{s.Floor}: health {plan.BossHealth}, expected {health}");
                if (run.BossStartManaBonus(s) != mana) log.Add($"{seed} F{s.Floor}: mana {run.BossStartManaBonus(s)}, expected {mana}");
                if (run.PlayerHealthBonus(s) != mods.Sum(m => m.PlayerHealthDelta)) log.Add($"{seed} F{s.Floor}: player health bonus {run.PlayerHealthBonus(s)}");
                if (run.OpeningHandBonus(s) != mods.Sum(m => m.OpeningHandBonus)) log.Add($"{seed} F{s.Floor}: opening bonus {run.OpeningHandBonus(s)}");
                if (plan.PlayerDeck.Select(c => c.Id).SequenceEqual(s.Deck) == false) log.Add($"{seed} F{s.Floor}: plan deck differs");
                if (boss.Id != s.BossSequence[s.Floor - 1] || node.Floor != s.Floor) log.Add($"{seed} F{s.Floor}: node/boss mismatch");
            }
            if (s.Phase == DungeonRunPhase.Tavern)
            {
                var act = acts.Acts[run.CurrentNode(s).Act - 1];
                // ULDA_011 VIP Membership (phase 6 F13) adds VipExtraGold while the run holds it (first picked by one of these runs since F15).
                int gold = act.TavernGold + s.ActiveRules.Where(x => x.Act == run.CurrentNode(s).Act).Sum(x => data.Schedules.Modifier(x.Id).TavernGoldDelta)
                    + (DungeonRun.HasVip(s) ? DungeonRun.VipExtraGold : 0);
                if (s.Tavern == null || s.Tavern.Gold != gold || s.Tavern.Node != s.Node) log.Add($"{seed} node {s.Node}: tavern gold {s.Tavern?.Gold}, expected {gold}");
            }
            if (s.Phase == DungeonRunPhase.BundleDraft && s.OpenDraft.Chosen.Count == 0)
            {
                var node = run.CurrentNode(s);
                int options = Math.Max(1, Math.Min(3, acts.Acts[node.Act - 1].DraftOptions + s.ActiveRules.Where(x => x.Act == node.Act).Sum(x => data.Schedules.Modifier(x.Id).DraftOptionsDelta)));
                if (s.OpenDraft.Offered.Count > options) log.Add($"{seed} F{s.Floor}: {s.OpenDraft.Offered.Count} offers, at most {options}");
                if (s.OpenDraft.Takes != (node.Act == 1 ? 0 : 2)) log.Add($"{seed} F{s.Floor}: takes {s.OpenDraft.Takes}");
            }
            Step(run, s, profile, seed);
            if (run.SkippedDraftFloor(s) is int skip && skip > 0) reported.Add(skip);
            if (s.Phase == DungeonRunPhase.BundleDraft && s.OpenDraft.Chosen.Count == 0 && run.DraftBand(s) != Math.Min(3, run.CurrentNode(s).Act))
                log.Add($"{seed} F{s.Floor}: draft band {run.DraftBand(s)}, act {run.CurrentNode(s).Act}");
            string mirror = DungeonRunDeck.MirrorError(s);
            if (mirror != null || !s.Deck.SequenceEqual(s.Cards.Select(c => c.Id))) log.Add($"{seed} step {steps}: {mirror ?? "mirror differs"}");
            if (s.Deck.Count < acts.DeckMin || s.Deck.Count > acts.DeckMax) log.Add($"{seed} step {steps}: deck {s.Deck.Count} outside 10..50");
            foreach (var up in s.Upgrades)
                if (DungeonRunDeck.Find(s, up.Uid)?.Id != SabberStoneCore.CardSets.DungeonRun.DungeonRunContent.Get(up.Key).ShellId) log.Add($"{seed}: upgrade {up.Key} uid {up.Uid} is not its shell");
            sizes.Add(s.Deck.Count);
            digest.Append(Hash(Xml(save)));
        }
        if (s.Phase == DungeonRunPhase.Victory) victories++;
        var stats = profile.Heroes.SingleOrDefault();
        if (s.Phase != DungeonRunPhase.Victory || s.Floor != 15 || s.MajorsCleared != 3 || stats?.BestFloor != 15 || stats.MajorsCleared != 3
            || profile.MajorsCleared != 3 || stats.Wins != 1)
            log.Add($"{seed}: ended {s.Phase} floor {s.Floor} majors {s.MajorsCleared} best {stats?.BestFloor} stats majors {stats?.MajorsCleared}");
        // 14 drafts (one after every won fight but the last), unless the class's catalog runs dry: a class with no pool entry
        // has at most as many drafts as its fixed bundles. Since phase 6 F16 every class has pool entries (Death Knight and
        // Demon Hunter through the SoloStone-curated buckets; A12 walks them), so dryClasses must stay empty.
        var classes = run.RunClasses(s);
        bool secret = DungeonRunAdmission.MechanicHolds(data, classes, "secret");
        var eligible = data.Bundles.Where(b => b.EligibleFor(classes, m => m == "secret" && secret)).ToList();
        int expectDrafts = eligible.Any(b => b.IsPool) ? 14 : Math.Min(14, eligible.Count);
        // Phase 7 T1: a Rikkar run with a board shrine takes up to four teammates (deck records of origin "teammate"); once they
        // and the drafts fill the deck to deckMax, a later floor's draft is skipped for want of room (and reported, below).
        int mates = s.Cards.Count(c => c.Origin == DungeonRunDeck.OriginTeammate);
        var roomless = new SortedSet<int>(s.Matches.Where(m => m.Outcome == DungeonRunOutcome.Win && m.Floor < 15 && !s.Drafts.Any(d => d.Kind == "bundle" && d.Floor == m.Floor)).Select(m => m.Floor));
        if (mates > 0 && roomless.Count > 0 && s.Deck.Count + 2 > acts.DeckMax) expectDrafts -= roomless.Count;
        if (!eligible.Any(b => b.IsPool)) dryClasses.Add(s.Class + " " + s.Drafts.Count(d => d.Kind == "bundle"));
        // Every won floor but the last with no bundle draft was reported by SkippedDraftFloor on the page after it.
        var skippedFloors = new SortedSet<int>(s.Matches.Where(m => m.Outcome == DungeonRunOutcome.Win && m.Floor < 15 && !s.Drafts.Any(d => d.Kind == "bundle" && d.Floor == m.Floor)).Select(m => m.Floor));
        if (!skippedFloors.SetEquals(reported)) skipProblems.Add($"{seed} {s.Class}: skipped [{string.Join(",", skippedFloors)}] reported [{string.Join(",", reported)}]");
        skipReports += reported.Count;
        if (s.Drafts.Count(d => d.Kind == "bundle") != expectDrafts) log.Add($"{seed}: {s.Drafts.Count(d => d.Kind == "bundle")} bundle drafts, expected {expectDrafts}");
        int drafted = s.Drafts.Where(d => d.Kind == "bundle").Sum(d => d.Floor <= 5 ? 3 : 2);
        if (s.Cards.Count(c => c.Origin == DungeonRunDeck.OriginStarter) != 10 || s.Cards.Count(c => c.Origin == DungeonRunDeck.OriginDraft) != drafted
            || expectDrafts == 14 && drafted != 15 + 10 + 8 || mates > 4)
            log.Add($"{seed}: starter {s.Cards.Count(c => c.Origin == "starter")}, draft {s.Cards.Count(c => c.Origin == "draft")} (expected 10, {drafted})");
        // Since F11 a Tombs run's picked signature is an active treasure too, on a card record of origin signature.
        int treasureRecords = s.Cards.Count(c => c.Origin == DungeonRunDeck.OriginTreasure || c.Origin == DungeonRunDeck.OriginSignature);
        if (treasureRecords != s.ActiveTreasures.Count) log.Add($"{seed}: treasure records {treasureRecords} vs {s.ActiveTreasures.Count} actives");
        if (s.Cards.Select(c => c.Uid).SequenceEqual(Enumerable.Range(1, s.Cards.Count)) == false || s.NextUid != s.Cards.Count) log.Add($"{seed}: uids not 1..n");
        if (s.Tavern != null || s.ModifierOffer != null) log.Add($"{seed}: an open node survived the run");
        digest.Append('|').Append(string.Join(",", sizes.Distinct().Take(3)));
    }
    problems = log;
    return Hash(digest.ToString());
}
{
    string d1 = LongRuns(out var p1, out int v1), d2 = LongRuns(out var p2, out int v2);
    Check("A6 50 scripted all-win act runs reach Victory on floor 15 with MajorsCleared 3 and BestFloor 15", v1 == 50 && p1.Count(x => x.Contains("ended")) == 0,
        v1 + " victories; " + string.Join(" | ", p1.Where(x => x.Contains("ended")).Take(3)));
    Check("A5 every fight's plan: health = act curve + the boss's own Health offset + bossHealthDelta; mana bonus = curve + bossManaDelta capped at 3; player/opening bonuses from the rules",
        !p1.Any(x => x.Contains("health") || x.Contains("mana") || x.Contains("bonus") || x.Contains("plan deck") || x.Contains("node/boss")),
        string.Join(" | ", p1.Where(x => x.Contains("health") || x.Contains("mana") || x.Contains("bonus")).Take(4)));
    Check("A6 deck size stays within 10..50 at every node", !p1.Any(x => x.Contains("outside 10..50")), string.Join(" | ", p1.Where(x => x.Contains("outside")).Take(3)));
    Check("A6 14 bundle drafts per run; 10 starter + 33 drafted card records; treasure records match actives; uids 1..n",
        !p1.Any(x => x.Contains("bundle drafts") || x.Contains("starter") || x.Contains("treasure records") || x.Contains("uids")),
        string.Join(" | ", p1.Where(x => x.Contains("drafts") || x.Contains("starter") || x.Contains("records") || x.Contains("uids")).Take(4)));
    Check("A6 tavern gold = act gold + tavernGoldDelta; draft offers within the act's options; takes 3 in act I, 2 after",
        !p1.Any(x => x.Contains("tavern gold") || x.Contains("offers, at most") || x.Contains("takes")), string.Join(" | ", p1.Where(x => x.Contains("tavern") || x.Contains("takes") || x.Contains("offers")).Take(4)));
    Check("A6 the digest of every state is stable across two passes", d1 == d2 && p1.SequenceEqual(p2), d1 + " vs " + d2);
    Check("A11 Deck == Cards ids after every step of the 50 runs; upgrades track their shell by uid", !p1.Any(x => x.Contains("mirror") || x.Contains("upgrade") || x.Contains("open node")),
        string.Join(" | ", p1.Where(x => x.Contains("mirror") || x.Contains("upgrade") || x.Contains("open node")).Take(3)));
    Console.WriteLine("A6 digest " + d1);
    // A tracked gap, not an expected value: a class with no catalog pool entry runs out of drafts before act III ends
    // (phase 9 economy/long-run work). The run now says so on the page after a skipped draft.
    Console.WriteLine("NOTE A6 classes whose catalog runs dry before the last draft (none since phase 6 F16): " + string.Join(", ", dryClasses));
    Check("A6 no class's catalog runs dry before the last draft (phase 6 F16)", dryClasses.Count == 0, string.Join(", ", dryClasses));
    Check("A6 every skipped draft (pool dry or deck full) is reported by SkippedDraftFloor on the next page, and nothing else is",
        skipProblems.Count == 0, skipReports + " reports; " + string.Join(" | ", skipProblems.Take(3)));
    Check("A6 an act run's draft band is the act of the fight just won (1, 2, 3)", !p1.Any(x => x.Contains("draft band")), string.Join(" | ", p1.Where(x => x.Contains("draft band")).Take(3)));
}
{
    // A5 on synthetic boss rows: fixedHealth uses the boss's own Health; bossManaCap clamps the bonus.
    var fixedData = Load();
    var fr = new DungeonRun(fixedData);
    var s = NewAct(fr, all, 0, 31337);
    // Walk to the act I major (floor 5): curve bonus 1 there.
    while (s.Floor < 5 || s.Phase != DungeonRunPhase.PreMatch) Step(fr, s, all, 31337);
    var major = fr.CurrentBoss(s);
    Check("A5 act I major: health 40 and start-mana bonus 1 from the curve", fr.BossHealth(s) == 40 && fr.BossStartManaBonus(s) == 1, fr.BossHealth(s) + "/" + fr.BossStartManaBonus(s));
    int? ownHealth = major.Health;
    major.FixedHealth = true; major.Health = 77; major.BossManaCap = 0;
    Check("A5 a fixedHealth boss uses its own Health; bossManaCap 0 clamps the bonus to 0", fr.BossHealth(s) == 77 && fr.BuildPlan(s).BossHealth == 77 && fr.BossStartManaBonus(s) == 0);
    major.FixedHealth = false;
    int bandDefault = major.Final ? fixedData.Rules.FinalHealth : fixedData.Rules.BandHealth[major.Band - 1];
    Check("A5 without fixedHealth a row's own Health is an offset to the curve (77 against its band's default)",
        fr.BossHealth(s) == 40 + 77 - bandDefault && fr.HealthOffset(major) == 77 - bandDefault, fr.BossHealth(s) + " band default " + bandDefault);
    major.Health = ownHealth;
    Check("A5 a row without its own Health follows the curve", fr.BossHealth(s) == 40 + fr.HealthOffset(major) && (ownHealth != null || fr.BossHealth(s) == 40));
    s.ActiveRules.Add(new DungeonRunRule { Id = "mod_iron_bosses", Act = 1, FromNode = 0 });
    s.ActiveRules.Add(new DungeonRunRule { Id = "mod_hot_start", Act = 1, FromNode = 0 });
    major.BossManaCap = null;
    Check("A5 Iron Bosses +5 Health, Hot Start +1 mana and +10 player Health", fr.BossHealth(s) == 45 + fr.HealthOffset(major) && fr.BossStartManaBonus(s) == 2 && fr.PlayerHealthBonus(s) == 10);
    s.ActiveRules.Add(new DungeonRunRule { Id = "mod_hot_start", Act = 2, FromNode = 6 });
    Check("A5 a rule of another act does not apply", fr.PlayerHealthBonus(s) == 10);
    s.HealthBonus = 5;
    Check("A5 the run's HealthBonus adds to the player bonus", fr.PlayerHealthBonus(s) == 15);
    // The legacy accessors are zero / the old values.
    var ls = run.NewRun(fresh, "MAGE", "mage_rimeward", "CS2_034", 77, schedule: DungeonRunSchedules.LegacyId, signatureId: run.SignatureOffer(fresh, run.Data.FindClass("MAGE"), 77).FirstOrDefault());
    Check("A5 legacy run: band health, no mana, health or opening bonus, deckMax 40", run.BossHealth(ls) == run.BossHealth(run.CurrentBoss(ls)) && run.BossStartManaBonus(ls) == 0
        && run.PlayerHealthBonus(ls) == 0 && run.OpeningHandBonus(ls) == 0 && run.DeckMax(ls) == 40 && run.DraftOptions(ls) == 3 && run.DraftTakes(ls) == 3);
    // Mana curve clamps at the default cap 3: act III floor 5 bonus 3, + Hot Start 1 = 4 -> 3.
    var cap = NewAct(fr, fresh, 2, 4711);
    while (!(cap.Floor == 15 && cap.Phase == DungeonRunPhase.PreMatch)) Step(fr, cap, fresh, 4711);
    cap.ActiveRules.Add(new DungeonRunRule { Id = "mod_hot_start", Act = 3, FromNode = cap.Node });
    Check("A5 the final's bonus 3 + Hot Start 1 clamps to the default cap 3", fr.BossStartManaBonus(cap) == 3 && fr.CurrentAct(cap).BossMana[4] == 3);
}

// ---------------------------------------------------------------- modifiers
{
    int offered = 0, distinct = 0, incompat = 0, deterministic = 0;
    var seen = new HashSet<string>();
    var md = Load();
    var mr = new DungeonRun(md);
    for (int k = 0; k < 60; k++)
    {
        long seed = 51000 + 13L * k;
        var s = NewAct(run, fresh, k, seed);
        while (s.Phase != DungeonRunPhase.Modifier) Step(run, s, fresh, seed);
        offered++;
        if (s.ModifierOffer.Offered.Count == 2 && s.ModifierOffer.Offered.Distinct().Count() == 2 && s.ModifierOffer.Node == s.Node) distinct++;
        seen.UnionWith(s.ModifierOffer.Offered);
        var t = NewAct(run, fresh, k, seed);
        while (t.Phase != DungeonRunPhase.Modifier) Step(run, t, fresh, seed);
        if (t.ModifierOffer.Offered.SequenceEqual(s.ModifierOffer.Offered)) deterministic++;
        // Every act-II boss lists the first offered modifier as incompatible: it is never offered.
        string banned = s.ModifierOffer.Offered[0];
        foreach (var b in md.Bosses) b.IncompatibleModifiers = b.Pools.Contains("band2") || b.Pools.Contains("band3") ? new List<string> { banned } : new List<string>();
        var u = NewAct(mr, fresh, k, seed);
        while (u.Phase != DungeonRunPhase.Modifier) Step(mr, u, fresh, seed);
        if (!u.ModifierOffer.Offered.Contains(banned) && u.ModifierOffer.Offered.Count == 2) incompat++;
    }
    Check("A5 modifier nodes offer 2 distinct modifiers, deterministically", offered == 60 && distinct == 60 && deterministic == 60, $"{distinct}/{deterministic}/{offered}");
    Check("A5 all 4 modifiers are offered across seeds", seen.Count == 4, string.Join(",", seen));
    Check("A5 a modifier an act boss lists as incompatible is never offered on that act", incompat == 60, incompat + "/60");
    var s2 = NewAct(run, fresh, 3, 777);
    while (s2.Phase != DungeonRunPhase.Modifier) Step(run, s2, fresh, 777);
    Throws<InvalidOperationException>("A5 a modifier not on offer is refused", () => run.PickModifier(s2, data.Schedules.Modifiers.Select(m => m.Id).First(id => !s2.ModifierOffer.Offered.Contains(id))));
    string pick = s2.ModifierOffer.Offered[1];
    int node = s2.Node;
    run.PickModifier(s2, pick);
    Check("A5 a picked modifier is an active rule of the act from its node; the offer closes and the run moves on",
        s2.ActiveRules.Single().Id == pick && s2.ActiveRules[0].Act == 2 && s2.ActiveRules[0].FromNode == node && s2.ModifierOffer == null && s2.Node == node + 1
        && s2.Phase == DungeonRunPhase.PreMatch && s2.Floor == 6);
    // Lean Supply: one bundle fewer on that act's drafts; Iron Bosses: a treasure draft after the major.
    var lean = NewAct(run, fresh, 4, 1234);
    while (lean.Phase != DungeonRunPhase.Modifier) Step(run, lean, fresh, 1234);
    lean.ModifierOffer.Offered = new List<string> { "mod_lean_supply", "mod_iron_bosses" };
    run.PickModifier(lean, "mod_lean_supply");
    while (!(lean.Phase == DungeonRunPhase.BundleDraft && lean.OpenDraft.Chosen.Count == 0)) Step(run, lean, fresh, 1234);
    Check("A5 Lean Supply: act II drafts offer 2 bundles", run.DraftOptions(lean) == 2 && lean.OpenDraft.Offered.Count == 2, lean.OpenDraft.Offered.Count.ToString());
    var iron = NewAct(run, fresh, 5, 4321);
    while (iron.Phase != DungeonRunPhase.Modifier) Step(run, iron, fresh, 4321);
    iron.ModifierOffer.Offered = new List<string> { "mod_iron_bosses", "mod_lean_supply" };
    run.PickModifier(iron, "mod_iron_bosses");
    while (!(iron.Floor == 10 && iron.Phase == DungeonRunPhase.InMatch)) Step(run, iron, fresh, 4321);
    run.RecordOutcome(iron, fresh, DungeonRunOutcome.Win);
    Check("A5 Iron Bosses: the act II major opens a treasure draft (no treasure flag there)", iron.Phase == DungeonRunPhase.TreasureDraft && iron.OpenDraft.Floor == 10 && iron.MajorsCleared == 2);
    var noIron = NewAct(run, fresh, 5, 4321);
    while (noIron.Phase != DungeonRunPhase.Modifier) Step(run, noIron, fresh, 4321);
    noIron.ModifierOffer.Offered = new List<string> { "mod_lean_supply", "mod_iron_bosses" };
    run.PickModifier(noIron, "mod_lean_supply");
    while (!(noIron.Floor == 10 && noIron.Phase == DungeonRunPhase.InMatch)) Step(run, noIron, fresh, 4321);
    run.RecordOutcome(noIron, fresh, DungeonRunOutcome.Win);
    Check("A5 without it, the act II major goes straight to the bundle draft", noIron.Phase == DungeonRunPhase.BundleDraft);
}

// ---------------------------------------------------------------- A7 drafts
{
    var s = NewAct(run, fresh, 6, 8080);
    bool actOne = true, actTwo = true;
    var notes = new List<string>();
    int twoTakeDrafts = 0;
    while (!s.Finished)
    {
        if (s.Phase == DungeonRunPhase.BundleDraft && s.OpenDraft.Chosen.Count == 0)
        {
            var d = s.OpenDraft;
            int before = s.Deck.Count;
            var node = run.CurrentNode(s);
            if (node.Act == 1)
            {
                Step(run, s, fresh, 8080);
                if (d.Takes != 0 || s.Deck.Count != before + 3 || d.Picked == null) { actOne = false; notes.Add("act I F" + d.Floor); }
                continue;
            }
            twoTakeDrafts++;
            Throws<InvalidOperationException>("A7 a whole-bundle pick is refused on a two-card draft (F" + d.Floor + ")", () => run.PickBundle(s, d.Offered[0]), "one at a time");
            string bundle = d.Offered[0];
            var cards = run.OfferCards(d, bundle);
            run.PickDraftCard(s, bundle, cards[0]);
            bool partial = d.Picked == null && d.Choosing == bundle && d.Chosen.SequenceEqual(new[] { cards[0] }) && s.Deck.Count == before && s.Phase == DungeonRunPhase.BundleDraft;
            if (d.Offered.Count > 1)
                Throws<InvalidOperationException>("A7 the second card must come from the same bundle (F" + d.Floor + ")", () => run.PickDraftCard(s, d.Offered[1], run.OfferCards(d, d.Offered[1])[0]), "being kept");
            // Clear, then keep cards 2 and 3 instead.
            run.ClearDraftCards(s);
            bool cleared = d.Choosing == null && d.Chosen.Count == 0 && s.Deck.Count == before;
            run.PickDraftCard(s, bundle, cards[1]);
            string text = Xml(new DungeonRunSave { Active = s });
            bool stored = text.Contains("Takes=\"2\"") && text.Contains("Choosing=\"" + bundle + "\"");
            run.PickDraftCard(s, bundle, cards[2]);
            bool grew = s.Deck.Count == before + 2 && s.Deck.Skip(before).SequenceEqual(new[] { cards[1], cards[2] }) && d.Picked == bundle && d.Takes == 2
                && s.Cards.Skip(before).All(c => c.Origin == DungeonRunDeck.OriginDraft && c.AddedFloor == d.Floor);
            bool third = false;
            try { run.PickDraftCard(s, bundle, cards[0]); } catch (InvalidOperationException) { third = true; }
            if (!(partial && cleared && stored && grew && third)) { actTwo = false; notes.Add($"F{d.Floor}: partial {partial} cleared {cleared} stored {stored} grew {grew} third {third}"); }
            continue;
        }
        Step(run, s, fresh, 8080);
    }
    Check("A7 act I drafts take a whole bundle: +3 cards, no Takes attribute", actOne, string.Join(" | ", notes));
    Check("A7 acts II-III drafts keep 2 of 3: +2 cards, stored Takes=2 and the partial pick, a 3rd pick refused", actTwo && twoTakeDrafts == 9, twoTakeDrafts + " drafts; " + string.Join(" | ", notes));
    Check("A7 the run still ends in Victory", s.Phase == DungeonRunPhase.Victory);
    // An act run without the catalog drafts the fixed bundles with the same takes.
    var nc = new DungeonRun(Load(null, catalog: false));
    var t = NewAct(nc, fresh, 1, 6060);
    while (!(t.Phase == DungeonRunPhase.BundleDraft && t.OpenDraft.Takes == 2)) Step(nc, t, fresh, 6060);
    int size = t.Deck.Count;
    string fixedBundle = t.OpenDraft.Offered[0];
    var fixedCards = nc.Data.LegacyBundles.First(b => b.Id == fixedBundle).Cards;
    nc.PickDraftCard(t, fixedBundle, fixedCards[2]); nc.PickDraftCard(t, fixedBundle, fixedCards[0]);
    Check("A7 a fixed-bundle act draft (no catalog) keeps 2 of its 3 cards", t.Deck.Count == size + 2 && t.Deck.Skip(size).SequenceEqual(new[] { fixedCards[2], fixedCards[0] }) && t.TakenBundles.Contains(fixedBundle));
}

// ---------------------------------------------------------------- A8 ladder
{
    var p = new DungeonRunProfile();
    var s = NewAct(run, p, 0, 60606);
    string cls = s.Class.ToLowerInvariant();
    while (!s.Finished) Step(run, s, p, 60606, st => st.Floor == 6 ? DungeonRunOutcome.Loss : DungeonRunOutcome.Win);
    Check("A8 an act run lost at the F6 combat: Defeat on floor 6 after one major", s.Phase == DungeonRunPhase.Defeat && s.Floor == 6 && s.MajorsCleared == 1);
    Check("A8 it unlocks floor3/4/5 and win (majorsCleared 1), and nothing of another class",
        p.Unlocked.OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(new[] { cls + "_floor3", cls + "_floor4", cls + "_floor5", cls + "_win" }.OrderBy(x => x, StringComparer.Ordinal)),
        string.Join(",", p.Unlocked));
    Check("A8 stats: one run, no win, best floor 6, majors 1 (class and profile)", p.Heroes.Single().Runs == 1 && p.Heroes.Single().Wins == 0 && p.Heroes.Single().BestFloor == 6
        && p.Heroes.Single().MajorsCleared == 1 && p.MajorsCleared == 1);
    var q = new DungeonRunProfile();
    var e = NewAct(run, q, 0, 60607);
    while (!e.Finished) Step(run, e, q, 60607, st => st.Floor == 5 ? DungeonRunOutcome.Loss : DungeonRunOutcome.Win);
    Check("A8 lost at the act I major: floor3/4/5 only (no major beaten, no win)", e.MajorsCleared == 0
        && q.Unlocked.OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(new[] { cls + "_floor3", cls + "_floor4", cls + "_floor5" }), string.Join(",", q.Unlocked));
    var typed = new DungeonRunProfile();
    var ty = run.NewRun(typed, s.Class, data.Class(s.Class).DefaultDeck, data.Class(s.Class).DefaultPower, 60606, typedSeed: true, signatureId: run.SignatureOffer(typed, run.Data.FindClass(s.Class), 60606).FirstOrDefault());
    while (!ty.Finished) Step(run, ty, typed, 60606, st => st.Floor == 6 ? DungeonRunOutcome.Loss : DungeonRunOutcome.Win);
    Check("A8 a typed-seed act run feeds no stats, majors or unlocks", typed.Unlocked.Count == 0 && typed.Heroes.All(h => h.Runs == 0 && h.BestFloor == 0 && h.MajorsCleared == 0)
        && typed.MajorsCleared == 0 && ty.MajorsCleared == 1);
    var lp = new DungeonRunProfile();
    var ls = run.NewRun(lp, s.Class, data.Class(s.Class).DefaultDeck, data.Class(s.Class).DefaultPower, 60606, schedule: DungeonRunSchedules.LegacyId, signatureId: run.SignatureOffer(lp, run.Data.FindClass(s.Class), 60606).FirstOrDefault());
    while (!ls.Finished)
    {
        if (ls.Phase == DungeonRunPhase.PreMatch) { run.BeginMatch(ls); run.RecordOutcome(ls, lp, ls.Floor == 6 ? DungeonRunOutcome.Loss : DungeonRunOutcome.Win); }
        else if (ls.Phase == DungeonRunPhase.TreasureDraft) run.PickTreasure(ls, ls.OpenDraft.Offered[0]);
        else run.PickBundle(ls, ls.OpenDraft.Offered[0]);
    }
    Check("A8 a legacy run lost on floor 6: floor3/4/5, no win (unchanged ladder)", lp.Heroes.Single().MajorsCleared == 0
        && lp.Unlocked.OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(new[] { cls + "_floor3", cls + "_floor4", cls + "_floor5" }), string.Join(",", lp.Unlocked));
    Check("A8 the win step is runsWon 1 OR majorsCleared 1, scoped to the class on both branches",
        data.Unlocks.TryGetValue("mage_win", out var w) && w.Class == "MAGE" && w.AnyOf?.Count == 2 && w.AnyOf[0].RunsWon == 1 && w.AnyOf[1].MajorsCleared == 1 && w.AnyOf.All(c => c.Class == "MAGE"));
    var other = new DungeonRunProfile(); other.StatsFor("WARRIOR").MajorsCleared = 1;
    Check("A8 another class's major does not unlock this class's win", !run.Holds(data.Unlocks["mage_win"], other) && run.Holds(data.Unlocks["warrior_win"], other));
    Check("A8 the class page hint of win stays \"Win a run with Mage to unlock\"", DungeonRunCatalog.Hint(run, "mage_win") == "Win a run with Mage to unlock", DungeonRunCatalog.Hint(run, "mage_win"));
}

// ---------------------------------------------------------------- A9 save/resume mid-node
{
    const long seed = 24680;
    const int k = 2;
    // The unbroken run: its final save, and the step at which each phase first occurs.
    string Final(Func<DungeonRunState, DungeonRunProfile, int, (DungeonRunState, DungeonRunProfile)> interrupt, out List<string> notes)
    {
        notes = new List<string>();
        var p = new DungeonRunProfile();
        var s = NewAct(run, p, k, seed);
        int step = 0;
        while (!s.Finished)
        {
            (s, p) = interrupt(s, p, step);
            if (s.Finished) break;
            Step(run, s, p, seed);
            step++;
        }
        return Xml(new DungeonRunSave { Profile = p, Active = s });
    }
    string unbroken = Final((s, p, _) => (s, p), out _);
    var stops = new List<(string Name, Func<DungeonRunState, bool> At)>
    {
        ("PreMatch", s => s.Phase == DungeonRunPhase.PreMatch && s.Floor == 4),
        ("InMatch", s => s.Phase == DungeonRunPhase.InMatch && s.Floor == 7),
        ("TreasureDraft", s => s.Phase == DungeonRunPhase.TreasureDraft && s.Floor == 6),
        ("BundleDraft 1 of 2", s => s.Phase == DungeonRunPhase.BundleDraft && s.OpenDraft.Takes == 2 && s.OpenDraft.Chosen.Count == 1),
        ("Modifier", s => s.Phase == DungeonRunPhase.Modifier && s.Floor == 11),
        ("Tavern", s => s.Phase == DungeonRunPhase.Tavern && s.Floor == 8),
        ("Tavern with Pending", s => s.Phase == DungeonRunPhase.Tavern && s.Floor == 13),
    };
    foreach (var (name, at) in stops)
    {
        bool done = false, identical = false, migrated = false, restaged = true, pendingKept = true;
        string dir = TempDir();
        string resumed = Final((s, p, step) =>
        {
            if (done || !at(s)) return (s, p);
            done = true;
            bool pending = name.EndsWith("Pending");
            if (pending) s.Tavern.Pending = new DungeonRunTavernPending { Option = "DALA_908", Candidates = s.Cards.Take(4).Select(c => c.Uid.ToString()).ToList() };
            var save = new DungeonRunSave { Profile = p, Active = s };
            string before = Xml(save);
            DungeonRunStore.Save(dir, save);
            var loaded = DungeonRunStore.Load(dir);
            identical = Xml(loaded) == before;
            migrated = !DungeonRunMigration.Upgrade(loaded, run, null);
            int attempt = loaded.Active.Attempt, match = run.MatchSeed(loaded.Active);
            run.Resume(loaded.Active);
            if (name == "InMatch")
                restaged = loaded.Active.Phase == DungeonRunPhase.PreMatch && run.MatchSeed(loaded.Active) == match && attempt == 1;
            if (pending)
            {
                pendingKept = loaded.Active.Tavern.Pending?.Option == "DALA_908" && loaded.Active.Tavern.Pending.Candidates.Count == 4;
                try { run.LeaveTavern(loaded.Active); pendingKept = false; } catch (InvalidOperationException) { }
                loaded.Active.Tavern.Pending = null;
            }
            return (loaded.Active, loaded.Profile);
        }, out _);
        // A restaged fight is staged again: its summary records attempt 2 instead of 1, and nothing else moves.
        string expect = name == "InMatch" ? null : unbroken;
        bool same = expect != null ? resumed == expect
            : System.Text.RegularExpressions.Regex.Replace(resumed, " Attempt=\"\\d+\"", "") == System.Text.RegularExpressions.Regex.Replace(unbroken, " Attempt=\"\\d+\"", "")
              && resumed.Contains("Floor=\"7\" Boss=") && resumed != unbroken;
        Check($"A9 save/reload at {name}: byte-identical, no migration, resumes to the unbroken run's final state"
            + (name == "InMatch" ? " (the fight restaged: attempt 2)" : ""), done && identical && migrated && same && restaged && pendingKept,
            $"reached {done} identical {identical} migrated-none {migrated} same {same} restaged {restaged} pending {pendingKept}");
    }
}

// ---------------------------------------------------------------- A10 migration
{
    var log = new List<string>();
    int v1 = 0, v1n = 0;
    foreach (string fixture in Directory.GetDirectories(fixtures).OrderBy(x => x, StringComparer.Ordinal))
    {
        if (!File.Exists(Path.Combine(fixture, DungeonRunStore.FileName))) continue;
        v1n++;
        string dir = TempDir();
        File.Copy(Path.Combine(fixture, DungeonRunStore.FileName), Path.Combine(dir, DungeonRunStore.FileName), true);
        var save = DungeonRunStore.Load(dir);
        int was = save.Version;
        DungeonRunMigration.Upgrade(save, new DungeonRun(legacyData), log.Add);
        if (was == 1 && save.Version == DungeonRunSave.CurrentVersion && (save.Active == null || save.Active.ScheduleVersion == 0 && save.Active.Cards.Count == 0 && !save.Active.IsActRun)
            && !Xml(save).Contains("ScheduleVersion") && !Xml(save).Contains("<Cards")) v1++;
    }
    Check("A10 every version-1 fixture upgrades to the current version (5, phase 7): ScheduleVersion 0, no card records", v1n > 0 && v1 == v1n, v1 + "/" + v1n);
    // Version 2: a legacy run with an open legacy draft, saved as a version-2 plugin wrote it.
    {
        var lr = new DungeonRun(legacyData);
        var s = lr.NewRun(all, "PALADIN", "paladin_lantern", legacyData.Class("PALADIN").DefaultPower, 5150, signatureId: lr.SignatureOffer(all, lr.Data.FindClass("PALADIN"), 5150).FirstOrDefault());
        while (s.Phase != DungeonRunPhase.BundleDraft) { if (s.Phase == DungeonRunPhase.PreMatch) { lr.BeginMatch(s); lr.RecordOutcome(s, new DungeonRunProfile(), DungeonRunOutcome.Win); } else lr.PickTreasure(s, s.OpenDraft.Offered[0]); }
        var v2 = new DungeonRunSave { Version = 2, Active = s };
        string text = Xml(v2);
        var copy = DungeonRunStore.Clone(v2);
        bool up = DungeonRunMigration.Upgrade(copy, lr, log.Add);
        Check("A10 a version-2 save upgrades to 4 in value-free steps (body unchanged but for the root version)",
            up && copy.Version == DungeonRunSave.CurrentVersion && copy.Active.ScheduleVersion == 0 && Xml(copy).Replace("Version=\"6\">", "Version=\"2\">") == text && log.Any(l => l.Contains("from version 2 to 6")));
    }
    // Version 3: files the pre-change plugin wrote (legacy and catalog runs, mid-run and finished).
    int v3 = 0, v3n = 0;
    var notes = new List<string>();
    foreach (string file in Directory.GetFiles(golden, "v3-*.xml").OrderBy(x => x, StringComparer.Ordinal))
    {
        v3n++;
        string original = VerifyPaths.ReadText(file);
        string dir = TempDir();
        // The golden files are XmlSerializer output through a StringWriter (a utf-16 declaration); stored as the store writes, UTF-8.
        var parsed = (DungeonRunSave)ser.Deserialize(new StringReader(original));
        parsed.LoadedVersion = 3;
        Check("A10 " + Path.GetFileName(file) + " is a version-3 save", parsed.Version == 3);
        using (var fs = new FileStream(Path.Combine(dir, DungeonRunStore.FileName), FileMode.Create)) ser.Serialize(fs, parsed);
        byte[] onDisk = File.ReadAllBytes(Path.Combine(dir, DungeonRunStore.FileName));
        var save = DungeonRunStore.Load(dir);
        bool catalogRun = save.Active.CatalogVersion == 1;
        bool up = DungeonRunMigration.Upgrade(save, run, log.Add);
        bool kept = save.Active.CatalogVersion == (file.Contains("catalog") ? 1 : 0) && catalogRun == file.Contains("catalog");
        bool body = Xml(save) == original.Replace(" Version=\"3\">", " Version=\"6\">");
        DungeonRunStore.Save(dir, save);
        string backup = Path.Combine(dir, DungeonRunStore.BackupName(3));
        bool backed = File.Exists(backup) && File.ReadAllBytes(backup).SequenceEqual(onDisk);
        var reloaded = DungeonRunStore.Load(dir);
        bool round = reloaded.Version == DungeonRunSave.CurrentVersion && Xml(reloaded) == Xml(save) && !DungeonRunMigration.Upgrade(reloaded, run, log.Add);
        if (up && kept && body && backed && round && save.Version == DungeonRunSave.CurrentVersion && save.Active.ScheduleVersion == 0) v3++;
        else notes.Add($"{Path.GetFileName(file)}: up {up} kept {kept} body {body} backed {backed} round {round}");
    }
    Check("A10 version-3 files: upgraded to 4, ScheduleVersion 0, CatalogVersion kept, body bytes equal the v3 output, a .v3.xml backup, idempotent",
        v3n == 12 && v3 == v3n, v3 + "/" + v3n + " " + string.Join(" | ", notes));
    // A version-4 act run round-trips; a version-5 file is refused.
    {
        string dir = TempDir();
        var p = new DungeonRunProfile();
        var s = NewAct(run, p, 7, 13579);
        while (!(s.Phase == DungeonRunPhase.Tavern)) Step(run, s, p, 13579);
        var save = new DungeonRunSave { Profile = p, Active = s };
        DungeonRunStore.Save(dir, save);
        string text = File.ReadAllText(Path.Combine(dir, DungeonRunStore.FileName));
        var back = DungeonRunStore.Load(dir);
        Check("A10 a version-6 act run round-trips (ScheduleVersion, Schedule, Node, Cards, Tavern)", back.Version == 6 && Xml(back) == Xml(save)
            && text.Contains("ScheduleVersion=\"2\"") && text.Contains("Schedule=\"acts-v2\"") && text.Contains("<Cards>") && text.Contains("<Tavern ")
            && back.Active.Cards.Count == s.Cards.Count && back.Active.Tavern.Gold == s.Tavern.Gold);
        File.WriteAllText(Path.Combine(dir, DungeonRunStore.FileName), text.Replace(" Version=\"6\">", " Version=\"7\">"));
        Throws<InvalidDataException>("A10 a version-7 file is refused", () => DungeonRunStore.Load(dir), "version 7");
    }
    {
        // A legacy run on version 4 writes none of the new fields.
        var s = run.NewRun(fresh, "MAGE", "mage_rimeward", "CS2_034", 99, schedule: DungeonRunSchedules.LegacyId, signatureId: run.SignatureOffer(fresh, run.Data.FindClass("MAGE"), 99).FirstOrDefault());
        run.BeginMatch(s); run.RecordOutcome(s, new DungeonRunProfile(), DungeonRunOutcome.Win);
        string text = Xml(new DungeonRunSave { Active = s });
        Check("A10 a version-4 legacy run writes no version-4 field", !new[] { "ScheduleVersion", "Schedule=", " Node=", "NextUid", "HealthBonus", "MajorsCleared", "<Cards", "<ActiveRules",
            "<Tavern", "<ModifierOffer", "<Loadout", "Takes=", "Choosing", "<Chosen", " Uid=", "Family" }.Any(text.Contains), text.Length.ToString());
    }
}

// ---------------------------------------------------------------- A11 a corrupted mirror is refused
{
    string dir = TempDir();
    var p = new DungeonRunProfile();
    var s = NewAct(run, p, 8, 11235);
    var save = new DungeonRunSave { Profile = p, Active = s };
    DungeonRunStore.Save(dir, save);
    byte[] good = File.ReadAllBytes(Path.Combine(dir, DungeonRunStore.FileName));
    var snapshot = DungeonRunStore.Clone(save);
    s.Deck.Add("CS2_024");
    Throws<InvalidDataException>("A11 a hand-corrupted mirror (Deck without a record) makes Save refuse", () => DungeonRunStore.Save(dir, save), "does not mirror");
    Check("A11 the refused save leaves the file as it was", File.ReadAllBytes(Path.Combine(dir, DungeonRunStore.FileName)).SequenceEqual(good));
    // The runtime's Mutate rolls back to its snapshot (DungeonRunRuntime.Mutate): the restored run saves again.
    save.Active = snapshot.Active;
    DungeonRunStore.Save(dir, save);
    Check("A11 the rolled-back run saves and matches the file", File.ReadAllBytes(Path.Combine(dir, DungeonRunStore.FileName)).SequenceEqual(good));
    save.Active.Cards[0].Uid = save.Active.Cards[1].Uid;
    Throws<InvalidDataException>("A11 a repeated uid is refused", () => DungeonRunStore.Save(dir, save), "repeats");
    var legacy = new DungeonRunSave { Active = run.NewRun(fresh, "MAGE", "mage_rimeward", "CS2_034", 5, schedule: DungeonRunSchedules.LegacyId, signatureId: run.SignatureOffer(fresh, run.Data.FindClass("MAGE"), 5).FirstOrDefault()) };
    legacy.Active.Deck.Add("CS2_024");
    DungeonRunStore.Save(dir, legacy);
    Check("A11 a legacy run has no records and is never refused for them", DungeonRunStore.Load(dir).Active.Deck.Count == 11);
}

// ---------------------------------------------------------------- A12 UI text
{
    string dot = " " + (char)0xB7 + " ";
    Check("A12 act progress line: \"Act II · Floor 3 of 5 (8 of 15)\"", NativeDungeonRunText.ActFloorLine("II", 3, 5, 8, 15, false) == "Act II" + dot + "Floor 3 of 5 (8 of 15)",
        NativeDungeonRunText.ActFloorLine("II", 3, 5, 8, 15, false));
    Check("A12 an act's major adds \" · Major\"", NativeDungeonRunText.ActFloorLine("I", 5, 5, 5, 15, true) == "Act I" + dot + "Floor 5 of 5 (5 of 15)" + dot + "Major");
    Check("A12 the legacy floor line is unchanged", NativeDungeonRunText.FloorLine(3, 8, 1, false) == "Floor 3 of 8" + dot + "Band 1" && NativeDungeonRunText.FloorLine(8, 8, 3, true) == "Floor 8 of 8" + dot + "Final");
    // From a real state at act II floor 3 (as NativeDungeonRunUI.ProgressLine builds it).
    var p = new DungeonRunProfile();
    var s = NewAct(run, p, 9, 97531);
    while (!(s.Floor == 8 && s.Phase == DungeonRunPhase.PreMatch)) Step(run, s, p, 97531);
    var node = run.CurrentNode(s);
    var sch = run.ScheduleFor(s);
    string line = NativeDungeonRunText.ActFloorLine(sch.Acts[node.Act - 1].Id, node.FloorInAct, sch.Acts[node.Act - 1].Floors, node.Floor, sch.FloorCount, node.Kind == DungeonRunNodeKind.Major);
    Check("A12 a run at global floor 8 reads Act II · Floor 3 of 5 (8 of 15)", line == "Act II" + dot + "Floor 3 of 5 (8 of 15)", line);
    Check("A12 modifier effect text", NativeDungeonRunText.ModifierEffect(5, 0, 0, 0, 0, 0, true) == "Bosses +5 Health; an extra treasure after the major"
        && NativeDungeonRunText.ModifierEffect(0, -1, 1, 0, 0, 0, false) == "-1 bundle on offer; +1 tavern gold", NativeDungeonRunText.ModifierEffect(0, -1, 1, 0, 0, 0, false));
    Check("A12 keep caption and rules line", NativeDungeonRunText.KeepCaption(2, 1) == "Keep 2 cards of one bundle (1 kept)." && NativeDungeonRunText.RulesLine(new[] { "Iron Bosses" }) == "Rules: Iron Bosses"
        && NativeDungeonRunText.RulesLine(new string[0]) == null);
}

// ---------------------------------------------------------------- refusals and edges
{
    Throws<ArgumentException>("NewRun with an unknown schedule is refused", () => run.NewRun(fresh, "MAGE", "mage_rimeward", "CS2_034", 1, schedule: "acts-v9", signatureId: run.SignatureOffer(fresh, run.Data.FindClass("MAGE"), 1).FirstOrDefault()), "Unknown schedule");
    Throws<ArgumentException>("NewRun with a schedule on data without schedules is refused", () => new DungeonRun(legacyData).NewRun(fresh, "MAGE", "mage_rimeward", "CS2_034", 1, schedule: "acts-v1"));
    var s = run.NewRun(fresh, "MAGE", "mage_rimeward", "CS2_034", 2, schedule: "act1", signatureId: run.SignatureOffer(fresh, run.Data.FindClass("MAGE"), 2).FirstOrDefault());
    while (!s.Finished) Step(run, s, fresh, 2);
    Check("the act1 MVP schedule: five floors, ends in Victory at its major", s.Phase == DungeonRunPhase.Victory && s.Floor == 5 && s.MajorsCleared == 1 && s.BossSequence.Count == 5);
    var a = NewAct(run, fresh, 0, 3);
    Check("a new act run starts at node 0, floor 1, pre-match, with its stamps and 10 starter records",
        a.IsActRun && a.ScheduleVersion == 2 && a.Schedule == "acts-v2" && a.Node == 0 && a.Floor == 1 && a.Phase == DungeonRunPhase.PreMatch && a.Cards.Count == 10 && a.NextUid == 10
        && a.Cards.All(c => c.Origin == "starter" && c.AddedFloor == 1) && a.Deck.SequenceEqual(data.Deck(a.StarterDeck).Cards));
    while (a.Phase != DungeonRunPhase.Tavern) Step(run, a, fresh, 3);
    Throws<InvalidOperationException>("a fight cannot start at a tavern", () => run.BuildPlan(a));
    a.Tavern.Pending = new DungeonRunTavernPending { Option = "x" };
    Throws<InvalidOperationException>("a tavern with a pending purchase cannot be left", () => run.LeaveTavern(a), "pending");
    a.Tavern.Pending = null;
    run.Abandon(a, new DungeonRunProfile());
    Check("abandoning at a tavern ends the run and closes the node", a.Phase == DungeonRunPhase.Defeat && a.Tavern == null);
    var big = NewAct(run, fresh, 1, 4);
    for (int i = 0; i < 41; i++) DungeonRunDeck.Add(big, "CS2_024", DungeonRunDeck.OriginTavern);
    Throws<InvalidOperationException>("an act-run deck above deckMax (51) cannot start a fight", () => run.BuildPlan(big), "10 to 50");
    var fault = NewAct(run, fresh, 1, 5);
    run.BeginMatch(fault); run.RecordOutcome(fault, fresh, DungeonRunOutcome.Fault);
    Check("a fault restages the act fight at the same node and floor", fault.Phase == DungeonRunPhase.PreMatch && fault.Node == 0 && fault.Floor == 1 && fault.Attempt == 1);
}

// ---------------------------------------------------------------- R review fixes: Health identity, schedule pinning, summary
{
    bool Ok(Action action) { try { action(); return true; } catch (Exception) { return false; } }
    string First(string text, string old, string replacement)
    {
        int at = text.IndexOf(old, StringComparison.Ordinal);
        if (at < 0) throw new InvalidOperationException("mutation anchor missing: " + old);
        return text.Substring(0, at) + replacement + text.Substring(at + old.Length);
    }
    // R1 a boss's own Health is an offset to the act curve: Pit Champion keeps +10 over its band-2 peers in every slot.
    var pit = data.Boss("pit_champion"); var sov = data.Boss("hollow_sovereign");
    Check("R1 Health offsets: Pit Champion +10 (50 vs band 2's 40), Hollow Sovereign +10 (70 vs finals' 60), others 0",
        run.HealthOffset(pit) == 10 && run.HealthOffset(sov) == 10 && data.Bosses.Where(b => b.Id != pit.Id && b.Id != sov.Id).All(b => run.HealthOffset(b) == 0),
        run.HealthOffset(pit) + "/" + run.HealthOffset(sov));
    int pitSeen = 0, pitBad = 0, peerBad = 0;
    for (int k = 0; k < 60 && pitSeen < 6; k++)
    {
        long seed = 5150 + 31L * k;
        var s = NewAct(run, all, 0, seed);
        int at = s.BossSequence.IndexOf("pit_champion");
        if (at < 0) continue;
        while (!s.Finished && !(s.Floor == at + 1 && s.Phase == DungeonRunPhase.PreMatch)) Step(run, s, all, seed);
        if (s.Finished) continue;
        pitSeen++;
        var node = run.CurrentNode(s);
        int curve = acts.Acts[node.Act - 1].BossHealth[node.FloorInAct - 1] + run.ActiveModifiers(s).Sum(m => m.BossHealthDelta);
        if (run.BossHealth(s) != curve + 10 || run.BuildPlan(s).BossHealth != curve + 10) pitBad++;
        // A peer in the same slot of another run gets the curve alone.
        var peer = NewAct(run, all, 0, seed + 1);
        while (!peer.Finished && !(peer.Floor == at + 1 && peer.Phase == DungeonRunPhase.PreMatch)) Step(run, peer, all, seed + 1);
        if (!peer.Finished && run.CurrentBoss(peer).Health == null)
        {
            var pn = run.CurrentNode(peer);
            if (run.BossHealth(peer) != acts.Acts[pn.Act - 1].BossHealth[pn.FloorInAct - 1] + run.ActiveModifiers(peer).Sum(m => m.BossHealthDelta)) peerBad++;
        }
    }
    Check("R1 Pit Champion's act-run Health is its slot's curve + 10, a peer's is the curve (walked runs)", pitSeen >= 3 && pitBad == 0 && peerBad == 0, pitSeen + " seen, " + pitBad + "/" + peerBad + " bad");

    // R2 a saved act run is pinned to its schedule version and must fit its schedule.
    var a = NewAct(run, fresh, 0, 808);
    Step(run, a, fresh, 808);
    // Phase 8: version 3 is the loaded one (2 and 1 load as older released versions); a run of a future version (4) is the one refused.
    // Step 9: version 4 is the loaded one, so the future version is 5.
    var v2 = new DungeonRunState { ScheduleVersion = 5, Schedule = a.Schedule, Node = a.Node, Floor = a.Floor, BossSequence = new List<string>(a.BossSequence), Phase = a.Phase };
    Throws<InvalidDataException>("R2 a run saved on another schedule version is refused (ScheduleFor)", () => run.ScheduleFor(v2), "schedule version 5");
    Throws<InvalidDataException>("R2 ... and at load (Resume)", () => run.Resume(v2), "schedule version 5");
    var shortSeq = new DungeonRunState { ScheduleVersion = 2, Schedule = a.Schedule, Node = a.Node, Floor = a.Floor, BossSequence = a.BossSequence.Take(14).ToList(), Phase = a.Phase };
    Throws<InvalidDataException>("R2 a boss sequence shorter than the schedule's floors is refused", () => run.CurrentNode(shortSeq), "14 bosses");
    var moved = new DungeonRunState { ScheduleVersion = 2, Schedule = a.Schedule, Node = a.Node + 1, Floor = a.Floor, BossSequence = new List<string>(a.BossSequence), Phase = DungeonRunPhase.PreMatch };
    if (acts.Nodes[moved.Node].Floor == moved.Floor) moved.Node++;
    Throws<InvalidDataException>("R2 a node whose floor is not the run's floor is refused", () => run.CurrentNode(moved), "is on floor");
    Check("R2 a sound act run still resumes", Ok(() => run.Resume(a)) && run.CurrentNode(a).Floor == a.Floor);
    var fin = NewAct(run, fresh, 0, 809); run.Abandon(fin, new DungeonRunProfile()); fin.ScheduleVersion = 3;
    Check("R2 a finished run of another version still resumes (its summary and New run stay reachable)", Ok(() => run.Resume(fin)));

    // R3 the validator pins each released schedule's node shape: a node edit must bump scheduleVersion; tuning need not.
    Check("R3 the shipped schedules match their released shapes", DungeonRunSchedules.ReleasedShapes[1].All(p => DungeonRunSchedules.ShapeDigest(data.Schedules.Find(p.Key)) == p.Value));
    List<string> MutatedErrors(string json) => DungeonRunValidator.ValidateSchedules(Load(json));
    string tavernMoved = scheduleJson.Replace("\"taverns\": [3]", "\"taverns\": [2]");
    var movedErrs = MutatedErrors(tavernMoved);
    // Phase 7: acts-v2 (version 2) has act I's tavern too, and every released version's shapes stay pinned (a saved run of an
    // older version still loads), so a version bump no longer frees acts-v1 and act1: only a new schedule may differ.
    // Step 9 adds acts-v4h and acts-v4t (version 4, act I's tavern too): seven schedules refuse the edit.
    Check("R3 moving act I's tavern without a version bump is refused (acts-v1, act1, acts-v2, since phase 8 acts-v3h and acts-v3t, since step 9 acts-v4h and acts-v4t)", movedErrs.Count(e => e.Contains("node shape changed")) == 7, string.Join(" | ", movedErrs));
    var bumped = MutatedErrors(First(tavernMoved, "\"scheduleVersion\": 4", "\"scheduleVersion\": 5"));
    Check("R3 the same edit with scheduleVersion 5 still refuses the schedules released earlier (all seven stay pinned)",
        bumped.Count(e => e.Contains("node shape changed")) == 7, string.Join(" | ", bumped));
    var tuned = MutatedErrors(First(scheduleJson, "\"bossHealth\": [30, 32, 34, 36, 40]", "\"bossHealth\": [31, 33, 35, 37, 41]"));
    Check("R3 a tuning edit (Health curve) keeps the shape", !tuned.Any(e => e.Contains("node shape")), string.Join(" | ", tuned));
    var dropped = MutatedErrors(scheduleJson.Replace("\"default\": \"acts-v2\"", "\"default\": \"act1\"").Replace("\"acts-v1\": {", "\"acts-vX\": {"));
    Check("R3 dropping a released schedule is refused", dropped.Any(e => e.StartsWith("schedule acts-v1: released") && e.Contains("missing")), string.Join(" | ", dropped));

    // R4 the summary's act wording and the last boss actually fought.
    var lost = NewAct(run, fresh, 0, 810);
    while (lost.Floor < 3) Step(run, lost, fresh, 810);
    while (lost.Phase != DungeonRunPhase.PreMatch) Step(run, lost, fresh, 810);
    string third = lost.BossSequence[2];
    run.BeginMatch(lost); run.RecordOutcome(lost, new DungeonRunProfile(), DungeonRunOutcome.Loss);
    Check("R4 a lost act fight names its boss", lost.Phase == DungeonRunPhase.Defeat && DungeonRun.LastBoss(lost) == third);
    var ab = NewAct(run, fresh, 0, 811);
    while (ab.Phase != DungeonRunPhase.Tavern) Step(run, ab, fresh, 811);
    string fought = ab.Matches.Last().Boss;
    var abProfile = new DungeonRunProfile();
    run.Abandon(ab, abProfile);
    Check("R4 abandoned at a tavern: the last boss is the last one fought, not the next floor's (summary and archive)",
        DungeonRun.LastBoss(ab) == fought && fought != ab.BossSequence[ab.Floor - 1] && abProfile.Archive.Last().LastBoss == fought, fought + " vs next " + ab.BossSequence[ab.Floor - 1]);
    Check("R4 act result text", NativeDungeonRunText.ActResultLine(false, false, "II", 3, 5, 8, 15) == "Defeated in Act II · Floor 3 of 5 (8 of 15)."
        && NativeDungeonRunText.ActResultLine(false, true, "I", 4, 5, 4, 15) == "Run abandoned in Act I · Floor 4 of 5 (4 of 15)."
        && NativeDungeonRunText.ActResultLine(true, false, "III", 5, 5, 15, 15) == "You won the run!");
    var lg = run.NewRun(fresh, "MAGE", "mage_rimeward", "CS2_034", 77, schedule: DungeonRunSchedules.LegacyId, signatureId: run.SignatureOffer(fresh, run.Data.FindClass("MAGE"), 77).FirstOrDefault());
    run.Abandon(lg, new DungeonRunProfile());
    Check("R4 a legacy run's last boss is still its floor's boss", DungeonRun.LastBoss(lg) == lg.BossSequence[0]);
}

// ---------------------------------------------------------------- A12 Death Knight and Demon Hunter draft supply (phase 6 F16)
// Before F16 the two classes had 4 fixed class bundles + 8 neutral ones, each taken once: an act run ran dry at floor 12.
// The SoloStone-curated pool entries (tools/dungeon-run-catalog/curation.json customBuckets, each taken up to
// draft.bucketMaxTakes times) close the gap. Phase 6 review: with 4 Death Knight entries an always-pick-last walk still fell
// to 2 offers on floors 13-14 (4 of 300 seeds), so each class has 7 entries now (Demon Hunter showed the same fault on other seeds). An act run that wins every fight is walked:
// (a) declining the Lean Supply modifier (draftOptionsDelta -1), 300 seeds per class and starter deck, the power rotating
//     through the class's powers, under three pick policies (always the first offer, always the last, a seeded random one,
//     for the signature, treasure, bundle and card picks): every one of the 14 draft floors offers 3 bundles;
// (b) taking Lean Supply whenever it is offered (20 seeds, first offer): every draft offers exactly the act's option count.
{
    const int SupplySeeds = 300;
    long[] supplySeeds = Enumerable.Range(1, SupplySeeds).Select(i => 7919L * i + 13).ToArray();
    string[] policyNames = { "first", "last", "random" };
    List<(int Floor, int Offers, int Options)> Walk(string cls, string deck, string power, long seed, bool takeLean, int policy)
    {
        var rng = new Random(unchecked((int)(seed * 31 + policy)));
        int Pick(int n) => policy == 0 ? 0 : policy == 1 ? n - 1 : rng.Next(n);
        var row = data.Class(cls);
        var sigs = run.SignatureOffer(fresh, row, seed);
        var s = run.NewRun(fresh, cls, deck, power, seed, signatureId: sigs.Count > 0 ? sigs[Pick(sigs.Count)] : null);
        var trace = new List<(int, int, int)>();
        int lastFight = run.ScheduleFor(s).FloorCount;
        for (int guard = 0; guard < 600 && !s.Finished; guard++)
        {
            switch (s.Phase)
            {
                case DungeonRunPhase.PreMatch:
                    int floor = s.Floor;
                    run.BeginMatch(s); run.RecordOutcome(s, fresh, DungeonRunOutcome.Win);
                    if (floor == lastFight) break;
                    if (s.Phase == DungeonRunPhase.TreasureDraft) run.PickTreasure(s, s.OpenDraft.Offered[Pick(s.OpenDraft.Offered.Count)]);
                    bool drafting = s.Phase == DungeonRunPhase.BundleDraft;
                    trace.Add((floor, drafting ? s.OpenDraft.Offered.Count : 0, run.DraftOptions(s)));
                    break;
                case DungeonRunPhase.TreasureDraft: run.PickTreasure(s, s.OpenDraft.Offered[Pick(s.OpenDraft.Offered.Count)]); break;
                case DungeonRunPhase.Modifier:
                {
                    var offered = s.ModifierOffer.Offered;
                    bool Lean(string id) => data.Schedules.Modifiers.First(m => m.Id == id).DraftOptionsDelta < 0;
                    string pick = takeLean ? offered.FirstOrDefault(Lean) ?? offered[0] : offered.FirstOrDefault(id => !Lean(id)) ?? offered[0];
                    run.PickModifier(s, pick);
                    break;
                }
                case DungeonRunPhase.Tavern: run.LeaveTavern(s); break;
                case DungeonRunPhase.BundleDraft:
                    if (s.OpenDraft.Takes == 0) { run.PickBundle(s, s.OpenDraft.Offered[Pick(s.OpenDraft.Offered.Count)]); break; }
                    string bundle = s.OpenDraft.Choosing ?? s.OpenDraft.Offered[Pick(s.OpenDraft.Offered.Count)];
                    var left = run.OfferCards(s.OpenDraft, bundle);
                    foreach (string kept in s.OpenDraft.Chosen) left.Remove(kept);
                    run.PickDraftCard(s, bundle, left[Pick(left.Count)]);
                    break;
                default: throw new Exception("A12 walk stopped at " + s.Phase);
            }
        }
        if (!s.Finished || s.Phase != DungeonRunPhase.Victory) throw new Exception("A12 the run did not reach Victory: " + s.Phase);
        return trace;
    }
    foreach (string cls in new[] { "DEATHKNIGHT", "DEMONHUNTER" })
    {
        var row = data.Class(cls);
        var decks = row.StartDecks(data).Where(d => data.FindDeck(d) != null).ToList();
        for (int di = 0; di < decks.Count; di++)
        {
            string deck = decks[di];
            for (int policy = 0; policy < 3; policy++)
            {
                var strict = new List<string>(); int strictFloors = 0, walks = 0;
                for (int i = 0; i < supplySeeds.Length; i++)
                {
                    long seed = supplySeeds[i];
                    string power = row.Powers[(i + di) % row.Powers.Count];
                    var a = Walk(cls, deck, power, seed, false, policy); walks++;
                    if (a.Count != 14) strict.Add(seed + ": " + a.Count + " draft floors");
                    foreach (var t in a) { strictFloors++; if (t.Offers != 3) strict.Add(seed + "/" + power + "@F" + t.Floor + "=" + t.Offers + "/" + t.Options); }
                }
                Check($"A12 {cls} {deck} pick-{policyNames[policy]}: {SupplySeeds} act runs (Lean Supply declined) offer 3 bundles on every one of the 14 draft floors",
                    strict.Count == 0 && strictFloors == 14 * walks && walks == SupplySeeds, strictFloors + " floors; " + string.Join(" ", strict.Take(10)));
            }
        }
        var lean = new List<string>(); int leanFloors = 0, leanCut = 0;
        foreach (long seed in supplySeeds.Take(20))
        {
            var b = Walk(cls, row.DefaultDeck, row.Powers[0], seed, true, 0);
            if (b.Count != 14) lean.Add(seed + ": " + b.Count + " draft floors");
            foreach (var t in b) { leanFloors++; if (t.Options < 3) leanCut++; if (t.Offers == 0 || t.Offers != t.Options) lean.Add(seed + "@F" + t.Floor + "=" + t.Offers + "/" + t.Options); }
        }
        Check($"A12 {cls}: 20 act runs taking Lean Supply offer exactly the act's option count on all 14 draft floors (never short)", lean.Count == 0 && leanFloors == 280 && leanCut > 0,
            leanFloors + " floors, " + leanCut + " cut to 2 by Lean Supply; " + string.Join(" ", lean.Take(10)));
        Console.WriteLine($"NOTE A12 {cls}: {decks.Count} decks x 3 pick policies x {SupplySeeds} seeds, 3 offers on all 14 floors; with Lean Supply {leanCut} of {leanFloors} floors cut to 2 offers, none short");
    }
    var curated = data.Bundles.Where(b => b.Provenance.Any(p => p.Adventure == "SoloStone-curated")).ToList();
    var poolPolicy = OfflinePoolPolicy.Load();
    Check("A12 curated pool entries: 7 Death Knight and 7 Demon Hunter, each with at least 6 admitted, rune-free candidates of that class", curated.Count == 14
        && curated.Count(b => b.ClassSet.SequenceEqual(new[] { "DEATHKNIGHT" })) == 7 && curated.Count(b => b.ClassSet.SequenceEqual(new[] { "DEMONHUNTER" })) == 7
        && curated.All(b => b.IsPool && b.Offerable && b.Candidates.Count >= 6 && b.Candidates.All(c => DungeonRunValidator.PoolCardReason(c.Id, b.ClassSet, false, poolPolicy, catalogRun: true) == null
            && !DungeonRunValidator.HasRuneCost(c.Id) && Cards.FromId(c.Id).Implemented && Cards.FromId(c.Id).Collectible)),
        string.Join(",", curated.Select(b => b.Id + ":" + b.Candidates.Count)));
}

Console.WriteLine(passes + " passed, " + failures + " failed");
return failures == 0 ? 0 : 1;
