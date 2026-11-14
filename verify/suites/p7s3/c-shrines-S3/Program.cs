// c-shrines-S3: phase 7 family P7-S3, the Rogue/Shaman/Warlock board shrines: TRLA_106, TRLA_186, TRLA_187 (Rogue), TRLA_109,
// TRLA_154, TRLA_155 (Shaman), TRLA_113, TRLA_178, TRLA_179 (Warlock), their dormant forms, the client enchantment records
// TRLA_106e/109e/113e, plus the engine helpers they need (BattlecryRules: EXTRA_BATTLECRIES_ADDITIONAL; RecastRules on the same
// target; the Hir'eek's Hunger redirect in Character.TakeDamage; the deck steal). Every game plays a real engine game through the
// run setup path (NewRun -> BuildPlan -> DungeonRunSetup.Configure -> Game -> ApplyAfterStart -> mulligan -> MainBegin).
// A admission/data/plan/AI view; G Rogue; S Shaman; W Warlock; L dormancy and revival of each; X interactions with upgrades
// (TRLA_804/805), treasures (LOOTA_846/845/832/824) and the shrine guards (Hex, Mind Control, Faceless, Silence, Sap).
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
using SabberStoneCore.Model.Costs;
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
DungeonRunData Load(string runText = null, string schedule = null) => DungeonRunData.Parse(runText ?? runJson, R("dungeon-run/pool-v1.json"), R("npc/decks-v1.json"),
    R("dungeon-run/catalog-v1.json"), schedule ?? scheduleJson, R("dungeon-run/tavern-v1.json"));
var data = Load();
var run = new DungeonRun(data);
var all = new DungeonRunProfile(); all.Unlocked.AddRange(data.Unlocks.Keys);
Card C(string id) => Cards.FromId(id);

DungeonRunState NewRun(string row, string deck, long seed, string schedule = null)
{
    var cls = data.Class(row);
    return run.NewRun(all, cls.Id, deck ?? cls.DefaultDeck, cls.DefaultPower, seed, schedule: schedule,
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
const string Rogue = "TRLA_209h_Rogue", Shaman = "TRLA_209h_Shaman", Warlock = "TRLA_209h_Warlock";
string[] Nine = { "TRLA_106", "TRLA_186", "TRLA_187", "TRLA_109", "TRLA_154", "TRLA_155", "TRLA_113", "TRLA_178", "TRLA_179" };
string ClassOf(string id) => Array.IndexOf(Nine, id) < 3 ? "rogue" : Array.IndexOf(Nine, id) < 6 ? "shaman" : "warlock";
string DeckOf(string id) => "rumble_" + ClassOf(id) + "_" + id.Substring(5);
string RowOf(string deck) => deck.StartsWith("rumble_rogue", StringComparison.Ordinal) ? Rogue : deck.StartsWith("rumble_shaman", StringComparison.Ordinal) ? Shaman : Warlock;
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
    return (s, plan, g);
}
void Mana(Controller c, int n) { c.BaseMana = n; c.UsedMana = 0; c.OverloadLocked = 0; c.OverloadOwed = 0; }
IPlayable ToHand(Game g, Controller c, string id)
{
    while (c.HandZone.Count >= 9) { var old = c.HandZone[0]; c.HandZone.Remove(old); c.SetasideZone.Add(old); }
    var e = Entity.FromCard(c, C(id)); Generic.AddHandPhase.Invoke(c, e); g.DeathProcessingAndAuraUpdate(); return e;
}
// Empties a hand (into the set-aside zone), so a check counts only what it adds.
void ClearHand(Game g, Controller c) { foreach (var p in c.HandZone.ToArray()) { c.HandZone.Remove(p); c.SetasideZone.Add(p); } g.DeathProcessingAndAuraUpdate(); }
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
void Damage(Game g, ICharacter m, int n) { Generic.DamageCharFunc(m.Controller.Opponent.Hero, m, n, false); g.DeathProcessingAndAuraUpdate(); }
void ToTurn(Game g, int turn) { for (int i = 0; i < 20 && g.Turn < turn && g.State != State.COMPLETE; i++) EndTurn(g); }
Minion Ready(Minion m) { m.IsExhausted = false; return m; }
bool Attack(Game g, Minion a, ICharacter t) => g.Process(MinionAttackTask.Any(g.CurrentPlayer, a, t));
bool HeroAttack(Game g, ICharacter t) => g.Process(HeroAttackTask.Any(g.CurrentPlayer, t));
bool Power(Game g, ICharacter target = null) => g.Process(HeroPowerTask.Any(g.CurrentPlayer, target));
IEnumerable<Enchantment> Ench(IEntity e) => (IEnumerable<Enchantment>)(e as IPlayable)?.AppliedEnchantments ?? Array.Empty<Enchantment>();
int Heal(Controller c) => c.Hero.Health;
// The run player's hand cards that were in the boss's deck when the snapshot was taken (stolen by Treasure from Below).
List<IPlayable> Stolen(Game g, HashSet<int> bossDeck) => g.Player1.HandZone.Where(p => bossDeck.Contains(p.Id)).ToList();
HashSet<int> DeckIds(Controller c) => new HashSet<int>(c.DeckZone.Select(p => p.Id));

// ================================================================ A admission, data, plan, AI view
Guard("A", () =>
{
    AdmissionVerdict V(string id, AdmitScope scope) => DungeonRunAdmission.Classify(null, id, scope, null, true);
    var gen = new HashSet<string>(DungeonRunAdmission.GeneratedRejects(true));
    foreach (string id in Nine)
    {
        var v = V(id, AdmitScope.Player);
        string family = id == "TRLA_186" ? "hidden-steal" : "board-shrine";
        Check($"A1 {id}: scripted with its form {id}t (3-turn sleeper pointing back), admitted for the run player only ({family}), never generated",
            ShrineRules.IsScripted(id) && ShrineRules.FormOf(id) == id + "t" && C(id).Implemented && C(id + "t").Implemented && C(id + "t").Dormant?.Turns == 3
            && C(id + "t")[(GameTag)1132] == C(id).AssetId && v.Admitted && v.Family == family && v.Rule.NeverGenerated
            && !V(id, AdmitScope.Boss).Admitted && !V(id, AdmitScope.Generated).Admitted && gen.Contains(id) && gen.Contains(id + "t")
            && V(id + "t", AdmitScope.Player).Family == "board-shrine-form", $"{v.Family} {v.Admitted}");
        var s = NewRun(RowOf(DeckOf(id)), DeckOf(id), 10 + Array.IndexOf(Nine, id));
        var plan = run.BuildPlan(s);
        Check($"A2 a new Rikkar run on {DeckOf(id)} has board shrine {id} (plan: one copy; not a power, treasure or deck card)",
            s.BoardShrine == id && plan.PlayerShrine == id && plan.PlayerShrineCopies == 1 && !s.Deck.Contains(id) && !plan.PlayerTreasures.Contains(id) && s.Shrine == null
            && DungeonRun.OffersPhase7Drafts(s), $"{s.BoardShrine} {plan.PlayerShrine}");
    }
    foreach (string e in new[] { "TRLA_106e", "TRLA_109e", "TRLA_113e" })
        Check($"A3 the client enchantment record {e} is scripted", C(e).Implemented && C(e).Type == CardType.ENCHANTMENT);
    var hs = DungeonRunAdmission.RuleFor("TRLA_186");
    Check("A4 the hidden-steal row (TRLA_186) states the hidden zone: the boss's deck, its own-deck ledger and the masked hand",
        hs.Family == "hidden-steal" && hs.Admit == AdmitScope.Player && hs.NeverGenerated && hs.Reason.Contains("OwnDeckLedger") && hs.Reason.Contains("deck")
        && hs.Reason.Contains("masks"));
    Check("A4 every one of the 27 shrines is scripted (no deck plays without its shrine any more)",
        data.Decks.Where(d => d.Shrine != null).All(d => ShrineRules.IsScripted(d.Shrine)) && data.Decks.Count(d => d.Shrine != null) == 27);
    var errors = DungeonRunValidator.Validate(data, null);
    Check("A5 the validator passes the shipped data (shrines included)", errors.Count == 0 && DungeonRunValidator.ValidateShrines(data).Count == 0, string.Join(" | ", errors.Take(3)));
    var (s2, p2, g) = F("rumble_warlock_179", 22);
    var bot = FairBotState.Create(g, g.Player2.PlayerId, 1, DeckStyle.Tempo);
    var seen = bot.Facts.Opponent.Board.FirstOrDefault(c => c.CardId == "TRLA_179");
    Check("A6 the boss's FairBotState sees Hir'eek's Hunger as an awake run shrine on the run player's board", seen != null && seen.IsRunShrine && seen.ShrineDormantTurns == 0);
    Check("A7 each of the nine has its shrine engine value in the evaluator table",
        DeckPlanEvaluator.ShrineEngine("TRLA_106") == 2.5 && DeckPlanEvaluator.ShrineEngine("TRLA_186") == 2.5 && DeckPlanEvaluator.ShrineEngine("TRLA_187") == 3
        && DeckPlanEvaluator.ShrineEngine("TRLA_109") == 2 && DeckPlanEvaluator.ShrineEngine("TRLA_154") == 2.5 && DeckPlanEvaluator.ShrineEngine("TRLA_155") == 2.5
        && DeckPlanEvaluator.ShrineEngine("TRLA_113") == 2 && DeckPlanEvaluator.ShrineEngine("TRLA_178") == 1.5 && DeckPlanEvaluator.ShrineEngine("TRLA_179") == 2.5);
    Check("A8 the run's generation never yields a shrine or a form (RunMatchPlan.ExcludedGenerated)",
        p2.ExcludedGenerated.Contains("TRLA_179") && p2.ExcludedGenerated.Contains("TRLA_179t") && p2.ExcludedGenerated.Contains("TRLA_186"));
    Check("A9 the Hunger's working tag is a shrine-model private tag", ShrineRules.IsPrivateTag(DungeonRunShrines.HungerResolving));
});

// ================================================================ G Rogue
Guard("G106", () =>
{
    var (s, plan, g) = F("rumble_rogue_106", 101);
    var shrine = ShrineOf(g, g.Player1);
    Mana(g.Player1, 10); ClearExceptShrines(g);
    Check("G1 Bottled Terror lands: 0/2, and does not stealth itself", shrine?.Card.Id == "TRLA_106" && shrine.AttackDamage == 0 && shrine.Health == 2 && !shrine.HasStealth,
        $"{shrine?.Card.Id} {shrine?.Health} stealth {shrine?.HasStealth}");
    var yeti = Ready(Board(g, g.Player1, "CS2_182"));
    var enemy = Board(g, g.Player2, "CS2_182");
    Check("G2 the run player's other minions have Stealth; the boss's do not", yeti.HasStealth && !enemy.HasStealth);
    Attack(g, yeti, enemy);
    Check("G3 a Yeti that attacks keeps the aura Stealth", yeti.HasStealth && enemy.Health == 1, $"stealth {yeti.HasStealth} enemy {enemy.Health}");
    EndTurn(g); Mana(g.Player2, 10);
    bool targeted = Play(g, ToHand(g, g.Player2, "CS2_029"), yeti);
    Check("G4 the boss cannot target the stealthed Yeti with a Fireball", !targeted && yeti.Health == 1 && !yeti.IsDead, $"played {targeted} yeti {yeti.Health}");
    Play(g, ToHand(g, g.Player2, "CS2_093")); // Consecration: 2 to the run player's characters (Stealth does not stop an AoE)
    var form = ShrineOf(g, g.Player1);
    Check("G5 a boss AoE kills Bottled Terror (0/2) and the 4/1 Yeti: the shrine dormant in its slot, the form unbuffed",
        form?.Card.Id == "TRLA_106t" && form.AttackDamage == 0 && yeti.IsDead, $"{form?.Card.Id} {form?.AttackDamage} {yeti.IsDead}");
    var (s2, p2, g2) = F("rumble_rogue_106", 102);
    Mana(g2.Player1, 10); ClearExceptShrines(g2);
    var a = Board(g2, g2.Player1, "CS2_182"); var b = Board(g2, g2.Player1, "CS2_120");
    Kill(g2, ShrineOf(g2, g2.Player1));
    var form2 = ShrineOf(g2, g2.Player1);
    Check("G6 killed: dormant; its Deathrattle fires once: Yeti 6/5, Croc 4/3, the Stealth gone; the form is not buffed",
        form2?.Card.Id == "TRLA_106t" && a.AttackDamage == 6 && b.AttackDamage == 4 && !a.HasStealth && !b.HasStealth && form2.AttackDamage == 0
        && Ench(a).Count(e => e.Card.Id == "TRLA_106e") == 1, $"{a.AttackDamage} {b.AttackDamage} {a.HasStealth} {form2?.AttackDamage}");
    var (s3, p3, g3) = F("rumble_rogue_106", 103);
    Mana(g3.Player1, 10); ClearExceptShrines(g3);
    var y3 = Board(g3, g3.Player1, "CS2_182");
    EndTurn(g3); Mana(g3.Player2, 10);
    Play(g3, ToHand(g3, g3.Player2, "EX1_332"), ShrineOf(g3, g3.Player1));
    Check("G7 silenced, Bottled Terror's aura ends: the Yeti loses Stealth (still a run shrine)", !y3.HasStealth && ShrineRules.IsAwake(ShrineOf(g3, g3.Player1)));
    Kill(g3, ShrineOf(g3, g3.Player1));
    Check("G7 a silenced Terror's Deathrattle is gone too: no +2 Attack when it goes dormant", y3.AttackDamage == 4 && ShrineOf(g3, g3.Player1)?.Card.Id == "TRLA_106t");
});
Guard("G186", () =>
{
    var (s, plan, g) = F("rumble_rogue_186", 111);
    var shrine = ShrineOf(g, g.Player1);
    Check("G8 Treasure from Below lands: 0/2", shrine?.Card.Id == "TRLA_186" && shrine.Health == 2);
    var bossDeck = DeckIds(g.Player2);
    int handBefore = g.Player1.HandZone.Count;
    EndTurn(g);
    Check("G9 the boss's turn start steals nothing for anyone", Stolen(g, bossDeck).Count == 0 && g.Player2.HandZone.All(p => p.Controller == g.Player2));
    var deckBefore = DeckIds(g.Player2); int deckCount = g.Player2.DeckZone.Count;
    bossDeck = DeckIds(g.Player2);
    EndTurn(g);
    var got = Stolen(g, bossDeck);
    var card = got.FirstOrDefault();
    Check("G10 at the start of the run player's turn: one card of the boss's deck is now theirs, in hand, from the boss's deck (count - 1)",
        got.Count == 1 && card.Controller == g.Player1 && card.Zone == g.Player1.HandZone && g.Player2.DeckZone.Count == deckCount - 1 && !g.Player2.DeckZone.Contains(card),
        $"{got.Count} {card?.Card.Id} deck {deckCount}->{g.Player2.DeckZone.Count}");
    Check("G11 the stolen card costs (2) less (LOOT_204e, floor 0)", card != null && card.Cost == Math.Max(0, card.Card.Cost - 2) && Ench(card).Any(e => e.Card.Id == "LOOT_204e"),
        $"{card?.Card.Id} {card?.Card.Cost}->{card?.Cost}");
    // The boss's fair view: the stolen card is a masked card in the run player's hand; its own-deck ledger drops an unknown slot.
    EndTurn(g);
    var bot = FairBotState.Create(g, g.Player2.PlayerId, 1, DeckStyle.Tempo);
    Check("G12 the boss's fair view masks the stolen card like the rest of the run player's hand",
        bot.Facts.Opponent.Hand.Count == g.Player1.HandZone.Count && bot.Facts.Opponent.Hand.All(c => c.Unknown) && bot.Facts.Own.DeckCount == g.Player2.DeckZone.Count);
    var ledger = OwnDeckLedger.Observe(g, g.Player2, null);
    bool consistent = true; var detail = new List<string>();
    for (int seed = 1; seed <= 6; seed++)
    {
        var sample = ledger.Sample(g, g.Player2, seed);
        var start = g.Player2.DeckCards.GroupBy(c => c.Id).ToDictionary(x => x.Key, x => x.Count());
        bool sub = sample.All(p => start.TryGetValue(p.Key, out int n) && n >= p.Value);
        consistent &= sample.Values.Sum() == g.Player2.DeckZone.Count && sub;
        detail.Add(sample.Values.Sum() + "/" + g.Player2.DeckZone.Count);
    }
    Check("G13 the boss's own-deck ledger stays consistent after the steal: every sample has the deck's count, drawn from its own list",
        consistent && !ledger.Observed(card.Id), string.Join(" ", detail));
    // The boss's search simulates the run player's next turn start in its fair view, whose deck slots are unmaterialized
    // (Unknown): the steal takes one and records only its residence change (no transfer fact names it).
    var view = bot.FingerprintGame;
    var viewBoss = view.Player1.PlayerId == g.Player2.PlayerId ? view.Player1 : view.Player2;
    int viewHand = viewBoss.Opponent.HandZone.Count, viewDeck = viewBoss.DeckZone.Count;
    string simulated = "ok";
    try { view.Process(EndTurnTask.Any(view.CurrentPlayer)); } catch (Exception e) { simulated = e.GetType().Name + ": " + e.Message; }
    Check("G13 the boss's fair view plays the run player's next turn start with the steal (an Unknown deck slot moves to their hand)",
        simulated == "ok" && viewBoss.DeckZone.Count == viewDeck - 1 && viewBoss.Opponent.HandZone.Count == viewHand + 2, $"{simulated} deck {viewDeck}->{viewBoss.DeckZone.Count} hand {viewHand}->{viewBoss.Opponent.HandZone.Count}");
    // A full hand burns the stolen card (the ordinary hand rule); an empty deck steals nothing.
    var (s2, p2, g2) = F("rumble_rogue_186", 112);
    EndTurn(g2);
    while (g2.Player1.HandZone.Count < 10) Generic.AddHandPhase.Invoke(g2.Player1, Entity.FromCard(g2.Player1, C("CS2_231")));
    var deck2 = DeckIds(g2.Player2); int count2 = g2.Player2.DeckZone.Count; int grave = g2.Player1.GraveyardZone.Count;
    EndTurn(g2);
    // The run player's own draw comes after the steal; the steal fills nothing (hand full), the stolen card is burned.
    Check("G14 with a full hand the stolen card is burned (to the run player's graveyard, still out of the boss's deck)",
        g2.Player2.DeckZone.Count == count2 - 1 && g2.Player1.GraveyardZone.Any(p => deck2.Contains(p.Id)), $"deck {count2}->{g2.Player2.DeckZone.Count}");
    var (s3, p3, g3) = F("rumble_rogue_186", 113);
    EndTurn(g3);
    foreach (var p in g3.Player2.DeckZone.ToArray()) { g3.Player2.DeckZone.Remove(p); g3.Player2.SetasideZone.Add(p); }
    int hand3 = g3.Player1.HandZone.Count;
    EndTurn(g3);
    Check("G15 the boss's deck empty: nothing is stolen (the run player only draws)", g3.Player1.HandZone.Count == hand3 + 1 && g3.State != State.COMPLETE, $"{hand3}->{g3.Player1.HandZone.Count}");
});
Guard("G187", () =>
{
    var (s, plan, g) = F("rumble_rogue_187", 121);
    var shrine = ShrineOf(g, g.Player1);
    Check("G16 Pirate's Mark lands: 0/5", shrine?.Card.Id == "TRLA_187" && shrine.Health == 5);
    Mana(g.Player1, 10); ClearExceptShrines(g); ClearHand(g, g.Player1);
    int boss = Heal(g.Player2); int spells = g.Player1.NumSpellsPlayedThisGame;
    Play(g, ToHand(g, g.Player1, "DS1_185"), g.Player2.Hero); // Arcane Shot
    Check("G17 Arcane Shot on the boss's hero is cast again on it: 2 + 2, one hand cast counted", Heal(g.Player2) == boss - 4 && g.Player1.NumSpellsPlayedThisGame == spells + 1,
        $"{boss}->{Heal(g.Player2)}");
    var golem = Board(g, g.Player2, "CS2_186");
    Play(g, ToHand(g, g.Player1, "CS2_024"), golem); // Frostbolt 3 + Freeze, twice
    Check("G18 Frostbolt on a War Golem: 3 + 3 on the same minion (7/7 -> 7/1), frozen", golem.Health == 1 && golem.IsFrozen, golem.Health.ToString());
    var wisp = Board(g, g.Player2, "CS2_231"); int boss2 = Heal(g.Player2);
    Play(g, ToHand(g, g.Player1, "DS1_185"), wisp);
    Check("G19 the first cast kills the target: the copy loses its target and hits nothing else", wisp.IsDead && Heal(g.Player2) == boss2 && golem.Health == 1);
    ClearExceptShrines(g); ClearHand(g, g.Player1);
    var w1 = Board(g, g.Player2, "CS2_120"); var w2 = Board(g, g.Player2, "CS2_182");
    int deck = g.Player1.DeckZone.Count;
    Play(g, ToHand(g, g.Player1, "EX1_129")); // Fan of Knives
    Check("G20 an untargeted spell is cast again: Fan of Knives twice (Croc 2/3 -> 2/1, Yeti 4/5 -> 4/3), two cards drawn",
        w1.Health == 1 && w2.Health == 3 && g.Player1.DeckZone.Count == deck - 2, $"{w1.Health} {w2.Health} deck {deck}->{g.Player1.DeckZone.Count}");
    ClearHand(g, g.Player1); deck = g.Player1.DeckZone.Count;
    var y = Board(g, g.Player2, "CS2_182");
    Play(g, ToHand(g, g.Player1, "EX1_154"), y, 2); // Wrath, the second option: 1 damage and draw
    Check("G21 Choose One: the recast keeps the chosen option (Wrath's 1 damage and a draw, twice)", y.Health == 3 && g.Player1.DeckZone.Count == deck - 2,
        $"{y.Health} deck {deck}->{g.Player1.DeckZone.Count}");
    EndTurn(g); Mana(g.Player2, 10);
    int me = Heal(g.Player1);
    Play(g, ToHand(g, g.Player2, "DS1_185"), g.Player1.Hero);
    Check("G22 the boss's spell is not cast again", Heal(g.Player1) == me - 2);
});

// ================================================================ S Shaman
Guard("S109", () =>
{
    var (s, plan, g) = F("rumble_shaman_109", 201);
    var shrine = ShrineOf(g, g.Player1);
    Check("S1 Krag'wa's Lure lands: 0/2", shrine?.Card.Id == "TRLA_109" && shrine.Health == 2 && shrine.AttackDamage == 0);
    Mana(g.Player1, 10); ClearExceptShrines(g);
    var yeti = Board(g, g.Player1, "CS2_182");
    var enemy = Board(g, g.Player2, "CS2_182");
    Play(g, ToHand(g, g.Player1, "EX1_238"), g.Player2.Hero); // Lightning Bolt, Overload (1)
    Check("S2 Lightning Bolt (Overload 1): the run player's minions, the shrine included, gain +1 Attack (TRLA_109e); the boss's do not",
        yeti.AttackDamage == 5 && shrine.AttackDamage == 1 && enemy.AttackDamage == 4 && Ench(yeti).Any(e => e.Card.Id == "TRLA_109e") && g.Player1.OverloadOwed == 1,
        $"yeti {yeti.AttackDamage} shrine {shrine.AttackDamage}");
    Play(g, ToHand(g, g.Player1, "EX1_241"), g.Player2.Hero); // Lava Burst, Overload (2)
    Check("S3 Lava Burst (Overload 2): +2 more (Yeti 7, shrine 3)", yeti.AttackDamage == 7 && shrine.AttackDamage == 3, $"{yeti.AttackDamage} {shrine.AttackDamage}");
    Play(g, ToHand(g, g.Player1, "EX1_247")); // Stormforged Axe, Overload (1)
    Check("S4 a weapon's Overload counts too (Stormforged Axe: +1)", yeti.AttackDamage == 8 && shrine.AttackDamage == 4);
    var devil = ToHand(g, g.Player1, "EX1_243"); // Dust Devil, Overload (2)
    Play(g, devil);
    Check("S5 a minion's own Overload buffs it too (Dust Devil 3 -> 5) and the rest (+2)", ((Minion)devil).AttackDamage == 5 && yeti.AttackDamage == 10,
        $"{((Minion)devil).AttackDamage} {yeti.AttackDamage}");
    EndTurn(g); Mana(g.Player2, 10);
    int ya = yeti.AttackDamage;
    Play(g, ToHand(g, g.Player2, "EX1_238"), g.Player1.Hero);
    Check("S6 the boss's Overload buffs nothing of the run player's", yeti.AttackDamage == ya && enemy.AttackDamage == 4);
});
Guard("S154", () =>
{
    var (s, plan, g) = F("rumble_shaman_154", 202);
    var shrine = ShrineOf(g, g.Player1);
    Check("S7 Tribute from the Tides lands: 0/3; the run player's Battlecries resolve 1 + 2 times, the boss's once",
        shrine?.Card.Id == "TRLA_154" && shrine.Health == 3 && BattlecryRules.Repeats(g.Player1, shrine) == 3 && BattlecryRules.Repeats(g.Player2, shrine) == 1
        && BattlecryRules.Additional(g.Player1) == 2 && g.Player1[GameTag.EXTRA_BATTLECRIES_ADDITIONAL] == 2, $"{BattlecryRules.Repeats(g.Player1, shrine)}");
    Mana(g.Player1, 10); ClearExceptShrines(g); ClearHand(g, g.Player1);
    int boss = Heal(g.Player2);
    Play(g, ToHand(g, g.Player1, "CS2_189"), g.Player2.Hero); // Elven Archer: 1 damage
    Check("S8 Elven Archer: its Battlecry three times on the same target (3 to the boss's hero)", Heal(g.Player2) == boss - 3, $"{boss}->{Heal(g.Player2)}");
    int hand = g.Player1.HandZone.Count;
    Play(g, ToHand(g, g.Player1, "EX1_015")); // Novice Engineer: draw
    Check("S9 Novice Engineer: three draws", g.Player1.HandZone.Count == hand + 3, $"{hand}->{g.Player1.HandZone.Count}");
    var raptor = Board(g, g.Player1, "CS2_172"); int hand2 = g.Player1.HandZone.Count;
    Play(g, ToHand(g, g.Player1, "CS2_120"));
    Check("S10 a minion without a Battlecry: nothing extra", g.Player1.HandZone.Count == hand2);
    EndTurn(g); Mana(g.Player2, 10);
    int me = Heal(g.Player1);
    Play(g, ToHand(g, g.Player2, "CS2_189"), g.Player1.Hero);
    Check("S11 the boss's Elven Archer: once (1 to the run player)", Heal(g.Player1) == me - 1);
    Play(g, ToHand(g, g.Player2, "EX1_332"), shrine);
    Check("S12 silenced, Tribute from the Tides adds nothing", BattlecryRules.Additional(g.Player1) == 0 && BattlecryRules.Repeats(g.Player1, shrine) == 1);
});
Guard("S155", () =>
{
    var (s, plan, g) = F("rumble_shaman_155", 203);
    var shrine = ShrineOf(g, g.Player1);
    Check("S13 Krag'wa's Grace lands: 0/10", shrine?.Card.Id == "TRLA_155" && shrine.Health == 10);
    Mana(g.Player1, 10); ClearHand(g, g.Player1);
    var fireball = ToHand(g, g.Player1, "CS2_029"); var shot = ToHand(g, g.Player1, "DS1_185"); var yeti = ToHand(g, g.Player1, "CS2_182");
    Check("S14 the run player's spells cost (2) less, floor (0) (Fireball 4 -> 2, Arcane Shot 1 -> 0); minions do not", fireball.Cost == 2 && shot.Cost == 0 && yeti.Cost == 4,
        $"{fireball.Cost} {shot.Cost} {yeti.Cost}");
    int mana = g.Player1.RemainingMana;
    Play(g, fireball, g.Player2.Hero);
    Check("S15 Fireball is paid at (2)", g.Player1.RemainingMana == mana - 2);
    var bossFire = ToHand(g, g.Player2, "CS2_029");
    Check("S16 the boss's spells keep their cost", bossFire.Cost == 4);
    Kill(g, shrine);
    Check("S17 the Grace dormant: spells cost their printed cost again", shot.Cost == 1);
});

// ================================================================ W Warlock
Guard("W113", () =>
{
    var (s, plan, g) = F("rumble_warlock_113", 301);
    var shrine = ShrineOf(g, g.Player1);
    Check("W1 Blood Pact lands: 0/10", shrine?.Card.Id == "TRLA_113" && shrine.Health == 10);
    Mana(g.Player1, 10); ClearExceptShrines(g); ClearHand(g, g.Player1);
    var fireball = ToHand(g, g.Player1, "CS2_029");
    Check("W2 the run player's spells are paid with Health", CostRules.PaymentKind(fireball) == CostPaymentKind.Health && fireball.Cost == 4);
    int me = Heal(g.Player1), mana = g.Player1.RemainingMana, boss = Heal(g.Player2);
    Play(g, fireball, g.Player2.Hero);
    Check("W3 Fireball: 4 Health paid, no Mana spent, 6 to the boss", Heal(g.Player1) == me - 4 && g.Player1.RemainingMana == mana && Heal(g.Player2) == boss - 6,
        $"me {me}->{Heal(g.Player1)} mana {mana}->{g.Player1.RemainingMana}");
    var yeti = ToHand(g, g.Player1, "CS2_182");
    Check("W4 minions are still paid with Mana", CostRules.PaymentKind(yeti) == CostPaymentKind.Mana);
    g.Player1.Hero.Damage = g.Player1.Hero.BaseHealth - 4; // 4 Health left
    var fb2 = ToHand(g, g.Player1, "CS2_029");
    Check("W5 a spell that would cost all the hero's Health cannot be played (4 Health, Fireball 4)", !fb2.IsPlayableByPlayer && !Play(g, fb2, g.Player2.Hero));
    g.Player1.Hero.Damage = 0;
    EndTurn(g); Mana(g.Player2, 10);
    var bossSpell = ToHand(g, g.Player2, "CS2_029");
    Check("W6 the boss's spells cost Mana", CostRules.PaymentKind(bossSpell) == CostPaymentKind.Mana);
    EndTurn(g);
    Kill(g, ShrineOf(g, g.Player1));
    Check("W7 the Pact dormant: spells cost Mana again", CostRules.PaymentKind(ToHand(g, g.Player1, "CS2_029")) == CostPaymentKind.Mana);
});
Guard("W178", () =>
{
    var (s, plan, g) = F("rumble_warlock_178", 302);
    var shrine = ShrineOf(g, g.Player1);
    Check("W8 Dark Reliquary lands: 0/4", shrine?.Card.Id == "TRLA_178" && shrine.Health == 4);
    Mana(g.Player1, 10); ClearExceptShrines(g); ClearHand(g, g.Player1);
    ToHand(g, g.Player1, "CS2_231");
    Play(g, ToHand(g, g.Player1, "EX1_308"), g.Player2.Hero); // Soulfire: discard a random card
    var demons = g.Player1.BoardZone.Where(m => !ShrineRules.IsMarked(m)).ToList();
    var rejects = new HashSet<string>(plan.ExcludedGenerated);
    Check("W9 Soulfire discards a card: one random Demon is summoned (scripted, not one the run's generation filter rejects)",
        demons.Count == 1 && demons[0].Card.IsRace(Race.DEMON) && demons[0].Card.Implemented && !rejects.Contains(demons[0].Card.Id), string.Join(",", demons.Select(m => m.Card.Id)));
    ClearExceptShrines(g); ClearHand(g, g.Player1);
    ToHand(g, g.Player1, "CS2_231"); ToHand(g, g.Player1, "CS2_231");
    var doomguard = ToHand(g, g.Player1, "EX1_310");
    Play(g, doomguard); // Doomguard: discard two
    var two = g.Player1.BoardZone.Where(m => !ShrineRules.IsMarked(m) && m != doomguard).ToList();
    Check("W10 Doomguard discards two: two Demons (beside the Doomguard)", two.Count == 2 && two.All(m => m.Card.IsRace(Race.DEMON)) && g.Player1.HandZone.Count == 0,
        string.Join(",", two.Select(m => m.Card.Id)));
    ClearExceptShrines(g); ClearHand(g, g.Player1);
    Play(g, ToHand(g, g.Player1, "EX1_308"), g.Player2.Hero);
    Check("W11 Soulfire with an empty hand discards nothing: no Demon", g.Player1.BoardZone.Count(m => !ShrineRules.IsMarked(m)) == 0);
    while (!g.Player1.BoardZone.IsFull) Board(g, g.Player1, "CS2_231");
    ToHand(g, g.Player1, "CS2_231");
    Play(g, ToHand(g, g.Player1, "EX1_308"), g.Player2.Hero);
    Check("W12 a full board: the discard summons nothing", g.Player1.BoardZone.Count(m => m.Card.Id == "CS2_231") == 6 && g.Player1.HandZone.Count == 0);
    ClearExceptShrines(g);
    EndTurn(g); Mana(g.Player2, 10); ClearHand(g, g.Player2);
    ToHand(g, g.Player2, "CS2_231");
    Play(g, ToHand(g, g.Player2, "EX1_308"), g.Player1.Hero);
    Check("W13 the boss's discard summons nothing for anyone", g.Player1.BoardZone.Count(m => !ShrineRules.IsMarked(m)) == 0 && g.Player2.BoardZone.Count == 0);
    var (s2, p2, g2) = F("rumble_warlock_178", 303);
    Mana(g2.Player1, 10); ClearExceptShrines(g2); ClearHand(g2, g2.Player1); ToHand(g2, g2.Player1, "CS2_231");
    Play(g2, ToHand(g2, g2.Player1, "EX1_308"), g2.Player2.Hero);
    var (s3, p3, g3) = F("rumble_warlock_178", 303);
    Mana(g3.Player1, 10); ClearExceptShrines(g3); ClearHand(g3, g3.Player1); ToHand(g3, g3.Player1, "CS2_231");
    Play(g3, ToHand(g3, g3.Player1, "EX1_308"), g3.Player2.Hero);
    Check("W14 deterministic: the same run seed summons the same Demon",
        g2.Player1.BoardZone.Where(m => !ShrineRules.IsMarked(m)).Select(m => m.Card.Id).SequenceEqual(g3.Player1.BoardZone.Where(m => !ShrineRules.IsMarked(m)).Select(m => m.Card.Id))
        && g2.Player1.BoardZone.Count(m => !ShrineRules.IsMarked(m)) == 1);
});
Guard("W179", () =>
{
    var (s, plan, g) = F("rumble_warlock_179", 303);
    var shrine = ShrineOf(g, g.Player1);
    Check("W15 Hir'eek's Hunger lands: 0/8; the run's power is Life Tap", shrine?.Card.Id == "TRLA_179" && shrine.Health == 8 && g.Player1.Hero.HeroPower.Card.Id == "CS2_056",
        g.Player1.Hero.HeroPower.Card.Id);
    Mana(g.Player1, 10); ClearExceptShrines(g);
    int me = Heal(g.Player1), boss = Heal(g.Player2);
    Power(g);
    Check("W16 Life Tap on the run player's turn: the boss's hero takes the 2, the run player's none", Heal(g.Player1) == me && Heal(g.Player2) == boss - 2,
        $"me {me}->{Heal(g.Player1)} boss {boss}->{Heal(g.Player2)}");
    me = Heal(g.Player1); boss = Heal(g.Player2);
    var mine = Board(g, g.Player1, "CS2_182");
    Play(g, ToHand(g, g.Player1, "CS2_062")); // Hellfire: 3 to all characters
    Check("W17 Hellfire: the boss's hero takes 3 + the run player's 3; the run player's hero none; minions take theirs",
        Heal(g.Player1) == me && Heal(g.Player2) == boss - 6 && mine.Health == 2, $"me {me}->{Heal(g.Player1)} boss {boss}->{Heal(g.Player2)} yeti {mine.Health}");
    g.Player1.Hero.Armor = 5; me = Heal(g.Player1); boss = Heal(g.Player2);
    Play(g, ToHand(g, g.Player1, "CS2_106")); // Fiery War Axe
    var enemy = Board(g, g.Player2, "CS2_182");
    HeroAttack(g, enemy);
    Check("W18 the hero attacks a Yeti: the Yeti's 4 goes to the boss's hero; the run player's Armor stays 5", enemy.Health == 2 && Heal(g.Player2) == boss - 4
        && Heal(g.Player1) == me && g.Player1.Hero.Armor == 5, $"boss {boss}->{Heal(g.Player2)} armor {g.Player1.Hero.Armor}");
    EndTurn(g); Mana(g.Player2, 10);
    me = Heal(g.Player1); int armor = g.Player1.Hero.Armor; boss = Heal(g.Player2);
    Play(g, ToHand(g, g.Player2, "DS1_185"), g.Player1.Hero);
    Check("W19 on the boss's turn the run player's hero takes damage as usual (Arcane Shot: 2 off the Armor)", g.Player1.Hero.Armor == armor - 2 && Heal(g.Player2) == boss);
    Play(g, ToHand(g, g.Player2, "EX1_332"), ShrineOf(g, g.Player1));
    EndTurn(g); Mana(g.Player1, 10);
    me = Heal(g.Player1) + g.Player1.Hero.Armor; boss = Heal(g.Player2);
    Power(g);
    Check("W20 silenced, the Hunger redirects nothing (Life Tap hurts its owner)", Heal(g.Player1) + g.Player1.Hero.Armor == me - 2 && Heal(g.Player2) == boss);
    // A boss-side copy (Faceless): no loop; each Hunger redirects only on its own owner's turn.
    var (s2, p2, g2) = F("rumble_warlock_179", 304);
    EndTurn(g2); Mana(g2.Player2, 10); ClearExceptShrines(g2);
    Play(g2, ToHand(g2, g2.Player2, "EX1_564"), ShrineOf(g2, g2.Player1));
    var copy = g2.Player2.BoardZone.FirstOrDefault(m => m.Card.Id == "TRLA_179");
    me = Heal(g2.Player1); boss = Heal(g2.Player2);
    Play(g2, ToHand(g2, g2.Player2, "CS2_062")); // the boss's Hellfire on its own turn
    Check("W21 a boss copy (plain minion) on the boss's turn: the boss's 3 goes to the run player (3 + 3), the boss's hero none",
        copy != null && !ShrineRules.IsMarked(copy) && Heal(g2.Player1) == me - 6 && Heal(g2.Player2) == boss, $"me {me}->{Heal(g2.Player1)} boss {boss}->{Heal(g2.Player2)}");
    Check("W21 the guard is cleared after the redirect", !g2.NativeTags.ContainsKey(DungeonRunShrines.HungerResolving));
    EndTurn(g2); Mana(g2.Player1, 10);
    me = Heal(g2.Player1); boss = Heal(g2.Player2);
    Power(g2);
    Check("W22 then on the run player's turn their Life Tap goes to the boss (the copy does not send it back)", Heal(g2.Player1) == me && Heal(g2.Player2) == boss - 2);
    // Paying Health (Blood Pact's rule) is not damage: a plain Hunger copy beside a Blood Pact redirects none of it.
    var (s3, p3, g3) = F("rumble_warlock_113", 305);
    Mana(g3.Player1, 10); ClearExceptShrines(g3);
    Board(g3, g3.Player1, "TRLA_179");
    me = Heal(g3.Player1); boss = Heal(g3.Player2);
    Play(g3, ToHand(g3, g3.Player1, "CS2_024"), Board(g3, g3.Player2, "CS2_186")); // Frostbolt paid with 2 Health
    Check("W23 Blood Pact's Health payment is not damage: the Hunger copy redirects none of it (run player -2, boss 0)", Heal(g3.Player1) == me - 2 && Heal(g3.Player2) == boss,
        $"me {me}->{Heal(g3.Player1)} boss {boss}->{Heal(g3.Player2)}");
    Power(g3);
    Check("W23 Life Tap's damage beside it is redirected (boss -2)", Heal(g3.Player2) == boss - 2 && Heal(g3.Player1) == me - 2);
});

// ================================================================ L dormancy and revival of each shrine
foreach (string id in Nine)
    Guard("L " + id, () =>
    {
        var (s, plan, g) = F(DeckOf(id), 400 + Array.IndexOf(Nine, id));
        var shrine = ShrineOf(g, g.Player1);
        int slot = shrine.ZonePosition, oldId = shrine.Id;
        Mana(g.Player1, 10);
        Damage(g, shrine, 20);
        var form = ShrineOf(g, g.Player1);
        Check($"L1 {id} killed: its {id}t form in the slot it left, untouchable, 3 turns; the corpse stays out of resurrect pools",
            form?.Card.Id == id + "t" && form.ZonePosition == slot && form.Untouchable && ShrineRules.TurnsLeft(form) == 3
            && g.Player1.GraveyardZone.Any(e => e.Id == oldId && ShrineRules.IsCorpse(e)), $"{form?.Card.Id} {ShrineRules.TurnsLeft(form)}");
        Mana(g.Player1, 10); ClearExceptShrines(g); ClearHand(g, g.Player1);
        bool asleep;
        HashSet<int> bossDeck = null;
        switch (id)
        {
            case "TRLA_106": { var m = Board(g, g.Player1, "CS2_182"); asleep = !m.HasStealth; break; }
            case "TRLA_186": bossDeck = DeckIds(g.Player2); asleep = true; break; // read at turn 3, still dormant
            case "TRLA_187": { int b = Heal(g.Player2); Play(g, ToHand(g, g.Player1, "DS1_185"), g.Player2.Hero); asleep = Heal(g.Player2) == b - 2; break; }
            case "TRLA_109": { var m = Board(g, g.Player1, "CS2_182"); Play(g, ToHand(g, g.Player1, "EX1_238"), g.Player2.Hero); asleep = m.AttackDamage == 4; break; }
            case "TRLA_154": { int b = Heal(g.Player2); Play(g, ToHand(g, g.Player1, "CS2_189"), g.Player2.Hero); asleep = Heal(g.Player2) == b - 1 && BattlecryRules.Additional(g.Player1) == 0; break; }
            case "TRLA_155": asleep = ToHand(g, g.Player1, "CS2_029").Cost == 4; break;
            case "TRLA_113": asleep = CostRules.PaymentKind(ToHand(g, g.Player1, "CS2_029")) == CostPaymentKind.Mana; break;
            case "TRLA_178": ToHand(g, g.Player1, "CS2_231"); Play(g, ToHand(g, g.Player1, "EX1_308"), g.Player2.Hero); asleep = g.Player1.BoardZone.Count(m => !ShrineRules.IsMarked(m)) == 0; break;
            default: { int me = Heal(g.Player1), b = Heal(g.Player2); Power(g); asleep = Heal(g.Player1) == me - 2 && Heal(g.Player2) == b; break; }
        }
        var seen = new List<string>();
        for (int t = 3; t <= 7; t += 2)
        {
            if (id == "TRLA_186" && t == 7) bossDeck = DeckIds(g.Player2);
            ToTurn(g, t);
            if (id == "TRLA_186" && t == 3) asleep = Stolen(g, bossDeck).Count == 0;
            seen.Add($"t{t}:{ShrineOf(g, g.Player1)?.Card.Id}");
        }
        Check($"L2 {id}'s dormant form does nothing (its aura or trigger is asleep)", asleep);
        Check($"L3 {id} revives at the start of the owner's turn 7", string.Join(" ", seen) == $"t3:{id}t t5:{id}t t7:{id}", string.Join(" ", seen));
        var back = ShrineOf(g, g.Player1);
        Check($"L4 {id} revived at full Health as a fresh entity, generation 1", back != null && back.Id != oldId && back.Damage == 0 && back.Health == C(id).Health
            && back[ShrineRules.Generation] == 1 && ShrineRules.IsAwake(back));
        bool works;
        if (id == "TRLA_186") works = Stolen(g, bossDeck).Count == 1; // the revival comes before the turn-start steal
        else
        {
            Mana(g.Player1, 10); ClearExceptShrines(g); ClearHand(g, g.Player1);
            switch (id)
            {
                case "TRLA_106": { var m = Board(g, g.Player1, "CS2_182"); works = m.HasStealth && !back.HasStealth; break; }
                case "TRLA_187": { int b = Heal(g.Player2); Play(g, ToHand(g, g.Player1, "DS1_185"), g.Player2.Hero); works = Heal(g.Player2) == b - 4; break; }
                case "TRLA_109": { var m = Board(g, g.Player1, "CS2_182"); Play(g, ToHand(g, g.Player1, "EX1_238"), g.Player2.Hero); works = m.AttackDamage == 5 && back.AttackDamage == 1; break; }
                case "TRLA_154": { int b = Heal(g.Player2); Play(g, ToHand(g, g.Player1, "CS2_189"), g.Player2.Hero); works = Heal(g.Player2) == b - 3; break; }
                case "TRLA_155": works = ToHand(g, g.Player1, "CS2_029").Cost == 2; break;
                case "TRLA_113": works = CostRules.PaymentKind(ToHand(g, g.Player1, "CS2_029")) == CostPaymentKind.Health; break;
                case "TRLA_178": ToHand(g, g.Player1, "CS2_231"); Play(g, ToHand(g, g.Player1, "EX1_308"), g.Player2.Hero); works = g.Player1.BoardZone.Count(m => !ShrineRules.IsMarked(m)) == 1; break;
                default: { int me = Heal(g.Player1), b = Heal(g.Player2); Power(g); works = Heal(g.Player1) == me && Heal(g.Player2) == b - 2; break; }
            }
        }
        Check($"L5 {id}'s aura or trigger works again after the revival", works);
    });

// ================================================================ X interactions with treasures, upgrades and guards
Guard("X1", () =>
{
    var (s, plan, g) = F("rumble_shaman_154", 501, treasures: new[] { "LOOTA_846" }); // Battle Totem: Battlecries trigger twice
    var shrine = ShrineOf(g, g.Player1);
    Check("X1 Battle Totem with Tribute from the Tides: 1 + 1 + 2 = 4", BattlecryRules.Repeats(g.Player1, shrine) == 4, BattlecryRules.Repeats(g.Player1, shrine).ToString());
    Mana(g.Player1, 10); ClearExceptShrines(g); ClearHand(g, g.Player1);
    int boss = Heal(g.Player2);
    Play(g, ToHand(g, g.Player1, "CS2_189"), g.Player2.Hero);
    Check("X1 Elven Archer: four Battlecries (4 to the boss's hero)", Heal(g.Player2) == boss - 4, $"{boss}->{Heal(g.Player2)}");
    boss = Heal(g.Player2);
    Play(g, ToHand(g, g.Player1, "CS2_042"), g.Player2.Hero); // Fire Elemental: 3
    Check("X1 Fire Elemental: 4 x 3 = 12", Heal(g.Player2) == boss - 12, $"{boss}->{Heal(g.Player2)}");
    var (s2, p2, g2) = F("rumble_shaman_154", 502, upgrades: new[] { DungeonRunShrines.EverlastingDevotion });
    Check("X2 TRLA_804: two Tributes, 1 + 2 + 2 = 5", ShrineRules.Of(g2.Player1).Count == 2 && BattlecryRules.Repeats(g2.Player1, ShrineOf(g2, g2.Player1)) == 5);
    Kill(g2, ShrineRules.Of(g2.Player1)[0]);
    Check("X2 one Tribute dormant: 1 + 2 = 3", BattlecryRules.Repeats(g2.Player1, ShrineOf(g2, g2.Player1)) == 3);
    var (s3, p3, g3) = F("rumble_shaman_154", 503);
    Mana(g3.Player1, 10); ClearExceptShrines(g3); ClearHand(g3, g3.Player1);
    Board(g3, g3.Player1, "LOE_077"); // Brann Bronzebeard
    int b3 = Heal(g3.Player2);
    Play(g3, ToHand(g3, g3.Player1, "CS2_189"), g3.Player2.Hero);
    Check("X3 Brann Bronzebeard with Tribute: 1 + 1 + 2 = 4 Battlecries", Heal(g3.Player2) == b3 - 4, $"{b3}->{Heal(g3.Player2)}");
});
Guard("X4", () =>
{
    var (s, plan, g) = F("rumble_rogue_106", 504, treasures: new[] { "LOOTA_845" }); // Totem of the Dead: Deathrattles trigger twice
    Mana(g.Player1, 10); ClearExceptShrines(g);
    var yeti = Board(g, g.Player1, "CS2_182");
    Kill(g, ShrineOf(g, g.Player1));
    Check("X4 Totem of the Dead doubles Bottled Terror's Deathrattle: the Yeti +4 Attack (8/5)", yeti.AttackDamage == 8, yeti.AttackDamage.ToString());
    var (s2, p2, g2) = F("rumble_rogue_106", 505, treasures: new[] { "LOOTA_832" }); // Cloak of Invisibility
    var shrine = ShrineOf(g2, g2.Player1);
    Mana(g2.Player1, 10); ClearExceptShrines(g2);
    var y2 = Board(g2, g2.Player1, "CS2_182");
    Check("X5 Cloak of Invisibility stealths the shrine itself (Bottled Terror does not), and the Yeti", shrine.HasStealth && y2.HasStealth);
    Kill(g2, shrine);
    Check("X5 the Terror dormant, the Cloak's Stealth stays on the Yeti (+2 Attack from the Deathrattle)", y2.HasStealth && y2.AttackDamage == 6);
    var (s3, p3, g3) = F("rumble_rogue_106", 506);
    EndTurn(g3); Mana(g3.Player2, 10); ClearExceptShrines(g3);
    var y3 = Board(g3, g3.Player1, "CS2_182");
    Play(g3, ToHand(g3, g3.Player2, "EX1_246"), ShrineOf(g3, g3.Player1)); // Hex
    Check("X6 Hex on Bottled Terror: dormant (no Frog), and its Deathrattle fires (Yeti 6/5, no Stealth)",
        ShrineOf(g3, g3.Player1)?.Card.Id == "TRLA_106t" && !g3.Player1.BoardZone.Any(m => m.Card.Id == "hexfrog") && y3.AttackDamage == 6 && !y3.HasStealth,
        $"{ShrineOf(g3, g3.Player1)?.Card.Id} {y3.AttackDamage}");
    var (s4, p4, g4) = F("rumble_rogue_106", 507);
    EndTurn(g4); Mana(g4.Player2, 10); ClearExceptShrines(g4);
    var y4 = Board(g4, g4.Player1, "CS2_182");
    Play(g4, ToHand(g4, g4.Player2, "EX1_581"), ShrineOf(g4, g4.Player1)); // Sap: a bounce
    Check("X7 Sap on Bottled Terror: destroyed instead of bounced, dormant, Deathrattle fired", ShrineOf(g4, g4.Player1)?.Card.Id == "TRLA_106t" && y4.AttackDamage == 6
        && !g4.Player1.HandZone.Any(p => p.Card.Id == "TRLA_106"));
});
Guard("X8", () =>
{
    var (s, plan, g) = F("rumble_shaman_155", 508, treasures: new[] { "LOOTA_824" }); // Khadgar's Scrying Orb: spells cost (1) less
    Mana(g.Player1, 10); ClearHand(g, g.Player1);
    var fb = ToHand(g, g.Player1, "CS2_029"); var bolt = ToHand(g, g.Player1, "CS2_024");
    Check("X8 Scrying Orb (1) and Krag'wa's Grace (2) stack: Fireball 4 -> 1, Frostbolt 2 -> 0", fb.Cost == 1 && bolt.Cost == 0, $"{fb.Cost} {bolt.Cost}");
    var (s2, p2, g2) = F("rumble_warlock_113", 509, treasures: new[] { "LOOTA_824" });
    Mana(g2.Player1, 10); ClearExceptShrines(g2); ClearHand(g2, g2.Player1);
    var fb2 = ToHand(g2, g2.Player1, "CS2_029");
    int me = Heal(g2.Player1);
    Play(g2, fb2, g2.Player2.Hero);
    Check("X9 Scrying Orb with Blood Pact: Fireball is paid with 3 Health", Heal(g2.Player1) == me - 3, $"{me}->{Heal(g2.Player1)}");
});
Guard("X10", () =>
{
    var (s, plan, g) = F("rumble_rogue_187", 510, upgrades: new[] { DungeonRunShrines.EverlastingDevotion });
    Mana(g.Player1, 10); ClearExceptShrines(g); ClearHand(g, g.Player1);
    int boss = Heal(g.Player2);
    Play(g, ToHand(g, g.Player1, "DS1_185"), g.Player2.Hero);
    Check("X10 TRLA_804: two Pirate's Marks, each casts the spell once more (3 Arcane Shots: 6), never a recast of a recast",
        ShrineRules.Of(g.Player1).Count == 2 && Heal(g.Player2) == boss - 6, $"{boss}->{Heal(g.Player2)}");
    var (s2, p2, g2) = F("rumble_rogue_186", 511, upgrades: new[] { DungeonRunShrines.PactWithTheLoa });
    Kill(g2, ShrineOf(g2, g2.Player1));
    var seen = new List<string> { "t1:" + ShrineRules.TurnsLeft(ShrineOf(g2, g2.Player1)) };
    ToTurn(g2, 3); seen.Add("t3:" + ShrineOf(g2, g2.Player1)?.Card.Id);
    var deck = DeckIds(g2.Player2);
    ToTurn(g2, 5); seen.Add("t5:" + ShrineOf(g2, g2.Player1)?.Card.Id);
    Check("X11 TRLA_805 with Treasure from Below: 2 turns, revived at the start of turn 5, and it steals that very turn",
        string.Join(" ", seen) == "t1:2 t3:TRLA_186t t5:TRLA_186" && Stolen(g2, deck).Count == 1, string.Join(" ", seen));
    var (s3, p3, g3) = F("rumble_warlock_179", 512, upgrades: new[] { DungeonRunShrines.EverlastingDevotion });
    Mana(g3.Player1, 10);
    int me = Heal(g3.Player1); int b3 = Heal(g3.Player2);
    Power(g3);
    Check("X12 TRLA_804: two Hungers redirect a hit once (the boss takes 2, not 4)", ShrineRules.Of(g3.Player1).Count == 2 && Heal(g3.Player1) == me && Heal(g3.Player2) == b3 - 2);
    var (s4, p4, g4) = F("rumble_shaman_109", 513);
    EndTurn(g4); Mana(g4.Player2, 10);
    Play(g4, ToHand(g4, g4.Player2, "CS1_113"), ShrineOf(g4, g4.Player1)); // Mind Control
    Check("X13 Mind Control on Krag'wa's Lure does nothing: it stays the run player's", ShrineOf(g4, g4.Player1)?.Controller == g4.Player1 && ShrineRules.IsAwake(ShrineOf(g4, g4.Player1)));
    Mana(g4.Player2, 10);
    Play(g4, ToHand(g4, g4.Player2, "EX1_564"), ShrineOf(g4, g4.Player1)); // Faceless copy
    var copy = g4.Player2.BoardZone.FirstOrDefault(m => m.Card.Id == "TRLA_109");
    var bossYeti = Board(g4, g4.Player2, "CS2_182");
    Play(g4, ToHand(g4, g4.Player2, "EX1_238"), g4.Player1.Hero);
    Check("X14 a boss copy of the Lure (plain minion) buffs the boss's minions after the boss's Overload, not the run player's",
        copy != null && !ShrineRules.IsMarked(copy) && bossYeti.AttackDamage == 5 && ShrineOf(g4, g4.Player1).AttackDamage == 0);
    if (copy != null) Kill(g4, copy);
    Check("X14 killed, the copy dies for good (no dormant form)", !g4.Player2.BoardZone.Any(m => m.Card.Id == "TRLA_109t"));
});

Console.WriteLine($"SUMMARY checks passed {passes} failed {failures}");
return failures == 0 ? 0 : 1;
