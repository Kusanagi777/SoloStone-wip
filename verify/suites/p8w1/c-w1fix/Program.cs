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

// Phase 8 wave 1 integration (families 02, 03, 04): the fixes the merged tree needed, asserted in real engine games through
// the run setup path (DungeonRunSetup.Configure / ApplyAfterStart). Built by p8w1\make_w1fix.py (helpers: the preamble of
// p8f02\suites\c-collectibles-A).
//   W1-W4  FP1_009 Deathlord: the minion its Deathrattle summons leaves the opponent's deck. Before the fix the entity stayed
//          listed in the deck while on the board; the released act bosspilot mode met it once the regenerated catalog changed
//          the drafts (hollow_sovereign, floor 15: ArgumentException "same key" in the fair search, then a
//          SerializationException "Invalid history residence links" when the boss answered a Rewind).
//   W5-W6  CFM_668 Doppelgangster's Battlecry repeated by GIL_820 Shudderwock: 2 copies of the repeater. It threw
//          NotImplementedException (released shrine S3 bosspilot mode, sleepless_one against the Shaman row, floor 15).
//   W7-W9  LOOT_210 Sudden Betrayal answers an enemy minion's attack only. A hero's attack on its owner's hero threw
//          "Can't obtain adjacent minions of non-minion" (released teammates T2 bosspilot mode, sleepless_one, floor 15).
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

string[] ids =
{
    "AT_027", "AT_079", "AT_081", "AT_115", "BOT_436", "DAL_573", "DAL_729", "DAL_731", "EX1_112", "GIL_655", "GVG_017", "GVG_025", "GVG_050",
    "GVG_054", "GVG_097", "GVG_107", "GVG_108", "GVG_111", "GVG_113", "GVG_119", "LOE_009", "LOE_022", "LOE_029", "LOE_047", "LOE_053", "LOE_061",
    "LOE_092", "LOE_107", "LOE_119", "TRL_318", "DAL_431", "GVG_014", "GVG_049",
};
string[] support =
{
    "AT_027e", "AT_081e", "AT_115e", "BOT_436e", "DAL_431t", "Mekka1", "Mekka2", "Mekka3", "Mekka3e", "Mekka4", "Mekka4t", "GIL_655e", "GVG_014a",
    "GVG_049e", "GVG_101e", "GVG_111t", "LOE_009t", "LOE_061e", "LOEA16_3", "LOEA16_3e", "LOEA16_4", "LOEA16_5", "LOEA16_5t",
};

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

// ============================================================ W1 the run player's Deathlord dies: a minion leaves the boss's deck
Guard("W1", () =>
{
    var g = Ready(Plan());
    var p1 = g.Player1; var p2 = g.Player2;
    var lord = Board(g, p1, "FP1_009");
    int deck = p2.DeckZone.Count, board = p2.BoardZone.Count;
    lord.Destroy(); g.DeathProcessingAndAuraUpdate();
    var summoned = p2.BoardZone.LastOrDefault();
    Check("W1 Deathlord (run player's): one minion of the boss's deck is on the boss's board", p2.BoardZone.Count == board + 1 && summoned != null && summoned.Card.Id == yeti,
        $"board {board}->{p2.BoardZone.Count}");
    Check("W1 the boss's deck is one card shorter and no longer lists the summoned minion", p2.DeckZone.Count == deck - 1 && summoned != null && !p2.DeckZone.Any(c => c.Id == summoned.Id),
        $"deck {deck}->{p2.DeckZone.Count}");
    Check("W1 no entity is listed in two zones", Doubled(g).Count == 0, string.Join("; ", Doubled(g)));
    var clone = g.CloneExact(history: true);
    Check("W1 an exact clone carries the same zones", clone.Player2.DeckZone.Count == deck - 1 && Doubled(clone).Count == 0);
    // Through player tasks only (a snapshot needs a game that was only moved by tasks): Deathlord played from hand and
    // destroyed by Naturalize; the snapshot's load validates the history's residence links (the check that faulted).
    var g2 = Ready(Plan());
    var q1 = g2.Player1; var q2 = g2.Player2;
    bool played = PlayNew(g2, "FP1_009");
    var lord2 = q1.BoardZone.FirstOrDefault(m => m.Card.Id == "FP1_009");
    int deck2 = q2.DeckZone.Count;
    bool cast = lord2 != null && PlayNew(g2, "EX1_161", lord2);
    Game loaded = null; string error = "";
    try { loaded = Game.LoadSnapshot(g2.SaveSnapshot()); } catch (Exception e) { error = e.GetType().Name + ": " + e.Message.Split('\n')[0]; }
    // Naturalize: the boss also draws 2 cards, so its deck is 3 cards shorter.
    Check("W1 played and destroyed by tasks: the game saves and loads as a snapshot (the history's residence links validate)",
        played && cast && q2.BoardZone.Count == 1 && q2.DeckZone.Count == deck2 - 3 && Doubled(g2).Count == 0 && loaded != null && loaded.Player2.BoardZone.Count == 1 && loaded.Player2.DeckZone.Count == deck2 - 3,
        $"played {played} cast {cast} board {q2.BoardZone.Count} deck {deck2}->{q2.DeckZone.Count} {error}");
});

// ============================================================ W2 the boss's Deathlord dies: a minion leaves the run player's deck
Guard("W2", () =>
{
    var g = Ready(Plan());
    var p1 = g.Player1; var p2 = g.Player2;
    var lord = Board(g, p2, "FP1_009");
    int deck = p1.DeckZone.Count, board = p1.BoardZone.Count;
    lord.Destroy(); g.DeathProcessingAndAuraUpdate();
    var summoned = p1.BoardZone.LastOrDefault();
    Check("W2 Deathlord (boss's): one minion of the run player's deck is on the run player's board, the deck one card shorter",
        p1.BoardZone.Count == board + 1 && p1.DeckZone.Count == deck - 1 && summoned != null && !p1.DeckZone.Any(c => c.Id == summoned.Id), $"board {board}->{p1.BoardZone.Count} deck {deck}->{p1.DeckZone.Count}");
    Check("W2 no entity is listed in two zones", Doubled(g).Count == 0, string.Join("; ", Doubled(g)));
});

// ============================================================ W3 a full opposing board, a deck without minions
Guard("W3", () =>
{
    var g = Ready(Plan());
    var p1 = g.Player1; var p2 = g.Player2;
    var lord = Board(g, p1, "FP1_009");
    while (!p2.BoardZone.IsFull) Board(g, p2, wisp);
    int deck = p2.DeckZone.Count;
    lord.Destroy(); g.DeathProcessingAndAuraUpdate();
    Check("W3 a full opposing board: nothing leaves the deck", p2.DeckZone.Count == deck && p2.BoardZone.Count == 7 && Doubled(g).Count == 0, $"deck {deck}->{p2.DeckZone.Count}");
    // Arcane Missiles x30: no minion in the boss's deck.
    var g2 = Ready(Plan(bossDeck: Deck(("EX1_277", 30))));
    var lord2 = Board(g2, g2.Player1, "FP1_009");
    int deck2 = g2.Player2.DeckZone.Count;
    lord2.Destroy(); g2.DeathProcessingAndAuraUpdate();
    Check("W3 a deck without minions: nothing is summoned", g2.Player2.DeckZone.Count == deck2 && g2.Player2.BoardZone.Count == 0 && Doubled(g2).Count == 0);
});

// ============================================================ W4 two Deathlords in one turn, then play goes on
Guard("W4", () =>
{
    var g = Ready(Plan());
    var p1 = g.Player1; var p2 = g.Player2;
    var first = Board(g, p1, "FP1_009"); var second = Board(g, p1, "FP1_009");
    int deck = p2.DeckZone.Count;
    first.Destroy(); g.DeathProcessingAndAuraUpdate();
    second.Destroy(); g.DeathProcessingAndAuraUpdate();
    var ids2 = p2.BoardZone.Select(m => m.Id).ToList();
    Check("W4 two Deathlords: two different minions of the boss's deck are on its board", p2.BoardZone.Count == 2 && ids2.Distinct().Count() == 2 && p2.DeckZone.Count == deck - 2, $"deck {deck}->{p2.DeckZone.Count}");
    EndTurn(g); EndTurn(g);
    Check("W4 the next two turns draw from the shorter decks and no entity is listed in two zones", Doubled(g).Count == 0 && p2.DeckZone.Count == deck - 3, $"deck {p2.DeckZone.Count}; " + string.Join("; ", Doubled(g)));
});

// ============================================================ W5 Shudderwock repeats Doppelgangster's Battlecry
Guard("W5", () =>
{
    var g = Ready(Plan(playerHero: "HERO_02", playerPower: "CS2_049"));
    var p1 = g.Player1;
    bool gang = PlayNew(g, "CFM_668");
    Check("W5 Doppelgangster played: itself and its two copies", gang && p1.BoardZone.Count == 3 && p1.BoardZone.Select(m => m.Card.Id).OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(new[] { "CFM_668", "CFM_668t", "CFM_668t2" }),
        string.Join(",", p1.BoardZone.Select(m => m.Card.Id)));
    bool wock = false; string error = "";
    try { wock = PlayNew(g, "GIL_820"); } catch (Exception e) { error = e.GetType().Name + ": " + e.Message.Split('\n')[0]; }
    var wocks = p1.BoardZone.Where(m => m.Card.Id == "GIL_820").ToList();
    Check("W5 Shudderwock repeats it: 2 copies of Shudderwock (three on the board), no exception", wock && error == "" && wocks.Count == 3 && p1.BoardZone.Count == 6 && wocks.All(m => m.AttackDamage == 6 && m.Health == 6),
        $"{error} board {string.Join(",", p1.BoardZone.Select(m => m.Card.Id + " " + m.AttackDamage + "/" + m.Health))}");
    Check("W5 the game goes on (the boss's turn, then the run player's)", g.State != State.COMPLETE && Doubled(g).Count == 0);
    EndTurn(g); EndTurn(g);
    Check("W5 after a round the board still holds the three Shudderwocks", p1.BoardZone.Count(m => m.Card.Id == "GIL_820") == 3 && g.CurrentPlayer == p1);
});

// ============================================================ W6 Shudderwock with room for one copy only, and none
Guard("W6", () =>
{
    var g = Ready(Plan(playerHero: "HERO_02", playerPower: "CS2_049"));
    var p1 = g.Player1;
    PlayNew(g, "CFM_668");                           // 3 on the board
    Board(g, p1, wisp); Board(g, p1, wisp);          // 5
    bool wock = PlayNew(g, "GIL_820");               // 6: room for one copy
    Check("W6 room for one copy: exactly one more Shudderwock, a full board", wock && p1.BoardZone.Count == 7 && p1.BoardZone.Count(m => m.Card.Id == "GIL_820") == 2,
        string.Join(",", p1.BoardZone.Select(m => m.Card.Id)));
    var g2 = Ready(Plan(playerHero: "HERO_02", playerPower: "CS2_049"));
    var q1 = g2.Player1;
    PlayNew(g2, "CFM_668"); Board(g2, q1, wisp); Board(g2, q1, wisp); Board(g2, q1, wisp);   // 6
    bool wock2 = PlayNew(g2, "GIL_820");             // 7: no room
    Check("W6 no room: Shudderwock alone, no copy, no exception", wock2 && q1.BoardZone.Count == 7 && q1.BoardZone.Count(m => m.Card.Id == "GIL_820") == 1);
});

// ============================================================ W7 Sudden Betrayal and a hero's attack
Guard("W7", () =>
{
    var g = Ready(Plan(playerHero: "HERO_03", playerPower: "CS2_083b"));
    var p1 = g.Player1; var p2 = g.Player2;
    bool secret = PlayNew(g, "LOOT_210");
    ToBoss(g);
    Board(g, p2, yeti); Board(g, p2, yeti);          // neighbours exist: the old script reached IncludeAdjacentTask with the hero
    bool axe = PlayNew(g, "CS2_106");                // Fiery War Axe 3/2 for the boss's hero
    int hp = p1.Hero.Health; bool attacked = false; string error = "";
    try { attacked = g.Process(HeroAttackTask.Any(p2, p1.Hero)); } catch (Exception e) { error = e.GetType().Name + ": " + e.Message.Split('\n')[0]; }
    Check("W7 Sudden Betrayal in play, the boss's hero attacks the run player's hero: the attack lands, no exception",
        secret && axe && attacked && error == "" && p1.Hero.Health == hp - 3, $"secret {secret} axe {axe} attacked {attacked} {error} health {hp}->{p1.Hero.Health}");
    Check("W7 the Secret did not trigger (a hero is not a minion): still in play", Secrets(p1).SequenceEqual(new[] { "LOOT_210" }), string.Join(",", Secrets(p1)));
});

// ============================================================ W8 Sudden Betrayal and an enemy minion with a neighbour
Guard("W8", () =>
{
    var g = Ready(Plan(playerHero: "HERO_03", playerPower: "CS2_083b"));
    var p1 = g.Player1; var p2 = g.Player2;
    PlayNew(g, "LOOT_210");
    ToBoss(g);
    var left = Board(g, p2, yeti); var right = Board(g, p2, yeti);
    int hp = p1.Hero.Health;
    bool attacked = Attack(g, left, p1.Hero);
    Check("W8 an enemy minion with a neighbour attacks the run player's hero: it attacks the neighbour instead, the Secret is spent",
        attacked && p1.Hero.Health == hp && left.Damage == 4 && right.Damage == 4 && Secrets(p1).Count == 0, $"health {hp}->{p1.Hero.Health} left -{left.Damage} right -{right.Damage} secrets {Secrets(p1).Count}");
});

// ============================================================ W9 Sudden Betrayal and a lone enemy minion; the boss's own Secret
Guard("W9", () =>
{
    var g = Ready(Plan(playerHero: "HERO_03", playerPower: "CS2_083b"));
    var p1 = g.Player1; var p2 = g.Player2;
    PlayNew(g, "LOOT_210");
    ToBoss(g);
    var lone = Board(g, p2, yeti);
    int hp = p1.Hero.Health;
    bool attacked = Attack(g, lone, p1.Hero);
    Check("W9 a lone enemy minion attacks the hero: no neighbour, the attack lands and the Secret stays", attacked && p1.Hero.Health == hp - 4 && Secrets(p1).SequenceEqual(new[] { "LOOT_210" }),
        $"health {hp}->{p1.Hero.Health}");
    // The boss holds the Secret; the run player's minions and hero attack the boss's hero.
    var g2 = Ready(Plan(playerHero: "HERO_01", playerPower: "CS2_102"));
    var q1 = g2.Player1; var q2 = g2.Player2;
    ToBoss(g2);
    bool bossSecret = PlayNew(g2, "LOOT_210");
    ToPlayer(g2);
    var a = Board(g2, q1, yeti); var b = Board(g2, q1, yeti);
    PlayNew(g2, "CS2_106");
    int bossHp = q2.Hero.Health; string error = "";
    bool heroAttack = false;
    try { heroAttack = g2.Process(HeroAttackTask.Any(q1, q2.Hero)); } catch (Exception e) { error = e.GetType().Name + ": " + e.Message.Split('\n')[0]; }
    Check("W9 the boss's Sudden Betrayal: the run player's hero attack lands and leaves the Secret", bossSecret && heroAttack && error == "" && q2.Hero.Health == bossHp - 3 && Secrets(q2).Count == 1,
        $"{error} boss health {bossHp}->{q2.Hero.Health} secrets {Secrets(q2).Count}");
    bool minionAttack = Attack(g2, a, q2.Hero);
    Check("W9 the run player's minion with a neighbour attacks the boss's hero: it hits the neighbour, the boss's Secret is spent",
        minionAttack && q2.Hero.Health == bossHp - 3 && a.Damage == 4 && b.Damage == 4 && Secrets(q2).Count == 0, $"boss health {q2.Hero.Health} a -{a.Damage} b -{b.Damage}");
});

Console.WriteLine($"RESULT {passes} passed, {failures} failed");
return failures;
