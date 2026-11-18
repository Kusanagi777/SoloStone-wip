using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.PlayerTasks;

// Step 9 (plan D3): the game loop of the encounter bosspilot, factored out of its Program.cs so that the run harness
// (verify/suites/p9/c-bosspilot-run, which links this file) plays its games with the same code. The encounter harness's output
// stays byte for byte what it was: the same calls in the same order, the same 1500-action guard, the same messages.

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
    /// <paramref name="actionLimit"/> actions or when the engine rejects a task, with the last ten actions in the message.
    /// </summary>
    internal static void Loop(Game game, int turnCap, Func<PlayerTask> boss, Func<PlayerTask> player, Action afterBoss = null, int actionLimit = 1500)
    {
        int actions = 0;
        var recent = new Queue<string>();
        void Act(PlayerTask task, string who)
        {
            recent.Enqueue($"t{game.Turn} {who}: {task}");
            if (recent.Count > 10) recent.Dequeue();
            if (++actions > actionLimit) throw new Exception("stuck after " + actionLimit + " actions | " + string.Join(" | ", recent));
            if (task is ChooseTask pick && game.Step != Step.BEGIN_MULLIGAN)
            {
                var decision = game.GetPendingDecisions().Single(d => d.ControllerId == task.Controller.Id);
                if (!game.TryAnswer(new SabberStoneCore.Model.Decisions.DecisionAnswer(decision.Id, decision.ControllerId, pick.Choices.ToArray()), out var why))
                    throw new Exception($"engine rejected {who} choice ({why}) | " + string.Join(" | ", recent));
            }
            else if (!game.Process(task)) throw new Exception($"engine rejected {who} task | " + string.Join(" | ", recent));
        }
        while (game.State != State.COMPLETE && game.Turn <= turnCap)
        {
            if (game.CurrentPlayer == game.Player2)
            {
                Act(boss(), "boss");
                afterBoss?.Invoke();
            }
            else Act(player(), "player");
        }
    }
}
