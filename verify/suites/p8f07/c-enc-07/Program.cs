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

// Phase 8 family 07, Dungeon Run bosses A (real engine games through DungeonRunSetup): the eighteen clickable boss powers of
// Waxmancer Sturmi, Pathmaker Hamm, Blackseed, Frostfur, Graves the Cleric, Overseer Mogark, Candlebeard, Thaddock the Thief,
// Giant Rat, Gnosh the Greatworm, Bristlesnarl, The Mothergloop, Wee Whelp and Bink the Burglar, each asserted against its
// client text; the Rat and Growing Ooze; the run player's board shrine against the copy, transform, destroy and damage
// powers; and the shipped rows, their plans, schedule eligibility, admission and the validator.
//   A = powers on public zones, B = powers on the boss's own hand, C = boss cards, D = shrines, F = a damage loop found in
//   Hamm's deck (Axe Flinger against Emberroot Destroyer), E = rows and rules.
int failures = 0, passes = 0;
void Check(string name, bool ok, string detail = "")
{
    if (ok) passes++; else failures++;
    Console.WriteLine((ok ? "PASS " : "FAIL ") + name + (!ok && detail.Length > 0 ? "  (" + detail + ")" : ""));
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
var run = new DungeonRun(data);
var all = new DungeonRunProfile(); all.Unlocked.AddRange(data.Unlocks.Keys);

const string Yeti = "CS2_182", Ogre = "CS2_200", Wisp = "CS2_231", Raptor = "CS2_172", Shrine = "TRLA_104";
string[] filler = Enumerable.Repeat(Yeti, 30).ToArray();
RunMatchPlan Plan(string n, string shrine = null, string playerPower = "CS2_034") => new RunMatchPlan
{
    PlayerHero = C("HERO_08"), PlayerHeroPowerId = playerPower, PlayerDeck = filler.Select(C).ToList(),
    BossHero = C("LOOTA_BOSS_" + n + "h"), BossHeroPowerId = "LOOTA_BOSS_" + n + "p", BossHealth = 30, BossDeck = filler.Select(C).ToList(),
    PlayerGeneratedSecrets = true, EncounterId = "test", EncounterDeck = true, PlayerShrine = shrine,
    ExcludedGenerated = DungeonRunAdmission.GeneratedRejects(true), ExcludedGeneratedBoss = DungeonRunAdmission.GeneratedRejectsBoss(true),
};
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
void Mana(Controller c, int n) { c.BaseMana = n; c.UsedMana = 0; c.OverloadLocked = 0; c.OverloadOwed = 0; }
IPlayable ToHand(Game g, Controller c, string id)
{
    while (c.HandZone.Count >= 9) { var old = c.HandZone[0]; c.HandZone.Remove(old); c.SetasideZone.Add(old); }
    var e = Entity.FromCard(c, C(id)); Generic.AddHandPhase.Invoke(c, e); g.DeathProcessingAndAuraUpdate(); return e;
}
void EmptyHand(Controller c) { foreach (var p in c.HandZone.ToList()) { c.HandZone.Remove(p); c.SetasideZone.Add(p); } }
Minion Board(Game g, Controller c, string id) { var m = (Minion)Entity.FromCard(c, C(id)); Generic.SummonBlock(g, m, -1, c.Hero); g.DeathProcessingAndAuraUpdate(); return m; }
bool Play(Game g, IPlayable card, ICharacter target = null) => g.Process(PlayCardTask.Any(g.CurrentPlayer, card, target, -1, 0));
void EndTurn(Game g) => g.Process(EndTurnTask.Any(g.CurrentPlayer));
List<HeroPowerTask> PowerOptions(Game g) => g.CurrentPlayer.Options().OfType<HeroPowerTask>().ToList();
bool Offered(Game g) => PowerOptions(g).Count > 0;
// The characters the power may be aimed at now (empty for an untargeted power or one that is not offered).
List<ICharacter> Targets(Game g) => PowerOptions(g).Where(o => o.Target != null).Select(o => (ICharacter)o.Target).ToList();
bool Power(Game g, ICharacter target = null) =>
    PowerOptions(g).Any(o => ReferenceEquals(o.Target, target)) && g.Process(HeroPowerTask.Any(g.CurrentPlayer, target));
void ToBoss(Game g) { if (g.CurrentPlayer == g.Player1) EndTurn(g); Mana(g.Player2, 10); }
void ToPlayer(Game g) { if (g.CurrentPlayer == g.Player2) EndTurn(g); Mana(g.Player1, 10); }
// One full round back to the boss's turn (the hero power is fresh again).
void Round(Game g) { EndTurn(g); EndTurn(g); Mana(g.Player2, 10); }
bool Has(IPlayable p, string enchantment) => p.AppliedEnchantments != null && p.AppliedEnchantments.Any(e => e.Card.Id == enchantment);
int Count(IPlayable p, string enchantment) => p.AppliedEnchantments == null ? 0 : p.AppliedEnchantments.Count(e => e.Card.Id == enchantment);
HeroPower Hp(Game g) => g.Player2.Hero.HeroPower;
// A game at the boss's turn with 10 Mana, both boards as listed (player minions, boss minions).
Game At(string n, string[] player = null, string[] boss = null, int seed = 7, string shrine = null)
{
    var g = Ready(Plan(n, shrine), seed);
    foreach (string id in player ?? new string[0]) Board(g, g.Player1, id);
    ToBoss(g);
    foreach (string id in boss ?? new string[0]) Board(g, g.Player2, id);
    return g;
}
string Text(string id) => factById[id].GetProperty("text").GetString().Replace("\n", " ");

// ============================================================ A1-A5 Waxmancer Sturmi: Sculpt Wax, Harden Sculpture
Guard("Sturmi", () =>
{
    var g = At("04", new[] { Ogre }, new[] { Raptor });
    var boss = g.Player2; var ogre = g.Player1.BoardZone[0]; var raptor = boss.BoardZone[0];
    var targets = Targets(g);
    Check("A1 Sculpt Wax (1) \"Summon a 1/1 copy of a minion.\": clickable, aimed at any minion of either board, never at a hero", Text("LOOTA_BOSS_04p") == "<b>Hero Power</b> Summon a 1/1 copy of a minion."
        && Hp(g).Cost == 1 && !Hp(g).IsPassiveHeroPower && targets.Count == 2 && targets.Contains(ogre) && targets.Contains(raptor), targets.Count + " targets");
    bool used = Power(g, ogre);
    var copy = boss.BoardZone.FirstOrDefault(m => m.Card.Id == Ogre);
    Check("A2 a 1/1 copy of the run player's Boulderfist Ogre joins the boss's board (Waxed); the original is untouched; the power costs (1) and is spent", used && copy != null && copy.AttackDamage == 1
        && copy.Health == 1 && Has(copy, "LOOTA_BOSS_04e") && boss.BoardZone.Count == 2 && ogre.AttackDamage == 6 && ogre.Health == 7 && ogre.Zone == g.Player1.BoardZone
        && boss.UsedMana == 1 && !Offered(g), copy == null ? "no copy" : copy.AttackDamage + "/" + copy.Health);
    Round(g);
    while (!boss.BoardZone.IsFull) Board(g, boss, Wisp);
    Check("A3 with a full board Sculpt Wax is not offered", !Offered(g));
    var g2 = At("04");
    Check("A3b with no minion in play it is not offered", !Offered(g2));

    var h = At("27", new[] { Ogre });
    var hboss = h.Player2; var hogre = h.Player1.BoardZone[0];
    hogre.Damage = 3;
    Generic.AddEnchantmentBlock(h, C("CS2_004e"), hogre, hogre); // Power Word: Shield (+2 Health)
    h.DeathProcessingAndAuraUpdate();
    bool hardened = Power(h, hogre);
    var hcopy = hboss.BoardZone.FirstOrDefault(m => m.Card.Id == Ogre);
    Check("A4 Harden Sculpture (3) \"Summon a copy of a minion.\": an exact copy (Attack, Health, damage, enchantments) on the boss's board", Text("LOOTA_BOSS_27p") == "<b>Hero Power</b> Summon a copy of a minion."
        && Hp(h).Cost == 3 && hardened && hcopy != null && hcopy.AttackDamage == hogre.AttackDamage && hcopy.Health == hogre.Health && hcopy.Damage == 3 && Has(hcopy, "CS2_004e")
        && !Has(hcopy, "LOOTA_BOSS_04e") && hboss.UsedMana == 3 && hogre.Zone == h.Player1.BoardZone, hcopy == null ? "no copy" : hcopy.AttackDamage + "/" + hcopy.Health + " dmg " + hcopy.Damage);
    Round(h);
    while (!hboss.BoardZone.IsFull) Board(h, hboss, Wisp);
    Check("A5 with a full board Harden Sculpture is not offered", !Offered(h));
});

// ============================================================ A6-A10 Pathmaker Hamm: Unstable Explosion, Unstable Demolition
Guard("Hamm", () =>
{
    int[] Hits(Game g) => new[] { g.Player1.Hero.Damage }.Concat(g.Player1.BoardZone.Select(m => m.Damage)).ToArray();
    var g = At("05", new[] { Ogre, Ogre, Ogre }, new[] { Ogre });
    bool used = Power(g);
    var hits = Hits(g);
    Check("A6 Unstable Explosion (1) \"Deal 1 damage to two random enemies.\": untargeted; exactly two different enemies (hero or minions) take 1; the boss's own side is untouched", Text("LOOTA_BOSS_05p") == "<b>Hero Power</b> Deal 1 damage to two random enemies."
        && Hp(g).Cost == 1 && used && hits.Count(d => d == 1) == 2 && hits.Sum() == 2 && g.Player2.BoardZone[0].Damage == 0 && g.Player2.Hero.Damage == 0 && g.Player2.UsedMana == 1,
        string.Join(",", hits));
    var lone = At("05");
    Check("A7 with the enemy hero alone it deals 1 damage once", Power(lone) && lone.Player1.Hero.Damage == 1);

    var d = At("28", new[] { Ogre, Ogre, Ogre });
    bool demolished = Power(d);
    var dh = Hits(d);
    Check("A8 Unstable Demolition (2) \"Deal 2 damage to three random enemies.\": three different enemies take 2", Text("LOOTA_BOSS_28p") == "<b>Hero Power</b> Deal 2 damage to three random enemies."
        && Hp(d).Cost == 2 && demolished && dh.Count(x => x == 2) == 3 && dh.Sum() == 6, string.Join(",", dh));
    var two = At("28", new[] { Ogre });
    Check("A9 with two enemies each is hit once (never one twice)", Power(two) && two.Player1.Hero.Damage == 2 && two.Player1.BoardZone[0].Damage == 2);
    // Over seeds: every enemy slot is picked sometimes, never one twice, and the pick follows the game seed.
    var seen = new HashSet<int>(); bool distinct = true; var patterns = new HashSet<string>();
    for (int seed = 1; seed <= 24; seed++)
    {
        var s = At("05", new[] { Ogre, Ogre, Ogre }, seed: seed);
        Power(s);
        var sh = Hits(s);
        distinct &= sh.Count(x => x == 1) == 2 && sh.Sum() == 2;
        for (int i = 0; i < sh.Length; i++) if (sh[i] > 0) seen.Add(i);
        patterns.Add(string.Join("", sh));
    }
    var again = At("05", new[] { Ogre, Ogre, Ogre }, seed: 3); Power(again);
    var again2 = At("05", new[] { Ogre, Ogre, Ogre }, seed: 3); Power(again2);
    Check("A10 over 24 seeds the two picks are always different, every enemy (the hero and each minion) is picked at some seed, and one seed gives one result", distinct && seen.Count == 4
        && patterns.Count > 1 && Hits(again).SequenceEqual(Hits(again2)), seen.Count + " slots, " + patterns.Count + " patterns");
});

// ============================================================ A11-A14 Blackseed: Evolvomancy, Greater Evolution
Guard("Blackseed", () =>
{
    var g = At("06", new[] { Yeti }, new[] { Wisp });
    var boss = g.Player2; var yeti = g.Player1.BoardZone[0];
    var targets = Targets(g);
    bool used = Power(g, yeti);
    var evolved = g.Player1.BoardZone[0];
    Check("A11 Evolvomancy (0) \"Transform a minion into a random one that costs (1) more.\": the run player's 4-Cost Yeti becomes a scripted 5-Cost minion where it stood", Text("LOOTA_BOSS_06p") == "<b>Hero Power</b> Transform a minion into a random one that costs (1) more."
        && Hp(g).Cost == 0 && targets.Count == 2 && used && g.Player1.BoardZone.Count == 1 && evolved.Card.Id != Yeti && evolved.Card.Cost == 5 && evolved.Card.Implemented
        && evolved.Card.Type == CardType.MINION && boss.UsedMana == 0 && !Offered(g), evolved.Card.Id + " cost " + evolved.Card.Cost);
    Round(g);
    var wisp = boss.BoardZone.First(m => m.Card.Id == Wisp);
    bool own = Power(g, wisp);
    Check("A12 aimed at the boss's own 0-Cost Wisp it makes a 1-Cost minion on the boss's board", own && boss.BoardZone.Count == 1 && boss.BoardZone[0].Card.Cost == 1 && boss.BoardZone[0].Card.Id != Wisp);
    // Over seeds: always Cost + 1, scripted, a card the run's generation filter allows; more than one result.
    var results = new HashSet<string>(); bool ok = true;
    var blocked = new HashSet<string>(DungeonRunAdmission.GeneratedRejects(true).Concat(DungeonRunAdmission.GeneratedRejectsBoss(true)), StringComparer.Ordinal);
    for (int seed = 1; seed <= 30; seed++)
    {
        var s = At("06", new[] { Yeti }, seed: seed);
        Power(s, s.Player1.BoardZone[0]);
        var m = s.Player1.BoardZone[0];
        ok &= m.Card.Cost == 5 && m.Card.Implemented && m.Card.Collectible && !blocked.Contains(m.Card.Id) && m.Card.Id != Yeti;
        results.Add(m.Card.Id);
    }
    var pool = DungeonRunEncounters.EvolvePool(g, Hp(g), g.Player1.BoardZone[0], 5);
    Check("A13 over 30 seeds the result is always a scripted collectible 5-Cost minion outside the run's generation rejects, and it varies; the pool holds no rejected card", ok && results.Count > 5
        && pool.Count > 20 && pool.All(c => c.Cost == 5 && c.Implemented && !blocked.Contains(c.Id)) && results.All(id => pool.Any(c => c.Id == id)), results.Count + " results, pool " + pool.Count);
    // No scripted minion of the Cost: the minion stays (Evolve's rule). A 25-Cost minion: nothing costs 26.
    var big = At("06");
    var giant = Board(big, big.Player1, "EX1_620"); // Molten Giant
    int top = giant.Card.Cost;
    bool none = DungeonRunEncounters.EvolvePool(big, Hp(big), giant, top + 1).Count == 0;
    bool tried = Power(big, giant);
    Check("A13b with no scripted minion that costs (1) more the minion stays as it is", none && tried && big.Player1.BoardZone[0] == giant && giant.Card.Id == "EX1_620", "cost " + top + " pool empty " + none);

    var e = At("29", new[] { Yeti });
    bool greater = Power(e, e.Player1.BoardZone[0]);
    var seven = e.Player1.BoardZone[0];
    Check("A14 Greater Evolution (1) \"Transform a minion into a random one that costs (3) more.\": the Yeti becomes a scripted 7-Cost minion", Text("LOOTA_BOSS_29p") == "<b>Hero Power</b> Transform a minion into a random one that costs (3) more."
        && Hp(e).Cost == 1 && greater && seven.Card.Cost == 7 && seven.Card.Implemented && e.Player2.UsedMana == 1, seven.Card.Id + " cost " + seven.Card.Cost);
});

// ============================================================ A15 Frostfur: Freeze
Guard("Frostfur", () =>
{
    var g = At("09", new[] { Yeti }, new[] { Raptor });
    var yeti = g.Player1.BoardZone[0];
    var targets = Targets(g);
    bool used = Power(g, yeti);
    bool frozen = yeti.IsFrozen;
    ToPlayer(g);
    bool cannot = !yeti.CanAttack && !g.Player1.Options().Any(o => o is MinionAttackTask a && a.Source == yeti);
    Check("A15 Freeze (2) \"Freeze a minion.\": aimed at any minion; the run player's Yeti is Frozen and cannot attack on its owner's next turn", Text("LOOTA_BOSS_09p") == "<b>Hero Power</b> <b>Freeze</b> a minion."
        && Hp(g).Cost == 2 && targets.Count == 2 && targets.All(t => t is Minion) && used && frozen && cannot, "frozen " + frozen + " cannot " + cannot);
    ToBoss(g); ToPlayer(g);
    Check("A15b the Freeze wears off: the Yeti can attack on the turn after", !yeti.IsFrozen && yeti.CanAttack);
    var none = At("09");
    Check("A15c with no minion in play Freeze is not offered", !Offered(none));
});

// ============================================================ A16 Graves the Cleric: Light's Will
Guard("Graves", () =>
{
    var g = At("10", new[] { Ogre, Yeti }, new[] { Ogre });
    var p1 = g.Player1.BoardZone[0]; var p2 = g.Player1.BoardZone[1]; var b1 = g.Player2.BoardZone[0];
    p1.Damage = 5; p2.Damage = 1; b1.Damage = 3; g.Player1.Hero.Damage = 6; g.Player2.Hero.Damage = 6;
    bool used = Power(g);
    Check("A16 Light's Will (0) \"Restore #2 Health to all minions.\": every damaged minion of both boards is healed 2 (never above full); the heroes are not healed", Text("LOOTA_BOSS_10p") == "<b>Hero Power</b> Restore #2 Health to all minions."
        && Hp(g).Cost == 0 && used && p1.Damage == 3 && p2.Damage == 0 && b1.Damage == 1 && g.Player1.Hero.Damage == 6 && g.Player2.Hero.Damage == 6 && g.Player2.UsedMana == 0 && !Offered(g),
        p1.Damage + "," + p2.Damage + "," + b1.Damage);
    // A healing trigger sees it: Lightwarden (whenever a character is healed, +2 Attack) on the boss's board.
    Round(g);
    var warden = Board(g, g.Player2, "EX1_001");
    int before = warden.AttackDamage;
    Power(g);
    Check("A16b the heal is a real heal: Lightwarden gains Attack for each minion healed", warden.AttackDamage == before + 2 * 2, warden.AttackDamage + " from " + before);
});

// ============================================================ A17-A20 Overseer Mogark: Cruel Words, Searing Lash
Guard("Mogark", () =>
{
    var g = At("11", new[] { Yeti }, new[] { Ogre, Wisp });
    var boss = g.Player2; var ogre = boss.BoardZone[0]; var wisp = boss.BoardZone[1];
    var targets = Targets(g);
    bool used = Power(g, ogre);
    Check("A17 Cruel Words (1) \"Deal 1 damage to a friendly minion and give it +2 Attack.\": aimed at the boss's own minions only", Text("LOOTA_BOSS_11p") == "<b>Hero Power</b> Deal 1 damage to a friendly minion and give it +2 Attack."
        && Hp(g).Cost == 1 && targets.Count == 2 && targets.Contains(ogre) && targets.Contains(wisp) && !Power(g, g.Player1.BoardZone[0]), targets.Count + " targets");
    Check("A18 the Ogre takes 1 damage and has +2 Attack (Whipped Into Shape)", used && ogre.Damage == 1 && ogre.AttackDamage == 8 && Has(ogre, "LOOTA_BOSS_11e") && boss.UsedMana == 1);
    Round(g);
    Power(g, wisp);
    Check("A19 a 1-Health minion dies of it", wisp.Zone != boss.BoardZone && boss.BoardZone.Count == 1);
    var none = At("11", new[] { Yeti });
    Check("A19b with no minion of its own the power is not offered", !Offered(none));

    var s = At("30", new[] { Yeti }, new[] { Ogre });
    var sogre = s.Player2.BoardZone[0];
    bool lashed = Power(s, sogre);
    Check("A20 Searing Lash (1) \"Deal 1 damage to a friendly minion and give it +5 Attack.\": 1 damage and +5 Attack (Whipped into Frenzy), the boss's own minions only", Text("LOOTA_BOSS_30p") == "<b>Hero Power</b> Deal 1 damage to a friendly minion and give it +5 Attack."
        && Hp(s).Cost == 1 && lashed && sogre.Damage == 1 && sogre.AttackDamage == 11 && Has(sogre, "LOOTA_BOSS_30e") && !Has(sogre, "LOOTA_BOSS_11e"), sogre.AttackDamage + " attack");
});

// ============================================================ A21 Candlebeard: Charge!
Guard("Candlebeard", () =>
{
    var g = At("12", new[] { Yeti });
    var boss = g.Player2;
    var card = ToHand(g, boss, Ogre);
    Play(g, card);
    var ogre = boss.BoardZone[0];
    bool asleep = !ogre.CanAttack && ogre.IsExhausted;
    var targets = Targets(g);
    bool used = Power(g, ogre);
    bool attacks = ogre.HasCharge && ogre.CanAttack && g.Process(MinionAttackTask.Any(boss, ogre, g.Player1.Hero));
    Check("A21 Charge! (1) \"Give a minion Charge.\": aimed at any minion; the Ogre the boss just played has Charge and attacks the run player's hero at once", Text("LOOTA_BOSS_12p") == "<b>Hero Power</b> Give a minion <b>Charge</b>."
        && Hp(g).Cost == 1 && asleep && targets.Count == 2 && used && Has(ogre, "LOOTA_BOSS_12e") && attacks && g.Player1.Hero.Damage == 6, "asleep " + asleep + " attacks " + attacks + " dmg " + g.Player1.Hero.Damage);
    Round(g);
    var yeti = g.Player1.BoardZone[0];
    Check("A21b it may be aimed at an enemy minion too, which then has Charge", Power(g, yeti) && yeti.HasCharge && Has(yeti, "LOOTA_BOSS_12e"));
});

// ============================================================ B1-B3 Thaddock the Thief: Tactical Retreat
Guard("Thaddock", () =>
{
    var g = At("13", new[] { Yeti }, new[] { Ogre });
    var boss = g.Player2; var ogre = boss.BoardZone[0];
    ogre.Damage = 4;
    int hand = boss.HandZone.Count, playerHand = g.Player1.HandZone.Count;
    var targets = Targets(g);
    bool used = Power(g, ogre);
    var back = boss.HandZone.LastOrDefault();
    Check("B1 Tactical Retreat (1) \"Return a friendly minion to your hand.\": aimed at the boss's own minions only", Text("LOOTA_BOSS_13p") == "<b>Hero Power</b> Return a friendly minion to your hand."
        && Hp(g).Cost == 1 && targets.Count == 1 && targets[0] == ogre && !Power(g, g.Player1.BoardZone[0]));
    Check("B2 the Ogre is back in the boss's hand as a fresh card (undamaged); the run player's hand and board are untouched", used && boss.BoardZone.Count == 0 && boss.HandZone.Count == hand + 1
        && back != null && back.Card.Id == Ogre && (back as Minion)?.Damage == 0 && g.Player1.HandZone.Count == playerHand && g.Player1.BoardZone.Count == 1 && boss.UsedMana == 1,
        boss.HandZone.Count + " vs " + hand);
    Round(g);
    var again = Board(g, boss, Wisp);
    while (!boss.HandZone.IsFull) { var e = Entity.FromCard(boss, C(Yeti)); Generic.AddHandPhase.Invoke(boss, e); }
    int full = boss.HandZone.Count;
    Power(g, again);
    Check("B3 with a full hand the returned minion is destroyed (the engine's bounce rule)", boss.HandZone.Count == full && again.Zone != boss.BoardZone && boss.BoardZone.Count == 0);
});

// ============================================================ A22-A23 Giant Rat: Rat Race
Guard("GiantRat", () =>
{
    var g = At("18");
    var boss = g.Player2;
    bool used = Power(g);
    var rats = boss.BoardZone.Where(m => m.Card.Id == "LOOTA_BOSS_18t").ToList();
    Check("A22 Rat Race (2) \"Summon two 1/1 Rats.\": two 1/1 Rats (Beasts) on the boss's board", Text("LOOTA_BOSS_18p") == "<b>Hero Power</b> Summon two 1/1 Rats."
        && Hp(g).Cost == 2 && used && rats.Count == 2 && rats.All(r => r.AttackDamage == 1 && r.Health == 1 && r.Card.IsRace(Race.BEAST)) && boss.UsedMana == 2, rats.Count + " rats");
    Round(g);
    while (boss.BoardZone.Count < 6) Board(g, boss, Wisp);
    bool one = Power(g);
    bool six = boss.BoardZone.Count(m => m.Card.Id == "LOOTA_BOSS_18t") == 3 && boss.BoardZone.IsFull;
    Round(g);
    Check("A23 with one free slot it summons one Rat; with a full board it is not offered", one && six && !Offered(g));
});

// ============================================================ A24-A26 Gnosh the Greatworm: Swallow Whole
Guard("Gnosh", () =>
{
    var g = At("21", new[] { Yeti }, new[] { Ogre, Raptor });
    var boss = g.Player2; var ogre = boss.BoardZone[0];
    bool used = Power(g);
    Check("A24 Swallow Whole (2) \"Destroy the minion with the highest Attack.\": untargeted; of both boards the boss's own 6-Attack Ogre dies, the others stay", Text("LOOTA_BOSS_21p") == "<b>Hero Power</b> Destroy the minion with the highest Attack."
        && Hp(g).Cost == 2 && used && ogre.Zone != boss.BoardZone && boss.BoardZone.Count == 1 && g.Player1.BoardZone.Count == 1 && boss.UsedMana == 2);
    Round(g);
    Power(g);
    Check("A24b used again the run player's 4-Attack Yeti is the highest and dies", g.Player1.BoardZone.Count == 0 && boss.BoardZone.Count == 1);
    // A Deathrattle fires: the destroy is a real death.
    var dr = At("21", new[] { "EX1_096" }); // Loot Hoarder
    int hand = dr.Player1.HandZone.Count;
    Power(dr);
    Check("A24c the minion dies a real death (Loot Hoarder's Deathrattle draws)", dr.Player1.BoardZone.Count == 0 && dr.Player1.HandZone.Count == hand + 1);
    // Equal highest Attack: exactly one dies, and over seeds each is picked.
    var picked = new HashSet<string>(); bool single = true;
    for (int seed = 1; seed <= 20; seed++)
    {
        var s = At("21", new[] { Yeti, Wisp }, new[] { "CS2_119" }, seed: seed); // the run player's Yeti (4 Attack) and the boss's Oasis Snapjaw, set to 4 Attack
        s.Player2.BoardZone[0].AttackDamage = 4;
        s.DeathProcessingAndAuraUpdate();
        Power(s);
        bool yetiDead = !s.Player1.BoardZone.Any(m => m.Card.Id == Yeti), snapDead = s.Player2.BoardZone.Count == 0;
        single &= yetiDead != snapDead && s.Player1.BoardZone.Any(m => m.Card.Id == Wisp);
        picked.Add(yetiDead ? "player" : "boss");
    }
    Check("A25 among minions with the same highest Attack exactly one is destroyed, and over 20 seeds either is picked", single && picked.Count == 2, string.Join(",", picked));
    var none = At("21");
    Check("A26 with no minion in play Swallow Whole is not offered", !Offered(none) && DungeonRunEncounters.HighestAttack(none.Player2).Count == 0);
});

// ============================================================ B4-B6 Bristlesnarl: Hunter's Call
Guard("Bristlesnarl", () =>
{
    var g = At("23");
    var boss = g.Player2;
    EmptyHand(boss);
    var ogre = ToHand(g, boss, Ogre); var wisp = ToHand(g, boss, Wisp); var spell = ToHand(g, boss, "CS2_029"); // Fireball (4)
    var playerCosts = g.Player1.HandZone.Select(p => p.Cost).ToList();
    bool used = Power(g);
    Check("B4 Hunter's Call (3) \"Reduce the Cost of cards in your hand by (1).\": every card the boss holds costs (1) less (never below 0), shown as Imperial Favor; the run player's hand is unchanged", Text("LOOTA_BOSS_23p") == "<b>Hero Power</b> Reduce the Cost of cards in your hand by (1)."
        && Hp(g).Cost == 3 && used && ogre.Cost == 5 && wisp.Cost == 0 && spell.Cost == 3 && Has(ogre, "BRM_028e") && Has(spell, "BRM_028e")
        && g.Player1.HandZone.Select(p => p.Cost).SequenceEqual(playerCosts) && boss.UsedMana == 3, ogre.Cost + "," + wisp.Cost + "," + spell.Cost);
    Round(g);
    foreach (var drawn in boss.HandZone.Where(p => p != ogre && p != wisp && p != spell).ToList()) { boss.HandZone.Remove(drawn); boss.SetasideZone.Add(drawn); }
    Power(g);
    Check("B5 used again it stacks: the Ogre costs (4), the Fireball (2)", ogre.Cost == 4 && spell.Cost == 2 && Count(ogre, "BRM_028e") == 2);
    Play(g, ogre);
    Check("B5b the discounted card is paid at its reduced Cost and the minion on the board is a plain Ogre", boss.UsedMana == 3 + 4 && boss.BoardZone[0].AttackDamage == 6 && boss.BoardZone[0].Health == 7);
    var empty = At("23");
    EmptyHand(empty.Player2);
    Check("B6 with an empty hand it is still offered (as the original) and does nothing but cost (3)", Offered(empty) && Power(empty) && empty.Player2.UsedMana == 3 && empty.Player2.HandZone.Count == 0);
});

// ============================================================ B7-B9 The Mothergloop: Gloop
Guard("Mothergloop", () =>
{
    var g = At("24");
    var boss = g.Player2;
    EmptyHand(boss);
    var ogre = (Minion)ToHand(g, boss, Ogre); var wisp = (Minion)ToHand(g, boss, Wisp); var spell = ToHand(g, boss, "CS2_029");
    var playerHand = g.Player1.HandZone.OfType<Minion>().Select(m => m.AttackDamage + "/" + m.Health).ToList();
    bool used = Power(g);
    Check("B7 Gloop (2) \"Give minions in your hand +1/+1.\": each minion the boss holds has +1/+1 (Glooped), a spell is untouched, the run player's hand is unchanged", Text("LOOTA_BOSS_24p") == "<b>Hero Power</b> Give minions in your hand +1/+1."
        && Hp(g).Cost == 2 && used && ogre.AttackDamage == 7 && ogre.Health == 8 && wisp.AttackDamage == 2 && wisp.Health == 2 && Has(ogre, "LOOTA_BOSS_24e") && !Has(spell, "LOOTA_BOSS_24e")
        && g.Player1.HandZone.OfType<Minion>().Select(m => m.AttackDamage + "/" + m.Health).SequenceEqual(playerHand) && boss.UsedMana == 2, ogre.AttackDamage + "/" + ogre.Health);
    Play(g, wisp);
    Check("B8 a Glooped minion keeps +1/+1 on the board", boss.BoardZone[0].Card.Id == Wisp && boss.BoardZone[0].AttackDamage == 2 && boss.BoardZone[0].Health == 2);
    Round(g);
    foreach (var drawn in boss.HandZone.Where(p => p != ogre && p != spell).ToList()) { boss.HandZone.Remove(drawn); boss.SetasideZone.Add(drawn); }
    Power(g);
    Check("B9 used again it stacks (+2/+2), and a minion on the board is not touched", ogre.AttackDamage == 8 && ogre.Health == 9 && boss.BoardZone[0].AttackDamage == 2);
});

// ============================================================ A27-A28 Wee Whelp: Baby Breath
Guard("WeeWhelp", () =>
{
    var g = At("44", new[] { Yeti }, new[] { Ogre });
    var boss = g.Player2;
    var targets = Targets(g);
    bool used = Power(g, g.Player1.Hero);
    Check("A27 Baby Breath (2) \"Deal 2 damage.\": aimed at any character (both heroes, every minion); the run player's hero takes 2", Text("LOOTA_BOSS_44p") == "<b>Hero Power</b> Deal 2 damage."
        && Hp(g).Cost == 2 && targets.Count == 4 && targets.Contains(g.Player1.Hero) && targets.Contains(boss.Hero) && used && g.Player1.Hero.Damage == 2 && boss.UsedMana == 2, targets.Count + " targets");
    Round(g);
    var yeti = g.Player1.BoardZone[0];
    Check("A28 aimed at a minion it deals 2 damage to it", Power(g, yeti) && yeti.Damage == 2);
});

// ============================================================ A29-A30 Bink the Burglar: Coin
Guard("Bink", () =>
{
    var g = Ready(Plan("45"));
    var boss = g.Player2;
    EndTurn(g);
    int crystals = boss.BaseMana, left = boss.RemainingMana;
    bool used = Power(g);
    Check("A29 Coin (0) \"Gain 1 Mana Crystal this turn only.\": on its first turn the boss has one more Mana to spend, its Mana Crystals unchanged; once a turn", Text("LOOTA_BOSS_45p") == "<b>Hero Power</b> Gain 1 Mana Crystal this turn only."
        && Hp(g).Cost == 0 && crystals == 1 && used && boss.RemainingMana == left + 1 && boss.BaseMana == crystals && boss.TemporaryMana == 1 && !Offered(g), boss.RemainingMana + " from " + left);
    var card = ToHand(g, boss, "CS2_172"); // Bloodfen Raptor (2)
    Check("A29b a 2-Cost card is playable on turn 1 with it", Play(g, card) && boss.BoardZone.Count == 1 && boss.RemainingMana == 0);
    EndTurn(g); EndTurn(g);
    Check("A30 the Mana Crystal lasts this turn only: next turn the boss has its 2 crystals and the power is offered again", boss.BaseMana == 2 && boss.RemainingMana == 2 && boss.TemporaryMana == 0 && Offered(g));
});

// ============================================================ C1-C3 the Rat and Growing Ooze
Guard("BossCards", () =>
{
    var rat = C("LOOTA_BOSS_18t");
    Check("C1 Rat (LOOTA_BOSS_18t) is a scripted 1/1 Beast token with no text", rat.Implemented && rat.Type == CardType.MINION && rat.ATK == 1 && rat.Health == 1 && rat.IsRace(Race.BEAST)
        && !factById["LOOTA_BOSS_18t"].TryGetProperty("text", out _) && DungeonRunEncounters.Find("LOOTA_BOSS_18t")?.Kind == EncounterCardKind.Token);
    var g = At("24", null, new[] { "ICCA07_004" });
    var boss = g.Player2; var ooze = boss.BoardZone[0];
    bool start = ooze.AttackDamage == 1 && ooze.Health == 1;
    EndTurn(g);
    bool still = ooze.AttackDamage == 1 && ooze.Health == 1;
    EndTurn(g);
    bool grown = ooze.AttackDamage == 2 && ooze.Health == 2 && Has(ooze, "ICCA07_004e");
    EndTurn(g); EndTurn(g);
    Check("C2 Growing Ooze (ICCA07_004) \"At the start of your turn, gain +1/+1.\": a 1/1 that grows at the start of its owner's turns only (2/2, then 3/3)", Text("ICCA07_004") == "At the start of your turn, gain +1/+1."
        && C("ICCA07_004").Implemented && start && still && grown && ooze.AttackDamage == 3 && ooze.Health == 3 && Count(ooze, "ICCA07_004e") == 2, ooze.AttackDamage + "/" + ooze.Health);
    var deck = data.BossDecks[data.Boss("loot_24_mothergloop").Deck];
    Check("C3 The Mothergloop's deck holds two Growing Oozes", deck.Count(id => id == "ICCA07_004") == 2);
    foreach (string id in new[] { "LOOTA_BOSS_04e", "LOOTA_BOSS_11e", "LOOTA_BOSS_12e", "LOOTA_BOSS_24e", "LOOTA_BOSS_30e", "ICCA07_004e" })
        Check("C4 " + id + " is a scripted enchantment of family 07", C(id).Implemented && C(id).Type == CardType.ENCHANTMENT && DungeonRunEncounters.Find(id)?.Kind == EncounterCardKind.Enchantment
            && DungeonRunEncounters.Find(id).Family == "07");
});

// ============================================================ D1-D9 the run player's board shrine
Guard("Shrines", () =>
{
    Minion ShrineOf(Game g) => ShrineRules.Of(g.Player1).FirstOrDefault();
    // Evolvomancy: an awake shrine is destroyed instead of transformed and goes dormant; the dormant form cannot be aimed at.
    var g = At("06", null, new[] { Wisp }, shrine: Shrine);
    var shrine = ShrineOf(g);
    bool awake = shrine != null && ShrineRules.IsAwake(shrine) && shrine.Card.Id == Shrine && Targets(g).Contains(shrine);
    bool used = Power(g, shrine);
    var form = ShrineOf(g);
    Check("D1 Evolvomancy aimed at the run player's awake shrine: the shrine is destroyed instead of transformed and goes dormant (no 1-Cost minion takes its place)", awake && used && form != null
        && ShrineRules.IsDormantForm(form) && form.Card.Id == ShrineRules.FormOf(Shrine) && g.Player1.BoardZone.Count == 1 && ShrineRules.TurnsLeft(form) == 3,
        form == null ? "no shrine" : form.Card.Id + " dormant " + form.IsDormant);
    Round(g);
    var targets = Targets(g);
    Check("D2 the dormant form cannot be aimed at (the boss's Wisp is the only target)", targets.Count == 1 && targets[0] is Minion only && only.Card.Id == Wisp && !Power(g, form));
    // Greater Evolution: the same rule.
    var ge = At("29", shrine: Shrine);
    Power(ge, ShrineOf(ge));
    Check("D3 Greater Evolution: the same (destroyed, dormant)", ShrineOf(ge) != null && ShrineRules.IsDormantForm(ShrineOf(ge)) && ge.Player1.BoardZone.Count == 1);
    // Sculpt Wax / Harden Sculpture: the copy is a plain minion, the shrine stays.
    // (TRLA_171 here: TRLA_104's own text sets its Attack to its owner's Armor, which would hide the 1/1.)
    const string Plain = "TRLA_171";
    var w = At("04", shrine: Plain);
    var ws = ShrineOf(w);
    bool waxed = Power(w, ws);
    var copy = w.Player2.BoardZone.FirstOrDefault();
    Check("D4 Sculpt Wax on the shrine: the boss gets a plain 1/1 copy (no shrine mark: it dies for good), the shrine stays awake", waxed && copy != null && copy.Card.Id == Plain && !ShrineRules.IsMarked(copy)
        && copy.AttackDamage == 1 && copy.Health == 1 && ShrineRules.IsAwake(ws) && ShrineRules.Of(w.Player2).Count == 0, copy == null ? "no copy" : copy.Card.Id + " " + copy.AttackDamage + "/" + copy.Health + " marked " + ShrineRules.IsMarked(copy));
    copy.Destroy(); w.DeathProcessingAndAuraUpdate();
    Check("D4b the copy dies like any minion (no dormant form on the boss's board)", w.Player2.BoardZone.Count == 0);
    var hs = At("27", shrine: Shrine);
    Power(hs, ShrineOf(hs));
    var hcopy = hs.Player2.BoardZone.FirstOrDefault();
    Check("D5 Harden Sculpture on the shrine: a plain copy for the boss, the shrine stays", hcopy != null && hcopy.Card.Id == Shrine && !ShrineRules.IsMarked(hcopy) && ShrineRules.IsAwake(ShrineOf(hs)));
    // Swallow Whole: an awake shrine with the highest Attack dies and goes dormant; a dormant form is never picked or offered for.
    var sw = At("21", shrine: Shrine);
    var sws = ShrineOf(sw);
    bool swallowed = Offered(sw) && Power(sw);
    var swf = ShrineOf(sw);
    Check("D6 Swallow Whole with the shrine as the only minion: it dies (a real death) and goes dormant", swallowed && swf != null && ShrineRules.IsDormantForm(swf));
    Round(sw);
    Check("D7 with only the dormant form in play Swallow Whole is not offered; with another minion it destroys that one and the form stays", !Offered(sw) && DungeonRunEncounters.HighestAttack(sw.Player2).Count == 0);
    var wisp = Board(sw, sw.Player2, Wisp);
    Power(sw);
    Check("D7b ... the Wisp dies, the dormant form stays", wisp.Zone != sw.Player2.BoardZone && ShrineOf(sw) != null && ShrineRules.IsDormantForm(ShrineOf(sw)));
    // Unstable Explosion never hits a dormant form; Light's Will and Freeze leave it alone.
    var ex = At("05", shrine: Shrine);
    ShrineOf(ex).Destroy(); ex.DeathProcessingAndAuraUpdate();
    var exf = ShrineOf(ex);
    Power(ex);
    Check("D8 Unstable Explosion with a dormant form on the run player's board: only the hero is hit", ShrineRules.IsDormantForm(exf) && exf.Damage == 0 && ex.Player1.Hero.Damage == 1);
    // Tactical Retreat and Cruel Words are aimed at the boss's own minions: never at the run player's shrine.
    var tr = At("13", null, new[] { Wisp }, shrine: Shrine);
    var cw = At("11", null, new[] { Wisp }, shrine: Shrine);
    Check("D9 Tactical Retreat and Cruel Words cannot be aimed at the run player's shrine", !Targets(tr).Contains(ShrineOf(tr)) && Targets(tr).Count == 1 && !Targets(cw).Contains(ShrineOf(cw)) && Targets(cw).Count == 1);
    // Baby Breath and Freeze treat an awake shrine as any minion.
    var bb = At("44", shrine: Shrine);
    var bbs = ShrineOf(bb);
    bool breathed = Power(bb, bbs);
    Check("D10 Baby Breath damages an awake shrine like any minion (2 damage, still awake)", breathed && bbs.Damage == 2 && ShrineRules.IsAwake(bbs) && ShrineOf(bb) == bbs);
    var fz = At("09", shrine: Shrine);
    Check("D11 Freeze on an awake shrine is an ordinary Freeze", Power(fz, ShrineOf(fz)) && ShrineOf(fz).IsFrozen && ShrineRules.IsAwake(ShrineOf(fz)));
});

// ============================================================ F1-F2 a damage loop the bosspilot seed sweep found in Hamm's deck
// Pathmaker Hamm's Axe Flinger ("Whenever this minion takes damage, deal 2 damage to the enemy hero") against the run player's
// Emberroot Destroyer (FIR_955: "Whenever your hero takes damage on your turn, deal 3 damage to a random enemy minion"): the
// Destroyer kept picking the mortally wounded Flinger, which kept answering, until the stack overflowed (the boss's search
// crashed the process on SEED_BASE 19000 and 29000). A mortally wounded minion is never the random pick now.
Guard("DamageLoop", () =>
{
    var g = Ready(Plan("28", playerPower: "CS2_056")); // the run player holds Life Tap
    var destroyer = Board(g, g.Player1, "FIR_955");
    EndTurn(g);
    var flinger = Board(g, g.Player2, "BRM_016");
    EndTurn(g);
    Mana(g.Player1, 10);
    bool tapped = g.Process(HeroPowerTask.Any(g.Player1));
    Check("F1 Life Tap with Emberroot Destroyer against one Axe Flinger ends: 2 (Life Tap), then 3 to the Flinger, 2 back, 3 to the Flinger (dead), 2 back, and no further pick", tapped
        && g.State != State.COMPLETE && g.Player1.Hero.Damage == 6 && flinger.Zone != g.Player2.BoardZone && g.Player2.BoardZone.Count == 0 && destroyer.Zone == g.Player1.BoardZone,
        "hero damage " + g.Player1.Hero.Damage + " boss board " + g.Player2.BoardZone.Count);
    // Two Flingers: the second takes over once the first is mortally wounded; still finite.
    var g2 = Ready(Plan("28", playerPower: "CS2_056"));
    Board(g2, g2.Player1, "FIR_955");
    EndTurn(g2);
    Board(g2, g2.Player2, "BRM_016"); Board(g2, g2.Player2, "BRM_016");
    EndTurn(g2);
    Mana(g2.Player1, 10);
    g2.Player1.Hero.Damage = 0;
    bool tapped2 = g2.Process(HeroPowerTask.Any(g2.Player1));
    Check("F2 against two Axe Flingers it ends too: each takes two hits and dies (2 + 4 x 2 damage to the run player's hero)", tapped2 && g2.Player2.BoardZone.Count == 0
        && g2.Player1.Hero.Damage == 10 && g2.State != State.COMPLETE, "hero damage " + g2.Player1.Hero.Damage + " boss board " + g2.Player2.BoardZone.Count);
});

// ============================================================ E1-E15 the rows, the run layer, admission and the validator
var rows = new (string n, string slug, string name, string tier, int health, string kind)[]
{
    ("04", "sturmi", "Waxmancer Sturmi", "2", 30, "authentic"), ("05", "hamm", "Pathmaker Hamm", "1", 20, "authentic"), ("06", "blackseed", "Blackseed", "2", 40, "substituted"),
    ("09", "frostfur", "Frostfur", "1", 20, "authentic"), ("10", "graves", "Graves the Cleric", "1", 20, "substituted"), ("11", "mogark", "Overseer Mogark", "1", 20, "authentic"),
    ("12", "candlebeard", "Candlebeard", "1", 20, "authentic"), ("13", "thaddock", "Thaddock the Thief", "2", 40, "authentic"), ("18", "giant_rat", "Giant Rat", "1", 10, "extended"),
    ("21", "gnosh", "Gnosh the Greatworm", "2", 40, "authentic"), ("23", "bristlesnarl", "Bristlesnarl", "3", 50, "authentic"), ("24", "mothergloop", "The Mothergloop", "3", 50, "authentic"),
    ("27", "sturmi", "Waxmancer Sturmi", "3", 50, "authentic"), ("28", "hamm", "Pathmaker Hamm", "3", 60, "authentic"), ("29", "blackseed", "Blackseed", "3", 60, "substituted"),
    ("30", "mogark", "Overseer Mogark", "3", 60, "authentic"), ("44", "wee_whelp", "Wee Whelp", "1", 10, "extended"), ("45", "bink", "Bink the Burglar", "1", 10, "extended"),
};
DungeonRunState AtFloor(string cls, long seed, string bossId)
{
    var c = data.Class(cls);
    var s = run.NewRun(all, c.Id, c.Decks[0], c.Powers[0], seed, schedule: "acts-v3h");
    while (s.Phase != DungeonRunPhase.PreMatch)
    {
        if (s.Phase != DungeonRunPhase.Modifier) throw new Exception("walk stopped at " + s.Phase);
        run.PickModifier(s, s.ModifierOffer.Kind == DungeonRunGlobalRule.KindAnomaly ? DungeonRunGlobalRule.None : s.ModifierOffer.Offered[0]);
    }
    s.BossSequence[s.Floor - 1] = bossId;
    return s;
}
Guard("Rows", () =>
{
    var errors = DungeonRunValidator.Validate(data, null);
    Check("E1 the shipped data validates with the 18 rows of family 07", errors.Count == 0 && data.Encounters.Count(b => b.Family == "07") == 18, string.Join(" | ", errors.Take(4)));
    foreach (var (n, slug, name, tier, health, kind) in rows)
    {
        string id = "loot_" + n + "_" + slug, hero = "LOOTA_BOSS_" + n + "h", power = "LOOTA_BOSS_" + n + "p";
        var b = data.Bosses.FirstOrDefault(x => x.Id == id);
        if (b == null) { Check("E2 " + id + " is a row", false); continue; }
        var deck = data.BossDecks[b.Deck];
        string expectTier = health <= 20 ? "1" : health <= 40 ? "2" : "3";
        Check("E2 " + id + ": " + name + ", Dungeon Run, tier " + tier + " by its printed Health " + health + ", a 30-card deck (" + kind + "), its wiki page recorded", b.Name == name && b.Provenance == "LOOT" && b.Family == "07"
            && b.HeroCard == hero && b.Power == power && b.Tier == tier && tier == expectTier && b.LevelSource == "printed-health" && b.LevelOriginal == health && FactTag(hero, 45) == health
            && C(hero).Type == CardType.HERO && C(hero).Name == name && deck.Count == 30 && b.Source.Kind == kind && b.Source.Url.StartsWith("https://hearthstone.wiki.gg/wiki/") && b.Forms.Count == 0
            && b.Variants.Count == 0 && !b.BossGeneratedSecrets && deck.All(c => C(c).Implemented), b.Tier + " " + b.Source.Kind + " " + deck.Count + " health " + FactTag(hero, 45));
        var entry = DungeonRunEncounters.Find(power);
        Check("E3 " + power + " is a scripted clickable power of family 07 with the client's text and Cost", entry != null && entry.Kind == EncounterCardKind.Power && entry.Family == "07" && C(power).Implemented
            && C(power).Type == CardType.HERO_POWER && C(power).Text == factById[power].GetProperty("text").GetString() && C(power).Cost == FactTag(power, 48)
            && !DungeonRunEncounters.PassivePowers.Contains(power) && FactTag(hero, 380) == factById[power].GetProperty("dbf").GetInt32());
        var s = AtFloor("MAGE", 7000 + int.Parse(n), id);
        var plan = run.BuildPlan(s, all);
        var cfg = new GameConfig { RandomSeed = 5, Shuffle = true, SkipMulligan = false, History = false, Logging = false, FormatType = FormatType.FT_WILD };
        DungeonRunSetup.Configure(cfg, plan);
        string pool = DungeonRunEncounterData.PoolOf(tier);
        Check("E4 " + id + ": the run's plan is the row's (hero, power, deck, no boss shrine, no forms) and its tier pool draws it", plan.EncounterId == id && plan.BossHero.Id == hero && plan.BossHeroPowerId == power
            && plan.BossDeck.Count == 30 && plan.BossDeck.Select(c => c.Id).SequenceEqual(deck) && plan.EncounterDeck && plan.BossShrine == null && (plan.BossForms?.Count ?? 0) == 0
            && !plan.BossGeneratedSecrets && data.Schedules.Member(b, pool) && b.Pools.SequenceEqual(new[] { pool }));
        var v = DungeonRunAdmission.Classify(C(power), power, AdmitScope.Boss, null, catalogRun: true);
        bool ownHand = n == "13" || n == "23" || n == "24";
        Check("E5 " + power + ": admitted for a boss by family 07's row (" + v.Family + "), on legacy data too; never the run player's, never generated", v.Admitted && v.Rule != null && v.Rule.Ids.Contains(power)
            && v.Family == (ownHand ? "boss-power-own-hand" : "boss-power-public") && DungeonRunAdmission.Classify(C(power), power, AdmitScope.Boss).Admitted
            && !DungeonRunAdmission.Classify(C(power), power, AdmitScope.Player, null, catalogRun: true).Admitted && DungeonRunAdmission.GeneratedRejects(true).Contains(power));
    }
    // The decks: what the wiki lists, with the recorded changes.
    List<string> Deck(string id) => data.BossDecks[data.Boss(id).Deck];
    DungeonRunDeckSource Src(string id) => data.Boss(id).Source;
    Check("E6 Sturmi's two ranks share the page's one list (30 cards, unchanged): Savannah Highmane x2, N'Zoth x1", Deck("loot_04_sturmi").SequenceEqual(Deck("loot_27_sturmi"))
        && Deck("loot_04_sturmi").Count(c => c == "EX1_534") == 2 && Deck("loot_04_sturmi").Count(c => c == "OG_133") == 1 && Src("loot_04_sturmi").Substitutions.Count == 0);
    Check("E7 Hamm's list keeps the wiki's counts (Mad Bomber x8, Madder Bomber x5, Frothing Berserker x4)", Deck("loot_05_hamm").Count(c => c == "EX1_082") == 8 && Deck("loot_05_hamm").Count(c => c == "GVG_090") == 5
        && Deck("loot_05_hamm").Count(c => c == "EX1_604") == 4 && Deck("loot_05_hamm").SequenceEqual(Deck("loot_28_hamm")));
    Check("E8 the three level-1 decks are the wiki's 10 cards twice plus 10 curated fillers (kind extended)", new[] { "loot_18_giant_rat", "loot_44_wee_whelp", "loot_45_bink" }.All(id =>
        Src(id).Kind == DungeonRunDeckSource.KindExtended && Src(id).Substitutions.Count == 20 && Src(id).Substitutions.All(x => x.Reason.StartsWith("fill:"))
        && Src(id).Substitutions.Count(x => x.Reason == "fill:second-copy") == 10 && Deck(id).GroupBy(c => c).All(grp => grp.Count() == 2)));
    Check("E9 Graves the Cleric: the 14 cards this client has are kept, the 16 it does not have are curated (recorded as unknown)", Src("loot_10_graves").Substitutions.Count == 16
        && Src("loot_10_graves").Substitutions.All(x => x.Reason == "unknown" && x.Used != null) && Deck("loot_10_graves").Count(c => c == "CS1_112") == 2 && Deck("loot_10_graves").Contains("EX1_350"));
    // Wave 2 integration: family 08 scripts Gluttonous Trogg, Reverberating Ooze and Shroomsayer, so the lists of Gnosh and The
    // Mothergloop are the wiki's unchanged (in the family's own lane they were replaced by Burly Rockjaw Trogg, of which Gnosh's
    // list holds two of its own, Grim Patron and Sludge Belcher; the replacements stay named in F07.json and are no longer used).
    Check("E10 the substitution for an unscripted card is recorded: Wrath (Blackseed); Gnosh keeps Gluttonous Trogg x2 and The Mothergloop Reverberating Ooze x5 and Shroomsayer x2 (family 08's cards)",
        Src("loot_06_blackseed").Substitutions.Count == 2 && Src("loot_06_blackseed").Substitutions.All(x => x.Listed == "EX1_154" && x.Reason == "unscripted")
        && Src("loot_21_gnosh").Substitutions.Count == 0 && Deck("loot_21_gnosh").Count(c => c == "LOOTA_109") == 2 && Deck("loot_21_gnosh").Count(c => c == "GVG_068") == 2
        && Src("loot_24_mothergloop").Substitutions.Count == 0 && Deck("loot_24_mothergloop").Count(c => c == "LOOTA_104") == 5 && Deck("loot_24_mothergloop").Count(c => c == "LOOTA_107") == 2 && !Deck("loot_24_mothergloop").Contains("FP1_012"),
        Src("loot_06_blackseed").Substitutions.Count + "/" + Src("loot_21_gnosh").Substitutions.Count + "/" + Src("loot_24_mothergloop").Substitutions.Count);
    // Schedule eligibility: every row is drawn somewhere on the pending schedules (through its tier's mixed pools).
    foreach (string schedule in new[] { "acts-v3h", "acts-v3t" })
    {
        var drawn = new HashSet<string>(StringComparer.Ordinal);
        // The close-out: NewRun refuses a schedule whose finale is the hero row's own character (DALA_Tekahn on acts-v3t).
        var classes = data.Classes.Where(x => !DungeonRun.FinaleExcludes(data.Schedules.Find(schedule), x)).ToList();
        for (int seed = 0; seed < 400; seed++)
        {
            var c = classes[seed % classes.Count];
            var s = run.NewRun(all, c.Id, c.Decks[0], c.Powers[0], 81000 + seed, schedule: schedule, signatureId: run.SignatureOffer(all, c, 81000 + seed).FirstOrDefault());
            foreach (string id in s.BossSequence) drawn.Add(id);
        }
        var missing = rows.Select(r => "loot_" + r.n + "_" + r.slug).Where(id => !drawn.Contains(id)).ToList();
        Check("E11 on " + schedule + " every one of the 18 rows is drawn by some run of 400 seeds", missing.Count == 0, string.Join(",", missing));
    }
    foreach (string old in new[] { "acts-v1", "acts-v2", "act1" })
    {
        bool any = false;
        for (int seed = 0; seed < 60 && !any; seed++)
        {
            var c = data.Classes[seed % data.Classes.Count];
            var s = run.NewRun(all, c.Id, c.Decks[0], c.Powers[0], 82000 + seed, schedule: old, signatureId: run.SignatureOffer(all, c, 82000 + seed).FirstOrDefault());
            any = s.BossSequence.Any(id => id.StartsWith("loot_"));
        }
        Check("E12 a run on " + old + " never draws an imported row", !any);
    }
    // The validator refuses a row whose power is not a boss's, and a row in the wrong pool.
    DungeonRunData Changed(Action<DungeonRunData> change) { var d = Load(); change(d); return d; }
    var wrongPool = DungeonRunValidator.Validate(Changed(d => d.Boss("loot_04_sturmi").Tier = "1"), null);
    Check("E13 a row whose pool is not its tier's is refused", wrongPool.Any(e => e.Contains("loot_04_sturmi") && e.Contains("pool is its tier's")), string.Join(" | ", wrongPool.Take(3)));
    var shortDeck = DungeonRunValidator.Validate(Changed(d => d.BossDecks[d.Boss("loot_44_wee_whelp").Deck].RemoveAt(0)), null);
    Check("E14 a combat-tier deck that is not 30 cards is refused", shortDeck.Any(e => e.Contains("loot_44_wee_whelp") && e.Contains("expected 30")), string.Join(" | ", shortDeck.Take(3)));
    foreach (string id in new[] { "LOOTA_BOSS_18t", "ICCA07_004" })
    {
        var v = DungeonRunAdmission.Classify(C(id), id, AdmitScope.Boss, null, catalogRun: true);
        Check("E15 " + id + ": a boss-only card (boss-token), never generated", v.Admitted && v.Family == "boss-token" && !DungeonRunAdmission.Classify(C(id), id, AdmitScope.Player, null, catalogRun: true).Admitted
            && DungeonRunAdmission.GeneratedRejects(true).Contains(id));
    }
});

Console.WriteLine($"SearchTelemetry swallowed leaf exceptions {SearchTelemetry.SwallowedLeafErrors}");
Console.WriteLine($"RESULT {passes} passed, {failures + SearchTelemetry.SwallowedLeafErrors} failed");
return failures + SearchTelemetry.SwallowedLeafErrors;
