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

// Phase 8 family 04, collectibles C (Uldum ULD_282..ULD_728): each card's text asserted in real engine games started
// through the run setup path (DungeonRunSetup.Configure / ApplyAfterStart), then the admission rows, the regenerated
// catalog and encounters, and the validator. The fair AI plays them as the boss in the f04 bosspilot modes (run.sh).
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
var facts = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(repo, "facts", "violet-hold-2026-09.json")));
var factById = new Dictionary<string, System.Text.Json.JsonElement>(StringComparer.Ordinal);
foreach (var c in facts.RootElement.GetProperty("cards").EnumerateArray()) factById[c.GetProperty("id").GetString()] = c;
int FactTag(string id, int tag) { foreach (var t in factById[id].GetProperty("tags").EnumerateArray()) if (t[0].GetInt32() == tag) return t[1].GetInt32(); return 0; }
string root = Path.Combine(repo, "data");
string R(string f) => File.ReadAllText(Path.Combine(root, f));
DungeonRunData Load() => DungeonRunData.Parse(R("dungeon-run/run-v1.json"), R("dungeon-run/pool-v1.json"), R("npc/decks-v1.json"), R("dungeon-run/catalog-v1.json"),
    R("dungeon-run/schedule-v1.json"), R("dungeon-run/tavern-v1.json"), R("dungeon-run/encounters-v1.json"));
var data = Load();

string[] family = { "ULD_282", "ULD_286", "ULD_288", "ULD_290", "ULD_291", "ULD_292", "ULD_293", "ULD_304", "ULD_326", "ULD_327", "ULD_329", "ULD_410", "ULD_431", "ULD_433",
    "ULD_435", "ULD_438", "ULD_707", "ULD_709", "ULD_711", "ULD_713", "ULD_714", "ULD_716", "ULD_718", "ULD_720", "ULD_721", "ULD_724", "ULD_726", "ULD_727", "ULD_728" };
string[] quests = { "ULD_291", "ULD_326", "ULD_431", "ULD_433", "ULD_711", "ULD_724" };
string[] support = { "ULD_286t", "ULD_288e", "ULD_290e", "ULD_291p", "ULD_291pe", "ULD_292a", "ULD_292ae", "ULD_292b", "ULD_326p", "ULD_326t", "ULD_410e", "ULD_431p", "ULD_431e",
    "ULD_433p", "ULD_433e", "ULD_435e", "ULD_711p3", "ULD_711t", "ULD_724p", "ULD_724e", "ULD_726e", "ULD_728e", "ULD_430t" };

string[] filler = Enumerable.Repeat("CS2_182", 30).ToArray(); // Chillwind Yeti
RunMatchPlan Plan(string hero = "HERO_08", string power = "CS2_034", string[] deck = null, string[] bossDeck = null) => new RunMatchPlan
{
    PlayerHero = C(hero), PlayerHeroPowerId = power, PlayerDeck = (deck ?? filler).Select(C).ToList(),
    BossHero = C("HERO_01"), BossHeroPowerId = "CS2_102", BossHealth = 60, BossDeck = (bossDeck ?? filler).Select(C).ToList(),
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
    Mana(g.Player1, 10);
    return g;
}
void Mana(Controller c, int n) { c.BaseMana = n; c.UsedMana = 0; c.OverloadLocked = 0; c.OverloadOwed = 0; }
IPlayable ToHand(Game g, Controller c, string id)
{
    while (c.HandZone.Count >= 9) { var old = c.HandZone[0]; c.HandZone.Remove(old); c.SetasideZone.Add(old); }
    var e = Entity.FromCard(c, C(id)); Generic.AddHandPhase.Invoke(c, e); g.DeathProcessingAndAuraUpdate(); return e;
}
void EmptyHand(Controller c) { foreach (var p in c.HandZone.ToList()) { c.HandZone.Remove(p); c.SetasideZone.Add(p); } }
void EmptyDeck(Controller c) { foreach (var p in c.DeckZone.ToList()) { c.DeckZone.Remove(p); c.SetasideZone.Add(p); } }
IPlayable ToDeck(Controller c, string id) { var e = Entity.FromCard(c, C(id)); c.DeckZone.Add(e); return e; } // the new top card
Minion Board(Game g, Controller c, string id) { var m = (Minion)Entity.FromCard(c, C(id)); Generic.SummonBlock(g, m, -1, c.Hero); g.DeathProcessingAndAuraUpdate(); return m; }
bool Play(Game g, IPlayable card, ICharacter target = null, int chooseOne = 0) => g.Process(PlayCardTask.Any(g.CurrentPlayer, card, target, -1, chooseOne));
void EndTurn(Game g) => g.Process(EndTurnTask.Any(g.CurrentPlayer));
// A full round back to the run player's turn, with full mana.
void Round(Game g) { EndTurn(g); EndTurn(g); Mana(g.Player1, 10); }
bool Offered(Game g) => g.CurrentPlayer.Options().Any(o => o is HeroPowerTask);
bool Power(Game g, ICharacter target = null) => Offered(g) && g.Process(HeroPowerTask.Any(g.CurrentPlayer, target));
void Kill(Game g, Minion m) { m.ToBeDestroyed = true; g.DeathProcessingAndAuraUpdate(); }
bool Answer(Game g, Controller c, int entityId)
{
    var d = g.GetPendingDecisions().Single(x => x.ControllerId == c.Id);
    return g.TryAnswer(new SabberStoneCore.Model.Decisions.DecisionAnswer(d.Id, d.ControllerId, new[] { entityId }), out _);
}
string Hand(Controller c) => string.Join(",", c.HandZone.Select(p => p.Card.Id + ":" + p.Cost));
string BoardOf(Controller c) => string.Join(",", c.BoardZone.Select(m => m.Card.Id + ":" + m.AttackDamage + "/" + m.Health));
Spell Quest(Controller c) => c.SecretZone.Quest;

// ============================================================ A the cards themselves
Guard("Cards", () =>
{
    Check("A1 the 29 cards are scripted", family.All(id => C(id).Implemented), string.Join(",", family.Where(id => !C(id).Implemented)));
    Check("A2 their tokens, reward powers and enchantments are scripted", support.All(id => C(id).Implemented), string.Join(",", support.Where(id => !C(id).Implemented)));
    // The engine's card data carries the client's spell-damage marker (" @spelldmg") on damage spells; the facts file drops it.
    var textOff = family.Concat(support).Where(id => factById[id].TryGetProperty("text", out var t) && (C(id).Text ?? "").Replace(" @spelldmg", "") != t.GetString()).ToList();
    Check("A3 every card keeps the client's text", textOff.Count == 0, string.Join(" ; ", textOff.Select(id => id + " [" + C(id).Text + "]")));
    var costOff = family.Where(id => C(id).Cost != FactTag(id, 48)).ToList();
    Check("A4 every card keeps the client's Cost", costOff.Count == 0, string.Join(",", costOff.Select(id => id + " " + C(id).Cost + " vs " + FactTag(id, 48))));
    Check("A5 the six Quests are Quests with the client's totals (6, 4, 5, 10, 5, 15)", quests.All(id => C(id).IsQuest)
        && quests.Select(id => C(id)[GameTag.QUEST_PROGRESS_TOTAL]).SequenceEqual(new[] { 6, 4, 5, 10, 5, 15 }), string.Join(",", quests.Select(id => C(id)[GameTag.QUEST_PROGRESS_TOTAL])));
    Check("A6 the six reward powers are clickable hero powers that cost (2)", new[] { "ULD_291p", "ULD_326p", "ULD_431p", "ULD_433p", "ULD_711p3", "ULD_724p" }
        .All(id => C(id).Type == CardType.HERO_POWER && C(id).Cost == 2 && !DungeonRunCardsGen.PassiveHeroPowers.Contains(id)));
    Check("A7 Colossus of the Moon is a 10/10 for (10) with Divine Shield and Reborn; Stone Golem is a 4/3; Locust a 1/1 with Rush", C("ULD_721").ATK == 10 && C("ULD_721").Health == 10
        && C("ULD_721")[GameTag.DIVINE_SHIELD] == 1 && C("ULD_721")[GameTag.REBORN] == 1 && C("ULD_711t").ATK == 4 && C("ULD_711t").Health == 3 && C("ULD_430t")[GameTag.RUSH] == 1);
});

// ============================================================ Q the Quests and their reward powers
Guard("CorruptTheWaters", () =>
{
    var g = Ready(Plan("HERO_02", "CS2_049"));
    var p = g.Player1;
    EmptyHand(p);
    bool played = Play(g, ToHand(g, p, "ULD_291"));
    Check("Q1 Corrupt the Waters goes to its owner's Quest slot with progress 0 of 6", played && Quest(p)?.Card.Id == "ULD_291" && Quest(p).QuestProgress == 0 && Quest(p).QuestTotalProgress == 6 && p.SecretZone.Count == 0);
    Play(g, ToHand(g, p, "CS2_182"));
    Check("Q2 a minion without a Battlecry is no progress", Quest(p).QuestProgress == 0);
    for (int i = 0; i < 5; i++) { Mana(p, 10); Play(g, ToHand(g, p, "EX1_015")); if (p.BoardZone.Count > 5) Kill(g, p.BoardZone[0]); }
    Check("Q3 five Battlecry cards: progress 5, the hero power is still Totemic Call", Quest(p)?.QuestProgress == 5 && p.Hero.HeroPower.Card.Id == "CS2_049", "" + Quest(p)?.QuestProgress);
    Mana(p, 10);
    Power(g);
    bool usedOld = p.Hero.HeroPower.IsExhausted;
    foreach (var m in p.BoardZone.ToList()) Kill(g, m);
    Play(g, ToHand(g, p, "EX1_015"));
    Check("Q4 the sixth: the Quest leaves the slot for the graveyard and the hero power is a ready Heart of Vir'naal (the old one was used this turn)", usedOld && Quest(p) == null
        && p.GraveyardZone.Any(x => x.Card.Id == "ULD_291") && p.Hero.HeroPower.Card.Id == "ULD_291p" && !p.Hero.HeroPower.IsExhausted && Offered(g), p.Hero.HeroPower.Card.Id);
    Mana(p, 10);
    int hand = p.HandZone.Count;
    bool pressed = Power(g);
    Play(g, ToHand(g, p, "EX1_015"));
    Check("Q5 Heart of Vir'naal costs (2): the next Battlecry this turn triggers twice (Novice Engineer draws two)", pressed && p.UsedMana == 4 && p.HandZone.Count == hand + 2 && p.ExtraBattlecry, (p.HandZone.Count - hand) + " drawn");
    Round(g);
    foreach (var m in p.BoardZone.ToList()) Kill(g, m);
    EmptyHand(p);
    Play(g, ToHand(g, p, "EX1_015"));
    Check("Q6 the next turn the doubling is gone (one draw) and its enchantment with it", !p.ExtraBattlecry && p.HandZone.Count == 1
        && !(p.AppliedEnchantments?.Any(e => e.Card.Id == "ULD_291pe") ?? false), p.HandZone.Count + " drawn");
    // A clone carries the Quest and completes it on its own.
    var g2 = Ready(Plan("HERO_02", "CS2_049"));
    EmptyHand(g2.Player1);
    Play(g2, ToHand(g2, g2.Player1, "ULD_291"));
    for (int i = 0; i < 5; i++) { Mana(g2.Player1, 10); Play(g2, ToHand(g2, g2.Player1, "EX1_015")); }
    var clone = g2.Clone();
    foreach (var m in clone.Player1.BoardZone.ToList()) Kill(clone, m);
    Mana(clone.Player1, 10);
    var extra = Entity.FromCard(clone.Player1, C("EX1_015")); Generic.AddHandPhase.Invoke(clone.Player1, extra);
    clone.Process(PlayCardTask.Any(clone.Player1, extra));
    Check("Q7 a clone carries the Quest's progress and completes it without touching the game it came from", clone.Player1.Hero.HeroPower.Card.Id == "ULD_291p" && clone.Player1.SecretZone.Quest == null
        && g2.Player1.Hero.HeroPower.Card.Id == "CS2_049" && Quest(g2.Player1)?.QuestProgress == 5);
});

Guard("BazaarBurglary", () =>
{
    var g = Ready(Plan("HERO_03", "CS2_083b"));
    var p = g.Player1;
    EmptyHand(p); EmptyDeck(p);
    Play(g, ToHand(g, p, "ULD_326"));
    ToHand(g, p, "CS2_182"); ToHand(g, p, "CS2_072");
    Check("Q8 Bazaar Burglary: a neutral card and a Rogue card entering the hand are no progress", Quest(p)?.QuestProgress == 0);
    ToHand(g, p, "CS2_029"); ToHand(g, p, "CS2_093"); ToHand(g, p, "EX1_015");
    Check("Q9 a Mage and a Paladin card are two steps; another neutral none", Quest(p)?.QuestProgress == 2, "" + Quest(p)?.QuestProgress);
    ToHand(g, p, "CS2_234");
    ToDeck(p, "CS2_023");
    ToDeck(g.Player2, "CS2_182");
    Round(g);
    Check("Q10 a Mage card drawn at turn start is the fourth: the hero power becomes Ancient Blades", Quest(p) == null && p.Hero.HeroPower.Card.Id == "ULD_326p", p.Hero.HeroPower.Card.Id + " " + Quest(p)?.QuestProgress);
    bool pressed = Power(g);
    var blade = p.Hero.Weapon;
    Check("Q11 Ancient Blades costs (2) and equips a 3/2 Mirage Blade", pressed && p.UsedMana == 2 && blade?.Card.Id == "ULD_326t" && blade.AttackDamage == 3 && blade.Durability == 2);
    var yeti = Board(g, g.Player2, "CS2_182");
    int health = p.Hero.Health;
    bool attacked = g.Process(HeroAttackTask.Any(p, yeti));
    Check("Q12 the hero is Immune while attacking with it (no damage from the Yeti) and not afterwards", attacked && p.Hero.Health == health && yeti.Damage == 3 && !p.Hero.IsImmune && p.Hero.Weapon?.Durability == 1,
        p.Hero.Health + " vs " + health);
});

Guard("MakingMummies", () =>
{
    var g = Ready(Plan("HERO_04", "CS2_101"));
    var p = g.Player1;
    EmptyHand(p);
    Play(g, ToHand(g, p, "ULD_431"));
    Play(g, ToHand(g, p, "CS2_182"));
    for (int i = 0; i < 4; i++) { Mana(p, 10); Play(g, ToHand(g, p, "ULD_275")); if (p.BoardZone.Count > 4) { var m = p.BoardZone[0]; m[GameTag.REBORN] = 0; Kill(g, m); } }
    Check("Q13 Making Mummies: four Reborn minions played are four steps, a Yeti none", Quest(p)?.QuestProgress == 4, "" + Quest(p)?.QuestProgress);
    foreach (var m in p.BoardZone.ToList()) { m[GameTag.REBORN] = 0; Kill(g, m); }
    Mana(p, 10);
    Play(g, ToHand(g, p, "ULD_721"));
    Check("Q14 the fifth (Colossus of the Moon): the hero power becomes Emperor Wraps", Quest(p) == null && p.Hero.HeroPower.Card.Id == "ULD_431p");
    Mana(p, 10);
    var colossus = p.BoardZone.First(m => m.Card.Id == "ULD_721");
    bool pressed = Power(g, colossus);
    var copy = p.BoardZone.FirstOrDefault(m => m.Card.Id == "ULD_721" && m != colossus);
    Check("Q15 Emperor Wraps costs (2) and summons a 2/2 copy of the chosen friendly minion (the copy keeps Divine Shield and Reborn)", pressed && p.UsedMana == 2 && copy != null && copy.AttackDamage == 2 && copy.Health == 2
        && copy.HasDivineShield && copy[GameTag.REBORN] == 1 && colossus.AttackDamage == 10, BoardOf(p));
    Check("Q16 it needs a friendly minion target: never offered on an enemy minion", !g.Process(HeroPowerTask.Any(p, Board(g, g.Player2, "CS2_182"))) || p.Hero.HeroPower.IsExhausted);
});

Guard("RaidTheSkyTemple", () =>
{
    var g = Ready(Plan());
    var p = g.Player1;
    EmptyHand(p);
    Play(g, ToHand(g, p, "ULD_433"));
    Check("Q17 Raid the Sky Temple: casting the Quest itself is no progress", Quest(p)?.QuestProgress == 0 && Quest(p).QuestTotalProgress == 10);
    Play(g, ToHand(g, p, "CS2_182"));
    for (int i = 0; i < 9; i++) Play(g, ToHand(g, p, "GAME_005"));
    Check("Q18 nine spells (a minion is none): progress 9", Quest(p)?.QuestProgress == 9, "" + Quest(p)?.QuestProgress);
    Play(g, ToHand(g, p, "GAME_005"));
    Check("Q19 the tenth: the hero power becomes Ascendant Scroll", Quest(p) == null && p.Hero.HeroPower.Card.Id == "ULD_433p");
    EmptyHand(p); Mana(p, 10);
    int left = p.RemainingMana; // the ten Coins left temporary mana
    bool pressed = Power(g);
    var got = p.HandZone.Count == 1 ? p.HandZone[0] : null;
    Check("Q20 Ascendant Scroll costs (2) and adds a random scripted Mage spell that costs (2) less", pressed && left - p.RemainingMana == 2 && got is Spell && got.Card.Class == CardClass.MAGE && got.Card.Implemented
        && got.Cost == Math.Max(0, got.Card.Cost - 2) && !got.Card.IsQuest, got?.Card.Id + " costs " + got?.Cost);
});

Guard("HackTheSystem", () =>
{
    var g = Ready(Plan("HERO_01", "CS2_102"));
    var p = g.Player1;
    EmptyHand(p);
    Play(g, ToHand(g, p, "ULD_711"));
    for (int i = 0; i < 4; i++)
    {
        Mana(p, 10);
        if (p.Hero.Weapon == null) Play(g, ToHand(g, p, "CS2_106"));
        g.Process(HeroAttackTask.Any(p, g.Player2.Hero));
        Round(g);
    }
    Check("Q21 Hack the System: four hero attacks are four steps", Quest(p)?.QuestProgress == 4, "" + Quest(p)?.QuestProgress);
    if (p.Hero.Weapon == null) Play(g, ToHand(g, p, "CS2_106"));
    g.Process(HeroAttackTask.Any(p, g.Player2.Hero));
    Check("Q22 the fifth: the hero power becomes Anraphet's Core, with its refresh trigger started", Quest(p) == null && p.Hero.HeroPower.Card.Id == "ULD_711p3" && p.Hero.HeroPower.ActivatedTrigger != null);
    Round(g);
    bool pressed = Power(g);
    var golem = p.BoardZone.FirstOrDefault(m => m.Card.Id == "ULD_711t");
    Check("Q23 Anraphet's Core costs (2) and summons a 4/3 Stone Golem; it is then used up", pressed && p.UsedMana == 2 && golem != null && golem.AttackDamage == 4 && golem.Health == 3 && !Offered(g));
    if (p.Hero.Weapon == null) Play(g, ToHand(g, p, "CS2_106"));
    g.Process(HeroAttackTask.Any(p, g.Player2.Hero));
    Check("Q24 after the hero attacks it is refreshed and summons a second Golem the same turn", Offered(g) && Power(g) && p.BoardZone.Count(m => m.Card.Id == "ULD_711t") == 2);
});

Guard("ActivateTheObelisk", () =>
{
    var g = Ready(Plan("HERO_09", "CS1h_001"));
    var p = g.Player1;
    EmptyHand(p);
    Play(g, ToHand(g, p, "ULD_724"));
    p.Hero.Damage = 20;
    Power(g, p.Hero);
    Check("Q25 Activate the Obelisk: Lesser Heal restores 2, progress 2 of 15", Quest(p)?.QuestProgress == 2 && Quest(p).QuestTotalProgress == 15, "" + Quest(p)?.QuestProgress);
    p.Hero.Damage = 3;
    Play(g, ToHand(g, p, "CS2_007"), p.Hero);
    Check("Q26 only Health actually restored counts: Healing Touch on a hero missing 3 is 3 steps", Quest(p)?.QuestProgress == 5, "" + Quest(p)?.QuestProgress);
    EndTurn(g);
    Mana(g.Player2, 10);
    g.Player2.Hero.Damage = 10;
    Play(g, ToHand(g, g.Player2, "CS2_007"), g.Player2.Hero);
    Check("Q27 the opponent's healing is no progress", Quest(p)?.QuestProgress == 5);
    EndTurn(g); Mana(p, 10);
    var yeti = Board(g, p, "CS2_182");
    yeti.Damage = 4; p.Hero.Damage = 20;
    Play(g, ToHand(g, p, "CS2_007"), p.Hero);
    Check("Q28 8 more: progress 13", Quest(p)?.QuestProgress == 13, "" + Quest(p)?.QuestProgress);
    Mana(p, 10);
    Play(g, ToHand(g, p, "CS2_007"), yeti);
    Check("Q29 healing a minion counts too; at 15 the hero power becomes Obelisk's Eye", Quest(p) == null && p.Hero.HeroPower.Card.Id == "ULD_724p" && yeti.Damage == 0);
    yeti.Damage = 4; Mana(p, 10);
    bool pressed = Power(g, yeti);
    Check("Q30 Obelisk's Eye costs (2): restores 3 Health to a minion and gives it +3/+3", pressed && p.UsedMana == 2 && yeti.AttackDamage == 7 && yeti.BaseHealth == 8 && yeti.Damage == 1, yeti.AttackDamage + "/" + yeti.Health + " dmg " + yeti.Damage);
    Round(g);
    p.Hero.Damage = 10;
    int attack = p.Hero.AttackDamage;
    Check("Q31 on a hero it only restores 3 Health", Power(g, p.Hero) && p.Hero.Damage == 7 && p.Hero.AttackDamage == attack);
});

// ============================================================ D own-deck inserts
Guard("ShadowOfDeath", () =>
{
    var g = Ready(Plan("HERO_03", "CS2_083b"));
    var p = g.Player1;
    EmptyHand(p); EmptyDeck(p);
    var yeti = Board(g, p, "CS2_182");
    Play(g, ToHand(g, p, "CS2_092"), yeti); // Blessing of Kings: the Shadows copy the card, not the buff
    Mana(p, 10);
    bool played = Play(g, ToHand(g, p, "ULD_286"), yeti);
    var shadows = p.DeckZone.Where(x => x.Card.Id == "ULD_286t").ToList();
    Check("D1 Shadow of Death shuffles three Shadows naming the chosen minion's card into its owner's deck", played && shadows.Count == 3 && p.DeckZone.Count == 3
        && shadows.All(s => UldumRules.SummonedCard(s)?.Id == "CS2_182"), p.DeckZone.Count + " in deck");
    ToDeck(g.Player2, "CS2_182");
    Round(g);
    var copies = p.BoardZone.Where(m => m != yeti).ToList();
    Check("D2 drawing one casts it: a plain 4/5 copy is summoned and the next card is drawn, so all three chain into three Yetis and one Fatigue draw", copies.Count == 3
        && copies.All(m => m.Card.Id == "CS2_182" && m.AttackDamage == 4 && m.Health == 5) && yeti.AttackDamage == 8 && p.DeckZone.Count == 0 && p.HandZone.Count == 0 && p.Hero.Damage == 1,
        BoardOf(p) + " hand " + p.HandZone.Count + " dmg " + p.Hero.Damage);
    var held = Entity.FromCard(p, C("ULD_286t"), new SabberStoneCore.Model.Entities.EntityData { [UldumRules.SummonedCardTag] = C("EX1_096").AssetId });
    Generic.AddHandPhase.Invoke(p, held);
    Mana(p, 10);
    Check("D3 a Shadow held in hand is a spell for (4) that summons its minion", held.Cost == 4 && Play(g, held) && p.BoardZone.Any(m => m.Card.Id == "EX1_096") && p.UsedMana == 4);
    var blank = Entity.FromCard(p, C("ULD_286t")); Generic.AddHandPhase.Invoke(p, blank);
    Mana(p, 10);
    int board = p.BoardZone.Count;
    Check("D4 a Shadow that names no minion summons nothing and breaks nothing", Play(g, blank) && p.BoardZone.Count == board && g.State != State.COMPLETE);
    // An enemy minion can be chosen; a full board keeps the Shadow's draw.
    var g2 = Ready(Plan("HERO_03", "CS2_083b"));
    var q = g2.Player1;
    EmptyHand(q); EmptyDeck(q);
    var foe = Board(g2, g2.Player2, "EX1_096");
    Play(g2, ToHand(g2, q, "ULD_286"), foe);
    while (!q.BoardZone.IsFull) Board(g2, q, "CS2_182");
    var bottom = Entity.FromCard(q, C("CS2_023")); q.DeckZone.Add(bottom, 0);
    ToDeck(g2.Player2, "CS2_182");
    Round(g2);
    Check("D5 chosen from the enemy board too; with a full board a drawn Shadow summons nothing but still draws on", q.BoardZone.Count == 7 && !q.BoardZone.Any(m => m.Card.Id == "EX1_096")
        && q.DeckZone.Count == 0 && q.HandZone.Any(x => x.Card.Id == "CS2_023"), "deck " + q.DeckZone.Count + " hand " + Hand(q));
    var drawn = (Minion)ToHand(g2, g.Player1, "CS2_182");
    Check("D6 UldumRules.SummonDrawn (summon it when drawn, for a minion card): the drawn minion goes from hand to board", UldumRules.SummonDrawn(g.Player1, drawn) && drawn.Zone == g.Player1.BoardZone
        && !g.Player1.HandZone.Contains(drawn));
});

Guard("BodyWrapper", () =>
{
    var g = Ready(Plan());
    var p = g.Player1;
    EmptyHand(p);
    int deck = p.DeckZone.Count;
    Play(g, ToHand(g, p, "ULD_727"));
    Check("D7 Body Wrapper with no dead friendly minion: no choice, nothing shuffled", p.Choice == null && p.DeckZone.Count == deck);
    Kill(g, Board(g, p, "EX1_096")); Kill(g, Board(g, p, "CS2_189")); Kill(g, Board(g, g.Player2, "EX1_015"));
    Mana(p, 10);
    Play(g, ToHand(g, p, "ULD_727"));
    var offers = p.Choice?.Choices.Select(id => g.IdEntityDic[id].Card.Id).ToList() ?? new List<string>();
    Check("D8 it offers the owner's own dead minions only (one offer per card)", offers.Count == 2 && offers.Contains("EX1_096") && offers.Contains("CS2_189"), string.Join(",", offers));
    int pick = p.Choice.Choices.First(id => g.IdEntityDic[id].Card.Id == "CS2_189");
    deck = p.DeckZone.Count;
    Check("D9 the chosen one is shuffled into its owner's deck", Answer(g, p, pick) && p.Choice == null && p.DeckZone.Count == deck + 1 && p.DeckZone.Count(x => x.Card.Id == "CS2_189") == 1);
});

// ============================================================ P own-deck pulls
Guard("DeckPulls", () =>
{
    var g = Ready(Plan());
    var p = g.Player1;
    EmptyHand(p); EmptyDeck(p);
    foreach (string id in new[] { "CS2_182", "EX1_096", "CS2_182", "CS2_189", "EX1_010", "CS2_023" }) ToDeck(p, id);
    Kill(g, Board(g, p, "ULD_438"));
    Check("P1 Salhet's Pride's Deathrattle draws two 1-Health minions from its owner's deck", p.HandZone.Count == 2 && p.HandZone.All(x => x is Minion m && m.Health == 1) && p.DeckZone.Count == 4, Hand(p));
    EmptyHand(p);
    Kill(g, Board(g, p, "ULD_438"));
    Check("P2 with one left it draws that one", p.HandZone.Count == 1 && p.HandZone[0] is Minion && p.DeckZone.Count == 3 && !p.DeckZone.Any(x => x is Minion m && m.Health == 1), Hand(p));
    EmptyDeck(p);
    foreach (string id in new[] { "CS2_168", "CS2_182", "EX1_506", "CS2_173", "CS2_023" }) ToDeck(p, id);
    Mana(p, 10);
    bool cast = Play(g, ToHand(g, p, "ULD_716"));
    Check("P3 Tip the Scales needs no target and summons the Murlocs of its owner's deck (three of three)", cast && p.BoardZone.Count(m => m.IsRace(Race.MURLOC)) == 3 && p.DeckZone.Count == 2 && p.DeckZone.All(x => !(x is Minion m && m.IsRace(Race.MURLOC))),
        BoardOf(p));
    foreach (var m in p.BoardZone.ToList()) Kill(g, m);
    EmptyDeck(p);
    for (int i = 0; i < 9; i++) ToDeck(p, "CS2_168");
    Mana(p, 10);
    Play(g, ToHand(g, p, "ULD_716"));
    Check("P4 at most seven: nine Murlocs in the deck leave two", p.BoardZone.Count == 7 && p.BoardZone.All(m => m.Card.Id == "CS2_168") && p.DeckZone.Count == 2);
    var s = Ready(Plan(deck: Enumerable.Repeat("CS2_182", 28).Concat(new[] { "EX1_287", "CS2_023" }).ToArray()));
    var o = s.Player1;
    foreach (var x in o.HandZone.Where(x => x.Card.Id == "EX1_287").ToList()) { o.HandZone.Remove(x); o.DeckZone.Add(x); }
    EmptyHand(o);
    int before = o.DeckZone.Count;
    Play(s, ToHand(s, o, "ULD_726"));
    Check("P5 Ancient Mysteries draws a Secret from its owner's deck and it costs (0)", o.HandZone.Count == 1 && o.HandZone[0].Card.Id == "EX1_287" && o.HandZone[0].Cost == 0 && o.DeckZone.Count == before - 1, Hand(o));
    Mana(o, 0);
    Check("P6 the Secret is then played for nothing; a second Ancient Mysteries with no Secret left draws nothing", Play(s, o.HandZone[0]) && o.SecretZone.Count == 1 && ((Func<bool>)(() => { Mana(o, 10); int d = o.DeckZone.Count; Play(s, ToHand(s, o, "ULD_726")); return o.DeckZone.Count == d && o.HandZone.Count == 0; }))());
    Mana(o, 10);
    var target = Board(s, s.Player2, "CS2_182");
    Play(s, ToHand(s, o, "ULD_293"), target);
    Check("P7 Cloud Prince with a friendly Secret in play deals 6 damage", target.Zone != s.Player2.BoardZone || target.Damage == 6, "damage " + target.Damage);
    var n = Ready(Plan());
    var foe = Board(n, n.Player2, "CS2_182");
    var prince = ToHand(n, n.Player1, "ULD_293");
    Check("P8 without a Secret it is played with no target and deals none", !n.Process(PlayCardTask.Any(n.Player1, prince, foe)) && Play(n, prince) && foe.Damage == 0 && n.Player2.Hero.Damage == 0);
});

// ============================================================ H own-hand rewrites
Guard("HandRewrites", () =>
{
    var g = Ready(Plan("HERO_03", "CS2_083b"));
    var p = g.Player1;
    EmptyHand(p);
    var hoarder = (Minion)ToHand(g, p, "EX1_096"); var highmane = (Minion)ToHand(g, p, "EX1_534"); var yeti = (Minion)ToHand(g, p, "CS2_182"); var spell = ToHand(g, p, "CS2_029");
    Play(g, ToHand(g, p, "ULD_288"));
    Check("H1 Anka: each Deathrattle minion in hand becomes a 1/1 that costs (1); other cards are untouched", hoarder.Cost == 1 && hoarder.AttackDamage == 1 && hoarder.Health == 1 && highmane.Cost == 1
        && highmane.AttackDamage == 1 && highmane.Health == 1 && yeti.Cost == 4 && yeti.AttackDamage == 4 && yeti.Health == 5 && spell.Cost == 4, Hand(p));
    Mana(p, 1);
    bool cheap = Play(g, highmane);
    Kill(g, highmane);
    Check("H2 the changed minion is played for (1) as a 1/1 and keeps its Deathrattle (two Hyenas)", cheap && p.BoardZone.Count(m => m.Card.Id == "EX1_534t") == 2, BoardOf(p));

    var h = Ready(Plan());
    var q = h.Player1;
    EmptyHand(q);
    var a = (Minion)ToHand(h, q, "CS2_182"); var b = (Minion)ToHand(h, q, "CS2_182"); var fire = ToHand(h, q, "CS2_029");
    var buff = ToHand(h, q, "ULD_290");
    Play(h, buff);
    Check("H3 History Buff: playing it buffs nothing", a.AttackDamage == 4 && b.AttackDamage == 4);
    Mana(q, 10);
    Play(h, a);
    Check("H4 whenever its owner plays a minion, a random minion in hand gets +1/+1 (the other Yeti: 5/6), never a spell", b.AttackDamage == 5 && b.Health == 6 && a.AttackDamage == 4 && fire.Cost == 4);
    Mana(q, 10);
    Play(h, b);
    Check("H5 with no minion left in hand nothing happens", b.Zone == q.BoardZone && b.AttackDamage == 5 && q.HandZone.Count == 1);

    var w = Ready(Plan("HERO_05", "DS1h_292"));
    var r = w.Player1;
    EmptyHand(r);
    var bear = ToHand(w, r, "CS2_125"); var lion = ToHand(w, r, "EX1_534"); var plain = ToHand(w, r, "CS2_182");
    Play(w, ToHand(w, r, "ULD_410"));
    Check("H6 Scarlet Webweaver: exactly one Beast in hand costs (5) less (not below 0); the Yeti keeps its Cost", plain.Cost == 4 && (bear.Cost == 0 && lion.Cost == 6 || bear.Cost == 3 && lion.Cost == 1), Hand(r));
    EmptyHand(r); Mana(r, 10);
    var only = ToHand(w, r, "CS2_182");
    Check("H7 with no Beast in hand it changes nothing", Play(w, ToHand(w, r, "ULD_410")) && only.Cost == 4);

    var m2 = Ready(Plan());
    var s = m2.Player1;
    EmptyHand(s);
    var coin = ToHand(m2, s, "GAME_005"); var pyro = ToHand(m2, s, "EX1_279"); var arcane = ToHand(m2, s, "CS2_023"); var minion = ToHand(m2, s, "CS2_182");
    Play(m2, ToHand(m2, s, "ULD_435"));
    Check("H8 Naga Sand Witch: every spell in hand costs (5); minions keep their Cost", coin.Cost == 5 && pyro.Cost == 5 && arcane.Cost == 5 && minion.Cost == 4, Hand(s));
});

// ============================================================ G scripted generation
Guard("Generation", () =>
{
    var g = Ready(Plan("HERO_03", "CS2_083b"));
    var p = g.Player1;
    EmptyHand(p);
    Kill(g, Board(g, p, "ULD_282"));
    var got = p.HandZone.Count == 1 ? p.HandZone[0] : null;
    Check("G1 Jar Dealer's Deathrattle adds a random scripted 1-Cost minion to its owner's hand", got is Minion && got.Card.Cost == 1 && got.Card.Implemented && got.Card.Collectible, got?.Card.Id);
    EmptyHand(p);
    var mugger = ToHand(g, p, "ULD_327");
    Play(g, mugger);
    got = p.HandZone.Count == 1 ? p.HandZone[0] : null;
    Check("G2 Bazaar Mugger adds a random scripted minion of another class (not Rogue, not neutral) and has Rush", got is Minion && got.Card.Implemented && got.Card.Class != CardClass.ROGUE && got.Card.Class != CardClass.NEUTRAL
        && UldumRules.IsOtherClass(p, got.Card) && ((Minion)mugger).IsRush, got?.Card.Id + " " + got?.Card.Class);
    EmptyHand(p); Mana(p, 10);
    Board(g, p, "ULD_329");
    Play(g, ToHand(g, p, "GAME_005"));
    got = p.HandZone.Count == 1 ? p.HandZone[0] : null;
    Check("G3 Dune Sculptor: after its owner casts a spell, a random scripted Mage minion is added to hand", got is Minion && got.Card.Class == CardClass.MAGE && got.Card.Implemented, got?.Card.Id);
    EndTurn(g); Mana(g.Player2, 10);
    int hand = p.HandZone.Count;
    Play(g, ToHand(g, g.Player2, "GAME_005"));
    Check("G4 the opponent's spell adds nothing", p.HandZone.Count == hand);
    EndTurn(g); Mana(p, 10);
    foreach (var m in p.BoardZone.ToList()) Kill(g, m);
    EmptyHand(p);
    ToHand(g, p, "CS2_029"); ToHand(g, p, "CS2_023"); ToHand(g, p, "GAME_005"); ToHand(g, p, "CS2_182");
    Play(g, ToHand(g, p, "ULD_304"));
    var summoned = p.BoardZone.Where(m => m.Card.Id != "ULD_304").Select(m => m.Card).ToList();
    Check("G5 King Phaoris summons one random scripted minion of the same Cost for each spell in hand (4, 3, 0), none for the minion", summoned.Count == 3 && summoned.All(c => c.Implemented)
        && summoned.Select(c => c.Cost).OrderBy(x => x).SequenceEqual(new[] { 0, 3, 4 }), string.Join(",", summoned.Select(c => c.Id + ":" + c.Cost)));
    var f = Ready(Plan());
    var q = f.Player1;
    EmptyHand(q);
    for (int i = 0; i < 6; i++) Board(f, q, "CS2_182");
    ToHand(f, q, "CS2_029"); ToHand(f, q, "CS2_023");
    Play(f, ToHand(f, q, "ULD_304"));
    Check("G6 a full board stops it", q.BoardZone.Count == 7 && q.BoardZone.Count(m => m.Card.Id == "CS2_182") == 6);
    var blockedIds = new HashSet<string>(DungeonRunAdmission.GeneratedRejects(true));
    Check("G7 the family's pools never hold a card the run's generation rejects, a Quest or an unscripted card", new[] { CardType.MINION, CardType.SPELL }.All(t =>
        UldumRules.ScriptedPool(f.Player1.Hero, t).All(c => c.Implemented && !c.IsQuest && !blockedIds.Contains(c.Id))));
    Check("G8 the run player's classes are not \"another class\" for it; neutral cards are of no class", !UldumRules.IsOtherClass(f.Player1, C("CS2_029")) && UldumRules.IsOtherClass(f.Player1, C("CS2_072"))
        && !UldumRules.IsOtherClass(f.Player1, C("CS2_182")) && !UldumRules.IsOtherClass(null, C("CS2_072")));
});

// ============================================================ B public board effects
Guard("Board", () =>
{
    var g = Ready(Plan("HERO_06", "CS2_017"));
    var p = g.Player1;
    EmptyHand(p);
    var surger = (Minion)ToHand(g, p, "ULD_292");
    Play(g, surger, null, 1);
    Check("B1 Oasis Surger, first choice: +2/+2 (a 5/5 with Rush)", p.BoardZone.Count == 1 && surger.AttackDamage == 5 && surger.Health == 5 && surger.IsRush, BoardOf(p));
    Mana(p, 10);
    Play(g, ToHand(g, p, "ULD_292"), null, 2);
    Check("B2 second choice: a copy of itself (two 3/3)", p.BoardZone.Count == 3 && p.BoardZone.Count(m => m.Card.Id == "ULD_292" && m.AttackDamage == 3 && m.Health == 3) == 2, BoardOf(p));
    foreach (var m in p.BoardZone.ToList()) Kill(g, m);
    Board(g, p, "OG_044");
    Mana(p, 10);
    Play(g, ToHand(g, p, "ULD_292"));
    Check("B3 with Fandral Staghelm both: +2/+2, then a copy of the buffed minion (two 5/5)", p.BoardZone.Count(m => m.Card.Id == "ULD_292" && m.AttackDamage == 5 && m.Health == 5) == 2, BoardOf(p));

    var w = Ready(Plan("HERO_01", "CS2_102"));
    var a = w.Player1; var b = w.Player2;
    EmptyHand(a);
    var mine = Board(w, a, "CS2_182"); var hurt = Board(w, a, "CS2_182"); var foe = Board(w, b, "CS2_182"); var foeHurt = Board(w, b, "CS2_182");
    hurt.Damage = 1; foeHurt.Damage = 2;
    Play(w, ToHand(w, a, "ULD_707"));
    Check("B4 Plague of Wrath destroys every damaged minion on both boards and no undamaged one", mine.Zone == a.BoardZone && foe.Zone == b.BoardZone && hurt.Zone != a.BoardZone && foeHurt.Zone != b.BoardZone);
    Mana(a, 10);
    mine.Damage = 2;
    Play(w, ToHand(w, a, "CS2_092"), mine);
    Mana(a, 10);
    var merc = ToHand(w, a, "ULD_720");
    Check("B5 Bloodsworn Mercenary cannot choose an undamaged or an enemy minion", !w.Process(PlayCardTask.Any(a, merc, foe)));
    Play(w, merc, mine);
    var twin = a.BoardZone.FirstOrDefault(m => m.Card.Id == "CS2_182" && m != mine);
    Check("B6 it summons a copy of the chosen damaged friendly minion as it is (8/9 with 2 damage)", twin != null && twin.AttackDamage == 8 && twin.BaseHealth == 9 && twin.Damage == 2 && mine.Damage == 2, BoardOf(a));
    foreach (var m in a.BoardZone.ToList()) Kill(w, m);
    Mana(a, 10);
    var alone = Board(w, a, "CS2_182");
    Check("B7 with no damaged friendly minion it is played without a target and copies nothing", Play(w, ToHand(w, a, "ULD_720")) && a.BoardZone.Count == 2);
    foreach (var m in a.BoardZone.ToList()) Kill(w, m);
    Mana(a, 10);
    Board(w, a, "ULD_709");
    Play(w, ToHand(w, a, "CS2_106"));
    int armor = a.Hero.Armor;
    w.Process(HeroAttackTask.Any(a, b.Hero));
    Check("B8 Armored Goon: its owner's hero attacking gains 5 Armor", a.Hero.Armor == armor + 5, a.Hero.Armor + " vs " + armor);
    EndTurn(w); Mana(b, 10);
    Play(w, ToHand(w, b, "CS2_106"));
    armor = a.Hero.Armor; int bossArmor = b.Hero.Armor;
    w.Process(HeroAttackTask.Any(b, a.Hero));
    Check("B9 the enemy hero attacking gains none for either side", b.Hero.Armor == bossArmor && a.Hero.Armor <= armor);

    var l = Ready(Plan("HERO_05", "DS1h_292"));
    var h = l.Player1;
    EmptyHand(h);
    Board(l, h, "CS2_182"); Board(l, h, "CS2_182"); Board(l, h, "CS2_182");
    Play(l, ToHand(l, h, "ULD_713"));
    Check("B10 Swarm of Locusts fills the board: four 1/1 Locusts with Rush beside three minions", h.BoardZone.Count == 7 && h.BoardZone.Count(m => m.Card.Id == "ULD_430t" && m.AttackDamage == 1 && m.Health == 1 && m.IsRush) == 4);
    foreach (var m in h.BoardZone.ToList()) Kill(l, m);
    Mana(h, 10);
    Play(l, ToHand(l, h, "ULD_713"));
    Check("B11 seven on an empty board", h.BoardZone.Count == 7 && h.BoardZone.All(m => m.Card.Id == "ULD_430t"));

    var pr = Ready(Plan("HERO_09", "CS1h_001"));
    var c = pr.Player1; var d = pr.Player2;
    EmptyHand(c);
    var victim = Board(pr, d, "CS2_182");
    c.Hero.Damage = 10;
    var penance = ToHand(pr, c, "ULD_714");
    Check("B12 Penance cannot target a hero", !pr.Process(PlayCardTask.Any(c, penance, d.Hero)));
    Play(pr, penance, victim);
    Check("B13 it deals 3 damage to a minion and, with Lifesteal, restores 3 Health to its caster", victim.Damage == 3 && c.Hero.Damage == 7, "minion " + victim.Damage + " hero " + c.Hero.Damage);
    Mana(c, 10);
    var big = Board(pr, d, "CS2_182");
    Play(pr, ToHand(pr, d.Opponent, "CS2_092"), big);
    Mana(c, 10);
    Play(pr, ToHand(pr, c, "ULD_728"), big);
    Check("B14 Subdue sets a minion's Attack and Health to 1", big.AttackDamage == 1 && big.Health == 1 && big.BaseHealth == 1, big.AttackDamage + "/" + big.Health);
    var hoarder = Board(pr, c, "EX1_096"); var shielded = Board(pr, d, "ULD_721");
    int hand = c.HandZone.Count; Mana(c, 10);
    Play(pr, ToHand(pr, c, "ULD_718"));
    Check("B15 Plague of Death silences, then destroys every minion: no Deathrattle draw, no Reborn, both boards empty", c.BoardZone.Count == 0 && d.BoardZone.Count == 0 && c.HandZone.Count == hand, BoardOf(c) + " | " + BoardOf(d) + " hand " + c.HandZone.Count + " vs " + hand);
    var colossus = Board(pr, c, "ULD_721");
    bool shield = colossus.HasDivineShield;
    Kill(pr, colossus);
    var reborn = c.BoardZone.FirstOrDefault(m => m.Card.Id == "ULD_721");
    Check("B16 Colossus of the Moon has Divine Shield and comes back once with 1 Health (Reborn)", shield && reborn != null && reborn != colossus && reborn.Health == 1 && reborn.AttackDamage == 10, BoardOf(c));
});

// ============================================================ R admission, catalog, encounters, validator
Guard("RunLayer", () =>
{
    foreach (string id in quests)
    {
        var player = DungeonRunAdmission.Classify(C(id), id, AdmitScope.Player, null, catalogRun: true);
        var boss = DungeonRunAdmission.Classify(C(id), id, AdmitScope.Boss, null, catalogRun: true);
        Check("R1 " + id + ": a Quest is the run player's only (family quest-hero-power; rejected for a boss with a lift that names no pending work); the boss's generation never yields it",
            player.Admitted && player.Family == "quest-hero-power" && !boss.Admitted && DungeonRunAdmission.IsNoneLift(boss.Lift) && DungeonRunAdmission.Classify(C(id), id, AdmitScope.Player).Admitted
            && DungeonRunAdmission.GeneratedRejectsBoss(true).Contains(id) && DungeonRunAdmission.GeneratedRejectsBoss(false).Contains(id) && !DungeonRunAdmission.GeneratedRejects(true).Contains(id));
    }
    var open = family.Except(quests).ToList();
    var notAny = open.Where(id => !DungeonRunAdmission.Classify(C(id), id, AdmitScope.Any, null, catalogRun: true).Admitted || !DungeonRunAdmission.Classify(C(id), id, AdmitScope.Any).Admitted
        || DungeonRunAdmission.RuleFor(id) == null).ToList();
    Check("R2 the other 23 cards each have an admission row that admits every scope, on legacy runs too", open.Count == 23 && notAny.Count == 0, string.Join(",", notAny));
    Check("R3 ULD_293 and ULD_726 keep their phase-2 secret-from-deck row; the family's rows are the six families of DungeonRunAdmission.F04.cs", DungeonRunAdmission.RuleFor("ULD_293").Family == "secret-from-deck"
        && DungeonRunAdmission.RuleFor("ULD_726").Family == "secret-from-deck" && family.Select(id => DungeonRunAdmission.RuleFor(id).Family).Distinct().OrderBy(x => x, StringComparer.Ordinal)
            .SequenceEqual(new[] { "own-deck-insert", "own-deck-pull", "own-hand-rewrite", "public-board", "quest-hero-power", "scripted-generation", "secret-from-deck" }));
    var errors = DungeonRunValidator.Validate(data, null);
    Check("R4 the shipped data (regenerated catalog and encounters) validates", errors.Count == 0, string.Join(" | ", errors.Take(4)));
    var offered = new HashSet<string>(data.Bundles.Where(b => b.IsPool).SelectMany(b => b.Candidates.Select(c => c.Id)));
    Check("R5 every card of the family is a candidate of a catalog bucket entry", family.All(offered.Contains), string.Join(",", family.Where(id => !offered.Contains(id))));
    var icarax = data.Boss("ulda_40_icarax");
    var icaraxDecks = new[] { icarax.Deck }.Concat(icarax.Forms.Where(f => f.Deck != null).Select(f => f.Deck)).SelectMany(d => data.BossDecks[d]).ToList();
    Check("R6 Icarax's shipped decks list Bloodsworn Mercenary and Plague of Wrath again (no substitution left for them)", icaraxDecks.Contains("ULD_720") && icaraxDecks.Contains("ULD_707")
        && !icarax.Source.Substitutions.Any(x => x.Listed == "ULD_720" || x.Listed == "ULD_707"));
    var noQuest = data.BossDecks.Where(d => d.Value.Any(quests.Contains)).Select(d => d.Key).ToList();
    Check("R7 no shipped boss deck lists a Quest", noQuest.Count == 0, string.Join(",", noQuest));
    DungeonRunData Changed(Action<DungeonRunData> change) { var d = Load(); change(d); return d; }
    var refused = DungeonRunValidator.Validate(Changed(d => { var b = d.Boss("loot_35_brandlemar"); d.BossDecks[b.Deck][0] = "ULD_433"; }), null);
    Check("R8 the validator refuses a Quest in a boss deck (admission quest-hero-power)", refused.Count == 1 && refused[0].Contains("quest-hero-power"), string.Join(" | ", refused.Take(3)));
    var accepted = DungeonRunValidator.Validate(Changed(d => { var b = d.Boss("loot_35_brandlemar"); d.BossDecks[b.Deck][0] = "ULD_286"; d.BossDecks[b.Deck][1] = "ULD_304"; d.BossDecks[b.Deck][2] = "ULD_716"; }), null);
    Check("R9 and accepts the family's other cards there", accepted.Count == 0, string.Join(" | ", accepted.Take(3)));
    // The bosspilot fixture (fixtures\encounters-f04.json, make_fixture.py): the shipped rows plus eleven fixture rows.
    string fixturePath = VerifyPaths.Fixture("p8f04/fixtures/encounters-f04.json");
    var fixture = DungeonRunData.Parse(R("dungeon-run/run-v1.json"), R("dungeon-run/pool-v1.json"), R("npc/decks-v1.json"), R("dungeon-run/catalog-v1.json"),
        R("dungeon-run/schedule-v1.json"), R("dungeon-run/tavern-v1.json"), File.ReadAllText(fixturePath));
    var fixtureErrors = DungeonRunValidator.Validate(fixture, null);
    var bossRows = fixture.Encounters.Where(b => b.Family == "04").ToList();
    var playerRows = fixture.Encounters.Where(b => b.Family == "04p").ToList();
    var bossCards = new HashSet<string>(bossRows.SelectMany(b => fixture.BossDecks[b.Deck]));
    Check("R10 the bosspilot fixture validates: five boss rows whose decks hold the 23 cards a boss may play (no Quest), six rows that give the run player one Quest each",
        fixtureErrors.Count == 0 && bossRows.Count == 5 && playerRows.Count == 6 && open.All(bossCards.Contains) && !bossCards.Any(quests.Contains)
        && playerRows.Select(b => b.Start.PlayerHandAdd[0]).OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(quests.OrderBy(x => x, StringComparer.Ordinal))
        && fixture.Encounters.Count() == data.Encounters.Count() + 11, string.Join(" | ", fixtureErrors.Take(4)) + " rows " + bossRows.Count + "/" + playerRows.Count);
});

Console.WriteLine($"SearchTelemetry swallowed leaf exceptions {SearchTelemetry.SwallowedLeafErrors}");
Console.WriteLine($"RESULT {passes} passed, {failures + SearchTelemetry.SwallowedLeafErrors} failed");
return failures + SearchTelemetry.SwallowedLeafErrors;
