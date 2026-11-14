using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.CardSets.Client.Sets;
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
	// Phase 8, family 11: Monster Hunt bosses B (The Witchwood). Thirteen encounters with a clickable hero power and the boss-only
	// cards they bring: Splintergraft (Grafted Bark), Brushwood Centurion (Survival of the Fittest, Hack), Baran the Blind
	// (Bulldoze), Gravekeeper Damph (Grave Mistake, the power and the spell), Doctor Sezavo (Hollow Needle), Garrow, the
	// Rancorous (Death and Taxes), Crooked Pete (Soot Up; he becomes Beastly Pete with Beast Within), Cragtorr (One with the
	// Trees), Azalina Soulthief (Unfinished Business, the Wisps, Soul Assimilation), Face Collector (A New Face), Raeth
	// Ghostsong (Desecrate, Shallow Graves), Grum (Terrify) and Gustave, the Gutripper (Cull the Meek). The texts are the
	// client's (facts/violet-hold-2026-09.json). Every power works on public zones (the boards, the heroes, the graveyards) or
	// on the boss's own hand and deck; a power that would do nothing is not offered, by a condition that reads only public
	// zones or the boss's own hand (never a deck: the boss's search samples its deck order). Every random choice uses the game
	// RNG and is reported as a random event; the only new state is the refresh mark of one private tag
	// (Actions.EncounterRules.PowerRefresh) and Crooked Pete's form index (the encounter model's forms). A run shrine never
	// changes sides, and a transform, a destroy or a shuffle aimed at one makes it go dormant, as from any such effect
	// (Actions.ShrineRules); a dead run shrine is never summoned again (ShrineRules.IsCorpse).
	public static partial class DungeonRunEncounters
	{
		/// <summary>GILA_BOSS_55p Unfinished Business: the 1/1 Wisp it summons (three at a time).</summary>
		public const string AzalinaWisp = "GILA_BOSS_55t";
		/// <summary>GILA_BOSS_57t Shallow Graves: how many minions that died this game it summons at most.</summary>
		public const int ShallowGravesCount = 7;
		/// <summary>Crooked Pete becomes Beastly Pete: the Armor Beastly Pete gains (his draw of 6 is the row's form "draw").</summary>
		public const int BeastlyPeteArmor = 15;

		/// <summary>
		/// GILA_BOSS_40p Grafted Bark: <paramref name="m"/> may be copied into <paramref name="c"/>'s hand (a minion whose card
		/// the run's generation filter lets that side be given: a copy is a generated card).
		/// </summary>
		public static bool CanGraft(Controller c, ICharacter m)
		{
			if (!(m is Minion minion) || c == null) return false;
			Game g = c.Game;
			using (g.GenerateFor(c)) return !g.BlocksGenerated(minion.Card);
		}

		/// <summary>
		/// GILA_BOSS_56p A New Face: the scripted minions <paramref name="m"/> may become (Cost = its card's Cost + 2, any class,
		/// behind the generation filter of the side that controls it).
		/// </summary>
		public static IReadOnlyList<Card> FacePool(Game g, Minion m)
		{
			if (m == null) return Array.Empty<Card>();
			int cost = m.Card.Cost + 2;
			return RuleMinions(g, m.Controller, m, "cost" + cost, card => card.Cost == cost);
		}

		/// <summary>
		/// GILA_BOSS_57t Shallow Graves: the minions of both players that died this game (the graveyards are public; a discarded
		/// or milled minion, a permanent and a dead run shrine are not "minions that died") whose card the generation filter lets
		/// <paramref name="c"/> be given, one entry per death, the caster's graveyard first.
		/// </summary>
		public static List<IPlayable> DiedThisGame(Controller c)
		{
			Game g = c.Game;
			var dead = new List<IPlayable>();
			using (g.GenerateFor(c))
				foreach (Controller side in new[] { c, c.Opponent })
					foreach (IPlayable p in side.GraveyardZone)
						if (p.ToBeDestroyed && p.Card.Type == CardType.MINION && !p.Card.Untouchable && !ShrineRules.IsCorpse(p) && !g.BlocksGenerated(p.Card))
							dead.Add(p);
			return dead;
		}

		/// <summary>GILA_BOSS_51p Death and Taxes: the minions the opponent of <paramref name="c"/> controls (dormant forms and permanents aside).</summary>
		public static int Taxed(Controller c) => Touchable(c.Opponent).Count();

		/// <summary>
		/// Crooked Pete (GILA_BOSS_52h), at the start of his turn at half his maximum Health or less: he becomes Beastly Pete,
		/// the row's script form (GILA_BOSS_52h2 with GILA_BOSS_52p2 Beast Within). As the wiki states it: his hand is removed
		/// (to the set-aside zone), his deck is replaced by Beastly Pete's and he draws 6 cards from it (EncounterRules.SwapForm
		/// with the form's deck and draw: the own-deck fact is recorded), and he gains 15 Armor. He keeps his Health and his
		/// maximum (the act curve's, not the portrait's printed 50). What the run player believed about his hand and deck no
		/// longer holds. Once per match (the form index). Returns whether he changed.
		/// </summary>
		public static bool BeastlyPete(Controller boss)
		{
			Game g = boss?.Game;
			RunEncounter enc = g?.EncounterOrNull;
			if (enc == null || g.State == State.COMPLETE || boss != g.Player2 || enc.BossForms.Count < 2 || boss[EncounterRules.FormIndex] != 0) return false;
			Hero hero = boss.Hero;
			if (hero.IsDead || hero.Health * 2 > hero.BaseHealth) return false;
			int max = hero.BaseHealth, damage = hero.Damage;
			foreach (IPlayable card in boss.HandZone.ToArray()) boss.SetasideZone.Add(boss.HandZone.Remove(card));
			EncounterRules.SwapForm(boss, 1);
			boss.Hero.BaseHealth = max;
			boss.Hero.Damage = damage;
			boss.Hero.GainArmor(boss.Hero, BeastlyPeteArmor);
			RunHelpers.BeliefReset(boss, RunBeliefZones.HandAndDeck);
			return true;
		}

		private static void F11()
		{
			var aMinion = new[] { PlayReq.REQ_TARGET_TO_PLAY, PlayReq.REQ_MINION_TARGET };
			var aFriendlyMinion = new[] { PlayReq.REQ_TARGET_TO_PLAY, PlayReq.REQ_MINION_TARGET, PlayReq.REQ_FRIENDLY_TARGET };
			bool AnyMinion(Controller c) => Touchable(c).Any() || Touchable(c.Opponent).Any();

			// ---------------- Splintergraft (GILA_BOSS_40h, 40 Health)
			// Grafted Bark (2): "Choose a minion. Add a 2/2 copy to your hand that costs (2)." Any minion of either board; the copy
			// is a fresh card of its kind (Petrified: Attack and Health set to 2, Cost set to 2). A minion whose card the boss may
			// never be given is not a legal choice; offered only while the hand has room.
			Add("GILA_BOSS_40p", EncounterCardKind.Power, () => new CardDef(Reqs(aMinion), new Power
			{
				PowerTask = new CustomTask((g, c, s, t, stack) =>
				{
					if (!(t is Minion m) || c.HandZone.IsFull || !CanGraft(c, m)) return;
					IPlayable copy = Entity.FromCard(c, m.Card, new EntityData { [GameTag.DISPLAYED_CREATOR] = s.Id });
					Generic.AddHandPhase.Invoke(c, copy);
					if (copy.Zone == c.HandZone) Generic.AddEnchantmentBlock(g, Cards.FromId("GILA_BOSS_40e"), (IPlayable)s, copy);
				}),
			}), card =>
			{
				card.SourceTargetingPredicate = (source, target) => CanGraft(source.Controller, target);
				card.PlayAvailabilityPredicate += (c, _) => !c.HandZone.IsFull;
			});
			Enchantment("GILA_BOSS_40e", () => new Power { Enchant = new Enchant(Effects.SetAttackHealth(2).Concat(new[] { Effects.SetCost(2) }).ToArray()) });

			// ---------------- Brushwood Centurion (GILA_BOSS_41h, 60 Health)
			// Survival of the Fittest (3): "All minions attack random enemy minions." Every minion of both boards, in play order,
			// attacks a random enemy minion (Actions.EncounterRules.AllAttackRandomEnemyMinions). Offered only while both boards
			// hold a minion.
			Power("GILA_BOSS_41p", null, () => new CustomTask((g, c, s, t, stack) => EncounterRules.AllAttackRandomEnemyMinions(g)),
				usable: c => Touchable(c).Any() && Touchable(c.Opponent).Any());
			// Hack (3): "Deal $1 damage to a minion. Then do it four more times." Five hits of 1 (Spell Damage on each) on the chosen
			// minion, each its own damage event; the hits stop once it is dead.
			BossCard("GILA_BOSS_41t", () => new CardDef(Reqs(aMinion), new Power
			{
				PowerTask = new CustomTask((g, c, s, t, stack) =>
				{
					for (int i = 0; i < 5; i++)
					{
						if (!(t is Minion m) || g.State == State.COMPLETE || m.Zone?.Type != Zone.PLAY || m.IsDead) break;
						new DamageTask(1, EntityType.TARGET, true).Process(g, c, s, t, stack);
					}
				}),
			}));

			// ---------------- Baran the Blind (GILA_BOSS_42h, 30 Health)
			// Bulldoze (1): "Spend all your armor. Deal that much damage to a random minion." The Armor is gone; the damage is one
			// hit on a random minion of either board. Offered only with Armor and a minion on a board.
			Power("GILA_BOSS_42p", null, () => new CustomTask((g, c, s, t, stack) =>
			{
				int armor = c.Hero.Armor;
				var minions = Touchable(c).Concat(Touchable(c.Opponent)).ToList();
				if (armor <= 0 || minions.Count == 0) return;
				c.Hero.Armor = 0;
				Minion pick = minions[0];
				if (minions.Count > 1)
				{
					pick = minions[g.Random.Next(minions.Count)];
					g.OnRandomHappened(true);
				}
				Generic.DamageCharFunc.Invoke((IPlayable)s, pick, armor, false);
			}), usable: c => c.Hero.Armor > 0 && AnyMinion(c));

			// ---------------- Gravekeeper Damph (GILA_BOSS_43h, 35 Health)
			// Grave Mistake (3, the power; the 0-Cost spell is the hunt's treasure): "Trigger all minions' Deathrattles twice."
			// Every minion of both boards, in play order, has each of its Deathrattles (its card's and its enchantments') resolve
			// twice for its own controller, as Feign Death does; a silenced minion's card Deathrattle stays quiet. The power is
			// offered only while a minion with a Deathrattle is on a board.
			Func<ISimpleTask> graveMistake = () => new CustomTask((g, c, s, t, stack) =>
			{
				foreach (Minion m in Touchable(c).Concat(Touchable(c.Opponent)).OrderBy(m => m.OrderOfPlay).ThenBy(m => m.Id).ToList())
					for (int i = 0; i < 2; i++)
						new ActivateDeathrattleTask(EntityType.TARGET).Process(g, c, s, m, stack);
			});
			Power("GILA_BOSS_43p", null, graveMistake, usable: c => Touchable(c).Concat(Touchable(c.Opponent)).Any(m => m.HasDeathrattle));
			BossCard("GILA_BOSS_43t", () => new CardDef(new Power { PowerTask = graveMistake() }));

			// ---------------- Doctor Sezavo (GILA_BOSS_44h, 20 Health)
			// Hollow Needle (2): "Deal $1 damage to a minion. If it dies, restore #3 Health to your hero." A hero power: no Spell
			// Damage. "Dies": the hit leaves it dead.
			Power("GILA_BOSS_44p", aMinion, () => new CustomTask((g, c, s, t, stack) =>
			{
				if (!(t is Minion m)) return;
				new DamageTask(1, EntityType.TARGET).Process(g, c, s, t, stack);
				if (m.IsDead) new HealTask(3, EntityType.HERO).Process(g, c, s, t, stack);
			}));

			// ---------------- Garrow, the Rancorous (GILA_BOSS_51h, 40 Health)
			// Death and Taxes (1): "Deal 1 damage to your opponent for each minion they control." One hit of that many. Offered only
			// while the opponent controls a minion.
			Power("GILA_BOSS_51p", null, () => new CustomTask((g, c, s, t, stack) =>
			{
				int n = Taxed(c);
				if (n > 0) Generic.DamageCharFunc.Invoke((IPlayable)s, c.Opponent.Hero, n, false);
			}), usable: c => Taxed(c) > 0);

			// ---------------- Crooked Pete (GILA_BOSS_52h, 40 Health) / Beastly Pete (GILA_BOSS_52h2)
			// Soot Up (1): "Give a friendly minion Stealth until your next turn." (Covered in Soot; a minion that has Stealth already
			// is not a choice.) Its start-of-turn trigger is Pete's change into Beastly Pete (BeastlyPete above), so it is started
			// with the power (EncounterRules.ActivatePower) and stops when the form replaces the power.
			Add("GILA_BOSS_52p", EncounterCardKind.Power, () => new CardDef(Reqs(aFriendlyMinion), new Power
			{
				PowerTask = new AddEnchantmentTask("GILA_BOSS_52e2", EntityType.TARGET),
				Trigger = new Trigger(TriggerType.TURN_START) { SingleTask = new CustomTask((g, c, s, t, stack) => BeastlyPete(c)) },
			}), card => card.SourceTargetingPredicate = (source, target) => target is Minion m && !m.HasStealth, activate: true);
			Enchantment("GILA_BOSS_52e2", () => SabberStoneCore.Enchants.Power.OneTurnStealthEnchantmentPower);
			// Beast Within (1): "Give your minions +1 Attack." (Beast Mode, for good.) Offered only while the boss has a minion.
			Power("GILA_BOSS_52p2", null, () => new CustomTask((g, c, s, t, stack) =>
			{
				foreach (Minion m in Touchable(c).ToList())
					Generic.AddEnchantmentBlock(g, Cards.FromId("GILA_BOSS_52e"), (IPlayable)s, m);
			}), usable: c => Touchable(c).Any());
			Enchantment("GILA_BOSS_52e", () => new Power { Enchant = new Enchant(Effects.Attack_N(1)) });

			// ---------------- Cragtorr (GILA_BOSS_54h, 40 Health)
			// One with the Trees (2): "Reduce the cost of minions in your hand by (1)." Every minion the boss holds (Assimilation).
			// Offered only while it holds a minion that costs (1) or more.
			Power("GILA_BOSS_54p", null, () => new CustomTask((g, c, s, t, stack) =>
			{
				foreach (IPlayable p in c.HandZone.Where(p => p is Minion).ToList())
					Generic.AddEnchantmentBlock(g, Cards.FromId("GILA_BOSS_54e"), (IPlayable)s, p);
			}), usable: c => c.HandZone.Any(p => p is Minion && p.Cost > 0));
			Enchantment("GILA_BOSS_54e", () => new Power { Enchant = new Enchant(Effects.ReduceCost(1)) });

			// ---------------- Azalina Soulthief (GILA_BOSS_55h, 50 Health)
			// Unfinished Business (2): "Summon three 1/1 Wisps." As many as the board has room for; needs a free slot.
			Power("GILA_BOSS_55p", new[] { PlayReq.REQ_NUM_MINION_SLOTS }, () => new CustomTask((g, c, s, t, stack) =>
			{
				for (int i = 0; i < 3 && !c.BoardZone.IsFull && g.State != State.COMPLETE; i++)
					Generic.SummonBlock(g, (Minion)Entity.FromCard(c, Cards.FromId(AzalinaWisp)), -1, s);
			}));
			// Wisp: a vanilla 1/1.
			Token(AzalinaWisp);
			// Soul Assimilation (4): "Destroy your Wisps. Gain control of a random enemy minion for each Wisp destroyed." The
			// caster's Wisps (every card the client names Wisp: EMERALD_DREAM Divination's list) are destroyed and their deaths
			// resolve; then, once per Wisp, a random enemy minion a control change can take changes sides (one taken while the
			// caster's board is full is destroyed: the engine's control rule). A run shrine never changes sides.
			BossCard("GILA_BOSS_55t2", () => new CardDef(new Power
			{
				PowerTask = new CustomTask((g, c, s, t, stack) =>
				{
					var wisps = Touchable(c).Where(m => Divination.Wisp(m)).ToList();
					if (wisps.Count == 0) return;
					foreach (Minion w in wisps)
					{
						DeathContextRules.RecordDestroy(w, s);
						w.Destroy();
					}
					g.DeathProcessingAndAuraUpdate();
					for (int i = 0; i < wisps.Count && g.State != State.COMPLETE; i++)
					{
						var pool = Charmable(c, -1);
						if (pool.Count == 0) break;
						Minion pick = pool[0];
						if (pool.Count > 1)
						{
							pick = pool[g.Random.Next(pool.Count)];
							g.OnRandomHappened(true);
						}
						new ControlTask(EntityType.TARGET).Process(g, c, s, pick, stack);
					}
				}),
			}));

			// ---------------- Face Collector (GILA_BOSS_56h, 60 Health)
			// A New Face (0): "Transform a minion into a random one that costs (2) more." Any minion of either board becomes a random
			// scripted minion whose Cost is its card's Cost + 2 (any class, behind the generation filter of its controller's side);
			// a minion with no such minion is not a choice. A run shrine chosen is destroyed instead and goes dormant (the transform
			// rule of Actions.Generic).
			Add("GILA_BOSS_56p", EncounterCardKind.Power, () => new CardDef(Reqs(aMinion), new Power
			{
				PowerTask = new CustomTask((g, c, s, t, stack) =>
				{
					if (!(t is Minion m)) return;
					var pool = FacePool(g, m);
					if (pool.Count == 0) return;
					g.OnRandomHappened(true);
					Generic.TransformBlock.Invoke(m.Controller, pool[g.Random.Next(pool.Count)], m);
				}),
			}), card => card.SourceTargetingPredicate = (source, target) => target is Minion m && FacePool(source.Game, m).Count > 0);

			// ---------------- Raeth Ghostsong (GILA_BOSS_57h, 60 Health)
			// Desecrate (1): "Deal $1 damage to all minions. If any die, refresh this." A hero power: no Spell Damage. When a hit
			// leaves a minion dead the use marks the power, and its own Inspire trigger (after the engine exhausted it) readies it
			// again (Actions.EncounterRules.MarkRefresh / RefreshIfMarked): each use still costs its Mana. Offered only while a
			// minion is on a board.
			Add("GILA_BOSS_57p", EncounterCardKind.Power, () => new CardDef(new Power
			{
				PowerTask = new CustomTask((g, c, s, t, stack) =>
				{
					var all = Touchable(c.Opponent).Concat(Touchable(c)).ToList();
					foreach (Minion m in all)
						if (g.State != State.COMPLETE) Generic.DamageCharFunc.Invoke((IPlayable)s, m, 1, false);
					if (all.Any(m => m.IsDead)) EncounterRules.MarkRefresh(s);
				}),
				Trigger = new Trigger(TriggerType.INSPIRE)
				{
					TriggerSource = TriggerSource.FRIENDLY,
					SingleTask = new CustomTask((g, c, s, t, stack) => EncounterRules.RefreshIfMarked(s)),
				},
			}), card => card.PlayAvailabilityPredicate += (c, _) => AnyMinion(c), activate: true);
			// Shallow Graves (9): "Summon 7 random minions that died this game." Fresh minions of up to seven random deaths of
			// either player this game (one per death; DiedThisGame above: never a dead run shrine), for the caster, as long as its
			// board has room.
			BossCard("GILA_BOSS_57t", () => new CardDef(new Power
			{
				PowerTask = new CustomTask((g, c, s, t, stack) =>
				{
					var dead = DiedThisGame(c);
					for (int n = 0; n < ShallowGravesCount && dead.Count > 0 && !c.BoardZone.IsFull && g.State != State.COMPLETE; n++)
					{
						int k = 0;
						if (dead.Count > 1)
						{
							k = g.Random.Next(dead.Count);
							g.OnRandomHappened(true);
						}
						Card card = dead[k].Card;
						dead.RemoveAt(k);
						Generic.SummonBlock.Invoke(g, (Minion)Entity.FromCard(c, card, new EntityData { [GameTag.DISPLAYED_CREATOR] = s.Id }), -1, s);
					}
				}),
			}));

			// ---------------- Grum (GILA_BOSS_58h, 40 Health)
			// Terrify (4): "Shuffle a minion into its owner's deck. It costs (4) less." Any minion of either board goes into its own
			// controller's deck as Recycle's returns do (the engine's MoveToDeck: the card itself, reset as that task resets it, its
			// damage gone), then gets Terrified there (Costs (4) less). Not a choice while that deck is full. A run shrine chosen is destroyed
			// instead and goes dormant (ShrineRules.InterceptLeave); it never enters a deck. The minion was public: both sides know
			// the card that entered the deck.
			Add("GILA_BOSS_58p", EncounterCardKind.Power, () => new CardDef(Reqs(aMinion), new Power
			{
				PowerTask = new CustomTask((g, c, s, t, stack) =>
				{
					if (!(t is Minion m) || m.Controller.DeckZone.IsFull) return;
					Controller owner = m.Controller;
					new MoveToDeck(EntityType.TARGET, opponent: owner != c).Process(g, c, s, m, stack);
					if (m.Zone == owner.DeckZone) Generic.AddEnchantmentBlock(g, Cards.FromId("GILA_BOSS_58e"), (IPlayable)s, m);
				}),
			}), card => card.SourceTargetingPredicate = (source, target) => target is Minion m && !m.Controller.DeckZone.IsFull);
			Enchantment("GILA_BOSS_58e", () => new Power { Enchant = new Enchant(Effects.ReduceCost(4)) });

			// ---------------- Gustave, the Gutripper (GILA_BOSS_62h, 50 Health)
			// Cull the Meek (1): "Destroy a minion with the lowest Attack." Among the minions of both boards an effect can pick, the
			// lowest Attack; a tie is broken by the game RNG. Offered only while a minion is on a board.
			Power("GILA_BOSS_62p", null, () => new CustomTask((g, c, s, t, stack) =>
			{
				var all = Touchable(c).Concat(Touchable(c.Opponent)).ToList();
				if (all.Count == 0) return;
				int low = all.Min(m => m.AttackDamage);
				var meek = all.Where(m => m.AttackDamage == low).ToList();
				Minion pick = meek[0];
				if (meek.Count > 1)
				{
					pick = meek[g.Random.Next(meek.Count)];
					g.OnRandomHappened(true);
				}
				DeathContextRules.RecordDestroy(pick, s);
				pick.Destroy();
			}), usable: AnyMinion);
		}
	}
}
