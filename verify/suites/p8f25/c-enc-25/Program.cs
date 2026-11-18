using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

// c-enc-25: the phase-8 close-out (family 25), on the shipped data (encounters-v1.json loaded, as the plugin loads it).
//   A  the family 25 ids in the ledger: the 22 Monster Hunt label cards are their buckets' label rows, the three tavern keepers
//      are portraits of Bob's tavern, DALA_740 stays rejected and its five tokens inherit the reason, ULDA_911 Kindle and
//      ULDA_038 Explorer Retraining are tavern options, ULDA_607 stays deferred, ULDA_111 / GILA_900p / GILA_911 are phase 9;
//      a weight-0 tavern alias reads "via <target>" and a fight hero card is its encounter's row (the runs-and-state rerun)
//   B  the rejections audit: every row left missing or partial is pinned with a stated blocker the ledger describes, and every
//      rejection the families reported is recorded (ledger, curation.json); the stale F14 TRLA_065p note is gone; every admitted
//      twist no default schedule rolls is partial with run:twist-pools and HANDOFF 3.3 hands it to step 9
//   C  Explorer Retraining on new runs: the starting powers a run of an encounter schedule could have begun with are recorded
//      (DungeonRunState.PowerChoices); its taverns may offer ULDA_038; a run of an older schedule never records or offers it
//   D  the shipped finales: acts-v3h ends with the Heist finale, acts-v3t with Tekahn and the hero relay (the relay heroes are
//      the Tombs explorers other than the run's own)
//   P  the powers the client marks AI_MUST_PLAY (the wiki's Auto-cast): each is cast by rule (never an option of the boss's
//      AI) or its row's power note states that the AI chooses instead (the close-out review: Sinkhole, Colossus, Sazin, Ruby
//      and Marei Loom did neither)
//   L  legacy replay: runs saved before the close-out flipped the defaults (the two version-5 golden saves and eight version-6
//      saves of new runs on the then default acts-v2) load and replay step for step as they did on the pre-close-out tree
//      (verify/fixtures/p8f25/replay.txt, recorded there by verify/tools/gen/p8f25/record_replay.sh)
//   L4 released-schedule replay (step 9, plan D5): eight acts-v3h/acts-v3t runs saved at the plan's L4 stops on the tree before
//      step 9 (f62275b; verify/fixtures/p9/replay-v3, recorded there by verify/tools/gen/p9/record_replay_v3.sh with the frozen
//      ReplayV3.cs) replay step for step as recorded, except lines that follow an offer a step-9 correctness fix changed: each
//      such offer is redrawn exactly as recorded once the exclusions f62275b lacked (its exclusions.txt) are lifted
//   R  the reachability sweep: hundreds of new runs on the default roll; meetings per adventure, and every boss row that can
//      never appear, each with its reason (tier, unlock, exclusion, finale-only)
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

string root = VerifyPaths.Data;
string R(string f) => File.ReadAllText(Path.Combine(root, f));
DungeonRunData Load() => DungeonRunData.Parse(R("dungeon-run/run-v1.json"), R("dungeon-run/pool-v1.json"), R("npc/decks-v1.json"), R("dungeon-run/catalog-v1.json"),
    R("dungeon-run/schedule-v1.json"), R("dungeon-run/tavern-v1.json"), R("dungeon-run/encounters-v1.json"));
var data = Load();
var run = new DungeonRun(data);
var all = new DungeonRunProfile(); all.Unlocked.AddRange(data.Unlocks.Keys);
string repo = VerifyPaths.Root;
var ledger = ReadLedger(Path.Combine(repo, "docs", "design", "restoration-ledger.csv"));
var rows = ledger.ToDictionary(r => r["card_id"], r => r, StringComparer.Ordinal);
string ledgerMd = File.ReadAllText(Path.Combine(repo, "docs", "design", "restoration-ledger.md"));
string curationText = File.ReadAllText(Path.Combine(repo, "tools", "restoration-ledger", "curation.json"));
using var curation = JsonDocument.Parse(curationText);
string Cls(string id) => rows.TryGetValue(id, out var r) ? r["classification"] : "(no row)";

// ============================================================ A. the family 25 ids
Guard("A", () =>
{
    var labels = Enumerable.Range(1, 11).SelectMany(n => new[] { $"GILA_Darius_{n:00}", $"GILA_Toki_{n:00}" }).ToList();
    var labelRows = labels.Select(id => rows[id]).ToList();
    Check("A1 the 22 Monster Hunt label cards (GILA_Darius_01-11, GILA_Toki_01-11) are support rows, each its bucket's label row (curation.json labelRecords)",
        labelRows.All(r => r["classification"] == "duplicate/support" && r["roles"] == "bucket label" && r["notes"].Contains("the client's label card of bucket Monster Hunt:")
            && r["owner"].EndsWith(" member cards (buckets column)")),
        string.Join(" ", labelRows.Where(r => r["classification"] != "duplicate/support" || r["roles"] != "bucket label").Select(r => r["card_id"] + "=" + r["classification"] + "/" + r["roles"])));
    Check("A1 no \"label:\" row stands beside them (the Darius Crowley and Toki buckets have one label row each: 11 + 11)",
        !rows.Keys.Any(k => k.StartsWith("label:Monster Hunt:Darius Crowley:", StringComparison.Ordinal) || k.StartsWith("label:Monster Hunt:Toki, Time-Tinker:", StringComparison.Ordinal)),
        string.Join(" ", rows.Keys.Where(k => k.StartsWith("label:Monster Hunt:D", StringComparison.Ordinal) || k.StartsWith("label:Monster Hunt:T", StringComparison.Ordinal)).Take(3)));
    var keepers = new[] { "DALA_BOSS_98h", "DALA_BOSS_99h", "ULDA_BOSS_99h" };
    Check("A2 the three tavern keepers (Bartendotron, Bartender Bob, Bazaar Bob) are portrait support rows of Bob's tavern, and no encounter row is one of them",
        keepers.All(id => Cls(id) == "duplicate/support" && rows[id]["roles"] == "portrait" && rows[id]["owner"].StartsWith("Bob's tavern") && rows[id]["notes"].Contains("tavern keeper"))
        && data.Bosses.All(b => !keepers.Contains(b.HeroCard)), string.Join(" ", keepers.Select(id => id + "=" + Cls(id) + "/" + rows[id]["roles"])));
    var tokens = new[] { "DALA_740ts", "DALA_740ts2", "DALA_740ts3", "DALA_740ts4", "DALA_740ts5" };
    Check("A3 DALA_740 Wondrous Wisdomball stays rejected (a data gap: missing, ai:treasure-data-gap) and is no treasure a run is offered",
        Cls("DALA_740") == "missing" && rows["DALA_740"]["blocked_by"].Split(';').Contains("ai:treasure-data-gap") && rows["DALA_740"]["notes"].Contains("data-gap")
        && rows["DALA_740"]["player_availability"] == "not offered", rows["DALA_740"]["blocked_by"]);
    Check("A3 its five token spells DALA_740ts-ts5 are its support rows and inherit its rejection",
        tokens.All(id => Cls(id) == "duplicate/support" && rows[id]["owner"] == "DALA_740" && rows[id]["notes"].Contains("inherits DALA_740's rejection")),
        string.Join(" ", tokens.Select(id => id + "=" + Cls(id))));
    var kindle = data.Tavern.Option("ULDA_911"); var retrain = data.Tavern.Option("ULDA_038");
    Check("A4 ULDA_911 Kindle is enabled: the Tombs card of DALA_911's removeFromSample (alias, weight 0), implemented",
        kindle != null && kindle.AliasOf == "DALA_911" && kindle.Op == "removeFromSample" && kindle.Weight == 0 && Cls("ULDA_911") == "implemented"
        && rows["ULDA_911"]["player_availability"].StartsWith("offered:tavern"));
    Check("A5 ULDA_038 Explorer Retraining is a tavern option of encounter schedules (repickPower, runs encounters, weight 1), implemented",
        retrain != null && retrain.Op == "repickPower" && retrain.Runs == DungeonRunTreasure.RunsEncounters && retrain.Weight == 1 && Cls("ULDA_038") == "implemented");
    Check("A6 ULDA_607 House Special stays deferred: in tavern-v1.json deferred with its reason, no option, missing with data:bombastic-decks-absent",
        data.Tavern.Option("ULDA_607") == null && Cls("ULDA_607") == "missing" && rows["ULDA_607"]["blocked_by"].Split(';').Contains("data:bombastic-decks-absent")
        && R("dungeon-run/tavern-v1.json").Contains("\"id\": \"ULDA_607\", \"reason\": \"Bob's Bombastic Decks were server-side"));
    Check("A7 ULDA_111 Hearthstone, GILA_900p Temporal Loop and GILA_911 Rewind are phase 9 (missing, each with its F15 reason)",
        new[] { "ULDA_111", "GILA_900p", "GILA_911" }.All(id => Cls(id) == "missing" && rows[id]["notes"].Contains("phase 9")));
    // The runs-and-state review (rerun): a weight-0 alias is never drawn under its own id (DungeonRunTavern.Open keeps weight > 0),
    // so its row must say the run is offered the alias's target ("via <id>", the ledger's reprint convention), not the card itself.
    var aliases = data.Tavern.Options.Where(o => o.Weight <= 0 && o.AliasOf != null).ToList();
    var badAlias = aliases.Where(o => !(rows.TryGetValue(o.Id, out var r) && r["player_availability"] == $"offered:tavern tavern-v1.json {o.Op} via {o.AliasOf}"
        && r["notes"].Contains("never draws it under its own id"))).Select(o => o.Id + "=" + (rows.TryGetValue(o.Id, out var r2) ? r2["player_availability"] : "(no row)")).ToList();
    Check($"A8 the {aliases.Count} weight-0 tavern aliases (ULDA_911 Kindle, ULDA_912 Recruit) are offered only as their targets: each row reads \"via <target>\" and says it is never drawn under its own id",
        aliases.Count == 2 && badAlias.Count == 0, string.Join(" ", badAlias));
    // ... and a fight hero card the run player plays as its hero (Hagatha's GILA_600h2 and GILA_500h4) is recorded as the encounter's
    // card, not as a portrait variant of the hunter's row.
    var fightCards = data.Bosses.SelectMany(b => b.HeroCards.Select(h => (Boss: b, h.Hero))).ToList();
    var badFight = fightCards.Where(x => !(rows.TryGetValue(x.Hero, out var r) && r["owner"] == x.Boss.HeroCard
        && r["notes"].Contains("support: fight hero card of the encounter " + x.Boss.Id) && !r["notes"].Contains("hero portrait variant")
        && r["player_availability"].Contains("fight hero card:" + x.Boss.Id))).Select(x => x.Hero + "=" + (rows.TryGetValue(x.Hero, out var r2) ? r2["owner"] + "/" + r2["notes"] : "(no row)")).ToList();
    Check($"A9 the {fightCards.Count} fight hero cards (Hagatha's hunters' cards) are rows of their encounter: owned by its boss hero, noted as the fight hero card the run player plays, never a portrait variant",
        fightCards.Count == 2 && badFight.Count == 0, string.Join(" ", badFight));
});

// ============================================================ B. the rejections audit
Guard("B", () =>
{
    var open = ledger.Where(r => r["classification"] == "missing" || r["classification"] == "partial").ToList();
    var expected = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        // missing: rejections and deferrals, each recorded by its family
        ["DALA_501"] = "missing", ["DALA_740"] = "missing", ["DALA_857"] = "missing", ["DALA_BOSS_61t"] = "missing", ["DALA_BOSS_75h"] = "missing",
        ["DALA_BOSS_75p"] = "missing", ["GILA_900p"] = "missing", ["GILA_911"] = "missing", ["GILA_BOSS_48t"] = "missing", ["ULDA_111"] = "missing",
        ["ULDA_607"] = "missing", ["ULDA_BOSS_58h"] = "missing", ["ULDA_BOSS_58p"] = "missing",
        // partial: scripted and admitted, reached by no shipped feature (the close-out's stated reasons)
        ["DALA_829"] = "partial", ["DALA_831"] = "partial", ["DALA_832"] = "partial", ["DALA_833"] = "partial", ["ULDA_801"] = "partial",
        ["ULDA_802"] = "partial", ["ULDA_803"] = "partial", ["ULDA_804"] = "partial", ["GILA_BOSS_43t"] = "partial", ["GILA_BOSS_99t"] = "partial",
    };
    Check("B1 the rows left missing (13) or partial (10) are exactly the pinned rejections, deferrals and unreached rows",
        open.Count == expected.Count && open.All(r => expected.TryGetValue(r["card_id"], out var c) && c == r["classification"]),
        string.Join(" ", open.Where(r => !expected.ContainsKey(r["card_id"])).Select(r => r["card_id"] + "=" + r["classification"]).Take(8)) + " / " + open.Count);
    // The blocker table of the ledger's Markdown: "| `key` | rows | only | collectible | what it needs |" rows. A key counts as
    // described only when its "What it needs" cell is not empty (the review found three permanent rejections left blank).
    var blockerRows = ledgerMd.Split('\n').Where(l => l.StartsWith("| `", StringComparison.Ordinal)).Select(l => l.TrimEnd().TrimEnd('|').Split('|'))
        .Where(c => c.Length >= 6 && c.Skip(2).Take(3).All(x => int.TryParse(x.Trim().Replace(",", ""), out _))).ToList();
    var described = new HashSet<string>(blockerRows.Where(c => string.Join("|", c.Skip(5)).Trim().Length > 0).Select(c => c[1].Trim().Trim('`')), StringComparer.Ordinal);
    var undescribed = open.SelectMany(r => r["blocked_by"].Split(';', StringSplitOptions.RemoveEmptyEntries).Where(k => !described.Contains(k)).Select(k => r["card_id"] + ":" + k)).ToList();
    Check("B2 each of them carries a blocker, and the ledger's blocker table describes every blocker it names (a non-empty \"What it needs\" cell)", open.All(r => r["blocked_by"].Length > 0) && undescribed.Count == 0,
        string.Join(" ", undescribed.Take(6)));
    Check("B2 each partial row names its own reason (run:twist-pools, run:hunt-reward, data:unused-in-adventure), not a generic capability",
        open.Where(r => r["classification"] == "partial").All(r => r["blocked_by"] == "run:twist-pools" || r["blocked_by"] == "run:hunt-reward" || r["blocked_by"] == "data:unused-in-adventure"),
        string.Join(" ", open.Where(r => r["classification"] == "partial").Select(r => r["card_id"] + "=" + r["blocked_by"])));
    // The rejections the families reported (HANDOFF 3.1 / 3.4 and the lane reports): the id, where the reason is recorded, a word of it.
    var reported = new (string Id, string Where, string Word)[]
    {
        ("DALA_BOSS_75h", "encounterRejections", "data gap"), ("DALA_BOSS_75p", "E18", "no text"), ("DALA_BOSS_61t", "E18", "unused"),
        ("DALA_501", "E18", "unused"), ("ULDA_BOSS_58h", "encounterRejections", "non-current player"), ("ULDA_BOSS_58p", "E21", "non-current player"),
        ("GILA_BOSS_48t", "encounterRejections", "phase 9"), ("GILA_900h2", "encounterRejections", "phase 9"), ("DALA_857", "E05", "treasure"),
        ("DALA_740", "E25", "data-gap"), ("ULDA_607", "E25", "Bombastic"), ("ULDA_111", "E25", "phase 9"), ("GILA_900p", "E25", "phase 9"), ("GILA_911", "E25", "phase 9"),
        ("TRLA_209h", "F14", "NEUTRAL"), ("LOOTA_BOSS_53h2", "-", "portrait"),
    };
    var families = curation.RootElement.GetProperty("landedFamilies").EnumerateArray().ToList();
    bool InFamily(string fam, string id) => families.Any(f => f.GetProperty("id").GetString() == fam && f.TryGetProperty("rejected", out var rej) && rej.TryGetProperty(id, out _));
    bool InRejections(string id) => curation.RootElement.GetProperty("encounterRejections").EnumerateArray()
        .Any(e => e.GetProperty("id").GetString() == id || e.TryGetProperty("power", out var p) && p.GetString() == id);
    var unrecorded = reported.Where(x => !(rows.TryGetValue(x.Id, out var r) && r["classification"] != "implemented" && r["classification"] != "partial"
        && (r["notes"].IndexOf(x.Word, StringComparison.OrdinalIgnoreCase) >= 0 || x.Where == "-" && r["roles"].Contains(x.Word))
        && (x.Where == "-" || (x.Where == "encounterRejections" ? InRejections(x.Id) : InFamily(x.Where, x.Id))))).Select(x => x.Id).ToList();
    Check("B3 every rejection the families reported is recorded: not implemented in the ledger, the reason in its notes, and in curation.json (the family's rejected list or encounterRejections)",
        unrecorded.Count == 0, string.Join(" ", unrecorded));
    Check("B4 Infinite Toki's deck leaves out Alternate Timeline and Enhance-a-matic (recorded adaptations)", data.Boss("gila_48_infinite_toki") is var toki
        && !data.BossDecks[toki.Deck].Contains("GILA_BOSS_48t") && R("dungeon-run/encounters-v1.json").Contains("Enhance-a-matic"));
    var f14 = families.Single(f => f.GetProperty("id").GetString() == "F14");
    Check("B5 the stale F14 \"rejected\" note for TRLA_065p is gone (Steady Throw is Zul'jin's scripted power, implemented)",
        !f14.GetProperty("rejected").TryGetProperty("TRLA_065p", out _) && Cls("TRLA_065p") == "implemented");
    var e25 = families.SingleOrDefault(f => f.GetProperty("id").GetString() == "E25");
    Check("B6 curation.json lands family E25 with its ids, support records and rejections", e25.ValueKind == JsonValueKind.Object
        && e25.GetProperty("ids").EnumerateArray().Select(x => x.GetString()).OrderBy(x => x).SequenceEqual(new[] { "DALA_740", "GILA_900p", "GILA_911", "ULDA_038", "ULDA_111", "ULDA_607", "ULDA_911" })
        && e25.GetProperty("supportRecords").GetArrayLength() == 30 && e25.GetProperty("rejected").EnumerateObject().Count() == 5);
    // The runs-and-state review (rerun): an admitted twist no default schedule can roll (an act whose pool holds it at a chance
    // above 0) is not eligible anywhere in the mode, so the completion bar is open for it. Each must be partial with run:twist-pools,
    // and HANDOFF.md section 3.3 must hand it to step 9 by id (the close-out left that list without the act I-III twist pools).
    // The count 8 is pinned with B1's partial rows (the set cannot come out empty by a wrong read); when step 9 fills the pools,
    // it moves both pins with the ledger, and the per-id rule (partial, run:twist-pools, named in 3.3) stays as it is.
    var rolled = new HashSet<string>(data.Schedules.Defaults.SelectMany(s => data.Schedules.Schedules[s].Acts.Where(a => a.TwistChance > 0).SelectMany(a => a.TwistPool)), StringComparer.Ordinal);
    var idle = data.Schedules.Rules.Where(r => r.Kind == DungeonRunGlobalRule.KindTwist && r.Admitted && !rolled.Contains(r.Id)).Select(r => r.Id).OrderBy(x => x, StringComparer.Ordinal).ToList();
    string handoff = File.ReadAllText(Path.Combine(repo, "HANDOFF.md"));
    int s33 = handoff.IndexOf("\n### 3.3 ", StringComparison.Ordinal), s34 = handoff.IndexOf("\n### 3.4 ", StringComparison.Ordinal);
    string then = s33 >= 0 && s34 > s33 ? handoff.Substring(s33, s34 - s33) : "";
    var unowned = idle.Where(id => !(Cls(id) == "partial" && rows[id]["blocked_by"] == "run:twist-pools" && then.Contains(id))).ToList();
    Check($"B7 the {idle.Count} admitted twists no default schedule rolls (DALA_829, 831-833, ULDA_801-804) are partial with run:twist-pools, and HANDOFF 3.3 hands each to step 9 by id",
        idle.Count == 8 && unowned.Count == 0 && then.Contains("twist pool"), string.Join(" ", unowned) + (then.Length == 0 ? " (no section 3.3)" : ""));
    var counts = ledger.GroupBy(r => r["classification"]).OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => g.Key + " " + g.Count());
    Note("B ledger: " + ledger.Count + " rows; " + string.Join(", ", counts));
});

// Walks a run until stop holds: every fight won, first offers, anomaly nodes none, taverns left as they are.
DungeonRunState WalkUntil(DungeonRun r, DungeonRunState s, Func<DungeonRunState, bool> stop)
{
    var scratch = new DungeonRunProfile();
    for (int guard = 0; guard < 400 && !stop(s) && !s.Finished; guard++)
    {
        switch (s.Phase)
        {
            case DungeonRunPhase.PreMatch: r.BeginMatch(s); r.RecordOutcome(s, scratch, DungeonRunOutcome.Win); break;
            case DungeonRunPhase.TreasureDraft: r.PickTreasure(s, s.OpenDraft.Offered[0]); break;
            case DungeonRunPhase.Modifier:
                r.PickModifier(s, s.ModifierOffer.Kind == DungeonRunGlobalRule.KindAnomaly ? DungeonRunGlobalRule.None : s.ModifierOffer.Offered[0]); break;
            case DungeonRunPhase.Tavern: r.LeaveTavern(s); break;
            case DungeonRunPhase.BundleDraft:
                if (s.OpenDraft.Takes == 0) { r.PickBundle(s, s.OpenDraft.Offered[0]); break; }
                string bundle = s.OpenDraft.Choosing ?? s.OpenDraft.Offered[0];
                var left = r.OfferCards(s.OpenDraft, bundle);
                foreach (string kept in s.OpenDraft.Chosen) left.Remove(kept);
                r.PickDraftCard(s, bundle, left[0]);
                break;
            default: throw new InvalidOperationException("walk stopped at " + s.Phase);
        }
    }
    return s;
}
DungeonRunState Reach(DungeonRunState s, int floor) => WalkUntil(run, s, x => x.Floor == floor && x.Phase == DungeonRunPhase.PreMatch);
bool AtTavern(DungeonRunState s) => s.Phase == DungeonRunPhase.Tavern;
DungeonRunState New(string row, long seed, DungeonRunProfile profile = null, string schedule = null)
{
    var cls = data.FindClass(row);
    profile ??= all;
    string deck = cls.Decks.First(d => run.IsUnlocked(profile, data.FindDeck(d).Unlock));
    return run.NewRun(profile, cls.Id, deck, cls.Powers[0], seed, schedule: schedule, signatureId: run.SignatureOffer(profile, cls, seed).FirstOrDefault());
}

// ============================================================ C. Explorer Retraining on new runs
Guard("C", () =>
{
    var mage = New("MAGE", 11, schedule: "acts-v3h");
    var fresh = New("MAGE", 11, new DungeonRunProfile(), schedule: "acts-v3h");
    var v2 = New("MAGE", 11, schedule: "acts-v2");
    var reno = New("MAGE_ROGUE", 12, schedule: "acts-v3t");
    Check("C1 a new run of an encounter schedule records the starting powers its row offered (MAGE with every unlock: the three, in row order)",
        mage.ScheduleVersion == 3 && mage.PowerChoices == "CS2_034 carto_survey mage_rimebite" && run.RetrainPowers(mage).SequenceEqual(new[] { "carto_survey", "mage_rimebite" }),
        mage.PowerChoices ?? "(none)");
    Check("C2 none is recorded with one open power (a fresh profile) or on a schedule of stamp 2 (acts-v2), so a saved older run writes nothing new",
        fresh.PowerChoices == null && v2.ScheduleVersion == 2 && v2.PowerChoices == null && run.RetrainPowers(v2).Count == 0
        && !Replay.Xml(new DungeonRunSave { Profile = all, Active = v2 }).Contains("PowerChoices"));
    Check("C3 a hero row records its own powers (Reno's row on acts-v3t)", reno.PowerChoices == string.Join(" ", data.FindClass("MAGE_ROGUE").Powers.Distinct()), reno.PowerChoices ?? "(none)");
    // Over 300 new runs on the default roll, the first tavern offers ULDA_038 in some; acts-v2 runs (the older default) never.
    int offered = 0, opened = 0, v2Offered = 0, v2Opened = 0, v2Same = 0;
    var without = Load();
    without.Tavern.Options.RemoveAll(o => o.Id == "ULDA_038");
    var runWithout = new DungeonRun(without);
    for (int seed = 1; seed <= 300; seed++)
    {
        string row = seed % 3 == 0 ? "WARRIOR" : seed % 3 == 1 ? "MAGE" : "PRIEST";
        var s = WalkUntil(run, New(row, 5000 + seed), AtTavern);
        if (AtTavern(s)) { opened++; if (s.Tavern.Offers.Contains("ULDA_038")) offered++; }
        var o = WalkUntil(run, New(row, 5000 + seed, schedule: "acts-v2"), AtTavern);
        // The same walk with the option removed from the table: an older schedule's tavern is the one it was before.
        var w = WalkUntil(runWithout, runWithout.NewRun(all, o.Class, o.StarterDeck, o.Power, 5000 + seed, schedule: "acts-v2",
            signatureId: run.SignatureOffer(all, data.FindClass(row), 5000 + seed).FirstOrDefault()), AtTavern);
        if (!AtTavern(o)) continue;
        v2Opened++;
        if (o.Tavern.Offers.Contains("ULDA_038")) v2Offered++;
        if (w.Phase == DungeonRunPhase.Tavern && w.Tavern.Offers.SequenceEqual(o.Tavern.Offers) && w.Tavern.Bob.SequenceEqual(o.Tavern.Bob)) v2Same++;
    }
    Check("C4 300 new runs on the default roll reach a tavern; some of those taverns offer ULDA_038", opened == 300 && offered > 0, $"{offered} of {opened}");
    Check("C5 the same seeds on acts-v2: no tavern offers ULDA_038, and each offers what it offers with the option removed from the table", v2Opened == 300 && v2Offered == 0 && v2Same == v2Opened,
        $"{v2Offered} offered, {v2Same} of {v2Opened} the same");
    Note($"C ULDA_038 in {offered} of {opened} first taverns of default-roll runs");
});

// ============================================================ D. the shipped finales
Guard("D", () =>
{
    var heist = Reach(New("PALADIN", 21, schedule: "acts-v3h"), 16);
    var plan = run.BuildPlan(heist, all);
    Check("D1 an acts-v3h run meets the Heist finale on floors 16-19 (Norroa first, after the finale's tavern), The Finale active in act IV",
        heist.BossSequence.Skip(15).SequenceEqual(new[] { "dala_43_norroa", "dala_67_vargoth", "dala_69_kalec", "dala_60_khadgar" }) && plan.EncounterId == "dala_43_norroa"
        && run.CurrentAct(heist).IsFinale && run.RuleActive(heist, "DALA_830") && plan.PlayerRelay.Count == 0);
    var tek = Reach(New("PALADIN", 22, schedule: "acts-v3t"), 16);
    var tplan = run.BuildPlan(tek, all);
    var explorers = new[] { "ULDA_Reno", "ULDA_Elise", "ULDA_Brann", "ULDA_Finley" };
    // The relay lists every explorer with each of its three client finale decks (the fight takes one per hero, seeded).
    string Heroes(SabberStoneCore.CardSets.DungeonRun.RunMatchPlan p) => string.Join(",", p.PlayerRelay.Select(h => h.Hero.Id).Distinct());
    Check("D2 an acts-v3t run meets Tekahn on floor 16 with the hero relay: the four explorers (three finale decks each) after the run's own hero",
        tek.BossSequence.Count == 16 && tek.BossSequence.Last() == "ulda_67_tekahn" && tplan.EncounterId == "ulda_67_tekahn"
        && tplan.PlayerRelay.Select(h => h.Hero.Id).Distinct().OrderBy(x => x).SequenceEqual(explorers.OrderBy(x => x)) && tplan.PlayerRelay.Count == 12
        && tplan.PlayerRelay.GroupBy(h => h.Hero.Id).All(g => g.Count() == 3 && g.All(h => h.Health == 50)), Heroes(tplan) + " / " + tplan.PlayerRelay.Count);
    var renoRun = Reach(New("MAGE_ROGUE", 23, schedule: "acts-v3t"), 16);
    var rplan = run.BuildPlan(renoRun, all);
    Check("D3 an explorer's own run relays the other three (Reno's row: Elise, Finley, Brann)", Heroes(rplan) == "ULDA_Elise,ULDA_Finley,ULDA_Brann" && rplan.PlayerRelay.Count == 9,
        Heroes(rplan));
});

// ============================================================ P. the wiki's Auto-cast powers
Guard("P", () =>
{
    // A power whose client card carries AI_MUST_PLAY (tag 367 = 1; the wiki's Hero Power tables mark the same powers "Auto-cast")
    // is either cast by rule (registered with the passive powers through family 09's AutoCast: never an option of the boss's
    // AI) or its row's "power" adaptation note says the wiki's auto-cast is not followed and why. Every encounter row's power
    // and each form's power are checked.
    var powerNotes = curation.RootElement.GetProperty("encounterAdaptations").EnumerateArray()
        .Where(a => a.GetProperty("kind").GetString() == "power").GroupBy(a => a.GetProperty("id").GetString())
        .ToDictionary(g => g.Key, g => string.Join(" ", g.Select(a => a.GetProperty("note").GetString())), StringComparer.Ordinal);
    var mustPlay = new List<(string Row, string Power)>();
    foreach (var b in data.Encounters)
        foreach (string power in new[] { b.Power }.Concat(b.Forms.Select(f => f.Power)).Where(x => x != null).Distinct())
            if (SabberStoneCore.Model.Cards.FromId(power)[SabberStoneCore.Enums.GameTag.AI_MUST_PLAY] == 1) mustPlay.Add((b.Id, power));
    bool RuleCast(string power) => SabberStoneCore.CardSets.DungeonRun.DungeonRunEncounters.PassivePowers.Contains(power);
    bool Noted(string row) => powerNotes.TryGetValue(row, out var n) && n.IndexOf("auto-cast", StringComparison.OrdinalIgnoreCase) >= 0;
    var silent = mustPlay.Where(x => !RuleCast(x.Power) && !Noted(x.Row)).Select(x => x.Row + ":" + x.Power).ToList();
    Check("P1 every AI_MUST_PLAY encounter power is cast by rule or its row's power note says why the AI chooses instead (" + mustPlay.Count + " powers, "
        + mustPlay.Count(x => RuleCast(x.Power)) + " cast by rule)", mustPlay.Count > 30 && silent.Count == 0, string.Join(" ", silent));
    var reviewed = new[] { "ULDA_BOSS_08p", "ULDA_BOSS_22p", "ULDA_BOSS_28p", "ULDA_BOSS_33p", "DALA_BOSS_66p" };
    Check("P2 the five the close-out review moved to a rule cast (Sand Surge, Power of Wrath, Mighty Windstorm, Out of Control, Next...) are cast by rule and their notes say so",
        reviewed.All(p => mustPlay.Any(x => x.Power == p) && RuleCast(p) && SabberStoneCore.CardSets.DungeonRun.DungeonRunEncounters.Find(p).Kind == SabberStoneCore.CardSets.DungeonRun.EncounterCardKind.Passive
            && Noted(mustPlay.First(x => x.Power == p).Row)),
        string.Join(" ", reviewed.Where(p => !RuleCast(p) || !mustPlay.Any(x => x.Power == p && Noted(x.Row)))));
});

// ============================================================ L. legacy replay
Guard("L", () =>
{
    string fixtures = VerifyPaths.Fixture("p8f25");
    var files = new List<string> { Path.Combine(VerifyPaths.Fixture("p8f01"), "golden-save-legacy-v5.xml"), Path.Combine(VerifyPaths.Fixture("p8f01"), "golden-save-acts-v5.xml") };
    files.AddRange(Enumerable.Range(1, Replay.Starts.Length).Select(i => Path.Combine(fixtures, $"pre-closeout-{i}.xml")));
    var lines = new List<string>();
    foreach (string f in files) lines.AddRange(Replay.Walk(run, f));
    if (Environment.GetEnvironmentVariable("C_ENC25_REPLAY") is string dump && dump.Length > 0) File.WriteAllText(dump, string.Join("\n", lines) + "\n", new UTF8Encoding(false));
    var expected = File.ReadAllLines(Path.Combine(fixtures, "replay.txt"));
    int differ = Enumerable.Range(0, Math.Max(lines.Count, expected.Length)).Count(i => i >= lines.Count || i >= expected.Length || lines[i] != expected[i]);
    int first = Enumerable.Range(0, Math.Max(lines.Count, expected.Length)).FirstOrDefault(i => i >= lines.Count || i >= expected.Length || lines[i] != expected[i]);
    Check($"L1 {files.Count} runs saved before the close-out (2 version-5 golden saves, {Replay.Starts.Length} acts-v2 runs of the then default) load and replay to their end step for step as on the pre-close-out tree ({expected.Length} lines, the state's digest after every step)",
        differ == 0 && expected.Length > 0, differ + " lines differ" + (differ > 0 ? "; first at " + first + ": " + (first < lines.Count ? lines[first] : "(none)") : ""));
    var starts = files.Skip(2).Select(Replay.Read).ToList();
    Check("L2 the eight version-6 saves are runs of the then default schedule, acts-v2 (stamp 2), with no recorded power choices",
        starts.All(x => x.Version == 6 && x.Active.Schedule == "acts-v2" && x.Active.ScheduleVersion == 2 && x.Active.PowerChoices == null)
        && starts.Select(x => x.Active.Class).SequenceEqual(Replay.Starts.Select(x => x.Row)));
    Check("L3 every replayed run ends in victory (the policy wins every fight)", lines.Where(l => l.StartsWith("end ", StringComparison.Ordinal)).All(l => l.StartsWith("end Victory", StringComparison.Ordinal))
        && lines.Count(l => l.StartsWith("end ", StringComparison.Ordinal)) == files.Count);
});

// ============================================================ L4. released-schedule replay (step 9)
Guard("L4", () =>
{
    string fixtures = VerifyPaths.Fixture("p9/replay-v3");
    int n = ReplayV3.Stops.Length;
    var files = Enumerable.Range(0, n).Select(i => Path.Combine(fixtures, ReplayV3.FileName(i))).ToList();
    // The recording, split per save (each replay starts with its "save" line).
    var expected = File.ReadAllLines(Path.Combine(fixtures, "replay.txt"));
    var recorded = new List<List<string>>();
    foreach (string line in expected) { if (line.StartsWith("save ", StringComparison.Ordinal)) recorded.Add(new List<string>()); recorded[recorded.Count - 1].Add(line); }
    // f62275b's own exclusion data (written by the recorder from that tree's parse): what the released schedules already excluded.
    var oldRules = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
    var oldBosses = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
    foreach (string line in File.ReadAllLines(Path.Combine(fixtures, "exclusions.txt")))
    {
        var t = line.Split(' ');
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int k = 2; k + 1 < t.Length; k += 2) fields[t[k]] = t[k + 1];
        (t[0] == "rule" ? oldRules : oldBosses)[t[1]] = fields;
    }
    bool OldHas(Dictionary<string, Dictionary<string, string>> table, string id, string key, string value) =>
        table.TryGetValue(id, out var f) && f.TryGetValue(key, out var list) && list.Split(',').Contains(value);

    var schedules = data.Schedules;
    List<DungeonRunBoss> BossesOfAct(DungeonRunState st, DungeonRunSchedule sc, int act) =>
        sc.Nodes.Where(nd => nd.Act == act && nd.IsFight && nd.Floor >= 1 && nd.Floor <= st.BossSequence.Count)
            .Select(nd => data.Bosses.FirstOrDefault(b => b.Id == st.BossSequence[nd.Floor - 1])).Where(b => b != null).ToList();
    // Why the current tree leaves rule r out of act `act`'s candidates (RuleCandidates' conditions, in its order), each cause
    // marked new when the f62275b data did not have it (a step-9 correctness fix: lane R's exclusions, flags, rule keys, and the
    // run-scope check against every act).
    List<(string Cause, bool New)> RuleCauses(DungeonRunState st, DungeonRunSchedule sc, int act, DungeonRunGlobalRule r)
    {
        var causes = new List<(string, bool)>();
        var active = st.ActiveRules.Where(x => x.Act == 0 || x.Act == act).ToList();
        var ids = new HashSet<string>((r.RunScope ? st.ActiveRules : active).Select(x => x.Id), StringComparer.Ordinal);
        var actIds = new HashSet<string>(active.Select(x => x.Id), StringComparer.Ordinal);
        var rulesNow = (r.RunScope ? st.ActiveRules : active).Where(x => x.Kind != null).Select(x => schedules.Rule(x.Id)).Where(x => x != null).ToList();
        var bosses = r.RunScope ? Enumerable.Range(1, sc.Acts.Count).SelectMany(a => BossesOfAct(st, sc, a)).ToList() : BossesOfAct(st, sc, act);
        var actBosses = new HashSet<string>(BossesOfAct(st, sc, act).Select(b => b.Id), StringComparer.Ordinal);
        if (r.RunScope && act != 1) causes.Add(("run scope, act " + act, false));
        if (ids.Contains(r.Id)) causes.Add(("already active", false));
        if (r.Effect == DungeonRunGlobalRule.EffectMatch && !SabberStoneCore.CardSets.DungeonRun.DungeonRunGlobalRules.IsScripted(r.Id)) causes.Add(("not scripted", false));
        if (!DungeonRunAdmission.Classify(null, r.Id, AdmitScope.Rule, null, st.IsCatalogRun).Admitted) causes.Add(("not admitted", false));
        if (r.NoBoardShrine && st.HasBoardShrine) causes.Add(("noBoardShrine", !OldHas(oldRules, r.Id, "noBoardShrine", "True")));
        if (r.IncompatibleClasses.Contains(st.Class)) causes.Add(("class " + st.Class, !OldHas(oldRules, r.Id, "classes", st.Class)));
        foreach (string t in r.IncompatibleTreasures.Where(t => DungeonRun.HoldsTreasure(st, t))) causes.Add(("treasure " + t + " (incompatibleTreasures)", true));
        if (r.NoOpeningFlags && st.Cards.Any(c => c.Opening)) causes.Add(("an opening-flag card (noOpeningFlags)", true));
        foreach (var other in rulesNow.Where(o => r.IncompatibleRules.Contains(o.Id) || o.IncompatibleRules.Contains(r.Id)))
            causes.Add(("rule " + other.Id, !(OldHas(oldRules, r.Id, "rules", other.Id) || OldHas(oldRules, other.Id, "rules", r.Id)) || !actIds.Contains(other.Id)));
        foreach (string m in r.IncompatibleModifiers.Where(ids.Contains)) causes.Add(("modifier " + m, !OldHas(oldRules, r.Id, "modifiers", m) || !actIds.Contains(m)));
        foreach (var b in bosses)
        {
            bool cross = !actBosses.Contains(b.Id);
            if (b.IncompatibleRules.Contains(r.Id)) causes.Add(("boss " + b.Id + " incompatibleRules", cross || !OldHas(oldBosses, b.Id, "rules", r.Id)));
            foreach (string f in b.Flags.Where(r.IncompatibleFlags.Contains))
                causes.Add(("boss " + b.Id + " flag " + f + (cross ? " (another act: the run-scope check)" : ""), cross || !OldHas(oldRules, r.Id, "flags", f) || !OldHas(oldBosses, b.Id, "flags", f)));
        }
        return causes;
    }
    // The modifiers the current tree bans on the act (ModifierCandidates' sources), each cause marked new as above.
    List<(string Cause, bool New)> ModifierCauses(DungeonRunState st, string m)
    {
        var causes = new List<(string, bool)>();
        var node = run.CurrentNode(st);
        var sc = run.ScheduleFor(st);
        if (st.ActiveRules.Any(x => x.Act == node.Act && x.Id == m)) causes.Add(("already active", false));
        foreach (var b in BossesOfAct(st, sc, node.Act).Where(b => DungeonRun.ActWideModifierBans(st, b).Contains(m)))
            causes.Add(("boss " + b.Id + " incompatibleModifiers", !OldHas(oldBosses, b.Id, "modifiers", m)));
        foreach (var r in run.ActiveGlobalRules(st).Where(r => r.IncompatibleModifiers.Contains(m)))
            causes.Add(("rule " + r.Id + " incompatibleModifiers", !OldHas(oldRules, r.Id, "modifiers", m)));
        return causes;
    }
    // OpenAnomaly's and OpenModifier's draws over a candidate list (the same streams and order).
    List<string> DrawAnomaly(DungeonRunState st, List<DungeonRunGlobalRule> candidates)
    {
        var left = candidates.ToList(); var offer = new List<string>();
        for (int k = 0; k < DungeonRun.AnomalyOptions && left.Count > 0; k++)
        {
            int roll = StableSeed.Index(StableSeed.Derive(st.Seed, DungeonRun.AnomalyStream, st.Node, k), left.Sum(r => Math.Max(1, r.Weight)));
            var picked = left[left.Count - 1];
            foreach (var r in left) { roll -= Math.Max(1, r.Weight); if (roll < 0) { picked = r; break; } }
            offer.Add(picked.Id); left.Remove(picked);
        }
        offer.Add(DungeonRunGlobalRule.None);
        return offer;
    }
    List<string> DrawModifier(DungeonRunState st, List<DungeonRunModifier> candidates)
    {
        var left = candidates.ToList(); var offer = new List<string>();
        for (int k = 0; k < DungeonRun.ModifierOptions && left.Count > 0; k++)
        {
            var picked = left[StableSeed.Index(StableSeed.Derive(st.Seed, DungeonRun.ModifierStream, st.Node, k), left.Count)];
            offer.Add(picked.Id); left.Remove(picked);
        }
        return offer;
    }
    // Attributes a moved offer: lifting every exclusion the current tree has and f62275b had not reproduces the recorded offer.
    string Attribute(DungeonRunState st, string kind, List<string> recordedOffer, out bool ok)
    {
        ok = false;
        var sc = run.ScheduleFor(st);
        int act = run.CurrentNode(st).Act;
        var lifted = new List<string>();
        List<string> redraw;
        if (kind == "anomaly")
        {
            var now = run.RuleCandidates(st, sc, act, DungeonRunGlobalRule.KindAnomaly);
            var before = new List<DungeonRunGlobalRule>();
            foreach (var r in schedules.Rules.Where(r => r.Kind == DungeonRunGlobalRule.KindAnomaly && r.Admitted))
            {
                if (now.Contains(r)) { before.Add(r); continue; }
                var causes = RuleCauses(st, sc, act, r);
                if (causes.Count > 0 && causes.All(c => c.New)) { before.Add(r); lifted.Add(r.Id + " [" + string.Join("; ", causes.Select(c => c.Cause)) + "]"); }
            }
            redraw = DrawAnomaly(st, before);
        }
        else if (kind == "modifier")
        {
            var now = run.ModifierCandidates(st);
            var before = new List<DungeonRunModifier>();
            foreach (var m in schedules.Modifiers.OrderBy(m => m.Id, StringComparer.Ordinal))
            {
                if (now.Contains(m)) { before.Add(m); continue; }
                var causes = ModifierCauses(st, m.Id);
                if (causes.Count > 0 && causes.All(c => c.New)) { before.Add(m); lifted.Add(m.Id + " [" + string.Join("; ", causes.Select(c => c.Cause)) + "]"); }
            }
            redraw = DrawModifier(st, before);
        }
        else return "a moved " + kind + " line: no step-9 attribution for it";
        ok = lifted.Count > 0 && redraw.SequenceEqual(recordedOffer);
        return "lifted " + string.Join(", ", lifted) + " -> redraw " + string.Join(",", redraw) + (ok ? " = recorded" : " != recorded " + string.Join(",", recordedOffer));
    }
    string Seen(string line) { int k = line.IndexOf(" => ", StringComparison.Ordinal); return k < 0 ? line : line.Substring(0, k); }
    string Kind(string line) => line.Split(' ')[0];
    List<string> OfferOf(string line) => Seen(line).Split(' ').ElementAtOrDefault(1)?.Split(',').ToList() ?? new List<string>();

    var lines = new List<string>();
    int movedLines = 0, attributed = 0;
    var moved = new List<string>(); var unattributed = new List<string>(); var headers = new List<string>();
    for (int i = 0; i < n; i++)
    {
        var pre = new Dictionary<int, DungeonRunSave>();
        var cur = ReplayV3.Walk(run, files[i], (k, sv) => pre[k] = DungeonRunStore.Clone(sv));
        lines.AddRange(cur);
        var exp = i < recorded.Count ? recorded[i] : new List<string>();
        if (cur[0] != exp.ElementAtOrDefault(0)) headers.Add(ReplayV3.FileName(i));
        int d = Enumerable.Range(0, Math.Max(cur.Count, exp.Count)).FirstOrDefault(k => k >= cur.Count || k >= exp.Count || cur[k] != exp[k], -1);
        if (d < 0) continue;
        int count = Enumerable.Range(0, Math.Max(cur.Count, exp.Count)).Count(k => k >= cur.Count || k >= exp.Count || cur[k] != exp[k]);
        movedLines += count;
        string at = $"{ReplayV3.FileName(i)} line {d} ({count} of {exp.Count} lines move from it)";
        // The first moved line is a step that did the same as recorded but whose resulting state differs: the node it entered
        // made a different offer (the next line), which a step-9 exclusion explains.
        bool sameStep = d > 0 && d + 1 < cur.Count && d + 1 < exp.Count && Seen(cur[d]) == Seen(exp[d]) && Kind(cur[d + 1]) == Kind(exp[d + 1])
            && Seen(cur[d + 1]) != Seen(exp[d + 1]) && pre.ContainsKey(d + 1);
        if (!sameStep) { unattributed.Add(at + ": " + cur.ElementAtOrDefault(d) + " | recorded " + exp.ElementAtOrDefault(d)); continue; }
        string why = Attribute(pre[d + 1].Active, Kind(cur[d + 1]), OfferOf(exp[d + 1]), out bool ok);
        string entry = $"{at}: {Kind(exp[d + 1])} offer {string.Join(",", OfferOf(exp[d + 1]))} -> {string.Join(",", OfferOf(cur[d + 1]))}; {why}";
        if (ok) { attributed++; moved.Add(entry); } else unattributed.Add(entry);
    }
    if (Environment.GetEnvironmentVariable("C_ENC25_REPLAY_V3") is string dump && dump.Length > 0) File.WriteAllText(dump, string.Join("\n", lines) + "\n", new UTF8Encoding(false));
    foreach (string m in moved) Note("L4 moved: " + m);
    foreach (string m in unattributed) Note("L4 unattributed: " + m);
    var saves = files.Select(ReplayV3.Read).ToList();
    Check($"L4 the {n} saves recorded on f62275b (verify/tools/gen/p9/record_replay_v3.sh) are stamp-3 runs of acts-v3h and acts-v3t at their stops"
        + " (act I anomaly, a shrine run's act I anomaly holding a rule its board shrine excludes, the finale tavern, Hagatha's and the relay's pre-match,"
        + " a Plague Lord in the match, a modifier node, a tavern purchase waiting for its target), and each loads and resumes as it did there",
        saves.All(x => x.Version == 6 && x.Active.ScheduleVersion == 3) && saves.Select(x => x.Active.Schedule).SequenceEqual(ReplayV3.Stops.Select(x => x.Schedule))
        && saves.Select(x => x.Active.Class).SequenceEqual(ReplayV3.Stops.Select(x => x.Row)) && saves[5].Active.Phase == DungeonRunPhase.InMatch
        && saves[7].Active.Tavern?.Pending != null && saves[1].Active.HasBoardShrine && headers.Count == 0 && recorded.Count == n,
        headers.Count + " load lines differ: " + string.Join(" ", headers));
    Check($"L4 each replay equals the recording, or every line it moves follows one offer that lifting the step-9 exclusions (those f62275b's exclusions.txt lacks) redraws exactly as recorded"
        + $" ({movedLines} of {expected.Length} lines move, in {attributed} attributed and {unattributed.Count} unattributed replays)",
        unattributed.Count == 0 && lines.Count(l => l.StartsWith("save ", StringComparison.Ordinal)) == n, string.Join(" | ", unattributed));
    string shrine = recorded[1].FirstOrDefault(l => l.StartsWith("anomaly ", StringComparison.Ordinal));
    Check("L4 a stored offer is never redrawn: the shrine run's act I anomaly offer (" + Seen(shrine ?? "(none)") + "), made before the ordering fix, replays as recorded",
        shrine != null && lines.Contains(shrine) && OfferOf(shrine).Any(id => data.Schedules.Rule(id)?.NoBoardShrine == true));
    Check("L4 every replayed run ends in victory", lines.Count(l => l.StartsWith("end Victory", StringComparison.Ordinal)) == n);
});

// ============================================================ R. the reachability sweep
Guard("R", () =>
{
    const int Runs = 600;
    var rowsOfRuns = data.Classes.Where(c => c.Decks.Count > 0).ToList();
    var met = new Dictionary<string, int>(StringComparer.Ordinal); var metFresh = new Dictionary<string, int>(StringComparer.Ordinal);
    var bySchedule = new Dictionary<string, int>(StringComparer.Ordinal);
    var fresh = new DungeonRunProfile();
    var freshRows = data.Classes.Where(c => !c.IsHero && c.Decks.Any(d => run.IsUnlocked(fresh, data.FindDeck(d).Unlock)) && run.IsUnlocked(fresh, c.PowerUnlock(c.Powers[0]))).ToList();
    for (int i = 0; i < Runs; i++)
    {
        var s = New(rowsOfRuns[i % rowsOfRuns.Count].Id, 250000 + i);
        bySchedule[s.Schedule] = bySchedule.GetValueOrDefault(s.Schedule) + 1;
        foreach (string id in s.BossSequence) met[id] = met.GetValueOrDefault(id) + 1;
        var f = New(freshRows[i % freshRows.Count].Id, 250000 + i, new DungeonRunProfile());
        foreach (string id in f.BossSequence) metFresh[id] = metFresh.GetValueOrDefault(id) + 1;
    }
    var defaults = data.Schedules.Defaults.Select(data.Schedules.Find).ToList();
    bool InActPools(DungeonRunBoss b) => defaults.Any(sc => sc.Acts.Any(a => !a.IsFinale && (a.Combat.Any(c => data.Schedules.Member(b, c.Pool)) || data.Schedules.Member(b, a.MajorPool))));
    bool Fixed(DungeonRunBoss b) => defaults.Any(sc => sc.Acts.Any(a => a.IsFinale && a.FixedBosses.Contains(b.Id)));
    string Reason(DungeonRunBoss b)
    {
        if (b.Tier == DungeonRunEncounterData.FinaleTier && !Fixed(b)) return "finale-only (no default finale fixes it)";
        if (!InActPools(b) && !Fixed(b)) return "tier (no pool of a default schedule holds " + (b.IsEncounter ? "tier " + b.Tier : b.Final ? "a final" : b.Challenge ? "a challenge row" : "band " + b.Band) + ")";
        return null;
    }
    string Adventure(DungeonRunBoss b) => b.Provenance;
    var table = new List<string>();
    foreach (var g in data.Bosses.GroupBy(Adventure).OrderBy(g => g.Key, StringComparer.Ordinal))
        table.Add($"{g.Key}: {g.Count()} rows, {g.Count(b => met.ContainsKey(b.Id))} met, {g.Sum(b => met.GetValueOrDefault(b.Id))} meetings; fresh profile {g.Count(b => metFresh.ContainsKey(b.Id))} met");
    foreach (string line in table) Note("R " + line);
    Note("R schedules rolled: " + string.Join(", ", bySchedule.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => p.Key + " " + p.Value)));
    var never = data.Bosses.Where(b => !met.ContainsKey(b.Id)).ToList();
    foreach (var b in never) Note($"R never met: {b.Id} ({b.Provenance}): {Reason(b) ?? "(no structural reason)"}");
    Check($"R1 {Runs} new runs on the default roll (every run row in turn, every unlock held) follow both defaults", bySchedule.Count == 2 && bySchedule.Keys.All(data.Schedules.Defaults.Contains)
        && bySchedule.Values.All(n => n > Runs / 4), string.Join(" ", bySchedule.Select(p => p.Key + "=" + p.Value)));
    Check("R2 every adventure is met: each provenance's rows meet the run in hundreds of fights", data.Bosses.Select(Adventure).Distinct().All(p => data.Bosses.Where(b => b.Provenance == p).Sum(b => met.GetValueOrDefault(b.Id)) >= 100),
        string.Join("; ", table));
    var unexplained = never.Where(b => Reason(b) == null).Select(b => b.Id).ToList();
    Check("R3 every boss row a default schedule can draw is met at least once; every row never met has a structural reason", unexplained.Count == 0, string.Join(" ", unexplained));
    Check("R4 every encounter row (the five adventures) is met: no encounter row is left to a tier no pool holds, and every finale row is fixed by a default finale",
        data.Encounters.All(b => met.ContainsKey(b.Id)), string.Join(" ", data.Encounters.Where(b => !met.ContainsKey(b.Id)).Select(b => b.Id)));
    // Unlocks: with a fresh profile a row behind an unlock never appears; every such row is met once its unlock is held.
    var locked = data.Bosses.Where(b => b.Unlock != null && !run.IsUnlocked(fresh, b.Unlock)).ToList();
    Check("R5 with a fresh profile no row behind an unlock is met; each of them is met with every unlock held (reason: unlock)",
        locked.All(b => !metFresh.ContainsKey(b.Id)) && locked.Where(b => InActPools(b)).All(b => met.ContainsKey(b.Id)), locked.Count + " locked rows: " + string.Join(" ", locked.Take(6).Select(b => b.Id)));
    // Exclusions: a hero row of a boss's character never meets that row; other rows do.
    var excluded = data.Classes.Where(c => c.ExcludeBosses.Count > 0).ToList();
    int breaches = 0, metByOthers = 0;
    foreach (var c in excluded)
    {
        for (int i = 0; i < 100; i++) if (New(c.Id, 260000 + i).BossSequence.Any(c.ExcludeBosses.Contains)) breaches++;
        if (c.ExcludeBosses.All(met.ContainsKey)) metByOthers++;
    }
    Check($"R6 the {excluded.Count} hero rows with excludeBosses never meet their own character in 100 default-roll runs each (reason: exclusion), and other rows meet those bosses",
        breaches == 0 && metByOthers == excluded.Count, $"{breaches} breaches, {metByOthers} met by others");
    Note("R excluded: " + string.Join(" ", excluded.Select(c => c.Id + ":" + string.Join(",", c.ExcludeBosses))) + "; locked behind an unlock: " + locked.Count);
});

Console.WriteLine($"RESULT {passes} passed, {failures} failed");
return failures == 0 ? 0 : 1;

static List<Dictionary<string, string>> ReadLedger(string path)
{
    var result = new List<Dictionary<string, string>>();
    var records = new List<List<string>>();
    string text = File.ReadAllText(path);
    var field = new StringBuilder(); var record = new List<string>(); bool quoted = false;
    for (int i = 0; i < text.Length; i++)
    {
        char c = text[i];
        if (quoted)
        {
            if (c == '"' && i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
            else if (c == '"') quoted = false;
            else field.Append(c);
        }
        else if (c == '"') quoted = true;
        else if (c == ',') { record.Add(field.ToString()); field.Clear(); }
        else if (c == '\n') { record.Add(field.ToString().TrimEnd('\r')); field.Clear(); records.Add(record); record = new List<string>(); }
        else field.Append(c);
    }
    if (field.Length > 0 || record.Count > 0) { record.Add(field.ToString()); records.Add(record); }
    var header = records[0];
    foreach (var r in records.Skip(1).Where(r => r.Count == header.Count))
        result.Add(header.Select((h, k) => (h, k)).ToDictionary(x => x.h, x => r[x.k], StringComparer.Ordinal));
    return result;
}
