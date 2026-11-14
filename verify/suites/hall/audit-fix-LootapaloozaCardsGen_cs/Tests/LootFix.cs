using System.Linq;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Audit.H;

namespace Audit
{
    internal static class LootFix
    {
        // Ironwood Golem (LOOT_048): "Taunt. Can only attack if you have 3 or more Armor."
        [AuditTest]
        static void LOOT_048_IronwoodGolem_ArmorThreshold()
        {
            Note(CardText("LOOT_048"));
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var m = Board(p, "LOOT_048");
            Check("taunt", m.HasTaunt);
            Round(g); Mana(p, 10); ClearHand(o);
            Check("0 armor: cannot attack", !m.CanAttack, Describe(g));
            p.Hero.Armor = 2; g.DeathProcessingAndAuraUpdate();
            Check("2 armor: cannot attack", !m.CanAttack);
            p.Hero.Armor = 3; g.DeathProcessingAndAuraUpdate();
            Check("3 armor: can attack", m.CanAttack, "armor " + p.Hero.Armor);
            var c = Clone(g);
            Check("clone at 3 armor: can attack", At(c, m).CanAttack);
            p.Hero.Armor = 4; g.DeathProcessingAndAuraUpdate();
            Check("4 armor: can attack", m.CanAttack);
            p.Hero.Armor = 3; g.DeathProcessingAndAuraUpdate();
            Check("attack at 3 armor works", g.Process(MinionAttackTask.Any(p, m, o.Hero)));
            Eq("hit hero for 3", 27, Hp(o.Hero));
            p.Hero.Armor = 2; g.DeathProcessingAndAuraUpdate();
            Check("back to 2 armor: cannot attack", !m.CanAttack);
        }

        // Gemstudded Golem (LOOT_365): "Taunt. Can only attack if you have 5 or more Armor." -- untouched control.
        [AuditTest]
        static void LOOT_365_GemstuddedGolem_Unchanged()
        {
            Note(CardText("LOOT_365"));
            var g = NewGame(); var p = P1(g); var o = P2(g);
            var m = Board(p, "LOOT_365");
            Round(g); Mana(p, 10); ClearHand(o);
            p.Hero.Armor = 4; g.DeathProcessingAndAuraUpdate();
            Check("4 armor: cannot attack", !m.CanAttack);
            p.Hero.Armor = 5; g.DeathProcessingAndAuraUpdate();
            Check("5 armor: can attack", m.CanAttack);
        }
    }
}
