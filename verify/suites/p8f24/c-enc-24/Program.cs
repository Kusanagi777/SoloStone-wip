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
using SabberStoneCore.Tasks.SimpleTasks;

// Phase 8 family 24, the Plague Lords and Tekahn (real engine games through the run setup path: DungeonRun.BuildPlan on the
// shipped row -> DungeonRunSetup.Configure / ApplyAfterStart): Xatma (Foreboding Fate, Death's Embrace, Death's Gaze, Untimely
// Demise), K'zrath (Systematic Betrayal, Evoke Panic, Murmurs of Mortality), Vesh (Made of Murlocs, Ashes Anew, Mur-nado, Scaly
// Golem and his special cards), Tekahn (Awaken the Flame, Curse of Flame, Soul Pyre, the golems, Rakanishu, The Final Plague?!,
// the explorers' relay with BOT_543 Omega Mind), the phases with deck replacement and persistent damage, ULDA_115
// "Gnomebliterator" and its run state, the rows, the admission rows and the validator.
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
DungeonRunData Load(string schedule = null) => DungeonRunData.Parse(R("dungeon-run/run-v1.json"), R("dungeon-run/pool-v1.json"), R("npc/decks-v1.json"),
    R("dungeon-run/catalog-v1.json"), schedule ?? R("dungeon-run/schedule-v1.json"), R("dungeon-run/tavern-v1.json"), R("dungeon-run/encounters-v1.json"));
var data = Load();
var run = new DungeonRun(data);
var all = new DungeonRunProfile(); all.Unlocked.AddRange(data.Unlocks.Keys);
var xml = new System.Xml.Serialization.XmlSerializer(typeof(DungeonRunSave));
string Xml(DungeonRunSave save) { using var w = new StringWriter(); xml.Serialize(w, save); return w.ToString(); }

const string Yeti = "CS2_182", Croc = "CS2_120", Wisp = "CS2_231", Moonfire = "CS2_008", Hoarder = "EX1_096", ArcaneExplosion = "CS2_025";
const string MurlocRaider = "CS2_168", Bluegill = "CS2_173", Shrine = "TRLA_154", Lackey = "DAL_614", Omega = "BOT_543";
const string Xatma = "ulda_37_xatma", Kzrath = "ulda_38_kzrath", Vesh = "ulda_39_vesh", Tekahn = "ulda_67_tekahn", Icarax = "ulda_40_icarax";
string[] Thirty(string id) => Enumerable.Repeat(id, 30).ToArray();

// A run on acts-v3h at its first pre-match, with the boss under test on that floor (as the encounter bosspilot harness does).
DungeonRunState At(string cls, long seed, string bossId, string schedule = "acts-v3h", DungeonRun r = null)
{
    r ??= run;
    var c = r.Data.Class(cls);
    var s = r.NewRun(all, c.Id, c.Decks[0], c.Powers[0], seed, schedule: schedule, signatureId: r.SignatureOffer(all, c, seed).FirstOrDefault());
    while (s.Phase != DungeonRunPhase.PreMatch)
    {
        if (s.Phase != DungeonRunPhase.Modifier) throw new Exception("walk stopped at " + s.Phase);
        r.PickModifier(s, s.ModifierOffer.Kind == DungeonRunGlobalRule.KindAnomaly ? DungeonRunGlobalRule.None : s.ModifierOffer.Offered[0]);
    }
    s.BossSequence[s.Floor - 1] = bossId;
    return s;
}
DungeonRunProfile Banked(string bossId, int remaining)
{
    var p = new DungeonRunProfile(); p.Unlocked.AddRange(data.Unlocks.Keys);
    p.BossDamage.Add(new DungeonRunBossDamage { Boss = bossId, Remaining = remaining, Attempts = 1 });
    return p;
}
RunMatchPlan RowPlan(string bossId, DungeonRunProfile profile = null, long seed = 4400) => run.BuildPlan(At("MAGE", seed, bossId), profile ?? all);
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
// The shipped row's plan through the run layer (its forms with their decks, its banked damage); the starting decks are plain
// cards so a test controls what is drawn. A profile with a record starts the boss in the form its Health belongs to.
Game Fight(string bossId, DungeonRunProfile profile = null, string bossDeck = Yeti, string playerDeck = Yeti, string shrine = null, int seed = 7)
{
    var plan = RowPlan(bossId, profile);
    plan.BossDeck = Thirty(bossDeck).Select(C).ToList();
    plan.PlayerDeck = Thirty(playerDeck).Select(C).ToList();
    plan.PlayerDeckSlots = null;
    if (shrine != null) plan.PlayerShrine = shrine;
    return Ready(plan, seed);
}
void Mana(Controller c, int n = 10) { c.BaseMana = n; c.UsedMana = 0; c.OverloadLocked = 0; c.OverloadOwed = 0; }
IPlayable ToHand(Game g, Controller c, string id)
{
    while (c.HandZone.Count >= 9) { var old = c.HandZone[0]; c.HandZone.Remove(old); c.SetasideZone.Add(old); }
    var e = Entity.FromCard(c, C(id)); Generic.AddHandPhase.Invoke(c, e); g.DeathProcessingAndAuraUpdate(); return e;
}
void Clear(Game g, Controller c) { foreach (var m in c.BoardZone.ToArray()) { c.BoardZone.Remove(m); c.SetasideZone.Add(m); } g.DeathProcessingAndAuraUpdate(); }
void EmptyHand(Controller c) { foreach (var p in c.HandZone.ToArray()) { c.HandZone.Remove(p); c.SetasideZone.Add(p); } }
Minion Board(Game g, Controller c, string id) { var m = (Minion)Entity.FromCard(c, C(id)); Generic.SummonBlock(g, m, -1, c.Hero); g.DeathProcessingAndAuraUpdate(); return m; }
bool Play(Game g, IPlayable card, ICharacter target = null) { Mana(g.CurrentPlayer); return g.Process(PlayCardTask.Any(g.CurrentPlayer, card, target, -1, 0)); }
bool Cast(Game g, string id, ICharacter target = null) => Play(g, ToHand(g, g.CurrentPlayer, id), target);
void EndTurn(Game g) => g.Process(EndTurnTask.Any(g.CurrentPlayer));
bool Offered(Game g) => g.CurrentPlayer.Options().Any(o => o is HeroPowerTask);
List<ICharacter> PowerTargets(Game g) => g.CurrentPlayer.Options().OfType<HeroPowerTask>().Select(o => o.Target).Where(t => t != null).ToList();
bool Power(Game g, ICharacter target = null) => Offered(g) && g.Process(HeroPowerTask.Any(g.CurrentPlayer, target));
void ToBoss(Game g) { if (g.CurrentPlayer == g.Player1) EndTurn(g); Mana(g.Player2); }
void ToPlayer(Game g) { if (g.CurrentPlayer == g.Player2) EndTurn(g); Mana(g.Player1); }
void Kill(Game g, Minion m) { new DestroyTask(EntityType.TARGET).Process(g, m.Controller, m.Controller.Hero, m, null); g.DeathProcessingAndAuraUpdate(); }
void Hurt(Game g, Hero h, int toHealth) { h.Damage += h.Health - toHealth; g.DeathProcessingAndAuraUpdate(); }
int Count(Controller c, string id) => c.BoardZone.Count(m => m.Card.Id == id);
bool Has(IPlayable p, string enchantmentId) => p.AppliedEnchantments?.Any(e => e.Card.Id == enchantmentId) == true;
string Hero(Controller c) => c.Hero.Card.Id + "/" + c.Hero.HeroPower.Card.Id + " " + c.Hero.Health + "hp";
string Row(Controller c) => string.Join(",", c.BoardZone.Select(m => m.Card.Id + ":" + m.AttackDamage + "/" + m.Health));

// ============================================================ P  the rows (forms, decks, relay) and persistent damage in the plan
Guard("Rows", () =>
{
    var errors = DungeonRunValidator.Validate(data, null);
    Check("P1 the shipped data validates with the family 24 rows", errors.Count == 0, string.Join(" | ", errors.Take(4)));
    var lords = new (string id, string n, string[] powers, int draw)[]
    {
        (Xatma, "37", new[] { "ULDA_BOSS_37p1", "ULDA_BOSS_37p2", "ULDA_BOSS_37p3" }, 3),
        (Kzrath, "38", new[] { "ULDA_BOSS_38p2", "ULDA_BOSS_38p1", "ULDA_BOSS_38p3" }, 3),
        (Vesh, "39", new[] { "ULDA_BOSS_39p1", "ULDA_BOSS_39p2", "ULDA_BOSS_39p3" }, 5),
    };
    bool shape = true; string why = "";
    foreach (var (id, n, powers, draw) in lords)
    {
        var b = data.Boss(id);
        bool ok = b.Family == "24" && b.Tier == "major3" && b.Provenance == "ULDA" && b.HeroCard == "ULDA_BOSS_" + n + "h" && b.Power == powers[0] && b.Health == 300 && b.FixedHealth
            && b.HasFlag("forms") && b.HasFlag("persistentDamage") && b.HasFlag("deckReplace") && b.Forms.Count == 2
            && b.Forms[0].Hero == "ULDA_BOSS_" + n + "h2" && b.Forms[0].Power == powers[1] && b.Forms[0].Health == 200 && b.Forms[0].HealthAtMost == 200 && b.Forms[0].KeepExcess && b.Forms[0].Draw == draw
            && b.Forms[1].Hero == "ULDA_BOSS_" + n + "h3" && b.Forms[1].Power == powers[2] && b.Forms[1].Health == 100 && b.Forms[1].HealthAtMost == 100 && b.Forms[1].Draw == draw
            && data.BossDecks[b.Deck].Count == 30 && b.Forms.All(f => data.BossDecks[f.Deck].Count >= 30) && b.Source.Url.StartsWith("https://hearthstone.wiki.gg/wiki/");
        if (!ok) { shape = false; why += id + " "; }
    }
    Check("P2 Xatma, K'zrath (300 form: Systematic Betrayal, 200 form: Evoke Panic) and Vesh are major3 rows of three hero forms at 300/200/100, each phase with its own deck and 3 (Vesh: 5) extra draws, persistent damage",
        shape, why);
    var t = data.Boss(Tekahn);
    var relay = t.Relay;
    Check("P3 Tekahn is the finale row: 300 Health, power-only forms at 200 (Curse of Flame) and 100 (Soul Pyre) with 40-card phase decks, no persistent damage",
        t.Tier == "finale" && t.HeroCard == "ULDA_BOSS_67h" && t.Power == "ULDA_BOSS_67p1" && t.Health == 300 && !t.HasFlag("persistentDamage") && t.HasFlag("relay")
        && t.Forms.Count == 2 && t.Forms.All(f => f.Hero == null) && t.Forms[0].Power == "ULDA_BOSS_67p2" && t.Forms[1].Power == "ULDA_BOSS_67p3"
        && data.BossDecks[t.Deck].Count == 40 && t.Forms.All(f => data.BossDecks[f.Deck].Count == 40), t.Forms.Count + " forms");
    var adv = System.Text.Json.JsonDocument.Parse(R("dungeon-run/reference/adventures.json")).RootElement.GetProperty("adventures").EnumerateArray()
        .First(a => a.GetProperty("code").GetString() == "ULDA").GetProperty("otherPrefixedDecks").EnumerateArray()
        .ToDictionary(d => d.GetProperty("noteName").GetString(), d => d.GetProperty("cards").EnumerateArray().Select(x => x.GetString()).OrderBy(x => x, StringComparer.Ordinal).ToList());
    bool decks = relay.Count == 12;
    foreach (var h in relay)
    {
        string explorer = h.Hero.Substring("ULDA_".Length);
        int n = int.Parse(h.Power.Substring(h.Power.Length - 1));
        var listed = adv["ULDA_" + explorer + "_Tekanh_Deck_" + n];
        // The client's 30 cards, less the few the engine does not script (each recorded as a substitution, filled to 30 with second copies).
        var deck = data.BossDecks[h.Deck];
        decks &= h.Health == 50 && h.SkipFor.SequenceEqual(new[] { h.Hero }) && deck.Count == 30 && deck.All(listed.Contains)
            && listed.Distinct().Count(id => !deck.Contains(id)) <= 3;
    }
    Check("P4 the relay lists each explorer three times: hero power n with its client finale deck n (ULDA_<Explorer>_Tekanh_Deck_n: 30 of its cards, an unscripted one replaced by a second copy), 50 Health, skipped for the run's own explorer",
        decks && relay.Select(h => h.Hero).Distinct().SequenceEqual(new[] { "ULDA_Elise", "ULDA_Finley", "ULDA_Brann", "ULDA_Reno" }), relay.Count + " entries");
    Check("P5 BOT_543 Omega Mind is in exactly one finale deck (Finley's third, two copies), and every finale-deck card is scripted",
        relay.Count(h => data.BossDecks[h.Deck].Contains(Omega)) == 1 && data.BossDecks["enc-ulda_67_tekahn-relay-ULDA_Finley-3"].Contains(Omega)
        && relay.SelectMany(h => data.BossDecks[h.Deck]).Distinct().All(id => C(id).Implemented));

    var plan = RowPlan(Xatma);
    Check("P6 BuildPlan: the boss starts in form 0 at full Health; its forms carry the hero, power, Health and deck of each phase", plan.BossHealth == 300 && plan.BossCarriedDamage == 0
        && plan.BossFormStart == 0 && plan.BossForms.Count == 3 && plan.BossForms[1].Hero.Id == "ULDA_BOSS_37h2" && plan.BossForms[1].Power.Id == "ULDA_BOSS_37p2"
        && plan.BossForms[2].Deck.Count == data.BossDecks[data.Boss(Xatma).Forms[1].Deck].Count && plan.BossForms[1].Draw == 3, plan.BossFormStart + " " + plan.BossHealth);
    var p2 = RowPlan(Kzrath, Banked(Kzrath, 150));
    var p3 = RowPlan(Vesh, Banked(Vesh, 90));
    Check("P7 banked damage (profile scope) starts the next fight in the phase its Health belongs to: K'zrath at 150 in form 1 (Evoke Panic, the phase-2 deck), Vesh at 90 in form 2 (Mur-nado)",
        p2.BossFormStart == 1 && p2.BossHero.Id == "ULDA_BOSS_38h2" && p2.BossHeroPowerId == "ULDA_BOSS_38p1" && p2.BossHealth == 200 && p2.BossCarriedDamage == 50
        && p2.BossDeck.Select(c => c.Id).SequenceEqual(data.BossDecks[data.Boss(Kzrath).Forms[0].Deck])
        && p3.BossFormStart == 2 && p3.BossHero.Id == "ULDA_BOSS_39h3" && p3.BossHeroPowerId == "ULDA_BOSS_39p3" && p3.BossHealth == 100 && p3.BossCarriedDamage == 10,
        p2.BossFormStart + "/" + p2.BossCarriedDamage + " " + p3.BossFormStart + "/" + p3.BossCarriedDamage);
    var g = Ready(p2);
    Check("P8 in the game the banked boss is that form's hero and power at its banked Health, the form index set", g.Player2.Hero.Card.Id == "ULDA_BOSS_38h2"
        && g.Player2.Hero.HeroPower.Card.Id == "ULDA_BOSS_38p1" && g.Player2.Hero.Health == 150 && g.Player2[EncounterRules.FormIndex] == 1, Hero(g.Player2));
    // A won fight clears the record; a lost one banks the Health left.
    var s = At("MAGE", 4401, Xatma);
    var profile = Banked(Xatma, 240);
    run.BeginMatch(s);
    run.RecordOutcome(s, profile, DungeonRunOutcome.Loss, null, new RunMatchEnd(170, 1, 1, 5, new[] { "HERO_08" }, false));
    var s2 = At("MAGE", 4402, Xatma);
    var profile2 = Banked(Xatma, 240);
    run.BeginMatch(s2);
    run.RecordOutcome(s2, profile2, DungeonRunOutcome.Win, null, new RunMatchEnd(0, 2, 2, 5, new[] { "HERO_08" }, false));
    Check("P9 a lost fight banks the lord's Health left (profile, per lord) and a won one clears the record", profile.DamageOf(Xatma)?.Remaining == 170 && profile.DamageOf(Xatma).Attempts == 2
        && profile2.DamageOf(Xatma) == null, profile.DamageOf(Xatma)?.Remaining + "");
});

// ============================================================ X  Xatma
Guard("Xatma", () =>
{
    var g = Fight(Xatma);
    Controller boss = g.Player2, player = g.Player1;
    var yeti = Board(g, player, Yeti);
    ToBoss(g);
    bool none = !Offered(g);
    var hoarder = Board(g, boss, Hoarder);
    var own = Board(g, boss, Yeti);
    var targets = PowerTargets(g);
    int hand = boss.HandZone.Count, deck = boss.DeckZone.Count;
    Check("X1 Foreboding Fate costs (0), aims only at a friendly minion with a Deathrattle and triggers it (Loot Hoarder draws the boss a card); not offered without one",
        none && boss.Hero.HeroPower.Card.Cost == 0 && targets.Count == 1 && targets[0] == hoarder && Power(g, hoarder) && boss.HandZone.Count == hand + 1
        && boss.DeckZone.Count == deck - 1 && hoarder.Zone.Type == Zone.PLAY, string.Join(",", targets.Select(x => x.Card.Id)));

    // Phase change in play: 300 -> 199.
    EmptyHand(boss);
    int before = boss.HandZone.Count;
    Hurt(g, boss.Hero, 199);
    var phase2 = data.BossDecks[data.Boss(Xatma).Forms[0].Deck];
    Check("X2 at 200 Health or less Xatma becomes ULDA_BOSS_37h2 with Death's Embrace (passive, not clickable), keeps 199 Health, her deck is the phase-2 list and she draws 3 from it",
        boss.Hero.Card.Id == "ULDA_BOSS_37h2" && boss.Hero.HeroPower.Card.Id == "ULDA_BOSS_37p2" && boss.Hero.HeroPower.IsPassiveHeroPower && boss.Hero.Health == 199
        && boss.Hero.BaseHealth == 200 && boss[EncounterRules.FormIndex] == 1 && boss.HandZone.Count == before + 3 && boss.DeckZone.Count == phase2.Count - 3
        && boss.HandZone.Concat(boss.DeckZone).All(p => phase2.Contains(p.Card.Id)) && !Offered(g), Hero(boss) + " hand " + boss.HandZone.Count + " deck " + boss.DeckZone.Count);
    // Death's Embrace: the first minion of each player to die in a turn has Reborn.
    ToPlayer(g);
    Clear(g, boss); Clear(g, player);
    var w1 = Board(g, boss, Wisp); var w2 = Board(g, boss, Wisp); var pw = Board(g, player, Wisp);
    EndTurn(g); ToPlayer(g);
    Mana(player);
    bool cast = Cast(g, ArcaneExplosion);
    int reborn = boss.BoardZone.Count(m => m.Card.Id == Wisp);
    Kill(g, pw);
    int playerReborn = Count(player, Wisp);
    var back = player.BoardZone.FirstOrDefault(m => m.Card.Id == Wisp);
    if (back != null) Kill(g, back);
    int secondTime = Count(player, Wisp);
    Check("X3 Death's Embrace: two boss minions dying at once give one Reborn (the first only); the run player's first death this turn is reborn too, its second is not",
        cast && reborn == 1 && boss.BoardZone.Single(m => m.Card.Id == Wisp).Health == 1 && playerReborn == 1 && secondTime == 0, $"boss {reborn} player {playerReborn}/{secondTime}");
    EndTurn(g); ToPlayer(g);
    var again = Board(g, player, Wisp);
    Kill(g, again);
    Check("X4 the next turn the first death is reborn again", Count(player, Wisp) == 1);

    // Phase 3: Death's Gaze and Untimely Demise.
    foreach (var m in player.BoardZone.ToArray()) Kill(g, m);
    Hurt(g, boss.Hero, 90);
    ToBoss(g);
    foreach (var m in boss.BoardZone.ToArray()) Kill(g, m);
    EmptyHand(boss);
    var bw = Board(g, boss, Wisp); var by = Board(g, boss, Yeti); var py = Board(g, player, Yeti);
    bool gaze = boss.Hero.Card.Id == "ULDA_BOSS_37h3" && boss.Hero.HeroPower.Card.Id == "ULDA_BOSS_37p3" && boss.Hero.HeroPower.Card.Cost == 1 && Power(g);
    Check("X5 at 100 or less: ULDA_BOSS_37h3 with Death's Gaze (1): 1 damage to every minion; a minion died, so Untimely Demise joins the boss's hand (Death's Embrace is no longer active: the Wisp stays dead)",
        gaze && bw.Zone.Type != Zone.PLAY && Count(boss, Wisp) == 0 && by.Damage == 1 && py.Damage == 1 && boss.HandZone.Count(p => p.Card.Id == "ULDA_BOSS_37t") == 1, Hero(boss) + " " + Row(boss));
    EndTurn(g); EndTurn(g); Mana(boss);
    bool gaze2 = Power(g);
    Check("X6 when no minion dies Death's Gaze adds nothing", gaze2 && boss.HandZone.Count(p => p.Card.Id == "ULDA_BOSS_37t") == 1 && by.Damage == 2 && py.Damage == 2);
    int heroHealth = player.Hero.Health, yetiHealth = py.Health, bossYeti = by.Health;
    bool demise = Play(g, boss.HandZone.First(p => p.Card.Id == "ULDA_BOSS_37t"));
    Check("X7 Untimely Demise (3) deals 3 damage to every enemy (the run player's hero and minions), none to the boss's own", demise && C("ULDA_BOSS_37t").Cost == 3
        && player.Hero.Health == heroHealth - 3 && py.Health == yetiHealth - 3 && by.Health == bossYeti, $"{heroHealth - player.Hero.Health} {yetiHealth - py.Health} {bossYeti - by.Health}");
});

// ============================================================ K  K'zrath
Guard("Kzrath", () =>
{
    var g = Fight(Kzrath);
    Controller boss = g.Player2, player = g.Player1;
    ToBoss(g);
    bool empty = !Offered(g);
    var a = Board(g, player, Wisp); var b = Board(g, player, Yeti);
    var c = Board(g, boss, Croc); var d = Board(g, boss, Hoarder);
    bool used = Power(g);
    // Counter-clockwise as the run player sees it: its rightmost (Yeti) goes up to the boss's right end; the boss's leftmost
    // (Croc) comes down to the run player's left end.
    Check("K1 Systematic Betrayal (0), K'zrath's first form: not offered with both boards empty; it rotates the minions counter-clockwise (the run player's rightmost to the boss's right end, the boss's leftmost to the run player's left end)",
        empty && boss.Hero.HeroPower.Card.Id == "ULDA_BOSS_38p2" && used && b.Controller == boss && c.Controller == player && boss.BoardZone.Last() == b && player.BoardZone.First() == c
        && a.Controller == player && d.Controller == boss, "player " + Row(player) + " boss " + Row(boss));
    Check("K2 the two that switched sides gain +1/+1 (Chosen Betrayer); the others gain nothing", Has(b, "ULDA_BOSS_38p2e1") && Has(c, "ULDA_BOSS_38p2e1") && b.AttackDamage == 5 && b.Health == 6
        && c.AttackDamage == 3 && c.Health == 4 && !Has(a, "ULDA_BOSS_38p2e1") && !Has(d, "ULDA_BOSS_38p2e1"));

    var gs = Fight(Kzrath, shrine: Shrine);
    Controller bs = gs.Player2, ps = gs.Player1;
    var shrine = ps.BoardZone.First(m => ShrineRules.IsMarked(m));
    var pyeti = Board(gs, ps, Yeti);
    ToBoss(gs);
    var bcroc = Board(gs, bs, Croc);
    Power(gs);
    Check("K3 a run board shrine never changes sides: the rotation passes it over and moves the others", shrine.Controller == ps && pyeti.Controller == bs && bcroc.Controller == ps,
        "player " + Row(ps) + " boss " + Row(bs));

    // Phase 2: Evoke Panic.
    var g2 = Fight(Kzrath, Banked(Kzrath, 150));
    boss = g2.Player2; player = g2.Player1;
    var yeti = Board(g2, player, Yeti); var wisp = Board(g2, player, Wisp); var croc = Board(g2, player, Croc);
    var lone = Board(g2, boss, Yeti);
    ToBoss(g2);
    bool panic = boss.Hero.HeroPower.Card.Id == "ULDA_BOSS_38p1" && Power(g2);
    Check("K4 Evoke Panic (0), the 200 form: the lowest-Health minion of both rows (the run player's Wisp) attacks one of its neighbors (the Yeti or the Croc) and dies",
        panic && wisp.Zone.Type != Zone.PLAY && (yeti.Damage == 1) != (croc.Damage == 1) && lone.Damage == 0, Row(player));
    foreach (var m in player.BoardZone.ToArray().Skip(1)) Kill(g2, m);
    EndTurn(g2); EndTurn(g2); Mana(boss);
    Check("K5 it is not offered while no row holds two minions that can fight", player.BoardZone.Count <= 1 && boss.BoardZone.Count == 1 && !Offered(g2), Row(player) + " | " + Row(boss));

    // Phase 3: Murmurs of Mortality.
    var g3 = Fight(Kzrath, Banked(Kzrath, 90));
    boss = g3.Player2; player = g3.Player1;
    var big = Board(g3, player, Yeti);
    var mine = Board(g3, boss, Wisp);
    bool passive = boss.Hero.HeroPower.Card.Id == "ULDA_BOSS_38p3" && boss.Hero.HeroPower.IsPassiveHeroPower;
    Kill(g3, mine);
    Check("K6 Murmurs of Mortality (passive, the 100 form): after a friendly minion dies, an enemy minion above 1 Health is brought to 1 Health (no damage dealt: no damage event)",
        passive && big.Health == 1 && big.Zone.Type == Zone.PLAY, "yeti " + big.Health);
    var small = Board(g3, player, Wisp);
    var mine2 = Board(g3, boss, Wisp);
    Kill(g3, mine2);
    Check("K7 with no enemy minion above 1 Health it does nothing", small.Health == 1 && big.Health == 1);
});

// ============================================================ V  Vesh
Guard("Vesh", () =>
{
    var g = Fight(Vesh);
    Controller boss = g.Player2, player = g.Player1;
    int health = boss.Hero.Health;
    bool immune = boss.Hero.IsImmune;
    Generic.DamageCharFunc(player.Hero, boss.Hero, 6, false); g.DeathProcessingAndAuraUpdate();
    Check("V1 Made of Murlocs (passive): the boss's hero is Immune (6 damage does nothing)", boss.Hero.HeroPower.Card.Id == "ULDA_BOSS_39p1" && boss.Hero.HeroPower.IsPassiveHeroPower
        && immune && boss.Hero.Health == health, health + " -> " + boss.Hero.Health);
    var pm = Board(g, player, MurlocRaider); var bm = Board(g, boss, Bluegill); var other = Board(g, player, Yeti);
    Kill(g, pm); int afterOne = boss.Hero.Health;
    Kill(g, bm); int afterTwo = boss.Hero.Health;
    Kill(g, other);
    Check("V2 after any Murloc dies (either side) the Immune boss loses 3 Health; another minion's death costs nothing", afterOne == health - 3 && afterTwo == health - 6
        && boss.Hero.Health == health - 6, $"{afterOne} {afterTwo} {boss.Hero.Health}");
    Hurt(g, boss.Hero, 202);
    EmptyHand(boss);
    var last = Board(g, player, MurlocRaider);
    Kill(g, last);
    var phase2 = data.BossDecks[data.Boss(Vesh).Forms[0].Deck];
    Check("V3 the Murloc death that takes Vesh to 200 or less brings his next form at once: ULDA_BOSS_39h2 with Ashes Anew, 199 Health, no longer Immune, the phase-2 deck and 5 cards drawn",
        boss.Hero.Card.Id == "ULDA_BOSS_39h2" && boss.Hero.HeroPower.Card.Id == "ULDA_BOSS_39p2" && boss.Hero.Health == 199 && !boss.Hero.IsImmune && boss.HandZone.Count == 5
        && boss.DeckZone.Count == phase2.Count - 5, Hero(boss) + " hand " + boss.HandZone.Count);

    ToBoss(g);
    foreach (var m in boss.BoardZone.ToArray()) Kill(g, m);
    var target = Board(g, boss, MurlocRaider); var notMurloc = Board(g, boss, Yeti); var enemyMurloc = Board(g, player, Bluegill);
    for (int i = 0; i < 5; i++) Board(g, boss, Wisp);
    var targets = PowerTargets(g);
    bool ashes = Power(g, target);
    var golem = boss.BoardZone.FirstOrDefault(m => m.Card.Id == "ULDA_BOSS_39m");
    Check("V4 Ashes Anew (0) aims only at a friendly Murloc; it destroys it and summons a Scaly Golem (0/4... the client's 1/4 Taunt) in its place, a full board included",
        targets.Count == 1 && targets[0] == target && ashes && target.Zone.Type != Zone.PLAY && golem != null && golem.HasTaunt && boss.BoardZone.Count == 7
        && golem.ZonePosition == 0, string.Join(",", targets.Select(x => x.Card.Id)) + " " + Row(boss));
    var mine = Board(g, player, MurlocRaider);
    int a0 = enemyMurloc.AttackDamage, m0 = mine.AttackDamage;
    Kill(g, golem);
    Check("V5 Scaly Golem's Deathrattle gives every Murloc of both boards +1/+1", enemyMurloc.AttackDamage == a0 + 1 && mine.AttackDamage == m0 + 1 && Has(enemyMurloc, "ULDA_BOSS_39me")
        && notMurloc.AttackDamage == 4, $"{enemyMurloc.AttackDamage} {mine.AttackDamage}");

    // Phase 3: Mur-nado.
    var g3 = Fight(Vesh, Banked(Vesh, 90));
    boss = g3.Player2; player = g3.Player1;
    var murnado = boss.Hero.HeroPower;
    int cost0 = murnado.Cost;
    for (int i = 0; i < 5; i++) Kill(g3, Board(g3, i % 2 == 0 ? player : boss, MurlocRaider));
    Kill(g3, Board(g3, player, Yeti));
    int cost1 = murnado.Cost;
    var py = Board(g3, player, Yeti); var pw = Board(g3, player, Wisp); var bc = Board(g3, boss, Croc);
    ToBoss(g3);
    int deck = boss.DeckZone.Count, playerDeck = player.DeckZone.Count;
    bool used = Power(g3);
    Check("V6 Mur-nado costs (15) and (1) less after each Murloc death of either side while it is active (5 deaths: 10; a Yeti's death changes nothing)",
        murnado.Card.Id == "ULDA_BOSS_39p3" && cost0 == 15 && cost1 == 10 && murnado[EncounterRules.PlagueCount] == 5, $"{cost0} -> {cost1}");
    Check("V7 Mur-nado shuffles every minion of both boards into the boss's deck (the run player's become the boss's cards); both boards are empty",
        used && player.BoardZone.Count == 0 && boss.BoardZone.Count == 0 && boss.DeckZone.Count == deck + 3 && player.DeckZone.Count == playerDeck
        && boss.DeckZone.Count(p => p.Card.Id == Yeti) >= 1 && boss.DeckZone.Count(p => p.Card.Id == Wisp) == 1 && boss.DeckZone.Count(p => p.Card.Id == Croc) == 1 && py.Controller == boss,
        $"deck {deck} -> {boss.DeckZone.Count}");
    var gs = Fight(Vesh, Banked(Vesh, 90), shrine: Shrine);
    var shrine = gs.Player1.BoardZone.First(m => ShrineRules.IsMarked(m));
    ToBoss(gs);
    Power(gs);
    Check("V8 a run board shrine is not shuffled away: it goes dormant on its board, as from any such effect", shrine.Controller == gs.Player1
        && gs.Player1.BoardZone.Any(m => ShrineRules.IsMarked(m)), Row(gs.Player1));

    // Vesh's special cards.
    var gc = Fight(Vesh, Banked(Vesh, 150));
    boss = gc.Player2; player = gc.Player1;
    Kill(gc, Board(gc, player, MurlocRaider)); Kill(gc, Board(gc, boss, Bluegill));
    ToBoss(gc);
    foreach (var m in boss.BoardZone.ToArray()) Kill(gc, m);
    bool mrgl = Cast(gc, "LOEA10_5");
    int summoned = boss.BoardZone.Count(m => m.Card.IsRace(Race.MURLOC));
    int hand = boss.HandZone.Count, armor = boss.Hero.Armor;
    bool presence = Cast(gc, "KARA_13_12H");
    Check("V9 Mrgl Mrgl Nyah Nyah summons Murlocs that died this game (the two that died: up to 3); Demonic Presence draws 3 and gains 10 Armor",
        mrgl && summoned >= 1 && summoned <= 3 && presence && boss.HandZone.Count == hand + 3 && boss.Hero.Armor == armor + 10, $"summoned {summoned}");
    foreach (var m in boss.BoardZone.ToArray()) Kill(gc, m);
    Board(gc, boss, "LOEA16_24H");
    int h2 = boss.HandZone.Count;
    EndTurn(gc);
    Check("V10 Giantfin draws its owner 2 cards at the end of its turn", boss.HandZone.Count == Math.Min(10, h2 + 2), h2 + " -> " + boss.HandZone.Count);
});

// ============================================================ T  Tekahn
Guard("Tekahn", () =>
{
    var g = Fight(Tekahn);
    Controller boss = g.Player2, player = g.Player1;
    var py = Board(g, player, Yeti);
    ToBoss(g);
    bool none = !Offered(g);
    var golem = Board(g, boss, "ULDA_031");
    golem.Damage = 2;
    Generic.AddEnchantmentBlock(g, C("ULDA_BOSS_38p2e1"), boss.Hero, golem);
    var targets = PowerTargets(g);
    bool awaken = Power(g, golem);
    Check("T1 Awaken the Flame (2) aims only at a Sanctum Golem and turns it into an Enflamed Golem in place, keeping its damage and enchantments; not offered without one",
        none && C("ULDA_BOSS_67p1").Cost == 2 && targets.Count == 1 && targets[0] == golem && awaken && golem.Card.Id == "ULDA_032" && golem.Damage == 2 && Has(golem, "ULDA_BOSS_38p2e1")
        && golem.Zone.Type == Zone.PLAY, string.Join(",", targets.Select(x => x.Card.Id)) + " " + golem.Card.Id);
    var sanctum = Board(g, boss, "ULDA_031");
    sanctum.IsExhausted = false;
    bool canAttack = boss.Options().Any(o => o is MinionAttackTask a && a.Source == sanctum);
    int pyDamage = py.Damage;
    Kill(g, golem);
    Check("T2 Sanctum Golem can't attack; the Enflamed Golem's Deathrattle deals 3 damage to all minions and summons a Sanctum Golem",
        !canAttack && py.Damage == pyDamage + 3 && sanctum.Damage == 3 && Count(boss, "ULDA_031") == 2, Row(boss));

    // Phase 2: Curse of Flame.
    Hurt(g, boss.Hero, 199);
    Check("T3 at 200 or less Tekahn keeps his hero and Health and takes Curse of Flame; his deck is the phase-2 list (40 cards, no extra draw)",
        boss.Hero.Card.Id == "ULDA_BOSS_67h" && boss.Hero.HeroPower.Card.Id == "ULDA_BOSS_67p2" && boss.Hero.Health == 199 && boss.DeckZone.Count == 40, Hero(boss) + " deck " + boss.DeckZone.Count);
    ToBoss(g);
    foreach (var m in boss.BoardZone.ToArray()) Kill(g, m);
    var cursed = Board(g, player, Croc);
    var bossYeti = Board(g, boss, Yeti);
    targets = PowerTargets(g);
    bool curse = Power(g, cursed);
    int ph = player.Hero.Health, bh = boss.Hero.Health, ycr = cursed.Health, by = bossYeti.Health, yp = py.Health;
    EndTurn(g);
    bool quiet = player.Hero.Health == ph && boss.Hero.Health == bh;
    EndTurn(g);
    Check("T4 Curse of Flame (2) aims at an enemy minion only; the cursed minion living to the boss's next turn deals 5 damage to every character, then the curse is gone",
        targets.All(x => x.Controller == player && x is Minion) && curse && quiet && player.Hero.Health == ph - 5 && boss.Hero.Health == bh - 5 && bossYeti.Health == by - 5
        && cursed.Zone.Type != Zone.PLAY && py.Health == yp - 5 && !player.BoardZone.Concat(boss.BoardZone).Any(m => Has(m, "ULDA_BOSS_67p2e")), $"player {ph}->{player.Hero.Health} boss {bh}->{boss.Hero.Health}");
    Mana(boss);
    var second = Board(g, player, Yeti);
    Power(g, second);
    EndTurn(g);
    Kill(g, second);
    int ph2 = player.Hero.Health, bh2 = boss.Hero.Health;
    EndTurn(g);
    Check("T5 a cursed minion that dies before the boss's next turn deals nothing", player.Hero.Health == ph2 && boss.Hero.Health == bh2, $"{ph2}->{player.Hero.Health}");

    // Phase 3: Soul Pyre.
    Hurt(g, boss.Hero, 90);
    ToPlayer(g);
    int before = player.Hero.Health;
    bool played = Cast(g, Yeti);
    int bossBefore = boss.Hero.Health;
    ToBoss(g);
    bool bossPlayed = Cast(g, Croc);
    Check("T6 at 100 or less Soul Pyre (passive): after a player plays a card, that player's hero takes damage equal to its Cost (the run player's Yeti: 4; the boss's Croc: 2)",
        boss.Hero.HeroPower.Card.Id == "ULDA_BOSS_67p3" && boss.Hero.HeroPower.IsPassiveHeroPower && played && player.Hero.Health == before - 4 && bossPlayed && boss.Hero.Health == bossBefore - 2,
        $"{before}->{player.Hero.Health} {bossBefore}->{boss.Hero.Health}");

    // Rakanishu and The Final Plague?!.
    var gr = Fight(Tekahn);
    boss = gr.Player2; player = gr.Player1;
    ToBoss(gr);
    var held = ToHand(gr, boss, Lackey);
    var onBoard = Board(gr, boss, Lackey);
    bool raka = Cast(gr, "ULDA_033");
    var later = Board(gr, boss, Lackey);
    var theirs = Board(gr, player, Lackey);
    Check("T7 Rakanishu: for the rest of the game the boss's Lackeys are 4/4 (in hand, on the board, and those that come later); the run player's are not",
        raka && held.Card.Id == Lackey && ((Minion)held).AttackDamage == 4 && ((Minion)held).Health == 4 && onBoard.AttackDamage == 4 && onBoard.Health == 4
        && later.AttackDamage == 4 && later.Health == 4 && theirs.AttackDamage == 1 && theirs.Health == 1, $"{((Minion)held).AttackDamage}/{((Minion)held).Health} {onBoard.AttackDamage}/{onBoard.Health} {later.AttackDamage}/{later.Health}");
    foreach (var m in boss.BoardZone.ToArray()) Kill(gr, m);
    bool plague = Cast(gr, "ULDA_BOSS_67t");
    Check("T8 The Final Plague?! (10) summons C'Thun, N'Zoth, Y'Shaarj and Yogg-Saron (no Battlecry)", plague && C("ULDA_BOSS_67t").Cost == 10
        && boss.BoardZone.Select(m => m.Card.Id).SequenceEqual(new[] { "OG_280", "OG_133", "OG_042", "OG_134" }), Row(boss));
});

// ============================================================ L  the Tekahn relay (the finale's heroRelay)
Guard("Relay", () =>
{
    var s = At("MAGE", 4410, Tekahn);
    var plan = run.BuildPlan(s, all);
    run.BeginMatch(s);
    var sw = s.HeroSwitch;
    Check("L1 the relay plan: the run's own hero first, then the four explorers in order (Elise, Finley, Brann, Reno), each at 50 Health with one of its three entries; the run state records the same heroes and decks for the floor",
        plan.PlayerRelay.Count == 4 && plan.PlayerRelay.Select(h => h.Hero.Id).SequenceEqual(new[] { "ULDA_Elise", "ULDA_Finley", "ULDA_Brann", "ULDA_Reno" })
        && plan.PlayerRelay.All(h => h.Health == 50 && h.Deck.Count == 30) && sw != null && sw.Kind == DungeonRunHeroSwitch.KindRelay && sw.Floor == s.Floor
        && sw.Options.Select(o => o.Hero + "/" + o.Power).SequenceEqual(plan.PlayerRelay.Select(h => h.Hero.Id + "/" + h.Power.Id))
        && sw.Options.All(o => o.Deck != null && data.BossDecks[o.Deck].Count == 30 && o.Health == 50),
        string.Join(",", plan.PlayerRelay.Select(h => h.Hero.Id + "/" + h.Power.Id)));
    var again = At("MAGE", 4410, Tekahn);
    var plan2 = run.BuildPlan(again, all);
    var picks = new HashSet<string>();
    for (long seed = 4400; seed < 4440; seed++)
        foreach (var h in run.BuildPlan(At("MAGE", seed, Tekahn), all).PlayerRelay) picks.Add(h.Power.Id);
    Check("L2 the deck and power of each explorer is drawn from the run seed: the same seed gives the same relay, and forty seeds use all twelve entries",
        plan2.PlayerRelay.Select(h => h.Power.Id).SequenceEqual(plan.PlayerRelay.Select(h => h.Power.Id)) && picks.Count == 12, picks.Count + " entries");
    var reno = At("MAGE_ROGUE", 4410, Tekahn);
    var renoPlan = run.BuildPlan(reno, all);
    Check("L3 a run whose own hero is an explorer (Reno) meets the other three only", renoPlan.PlayerRelay.Select(h => h.Hero.Id).SequenceEqual(new[] { "ULDA_Elise", "ULDA_Finley", "ULDA_Brann" }),
        string.Join(",", renoPlan.PlayerRelay.Select(h => h.Hero.Id)));
    Check("L4 the match classes cover every relay hero (the boss's Secret prior): Druid, Paladin, Hunter, Mage and the run's own", run.MatchClasses(s).Contains("DRUID") && run.MatchClasses(s).Contains("PALADIN")
        && run.MatchClasses(s).Contains("HUNTER") && run.MatchClasses(s).Contains("MAGE"), string.Join(",", run.MatchClasses(s)));

    // A seed whose Finley entry is the third (Big Value, with BOT_543 Omega Mind).
    long omegaSeed = -1;
    for (long seed = 4400; seed < 4600 && omegaSeed < 0; seed++)
        if (run.BuildPlan(At("MAGE", seed, Tekahn), all).PlayerRelay[1].Power.Id == "ULDA_Finley_HP3") omegaSeed = seed;
    var fs = At("MAGE", omegaSeed, Tekahn);
    var fplan = run.BuildPlan(fs, all);
    fplan.BossDeck = Thirty(Yeti).Select(C).ToList();
    var g = Ready(fplan);
    Controller player = g.Player1, boss = g.Player2;
    int bossHealth = boss.Hero.Health;
    Hurt(g, player.Hero, 0);
    string eliseWhy = $"{Hero(player)} want {fplan.PlayerRelay[0].Power.Id} hand {player.HandZone.Count} deck {player.DeckZone.Count} state {g.State} boss {bossHealth}->{boss.Hero.Health}";
    bool elise = player.Hero.Card.Id == "ULDA_Elise" && player.Hero.Health == 50 && player.Hero.HeroPower.Card.Id == fplan.PlayerRelay[0].Power.Id && player.HandZone.Count == EncounterRules.RelayDraw
        && player.DeckZone.Count == 30 - EncounterRules.RelayDraw && g.State != State.COMPLETE && boss.Hero.Health == bossHealth;
    Hurt(g, player.Hero, 0);
    bool finley = player.Hero.Card.Id == "ULDA_Finley" && player.Hero.HeroPower.Card.Id == "ULDA_Finley_HP3"
        && player.HandZone.Concat(player.DeckZone).Count(p => p.Card.Id == Omega) == 2;
    Check("L5 in the game the run hero's death brings Elise (50 Health, her power, a new hand of 4 from her finale deck; the boss keeps its Health), the next death Finley with his third deck (two BOT_543 in it)",
        omegaSeed > 0 && elise && finley, $"seed {omegaSeed} {Hero(player)} elise {eliseWhy}");
    ToPlayer(g);
    EmptyHand(player);
    var omega = ToHand(g, player, Omega);
    Mana(player, 10);
    player.Hero.Damage = 20;
    bool omegaPlayed = Play(g, omega);
    int healthBefore = player.Hero.Health;
    bool moon = Cast(g, Moonfire, boss.Hero);
    EndTurn(g);
    bool gone = player.AppliedEnchantments?.Any(e => e.Card.Id == "BOT_543e") != true;
    Check("L6 BOT_543 Omega Mind, played by the relay hero with 10 Mana Crystals, gives its spells Lifesteal this turn (Moonfire heals the hero 1; Mind Meld is gone when the turn ends)", omegaPlayed && moon
        && player.Hero.Health == healthBefore + 1 && gone, healthBefore + " -> " + player.Hero.Health);
    ToPlayer(g);
    Hurt(g, player.Hero, 0); Hurt(g, player.Hero, 0);
    bool renoIn = player.Hero.Card.Id == "ULDA_Reno";
    Hurt(g, player.Hero, 0);
    var end = DungeonRunSetup.Harvest(g);
    Check("L7 after the fourth explorer the next death ends the game; the harvest lists every hero the run player used, in order", renoIn && g.State == State.COMPLETE
        && end.HeroesUsed.Count == 5 && end.HeroesUsed.Skip(1).SequenceEqual(new[] { "ULDA_Elise", "ULDA_Finley", "ULDA_Brann", "ULDA_Reno" }), string.Join(">", end.HeroesUsed));
    run.BeginMatch(fs);
    run.RecordOutcome(fs, all, DungeonRunOutcome.Loss, null, end);
    Check("L8 the fight's end clears the run's relay state and records the heroes used; the run's own class, hero and deck are untouched", fs.HeroSwitch == null
        && fs.Matches.Last().HeroesUsed == string.Join(",", end.HeroesUsed) && fs.Class == "MAGE" && run.RunClass(fs).HeroCard == data.Class("MAGE").HeroCard, fs.Matches.Last().HeroesUsed ?? "-");
});

// ============================================================ G  ULDA_115 "Gnomebliterator" and the run state it carries
Guard("Gnomebliterator", () =>
{
    Check("G1 ULDA_115 is scripted as a Tombs active treasure on its own id (Cost 2, the client's name and text)", C("ULDA_115").Implemented && C("ULDA_115").Cost == 2
        && DungeonRunContent.TryGet("ULDA_115", out var content) && content.ShellId == "ULDA_115" && content.Kind == RunContentKind.ActiveTreasure);
    var g = Fight("dala_21_whirt");
    Controller boss = g.Player2, player = g.Player1;
    var minion = Board(g, boss, Yeti);
    int health = boss.Hero.Health;
    bool one = Cast(g, "ULDA_115", minion);
    Board(g, player, "CS2_142");
    bool two = Cast(g, "ULDA_115", boss.Hero);
    var end = DungeonRunSetup.Harvest(g);
    Check("G2 it deals 10 damage (Spell Damage applies: 11 with Kobold Geomancer) and each cast is counted for the run (the harvest reports 2)", one && minion.Zone.Type != Zone.PLAY
        && two && boss.Hero.Health == health - 11 && player[EncounterRules.PlagueCount] == 2 && end.Gnomebliterators == 2, $"{health} -> {boss.Hero.Health}, casts {end.Gnomebliterators}");
    var s = At("MAGE", 4420, "dala_21_whirt");
    run.BeginMatch(s);
    run.RecordOutcome(s, all, DungeonRunOutcome.Win, null, end);
    Check("G3 the casts of a fight against another boss bank 10 damage each for the run's next Plague Lord (DungeonRunState.PlagueLordDamage)", s.PlagueLordDamage == 20, s.PlagueLordDamage.ToString());
    var lord = At("MAGE", 4421, Xatma);
    lord.PlagueLordDamage = 20;
    var plan = run.BuildPlan(lord, all);
    var lord2 = At("MAGE", 4421, Xatma);
    lord2.PlagueLordDamage = 30;
    var plan2 = run.BuildPlan(lord2, Banked(Xatma, 225));
    Check("G4 the Plague Lord fight starts with that damage (20 of 300), added to the banked Health (225 - 30 = 195: phase 2)", plan.BossCarriedDamage == 20 && plan.BossFormStart == 0
        && plan2.BossFormStart == 1 && plan2.BossHealth == 200 && plan2.BossCarriedDamage == 5, $"{plan.BossCarriedDamage} {plan2.BossFormStart}/{plan2.BossCarriedDamage}");
    var ws = At("MAGE", 4422, "dala_21_whirt");
    ws.PlagueLordDamage = 20;
    var wplan = run.BuildPlan(ws, all);
    Check("G5 a boss without persistent damage carries none of it", wplan.BossCarriedDamage == 0);
    run.BeginMatch(lord);
    run.RecordOutcome(lord, all, DungeonRunOutcome.Loss, null, new RunMatchEnd(250, 0, 0, 30, new[] { "HERO_08" }, false, 1));
    Check("G6 the Plague Lord fight spends it (a cast inside that fight banks nothing more)", lord.PlagueLordDamage == 0, lord.PlagueLordDamage.ToString());
    var save = new DungeonRunSave { Profile = all, Active = At("MAGE", 4423, Xatma) };
    string plain = Xml(save);
    save.Active.PlagueLordDamage = 30;
    string withField = Xml(save);
    var back = (DungeonRunSave)xml.Deserialize(new StringReader(withField));
    Check("G7 the save field is append-only: written only when set (the run's body is otherwise unchanged) and read back", !plain.Contains("PlagueLordDamage")
        && withField.Contains("PlagueLordDamage=\"30\"") && back.Active.PlagueLordDamage == 30 && withField.Replace(" PlagueLordDamage=\"30\"", "") == plain);
    var t = data.Treasure("ULDA_115");
    var v3 = At("MAGE_ROGUE", 4424, Xatma);
    var v2 = run.NewRun(all, "MAGE_ROGUE", data.Class("MAGE_ROGUE").Decks[0], data.Class("MAGE_ROGUE").Powers[0], 4424, schedule: "acts-v2",
        signatureId: run.SignatureOffer(all, data.Class("MAGE_ROGUE"), 4424).FirstOrDefault());
    Check("G8 the treasure is offered to an explorer's act run on an encounter schedule only (runs \"encounters\"): acts-v3h yes, acts-v2 no; it joins the Tombs treasure pool only there",
        t.Active && t.Runs == "encounters" && DungeonRun.TreasureOfferedOn(t, v3) && !DungeonRun.TreasureOfferedOn(t, v2) && run.TreasurePool(v3).Contains("ULDA_115")
        && !run.TreasurePool(v2).Contains("ULDA_115"), v2.ScheduleVersion + " " + v3.ScheduleVersion);
});

// ============================================================ A  admission
Guard("Admission", () =>
{
    bool Admits(string id, AdmitScope scope) => DungeonRunAdmission.Classify(C(id), id, scope).Admitted;
    var powers = new[] { "ULDA_BOSS_37p1", "ULDA_BOSS_37p2", "ULDA_BOSS_37p3", "ULDA_BOSS_38p1", "ULDA_BOSS_38p2", "ULDA_BOSS_38p3", "ULDA_BOSS_39p1", "ULDA_BOSS_39p2",
        "ULDA_BOSS_39p3", "ULDA_BOSS_67p1", "ULDA_BOSS_67p2", "ULDA_BOSS_67p3" };
    var cards = new[] { "ULDA_BOSS_37t", "ULDA_BOSS_39m", "ULDA_031", "ULDA_032", "ULDA_033", "ULDA_BOSS_67t", "LOEA10_5", "LOEA10_5H", "KARA_13_12H", "LOEA16_24H" };
    Check("A1 the twelve powers are admitted for a boss only", powers.All(id => Admits(id, AdmitScope.Boss) && !Admits(id, AdmitScope.Player) && DungeonRunEncounters.IsScripted(id)),
        string.Join(",", powers.Where(id => !Admits(id, AdmitScope.Boss) || Admits(id, AdmitScope.Player))));
    Check("A2 the boss cards are admitted for both sides and never generated", cards.All(id => Admits(id, AdmitScope.Boss) && Admits(id, AdmitScope.Player)
        && DungeonRunAdmission.RuleFor(id)?.NeverGenerated == true && DungeonRunEncounters.IsScripted(id)));
    Check("A3 ULDA_115 is the run player's only (family run-treasure-plague-lord)", Admits("ULDA_115", AdmitScope.Player) && !Admits("ULDA_115", AdmitScope.Boss)
        && DungeonRunAdmission.RuleFor("ULDA_115")?.Family == "run-treasure-plague-lord");
    Check("A4 the passive powers are passive in the engine and need activation; the clickable ones are not",
        new[] { "ULDA_BOSS_37p2", "ULDA_BOSS_38p3", "ULDA_BOSS_39p1", "ULDA_BOSS_67p3" }.All(id => DungeonRunEncounters.Find(id).Kind == EncounterCardKind.Passive && DungeonRunEncounters.NeedsActivation(id))
        && DungeonRunEncounters.Find("ULDA_BOSS_39p3").NeedsActivation && DungeonRunEncounters.Find("ULDA_BOSS_38p2").Kind == EncounterCardKind.Power);
});

Console.WriteLine($"SearchTelemetry swallowed leaf exceptions {SearchTelemetry.SwallowedLeafErrors}");
Console.WriteLine($"RESULT {passes} passed, {failures + SearchTelemetry.SwallowedLeafErrors} failed");
return failures + SearchTelemetry.SwallowedLeafErrors;
