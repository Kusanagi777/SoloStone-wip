# Phase 8 family 02: builds p8f02/suites/c-bosspilot-collect from the encounter bosspilot harness
# (p8f01/suites/c-bosspilot-encounter) with one addition: PLAYER_CARDS=id,id,... puts those cards into the run player's
# match deck (the first cards of the plan's deck are replaced, the deck size stays), and the final line counts how often
# the run player played each WATCH card. It proves the run player's side of the family's collectibles against a boss
# piloted by the fair AI (the boss's fair view and search meet the cards as the opponent's plays).
# The sources of truth stay the base harness and this script: rerun it after the base harness changes.
#   python -I verify/tools/gen/p8f02/make_bp_collect.py [<output folder>]
# (ported to verify/: base verify/suites/p8f01/c-bosspilot-encounter; output verify/suites/p8f02/c-bosspilot-collect by default)
import os, sys
V = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))  # verify/, from verify/tools/gen/<family>/
BASE = V + '/suites/p8f01/c-bosspilot-encounter'
OUT = sys.argv[1] if len(sys.argv) > 1 else V + '/suites/p8f02/c-bosspilot-collect'

def once(text, old, new):
    if text.count(old) != 1: raise SystemExit('expected one match for %r, found %d' % (old[:60], text.count(old)))
    return text.replace(old, new)

src = open(BASE + '/Program.cs', encoding='utf-8', newline='').read()
src = once(src,
'''// A boss with shrine variants plays its games once per variant (the seeds that roll that variant).
''',
'''// A boss with shrine variants plays its games once per variant (the seeds that roll that variant).
// Family 02 variant (p8f02/make_bp_collect.py):
//   PLAYER_CARDS=id,id  these cards replace the first cards of the run player's match deck (the deck size stays); the final
//                       line then also counts how often the run player played each WATCH card.
''')
src = once(src,
'''var watched = new Dictionary<string, int>(StringComparer.Ordinal); int mostSecrets = 0;
''',
'''var watched = new Dictionary<string, int>(StringComparer.Ordinal); int mostSecrets = 0;
string[] playerCards = Env("PLAYER_CARDS")?.Split(',').Select(id => id.Trim()).Where(id => id.Length > 0).ToArray();
var playerWatched = new Dictionary<string, int>(StringComparer.Ordinal);
''')
src = once(src,
'''                var plan = run.BuildPlan(s, profile);
''',
'''                var plan = run.BuildPlan(s, profile);
                if (playerCards != null)
                {
                    // The injected cards first, then the run deck's own cards, to the deck's size.
                    var injected = playerCards.Select(id => Cards.FromId(id) ?? throw new Exception("unknown card " + id)).ToList();
                    int size = Math.Max(plan.PlayerDeck.Count, injected.Count);
                    plan.PlayerDeck = injected.Concat(plan.PlayerDeck.Skip(injected.Count)).Take(size).ToList();
                }
''')
src = once(src,
'''                        var heroCard = options.FirstOrDefault(t => t is PlayCardTask play && play.Source is Hero);
                        Act(heroCard ?? LocalBot.Choose(options), "player");
''',
'''                        var heroCard = options.FirstOrDefault(t => t is PlayCardTask play && play.Source is Hero);
                        // With injected cards the run player plays one of them whenever it can (the cards under test), else the local bot's pick.
                        var injectedPlay = playerCards == null ? null : options.FirstOrDefault(t => t is PlayCardTask play && play.Source != null && playerCards.Contains(play.Source.Card.Id));
                        var playerTask = heroCard ?? injectedPlay ?? LocalBot.Choose(options);
                        if (watch != null && playerTask is PlayCardTask mine && mine.Source != null && watch.Contains(mine.Source.Card.Id))
                            playerWatched[mine.Source.Card.Id] = playerWatched.TryGetValue(mine.Source.Card.Id, out int seenMine) ? seenMine + 1 : 1;
                        Act(playerTask, "player");
''')
src = once(src,
'''    + (watch == null ? "" : ", watched " + string.Join(" ", watch.Select(id => id + " x" + (watched.TryGetValue(id, out int n) ? n : 0))) + ", most boss Secrets " + mostSecrets));
''',
'''    + (watch == null ? "" : ", watched " + string.Join(" ", watch.Select(id => id + " x" + (watched.TryGetValue(id, out int n) ? n : 0))) + ", most boss Secrets " + mostSecrets)
    + (watch == null || playerCards == null ? "" : ", player played " + string.Join(" ", watch.Select(id => id + " x" + (playerWatched.TryGetValue(id, out int n) ? n : 0)))));
''')
os.makedirs(OUT, exist_ok=True)
open(OUT + '/Program.cs', 'w', encoding='utf-8', newline='').write(src)
proj = open(BASE + '/BossPilot.csproj', encoding='utf-8', newline='').read()
open(OUT + '/BossPilotCollect.csproj', 'w', encoding='utf-8', newline='').write(proj)
print('wrote', OUT)
