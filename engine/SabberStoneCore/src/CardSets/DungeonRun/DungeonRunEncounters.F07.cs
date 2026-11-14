using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Conditions;
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
	// Phase 8, family 07: the Dungeon Run bosses with a simple clickable hero power (Kobolds and Catacombs, levels 1-7).
	// Eighteen powers on their own client ids, built from existing tasks; the texts are the client's
	// (facts/violet-hold-2026-09.json). Every power works on public zones (the boards, the heroes) or on the boss's own hand,
	// every random choice uses the game RNG and reports a random event, and no power keeps state of its own. A run player's
	// board shrine is never transformed, bounced or copied as a shrine: the transform and bounce rules of Actions.Generic
	// (ShrineRules.InterceptTransform / InterceptLeave) destroy an awake shrine instead (it goes dormant) and leave a dormant
	// form alone, a copy of a shrine is a plain minion (ShrineRules.ClearMark), and a destroyed shrine dies as any minion does.
	public static partial class DungeonRunEncounters
	{
		/// <summary>The minions of <paramref name="c"/>'s board an effect can pick: on the board, not dormant, not already dying.</summary>
		private static IEnumerable<Minion> Touchable(Controller c) => c.BoardZone.Where(m => !m.Untouchable && !m.ToBeDestroyed);

		/// <summary>
		/// LOOTA_BOSS_05p Unstable Explosion / LOOTA_BOSS_28p Unstable Demolition: "Deal N damage to K random enemies." K
		/// different enemy characters (the hero and the minions an effect can pick), chosen first, then damaged in board order
		/// (hero first); with K or fewer enemies each is hit once. A hero power: no Spell Damage.
		/// </summary>
		internal static ISimpleTask DamageRandomEnemies(int count, int damage) => new CustomTask((g, c, s, t, stack) =>
		{
			var pool = new List<ICharacter> { c.Opponent.Hero };
			pool.AddRange(Touchable(c.Opponent));
			if (pool.Count > count)
			{
				// A partial shuffle by the game RNG, then the picks back in board order.
				var order = Enumerable.Range(0, pool.Count).ToList();
				var picked = new List<int>();
				for (int i = 0; i < count; i++)
				{
					int k = g.Random.Next(order.Count);
					picked.Add(order[k]);
					order.RemoveAt(k);
				}
				g.OnRandomHappened(true);
				picked.Sort();
				pool = picked.Select(i => pool[i]).ToList();
			}
			foreach (ICharacter victim in pool) Generic.DamageCharFunc.Invoke((IPlayable)s, victim, damage, false);
		});

		/// <summary>
		/// LOOTA_BOSS_21p Swallow Whole: the minions with the highest Attack among those of both boards an effect can pick (the
		/// power's owner's board first, each in board order); empty with no such minion.
		/// </summary>
		public static List<Minion> HighestAttack(Controller c)
		{
			var minions = Touchable(c).Concat(Touchable(c.Opponent)).ToList();
			if (minions.Count == 0) return minions;
			int top = minions.Max(m => m.AttackDamage);
			return minions.Where(m => m.AttackDamage == top).ToList();
		}

		/// <summary>
		/// The scripted minions of <paramref name="cost"/> a transform may turn <paramref name="target"/> into: the format's
		/// minions of any class behind the run's generation filter of the power's owner and of the minion's owner
		/// (Game.GenerateFor: a card either side excludes is never made on that side's board); distinct, ordinal by id.
		/// </summary>
		public static IReadOnlyList<Card> EvolvePool(Game g, IEntity source, Minion target, int cost)
		{
			using (g.GenerateFor(target.Controller))
				return RandomCardTask.GetCardList(source, CardType.MINION)
					.Where(card => card.Implemented && card.Type == CardType.MINION && card.Cost == cost)
					.GroupBy(card => card.Id, StringComparer.Ordinal).Select(x => x.First()).OrderBy(card => card.Id, StringComparer.Ordinal).ToArray();
		}

		/// <summary>
		/// LOOTA_BOSS_06p Evolvomancy / LOOTA_BOSS_29p Greater Evolution: "Transform a minion into a random one that costs (N)
		/// more." Evolve's rule on one minion of either board: the printed Cost of its card plus N; with no scripted minion of
		/// that Cost it stays as it is. A run shrine is destroyed instead (an awake one goes dormant; a dormant form stays).
		/// </summary>
		internal static ISimpleTask Evolve(int more) => new CustomTask((g, c, s, t, stack) =>
		{
			if (!(t is Minion target) || !(target.Zone is BoardZone)) return;
			if (ShrineRules.InterceptTransform(target)) return;
			var pool = EvolvePool(g, s, target, target.Card.Cost + more);
			if (pool.Count == 0) return;
			g.OnRandomHappened(true);
			Generic.ChangeEntityBlock.Invoke(c, target, pool[g.Random.Next(pool.Count)], true);
		});

		private static void F07()
		{
			var aMinion = new[] { PlayReq.REQ_TARGET_TO_PLAY, PlayReq.REQ_MINION_TARGET };
			var aFriendlyMinion = new[] { PlayReq.REQ_TARGET_TO_PLAY, PlayReq.REQ_MINION_TARGET, PlayReq.REQ_FRIENDLY_TARGET };
			var aMinionAndRoom = new[] { PlayReq.REQ_TARGET_TO_PLAY, PlayReq.REQ_MINION_TARGET, PlayReq.REQ_NUM_MINION_SLOTS };

			// ---------------- Waxmancer Sturmi (LOOTA_BOSS_04h, 30 Health; LOOTA_BOSS_27h, 50 Health)
			// Sculpt Wax (1): "Summon a 1/1 copy of a minion." A copy of a minion of either board on the boss's board (its
			// enchantments copied, as Herald Volazj's copies), then set to 1/1 by Waxed. Needs a free board slot.
			Power("LOOTA_BOSS_04p", aMinionAndRoom, () => ComplexTask.Create(
				new CopyTask(EntityType.TARGET, Zone.PLAY, addToStack: true),
				new AddEnchantmentTask("LOOTA_BOSS_04e", EntityType.STACK)));
			Enchantment("LOOTA_BOSS_04e", () => new Power { Enchant = new Enchant(Effects.SetAttackHealth(1)) });
			// Harden Sculpture (3): "Summon a copy of a minion."
			Power("LOOTA_BOSS_27p", aMinionAndRoom, () => new CopyTask(EntityType.TARGET, Zone.PLAY));

			// ---------------- Pathmaker Hamm (LOOTA_BOSS_05h, 20 Health; LOOTA_BOSS_28h, 60 Health)
			// Unstable Explosion (1): "Deal 1 damage to two random enemies." Unstable Demolition (2): "Deal 2 damage to three random enemies."
			Power("LOOTA_BOSS_05p", null, () => DamageRandomEnemies(2, 1));
			Power("LOOTA_BOSS_28p", null, () => DamageRandomEnemies(3, 2));

			// ---------------- Blackseed (LOOTA_BOSS_06h, 40 Health; LOOTA_BOSS_29h, 60 Health)
			// Evolvomancy (0): "Transform a minion into a random one that costs (1) more." Greater Evolution (1): "(3) more."
			Power("LOOTA_BOSS_06p", aMinion, () => Evolve(1));
			Power("LOOTA_BOSS_29p", aMinion, () => Evolve(3));

			// ---------------- Frostfur (LOOTA_BOSS_09h). Freeze (2): "Freeze a minion."
			Power("LOOTA_BOSS_09p", aMinion, () => ComplexTask.Freeze(EntityType.TARGET));

			// ---------------- Graves the Cleric (LOOTA_BOSS_10h). Light's Will (0): "Restore #2 Health to all minions."
			Power("LOOTA_BOSS_10p", null, () => new HealTask(2, EntityType.ALLMINIONS));

			// ---------------- Overseer Mogark (LOOTA_BOSS_11h, 20 Health; LOOTA_BOSS_30h, 60 Health)
			// Cruel Words (1): "Deal 1 damage to a friendly minion and give it +2 Attack." (Cruel Taskmaster's rule on the boss's
			// own minion.) Searing Lash (1): "... and give it +5 Attack."
			Power("LOOTA_BOSS_11p", aFriendlyMinion, () => ComplexTask.Create(
				new DamageTask(1, EntityType.TARGET),
				new AddEnchantmentTask("LOOTA_BOSS_11e", EntityType.TARGET)));
			Enchantment("LOOTA_BOSS_11e", () => new Power { Enchant = new Enchant(Effects.Attack_N(2)) });
			Power("LOOTA_BOSS_30p", aFriendlyMinion, () => ComplexTask.Create(
				new DamageTask(1, EntityType.TARGET),
				new AddEnchantmentTask("LOOTA_BOSS_30e", EntityType.TARGET)));
			Enchantment("LOOTA_BOSS_30e", () => new Power { Enchant = new Enchant(Effects.Attack_N(5)) });

			// ---------------- Candlebeard (LOOTA_BOSS_12h; his Rank 2 passive LOOTA_BOSS_31p is family 08's)
			// Charge! (1): "Give a minion Charge." Any minion; one summoned this turn can attack at once.
			Power("LOOTA_BOSS_12p", aMinion, () => new AddEnchantmentTask("LOOTA_BOSS_12e", EntityType.TARGET));
			Enchantment("LOOTA_BOSS_12e", () => new Power { Enchant = new Enchant(Effects.Charge) });

			// ---------------- Thaddock the Thief (LOOTA_BOSS_13h). Tactical Retreat (1): "Return a friendly minion to your hand."
			Power("LOOTA_BOSS_13p", aFriendlyMinion, () => new ReturnHandTask(EntityType.TARGET));

			// ---------------- Giant Rat (LOOTA_BOSS_18h). Rat Race (2): "Summon two 1/1 Rats." Needs a free board slot.
			Power("LOOTA_BOSS_18p", new[] { PlayReq.REQ_NUM_MINION_SLOTS }, () => new SummonTask("LOOTA_BOSS_18t", 2));
			Token("LOOTA_BOSS_18t");

			// ---------------- Gnosh the Greatworm (LOOTA_BOSS_21h)
			// Swallow Whole (2): "Destroy the minion with the highest Attack." Of both boards; among several one at random. Offered
			// only while a minion it can destroy is on a board (the boards are public: the offer is exact in the boss's view).
			Power("LOOTA_BOSS_21p", null, () => new CustomTask((g, c, s, t, stack) =>
			{
				var top = HighestAttack(c);
				if (top.Count == 0) return;
				Minion victim = top[0];
				if (top.Count > 1)
				{
					victim = top[g.Random.Next(top.Count)];
					g.OnRandomHappened(true);
				}
				DeathContextRules.RecordDestroy(victim, s);
				victim.Destroy();
			}), usable: c => Touchable(c).Any() || Touchable(c.Opponent).Any());

			// ---------------- Bristlesnarl (LOOTA_BOSS_23h)
			// Hunter's Call (3): "Reduce the Cost of cards in your hand by (1)." The client has no enchantment card for it: the
			// reduction is Emperor Thaurissan's Imperial Favor (BRM_028e, "Costs (1) less"), one per use on each card held.
			Power("LOOTA_BOSS_23p", null, () => new AddEnchantmentTask("BRM_028e", EntityType.HAND));

			// ---------------- The Mothergloop (LOOTA_BOSS_24h). Gloop (2): "Give minions in your hand +1/+1."
			Power("LOOTA_BOSS_24p", null, () => ComplexTask.Create(
				new IncludeTask(EntityType.HAND),
				new FilterStackTask(SelfCondition.IsMinion),
				new AddEnchantmentTask("LOOTA_BOSS_24e", EntityType.STACK)));
			Enchantment("LOOTA_BOSS_24e", () => new Power { Enchant = new Enchant(Effects.AttackHealth_N(1)) });
			// Growing Ooze (ICCA07_004, a Frozen Throne adventure card The Mothergloop's deck lists): "At the start of your turn,
			// gain +1/+1." (Shade of Naxxramas's rule; its enchantment is Grow, ICCA07_004e.)
			BossCard("ICCA07_004", () => new CardDef(new Power
			{
				Trigger = new Trigger(TriggerType.TURN_START) { SingleTask = new AddEnchantmentTask("ICCA07_004e", EntityType.SOURCE) },
			}));
			Enchantment("ICCA07_004e", () => new Power { Enchant = new Enchant(Effects.AttackHealth_N(1)) });

			// ---------------- Wee Whelp (LOOTA_BOSS_44h). Baby Breath (2): "Deal 2 damage." Any character.
			Power("LOOTA_BOSS_44p", new[] { PlayReq.REQ_TARGET_TO_PLAY }, () => new DamageTask(2, EntityType.TARGET));

			// ---------------- Bink the Burglar (LOOTA_BOSS_45h). Coin (0): "Gain 1 Mana Crystal this turn only."
			Power("LOOTA_BOSS_45p", null, () => new TempManaTask(1));
		}
	}
}
