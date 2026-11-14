using System.Linq;
using SabberStoneCore.CardSets.DungeonRun;
using static Audit.H;
namespace Audit { internal static class ZQuery { [AuditTest] static void Z_PowerTokens() {
  foreach (var c in DungeonRunContent.All.Where(c => c.Kind == RunContentKind.HeroPower && c.Tokens.Count > 0)) Note(c.Key + " -> " + string.Join(",", c.Tokens));
} } }
