using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.Actions
{
	// Phase 8, family 17 (Dalaran Heist bosses D): the engine rules four of its encounters need.
	//   The Void (Zuramat the Obliterator): To The Void banishes a minion. It leaves its board through family 10's set-aside store
	//     (CanStore / Store), held by its own Voided enchantment (DALA_BOSS_48e, "In the Void."): one holder per banished minion,
	//     linked by the store's tag SetAsideStore. No Deathrattle, no death. Void Shift summons a fresh copy of every minion in the
	//     Void for its caster, in the order they were banished, and empties the Void. A run shrine never enters the Void.
	//   Extortion (Madam Goya): Blackmail moves a random card of the opponent's hand to that player's set-aside zone (the hand
	//     escrow of HandZone.DetachForEscrow: held modifiers stay, hand auras reattach when it returns) and gives that player an
	//     Extortion whose private tag ExtortionEscrow (32652, the tag table of EncounterRules.cs) holds the card's entity id;
	//     played, the Extortion returns the card itself to its player's hand. Nobody but that player ever learns the card: it was
	//     hidden in a hand and stays hidden in the set-aside zone, and the tag is private (never sent; the boss's fair view masks
	//     a hidden set-aside card).
	//   Spell Lock (Locksmith Zibb): a spell played Overloads its player for the Cost it was played for, through the engine's
	//     Overload bookkeeping, so Overload triggers see it.
	//   The Wire (Boommaster Flark): two wires touch when they are next to each other on a board.
	// Everything is board, zone and tag state, so clones and AI simulations replay it.
	public static partial class EncounterRules
	{
		/// <summary>DALA_BOSS_48e Voided ("In the Void."): the holder of one banished minion.</summary>
		public const string VoidedCard = "DALA_BOSS_48e";

		/// <summary>
		/// Banishes <paramref name="victim"/> to the Void: it gets its Voided holder and leaves its board for its own controller's
		/// set-aside zone as it is (no death, no Deathrattle; its triggers and auras stop). False, and nothing happens, when the
		/// set-aside store refuses it (a run shrine, a dormant or dying minion: <see cref="CanStore"/>).
		/// </summary>
		public static bool Banish(IPlayable source, Minion victim)
		{
			if (!CanStore(victim)) return false;
			Game game = victim.Game;
			Generic.AddEnchantmentBlock(game, Cards.FromId(VoidedCard), source, victim);
			Enchantment holder = victim.AppliedEnchantments?.LastOrDefault(e => e.Card.Id == VoidedCard && e[SetAsideStore] == 0);
			if (holder != null && Store(holder, victim)) return true;
			holder?.Remove();
			return false;
		}

		/// <summary>The minions in the Void now (both players' set-aside zones), in the order they were banished.</summary>
		public static List<Minion> VoidOf(Game game)
		{
			var found = new List<(int order, Minion minion)>();
			foreach (Controller c in new[] { game.Player1, game.Player2 })
				foreach (IPlayable p in c.SetasideZone)
				{
					if (!(p is Minion m) || m.AppliedEnchantments == null) continue;
					foreach (Enchantment holder in m.AppliedEnchantments)
						if (holder.Card.Id == VoidedCard && ReferenceEquals(StoredBy(holder), m)) found.Add((holder.Id, m));
				}
			return found.OrderBy(x => x.order).Select(x => x.minion).ToList();
		}

		/// <summary>
		/// DALA_BOSS_48t Void Shift, "Destroy all minions. Summon all minions from the Void.": every minion of both boards an
		/// effect can pick is destroyed (a run shrine goes dormant) and the deaths resolve; then a fresh copy of each minion in the
		/// Void is summoned for <paramref name="caster"/>, in banishing order, while its board has room, and the Void is empty
		/// (a minion that found no room is gone). Returns how many were summoned.
		/// </summary>
		public static int VoidShift(Controller caster, IPlayable source)
		{
			Game game = caster.Game;
			foreach (Minion m in caster.BoardZone.Concat(caster.Opponent.BoardZone).Where(m => !m.Untouchable && !m.ToBeDestroyed).ToList())
			{
				DeathContextRules.RecordDestroy(m, source);
				m.Destroy();
			}
			game.DeathProcessingAndAuraUpdate();
			var banished = VoidOf(game);
			foreach (Minion m in banished)
				foreach (Enchantment holder in m.AppliedEnchantments.Where(e => e.Card.Id == VoidedCard && e[SetAsideStore] == m.Id).ToList())
					holder[SetAsideStore] = 0;
			int summoned = 0;
			foreach (Minion m in banished)
			{
				if (game.State == State.COMPLETE || caster.BoardZone.IsFull) break;
				Generic.SummonBlock(game, (Minion)Entity.FromCard(caster, m.Card), -1, source);
				summoned++;
			}
			return summoned;
		}

		/// <summary>DALA_BOSS_59t3 Extortion.</summary>
		public const string ExtortionCard = "DALA_BOSS_59t3";

		/// <summary>
		/// DALA_BOSS_59p Blackmail, "Replace a random card in your opponent's hand with an 'Extortion.'": one card of
		/// <paramref name="boss"/>'s opponent's hand (the game RNG) goes to that player's set-aside zone (the hand escrow) and an
		/// Extortion that holds it takes its slot in that hand. Returns the Extortion, or null with an empty hand.
		/// </summary>
		public static IPlayable Blackmail(Controller boss, IEntity source)
		{
			Game game = boss.Game;
			Controller victim = boss.Opponent;
			if (victim.HandZone.Count == 0 || game.State == State.COMPLETE) return null;
			int pick = 0;
			if (victim.HandZone.Count > 1)
			{
				game.OnRandomHappened(true);
				pick = game.Random.Next(victim.HandZone.Count);
			}
			IPlayable card = victim.HandZone[pick];
			using (victim.HandZone.BeginCardStateMutation())
			{
				CardStateBoundary.EnrollAttachments(card);
				victim.HandZone.DetachForEscrow(card);
				victim.SetasideZone.Add(card);
			}
			IPlayable extortion = Entity.FromCard(victim, Cards.FromId(ExtortionCard));
			extortion[ExtortionEscrow] = card.Id;
			Generic.AddHandPhase(victim, extortion);
			// A replacement takes the replaced card's slot (as a transform keeps it): the Extortion enters at the right end
			// (the hand had room: one card just left) and moves left past the cards that followed the taken one.
			while (extortion.Zone == victim.HandZone && extortion.ZonePosition > pick)
				victim.HandZone.Swap(extortion, victim.HandZone[extortion.ZonePosition - 1]);
			return extortion;
		}

		/// <summary>The card an Extortion holds (in its player's set-aside zone), or null.</summary>
		public static IPlayable EscrowOf(IEntity extortion)
		{
			int id = extortion?[ExtortionEscrow] ?? 0;
			return id != 0 && extortion.Game.IdEntityDic.TryGetValue(id, out IPlayable p) && p.Zone != null
				&& p.Zone == p.Controller.SetasideZone ? p : null;
		}

		/// <summary>
		/// DALA_BOSS_59t3 Extortion, "Return your '{0}' to your hand.": the card it holds comes back to <paramref name="c"/>'s
		/// hand, itself, with what it held (its enchantments); the link is consumed. Returns the card, or null.
		/// </summary>
		public static IPlayable ReturnEscrow(Controller c, IEntity extortion)
		{
			IPlayable card = EscrowOf(extortion);
			if (extortion != null && extortion[ExtortionEscrow] != 0) extortion[ExtortionEscrow] = 0;
			if (card == null || card.Controller != c || c.Game.State == State.COMPLETE) return null;
			using (c.HandZone.BeginCardStateMutation())
			{
				c.SetasideZone.Remove(card);
				card[GameTag.ZONE] = (int)Zone.HAND;
				Generic.AddHandPhase(c, card);
			}
			return card.Zone == c.HandZone ? card : null;
		}

		/// <summary>LOE_018 Tunnel Trogg and its enchantment (LOE_018e: +Attack by its script number).</summary>
		private const string TunnelTrogg = "LOE_018", TunnelTroggBuff = "LOE_018e";

		/// <summary>
		/// DALA_BOSS_54p Spell Lock (and, since the wave-6 merge, family 22's ULDA_BOSS_63p Plaguebringer through
		/// DungeonRunEncounters.TombsD.Overload): <paramref name="c"/> Overloads <paramref name="amount"/> for <paramref name="spell"/>, as a
		/// card's own Overload does (Generic.OverloadBlock: Overload owed, the game's count, the Overload event). The engine's
		/// Tunnel Trogg ("Whenever you Overload, gain +1 Attack per locked Mana Crystal") answers the play of a card with printed
		/// Overload, not the Overload event, so it would never see this Overload: each Tunnel Trogg of <paramref name="c"/>'s
		/// board that is not silenced or dying gains <paramref name="amount"/> Attack here (its own LOE_018e, as its script
		/// gives it), the same as for a printed Overload of that amount.
		/// </summary>
		public static void OverloadFor(Controller c, IPlayable spell, int amount)
		{
			if (amount <= 0) return;
			c.OverloadOwed += amount;
			c.OverloadThisGame += amount;
			c.Game.TriggerManager.OnOverloadTrigger(spell, amount);
			foreach (Minion trogg in c.BoardZone.Where(m => m.Card.Id == TunnelTrogg && !m.IsSilenced && !m.ToBeDestroyed && !m.Untouchable).ToList())
				Generic.AddEnchantmentBlock(c.Game, Cards.FromId(TunnelTroggBuff), trogg, trogg, amount);
		}

		/// <summary>DALA_BOSS_58t The Wire and DALA_BOSS_58t2 The Other Wire.</summary>
		public const string TheWireCard = "DALA_BOSS_58t", TheOtherWireCard = "DALA_BOSS_58t2";

		/// <summary>The minion is one of the two wires.</summary>
		public static bool IsWire(IEntity m) => m != null && (m.Card.Id == TheWireCard || m.Card.Id == TheOtherWireCard);

		/// <summary>
		/// The wire <paramref name="wire"/> touches the other wire on its right (its next board neighbour is the other wire card):
		/// a touching pair is counted once, by its left wire.
		/// </summary>
		public static bool TouchesOnRight(Minion wire)
		{
			if (!IsWire(wire) || !(wire.Zone is Model.Zones.BoardZone board)) return false;
			int pos = wire.ZonePosition;
			if (pos < 0 || pos + 1 >= board.Count) return false;
			Minion next = board[pos + 1];
			return IsWire(next) && next.Card.Id != wire.Card.Id;
		}
	}
}
