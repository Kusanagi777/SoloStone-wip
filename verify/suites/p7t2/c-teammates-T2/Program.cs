// c-teammates-T2: phase 7 family P7-T2, the Priest, Rogue, Shaman, Warlock and Warrior teammates: TRLA_149-153 (Priest),
// TRLA_189-193 (Rogue), TRLA_157-161 (Shaman), TRLA_181-185 (Warlock), TRLA_173-177 (Warrior), and the client enchantment records
// TRLA_153e/174e/176e (plus GBL_008e, TRLA_191's discount). Every game plays a real engine game through the run setup path
// (NewRun -> BuildPlan -> DungeonRunSetup.Configure -> Game -> ApplyAfterStart -> mulligan -> MainBegin), on the class's Rikkar row
// with a board shrine.
//   A admission, registry and data: scripted, admitted for the run player only, never generated; the hidden-steal and
//     not-started-in-deck rows; the class pools.
//   E/O/S/W/X each teammate's rule in real games, with its interactions: the class's board shrines (TRLA_114/146/147, TRLA_106/186/
//     187, TRLA_109/154/155, TRLA_113/178/179, TRLA_104/170/171), shrine dormancy and revival, the TRLA_107 boss fixture, shrine
//     upgrades TRLA_801/805, treasures LOOTA_846 (Battle Totem) and LOOTA_845 (Totem of the Dead); the boss's fair view and its
//     own-deck ledger after the steals (TRLA_152, TRLA_190).
//   Q the teammate draft through the run flow for the five classes: offers, no repeats, deck records, the plan, the match deck, a
//     drafted teammate played from the run deck.
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
void ClearHand(Game g, Controller c) { foreach (var p in c.HandZone.ToArray()) { c.HandZone.Remove(p); c.SetasideZone.Add(p); } g.DeathProcessingAndAuraUpdate(); }
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
Minion Mate(Game g, string id, ICharacter target = null) { Mana(g.Player1, 10); var m = (Minion)ToHand(g, g.Player1, id); Play(g, m, target); return m; }
Minion Fixture(Game g) { ShrineRules.Place(g.Player2, DungeonRunShrines.FixtureShrine); g.DeathProcessingAndAuraUpdate(); return ShrineOf(g, g.Player2); }
// A generated card passed the run's generation filter (RunMatchPlan.ExcludedGenerated: unscripted cards, run rewards, teammates).
bool Generated(RunMatchPlan plan, IPlayable p) => !plan.ExcludedGenerated.Contains(p.Card.Id);
// Plays a teammate with 10 Mana, keeping the run player's Overload state (Mate resets it).
Minion MateKeep(Game g, string id) { g.Player1.BaseMana = 10; g.Player1.UsedMana = 0; var m = (Minion)ToHand(g, g.Player1, id); Play(g, m); return m; }
HashSet<int> DeckIds(Controller c) => new HashSet<int>(c.DeckZone.Select(p => p.Id));
int Pick(Game g, Controller c)
{
    int picks = 0;
    for (int i = 0; i < 12 && c.Choice != null && g.State != State.COMPLETE; i++) { g.Process(ChooseTask.Pick(c, c.Choice.Choices[0])); picks++; }
    return picks;
}

var classes = new (string Class, string Row, string[] Mates, string Companion)[]
{
    ("PRIEST", "TRLA_209h_Priest", new[] { "TRLA_149", "TRLA_150", "TRLA_151", "TRLA_152", "TRLA_153" }, "TRLA_148"),
    ("ROGUE", "TRLA_209h_Rogue", new[] { "TRLA_189", "TRLA_190", "TRLA_191", "TRLA_192", "TRLA_193" }, "TRLA_188"),
    ("SHAMAN", "TRLA_209h_Shaman", new[] { "TRLA_157", "TRLA_158", "TRLA_159", "TRLA_160", "TRLA_161" }, "TRLA_156"),
    ("WARLOCK", "TRLA_209h_Warlock", new[] { "TRLA_181", "TRLA_182", "TRLA_183", "TRLA_184", "TRLA_185" }, "TRLA_180"),
    ("WARRIOR", "TRLA_209h_Warrior", new[] { "TRLA_173", "TRLA_174", "TRLA_175", "TRLA_176", "TRLA_177" }, "TRLA_172"),
};
string[] T2 = classes.SelectMany(c => c.Mates).ToArray();

// ================================================================ A admission, registry, data
Guard("A", () =>
{
    AdmissionVerdict V(string id, AdmitScope scope, bool catalog = true) => DungeonRunAdmission.Classify(null, id, scope, null, catalog);
    var gen = new HashSet<string>(DungeonRunAdmission.GeneratedRejects(true));
    var genLegacy = new HashSet<string>(DungeonRunAdmission.GeneratedRejects(false));
    Check("A1 25 T2 teammates", T2.Length == 25 && T2.Distinct().Count() == 25);
    foreach (string id in T2)
    {
        var v = V(id, AdmitScope.Player);
        string family = id == "TRLA_152" || id == "TRLA_190" ? "hidden-steal" : id == "TRLA_191" ? "not-started-in-deck" : "teammate";
        Check($"A2 {id} {C(id).Name}: scripted, a ScriptedTeammate, uncollectible, in its class's teammate row",
            C(id).Implemented && DungeonRunShrines.ScriptedTeammates.Contains(id) && !C(id).Collectible
            && data.Teammates.TryGetValue(C(id).Class.ToString(), out var row) && row.Contains(id), C(id).Class.ToString());
        Check($"A3 {id}: admitted for the run player only ({family} row), never generated, no pending requirement",
            v.Admitted && v.Family == family && v.Rule.NeverGenerated && v.Requires == null && !V(id, AdmitScope.Boss).Admitted
            && !V(id, AdmitScope.Generated).Admitted && gen.Contains(id) && genLegacy.Contains(id) && V(id, AdmitScope.Player, false).Admitted,
            $"{v.Family} {v.Admitted} {v.Requires}");
    }
    var steal = DungeonRunAdmission.RuleFor("TRLA_152");
    Check("A4 the teammates' hidden-steal row holds TRLA_152 and TRLA_190 (TRLA_186, the shrine, keeps its own) and states the hidden zones (the boss's deck and hand, its own-deck ledger, the masked hand, the hand-transfer facts)",
        steal.Ids.SequenceEqual(new[] { "TRLA_152", "TRLA_190" }) && steal.Admit == AdmitScope.Player && steal.Lift == DungeonRunAdmission.RunTeammateLift
        && steal.Reason.Contains("OwnDeckLedger") && steal.Reason.Contains("masks") && steal.Reason.Contains("LOOTA_834") && DungeonRunAdmission.RuleFor("TRLA_190") == steal
        && DungeonRunAdmission.RuleFor("TRLA_186") != steal && DungeonRunAdmission.RuleFor("TRLA_186").Family == "hidden-steal");
    var harpoon = DungeonRunAdmission.RuleFor("TRLA_191");
    Check("A5 TRLA_191's row names SelfCondition.IsNotStartInDeck and no pending requirement", harpoon.Reason.Contains("SelfCondition.IsNotStartInDeck") && harpoon.Requires == null
        && harpoon.Ids.SequenceEqual(new[] { "TRLA_191" }));
    Check("A6 no teammate row is pending any more (the companions' start-of-game-draw row is phase 7 D's)",
        DungeonRunAdmission.Rules.Where(r => r.Family == "teammate").All(r => r.Admit == AdmitScope.Player && r.Requires == null));
    foreach (string e in new[] { "TRLA_153e", "TRLA_174e", "TRLA_176e", DungeonRunShrines.HarpoonerDiscount })
        Check($"A7 enchantment {e} is scripted", C(e).Implemented && C(e).Type == CardType.ENCHANTMENT && C(e).Text != null, C(e).Text);
    foreach (var (cls, row, mates, companion) in classes)
    {
        var s = NewRun(row, data.Class(row).Decks[0], 21);
        var pool = run.TeammatePool(s);
        // Phase 7 D scripted the companions: the pool is the class's five T2 teammates and its companion (start-of-game-draw, admitted).
        Check($"A8 a new {cls} Rikkar run's teammate pool is its five scripted teammates and its companion {companion} (phase 7 D: start-of-game-draw, admitted)",
            pool.SequenceEqual(mates.Append(companion).OrderBy(x => x, StringComparer.Ordinal)) && V(companion, AdmitScope.Player).Admitted
            && V(companion, AdmitScope.Player).Family == "start-of-game-draw", string.Join(",", pool));
    }
    var pending = data.Teammates.Values.SelectMany(v => v).Where(id => !DungeonRunShrines.ScriptedTeammates.Contains(id)).OrderBy(x => x, StringComparer.Ordinal).ToList();
    // Phase 7 D scripted the nine companions: no teammate is unscripted any more.
    Check("A9 no teammate is still unscripted (phase 7 D scripted the nine companions); every one is admitted for the run player",
        pending.Count == 0 && data.Teammates.Values.SelectMany(v => v).All(id => V(id, AdmitScope.Player).Admitted), string.Join(",", pending));
    var errors = DungeonRunValidator.Validate(data, null);
    Check("A10 the validator passes the shipped data (teammate table, registry)", errors.Count == 0, string.Join(" | ", errors.Take(3)));
});

// ================================================================ E Priest
Guard("E149", () =>
{
    var (s, plan, g) = F("rumble_priest_146", 501);
    Mana(g.Player1, 10); ClearExceptShrines(g);
    var coil = Mate(g, "TRLA_149");
    Kill(g, coil);
    var summoned = g.Player1.BoardZone.Where(m => !ShrineRules.IsMarked(m)).ToList();
    Check("E1 TRLA_149 Ghastcoiler: its Deathrattle summons 2 random Deathrattle minions (scripted, through the run's generation filter)",
        summoned.Count == 2 && summoned.All(m => m.Card[GameTag.DEATHRATTLE] != 0 && Generated(plan, m)), string.Join(",", summoned.Select(m => m.Card.Id)));
    var (s2, p2, g2) = F("rumble_priest_146", 501);
    Mana(g2.Player1, 10); ClearExceptShrines(g2);
    Kill(g2, Mate(g2, "TRLA_149"));
    Check("E2 deterministic: the same run seed summons the same two", g2.Player1.BoardZone.Where(m => !ShrineRules.IsMarked(m)).Select(m => m.Card.Id).SequenceEqual(summoned.Select(m => m.Card.Id)));
    // Bwonsamdi's Calling (TRLA_114): Deathrattles trigger two extra times -> 6 summons, the board (shrine + 6) full.
    var (s3, p3, g3) = F("rumble_priest_114", 502);
    Mana(g3.Player1, 10); ClearExceptShrines(g3);
    Kill(g3, Mate(g3, "TRLA_149"));
    Check("E3 with TRLA_114 (Deathrattles two extra times) three Deathrattles summon 6: the board (shrine + 6) is full",
        g3.Player1.BoardZone.Count == 7 && g3.Player1.BoardZone.Count(m => !ShrineRules.IsMarked(m)) == 6, g3.Player1.BoardZone.Count.ToString());
    // A board full at its death: one slot (its own) is free, one minion is summoned.
    var (s4, p4, g4) = F("rumble_priest_146", 503);
    Mana(g4.Player1, 10); ClearExceptShrines(g4);
    var c4 = Mate(g4, "TRLA_149");
    while (!g4.Player1.BoardZone.IsFull) Board(g4, g4.Player1, "CS2_231");
    Kill(g4, c4);
    Check("E4 a full board at its death: its own slot is the only one free, one minion is summoned",
        g4.Player1.BoardZone.IsFull && g4.Player1.BoardZone.Count(m => m.Card.Id != "CS2_231" && !ShrineRules.IsMarked(m)) == 1);
});
Guard("E150", () =>
{
    var (s, plan, g) = F("rumble_priest_146", 504);
    Mana(g.Player1, 10); ClearExceptShrines(g); ClearHand(g, g.Player1);
    var boar = Board(g, g.Player2, "CS2_171");
    var medic = Mate(g, "TRLA_150");
    int h0 = g.Player1.HandZone.Count;
    bool hit = Attack(g, medic, boar);
    var got = g.Player1.HandZone.Skip(h0).ToList();
    Check("E5 TRLA_150 Bloodwash Medic: Rush; its Overkill adds 3 healing spells (spells that restore Health, through the run's generation filter)",
        hit && boar.IsDead && got.Count == 3 && got.All(p => DungeonRunShrines.IsHealingSpell(p.Card) && Generated(plan, p)), string.Join(",", got.Select(p => p.Card.Id)));
    var yeti = Board(g, g.Player2, "CS2_182"); yeti.Damage = 1; // 4 Health: the Medic's 3 is no kill
    var m2 = Mate(g, "TRLA_150"); int h1 = g.Player1.HandZone.Count;
    Attack(g, m2, yeti);
    Check("E6 no kill, no Overkill: nothing is added", g.Player1.HandZone.Count == h1 && !yeti.IsDead);
});
Guard("E151", () =>
{
    var (s, plan, g) = F("rumble_priest_146", 505);
    Mana(g.Player1, 10); ClearExceptShrines(g);
    var mine = Board(g, g.Player1, "CS2_182"); mine.Damage = 3;
    var theirs = Board(g, g.Player2, "CS2_182"); theirs.Damage = 2;
    var shrine = ShrineOf(g, g.Player1); shrine.Damage = 2;
    int hero = g.Player1.Hero.Health; g.Player1.Hero.Damage = 5;
    Mate(g, "TRLA_151");
    Check("E7 TRLA_151 Bwonsamdi's Keeper: every minion (both sides, the run shrine included) is restored to full Health; heroes are not",
        mine.Damage == 0 && theirs.Damage == 0 && shrine.Damage == 0 && g.Player1.Hero.Damage == 5, $"{mine.Damage} {theirs.Damage} {shrine.Damage}");
    // Bwonsamdi's Covenant (TRLA_147): healing an enemy damages it instead, by the heal's amount (its missing Health).
    var (s2, p2, g2) = F("rumble_priest_147", 506);
    Mana(g2.Player1, 10); ClearExceptShrines(g2);
    var mine2 = Board(g2, g2.Player1, "CS2_182"); mine2.Damage = 3;
    var en = Board(g2, g2.Player2, "CS2_182"); en.Damage = 2;
    var full = Board(g2, g2.Player2, "CS2_200");
    Mate(g2, "TRLA_151");
    Check("E8 with TRLA_147 the friendly Yeti is healed, the damaged enemy Yeti takes its missing 2 instead (1 left), the undamaged Ogre takes nothing",
        mine2.Damage == 0 && en.Health == 1 && !en.IsDead && full.Damage == 0, $"{mine2.Damage} {en.Health} {full.Damage}");
    // A dormant form is untouchable: no heal reaches it; the shrine revives at full Health anyway.
    Kill(g2, ShrineOf(g2, g2.Player1));
    var form = ShrineOf(g2, g2.Player1);
    Mate(g2, "TRLA_151");
    Check("E9 the dormant shrine form takes no heal (untouchable) and stays dormant", ShrineRules.IsDormantForm(form) && form.Damage == 0);
});
Guard("E152", () =>
{
    var (s, plan, g) = F("rumble_priest_146", 507);
    Mana(g.Player1, 10); ClearExceptShrines(g); ClearHand(g, g.Player1);
    SetDeck(g, g.Player2, "CS2_029", "CS2_182", "CS2_024", "CS2_200");
    var bossDeck = DeckIds(g.Player2);
    var sapper = Mate(g, "TRLA_152");
    Kill(g, sapper);
    var stolen = g.Player1.HandZone.Where(p => bossDeck.Contains(p.Id)).ToList();
    Check("E10 TRLA_152 Soulsapper: its Deathrattle steals both spells of the boss's deck (Fireball, Frostbolt) into the run player's hand, no minion",
        stolen.Count == 2 && stolen.All(p => p.Card.Type == CardType.SPELL && p.Controller == g.Player1 && p.Zone == g.Player1.HandZone)
        && g.Player2.DeckZone.Count == 2 && g.Player2.DeckZone.All(p => p.Card.Type == CardType.MINION), string.Join(",", stolen.Select(p => p.Card.Id)));
    Check("E11 the stolen spells keep their Cost (no discount)", stolen.All(p => p.Cost == p.Card.Cost));
    // The boss's fair view and own-deck ledger (its real run deck): unknown slots left the deck, the stolen cards are masked in
    // the run player's hand.
    var (s5, p5, g5) = F("rumble_priest_146", 512);
    Mana(g5.Player1, 10); ClearExceptShrines(g5); ClearHand(g5, g5.Player1);
    var bossDeck5 = DeckIds(g5.Player2);
    int spells5 = g5.Player2.DeckZone.Count(p => p.Card.Type == CardType.SPELL), deck5 = g5.Player2.DeckZone.Count;
    Kill(g5, Mate(g5, "TRLA_152"));
    stolen = g5.Player1.HandZone.Where(p => bossDeck5.Contains(p.Id)).ToList();
    Check("E12a on the boss's own run deck the Deathrattle steals min(2, its spells) spells", stolen.Count == Math.Min(2, spells5) && stolen.All(p => p.Card.Type == CardType.SPELL)
        && g5.Player2.DeckZone.Count == deck5 - stolen.Count, $"{stolen.Count} of {spells5} spells");
    g = g5;
    EndTurn(g);
    var bot = FairBotState.Create(g, g.Player2.PlayerId, 1, DeckStyle.Tempo);
    Check("E12 the boss's fair view masks the stolen spells like the rest of the run player's hand; its own deck count is the engine's",
        bot.Facts.Opponent.Hand.Count == g.Player1.HandZone.Count && bot.Facts.Opponent.Hand.All(c => c.Unknown) && bot.Facts.Own.DeckCount == g.Player2.DeckZone.Count);
    var ledger = OwnDeckLedger.Observe(g, g.Player2, null);
    bool consistent = true; var detail = new List<string>();
    for (int seed = 1; seed <= 6; seed++)
    {
        var sample = ledger.Sample(g, g.Player2, seed);
        var start = g.Player2.DeckCards.GroupBy(c => c.Id).ToDictionary(x => x.Key, x => x.Count());
        consistent &= sample.Values.Sum() == g.Player2.DeckZone.Count && sample.All(p => start.TryGetValue(p.Key, out int n) && n >= p.Value);
        detail.Add(sample.Values.Sum() + "/" + g.Player2.DeckZone.Count);
    }
    Check("E13 the boss's own-deck ledger stays consistent after the steal: every sample has the deck's count, from its own list; the stolen cards are not observed",
        consistent && stolen.All(p => !ledger.Observed(p.Id)), string.Join(" ", detail));
    // One spell left: one steal; none: none.
    var (s2, p2, g2) = F("rumble_priest_146", 508);
    Mana(g2.Player1, 10); ClearExceptShrines(g2); ClearHand(g2, g2.Player1);
    SetDeck(g2, g2.Player2, "CS2_182", "CS2_024");
    Kill(g2, Mate(g2, "TRLA_152"));
    Check("E14 one spell in the boss's deck: one is stolen, the minion stays", g2.Player1.HandZone.Count(p => p.Card.Id == "CS2_024") == 1 && g2.Player2.DeckZone.Count == 1);
    Kill(g2, Mate(g2, "TRLA_152"));
    Check("E15 no spell left: nothing is stolen", g2.Player2.DeckZone.Count == 1 && g2.Player1.HandZone.Count == 1);
    // Bwonsamdi's Calling (TRLA_114): three Deathrattles steal up to 6 spells.
    var (s3, p3, g3) = F("rumble_priest_114", 509);
    Mana(g3.Player1, 10); ClearExceptShrines(g3); ClearHand(g3, g3.Player1);
    SetDeck(g3, g3.Player2, Enumerable.Repeat("CS2_024", 8).ToArray());
    Kill(g3, Mate(g3, "TRLA_152"));
    Check("E16 with TRLA_114 the Deathrattle runs three times: 6 spells stolen", g3.Player1.HandZone.Count == 6 && g3.Player2.DeckZone.Count == 2, $"{g3.Player1.HandZone.Count}/{g3.Player2.DeckZone.Count}");
});
Guard("E153", () =>
{
    var (s, plan, g) = F("rumble_priest_146", 510);
    Mana(g.Player1, 10); ClearExceptShrines(g); ClearHand(g, g.Player1);
    var fireball = ToHand(g, g.Player1, "CS2_029"); var coin = ToHand(g, g.Player1, "GAME_005"); var yeti = ToHand(g, g.Player1, "CS2_182");
    var att = Mate(g, "TRLA_153");
    Check("E17 TRLA_153 Conjuring Attendant: the run player's spells cost (2) less (TRLA_153e), floor (0); minions do not",
        fireball.Cost == 2 && coin.Cost == 0 && yeti.Cost == 4 && Ench(fireball).Any(e => e.Card.Id == "TRLA_153e"), $"{fireball.Cost} {coin.Cost} {yeti.Cost}");
    var boss = ToHand(g, g.Player2, "CS2_029");
    Check("E18 the boss's spells are not cheaper", boss.Cost == 4);
    EndTurn(g); Mana(g.Player2, 10);
    Play(g, ToHand(g, g.Player2, "EX1_332"), att);
    Check("E19 Silenced, its aura ends", fireball.Cost == 4);
    // Sacred Ritual (TRLA_801): spells cost (1) less per shrine; with the Attendant: 4 - 2 - 1 = 1.
    var (s2, p2, g2) = F("rumble_priest_146", 511, upgrades: new[] { "TRLA_801" });
    Mana(g2.Player1, 10); ClearExceptShrines(g2); ClearHand(g2, g2.Player1);
    var fb2 = ToHand(g2, g2.Player1, "CS2_029");
    Mate(g2, "TRLA_153");
    Check("E20 with shrine upgrade TRLA_801 (one shrine) the cuts stack: Fireball (1)", fb2.Cost == 1, fb2.Cost.ToString());
});

// ================================================================ O Rogue
Guard("O189", () =>
{
    var (s, plan, g) = F("rumble_rogue_106", 601);
    Mana(g.Player1, 10); ClearExceptShrines(g); ClearHand(g, g.Player1);
    var wisp = ToHand(g, g.Player1, "CS2_231"); var coin = ToHand(g, g.Player1, "GAME_005"); var parrot = ToHand(g, g.Player1, "TRLA_189");
    Play(g, wisp); Play(g, coin); Play(g, parrot);
    var hand = g.Player1.HandZone.Select(p => p.Card.Id).ToList();
    Check("O0 TRLA_189 Parrot Mascot: Combo adds a copy of each card played this turn, in order, itself included (Wisp, Coin, Parrot)",
        hand.SequenceEqual(new[] { "CS2_231", "GAME_005", "TRLA_189" }) && g.Player1.HandZone.All(p => p.Id != wisp.Id && p.Id != coin.Id), string.Join(",", hand));
    var (s2, p2, g2) = F("rumble_rogue_106", 602);
    Mana(g2.Player1, 10); ClearExceptShrines(g2); ClearHand(g2, g2.Player1);
    Play(g2, ToHand(g2, g2.Player1, "TRLA_189"));
    Check("O1 played first (no Combo): nothing is added", g2.Player1.HandZone.Count == 0);
    // A copy of the Parrot plays as a Combo again: a fresh copy set (the hand bounds it).
    for (int i = 0; i < 6; i++) ToHand(g2, g2.Player1, "CS2_231");
    var p1 = g2.Player1.HandZone.First(p => p.Card.Id == "CS2_231"); Play(g2, p1);
    Play(g2, ToHand(g2, g2.Player1, "TRLA_189"));
    Check("O2 a full hand takes no more copies (10)", g2.Player1.HandZone.Count == 8, g2.Player1.HandZone.Count.ToString());
});
Guard("O190", () =>
{
    var (s, plan, g) = F("rumble_rogue_106", 603);
    Mana(g.Player1, 10); ClearExceptShrines(g); ClearHand(g, g.Player1); ClearHand(g, g.Player2);
    var a = ToHand(g, g.Player2, "CS2_029"); var b = ToHand(g, g.Player2, "CS2_182"); var c = ToHand(g, g.Player2, "CS2_200");
    var bossHand = new HashSet<int> { a.Id, b.Id, c.Id };
    var looter = Mate(g, "TRLA_190");
    var got = g.Player1.HandZone.Where(p => bossHand.Contains(p.Id)).ToList();
    Check("O3 TRLA_190 Salty Looter: its Battlecry moves a random card of the boss's hand to the run player's (their card now, cost kept)",
        got.Count == 1 && got[0].Controller == g.Player1 && g.Player2.HandZone.Count == 2 && got[0].Cost == got[0].Card.Cost);
    Kill(g, looter);
    got = g.Player1.HandZone.Where(p => bossHand.Contains(p.Id)).ToList();
    Check("O4 its Deathrattle steals another", got.Count == 2 && g.Player2.HandZone.Count == 1);
    EndTurn(g);
    var bot = FairBotState.Create(g, g.Player2.PlayerId, 1, DeckStyle.Tempo);
    Check("O5 the boss's fair view after the thefts: its own hand and the run player's hand counts are the engine's",
        bot.Facts.Own.Hand.Count == g.Player2.HandZone.Count && bot.Facts.Opponent.Hand.Count == g.Player1.HandZone.Count,
        $"{bot.Facts.Own.Hand.Count}/{g.Player2.HandZone.Count} {bot.Facts.Opponent.Hand.Count}/{g.Player1.HandZone.Count}");
    // Battle Totem (LOOTA_846): two Battlecries, two thefts; Totem of the Dead (LOOTA_845): two Deathrattles.
    var (s2, p2, g2) = F("rumble_rogue_106", 604, treasures: new[] { "LOOTA_846" });
    Mana(g2.Player1, 10); ClearExceptShrines(g2); ClearHand(g2, g2.Player1); ClearHand(g2, g2.Player2);
    for (int i = 0; i < 4; i++) ToHand(g2, g2.Player2, "CS2_231");
    Mate(g2, "TRLA_190");
    Check("O6 with Battle Totem (LOOTA_846) the Battlecry steals twice", g2.Player2.HandZone.Count == 2 && g2.Player1.HandZone.Count == 2);
    var (s3, p3, g3) = F("rumble_rogue_106", 605, treasures: new[] { "LOOTA_845" });
    Mana(g3.Player1, 10); ClearExceptShrines(g3); ClearHand(g3, g3.Player1); ClearHand(g3, g3.Player2);
    for (int i = 0; i < 4; i++) ToHand(g3, g3.Player2, "CS2_231");
    var l3 = Mate(g3, "TRLA_190"); Kill(g3, l3);
    Check("O7 with Totem of the Dead (LOOTA_845) the Deathrattle steals twice (one Battlecry + two Deathrattles: 3)", g3.Player2.HandZone.Count == 1 && g3.Player1.HandZone.Count == 3);
    ClearHand(g3, g3.Player2);
    var l4 = Mate(g3, "TRLA_190"); int mine = g3.Player1.HandZone.Count; Kill(g3, l4);
    Check("O8 an empty boss hand: nothing is stolen, nothing breaks", g3.Player1.HandZone.Count == mine && g3.State != State.COMPLETE);
});
Guard("O191", () =>
{
    var (s, plan, g) = F("rumble_rogue_106", 606);
    Mana(g.Player1, 10); ClearExceptShrines(g);
    var started = g.Player1.HandZone.Where(p => p.Card.Type != CardType.INVALID && p.Card.Id != "GAME_005").ToList();
    var startCosts = started.Select(p => p.Cost).ToList();
    var generated = ToHand(g, g.Player1, "CS2_200");
    Mate(g, "TRLA_191");
    Check("O9 TRLA_191 Sharktoothed Harpooner: a card that did not start in the deck costs (4) less (Ogre 6 -> 2, GBL_008e)",
        generated.Cost == 2 && Ench(generated).Any(e => e.Card.Id == DungeonRunShrines.HarpoonerDiscount), generated.Cost.ToString());
    Check("O10 the cards dealt from the run deck (they started in it) keep their Cost (SelfCondition.IsNotStartInDeck, the history's starting deck)",
        started.Count > 0 && started.Select(p => p.Cost).SequenceEqual(startCosts) && started.All(p => !Ench(p).Any(e => e.Card.Id == DungeonRunShrines.HarpoonerDiscount)),
        string.Join(",", started.Select(p => p.Card.Id)));
    var drawn = Generic.Draw(g.Player1);
    int dc = drawn.Cost;
    Mate(g, "TRLA_191");
    Check("O11 a card drawn later from the run deck still started in it: no discount", drawn.Cost == dc && !Ench(drawn).Any(e => e.Card.Id == DungeonRunShrines.HarpoonerDiscount));
    // Treasure from Below (TRLA_186): the card it steals from the boss's deck did not start in the run player's deck.
    var (s2, p2, g2) = F("rumble_rogue_186", 607);
    EndTurn(g2);
    var bossDeck = DeckIds(g2.Player2);
    EndTurn(g2);
    var stolen = g2.Player1.HandZone.FirstOrDefault(p => bossDeck.Contains(p.Id));
    int before = stolen?.Cost ?? -1;
    Mate(g2, "TRLA_191");
    Check("O12 with TRLA_186 the card stolen from the boss's deck (it started in the boss's deck) gets the (4) cut too, floor (0)",
        stolen != null && stolen.Cost == Math.Max(0, before - 4) && Ench(stolen).Any(e => e.Card.Id == DungeonRunShrines.HarpoonerDiscount), $"{stolen?.Card.Id} {before}->{stolen?.Cost}");
    // Parrot Mascot copies did not start in the deck.
    var (s3, p3, g3) = F("rumble_rogue_106", 608);
    Mana(g3.Player1, 10); ClearExceptShrines(g3); ClearHand(g3, g3.Player1);
    Play(g3, ToHand(g3, g3.Player1, "CS2_182")); Play(g3, ToHand(g3, g3.Player1, "TRLA_189"));
    var yetiCopy = g3.Player1.HandZone.First(p => p.Card.Id == "CS2_182");
    Mate(g3, "TRLA_191");
    Check("O13 a Parrot Mascot copy (Yeti 4) gets the cut: (0)", yetiCopy.Cost == 0);
});
Guard("O192", () =>
{
    var (s, plan, g) = F("rumble_rogue_106", 609);
    Mana(g.Player1, 10); ClearExceptShrines(g); ClearHand(g, g.Player1);
    SetDeck(g, g.Player1, "CS2_231", "CS2_029", "CS2_182");
    Mate(g, "TRLA_192");
    Mana(g.Player1, 10);
    Play(g, ToHand(g, g.Player1, "GAME_005"));
    var fb = g.Player1.HandZone.FirstOrDefault(p => p.Card.Id == "CS2_029");
    Check("O14 TRLA_192 Lobstrok Tastetester: after a spell, the deck's spell (Fireball) is drawn and costs (2) less (LOOT_204e)",
        fb != null && fb.Cost == 2 && Ench(fb).Any(e => e.Card.Id == "LOOT_204e") && g.Player1.DeckZone.Count == 2);
    Mana(g.Player1, 10); Play(g, ToHand(g, g.Player1, "GAME_005"));
    Check("O15 no spell left in the deck: nothing is drawn", g.Player1.DeckZone.Count == 2);
    EndTurn(g); Mana(g.Player2, 10); SetDeck(g, g.Player1, "CS2_029");
    Play(g, ToHand(g, g.Player2, "GAME_005"));
    Check("O16 the boss's spell draws nothing for the owner", g.Player1.DeckZone.Count == 1);
    // Pirate's Mark (TRLA_187) casts each spell again: the recast is an effect's cast, so one draw per hand cast.
    var (s2, p2, g2) = F("rumble_rogue_187", 610);
    Mana(g2.Player1, 10); ClearExceptShrines(g2); ClearHand(g2, g2.Player1);
    SetDeck(g2, g2.Player1, "CS2_029", "CS2_029", "CS2_029");
    Mate(g2, "TRLA_192");
    int boss = g2.Player2.Hero.Health;
    Mana(g2.Player1, 10); Play(g2, ToHand(g2, g2.Player1, "CS2_024"), g2.Player2.Hero); // Frostbolt: 3, cast again
    Check("O17 with TRLA_187 a Frostbolt is cast twice (6 to the boss's hero) and one spell is drawn", g2.Player2.Hero.Health == boss - 6 && g2.Player1.DeckZone.Count == 2,
        $"{boss}->{g2.Player2.Hero.Health} deck {g2.Player1.DeckZone.Count}");
});
Guard("O193", () =>
{
    var (s, plan, g) = F("rumble_rogue_106", 611);
    Mana(g.Player1, 10); ClearExceptShrines(g);
    var cont = Mate(g, "TRLA_193");
    var boar = Board(g, g.Player2, "CS2_200");
    bool hit = Attack(g, cont, boar);
    Check("O18 TRLA_193 Raging Contender: Rush (it attacks the Ogre the turn it is played; 8 kills it, the Ogre's 6 kills it)", hit && boar.IsDead && cont.IsDead);
    var pirates = g.Player1.BoardZone.Where(m => !ShrineRules.IsMarked(m)).ToList();
    Check("O19 its Deathrattle fills the board with random Pirates (scripted, the run's generation filter): shrine + 6",
        g.Player1.BoardZone.IsFull && pirates.Count == 6 && pirates.All(m => m.Card.IsRace(Race.PIRATE) && Generated(plan, m)), string.Join(",", pirates.Select(m => m.Card.Id)));
    Check("O20 with TRLA_106 Bottled Terror the Pirates have Stealth (the shrine's aura), the shrine does not", pirates.All(m => m.HasStealth) && !ShrineOf(g, g.Player1).HasStealth);
    var (s2, p2, g2) = F("rumble_rogue_106", 612, treasures: new[] { "LOOTA_845" });
    Mana(g2.Player1, 10); ClearExceptShrines(g2);
    Kill(g2, Mate(g2, "TRLA_193"));
    Check("O21 with Totem of the Dead the second Deathrattle meets a full board: still shrine + 6", g2.Player1.BoardZone.Count == 7);
});

// ================================================================ S Shaman
Guard("S157", () =>
{
    var (s, plan, g) = F("rumble_shaman_109", 701);
    Mana(g.Player1, 10); ClearExceptShrines(g);
    var a = Board(g, g.Player2, "CS2_231"); var b = Board(g, g.Player2, "CS2_231"); var c = Board(g, g.Player2, "CS2_171");
    Mate(g, "TRLA_157");
    Check("S1 TRLA_157 Leap Frog: 3 to a random enemy minion, repeated on each kill: three 1-Health minions all die", a.IsDead && b.IsDead && c.IsDead);
    var yeti = Board(g, g.Player2, "CS2_182");
    Mate(g, "TRLA_157");
    Check("S2 no kill, no repeat: a lone Yeti takes 3 (2 left)", yeti.Health == 2 && !yeti.IsDead);
    Kill(g, yeti);
    var fixture = Fixture(g); var w = Board(g, g.Player2, "CS2_231");
    Mate(g, "TRLA_157");
    var form = ShrineOf(g, g.Player2);
    Check("S3 the boss's 1-Health TRLA_107 fixture shrine counts as a kill (it goes dormant): the Wisp dies too; the dormant form is untouchable",
        ShrineRules.IsDormantForm(form) && w.IsDead && form.Damage == 0, form?.Card.Id);
    Mate(g, "TRLA_157");
    Check("S4 only the dormant form left: nothing to hit", ShrineRules.IsDormantForm(ShrineOf(g, g.Player2)));
    // Tribute from the Tides (TRLA_154): three Battlecries; the second kills the hit Yeti (2 left), the third finds no target.
    var (s2, p2, g2) = F("rumble_shaman_154", 702);
    Mana(g2.Player1, 10); ClearExceptShrines(g2);
    var y2 = Board(g2, g2.Player2, "CS2_182");
    Mate(g2, "TRLA_157");
    Check("S5 with TRLA_154 (Battlecries two extra times) a lone Yeti is hit and then killed", y2.IsDead);
});
Guard("S158", () =>
{
    var (s, plan, g) = F("rumble_shaman_109", 703);
    Mana(g.Player1, 10); ClearExceptShrines(g);
    var totem = Mate(g, "TRLA_158");
    var y = Board(g, g.Player2, "CS2_182"); var mine = Board(g, g.Player1, "CS2_182");
    int boss = g.Player2.Hero.Health;
    Mana(g.Player1, 10); Play(g, ToHand(g, g.Player1, "EX1_238"), g.Player2.Hero); // Lightning Bolt: 3, Overload (1)
    Check("S6 TRLA_158 Overcharged Totem: after the owner Overloads, 2 to every enemy minion (not the friendly Yeti, not the boss's hero)",
        y.Health == 3 && mine.Health == 5 && g.Player2.Hero.Health == boss - 3, $"{y.Health} {mine.Health}");
    Check("S7 with Krag'wa's Lure (TRLA_109) the same Overload gives the owner's minions +1 Attack (the Totem 0 -> 1)", totem.AttackDamage == 1 && mine.AttackDamage == 5);
    EndTurn(g); Mana(g.Player2, 10);
    Play(g, ToHand(g, g.Player2, "EX1_238"), g.Player1.Hero);
    Check("S8 the boss's Overload does nothing for the Totem", y.Health == 3);
    // Naga Tonguelasher's Overload is the opponent's: neither side's "after you Overload" fires.
    EndTurn(g); Mana(g.Player1, 10);
    Mate(g, "TRLA_160");
    Check("S9 TRLA_160's Overload on the boss does not set off the owner's Totem", y.Health == 3 && totem.AttackDamage == 1);
});
Guard("S159", () =>
{
    var (s, plan, g) = F("rumble_shaman_109", 704);
    Mana(g.Player1, 10); ClearExceptShrines(g); ClearHand(g, g.Player1);
    Mate(g, "TRLA_159");
    var choice = g.Player1.Choice;
    var offered = choice?.Choices.Select(id => g.IdEntityDic[id].Card).ToList();
    Check("S10 TRLA_159 Croak Jouster: not Overloaded, a Discover of 3 spells", offered != null && offered.Count == 3 && offered.All(cd => cd.Type == CardType.SPELL && !plan.ExcludedGenerated.Contains(cd.Id)),
        offered == null ? "no choice" : string.Join(",", offered.Select(cd => cd.Id)));
    Pick(g, g.Player1);
    Check("S11 one is kept", g.Player1.HandZone.Count == 1 && g.Player1.Choice == null);
    ClearHand(g, g.Player1); Mana(g.Player1, 10);
    Play(g, ToHand(g, g.Player1, "EX1_238"), g.Player2.Hero); // Overload (1) owed
    ClearHand(g, g.Player1);
    MateKeep(g, "TRLA_159");
    Check("S12 Overloaded (Overload owed this turn): all 3 spells are kept, no choice", g.Player1.HandZone.Count == 3 && g.Player1.Choice == null
        && g.Player1.HandZone.All(p => p.Card.Type == CardType.SPELL), g.Player1.HandZone.Count.ToString());
    // Tribute from the Tides (TRLA_154): three Battlecries, each a Discover (picked in turn) or, Overloaded, three kept sets.
    var (s2, p2, g2) = F("rumble_shaman_154", 705);
    Mana(g2.Player1, 10); ClearExceptShrines(g2); ClearHand(g2, g2.Player1);
    Mate(g2, "TRLA_159");
    int picks = Pick(g2, g2.Player1);
    Check("S13 with TRLA_154 three Discovers, each picked", picks == 3 && g2.Player1.HandZone.Count == 3, $"{picks} {g2.Player1.HandZone.Count}");
    ClearHand(g2, g2.Player1); Mana(g2.Player1, 10);
    g2.Player1.OverloadLocked = 2;
    MateKeep(g2, "TRLA_159");
    Check("S14 with TRLA_154 and locked Overloaded crystals, 3 Battlecries keep 9 spells", g2.Player1.HandZone.Count == 9 && g2.Player1.Choice == null, g2.Player1.HandZone.Count.ToString());
});
Guard("S160", () =>
{
    var (s, plan, g) = F("rumble_shaman_155", 706);
    Mana(g.Player1, 10); ClearExceptShrines(g);
    Mate(g, "TRLA_160");
    Check("S15 TRLA_160 Naga Tonguelasher: the boss owes 3 Overload", g.Player2.OverloadOwed == 3 && g.Player1.OverloadOwed == 0);
    g.Player2.BaseMana = 9;
    EndTurn(g);
    Check("S16 on the boss's next turn 3 of its Mana Crystals are locked", g.Player2.OverloadLocked == 3 && g.Player2.RemainingMana == g.Player2.BaseMana - 3,
        $"locked {g.Player2.OverloadLocked} remaining {g.Player2.RemainingMana}/{g.Player2.BaseMana}");
    var (s2, p2, g2) = F("rumble_shaman_155", 707, treasures: new[] { "LOOTA_846" });
    Mana(g2.Player1, 10); ClearExceptShrines(g2);
    Mate(g2, "TRLA_160");
    Check("S17 with Battle Totem two Battlecries: the boss owes 6", g2.Player2.OverloadOwed == 6);
});
Guard("S161", () =>
{
    var (s, plan, g) = F("rumble_shaman_155", 708);
    Mana(g.Player1, 10); ClearExceptShrines(g); ClearHand(g, g.Player1);
    var boar = Board(g, g.Player2, "CS2_171");
    var her = Mate(g, "TRLA_161");
    Attack(g, her, boar);
    var got = g.Player1.HandZone.ToList();
    Check("S18 TRLA_161 War Heralder: Rush; Overkill adds 3 random Shaman spells (scripted, the run's generation filter)",
        boar.IsDead && got.Count == 3 && got.All(p => p.Card.Type == CardType.SPELL && p.Card.Class == CardClass.SHAMAN && Generated(plan, p)), string.Join(",", got.Select(p => p.Card.Id)));
    Check("S19 with Krag'wa's Grace (TRLA_155) they cost (2) less, floor (0)", got.All(p => p.Cost == Math.Max(0, p.Card.Cost - 2)));
});

// ================================================================ W Warlock
Guard("W181", () =>
{
    var (s, plan, g) = F("rumble_warlock_113", 801);
    Mana(g.Player1, 10); ClearExceptShrines(g); ClearHand(g, g.Player1);
    var ogre = ToHand(g, g.Player1, "CS2_200"); var yeti = ToHand(g, g.Player1, "CS2_182"); var bolt = ToHand(g, g.Player1, "CS2_024");
    SetDeck(g, g.Player1, "CS2_231");
    var boar = Board(g, g.Player2, "CS2_171");
    var fam = Mate(g, "TRLA_181");
    Attack(g, fam, boar);
    Check("W1 TRLA_181 Ravenous Familiar: Rush; Overkill discards the lowest-Cost card (Frostbolt 2) and draws a card (the Wisp)",
        boar.IsDead && bolt.Zone == g.Player1.GraveyardZone && ogre.Zone == g.Player1.HandZone && yeti.Zone == g.Player1.HandZone
        && g.Player1.HandZone.Any(p => p.Card.Id == "CS2_231") && g.Player1.DeckZone.Count == 0, $"{bolt.Zone?.Type}");
    // Dark Reliquary (TRLA_178): the discard summons a random Demon.
    var (s2, p2, g2) = F("rumble_warlock_178", 802);
    Mana(g2.Player1, 10); ClearExceptShrines(g2); ClearHand(g2, g2.Player1);
    ToHand(g2, g2.Player1, "CS2_231");
    var b2 = Board(g2, g2.Player2, "CS2_171");
    var f2 = Mate(g2, "TRLA_181");
    Attack(g2, f2, b2);
    var demons = g2.Player1.BoardZone.Where(m => m.Card.IsRace(Race.DEMON)).ToList();
    Check("W2 with TRLA_178 the Overkill's discard summons a random Demon", demons.Count == 1 && Generated(p2, demons[0]), string.Join(",", demons.Select(m => m.Card.Id)));
    ClearHand(g2, g2.Player1); SetDeck(g2, g2.Player1, "CS2_182");
    var b3 = Board(g2, g2.Player2, "CS2_171"); var f3 = Mate(g2, "TRLA_181");
    Attack(g2, f3, b3);
    Check("W3 an empty hand: nothing is discarded (no Demon), a card is still drawn",
        g2.Player1.HandZone.Count == 1 && g2.Player1.HandZone[0].Card.Id == "CS2_182" && g2.Player1.BoardZone.Count(m => m.Card.IsRace(Race.DEMON)) == 1);
});
Guard("W182", () =>
{
    var (s, plan, g) = F("rumble_warlock_113", 803);
    Mana(g.Player1, 10); ClearExceptShrines(g);
    g.Player1.Hero.Damage = 15;
    var y1 = Board(g, g.Player2, "CS2_182"); var y2 = Board(g, g.Player2, "CS2_182"); var w = Board(g, g.Player2, "CS2_231");
    var mine = Board(g, g.Player1, "CS2_182");
    var doc = Mate(g, "TRLA_182");
    Check("W4 TRLA_182 Vile Necrodoctor: Lifesteal; the Battlecry deals 3 to every enemy minion (Yetis 2 left, the Wisp dies, the friendly Yeti untouched)",
        doc.HasLifeSteal && y1.Health == 2 && y2.Health == 2 && w.IsDead && mine.Health == 5);
    Check("W5 the Lifesteal heals the run player's hero by the damage dealt (9)", g.Player1.Hero.Damage == 6, g.Player1.Hero.Damage.ToString());
    var fixture = Fixture(g);
    Mate(g, "TRLA_182");
    Check("W6 the boss's 1-Health fixture shrine dies and goes dormant", ShrineRules.IsDormantForm(ShrineOf(g, g.Player2)));
});
Guard("W183", () =>
{
    var (s, plan, g) = F("rumble_warlock_113", 804);
    Mana(g.Player1, 10); ClearExceptShrines(g); ClearHand(g, g.Player1);
    SetDeck(g, g.Player1, Enumerable.Repeat("CS2_231", 8).ToArray());
    bool first = Power(g);
    var stalker = Mate(g, "TRLA_183");
    Check("W7 TRLA_183 Warp Stalker: Stealth; played after the Hero Power (Life Tap) was spent, it is ready again", first && stalker.HasStealth && !g.Player1.Hero.HeroPower.IsExhausted);
    int hp = g.Player1.Hero.Health; Mana(g.Player1, 10);
    int uses = 0; for (int i = 0; i < 4; i++) if (Power(g)) uses++;
    Check("W8 the Hero Power is used four more times this turn (4 Life Taps: 8 damage, 4 draws)", uses == 4 && g.Player1.Hero.Health == hp - 8 && !g.Player1.Hero.HeroPower.IsExhausted,
        $"{uses} {hp}->{g.Player1.Hero.Health}");
    Mana(g.Player1, 10);
    Play(g, ToHand(g, g.Player1, "EX1_332"), stalker); // Silence
    Power(g);
    Check("W9 Silenced, the next use spends it", g.Player1.Hero.HeroPower.IsExhausted && !Power(g));
    // Blood Portal (TRLA_185) answers each Life Tap; with Hir'eek's Hunger (TRLA_179) the self-damage on the owner's turn goes to the boss.
    var (s2, p2, g2) = F("rumble_warlock_179", 805);
    Mana(g2.Player1, 10); ClearExceptShrines(g2); ClearHand(g2, g2.Player1);
    SetDeck(g2, g2.Player1, Enumerable.Repeat("CS2_231", 8).ToArray());
    Mate(g2, "TRLA_183"); Mate(g2, "TRLA_185");
    int me = g2.Player1.Hero.Health, boss = g2.Player2.Hero.Health; Mana(g2.Player1, 10);
    for (int i = 0; i < 3; i++) Power(g2);
    Check("W10 with TRLA_179 three Life Taps on the owner's turn hit the boss's hero (6), not the owner's: no Blood Portal spell",
        g2.Player1.Hero.Health == me && g2.Player2.Hero.Health == boss - 6 && g2.Player1.HandZone.All(p => p.Card.Id == "CS2_231"), $"{me}->{g2.Player1.Hero.Health} boss {boss}->{g2.Player2.Hero.Health}");
});
Guard("W184", () =>
{
    var (s, plan, g) = F("rumble_warlock_113", 806);
    Mana(g.Player1, 10); ClearExceptShrines(g); ClearHand(g, g.Player1);
    ToHand(g, g.Player1, "CS2_231"); ToHand(g, g.Player1, "CS2_231");
    var div = Mate(g, "TRLA_184");
    var spells = g.Player1.HandZone.Where(p => p.Card.Id != "CS2_231").ToList();
    Check("W11 TRLA_184 Dark Diviner: the hand is filled with random spells (8 beside the two Wisps; scripted, the run's generation filter)",
        g.Player1.HandZone.Count == 10 && spells.Count == 8 && spells.All(p => p.Card.Type == CardType.SPELL && Generated(plan, p)) && spells.All(p => p[GameTag.CREATOR] == div.Id),
        string.Join(",", spells.Select(p => p.Card.Id)));
    Check("W12 spells of more than one class (the text names none)", spells.Select(p => p.Card.Class).Distinct().Count() > 1);
    var (s2, p2, g2) = F("rumble_warlock_113", 806);
    Mana(g2.Player1, 10); ClearExceptShrines(g2); ClearHand(g2, g2.Player1);
    ToHand(g2, g2.Player1, "CS2_231"); ToHand(g2, g2.Player1, "CS2_231");
    Mate(g2, "TRLA_184");
    Check("W13 deterministic: the same run seed fills the same spells", g2.Player1.HandZone.Select(p => p.Card.Id).SequenceEqual(g.Player1.HandZone.Select(p => p.Card.Id)));
});
Guard("W185", () =>
{
    var (s, plan, g) = F("rumble_warlock_113", 807);
    Mana(g.Player1, 10); ClearExceptShrines(g); ClearHand(g, g.Player1);
    SetDeck(g, g.Player1, Enumerable.Repeat("CS2_231", 10).ToArray());
    var portal = Mate(g, "TRLA_185");
    Mana(g.Player1, 10); Power(g); // Life Tap: the owner's hero takes 2 (and draws a Wisp)
    var got = g.Player1.HandZone.Where(p => p.Card.Type == CardType.SPELL).ToList();
    Check("W14 TRLA_185 Blood Portal: the owner's hero takes damage (Life Tap) -> a random Warlock spell (scripted, the run's generation filter)",
        got.Count == 1 && got[0].Card.Class == CardClass.WARLOCK && Generated(plan, got[0]), string.Join(",", got.Select(p => p.Card.Id)));
    ClearHand(g, g.Player1);
    Mana(g.Player1, 10); Play(g, ToHand(g, g.Player1, "CS2_062")); // Hellfire: 3 to all characters
    Check("W15 Hellfire hits both heroes: one spell (the owner's hero), none for the boss's", g.Player1.HandZone.Count(p => p.Card.Class == CardClass.WARLOCK) == 1,
        string.Join(",", g.Player1.HandZone.Select(p => p.Card.Id)));
    ClearHand(g, g.Player1);
    EndTurn(g); Mana(g.Player2, 10);
    Play(g, ToHand(g, g.Player2, "CS2_029"), g.Player1.Hero);
    Check("W16 on the boss's turn its Fireball on the owner's hero adds one", g.Player1.HandZone.Count(p => p.Card.Class == CardClass.WARLOCK) == 1);
    // Blood Pact (TRLA_113): a spell paid with Health is no damage.
    EndTurn(g); ClearHand(g, g.Player1); Mana(g.Player1, 10);
    int hp = g.Player1.Hero.Health;
    Play(g, ToHand(g, g.Player1, "CS2_024"), g.Player2.Hero);
    Check("W17 with TRLA_113 a Frostbolt paid with 2 Health adds nothing (paying Health is not damage)", g.Player1.Hero.Health == hp - 2 && g.Player1.HandZone.Count == 0,
        $"{hp}->{g.Player1.Hero.Health} hand {g.Player1.HandZone.Count}");
    // The shrine's dormancy and revival do not touch the Portal; with Hir'eek's Hunger revived, the owner's own-turn damage goes to the boss.
    var (s2, p2, g2) = F("rumble_warlock_179", 808);
    Mana(g2.Player1, 10); ClearExceptShrines(g2); ClearHand(g2, g2.Player1);
    SetDeck(g2, g2.Player1, Enumerable.Repeat("CS2_231", 12).ToArray());
    Mate(g2, "TRLA_185");
    Kill(g2, ShrineOf(g2, g2.Player1));
    Mana(g2.Player1, 10); Power(g2);
    Check("W18 with TRLA_179 dormant the Life Tap hits the owner's hero: a spell", g2.Player1.HandZone.Count(p => p.Card.Class == CardClass.WARLOCK) == 1);
    for (int i = 0; i < 12 && !(ShrineRules.IsAwake(ShrineOf(g2, g2.Player1)) && g2.CurrentPlayer == g2.Player1); i++) EndTurn(g2);
    ClearHand(g2, g2.Player1); Mana(g2.Player1, 10);
    int me = g2.Player1.Hero.Health; Power(g2);
    Check("W19 revived, Hir'eek's Hunger sends the Life Tap to the boss: no spell", ShrineRules.IsAwake(ShrineOf(g2, g2.Player1)) && g2.Player1.Hero.Health == me && g2.Player1.HandZone.All(p => p.Card.Id == "CS2_231"),
        $"{me}->{g2.Player1.Hero.Health} {string.Join(",", g2.Player1.HandZone.Select(p => p.Card.Id))}");
});

// ================================================================ X Warrior
Guard("X173", () =>
{
    var (s, plan, g) = F("rumble_warrior_171", 901);
    Mana(g.Player1, 10); ClearExceptShrines(g); ClearHand(g, g.Player1);
    var boar = Board(g, g.Player2, "CS2_171");
    var rocker = Mate(g, "TRLA_173");
    Attack(g, rocker, boar);
    var offered = g.Player1.Choice?.Choices.Select(id => g.IdEntityDic[id].Card).ToList();
    Check("X1 TRLA_173 Redcrest Rocker: Rush; Overkill Discovers a Dragon (3 Dragons offered)", boar.IsDead && offered != null && offered.Count == 3 && offered.All(cd => cd.IsRace(Race.DRAGON)),
        offered == null ? "no choice" : string.Join(",", offered.Select(cd => cd.Id)));
    Pick(g, g.Player1);
    Check("X2 one is kept", g.Player1.HandZone.Count == 1 && g.Player1.HandZone[0].Card.IsRace(Race.DRAGON));
    // Akali's Rhino (TRLA_171): "Overkill: This can attack again" -- a second Overkill with a Dragon on the board keeps all 3.
    ClearHand(g, g.Player1);
    Board(g, g.Player1, "NEW1_023"); // Faerie Dragon
    var b2 = Board(g, g.Player2, "CS2_171");
    Attack(g, rocker, b2);
    Check("X3 with TRLA_171 the Rocker can attack again after its Overkill; now beside a Dragon, all 3 Dragons are kept", b2.IsDead && g.Player1.HandZone.Count == 3
        && g.Player1.HandZone.All(p => p.Card.IsRace(Race.DRAGON)) && g.Player1.Choice == null, $"{b2.IsDead} {g.Player1.HandZone.Count}");
});
Guard("X174", () =>
{
    var (s, plan, g) = F("rumble_warrior_104", 902);
    Mana(g.Player1, 10); ClearExceptShrines(g);
    var drummer = Mate(g, "TRLA_174");
    var wisp = Board(g, g.Player1, "CS2_231"); var yeti = Board(g, g.Player1, "CS2_182");
    var shrine = ShrineOf(g, g.Player1); shrine.Damage = shrine.Health - 2; // 2 Health left
    var enemy = Board(g, g.Player2, "CS2_231");
    Mana(g.Player1, 10); Play(g, ToHand(g, g.Player1, "CS2_062")); // Hellfire on the owner's turn
    Check("X4 TRLA_174 Ironhorn Drummer: on the owner's turn the owner's minions stop at 1 Health (Wisp 1, Yeti 2, the 2-Health shrine 1, awake); the boss's Wisp dies",
        !wisp.IsDead && wisp.Health == 1 && yeti.Health == 2 && ShrineRules.IsAwake(shrine) && shrine.Health == 1 && enemy.IsDead && Ench(wisp).Any(e => e.Card.Id == "TRLA_174e"),
        $"{wisp.Health} {yeti.Health} {shrine.Health}");
    EndTurn(g); Mana(g.Player2, 10);
    Play(g, ToHand(g, g.Player2, "CS2_029"), yeti);
    Check("X5 on the boss's turn it does not: a Fireball kills the Yeti", yeti.IsDead);
    Play(g, ToHand(g, g.Player2, "EX1_332"), drummer);
    EndTurn(g); Mana(g.Player1, 10);
    Play(g, ToHand(g, g.Player1, "CS2_062"));
    Check("X6 Silenced, the aura ends: the owner's own Hellfire kills the Wisp and sends the shrine dormant", wisp.IsDead && ShrineRules.IsDormantForm(ShrineOf(g, g.Player1)));
});
Guard("X175", () =>
{
    var (s, plan, g) = F("rumble_warrior_104", 903);
    Mana(g.Player1, 10); ClearExceptShrines(g); ClearHand(g, g.Player1);
    g.Player1.Hero.Armor = 0;
    var pit = Mate(g, "TRLA_175");
    Mana(g.Player1, 10); Power(g); // Armor Up: 2
    Check("X7 TRLA_175 Seasoned Pitbrawler: Armor Up gives twice as much (4)", g.Player1.Hero.Armor == 4, g.Player1.Hero.Armor.ToString());
    Check("X8 with TRLA_104 (Attack equal to the Armor) the shrine has 4 Attack", ShrineOf(g, g.Player1).AttackDamage == 4);
    var pit2 = Mate(g, "TRLA_175");
    g.Player1.Hero.Armor = 0;
    Mana(g.Player1, 10); Play(g, ToHand(g, g.Player1, "EX1_606")); // Shield Block: 5
    Check("X9 two Pitbrawlers: four times as much (Shield Block 5 -> 20)", g.Player1.Hero.Armor == 20, g.Player1.Hero.Armor.ToString());
    // One gain, one event: TRLA_123 Succoring Skyscreamer draws once per gain.
    ClearHand(g, g.Player1); SetDeck(g, g.Player1, Enumerable.Repeat("CS2_231", 10).ToArray());
    Board(g, g.Player1, "TRLA_123");
    g.Player1.Hero.Armor = 0;
    EndTurn(g); EndTurn(g); ClearHand(g, g.Player1); Mana(g.Player1, 10);
    Power(g);
    Check("X10 one doubled gain is one Armor event: TRLA_123 draws once (Armor Up 2 -> 8 with two Pitbrawlers)", g.Player1.Hero.Armor == 8 && g.Player1.HandZone.Count == 1,
        $"{g.Player1.Hero.Armor} hand {g.Player1.HandZone.Count}");
    EndTurn(g); Mana(g.Player2, 10);
    Play(g, ToHand(g, g.Player2, "EX1_332"), pit); Play(g, ToHand(g, g.Player2, "EX1_332"), pit2);
    Play(g, ToHand(g, g.Player2, "EX1_606"));
    EndTurn(g); g.Player1.Hero.Armor = 0; Mana(g.Player1, 10); Power(g);
    Check("X11 Silenced Pitbrawlers double nothing; the boss's Armor is never doubled", g.Player1.Hero.Armor == 2, g.Player1.Hero.Armor.ToString());
});
Guard("X176", () =>
{
    var (s, plan, g) = F("rumble_warrior_170", 904);
    Mana(g.Player1, 10); ClearExceptShrines(g); ClearHand(g, g.Player1);
    SetDeck(g, g.Player1, "EX1_043", "CS2_231", "CS2_231");
    Mate(g, "TRLA_176");
    Check("X12 the Herald (itself a Dragon) does not answer its own play: nothing drawn", g.Player1.HandZone.Count == 0 && g.Player1.DeckZone.Count == 3);
    var fd = (Minion)ToHand(g, g.Player1, "NEW1_023"); Mana(g.Player1, 10); Play(g, fd);
    var drake = g.Player1.HandZone.FirstOrDefault(p => p.Card.Id == "EX1_043");
    Check("X13 TRLA_176 Herald of Flame: a played Dragon gets Rush (TRLA_176e) and the deck's Dragon (Twilight Drake) is drawn",
        fd.IsRush && Ench(fd).Any(e => e.Card.Id == "TRLA_176e") && drake != null && g.Player1.DeckZone.Count == 2);
    var enemy = Board(g, g.Player2, "CS2_231");
    Check("X14 the Rush Dragon attacks a minion the turn it is played", Attack(g, fd, enemy) && enemy.IsDead);
    Check("X15 with Akali's War Drum (TRLA_170) the drawn Dragon then costs (1) less (after the summon)", drake != null && drake.Cost == drake.Card.Cost - 1, $"{drake?.Cost}");
    EndTurn(g); Mana(g.Player2, 10);
    Play(g, ToHand(g, g.Player2, "NEW1_023"));
    Check("X16 the boss's Dragon draws nothing for the owner", g.Player1.DeckZone.Count == 2);
});
Guard("X177", () =>
{
    var (s, plan, g) = F("rumble_warrior_104", 905);
    Mana(g.Player1, 10); ClearExceptShrines(g);
    g.Player1.Hero.Armor = 0;
    var a = Board(g, g.Player2, "CS2_182"); var b = Board(g, g.Player2, "CS2_200");
    var fixture = Fixture(g);
    var mine = Board(g, g.Player1, "CS2_182");
    Mate(g, "TRLA_177");
    Check("X17 TRLA_177 Unbound Punisher: every enemy minion is destroyed (the TRLA_107 fixture goes dormant), 2 Armor each (6); the friendly Yeti stays",
        a.IsDead && b.IsDead && ShrineRules.IsDormantForm(ShrineOf(g, g.Player2)) && g.Player1.Hero.Armor == 6 && !mine.IsDead, g.Player1.Hero.Armor.ToString());
    Check("X18 with TRLA_104 the shrine's Attack is the Armor (6)", ShrineOf(g, g.Player1).AttackDamage == 6);
    g.Player1.Hero.Armor = 0;
    Mate(g, "TRLA_177");
    Check("X19 only the untouchable dormant form left: nothing destroyed, no Armor", g.Player1.Hero.Armor == 0 && ShrineRules.IsDormantForm(ShrineOf(g, g.Player2)));
    Mate(g, "TRLA_175");
    Board(g, g.Player2, "CS2_231"); Board(g, g.Player2, "CS2_231");
    Mate(g, "TRLA_177");
    Check("X20 with a Pitbrawler the gain (2 x 2) is doubled in one: 8", g.Player1.Hero.Armor == 8, g.Player1.Hero.Armor.ToString());
    // The run shrine's revival (TRLA_805: in 2 turns) keeps its rule: the Attack follows the Armor again.
    var (s2, p2, g2) = F("rumble_warrior_104", 906, upgrades: new[] { "TRLA_805" });
    Mana(g2.Player1, 10); ClearExceptShrines(g2);
    Kill(g2, ShrineOf(g2, g2.Player1));
    int deathTurn = g2.Turn;
    for (int i = 0; i < 12 && !(ShrineRules.IsAwake(ShrineOf(g2, g2.Player1)) && g2.CurrentPlayer == g2.Player1); i++) EndTurn(g2);
    g2.Player1.Hero.Armor = 0;
    var e2 = Board(g2, g2.Player2, "CS2_231");
    Mate(g2, "TRLA_177");
    Check("X21 with TRLA_805 the shrine revives on the owner's second turn after its death; the Punisher's 2 Armor is its Attack",
        g2.Turn == deathTurn + 4 && ShrineOf(g2, g2.Player1).AttackDamage == 2 && e2.IsDead, $"turn {deathTurn}->{g2.Turn} atk {ShrineOf(g2, g2.Player1).AttackDamage}");
});

// ================================================================ Q the teammate draft through the run flow
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
    Guard("Q-" + cls, () =>
    {
        var s = NewRun(row, data.Class(row).Decks[1], 650 + Array.FindIndex(classes, c => c.Class == cls));
        var log = new List<string>();
        Walk(s, x => x.Floor > 13, log);
        var offers = log.Where(l => l.Contains(":teammate[")).ToList();
        var taken = s.Cards.Where(c => c.Origin == DungeonRunDeck.OriginTeammate).Select(c => c.Id).ToList();
        var sets = offers.Select(l => l.Substring(l.IndexOf('[') + 1).TrimEnd(']').Split(',').ToList()).ToList();
        // Phase 7 D: the pool is six (the companion included), so the fourth draft still offers 3.
        var six = mates.Append(companion).ToArray();
        bool sized = sets.Count == 4 && sets.Select(x => x.Count).SequenceEqual(new[] { 3, 3, 3, 3 });
        bool fresh = sets.Select((x, i) => x.All(id => !taken.Take(i).Contains(id))).All(b => b);
        Check($"Q1 {cls}: four teammate drafts (I.2, I.4, II.3, III.3) offer 3 each of its six teammates (the companion included), never one already taken",
            sized && fresh && sets.All(x => x.All(six.Contains)) && string.Join(",", offers.Select(l => l.Substring(0, l.IndexOf(':')))) == "f2,f4,f8,f13",
            string.Join(" ", offers));
        Check($"Q2 {cls}: the four picks are distinct deck records with origin \"teammate\"", taken.Count == 4 && taken.Distinct().Count() == 4 && taken.All(six.Contains), string.Join(",", taken));
        var plan = run.BuildPlan(s);
        Check($"Q3 {cls}: the next fight's plan deck holds the four teammates", taken.All(id => plan.PlayerDeck.Any(c => c.Id == id)));
        var g = Start(plan, run.MatchSeed(s));
        g.Process(ChooseTask.Mulligan(g.Player1, new List<int>())); g.Process(ChooseTask.Mulligan(g.Player2, new List<int>()));
        g.MainBegin();
        var inGame = g.Player1.DeckZone.Concat(g.Player1.HandZone).Select(p => p.Card.Id).ToList();
        Check($"Q4 {cls}: the match deals them to the run player (deck or hand), with the board shrine on the board",
            taken.All(inGame.Contains) && ShrineRules.Of(g.Player1).Count >= 1, string.Join(",", taken.Where(id => !inGame.Contains(id))));
        foreach (string id in taken)
        {
            while (g.Player1.HandZone.Count >= 9) { var spare = g.Player1.HandZone.First(p => !taken.Contains(p.Card.Id)); g.Player1.HandZone.Remove(spare); g.Player1.SetasideZone.Add(spare); }
            var card = g.Player1.HandZone.FirstOrDefault(p => p.Card.Id == id) ?? Generic.Draw(g.Player1, g.Player1.DeckZone.First(p => p.Card.Id == id));
            if (g.Player1.BoardZone.IsFull) break;
            Mana(g.Player1, 10);
            bool played = card != null && card.Zone == g.Player1.HandZone && Play(g, card);
            while (g.Player1.Choice != null) Pick(g, g.Player1);
            Check($"Q5 {cls}: the drafted {id} plays from the run deck onto the board", played && (card.Zone == g.Player1.BoardZone || card.Zone == g.Player1.GraveyardZone),
                card?.Zone?.Type.ToString());
        }
    });

Console.WriteLine($"SUMMARY checks passed {passes} failed {failures}");
return failures == 0 ? 0 : 1;
