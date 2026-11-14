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

// Phase 8 wave 3 integration (families 06, 09, 10): the fix the merged tree needed, asserted in real engine games through
// the run setup path (DungeonRunSetup.Configure / ApplyAfterStart). Built by p8w3/make_w3fix.py (helpers: the preamble of
// p8w1/suites/c-w1fix).
//   X1-X4  GIL_618 Glinda Crowskin ("Minions in your hand have Echo", a HAND aura with a SET effect) dies in the boss's fair
//          view. The view replaces the run player's hidden hand cards with Unknown stand-ins that hold no aura effects, and
//          Effect.RemoveAuraFrom dereferenced them: NullReferenceException in the boss's search, a fallback decision
//          (cross-family bosspilot run: ULDA_719 Marin's Return against Chronomancer Inara, turn 21; the run player held
//          Glinda). A stand-in without aura effects has nothing to remove.
int failures = 0, passes = 0;
void Check(string name, bool ok, string detail = "")
{
    if (ok) passes++; else failures++;
    Console.WriteLine((ok ? "PASS " : "FAIL ") + name + (detail.Length > 0 ? "  (" + detail + ")" : ""));
}
void Guard(string name, Action body)
{
    try { body(); }
    catch (Exception e) { failures++; Console.WriteLine("FAIL " + name + " threw " + e.GetType().Name + ": " + e.Message.Split('\n')[0] + " @ " + (e.StackTrace ?? "").Split('\n').FirstOrDefault()?.Trim()); }
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
Card C(string id) => Cards.FromId(id);
string root = VerifyPaths.Data;
string R(string f) => File.ReadAllText(Path.Combine(root, f));
var data = DungeonRunData.Parse(R("dungeon-run/run-v1.json"), R("dungeon-run/pool-v1.json"), R("npc/decks-v1.json"), R("dungeon-run/catalog-v1.json"),
    R("dungeon-run/schedule-v1.json"), R("dungeon-run/tavern-v1.json"), R("dungeon-run/encounters-v1.json"));
var run = new DungeonRun(data);
var all = new DungeonRunProfile(); all.Unlocked.AddRange(data.Unlocks.Keys);

string yeti = "CS2_182"; // Chillwind Yeti 4/5, cost 4
string[] Deck(params (string id, int n)[] cards) => cards.SelectMany(c => Enumerable.Repeat(c.id, c.n)).ToArray();
string[] filler = Deck((yeti, 30));
RunMatchPlan Plan(string[] playerDeck = null, string[] bossDeck = null, string playerHero = "HERO_08", string playerPower = "CS2_034") => new RunMatchPlan
{
    PlayerHero = C(playerHero), PlayerHeroPowerId = playerPower, PlayerDeck = (playerDeck ?? filler).Select(C).ToList(),
    BossHero = C("LOOTA_BOSS_35h"), BossHeroPowerId = "LOOTA_BOSS_35p", BossHealth = 30, BossDeck = (bossDeck ?? filler).Select(C).ToList(),
    PlayerGeneratedSecrets = true, EncounterId = "test", EncounterDeck = true,
    ExcludedGenerated = DungeonRunAdmission.GeneratedRejects(true), ExcludedGeneratedBoss = DungeonRunAdmission.GeneratedRejectsBoss(true),
};
Game Ready(RunMatchPlan plan, int seed = 7, bool history = true)
{
    var cfg = new GameConfig { RandomSeed = seed, Shuffle = true, SkipMulligan = false, History = history, Logging = false, FormatType = FormatType.FT_WILD };
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
void Mana(Controller c, int n = 10) { c.BaseMana = n; c.UsedMana = 0; c.OverloadLocked = 0; c.OverloadOwed = 0; }
IPlayable ToHand(Game g, Controller c, string id)
{
    while (c.HandZone.Count >= 9) { var old = c.HandZone[0]; c.HandZone.Remove(old); c.SetasideZone.Add(old); }
    var e = Entity.FromCard(c, C(id)); Generic.AddHandPhase.Invoke(c, e); g.DeathProcessingAndAuraUpdate(); return e;
}
Minion Board(Game g, Controller c, string id, bool ready = true)
{
    var m = (Minion)Entity.FromCard(c, C(id)); Generic.SummonBlock(g, m, -1, c.Hero); g.DeathProcessingAndAuraUpdate();
    if (ready) m.IsExhausted = false;
    return m;
}
bool Play(Game g, IPlayable card, ICharacter target = null) { Mana(g.CurrentPlayer); return g.Process(PlayCardTask.Any(g.CurrentPlayer, card, target, -1, 0)); }
bool PlayNew(Game g, string id, ICharacter target = null) => Play(g, ToHand(g, g.CurrentPlayer, id), target);
void EndTurn(Game g) => g.Process(EndTurnTask.Any(g.CurrentPlayer));
bool Power(Game g, ICharacter target = null) { Mana(g.CurrentPlayer); return g.Process(HeroPowerTask.Any(g.CurrentPlayer, target)); }
bool Attack(Game g, Minion m, ICharacter target) { m.IsExhausted = false; return g.Process(MinionAttackTask.Any(g.CurrentPlayer, m, target)); }
void ToBoss(Game g) { if (g.CurrentPlayer == g.Player1) EndTurn(g); Mana(g.Player2); }
void ToPlayer(Game g) { if (g.CurrentPlayer == g.Player2) EndTurn(g); Mana(g.Player1); }
void Round(Game g) { EndTurn(g); EndTurn(g); Mana(g.CurrentPlayer); }
List<string> Hand(Controller c) => c.HandZone.Select(p => p.Card.Id).ToList();
List<string> Secrets(Controller c) => c.SecretZone.Select(s => s.Card.Id).ToList();
void SecretsToDeck(Controller c) { foreach (var s in c.HandZone.Where(p => p.Card.IsSecret).ToList()) { c.HandZone.Remove(s); c.DeckZone.Add(s); } }
bool Pick(Game g, Controller c, int index = 0) => c.Choice != null && g.Process(ChooseTask.Pick(c, c.Choice.Choices[index]));
List<Card> Offers(Game g, Controller c) => c.Choice == null ? new List<Card>() : c.Choice.Choices.Select(id => g.IdEntityDic[id].Card).ToList();
void ClearHand(Controller c) { foreach (var p in c.HandZone.ToList()) { c.HandZone.Remove(p); c.SetasideZone.Add(p); } }

// Every entity sits in exactly one zone list of its controller (the invariant the fault broke).
List<string> Doubled(Game g)
{
    var seen = new Dictionary<int, string>(); var bad = new List<string>();
    foreach (var c in new[] { g.Player1, g.Player2 })
    {
        var zones = new (string name, IEnumerable<IPlayable> cards)[] { ("deck", c.DeckZone), ("hand", c.HandZone), ("board", c.BoardZone), ("graveyard", c.GraveyardZone), ("setaside", c.SetasideZone) };
        foreach (var (name, cards) in zones)
            foreach (var p in cards)
            {
                string where = "P" + c.PlayerId + " " + name;
                if (seen.TryGetValue(p.Id, out string first)) bad.Add(p.Card.Id + "#" + p.Id + " in " + first + " and " + where); else seen[p.Id] = where;
            }
    }
    return bad;
}
string wisp = "CS2_231";

const string Glinda = "GIL_618", Raptor = "CS2_172";
(Game view, Controller boss, Controller player) FairView(Game g)
{
    var bot = FairBotState.Create(g, g.Player2.PlayerId, 1, DeckStyle.Tempo);
    var view = bot.FingerprintGame;
    var viewBoss = view.Player1.PlayerId == g.Player2.PlayerId ? view.Player1 : view.Player2;
    return (view, viewBoss, viewBoss.Opponent);
}
void Kill(Game g, Minion m) { new SabberStoneCore.Tasks.SimpleTasks.DestroyTask(SabberStoneCore.Tasks.SimpleTasks.EntityType.TARGET).Process(g, m.Controller, m.Controller.Hero, m, null); g.DeathProcessingAndAuraUpdate(); }

// ============================================================ X. Glinda Crowskin's hand aura and the boss's fair view
Guard("X", () =>
{
    var g = Ready(Plan());
    Controller p1 = g.Player1, p2 = g.Player2;
    ClearHand(p1);
    var raptorA = ToHand(g, p1, Raptor); var raptorB = ToHand(g, p1, Raptor);
    var glinda = Board(g, p1, Glinda);
    Check("X1 the run player's Glinda gives the minions in its hand Echo", raptorA[GameTag.ECHO] == 1 && raptorB[GameTag.ECHO] == 1, $"{raptorA[GameTag.ECHO]} {raptorB[GameTag.ECHO]}");
    // The boss's turn: its fair view masks the run player's hand.
    ToBoss(g);
    var (view, vBoss, vPlayer) = FairView(g);
    var vGlinda = vPlayer.BoardZone.First(m => m.Card.Id == Glinda);
    int masked = vPlayer.HandZone.Count(c => c is Unknown);
    string error = "";
    try { Kill(view, vGlinda); } catch (Exception e) { error = e.GetType().Name + ": " + e.Message.Split('\n')[0]; }
    Check("X2 in the boss's fair view the run player's hand is masked and Glinda's death resolves (no exception)", masked == vPlayer.HandZone.Count && masked >= 2 && error == ""
        && vGlinda.Zone.Type == Zone.GRAVEYARD, $"masked {masked} of {vPlayer.HandZone.Count} error '{error}' zone {vGlinda.Zone?.Type}");
    // The boss's search plays such lines: its fair state ends its turn with Glinda in play.
    var bot = FairBotState.Create(g, p2.PlayerId, 3, DeckStyle.Tempo);
    string applyError = "";
    try { bot.Apply(BotAction.From(EndTurnTask.Any(p2))); } catch (Exception e) { applyError = e.GetType().Name + ": " + (e.InnerException ?? e).Message.Split('\n')[0]; }
    Check("X3 the boss's fair state applies its end of turn with Glinda in play", applyError == "", applyError);
    // The real game is unchanged: Glinda's death takes Echo from the hand minions.
    Kill(g, glinda);
    Check("X4 in the real game Glinda's death removes Echo from the minions in hand", raptorA[GameTag.ECHO] == 0 && raptorB[GameTag.ECHO] == 0 && glinda.Zone.Type == Zone.GRAVEYARD,
        $"{raptorA[GameTag.ECHO]} {raptorB[GameTag.ECHO]}");
});

Console.WriteLine($"RESULT {passes} passed, {failures} failed");
return failures;
