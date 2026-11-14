// Regression tests for FIR_907 Amirdrassil: a location refreshes at the start of your next turn (no extra cooldown).
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Audit.H;

namespace Audit
{
    internal static class AmirdrassilFix
    {
        static bool ActivateLoc(Game g, Controller c, IPlayable loc, IPlayable target = null)
            => g.Process(new ActivateLocationTask(c, loc, target));
        static BoardObject Loc(Controller c, string id) => c.BoardZone.GetLocations().FirstOrDefault(l => l.Card.Id == id);
        static Minion NewMinion(Controller c, List<int> before) => c.BoardZone.FirstOrDefault(m => !before.Contains(m.Id));

        [AuditTest]
        static void FIR_907_Amirdrassil()
        {
            var g = NewGame(s => s.PlayerDeck = Fill(30)); var p = P1(g);
            PlayNew(g, p, "FIR_907");
            var loc = Loc(p, "FIR_907");
            Check("location on board", loc != null, Describe(g)); if (loc == null) return;
            for (int n = 1; n <= 3; n++)
            {
                ClearHand(p); Mana(p, 10); p.UsedMana = 10;
                int armor = Armor(p); var before = p.BoardZone.Select(m => m.Id).ToList();
                Check($"use {n}: usable on turn {g.Turn}", LocationRules.CanActivate(p, loc, null),
                    $"EXHAUSTED {loc[GameTag.EXHAUSTED]} LOCATION_ACTION_COOLDOWN {loc[GameTag.LOCATION_ACTION_COOLDOWN]}");
                Check($"use {n}: activate", ActivateLoc(g, p, loc));
                var m = NewMinion(p, before);
                Check($"use {n}: summoned a {n}-Cost minion", m != null && m.Card.Cost == n, m?.Card.Id + " " + m?.Card.Cost);
                Eq($"use {n}: armor +{n}", armor + n, Armor(p));
                Eq($"use {n}: drew {n}", n, p.HandZone.Count);
                Eq($"use {n}: refreshed {n} Mana", n, p.RemainingMana);
                if (n < 3)
                {
                    Eq($"use {n}: durability", 3 - n, loc.Durability);
                    Check($"use {n}: cannot reuse this turn", !ActivateLoc(g, p, loc));
                    EndTurn(g);
                    Check($"use {n}: still exhausted on the opponent's turn", !LocationRules.CanActivate(p, loc, null));
                    EndTurn(g);
                }
            }
            Check("location gone after 3 uses", Loc(p, "FIR_907") == null, Describe(g));
        }

        [AuditTest]
        static void FIR_907_Amirdrassil_ReusableAfterClone()
        {
            var g = NewGame(s => s.PlayerDeck = Fill(30)); var p = P1(g);
            PlayNew(g, p, "FIR_907");
            var loc = Loc(p, "FIR_907");
            ActivateLoc(g, p, loc);
            var c = Clone(g); var cp = At(c, p); var cl = Loc(cp, "FIR_907");
            Round(c);
            Check("clone: usable on your next turn", cl != null && LocationRules.CanActivate(cp, cl, null),
                $"EXHAUSTED {cl?[GameTag.EXHAUSTED]} COOLDOWN {cl?[GameTag.LOCATION_ACTION_COOLDOWN]}");
        }

        // Control: an unaffected location still refreshes normally.
        [AuditTest]
        static void JAIL_987_ControlReusableNextTurn()
        {
            var g2 = NewGame(s => s.PlayerHero = "HERO_02"); var p2 = P1(g2);
            PlayNew(g2, p2, "JAIL_987");
            var wing = Loc(p2, "JAIL_987");
            ActivateLoc(g2, p2, wing);
            Check("not reusable this turn", !LocationRules.CanActivate(p2, wing, null));
            Round(g2);
            Check("JAIL_987 usable on next turn", LocationRules.CanActivate(p2, wing, null),
                $"EXHAUSTED {wing[GameTag.EXHAUSTED]} COOLDOWN {wing[GameTag.LOCATION_ACTION_COOLDOWN]}");
        }
    }
}
