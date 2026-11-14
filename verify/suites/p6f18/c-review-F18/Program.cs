// c-review-F18: regression games for the phase 6 review findings (fix round F18). Every check plays the card in a real engine
// game built through the run setup path (DungeonRun.NewRun -> BuildPlan -> DungeonRunSetup.Configure -> Game -> ApplyAfterStart
// -> mulligan -> MainBegin), or drives the real boss AI (DungeonRunBossPilot + GoldenPilot), or reads the regenerated ledger.
//   R1 LOOTA_832 Cloak of Invisibility: permanent Stealth for every friendly minion (summoned, taken by control, transformed,
//      moved by Scroll of Confusion), kept through an attack and an AOE Silence, scoped to its owner.
//   R2 ULDA_303 Starseeker's Tools / ULDA_301 Map of Uldum on a dual-class Tombs row Discover cards of both row classes.
//   R3 LOOTA_842a/b: two Quel'Delar pieces dealt in the opening hand stay two opening cards; they combine at turn start.
//   R5 DeckPlanEvaluator.PublicIncomingDamage with the active enemy's remaining mana below 0 (Overload locked past its
//      crystals): no OverflowException, and the boss AI decides that state without a fallback.
//   R6 LOE_007t Cursed! (DALA_745 gives the boss two): the boss AI plays them instead of holding them.
//   R7/R8 the restoration ledger: ULDA_039 and Elise's Choose One options are reached; the Monster Hunt rows' extra powers
//      are recorded adaptations.
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
var data = DungeonRunData.Parse(R("dungeon-run/run-v1.json"), R("dungeon-run/pool-v1.json"), R("npc/decks-v1.json"),
    R("dungeon-run/catalog-v1.json"), R("dungeon-run/schedule-v1.json"), R("dungeon-run/tavern-v1.json"));
var run = new DungeonRun(data);
var all = new DungeonRunProfile(); all.Unlocked.AddRange(data.Unlocks.Keys);
Card C(string id) => Cards.FromId(id);

DungeonRunState NewRun(string row, long seed, string[] actives, string[] passives, string power, int bosses)
{
    var cls = data.Class(row);
    var s = run.NewRun(all, cls.Id, cls.DefaultDeck, power ?? cls.DefaultPower, seed, signatureId: run.SignatureOffer(all, data.FindClass(cls.Id), seed).FirstOrDefault());
    foreach (var id in actives ?? Array.Empty<string>())
        if (!s.ActiveTreasures.Contains(id)) { s.ActiveTreasures.Add(id); DungeonRunDeck.Add(s, DungeonRunContent.Get(id).ShellId, DungeonRunDeck.OriginTreasure); }
    foreach (var id in passives ?? Array.Empty<string>()) s.Treasures.Add(id);
    for (int i = 0; i < bosses; i++) s.Matches.Add(new DungeonRunMatchSummary { Floor = 1, Boss = s.BossSequence[0], Outcome = DungeonRunOutcome.Win });
    return s;
}
Game Start(RunMatchPlan plan, int seed, bool history = true)
{
    var cfg = new GameConfig { RandomSeed = seed, Shuffle = true, SkipMulligan = false, History = history, Logging = false, FormatType = FormatType.FT_WILD };
    DungeonRunSetup.Configure(cfg, plan);
    var g = new Game(cfg);
    g.StartGame();
    DungeonRunSetup.ApplyAfterStart(g, plan);
    return g;
}
(DungeonRunState s, RunMatchPlan plan, Game g) Fight(string row, long seed, string[] actives = null, string[] passives = null, string power = null, int bosses = 0)
{
    var s = NewRun(row, seed, actives, passives, power, bosses);
    var plan = run.BuildPlan(s);
    var g = Start(plan, run.MatchSeed(s));
    g.Process(ChooseTask.Mulligan(g.Player1, new List<int>()));
    g.Process(ChooseTask.Mulligan(g.Player2, new List<int>()));
    g.MainBegin();
    if (g.CurrentPlayer != g.Player1) throw new Exception("run player not first");
    return (s, plan, g);
}
IPlayable Draw(Game g, string id)
{
    var p = g.Player1;
    var e = p.HandZone.FirstOrDefault(x => x.Card.Id == id);
    if (e != null) return e;
    e = p.DeckZone.FirstOrDefault(x => x.Card.Id == id) ?? throw new Exception(id + " is not in the run deck");
    Generic.Draw(p, e);
    g.DeathProcessingAndAuraUpdate();
    return e;
}
void Mana(Controller c, int n) { c.BaseMana = n; c.UsedMana = 0; c.OverloadLocked = 0; c.OverloadOwed = 0; }
IPlayable ToHand(Game g, Controller c, string id) { var e = Entity.FromCard(c, C(id)); Generic.AddHandPhase.Invoke(c, e); g.DeathProcessingAndAuraUpdate(); return e; }
Minion Board(Game g, Controller c, string id) { var m = (Minion)Entity.FromCard(c, C(id)); Generic.SummonBlock(g, m, -1, c.Hero); g.DeathProcessingAndAuraUpdate(); return m; }
bool Play(Game g, IPlayable card, ICharacter target = null) => g.Process(PlayCardTask.Any(g.CurrentPlayer, card, target, -1, 0));
void EndTurn(Game g) => g.Process(EndTurnTask.Any(g.CurrentPlayer));
void Clear(Game g) { foreach (var c in new[] { g.Player1, g.Player2 }) foreach (var m in c.BoardZone.GetAll().ToArray()) { c.BoardZone.Remove(m); c.SetasideZone.Add(m); } g.DeathProcessingAndAuraUpdate(); }
bool Stealthed(Minion m) => m.HasStealth && m[GameTag.STEALTH] > 0;
const string Cloak = "LOOTA_832";

// ================================================================ R1 LOOTA_832 Cloak of Invisibility
{
    var (s, plan, g) = Fight("MAGE", 101, actives: new[] { "LOOTA_805" }, passives: new[] { Cloak });
    Mana(g.Player1, 10); Clear(g);
    var mine = (Minion)ToHand(g, g.Player1, "CS2_182"); Play(g, mine);
    var theirs = Board(g, g.Player2, "CS2_120");
    Check("R1 a minion the owner summons has Stealth (the HasStealth flag and the STEALTH tag the client is sent)", Stealthed(mine), $"{mine.HasStealth}/{mine[GameTag.STEALTH]}");
    Check("R1 an enemy minion has no Stealth from the owner's Cloak", !theirs.HasStealth && theirs[GameTag.STEALTH] == 0);
    var amulet = Draw(g, "LOOTA_805");
    Play(g, amulet, theirs);
    g.DeathProcessingAndAuraUpdate();
    Check("R1 a minion taken by Amulet of Domination (LOOTA_805) has Stealth", theirs.Controller == g.Player1 && Stealthed(theirs),
        $"controller p{theirs.Controller.PlayerId} stealth {theirs.HasStealth}");
    EndTurn(g); Mana(g.Player2, 10);
    var fireball = ToHand(g, g.Player2, "CS2_029");
    var targets = g.Player2.Options().OfType<PlayCardTask>().Where(o => o.Source == fireball).Select(o => o.Target).ToList();
    Check("R1 the boss cannot target a Cloak-stealthed minion (Fireball's options name only heroes)",
        targets.Count > 0 && !targets.Contains(mine) && !targets.Contains(theirs) && targets.Contains(g.Player1.Hero),
        string.Join(",", targets.Select(t => t?.Card.Id ?? "-")));
    var dispel = ToHand(g, g.Player2, "EX1_626");
    Play(g, dispel);
    Check("R1 the boss's Mass Dispel (EX1_626) silences the minions, and they keep the Cloak's Stealth", mine.IsSilenced && Stealthed(mine) && Stealthed(theirs),
        $"silenced {mine.IsSilenced}; stealth {mine.HasStealth}/{theirs.HasStealth}");
    EndTurn(g);
    Check("R1 Stealth still holds at the owner's next turn", Stealthed(mine) && Stealthed(theirs));
    int hp = g.Player2.Hero.Health;
    bool attacked = g.Process(MinionAttackTask.Any(g.Player1, mine, g.Player2.Hero));
    Check("R1 a Cloak-stealthed minion that attacks keeps Stealth (permanent)", attacked && g.Player2.Hero.Health < hp && Stealthed(mine),
        $"attacked {attacked}; hero {hp}->{g.Player2.Hero.Health}; stealth {mine.HasStealth}");
    // Control taken away: the Cloak is its owner's, so a friendly minion the boss takes loses it.
    EndTurn(g); Mana(g.Player2, 10);
    var mc = ToHand(g, g.Player2, "CS1_113");
    var mcTargets = g.Player2.Options().OfType<PlayCardTask>().Where(o => o.Source == mc).Select(o => o.Target).ToList();
    Check("R1 Mind Control cannot target a Cloak-stealthed minion", !mcTargets.Contains(mine) && !mcTargets.Contains(theirs), string.Join(",", mcTargets.Select(t => t?.Card.Id ?? "-")));
    var spare = Board(g, g.Player1, "CS2_172");
    Check("R1 a minion that arrives mid-turn on the owner's side (a summon outside a card play) has Stealth", Stealthed(spare));
}
{
    // Transform: Vessina's Evolution (DALA_Shaman_HP1) turns a stealthed minion into one that costs (1) more.
    var (s, plan, g) = Fight("DALA_Vessina", 401, passives: new[] { Cloak }, power: "DALA_Shaman_HP1");
    Mana(g.Player1, 10); Clear(g);
    var y = (Minion)ToHand(g, g.Player1, "CS2_120"); Play(g, y);
    bool st0 = Stealthed(y);
    bool used = g.Process(HeroPowerTask.Any(g.Player1, y));
    var m = g.Player1.BoardZone.GetAll().FirstOrDefault();
    Check("R1 a minion transformed by Evolution (DALA_Shaman_HP1) keeps the Cloak's Stealth", st0 && used && m != null && m.Card.Id != "CS2_120" && Stealthed(m),
        $"before {st0}; used {used}; after {m?.Card.Id} stealth {m?.HasStealth}");
}
{
    // Scroll of Confusion (LOOTA_839): each minion goes to a random side; those that come to the owner are stealthed.
    var (s, plan, g) = Fight("MAGE", 402, passives: new[] { Cloak }, actives: new[] { "LOOTA_839" });
    Mana(g.Player1, 10); Clear(g);
    var mine = (Minion)ToHand(g, g.Player1, "CS2_182"); Play(g, mine);
    for (int i = 0; i < 6; i++) Board(g, g.Player2, "CS2_120");
    var scroll = Draw(g, "LOOTA_839"); Play(g, scroll);
    var moved = g.Player1.BoardZone.GetAll().Where(x => x.Card.Id == "CS2_120").ToList();
    var stayed = g.Player2.BoardZone.GetAll().ToList();
    Check("R1 enemy minions Scroll of Confusion (LOOTA_839) moves to the owner's side are stealthed; those on the boss's side are not",
        moved.Count > 0 && moved.All(Stealthed) && stayed.All(x => !x.HasStealth),
        $"moved {moved.Count}, stealthed {moved.Count(x => x.HasStealth)}; boss side {stayed.Count}, stealthed {stayed.Count(x => x.HasStealth)}");
}
{
    var (s, plan, g) = Fight("MAGE", 403);
    Mana(g.Player1, 10); Clear(g);
    var y = (Minion)ToHand(g, g.Player1, "CS2_182"); Play(g, y);
    Check("R1 without the Cloak a Yeti has no Stealth", !y.HasStealth && y[GameTag.STEALTH] == 0);
}

// ================================================================ R2 dual-class explorer Discovers
{
    var (s, plan, g) = Fight("DRUID_PRIEST", 206, actives: new[] { "ULDA_303" });
    var t = Draw(g, "ULDA_303");
    var spells = DungeonRunContent.ClassDiscoverPool(g, g.Player1, t, CardType.SPELL);
    var minions = DungeonRunContent.ClassDiscoverPool(g, g.Player1, t, CardType.MINION);
    Check("R2 Elise (DRUID_PRIEST) ULDA_303: the spell Discover pool holds Druid and Priest cards, and nothing else but neutral",
        spells.Any(c => c.Class == CardClass.PRIEST) && spells.Any(c => c.Class == CardClass.DRUID) && spells.All(c => c.Class == CardClass.DRUID || c.Class == CardClass.PRIEST || c.Class == CardClass.NEUTRAL),
        $"druid {spells.Count(c => c.Class == CardClass.DRUID)}, priest {spells.Count(c => c.Class == CardClass.PRIEST)}, neutral {spells.Count(c => c.Class == CardClass.NEUTRAL)}; hero class {g.Player1.HeroClass}");
    Check("R2 Elise ULDA_303: the minion Discover pool holds Druid, Priest and neutral minions",
        minions.Any(c => c.Class == CardClass.PRIEST) && minions.Any(c => c.Class == CardClass.DRUID) && minions.Any(c => c.Class == CardClass.NEUTRAL));
    // Play it for real over seeds: the offered cards are of the pair or neutral, and a Priest card is offered.
    var offered = new List<Card>(); int plays = 0;
    for (long seed = 206; seed < 226; seed++)
    {
        var (s2, plan2, g2) = Fight("DRUID_PRIEST", seed, actives: new[] { "ULDA_303" });
        Mana(g2.Player1, 10);
        var tools = Draw(g2, "ULDA_303");
        if (!Play(g2, tools)) continue;
        plays++;
        for (int k = 0; k < 2 && g2.Player1.Choice != null; k++)
        {
            offered.AddRange(g2.Player1.Choice.Choices.Select(id => g2.IdEntityDic[id].Card));
            g2.Process(ChooseTask.Pick(g2.Player1, g2.Player1.Choice.Choices[0]));
        }
    }
    Check("R2 Elise playing ULDA_303 (20 games): every offered card is Druid, Priest or neutral, and Priest cards are offered",
        plays == 20 && offered.Count > 0 && offered.All(c => c.Class == CardClass.DRUID || c.Class == CardClass.PRIEST || c.Class == CardClass.NEUTRAL)
        && offered.Any(c => c.Class == CardClass.PRIEST) && offered.Any(c => c.Class == CardClass.DRUID),
        $"plays {plays}; offered {offered.Count}: druid {offered.Count(c => c.Class == CardClass.DRUID)}, priest {offered.Count(c => c.Class == CardClass.PRIEST)}, neutral {offered.Count(c => c.Class == CardClass.NEUTRAL)}");
}
{
    var offered = new List<Card>(); int plays = 0;
    for (long seed = 300; seed < 320; seed++)
    {
        var (s, plan, g) = Fight("MAGE_ROGUE", seed, actives: new[] { "ULDA_301" }, bosses: 1);
        Mana(g.Player1, 10);
        var map = Draw(g, "ULDA_301");
        if (seed == 300)
        {
            var pool = DungeonRunContent.ClassDiscoverPool(g, g.Player1, map, CardType.MINION).Concat(DungeonRunContent.ClassDiscoverPool(g, g.Player1, map, CardType.SPELL))
                .Concat(DungeonRunContent.ClassDiscoverPool(g, g.Player1, map, CardType.WEAPON)).ToList();
            Check("R2 Reno (MAGE_ROGUE) ULDA_301 Map of Uldum: the Discover pool holds Mage, Rogue and neutral cards and no other class",
                pool.Any(c => c.Class == CardClass.ROGUE) && pool.Any(c => c.Class == CardClass.MAGE) && pool.Any(c => c.Class == CardClass.NEUTRAL)
                && pool.All(c => c.Class == CardClass.MAGE || c.Class == CardClass.ROGUE || c.Class == CardClass.NEUTRAL),
                $"mage {pool.Count(c => c.Class == CardClass.MAGE)}, rogue {pool.Count(c => c.Class == CardClass.ROGUE)}, neutral {pool.Count(c => c.Class == CardClass.NEUTRAL)}; weapons rogue {pool.Count(c => c.Type == CardType.WEAPON && c.Class == CardClass.ROGUE)}");
        }
        if (!Play(g, map)) continue;
        plays++;
        for (int k = 0; k < 3 && g.Player1.Choice != null; k++)
        {
            offered.AddRange(g.Player1.Choice.Choices.Select(id => g.IdEntityDic[id].Card));
            g.Process(ChooseTask.Pick(g.Player1, g.Player1.Choice.Choices[0]));
        }
    }
    Check("R2 Reno playing ULDA_301 after one boss (20 games): every offered card is Mage, Rogue or neutral, and Rogue cards are offered",
        plays == 20 && offered.Count >= 60 && offered.All(c => c.Class == CardClass.MAGE || c.Class == CardClass.ROGUE || c.Class == CardClass.NEUTRAL) && offered.Any(c => c.Class == CardClass.ROGUE),
        $"plays {plays}; offered {offered.Count}: mage {offered.Count(c => c.Class == CardClass.MAGE)}, rogue {offered.Count(c => c.Class == CardClass.ROGUE)}, neutral {offered.Count(c => c.Class == CardClass.NEUTRAL)}");
}
{
    var (s, plan, g) = Fight("MAGE", 330, actives: new[] { "ULDA_301" }, bosses: 1);
    var map = Draw(g, "ULDA_301");
    var pool = DungeonRunContent.ClassDiscoverPool(g, g.Player1, map, CardType.SPELL);
    Check("R2 a single-class row (MAGE) still discovers only its class and neutral", pool.Count > 0 && pool.All(c => c.Class == CardClass.MAGE || c.Class == CardClass.NEUTRAL));
    var boss = DungeonRunContent.ClassDiscoverPool(g, g.Player2, map, CardType.SPELL);
    Check("R2 the boss side is not given the run row's classes", boss.All(c => c.Class == g.Player2.HeroClass || c.Class == CardClass.NEUTRAL), $"boss class {g.Player2.HeroClass}");
}

// ================================================================ R3 Quel'Delar pieces in the opening hand
{
    int bothSeeds = 0; var problems = new List<string>(); int mulliganedSeeds = 0;
    for (long seed = 1; seed <= 120; seed++)
    {
        var s = NewRun("MAGE", seed, new[] { DungeonRunContent.QuelDelarBlade, DungeonRunContent.QuelDelarHilt }, null, null, 0);
        var plan = run.BuildPlan(s);
        var g = Start(plan, run.MatchSeed(s));
        var hand = g.Player1.HandZone.Select(p => p.Card.Id).ToList();
        bool blade = hand.Contains(DungeonRunContent.QuelDelarBlade), hilt = hand.Contains(DungeonRunContent.QuelDelarHilt);
        if (hand.Contains(DungeonRunContent.QuelDelar)) problems.Add($"{seed}: Quel'Delar formed in the opening deal [{string.Join(",", hand)}]");
        if (!(blade && hilt)) continue;
        bothSeeds++;
        var choices = g.Player1.Choice?.Choices.ToList() ?? new List<int>();
        if (hand.Count != 3 || choices.Count != 3) problems.Add($"{seed}: opening hand {hand.Count} cards, {choices.Count} mulligan choices [{string.Join(",", hand)}]");
        // Even seeds keep the hand; odd seeds mulligan both pieces away.
        bool keep = seed % 2 == 0;
        var pieces = g.Player1.HandZone.Where(p => p.Card.Id == DungeonRunContent.QuelDelarBlade || p.Card.Id == DungeonRunContent.QuelDelarHilt).Select(p => p.Id).ToList();
        g.Process(ChooseTask.Mulligan(g.Player1, keep ? choices : choices.Where(id => !pieces.Contains(id)).ToList()));
        var afterMulligan = g.Player1.HandZone.Select(p => p.Card.Id).ToList();
        if (afterMulligan.Count != 3) problems.Add($"{seed}: {afterMulligan.Count} cards after the mulligan");
        if (afterMulligan.Contains(DungeonRunContent.QuelDelar)) problems.Add($"{seed}: combined during the mulligan");
        g.Process(ChooseTask.Mulligan(g.Player2, new List<int>()));
        g.MainBegin();
        var turn1 = g.Player1.HandZone.Select(p => p.Card.Id).ToList();
        bool hasQ = turn1.Contains(DungeonRunContent.QuelDelar), hasPiece = turn1.Contains(DungeonRunContent.QuelDelarBlade) || turn1.Contains(DungeonRunContent.QuelDelarHilt);
        bool deckQ = g.Player1.DeckZone.Any(p => p.Card.Id == DungeonRunContent.QuelDelar);
        if (keep && (!hasQ || hasPiece || turn1.Count != 3)) problems.Add($"{seed} kept: turn 1 hand [{string.Join(",", turn1)}]");
        if (!keep)
        {
            mulliganedSeeds++;
            // Each piece is back in the deck, or drawn again (a mulligan replacement or the turn's draw) as itself.
            int Pieces(string id) => g.Player1.DeckZone.Count(p => p.Card.Id == id) + g.Player1.HandZone.Count(p => p.Card.Id == id);
            bool piecesKept = hasQ ? Pieces(DungeonRunContent.QuelDelarBlade) + Pieces(DungeonRunContent.QuelDelarHilt) == 0
                : Pieces(DungeonRunContent.QuelDelarBlade) == 1 && Pieces(DungeonRunContent.QuelDelarHilt) == 1;
            if (deckQ || !piecesKept) problems.Add($"{seed} mulliganed: deck has Quel'Delar {deckQ}, pieces kept {piecesKept}, turn 1 [{string.Join(",", turn1)}]");
        }
    }
    Check("R3 MAGE act runs holding both Quel'Delar pieces (seeds 1-120): the opening deal never combines them; a dealt pair is 3 opening cards and 3 mulligan choices; kept, they combine at turn 1 (3 cards: 2 kept + Quel'Delar + the turn's draw - the merge); mulliganed, they go back to the deck as pieces (none lost, no Quel'Delar in the deck)",
        bothSeeds >= 2 && mulliganedSeeds >= 1 && problems.Count == 0, $"seeds with both dealt {bothSeeds} (mulliganed {mulliganedSeeds}); {string.Join(" | ", problems.Take(6))}");
    // Mid-game: drawing the second piece still combines at once (the ZONE trigger outside the opening deal).
    var (s3, plan3, g3) = Fight("MAGE", 117, actives: new[] { DungeonRunContent.QuelDelarBlade, DungeonRunContent.QuelDelarHilt });
    foreach (var id in new[] { DungeonRunContent.QuelDelarBlade, DungeonRunContent.QuelDelarHilt })
    {
        var e = g3.Player1.DeckZone.FirstOrDefault(x => x.Card.Id == id);
        if (e != null) { Generic.Draw(g3.Player1, e); g3.DeathProcessingAndAuraUpdate(); }
    }
    var hand3 = g3.Player1.HandZone.Select(p => p.Card.Id).ToList();
    Check("R3 mid-game, the second piece drawn combines with the first at once", hand3.Contains(DungeonRunContent.QuelDelar) && !hand3.Contains(DungeonRunContent.QuelDelarBlade) && !hand3.Contains(DungeonRunContent.QuelDelarHilt),
        string.Join(",", hand3));
}

// ================================================================ boss AI helpers (R5, R6)
var testBudget = new Budget(6000, 2000, 300, 6, 3, 100, 8, turnSearchSeconds: 6, turnWallSeconds: 8);
(DungeonRunBossPilot pilot, GoldenPilot golden, NpcProfile profile) Pilot(DungeonRunState s, RunMatchPlan plan)
{
    var pilot = DungeonRunBossPilot.Build(run.CurrentBoss(s), plan, data, runClasses: run.RunClasses(s));
    var profile = pilot.Profile.WithBudget(testBudget);
    return (pilot, new GoldenPilot(profile, pilot.Plan, null, pilot.SecretPrior), profile);
}

// ================================================================ R5 negative remaining mana in the incoming-damage bound
{
    var (s, plan, g) = Fight("SHAMAN", 520);
    var (pilot, golden, profile) = Pilot(s, plan);
    // The run player's own turn with Overload locked past its crystals: 4 crystals, 6 locked, remaining mana -2.
    g.Player1.BaseMana = 4; g.Player1.UsedMana = 0; g.Player1.OverloadLocked = 6; g.Player1.OverloadOwed = 0;
    int remaining = g.Player1.RemainingMana;
    string error = null; double damage = -1;
    try { damage = new DeckPlanEvaluator(pilot.Plan).PublicIncomingDamage(new FairFacts(g, g.Player2, 0, new Dictionary<string, int>())); }
    catch (Exception e) { error = e.GetType().Name + ": " + e.Message; }
    Check("R5 PublicIncomingDamage with the active enemy at -2 remaining mana returns a bound (before F17: OverflowException from new double[-1])",
        remaining == -2 && error == null && damage >= 0, $"remaining {remaining}; damage {damage}; {error}");
    // The boss decides its turn when the player's next turn will start with 6 Overload owed on 3 crystals: each line that ends
    // the boss's turn reaches a state whose active enemy has negative mana.
    EndTurn(g);
    Mana(g.Player2, 6);
    g.Player1.BaseMana = 3; g.Player1.OverloadOwed = 6;
    int decisionIndex = 0, fallbacks = 0, decisions = 0; var reasons = new List<string>();
    for (int guard = 0; guard < 30 && g.CurrentPlayer == g.Player2 && g.State != State.COMPLETE; guard++)
    {
        var (task, trace) = BotDriver.Decide(g, g.Player2.PlayerId, pilot.Deck.Style,
            st => golden.DecidePilot(st, new GoldenContext(g.Turn, decisionIndex, unchecked(997 * g.Turn + decisionIndex * 31), profile)),
            ref decisionIndex, golden.Options);
        decisions++;
        if (trace?.Kind == "fallback") { fallbacks++; reasons.Add(trace.FallbackReason); }
        if (!g.Process(task)) throw new Exception("engine rejected boss task " + task);
    }
    // Phase 7 review: Game.MainRessources now locks at most the player's crystals (Hearthstone drops the excess), so the
    // player's turn starts at 0 Mana (4 crystals, 4 locked), not below 0; the -2 state above is still read without an error.
    Check("R5 the boss AI decides that turn without a fallback, and the player's turn starts with every crystal locked and 0 Mana (never below 0)",
        decisions > 0 && fallbacks == 0 && g.CurrentPlayer == g.Player1 && g.Player1.RemainingMana == 0 && g.Player1.OverloadLocked == g.Player1.BaseMana,
        $"decisions {decisions}; fallbacks {fallbacks} {string.Join(";", reasons)}; player mana {g.Player1.RemainingMana}");
}

// ================================================================ R6 Cursed! in the boss's hand
Check("R6 the boss planner reads Cursed!'s hand self-damage (2) and no other's from a plain minion",
    DeckPlanEvaluator.HeldSelfDamage(DungeonRunContent.RafaamCurse) == 2 && DeckPlanEvaluator.HeldSelfDamage("CS2_182") == 0);
string CurseGame(long seed)
{
    var s = NewRun("DALA_Tekahn", seed, null, new[] { "DALA_745" }, null, 2);
    var plan = run.BuildPlan(s);
    var (pilot, golden, profile) = Pilot(s, plan);
    var game = Start(plan, run.MatchSeed(s), history: false);
    game.Process(ChooseTask.Mulligan(game.Player1, new List<int>()));
    var choice = game.Player2.Choice;
    var view = new MulliganView(choice.Choices.Select(x => new MulliganCard(x, game.IdEntityDic[x].Card.Id, game.IdEntityDic[x].Card.Cost)),
        false, pilot.Deck.CardIds, game.Player1.Hero.Card.Class);
    game.Process(ChooseTask.Mulligan(game.Player2, BotDriver.Mulligan(view, new DeckPlanMulligan(pilot.Plan)).ToList()));
    game.MainBegin();
    int decisionIndex = 0, fallbacks = 0, curses = 0, firstCurseBossTurn = -1, bossTurns = 0, lastTurn = -1, actions = 0, heldAtTwoMana = 0;
    while (game.State != State.COMPLETE && game.Turn <= 60)
    {
        PlayerTask task;
        if (game.CurrentPlayer == game.Player2)
        {
            if (game.Turn != lastTurn) { lastTurn = game.Turn; bossTurns++; }
            var (t, trace) = BotDriver.Decide(game, game.Player2.PlayerId, pilot.Deck.Style,
                st => golden.DecidePilot(st, new GoldenContext(game.Turn, decisionIndex, unchecked(997 * game.Turn + decisionIndex * 31), profile)),
                ref decisionIndex, golden.Options);
            if (trace?.Kind == "fallback") fallbacks++;
            if (t is PlayCardTask pc && pc.Source.Card.Id == DungeonRunContent.RafaamCurse) { curses++; if (firstCurseBossTurn < 0) firstCurseBossTurn = bossTurns; }
            if (t is EndTurnTask && game.Player2.RemainingMana >= 2 && game.Player2.HandZone.Any(e => e.Card.Id == DungeonRunContent.RafaamCurse)) heldAtTwoMana++;
            task = t;
        }
        else task = LocalBot.Choose(game.Player1.Options());
        if (++actions > 1500) throw new Exception("stuck");
        if (task is ChooseTask pick && game.Step != Step.BEGIN_MULLIGAN)
        {
            var decision = game.GetPendingDecisions().Single(d => d.ControllerId == task.Controller.Id);
            if (!game.TryAnswer(new SabberStoneCore.Model.Decisions.DecisionAnswer(decision.Id, decision.ControllerId, pick.Choices.ToArray()), out var why))
                throw new Exception("engine rejected a choice: " + why);
        }
        else if (!game.Process(task)) throw new Exception("engine rejected task " + task);
    }
    return $"curses {curses} first-boss-turn {firstCurseBossTurn} ended-turn-holding-a-curse-with-2-mana {heldAtTwoMana} fallbacks {fallbacks} boss-turns {bossTurns} boss {run.CurrentBoss(s).Id}";
}
foreach (long seed in new long[] { 901, 911, 912, 913 })
{
    string line;
    try { line = CurseGame(seed); } catch (Exception e) { line = "FAULT " + e.GetType().Name + ": " + e.Message.Split('\n')[0]; }
    var parts = line.Split(' ');
    int Val(string key) { int i = Array.IndexOf(parts, key); return i >= 0 && i + 1 < parts.Length && int.TryParse(parts[i + 1], out int v) ? v : -99; }
    int first = Val("first-boss-turn");
    Check($"R6 DALA_745 game {seed}: the boss AI plays its Cursed! cards (the first by its 4th turn), with no fallback",
        !line.StartsWith("FAULT") && Val("curses") >= 1 && first >= 1 && first <= 4 && Val("fallbacks") == 0, line);
}

// ================================================================ R7 / R8 the restoration ledger
{
    var csv = File.ReadAllLines(VerifyPaths.Repo("docs/design/restoration-ledger.csv"));
    List<string> Split(string line)
    {
        var cells = new List<string>(); var cur = new System.Text.StringBuilder(); bool q = false;
        for (int i = 0; i < line.Length; i++)
        {
            char ch = line[i];
            if (q) { if (ch == '"' && i + 1 < line.Length && line[i + 1] == '"') { cur.Append('"'); i++; } else if (ch == '"') q = false; else cur.Append(ch); }
            else if (ch == '"') q = true; else if (ch == ',') { cells.Add(cur.ToString()); cur.Clear(); } else cur.Append(ch);
        }
        cells.Add(cur.ToString());
        return cells;
    }
    var head = Split(csv[0]);
    int Col(string name) => head.IndexOf(name);
    var rows = csv.Skip(1).Select(Split).Where(r => r.Count == head.Count).ToDictionary(r => r[0], r => r);
    string Cell(string id, string col) => rows.TryGetValue(id, out var r) ? r[Col(col)] : "<missing>";
    Check("R7 ledger ULDA_039 Uldum Treasure Cache: implemented, offered as the power of the Tomb Divers, the stale note gone",
        Cell("ULDA_039", "classification") == "implemented" && Cell("ULDA_039", "player_availability").Contains("power of ULDA_016")
        && Cell("ULDA_039", "player_availability").Contains("power of ULDA_021") && !Cell("ULDA_039", "notes").Contains("no class or boss uses this power"),
        Cell("ULDA_039", "classification") + " / " + Cell("ULDA_039", "player_availability"));
    foreach (string id in new[] { "ULDA_Elise_HP1a", "ULDA_Elise_HP1b" })
        Check($"R7 ledger {id}: implemented, offered as a Choose One option of ULDA_Elise_HP1",
            Cell(id, "classification") == "implemented" && Cell(id, "player_availability") == "offered:option of ULDA_Elise_HP1",
            Cell(id, "classification") + " / " + Cell(id, "player_availability"));
    foreach (var (row, power) in new[] { ("GILA_500h3", "CS2_083b"), ("GILA_600h", "CS2_102"), ("GILA_400h", "DS1h_292"), ("GILA_900h", "CS2_034") })
        Check($"R8 ledger run:class:{row}: the power {power} the Witchwood did not give is a recorded adaptation (adapted_by, row and portrait)",
            Cell("run:class:" + row, "adapted_by").Contains("SoloStone adaptation: " + power) && Cell(row, "adapted_by").Contains("SoloStone adaptation: " + power),
            Cell("run:class:" + row, "adapted_by"));
}

Console.WriteLine($"c-review-F18: {passes} passed, {failures} failed");
return failures == 0 ? 0 : 1;
