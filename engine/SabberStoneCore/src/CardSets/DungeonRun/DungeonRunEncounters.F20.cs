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
	// Phase 8, family 20: Tombs of Terror bosses B (ULDA_BOSS_19 to ULDA_BOSS_36), and Sothis's Mogu Cultist (ULD_705, a Saviors of
	// Uldum collectible the engine had as a stub) with its Highkeeper Ra (ULD_705t); those two keep their entries in
	// Standard/UldumCardsGen.cs, which call the rules here. Fourteen clickable powers and four passive ones (Spectral Swordsman,
	// Staking A Claim, Mother of Sand, Fireshaper). The texts are the client's (facts/violet-hold-2026-09.json). A power works on
	// public zones (the boards, the heroes) or on the boss's own hand; two read the run player's hand by the rule their text states
	// ("Retired" Treasure Hunter copies a random card of it, Mighty Windstorm puts a random minion of it onto the board), and in the
	// boss's fair-view projection that hand holds masked cards (Unknown), so there they use only cards the boss already saw go into
	// it. A power that would do nothing is not offered, by a condition that reads only public zones, zone counts or the boss's own
	// hand (never a deck, never the identity of a hidden card). Every random choice uses the game RNG and is reported as a random
	// event. No state is kept outside entity tags. A run shrine never leaves the board (Mighty Windstorm does not choose one), a
	// destroy aimed at one makes it go dormant (Vengeful Construct), and a dormant one (untouchable) is left out of every effect.
	public static partial class DungeonRunEncounters
	{
		/// <summary>
		/// Family 20 (Tombs of Terror bosses B): the constants and rules its scripts share, in one nested class so that no name of it
		/// can meet a name of another family's partial file. Tests read them.
		/// </summary>
		public static class TombsB
		{
			/// <summary>ULDA_BOSS_19p Titan Ritual: the Mogu Cultist it summons (ULD_705), two at a time.</summary>
			public const string MoguCultist = "ULD_705";
			/// <summary>ULD_705 Mogu Cultist: the minion seven Mogu Cultists become.</summary>
			public const string HighkeeperRa = "ULD_705t";
			/// <summary>ULD_705t Highkeeper Ra: the damage it deals to every enemy at the end of its owner's turn.</summary>
			public const int HighkeeperRaDamage = 20;
			/// <summary>ULDA_BOSS_24p Corrupted Oasis: the 2/2 Treant it adds (Witchwood Apple's, the power's template).</summary>
			public const string OasisTreant = "GIL_663t";
			/// <summary>ULDA_BOSS_25p Beetle Stampede: the Plated Beetle it summons.</summary>
			public const string PlatedBeetle = "LOOT_413";
			/// <summary>ULDA_BOSS_29p Mother of Sand: the Dust Devil it summons.</summary>
			public const string DustDevil = "EX1_243";
			/// <summary>ULDA_BOSS_32p Whispering Sentinel: the Wisp it summons.</summary>
			public const string SentinelWisp = "CS2_231";
			/// <summary>
			/// ULDA_BOSS_32p Whispering Sentinel: the Lifesteal it gives (Lightforged Blessing's enchantment "Lifesteal"; the client has
			/// no enchantment of the power's own).
			/// </summary>
			public const string SentinelLifesteal = "DAL_568e";
			/// <summary>ULDA_BOSS_35p Vengeful Construct: the highest Health a minion it may choose has.</summary>
			public const int ConstructMaxHealth = 2;
			/// <summary>ULDA_BOSS_36p Madness Incarnate: how many different enemy minions the hero attacks.</summary>
			public const int MadnessAttacks = 3;

			/// <summary>A run shrine (awake or dormant): never moved off its board by a family 20 power.</summary>
			public static bool IsShrine(IPlayable p) => p != null && p[ShrineRules.RunShrine] == 1;

			/// <summary>
			/// ULD_705 Mogu Cultist's Battlecry: "If your board is full of Mogu Cultists, sacrifice them all and summon Highkeeper Ra."
			/// With a full board of Mogu Cultists (the played one included) every one is destroyed, the deaths are resolved (so the
			/// board has room), then Highkeeper Ra is summoned for the owner.
			/// </summary>
			public static ISimpleTask MoguCultistBattlecry => new CustomTask((g, c, s, t, stack) =>
			{
				if (!c.BoardZone.IsFull || c.BoardZone.Any(m => m.Card.Id != MoguCultist)) return;
				foreach (Minion cultist in c.BoardZone.ToList())
					if (!cultist.ToBeDestroyed) cultist.Destroy();
				g.DeathProcessingAndAuraUpdate();
				if (g.State == State.COMPLETE || c.BoardZone.IsFull) return;
				Generic.SummonBlock.Invoke(g, (Minion)Entity.FromCard(c, Cards.FromId(HighkeeperRa)), -1, s);
			});

			/// <summary>ULD_705t Highkeeper Ra: "At the end of your turn, deal 20 damage to all enemies."</summary>
			public static Trigger HighkeeperRaTrigger => new Trigger(TriggerType.TURN_END)
			{
				SingleTask = new DamageTask(HighkeeperRaDamage, EntityType.ENEMIES),
			};

			/// <summary>ULDA_BOSS_20p Spectral Swordsman: the hero has a weapon that is not leaving (the Stealth holds while it does).</summary>
			public static bool HoldsWeapon(Controller c, IEntity leaving = null) =>
				c.Hero.Weapon != null && c.Hero.Weapon != leaving && !c.Hero.Weapon.ToBeDestroyed;

			// Spectral Swordsman: the hero's Stealth follows the weapon (set on an equip, cleared when the last weapon leaves).
			internal static void SetSwordsmanStealth(Controller c, IEntity leaving = null)
			{
				bool stealth = HoldsWeapon(c, leaving);
				if (c.Hero.HasStealth != stealth) c.Hero.HasStealth = stealth;
			}

			/// <summary>ULDA_BOSS_21p Staking A Claim: the card is a Discover card (its DISCOVER tag).</summary>
			public static bool IsDiscoverCard(IPlayable p) => p?.Card != null && p.Card[GameTag.DISCOVER] == 1;

			/// <summary>The minions of both boards an effect can reach (no dormant run shrine, none already dying), the opponent's board first.</summary>
			public static List<Minion> BothBoards(Controller c) => Touchable(c.Opponent).Concat(Touchable(c)).ToList();

			/// <summary>ULDA_BOSS_22p Power of Wrath: the damaged minions of both boards.</summary>
			public static List<Minion> Damaged(Controller c) => BothBoards(c).Where(m => m.Damage > 0).ToList();

			/// <summary>ULDA_BOSS_23p Hatching Time: the Beasts in <paramref name="c"/>'s hand.</summary>
			public static List<IPlayable> HandBeasts(Controller c) => c.HandZone.Where(p => p is Minion && p.Card.IsRace(Race.BEAST)).ToList();

			/// <summary>
			/// ULDA_BOSS_27p "Retired" Treasure Hunter: the cards of the opponent's hand it may copy (a masked card of a fair-view
			/// projection is never one).
			/// </summary>
			public static List<IPlayable> CopyableHand(Controller c) => c.Opponent.HandZone.Where(p => !(p is Unknown) && p.Card != null).ToList();

			/// <summary>ULDA_BOSS_28p Mighty Windstorm: the board minions of <paramref name="c"/> it may return (no run shrine).</summary>
			public static List<Minion> SwappableBoard(Controller c) => Touchable(c).Where(m => !IsShrine(m)).ToList();

			/// <summary>ULDA_BOSS_28p Mighty Windstorm: the hand minions of <paramref name="c"/> it may put onto the board (a masked card is none).</summary>
			public static List<Minion> SwappableHand(Controller c) => c.HandZone.OfType<Minion>().ToList();

			/// <summary>
			/// ULDA_BOSS_28p Mighty Windstorm, one player's swap: a random minion of the hand goes onto the board in the place of a random
			/// board minion, which returns to the hand (a fresh card). Returns true when a swap happened.
			/// </summary>
			internal static bool Windstorm(Game g, Controller owner, IEntity source)
			{
				var board = SwappableBoard(owner);
				var hand = SwappableHand(owner);
				if (board.Count == 0 || hand.Count == 0) return false;
				if (board.Count > 1 || hand.Count > 1) g.OnRandomHappened(true);
				Minion fromHand = hand.Count == 1 ? hand[0] : hand[g.Random.Next(hand.Count)];
				Minion fromBoard = board.Count == 1 ? board[0] : board[g.Random.Next(board.Count)];
				int position = fromBoard.ZonePosition;
				Generic.RemoveFromZone.Invoke(owner, fromHand);
				if (!Generic.ReturnToHandBlock.Invoke(owner, fromBoard))
				{
					// The board minion did not leave (no run shrine is chosen, so nothing should stop it): the hand minion goes back.
					Generic.AddHandPhase.Invoke(owner, fromHand);
					return false;
				}
				if (owner.BoardZone.IsFull) { Generic.AddHandPhase.Invoke(owner, fromHand); return true; }
				Generic.SummonBlock.Invoke(g, fromHand, Math.Min(position, owner.BoardZone.Count), source);
				return true;
			}

			/// <summary>ULDA_BOSS_28p Mighty Windstorm: a swap is possible for the boss (its own hand and board) or for the opponent (public counts).</summary>
			public static bool WindstormUsable(Controller c) =>
				SwappableHand(c).Count > 0 && SwappableBoard(c).Count > 0 || SwappableBoard(c.Opponent).Count > 0 && c.Opponent.HandZone.Count > 0;

			/// <summary>
			/// ULDA_BOSS_33p Out of Control: the center-most minions of one board (positions over the whole board): the middle one of an
			/// odd count, the two middle ones of an even count.
			/// </summary>
			public static List<Minion> CenterMost(Controller owner)
			{
				var all = owner.BoardZone.ToList();
				if (all.Count == 0) return new List<Minion>();
				if (all.Count % 2 == 1) return new List<Minion> { all[all.Count / 2] };
				return new List<Minion> { all[all.Count / 2 - 1], all[all.Count / 2] };
			}

			/// <summary>ULDA_BOSS_33p Out of Control: the minions it damages (the center-most of each board an effect can reach), the opponent's first.</summary>
			public static List<Minion> OutOfControlTargets(Controller c) =>
				CenterMost(c.Opponent).Concat(CenterMost(c)).Where(m => !m.Untouchable && !m.ToBeDestroyed).ToList();

			/// <summary>ULDA_BOSS_35p Vengeful Construct: the chosen minion and the minions next to it on its board.</summary>
			public static List<Minion> WithNeighbours(Minion m)
			{
				var board = m.Controller.BoardZone;
				var list = new List<Minion>();
				int at = m.ZonePosition;
				if (at > 0) list.Add(board[at - 1]);
				list.Add(m);
				if (at + 1 < board.Count) list.Add(board[at + 1]);
				return list;
			}

			/// <summary>ULDA_BOSS_36p Madness Incarnate: the enemy minions the hero may be made to attack (no Stealth, no Immune, alive).</summary>
			public static List<Minion> MadnessTargets(Controller c) =>
				Touchable(c.Opponent).Where(m => !m.HasStealth && !m.IsImmune && !m.IsDead).ToList();

			// A forced attack of the hero (PastColossalCombat.Force's rule for a minion): the attack is resolved at once, with its
			// triggers and the death pass; the event the power runs in is restored afterwards.
			internal static void ForceHeroAttack(Hero hero, ICharacter target)
			{
				Game g = hero.Game;
				var previous = g.CurrentEventData;
				try { Generic.AttackBlock(hero.Controller, hero, target, true, false); }
				finally { g.CurrentEventData = previous; }
			}

			// A minion summoned for c, with an optional enchantment (Titan Ritual, Beetle Stampede, Mother of Sand, Whispering
			// Sentinel); none on a full board. Returns the minion, or null.
			internal static Minion Summon(Game g, Controller c, IEntity source, string minionId, string enchantmentId = null)
			{
				if (c.BoardZone.IsFull) return null;
				var minion = (Minion)Entity.FromCard(c, Cards.FromId(minionId));
				Generic.SummonBlock.Invoke(g, minion, -1, source);
				if (minion.Zone?.Type != Zone.PLAY || minion.ToBeDestroyed) return null;
				if (enchantmentId != null) Generic.AddEnchantmentBlock(g, Cards.FromId(enchantmentId), (IPlayable)source, minion);
				return minion;
			}
		}

		private static void F20()
		{
			var aMinion = new[] { PlayReq.REQ_TARGET_TO_PLAY, PlayReq.REQ_MINION_TARGET };

			// ---------------- Sothis (ULDA_BOSS_19h)
			// Titan Ritual (2): "Summon two 1/1 Mogu Cultists." As many as fit (summoned: no Battlecry). Needs a free slot.
			Power("ULDA_BOSS_19p", new[] { PlayReq.REQ_NUM_MINION_SLOTS }, () => new CustomTask((g, c, s, t, stack) =>
			{
				for (int i = 0; i < 2; i++) TombsB.Summon(g, c, s, TombsB.MoguCultist);
			}));

			// ---------------- Kham (ULDA_BOSS_20h)
			// Spectral Swordsman (passive): "Your hero is Stealthed while you have a weapon equipped." An equip sets the hero's
			// Stealth; a weapon of the boss's leaving (destroyed, broken or replaced) clears it unless another weapon is equipped.
			// The trigger on the leaving weapon runs at once (fast), while the weapon is still the hero's, so it is left out.
			Passive("ULDA_BOSS_20p", () => new Power
			{
				Trigger = new MultiTrigger(
					new Trigger(TriggerType.EQUIP_WEAPON)
					{
						TriggerSource = TriggerSource.FRIENDLY,
						SingleTask = new CustomTask((g, c, s, t, stack) => TombsB.SetSwordsmanStealth(c)),
					},
					new Trigger(TriggerType.DEATH)
					{
						TriggerSource = TriggerSource.FRIENDLY,
						Condition = new SelfCondition(p => p is Weapon),
						FastExecution = true,
						SingleTask = new CustomTask((g, c, s, t, stack) => TombsB.SetSwordsmanStealth(c, t)),
					}),
			});

			// ---------------- Illidara Sunsdawn (ULDA_BOSS_21h)
			// Staking A Claim (passive): "Whenever a Discover card is played, all minions gain +1/+1." A card of either player with
			// the Discover keyword; every minion of both boards an effect can reach gets ULDA_BOSS_21e.
			Passive("ULDA_BOSS_21p", () => new Power
			{
				Trigger = new Trigger(TriggerType.PLAY_CARD)
				{
					TriggerSource = TriggerSource.ALL,
					Condition = new SelfCondition(TombsB.IsDiscoverCard),
					SingleTask = new CustomTask((g, c, s, t, stack) =>
					{
						foreach (Minion m in TombsB.BothBoards(c))
							Generic.AddEnchantmentBlock(g, Cards.FromId("ULDA_BOSS_21e"), (IPlayable)s, m);
					}),
				},
			});
			Enchantment("ULDA_BOSS_21e", () => new Power { Enchant = new Enchant(Effects.AttackHealth_N(1)) });

			// ---------------- Colossus of the Sun (ULDA_BOSS_22h)
			// Power of Wrath (0): "Deal 1 damage to all damaged minions." The damaged minions of both boards are taken first, then
			// each takes 1. Offered while a damaged minion is on a board.
			Power("ULDA_BOSS_22p", null, () => new CustomTask((g, c, s, t, stack) =>
			{
				foreach (Minion m in TombsB.Damaged(c))
					new DamageTask(1, EntityType.TARGET).Process(g, c, s, m, stack);
			}), usable: c => TombsB.Damaged(c).Count > 0);

			// ---------------- Skarik (ULDA_BOSS_23h)
			// Hatching Time (2): "Reduce the Cost of Beasts in your hand by (1)." (ULDA_BOSS_23e "Costs 1 less.") Offered while the
			// boss holds a Beast.
			Power("ULDA_BOSS_23p", null, () => new CustomTask((g, c, s, t, stack) =>
			{
				foreach (IPlayable beast in TombsB.HandBeasts(c))
					Generic.AddEnchantmentBlock(g, Cards.FromId("ULDA_BOSS_23e"), (IPlayable)s, beast);
			}), usable: c => TombsB.HandBeasts(c).Count > 0);
			Enchantment("ULDA_BOSS_23e", () => new Power { Enchant = new Enchant(Effects.ReduceCost(1)) });

			// ---------------- Suspicious Palm Tree (ULDA_BOSS_24h)
			// Corrupted Oasis (2): "Add two 2/2 Treants to your hand." As many as fit. Offered while the hand has room.
			Power("ULDA_BOSS_24p", null, () => new CustomTask((g, c, s, t, stack) =>
			{
				for (int i = 0; i < 2 && !c.HandZone.IsFull; i++)
					Generic.AddHandPhase.Invoke(c, Entity.FromCard(c, Cards.FromId(TombsB.OasisTreant)));
			}), usable: c => !c.HandZone.IsFull);

			// ---------------- Beetle Herder Zenda (ULDA_BOSS_25h)
			// Beetle Stampede (2): "Summon a Plated Beetle." Needs a free slot.
			Power("ULDA_BOSS_25p", new[] { PlayReq.REQ_NUM_MINION_SLOTS }, () => new CustomTask((g, c, s, t, stack) =>
				TombsB.Summon(g, c, s, TombsB.PlatedBeetle)));

			// ---------------- Thudd Lockspring (ULDA_BOSS_26h)
			// Gatling Wand (2): "Deal 2 damage split among enemy minions." Two 1-damage hits, each on a random enemy minion still
			// alive (Arcane Missiles' rule, minions only). Offered while the opponent has a minion an effect can reach.
			Power("ULDA_BOSS_26p", null, () => SplitHits.Inline(2, ComplexTask.DamageRandomTargets(1, EntityType.OP_MINIONS, 1), deathProcessing: false),
				usable: c => Touchable(c.Opponent).Any());

			// ---------------- Ol' Toomba (ULDA_BOSS_27h)
			// "Retired" Treasure Hunter (2): "Copy a card from your opponent's hand and add it to your hand." A random card of the
			// opponent's hand, as a fresh copy (marked a copy of an opponent's card). Offered while the boss's hand has room and the
			// opponent's hand (a public count) holds a card.
			Power("ULDA_BOSS_27p", null, () => new CustomTask((g, c, s, t, stack) =>
			{
				if (c.HandZone.IsFull) return;
				var cards = TombsB.CopyableHand(c);
				if (cards.Count == 0) return;
				if (c.Opponent.HandZone.Count > 1) g.OnRandomHappened(true);
				IPlayable pick = cards.Count == 1 ? cards[0] : cards[g.Random.Next(cards.Count)];
				IPlayable copy = Entity.FromCard(c, pick.Card);
				PayloadRules.MarkCopy(c.Opponent, copy);
				Generic.AddHandPhase.Invoke(c, copy);
			}), usable: c => !c.HandZone.IsFull && c.Opponent.HandZone.Count > 0);

			// ---------------- Sazin (ULDA_BOSS_28h)
			// Mighty Windstorm (0): "Each player swaps a minion from their hand with one on their battlefield." The boss first, then
			// the opponent (TombsB.Windstorm: a random hand minion onto the board in a random board minion's place; no run shrine).
			Power("ULDA_BOSS_28p", null, () => new CustomTask((g, c, s, t, stack) =>
			{
				TombsB.Windstorm(g, c, s);
				if (g.State != State.COMPLETE) TombsB.Windstorm(g, c.Opponent, s);
			}), usable: TombsB.WindstormUsable);

			// ---------------- Ozara (ULDA_BOSS_29h)
			// Mother of Sand (passive): "After you Overload, summon a Dust Devil." When a card of the boss's Overloads (the Overload
			// event of Generic.OverloadBlock, raised as the card is played).
			Passive("ULDA_BOSS_29p", () => new Power
			{
				Trigger = new Trigger(TriggerType.OVERLOAD)
				{
					TriggerSource = TriggerSource.FRIENDLY,
					// Resolved in place, as TRLA_158 Overcharged Totem's (a task queued by the Overload event is not run): the
					// Overload is raised as the card is played, before its effect resolves.
					FastExecution = true,
					SingleTask = new CustomTask((g, c, s, t, stack) =>
					{
						if (g.State != State.COMPLETE) TombsB.Summon(g, c, s, TombsB.DustDevil);
					}),
				},
			});

			// ---------------- Ermavar (ULDA_BOSS_30h)
			// Fireshaper (passive): "After you cast a spell, deal $2 damage randomly split among all enemies." Flamewaker's trigger.
			Passive("ULDA_BOSS_30p", () => new Power
			{
				Trigger = new Trigger(TriggerType.AFTER_CAST)
				{
					TriggerSource = TriggerSource.FRIENDLY,
					SingleTask = new EnqueueTask(2, ComplexTask.DamageRandomTargets(1, EntityType.ENEMIES, 1)),
				},
			});

			// ---------------- Pillager Drasar (ULDA_BOSS_31h)
			// Pillaged Relics (2): "Choose a minion. Give it +1/+1, Divine Shield or Taunt at random." Any minion.
			Power("ULDA_BOSS_31p", aMinion, () => new CustomTask((g, c, s, t, stack) =>
			{
				if (!(t is Minion m) || m.Zone?.Type != Zone.PLAY) return;
				g.OnRandomHappened(true);
				switch (g.Random.Next(3))
				{
					case 0: Generic.AddEnchantmentBlock(g, Cards.FromId("ULDA_BOSS_31e1"), (IPlayable)s, m); break;
					case 1: new SetGameTagTask(GameTag.DIVINE_SHIELD, 1, EntityType.TARGET).Process(g, c, s, m, stack); break;
					default: new SetGameTagTask(GameTag.TAUNT, 1, EntityType.TARGET).Process(g, c, s, m, stack); break;
				}
			}));
			Enchantment("ULDA_BOSS_31e1", () => new Power { Enchant = new Enchant(Effects.AttackHealth_N(1)) });

			// ---------------- Edra (ULDA_BOSS_32h)
			// Whispering Sentinel (1): "Summon a 1/1 Wisp. Give it Lifesteal." Needs a free slot.
			Power("ULDA_BOSS_32p", new[] { PlayReq.REQ_NUM_MINION_SLOTS }, () => new CustomTask((g, c, s, t, stack) =>
				TombsB.Summon(g, c, s, TombsB.SentinelWisp, TombsB.SentinelLifesteal)));

			// ---------------- Runaway Ruby (ULDA_BOSS_33h)
			// Out of Control (1): "Deal 1 damage to the Center-most minions." The center-most minions of each board (TombsB.CenterMost).
			// Offered while one of them can be damaged.
			Power("ULDA_BOSS_33p", null, () => new CustomTask((g, c, s, t, stack) =>
			{
				foreach (Minion m in TombsB.OutOfControlTargets(c))
					new DamageTask(1, EntityType.TARGET).Process(g, c, s, m, stack);
			}), usable: c => TombsB.OutOfControlTargets(c).Count > 0);

			// ---------------- Dovo Fastfuse (ULDA_BOSS_34h)
			// Tomb Boomin' (2): "Deal 4 damage split randomly between all characters." Four 1-damage hits on random characters of
			// either side still alive, both heroes included (Mad Bomber's rule with the boss's own hero in the pool).
			Power("ULDA_BOSS_34p", null, () => SplitHits.Inline(4, ComplexTask.DamageRandomTargets(1, EntityType.ALL, 1), deathProcessing: false));

			// ---------------- Earthrager Ptah (ULDA_BOSS_35h)
			// Vengeful Construct (3): "Choose a minion with 2 or less Health. Destroy it and its neighbors." A minion of either side;
			// it and the minions next to it on its board are destroyed (a dormant run shrine is left as it is).
			Add("ULDA_BOSS_35p", EncounterCardKind.Power, () => new CardDef(Reqs(aMinion), new Power
			{
				PowerTask = new CustomTask((g, c, s, t, stack) =>
				{
					if (!(t is Minion chosen) || chosen.Zone?.Type != Zone.PLAY) return;
					foreach (Minion m in TombsB.WithNeighbours(chosen))
						if (!m.Untouchable && !m.ToBeDestroyed) m.Destroy();
				}),
			}), card => card.SourceTargetingPredicate = (source, target) => target is Minion m && m.Health <= TombsB.ConstructMaxHealth);

			// ---------------- Kasa Softspeaker (ULDA_BOSS_36h)
			// Madness Incarnate (1): "Gain +1 Attack this turn and attack 3 different random enemy minions." The hero gets
			// ULDA_BOSS_36e, then attacks up to three different random enemy minions one after another (forced attacks), stopping
			// when either hero dies or no new target is left. Offered while there is an enemy minion it can attack.
			Power("ULDA_BOSS_36p", null, () => new CustomTask((g, c, s, t, stack) =>
			{
				Hero hero = c.Hero;
				Generic.AddEnchantmentBlock(g, Cards.FromId("ULDA_BOSS_36e"), (IPlayable)s, hero);
				var attacked = new HashSet<int>();
				for (int i = 0; i < TombsB.MadnessAttacks; i++)
				{
					if (g.State == State.COMPLETE || hero.IsDead || hero.ToBeDestroyed || c.Opponent.Hero.IsDead || c.Opponent.Hero.ToBeDestroyed) break;
					var pool = TombsB.MadnessTargets(c).Where(m => !attacked.Contains(m.Id)).ToList();
					if (pool.Count == 0) break;
					if (pool.Count > 1) g.OnRandomHappened(true);
					Minion victim = pool.Count == 1 ? pool[0] : pool[g.Random.Next(pool.Count)];
					attacked.Add(victim.Id);
					TombsB.ForceHeroAttack(hero, victim);
				}
			}), usable: c => TombsB.MadnessTargets(c).Count > 0);
			Enchantment("ULDA_BOSS_36e", () => new Power { Enchant = new Enchant(Effects.Attack_N(1)) { IsOneTurnEffect = true } });
		}
	}
}
