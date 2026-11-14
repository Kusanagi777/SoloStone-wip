using System.Linq;
using SabberStoneCore.CardSets.DungeonRun;
using static Audit.H;
namespace Audit {
  internal static class ZProbe {
    [AuditTest] static void Probe_PowerTokens() {
      foreach (var c in DungeonRunContent.All.Where(c => c.Kind == RunContentKind.HeroPower && c.Tokens.Count > 0))
        Note(c.Key + " -> " + string.Join(",", c.Tokens));
    }
  }
}
