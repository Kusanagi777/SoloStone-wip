using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Auras;
using SabberStoneCore.Conditions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.src.Loader;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.DungeonRun
{
	// Phase 8, family 08: Dungeon Run bosses B. Seventeen encounters whose powers are passives, triggers and both-player
	// effects, and the boss deck cards they bring: the tag team of George and Karl (LOOTA_BOSS_15h / 32h, LOOTA_BOSS_110),
	// Spiritspeaker Azun, Battlecrier Jin'zo, Voodoomaster Vex (all Deathrattles / Battlecries twice, for BOTH players), the
	// two Gutmooks, Candlebeard, Ixlid, Lyris, Kraxx, Seriona, Whompwhisker, Elder Jari, Jeeru, Fungalmancer Flurgl, the
	// Lava-Filled Chamber and Russell the Bard. The texts are the client's (facts/violet-hold-2026-09.json). A clickable power
	// that would do nothing is not offered; each such condition reads only public boards or the owner's own zone counts.
	public static partial class DungeonRunEncounters
	{
		/// <summary>
		/// "Poisonous to heroes" (LOOTA_105 Deadly Spore): whenever its minion deals damage to a hero, that hero is destroyed.
		/// Damage, not an attack: a hit Armor absorbed still counts, a hit an Immune hero ignored does not (no damage event). The
		/// ordinary Poisonous keyword (minions) stays the card's own tag.
		/// </summary>
		public static Trigger PoisonousToHeroes() => new Trigger(TriggerType.DEAL_DAMAGE)
		{
			TriggerSource = TriggerSource.SELF,
			Condition = new SelfCondition(p => p.Game.CurrentEventData?.EventTarget is Hero),
			SingleTask = new DestroyTask(EntityType.EVENT_TARGET),
		};

		/// <summary>
		/// One Recruit for <paramref name="owner"/>: a random minion of its deck is summoned (none with a full board or no minion
		/// left). The pulled card becomes a public board minion by a public summon; nothing else of the deck is shown.
		/// </summary>
		public static Minion RecruitOne(Controller owner, IEntity source)
		{
			Game g = owner.Game;
			if (owner.BoardZone.IsFull) return null;
			var minions = owner.DeckZone.Where(p => p is Minion).ToList();
			if (minions.Count == 0) return null;
			if (minions.Count > 1) g.OnRandomHappened(true);
			var pick = (Minion)minions[g.Random.Next(minions.Count)];
			Generic.RemoveFromZone.Invoke(owner, pick);
			Generic.SummonBlock.Invoke(g, pick, -1, source);
			return pick.Zone == owner.BoardZone ? pick : null;
		}

		private static void F08()
		{
			// ---------------- George and Karl (LOOTA_BOSS_15h) / Karl and George (LOOTA_BOSS_32h): one tag-team encounter
			// Divinity: "Give all your minions Divine Shield." Not offered when every minion of the boss has one already.
			Power("LOOTA_BOSS_15p", null, () => new SetGameTagTask(GameTag.DIVINE_SHIELD, 1, EntityType.MINIONS),
				usable: c => c.BoardZone.Any(m => !m.HasDivineShield && !m.Untouchable));
			// Karl's power is the client's AT_132_PALADIN The Silver Hand (TgtCardsGen), named by the row's form.
			// Tag Team: "Swap George and Karl." The benched hero comes in with its own Health (EncounterRules.TagTeam).
			BossCard("LOOTA_BOSS_110", () => new CardDef(new Power { PowerTask = new CustomTask((g, c, s, t, stack) => EncounterRules.TagTeam(c)) }));

			// ---------------- the three totems: passive, and for BOTH players ("All ...")
			// Spiritspeaker Azun (LOOTA_BOSS_16h). Totem of the Dead: "All Deathrattles trigger twice." Baron Rivendare's rule on
			// each controller (DeathrattleRules reads the live aura), so it does not stack with a player's own doubling.
			Passive("LOOTA_BOSS_16p", () => new Power
			{
				Aura = new Aura(AuraType.CONTROLLERS, new Effect(GameTag.EXTRA_DEATHRATTLES_BASE, EffectOperator.ADD, 1)),
			});
			// Battlecrier Jin'zo (LOOTA_BOSS_17h). Battle Totem: "All Battlecries trigger twice." Brann Bronzebeard's rule on each
			// controller (one more resolution, however many doublings a side holds).
			Passive("LOOTA_BOSS_17p", () => new Power
			{
				Aura = new Aura(AuraType.CONTROLLERS, new Effect(GameTag.EXTRA_BATTLECRIES_BASE, EffectOperator.ADD, 1)),
			});
			// Voodoomaster Vex (LOOTA_BOSS_38h). Totem of Chaos: "All Battlecries and Deathrattles trigger twice."
			Passive("LOOTA_BOSS_38p", () => new Power
			{
				Aura = new Aura(AuraType.CONTROLLERS, new Effect(GameTag.EXTRA_BATTLECRIES_BASE, EffectOperator.ADD, 1),
					new Effect(GameTag.EXTRA_DEATHRATTLES_BASE, EffectOperator.ADD, 1)),
			});

			// ---------------- Gutmook (LOOTA_BOSS_19h, 30 Health; LOOTA_BOSS_33h, 50 Health): the run player's spells feed him
			// Digest Magic: "Whenever your opponent casts a spell, summon a Tunnel Trogg." (Troggzor's rule: on the cast, before
			// the spell resolves; nothing with a full board.)
			Passive("LOOTA_BOSS_19p", () => new Power
			{
				Trigger = new Trigger(TriggerType.CAST_SPELL) { TriggerSource = TriggerSource.ENEMY, SingleTask = new SummonTask("LOE_018") },
			});
			// Metabolized Magic: "Whenever your opponent casts a spell, draw a card. It costs (1)." The boss's own draw; a card
			// burned by a full hand costs nothing.
			Passive("LOOTA_BOSS_33p", () => new Power
			{
				Trigger = new Trigger(TriggerType.CAST_SPELL)
				{
					TriggerSource = TriggerSource.ENEMY,
					SingleTask = new CustomTask((g, c, s, t, stack) =>
					{
						IPlayable drawn = Generic.Draw(c);
						if (drawn != null && drawn.Zone == c.HandZone) Effects.SetCost(1).ApplyTo(drawn);
					}),
				},
			});
			// Gluttonous Trogg (boss deck minion): "Whenever your opponent casts a spell, gain +1/+1."
			BossCard("LOOTA_109", () => new CardDef(new Power
			{
				Trigger = new Trigger(TriggerType.CAST_SPELL) { TriggerSource = TriggerSource.ENEMY, SingleTask = new AddEnchantmentTask("LOOTA_109e", EntityType.SOURCE) },
			}));
			Enchantment("LOOTA_109e", () => new Power { Enchant = new Enchant(Effects.AttackHealth_N(1)) });

			// ---------------- Candlebeard (LOOTA_BOSS_31h). Charrrrrge!: "After you play a minion, give it Charge."
			// Played from hand only (a summoned minion gets nothing); the minion must still be on the boss's board.
			Passive("LOOTA_BOSS_31p", () => new Power
			{
				Trigger = new Trigger(TriggerType.AFTER_PLAY_MINION)
				{
					TriggerSource = TriggerSource.FRIENDLY,
					SingleTask = new CustomTask((g, c, s, t, stack) =>
					{
						if (t is Minion m && m.Zone == c.BoardZone && !m.ToBeDestroyed) m.HasCharge = true;
					}),
				},
			});

			// ---------------- Ixlid (LOOTA_BOSS_36h). Sprouting Spore: "Summon an extremely Deadly Spore."
			Power("LOOTA_BOSS_36p", null, () => new SummonTask("LOOTA_105"), usable: c => !c.BoardZone.IsFull);
			// Deadly Spore: "Poisonous. Poisonous to heroes." (the keyword is the card's tag; the hero rule is the trigger)
			BossCard("LOOTA_105", () => new CardDef(new Power { Trigger = PoisonousToHeroes() }));

			// ---------------- Lyris the Wild Mage (LOOTA_BOSS_37h). Arcane Infusion: "Add 'Arcane Missiles' to your hand."
			Power("LOOTA_BOSS_37p", null, () => new AddCardTo("EX1_277", EntityType.HAND), usable: c => !c.HandZone.IsFull);

			// ---------------- Kraxx (LOOTA_BOSS_39h). Giant Stomp: "Deal 1 damage to all enemy minions."
			Power("LOOTA_BOSS_39p", null, () => new DamageTask(1, EntityType.OP_MINIONS), usable: c => c.Opponent.BoardZone.Any(m => !m.Untouchable));

			// ---------------- Seriona (LOOTA_BOSS_40h). Fading Light: "Give a minion -1 Attack." (any minion with Attack left)
			Add("LOOTA_BOSS_40p", EncounterCardKind.Power, () => new CardDef(
				new Dictionary<PlayReq, int> { { PlayReq.REQ_TARGET_TO_PLAY, 0 }, { PlayReq.REQ_MINION_TARGET, 0 }, { PlayReq.REQ_TARGET_MIN_ATTACK, 1 } },
				new Power { PowerTask = new AddEnchantmentTask("LOOTA_BOSS_40pe", EntityType.TARGET) }));
			Enchantment("LOOTA_BOSS_40pe", () => new Power { Enchant = new Enchant(Effects.Attack_N(-1)) });

			// ---------------- Whompwhisker (LOOTA_BOSS_41h). Join the Fray: "Both players Recruit a minion."
			// The boss first, then its opponent: each summons a random minion of its own deck (RecruitOne). Offered while either
			// board has room; whether a deck still holds a minion is not asked (a deck is not exact in the boss's view).
			Power("LOOTA_BOSS_41p", null, () => new CustomTask((g, c, s, t, stack) =>
			{
				RecruitOne(c, s);
				if (g.State != State.COMPLETE) RecruitOne(c.Opponent, s);
			}), usable: c => !c.BoardZone.IsFull || !c.Opponent.BoardZone.IsFull);

			// ---------------- Elder Jari (LOOTA_BOSS_42h). Mystic Barrier: "Gain 3 Armor."
			Power("LOOTA_BOSS_42p", null, () => new ArmorTask(3));

			// ---------------- Jeeru (LOOTA_BOSS_43h). Three Wee Wishes: "Each player draws 3 cards." The boss first, then its
			// opponent; an empty deck and a full hand follow the ordinary draw rules (fatigue, a burned card) on either side.
			Power("LOOTA_BOSS_43p", null, () => new CustomTask((g, c, s, t, stack) =>
			{
				for (int i = 0; i < 3 && g.State != State.COMPLETE; i++) Generic.Draw(c);
				for (int i = 0; i < 3 && g.State != State.COMPLETE; i++) Generic.Draw(c.Opponent);
			}));

			// ---------------- Fungalmancer Flurgl (LOOTA_BOSS_46h). Fungal Infection: "Give your minions +1/+1."
			Power("LOOTA_BOSS_46p", null, () => new AddEnchantmentTask("LOOTA_BOSS_46pe", EntityType.MINIONS), usable: c => c.BoardZone.Any(m => !m.Untouchable));
			Enchantment("LOOTA_BOSS_46pe", () => new Power { Enchant = new Enchant(Effects.AttackHealth_N(1)) });
			// Murloc Tinyfin (his deck lists two): a collectible 0-Cost 1/1 Murloc with no text and no script; the card database
			// has no entry for it, so it is registered here as a card that needs none.
			Token("LOEA10_3");

			// ---------------- Lava-Filled Chamber (LOOTA_BOSS_47h). The Floor is Lava: "After a minion is played, deal 2 damage
			// to it." Either player's minion, played from hand (a summoned one is untouched), if it is still on a board.
			Passive("LOOTA_BOSS_47p", () => new Power
			{
				Trigger = new Trigger(TriggerType.AFTER_PLAY_MINION)
				{
					TriggerSource = TriggerSource.ALL,
					SingleTask = new CustomTask((g, c, s, t, stack) =>
					{
						if (t is Minion m && m.Zone == m.Controller.BoardZone && !m.ToBeDestroyed && !m.Untouchable)
							new DamageTask(2, EntityType.TARGET).Process(g, c, s, t, stack);
					}),
				},
			});

			// ---------------- Russell the Bard (LOOTA_BOSS_51h). Alluring Tune: "Gain control of an enemy minion with 2 or less
			// Attack." A ControlTask on the target, so a run shrine is never a legal target (ShrineRules.TakesControlOfTarget);
			// it needs a free slot on the boss's board.
			Add("LOOTA_BOSS_51p", EncounterCardKind.Power, () => new CardDef(
				new Dictionary<PlayReq, int>
				{
					{ PlayReq.REQ_TARGET_TO_PLAY, 0 }, { PlayReq.REQ_MINION_TARGET, 0 }, { PlayReq.REQ_ENEMY_TARGET, 0 },
					{ PlayReq.REQ_TARGET_MAX_ATTACK, 2 }, { PlayReq.REQ_NUM_MINION_SLOTS, 1 },
				},
				new Power { PowerTask = new ControlTask(EntityType.TARGET) }));

			// ---------------- boss deck minions of the Dungeon Run (their decks: Trapped Room and the families 07 / 09)
			// Starving Crab: "At the start of your turn, deal 5 damage to your hero."
			BossCard("LOOTA_102", () => new CardDef(new Power
			{
				Trigger = new Trigger(TriggerType.TURN_START) { SingleTask = new DamageTask(5, EntityType.HERO) },
			}));
			// Reverberating Ooze: "After this minion survives damage, summon a copy of this minion." (the copy keeps the damage
			// and the enchantments, and reverberates in turn)
			BossCard("LOOTA_104", () => new CardDef(new Power
			{
				Trigger = new Trigger(TriggerType.TAKE_DAMAGE)
				{
					TriggerSource = TriggerSource.SELF,
					Condition = SelfCondition.IsNotDead,
					SingleTask = new SummonCopyTask(EntityType.SOURCE, false, false, SummonSide.RIGHT),
				},
			}));
			// Shroomsayer: "After this minion survives damage, destroy all minions." (both boards, itself included; a run shrine
			// goes dormant as from any destroy effect)
			BossCard("LOOTA_107", () => new CardDef(new Power
			{
				Trigger = new Trigger(TriggerType.TAKE_DAMAGE)
				{
					TriggerSource = TriggerSource.SELF,
					Condition = SelfCondition.IsNotDead,
					SingleTask = new DestroyTask(EntityType.ALLMINIONS),
				},
			}));
		}
	}
}
