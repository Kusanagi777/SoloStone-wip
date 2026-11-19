using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using SabberStoneCore.Actions;
using SabberStoneCore.CardSets.DungeonRun;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

// c-ends-9: step 9 lane X, match ends, restarts and finales (handoff/phase9/plan.md section 1 D3, section 2 D11-D14, section 3
// stage 2 lane X), on the run layer and in real engine games built through the run setup path (DungeonRun.BuildPlan ->
// DungeonRunSetup.Configure / ApplyAfterStart), plus the save checks of lane S (D6 and Resume's refusal).
//   P  the plan knobs and their gate: a released schedule keeps RunMatchPlan's defaults and no GameConfig.MatchEnds even with
//      Hearthstone and Rewind held; TurnCap 150 and FormRatchet only on a stamp-4 relay fight (Tekahn on acts-v4t, the act keys
//      or, without them, the relay rule); the match seed is the floor's until a tuned restart (restartIndex > 0)
//   E  D14, the escape (ULDA_111 Hearthstone): allowed on a combat, a major and a Plague Lord fight, refused on a finale and on
//      the schedule's last node (isolated on a variant whose last node is no finale) and at deckMin; real games cast the card
//      (or cannot) and the run records the escape (no credit, no draft, the escape tavern, the card gone, lord damage banked)
//   R  D13, the restarts (GILA_911 Rewind, GILA_BOSS_48t Alternate Timeline): real games mark them; the run restages under a
//      fresh seed, consumes Rewind, caps each source once per floor (one never blocks the other), spends a turn budget of twice
//      the cap, banks lord damage, keeps the fight-start Health under Battle Scars for Rewind and carries Health for 48t
//   K  D11, the Tekahn ratchet: a power-only threshold form clamps the boss's maximum Health only with FormRatchet
//   D  D3: synthetic RunMatchEnd values move every match-fed field; three consecutive runs carry a Plague Lord's damage
//   F  D12: every new field round-trips (summary Escaped / Restarted, BankedGold, RestartIndex, RestartSources,
//      RestartCarriedDamage, RestartTurns), each is omitted at its default, a version-6 file with them loads, version 7 is refused
//   S  lane S: DungeonRunStore.Restore restores Version (D6); Resume refuses unknown starter deck and BossSequence ids
//   V  a hero replaced this turn in the fair view (test phase, the tekahn9 32-4 fallbacks): a hero that gained Attack "this
//      turn" and is then replaced (the Tekahn relay; a Plague Lord's next form) sits in the set-aside zone with its one-turn
//      effect still queued (Game.OneTurnEffects); the fair view masks it, and the end of a simulated turn gave the effect back
//      to the Unknown stand-in (ATK.Remove -> NotImplementedException): the run player's and the boss's fair states end the
//      turn with no exception, the view queues no effect on an actor it masked, and the real game still gives the Attack back
// C_ENDS_9_ONLY=P,E,... runs only those sections (for the break-and-run proofs; unset: every section).
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

int failures = 0, passes = 0;
void Check(string name, bool ok, string detail = "")
{
    if (ok) passes++; else failures++;
    Console.WriteLine((ok ? "PASS " : "FAIL ") + name + (detail.Length > 0 ? "  (" + detail + ")" : ""));
}
var only = Environment.GetEnvironmentVariable("C_ENDS_9_ONLY") is string o && o.Length > 0 ? o.Split(',') : null;
void Guard(string name, Action body)
{
    if (only != null && !only.Contains(name)) return;
    try { body(); }
    catch (Exception e) { failures++; Console.WriteLine("FAIL " + name + " threw " + e.GetType().Name + ": " + e.Message.Split('\n')[0] + " @ " + (e.StackTrace ?? "").Split('\n').FirstOrDefault()?.Trim()); }
}

Card C(string id) => Cards.FromId(id);
string root = VerifyPaths.Data;
string R(string f) => File.ReadAllText(Path.Combine(root, f));
DungeonRunData Load(string schedule = null) => DungeonRunData.Parse(R("dungeon-run/run-v1.json"), R("dungeon-run/pool-v1.json"), R("npc/decks-v1.json"),
    R("dungeon-run/catalog-v1.json"), schedule ?? R("dungeon-run/schedule-v1.json"), R("dungeon-run/tavern-v1.json"), R("dungeon-run/encounters-v1.json"));
var data = Load();
var run = new DungeonRun(data);
DungeonRunProfile All() { var p = new DungeonRunProfile(); p.Unlocked.AddRange(data.Unlocks.Keys); return p; }
var xml = new System.Xml.Serialization.XmlSerializer(typeof(DungeonRunSave));
string Xml(DungeonRunSave save) { using var w = new StringWriter(); xml.Serialize(w, save); return w.ToString(); }
DungeonRunSave FromXml(string text) { using var r = new StringReader(text); return (DungeonRunSave)xml.Deserialize(r); }

const string Hearthstone = RunMatchEnds.Hearthstone, Rewind = RunMatchEnds.Rewind, Timeline = RunMatchEnds.AlternateTimeline;
const string Yeti = "CS2_182", BattleScars = "ULDA_720";
const string Xatma = "ulda_37_xatma", Tekahn = "ulda_67_tekahn", Toki = "gila_48_infinite_toki", Norroa = "dala_43_norroa", Khadgar = "dala_60_khadgar";
const long MatchStream = 2; // DungeonRun.MatchStream (private): the engine-seed stream of DungeonRun.MatchSeed
string HearthShell = DungeonRunContent.Get(Hearthstone).ShellId;
// The floors of acts-v4h / acts-v4t (acts I-III: 5 fights each, the major last; then the finale): combat 1, majors 5 / 10 / 15,
// the finale 16-19 (v4h: Norroa ... Khadgar, the last node) or 16 (v4t: Tekahn).
const int Combat = 1, Major1 = 5, Major2 = 10, Lord = 15, Finale = 16, LastV4h = 19;

// A run of the class row on the schedule, walked by won fights to the pre-match of the floor (pairs9's walk: first offers,
// "none" at a stamp-4 modifier node, every bundle card taken), with the boss under test put on that floor. Twists and anomalies
// the walk rolled are cleared unless asked for, so a test controls its fight's rules.
DungeonRunState At(string schedule, int floor, string bossId = null, long seed = 9100, DungeonRun r = null, bool keepRules = false, string cls = "MAGE", bool typed = false)
{
    r ??= run;
    var row = r.Data.Class(cls);
    var profile = new DungeonRunProfile(); profile.Unlocked.AddRange(r.Data.Unlocks.Keys);
    var s = r.NewRun(profile, row.Id, row.Decks[0], row.Powers[0], seed, typedSeed: typed, schedule: schedule, signatureId: r.SignatureOffer(profile, row, seed).FirstOrDefault());
    var scratch = new DungeonRunProfile();
    for (int guard = 0; guard < 800 && !(s.Floor == floor && s.Phase == DungeonRunPhase.PreMatch); guard++)
    {
        switch (s.Phase)
        {
            case DungeonRunPhase.PreMatch: r.BeginMatch(s); r.RecordOutcome(s, scratch, DungeonRunOutcome.Win); break;
            case DungeonRunPhase.TreasureDraft: r.PickTreasure(s, s.OpenDraft.Offered[0]); break;
            case DungeonRunPhase.Modifier:
                r.PickModifier(s, s.ModifierOffer.Kind == DungeonRunGlobalRule.KindAnomaly || s.ModifierOffer.Offered.Contains(DungeonRunGlobalRule.None) ? DungeonRunGlobalRule.None : s.ModifierOffer.Offered[0]);
                break;
            case DungeonRunPhase.Tavern: r.LeaveTavern(s); break;
            case DungeonRunPhase.BundleDraft:
                if (s.OpenDraft.Takes == 0) { r.PickBundle(s, s.OpenDraft.Offered[0]); break; }
                string bundle = s.OpenDraft.Choosing ?? s.OpenDraft.Offered[0];
                var left = r.OfferCards(s.OpenDraft, bundle);
                foreach (string kept in s.OpenDraft.Chosen) left.Remove(kept);
                r.PickDraftCard(s, bundle, left[0]);
                break;
            default: throw new Exception("walk stopped at " + s.Phase);
        }
    }
    if (!(s.Floor == floor && s.Phase == DungeonRunPhase.PreMatch)) throw new Exception("the walk did not reach floor " + floor + " of " + schedule);
    if (bossId != null) s.BossSequence[floor - 1] = bossId;
    if (!keepRules) s.ActiveRules.RemoveAll(x => x.Kind != null);
    // Modifiers the walk took are dropped too (their knobs would move Health and mana the checks read).
    if (!keepRules) s.ActiveRules.Clear();
    return s;
}
// The run deck at exactly n cards: plain starter Yetis added, or non-treasure records removed from the end.
void DeckTo(DungeonRunState s, int n)
{
    while (s.Cards.Count < n) DungeonRunDeck.Add(s, Yeti, DungeonRunDeck.OriginStarter);
    while (s.Cards.Count > n)
    {
        var card = s.Cards.Last(c => !DungeonRunDeck.IsTreasureOrigin(c.Origin));
        s.Cards.Remove(card);
        s.Upgrades.RemoveAll(u => u.Uid != 0 && u.Uid == card.Uid);
        DungeonRunDeck.Sync(s);
    }
}
// Hearthstone held as the run holds an active treasure (DungeonRun.AddActiveTreasure: the key, its shell's card record).
void GiveHearthstone(DungeonRunState s) { s.ActiveTreasures.Add(Hearthstone); DungeonRunDeck.Add(s, HearthShell, DungeonRunDeck.OriginTreasure); }
bool HoldsHearthstone(DungeonRunState s) => s.ActiveTreasures.Contains(Hearthstone) || s.Deck.Contains(HearthShell) || s.Cards.Any(c => c.Id == HearthShell);
void AddRule(DungeonRunState s, string id)
{
    var rule = data.Schedules.Rule(id);
    s.ActiveRules.Add(new DungeonRunRule { Id = id, Act = rule.RunScope ? 0 : run.CurrentNode(s).Act, FromNode = -1, Kind = rule.Kind });
}
RunMatchEnd End(int bossRemaining, int playerDamage = 0, bool escaped = false, string restartSource = null, int turns = 20, int form = 0,
    IEnumerable<string> heroes = null, int gnome = 0) =>
    new RunMatchEnd(bossRemaining, form, form, playerDamage, heroes ?? new[] { "HERO_08" }, false, gnome, escaped: escaped, restarted: restartSource != null,
        restartSource: restartSource, turns: turns);
// Records a fight as the host does: BeginMatch, then RecordOutcome with what the game left.
List<string> Fight(DungeonRunState s, DungeonRunProfile p, DungeonRunOutcome outcome, RunMatchEnd end, RunMatchResult result = null, DungeonRun r = null)
{
    r ??= run;
    r.BeginMatch(s);
    return r.RecordOutcome(s, p, outcome, result, end);
}
Game Ready(RunMatchPlan plan, int seed = 7)
{
    var cfg = new GameConfig { RandomSeed = seed, Shuffle = true, SkipMulligan = false, History = true, Logging = false, FormatType = FormatType.FT_WILD };
    DungeonRunSetup.Configure(cfg, plan);
    var g = new Game(cfg);
    g.StartGame();
    DungeonRunSetup.ApplyAfterStart(g, plan);
    g.Process(ChooseTask.Mulligan(g.Player1, new List<int>()));
    g.Process(ChooseTask.Mulligan(g.Player2, new List<int>()));
    g.MainBegin();
    if (g.CurrentPlayer != g.Player1) throw new Exception("run player not first");
    return g;
}
GameConfig Configured(RunMatchPlan plan) { var cfg = new GameConfig { RandomSeed = 7, FormatType = FormatType.FT_WILD }; DungeonRunSetup.Configure(cfg, plan); return cfg; }
void Mana(Controller c, int n = 10) { c.BaseMana = n; c.UsedMana = 0; c.OverloadLocked = 0; c.OverloadOwed = 0; }
IPlayable ToHand(Game g, Controller c, string id)
{
    while (c.HandZone.Count >= 9) { var old = c.HandZone[0]; c.HandZone.Remove(old); c.SetasideZone.Add(old); }
    var e = Entity.FromCard(c, C(id)); Generic.AddHandPhase.Invoke(c, e); g.DeathProcessingAndAuraUpdate(); return e;
}
void EndTurn(Game g) => g.Process(EndTurnTask.Any(g.CurrentPlayer));
void Hurt(Game g, Hero h, int toHealth) { h.Damage += h.Health - toHealth; g.DeathProcessingAndAuraUpdate(); }
bool Playable(Controller c, string id) => c.Options().OfType<PlayCardTask>().Any(t => t.Source?.Card.Id == id);
IPlayable InHand(Controller c, string id) => c.HandZone.FirstOrDefault(p => p.Card.Id == id);
string Plan(RunMatchPlan p) => $"cap {p.TurnCap} ratchet {p.FormRatchet} escape {p.EscapeAllowed} restarts [{string.Join(",", p.Restarts)}] carried {p.PlayerCarriedDamage}";

// ============================================================ P. the plan knobs and their gate
Guard("P", () =>
{
    // A released schedule: Hearthstone and Rewind held change nothing (every knob is read through Tuned).
    var v3 = At("acts-v3h", Combat, seed: 9101);
    GiveHearthstone(v3); DeckTo(v3, 20); v3.Treasures.Add(Rewind);
    var p3 = run.BuildPlan(v3, All());
    Check("P1 acts-v3h (stamp 3): Hearthstone and Rewind held, the plan keeps the defaults and the config no MatchEnds", !DungeonRun.Tuned(v3)
        && p3.TurnCap == 100 && !p3.FormRatchet && !p3.EscapeAllowed && p3.Restarts.Count == 0 && Configured(p3).MatchEnds == null && !run.EscapeAllowed(v3), Plan(p3));
    var v4 = At("acts-v4h", Combat, seed: 9102);
    GiveHearthstone(v4); DeckTo(v4, 20); v4.Treasures.Add(Rewind);
    var p4 = run.BuildPlan(v4, All());
    var ends = Configured(p4).MatchEnds;
    Check("P2 acts-v4h combat fight: escape allowed, Rewind listed, cap 100, no ratchet; GameConfig.MatchEnds carries them", DungeonRun.Tuned(v4)
        && p4.TurnCap == 100 && !p4.FormRatchet && p4.EscapeAllowed && p4.Restarts.SequenceEqual(new[] { Rewind })
        && ends != null && ends.EscapeAllowed && !ends.FormRatchet && ends.Allows(Rewind) && !ends.Allows(Timeline), Plan(p4));
    var plain = At("acts-v4h", Combat, seed: 9103);
    var pp = run.BuildPlan(plain, All());
    Check("P3 acts-v4h with neither card held: the defaults, no MatchEnds (a tuned plan that needs none stays null)", pp.TurnCap == 100 && !pp.FormRatchet
        && !pp.EscapeAllowed && pp.Restarts.Count == 0 && Configured(pp).MatchEnds == null, Plan(pp));
    var t4 = At("acts-v4t", Finale, Tekahn, seed: 9104);
    var t3 = At("acts-v3t", Finale, Tekahn, seed: 9104);
    var kh = At("acts-v4h", LastV4h, Khadgar, seed: 9105);
    var no = At("acts-v4h", Finale, Norroa, seed: 9105);
    var pt4 = run.BuildPlan(t4, All()); var pt3 = run.BuildPlan(t3, All()); var pkh = run.BuildPlan(kh, All()); var pno = run.BuildPlan(no, All());
    Check("P4 TurnCap 150 and FormRatchet only on the stamp-4 relay fight: Tekahn on acts-v4t; acts-v3t's Tekahn and acts-v4h's Heist finale keep 100 and no ratchet",
        pt4.TurnCap == 150 && pt4.FormRatchet && pt4.PlayerRelay.Count > 0 && pt3.TurnCap == 100 && !pt3.FormRatchet && pt3.PlayerRelay.Count > 0
        && pkh.TurnCap == 100 && !pkh.FormRatchet && pno.TurnCap == 100 && !pno.FormRatchet && Configured(pt3).MatchEnds == null && Configured(pt4).MatchEnds.FormRatchet,
        "v4t " + Plan(pt4) + " | v3t " + Plan(pt3) + " | v4h last " + Plan(pkh));
    Check("P5 run.TurnCapFor: 150 for Tekahn on acts-v4t, 100 on acts-v3t and on every acts-v4h fight", run.TurnCapFor(t4, data.Boss(Tekahn)) == 150
        && run.TurnCapFor(t3, data.Boss(Tekahn)) == 100 && run.TurnCapFor(kh, data.Boss(Khadgar)) == 100 && run.TurnCapFor(v4, run.CurrentBoss(v4)) == 100);
    // The relay rule alone (the act states no turnCap or formRatchet): the same 150 and ratchet; a non-relay fight keeps 100.
    var bare = Load();
    var bareFinale = bare.Schedules.Find("acts-v4t").Acts.Single(a => a.IsFinale);
    bareFinale.TurnCap = 0; bareFinale.FormRatchet = false;
    var bareRun = new DungeonRun(bare);
    var bt = At("acts-v4t", Finale, Tekahn, seed: 9104, r: bareRun);
    var bc = At("acts-v4t", Combat, seed: 9104, r: bareRun);
    var pbt = bareRun.BuildPlan(bt, All()); var pbc = bareRun.BuildPlan(bc, All());
    Check("P6 without the finale's turnCap/formRatchet keys a stamp-4 relay fight still gets 150 and the ratchet (the relay rule); a combat fight 100",
        pbt.TurnCap == 150 && pbt.FormRatchet && pbc.TurnCap == 100 && !pbc.FormRatchet, Plan(pbt) + " | " + Plan(pbc));
    // The match seed: the floor's (design R11) until a tuned restart; a released run with a restart index keeps it.
    int floorSeed(DungeonRunState s) => StableSeed.Derive(s.Seed, MatchStream, s.Floor) & int.MaxValue;
    int seed0 = run.MatchSeed(v4);
    v4.RestartIndex = 1; int seed1 = run.MatchSeed(v4);
    v4.RestartIndex = 2; int seed2 = run.MatchSeed(v4);
    v4.RestartIndex = 0;
    v3.RestartIndex = 1; int seed3 = run.MatchSeed(v3); v3.RestartIndex = 0;
    Check("P7 fresh seed only when restartIndex > 0: index 0 is the floor's seed, 1 and 2 are StableSeed(seed, MatchStream, floor, index), all different",
        seed0 == floorSeed(v4) && seed1 == (StableSeed.Derive(v4.Seed, MatchStream, v4.Floor, 1) & int.MaxValue)
        && seed2 == (StableSeed.Derive(v4.Seed, MatchStream, v4.Floor, 2) & int.MaxValue) && seed0 != seed1 && seed1 != seed2 && seed0 != seed2, $"{seed0} {seed1} {seed2}");
    Check("P8 a released run (acts-v3h) with a restart index keeps the floor's seed", seed3 == floorSeed(v3), $"{seed3} vs {floorSeed(v3)}");
    // The defaults of RunMatchPlan and RunMatchEnd (every run before step 9 constructs these).
    var d = new RunMatchPlan();
    var e0 = new RunMatchEnd(5, 0, 0, 3, new[] { "HERO_08" }, false);
    var e1 = new RunMatchEnd(5, 0, 0, 3, null, false, restartSource: Timeline, turns: -4);
    Check("P9 defaults: RunMatchPlan cap 100, no ratchet, no escape, no restarts; RunMatchEnd no marks, Turns 0; a restart source implies Restarted; negative Turns clamp to 0",
        d.TurnCap == 100 && !d.FormRatchet && !d.EscapeAllowed && d.Restarts.Count == 0 && !e0.Escaped && !e0.Restarted && e0.RestartSource == null && e0.Turns == 0
        && e1.Restarted && e1.RestartSource == Timeline && e1.Turns == 0 && new RunMatchEnd(5, 0, 0, 3, null, false, restarted: false, restartSource: null).RestartSource == null);
});

// ============================================================ E. D14, the escape (ULDA_111 Hearthstone)
Guard("E", () =>
{
    // Where the escape is allowed: a tuned run holding the card and deckMin + 1 cards, not in a finale act, not on the last node.
    DungeonRunState With(string schedule, int floor, string boss, int deck, long seed, DungeonRun r = null)
    {
        var s = At(schedule, floor, boss, seed, r);
        GiveHearthstone(s); DeckTo(s, deck);
        return s;
    }
    var combat = With("acts-v4h", Combat, null, 11, 9201);
    var major = With("acts-v4h", Major1, null, 11, 9202);
    var lord = With("acts-v4h", Lord, Xatma, 11, 9203);
    var finale = With("acts-v4h", Finale, Norroa, 11, 9204);
    var last = With("acts-v4h", LastV4h, Khadgar, 11, 9205);
    var tekahn = With("acts-v4t", Finale, Tekahn, 11, 9206);
    int min = run.DeckMin(combat);
    Check("E1 escape allowed on a combat fight, a major and a Plague Lord (acts-v4h floors 1, 5, 15; deck at deckMin + 1)", min == 10
        && run.EscapeAllowed(combat) && run.EscapeAllowed(major) && run.EscapeAllowed(lord) && run.CurrentNode(major).Kind == DungeonRunNodeKind.Major
        && run.CurrentNode(lord).Kind == DungeonRunNodeKind.Major && run.BuildPlan(lord, All()).EscapeAllowed, "deckMin " + min);
    Check("E2 escape refused in a finale act (acts-v4h Norroa, Khadgar; acts-v4t Tekahn): EscapeAllowed and the plan say no",
        !run.EscapeAllowed(finale) && !run.EscapeAllowed(last) && !run.EscapeAllowed(tekahn) && !run.BuildPlan(finale, All()).EscapeAllowed
        && !run.BuildPlan(tekahn, All()).EscapeAllowed);
    // The last-node rule on its own: acts-v4h with its finale disabled ends on act III's major (floor 15), which is no finale.
    var json = JsonNode.Parse(R("dungeon-run/schedule-v1.json"));
    json["schedules"]["acts-v4h"]["finale"]["enabled"] = false;
    var noFinale = new DungeonRun(Load(json.ToJsonString()));
    var nfLast = With("acts-v4h", Lord, Xatma, 11, 9207, noFinale);
    var nfBefore = With("acts-v4h", Lord - 1, null, 11, 9207, noFinale);
    var nfSchedule = noFinale.ScheduleFor(nfLast);
    Check("E3 the last node alone refuses it: acts-v4h without its finale ends on floor 15 (not a finale act): floor 15 refused, floor 14 allowed",
        nfSchedule.Acts.All(a => !a.IsFinale) && noFinale.CurrentNode(nfLast).Index == nfSchedule.Nodes.Count - 1 && !noFinale.EscapeAllowed(nfLast) && noFinale.EscapeAllowed(nfBefore),
        "nodes " + nfSchedule.Nodes.Count + " last floor " + noFinale.CurrentNode(nfLast).Floor);
    var atMin = With("acts-v4h", Combat, null, 10, 9208);
    var noCard = At("acts-v4h", Combat, null, 9209); DeckTo(noCard, 20);
    var v3 = At("acts-v3h", Combat, null, 9210); GiveHearthstone(v3); DeckTo(v3, 20);
    Check("E4 refused at deckMin (10 cards, the card one of them), without the card, and on a released schedule (acts-v3h)", atMin.Deck.Count == 10 && HoldsHearthstone(atMin)
        && !run.EscapeAllowed(atMin) && !run.BuildPlan(atMin, All()).EscapeAllowed && !run.EscapeAllowed(noCard) && !run.EscapeAllowed(v3));

    // Real games: the card is drawn at the start of the game; it is playable only when the plan allows the escape.
    var minPlan = run.BuildPlan(atMin, All());
    var gMin = Ready(minPlan);
    var heldMin = InHand(gMin.Player1, HearthShell);
    bool castMin = heldMin != null && gMin.Process(PlayCardTask.Any(gMin.Player1, heldMin));
    Check("E5 at deckMin the drawn Hearthstone is unplayable: not among the options, a forced play is refused and the game goes on", heldMin != null
        && !Playable(gMin.Player1, HearthShell) && !castMin && gMin.State != State.COMPLETE && gMin[EncounterRules.Escape] == 0 && gMin.Player1.HandZone.Contains(heldMin),
        "in hand " + (heldMin != null) + " cast " + castMin);
    // The deck at deckMin + 1 -> escape -> the card gone (combat fight, a real game, then the run layer).
    var profile = All();
    int bossHealth = run.BuildPlan(combat, profile).BossHealth;
    var gc = Ready(run.BuildPlan(combat, profile));
    bool playable = Playable(gc.Player1, HearthShell);
    bool cast = gc.Process(PlayCardTask.Any(gc.Player1, InHand(gc.Player1, HearthShell)));
    var endC = DungeonRunSetup.Harvest(gc);
    Check("E6 at deckMin + 1 it is playable; its cast ends the game as the run player's win, marked (tag 32638), Hearthstone consumed, the boss alive at its Health",
        playable && cast && gc.State == State.COMPLETE && gc.Player1.PlayState == PlayState.WON && gc[EncounterRules.Escape] == 1
        && gc.RunResult != null && gc.RunResult.ConsumedTreasures.SequenceEqual(new[] { Hearthstone }) && endC.Escaped && !endC.Restarted && !endC.NoLoss
        && endC.BossRemainingHealth == bossHealth && endC.Turns == gc.Turn, $"remaining {endC.BossRemainingHealth} of {bossHealth} turns {endC.Turns}");
    int defeated = DungeonRun.BossesDefeated(combat), majors = combat.MajorsCleared, wins = combat.Matches.Count, node = combat.Node, floor = combat.Floor;
    var notes = Fight(combat, profile, DungeonRunOutcome.Win, endC, gc.RunResult);
    var sum = combat.Matches.Last();
    Check("E7 the run records the escape: a Win marked Escaped, no kill credit, no draft, the escape tavern on the same node, the card gone (deck at deckMin)",
        sum.Outcome == DungeonRunOutcome.Win && sum.Escaped && !sum.Restarted && DungeonRun.BossesDefeated(combat) == defeated && combat.MajorsCleared == majors
        && combat.Phase == DungeonRunPhase.Tavern && combat.Tavern?.Node == node && combat.OpenDraft == null && combat.Deck.Count == min && !HoldsHearthstone(combat)
        && notes.Any(n => n.StartsWith("escaped: floor " + floor)) && notes.Contains("treasure used up: " + Hearthstone), string.Join(" | ", notes));
    run.LeaveTavern(combat);
    Check("E8 leaving the escape tavern moves on to the next node (the run goes on)", combat.Node > node && !combat.Finished && combat.Phase != DungeonRunPhase.Tavern,
        "node " + node + " -> " + combat.Node + " " + combat.Phase);
    // A major: no MajorsCleared, no treasure draft (act I's major opens a shrine-upgrade draft and a bundle after a win).
    var gm = Ready(run.BuildPlan(major, profile));
    gm.Process(PlayCardTask.Any(gm.Player1, InHand(gm.Player1, HearthShell)));
    int majorsBefore = major.MajorsCleared;
    Fight(major, profile, DungeonRunOutcome.Win, DungeonRunSetup.Harvest(gm), gm.RunResult);
    Check("E9 an escaped major: no major credit, no draft, the escape tavern", gm[EncounterRules.Escape] == 1 && major.MajorsCleared == majorsBefore
        && major.Matches.Last().Escaped && major.Phase == DungeonRunPhase.Tavern && major.OpenDraft == null && !HoldsHearthstone(major));
    // A Plague Lord: the escape banks its damage as a loss does and spends the run's Plague Lord damage.
    var lordProfile = All();
    lordProfile.BossDamage.Add(new DungeonRunBossDamage { Boss = Xatma, Remaining = 260, Attempts = 1 });
    lord.PlagueLordDamage = 10;
    var lordPlan = run.BuildPlan(lord, lordProfile);
    var gl = Ready(lordPlan);
    Hurt(gl, gl.Player2.Hero, 230);
    bool lordCast = gl.Process(PlayCardTask.Any(gl.Player1, InHand(gl.Player1, HearthShell)));
    var endL = DungeonRunSetup.Harvest(gl);
    Fight(lord, lordProfile, DungeonRunOutcome.Win, endL, gl.RunResult);
    var rec = lordProfile.DamageOf(Xatma);
    Check("E10 an escaped Plague Lord (Xatma, banked 260, Gnomebliterator 10, hit to 230): the escape banks 230, spends the run's lord damage, no kill credit",
        lordPlan.BossCarriedDamage == 50 && lordCast && endL.Escaped && endL.BossRemainingHealth == 230 && rec?.Remaining == 230 && rec.Attempts == 2
        && lord.PlagueLordDamage == 0 && lord.Matches.Last().Escaped && lord.Matches.Last().BossRemaining == 230 && lord.Phase == DungeonRunPhase.Tavern,
        $"carried {lordPlan.BossCarriedDamage} remaining {endL.BossRemainingHealth} banked {rec?.Remaining} attempts {rec?.Attempts} lord damage {lord.PlagueLordDamage}");
    // An escape the run does not allow (a host that reports one on a finale) is restaged as a fault; the card stays.
    var refusedNotes = Fight(finale, All(), DungeonRunOutcome.Win, End(70, escaped: true), null);
    Check("E11 an escape reported on a finale fight is refused: restaged as a fault (note), the card kept, nothing else recorded",
        finale.Matches.Last().Outcome == DungeonRunOutcome.Fault && !finale.Matches.Last().Escaped && finale.Phase == DungeonRunPhase.PreMatch && HoldsHearthstone(finale)
        && refusedNotes.Contains("escape refused: this fight allows no escape (restaged)"), string.Join(" | ", refusedNotes));
    // Under Battle Scars the run hero keeps the damage it escaped with.
    var scars = With("acts-v4h", Combat, null, 12, 9211);
    AddRule(scars, BattleScars);
    scars.HeroDamage = 5;
    Fight(scars, All(), DungeonRunOutcome.Win, End(30, playerDamage: 41, escaped: true), null);
    Check("E12 an escape under Battle Scars keeps the hero's damage (5 -> 41); the card leaves even when the host passes no match result",
        scars.HeroDamage == 41 && scars.Matches.Last().Escaped && !HoldsHearthstone(scars) && scars.Deck.Count == 11, "HeroDamage " + scars.HeroDamage);
});

// ============================================================ R. D13, the restarts (GILA_911 Rewind, GILA_BOSS_48t Alternate Timeline)
// Toki with Alternate Timeline in his deck (the stage-3 gate has not put it back; this data copy does, for the mechanism only).
var tokiData = Load();
var tokiDeck = tokiData.BossDecks[tokiData.Boss(Toki).Deck];
tokiDeck[tokiDeck.Count - 1] = Timeline;
var tokiRun = new DungeonRun(tokiData);
Guard("R", () =>
{
    // A real game: the run hero dies with Rewind listed -> the game is marked and Rewind consumed; the game ends as a loss.
    var s = At("acts-v4h", Combat, null, 9301);
    s.Treasures.Add(Rewind);
    var profile = All();
    var plan = run.BuildPlan(s, profile);
    var g = Ready(plan);
    EndTurn(g); EndTurn(g);
    Hurt(g, g.Player1.Hero, 0);
    var end = DungeonRunSetup.Harvest(g);
    Check("R1 a real game: the run hero dies with Rewind listed -> tag 32639 = 1, Rewind consumed, the game a loss, the harvest a Rewind restart with its turns",
        plan.Restarts.SequenceEqual(new[] { Rewind }) && g.State == State.COMPLETE && g.Player1.PlayState == PlayState.LOST && g[EncounterRules.Restart] == EncounterRules.RestartRewind
        && g.RunResult?.ConsumedTreasures.SequenceEqual(new[] { Rewind }) == true && end.Restarted && end.RestartSource == Rewind && !end.Escaped && end.Turns == g.Turn && end.Turns >= 3,
        $"state {g.State} play {g.Player1.PlayState} mark {g[EncounterRules.Restart]} turns {end.Turns}");
    var noList = At("acts-v4h", Combat, null, 9302);
    var g0 = Ready(run.BuildPlan(noList, All()));
    Hurt(g0, g0.Player1.Hero, 0);
    var end0 = DungeonRunSetup.Harvest(g0);
    Check("R2 without Rewind listed the same death is a plain loss: no mark, nothing consumed", g0.State == State.COMPLETE && g0[EncounterRules.Restart] == 0
        && !end0.Restarted && (g0.RunResult == null || g0.RunResult.ConsumedTreasures.Count == 0));
    int floorSeed = run.MatchSeed(s), floor = s.Floor, attempt = s.Attempt;
    var notes = Fight(s, profile, DungeonRunOutcome.Loss, end, g.RunResult);
    var sum = s.Matches.Last();
    Check("R3 the run restages the floor: a Fault marked Restarted, pre-match on the same floor, Rewind gone, restart 1 from GILA_911 with the game's turns",
        sum.Outcome == DungeonRunOutcome.Fault && sum.Restarted && s.Phase == DungeonRunPhase.PreMatch && s.Floor == floor && !s.Finished && !s.Treasures.Contains(Rewind)
        && s.RestartIndex == 1 && s.RestartSources == Rewind && s.RestartTurns == end.Turns && s.RestartCarriedDamage == 0
        && notes.Contains("restarted (" + Rewind + "): floor " + floor + ", restart 1") && notes.Contains("treasure used up: " + Rewind), string.Join(" | ", notes));
    var plan2 = run.BuildPlan(s, profile);
    Check("R4 the restarted game: a fresh seed (restart 1), no restart left (Rewind used), the rest of the floor's budget as its cap (2 x 100 - turns)",
        run.MatchSeed(s) != floorSeed && run.MatchSeed(s) == (StableSeed.Derive(s.Seed, MatchStream, floor, 1) & int.MaxValue) && plan2.Restarts.Count == 0
        && plan2.TurnCap == 200 - end.Turns, Plan(plan2) + " seed " + floorSeed + " -> " + run.MatchSeed(s));
    // A plain fault on the restarted floor restages it with the same (restart) seed; a decided floor clears the restart state.
    int restartSeed = run.MatchSeed(s);
    Fight(s, profile, DungeonRunOutcome.Fault, null);
    bool faultKeeps = s.RestartIndex == 1 && run.MatchSeed(s) == restartSeed && s.Phase == DungeonRunPhase.PreMatch;
    Fight(s, profile, DungeonRunOutcome.Win, End(0));
    Check("R5 a fault on the restarted floor keeps the restart seed; the floor decided (a win) clears RestartIndex, sources, carried damage and turns",
        faultKeeps && s.RestartIndex == 0 && s.RestartSources == null && s.RestartCarriedDamage == 0 && s.RestartTurns == 0
        && s.Matches.Last().Outcome == DungeonRunOutcome.Win && s.Phase != DungeonRunPhase.PreMatch,
        $"fault keeps {faultKeeps}, index {s.RestartIndex} sources {s.RestartSources ?? "-"} phase {s.Phase}");

});
Guard("R", () =>
{
    // Rewind is consumed even when the host passes no match result (outcome-implied, as the escape).
    var bare = At("acts-v4h", Combat, null, 9303); bare.Treasures.Add(Rewind);
    Fight(bare, All(), DungeonRunOutcome.Fault, End(30, playerDamage: 30, restartSource: Rewind), null);
    Check("R7 a Rewind restart with no match result still uses Rewind up", bare.RestartIndex == 1 && !bare.Treasures.Contains(Rewind));

    // Per-source caps: Rewind once per floor; a second Rewind (another copy held) is not listed and a reported one is refused.
    bare.Treasures.Add(Rewind);
    var capped = run.BuildPlan(bare, All());
    var capNotes = Fight(bare, All(), DungeonRunOutcome.Loss, End(30, playerDamage: 30, restartSource: Rewind), null);
    Check("R8 Rewind's cap: once per floor; a second copy is not listed, and a second Rewind restart is refused: recorded as the loss, the run over",
        capped.Restarts.Count == 0 && bare.Matches.Last().Outcome == DungeonRunOutcome.Loss && !bare.Matches.Last().Restarted && bare.Finished && bare.Phase == DungeonRunPhase.Defeat
        && capNotes.Contains("restart refused (" + Rewind + "): its once-per-floor restart is used; recorded as Loss"), string.Join(" | ", capNotes));
});
Guard("R", () =>
{
    // The caps are per source: Rewind used never blocks Alternate Timeline (Toki with the card in his deck, act II's major).
    var t = At("acts-v4h", Major2, Toki, 9304, tokiRun);
    t.Treasures.Add(Rewind);
    var tp = tokiRun.BuildPlan(t, All());
    Fight(t, All(), DungeonRunOutcome.Loss, End(50, playerDamage: 30, restartSource: Rewind), null, tokiRun);
    var tp2 = tokiRun.BuildPlan(t, All());
    var tNotes = Fight(t, All(), DungeonRunOutcome.Fault, End(40, playerDamage: 12, restartSource: Timeline), null, tokiRun);
    bool second = t.RestartIndex == 2 && t.RestartSources == Rewind + "," + Timeline && t.Phase == DungeonRunPhase.PreMatch;
    var tp3 = tokiRun.BuildPlan(t, All());
    var tNotes2 = Fight(t, All(), DungeonRunOutcome.Fault, End(40, playerDamage: 12, restartSource: Timeline), null, tokiRun);
    Check("R9 per-source caps: Toki's fight lists Rewind and Alternate Timeline; after Rewind only the Timeline; it restarts once more, then is capped too",
        tp.Restarts.SequenceEqual(new[] { Rewind, Timeline }) && tp2.Restarts.SequenceEqual(new[] { Timeline }) && second && tp3.Restarts.Count == 0
        && t.Finished && tNotes2.Contains("restart refused (" + Timeline + "): its once-per-floor restart is used; recorded as Loss"),
        Plan(tp) + " | " + Plan(tp2) + " | " + string.Join(" | ", tNotes) + " | " + string.Join(" | ", tNotes2));
});
Guard("R", () =>
{
    // A source the fight could not use is refused (no Rewind held; Alternate Timeline on a boss without it).
    var none1 = At("acts-v4h", Combat, null, 9305);
    var n1 = Fight(none1, All(), DungeonRunOutcome.Loss, End(30, restartSource: Rewind), null);
    var none2 = At("acts-v4h", Major2, Toki, 9306);
    var n2 = Fight(none2, All(), DungeonRunOutcome.Fault, End(30, restartSource: Timeline), null);
    Check("R10 a restart from a source the fight could not use is refused and recorded as a loss (no Rewind held; the shipped Toki deck has no Timeline)",
        none1.Finished && none1.RestartIndex == 0 && n1.Any(n => n.StartsWith("restart refused (" + Rewind + "): this fight has no such restart"))
        && none2.Finished && none2.Matches.Last().Outcome == DungeonRunOutcome.Loss && n2.Any(n => n.StartsWith("restart refused (" + Timeline + "): this fight has no such restart")),
        string.Join(" | ", n1) + " || " + string.Join(" | ", n2));
});
Guard("R", () =>
{
    // The turn budget: a floor's games take at most 2 x TurnCap; a restart that leaves no turn is a turn limit.
    var budget = At("acts-v4h", Combat, null, 9307); budget.Treasures.Add(Rewind);
    var bNotes = Fight(budget, All(), DungeonRunOutcome.Loss, End(30, restartSource: Rewind, turns: 199), null);
    var budget2 = At("acts-v4h", Combat, null, 9308); budget2.Treasures.Add(Rewind);
    Fight(budget2, All(), DungeonRunOutcome.Loss, End(30, restartSource: Rewind, turns: 150), null);
    var bp2 = run.BuildPlan(budget2, All());
    Check("R11 the budget: a restart after 199 of 200 turns is refused as a TurnLimit; after 150 it is taken and the new game's cap is 50",
        budget.Finished && budget.Matches.Last().Outcome == DungeonRunOutcome.TurnLimit && bNotes.Any(n => n.Contains("the floor's turn budget is spent; recorded as TurnLimit"))
        && budget2.RestartIndex == 1 && bp2.TurnCap == 50, string.Join(" | ", bNotes) + " | " + Plan(bp2));
});
Guard("R", () =>
{
    // Lord damage banked on a restart (a ledger run): the new game starts at the banked remaining; the run's lord damage spent.
    var lord = At("acts-v4h", Lord, Xatma, 9309); lord.Treasures.Add(Rewind); lord.PlagueLordDamage = 10;
    var lp = All();
    var lordPlan = run.BuildPlan(lord, lp);
    Fight(lord, lp, DungeonRunOutcome.Loss, End(240, playerDamage: 30, restartSource: Rewind), null);
    var lordPlan2 = run.BuildPlan(lord, lp);
    Check("R12 lord damage banked on a restart: Xatma (Gnomebliterator 10) left at 240 -> the ledger holds 240, the run's lord damage is spent, the new game starts 60 down",
        lordPlan.BossCarriedDamage == 10 && lp.DamageOf(Xatma)?.Remaining == 240 && lp.DamageOf(Xatma).Attempts == 1 && lord.PlagueLordDamage == 0
        && lord.RestartIndex == 1 && lordPlan2.BossCarriedDamage == 60 && lord.Matches.Last().BossRemaining == 240,
        $"carried {lordPlan.BossCarriedDamage} -> {lordPlan2.BossCarriedDamage}, banked {lp.DamageOf(Xatma)?.Remaining}, lord damage {lord.PlagueLordDamage}");
    Check("R6 a restart is no last boss: LastBoss names the fight before it (the walk's floor 14), not Xatma's restarted game",
        lord.Matches.Last().Restarted && DungeonRun.LastBoss(lord) == lord.BossSequence[Lord - 2], DungeonRun.LastBoss(lord) ?? "-");
});
Guard("R", () =>
{
    // A typed seed reads no ledger: nothing is banked, and the run's lord damage is kept for the restarted game.
    var typedLord = At("acts-v4h", Lord, Xatma, 9317, typed: true); typedLord.Treasures.Add(Rewind); typedLord.PlagueLordDamage = 10;
    var tl0 = All();
    Fight(typedLord, tl0, DungeonRunOutcome.Loss, End(240, playerDamage: 30, restartSource: Rewind), null);
    Check("R19 on a typed seed (no ledger) the restart banks nothing and keeps the run's lord damage: the new game starts 10 down again",
        typedLord.RestartIndex == 1 && tl0.DamageOf(Xatma) == null && typedLord.PlagueLordDamage == 10 && run.BuildPlan(typedLord, tl0).BossCarriedDamage == 10,
        "lord damage " + typedLord.PlagueLordDamage);
});
Guard("R", () =>
{
    // Rewind runs after the relay: Tekahn on acts-v4t, the run hero dies -> a relay hero enters (no restart); the last one dies -> Rewind.
    var relay = At("acts-v4t", Finale, Tekahn, 9318); relay.Treasures.Add(Rewind);
    var relayPlan = run.BuildPlan(relay, All());
    var gy = Ready(relayPlan);
    string firstHero = gy.Player1.Hero.Card.Id;
    Hurt(gy, gy.Player1.Hero, 0);
    bool relayFirst = gy.State != State.COMPLETE && gy.Player1.Hero.Card.Id != firstHero && gy[EncounterRules.Restart] == 0;
    for (int k = 0; k < 10 && gy.State != State.COMPLETE; k++) Hurt(gy, gy.Player1.Hero, 0);
    var endY = DungeonRunSetup.Harvest(gy);
    Check("R20 Rewind comes after the relay: the run hero's death brings in a relay hero (no mark); the last relay hero's death marks the Rewind restart",
        relayPlan.Restarts.SequenceEqual(new[] { Rewind }) && relayPlan.PlayerRelay.Count > 0 && relayFirst && gy.State == State.COMPLETE
        && gy[EncounterRules.Restart] == EncounterRules.RestartRewind && endY.Restarted && endY.HeroesUsed.Count == relayPlan.PlayerRelay.Select(h => h.Hero.Id).Distinct().Count() + 1,
        $"relay first {relayFirst}, relay {relayPlan.PlayerRelay.Count}, state {gy.State}, mark {gy[EncounterRules.Restart]}, heroes {string.Join(">", endY.HeroesUsed)}");
    Fight(relay, All(), DungeonRunOutcome.Loss, endY, gy.RunResult);
    // (No carried-damage claim: a Rewind restart never carries damage, relay or not; R13 checks that.)
    Check("R21 the relay fight's Rewind restart restages the finale and uses Rewind up", relay.RestartIndex == 1
        && relay.Phase == DungeonRunPhase.PreMatch && !relay.Treasures.Contains(Rewind));
});
Guard("R", () =>
{
    // Rewind under Battle Scars: the fight-start damage stays (earlier fights' damage kept, this fight's undone).
    var scars = At("acts-v4h", Combat, null, 9310); AddRule(scars, BattleScars); scars.Treasures.Add(Rewind); scars.HeroDamage = 10;
    var sp = run.BuildPlan(scars, All());
    Fight(scars, All(), DungeonRunOutcome.Loss, End(30, playerDamage: 100, restartSource: Rewind), null);
    var sp2 = run.BuildPlan(scars, All());
    Check("R13 Rewind under Battle Scars: started 10 down, died -> restarted 10 down (HeroDamage 10 kept, nothing carried)",
        sp.PlayerCarriedDamage == 10 && sp.PlayerHeroHealth == 100 && scars.HeroDamage == 10 && scars.RestartCarriedDamage == 0 && sp2.PlayerCarriedDamage == 10 && scars.RestartIndex == 1,
        Plan(sp) + " -> " + Plan(sp2));
});
Guard("R", () =>
{
    // Alternate Timeline carries the run hero's damage of the game it ended (on top of Battle Scars' start damage).
    var tc = At("acts-v4h", Major2, Toki, 9311, tokiRun);
    Fight(tc, All(), DungeonRunOutcome.Fault, End(40, playerDamage: 17, restartSource: Timeline), null, tokiRun);
    var tcp = tokiRun.BuildPlan(tc, All());
    var ts = At("acts-v4h", Major2, Toki, 9312, tokiRun); AddRule(ts, BattleScars); ts.HeroDamage = 5;
    Fight(ts, All(), DungeonRunOutcome.Fault, End(40, playerDamage: 25, restartSource: Timeline), null, tokiRun);
    var tsp = tokiRun.BuildPlan(ts, All());
    Check("R14 48t carries Health: 17 damage taken -> RestartCarriedDamage 17, the new game 17 down; under Battle Scars (5 at the start, 25 at the end) 20 carried, 25 down",
        tc.RestartCarriedDamage == 17 && tcp.PlayerCarriedDamage == 17 && ts.RestartCarriedDamage == 20 && ts.HeroDamage == 5 && tsp.PlayerCarriedDamage == 25,
        Plan(tcp) + " | " + Plan(tsp));
});
Guard("R", () =>
{
    // A real game: Toki casts Alternate Timeline (listed) -> both heroes leave (a tie), the game marked with its source.
    var tg = At("acts-v4h", Major2, Toki, 9313, tokiRun);
    var tgPlan = tokiRun.BuildPlan(tg, All());
    var gt = Ready(tgPlan);
    Hurt(gt, gt.Player1.Hero, 21);
    EndTurn(gt); Mana(gt.Player2);
    var timeline = ToHand(gt, gt.Player2, Timeline);
    bool tokiCan = Playable(gt.Player2, Timeline);
    bool tokiCast = gt.Process(PlayCardTask.Any(gt.Player2, timeline));
    var endT = DungeonRunSetup.Harvest(gt);
    Check("R15 a real game: Toki casts Alternate Timeline -> tag 32639 = 2, a tie, the harvest a Timeline restart carrying the run hero's 9 damage",
        tgPlan.Restarts.SequenceEqual(new[] { Timeline }) && tokiCan && tokiCast && gt.State == State.COMPLETE && gt.Player1.PlayState == PlayState.TIED
        && gt[EncounterRules.Restart] == EncounterRules.RestartAlternateTimeline && endT.Restarted && endT.RestartSource == Timeline && endT.PlayerHeroDamage == 9,
        $"can {tokiCan} cast {tokiCast} state {gt.State} play {gt.Player1.PlayState} damage {endT.PlayerHeroDamage}");
    Fight(tg, All(), DungeonRunOutcome.Tie, endT, gt.RunResult, tokiRun);
    Check("R16 the run takes the tie with the mark as the restart: restaged, 9 damage carried into the new game", tg.RestartIndex == 1 && tg.Phase == DungeonRunPhase.PreMatch
        && tg.RestartCarriedDamage == 9 && tokiRun.BuildPlan(tg, All()).PlayerCarriedDamage == 9);
});
Guard("R", () =>
{
    // Without the listing (a released run, the same deck) Toki cannot cast it.
    var tr = At("acts-v3h", Major2, Toki, 9314, tokiRun);
    var trPlan = tokiRun.BuildPlan(tr, All());
    var gr = Ready(trPlan);
    EndTurn(gr); Mana(gr.Player2);
    var tl = ToHand(gr, gr.Player2, Timeline);
    bool trCast = gr.Process(PlayCardTask.Any(gr.Player2, tl));
    Check("R17 on a released schedule (acts-v3h, Toki's deck holding it) Alternate Timeline is unplayable: no restart listed, a forced cast refused",
        trPlan.Restarts.Count == 0 && !Playable(gr.Player2, Timeline) && !trCast && gr.State != State.COMPLETE && gr[EncounterRules.Restart] == 0);
});
Guard("R", () =>
{
    // A released run never takes the restart branch: a reported mark is the plain fault it is reported as; Rewind stays.
    var rel = At("acts-v3h", Combat, null, 9315); rel.Treasures.Add(Rewind);
    Fight(rel, All(), DungeonRunOutcome.Fault, End(30, restartSource: Rewind), null);
    var relEsc = At("acts-v3h", Combat, null, 9316); GiveHearthstone(relEsc); DeckTo(relEsc, 20);
    Fight(relEsc, All(), DungeonRunOutcome.Win, End(30, escaped: true), null);
    Check("R18 a released run (acts-v3h) ignores the marks: a reported restart is a plain fault (Rewind kept, no index), a reported escape a plain win",
        rel.RestartIndex == 0 && rel.Treasures.Contains(Rewind) && !rel.Matches.Last().Restarted && rel.Phase == DungeonRunPhase.PreMatch
        && !relEsc.Matches.Last().Escaped && relEsc.Matches.Last().Outcome == DungeonRunOutcome.Win && HoldsHearthstone(relEsc) && relEsc.Phase != DungeonRunPhase.Tavern,
        relEsc.Phase.ToString());
});

// ============================================================ K. D11, the Tekahn form ratchet
Guard("K", () =>
{
    (int Form, int Max, int Health, int MaxAfterHeal, int Health2, int Max2) Phases(string schedule)
    {
        var s = At(schedule, Finale, Tekahn, 9401);
        var g = Ready(run.BuildPlan(s, All()));
        var boss = g.Player2.Hero;
        Hurt(g, boss, 190);
        int form = g.Player2[EncounterRules.FormIndex], max = boss.BaseHealth, health = boss.Health;
        boss.TakeHeal(boss, 60); g.DeathProcessingAndAuraUpdate(); // an engine heal of 60 (HealingRules.Heal)
        int maxAfterHeal = boss.Health;
        Hurt(g, boss, 90);
        return (form, max, health, maxAfterHeal, boss.Health, boss.BaseHealth);
    }
    var on = Phases("acts-v4t");
    var off = Phases("acts-v3t");
    Check("K1 FormRatchet (acts-v4t): Tekahn at 190 enters form 1 with his maximum clamped to 200, an engine heal of 60 stops at 200; at 90 form 2 clamps to 100",
        on.Form == 1 && on.Max == 200 && on.Health == 190 && on.MaxAfterHeal == 200 && on.Max2 == 100 && on.Health2 == 90, on.ToString());
    Check("K2 without it (acts-v3t) the same blows keep his 300 maximum (the released behaviour): the engine heal goes to 250", off.Form == 1 && off.Max == 300 && off.Health == 190
        && off.MaxAfterHeal == 250 && off.Max2 == 300, off.ToString());
});

// ============================================================ D. D3, synthetic RunMatchEnd values move every match-fed field
Guard("D", () =>
{
    var p = All();
    var lord = At("acts-v4h", Lord, Xatma, 9501);
    Fight(lord, p, DungeonRunOutcome.Loss, End(150, form: 1, heroes: new[] { "HERO_08", "ULDA_Elise" }));
    var m = lord.Matches.Last();
    Check("D1 a lost Plague Lord fight: the profile's BossDamage (150), the summary's BossRemaining 150, BossForm 1 and HeroesUsed",
        p.DamageOf(Xatma)?.Remaining == 150 && m.BossRemaining == 150 && m.BossForm == 1 && m.HeroesUsed == "HERO_08,ULDA_Elise",
        $"banked {p.DamageOf(Xatma)?.Remaining} remaining {m.BossRemaining} form {m.BossForm} heroes {m.HeroesUsed}");
    var scars = At("acts-v4h", Combat, null, 9502); AddRule(scars, BattleScars);
    Fight(scars, All(), DungeonRunOutcome.Win, End(0, playerDamage: 12, gnome: 10));
    Check("D2 a won fight under Battle Scars with a Gnomebliterator cast: HeroDamage 12, PlagueLordDamage 10", scars.HeroDamage == 12 && scars.PlagueLordDamage == 10,
        $"HeroDamage {scars.HeroDamage} PlagueLordDamage {scars.PlagueLordDamage}");
    var esc = At("acts-v4h", Combat, null, 9503); GiveHearthstone(esc); DeckTo(esc, 15);
    Fight(esc, All(), DungeonRunOutcome.Win, End(25, escaped: true));
    var rs = At("acts-v4h", Major2, Toki, 9504, tokiRun);
    Fight(rs, All(), DungeonRunOutcome.Fault, End(40, playerDamage: 8, restartSource: Timeline), null, tokiRun);
    Check("D3 Escaped, Restarted and RestartCarriedDamage move with their marks", esc.Matches.Last().Escaped && rs.Matches.Last().Restarted && rs.RestartCarriedDamage == 8);
    // Three consecutive runs carry the lord's damage through the profile (each a new random-seed run).
    var ledger = All();
    var seen = new List<string>();
    int[] left = { 250, 180, 90 };
    bool carried = true;
    for (int k = 0; k < 3; k++)
    {
        var s = At("acts-v4h", Lord, Xatma, 9510 + k);
        var plan = run.BuildPlan(s, ledger);
        int startRemaining = plan.BossHealth - plan.BossCarriedDamage;
        int expected = k == 0 ? 300 : left[k - 1];
        carried &= startRemaining == expected && plan.BossFormStart == (expected <= 100 ? 2 : expected <= 200 ? 1 : 0);
        seen.Add($"run {k + 1}: starts at {startRemaining} form {plan.BossFormStart}, left at {left[k]}");
        Fight(s, ledger, DungeonRunOutcome.Loss, End(left[k]));
        carried &= ledger.DamageOf(Xatma)?.Remaining == left[k] && ledger.DamageOf(Xatma).Attempts == k + 1;
    }
    var fourth = At("acts-v4h", Lord, Xatma, 9513);
    int fourthStart = run.BuildPlan(fourth, ledger) is var fp ? fp.BossHealth - fp.BossCarriedDamage : -1;
    Fight(fourth, ledger, DungeonRunOutcome.Win, End(0));
    Check("D4 three consecutive runs carry Xatma's damage (300 -> 250 -> 180 -> 90, each starting where the last left, in its form); a fourth starts at 90 and its win clears it",
        carried && fourthStart == 90 && ledger.DamageOf(Xatma) == null, string.Join(" | ", seen) + " | fourth " + fourthStart);
});

// ============================================================ F. D12, every new field round-trips (save version 6)
Guard("F", () =>
{
    var s = At("acts-v4h", Combat, null, 9601);
    s.BankedGold = 3; s.RestartIndex = 2; s.RestartSources = Rewind + "," + Timeline; s.RestartCarriedDamage = 7; s.RestartTurns = 33;
    s.Matches.Add(new DungeonRunMatchSummary { Floor = 1, Boss = s.BossSequence[0], Outcome = DungeonRunOutcome.Win, Attempt = 1, Escaped = true });
    s.Matches.Add(new DungeonRunMatchSummary { Floor = 2, Boss = s.BossSequence[1], Outcome = DungeonRunOutcome.Fault, Attempt = 1, Restarted = true });
    var save = new DungeonRunSave { Profile = All(), Active = s };
    string text = Xml(save);
    var back = FromXml(text);
    var b = back.Active;
    Check("F1 every new field round-trips through the save: BankedGold, RestartIndex, RestartSources, RestartCarriedDamage, RestartTurns, summary Escaped and Restarted",
        back.Version == 6 && b.BankedGold == 3 && b.RestartIndex == 2 && b.RestartSources == Rewind + "," + Timeline && b.RestartCarriedDamage == 7 && b.RestartTurns == 33
        && b.Matches[b.Matches.Count - 2].Escaped && !b.Matches[b.Matches.Count - 2].Restarted && b.Matches.Last().Restarted && !b.Matches.Last().Escaped && Xml(back) == text);
    string[] names = { "BankedGold=", "RestartIndex=", "RestartSources=", "RestartCarriedDamage=", "RestartTurns=", "Escaped=", "Restarted=" };
    var plain = new DungeonRunSave { Profile = All(), Active = At("acts-v4h", Combat, null, 9602) };
    plain.Active.Matches.Add(new DungeonRunMatchSummary { Floor = 1, Boss = plain.Active.BossSequence[0], Outcome = DungeonRunOutcome.Win, Attempt = 1 });
    string plainText = Xml(plain);
    Check("F2 each is written as an attribute when set and omitted at its default (an earlier run writes the same body)", names.All(n => text.Contains(" " + n))
        && names.All(n => !plainText.Contains(" " + n)), string.Join(",", names.Where(n => plainText.Contains(" " + n))));
    // A version-6 file with them loads through the store (the sidecar path), and a version-7 file is refused.
    string dir = VerifyPaths.Out("tmp/c-ends-9");
    if (Directory.Exists(dir)) Directory.Delete(dir, true);
    DungeonRunStore.Save(dir, save);
    var loaded = DungeonRunStore.Load(dir);
    bool v7Refused = false;
    File.WriteAllText(Path.Combine(dir, DungeonRunStore.FileName), File.ReadAllText(Path.Combine(dir, DungeonRunStore.FileName)).Replace("Version=\"6\"", "Version=\"7\""));
    try { DungeonRunStore.Load(dir); } catch (InvalidDataException) { v7Refused = true; }
    Directory.Delete(dir, true);
    Check("F3 a version-6 file with the step-9 attributes loads through DungeonRunStore (LoadedVersion 6, the fields intact); a version-7 file is refused",
        loaded.Version == 6 && loaded.LoadedVersion == 6 && loaded.Active.RestartSources == Rewind + "," + Timeline && loaded.Active.BankedGold == 3 && loaded.Active.RestartTurns == 33
        && loaded.Active.Matches.Any(x => x.Escaped) && v7Refused);
});

// ============================================================ S. lane S: Restore restores Version (D6); Resume refuses unknown ids
Guard("S", () =>
{
    var save = new DungeonRunSave { Version = 5, LoadedVersion = 5, Profile = All(), Active = At("acts-v4h", Combat, null, 9701) };
    var snapshot = DungeonRunStore.Clone(save);
    string before = Xml(save);
    // A change that raised the version (the first commit of an upgraded file) and touched profile and run, then failed.
    save.Version = DungeonRunSave.CurrentVersion;
    save.Profile.Unlocked.Add("unlock_from_a_failed_change");
    save.Active.Floor += 1;
    // LoadedVersion (XmlIgnore, the version the file was read at) is set apart from the snapshot's, so a Restore that copied it
    // back would show.
    save.LoadedVersion = 4;
    DungeonRunStore.Restore(save, snapshot);
    Check("S1 DungeonRunStore.Restore puts back Version (6 -> 5), the profile and the run, and leaves LoadedVersion alone (4, the snapshot's 5)", save.Version == 5 && save.LoadedVersion == 4
        && !save.Profile.Unlocked.Contains("unlock_from_a_failed_change") && Xml(save) == before, "Version " + save.Version + " LoadedVersion " + save.LoadedVersion);
    var nullRun = new DungeonRunSave { Version = 4, Profile = All(), Active = null };
    var nullSnap = DungeonRunStore.Clone(nullRun);
    nullRun.Version = 6; nullRun.Active = At("acts-v4h", Combat, null, 9702);
    DungeonRunStore.Restore(nullRun, nullSnap);
    Check("S2 Restore of a save with no run in progress: the run dropped again, Version 6 -> 4", nullRun.Active == null && nullRun.Version == 4);
    string Refusal(DungeonRunState s) { try { run.Resume(s); return null; } catch (InvalidDataException e) { return e.Message; } }
    var deck = At("acts-v4h", Combat, null, 9703); deck.StarterDeck = "no_such_deck";
    var boss = At("acts-v4h", Combat, null, 9704); boss.BossSequence[3] = "no_such_boss";
    var both = At("acts-v4h", Combat, null, 9705); both.StarterDeck = "no_such_deck"; both.BossSequence[2] = "boss_a"; both.BossSequence[4] = "boss_b";
    string rd = Refusal(deck), rb = Refusal(boss), r2 = Refusal(both);
    Check("S3 Resume refuses an unknown starter deck id, naming it", rd == "Dungeon Run save: the run names starter deck no_such_deck, unknown to the loaded data; the run is not resumed.", rd ?? "resumed");
    Check("S4 Resume refuses an unknown BossSequence id, naming it", rb == "Dungeon Run save: the run names boss no_such_boss, unknown to the loaded data; the run is not resumed.", rb ?? "resumed");
    Check("S5 Resume names every unknown id at once", r2 == "Dungeon Run save: the run names starter deck no_such_deck and bosses boss_a, boss_b, unknown to the loaded data; the run is not resumed.", r2 ?? "resumed");
    // A deck of a disabled row is named as such (a data copy with the run's deck moved to the disabled list).
    var dis = Load();
    var row = dis.Class("MAGE");
    var disabledDeck = dis.FindDeck(row.Decks[0]);
    var disRun = new DungeonRun(dis);
    var ds = At("acts-v4h", Combat, null, 9706);
    dis.DisabledDecks.Add(disabledDeck);
    var catalog = dis.Decks;
    catalog.Remove(disabledDeck);
    string rdis; try { disRun.Resume(ds); rdis = null; } catch (InvalidDataException e) { rdis = e.Message; }
    Check("S6 a starter deck whose row is disabled is named as such", rdis != null && rdis.Contains("starter deck " + row.Decks[0] + " (its row is disabled)"), rdis ?? "resumed");
    // What Resume lets through: a finished run, a version-1 run with no starter deck id, a good run (mid-match -> pre-match).
    var done = At("acts-v4h", Combat, null, 9707); run.Abandon(done, All()); done.StarterDeck = "no_such_deck";
    var v1 = At("acts-v4h", Combat, null, 9708); v1.StarterDeck = null;
    var good = At("acts-v4h", Combat, null, 9709); run.BeginMatch(good);
    string rdone = Refusal(done), rv1 = Refusal(v1), rgood = Refusal(good);
    Check("S7 Resume lets through a finished run (not checked), a run with no starter deck id (version 1) and a known run, which goes back to pre-match",
        rdone == null && rv1 == null && rgood == null && good.Phase == DungeonRunPhase.PreMatch, (rdone ?? "-") + " | " + (rv1 ?? "-"));
});

// ============================================================ V. a hero replaced this turn, in the fair view
// bosspilot tekahn9 32-4 (games 33 and 35): the relay hero Brann played Heroic Strike (CS2_105: +4 Attack this turn), attacked
// and died; Reno entered and Brann went to the set-aside zone with his +4 still in Game.OneTurnEffects (the real game gives it
// back at the end of the turn). The run player's fair view masked the set-aside Brann (an Unknown at his id), and every
// simulated end of that turn (MainCleanUp) gave the +4 back to the Unknown: ATK.Remove has no stand-in path
// (NotImplementedException), the search fell back (4 FALLBACK lines). The boss's own replaced form takes the same path.
Guard("V", () =>
{
    const string HeroicStrike = "CS2_105";
    string Try(Action body) { try { body(); return ""; } catch (Exception e) { var x = e.InnerException ?? e; return x.GetType().Name + ": " + x.Message.Split('\n')[0]; } }
    // The view's queued one-turn effects that do not name the real game's actor: an Unknown stand-in at its id, no actor, or
    // another card (a masked id the view gave to a sampled card of the own deck).
    int Masked(Game view, Game real) => view.OneTurnEffects.Count(p => !view.IdEntityDic.TryGetValue(p.entityId, out var e) || e is Unknown
        || !real.IdEntityDic.TryGetValue(p.entityId, out var r) || r.Card.Id != e.Card.Id);
    // The golden path's view options (GoldenPilot.Options: Secrets removed or tracked, the own deck tracked).
    FairOptions Golden() => new FairOptions { RemoveOpponentSecrets = true, TrackOwnDeck = true, MaterializeOwnDeckTriggers = true };
    bool Strike(Game g, Controller c) { Mana(c); var card = ToHand(g, c, HeroicStrike); return g.Process(PlayCardTask.Any(c, card)); }

    // V1-V4: the Tekahn relay on acts-v4t, the run player's turn.
    var s = At("acts-v4t", Finale, Tekahn, 9320, cls: "WARRIOR");
    var plan = run.BuildPlan(s, All());
    var g = Ready(plan);
    Controller p1 = g.Player1;
    Hero first = p1.Hero;
    bool struck = Strike(g, p1) && first.AttackDamage == 4;
    Hurt(g, first, 0);
    bool queued = g.OneTurnEffects.Any(p => p.entityId == first.Id);
    Check("V1 the relay: the run hero gains +4 Attack this turn (Heroic Strike), dies, a relay hero enters; the dead hero is set aside with its +4 still queued",
        struck && plan.PlayerRelay.Count > 0 && g.State != State.COMPLETE && p1.Hero != first && first.Zone == p1.SetasideZone && queued,
        $"struck {struck}, hero {p1.Hero.Card.Id}, old zone {first.Zone?.Type}, queued {queued}");
    var view = FairBotState.Create(g, p1.PlayerId, 3, DeckStyle.Tempo, Golden()).FingerprintGame;
    Check("V2 the run player's fair view queues no one-turn effect on an actor it masked", Masked(view, g) == 0,
        $"{Masked(view, g)} of {view.OneTurnEffects.Count}; old hero in view {(view.IdEntityDic.TryGetValue(first.Id, out var vo) ? vo.GetType().Name + " " + vo.Card.Id : "-")}");
    var mine = FairBotState.Create(g, p1.PlayerId, 3, DeckStyle.Tempo, Golden());
    string endError = Try(() => mine.Apply(BotAction.From(EndTurnTask.Any(p1))));
    Check("V3 the run player's fair state ends the turn after the relay (no exception)", endError == "", endError);
    string realError = Try(() => EndTurn(g));
    Check("V4 the real game ends the turn and gives the set-aside hero's +4 back (Attack 0, nothing queued)",
        realError == "" && g.CurrentPlayer == g.Player2 && first.AttackDamage == 0 && g.OneTurnEffects.Count == 0, realError + $" attack {first.AttackDamage}");

    // V5-V6: a Plague Lord's next form on acts-v4h, the boss's turn (the live host's NPC: its own fair view).
    var lord = At("acts-v4h", Lord, Xatma, 9321, cls: "WARRIOR");
    var gl = Ready(run.BuildPlan(lord, All()));
    EndTurn(gl);
    Controller p2 = gl.Player2;
    Hero form = p2.Hero;
    bool lordStruck = Strike(gl, p2) && form.AttackDamage == 4;
    Hurt(gl, form, 150);
    Check("V5 Xatma gains +4 Attack this turn and crosses a form threshold: the next form enters, the old form is set aside with its +4 queued",
        lordStruck && gl.CurrentPlayer == p2 && p2.Hero != form && form.Zone == p2.SetasideZone && gl.OneTurnEffects.Any(p => p.entityId == form.Id),
        $"struck {lordStruck}, hero {p2.Hero.Card.Id}, old zone {form.Zone?.Type}, old {form.Id}, queued [{string.Join(",", gl.OneTurnEffects.Select(p => p.entityId + " " + p.effect))}], enchantments [{string.Join(",", gl.OneTurnEffectEnchantments.Select(e => e.Card.Id + ">" + e.Target.Id))}]");
    var boss = FairBotState.Create(gl, p2.PlayerId, 3, DeckStyle.Tempo, Golden());
    int bossMasked = Masked(boss.FingerprintGame, gl);
    string bossError = Try(() => boss.Apply(BotAction.From(EndTurnTask.Any(p2))));
    Check("V6 the boss's fair state ends its turn after its form change (no exception, no one-turn effect on a masked actor)",
        bossError == "" && bossMasked == 0, bossError + $" masked {bossMasked}");
});

Console.WriteLine($"RESULT {passes} passed, {failures} failed");
return failures == 0 ? 0 : 1;
