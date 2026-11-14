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

// Phase 8 family 05: the global-rule framework and the Dalaran Heist anomalies and twists. Each rule's text is asserted on
// both sides in real engine games through DungeonRunSetup (sections A to R, one per rule), then the framework (S: hooks,
// marks, clones, texts) and the run layer (T: schedule rows, admission, incompatibility, the anomaly offer and the twist roll).
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
string fixtures = args.Length > 0 ? args[0] : VerifyPaths.Fixture("p8f05/fixtures");
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
void ClearBoards(Game g)
{
    foreach (var c in new[] { g.Player1, g.Player2 })
        foreach (var m in c.BoardZone.ToArray()) if (!m.Card.Untouchable) { c.BoardZone.Remove(m); c.SetasideZone.Add(m); }
    g.DeathProcessingAndAuraUpdate();
}
bool Both(Game g, string rule) => DungeonRunGlobalRules.IsActive(g.Player1, rule) && DungeonRunGlobalRules.IsActive(g.Player2, rule);
int Ench(IPlayable p, string id) => p.AppliedEnchantments?.Count(e => e.Card.Id == id) ?? 0;

// ============================================================ A. DALA_851 Summons
Guard("A", () =>
{
    var g = Ready(Plan("DALA_851"));
    Check("A1 the rule's player enchantment is on both players", Both(g, "DALA_851") && g.Player1.AppliedEnchantments.Count(e => e.Card.Id == "DALA_851e") == 1 && g.Player2.AppliedEnchantments.Count(e => e.Card.Id == "DALA_851e") == 1);
    Cast(g, "CS2_029", g.Player2.Hero);
    var mine = g.Player1.BoardZone.ToArray();
    Check("A2 after the run player plays Fireball (4) a 4-Cost minion is summoned for the run player only", mine.Length == 1 && mine[0].Card.Cost == 4 && mine[0].Card.Type == CardType.MINION
        && g.Player2.BoardZone.Count == 0 && g.Player2.Hero.Damage == 6, string.Join(",", mine.Select(m => m.Card.Id + ":" + m.Card.Cost)));
    Check("A3 the minion is a scripted card the run's generation filter allows", mine[0].Card.Implemented && !rejects.Contains(mine[0].Card.Id));
    Cast(g, "EX1_277");
    Check("A4 Arcane Missiles (1): a 1-Cost minion", g.Player1.BoardZone.Count == 2 && g.Player1.BoardZone[1].Card.Cost == 1);
    ToBoss(g);
    int before = g.Player2.BoardZone.Count;
    Cast(g, "CS2_023");
    Check("A5 the boss plays Arcane Intellect (3): a 3-Cost minion for the boss, none for the run player", g.Player2.BoardZone.Count == before + 1 && g.Player2.BoardZone.Last().Card.Cost == 3
        && g.Player1.BoardZone.Count == 2 && !rejectsBoss.Contains(g.Player2.BoardZone.Last().Card.Id));
    ClearBoards(g);
    for (int i = 0; i < 7; i++) Board(g, g.Player2, "CS2_182");
    Cast(g, "CS2_023");
    Check("A6 a full board: nothing is summoned and the spell still resolves", g.Player2.BoardZone.Count == 7 && g.Player2.BoardZone.All(m => m.Card.Id == "CS2_182"));
    // The Cost is what the spell cost in hand when it was played.
    var g2 = Ready(Plan("DALA_851"));
    Mana(g2.Player1);
    var fireball = ToHand(g2, g2.Player1, "CS2_029");
    Generic.AddEnchantmentBlock(g2, C("BRM_028e"), g2.Player1.Hero, fireball);
    g2.DeathProcessingAndAuraUpdate();
    Play(g2, fireball, g2.Player2.Hero);
    Check("A7 a Fireball that cost (3) in hand summons a 3-Cost minion", g2.Player1.BoardZone.Count == 1 && g2.Player1.BoardZone[0].Card.Cost == 3 && fireball[GameTag.TAG_LAST_KNOWN_COST_IN_HAND] == 3,
        g2.Player1.BoardZone.Count == 0 ? "none" : g2.Player1.BoardZone[0].Card.Id + ":" + g2.Player1.BoardZone[0].Card.Cost);
    var seen = new HashSet<string>();
    for (int seed = 1; seed <= 12; seed++)
    {
        var gs = Ready(Plan("DALA_851"), seed, history: false);
        Cast(gs, "CS2_029", gs.Player2.Hero);
        if (gs.Player1.BoardZone.Count == 1 && gs.Player1.BoardZone[0].Card.Cost == 4) seen.Add(gs.Player1.BoardZone[0].Card.Id);
    }
    Check("A8 the minion is random (twelve seeds give several different 4-Cost minions)", seen.Count >= 4, seen.Count + " distinct");
});

// ============================================================ B. DALA_852 Crying
Guard("B", () =>
{
    var g = Ready(Plan("DALA_852"));
    Check("B1 both players' Battlecries trigger twice (the controller aura is on each)", Both(g, "DALA_852") && g.Player1.ExtraBattlecry && g.Player2.ExtraBattlecry);
    Mana(g.Player1);
    Play(g, ToHand(g, g.Player1, "CS2_189"), g.Player2.Hero);
    Check("B2 the run player's Elven Archer deals its 1 damage twice", g.Player2.Hero.Damage == 2);
    ToBoss(g); Mana(g.Player2);
    Play(g, ToHand(g, g.Player2, "CS2_189"), g.Player1.Hero);
    Check("B3 the boss's Elven Archer deals its 1 damage twice", g.Player1.Hero.Damage == 2);
    var plain = Ready(Plan());
    Mana(plain.Player1);
    Play(plain, ToHand(plain, plain.Player1, "CS2_189"), plain.Player2.Hero);
    Check("B4 without the rule: once", plain.Player2.Hero.Damage == 1 && !plain.Player1.ExtraBattlecry);
    var clone = g.Clone();
    Mana(clone.Player2);
    clone.Process(PlayCardTask.Any(clone.CurrentPlayer, ToHand(clone, clone.Player2, "CS2_189"), clone.Player1.Hero, -1, 0));
    Check("B5 a clone plays under the same rule", clone.Player1.Hero.Damage == 4);
    Check("B6 the shared helper: one aura for both players (a boss power's \"All Battlecries trigger twice\")",
        EncounterRules.BattlecriesTwice(true).Type == SabberStoneCore.Auras.AuraType.CONTROLLERS && EncounterRules.BattlecriesTwice().Type == SabberStoneCore.Auras.AuraType.CONTROLLER
        && EncounterRules.DeathrattlesTwice(true).Type == SabberStoneCore.Auras.AuraType.CONTROLLERS);
});

// ============================================================ C. DALA_853 Rattling
Guard("C", () =>
{
    var g = Ready(Plan("DALA_853"));
    Check("C1 the rule is on both players", Both(g, "DALA_853") && DeathrattleRules.Repeats(g.Player1, null) == 2 && DeathrattleRules.Repeats(g.Player2, null) == 2);
    var leper = Board(g, g.Player1, "EX1_029");
    Kill(g, leper);
    Check("C2 the run player's Leper Gnome deals its 2 damage to the boss twice", g.Player2.Hero.Damage == 4);
    var theirs = Board(g, g.Player2, "EX1_029");
    Kill(g, theirs);
    Check("C3 the boss's Leper Gnome deals its 2 damage to the run player twice", g.Player1.Hero.Damage == 4);
    int hand = g.Player1.HandZone.Count;
    Kill(g, Board(g, g.Player1, "EX1_096"));
    Check("C4 Loot Hoarder draws two cards", g.Player1.HandZone.Count == hand + 2);
    var plain = Ready(Plan());
    Kill(plain, Board(plain, plain.Player1, "EX1_029"));
    Check("C5 without the rule: once", plain.Player2.Hero.Damage == 2 && DeathrattleRules.Repeats(plain.Player1, null) == 1);
});

// ============================================================ D. DALA_854 Nesting
Guard("D", () =>
{
    var g = Ready(Plan("DALA_854"));
    Mana(g.Player1);
    var yeti = (Minion)ToHand(g, g.Player1, "CS2_182");
    Play(g, yeti);
    Check("D1 a played minion gains Nested and counts as a Deathrattle minion", Both(g, "DALA_854") && Ench(yeti, "DALA_854e2") == 1 && yeti.HasDeathrattle && yeti.AttackDamage == 4 && yeti.Health == 5);
    var summoned = Board(g, g.Player1, "CS2_120");
    Check("D2 a summoned (not played) minion gains nothing", Ench(summoned, "DALA_854e2") == 0 && !summoned.HasDeathrattle);
    int position = yeti.ZonePosition;
    Kill(g, yeti);
    var copy = g.Player1.BoardZone.FirstOrDefault(m => m.Card.Id == "CS2_182");
    Check("D3 when it dies a 1/1 copy of it takes its place", copy != null && !ReferenceEquals(copy, yeti) && copy.AttackDamage == 1 && copy.Health == 1 && copy.ZonePosition == position
        && Ench(copy, "DALA_854e3") == 1 && g.Player1.BoardZone.Count == 2, copy == null ? "no copy" : copy.AttackDamage + "/" + copy.Health + " at " + copy.ZonePosition);
    Check("D4 the copy was not played: it has no Nested and leaves nothing when it dies", Ench(copy, "DALA_854e2") == 0);
    Kill(g, copy);
    Check("D4b nothing follows the copy", g.Player1.BoardZone.Count == 1 && g.Player1.BoardZone[0].Card.Id == "CS2_120");
    ToBoss(g); Mana(g.Player2);
    var theirs = (Minion)ToHand(g, g.Player2, "CS2_182");
    Play(g, theirs);
    Kill(g, theirs);
    Check("D5 the boss's played minion leaves a 1/1 copy on the boss's board", g.Player2.BoardZone.Count == 1 && g.Player2.BoardZone[0].Card.Id == "CS2_182" && g.Player2.BoardZone[0].Health == 1
        && g.Player1.BoardZone.Count == 1);
    ClearBoards(g);
    var last = (Minion)ToHand(g, g.Player2, "CS2_182");
    Play(g, last);
    for (int i = 0; i < 6; i++) Board(g, g.Player2, "CS2_120");
    Kill(g, last);
    Check("D6 the dead minion's own slot is free again, so the copy still fits a board that was full", g.Player2.BoardZone.Count == 7 && g.Player2.BoardZone.Count(m => m.Card.Id == "CS2_182" && m.Health == 1) == 1);
});

// ============================================================ E. DALA_855 Refreshing
Guard("E", () =>
{
    var g = Ready(Plan("DALA_855"));
    while (g.Player1.HandZone.Count > 2) { var card = g.Player1.HandZone[0]; g.Player1.HandZone.Remove(card); g.Player1.SetasideZone.Add(card); }
    int deck = g.Player1.DeckZone.Count, bossHand = g.Player2.HandZone.Count;
    EndTurn(g);
    Check("E1 at the end of the run player's turn the run player draws until 5 cards; the boss does not draw for it", Both(g, "DALA_855") && g.Player1.HandZone.Count == 5 && g.Player1.DeckZone.Count == deck - 3
        && g.Player2.HandZone.Count == bossHand + 1, g.Player1.HandZone.Count + " " + g.Player2.HandZone.Count + " (boss drew its turn card)");
    while (g.Player2.HandZone.Count > 1) { var card = g.Player2.HandZone[0]; g.Player2.HandZone.Remove(card); g.Player2.SetasideZone.Add(card); }
    EndTurn(g);
    Check("E2 at the end of the boss's turn the boss draws until 5 cards", g.Player2.HandZone.Count == 5);
    while (g.Player1.HandZone.Count < 7) ToHand(g, g.Player1, "CS2_182");
    int full = g.Player1.HandZone.Count; deck = g.Player1.DeckZone.Count;
    EndTurn(g);
    Check("E3 with 5 or more cards nothing is drawn", g.Player1.HandZone.Count == full && g.Player1.DeckZone.Count == deck);
    // An empty deck: the missing cards are counted once and each draw is Fatigue (Jeeves's rule).
    var g2 = Ready(Plan("DALA_855"));
    foreach (var card in g2.Player1.DeckZone.ToArray()) { g2.Player1.DeckZone.Remove(card); g2.Player1.SetasideZone.Add(card); }
    while (g2.Player1.HandZone.Count > 3) { var card = g2.Player1.HandZone[0]; g2.Player1.HandZone.Remove(card); g2.Player1.SetasideZone.Add(card); }
    EndTurn(g2);
    Check("E4 an empty deck: two missing cards are two Fatigue draws (1 + 2 damage)", g2.Player1.HandZone.Count == 3 && g2.Player1.Hero.Damage == 3, "damage " + g2.Player1.Hero.Damage);
});

// ============================================================ F. DALA_856 Wild Magic
Guard("F", () =>
{
    var g = Ready(Plan("DALA_856"));
    int hand = g.Player1.HandZone.Count;
    Cast(g, "CS2_023");
    Check("F1 Arcane Intellect is cast an additional time: four cards drawn, and the copy does not cast again", Both(g, "DALA_856") && g.Player1.HandZone.Count == hand + 4, (g.Player1.HandZone.Count - hand) + " drawn");
    int total = 0;
    for (int seed = 1; seed <= 10; seed++)
    {
        var gs = Ready(Plan("DALA_856"), seed, history: false);
        var a = Board(gs, gs.Player1, "CS2_182"); var b = Board(gs, gs.Player2, "CS2_182");
        Cast(gs, "CS2_029", gs.Player2.Hero);
        // The copy's 6 damage went to exactly one character: the boss again, the caster's hero, or one of the two 4/5 Yetis (which dies).
        bool[] hit = { gs.Player2.Hero.Damage == 12, gs.Player1.Hero.Damage == 6, a.Zone != gs.Player1.BoardZone, b.Zone != gs.Player2.BoardZone };
        if (gs.Player2.Hero.Damage >= 6 && hit.Count(x => x) == 1) total++;
    }
    Check("F2 Fireball: the chosen target takes 6 and the copy hits a random character for 6 (ten seeds)", total == 10, total + " of 10");
    var targets = new HashSet<string>();
    for (int seed = 1; seed <= 16; seed++)
    {
        var gs = Ready(Plan("DALA_856"), seed, history: false);
        var a = Board(gs, gs.Player1, "CS2_182"); var b = Board(gs, gs.Player2, "CS2_182");
        Cast(gs, "CS2_029", gs.Player2.Hero);
        targets.Add(gs.Player2.Hero.Damage == 12 ? "boss" : gs.Player1.Hero.Damage == 6 ? "own hero" : a.Zone != gs.Player1.BoardZone ? "own minion" : "boss minion");
    }
    Check("F3 the copy's target is random among all characters, the caster's own included", targets.Count >= 3, string.Join(",", targets.OrderBy(x => x)));
    Mana(g.Player1);
    Play(g, ToHand(g, g.Player1, "EX1_287"));
    Check("F4 a Secret is not cast again (it could not enter play twice)", g.Player1.SecretZone.Count == 1 && g.Player1.GraveyardZone.Count(c => c.Card.Id == "EX1_287") == 0);
    ToBoss(g);
    int bossHand = g.Player2.HandZone.Count;
    Cast(g, "CS2_023");
    Check("F5 the boss's spell: Counterspell counters the played spell, and a countered spell casts no copy", g.Player2.HandZone.Count == bossHand && g.Player1.SecretZone.Count == 0, (g.Player2.HandZone.Count - bossHand) + " drawn");
    bossHand = g.Player2.HandZone.Count;
    Cast(g, "CS2_023");
    Check("F6 the boss's next Arcane Intellect draws four", g.Player2.HandZone.Count == bossHand + 4);
    var plain = Ready(Plan());
    hand = plain.Player1.HandZone.Count;
    Cast(plain, "CS2_023");
    Check("F7 without the rule: two cards", plain.Player1.HandZone.Count == hand + 2);
    // The helper itself: a recast copy never recasts, a Secret is refused.
    var g3 = Ready(Plan());
    var spell = (Spell)Entity.FromCard(g3.Player1, C("CS2_023"));
    hand = g3.Player1.HandZone.Count;
    bool cast = EncounterRules.RecastRandom(spell, g3.Player1.Hero);
    var marked = (Spell)Entity.FromCard(g3.Player1, C("CS2_023")); marked.NativeTags[RecastRules.Recasting] = 1;
    Check("F8 RecastRandom casts a copy of a spell, never of a recast's copy, a Secret or a Quest", cast && g3.Player1.HandZone.Count == hand + 2 && !EncounterRules.RecastRandom(marked, null)
        && !EncounterRules.RecastRandom((Spell)Entity.FromCard(g3.Player1, C("EX1_287")), null) && !EncounterRules.RecastRandom(null, null));
});

// ============================================================ G. DALA_858 Fishy
Guard("G", () =>
{
    int summons = 0, turns = 0, mine = 0, theirs = 0; bool allMurlocs = true, allowed = true;
    var ids = new HashSet<string>();
    for (int seed = 1; seed <= 4; seed++)
    {
        var g = Ready(Plan("DALA_858"), seed, history: false);
        if (seed == 1) Check("G1 the rule is on both players", Both(g, "DALA_858"));
        // The first turn start already rolled (MainBegin); count from the boards as they are.
        for (int t = 0; t < 40 && g.State != State.COMPLETE; t++)
        {
            foreach (var c in new[] { g.Player1, g.Player2 })
                foreach (var m in c.BoardZone.ToArray())
                {
                    summons++; ids.Add(m.Card.Id);
                    if (c == g.Player1) mine++; else theirs++;
                    allMurlocs &= m.Card.IsRace(Race.MURLOC) && m.Card.Implemented;
                    allowed &= !(c == g.Player1 ? rejects : rejectsBoss.Concat(rejects)).Contains(m.Card.Id);
                    if (g.CurrentPlayer != c) allowed = false;
                }
            ClearBoards(g);
            foreach (var c in new[] { g.Player1, g.Player2 }) { c.Hero.Damage = 0; while (c.HandZone.Count > 5) { var card = c.HandZone[0]; c.HandZone.Remove(card); c.SetasideZone.Add(card); } }
            turns++;
            EndTurn(g);
        }
    }
    Check("G2 at the start of a player's turn a Murloc sometimes joins that player's board (1 in " + DungeonRunEncounters.FishyOneIn + "): " + summons + " in " + turns + " turn starts",
        summons >= turns / 8 && summons <= turns / 2 && mine > 0 && theirs > 0, mine + " for the run player, " + theirs + " for the boss");
    Check("G3 every one is a scripted Murloc the generation filter allows, summoned for the player whose turn started", allMurlocs && allowed && ids.Count >= 5, ids.Count + " distinct");
    var plain = Ready(Plan());
    for (int t = 0; t < 12; t++) EndTurn(plain);
    Check("G4 without the rule no minion appears", plain.Player1.BoardZone.Count == 0 && plain.Player2.BoardZone.Count == 0);
});

// ============================================================ H. DALA_859 Gorged
Guard("H", () =>
{
    var plain = Start(Plan());
    var g = Start(Plan("DALA_859"));
    Check("H1 both players are dealt two more opening cards, before the mulligan", g.Player1.HandZone.Count == plain.Player1.HandZone.Count + 2 && g.Player2.HandZone.Count == plain.Player2.HandZone.Count + 2
        && g.Player1.Choice.Choices.Count == plain.Player1.Choice.Choices.Count + 2 && g.Player2.Choice.Choices.Count == plain.Player2.Choice.Choices.Count + 2,
        g.Player1.HandZone.Count + "/" + g.Player2.HandZone.Count + " against " + plain.Player1.HandZone.Count + "/" + plain.Player2.HandZone.Count);
    Check("H2 both players start with two Mana Crystals; the rule has no player enchantment and is active by its mark", g.Player1.BaseMana == 2 && g.Player2.BaseMana == 2 && Both(g, "DALA_859")
        && DungeonRunGlobalRules.EffectOf("DALA_859") == null && !DungeonRunGlobalRules.IsActive(plain.Player1, "DALA_859"));
    Finish(g);
    Check("H3 the run player's first turn has 3 Mana Crystals", g.Player1.BaseMana == 3 && g.Player1.RemainingMana == 3);
    EndTurn(g);
    Check("H4 the boss's first turn has 3 Mana Crystals", g.Player2.BaseMana == 3 && g.Player2.RemainingMana == 3);
    DungeonRunGlobalRules.Apply(g, "DALA_859");
    Check("H5 applying the rule again gives nothing more (its mark is on the game, in a clone too)", g.Player1.BaseMana == 3 && g.Player2.BaseMana == 3 && g.Clone()[EncounterRules.RuleApplied] == g[EncounterRules.RuleApplied]
        && g[EncounterRules.RuleApplied] != 0);
    var rich = Plan("DALA_859"); rich.PlayerOpeningHandBonus = 4; rich.BossOpeningHandBonus = 5;
    var cfg = new GameConfig { RandomSeed = 3, Shuffle = true, SkipMulligan = false, History = false, Logging = false, FormatType = FormatType.FT_WILD };
    DungeonRunSetup.Configure(cfg, rich);
    Check("H6 the engine's opening-hand cap applies to the sum", cfg.Player1OpeningHandBonus == GameConfig.MaxOpeningHandBonus && cfg.Player2OpeningHandBonus == GameConfig.MaxOpeningHandBonus);
    var cfg0 = new GameConfig { RandomSeed = 3, Shuffle = true, SkipMulligan = false, History = false, Logging = false, FormatType = FormatType.FT_WILD };
    DungeonRunSetup.Configure(cfg0, Plan("DALA_855"));
    Check("H7 a rule without opening cards leaves both bonuses alone", cfg0.Player1OpeningHandBonus == 0 && cfg0.Player2OpeningHandBonus == 0);
});

// ============================================================ I. DALA_860 Dragon Soul
Guard("I", () =>
{
    var g = Ready(Plan("DALA_860"));
    Cast(g, "CS2_023"); Cast(g, "CS2_023");
    Check("I1 two spells in a turn: no Dragon yet", Both(g, "DALA_860") && g.Player1.BoardZone.Count == 0);
    Cast(g, "CS2_023");
    var dragon = g.Player1.BoardZone.FirstOrDefault();
    Check("I2 the third spell in a turn summons a 5/5 Dragon Spirit for the run player", g.Player1.BoardZone.Count == 1 && dragon.Card.Id == "LOOT_209t" && dragon.AttackDamage == 5 && dragon.Health == 5
        && dragon.Card.IsRace(Race.DRAGON) && g.Player2.BoardZone.Count == 0);
    Cast(g, "EX1_277"); Cast(g, "EX1_277");
    Check("I3 the count starts again after a Dragon", g.Player1.BoardZone.Count == 1);
    Cast(g, "EX1_277");
    Check("I4 six spells in a turn: two Dragons", g.Player1.BoardZone.Count(m => m.Card.Id == "LOOT_209t") == 2);
    Cast(g, "CS2_023"); Cast(g, "CS2_023");
    ToBoss(g);
    Cast(g, "CS2_023");
    Check("I5 the count is per player and per turn: the boss's first spell summons nothing", g.Player2.BoardZone.Count == 0);
    Cast(g, "CS2_023"); Cast(g, "CS2_023");
    Check("I6 the boss's third spell in its turn summons the boss's Dragon", g.Player2.BoardZone.Count == 1 && g.Player2.BoardZone[0].Card.Id == "LOOT_209t");
    ToPlayer(g);
    Cast(g, "EX1_277");
    Check("I7 the run player's two spells of last turn do not count this turn", g.Player1.BoardZone.Count(m => m.Card.Id == "LOOT_209t") == 2);
    var clone = g.Clone();
    Mana(clone.Player1);
    clone.Process(PlayCardTask.Any(clone.CurrentPlayer, ToHand(clone, clone.Player1, "EX1_277"), null, -1, 0));
    Mana(clone.Player1);
    clone.Process(PlayCardTask.Any(clone.CurrentPlayer, ToHand(clone, clone.Player1, "EX1_277"), null, -1, 0));
    Check("I8 a clone keeps the count (an entity tag of the player's enchantment)", clone.Player1.BoardZone.Count(m => m.Card.Id == "LOOT_209t") == 3 && g.Player1.BoardZone.Count(m => m.Card.Id == "LOOT_209t") == 2);
});

// ============================================================ J. DALA_861 Ogreish
Guard("J", () =>
{
    int wrong = 0, right = 0, bossWrong = 0, bossRight = 0;
    for (int seed = 1; seed <= 40; seed++)
    {
        var g = Ready(Plan("DALA_861"), seed, history: false);
        if (seed == 1) Check("J1 the rule is on both players", Both(g, "DALA_861"));
        var boar = Board(g, g.Player1, "CS2_171");
        var dummy = Board(g, g.Player2, "CS2_182");
        g.Process(MinionAttackTask.Any(g.Player1, boar, dummy));
        if (dummy.Damage == 1) right++; else if (g.Player2.Hero.Damage == 1) wrong++;
        ToBoss(g);
        var theirs = Board(g, g.Player2, "CS2_171");
        var guard = Board(g, g.Player1, "CS2_182");
        int heroBefore = g.Player1.Hero.Damage;
        g.Process(MinionAttackTask.Any(g.Player2, theirs, guard));
        // Another enemy: the run player's hero, or its Boar when that survived its own attack.
        if (guard.Damage == 1) bossRight++; else if (g.Player1.Hero.Damage == heroBefore + 1 || boar.Zone != g.Player1.BoardZone || boar.Damage > 0) bossWrong++;
    }
    Check("J2 a run player's minion attacks the wrong enemy about half the time (40 attacks)", wrong + right == 40 && wrong >= 10 && wrong <= 30, wrong + " wrong, " + right + " right");
    Check("J3 so does a boss's minion", bossWrong + bossRight == 40 && bossWrong >= 10 && bossWrong <= 30, bossWrong + " wrong, " + bossRight + " right");
    var lone = Ready(Plan("DALA_861"), 5, history: false);
    var charger = Board(lone, lone.Player1, "CS2_171");
    for (int i = 0; i < 1; i++) lone.Process(MinionAttackTask.Any(lone.Player1, charger, lone.Player2.Hero));
    Check("J4 with no other enemy the attack lands where it was aimed", lone.Player2.Hero.Damage == 1);
    var plain = Ready(Plan(), 3, history: false);
    int hits = 0;
    for (int i = 0; i < 6; i++)
    {
        var b = Board(plain, plain.CurrentPlayer, "CS2_171"); var t = Board(plain, plain.CurrentPlayer.Opponent, "CS2_182");
        plain.Process(MinionAttackTask.Any(plain.CurrentPlayer, b, t));
        if (t.Damage == 1) hits++;
        ClearBoards(plain);
    }
    Check("J5 without the rule every attack lands where it was aimed", hits == 6);
    // A Secret that answers the attack reads the defender the minion really attacks (found by the rules05 bosspilot mode:
    // Splitting Image, validated on the aimed minion and resolved after a redirect to the hero, tried to copy a hero).
    int copied = 0, kept = 0, other = 0;
    for (int seed = 1; seed <= 24; seed++)
    {
        var g = Ready(Plan("DALA_861"), seed, history: false);
        ToBoss(g);
        DungeonRunEncounters.PutSecret(g.Player2, C("TRL_400"));
        var defender = Board(g, g.Player2, "CS2_182");
        ToPlayer(g);
        var boar = Board(g, g.Player1, "CS2_171");
        g.Process(MinionAttackTask.Any(g.Player1, boar, defender));
        bool secretLeft = g.Player2.SecretZone.Count == 0;
        if (defender.Damage == 1 && secretLeft && g.Player2.BoardZone.Count(m => m.Card.Id == "CS2_182") == 2) copied++;
        else if (g.Player2.Hero.Damage == 1 && !secretLeft && g.Player2.BoardZone.Count == 1) kept++;
        else other++;
    }
    Check("J6 Splitting Image under the rule: the attacked Yeti is copied; an attack that went to the hero leaves the Secret in play", copied >= 5 && kept >= 5 && other == 0,
        copied + " copied, " + kept + " kept, " + other + " other");
    // Family 02's Ogre Warmaul (GVG_054) redirected from the task queue: with the weapon equipped before the Secret is played,
    // Splitting Image was validated on the aimed minion and resolved after the redirect (13 of 24 attacks threw
    // InvalidCastException in SummonCopyTask). The wave 2 integration made its trigger redirect at once, as the rule does;
    // the family's note is a check since then.
    int warmaulFaults = 0, warmaulAttacks = 0;
    for (int seed = 1; seed <= 24; seed++)
    {
        try
        {
            var g = Ready(Plan(), seed, history: false);
            Mana(g.Player1);
            Play(g, ToHand(g, g.Player1, "GVG_054"));
            ToBoss(g);
            DungeonRunEncounters.PutSecret(g.Player2, C("TRL_400"));
            var defender = Board(g, g.Player2, "CS2_182");
            ToPlayer(g);
            if (g.Player1.Hero.Weapon?.Card.Id != "GVG_054") continue;
            warmaulAttacks++;
            g.Process(HeroAttackTask.Any(g.Player1, defender));
        }
        catch (InvalidCastException) { warmaulFaults++; }
    }
    Check("J7 Ogre Warmaul (GVG_054, family 02) equipped before Splitting Image: no hero attack faults", warmaulFaults == 0 && warmaulAttacks == 24, warmaulFaults + " of " + warmaulAttacks + " hero attacks fault");
});

// ============================================================ K. DALA_862 Reductive
Guard("K", () =>
{
    var g = Ready(Plan("DALA_862"));
    var fireball = ToHand(g, g.Player1, "CS2_029"); var theirs = ToHand(g, g.Player2, "CS2_029"); var wisp = ToHand(g, g.Player1, "CS2_231");
    var bells = ToHand(g, g.Player1, "GIL_145"); var evolution = ToHand(g, g.Player1, "LOOT_504");
    EndTurn(g);
    Check("K1 at the end of the run player's turn its hand costs (1) less; the boss's hand does not", Both(g, "DALA_862") && fireball.Cost == 3 && theirs.Cost == 4 && wisp.Cost == 0
        && g.Player1.HandZone.Where(c => c.Card.Id == "CS2_182").All(c => c.Cost == 3), fireball.Cost + " " + theirs.Cost);
    Check("K2 an Echo or repeatable card is never brought below (1)", bells.Cost == 1 && evolution.Cost == 1 && Ench(evolution, "BRM_028e") == 0 && Ench(bells, "BRM_028e") == 1);
    EndTurn(g);
    Check("K3 at the end of the boss's turn the boss's hand costs (1) less; the run player's is unchanged", theirs.Cost == 3 && fireball.Cost == 3);
    EndTurn(g);
    Check("K4 it adds up turn after turn; Sound the Bells! stays at (1)", fireball.Cost == 2 && bells.Cost == 1);
    Mana(g.Player2, 0);
    var plain = Ready(Plan());
    var same = ToHand(plain, plain.Player1, "CS2_029");
    EndTurn(plain);
    Check("K5 without the rule nothing changes", same.Cost == 4);
});

// ============================================================ L. DALA_863 Hallucinating
Guard("L", () =>
{
    var g = Start(Plan("DALA_863"));
    Check("L1 nothing before the mulligan: decks and hands hold no Hallucination", Both(g, "DALA_863") && g.Player1.DeckZone.Concat(g.Player1.HandZone).Concat(g.Player2.DeckZone).Concat(g.Player2.HandZone).All(c => c.Card.Id != "UNG_856"));
    int deck1 = g.Player1.DeckZone.Count, deck2 = g.Player2.DeckZone.Count, cards1 = deck1 + g.Player1.HandZone.Count;
    Finish(g);
    int in1 = g.Player1.DeckZone.Count(c => c.Card.Id == "UNG_856") + g.Player1.HandZone.Count(c => c.Card.Id == "UNG_856");
    int in2 = g.Player2.DeckZone.Count(c => c.Card.Id == "UNG_856") + g.Player2.HandZone.Count(c => c.Card.Id == "UNG_856");
    Check("L2 after both mulligans each deck holds ten Hallucinations", in1 == 10 && in2 == 10 && g.Player1.DeckZone.Count + g.Player1.HandZone.Count == cards1 + 10
        && g.Player2.DeckZone.Count == deck2 + 10, in1 + " " + in2);
    var positions = g.Player1.DeckZone.Select((c, i) => c.Card.Id == "UNG_856" ? i : -1).Where(i => i >= 0).ToList();
    Check("L3 they are shuffled in, not stacked", positions.Count >= 9 && positions.Max() - positions.Min() > positions.Count);
    var fact1 = g.RunDeckFacts.FirstOrDefault(f => f.ControllerId == g.Player1.Id); var fact2 = g.RunDeckFacts.FirstOrDefault(f => f.ControllerId == g.Player2.Id);
    Check("L4 each owner's deck is recorded for its own-deck ledger", g.RunDeckFacts.Count == 2 && fact1 != null && fact2 != null && fact2.Kind == RunDeckFactKind.Rewritten
        && fact2.Definitions.Count(d => d == "UNG_856") == 10 && fact2.Definitions.Count == deck2 + 10);
    var sample = OwnDeckLedger.Observe(g, g.Player2, null).Sample(g, g.Player2, 11);
    Check("L5 the boss's own-deck ledger samples its deck with the ten Hallucinations", sample.TryGetValue("UNG_856", out int n) && n == 10 && sample.Values.Sum() == g.Player2.DeckZone.Count,
        string.Join(",", sample.Select(p => p.Key + "x" + p.Value)));
    Mana(g.Player1);
    var mine = ToHand(g, g.Player1, "UNG_856");
    Play(g, mine);
    var offers = g.Player1.Choice?.Choices.Select(id => g.IdEntityDic[id].Card).ToList() ?? new List<Card>();
    Check("L6 the run player's Hallucination: a Discover among three cards of the boss's class the generation filter allows", offers.Count == 3 && offers.All(c => c.Class == CardClass.WARRIOR && !rejects.Contains(c.Id)),
        string.Join(",", offers.Select(c => c.Id)));
    if (g.Player1.Choice != null) g.Process(ChooseTask.Pick(g.Player1, g.Player1.Choice.Choices[0]));
    ToBoss(g); Mana(g.Player2);
    Play(g, ToHand(g, g.Player2, "UNG_856"));
    var bossOffers = g.Player2.Choice?.Choices.Select(id => g.IdEntityDic[id].Card).ToList() ?? new List<Card>();
    Check("L7 the boss's Hallucination: a Discover among three cards of the run player's class that both generation lists allow", bossOffers.Count == 3
        && bossOffers.All(c => c.Class == CardClass.MAGE && !rejects.Contains(c.Id) && !rejectsBoss.Contains(c.Id)), string.Join(",", bossOffers.Select(c => c.Id)));
    var big = Plan("DALA_863", playerDeck: Enumerable.Repeat("CS2_182", 50).ToArray());
    var gb = Ready(big);
    Check("L8 a 50-card run deck holds them too (60 cards with the hand)", gb.Player1.DeckZone.Count + gb.Player1.HandZone.Count == 60);
});

// ============================================================ M. DALA_865 Explosive
Guard("M", () =>
{
    var g = Ready(Plan("DALA_865"));
    var mine = Board(g, g.Player1, "CS2_182"); var other = Board(g, g.Player1, "CS2_182"); var theirs = Board(g, g.Player2, "CS2_182");
    Check("M1 a summoned minion gains Explosive (a Deathrattle), on either side", Both(g, "DALA_865") && Ench(mine, "DALA_865e2") == 1 && mine.HasDeathrattle && Ench(theirs, "DALA_865e2") == 1);
    Kill(g, mine);
    Check("M2 when it dies it deals 1 damage to all minions", other.Damage == 1 && theirs.Damage == 1 && g.Player1.Hero.Damage == 0 && g.Player2.Hero.Damage == 0);
    Mana(g.Player1);
    var played = (Minion)ToHand(g, g.Player1, "CS2_171");
    Play(g, played);
    Check("M3 a played minion gains it too", Ench(played, "DALA_865e2") == 1);
    var a = Board(g, g.Player2, "CS2_171"); var b = Board(g, g.Player2, "CS2_171");
    Kill(g, a);
    Check("M4 a chain: the 1 damage kills the 1-Health minions, whose own explosions follow", b.Zone != g.Player2.BoardZone && played.Zone != g.Player1.BoardZone && other.Damage == 4 && theirs.Damage == 4,
        other.Damage + " " + theirs.Damage);
    // Wave 2 integration: two minions that summon each other when they die (Eternal Bloodpetal 5/1 and its 0/1 Seedling) exploded
    // on each other without end; the rule marks at most ExplosiveMarksPerTurn minions of one player a turn. The 30 marked
    // minions die one after the other, the 31st (unmarked) dies to the 30th's explosion, and the 32nd stays.
    var k = Ready(Plan("DALA_865"));
    var petal = Board(k, k.Player1, "TLC_234");
    bool ended = System.Threading.Tasks.Task.Run(() => Kill(k, petal)).Wait(20000);
    var left = ended && k.Player1.BoardZone.Count == 1 ? k.Player1.BoardZone[0] : null;
    int pairDeaths = ended ? k.Player1.GraveyardZone.Count(x => x.Card.Id == "TLC_234" || x.Card.Id == "TLC_234t") : -1;
    Check("M5 Eternal Bloodpetal and its Seedling: the death phase ends once " + DungeonRunEncounters.ExplosiveMarksPerTurn + " minions of that player were marked this turn",
        ended && k.State != State.COMPLETE && left != null && left.Card.Id == "TLC_234t" && Ench(left, "DALA_865e2") == 0 && pairDeaths == DungeonRunEncounters.ExplosiveMarksPerTurn + 1
        && k.Player2.BoardZone.Count == 0, (ended ? "ended" : "STILL RUNNING after 20 s") + ", " + pairDeaths + " deaths, left " + (left?.Card.Id ?? "-"));
    if (ended)
    {
        var late = Board(k, k.Player1, "CS2_182");
        Check("M6 past the limit a minion summoned in the same turn gains nothing; the other player's count is its own", Ench(late, "DALA_865e2") == 0 && Ench(Board(k, k.Player2, "CS2_182"), "DALA_865e2") == 1);
        EndTurn(k);
        Check("M7 the count starts again at the turn's end", Ench(Board(k, k.Player1, "CS2_182"), "DALA_865e2") == 1);
    }
});

// ============================================================ N. DALA_867 Infused
Guard("N", () =>
{
    var counts = new int[4]; bool one = true;
    for (int seed = 1; seed <= 6; seed++)
    {
        var g = Ready(Plan("DALA_867"), seed, history: false);
        if (seed == 1) Check("N1 the rule is on both players", Both(g, "DALA_867"));
        for (int i = 0; i < 6; i++)
        {
            var m = Board(g, i % 2 == 0 ? g.Player1 : g.Player2, "CS2_182");
            bool[] has = { m.HasTaunt, m.HasDivineShield, m.IsRush, m.HasWindfury };
            one &= has.Count(x => x) == 1;
            for (int k = 0; k < 4; k++) if (has[k]) counts[k]++;
        }
    }
    Check("N2 each summoned minion gains exactly one of Taunt, Divine Shield, Rush and Windfury", one, string.Join("/", counts));
    Check("N3 all four are given over 36 summons", counts.All(n => n >= 3) && counts.Sum() == 36, string.Join("/", counts));
    Minion rusher = null; Game gr = null;
    for (int seed = 1; seed <= 40 && rusher == null; seed++)
    {
        gr = Ready(Plan("DALA_867"), seed, history: false);
        Board(gr, gr.Player2, "CS2_182");
        Mana(gr.Player1);
        var m = (Minion)ToHand(gr, gr.Player1, "CS2_120");
        Play(gr, m);
        if (m.IsRush) rusher = m;
    }
    Check("N4 a played minion that got Rush can attack a minion at once, not the hero", rusher != null && rusher.CanAttack && rusher.ValidAttackTargets.Any() && rusher.ValidAttackTargets.All(t => t is Minion),
        rusher == null ? "no Rush in 40 seeds" : "");
});

// ============================================================ O. DALA_829 The Coffers
Guard("O", () =>
{
    var g = Start(Plan("DALA_829"));
    Check("O1 nothing on the boards before the mulligan; the rule has no player enchantment", g.Player2.BoardZone.Count == 0 && Both(g, "DALA_829") && DungeonRunGlobalRules.EffectOf("DALA_829") == null);
    Finish(g);
    var cache = g.Player2.BoardZone.FirstOrDefault();
    Check("O2 after both mulligans the boss has a 0/3 Cache of Cash; the run player has none", g.Player2.BoardZone.Count == 1 && cache.Card.Id == "DALA_829t" && cache.AttackDamage == 0 && cache.Health == 3
        && g.Player1.BoardZone.Count == 0 && cache.HasDeathrattle && g[EncounterRules.StartDone] == 1);
    Check("O3 it is an ordinary minion: the run player may attack and target it", !cache.Untouchable && !cache.Card.Untouchable);
    int h1 = g.Player1.HandZone.Count, h2 = g.Player2.HandZone.Count;
    Kill(g, cache);
    Check("O4 its Deathrattle gives each player two Coins", g.Player1.HandZone.Count == h1 + 2 && g.Player2.HandZone.Count == h2 + 2 && g.Player1.HandZone.Count(c => c.Card.Id == "GAME_005") == 2
        && g.Player2.HandZone.Count(c => c.Card.Id == "GAME_005") >= 2);
    var clone = Ready(Plan("DALA_829")).Clone();
    EncounterRules.ResolveStart(clone);
    Check("O5 the start hook runs once (a second resolve does nothing, in a clone too)", clone.Player2.BoardZone.Count == 1);
});

// ============================================================ P. DALA_831 The Stench
Guard("P", () =>
{
    var g = Ready(Plan("DALA_831"));
    Mana(g.Player1);
    var yeti = (Minion)ToHand(g, g.Player1, "CS2_182");
    Play(g, yeti);
    Check("P1 a played 4/5 Yeti is a 5/4 with the Stinky enchantment", Both(g, "DALA_831") && yeti.AttackDamage == 5 && yeti.Health == 4 && Ench(yeti, "DALA_831e2") == 1, yeti.AttackDamage + "/" + yeti.Health);
    var croc = Board(g, g.Player2, "CS2_120");
    Check("P2 a summoned boss 2/3 Crocolisk is a 3/2", croc.AttackDamage == 3 && croc.Health == 2);
    var dummy = Board(g, g.Player1, "GVG_093");
    Check("P3 a 0-Attack minion has 0 Health and dies", dummy.Zone != g.Player1.BoardZone && g.Player1.GraveyardZone.Any(c => c.Card.Id == "GVG_093"));
    Mana(g.Player1);
    var archer = (Minion)ToHand(g, g.Player1, "CS2_189");
    Play(g, archer, g.Player2.Hero);
    Check("P4 the swap comes as the minion enters play; its Battlecry still resolves", archer.AttackDamage == 1 && archer.Health == 1 && g.Player2.Hero.Damage == 1);
    Generic.AddEnchantmentBlock(g, C("BRM_028e"), g.Player1.Hero, yeti);
    Cast(g, "GIL_145", yeti);
    Check("P5 a later buff applies to the swapped values (5/4 and +1/+2: 6/6)", yeti.AttackDamage == 6 && yeti.Health == 6, yeti.AttackDamage + "/" + yeti.Health);
    Hit(g, croc, 1);
    Check("P6 damage is dealt to the swapped Health", croc.Health == 1 && croc.Zone == g.Player2.BoardZone);
    var hurt = Ready(Plan("DALA_831"));
    Mana(hurt.Player1);
    Play(hurt, ToHand(hurt, hurt.Player1, "CS2_027"));
    Check("P7 Mirror Image's two 0/2 Taunts are 2/0 and die as they arrive", hurt.Player1.BoardZone.Count == 0 && hurt.Player1.GraveyardZone.Count(c => c.Card.Id == "CS2_mirror") == 2);
    var plain = Ready(Plan());
    var same = Board(plain, plain.Player1, "CS2_182");
    Check("P8 without the rule: 4/5", same.AttackDamage == 4 && same.Health == 5);
});

// ============================================================ Q. DALA_832 The Prisons
Guard("Q", () =>
{
    // A seed whose two prisoners both cost 2 or more, so both prisons still stand once the run player's first turn has started.
    Game g = null; Minion mine = null, theirs = null;
    for (int seed = 7; seed < 60; seed++)
    {
        g = Start(Plan("DALA_832"), seed);
        if (seed == 7) Check("Q1 nothing on the boards before the mulligan", g.Player1.BoardZone.Count == 0 && g.Player2.BoardZone.Count == 0 && Both(g, "DALA_832"));
        Finish(g);
        mine = g.Player1.BoardZone.FirstOrDefault(); theirs = g.Player2.BoardZone.FirstOrDefault();
        if (mine?.Card.Id == "DALA_832t" && theirs?.Card.Id == "DALA_832t" && EncounterRules.PrisonTurns(theirs) >= 2) break;
    }
    Card inMine = EncounterRules.Imprisoned(mine), inTheirs = EncounterRules.Imprisoned(theirs);
    Check("Q2 after both mulligans each player has a Violet Prison holding a minion", g.Player1.BoardZone.Count == 1 && g.Player2.BoardZone.Count == 1 && mine.Card.Id == "DALA_832t" && theirs.Card.Id == "DALA_832t"
        && inMine != null && inTheirs != null && inMine.Type == CardType.MINION && inMine.Implemented && inTheirs.Implemented, (inMine?.Id ?? "-") + " " + (inTheirs?.Id ?? "-"));
    Check("Q3 the held minion and the turns left are public tags (the turns: the minion's Cost, at least 1); the generation filter chose them",
        mine[GameTag.ALTERNATE_MOUSE_OVER_CARD] == inMine.AssetId && theirs[GameTag.ALTERNATE_MOUSE_OVER_CARD] == inTheirs.AssetId
        && EncounterRules.PrisonTurns(mine) == inMine.Cost - 1 && EncounterRules.PrisonTurns(theirs) == inTheirs.Cost && theirs[GameTag.SCORE_VALUE_1] == inTheirs.Cost
        && !rejects.Contains(inMine.Id) && !rejectsBoss.Contains(inTheirs.Id) && !rejects.Contains(inTheirs.Id),
        "run player's: " + EncounterRules.PrisonTurns(mine) + " left of " + inMine.Cost + " (its first turn started); boss's: " + EncounterRules.PrisonTurns(theirs) + " of " + inTheirs.Cost);
    Check("Q4 a prison is a permanent: untouchable, never a target, never attackable, not counted as a minion", mine.Untouchable && theirs.Untouchable && g.Player2.BoardZone.CountExceptUntouchables == 0
        && g.Player2.BoardZone.GetAll().Length == 0);
    // A known prisoner: a 2-Cost River Crocolisk is released at the start of its owner's second turn after the prison landed.
    var k = Ready(Plan());
    var prison = EncounterRules.Imprison(k.Player1, C("DALA_832t"), C("CS2_120"), k.Player1.Hero);
    var bossPrison = EncounterRules.Imprison(k.Player2, C("DALA_832t"), C("CS2_171"), k.Player2.Hero);
    k.DeathProcessingAndAuraUpdate();
    Check("Q5 Imprison: 2 turns for a 2-Cost minion, 1 for a 1-Cost one", EncounterRules.PrisonTurns(prison) == 2 && EncounterRules.PrisonTurns(bossPrison) == 1 && EncounterRules.Imprisoned(prison).Id == "CS2_120");
    Mana(k.Player1);
    var fireball = ToHand(k, k.Player1, "CS2_029");
    var charger = Board(k, k.Player1, "CS2_171");
    Check("Q6 the run player's Fireball and minions cannot choose the boss's prison", !fireball.ValidPlayTargets.Contains(bossPrison) && !charger.ValidAttackTargets.Contains(bossPrison) && charger.ValidAttackTargets.Contains(k.Player2.Hero));
    Cast(k, "EX1_312");
    Check("Q7 Twisting Nether destroys the minions and leaves both prisons", prison.Zone == k.Player1.BoardZone && bossPrison.Zone == k.Player2.BoardZone && charger.Zone != k.Player1.BoardZone);
    EndTurn(k);
    var boar = k.Player2.BoardZone.FirstOrDefault();
    Check("Q8 at the start of the boss's next turn its 1-Cost prisoner is released in the prison's place", k.Player2.BoardZone.Count == 1 && boar.Card.Id == "CS2_171" && bossPrison.Zone != k.Player2.BoardZone);
    Check("Q9 a released Charge minion may attack at once", boar.CanAttack && !boar.IsExhausted);
    EndTurn(k);
    Check("Q10 the run player's prison counts down at the start of the run player's turn", EncounterRules.PrisonTurns(prison) == 1 && prison.Zone == k.Player1.BoardZone && prison[GameTag.SCORE_VALUE_1] == 1);
    EndTurn(k); EndTurn(k);
    var croc = k.Player1.BoardZone.FirstOrDefault();
    Check("Q11 one turn later the Crocolisk is released; it entered play this turn and cannot attack yet", k.Player1.BoardZone.Count == 1 && croc.Card.Id == "CS2_120" && croc.AttackDamage == 2 && croc.Health == 3
        && !croc.CanAttack && croc.IsExhausted);
    EndTurn(k); EndTurn(k);
    Check("Q12 next turn it attacks as any minion", k.Player1.BoardZone[0].CanAttack);
    // The shipped rule end to end: each prisoner is released after as many of its owner's turns as it costs.
    int cost1 = Math.Max(1, inMine.Cost), cost2 = Math.Max(1, inTheirs.Cost), released1 = 0, released2 = 0;
    for (int turn = 1; turn <= 12 && g.State != State.COMPLETE; turn++)
    {
        // The run player's turn number "turn" has started.
        if (released1 == 0 && g.Player1.BoardZone.All(m => m.Card.Id != "DALA_832t")) released1 = turn;
        EndTurn(g);
        if (released2 == 0 && g.Player2.BoardZone.All(m => m.Card.Id != "DALA_832t")) released2 = turn;
        EndTurn(g);
    }
    Check("Q13 the shipped prisons release their minions after as many owner turns as they cost (the first turn start counts)", released1 == cost1 && released2 == cost2
        && g.Player1.BoardZone.Any(m => m.Card.Id == inMine.Id) && g.Player2.BoardZone.Any(m => m.Card.Id == inTheirs.Id), released1 + "/" + cost1 + " " + released2 + "/" + cost2);
    var costs = new HashSet<int>();
    // A prisoner that costs (0) or (1) is already released when the run player's first turn has started (wave 2 integration:
    // with Murloc Tinyfin scripted by family 08 the pool moved, and seed 6 draws a 1-Cost prisoner): read the released minion then.
    for (int seed = 1; seed <= 10; seed++)
    {
        var gs = Ready(Plan("DALA_832"), seed, history: false);
        foreach (var held in new[] { gs.Player1.BoardZone[0], gs.Player2.BoardZone[0] }) costs.Add((EncounterRules.Imprisoned(held) ?? held.Card).Cost);
    }
    Check("Q14 the prisoner is random (ten seeds: several different Costs)", costs.Count >= 4, string.Join(",", costs.OrderBy(x => x)));
});

// ============================================================ R. DALA_833 The Carts
Guard("R", () =>
{
    var g = Start(Plan("DALA_833"));
    Check("R1 nothing on the boards before the mulligan", g.Player1.BoardZone.Count == 0 && g.Player2.BoardZone.Count == 0 && Both(g, "DALA_833"));
    Finish(g);
    var carts1 = g.Player1.BoardZone.ToArray(); var carts2 = g.Player2.BoardZone.ToArray();
    Check("R2 after both mulligans each player has three different Carts", carts1.Length == 3 && carts2.Length == 3 && carts1.Concat(carts2).All(m => DungeonRunEncounters.Carts.Contains(m.Card.Id))
        && carts1.Select(m => m.Card.Id).Distinct().Count() == 3 && carts2.Select(m => m.Card.Id).Distinct().Count() == 3
        && EncounterRules.Blockers(g.Player1, DungeonRunEncounters.Carts) == 3, string.Join(",", carts1.Select(m => m.Card.Id)) + " | " + string.Join(",", carts2.Select(m => m.Card.Id)));
    for (int i = 0; i < 4; i++) Board(g, g.Player1, "CS2_182");
    Check("R3 both players can only have four minions: the board is full with four", g.Player1.BoardZone.IsFull && g.Player1.BoardZone.CountExceptUntouchables == 4);
    Mana(g.Player1);
    var fifth = ToHand(g, g.Player1, "CS2_182");
    bool played = false;
    try { played = Play(g, fifth); } catch (Exception) { played = false; }
    Check("R4 a fifth minion cannot be played", !played && fifth.Zone == g.Player1.HandZone && g.Player1.BoardZone.CountExceptUntouchables == 4);
    var theirs = Board(g, g.Player2, "CS2_182");
    Mana(g.Player1);
    var fireball = ToHand(g, g.Player1, "CS2_029");
    Check("R5 a Cart is never a target and cannot be attacked", carts2.All(c => !fireball.ValidPlayTargets.Contains(c)) && fireball.ValidPlayTargets.Contains(theirs)
        && g.Player1.BoardZone.Where(m => !m.Untouchable).All(m => m.ValidAttackTargets.All(t => !(t is Minion x) || !x.Untouchable)));
    Cast(g, "CS2_032");
    Cast(g, "EX1_312");
    Check("R6 Flamestrike and Twisting Nether leave all six Carts and clear the minions", g.Player1.BoardZone.Count == 3 && g.Player2.BoardZone.Count == 3 && g.Player1.BoardZone.Concat(g.Player2.BoardZone).All(m => m.Card.Untouchable)
        && g.Player1.BoardZone.CountExceptUntouchables == 0);
    for (int t = 0; t < 6; t++) EndTurn(g);
    Check("R7 the Carts stay for the whole match", EncounterRules.Blockers(g.Player1, DungeonRunEncounters.Carts) == 3 && EncounterRules.Blockers(g.Player2, DungeonRunEncounters.Carts) == 3);
    var kinds = new HashSet<string>();
    for (int seed = 1; seed <= 8; seed++) { var gs = Ready(Plan("DALA_833"), seed, history: false); foreach (var m in gs.Player1.BoardZone.Concat(gs.Player2.BoardZone)) kinds.Add(m.Card.Id); }
    Check("R8 all four Carts appear over eight seeds", kinds.Count == 4);
    bool refused = false;
    try { EncounterRules.PlaceBlockers(g.Player1, new[] { C("CS2_182") }, 1, g.Player1.Hero); } catch (ArgumentException) { refused = true; }
    Check("R9 PlaceBlockers refuses a card that is not a permanent", refused);
});

// ============================================================ S. the framework
string[] family = { "DALA_851", "DALA_852", "DALA_853", "DALA_854", "DALA_855", "DALA_856", "DALA_858", "DALA_859", "DALA_860", "DALA_861", "DALA_862", "DALA_863", "DALA_865", "DALA_867",
    "DALA_829", "DALA_831", "DALA_832", "DALA_833" };
string[] hooksOnly = { "DALA_829", "DALA_832", "DALA_833", "DALA_859", "DALA_863" };
string[] tokens = { "DALA_829t", "DALA_832t", "DALA_833t", "DALA_833t2", "DALA_833t3", "DALA_833t4" };
Guard("S", () =>
{
    Check("S1 the scripted rules: family 01's three and this family's eighteen (DALA_857 is not scripted)", DungeonRunGlobalRules.Scripted.Where(id => id.StartsWith("DALA_")).OrderBy(id => id, StringComparer.Ordinal)
        .SequenceEqual(family.Concat(new[] { "DALA_850", "DALA_864", "DALA_866" }).OrderBy(id => id, StringComparer.Ordinal)) && !DungeonRunGlobalRules.IsScripted("DALA_857"),
        string.Join(",", DungeonRunGlobalRules.Scripted));
    foreach (string id in family)
    {
        bool hooks = hooksOnly.Contains(id);
        string effect = DungeonRunGlobalRules.EffectOf(id);
        Check("S2 " + id + ": the rule card carries the client's text" + (hooks ? " and is hooks only (the client has no player enchantment for it)" : " and its player enchantment is the client's"),
            C(id).Text == FactText(id) && C(id).Implemented && C(id).Type == CardType.SPELL
            && (hooks ? effect == null && !factById.ContainsKey(id + "e") : effect == id + "e" && C(effect).Implemented && C(effect).Type == CardType.ENCHANTMENT && C(effect).Name.EndsWith("Player Enchantment")),
            C(id).Text);
    }
    Check("S3 the tokens are scripted client cards; the prison and the Carts are permanents", tokens.All(id => C(id).Implemented && C(id).Type == CardType.MINION && factById.ContainsKey(id))
        && C("DALA_832t").Untouchable && DungeonRunEncounters.Carts.All(id => C(id).Untouchable) && !C("DALA_829t").Untouchable && C("DALA_829t").Text == FactText("DALA_829t"));
    Check("S4 what each hooked rule creates is listed for the validator", DungeonRunGlobalRules.CardsOf("DALA_829").SequenceEqual(new[] { "DALA_829t" }) && DungeonRunGlobalRules.CardsOf("DALA_832").SequenceEqual(new[] { "DALA_832t" })
        && DungeonRunGlobalRules.CardsOf("DALA_833").Count == 4 && DungeonRunGlobalRules.CardsOf("DALA_863").SequenceEqual(new[] { "UNG_856" }) && DungeonRunGlobalRules.CardsOf("DALA_851").Count == 0
        && DungeonRunGlobalRules.HasStart("DALA_833") && !DungeonRunGlobalRules.HasStart("DALA_859") && !DungeonRunGlobalRules.HasStart("DALA_851")
        && DungeonRunGlobalRules.OpeningCards(new[] { "DALA_859", "DALA_851" }) == 2 && DungeonRunGlobalRules.OpeningCards(null) == 0);
    var two = Plan("DALA_833"); two.GlobalRules.Add("DALA_865");
    var g = Ready(two);
    Check("S5 a twist and an anomaly together: both active on both players, each doing its part (the Carts gain nothing)", Both(g, "DALA_833") && Both(g, "DALA_865") && g.Player1.BoardZone.Count == 3
        && g.Player1.BoardZone.All(m => Ench(m, "DALA_865e2") == 0) && Ench(Board(g, g.Player1, "CS2_182"), "DALA_865e2") == 1);
    var plain = Ready(Plan());
    Check("S6 a plan without rules has no encounter rules, no mark and no start block", plain.EncounterOrNull == null && plain[EncounterRules.RuleApplied] == 0 && plain[EncounterRules.StartDone] == 0);
    bool refused = false;
    try { Start(Plan("DALA_857")); } catch (ArgumentException) { refused = true; }
    Check("S7 Configure refuses the unscripted rule DALA_857", refused);
    var clone = Ready(Plan("DALA_832")).Clone();
    Check("S8 a clone keeps a hooked rule's mark and start state", DungeonRunGlobalRules.IsActive(clone.Player1, "DALA_832") && clone[EncounterRules.StartDone] == 1 && clone.Player1.BoardZone.Count == 1);
    Check("S9 the family's private tags are inside the encounter range (never sent to the client)", EncounterRules.IsPrivateTag(EncounterRules.RuleApplied) && EncounterRules.IsPrivateTag(EncounterRules.RuleSpells)
        && (int)EncounterRules.RuleApplied >= EncounterRules.RuleTagsFrom && (int)EncounterRules.RuleSpells <= EncounterRules.RuleTagsTo);
});

// ============================================================ T. the run layer
Guard("T", () =>
{
    string dataRoot = Path.Combine(repo, "data");
    string R(string f) => File.ReadAllText(Path.Combine(dataRoot, f));
    DungeonRunData Load(string schedule = null) => DungeonRunData.Parse(R("dungeon-run/run-v1.json"), R("dungeon-run/pool-v1.json"), R("npc/decks-v1.json"), R("dungeon-run/catalog-v1.json"),
        schedule ?? R("dungeon-run/schedule-v1.json"), R("dungeon-run/tavern-v1.json"), R("dungeon-run/encounters-v1.json"));
    var data = Load();
    var run = new DungeonRun(data);
    DungeonRunProfile All(DungeonRunData d) { var p = new DungeonRunProfile(); p.Unlocked.AddRange(d.Unlocks.Keys); return p; }
    DungeonRunState V3(DungeonRun r, string cls, long seed, string schedule = "acts-v3h")
    {
        var c = r.Data.Class(cls);
        return r.NewRun(All(r.Data), c.Id, c.Decks[0], c.Powers[0], seed, schedule: schedule);
    }
    var errors = DungeonRunValidator.Validate(data, null);
    Check("T1 the shipped data validates with the family's rule rows", errors.Count == 0, string.Join(" | ", errors.Take(4)));
    var rules = data.Schedules.Rules;
    Check("T2 schedule-v1.json lists the 18 anomalies and the 4 twists of the Dalaran Heist", rules.Count(r => r.Provenance == "DALA" && r.Kind == "anomaly") == 18 && rules.Count(r => r.Provenance == "DALA" && r.Kind == "twist") == 4
        && rules.Where(r => r.Provenance == "DALA" && r.Kind == "twist").Select(r => r.Id).SequenceEqual(new[] { "DALA_829", "DALA_831", "DALA_832", "DALA_833" }));
    var glittering = data.Schedules.Rule("DALA_857");
    Check("T3 DALA_857 Glittering is rejected with its reason, and the admission table rejects it too", !glittering.Admitted && glittering.Reason.Contains("treasures")
        && !DungeonRunAdmission.Classify(null, "DALA_857", AdmitScope.Rule, null, true).Admitted && DungeonRunAdmission.RuleFor("DALA_857").Lift == DungeonRunAdmission.RuleTreasureLift);
    Check("T4 every scripted rule of the family is admitted as a rule only and never generated", family.All(id => DungeonRunAdmission.Classify(null, id, AdmitScope.Rule, null, true).Admitted
        && !DungeonRunAdmission.Classify(null, id, AdmitScope.Player, null, true).Admitted && !DungeonRunAdmission.Classify(null, id, AdmitScope.Boss, null, true).Admitted && rejects.Contains(id)));
    Check("T5 the tokens are admitted for a rule only and never generated", tokens.All(id => DungeonRunAdmission.Classify(null, id, AdmitScope.Rule, null, true).Admitted
        && !DungeonRunAdmission.Classify(null, id, AdmitScope.Boss, null, true).Admitted && rejects.Contains(id)));
    // The anomaly offer: every admitted anomaly can be rolled, the rejected one and the twists never.
    var offered = new HashSet<string>();
    for (long seed = 1; seed <= 400; seed++)
    {
        var s = V3(run, "MAGE", 50000 + seed);
        if (s.ModifierOffer?.Kind == "anomaly") foreach (string id in s.ModifierOffer.Offered) offered.Add(id);
    }
    var anomalies = rules.Where(r => r.Provenance == "DALA" && r.Kind == "anomaly" && r.Admitted).Select(r => r.Id).ToList();
    Check("T6 over 400 seeds the act I anomaly node offers each of the 17 admitted anomalies, never DALA_857 and never a twist", anomalies.Count == 17 && anomalies.All(offered.Contains)
        && !offered.Contains("DALA_857") && !offered.Any(id => data.Schedules.Rule(id)?.Kind == "twist"), string.Join(",", anomalies.Where(id => !offered.Contains(id))));
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
    var voone = WithBoss("MAGE", 61001, "trl_200_voone", out int vooneAct);
    var sch = run.ScheduleFor(voone);
    var twistsVoone = run.RuleCandidates(voone, sch, vooneAct, "twist").Select(r => r.Id).ToList();
    Check("T7 an act with a shrine boss (Voone) never gets The Stench or The Carts; The Coffers and The Prisons stay possible", !twistsVoone.Contains("DALA_831") && !twistsVoone.Contains("DALA_833")
        && twistsVoone.Contains("DALA_829") && twistsVoone.Contains("DALA_832"), string.Join(",", twistsVoone));
    var icarax = WithBoss("MAGE", 61002, "ulda_40_icarax", out int icaraxAct);
    Check("T8 an act with a deck-replacing boss (Icarax) is never offered Hallucinating; another act is", !run.RuleCandidates(icarax, run.ScheduleFor(icarax), icaraxAct, "anomaly").Any(r => r.Id == "DALA_863")
        && run.RuleCandidates(icarax, run.ScheduleFor(icarax), icaraxAct == 1 ? 2 : 1, "anomaly").Any(r => r.Id == "DALA_863"), "Icarax act " + icaraxAct);
    // A fixture boss flag: doubleBattlecry excludes Crying, doubleDeathrattle Rattling, recast Wild Magic, forcedAttack Ogreish, boardStart the board twists.
    var flagged = Load();
    var custom = flagged.Bosses.First(b => !b.IsEncounter && b.Band == 1 && b.Unlock == null);
    custom.Flags.AddRange(new[] { "doubleBattlecry", "doubleDeathrattle", "recast", "forcedAttack", "boardStart" });
    var fr = new DungeonRun(flagged);
    var fs = V3(fr, "MAGE", 61003);
    fs.BossSequence[0] = custom.Id;
    var left = fr.RuleCandidates(fs, fr.ScheduleFor(fs), 1, "anomaly").Select(r => r.Id).ToList();
    var leftTwists = fr.RuleCandidates(fs, fr.ScheduleFor(fs), 1, "twist").Select(r => r.Id).Where(id => id.StartsWith("DALA_")).ToList();
    Check("T9 a boss with the same rule as its power excludes the rule: Crying, Rattling, Wild Magic and Ogreish by flag, every board twist by boardStart",
        !left.Contains("DALA_852") && !left.Contains("DALA_853") && !left.Contains("DALA_856") && !left.Contains("DALA_861") && left.Contains("DALA_851") && left.Contains("DALA_867")
        && leftTwists.Count == 0, string.Join(",", left) + " | " + string.Join(",", leftTwists));
    // The run itself: a board shrine, Darius Crowley's Cannons.
    var shrineDeck = data.Decks.First(x => x.Shrine != null && data.FindClass(x.Class) != null);
    var shrineRun = run.NewRun(All(data), shrineDeck.Class, shrineDeck.Id, data.Class(shrineDeck.Class).Powers[0], 61004, schedule: "acts-v3h");
    foreach (int f in Enumerable.Range(0, 5)) shrineRun.BossSequence[f] = data.Bosses.Where(b => !b.IsEncounter && b.Band == 1 && b.Unlock == null).ElementAt(f % 3).Id;
    var shrineTwists = run.RuleCandidates(shrineRun, run.ScheduleFor(shrineRun), 1, "twist").Select(r => r.Id).ToList();
    Check("T10 a run with a board shrine never gets The Stench or The Carts", shrineRun.HasBoardShrine && !shrineTwists.Contains("DALA_831") && !shrineTwists.Contains("DALA_833") && shrineTwists.Contains("DALA_832"),
        string.Join(",", shrineTwists));
    var darius = V3(run, "GILA_600h", 61005);
    foreach (int f in Enumerable.Range(0, 5)) darius.BossSequence[f] = data.Bosses.Where(b => !b.IsEncounter && b.Band == 1 && b.Unlock == null).ElementAt(f % 3).Id;
    var dariusTwists = run.RuleCandidates(darius, run.ScheduleFor(darius), 1, "twist").Select(r => r.Id).Where(id => id.StartsWith("DALA_")).ToList();
    var mage = V3(run, "MAGE", 61005);
    foreach (int f in Enumerable.Range(0, 5)) mage.BossSequence[f] = data.Bosses.Where(b => !b.IsEncounter && b.Band == 1 && b.Unlock == null).ElementAt(f % 3).Id;
    var mageTwists = run.RuleCandidates(mage, run.ScheduleFor(mage), 1, "twist").Select(r => r.Id).Where(id => id.StartsWith("DALA_")).ToList();
    Check("T11 Darius Crowley (his Cannons take board slots) never gets The Carts; a Mage run may get all four twists", !dariusTwists.Contains("DALA_833") && dariusTwists.Contains("DALA_831")
        && mageTwists.SequenceEqual(new[] { "DALA_829", "DALA_831", "DALA_832", "DALA_833" }), string.Join(",", dariusTwists) + " | " + string.Join(",", mageTwists));
    // Rule against rule, both ways.
    var active = V3(run, "MAGE", 61006);
    foreach (int f in Enumerable.Range(0, 5)) active.BossSequence[f] = data.Bosses.Where(b => !b.IsEncounter && b.Band == 1 && b.Unlock == null).ElementAt(f % 3).Id;
    active.ActiveRules.Add(new DungeonRunRule { Id = "DALA_833", Act = 1, FromNode = -1, Kind = "twist" });
    var beside = run.RuleCandidates(active, run.ScheduleFor(active), 1, "twist").Select(r => r.Id).ToList();
    Check("T12 the board twists exclude one another, whichever row states it (The Carts active: no Coffers, no Prisons)", !beside.Contains("DALA_829") && !beside.Contains("DALA_832") && !beside.Contains("DALA_833"),
        string.Join(",", beside));
    Check("T12b an anomaly may share the act with a twist", run.RuleCandidates(active, run.ScheduleFor(active), 1, "anomaly").Any(r => r.Id == "DALA_865"));
    // The validator's checks on the new keys.
    string Refused(Action<DungeonRunData> change)
    {
        var d = Load(); change(d);
        return string.Join(" | ", DungeonRunValidator.Validate(d, null));
    }
    Check("T13 the validator refuses an unknown class in incompatibleClasses and an unknown flag", Refused(d => d.Schedules.Rule("DALA_833").IncompatibleClasses.Add("NOBODY")).Contains("incompatibleClasses names unknown class NOBODY")
        && Refused(d => d.Schedules.Rule("DALA_852").IncompatibleFlags.Add("twice")).Contains("unknown boss flag twice"));
    Check("T14 the validator refuses DALA_857 as an admitted rule (the table rejects it and it has no script)", Refused(d => d.Schedules.Rule("DALA_857").Status = "admitted").Contains("rule DALA_857"));
    var known = new HashSet<string>(factById.Keys, StringComparer.Ordinal); known.Remove("DALA_833t3");
    var clientErrors = DungeonRunValidator.Validate(data, id => known.Contains(id));
    Check("T15 the validator checks that the client knows each card a rule creates", clientErrors.Any(e => e.StartsWith("DALA_833t3:") && e.Contains("rule DALA_833")), string.Join(" | ", clientErrors.Take(3)));
    // The twist roll through a fixture schedule (the shipped acts have empty twist pools: the close-out fills them).
    var fixture = Load(File.ReadAllText(Path.Combine(fixtures, "schedule-twists.json")));
    var fixtureErrors = DungeonRunValidator.Validate(fixture, null);
    var tr = new DungeonRun(fixture);
    var rolledIds = new Dictionary<string, int>(); int runs = 0, withShrineBoss = 0, wrong = 0;
    for (long seed = 1; seed <= 300; seed++)
    {
        var s = V3(tr, "MAGE", 70000 + seed);
        runs++;
        foreach (var rule in s.ActiveRules.Where(r => r.Kind == "twist"))
        {
            rolledIds[rule.Id] = rolledIds.TryGetValue(rule.Id, out int n) ? n + 1 : 1;
            var bosses = tr.ScheduleFor(s).Nodes.Where(n => n.Act == rule.Act && n.IsFight).Select(n => fixture.Boss(s.BossSequence[n.Floor - 1])).ToList();
            if (bosses.Any(b => b.HasFlag("shrine"))) { withShrineBoss++; if (rule.Id == "DALA_831" || rule.Id == "DALA_833") wrong++; }
        }
    }
    // Phase 8 family 13: nine Rumble Run rows carry the shrine flag, which keeps DALA_831 and DALA_833 out of more acts (20 and 25 of 300 runs).
    Check("T16 the fixture schedule (twist pools filled, chance 100) validates and rolls each of the four twists", fixtureErrors.Count == 0 && rolledIds.Count == 4 && rolledIds.Values.All(n => n >= 15),
        string.Join(" | ", fixtureErrors.Take(2)) + string.Join(",", rolledIds.Select(p => p.Key + "x" + p.Value)));
    Check("T17 a rolled twist is never one a boss of its act excludes (" + withShrineBoss + " acts with a shrine boss)", wrong == 0 && withShrineBoss > 0);
    var first = V3(tr, "MAGE", 70001);
    var twist = first.ActiveRules.First(r => r.Kind == "twist" && r.Act == 1);
    if (first.Phase == DungeonRunPhase.Modifier) tr.PickModifier(first, DungeonRunGlobalRule.None);
    first.BossSequence[0] = fixture.Bosses.First(b => !b.IsEncounter && b.Band == 1 && b.Unlock == null).Id;
    var plan = tr.BuildPlan(first, All(fixture));
    Check("T18 the rolled twist is on the fight's plan and plays (" + twist.Id + ")", plan.GlobalRules.SequenceEqual(new[] { twist.Id }) && DungeonRunGlobalRules.IsScripted(twist.Id));
    Check("T19 the rule page shows each rule card's own name and text", family.All(id => NativeDungeonRunText.RuleText(id) == DungeonRunValidator.MatchText(C(id).Text) && NativeDungeonRunText.RuleText(id).Length > 0
        && NativeDungeonRunText.RuleName(id) == C(id).Name && (C(id).Name.StartsWith("Anomaly - ") || C(id).Name.StartsWith("Twist - "))));
});

Console.WriteLine($"RESULT {passes} passed, {failures} failed");
return failures;
