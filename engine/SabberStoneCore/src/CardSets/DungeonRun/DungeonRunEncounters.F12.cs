using System;
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
	// Phase 8, family 12: Monster Hunt bosses C (The Witchwood). Thirteen encounters, ten of them with a passive hero power
	// (started at match start by EncounterRules.ActivatePower) and three with a clickable one, and the two boss-only cards
	// they bring: Groddo the Bogwarden (From the Swamp, Bloated Zombie), Blood Witch Gretta (Blood Red Apple), Forlorn Lovers
	// (Together Forever), Cultist S'thara (Poisoned Drink), Vitus the Exiled (Monstrous Aura), Shudderwock (Frumiousity), The
	// Whisperer (Echoes of the Witchwood), Sephira Dusktalon (Tangled Wrath), Winslow Tobtock (Hypnotize), Gobbles (Starving),
	// The Mute (...), Ratcatcher Hannigul (Cannibalism) and Plaguemaster Rancel (Poison Flask, Infected Quillflinger). The
	// texts are the client's (facts/violet-hold-2026-09.json). Every random choice uses the game RNG and is reported as a
	// random event; the only rules beyond a trigger or an aura are in Actions/EncounterRules.F12.cs (no private tag). Hidden
	// zones: Forlorn Lovers and Winslow Tobtock change the boss's own deck (an own-deck fact, EncounterRules.DeckRewritten);
	// Hypnotize changes both hands and both decks while hidden (a belief reset for both players, RunHelpers.BeliefReset).
	public static partial class DungeonRunEncounters
	{
		/// <summary>GILA_BOSS_24p From the Swamp: the 1/3 Bloated Zombie it raises.</summary>
		public const string BloatedZombie = "GILA_BOSS_24t";

		/// <summary>
		/// GILA_BOSS_64p Hypnotize, "Each player shuffles their hand into their deck and draws that many cards.": the power's owner
		/// first, then its opponent. Each card of a hand goes back into its owner's deck as a plain card (its enchantments are removed
		/// and the MoveToDeck task, Plot Twist's, resets its tags; a card that finds the deck full stays in hand and is not counted), then the
		/// owner draws as many as went back, by the ordinary draw rules. Both hands and decks changed while hidden: a belief reset of
		/// hand and deck is recorded for each player, and the owner's own-deck ledger is told its deck's exact list.
		/// </summary>
		public static void Hypnotize(Controller c, IEntity source)
		{
			Game g = c.Game;
			foreach (Controller p in new[] { c, c.Opponent })
			{
				if (g.State == State.COMPLETE) return;
				int moved = 0;
				foreach (IPlayable card in p.HandZone.ToArray())
				{
					if (p.DeckZone.IsFull) break;
					// Back to the deck as the card itself: what changed it in hand ends (its enchantments, as a graveyard does with
					// them; MoveToDeck resets its tags).
					if (card.AppliedEnchantments != null)
						foreach (Enchantment e in card.AppliedEnchantments.ToArray())
							if (!e.Card.Modular) e.Remove();
					new MoveToDeck(EntityType.TARGET).Process(g, p, source, card);
					if (card.Zone == p.DeckZone) moved++;
				}
				if (moved > 0 && p.DeckZone.Count > 1) g.OnRandomHappened(true);
				for (int i = 0; i < moved && g.State != State.COMPLETE; i++) Generic.Draw(p);
			}
			RunHelpers.BeliefReset(c, RunBeliefZones.HandAndDeck);
			RunHelpers.BeliefReset(c.Opponent, RunBeliefZones.HandAndDeck);
			EncounterRules.DeckRewritten(c, c.DeckZone.Select(p => p.Card.Id).OrderBy(id => id, StringComparer.Ordinal));
		}

		private static void F12()
		{
			var aMinion = new[] { PlayReq.REQ_TARGET_TO_PLAY, PlayReq.REQ_MINION_TARGET };

			// ---------------- Groddo the Bogwarden (GILA_BOSS_24h, 30 Health)
			// From the Swamp (passive): "Whenever an enemy dies, raise a 1/3 Bloated Zombie." A minion of the run player dies: one
			// Bloated Zombie is summoned for the boss (nothing with a full board).
			Passive("GILA_BOSS_24p", () => new Power
			{
				Trigger = new Trigger(TriggerType.DEATH) { TriggerSource = TriggerSource.OP_MINIONS, SingleTask = new SummonTask(BloatedZombie) },
			});
			// Bloated Zombie (1/3): "Deathrattle: Deal 1 damage to all characters." (both heroes and every minion)
			BossCard(BloatedZombie, () => new CardDef(new Power { DeathrattleTask = new DamageTask(1, EntityType.ALL) }));

			// ---------------- Blood Witch Gretta (GILA_BOSS_30h, 30 Health)
			// Blood Red Apple (passive): "Spells cost Health instead of Mana." The boss's own spells (Blood Pact's rule, TRLA_113:
			// the SPELLS_COST_HEALTH controller aura, a count). Paying Health is not damage.
			Passive("GILA_BOSS_30p", () => new Power
			{
				Aura = new Aura(AuraType.CONTROLLER, new Effect(GameTag.SPELLS_COST_HEALTH, EffectOperator.ADD, 1)),
			});

			// ---------------- Forlorn Lovers (GILA_BOSS_39h, 70 Health)
			// Together Forever (passive): "Whenever a friendly minion dies, shuffle a copy into your deck." A fresh copy of the
			// card (no buffs); nothing when the deck is full; a run shrine never counts. The boss's own-deck ledger is told the
			// deck's exact list after each copy (the copy's identity is public: it is the minion that just died).
			Passive("GILA_BOSS_39p", () => new Power
			{
				Trigger = new Trigger(TriggerType.DEATH)
				{
					TriggerSource = TriggerSource.MINIONS,
					SingleTask = new CustomTask((g, c, s, t, stack) =>
					{
						if (!(t is Minion dead) || ShrineRules.IsMarked(dead) || c.DeckZone.IsFull || g.State == State.COMPLETE) return;
						if (c.DeckZone.Count > 0) g.OnRandomHappened(true);
						if (!Generic.ShuffleIntoDeck(c, s, Entity.FromCard(c, dead.Card))) return;
						EncounterRules.DeckRewritten(c, c.DeckZone.Select(p => p.Card.Id).OrderBy(id => id, StringComparer.Ordinal));
					}),
				},
			});

			// ---------------- Cultist S'thara (GILA_BOSS_45h, 40 Health)
			// Poisoned Drink (passive): "Whenever a player draws a card, deal 1 damage to them." Either player, every draw (a card
			// burned by a full hand was drawn too); Fatigue draws no card. The opening hand and the mulligan are dealt, not drawn.
			Passive("GILA_BOSS_45p", () => new Power
			{
				Trigger = new Trigger(TriggerType.DRAW)
				{
					TriggerSource = TriggerSource.ALL,
					SingleTask = new CustomTask((g, c, s, t, stack) =>
					{
						Hero hero = t?.Controller?.Hero;
						if (hero == null || g.State == State.COMPLETE) return;
						Generic.DamageCharFunc.Invoke((IPlayable)s, hero, 1, false);
					}),
				},
			});

			// ---------------- Vitus the Exiled (GILA_BOSS_46h, 40 Health)
			// Monstrous Aura (passive): "Your minions have +1/+1." (Empowered, GILA_BOSS_46e, on each minion of the boss's board)
			Passive("GILA_BOSS_46p", () => new Power { Aura = new Aura(AuraType.BOARD, "GILA_BOSS_46e") });
			Enchantment("GILA_BOSS_46e", () => new Power { Enchant = new Enchant(Effects.AttackHealth_N(1)) });

			// ---------------- Shudderwock (GILA_BOSS_47h, 50 Health)
			// Frumiousity (passive): "All Battlecries trigger twice." Battle Totem's rule for both players (EncounterRules.BattlecriesTwice).
			Passive("GILA_BOSS_47p", () => new Power { Aura = EncounterRules.BattlecriesTwice(bothPlayers: true) });

			// ---------------- The Whisperer (GILA_BOSS_50h, 25 Health)
			// Echoes of the Witchwood (passive): "All spells have Echo." Every spell in either hand has Echo (a hand aura on both
			// players, renewed at every aura update). An Echo copy never costs less than (1) (EncounterRules.EchoCopyFloor), and a
			// player who has received EncounterRules.EchoCopiesPerTurn copies this turn has no Echo on its spells until the turn ends.
			Passive("GILA_BOSS_50p", () => new Power
			{
				Aura = new Aura(AuraType.HANDS, new Effect(GameTag.ECHO, EffectOperator.SET, 1))
				{
					Condition = new SelfCondition(p => p is Spell && EncounterRules.EchoCopiesLeft(p.Controller)),
					Restless = true,
				},
			});

			// ---------------- Sephira Dusktalon (GILA_BOSS_63h, 40 Health)
			// Tangled Wrath (passive): "Whenever your opponent casts a spell, draw a spell from your deck." One random spell of the
			// boss's own deck, an ordinary draw of that card (a full hand burns it); nothing when no spell is left.
			Passive("GILA_BOSS_63p", () => new Power
			{
				Trigger = new Trigger(TriggerType.CAST_SPELL)
				{
					TriggerSource = TriggerSource.ENEMY,
					SingleTask = new CustomTask((g, c, s, t, stack) =>
					{
						if (g.State == State.COMPLETE) return;
						var spells = c.DeckZone.Where(p => p is Spell).ToList();
						if (spells.Count == 0) return;
						if (spells.Count > 1) g.OnRandomHappened(true);
						Generic.Draw(c, spells[g.Random.Next(spells.Count)]);
					}),
				},
			});

			// ---------------- Winslow Tobtock (GILA_BOSS_64h, 40 Health)
			// Hypnotize (2): "Each player shuffles their hand into their deck and draws that many cards." (Hypnotize above) Offered
			// while either hand holds a card (hand sizes are public).
			Power("GILA_BOSS_64p", null, () => new CustomTask((g, c, s, t, stack) => Hypnotize(c, s)),
				usable: c => c.HandZone.Count > 0 || c.Opponent.HandZone.Count > 0);

			// ---------------- Gobbles (GILA_BOSS_65h, 10 Health)
			// Starving (passive): "Whenever a friendly Beast dies, draw a card." (a minion of every type is a Beast too)
			Passive("GILA_BOSS_65p", () => new Power
			{
				Trigger = new Trigger(TriggerType.DEATH)
				{
					TriggerSource = TriggerSource.MINIONS,
					Condition = new SelfCondition(p => p.Card.IsRace(Race.BEAST)),
					SingleTask = new DrawTask(),
				},
			});

			// ---------------- The Mute (GILA_BOSS_66h, 25 Health)
			// ... (1): "Silence a minion." Any minion (the engine's Silence: a silenced run shrine loses its rule, as from any Silence).
			Power("GILA_BOSS_66p", aMinion, () => new SilenceTask(EntityType.TARGET));

			// ---------------- Ratcatcher Hannigul (GILA_BOSS_67h, 40 Health)
			// Cannibalism (passive): "Whenever a minion dies, give the adjacent minions +1 Attack." Either board; the minions that
			// stood next to it when it died (EncounterRules.DeathNeighbours), read inside the death pass (a fast trigger), each
			// gaining Mmm, so tasty! (GILA_BOSS_67e).
			Passive("GILA_BOSS_67p", () => new Power
			{
				Trigger = new Trigger(TriggerType.DEATH)
				{
					TriggerSource = TriggerSource.ALL_MINIONS,
					FastExecution = true,
					SingleTask = new CustomTask((g, c, s, t, stack) =>
					{
						if (!(t is Minion dead)) return;
						foreach (Minion m in EncounterRules.DeathNeighbours(dead))
							Generic.AddEnchantmentBlock(g, Cards.FromId("GILA_BOSS_67e"), (IPlayable)s, m);
					}),
				},
			});
			Enchantment("GILA_BOSS_67e", () => new Power { Enchant = new Enchant(Effects.Attack_N(1)) });

			// ---------------- Plaguemaster Rancel (GILA_BOSS_68h, 40 Health)
			// Poison Flask (2): "Deal 2 damage to a minion. If it survives, give it Poisonous." Any minion (a hero power: no Spell
			// Damage); Dripping Poison (GILA_BOSS_68e) when the minion is still alive after the hit.
			Power("GILA_BOSS_68p", aMinion, () => new CustomTask((g, c, s, t, stack) =>
			{
				if (!(t is Minion m)) return;
				new DamageTask(2, EntityType.TARGET).Process(g, c, s, t, stack);
				if (m.Zone == m.Controller.BoardZone && !m.ToBeDestroyed && m.Health > 0)
					Generic.AddEnchantmentBlock(g, Cards.FromId("GILA_BOSS_68e"), (IPlayable)s, m);
			}));
			Enchantment("GILA_BOSS_68e", () => new Power { Enchant = new Enchant(new Effect(GameTag.POISONOUS, EffectOperator.SET, 1)) });
			// Infected Quillflinger (3/7): "Whenever this minion takes damage, deal 1 damage to a random enemy minion." Also when the
			// hit kills it; an enemy minion an effect can pick, by the game RNG; nothing when there is none.
			BossCard("GILA_BOSS_68t", () => new CardDef(new Power
			{
				Trigger = new Trigger(TriggerType.TAKE_DAMAGE)
				{
					TriggerSource = TriggerSource.SELF,
					SingleTask = new CustomTask((g, c, s, t, stack) =>
					{
						if (g.State == State.COMPLETE) return;
						var enemies = Touchable(c.Opponent).ToList();
						if (enemies.Count == 0) return;
						if (enemies.Count > 1) g.OnRandomHappened(true);
						Generic.DamageCharFunc.Invoke((IPlayable)s, enemies[g.Random.Next(enemies.Count)], 1, false);
					}),
				},
			}));
		}
	}
}
