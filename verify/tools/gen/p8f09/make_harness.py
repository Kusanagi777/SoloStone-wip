# Builds suites/c-bosspilot-enc09 from the shared encounter bosspilot (p8f01/suites/c-bosspilot-encounter): the same harness
# with what family 09's rule-cast powers and no-loss ending need to be seen in a bosspilot log. Usage:
#   python -I verify/tools/gen/p8f09/make_harness.py [<output folder>]
# (ported to verify/: base verify/suites/p8f01/c-bosspilot-encounter; output verify/suites/p8f09/c-bosspilot-enc09 by default)
# Added (nothing else changes; without AUTO the final line only gains ", no-loss N"):
#   - each game line ends with " auto N" when the boss's power is cast by rule (DungeonRunEncounters.AutoCasts) and with
#     " no-loss" when a script ended the fight with no loss for the run player;
#   - AUTO=id,id  the rows whose power is cast by rule: one with no cast in all its games is listed ("auto-unused");
#   - a check that the run's match summary carries the no-loss mark exactly when the harvest does, as a win.
import io, os, sys
V = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))  # verify/, from verify/tools/gen/<family>/
src = os.path.join(V, 'suites', 'p8f01', 'c-bosspilot-encounter')
dst = sys.argv[1] if len(sys.argv) > 1 else os.path.join(V, 'suites', 'p8f09', 'c-bosspilot-enc09')
os.makedirs(dst, exist_ok=True)
proj = io.open(os.path.join(src, 'BossPilot.csproj'), encoding='utf-8', newline='').read()
io.open(os.path.join(dst, 'BossPilot.csproj'), 'w', encoding='utf-8', newline='').write(proj)
s = io.open(os.path.join(src, 'Program.cs'), encoding='utf-8', newline='').read()


def rep(a, b):
    global s
    assert s.count(a) == 1, a[:90]
    s = s.replace(a, b)


rep('''// A boss with shrine variants plays its games once per variant (the seeds that roll that variant).
''', '''// A boss with shrine variants plays its games once per variant (the seeds that roll that variant).
// Family 09 variant (p8f09/make_harness.py): a game line ends with " auto N" for a power cast by rule and " no-loss" for a fight
// a script ended with no loss; AUTO=id,id lists the rows whose power is cast by rule (one never cast is "auto-unused").
''')
rep('''var watched = new Dictionary<string, int>(StringComparer.Ordinal); int mostSecrets = 0;
''', '''var watched = new Dictionary<string, int>(StringComparer.Ordinal); int mostSecrets = 0;
string[] autoRows = Env("AUTO")?.Split(',').Select(id => id.Trim()).ToArray();
var autoCasts = new Dictionary<string, int>(StringComparer.Ordinal); int noLossGames = 0;
''')
rep('''                run.RecordOutcome(s, profile, outcome, game.RunResult, end);
''', '''                run.RecordOutcome(s, profile, outcome, game.RunResult, end);
                int auto = DungeonRunEncounters.AutoCasts(game.Player2.Hero.HeroPower);
                autoCasts[boss.Id] = (autoCasts.TryGetValue(boss.Id, out int castsSoFar) ? castsSoFar : 0) + auto;
                if (game.Player2.Hero.HeroPower.IsPassiveHeroPower && (auto > 0 || autoRows != null && autoRows.Contains(boss.Id))) extra += " auto " + auto;
                var recorded = s.Matches.Last();
                Check(recorded.NoLoss == end.NoLoss && (!end.NoLoss || recorded.Outcome == DungeonRunOutcome.Win && result == "player"), "the no-loss mark of the match summary", problems);
                if (end.NoLoss) { noLossGames++; extra += " no-loss"; }
''')
rep('''    + (watch == null ? "" : ", watched "''', '''    + ", no-loss " + noLossGames
    + (autoRows == null ? "" : ", auto-unused " + autoRows.Count(id => !autoCasts.TryGetValue(id, out int n0) || n0 == 0)
        + (autoRows.Any(id => !autoCasts.TryGetValue(id, out int n1) || n1 == 0) ? " (" + string.Join(",", autoRows.Where(id => !autoCasts.TryGetValue(id, out int n2) || n2 == 0)) + ")" : ""))
    + (watch == null ? "" : ", watched "''')
rep('''return faults + failed + SearchTelemetry.SwallowedLeafErrors;''',
    '''return faults + failed + SearchTelemetry.SwallowedLeafErrors + (autoRows == null ? 0 : autoRows.Count(id => !autoCasts.TryGetValue(id, out int n3) || n3 == 0));''')
io.open(os.path.join(dst, 'Program.cs'), 'w', encoding='utf-8', newline='').write(s)
print('wrote', dst)
