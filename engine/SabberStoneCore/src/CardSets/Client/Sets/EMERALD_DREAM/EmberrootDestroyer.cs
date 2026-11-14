using SabberStoneCore.Conditions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class EmberrootDestroyer
    {
        internal const int DbfId = 115754;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.TAKE_DAMAGE) {
                // Global damage events include Armor damage and survive hero replacement.
                TriggerSource = TriggerSource.FRIENDLY,
                Condition = new SelfCondition(p => p == p.Controller.Hero && p.Controller == p.Game.CurrentPlayer),
                // A mortally wounded minion is never the random pick (as in the client): without this an enemy "whenever this
                // minion takes damage, damage the enemy hero" minion (Axe Flinger) answered every hit, dead or not, without end.
                SingleTask = ComplexTask.Create(
                    new IncludeTask(EntityType.OP_MINIONS),
                    new FilterStackTask(new SelfCondition(p => p is SabberStoneCore.Model.Entities.Minion m && !m.ToBeDestroyed && m.Health > 0)),
                    new RandomTask(1, EntityType.STACK),
                    new DamageTask(3, EntityType.STACK))
            } };
        }
    }
}
