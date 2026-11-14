// c-treasures-F7: phase 6 family F7 of the unified Dungeon Run, the Dalaran Heist active treasures (DALA_700-706, 708, 709,
// 711-713, 714 and its Twinspell stages 714a-c, 715-727, 729, 730; DALA_707 is family F15). For every id: the engine content
// (scripted, keyed and shelled by its own client id, name and text as facts/violet-hold-2026-09.json has them), the run data
// (run-v1.json rows scoped to the 35 Heist starters, pool Heist, the Twinspell stages never offered), the validator and
// admission checks, a scripted regression game through the run setup path (DungeonRun.NewRun on a Heist villain's hero row ->
// BuildPlan -> DungeonRunSetup.Configure -> Game -> ApplyAfterStart -> mulligan -> MainBegin -> the card drawn from the run
// deck and played), and a full game against the real boss AI (DungeonRunBossPilot + GoldenPilot) in which the player plays it;
// it must end without a fault or an AI fallback. DALA_718 and DALA_719 also check their run logs across two fights
// (DungeonRun.ApplyMatchResult), DALA_721 the run deck replace (applied on a win only, all or none). Headless: compiles
// C:\SoloStone\plugin\DungeonRun\*.cs and the NPC AI against p6f7/engine-out. Run it twice and compare the output.
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
string[] f7 = DungeonRunContent.HeistActiveIds.ToArray();
string[] stages = { "DALA_714a", "DALA_714b", "DALA_714c" };
string[] offeredF7 = f7.Where(id => !stages.Contains(id)).ToArray();
var heistDecks = villains.SelectMany(v => data.Class(v).Decks).ToList();
var effectShells = new Dictionary<string, string>
{
    ["fx_DALA_701e"] = "DALA_701e", ["fx_DALA_702e"] = "DALA_702e", ["fx_DALA_715e"] = "DALA_715e", ["fx_DALA_723e"] = "DALA_723e",
    ["fx_DALA_724e"] = "DALA_724e", ["fx_DALA_725e"] = "DALA_725e", ["fx_DALA_727e"] = "DALA_727e",
};
int Slot(IPlayable e) => RunHelpers.SlotOf(e);
// The run deck card of the record <paramref name="id"/> (by its dealt slot, so a card that changed in hand is still found), drawn.
IPlayable DrawRun(Game g, DungeonRunState s, string id)
{
    int slot = s.Cards.FindIndex(c => c.Id == id);
    var e = (IPlayable)g.IdEntityDic[g.Player1DeckSlotIds[slot]];
    if (e.Zone == g.Player1.DeckZone) { Generic.Draw(g.Player1, e); g.DeathProcessingAndAuraUpdate(); }
    return e;
}
List<string> Logs(Game g, int slot) => g.RunResult?.SlotLogAppends.Where(p => p.Key == slot).Select(p => p.Value).ToList() ?? new List<string>();

// ---------------------------------------------------------------- T1 content
Check("T1 31 F7 ids", f7.Length == 31 && f7.Distinct().Count() == 31 && offeredF7.Length == 28);
foreach (string id in f7.Concat(new[] { "DALA_716t" }))
{
    var content = DungeonRunContent.Get(id);
    var card = C(id);
    Check($"T1 {id}: an ActiveTreasure keyed and shelled by its own id, scripted, the client's {FactType(id)}",
        content.Kind == RunContentKind.ActiveTreasure && content.ShellId == id && card != null && card.Implemented && card.Type == FactType(id)
        && DungeonRunContent.ForShell(id) == content && clientIds.Contains(id), card == null ? "no card" : content.Kind + " " + card.Type + " " + card.Implemented);
    Check($"T1 ids-text-match {id}: content name and text equal facts/violet-hold-2026-09.json",
        content.Name == factById[id].GetProperty("name").GetString() && content.Text == FactText(id), content.Name + " | " + content.Text);
    Check($"T1 {id}: cost {FactTag(id, 48)} as the client prints it" + (card.Type == CardType.MINION ? $", stats {FactTag(id, 47)}/{FactTag(id, 45)}" : "")
        + (card.Type == CardType.WEAPON ? $", weapon {FactTag(id, 47)}/{FactTag(id, 187)}" : ""),
        card.Cost == FactTag(id, 48) && content.Cost == FactTag(id, 48)
        && (card.Type != CardType.MINION || card.ATK == FactTag(id, 47) && card.Health == FactTag(id, 45))
        && (card.Type != CardType.WEAPON || card.ATK == FactTag(id, 47) && card[GameTag.DURABILITY] == FactTag(id, 187)),
        $"{card.Cost} {card.ATK}/{card.Health} dur {card[GameTag.DURABILITY]}");
}
foreach (var fx in effectShells)
{
    var e = DungeonRunContent.Get(fx.Key);
    Check($"T1 {fx.Value}: content {fx.Key} on the client's own ENCHANTMENT, scripted, name and text word for word",
        e.Kind == RunContentKind.Effect && e.ShellId == fx.Value && C(fx.Value).Implemented && C(fx.Value).Type == CardType.ENCHANTMENT && clientIds.Contains(fx.Value)
        && e.Name == factById[fx.Value].GetProperty("name").GetString() && e.Text == FactText(fx.Value), e.Name + " | " + e.Text);
}
Check("T1 keywords: Golden Candle Taunt+Battlecry, Elder Taggawag Battlecry+Deathrattle, Dagwik Stealth, Hyperblaster Poisonous, the Scythe Deathrattle, Murgatha a Murloc",
    C("DALA_709")[GameTag.TAUNT] == 1 && C("DALA_709")[GameTag.BATTLECRY] == 1 && C("DALA_713")[GameTag.BATTLECRY] == 1 && C("DALA_713")[GameTag.DEATHRATTLE] == 1
    && C("DALA_729")[GameTag.STEALTH] == 1 && C("DALA_723")[GameTag.POISONOUS] == 1 && C("DALA_719")[GameTag.DEATHRATTLE] == 1 && C("DALA_730")[GameTag.CARDRACE] == (int)Race.MURLOC);
Check("T1 THE... Candles? stages: each Twinspell stage's client TWINSPELL_COPY is the next stage; the last has none",
    DungeonRunContent.CandleStages.All(p => C(p.Key)[GameTag.TWINSPELL] == 1 && Cards.FromAssetId(C(p.Key)[GameTag.TWINSPELL_COPY])?.Id == p.Value)
    && C("DALA_714c")[GameTag.TWINSPELL] == 0 && DungeonRunContent.CandleStages.Count == 3);
Check("T1 Master Scheme starts at @ = 1 (TAG_SCRIPT_DATA_NUM_1)", C("DALA_726")[GameTag.TAG_SCRIPT_DATA_NUM_1] == 1);
Check("T1 tokens: Fly-By lists the Kadoom Bot, the Sphere King Mukla, the Horn Annoy-o-Tron, the Scheme the Boom Bot, the Candles their next stage",
    DungeonRunContent.Get("DALA_716").Tokens.Contains("DALA_716t") && DungeonRunContent.Get("DALA_704").Tokens.Contains("EX1_014")
    && DungeonRunContent.Get("DALA_722").Tokens.Contains("GVG_085") && DungeonRunContent.Get("DALA_726").Tokens.Contains("GVG_110t")
    && DungeonRunContent.Get("DALA_714").Tokens.Contains("DALA_714a") && DungeonRunContent.Get("DALA_714b").Tokens.Contains("DALA_714c"));
Check("T1 Case Study's Quest Rewards are the 9 Un'Goro rewards, every one scripted",
    DungeonRunContent.QuestRewards.Count == 9 && DungeonRunContent.QuestRewards.All(id => C(id) != null && C(id).Implemented)
    && Cards.All.Where(c => c.Id.StartsWith("UNG_") && c[GameTag.QUEST] == 1 && c.Collectible).Select(c => Cards.FromAssetId(c[GameTag.QUEST_REWARD_DATABASE_ID]).Id)
        .OrderBy(x => x).SequenceEqual(DungeonRunContent.QuestRewards.OrderBy(x => x)));

// ---------------------------------------------------------------- T2 data, validator, admission
{
    var errors = DungeonRunValidator.Validate(data, id => clientIds.Contains(transport.TryGetValue(id, out var sent) ? sent : id));
    Check("T2 shipped data validates with 0 errors (client check through the transported id)", errors.Count == 0, string.Join(" | ", errors.Take(5)));
    var legacy = DungeonRunData.Parse(runJson, poolJson, npcJson);
    var le = DungeonRunValidator.Validate(legacy, null);
    Check("T2 the data without the catalog validates with 0 errors", le.Count == 0, string.Join(" | ", le.Take(3)));
    foreach (string id in f7)
    {
        var t = data.FindTreasure(id);
        bool stage = stages.Contains(id);
        Check($"T2 {id}: an active act-run treasure row, {(stage ? "never offered (an in-match Twinspell stage)" : "offered")}, Heist pool, scoped to the 36 Heist starters",
            t != null && t.Active && t.Offered == !stage && !t.Legacy && !t.Signature && t.Runs == DungeonRunTreasure.RunsAct && t.Pool == "Heist"
            && t.Scope.Decks.OrderBy(x => x).SequenceEqual(heistDecks.OrderBy(x => x)) && t.Scope.Classes.Count == 0 && t.Scope.Powers.Count == 0 && t.UpgradesTo == null);
    }
    // Phase 6 F10 ships DALA_Hunter_Deck3 (heist_hunter_3): 36 Heist starters.
    Check("T2 the 36 Heist starters are the nine villains' decks", heistDecks.Count == 36 && heistDecks.Distinct().Count() == 36);
    var families = new Dictionary<string, string>();
    foreach (string id in new[] { "DALA_700", "DALA_702", "DALA_703", "DALA_704", "DALA_706", "DALA_711", "DALA_712", "DALA_713", "DALA_714", "DALA_714a",
        "DALA_714b", "DALA_714c", "DALA_715", "DALA_716", "DALA_717", "DALA_720", "DALA_722", "DALA_723", "DALA_724", "DALA_725", "DALA_726", "DALA_729", "DALA_730" })
        families[id] = "run-treasure";
    families["DALA_705"] = "run-treasure-copy"; families["DALA_701"] = "run-treasure-discover"; families["DALA_708"] = "run-treasure-discover";
    families["DALA_709"] = "run-treasure-belief-reset"; families["DALA_718"] = "run-treasure-run-log"; families["DALA_719"] = "run-treasure-run-log";
    families["DALA_721"] = "run-treasure-deck-edit"; families["DALA_727"] = "deck-wide-change";
    Check("T2 every F7 id has exactly one admission family here", f7.All(families.ContainsKey) && families.Count == 31);
    foreach (var pair in families.OrderBy(p => p.Key, StringComparer.Ordinal))
    {
        string id = pair.Key;
        var p = DungeonRunAdmission.Classify(C(id), id, AdmitScope.Player, null, catalogRun: true);
        var pl = DungeonRunAdmission.Classify(C(id), id, AdmitScope.Player, null, catalogRun: false);
        var b = DungeonRunAdmission.Classify(C(id), id, AdmitScope.Boss, null, catalogRun: true);
        Check($"T2 admission {id}: the {pair.Value} row admits the player (catalog and legacy verdicts), never the boss",
            p.Subject && p.Admitted && pl.Admitted && !b.Admitted && DungeonRunAdmission.RuleFor(id)?.Family == pair.Value, p.Family + " " + p.Admit);
        Check($"T2 admission {id}: the boss's generation never yields it", DungeonRunAdmission.GeneratedRejectsBoss(true).Contains(id));
    }
    var kb = DungeonRunAdmission.Classify(C("DALA_716t"), "DALA_716t", AdmitScope.Both, null, catalogRun: true);
    Check("T2 admission DALA_716t: the Kadoom Bot row admits both sides (it reaches the boss's deck), never generation",
        kb.Admitted && DungeonRunAdmission.RuleFor("DALA_716t")?.Family == "run-deck-token" && DungeonRunAdmission.GeneratedRejectsBoss(true).Contains("DALA_716t"));
    Check("T2 the loop guard: Elistra's run-log row carries RepeatUntil max=60 (ElistraMaxRecasts); the Patron row carries none (the board bounds it)",
        DungeonRunAdmission.RuleFor("DALA_718")?.Guard == "RepeatUntil max=60" && DungeonRunContent.ElistraMaxRecasts == 60 && DungeonRunAdmission.RuleFor("DALA_705")?.Guard == null);
}

// ---------------------------------------------------------------- T3 offers: every villain, never another run; the stages never
{
    var offered = new Dictionary<string, HashSet<string>>();
    foreach (var cls in data.Classes)
    {
        var set = offered[cls.Id] = new HashSet<string>();
        int seeds = villains.Contains(cls.Id) ? 300 : 40;
        for (long seed = 1; seed <= seeds; seed++)
        {
            var s = run.NewRun(all, cls.Id, cls.DefaultDeck, cls.DefaultPower, seed, signatureId: run.SignatureOffer(all, run.Data.FindClass(cls.Id), seed).FirstOrDefault());
            for (int k = 0; k < 3 && run.OpenTreasureDraft(s); k++)
            {
                set.UnionWith(s.OpenDraft.Offered);
                run.PickTreasure(s, s.OpenDraft.Offered[0]);
            }
        }
    }
    var heist = new HashSet<string>(villains.SelectMany(h => offered[h]));
    foreach (string id in offeredF7) Check($"T3 {id} is offered to Dalaran Heist runs", heist.Contains(id));
    foreach (string h in villains)
        Check($"T3 {h}: offered Heist actives (at least 15 of the 28)", offered[h].Count(offeredF7.Contains) >= 15, offered[h].Count(offeredF7.Contains).ToString());
    Check("T3 no Twinspell stage (714a, 714b, 714c) is ever offered", !offered.Values.Any(v => v.Overlaps(stages)));
    Check("T3 no other run is offered a Heist active treasure (deck scope)",
        offered.Where(p => !villains.Contains(p.Key)).All(p => !p.Value.Overlaps(f7)),
        string.Join(";", offered.Where(p => !villains.Contains(p.Key) && p.Value.Overlaps(f7)).Select(p => p.Key)));
    var sp = NewAct(Chu, 9);
    run.OpenTreasureDraft(sp);
    sp.OpenDraft.Offered[0] = "DALA_705";
    int before = sp.Deck.Count;
    run.PickTreasure(sp, "DALA_705");
    Check("T3 PickTreasure DALA_705 on a Mr. Chu run: the run deck gains it (origin treasure)",
        sp.Deck.Count == before + 1 && sp.Cards.Last().Id == "DALA_705" && sp.Cards.Last().Origin == DungeonRunDeck.OriginTreasure && sp.ActiveTreasures.Contains("DALA_705"));
    var pool = run.TreasurePool(sp, "Heist");
    var ctx = run.BuildContext(sp);
    Check("T3 the run context's Heist pool: every offered F7 id, no stage, no other adventure's",
        offeredF7.All(pool.Contains) && !stages.Any(pool.Contains) && pool.All(id => id.StartsWith("DALA_7")) && ctx.TreasurePool.Any(id => id.StartsWith("LOOTA_"))
        && pool.OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(ctx.TreasurePool.Where(id => ctx.TreasurePoolTags.TryGetValue(id, out var t) && t == "Heist")),
        string.Join(",", pool));
}

// ---------------------------------------------------------------- T4 DALA_700 Continuum Collider
{
    var (s, plan, g) = Fight(Rakanishu, 401, new[] { "DALA_700" });
    Check("T4 DALA_700 is a card of the run deck the match plan deals", FromRunDeck(s, plan, "DALA_700"));
    var p1 = g.Player1; var p2 = g.Player2;
    var col = Draw(g, "DALA_700"); Mana(p1, 10);
    Check("T4 DALA_700 played", Play(g, col));
    var order = new List<string>();
    for (int i = 0; i < 5; i++) { EndTurn(g); order.Add(g.CurrentPlayer == p1 ? "me" : "op"); }
    Check("T4 DALA_700: I take an extra turn, then my opponent its turn and an extra turn after, then the usual alternation",
        string.Join(",", order) == "me,op,op,me,op", string.Join(",", order));
    var grants = g.HistoryFacts.Query.Events<SabberStoneCore.Model.ExtraTurnGrantedFact>().ToList();
    Check("T4 DALA_700: two granted extra turns are history facts (mine and my opponent's)",
        grants.Count == 2 && grants.Any(f => f.ControllerId == p1.Id) && grants.Any(f => f.ControllerId == p2.Id), grants.Count.ToString());
}

// ---------------------------------------------------------------- T5 DALA_701 The Box (H2, Heist pool)
{
    var (s, plan, g) = Fight(Eudora, 411, new[] { "DALA_701" });
    var p1 = g.Player1;
    var box = DrawRun(g, s, "DALA_701");
    Check("T5 DALA_701: drawn from the run deck it is The Box", box.Card.Id == "DALA_701");
    var heistPool = run.TreasurePool(s, "Heist");
    Check("T5 the match's run context carries the Heist treasure pool (28 actives; 29 since F15 adds DALA_707)", RunHelpers.TreasurePool(g, "Heist").SequenceEqual(heistPool) && heistPool.Count == 29, heistPool.Count.ToString());
    // A change to another card type is a new entity with the same id (Generic.ChangeEntity): always read the live one.
    IPlayable Live(Game gg, int id) => (IPlayable)gg.IdEntityDic[id];
    int boxId = box.Id;
    EndTurn(g); EndTurn(g);
    string first = Live(g, boxId).Card.Id;
    Check("T5 DALA_701: at the start of my turn it became a random Heist treasure (never The Box) with Sifting",
        first != "DALA_701" && heistPool.Contains(first) && p1.HandZone.Contains(Live(g, boxId)) && Live(g, boxId).AppliedEnchantments.Any(e => e.Card.Id == "DALA_701e"), first);
    var seen = new List<string> { first };
    bool held = true;
    for (int i = 0; i < 8; i++)
    {
        EndTurn(g); EndTurn(g);
        var live = Live(g, boxId);
        seen.Add(live.Card.Id);
        held &= heistPool.Contains(live.Card.Id) && p1.HandZone.Contains(live) && p1.HandZone.Count(e => e.Id == boxId) == 1
            && live.AppliedEnchantments.Count(e => e.Card.Id == "DALA_701e") == 1;
    }
    Check("T5 DALA_701: it transforms every turn, across card types, and stays one hand card with one Sifting",
        held && seen.Select(id => C(id).Type).Distinct().Count() >= 2, string.Join(",", seen));
    string Seq(int seed)
    {
        var (ss, _, gs) = Fight(Eudora, seed, new[] { "DALA_701" });
        int b = DrawRun(gs, ss, "DALA_701").Id;
        var ids = new List<string>();
        for (int i = 0; i < 3; i++) { EndTurn(gs); EndTurn(gs); ids.Add(Live(gs, b).Card.Id); }
        return string.Join(",", ids);
    }
    Check("T5 DALA_701: the same seed transforms the same way", Seq(412) == Seq(412));
    var final = Live(g, boxId); Mana(p1, 10);
    var playable = p1.Options().OfType<PlayCardTask>().FirstOrDefault(o => o.Source == final);
    if (playable != null) g.Process(playable);
    Check("T5 DALA_701: the treasure it became is played as itself; Sifting leaves with the play",
        playable == null || final.Zone != p1.HandZone && !(final.AppliedEnchantments?.Any(e => e.Card.Id == "DALA_701e") ?? false), final.Card.Id);
    var bare = new Game(new GameConfig { StartPlayer = 1, Player1HeroClass = CardClass.ROGUE, Player2HeroClass = CardClass.MAGE, FillDecks = true, Shuffle = false, History = false, Logging = false });
    bare.StartGame();
    var bareBox = ToHand(bare, bare.Player1, "DALA_701");
    bare.Process(EndTurnTask.Any(bare.CurrentPlayer)); bare.Process(EndTurnTask.Any(bare.CurrentPlayer));
    Check("T5 DALA_701 outside a run match: the pool is empty, it stays The Box", bareBox.Card.Id == "DALA_701");
}

// ---------------------------------------------------------------- T6 DALA_702 Gnomish Army Knife
{
    var (s, plan, g) = Fight(Chu, 421, new[] { "DALA_702" });
    var p1 = g.Player1;
    var knife = Draw(g, "DALA_702"); Mana(p1, 10);
    var yeti = (Minion)ToHand(g, p1, "CS2_182"); Play(g, yeti);
    Play(g, knife, yeti);
    Check("T6 DALA_702: the minion has Charge, Windfury, Divine Shield, Lifesteal, Poisonous, Taunt and Stealth (Tooled Up!)",
        yeti.HasCharge && yeti.HasWindfury && yeti.HasDivineShield && yeti.HasLifeSteal && yeti.Poisonous && yeti.HasTaunt && yeti.HasStealth
        && yeti.AppliedEnchantments.Any(e => e.Card.Id == "DALA_702e"),
        $"charge {yeti.HasCharge} wf {yeti.HasWindfury} ds {yeti.HasDivineShield} ls {yeti.HasLifeSteal} poison {yeti.Poisonous} taunt {yeti.HasTaunt} stealth {yeti.HasStealth}");
    Check("T6 DALA_702: Charge lets the minion just played attack the enemy hero", g.Process(MinionAttackTask.Any(p1, yeti, g.Player2.Hero)) && g.Player2.Hero.Damage == 4);
    Check("T6 DALA_702: Lifesteal healed my hero, Windfury allows a second attack", g.Process(MinionAttackTask.Any(p1, yeti, g.Player2.Hero)) && g.Player2.Hero.Damage == 8);
}

// ---------------------------------------------------------------- T7 DALA_703 The Muscle
{
    var (s, plan, g) = Fight(Chu, 431, new[] { "DALA_703" });
    var p1 = g.Player1;
    var muscle = Draw(g, "DALA_703"); Mana(p1, 10); Clear(p1); Generic.AddHandPhase(p1, muscle);
    Play(g, muscle);
    var added = p1.HandZone.ToList();
    Check("T7 DALA_703: a 4/5 Battlecry adds three random scripted cards to my hand, each costing (0)",
        ((Minion)muscle).AttackDamage == 4 && ((Minion)muscle).Health == 5 && added.Count == 3 && added.All(e => e.Cost == 0 && e.Card.Implemented),
        string.Join(",", added.Select(e => e.Card.Id + ":" + e.Cost)));
    string Adds(int seed)
    {
        var (_, _, gs) = Fight(Chu, seed, new[] { "DALA_703" });
        var m = Draw(gs, "DALA_703"); Mana(gs.Player1, 10); Clear(gs.Player1); Generic.AddHandPhase(gs.Player1, m);
        Play(gs, m);
        return string.Join(",", gs.Player1.HandZone.Select(e => e.Card.Id));
    }
    Check("T7 DALA_703: the same seed adds the same cards", Adds(432) == Adds(432));
}

// ---------------------------------------------------------------- T8 DALA_704 Super Simian Sphere
{
    var (s, plan, g) = Fight(Squeamlish, 441, new[] { "DALA_704" });
    var p1 = g.Player1; var p2 = g.Player2;
    var sphere = Draw(g, "DALA_704"); Mana(p1, 10);
    Play(g, sphere);
    var mukla = p1.BoardZone.GetAll().FirstOrDefault(m => m.Card.Id == "EX1_014");
    Check("T8 DALA_704: King Mukla is summoned (no Battlecry: the opponent gets no Bananas), Immune, not targetable by spells or Hero Powers",
        mukla != null && mukla.IsImmune && mukla[GameTag.CANT_BE_TARGETED_BY_SPELLS] == 1 && mukla[GameTag.CANT_BE_TARGETED_BY_HERO_POWERS] == 1
        && !p2.HandZone.Any(e => e.Card.Id == "EX1_014t"));
    var fireball = ToHand(g, p1, "CS2_029");
    Check("T8 DALA_704: my Fireball cannot target Mukla", p1.Options().OfType<PlayCardTask>().Where(o => o.Source == fireball).All(o => o.Target != mukla));
    EndTurn(g); Mana(p2, 10);
    var bossBall = ToHand(g, p2, "CS2_029");
    Check("T8 DALA_704: the boss's spells and Hero Power cannot target Mukla",
        p2.Options().OfType<PlayCardTask>().Where(o => o.Source == bossBall).All(o => o.Target != mukla)
        && p2.Options().OfType<HeroPowerTask>().All(o => o.Target != mukla));
    var boss = Board(g, p2, "CS2_182");
    p2.BoardZone.GetAll().ToList().ForEach(m => m.IsExhausted = false);
    if (p2.Options().OfType<MinionAttackTask>().Any(o => o.Target == mukla)) g.Process(MinionAttackTask.Any(p2, boss, mukla));
    Check("T8 DALA_704: Mukla takes no damage (Immune)", mukla.Damage == 0 && mukla.Zone == p1.BoardZone);
}

// ---------------------------------------------------------------- T9 DALA_705 Grimmer Patron
{
    var (s, plan, g) = Fight(Chu, 451, new[] { "DALA_705" });
    var p1 = g.Player1; var p2 = g.Player2;
    var patron = Draw(g, "DALA_705"); Mana(p1, 10);
    Play(g, patron);
    int Patrons() => p1.BoardZone.GetAll().Count(m => m.Card.Id == "DALA_705");
    EndTurn(g);
    int a = Patrons();
    EndTurn(g);
    int b = Patrons();
    EndTurn(g);
    int c = Patrons();
    EndTurn(g); EndTurn(g);
    int d = Patrons();
    Check("T9 DALA_705: each Patron summons one copy at the end of my turn (1, 2, 2 after the boss's turn, 4, 7 at the board limit)",
        a == 2 && b == 2 && c == 4 && d == 7 && p1.BoardZone.IsFull, $"{a} {b} {c} {d}");
    var buffed = p1.BoardZone.GetAll().First(m => m.Card.Id == "DALA_705");
    Check("T9 DALA_705: a copy is a 3/3 Grimmer Patron", buffed.AttackDamage == 3 && buffed.Health == 3);
}

// ---------------------------------------------------------------- T10 DALA_706 EVIL Propaganda
{
    var (s, plan, g) = Fight(Tekahn, 461, new[] { "DALA_706" });
    var p1 = g.Player1; var p2 = g.Player2;
    var prop = Draw(g, "DALA_706"); Mana(p1, 10);
    for (int i = 0; i < 5; i++) Board(g, p1, "CS2_231");
    var e1 = Board(g, p2, "CS2_182"); var e2 = Board(g, p2, "CS2_120"); var e3 = Board(g, p2, "CS2_168");
    Play(g, prop);
    Check("T10 DALA_706: I take control of the enemy minions while my board has room; the one that does not fit is destroyed (the control rule)",
        p2.BoardZone.Count == 0 && p1.BoardZone.Count == 7 && e1.Controller == p1 && e2.Controller == p1 && e3.IsDead, $"{p1.BoardZone.Count} {p2.BoardZone.Count} {e3.IsDead}");
}

// ---------------------------------------------------------------- T11 DALA_708 Untold Splendor
{
    var (s, plan, g) = Fight(Vessina, 471, new[] { "DALA_708" });
    var p1 = g.Player1;
    var spl = Draw(g, "DALA_708"); Mana(p1, 10);
    var heistPool = run.TreasurePool(s, "Heist");
    var before = p1.DeckZone.Select(e => e.Id).ToHashSet();
    Play(g, spl);
    var added = p1.DeckZone.Where(e => !before.Contains(e.Id)).ToList();
    Check("T11 DALA_708: five random Heist treasures (never itself) are shuffled into my deck",
        added.Count == 5 && added.All(e => heistPool.Contains(e.Card.Id) && e.Card.Id != "DALA_708"), string.Join(",", added.Select(e => e.Card.Id)));
}

// ---------------------------------------------------------------- T12 DALA_709 Golden Candle (H8 belief reset)
{
    var (s, plan, g) = Fight(George, 481, new[] { "DALA_709" });
    var p1 = g.Player1;
    var candle = Draw(g, "DALA_709"); Mana(p1, 10);
    int hand = p1.HandZone.Count, deck = p1.DeckZone.Count;
    Play(g, candle);
    var others = p1.HandZone.Concat(p1.DeckZone).ToList();
    Check("T12 DALA_709: a 7/7 Taunt; every other card of my hand and deck is now a scripted Legendary minion",
        ((Minion)candle).AttackDamage == 7 && ((Minion)candle).HasTaunt && others.Count == hand - 1 + deck
        && others.All(e => e.Card.Type == CardType.MINION && e.Card.Rarity == Rarity.LEGENDARY && e.Card.Implemented));
    var reset = g.RunBeliefResets.LastOrDefault();
    Check("T12 DALA_709: a belief reset of my hand and deck is recorded for the fair search (H8)",
        reset != null && reset.ControllerId == p1.PlayerId && reset.Zones == RunBeliefZones.HandAndDeck && reset.EntityIds.Count == others.Count);
}

// ---------------------------------------------------------------- T13 DALA_711 Dreamgrove Ring
{
    var (s, plan, g) = Fight(Squeamlish, 491, new[] { "DALA_711" });
    var p1 = g.Player1;
    var ring = Draw(g, "DALA_711"); Mana(p1, 10);
    Play(g, ring);
    var offers = p1.Choice?.Choices.Select(id => g.IdEntityDic[id].Card).ToList() ?? new List<Card>();
    Check("T13 DALA_711: Discovers 3 distinct scripted Legendary minions", offers.Count == 3 && offers.Select(c => c.Id).Distinct().Count() == 3
        && offers.All(c => c.Type == CardType.MINION && c.Rarity == Rarity.LEGENDARY && c.Implemented), string.Join(",", offers.Select(c => c.Id)));
    string pick = offers[0].Id;
    g.Process(ChooseTask.Pick(p1, p1.Choice.Choices[0]));
    Check("T13 DALA_711: five copies of the pick are on my board", p1.BoardZone.GetAll().Count(m => m.Card.Id == pick) == 5, p1.BoardZone.Count.ToString());
}

// ---------------------------------------------------------------- T14 DALA_712 Orb of the Untold
{
    var (s, plan, g) = Fight(Rakanishu, 501, new[] { "DALA_712" });
    var p1 = g.Player1; var p2 = g.Player2;
    var orb = Draw(g, "DALA_712"); Mana(p1, 10);
    var a = Board(g, p2, "CS2_200"); var b = Board(g, p2, "CS2_200");
    int heroBefore = p2.Hero.Damage;
    Play(g, orb);
    int dealt = p2.Hero.Damage - heroBefore + a.Damage + b.Damage;
    Check("T14 DALA_712: 20 damage split between all enemies, one point at a time", dealt == 20 && g.Player1.Hero.Damage == 0, dealt.ToString());
    var (_, _, g2) = Fight(Rakanishu, 502, new[] { "DALA_712" });
    var orb2 = Draw(g2, "DALA_712"); Mana(g2.Player1, 10);
    Board(g2, g2.Player1, "CS2_197");
    int h2 = g2.Player2.Hero.Damage;
    Play(g2, orb2);
    Check("T14 DALA_712: Spell Damage +1 adds a hit (21)", g2.Player2.Hero.Damage - h2 == 21, (g2.Player2.Hero.Damage - h2).ToString());
}

// ---------------------------------------------------------------- T15 DALA_713 Elder Taggawag
{
    var (s, plan, g) = Fight(Barkeye, 511, new[] { "DALA_713" });
    var p1 = g.Player1; var p2 = g.Player2;
    var tag = Draw(g, "DALA_713"); Mana(p1, 10);
    var e1 = Board(g, p2, "CS2_182"); var e2 = Board(g, p2, "CS2_120");
    Play(g, tag);
    Check("T15 DALA_713: the 4/4 Battlecry deals 4 to every enemy minion", e1.Damage == 4 && e2.IsDead && ((Minion)tag).AttackDamage == 4);
    int deck = p1.DeckZone.Count;
    Kill(g, (Minion)tag);
    Check("T15 DALA_713: its Deathrattle shuffles a new Elder Taggawag into my deck", p1.DeckZone.Count == deck + 1 && p1.DeckZone.Any(e => e.Card.Id == "DALA_713"));
}

// ---------------------------------------------------------------- T16 DALA_714 THE... Candles? (Twinspell stages)
{
    var (s, plan, g) = Fight(Kriziki, 521, new[] { "DALA_714" });
    var p1 = g.Player1; var p2 = g.Player2;
    var candles = Draw(g, "DALA_714"); Mana(p1, 10); Clear(p1); Generic.AddHandPhase(p1, candles);
    var giant = Board(g, p2, "CS2_200");
    var seen = new List<string>();
    for (int i = 0; i < 4; i++)
    {
        var card = p1.HandZone.FirstOrDefault(e => e.Card.Id.StartsWith("DALA_714"));
        if (card == null) break;
        seen.Add(card.Card.Id);
        Mana(p1, 10);
        Play(g, card);
    }
    Check("T16 DALA_714: each cast leaves the next Twinspell stage in hand: 714, 714a, 714b, 714c, then none",
        string.Join(",", seen) == "DALA_714,DALA_714a,DALA_714b,DALA_714c" && !p1.HandZone.Any(e => e.Card.Id.StartsWith("DALA_714")), string.Join(",", seen));
    Check("T16 DALA_714: each stage deals 3 to the enemy minions (12 to an 8-Health Boulderfist: dead)", giant.IsDead);
}

// ---------------------------------------------------------------- T17 DALA_715 Overpowered (one-turn replay)
{
    var (s, plan, g) = Fight(Rakanishu, 531, new[] { "DALA_715" });
    var p1 = g.Player1; var p2 = g.Player2;
    var op = Draw(g, "DALA_715"); Mana(p1, 10);
    Play(g, op);
    Check("T17 DALA_715: Overpowered attaches its one-turn player enchantment DALA_715e", p1.AppliedEnchantments.Any(e => e.Card.Id == "DALA_715e"));
    int total() => p1.Hero.Damage + p2.Hero.Damage + p1.BoardZone.GetAll().Sum(m => m.Damage) + p2.BoardZone.GetAll().Sum(m => m.Damage);
    p2.Hero.Armor = 40; p1.Hero.Armor = 40;
    int armor() => 80 - p1.Hero.Armor - p2.Hero.Armor;
    Play(g, ToHand(g, p1, "CS2_029"), p2.Hero);
    Check("T17 DALA_715: a Fireball I play is replayed once at a random target (12 damage dealt in all)", armor() + total() == 12, (armor() + total()).ToString());
    int handBefore = p1.HandZone.Count;
    Play(g, ToHand(g, p1, "EX1_015"));
    Check("T17 DALA_715: a minion I play is replayed once: a second Novice Engineer, its Battlecry included (two draws)",
        p1.BoardZone.GetAll().Count(m => m.Card.Id == "EX1_015") == 2 && p1.HandZone.Count == handBefore + 2, (p1.HandZone.Count - handBefore).ToString());
    Play(g, ToHand(g, p1, "CS2_106"));
    Check("T17 DALA_715: a weapon I play is replayed once (a new Fiery War Axe replaces it)", p1.Hero.Weapon?.Card.Id == "CS2_106");
    EndTurn(g); EndTurn(g); Mana(p1, 10);
    Check("T17 DALA_715: the enchantment is gone at the end of my turn", !p1.AppliedEnchantments.Any(e => e.Card.Id == "DALA_715e"));
    Play(g, ToHand(g, p1, "EX1_015"));
    Check("T17 DALA_715: the next turn nothing is replayed", p1.BoardZone.GetAll().Count(m => m.Card.Id == "EX1_015") == 3);
    var (_, _, g2) = Fight(Rakanishu, 532, new[] { "DALA_715" });
    var op2 = Draw(g2, "DALA_715"); Mana(g2.Player1, 10); Play(g2, op2);
    Play(g2, ToHand(g2, g2.Player1, "DAL_608"));
    if (g2.Player1.Choice != null) g2.Process(ChooseTask.Pick(g2.Player1, g2.Player1.Choice.Choices[0]));
    Check("T17 DALA_715: a Discover spell is replayed without a fault and no choice is left open", g2.Player1.Choice == null && g2.State == State.RUNNING);
}

// ---------------------------------------------------------------- T18 DALA_716 Fly-By and the Kadoom Bot
{
    var (s, plan, g) = Fight(Eudora, 541, new[] { "DALA_716" });
    var p1 = g.Player1; var p2 = g.Player2;
    var fly = Draw(g, "DALA_716"); Mana(p1, 10);
    int opDeck = p2.DeckZone.Count;
    Play(g, fly);
    var bot = p2.DeckZone.FirstOrDefault(e => e.Card.Id == "DALA_716t");
    Check("T18 DALA_716: a Kadoom Bot is shuffled into the opponent's deck", bot != null && p2.DeckZone.Count == opDeck + 1);
    p2.Hero.Armor = 60;
    int hand = p2.HandZone.Count;
    Generic.Draw(p2, bot);
    g.DeathProcessingAndAuraUpdate();
    Check("T18 DALA_716t: drawn, it casts itself: the drawer takes 50 damage and draws the next card instead",
        p2.Hero.Armor == 10 && !p2.HandZone.Any(e => e.Card.Id == "DALA_716t") && p2.HandZone.Count == hand + 1, $"{p2.Hero.Armor} {p2.HandZone.Count - hand}");
}

// ---------------------------------------------------------------- T20 DALA_717 Case Study
{
    var (s, plan, g) = Fight(Chu, 551, new[] { "DALA_717" });
    var p1 = g.Player1;
    var cs = Draw(g, "DALA_717"); Mana(p1, 10);
    Play(g, cs);
    var offers = p1.Choice?.Choices.Select(id => g.IdEntityDic[id].Card.Id).ToList() ?? new List<string>();
    Check("T20 DALA_717: Discovers 3 distinct scripted Quest Rewards", offers.Count == 3 && offers.Distinct().Count() == 3 && offers.All(DungeonRunContent.QuestRewards.Contains),
        string.Join(",", offers));
    g.Process(ChooseTask.Pick(p1, p1.Choice.Choices[2]));
    Check("T20 DALA_717: the pick is in my hand", p1.HandZone.Any(e => e.Card.Id == offers[2]) && p1.Choice == null);
}

// ---------------------------------------------------------------- T21 DALA_718 Elistra the Immortal (run log of spells)
{
    var (s, plan, g) = Fight(George, 561, new[] { "DALA_718" });
    var p1 = g.Player1;
    var eli = (Minion)Draw(g, "DALA_718"); Mana(p1, 10);
    int slot = Slot(eli);
    Play(g, eli);
    Check("T21 DALA_718: a 1/1 run deck card; an empty log recasts nothing", slot >= 0 && eli.AttackDamage == 1 && eli.Health == 1 && Logs(g, slot).Count == 0);
    Play(g, ToHand(g, p1, "CS2_092"), eli);
    Play(g, ToHand(g, p1, "CS2_004"), eli);
    Play(g, ToHand(g, p1, "CS2_029"), g.Player2.Hero);
    Check("T21 DALA_718: the spells I cast on it are logged in order (Blessing of Kings, Power Word: Shield); a spell on another target is not",
        string.Join(",", Logs(g, slot)) == "CS2_092,CS2_004", string.Join(",", Logs(g, slot)));
    var notes = run.ApplyMatchResult(s, g.RunResult);
    Check("T21 DALA_718: the won fight keeps the log on the card record", notes.Count == 0 && s.Cards[slot].Id == "DALA_718"
        && string.Join(",", s.Cards[slot].Log) == "CS2_092,CS2_004", string.Join(";", notes));
    var plan2 = run.BuildPlan(s);
    var g2 = StartFight(plan2, run.MatchSeed(s));
    var eli2 = (Minion)Draw(g2, "DALA_718"); Mana(g2.Player1, 10);
    int hand = g2.Player1.HandZone.Count;
    Play(g2, eli2);
    Check("T21 DALA_718: next fight, its Battlecry recasts both spells on it, in order (Blessing of Kings then Power Word: Shield: 5/7, one card drawn)",
        eli2.AttackDamage == 5 && eli2.Health == 7 && g2.Player1.HandZone.Count == hand, $"{eli2.AttackDamage}/{eli2.Health} hand {g2.Player1.HandZone.Count - hand}");
    Check("T21 DALA_718: the recasts are effect casts and are not logged again", Logs(g2, Slot(eli2)).Count == 0);
    var (s3, _, g3) = Fight(George, 562, new[] { "DALA_718" });
    var eli3 = (Minion)Draw(g3, "DALA_718");
    int slot3 = Slot(eli3);
    for (int i = 0; i < 70; i++) s3.Cards[slot3].Log.Add("CS2_092");
    var g4 = StartFight(run.BuildPlan(s3), run.MatchSeed(s3));
    var eli4 = (Minion)Draw(g4, "DALA_718"); Mana(g4.Player1, 10);
    Play(g4, eli4);
    Check("T21 DALA_718: a 70-spell log is recast at most 60 times (the loop guard)", eli4.Health == 1 + 4 * 60 && g4.State == State.RUNNING, eli4.Health.ToString());
}

// ---------------------------------------------------------------- T22 DALA_719 Soulreaper's Scythe (run log of kills)
{
    var (s, plan, g) = Fight(Chu, 571, new[] { "DALA_719" });
    var p1 = g.Player1; var p2 = g.Player2;
    var scythe = Draw(g, "DALA_719"); Mana(p1, 10);
    int slot = Slot(scythe);
    Play(g, scythe);
    var croc = Board(g, p2, "CS2_120");
    Check("T22 DALA_719: a 6/1 weapon", p1.Hero.Weapon?.Card.Id == "DALA_719" && p1.Hero.AttackDamage == 6);
    g.Process(HeroAttackTask.Any(p1, croc));
    Check("T22 DALA_719: the kill is logged; the weapon breaks and its Deathrattle summons the River Crocolisk it killed this run",
        croc.IsDead && p1.Hero.Weapon == null && string.Join(",", Logs(g, slot)) == "CS2_120" && p1.BoardZone.GetAll().Count(m => m.Card.Id == "CS2_120") == 1,
        string.Join(",", Logs(g, slot)) + " board " + string.Join(",", p1.BoardZone.GetAll().Select(m => m.Card.Id)));
    var notes = run.ApplyMatchResult(s, g.RunResult);
    Check("T22 DALA_719: the won fight keeps the log", notes.Count == 0 && string.Join(",", s.Cards[slot].Log) == "CS2_120");
    var g2 = StartFight(run.BuildPlan(s), run.MatchSeed(s));
    var sc2 = Draw(g2, "DALA_719"); Mana(g2.Player1, 10);
    Play(g2, sc2);
    var yeti = Board(g2, g2.Player2, "CS2_182");
    g2.Process(HeroAttackTask.Any(g2.Player1, yeti));
    var mine = g2.Player1.BoardZone.GetAll().Select(m => m.Card.Id).ToList();
    Check("T22 DALA_719: next fight, a kill and the break summon the earlier kill and this fight's (Crocolisk, Yeti)",
        yeti.IsDead && string.Join(",", mine) == "CS2_120,CS2_182", string.Join(",", mine));
    var (s4, _, g4) = Fight(Chu, 572, new[] { "DALA_719" });
    var sc4 = Draw(g4, "DALA_719"); Mana(g4.Player1, 10);
    for (int i = 0; i < 6; i++) Board(g4, g4.Player1, "CS2_231");
    Play(g4, sc4);
    var e4 = Board(g4, g4.Player2, "CS2_120");
    g4.Process(HeroAttackTask.Any(g4.Player1, e4));
    Check("T22 DALA_719: the summons stop at the board limit", g4.Player1.BoardZone.Count == 7);
}

// ---------------------------------------------------------------- T23 DALA_720 Swampqueen's Call
{
    var (s, plan, g) = Fight(Vessina, 581, new[] { "DALA_720" });
    var p1 = g.Player1;
    var call = Draw(g, "DALA_720"); Mana(p1, 10);
    Board(g, p1, "CS2_231"); Board(g, p1, "CS2_182");
    Play(g, call);
    var mine = p1.BoardZone.GetAll().ToList();
    Check("T23 DALA_720: my minions became scripted Legendary minions", mine.Count == 2 && mine.All(m => m.Card.Rarity == Rarity.LEGENDARY && m.Card.Implemented),
        string.Join(",", mine.Select(m => m.Card.Id)));
    var echo = p1.HandZone.FirstOrDefault(e => e.Card.Id == "DALA_720");
    Check("T23 DALA_720: Repeatable this turn: a Ghostly copy is in my hand", echo != null && echo[GameTag.GHOSTLY] == 1);
    Mana(p1, 10);
    Play(g, echo);
    Check("T23 DALA_720: the copy works and leaves another copy", p1.HandZone.Count(e => e.Card.Id == "DALA_720") == 1);
    EndTurn(g);
    Check("T23 DALA_720: the copies are discarded at the end of my turn", !p1.HandZone.Any(e => e.Card.Id == "DALA_720"));
}

// ---------------------------------------------------------------- T24 DALA_721 Duplatransmogrifier (run deck replace)
{
    var (s, plan, g) = Fight(Chu, 591, new[] { "DALA_721", "DALA_705" });
    var p1 = g.Player1;
    var dup = Draw(g, "DALA_721"); Mana(p1, 10);
    var yeti = Board(g, p1, "CS2_182");
    var deckBefore = s.Cards.Select(c => c.Id).ToList();
    Play(g, dup, yeti);
    var edits = g.RunResult?.Edits.ToList() ?? new List<RunEdit>();
    var minionSlots = Enumerable.Range(0, s.Cards.Count).Where(i => C(s.Cards[i].Id).Type == CardType.MINION && !DungeonRunDeck.IsTreasureOrigin(s.Cards[i].Origin)).ToList();
    Check("T24 DALA_721: a replace edit for every non-treasure minion slot of the run deck, none for a spell or a treasure",
        edits.Count == minionSlots.Count && edits.All(e => e.Op == RunEdit.OpReplace && e.CardId == "CS2_182") && edits.Select(e => e.SlotIndex).SequenceEqual(minionSlots) && minionSlots.Count > 0,
        edits.Count + " vs " + minionSlots.Count);
    Check("T24 DALA_721: nothing changes in the match (my deck keeps its cards)", p1.DeckZone.All(e => e.Card.Id != "CS2_182" || deckBefore.Contains("CS2_182")));
    var notes = run.ApplyMatchResult(s, g.RunResult);
    Check("T24 DALA_721: after a won fight, every non-treasure minion of the run deck is a Chillwind Yeti; spells and the treasures are kept",
        notes.Count == 0 && minionSlots.All(i => s.Cards[i].Id == "CS2_182" && s.Cards[i].Origin == DungeonRunDeck.OriginMatch)
        && s.Cards.Count == deckBefore.Count && s.Cards.Where((c, i) => !minionSlots.Contains(i)).Select(c => c.Id).SequenceEqual(deckBefore.Where((c, i) => !minionSlots.Contains(i)))
        && s.Cards.Any(c => c.Id == "DALA_705") && s.Deck.SequenceEqual(s.Cards.Select(c => c.Id)), string.Join(";", notes));
    var (s2, _, g2) = Fight(Chu, 592, new[] { "DALA_721" });
    var d2 = Draw(g2, "DALA_721"); Mana(g2.Player1, 10);
    var token = Board(g2, g2.Player1, "CS2_101t");
    var before2 = s2.Cards.Select(c => c.Id).ToList();
    Play(g2, d2, token);
    var notes2 = run.ApplyMatchResult(s2, g2.RunResult);
    Check("T24 DALA_721: a minion the run deck may not hold (a Silver Hand Recruit token) is refused as a whole, with a note",
        notes2.Count == 1 && notes2[0].StartsWith("edits refused") && s2.Cards.Select(c => c.Id).SequenceEqual(before2), string.Join(";", notes2));
    var (s3, _, g3) = Fight(Chu, 593, new[] { "DALA_721" });
    var d3 = Draw(g3, "DALA_721"); Mana(g3.Player1, 10);
    Play(g3, d3, Board(g3, g3.Player1, "CS2_182"));
    var before3 = s3.Cards.Select(c => c.Id).ToList();
    run.BeginMatch(s3);
    run.RecordOutcome(s3, all, DungeonRunOutcome.Loss, g3.RunResult);
    Check("T24 DALA_721: a lost fight keeps the run deck as it was", s3.Cards.Select(c => c.Id).SequenceEqual(before3));
}

// ---------------------------------------------------------------- T25 DALA_722 Annoy-o Horn
{
    var (s, plan, g) = Fight(Tekahn, 601, new[] { "DALA_722" });
    var p1 = g.Player1;
    var horn = Draw(g, "DALA_722"); Mana(p1, 10);
    Board(g, p1, "CS2_231"); Board(g, p1, "CS2_231");
    Play(g, horn);
    Check("T25 DALA_722: my board is filled with Annoy-o-Trons (Taunt, Divine Shield)",
        p1.BoardZone.IsFull && p1.BoardZone.GetAll().Count(m => m.Card.Id == "GVG_085") == 5 && p1.BoardZone.GetAll().Where(m => m.Card.Id == "GVG_085").All(m => m.HasTaunt && m.HasDivineShield));
    var opts = p1.Options().OfType<PlayCardTask>().Where(o => o.Source.Card.Id == "DALA_722");
    var horn2 = ToHand(g, p1, "DALA_722"); Mana(p1, 10);
    Check("T25 DALA_722: not playable with a full board", !p1.Options().OfType<PlayCardTask>().Any(o => o.Source == horn2));
}

// ---------------------------------------------------------------- T26 DALA_723 Hyperblaster
{
    var (s, plan, g) = Fight(Barkeye, 611, new[] { "DALA_723" });
    var p1 = g.Player1; var p2 = g.Player2;
    var hb = Draw(g, "DALA_723"); Mana(p1, 10);
    Play(g, hb);
    var yeti = Board(g, p2, "CS2_182");
    g.Process(HeroAttackTask.Any(p1, yeti));
    Check("T26 DALA_723: a 1/5 Poisonous weapon kills a Yeti; my hero is Immune while attacking (no damage taken)",
        yeti.IsDead && p1.Hero.Damage == 0 && p1.Hero.Weapon?.Durability == 4, $"{yeti.IsDead} {p1.Hero.Damage}");
    Check("T26 DALA_723: the Immune enchantment is gone after the attack", !p1.Hero.AppliedEnchantments.Any(e => e.Card.Id == "DALA_723e") && !p1.Hero.IsImmune);
}

// ---------------------------------------------------------------- T27 DALA_724 Big Boomba
{
    var (s, plan, g) = Fight(Eudora, 621, new[] { "DALA_724" });
    var p1 = g.Player1; var p2 = g.Player2;
    var boom = Draw(g, "DALA_724"); Mana(p1, 10);
    Board(g, p1, "CS2_182"); Board(g, p2, "CS2_182"); Board(g, p2, "CS2_120");
    Play(g, boom);
    Check("T27 DALA_724: every minion is destroyed; the player enchantment DALA_724e waits", p1.BoardZone.Count == 0 && p2.BoardZone.Count == 0
        && p1.AppliedEnchantments.Any(e => e.Card.Id == "DALA_724e"));
    EndTurn(g);
    Board(g, p2, "CS2_182");
    EndTurn(g);
    Check("T27 DALA_724: at the start of my next turn every minion is destroyed again, and the enchantment leaves",
        p2.BoardZone.Count == 0 && !p1.AppliedEnchantments.Any(e => e.Card.Id == "DALA_724e"));
    Board(g, p1, "CS2_182");
    EndTurn(g); EndTurn(g);
    Check("T27 DALA_724: the turn after, nothing is destroyed", p1.BoardZone.Count == 1);
}

// ---------------------------------------------------------------- T28 DALA_725 Banana Split
{
    var (s, plan, g) = Fight(Barkeye, 631, new[] { "DALA_725" });
    var p1 = g.Player1;
    var split = Draw(g, "DALA_725"); Mana(p1, 10);
    var wisp = Board(g, p1, "CS2_231");
    Play(g, split, wisp);
    var wisps = p1.BoardZone.GetAll().Where(m => m.Card.Id == "CS2_231").ToList();
    Check("T28 DALA_725: the minion gets +2/+2 (Glowing Green) and two copies of it are summoned, buff included",
        wisps.Count == 3 && wisps.All(m => m.AttackDamage == 3 && m.Health == 3), string.Join(",", wisps.Select(m => m.AttackDamage + "/" + m.Health)));
    var e = Board(g, g.Player2, "CS2_231");
    var split2 = ToHand(g, p1, "DALA_725"); Mana(p1, 10);
    Check("T28 DALA_725: only a friendly minion is a target", p1.Options().OfType<PlayCardTask>().Where(o => o.Source == split2).All(o => o.Target?.Controller == p1));
}

// ---------------------------------------------------------------- T29 DALA_726 Master Scheme
{
    var (s, plan, g) = Fight(Kriziki, 641, new[] { "DALA_726" });
    var p1 = g.Player1; var p2 = g.Player2;
    var scheme = Draw(g, "DALA_726");
    int n0 = scheme[GameTag.TAG_SCRIPT_DATA_NUM_1];
    EndTurn(g); EndTurn(g);
    int n1 = scheme[GameTag.TAG_SCRIPT_DATA_NUM_1];
    EndTurn(g); EndTurn(g);
    int n = scheme[GameTag.TAG_SCRIPT_DATA_NUM_1];
    Check("T29 DALA_726: @ starts at 1 and upgrades each turn in hand", n0 == 1 && n1 > n0 && n > n1, $"{n0} {n1} {n}");
    Mana(p1, 10);
    int hand = p1.HandZone.Count, armor = p1.Hero.Armor, dmg = p2.Hero.Damage;
    Play(g, scheme, p2.Hero);
    Check($"T29 DALA_726: @ = {n}: {n} damage, {n} cards, {n} Armor, {n} Boom Bots",
        p2.Hero.Damage - dmg == n && p1.HandZone.Count == hand - 1 + n && p1.Hero.Armor - armor == n && p1.BoardZone.GetAll().Count(m => m.Card.Id == "GVG_110t") == Math.Min(7, n),
        $"{p2.Hero.Damage - dmg} {p1.HandZone.Count - hand + 1} {p1.Hero.Armor - armor} {p1.BoardZone.Count}");
}

// ---------------------------------------------------------------- T30 DALA_727 Sow the Seeds
{
    var (s, plan, g) = Fight(Squeamlish, 651, new[] { "DALA_727" });
    var p1 = g.Player1;
    var sow = Draw(g, "DALA_727"); Mana(p1, 10);
    var minions = p1.DeckZone.OfType<Minion>().ToList();
    var spells = p1.DeckZone.Where(e => !(e is Minion)).ToList();
    Play(g, sow);
    Check("T30 DALA_727: every minion of my deck has +3/+3 (Seeds Sown); other cards are untouched",
        minions.Count > 0 && minions.All(m => m.AttackDamage == m.Card.ATK + 3 && m.Health == m.Card.Health + 3 && m.AppliedEnchantments.Any(e => e.Card.Id == "DALA_727e"))
        && spells.All(e => !(e.AppliedEnchantments?.Any(x => x.Card.Id == "DALA_727e") ?? false)));
    Check("T30 DALA_727: the boss's deck is untouched", g.Player2.DeckZone.OfType<Minion>().All(m => m.AttackDamage == m.Card.ATK));
}

// ---------------------------------------------------------------- T31 DALA_729 Dagwik Stickytoe, T32 DALA_730 Murgatha
{
    var (s, plan, g) = Fight(Eudora, 661, new[] { "DALA_729" });
    var p1 = g.Player1; var p2 = g.Player2;
    var dag = (Minion)Draw(g, "DALA_729"); Mana(p1, 10);
    Play(g, dag);
    Check("T31 DALA_729: a 2/5 Stealth minion", dag.AttackDamage == 2 && dag.Health == 5 && dag.HasStealth);
    EndTurn(g); EndTurn(g);
    var yeti = Board(g, p2, "CS2_120");
    int mine = p1.HandZone.Count, theirs = p2.HandZone.Count;
    g.Process(MinionAttackTask.Any(p1, dag, yeti));
    Check("T31 DALA_729: attacking a minion steals nothing", p1.HandZone.Count == mine && p2.HandZone.Count == theirs);
    EndTurn(g); EndTurn(g);
    mine = p1.HandZone.Count; theirs = p2.HandZone.Count;
    g.Process(MinionAttackTask.Any(p1, dag, p2.Hero));
    Check("T31 DALA_729: after it attacks a hero, a card of the opponent's hand moves to mine", p1.HandZone.Count == mine + 1 && p2.HandZone.Count == theirs - 1,
        $"{p1.HandZone.Count - mine} {p2.HandZone.Count - theirs}");

    var (s2, _, g2) = Fight(Vessina, 671, new[] { "DALA_730" });
    var q1 = g2.Player1; var q2 = g2.Player2;
    var mur = Draw(g2, "DALA_730"); Mana(q1, 10);
    Play(g2, mur);
    Check("T32 DALA_730: a 3/6 Murloc; playing it overloads nothing", ((Minion)mur).AttackDamage == 3 && ((Minion)mur).Health == 6 && q2.OverloadOwed == 0);
    Play(g2, ToHand(g2, q1, "CS2_231")); Play(g2, ToHand(g2, q1, "CS2_029"), q2.Hero);
    Check("T32 DALA_730: each card I play overloads one of the opponent's Mana Crystals", q2.OverloadOwed == 2, q2.OverloadOwed.ToString());
    // Phase 7 review: a player never has more crystals locked than it has (Game.MainRessources drops the excess), so the
    // opponent is given 3 crystals here (4 on its turn) to show both Overloads lock; with 1 crystal it would lock 1.
    int crystals = q2.BaseMana; q2.BaseMana = Math.Max(q2.BaseMana, 3);
    EndTurn(g2);
    Check("T32 DALA_730: the opponent's next turn starts with 2 crystals locked", q2.OverloadLocked == 2 && q2.RemainingMana == q2.BaseMana - 2, q2.OverloadLocked + " of " + q2.BaseMana + " (had " + crystals + ")");
    int owed = q2.OverloadOwed;
    g2.Process(PlayCardTask.Any(q2, ToHand(g2, q2, "CS2_231")));
    Check("T32 DALA_730: the opponent's plays do not fire it", q2.OverloadOwed == owed);
}

var testBudget = new Budget(6000, 2000, 300, 6, 3, 100, 8, turnSearchSeconds: 6, turnWallSeconds: 8);
string AiGame(string id, long seed, string row, string[] swapIn = null)
{
    var s = NewAct(row, seed);
    bool isPower = false;
    bool isPassive = false;
    bool isHero = false;
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
    // The run deck card's entity (by its dealt slot): The Box changes card while held, so it is followed by entity, not id.
    int runSlot = s.Cards.FindIndex(c => c.Id == id);
    int runEntity = runSlot >= 0 ? game.Player1DeckSlotIds[runSlot] : -1;
    game.MainBegin();
    if (!isPower && !isHero && !isPassive)
    {
        var e = game.Player1.DeckZone.FirstOrDefault(x => x.Card.Id == id || x.Id == runEntity);
        if (e != null) Generic.Draw(game.Player1, e);
    }
    if (swapIn != null)
        foreach (string give in swapIn) Generic.AddHandPhase(game.Player1, Entity.FromCard(game.Player1, Cards.FromId(give)));
    game.DeathProcessingAndAuraUpdate();
    int decisionIndex = 0, actions = 0, decisions = 0, fallbacks = 0, boostedTurn = -1, powerUses = 0;
    int playedTurn = isPassive || isHero ? 0 : -1;
    var recent = new List<string>();
    void Act(PlayerTask task, string who)
    {
        recent.Add(who + ":" + task + "@" + game.Turn); if (recent.Count > 12) recent.RemoveAt(0);
        if (Environment.GetEnvironmentVariable("F7_TRACE") == "1" && actions < 400) Console.Error.WriteLine($"{who} {task} t{game.Turn} e18={(game.IdEntityDic.TryGetValue(18, out var e18) ? e18.Card.Id + "/" + e18.Zone?.Type : "-")}");
        if (++actions > 1500)
        {
            string extra = task is MinionAttackTask mat ? $" src exhausted={((Minion)mat.Source).IsExhausted} atk={((Minion)mat.Source).NumAttacksThisTurn} tgt stealth={mat.Target is Minion tm && tm.HasStealth} immune={mat.Target.IsImmune} tgtzone={mat.Target.Zone?.Type} untouch={(mat.Target as Minion)?.Untouchable} cantattack={((Minion)mat.Source).CantAttack} canattack={((Minion)mat.Source).CanAttack}" : "";
            throw new Exception("stuck after 1500 actions: " + string.Join(" | ", recent) + extra);
        }
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
        else
        {
            var options = game.Player1.Options();
            PlayerTask mine = null;
            if (isPower) mine = options.OfType<HeroPowerTask>().FirstOrDefault();
            else if (playedTurn < 0)
            {
                var held = game.Player1.HandZone.FirstOrDefault(e => e.Card.Id == id || e.Id == runEntity);
                if (held != null && boostedTurn != game.Turn && game.Player1.RemainingMana < held.Cost) { boostedTurn = game.Turn; game.Player1.BaseMana = Math.Min(10, held.Cost); game.Player1.UsedMana = 0; options = game.Player1.Options(); }
                mine = options.OfType<PlayCardTask>().Where(o => o.Source.Card.Id == id || o.Source.Id == runEntity)
                    .OrderByDescending(o => o.Target != null && o.Target.Controller != game.Player1 ? 1 : 0).FirstOrDefault();
            }
            if (mine != null)
            {
                if (mine is HeroPowerTask) { powerUses++; if (playedTurn < 0) playedTurn = game.Turn; }
                else playedTurn = game.Turn;
                Act(mine, "player");
            }
            else Act(LocalBot.Choose(options), "player");
        }
    }
    string result = game.State != State.COMPLETE ? "turn-cap" : game.Player2.PlayState == PlayState.WON ? "boss" : game.Player1.PlayState == PlayState.WON ? "player" : "tie";
    var bossDala = game.HistoryFacts.Query.Plays().Where(f => f.ControllerId == game.Player2.Id && f.DefinitionKnown && Cards.FromAssetId(f.DbfId).Id.StartsWith("DALA_7"))
        .Select(f => Cards.FromAssetId(f.DbfId).Id).ToList();
    var mineDala = game.HistoryFacts.Query.Plays().Where(f => f.ControllerId == game.Player1.Id && f.DefinitionKnown && Cards.FromAssetId(f.DbfId).Id.StartsWith("DALA_7"))
        .Select(f => Cards.FromAssetId(f.DbfId).Id).ToList();
    return $"{result} turns {game.Turn} played-turn {playedTurn} power-uses {powerUses} decisions {decisions} fallbacks {fallbacks} boss-dala [{string.Join(",", bossDala)}] mine [{string.Join(",", mineDala)}] boss {boss.Id}";
}
// ---------------------------------------------------------------- T33 full games against the real boss AI, the player playing the id
string RowFor(string id) => id switch
{
    "DALA_719" or "DALA_723" or "DALA_705" or "DALA_721" or "DALA_717" or "DALA_702" or "DALA_703" => Chu,
    "DALA_700" or "DALA_712" or "DALA_715" or "DALA_726" => Rakanishu,
    "DALA_701" or "DALA_716" or "DALA_724" or "DALA_729" => Eudora,
    "DALA_709" or "DALA_718" => George,
    "DALA_704" or "DALA_711" or "DALA_727" => Squeamlish,
    "DALA_708" or "DALA_720" or "DALA_730" => Vessina,
    "DALA_713" or "DALA_725" => Barkeye,
    "DALA_714" => Kriziki,
    _ => Tekahn,
};
// F7_AI=<id> runs only that id's full game (a debugging aid; the suite's record runs every one).
string aiOnly = Environment.GetEnvironmentVariable("F7_AI");
foreach (string id in offeredF7)
{
    if (aiOnly != null && id != aiOnly) continue;
    string row = RowFor(id);
    string line;
    try { line = AiGame(id, 701, row); }
    catch (Exception e) { line = "FAULT " + e.GetType().Name + ": " + e.Message.Split('\n')[0]; }
    Console.WriteLine($"T33 {id,-10} {row,-16} {line}");
    Check($"T33 {id}: the player plays it in a full game against the boss AI, which ends without a fault or an AI fallback",
        !line.StartsWith("FAULT") && line.Contains("fallbacks 0 ") && !line.Contains("played-turn -1"), line);
}
// The Twinspell stages reach the hand only from a cast stage: a game in which the player holds THE... Candles? at each stage.
foreach (string stage in stages)
{
    string line;
    try { line = AiGame("DALA_714", 702, Kriziki, new[] { stage }); }
    catch (Exception e) { line = "FAULT " + e.GetType().Name + ": " + e.Message.Split('\n')[0]; }
    Console.WriteLine($"T33 {stage,-10} {Kriziki,-16} {line}");
    Check($"T33 {stage}: a full game with the stage in hand ends without a fault or an AI fallback", !line.StartsWith("FAULT") && line.Contains("fallbacks 0 "), line);
}
// ---------------------------------------------------------------- T34 Kadoom Bot drawn by the boss in a full game
foreach (long seed in new long[] { 711, 712 })
{
    string line;
    try { line = AiGame("DALA_716", seed, Eudora); }
    catch (Exception e) { line = "FAULT " + e.GetType().Name + ": " + e.Message.Split('\n')[0]; }
    Console.WriteLine($"T34 DALA_716/{seed} {line}");
    Check($"T34 DALA_716 ({seed}): Fly-By's Kadoom Bot in the boss's deck: a full game ends without a fault or fallback", !line.StartsWith("FAULT") && line.Contains("fallbacks 0 "), line);
}

Console.WriteLine($"{passes} passed, {failures} failed");
return failures == 0 ? 0 : 1;
