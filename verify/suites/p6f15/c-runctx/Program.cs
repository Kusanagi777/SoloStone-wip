// c-runctx: phase 6 F0 run context and match results. RunContext on GameConfig (bosses defeated, slot logs, the treasure pool,
// the next-fight bonus; shared by clones), the RunHelpers producers (H7: run edits, slot logs, a power override, a next-fight
// bonus) filling Game.RunResult (copied, never shared, by a simulation clone), DungeonRun.ApplyMatchResult on a won match versus
// a lost one, and belief resets (H8) with the fair search's listener (FairBotState.ForgetResetHand). Every check runs a real
// engine game. Compiles the run layer and FairBotState (ENGINE_SOURCE) against p6f0/engine-out.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SabberStoneCore.CardSets.DungeonRun;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;

int failures = 0, passes = 0;
void Check(string name, bool ok, string detail = "")
{
    if (ok) passes++; else failures++;
    if (!ok || Environment.GetEnvironmentVariable("VERBOSE") == "1")
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
var fresh = new DungeonRunProfile();
Game StartPlan(RunMatchPlan plan, int seed)
{
    var cfg = new GameConfig { RandomSeed = seed, Shuffle = true, SkipMulligan = false, History = true, Logging = false, FormatType = FormatType.FT_WILD };
    DungeonRunSetup.Configure(cfg, plan);
    var g = new Game(cfg); g.StartGame(); DungeonRunSetup.ApplyAfterStart(g, plan);
    return g;
}
IPlayable Slot(Game g, int i) => g.IdEntityDic[g.Player1DeckSlotIds[i]];
void WinFight(DungeonRunState s, RunMatchResult result = null, DungeonRunOutcome outcome = DungeonRunOutcome.Win)
{
    run.BeginMatch(s); run.RecordOutcome(s, fresh, outcome, result);
}
// Moves an act run on to its next fight (drafts take the first offer, taverns are left, modifiers take the first).
void ToNextFight(DungeonRunState s)
{
    for (int guard = 0; guard < 50 && !s.Finished && s.Phase != DungeonRunPhase.PreMatch; guard++)
        switch (s.Phase)
        {
            case DungeonRunPhase.TreasureDraft: run.PickTreasure(s, s.OpenDraft.Offered[0]); break;
            case DungeonRunPhase.Modifier: run.PickModifier(s, s.ModifierOffer.Offered[0]); break;
            case DungeonRunPhase.Tavern: run.LeaveTavern(s); break;
            case DungeonRunPhase.BundleDraft:
                if (s.OpenDraft.Takes == 0) { run.PickBundle(s, s.OpenDraft.Offered[0]); break; }
                string b = s.OpenDraft.Choosing ?? s.OpenDraft.Offered[0];
                var left = run.OfferCards(s.OpenDraft, b); foreach (string k in s.OpenDraft.Chosen) left.Remove(k);
                run.PickDraftCard(s, b, left[0]); break;
        }
}
DungeonRunState Act(string cls = "MAGE", long seed = 2024) => run.NewRun(fresh, cls, data.Class(cls).DefaultDeck, data.Class(cls).Powers[0], seed, signatureId: run.SignatureOffer(fresh, run.Data.FindClass(cls), seed).FirstOrDefault());

// ---------------------------------------------------------------- R1 bosses defeated
{
    var legacy = run.NewRun(fresh, "MAGE", data.Class("MAGE").DefaultDeck, "CS2_034", 7, schedule: DungeonRunSchedules.LegacyId, signatureId: run.SignatureOffer(fresh, run.Data.FindClass("MAGE"), 7).FirstOrDefault());
    Check("R1 a new legacy run has defeated no boss", DungeonRun.BossesDefeated(legacy) == 0);
    WinFight(legacy); ToNextFight(legacy); WinFight(legacy); ToNextFight(legacy);
    Check("R1 a legacy run on floor 3 has defeated 2 (Floor - 1)", legacy.Floor == 3 && DungeonRun.BossesDefeated(legacy) == 2);
    var s = Act();
    int wins = 0;
    for (int i = 0; i < 6; i++) { WinFight(s); wins++; ToNextFight(s); }
    Check("R1 an act run counts its won fights, whatever taverns and modifiers it passed", DungeonRun.BossesDefeated(s) == wins && s.Matches.Count == wins);
    run.BeginMatch(s); run.RecordOutcome(s, fresh, DungeonRunOutcome.Fault);
    Check("R1 a fault is no defeated boss", DungeonRun.BossesDefeated(s) == wins);
    var plan = run.BuildPlan(s);
    var g = StartPlan(plan, 5);
    Check("R1 the real game reads it (RunHelpers.Bosses)", RunHelpers.Bosses(g) == wins && plan.RunContext.BossesDefeated == wins);
    var clone = g.Clone();
    Check("R1 a clone shares the same immutable RunContext", ReferenceEquals(clone.RunContextOrNull, g.RunContextOrNull) && RunHelpers.Bosses(clone) == wins);
    var plain = new Game(new GameConfig { StartPlayer = 1, Player1HeroClass = CardClass.MAGE, Player2HeroClass = CardClass.MAGE, FillDecks = true, Shuffle = false });
    Check("R1 outside a run match RunHelpers reads an empty run", RunHelpers.Bosses(plain) == 0 && RunHelpers.Context(plain) == RunContext.Empty && RunHelpers.NextFight(plain) == null);
}

// ---------------------------------------------------------------- R2 the treasure pool and run deck facts
{
    var s = Act();
    var pool = run.TreasurePool(s);
    var expected = data.Treasures.Where(t => t.Active && t.Offered && !t.Legacy && !t.Signature && t.Scope.Matches(run.RunClass(s).ClassSet, s.StarterDeck, s.Power))
        .Select(t => DungeonRunContent.Get(t.Id).ShellId).Distinct().OrderBy(x => x, StringComparer.Ordinal).ToList();
    Check("R2 the treasure pool is the offered, scoped, scripted active treasures as shell ids, ordinal", pool.SequenceEqual(expected) && pool.All(id => Cards.FromId(id).Implemented)
        && !pool.Contains(DungeonRunContent.Get("sig_field_atlas").ShellId), string.Join(",", pool));
    var hunter = Act("HUNTER");
    Check("R2 a scoped treasure joins only its scope's pool (hunting_net with Set Snare)", !pool.Contains(DungeonRunContent.Get("hunting_net").ShellId));
    var plan = run.BuildPlan(s);
    var g = StartPlan(plan, 8);
    var ctx = RunHelpers.Context(g);
    Check("R2 the game's context: the pool, the run deck ids and origins, one empty log per slot", ctx.TreasurePool.SequenceEqual(pool) && ctx.RunDeckIds.SequenceEqual(s.Deck)
        && ctx.RunDeckOrigins.All(o => o == DungeonRunDeck.OriginStarter) && ctx.SlotLogs.Count == s.Cards.Count && ctx.SlotLogs.All(l => l.Count == 0));
    Check("R2 every dealt run card knows its slot and origin", Enumerable.Range(0, plan.PlayerDeck.Count).All(i => RunHelpers.SlotOf(Slot(g, i)) == i && RunHelpers.SlotOrigin(Slot(g, i)) == DungeonRunDeck.OriginStarter));
    Check("R2 a boss card has no slot", g.Player2.DeckZone.Concat(g.Player2.HandZone).All(c => RunHelpers.SlotOf(c) == -1));
    var legacy = run.NewRun(fresh, "MAGE", data.Class("MAGE").DefaultDeck, "CS2_034", 7, schedule: DungeonRunSchedules.LegacyId, signatureId: run.SignatureOffer(fresh, run.Data.FindClass("MAGE"), 7).FirstOrDefault());
    var lctx = run.BuildPlan(legacy).RunContext;
    Check("R2 a legacy run's context has no slot logs or origins", lctx.SlotLogs.Count == 0 && lctx.RunDeckOrigins.Count == 0 && lctx.RunDeckIds.SequenceEqual(legacy.Deck));
}

// ---------------------------------------------------------------- R3 slot logs in the match
{
    var s = Act();
    s.Cards[2].Log.AddRange(new[] { "CS2_029", "CS2_024" });
    var g = StartPlan(run.BuildPlan(s), 9);
    Check("R3 a card's run log reaches its entity in the match (RunHelpers.SlotLog)", RunHelpers.SlotLog(Slot(g, 2)).SequenceEqual(new[] { "CS2_029", "CS2_024" })
        && RunHelpers.SlotLog(Slot(g, 1)).Count == 0);
}

// ---------------------------------------------------------------- R4 producers, clones and a won match
{
    var s = Act();
    string mageCard = s.Deck[0];
    int n0 = s.Cards.Count, u3 = s.Cards[3].Uid, u5 = s.Cards[5].Uid;
    var plan = run.BuildPlan(s);
    var g = StartPlan(plan, 10);
    Check("R4 a fresh match asks for nothing", g.RunResult == null);
    RunHelpers.EditAdd(g, mageCard);
    Check("R4 EditReplace/EditRemove/AppendSlotLog name a run slot", RunHelpers.EditReplace(Slot(g, 3), mageCard) && RunHelpers.EditRemove(Slot(g, 5)) && RunHelpers.AppendSlotLog(Slot(g, 1), "EX1_277"));
    Check("R4 a boss card names no slot: refused", !RunHelpers.EditRemove(g.Player2.DeckZone[0]) && !RunHelpers.AppendSlotLog(g.Player2.DeckZone[0], "x"));
    RunHelpers.OverridePower(g, "mage_rimebite");
    RunHelpers.GrantNextFight(g, 4, 2);
    var clone = g.Clone();
    RunHelpers.EditAdd(clone, mageCard);
    RunHelpers.OverridePower(clone, "carto_survey");
    Check("R4 a simulation clone copies the result and never writes into the real game's", clone.RunResult.Edits.Count == 4 && g.RunResult.Edits.Count == 3
        && g.RunResult.PowerOverride == "mage_rimebite" && clone.RunResult.PowerOverride == "carto_survey");
    var exact = g.CloneExact();
    Check("R4 an exact clone carries the same requests", exact.RunResult.Edits.Select(e => e.ToString()).SequenceEqual(g.RunResult.Edits.Select(e => e.ToString())));
    Check("R4 the requests in order", string.Join(" ", g.RunResult.Edits) == $"add:{mageCard} replace@3:{mageCard} remove@5" && g.RunResult.SlotLogAppends.Single().Key == 1
        && g.RunResult.NextFight.Mana == 4 && g.RunResult.NextFight.Draws == 2, string.Join(" ", g.RunResult.Edits));
    WinFight(s, g.RunResult);
    Check("R4 a won match applies the edits by plan slot: slot 3 replaced in place (origin match), slot 5 removed, a card added (origin match)",
        s.Cards.Count == n0 && DungeonRunDeck.Find(s, u3).Id == mageCard && DungeonRunDeck.Find(s, u3).Origin == DungeonRunDeck.OriginMatch && DungeonRunDeck.Find(s, u5) == null
        && s.Cards.Last().Id == mageCard && s.Cards.Last().Origin == DungeonRunDeck.OriginMatch && DungeonRunDeck.MirrorError(s) == null);
    Check("R4 the slot log line is kept on its card", s.Cards[1].Log.SequenceEqual(new[] { "EX1_277" }));
    Check("R4 the power override and the next-fight bonus are kept", s.PowerOverride == "mage_rimebite" && s.NextFight?.Mana == 4 && s.NextFight.Draws == 2 && s.Power == "CS2_034");
    ToNextFight(s);
    var next = run.BuildPlan(s);
    Check("R4 the next plan plays the override and carries the bonus", next.PlayerHeroPowerId == "mage_rimebite" && next.NextFight.Mana == 4 && next.NextFight.Draws == 2
        && next.RunContext.NextFight.Mana == 4);
    // The bonus in a real game, against the same plan without it.
    var withBonus = StartPlan(next, 77);
    var control = run.BuildPlan(s); control.NextFight = null;
    var without = StartPlan(control, 77);
    Check("R4 the next fight starts with 4 more Mana Crystals and 2 more opening cards (real game)",
        withBonus.Player1.BaseMana == without.Player1.BaseMana + 4 && withBonus.Player1.HandZone.Count == without.Player1.HandZone.Count + 2,
        $"mana {withBonus.Player1.BaseMana}/{without.Player1.BaseMana} hand {withBonus.Player1.HandZone.Count}/{without.Player1.HandZone.Count}");
    Check("R4 the power override is the match's hero power", withBonus.Player1.Hero.HeroPower.Card.Id == DungeonRunSetup.PowerCardId("mage_rimebite"));
    run.BeginMatch(s); run.RecordOutcome(s, fresh, DungeonRunOutcome.Fault);
    Check("R4 a fault keeps the bonus for the restaged fight", s.NextFight?.Mana == 4);
    WinFight(s);
    Check("R4 once that fight is recorded the bonus is spent; the override stays", s.NextFight == null && s.PowerOverride == "mage_rimebite");
}

// ---------------------------------------------------------------- R5 a lost match drops its requests; refusals are notes
{
    var s = Act();
    var deckBefore = s.Deck.ToList();
    var g = StartPlan(run.BuildPlan(s), 11);
    RunHelpers.EditAdd(g, s.Deck[0]); RunHelpers.OverridePower(g, "mage_rimebite"); RunHelpers.GrantNextFight(g, 4, 2); RunHelpers.AppendSlotLog(Slot(g, 0), "x");
    var notes = new List<string>();
    run.BeginMatch(s); notes = run.RecordOutcome(s, fresh, DungeonRunOutcome.Loss, g.RunResult);
    Check("R5 a lost match applies nothing (the run ends)", s.Finished && s.Deck.SequenceEqual(deckBefore) && s.PowerOverride == null && s.NextFight == null && s.Cards[0].Log.Count == 0 && notes.Count == 0);
    var t = Act(seed: 3);
    var tg = StartPlan(run.BuildPlan(t), 12);
    RunHelpers.EditAdd(tg, "CS2_106"); // a Warrior weapon: refused by the run's class check
    RunHelpers.OverridePower(tg, "mage_rimebite");
    run.BeginMatch(t);
    var tn = run.RecordOutcome(t, fresh, DungeonRunOutcome.Win, tg.RunResult);
    Check("R5 a refused edit is a note, changes no card, and the fight is still recorded with the rest applied",
        tn.Count == 1 && tn[0].StartsWith("edits refused: class") && !t.Deck.Contains("CS2_106") && t.Matches.Last().Outcome == DungeonRunOutcome.Win && t.PowerOverride == "mage_rimebite",
        string.Join(" | ", tn));
    var legacy = run.NewRun(fresh, "MAGE", data.Class("MAGE").DefaultDeck, "CS2_034", 7, schedule: DungeonRunSchedules.LegacyId, signatureId: run.SignatureOffer(fresh, run.Data.FindClass("MAGE"), 7).FirstOrDefault());
    var lg = StartPlan(run.BuildPlan(legacy), 13);
    RunHelpers.EditAdd(lg, legacy.Deck[0]); RunHelpers.AppendSlotLog(Slot(lg, 0), "x"); RunHelpers.GrantNextFight(lg, 1, 0);
    run.BeginMatch(legacy);
    var ln = run.RecordOutcome(legacy, fresh, DungeonRunOutcome.Win, lg.RunResult);
    Check("R5 a legacy run refuses edits and logs (no card records) but keeps the bonus", ln.Count == 2 && ln.Any(x => x.StartsWith("slot logs refused")) && ln.Any(x => x.StartsWith("edits refused: not-act-run"))
        && legacy.Deck.Count == 10 && legacy.NextFight?.Mana == 1, string.Join(" | ", ln));
    Check("R5 a RunEdit refuses a malformed request", Throws(() => new RunEdit(RunEdit.OpAdd, 2, "x")) && Throws(() => new RunEdit(RunEdit.OpRemove, 1, "x")) && Throws(() => new RunEdit("swap", 1, "x")));
}

// ---------------------------------------------------------------- R6 belief resets (H8) and the fair search's listener
{
    var s = Act();
    var g = StartPlan(run.BuildPlan(s), 14);
    var hand = g.Player1.HandZone.Select(c => c.Id).ToList();
    var other = g.Player2.HandZone.Select(c => c.Id).ToList();
    Check("R6 no reset before one is asked", g.RunBeliefResets.Count == 0);
    RunHelpers.BeliefReset(g.Player1, RunBeliefZones.Hand);
    var reset = g.RunBeliefResets.Single();
    Check("R6 a reset records the controller, the zones and the hand's entities at that moment", reset.ControllerId == g.Player1.PlayerId && reset.Zones == RunBeliefZones.Hand
        && reset.EntityIds.SequenceEqual(hand) && reset.HistorySequence == g.HistoryFacts.RecordCount);
    var clone = g.Clone();
    Check("R6 a clone keeps the resets", clone.RunBeliefResets.Count == 1 && clone.RunBeliefResets[0].EntityIds.SequenceEqual(hand));
    RunHelpers.BeliefReset(clone.Player1, RunBeliefZones.Deck);
    Check("R6 a clone's reset stays in the clone", g.RunBeliefResets.Count == 1 && clone.RunBeliefResets.Count == 2);
    // The listener: public hand knowledge of the reset player's hand is dropped; the other player's is kept.
    var known = hand.Concat(other).ToDictionary(id => id, id => "known");
    int dropped = FairBotState.ForgetResetHand(g, g.Player1.PlayerId, known);
    Check("R6 ForgetResetHand drops what the bot knew about the reset hand, keeps the other hand", dropped == hand.Count && hand.All(id => !known.ContainsKey(id)) && other.All(known.ContainsKey));
    var mine = other.ToDictionary(id => id, id => "known");
    Check("R6 a reset of the other player leaves this hand's knowledge alone", FairBotState.ForgetResetHand(g, g.Player2.PlayerId, mine) == 0 && mine.Count == other.Count);
    var deckOnly = StartPlan(run.BuildPlan(s), 15);
    RunHelpers.BeliefReset(deckOnly.Player1, RunBeliefZones.Deck);
    var dk = deckOnly.Player1.HandZone.Select(c => c.Id).ToDictionary(id => id, id => "known");
    Check("R6 a deck-only reset clears no hand knowledge (deck cards are private actors anyway)", FairBotState.ForgetResetHand(deckOnly, deckOnly.Player1.PlayerId, dk) == 0
        && deckOnly.RunBeliefResets[0].EntityIds.Count == deckOnly.Player1.DeckZone.Count);
    // A full fair view after a reset still builds (the reconstruction path runs the listener): after both mulligans, on the
    // boss's turn, against the same game without a reset.
    bool Built(bool withReset, out string why)
    {
        why = "";
        var fg = StartPlan(run.BuildPlan(s), 16);
        foreach (var p in new[] { fg.Player1, fg.Player2 }) fg.Process(SabberStoneCore.Tasks.PlayerTasks.ChooseTask.Mulligan(p, new List<int>()));
        fg.Process(SabberStoneCore.Tasks.PlayerTasks.EndTurnTask.Any(fg.CurrentPlayer));
        if (withReset) RunHelpers.BeliefReset(fg.Player1, RunBeliefZones.HandAndDeck);
        try { return FairBotState.Create(fg, fg.Player2.PlayerId, 4242, DeckStyle.Tempo, new FairOptions { RemoveOpponentSecrets = true }) != null; }
        catch (Exception e) { why = e.GetType().Name + ": " + e.Message + " " + (e.StackTrace ?? "").Split((char)10).FirstOrDefault(); return false; }
    }
    bool control = Built(false, out string cw), withReset = Built(true, out string rw);
    Check("R6 the bot's fair view builds over a game with a belief reset, as over the same game without one", control && withReset, cw + " | " + rw);
}

bool Throws(Action a) { try { a(); return false; } catch { return true; } }
Console.WriteLine($"c-runctx: {passes} passed, {failures} failed");
return failures == 0 ? 0 : 1;
