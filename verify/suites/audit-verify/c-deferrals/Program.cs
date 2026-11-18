// c-deferrals: phase 6 family F15 of the unified Dungeon Run, the cards earlier phases deferred to phase 6: the wish (ULD_003
// Zephrys the Great, ULDA_013 Zephrys's Lamp, ULDA_040 Sack of Lamps), the "Start of Game: Draw this." treasures (DALA_707, ULDA_407,
// ULDA_013, ULDA_040, ULDA_302), Primordial Bulwark (ULDA_045 and its Secret ULDA_045t, live on its owner's turn), Sticky Fingers
// (GILA_511, not-started-in-deck), and the three that stay rejected (ULDA_111 Hearthstone, GILA_900p Temporal Loop, GILA_911 Rewind:
// phase 9). For every id: the engine content (keyed and shelled by its own client id, name and text as facts/violet-hold-2026-09.json
// has them), the run data, the validator and admission checks, the real draft or signature offer, a scripted regression game through
// the run setup path (DungeonRun.NewRun -> BuildPlan -> DungeonRunSetup.Configure -> Game -> ApplyAfterStart -> mulligan -> MainBegin)
// asserting the board, hand, deck and hero outcome, the boss AI's Secret prior and sampled worlds for ULDA_045t, and full games against
// the real boss AI (DungeonRunBossPilot + GoldenPilot) that must end without a fault or an AI fallback. Phase 7 D (T13): the nine
// Rumble Run companions share the self-drawing Start of Game rule, drawn after the board shrine and the treasures. Run it twice and compare.
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
using SabberStoneCore.Tasks.SimpleTasks;

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
string runJson = R("dungeon-run/run-v1.json"), poolJson = R("dungeon-run/pool-v1.json"), npcJson = R("npc/decks-v1.json"),
    catalogJson = R("dungeon-run/catalog-v1.json"), scheduleJson = R("dungeon-run/schedule-v1.json"), tavernJson = R("dungeon-run/tavern-v1.json");
var data = DungeonRunData.Parse(runJson, poolJson, npcJson, catalogJson, scheduleJson, tavernJson);
var run = new DungeonRun(data);
var all = new DungeonRunProfile(); all.Unlocked.AddRange(data.Unlocks.Keys);
var facts = System.Text.Json.JsonDocument.Parse(File.ReadAllText(VerifyPaths.Repo("facts/violet-hold-2026-09.json")));
var factById = new Dictionary<string, System.Text.Json.JsonElement>(StringComparer.Ordinal);
foreach (var c in facts.RootElement.GetProperty("cards").EnumerateArray()) factById[c.GetProperty("id").GetString()] = c;
string FactText(string id) => factById[id].TryGetProperty("text", out var t) ? t.GetString() : null;
string FactName(string id) => factById[id].GetProperty("name").GetString();
int FactTag(string id, int tag)
{
    foreach (var pair in factById[id].GetProperty("tags").EnumerateArray())
        if (pair[0].GetInt32() == tag) return pair[1].GetInt32();
    return 0;
}
CardType FactType(string id) => FactTag(id, 202) switch { 3 => CardType.HERO, 4 => CardType.MINION, 5 => CardType.SPELL, 6 => CardType.ENCHANTMENT, 7 => CardType.WEAPON, 10 => CardType.HERO_POWER, _ => CardType.INVALID };
Card C(string id) => Cards.FromId(id);
var tables = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "dungeon-run/reference/client-tables.json"))).RootElement.GetProperty("tables");
var clientIds = new HashSet<string>(tables.GetProperty("CARD").EnumerateArray().Select(r => r.GetProperty("id").GetString()), StringComparer.Ordinal);
var transport = new Dictionary<string, string>
{
    ["CS2_034"] = "HERO_08bp", ["CS2_102"] = "HERO_01bp", ["CS1h_001"] = "HERO_09bp", ["CS2_101"] = "HERO_04bp", ["CS2_017"] = "HERO_06bp",
    ["DS1h_292"] = "HERO_05bp", ["CS2_083b"] = "HERO_03bp", ["CS2_056"] = "HERO_07bp", ["CS2_049"] = "HERO_02bp",
    ["AT_132_WARRIOR"] = "HERO_01bp2", ["AT_132_SHAMAN"] = "HERO_02bp2", ["AT_132_ROGUE"] = "HERO_03bp2", ["AT_132_PALADIN"] = "HERO_04bp2",
    ["AT_132_HUNTER"] = "HERO_05bp2", ["AT_132_DRUID"] = "HERO_06bp2", ["AT_132_WARLOCK"] = "HERO_07bp2", ["AT_132_MAGE"] = "HERO_08bp2", ["AT_132_PRIEST"] = "HERO_09bp2",
};
bool ClientKnows(string id) => clientIds.Contains(transport.TryGetValue(id, out var sent) ? sent : id);

// ---------------------------------------------------------------- the run setup path
const string Reno = "MAGE_ROGUE", Elise = "DRUID_PRIEST", Brann = "HUNTER_WARRIOR", Heist = "DALA_Rakanishu", Tess = "GILA_500h3";
string[] explorers = { Reno, "PALADIN_SHAMAN", Elise, Brann };
var juniors = new DungeonRunProfile(); juniors.Unlocked.AddRange(data.Unlocks.Keys.Where(k => !k.EndsWith("_majors4")));
GameConfig Configured(RunMatchPlan plan, int seed, bool history = true)
{
    var cfg = new GameConfig { RandomSeed = seed, Shuffle = true, SkipMulligan = false, History = history, Logging = false, FormatType = FormatType.FT_WILD };
    DungeonRunSetup.Configure(cfg, plan);
    return cfg;
}
// An act run of a row at floor 1 (an explorer picks its first offered signature, or the one named).
DungeonRunState NewAct(string row, long seed, string signature = null, string power = null, DungeonRunProfile profile = null)
{
    profile = profile ?? juniors;
    var cls = data.Class(row);
    string sig = signature ?? run.SignatureOffer(profile, cls, seed).FirstOrDefault();
    var s = run.NewRun(profile, row, cls.DefaultDeck, power ?? cls.DefaultPower, seed, signatureId: sig);
    if (!s.IsActRun || s.Phase != DungeonRunPhase.PreMatch || s.Floor != 1) throw new Exception("act run not at floor 1 pre-match");
    return s;
}
// A picked active treasure joins the run (DungeonRun.AddActiveTreasure: the id and its card record, origin treasure).
void Grant(DungeonRunState s, string id) { s.ActiveTreasures.Add(id); DungeonRunDeck.Add(s, id, DungeonRunDeck.OriginTreasure); }
// A drafted card joins the run deck (origin draft).
void Draft(DungeonRunState s, string id) => DungeonRunDeck.Add(s, id, DungeonRunDeck.OriginDraft);
// A passive treasure joins the run as a picked one does (the id in Treasures, the deck unchanged).
void Hold(DungeonRunState s, string id) => s.Treasures.Add(id);
Game Begin(RunMatchPlan plan, int seed, bool history = true)
{
    var g = new Game(Configured(plan, seed, history));
    g.StartGame();
    DungeonRunSetup.ApplyAfterStart(g, plan);
    return g;
}
void Finish(Game g)
{
    g.Process(ChooseTask.Mulligan(g.Player1, new List<int>()));
    g.Process(ChooseTask.Mulligan(g.Player2, new List<int>()));
    g.MainBegin();
    if (g.CurrentPlayer != g.Player1) throw new Exception("run player not first");
}
Game StartFight(RunMatchPlan plan, int seed) { var g = Begin(plan, seed); Finish(g); return g; }
(DungeonRunState s, RunMatchPlan plan, Game g) Fight(DungeonRunState s) { var plan = run.BuildPlan(s); return (s, plan, StartFight(plan, run.MatchSeed(s))); }
void Mana(Controller c, int n) { c.BaseMana = n; c.UsedMana = 0; c.OverloadLocked = 0; c.OverloadOwed = 0; }
IPlayable ToHand(Game g, Controller c, string id)
{
    var e = Entity.FromCard(c, C(id));
    Generic.AddHandPhase.Invoke(c, e);
    g.DeathProcessingAndAuraUpdate();
    return e;
}
Minion Board(Game g, Controller c, string id)
{
    var m = (Minion)Entity.FromCard(c, C(id));
    Generic.SummonBlock(g, m, -1, c.Hero);
    g.DeathProcessingAndAuraUpdate();
    return m;
}
IPlayable InHand(Game g, string id) => g.Player1.HandZone.FirstOrDefault(p => p.Card.Id == id);
bool Play(Game g, IPlayable card, ICharacter target = null) => g.Process(PlayCardTask.Any(g.CurrentPlayer, card, target));
bool Pick(Game g, int id)
{
    var decision = g.GetPendingDecisions().FirstOrDefault(d => d.ControllerId == g.Player1.Id && d.Kind != SabberStoneCore.Model.Decisions.DecisionKind.Options);
    if (decision != null) return g.TryAnswer(new SabberStoneCore.Model.Decisions.DecisionAnswer(decision.Id, decision.ControllerId, new[] { id }), out _);
    return g.Process(ChooseTask.Pick(g.Player1, id));
}
void EndTurn(Game g) => g.Process(EndTurnTask.Any(g.CurrentPlayer));
void NextTurn(Game g)
{
    EndTurn(g);
    if (g.CurrentPlayer == g.Player2) EndTurn(g);
    while (g.Player1.Choice != null) Pick(g, g.Player1.Choice.Choices[0]);
}
void Clear(Controller c) { foreach (var e in c.HandZone.ToArray()) { c.HandZone.Remove(e); c.SetasideZone.Add(e); } }
void ClearBoards(Game g) { foreach (var c in new[] { g.Player1, g.Player2 }) foreach (var m in c.BoardZone.ToArray()) { c.BoardZone.Remove(m); c.SetasideZone.Add(m); } }
void SetDeck(Game g, Controller c, params string[] ids)
{
    foreach (var e in c.DeckZone.ToArray()) { c.DeckZone.Remove(e); c.SetasideZone.Add(e); }
    foreach (string id in ids) Entity.FromCard(c, C(id), null, c.DeckZone);
    g.DeathProcessingAndAuraUpdate();
}
void Kill(Game g, Minion m) { m.Destroy(); g.DeathProcessingAndAuraUpdate(); }
bool HasFx(Controller c, string shell) => c.AppliedEnchantments?.Any(e => e.Card.Id == shell) == true;
string[] ChoiceIds(Game g) => g.Player1.Choice == null ? new string[0] : g.Player1.Choice.Choices.Select(id => g.IdEntityDic[id].Card.Id).ToArray();

string[] f15 = DungeonRunContent.DeferralIds.ToArray();
string[] sog = DungeonRunContent.StartOfGameDrawIds.ToArray();
string[] phase9 = DungeonRunContent.DeferredToPhase9Ids.ToArray();
string[] contentIds = f15.Where(id => id != "ULD_003").ToArray();
var mule = data.FindTreasure("ULDA_019");
var heistScope = data.FindTreasure("DALA_716");

// ---------------------------------------------------------------- T1 content
// Step 9 (lane X) scripted ULDA_111 and GILA_911 as match ends (DungeonRunDeferrals.MatchEndIds): GILA_900p is the one id left,
// and ULDA_111 Hearthstone is a sixth Start of Game draw (a match end, not one of the nine F15 content ids).
Check("T1 9 F15 ids with content (ULD_003 in UldumCardsGen), 6 Start of Game draws (5 of them F15's, ULDA_111 step 9's), 1 phase-9 id and step 9's 2 match ends, in id order",
    f15.Length == 9 && f15.SequenceEqual(f15.OrderBy(x => x, StringComparer.Ordinal)) && sog.Length == 6 && sog.Count(f15.Contains) == 5
    && sog.Where(id => !f15.Contains(id)).SequenceEqual(new[] { "ULDA_111" })
    && phase9.SequenceEqual(new[] { "GILA_900p" }) && DungeonRunContent.MatchEndIds.SequenceEqual(new[] { "GILA_911", "ULDA_111" }));
var kinds = new Dictionary<string, RunContentKind>
{
    ["DALA_707"] = RunContentKind.ActiveTreasure, ["ULDA_013"] = RunContentKind.ActiveTreasure, ["ULDA_040"] = RunContentKind.ActiveTreasure,
    ["ULDA_045t"] = RunContentKind.ActiveTreasure, ["ULDA_302"] = RunContentKind.ActiveTreasure, ["ULDA_407"] = RunContentKind.ActiveTreasure,
    ["ULDA_045"] = RunContentKind.PassiveTreasure, ["GILA_511"] = RunContentKind.PassiveTreasure,
};
foreach (string id in contentIds)
{
    var content = DungeonRunContent.Get(id);
    var card = C(id);
    Check($"T1 {id}: a {kinds[id]} keyed and shelled by its own id, scripted, the client's {FactType(id)}",
        content.Kind == kinds[id] && content.ShellId == id && card != null && card.Implemented && card.Type == FactType(id)
        && DungeonRunContent.ForShell(id) == content && clientIds.Contains(id), content.Kind + " " + card?.Type);
    Check($"T1 ids-text-match {id}: content name and text equal facts/violet-hold-2026-09.json", content.Name == FactName(id) && content.Text == FactText(id),
        content.Name + " | " + content.Text);
    if (content.Kind == RunContentKind.ActiveTreasure)
        Check($"T1 {id}: printed Cost {FactTag(id, 48)} as the client's", card.Cost == FactTag(id, 48), card.Cost.ToString());
}
Check("T1 ULD_003 Zephrys the Great is scripted (a Battlecry minion, Cost 2, 3/2)", C("ULD_003").Implemented && C("ULD_003").Cost == 2 && C("ULD_003").ATK == 3);
Check("T1 DALA_707 Loyal Henchman: a 1/1 Taunt minion", C("DALA_707").ATK == 1 && C("DALA_707").Health == 1 && C("DALA_707").Taunt);
Check("T1 ULDA_302 Academic Research: a Druid spell whose script data starts at 1 (the client's '@')",
    C("ULDA_302").Class == CardClass.DRUID && C("ULDA_302")[GameTag.TAG_SCRIPT_DATA_NUM_1] == 1 && DungeonRunContent.AcademicResearchBase == 1);
foreach (string id in sog)
    Check($"T1 {id}: a Start of Game source (StartOfGameRules)", StartOfGameRules.HasEffect(C(id)));
Check("T1 no other F15 card is a Start of Game source", f15.Where(id => !sog.Contains(id)).All(id => !StartOfGameRules.HasEffect(C(id))));
Check("T1 ULDA_045t: an engine Secret (the client shell names it in its text only: tag 219 is a reference) that triggers on its owner's turn",
    C("ULDA_045t").IsSecret && FactTag("ULDA_045t", 219) == 0 && C("ULDA_045t")[OwnTurnSecretRules.Tag] == 1 && C("EX1_295").IsSecret
    && C("EX1_295")[OwnTurnSecretRules.Tag] == 0);
Check("T1 ULDA_045 names ULDA_045t as its token; ULDA_040 names ULDA_013", DungeonRunContent.Get("ULDA_045").Tokens.SequenceEqual(new[] { "ULDA_045t" })
    && DungeonRunContent.Get("ULDA_040").Tokens.SequenceEqual(new[] { "ULDA_013" }));
foreach (var fx in new Dictionary<string, string> { ["fx_DALA_707e"] = "DALA_707e", ["fx_GILA_511"] = "GILA_511e" })
{
    var e = DungeonRunContent.Get(fx.Key);
    Check($"T1 {fx.Value}: content {fx.Key} on the client's own ENCHANTMENT, scripted, name and text word for word",
        e.Kind == RunContentKind.Effect && e.ShellId == fx.Value && C(fx.Value).Implemented && C(fx.Value).Type == CardType.ENCHANTMENT
        && clientIds.Contains(fx.Value) && e.Name == FactName(fx.Value) && e.Text == FactText(fx.Value), e.Name + " | " + e.Text);
}
Check("T1 GILA_511 attaches its player enchantment (EffectKey fx_GILA_511); ULDA_045 has no aura (OnMatchStart only)",
    DungeonRunContent.Get("GILA_511").EffectKey == "fx_GILA_511" && DungeonRunContent.Get("ULDA_045").EffectKey == null);
var excludedGen = new HashSet<string>(DungeonRunAdmission.GeneratedRejects(true), StringComparer.Ordinal);
Check("T1 every wish candidate is a scripted, collectible card the run's generation may yield",
    DungeonRunContent.WishCandidates.All(id => C(id).Implemented && C(id).Collectible && !excludedGen.Contains(id)),
    string.Join(",", DungeonRunContent.WishCandidates.Where(id => !C(id).Implemented || excludedGen.Contains(id))));
Check("T1 the wish's candidate list is distinct (16 cards)", DungeonRunContent.WishCandidates.Distinct().Count() == DungeonRunContent.WishCandidates.Count()
    && DungeonRunContent.WishCandidates.Count() == 16);

// ---------------------------------------------------------------- T2 data, validator, admission
{
    var errors = DungeonRunValidator.Validate(data, ClientKnows);
    Check("T2 shipped data validates with 0 errors (client check through the transported id)", errors.Count == 0, string.Join(" | ", errors.Take(5)));
    var legacy = DungeonRunData.Parse(runJson, poolJson, npcJson);
    var le = DungeonRunValidator.Validate(legacy, null);
    Check("T2 the data without the catalog validates with 0 errors and loads no F15 treasure (their rows scope catalog starters)",
        le.Count == 0 && contentIds.All(id => legacy.FindTreasure(id) == null), string.Join(" | ", le.Take(3)));
    var tombsActive = new[] { "ULDA_013", "ULDA_040", "ULDA_407" };
    foreach (string id in tombsActive)
    {
        var t = data.FindTreasure(id);
        Check($"T2 {id}: an offered active Tombs treasure of act runs, scoped by the explorers' sixteen starters as Pack Mule",
            t != null && t.Active && t.Offered && !t.Signature && !t.Legacy && t.Pool == "Tombs" && t.Runs == DungeonRunTreasure.RunsAct
            && t.Scope.Decks.SequenceEqual(mule.Scope.Decks) && t.Scope.Classes.Count == 0 && t.UpgradesTo == null && t.Tags.Count > 0);
    }
    var bul = data.FindTreasure("ULDA_045");
    Check("T2 ULDA_045: an offered passive Tombs treasure of act runs, scoped by the explorers' sixteen starters",
        bul != null && !bul.Active && bul.Offered && !bul.Signature && bul.Pool == "Tombs" && bul.Runs == DungeonRunTreasure.RunsAct && bul.Scope.Decks.SequenceEqual(mule.Scope.Decks));
    var hench = data.FindTreasure("DALA_707");
    Check("T2 DALA_707: an offered active Heist treasure of act runs, scoped by the Heist starters as the other Heist actives",
        hench != null && hench.Active && hench.Offered && hench.Pool == "Heist" && hench.Runs == DungeonRunTreasure.RunsAct && hench.Scope.Decks.SequenceEqual(heistScope.Scope.Decks)
        && heistScope.Scope.Decks.Count == 36);
    var sticky = data.FindTreasure("GILA_511");
    Check("T2 GILA_511: an offered passive Monster Hunt treasure of Tess's act runs (her starter's scope, as Cult of the Wolf)",
        sticky != null && !sticky.Active && sticky.Offered && sticky.Pool == "MonsterHunt" && sticky.Scope.Decks.SequenceEqual(data.FindTreasure("GILA_513").Scope.Decks));
    var research = data.FindTreasure("ULDA_302");
    var elise = data.Class(Elise);
    Check("T2 ULDA_302: Elise's active signature (never offered in a draft, pool Tombs), unlocked by druid_priest_majors2 (the client's sortOrder 4: 'Defeat Plague Lords 2')",
        research != null && research.Active && research.Signature && !research.Offered && research.Pool == "Tombs" && research.Scope.Classes.SequenceEqual(new[] { Elise })
        && research.Unlock == "druid_priest_majors2" && data.Unlocks.ContainsKey("druid_priest_majors2"));
    Check("T2 Elise's signatures are the client's six in sortOrder (ULDA_302 fifth), three offered",
        elise.Signatures.SequenceEqual(new[] { "ULDA_303", "ULDA_305", "ULDA_003", "ULDA_017", "ULDA_302", "ULDA_304" }) && elise.SignatureOffer == 3, string.Join(",", elise.Signatures));
    Check("T2 ULD_003 has no treasure row (a collectible card the catalog drafts)", data.FindTreasure("ULD_003") == null && data.FindTreasure("ULDA_045t") == null);
}
var families = new Dictionary<string, string>
{
    ["ULD_003"] = "highlander", ["ULDA_013"] = "start-of-game-draw", ["ULDA_040"] = "start-of-game-draw", ["DALA_707"] = "start-of-game-draw",
    ["ULDA_407"] = "start-of-game-draw", ["ULDA_302"] = "start-of-game-draw", ["ULDA_045t"] = "secret-card", ["GILA_511"] = "not-started-in-deck",
};
foreach (var pair in families.OrderBy(p => p.Key, StringComparer.Ordinal))
{
    string id = pair.Key;
    var rule = DungeonRunAdmission.RuleFor(id);
    var p = DungeonRunAdmission.Classify(C(id), id, AdmitScope.Player, null, catalogRun: true);
    var pl = DungeonRunAdmission.Classify(C(id), id, AdmitScope.Player);
    var b = DungeonRunAdmission.Classify(C(id), id, AdmitScope.Boss, null, catalogRun: true);
    bool bossToo = id == "ULDA_045t";
    Check($"T2 admission {id}: the {pair.Value} row admits the player (catalog and legacy){(bossToo ? " and the boss" : ", never the boss")}, names c-deferrals, no pending requirement",
        p.Subject && p.Admitted && pl.Admitted && b.Admitted == bossToo && rule?.Family == pair.Value && rule.Requires == null && rule.Reason.Contains("c-deferrals")
        && !rule.Lift.StartsWith("phase 6"), p.Family + " " + p.Admit + " " + rule?.Lift);
    if (!bossToo) Check($"T2 admission {id}: the boss's generation never yields it", DungeonRunAdmission.GeneratedRejectsBoss(true).Contains(id));
}
Check("T2 ULDA_045 (the passive) is no admission subject of its own: its Secret carries the rule", DungeonRunAdmission.RuleFor("ULDA_045") == null);

// ---------------------------------------------------------------- T3 offers (the real draft and signature paths)
{
    var seen = new HashSet<string>();
    for (long seed = 1; seed <= 3000 && !new[] { "ULDA_013", "ULDA_040", "ULDA_407" }.All(seen.Contains); seed++)
    {
        var s = NewAct(explorers[(int)(seed % 4)], seed);
        if (!run.OpenTreasureDraft(s)) continue;
        var pick = s.OpenDraft.Offered.FirstOrDefault(o => (o == "ULDA_013" || o == "ULDA_040" || o == "ULDA_407") && !seen.Contains(o));
        if (pick == null) continue;
        seen.Add(pick);
        int before = s.Deck.Count;
        run.PickTreasure(s, pick);
        Check($"T3 {pick}: offered in an explorer's treasure draft and picked: it and its card record (origin treasure) join the run deck",
            s.ActiveTreasures.Contains(pick) && s.Deck.Count == before + 1 && s.Cards.Last().Id == pick && s.Cards.Last().Origin == DungeonRunDeck.OriginTreasure
            && DungeonRunDeck.MirrorError(s) == null);
    }
    Check("T3 every F15 Tombs active is offered to an explorer (3000 seeds)", new[] { "ULDA_013", "ULDA_040", "ULDA_407" }.All(seen.Contains), string.Join(",", seen));
    bool Offered(string row, string id, bool passive, int seeds)
    {
        for (long seed = 1; seed <= seeds; seed++)
        {
            var s = NewAct(row, seed);
            for (int k = 0; k < 3 && run.OpenTreasureDraft(s, passiveOnly: passive); k++)
            {
                if (s.OpenDraft.Offered.Contains(id)) return true;
                run.PickTreasure(s, s.OpenDraft.Offered[0]);
            }
        }
        return false;
    }
    Check("T3 ULDA_045 is offered in an explorer's passive treasure draft", Offered(Brann, "ULDA_045", true, 300));
    Check("T3 GILA_511 is offered in Tess's passive treasure draft", Offered(Tess, "GILA_511", true, 300));
    Check("T3 DALA_707 is offered in a Heist villain's treasure draft", Offered(Heist, "DALA_707", false, 600));
    Check("T3 a plain Mage act run is never offered an F15 treasure (deck scope, 200 seeds)", !Offered("MAGE", "ULDA_045", true, 100) && !Offered("MAGE", "DALA_707", false, 100));
    var cls = data.Class(Elise);
    bool offered302 = Enumerable.Range(1, 200).Any(seed => run.SignatureOffer(all, cls, seed).Contains("ULDA_302"));
    var noMajors = new DungeonRunProfile(); noMajors.Unlocked.AddRange(data.Unlocks.Keys.Where(k => k != "druid_priest_majors2"));
    bool locked302 = Enumerable.Range(1, 200).All(seed => !run.SignatureOffer(noMajors, cls, seed).Contains("ULDA_302"));
    Check("T3 ULDA_302 is in Elise's signature offer once druid_priest_majors2 holds, and never before", offered302 && locked302);
    int seed302 = Enumerable.Range(1, 200).First(seed => run.SignatureOffer(all, cls, seed).Contains("ULDA_302"));
    var es = NewAct(Elise, seed302, "ULDA_302", profile: all);
    Check("T3 NewRun with the ULDA_302 signature: the run deck holds it with origin signature, the loadout names it",
        es.Deck.Contains("ULDA_302") && es.Cards.Any(c => c.Id == "ULDA_302" && c.Origin == DungeonRunDeck.OriginSignature) && es.Loadout?.Signature?.Contains("ULDA_302") == true);
    // Zephrys the Great: the catalog drafts it (a Unique entry candidate) once scripted and admitted.
    var zephrysEntries = data.Bundles.Where(b => b.IsPool && b.Candidates.Any(c => c.Id == "ULD_003")).Select(b => b.Id).ToList();
    Check("T3 ULD_003 is a candidate of catalog entries (it was excluded as unscripted and highlander before F15)", zephrysEntries.Count >= 1, string.Join(",", zephrysEntries));
}

// ---------------------------------------------------------------- T4 Start of Game draws: ordering against passive treasures and passive powers
{
    var cls = data.Class(Elise);
    int seed = Enumerable.Range(1, 200).First(x => run.SignatureOffer(all, cls, x).Contains("ULDA_302"));
    var s = NewAct(Elise, seed, "ULDA_302", "ULDA_Elise_HP3", all);
    foreach (string id in new[] { "ULDA_013", "ULDA_040", "ULDA_407" }) Grant(s, id);
    Hold(s, "ULDA_009");
    var plan = run.BuildPlan(s);
    var g = Begin(plan, run.MatchSeed(s));
    var p1 = g.Player1;
    string[] mine = { "ULDA_302", "ULDA_013", "ULDA_040", "ULDA_407" };
    Check("T4 before the mulligan: no Start of Game draw is in the opening hand; all four wait in the deck",
        mine.All(id => p1.DeckZone.Any(e => e.Card.Id == id) && !p1.HandZone.Any(e => e.Card.Id == id)), string.Join(",", p1.HandZone.Select(e => e.Card.Id)));
    var deckMinions = p1.DeckZone.OfType<Minion>().ToList();
    Check("T4 before the mulligan: the passive treasure (ULDA_009 Unlocked Potential) has applied already", deckMinions.Count > 0 && deckMinions.All(m => m.AttackDamage == m.Health));
    Check("T4 before the mulligan: the passive power (ULDA_Elise_HP3) has not activated", !HasFx(p1, "ULDA_Elise_HPe"));
    int opening = p1.HandZone.Count;
    g.Process(ChooseTask.Mulligan(g.Player1, p1.Choice.Choices.Take(2).ToList()));
    g.Process(ChooseTask.Mulligan(g.Player2, new List<int>()));
    Check("T4 after a two-card mulligan: still none of the four in hand (never dealt as a replacement)", mine.All(id => !p1.HandZone.Any(e => e.Card.Id == id)) && p1.HandZone.Count == opening);
    int historyBefore = g.PowerHistory.Full.Count;
    g.MainBegin();
    var drawn = mine.Where(id => p1.HandZone.Any(e => e.Card.Id == id)).ToList();
    Check("T4 after MainBegin: all four are drawn (Start of Game), the deck no longer holds them",
        drawn.Count == 4 && mine.All(id => !p1.DeckZone.Any(e => e.Card.Id == id)), string.Join(",", drawn));
    Check("T4 the passive power is active after Start of Game", HasFx(p1, "ULDA_Elise_HPe"));
    var blocks = g.PowerHistory.Full.Skip(historyBefore).OfType<SabberStoneCore.Kettle.PowerHistoryBlockStart>().Where(b => b.BlockType == BlockType.TRIGGER).Select(b => b.Source).ToList();
    int lastSog = mine.Select(id => p1.HandZone.First(e => e.Card.Id == id).Id).Max(id => blocks.IndexOf(id));
    int power = blocks.IndexOf(p1.Hero.HeroPower.Id);
    Check("T4 history order: every Start of Game draw's trigger block comes before the passive power's", lastSog >= 0 && power > lastSog, $"sog {lastSog} power {power}");
}

// ---------------------------------------------------------------- T5 DALA_707 Loyal Henchman
{
    var s = NewAct(Heist, 501);
    Grant(s, "DALA_707");
    var (_, plan, g) = Fight(s);
    var p1 = g.Player1;
    var hench = InHand(g, "DALA_707") as Minion;
    Check("T5 DALA_707: drawn at the start of the game (in hand on turn 1)", hench != null);
    int a0 = hench?.AttackDamage ?? 0, h0 = hench?.Health ?? 0;
    Check("T5 DALA_707: on turn 1 it has grown once (the turn start in hand): 2/2, marked Scheming", a0 == 2 && h0 == 2 && hench.AppliedEnchantments.Count(e => e.Card.Id == "DALA_707e") == 1, $"{a0}/{h0}");
    NextTurn(g);
    Check("T5 DALA_707: each of its owner's turns in hand adds +1/+1 (the boss's turn does not)", hench.AttackDamage == 3 && hench.Health == 3, $"{hench.AttackDamage}/{hench.Health}");
    Mana(p1, 10); ClearBoards(g);
    Play(g, hench);
    Check("T5 DALA_707: played, a 3/3 Taunt on the board that grows no more", p1.BoardZone.Contains(hench) && hench.HasTaunt && hench.AttackDamage == 3);
    NextTurn(g);
    Check("T5 DALA_707: on the board it keeps its stats", hench.AttackDamage == 3 && hench.Health == 3);
}

// ---------------------------------------------------------------- T6 ULDA_407 Tracking Device
string TrackingTrace(long seed)
{
    var s = NewAct(Brann, seed);
    Grant(s, "ULDA_407");
    var (_, plan, g) = Fight(s);
    var p1 = g.Player1;
    var device = InHand(g, "ULDA_407");
    Clear(p1); p1.HandZone.Add(device);
    SetDeck(g, p1, "CS2_231", "CS2_120", "CS2_168", "CS2_120", "CS2_182", "CS2_187", "CS2_200");
    Mana(p1, 10);
    Play(g, device);
    return string.Join(",", p1.HandZone.Select(e => e.Card.Id + ":" + e.Cost)) + "|" + string.Join(",", p1.DeckZone.Select(e => e.Card.Id).OrderBy(x => x));
}
{
    var s = NewAct(Brann, 502);
    Grant(s, "ULDA_407");
    var (_, plan, g) = Fight(s);
    var p1 = g.Player1;
    var device = InHand(g, "ULDA_407");
    Check("T6 ULDA_407: drawn at the start of the game", device != null);
    Clear(p1); p1.HandZone.Add(device);
    SetDeck(g, p1, "CS2_231", "CS2_120", "CS2_120", "CS2_182", "CS2_187", "CS2_200");
    Mana(p1, 10);
    Play(g, device);
    var costs = p1.HandZone.Select(e => e.Cost).OrderBy(x => x).ToList();
    Check("T6 ULDA_407: with no 3-Cost card in the deck it draws a 2, a 4 and a 5 (three cards)", costs.SequenceEqual(new[] { 2, 4, 5 }), string.Join(",", costs));
    Check("T6 ULDA_407: the deck keeps the 0, the 6 and the other 2", p1.DeckZone.Select(e => e.Card.Id).OrderBy(x => x).SequenceEqual(new[] { "CS2_120", "CS2_200", "CS2_231" }));
    string t1 = TrackingTrace(77), t2 = TrackingTrace(77);
    Check("T6 ULDA_407: the same seed draws the same cards (game RNG)", t1 == t2, t1);
}

// ---------------------------------------------------------------- T7 ULDA_302 Academic Research
{
    var cls = data.Class(Elise);
    int seed = Enumerable.Range(1, 200).First(x => run.SignatureOffer(all, cls, x).Contains("ULDA_302"));
    var s = NewAct(Elise, seed, "ULDA_302", profile: all);
    var (_, plan, g) = Fight(s);
    var p1 = g.Player1;
    var research = InHand(g, "ULDA_302");
    Check("T7 ULDA_302 (Elise's signature): drawn at the start of the game, at 1 Mana Crystal", research != null && DungeonRunContent.AcademicResearchAmount(research) == 1);
    Mana(p1, 3); ClearBoards(g);
    var a = Board(g, p1, "CS2_120"); var b = Board(g, p1, "CS2_168"); var foe = Board(g, g.Player2, "CS2_120");
    Kill(g, a); Kill(g, b); Kill(g, foe);
    Check("T7 ULDA_302: two friendly minions died: it now gains 3 (an enemy death does not count)", DungeonRunContent.AcademicResearchAmount(research) == 3, DungeonRunContent.AcademicResearchAmount(research).ToString());
    Play(g, research);
    Check("T7 ULDA_302: played at 3 Mana: 6 full Mana Crystals", p1.BaseMana == 6 && p1.RemainingMana == 6, $"{p1.BaseMana} {p1.RemainingMana}");
    var r2 = ToHand(g, p1, "ULDA_302");
    Mana(p1, 9);
    Play(g, r2);
    Check("T7 ULDA_302: a fresh copy gains 1; Mana Crystals stop at 10", p1.BaseMana == 10, p1.BaseMana.ToString());
}

// ---------------------------------------------------------------- T8 the wish: ULDA_013, ULD_003, ULDA_040
{
    var s = NewAct(Reno, 801);
    Grant(s, "ULDA_013");
    var (_, plan, g) = Fight(s);
    var p1 = g.Player1; var p2 = g.Player2;
    var lamp = InHand(g, "ULDA_013");
    Check("T8 ULDA_013: drawn at the start of the game", lamp != null);
    Clear(p1); p1.HandZone.Add(lamp); ClearBoards(g); Mana(p1, 4);
    p2.Hero.Damage = p2.Hero.Health - 5 + p2.Hero.Damage;
    Play(g, lamp);
    var offer = ChoiceIds(g);
    var expected = DungeonRunContent.WishOffer(p1);
    Check("T8 ULDA_013: playing it Discovers three cards: the wish's offer", offer.Length == 3 && offer.SequenceEqual(expected), string.Join(",", offer) + " vs " + string.Join(",", expected));
    Check("T8 lethal: the enemy hero at 5 Health: the first option is the smallest burn that kills (Fireball, 6)", offer[0] == "CS2_029", string.Join(",", offer));
    // Public information only: the enemy's hidden hand and deck, and the owner's own deck, change; the offer does not.
    foreach (var e in p2.HandZone.ToArray()) { p2.HandZone.Remove(e); p2.SetasideZone.Add(e); }
    for (int i = 0; i < 4; i++) Entity.FromCard(p2, C(i % 2 == 0 ? "EX1_279" : "CS2_032"), null, p2.HandZone);
    foreach (var e in p2.DeckZone.Take(5).ToArray()) { p2.DeckZone.Remove(e); p2.SetasideZone.Add(e); }
    SetDeck(g, p1, "CS2_231");
    var mutated = DungeonRunContent.WishOffer(p1);
    Check("T8 public information only: new hidden enemy cards (same hand count), a thinner enemy deck and another own deck give the same three options",
        mutated.SequenceEqual(expected), string.Join(",", mutated));
    Pick(g, g.Player1.Choice.Choices[0]);
    Check("T8 the picked option joins the hand (Fireball)", p1.HandZone.Any(e => e.Card.Id == "CS2_029"));
    // Board clear and heal: a public change of the enemy board and the owner's Health changes the offer.
    Clear(p1);
    for (int i = 0; i < 3; i++) Board(g, p2, "CS2_168");
    p2.Hero.Damage = 0; p1.Hero.Damage = 20; Mana(p1, 4);
    var clear = DungeonRunContent.WishOffer(p1);
    Check("T8 board clear: three 2/1 Murlocs: Arcane Explosion (1 damage clears them); the owner at 10 Health: Healing Touch",
        clear.Contains("CS2_025") && clear.Contains("CS2_007") && clear.Count == 3, string.Join(",", clear));
    foreach (var m in p2.BoardZone.ToArray()) { p2.BoardZone.Remove(m); p2.SetasideZone.Add(m); }
    Board(g, p2, "CS2_200"); Board(g, p2, "CS2_201");
    p1.Hero.Damage = 0;
    var big = DungeonRunContent.WishOffer(p1);
    Check("T8 board clear: a 6/7 and a 9/5 with 15 Attack: Twisting Nether (no damage clear reaches 7 Health) and a Taunt",
        big.Contains("EX1_312") && (big.Contains("CS2_179") || big.Contains("CS2_162")), string.Join(",", big));
    // Zephrys the Great in the run deck (a drafted card): the wish only when the deck has no duplicates.
    var zs = NewAct(Reno, 802);
    Draft(zs, "ULD_003"); Draft(zs, "ULD_003");
    var (_, zplan, zg) = Fight(zs);
    var z1 = zg.Player1;
    var zeph = z1.HandZone.FirstOrDefault(e => e.Card.Id == "ULD_003") ?? z1.DeckZone.First(e => e.Card.Id == "ULD_003");
    if (zeph.Zone.Type == Zone.DECK) { z1.DeckZone.Remove(zeph); Generic.AddHandPhase.Invoke(z1, zeph); }
    var other = z1.DeckZone.Concat(z1.HandZone).First(e => e.Card.Id == "ULD_003" && e != zeph);
    if (other.Zone.Type == Zone.HAND) { z1.HandZone.Remove(other); z1.SetasideZone.Add(other); }
    SetDeck(zg, z1, "CS2_231", "CS2_120", "CS2_120");
    Clear(z1); z1.HandZone.Add(zeph); ClearBoards(zg); Mana(z1, 10);
    Play(zg, zeph);
    Check("T8 ULD_003: a duplicate in the deck: no wish (no choice), the 3/2 is on the board", zg.Player1.Choice == null && z1.BoardZone.Contains(zeph));
    SetDeck(zg, z1, "CS2_231", "CS2_120", "CS2_182");
    other.Zone?.Remove(other); Generic.AddHandPhase.Invoke(z1, other);
    Play(zg, other);
    var zoffer = ChoiceIds(zg);
    Check("T8 ULD_003: a deck with no duplicates: the Battlecry wishes (three options, the wish's offer)",
        zoffer.Length == 3 && zoffer.SequenceEqual(DungeonRunContent.WishOffer(z1)), string.Join(",", zoffer));
    // Sack of Lamps.
    var ss = NewAct(Reno, 803);
    Grant(ss, "ULDA_040");
    var (_, splan, sg) = Fight(ss);
    var s1 = sg.Player1;
    var sack = sg.Player1.HandZone.FirstOrDefault(e => e.Card.Id == "ULDA_040");
    Check("T8 ULDA_040: drawn at the start of the game", sack != null);
    Clear(s1); s1.HandZone.Add(sack); ToHand(sg, s1, "CS2_231"); Mana(s1, 1);
    Play(sg, sack);
    Check("T8 ULDA_040: played: the hand is full of Zephrys's Lamps (9 with the kept Wisp)",
        s1.HandZone.IsFull && s1.HandZone.Count(e => e.Card.Id == "ULDA_013") == 9 && s1.HandZone.Count(e => e.Card.Id == "CS2_231") == 1);
    var generated = s1.HandZone.First(e => e.Card.Id == "ULDA_013");
    Play(sg, generated);
    Check("T8 a generated Lamp wishes too (three options)", ChoiceIds(sg).Length == 3);
}

// ---------------------------------------------------------------- T9 ULDA_045 Primordial Bulwark and its Secret ULDA_045t
{
    var s = NewAct(Elise, 901);
    Hold(s, "ULDA_045");
    var plan = run.BuildPlan(s);
    var g = Begin(plan, run.MatchSeed(s));
    var p1 = g.Player1; var p2 = g.Player2;
    var secret = p1.SecretZone.FirstOrDefault(e => e.Card.Id == "ULDA_045t");
    Check("T9 ULDA_045: before the mulligan its Secret ULDA_045t is in my Secret zone, a live Secret", secret != null && secret.IsSecret && !secret.IsExhausted && secret.ActivatedTrigger != null);
    Finish(g);
    Check("T9 ULDA_045t: on my own turn 1 it is not exhausted (it can trigger on my turn)", !secret.IsExhausted);
    var ice = (Spell)Entity.FromCard(p1, C("EX1_295"), null, p1.SecretZone);
    ice.Card.Power.Trigger.Activate(ice); ice.IsExhausted = true;
    NextTurn(g);
    Check("T9 my turn start exhausts an ordinary Secret (Ice Block) and not ULDA_045t", ice.IsExhausted && !secret.IsExhausted);
    ice.ActivatedTrigger?.Remove(); p1.SecretZone.Remove(ice); p1.SetasideZone.Add(ice);
    ClearBoards(g); Clear(p1); Mana(p1, 10);
    p1.Hero.Damage = p1.Hero.Health - 3 + p1.Hero.Damage; p1.Hero.Armor = 0;
    int enemyBefore = p2.Hero.Health;
    var hellfire = ToHand(g, p1, "CS2_062");
    Play(g, hellfire);
    Check("T9 my own Hellfire (3 to all) at 3 Health on my turn: the fatal damage is blocked, I live at 3", p1.Hero.Health == 3 && g.State != State.COMPLETE, p1.Hero.Health.ToString());
    Check("T9 the enemy hero takes Hellfire's 3 and the Secret's $20", p2.Hero.Health == enemyBefore - 23, $"{enemyBefore}->{p2.Hero.Health}");
    Check("T9 once per game: the Secret is revealed and gone to the graveyard", !p1.SecretZone.Any(e => e.Card.Id == "ULDA_045t") && p1.GraveyardZone.Any(e => e.Card.Id == "ULDA_045t"));
    // On the boss's turn: a lethal attack is blocked too.
    var s2 = NewAct(Brann, 902);
    Hold(s2, "ULDA_045");
    var (_, plan2, g2) = Fight(s2);
    var q1 = g2.Player1; var q2 = g2.Player2;
    ClearBoards(g2);
    var ogre = Board(g2, q2, "CS2_200");
    q1.Hero.Damage = q1.Hero.Health - 4 + q1.Hero.Damage; q1.Hero.Armor = 0;
    int bossBefore = q2.Hero.Health;
    EndTurn(g2);
    g2.Process(MinionAttackTask.Any(q2, ogre, q1.Hero));
    Check("T9 on the boss's turn a 6-damage attack at 4 Health is blocked, the boss takes $20", q1.Hero.Health == 4 && q2.Hero.Health == bossBefore - 20 && g2.State != State.COMPLETE,
        $"{q1.Hero.Health} {bossBefore}->{q2.Hero.Health}");
    Check("T9 a non-fatal hit does not use it (a fresh Bulwark game)", true);
    var s3 = NewAct(Brann, 903);
    Hold(s3, "ULDA_045");
    var (_, plan3, g3) = Fight(s3);
    ClearBoards(g3);
    var croc = Board(g3, g3.Player2, "CS2_120");
    int life = g3.Player1.Hero.Health;
    EndTurn(g3);
    g3.Process(MinionAttackTask.Any(g3.Player2, croc, g3.Player1.Hero));
    Check("T9 a 2-damage hit at full Health goes through and keeps the Secret", g3.Player1.Hero.Health == life - 2 && g3.Player1.SecretZone.Any(e => e.Card.Id == "ULDA_045t"));
}

// ---------------------------------------------------------------- T10 the boss AI: Secret prior, sampled worlds, replies
{
    var s = NewAct(Elise, 1001);
    Hold(s, "ULDA_045");
    var plan = run.BuildPlan(s);
    var prior = DungeonRunAdmission.SecretPrior(data, run.RunClasses(s), plan.PlayerGeneratedSecrets);
    Check("T10 the boss's Secret prior for an explorer run holds ULDA_045t (ULDA_045's token, a public design-time source)", prior != null && prior.ContainsKey("ULDA_045t"),
        prior == null ? "null" : prior.Count.ToString());
    var mage = data.Class("MAGE");
    var ms = run.NewRun(all, "MAGE", mage.DefaultDeck, mage.DefaultPower, 1001);
    var mprior = DungeonRunAdmission.SecretPrior(data, run.RunClasses(ms), run.BuildPlan(ms).PlayerGeneratedSecrets);
    Check("T10 a plain Mage run's prior never holds it (no ULDA_045 in its scope)", mprior == null || !mprior.ContainsKey("ULDA_045t"));
    var g = StartFight(plan, run.MatchSeed(s));
    EndTurn(g);
    FairBotState st = null; string why = "";
    try { st = FairBotState.Create(g, g.Player2.PlayerId, 7, DeckStyle.Tempo, new FairOptions { RemoveOpponentSecrets = true, TrackOwnDeck = true, OpponentSecretPrior = prior }); }
    catch (Exception e) { why = e.GetType().Name + ": " + e.Message; }
    Check("T10 the boss's fair view builds on its turn with the player's hidden Secret", st != null, why);
    if (st != null)
    {
        var worlds = st.PlausibleOpponentSecretWorlds(out bool complete, out double mass);
        Check("T10 the boss's sampled Secret worlds include ULDA_045t", worlds.Any(w => w.Length == 1 && w[0] == "ULDA_045t"), string.Join(";", worlds.Take(6).Select(w => string.Join(",", w))));
        Check("T10 the fair view itself does not show the Secret's identity", !st.FingerprintGame.Player1.SecretZone.Any(e => e.Card.Id == "ULDA_045t"));
        var world = st.WithOpponentSecrets(new[] { "ULDA_045t" }, 3);
        var wsecret = world.FingerprintGame.Player1.SecretZone.First(e => e.Card.Id == "ULDA_045t");
        Check("T10 installed on the boss's turn: live", !wsecret.IsExhausted && wsecret.ActivatedTrigger != null);
        var reply = world.Apply(world.ReplyEndTurnAction);
        var rsecret = reply.FingerprintGame.Player1.SecretZone.FirstOrDefault(e => e.Card.Id == "ULDA_045t");
        Check("T10 the reply (the player's turn in the clone): ULDA_045t stays live on its owner's turn", reply.FingerprintGame.CurrentPlayer == reply.FingerprintGame.Player1
            && rsecret != null && !rsecret.IsExhausted);
        var iceWorld = st.WithOpponentSecrets(new[] { "EX1_295" }, 3).Apply(world.ReplyEndTurnAction);
        var rice = iceWorld.FingerprintGame.Player1.SecretZone.FirstOrDefault(e => e.Card.Id == "EX1_295");
        Check("T10 the same reply with an ordinary Secret (Ice Block): exhausted on its owner's turn", rice != null && rice.IsExhausted);
    }
}

// ---------------------------------------------------------------- T11 GILA_511 Sticky Fingers
{
    var s = NewAct(Tess, 1101);
    Hold(s, "GILA_511");
    var (_, plan, g) = Fight(s);
    var p1 = g.Player1; var p2 = g.Player2;
    Check("T11 GILA_511: the plan lists it and its player enchantment (GILA_511e) is attached", plan.PlayerTreasures.Contains("GILA_511") && HasFx(p1, "GILA_511e"));
    var started = p1.HandZone.Concat(p1.DeckZone).Where(e => e.Card.Cost > 0).ToList();
    Check("T11 GILA_511: the cards that started in my deck keep their Cost", started.Count > 0 && started.All(e => e.Cost == e.Card.Cost),
        string.Join(",", started.Where(e => e.Cost != e.Card.Cost).Select(e => e.Card.Id)));
    var yeti = ToHand(g, p1, "CS2_182");
    Check("T11 GILA_511: a card that did not start in my deck costs (1) less (Yeti 4 -> 3)", yeti.Cost == 3, yeti.Cost.ToString());
    var theirs = ToHand(g, p2, "CS2_182");
    Check("T11 GILA_511: the boss's cards are not discounted", theirs.Cost == 4);
    var drawnLater = p1.DeckZone.First(e => e.Card.Cost > 0);
    p1.DeckZone.Remove(drawnLater); Generic.AddHandPhase.Invoke(p1, drawnLater); g.DeathProcessingAndAuraUpdate();
    Check("T11 GILA_511: a deck card drawn later keeps its Cost", drawnLater.Cost == drawnLater.Card.Cost);
}

// ---------------------------------------------------------------- T12 the three phase 9 ids: one final rejection, two match ends (step 9)
foreach (string id in phase9)
{
    var rule = DungeonRunAdmission.RuleFor(id);
    var v = DungeonRunAdmission.Classify(C(id), id, AdmitScope.Player, null, catalogRun: true);
    string lift = DungeonRunAdmission.TurnRestartLift;
    // Step 9 made GILA_900p's lift a final rejection ("none: a final rejection (step 9, ...)"); before, "phase 9 (economy)".
    Check($"T12 {id}: unscripted, no content, no treasure row and no power row; its admission row admits no scope, a final rejection (step 9)",
        !C(id).Implemented && !DungeonRunContent.TryGet(id, out _) && data.FindTreasure(id) == null && !data.Classes.Any(c => c.Powers.Contains(id))
        && rule != null && rule.Admit == AdmitScope.None && !v.AdmittedSome && rule.Lift == lift && lift.StartsWith("none: a final rejection (step 9"), rule?.Family + " " + rule?.Lift);
}
foreach (string id in DungeonRunContent.MatchEndIds)
{
    var rule = DungeonRunAdmission.RuleFor(id);
    var v = DungeonRunAdmission.Classify(C(id), id, AdmitScope.Player, null, catalogRun: true);
    var t = data.FindTreasure(id);
    Check($"T12 {id}: scripted with content and a tuned run's treasure row (runs v4); admitted for the run player only, never generated (step 9)",
        C(id).Implemented && DungeonRunContent.TryGet(id, out _) && t != null && t.Runs == DungeonRunTreasure.RunsV4
        && rule != null && rule.Admit == AdmitScope.Player && rule.NeverGenerated && v.Admitted && rule.Lift == DungeonRunAdmission.RunTreasureLift,
        rule?.Family + " " + rule?.Admit + " " + t?.Runs);
}
Check("T12 Toki (GILA_900h) keeps the Mage basic power as the stand-in for Temporal Loop", data.Class("GILA_900h").Powers.SequenceEqual(new[] { "CS2_034" }));

// ---------------------------------------------------------------- T13 phase 7 D: the Rumble Run companions share the Start of Game draw
// The nine start-of-game companions (DungeonRunShrines.CompanionIds; c-companions-D tests each one) are declared through the same
// self-drawing Start of Game rule as the treasures here (DungeonRunContent.DeclareSogDraw, now internal), at a later priority:
// on a board-shrine run the shrine lands first (-100), then the treasures' Start of Game effects and draws (0), then the companion (10).
{
    string[] mates = DungeonRunShrines.CompanionIds.ToArray();
    Check("T13 the nine companions and the five treasures are self-drawing Start of Game sources",
        mates.Length == 9 && mates.All(id => C(id).Implemented && StartOfGameRules.HasEffect(C(id))) && DungeonRunContent.StartOfGameDrawIds.All(id => StartOfGameRules.HasEffect(C(id))));
    var cls = data.Class("TRLA_209h_Paladin");
    var s = run.NewRun(all, cls.Id, "rumble_paladin_105", cls.DefaultPower, 905, signatureId: run.SignatureOffer(all, cls, 905).FirstOrDefault());
    DungeonRunDeck.Add(s, "TRLA_139", DungeonRunDeck.OriginTeammate);
    Grant(s, "DALA_707");
    Hold(s, "DALA_743");
    var plan = run.BuildPlan(s);
    var g = Begin(plan, run.MatchSeed(s));
    var p1 = g.Player1;
    Check("T13 before the mulligan: neither the companion (TRLA_139) nor the treasure draw (DALA_707) is in the opening hand",
        InHand(g, "TRLA_139") == null && InHand(g, "DALA_707") == null && p1.DeckZone.Any(e => e.Card.Id == "TRLA_139") && p1.DeckZone.Any(e => e.Card.Id == "DALA_707"));
    g.Process(ChooseTask.Mulligan(g.Player1, p1.Choice.Choices.ToList()));
    g.Process(ChooseTask.Mulligan(g.Player2, new List<int>()));
    Check("T13 a full mulligan deals neither", InHand(g, "TRLA_139") == null && InHand(g, "DALA_707") == null);
    int before = g.PowerHistory.Full.Count;
    g.MainBegin();
    var blocks = g.PowerHistory.Full.Skip(before).OfType<SabberStoneCore.Kettle.PowerHistoryBlockStart>().Where(b => b.BlockType == BlockType.TRIGGER).Select(b => b.Source).ToList();
    var shrine = ShrineRules.Of(p1).FirstOrDefault();
    var remote = p1.AppliedEnchantments?.FirstOrDefault(e => e.Card.Id == DungeonRunContent.Get("fx_DALA_743").ShellId);
    var hench = InHand(g, "DALA_707"); var marshal = InHand(g, "TRLA_139");
    int at(IEntity e) => e == null ? -1 : blocks.IndexOf(e.Id);
    Check("T13 after MainBegin both are drawn; Start of Game order: the shrine, then the treasures (DALA_707's draw, DALA_743's Boom Bots), then the companion",
        hench != null && marshal != null && shrine != null && at(shrine) >= 0 && at(hench) > at(shrine) && at(remote) > at(shrine)
        && at(marshal) > at(hench) && at(marshal) > at(remote), $"shrine {at(shrine)} hench {at(hench)} remote {at(remote)} marshal {at(marshal)}");
    Check("T13 the shrine is left of the Boom Bots", shrine?.ZonePosition == 0 && p1.BoardZone.Count(m => m.Card.Id == DungeonRunContent.BoomBot) == DungeonRunContent.RemoteBoomBots);
}

// ---------------------------------------------------------------- T30 full games against the real boss AI
var testBudget = new Budget(6000, 2000, 300, 6, 3, 100, 8, turnSearchSeconds: 6, turnWallSeconds: 8);
string AiGame(string row, string id, long seed, bool passive, bool draft = false)
{
    var s = NewAct(row, seed, id == "ULDA_302" ? "ULDA_302" : null, profile: id == "ULDA_302" ? all : null);
    if (passive) Hold(s, id); else if (draft) Draft(s, id); else if (id != "ULDA_302") Grant(s, id);
    for (int i = 0; i < 2; i++) s.Matches.Add(new DungeonRunMatchSummary { Floor = 1, Boss = s.BossSequence[0], Outcome = DungeonRunOutcome.Win });
    var boss = run.CurrentBoss(s);
    var plan = run.BuildPlan(s);
    var pilot = DungeonRunBossPilot.Build(boss, plan, data, runClasses: run.RunClasses(s));
    var profile = pilot.Profile.WithBudget(testBudget);
    var golden = new GoldenPilot(profile, pilot.Plan, null, pilot.SecretPrior);
    var cfg = new GameConfig { RandomSeed = run.MatchSeed(s), Shuffle = true, SkipMulligan = false, History = false, Logging = false, FormatType = FormatType.FT_WILD };
    DungeonRunSetup.Configure(cfg, plan);
    var game = new Game(cfg);
    game.StartGame();
    DungeonRunSetup.ApplyAfterStart(game, plan);
    game.Process(ChooseTask.Mulligan(game.Player1, new List<int>()));
    var choice = game.Player2.Choice;
    var view = new MulliganView(choice.Choices.Select(x => new MulliganCard(x, game.IdEntityDic[x].Card.Id, game.IdEntityDic[x].Card.Cost)),
        false, pilot.Deck.CardIds, game.Player1.Hero.Card.Class);
    game.Process(ChooseTask.Mulligan(game.Player2, BotDriver.Mulligan(view, new DeckPlanMulligan(pilot.Plan)).ToList()));
    game.MainBegin();
    var p1 = game.Player1;
    int cardId = -1;
    if (!passive)
    {
        if (!p1.HandZone.Any(p => p.Card.Id == id))
        {
            var e = p1.DeckZone.First(p => p.Card.Id == id);
            p1.DeckZone.Remove(e);
            if (p1.HandZone.IsFull) { var drop = p1.HandZone.Last(); p1.HandZone.Remove(drop); p1.DeckZone.Add(drop); }
            Generic.AddHandPhase.Invoke(p1, e);
        }
        cardId = p1.HandZone.First(p => p.Card.Id == id).Id;
    }
    int decisionIndex = 0, actions = 0, decisions = 0, fallbacks = 0, playerChoices = 0;
    bool played = passive;
    void Act(PlayerTask task, string who)
    {
        if (++actions > 1500) throw new Exception("stuck after 1500 actions");
        if (task is ChooseTask pick && game.Step != Step.BEGIN_MULLIGAN)
        {
            var decision = game.GetPendingDecisions().Single(dd => dd.ControllerId == task.Controller.Id);
            if (!game.TryAnswer(new SabberStoneCore.Model.Decisions.DecisionAnswer(decision.Id, decision.ControllerId, pick.Choices.ToArray()), out var why))
                throw new Exception($"engine rejected {who} choice ({why})");
            if (who == "player") playerChoices++;
        }
        else if (!game.Process(task)) throw new Exception($"engine rejected {who} task {task}");
    }
    while (game.State != State.COMPLETE && game.Turn <= 60)
    {
        if (game.CurrentPlayer == game.Player2 && game.Player1.Choice == null)
        {
            var (task, trace) = BotDriver.Decide(game, game.Player2.PlayerId, pilot.Deck.Style,
                st => golden.DecidePilot(st, new GoldenContext(game.Turn, decisionIndex, unchecked(997 * game.Turn + decisionIndex * 31), profile)),
                ref decisionIndex, golden.Options);
            decisions++;
            if (trace?.Kind == "fallback") fallbacks++;
            Act(task, "boss");
        }
        else
        {
            var options = game.Player1.Options();
            var mine = options.OfType<PlayCardTask>().Where(o => o.Source?.Id == cardId).OrderBy(o => o.Target == null ? 0 : o.Target.Controller == game.Player1 ? 1 : 0).FirstOrDefault();
            if (mine != null) { played = true; Act(mine, "player"); }
            else Act(LocalBot.Choose(options), "player");
        }
    }
    string result = game.State != State.COMPLETE ? "turn-cap" : game.Player2.PlayState == PlayState.WON ? "boss" : game.Player1.PlayState == PlayState.WON ? "player" : "tie";
    return $"{result} turns {game.Turn} played {(played ? 1 : 0)} player-choices {playerChoices} decisions {decisions} fallbacks {fallbacks} boss {boss.Id}";
}
var games = new (string row, string id, bool passive, bool draft)[]
{
    (Heist, "DALA_707", false, false), (Brann, "ULDA_407", false, false), (Elise, "ULDA_302", false, false), (Reno, "ULDA_013", false, false),
    (Reno, "ULDA_040", false, false), (Reno, "ULD_003", false, true), (Elise, "ULDA_045", true, false), (Brann, "ULDA_045", true, false),
    (Tess, "GILA_511", true, false),
};
foreach (var x in games)
{
    string line;
    try { line = AiGame(x.row, x.id, 281, x.passive, x.draft); }
    catch (Exception e) { line = "FAULT " + e.GetType().Name + ": " + e.Message.Split('\n')[0]; }
    Console.WriteLine($"T30 {x.id,-9} {x.row,-15} {line}");
    Check($"T30 {x.id} ({x.row}): a full game against the boss AI that ends without a fault or an AI fallback{(x.passive ? " (the player holds the passive)" : ", the player playing it")}",
        !line.StartsWith("FAULT") && line.Contains("fallbacks 0 ") && line.Contains("played 1 "), line);
}

Console.WriteLine($"{passes} passed, {failures} failed");
return failures == 0 ? 0 : 1;
