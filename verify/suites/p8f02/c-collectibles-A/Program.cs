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

// Phase 8 family 02, unscripted collectibles A (33 ids): each card's text asserted in real engine games through the run setup
// path (DungeonRunSetup.Configure / ApplyAfterStart), on the run player's side and, where the boss side differs, on the
// boss's. The bosspilot fixture mode (p8f02\run.sh bp) plays the same cards with the fair AI as the boss.
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

// ============================================================ A0 definitions
Guard("definitions", () =>
{
    var unscripted = ids.Concat(support).Where(id => C(id) == null || !C(id).Implemented).ToList();
    Check("A0 the 33 cards and the 23 tokens and enchantments they use are scripted", unscripted.Count == 0, string.Join(",", unscripted));
    Check("A0b the 33 cards are collectible and every one is a card of the legacy pools already (no pool gained a member)", ids.All(id => C(id).Collectible));
});

// ============================================================ AT_027 Wilfred Fizzlebang
Guard("AT_027", () =>
{
    var g = Ready(Plan(playerHero: "HERO_07", playerPower: "CS2_056"));
    var p = g.Player1;
    Board(g, p, "AT_027");
    int before = p.HandZone.Count;
    bool used = Power(g);
    var drawn = p.HandZone[p.HandZone.Count - 1];
    Check("AT_027 a card drawn by its owner's Hero Power (Life Tap) costs (0)", used && p.HandZone.Count == before + 1 && drawn.Card.Id == yeti && drawn.Cost == 0, drawn.Card.Id + " cost " + drawn.Cost);
    int count = p.HandZone.Count;
    PlayNew(g, "CS2_023"); // Arcane Intellect: draw 2
    var later = p.HandZone.Skip(count).ToList();
    Check("AT_027 cards drawn by a spell keep their Cost", later.Count == 2 && later.All(x => x.Cost == 4), string.Join(",", later.Select(x => x.Cost)));
    Round(g);
    Check("AT_027 the turn's draw keeps its Cost", p.HandZone[p.HandZone.Count - 1].Cost == 4);
    // The opponent's Wilfred does nothing for this player's Hero Power.
    var g2 = Ready(Plan(playerHero: "HERO_07", playerPower: "CS2_056"));
    Board(g2, g2.Player2, "AT_027");
    Power(g2);
    Check("AT_027 an enemy Wilfred does not discount the draw", g2.Player1.HandZone[g2.Player1.HandZone.Count - 1].Cost == 4);
});

// ============================================================ AT_079 Mysterious Challenger
Guard("AT_079", () =>
{
    var g = Ready(Plan(bossDeck: Deck(("EX1_610", 2), ("EX1_611", 1), ("EX1_554", 1), (yeti, 26))));
    var boss = g.Player2;
    ToBoss(g);
    SecretsToDeck(boss);
    int deck = boss.DeckZone.Count;
    bool played = PlayNew(g, "AT_079");
    Check("AT_079 (boss) one of each Secret of its deck enters play, live; the second copy stays in the deck", played && Secrets(boss).OrderBy(x => x).SequenceEqual(new[] { "EX1_554", "EX1_610", "EX1_611" })
        && boss.DeckZone.Count == deck - 3 && boss.DeckZone.Count(c => c.Card.Id == "EX1_610") == 1 && boss.SecretZone.All(s => s.ActivatedTrigger != null),
        string.Join(",", Secrets(boss)) + " deck " + boss.DeckZone.Count + "/" + deck);
    ToPlayer(g);
    var attacker = Board(g, g.Player1, yeti);
    int hero = g.Player1.Hero.Health;
    Attack(g, attacker, boss.Hero);
    Check("AT_079 the Secrets it put into play trigger (Explosive Trap answers the attack)", !Secrets(boss).Contains("EX1_610") && g.Player1.Hero.Health == hero - 2, string.Join(",", Secrets(boss)));
    var g2 = Ready(Plan(playerDeck: Deck(("EX1_610", 1), ("EX1_611", 1), ("EX1_554", 1), ("EX1_609", 1), ("LOE_021", 1), ("EX1_533", 1), ("EX1_287", 1), (yeti, 23)), playerHero: "HERO_05", playerPower: "DS1h_292"));
    SecretsToDeck(g2.Player1);
    PlayNew(g2, "AT_079");
    Check("AT_079 (player) more Secrets in the deck than room: five different ones enter play", g2.Player1.SecretZone.Count == 5 && Secrets(g2.Player1).Distinct().Count() == 5, string.Join(",", Secrets(g2.Player1)));
});

// ============================================================ AT_081 Eadric the Pure
Guard("AT_081", () =>
{
    var g = Ready(Plan());
    var a = Board(g, g.Player2, yeti); var b = Board(g, g.Player2, "CS2_186"); // War Golem 7/7
    var own = Board(g, g.Player1, yeti);
    bool played = PlayNew(g, "AT_081");
    Check("AT_081 all enemy minions' Attack becomes 1; friendly minions and Health are untouched", played && a.AttackDamage == 1 && b.AttackDamage == 1 && a.Health == 5 && b.Health == 7 && own.AttackDamage == 4,
        a.AttackDamage + "," + b.AttackDamage + "," + own.AttackDamage);
});

// ============================================================ AT_115 Fencing Coach
Guard("AT_115", () =>
{
    var g = Ready(Plan());
    var p = g.Player1;
    PlayNew(g, "AT_115");
    g.DeathProcessingAndAuraUpdate();
    Check("AT_115 the next Hero Power costs (2) less", p.Hero.HeroPower.Cost == 0, "cost " + p.Hero.HeroPower.Cost);
    ToBoss(g);
    Power(g); // the boss's own power (Dampen Magic) is not the owner's use
    ToPlayer(g);
    Check("AT_115 it waits through the opponent's Hero Power use", p.Hero.HeroPower.Cost == 0 && g.Player2.SecretZone.Count == 1, "cost " + p.Hero.HeroPower.Cost);
    bool used = Power(g, g.Player2.Hero);
    g.DeathProcessingAndAuraUpdate();
    Check("AT_115 after the owner's use the Hero Power costs (2) again", used && p.Hero.HeroPower.Cost == 2, "cost " + p.Hero.HeroPower.Cost);
});

// ============================================================ BOT_436 Prismatic Lens
Guard("BOT_436", () =>
{
    var g = Ready(Plan(playerDeck: Deck((yeti, 15), ("CS2_032", 15)))); // Flamestrike costs 7
    var p = g.Player1;
    ClearHand(p);
    bool played = PlayNew(g, "BOT_436");
    var minion = p.HandZone.FirstOrDefault(x => x is Minion); var spell = p.HandZone.FirstOrDefault(x => x is Spell);
    Check("BOT_436 draws a minion and a spell from the deck and swaps their Costs", played && p.HandZone.Count == 2 && minion != null && spell != null && minion.Cost == 7 && spell.Cost == 4,
        string.Join(",", p.HandZone.Select(x => x.Card.Id + ":" + x.Cost)));
    var g2 = Ready(Plan());
    ClearHand(g2.Player1);
    PlayNew(g2, "BOT_436");
    Check("BOT_436 with no spell in the deck it draws the minion and swaps nothing", g2.Player1.HandZone.Count == 1 && g2.Player1.HandZone[0].Cost == 4);
});

// ============================================================ DAL_573 Commander Rhyssa
Guard("DAL_573", () =>
{
    int Trap(bool rhyssa, bool silenced)
    {
        var g = Ready(Plan());
        var boss = g.Player2;
        ToBoss(g);
        DungeonRunEncounters.PutSecret(boss, C("EX1_610")); // Explosive Trap: 2 damage to all enemies
        if (rhyssa) { var r = Board(g, boss, "DAL_573"); if (silenced) r.Silence(); }
        ToPlayer(g);
        var attacker = Board(g, g.Player1, yeti);
        int before = g.Player1.Hero.Health;
        Attack(g, attacker, boss.Hero);
        return boss.SecretZone.Count == 0 ? before - g.Player1.Hero.Health : -1;
    }
    Check("DAL_573 (boss) its owner's Secret triggers twice: Explosive Trap deals 2 damage twice, then leaves play", Trap(true, false) == 4, "damage " + Trap(true, false));
    Check("DAL_573 without it the Secret triggers once", Trap(false, false) == 2);
    Check("DAL_573 a silenced Rhyssa doubles nothing", Trap(true, true) == 2);
    var g2 = Ready(Plan());
    var p = g2.Player1;
    Board(g2, p, "DAL_573");
    PlayNew(g2, "EX1_130"); // Noble Sacrifice
    ToBoss(g2);
    var enemy = Board(g2, g2.Player2, yeti);
    Attack(g2, enemy, p.Hero);
    Check("DAL_573 (player) Noble Sacrifice triggers twice: two Defenders are summoned and the Secret is revealed once", p.SecretZone.Count == 0 && p.GraveyardZone.Count(x => x.Card.Id == "EX1_130a") + p.BoardZone.Count(x => x.Card.Id == "EX1_130a") == 2,
        "defenders " + (p.GraveyardZone.Count(x => x.Card.Id == "EX1_130a") + p.BoardZone.Count(x => x.Card.Id == "EX1_130a")));
    Check("DAL_573 the enemy's Secrets are not doubled (rule read from the Secret's owner)", EncounterRules.SecretRepeats(g2.Player2) == 1 && EncounterRules.SecretRepeats(p) == 2);
});

// ============================================================ DAL_729 Madame Lazul
Guard("DAL_729", () =>
{
    var g = Ready(Plan(bossDeck: Deck((yeti, 10), ("CS2_186", 10), ("CS2_023", 10))));
    var p = g.Player1; var boss = g.Player2;
    ClearHand(boss);
    foreach (string id in new[] { yeti, "CS2_186", "CS2_023", "CS2_032", yeti }) ToHand(g, boss, id);
    var bossHand = Hand(boss);
    int handBefore = p.HandZone.Count;
    bool played = PlayNew(g, "DAL_729");
    var offers = Offers(g, p).Select(c => c.Id).ToList();
    Check("DAL_729 a Discover among three different cards of the opponent's hand", played && offers.Count == 3 && offers.Distinct().Count() == 3 && offers.All(bossHand.Contains), string.Join(",", offers));
    string picked = offers[0];
    Pick(g, p);
    Check("DAL_729 the picked copy joins its owner's hand; the opponent's hand is unchanged", p.Choice == null && p.HandZone.Count == handBefore + 1 && p.HandZone[p.HandZone.Count - 1].Card.Id == picked && Hand(boss).SequenceEqual(bossHand));
    var g2 = Ready(Plan());
    ClearHand(g2.Player2);
    ToHand(g2, g2.Player2, yeti); ToHand(g2, g2.Player2, yeti);
    PlayNew(g2, "DAL_729");
    Check("DAL_729 a hand with one card name offers that one card", Offers(g2, g2.Player1).Select(c => c.Id).SequenceEqual(new[] { yeti }));
    Pick(g2, g2.Player1);
    var g3 = Ready(Plan());
    ClearHand(g3.Player2);
    bool empty = PlayNew(g3, "DAL_729");
    Check("DAL_729 an empty opponent hand: no Discover", empty && g3.Player1.Choice == null);
    // The boss plays it: it is shown cards of the run player's hand.
    var g4 = Ready(Plan());
    ToBoss(g4);
    var playerHand = Hand(g4.Player1);
    PlayNew(g4, "DAL_729");
    var bossOffers = Offers(g4, g4.Player2).Select(c => c.Id).ToList();
    Check("DAL_729 (boss) the boss is offered copies of the run player's hand cards", bossOffers.Count > 0 && bossOffers.All(playerHand.Contains), string.Join(",", bossOffers));
});

// ============================================================ DAL_731 Duel!
Guard("DAL_731", () =>
{
    var g = Ready(Plan(playerDeck: Deck((yeti, 20), ("CS2_023", 10)), bossDeck: Deck(("CS2_186", 30))));
    var p = g.Player1; var boss = g.Player2;
    int mineDeck = p.DeckZone.Count, theirDeck = boss.DeckZone.Count;
    bool played = PlayNew(g, "DAL_731");
    var mine = p.BoardZone.FirstOrDefault(); var theirs = boss.BoardZone.FirstOrDefault();
    Check("DAL_731 a minion of each player's deck is summoned and they fight (the caster's attacks)", played && p.DeckZone.Count == mineDeck - 1 && boss.DeckZone.Count == theirDeck - 1
        && theirs != null && theirs.Card.Id == "CS2_186" && theirs.Damage == 4 && p.BoardZone.Count == 0 && p.GraveyardZone.Any(x => x.Card.Id == yeti && x.ToBeDestroyed),
        "board " + p.BoardZone.Count + "/" + boss.BoardZone.Count + " damage " + theirs?.Damage);
    var g2 = Ready(Plan(playerDeck: Deck(("CS2_023", 30)), bossDeck: Deck((yeti, 30))));
    PlayNew(g2, "DAL_731");
    Check("DAL_731 a deck with no minion summons none: the other minion arrives and nothing fights", g2.Player1.BoardZone.Count == 0 && g2.Player2.BoardZone.Count == 1 && g2.Player2.BoardZone[0].Damage == 0);
    var g3 = Ready(Plan(bossDeck: Deck(("CS2_186", 30))));
    ToBoss(g3);
    PlayNew(g3, "DAL_731");
    Check("DAL_731 (boss) the boss's minion attacks the run player's", g3.Player2.BoardZone.Count == 1 && g3.Player2.BoardZone[0].Damage == 4 && g3.Player1.BoardZone.Count == 0);
});

// ============================================================ EX1_112 Gelbin Mekkatorque and his inventions
Guard("EX1_112", () =>
{
    var seen = new HashSet<string>();
    for (int seed = 1; seed <= 24; seed++)
    {
        var g = Ready(Plan(), seed, history: false);
        PlayNew(g, "EX1_112");
        var others = g.Player1.BoardZone.Where(m => m.Card.Id != "EX1_112").Select(m => m.Card.Id).ToList();
        if (others.Count == 1) seen.Add(others[0]); else seen.Add("?" + others.Count);
    }
    Check("EX1_112 summons one AWESOME invention; over 24 seeds all four appear", seen.SetEquals(DungeonRunEncounters.Inventions), string.Join(",", seen.OrderBy(x => x)));
    // Homing Chicken
    var g1 = Ready(Plan());
    var chicken = Board(g1, g1.Player1, "Mekka1");
    Round(g1);
    int hand = g1.Player1.HandZone.Count;
    Check("Mekka1 Homing Chicken: at the start of its owner's turn it is destroyed and 3 cards are drawn", chicken.Zone != g1.Player1.BoardZone && g1.Player1.NumCardsDrawnThisTurn == 4, "drawn " + g1.Player1.NumCardsDrawnThisTurn);
    // Repair Bot
    var g2 = Ready(Plan());
    Board(g2, g2.Player1, "Mekka2");
    g2.Player2.Hero.Damage = 10;
    EndTurn(g2);
    Check("Mekka2 Repair Bot: at the end of its owner's turn it restores 6 Health to the damaged character", g2.Player2.Hero.Damage == 4, "damage " + g2.Player2.Hero.Damage);
    // Emboldener 3000
    var g3 = Ready(Plan());
    var bot = Board(g3, g3.Player1, "Mekka3");
    EndTurn(g3);
    Check("Mekka3 Emboldener 3000: at the end of its owner's turn a random minion (here itself) gets +1/+1", bot.AttackDamage == 1 && bot.Health == 5, bot.AttackDamage + "/" + bot.Health);
    // Poultryizer
    var g4 = Ready(Plan());
    Board(g4, g4.Player1, "Mekka4");
    Round(g4);
    Check("Mekka4 Poultryizer: at the start of its owner's turn a random minion (here itself) becomes a 1/1 Chicken", g4.Player1.BoardZone.Count == 1 && g4.Player1.BoardZone[0].Card.Id == "Mekka4t"
        && g4.Player1.BoardZone[0].AttackDamage == 1 && g4.Player1.BoardZone[0].Health == 1, g4.Player1.BoardZone.Count > 0 ? g4.Player1.BoardZone[0].Card.Id : "-");
});

// ============================================================ GIL_655 Festeroot Hulk
Guard("GIL_655", () =>
{
    var g = Ready(Plan());
    var hulk = Board(g, g.Player1, "GIL_655"); var other = Board(g, g.Player1, yeti);
    Attack(g, other, g.Player2.Hero);
    Check("GIL_655 after another friendly minion attacks it gains +1 Attack", hulk.AttackDamage == 3, "atk " + hulk.AttackDamage);
    Attack(g, hulk, g.Player2.Hero);
    Check("GIL_655 its own attack counts too", hulk.AttackDamage == 4, "atk " + hulk.AttackDamage);
    ToBoss(g);
    var enemy = Board(g, g.Player2, yeti);
    Attack(g, enemy, g.Player1.Hero);
    Check("GIL_655 an enemy minion's attack does not count", hulk.AttackDamage == 4);
});

// ============================================================ GVG_014 Vol'jin
Guard("GVG_014", () =>
{
    var g = Ready(Plan());
    var target = Board(g, g.Player2, "CS2_186"); // 7/7
    target.Damage = 2; // 5 Health left
    var card = ToHand(g, g.Player1, "GVG_014");
    Play(g, card, target);
    var voljin = g.Player1.BoardZone.First(m => m.Card.Id == "GVG_014");
    Check("GVG_014 swaps Health with the chosen minion (its current Health)", voljin.Health == 5 && target.Health == 2 && voljin.AttackDamage == 6 && target.AttackDamage == 7, voljin.Health + " / " + target.Health);
    var g2 = Ready(Plan());
    bool alone = PlayNew(g2, "GVG_014");
    Check("GVG_014 with no other minion it is played without a target", alone && g2.Player1.BoardZone[0].Health == 2);
});

// ============================================================ GVG_017 Call Pet
Guard("GVG_017", () =>
{
    var g = Ready(Plan(playerDeck: Deck(("DS1_178", 30)), playerHero: "HERO_05", playerPower: "DS1h_292")); // Tundra Rhino, a 5-Cost Beast
    int hand = g.Player1.HandZone.Count;
    PlayNew(g, "GVG_017");
    var drawn = g.Player1.HandZone[g.Player1.HandZone.Count - 1];
    Check("GVG_017 draws a card; a Beast costs (4) less", g.Player1.HandZone.Count == hand + 1 && drawn.Card.Id == "DS1_178" && drawn.Cost == 1, drawn.Card.Id + " cost " + drawn.Cost);
    var g2 = Ready(Plan(playerHero: "HERO_05", playerPower: "DS1h_292"));
    PlayNew(g2, "GVG_017");
    Check("GVG_017 a card that is no Beast keeps its Cost", g2.Player1.HandZone[g2.Player1.HandZone.Count - 1].Cost == 4);
});

// ============================================================ GVG_025 One-eyed Cheat
Guard("GVG_025", () =>
{
    var g = Ready(Plan());
    var cheat = Board(g, g.Player1, "GVG_025");
    Board(g, g.Player1, yeti);
    Check("GVG_025 summoning a minion that is no Pirate gives no Stealth", !cheat.HasStealth);
    Board(g, g.Player1, "CS2_146"); // Southsea Deckhand
    Check("GVG_025 whenever its owner summons a Pirate it gains Stealth", cheat.HasStealth);
    cheat.HasStealth = false;
    Board(g, g.Player2, "CS2_146");
    Check("GVG_025 an enemy Pirate does not count", !cheat.HasStealth);
    PlayNew(g, "CS2_146");
    Check("GVG_025 a Pirate played from hand counts", cheat.HasStealth);
});

// ============================================================ GVG_049 Gahz'rilla
Guard("GVG_049", () =>
{
    var g = Ready(Plan());
    var gahz = Board(g, g.Player1, "GVG_049");
    Power(g, gahz); // Fireblast: 1 damage
    Check("GVG_049 whenever it takes damage its Attack doubles", gahz.AttackDamage == 12 && gahz.Health == 8, gahz.AttackDamage + "/" + gahz.Health);
    Round(g);
    Power(g, gahz);
    Check("GVG_049 and doubles again", gahz.AttackDamage == 24, "atk " + gahz.AttackDamage);
});

// ============================================================ GVG_050 Bouncing Blade
Guard("GVG_050", () =>
{
    var g = Ready(Plan());
    var only = Board(g, g.Player2, "CS2_125"); // Ironfur Grizzly 3/3
    bool played = PlayNew(g, "GVG_050");
    Check("GVG_050 hits a random minion for 1 until a minion dies (one 3-Health minion: three hits)", played && only.Zone != g.Player2.BoardZone && g.Player2.Hero.Damage == 0);
    var g2 = Ready(Plan());
    var a = Board(g2, g2.Player2, yeti); var b = Board(g2, g2.Player1, yeti); var c = Board(g2, g2.Player2, "CS2_186");
    PlayNew(g2, "GVG_050");
    int dead = new[] { a, b, c }.Count(m => m.Zone?.Type != Zone.PLAY);
    int total = new[] { a, b, c }.Sum(m => m.Zone?.Type == Zone.PLAY ? m.Damage : m.Card.Health);
    Check("GVG_050 among several minions exactly one dies and the hits stop there", dead == 1 && total >= 5 && total <= 5 + 4 + 6, "dead " + dead + " total " + total);
    // No minion can die (Commanding Shout): the cast ends at its bound.
    var g3 = Ready(Plan());
    var held = Board(g3, g3.Player1, yeti);
    PlayNew(g3, "NEW1_036");
    bool bounded = PlayNew(g3, "GVG_050");
    Check("GVG_050 when no minion can die it stops after " + DungeonRunEncounters.BouncingBladeHits + " hits", bounded && held.Health == 1 && held.Zone == g3.Player1.BoardZone && g3.State != State.COMPLETE);
    var g4 = Ready(Plan());
    Check("GVG_050 needs a minion in play", !g4.Player1.Options().Any(o => o is PlayCardTask t && t.Source == ToHand(g4, g4.Player1, "GVG_050")) && !PlayNew(g4, "GVG_050"));
});

// ============================================================ GVG_054 Ogre Warmaul
Guard("GVG_054", () =>
{
    int toHero = 0, toMinion = 0, soloHero = 0;
    for (int seed = 1; seed <= 40; seed++)
    {
        var g = Ready(Plan(playerHero: "HERO_01", playerPower: "CS2_102"), seed, history: false);
        PlayNew(g, "GVG_054");
        var minion = Board(g, g.Player2, "CS2_186");
        g.Process(HeroAttackTask.Any(g.Player1, g.Player2.Hero));
        if (g.Player2.Hero.Damage == 4 && minion.Damage == 0) toHero++; else if (minion.Damage == 4 && g.Player2.Hero.Damage == 0) toMinion++;
        var solo = Ready(Plan(playerHero: "HERO_01", playerPower: "CS2_102"), seed, history: false);
        PlayNew(solo, "GVG_054");
        solo.Process(HeroAttackTask.Any(solo.Player1, solo.Player2.Hero));
        if (solo.Player2.Hero.Damage == 4) soloHero++;
    }
    Check("GVG_054 its wielder attacks the wrong enemy about half the time (40 seeds: every attack hits the hero or the other enemy)", toHero + toMinion == 40 && toHero >= 10 && toMinion >= 10, toHero + " hero / " + toMinion + " minion");
    Check("GVG_054 with no other enemy the attack always hits its target", soloHero == 40, soloHero + "/40");
    var gw = Ready(Plan(playerHero: "HERO_01", playerPower: "CS2_102"));
    PlayNew(gw, "GVG_054");
    Check("GVG_054 is a 4/2 weapon", gw.Player1.Hero.Weapon?.Card.Id == "GVG_054" && gw.Player1.Hero.Weapon.AttackDamage == 4 && gw.Player1.Hero.Weapon.Durability == 2);
    // A minion of the wielder is not forgetful.
    int minionWrong = 0;
    for (int seed = 1; seed <= 20; seed++)
    {
        var g = Ready(Plan(playerHero: "HERO_01", playerPower: "CS2_102"), seed, history: false);
        PlayNew(g, "GVG_054");
        var mine = Board(g, g.Player1, yeti); var other = Board(g, g.Player2, "CS2_186");
        Attack(g, mine, g.Player2.Hero);
        if (other.Damage != 0) minionWrong++;
    }
    Check("GVG_054 only the hero is forgetful, never its minions", minionWrong == 0, minionWrong + " wrong");
});

// ============================================================ GVG_097 Lil' Exorcist
Guard("GVG_097", () =>
{
    var g = Ready(Plan());
    Board(g, g.Player2, "EX1_096"); Board(g, g.Player2, "EX1_096"); Board(g, g.Player2, yeti); // two Loot Hoarders
    Board(g, g.Player1, "EX1_096");
    PlayNew(g, "GVG_097");
    var ex = g.Player1.BoardZone.First(m => m.Card.Id == "GVG_097");
    Check("GVG_097 Taunt, and +1/+1 for each enemy Deathrattle minion (two)", ex.HasTaunt && ex.AttackDamage == 4 && ex.Health == 5, ex.AttackDamage + "/" + ex.Health);
    var g2 = Ready(Plan());
    PlayNew(g2, "GVG_097");
    Check("GVG_097 with none it stays 2/3", g2.Player1.BoardZone[0].AttackDamage == 2 && g2.Player1.BoardZone[0].Health == 3);
});

// ============================================================ GVG_107 Enhance-o Mechano
Guard("GVG_107", () =>
{
    var kinds = new HashSet<string>(); bool exact = true;
    for (int seed = 1; seed <= 12; seed++)
    {
        var g = Ready(Plan(), seed, history: false);
        var others = new[] { Board(g, g.Player1, yeti), Board(g, g.Player1, yeti), Board(g, g.Player1, yeti) };
        var enemy = Board(g, g.Player2, yeti);
        PlayNew(g, "GVG_107");
        var self = g.Player1.BoardZone.First(m => m.Card.Id == "GVG_107");
        foreach (var m in others)
        {
            int n = (m.HasWindfury ? 1 : 0) + (m.HasTaunt ? 1 : 0) + (m.HasDivineShield ? 1 : 0);
            exact &= n == 1;
            kinds.Add(m.HasWindfury ? "W" : m.HasTaunt ? "T" : "D");
        }
        exact &= !self.HasWindfury && !self.HasTaunt && !self.HasDivineShield && !enemy.HasWindfury && !enemy.HasTaunt && !enemy.HasDivineShield;
    }
    Check("GVG_107 each other friendly minion gets exactly one of Windfury, Taunt, Divine Shield; itself and enemies none; all three appear", exact && kinds.Count == 3, string.Join("", kinds));
});

// ============================================================ GVG_108 Recombobulator
Guard("GVG_108", () =>
{
    var results = new HashSet<string>(); bool ok = true;
    for (int seed = 1; seed <= 12; seed++)
    {
        var g = Ready(Plan(), seed, history: false);
        var target = Board(g, g.Player1, yeti);
        var card = ToHand(g, g.Player1, "GVG_108");
        ok &= Play(g, card, target);
        var now = g.Player1.BoardZone.FirstOrDefault(m => m.Card.Id != "GVG_108");
        ok &= now != null && now.Card.Cost == 4 && now.Card.Implemented && now.Card.Type == CardType.MINION && g.Player1.BoardZone.Count == 2
            && !DungeonRunAdmission.GeneratedRejects(true).Contains(now.Card.Id);
        if (now != null) results.Add(now.Card.Id);
    }
    Check("GVG_108 transforms a friendly minion into a random scripted minion of the same Cost (12 seeds)", ok && results.Count >= 4, string.Join(",", results));
    var g2 = Ready(Plan());
    var enemy = Board(g2, g2.Player2, yeti);
    var c2 = ToHand(g2, g2.Player1, "GVG_108");
    Mana(g2.Player1);
    Check("GVG_108 cannot target an enemy minion", !g2.Process(PlayCardTask.Any(g2.Player1, c2, enemy, -1, 0)) || enemy.Card.Id == yeti);
});

// ============================================================ GVG_111 Mimiron's Head
Guard("GVG_111", () =>
{
    var g = Ready(Plan());
    var p = g.Player1;
    Board(g, p, "GVG_111"); Board(g, p, "GVG_006"); Board(g, p, "GVG_006"); var keep = Board(g, p, yeti); // Mechwarper x2
    Round(g);
    var v = p.BoardZone.FirstOrDefault(m => m.Card.Id == "GVG_111t");
    Check("GVG_111 at the start of its owner's turn with 3 Mechs they are destroyed and V-07-TR-0N is formed (4/8, Charge, Mega-Windfury)", v != null && p.BoardZone.Count == 2 && keep.Zone == p.BoardZone
        && v.AttackDamage == 4 && v.Health == 8 && v.HasCharge && CombatRules.AttackLimit(v) == 4, string.Join(",", p.BoardZone.Select(m => m.Card.Id)) + " " + v?.AttackDamage + "/" + v?.Health + " charge " + v?.HasCharge + " limit " + (v == null ? 0 : CombatRules.AttackLimit(v)) + " wf " + v?[GameTag.WINDFURY] + " mwf " + v?[GameTag.MEGA_WINDFURY]);
    var g2 = Ready(Plan());
    Board(g2, g2.Player1, "GVG_111"); Board(g2, g2.Player1, "GVG_006");
    Round(g2);
    Check("GVG_111 with two Mechs nothing happens", g2.Player1.BoardZone.Count == 2 && g2.Player1.BoardZone.All(m => m.Card.Id != "GVG_111t"));
    var g3 = Ready(Plan());
    Board(g3, g3.Player1, "GVG_111");
    for (int i = 0; i < 6; i++) Board(g3, g3.Player1, "GVG_006");
    Round(g3);
    Check("GVG_111 a full board of Mechs still forms V-07-TR-0N (the Mechs die first)", g3.Player1.BoardZone.Count == 1 && g3.Player1.BoardZone[0].Card.Id == "GVG_111t");
});

// ============================================================ GVG_113 Foe Reaper 4000
Guard("GVG_113", () =>
{
    var g = Ready(Plan());
    var reaper = Board(g, g.Player1, "GVG_113");
    var left = Board(g, g.Player2, "CS2_186"); var mid = Board(g, g.Player2, "CS2_186"); var right = Board(g, g.Player2, "CS2_186"); var far = Board(g, g.Player2, "CS2_186");
    Attack(g, reaper, mid);
    Check("GVG_113 also damages the minions next to whomever it attacks", left.Damage == 6 && mid.Damage == 6 && right.Damage == 6 && far.Damage == 0 && reaper.Damage == 7,
        left.Damage + "," + mid.Damage + "," + right.Damage + "," + far.Damage);
    var g2 = Ready(Plan());
    var r2 = Board(g2, g2.Player1, "GVG_113"); var m2 = Board(g2, g2.Player2, "CS2_186");
    Attack(g2, r2, g2.Player2.Hero);
    Check("GVG_113 an attack on the hero damages no minion", g2.Player2.Hero.Damage == 6 && m2.Damage == 0);
});

// ============================================================ GVG_119 Blingtron 3000
Guard("GVG_119", () =>
{
    var weapons = new HashSet<string>(); bool ok = true;
    for (int seed = 1; seed <= 8; seed++)
    {
        var g = Ready(Plan(), seed, history: false);
        ok &= PlayNew(g, "GVG_119");
        var mine = g.Player1.Hero.Weapon; var theirs = g.Player2.Hero.Weapon;
        ok &= mine != null && theirs != null && mine.Card.Implemented && theirs.Card.Implemented && mine.Card.Collectible && theirs.Card.Collectible && mine.Controller == g.Player1 && theirs.Controller == g.Player2;
        if (mine != null) weapons.Add(mine.Card.Id); if (theirs != null) weapons.Add(theirs.Card.Id);
    }
    Check("GVG_119 equips a random scripted collectible weapon for each player (8 seeds)", ok && weapons.Count >= 5, string.Join(",", weapons));
});

// ============================================================ LOE_009 Obsidian Destroyer, LOE_022 Fierce Monkey
Guard("LOE_009", () =>
{
    var g = Ready(Plan());
    var destroyer = Board(g, g.Player1, "LOE_009");
    EndTurn(g);
    var scarab = g.Player1.BoardZone.FirstOrDefault(m => m.Card.Id == "LOE_009t");
    Check("LOE_009 at the end of its owner's turn a 1/1 Scarab with Taunt is summoned to its right", scarab != null && scarab.HasTaunt && scarab.AttackDamage == 1 && scarab.Health == 1 && scarab.ZonePosition == destroyer.ZonePosition + 1);
    EndTurn(g);
    Check("LOE_009 not at the end of the opponent's turn", g.Player1.BoardZone.Count == 2);
    var monkey = Board(g, g.Player1, "LOE_022");
    Check("LOE_022 Fierce Monkey is a 3/4 Beast with Taunt", monkey.HasTaunt && monkey.AttackDamage == 3 && monkey.Health == 4 && monkey.IsRace(Race.BEAST));
});

// ============================================================ LOE_029 Jeweled Scarab, LOE_047 Tomb Spider
Guard("LOE_029", () =>
{
    var rejects = new HashSet<string>(DungeonRunAdmission.GeneratedRejects(true));
    bool ok = true; var seenCards = new HashSet<string>();
    for (int seed = 1; seed <= 6; seed++)
    {
        var g = Ready(Plan(), seed, history: false);
        ok &= PlayNew(g, "LOE_029");
        var offers = Offers(g, g.Player1);
        ok &= offers.Count == 3 && offers.Select(c => c.Id).Distinct().Count() == 3 && offers.All(c => c.Cost == 3 && c.Implemented && !rejects.Contains(c.Id) && (c.Class == CardClass.MAGE || c.Class == CardClass.NEUTRAL));
        foreach (var c in offers) seenCards.Add(c.Id);
        string picked = offers.Count > 0 ? offers[0].Id : null;
        int hand = g.Player1.HandZone.Count;
        ok &= Pick(g, g.Player1) && g.Player1.HandZone.Count == hand + 1 && g.Player1.HandZone[g.Player1.HandZone.Count - 1].Card.Id == picked;
    }
    Check("LOE_029 Discover a 3-Cost card: three different scripted cards of the owner's class or neutral; the pick joins the hand", ok && seenCards.Count >= 8, seenCards.Count + " cards seen");
    bool beasts = true;
    for (int seed = 1; seed <= 6; seed++)
    {
        var g = Ready(Plan(playerHero: "HERO_05", playerPower: "DS1h_292"), seed, history: false);
        beasts &= PlayNew(g, "LOE_047");
        var offers = Offers(g, g.Player1);
        beasts &= offers.Count == 3 && offers.All(c => c.Type == CardType.MINION && c.IsRace(Race.BEAST) && c.Implemented && !rejects.Contains(c.Id));
        beasts &= Pick(g, g.Player1);
    }
    Check("LOE_047 Discover a Beast: three scripted Beasts", beasts);
    var gb = Ready(Plan());
    ToBoss(gb);
    PlayNew(gb, "LOE_029");
    var bossRejects = new HashSet<string>(DungeonRunAdmission.GeneratedRejectsBoss(true));
    var bossOffers = Offers(gb, gb.Player2);
    Check("LOE_029 (boss) the boss's Discover honours the boss's generation list", bossOffers.Count == 3 && bossOffers.All(c => c.Cost == 3 && !rejects.Contains(c.Id) && !bossRejects.Contains(c.Id)));
});

// ============================================================ LOE_053 Djinni of Zephyrs
Guard("LOE_053", () =>
{
    var g = Ready(Plan(playerHero: "HERO_04", playerPower: "CS2_101"));
    var p = g.Player1;
    var djinni = Board(g, p, "LOE_053"); var friend = Board(g, p, yeti); var enemy = Board(g, g.Player2, yeti);
    Play(g, ToHand(g, p, "CS2_087"), friend); // Blessing of Might: +3 Attack
    Check("LOE_053 after its owner casts a spell on another friendly minion, a copy is cast on it", friend.AttackDamage == 7 && djinni.AttackDamage == 7, friend.AttackDamage + " / " + djinni.AttackDamage);
    Play(g, ToHand(g, p, "CS2_087"), djinni);
    Check("LOE_053 a spell cast on itself is not copied", djinni.AttackDamage == 10);
    Play(g, ToHand(g, p, "CS2_087"), enemy);
    Check("LOE_053 a spell cast on an enemy minion is not copied", enemy.AttackDamage == 7 && djinni.AttackDamage == 10);
    ToBoss(g);
    var bossMinion = Board(g, g.Player2, yeti);
    Play(g, ToHand(g, g.Player2, "CS2_087"), bossMinion);
    Check("LOE_053 the opponent's spells are not copied", djinni.AttackDamage == 10);
});

// ============================================================ LOE_061 Anubisath Sentinel
Guard("LOE_061", () =>
{
    var g = Ready(Plan());
    var sentinel = Board(g, g.Player1, "LOE_061"); var friend = Board(g, g.Player1, yeti); var enemy = Board(g, g.Player2, yeti);
    sentinel.Destroy(); g.DeathProcessingAndAuraUpdate();
    Check("LOE_061 Deathrattle: a random friendly minion gets +3/+3", friend.AttackDamage == 7 && friend.Health == 8 && enemy.AttackDamage == 4, friend.AttackDamage + "/" + friend.Health);
    var g2 = Ready(Plan());
    var alone = Board(g2, g2.Player1, "LOE_061");
    alone.Destroy(); g2.DeathProcessingAndAuraUpdate();
    Check("LOE_061 with no friendly minion left nothing happens", g2.State != State.COMPLETE && g2.Player1.BoardZone.Count == 0);
});

// ============================================================ LOE_092 Arch-Thief Rafaam and the artifacts
Guard("LOE_092", () =>
{
    var g = Ready(Plan());
    var p = g.Player1;
    PlayNew(g, "LOE_092");
    var offers = Offers(g, p).Select(c => c.Id).OrderBy(x => x).ToList();
    Check("LOE_092 Discover a powerful Artifact: Lantern of Power, Timepiece of Horror, Mirror of Doom", offers.SequenceEqual(new[] { "LOEA16_3", "LOEA16_4", "LOEA16_5" }), string.Join(",", offers));
    Pick(g, p);
    Check("LOE_092 the picked artifact joins the hand (10 mana)", p.HandZone[p.HandZone.Count - 1].Card.Id.StartsWith("LOEA16_") && p.HandZone[p.HandZone.Count - 1].Cost == 10);
    var g1 = Ready(Plan());
    var friend = Board(g1, g1.Player1, yeti);
    Play(g1, ToHand(g1, g1.Player1, "LOEA16_3"), friend);
    Check("LOEA16_3 Lantern of Power gives a minion +10/+10", friend.AttackDamage == 14 && friend.Health == 15);
    var g2 = Ready(Plan());
    var e1 = Board(g2, g2.Player2, "CS2_186"); var own = Board(g2, g2.Player1, yeti);
    PlayNew(g2, "LOEA16_4");
    Check("LOEA16_4 Timepiece of Horror deals 10 damage split among all enemies", g2.Player2.Hero.Damage + (e1.Zone?.Type == Zone.PLAY ? e1.Damage : 7) == 10 && own.Damage == 0 && g2.Player1.Hero.Damage == 0,
        g2.Player2.Hero.Damage + " + " + e1.Damage);
    var g3 = Ready(Plan());
    Board(g3, g3.Player1, yeti);
    PlayNew(g3, "LOEA16_5");
    Check("LOEA16_5 Mirror of Doom fills the board with 3/3 Mummy Zombies", g3.Player1.BoardZone.Count == 7 && g3.Player1.BoardZone.Count(m => m.Card.Id == "LOEA16_5t" && m.AttackDamage == 3 && m.Health == 3) == 6);
});

// ============================================================ LOE_107 Eerie Statue
Guard("LOE_107", () =>
{
    var g = Ready(Plan());
    var p = g.Player1;
    var statue = Board(g, p, "LOE_107");
    g.DeathProcessingAndAuraUpdate();
    bool CanAttack() => p.Options().Any(o => o is MinionAttackTask a && a.Source == statue);
    Check("LOE_107 alone on the battlefield it can attack", !statue.CantAttack && CanAttack());
    var other = Board(g, g.Player2, yeti);
    Check("LOE_107 with another minion in play (an enemy one) it can't attack", statue.CantAttack && !CanAttack());
    other.Destroy(); g.DeathProcessingAndAuraUpdate();
    Check("LOE_107 alone again it can attack", !statue.CantAttack && CanAttack());
    var friend = Board(g, p, yeti);
    Check("LOE_107 a friendly minion stops it too", statue.CantAttack && !CanAttack());
    statue.Silence(); g.DeathProcessingAndAuraUpdate();
    Check("LOE_107 silenced it attacks freely", !statue.CantAttack && CanAttack());
});

// ============================================================ LOE_119 Animated Armor
Guard("LOE_119", () =>
{
    var g = Ready(Plan());
    var p = g.Player1;
    var armor = Board(g, p, "LOE_119");
    ToBoss(g);
    var enemy = Board(g, g.Player2, "CS2_186");
    Attack(g, enemy, p.Hero);
    Check("LOE_119 its owner's hero takes only 1 damage from a 7-Attack hit", p.Hero.Damage == 1, "damage " + p.Hero.Damage);
    Play(g, ToHand(g, g.Player2, "CS2_029"), p.Hero); // Fireball: 6
    Check("LOE_119 and only 1 from a 6-damage spell", p.Hero.Damage == 2, "damage " + p.Hero.Damage);
    p.Hero.Armor = 5;
    Play(g, ToHand(g, g.Player2, "CS2_029"), p.Hero);
    Check("LOE_119 Armor absorbs the single point", p.Hero.Damage == 2 && p.Hero.Armor == 4, p.Hero.Damage + " armor " + p.Hero.Armor);
    Play(g, ToHand(g, g.Player2, "CS2_029"), armor);
    Check("LOE_119 its own damage is not capped (it dies to Fireball)", armor.Zone != p.BoardZone);
    Play(g, ToHand(g, g.Player2, "CS2_029"), p.Hero);
    Check("LOE_119 once it is gone the hero takes full damage (4 Armor, then 2 Health)", p.Hero.Armor == 0 && p.Hero.Damage == 4, p.Hero.Damage + " armor " + p.Hero.Armor);
    var g2 = Ready(Plan());
    var a2 = Board(g2, g2.Player1, "LOE_119");
    a2.Silence();
    ToBoss(g2);
    Play(g2, ToHand(g2, g2.Player2, "CS2_029"), g2.Player1.Hero);
    Check("LOE_119 silenced it caps nothing", g2.Player1.Hero.Damage == 6);
    var g3 = Ready(Plan());
    Board(g3, g3.Player2, "LOE_119");
    Play(g3, ToHand(g3, g3.Player1, "CS2_029"), g3.Player2.Hero);
    Check("LOE_119 (boss) the boss's hero is capped by its own Animated Armor; the run player's is not", g3.Player2.Hero.Damage == 1 && EncounterRules.HeroDamageCapOf(g3.Player2) == 1 && EncounterRules.HeroDamageCapOf(g3.Player1) == 0);
});

// ============================================================ TRL_318 Hex Lord Malacrass
Guard("TRL_318", () =>
{
    var g = Ready(Plan(playerDeck: Deck((yeti, 14), ("CS2_186", 14), ("TRL_318", 2))));
    var p = g.Player1;
    // The hand now is the opening hand plus the first turn's draw (its last card).
    var opening = Hand(p).Take(p.HandZone.Count - 1).Where(id => !id.StartsWith("GAME_005")).ToList();
    var recorded = TurnQueueRules.StartingHand(p).Select(c => c.Id).Where(id => !id.StartsWith("GAME_005")).ToList();
    Check("TRL_318 the opening hand is recorded after the mulligan (before the first draw)", recorded.SequenceEqual(opening) && opening.Count == 3 && p.NumCardsDrawnThisTurn == 1, string.Join(",", recorded));
    Round(g); Round(g);
    ClearHand(p);
    PlayNew(g, "TRL_318");
    var expected = opening.Where(id => id != "TRL_318").OrderBy(x => x).ToList();
    Check("TRL_318 adds a copy of the opening hand to the hand, except Malacrass himself", Hand(p).OrderBy(x => x).SequenceEqual(expected), string.Join(",", Hand(p)) + " vs " + string.Join(",", expected));
    // A run match records the opening hand even when no deck lists Malacrass (a Discover can give one later).
    var cls = data.Classes[0];
    // The close-out made the encounter schedules the defaults (a new run opens on an anomaly node); this check is about the
    // first fight's plan, so it names acts-v2, the schedule it was written on (the same run as before).
    var s = run.NewRun(all, cls.Id, cls.Decks[0], cls.Powers[0], 4242, schedule: "acts-v2");
    var plan = run.BuildPlan(s, all);
    var gr = Ready(plan);
    Check("TRL_318 a run match (DungeonRun.BuildPlan) records both opening hands", plan.RunContext != null && TurnQueueRules.StartingHand(gr.Player1).Length >= 3 && TurnQueueRules.StartingHand(gr.Player2).Length >= 3);
    ToBoss(gr);
    var bossOpening = TurnQueueRules.StartingHand(gr.Player2).Select(c => c.Id).Where(id => id != "TRL_318" && !id.StartsWith("GAME_005")).OrderBy(x => x).ToList();
    ClearHand(gr.Player2);
    PlayNew(gr, "TRL_318");
    Check("TRL_318 (boss) the boss gets a copy of its own opening hand", Hand(gr.Player2).OrderBy(x => x).SequenceEqual(bossOpening), string.Join(",", Hand(gr.Player2)));
});

// ============================================================ DAL_431 Swampqueen Hagatha and the Drustvar Horror
Guard("DAL_431", () =>
{
    var rejects = new HashSet<string>(DungeonRunAdmission.GeneratedRejects(true));
    var g = Ready(Plan(playerHero: "HERO_02", playerPower: "CS2_049"));
    var p = g.Player1;
    PlayNew(g, "DAL_431");
    var first = Offers(g, p);
    Check("DAL_431 the first Discover offers three scripted Shaman spells", first.Count == 3 && first.All(c => c.Type == CardType.SPELL && c.Class == CardClass.SHAMAN && c.Implemented && !rejects.Contains(c.Id)), string.Join(",", first.Select(c => c.Id)));
    Card one = first[0];
    Pick(g, p);
    var second = Offers(g, p);
    Check("DAL_431 then a second Discover (only untargeted spells after a targeted one)", second.Count == 3 && second.All(c => c.Type == CardType.SPELL && c.Class == CardClass.SHAMAN)
        && (one.TargetingType == TargetingType.None || second.All(c => c.TargetingType == TargetingType.None)), string.Join(",", second.Select(c => c.Id)));
    Card two = second[0];
    int hand = p.HandZone.Count;
    Pick(g, p);
    var horror = p.HandZone[p.HandZone.Count - 1];
    Check("DAL_431 a 5/5 Drustvar Horror that knows both spells joins the hand", p.Choice == null && p.HandZone.Count == hand + 1 && horror.Card.Id == "DAL_431t" && horror.Card.ATK == 5 && horror.Card.Health == 5 && horror.Cost == 5
        && horror.Card[GameTag.MODULAR_ENTITY_PART_1] == one.AssetId && horror.Card[GameTag.MODULAR_ENTITY_PART_2] == two.AssetId, horror.Card.Id + " " + one.Id + "+" + two.Id);
    // Fixed spells: Lightning Bolt (targeted, 3 damage, Overload 1) and Feral Spirit (two 2/3 Taunt wolves, Overload 2).
    var g2 = Ready(Plan(playerHero: "HERO_02", playerPower: "CS2_049"));
    var p2 = g2.Player1;
    var enemy = Board(g2, g2.Player2, yeti);
    var made = Entity.FromCard(p2, DungeonRunEncounters.Horror(C("EX1_238"), C("EX1_248")));
    Generic.AddHandPhase.Invoke(p2, made);
    Mana(p2);
    bool needsTarget = made.Card.TargetingType != TargetingType.None;
    bool played = g2.Process(PlayCardTask.Any(p2, made, enemy, -1, 0));
    Check("DAL_431t its Battlecry casts both spells: Lightning Bolt on the chosen target, then Feral Spirit", needsTarget && played && enemy.Damage == 3 && p2.BoardZone.Count(m => m.Card.Id == "EX1_tk11") == 2 && p2.BoardZone.Any(m => m.Card.Id == "DAL_431t"),
        "target " + needsTarget + " damage " + enemy.Damage + " board " + string.Join(",", p2.BoardZone.Select(m => m.Card.Id)));
    var g3 = Ready(Plan(playerHero: "HERO_02", playerPower: "CS2_049"));
    var plain = Entity.FromCard(g3.Player1, DungeonRunEncounters.Horror(C("EX1_248"), C("EX1_248")));
    Generic.AddHandPhase.Invoke(g3.Player1, plain);
    Mana(g3.Player1);
    Check("DAL_431t two untargeted spells: no target, both are cast", plain.Card.TargetingType == TargetingType.None && g3.Process(PlayCardTask.Any(g3.Player1, plain, null, -1, 0)) && g3.Player1.BoardZone.Count(m => m.Card.Id == "EX1_tk11") == 4);
    var clone = g2.Clone();
    Check("DAL_431t the generated definition survives a game clone", clone.Player1.BoardZone.Any(m => m.Card.Id == "DAL_431t" && m.Card[GameTag.MODULAR_ENTITY_PART_1] == C("EX1_238").AssetId));
    Check("DAL_431t the printed token is a vanilla 5/5", C("DAL_431t").Implemented && C("DAL_431t").ATK == 5 && C("DAL_431t").Health == 5 && C("DAL_431t").Power == null);
});

// ============================================================ the boss's fair view (what its search may read)
Guard("fairview", () =>
{
    // A boss turn with Madame Lazul, Duel! and Hex Lord Malacrass in the boss's hand; the run player's deck holds minions.
    var g = Ready(Plan(bossDeck: Deck(("CS2_186", 28), ("TRL_318", 2))));
    ToBoss(g);
    foreach (string id in new[] { "DAL_729", "DAL_731" }) ToHand(g, g.Player2, id);
    var bot = FairBotState.Create(g, g.Player2.PlayerId, 1, DeckStyle.Tempo);
    var view = bot.FingerprintGame;
    var viewBoss = view.Player1.PlayerId == g.Player2.PlayerId ? view.Player1 : view.Player2;
    var viewPlayer = viewBoss.Opponent;
    Check("FV1 the boss's fair view masks the run player's hand and deck", bot.Facts.Opponent.Hand.Count == g.Player1.HandZone.Count && bot.Facts.Opponent.Hand.All(c => c.Unknown)
        && viewPlayer.HandZone.All(p => p is Unknown) && !viewPlayer.DeckZone.Any(p => p is Minion), viewPlayer.HandZone.Count + " hand, " + viewPlayer.DeckZone.Count + " deck");
    Check("FV2 the fair view keeps the boss's own opening hand and holds none of the run player's (TRL_318)", TurnQueueRules.StartingHand(viewBoss).Length == TurnQueueRules.StartingHand(g.Player2).Length
        && TurnQueueRules.StartingHand(g.Player2).Length >= 3 && TurnQueueRules.StartingHand(viewPlayer).Length == 0 && TurnQueueRules.StartingHand(g.Player1).Length >= 3);
    IPlayable InView(string id) => viewBoss.HandZone.First(p => p.Card.Id == id);
    Mana(viewBoss);
    string lazul = "ok";
    try { view.Process(PlayCardTask.Any(viewBoss, InView("DAL_729"), null, -1, 0)); } catch (Exception e) { lazul = e.GetType().Name + ": " + e.Message; }
    Check("FV3 in the fair view Madame Lazul's Battlecry offers nothing: the search never reads the hidden hand (DAL_729)", lazul == "ok" && viewBoss.Choice == null && viewBoss.BoardZone.Any(m => m.Card.Id == "DAL_729"), lazul);
    Mana(viewBoss);
    int viewDeck = viewBoss.DeckZone.Count, viewPlayerDeck = viewPlayer.DeckZone.Count;
    string duel = "ok";
    try { view.Process(PlayCardTask.Any(viewBoss, InView("DAL_731"), null, -1, 0)); } catch (Exception e) { duel = e.GetType().Name + ": " + e.Message; }
    // The fair view's decks are unmaterialized slots (the search samples the boss's own deck per world, OwnDeckLedger): the run
    // player's deck yields no minion in any world, so only the boss's own sampled minion can arrive.
    Check("FV4 in the fair view Duel! takes nothing from the run player's deck: it stays hidden (DAL_731)", duel == "ok" && viewPlayer.DeckZone.Count == viewPlayerDeck && viewPlayer.BoardZone.Count == 0
        && viewBoss.DeckZone.Count + viewBoss.BoardZone.Count(m => m.Card.Id != "DAL_729") == viewDeck, duel + " deck " + viewDeck + "->" + viewBoss.DeckZone.Count + " player deck " + viewPlayerDeck + "->" + viewPlayer.DeckZone.Count);
    // The real game: both minions arrive; the boss's own-deck ledger stays consistent with its real deck.
    Play(g, g.Player2.HandZone.First(p => p.Card.Id == "DAL_731"));
    var ledger = OwnDeckLedger.Observe(g, g.Player2, null);
    bool consistent = true; var detail = new List<string>();
    for (int seed = 1; seed <= 6; seed++)
    {
        var sample = ledger.Sample(g, g.Player2, seed);
        var start = g.Player2.DeckCards.GroupBy(c => c.Id).ToDictionary(x => x.Key, x => x.Count());
        consistent &= sample.Values.Sum() == g.Player2.DeckZone.Count && sample.All(p => start.TryGetValue(p.Key, out int n) && n >= p.Value);
        detail.Add(sample.Values.Sum() + "/" + g.Player2.DeckZone.Count);
    }
    Check("FV5 after the real Duel! the boss's own-deck ledger samples its deck exactly (one minion left it for the board)", consistent && g.Player2.BoardZone.Count(m => m.Card.Id == "CS2_186" || m.Card.Id == "TRL_318") == 1
        && g.Player1.GraveyardZone.Concat(g.Player1.BoardZone.Cast<IPlayable>()).Any(p => p.Card.Id == yeti), string.Join(" ", detail));
    // Mysterious Challenger for a boss: the Secrets leave its deck for its Secret zone; the ledger follows.
    var g2 = Ready(Plan(bossDeck: Deck(("EX1_610", 2), ("EX1_611", 2), ("EX1_554", 2), (yeti, 24))));
    ToBoss(g2);
    SecretsToDeck(g2.Player2);
    PlayNew(g2, "AT_079");
    var ledger2 = OwnDeckLedger.Observe(g2, g2.Player2, null);
    bool consistent2 = true;
    for (int seed = 1; seed <= 6; seed++)
    {
        var sample = ledger2.Sample(g2, g2.Player2, seed);
        var real = g2.Player2.DeckZone.GroupBy(c => c.Card.Id).ToDictionary(x => x.Key, x => x.Count());
        consistent2 &= sample.Values.Sum() == g2.Player2.DeckZone.Count && new[] { "EX1_610", "EX1_611", "EX1_554" }.All(id => sample.GetValueOrDefault(id) == real.GetValueOrDefault(id));
    }
    Check("FV6 after Mysterious Challenger the boss's own-deck ledger holds exactly the Secrets left in its deck (AT_079)", consistent2 && g2.Player2.SecretZone.Count == 3);
});

// ============================================================ admission and data
Guard("admission", () =>
{
    var notAdmitted = ids.Where(id => !DungeonRunAdmission.Classify(C(id), id, AdmitScope.Player, null, true).Admitted || !DungeonRunAdmission.Classify(C(id), id, AdmitScope.Boss, null, true).Admitted).ToList();
    Check("Z1 every one of the 33 cards is admitted for the run player and for a boss (catalog runs)", notAdmitted.Count == 0, string.Join(",", notAdmitted));
    var noRow = ids.Where(id => DungeonRunAdmission.RuleFor(id) == null).ToList();
    Check("Z2 every one has an admission row that names its test", noRow.Count == 0 && ids.All(id => DungeonRunAdmission.RuleFor(id).Reason.Contains("c-collectibles-A") || id == "AT_079" || id == "DAL_573"), string.Join(",", noRow));
    var generated = new HashSet<string>(DungeonRunAdmission.GeneratedRejects(true).Concat(DungeonRunAdmission.GeneratedRejectsBoss(true)));
    Check("Z3 none of them is on a generation reject list", !ids.Any(generated.Contains), string.Join(",", ids.Where(generated.Contains)));
    var pool = data.Bundles.Where(b => b.IsPool).SelectMany(b => b.Candidates.Select(c => c.Id)).ToHashSet();
    var offered = ids.Where(pool.Contains).ToList();
    Check("Z4 the regenerated catalog offers the bucket members among them", offered.Count >= 25, offered.Count + " offered: " + string.Join(",", ids.Where(id => !pool.Contains(id))));
});

Console.WriteLine($"RESULT {passes} passed, {failures} failed");
return failures;
