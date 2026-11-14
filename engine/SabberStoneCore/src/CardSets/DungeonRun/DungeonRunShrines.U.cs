using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Auras;
using SabberStoneCore.Conditions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model.Zones;
using SabberStoneCore.src.Loader;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.DungeonRun
{
	/// <summary>
	/// Phase 7 U: the Rumble Run shrine upgrades whose grant sits on the shrine (TRLA_801 Sacred Ritual, TRLA_802 Favor of the
	/// Gods, TRLA_803 Fortified Spirit, TRLA_806 Wrath of the Loas, TRLA_807 Boon Reaper, TRLA_808 Roaring Edifice; TRLA_804 and
	/// TRLA_805 are in DungeonRunShrines.cs; TRLA_809 Windfall Blessing, phase 7 D, in DungeonRunShrines.D.cs). An upgrade is a
	/// run reward, never a hero power or a passive treasure: the run layer's shrine-upgrade draft adds it to
	/// DungeonRunState.ShrineUpgrades, and at match start
	/// <see cref="ApplyUpgrades"/> attaches its client "Player Enchant" record to the run player. Every run shrine of that
	/// player then carries the upgrade's grant, the client's shrine enchantment (<see cref="Grants"/>): the grant is attached
	/// when the shrine lands at Start of Game and again each time it revives (<see cref="Grant"/>, from ShrineRules), so a
	/// revived shrine (a fresh entity) has it back. Every grant is an entity in History=false games too (it owns an aura, a
	/// trigger or a Deathrattle), as the AI's simulations need. Silence removes the grants with the shrine's text until it revives. A copy
	/// of a shrine (Faceless Manipulator) is a plain minion: the grants never copy, and a copied grant would do nothing.
	/// </summary>
	public static partial class DungeonRunShrines
	{
		/// <summary>TRLA_801 Sacred Ritual: "Your Shrines have 'Your spells cost (1) less.'"</summary>
		public const string SacredRitual = "TRLA_801";
		/// <summary>TRLA_802 Favor of the Gods: "Minions adjacent to your Shrines have Taunt."</summary>
		public const string FavorOfTheGods = "TRLA_802";
		/// <summary>TRLA_803 Fortified Spirit: "At the start of each turn, restore your Shrines to full Health."</summary>
		public const string FortifiedSpirit = "TRLA_803";
		/// <summary>TRLA_806 Wrath of the Loas: "Your Shrines have 'Deathrattle: Destroy all enemy Shrines.'"</summary>
		public const string WrathOfTheLoas = "TRLA_806";
		/// <summary>TRLA_807 Boon Reaper: "Your Shrines have 'Deathrattle: Draw cards until your hand is full.'"</summary>
		public const string BoonReaper = "TRLA_807";
		/// <summary>TRLA_808 Roaring Edifice: "Your Shrines have 'Deathrattle: Cards in your hand cost (2) less.'"</summary>
		public const string RoaringEdifice = "TRLA_808";

		/// <summary>The client's SHRINE keyword tag (1057): set on every base shrine card, never on a dormant form.</summary>
		public const GameTag ShrineKeyword = (GameTag)1057;
		/// <summary>TRLA_808 Roaring Edifice's discount on each card in hand: the client's LOOT_204e "Costs (2) less."</summary>
		internal const string EdificeDiscount = "LOOT_204e";

		/// <summary>
		/// Each upgrade's player enchantment and the grant it puts on every run shrine of its owner (the client's own records:
		/// TRLA_801e "Sacred", TRLA_802e "Favored", TRLA_803e "Fortified", TRLA_806e2/807e2/808e2 the granted Deathrattles).
		/// </summary>
		public static readonly IReadOnlyDictionary<string, string> Grants = new Dictionary<string, string>(StringComparer.Ordinal)
		{
			["TRLA_801e2"] = "TRLA_801e", ["TRLA_802e2"] = "TRLA_802e", ["TRLA_803e2"] = "TRLA_803e",
			["TRLA_806e"] = "TRLA_806e2", ["TRLA_807e"] = "TRLA_807e2", ["TRLA_808e"] = "TRLA_808e2",
			// phase 7 D (DungeonRunShrines.D.cs): TRLA_809e Windfall Blessing Player Enchant -> TRLA_809e2 Windfall Glyph
			["TRLA_809e"] = "TRLA_809e2",
		};
		private static readonly HashSet<string> GrantIds = new HashSet<string>(Grants.Values, StringComparer.Ordinal);

		/// <summary>A shrine upgrade's grant (on a run shrine only: copies never carry one).</summary>
		public static bool IsGrant(string cardId) => cardId != null && GrantIds.Contains(cardId);

		/// <summary>
		/// Attaches the grants of its owner's shrine upgrades to an awake run shrine that just landed or revived, in the order the
		/// upgrades were applied (pick order). Called by ShrineRules (EnterFromSetaside, Place, Revive); a shrine whose owner holds
		/// no upgrade (every boss shrine) gets nothing.
		/// </summary>
		internal static void Grant(Minion shrine)
		{
			if (!ShrineRules.IsAwake(shrine)) return;
			Controller c = shrine.Controller;
			var applied = c.AppliedEnchantments;
			if (applied == null || applied.Count == 0) return;
			foreach (Enchantment upgrade in applied.ToArray())
				if (Grants.TryGetValue(upgrade.Card.Id, out string grant))
					Generic.AddEnchantmentBlock(c.Game, Cards.FromId(grant), upgrade, shrine);
		}

		// An awake, unsilenced run shrine: one that holds its owner's grants (Silence removes them until it revives).
		private static bool Favored(IPlayable p) => p is Minion m && ShrineRules.IsAwake(m) && !m.IsSilenced;

		/// <summary>
		/// TRLA_802 Favor of the Gods: <paramref name="p"/> is a minion in play (not a dormant form) next to an awake, unsilenced
		/// run shrine of its controller. Another shrine counts as a minion, so two shrines side by side (TRLA_804) taunt each other.
		/// </summary>
		internal static bool AdjacentToFavoredShrine(IPlayable p)
		{
			if (!(p is Minion m) || !(m.Zone is BoardZone board) || m.Untouchable) return false;
			int pos = m.ZonePosition;
			int count = board.HasLocations ? board.SlotCount : board.Count;
			IPlayable left = pos > 0 && pos - 1 < count ? board.GetSlot(pos - 1) : null;
			IPlayable right = pos + 1 < count ? board.GetSlot(pos + 1) : null;
			return Favored(left) || Favored(right);
		}

		/// <summary>
		/// TRLA_806 Wrath of the Loas's targets: the enemy Shrines in play, awake (a dormant form is not a Shrine: it has no SHRINE
		/// keyword and is untouchable). A Shrine is a minion whose card has the SHRINE keyword, a run shrine or a plain copy.
		/// </summary>
		public static IReadOnlyList<Minion> EnemyShrines(Controller c) =>
			c.Opponent.BoardZone.Slots.OfType<Minion>().Where(m => m.Card[ShrineKeyword] == 1 && !m.IsDormant && !m.Untouchable && !m.ToBeDestroyed).ToArray();

		private static void AddU(Dictionary<string, CardDef> cards)
		{
			// ---------------- the player enchantments ("... Player Enchant"): records of the upgrade on its owner; the grants do the work.
			// No aura of their own: Silence keeps an enchantment whose creator has an aura (Minion.Silence), and each grant's
			// creator is its player enchantment, so a silenced shrine loses every grant.
			cards.Add("TRLA_801e2", Implemented(new CardDef(new Power())));
			cards.Add("TRLA_802e2", Implemented(new CardDef(new Power())));
			cards.Add("TRLA_803e2", Implemented(new CardDef(new Power())));
			cards.Add("TRLA_806e", Implemented(new CardDef(new Power())));
			cards.Add("TRLA_807e", Implemented(new CardDef(new Power())));
			cards.Add("TRLA_808e", Implemented(new CardDef(new Power())));

			// ---------------- the grants on each run shrine
			// TRLA_801e Sacred: "Your spells cost (1) less." A hand aura owned by the grant: it lives while the shrine is awake and
			// unsilenced (death and Silence remove the grant, and its aura with it); two shrines (TRLA_804) are two auras, (2) less.
			cards.Add("TRLA_801e", Implemented(new CardDef(new Power
			{
				Aura = new Aura(AuraType.HAND, Effects.ReduceCost(1)) { Condition = SelfCondition.IsSpell },
			})));
			// TRLA_802e Favored: "Adjacent minions gain Taunt." A board aura owned by the grant that re-reads adjacency on every
			// update (Restless, as Ravitz's), so a minion that moves next to or away from the shrine gains or loses the Taunt at
			// once; the shrine's death or Silence removes the grant and the aura with it. With two shrines (TRLA_804) each grant
			// has its aura (a minion between them counts both), and two shrines side by side taunt each other.
			cards.Add("TRLA_802e", Implemented(new CardDef(new Power
			{
				Aura = new Aura(AuraType.BOARD, new Effect(GameTag.TAUNT, EffectOperator.ADD, 1))
				{
					Restless = true,
					Condition = new SelfCondition(AdjacentToFavoredShrine),
				},
			})));
			// TRLA_803e Fortified: "Restores to full Health each turn." At the start of each turn (either player's), a damaged
			// shrine carrying it is restored to full Health (an ordinary heal: healing triggers see it). A dormant form is not.
			cards.Add("TRLA_803e", Implemented(new CardDef(new Power
			{
				Trigger = new Trigger(TriggerType.TURN_START)
				{
					EitherTurn = true,
					SingleTask = new CustomTask((g, c, s, t, stack) =>
					{
						var shrine = t as Minion ?? (s as Enchantment)?.Target as Minion;
						if (shrine == null || !ShrineRules.IsAwake(shrine) || shrine.Damage <= 0 || g.State == State.COMPLETE) return;
						HealingRules.Heal(shrine, new ICharacter[] { shrine }, 0, full: true);
					}),
				},
			})));
			// The granted Deathrattles: they fire when the shrine dies (it goes dormant, ShrineRules), as its own Deathrattle would,
			// and the Deathrattle doublers repeat them (TaskProcessor, DeathrattleRules.Repeats). A copied grant (a plain copy of a
			// shrine) does nothing: the source must be a run shrine.
			// TRLA_806e2: "Deathrattle: Destroy all enemy Shrines." An enemy run shrine destroyed so goes dormant in turn.
			cards.Add("TRLA_806e2", Implemented(new CardDef(new Power
			{
				DeathrattleTask = new CustomTask((g, c, s, t, stack) =>
				{
					if (!ShrineRules.IsMarked(s) || g.State == State.COMPLETE) return;
					foreach (Minion enemy in EnemyShrines(c)) enemy.Destroy();
				}),
			})));
			// TRLA_807e2: "Deathrattle: Draw cards until your hand is full." (as LOOTA_823 Bag of Stuffing: an empty deck stops it).
			cards.Add("TRLA_807e2", Implemented(new CardDef(new Power
			{
				DeathrattleTask = new CustomTask((g, c, s, t, stack) =>
				{
					if (!ShrineRules.IsMarked(s) || g.State == State.COMPLETE) return;
					new EffectContext(g, c.Id, s.Id, stack: stack).DrawUntilHandFull();
				}),
			})));
			// TRLA_808e2: "Deathrattle: Reduce the cost of cards in your hand by (2)." Each card in the owner's hand then gets the
			// client's "Costs (2) less." (LOOT_204e; costs floor at (0)); cards drawn later do not.
			cards.Add("TRLA_808e2", Implemented(new CardDef(new Power
			{
				DeathrattleTask = new CustomTask((g, c, s, t, stack) =>
				{
					if (!ShrineRules.IsMarked(s) || !(s is IPlayable shrine) || g.State == State.COMPLETE) return;
					var discount = Cards.FromId(EdificeDiscount);
					foreach (IPlayable card in c.HandZone.ToArray()) Generic.AddEnchantmentBlock(g, discount, shrine, card);
				}),
			})));
		}
	}
}
