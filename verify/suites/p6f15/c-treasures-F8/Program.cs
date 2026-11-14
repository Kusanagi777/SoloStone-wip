// c-treasures-F8: phase 6 family F8 of the unified Dungeon Run, the Dalaran Heist passive treasures (DALA_728, 731, 733, 735,
// 736, 737, 738, 739, 741, 743, 744, 745, 746, 747, 748; DALA_740 is rejected with a stated reason). For every id: the engine
// content (scripted, keyed and shelled by its own client id, name and text as facts/violet-hold-2026-09.json has them) and its
// effect shell, the run data (run-v1.json rows scoped to the 35 Heist starters, pool Heist), the validator and admission checks,
// a scripted regression game through the run setup path (DungeonRun.NewRun on a Heist villain's hero row -> BuildPlan ->
// DungeonRunSetup.Configure -> Game -> ApplyAfterStart -> mulligan -> MainBegin) asserting the board, hand, deck and hero
// outcome (the Start of Game treasures resolve after both mulligans), and a full game against the real boss AI
// (DungeonRunBossPilot + GoldenPilot) that must end without a fault or an AI fallback. Headless: compiles
// C:\SoloStone\plugin\DungeonRun\*.cs and the NPC AI against p6f8/engine-out. Run it twice and compare the output.
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
using SabberStoneCore.Model.Costs;
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
var factById = facts.RootElement.GetProperty("cards").EnumerateArray().ToDictionary(c => c.GetProperty("id").GetString(), c => c);
string FactText(string id) => factById[id].TryGetProperty("text", out var t) ? t.GetString() : null;
int FactTag(string id, int tag)
{
    foreach (var pair in factById[id].GetProperty("tags").EnumerateArray())
        if (pair[0].GetInt32() == tag) return pair[1].GetInt32();
    return 0;
}
Card C(string id) => Cards.FromId(id);
var clientIds = new HashSet<string>(System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "dungeon-run/reference/client-tables.json")))
    .RootElement.GetProperty("tables").GetProperty("CARD").EnumerateArray().Select(r => r.GetProperty("id").GetString()), StringComparer.Ordinal);

CardType FactType(string id) => FactTag(id, 202) switch { 3 => CardType.HERO, 4 => CardType.MINION, 5 => CardType.SPELL, 6 => CardType.ENCHANTMENT, 7 => CardType.WEAPON, 10 => CardType.HERO_POWER, _ => CardType.INVALID };
var transport = new Dictionary<string, string>
{
    ["CS2_034"] = "HERO_08bp", ["CS2_102"] = "HERO_01bp", ["CS1h_001"] = "HERO_09bp", ["CS2_101"] = "HERO_04bp", ["CS2_017"] = "HERO_06bp",
    ["DS1h_292"] = "HERO_05bp", ["CS2_083b"] = "HERO_03bp", ["CS2_056"] = "HERO_07bp", ["CS2_049"] = "HERO_02bp",
    ["AT_132_WARRIOR"] = "HERO_01bp2", ["AT_132_SHAMAN"] = "HERO_02bp2", ["AT_132_ROGUE"] = "HERO_03bp2", ["AT_132_PALADIN"] = "HERO_04bp2",
    ["AT_132_HUNTER"] = "HERO_05bp2", ["AT_132_DRUID"] = "HERO_06bp2", ["AT_132_WARLOCK"] = "HERO_07bp2", ["AT_132_MAGE"] = "HERO_08bp2", ["AT_132_PRIEST"] = "HERO_09bp2",
};
// ---------------------------------------------------------------- the run setup path
GameConfig Configured(RunMatchPlan plan, int seed)
{
    var cfg = new GameConfig { RandomSeed = seed, Shuffle = true, SkipMulligan = false, History = true, Logging = false, FormatType = FormatType.FT_WILD };
    DungeonRunSetup.Configure(cfg, plan);
    return cfg;
}
DungeonRunState NewAct(string row, long seed, string power = null)
{
    var cls = data.Class(row);
    var s = run.NewRun(all, cls.Id, cls.DefaultDeck, power ?? cls.DefaultPower, seed, signatureId: run.SignatureOffer(all, run.Data.FindClass(cls.Id), seed).FirstOrDefault());
    if (!s.IsActRun || s.Phase != DungeonRunPhase.PreMatch || s.Floor != 1) throw new Exception("act run not at floor 1 pre-match");
    return s;
}
void Hold(DungeonRunState s, params string[] ids)
{
    foreach (string id in ids)
    {
        if (data.FindTreasure(id).Active) { s.ActiveTreasures.Add(id); DungeonRunDeck.Add(s, DungeonRunContent.Get(id).ShellId, DungeonRunDeck.OriginTreasure); }
        else s.Treasures.Add(id);
    }
}
Game StartFight(RunMatchPlan plan, int seed)
{
    var g = new Game(Configured(plan, seed));
    g.StartGame();
    DungeonRunSetup.ApplyAfterStart(g, plan);
    g.Process(ChooseTask.Mulligan(g.Player1, new List<int>()));
    g.Process(ChooseTask.Mulligan(g.Player2, new List<int>()));
    g.MainBegin();
    if (g.CurrentPlayer != g.Player1) throw new Exception("run player not first");
    return g;
}
(DungeonRunState s, RunMatchPlan plan, Game g) Fight(string row, long seed, string[] treasures, string power = null)
{
    var s = NewAct(row, seed, power);
    Hold(s, treasures);
    var plan = run.BuildPlan(s);
    return (s, plan, StartFight(plan, run.MatchSeed(s)));
}
IPlayable Draw(Game g, string id)
{
    var p = g.Player1;
    var e = p.HandZone.FirstOrDefault(x => x.Card.Id == id);
    if (e != null) return e;
    e = p.DeckZone.FirstOrDefault(x => x.Card.Id == id) ?? throw new Exception(id + " is not in the run deck");
    Generic.Draw(p, e);
    g.DeathProcessingAndAuraUpdate();
    return e;
}
void Mana(Controller c, int n) { c.BaseMana = n; c.UsedMana = 0; }
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
bool Play(Game g, IPlayable card, ICharacter target = null, int pos = -1) => g.Process(PlayCardTask.Any(g.Player1, card, target, pos));
bool UsePower(Game g, ICharacter target = null) => g.Process(HeroPowerTask.Any(g.Player1, target));
void EndTurn(Game g) => g.Process(EndTurnTask.Any(g.CurrentPlayer));
void Clear(Controller c) { foreach (var e in c.HandZone.ToArray()) { c.HandZone.Remove(e); c.SetasideZone.Add(e); } }
bool FromRunDeck(DungeonRunState s, RunMatchPlan plan, string id) => plan.PlayerDeck.Any(c => c.Id == id) && s.Deck.Contains(id);
void Kill(Game g, Minion m) { m.Damage = m.Health; g.DeathProcessingAndAuraUpdate(); }
// Draws every card of the deck that the predicate rejects, so the rest of the deck is known.
void Thin(Game g, Controller c, Func<IPlayable, bool> keep)
{
    foreach (var e in c.DeckZone.ToArray()) if (!keep(e)) { c.DeckZone.Remove(e); c.SetasideZone.Add(e); }
    g.DeathProcessingAndAuraUpdate();
}



const string Rakanishu = "DALA_Rakanishu", Chu = "DALA_Chu", Eudora = "DALA_Eudora", George = "DALA_George", Squeamlish = "DALA_Squeamlish",
    Kriziki = "DALA_Kriziki", Tekahn = "DALA_Tekahn", Barkeye = "DALA_Barkeye", Vessina = "DALA_Vessina";
string[] villains = { Rakanishu, Barkeye, Vessina, Kriziki, Eudora, Chu, Squeamlish, Tekahn, George };
string[] f8 = DungeonRunContent.HeistPassiveIds.ToArray();
string[] shipped = f8.Where(id => id != "DALA_740").ToArray();
var heistDecks = villains.SelectMany(v => data.Class(v).Decks).ToList();
// Effect content key -> its shell, and whether the shell is the client's own enchantment (true) or a borrowed one (false).
var effects = new Dictionary<string, (string Shell, bool Own)>
{
    ["fx_DALA_728"] = ("DALA_728e", true), ["fx_DALA_731"] = ("DALA_731e", true), ["fx_DALA_733"] = ("DALA_733e", true),
    ["fx_DALA_735"] = ("DALA_735e", true), ["fx_DALA_736"] = ("DALA_736e", true), ["fx_DALA_737e"] = ("DALA_737e", true),
    ["fx_DALA_741"] = ("DALA_741e", true), ["fx_DALA_741e2"] = ("DALA_741e2", true), ["fx_DALA_744"] = ("DALA_744e", true),
    ["fx_DALA_744e2"] = ("DALA_744e2", true), ["fx_DALA_746"] = ("DALA_746e", true), ["fx_DALA_747"] = ("DALA_747e", true),
    ["fx_DALA_748"] = ("DALA_748e", true),
    ["fx_DALA_737"] = ("TB_BaconShop_1P_PlayerE", false), ["fx_DALA_739"] = ("TB_BaconShop_8P_PlayerE", false),
    ["fx_DALA_743"] = ("TB_BaconShop_3ofKindChecke", false), ["fx_DALA_745"] = ("TB_DeckRecipe_MyDeckID", false),
    ["fx_DALA_746_cost"] = ("GBL_001e", false),
};
string[] sogShells = { "TB_BaconShop_1P_PlayerE", "TB_BaconShop_8P_PlayerE", "TB_BaconShop_3ofKindChecke", "TB_DeckRecipe_MyDeckID" };
bool HasFx(Controller c, string shell) => c.AppliedEnchantments?.Any(e => e.Card.Id == shell) == true;
// A fight stopped before the mulligan: the run setup path up to ApplyAfterStart.
(DungeonRunState s, RunMatchPlan plan, Game g) PreMulligan(string row, long seed, params string[] treasures)
{
    var s = NewAct(row, seed);
    Hold(s, treasures);
    var plan = run.BuildPlan(s);
    var g = new Game(Configured(plan, run.MatchSeed(s)));
    g.StartGame();
    DungeonRunSetup.ApplyAfterStart(g, plan);
    return (s, plan, g);
}
void Finish(Game g)
{
    g.Process(ChooseTask.Mulligan(g.Player1, new List<int>()));
    g.Process(ChooseTask.Mulligan(g.Player2, new List<int>()));
    g.MainBegin();
}

// ---------------------------------------------------------------- T1 content
Check("T1 16 F8 ids, DALA_740 the one without content", f8.Length == 16 && f8.Distinct().Count() == 16 && shipped.Length == 15
    && !DungeonRunContent.TryGet("DALA_740", out _) && DungeonRunContent.ForShell("DALA_740") == null);
foreach (string id in shipped)
{
    var content = DungeonRunContent.Get(id);
    var card = C(id);
    Check($"T1 {id}: a PassiveTreasure keyed and shelled by its own id, scripted, the client's {FactType(id)}",
        content.Kind == RunContentKind.PassiveTreasure && content.ShellId == id && card != null && card.Implemented && card.Type == FactType(id)
        && card.Type == CardType.SPELL && DungeonRunContent.ForShell(id) == content && clientIds.Contains(id), content.Kind + " " + card?.Type);
    Check($"T1 ids-text-match {id}: content name and text equal facts/violet-hold-2026-09.json",
        content.Name == factById[id].GetProperty("name").GetString() && content.Text == FactText(id), content.Name + " | " + content.Text);
    Check($"T1 {id}: an effect ({content.EffectKey}) or a match-start change", content.EffectKey != null || id == "DALA_738");
}
foreach (var fx in effects)
{
    var e = DungeonRunContent.Get(fx.Key);
    var card = C(fx.Value.Shell);
    bool shellOk = e.Kind == RunContentKind.Effect && e.ShellId == fx.Value.Shell && card != null && card.Implemented && card.Type == CardType.ENCHANTMENT
        && clientIds.Contains(fx.Value.Shell) && DungeonRunContent.All.Count(x => x.ShellId == fx.Value.Shell) == 1;
    if (fx.Value.Own)
        Check($"T1 {fx.Value.Shell}: content {fx.Key} on the client's own ENCHANTMENT, scripted, name and text word for word",
            shellOk && e.Name == factById[fx.Value.Shell].GetProperty("name").GetString() && e.Text == FactText(fx.Value.Shell), e.Name + " | " + e.Text);
    else
        Check($"T1 {fx.Value.Shell}: content {fx.Key} on a borrowed, otherwise unscripted client ENCHANTMENT used once",
            shellOk && e.Name != null && e.Text != null, e.Name + " | " + e.Text);
}
foreach (string id in new[] { "DALA_737", "DALA_739", "DALA_743", "DALA_745" })
{
    var t = DungeonRunContent.Get(id);
    var fx = DungeonRunContent.Get(t.EffectKey);
    Check($"T1 {id}: its effect shell {fx.ShellId} is a declared Start of Game source, named and worded as the treasure",
        StartOfGameRules.HasEffect(C(fx.ShellId)) && fx.Name == t.Name && "<b>Passive</b>\n" + fx.Text == t.Text, fx.Name + " | " + fx.Text);
}
Check("T1 no other F8 shell is a Start of Game source", effects.Values.Where(v => !sogShells.Contains(v.Shell)).All(v => !StartOfGameRules.HasEffect(C(v.Shell))));
Check("T1 DALA_736 Stargazing is GILA_513 Cult of the Wolf's rule: both effects carry the same H6 shape (a Hero Power cost aura and the Inspire refresh)",
    C("DALA_736e").Power.Aura != null && C("DALA_736e").Power.Trigger?.Type == TriggerType.INSPIRE
    && C("GILA_513e").Power.Aura != null && C("GILA_513e").Power.Trigger?.Type == TriggerType.INSPIRE);
Check("T1 tokens: Scroll of Wonder (LOOT_106t, Casts When Drawn), Cursed! (LOE_007t), Boom Bot (GVG_110t) are scripted client cards",
    new[] { DungeonRunContent.ScrollOfWonder, DungeonRunContent.RafaamCurse, DungeonRunContent.BoomBot }.All(x => C(x).Implemented && clientIds.Contains(x))
    && C(DungeonRunContent.ScrollOfWonder)[GameTag.TOPDECK] == 1 && C(DungeonRunContent.ScrollOfWonder).Power.TopdeckTask != null
    && C(DungeonRunContent.RafaamCurse).Power.Trigger != null && C(DungeonRunContent.BoomBot)[GameTag.DEATHRATTLE] == 1
    && FactText(DungeonRunContent.ScrollOfWonder).Contains("Casts When Drawn") && factById[DungeonRunContent.RafaamCurse].GetProperty("name").GetString() == "Cursed!"
    && factById[DungeonRunContent.BoomBot].GetProperty("name").GetString() == "Boom Bot" && C(DungeonRunContent.BoomBot).ATK == 1 && C(DungeonRunContent.BoomBot).Health == 1);
foreach (string p in DungeonRunContent.PrincesRingPowers)
{
    var card = C(p);
    Check($"T1 DALA_738 pool {p} ({card?.Name}): a scripted, clickable Death Knight hero power the client knows (not Choose One, not passive)",
        card != null && card.Implemented && card.Type == CardType.HERO_POWER && clientIds.Contains(p) && card[GameTag.CHOOSE_ONE] == 0
        && !HeroPower.ActivatesAtGameStart(card) && FactType(p) == CardType.HERO_POWER);
}
Check("T1 DALA_738 pool: the six Knights of the Frozen Throne Death Knight powers the engine scripts as clickable powers",
    DungeonRunContent.PrincesRingPowers.Count == 6 && DungeonRunContent.PrincesRingPowers.Distinct().Count() == 6
    && !DungeonRunContent.PrincesRingPowers.Contains("ICC_828p") && !DungeonRunContent.PrincesRingPowers.Contains("ICC_827p") && !DungeonRunContent.PrincesRingPowers.Contains("ICC_832p"));

// ---------------------------------------------------------------- T2 data, validator, admission
{
    var errors = DungeonRunValidator.Validate(data, id => clientIds.Contains(transport.TryGetValue(id, out var sent) ? sent : id));
    Check("T2 shipped data validates with 0 errors (client check through the transported id)", errors.Count == 0, string.Join(" | ", errors.Take(5)));
    var legacy = DungeonRunData.Parse(runJson, poolJson, npcJson);
    var le = DungeonRunValidator.Validate(legacy, null);
    Check("T2 the data without the catalog validates with 0 errors", le.Count == 0, string.Join(" | ", le.Take(3)));
    foreach (string id in shipped)
    {
        var t = data.FindTreasure(id);
        Check($"T2 {id}: a passive act-run treasure row, offered, Heist pool, scoped to the 36 Heist starters",
            t != null && !t.Active && t.Offered && !t.Legacy && !t.Signature && t.Runs == DungeonRunTreasure.RunsAct && t.Pool == "Heist"
            && t.Scope.Decks.OrderBy(x => x).SequenceEqual(heistDecks.OrderBy(x => x)) && t.Scope.Classes.Count == 0 && t.Scope.Powers.Count == 0 && t.UpgradesTo == null);
    }
    Check("T2 DALA_740 has no treasure row", data.FindTreasure("DALA_740") == null);
    var families = new Dictionary<string, string>
    {
        ["DALA_728"] = "run-treasure", ["DALA_731"] = "run-treasure", ["DALA_733"] = "run-treasure", ["DALA_736"] = "run-treasure",
        ["DALA_744"] = "run-treasure", ["DALA_746"] = "run-treasure", ["DALA_748"] = "run-treasure", ["DALA_735"] = "run-treasure-rule", ["DALA_747"] = "run-treasure-rule",
        ["DALA_737"] = "run-treasure-start-of-game", ["DALA_739"] = "run-treasure-start-of-game", ["DALA_743"] = "run-treasure-start-of-game",
        ["DALA_745"] = "run-treasure-start-of-game", ["DALA_741"] = "run-treasure-random-cost", ["DALA_738"] = "run-treasure-power-swap",
    };
    Check("T2 every shipped F8 id has exactly one admission family here", shipped.All(families.ContainsKey) && families.Count == 15);
    foreach (var pair in families.OrderBy(p => p.Key, StringComparer.Ordinal))
    {
        string id = pair.Key;
        var p = DungeonRunAdmission.Classify(C(id), id, AdmitScope.Player, null, catalogRun: true);
        var pl = DungeonRunAdmission.Classify(C(id), id, AdmitScope.Player, null, catalogRun: false);
        var b = DungeonRunAdmission.Classify(C(id), id, AdmitScope.Boss, null, catalogRun: true);
        Check($"T2 admission {id}: the {pair.Value} row admits the player (catalog and legacy verdicts), never the boss",
            p.Subject && p.Admitted && pl.Admitted && !b.Admitted && DungeonRunAdmission.RuleFor(id)?.Family == pair.Value
            && DungeonRunAdmission.RuleFor(id).Reason.Contains("c-treasures-F8"), p.Family + " " + p.Admit);
        Check($"T2 admission {id}: the boss's generation never yields it", DungeonRunAdmission.GeneratedRejectsBoss(true).Contains(id));
    }
    var w = DungeonRunAdmission.Classify(C("DALA_740"), "DALA_740", AdmitScope.Player, null, catalogRun: true);
    Check("T2 admission DALA_740: rejected in every scope with the stated data-gap reason and a none: lift",
        w.Subject && !w.Admitted && w.Admit == AdmitScope.None && w.Reason.Contains(DungeonRunContent.WisdomballRejection)
        && DungeonRunAdmission.IsNoneLift(w.Lift) && !DungeonRunAdmission.Classify(C("DALA_740"), "DALA_740", AdmitScope.Player, null, false).Admitted);
    foreach (string p in DungeonRunContent.PrincesRingPowers)
        Check($"T2 admission {p}: the DALA_738 pool power is admitted for the run player", DungeonRunAdmission.Classify(C(p), p, AdmitScope.Player, null, true).Admitted);
}

// ---------------------------------------------------------------- T3 offers: every Heist villain, never another run, never DALA_740
{
    var offered = new Dictionary<string, HashSet<string>>();
    foreach (var cls in data.Classes)
    {
        var set = offered[cls.Id] = new HashSet<string>();
        int seeds = villains.Contains(cls.Id) ? 200 : 30;
        for (long seed = 1; seed <= seeds; seed++)
        {
            var s = run.NewRun(all, cls.Id, cls.DefaultDeck, cls.DefaultPower, seed, signatureId: run.SignatureOffer(all, run.Data.FindClass(cls.Id), seed).FirstOrDefault());
            for (int k = 0; k < 3 && run.OpenTreasureDraft(s, passiveOnly: k == 0); k++)
            {
                set.UnionWith(s.OpenDraft.Offered);
                run.PickTreasure(s, s.OpenDraft.Offered[0]);
            }
        }
    }
    var heist = new HashSet<string>(villains.SelectMany(h => offered[h]));
    foreach (string id in shipped) Check($"T3 {id} is offered to Dalaran Heist runs", heist.Contains(id));
    foreach (string h in villains)
        Check($"T3 {h}: offered Heist passives (at least 8 of the 15)", offered[h].Count(shipped.Contains) >= 8, offered[h].Count(shipped.Contains).ToString());
    Check("T3 DALA_740 is never offered", !offered.Values.Any(v => v.Contains("DALA_740")));
    Check("T3 no other run is offered a Heist passive treasure (deck scope)",
        offered.Where(p => !villains.Contains(p.Key)).All(p => !p.Value.Overlaps(f8)),
        string.Join(";", offered.Where(p => !villains.Contains(p.Key) && p.Value.Overlaps(f8)).Select(p => p.Key)));
    var sp = NewAct(Chu, 9);
    run.OpenTreasureDraft(sp, passiveOnly: true);
    sp.OpenDraft.Offered[0] = "DALA_735";
    int before = sp.Deck.Count;
    run.PickTreasure(sp, "DALA_735");
    Check("T3 PickTreasure DALA_735 on a Mr. Chu run: a passive (Treasures), the run deck unchanged, the plan lists it",
        sp.Treasures.Contains("DALA_735") && !sp.ActiveTreasures.Contains("DALA_735") && sp.Deck.Count == before);
}

// ---------------------------------------------------------------- T4 DALA_728 Recycling
{
    var (s, plan, g) = Fight(Chu, 801, new[] { "DALA_728" });
    var p1 = g.Player1; var p2 = g.Player2;
    Check("T4 DALA_728: the plan lists it and its player enchantment is attached", plan.PlayerTreasures.Contains("DALA_728") && HasFx(p1, "DALA_728e"));
    int armor = p1.Hero.Armor;
    var m = Board(g, p1, "CS2_120"); Kill(g, m);
    Check("T4 DALA_728: a friendly minion dies -> 2 Armor", p1.Hero.Armor == armor + 2, $"{armor}->{p1.Hero.Armor}");
    var e = Board(g, p2, "CS2_120"); Kill(g, e);
    Check("T4 DALA_728: an enemy minion dies -> no Armor", p1.Hero.Armor == armor + 2 && p2.Hero.Armor == 0);
    var a = Board(g, p1, "CS2_120"); var b = Board(g, p1, "CS2_120");
    a.Damage = a.Health; b.Damage = b.Health; g.DeathProcessingAndAuraUpdate();
    Check("T4 DALA_728: two friendly minions die together -> 4 more Armor", p1.Hero.Armor == armor + 6, p1.Hero.Armor.ToString());
}

// ---------------------------------------------------------------- T5 DALA_731 Rocket Backpacks
{
    var (s, plan, g) = Fight(Barkeye, 802, new[] { "DALA_731" });
    var p1 = g.Player1; var p2 = g.Player2;
    var enemy = Board(g, p2, "CS2_120");
    var yeti = Board(g, p1, "CS2_182");
    var opts = p1.Options().OfType<MinionAttackTask>().Where(o => o.Source == yeti).ToList();
    Check("T5 DALA_731: my newly summoned minion has Rush: it may attack the enemy minion, not the hero",
        yeti.IsRush && opts.Any(o => o.Target == enemy) && !opts.Any(o => o.Target == p2.Hero), string.Join(",", opts.Select(o => o.Target?.Card.Id)));
    Check("T5 DALA_731: the enemy's minion has no Rush", !enemy.IsRush);
    Clear(p1); var croc = ToHand(g, p1, "CS2_120"); Mana(p1, 10);
    Play(g, croc);
    Check("T5 DALA_731: a minion played from hand has Rush too", ((Minion)croc).IsRush && p1.Options().OfType<MinionAttackTask>().Any(o => o.Source == croc && o.Target == enemy));
}

// ---------------------------------------------------------------- T6 DALA_733 Emerald Goggles
{
    var (s, plan, g) = Fight(Rakanishu, 803, new[] { "DALA_733" });
    var p1 = g.Player1; var p2 = g.Player2;
    Clear(p1);
    var first = ToHand(g, p1, "CS2_182"); var second = ToHand(g, p1, "CS2_029"); var third = ToHand(g, p1, "CS2_120");
    Check("T6 DALA_733: the left-most card costs (2) less, the others their printed Cost",
        first.Cost == 2 && second.Cost == 4 && third.Cost == 2 && first.ZonePosition == 0, $"{first.Cost} {second.Cost} {third.Cost}");
    Mana(p1, 10); Play(g, first);
    Check("T6 DALA_733: when it leaves, the new left-most card costs (2) less", second.ZonePosition == 0 && second.Cost == 2 && third.Cost == 2, $"{second.Cost} {third.Cost}");
    Clear(p2); var theirs = ToHand(g, p2, "CS2_182");
    Check("T6 DALA_733: the enemy's left-most card is not discounted", theirs.Cost == 4);
}

// ---------------------------------------------------------------- T7 DALA_735 Robes of Gaudiness
{
    var (s, plan, g) = Fight(Rakanishu, 804, new[] { "DALA_735" });
    var p1 = g.Player1; var p2 = g.Player2;
    Clear(p1);
    var yeti = ToHand(g, p1, "CS2_182"); var fireball = ToHand(g, p1, "CS2_029"); var croc = ToHand(g, p1, "CS2_120");
    var wisp = ToHand(g, p1, "CS2_231"); var boulder = ToHand(g, p1, "CS2_200");
    Check("T7 DALA_735: my cards cost half, rounded up (4->2, 4->2, 2->1, 0->0, 6->3)",
        yeti.Cost == 2 && fireball.Cost == 2 && croc.Cost == 1 && wisp.Cost == 0 && boulder.Cost == 3, $"{yeti.Cost} {fireball.Cost} {croc.Cost} {wisp.Cost} {boulder.Cost}");
    Clear(p2); var theirs = ToHand(g, p2, "CS2_182");
    Check("T7 DALA_735: the enemy's cards keep their Cost", theirs.Cost == 4);
    Check("T7 DALA_735: the hero power keeps its Cost", p1.Hero.HeroPower.Cost == p1.Hero.HeroPower.Card.Cost);
    Mana(p1, 10);
    Check("T7 DALA_735: first card played at half Cost", Play(g, yeti) && p1.UsedMana == 2, p1.UsedMana.ToString());
    Check("T7 DALA_735: second card played", Play(g, croc) && p1.NumCardsPlayedThisTurn == 2);
    var plays = p1.Options().OfType<PlayCardTask>().ToList();
    Check("T7 DALA_735: after two cards the options offer no third card (the bot's legal moves)", plays.Count == 0 && p1.RemainingMana >= 3,
        string.Join(",", plays.Select(o => o.Source.Card.Id)));
    Check("T7 DALA_735: a third card is unplayable (CANT_PLAY shown) and refused", !fireball.IsPlayable && fireball[GameTag.CANT_PLAY] == 1 && !Play(g, wisp, null));
    Check("T7 DALA_735: the hero power stays usable", p1.Options().OfType<HeroPowerTask>().Any());
    Check("T7 DALA_735: RunPlayerRules.PlaysLeft reads 0; the boss has no limit", RunPlayerRules.PlaysLeft(p1) == 0 && RunPlayerRules.PlaysLeft(p2) == null);
    EndTurn(g); EndTurn(g);
    Check("T7 DALA_735: next turn I may play again", fireball.IsPlayable && RunPlayerRules.PlaysLeft(p1) == 2 && fireball[GameTag.CANT_PLAY] == 0);
}

// ---------------------------------------------------------------- T8 DALA_736 Stargazing (= GILA_513's rule)
foreach (string row in new[] { Eudora, Tekahn })
{
    var (s, plan, g) = Fight(row, 805, new[] { "DALA_736" });
    var p1 = g.Player1;
    var hp = p1.Hero.HeroPower;
    Check($"T8 DALA_736 ({row}): the Hero Power costs (1) less", hp.Cost == hp.Card.Cost - 1, hp.Cost + " vs " + hp.Card.Cost);
    Mana(p1, 10);
    var target = hp.Card.RequiresTarget ? (ICharacter)g.Player2.Hero : null;
    bool one = UsePower(g, target);
    bool two = p1.Options().OfType<HeroPowerTask>().Any() && UsePower(g, target);
    Check($"T8 DALA_736 ({row}): usable twice in a turn, not a third time", one && two && !p1.Options().OfType<HeroPowerTask>().Any(),
        $"{one} {two} {p1.HeroPowerActivationsThisTurn}");
    EndTurn(g); EndTurn(g);
    Check($"T8 DALA_736 ({row}): next turn it is usable again", p1.Options().OfType<HeroPowerTask>().Any());
}

// ---------------------------------------------------------------- T9 DALA_737 Resourcefulness (Start of Game)
{
    var (s, plan, g) = PreMulligan(George, 806, "DALA_737");
    var p1 = g.Player1;
    var fx = p1.AppliedEnchantments.FirstOrDefault(e => e.Card.Id == "TB_BaconShop_1P_PlayerE");
    Check("T9 DALA_737: after ApplyAfterStart its enchantment is a pending Start of Game source; no weapon before the mulligan",
        fx != null && fx[StartOfGameRules.Pending] == 1 && p1.Hero.Weapon == null);
    g.Process(ChooseTask.Mulligan(g.Player1, new List<int>()));
    g.Process(ChooseTask.Mulligan(g.Player2, new List<int>()));
    Check("T9 DALA_737: still no weapon after both mulligans", p1.Hero.Weapon == null);
    g.MainBegin();
    var w = p1.Hero.Weapon;
    var pool = DungeonRunContent.ResourcefulnessWeapons(p1, fx);
    Check("T9 DALA_737: at the start of the game a random scripted weapon of the pool is equipped with +1/+1 (E.V.I.L. Upgrade)",
        w != null && pool.Any(c => c.Id == w.Card.Id) && w.AttackDamage == w.Card.ATK + 1 && w.Durability == w.Card[GameTag.DURABILITY] + 1
        && w.AppliedEnchantments?.Any(e => e.Card.Id == "DALA_737e") == true && fx[StartOfGameRules.Pending] == -1,
        w == null ? "none" : $"{w.Card.Id} {w.AttackDamage}/{w.Durability}");
    Check("T9 DALA_737: the pool holds only scripted weapons (many)", pool.Count > 20 && pool.All(c => c.Implemented && c.Type == CardType.WEAPON), pool.Count.ToString());
    var (s2, plan2, g2) = PreMulligan(George, 806, "DALA_737"); Finish(g2);
    Check("T9 DALA_737: the same run seed equips the same weapon (game RNG)", g2.Player1.Hero.Weapon?.Card.Id == w?.Card.Id);
    var seen = new HashSet<string>();
    for (long seed = 810; seed < 822; seed++) { var (_, _, gx) = PreMulligan(George, seed, "DALA_737"); Finish(gx); seen.Add(gx.Player1.Hero.Weapon?.Card.Id ?? "none"); }
    Check("T9 DALA_737: other seeds equip other weapons", seen.Count >= 4 && !seen.Contains("none"), string.Join(",", seen));
}

// ---------------------------------------------------------------- T10 DALA_738 A Prince's Ring
{
    var (s, plan, g) = PreMulligan(Kriziki, 807, "DALA_738");
    var p1 = g.Player1;
    string swapped = p1.Hero.HeroPower.Card.Id;
    Check($"T10 DALA_738: before the mulligan the run power ({plan.PlayerHeroPowerId}) became a random Death Knight power",
        plan.PlayerHeroPowerId != null && !DungeonRunContent.PrincesRingPowers.Contains(plan.PlayerHeroPowerId)
        && DungeonRunContent.PrincesRingPowers.Contains(swapped) && p1.Hero.HeroPower.Controller == p1 && p1.SetasideZone.Any(e => e.Card.Id == plan.PlayerHeroPowerId), swapped);
    Finish(g);
    Mana(p1, 10);
    var hpTask = p1.Options().OfType<HeroPowerTask>().FirstOrDefault();
    Check("T10 DALA_738: the swapped power is offered and used", hpTask != null && g.Process(hpTask), hpTask?.ToString() ?? "no option");
    var seen = new HashSet<string>();
    for (long seed = 830; seed < 860; seed++) { var (_, _, gx) = PreMulligan(Kriziki, seed, "DALA_738"); seen.Add(gx.Player1.Hero.HeroPower.Card.Id); }
    Check("T10 DALA_738: across seeds every power of the pool appears, nothing else", seen.SetEquals(DungeonRunContent.PrincesRingPowers), string.Join(",", seen));
    var (_, _, g2) = PreMulligan(Kriziki, 807, "DALA_738");
    Check("T10 DALA_738: the same run seed swaps in the same power", g2.Player1.Hero.HeroPower.Card.Id == swapped);
    var (_, _, g3) = PreMulligan(Kriziki, 807, "DALA_738", "DALA_736"); Finish(g3);
    Check("T10 DALA_738 with DALA_736: the swapped power costs (1) less", g3.Player1.Hero.HeroPower.Cost == g3.Player1.Hero.HeroPower.Card.Cost - 1);
}

// ---------------------------------------------------------------- T11 DALA_739 Book of Wonders (Start of Game)
{
    var (s, plan, g) = PreMulligan(Rakanishu, 808, "DALA_739");
    var p1 = g.Player1;
    int deck = p1.DeckZone.Count;
    Check("T11 DALA_739: no Scroll of Wonder before the mulligan (none in the opening hand or deck)",
        !p1.DeckZone.Any(e => e.Card.Id == DungeonRunContent.ScrollOfWonder) && !p1.HandZone.Any(e => e.Card.Id == DungeonRunContent.ScrollOfWonder));
    Finish(g);
    // The turn's first draw may already find a Scroll (it casts itself and its owner draws on): count every Scroll entity.
    var allScrolls = g.IdEntityDic.Values.Where(e => e.Card.Id == DungeonRunContent.ScrollOfWonder).ToList();
    int scrolls = p1.DeckZone.Count(e => e.Card.Id == DungeonRunContent.ScrollOfWonder);
    Check("T11 DALA_739: at the start of the game 10 Scrolls of Wonder are shuffled into my deck (any drawn since cast themselves)",
        allScrolls.Count == 10 && allScrolls.All(e => e.Controller == p1) && allScrolls.Count(e => e.Zone == p1.DeckZone) == scrolls && scrolls >= 1
        // Not asserted: how many other cards left the deck. A drawn Scroll casts a random spell, which may itself draw (phase 8
        // family 01: Pressure Plate joined the run player's random-spell pool; with it the seeded casts take one more card from the deck).
        && allScrolls.All(e => e.Zone != p1.HandZone) && p1.DeckZone.Count - scrolls <= deck,
        $"{scrolls} in deck of {allScrolls.Count}; deck {deck}->{p1.DeckZone.Count}");
    Check("T11 DALA_739: the boss's deck has none", !g.Player2.DeckZone.Any(e => e.Card.Id == DungeonRunContent.ScrollOfWonder));
    var scroll = p1.DeckZone.First(e => e.Card.Id == DungeonRunContent.ScrollOfWonder);
    int hand = p1.HandZone.Count;
    Generic.Draw(p1, scroll); g.DeathProcessingAndAuraUpdate();
    Check("T11 DALA_739: a drawn Scroll casts itself (it does not stay in hand) and its owner draws the next card",
        scroll.Zone?.Type != Zone.HAND && p1.HandZone.Count == hand + 1, $"{scroll.Zone?.Type} {hand}->{p1.HandZone.Count}");
}

// ---------------------------------------------------------------- T12 DALA_741 Togwaggle's Dice
{
    var (s, plan, g) = Fight(Vessina, 809, new[] { "DALA_741" });
    var p1 = g.Player1; var p2 = g.Player2;
    Clear(p1);
    var cards = new[] { "CS2_182", "CS2_029", "CS2_120", "CS2_231", "CS2_200" }.Select(id => ToHand(g, p1, id)).ToList();
    Clear(p2); var theirs = ToHand(g, p2, "CS2_182");
    EndTurn(g);
    var costs = cards.Select(c => c.Cost).ToList();
    Check("T12 DALA_741: at the end of my turn every card in my hand gets a Cost from 0 to 10 (Roll the Dice)",
        cards.All(c => c.AppliedEnchantments?.Count(e => e.Card.Id == "DALA_741e2") == 1 && c.Cost >= 0 && c.Cost <= 10), string.Join(",", costs));
    Check("T12 DALA_741: the costs are not all printed (the roll happened)", !cards.Select(c => c.Cost).SequenceEqual(cards.Select(c => c.Card.Cost)));
    Check("T12 DALA_741: the boss's hand is untouched", theirs.Cost == 4 && theirs.AppliedEnchantments?.Any(e => e.Card.Id == "DALA_741e2") != true);
    while (g.CurrentPlayer == p2 && g.State != State.COMPLETE) EndTurn(g);
    Check("T12 DALA_741: the boss's turn end rolls nothing", cards.Select(c => c.Cost).SequenceEqual(costs) && cards.All(c => c.AppliedEnchantments.Count(e => e.Card.Id == "DALA_741e2") == 1));
    var (s2, plan2, g2) = Fight(Vessina, 809, new[] { "DALA_741" });
    Clear(g2.Player1);
    var again = new[] { "CS2_182", "CS2_029", "CS2_120", "CS2_231", "CS2_200" }.Select(id => ToHand(g2, g2.Player1, id)).ToList();
    Clear(g2.Player2); ToHand(g2, g2.Player2, "CS2_182");
    EndTurn(g2);
    Check("T12 DALA_741: the same seed rolls the same costs (game RNG)", again.Select(c => c.Cost).SequenceEqual(costs), string.Join(",", again.Select(c => c.Cost)));
    Mana(p1, 10);
    var cheap = cards.OrderBy(c => c.Cost).First();
    Check("T12 DALA_741: a card is played at its rolled Cost", Play(g, cheap, cheap.Card.Id == "CS2_029" ? p2.Hero : null) && p1.UsedMana == costs[cards.IndexOf(cheap)],
        $"{cheap.Card.Id} used {p1.UsedMana}");
}

// ---------------------------------------------------------------- T13 DALA_743 Dr. Boom's Remote (Start of Game)
{
    var (s, plan, g) = PreMulligan(Chu, 810, "DALA_743");
    var p1 = g.Player1;
    Check("T13 DALA_743: no Boom Bot before the mulligan", p1.BoardZone.Count == 0);
    Finish(g);
    var bots = p1.BoardZone.Where(m => m.Card.Id == DungeonRunContent.BoomBot).ToList();
    Check("T13 DALA_743: at the start of the game three 1/1 Boom Bots are summoned on my side", bots.Count == 3 && bots.All(b => b.AttackDamage == 1 && b.Health == 1)
        && g.Player2.BoardZone.Count == 0, bots.Count.ToString());
    int enemy = g.Player2.Hero.Health + g.Player2.Hero.Armor + g.Player2.BoardZone.Sum(m => m.Health);
    Mana(p1, 10); var bolt = ToHand(g, p1, "CS2_029");
    Play(g, bolt, bots[0]);
    int dealt = enemy - (g.Player2.Hero.Health + g.Player2.Hero.Armor + g.Player2.BoardZone.Sum(m => m.Health));
    Check("T13 DALA_743: a Boom Bot's Deathrattle deals 1-4 damage to a random enemy", dealt is >= 1 and <= 4, $"dealt {dealt}; bot zone {bots[0].Zone?.Type}");
}

// ---------------------------------------------------------------- T14 DALA_744 Hagatha's Embrace
{
    var (s, plan, g) = Fight(Squeamlish, 811, new[] { "DALA_744" });
    var p1 = g.Player1;
    Clear(p1);
    var croc = ToHand(g, p1, "CS2_120"); var fireball = ToHand(g, p1, "CS2_029");
    EndTurn(g);
    Check("T14 DALA_744: the boss's turn start buffs nothing of mine", ((Minion)croc).AttackDamage == 2 && ((Minion)croc).Health == 3);
    EndTurn(g);
    var drawn = p1.HandZone.Where(e => e != croc && e != fireball).ToList();
    var buffed = p1.HandZone.Where(e => e.AppliedEnchantments?.Any(x => x.Card.Id == "DALA_744e2") == true).ToList();
    Check("T14 DALA_744: at the start of my turn exactly one minion in my hand gets +1/+1 (Hagatha's Embrace), never a spell",
        buffed.Count == 1 && buffed[0] is Minion bm && bm.AttackDamage == bm.Card.ATK + 1 && bm.Health == bm.Card.Health + 1 && !buffed.Contains(fireball),
        string.Join(",", buffed.Select(b => b.Card.Id)));
    Clear(p1); var spell = ToHand(g, p1, "CS2_029");
    EndTurn(g); EndTurn(g);
    Check("T14 DALA_744: with no minion in hand nothing happens", p1.HandZone.All(e => e is Minion || e.AppliedEnchantments?.Any(x => x.Card.Id == "DALA_744e2") != true));
}

// ---------------------------------------------------------------- T15 DALA_745 The Hand of Rafaam (Start of Game)
{
    var (s, plan, g) = PreMulligan(Tekahn, 812, "DALA_745");
    var p1 = g.Player1; var p2 = g.Player2;
    Check("T15 DALA_745: no Cursed! before the mulligan", !p2.HandZone.Any(e => e.Card.Id == DungeonRunContent.RafaamCurse));
    Finish(g);
    var curses = p2.HandZone.Where(e => e.Card.Id == DungeonRunContent.RafaamCurse).ToList();
    Check("T15 DALA_745: at the start of the game the boss is given two Cursed! cards; I get none",
        curses.Count == 2 && !p1.HandZone.Any(e => e.Card.Id == DungeonRunContent.RafaamCurse), curses.Count.ToString());
    int hp = p2.Hero.Health;
    EndTurn(g);
    Check("T15 DALA_745: at the start of the boss's turn each Cursed! in its hand deals 2 damage to it", p2.Hero.Health == hp - 4, $"{hp}->{p2.Hero.Health}");
    Mana(p2, 10);
    var curse = p2.HandZone.First(e => e.Card.Id == DungeonRunContent.RafaamCurse);
    Check("T15 DALA_745: the boss may play a Cursed! (2 mana) to stop it", p2.Options().OfType<PlayCardTask>().Any(o => o.Source == curse)
        && g.Process(PlayCardTask.Any(p2, curse)) && p2.HandZone.Count(e => e.Card.Id == DungeonRunContent.RafaamCurse) == 1);
}

// ---------------------------------------------------------------- T16 DALA_746 Elixir of Vigor
{
    var (s, plan, g) = Fight(George, 813, new[] { "DALA_746" });
    var p1 = g.Player1;
    Clear(p1); Mana(p1, 10);
    int deck = p1.DeckZone.Count;
    var yeti = ToHand(g, p1, "CS2_182");
    Play(g, yeti);
    var copies = p1.DeckZone.Where(e => e.Card.Id == "CS2_182").ToList();
    Check("T16 DALA_746: after I play a minion, two copies are shuffled into my deck, each costing (1)",
        p1.DeckZone.Count == deck + 2 && copies.Count >= 2 && copies.Count(e => e.Cost == 1 && e.AppliedEnchantments?.Any(x => x.Card.Id == DungeonRunContent.VigorCostShell) == true) == 2,
        $"{deck}->{p1.DeckZone.Count} copies {copies.Count}");
    var copy = copies.First(e => e.Cost == 1);
    Generic.Draw(p1, copy); g.DeathProcessingAndAuraUpdate();
    Check("T16 DALA_746: a drawn copy still costs (1)", copy.Zone == p1.HandZone && copy.Cost == 1, copy.Cost.ToString());
    Clear(p1); var spell = ToHand(g, p1, "CS2_024");
    int d2 = p1.DeckZone.Count;
    Play(g, spell, g.Player2.Hero);
    Check("T16 DALA_746: a spell shuffles nothing", p1.DeckZone.Count == d2);
    Board(g, p1, "CS2_120");
    Check("T16 DALA_746: a summoned (not played) minion shuffles nothing", p1.DeckZone.Count == d2);
}

// ---------------------------------------------------------------- T17 DALA_747 Elixir of Vim
{
    var (s, plan, g) = Fight(Kriziki, 814, new[] { "DALA_747" });
    var p1 = g.Player1; var p2 = g.Player2;
    Clear(p1);
    EndTurn(g);
    Check("T17 DALA_747: the boss's turn start draws me nothing", p1.HandZone.Count == 0);
    EndTurn(g);
    Check("T17 DALA_747: at the start of my turn I draw 3 cards (1 + 2)", p1.HandZone.Count == 3, p1.HandZone.Count.ToString());
    Thin(g, p1, e => false); Thin(g, p2, e => false);
    int hp = p1.Hero.Health, bossHp = p2.Hero.Health;
    EndTurn(g);
    Check("T17 DALA_747 control: the boss with an empty deck takes Fatigue", p2.Hero.Health < bossHp && p2.Hero.Fatigue == 1, $"{bossHp}->{p2.Hero.Health}");
    EndTurn(g);
    Check("T17 DALA_747: with an empty deck I take no Fatigue (three empty draws)", p1.Hero.Health == hp && p1.Hero.Fatigue == 0, $"{hp}->{p1.Hero.Health} f{p1.Hero.Fatigue}");
    Check("T17 DALA_747: RunPlayerRules reads me immune, the boss not", RunPlayerRules.IsFatigueImmune(p1) && !RunPlayerRules.IsFatigueImmune(p2));
}

// ---------------------------------------------------------------- T18 DALA_748 Elixir of Vile
{
    var (s, plan, g) = Fight(Rakanishu, 815, new[] { "DALA_748" });
    var p1 = g.Player1; var p2 = g.Player2;
    Clear(p1); Mana(p1, 0);
    var fireball = ToHand(g, p1, "CS2_029"); var croc = ToHand(g, p1, "CS2_120");
    Check("T18 DALA_748: my spells cost Health (the engine's cost-in-Health rule), my minions Mana",
        SabberStoneCore.Actions.CostRules.PaymentKind(fireball) == CostPaymentKind.Health && SabberStoneCore.Actions.CostRules.PaymentKind(croc) == CostPaymentKind.Mana);
    var opts = p1.Options().OfType<PlayCardTask>().ToList();
    Check("T18 DALA_748: with 0 mana the options offer the spell, not the minion", opts.Any(o => o.Source == fireball) && !opts.Any(o => o.Source == croc));
    int hp = p1.Hero.Health, bossHp = p2.Hero.Health;
    Check("T18 DALA_748: Fireball cast for 4 Health, 0 Mana", Play(g, fireball, p2.Hero) && p1.Hero.Health == hp - 4 && p1.UsedMana == 0 && p2.Hero.Health == bossHp - 6,
        $"{hp}->{p1.Hero.Health} boss {bossHp}->{p2.Hero.Health}");
    Clear(p2); Mana(p2, 0); var theirs = ToHand(g, p2, "CS2_029");
    Check("T18 DALA_748: the boss's spells still cost Mana", SabberStoneCore.Actions.CostRules.PaymentKind(theirs) == CostPaymentKind.Mana);
    p1.Hero.Damage = p1.Hero.BaseHealth - 3;
    var big = ToHand(g, p1, "CS2_029");
    Check("T18 DALA_748: a spell whose Health cost would kill me is not offered", !p1.Options().OfType<PlayCardTask>().Any(o => o.Source == big), p1.Hero.Health.ToString());
}

// ---------------------------------------------------------------- T19 DALA_740 Wondrous Wisdomball (rejected)
{
    Check("T19 DALA_740: no engine script (unscripted client card), no content, no treasure row, never offered",
        !C("DALA_740").Implemented && !DungeonRunContent.TryGet("DALA_740", out _) && data.FindTreasure("DALA_740") == null);
    Check("T19 DALA_740: the client carries the hidden helper cards the rejection names",
        new[] { "DALA_740ts", "DALA_740ts2", "DALA_740ts3", "DALA_740ts4", "DALA_740ts5", "DALA_740e2", "DALA_740e3" }.All(factById.ContainsKey));
}

// ---------------------------------------------------------------- T20 two passives together, the run plan path
{
    var (s, plan, g) = Fight(Chu, 816, new[] { "DALA_735", "DALA_747", "DALA_731" });
    var p1 = g.Player1;
    Check("T20 three F8 passives in one fight: all three enchantments attached, in pick order",
        plan.PlayerTreasures.SequenceEqual(new[] { "DALA_735", "DALA_747", "DALA_731" }) && HasFx(p1, "DALA_735e") && HasFx(p1, "DALA_747e") && HasFx(p1, "DALA_731e"));
    var (s2, plan2, g2) = PreMulligan(Chu, 817, "DALA_737", "DALA_743", "DALA_745", "DALA_739"); Finish(g2);
    Check("T20 the four Start of Game passives together all resolve once",
        // Counted on the board and in the graveyard: a Scroll of Wonder drawn in the opening turn may cast a board clear (phase 6
        // F10 scripted two more weapons, so DALA_737's draw moves the seed's later random casts: here Dragonfire Potion).
        // Phase 8 wave 1 (families 02-04 scripted 92 more collectibles, so the random casts moved again): the Scroll now casts
        // Doomerang, which throws DALA_737's weapon at a Boom Bot and returns it to the hand. So the weapon is counted by its
        // upgrade (DALA_737e attached exactly once: one weapon was equipped and upgraded), wherever it is now.
        g2.IdEntityDic.Values.Count(e => e.Card.Id == "DALA_737e") == 1 && g2.Player1.BoardZone.Concat(g2.Player1.GraveyardZone).Count(m => m.Card.Id == DungeonRunContent.BoomBot) == 3
        && g2.Player2.HandZone.Count(e => e.Card.Id == DungeonRunContent.RafaamCurse) == 2 && g2.IdEntityDic.Values.Count(e => e.Card.Id == DungeonRunContent.ScrollOfWonder) == 10
        && g2.Player1.AppliedEnchantments.Where(e => sogShells.Contains(e.Card.Id)).All(e => e[StartOfGameRules.Pending] == -1),
        $"weapon {g2.Player1.Hero.Weapon?.Card.Id ?? "none"} (upgrades DALA_737e ever attached: {g2.IdEntityDic.Values.Count(e => e.Card.Id == "DALA_737e")}; weapons in my hand {string.Join(",", g2.Player1.HandZone.Where(e => e.Card.Type == CardType.WEAPON).Select(e => e.Card.Id))}); Boom Bots board {g2.Player1.BoardZone.Count(m => m.Card.Id == DungeonRunContent.BoomBot)} graveyard {g2.Player1.GraveyardZone.Count(m => m.Card.Id == DungeonRunContent.BoomBot)}"
        + $"; curses in the boss's hand {g2.Player2.HandZone.Count(e => e.Card.Id == DungeonRunContent.RafaamCurse)} of {g2.IdEntityDic.Values.Count(e => e.Card.Id == DungeonRunContent.RafaamCurse)}; Scrolls {g2.IdEntityDic.Values.Count(e => e.Card.Id == DungeonRunContent.ScrollOfWonder)}"
        + $"; pending {string.Join(",", g2.Player1.AppliedEnchantments.Where(e => sogShells.Contains(e.Card.Id)).Select(e => e.Card.Id + "=" + e[StartOfGameRules.Pending]))}"
        + $"; my board {string.Join(",", g2.Player1.BoardZone.Select(m => m.Card.Id))}; my graveyard {string.Join(",", g2.Player1.GraveyardZone.Select(m => m.Card.Id))}; boss board {string.Join(",", g2.Player2.BoardZone.Select(m => m.Card.Id))}; boss hand {g2.Player2.HandZone.Count}");
}

// ---------------------------------------------------------------- full games against the real boss AI
var testBudget = new Budget(6000, 2000, 300, 6, 3, 100, 8, turnSearchSeconds: 6, turnWallSeconds: 8);
string AiGame(string id, long seed, string row)
{
    var s = NewAct(row, seed);
    Hold(s, id);
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
    int decisionIndex = 0, actions = 0, decisions = 0, fallbacks = 0, maxPlays = 0, playsThisTurn = 0, playTurn = -1, healthPaid = 0, bossCurses = 0;
    var recent = new List<string>();
    void Act(PlayerTask task, string who)
    {
        recent.Add(who + ":" + task + "@" + game.Turn); if (recent.Count > 12) recent.RemoveAt(0);
        if (++actions > 1500) throw new Exception("stuck after 1500 actions: " + string.Join(" | ", recent));
        if (who == "player" && task is PlayCardTask pc)
        {
            if (playTurn != game.Turn) { playTurn = game.Turn; playsThisTurn = 0; }
            playsThisTurn++; maxPlays = Math.Max(maxPlays, playsThisTurn);
            if (SabberStoneCore.Actions.CostRules.PaymentKind(pc.Source) == CostPaymentKind.Health && pc.Source.Cost > 0) healthPaid++;
        }
        if (who == "boss" && task is PlayCardTask bc && bc.Source.Card.Id == DungeonRunContent.RafaamCurse) bossCurses++;
        if (task is ChooseTask pick && game.Step != Step.BEGIN_MULLIGAN)
        {
            var decision = game.GetPendingDecisions().Single(d => d.ControllerId == task.Controller.Id);
            if (!game.TryAnswer(new SabberStoneCore.Model.Decisions.DecisionAnswer(decision.Id, decision.ControllerId, pick.Choices.ToArray()), out var why))
                throw new Exception($"engine rejected {who} choice ({why})");
        }
        else if (!game.Process(task)) throw new Exception($"engine rejected {who} task {task}");
    }
    while (game.State != State.COMPLETE && game.Turn <= 60)
    {
        if (game.CurrentPlayer == game.Player2)
        {
            var (task, trace) = BotDriver.Decide(game, game.Player2.PlayerId, pilot.Deck.Style,
                st => golden.DecidePilot(st, new GoldenContext(game.Turn, decisionIndex, unchecked(997 * game.Turn + decisionIndex * 31), profile)),
                ref decisionIndex, golden.Options);
            decisions++;
            if (trace?.Kind == "fallback") fallbacks++;
            Act(task, "boss");
        }
        else Act(LocalBot.Choose(game.Player1.Options()), "player");
    }
    string result = game.State != State.COMPLETE ? "turn-cap" : game.Player2.PlayState == PlayState.WON ? "boss" : game.Player1.PlayState == PlayState.WON ? "player" : "tie";
    return $"{result} turns {game.Turn} decisions {decisions} fallbacks {fallbacks} max-plays {maxPlays} health-paid {healthPaid} boss-curses {bossCurses} "
        + $"power {game.Player1.Hero.HeroPower.Card.Id} fatigue {game.Player1.Hero.Fatigue} boss {boss.Id}";
}
string RowFor(string id) => id switch
{
    "DALA_728" or "DALA_737" => Chu,
    "DALA_731" or "DALA_743" => Barkeye,
    "DALA_733" or "DALA_735" or "DALA_739" or "DALA_748" => Rakanishu,
    "DALA_736" or "DALA_745" => Tekahn,
    "DALA_738" or "DALA_747" => Kriziki,
    "DALA_741" or "DALA_746" => Vessina,
    _ => Squeamlish,
};
string aiOnly = Environment.GetEnvironmentVariable("F8_AI");
foreach (string id in shipped)
{
    if (aiOnly != null && id != aiOnly) continue;
    string row = RowFor(id);
    string line;
    try { line = AiGame(id, 901, row); }
    catch (Exception e) { line = "FAULT " + e.GetType().Name + ": " + e.Message.Split('\n')[0]; }
    Console.WriteLine($"T21 {id,-10} {row,-16} {line}");
    Check($"T21 {id}: a full game against the boss AI with the treasure ends without a fault or an AI fallback",
        !line.StartsWith("FAULT") && line.Contains("fallbacks 0 "), line);
    if (id == "DALA_735") Check("T21 DALA_735: the player never played more than two cards in a turn", line.Contains("max-plays 1 ") || line.Contains("max-plays 2 "), line);
    if (id == "DALA_738") Check("T21 DALA_738: the game was played with a Death Knight power", DungeonRunContent.PrincesRingPowers.Any(p => line.Contains("power " + p + " ")), line);
}
// The boss AI with Cursed! in its hand, over more seeds (the one F8 card that reaches the boss).
foreach (long seed in new long[] { 911, 912, 913 })
{
    if (aiOnly != null && aiOnly != "DALA_745") continue;
    string line;
    try { line = AiGame("DALA_745", seed, Tekahn); }
    catch (Exception e) { line = "FAULT " + e.GetType().Name + ": " + e.Message.Split('\n')[0]; }
    Console.WriteLine($"T22 DALA_745/{seed} {line}");
    Check($"T22 DALA_745 ({seed}): the boss holds two Cursed! from the start; the game ends without a fault or fallback", !line.StartsWith("FAULT") && line.Contains("fallbacks 0 "), line);
}

Console.WriteLine($"{passes} passed, {failures} failed");
return failures == 0 ? 0 : 1;
