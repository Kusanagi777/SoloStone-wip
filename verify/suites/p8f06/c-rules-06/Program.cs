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

// Phase 8 family 06: the Tombs of Terror anomalies and plague twists. Each rule's text is asserted on both sides in real
// engine games through DungeonRunSetup (sections A to T, one per rule with a match script), then the two run-scope rules on
// the run layer (U Battle Scars, V Multiclassing) and the framework and run layer (W: schedule rows, admission,
// incompatibility, the anomaly offer and the twist roll).
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
string repo = VerifyPaths.Root;
string fixtures = args.Length > 0 ? args[0] : VerifyPaths.Fixture("p8f06/fixtures");
var facts = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(repo, "facts", "violet-hold-2026-09.json")));
var factById = new Dictionary<string, System.Text.Json.JsonElement>(StringComparer.Ordinal);
foreach (var c in facts.RootElement.GetProperty("cards").EnumerateArray()) factById[c.GetProperty("id").GetString()] = c;
string FactText(string id) => factById[id].TryGetProperty("text", out var t) ? t.GetString() : null;

// A plain run plan: Mage player, a Warrior boss, 30 Chillwind Yetis each (no effect that matters).
string[] filler = Enumerable.Repeat("CS2_182", 30).ToArray();
var rejects = DungeonRunAdmission.GeneratedRejects(true);
var rejectsBoss = DungeonRunAdmission.GeneratedRejectsBoss(true);
RunMatchPlan Plan(string rule = null, string[] playerDeck = null, string[] bossDeck = null)
{
    var plan = new RunMatchPlan
    {
        PlayerHero = C("HERO_08"), PlayerHeroPowerId = "CS2_034", PlayerDeck = (playerDeck ?? filler).Select(C).ToList(),
        BossHero = C("HERO_01"), BossHeroPowerId = "CS2_102", BossHealth = 30, BossDeck = (bossDeck ?? filler).Select(C).ToList(),
        PlayerGeneratedSecrets = true, ExcludedGenerated = rejects, ExcludedGeneratedBoss = rejectsBoss,
    };
    if (rule != null) plan.GlobalRules = new List<string> { rule };
    return plan;
}
Game Start(RunMatchPlan plan, int seed = 7, bool history = true)
{
    var cfg = new GameConfig { RandomSeed = seed, Shuffle = true, SkipMulligan = false, History = history, Logging = false, FormatType = FormatType.FT_WILD };
    DungeonRunSetup.Configure(cfg, plan);
    var g = new Game(cfg);
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
Game Ready(RunMatchPlan plan, int seed = 7, bool history = true) { var g = Start(plan, seed, history); Finish(g); return g; }
void Mana(Controller c, int n = 10) { c.BaseMana = n; c.UsedMana = 0; c.OverloadLocked = 0; c.OverloadOwed = 0; }
IPlayable ToHand(Game g, Controller c, string id)
{
    while (c.HandZone.Count >= 9) { var old = c.HandZone[0]; c.HandZone.Remove(old); c.SetasideZone.Add(old); }
    var e = Entity.FromCard(c, C(id)); Generic.AddHandPhase.Invoke(c, e); g.DeathProcessingAndAuraUpdate(); return e;
}
Minion Board(Game g, Controller c, string id) { var m = (Minion)Entity.FromCard(c, C(id)); Generic.SummonBlock(g, m, -1, c.Hero); g.DeathProcessingAndAuraUpdate(); return m; }
bool Play(Game g, IPlayable card, ICharacter target = null) => g.Process(PlayCardTask.Any(g.CurrentPlayer, card, target, -1, 0));
bool Cast(Game g, string id, ICharacter target = null) { Mana(g.CurrentPlayer); return Play(g, ToHand(g, g.CurrentPlayer, id), target); }
void EndTurn(Game g) => g.Process(EndTurnTask.Any(g.CurrentPlayer));
void Hit(Game g, ICharacter target, int amount) { Generic.DamageCharFunc(g.CurrentPlayer.Hero, target, amount, false); g.DeathProcessingAndAuraUpdate(); }
void Kill(Game g, Minion m) { m.Destroy(); g.DeathProcessingAndAuraUpdate(); }
void ToBoss(Game g) { if (g.CurrentPlayer == g.Player1) EndTurn(g); }
void ToPlayer(Game g) { if (g.CurrentPlayer == g.Player2) EndTurn(g); }
bool Both(Game g, string rule) => DungeonRunGlobalRules.IsActive(g.Player1, rule) && DungeonRunGlobalRules.IsActive(g.Player2, rule);
int Ench(IPlayable p, string id) => p.AppliedEnchantments?.Count(e => e.Card.Id == id) ?? 0;
int InDeck(Controller c, string id) => c.DeckZone.Count(p => p.Card.Id == id);
int InHand(Controller c, string id) => c.HandZone.Count(p => p.Card.Id == id);
Dictionary<string, int> Ledger(Game g, Controller c) => OwnDeckLedger.Observe(g, c, null).Sample(g, c, 11);
int Known(Dictionary<string, int> sample, string id) => sample.TryGetValue(id, out int n) ? n : 0;

// ============================================================ A. ULDA_702 Loot-Filled
Guard("A", () =>
{
    var g = Ready(Plan("ULDA_702"));
    Check("A1 the rule's player enchantment is on both players", Both(g, "ULDA_702") && g.Player1.AppliedEnchantments.Count(e => e.Card.Id == "ULDA_702e") == 1 && g.Player2.AppliedEnchantments.Count(e => e.Card.Id == "ULDA_702e") == 1);
    int coins = InHand(g.Player2, "GAME_005"), mine = InHand(g.Player1, "GAME_005");
    Mana(g.Player1);
    Play(g, ToHand(g, g.Player1, "CS2_182"));
    Check("A2 after the run player plays a minion the boss is given a Coin, the run player none", InHand(g.Player2, "GAME_005") == coins + 1 && InHand(g.Player1, "GAME_005") == mine);
    Board(g, g.Player1, "CS2_182");
    Check("A3 a summoned (not played) minion gives nothing", InHand(g.Player2, "GAME_005") == coins + 1);
    Cast(g, "CS2_029", g.Player2.Hero);
    Check("A4 a spell gives nothing", InHand(g.Player2, "GAME_005") == coins + 1);
    ToBoss(g); Mana(g.Player2);
    Play(g, ToHand(g, g.Player2, "CS2_182"));
    Check("A5 after the boss plays a minion the run player is given a Coin", InHand(g.Player1, "GAME_005") == mine + 1);
    while (g.Player1.HandZone.Count < 10) Generic.AddHandPhase.Invoke(g.Player1, Entity.FromCard(g.Player1, C("CS2_182")));
    int full = InHand(g.Player1, "GAME_005");
    Mana(g.Player2);
    Play(g, ToHand(g, g.Player2, "CS2_182"));
    Check("A6 a full hand takes no Coin", g.Player1.HandZone.Count == 10 && InHand(g.Player1, "GAME_005") == full);
    var plain = Ready(Plan());
    int before = InHand(plain.Player2, "GAME_005");
    Mana(plain.Player1); Play(plain, ToHand(plain, plain.Player1, "CS2_182"));
    Check("A7 without the rule: no Coin", InHand(plain.Player2, "GAME_005") == before);
});

// ============================================================ B. ULDA_705 Booms Away
Guard("B", () =>
{
    var g = Ready(Plan("ULDA_705"));
    Check("B1 the rule is on both players", Both(g, "ULDA_705"));
    int d1 = g.Player1.DeckZone.Count, d2 = g.Player2.DeckZone.Count;
    Kill(g, Board(g, g.Player1, "CS2_182"));
    Check("B2 the run player's minion dies: a Bomb is shuffled into the run player's deck only", InDeck(g.Player1, "BOT_511t") == 1 && g.Player1.DeckZone.Count == d1 + 1 && g.Player2.DeckZone.Count == d2 && InDeck(g.Player2, "BOT_511t") == 0);
    Kill(g, Board(g, g.Player2, "CS2_182"));
    Kill(g, Board(g, g.Player2, "CS2_120"));
    Check("B3 two of the boss's minions die: two Bombs in the boss's deck", InDeck(g.Player2, "BOT_511t") == 2 && InDeck(g.Player1, "BOT_511t") == 1);
    Check("B4 the Bomb is the client's (\"Casts When Drawn\": 5 damage)", C("BOT_511t").Text.Contains("5 damage") && FactText("ULDA_705").Contains("explodes for 5 damage"));
    var bossLedger = Ledger(g, g.Player2);
    Check("B5 the boss's own-deck ledger holds its two Bombs (a public shuffle) and exactly its deck's cards", Known(bossLedger, "BOT_511t") == 2 && bossLedger.Values.Sum() == g.Player2.DeckZone.Count,
        string.Join(",", bossLedger.Select(p => p.Key + "x" + p.Value)));
    int damage = g.Player1.Hero.Damage, hand = g.Player1.HandZone.Count;
    Generic.Draw(g.Player1, g.Player1.DeckZone.First(p => p.Card.Id == "BOT_511t"));
    g.DeathProcessingAndAuraUpdate();
    Check("B6 drawing the Bomb: 5 damage to the drawer and another card drawn", g.Player1.Hero.Damage == damage + 5 && g.Player1.HandZone.Count == hand + 1 && InHand(g.Player1, "BOT_511t") == 0);
    var cart = Board(g, g.Player1, "DALA_833t");
    int before = InDeck(g.Player1, "BOT_511t");
    Hit(g, cart, 5);
    Check("B7 a permanent never dies: no Bomb", cart.Zone == g.Player1.BoardZone && InDeck(g.Player1, "BOT_511t") == before);
    var taken = Board(g, g.Player2, "CS2_182");
    g.Player2.BoardZone.Remove(taken); taken.Controller = g.Player1; taken[GameTag.CONTROLLER] = g.Player1.PlayerId; g.Player1.BoardZone.Add(taken);
    int mineBefore = InDeck(g.Player1, "BOT_511t"), theirsBefore = InDeck(g.Player2, "BOT_511t");
    Kill(g, taken);
    Check("B8 a minion that changed sides: the Bomb goes to the player who controlled it when it died", InDeck(g.Player1, "BOT_511t") == mineBefore + 1 && InDeck(g.Player2, "BOT_511t") == theirsBefore);
});

// ============================================================ C. ULDA_706 No-Face
Guard("C", () =>
{
    var g = Ready(Plan("ULDA_706"));
    Check("C1 the rule is on both players; with no minions neither hero is Immune", Both(g, "ULDA_706") && !g.Player1.Hero.IsImmune && !g.Player2.Hero.IsImmune);
    Hit(g, g.Player2.Hero, 3);
    Check("C2 a hero with no minions takes damage", g.Player2.Hero.Damage == 3);
    var yeti = Board(g, g.Player1, "CS2_182");
    Check("C3 the run player controls a minion: the run hero is Immune, the boss's hero is not", g.Player1.Hero.IsImmune && !g.Player2.Hero.IsImmune);
    Hit(g, g.Player1.Hero, 7);
    Check("C4 an Immune hero takes no damage", g.Player1.Hero.Damage == 0);
    Kill(g, yeti);
    Check("C5 its last minion dies: the hero is no longer Immune", !g.Player1.Hero.IsImmune);
    Hit(g, g.Player1.Hero, 2);
    Check("C6 and takes damage again", g.Player1.Hero.Damage == 2);
    Board(g, g.Player1, "DALA_833t");
    Check("C7 a permanent (a Cart) is not a minion that counts", !g.Player1.Hero.IsImmune && !EncounterRules.ControlsMinion(g.Player1));
    var bossMinion = Board(g, g.Player2, "CS2_182");
    Check("C8 the boss controls a minion: its hero is Immune", g.Player2.Hero.IsImmune);
    int before = g.Player2.Hero.Damage;
    Cast(g, "CS2_029", bossMinion);
    Check("C9 the boss's minion can still be killed (Fireball), which ends the Immunity", bossMinion.Zone != g.Player2.BoardZone && !g.Player2.Hero.IsImmune && g.Player2.Hero.Damage == before);
    var murloc = Board(g, g.Player2, "CS2_168");
    Check("C10 the shared helper: a predicate narrows the minions that count (a Murloc counts, a Yeti does not)", EncounterRules.ControlsMinion(g.Player2, m => m.IsRace(Race.MURLOC))
        && !EncounterRules.ControlsMinion(g.Player2, m => m.IsRace(Race.DRAGON)) && EncounterRules.HeroImmuneWhile().Restless);
    var clone = g.Clone();
    Check("C11 a clone plays under the same rule", clone.Player2.Hero.IsImmune && !clone.Player1.Hero.IsImmune);
    Kill(clone, clone.Player2.BoardZone.First(m => m.Card.Id == "CS2_168"));
    Check("C12 and loses the Immunity with the minion in the clone only", !clone.Player2.Hero.IsImmune && g.Player2.Hero.IsImmune);
    var plain = Ready(Plan());
    Board(plain, plain.Player1, "CS2_182");
    Check("C13 without the rule: never Immune", !plain.Player1.Hero.IsImmune);
});

// ============================================================ D. ULDA_707 Eternal Army
Guard("D", () =>
{
    var g = Ready(Plan("ULDA_707"));
    Check("D1 the rule is on both players", Both(g, "ULDA_707"));
    int d1 = g.Player1.DeckZone.Count, d2 = g.Player2.DeckZone.Count;
    var leper = Board(g, g.Player1, "EX1_029");
    Generic.AddEnchantmentBlock(g, C("CS2_087e"), g.Player1.Hero, leper);
    g.DeathProcessingAndAuraUpdate();
    Kill(g, leper);
    var copy = g.Player1.DeckZone.FirstOrDefault(p => p.Card.Id == "EX1_029") as Minion;
    Check("D2 the run player's Leper Gnome dies: a Leper Gnome is shuffled into the run player's deck only", copy != null && !ReferenceEquals(copy, leper) && g.Player1.DeckZone.Count == d1 + 1 && g.Player2.DeckZone.Count == d2);
    Check("D3 the copy is a new card of the minion's card (its Blessing of Might is not copied)", copy != null && copy.AttackDamage == C("EX1_029").ATK && Ench(copy, "CS2_087e") == 0);
    Kill(g, Board(g, g.Player2, "CS2_120"));
    Check("D4 the boss's River Crocolisk dies: a copy in the boss's deck", InDeck(g.Player2, "CS2_120") == 1 && InDeck(g.Player1, "CS2_120") == 0);
    var bossLedger = Ledger(g, g.Player2);
    Check("D5 the boss's own-deck ledger holds the copy (a public shuffle) and exactly its deck's cards", Known(bossLedger, "CS2_120") == 1 && bossLedger.Values.Sum() == g.Player2.DeckZone.Count,
        string.Join(",", bossLedger.Select(p => p.Key + "x" + p.Value)));
    int yetis = InDeck(g.Player1, "CS2_182");
    Kill(g, Board(g, g.Player1, "CS2_182"));
    Kill(g, Board(g, g.Player1, "CS2_182"));
    Check("D6 every death shuffles one copy", InDeck(g.Player1, "CS2_182") == yetis + 2);
    var plain = Ready(Plan());
    int plainDeck = plain.Player1.DeckZone.Count;
    Kill(plain, Board(plain, plain.Player1, "EX1_029"));
    Check("D7 without the rule: nothing is shuffled", plain.Player1.DeckZone.Count == plainDeck);
});

// ============================================================ E. ULDA_708 Dizzying Blows
Guard("E", () =>
{
    var g = Ready(Plan("ULDA_708"));
    Check("E1 the rule is on both players", Both(g, "ULDA_708"));
    var bossCosts = g.Player2.HandZone.Select(p => p.Cost).ToList();
    Hit(g, g.Player1.Hero, 1);
    var hand = g.Player1.HandZone.ToList();
    Check("E2 the run hero is damaged: every card in the run player's hand has a Cost of 0 to 10 and one \"Cost Randomized.\" enchantment", hand.Count > 0 && hand.All(p => p.Cost >= 0 && p.Cost <= DungeonRunEncounters.DizzyingCostMax && Ench(p, "ULDA_708e2") == 1)
        && FactText("ULDA_708e2") == "Cost Randomized.", string.Join(",", hand.Select(p => p.Cost)));
    Check("E3 the Costs differ from card to card (four Yetis, not all one Cost)", hand.Select(p => p.Cost).Distinct().Count() >= 2, string.Join(",", hand.Select(p => p.Cost)));
    Check("E4 the boss's hand is untouched", g.Player2.HandZone.Select(p => p.Cost).SequenceEqual(bossCosts));
    var first = hand.Select(p => p.Cost).ToList();
    var rolls = new List<string>();
    for (int i = 0; i < 6; i++) { Hit(g, g.Player1.Hero, 1); rolls.Add(string.Join(",", g.Player1.HandZone.Select(p => p.Cost))); }
    Check("E5 every hit rolls again, and a card still carries one enchantment", rolls.Distinct().Count() >= 4 && g.Player1.HandZone.All(p => Ench(p, "ULDA_708e2") == 1 && p.Cost >= 0 && p.Cost <= 10), string.Join(" | ", rolls.Take(3)));
    Hit(g, g.Player2.Hero, 2);
    Check("E6 the boss's hero is damaged: the boss's hand is randomized", g.Player2.HandZone.All(p => p.Cost >= 0 && p.Cost <= 10 && (p.Card.Id == "GAME_005" || Ench(p, "ULDA_708e2") == 1)) && !g.Player2.HandZone.Select(p => p.Cost).SequenceEqual(bossCosts),
        string.Join(",", g.Player2.HandZone.Select(p => p.Card.Id + ":" + p.Cost)));
    var before = string.Join(",", g.Player1.HandZone.Select(p => p.Cost));
    Hit(g, Board(g, g.Player1, "CS2_182"), 1);
    Check("E7 a damaged minion changes nothing", string.Join(",", g.Player1.HandZone.Select(p => p.Cost)) == before);
    g.Player1.Hero.Armor = 5;
    var costs = new HashSet<string>();
    for (int i = 0; i < 4; i++) { Hit(g, g.Player1.Hero, 1); costs.Add(string.Join(",", g.Player1.HandZone.Select(p => p.Cost))); }
    Check("E8 a hit Armor absorbs is a hit: the hand is rolled", costs.Count >= 2 && g.Player1.Hero.Armor == 1);
    // A card that can be played again and again in one turn is never set below (1).
    var echo = ToHand(g, g.Player1, "GIL_207");
    int lowest = int.MaxValue;
    g.Player1.Hero.Armor = 300;
    for (int i = 0; i < 120; i++) { Hit(g, g.Player1.Hero, 1); lowest = Math.Min(lowest, echo.Cost); }
    Check("E9 an Echo card (Phantom Militia) is never set below (1) in 120 rolls; a plain card reaches (0)", DungeonRunEncounters.Repeatable(echo) && lowest == 1, "lowest " + lowest);
    Mana(g.Player1);
    var card = g.Player1.HandZone.First(p => p.Card.Id == "CS2_182");
    int cost = card.Cost;
    bool played = Play(g, card);
    Check("E10 a card is played for its randomized Cost", played && g.Player1.UsedMana == cost, "cost " + cost + " used " + g.Player1.UsedMana);
    var zero = false;
    for (int seed = 1; seed <= 6 && !zero; seed++)
    {
        var gs = Ready(Plan("ULDA_708"), seed, history: false);
        for (int i = 0; i < 10; i++) { Hit(gs, gs.Player1.Hero, 1); zero |= gs.Player1.HandZone.Any(p => p.Cost == 0); }
        if (gs.Player1.HandZone.Any(p => p.Cost < 0 || p.Cost > 10)) throw new Exception("cost out of range without history");
    }
    Check("E11 without presentation history the Costs are rolled the same way (0 is reached)", zero);
});

// ============================================================ F. ULDA_709 Angelic Presence
Guard("F", () =>
{
    var g = Ready(Plan("ULDA_709"));
    var yeti = Board(g, g.Player1, "CS2_182");
    Check("F1 the rule is on both players; a 4/5 Yeti has 5 Attack", Both(g, "ULDA_709") && yeti.AttackDamage == 5 && yeti.Health == 5);
    Hit(g, yeti, 2);
    Check("F2 damaged to 3 Health: 3 Attack", yeti.AttackDamage == 3 && yeti.Health == 3);
    Cast(g, "CS2_087", yeti);
    Check("F3 an Attack buff (Blessing of Might) changes nothing: Attack is ALWAYS the Health", yeti.AttackDamage == 3 && yeti.Health == 3);
    Cast(g, "CS2_009", yeti);
    Check("F4 a Health buff (Mark of the Wild, +2/+2) raises both", yeti.Health == 5 && yeti.AttackDamage == 5);
    var theirs = Board(g, g.Player2, "EX1_405");
    Check("F5 the boss's 0/4 Shieldbearer has 4 Attack", theirs.AttackDamage == 4);
    var cart = Board(g, g.Player1, "DALA_833t");
    Check("F6 a permanent is left alone", cart.AttackDamage == C("DALA_833t").ATK);
    var lightspawn = Board(g, g.Player1, "EX1_335");
    Hit(g, lightspawn, 1);
    Check("F7 Lightspawn (the same rule of its own) agrees with the anomaly", lightspawn.AttackDamage == lightspawn.Health && lightspawn.Health == 4);
    Cast(g, "EX1_581", theirs);
    var back = g.Player2.HandZone.FirstOrDefault(p => p.Card.Id == "EX1_405") as Minion;
    Check("F8 a minion returned to hand (Sap) has its printed Attack again", back != null && back.AttackDamage == 0, back == null ? "not in hand" : "attack " + back.AttackDamage);
    var moved = Board(g, g.Player2, "CS2_182");
    Hit(g, moved, 1);
    g.Player2.BoardZone.Remove(moved); moved.Controller = g.Player1; moved[GameTag.CONTROLLER] = g.Player1.PlayerId; g.Player1.BoardZone.Add(moved);
    g.DeathProcessingAndAuraUpdate();
    Hit(g, moved, 1);
    Check("F9 a minion that changed sides follows its new side's copy of the rule", moved.AttackDamage == 3 && moved.Health == 3);
    var clone = g.Clone();
    var cy = clone.Player1.BoardZone.First(m => m.Id == yeti.Id);
    Hit(clone, cy, 1);
    Check("F10 a clone plays under the same rule, and the real game is untouched", cy.AttackDamage == 4 && cy.Health == 4 && yeti.AttackDamage == 5);
    ToBoss(g); ToPlayer(g);
    int bossDamage = g.Player2.Hero.Damage;
    g.Process(MinionAttackTask.Any(g.Player1, yeti, g.Player2.Hero));
    Check("F11 the Yeti attacks for its Health", g.Player2.Hero.Damage == bossDamage + 5);
    var plain = Ready(Plan());
    Check("F12 without the rule: a 4/5", Board(plain, plain.Player1, "CS2_182").AttackDamage == 4);
    var nh = Ready(Plan("ULDA_709"), 9, history: false);
    var ny = Board(nh, nh.Player2, "CS2_182");
    Hit(nh, ny, 3);
    Check("F13 without presentation history too", ny.AttackDamage == 2);
});

// ============================================================ G. ULDA_710 CHARGE!
Guard("G", () =>
{
    var g = Ready(Plan("ULDA_710"));
    Mana(g.Player1);
    var yeti = (Minion)ToHand(g, g.Player1, "CS2_182");
    Play(g, yeti);
    Check("G1 the rule is on both players; a minion played this turn has Charge and can attack", Both(g, "ULDA_710") && yeti.HasCharge && yeti.CanAttack && !C("CS2_182").Charge);
    bool attacked = g.Process(MinionAttackTask.Any(g.Player1, yeti, g.Player2.Hero));
    Check("G2 it attacks the boss's hero the turn it is played", attacked && g.Player2.Hero.Damage == 4);
    ToBoss(g);
    var theirs = Board(g, g.Player2, "CS2_120");
    Check("G3 the boss's summoned minion has Charge too", theirs.HasCharge && theirs.CanAttack);
    Check("G4 a permanent gains nothing", !Board(g, g.Player2, "DALA_833t").HasCharge);
    Check("G5 the client's enchantment names the grant", Ench(theirs, "ULDA_710e2") == 1 && FactText("ULDA_710e2").Contains("Charge"));
    var plain = Ready(Plan());
    Check("G6 without the rule: no Charge", !Board(plain, plain.Player1, "CS2_182").HasCharge);
});

// ============================================================ H. ULDA_711 Relentless
Guard("H", () =>
{
    var g = Ready(Plan("ULDA_711"));
    var yeti = Board(g, g.Player1, "CS2_182");
    var theirs = Board(g, g.Player2, "CS2_120");
    Check("H1 the rule is on both players; minions on both boards have Windfury", Both(g, "ULDA_711") && yeti.HasWindfury && theirs.HasWindfury && C("CS2_182")[GameTag.WINDFURY] == 0);
    ToBoss(g); ToPlayer(g);
    bool one = g.Process(MinionAttackTask.Any(g.Player1, yeti, g.Player2.Hero));
    bool two = g.Process(MinionAttackTask.Any(g.Player1, yeti, g.Player2.Hero));
    Check("H2 the Yeti attacks twice in one turn", one && two && g.Player2.Hero.Damage == 8 && !yeti.CanAttack);
    Check("H3 the client's enchantment names the grant", Ench(yeti, "ULDA_711e2") == 1 && FactText("ULDA_711e2").Contains("Windfury"));
    var plain = Ready(Plan());
    Check("H4 without the rule: no Windfury", !Board(plain, plain.Player1, "CS2_182").HasWindfury);
});

// ============================================================ I. ULDA_712 Holograms
Guard("I", () =>
{
    var g = Ready(Plan("ULDA_712"));
    Mana(g.Player1);
    var yeti = (Minion)ToHand(g, g.Player1, "CS2_182");
    Play(g, yeti);
    var copy = g.Player2.BoardZone.FirstOrDefault(m => m.Card.Id == "CS2_182");
    Check("I1 the rule is on both players; the run player plays a Yeti: the boss gets a 1/1 Yeti", Both(g, "ULDA_712") && copy != null && copy.AttackDamage == 1 && copy.Health == 1 && g.Player2.BoardZone.Count == 1
        && yeti.AttackDamage == 4 && yeti.Health == 5);
    Mana(g.Player1);
    Play(g, ToHand(g, g.Player1, "CS2_189"), g.Player2.Hero);
    Check("I2 the copy is summoned: Elven Archer's Battlecry happens once (the run player's)", g.Player2.Hero.Damage == 1 && g.Player1.Hero.Damage == 0 && g.Player2.BoardZone.Count(m => m.Card.Id == "CS2_189" && m.Health == 1 && m.AttackDamage == 1) == 1);
    int before = g.Player2.BoardZone.Count;
    Board(g, g.Player1, "CS2_120");
    Check("I3 a summoned (not played) minion gives no copy", g.Player2.BoardZone.Count == before);
    ToBoss(g); Mana(g.Player2);
    int mine = g.Player1.BoardZone.Count;
    Play(g, ToHand(g, g.Player2, "CS2_200"));
    Check("I4 the boss plays a Boulderfist Ogre: the run player gets a 1/1 Ogre", g.Player1.BoardZone.Count == mine + 1 && g.Player1.BoardZone.Any(m => m.Card.Id == "CS2_200" && m.AttackDamage == 1 && m.Health == 1));
    while (!g.Player1.BoardZone.IsFull) Board(g, g.Player1, "CS2_182");
    Mana(g.Player2);
    Play(g, ToHand(g, g.Player2, "CS2_200"));
    Check("I5 a full board on the other side: no copy, and the minion is still played", g.Player1.BoardZone.Count == 7 && g.Player2.BoardZone.Count(m => m.Card.Id == "CS2_200") == 2);
    var plain = Ready(Plan());
    Mana(plain.Player1); Play(plain, ToHand(plain, plain.Player1, "CS2_182"));
    Check("I6 without the rule: no copy", plain.Player2.BoardZone.Count == 0);
});

// ============================================================ J. ULDA_713 Whirling Winds
Guard("J", () =>
{
    var g = Ready(Plan("ULDA_713"));
    var mine = Board(g, g.Player1, "CS2_182");
    var theirs = Board(g, g.Player2, "CS2_200");
    Check("J1 the rule is on both players; nothing happens inside a turn", Both(g, "ULDA_713") && mine.Damage == 0 && theirs.Damage == 0);
    EndTurn(g);
    Check("J2 at the start of the boss's turn every minion takes 1 damage, once", mine.Damage == 1 && theirs.Damage == 1 && g.Player1.Hero.Damage == 0 && g.Player2.Hero.Damage == 0);
    EndTurn(g);
    Check("J3 at the start of the run player's turn again", mine.Damage == 2 && theirs.Damage == 2);
    var small = Board(g, g.Player1, "CS2_189");
    EndTurn(g);
    Check("J4 a 1-Health minion dies of it", small.Zone != g.Player1.BoardZone && mine.Damage == 3);
    var plain = Ready(Plan());
    var py = Board(plain, plain.Player1, "CS2_182");
    EndTurn(plain);
    Check("J5 without the rule: no damage", py.Damage == 0);
});

// ============================================================ K. ULDA_714 In the Face
Guard("K", () =>
{
    var g = Ready(Plan("ULDA_714"));
    Check("K1 the rule is on both players (the player enchantment and the controller mark)", Both(g, "ULDA_714") && g.Player1[EncounterRules.HeroDamageDouble] == 1 && g.Player2[EncounterRules.HeroDamageDouble] == 1
        && g.Player1.AppliedEnchantments.Count(e => e.Card.Id == "ULDA_714e") == 1);
    Hit(g, g.Player2.Hero, 3);
    Hit(g, g.Player1.Hero, 2);
    Check("K2 3 damage to the boss's hero is 6, 2 to the run hero is 4", g.Player2.Hero.Damage == 6 && g.Player1.Hero.Damage == 4);
    g.Player2.Hero.Armor = 5;
    Hit(g, g.Player2.Hero, 4);
    Check("K3 Armor absorbs the doubled amount (4 damage: 5 Armor and 3 Health)", g.Player2.Hero.Armor == 0 && g.Player2.Hero.Damage == 9);
    var yeti = Board(g, g.Player1, "CS2_182");
    Hit(g, yeti, 2);
    Check("K4 a minion takes the plain amount", yeti.Damage == 2);
    Cast(g, "CS2_029", g.Player2.Hero);
    Check("K5 a Fireball to the face deals 12", g.Player2.Hero.Damage == 21);
    Board(g, g.Player2, "LOE_119");
    int before = g.Player2.Hero.Damage;
    Hit(g, g.Player2.Hero, 5);
    Check("K6 Animated Armor's cap (1 damage at a time) applies after the doubling", g.Player2.Hero.Damage == before + 1);
    var clone = g.Clone();
    int cb = clone.Player1.Hero.Damage;
    Hit(clone, clone.Player1.Hero, 1);
    Check("K7 a clone plays under the same rule", clone.Player1.Hero.Damage == cb + 2);
    var plain = Ready(Plan());
    Hit(plain, plain.Player2.Hero, 3);
    Check("K8 without the rule: 3", plain.Player2.Hero.Damage == 3 && plain.Player2[EncounterRules.HeroDamageDouble] == 0);
});

// ============================================================ L. ULDA_715 Bountiful
Guard("L", () =>
{
    var plan = Plan("ULDA_715");
    var cfg = new GameConfig { RandomSeed = 7, Shuffle = true, SkipMulligan = false, History = true, Logging = false, FormatType = FormatType.FT_WILD };
    DungeonRunSetup.Configure(cfg, plan);
    Check("L1 Configure: 6 extra cards for the run player (3 + 6), 4 for the boss (4 + 4 and The Coin)", cfg.Player1OpeningHandBonus == 6 && cfg.Player2OpeningHandBonus == 4 && GameConfig.MaxFirstOpeningHandBonus == 6 && GameConfig.MaxOpeningHandBonus == 5);
    var g = Start(plan);
    Check("L2 before the mulligan each player holds 9 cards (The Coin is one of the boss's nine)", g.Player1.HandZone.Count == 9 && g.Player2.HandZone.Count(p => p.Card.Id == "GAME_005") == 1 && g.Player2.HandZone.Count == 9
        && g.Step == Step.BEGIN_MULLIGAN, g.Player1.HandZone.Count + " / " + g.Player2.HandZone.Count);
    Check("L3 all nine can be mulliganed", g.Player1.Choice.Choices.Count == 9);
    Check("L4 the rule is active for both players (it has no player enchantment: its mark)", Both(g, "ULDA_715") && DungeonRunGlobalRules.EffectOf("ULDA_715") == null && DungeonRunGlobalRules.OpeningHand(new[] { "ULDA_715" }) == 9);
    int decks = g.Player1.DeckZone.Count + g.Player2.DeckZone.Count;
    g.Process(ChooseTask.Mulligan(g.Player1, g.Player1.Choice.Choices.Take(3).ToList()));
    g.Process(ChooseTask.Mulligan(g.Player2, new List<int>()));
    g.MainBegin();
    Check("L5 both mulligans (six and all eight cards replaced) lose no card; after its first draw the run player holds 10 cards, the boss 9", g.Player1.HandZone.Count == 10 && g.Player2.HandZone.Count == 9
        && g.Player1.DeckZone.Count + g.Player2.DeckZone.Count == decks - 1 && g.CurrentPlayer == g.Player1);
    var rich = Plan("ULDA_715"); rich.PlayerOpeningHandBonus = 2; rich.BossOpeningHandBonus = 1;
    var cfg2 = new GameConfig { RandomSeed = 7, Shuffle = true, SkipMulligan = false, History = false, Logging = false, FormatType = FormatType.FT_WILD };
    DungeonRunSetup.Configure(cfg2, rich);
    Check("L6 a smaller bonus the run already has is raised to nine cards, not added", cfg2.Player1OpeningHandBonus == 6 && cfg2.Player2OpeningHandBonus == 4);
    bool refused = false;
    try { new Game(new GameConfig { StartPlayer = 1, Player1HeroClass = CardClass.MAGE, Player2HeroClass = CardClass.WARRIOR, Player2OpeningHandBonus = 6, FillDecks = true }); } catch (ArgumentOutOfRangeException) { refused = true; }
    bool allowed = true;
    try { new Game(new GameConfig { StartPlayer = 1, Player1HeroClass = CardClass.MAGE, Player2HeroClass = CardClass.WARRIOR, Player1OpeningHandBonus = 6, FillDecks = true }); } catch (ArgumentOutOfRangeException) { allowed = false; }
    Check("L7 the engine allows 6 only for the player known to go first", refused && allowed);
    var plain = Start(Plan());
    Check("L8 without the rule: 3 and 4 (and The Coin)", plain.Player1.HandZone.Count == 3 && plain.Player2.HandZone.Count == 5);
});

// ============================================================ M. ULDA_719 Marin's Return
Guard("M", () =>
{
    var started = Start(Plan("ULDA_719"));
    Check("M1 nothing is on a board before the mulligan", started.Player1.BoardZone.Count == 0 && started.Player2.BoardZone.Count == 0 && Both(started, "ULDA_719"));
    Finish(started);
    var g = started;
    var mine = g.Player1.BoardZone.FirstOrDefault(m => m.Card.Id == "LOOT_357l");
    var theirs = g.Player2.BoardZone.FirstOrDefault(m => m.Card.Id == "LOOT_357l");
    Check("M2 after both mulligans each player has a 0/8 Master Chest on its own side", mine != null && theirs != null && g.Player1.BoardZone.Count == 1 && g.Player2.BoardZone.Count == 1 && mine.Health == 8 && mine.AttackDamage == 0
        && C("LOOT_357l").Name == "Master Chest" && DungeonRunEncounters.TreasureChest == "LOOT_357l");
    string[] loot = { "LOOT_998h", "LOOT_998j", "LOOT_998k", "LOOT_998l" };
    int hand = g.Player1.HandZone.Count, bossHand = g.Player2.HandZone.Count;
    Hit(g, theirs, 8);
    var got = g.Player1.HandZone.LastOrDefault();
    Check("M3 the run player breaks the boss's chest: the run player is given one of Marin's four treasures", theirs.Zone != g.Player2.BoardZone && g.Player1.HandZone.Count == hand + 1 && got != null && loot.Contains(got.Card.Id)
        && g.Player2.HandZone.Count == bossHand, got?.Card.Id ?? "none");
    Hit(g, mine, 8);
    var bossGot = g.Player2.HandZone.LastOrDefault();
    Check("M4 the run player's chest breaks: the boss is given a treasure", g.Player2.HandZone.Count == bossHand + 1 && bossGot != null && loot.Contains(bossGot.Card.Id), bossGot?.Card.Id ?? "none");
    Check("M5 the four treasures are scripted cards neither side's generation filter rejects", loot.All(id => C(id).Implemented && !rejects.Contains(id) && !rejectsBoss.Contains(id)));
    var seen = new HashSet<string>();
    for (int seed = 1; seed <= 12; seed++)
    {
        var gs = Ready(Plan("ULDA_719"), seed, history: false);
        Hit(gs, gs.Player2.BoardZone.First(m => m.Card.Id == "LOOT_357l"), 8);
        seen.Add(gs.Player1.HandZone.Last().Card.Id);
    }
    Check("M6 the treasure is random (twelve seeds give at least three of the four)", seen.Count >= 3 && seen.All(loot.Contains), string.Join(",", seen));
    Check("M7 the rule lists the chest for the validator and has a start block", DungeonRunGlobalRules.CardsOf("ULDA_719").SequenceEqual(new[] { "LOOT_357l" }) && DungeonRunGlobalRules.HasStart("ULDA_719"));
});

// ============================================================ N. ULDA_721 Aleatory
Guard("N", () =>
{
    string[] crocs = Enumerable.Repeat("CS2_120", 30).ToArray();
    var started = Start(Plan("ULDA_721", bossDeck: crocs));
    Check("N1 nothing is mixed before the mulligan", started.Player1.DeckZone.All(p => p.Card.Id == "CS2_182") && started.Player2.DeckZone.All(p => p.Card.Id == "CS2_120") && Both(started, "ULDA_721"));
    int total = started.Player1.DeckZone.Count + started.Player2.DeckZone.Count;
    Finish(started);
    var g = started;
    // The run player has drawn its first card since.
    int now = g.Player1.DeckZone.Count + g.Player2.DeckZone.Count;
    Check("N2 after both mulligans the decks are mixed and each player holds half (53 cards: 27 and 26, less the run player's first draw)", now == total - 1 && g.Player1.DeckZone.Count == 26 && g.Player2.DeckZone.Count == 26,
        g.Player1.DeckZone.Count + " / " + g.Player2.DeckZone.Count + " of " + total);
    int crocsMine = InDeck(g.Player1, "CS2_120"), yetisTheirs = InDeck(g.Player2, "CS2_182");
    Check("N3 each deck holds cards of both (the run player's deck has Crocolisks, the boss's has Yetis)", crocsMine >= 5 && yetisTheirs >= 5, crocsMine + " / " + yetisTheirs);
    Check("N4 every card is controlled by the player whose deck holds it", g.Player1.DeckZone.All(p => p.Controller == g.Player1 && p[GameTag.CONTROLLER] == g.Player1.PlayerId)
        && g.Player2.DeckZone.All(p => p.Controller == g.Player2 && p[GameTag.CONTROLLER] == g.Player2.PlayerId));
    int yetis = g.Player1.DeckZone.Concat(g.Player2.DeckZone).Concat(g.Player1.HandZone).Concat(g.Player2.HandZone).Count(p => p.Card.Id == "CS2_182");
    int crocsAll = g.Player1.DeckZone.Concat(g.Player2.DeckZone).Concat(g.Player1.HandZone).Concat(g.Player2.HandZone).Count(p => p.Card.Id == "CS2_120");
    Check("N5 no card is lost or made: 30 Yetis and 30 Crocolisks in the hands and decks", yetis == 30 && crocsAll == 30);
    Check("N6 both sides' deck beliefs are reset", g.RunBeliefResets.Count == 2 && g.RunBeliefResets.All(r => r.Zones == RunBeliefZones.Deck) && g.RunBeliefResets.Select(r => r.ControllerId).Distinct().Count() == 2);
    var bossLedger = Ledger(g, g.Player2);
    Check("N7 the boss's own-deck ledger knows only cards of its own list and fewer of them than its deck holds (the rest are unknown cards)", bossLedger.Keys.All(k => k == "CS2_120") && bossLedger.Values.Sum() < g.Player2.DeckZone.Count
        && bossLedger.Values.Sum() > 0, string.Join(",", bossLedger.Select(p => p.Key + "x" + p.Value)) + " of " + g.Player2.DeckZone.Count);
    var splits = new HashSet<int>();
    for (int seed = 1; seed <= 8; seed++) { var gs = Ready(Plan("ULDA_721", bossDeck: crocs), seed, history: false); splits.Add(InDeck(gs.Player1, "CS2_120")); }
    Check("N8 the split is random (eight seeds, several different shares)", splits.Count >= 3, string.Join(",", splits));
    // A card a side may not be given stays with its owner: a boss is handed no Secret and no card its generation list excludes.
    string blocked = rejectsBoss.Where(id => !rejects.Contains(id)).OrderBy(id => id, StringComparer.Ordinal).First(id => { try { var card = C(id); return card.Implemented && card.Type == CardType.MINION && card.Collectible; } catch { return false; } });
    string[] mixed = Enumerable.Repeat("EX1_287", 10).Concat(Enumerable.Repeat(blocked, 10)).Concat(Enumerable.Repeat("CS2_182", 10)).ToArray();
    bool clean = true; int crossed = 0;
    for (int seed = 1; seed <= 6; seed++)
    {
        var gs = Ready(Plan("ULDA_721", playerDeck: mixed, bossDeck: crocs), seed, history: false);
        clean &= InDeck(gs.Player2, "EX1_287") == 0 && InDeck(gs.Player2, blocked) == 0 && gs.Player2.DeckZone.Count == 26 && gs.Player1.DeckZone.Count == 26;
        crossed += InDeck(gs.Player2, "CS2_182");
    }
    Check("N9 the run player's Counterspells and a card the boss's generation list excludes (" + blocked + ") never reach the boss's deck; its Yetis do, and the halves stay equal", clean && crossed > 0, "yetis crossed " + crossed);
    var secretsPlan = Plan("ULDA_721", playerDeck: mixed, bossDeck: crocs); secretsPlan.BossGeneratedSecrets = true;
    int secretsCrossed = 0;
    for (int seed = 1; seed <= 6; seed++) { var gs = Ready(secretsPlan, seed, history: false); secretsCrossed += InDeck(gs.Player2, "EX1_287"); }
    Check("N10 not even a boss whose row turned its generated Secrets on is handed one (its generation list holds every Secret)", secretsCrossed == 0 && rejectsBoss.Contains("EX1_287"));
    var clone = g.Clone();
    Check("N11 a clone keeps the mixed decks and does not mix again", clone.Player1.DeckZone.Select(p => p.Card.Id).SequenceEqual(g.Player1.DeckZone.Select(p => p.Card.Id)) && clone[EncounterRules.StartDone] == 1);
    Check("N12 the helper alone: nothing to mix in a finished or deckless game", EncounterRules.MixDecks(null) == 0);
});

// ============================================================ P. ULDA_724 Spellshields
Guard("P", () =>
{
    var g = Ready(Plan("ULDA_724"));
    Cast(g, "CS2_029", g.Player2.Hero);
    Check("P1 the rule is on both players; the run player casts a spell and gains 2 Armor", Both(g, "ULDA_724") && g.Player1.Hero.Armor == 2 && g.Player2.Hero.Armor == 0);
    Cast(g, "EX1_277");
    Check("P2 each spell gives 2 more", g.Player1.Hero.Armor == 4);
    Mana(g.Player1); Play(g, ToHand(g, g.Player1, "CS2_182"));
    Check("P3 a minion gives none", g.Player1.Hero.Armor == 4);
    ToBoss(g);
    Cast(g, "CS2_023");
    Check("P4 the boss casts a spell and gains 2 Armor", g.Player2.Hero.Armor == 2 && g.Player1.Hero.Armor == 4);
    var g2 = Ready(Plan("ULDA_724"));
    DungeonRunEncounters.PutSecret(g2.Player1, C("EX1_287"));
    ToBoss(g2);
    int bossHand = g2.Player2.HandZone.Count;
    Cast(g2, "CS2_023");
    Check("P5 a countered spell was not cast: no Armor", g2.Player2.Hero.Armor == 0 && g2.Player1.SecretZone.Count == 0 && g2.Player2.HandZone.Count == bossHand, "armor " + g2.Player2.Hero.Armor + " secrets " + g2.Player1.SecretZone.Count);
    var plain = Ready(Plan());
    Cast(plain, "CS2_029", plain.Player2.Hero);
    Check("P6 without the rule: no Armor", plain.Player1.Hero.Armor == 0);
});

// ============================================================ Q. ULDA_801 Plague of Murlocs, ULDA_801t Surprise! Murlocs!
Guard("Q", () =>
{
    int Shuffled(Game x, Controller c) => x.IdEntityDic.Values.Count(e => e.Card.Id == "ULDA_801t" && e.Controller == c);
    var started = Start(Plan("ULDA_801"));
    Check("Q1 nothing is shuffled before the mulligan", Shuffled(started, started.Player1) == 0 && Both(started, "ULDA_801") && started.Player1.AppliedEnchantments.Count(e => e.Card.Id == "ULDA_801e") == 1);
    Finish(started);
    var g = started;
    Check("Q2 outside a run (no boss defeated): one Surprise! Murlocs! for each deck after both mulligans", Shuffled(g, g.Player1) == 1 && Shuffled(g, g.Player2) == 1 && InDeck(g.Player2, "ULDA_801t") == 1 && DungeonRunEncounters.MurlocPlagueCopies(g) == 1);
    RunMatchPlan Fight(int defeated) { var plan = Plan("ULDA_801"); plan.RunContext = new RunContext(defeated, null, null, null, null, null); return plan; }
    var g4 = Ready(Fight(3), 7, history: false);
    var g20 = Ready(Fight(20), 7, history: false);
    Check("Q3 one more for each fight of the run (the fourth fight: 4), at most 8", Shuffled(g4, g4.Player1) == 4 && Shuffled(g4, g4.Player2) == 4 && InDeck(g4.Player2, "ULDA_801t") == 4
        && Shuffled(g20, g20.Player2) == 8 && DungeonRunEncounters.MurlocPlagueMax == 8);
    var bossLedger = Ledger(g4, g4.Player2);
    Check("Q4 the boss's own-deck ledger holds its four (recorded without presentation history too) and exactly its deck", Known(bossLedger, "ULDA_801t") == 4 && bossLedger.Values.Sum() == g4.Player2.DeckZone.Count
        && g4.RunDeckFacts.Count(f => f.Kind == RunDeckFactKind.Rewritten) == 2, string.Join(",", bossLedger.Select(p => p.Key + "x" + p.Value)));
    Check("Q5 the card is the client's: 1 Mana, \"Casts When Drawn\"", C("ULDA_801t").Cost == 1 && C("ULDA_801t").Text == FactText("ULDA_801t") && C("ULDA_801t").Type == CardType.SPELL && C("ULDA_801t").Implemented);
    // Drawn: it casts (1 to 3 Murlocs for the drawer) and another card is drawn.
    var counts = new HashSet<int>(); bool murlocs = true, drew = true, allowed = true;
    for (int seed = 1; seed <= 14; seed++)
    {
        var gs = Ready(Fight(3), seed, history: false);
        ToBoss(gs);
        foreach (var m in gs.Player2.BoardZone.ToArray()) { gs.Player2.BoardZone.Remove(m); gs.Player2.SetasideZone.Add(m); }
        var surprise = gs.Player2.DeckZone.FirstOrDefault(p => p.Card.Id == "ULDA_801t");
        if (surprise == null) continue;
        int hand = gs.Player2.HandZone.Count, left = InDeck(gs.Player2, "ULDA_801t");
        Generic.Draw(gs.Player2, surprise);
        gs.DeathProcessingAndAuraUpdate();
        // A second Surprise may be the replacement card and cast as well.
        int cast = left - InDeck(gs.Player2, "ULDA_801t");
        var board = gs.Player2.BoardZone.ToList();
        if (cast == 1) counts.Add(board.Count);
        murlocs &= board.Count >= cast && board.Count <= 3 * cast && board.All(m => m.Card.IsRace(Race.MURLOC));
        allowed &= board.All(m => m.Card.Implemented && !rejectsBoss.Contains(m.Card.Id));
        drew &= gs.Player2.HandZone.Count == hand + 1 && InHand(gs.Player2, "ULDA_801t") == 0 && gs.Player1.BoardZone.Count == 0;
    }
    Check("Q6 drawn by the boss: 1 to 3 Murlocs for the boss, none for the run player", murlocs && counts.SetEquals(new[] { 1, 2, 3 }), string.Join(",", counts.OrderBy(n => n)));
    Check("Q7 the Murlocs are scripted cards the boss's generation filter allows", allowed);
    Check("Q8 the card is not kept and another card is drawn in its place", drew);
    Mana(g.Player1);
    int handBefore = g.Player1.HandZone.Count;
    var held = ToHand(g, g.Player1, "ULDA_801t");
    bool played = Play(g, held);
    Check("Q9 played from hand (1 Mana): Murlocs, and no card is drawn", played && g.Player1.UsedMana == 1 && g.Player1.BoardZone.Count >= 1 && g.Player1.BoardZone.Count <= 3 && g.Player1.BoardZone.All(m => m.Card.IsRace(Race.MURLOC))
        && g.Player1.HandZone.Count == handBefore);
    while (!g.Player1.BoardZone.IsFull) Board(g, g.Player1, "CS2_182");
    var deckSurprise = g.Player1.DeckZone.FirstOrDefault(p => p.Card.Id == "ULDA_801t");
    if (deckSurprise == null) { Generic.ShuffleIntoDeck(g.Player1, g.Player1.Hero, Entity.FromCard(g.Player1, C("ULDA_801t"))); deckSurprise = g.Player1.DeckZone.First(p => p.Card.Id == "ULDA_801t"); }
    int h = g.Player1.HandZone.Count;
    Generic.Draw(g.Player1, deckSurprise);
    Check("Q10 a full board: no Murloc, a card is still drawn", g.Player1.BoardZone.Count == 7 && g.Player1.HandZone.Count == h + 1);
    Check("Q11 the helper shuffles the copies it is asked for", DungeonRunEncounters.ShuffleSurpriseMurlocs(g.Player2, 2, g.Player2.Hero) == 2 && DungeonRunGlobalRules.CardsOf("ULDA_801").SequenceEqual(new[] { "ULDA_801t" }));
});

// ============================================================ R. ULDA_802 Plague of Madness
Guard("R", () =>
{
    // The run player's board: a Yeti, then a River Crocolisk at the right end. The boss: one Boulderfist Ogre.
    int hero = 0, minion = 0; bool onlyRightmost = true;
    for (int seed = 1; seed <= 16; seed++)
    {
        var gs = Ready(Plan("ULDA_802"), seed, history: false);
        var yeti = Board(gs, gs.Player1, "CS2_182");
        var croc = Board(gs, gs.Player1, "CS2_120");
        var ogre = Board(gs, gs.Player2, "CS2_200");
        EndTurn(gs);
        bool hitHero = gs.Player2.Hero.Damage == 2 && ogre.Damage == 0 && croc.Zone == gs.Player1.BoardZone;
        bool hitOgre = gs.Player2.Hero.Damage == 0 && ogre.Damage == 2 && croc.Zone != gs.Player1.BoardZone;
        if (hitHero) hero++; else if (hitOgre) minion++; else onlyRightmost = false;
        onlyRightmost &= yeti.Damage == 0 && yeti.Zone == gs.Player1.BoardZone;
    }
    Check("R1 at the end of the run player's turn its right-most minion attacks a random enemy: the hero or the minion, nothing else", onlyRightmost && hero + minion == 16 && hero >= 2 && minion >= 2, "hero " + hero + " minion " + minion);
    var g = Ready(Plan("ULDA_802"));
    Check("R2 the rule is on both players", Both(g, "ULDA_802"));
    int before = g.Player2.Hero.Damage;
    EndTurn(g);
    Check("R3 no minion: nothing happens", g.Player2.Hero.Damage == before && g.CurrentPlayer == g.Player2);
    var theirs = Board(g, g.Player2, "CS2_200");
    EndTurn(g);
    Check("R4 at the end of the boss's turn the boss's right-most minion attacks (the only enemy: the run hero)", g.Player1.Hero.Damage == 6 && g.CurrentPlayer == g.Player1);
    // A 0-Attack minion and a Frozen minion attack all the same; a permanent at the right end is passed over.
    var g2 = Ready(Plan("ULDA_802"), 3);
    var bearer = Board(g2, g2.Player1, "EX1_405");
    Board(g2, g2.Player1, "DALA_833t");
    bearer.IsFrozen = true;
    foreach (var m in g2.Player2.BoardZone.ToArray()) Kill(g2, m);
    var wall = Board(g2, g2.Player2, "CS2_200");
    g2.Player2.Hero.IsImmune = true;
    // Whichever enemy is picked, the attack happens: against the Ogre the Shieldbearer takes 6 and dies.
    int tries = 0;
    while (bearer.Zone == g2.Player1.BoardZone && tries++ < 12) { EndTurn(g2); EndTurn(g2); }
    Check("R5 a Frozen 0/4 behind a Cart still attacks (it dies on the Ogre)", bearer.Zone != g2.Player1.BoardZone && wall.Damage == 0, "tries " + tries);
    Check("R6 the shared helper: the right-most minion that can fight, a forced attack only on the other side", EncounterRules.RightmostMinion(g2.Player2) == wall && !EncounterRules.ForcedAttack(wall, g2.Player2.Hero)
        && EncounterRules.ForcedAttackRandomEnemy(null) == null);
    var plain = Ready(Plan());
    Board(plain, plain.Player1, "CS2_182");
    EndTurn(plain);
    Check("R7 without the rule: no attack", plain.Player2.Hero.Damage == 0);
});

// ============================================================ S. ULDA_803 Plague of Wrath
Guard("S", () =>
{
    var g = Ready(Plan("ULDA_803"));
    var yeti = Board(g, g.Player1, "CS2_182");
    Hit(g, yeti, 1);
    Check("S1 the rule is on both players; a damaged Yeti gains +1 Attack (the client's Rage)", Both(g, "ULDA_803") && yeti.AttackDamage == 5 && yeti.Health == 4 && Ench(yeti, "ULDA_803e2") == 1);
    Hit(g, yeti, 1);
    Check("S2 each time", yeti.AttackDamage == 6 && Ench(yeti, "ULDA_803e2") == 2);
    var theirs = Board(g, g.Player2, "CS2_200");
    Hit(g, theirs, 2);
    Check("S3 the boss's minion too", theirs.AttackDamage == 7);
    var squire = Board(g, g.Player1, "EX1_008");
    Hit(g, squire, 1);
    Check("S4 damage a Divine Shield absorbs is no damage", squire.AttackDamage == 1 && !squire.HasDivineShield);
    var doomed = Board(g, g.Player1, "CS2_189");
    Hit(g, doomed, 1);
    Check("S5 a minion that dies of the damage gains nothing", doomed.Zone != g.Player1.BoardZone);
    Hit(g, g.Player1.Hero, 3);
    Check("S6 a damaged hero changes nothing", yeti.AttackDamage == 6 && theirs.AttackDamage == 7);
    var plain = Ready(Plan());
    var py = Board(plain, plain.Player1, "CS2_182");
    Hit(plain, py, 1);
    Check("S7 without the rule: no gain", py.AttackDamage == 4);
});

// ============================================================ T. ULDA_804 Plague of Death, ULDA_804t Eternal Tomb
Guard("T", () =>
{
    var started = Start(Plan("ULDA_804"));
    Check("T1 nothing is on a board before the mulligan", started.Player1.BoardZone.Count == 0 && started.Player2.BoardZone.Count == 0);
    Finish(started);
    var g = started;
    var mine = g.Player1.BoardZone.FirstOrDefault(m => m.Card.Id == "ULDA_804t");
    var theirs = g.Player2.BoardZone.FirstOrDefault(m => m.Card.Id == "ULDA_804t");
    Check("T2 after both mulligans each player has an Eternal Tomb, a permanent", Both(g, "ULDA_804") && mine != null && theirs != null && mine.Untouchable && C("ULDA_804t").Untouchable && C("ULDA_804t").Text == FactText("ULDA_804t"));
    var held = DungeonRunEncounters.Entombed(mine);
    Check("T3 each Tomb holds a minion (public: the mouse-over card) and counts as many deaths as the minion costs", held != null && held.Type == CardType.MINION && DungeonRunEncounters.TombDeathsLeft(mine) == Math.Max(1, held.Cost)
        && mine[GameTag.ALTERNATE_MOUSE_OVER_CARD] == held.AssetId && DungeonRunEncounters.Entombed(theirs) != null, held?.Id + ":" + held?.Cost);
    Check("T4 the held minion is a scripted card the side's generation filter allows", held.Implemented && !rejects.Contains(held.Id) && !rejectsBoss.Contains(DungeonRunEncounters.Entombed(theirs).Id));
    int need = DungeonRunEncounters.TombDeathsLeft(mine);
    int bossNeed = DungeonRunEncounters.TombDeathsLeft(theirs);
    Kill(g, Board(g, g.Player2, "CS2_182"));
    Check("T5 an enemy death does not count for the run player's Tomb (it counts for the boss's)", DungeonRunEncounters.TombDeathsLeft(mine) == need
        && (bossNeed == 1 ? g.Player2.BoardZone.Count >= 1 : DungeonRunEncounters.TombDeathsLeft(theirs) == bossNeed - 1));
    for (int i = 0; i < need - 1; i++) Kill(g, Board(g, g.Player1, "CS2_189"));
    Check("T6 the count goes down with each friendly death and nothing is summoned before it ends", g.Player1.BoardZone.Count == 1 && DungeonRunEncounters.TombDeathsLeft(mine) == 1 && mine[GameTag.TAG_SCRIPT_DATA_NUM_2] == 1);
    Kill(g, Board(g, g.Player1, "CS2_189"));
    var summoned = g.Player1.BoardZone.FirstOrDefault(m => m.Card.Id == held.Id);
    var next = DungeonRunEncounters.Entombed(mine);
    Check("T7 at the last death the Tomb summons its minion and holds a new one", summoned != null && g.Player1.BoardZone.Count == 2 && next != null && DungeonRunEncounters.TombDeathsLeft(mine) == Math.Max(1, next.Cost),
        held.Id + " then " + next?.Id);
    Hit(g, mine, 10);
    Check("T8 the Tomb cannot be damaged or removed", mine.Zone == g.Player1.BoardZone && mine.Damage == 0);
    // A full board loses the summon; the Tomb starts again.
    while (!g.Player1.BoardZone.IsFull) Board(g, g.Player1, "CS2_182");
    var waiting = DungeonRunEncounters.Entombed(mine);
    mine[GameTag.SCORE_VALUE_1] = 1;
    var victim = g.Player1.BoardZone.First(m => m.Card.Id == "CS2_182");
    mine[EncounterRules.RuleTombSummons] = DungeonRunEncounters.TombSummonsPerTurn;
    Kill(g, victim);
    Check("T9 the turn's bound (7 summons of one Tomb): the minion waits for a death of a later turn", g.Player1.BoardZone.Count == 6 && DungeonRunEncounters.Entombed(mine) == waiting && DungeonRunEncounters.TombDeathsLeft(mine) == 1);
    EndTurn(g);
    Check("T10 the bound starts again at the end of a turn", mine[EncounterRules.RuleTombSummons] == 0);
    Kill(g, g.Player1.BoardZone.First(m => m.Card.Id == "CS2_182"));
    Check("T11 the next death summons it", g.Player1.BoardZone.Any(m => m.Card.Id == waiting.Id) && mine[EncounterRules.RuleTombSummons] == 1);
    var seen = new HashSet<string>();
    for (int seed = 1; seed <= 10; seed++) { var gs = Ready(Plan("ULDA_804"), seed, history: false); seen.Add(DungeonRunEncounters.Entombed(gs.Player1.BoardZone.First(m => m.Card.Id == "ULDA_804t")).Id); }
    Check("T12 the held minion is random (ten seeds, many different minions)", seen.Count >= 6, seen.Count + " distinct");
    var clone = g.Clone();
    var cm = clone.Player1.BoardZone.First(m => m.Card.Id == "ULDA_804t");
    Check("T13 a clone keeps the Tomb's minion and count", DungeonRunEncounters.Entombed(cm) == DungeonRunEncounters.Entombed(mine) && DungeonRunEncounters.TombDeathsLeft(cm) == DungeonRunEncounters.TombDeathsLeft(mine));
});

// ============================================================ the run layer
string dataRoot = Path.Combine(repo, "data");
string RD(string f) => File.ReadAllText(Path.Combine(dataRoot, f));
DungeonRunData Load(string schedule = null) => DungeonRunData.Parse(RD("dungeon-run/run-v1.json"), RD("dungeon-run/pool-v1.json"), RD("npc/decks-v1.json"), RD("dungeon-run/catalog-v1.json"),
    schedule ?? RD("dungeon-run/schedule-v1.json"), RD("dungeon-run/tavern-v1.json"), RD("dungeon-run/encounters-v1.json"));
DungeonRunProfile All(DungeonRunData d) { var p = new DungeonRunProfile(); p.Unlocked.AddRange(d.Unlocks.Keys); return p; }
DungeonRunState V3(DungeonRun r, string cls, long seed, string schedule = "acts-v3h")
{
    var c = r.Data.Class(cls);
    return r.NewRun(All(r.Data), c.Id, c.Decks[0], c.Powers[0], seed, schedule: schedule);
}
// Custom band-1 bosses on the first act (no flag, no encounter rule of their own).
void Plain(DungeonRun r, DungeonRunState s)
{
    var plainBosses = r.Data.Bosses.Where(b => !b.IsEncounter && b.Band == 1 && b.Unlock == null).ToList();
    foreach (int f in Enumerable.Range(0, 5)) s.BossSequence[f] = plainBosses[f % plainBosses.Count].Id;
}
// Walks the run's nodes to the pre-match of its current floor (an anomaly node: no rule; drafts and taverns: the first offer).
void ToFight(DungeonRun r, DungeonRunState s, Func<DungeonRunState, string> bundle = null)
{
    while (s.Phase != DungeonRunPhase.PreMatch)
    {
        switch (s.Phase)
        {
            case DungeonRunPhase.TreasureDraft: r.PickTreasure(s, s.OpenDraft.Offered[0]); break;
            case DungeonRunPhase.Modifier: r.PickModifier(s, s.ModifierOffer.Kind == DungeonRunGlobalRule.KindAnomaly ? DungeonRunGlobalRule.None : s.ModifierOffer.Offered[0]); break;
            case DungeonRunPhase.Tavern: r.LeaveTavern(s); break;
            case DungeonRunPhase.BundleDraft:
                if (s.OpenDraft.Takes == 0) { r.PickBundle(s, bundle?.Invoke(s) ?? s.OpenDraft.Offered[0]); break; }
                string pick = s.OpenDraft.Choosing ?? bundle?.Invoke(s) ?? s.OpenDraft.Offered[0];
                var left = r.OfferCards(s.OpenDraft, pick);
                foreach (string kept in s.OpenDraft.Chosen) left.Remove(kept);
                r.PickDraftCard(s, pick, left[0]);
                break;
            default: throw new Exception("walk stopped at " + s.Phase);
        }
    }
}
Game Fight(RunMatchPlan plan, long seed)
{
    var cfg = new GameConfig { RandomSeed = (int)(seed % int.MaxValue), Shuffle = true, SkipMulligan = false, History = false, Logging = false, FormatType = FormatType.FT_WILD };
    DungeonRunSetup.Configure(cfg, plan);
    var g = new Game(cfg);
    g.StartGame();
    DungeonRunSetup.ApplyAfterStart(g, plan);
    Finish(g);
    return g;
}

// ============================================================ U. ULDA_720 Battle Scars (a run-scope rule of the run layer)
Guard("U", () =>
{
    var data = Load();
    var run = new DungeonRun(data);
    var profile = All(data);
    var s = V3(run, "MAGE", 81001);
    Plain(run, s);
    s.ActiveRules.Add(new DungeonRunRule { Id = "ULDA_720", Act = 0, FromNode = -1, Kind = DungeonRunGlobalRule.KindAnomaly });
    ToFight(run, s);
    Check("U1 the rule is active on the run and is not a match rule (no script, never in a plan's rule list)", run.RuleActive(s, "ULDA_720") && !DungeonRunGlobalRules.IsScripted("ULDA_720") && data.Schedules.Rule("ULDA_720").Effect == "run"
        && data.Schedules.Rule("ULDA_720").RunScope);
    var plan = run.BuildPlan(s, profile);
    Check("U2 the first fight's plan: the run hero has 100 Health and no damage", plan.PlayerHeroHealth == 100 && plan.PlayerCarriedDamage == 0 && plan.GlobalRules.Count == 0 && s.HeroDamage == 0);
    var g = Fight(plan, run.MatchSeed(s));
    Check("U3 in the match the run hero starts at 100 Health; the boss's Health is its own", g.Player1.Hero.BaseHealth == 100 + run.PlayerHealthBonus(s) && g.Player1.Hero.Damage == 0 && g.Player2.Hero.Health == plan.BossHealth);
    run.BeginMatch(s);
    g.Player1.Hero.Damage = 37; g.Player1.Hero.Armor = 9;
    run.RecordOutcome(s, profile, DungeonRunOutcome.Win, g.RunResult, DungeonRunSetup.Harvest(g));
    Check("U4 a won fight: the damage the hero ended with is kept on the run", s.HeroDamage == 37 && s.Phase != DungeonRunPhase.Defeat && s.Phase != DungeonRunPhase.Victory);
    ToFight(run, s);
    var plan2 = run.BuildPlan(s, profile);
    var g2 = Fight(plan2, run.MatchSeed(s));
    Check("U5 the next fight starts with that damage (63 of 100 Health) and no Armor", plan2.PlayerHeroHealth == 100 && plan2.PlayerCarriedDamage == 37 && g2.Player1.Hero.Health == g2.Player1.Hero.BaseHealth - 37 && g2.Player1.Hero.Armor == 0);
    run.BeginMatch(s);
    g2.Player1.Hero.Damage = 12;
    run.RecordOutcome(s, profile, DungeonRunOutcome.Win, g2.RunResult, DungeonRunSetup.Harvest(g2));
    Check("U6 healing inside a fight counts: the run keeps what the hero ended with", s.HeroDamage == 12);
    var xml = new System.Xml.Serialization.XmlSerializer(typeof(DungeonRunState));
    var text = new StringWriter();
    xml.Serialize(text, s);
    var copy = (DungeonRunState)xml.Deserialize(new StringReader(text.ToString()));
    Check("U7 the carried damage is saved with the run", copy.HeroDamage == 12 && copy.ActiveRules.Any(r => r.Id == "ULDA_720" && r.Act == 0 && r.Kind == "anomaly") && text.ToString().Contains("HeroDamage=\"12\""));
    // Without the rule: the class's own Health and nothing carried.
    var plainRun = V3(run, "MAGE", 81001);
    Plain(run, plainRun);
    ToFight(run, plainRun);
    var plainPlan = run.BuildPlan(plainRun, profile);
    var pg = Fight(plainPlan, run.MatchSeed(plainRun));
    run.BeginMatch(plainRun);
    pg.Player1.Hero.Damage = 20;
    run.RecordOutcome(plainRun, profile, DungeonRunOutcome.Win, pg.RunResult, DungeonRunSetup.Harvest(pg));
    Check("U8 without the rule: 30 Health and nothing is carried", plainPlan.PlayerHeroHealth != 100 && plainPlan.PlayerCarriedDamage == 0 && pg.Player1.Hero.BaseHealth == 30 + run.PlayerHealthBonus(plainRun) && plainRun.HeroDamage == 0);
    // Offered at act I only, and picked like any anomaly: it lasts the run (Act 0).
    var probe = V3(run, "MAGE", 81002);
    var sch = run.ScheduleFor(probe);
    Check("U9 a run-scope rule is a candidate of act I only", run.RuleCandidates(probe, sch, 1, "anomaly").Any(r => r.Id == "ULDA_720") && !run.RuleCandidates(probe, sch, 2, "anomaly").Any(r => r.Id == "ULDA_720")
        && !run.RuleCandidates(probe, sch, 3, "anomaly").Any(r => r.Id == "ULDA_729"));
    DungeonRunState offeredRun = null;
    for (long seed = 1; seed <= 600 && offeredRun == null; seed++)
    {
        var t = V3(run, "MAGE", 82000 + seed);
        if (t.ModifierOffer?.Kind == "anomaly" && t.ModifierOffer.Offered.Contains("ULDA_720")) offeredRun = t;
    }
    if (offeredRun != null) run.PickModifier(offeredRun, "ULDA_720");
    Check("U10 picked at the act I anomaly node it is an active rule of the whole run", offeredRun != null && offeredRun.ActiveRules.Any(r => r.Id == "ULDA_720" && r.Act == 0 && r.Kind == "anomaly") && run.RuleActive(offeredRun, "ULDA_720"));
});

// ============================================================ V. ULDA_729 Multiclassing (a run-scope rule of the run layer)
Guard("V", () =>
{
    var data = Load();
    var run = new DungeonRun(data);
    var profile = All(data);
    var s = V3(run, "MAGE", 83001);
    Check("V1 without the rule the loot classes are the run's own", run.LootClasses(s).SequenceEqual(new[] { "MAGE" }) && run.MatchClasses(s).SequenceEqual(new[] { "MAGE" }));
    s.ActiveRules.Add(new DungeonRunRule { Id = "ULDA_729", Act = 0, FromNode = -1, Kind = DungeonRunGlobalRule.KindAnomaly });
    var loot = run.LootClasses(s);
    Check("V2 with the rule: every class, the run's own first", loot.Count == data.ClassRows.Count() && loot[0] == "MAGE" && loot.Contains("WARRIOR") && loot.Contains("DEMONHUNTER") && loot.Distinct().Count() == loot.Count,
        string.Join(",", loot));
    Check("V3 the run's own classes, its class Discovers and its hero are unchanged; the boss's Secret prior covers every class", run.RunClasses(s).SequenceEqual(new[] { "MAGE" })
        && run.RunClassesOf(s).SequenceEqual(new[] { CardClass.MAGE }) && run.MatchClasses(s).SequenceEqual(loot) && s.Class == "MAGE");
    Check("V4 it is a run-layer rule: no match script, effect run, scope run", !DungeonRunGlobalRules.IsScripted("ULDA_729") && data.Schedules.Rule("ULDA_729").Effect == "run" && data.Schedules.Rule("ULDA_729").RunScope);
    // The card drafts: with the rule, entries of other classes are offered; without it, never.
    bool OffClass(DungeonRun r, string id) { var b = r.Data.Bundles.First(x => x.Id == id); return !b.IsNeutral && !b.ClassSet.Contains("MAGE"); }
    int with = 0, without = 0, drafts = 0;
    DungeonRunState picked = null; string pickedBundle = null;
    for (long seed = 1; seed <= 12; seed++)
    {
        foreach (bool rule in new[] { true, false })
        {
            var t = V3(run, "MAGE", 83100 + seed);
            Plain(run, t);
            if (rule) t.ActiveRules.Add(new DungeonRunRule { Id = "ULDA_729", Act = 0, FromNode = -1, Kind = DungeonRunGlobalRule.KindAnomaly });
            ToFight(run, t);
            run.BeginMatch(t);
            run.RecordOutcome(t, profile, DungeonRunOutcome.Win);
            while (t.Phase == DungeonRunPhase.TreasureDraft) run.PickTreasure(t, t.OpenDraft.Offered[0]);
            if (t.Phase != DungeonRunPhase.BundleDraft) continue;
            drafts++;
            int off = t.OpenDraft.Offered.Count(id => OffClass(run, id));
            if (rule) { with += off; if (off > 0 && picked == null) { picked = t; pickedBundle = t.OpenDraft.Offered.First(id => OffClass(run, id)); } }
            else without += off;
        }
    }
    Check("V5 over 12 seeds the first card draft offers other classes' loot with the rule (" + with + " entries) and never without it", drafts == 24 && with >= 6 && without == 0);
    Check("V6 a draft with another class's entry was found", picked != null);
    int deckBefore = picked.Deck.Count;
    ToFight(run, picked, _ => pickedBundle);
    var added = picked.Cards.Skip(deckBefore).ToList();
    var foreign = added.FirstOrDefault(c => { var card = C(c.Id); return card.Class != CardClass.MAGE && card.Class != CardClass.NEUTRAL; });
    Check("V7 the picked cards of another class are in the run deck", added.Count > 0 && foreign != null, string.Join(",", added.Select(c => c.Id)));
    Check("V8 the deck accepts a copy of such a card while the rule is active", DungeonRunDeck.CopyError(run, picked, foreign.Uid) == null);
    var plan = run.BuildPlan(picked, profile);
    var pilot = DungeonRunBossPilot.Build(run.CurrentBoss(picked), plan, data, runClasses: run.MatchClasses(picked));
    var g = Fight(plan, run.MatchSeed(picked));
    Check("V9 the fight's plan holds the card; no match rule is added and the match starts", plan.PlayerDeck.Any(c => c.Id == foreign.Id) && plan.GlobalRules.Count == 0 && pilot.Plan != null && g.Player1.Hero.Card.Id == plan.PlayerHero.Id);
    picked.ActiveRules.RemoveAll(r => r.Id == "ULDA_729");
    Check("V10 without the rule the copy is refused for its class", DungeonRunDeck.CopyError(run, picked, foreign.Uid) != null && run.LootClasses(picked).Count == 1);
});

// ============================================================ W. the framework and the run layer
string[] family = { "ULDA_702", "ULDA_705", "ULDA_706", "ULDA_707", "ULDA_708", "ULDA_709", "ULDA_710", "ULDA_711", "ULDA_712", "ULDA_713", "ULDA_714", "ULDA_715", "ULDA_719", "ULDA_721", "ULDA_724",
    "ULDA_801", "ULDA_802", "ULDA_803", "ULDA_804" };
string[] runRules = { "ULDA_720", "ULDA_729" };
string[] hooksOnly = { "ULDA_715", "ULDA_719", "ULDA_721", "ULDA_804" };
string[] twists = { "ULDA_801", "ULDA_802", "ULDA_803", "ULDA_804" };
string[] tokens = { "ULDA_801t", "ULDA_804t" };
string[] dummies = { "ULDA_702d", "ULDA_705d", "ULDA_707d", "ULDA_708d", "ULDA_712d", "ULDA_713d", "ULDA_721d", "ULDA_724d", "ULDA_801d", "ULDA_803d" };
Guard("W", () =>
{
    Check("W1 the scripted Tombs rules are this family's nineteen (the two run-layer rules have no match script)", DungeonRunGlobalRules.Scripted.Where(id => id.StartsWith("ULDA_")).OrderBy(id => id, StringComparer.Ordinal)
        .SequenceEqual(family.OrderBy(id => id, StringComparer.Ordinal)) && runRules.All(id => !DungeonRunGlobalRules.IsScripted(id) && DungeonRunEncounters.Find(id) == null), string.Join(",", DungeonRunGlobalRules.Scripted));
    foreach (string id in family)
    {
        bool hooks = hooksOnly.Contains(id);
        string effect = DungeonRunGlobalRules.EffectOf(id);
        Check("W2 " + id + ": the rule card carries the client's text" + (hooks ? " and is hooks only (the client has no player enchantment for it)" : " and its player enchantment is the client's"),
            C(id).Text == FactText(id) && C(id).Implemented && C(id).Type == CardType.SPELL
            && (hooks ? effect == null && !factById.ContainsKey(id + "e") : effect == id + "e" && C(effect).Implemented && C(effect).Type == CardType.ENCHANTMENT && factById.ContainsKey(effect)),
            C(id).Text.Replace('\n', ' '));
    }
    Check("W3 the tokens and the card enchantments are scripted client cards; the client's Dummy cards are not created", tokens.All(id => C(id).Implemented && factById.ContainsKey(id))
        && new[] { "ULDA_708e2", "ULDA_710e2", "ULDA_711e2", "ULDA_803e2" }.All(id => C(id).Implemented && C(id).Type == CardType.ENCHANTMENT)
        && dummies.All(id => factById.ContainsKey(id) && DungeonRunEncounters.Find(id) == null));
    Check("W4 the family's private tags are inside the encounter range (never sent to the client)", EncounterRules.IsPrivateTag(EncounterRules.HeroDamageDouble) && EncounterRules.IsPrivateTag(EncounterRules.RuleTombSummons)
        && (int)EncounterRules.HeroDamageDouble == 32641 && (int)EncounterRules.RuleTombSummons >= 32680 && (int)EncounterRules.RuleTombSummons <= EncounterRules.RuleTagsTo);
    var two = Plan("ULDA_804"); two.GlobalRules.Add("ULDA_709");
    var g = Ready(two);
    var y = Board(g, g.Player1, "CS2_182");
    Check("W5 a twist and an anomaly together: both active on both players, each doing its part", Both(g, "ULDA_804") && Both(g, "ULDA_709") && y.AttackDamage == 5 && g.Player1.BoardZone.Count(m => m.Card.Id == "ULDA_804t") == 1);

    var data = Load();
    var run = new DungeonRun(data);
    var errors = DungeonRunValidator.Validate(data, null);
    Check("W6 the shipped data validates with the family's rule rows", errors.Count == 0, string.Join(" | ", errors.Take(4)));
    var rules = data.Schedules.Rules;
    Check("W7 schedule-v1.json lists the 17 anomalies and the 4 plague twists of Tombs of Terror, all admitted", rules.Count(r => r.Provenance == "ULDA" && r.Kind == "anomaly") == 17
        && rules.Where(r => r.Provenance == "ULDA" && r.Kind == "twist").Select(r => r.Id).SequenceEqual(twists) && rules.Where(r => r.Provenance == "ULDA").All(r => r.Admitted)
        && rules.Where(r => r.RunScope).Select(r => r.Id).SequenceEqual(runRules));
    Check("W8 every rule of the family is admitted as a rule only and never generated", family.Concat(runRules).All(id => DungeonRunAdmission.Classify(null, id, AdmitScope.Rule, null, true).Admitted
        && !DungeonRunAdmission.Classify(null, id, AdmitScope.Player, null, true).Admitted && !DungeonRunAdmission.Classify(null, id, AdmitScope.Boss, null, true).Admitted && rejects.Contains(id)));
    Check("W9 the tokens are admitted for a rule only and never generated", tokens.All(id => DungeonRunAdmission.Classify(null, id, AdmitScope.Rule, null, true).Admitted
        && !DungeonRunAdmission.Classify(null, id, AdmitScope.Boss, null, true).Admitted && rejects.Contains(id)));
    Check("W10 no shipped act names a plague in its twist pool yet (the close-out fills the pools)", data.Schedules.Schedules.Values.All(sc => sc.Acts.All(a => !a.TwistPool.Any(twists.Contains))));
    // The anomaly offer: every admitted Tombs anomaly can be rolled at act I, a twist never.
    var offered = new HashSet<string>();
    for (long seed = 1; seed <= 900; seed++)
    {
        var s = V3(run, "MAGE", 90000 + seed);
        if (s.ModifierOffer?.Kind == "anomaly") foreach (string id in s.ModifierOffer.Offered) offered.Add(id);
    }
    // Act I of every run draws War Master Voone (the only Rumble row so far: a shrine) and Manhunter Ivan (the only Monster
    // Hunt combat row: Secrets from its deck), which exclude No-Face and Aleatory there; an act without them offers both.
    var mine = rules.Where(r => r.Provenance == "ULDA" && r.Kind == "anomaly").Select(r => r.Id).ToList();
    var here = mine.Where(id => id != "ULDA_706" && id != "ULDA_721").ToList();
    Check("W11 over 900 seeds the act I anomaly node offers each of the other 15 Tombs anomalies and never a twist", here.Count == 15 && here.All(offered.Contains) && !offered.Any(twists.Contains), string.Join(",", here.Where(id => !offered.Contains(id))));
    var quiet = V3(run, "MAGE", 90001);
    Plain(run, quiet);
    var quietRules = run.RuleCandidates(quiet, run.ScheduleFor(quiet), 1, "anomaly").Select(r => r.Id).ToList();
    Check("W11b an act of bosses without flags may be offered all 17", mine.All(quietRules.Contains), string.Join(",", mine.Where(id => !quietRules.Contains(id))));
    // Incompatibility with the pre-rolled bosses of the act (their flags).
    DungeonRunState WithBoss(string cls, long seed, string bossId, out int act)
    {
        var s = V3(run, cls, seed);
        var schedule = run.ScheduleFor(s);
        int floor = s.BossSequence.IndexOf(bossId) + 1;
        if (floor == 0) { floor = 2; s.BossSequence[1] = bossId; }
        act = schedule.Nodes.First(n => n.IsFight && n.Floor == floor).Act;
        return s;
    }
    List<string> Candidates(DungeonRun r, DungeonRunState s, int act, string kind) => r.RuleCandidates(s, r.ScheduleFor(s), act, kind).Select(x => x.Id).ToList();
    var voone = WithBoss("MAGE", 91001, "trl_200_voone", out int vooneAct);
    Check("W12 an act with a shrine boss (Voone) is never offered No-Face; the others stay possible", !Candidates(run, voone, vooneAct, "anomaly").Contains("ULDA_706") && Candidates(run, voone, vooneAct, "anomaly").Contains("ULDA_709"));
    var icarax = WithBoss("MAGE", 91002, "ulda_40_icarax", out int icaraxAct);
    Check("W13 an act with a deck-replacing boss (Icarax) is never offered Aleatory and never gets Plague of Murlocs", !Candidates(run, icarax, icaraxAct, "anomaly").Contains("ULDA_721")
        && !Candidates(run, icarax, icaraxAct, "twist").Contains("ULDA_801") && Candidates(run, icarax, icaraxAct, "twist").Contains("ULDA_802"));
    var ivan = WithBoss("MAGE", 91003, "gila_25_ivan", out int ivanAct);
    var hagatha = WithBoss("MAGE", 91004, "gila_61_hagatha", out int hagathaAct);
    Check("W14 a boss that plays Secrets from its deck (Ivan) or a hero-switch fight (Hagatha) excludes Aleatory", !Candidates(run, ivan, ivanAct, "anomaly").Contains("ULDA_721")
        && !Candidates(run, hagatha, hagathaAct, "anomaly").Contains("ULDA_721") && Candidates(run, ivan, ivanAct, "anomaly").Contains("ULDA_705"));
    var flagged = Load();
    var custom = flagged.Bosses.First(b => !b.IsEncounter && b.Band == 1 && b.Unlock == null);
    custom.Flags.AddRange(new[] { "heroImmune", "damageCap", "boardStart", "relay" });
    var fr = new DungeonRun(flagged);
    var fs = V3(fr, "MAGE", 91005);
    fs.BossSequence[0] = custom.Id;
    var left = Candidates(fr, fs, 1, "anomaly");
    var leftTwists = Candidates(fr, fs, 1, "twist");
    Check("W15 boss flags exclude the rule that would clash: heroImmune No-Face, damageCap In the Face, boardStart Marin's Return and Plague of Death, relay Aleatory and Plague of Murlocs",
        !left.Contains("ULDA_706") && !left.Contains("ULDA_714") && !left.Contains("ULDA_719") && !left.Contains("ULDA_721") && left.Contains("ULDA_702") && left.Contains("ULDA_720")
        && !leftTwists.Contains("ULDA_804") && !leftTwists.Contains("ULDA_801") && leftTwists.Contains("ULDA_802") && leftTwists.Contains("ULDA_803"), string.Join(",", left) + " | " + string.Join(",", leftTwists));
    var shrineDeck = data.Decks.First(x => x.Shrine != null && data.FindClass(x.Class) != null);
    var shrineRun = run.NewRun(All(data), shrineDeck.Class, shrineDeck.Id, data.Class(shrineDeck.Class).Powers[0], 91006, schedule: "acts-v3h");
    Plain(run, shrineRun);
    Check("W16 a run with a board shrine is never offered No-Face", shrineRun.HasBoardShrine && !Candidates(run, shrineRun, 1, "anomaly").Contains("ULDA_706") && Candidates(run, shrineRun, 1, "anomaly").Contains("ULDA_714"));
    // Step 9 (lane R, C2): the act I anomaly offer NewRun stores is drawn after the board shrine is set (the ordering fix), so the
    // stored offer itself never holds No-Face (W16 above reads the candidates afterwards, which held before the fix too).
    int storedOffers = 0, storedNoFace = 0;
    var shrineDecks = data.Decks.Where(x => x.Shrine != null && data.FindClass(x.Class) != null).ToList();
    foreach (var deck in shrineDecks)
        for (int k = 0; k < 20; k++)
        {
            var stored = run.NewRun(All(data), deck.Class, deck.Id, data.Class(deck.Class).Powers[0], 93000 + 100 * shrineDecks.IndexOf(deck) + k, schedule: "acts-v3h");
            if (!stored.HasBoardShrine || stored.ModifierOffer?.Kind != DungeonRunGlobalRule.KindAnomaly) continue;
            storedOffers++;
            if (stored.ModifierOffer.Offered.Contains("ULDA_706")) storedNoFace++;
        }
    Check("W16b the act I anomaly offer a new shrine run stores never holds No-Face (27 shrine decks x 20 seeds)", storedOffers == 27 * 20 && storedNoFace == 0, storedOffers + " offers, " + storedNoFace + " with ULDA_706");
    // Rule against rule, both ways.
    DungeonRunState With(string rule, string kind)
    {
        var s = V3(run, "MAGE", 91007);
        Plain(run, s);
        s.ActiveRules.Add(new DungeonRunRule { Id = rule, Act = 1, FromNode = -1, Kind = kind });
        return s;
    }
    Check("W17 The Stench (a twist) excludes Angelic Presence", !Candidates(run, With("DALA_831", "twist"), 1, "anomaly").Contains("ULDA_709") && Candidates(run, With("DALA_833", "twist"), 1, "anomaly").Contains("ULDA_709"));
    var death = With("ULDA_804", "twist");
    Check("W18 the board-start rules exclude one another: Plague of Death active, no Marin's Return and no Heist board twist", !Candidates(run, death, 1, "anomaly").Contains("ULDA_719")
        && !Candidates(run, death, 1, "twist").Any(id => id == "DALA_829" || id == "DALA_832" || id == "DALA_833") && !Candidates(run, With("DALA_833", "twist"), 1, "anomaly").Contains("ULDA_719")
        && Candidates(run, With("ULDA_803", "twist"), 1, "anomaly").Contains("ULDA_719"));
    Check("W19 Plague of Murlocs excludes Aleatory", !Candidates(run, With("ULDA_801", "twist"), 1, "anomaly").Contains("ULDA_721") && Candidates(run, With("ULDA_802", "twist"), 1, "anomaly").Contains("ULDA_721"));
    var road = V3(run, "MAGE", 91008);
    Plain(run, road);
    road.ActiveRules.Add(new DungeonRunRule { Id = "mod_open_road", Act = 1, FromNode = -1 });
    Check("W20 Bountiful and the opening-hand modifier exclude each other", !Candidates(run, road, 1, "anomaly").Contains("ULDA_715") && data.Schedules.Rule("ULDA_715").IncompatibleModifiers.SequenceEqual(new[] { "mod_open_road" }));
    // The validator's checks.
    string Refused(Action<DungeonRunData> change)
    {
        var d = Load(); change(d);
        return string.Join(" | ", DungeonRunValidator.Validate(d, null));
    }
    Check("W21 the validator refuses a match effect for a rule with no script (ULDA_720) and an unknown modifier", Refused(d => d.Schedules.Rule("ULDA_720").Effect = "match").Contains("rule ULDA_720: not scripted")
        && Refused(d => d.Schedules.Rule("ULDA_715").IncompatibleModifiers.Add("mod_nothing")).Contains("unknown modifier mod_nothing"));
    var known = new HashSet<string>(factById.Keys, StringComparer.Ordinal); known.Remove("ULDA_804t");
    var clientErrors = DungeonRunValidator.Validate(data, id => known.Contains(id));
    Check("W22 the validator checks that the client knows each card a rule creates", clientErrors.Any(e => e.StartsWith("ULDA_804t:") && e.Contains("rule ULDA_804")), string.Join(" | ", clientErrors.Take(3)));
    // The twist roll through a fixture schedule (the shipped acts have empty twist pools: the close-out fills them).
    var fixture = Load(File.ReadAllText(Path.Combine(fixtures, "schedule-plagues.json")));
    var fixtureErrors = DungeonRunValidator.Validate(fixture, null);
    var tr = new DungeonRun(fixture);
    var rolledIds = new Dictionary<string, int>(); int wrong = 0;
    for (long seed = 1; seed <= 300; seed++)
    {
        var s = V3(tr, "MAGE", 92000 + seed);
        // The close-out ships the Heist finale (act IV), whose own twist DALA_830 The Finale every run holds: the fixture's
        // filled pools are acts I-III's.
        foreach (var rule in s.ActiveRules.Where(r => r.Kind == "twist" && !tr.ScheduleFor(s).Acts[r.Act - 1].IsFinale))
        {
            rolledIds[rule.Id] = rolledIds.TryGetValue(rule.Id, out int n) ? n + 1 : 1;
            var bosses = tr.ScheduleFor(s).Nodes.Where(n => n.Act == rule.Act && n.IsFight).Select(n => fixture.Boss(s.BossSequence[n.Floor - 1])).ToList();
            if (rule.Id == "ULDA_801" && bosses.Any(b => b.HasFlag("deckReplace") || b.HasFlag("relay"))) wrong++;
        }
    }
    Check("W23 the fixture schedule (the four plagues in every twist pool, chance 100) validates and rolls each of them", fixtureErrors.Count == 0 && rolledIds.Count == 4 && rolledIds.Values.All(n => n > 20),
        string.Join(" | ", fixtureErrors.Take(2)) + string.Join(",", rolledIds.Select(p => p.Key + "x" + p.Value)));
    Check("W24 a rolled plague is never one a boss of its act excludes", wrong == 0);
    var first = V3(tr, "MAGE", 92001);
    var twist = first.ActiveRules.First(r => r.Kind == "twist" && r.Act == 1);
    Plain(tr, first);
    ToFight(tr, first);
    var plan = tr.BuildPlan(first, All(fixture));
    var fight = Fight(plan, tr.MatchSeed(first));
    Check("W25 the rolled plague is on the fight's plan and active in the match (" + twist.Id + ")", plan.GlobalRules.SequenceEqual(new[] { twist.Id }) && Both(fight, twist.Id));
    Check("W26 the rule page shows each rule card's own name and text", family.Concat(runRules).All(id => NativeDungeonRunText.RuleText(id) == DungeonRunValidator.MatchText(C(id).Text) && NativeDungeonRunText.RuleText(id).Length > 0
        && NativeDungeonRunText.RuleName(id) == C(id).Name && (C(id).Name.StartsWith("Anomaly - ") || C(id).Name.StartsWith("Twist - "))));
});

Console.WriteLine($"RESULT {passes} passed, {failures} failed");
return failures;
