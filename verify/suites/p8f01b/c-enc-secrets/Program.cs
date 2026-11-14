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

// Phase 8 family 01, the Secret bosses and the boss side's generated Secrets (real engine games through DungeonRunSetup):
// Elder Brandlemar's Dampen Magic, Trapped Room's Alarm and Sawblade, Manhunter Ivan's Trap Preparation, Trap Room's Tricks
// and Traps, Pressure Plate (ULD_152), and Professor Putricide / Streetsmuggler in a boss's hands (the boss row of the
// generated-Secret lift), with the shipped rows, their plans and the admission and validator rules.
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
string root = VerifyPaths.Data;
string R(string f) => File.ReadAllText(Path.Combine(root, f));
DungeonRunData Load() => DungeonRunData.Parse(R("dungeon-run/run-v1.json"), R("dungeon-run/pool-v1.json"), R("npc/decks-v1.json"), R("dungeon-run/catalog-v1.json"),
    R("dungeon-run/schedule-v1.json"), R("dungeon-run/tavern-v1.json"), R("dungeon-run/encounters-v1.json"));
var data = Load();
var run = new DungeonRun(data);
var all = new DungeonRunProfile(); all.Unlocked.AddRange(data.Unlocks.Keys);

string[] filler = Enumerable.Repeat("CS2_182", 30).ToArray(); // Chillwind Yeti
RunMatchPlan Plan(string bossHero, string bossPower, int bossHealth = 30, string[] bossDeck = null, bool bossSecrets = false) => new RunMatchPlan
{
    PlayerHero = C("HERO_08"), PlayerHeroPowerId = "CS2_034", PlayerDeck = filler.Select(C).ToList(),
    BossHero = C(bossHero), BossHeroPowerId = bossPower, BossHealth = bossHealth, BossDeck = (bossDeck ?? filler).Select(C).ToList(),
    PlayerGeneratedSecrets = true, BossGeneratedSecrets = bossSecrets, EncounterId = "test", EncounterDeck = true,
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
void Mana(Controller c, int n) { c.BaseMana = n; c.UsedMana = 0; c.OverloadLocked = 0; c.OverloadOwed = 0; }
IPlayable ToHand(Game g, Controller c, string id)
{
    while (c.HandZone.Count >= 9) { var old = c.HandZone[0]; c.HandZone.Remove(old); c.SetasideZone.Add(old); }
    var e = Entity.FromCard(c, C(id)); Generic.AddHandPhase.Invoke(c, e); g.DeathProcessingAndAuraUpdate(); return e;
}
Minion Board(Game g, Controller c, string id) { var m = (Minion)Entity.FromCard(c, C(id)); Generic.SummonBlock(g, m, -1, c.Hero); g.DeathProcessingAndAuraUpdate(); return m; }
bool Play(Game g, IPlayable card, ICharacter target = null) => g.Process(PlayCardTask.Any(g.CurrentPlayer, card, target, -1, 0));
void EndTurn(Game g) => g.Process(EndTurnTask.Any(g.CurrentPlayer));
bool Offered(Game g) => g.CurrentPlayer.Options().Any(o => o is HeroPowerTask);
bool Power(Game g, ICharacter target = null) => Offered(g) && g.Process(HeroPowerTask.Any(g.CurrentPlayer, target));
void ToBoss(Game g) { if (g.CurrentPlayer == g.Player1) EndTurn(g); Mana(g.Player2, 10); }
void ToPlayer(Game g) { if (g.CurrentPlayer == g.Player2) EndTurn(g); Mana(g.Player1, 10); }
// One full round back to the boss's turn.
bool Round(Game g) { EndTurn(g); EndTurn(g); Mana(g.Player2, 10); return true; }
List<string> Secrets(Controller c) => c.SecretZone.Select(s => s.Card.Id).ToList();
// The run player casts a harmless spell (Arcane Intellect): what the boss's Secrets react to.
bool CastSpell(Game g, string id = "CS2_023") { Mana(g.Player1, 10); return Play(g, ToHand(g, g.Player1, id)); }
string[] Deck(params (string id, int n)[] cards) => cards.SelectMany(c => Enumerable.Repeat(c.id, c.n)).ToArray();
// Every Secret of the boss's hand goes back into its deck, so a deck test does not depend on the draws.
void SecretsToDeck(Controller c) { foreach (var s in c.HandZone.Where(p => p.Card.IsSecret).ToList()) { c.HandZone.Remove(s); c.DeckZone.Add(s); } }
string[] hunterSecrets = { "EX1_610", "EX1_611", "EX1_554", "EX1_609", "LOE_021" };
var hunterPool = DungeonRunValidator.BossSecretPool(CardClass.HUNTER).Select(c => c.Id).ToList();

// ============================================================ S1-S6 Elder Brandlemar: Dampen Magic
Guard("Brandlemar", () =>
{
    var g = Ready(Plan("LOOTA_BOSS_35h", "LOOTA_BOSS_35p"));
    var boss = g.Player2;
    ToBoss(g);
    Check("S1 Dampen Magic costs (2), is clickable and puts a live Counterspell into the boss's Secret zone (put, not cast)", boss.Hero.HeroPower.Cost == 2 && !boss.Hero.HeroPower.IsPassiveHeroPower
        && Power(g) && Secrets(boss).SequenceEqual(new[] { "EX1_287" }) && boss.UsedMana == 2 && boss.SecretZone[0].ActivatedTrigger != null && boss.NumSpellsPlayedThisGame == 0,
        string.Join(",", Secrets(boss)));
    Round(g);
    Check("S2 while Counterspell is in play the power is not offered (never a second copy)", !Offered(g) && !Power(g) && boss.SecretZone.Count == 1);
    ToPlayer(g);
    int hand = g.Player1.HandZone.Count;
    bool cast = CastSpell(g);
    Check("S3 the run player's next spell is countered (no cards drawn) and the Secret is gone", cast && g.Player1.HandZone.Count == hand && boss.SecretZone.Count == 0, g.Player1.HandZone.Count + " vs " + hand);
    ToBoss(g);
    Check("S4 with no Counterspell left it is offered again and puts a new one", Offered(g) && Power(g) && Secrets(boss).SequenceEqual(new[] { "EX1_287" }));
    var g2 = Ready(Plan("LOOTA_BOSS_35h", "LOOTA_BOSS_35p"));
    ToBoss(g2);
    foreach (string id in hunterSecrets) DungeonRunEncounters.PutSecret(g2.Player2, C(id));
    Check("S5 with a full Secret zone it is not offered", g2.Player2.SecretZone.IsFull && !Offered(g2));
    var b = data.Boss("loot_35_brandlemar");
    Check("S6 the shipped row: Elder Brandlemar, Dungeon Run, tier 1, the wiki's 30-card list unchanged", b.Provenance == "LOOT" && b.Tier == "1"
        && b.HeroCard == "LOOTA_BOSS_35h" && b.Power == "LOOTA_BOSS_35p" && b.Source.Kind == DungeonRunDeckSource.KindAuthentic && data.BossDecks[b.Deck].Count == 30
        && data.BossDecks[b.Deck].Count(id => id == "EX1_246") == 2 && !b.BossGeneratedSecrets);
});

// ============================================================ S7-S14 Trapped Room: Alarm, Sawblade
Guard("TrappedRoom", () =>
{
    var g = Ready(Plan("LOOTA_BOSS_48h", "LOOTA_BOSS_48p", 40));
    var boss = g.Player2;
    Check("S7 Alarm is a passive hero power, started at match start", boss.Hero.HeroPower.IsPassiveHeroPower && boss.Hero.HeroPower[EncounterRules.PowerActive] == 1 && boss.Hero.HeroPower.ActivatedTrigger != null
        && DungeonRunCardsGen.PassiveHeroPowers.Contains("LOOTA_BOSS_48p") && DungeonRunEncounters.PassivePowers.Contains("LOOTA_BOSS_48p"));
    ToBoss(g);
    Check("S8 it is never offered as an option", !Offered(g));
    DungeonRunEncounters.PutSecret(boss, C("EX1_287"));
    DungeonRunEncounters.PutSecret(boss, C("ULD_152"));
    ToPlayer(g);
    var yeti = Board(g, g.Player1, "CS2_182");
    CastSpell(g);
    var blades = boss.BoardZone.Where(m => m.Card.Id == "LOOTA_BOSS_48t").ToList();
    Check("S9 the run player's spell reveals Counterspell: one 3/3 Sawblade is summoned for the boss; the countered spell never reaches Pressure Plate", blades.Count == 1 && blades[0].AttackDamage == 3 && blades[0].Health == 3
        && Secrets(boss).SequenceEqual(new[] { "ULD_152" }) && yeti.Zone == g.Player1.BoardZone, blades.Count + " blades, secrets " + string.Join(",", Secrets(boss)));
    CastSpell(g);
    Check("S10 the next spell resolves, then Pressure Plate destroys the run player's minion and a second Sawblade is summoned", boss.BoardZone.Count(m => m.Card.Id == "LOOTA_BOSS_48t") == 2
        && boss.SecretZone.Count == 0 && yeti.Zone != g.Player1.BoardZone);
    ToBoss(g);
    DungeonRunEncounters.PutSecret(boss, C("EX1_610"));
    DungeonRunEncounters.PutSecret(boss, C("EX1_611"));
    ToPlayer(g);
    Play(g, ToHand(g, g.Player1, "EX1_544")); // Flare: destroy all enemy Secrets
    Check("S11 Secrets destroyed unrevealed (Flare) summon nothing", boss.SecretZone.Count == 0 && boss.BoardZone.Count(m => m.Card.Id == "LOOTA_BOSS_48t") == 2);
    // The run player's own Secret is not "you reveal": a boss spell into the player's Counterspell summons nothing.
    Mana(g.Player1, 10);
    Play(g, ToHand(g, g.Player1, "EX1_287"));
    ToBoss(g);
    Play(g, ToHand(g, boss, "CS2_023"));
    Check("S12 a Secret the run player reveals summons no Sawblade", g.Player1.SecretZone.Count == 0 && boss.BoardZone.Count(m => m.Card.Id == "LOOTA_BOSS_48t") == 2);
    while (!boss.BoardZone.IsFull) Board(g, boss, "CS2_182");
    DungeonRunEncounters.PutSecret(boss, C("EX1_287"));
    ToPlayer(g);
    CastSpell(g);
    Check("S13 with a full board the reveal summons nothing and nothing breaks", boss.SecretZone.Count == 0 && boss.BoardZone.Count == 7 && g.State != State.COMPLETE);
    var row = data.Boss("loot_48_trapped_room");
    Check("S14 the shipped row: Trapped Room, Dungeon Run, tier 2, passive; its deck is the wiki's list unchanged (phase 8 family 08 scripted its two Shroomsayers)", row.Provenance == "LOOT" && row.Tier == "2" && row.Power == "LOOTA_BOSS_48p"
        && row.HasFlag("passive") && row.Cards.Contains("LOOTA_BOSS_48t") && data.BossDecks[row.Deck].Count == 30 && row.Source.Kind == DungeonRunDeckSource.KindAuthentic
        && row.Source.Substitutions.Count == 0 && data.BossDecks[row.Deck].Count(id => id == "LOOTA_107") == 2 && data.BossDecks[row.Deck].Count(id => C(id).IsSecret) == 24,
        row.Source.Kind + " " + data.BossDecks[row.Deck].Count(id => C(id).IsSecret));
});

// ============================================================ S15-S20 Manhunter Ivan: Trap Preparation
Guard("Ivan", () =>
{
    var g = Ready(Plan("GILA_BOSS_25h", "GILA_BOSS_25p", 25, Deck(("EX1_610", 1), ("EX1_611", 1), ("EX1_554", 1), ("CS2_182", 27))));
    var boss = g.Player2;
    ToBoss(g);
    SecretsToDeck(boss);
    int deck = boss.DeckZone.Count, entities = g.IdEntityDic.Count;
    var inDeck = boss.DeckZone.Where(p => p.Card.IsSecret).Select(p => p.Id).ToList();
    Check("S15 Trap Preparation costs (2) and puts a Secret of the boss's deck into play: the deck is one card shorter, no new entity", boss.Hero.HeroPower.Cost == 2 && inDeck.Count == 3 && Power(g)
        && boss.SecretZone.Count == 1 && boss.DeckZone.Count == deck - 1 && inDeck.Contains(boss.SecretZone[0].Id) && g.IdEntityDic.Count == entities && boss.UsedMana == 2
        && boss.SecretZone[0].ActivatedTrigger != null, boss.SecretZone.Count + " secrets, deck " + boss.DeckZone.Count + " of " + deck);
    for (int i = 0; i < 2; i++) { Round(g); SecretsToDeck(boss); Power(g); }
    Check("S16 used again it puts the others, never one in play: three different Secrets, none left in the deck", boss.SecretZone.Count == 3 && Secrets(boss).Distinct().Count() == 3
        && !boss.DeckZone.Any(p => p.Card.IsSecret), string.Join(",", Secrets(boss)));
    Round(g);
    int deckLeft = boss.DeckZone.Count;
    Check("S17 with no Secret left in the deck it is still offered (the offer never reads the deck) and does nothing but cost (2)", Offered(g) && Power(g) && boss.SecretZone.Count == 3
        && boss.DeckZone.Count == deckLeft && boss.UsedMana == 2);
    // Only a copy of the Secret in play is left: nothing is put, the copy stays in the deck. A full Secret zone: not offered.
    var g2 = Ready(Plan("GILA_BOSS_25h", "GILA_BOSS_25p", 25, Deck(("EX1_610", 2), ("CS2_182", 28))));
    ToBoss(g2);
    SecretsToDeck(g2.Player2);
    bool first = Power(g2);
    Round(g2); SecretsToDeck(g2.Player2);
    bool second = Power(g2);
    bool kept = g2.Player2.SecretZone.Count == 1 && g2.Player2.DeckZone.Count(p => p.Card.Id == "EX1_610") == 1;
    foreach (string id in hunterSecrets) DungeonRunEncounters.PutSecret(g2.Player2, C(id));
    Round(g2);
    Check("S18 a deck holding only a second copy of the Secret in play: nothing is put the second time; with a full Secret zone the power is not offered", first && second && kept
        && g2.Player2.SecretZone.IsFull && !Offered(g2));
    // The boss's own-deck ledger (no hand shuffling here: the history is the game's own).
    var g3 = Ready(Plan("GILA_BOSS_25h", "GILA_BOSS_25p", 25, Deck(("EX1_610", 2), ("EX1_611", 2), ("EX1_554", 2), ("EX1_609", 2), ("CS2_182", 22))), 3);
    ToBoss(g3);
    var before = OwnDeckLedger.Observe(g3, g3.Player2, null).Sample(g3, g3.Player2, 11);
    Power(g3);
    string put = Secrets(g3.Player2).Single();
    var after = OwnDeckLedger.Observe(g3, g3.Player2, null).Sample(g3, g3.Player2, 11);
    if (Environment.GetEnvironmentVariable("DEBUG_S19") == "1")
        foreach (var f in g3.HistoryFacts.Query.Events<ResidenceChangedFact>().Where(f => f.EntityId == g3.Player2.SecretZone[0].Id))
            Console.WriteLine($"  residence seq {f.Sequence} zone {f.Zone} entered {f.Entered} controller {f.ControllerId} (boss {g3.Player2.Id})");
    Check("S19 the boss's own-deck ledger drops exactly that Secret from its deck (it saw its own card leave)", before.Values.Sum() == after.Values.Sum() + 1
        && before[put] == after.GetValueOrDefault(put) + 1 && after.Values.Sum() == g3.Player2.DeckZone.Count, put + ": " + string.Join(",", after.Select(p => p.Key + "x" + p.Value)));
    var row = data.Boss("gila_25_ivan");
    Check("S20 the shipped row: Manhunter Ivan, Monster Hunt, tier 2, the wiki's 30 cards unchanged, boss-side generated Secrets on for Professor Putricide", row.Provenance == "GIL" && row.Tier == "2"
        && row.Source.Kind == DungeonRunDeckSource.KindAuthentic && data.BossDecks[row.Deck].Count == 30 && data.BossDecks[row.Deck].Contains("ICC_204") && row.BossGeneratedSecrets);
});

// ============================================================ S21-S27 Trap Room: Tricks and Traps
Guard("TrapRoom", () =>
{
    var g = Ready(Plan("ULDA_BOSS_44h", "ULDA_BOSS_44p", 30));
    var boss = g.Player2;
    ToBoss(g);
    Check("S21 with no Secret in hand or deck Tricks and Traps (2) is still offered and does nothing but cost (2)", boss.Hero.HeroPower.Cost == 2 && Offered(g) && Power(g)
        && boss.SecretZone.Count == 0 && boss.UsedMana == 2);
    Round(g);
    var held = ToHand(g, boss, "EX1_611");
    int hand = boss.HandZone.Count;
    Check("S22 it plays the Secret of the hand: an effect cast without payment (2 mana for the power only)", Offered(g) && Power(g) && Secrets(boss).SequenceEqual(new[] { "EX1_611" })
        && boss.HandZone.Count == hand - 1 && boss.UsedMana == 2 && ReferenceEquals(boss.SecretZone[0], held) && boss.SecretZone[0].ActivatedTrigger != null, string.Join(",", Secrets(boss)));
    Round(g);
    SecretsToDeck(boss);
    var second = ToHand(g, boss, "EX1_611");
    Check("S23 a second copy of the Secret in play is nothing to play: the power does nothing and the copy stays in hand", Power(g) && boss.SecretZone.Count == 1 && second.Zone == boss.HandZone);
    Round(g);
    boss.HandZone.Remove(second); boss.SetasideZone.Add(second);
    var top = Entity.FromCard(boss, C("ULD_152")); boss.DeckZone.Add(top);
    int deck = boss.DeckZone.Count;
    Check("S24 it plays a Secret of the deck when the hand holds none", Offered(g) && Power(g) && Secrets(boss).Contains("ULD_152") && boss.DeckZone.Count == deck - 1 && ReferenceEquals(boss.SecretZone.Last(), top));
    // Hand or deck: over many seeds both sources are picked.
    int fromHand = 0, fromDeck = 0;
    for (int seed = 1; seed <= 40; seed++)
    {
        var gs = Ready(Plan("ULDA_BOSS_44h", "ULDA_BOSS_44p", 30), seed, history: false);
        ToBoss(gs);
        ToHand(gs, gs.Player2, "EX1_610");
        gs.Player2.DeckZone.Add(Entity.FromCard(gs.Player2, C("EX1_554")));
        Power(gs);
        if (Secrets(gs.Player2).SequenceEqual(new[] { "EX1_610" })) fromHand++; else if (Secrets(gs.Player2).SequenceEqual(new[] { "EX1_554" })) fromDeck++;
    }
    Check("S25 \"hand or deck\": one random Secret of either, by the game RNG (both occur over 40 seeds, one Secret each time)", fromHand > 5 && fromDeck > 5 && fromHand + fromDeck == 40, fromHand + " hand, " + fromDeck + " deck");
    // The boss's own-deck ledger: the first seed whose pick comes from the deck.
    bool ledgerOk = false; string ledgerNote = "no seed played from the deck";
    for (int seed = 1; seed <= 20 && !ledgerOk; seed++)
    {
        var g3 = Ready(Plan("ULDA_BOSS_44h", "ULDA_BOSS_44p", 30, Deck(("EX1_610", 2), ("EX1_611", 2), ("EX1_554", 2), ("EX1_609", 2), ("ULD_152", 2), ("CS2_182", 20))), seed);
        ToBoss(g3);
        int handCount = g3.Player2.HandZone.Count;
        var before = OwnDeckLedger.Observe(g3, g3.Player2, null).Sample(g3, g3.Player2, 11);
        Power(g3);
        if (g3.Player2.HandZone.Count != handCount) continue;
        string played = Secrets(g3.Player2).Single();
        var after = OwnDeckLedger.Observe(g3, g3.Player2, null).Sample(g3, g3.Player2, 11);
        ledgerOk = before.Values.Sum() == after.Values.Sum() + 1 && before[played] == after.GetValueOrDefault(played) + 1 && after.Values.Sum() == g3.Player2.DeckZone.Count;
        ledgerNote = "seed " + seed + " " + played + ": " + string.Join(",", after.Select(p => p.Key + "x" + p.Value));
    }
    Check("S26 a Secret played from the deck: the boss's own-deck ledger drops exactly that Secret", ledgerOk, ledgerNote);
    var row = data.Boss("ulda_44_trap_room");
    Check("S27 the shipped row: Trap Room, Tombs of Terror, tier 3; the wiki's level 6 list unchanged (Commander Rhyssa is scripted since phase 8 family 02); Pressure Plate twice", row.Provenance == "ULDA" && row.Tier == "3"
        && data.BossDecks[row.Deck].Count == 30 && data.BossDecks[row.Deck].Count(id => id == "ULD_152") == 2 && data.BossDecks[row.Deck].Count(id => id == "EX1_080") == 4
        && row.Source.Kind == DungeonRunDeckSource.KindAuthentic && row.Source.Substitutions.Count == 0 && data.BossDecks[row.Deck].Count(id => id == "DAL_573") == 1
        && row.BossGeneratedSecrets && data.BossDecks[row.Deck].Contains("ICC_204"));
});

// ============================================================ S28-S33 Pressure Plate (ULD_152)
Guard("PressurePlate", () =>
{
    Check("S28 ULD_152 is scripted, a Hunter Secret, and keeps the client's text", C("ULD_152").Implemented && C("ULD_152").IsSecret && C("ULD_152").Class == CardClass.HUNTER
        && C("ULD_152").Text == factById["ULD_152"].GetProperty("text").GetString());
    var g = Ready(Plan("HERO_05", "DS1h_292"));
    var boss = g.Player2;
    ToBoss(g);
    Check("S29 the boss plays it from hand for (2): a Secret in its zone", Play(g, ToHand(g, boss, "ULD_152")) && Secrets(boss).SequenceEqual(new[] { "ULD_152" }) && boss.UsedMana == 2);
    ToPlayer(g);
    CastSpell(g);
    Check("S30 with no enemy minion the run player's spell leaves it face down", Secrets(boss).SequenceEqual(new[] { "ULD_152" }));
    var a = Board(g, g.Player1, "CS2_182"); var b = Board(g, g.Player1, "CS2_182");
    var own = Board(g, boss, "CS2_182");
    int hand = g.Player1.HandZone.Count;
    CastSpell(g);
    Check("S31 after the run player's spell resolved (its cards are drawn) exactly one of their minions is destroyed and the Secret is revealed; the boss's own minion is untouched",
        g.Player1.HandZone.Count == hand + 2 && g.Player1.BoardZone.Count == 1 && (a.Zone == g.Player1.BoardZone) != (b.Zone == g.Player1.BoardZone) && boss.SecretZone.Count == 0 && own.Zone == boss.BoardZone,
        g.Player1.HandZone.Count + " vs " + hand + ", board " + g.Player1.BoardZone.Count);
    // Its owner's own spell does not trigger it.
    ToBoss(g);
    Play(g, ToHand(g, boss, "ULD_152"));
    Play(g, ToHand(g, boss, "CS2_023"));
    Check("S32 its owner's own spell does not trigger it", Secrets(boss).SequenceEqual(new[] { "ULD_152" }) && g.Player1.BoardZone.Count == 1);
    Check("S33 it is in the closed Hunter pools: a boss's generated Secrets and the boss prior of a Hunter run's generation; admitted for both sides, generated by the run player only",
        hunterPool.Contains("ULD_152") && DungeonRunAdmission.GeneratedSecretPool(new[] { "HUNTER" }).Contains("ULD_152")
        && DungeonRunAdmission.Classify(C("ULD_152"), "ULD_152", AdmitScope.Both, null, catalogRun: true).Admitted && !DungeonRunAdmission.GeneratedRejects(true).Contains("ULD_152")
        && DungeonRunAdmission.GeneratedRejectsBoss(true).Contains("ULD_152"));
});

// ============================================================ S34-S41 the boss row of the generated-Secret lift: Putricide, Streetsmuggler
Guard("BossGenerated", () =>
{
    var on = Ready(Plan("GILA_BOSS_25h", "GILA_BOSS_25p", 25, bossSecrets: true));
    var boss = on.Player2;
    ToBoss(on);
    Board(on, boss, "ICC_204");
    Play(on, ToHand(on, boss, "EX1_611"));
    Check("S34 Professor Putricide in a boss's hands, boss side on: after the boss plays a Secret a random Hunter Secret joins it, from the validator's closed pool, never the one in play",
        boss.SecretZone.Count == 2 && Secrets(boss)[0] == "EX1_611" && Secrets(boss)[1] != "EX1_611" && hunterPool.Contains(Secrets(boss)[1]),
        string.Join(",", Secrets(boss)));
    var seen = new HashSet<string>();
    for (int seed = 1; seed <= 60; seed++)
    {
        var gs = Ready(Plan("GILA_BOSS_25h", "GILA_BOSS_25p", 25, bossSecrets: true), seed, history: false);
        ToBoss(gs); Board(gs, gs.Player2, "ICC_204"); Play(gs, ToHand(gs, gs.Player2, "EX1_611"));
        foreach (string id in Secrets(gs.Player2).Skip(1)) seen.Add(id);
    }
    Check("S35 over 60 seeds every generated Secret is a scripted Hunter Secret of the pool, admitted for a boss, and several different ones occur", seen.Count >= 4 && seen.All(hunterPool.Contains)
        && seen.All(id => C(id).Implemented && DungeonRunAdmission.Classify(C(id), id, AdmitScope.Boss, null, catalogRun: true).Admitted), string.Join(",", seen.OrderBy(x => x)));
    Check("S36 the engine's pool for the boss is the validator's (Hunter Secrets, whatever the boss's class)", DungeonRunContent.RandomSecretCandidates(boss, CardClass.HUNTER).Select(c => c.Id).SequenceEqual(hunterPool)
        && hunterPool.Count >= 8, hunterPool.Count + " Hunter Secrets");
    var off = Ready(Plan("GILA_BOSS_25h", "GILA_BOSS_25p", 25));
    ToBoss(off); Board(off, off.Player2, "ICC_204"); Play(off, ToHand(off, off.Player2, "EX1_611"));
    Check("S37 boss side off (every other boss): the same play generates nothing, which is why the validator refuses that row", Secrets(off.Player2).SequenceEqual(new[] { "EX1_611" }));
    // Streetsmuggler on a Warrior boss: the Secret pool is the Hunter one, not the portrait's.
    var sm = Ready(Plan("HERO_01", "CS2_102", 30, bossSecrets: true));
    ToBoss(sm);
    Board(sm, sm.Player2, "TRLA_168");
    DungeonRunEncounters.PutSecret(sm.Player2, C("EX1_287"));
    ToPlayer(sm);
    CastSpell(sm);
    Check("S38 Streetsmuggler in a boss's hands, boss side on: after the boss's Secret is revealed a random Hunter Secret of the pool is put into its zone", sm.Player2.SecretZone.Count == 1
        && hunterPool.Contains(Secrets(sm.Player2)[0]) && C(Secrets(sm.Player2)[0]).Class == CardClass.HUNTER, string.Join(",", Secrets(sm.Player2)));
    var smOff = Ready(Plan("HERO_01", "CS2_102", 30));
    ToBoss(smOff); Board(smOff, smOff.Player2, "TRLA_168"); DungeonRunEncounters.PutSecret(smOff.Player2, C("EX1_287")); ToPlayer(smOff); CastSpell(smOff);
    Check("S39 boss side off: Streetsmuggler generates nothing for the boss", smOff.Player2.SecretZone.Count == 0);
    // The run player's side is unchanged by the boss lift.
    var pl = Ready(Plan("HERO_01", "CS2_102", 30, bossSecrets: true));
    Mana(pl.Player1, 10);
    Board(pl, pl.Player1, "ICC_204");
    Play(pl, ToHand(pl, pl.Player1, "EX1_287"));
    Check("S40 the run player's Putricide still generates from the run player's own lists", pl.Player1.SecretZone.Count == 2 && C(Secrets(pl.Player1)[1]).Class == CardClass.HUNTER && C(Secrets(pl.Player1)[1]).Implemented,
        string.Join(",", Secrets(pl.Player1)));
    // Admission and the generation lists.
    foreach (string id in new[] { "ICC_204", "TRLA_168" })
    {
        var boss1 = DungeonRunAdmission.Classify(C(id), id, AdmitScope.Boss, null, catalogRun: true);
        Check("S41 " + id + ": admitted for a boss on a catalog run with the generated-Secrets need (Hunter pool), still the run player's and generated for the player; rejected on a legacy run; never the boss's own generation",
            boss1.Admitted && boss1.Rule.BossNeeds == DungeonRunAdmission.BossNeedsGeneratedSecrets && boss1.Rule.BossSecretClass == CardClass.HUNTER && boss1.Family == "secret-generated"
            && DungeonRunAdmission.Classify(C(id), id, AdmitScope.Player | AdmitScope.Generated, null, catalogRun: true).Admitted && !DungeonRunAdmission.Classify(C(id), id, AdmitScope.Any).AdmittedSome
            && DungeonRunAdmission.GeneratedRejectsBoss(true).Contains(id) && !DungeonRunAdmission.GeneratedRejects(true).Contains(id));
    }
});

// ============================================================ S42-S50 the rows through the run layer and the validator
DungeonRunState At(string cls, long seed, string bossId)
{
    var c = data.Class(cls);
    var s = run.NewRun(all, c.Id, c.Decks[0], c.Powers[0], seed, schedule: "acts-v3h");
    // The first pre-match (the anomaly node before it takes no rule); these plan fields do not depend on the floor.
    while (s.Phase != DungeonRunPhase.PreMatch)
    {
        if (s.Phase != DungeonRunPhase.Modifier) throw new Exception("walk stopped at " + s.Phase);
        run.PickModifier(s, s.ModifierOffer.Kind == DungeonRunGlobalRule.KindAnomaly ? DungeonRunGlobalRule.None : s.ModifierOffer.Offered[0]);
    }
    s.BossSequence[s.Floor - 1] = bossId;
    return s;
}
Guard("RunLayer", () =>
{
    var errors = DungeonRunValidator.Validate(data, null);
    Check("S42 the shipped data validates with the four Secret bosses", errors.Count == 0, string.Join(" | ", errors.Take(4)));
    foreach (var (id, hero, power, secrets) in new[] { ("loot_35_brandlemar", "LOOTA_BOSS_35h", "LOOTA_BOSS_35p", false), ("loot_48_trapped_room", "LOOTA_BOSS_48h", "LOOTA_BOSS_48p", false),
        ("gila_25_ivan", "GILA_BOSS_25h", "GILA_BOSS_25p", true), ("ulda_44_trap_room", "ULDA_BOSS_44h", "ULDA_BOSS_44p", true) })
    {
        var s = At("MAGE", 4400, id);
        var plan = run.BuildPlan(s, all);
        var cfg = new GameConfig { RandomSeed = 5, Shuffle = true, SkipMulligan = false, History = false, Logging = false, FormatType = FormatType.FT_WILD };
        DungeonRunSetup.Configure(cfg, plan);
        Check("S43 " + id + ": the plan is the row's (hero, power, 30-card deck, the row's boss-side generated Secrets) and Configure follows it", plan.EncounterId == id && plan.BossHero.Id == hero
            && plan.BossHeroPowerId == power && plan.BossDeck.Count == 30 && plan.EncounterDeck && plan.BossGeneratedSecrets == secrets && cfg.Player2ExcludeGeneratedSecrets == !secrets
            && C(hero).Type == CardType.HERO && data.Schedules.Member(data.Boss(id), DungeonRunEncounterData.PoolOf(data.Boss(id).Tier)));
    }
    foreach (string id in new[] { "LOOTA_BOSS_35p", "LOOTA_BOSS_48p", "GILA_BOSS_25p", "ULDA_BOSS_44p" })
        Check("S44 " + id + " is scripted and keeps the client's text", C(id).Implemented && C(id).Text == factById[id].GetProperty("text").GetString());
    Check("S45 Sawblade is a scripted 3/3 token for (3)", C("LOOTA_BOSS_48t").Implemented && C("LOOTA_BOSS_48t").Type == CardType.MINION && C("LOOTA_BOSS_48t").ATK == 3 && C("LOOTA_BOSS_48t").Health == 3 && C("LOOTA_BOSS_48t").Cost == 3);
    foreach (string id in new[] { "LOOTA_BOSS_35p", "LOOTA_BOSS_48p", "GILA_BOSS_25p", "ULDA_BOSS_44p" })
    {
        var v = DungeonRunAdmission.Classify(C(id), id, AdmitScope.Boss, null, catalogRun: true);
        // The secret-from-deck family is admitted in every scope (it generates nothing); Dampen Magic (secret-named) is a boss's only.
        bool bossOnly = id == "LOOTA_BOSS_35p";
        Check("S46 " + id + ": admitted for a boss by family 01's row (family " + v.Family + "), on legacy runs too, with no boss-side need", v.Admitted && v.Rule.Ids.Contains(id)
            && DungeonRunAdmission.Classify(C(id), id, AdmitScope.Boss).Admitted && v.Family == (bossOnly ? "secret-named" : "secret-from-deck")
            && DungeonRunAdmission.Classify(C(id), id, AdmitScope.Player, null, catalogRun: true).Admitted == !bossOnly && v.Rule.BossNeeds == null);
    }
    // The validator: a row that lists Putricide without the boss side on is refused; the Hunter pool is checked for any portrait.
    DungeonRunData Changed(Action<DungeonRunData> change) { var d = Load(); change(d); return d; }
    var offErrors = DungeonRunValidator.Validate(Changed(d => d.Boss("gila_25_ivan").BossGeneratedSecrets = false), null);
    Check("S47 Ivan's row without bossGeneratedSecrets is refused: Professor Putricide would do nothing", offErrors.Count == 1 && offErrors[0].StartsWith("ICC_204: generates a Secret for the boss, but the row keeps"), string.Join(" | ", offErrors.Take(3)));
    var needless = DungeonRunValidator.Validate(Changed(d => d.Boss("loot_35_brandlemar").BossGeneratedSecrets = true), null);
    Check("S48 a row that turns the boss side on with no card that needs it is refused", needless.Count == 1 && needless[0].Contains("bossGeneratedSecrets with no card that generates a Secret"), string.Join(" | ", needless.Take(3)));
    var smuggler = DungeonRunValidator.Validate(Changed(d => { var b = d.Boss("loot_35_brandlemar"); d.BossDecks[b.Deck][0] = "TRLA_168"; }), null);
    var smugglerOn = DungeonRunValidator.Validate(Changed(d => { var b = d.Boss("loot_35_brandlemar"); d.BossDecks[b.Deck][0] = "TRLA_168"; b.BossGeneratedSecrets = true; }), null);
    Check("S49 Streetsmuggler in a Shaman boss's deck: refused with the boss side off, accepted with it on (the Hunter pool is closed and boss-admitted)",
        smuggler.Any(e => e.StartsWith("TRLA_168: generates a Secret for the boss")) && !smugglerOn.Any(e => e.Contains("TRLA_168") || e.Contains("bossGeneratedSecrets") || e.Contains("the boss's generation")),
        string.Join(" | ", smuggler.Concat(smugglerOn).Take(4)));
    Check("S50 every Secret of the Hunter pool is admitted for a boss", hunterPool.All(id => DungeonRunAdmission.Classify(C(id), id, AdmitScope.Boss, null, catalogRun: true).Admitted));
});

Console.WriteLine($"SearchTelemetry swallowed leaf exceptions {SearchTelemetry.SwallowedLeafErrors}");
Console.WriteLine($"RESULT {passes} passed, {failures + SearchTelemetry.SwallowedLeafErrors} failed");
return failures + SearchTelemetry.SwallowedLeafErrors;
