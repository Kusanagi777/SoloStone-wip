// c-teammates-T1: phase 7 family P7-T1, the teammate draft and the Druid, Hunter, Mage and Paladin teammates: TRLA_122/123/124/
// 125/127 (Druid), TRLA_165-169 (Hunter), TRLA_130-134 (Mage), TRLA_140-144 (Paladin), and the client enchantment records
// TRLA_124e/125e/131e/169e. Every game plays a real engine game through the run setup path (NewRun -> BuildPlan ->
// DungeonRunSetup.Configure -> Game -> ApplyAfterStart -> mulligan -> MainBegin), on the class's Rikkar row with a board shrine.
//   A admission, registry and data: scripted, admitted for the run player only, never generated; the class pools.
//   D/H/M/P each teammate's rule in real games, with its interactions: the class's board shrines (TRLA_112/115, TRLA_110/162,
//     TRLA_108/128/129, TRLA_105/137), shrine dormancy (a shrine death counts as a death, dormant forms are untouchable), the
//     TRLA_107 boss fixture, shrine upgrade TRLA_803, treasures LOOTA_846 (Battle Totem), LOOTA_845 (Totem of the Dead),
//     LOOTA_825 (Robe of the Magi).
//   R the teammate draft through the run flow for the four classes: offers, no repeats, deck records, the plan, the match deck,
//     a drafted teammate played from the run deck; save and resume with the draft open.
// Headless: compiles C:\SoloStone\plugin\DungeonRun\*.cs and the NPC AI against audit-verify/engine-out. Run it twice and compare.
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
    Console.WriteLine((ok ? "PASS " : "FAIL ") + name + (detail.Length > 0 ? "  (" + detail + ")" : ""));
}
void Guard(string name, Action body)
{
    try { body(); }
    catch (Exception e) { failures++; Console.WriteLine("FAIL " + name + " threw " + e.GetType().Name + ": " + e.Message.Split('\n')[0]); }
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
string runJson = R("dungeon-run/run-v1.json"), scheduleJson = R("dungeon-run/schedule-v1.json");
DungeonRunData Load() => DungeonRunData.Parse(runJson, R("dungeon-run/pool-v1.json"), R("npc/decks-v1.json"),
    R("dungeon-run/catalog-v1.json"), scheduleJson, R("dungeon-run/tavern-v1.json"));
var data = Load();
var run = new DungeonRun(data);
var all = new DungeonRunProfile(); all.Unlocked.AddRange(data.Unlocks.Keys);
Card C(string id) => Cards.FromId(id);

DungeonRunState NewRun(string row, string deck, long seed)
{
    var cls = data.Class(row);
    return run.NewRun(all, cls.Id, deck ?? cls.DefaultDeck, cls.DefaultPower, seed,
        signatureId: run.SignatureOffer(all, data.FindClass(cls.Id), seed).FirstOrDefault());
}
Game Start(RunMatchPlan plan, int seed)
{
    var cfg = new GameConfig { RandomSeed = seed, Shuffle = true, SkipMulligan = false, History = true, Logging = false, FormatType = FormatType.FT_WILD };
    DungeonRunSetup.Configure(cfg, plan);
    var g = new Game(cfg);
    g.StartGame();
    DungeonRunSetup.ApplyAfterStart(g, plan);
    return g;
}
string RowOf(string deck) => "TRLA_209h_" + char.ToUpperInvariant(deck[7]) + deck.Substring(8, deck.IndexOf('_', 7) - 8);
(DungeonRunState s, RunMatchPlan plan, Game g) F(string deck, long seed, string[] upgrades = null, string[] treasures = null)
{
    var s = NewRun(RowOf(deck), deck, seed);
    foreach (string u in upgrades ?? Array.Empty<string>()) s.ShrineUpgrades.Add(u);
    foreach (string t in treasures ?? Array.Empty<string>()) s.Treasures.Add(t);
    var plan = run.BuildPlan(s);
    var g = Start(plan, run.MatchSeed(s));
    g.Process(ChooseTask.Mulligan(g.Player1, new List<int>()));
    g.Process(ChooseTask.Mulligan(g.Player2, new List<int>()));
    g.MainBegin();
    if (g.CurrentPlayer != g.Player1) throw new Exception("run player not first");
    if (s.BoardShrine == null || ShrineRules.Of(g.Player1).Count == 0) throw new Exception("no board shrine on " + deck);
    return (s, plan, g);
}
void Mana(Controller c, int n) { c.BaseMana = n; c.UsedMana = 0; c.OverloadLocked = 0; c.OverloadOwed = 0; }
IPlayable ToHand(Game g, Controller c, string id)
{
    while (c.HandZone.Count >= 9) { var old = c.HandZone[0]; c.HandZone.Remove(old); c.SetasideZone.Add(old); }
    var e = Entity.FromCard(c, C(id)); Generic.AddHandPhase.Invoke(c, e); g.DeathProcessingAndAuraUpdate(); return e;
}
// Fills a hand to 10 cards (ToHand keeps a slot free).
void FillHand(Game g, Controller c, string id) { while (c.HandZone.Count < 10) Generic.AddHandPhase.Invoke(c, Entity.FromCard(c, C(id))); g.DeathProcessingAndAuraUpdate(); }
void ClearHand(Game g, Controller c) { foreach (var p in c.HandZone.ToArray()) { c.HandZone.Remove(p); c.SetasideZone.Add(p); } g.DeathProcessingAndAuraUpdate(); }
// Replaces a deck with the given cards (the old cards go to the set-aside zone), so a draw check knows what can come.
void SetDeck(Game g, Controller c, params string[] ids)
{
    foreach (var p in c.DeckZone.ToArray()) { c.DeckZone.Remove(p); c.SetasideZone.Add(p); }
    foreach (string id in ids) c.DeckZone.Add(Entity.FromCard(c, C(id)));
    g.DeathProcessingAndAuraUpdate();
}
Minion Board(Game g, Controller c, string id) { var m = (Minion)Entity.FromCard(c, C(id)); Generic.SummonBlock(g, m, -1, c.Hero); g.DeathProcessingAndAuraUpdate(); return m; }
bool Play(Game g, IPlayable card, ICharacter target = null, int chooseOne = 0) => g.Process(PlayCardTask.Any(g.CurrentPlayer, card, target, -1, chooseOne));
void EndTurn(Game g) => g.Process(EndTurnTask.Any(g.CurrentPlayer));
void ClearExceptShrines(Game g)
{
    foreach (var c in new[] { g.Player1, g.Player2 })
        foreach (var m in c.BoardZone.Slots.OfType<Minion>().Where(m => !ShrineRules.IsMarked(m)).ToArray()) { c.BoardZone.Remove(m); c.SetasideZone.Add(m); }
    g.DeathProcessingAndAuraUpdate();
}
Minion ShrineOf(Game g, Controller c) => ShrineRules.Of(c).FirstOrDefault();
void Kill(Game g, Minion m) { m.Destroy(); g.DeathProcessingAndAuraUpdate(); }
Minion Ready(Minion m) { m.IsExhausted = false; return m; }
bool Attack(Game g, Minion a, ICharacter t) => g.Process(MinionAttackTask.Any(g.CurrentPlayer, a, t));
bool Power(Game g, ICharacter target = null) => g.Process(HeroPowerTask.Any(g.CurrentPlayer, target));
IEnumerable<Enchantment> Ench(IEntity e) => (IEnumerable<Enchantment>)(e as IPlayable)?.AppliedEnchantments ?? Array.Empty<Enchantment>();
// Plays a teammate from the run player's hand onto the board (10 Mana) and returns it.
Minion Mate(Game g, string id, ICharacter target = null) { Mana(g.Player1, 10); var m = (Minion)ToHand(g, g.Player1, id); Play(g, m, target); return m; }
// Places the TRLA_107 Tribal Shrine fixture (a 1-Health Taunt run shrine) on the boss's board.
Minion Fixture(Game g) { ShrineRules.Place(g.Player2, DungeonRunShrines.FixtureShrine); g.DeathProcessingAndAuraUpdate(); return ShrineOf(g, g.Player2); }

var classes = new (string Class, string Row, string[] Mates, string Companion)[]
{
    ("DRUID", "TRLA_209h_Druid", new[] { "TRLA_122", "TRLA_123", "TRLA_124", "TRLA_125", "TRLA_127" }, "TRLA_121"),
    ("HUNTER", "TRLA_209h_Hunter", new[] { "TRLA_165", "TRLA_166", "TRLA_167", "TRLA_168", "TRLA_169" }, "TRLA_164"),
    ("MAGE", "TRLA_209h_Mage", new[] { "TRLA_130", "TRLA_131", "TRLA_132", "TRLA_133", "TRLA_134" }, "TRLA_135"),
    ("PALADIN", "TRLA_209h_Paladin", new[] { "TRLA_140", "TRLA_141", "TRLA_142", "TRLA_143", "TRLA_144" }, "TRLA_139"),
};
string[] T1 = classes.SelectMany(c => c.Mates).Where(id => id != "TRLA_124" && id != "TRLA_125" && id != "TRLA_127").ToArray();

// ================================================================ A admission, registry, data
Guard("A", () =>
{
    AdmissionVerdict V(string id, AdmitScope scope, bool catalog = true) => DungeonRunAdmission.Classify(null, id, scope, null, catalog);
    var gen = new HashSet<string>(DungeonRunAdmission.GeneratedRejects(true));
    var genLegacy = new HashSet<string>(DungeonRunAdmission.GeneratedRejects(false));
    Check("A1 17 new T1 teammates (TRLA_124/125/127 were scripted with the shrine model)", T1.Length == 17, T1.Length.ToString());
    foreach (string id in T1)
    {
        var v = V(id, AdmitScope.Player);
        bool smuggler = id == "TRLA_168";
        Check($"A2 {id} {C(id).Name}: scripted, a ScriptedTeammate, uncollectible, in its class's teammate row",
            C(id).Implemented && DungeonRunShrines.ScriptedTeammates.Contains(id) && !C(id).Collectible
            && data.Teammates.TryGetValue(C(id).Class.ToString(), out var row) && row.Contains(id), C(id).Class.ToString());
        // Phase 8 family 01: Streetsmuggler's secret-generated row also admits a boss whose encounter row turns its generated
        // Secrets on (AdmissionRule.BossNeeds); the other teammates stay the run player's only.
        Check($"A3 {id}: admitted for the run player" + (smuggler ? " (its secret-generated row: catalog runs admit the run player, and since phase 8 a boss with the generated-Secrets need)" : " only (teammate row, never generated)"),
            v.Admitted && V(id, AdmitScope.Boss).Admitted == smuggler && v.Requires == null
            && (smuggler ? v.Family == "secret-generated" && !V(id, AdmitScope.Player, false).Admitted && v.Rule.BossNeeds == DungeonRunAdmission.BossNeedsGeneratedSecrets
                : v.Family == "teammate" && v.Rule.NeverGenerated && !V(id, AdmitScope.Generated).Admitted && gen.Contains(id) && genLegacy.Contains(id)),
            $"{v.Family} {v.Admitted}");
    }
    foreach (string e in new[] { "TRLA_124e", "TRLA_125e", "TRLA_131e", "TRLA_169e" })
        Check($"A4 enchantment {e} is scripted", C(e).Implemented && C(e).Type == CardType.ENCHANTMENT);
    Check("A5 Fireslinger casts the classic Fireball, Razzle Dazzler adds the classic Arcane Missiles (both scripted)",
        C(DungeonRunShrines.FireslingerSpell).Name == "Fireball" && C(DungeonRunShrines.FireslingerSpell).Implemented
        && C(DungeonRunShrines.DazzlerCard).Name == "Arcane Missiles" && C(DungeonRunShrines.DazzlerCard).Implemented);
    foreach (var (cls, row, mates, companion) in classes)
    {
        var s = NewRun(row, data.Class(row).Decks[0], 21);
        var pool = run.TeammatePool(s);
        // Phase 7 D scripted the companions: the pool is the class's five T1 teammates and its companion (start-of-game-draw, admitted).
        Check($"A6 a new {cls} Rikkar run's teammate pool is its five scripted teammates and its companion {companion} (phase 7 D: start-of-game-draw, admitted)",
            pool.SequenceEqual(mates.Append(companion).OrderBy(x => x, StringComparer.Ordinal)) && V(companion, AdmitScope.Player).Admitted
            && V(companion, AdmitScope.Player).Family == "start-of-game-draw", string.Join(",", pool));
    }
    var pending = data.Teammates.Values.SelectMany(v => v).Where(id => !DungeonRunShrines.ScriptedTeammates.Contains(id)).OrderBy(x => x, StringComparer.Ordinal).ToList();
    // Phase 7 T2 and D scripted the rest: no teammate is unscripted, and every one is admitted for the run player.
    Check("A7 no teammate is still unscripted (T2 and D scripted the rest); every one is admitted for the run player",
        pending.Count == 0 && data.Teammates.Values.SelectMany(v => v).All(id => V(id, AdmitScope.Player).Admitted), pending.Count + ": " + string.Join(",", pending.Take(6)));
});

// ================================================================ D Druid
Guard("D122", () =>
{
    var (s, plan, g) = F("rumble_druid_115", 101);
    Mana(g.Player1, 10); ClearExceptShrines(g);
    SetDeck(g, g.Player1, "CS2_231", "CS2_200", "CS2_182");
    var victim = Board(g, g.Player2, "CS2_171");
    var stomper = Mate(g, "TRLA_122");
    bool hit = Attack(g, stomper, victim);
    var ogre = g.Player1.BoardZone.FirstOrDefault(m => m.Card.Id == "CS2_200");
    Check("D1 TRLA_122 Direhorn Stomper: Rush; Overkill summons the deck's highest-Attack minion (Boulderfist Ogre over Yeti and Wisp)",
        hit && victim.IsDead && ogre != null && g.Player1.DeckZone.Count == 2 && !g.Player1.DeckZone.Any(p => p.Card.Id == "CS2_200"),
        $"{hit} deck {string.Join(",", g.Player1.DeckZone.Select(p => p.Card.Id))}");
    Check("D2 the summon is a summon: Gonk's Mark (TRLA_115) gives the Ogre +1/+1", ogre != null && ogre.AttackDamage == 7 && ogre.Health == 8,
        ogre == null ? "none" : $"{ogre.AttackDamage}/{ogre.Health}");
    // A full board: nothing is summoned, the deck is untouched.
    Mana(g.Player1, 10);
    var stomper2 = Mate(g, "TRLA_122");
    while (!g.Player1.BoardZone.IsFull) Board(g, g.Player1, "CS2_231");
    var victim2 = Board(g, g.Player2, "CS2_171");
    int deck = g.Player1.DeckZone.Count;
    Attack(g, stomper2, victim2);
    Check("D3 with a full board the Overkill summons nothing (the deck keeps its cards)", victim2.IsDead && g.Player1.DeckZone.Count == deck, $"deck {deck}->{g.Player1.DeckZone.Count}");
    // An exact kill is no Overkill.
    // On TRLA_112 (Gonk's Mark would make the played Stomper 5/7).
    var (s3, p3, g3) = F("rumble_druid_112", 102);
    Mana(g3.Player1, 10); ClearExceptShrines(g3); SetDeck(g3, g3.Player1, "CS2_200");
    var exact = Board(g3, g3.Player2, "CS2_182"); exact.Damage = 1; // 4/5 at 4 Health
    var st3 = Mate(g3, "TRLA_122");
    Attack(g3, st3, exact);
    Check("D4 an exact kill (4 into 4 Health) is no Overkill: nothing is summoned", exact.IsDead && g3.Player1.DeckZone.Count == 1);
});
Guard("D123", () =>
{
    var (s, plan, g) = F("rumble_druid_112", 103);
    Mana(g.Player1, 10); ClearExceptShrines(g); ClearHand(g, g.Player1);
    SetDeck(g, g.Player1, "CS2_200");
    var sky = Mate(g, "TRLA_123");
    int used = g.Player1.UsedMana, hand = g.Player1.HandZone.Count;
    bool powered = Power(g);
    var drawn = g.Player1.HandZone.FirstOrDefault(p => p.Card.Id == "CS2_200");
    Check("D5 TRLA_123 Succoring Skyscreamer: gaining Armor (Shapeshift) draws a card that costs (2) less (LOOT_204e)",
        powered && drawn != null && drawn.Cost == 4 && Ench(drawn).Any(e => e.Card.Id == "LOOT_204e") && g.Player1.HandZone.Count == hand + 1,
        drawn == null ? "no draw" : $"cost {drawn.Cost}");
    Check("D6 with TRLA_112 on the board the same Armor gain also refreshes the owner's Mana Crystals", used > 0 && g.Player1.UsedMana == 0,
        $"used {used} -> {g.Player1.UsedMana}");
    // A full hand: the drawn card burns and nothing is discounted.
    EndTurn(g); EndTurn(g); Mana(g.Player1, 10);
    SetDeck(g, g.Player1, "CS2_182");
    ClearHand(g, g.Player1); FillHand(g, g.Player1, "CS2_231");
    Power(g);
    var burned = g.Player1.GraveyardZone.FirstOrDefault(p => p.Card.Id == "CS2_182");
    Check("D7 with a full hand the draw burns and nothing is discounted", g.Player1.DeckZone.Count == 0 && burned != null && !Ench(burned).Any(e => e.Card.Id == "LOOT_204e")
        && g.Player1.HandZone.All(p => !Ench(p).Any(e => e.Card.Id == "LOOT_204e")), burned == null ? "not burned" : "burned");
    // Armor gained on the boss's side does not count.
    EndTurn(g); Mana(g.Player2, 10); int mine = g.Player1.HandZone.Count; SetDeck(g, g.Player1, "CS2_200");
    Play(g, ToHand(g, g.Player2, "EX1_606")); // Shield Block: the boss gains 5 Armor
    Check("D8 the boss gaining Armor draws nothing for the owner", g.Player1.DeckZone.Count == 1 && g.Player1.HandZone.Count == mine);
    // Dormant shrine: TRLA_112 refreshes nothing, the Skyscreamer still draws.
    EndTurn(g); Mana(g.Player1, 10); ClearHand(g, g.Player1); SetDeck(g, g.Player1, "CS2_200");
    Kill(g, ShrineOf(g, g.Player1));
    var sky2 = Mate(g, "TRLA_123"); int used2 = g.Player1.UsedMana;
    Power(g);
    Check("D9 with the shrine dormant the Skyscreamer still draws (discounted) and nothing refreshes the Mana",
        ShrineRules.IsDormantForm(ShrineOf(g, g.Player1)) && g.Player1.HandZone.Any(p => p.Card.Id == "CS2_200" && p.Cost == 4) && g.Player1.UsedMana == used2 + 2,
        $"used {used2}->{g.Player1.UsedMana}");
    // Revival: the shrine comes back at the start of the owner's third turn after its death, and its refresh with it.
    SetDeck(g, g.Player1, "CS2_200", "CS2_200", "CS2_200", "CS2_200");
    int deathTurn = g.Turn;
    for (int i = 0; i < 12 && !(ShrineRules.IsAwake(ShrineOf(g, g.Player1)) && g.CurrentPlayer == g.Player1); i++) EndTurn(g);
    Check("D10 the Halazzi's Gift shrine revives on the owner's third turn after its death", ShrineRules.IsAwake(ShrineOf(g, g.Player1)) && g.Turn == deathTurn + 6,
        $"turn {deathTurn}->{g.Turn}");
    Mana(g.Player1, 10); ClearHand(g, g.Player1);
    Play(g, ToHand(g, g.Player1, "CS2_231"));
    Power(g);
    Check("D11 after the revival one Armor gain both draws (discounted) and refreshes the Mana again", g.Player1.UsedMana == 0
        && g.Player1.HandZone.Count(p => p.Card.Id == "CS2_200" && p.Cost == 4) == 1, $"used {g.Player1.UsedMana}");
});

// ================================================================ H Hunter
Guard("H165", () =>
{
    var (s, plan, g) = F("rumble_hunter_162", 201);
    Mana(g.Player1, 10); ClearExceptShrines(g); ClearHand(g, g.Player1);
    SetDeck(g, g.Player1, "CS2_172", "CS2_231", "CS2_172", "DS1_185", "CS2_120");
    Mate(g, "TRLA_165");
    var hand = g.Player1.HandZone.Select(p => p.Card.Id).ToList();
    Check("H1 TRLA_165 Battlestarved Lynx: draws every Beast of the deck (two Bloodfen Raptors, a River Crocolisk) and nothing else",
        hand.Count == 3 && g.Player1.HandZone.All(p => p.Card.IsRace(Race.BEAST)) && g.Player1.DeckZone.All(p => !p.Card.IsRace(Race.BEAST)) && g.Player1.DeckZone.Count == 2,
        string.Join(",", hand));
    ClearHand(g, g.Player1);
    for (int i = 0; i < 6; i++) ToHand(g, g.Player1, "CS2_231");
    SetDeck(g, g.Player1, Enumerable.Repeat("CS2_172", 8).ToArray());
    Mate(g, "TRLA_165");
    Check("H2 until the hand is full: 6 cards in hand, 4 Beasts drawn, the rest stay in the deck", g.Player1.HandZone.Count == 10 && g.Player1.DeckZone.Count == 4,
        $"hand {g.Player1.HandZone.Count} deck {g.Player1.DeckZone.Count}");
    // Battle Totem: the second Battlecry draws on (a hand with room).
    var (s2, p2, g2) = F("rumble_hunter_162", 202, treasures: new[] { "LOOTA_846" });
    Mana(g2.Player1, 10); ClearExceptShrines(g2); ClearHand(g2, g2.Player1);
    SetDeck(g2, g2.Player1, "CS2_172", "CS2_172", "CS2_120");
    Mate(g2, "TRLA_165");
    Check("H3 with Battle Totem (LOOTA_846) both Battlecries run and the deck's Beasts are all drawn once", g2.Player1.HandZone.Count == 3 && g2.Player1.DeckZone.Count == 0);
});
Guard("H166", () =>
{
    var (s, plan, g) = F("rumble_hunter_162", 203);
    Mana(g.Player1, 10); ClearExceptShrines(g); ClearHand(g, g.Player1);
    var harbinger = Mate(g, "TRLA_166");
    int h0 = g.Player1.HandZone.Count;
    var yeti = Board(g, g.Player2, "CS2_182");
    Kill(g, yeti);
    var got = g.Player1.HandZone.Skip(h0).ToList();
    Check("H4 TRLA_166 Troll Harbinger: after a minion dies, a random Hunter spell joins the owner's hand (through the run's generation filter)",
        got.Count == 1 && got[0].Card.Type == CardType.SPELL && got[0].Card.Class == CardClass.HUNTER && !plan.ExcludedGenerated.Contains(got[0].Card.Id),
        string.Join(",", got.Select(p => p.Card.Id)));
    int h1 = g.Player1.HandZone.Count;
    Kill(g, ShrineOf(g, g.Player1));
    Check("H5 the run shrine's death (it goes dormant) counts as a minion death: one more Hunter spell",
        ShrineRules.IsDormantForm(ShrineOf(g, g.Player1)) && g.Player1.HandZone.Count == h1 + 1 && g.Player1.HandZone.Last().Card.Class == CardClass.HUNTER);
    // The shrine's revival is no death: with an empty deck (no turn draws), the hand stays as it is until it is back.
    SetDeck(g, g.Player1);
    int h3 = g.Player1.HandZone.Count;
    for (int i = 0; i < 12 && !(ShrineRules.IsAwake(ShrineOf(g, g.Player1)) && g.CurrentPlayer == g.Player1); i++) EndTurn(g);
    Check("H5b the shrine revives (a fresh entity, no death): no Hunter spell for it", ShrineRules.IsAwake(ShrineOf(g, g.Player1)) && g.Player1.HandZone.Count == h3,
        $"hand {h3}->{g.Player1.HandZone.Count}");
    Mana(g.Player1, 10);
    int h2 = g.Player1.HandZone.Count;
    Kill(g, harbinger);
    Check("H6 its own death adds nothing", g.Player1.HandZone.Count == h2);
    var (s2, p2, g2) = F("rumble_hunter_162", 203);
    Mana(g2.Player1, 10); ClearExceptShrines(g2); ClearHand(g2, g2.Player1);
    Mate(g2, "TRLA_166"); var y2 = Board(g2, g2.Player2, "CS2_182"); Kill(g2, y2);
    Check("H7 deterministic: the same run seed gives the same spell", g2.Player1.HandZone.FirstOrDefault()?.Card.Id == got[0].Card.Id);
});
Guard("H167", () =>
{
    var (s, plan, g) = F("rumble_hunter_163", 204);
    Mana(g.Player1, 10); ClearExceptShrines(g);
    var saurolisk = Mate(g, "TRLA_167");
    var yeti = Board(g, g.Player2, "CS2_182");
    var fixture = Fixture(g);
    int boss = g.Player2.Hero.Health;
    Kill(g, saurolisk);
    var form = ShrineOf(g, g.Player2);
    Check("H8 TRLA_167 Rabid Saurolisk: Deathrattle deals 1 to every enemy, and its Poisonous counts (the damage is the Saurolisk's): the boss's hero -1, "
        + "the 4/5 Yeti destroyed, the 1-Health fixture shrine dies and goes dormant",
        g.Player2.Hero.Health == boss - 1 && yeti.IsDead && ShrineRules.IsDormantForm(form), $"hero {boss}->{g.Player2.Hero.Health} yeti {yeti.Health} {yeti.Zone?.Type} {form?.Card.Id}");
    var yeti2 = Board(g, g.Player2, "CS2_182");
    var sau2 = Mate(g, "TRLA_167");
    int boss2 = g.Player2.Hero.Health;
    Kill(g, sau2);
    Check("H9 the dormant fixture form is untouchable: the second Deathrattle hits the hero and destroys the new Yeti only", ShrineRules.IsDormantForm(ShrineOf(g, g.Player2))
        && ShrineOf(g, g.Player2).Damage == 0 && g.Player2.Hero.Health == boss2 - 1 && yeti2.IsDead,
        $"{ShrineOf(g, g.Player2)?.Card.Id} dmg {ShrineOf(g, g.Player2)?.Damage} hero {boss2}->{g.Player2.Hero.Health} yeti {yeti2.Zone?.Type}");
    var sau3 = Ready(Mate(g, "TRLA_167"));
    var ogre = Board(g, g.Player2, "CS2_200");
    Attack(g, sau3, ogre);
    Check("H10 Poisonous: its 4 into a Boulderfist Ogre destroys it", ogre.IsDead && sau3.IsDead);
    var (s2, p2, g2) = F("rumble_hunter_163", 205, treasures: new[] { "LOOTA_845" });
    Mana(g2.Player1, 10); ClearExceptShrines(g2);
    var sau4 = Mate(g2, "TRLA_167"); var y2 = Board(g2, g2.Player2, "CS2_182"); int b2 = g2.Player2.Hero.Health;
    Kill(g2, sau4);
    Check("H11 with Totem of the Dead (LOOTA_845) the Deathrattle runs twice: the boss's hero -2 (the Yeti destroyed)", g2.Player2.Hero.Health == b2 - 2 && y2.IsDead, $"{b2}->{g2.Player2.Hero.Health} yeti {y2.Health}");
});
Guard("H168", () =>
{
    var (s, plan, g) = F("rumble_hunter_162", 206);
    Mana(g.Player1, 10); ClearExceptShrines(g);
    var smuggler = Mate(g, "TRLA_168");
    Mana(g.Player1, 10);
    Play(g, ToHand(g, g.Player1, "EX1_610")); // Explosive Trap
    Check("H12 setup: Explosive Trap is the owner's one Secret", g.Player1.SecretZone.Count == 1);
    EndTurn(g); Mana(g.Player2, 10);
    var boar = Board(g, g.Player2, "CS2_171");
    Attack(g, boar, g.Player1.Hero);
    var now = g.Player1.SecretZone.ToList();
    Check("H13 TRLA_168 Streetsmuggler: after the Trap is revealed, a random scripted Hunter Secret is put into the battlefield",
        boar.IsDead && now.Count == 1 && now[0].Card.IsSecret && now[0].Card.Class == CardClass.HUNTER && now[0].Card.Implemented && now[0].Zone == g.Player1.SecretZone,
        string.Join(",", now.Select(p => p.Card.Id)));
    // TRLA_110 Halazzi's Trap puts a Secret after a spell; a reveal of it is answered by the Smuggler.
    var (s2, p2, g2) = F("rumble_hunter_110", 207);
    Mana(g2.Player1, 10); ClearExceptShrines(g2);
    Mate(g2, "TRLA_168"); Mana(g2.Player1, 10);
    Play(g2, ToHand(g2, g2.Player1, "DS1_185"), g2.Player2.Hero);
    int secrets = g2.Player1.SecretZone.Count;
    Check("H14 with Halazzi's Trap (TRLA_110) a spell puts a Secret first", secrets == 1, secrets.ToString());
    // A revealed Secret of the boss does not count.
    EndTurn(g2); Mana(g2.Player2, 10);
    Play(g2, ToHand(g2, g2.Player2, "EX1_610"));
    EndTurn(g2); Mana(g2.Player1, 10);
    var mine = Ready(Board(g2, g2.Player1, "CS2_182"));
    int before = g2.Player1.SecretZone.Count;
    Attack(g2, mine, g2.Player2.Hero);
    Check("H15 the boss's Secret revealing puts nothing for the owner", g2.Player2.SecretZone.Count == 0 && g2.Player1.SecretZone.Count == before);
    Kill(g2, g2.Player1.BoardZone.First(m => m.Card.Id == "TRLA_168"));
    Check("H16 TRLA_168 is admitted for the run player of a catalog run (a board-shrine run is one)", s2.IsCatalogRun);
});
Guard("H169", () =>
{
    var (s, plan, g) = F("rumble_hunter_163", 208);
    Mana(g.Player1, 10); ClearExceptShrines(g);
    var wisp = Board(g, g.Player1, "CS2_231"); var yeti = Board(g, g.Player1, "CS2_182"); var enemy = Board(g, g.Player2, "CS2_182");
    var slam = Mate(g, "TRLA_169");
    Check("H17 TRLA_169 Slamma Jamma: the other friendly minions' Attack doubles (Wisp 2, Yeti 8; the shrine's 0 stays 0), not its own or the boss's",
        wisp.AttackDamage == 2 && yeti.AttackDamage == 8 && slam.AttackDamage == 5 && enemy.AttackDamage == 4 && ShrineOf(g, g.Player1).AttackDamage == 0
        && Ench(wisp).Any(e => e.Card.Id == "TRLA_169e"), $"{wisp.AttackDamage} {yeti.AttackDamage} {slam.AttackDamage} ench [{string.Join(",", Ench(wisp).Select(e => e.Card.Id))}] zone {slam.Zone?.Type} impl {C("TRLA_169").Implemented} bc {C("TRLA_169").Power?.PowerTask?.GetType().Name}");
    var (s2, p2, g2) = F("rumble_hunter_163", 209, treasures: new[] { "LOOTA_846" });
    Mana(g2.Player1, 10); ClearExceptShrines(g2);
    var y2 = Board(g2, g2.Player1, "CS2_182");
    Kill(g2, ShrineOf(g2, g2.Player1));
    Mate(g2, "TRLA_169");
    var form = ShrineOf(g2, g2.Player1);
    Check("H18 with Battle Totem the Battlecry runs twice (Yeti 4 -> 16); the dormant shrine form is not among 'your minions'",
        y2.AttackDamage == 16 && ShrineRules.IsDormantForm(form) && !Ench(form).Any(e => e.Card.Id == "TRLA_169e"), y2.AttackDamage.ToString());
});

// ================================================================ M Mage
Guard("M130", () =>
{
    var (s, plan, g) = F("rumble_mage_108", 301);
    Mana(g.Player1, 10); ClearExceptShrines(g); ClearHand(g, g.Player1);
    ToHand(g, g.Player1, "CS2_231"); ToHand(g, g.Player1, "CS2_231");
    var dazzler = Mate(g, "TRLA_130");
    var missiles = g.Player1.HandZone.Where(p => p.Card.Id == "EX1_277").ToList();
    Check("M0 TRLA_130 Razzle Dazzler: Taunt; the Battlecry fills the hand with Arcane Missiles (8 beside the two Wisps)",
        dazzler.HasTaunt && g.Player1.HandZone.Count == 10 && missiles.Count == 8 && missiles.All(p => p[GameTag.CREATOR] == dazzler.Id), $"{missiles.Count}");
    Check("M1 with Jan'alai's Mantle (TRLA_108) the shrine's Spell Damage follows the spells in hand: +8", ShrineOf(g, g.Player1).SpellPower == 8, ShrineOf(g, g.Player1).SpellPower.ToString());
    var d2 = Mate(g, "TRLA_130");
    Check("M2 a second Dazzler fills only the free slots (ToHand set two cards aside to fit it): the hand full, every other card a Missile",
        g.Player1.HandZone.Count == 10 && g.Player1.HandZone.All(p => p.Card.Id == "EX1_277" || p.Card.Id == "CS2_231"),
        string.Join(",", g.Player1.HandZone.Select(p => p.Card.Id)));
});
Guard("M131", () =>
{
    var (s, plan, g) = F("rumble_mage_129", 302);
    Mana(g.Player1, 10); ClearExceptShrines(g); ClearHand(g, g.Player1);
    var glyph = Mate(g, "TRLA_131");
    int sp = g.Player1.CurrentSpellPower;
    var yeti = Board(g, g.Player2, "CS2_182");
    Mana(g.Player1, 10);
    Play(g, ToHand(g, g.Player1, "CS2_024"), yeti); // Frostbolt
    Check("M3 TRLA_131 Glyph Guardian: a character is Frozen -> Spell Damage +1 (TRLA_131e)", yeti.IsFrozen && g.Player1.CurrentSpellPower == sp + 1
        && Ench(glyph).Count(e => e.Card.Id == "TRLA_131e") == 1, $"{sp}->{g.Player1.CurrentSpellPower}");
    Check("M4 with Jan'alai's Progeny (TRLA_129) the same freeze also makes a Frostfire", g.Player1.HandZone.Any(p => p.Card.Id == "TRLA_129s"));
    EndTurn(g); Mana(g.Player2, 10);
    var mine = Board(g, g.Player1, "CS2_182");
    Play(g, ToHand(g, g.Player2, "CS2_024"), mine);
    Check("M5 any freezer counts: the boss Freezing the owner's Wisp gives +1 more", g.Player1.CurrentSpellPower == sp + 2, g.Player1.CurrentSpellPower.ToString());
    Mana(g.Player2, 10);
    Play(g, ToHand(g, g.Player2, "EX1_332"), glyph); // Silence
    Check("M6 Silence removes the gained Spell Damage", g.Player1.CurrentSpellPower == sp - glyph.Card.SpellPower && !Ench(glyph).Any(e => e.Card.Id == "TRLA_131e"),
        g.Player1.CurrentSpellPower.ToString());
});
Guard("M132", () =>
{
    var (s, plan, g) = F("rumble_mage_128", 303);
    Mana(g.Player1, 10); ClearExceptShrines(g); ClearHand(g, g.Player1);
    Mate(g, "TRLA_132");
    var a = Board(g, g.Player2, "CS2_182"); var b = Board(g, g.Player2, "CS2_182");
    Mana(g.Player1, 10);
    Play(g, ToHand(g, g.Player1, "CS2_024"), a); // Frostbolt: a is Frozen
    var power = g.Player1.Hero.HeroPower;
    Power(g, a);
    Check("M7 TRLA_132 Frostweaver: the Hero Power aimed at a Frozen minion destroys it", a.IsDead, $"{a.Health} {a.Zone?.Type}");
    Check("M8 with Jan'alai's Flame (TRLA_128) that destroy is the Hero Power's kill: it is refreshed (and costs 0)", !power.IsExhausted && power.Cost == 0);
    Power(g, b);
    Check("M9 the next use Freezes an unfrozen target (and deals its 1); no kill, so no refresh", b.IsFrozen && b.Health == 4 && !b.IsDead && power.IsExhausted);
    EndTurn(g); EndTurn(g); Mana(g.Player1, 10);
    Power(g, g.Player2.Hero);
    Check("M10 a hero target is Frozen", g.Player2.Hero.IsFrozen, $"exh {power.IsExhausted} weaver {g.Player1.BoardZone.Count(m => m.Card.Id == "TRLA_132")} hp {g.Player2.Hero.Health}");
    // Without the Flame (TRLA_108 run): the destroy, then the power is spent; with Progeny (TRLA_129): its freeze makes a Frostfire.
    var (s2, p2, g2) = F("rumble_mage_129", 304);
    Mana(g2.Player1, 10); ClearExceptShrines(g2); ClearHand(g2, g2.Player1);
    Mate(g2, "TRLA_132"); var c2 = Board(g2, g2.Player2, "CS2_182");
    Mana(g2.Player1, 10);
    Power(g2, c2);
    Check("M11 with Jan'alai's Progeny (TRLA_129) the Hero Power's freeze is the owner's: a Frostfire", c2.IsFrozen && g2.Player1.HandZone.Count(p => p.Card.Id == "TRLA_129s") == 1);
    EndTurn(g2); Mana(g2.Player2, 10);
    var mine = Board(g2, g2.Player1, "CS2_231");
    g2.Process(HeroPowerTask.Any(g2.Player2, mine));
    Check("M12 the boss's Hero Power is not the owner's: no freeze", mine.IsDead || !mine.IsFrozen);
});
Guard("M133", () =>
{
    var (s, plan, g) = F("rumble_mage_129", 305);
    Mana(g.Player1, 10); ClearExceptShrines(g); ClearHand(g, g.Player1);
    Mate(g, "TRLA_133");
    int boss = g.Player2.Hero.Health, sp = g.Player1.CurrentSpellPower;
    Mana(g.Player1, 10);
    Play(g, ToHand(g, g.Player1, "GAME_005")); // The Coin
    Check("M13 TRLA_133 Fireslinger: after a spell, a Fireball hits a random enemy (the boss's hero, the only one): 6 damage, once",
        sp == 0 && g.Player2.Hero.Health == boss - 6, $"{boss}->{g.Player2.Hero.Health}");
    var yeti = Board(g, g.Player2, "CS2_182");
    int b2 = g.Player2.Hero.Health;
    Mana(g.Player1, 10); Play(g, ToHand(g, g.Player1, "GAME_005"));
    Check("M14 with an enemy minion the target is random among the enemies (one of them took 6)", yeti.IsDead ^ (g.Player2.Hero.Health == b2 - 6), $"yeti {yeti.IsDead} hero {b2}->{g.Player2.Hero.Health}");
    // Two Fireslingers: each answers the hand cast, neither answers the other's Fireball (an effect's cast raises no event).
    ClearExceptShrines(g);
    int b3 = g.Player2.Hero.Health;
    Board(g, g.Player1, "TRLA_133"); Board(g, g.Player1, "TRLA_133");
    Mana(g.Player1, 10); Play(g, ToHand(g, g.Player1, "GAME_005"));
    Check("M15 two Fireslingers: exactly two Fireballs (12), no chain", g.Player2.Hero.Health == b3 - 12, $"{b3}->{g.Player2.Hero.Health}");
    // Spell Damage: Robe of the Magi (+3) and Jan'alai's Mantle (+1 per spell in hand).
    var (s2, p2, g2) = F("rumble_mage_108", 306, treasures: new[] { "LOOTA_825" });
    Mana(g2.Player1, 10); ClearExceptShrines(g2); ClearHand(g2, g2.Player1);
    Mate(g2, "TRLA_133");
    ToHand(g2, g2.Player1, "CS2_024"); ToHand(g2, g2.Player1, "CS2_024");
    int b4 = g2.Player2.Hero.Health;
    Mana(g2.Player1, 10); Play(g2, ToHand(g2, g2.Player1, "GAME_005"));
    Check("M16 the owner's Spell Damage applies: Robe of the Magi +3 and Jan'alai's Mantle +2 (two spells in hand) -> 11", g2.Player2.Hero.Health == b4 - 11,
        $"{b4}->{g2.Player2.Hero.Health}");
    EndTurn(g2); Mana(g2.Player2, 10);
    int me = g2.Player1.Hero.Health, b5 = g2.Player2.Hero.Health;
    Play(g2, ToHand(g2, g2.Player2, "GAME_005"));
    Check("M17 the boss casting a spell casts nothing", g2.Player1.Hero.Health == me && g2.Player2.Hero.Health == b5);
});
Guard("M134", () =>
{
    var (s, plan, g) = F("rumble_mage_108", 307);
    Mana(g.Player1, 10); ClearExceptShrines(g); ClearHand(g, g.Player1);
    SetDeck(g, g.Player1, "CS2_231", "CS2_024", "CS2_182");
    var victim = Board(g, g.Player2, "CS2_171");
    var conj = Mate(g, "TRLA_134");
    bool hit = Attack(g, conj, victim);
    Check("M18 TRLA_134 Showstopping Conjurer: Rush; Overkill draws a spell from the deck (the one Frostbolt)",
        hit && victim.IsDead && g.Player1.HandZone.Count == 1 && g.Player1.HandZone[0].Card.Id == "CS2_024" && g.Player1.DeckZone.Count == 2);
    var v2 = Board(g, g.Player2, "CS2_171"); var c2 = Mate(g, "TRLA_134");
    Attack(g, c2, v2);
    Check("M19 no spell left in the deck: nothing is drawn", g.Player1.HandZone.Count == 1 && g.Player1.DeckZone.Count == 2);
});

// ================================================================ P Paladin
Guard("P140", () =>
{
    var (s, plan, g) = F("rumble_paladin_138", 401);
    Mana(g.Player1, 10); ClearExceptShrines(g); ClearHand(g, g.Player1);
    SetDeck(g, g.Player1, "CS2_231", "CS2_089", "CS2_024", "CS2_182", "CS2_029");
    Mate(g, "TRLA_140");
    Check("P1 TRLA_140 Blessed One: draws every spell of the deck (three) and no minion", g.Player1.HandZone.Count == 3 && g.Player1.HandZone.All(p => p.Card.Type == CardType.SPELL)
        && g.Player1.DeckZone.Count == 2 && g.Player1.DeckZone.All(p => p.Card.Type == CardType.MINION));
    for (int i = 0; i < 4; i++) ToHand(g, g.Player1, "CS2_231");
    SetDeck(g, g.Player1, Enumerable.Repeat("CS2_024", 6).ToArray());
    Mate(g, "TRLA_140");
    Check("P2 until the hand is full (7 cards: 3 drawn, 3 left)", g.Player1.HandZone.Count == 10 && g.Player1.DeckZone.Count == 3, $"{g.Player1.HandZone.Count}/{g.Player1.DeckZone.Count}");
});
Guard("P141", () =>
{
    var (s, plan, g) = F("rumble_paladin_138", 402);
    Mana(g.Player1, 10); ClearExceptShrines(g);
    Mate(g, "TRLA_141");
    var a = Board(g, g.Player2, "CS2_182"); var b = Board(g, g.Player2, "CS2_200");
    Mana(g.Player1, 10);
    Play(g, ToHand(g, g.Player1, "CS2_089"), g.Player1.Hero); // Holy Light on a full-Health hero
    Check("P3 TRLA_141 Lightchucker: a heal that heals nothing (full Health) destroys nothing", !a.IsDead && !b.IsDead);
    g.Player1.Hero.Damage = 10;
    Mana(g.Player1, 10);
    Play(g, ToHand(g, g.Player1, "CS2_089"), g.Player1.Hero);
    Check("P4 after a character is healed, a random enemy minion is destroyed (one of the two)", a.IsDead ^ b.IsDead && g.Player1.Hero.Damage < 10);
    var lone = Fixture(g);
    var survivor = a.IsDead ? b : a; Kill(g, survivor);
    lone = ShrineOf(g, g.Player2);
    g.Player2.Hero.Damage = 5; Mana(g.Player1, 10);
    Play(g, ToHand(g, g.Player1, "CS2_089"), g.Player2.Hero);
    Check("P5 any character counts (the boss's hero healed); the boss's TRLA_107 fixture, its only minion, is destroyed and goes dormant",
        ShrineRules.IsDormantForm(ShrineOf(g, g.Player2)), ShrineOf(g, g.Player2)?.Card.Id);
    g.Player1.Hero.Damage = 5; Mana(g.Player1, 10);
    Play(g, ToHand(g, g.Player1, "CS2_089"), g.Player1.Hero);
    Check("P6 the dormant form is untouchable: no enemy minion to destroy, nothing happens", ShrineRules.IsDormantForm(ShrineOf(g, g.Player2)));
    // Fortified Spirit (TRLA_803): the shrine's turn-start full heal is a heal.
    var (s2, p2, g2) = F("rumble_paladin_138", 403, upgrades: new[] { "TRLA_803" });
    Mana(g2.Player1, 10); ClearExceptShrines(g2);
    Mate(g2, "TRLA_141");
    var shrine = ShrineOf(g2, g2.Player1);
    shrine.Damage = 2;
    var e1 = Board(g2, g2.Player2, "CS2_182");
    EndTurn(g2); EndTurn(g2);
    Check("P7 with Fortified Spirit (TRLA_803) the shrine's turn-start full heal destroys a random enemy minion", shrine.Damage == 0 && e1.IsDead, $"dmg {shrine.Damage} {e1.IsDead}");
});
Guard("P142", () =>
{
    var (s, plan, g) = F("rumble_paladin_138", 404);
    Mana(g.Player1, 10); ClearExceptShrines(g);
    var fort = Mate(g, "TRLA_142");
    EndTurn(g); Mana(g.Player2, 10);
    Play(g, ToHand(g, g.Player2, "CS2_029"), fort); // Fireball
    Check("P8 TRLA_142 The Walking Fort: Taunt; a 6-damage Fireball takes 1", fort.HasTaunt && fort.Health == 4 && !fort.IsDead, fort.Health.ToString());
    var ogre = Ready(Board(g, g.Player2, "CS2_200"));
    Attack(g, ogre, fort);
    Check("P9 a 6-Attack Ogre's hit takes 1; the Fort's 2 is dealt back", fort.Health == 3 && ogre.Health == 5);
    Mana(g.Player2, 10);
    Play(g, ToHand(g, g.Player2, "EX1_332"), fort); // Silence
    Mana(g.Player2, 10);
    Play(g, ToHand(g, g.Player2, "CS2_029"), fort);
    Check("P10 Silenced, it takes the whole hit and dies", fort.IsDead);
    // Shirvallah's Protection (TRLA_105): a friendly minion that survives damage gets Divine Shield.
    var (s2, p2, g2) = F("rumble_paladin_105", 405);
    Mana(g2.Player1, 10); ClearExceptShrines(g2);
    var f2 = Mate(g2, "TRLA_142");
    EndTurn(g2); Mana(g2.Player2, 10);
    Play(g2, ToHand(g2, g2.Player2, "CS2_029"), f2);
    Check("P11 with Shirvallah's Protection (TRLA_105) the capped hit leaves it alive, so it gets Divine Shield", f2.Health == 4 && f2.HasDivineShield);
    Mana(g2.Player2, 10);
    Play(g2, ToHand(g2, g2.Player2, "CS2_029"), f2);
    Check("P12 the next hit pops the shield (no damage)", f2.Health == 4 && !f2.HasDivineShield);
});
Guard("P143", () =>
{
    var (s, plan, g) = F("rumble_paladin_138", 406);
    Mana(g.Player1, 10); ClearExceptShrines(g);
    var glory = Mate(g, "TRLA_143");
    EndTurn(g); Mana(g.Player2, 10);
    int me = g.Player1.Hero.Health;
    Play(g, ToHand(g, g.Player2, "CS2_029"), glory);
    Check("P13 TRLA_143 Gloryseeker: a Fireball on it hits the owner's hero instead (hero -6, the 3/1 untouched)",
        !glory.IsDead && glory.Damage == 0 && g.Player1.Hero.Health == me - 6, $"{me}->{g.Player1.Hero.Health} dmg {glory.Damage}");
    g.Player1.Hero.Armor = 4;
    int me2 = g.Player1.Hero.Health;
    Mana(g.Player2, 10); Play(g, ToHand(g, g.Player2, "CS2_029"), glory);
    Check("P14 the hero's Armor absorbs first (4 Armor, 2 Health)", g.Player1.Hero.Armor == 0 && g.Player1.Hero.Health == me2 - 2 && glory.Damage == 0);
    var cobra = Ready(Board(g, g.Player2, "EX1_170")); // Emperor Cobra 2/3 Poisonous
    int me3 = g.Player1.Hero.Health;
    Attack(g, cobra, glory);
    Check("P15 a Poisonous attacker's hit goes to the hero: the Gloryseeker survives, its 3 kills the Cobra",
        !glory.IsDead && g.Player1.Hero.Health == me3 - 2 && cobra.IsDead);
    // Shirvallah's Vengeance (TRLA_137): after the owner's hero takes damage, 5 to the boss's hero.
    var (s2, p2, g2) = F("rumble_paladin_137", 407);
    Mana(g2.Player1, 10); ClearExceptShrines(g2);
    var gl2 = Mate(g2, "TRLA_143");
    EndTurn(g2); Mana(g2.Player2, 10);
    int me4 = g2.Player1.Hero.Health, boss = g2.Player2.Hero.Health;
    Play(g2, ToHand(g2, g2.Player2, "CS2_029"), gl2);
    Check("P16 with Shirvallah's Vengeance (TRLA_137) the redirected hit is hero damage: the boss's hero takes 5",
        g2.Player1.Hero.Health == me4 - 6 && g2.Player2.Hero.Health == boss - 5 && !gl2.IsDead, $"{me4}->{g2.Player1.Hero.Health} boss {boss}->{g2.Player2.Hero.Health}");
    Mana(g2.Player2, 10);
    Play(g2, ToHand(g2, g2.Player2, "EX1_332"), gl2);
    Mana(g2.Player2, 10);
    Play(g2, ToHand(g2, g2.Player2, "CS2_029"), gl2);
    Check("P17 Silenced, it takes the hit itself and dies", gl2.IsDead);
});
Guard("P144", () =>
{
    var (s, plan, g) = F("rumble_paladin_138", 408);
    Mana(g.Player1, 10); ClearExceptShrines(g);
    var exactor = Ready(Mate(g, "TRLA_144"));
    var boar = Board(g, g.Player2, "CS2_171");
    Check("P18 setup: Exactor of Justice has Divine Shield", exactor.HasDivineShield);
    Attack(g, exactor, boar);
    Check("P19 TRLA_144 Exactor of Justice: the Boar pops the shield, the Overkill gives it back", boar.IsDead && exactor.HasDivineShield && exactor.Damage == 0);
    EndTurn(g); EndTurn(g);
    var fixture = Fixture(g);
    exactor.IsExhausted = false; exactor.HasDivineShield = false;
    Attack(g, exactor, fixture);
    Check("P20 an Overkill on the boss's TRLA_107 fixture shrine (1 Health, Taunt): Divine Shield again; the fixture goes dormant",
        exactor.HasDivineShield && ShrineRules.IsDormantForm(ShrineOf(g, g.Player2)));
    var yeti = Board(g, g.Player2, "CS2_182");
    EndTurn(g); EndTurn(g);
    exactor.HasDivineShield = false; exactor.IsExhausted = false;
    yeti.Damage = 0; // 4/5: the Exactor's 5 is exact
    Attack(g, exactor, yeti);
    Check("P21 an exact kill is no Overkill: no shield", yeti.IsDead && !exactor.HasDivineShield);
});

// ================================================================ R the teammate draft through the run flow
DungeonRunState Walk(DungeonRunState s, Func<DungeonRunState, bool> stop, List<string> log)
{
    var scratch = new DungeonRunProfile();
    for (int guard = 0; guard < 500 && !stop(s) && !s.Finished; guard++)
    {
        switch (s.Phase)
        {
            case DungeonRunPhase.PreMatch: run.BeginMatch(s); run.RecordOutcome(s, scratch, DungeonRunOutcome.Win); break;
            case DungeonRunPhase.TreasureDraft:
                log?.Add($"f{s.Floor}:{s.OpenDraft.Kind}[{string.Join(",", s.OpenDraft.Offered)}]");
                run.PickTreasure(s, s.OpenDraft.Offered[0]); break;
            case DungeonRunPhase.Modifier: run.PickModifier(s, s.ModifierOffer.Offered[0]); break;
            case DungeonRunPhase.Tavern: run.LeaveTavern(s); break;
            case DungeonRunPhase.BundleDraft:
                if (s.OpenDraft.Takes == 0) { run.PickBundle(s, s.OpenDraft.Offered[0]); break; }
                string bundle = s.OpenDraft.Choosing ?? s.OpenDraft.Offered[0];
                var left = run.OfferCards(s.OpenDraft, bundle);
                foreach (string kept in s.OpenDraft.Chosen) left.Remove(kept);
                run.PickDraftCard(s, bundle, left[0]);
                break;
            default: throw new Exception("walk stopped at " + s.Phase);
        }
    }
    return s;
}
foreach (var (cls, row, mates, companion) in classes)
    Guard("R-" + cls, () =>
    {
        var s = NewRun(row, data.Class(row).Decks[1], 600 + Array.FindIndex(classes, c => c.Class == cls));
        var log = new List<string>();
        Walk(s, x => x.Floor > 13, log);
        var offers = log.Where(l => l.Contains(":teammate[")).ToList();
        var taken = s.Cards.Where(c => c.Origin == DungeonRunDeck.OriginTeammate).Select(c => c.Id).ToList();
        var sets = offers.Select(l => l.Substring(l.IndexOf('[') + 1).TrimEnd(']').Split(',').ToList()).ToList();
        // Phase 7 D: the pool is six (the companion included), so the fourth draft still offers 3.
        var six = mates.Append(companion).ToArray();
        bool sized = sets.Count == 4 && sets.Select(x => x.Count).SequenceEqual(new[] { 3, 3, 3, 3 });
        bool fresh = sets.Select((x, i) => x.All(id => !taken.Take(i).Contains(id))).All(b => b);
        Check($"R1 {cls}: four teammate drafts (I.2, I.4, II.3, III.3) offer 3 each of its six teammates (the companion included), never one already taken",
            sized && fresh && sets.All(x => x.All(six.Contains)) && string.Join(",", offers.Select(l => l.Substring(0, l.IndexOf(':')))) == "f2,f4,f8,f13",
            string.Join(" ", offers));
        Check($"R2 {cls}: the four picks are distinct deck records with origin \"teammate\"", taken.Count == 4 && taken.Distinct().Count() == 4 && taken.All(six.Contains), string.Join(",", taken));
        var plan = run.BuildPlan(s);
        Check($"R3 {cls}: the next fight's plan deck holds the four teammates", taken.All(id => plan.PlayerDeck.Any(c => c.Id == id)));
        var g = Start(plan, run.MatchSeed(s));
        g.Process(ChooseTask.Mulligan(g.Player1, new List<int>())); g.Process(ChooseTask.Mulligan(g.Player2, new List<int>()));
        g.MainBegin();
        var inGame = g.Player1.DeckZone.Concat(g.Player1.HandZone).Select(p => p.Card.Id).ToList();
        Check($"R4 {cls}: the match deals them to the run player (deck or hand), with the board shrine on the board",
            taken.All(inGame.Contains) && ShrineRules.Of(g.Player1).Count >= 1, string.Join(",", taken.Where(id => !inGame.Contains(id))));
        // Play a drafted teammate dealt from the run deck.
        string first = taken[0];
        var card = g.Player1.HandZone.FirstOrDefault(p => p.Card.Id == first) ?? Generic.Draw(g.Player1, g.Player1.DeckZone.First(p => p.Card.Id == first));
        Mana(g.Player1, 10);
        ICharacter target = null;
        bool played = card != null && card.Zone == g.Player1.HandZone && Play(g, card, target);
        Check($"R5 {cls}: the drafted {first} plays from the run deck onto the board", played && card.Zone == g.Player1.BoardZone, card?.Zone?.Type.ToString());
    });
Guard("R-resume", () =>
{
    var s = NewRun("TRLA_209h_Mage", "rumble_mage_108", 650);
    Walk(s, x => x.Phase == DungeonRunPhase.TreasureDraft && x.OpenDraft.Kind == DungeonRun.TeammateKind, null);
    var draft = s.OpenDraft;
    var ser = new System.Xml.Serialization.XmlSerializer(typeof(DungeonRunSave));
    var sw = new StringWriter(); ser.Serialize(sw, new DungeonRunSave { Active = s });
    var back = ((DungeonRunSave)ser.Deserialize(new StringReader(sw.ToString()))).Active;
    run.Resume(back);
    Check("R6 a Mage run saved with its floor-2 teammate draft open resumes with the same stored offer",
        back.Floor == 2 && back.OpenDraft?.Kind == "teammate" && back.OpenDraft.Offered.SequenceEqual(draft.Offered) && draft.Offered.Count == 3
        && draft.Offered.All(id => classes[2].Mates.Contains(id)), string.Join(",", draft.Offered));
    string pick = back.OpenDraft.Offered[1];
    run.PickTreasure(back, pick);
    Check("R7 the pick after the resume joins the deck", back.Cards.Last().Id == pick && back.Cards.Last().Origin == DungeonRunDeck.OriginTeammate);
    var errors = DungeonRunValidator.Validate(data, null);
    Check("R8 the validator passes the shipped data (teammate table, registry)", errors.Count == 0, string.Join(" | ", errors.Take(3)));
});

Console.WriteLine($"SUMMARY checks passed {passes} failed {failures}");
return failures == 0 ? 0 : 1;
