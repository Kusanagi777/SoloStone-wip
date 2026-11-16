using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Conditions;
using SabberStoneCore.EffectSystem;
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
	// Phase 8, family 19: Tombs of Terror bosses A (ULDA_BOSS_01 to ULDA_BOSS_18) and Sinkhole's Sand Trap (ULDA_030). Thirteen
	// clickable powers and five passive ones (E.V.I.L. Promotion, "Confiscated" Goods, Unstable Weaponry, Sword Dance, Under
	// Wraps). The texts are the client's (facts/violet-hold-2026-09.json). A power works on public zones (the boards, the heroes,
	// both graveyards) or on the boss's own hand and deck; Sand Surge puts a publicly known card into the run player's deck. A
	// power that would do nothing is not offered, by a condition that reads only public zones or the boss's own hand (never a
	// deck). Every random choice uses the game RNG and is reported as a random event. The only state is E.V.I.L. Promotion's
	// count, kept on the power itself in its client script number (the @ of its text). A run shrine is never resurrected (a dead
	// shrine is a corpse: ShrineRules.IsCorpse), and a destroy aimed at one makes it go dormant (ShrineRules).
	public static partial class DungeonRunEncounters
	{
		/// <summary>
		/// Family 19 (Tombs of Terror bosses A): the constants and rules its scripts share, in one nested class so that no name of
		/// it can meet a name of another family's partial file. Tests read them.
		/// </summary>
		public static class TombsA
		{
			/// <summary>ULDA_BOSS_01p Boom Bullets: the Goblin Bomb it summons (Boomsday's 0/2, "Deathrattle: Deal 2 damage to the enemy hero").</summary>
			public const string GoblinBomb = "BOT_031";
			/// <summary>ULDA_BOSS_08p Sand Surge: the card it shuffles into the opponent's deck.</summary>
			public const string SandTrap = "ULDA_030";
			/// <summary>ULDA_030 Sand Trap: how many cards it removes from the top of its holder's deck.</summary>
			public const int SandTrapRemoves = 2;
			/// <summary>ULDA_BOSS_13p Snakeshot: the 1/1 Snake (Snake Trap's).</summary>
			public const string SnakeshotSnake = "EX1_554t";
			/// <summary>ULDA_BOSS_17p Spreading the Curse: the Leper Gnome it summons (the classic collectible).</summary>
			public const string CursedGnome = "EX1_029";
			/// <summary>ULDA_BOSS_09p Armor Repairs: below this Armor it gives 4, else 2.</summary>
			public const int ArmorRepairsBelow = 10;

			/// <summary>
			/// ULDA_BOSS_03p E.V.I.L. Promotion: the Lackeys (the engine's Lackey pool, LegacyGenerationV1 "Lackeys": Faceless, Kobold,
			/// Witchy, Goblin, Ethereal, Draconic and Titanic Lackey). Ids only: no card is read here.
			/// </summary>
			public static readonly IReadOnlyList<string> Lackeys = new[] { "DAL_613", "DAL_614", "DAL_615", "DAL_739", "DAL_741", "DRG_052", "ULD_616" };

			/// <summary>ULDA_BOSS_03p E.V.I.L. Promotion: the +N/+N the next Lackey gets (the power's script number; 1 before the first).</summary>
			public static int PromotionBonus(HeroPower power) => Math.Max(1, power?[GameTag.TAG_SCRIPT_DATA_NUM_1] ?? 1);

			/// <summary>
			/// The minions that died this game in <paramref name="c"/>'s graveyard (a public zone) whose card passes
			/// <paramref name="filter"/>; never a dead run shrine.
			/// </summary>
			public static List<IPlayable> DiedThisGame(Controller c, Func<Card, bool> filter = null) =>
				c.GraveyardZone.Where(p => p.ToBeDestroyed && p.Card.Type == CardType.MINION && !ShrineRules.IsCorpse(p) && (filter == null || filter(p.Card))).ToList();

			/// <summary>ULDA_BOSS_02p Mummy Ritual: the boss's dead minions with Reborn on their card.</summary>
			public static List<IPlayable> RitualCorpses(Controller c) => DiedThisGame(c, card => card[GameTag.REBORN] == 1);

			/// <summary>ULDA_BOSS_04p Belch: the minions of either player that died this game.</summary>
			public static List<IPlayable> BelchCorpses(Controller c) => DiedThisGame(c).Concat(DiedThisGame(c.Opponent)).ToList();

			// Summons a new copy of a random card of the corpses for c (the resurrection rule: the card, not the dead entity's
			// state). Returns the summoned minion, or null.
			internal static Minion Resurrect(Game g, Controller c, IEntity source, List<IPlayable> corpses)
			{
				if (corpses.Count == 0 || c.BoardZone.IsFull) return null;
				IPlayable pick = corpses[0];
				if (corpses.Count > 1)
				{
					pick = corpses[g.Random.Next(corpses.Count)];
					g.OnRandomHappened(true);
				}
				var minion = (Minion)Entity.FromCard(c, pick.Card, new EntityData { [GameTag.DISPLAYED_CREATOR] = source.Id });
				Generic.SummonBlock.Invoke(g, minion, -1, source);
				return minion.Zone?.Type == Zone.PLAY ? minion : null;
			}

			// A minion the boss summons with an enchantment (Snakeshot, Spreading the Curse).
			internal static ISimpleTask SummonWith(string minionId, string enchantmentId) => new CustomTask((g, c, s, t, stack) =>
			{
				if (c.BoardZone.IsFull) return;
				var minion = (Minion)Entity.FromCard(c, Cards.FromId(minionId));
				Generic.SummonBlock.Invoke(g, minion, -1, s);
				if (minion.Zone?.Type == Zone.PLAY && !minion.ToBeDestroyed)
					Generic.AddEnchantmentBlock(g, Cards.FromId(enchantmentId), (IPlayable)s, minion);
			});

			/// <summary>
			/// ULDA_BOSS_14p Sword Dance: a new copy of the destroyed <paramref name="weapon"/> (its Attack and every enchantment it
			/// holds, Kingsbane's rule; its Durability is the card's plus what those enchantments add, with no damage carried) is shuffled into its controller's deck as a public shuffle.
			/// Called while the weapon still holds its enchantments (the trigger runs at once, before Hero.ClearWeapon removes them).
			/// Returns the copy, or null (a full deck takes none).
			/// </summary>
			public static IPlayable SwordDance(Weapon weapon)
			{
				if (weapon == null) return null;
				Controller c = weapon.Controller;
				var tags = new EntityData
				{
					{ GameTag.ATK, weapon[GameTag.ATK] },
					{ GameTag.POISONOUS, weapon[GameTag.POISONOUS] },
					{ GameTag.LIFESTEAL, weapon[GameTag.LIFESTEAL] },
					// The Durability tag holds what the kept enchantments added (Greenskin's +1); Durability lost is DAMAGE, not copied.
					{ GameTag.DURABILITY, weapon[GameTag.DURABILITY] },
				};
				IPlayable copy = Entity.FromCard(c, weapon.Card, tags);
				if (weapon.AppliedEnchantments != null)
					foreach (Model.Entities.Enchantment e in weapon.AppliedEnchantments.ToArray())
					{
						Model.Entities.Enchantment instance = Model.Entities.Enchantment.CopyAttachment(c, copy, copy, e);
						if (e[GameTag.TAG_SCRIPT_DATA_NUM_1] > 0)
						{
							instance[GameTag.TAG_SCRIPT_DATA_NUM_1] = e[GameTag.TAG_SCRIPT_DATA_NUM_1];
							if (e[GameTag.TAG_SCRIPT_DATA_NUM_2] > 0) instance[GameTag.TAG_SCRIPT_DATA_NUM_2] = e[GameTag.TAG_SCRIPT_DATA_NUM_2];
						}
					}
				return Generic.ShuffleIntoDeck(c, weapon, copy, revealGenerated: true) ? copy : null;
			}

			/// <summary>ULDA_BOSS_06p "Confiscated" Goods: the weapons it may equip for <paramref name="c"/> (any class, scripted, behind the generation filter).</summary>
			public static IReadOnlyList<Card> ConfiscatedWeapons(Game g, Controller c, IEntity source) =>
				ScriptedPool(g, c, source, card => card.Type == CardType.WEAPON, anyClass: true);

			/// <summary>ULDA_BOSS_10p Well Equipped: the Rogue spells and weapons it may give <paramref name="c"/> (no Quest, no Secret).</summary>
			public static IReadOnlyList<Card> WellEquippedPool(Game g, Controller c, IEntity source) =>
				ScriptedPool(g, c, source, card => card.Class == CardClass.ROGUE && (card.Type == CardType.SPELL && !card.IsQuest && !card.IsSecret
					|| card.Type == CardType.WEAPON), anyClass: true);

			/// <summary>ULDA_BOSS_12p Brick by Brick: the Elementals it may offer <paramref name="c"/> (its class and neutral).</summary>
			public static IReadOnlyList<Card> BrickPool(Game g, Controller c, IEntity source) =>
				ScriptedPool(g, c, source, card => card.Type == CardType.MINION && card.IsRace(Race.ELEMENTAL));

			/// <summary>ULDA_BOSS_15p Puffertrooper: the Murlocs on <paramref name="c"/>'s board it can give Rush (no Rush or Charge yet).</summary>
			public static List<Minion> RushlessMurlocs(Controller c) =>
				Touchable(c).Where(m => m.Card.IsRace(Race.MURLOC) && !m.IsRush && !m.HasCharge).ToList();

			/// <summary>ULDA_BOSS_05p Venomous Stinger: the minion holds the sting.</summary>
			public static bool Stung(IPlayable p) => p?.AppliedEnchantments?.Any(e => e.Card.Id == "ULDA_BOSS_05e") == true;

			// Sand Trap: the top SandTrapRemoves cards of the holder's deck go to its graveyard (Azari's Devour's rule).
			internal static ISimpleTask SandTrapRemove() => new CustomTask((g, c, s, t, stack) =>
			{
				var burn = new MoveToGraveYard(EntityType.TOPCARDFROMDECK);
				for (int i = 0; i < SandTrapRemoves && !c.DeckZone.IsEmpty; i++) burn.Process(g, c, s, null, stack);
			});
		}

		private static void F19()
		{
			var aMinion = new[] { PlayReq.REQ_TARGET_TO_PLAY, PlayReq.REQ_MINION_TARGET };

			// ---------------- Waste Wanderer Cardish (ULDA_BOSS_01h)
			// Boom Bullets (2): "Summon a 0/2 Goblin Bomb. Give all other Goblin Bombs +1 Attack." The new Bomb first, then every
			// other Goblin Bomb on either board ("all other" names no side) gains +1 Attack. Needs a free slot.
			Power("ULDA_BOSS_01p", new[] { PlayReq.REQ_NUM_MINION_SLOTS }, () => new CustomTask((g, c, s, t, stack) =>
			{
				if (c.BoardZone.IsFull) return;
				var bomb = (Minion)Entity.FromCard(c, Cards.FromId(TombsA.GoblinBomb));
				Generic.SummonBlock.Invoke(g, bomb, -1, s);
				foreach (Minion other in Touchable(c).Concat(Touchable(c.Opponent)).Where(m => m != bomb && m.Card.Id == TombsA.GoblinBomb).ToList())
					Generic.AddEnchantmentBlock(g, Cards.FromId("ULDA_BOSS_01e"), (IPlayable)s, other);
			}));
			Enchantment("ULDA_BOSS_01e", () => new Power { Enchant = new Enchant(Effects.Attack_N(1)) });

			// ---------------- Dark Ritualist Zafarr (ULDA_BOSS_02h)
			// Mummy Ritual (3): "Resurrect a Reborn minion with 1 Health." A new copy of a random minion of the boss's that died
			// this game and has Reborn on its card, with 1 Health (as a Reborn return; the copy keeps its card's Reborn). Offered
			// while such a dead minion exists (the graveyard is public) and the board has room.
			Power("ULDA_BOSS_02p", new[] { PlayReq.REQ_NUM_MINION_SLOTS }, () => new CustomTask((g, c, s, t, stack) =>
			{
				Minion back = TombsA.Resurrect(g, c, s, TombsA.RitualCorpses(c));
				if (back != null && back.Health > 1) back.Damage = back.Health - 1;
			}), usable: c => TombsA.RitualCorpses(c).Count > 0);

			// ---------------- Vera Ridley (ULDA_BOSS_03h)
			// E.V.I.L. Promotion (passive): "After you play a Lackey, give it +@/+@ (upgrades each time you play a Lackey)." The
			// played Lackey gets +N/+N (ULDA_BOSS_03e "Increased Stats."), then N rises by 1; N is the power's script number.
			Passive("ULDA_BOSS_03p", () => new Power
			{
				Trigger = new Trigger(TriggerType.AFTER_PLAY_MINION)
				{
					TriggerSource = TriggerSource.FRIENDLY,
					Condition = new SelfCondition(p => TombsA.Lackeys.Contains(p.Card.Id)),
					SingleTask = new CustomTask((g, c, s, t, stack) =>
					{
						if (!(s is HeroPower power) || !(t is Minion lackey)) return;
						int bonus = TombsA.PromotionBonus(power);
						power[GameTag.TAG_SCRIPT_DATA_NUM_1] = bonus + 1;
						if (lackey.Zone?.Type == Zone.PLAY && !lackey.ToBeDestroyed)
							Generic.AddEnchantmentBlock(g, Cards.FromId("ULDA_BOSS_03e"), power, lackey, bonus, bonus);
					}),
				},
			});
			Enchantment("ULDA_BOSS_03e", () => new Power { Enchant = Enchants.Enchants.AddAttackHealthScriptTag });

			// ---------------- Nash the Greatworm (ULDA_BOSS_04h)
			// Belch (3): "Summon a minion that died this game." A new copy of a random minion of either player that died this game,
			// for the boss. Offered while one exists (both graveyards are public) and the board has room.
			Power("ULDA_BOSS_04p", new[] { PlayReq.REQ_NUM_MINION_SLOTS }, () => new CustomTask((g, c, s, t, stack) =>
				TombsA.Resurrect(g, c, s, TombsA.BelchCorpses(c))), usable: c => TombsA.BelchCorpses(c).Count > 0);

			// ---------------- Glack the Scorpid (ULDA_BOSS_05h)
			// Venomous Stinger (2): "Sting a minion. When it takes damage, destroy it." Any minion not stung yet.
			Add("ULDA_BOSS_05p", EncounterCardKind.Power, () => new CardDef(Reqs(aMinion),
				new Power { PowerTask = new AddEnchantmentTask("ULDA_BOSS_05e", EntityType.TARGET) }),
				card => card.SourceTargetingPredicate = (source, target) => target is Minion m && !TombsA.Stung(m));
			Enchantment("ULDA_BOSS_05e", () => new Power
			{
				Trigger = new Trigger(TriggerType.TAKE_DAMAGE)
				{
					TriggerSource = TriggerSource.ENCHANTMENT_TARGET,
					SingleTask = new CustomTask((g, c, s, t, stack) =>
					{
						if (!(t is Minion stung) || stung.Zone?.Type != Zone.PLAY || stung.ToBeDestroyed) return;
						new DestroyTask(EntityType.TARGET).Process(g, c, s, stung, stack);
					}),
				},
			});

			// ---------------- Mister Chu (ULDA_BOSS_06h)
			// "Confiscated" Goods (passive): "After you attack and kill a minion, equip a random weapon." After the boss's hero
			// attacked a minion that is dead from it: a random scripted weapon of any class the generation filter allows.
			Passive("ULDA_BOSS_06p", () => new Power
			{
				Trigger = new Trigger(TriggerType.AFTER_ATTACK)
				{
					TriggerSource = TriggerSource.HERO,
					Condition = new SelfCondition(p => p.Game.CurrentEventData?.EventTarget is Minion m && m.IsDead),
					SingleTask = new CustomTask((g, c, s, t, stack) =>
					{
						if (g.State == State.COMPLETE) return;
						var pool = TombsA.ConfiscatedWeapons(g, c, s);
						if (pool.Count == 0) return;
						if (pool.Count > 1) g.OnRandomHappened(true);
						var weapon = (Weapon)Entity.FromCard(c, pool[g.Random.Next(pool.Count)]);
						new WeaponTask().Process(g, c, s, null, new TaskStack { Playables = new List<IPlayable> { weapon } });
					}),
				},
			});

			// ---------------- Squeamlish (ULDA_BOSS_07h)
			// Druid of the Rat (2): "Give your minions +1/+1." Offered while the boss has a minion.
			Power("ULDA_BOSS_07p", null, () => new CustomTask((g, c, s, t, stack) =>
			{
				foreach (Minion m in Touchable(c).ToList())
					Generic.AddEnchantmentBlock(g, Cards.FromId("ULDA_BOSS_07e"), (IPlayable)s, m);
			}), usable: c => Touchable(c).Any());
			Enchantment("ULDA_BOSS_07e", () => new Power { Enchant = new Enchant(Effects.AttackHealth_N(1)) });

			// ---------------- Sinkhole (ULDA_BOSS_08h)
			// Sand Surge (1): "Shuffle a Sand Trap into your opponent's deck that swallows cards when drawn." A public shuffle: the run
			// player sees it go in; the boss's fair view keeps that deck masked, Sand Trap included. Offered while that deck has room (its count is public).
			Power("ULDA_BOSS_08p", null, () => new CustomTask((g, c, s, t, stack) =>
				Generic.ShuffleIntoDeck(c.Opponent, s, Entity.FromCard(c.Opponent, Cards.FromId(TombsA.SandTrap)), revealGenerated: true)),
				usable: c => !c.Opponent.DeckZone.IsFull);
			// Sand Trap: "Casts When Drawn. Remove the top 2 cards of your deck." Each card goes from the top of its holder's deck
			// to the holder's graveyard, a public zone (Azari's Devour), then the holder draws a card (the Casts When Drawn rule).
			BossCard(TombsA.SandTrap, () => new CardDef(new Power
			{
				PowerTask = TombsA.SandTrapRemove(),
				TopdeckTask = ComplexTask.Create(TombsA.SandTrapRemove(), new DrawTask()),
			}));

			// ---------------- Demolisher 3V-11 (ULDA_BOSS_09h)
			// Armor Repairs (2): "Gain 2 Armor. If you have less than 10 Armor, gain 4 Armor instead."
			Power("ULDA_BOSS_09p", null, () => new CustomTask((g, c, s, t, stack) =>
				c.Hero.GainArmor((IPlayable)s, c.Hero.Armor < TombsA.ArmorRepairsBelow ? 4 : 2)));

			// ---------------- Terraviss (ULDA_BOSS_10h)
			// Well Equipped (2): "Put a random Rogue Spell or Weapon into your hand." Offered while the hand has room.
			Power("ULDA_BOSS_10p", null, () => new CustomTask((g, c, s, t, stack) =>
			{
				var pool = TombsA.WellEquippedPool(g, c, s);
				if (pool.Count == 0 || c.HandZone.IsFull) return;
				g.OnRandomHappened(true);
				Generic.AddHandPhase.Invoke(c, Entity.FromCard(c, pool[g.Random.Next(pool.Count)]));
			}), usable: c => !c.HandZone.IsFull);

			// ---------------- R.O.B.O.L.D. (ULDA_BOSS_11h)
			// Unstable Weaponry (passive): "After you summon a Mech, deal 2 damage to a random enemy." Any enemy character an effect
			// can pick (Knife Juggler's rule).
			Passive("ULDA_BOSS_11p", () => new Power
			{
				Trigger = new Trigger(TriggerType.AFTER_SUMMON)
				{
					TriggerSource = TriggerSource.MINIONS,
					Condition = new SelfCondition(p => p.Card.IsRace(Race.MECHANICAL)),
					SingleTask = ComplexTask.DamageRandomTargets(1, EntityType.ENEMIES, 2),
				},
			});

			// ---------------- Pyramad (ULDA_BOSS_12h)
			// Brick by Brick (3): "Discover an Elemental. It costs (2) less." Among three distinct scripted Elementals of the boss's
			// class and neutral; the picked card gets "Bricked" (Costs 2 less). Offered while the hand has room.
			Power("ULDA_BOSS_12p", null, () => new CustomTask((g, c, s, t, stack) =>
			{
				if (c.HandZone.IsFull) return;
				var pool = TombsA.BrickPool(g, c, s);
				if (pool.Count == 0) return;
				var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
				ctx.Discover(ctx.SampleDistinct(pool, 3), ChoiceAction.HAND, new AddEnchantmentTask("ULDA_BOSS_12e", EntityType.TARGET));
				g.OnRandomHappened(true);
			}), usable: c => !c.HandZone.IsFull);
			Enchantment("ULDA_BOSS_12e", () => new Power { Enchant = new Enchant(Effects.ReduceCost(2)) });

			// ---------------- Snakeflinger Scalesnout (ULDA_BOSS_13h)
			// Snakeshot (2): "Summon a 1/1 Snake and give it Poisonous." Needs a free slot.
			Power("ULDA_BOSS_13p", new[] { PlayReq.REQ_NUM_MINION_SLOTS }, () => TombsA.SummonWith(TombsA.SnakeshotSnake, "ULDA_BOSS_13e"));
			Enchantment("ULDA_BOSS_13e", () => new Power { Enchant = new Enchant(GameTag.POISONOUS, EffectOperator.SET, 1) });

			// ---------------- Sword Dancer Sirinell (ULDA_BOSS_14h)
			// Sword Dance (passive): "After your weapon is destroyed, shuffle it back into your deck. It keeps any enchantments."
			// Runs at once when the weapon leaves (fast execution), while it still holds its enchantments.
			Passive("ULDA_BOSS_14p", () => new Power
			{
				Trigger = new Trigger(TriggerType.DEATH)
				{
					TriggerSource = TriggerSource.FRIENDLY,
					Condition = new SelfCondition(p => p is Weapon),
					FastExecution = true,
					SingleTask = new CustomTask((g, c, s, t, stack) => TombsA.SwordDance(t as Weapon)),
				},
			});

			// ---------------- Lt. Herring (ULDA_BOSS_15h)
			// Puffertrooper (2): "Give your Murlocs Rush." Each Murloc on the boss's board without Rush (ULDA_BOSS_15e). Offered
			// while there is one.
			Power("ULDA_BOSS_15p", null, () => new CustomTask((g, c, s, t, stack) =>
			{
				foreach (Minion m in TombsA.RushlessMurlocs(c))
					Generic.AddEnchantmentBlock(g, Cards.FromId("ULDA_BOSS_15e"), (IPlayable)s, m);
			}), usable: c => TombsA.RushlessMurlocs(c).Count > 0);
			Enchantment("ULDA_BOSS_15e", () => new Power { Enchant = new Enchant(Effects.Rush) });

			// ---------------- Battrund (ULDA_BOSS_16h)
			// Under Wraps (passive): "After you play a minion, give it Reborn." (ULDA_BOSS_16e "Wrapped Up").
			Passive("ULDA_BOSS_16p", () => new Power
			{
				Trigger = new Trigger(TriggerType.AFTER_PLAY_MINION)
				{
					TriggerSource = TriggerSource.FRIENDLY,
					SingleTask = new CustomTask((g, c, s, t, stack) =>
					{
						if (t is Minion played && played.Zone?.Type == Zone.PLAY && !played.ToBeDestroyed)
							Generic.AddEnchantmentBlock(g, Cards.FromId("ULDA_BOSS_16e"), (IPlayable)s, played);
					}),
				},
			});
			Enchantment("ULDA_BOSS_16e", () => new Power { Enchant = new Enchant(GameTag.REBORN, EffectOperator.SET, 1) });

			// ---------------- Ichabod the Cursed (ULDA_BOSS_17h)
			// Spreading the Curse (2): "Summon a Leper Gnome and give it Rush." Needs a free slot.
			Power("ULDA_BOSS_17p", new[] { PlayReq.REQ_NUM_MINION_SLOTS }, () => TombsA.SummonWith(TombsA.CursedGnome, "ULDA_BOSS_17e"));
			Enchantment("ULDA_BOSS_17e", () => new Power { Enchant = new Enchant(Effects.Rush) });

			// ---------------- Twizzleflux (ULDA_BOSS_18h)
			// Plagued Horde (2): "Give a minion 'Deathrattle: Deal 2 damage to the enemy hero.'" Any minion; the enemy is the one of
			// the minion's controller when it dies (an enchantment's Deathrattle runs for its target's controller).
			Power("ULDA_BOSS_18p", aMinion, () => new AddEnchantmentTask("ULDA_BOSS_18e", EntityType.TARGET));
			Enchantment("ULDA_BOSS_18e", () => new Power { DeathrattleTask = new DamageTask(2, EntityType.OP_HERO) });
		}

	}
}
