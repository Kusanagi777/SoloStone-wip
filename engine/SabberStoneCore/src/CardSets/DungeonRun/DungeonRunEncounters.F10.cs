using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Actions;
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
	// Phase 8, family 10: Monster Hunt bosses A (The Witchwood). Fifteen encounters with a clickable hero power and the nine
	// boss-only cards they bring: A Mangy Wolf, Rottooth, Cutthroat Willie, Grubb the Swampdrinker (Chuck, twice), Gnomenapper
	// (Nab, Stuffed Sack, Sack of Gnomes), Experiment 3C (Consume, Amalgamate, Amalgamation), Sazzmi Gentlehorn (Infatuation,
	// Enchanting Tune), Gnarlroot, Niira the Trickster, The Scarecrow (Harvest, Scythe), Inquisitor Hav'nixx, Chupacabran
	// (Bloodthirst, Vampiric Fangs), Griselda, Wharrgarbl (It's Raining Fin, Murloc Infestation) and Ravencaller Cozzlewurt.
	// The texts are the client's (facts/violet-hold-2026-09.json). Every power works on public zones (the boards, the heroes)
	// or on the boss's own hand and deck; a power that would do nothing is not offered, by a condition that reads only the
	// public boards and heroes or the boss's own hand (never a deck: the boss's search samples its deck order). Every random
	// choice uses the game RNG and is reported as a random event; the only state is the set-aside store's one private tag
	// (Actions.EncounterRules.SetAsideStore). A run player's board shrine is never put into a sack and never changes sides; a
	// transform or a destroy aimed at one makes it go dormant, as from any such effect (Actions.ShrineRules).
	public static partial class DungeonRunEncounters
	{
		/// <summary>GILA_BOSS_26p Nab: the Stuffed Sack that holds the minion (0/3, "Deathrattle: Return your stolen minion!").</summary>
		public const string StuffedSack = "GILA_BOSS_26t";
		/// <summary>GILA_BOSS_27t Amalgamate: the Amalgamation it summons (1/1, every minion type).</summary>
		public const string Amalgamation = "GILA_BOSS_27t2";
		/// <summary>GILA_BOSS_31p Splinter: the 2/2 Treant it summons (The Witchwood's own Treant token).</summary>
		public const string SplinterTreant = "GIL_663t";
		/// <summary>GILA_BOSS_36p Witch's Kiss: the 0/1 Frog with Taunt (Hex's Frog).</summary>
		public const string KissFrog = "hexfrog";

		/// <summary>
		/// GILA_BOSS_26t2 Sack of Gnomes, "Fill your board with random Gnomes": the client data names no pool, so it is the
		/// collectible minions that are Gnomes by name at the time of The Witchwood (Leper Gnome, Clockwork Gnome, Gnomish
		/// Inventor, Gnomish Experimenter, Gnomeregan Infantry, Gnomeferatu), a recorded adaptation.
		/// </summary>
		public static readonly IReadOnlyList<string> Gnomes = new[] { "CS2_147", "EX1_029", "GVG_082", "GVG_092", "GVG_098", "ICC_407" };

		/// <summary>The Gnomes Sack of Gnomes may summon for <paramref name="c"/>: the scripted ones the run's generation filter allows.</summary>
		public static IReadOnlyList<Card> GnomePool(Game g, Controller c)
		{
			using (g.GenerateFor(c))
				return Gnomes.Select(Cards.FromId).Where(card => card != null && card.Implemented && !g.BlocksGenerated(card)).ToArray();
		}

		/// <summary>
		/// GILA_BOSS_29p Infatuation / GILA_BOSS_29t Enchanting Tune: the enemy minions of <paramref name="c"/> a control change
		/// can take, in board order: on the board, not dormant, not dying, at most <paramref name="maxAttack"/> Attack (a
		/// negative value: any), and never a run shrine (it cannot change sides).
		/// </summary>
		public static List<Minion> Charmable(Controller c, int maxAttack) =>
			Touchable(c.Opponent).Where(m => (maxAttack < 0 || m.AttackDamage <= maxAttack) && ShrineRules.CanChangeController(m)).ToList();

		/// <summary>
		/// GILA_BOSS_26p Nab, "Put an enemy minion into a sack": the minion leaves its board for the set-aside store of a
		/// Stuffed Sack that is summoned for the power's owner (Actions.EncounterRules.Store). Nothing when the minion cannot be
		/// stored (a run shrine, a dormant or dying minion) or the owner's board is full.
		/// </summary>
		internal static Minion Nab(Controller c, IEntity source, Minion victim)
		{
			if (victim == null || victim.Controller == c || c.BoardZone.IsFull || !EncounterRules.CanStore(victim)) return null;
			Game g = c.Game;
			var sack = (Minion)Entity.FromCard(c, Cards.FromId(StuffedSack));
			if (!EncounterRules.Store(sack, victim)) return null;
			Generic.SummonBlock(g, sack, -1, source);
			return sack;
		}

		private static void F10()
		{
			var aMinion = new[] { PlayReq.REQ_TARGET_TO_PLAY, PlayReq.REQ_MINION_TARGET };
			var aFriendlyMinion = new[] { PlayReq.REQ_TARGET_TO_PLAY, PlayReq.REQ_MINION_TARGET, PlayReq.REQ_FRIENDLY_TARGET };

			// ---------------- A Mangy Wolf (GILA_BOSS_20h, 15 Health)
			// Guttural Howl (1): "Give a random minion in your hand +1/+1." One minion the boss holds, by the game RNG; offered
			// only while it holds one (its own hand is exact in its view).
			Power("GILA_BOSS_20p", null, () => new CustomTask((g, c, s, t, stack) =>
			{
				var minions = c.HandZone.Where(p => p is Minion).ToList();
				if (minions.Count == 0) return;
				IPlayable pick = minions[0];
				if (minions.Count > 1)
				{
					pick = minions[g.Random.Next(minions.Count)];
					g.OnRandomHappened(true);
				}
				Generic.AddEnchantmentBlock(g, Cards.FromId("GILA_BOSS_20e"), (IPlayable)s, pick);
			}), usable: c => c.HandZone.Any(p => p is Minion));
			Enchantment("GILA_BOSS_20e", () => new Power { Enchant = new Enchant(Effects.AttackHealth_N(1)) });

			// ---------------- Rottooth (GILA_BOSS_21h, 15 Health)
			// Fading Bite (2): "Gain +2 Attack this turn." (Fangs: the hero has +2 Attack until the turn ends.)
			Power("GILA_BOSS_21p", null, () => new AddEnchantmentTask("GILA_BOSS_21e", EntityType.HERO));
			Enchantment("GILA_BOSS_21e", () => new Power { Enchant = new Enchant(Effects.Attack_N(2)) { IsOneTurnEffect = true } });

			// ---------------- Cutthroat Willie (GILA_BOSS_22h, 25 Health)
			// Shank (2): "Deal 1 damage to a minion. Draw a card." Any minion; a hero power, so no Spell Damage.
			Power("GILA_BOSS_22p", aMinion, () => ComplexTask.Create(new DamageTask(1, EntityType.TARGET), new DrawTask()));

			// ---------------- Grubb the Swampdrinker (GILA_BOSS_23h, 50 Health)
			// Chuck (1): "Destroy a friendly minion and deal its Attack to the enemy hero." The Attack it has when it is thrown.
			Power("GILA_BOSS_23p", aFriendlyMinion, () => new CustomTask((g, c, s, t, stack) =>
			{
				if (!(t is Minion thrown)) return;
				int attack = thrown.AttackDamage;
				DeathContextRules.RecordDestroy(thrown, s);
				thrown.Destroy();
				if (attack > 0) Generic.DamageCharFunc.Invoke((IPlayable)s, c.Opponent.Hero, attack, false);
			}));
			// Chuck (the 6-Cost spell): "Discard all minions from your hand and deal their Attack to the enemy hero." Each minion
			// in hand order: discarded, then its Attack dealt as one hit (no Spell Damage: the text has no damage number).
			BossCard("GILA_BOSS_23t", () => new CardDef(new Power
			{
				PowerTask = new CustomTask((g, c, s, t, stack) =>
				{
					foreach (IPlayable p in c.HandZone.Where(p => p is Minion).ToList())
					{
						if (g.State == State.COMPLETE || p.Zone != c.HandZone) continue;
						int attack = ((Minion)p).AttackDamage;
						Generic.DiscardBlock.Invoke(c, p);
						if (attack > 0) Generic.DamageCharFunc.Invoke((IPlayable)s, c.Opponent.Hero, attack, false);
					}
				}),
			}));

			// ---------------- Gnomenapper (GILA_BOSS_26h, 40 Health)
			// Nab (2): "Put an enemy minion into a sack." A Stuffed Sack is summoned for the boss and holds the minion out of play;
			// it needs a free slot on the boss's board, and a run shrine is never a legal target.
			Add("GILA_BOSS_26p", EncounterCardKind.Power, () => new CardDef(
				new Dictionary<PlayReq, int>
				{
					{ PlayReq.REQ_TARGET_TO_PLAY, 0 }, { PlayReq.REQ_MINION_TARGET, 0 }, { PlayReq.REQ_ENEMY_TARGET, 0 }, { PlayReq.REQ_NUM_MINION_SLOTS, 1 },
				},
				new Power { PowerTask = new CustomTask((g, c, s, t, stack) => Nab(c, s, t as Minion)) }),
				card => card.SourceTargetingPredicate = (source, target) => !ShrineRules.IsMarked(target));
			// Stuffed Sack (0/3): "Deathrattle: Return your stolen minion!" The minion comes back to its own controller's board as
			// it left (its damage and enchantments kept), whoever controls the sack; with that board full it is lost. A silenced,
			// transformed or bounced sack never returns it.
			BossCard(StuffedSack, () => new CardDef(new Power
			{
				DeathrattleTask = new CustomTask((g, c, s, t, stack) => EncounterRules.Release(s, s)),
			}));
			// Sack of Gnomes (4): "Fill your board with random Gnomes." One random Gnome of the pool per free slot.
			BossCard("GILA_BOSS_26t2", () => new CardDef(new Power
			{
				PowerTask = new CustomTask((g, c, s, t, stack) =>
				{
					var pool = GnomePool(g, c);
					if (pool.Count == 0) return;
					while (!c.BoardZone.IsFull && g.State != State.COMPLETE)
					{
						g.OnRandomHappened(true);
						int before = c.BoardZone.Count;
						Generic.SummonBlock(g, (Minion)Entity.FromCard(c, pool[g.Random.Next(pool.Count)]), -1, s);
						if (c.BoardZone.Count <= before) break;
					}
				}),
			}));

			// ---------------- Experiment 3C (GILA_BOSS_27h, 60 Health)
			// Consume (1): "Destroy a friendly minion, then draw 3 cards."
			Power("GILA_BOSS_27p", aFriendlyMinion, () => ComplexTask.Create(new DestroyTask(EntityType.TARGET), new DrawTask(3)));
			// Amalgamate (8): "Destroy all minions. Summon an Amalgamation with the combined Attack and Health." The Attack and
			// remaining Health of every minion of both boards an effect can pick are added up, the minions are destroyed and
			// leave (their Deathrattles resolve; a run shrine goes dormant), then one Amalgamation with those stats is summoned
			// for the caster. With no minion to destroy nothing is summoned.
			BossCard("GILA_BOSS_27t", () => new CardDef(new Power
			{
				PowerTask = new CustomTask((g, c, s, t, stack) =>
				{
					var all = Touchable(c).Concat(Touchable(c.Opponent)).ToList();
					if (all.Count == 0) return;
					int attack = all.Sum(m => Math.Max(0, m.AttackDamage)), health = all.Sum(m => Math.Max(0, m.Health));
					foreach (Minion m in all)
					{
						DeathContextRules.RecordDestroy(m, s);
						m.Destroy();
					}
					g.DeathProcessingAndAuraUpdate();
					if (g.State == State.COMPLETE || health <= 0 || c.BoardZone.IsFull) return;
					var amalgamation = (Minion)Entity.FromCard(c, Cards.FromId(Amalgamation));
					amalgamation.AttackDamage = attack;
					amalgamation.BaseHealth = health;
					Generic.SummonBlock(g, amalgamation, -1, s);
				}),
			}));
			// Amalgamation: "This is an Elemental, Mech, Demon, Murloc, Dragon, Beast, Pirate and Totem." (the card's own race tag)
			Token(Amalgamation);

			// ---------------- Sazzmi Gentlehorn (GILA_BOSS_29h, 35 Health)
			// Infatuation (1): "Gain control of enemy minions with 1 or less Attack." Every such minion, in board order; one
			// taken with the boss's board full is destroyed (the engine's rule for a control change); a run shrine never changes
			// sides. Offered only while such a minion is on the enemy board.
			Power("GILA_BOSS_29p", null, () => new CustomTask((g, c, s, t, stack) =>
			{
				foreach (Minion m in Charmable(c, 1))
					new ControlTask(EntityType.TARGET).Process(g, c, s, m, stack);
			}), usable: c => Charmable(c, 1).Count > 0);
			// Enchanting Tune (4): "Gain control of all enemy minions. Set their Attack and Health to 1." Each minion taken is
			// set to 1/1 (Enchanted); one that found no room is destroyed instead.
			BossCard("GILA_BOSS_29t", () => new CardDef(new Power
			{
				PowerTask = new CustomTask((g, c, s, t, stack) =>
				{
					foreach (Minion m in Charmable(c, -1))
					{
						new ControlTask(EntityType.TARGET).Process(g, c, s, m, stack);
						if (m.Zone == c.BoardZone && !m.ToBeDestroyed) Generic.AddEnchantmentBlock(g, Cards.FromId("GILA_BOSS_29e"), (IPlayable)s, m);
					}
				}),
			}));
			Enchantment("GILA_BOSS_29e", () => new Power { Enchant = new Enchant(Effects.SetAttackHealth(1)) });

			// ---------------- Gnarlroot (GILA_BOSS_31h, 30 Health)
			// Splinter (1): "Take 2 damage. Summon a 2/2 Treant." Needs a free board slot.
			Power("GILA_BOSS_31p", new[] { PlayReq.REQ_NUM_MINION_SLOTS }, () => ComplexTask.Create(
				new DamageTask(2, EntityType.HERO),
				new SummonTask(SplinterTreant)));

			// ---------------- Niira the Trickster (GILA_BOSS_32h, 25 Health)
			// Unstable Growth (1): "Give all minions +1/+1." Both boards. Offered only while a minion is on a board.
			Power("GILA_BOSS_32p", null, () => new CustomTask((g, c, s, t, stack) =>
			{
				foreach (Minion m in Touchable(c).Concat(Touchable(c.Opponent)).ToList())
					Generic.AddEnchantmentBlock(g, Cards.FromId("GILA_BOSS_32e"), (IPlayable)s, m);
			}), usable: c => Touchable(c).Any() || Touchable(c.Opponent).Any());
			Enchantment("GILA_BOSS_32e", () => new Power { Enchant = new Enchant(Effects.AttackHealth_N(1)) });

			// ---------------- The Scarecrow (GILA_BOSS_33h, 40 Health)
			// Harvest (1): "Destroy a friendly minion. Restore 8 Health to your hero."
			Power("GILA_BOSS_33p", aFriendlyMinion, () => ComplexTask.Create(new DestroyTask(EntityType.TARGET), new HealTask(8, EntityType.HERO)));
			// Scythe (1/1 weapon): "Doesn't lose Durability. When you kill a minion with this weapon, gain +1 Attack." An attack
			// never costs it Durability (the weapon is Immune, as Runeforge Haunter's rule; a destroy effect still removes it);
			// after its hero attacked a minion that is dead from it, the weapon gains +1 Attack (Bloody).
			BossCard("GILA_BOSS_33t", () => new CardDef(new Power
			{
				Trigger = new Trigger(TriggerType.AFTER_ATTACK)
				{
					TriggerSource = TriggerSource.HERO,
					Condition = new SelfCondition(p => p.Game.CurrentEventData?.EventTarget is Minion m && m.IsDead),
					SingleTask = new AddEnchantmentTask("GILA_BOSS_33e2", EntityType.SOURCE),
				},
			}), card => card.Tags[GameTag.IMMUNE] = 1);
			Enchantment("GILA_BOSS_33e2", () => new Power { Enchant = new Enchant(Effects.Attack_N(1)) });

			// ---------------- Inquisitor Hav'nixx (GILA_BOSS_34h, 40 Health)
			// Devour Soul (4): "Halve the enemy hero's Health." Not damage: the hero keeps the larger half (a hero at 1 Health
			// stays at 1), Armor is untouched and nothing that reacts to damage sees it. Offered only above 1 Health.
			Power("GILA_BOSS_34p", null, () => new CustomTask((g, c, s, t, stack) =>
			{
				Hero hero = c.Opponent.Hero;
				int lost = hero.Health / 2;
				if (lost > 0) hero.Damage += lost;
			}), usable: c => c.Opponent.Hero.Health > 1);

			// ---------------- Chupacabran (GILA_BOSS_35h, 30 Health)
			// Bloodthirst (1): "Give a friendly minion Lifesteal." A minion of the boss that has none yet.
			Add("GILA_BOSS_35p", EncounterCardKind.Power, () => new CardDef(Reqs(aFriendlyMinion),
				new Power { PowerTask = new AddEnchantmentTask("GILA_BOSS_35e2", EntityType.TARGET) }),
				card => card.SourceTargetingPredicate = (source, target) => target is Minion m && !m.HasLifeSteal);
			Enchantment("GILA_BOSS_35e2", () => new Power { Enchant = new Enchant(Effects.Lifesteal) });
			// Vampiric Fangs (3): "Destroy a minion. Restore its Health to your hero." Its remaining Health when it is destroyed.
			BossCard("GILA_BOSS_35t", () => new CardDef(Reqs(aMinion), new Power
			{
				PowerTask = new CustomTask((g, c, s, t, stack) =>
				{
					if (!(t is Minion victim)) return;
					int health = Math.Max(0, victim.Health);
					DeathContextRules.RecordDestroy(victim, s);
					victim.Destroy();
					if (health > 0) new HealTask(health, EntityType.HERO).Process(g, c, s, t, stack);
				}),
			}));

			// ---------------- Griselda (GILA_BOSS_36h, 30 Health)
			// Witch's Kiss (2): "Transform a random minion into a 0/1 Frog with Taunt." Any minion of either board an effect can
			// pick, by the game RNG (Hex's Frog). A run shrine picked is destroyed instead and goes dormant (the transform rule of
			// Actions.Generic). Offered only while a minion is on a board.
			Power("GILA_BOSS_36p", null, () => new CustomTask((g, c, s, t, stack) =>
			{
				var minions = Touchable(c).Concat(Touchable(c.Opponent)).ToList();
				if (minions.Count == 0) return;
				Minion pick = minions[0];
				if (minions.Count > 1)
				{
					pick = minions[g.Random.Next(minions.Count)];
					g.OnRandomHappened(true);
				}
				Generic.TransformBlock.Invoke(pick.Controller, Cards.FromId(KissFrog), pick);
			}), usable: c => Touchable(c).Any() || Touchable(c.Opponent).Any());

			// ---------------- Wharrgarbl (GILA_BOSS_37h, 30 Health)
			// It's Raining Fin (2): "Draw 3 Murlocs from your deck." Up to three random Murlocs of the boss's own deck, each an
			// ordinary draw of that card (a full hand burns it). Offered whenever the hand has room: whether a Murloc is left is
			// the search's estimate, never a rule (its deck order is sampled).
			Power("GILA_BOSS_37p", null, () => new CustomTask((g, c, s, t, stack) =>
			{
				for (int i = 0; i < 3 && g.State != State.COMPLETE; i++)
				{
					var murlocs = c.DeckZone.Where(p => p is Minion m && m.Card.IsRace(Race.MURLOC)).ToList();
					if (murlocs.Count == 0) return;
					if (murlocs.Count > 1) g.OnRandomHappened(true);
					Generic.Draw(c, murlocs[g.Random.Next(murlocs.Count)]);
				}
			}), usable: c => !c.HandZone.IsFull);
			// Murloc Infestation (3): "Give your minions: 'Deathrattle: Summon a random Murloc.'" The minions on the caster's
			// board now; each summons, when it dies, one random scripted Murloc the run's generation filter allows (the summoned
			// Murloc has no such Deathrattle).
			BossCard("GILA_BOSS_37t", () => new CardDef(new Power
			{
				PowerTask = new CustomTask((g, c, s, t, stack) =>
				{
					foreach (Minion m in Touchable(c).ToList())
						Generic.AddEnchantmentBlock(g, Cards.FromId("GILA_BOSS_37e2"), (IPlayable)s, m);
				}),
			}));
			Enchantment("GILA_BOSS_37e2", () => new Power
			{
				DeathrattleTask = new CustomTask((g, c, s, t, stack) =>
					SummonRandom(g, c, s, RuleMinions(g, c, s, "murloc", card => card.IsRace(Race.MURLOC)))),
			});

			// ---------------- Ravencaller Cozzlewurt (GILA_BOSS_38h, 15 Health)
			// Call of the Raven (1): "Add a random 1-Cost minion to your hand." A random scripted 1-Cost minion of any class the
			// run's generation filter allows for the boss. Offered only while the hand has room.
			Power("GILA_BOSS_38p", null, () => new CustomTask((g, c, s, t, stack) =>
			{
				var pool = RuleMinions(g, c, s, "cost1", card => card.Cost == 1);
				if (pool.Count == 0 || c.HandZone.IsFull) return;
				g.OnRandomHappened(true);
				Generic.AddHandPhase.Invoke(c, Entity.FromCard(c, pool[g.Random.Next(pool.Count)]));
			}), usable: c => !c.HandZone.IsFull);
		}
	}
}
