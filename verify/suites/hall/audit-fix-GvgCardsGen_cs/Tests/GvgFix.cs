// Regression: GVG_048 Metaltooth Leaper "Battlecry: Give your other Mechs +2 Attack."
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;
using static Audit.H;

namespace Audit
{
    internal static class GvgFix
    {
        [AuditTest]
        static void GVG_048_OtherMechsPlus2()
        {
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var mech = Board(p, "BOT_445t"); var y = Board(p, Yeti); var em = Board(o, "BOT_445t");
            var l = (Minion)PlayNew(g, p, "GVG_048");
            Eq("friendly mech +2", 3, Atk(mech)); Eq("yeti none", 4, Atk(y)); Eq("enemy mech none", 1, Atk(em));
            Eq("leaper itself not buffed (other)", 3, Atk(l));
            Check("leaper has no GVG_048e", !EnchantIds(l).Contains("GVG_048e"));
            var c = Clone(g);
            Eq("clone: friendly mech +2", 3, Atk((Minion)At(c, mech)));
            Eq("clone: leaper 3", 3, Atk((Minion)At(c, l)));
        }

        [AuditTest]
        static void GVG_048_SecondLeaperBuffsFirst()
        {
            var g = NewGame(); var p = P1(g);
            var l1 = (Minion)PlayNew(g, p, "GVG_048");
            var l2 = (Minion)PlayNew(g, p, "GVG_048");
            Eq("first leaper buffed by second", 5, Atk(l1));
            Eq("second leaper not self-buffed", 3, Atk(l2));
        }

        [AuditTest]
        static void GVG_048_AloneNoBuff()
        {
            var g = NewGame(); var p = P1(g);
            var l = (Minion)PlayNew(g, p, "GVG_048");
            Eq("alone stays 3", 3, Atk(l));
        }
    }
}
