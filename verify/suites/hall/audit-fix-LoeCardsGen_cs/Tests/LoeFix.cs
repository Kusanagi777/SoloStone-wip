using System.Linq;
using SabberStoneCore.Model.Entities;
using static Audit.H;

namespace Audit
{
    internal static class LoeFix
    {
        // Mounted Raptor (LOE_050): "Deathrattle: Summon a random 1-Cost minion."
        [AuditTest]
        static void LOE_050_MountedRaptor()
        {
            Note(CardText("LOE_050"));
            for (int seed = 1; seed <= 8; seed++)
            {
                int sd = seed;
                var g = NewGame(s => { s.PlayerHero = "HERO_06"; s.Seed = sd; }); var p = P1(g);
                var r = Board(p, "LOE_050");
                Kill(g, r);
                Check($"seed {seed}: Deathrattle summoned one minion", p.BoardZone.Count == 1, Describe(g));
                if (p.BoardZone.Count == 1) Eq($"seed {seed}: 1-cost ({p.BoardZone[0].Card.Id})", 1, p.BoardZone[0].Card.Cost);
                Eq($"seed {seed}: enemy board untouched", 0, P2(g).BoardZone.Count);
            }
            var g2 = NewGame(s => s.PlayerHero = "HERO_06"); var p2 = P1(g2);
            var raptor = (Minion)PlayNew(g2, p2, "LOE_050");
            Eq("played from hand: only the Raptor on board (no Battlecry summon)", 1, p2.BoardZone.Count);
            Eq("Raptor is 3/2", "3/2", Atk(raptor) + "/" + Hp(raptor));
            Check("has Deathrattle", raptor.HasDeathrattle, Describe(g2));
            // After clone, the Deathrattle still works.
            var c = Clone(g2); var cp = At(c, p2);
            Kill(c, At(c, raptor));
            Check("clone: Deathrattle summoned one 1-cost minion", cp.BoardZone.Count == 1 && cp.BoardZone[0].Card.Cost == 1, Describe(c));
            // Silenced: no Deathrattle.
            var g3 = NewGame(); var p3 = P1(g3);
            var r3 = Board(p3, "LOE_050"); Silence(g3, r3); Kill(g3, r3);
            Eq("silenced: nothing summoned", 0, p3.BoardZone.Count);
        }
    }
}
