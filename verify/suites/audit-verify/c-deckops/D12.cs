// D12 (c-deckops): the no-op path. A run match without per-card records (a legacy run's plan: no PlayerDeckSlots, no boss
// mana bonus, no extra health or opening cards) must play byte for byte as before the phase-5 engine change. This file is
// compiled twice: into p5/d12golden against the PRE-CHANGE engine (p5/engine-before), which writes the golden digests, and
// into c-deckops against the new engine, which compares. It uses only engine APIs both engines have.
using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.CardSets.DungeonRun;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.PlayerTasks;

internal static class D12
{
    internal static readonly string[] PlayerDeck =
    {
        "CS2_182", "CS2_231", "CS2_120", "CS2_172", "CS2_200", "CS2_008", "EX1_544", "CS2_029", "CS2_024", "CS2_022",
        "CS2_182", "CS2_120", "CS2_172", "CS2_189", "CS2_168",
    };

    /// <summary>One headless run match with full history: deterministic picks for 12 turns, digest of the printed history.</summary>
    internal static string Digest(int seed)
    {
        var plan = new RunMatchPlan
        {
            PlayerHero = Cards.FromId("HERO_08"), PlayerDeck = PlayerDeck.Select(Cards.FromId).ToList(),
            BossHero = Cards.FromId("HERO_01"), BossDeck = Enumerable.Range(0, 30).Select(i => Cards.FromId(i % 3 == 0 ? "CS2_182" : i % 3 == 1 ? "CS2_120" : "CS2_172")).ToList(),
            BossHealth = 30,
        };
        var cfg = new GameConfig { RandomSeed = seed, Shuffle = true, SkipMulligan = false, History = true, Logging = false, FormatType = FormatType.FT_WILD };
        DungeonRunSetup.Configure(cfg, plan);
        var game = new Game(cfg);
        game.StartGame();
        DungeonRunSetup.ApplyAfterStart(game, plan);
        game.Process(ChooseTask.Mulligan(game.Player1, new List<int>()));
        game.Process(ChooseTask.Mulligan(game.Player2, new List<int>()));
        game.MainBegin();
        int actions = 0;
        while (game.State != State.COMPLETE && game.Turn <= 12 && actions < 400)
        {
            var options = game.CurrentPlayer.Options();
            // The last non-end-turn option (or end turn): deterministic, and it plays cards and attacks.
            var task = options.LastOrDefault(o => o.PlayerTaskType != PlayerTaskType.END_TURN) ?? options.First();
            if (!game.Process(task)) break;
            actions++;
        }
        string text = game.PowerHistory.Print(true) + "|turn " + game.Turn + "|actions " + actions;
        // The golden digests were recorded on Windows, where Print's AppendLine ends lines with CRLF: hash those bytes everywhere.
        text = VerifyPaths.WindowsText(text);
        using var sha = System.Security.Cryptography.SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(text))).Replace("-", "").Substring(0, 16);
    }
}
