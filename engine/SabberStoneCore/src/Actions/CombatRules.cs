using System;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.Actions
{
    public static class CombatRules
    {
        public const GameTag BonusRace = (GameTag)31360;
        public const GameTag BonusAmount = (GameTag)31361;
        public const GameTag DoubleRace = (GameTag)31362;
        public const GameTag OwnerTurnOnly = (GameTag)31364;
        public const GameTag IgnoreTauntAura = (GameTag)31365;
        public const GameTag Cleave = (GameTag)31366;
        public const GameTag ReplaceHeroDamage = (GameTag)31367;
        internal const GameTag DeathPasses = (GameTag)31368;
        internal const GameTag PreparationDepth = (GameTag)31369;
        public const GameTag FireImmune = (GameTag)4128;
        public static bool IsInternalTag(GameTag tag) => (int)tag >= 31360 && (int)tag <= 31369;

        private static bool Active(Minion minion) => !minion.IsSilenced && !minion.Untouchable;
        public static bool IgnoresTaunt(ICharacter source) => source.Controller.BoardZone
            .Any(m => Active(m) && m[IgnoreTauntAura] > 0);

        // A hero's Mega-Windfury may come from its weapon (LOOTA_821 Vorpal Dagger), as its Windfury does (Hero.HasWindfury).
        public static int AttackLimit(ICharacter source) => source[GameTag.MEGA_WINDFURY] > 0 || source is Hero hero && hero.WeaponHasMegaWindfury
            ? 4 : source.HasWindfury ? 2 : 1;

        public static bool PreventsFire(CardType sourceType, int school, ICharacter target) =>
            sourceType == CardType.SPELL && school == 2 && target[FireImmune] > 0
            && !(target is Minion minion && minion.IsSilenced);

        internal static int Outgoing(IPlayable source, int damage)
        {
            if (damage <= 0) return damage;
            long addition = source[GameTag.OUTGOING_DAMAGE_ADJUSTMENT];
            long multiplier = Math.Max(1, source[GameTag.OUTGOING_DAMAGE_MULTIPLIER]);
            foreach (var provider in source.Controller.BoardZone)
            {
                if (!Active(provider) || provider[OwnerTurnOnly] > 0 && source.Game.CurrentPlayer != source.Controller) continue;
                if (provider[BonusRace] > 0 && source.Card.IsRace((Race)provider[BonusRace])) addition += provider[BonusAmount];
                if (provider[DoubleRace] > 0 && source.Card.IsRace((Race)provider[DoubleRace])) multiplier = Math.Min(int.MaxValue, multiplier * 2);
            }
            return (int)Math.Min(int.MaxValue, Math.Min(int.MaxValue, Math.Max(0L, damage + addition)) * multiplier);
        }

        internal static bool ReplaceDamage(Hero target, int damage)
        {
            var weapon = target?.Weapon;
            if (damage <= 0 || weapon == null || weapon.Durability <= 0 || weapon[GameTag.SILENCED] != 0
                || weapon[ReplaceHeroDamage] == 0) return false;
            weapon.Damage++;
            return true;
        }

        /// <summary>
        /// TRL_535 Snapjaw Shellfighter (phase 6 F10): "Whenever an adjacent minion takes damage, this minion takes it instead."
        /// The active Shellfighter next to <paramref name="damaged"/> (the left one when both neighbours are), or null. Read by
        /// Character.TakeDamage for a hit on a minion in play before its own prevention (Divine Shield, Immune); the redirected
        /// hit is dealt with outgoingApplied, so it is not redirected again.
        /// </summary>
        internal static Minion Shellfighter(Minion damaged)
        {
            if (!(damaged.Zone is Model.Zones.BoardZone board) || damaged.Card.Id == ShellfighterId) return null;
            bool any = false;
            foreach (Minion m in board) if (m.Card.Id == ShellfighterId) { any = true; break; }
            if (!any) return null;
            foreach (Minion m in damaged.GetAdjacentMinions())
                if (m.Card.Id == ShellfighterId && Active(m) && !m.IsDormant && m.Zone == board) return m;
            return null;
        }

        private const string ShellfighterId = "TRL_535";

        internal static Minion[] CleaveTargets(ICharacter source, ICharacter target)
        {
            // A hero cleaves while its weapon has Cleave (phase 6 F3: GILA_500t3 Blunderbuss, "Also damages the minions next to
            // whomever your hero attacks").
            bool weaponCleave = source is Hero hero && hero.Weapon != null && hero.Weapon.Card[Cleave] != 0 && hero.Weapon[GameTag.SILENCED] == 0;
            if (!(target is Minion defender) || (source[Cleave] == 0 || source[GameTag.SILENCED] > 0) && !weaponCleave)
                return Array.Empty<Minion>();
            return defender.GetAdjacentMinions();
        }
    }
}
