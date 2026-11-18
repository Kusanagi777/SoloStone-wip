using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using SabberStoneCore.CardSets.DungeonRun;

// c-rules-9: step 9 lane R, incompatibility prevention (handoff/phase9/plan.md section 1 C), on the run layer (no engine game).
//   V  C7: the validator's negatives for every new key (exclusionReasons, one-way and symmetric rule-rule lists, the treasure
//      and opening-flag keys, the v4 block's incompatibleModifiers and modifierReasons, "none"), each refused with its message,
//      and the shipped data and the fixture accepted
//   F  D17: ForbiddenPairs on constructed states: each kind of pair it reports, the same lines in either order, nothing on a
//      clean run, BuildPlan's log line, and RuleCandidates leaving out every rule the predicate would pair (F14: a fight-only
//      hero with no flag behind it, the gate check this suite added to RuleCandidates; F15: a run-scope rule against a later act,
//      on a data copy)
//   S  C2: the ordering fix: every Rumble shrine deck x 200 seeds on acts-v3h and acts-v4h, no stored act I anomaly offer and
//      no rolled twist carries noBoardShrine
//   T  C5: the twist roll: on the shipped defaults (2000 new runs) each of the eight act twists rolls in at least 15% of runs,
//      each act's roll rate is its chance less the measured exclusion loss, DALA_830 rolls on every acts-v4h finale; on the
//      fixture verify/fixtures/p9/schedule-twists9.json (all eight twists in every act, chances 70 / 85 / 55, DALA_829 weight 4)
//      the same with chances below 100, and the stamp-4 pick honours the weight where the released pick is uniform
//   W  C1 and C3: walks of every loadout on acts-v3h, acts-v3t, acts-v4h and acts-v4t to Victory (seeded picks, every tavern
//      offer bought): 0 forbidden pairs at any step, every offered rule, modifier and treasure stays clean when taken, and on
//      stamp 4 every modifier node offers at least one modifier and then "none"
//   K  C4: every fight knob of every modifier x every major, finale and fixedHealth row a stamp-4 run reaches: the knob moves the
//      built plan, or the row's v4 block neutralises the modifier with a reason, or no modifier can be active on that floor
// One argument: the fixture file (default verify/fixtures/p9/schedule-twists9.json, from verify/tools/gen/p9/make_twists9.py).
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
// C_RULES_9_ONLY=V,F,... runs only those sections (for the break-and-run proofs; unset: every section).
var only = Environment.GetEnvironmentVariable("C_RULES_9_ONLY") is string o && o.Length > 0 ? o.Split(',') : null;
void Guard(string name, Action body)
{
    if (only != null && !only.Contains(name)) return;
    try { body(); }
    catch (Exception e) { failures++; Console.WriteLine("FAIL " + name + " threw " + e.GetType().Name + ": " + e.Message.Split('\n')[0] + " @ " + (e.StackTrace ?? "").Split('\n').FirstOrDefault()?.Trim()); }
}
string Pct(int n, int d) => d == 0 ? "-" : (100.0 * n / d).ToString("F1", System.Globalization.CultureInfo.InvariantCulture);

string root = VerifyPaths.Data;
string R(string f) => File.ReadAllText(Path.Combine(root, f));
string fixturePath = args.Length > 0 ? args[0] : VerifyPaths.Fixture("p9/schedule-twists9.json");
DungeonRunData Load(string schedule = null) => DungeonRunData.Parse(R("dungeon-run/run-v1.json"), R("dungeon-run/pool-v1.json"), R("npc/decks-v1.json"),
    R("dungeon-run/catalog-v1.json"), schedule ?? R("dungeon-run/schedule-v1.json"), R("dungeon-run/tavern-v1.json"), R("dungeon-run/encounters-v1.json"));
DungeonRunProfile All(DungeonRunData d) { var p = new DungeonRunProfile(); p.Unlocked.AddRange(d.Unlocks.Keys); return p; }
var data = Load();
var run = new DungeonRun(data);
var all = All(data);
string[] actTwists = { "DALA_829", "DALA_831", "DALA_832", "DALA_833", "ULDA_801", "ULDA_802", "ULDA_803", "ULDA_804" };

// The loadouts: every hero row x every starter deck (the row's first power, its first signature offer).
List<(DungeonRunClass Row, string Deck)> Loadouts(DungeonRunData d) => d.Classes.SelectMany(c => c.Decks.Select(deck => (c, deck))).ToList();
DungeonRunState New(DungeonRun r, DungeonRunClass row, string deck, long seed, string schedule, int power = 0)
{
    var profile = All(r.Data);
    return r.NewRun(profile, row.Id, deck, row.Powers[power % row.Powers.Count], seed, schedule: schedule,
        signatureId: r.SignatureOffer(profile, row, seed).FirstOrDefault());
}
DungeonRunRule Rule(string id, int act, string kind) => new DungeonRunRule { Id = id, Act = act, FromNode = -1, Kind = kind };

// ============================================================ V. C7: the validator's negatives
Guard("V", () =>
{
    string Errors(DungeonRunData d) => string.Join(" | ", DungeonRunValidator.Validate(d, null));
    string Refused(Action<DungeonRunData> change) { var d = Load(); change(d); return Errors(d); }
    var shipped = DungeonRunValidator.Validate(data, null);
    Check("V1 the shipped data (scheduleVersion 4, the v4 schedules, every new key) validates", shipped.Count == 0 && data.Schedules.ScheduleVersion == 4, string.Join(" | ", shipped.Take(3)));
    var fixture = DungeonRunValidator.Validate(Load(File.ReadAllText(fixturePath)), null);
    Check("V2 the fixture schedule-twists9.json validates", fixture.Count == 0, string.Join(" | ", fixture.Take(3)));
    // exclusionReasons: every non-rule entry needs a reason from scheduleVersion 4; on a file below 4 none is required.
    string flagGone = Refused(d => d.Schedules.Rule("DALA_859").ExclusionReasons.Remove("listedDeck"));
    Check("V3 a flag exclusion without a reason is refused (DALA_859 listedDeck)", flagGone.Contains("rule DALA_859: exclusion listedDeck states no reason (exclusionReasons)"), flagGone);
    string modGone = Refused(d => d.Schedules.Rule("ULDA_715").ExclusionReasons.Remove("mod_open_road"));
    string classGone = Refused(d => d.Schedules.Rule("DALA_833").ExclusionReasons.Remove("GILA_600h"));
    string treasureGone = Refused(d => d.Schedules.Rule("ULDA_720").ExclusionReasons.Remove("LOOTA_800"));
    Check("V4 a modifier, class and treasure exclusion without a reason are refused", modGone.Contains("rule ULDA_715: exclusion mod_open_road states no reason")
        && classGone.Contains("rule DALA_833: exclusion GILA_600h states no reason") && treasureGone.Contains("rule ULDA_720: exclusion LOOTA_800 states no reason"), modGone + " || " + classGone + " || " + treasureGone);
    string below = Refused(d => { d.Schedules.ScheduleVersion = 3; d.Schedules.Rule("DALA_859").ExclusionReasons.Remove("listedDeck"); d.Schedules.Rule("ULDA_719").ExclusionReasons.Remove("DALA_831"); });
    Check("V5 below scheduleVersion 4 no reason is required (released files and fixtures keep validating)", !below.Contains("states no reason") && !below.Contains("one-way"), below);
    string stray = Refused(d => d.Schedules.Rule("ULDA_702").ExclusionReasons["boardStart"] = "not one of its exclusions");
    Check("V6 a reason for an entry the rule does not exclude is refused", stray.Contains("rule ULDA_702: exclusionReasons names boardStart, which is not one of its exclusions"), stray);
    // Rule-rule lists: symmetric (a reason on either side) or one-way (a reason on the listing side).
    string oneWay = Refused(d => d.Schedules.Rule("ULDA_719").ExclusionReasons.Remove("DALA_831"));
    Check("V7 a one-way rule exclusion without a reason is refused (ULDA_719 lists DALA_831, which does not list it back)", oneWay.Contains("rule ULDA_719: lists DALA_831 one-way (not listed back) with no reason declaring it (exclusionReasons)"), oneWay);
    Check("V8 the shipped one-way lists all state their reason (ULDA_709 x ULDA_803, DALA_829 x ULDA_706, ULDA_719 x DALA_831)", data.Schedules.Rule("ULDA_709").ExclusionReasons.ContainsKey("ULDA_803")
        && !data.Schedules.Rule("ULDA_803").IncompatibleRules.Contains("ULDA_709") && data.Schedules.Rule("DALA_829").ExclusionReasons.ContainsKey("ULDA_706")
        && data.Schedules.Rule("ULDA_719").ExclusionReasons.ContainsKey("DALA_831"));
    void Symmetric(DungeonRunData d) { d.Schedules.Rule("DALA_832").IncompatibleRules.Add("DALA_831"); d.Schedules.Rule("DALA_831").ExclusionReasons.Remove("DALA_832"); d.Schedules.Rule("DALA_832").ExclusionReasons.Remove("DALA_831"); }
    string both = Refused(Symmetric);
    string oneSide = Refused(d => { Symmetric(d); d.Schedules.Rule("DALA_832").ExclusionReasons["DALA_831"] = "a reason on one side covers the pair"; });
    Check("V9 a symmetric pair with no reason on either side is refused; a reason on one side is enough", (both.Contains("rule DALA_831: exclusion DALA_832 (listed on both rules) states no reason on either")
        || both.Contains("rule DALA_832: exclusion DALA_831 (listed on both rules) states no reason on either")) && !oneSide.Contains("DALA_831") && !oneSide.Contains("DALA_832"), both + " || " + oneSide);
    string unknownTreasure = Refused(d => { d.Schedules.Rule("ULDA_720").IncompatibleTreasures.Add("LOOTA_000"); d.Schedules.Rule("ULDA_720").ExclusionReasons["LOOTA_000"] = "x"; });
    string unknownFlag = Refused(d => { d.Schedules.Rule("ULDA_710").IncompatibleFlags.Add("sprinting"); d.Schedules.Rule("ULDA_710").ExclusionReasons["sprinting"] = "x"; });
    Check("V10 an unknown treasure or boss flag in an exclusion list is refused", unknownTreasure.Contains("rule ULDA_720: incompatibleTreasures names unknown treasure LOOTA_000")
        && unknownFlag.Contains("rule ULDA_710: unknown boss flag sprinting"), unknownTreasure + " || " + unknownFlag);
    Check("V11 the step-9 flags are known boss flags and are carried by their rows", new[] { "attackRedirect", "charge", "windfury", "statSwap", "fatigue", "handAdd", "manaIdentity", "room" }.All(f => DungeonRunEncounterData.Flags.Contains(f) && data.Bosses.Any(b => b.HasFlag(f)))
        && data.Bosses.Count(b => b.HasFlag("room")) == 5, string.Join(",", data.Bosses.Where(b => b.HasFlag("room")).Select(b => b.Id)));
    // The v4 block: known modifiers, a reason for each one a stamp-4 schedule can reach, no stray reason, encounter rows only.
    string unknownMod = Refused(d => d.Boss("loot_36_ixlid").V4.IncompatibleModifiers.Add("mod_nothing"));
    Check("V12 a v4 incompatibleModifiers entry naming an unknown modifier is refused", unknownMod.Contains("boss loot_36_ixlid: v4 incompatibleModifiers names unknown modifier mod_nothing"), unknownMod);
    string noReason = Refused(d => d.Boss("loot_36_ixlid").ModifierReasons.Remove("mod_iron_bosses"));
    Check("V13 a reachable row that neutralises a modifier with no reason is refused", noReason.Contains("boss loot_36_ixlid: v4 neutralises mod_iron_bosses with no reason (modifierReasons)"), noReason);
    string unreached = Refused(d =>
    {
        foreach (string id in d.Schedules.Schedules.Values.Where(x => x.StampVersion >= 4).Select(x => x.Id).ToList()) d.Schedules.Schedules.Remove(id);
        d.Boss("loot_36_ixlid").ModifierReasons.Remove("mod_iron_bosses");
    });
    Check("V14 with no stamp-4 schedule no row is reachable on v4, so no reason is required", !unreached.Contains("with no reason (modifierReasons)") && DungeonRunValidator.ReachableOnTuned(data).Contains("loot_36_ixlid"), unreached.Length > 300 ? unreached.Substring(0, 300) : unreached);
    string strayReason = Refused(d => d.Boss("gila_43_damph").ModifierReasons["mod_hot_start"] = "not one of its modifiers");
    Check("V15 a modifierReasons entry for a modifier the row lists nowhere is refused", strayReason.Contains("boss gila_43_damph: modifierReasons names mod_hot_start, which is not one of its incompatible modifiers"), strayReason);
    var customRow = Load();
    var custom = customRow.Bosses.First(b => !b.IsEncounter);
    custom.V4 = new DungeonRunBossV4();
    string customV4 = Errors(customRow);
    Check("V16 a v4 block on a custom (non-encounter) row is refused", customV4.Contains("boss " + custom.Id + " v4: only an encounter row has a v4 block"), customV4);
    string badReward = Refused(d => d.Boss("gila_43_damph").V4.Reward = "LOOTA_800");
    Check("V17 a v4 reward that is not a reward row of run-v1.json is refused", badReward.Contains("boss gila_43_damph v4: reward LOOTA_800 is not a reward row of run-v1.json"), badReward);
    string noneMod = Refused(d => d.Schedules.Modifiers.Add(new DungeonRunModifier { Id = DungeonRunGlobalRule.None, Name = "Nothing" }));
    string noneRule = Refused(d => d.Schedules.Rules.Add(new DungeonRunGlobalRule { Id = DungeonRunGlobalRule.None, Kind = "anomaly", Status = "rejected", Reason = "x" }));
    Check("V18 \"none\" (the offer's skip option) is refused as a modifier id and as a rule id", noneMod.Contains("modifier none: \"none\" is the offer's skip option") && noneRule.Contains("rule none: \"none\" is the offer's skip option"), noneMod + " || " + noneRule);
    Check("V19 every exclusion entry of every shipped rule states a reason (or is the reason's other side of a symmetric pair)", data.Schedules.Rules.All(r => r.ExclusionEntries.All(e => r.ExclusionReasons.ContainsKey(e)
        || r.IncompatibleRules.Contains(e) && (data.Schedules.Rule(e)?.ExclusionReasons.ContainsKey(r.Id) ?? false))),
        string.Join(",", data.Schedules.Rules.SelectMany(r => r.ExclusionEntries.Where(e => !r.ExclusionReasons.ContainsKey(e)).Select(e => r.Id + ">" + e))));
});

// ============================================================ F. D17: ForbiddenPairs on constructed states
Guard("F", () =>
{
    var plainClass = data.ClassRows.First(c => c.Id == "MAGE");
    DungeonRunState V4(long seed) => New(run, plainClass, plainClass.Decks[0], seed, "acts-v4h");
    // Custom band-1 bosses on act I (no flag, no rule of their own), and act I's rolled twist removed.
    var plain = data.Bosses.Where(b => !b.IsEncounter && b.Band == 1 && b.Unlock == null).ToList();
    DungeonRunState Clean(long seed)
    {
        var s = V4(seed);
        var schedule = run.ScheduleFor(s);
        foreach (var n in schedule.Nodes.Where(n => n.IsFight && n.Act == 1)) s.BossSequence[n.Floor - 1] = plain[(n.Floor - 1) % plain.Count].Id;
        s.ActiveRules.RemoveAll(r => r.Kind != null);
        return s;
    }
    var clean = Clean(70001);
    Check("F1 a run with no rule holds no forbidden pair", run.ForbiddenPairs(clean).Count == 0, string.Join(" | ", run.ForbiddenPairs(clean)));
    // A flag clash: ULDA_710 Ruthless Winds (no Charge) with a charge boss in its act.
    var charge = data.Bosses.First(b => b.HasFlag("charge"));
    var s1 = Clean(70002); s1.BossSequence[1] = charge.Id; s1.ActiveRules.Add(Rule("ULDA_710", 1, "anomaly"));
    var l1 = run.ForbiddenPairs(s1);
    Check("F2 a rule x boss flag pair is reported (ULDA_710 x " + charge.Id + " flag charge)", l1.SequenceEqual(new[] { "act 1: ULDA_710 x " + charge.Id + " (flag charge)" }), string.Join(" | ", l1));
    // Order independence: the twist rolled first and the anomaly taken later, or the anomaly first: the same line.
    var a = Clean(70003); a.ActiveRules.Add(Rule("DALA_831", 1, "twist")); a.ActiveRules.Add(Rule("ULDA_719", 1, "anomaly"));
    var b = Clean(70003); b.ActiveRules.Add(Rule("ULDA_719", 1, "anomaly")); b.ActiveRules.Add(Rule("DALA_831", 1, "twist"));
    var la = run.ForbiddenPairs(a); var lb = run.ForbiddenPairs(b);
    Check("F3 a rule x rule pair is reported in either order, once (ULDA_719 lists DALA_831 one-way)", la.Count == 1 && lb.Count == 1 && la[0].Contains("ULDA_719") && la[0].Contains("DALA_831") && la[0].EndsWith("(incompatibleRules)")
        && lb[0].Contains("ULDA_719") && lb[0].Contains("DALA_831"), string.Join(" | ", la) + " || " + string.Join(" | ", lb));
    Check("F4 rules of different acts never pair (DALA_831 on act 1, ULDA_719 on act 2)", run.ForbiddenPairs(new Func<DungeonRunState>(() => { var s = Clean(70004); s.ActiveRules.Add(Rule("DALA_831", 1, "twist")); s.ActiveRules.Add(Rule("ULDA_719", 2, "anomaly")); return s; })())
        .All(l => !(l.Contains("ULDA_719 x DALA_831") || l.Contains("DALA_831 x ULDA_719"))));
    // A run-scope rule (act 0) is checked against every act: ULDA_720 Battle Scars and a later act's bosses, the treasure it excludes.
    var s5 = Clean(70005); s5.ActiveRules.Add(Rule("ULDA_720", 0, "anomaly")); s5.Treasures.Add("LOOTA_800");
    var l5 = run.ForbiddenPairs(s5);
    Check("F5 a run-scope rule x a held treasure is reported (ULDA_720 x LOOTA_800)", l5.Contains("act 1: ULDA_720 x treasure LOOTA_800 (incompatibleTreasures)"), string.Join(" | ", l5));
    var s6 = Clean(70006); s6.ActiveRules.Add(Rule("ULDA_715", 1, "anomaly")); s6.ActiveRules.Add(new DungeonRunRule { Id = "mod_open_road", Act = 1, FromNode = -1 });
    Check("F6 a rule x modifier pair is reported (ULDA_715 x mod_open_road)", run.ForbiddenPairs(s6).Contains("act 1: ULDA_715 x mod_open_road (incompatibleModifiers)"), string.Join(" | ", run.ForbiddenPairs(s6)));
    var s7 = Clean(70007); s7.ActiveRules.Add(Rule("ULDA_721", 1, "anomaly")); s7.Cards[0].Opening = true;
    Check("F7 a rule x the run deck's opening flag is reported (ULDA_721)", run.ForbiddenPairs(s7).Contains("act 1: ULDA_721 x an opening-flag card of the run deck (noOpeningFlags)"), string.Join(" | ", run.ForbiddenPairs(s7)));
    var shrineDeck = data.Decks.First(x => x.Shrine != null && data.FindClass(x.Class) != null && SabberStoneCore.Actions.ShrineRules.IsScripted(x.Shrine));
    var shrineRun = New(run, data.FindClass(shrineDeck.Class), shrineDeck.Id, 70008, "acts-v4h");
    shrineRun.ActiveRules.RemoveAll(r => r.Kind != null);
    shrineRun.ActiveRules.Add(Rule("ULDA_706", 1, "anomaly"));
    var l8 = run.ForbiddenPairs(shrineRun);
    Check("F8 a noBoardShrine rule on a shrine run is reported (ULDA_706)", shrineRun.HasBoardShrine && l8.Contains("act 1: ULDA_706 x board shrine " + shrineRun.BoardShrine + " (noBoardShrine)"), string.Join(" | ", l8));
    var darius = data.FindClass("GILA_600h");
    var s9 = New(run, darius, darius.Decks[0], 70009, "acts-v4h");
    s9.ActiveRules.RemoveAll(r => r.Kind != null); s9.ActiveRules.Add(Rule("DALA_833", 3, "twist"));
    Check("F9 a rule x the run class is reported (DALA_833 x GILA_600h)", run.ForbiddenPairs(s9).Contains("act 3: DALA_833 x run class GILA_600h (incompatibleClasses)"), string.Join(" | ", run.ForbiddenPairs(s9)));
    // A fight-only hero: Hagatha's fight gives the run player GILA_600h2 (Darius's card), which DALA_833 names by its row.
    var s10 = Clean(70010); s10.BossSequence[1] = "gila_61_hagatha"; s10.ActiveRules.Add(Rule("DALA_833", 1, "twist"));
    var l10 = run.ForbiddenPairs(s10);
    Check("F10 a rule x a fight-only hero is reported (DALA_833 x Hagatha's GILA_600h2)", l10.Contains("act 1: DALA_833 x gila_61_hagatha (fight-only hero GILA_600h2, incompatibleClasses)")
        && l10.Contains("act 1: DALA_833 x gila_61_hagatha (flag heroSwitch)"), string.Join(" | ", l10));
    // Modifier x boss: on stamp 4 a row with a v4 block bans nothing act-wide; a released schedule keeps the ban.
    var s11 = Clean(70011); s11.BossSequence[1] = "loot_36_ixlid"; s11.ActiveRules.Add(new DungeonRunRule { Id = "mod_iron_bosses", Act = 1, FromNode = -1 });
    var v3 = New(run, plainClass, plainClass.Decks[0], 70011, "acts-v3h");
    v3.BossSequence[1] = "loot_36_ixlid"; v3.ActiveRules.RemoveAll(r => r.Kind != null); v3.ActiveRules.Add(new DungeonRunRule { Id = "mod_iron_bosses", Act = 1, FromNode = -1 });
    Check("F11 a modifier x boss pair: reported on a released schedule (act-wide ban), not on stamp 4 (per-fight neutralisation)", run.ForbiddenPairs(s11).Count == 0
        && run.ForbiddenPairs(v3).Contains("act 1: mod_iron_bosses x loot_36_ixlid (the boss's incompatibleModifiers)"), string.Join(" | ", run.ForbiddenPairs(s11)) + " || " + string.Join(" | ", run.ForbiddenPairs(v3)));
    // BuildPlan logs a violation (never changes the plan); no sink, no work.
    var logged = new List<string>();
    var walk = Clean(70012); walk.BossSequence[0] = charge.Id; walk.ActiveRules.Add(Rule("ULDA_710", 1, "anomaly"));
    while (walk.Phase != DungeonRunPhase.PreMatch)
    {
        if (walk.Phase == DungeonRunPhase.Modifier) run.PickModifier(walk, DungeonRunGlobalRule.None);
        else throw new Exception("F12 walk stopped at " + walk.Phase);
    }
    var quiet = run.BuildPlan(walk, all);
    DungeonRun.ForbiddenPairLog = logged.Add;
    var loud = run.BuildPlan(walk, all);
    DungeonRun.ForbiddenPairLog = null;
    Check("F12 BuildPlan logs each forbidden pair with its floor and builds the same plan", logged.SequenceEqual(new[] { "floor 1: act 1: ULDA_710 x " + charge.Id + " (flag charge)" })
        && quiet.GlobalRules.SequenceEqual(loud.GlobalRules) && quiet.GlobalRules.Contains("ULDA_710") && quiet.BossHealth == loud.BossHealth, string.Join(" | ", logged));
    // The gate agrees with the predicate: what ForbiddenPairs reports, RuleCandidates never offers.
    Check("F13 RuleCandidates leaves out each rule the cases above pair (ULDA_710 with a charge boss, ULDA_719 after DALA_831, ULDA_715 after Open Road, DALA_833 for Darius or with Hagatha)",
        !run.RuleCandidates(new Func<DungeonRunState>(() => { var s = Clean(70002); s.BossSequence[1] = charge.Id; return s; })(), run.ScheduleFor(s1), 1, "anomaly").Any(r => r.Id == "ULDA_710")
        && !run.RuleCandidates(new Func<DungeonRunState>(() => { var s = Clean(70003); s.ActiveRules.Add(Rule("DALA_831", 1, "twist")); return s; })(), run.ScheduleFor(a), 1, "anomaly").Any(r => r.Id == "ULDA_719")
        && !run.RuleCandidates(new Func<DungeonRunState>(() => { var s = Clean(70006); s.ActiveRules.Add(new DungeonRunRule { Id = "mod_open_road", Act = 1, FromNode = -1 }); return s; })(), run.ScheduleFor(s6), 1, "anomaly").Any(r => r.Id == "ULDA_715")
        && !run.RuleCandidates(new Func<DungeonRunState>(() => { var s = New(run, darius, darius.Decks[0], 70009, "acts-v4h"); s.ActiveRules.RemoveAll(r => r.Kind != null); return s; })(), run.ScheduleFor(s9), 3, "twist").Any(r => r.Id == "DALA_833")
        && !run.RuleCandidates(new Func<DungeonRunState>(() => { var s = Clean(70010); s.BossSequence[1] = "gila_61_hagatha"; return s; })(), run.ScheduleFor(s10), 1, "twist").Any(r => r.Id == "DALA_833"));
    // The fight-only hero on its own: Hagatha without her heroSwitch flag still gives the run player Darius's card, which
    // DALA_833 names in incompatibleClasses. The predicate reports the pair, so the gate must leave the twist out too.
    var unflagged = Load();
    unflagged.Boss("gila_61_hagatha").Flags.Remove("heroSwitch");
    var ur = new DungeonRun(unflagged);
    var umage = unflagged.ClassRows.First(c => c.Id == "MAGE");
    var us = New(ur, umage, umage.Decks[0], 70014, "acts-v4h");
    foreach (var n in ur.ScheduleFor(us).Nodes.Where(n => n.IsFight && n.Act == 1)) us.BossSequence[n.Floor - 1] = plain[(n.Floor - 1) % plain.Count].Id;
    us.BossSequence[1] = "gila_61_hagatha";
    us.ActiveRules.RemoveAll(r => r.Kind != null);
    bool gateOffers = ur.RuleCandidates(us, ur.ScheduleFor(us), 1, "twist").Any(r => r.Id == "DALA_833");
    us.ActiveRules.Add(Rule("DALA_833", 1, "twist"));
    var lu = ur.ForbiddenPairs(us);
    Check("F14 a rule x fight-only hero pair with no flag behind it: the predicate reports it and RuleCandidates leaves the rule out", !gateOffers
        && lu.SequenceEqual(new[] { "act 1: DALA_833 x gila_61_hagatha (fight-only hero GILA_600h2, incompatibleClasses)" }), "gate offers " + gateOffers + "; " + string.Join(" | ", lu));
    // C1's run-scope half: a run-scope anomaly offered in act I is checked against every act's bosses (and rules). No shipped
    // run-scope rule excludes a boss or a rule, so a data copy gives ULDA_729 an incompatibleFlags entry ("charge") that only an
    // act III boss carries: act I's gate must leave it out, the predicate must report it on act 3, and without the act III boss
    // the gate offers it (the exclusion is the act III boss's alone).
    var scoped = Load();
    scoped.Schedules.Rule("ULDA_729").IncompatibleFlags.Add("charge");
    var sr = new DungeonRun(scoped);
    var smage = scoped.ClassRows.First(c => c.Id == "MAGE");
    DungeonRunState AllPlain(long seed)
    {
        var st = New(sr, smage, smage.Decks[0], seed, "acts-v4h");
        foreach (var n in sr.ScheduleFor(st).Nodes.Where(n => n.IsFight && !sr.ScheduleFor(st).Acts[n.Act - 1].IsFinale)) st.BossSequence[n.Floor - 1] = plain[(n.Floor - 1) % plain.Count].Id;
        st.ActiveRules.RemoveAll(r => r.Kind != null);
        return st;
    }
    var quietRun = AllPlain(70015);
    var loudRun = AllPlain(70015);
    int act3Floor = sr.ScheduleFor(loudRun).Nodes.First(n => n.IsFight && n.Act == 3).Floor;
    loudRun.BossSequence[act3Floor - 1] = charge.Id;
    bool offeredQuiet = sr.RuleCandidates(quietRun, sr.ScheduleFor(quietRun), 1, "anomaly").Any(r => r.Id == "ULDA_729");
    bool offeredLoud = sr.RuleCandidates(loudRun, sr.ScheduleFor(loudRun), 1, "anomaly").Any(r => r.Id == "ULDA_729");
    loudRun.ActiveRules.Add(Rule("ULDA_729", 0, "anomaly"));
    var l15 = sr.ForbiddenPairs(loudRun).Where(l => l.Contains("ULDA_729")).ToList();
    Check("F15 a run-scope anomaly is checked against a later act's bosses (ULDA_729 given incompatibleFlags charge, " + charge.Id + " on act III floor " + act3Floor
        + "): act I's gate leaves it out, offers it without that boss, and the predicate reports it once on act 3",
        offeredQuiet && !offeredLoud && l15.SequenceEqual(new[] { "act 3: ULDA_729 x " + charge.Id + " (flag charge)" }),
        "offered without " + offeredQuiet + ", with " + offeredLoud + "; " + string.Join(" | ", l15));
});

// ============================================================ S. C2: the ordering fix on every Rumble shrine deck
Guard("S", () =>
{
    var shrineDecks = data.Decks.Where(x => x.Shrine != null && data.FindClass(x.Class) != null).ToList();
    var noShrine = data.Schedules.Rules.Where(r => r.NoBoardShrine).Select(r => r.Id).ToList();
    int runs = 0, shrineRuns = 0, offers = 0, badOffers = 0, badTwists = 0, twists = 0, other = 0;
    var bad = new List<string>();
    foreach (string schedule in new[] { "acts-v3h", "acts-v4h" })
        foreach (var deck in shrineDecks)
            for (int k = 0; k < 200; k++)
            {
                long seed = 300000 + 1000 * shrineDecks.IndexOf(deck) + k + (schedule == "acts-v4h" ? 500 : 0);
                var s = New(run, data.FindClass(deck.Class), deck.Id, seed, schedule);
                runs++;
                if (!s.HasBoardShrine) { other++; continue; }
                shrineRuns++;
                if (s.ModifierOffer?.Kind == DungeonRunGlobalRule.KindAnomaly && run.ScheduleFor(s).Nodes[s.ModifierOffer.Node].Act == 1)
                {
                    offers++;
                    if (s.ModifierOffer.Offered.Any(noShrine.Contains)) { badOffers++; if (bad.Count < 4) bad.Add(schedule + " " + deck.Id + " " + seed + " offers " + string.Join(",", s.ModifierOffer.Offered)); }
                }
                foreach (var t in s.ActiveRules.Where(r => r.Kind == DungeonRunGlobalRule.KindTwist))
                {
                    twists++;
                    if (noShrine.Contains(t.Id)) { badTwists++; if (bad.Count < 4) bad.Add(schedule + " " + deck.Id + " " + seed + " rolls " + t.Id); }
                }
            }
    Note($"S {shrineDecks.Count} shrine decks, {runs} new runs ({shrineRuns} with a board shrine), {offers} stored act I anomaly offers, {twists} rolled twists; noBoardShrine rules {string.Join(",", noShrine)}");
    Check("S1 the shrine decks are the 27 Rumble decks and every run on them holds its board shrine", shrineDecks.Count == 27 && other == 0 && shrineRuns == runs, other + " without a shrine");
    Check("S2 every new shrine run stores its act I anomaly offer at once (the schedule starts with an anomaly node)", offers == shrineRuns);
    Check("S3 no stored act I anomaly offer of a shrine run carries a noBoardShrine rule (the ordering fix)", badOffers == 0, string.Join(" | ", bad));
    Check("S4 no shrine run rolls a noBoardShrine twist (DALA_831, DALA_833 on acts-v4h act III)", badTwists == 0 && twists > 0, string.Join(" | ", bad));
});

// ============================================================ T. C5: the twist roll
// Whether twist t can be rolled on act a of run s: an independent reading of the data (the rule's admission, the board shrine,
// the run class, and every boss pre-rolled for the act: its incompatibleRules and flags, and its fight-only heroes, Hagatha's hero
// cards and a finale's relay heroes, against the rule's incompatibleClasses by character). At roll time no other rule is active
// on the act (twists are act rules, rolled before the first node).
// One character: equal ids, or ids that are a letter followed by digits and differ only in the digits (GILA_600h, GILA_600h2).
bool SameHero(string a, string b)
{
    string Trim(string id) { string t = id.TrimEnd("0123456789".ToCharArray()); return t.Length > 0 && char.IsLetter(t[t.Length - 1]) ? t : null; }
    return a == b || Trim(a) != null && Trim(a) == Trim(b);
}
bool Compatible(DungeonRunData d, DungeonRunState s, DungeonRunSchedule schedule, int act, string twistId)
{
    var t = d.Schedules.Rule(twistId);
    if (t == null || !t.Admitted || t.Kind != DungeonRunGlobalRule.KindTwist) return false;
    if (t.NoBoardShrine && s.HasBoardShrine) return false;
    if (t.IncompatibleClasses.Contains(s.Class)) return false;
    foreach (var n in schedule.Nodes.Where(n => n.Act == act && n.IsFight))
    {
        var boss = d.Boss(s.BossSequence[n.Floor - 1]);
        if (boss.IncompatibleRules.Contains(twistId) || boss.Flags.Any(t.IncompatibleFlags.Contains)) return false;
        if (boss.HeroCards.Select(h => h.Hero).Concat(boss.Relay.Select(h => h.Hero)).Any(h => t.IncompatibleClasses.Any(c => SameHero(c, h)))) return false;
    }
    return true;
}
// One sweep: n new runs with no named schedule (the default roll), the loadouts in turn. Per act I-III: runs, runs whose chance
// draw hit, runs with no compatible twist (the exclusion loss), rolls, and rolls per twist; plus per-run agreement.
(int Runs, Dictionary<string, int> PerSchedule, int[] Hits, int[] Empty, int[] Rolled, Dictionary<string, int>[] PerTwist, int Disagree, string FirstDisagree, int FinaleRuns, int FinaleRolled)
    Sweep(DungeonRun r, int n, long seedBase, string schedule = null)
{
    var loadouts = Loadouts(r.Data);
    var per = new Dictionary<string, int>(StringComparer.Ordinal);
    int[] hits = new int[4], empty = new int[4], rolled = new int[4];
    var perTwist = Enumerable.Range(0, 4).Select(_ => new Dictionary<string, int>(StringComparer.Ordinal)).ToArray();
    int disagree = 0, finaleRuns = 0, finaleRolled = 0; string first = "";
    int made = 0;
    for (int i = 0; made < n; i++)
    {
        var (row, deck) = loadouts[i % loadouts.Count];
        long seed = seedBase + i;
        DungeonRunState s;
        try { s = New(r, row, deck, seed, schedule, i / loadouts.Count); }
        catch (InvalidOperationException) { continue; } // a named schedule whose finale the row excludes (DALA_Tekahn on acts-v4t)
        made++;
        per[s.Schedule] = per.TryGetValue(s.Schedule, out int c) ? c + 1 : 1;
        var sc = r.ScheduleFor(s);
        for (int a = 1; a <= sc.Acts.Count; a++)
        {
            var act = sc.Acts[a - 1];
            var mine = s.ActiveRules.Where(x => x.Kind == DungeonRunGlobalRule.KindTwist && x.Act == a).Select(x => x.Id).ToList();
            if (act.IsFinale)
            {
                if (act.TwistPool.Count > 0) { finaleRuns++; if (mine.SequenceEqual(act.TwistPool)) finaleRolled++; }
                continue;
            }
            if (act.TwistPool.Count == 0) continue;
            bool hit = StableSeed.Index(StableSeed.Derive(s.Seed, DungeonRun.TwistStream, a, 0), 100) < act.TwistChance;
            bool any = act.TwistPool.Any(t => Compatible(r.Data, s, sc, a, t));
            if (hit) hits[a]++;
            if (!any) empty[a]++;
            if (mine.Count > 0) { rolled[a]++; perTwist[a][mine[0]] = perTwist[a].TryGetValue(mine[0], out int k) ? k + 1 : 1; }
            bool agrees = mine.Count == (hit && any ? 1 : 0) && mine.All(t => act.TwistPool.Contains(t) && Compatible(r.Data, s, sc, a, t));
            if (!agrees) { disagree++; if (first.Length == 0) first = s.Schedule + " seed " + seed + " act " + a + ": hit " + hit + " compatible " + any + " rolled " + string.Join(",", mine); }
        }
    }
    return (made, per, hits, empty, rolled, perTwist, disagree, first, finaleRuns, finaleRolled);
}
Guard("T", () =>
{
    var sweep = Sweep(run, 2000, 500000);
    var sc4 = data.Schedules.Find("acts-v4h");
    Note("T shipped default roll: " + sweep.Runs + " runs, " + string.Join(", ", sweep.PerSchedule.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => p.Key + " " + p.Value)));
    var twistRuns = actTwists.ToDictionary(t => t, t => Enumerable.Range(1, 3).Sum(a => sweep.PerTwist[a].TryGetValue(t, out int c) ? c : 0));
    for (int a = 1; a <= 3; a++)
        Note($"T act {a}: chance {sc4.Acts[a - 1].TwistChance}, exclusion loss {Pct(sweep.Empty[a], sweep.Runs)}%, rolled {Pct(sweep.Rolled[a], sweep.Runs)}%: "
            + string.Join(" ", sweep.PerTwist[a].OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => p.Key + " " + Pct(p.Value, sweep.Runs) + "%")));
    Check("T1 the default roll on the shipped data gives stamp-4 runs only (acts-v4h and acts-v4t)", sweep.PerSchedule.Keys.OrderBy(k => k, StringComparer.Ordinal).SequenceEqual(new[] { "acts-v4h", "acts-v4t" }) && sweep.Runs == 2000);
    Check("T2 each of the eight act twists rolls in at least 15% of 2000 new runs", actTwists.All(t => twistRuns[t] * 100 >= 15 * sweep.Runs),
        string.Join(" ", actTwists.Select(t => t + " " + Pct(twistRuns[t], sweep.Runs) + "%")));
    Check("T3 every roll is the chance draw and a compatible twist of the pool, and a hit with no compatible twist rolls none", sweep.Disagree == 0, sweep.Disagree + " runs; first: " + sweep.FirstDisagree);
    Check("T4 per act, the roll rate is the chance less the measured exclusion loss (within 3 points)", Enumerable.Range(1, 3).All(a =>
        Math.Abs(100.0 * sweep.Rolled[a] / sweep.Runs - sc4.Acts[a - 1].TwistChance * (1 - (double)sweep.Empty[a] / sweep.Runs)) <= 3),
        string.Join(" ", Enumerable.Range(1, 3).Select(a => "act " + a + " " + Pct(sweep.Rolled[a], sweep.Runs) + " vs " + (sc4.Acts[a - 1].TwistChance * (1 - (double)sweep.Empty[a] / sweep.Runs)).ToString("F1", System.Globalization.CultureInfo.InvariantCulture))));
    Check("T5 DALA_830 The Finale rolls on every acts-v4h run (and acts-v4t's finale has no twist)", sweep.FinaleRuns == sweep.PerSchedule["acts-v4h"] && sweep.FinaleRolled == sweep.FinaleRuns
        && data.Schedules.Find("acts-v4t").Acts.Where(x => x.IsFinale).All(x => x.TwistPool.Count == 0), sweep.FinaleRolled + " of " + sweep.FinaleRuns);
    Check("T6 the shipped pools are the chapter mapping (I DALA_829 ULDA_801, II DALA_832 ULDA_802, III DALA_833 DALA_831 ULDA_803 ULDA_804) at chance 100, acts-v4t's the same acts",
        sc4.Acts[0].TwistPool.SequenceEqual(new[] { "DALA_829", "ULDA_801" }) && sc4.Acts[1].TwistPool.SequenceEqual(new[] { "DALA_832", "ULDA_802" })
        && sc4.Acts[2].TwistPool.SequenceEqual(new[] { "DALA_833", "DALA_831", "ULDA_803", "ULDA_804" }) && sc4.Acts.Take(3).All(x => x.TwistChance == 100)
        && data.Schedules.Find("acts-v4t").Acts.Take(3).Select(x => string.Join(",", x.TwistPool) + x.TwistChance).SequenceEqual(sc4.Acts.Take(3).Select(x => string.Join(",", x.TwistPool) + x.TwistChance)));
    // The released schedules roll no act twist (their pools stay empty).
    var v3 = Sweep(run, 400, 520000, "acts-v3h");
    Check("T7 acts-v3h rolls no act I-III twist (empty pools) and DALA_830 on every finale", v3.Rolled.Sum() == 0 && v3.FinaleRolled == v3.Runs && v3.Runs == 400);
    // The fixture: all eight twists in every act, chances below 100, DALA_829 weight 4.
    var fdata = Load(File.ReadAllText(fixturePath));
    var frun = new DungeonRun(fdata);
    var f4 = Sweep(frun, 2000, 540000, "acts-v4h");
    var f3 = Sweep(frun, 2000, 560000, "acts-v3h");
    var fsc = fdata.Schedules.Find("acts-v4h");
    for (int a = 1; a <= 3; a++)
        Note($"T fixture acts-v4h act {a}: chance {fsc.Acts[a - 1].TwistChance}, hits {Pct(f4.Hits[a], f4.Runs)}%, exclusion loss {Pct(f4.Empty[a], f4.Runs)}%, rolled {Pct(f4.Rolled[a], f4.Runs)}%: "
            + string.Join(" ", f4.PerTwist[a].OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => p.Key + " " + p.Value)));
    Check("T8 fixture: every roll is the chance draw and a compatible twist (stamp 4 and the released pick)", f4.Disagree == 0 && f3.Disagree == 0, f4.FirstDisagree + " " + f3.FirstDisagree);
    Check("T9 fixture: the chance draw hits at the act's chance (70 / 85 / 55, within 3 points)", Enumerable.Range(1, 3).All(a => Math.Abs(100.0 * f4.Hits[a] / f4.Runs - fsc.Acts[a - 1].TwistChance) <= 3),
        string.Join(" ", Enumerable.Range(1, 3).Select(a => Pct(f4.Hits[a], f4.Runs))));
    Check("T10 fixture: per act, the roll rate is the chance less the exclusion loss (within 3 points)", Enumerable.Range(1, 3).All(a =>
        Math.Abs(100.0 * f4.Rolled[a] / f4.Runs - fsc.Acts[a - 1].TwistChance * (1 - (double)f4.Empty[a] / f4.Runs)) <= 3));
    int Twist(Dictionary<string, int>[] per, string id) => Enumerable.Range(1, 3).Sum(a => per[a].TryGetValue(id, out int c) ? c : 0);
    double ratio4 = (double)Twist(f4.PerTwist, "DALA_829") / Math.Max(1, Twist(f4.PerTwist, "DALA_832")), ratio3 = (double)Twist(f3.PerTwist, "DALA_829") / Math.Max(1, Twist(f3.PerTwist, "DALA_832"));
    string Ratio(double x) => x.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
    Note($"T fixture DALA_829 (weight 4) : DALA_832 (weight 1): stamp 4 {Twist(f4.PerTwist, "DALA_829")}:{Twist(f4.PerTwist, "DALA_832")} = {Ratio(ratio4)}, released {Twist(f3.PerTwist, "DALA_829")}:{Twist(f3.PerTwist, "DALA_832")} = {Ratio(ratio3)}");
    Check("T11 the stamp-4 pick honours the weight (DALA_829 : DALA_832 near 4), the released pick stays uniform (near 1)", ratio4 >= 3.2 && ratio4 <= 4.8 && ratio3 >= 0.8 && ratio3 <= 1.25,
        Ratio(ratio4) + " / " + Ratio(ratio3));
    Check("T12 fixture: every one of the eight twists rolls on every act of acts-v4h", Enumerable.Range(1, 3).All(a => actTwists.All(t => f4.PerTwist[a].ContainsKey(t))), string.Join(" | ", Enumerable.Range(1, 3).Select(a => "act " + a + " missing " + string.Join(",", actTwists.Where(t => !f4.PerTwist[a].ContainsKey(t))))));
});

// ============================================================ W. C1 and C3: walks to Victory
// A seeded pick: the same choice for the same run, node and draw.
int Pick(DungeonRunState s, int count, int draw) => count <= 1 ? 0 : StableSeed.Index(StableSeed.Derive(s.Seed, 777, s.Node * 16 + s.Floor, draw), count);
Guard("W", () =>
{
    var loadouts = Loadouts(data);
    var scratch = new DungeonRunProfile();
    int walks = 0, victories = 0, steps = 0, pairStates = 0, probes = 0, probeBad = 0, modNodes = 0, modOffers = 0, modForced = 0, modNoNone = 0, modSkipped = 0, v4Walks = 0;
    int rulesTaken = 0, anomalyOffers = 0, planErrors = 0;
    var pairs = new List<string>(); var probeLines = new List<string>(); var livenessLines = new List<string>(); var planLines = new List<string>();
    var perSchedule = new Dictionary<string, int>(StringComparer.Ordinal);
    var taken = new HashSet<string>(StringComparer.Ordinal);
    void Pairs(DungeonRunState s, string where)
    {
        var lines = run.ForbiddenPairs(s);
        if (lines.Count == 0) return;
        pairStates++;
        if (pairs.Count < 6) pairs.Add(where + ": " + lines[0]);
    }
    // Takes the option hypothetically (the offer stays as it is) and checks the state stays clean.
    void Probe(DungeonRunState s, string what, Action<DungeonRunState> take, Action<DungeonRunState> undo)
    {
        probes++;
        take(s);
        var lines = run.ForbiddenPairs(s);
        undo(s);
        if (lines.Count > 0) { probeBad++; if (probeLines.Count < 6) probeLines.Add(s.Schedule + " seed " + s.Seed + " node " + s.Node + " " + what + ": " + lines[0]); }
    }
    foreach (string schedule in new[] { "acts-v3h", "acts-v3t", "acts-v4h", "acts-v4t" })
        for (int li = 0; li < loadouts.Count; li++)
        {
            var (row, deck) = loadouts[li];
            int seeds = schedule.StartsWith("acts-v4") ? 2 : 1;
            for (int k = 0; k < seeds; k++)
            {
                long seed = 600000 + 10000 * Array.IndexOf(new[] { "acts-v3h", "acts-v3t", "acts-v4h", "acts-v4t" }, schedule) + 10 * li + k;
                DungeonRunState s;
                try { s = New(run, row, deck, seed, schedule, k); }
                catch (InvalidOperationException) { continue; } // DALA_Tekahn on acts-v3t / acts-v4t (FinaleExcludes)
                walks++;
                perSchedule[schedule] = perSchedule.TryGetValue(schedule, out int c) ? c + 1 : 1;
                var sc = run.ScheduleFor(s);
                bool tuned = DungeonRun.Tuned(s);
                if (tuned) v4Walks++;
                var offeredAt = new HashSet<int>();
                for (int guard = 0; guard < 600 && !s.Finished; guard++)
                {
                    steps++;
                    Pairs(s, schedule + " seed " + seed + " node " + s.Node + " " + s.Phase);
                    switch (s.Phase)
                    {
                        case DungeonRunPhase.PreMatch:
                            try { run.BuildPlan(s, scratch); }
                            catch (Exception e) { planErrors++; if (planLines.Count < 4) planLines.Add(schedule + " seed " + seed + " floor " + s.Floor + ": " + e.GetType().Name + ": " + e.Message); }
                            run.BeginMatch(s); run.RecordOutcome(s, scratch, DungeonRunOutcome.Win); break;
                        case DungeonRunPhase.TreasureDraft:
                            foreach (string t in s.OpenDraft.Offered.ToList())
                                if (s.OpenDraft.Kind == "treasure" && data.FindTreasure(t) is DungeonRunTreasure tr)
                                    Probe(s, "treasure " + t, x => (tr.Active ? x.ActiveTreasures : x.Treasures).Add(t), x => (tr.Active ? x.ActiveTreasures : x.Treasures).Remove(t));
                            run.PickTreasure(s, s.OpenDraft.Offered[Pick(s, s.OpenDraft.Offered.Count, 1)]); break;
                        case DungeonRunPhase.Modifier:
                        {
                            var offer = s.ModifierOffer;
                            bool anomaly = offer.Kind == DungeonRunGlobalRule.KindAnomaly;
                            var real = offer.Offered.Where(id => id != DungeonRunGlobalRule.None).ToList();
                            if (anomaly) anomalyOffers++;
                            else
                            {
                                modOffers++; offeredAt.Add(s.Node);
                                if (tuned && offer.Offered.Count < 2) { modForced++; if (livenessLines.Count < 4) livenessLines.Add(schedule + " seed " + seed + " node " + s.Node + " offers " + string.Join(",", offer.Offered)); }
                                if (tuned && (offer.Offered.LastOrDefault() != DungeonRunGlobalRule.None || real.Count < 1)) { modNoNone++; if (livenessLines.Count < 4) livenessLines.Add(schedule + " seed " + seed + " node " + s.Node + " offers " + string.Join(",", offer.Offered)); }
                            }
                            int act = run.CurrentNode(s).Act;
                            foreach (string id in real)
                            {
                                var rule = anomaly ? data.Schedules.Rule(id) : null;
                                var entry = anomaly ? Rule(id, rule.RunScope ? 0 : act, DungeonRunGlobalRule.KindAnomaly) : new DungeonRunRule { Id = id, Act = act, FromNode = s.Node };
                                Probe(s, (anomaly ? "anomaly " : "modifier ") + id, x => x.ActiveRules.Add(entry), x => x.ActiveRules.Remove(entry));
                            }
                            string pick = offer.Offered[Pick(s, offer.Offered.Count, 2)];
                            if (anomaly && pick != DungeonRunGlobalRule.None) { rulesTaken++; taken.Add(pick); }
                            run.PickModifier(s, pick);
                            break;
                        }
                        case DungeonRunPhase.Tavern:
                            foreach (string option in s.Tavern.Offers.ToList())
                            {
                                var bought = DungeonRunTavernNode.Buy(run, s, option);
                                int d = 3;
                                while (bought.Ok && s.Tavern.Pending != null) bought = DungeonRunTavernNode.Choose(run, s, s.Tavern.Pending.Candidates[Pick(s, s.Tavern.Pending.Candidates.Count, d++)]);
                                if (s.Tavern.Pending != null) DungeonRunTavernNode.Back(s);
                                Pairs(s, schedule + " seed " + seed + " node " + s.Node + " after tavern " + option);
                            }
                            run.LeaveTavern(s);
                            break;
                        case DungeonRunPhase.BundleDraft:
                            if (s.OpenDraft.Takes == 0) { run.PickBundle(s, s.OpenDraft.Offered[Pick(s, s.OpenDraft.Offered.Count, 4)]); break; }
                            string bundle = s.OpenDraft.Choosing ?? s.OpenDraft.Offered[Pick(s, s.OpenDraft.Offered.Count, 5)];
                            var left = run.OfferCards(s.OpenDraft, bundle);
                            foreach (string kept in s.OpenDraft.Chosen) left.Remove(kept);
                            run.PickDraftCard(s, bundle, left[Pick(s, left.Count, 6)]);
                            break;
                        default: throw new InvalidOperationException("walk stopped at " + s.Phase);
                    }
                }
                Pairs(s, schedule + " seed " + seed + " end");
                if (s.Phase == DungeonRunPhase.Victory) victories++;
                if (tuned)
                    foreach (var n in sc.Nodes.Where(n => n.Kind == DungeonRunNodeKind.Modifier))
                    {
                        modNodes++;
                        if (!offeredAt.Contains(n.Index)) { modSkipped++; if (livenessLines.Count < 4) livenessLines.Add(schedule + " seed " + seed + " modifier node " + n.Index + " opened no offer"); }
                    }
            }
        }
    Note($"W {walks} walks ({string.Join(", ", perSchedule.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => p.Key + " " + p.Value))}), {victories} to Victory, {steps} steps, {anomalyOffers} anomaly offers, "
        + $"{rulesTaken} rules taken ({taken.Count} distinct), {modOffers} modifier offers, {probes} options probed");
    Check("W1 every walk reaches Victory (every loadout; acts-v3t and acts-v4t without DALA_Tekahn's)", victories == walks && perSchedule.Count == 4, victories + " of " + walks);
    Check("W2 C1: no state of any walk holds a forbidden pair", pairStates == 0, pairStates + " states; " + string.Join(" | ", pairs));
    Check("W3 C1: every offered rule, modifier and treasure keeps the state clean when taken (the gates match the predicate)", probeBad == 0 && probes > 1000, probeBad + " of " + probes + "; " + string.Join(" | ", probeLines));
    Check("W4 every fight's plan builds (BuildPlan never throws at PreMatch)", planErrors == 0, string.Join(" | ", planLines));
    Check("W5 C3: on stamp 4 every modifier node opens an offer (" + modNodes + " nodes)", modSkipped == 0 && modNodes > 0, string.Join(" | ", livenessLines));
    Check("W6 C3: on stamp 4 every modifier offer holds at least one modifier and ends with \"none\" (no forced single pick)", modForced == 0 && modNoNone == 0, string.Join(" | ", livenessLines));
    // The run-scope anomalies are ULDA_720 and ULDA_729 (scope "run"); ULDA_721 is an act-scope match rule.
    var runScope = data.Schedules.Rules.Where(r => r.RunScope && r.Admitted && r.Kind == DungeonRunGlobalRule.KindAnomaly).Select(r => r.Id).ToList();
    Check("W7 the walks take anomalies of many kinds (at least 25 distinct rules, both run-scope ones included: " + string.Join(", ", runScope) + ")",
        taken.Count >= 25 && runScope.Count == 2 && runScope.All(taken.Contains), string.Join(",", taken.OrderBy(x => x, StringComparer.Ordinal)));
});

// ============================================================ K. C4: every modifier knob x the majors, finales and fixedHealth rows
Guard("K", () =>
{
    var knobNames = new[] { "bossHealthDelta", "bossManaDelta", "playerHealthDelta", "openingHandBonus" };
    int Knob(DungeonRunModifier m, string k) => k == "bossHealthDelta" ? m.BossHealthDelta : k == "bossManaDelta" ? m.BossManaDelta : k == "playerHealthDelta" ? m.PlayerHealthDelta : m.OpeningHandBonus;
    int Field(RunMatchPlan p, string k) => k == "bossHealthDelta" ? p.BossHealth : k == "bossManaDelta" ? p.BossStartManaBonus : k == "playerHealthDelta" ? p.PlayerHealthBonus : p.PlayerOpeningHandBonus;
    var mage = data.ClassRows.First(c => c.Id == "MAGE");
    int visible = 0, declared = 0, unreachable = 0, silent = 0, checkedRows = 0, inconsistent = 0;
    var silentLines = new List<string>(); var declaredLines = new List<string>(); var badDeclared = new List<string>();
    foreach (string schedule in new[] { "acts-v4h", "acts-v4t" })
    {
        var sc = data.Schedules.Find(schedule);
        // Each fight floor's pool (a finale floor: its fixed boss).
        var floors = sc.Nodes.Where(n => n.IsFight).OrderBy(n => n.Floor).ToList();
        var acts = sc.Acts;
        string PoolAt(DungeonRunNode n)
        {
            var act = acts[n.Act - 1];
            if (act.IsFinale) return "=" + act.FixedBosses[n.FloorInAct - 1];
            if (n.Kind == DungeonRunNodeKind.Major) return act.MajorPool;
            int k = n.FloorInAct - 1;
            foreach (var slot in act.Combat) { if (k < slot.Count) return slot.Pool; k -= slot.Count; }
            return null;
        }
        var rows = data.Bosses.Where(b => (b.FixedHealth || b.Tier != null && (b.Tier.StartsWith("major") || b.Tier == DungeonRunEncounterData.FinaleTier)
            || floors.Any(n => n.Kind == DungeonRunNodeKind.Major && !acts[n.Act - 1].IsFinale && data.Schedules.MemberTuned(b, PoolAt(n))))).ToList();
        foreach (var boss in rows)
        {
            var node = floors.FirstOrDefault(n => PoolAt(n) == "=" + boss.Id) ?? floors.FirstOrDefault(n => PoolAt(n) is string p && !p.StartsWith("=") && data.Schedules.MemberTuned(boss, p));
            if (node == null) continue; // not on this schedule (the other finale's rows)
            checkedRows++;
            var s = New(run, mage, mage.Decks[0], 800000 + data.Bosses.IndexOf(boss), schedule);
            var walkProfile = new DungeonRunProfile();
            for (int guard = 0; guard < 600 && !(s.Floor == node.Floor && s.Phase == DungeonRunPhase.PreMatch); guard++)
            {
                switch (s.Phase)
                {
                    case DungeonRunPhase.PreMatch: run.BeginMatch(s); run.RecordOutcome(s, walkProfile, DungeonRunOutcome.Win); break;
                    case DungeonRunPhase.TreasureDraft: run.PickTreasure(s, s.OpenDraft.Offered[0]); break;
                    case DungeonRunPhase.Modifier: run.PickModifier(s, DungeonRunGlobalRule.None); break;
                    case DungeonRunPhase.Tavern: run.LeaveTavern(s); break;
                    case DungeonRunPhase.BundleDraft:
                        if (s.OpenDraft.Takes == 0) { run.PickBundle(s, s.OpenDraft.Offered[0]); break; }
                        string bundle = s.OpenDraft.Choosing ?? s.OpenDraft.Offered[0];
                        var left = run.OfferCards(s.OpenDraft, bundle);
                        foreach (string kept in s.OpenDraft.Chosen) left.Remove(kept);
                        run.PickDraftCard(s, bundle, left[0]);
                        break;
                    default: throw new Exception("K walk stopped at " + s.Phase);
                }
            }
            s.BossSequence[node.Floor - 1] = boss.Id;
            s.ActiveRules.RemoveAll(r => r.Kind != null || r.Act == node.Act);
            bool canHold = sc.Nodes.Any(n => n.Act == node.Act && n.Kind == DungeonRunNodeKind.Modifier && n.Index < node.Index);
            var bare = run.BuildPlan(s, all);
            foreach (var m in data.Schedules.Modifiers)
            {
                var entry = new DungeonRunRule { Id = m.Id, Act = node.Act, FromNode = -1 };
                s.ActiveRules.Add(entry);
                var with = run.BuildPlan(s, all);
                s.ActiveRules.Remove(entry);
                bool neutral = DungeonRun.Neutralises(s, boss, m.Id);
                bool reason = boss.ModifierReasons.TryGetValue(m.Id, out string why) && !string.IsNullOrWhiteSpace(why);
                foreach (string k in knobNames.Where(k => Knob(m, k) != 0))
                {
                    string at = $"{schedule} {boss.Id} (floor {node.Floor}) {m.Id}.{k}";
                    bool moved = Field(with, k) != Field(bare, k);
                    if (!canHold) unreachable++;
                    else if (moved) visible++;
                    else if (neutral && reason) { declared++; if (declaredLines.Count < 40) declaredLines.Add(boss.Id + " " + m.Id + "." + k); }
                    else { silent++; silentLines.Add(at + $" ({Field(bare, k)} -> {Field(with, k)}{(neutral ? ", neutralised without a reason" : "")})"); }
                }
            }
        }
    }
    Note($"K {checkedRows} rows on acts-v4h and acts-v4t: knobs visible {visible}, declared neutral {declared}, on a floor no modifier reaches {unreachable}, silent {silent}");
    Note("K declared neutral: " + string.Join(", ", declaredLines.Distinct()));
    Check("K1 C4: no modifier knob is a silent no-op on a major, finale or fixedHealth row (it moves the plan or is neutralised with a reason)", silent == 0 && checkedRows > 20, silent + ": " + string.Join(" | ", silentLines.Take(12)));
});

Console.WriteLine($"RESULT {passes} passed, {failures} failed");
return failures;
