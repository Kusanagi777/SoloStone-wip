// c-shrine-core: phase 7 S1, the shrine and teammate model. Every game check plays a real engine game built through the run
// setup path (DungeonRun.NewRun -> BuildPlan -> DungeonRunSetup.Configure -> Game -> ApplyAfterStart -> mulligan -> MainBegin),
// where the run player's board shrine lands at Start of Game (ShrineRules.Arm). Boss-side moves are played by hand on the
// boss's turn (Mana() gives the side its crystals), so each rule is exercised exactly; the boss AI is c-bosspilot SHRINE_RUN.
//   D  data, schedule, validator and admission: shrines on deck rows, acts-v2 and its draft flags, the teammate and
//      shrine-upgrade tables, ReleasedShapes, the admission rows.
//   N  NewRun: which runs get a board shrine (BoardShrine), and the plan fields.
//   S  the shrine lifecycle in real games: placement, scripts (TRLA_112/115/116), death and dormancy, exact revival turns,
//      revival at full Health with its trigger back, the run rules (transform, bounce, shuffle, remove, control, copy,
//      silence), death counts and resurrect pools, a full board at death, Cannons, the TRLA_107 boss fixture.
//   U  shrine upgrades TRLA_804 (two shrines) and TRLA_805 (revival in two turns).
//   T  teammates TRLA_124/125/127 in real games; the teammate and shrine-upgrade drafts through the run flow; save/resume.
//   V  save version 4 -> 5 and a phase-6 Rikkar run on schedule version 1 (no shrine, no new draft).
// Headless: compiles C:\SoloStone\plugin\DungeonRun\*.cs and the NPC AI against audit-verify/engine-out. Run it twice and compare.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.CardSets.DungeonRun;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

int failures = 0, passes = 0;
void Check(string name, bool ok, string detail = "")
{
    if (ok) passes++; else failures++;
    Console.WriteLine((ok ? "PASS " : "FAIL ") + name + (detail.Length > 0 ? "  (" + detail + ")" : ""));
}
void Guard(string name, Action body)
{
    try { body(); }
    catch (Exception e) { failures++; Console.WriteLine("FAIL " + name + " threw " + e.GetType().Name + ": " + e.Message.Split('\n')[0]); }
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
string runJson = R("dungeon-run/run-v1.json"), scheduleJson = R("dungeon-run/schedule-v1.json");
DungeonRunData Load(string run = null, string schedule = null) => DungeonRunData.Parse(run ?? runJson, R("dungeon-run/pool-v1.json"), R("npc/decks-v1.json"),
    R("dungeon-run/catalog-v1.json"), schedule ?? scheduleJson, R("dungeon-run/tavern-v1.json"));
var data = Load();
var run = new DungeonRun(data);
var all = new DungeonRunProfile(); all.Unlocked.AddRange(data.Unlocks.Keys);
Card C(string id) => Cards.FromId(id);
const string Druid = "TRLA_209h_Druid";

// The first single-class row's deck whose board shrine the engine does not script yet (rows of a shrine family still pending),
// or null once every shrine is scripted: the "unscripted shrine" checks read it instead of naming a family's shrine.
(string Row, string Deck, string Shrine)? PendingShrine()
{
    foreach (var c in data.Classes.Where(c => c.ClassSet.Count == 1))
        foreach (var d in c.Decks.Select(data.FindDeck))
            if (d?.Shrine != null && !ShrineRules.IsScripted(d.Shrine)) return (c.Id, d.Id, d.Shrine);
    return null;
}
var pending = PendingShrine();
DungeonRunState NewRun(string row, string deck, long seed, string schedule = null)
{
    var cls = data.Class(row);
    return run.NewRun(all, cls.Id, deck ?? cls.DefaultDeck, cls.DefaultPower, seed, schedule: schedule,
        signatureId: run.SignatureOffer(all, data.FindClass(cls.Id), seed).FirstOrDefault());
}
Game Start(RunMatchPlan plan, int seed)
{
    var cfg = new GameConfig { RandomSeed = seed, Shuffle = true, SkipMulligan = false, History = true, Logging = false, FormatType = FormatType.FT_WILD };
    DungeonRunSetup.Configure(cfg, plan);
    var g = new Game(cfg);
    g.StartGame();
    DungeonRunSetup.ApplyAfterStart(g, plan);
    return g;
}
(DungeonRunState s, RunMatchPlan plan, Game g) Fight(string deck, long seed, string[] upgrades = null, string row = Druid)
{
    var s = NewRun(row, deck, seed);
    foreach (string u in upgrades ?? Array.Empty<string>()) s.ShrineUpgrades.Add(u);
    var plan = run.BuildPlan(s);
    var g = Start(plan, run.MatchSeed(s));
    g.Process(ChooseTask.Mulligan(g.Player1, new List<int>()));
    g.Process(ChooseTask.Mulligan(g.Player2, new List<int>()));
    g.MainBegin();
    if (g.CurrentPlayer != g.Player1) throw new Exception("run player not first");
    return (s, plan, g);
}
void Mana(Controller c, int n) { c.BaseMana = n; c.UsedMana = 0; c.OverloadLocked = 0; c.OverloadOwed = 0; }
// Adds a card to a hand (making room first: a full hand would burn it).
IPlayable ToHand(Game g, Controller c, string id)
{
    while (c.HandZone.Count >= 9) { var old = c.HandZone[0]; c.HandZone.Remove(old); c.SetasideZone.Add(old); }
    var e = Entity.FromCard(c, C(id)); Generic.AddHandPhase.Invoke(c, e); g.DeathProcessingAndAuraUpdate(); return e;
}
Minion Board(Game g, Controller c, string id) { var m = (Minion)Entity.FromCard(c, C(id)); Generic.SummonBlock(g, m, -1, c.Hero); g.DeathProcessingAndAuraUpdate(); return m; }
bool Play(Game g, IPlayable card, ICharacter target = null) => g.Process(PlayCardTask.Any(g.CurrentPlayer, card, target, -1, 0));
void EndTurn(Game g) => g.Process(EndTurnTask.Any(g.CurrentPlayer));
// Clears both boards except the run shrines, so a check sees only what it set up.
void ClearExceptShrines(Game g)
{
    foreach (var c in new[] { g.Player1, g.Player2 })
        foreach (var m in c.BoardZone.Slots.OfType<Minion>().Where(m => !ShrineRules.IsMarked(m)).ToArray()) { c.BoardZone.Remove(m); c.SetasideZone.Add(m); }
    g.DeathProcessingAndAuraUpdate();
}
Minion ShrineOf(Game g, Controller c) => ShrineRules.Of(c).FirstOrDefault();
void Kill(Game g, Minion m) { m.Destroy(); g.DeathProcessingAndAuraUpdate(); }
void Damage(Game g, Minion m, int n) { Generic.DamageCharFunc(m.Controller.Opponent.Hero, m, n, false); g.DeathProcessingAndAuraUpdate(); }
// Ends turns until the run player's turn number <paramref name="turn"/> starts.
void ToTurn(Game g, int turn) { while (g.Turn < turn && g.State != State.COMPLETE) EndTurn(g); }

// ================================================================ D data, schedule, validator, admission
Guard("D", () =>
{
    var rumble = data.Decks.Where(d => d.Id.StartsWith("rumble_", StringComparison.Ordinal)).ToList();
    Check("D1 the 27 Rumble Run starter decks each name their shrine (deck \"shrine\" = TRLA_<n> of the deck id)",
        rumble.Count == 27 && rumble.All(d => d.Shrine == "TRLA_" + d.Id.Substring(d.Id.LastIndexOf('_') + 1)), rumble.Count + " decks");
    // Build Deck: a Rikkar row's build deck names its default deck's shrine (a built run starts with that shrine on the board).
    Check("D2 no other deck names a shrine but Rikkar's nine build decks, each its row's default deck's",
        data.Decks.Where(d => !d.Id.StartsWith("rumble_", StringComparison.Ordinal) && !d.Build).All(d => d.Shrine == null)
        && data.Decks.Where(d => d.Build && d.Shrine != null).Select(d => d.Shrine + ":" + d.Class).OrderBy(x => x, StringComparer.Ordinal)
            .SequenceEqual(data.Classes.Where(c => c.Adventure == "Rumble").Select(c => data.FindDeck(c.DefaultDeck).Shrine + ":" + c.Id).OrderBy(x => x, StringComparer.Ordinal))
        && data.Decks.Count(d => d.Build && d.Shrine != null) == 9);
    Check("D3 the teammate table: 9 classes, 6 each, 54 distinct, the 9 companions included",
        data.Teammates.Count == 9 && data.Teammates.Values.All(v => v.Count == 6) && data.Teammates.Values.SelectMany(v => v).Distinct().Count() == 54
        && new[] { "TRLA_121", "TRLA_135", "TRLA_139", "TRLA_148", "TRLA_156", "TRLA_164", "TRLA_172", "TRLA_180", "TRLA_188" }.All(id => data.Teammates.Values.Any(v => v.Contains(id))));
    Check("D4 the shrine-upgrade pool lists TRLA_801..809", data.ShrineUpgrades.SequenceEqual(Enumerable.Range(801, 9).Select(n => "TRLA_" + n)));
    var schedules = data.Schedules;
    var v2 = schedules.Find("acts-v2");
    // The close-out (family 25): the defaults are acts-v3h and acts-v3t; a data set without the encounter file keeps acts-v2.
    // Step 9 (wave 9b): scheduleVersion 4, the defaults acts-v4h and acts-v4t.
    Check("D5 schedule data is scheduleVersion 4 (step 9; acts-v2 still stamps 2), defaults acts-v4h and acts-v4t (acts-v2 without the encounter file); acts-v1 and act1 keep their released shapes",
        schedules.ScheduleVersion == 4 && v2.StampVersion == 2 && schedules.Default == "acts-v4h" && schedules.Defaults.SequenceEqual(new[] { "acts-v4h", "acts-v4t" }) && schedules.DefaultWithoutEncounters == "acts-v2" && DungeonRunSchedules.ShapeDigest(schedules.Find("acts-v1")) == "3293D82FA6BD48B3"
        && DungeonRunSchedules.ShapeDigest(schedules.Find("act1")) == "3673E8AE7F914E4F",
        $"v{schedules.ScheduleVersion} {schedules.Default} {DungeonRunSchedules.ShapeDigest(schedules.Find("acts-v1"))}");
    Check("D6 acts-v2's shape is pinned for version 2 (ReleasedShapes[2])",
        DungeonRunSchedules.ReleasedShapes[2]["acts-v2"] == DungeonRunSchedules.ShapeDigest(v2), DungeonRunSchedules.ShapeDigest(v2));
    var tm = v2.Nodes.Where(n => n.TeammateDraft).Select(n => n.Act + "." + n.FloorInAct).ToList();
    var su = v2.Nodes.Where(n => n.ShrineUpgradeDraft).Select(n => n.Act + "." + n.FloorInAct).ToList();
    Check("D7 acts-v2: teammate drafts after I.2, I.4, II.3, III.3; shrine-upgrade drafts after the I and II majors; the same nodes as acts-v1",
        string.Join(",", tm) == "1.2,1.4,2.3,3.3" && string.Join(",", su) == "1.5,2.5" && v2.Nodes.Count == schedules.Find("acts-v1").Nodes.Count
        && schedules.Find("acts-v1").Nodes.All(n => !n.TeammateDraft && !n.ShrineUpgradeDraft), string.Join(",", tm) + " | " + string.Join(",", su));
    var v1only = Load(schedule: scheduleJson.Replace("\"scheduleVersion\": 4", "\"scheduleVersion\": 1"));
    Check("D8 the version edit took (the anchor is the shipped file's version)", v1only.Schedules.ScheduleVersion == 1);
    Check("D8 a version-1 file never reads the phase-7 keys", v1only.Schedules.Schedules.Values.All(s => s.Nodes.All(n => !n.TeammateDraft && !n.ShrineUpgradeDraft)));
    var errors = DungeonRunValidator.Validate(data, null);
    var mine = errors.Where(e => e.Contains("shrine") || e.Contains("teammate") || e.Contains("schedule")).ToList();
    Check("D9 the validator passes the shipped data's shrines, teammates, upgrades and schedules", mine.Count == 0, string.Join(" | ", mine.Take(4)));
    Check("D10 the validator passes the shipped data (no error at all)", errors.Count == 0, errors.Count + ": " + string.Join(" | ", errors.Take(3)));
    var bad = Load(run: runJson.Replace("\"TRLA_122\", \"TRLA_123\"", "\"TRLA_122\", \"TRLA_115\""));
    Check("D11 a shrine in the teammate table is refused", DungeonRunValidator.ValidateShrines(bad).Any(e => e.Contains("TRLA_115 is a shrine")));
    var wrongClass = Load(run: runJson.Replace("\"shrine\": \"TRLA_112\"", "\"shrine\": \"TRLA_108\""));
    Check("D12 a deck naming another class's shrine is refused", DungeonRunValidator.ValidateShrines(wrongClass).Any(e => e.Contains("TRLA_108 is MAGE")));
    var noForm = Load(schedule: scheduleJson.Replace("\"teammateAfter\": [2, 4]", "\"teammateAfter\": [2, 9]"));
    Check("D13 a teammate floor outside the act is refused", DungeonRunValidator.ValidateSchedules(noForm).Any(e => e.Contains("teammateAfter 9")));
    foreach (string id in ShrineRules.Scripted.Where(i => i != DungeonRunShrines.FixtureShrine))
        Check($"D14 scripted shrine {id}: its form {id}t points back (tag 1132) and is a 3-turn sleeper", C(id + "t")[(GameTag)1132] == C(id).AssetId
            && C(id + "t").Dormant?.Turns == 3 && C(ShrineRules.DormantCondition)[(GameTag)451] == 3 && C(id).Implemented && C(id + "t").Implemented);
    AdmissionVerdict V(string id, AdmitScope scope) => DungeonRunAdmission.Classify(null, id, scope, null, true);
    // Phase 7 T1 scripted TRLA_122 (the id this check named before): the first teammate still unscripted, null once all are.
    string pendingMate = data.Teammates.Values.SelectMany(v => v).OrderBy(x => x, StringComparer.Ordinal).FirstOrDefault(id => !DungeonRunShrines.ScriptedTeammates.Contains(id));
    // Phase 8 family 13: the shrines are a Rumble Run champion's too (Wardruid Loti's variants: the boss-shrine row).
    Check("D15 admission: the scripted shrines are admitted for the run player and (phase 8) a boss, the upgrades and teammates for the run player only",
        new[] { "TRLA_112", "TRLA_115", "TRLA_116", "TRLA_804", "TRLA_805", "TRLA_124", "TRLA_125", "TRLA_127" }
            .All(id => V(id, AdmitScope.Player).Admitted && V(id, AdmitScope.Boss).Admitted == (id == "TRLA_112" || id == "TRLA_115" || id == "TRLA_116") && !V(id, AdmitScope.Generated).Admitted));
    Check("D16 admission: unscripted shrines, forms, the TRLA_107 fixture, unscripted upgrades and teammates admit nothing",
        // Phase 7 T2 scripted TRLA_193 (named here before): pendingMate stands for the unscripted teammates.
        // Phase 7 D scripted TRLA_809 (named here before) and the companions: no upgrade or teammate is unscripted any more.
        new[] { pending?.Shrine, "TRLA_115t", "TRLA_107", "TRLA_107t", pendingMate }.Where(id => id != null).All(id => V(id, AdmitScope.Player).Subject && !V(id, AdmitScope.Player).Admitted
            && !V(id, AdmitScope.Generated).Admitted));
    var gen = new HashSet<string>(DungeonRunAdmission.GeneratedRejects(true));
    Check("D17 generation never yields a shrine, a form or a teammate (the Generated scope rejects them)",
        new[] { "TRLA_115", "TRLA_115t", "TRLA_124", "TRLA_127", "TRLA_193", "TRLA_107" }.All(gen.Contains));
});

// ================================================================ N NewRun and the plan
Guard("N", () =>
{
    var s = NewRun(Druid, "rumble_druid_115", 11);
    Check("N1 a new Rikkar Druid run on Gonk's Mark has the board shrine TRLA_115 (schedule acts-v2, version 2)",
        s.BoardShrine == "TRLA_115" && s.ScheduleVersion == 2 && s.Schedule == "acts-v2" && s.ShrineUpgrades.Count == 0, $"{s.BoardShrine} v{s.ScheduleVersion} {s.Schedule}");
    Check("N2 the shrine is never the run's hero power, a passive treasure or a deck card",
        s.Power == "CS2_017" && s.Shrine == null && !s.Treasures.Contains("TRLA_115") && !s.Deck.Contains("TRLA_115"));
    var plan = run.BuildPlan(s);
    Check("N3 the plan carries the shrine (one copy, no upgrades); its treasures stay passive treasures only",
        plan.PlayerShrine == "TRLA_115" && plan.PlayerShrineCopies == 1 && plan.PlayerShrineUpgrades.Count == 0 && !plan.PlayerTreasures.Contains("TRLA_115"));
    var mage = pending == null ? null : NewRun(pending.Value.Row, pending.Value.Deck, 12);
    Check("N4 a Rikkar deck whose shrine is not scripted yet plays without one (rejected until its shrine family scripts it; vacuous once all are)",
        mage == null || mage.BoardShrine == null && run.BuildPlan(mage).PlayerShrine == null, pending?.Deck ?? "none pending");
    var cls = NewRun("DRUID", null, 13);
    Check("N5 a class-row run has no shrine", cls.BoardShrine == null && run.BuildPlan(cls).PlayerShrine == null);
    var legacy = NewRun(Druid, "rumble_druid_115", 14, schedule: DungeonRunSchedules.LegacyId);
    Check("N6 a legacy-schedule run has no shrine", !legacy.IsActRun && legacy.BoardShrine == null);
    s.ShrineUpgrades.Add(DungeonRunShrines.EverlastingDevotion);
    var plan2 = run.BuildPlan(s);
    Check("N7 TRLA_804 makes the plan two copies", plan2.PlayerShrineCopies == 2 && plan2.PlayerShrineUpgrades.SequenceEqual(new[] { "TRLA_804" }));
    var bad = run.BuildPlan(cls);
    bad.PlayerShrine = pending?.Shrine ?? "TRLA_108t";
    bool refused = false;
    try { DungeonRunSetup.Configure(new GameConfig(), bad); } catch (ArgumentException) { refused = true; }
    Check("N8 Configure refuses an unscripted shrine", refused);
    var xml = Serialize(new DungeonRunSave { Active = s });
    Check("N9 the save writes BoardShrine and ShrineUpgrades", xml.Contains("BoardShrine=\"TRLA_115\"") && xml.Contains("<ShrineUpgrades>"));
    Check("N10 a run with no shrine writes neither field", !Serialize(new DungeonRunSave { Active = cls }).Contains("BoardShrine") && !Serialize(new DungeonRunSave { Active = cls }).Contains("ShrineUpgrades"));
});
string Serialize(DungeonRunSave save)
{
    var ser = new System.Xml.Serialization.XmlSerializer(typeof(DungeonRunSave));
    using var w = new StringWriter();
    ser.Serialize(w, save);
    return w.ToString();
}

// ================================================================ S the shrine lifecycle
Guard("S1", () =>
{
    var (s, plan, g) = Fight("rumble_druid_115", 101);
    var shrine = ShrineOf(g, g.Player1);
    Check("S1 Gonk's Mark is on the run player's board after Start of Game: left-most slot, marked, 0/3, an ordinary minion",
        shrine != null && shrine.Card.Id == "TRLA_115" && shrine.ZonePosition == 0 && shrine.AttackDamage == 0 && shrine.Health == 3
        && !shrine.Untouchable && !shrine.IsDormant && ShrineRules.IsAwake(shrine), $"{shrine?.Card.Id} pos {shrine?.ZonePosition} {shrine?.AttackDamage}/{shrine?.Health}");
    Check("S1 nothing is left in the set-aside zone and the boss has no shrine", !g.Player1.SetasideZone.Any(e => e.Card.Id == "TRLA_115") && ShrineRules.Of(g.Player2).Count == 0);
    Check("S1 the shrine entered through the history (a FULL_ENTITY the client is sent)",
        g.PowerHistory.Last.Any(h => h is SabberStoneCore.Kettle.PowerHistoryFullEntity f && f.Entity.Id == shrine.Id) || g.PowerHistory.Full.Any(h => h is SabberStoneCore.Kettle.PowerHistoryFullEntity f && f.Entity.Id == shrine.Id));
    Mana(g.Player1, 10); ClearExceptShrines(g);
    var raptor = (Minion)ToHand(g, g.Player1, "CS2_172"); Play(g, raptor);
    Check("S2 TRLA_115: a minion the owner summons gets +1/+1 (Bloodfen Raptor 3/2 -> 4/3)", raptor.AttackDamage == 4 && raptor.Health == 3, $"{raptor.AttackDamage}/{raptor.Health}");
    Check("S2 the shrine does not buff itself", shrine.AttackDamage == 0 && shrine.Health == 3);
    var enemy = Board(g, g.Player2, "CS2_120");
    Check("S2 an enemy summon is not buffed", enemy.AttackDamage == 2 && enemy.Health == 3);
});
Guard("S3", () =>
{
    // Death on the owner's turn: dormant in the same slot, Deathrattle-free shrine leaves a sleeper; revival at the start of
    // the owner's third turn after the death (turn 1 death -> turn 7).
    var (s, plan, g) = Fight("rumble_druid_115", 102);
    Mana(g.Player1, 10); ClearExceptShrines(g);
    var left = Board(g, g.Player1, "CS2_120");
    var shrine = ShrineOf(g, g.Player1);
    g.Process(MinionAttackTask.Any(g.Player1, left, g.Player2.Hero)); // nothing to do with the shrine
    int slot = shrine.ZonePosition, deaths = g.HistoryFacts.Query.Deaths().Count(), died = g.Player1.NumFriendlyMinionsThatDiedThisTurn, oldId = shrine.Id;
    Damage(g, shrine, 5);
    var form = ShrineOf(g, g.Player1);
    Check("S3 a shrine killed by damage goes dormant in the slot it left (its TRLA_115t form, untouchable, marked)",
        form != null && form.Card.Id == "TRLA_115t" && form.ZonePosition == slot && form.IsDormant && form.Untouchable && ShrineRules.IsDormantForm(form)
        && !ShrineRules.IsAwake(form), $"{form?.Card.Id} pos {form?.ZonePosition}/{slot} dormant {form?.IsDormant}");
    Check("S3 the death counts: a death fact, NumFriendlyMinionsThatDiedThisTurn, the corpse in the graveyard",
        g.HistoryFacts.Query.Deaths().Count() == deaths + 1 && g.Player1.NumFriendlyMinionsThatDiedThisTurn == died + 1
        && g.Player1.GraveyardZone.Any(e => e.Id == oldId && ShrineRules.IsCorpse(e)));
    Check("S3 the dormant form shows 3 turns", ShrineRules.TurnsLeft(form) == 3, ShrineRules.TurnsLeft(form).ToString());
    var targets = g.Player1.Options().OfType<PlayCardTask>().SelectMany(o => new[] { o.Target }).ToList();
    var raptor = (Minion)ToHand(g, g.Player1, "CS2_172"); Play(g, raptor);
    Check("S3 the dormant form has no trigger (a summon gets no +1/+1)", raptor.AttackDamage == 3, raptor.AttackDamage.ToString());
    var aoe = ToHand(g, g.Player1, "CS2_062"); // Hellfire: 3 damage to ALL characters
    Play(g, aoe);
    Check("S3 an AoE does not touch the dormant form", ShrineOf(g, g.Player1) == form && form.IsDormant && form.Damage == 0);
    var seen = new List<string>();
    for (int t = 3; t <= 7; t += 2)
    {
        ToTurn(g, t);
        var now = ShrineOf(g, g.Player1);
        seen.Add($"t{t}:{now?.Card.Id}/{(now?.IsDormant == true ? ShrineRules.TurnsLeft(now) : 0)}");
    }
    Check("S4 death on the owner's turn 1: dormant 2 at turn 3, 1 at turn 5, revived at the start of turn 7",
        string.Join(" ", seen) == "t3:TRLA_115t/2 t5:TRLA_115t/1 t7:TRLA_115/0", string.Join(" ", seen));
    var revived = ShrineOf(g, g.Player1);
    Check("S5 revived: a fresh entity at full Health in the same slot, marked, generation 1, awake",
        revived.Id != oldId && revived.Id != form.Id && revived.Health == 3 && revived.Damage == 0 && revived.ZonePosition == slot
        && ShrineRules.IsAwake(revived) && revived[ShrineRules.Generation] == 1, $"id {revived.Id} hp {revived.Health} pos {revived.ZonePosition} gen {revived[ShrineRules.Generation]}");
    Mana(g.Player1, 10);
    var second = (Minion)ToHand(g, g.Player1, "CS2_172"); Play(g, second);
    Check("S5 its trigger is back (a summon gets +1/+1 again)", second.AttackDamage == 4, second.AttackDamage.ToString());
});
Guard("S6", () =>
{
    // Death on the enemy's turn 2: revival still at the owner's third turn after it (turn 7).
    var (s, plan, g) = Fight("rumble_druid_115", 103);
    EndTurn(g); Mana(g.Player2, 10);
    var shrine = ShrineOf(g, g.Player1);
    var fireball = ToHand(g, g.Player2, "CS2_029");
    Play(g, fireball, shrine);
    var form = ShrineOf(g, g.Player1);
    Check("S6 the boss's Fireball kills the shrine on the boss's turn: dormant", form?.Card.Id == "TRLA_115t" && form.IsDormant);
    var seen = new List<string>();
    for (int t = 3; t <= 7; t += 2) { ToTurn(g, t); var now = ShrineOf(g, g.Player1); seen.Add($"t{t}:{now?.Card.Id}"); }
    Check("S6 death on the enemy's turn 2: revived at the start of the owner's turn 7", string.Join(" ", seen) == "t3:TRLA_115t t5:TRLA_115t t7:TRLA_115", string.Join(" ", seen));
});
Guard("S7", () =>
{
    var (s, plan, g) = Fight("rumble_druid_115", 104);
    EndTurn(g); Mana(g.Player2, 10);
    var shrine = ShrineOf(g, g.Player1);
    Play(g, ToHand(g, g.Player2, "CS2_076"), shrine); // Assassinate
    Check("S7 a destroy effect (Assassinate) sends the shrine dormant", ShrineOf(g, g.Player1)?.Card.Id == "TRLA_115t");
});
Guard("S8", () =>
{
    var (s, plan, g) = Fight("rumble_druid_115", 105);
    EndTurn(g); Mana(g.Player2, 10);
    var shrine = ShrineOf(g, g.Player1);
    bool played = Play(g, ToHand(g, g.Player2, "EX1_246"), shrine); // Hex
    var now = ShrineOf(g, g.Player1);
    Check("S8 Hex on the shrine destroys it instead (dormant, no Frog on the board)", played && now?.Card.Id == "TRLA_115t"
        && !g.Player1.BoardZone.Any(m => m.Card.Id == "hexfrog"), $"played {played} {now?.Card.Id} board {string.Join(",", g.Player1.BoardZone.Select(m => m.Card.Id))}");
    var poly = ToHand(g, g.Player2, "CS2_022"); // Polymorph cannot target the dormant form
    var polyTargets = g.Player2.Options().OfType<PlayCardTask>().Where(o => o.Source == poly).Select(o => o.Target).ToList();
    Check("S8 the dormant form cannot be targeted (Polymorph's options do not name it)", !polyTargets.Contains(now), string.Join(",", polyTargets.Select(t => t?.Card.Id ?? "-")));
});
Guard("S9", () =>
{
    var (s, plan, g) = Fight("rumble_druid_115", 106);
    EndTurn(g); Mana(g.Player2, 10);
    var shrine = ShrineOf(g, g.Player1);
    int hand = g.Player1.HandZone.Count;
    bool played = Play(g, ToHand(g, g.Player2, "EX1_581"), shrine); // Sap
    Check("S9 Sap (return to hand) destroys the shrine instead: dormant, not in the owner's hand",
        played && ShrineOf(g, g.Player1)?.Card.Id == "TRLA_115t" && g.Player1.HandZone.Count == hand && !g.Player1.HandZone.Any(c => c.Card.Id == "TRLA_115"));
});
Guard("S10", () =>
{
    var (s, plan, g) = Fight("rumble_druid_115", 107);
    EndTurn(g); Mana(g.Player2, 10);
    var shrine = ShrineOf(g, g.Player1);
    bool played = Play(g, ToHand(g, g.Player2, "GVG_031"), shrine); // Recycle: a copy into the owner's deck, the target removed
    Check("S10 Recycle (shuffle into a deck) destroys the shrine instead: dormant; the copy in the deck is a plain card",
        played && ShrineOf(g, g.Player1)?.Card.Id == "TRLA_115t" && g.Player1.DeckZone.Where(c => c.Card.Id == "TRLA_115").All(c => !ShrineRules.IsMarked(c)),
        $"{ShrineOf(g, g.Player1)?.Card.Id}");
});
Guard("S11", () =>
{
    var (s, plan, g) = Fight("rumble_druid_115", 108);
    EndTurn(g); Mana(g.Player2, 10);
    var shrine = ShrineOf(g, g.Player1);
    var mind = ToHand(g, g.Player2, "CS1_113");
    bool played = Play(g, mind, shrine); // Mind Control
    // Phase 7 review: a shrine cannot change controller, so it is not a valid Mind Control target (the play is refused).
    Check("S11 Mind Control cannot target the shrine: the play is refused, the shrine stays the owner's and awake, card and Mana kept",
        !played && shrine.Controller == g.Player1 && ShrineRules.IsAwake(shrine) && g.Player2.BoardZone.Count == 0 && g.Player2.UsedMana == 0 && mind.Zone == g.Player2.HandZone);
    Mana(g.Player2, 10);
    var faceless = ToHand(g, g.Player2, "EX1_564");
    Play(g, faceless, shrine);
    var copy = g.Player2.BoardZone.FirstOrDefault(m => m.Card.Id == "TRLA_115");
    Check("S12 Faceless Manipulator copies it as a plain minion (no mark), keeping its text", copy != null && !ShrineRules.IsMarked(copy),
        $"p2 [{string.Join(",", g.Player2.BoardZone.Slots.Select(m => m.Card.Id + (ShrineRules.IsMarked(m) ? "*" : "")))}] p1 [{string.Join(",", g.Player1.BoardZone.Slots.Select(m => m.Card.Id + (ShrineRules.IsMarked(m) ? "*" : "")))}]");
    var boar = Board(g, g.Player2, "CS2_171");
    Check("S12 the copy's text works for its new owner (Stonetusk Boar 1/1 -> 2/2)", boar.AttackDamage == 2 && boar.Health == 2, $"{boar.AttackDamage}/{boar.Health}");
    Kill(g, copy);
    Check("S12 the copy dies for good (no dormant form on the boss's board)", g.Player2.BoardZone.All(m => m.Card.Id != "TRLA_115t") && ShrineRules.Of(g.Player2).Count == 0);
});
Guard("S13", () =>
{
    var (s, plan, g) = Fight("rumble_druid_115", 109);
    EndTurn(g); Mana(g.Player2, 10);
    var shrine = ShrineOf(g, g.Player1);
    Play(g, ToHand(g, g.Player2, "EX1_332"), shrine); // Silence
    Check("S13 a silenced shrine stays a run shrine (marked, awake)", shrine.IsSilenced && ShrineRules.IsAwake(shrine));
    EndTurn(g); Mana(g.Player1, 10);
    var raptor = (Minion)ToHand(g, g.Player1, "CS2_172"); Play(g, raptor);
    Check("S13 silenced: its text is gone (a summon gets no +1/+1)", raptor.AttackDamage == 3, raptor.AttackDamage.ToString());
    Kill(g, shrine);
    Check("S13 a silenced shrine still goes dormant", ShrineOf(g, g.Player1)?.Card.Id == "TRLA_115t");
    ToTurn(g, 9);
    var back = ShrineOf(g, g.Player1);
    Mana(g.Player1, 10);
    var raptor2 = (Minion)ToHand(g, g.Player1, "CS2_172"); Play(g, raptor2);
    Check("S13 it comes back un-silenced with its text", back?.Card.Id == "TRLA_115" && !back.IsSilenced && raptor2.AttackDamage == 4, $"{back?.Card.Id} silenced {back?.IsSilenced} raptor {raptor2.AttackDamage}");
});
Guard("S14", () =>
{
    var (s, plan, g) = Fight("rumble_druid_115", 110);
    Mana(g.Player1, 10); ClearExceptShrines(g);
    Kill(g, ShrineOf(g, g.Player1));
    var res = ToHand(g, g.Player1, "BRM_017"); // Resurrect
    int before = g.Player1.BoardZone.Count;
    bool played = Play(g, res);
    Check("S14 the shrine's death counts for \"a friendly minion died\" (Resurrect is playable) but it is not in the pool (nothing summoned)",
        played && g.Player1.BoardZone.Count == before && !g.Player1.BoardZone.Any(m => m.Card.Id == "TRLA_115"), $"played {played} board {string.Join(",", g.Player1.BoardZone.Select(m => m.Card.Id))}");
    var raptor = Board(g, g.Player1, "CS2_172"); Kill(g, raptor);
    Play(g, ToHand(g, g.Player1, "BRM_017"));
    Check("S14 an ordinary dead minion is still resurrected", g.Player1.BoardZone.Any(m => m.Card.Id == "CS2_172"));
    Check("S14 the graveyard pool (IncludeTask GRAVEYARD) leaves the corpse out",
        SabberStoneCore.Tasks.SimpleTasks.IncludeTask.GetEntities(SabberStoneCore.Tasks.SimpleTasks.EntityType.GRAVEYARD, g.Player1, g.Player1.Hero, null, null).All(e => !ShrineRules.IsCorpse(e))
        && g.Player1.GraveyardZone.Any(ShrineRules.IsCorpse));
});
Guard("S15", () =>
{
    // Board full at death: the shrine and six minions (two Harvest Golems) die to the boss's Flamestrike-like AoE together.
    var (s, plan, g) = Fight("rumble_druid_115", 111);
    Mana(g.Player1, 10); ClearExceptShrines(g);
    foreach (string id in new[] { "EX1_556", "CS2_120", "EX1_556", "CS2_120", "CS2_120", "CS2_120" }) Board(g, g.Player1, id);
    Check("S15 the board is full (shrine + 6)", g.Player1.BoardZone.IsFull);
    EndTurn(g); Mana(g.Player2, 10);
    Play(g, ToHand(g, g.Player2, "CS2_032")); // Flamestrike: 4 to enemy minions
    var ids = g.Player1.BoardZone.Slots.Select(m => m.Card.Id).ToList();
    Check("S15 a full board at death: the dormant form takes the shrine's slot and both Golems' Deathrattles still summon",
        ids.Count(i => i == "TRLA_115t") == 1 && ids.Count(i => i == "skele21") == 2, string.Join(",", ids));
});
Guard("S16", () =>
{
    var (s, plan, g) = Fight("rumble_druid_112", 112);
    var shrine = ShrineOf(g, g.Player1);
    Mana(g.Player1, 10); g.Player1.UsedMana = 6;
    bool used = g.Process(HeroPowerTask.Any(g.Player1));
    Check("S16 TRLA_112 Gonk's Armament: gaining Armor (Shapeshift) refreshes the Mana Crystals", shrine?.Card.Id == "TRLA_112" && used && g.Player1.Hero.Armor >= 1
        && g.Player1.RemainingMana == 10, $"{shrine?.Card.Id} used {used} armor {g.Player1.Hero.Armor} mana {g.Player1.RemainingMana}");
});
Guard("S17", () =>
{
    var (s, plan, g) = Fight("rumble_druid_116", 113);
    var shrine = ShrineOf(g, g.Player1);
    Mana(g.Player1, 10); ClearExceptShrines(g);
    Play(g, ToHand(g, g.Player1, "CS2_106")); // Fiery War Axe 3/2
    int hero = g.Player1.Hero.AttackDamage;
    EndTurn(g);
    Check("S17 TRLA_116 Bonds of Balance: at the end of its owner's turn it gains Attack equal to the hero's (3)", shrine?.Card.Id == "TRLA_116" && shrine.AttackDamage == hero && hero == 3,
        $"{shrine?.Card.Id} {shrine?.AttackDamage} hero {hero} trig {shrine?.ActivatedTrigger != null} ench [{string.Join(",", shrine?.AppliedEnchantments?.Select(e => e.Card.Id) ?? new string[0])}] turn {g.Turn}");
    EndTurn(g);
    int hp = g.Player2.Hero.Health;
    bool attacked = g.Process(MinionAttackTask.Any(g.Player1, shrine, g.Player2.Hero));
    Check("S17 a shrine with Attack can attack (no CANT_ATTACK invented)", attacked && g.Player2.Hero.Health == hp - 3, $"{attacked} {hp}->{g.Player2.Hero.Health}");
    EndTurn(g);
    Check("S17 the end of the owner's next turn grows it again (3 + 3)", shrine.AttackDamage == 6, shrine.AttackDamage.ToString());
    EndTurn(g);
    Check("S17 the boss's turn does not grow it", shrine.AttackDamage == 6, shrine.AttackDamage.ToString());
});
Guard("S18", () =>
{
    // Darius's Cannons with a shrine placed by the test hook (no Darius row has a shrine): it sits between the edge Cannons.
    var darius = data.Class("GILA_600h");
    var s = run.NewRun(all, darius.Id, darius.DefaultDeck, darius.DefaultPower, 114);
    var plan = run.BuildPlan(s);
    var g = Start(plan, run.MatchSeed(s));
    g.Process(ChooseTask.Mulligan(g.Player1, new List<int>())); g.Process(ChooseTask.Mulligan(g.Player2, new List<int>()));
    g.MainBegin();
    var placed = ShrineRules.Place(g.Player1, "TRLA_115");
    g.DeathProcessingAndAuraUpdate();
    var ids = g.Player1.BoardZone.Slots.Select(m => m.Card.Id).ToList();
    Check("S18 a shrine placed on Darius's board sits between his edge Cannons", ids.Count == 3 && ids[0] == Cannons.CannonId && ids[1] == "TRLA_115" && ids[2] == Cannons.CannonId, string.Join(",", ids));
    Kill(g, placed);
    ids = g.Player1.BoardZone.Slots.Select(m => m.Card.Id).ToList();
    Check("S18 its dormant form keeps the slot between them", string.Join(",", ids) == "GILA_601,TRLA_115t,GILA_601", string.Join(",", ids));
});
Guard("S19", () =>
{
    // The TRLA_107 Tribal Shrine fixture on the boss side (test hook): a 0/1 Taunt the run player must attack; Amulet of
    // Domination cannot take it; it goes dormant on the boss's board.
    var (s, plan, g) = Fight("rumble_druid_115", 115);
    var fixture = ShrineRules.Place(g.Player2, DungeonRunShrines.FixtureShrine);
    g.DeathProcessingAndAuraUpdate();
    Mana(g.Player1, 10); ClearExceptShrines(g);
    var axe = ToHand(g, g.Player1, "CS2_106"); Play(g, axe);
    var attacks = g.Player1.Options().OfType<HeroAttackTask>().Select(o => o.Target).ToList();
    Check("S19 the fixture is a Taunt: the hero can only attack it", fixture.HasTaunt && attacks.Count == 1 && attacks[0] == fixture, string.Join(",", attacks.Select(t => t.Card.Id)));
    var amulet = ToHand(g, g.Player1, "LOOTA_805");
    bool played = Play(g, amulet, fixture);
    Check("S19 Amulet of Domination (control) on the fixture does nothing", fixture.Controller == g.Player2 && ShrineRules.IsAwake(fixture), $"played {played}");
    g.Process(HeroAttackTask.Any(g.Player1, fixture));
    Check("S19 killed, the fixture goes dormant on the boss's board", ShrineOf(g, g.Player2)?.Card.Id == "TRLA_107t");
});

// ================================================================ U shrine upgrades
Guard("U1", () =>
{
    var (s, plan, g) = Fight("rumble_druid_115", 201, upgrades: new[] { DungeonRunShrines.EverlastingDevotion });
    var shrines = ShrineRules.Of(g.Player1);
    Check("U1 TRLA_804: two shrines start on the board, side by side in the two left-most slots",
        shrines.Count == 2 && shrines[0].ZonePosition == 0 && shrines[1].ZonePosition == 1 && shrines.All(ShrineRules.IsAwake), string.Join(",", g.Player1.BoardZone.Slots.Select(m => m.Card.Id)));
    Mana(g.Player1, 10); ClearExceptShrines(g);
    var raptor = (Minion)ToHand(g, g.Player1, "CS2_172"); Play(g, raptor);
    Check("U1 both triggers work (+2/+2)", raptor.AttackDamage == 5 && raptor.Health == 4, $"{raptor.AttackDamage}/{raptor.Health}");
    Kill(g, shrines[1]);
    Check("U1 each copy goes dormant on its own", ShrineRules.Of(g.Player1).Count(m => m.IsDormant) == 1 && ShrineRules.Of(g.Player1).Count(ShrineRules.IsAwake) == 1);
});
Guard("U2", () =>
{
    var (s, plan, g) = Fight("rumble_druid_115", 202, upgrades: new[] { DungeonRunShrines.PactWithTheLoa });
    Check("U2 TRLA_805 attaches its player enchantment", g.Player1.AppliedEnchantments.Any(e => e.Card.Id == ShrineRules.FasterRevivalEnchantment) && ShrineRules.ReviveTurns(g.Player1) == 2);
    Kill(g, ShrineOf(g, g.Player1));
    var form = ShrineOf(g, g.Player1);
    var seen = new List<string> { $"t1:{ShrineRules.TurnsLeft(form)}" };
    for (int t = 3; t <= 5; t += 2) { ToTurn(g, t); var now = ShrineOf(g, g.Player1); seen.Add($"t{t}:{now.Card.Id}"); }
    Check("U2 TRLA_805: a shrine that dies on turn 1 shows 2 turns and revives at the start of turn 5", string.Join(" ", seen) == "t1:2 t3:TRLA_115t t5:TRLA_115", string.Join(" ", seen));
    var boss = ShrineRules.ReviveTurns(g.Player2);
    Check("U2 the upgrade is its owner's only (the boss's shrines would still sleep 3)", boss == 3);
});

// ================================================================ T teammates in real games
Guard("T1", () =>
{
    var (s, plan, g) = Fight("rumble_druid_115", 301);
    Mana(g.Player1, 10); ClearExceptShrines(g);
    var striker = (Minion)ToHand(g, g.Player1, "TRLA_124"); Play(g, striker);
    int before = striker.AttackDamage;
    Play(g, ToHand(g, g.Player1, "CS2_106"));
    g.Process(HeroAttackTask.Any(g.Player1, g.Player2.Hero));
    var shrine = ShrineOf(g, g.Player1);
    Check("T1 TRLA_124 Zandalari Striker: after the hero attacks, the owner's minions get +2 Attack (the shrine included)",
        striker.AttackDamage == before + 2 && shrine.AttackDamage == 2, $"{before}->{striker.AttackDamage} shrine {shrine.AttackDamage}");
});
Guard("T2", () =>
{
    var (s, plan, g) = Fight("rumble_druid_115", 302);
    Mana(g.Player1, 10); ClearExceptShrines(g);
    var fan = Board(g, g.Player1, "TRLA_125");
    var victim = Board(g, g.Player2, "CS2_171"); // a 1/1 Boar
    EndTurn(g); EndTurn(g); Mana(g.Player1, 10);
    int heroAttack = g.Player1.Hero.AttackDamage;
    bool attacked = g.Process(MinionAttackTask.Any(g.Player1, fan, victim));
    Check("T2 TRLA_125 The Fan Favorite: Overkill gives the hero +10 Attack this turn", attacked && victim.IsDead && g.Player1.Hero.AttackDamage == heroAttack + 10,
        $"{attacked} hero {g.Player1.Hero.AttackDamage}");
    EndTurn(g);
    Check("T2 the +10 lasts this turn only", g.Player1.Hero.AttackDamage == heroAttack, g.Player1.Hero.AttackDamage.ToString());
});
Guard("T3", () =>
{
    var (s, plan, g) = Fight("rumble_druid_115", 303);
    Mana(g.Player1, 10); ClearExceptShrines(g);
    var victim = Board(g, g.Player2, "CS2_171");
    var raptor = (Minion)ToHand(g, g.Player1, "TRLA_127"); Play(g, raptor);
    bool attacked = g.Process(MinionAttackTask.Any(g.Player1, raptor, victim));
    var raptors = g.Player1.BoardZone.Count(m => m.Card.Id == "TRLA_127");
    Check("T3 TRLA_127 Astral Raptor: Rush; Overkill summons another (two on the board; the summon gets Gonk's Mark)",
        attacked && victim.IsDead && raptors == 2 && g.Player1.BoardZone.Where(m => m.Card.Id == "TRLA_127").All(m => m.AttackDamage == 4), $"{attacked} raptors {raptors}");
});

// ================================================================ T drafts through the run flow
DungeonRunState Walk(DungeonRunState s, Func<DungeonRunState, bool> stop, List<string> log)
{
    var scratch = new DungeonRunProfile();
    for (int guard = 0; guard < 500 && !stop(s) && !s.Finished; guard++)
    {
        switch (s.Phase)
        {
            case DungeonRunPhase.PreMatch: run.BeginMatch(s); run.RecordOutcome(s, scratch, DungeonRunOutcome.Win); break;
            case DungeonRunPhase.TreasureDraft:
                log?.Add($"f{s.Floor}:{s.OpenDraft.Kind}[{string.Join(",", s.OpenDraft.Offered)}]");
                run.PickTreasure(s, s.OpenDraft.Offered[0]); break;
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
    return s;
}
Guard("T4", () =>
{
    var s = NewRun(Druid, "rumble_druid_115", 401);
    var log = new List<string>();
    Walk(s, x => x.Phase == DungeonRunPhase.TreasureDraft && x.OpenDraft.Kind == DungeonRun.TeammateKind, log);
    var draft = s.OpenDraft;
    // Phase 7 T1: the Druid pool is its five scripted teammates (TRLA_122/123 joined), so the offer is 3 of them, drawn from the run seed.
    var druidMates = data.Teammates["DRUID"].Where(DungeonRunShrines.ScriptedTeammates.Contains).ToList();
    Check("T4 the first teammate draft opens after the act I floor-2 fight, on the treasure screen, offering 3 scripted Druid teammates (TRLA_125 among them)",
        s.Floor == 2 && draft?.Kind == "teammate" && s.Phase == DungeonRunPhase.TreasureDraft && draft.Offered.Count == 3 && draft.Offered.All(druidMates.Contains)
        && draft.Offered.Contains("TRLA_125"),
        $"floor {s.Floor} {draft?.Kind} [{string.Join(",", draft?.Offered ?? new List<string>())}] log {string.Join(" ", log)}");
    // Save and resume with the draft open: the stored offer is kept, never redrawn.
    var saved = Serialize(new DungeonRunSave { Active = s });
    var ser = new System.Xml.Serialization.XmlSerializer(typeof(DungeonRunSave));
    var back = ((DungeonRunSave)ser.Deserialize(new StringReader(saved))).Active;
    run.Resume(back);
    Check("T4 a save and resume keeps the open teammate draft and its offer", back.OpenDraft?.Kind == "teammate" && back.OpenDraft.Offered.SequenceEqual(draft.Offered) && back.Phase == DungeonRunPhase.TreasureDraft);
    int deck = back.Deck.Count;
    run.PickTreasure(back, "TRLA_125");
    var card = back.Cards.LastOrDefault();
    Check("T4 the pick joins the deck as a card record with origin \"teammate\", then the bundle draft opens",
        back.Deck.Count == deck + 1 && card?.Id == "TRLA_125" && card.Origin == DungeonRunDeck.OriginTeammate && back.Phase == DungeonRunPhase.BundleDraft,
        $"{card?.Id}/{card?.Origin} {back.Phase}");
    bool refused = false;
    try { run.PickTreasure(back, "TRLA_124"); } catch (InvalidOperationException) { refused = true; }
    Check("T4 a second pick is refused", refused);
    var log2 = new List<string>();
    Walk(back, x => x.Phase == DungeonRunPhase.TreasureDraft && x.OpenDraft.Kind == DungeonRun.TeammateKind, log2);
    Check("T5 the floor-4 teammate draft never offers the teammate already taken", back.Floor == 4 && back.OpenDraft.Offered.Count == Math.Min(3, druidMates.Count - 1)
        && !back.OpenDraft.Offered.Contains("TRLA_125") && back.OpenDraft.Offered.All(druidMates.Contains),
        $"floor {back.Floor} [{string.Join(",", back.OpenDraft.Offered)}]");
    run.PickTreasure(back, back.OpenDraft.Offered[0]);
    var log3 = new List<string>();
    Walk(back, x => x.Phase == DungeonRunPhase.TreasureDraft && x.OpenDraft.Kind == DungeonRun.ShrineUpgradeKind, log3);
    // Phase 7 U: the pool is the scripted, admitted upgrades (TRLA_801..808), so the draft offers 3 of them, drawn from the run
    // seed; the checks read the offer instead of naming it (c-shrine-upgrades A12-A16 test the draw and the grants).
    var scriptedUpgrades = DungeonRunShrines.ScriptedUpgrades.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();
    var offer1 = back.OpenDraft?.Offered ?? new List<string>();
    Check("T6 the shrine-upgrade draft opens after the act I major (floor 5), after its treasure draft, offering 3 scripted upgrades",
        back.Floor == 5 && offer1.Count == Math.Min(3, scriptedUpgrades.Count) && offer1.All(scriptedUpgrades.Contains), $"floor {back.Floor} [{string.Join(",", offer1)}] {string.Join(" ", log3)}");
    string pick1 = offer1.Contains("TRLA_805") ? "TRLA_805" : offer1[0];
    run.PickTreasure(back, pick1);
    Check("T6 the upgrade joins ShrineUpgrades, not Treasures, and the plan carries it",
        back.ShrineUpgrades.SequenceEqual(new[] { pick1 }) && !back.Treasures.Contains(pick1) && back.Phase == DungeonRunPhase.BundleDraft);
    var log4 = new List<string>();
    Walk(back, x => x.Phase == DungeonRunPhase.TreasureDraft && x.OpenDraft.Kind == DungeonRun.ShrineUpgradeKind || x.Floor > 10, log4);
    var offer2 = back.OpenDraft?.Offered ?? new List<string>();
    Check("T7 the act II shrine-upgrade draft offers only upgrades not held", back.Floor == 10 && back.OpenDraft?.Kind == DungeonRun.ShrineUpgradeKind
        && offer2.Count == Math.Min(3, scriptedUpgrades.Count - 1) && !offer2.Contains(pick1) && offer2.All(scriptedUpgrades.Contains),
        $"floor {back.Floor} {back.OpenDraft?.Kind} [{string.Join(",", offer2)}] {string.Join(" ", log4)}");
    Check("T7 the act II floor-3 teammate draft offered the last scripted teammate", log4.Any(l => l == "f8:teammate[TRLA_127]") || log4.Any(l => l.StartsWith("f8:teammate[")), string.Join(" ", log4));
    string pick2 = offer2.Contains("TRLA_804") ? "TRLA_804" : offer2[0];
    run.PickTreasure(back, pick2);
    var walked = Walk(back, x => x.Phase == DungeonRunPhase.PreMatch && x.Floor == 11, null);
    var plan = run.BuildPlan(walked);
    var picks = new[] { pick1, pick2 };
    Check("T8 a later fight's plan: the shrine, its copies (two with TRLA_804), both upgrades in pick order", plan.PlayerShrine == "TRLA_115"
        && plan.PlayerShrineCopies == (picks.Contains("TRLA_804") ? 2 : 1) && plan.PlayerShrineUpgrades.SequenceEqual(picks), string.Join(",", picks));
    var g = Start(plan, run.MatchSeed(walked));
    g.Process(ChooseTask.Mulligan(g.Player1, new List<int>())); g.Process(ChooseTask.Mulligan(g.Player2, new List<int>()));
    g.MainBegin();
    Check("T8 that fight starts with the planned shrines and revival timer", ShrineRules.Of(g.Player1).Count == plan.PlayerShrineCopies
        && ShrineRules.ReviveTurns(g.Player1) == (picks.Contains("TRLA_805") ? 2 : 3));
    var drafted = walked.Cards.Where(c => c.Origin == DungeonRunDeck.OriginTeammate).Select(c => c.Id).ToList();
    Check("T8 the run's teammates are in the plan's deck", drafted.All(id => plan.PlayerDeck.Any(c => c.Id == id)) && drafted.Count >= 2, string.Join(",", drafted));
});
Guard("T9", () =>
{
    var s = NewRun("DRUID", null, 402);
    var log = new List<string>();
    Walk(s, x => x.Floor >= 11, log);
    Check("T9 a class-row run on acts-v2 never opens a teammate or shrine-upgrade draft", log.All(l => !l.Contains("teammate") && !l.Contains("shrine-upgrade")), string.Join(" ", log));
    var log2 = new List<string>();
    if (pending != null) Walk(NewRun(pending.Value.Row, pending.Value.Deck, 403), x => x.Floor >= 6, log2);
    Check("T9 a Rikkar run whose shrine is unscripted opens none either", log2.All(l => !l.Contains("teammate") && !l.Contains("shrine-upgrade")), string.Join(" ", log2));
});

// ================================================================ V save version and the phase-6 run
Guard("V", () =>
{
    // A fixed folder beside the suite; only its two files are replaced (no folder is ever deleted).
    string dir = Path.Combine(AppContext.BaseDirectory, "save-fixture");
    Directory.CreateDirectory(dir);
    foreach (string f in new[] { DungeonRunStore.FileName, "dungeon-run.v4.xml" }) if (File.Exists(Path.Combine(dir, f))) File.Delete(Path.Combine(dir, f));
    {
        // A phase-6 Rikkar run, as a phase-6 build wrote it: the run starts on the phase-6 data (run-v1.json, schedule-v1.json
        // and catalog-v1.json as they were before phase 7: scheduleVersion 1, default acts-v1), so NewRun itself stamps schedule
        // version 1 and gives no shrine; no field is edited by hand.
        string before = VerifyPaths.Fixture("p7/before");
        var p6data = DungeonRunData.Parse(File.ReadAllText(Path.Combine(before, "run-v1.json")), R("dungeon-run/pool-v1.json"), R("npc/decks-v1.json"),
            File.ReadAllText(Path.Combine(before, "catalog-v1.json")), File.ReadAllText(Path.Combine(before, "schedule-v1.json")), R("dungeon-run/tavern-v1.json"));
        var p6run = new DungeonRun(p6data);
        var p6all = new DungeonRunProfile(); p6all.Unlocked.AddRange(p6data.Unlocks.Keys);
        var p6cls = p6data.Class(Druid);
        var s = p6run.NewRun(p6all, p6cls.Id, "rumble_druid_115", p6cls.DefaultPower, 501, signatureId: p6run.SignatureOffer(p6all, p6data.FindClass(p6cls.Id), 501).FirstOrDefault());
        Check("V1 the fixture is a phase-6 run: phase-6 data (scheduleVersion 1) stamps schedule version 1 on acts-v1 and gives no shrine",
            p6data.Schedules.ScheduleVersion == 1 && s.ScheduleVersion == 1 && s.Schedule == "acts-v1" && s.BoardShrine == null && s.ShrineUpgrades.Count == 0,
            $"data v{p6data.Schedules.ScheduleVersion} run v{s.ScheduleVersion} {s.Schedule} shrine {s.BoardShrine ?? "-"}");
        var save = new DungeonRunSave { Active = s };
        string xml = Serialize(save).Replace("Version=\"6\"", "Version=\"4\"").Replace("encoding=\"utf-16\"", "encoding=\"utf-8\"");
        File.WriteAllText(Path.Combine(dir, DungeonRunStore.FileName), xml);
        var loaded = DungeonRunStore.Load(dir);
        var log = new List<string>();
        bool up = DungeonRunMigration.Upgrade(loaded, run, log.Add);
        Check("V1 a version-4 file upgrades to 6 as the identity (the run's fields unchanged, no shrine)",
            loaded.LoadedVersion == 4 && up && loaded.Version == 6 && loaded.Active.BoardShrine == null && loaded.Active.ScheduleVersion == 1 && loaded.Active.Schedule == "acts-v1"
            && log.Any(l => l.Contains("from version 4 to 6") && l.Contains("schedule v1")), string.Join(" | ", log));
        DungeonRunStore.Save(dir, loaded);
        Check("V1 the one-time backup dungeon-run.v4.xml is written, and the file is version 6", File.Exists(Path.Combine(dir, "dungeon-run.v4.xml"))
            && File.ReadAllText(Path.Combine(dir, DungeonRunStore.FileName)).Contains("Version=\"6\""));
        string body4 = xml.Substring(xml.IndexOf("<Profile", StringComparison.Ordinal)), body5 = File.ReadAllText(Path.Combine(dir, DungeonRunStore.FileName));
        body5 = body5.Substring(body5.IndexOf("<Profile", StringComparison.Ordinal));
        Check("V1 the run body is byte-identical after the upgrade", body4.Replace("\r\n", "\n").Trim() == body5.Replace("\r\n", "\n").Trim());
        var again = DungeonRunStore.Load(dir);
        Check("V1 a version-6 file loads as is (no upgrade)", !DungeonRunMigration.Upgrade(again, run, null) && again.Version == 6);
        var walked = Walk(again.Active, x => x.Floor >= 6 && x.Phase == DungeonRunPhase.PreMatch, log);
        Check("V2 the phase-6 run (schedule version 1) still loads on version-2 data and walks acts-v1 with no phase-7 draft",
            run.ScheduleFor(again.Active).Id == "acts-v1" && log.All(l => !l.Contains("teammate") && !l.Contains("shrine-upgrade")) && walked.BoardShrine == null);
        var plan = run.BuildPlan(walked);
        Check("V2 its plan has no shrine", plan.PlayerShrine == null && plan.PlayerShrineUpgrades.Count == 0);
        var future = NewRun(Druid, "rumble_druid_115", 502);
        future.ScheduleVersion = 5; // phase 8: 3 was the loaded version; step 9: 4 is
        bool refused = false;
        try { run.ScheduleFor(future); } catch (InvalidDataException) { refused = true; }
        Check("V3 a run on a future schedule version is still refused", refused);
        var orphan = NewRun(Druid, "rumble_druid_115", 503);
        orphan.ScheduleVersion = 1; // acts-v2 was never released with version 1
        refused = false;
        try { run.ScheduleFor(orphan); } catch (InvalidDataException) { refused = true; }
        Check("V3 a version-1 run naming acts-v2 (never released with version 1) is refused", refused);
        string newer = Serialize(new DungeonRunSave { Active = s }).Replace("Version=\"6\"", "Version=\"7\"").Replace("encoding=\"utf-16\"", "encoding=\"utf-8\"");
        File.WriteAllText(Path.Combine(dir, DungeonRunStore.FileName), newer);
        refused = false;
        try { DungeonRunStore.Load(dir); } catch (InvalidDataException) { refused = true; }
        Check("V4 a version-7 file is refused (never downgraded)", refused && DungeonRunSave.CurrentVersion == 6);
    }
});

Console.WriteLine($"SUMMARY checks passed {passes} failed {failures}");
return failures == 0 ? 0 : 1;
