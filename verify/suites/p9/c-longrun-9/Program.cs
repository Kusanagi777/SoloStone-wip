using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using SabberStoneCore.CardSets.DungeonRun;

// c-longrun-9: step 9's long-run and save/resume walk (handoff/phase9/plan.md section 1 D, criteria D1, D2 and D4; lane S), on
// the shipped data with encounters, on the released schedules acts-v3h and acts-v3t and on the step-9 schedules acts-v4h and
// acts-v4t. No engine game is played: every fight's end is a synthetic RunMatchEnd of the walker's seeded outcome script
// (verify/suites/p9/common/LongRunWalker.cs, picks by PickPolicy.cs).
//   W  the run set, per schedule: every loadout (hero row x starter deck; acts-v3t and acts-v4t leave out DALA_Tekahn's four,
//      FinaleExcludes) x 2 seeds, and 22 rows (the Monster Hunt, Heist and Rumble hero rows) x 10 seeds. Seed 0 of a loadout,
//      and the even seeds of a row, win every fight (Victory); the others lose one seeded fight (a scripted Defeat). The seeds
//      of a loadout, and of a row, share one profile, so a Plague Lord's banked damage and the stats carry into the next run.
//      On a stamp-4 schedule the walker also escapes (ULDA_111) and restarts (GILA_911) once whenever the plan allows it.
//   D1 after every step the run is saved to a file, loaded, upgraded (a version-6 save: the identity) and resumed, and the walk
//      goes on from the loaded object; its step lines and its save file (the whole save, profile included) equal those of an
//      unbroken twin walked beside it from the same new run (the bytes the twin's save would write), and so does the loaded
//      end state. The twin never crashes and builds each plan once, so the crash checks and the extra BuildPlan calls of the
//      reloaded run are shown to change nothing.
//   D2 a crash at InMatch at every pre-match (on a copy of the save): the fight starts, the save is written and loaded, and the
//      restaged pre-match equals the one before the crash except Attempt (+1) and the floor's HeroSwitch (the documented quirk:
//      BeginMatch stores it, Resume keeps it); the rebuilt plan and match seed are the same. Hero-switch (Hagatha), relay
//      (Tekahn), Plague Lord and restarted (Rewind) floors are counted and each kind is met.
//   D4 card legality at every step (every card record through the drafted-card predicate, Death Knight runes, opening flags,
//      the deck mirror and deckMax), with the documented exemptions only; BuildPlan never throws at a pre-match and the plan
//      always passes NativeMatch's run-fight limits.
//   C  invariants of the same walks: no forbidden pair (DungeonRun.ForbiddenPairs, criterion C1) at any step; the step-9 match
//      ends stay off the released schedules (no escape, restart, Rewind or Hearthstone on acts-v3h/v3t).
// C_LONGRUN9_STRIDE=n (a development aid, never set by the runners) walks only every n-th loadout and row, so W1 fails by design.
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
void Guard(string name, Action body)
{
    try { body(); }
    catch (Exception e) { failures++; Console.WriteLine("FAIL " + name + " threw " + e.GetType().Name + ": " + e.Message.Split('\n')[0] + " @ " + (e.StackTrace ?? "").Split('\n').FirstOrDefault()?.Trim()); }
}
var clock = System.Diagnostics.Stopwatch.StartNew();

string root = VerifyPaths.Data;
string R(string f) => File.ReadAllText(Path.Combine(root, f));
var data = DungeonRunData.Parse(R("dungeon-run/run-v1.json"), R("dungeon-run/pool-v1.json"), R("npc/decks-v1.json"), R("dungeon-run/catalog-v1.json"),
    R("dungeon-run/schedule-v1.json"), R("dungeon-run/tavern-v1.json"), R("dungeon-run/encounters-v1.json"));
var run = new DungeonRun(data);
DungeonRunProfile All() { var p = new DungeonRunProfile(); p.Unlocked.AddRange(data.Unlocks.Keys); return p; }
string Origin(DungeonRunClass c) => c.Adventure ?? "classes";
// The save files go through DungeonRunStore.Save and Load as the plugin's do; on a host with a RAM file system (/dev/shm) they
// live there, since Save flushes every write to disk and the walk writes about 150000 files (the folder is removed at the end).
string tmpRoot = Directory.Exists("/dev/shm") ? "/dev/shm/solostone-longrun9-" + Environment.ProcessId : VerifyPaths.Out("tmp/longrun9");
string reloadDir = Path.Combine(tmpRoot, "reload"), crashDir = Path.Combine(tmpRoot, "crash");
foreach (string d in new[] { reloadDir, crashDir }) { if (Directory.Exists(d)) Directory.Delete(d, true); Directory.CreateDirectory(d); }

string[] schedules = { "acts-v3h", "acts-v3t", "acts-v4h", "acts-v4t" };
int stride = int.TryParse(Environment.GetEnvironmentVariable("C_LONGRUN9_STRIDE"), out int st) && st > 1 ? st : 1;
if (stride > 1) Note("C_LONGRUN9_STRIDE " + stride + ": a partial run set");
var loadouts = data.Classes.SelectMany(c => c.StartDecks(data).Select(deck => (Row: c, Deck: deck))).ToList();
var tenRows = data.Classes.Where(c => c.Adventure == "MonsterHunt" || c.Adventure == "Heist" || c.Adventure == "Rumble").ToList();

// D4's documented exemptions (LongRunWalker.Exemptions): three class failures a run deck holds by design, each with its reason.
var exemptions = LongRunWalker.Exemptions;

// ============================================================ the walks
var tallies = schedules.ToDictionary(x => x, _ => new Tally(), StringComparer.Ordinal);

// One run of a loadout on a schedule, walked twice side by side (reloaded at every step, and unbroken); the profiles go on.
void WalkPair(Tally t, string schedule, DungeonRunClass row, string deck, long seed, bool win, ref DungeonRunProfile profA, ref DungeonRunProfile profB)
{
    var powers = run.AvailablePowers(row, profA).ToList();
    string power = powers[PickPolicy.Roll(seed, PickPolicy.SaltPower, 0, powers.Count)];
    var sigs = run.SignatureOffer(profA, row, seed);
    string sig = sigs.Count == 0 ? null : sigs[PickPolicy.Roll(seed, PickPolicy.SaltSignature, 0, sigs.Count)];
    DungeonRunState stateA, stateB;
    try
    {
        stateA = run.NewRun(profA, row.Id, deck, power, seed, schedule: schedule, signatureId: sig);
        stateB = run.NewRun(profB, row.Id, deck, power, seed, schedule: schedule, signatureId: sig);
    }
    catch (InvalidOperationException) when (row.Id == "DALA_Tekahn" && DungeonRun.FinaleExcludes(data.Schedules.Find(schedule), row)) { t.Excluded++; return; }
    int defeat = win ? 0 : 1 + PickPolicy.Roll(seed, PickPolicy.SaltOutcome, 99, stateA.BossSequence.Count);
    string tag = $"{schedule} {row.Id}/{deck} seed {seed}";
    var a = new LongRunWalker(run, new DungeonRunSave { Profile = profA, Active = stateA }) { DefeatFloor = defeat, TryEscape = true, TryRestart = true };
    var b = new LongRunWalker(run, new DungeonRunSave { Profile = profB, Active = stateB }) { DefeatFloor = defeat, TryEscape = true, TryRestart = true };
    t.Runs++;
    a.OnPreMatch = w =>
    {
        var s = w.State;
        t.Crashes++;
        var hs = run.HeroSwitchFor(s);
        if (hs?.Kind == DungeonRunHeroSwitch.KindHunters) t.CrashSwitch++;
        if (hs?.Kind == DungeonRunHeroSwitch.KindRelay) t.CrashRelay++;
        if (run.CurrentBoss(s).HasFlag(DungeonRunEncounterData.PersistentDamage)) { t.CrashLord++; t.Lords.Add(run.CurrentBoss(s).Id); }
        if (s.RestartIndex > 0) t.CrashRestart++;
        string problem;
        try { problem = LongRunWalker.CrashRestage(run, w.Save, crashDir); } catch (Exception e) { problem = "threw " + e.GetType().Name + ": " + e.Message; }
        if (problem != null) { t.CrashBad++; t.Bad($"D2 {tag} floor {s.Floor}: {problem}"); }
    };
    a.OnPlan = (w, plan, thrown) =>
    {
        t.Plans++;
        if (plan == null) { t.PlanThrew++; t.Bad($"D4 {tag} floor {w.State.Floor}: BuildPlan threw {thrown?.GetType().Name}: {thrown?.Message}"); return; }
        string limits = LongRunWalker.NativeLimits(run, w.State, plan);
        if (limits != null) { t.LimitBad++; t.Bad($"D4 {tag} floor {w.State.Floor}: {limits}"); }
    };
    void Invariants(LongRunWalker w)
    {
        foreach (string line in LongRunWalker.Legality(run, w.State))
        {
            var parts = line.Split(' ');
            string kind = string.Join(" ", parts.Take(parts.Length - 1));
            t.Legality[kind] = t.Legality.GetValueOrDefault(kind) + 1;
            if (!t.LegalityExample.ContainsKey(kind)) t.LegalityExample[kind] = line.Split(' ').Last() + " (" + tag + ")";
        }
        var pairs = run.ForbiddenPairs(w.State);
        if (pairs.Count > 0) { t.Forbidden++; t.Bad($"C1 {tag} node {w.State.Node}: {pairs[0]}"); }
        if (w.State.Treasures.Contains(RunMatchEnds.Rewind)) t.Rewinds++;
        if (w.State.Deck.Contains(RunMatchEnds.Hearthstone)) t.Hearthstones++;
    }
    a.Save = LongRunWalker.Reload(run, a.Save, reloadDir, out bool up0);
    t.Reloads++; if (up0) t.Upgrades++;
    bool broken = false;
    for (int guard = 0; guard < 600 && !b.Done; guard++)
    {
        string lb = b.Step();
        string la;
        try { la = a.Step(); }
        catch (Exception e) { t.LineMismatch++; t.Bad($"D1 {tag} step {b.Steps}: the reloaded run threw {e.GetType().Name}: {e.Message}"); broken = true; break; }
        a.Save = LongRunWalker.Reload(run, a.Save, reloadDir, out bool up);
        t.Reloads++; if (up) t.Upgrades++;
        t.Steps++;
        if (la != lb && !broken) { t.LineMismatch++; t.Bad($"D1 {tag} step {b.Steps}: {la} | twin {lb}"); broken = true; }
        // The file the reloaded run was just saved to (what it continues from) against the bytes the twin would write.
        if (!broken && LongRunWalker.FileDigest(reloadDir) != LongRunWalker.StoreDigest(b.Save))
        {
            t.DigestMismatch++; broken = true;
            t.Bad($"D1 {tag} step {b.Steps} ({lb}): {LongRunWalker.FirstDiff(LongRunWalker.Xml(b.Save), LongRunWalker.Xml(a.Save))}");
        }
        Invariants(a);
    }
    if (!a.Done && !broken) { t.LineMismatch++; t.Bad($"D1 {tag}: the reloaded run did not end with its twin"); }
    // The final loaded object too (a field a load drops would show in the next file; after the last step there is none).
    if (!broken && LongRunWalker.Digest(a.Save) != LongRunWalker.Digest(b.Save)) { t.DigestMismatch++; t.Bad($"D1 {tag}: the loaded end state differs from the twin's"); }
    t.Fights += b.Fights; t.Escapes += b.Escapes; t.Restarts += b.Restarts; t.Ends++;
    var end = b.State;
    if (end.Phase == DungeonRunPhase.Victory && defeat == 0) t.Victories++;
    else if (end.Phase == DungeonRunPhase.Defeat && defeat > 0 && end.Matches.Last().Floor == defeat) t.Defeats++;
    else { t.WrongEnd++; t.Bad($"W {tag}: ended {end.Phase} at floor {end.Floor}, script {(defeat == 0 ? "Victory" : "Defeat at floor " + defeat)}"); }
    profA = a.Save.Profile; profB = b.Save.Profile;
}

Guard("W", () =>
{
    foreach (string schedule in schedules)
    {
        var t = tallies[schedule];
        int si = Array.IndexOf(schedules, schedule);
        for (int li = 0; li < loadouts.Count; li += stride)
        {
            var (row, deck) = loadouts[li];
            DungeonRunProfile pa = All(), pb = All();
            for (int k = 0; k < 2; k++) WalkPair(t, schedule, row, deck, 9000000 + 1000000L * si + 10 * li + k, k == 0, ref pa, ref pb);
        }
        for (int ri = 0; ri < tenRows.Count; ri += stride)
        {
            var row = tenRows[ri];
            DungeonRunProfile pa = All(), pb = All();
            for (int k = 0; k < 10; k++) WalkPair(t, schedule, row, row.StartDecks(data)[k % row.StartDecks(data).Count], 9500000 + 1000000L * si + 100 * ri + k, k % 2 == 0, ref pa, ref pb);
        }
        Note($"W {schedule}: {t.Runs} runs ({t.Victories} Victory, {t.Defeats} scripted Defeat, {t.Excluded} left out by FinaleExcludes), {t.Steps} steps, {t.Fights} fights,"
            + $" {t.Escapes} escapes, {t.Restarts} restarts; {t.Reloads} reloads; {t.Crashes} crash restages (hero switch {t.CrashSwitch}, relay {t.CrashRelay},"
            + $" Plague Lord {t.CrashLord} [{string.Join(",", t.Lords)}], restarted {t.CrashRestart})");
        foreach (var kv in t.Legality) Note($"D4 {schedule} legality {kv.Key}: {kv.Value} card-steps, e.g. {t.LegalityExample[kv.Key]}");
        foreach (string bad in t.FirstBad) Note("first problems " + schedule + ": " + bad);
    }
});

int Sum(Func<Tally, int> f, params string[] which) => (which.Length == 0 ? schedules : which).Sum(x => f(tallies[x]));
string[] v3 = { "acts-v3h", "acts-v3t" }, v4 = { "acts-v4h", "acts-v4t" }, tekahnFinale = { "acts-v3t", "acts-v4t" };
Guard("checks", () =>
{
    int expectH = loadouts.Count * 2 + tenRows.Count * 10;
    int tekahn = loadouts.Count(l => l.Row.Id == "DALA_Tekahn") * 2 + (tenRows.Any(r => r.Id == "DALA_Tekahn") ? 10 : 0);
    Check($"W1 the run set: {loadouts.Count} loadouts x 2 seeds + {tenRows.Count} rows x 10 seeds on each schedule ({expectH} runs; acts-v3t and acts-v4t leave out DALA_Tekahn's {tekahn})",
        loadouts.Count == 116 && tenRows.Count == 22 && schedules.All(x => tallies[x].Runs == expectH - (tekahnFinale.Contains(x) ? tekahn : 0) && tallies[x].Excluded == (tekahnFinale.Contains(x) ? tekahn : 0)),
        string.Join(" ", schedules.Select(x => x + "=" + tallies[x].Runs + "/-" + tallies[x].Excluded)));
    Check("W2 every run ends as its script says (Victory, or Defeat at the scripted floor), with at least one Victory and one Defeat per schedule",
        schedules.All(x => tallies[x].WrongEnd == 0 && tallies[x].Victories > 0 && tallies[x].Defeats > 0 && tallies[x].Victories + tallies[x].Defeats == tallies[x].Runs),
        string.Join(" ", schedules.Select(x => $"{x}={tallies[x].Victories}V/{tallies[x].Defeats}D/{tallies[x].WrongEnd}bad")));
    Check($"D1 after every one of the {Sum(t => t.Steps)} steps the run reloaded from its file (Save, Load, Upgrade, Resume) equals its unbroken twin: the same step lines and the same save digest (profile included)",
        Sum(t => t.DigestMismatch) == 0 && Sum(t => t.LineMismatch) == 0 && Sum(t => t.Steps) > 100000 / stride,
        $"{Sum(t => t.DigestMismatch)} digest, {Sum(t => t.LineMismatch)} line mismatches");
    Check($"D1 every reload of a version-6 save is an Upgrade no-op ({Sum(t => t.Reloads)} reloads)", Sum(t => t.Upgrades) == 0 && Sum(t => t.Reloads) > Sum(t => t.Steps), Sum(t => t.Upgrades) + " upgraded");
    Check($"D1 the step-9 match ends are walked on acts-v4h/v4t (escapes {Sum(t => t.Escapes, v4)}, Rewind restarts {Sum(t => t.Restarts, v4)}) and never on the released acts-v3h/v3t"
        + " (no escape, no restart, no Rewind held, no Hearthstone in a deck)",
        v4.All(x => tallies[x].Escapes > 0 && tallies[x].Restarts > 0) && v3.All(x => tallies[x].Escapes == 0 && tallies[x].Restarts == 0 && tallies[x].Rewinds == 0 && tallies[x].Hearthstones == 0),
        string.Join(" ", schedules.Select(x => $"{x}: {tallies[x].Escapes}e/{tallies[x].Restarts}r/{tallies[x].Rewinds}rw/{tallies[x].Hearthstones}hs")));
    Check($"D2 a crash at InMatch at every one of the {Sum(t => t.Crashes)} pre-matches restages identically (Attempt +1 and the floor's HeroSwitch excepted; plan and match seed unchanged)",
        Sum(t => t.CrashBad) == 0 && Sum(t => t.Crashes) == Sum(t => t.Plans), Sum(t => t.CrashBad) + " differ");
    Check($"D2 the crashed floors include hero-switch floors on every schedule ({Sum(t => t.CrashSwitch)}), relay floors on the Tekahn finales ({Sum(t => t.CrashRelay, tekahnFinale)}),"
        + $" Plague Lord floors on every schedule ({Sum(t => t.CrashLord)}) and Rewind-restarted floors on acts-v4h/v4t ({Sum(t => t.CrashRestart, v4)})",
        schedules.All(x => tallies[x].CrashSwitch > 0 && tallies[x].CrashLord > 0) && tekahnFinale.All(x => tallies[x].CrashRelay > 0)
        && v4.All(x => tallies[x].CrashRestart > 0) && v3.All(x => tallies[x].CrashRestart == 0) && new[] { "acts-v3h", "acts-v4h" }.All(x => tallies[x].CrashRelay == 0),
        string.Join(" ", schedules.Select(x => $"{x}: {tallies[x].CrashSwitch}/{tallies[x].CrashRelay}/{tallies[x].CrashLord}/{tallies[x].CrashRestart}")));
    var unexempt = schedules.SelectMany(x => tallies[x].Legality.Keys.Where(k => !k.StartsWith("exempt:", StringComparison.Ordinal)).Select(k => x + ": " + k + " " + tallies[x].LegalityExample[k])).ToList();
    foreach (var e in exemptions) Note($"D4 exemption {e.Key}: {e.Value}; {Sum(t => t.Legality.Where(kv => kv.Key.StartsWith("exempt:" + e.Key + " ", StringComparison.Ordinal)).Sum(kv => kv.Value))} card-steps");
    Check($"D4 card legality at every step: no run-deck card fails the drafted-card predicate, the runes or the opening-flag limit beyond the {exemptions.Count} documented exemptions",
        unexempt.Count == 0, string.Join(" | ", unexempt.Take(6)));
    Check($"D4 BuildPlan never throws at a pre-match ({Sum(t => t.Plans)} plans) and every plan passes NativeMatch's run-fight limits",
        Sum(t => t.PlanThrew) == 0 && Sum(t => t.LimitBad) == 0 && Sum(t => t.Plans) == Sum(t => t.Fights), $"{Sum(t => t.PlanThrew)} threw, {Sum(t => t.LimitBad)} out of limits");
    Check("C1 no forbidden pair (DungeonRun.ForbiddenPairs) in any state of any walk", Sum(t => t.Forbidden) == 0, Sum(t => t.Forbidden) + " states");
});

try { Directory.Delete(tmpRoot, true); } catch (IOException) { }
Console.WriteLine($"TIME {clock.Elapsed.TotalSeconds:F0}s");
Console.WriteLine($"RESULT {passes} passed, {failures} failed");
return failures == 0 ? 0 : 1;

internal sealed class Tally
{
    public int Runs, Victories, Defeats, WrongEnd, Steps, Fights, Escapes, Restarts, Reloads, Upgrades, DigestMismatch, LineMismatch, Crashes, CrashBad,
        CrashSwitch, CrashRelay, CrashLord, CrashRestart, Plans, PlanThrew, LimitBad, Forbidden, Excluded, Rewinds, Hearthstones, Ends;
    public readonly List<string> FirstBad = new List<string>();
    public readonly SortedDictionary<string, int> Legality = new SortedDictionary<string, int>(StringComparer.Ordinal);
    public readonly SortedDictionary<string, string> LegalityExample = new SortedDictionary<string, string>(StringComparer.Ordinal);
    public readonly SortedSet<string> Lords = new SortedSet<string>(StringComparer.Ordinal);
    public void Bad(string what) { if (FirstBad.Count < 6) FirstBad.Add(what); }
}
