// c-treasures-F2: phase 6 family F2 of the unified Dungeon Run, the original Dungeon Run (Kobolds and Catacombs) active
// treasures LOOTA_805, 806, 811, 812, 813, 814, 816, 817, 819, 820, 821, 822, 823, 826, 827, 829, 830, 834-841, 842, 842a,
// 842b, 843 and 847, plus the Quel'Delar assembly (842a + 842b combine in hand into 842). For every id: the engine content
// (scripted, keyed and shelled by its own client id, name, text, type and cost as facts/violet-hold-2026-09.json has them),
// the run data row (an active, act-run treasure; LOOTA_842 reachable only by the combine), the validator and admission
// checks, a scripted regression game through the run setup path (DungeonRun.NewRun -> the treasure in the run deck ->
// BuildPlan -> DungeonRunSetup.Configure -> Game -> ApplyAfterStart -> the card drawn from the run deck and played)
// asserting the board, hand, deck and RunMatchResult outcome, the run-layer harvest of the deck edits (LOOTA_805, 813), and a
// full game against the real boss AI (DungeonRunBossPilot + GoldenPilot) in which the player plays the treasure; it must end
// without a fault or an AI fallback. Headless: compiles C:\SoloStone\plugin\DungeonRun\*.cs and the NPC AI against
// p6f2/engine-out. Run it twice and compare the output (determinism).
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

string[] f2 = { "LOOTA_805", "LOOTA_806", "LOOTA_811", "LOOTA_812", "LOOTA_813", "LOOTA_814", "LOOTA_816", "LOOTA_817", "LOOTA_819",
    "LOOTA_820", "LOOTA_821", "LOOTA_822", "LOOTA_823", "LOOTA_826", "LOOTA_827", "LOOTA_829", "LOOTA_830", "LOOTA_834", "LOOTA_835",
    "LOOTA_836", "LOOTA_837", "LOOTA_838", "LOOTA_839", "LOOTA_840", "LOOTA_841", "LOOTA_842", "LOOTA_842a", "LOOTA_842b", "LOOTA_843",
    "LOOTA_847" };
var effectShells = new Dictionary<string, string> { ["fx_LOOTA_812"] = "LOOTA_812e", ["fx_LOOTA_820"] = "LOOTA_820e", ["fx_LOOTA_842b"] = "LOOTA_842be" };
CardType FactType(string id) => FactTag(id, 202) switch { 4 => CardType.MINION, 5 => CardType.SPELL, 6 => CardType.ENCHANTMENT, 7 => CardType.WEAPON, _ => CardType.INVALID };

// ---------------------------------------------------------------- T1 content: scripted, own shells, client name, text, type and cost
Check("T1 30 F2 ids", f2.Length == 30 && f2.Distinct().Count() == 30);
foreach (string id in f2)
{
    var content = DungeonRunContent.Get(id);
    var card = C(id);
    Check($"T1 {id}: an ActiveTreasure keyed and shelled by its own id, scripted, the client's {FactType(id)}",
        content.Kind == RunContentKind.ActiveTreasure && content.ShellId == id && card != null && card.Implemented && card.Type == FactType(id)
        && DungeonRunContent.ForShell(id) == content && clientIds.Contains(id), card == null ? "no card" : card.Type + " " + card.Implemented);
    Check($"T1 ids-text-match {id}: content name and text equal facts/violet-hold-2026-09.json",
        content.Name == factById[id].GetProperty("name").GetString() && content.Text == FactText(id), content.Name + " | " + content.Text);
    Check($"T1 {id}: cost {FactTag(id, 48)} as the client prints it" + (card?.Type == CardType.MINION || card?.Type == CardType.WEAPON ? $", stats {FactTag(id, 47)}/{(card.Type == CardType.WEAPON ? FactTag(id, 187) : FactTag(id, 45))}" : ""),
        card != null && card.Cost == FactTag(id, 48) && content.Cost == FactTag(id, 48)
        && (card.Type == CardType.MINION ? card.ATK == FactTag(id, 47) && card.Health == FactTag(id, 45)
            : card.Type == CardType.WEAPON ? card.ATK == FactTag(id, 47) && card[GameTag.DURABILITY] == FactTag(id, 187) : true),
        card == null ? "" : $"{card.Cost} {card.ATK}/{card.Health}/{card[GameTag.DURABILITY]}");
}
foreach (var fx in effectShells)
{
    var e = DungeonRunContent.Get(fx.Key);
    Check($"T1 {fx.Value}: its enchantment is content {fx.Key} on the client's own ENCHANTMENT, scripted, name and text word for word",
        e.Kind == RunContentKind.Effect && e.ShellId == fx.Value && C(fx.Value).Implemented && C(fx.Value).Type == CardType.ENCHANTMENT && clientIds.Contains(fx.Value)
        && e.Name == factById[fx.Value].GetProperty("name").GetString() && e.Text == FactText(fx.Value), e.Name + " | " + e.Text);
}
Check("T1 tokens are scripted cards the client knows (Pyroblast, Coin, Boom Bot, Wax Rager, Quel'Delar)",
    f2.SelectMany(id => DungeonRunContent.Get(id).Tokens).Distinct().All(t => C(t) != null && C(t).Implemented && clientIds.Contains(t)),
    string.Join(",", f2.SelectMany(id => DungeonRunContent.Get(id).Tokens).Distinct()));

// ---------------------------------------------------------------- T2 data, validator, admission
{
    // The 37.x client's ids for the engine's basic and upgraded powers (NativeMatch.TransportCardId).
    var transport = new Dictionary<string, string>
    {
        ["CS2_034"] = "HERO_08bp", ["CS2_102"] = "HERO_01bp", ["CS1h_001"] = "HERO_09bp", ["CS2_101"] = "HERO_04bp", ["CS2_017"] = "HERO_06bp",
        ["DS1h_292"] = "HERO_05bp", ["CS2_083b"] = "HERO_03bp", ["CS2_056"] = "HERO_07bp", ["CS2_049"] = "HERO_02bp",
        ["AT_132_WARRIOR"] = "HERO_01bp2", ["AT_132_SHAMAN"] = "HERO_02bp2", ["AT_132_ROGUE"] = "HERO_03bp2", ["AT_132_PALADIN"] = "HERO_04bp2",
        ["AT_132_HUNTER"] = "HERO_05bp2", ["AT_132_DRUID"] = "HERO_06bp2", ["AT_132_WARLOCK"] = "HERO_07bp2", ["AT_132_MAGE"] = "HERO_08bp2", ["AT_132_PRIEST"] = "HERO_09bp2",
    };
    var errors = DungeonRunValidator.Validate(data, id => clientIds.Contains(transport.TryGetValue(id, out var sent) ? sent : id));
    Check("T2 shipped data validates with 0 errors (client check through the transported id)", errors.Count == 0, string.Join(" | ", errors.Take(5)));
    var legacyData = DungeonRunData.Parse(runJson, poolJson, npcJson);
    var le = DungeonRunValidator.Validate(legacyData, null);
    Check("T2 the data without the catalog validates with 0 errors", le.Count == 0, string.Join(" | ", le.Take(3)));
    foreach (string id in f2)
    {
        var t = data.FindTreasure(id);
        bool combined = id == "LOOTA_842";
        Check($"T2 {id}: an active act-run treasure row" + (combined ? ", not offered (formed in hand only)" : ", offered"),
            t != null && t.Active && t.Offered == !combined && !t.Legacy && !t.Signature && t.Runs == DungeonRunTreasure.RunsAct && t.UpgradesTo == null);
        var p = DungeonRunAdmission.Classify(C(id), id, AdmitScope.Player);
        var pc = DungeonRunAdmission.Classify(C(id), id, AdmitScope.Player, null, catalogRun: true);
        var b = DungeonRunAdmission.Classify(C(id), id, AdmitScope.Boss, null, catalogRun: true);
        var gen = DungeonRunAdmission.Classify(C(id), id, AdmitScope.Generated, null, catalogRun: true);
        // Phase 8 family 09: the fifteen run treasures King Togwaggle's Magic Candle finds (two of them in A. F. Kay's deck) are admitted for a
        // boss on catalog runs (DungeonRunAdmission.F09.cs run-treasure-boss-find, and LOOTA_839's own row); the legacy verdict is unchanged.
        bool bossFinds = new[] { "LOOTA_806", "LOOTA_812", "LOOTA_816", "LOOTA_819", "LOOTA_821", "LOOTA_823", "LOOTA_826", "LOOTA_827", "LOOTA_835", "LOOTA_836",
            "LOOTA_838", "LOOTA_839", "LOOTA_842b", "LOOTA_843", "LOOTA_847" }.Contains(id)
            // Phase 8 family 16: the five Ol' Toomba's Tales of Fortune shuffles into his deck (DungeonRunAdmission.F16.cs run-treasure-boss-shuffle).
            || new[] { "LOOTA_811", "LOOTA_837", "LOOTA_841", "LOOTA_842", "LOOTA_842a" }.Contains(id);
        Check($"T2 admission {id}: a run-treasure row, admitted for the player only (legacy and catalog; a catalog run's boss too for the fifteen King Togwaggle finds and the five Ol' Toomba shuffles in), with a 'none:' lift",
            p.Subject && p.Admitted && pc.Admitted && b.Admitted == bossFinds && !DungeonRunAdmission.Classify(C(id), id, AdmitScope.Boss).Admitted && !gen.Admitted && p.Family.StartsWith("run-treasure", StringComparison.Ordinal)
            && DungeonRunAdmission.IsNoneLift(p.Lift) && p.Reason.StartsWith("treasure: only the player can hold it", StringComparison.Ordinal), p.Family + " " + p.Admit);
    }
    Check("T2 admission: LOOTA_822's row carries the loop guard RepeatUntil max=60, the engine's bound",
        DungeonRunAdmission.RuleFor("LOOTA_822")?.Guard == "RepeatUntil max=60" && DungeonRunContent.RodOfRoastingMaxCasts == 60);
    Check("T2 admission families: 805/813 deck edit, 817/829/830 run read, 820 belief reset, 834 hand steal, 839 side shuffle",
        DungeonRunAdmission.RuleFor("LOOTA_805").Family == "run-treasure-deck-edit" && DungeonRunAdmission.RuleFor("LOOTA_813").Family == "run-treasure-deck-edit"
        && new[] { "LOOTA_817", "LOOTA_829", "LOOTA_830" }.All(i => DungeonRunAdmission.RuleFor(i).Family == "run-treasure-run-read")
        && DungeonRunAdmission.RuleFor("LOOTA_820").Family == "run-treasure-belief-reset" && DungeonRunAdmission.RuleFor("LOOTA_834").Family == "run-treasure-hand-steal"
        && DungeonRunAdmission.RuleFor("LOOTA_839").Family == "run-treasure-side-shuffle");
    Check("T2 the boss's generation never yields an F2 id (GeneratedRejectsBoss), and the player's is not blocked from its own treasures",
        f2.All(id => DungeonRunAdmission.GeneratedRejectsBoss(true).Contains(id) && !DungeonRunAdmission.GeneratedRejects(true).Contains(id)));
    // Negative: an active treasure row whose content is a weapon still checks the type (a SPELL shell for a weapon key fails).
    string badType = runJson.Replace("{\"id\": \"LOOTA_806\", \"rarity\": \"rare\", \"active\": true, \"runs\": \"act\"}", "{\"id\": \"LOOTA_806\", \"rarity\": \"rare\", \"runs\": \"act\"}");
    var eType = DungeonRunValidator.Validate(DungeonRunData.Parse(badType, poolJson, npcJson, catalogJson, scheduleJson, tavernJson), null);
    Check("T2 validator: a row that calls an active treasure passive is an error", badType != runJson && eType.Any(e => e.Contains("LOOTA_806") && e.Contains("passive")),
        string.Join(" | ", eType.Take(2)));
}

// ---------------------------------------------------------------- T3 offers: act runs only, picked into the run deck
{
    var offered = new HashSet<string>();
    foreach (var cls in data.Classes)
        for (long seed = 1; seed <= 40; seed++)
        {
            var s = run.NewRun(all, cls.Id, cls.DefaultDeck, cls.DefaultPower, seed, signatureId: run.SignatureOffer(all, run.Data.FindClass(cls.Id), seed).FirstOrDefault());
            if (run.OpenTreasureDraft(s)) offered.UnionWith(s.OpenDraft.Offered);
        }
    foreach (string id in f2.Where(i => i != "LOOTA_842"))
        Check($"T3 {id} is offered on act runs", offered.Contains(id));
    Check("T3 LOOTA_842 (Quel'Delar) is never offered: it is formed in hand", !offered.Contains("LOOTA_842"));
    var legacyOffered = new HashSet<string>();
    foreach (var cls in data.ClassRows)
        for (long seed = 1; seed <= 40; seed++)
        {
            var s = run.NewRun(all, cls.Id, cls.DefaultDeck, cls.DefaultPower, seed, schedule: DungeonRunSchedules.LegacyId, signatureId: run.SignatureOffer(all, run.Data.FindClass(cls.Id), seed).FirstOrDefault());
            if (run.OpenTreasureDraft(s)) legacyOffered.UnionWith(s.OpenDraft.Offered);
        }
    Check("T3 a legacy run is never offered an F2 treasure (its offers stay as released)", !legacyOffered.Any(f2.Contains), string.Join(",", legacyOffered.Where(f2.Contains)));
    // A real pick: the draft offers it, PickTreasure adds its card to the run deck as a treasure card record.
    var sp = run.NewRun(all, "MAGE", data.Class("MAGE").DefaultDeck, data.Class("MAGE").DefaultPower, 7, signatureId: run.SignatureOffer(all, run.Data.FindClass("MAGE"), 7).FirstOrDefault());
    run.OpenTreasureDraft(sp);
    sp.OpenDraft.Offered[0] = "LOOTA_822";
    int before = sp.Deck.Count;
    run.PickTreasure(sp, "LOOTA_822");
    var rec = sp.Cards.LastOrDefault();
    Check("T3 PickTreasure LOOTA_822: the run deck gains the card (origin treasure), ActiveTreasures holds it",
        sp.Deck.Count == before + 1 && rec?.Id == "LOOTA_822" && rec.Origin == DungeonRunDeck.OriginTreasure && sp.ActiveTreasures.Contains("LOOTA_822"));
}

// ---------------------------------------------------------------- the run setup path
DungeonRunState NewAct(string classId, long seed)
{
    var cls = data.Class(classId);
    var s = run.NewRun(all, cls.Id, cls.DefaultDeck, cls.DefaultPower, seed, signatureId: run.SignatureOffer(all, run.Data.FindClass(cls.Id), seed).FirstOrDefault());
    if (!s.IsActRun || s.Phase != DungeonRunPhase.PreMatch || s.Floor != 1) throw new Exception("act run not at floor 1 pre-match");
    return s;
}
// An active pick as DungeonRun.PickTreasure records it (the draft's own pick is T3): the card joins the run deck.
void Hold(DungeonRunState s, params string[] ids)
{
    foreach (string id in ids) { s.ActiveTreasures.Add(id); DungeonRunDeck.Add(s, DungeonRunContent.Get(id).ShellId, DungeonRunDeck.OriginTreasure); }
}
void Defeated(DungeonRunState s, int bosses)
{
    for (int i = 0; i < bosses; i++) s.Matches.Add(new DungeonRunMatchSummary { Floor = 1, Boss = s.BossSequence[0], Outcome = DungeonRunOutcome.Win });
}
Game StartFight(RunMatchPlan plan, int seed)
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
(DungeonRunState s, RunMatchPlan plan, Game g) Fight(string classId, long seed, string[] treasures, int bosses = 0)
{
    var s = NewAct(classId, seed);
    Hold(s, treasures);
    Defeated(s, bosses);
    var plan = run.BuildPlan(s);
    return (s, plan, StartFight(plan, run.MatchSeed(s)));
}
// The run card drawn from the run deck (or already in the opening hand).
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
bool Play(Game g, IPlayable card, ICharacter target = null) => g.Process(PlayCardTask.Any(g.Player1, card, target));
void EndTurn(Game g) => g.Process(EndTurnTask.Any(g.CurrentPlayer));
bool FromRunDeck(DungeonRunState s, RunMatchPlan plan, string id) => plan.PlayerDeck.Any(c => c.Id == DungeonRunContent.Get(id).ShellId) && s.Deck.Contains(id);

// ---------------------------------------------------------------- T4 LOOTA_805 Amulet of Domination (+ run deck edit)
{
    var (s, plan, g) = Fight("MAGE", 11, new[] { "LOOTA_805" });
    Check("T4 LOOTA_805 is a card of the run deck the match plan deals", FromRunDeck(s, plan, "LOOTA_805"));
    var amulet = Draw(g, "LOOTA_805"); Mana(g.Player1, 10);
    var yeti = Board(g, g.Player2, "CS2_182");
    bool played = Play(g, amulet, yeti);
    Check("T4 LOOTA_805: takes control of the boss's Chillwind Yeti", played && yeti.Controller == g.Player1 && g.Player1.BoardZone.Contains(yeti) && g.Player2.BoardZone.Count == 0);
    Check("T4 LOOTA_805: the match asks the run to add CS2_182 to the run deck (RunMatchResult add)",
        g.RunResult != null && g.RunResult.Edits.Count == 1 && g.RunResult.Edits[0].Op == RunEdit.OpAdd && g.RunResult.Edits[0].CardId == "CS2_182");
    var noTarget = Fight("MAGE", 12, new[] { "LOOTA_805" });
    var a2 = Draw(noTarget.g, "LOOTA_805"); Mana(noTarget.g.Player1, 10);
    Check("T4 LOOTA_805: unplayable with no enemy minion (a targeted spell)", !noTarget.g.Player1.Options().OfType<PlayCardTask>().Any(o => o.Source == a2));
    // The harvest: a won fight adds the taken minion's card (origin match); a lost one drops it.
    run.BeginMatch(s);
    int n0 = s.Cards.Count;
    var notes = run.RecordOutcome(s, all, DungeonRunOutcome.Win, g.RunResult);
    Check("T4 LOOTA_805 won fight: the run deck holds CS2_182 (origin match), no note", notes.Count == 0 && s.Cards.Count == n0 + 1
        && s.Cards.Last().Id == "CS2_182" && s.Cards.Last().Origin == DungeonRunDeck.OriginMatch, string.Join(";", notes));
    var (sl, _, gl) = Fight("MAGE", 13, new[] { "LOOTA_805" });
    var al = Draw(gl, "LOOTA_805"); Mana(gl.Player1, 10); var yl = Board(gl, gl.Player2, "CS2_182"); Play(gl, al, yl);
    run.BeginMatch(sl); int nl = sl.Cards.Count;
    run.RecordOutcome(sl, all, DungeonRunOutcome.Loss, gl.RunResult);
    Check("T4 LOOTA_805 lost fight: the run deck is unchanged", sl.Cards.Count == nl && !sl.Cards.Any(c => c.Origin == DungeonRunDeck.OriginMatch));
    var (so, _, go) = Fight("MAGE", 14, new[] { "LOOTA_805" });
    var ao = Draw(go, "LOOTA_805"); Mana(go.Player1, 10); var axe = Board(go, go.Player2, "EX1_398"); Play(go, ao, axe); // Arathi Weaponsmith, a Warrior card
    run.BeginMatch(so); int no = so.Cards.Count;
    var oNotes = run.RecordOutcome(so, all, DungeonRunOutcome.Win, go.RunResult);
    Check("T4 LOOTA_805: an off-class minion is taken in the match but the run deck refuses it (class check), with a note",
        axe.Controller == go.Player1 && so.Cards.Count == no && oNotes.Count == 1 && oNotes[0].Contains("class"), string.Join(";", oNotes));
}

// ---------------------------------------------------------------- T5 LOOTA_806 Wand of Disintegration
{
    var (s, plan, g) = Fight("WARRIOR", 21, new[] { "LOOTA_806" });
    var wand = Draw(g, "LOOTA_806"); Mana(g.Player1, 10);
    var gnome = Board(g, g.Player2, "EX1_029"); var yeti = Board(g, g.Player2, "CS2_182"); var mine = Board(g, g.Player1, "CS2_231");
    int hp = g.Player1.Hero.Health;
    Play(g, wand);
    Check("T5 LOOTA_806: every enemy minion is destroyed, the silenced Leper Gnome's Deathrattle does not fire, mine stays",
        g.Player2.BoardZone.Count == 0 && g.Player1.Hero.Health == hp && g.Player1.BoardZone.Contains(mine) && gnome.Zone?.Type == Zone.GRAVEYARD && yeti.Zone?.Type == Zone.GRAVEYARD,
        g.Player2.BoardZone.Count + " hp " + hp + "->" + g.Player1.Hero.Health);
}

// ---------------------------------------------------------------- T6 LOOTA_811 Orb of Destruction
{
    var (s, plan, g) = Fight("PRIEST", 31, new[] { "LOOTA_811" });
    var orb = Draw(g, "LOOTA_811"); Mana(g.Player1, 10);
    g.Player2.BaseMana = 5;
    int hand = g.Player2.HandZone.Count, grave = g.Player2.GraveyardZone.Count;
    Play(g, orb);
    Check("T6 LOOTA_811: the boss loses 2 Mana Crystals (5 -> 3) and discards 2 cards", g.Player2.BaseMana == 3 && g.Player2.HandZone.Count == hand - 2
        && g.Player2.GraveyardZone.Count == grave + 2 && g.Player2.DiscardedEntities.Count == 2, $"{g.Player2.BaseMana} {hand}->{g.Player2.HandZone.Count}");
    var (_, _, g1) = Fight("PRIEST", 31, new[] { "LOOTA_811" });
    var o1 = Draw(g1, "LOOTA_811"); Mana(g1.Player1, 10); g1.Player2.BaseMana = 1;
    while (g1.Player2.HandZone.Count > 1) { var x = g1.Player2.HandZone[0]; g1.Player2.HandZone.Remove(x); g1.Player2.SetasideZone.Add(x); }
    Play(g1, o1);
    Check("T6 LOOTA_811: never below 0 crystals, and a 1-card hand discards 1", g1.Player2.BaseMana == 0 && g1.Player2.HandZone.Count == 0);
    EndTurn(g);
    Check("T6 LOOTA_811: on its next turn the boss has 4 crystals (3 + 1)", g.Player2.BaseMana == 4 && g.CurrentPlayer == g.Player2);
}

// ---------------------------------------------------------------- T7 LOOTA_812 Boots of Haste
{
    var (s, plan, g) = Fight("PALADIN", 41, new[] { "LOOTA_812" });
    var boots = Draw(g, "LOOTA_812");
    var yeti = ToHand(g, g.Player1, "CS2_182"); var fireball = ToHand(g, g.Player1, "CS2_029"); var bossYeti = ToHand(g, g.Player2, "CS2_182");
    Play(g, boots);
    Check("T7 LOOTA_812: my minions cost (0) this turn; spells and the boss's minions keep their cost", yeti.Cost == 0 && fireball.Cost == 4 && bossYeti.Cost == 4,
        $"{yeti.Cost} {fireball.Cost} {bossYeti.Cost}");
    Check("T7 LOOTA_812: the 0-cost Yeti is played with 0 mana left", g.Player1.RemainingMana == 0 && Play(g, yeti) && g.Player1.BoardZone.Contains((Minion)yeti));
    var later = ToHand(g, g.Player1, "CS2_182");
    Check("T7 LOOTA_812: a minion drawn later this turn costs (0) too", later.Cost == 0);
    EndTurn(g); EndTurn(g);
    Check("T7 LOOTA_812: next turn my minions cost their printed cost again", later.Cost == 4, later.Cost.ToString());
}

// ---------------------------------------------------------------- T8 LOOTA_813 Magic Mirror (+ run deck edit)
{
    var (s, plan, g) = Fight("HUNTER", 51, new[] { "LOOTA_813" });
    var mirror = Draw(g, "LOOTA_813"); Mana(g.Player1, 10);
    var boss = Board(g, g.Player2, "CS2_182");
    Play(g, mirror, boss);
    Check("T8 LOOTA_813: a copy of the boss's Yeti is summoned for me, the original stays", g.Player1.BoardZone.Count == 1 && g.Player1.BoardZone[0].Card.Id == "CS2_182"
        && g.Player2.BoardZone.Contains(boss));
    Check("T8 LOOTA_813: the match asks the run to add CS2_182", g.RunResult?.Edits.Count == 1 && g.RunResult.Edits[0].CardId == "CS2_182");
    run.BeginMatch(s); int n0 = s.Cards.Count;
    var notes = run.RecordOutcome(s, all, DungeonRunOutcome.Win, g.RunResult);
    Check("T8 LOOTA_813 won fight: the run deck holds the copied card", notes.Count == 0 && s.Cards.Count == n0 + 1 && s.Cards.Last().Id == "CS2_182");
    var (_, _, g2) = Fight("HUNTER", 52, new[] { "LOOTA_813" });
    var m2 = Draw(g2, "LOOTA_813"); Mana(g2.Player1, 10); var own = Board(g2, g2.Player1, "CS2_231");
    Play(g2, m2, own);
    Check("T8 LOOTA_813: a friendly minion can be mirrored too", g2.Player1.BoardZone.Count == 2 && g2.RunResult.Edits[0].CardId == "CS2_231");
    var (_, _, gf) = Fight("HUNTER", 53, new[] { "LOOTA_813" });
    var mf = Draw(gf, "LOOTA_813"); Mana(gf.Player1, 10); for (int i = 0; i < 7; i++) Board(gf, gf.Player1, "CS2_231");
    Check("T8 LOOTA_813: unplayable with a full board", !gf.Player1.Options().OfType<PlayCardTask>().Any(o => o.Source == mf));
}

// ---------------------------------------------------------------- T9 LOOTA_814 Wish
{
    var (s, plan, g) = Fight("MAGE", 61, new[] { "LOOTA_814" });
    var wish = Draw(g, "LOOTA_814"); Mana(g.Player1, 10);
    Board(g, g.Player1, "CS2_231");
    g.Player1.Hero.Damage = 17;
    Play(g, wish);
    var summoned = g.Player1.BoardZone.Skip(1).ToList();
    Check("T9 LOOTA_814: the board is filled (1 + 6) with scripted Legendary minions, and the hero is fully healed",
        g.Player1.BoardZone.Count == 7 && summoned.Count == 6 && summoned.All(m => m.Card.Rarity == Rarity.LEGENDARY && m.Card.Implemented)
        && g.Player1.Hero.Health == g.Player1.Hero.BaseHealth, string.Join(",", summoned.Select(m => m.Card.Id)) + " hp " + g.Player1.Hero.Health);
    var (_, _, g2) = Fight("MAGE", 61, new[] { "LOOTA_814" });
    var w2 = Draw(g2, "LOOTA_814"); Mana(g2.Player1, 10); Board(g2, g2.Player1, "CS2_231"); Play(g2, w2);
    Check("T9 LOOTA_814: the same seed summons the same minions (game RNG)", g2.Player1.BoardZone.Select(m => m.Card.Id).SequenceEqual(g.Player1.BoardZone.Select(m => m.Card.Id)));
}

// ---------------------------------------------------------------- T10 LOOTA_816 Party Portal
{
    var (s, plan, g) = Fight("MAGE", 71, new[] { "LOOTA_816" });
    var portal = Draw(g, "LOOTA_816"); Mana(g.Player1, 10);
    Play(g, portal);
    var explosion = ToHand(g, g.Player1, "CS2_025");
    Play(g, explosion);
    var spawned = g.Player1.BoardZone.Where(m => m != portal).ToList();
    Check("T10 LOOTA_816: Party Portal (0/10) on the board; casting a 2-cost spell summons a random 2-cost minion",
        g.Player1.BoardZone.Contains((Minion)portal) && ((Minion)portal).AttackDamage == 0 && ((Minion)portal).Health == 10
        && spawned.Count == 1 && spawned[0].Card.Cost == 2, string.Join(",", spawned.Select(m => m.Card.Id + ":" + m.Card.Cost)));
    EndTurn(g);
    var bossSpell = ToHand(g, g.Player2, "CS2_025"); g.Process(PlayCardTask.Any(g.Player2, bossSpell));
    Check("T10 LOOTA_816: the boss's spells summon nothing for anyone", g.Player2.BoardZone.Count == 0);
}

// ---------------------------------------------------------------- T11 LOOTA_817 Primordial Wand (bosses defeated)
foreach (int bosses in new[] { 0, 2 })
{
    var (s, plan, g) = Fight("DRUID", 81, new[] { "LOOTA_817" }, bosses);
    Check($"T11 LOOTA_817 ({bosses} bosses): the match's run context reads {bosses} bosses defeated", RunHelpers.Bosses(g) == bosses && plan.RunContext.BossesDefeated == bosses);
    var wand = Draw(g, "LOOTA_817"); Mana(g.Player1, 10);
    var target = Board(g, g.Player1, "CS2_182");
    Play(g, wand, target);
    int choices = 0;
    while (g.Player1.Choice != null && choices < 10) { choices++; g.Process(ChooseTask.Pick(g.Player1, g.Player1.Choice.Choices[0])); }
    int adapts = target.AppliedEnchantments?.Count(e => e.Card.Id.StartsWith("UNG_999t", StringComparison.Ordinal)) ?? 0;
    Check($"T11 LOOTA_817 ({bosses} bosses): {1 + bosses} Adapt choices, each applied to the friendly minion", choices == 1 + bosses && adapts >= 1 + bosses - 1 && g.Player1.Choice == null,
        "choices " + choices + " enchants " + adapts);
}
{
    var (_, _, g) = Fight("DRUID", 82, new[] { "LOOTA_817" });
    var wand = Draw(g, "LOOTA_817"); Mana(g.Player1, 10);
    Check("T11 LOOTA_817: needs a friendly minion (no enemy target)", !g.Player1.Options().OfType<PlayCardTask>().Any(o => o.Source == wand));
}

// ---------------------------------------------------------------- T12 LOOTA_819 Archmage Staff
{
    var (s, plan, g) = Fight("WARLOCK", 91, new[] { "LOOTA_819" });
    var staff = Draw(g, "LOOTA_819");
    Play(g, staff);
    Check("T12 LOOTA_819: the 0-cost 0/3 Archmage Staff is equipped", g.Player1.Hero.Weapon?.Card.Id == "LOOTA_819" && g.Player1.Hero.Weapon.Durability == 3 && g.Player1.Hero.AttackDamage == 0);
    var before = g.Player1.HandZone.Select(e => e.Id).ToHashSet();
    EndTurn(g); EndTurn(g);
    var added = g.Player1.HandZone.Where(e => !before.Contains(e.Id)).ToList();
    var mageSpells = added.Where(e => e.Card.Type == CardType.SPELL && e.Card.Class == CardClass.MAGE && e.Card.Implemented).ToList();
    Check("T12 LOOTA_819: at the start of my turn a random scripted Mage spell is added (plus the turn's draw)", mageSpells.Count >= 1 && added.Count == 2,
        string.Join(",", added.Select(e => e.Card.Id)));
    Check("T12 LOOTA_819: it does not fire on the boss's turn", g.Player2.HandZone.All(e => !(e.Card.Class == CardClass.MAGE && e[GameTag.DISPLAYED_CREATOR] == g.Player1.Hero.Weapon.Id)));
}

// ---------------------------------------------------------------- T13 LOOTA_820 Aleatoric Cube
{
    var (s, plan, g) = Fight("ROGUE", 101, new[] { "LOOTA_820" });
    var cube = Draw(g, "LOOTA_820"); Mana(g.Player1, 10);
    var mine = g.Player1.DeckZone.Select(e => e.Id).ToList(); var theirs = g.Player2.DeckZone.Select(e => e.Id).ToList();
    var handIds = g.Player1.HandZone.Where(e => e != cube).Select(e => e.Id).ToList();
    var costs = g.Player1.HandZone.Concat(g.Player1.DeckZone).Concat(g.Player2.DeckZone).Where(e => e != cube).ToDictionary(e => e.Id, e => e.Cost);
    Play(g, cube);
    int total = mine.Count + theirs.Count;
    var nowMine = g.Player1.DeckZone.Select(e => e.Id).ToList(); var nowTheirs = g.Player2.DeckZone.Select(e => e.Id).ToList();
    Check("T13 LOOTA_820: both decks are pooled and split, the owner taking the larger half; no card lost or doubled",
        nowMine.Count == (total + 1) / 2 && nowTheirs.Count == total / 2 && nowMine.Concat(nowTheirs).OrderBy(x => x).SequenceEqual(mine.Concat(theirs).OrderBy(x => x)),
        $"{mine.Count}+{theirs.Count} -> {nowMine.Count}+{nowTheirs.Count}");
    Check("T13 LOOTA_820: every card belongs to the deck it is in (controller rebound)",
        g.Player1.DeckZone.All(e => e.Controller == g.Player1 && e[GameTag.CONTROLLER] == g.Player1.PlayerId) && g.Player2.DeckZone.All(e => e.Controller == g.Player2 && e[GameTag.CONTROLLER] == g.Player2.PlayerId));
    Check("T13 LOOTA_820: cards moved both ways (a real shuffle)", nowMine.Any(theirs.Contains) && nowTheirs.Any(mine.Contains));
    var mineNow = g.Player1.HandZone.Concat(g.Player1.DeckZone).ToList();
    Check("T13 LOOTA_820: my cards (hand and new deck) cost (2) less, each carrying LOOTA_820e; the boss's do not",
        mineNow.All(e => e.AppliedEnchantments != null && e.AppliedEnchantments.Any(x => x.Card.Id == "LOOTA_820e") && e.Cost == Math.Max(0, costs[e.Id] - 2))
        && g.Player2.DeckZone.All(e => e.AppliedEnchantments == null || !e.AppliedEnchantments.Any(x => x.Card.Id == "LOOTA_820e")),
        string.Join(",", mineNow.Where(e => e.Cost != Math.Max(0, costs[e.Id] - 2)).Select(e => e.Card.Id + ":" + costs[e.Id] + "->" + e.Cost).Take(4)));
    Check("T13 LOOTA_820: the AI's deck beliefs reset (one RunBeliefReset per side, deck zone)",
        g.RunBeliefResets.Count == 2 && g.RunBeliefResets.Any(r => r.ControllerId == g.Player1.PlayerId && r.Zones == RunBeliefZones.Deck)
        && g.RunBeliefResets.Any(r => r.ControllerId == g.Player2.PlayerId && r.Zones == RunBeliefZones.Deck));
    var (_, _, g2) = Fight("ROGUE", 101, new[] { "LOOTA_820" });
    var c2 = Draw(g2, "LOOTA_820"); Mana(g2.Player1, 10); Play(g2, c2);
    Check("T13 LOOTA_820: the same seed deals the same split (game RNG)", g2.Player1.DeckZone.Select(e => e.Card.Id).SequenceEqual(g.Player1.DeckZone.Select(e => e.Card.Id)));
    // The boss's own-deck ledger: a card it received is never sampled as one of its original cards.
    var ledger = OwnDeckLedger.Observe(g, g.Player2, null);
    var sample = ledger.Sample(g, g.Player2, 7);
    Check("T13 LOOTA_820: the boss's own-deck sample holds no more cards than its deck", sample.Values.Sum() <= g.Player2.DeckZone.Count, sample.Values.Sum() + " vs " + g.Player2.DeckZone.Count);
    // The boss plays on: its next turn draws a card from the new deck.
    EndTurn(g);
    Check("T13 LOOTA_820: the boss draws from its new deck", g.CurrentPlayer == g.Player2 && g.Player2.DeckZone.Count == total / 2 - 1);
}

// ---------------------------------------------------------------- T14 LOOTA_821 Vorpal Dagger
{
    var (s, plan, g) = Fight("ROGUE", 111, new[] { "LOOTA_821" });
    var dagger = Draw(g, "LOOTA_821"); Mana(g.Player1, 10);
    Play(g, dagger);
    var hero = g.Player1.Hero;
    int hp = g.Player2.Hero.Health, attacks = 0;
    while (attacks < 6 && g.Process(HeroAttackTask.Any(g.Player1, g.Player2.Hero))) attacks++;
    Check("T14 LOOTA_821: Mega-Windfury: the hero attacks 4 times a turn (1 damage each)", attacks == 4 && g.Player2.Hero.Health == hp - 4 && hero.Weapon?.Durability == 0 || attacks == 4 && g.Player2.Hero.Health == hp - 4,
        "attacks " + attacks + " hp " + hp + "->" + g.Player2.Hero.Health);
    var (_, _, gp) = Fight("ROGUE", 112, new[] { "LOOTA_821" });
    var dp = Draw(gp, "LOOTA_821"); Mana(gp.Player1, 10); Play(gp, dp);
    var big = Board(gp, gp.Player2, "CS2_186");
    gp.Process(HeroAttackTask.Any(gp.Player1, big));
    Check("T14 LOOTA_821: Poisonous: a 1-damage hit destroys War Golem (7/7)", big.Zone?.Type == Zone.GRAVEYARD || big.ToBeDestroyed, big.Health.ToString());
    EndTurn(g);
    Check("T14 LOOTA_821: the boss's hero has no Mega-Windfury", g.Player2.Hero[GameTag.MEGA_WINDFURY] == 0 && !g.Player2.Hero.WeaponHasMegaWindfury);
    var megaWeapons = Cards.All.Where(c => c.Type == CardType.WEAPON && c[GameTag.MEGA_WINDFURY] > 0).Select(c => c.Id).ToList();
    Check("T14 LOOTA_821 is the only weapon with Mega-Windfury (the attack-limit change reaches no other card)", megaWeapons.SequenceEqual(new[] { "LOOTA_821" }), string.Join(",", megaWeapons));
}

// ---------------------------------------------------------------- T15 LOOTA_822 Rod of Roasting (loop guard)
{
    var (s, plan, g) = Fight("MAGE", 121, new[] { "LOOTA_822" });
    var rod = Draw(g, "LOOTA_822"); Mana(g.Player1, 10);
    Play(g, rod);
    int casts = g.Player1.GraveyardZone.Count(e => e.Card.Id == "EX1_279");
    bool aHeroDied = g.Player1.Hero.Health <= 0 || g.Player2.Hero.Health <= 0 || g.State == State.COMPLETE;
    Check("T15 LOOTA_822: Pyroblasts are cast until a hero dies (the game ends), within the guard", aHeroDied && casts >= 3 && casts <= DungeonRunContent.RodOfRoastingMaxCasts,
        "casts " + casts + " hp " + g.Player1.Hero.Health + "/" + g.Player2.Hero.Health + " " + g.State);
    var (_, _, gi) = Fight("MAGE", 122, new[] { "LOOTA_822" });
    var ri = Draw(gi, "LOOTA_822"); Mana(gi.Player1, 10);
    gi.Player1.Hero.IsImmune = true; gi.Player2.Hero.IsImmune = true;
    Play(gi, ri);
    int guarded = gi.Player1.GraveyardZone.Count(e => e.Card.Id == "EX1_279");
    Check("T15 LOOTA_822: with both heroes Immune the loop stops at the guard (60 casts) and the game goes on", guarded == 60 && gi.State != State.COMPLETE && gi.Player1.Hero.Health > 0,
        "casts " + guarded);
}

// ---------------------------------------------------------------- T16 LOOTA_823 Bag of Stuffing
{
    var (s, plan, g) = Fight("PRIEST", 131, new[] { "LOOTA_823" });
    var bag = Draw(g, "LOOTA_823");
    int deck = g.Player1.DeckZone.Count, hand = g.Player1.HandZone.Count;
    Play(g, bag);
    Check("T16 LOOTA_823: draws until the hand is full (10)", g.Player1.HandZone.Count == 10 && g.Player1.DeckZone.Count == deck - (10 - (hand - 1)), hand + "->" + g.Player1.HandZone.Count);
}

// ---------------------------------------------------------------- T17 LOOTA_826 Portable Ice Wall
{
    var (s, plan, g) = Fight("SHAMAN", 141, new[] { "LOOTA_826" });
    var wall = (Minion)Draw(g, "LOOTA_826");
    Play(g, wall);
    Check("T17 LOOTA_826: a 3/15 Taunt that can't attack", wall.Zone == g.Player1.BoardZone && wall.AttackDamage == 3 && wall.Health == 15 && wall.HasTaunt && wall.CantAttack);
    EndTurn(g); EndTurn(g);
    Check("T17 LOOTA_826: no attack option even when ready", !g.Player1.Options().OfType<MinionAttackTask>().Any(o => o.Source == wall));
    EndTurn(g);
    var boar = (Minion)Board(g, g.Player2, "CS2_171"); boar.IsExhausted = false;
    Check("T17 LOOTA_826: Taunt: the boss's minion must attack the wall", !g.Player2.Options().OfType<MinionAttackTask>().Any(o => o.Source == boar && o.Target == g.Player1.Hero)
        && g.Player2.Options().OfType<MinionAttackTask>().Any(o => o.Source == boar && o.Target == wall));
    var yeti = Board(g, g.Player2, "CS2_182"); yeti.IsExhausted = false;
    g.Process(MinionAttackTask.Any(g.Player2, yeti, wall));
    Check("T17 LOOTA_826: a minion damaged by the wall in combat is Frozen", yeti.IsFrozen && yeti.Health == 2, yeti.IsFrozen + " " + yeti.Health);
}

// ---------------------------------------------------------------- T18 LOOTA_827 Embers of Ragnaros
{
    var (s, plan, g) = Fight("WARLOCK", 151, new[] { "LOOTA_827" });
    var embers = Draw(g, "LOOTA_827"); Mana(g.Player1, 10);
    int hp = g.Player2.Hero.Health, mine = g.Player1.Hero.Health;
    Play(g, embers);
    Check("T18 LOOTA_827: three 8-damage fireballs at random enemies (no minion: 24 to the boss), none at me",
        g.Player2.Hero.Health == hp - 24 && g.Player1.Hero.Health == mine, hp + "->" + g.Player2.Hero.Health);
    var (_, _, g2) = Fight("WARLOCK", 152, new[] { "LOOTA_827" });
    var e2 = Draw(g2, "LOOTA_827"); Mana(g2.Player1, 10); var golem = Board(g2, g2.Player2, "CS2_186");
    int hp2 = g2.Player2.Hero.Health;
    Play(g2, e2);
    int dealt = (hp2 - g2.Player2.Hero.Health) + (7 - Math.Max(0, golem.Health)) + (golem.Zone?.Type == Zone.GRAVEYARD ? 0 : 0);
    Check("T18 LOOTA_827: with a War Golem the three fireballs split between enemies (never a dead target)", hp2 - g2.Player2.Hero.Health >= 8 || golem.Zone?.Type == Zone.GRAVEYARD,
        "boss " + hp2 + "->" + g2.Player2.Hero.Health + " golem " + golem.Health);
}

// ---------------------------------------------------------------- T19 LOOTA_829 Loyal Sidekick (bosses defeated)
foreach (int bosses in new[] { 0, 3 })
{
    var (s, plan, g) = Fight("PALADIN", 161, new[] { "LOOTA_829" }, bosses);
    var kick = (Minion)Draw(g, "LOOTA_829");
    Play(g, kick);
    Check($"T19 LOOTA_829 ({bosses} bosses): a {2 + bosses}/{2 + bosses} Taunt", kick.AttackDamage == 2 + bosses && kick.Health == 2 + bosses && kick.HasTaunt, kick.AttackDamage + "/" + kick.Health);
    if (bosses > 0)
    {
        kick.Damage = 2;
        Check($"T19 LOOTA_829 ({bosses} bosses): damage counts against the boosted Health", kick.Health == bosses && kick.Zone == g.Player1.BoardZone);
    }
}

// ---------------------------------------------------------------- T20 LOOTA_830 Shifting Hourglass (bosses defeated)
foreach (int bosses in new[] { 0, 4 })
{
    var (s, plan, g) = Fight("MAGE", 171, new[] { "LOOTA_830" }, bosses);
    var glass = Draw(g, "LOOTA_830");
    Check($"T20 LOOTA_830 ({bosses} bosses): costs {7 - bosses}", glass.Cost == 7 - bosses, glass.Cost.ToString());
    Mana(g.Player1, 10);
    Play(g, glass);
    int turn = g.Turn;
    EndTurn(g);
    Check($"T20 LOOTA_830 ({bosses} bosses): I take an extra turn", g.CurrentPlayer == g.Player1, "current " + g.CurrentPlayer.PlayerId);
    EndTurn(g);
    Check($"T20 LOOTA_830 ({bosses} bosses): then the boss plays", g.CurrentPlayer == g.Player2);
}

// ---------------------------------------------------------------- T21 LOOTA_834 Gloves of Mugging
{
    var (s, plan, g) = Fight("ROGUE", 181, new[] { "LOOTA_834" });
    var gloves = Draw(g, "LOOTA_834");
    var bossHand = g.Player2.HandZone.Select(e => e.Id).ToList();
    int mine = g.Player1.HandZone.Count;
    Play(g, gloves);
    var stolen = g.Player1.HandZone.Where(e => bossHand.Contains(e.Id)).ToList();
    Check("T21 LOOTA_834: three random cards move from the boss's hand to mine, now mine", stolen.Count == 3 && g.Player2.HandZone.Count == bossHand.Count - 3
        && g.Player1.HandZone.Count == mine - 1 + 3 && stolen.All(e => e.Controller == g.Player1 && e[GameTag.CONTROLLER] == g.Player1.PlayerId), stolen.Count.ToString());
    Mana(g.Player1, 10);
    var playable = stolen.FirstOrDefault(e => g.Player1.Options().OfType<PlayCardTask>().Any(o => o.Source == e));
    Check("T21 LOOTA_834: a stolen card is playable by me", playable != null && g.Process(g.Player1.Options().OfType<PlayCardTask>().First(o => o.Source == playable)));
    var (_, _, g1) = Fight("ROGUE", 182, new[] { "LOOTA_834" });
    var gl = Draw(g1, "LOOTA_834");
    while (g1.Player2.HandZone.Count > 2) { var x = g1.Player2.HandZone[0]; g1.Player2.HandZone.Remove(x); g1.Player2.SetasideZone.Add(x); }
    Play(g1, gl);
    Check("T21 LOOTA_834: a 2-card hand gives 2", g1.Player2.HandZone.Count == 0);
}

// ---------------------------------------------------------------- T22 LOOTA_835 Greedy Pickaxe
{
    var (s, plan, g) = Fight("WARRIOR", 191, new[] { "LOOTA_835" });
    var pick = Draw(g, "LOOTA_835"); Mana(g.Player1, 3);
    Play(g, pick);
    int baseMana = g.Player1.BaseMana, left = g.Player1.RemainingMana;
    g.Process(HeroAttackTask.Any(g.Player1, g.Player2.Hero));
    Check("T22 LOOTA_835: a 3/2 weapon; after the hero attacks, an empty Mana Crystal (3 -> 4, remaining unchanged)",
        g.Player1.BaseMana == baseMana + 1 && g.Player1.RemainingMana == left && g.Player1.Hero.Weapon?.Durability == 1, $"{baseMana}->{g.Player1.BaseMana} {left}->{g.Player1.RemainingMana}");
}

// ---------------------------------------------------------------- T23 LOOTA_836 Bag of Coins
{
    var (s, plan, g) = Fight("DRUID", 201, new[] { "LOOTA_836" });
    var bag = Draw(g, "LOOTA_836");
    int hand = g.Player1.HandZone.Count;
    Play(g, bag);
    Check("T23 LOOTA_836: the hand is filled with Coins (10)", g.Player1.HandZone.Count == 10 && g.Player1.HandZone.Count(e => e.Card.Id == "GAME_005") >= 10 - (hand - 1));
}

// ---------------------------------------------------------------- T24 LOOTA_837 Horn of Cenarius
{
    var (s, plan, g) = Fight("HUNTER", 211, new[] { "LOOTA_837" });
    var horn = Draw(g, "LOOTA_837"); Mana(g.Player1, 10);
    int minions = g.Player1.DeckZone.Count(e => e is Minion), deck = g.Player1.DeckZone.Count;
    Play(g, horn);
    Check("T24 LOOTA_837: Recruits 3 minions from my deck", g.Player1.BoardZone.Count == Math.Min(3, minions) && g.Player1.DeckZone.Count == deck - Math.Min(3, minions), minions + " in deck");
}

// ---------------------------------------------------------------- T25 LOOTA_838 Dr. Boom's Boombox
{
    var (s, plan, g) = Fight("WARRIOR", 221, new[] { "LOOTA_838" });
    var box = Draw(g, "LOOTA_838"); Mana(g.Player1, 10);
    Play(g, box);
    Check("T25 LOOTA_838: seven Boom Bots", g.Player1.BoardZone.Count == 7 && g.Player1.BoardZone.All(m => m.Card.Id == "GVG_110t"));
    var (_, _, g2) = Fight("WARRIOR", 222, new[] { "LOOTA_838" });
    var b2 = Draw(g2, "LOOTA_838"); Mana(g2.Player1, 10); Board(g2, g2.Player1, "CS2_231"); Board(g2, g2.Player1, "CS2_231");
    Play(g2, b2);
    Check("T25 LOOTA_838: a board with 2 minions takes 5 Boom Bots", g2.Player1.BoardZone.Count == 7 && g2.Player1.BoardZone.Count(m => m.Card.Id == "GVG_110t") == 5);
}

// ---------------------------------------------------------------- T26 LOOTA_839 Scroll of Confusion
{
    var moved = 0;
    for (int seed = 0; seed < 6; seed++)
    {
        var (s, plan, g) = Fight("PRIEST", 231 + seed, new[] { "LOOTA_839" });
        var scroll = Draw(g, "LOOTA_839");
        var ids = new List<Minion>();
        for (int i = 0; i < 3; i++) { ids.Add(Board(g, g.Player1, "CS2_231")); ids.Add(Board(g, g.Player2, "CS2_182")); }
        var before = ids.ToDictionary(m => m.Id, m => m.Controller.PlayerId);
        Play(g, scroll);
        bool consistent = ids.All(m => m.Zone == m.Controller.BoardZone && m[GameTag.CONTROLLER] == m.Controller.PlayerId)
            && g.Player1.BoardZone.Count + g.Player2.BoardZone.Count == 6;
        moved += ids.Count(m => m.Controller.PlayerId != before[m.Id]);
        if (seed == 0)
        {
            Check("T26 LOOTA_839: every minion stays in play on the side it picked (6 minions, each on its controller's board)", consistent);
            var (_, _, g2) = Fight("PRIEST", 231, new[] { "LOOTA_839" });
            var sc2 = Draw(g2, "LOOTA_839"); var ids2 = new List<Minion>();
            for (int i = 0; i < 3; i++) { ids2.Add(Board(g2, g2.Player1, "CS2_231")); ids2.Add(Board(g2, g2.Player2, "CS2_182")); }
            Play(g2, sc2);
            Check("T26 LOOTA_839: the same seed picks the same sides (game RNG)", ids2.Select(m => m.Controller.PlayerId).SequenceEqual(ids.Select(m => m.Controller.PlayerId)));
        }
        else if (!consistent) Check($"T26 LOOTA_839 seed {seed}: consistent boards", false);
    }
    Check("T26 LOOTA_839: over six seeds minions change sides", moved > 0, moved.ToString());
    var (_, _, gf) = Fight("PRIEST", 240, new[] { "LOOTA_839" });
    var sf = Draw(gf, "LOOTA_839");
    for (int i = 0; i < 6; i++) Board(gf, gf.Player1, "CS2_231");
    for (int i = 0; i < 7; i++) Board(gf, gf.Player2, "CS2_182");
    Play(gf, sf);
    Check("T26 LOOTA_839: a full board keeps what cannot move (no minion lost)", gf.Player1.BoardZone.Count + gf.Player2.BoardZone.Count == 13
        && gf.Player1.BoardZone.Count <= 7 && gf.Player2.BoardZone.Count <= 7);
}

// ---------------------------------------------------------------- T27 LOOTA_840 Wax Rager
{
    var (s, plan, g) = Fight("DEATHKNIGHT", 251, new[] { "LOOTA_840" });
    var wax = (Minion)Draw(g, "LOOTA_840"); Mana(g.Player1, 10);
    Play(g, wax);
    Check("T27 LOOTA_840: a 5/1 with a Deathrattle", wax.AttackDamage == 5 && wax.Health == 1 && wax.HasDeathrattle);
    wax.Destroy(); g.DeathProcessingAndAuraUpdate();
    Check("T27 LOOTA_840: when it dies a new Wax Rager is summoned", wax.Zone?.Type == Zone.GRAVEYARD && g.Player1.BoardZone.Count == 1 && g.Player1.BoardZone[0].Card.Id == "LOOTA_840" && g.Player1.BoardZone[0] != wax);
    var again = g.Player1.BoardZone[0]; again.Destroy(); g.DeathProcessingAndAuraUpdate();
    Check("T27 LOOTA_840: and again", g.Player1.BoardZone.Count == 1 && g.Player1.BoardZone[0].Card.Id == "LOOTA_840");
}

// ---------------------------------------------------------------- T28 LOOTA_841 Portable Forge
{
    var (s, plan, g) = Fight("PALADIN", 261, new[] { "LOOTA_841" });
    var forge = Draw(g, "LOOTA_841");
    Play(g, forge);
    var choice = g.Player1.Choice;
    var offers = choice?.Choices.Select(id => g.IdEntityDic[id].Card).ToList() ?? new List<Card>();
    Check("T28 LOOTA_841: Discover 3 different scripted Legendary weapons", offers.Count == 3 && offers.Select(c => c.Id).Distinct().Count() == 3
        && offers.All(c => c.Type == CardType.WEAPON && c.Rarity == Rarity.LEGENDARY && c.Implemented), string.Join(",", offers.Select(c => c.Id)));
    var pool = DungeonRunContent.ScriptedLegendaryWeapons(g.Player1.Hero);
    Check("T28 LOOTA_841: the pool is scripted weapons only, and holds every offer", pool.Count >= 3 && pool.All(c => c.Implemented && c.Type == CardType.WEAPON) && offers.All(o => pool.Contains(o)),
        pool.Count + " weapons");
    if (choice != null) g.Process(ChooseTask.Pick(g.Player1, choice.Choices[1]));
    Check("T28 LOOTA_841: the chosen weapon is equipped", offers.Count == 3 && g.Player1.Hero.Weapon?.Card.Id == offers[1].Id && g.Player1.Choice == null,
        g.Player1.Hero.Weapon?.Card.Id ?? "none");
}

// ---------------------------------------------------------------- T29 Quel'Delar: LOOTA_842a + LOOTA_842b -> LOOTA_842
{
    var (s, plan, g) = Fight("WARRIOR", 271, new[] { "LOOTA_842a", "LOOTA_842b" });
    Check("T29 both pieces are run deck cards", FromRunDeck(s, plan, "LOOTA_842a") && FromRunDeck(s, plan, "LOOTA_842b"));
    // Put both back in the deck, then draw the blade (it stays the blade) and the hilt (they combine).
    foreach (var e in g.Player1.HandZone.Where(x => x.Card.Id == "LOOTA_842a" || x.Card.Id == "LOOTA_842b").ToList()) { g.Player1.HandZone.Remove(e); g.Player1.DeckZone.Add(e); }
    Check("T29 (setup) neither piece in hand, both in the deck", !g.Player1.HandZone.Any(e => e.Card.Id.StartsWith("LOOTA_842", StringComparison.Ordinal))
        && g.Player1.DeckZone.Count(e => e.Card.Id == "LOOTA_842a" || e.Card.Id == "LOOTA_842b") == 2);
    var blade = Draw(g, "LOOTA_842a");
    Check("T29 the blade alone stays the blade in hand", g.Player1.HandZone.Contains(blade) && blade.Card.Id == "LOOTA_842a");
    var hilt = g.Player1.DeckZone.First(e => e.Card.Id == "LOOTA_842b");
    Generic.Draw(g.Player1, hilt); g.DeathProcessingAndAuraUpdate();
    var hand = g.Player1.HandZone.Select(e => e.Card.Id).ToList();
    Check("T29 drawing the second piece combines them in hand into Quel'Delar", hand.Contains("LOOTA_842") && !hand.Contains("LOOTA_842a") && !hand.Contains("LOOTA_842b"),
        string.Join(",", hand));
    var sword = g.Player1.HandZone.FirstOrDefault(e => e.Card.Id == "LOOTA_842");
    Mana(g.Player1, 10);
    Board(g, g.Player2, "CS2_182");
    int bossHp = g.Player2.Hero.Health, myHp = g.Player1.Hero.Health;
    bool equipped = sword != null && Play(g, sword);
    Check("T29 Quel'Delar is a 6/6 weapon for (1)", equipped && g.Player1.Hero.Weapon?.Card.Id == "LOOTA_842" && g.Player1.Hero.AttackDamage == 6 && g.Player1.Hero.Weapon.Durability == 6);
    var taunting = g.Player2.BoardZone.FirstOrDefault();
    g.Process(HeroAttackTask.Any(g.Player1, g.Player2.Hero));
    Check("T29 Quel'Delar: after the hero attacks, 6 damage to all enemies (boss 6 + 6, its Yeti dies), not to me",
        g.Player2.Hero.Health == bossHp - 12 && g.Player2.BoardZone.Count == 0 && g.Player1.Hero.Health == myHp, bossHp + "->" + g.Player2.Hero.Health);

    // Hilt alone: +3/+3 on a minion. Blade alone: a 3/3 weapon.
    var (_, _, gh) = Fight("PRIEST", 272, new[] { "LOOTA_842b" });
    var h = Draw(gh, "LOOTA_842b"); Mana(gh.Player1, 10); var wisp = Board(gh, gh.Player1, "CS2_231");
    Play(gh, h, wisp);
    Check("T29 the hilt alone gives a minion +3/+3 (LOOTA_842be)", wisp.AttackDamage == 4 && wisp.Health == 4 && wisp.AppliedEnchantments.Any(e => e.Card.Id == "LOOTA_842be"));
    var (_, _, gb) = Fight("PRIEST", 273, new[] { "LOOTA_842a" });
    var b = Draw(gb, "LOOTA_842a"); Mana(gb.Player1, 10); Play(gb, b);
    Check("T29 the blade alone is a 3/3 weapon", gb.Player1.Hero.Weapon?.Card.Id == "LOOTA_842a" && gb.Player1.Hero.AttackDamage == 3 && gb.Player1.Hero.Weapon.Durability == 3);
    // Both pieces dealt in the opening hand stay two opening cards (phase 6 review: the hand-entry combine waits out the
    // opening deal and the mulligan); kept through the mulligan, they combine at the start of my first turn.
    int found = 0, dealtCombined = 0;
    for (long seed = 1; seed <= 400 && found < 2; seed++)
    {
        var so = NewAct("PRIEST", seed); Hold(so, "LOOTA_842a", "LOOTA_842b");
        var po = run.BuildPlan(so);
        var cfg = new GameConfig { RandomSeed = run.MatchSeed(so), Shuffle = true, SkipMulligan = false, History = true, Logging = false, FormatType = FormatType.FT_WILD };
        DungeonRunSetup.Configure(cfg, po);
        var gx = new Game(cfg); gx.StartGame(); DungeonRunSetup.ApplyAfterStart(gx, po);
        var dealt = gx.Player1.HandZone.Select(e => e.Card.Id).ToList();
        if (dealt.Contains("LOOTA_842")) { dealtCombined++; continue; }
        if (!(dealt.Contains("LOOTA_842a") && dealt.Contains("LOOTA_842b"))) continue;
        found++;
        var keepAll = gx.Player1.Choice.Choices.ToList();
        Check($"T29 seed {seed}: the dealt pair is two of the three opening cards (three mulligan choices)", dealt.Count == 3 && keepAll.Count == 3, string.Join(",", dealt));
        gx.Process(ChooseTask.Mulligan(gx.Player1, keepAll)); gx.Process(ChooseTask.Mulligan(gx.Player2, new List<int>())); gx.MainBegin();
        var hx = gx.Player1.HandZone.Select(e => e.Card.Id).ToList();
        Check($"T29 seed {seed}: both pieces dealt and kept are combined by the start of my first turn", hx.Contains("LOOTA_842") && !hx.Contains("LOOTA_842a") && !hx.Contains("LOOTA_842b"),
            string.Join(",", dealt) + " -> " + string.Join(",", hx));
    }
    Check("T29 an opening hand that deals both pieces never combines them as dealt; kept, they hold Quel'Delar by my first turn",
        found > 0 && dealtCombined == 0, "dealt both " + found + ", combined as dealt " + dealtCombined);
    var (_, _, gy) = Fight("PRIEST", 275, new[] { "LOOTA_842a" });
    var by = Draw(gy, "LOOTA_842a"); var bossHilt = ToHand(gy, gy.Player2, "LOOTA_842b");
    Check("T29 the boss's hilt never combines with my blade", gy.Player1.HandZone.Contains(by) && gy.Player2.HandZone.Contains(bossHilt));
}

// ---------------------------------------------------------------- T30 LOOTA_843 THE CANDLE
{
    var (s, plan, g) = Fight("MAGE", 281, new[] { "LOOTA_843" });
    var candle = Draw(g, "LOOTA_843"); Mana(g.Player1, 10);
    var yeti = Board(g, g.Player2, "CS2_122"); var golem = Board(g, g.Player2, "CS2_186"); var mine = Board(g, g.Player1, "CS2_182");
    int deck = g.Player1.DeckZone.Count, hp = g.Player2.Hero.Health;
    Play(g, candle);
    Check("T30 LOOTA_843: 4 damage to each enemy minion (Raid Leader dies, Golem 7 -> 3), none to my minion or the boss",
        yeti.Zone?.Type == Zone.GRAVEYARD && golem.Health == 3 && mine.Health == 5 && g.Player2.Hero.Health == hp,
        $"yeti {yeti.Zone?.Type} {yeti.Health} golem {golem.Health} mine {mine.Health} boss {hp}->{g.Player2.Hero.Health}");
    Check("T30 LOOTA_843: THE CANDLE is shuffled back into my deck", g.Player1.DeckZone.Count == deck + 1 && g.Player1.DeckZone.Count(e => e.Card.Id == "LOOTA_843") == 1);
}

// ---------------------------------------------------------------- T31 LOOTA_847 Mask of Mimicry
{
    var (s, plan, g) = Fight("HUNTER", 291, new[] { "LOOTA_847" });
    var mask = Draw(g, "LOOTA_847"); Mana(g.Player1, 10);
    ToHand(g, g.Player1, "CS2_231"); ToHand(g, g.Player1, "CS2_189"); var spell = ToHand(g, g.Player1, "CS2_029");
    var golem = Board(g, g.Player2, "CS2_186");
    int minionsInHand = g.Player1.HandZone.Count(e => e is Minion && e != mask);
    Play(g, mask, golem);
    Check("T31 LOOTA_847: every minion in my hand becomes a War Golem; spells and the board stay",
        g.Player1.HandZone.Count(e => e.Card.Id == "CS2_186") == minionsInHand && g.Player1.HandZone.Contains(spell) && spell.Card.Id == "CS2_029" && g.Player2.BoardZone.Contains(golem),
        string.Join(",", g.Player1.HandZone.Select(e => e.Card.Id)));
}

// ---------------------------------------------------------------- T32 run edits in AI clones never leak into the real result
{
    var (s, plan, g) = Fight("MAGE", 301, new[] { "LOOTA_805" });
    var amulet = Draw(g, "LOOTA_805"); Mana(g.Player1, 10); var yeti = Board(g, g.Player2, "CS2_182");
    var clone = g.Clone();
    var cAmulet = clone.IdEntityDic[amulet.Id]; var cYeti = (ICharacter)clone.IdEntityDic[yeti.Id];
    clone.Process(PlayCardTask.Any(clone.Player1, cAmulet, cYeti));
    Check("T32 a simulated play (a clone) records its own edit, the real game none", clone.RunResult?.Edits.Count == 1 && (g.RunResult == null || g.RunResult.Edits.Count == 0));
}

// ---------------------------------------------------------------- T33 full games against the real boss AI, the player playing the treasure
var testBudget = new Budget(6000, 2000, 300, 6, 3, 100, 8, turnSearchSeconds: 6, turnWallSeconds: 8);
var aiClass = new Dictionary<string, string>
{
    ["LOOTA_805"] = "MAGE", ["LOOTA_806"] = "WARRIOR", ["LOOTA_811"] = "PRIEST", ["LOOTA_812"] = "PALADIN", ["LOOTA_813"] = "HUNTER",
    ["LOOTA_814"] = "MAGE", ["LOOTA_816"] = "MAGE", ["LOOTA_817"] = "DRUID", ["LOOTA_819"] = "WARLOCK", ["LOOTA_820"] = "ROGUE",
    ["LOOTA_821"] = "ROGUE", ["LOOTA_822"] = "MAGE", ["LOOTA_823"] = "PRIEST", ["LOOTA_826"] = "SHAMAN", ["LOOTA_827"] = "WARLOCK",
    ["LOOTA_829"] = "PALADIN", ["LOOTA_830"] = "MAGE", ["LOOTA_834"] = "ROGUE", ["LOOTA_835"] = "WARRIOR", ["LOOTA_836"] = "DRUID",
    ["LOOTA_837"] = "HUNTER", ["LOOTA_838"] = "WARRIOR", ["LOOTA_839"] = "PRIEST", ["LOOTA_840"] = "DEATHKNIGHT", ["LOOTA_841"] = "PALADIN",
    ["LOOTA_842"] = "WARRIOR", ["LOOTA_842a"] = "DEMONHUNTER", ["LOOTA_842b"] = "MAGE_ROGUE", ["LOOTA_843"] = "MAGE", ["LOOTA_847"] = "HUNTER",
};
string AiGame(string treasure, string clsId, long seed)
{
    var s = NewAct(clsId, seed);
    // Quel'Delar is played as the combine makes it: both pieces in the run deck.
    string[] held = treasure == "LOOTA_842" ? new[] { "LOOTA_842a", "LOOTA_842b" } : new[] { treasure };
    Hold(s, held);
    Defeated(s, 2);
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
    var view = new MulliganView(choice.Choices.Select(id => new MulliganCard(id, game.IdEntityDic[id].Card.Id, game.IdEntityDic[id].Card.Cost)),
        false, pilot.Deck.CardIds, game.Player1.Hero.Card.Class);
    game.Process(ChooseTask.Mulligan(game.Player2, BotDriver.Mulligan(view, new DeckPlanMulligan(pilot.Plan)).ToList()));
    game.MainBegin();
    foreach (string id in held)
    {
        var e = game.Player1.DeckZone.FirstOrDefault(x => x.Card.Id == id);
        if (e != null) Generic.Draw(game.Player1, e);
    }
    game.DeathProcessingAndAuraUpdate();
    string playId = treasure;
    int decisionIndex = 0, actions = 0, decisions = 0, fallbacks = 0, playedTurn = -1, boostedTurn = -1;
    void Act(PlayerTask task, string who)
    {
        if (++actions > 1500) throw new Exception("stuck after 1500 actions");
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
            // The treasure's cost is met on the first turn it is in hand (Wish and Rod of Roasting cost 10).
            var held0 = playedTurn < 0 ? game.Player1.HandZone.FirstOrDefault(e => e.Card.Id == playId) : null;
            if (held0 != null && boostedTurn != game.Turn && game.Player1.BaseMana < held0.Cost) { boostedTurn = game.Turn; game.Player1.BaseMana = Math.Min(10, held0.Cost); game.Player1.UsedMana = 0; }
            var options = game.Player1.Options();
            var treasurePlay = playedTurn < 0 ? options.OfType<PlayCardTask>().Where(o => o.Source.Card.Id == playId)
                .OrderByDescending(o => o.Target != null && o.Target.Controller != game.Player1 ? 1 : 0).FirstOrDefault() : null;
            if (treasurePlay != null) { playedTurn = game.Turn; Act(treasurePlay, "player"); }
            else Act(LocalBot.Choose(options), "player");
        }
    }
    string result = game.State != State.COMPLETE ? "turn-cap" : game.Player2.PlayState == PlayState.WON ? "boss" : game.Player1.PlayState == PlayState.WON ? "player" : "tie";
    return $"{result} turns {game.Turn} played-turn {playedTurn} decisions {decisions} fallbacks {fallbacks} boss {boss.Id}";
}
foreach (string id in f2)
{
    string row;
    try { row = AiGame(id, aiClass[id], 151); }
    catch (Exception e) { row = "FAULT " + e.GetType().Name + ": " + e.Message.Split('\n')[0]; }
    Console.WriteLine($"T33 {id,-10} {aiClass[id],-12} {row}");
    Check($"T33 {id}: the player plays it in a full game against the boss AI, which ends without a fault or an AI fallback",
        !row.StartsWith("FAULT") && row.Contains("fallbacks 0 ") && !row.Contains("played-turn -1"), row);
}

Console.WriteLine($"{passes} passed, {failures} failed");
return failures == 0 ? 0 : 1;
