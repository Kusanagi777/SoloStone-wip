using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.PlayerTasks;

// Step 9 (plan D3): the run harness's game loop, a copy of the encounter bosspilot's (verify/suites/p8f01/c-bosspilot-encounter
// Program.cs): the same calls in the same order, the same action guard, the same messages. It is a copy, not a shared file: four
// generators (make_bp_collect.py, make_bp_rules.py, make_bp_rules06.py, make_harness.py) patch the encounter harness's loop by
// its exact text, so that loop stays inline there.

/// <summary>
/// One side of a game piloted by the fair AI (GoldenPilot through BotDriver.Decide): the encounter harness's boss, the run
/// harness's boss and run player. Counts what the encounter harness printed per game.
/// </summary>
internal sealed class FairSide
{
    private readonly Game game;
    private readonly int playerId;
    private readonly DeckStyle style;
    internal GoldenPilot Golden;
    internal NpcProfile Profile;
    /// <summary>The name a FALLBACK line on stderr carries (the encounter harness: the boss row id).</summary>
    private readonly string label;
    internal int DecisionIndex, Decisions, Fallbacks, PowerUses;
    /// <summary>Options the AI may take (null: every option); passed to BotDriver.Decide as its nativeActionEnabled filter.</summary>
    internal Func<PlayerTask, bool> Enabled;
    /// <summary>Called with each chosen task before it is played.</summary>
    internal Action<PlayerTask> OnTask;

    internal FairSide(Game game, int playerId, DeckStyle style, GoldenPilot golden, NpcProfile profile, string label)
    {
        this.game = game; this.playerId = playerId; this.style = style; Golden = golden; Profile = profile; this.label = label;
    }

    internal PlayerTask Decide()
    {
        var (task, trace) = Enabled == null
            ? BotDriver.Decide(game, playerId, style,
                st => Golden.DecidePilot(st, new GoldenContext(game.Turn, DecisionIndex, unchecked(997 * game.Turn + DecisionIndex * 31), Profile)),
                ref DecisionIndex, Golden.Options)
            : BotDriver.Decide(game, playerId, style,
                st => Golden.DecidePilot(st, new GoldenContext(game.Turn, DecisionIndex, unchecked(997 * game.Turn + DecisionIndex * 31), Profile)),
                ref DecisionIndex, Golden.Options, null, Enabled);
        Decisions++;
        if (trace?.Kind == "fallback") { Fallbacks++; Console.Error.WriteLine($"  FALLBACK {label} t{game.Turn}: {trace.FallbackReason}"); }
        if (task is HeroPowerTask) PowerUses++;
        OnTask?.Invoke(task);
        return task;
    }
}

internal static class PlayGame
{
    /// <summary>
    /// Plays <paramref name="game"/> (after MainBegin) until it is complete or its turn passes <paramref name="turnCap"/>:
    /// <paramref name="boss"/> picks player 2's tasks, <paramref name="player"/> player 1's; <paramref name="afterBoss"/> runs after
    /// each boss task is played. A choice outside the mulligan is answered through TryAnswer. Throws after
    /// <paramref name="actionLimit"/> actions or when the engine rejects a task, with the last ten actions in the message, or when
    /// a side acts past the other side's open choice. Returns how many choices were answered off turn.
    /// </summary>
    internal static int Loop(Game game, int turnCap, Func<PlayerTask> boss, Func<PlayerTask> player, Action afterBoss = null, int actionLimit = 1500)
    {
        int actions = 0;
        var recent = new Queue<string>();
        void Act(PlayerTask task, string who)
        {
            recent.Enqueue($"t{game.Turn} {who}: {task}");
            if (recent.Count > 10) recent.Dequeue();
            if (++actions > actionLimit) throw new Exception("stuck after " + actionLimit + " actions | " + string.Join(" | ", recent));
            // The live host's order (NativeMatch.DriveUntilHumanInput, NpcCanAct): the boss never acts while the run player holds
            // an open choice, and the run player answers only its own choice while the boss holds one.
            var other = task.Controller.Opponent;
            if (game.Step != Step.BEGIN_MULLIGAN && other.Choice != null && (other == game.Player1 || !(task is ChooseTask)))
                throw new Exception($"{who} acted past the other side's open choice | " + string.Join(" | ", recent));
            if (task is ChooseTask pick && game.Step != Step.BEGIN_MULLIGAN)
            {
                var decision = game.GetPendingDecisions().Single(d => d.ControllerId == task.Controller.Id);
                if (!game.TryAnswer(new SabberStoneCore.Model.Decisions.DecisionAnswer(decision.Id, decision.ControllerId, pick.Choices.ToArray()), out var why))
                    throw new Exception($"engine rejected {who} choice ({why}) | " + string.Join(" | ", recent));
            }
            else if (!game.Process(task)) throw new Exception($"engine rejected {who} task | " + string.Join(" | ", recent));
        }
        int offTurn = 0;
        while (game.State != State.COMPLETE && game.Turn <= turnCap)
        {
            // A choice opened for the side whose turn it is not (ULDA_802 Plague of Madness: the right-most minion's forced attack
            // at its owner's turn end, so Vicious Fledgling adapts after the turn has passed) is answered first, as the live host
            // does: the boss waits while the run player holds a choice, and answers its own before the run player acts (the run
            // player's own choice on its turn still goes first). Controller.Options is empty off turn and the fair search plans
            // only its own turn, so that side takes LocalBot's pick of the offers.
            var off = game.CurrentPlayer.Opponent;
            if (off.Choice != null && off.Choice.ChoiceType != ChoiceType.MULLIGAN && !(off == game.Player2 && game.Player1.Choice != null))
            {
                offTurn++;
                Act(LocalBot.Choose(off.Choice.Choices.Select(id => (PlayerTask)ChooseTask.Pick(off, id)).ToList()), off == game.Player2 ? "boss" : "player");
                if (off == game.Player2) afterBoss?.Invoke();
                continue;
            }
            if (game.CurrentPlayer == game.Player2)
            {
                Act(boss(), "boss");
                afterBoss?.Invoke();
            }
            else Act(player(), "player");
        }
        return offTurn;
    }
}
