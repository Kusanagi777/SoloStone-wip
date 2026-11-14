using System;
using System.Linq;
using SabberStoneCore.Auras;
using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.Actions
{
    public static class DeathrattleRules
    {
        // Controller tags read from the live source auras (Controller's indexer, Aura.DeathrattleValue), not a controller field:
        // the Deathrattle multipliers and, phase 7 S3, the additional Battlecries (BattlecryRules, TRLA_154). The two
        // ADDITIONAL tags are counts; the others are on/off.
        internal static bool IsMultiplier(GameTag tag) => tag == GameTag.EXTRA_DEATHRATTLES_BASE
            || tag == GameTag.EXTRA_MINION_DEATHRATTLES_BASE || tag == GameTag.EXTRA_DEATHRATTLES_ADDITIONAL
            || tag == GameTag.EXTRA_BATTLECRIES_ADDITIONAL;
        internal static int AuraValue(Controller owner, GameTag tag)
        {
            int value = owner.Game.Auras?.OfType<Aura>().Sum(a => a.DeathrattleValue(owner,tag)) ?? 0;
            return tag == GameTag.EXTRA_DEATHRATTLES_ADDITIONAL || tag == GameTag.EXTRA_BATTLECRIES_ADDITIONAL ? value : Math.Min(1,Math.Max(0,value));
        }
        public static int Repeats(Controller owner, IEntity source) => 1
            + (owner[GameTag.EXTRA_DEATHRATTLES_BASE] > 0
                || source is Minion && owner[GameTag.EXTRA_MINION_DEATHRATTLES_BASE] > 0 ? 1 : 0)
            + Math.Max(0, owner[GameTag.EXTRA_DEATHRATTLES_ADDITIONAL]);
    }
}
