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

// Phase 8 family 01, the encounter model in the engine (real engine games through DungeonRunSetup): boss forms (Health,
// death and script forms; deck replacement; the own-deck fact), the start block, the run player's relay, carried damage, the
// boss board shrine (and Typhoon against a shrine), the boss's generated Secrets, the fight-only hero cards, the three
// anomalies and each scripted power and card's text. Covers the design's c-enc-forms, c-enc-start, c-enc-secrets,
// c-enc-ledger and c-rules suites.
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
var facts = System.Text.Json.JsonDocument.Parse(File.ReadAllText(VerifyPaths.Repo("facts/violet-hold-2026-09.json")));
var factById = new Dictionary<string, System.Text.Json.JsonElement>(StringComparer.Ordinal);
foreach (var c in facts.RootElement.GetProperty("cards").EnumerateArray()) factById[c.GetProperty("id").GetString()] = c;

// A plain run plan: Mage player, a boss hero and power, 30 cheap cards each (no draw effects that matter).
string[] filler = Enumerable.Repeat("CS2_182", 30).ToArray(); // Chillwind Yeti
RunMatchPlan Plan(string bossHero = "HERO_01", string bossPower = "CS2_102", int bossHealth = 30, string[] bossDeck = null) => new RunMatchPlan
{
    PlayerHero = C("HERO_08"), PlayerHeroPowerId = "CS2_034", PlayerDeck = filler.Select(C).ToList(),
    BossHero = C(bossHero), BossHeroPowerId = bossPower, BossHealth = bossHealth, BossDeck = (bossDeck ?? filler).Select(C).ToList(),
    PlayerGeneratedSecrets = true,
    ExcludedGenerated = DungeonRunAdmission.GeneratedRejects(true), ExcludedGeneratedBoss = DungeonRunAdmission.GeneratedRejectsBoss(true),
};
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
Game Ready(RunMatchPlan plan, int seed = 7) { var g = Start(plan, seed); Finish(g); return g; }
void Mana(Controller c, int n) { c.BaseMana = n; c.UsedMana = 0; c.OverloadLocked = 0; c.OverloadOwed = 0; }
IPlayable ToHand(Game g, Controller c, string id)
{
    while (c.HandZone.Count >= 9) { var old = c.HandZone[0]; c.HandZone.Remove(old); c.SetasideZone.Add(old); }
    var e = Entity.FromCard(c, C(id)); Generic.AddHandPhase.Invoke(c, e); g.DeathProcessingAndAuraUpdate(); return e;
}
Minion Board(Game g, Controller c, string id) { var m = (Minion)Entity.FromCard(c, C(id)); Generic.SummonBlock(g, m, -1, c.Hero); g.DeathProcessingAndAuraUpdate(); return m; }
bool Play(Game g, IPlayable card, ICharacter target = null) => g.Process(PlayCardTask.Any(g.CurrentPlayer, card, target, -1, 0));
void EndTurn(Game g) => g.Process(EndTurnTask.Any(g.CurrentPlayer));
bool Power(Game g, ICharacter target = null) => g.Process(HeroPowerTask.Any(g.CurrentPlayer, target));
void Hit(Game g, ICharacter target, int amount) { Generic.DamageCharFunc(g.CurrentPlayer.Hero, target, amount, false); g.DeathProcessingAndAuraUpdate(); }
// The boss's turn: end the player's turn so that the boss is the current player.
void ToBoss(Game g) { if (g.CurrentPlayer == g.Player1) EndTurn(g); }
void ToPlayer(Game g) { if (g.CurrentPlayer == g.Player2) EndTurn(g); }

// ============================================================ A. Health forms (the Plague Lord shape)
RunMatchPlan Icarax(int start = 0, int carried = 0)
{
    var plan = Plan("ULDA_BOSS_40h", "ULDA_BOSS_40p1", 300);
    var deck2 = Enumerable.Repeat("CS2_120", 30).ToArray(); // River Crocolisk
    var deck3 = Enumerable.Repeat("CS2_172", 30).ToArray(); // Bloodfen Raptor
    plan.EncounterId = "ulda_40_icarax"; plan.EncounterDeck = true;
    plan.BossForms = new List<RunForm>
    {
        new RunForm { Hero = C("ULDA_BOSS_40h"), Power = C("ULDA_BOSS_40p1"), Health = 300 },
        new RunForm { Hero = C("ULDA_BOSS_40h2"), Power = C("ULDA_BOSS_40p2"), Health = 200, HealthAtMost = 200, KeepExcess = true, Deck = deck2.Select(C).ToList(), Draw = 3 },
        new RunForm { Hero = C("ULDA_BOSS_40h3"), Power = C("ULDA_BOSS_40p3"), Health = 100, HealthAtMost = 100, KeepExcess = true, Deck = deck3.Select(C).ToList(), Draw = 3 },
    };
    if (start == 1) { plan.BossHero = C("ULDA_BOSS_40h2"); plan.BossHeroPowerId = "ULDA_BOSS_40p2"; plan.BossHealth = 200; plan.BossDeck = deck2.Select(C).ToList(); }
    if (start == 2) { plan.BossHero = C("ULDA_BOSS_40h3"); plan.BossHeroPowerId = "ULDA_BOSS_40p3"; plan.BossHealth = 100; plan.BossDeck = deck3.Select(C).ToList(); }
    plan.BossFormStart = start; plan.BossCarriedDamage = carried;
    return plan;
}
Guard("A", () =>
{
    var g = Ready(Icarax());
    var boss = g.Player2;
    Check("A1 phase 1: the row's hero and power at 300 Health, form 0", boss.Hero.Card.Id == "ULDA_BOSS_40h" && boss.Hero.HeroPower.Card.Id == "ULDA_BOSS_40p1" && boss.Hero.Health == 300 && boss[EncounterRules.FormIndex] == 0);
    Hit(g, boss.Hero, 99);
    Check("A2 at 201 it is still phase 1", boss.Hero.Card.Id == "ULDA_BOSS_40h" && boss.Hero.Health == 201);
    int handBefore = boss.HandZone.Count;
    var oldHero = boss.Hero; var oldPower = boss.Hero.HeroPower;
    Hit(g, boss.Hero, 6);
    Check("A3 at 195 it becomes phase 2: a new hero and power, 200 maximum Health with 195 left, the game goes on", boss.Hero.Card.Id == "ULDA_BOSS_40h2" && boss.Hero.HeroPower.Card.Id == "ULDA_BOSS_40p2"
        && boss.Hero.Health == 195 && boss.Hero.BaseHealth == 200 && boss[EncounterRules.FormIndex] == 1 && g.State != State.COMPLETE && boss.PlayState == PlayState.PLAYING,
        boss.Hero.Card.Id + " " + boss.Hero.Health + "/" + boss.Hero.BaseHealth);
    Check("A4 the old hero and power sit in the set-aside zone; the controller's hero entity is the new one", oldHero.Zone == boss.SetasideZone && oldPower.Zone == boss.SetasideZone && boss.HeroId == boss.Hero.Id
        && !ReferenceEquals(oldHero, boss.Hero));
    Check("A5 its deck is replaced by the phase's list and 3 cards are drawn", boss.DeckZone.Count == 27 && boss.DeckZone.All(c => c.Card.Id == "CS2_120") && boss.HandZone.Count == handBefore + 3
        && boss.HandZone.Count(c => c.Card.Id == "CS2_120") == 3 && boss.SetasideZone.Count(c => c.Card.Id == "CS2_182") >= 20);
    var fact = g.RunDeckFacts.LastOrDefault();
    Check("A6 the deck replacement is recorded as an own-deck fact of the boss", g.RunDeckFacts.Count == 1 && fact.ControllerId == boss.Id && fact.Kind == RunDeckFactKind.Replaced && fact.Definitions.Count == 30 && fact.Definitions.All(d => d == "CS2_120"));
    var sample = OwnDeckLedger.Observe(g, boss, null).Sample(g, boss, 11);
    Check("A7 the boss's own-deck ledger samples the new list (27 River Crocolisks), not the old deck", sample.Count == 1 && sample.TryGetValue("CS2_120", out int n) && n == 27, string.Join(",", sample.Select(p => p.Key + "x" + p.Value)));
    Check("A8 Wrath Smash's cost aura is live from the swap (2, no damaged minion)", boss.Hero.HeroPower.Cost == 2 && boss.Hero.HeroPower[EncounterRules.PowerActive] == 1);
    Hit(g, boss.Hero, 150);
    Check("A9 one hit across the next threshold: phase 3 with 45 of 100 left and the phase-3 deck", boss.Hero.Card.Id == "ULDA_BOSS_40h3" && boss.Hero.Health == 45 && boss.Hero.BaseHealth == 100
        && boss[EncounterRules.FormIndex] == 2 && boss.DeckZone.All(c => c.Card.Id == "CS2_172") && boss[EncounterRules.FormChanges] == 2);
    var end = DungeonRunSetup.Harvest(g);
    Check("A10 the harvest: 45 Health left in form 2 after 2 changes", end.BossRemainingHealth == 45 && end.BossForm == 2 && end.BossFormChanges == 2 && end.HeroesUsed.SequenceEqual(new[] { "HERO_08" }) && !end.NoLoss);
    Hit(g, boss.Hero, 45);
    Check("A11 at 0 in its last phase it dies and the run player wins", g.State == State.COMPLETE && g.Player1.PlayState == PlayState.WON && DungeonRunSetup.Harvest(g).BossRemainingHealth == 0);
    var g2 = Ready(Icarax());
    Hit(g2, g2.Player2.Hero, 250);
    Check("A12 one hit across two thresholds lands in phase 3 (50 left), both decks replaced in turn", g2.Player2.Hero.Card.Id == "ULDA_BOSS_40h3" && g2.Player2.Hero.Health == 50
        && g2.Player2[EncounterRules.FormChanges] == 2 && g2.RunDeckFacts.Count == 2 && g2.Player2.DeckZone.All(c => c.Card.Id == "CS2_172"));
    var g3 = Ready(Icarax());
    Hit(g3, g3.Player2.Hero, 300);
    Check("A13 a killing blow from phase 1 kills it (Health forms never revive)", g3.State == State.COMPLETE && g3.Player1.PlayState == PlayState.WON);
    // Carried damage: the plan starts it in a later form.
    var g4 = Ready(Icarax(1, 20));
    Check("A14 carried into phase 2: the phase-2 hero, power and deck from the start, 180 of 200", g4.Player2.Hero.Card.Id == "ULDA_BOSS_40h2" && g4.Player2.Hero.Health == 180 && g4.Player2[EncounterRules.FormIndex] == 1
        && g4.Player2.DeckZone.Concat(g4.Player2.HandZone).All(c => c.Card.Id == "CS2_120" || c.Card.Id == "GAME_005") && g4.Player2.Hero.HeroPower[EncounterRules.PowerActive] == 1 && g4.RunDeckFacts.Count == 0);
    Hit(g4, g4.Player2.Hero, 85);
    Check("A15 and it goes on to phase 3 at 95", g4.Player2.Hero.Card.Id == "ULDA_BOSS_40h3" && g4.Player2.Hero.Health == 95 && g4.Player2[EncounterRules.FormIndex] == 2);
    var g5 = Ready(Icarax(0, 56));
    Check("A16 carried damage inside phase 1", g5.Player2.Hero.Health == 244 && g5.Player2.Hero.Card.Id == "ULDA_BOSS_40h");
    // A clone keeps the form state and goes on by the same rules.
    var g6 = Ready(Icarax());
    Hit(g6, g6.Player2.Hero, 110);
    var clone = g6.Clone();
    Check("A17 a clone is in the same form with the same deck, and changes form by the same rule", clone.Player2.Hero.Card.Id == "ULDA_BOSS_40h2" && clone.Player2[EncounterRules.FormIndex] == 1 && clone.RunDeckFacts.Count == 1);
    Generic.DamageCharFunc(clone.CurrentPlayer.Hero, clone.Player2.Hero, 95, false); clone.DeathProcessingAndAuraUpdate();
    Check("A18 the clone's change does not touch the real game", clone.Player2.Hero.Card.Id == "ULDA_BOSS_40h3" && g6.Player2.Hero.Card.Id == "ULDA_BOSS_40h2" && g6.RunDeckFacts.Count == 1 && clone.RunDeckFacts.Count == 2);
    // No encounter: no hook does anything.
    var plain = Ready(Plan());
    Hit(plain, plain.Player2.Hero, 29);
    Check("A19 a plan without forms: the config has no encounter and the boss just takes damage", plain.EncounterOrNull == null && plain.Player2.Hero.Health == 1 && plain.Player2[EncounterRules.FormIndex] == 0);
});

// ============================================================ B. death and script forms
Guard("B", () =>
{
    var plan = Plan("HERO_01", "CS2_102", 20);
    plan.EncounterId = "fx_death"; plan.EncounterDeck = true;
    plan.BossForms = new List<RunForm>
    {
        new RunForm { Hero = C("HERO_01"), Power = C("CS2_102"), Health = 20 },
        new RunForm { Hero = C("HERO_02"), Power = C("CS2_049"), Health = 25, OnDeath = true },
        new RunForm { Power = C("CS2_034"), Health = 10, OnDeath = true },
    };
    var g = Ready(plan);
    var boss = g.Player2;
    boss.Hero.GainArmor(boss.Hero, 3);
    Hit(g, boss.Hero, 10);
    Check("B1 a death form does nothing while the boss lives", boss.Hero.Card.Id == "HERO_01" && boss.Hero.Health == 13 && boss.Hero.Armor == 0);
    boss.Hero.GainArmor(boss.Hero, 4);
    Hit(g, boss.Hero, 50);
    Check("B2 when it dies it becomes the next form at full Health instead of losing", g.State != State.COMPLETE && boss.Hero.Card.Id == "HERO_02" && boss.Hero.HeroPower.Card.Id == "CS2_049"
        && boss.Hero.Health == 25 && boss.Hero.Damage == 0 && !boss.Hero.ToBeDestroyed && boss.PlayState == PlayState.PLAYING && boss[EncounterRules.FormIndex] == 1);
    Check("B3 the deck stays when the form names none", boss.DeckZone.All(c => c.Card.Id == "CS2_182") && g.RunDeckFacts.Count == 0);
    Hit(g, boss.Hero, 25);
    Check("B4 a power-only form keeps the hero entity: full Health of the form, the new power", g.State != State.COMPLETE && boss.Hero.Card.Id == "HERO_02" && boss.Hero.HeroPower.Card.Id == "CS2_034"
        && boss.Hero.Health == 10 && boss.Hero.BaseHealth == 10 && boss[EncounterRules.FormIndex] == 2);
    Hit(g, boss.Hero, 10);
    Check("B5 with no form left it dies", g.State == State.COMPLETE && g.Player1.PlayState == PlayState.WON);
    // A script form: the card calls SwapForm.
    var sp = Plan("HERO_01", "CS2_102", 30);
    sp.EncounterId = "fx_script"; sp.EncounterDeck = true;
    sp.BossForms = new List<RunForm> { new RunForm { Hero = C("HERO_01"), Power = C("CS2_102"), Health = 30 }, new RunForm { Hero = C("HERO_05"), Power = C("DS1h_292"), Health = 30 } };
    var gs = Ready(sp);
    Hit(gs, gs.Player2.Hero, 12);
    EncounterRules.SwapForm(gs.Player2, 1);
    Check("B6 SwapForm (a script form) changes hero and power now, at the form's full Health", gs.Player2.Hero.Card.Id == "HERO_05" && gs.Player2.Hero.HeroPower.Card.Id == "DS1h_292" && gs.Player2.Hero.Health == 30
        && gs.Player2[EncounterRules.FormIndex] == 1);
    EncounterRules.SwapForm(gs.Player2, 0);
    Check("B7 and back (Tag Team, Belnaara): form 0 again", gs.Player2.Hero.Card.Id == "HERO_01" && gs.Player2[EncounterRules.FormIndex] == 0 && gs.Player2[EncounterRules.FormChanges] == 2);
    Hit(gs, gs.Player2.Hero, 40);
    Check("B8 a script form is never entered by Health or death", gs.State == State.COMPLETE && gs.Player1.PlayState == PlayState.WON);
    // The boss keeps playing after a form change: a full boss turn with the new power.
    var g2 = Ready(Icarax());
    Hit(g2, g2.Player2.Hero, 105);
    ToBoss(g2);
    var crocolisk = g2.Player2.HandZone.First(c => c.Card.Id == "CS2_120");
    Mana(g2.Player2, 10);
    Check("B9 after the change the boss plays cards of its new deck on its turn", Play(g2, crocolisk) && g2.Player2.BoardZone.Any(m => m.Card.Id == "CS2_120"));
});

// ============================================================ C. the start block and carried player damage
Guard("C", () =>
{
    var plan = Plan();
    plan.EncounterId = "fx_start"; plan.EncounterDeck = true;
    plan.Start = new RunStart
    {
        BossBoard = new[] { C("CS2_125"), C("CS2_125") }, PlayerBoard = new[] { C("CS2_171") }, BossWeapon = C("CS2_106"), BossArmor = 5,
        BossDeckAdd = new[] { C("EX1_029") }, PlayerDeckAdd = new[] { C("EX1_011"), C("EX1_011") }, BossHandAdd = new[] { C("CS2_023") }, PlayerHandAdd = new[] { C("CS2_029") },
    };
    plan.BossOpeningHandBonus = 2; plan.PlayerCarriedDamage = 9;
    var g = Start(plan);
    Check("C1 before the mulligan nothing of the start block exists; the boss was dealt 2 more cards", g.Player2.BoardZone.Count == 0 && g.Player1.BoardZone.Count == 0 && g.Player2.Hero.Weapon == null
        && g.Player2.Hero.Armor == 0 && g.Player1.DeckZone.All(c => c.Card.Id != "EX1_011") && g.Player2.Choice.Choices.Count == Start(Plan()).Player2.Choice.Choices.Count + 2 && g[EncounterRules.StartDone] == 0,
        "boss mulligan " + g.Player2.Choice.Choices.Count + " vs " + Start(Plan()).Player2.Choice.Choices.Count);
    Check("C2 carried damage is on the run hero from the start", g.Player1.Hero.Damage == 9 && g.Player1.Hero.Health == 21);
    int p1Deck = g.Player1.DeckZone.Count, p2Deck = g.Player2.DeckZone.Count;
    Finish(g);
    Check("C3 after both mulligans: the boss's two minions, the player's minion, the weapon and the Armor", g.Player2.BoardZone.Count(m => m.Card.Id == "CS2_125") == 2 && g.Player1.BoardZone.Count(m => m.Card.Id == "CS2_171") == 1
        && g.Player2.Hero.Weapon?.Card.Id == "CS2_106" && g.Player2.Hero.Armor == 5 && g[EncounterRules.StartDone] == 1);
    Check("C4 the deck adds are in the decks, the hand adds in the hands", g.Player1.DeckZone.Concat(g.Player1.HandZone).Count(c => c.Card.Id == "EX1_011") == 2 && g.Player2.DeckZone.Concat(g.Player2.HandZone).Count(c => c.Card.Id == "EX1_029") == 1
        && g.Player2.HandZone.Any(c => c.Card.Id == "CS2_023") && g.Player1.HandZone.Any(c => c.Card.Id == "CS2_029"));
    var clone = g.Clone();
    EncounterRules.ResolveStart(clone);
    Check("C5 the start block lands once (a second resolve does nothing, in the game or a clone)", clone.Player2.BoardZone.Count == 2 && clone.Player1.BoardZone.Count == 1);
    bool refused = false;
    var bad = Plan(); bad.BossOpeningHandBonus = 9;
    try { Start(bad); } catch (ArgumentException) { refused = true; }
    var bad2 = Plan(); bad2.BossShrine = "CS2_182"; bool refused2 = false;
    try { Start(bad2); } catch (ArgumentException) { refused2 = true; }
    var bad3 = Plan(); bad3.GlobalRules = new List<string> { "DALA_857" }; bool refused3 = false;
    try { Start(bad3); } catch (ArgumentException) { refused3 = true; }
    Check("C6 Configure refuses an opening bonus out of range, an unscripted boss shrine and an unscripted rule", refused && refused2 && refused3);
});

// ============================================================ D. the run player's relay
Guard("D", () =>
{
    var plan = Plan("HERO_01", "CS2_102", 60);
    plan.EncounterId = "fx_relay"; plan.EncounterDeck = true;
    plan.PlayerRelay = new List<RunFightHero>
    {
        new RunFightHero { Hero = C("HERO_05"), Power = C("DS1h_292"), Health = 50, Deck = Enumerable.Repeat("CS2_120", 12).Select(C).ToList() },
        new RunFightHero { Hero = C("HERO_09"), Power = C("CS1h_001"), Health = 40, Deck = Enumerable.Repeat("CS2_172", 12).Select(C).ToList() },
    };
    var g = Ready(plan);
    var p = g.Player1;
    var yeti = Board(g, p, "CS2_182");
    Mana(p, 6);
    Hit(g, g.Player2.Hero, 13);
    int handBefore = p.HandZone.Count;
    Hit(g, p.Hero, 40);
    Check("D1 when the run hero dies the first relay hero enters: its portrait and power at its own Health, the game goes on", g.State != State.COMPLETE && p.Hero.Card.Id == "HERO_05" && p.Hero.HeroPower.Card.Id == "DS1h_292"
        && p.Hero.Health == 50 && p.PlayState == PlayState.PLAYING && p[EncounterRules.RelayIndex] == 1);
    Check("D2 its hand and deck are its own list (4 drawn of 12); the old cards are set aside", p.HandZone.Count == 4 && p.HandZone.All(c => c.Card.Id == "CS2_120") && p.DeckZone.Count == 8
        && p.DeckZone.All(c => c.Card.Id == "CS2_120") && p.SetasideZone.Count(c => c.Card.Id == "CS2_182") >= 20, handBefore + " -> " + p.HandZone.Count);
    Check("D3 the board and the Mana Crystals stay; the boss keeps its Health", yeti.Zone == p.BoardZone && p.BaseMana == 6 && g.Player2.Hero.Health == 47);
    Check("D4 the boss's beliefs about the old hand and deck are reset, and the new deck is an own-deck fact of the player", g.RunBeliefResets.Count == 1 && g.RunBeliefResets[0].ControllerId == p.PlayerId
        && g.RunDeckFacts.Count == 1 && g.RunDeckFacts[0].ControllerId == p.Id);
    Hit(g, p.Hero, 50);
    Check("D5 the second relay hero follows", p.Hero.Card.Id == "HERO_09" && p.Hero.Health == 40 && p[EncounterRules.RelayIndex] == 2 && p.DeckZone.All(c => c.Card.Id == "CS2_172"));
    var end = DungeonRunSetup.Harvest(g);
    Check("D6 the harvest lists the three heroes in order", end.HeroesUsed.SequenceEqual(new[] { "HERO_08", "HERO_05", "HERO_09" }) && end.BossRemainingHealth == 47);
    Hit(g, p.Hero, 40);
    Check("D7 with no relay hero left the run player loses", g.State == State.COMPLETE && g.Player2.PlayState == PlayState.WON);
    var g2 = Ready(plan);
    g2.Process(ConcedeTask.Any(g2.Player1));
    Check("D8 a concession is never relayed", g2.State == State.COMPLETE && g2.Player1.Hero.Card.Id == "HERO_08" && g2.Player1[EncounterRules.RelayIndex] == 0);
});

// ============================================================ E. the boss board shrine; Typhoon against a shrine
Guard("E", () =>
{
    foreach (string shrine in new[] { "TRLA_104", "TRLA_170", "TRLA_171" })
    {
        var plan = Plan(); plan.BossShrine = shrine; plan.EncounterId = "trl_200_voone"; plan.EncounterDeck = true;
        var g = Start(plan);
        Check("E1 " + shrine + ": armed in the boss's set-aside zone before the mulligan, on no board", g.Player2.BoardZone.Count == 0 && g.Player2.SetasideZone.Any(c => c.Card.Id == shrine && ShrineRules.IsMarked(c)));
        Finish(g);
        var s = ShrineRules.Of(g.Player2).SingleOrDefault();
        Check("E2 " + shrine + ": on the boss's board after both mulligans, marked, awake, and none on the player's", s != null && s.Card.Id == shrine && ShrineRules.IsAwake(s) && ShrineRules.Of(g.Player1).Count == 0);
        s.Destroy(); g.DeathProcessingAndAuraUpdate();
        var form = ShrineRules.Of(g.Player2).SingleOrDefault();
        Check("E3 " + shrine + ": dies into its dormant form in its slot", form != null && form.Card.Id == shrine + "t" && ShrineRules.IsDormantForm(form) && ShrineRules.TurnsLeft(form) == 3);
        for (int i = 0; i < 6 && !ShrineRules.Of(g.Player2).Any(ShrineRules.IsAwake); i++) EndTurn(g);
        Check("E4 " + shrine + ": revives at the start of its owner's third turn", ShrineRules.Of(g.Player2).Any(m => m.Card.Id == shrine && ShrineRules.IsAwake(m)) && g.CurrentPlayer == g.Player2);
    }
    // Their effects work for the boss: Armor-equal Attack, Dragon cost cut, Overkill on its minions.
    var p104 = Plan(); p104.BossShrine = "TRLA_104";
    var g104 = Ready(p104);
    g104.Player2.Hero.GainArmor(g104.Player2.Hero, 6); g104.DeathProcessingAndAuraUpdate();
    Check("E5 TRLA_104 on the boss's side: its Attack is the boss's Armor", ShrineRules.Of(g104.Player2)[0].AttackDamage == 6, "attack " + ShrineRules.Of(g104.Player2)[0].AttackDamage);
    var p170 = Plan(); p170.BossShrine = "TRLA_170";
    var g170 = Ready(p170);
    ToBoss(g170);
    var drake = ToHand(g170, g170.Player2, "EX1_043"); // Twilight Drake (a Dragon, 4)
    Board(g170, g170.Player2, "EX1_043");
    Check("E6 TRLA_170 on the boss's side: after the boss summons a Dragon, the Dragons in its hand cost (1) less", drake.Cost == 3, "cost " + drake.Cost);
    // Typhoon: every minion is shuffled into a deck, but a run shrine never leaves its board.
    var pt = Plan(); pt.BossShrine = "TRLA_104";
    var gt = Ready(pt);
    var mine = Board(gt, gt.Player1, "CS2_182"); var theirs = Board(gt, gt.Player2, "CS2_182");
    var shrineT = ShrineRules.Of(gt.Player2)[0];
    Mana(gt.Player1, 10);
    var typhoon = ToHand(gt, gt.Player1, "EDR_232");
    Check("E7 Typhoon is played", Play(gt, typhoon));
    var after = ShrineRules.Of(gt.Player2).SingleOrDefault();
    Check("E8 the other minions left both boards for the decks; the shrine went dormant in its slot and is in no deck", mine.Zone != gt.Player1.BoardZone && theirs.Zone != gt.Player2.BoardZone
        && after != null && ShrineRules.IsDormantForm(after) && gt.Player2.BoardZone.Count == 1 && gt.Player1.BoardZone.Count == 0
        && !gt.Player1.DeckZone.Concat(gt.Player2.DeckZone).Any(c => c.Card.Id == "TRLA_104" || c.Card.Id == "TRLA_104t") && shrineT.Controller == gt.Player2);
    var gt2 = Ready(pt);
    ShrineRules.Of(gt2.Player2)[0].Destroy(); gt2.DeathProcessingAndAuraUpdate();
    Mana(gt2.Player1, 10);
    Play(gt2, ToHand(gt2, gt2.Player1, "EDR_232"));
    Check("E9 a dormant shrine stays where it is through Typhoon", ShrineRules.Of(gt2.Player2).Count == 1 && ShrineRules.IsDormantForm(ShrineRules.Of(gt2.Player2)[0]));
    var gt3 = Ready(Plan());
    var a = Board(gt3, gt3.Player1, "CS2_182"); var b = Board(gt3, gt3.Player2, "CS2_182");
    Mana(gt3.Player1, 10);
    int decks = gt3.Player1.DeckZone.Count + gt3.Player2.DeckZone.Count;
    Play(gt3, ToHand(gt3, gt3.Player1, "EDR_232"));
    Check("E10 without a shrine Typhoon is unchanged: both minions are shuffled into decks", gt3.Player1.BoardZone.Count == 0 && gt3.Player2.BoardZone.Count == 0
        && gt3.Player1.DeckZone.Count + gt3.Player2.DeckZone.Count == decks + 2);
});

// ============================================================ F. the boss's generated Secrets (Whirt)
Guard("F", () =>
{
    var plan = Plan("DALA_BOSS_21h", "DALA_BOSS_21p", 40);
    plan.BossGeneratedSecrets = true; plan.EncounterId = "dala_21_whirt"; plan.EncounterDeck = true;
    var g = Ready(plan);
    ToBoss(g);
    Mana(g.Player2, 10);
    var boss = g.Player2;
    Check("F1 Prediction costs (1) and is clickable", boss.Hero.HeroPower.Cost == 1 && !boss.Hero.HeroPower.IsPassiveHeroPower);
    Check("F2 it puts one Secret into the boss's Secret zone", Power(g) && boss.SecretZone.Count == 1 && boss.SecretZone[0].IsSecret && boss.UsedMana == 1);
    var secret = boss.SecretZone[0];
    var pool = DungeonRunValidator.BossSecretPool(CardClass.MAGE).Select(c => c.Id).ToList();
    Check("F3 a scripted Mage Secret of the validator's closed pool, admitted for a boss, live as if cast", pool.Contains(secret.Card.Id) && secret.Card.Class == CardClass.MAGE && secret.ActivatedTrigger != null
        && DungeonRunAdmission.Classify(secret.Card, secret.Card.Id, AdmitScope.Boss, null, catalogRun: true).Admitted, secret.Card.Id);
    var seen = new HashSet<string> { secret.Card.Id };
    for (int turn = 0; turn < 12 && !boss.SecretZone.IsFull; turn++)
    {
        EndTurn(g); EndTurn(g);
        Mana(g.Player2, 10);
        if (g.State == State.COMPLETE) break;
        Power(g);
        foreach (var s in boss.SecretZone) seen.Add(s.Card.Id);
    }
    Check("F4 used again it adds a different Secret each time (never one it holds), all from the pool", boss.SecretZone.Select(s => s.Card.Id).Distinct().Count() == boss.SecretZone.Count
        && boss.SecretZone.Count >= 3 && seen.All(pool.Contains), string.Join(",", seen));
    Check("F5 the engine's pool for the boss is the validator's", DungeonRunContent.RandomSecretCandidates(boss).Select(c => c.Id).SequenceEqual(pool), pool.Count + " vs " + DungeonRunContent.RandomSecretCandidates(boss).Count);
    Check("F6 the run player's side still generates from its own lists (both sides on)", !g.ExcludesGeneratedSecretsFor(1) && !g.ExcludesGeneratedSecretsFor(2));
    var off = Plan("DALA_BOSS_21h", "DALA_BOSS_21p", 40);
    var g2 = Ready(off);
    ToBoss(g2); Mana(g2.Player2, 10);
    Power(g2);
    Check("F7 on a plan that keeps the boss side off the power would do nothing (which is why the validator refuses that row)", g2.Player2.SecretZone.Count == 0 && g2.ExcludesGeneratedSecretsFor(2));
});

// ============================================================ G. the global rules (anomalies) on both players
Guard("G", () =>
{
    var plan = Plan(); plan.GlobalRules = new List<string> { "DALA_866" };
    var g = Start(plan);
    Check("G1 DALA_866: its player enchantment is on both players before the mulligan", DungeonRunGlobalRules.IsActive(g.Player1, "DALA_866") && DungeonRunGlobalRules.IsActive(g.Player2, "DALA_866")
        && g.Player1.AppliedEnchantments.Count(e => e.Card.Id == "DALA_866e") == 1 && g.Player2.AppliedEnchantments.Count(e => e.Card.Id == "DALA_866e") == 1);
    Finish(g);
    g.Player1.Hero.Damage = 10; g.Player2.Hero.Damage = 10;
    EndTurn(g);
    Check("G2 at the start of the boss's turn the boss's hero is healed 2, the run player's is not", g.Player2.Hero.Damage == 8 && g.Player1.Hero.Damage == 10);
    EndTurn(g);
    Check("G3 at the start of the run player's turn the run player's hero is healed 2", g.Player1.Hero.Damage == 8 && g.Player2.Hero.Damage == 8);
    DungeonRunGlobalRules.Apply(g, "DALA_866");
    Check("G4 applying a rule twice stacks nothing", g.Player1.AppliedEnchantments.Count(e => e.Card.Id == "DALA_866e") == 1);
    var p864 = Plan(); p864.GlobalRules = new List<string> { "DALA_864" };
    var g864 = Ready(p864);
    var mine = Board(g864, g864.Player1, "CS2_182"); var theirs = Board(g864, g864.Player2, "CS2_182");
    EndTurn(g864);
    Check("G5 DALA_864: at the end of the run player's turn only its minions gain +1/+1", mine.AttackDamage == 5 && mine.Health == 6 && theirs.AttackDamage == 4 && theirs.Health == 5);
    EndTurn(g864);
    Check("G6 at the end of the boss's turn the boss's minions gain +1/+1", theirs.AttackDamage == 5 && theirs.Health == 6 && mine.AttackDamage == 5);
    var p850 = Plan(); p850.GlobalRules = new List<string> { "DALA_850" };
    var g850 = Ready(p850);
    var fireball = ToHand(g850, g850.Player1, "CS2_029"); var yeti = ToHand(g850, g850.Player1, "CS2_182"); var theirSpell = ToHand(g850, g850.Player2, "CS2_029"); var cheap = ToHand(g850, g850.Player1, "CS2_025");
    Check("G7 DALA_850: spells in both hands cost (2) less (never below 0), minions do not", fireball.Cost == 2 && theirSpell.Cost == 2 && yeti.Cost == 4 && cheap.Cost == 0,
        fireball.Cost + " " + theirSpell.Cost + " " + yeti.Cost + " " + cheap.Cost);
    var echo1 = ToHand(g850, g850.Player2, "LOOT_504"); var echo2 = ToHand(g850, g850.Player1, "GIL_145");
    Check("G7b a repeatable spell is never brought below (1): Unstable Evolution (\"Repeatable this turn\") stays (1), Sound the Bells! (Echo, 2) costs (1)",
        echo1.Cost == 1 && echo2.Cost == 1 && DungeonRunEncounters.Repeatable(echo1) && DungeonRunEncounters.Repeatable(echo2) && !DungeonRunEncounters.Repeatable(fireball), echo1.Cost + " " + echo2.Cost);
    var both = Plan(); both.GlobalRules = new List<string> { "DALA_850", "DALA_866" };
    var gb = Ready(both);
    Check("G8 two rules are both on both players, in the plan's order", gb.EncounterOrNull.GlobalRules.SequenceEqual(new[] { "DALA_850", "DALA_866" }) && DungeonRunGlobalRules.IsActive(gb.Player2, "DALA_850") && DungeonRunGlobalRules.IsActive(gb.Player2, "DALA_866"));
    var clone = g.Clone();
    clone.Player1.Hero.Damage = 10;
    clone.Process(EndTurnTask.Any(clone.CurrentPlayer)); clone.Process(EndTurnTask.Any(clone.CurrentPlayer));
    Check("G9 a clone plays under the same rule", clone.Player1.Hero.Damage == 8);
    foreach (string id in new[] { "DALA_850", "DALA_864", "DALA_866" })
        Check("G10 " + id + ": the rule card and its enchantment carry the client's text", C(id).Text == factById[id].GetProperty("text").GetString() && C(id + "e").Name.EndsWith("Player Enchantment") && C(id).Implemented && C(id + "e").Implemented);
});

// ============================================================ H. Icarax's powers, Hagatha's cards, Looming Presence
Guard("H", () =>
{
    var g = Ready(Plan("ULDA_BOSS_40h", "ULDA_BOSS_40p1", 300));
    ToBoss(g);
    Mana(g.Player2, 10);
    var guard = Board(g, g.Player2, "CS2_182");
    Check("H1 Commanding Scream costs (1)", g.Player2.Hero.HeroPower.Cost == 1 && Power(g));
    Hit(g, guard, 20);
    Check("H2 its minions cannot be reduced below 1 Health this turn", guard.Zone == g.Player2.BoardZone && guard.Health == 1);
    EndTurn(g);
    Hit(g, guard, 1);
    Check("H3 the protection ends with the turn", guard.Zone != g.Player2.BoardZone);

    var g2 = Ready(Plan("ULDA_BOSS_40h2", "ULDA_BOSS_40p2", 200));
    ToBoss(g2);
    Mana(g2.Player2, 10);
    var a = Board(g2, g2.Player1, "CS2_182"); var b = Board(g2, g2.Player2, "CS2_182"); var whole = Board(g2, g2.Player1, "CS2_182");
    var power = g2.Player2.Hero.HeroPower;
    Check("H4 Wrath Smash: (2) with no damaged minion, and no legal target", power.Cost == 2 && !Power(g2, whole));
    Hit(g2, a, 1); Hit(g2, b, 2);
    Check("H5 it costs (1) more for each damaged minion on either board", power.Cost == 4, "cost " + power.Cost);
    Check("H6 it destroys a damaged minion and only a damaged one", !Power(g2, whole) && Power(g2, a) && a.Zone != g2.Player1.BoardZone && g2.Player2.UsedMana == 4);
    Check("H7 the cost follows the board (one damaged minion left: 3)", power.Cost == 3);

    var g3 = Ready(Plan("ULDA_BOSS_40h3", "ULDA_BOSS_40p3", 100));
    ToBoss(g3);
    Mana(g3.Player2, 10);
    var yeti = Board(g3, g3.Player1, "CS2_182");
    Check("H8 Hand of Defiance costs (0) and swaps a minion's Attack and Health", g3.Player2.Hero.HeroPower.Cost == 0 && Power(g3, yeti) && yeti.AttackDamage == 5 && yeti.Health == 4
        && yeti.AppliedEnchantments.Any(e => e.Card.Id == "ULDA_BOSS_40p3e"));
    foreach (string id in new[] { "ULDA_BOSS_40p1", "ULDA_BOSS_40p2", "ULDA_BOSS_40p3", "DALA_BOSS_21p", "GILA_BOSS_61t", "GILA_BOSS_61t3", "GILA_500h4", "GILA_600h2", "LOEA_01", "LOEA_01H" })
        Check("H9 " + id + " is scripted and keeps the client's text", C(id).Implemented && C(id).Text == factById[id].GetProperty("text").GetString());

    // Hagatha: Bewitch is passive and live from the start; Ritual Dagger; Ominous Fog.
    var hp = Plan("GILA_BOSS_61h", "GIL_504h", 100, Enumerable.Repeat("CS2_172", 30).ToArray());
    hp.BossStartManaBonus = 2;
    var gh = Ready(hp);
    Check("H10 Hagatha starts with 2 Mana Crystals more and an unclickable, started Bewitch", gh.Player2.BaseMana == 2 && gh.Player2.Hero.HeroPower.IsPassiveHeroPower
        && gh.Player2.Hero.HeroPower[EncounterRules.PowerActive] == 1 && gh.Player2.Hero.HeroPower.ActivatedTrigger != null);
    ToBoss(gh);
    Mana(gh.Player2, 10);
    Check("H11 the boss cannot click it", !Power(gh));
    int hand = gh.Player2.HandZone.Count;
    Play(gh, gh.Player2.HandZone.First(c => c.Card.Id == "CS2_172"));
    Check("H12 after the boss plays a minion a random Shaman spell joins its hand", gh.Player2.HandZone.Count == hand && gh.Player2.HandZone.Any(c => c.Card.Type == CardType.SPELL && c.Card.Class == CardClass.SHAMAN));
    var plainHagatha = Ready(Plan("HERO_08", "GIL_504h", 30));
    Check("H13 outside an encounter start the collectible passive is as it was (not started by the run setup's list only when listed)", plainHagatha.Player2.Hero.HeroPower[EncounterRules.PowerActive] == 1);
    var dagger = ToHand(gh, gh.Player2, "GILA_BOSS_61t");
    Check("H14 Ritual Dagger: a 3/4 Lifesteal weapon for (2)", dagger.Cost == 2 && Play(gh, dagger) && gh.Player2.Hero.Weapon.AttackDamage == 3 && gh.Player2.Hero.Weapon.Durability == 4 && gh.Player2.Hero.Weapon.Card[GameTag.LIFESTEAL] == 1);
    Play(gh, ToHand(gh, gh.Player2, "CS2_029"), gh.Player1.Hero);
    Check("H15 after the boss casts a spell it gains +1 Durability", gh.Player2.Hero.Weapon.Durability == 5 && gh.Player2.Hero.Weapon.AppliedEnchantments.Any(e => e.Card.Id == "GILA_BOSS_61te"));
    var victim = Board(gh, gh.Player1, "CS2_182"); var own = Board(gh, gh.Player2, "CS2_182");
    Check("H16 Ominous Fog costs (2) and corrupts the enemy minions only", ToHand(gh, gh.Player2, "GILA_BOSS_61t3").Cost == 2 && Play(gh, gh.Player2.HandZone.First(c => c.Card.Id == "GILA_BOSS_61t3"))
        && victim.AppliedEnchantments.Any(e => e.Card.Id == "UNG_831e") && (own.AppliedEnchantments?.All(e => e.Card.Id != "UNG_831e") ?? true));
    EndTurn(gh);
    Check("H17 they live through the run player's turn", victim.Zone == gh.Player1.BoardZone);
    EndTurn(gh);
    Check("H18 and are destroyed at the start of the boss's next turn; its own minion is not", victim.Zone != gh.Player1.BoardZone && own.Zone == gh.Player2.BoardZone);
    var gl = Ready(Plan());
    ToBoss(gl); Mana(gl.Player2, 10);
    int cards = gl.Player2.HandZone.Count;
    Play(gl, ToHand(gl, gl.Player2, "LOEA_01H"));
    Check("H19 Looming Presence (Heroic): draw 3 cards, gain 6 Armor", gl.Player2.HandZone.Count == cards + 3 && gl.Player2.Hero.Armor == 6);
    foreach (var extra in gl.Player2.HandZone.ToArray()) { gl.Player2.HandZone.Remove(extra); gl.Player2.SetasideZone.Add(extra); }
    Play(gl, ToHand(gl, gl.Player2, "LOEA_01"));
    Check("H20 Looming Presence: draw 2 cards, gain 4 Armor", gl.Player2.HandZone.Count == 2 && gl.Player2.Hero.Armor == 10);
});

// ============================================================ I. the run player's fight-only hero cards
Guard("I", () =>
{
    var plan = Plan("GILA_BOSS_61h", "GIL_504h", 100);
    plan.EncounterId = "gila_61_hagatha"; plan.EncounterDeck = true;
    var dariusDeck = Enumerable.Repeat("CS2_120", 12).Select(C).ToList();
    plan.PlayerHeroCards = new List<RunFightHero>
    {
        new RunFightHero { Hero = C("GILA_600h2"), Deck = dariusDeck, Passives = new[] { "GILA_603", "GILA_607", "GILA_805" } },
        new RunFightHero { Hero = C("GILA_500h4"), Deck = Enumerable.Repeat("CS2_172", 12).Select(C).ToList(), Passives = new[] { "GILA_513", "GILA_511", "LOOTA_832" } },
    };
    var g = Start(plan);
    Check("I1 before the mulligan the hero cards are nowhere", !g.Player1.HandZone.Concat(g.Player1.DeckZone).Any(c => c.Card.Type == CardType.HERO));
    Finish(g);
    var p = g.Player1;
    var darius = p.HandZone.FirstOrDefault(c => c.Card.Id == "GILA_600h2"); var tess = p.HandZone.FirstOrDefault(c => c.Card.Id == "GILA_500h4");
    Check("I2 after the mulligan both hero cards are in the run player's hand, at their printed Costs (5 and 8)", darius != null && tess != null && darius.Cost == 5 && tess.Cost == 8);
    var mine = Board(g, p, "CS2_182"); var theirs = Board(g, g.Player2, "CS2_186"); // Yeti 4/5, War Golem 7/7
    p.Hero.Damage = 12;
    Mana(p, 10);
    int deck = p.DeckZone.Count;
    Check("I3 Darius's card is played", Play(g, darius));
    var choice = p.Choice;
    Check("I4 its Battlecry first offers the hunter's three passives", choice != null && choice.Choices.Select(id => g.IdEntityDic[id].Card.Id).OrderBy(x => x).SequenceEqual(new[] { "GILA_603", "GILA_607", "GILA_805" })
        && mine.Health == 5 && theirs.Health == 7);
    int pick = choice.Choices.First(id => g.IdEntityDic[id].Card.Id == "GILA_805");
    var decision = g.GetPendingDecisions().Single(d => d.ControllerId == p.Id);
    Check("I5 the pick is accepted", g.TryAnswer(new SabberStoneCore.Model.Decisions.DecisionAnswer(decision.Id, decision.ControllerId, new[] { pick }), out _));
    Check("I6 the hero and power changed for this match: Darius with Fire!, the Health and damage carried, 5 Armor", p.Hero.Card.Id == "GILA_600h2" && p.Hero.HeroPower.Card.Id == "GILA_600p"
        && p.Hero.Damage == 12 && p.Hero.BaseHealth == 30 && p.Hero.Armor == 5);
    Check("I7 then 5 damage to all minions, with the passive already in force: the run player's 4/5 Yeti (4/7 with Entrenchment) survives at 2, the War Golem is at 2",
        theirs.Health == 2 && mine.Zone == p.BoardZone && mine.Health == 2, "mine " + mine.Health + " zone " + mine.Zone?.Type + " theirs " + theirs.Health + " base " + mine.BaseHealth + " ench " + string.Join(",", p.AppliedEnchantments.Select(e => e.Card.Id)));
    Check("I8 the chosen passive is a player enchantment of this match (Entrenchment: +2 Health)", p.AppliedEnchantments.Any(e => e.Card.Id == "GILA_805e") && mine.BaseHealth == 7);
    Check("I9 the hunter's 12 cards joined the deck", p.DeckZone.Count == deck + 12 && p.DeckZone.Count(c => c.Card.Id == "CS2_120") == 12);
    var end = DungeonRunSetup.Harvest(g);
    Check("I10 the harvest names both heroes", end.HeroesUsed.SequenceEqual(new[] { "HERO_08", "GILA_600h2" }));
    EndTurn(g); EndTurn(g);
    Mana(p, 10);
    Play(g, tess);
    var c2 = p.Choice;
    var d2 = g.GetPendingDecisions().Single(d => d.ControllerId == p.Id);
    g.TryAnswer(new SabberStoneCore.Model.Decisions.DecisionAnswer(d2.Id, d2.ControllerId, new[] { c2.Choices[0] }), out _);
    Check("I11 Tess's card after it: Tess with Scavenge, 8 damage to all minions, the second 12 cards, a second passive",
        p.Hero.Card.Id == "GILA_500h4" && p.Hero.HeroPower.Card.Id == "GILA_500p2" && g.Player2.BoardZone.All(m => m.Card.Id != "CS2_186") && p.DeckZone.Count(c => c.Card.Id == "CS2_172") == 12
        && DungeonRunSetup.Harvest(g).HeroesUsed.SequenceEqual(new[] { "HERO_08", "GILA_600h2", "GILA_500h4" }));
    Check("I12 the plan's own lists were never written", plan.PlayerDeck.Count == 30 && plan.PlayerHeroCards.Count == 2 && plan.PlayerHero.Id == "HERO_08");
    // A hero card played with no option data (outside its encounter) still does its text: the damage, no deck, no passive.
    var bare = Ready(Plan());
    Mana(bare.Player1, 10);
    var foe = Board(bare, bare.Player2, "CS2_186");
    Play(bare, ToHand(bare, bare.Player1, "GILA_600h2"));
    Check("I13 with no encounter data the card deals its damage at once and changes the hero", bare.Player1.Hero.Card.Id == "GILA_600h2" && foe.Health == 2 && bare.Player1.Choice == null);
});

Console.WriteLine($"SearchTelemetry swallowed leaf exceptions {SearchTelemetry.SwallowedLeafErrors}");
Console.WriteLine($"RESULT {passes} passed, {failures + SearchTelemetry.SwallowedLeafErrors} failed");
return failures + SearchTelemetry.SwallowedLeafErrors;
