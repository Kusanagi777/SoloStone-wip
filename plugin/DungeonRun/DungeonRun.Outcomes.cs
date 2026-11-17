using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.CardSets.DungeonRun;

// Step 9 lane X (handoff/phase9/plan.md section 3, stage 2; D11, D13, D14): match ends, restarts and finales. Everything here
// reads the step-9 tuning only through Tuned(state) (a schedule of stamp 4 and above), so every released schedule and every
// legacy run plays and replays as before: PlanMatchEnds leaves the plan's defaults, RestartMatchSeed keeps the floor's seed and
// EscapeOrRestartBranch hands every outcome on to RecordOutcome's own paths.
//
// The match side is the engine's (Actions/EncounterRules.S9.cs): ULDA_111 Hearthstone ends a fight as an escape (a Win with
// RunMatchEnd.Escaped), GILA_911 Rewind and GILA_BOSS_48t Alternate Timeline end it as a restart (RunMatchEnd.Restarted, with
// its RestartSource; the host reports it as a Fault, and any other outcome it reports with the mark is taken as the restart).
internal sealed partial class DungeonRun
{
    /// <summary>D11: the turn cap of a tuned relay fight (Tekahn on acts-v4t); every other fight keeps RunMatchPlan's 100.</summary>
    internal const int RelayTurnCap = 150;
    /// <summary>RunMatchPlan.TurnCap's default: every fight of a released schedule, and a tuned fight without a relay.</summary>
    internal const int DefaultTurnCap = 100;
    /// <summary>D13: the games of one floor take at most this many times the fight's turn cap, restarts included.</summary>
    internal const int RestartTurnBudget = 2;
    /// <summary>D13: restarts per source per floor (Rewind once, Alternate Timeline once; one never blocks the other).</summary>
    internal const int RestartsPerSource = 1;

    // ------------------------------------------------------------------ plan (BuildPlan's last step)

    /// <summary>
    /// D11, D13, D14: the match-plan knobs of a tuned run (<see cref="Tuned"/>): RunMatchPlan.TurnCap, FormRatchet,
    /// EscapeAllowed and Restarts, and a restarted floor's carried damage. Called at the end of BuildPlan; a run that is not
    /// tuned keeps the plan's defaults (100, false, false, none).
    /// </summary>
    private void PlanMatchEnds(DungeonRunState state, DungeonRunBoss boss, RunMatchPlan plan)
    {
        if (!Tuned(state)) return;
        // D11: a relay fight (Tekahn on acts-v4t, the only relay row) gets 150 turns and the form ratchet.
        if (IsRelayFight(boss)) plan.FormRatchet = true;
        int cap = TurnCapFor(state, boss);
        // D13: a restarted floor's games share one budget of twice the cap.
        plan.TurnCap = state.RestartIndex > 0 ? Math.Max(1, cap * RestartTurnBudget - state.RestartTurns) : cap;
        plan.EscapeAllowed = EscapeAllowed(state);
        foreach (string source in RestartsFor(state, plan)) plan.Restarts.Add(source);
        // D13: Alternate Timeline carries the run hero's damage of the game it ended into the new one (on top of Battle Scars').
        if (state.RestartCarriedDamage > 0) plan.PlayerCarriedDamage = Math.Max(0, plan.PlayerCarriedDamage) + state.RestartCarriedDamage;
    }

    /// <summary>The fight against <paramref name="boss"/> has a hero relay (the Tombs finale: Tekahn).</summary>
    private static bool IsRelayFight(DungeonRunBoss boss) => boss != null && boss.Relay.Count > 0;

    /// <summary>
    /// The turn cap of the current fight before any restart: <see cref="RelayTurnCap"/> for a tuned relay fight,
    /// <see cref="DefaultTurnCap"/> otherwise (every fight of a run that is not tuned).
    /// </summary>
    internal int TurnCapFor(DungeonRunState state, DungeonRunBoss boss) =>
        Tuned(state) && IsRelayFight(boss) ? RelayTurnCap : DefaultTurnCap;

    /// <summary>
    /// D14: ULDA_111 Hearthstone may end the current fight as an escape: a tuned run whose deck holds the card and at least
    /// deckMin + 1 cards (the card leaves it), on a fight that is not in a finale act and not the schedule's last node (an
    /// escape never skips the run's last fight or a finale). The engine makes the card unplayable when this is false.
    /// </summary>
    internal bool EscapeAllowed(DungeonRunState state)
    {
        if (!Tuned(state) || state.Phase != DungeonRunPhase.PreMatch && state.Phase != DungeonRunPhase.InMatch) return false;
        if (!state.Deck.Contains(ShellOf(RunMatchEnds.Hearthstone)) || state.Deck.Count < DeckMin(state) + 1) return false;
        var schedule = ScheduleFor(state);
        var node = CurrentNode(state);
        if (schedule == null || node == null || node.Kind == DungeonRunNodeKind.Tavern || node.Kind == DungeonRunNodeKind.Modifier
            || node.Kind == DungeonRunNodeKind.Anomaly) return false;
        if (schedule.Acts[node.Act - 1].IsFinale) return false;
        return node.Index < schedule.Nodes.Count - 1;
    }

    /// <summary>
    /// D13: the restart sources the current fight may still use: GILA_911 Rewind while the run holds it, GILA_BOSS_48t
    /// Alternate Timeline when the boss's deck holds it, each at most <see cref="RestartsPerSource"/> times per floor
    /// (<see cref="DungeonRunState.RestartSources"/>), and none once the floor's turn budget is spent.
    /// </summary>
    private List<string> RestartsFor(DungeonRunState state, RunMatchPlan plan)
    {
        var sources = new List<string>();
        if (!Tuned(state) || plan.TurnCap <= 1) return sources;
        var used = RestartSourcesOf(state);
        if (state.Treasures.Contains(RunMatchEnds.Rewind) && used.Count(s => s == RunMatchEnds.Rewind) < RestartsPerSource)
            sources.Add(RunMatchEnds.Rewind);
        if ((plan.BossDeck?.Any(c => c?.Id == RunMatchEnds.AlternateTimeline) ?? false)
            && used.Count(s => s == RunMatchEnds.AlternateTimeline) < RestartsPerSource)
            sources.Add(RunMatchEnds.AlternateTimeline);
        return sources;
    }

    /// <summary>The restart sources used on the floor being fought, in order (<see cref="DungeonRunState.RestartSources"/>).</summary>
    internal static List<string> RestartSourcesOf(DungeonRunState state) =>
        (state.RestartSources ?? "").Split(',').Where(s => s.Length > 0).ToList();

    // ------------------------------------------------------------------ seed (MatchSeed)

    /// <summary>
    /// D13: the engine seed of a restarted fight: StableSeed of the run seed, MatchStream, the floor and
    /// <see cref="DungeonRunState.RestartIndex"/>, only when that is above 0 on a tuned run (so the new game deals and draws
    /// differently, and a restage of it keeps its seed); null keeps the floor's seed (MatchSeed).
    /// </summary>
    private int? RestartMatchSeed(DungeonRunState state) =>
        Tuned(state) && state.RestartIndex > 0 ? StableSeed.Derive(state.Seed, MatchStream, state.Floor, state.RestartIndex) & int.MaxValue : (int?)null;

    // ------------------------------------------------------------------ outcomes (RecordOutcome's first branch)

    /// <summary>
    /// D13, D14: an escape (ULDA_111: <see cref="DungeonRunOutcome.Win"/> with RunMatchEnd.Escaped) or a restart (GILA_911,
    /// GILA_BOSS_48t: RunMatchEnd.Restarted, reported as a <see cref="DungeonRunOutcome.Fault"/>) takes its own branch: true
    /// when it recorded the outcome (RecordOutcome then returns <paramref name="notes"/>), false to go on to the fault, loss and
    /// win paths. Called by RecordOutcome after the match summary is added, before any other branch. A run that is not tuned
    /// always goes on (false), and so does every outcome without either mark; a floor decided by any other outcome than a
    /// fault ends the restart state of the floor (match scope).
    /// </summary>
    private bool EscapeOrRestartBranch(DungeonRunState state, DungeonRunProfile profile, DungeonRunOutcome outcome, RunMatchResult result,
        RunMatchEnd end, DungeonRunMatchSummary summary, List<string> notes)
    {
        if (!Tuned(state)) return false;
        if (end != null && end.Restarted && outcome != DungeonRunOutcome.Concede) return RecordRestart(state, profile, outcome, result, end, summary, notes);
        if (end != null && end.Escaped && outcome != DungeonRunOutcome.Fault) return RecordEscape(state, profile, result, end, summary, notes);
        // Any other outcome but a fault decides the floor: its restart state ends (a fault restages the same game, seed kept).
        if (outcome != DungeonRunOutcome.Fault) ClearRestart(state);
        return false;
    }

    /// <summary>
    /// D14: the escape. Recorded as a Win with <see cref="DungeonRunMatchSummary.Escaped"/>: no major credit, no kill credit
    /// (<see cref="BossesDefeated"/> skips it), no match edits, upgrades or drafts. The fight's facts are kept as a loss keeps
    /// them (RecordEncounterEnd: a persistent boss's damage is banked and the run's Plague Lord damage is spent; Gnomebliterator
    /// casts are banked), Battle Scars keeps the hero's damage, the one-fight bonus is spent, Hearthstone leaves the run deck
    /// (outcome-implied, not through a deck edit: the deck held deckMin + 1 cards or more), then an escape tavern opens on the
    /// current node; leaving it moves on to the next node. An escape the run does not allow (the engine never casts one) is
    /// restaged as a fault, with a note.
    /// </summary>
    private bool RecordEscape(DungeonRunState state, DungeonRunProfile profile, RunMatchResult result, RunMatchEnd end,
        DungeonRunMatchSummary summary, List<string> notes)
    {
        if (!EscapeAllowed(state))
        {
            notes.Add("escape refused: this fight allows no escape (restaged)");
            summary.Outcome = DungeonRunOutcome.Fault;
            state.Phase = DungeonRunPhase.PreMatch;
            return true;
        }
        summary.Outcome = DungeonRunOutcome.Win;
        summary.Escaped = true;
        summary.NoLoss = false;
        bool ownHero = end.HeroesUsed.Count <= 1;
        bool scars = ActiveGlobalRules(state).Any(r => r.Id == BattleScars);
        // The boss is not beaten: its facts are recorded as a loss records them (this also ends the floor's hero switch).
        RecordEncounterEnd(state, profile, DungeonRunOutcome.Loss, end, summary);
        if (ownHero && scars) state.HeroDamage = Math.Max(0, Math.Min(end.PlayerHeroDamage, BattleScarsHealth - 1));
        state.NextFight = null;
        var consumed = new List<string>(result?.ConsumedTreasures ?? Array.Empty<string>());
        if (!consumed.Contains(RunMatchEnds.Hearthstone)) consumed.Add(RunMatchEnds.Hearthstone);
        foreach (string key in consumed) RemoveTreasure(state, key, notes);
        ClearRestart(state);
        notes.Add("escaped: floor " + state.Floor + " (" + summary.Boss + ")");
        DungeonRunTavernNode.Open(this, state);
        return true;
    }

    /// <summary>
    /// D13: the restart. Recorded as a Fault with <see cref="DungeonRunMatchSummary.Restarted"/> (LastBoss skips it): the floor
    /// is restaged with its hero switch and the one-fight bonus kept, under a new match seed
    /// (<see cref="DungeonRunState.RestartIndex"/>). A persistent boss banks its damage as a loss does (the new game starts at
    /// the banked remaining; the run's Plague Lord damage, which that remaining already counts, is spent). The used-up
    /// treasures (RunMatchResult.ConsumedTreasures: Rewind) leave the run. Player Health by source: Rewind restarts from the
    /// fight's start (Battle Scars' damage from earlier fights is kept; this fight's is undone), Alternate Timeline carries the
    /// damage the run hero took in this fight (<see cref="DungeonRunState.RestartCarriedDamage"/>). A restart past its source's
    /// cap, or one that leaves the floor's games no turn of their budget, is refused: the fight is recorded as lost (a turn
    /// limit when the budget is spent), with a note.
    /// </summary>
    private bool RecordRestart(DungeonRunState state, DungeonRunProfile profile, DungeonRunOutcome outcome, RunMatchResult result,
        RunMatchEnd end, DungeonRunMatchSummary summary, List<string> notes)
    {
        var boss = Data.Bosses.FirstOrDefault(b => b.Id == summary.Boss);
        string source = end.RestartSource ?? RunMatchEnds.Rewind;
        var used = RestartSourcesOf(state);
        int cap = TurnCapFor(state, boss);
        int turns = state.RestartTurns + end.Turns;
        bool capped = used.Count(s => s == source) >= RestartsPerSource;
        bool spent = turns >= cap * RestartTurnBudget - 1;
        if (capped || spent)
        {
            var lost = spent ? DungeonRunOutcome.TurnLimit
                : outcome == DungeonRunOutcome.Win || outcome == DungeonRunOutcome.Fault ? DungeonRunOutcome.Loss : outcome;
            notes.Add("restart refused (" + source + "): " + (capped ? "its once-per-floor restart is used" : "the floor's turn budget is spent")
                + "; recorded as " + lost);
            summary.Outcome = lost;
            ClearRestart(state);
            RecordEncounterEnd(state, profile, lost, end, summary);
            state.NextFight = null;
            Finish(state, profile, won: false);
            return true;
        }
        summary.Outcome = DungeonRunOutcome.Fault;
        summary.Restarted = true;
        summary.BossForm = end.BossForm;
        summary.BossRemaining = end.BossRemainingHealth;
        if (boss != null && boss.HasFlag(DungeonRunEncounterData.PersistentDamage) && BankBossDamage(state, profile, boss, end))
            state.PlagueLordDamage = 0;
        // Rewind's removal is outcome-implied too (a host that passes no match result still uses it up).
        var consumed = new List<string>(result?.ConsumedTreasures ?? Array.Empty<string>());
        if (source == RunMatchEnds.Rewind && !consumed.Contains(RunMatchEnds.Rewind)) consumed.Add(RunMatchEnds.Rewind);
        foreach (string key in consumed) RemoveTreasure(state, key, notes);
        if (source == RunMatchEnds.AlternateTimeline)
        {
            // The damage this fight's own run hero took: its damage at the end less what it started the fight with (Battle Scars).
            bool ownHero = end.HeroesUsed.Count <= 1;
            int start = ActiveGlobalRules(state).Any(r => r.Id == BattleScars) ? Math.Max(0, state.HeroDamage) : 0;
            state.RestartCarriedDamage = ownHero ? Math.Max(0, end.PlayerHeroDamage - start) : 0;
        }
        else state.RestartCarriedDamage = 0;
        used.Add(source);
        state.RestartSources = string.Join(",", used);
        state.RestartIndex++;
        state.RestartTurns = turns;
        state.Phase = DungeonRunPhase.PreMatch;
        notes.Add("restarted (" + source + "): floor " + state.Floor + ", restart " + state.RestartIndex);
        return true;
    }

    /// <summary>
    /// A persistent-damage boss's damage after a fight it was not beaten in, banked in the profile as a loss banks it
    /// (RecordEncounterEnd's rule: a run that reads the ledger only; at least 1 Health left; only below the boss's full
    /// Health). True when the ledger was written.
    /// </summary>
    private bool BankBossDamage(DungeonRunState state, DungeonRunProfile profile, DungeonRunBoss boss, RunMatchEnd end)
    {
        if (!ReadsLedger(state) || profile == null || end == null) return false;
        if (profile.BossDamage == null) profile.BossDamage = new List<DungeonRunBossDamage>();
        var record = profile.DamageOf(boss.Id);
        int full = boss.Health ?? end.BossRemainingHealth;
        int remaining = Math.Max(1, Math.Min(end.BossRemainingHealth, record?.Remaining ?? int.MaxValue));
        if (remaining >= full) return false;
        if (record == null) profile.BossDamage.Add(record = new DungeonRunBossDamage { Boss = boss.Id });
        record.Remaining = remaining;
        record.Attempts++;
        return true;
    }

    /// <summary>
    /// Removes a used-up run treasure (ULDA_111 Hearthstone's escape, GILA_911 Rewind's restart): an active treasure leaves
    /// <see cref="DungeonRunState.ActiveTreasures"/> and its card record (the first record of its shell, a treasure record
    /// first), a passive one leaves <see cref="DungeonRunState.Treasures"/>; either one's upgrade record goes too. Unchecked by
    /// the deck rules (the run layer's own removal, as its own adds are). A key the run does not hold changes nothing.
    /// </summary>
    private void RemoveTreasure(DungeonRunState state, string key, List<string> notes)
    {
        if (key == null) return;
        var treasure = Data.FindTreasure(key);
        bool active = treasure?.Active ?? DungeonRunContent.TryGet(key, out var content) && content.Kind == RunContentKind.ActiveTreasure;
        if (active)
        {
            string shell = ShellOf(key);
            var card = state.Cards.FirstOrDefault(c => c.Id == shell && DungeonRunDeck.IsTreasureOrigin(c.Origin))
                ?? state.Cards.FirstOrDefault(c => c.Id == shell);
            if (card == null && !state.ActiveTreasures.Contains(key)) return;
            state.ActiveTreasures.Remove(key);
            if (card != null)
            {
                state.Cards.Remove(card);
                state.Upgrades.RemoveAll(u => u.Uid != 0 && u.Uid == card.Uid);
                DungeonRunDeck.Sync(state);
            }
        }
        else
        {
            if (!state.Treasures.Remove(key)) return;
            state.Upgrades.RemoveAll(u => u.Key == key && u.Slot == DungeonRunTreasure.SlotPassive);
        }
        notes.Add("treasure used up: " + key);
    }

    // The deck card id of a treasure content key (its shell; the key itself for a key-shelled treasure).
    private static string ShellOf(string key) => DungeonRunContent.TryGet(key, out var content) && content.ShellId != null ? content.ShellId : key;

    /// <summary>Ends the restart state of the floor (match scope): no restart index, sources, carried damage or spent turns.</summary>
    private static void ClearRestart(DungeonRunState state)
    {
        state.RestartIndex = 0;
        state.RestartSources = null;
        state.RestartCarriedDamage = 0;
        state.RestartTurns = 0;
    }
}
