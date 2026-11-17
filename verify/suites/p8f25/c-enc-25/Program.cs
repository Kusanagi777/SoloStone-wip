using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

// c-enc-25: the phase-8 close-out (family 25), on the shipped data (encounters-v1.json loaded, as the plugin loads it).
//   A  the family 25 ids in the ledger: the 22 Monster Hunt label cards are their buckets' label rows, the three tavern keepers
//      are portraits of Bob's tavern, DALA_740 stays rejected and its five tokens inherit the reason, ULDA_911 Kindle and
//      ULDA_038 Explorer Retraining are tavern options, ULDA_607 stays deferred, ULDA_111 / GILA_900p / GILA_911 are phase 9
//   B  the rejections audit: every row left missing or partial is pinned with a stated blocker the ledger describes, and every
//      rejection the families reported is recorded (ledger, curation.json); the stale F14 TRLA_065p note is gone
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
    Check("P2 the five the review found (Sand Surge, Power of Wrath, Mighty Windstorm, Out of Control, Next...) are cast by rule and their notes say so",
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
