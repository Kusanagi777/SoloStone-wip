using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class UnderbellyNetwork
    {
        internal const int DbfId = 126295;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { LocationActivation = new LocationActivation {
                Target = LocationTargetKind.None, RequiresFreeSlot = true,
                // No LOCATION_ACTION_COOLDOWN: the card carries no tag 2353, so it refreshes on the owner's next turn.
                Task = EffectContext.Task(context => {
                    context.SummonRightOfLocation("JAIL_877t");
                    return TaskState.COMPLETE;
                })
            } };
        }
        internal static void ConfigureRat(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { DeathrattleTask = EffectContext.Task(context => context.Draw()) };
        }
    }
}
