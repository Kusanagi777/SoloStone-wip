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
using SabberStoneCore.src.Loader;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.DungeonRun
{
	/// <summary>
	/// Phase 7 T2: the Priest, Rogue, Shaman, Warlock and Warrior teammates (the run's teammate draft offers them on a board-shrine
	/// run) and their client enchantment records (TRLA_153e, TRLA_174e, TRLA_176e). Each script sits on the client card itself.
	/// The start-of-game companions (TRLA_148, TRLA_188, TRLA_156, TRLA_180, TRLA_172) are phase 7 D's. Every random choice uses
	/// the game RNG and every random card comes through the run's generation filter (RandomCardTask.GetCardList:
	/// GameConfig.ExcludedGeneratedIds); every piece of state is an entity tag.
	/// </summary>
	public static partial class DungeonRunShrines
	{
		/// <summary>TRLA_191 Sharktoothed Harpooner's discount: the client's GBL_008e "Costs (4) less." (the client has no TRLA_191e).</summary>
		internal const string HarpoonerDiscount = "GBL_008e";
		/// <summary>TRLA_192 Lobstrok Tastetester's discount on the drawn spell: the client's LOOT_204e "Costs (2) less."</summary>
		internal const string TastetesterDiscount = "LOOT_204e";
		/// <summary>TRLA_175 Seasoned Pitbrawler.</summary>
		public const string Pitbrawler = "TRLA_175";
		/// <summary>TRLA_157 Leap Frog's damage per hit.</summary>
		public const int LeapFrogDamage = 3;
		/// <summary>
		/// TRLA_157 Leap Frog's hit bound: a kill takes its target out of the next pick, so the enemy board (7 minions) bounds the
		/// repeats at 8 hits; the bound is stated so the loop can never run away.
		/// </summary>
		public const int LeapFrogMaxHits = 8;
		/// <summary>TRLA_160 Naga Tonguelasher's Overload on the opponent.</summary>
		public const int TonguelasherOverload = 3;
		/// <summary>TRLA_177 Unbound Punisher's Armor per destroyed minion.</summary>
		public const int PunisherArmor = 2;

		private static void AddT2(Dictionary<string, CardDef> cards)
		{
			// ---------------- Priest
			// TRLA_149 Ghastcoiler: "Deathrattle: Summon 2 random Deathrattle minions." Two random minions with a Deathrattle (game
			// RNG, the run's generation filter), each at the Ghastcoiler's last position; nothing once the board is full.
			cards.Add("TRLA_149", Implemented(new CardDef(new Power
			{
				DeathrattleTask = new CustomTask((g, c, s, t, stack) =>
					SummonRandom(g, c, s, 2, p => p.Type == CardType.MINION && p.Tags.ContainsKey(GameTag.DEATHRATTLE))),
			})));
			// TRLA_150 Bloodwash Medic: "Rush. Overkill: Add 3 healing spells to your hand." Three random spells that restore Health
			// (any class: the text names none; the client lists no pool, so a healing spell is a spell whose text restores
			// Health), through the run's generation filter; a full hand takes no more.
			cards.Add("TRLA_150", Implemented(new CardDef(new Power
			{
				OverkillTask = new CustomTask((g, c, s, t, stack) => AddRandomToHand(g, c, s, 3, IsHealingSpell)),
			})));
			// TRLA_151 Bwonsamdi's Keeper: "Battlecry: Restore all minions to full Health." (both sides: a heal of each minion's
			// damage; an undamaged minion is not healed).
			cards.Add("TRLA_151", Implemented(new CardDef(new Power
			{
				PowerTask = new HealFullTask(EntityType.ALLMINIONS),
			})));
			// TRLA_152 Soulsapper: "Deathrattle: Steal 2 spells from your opponent's deck." Two random spells of the opponent's deck
			// (game RNG) move to the owner's hand by the deck-steal rule of TRLA_186 (TakeFromDeck: a deck exit and a transfer whose
			// definition only the new owner sees; a full hand burns it). Fewer spells, fewer steals.
			cards.Add("TRLA_152", Implemented(new CardDef(new Power
			{
				DeathrattleTask = new CustomTask((g, c, s, t, stack) =>
				{
					for (int i = 0; i < 2 && g.State != State.COMPLETE; i++)
						if (StealFromDeck(g, c, s, p => p.Card.Type == CardType.SPELL) == null) break;
				}),
			})));
			// TRLA_153 Conjuring Attendant: "Your spells cost (2) less." (a hand aura of TRLA_153e "Costs (2) less."; floor (0)).
			cards.Add("TRLA_153", Implemented(new CardDef(new Power
			{
				Aura = new Aura(AuraType.HAND, "TRLA_153e") { Condition = SelfCondition.IsSpell },
			})));
			cards.Add("TRLA_153e", Implemented(new CardDef(new Power { Enchant = new Enchant(Effects.ReduceCost(2)) })));

			// ---------------- Rogue
			// TRLA_189 Parrot Mascot: "Combo: Add a copy of each card you've played this turn to your hand." Every card the owner
			// played this turn, in play order, the Mascot itself included (the text has no "other"), as a fresh copy of its card;
			// a full hand takes no more. Combo from the card data (the Combo branch runs only after another card this turn).
			cards.Add("TRLA_189", Implemented(new CardDef(new Power
			{
				ComboTask = new CustomTask((g, c, s, t, stack) =>
				{
					foreach (var played in c.CardsPlayedThisTurn.ToArray())
					{
						if (c.HandZone.IsFull || g.State == State.COMPLETE) break;
						Generic.AddHandPhase.Invoke(c, Entity.FromCard(c, played, new EntityData
						{
							[GameTag.CREATOR] = s.Id, [GameTag.DISPLAYED_CREATOR] = s.Id,
						}));
					}
				}),
			})));
			// TRLA_190 Salty Looter: "Battlecry and Deathrattle: Steal a card from your opponent's hand." A random card of the
			// opponent's hand (game RNG) moves to the owner's by the hand-to-hand theft rule of Gloves of Mugging (StealFromHand:
			// costs kept, a full hand burns it by the ordinary hand rule).
			cards.Add("TRLA_190", Implemented(new CardDef(new Power
			{
				PowerTask = new CustomTask((g, c, s, t, stack) => StealFromHand(g, c, s)),
				DeathrattleTask = new CustomTask((g, c, s, t, stack) => StealFromHand(g, c, s)),
			})));
			// TRLA_191 Sharktoothed Harpooner: "Battlecry: If you're holding any cards that didn't start in your deck, reduce their
			// Cost by (4)." Each card in the owner's hand that did not start in their deck (SelfCondition.IsNotStartInDeck: the
			// history's frozen starting decks, so it holds for any run deck size; never an entity-id bound) gets GBL_008e.
			cards.Add("TRLA_191", Implemented(new CardDef(new Power
			{
				PowerTask = new CustomTask((g, c, s, t, stack) =>
				{
					var discount = Cards.FromId(HarpoonerDiscount);
					foreach (var p in c.HandZone.ToArray())
						if (SelfCondition.IsNotStartInDeck.Eval(p)) Generic.AddEnchantmentBlock(g, discount, (IPlayable)s, p);
				}),
			})));
			cards.Add(HarpoonerDiscount, Implemented(new CardDef(new Power { Enchant = new Enchant(Effects.ReduceCost(4)) })));
			// TRLA_192 Lobstrok Tastetester: "After you cast a spell, draw a spell from your deck. It costs (2) less." A random spell of
			// the owner's deck (game RNG) and LOOT_204e on it when it reaches the hand (a full hand burns it, undiscounted). An
			// effect's cast raises no "after you cast" event (hand casts only), so a recast (TRLA_187) draws nothing more.
			cards.Add("TRLA_192", Implemented(new CardDef(new Power
			{
				Trigger = new Trigger(TriggerType.AFTER_CAST)
				{
					TriggerSource = TriggerSource.FRIENDLY,
					SingleTask = new CustomTask((g, c, s, t, stack) =>
					{
						if (!(s is Minion me) || !Active(me) || g.State == State.COMPLETE) return;
						var drawn = DrawRandom(g, c, p => p.Card.Type == CardType.SPELL);
						if (drawn != null && drawn.Zone == c.HandZone) Generic.AddEnchantmentBlock(g, Cards.FromId(TastetesterDiscount), me, drawn);
					}),
				},
			})));
			// TRLA_193 Raging Contender: "Rush. Deathrattle: Fill your board with random Pirates." Random Pirate minions (game RNG,
			// the run's generation filter) at its last position until the board is full (Rush from the card data).
			cards.Add("TRLA_193", Implemented(new CardDef(new Power
			{
				DeathrattleTask = new CustomTask((g, c, s, t, stack) =>
					SummonRandom(g, c, s, int.MaxValue, p => p.Type == CardType.MINION && p.IsRaceForLegacyRandomPool(Race.PIRATE))),
			})));

			// ---------------- Shaman
			// TRLA_157 Leap Frog: "Battlecry: Deal 3 damage to a random enemy minion. If it dies, repeat this." A random enemy minion
			// in play (the untouchable dormant forms and minions already dying excluded; game RNG) takes 3 from the Frog; a kill
			// (it is now to be destroyed: a run shrine going dormant included) repeats it, at most LeapFrogMaxHits times.
			cards.Add("TRLA_157", Implemented(new CardDef(new Power
			{
				PowerTask = new CustomTask((g, c, s, t, stack) =>
				{
					if (!(s is IPlayable me)) return;
					for (int hit = 0; hit < LeapFrogMaxHits && g.State != State.COMPLETE; hit++)
					{
						var enemies = c.Opponent.BoardZone.GetAll(m => !m.ToBeDestroyed);
						if (enemies.Length == 0) return;
						if (enemies.Length > 1) g.OnRandomHappened(true);
						var victim = enemies[g.Random.Next(enemies.Length)];
						Generic.DamageCharFunc(me, victim, LeapFrogDamage, false);
						if (!victim.ToBeDestroyed) return;
					}
				}),
			})));
			// TRLA_158 Overcharged Totem: "After you Overload, deal 2 damage to all enemy minions." (the owner's Overload event,
			// Generic.OverloadBlock, raised only for a card with Overload).
			cards.Add("TRLA_158", Implemented(new CardDef(new Power
			{
				Trigger = new Trigger(TriggerType.OVERLOAD)
				{
					TriggerSource = TriggerSource.FRIENDLY,
					// Resolved in place, as TRLA_109's (a task queued by the Overload event is not run).
					FastExecution = true,
					SingleTask = new CustomTask((g, c, s, t, stack) =>
					{
						if (!(s is Minion me) || !Active(me) || g.State == State.COMPLETE) return;
						g.TaskQueue.Execute(new DamageTask(2, EntityType.OP_MINIONS), c, me, null);
					}),
				},
			})));
			// TRLA_159 Croak Jouster: "Battlecry: Discover a spell. If you're Overloaded, keep all 3." Overloaded: locked or owed
			// Overloaded Mana Crystals (as Likkim's TRL_352), read when the Battlecry resolves.
			cards.Add("TRLA_159", Implemented(new CardDef(new Power
			{
				PowerTask = new CustomTask((g, c, s, t, stack) =>
				{
					bool overloaded = c.OverloadLocked > 0 || c.OverloadOwed > 0;
					g.TaskQueue.Execute(overloaded ? KeepAllSpells : DiscoverSpell, c, (IPlayable)s, null);
				}),
			})));
			// TRLA_160 Naga Tonguelasher: "Battlecry: Overload 3 of your opponent's Mana Crystals." The opponent owes 3 more Overload
			// (locked on their next turn). It is not the opponent's own Overload, so no "after you Overload" fires for either side.
			cards.Add("TRLA_160", Implemented(new CardDef(new Power
			{
				PowerTask = new CustomTask((g, c, s, t, stack) =>
				{
					var op = c.Opponent;
					op.OverloadOwed += TonguelasherOverload;
					op.OverloadThisGame += TonguelasherOverload;
				}),
			})));
			// TRLA_161 War Heralder: "Rush. Overkill: Add 3 random Shaman spells to your hand." (the run's generation filter; a full
			// hand takes no more).
			cards.Add("TRLA_161", Implemented(new CardDef(new Power
			{
				OverkillTask = new CustomTask((g, c, s, t, stack) =>
					AddRandomToHand(g, c, s, 3, p => p.Type == CardType.SPELL && p.Class == CardClass.SHAMAN)),
			})));

			// ---------------- Warlock
			// TRLA_181 Ravenous Familiar: "Rush. Overkill: Discard your lowest Cost card and draw a card." The lowest current Cost in
			// the owner's hand (ties: game RNG) is discarded (a discard: TRLA_178 Dark Reliquary sees it), then a card is drawn; an
			// empty hand discards nothing and still draws.
			cards.Add("TRLA_181", Implemented(new CardDef(new Power
			{
				OverkillTask = new CustomTask((g, c, s, t, stack) =>
				{
					if (g.State == State.COMPLETE) return;
					var hand = c.HandZone.ToArray();
					if (hand.Length > 0)
					{
						int low = hand.Min(p => p.Cost);
						var lowest = hand.Where(p => p.Cost == low).ToArray();
						if (lowest.Length > 1) g.OnRandomHappened(true);
						Generic.DiscardBlock.Invoke(c, lowest[g.Random.Next(lowest.Length)]);
					}
					if (g.State != State.COMPLETE) Generic.Draw(c);
				}),
			})));
			// TRLA_182 Vile Necrodoctor: "Lifesteal. Battlecry: Deal 3 damage to all enemy minions." (Lifesteal from the card data:
			// the Battlecry's damage heals the owner's hero).
			cards.Add("TRLA_182", Implemented(new CardDef(new Power
			{
				PowerTask = new DamageTask(3, EntityType.OP_MINIONS),
			})));
			// TRLA_183 Warp Stalker: "Stealth. You can use your Hero Power any number of times." The owner's Hero Power is ready again
			// after each use on the owner's turn (the Garrison Commander path, an Inspire refresh) and when the Stalker arrives on
			// the owner's turn with the power spent (Stealth from the card data). Silence or its death ends it; a refresh already
			// given stays for that turn.
			cards.Add("TRLA_183", Implemented(new CardDef(new Power
			{
				Trigger = new MultiTrigger(
					new Trigger(TriggerType.INSPIRE)
					{
						TriggerSource = TriggerSource.FRIENDLY,
						SingleTask = new CustomTask((g, c, s, t, stack) => { if (s is Minion me && Active(me)) ReadyHeroPower(c); }),
					},
					new Trigger(TriggerType.AFTER_SUMMON)
					{
						TriggerSource = TriggerSource.SELF,
						SingleTask = new CustomTask((g, c, s, t, stack) => { if (s is Minion me && Active(me) && g.CurrentPlayer == c) ReadyHeroPower(c); }),
					}),
			})));
			// TRLA_184 Dark Diviner: "Battlecry: Fill your hand with random spells." Random spells of any class (the text names
			// none), through the run's generation filter, until the hand is full.
			cards.Add("TRLA_184", Implemented(new CardDef(new Power
			{
				PowerTask = new CustomTask((g, c, s, t, stack) => AddRandomToHand(g, c, s, int.MaxValue, p => p.Type == CardType.SPELL)),
			})));
			// TRLA_185 Blood Portal: "Whenever your hero takes damage, add a random Warlock spell to your hand." Each hit on the
			// owner's hero (its damage event), either turn; paying Health (TRLA_113 Blood Pact) is no damage, and a hit Hir'eek's
			// Hunger (TRLA_179) sends to the enemy hero never reaches the owner's. The spell comes through the run's generation filter.
			cards.Add("TRLA_185", Implemented(new CardDef(new Power
			{
				Trigger = new Trigger(TriggerType.TAKE_DAMAGE)
				{
					TriggerSource = TriggerSource.HERO,
					SingleTask = new CustomTask((g, c, s, t, stack) =>
					{
						if (s is Minion me && Active(me)) AddRandomToHand(g, c, me, 1, p => p.Type == CardType.SPELL && p.Class == CardClass.WARLOCK);
					}),
				},
			})));

			// ---------------- Warrior
			// TRLA_173 Redcrest Rocker: "Rush. Overkill: Discover a Dragon. If you control a Dragon, keep all 3." (a Dragon minion on
			// the owner's board when the Overkill resolves; the Rocker is none).
			cards.Add("TRLA_173", Implemented(new CardDef(new Power
			{
				OverkillTask = new CustomTask((g, c, s, t, stack) =>
				{
					bool dragon = c.BoardZone.GetAll(m => m.Card.IsRace(Race.DRAGON)).Length > 0;
					g.TaskQueue.Execute(dragon ? KeepAllDragons : DiscoverDragon, c, (IPlayable)s, null);
				}),
			})));
			// TRLA_174 Ironhorn Drummer: "Your minions can't be reduced below 1 Health on your turn." A board aura of TRLA_174e
			// (the Drummer included; the untouchable dormant forms take no damage anyway), whose damage clamp (Commanding Shout's
			// NEW1_036e PREDAMAGE trigger) acts only while its minion's owner is the current player.
			cards.Add("TRLA_174", Implemented(new CardDef(new Power
			{
				Aura = new Aura(AuraType.BOARD, "TRLA_174e"),
			})));
			cards.Add("TRLA_174e", Implemented(new CardDef(new Power
			{
				Enchant = new Enchant(Array.Empty<IEffect>()),
				Trigger = new Trigger(TriggerType.PREDAMAGE)
				{
					TriggerSource = TriggerSource.ENCHANTMENT_TARGET,
					FastExecution = true,
					SingleTask = new CustomTask((g, c, s, t, stack) =>
					{
						if (!(s is Enchantment e) || !(e.Target is Minion m) || g.CurrentPlayer != m.Controller) return;
						var data = g.CurrentEventData;
						if (data != null && data.EventNumber >= m.Health) data.EventNumber = Math.Max(0, m.Health - 1);
					}),
				},
			})));
			// TRLA_175 Seasoned Pitbrawler: "Whenever you gain Armor, gain twice as much." A replacement in Character.GainArmor
			// (ArmorGain): the owner's hero gains twice the Armor in one gain (one ARMOR event, so "whenever you gain Armor" answers
			// once), per awake unsilenced Pitbrawler (two give four times as much).
			cards.Add(Pitbrawler, Implemented(new CardDef(new Power())));
			// TRLA_176 Herald of Flame: "Whenever you play a Dragon, give it Rush and draw a Dragon." Another Dragon the owner plays
			// (not the Herald, itself a Dragon) gets TRLA_176e and a random Dragon of the owner's deck is drawn.
			cards.Add("TRLA_176", Implemented(new CardDef(new Power
			{
				Trigger = new Trigger(TriggerType.PLAY_MINION)
				{
					TriggerSource = TriggerSource.FRIENDLY,
					Condition = SelfCondition.IsRace(Race.DRAGON),
					SingleTask = new CustomTask((g, c, s, t, stack) =>
					{
						if (!(s is Minion me) || !Active(me) || !(t is Minion dragon) || dragon == me || g.State == State.COMPLETE) return;
						Generic.AddEnchantmentBlock(g, Cards.FromId("TRLA_176e"), me, dragon);
						DrawRandom(g, c, p => p.Card.IsRace(Race.DRAGON));
					}),
				},
			})));
			cards.Add("TRLA_176e", Implemented(new CardDef(new Power { Enchant = new Enchant(Effects.Rush) })));
			// TRLA_177 Unbound Punisher: "Battlecry: Destroy all enemy minions. Gain 2 Armor for each minion destroyed." The enemy
			// minions in play that are not already dying (an enemy run shrine going dormant is destroyed and counts; the
			// untouchable dormant forms are not minions to destroy) are destroyed; then 2 Armor each, in one gain.
			cards.Add("TRLA_177", Implemented(new CardDef(new Power
			{
				PowerTask = new CustomTask((g, c, s, t, stack) =>
				{
					var victims = c.Opponent.BoardZone.GetAll(m => !m.ToBeDestroyed);
					foreach (var m in victims) { DeathContextRules.RecordDestroy(m, s); m.Destroy(); }
					if (victims.Length > 0) c.Hero.GainArmor((IPlayable)s, PunisherArmor * victims.Length);
				}),
			})));
		}

		private static readonly ISimpleTask DiscoverSpell = new DiscoverTask(DiscoverType.SPELL);
		private static readonly ISimpleTask KeepAllSpells = new DiscoverTask(DiscoverType.SPELL, keepAllCondition: _ => true);
		private static readonly ISimpleTask DiscoverDragon = new DiscoverTask(DiscoverType.DRAGON);
		private static readonly ISimpleTask KeepAllDragons = new DiscoverTask(DiscoverType.DRAGON, keepAllCondition: _ => true);

		/// <summary>A spell whose text restores Health (TRLA_150 Bloodwash Medic's "healing spells").</summary>
		internal static bool IsHealingSpell(Card p) => p.Type == CardType.SPELL && p.Text != null
			&& p.Text.IndexOf("Restore", StringComparison.Ordinal) >= 0 && p.Text.IndexOf("Health", StringComparison.Ordinal) >= 0;

		/// <summary>
		/// The Armor <paramref name="character"/> gains instead of <paramref name="armor"/> (Character.GainArmor): twice as much per
		/// awake, unsilenced TRLA_175 Seasoned Pitbrawler (or a plain copy, which keeps the text) on its owner's board, when it is
		/// its owner's hero.
		/// </summary>
		internal static int ArmorGain(ICharacter character, int armor)
		{
			if (armor <= 0 || !(character is Hero hero) || hero.Controller?.Hero != hero) return armor;
			var board = hero.Controller.BoardZone;
			for (int i = 0; i < board.Count; i++)
			{
				var m = board[i];
				if (m.Card.Id == Pitbrawler && !m.IsSilenced && Active(m)) armor = armor > int.MaxValue / 2 ? int.MaxValue : armor * 2;
			}
			return armor;
		}

		/// <summary>TRLA_183 Warp Stalker: <paramref name="c"/>'s spent Hero Power is ready again.</summary>
		private static void ReadyHeroPower(Controller c)
		{
			var power = c.Hero?.HeroPower;
			if (power != null && power.IsExhausted && c.Game.State != State.COMPLETE) power.IsExhausted = false;
		}

		/// <summary>
		/// Summons up to <paramref name="count"/> random minions matching <paramref name="match"/> from the game's generation pool
		/// (game RNG; the run's generation filter applies) for <paramref name="c"/> at <paramref name="source"/>'s last position, until
		/// the board is full. Returns how many were summoned.
		/// </summary>
		internal static int SummonRandom(Game g, Controller c, IEntity source, int count, Func<Card, bool> match)
		{
			var pool = RandomCardTask.GetCardList(source, CardType.MINION).Where(match).ToArray();
			int summoned = 0, alternate = 0;
			for (int i = 0; i < count && pool.Length > 0 && !c.BoardZone.IsFull && g.State != State.COMPLETE; i++)
			{
				var minion = (Minion)Entity.FromCard(c, pool[g.Random.Next(pool.Length)], new EntityData { [GameTag.DISPLAYED_CREATOR] = source.Id });
				g.OnRandomHappened(true);
				int position = SummonTask.GetPosition(source, SummonSide.DEATHRATTLE, 0, ref alternate);
				if (position > c.BoardZone.SlotCount) position = c.BoardZone.SlotCount;
				Generic.SummonBlock.Invoke(g, minion, position, (IPlayable)source);
				summoned++;
			}
			return summoned;
		}

		/// <summary>
		/// Adds up to <paramref name="count"/> random cards matching <paramref name="match"/> from the game's generation pool (game
		/// RNG; the run's generation filter applies) to <paramref name="c"/>'s hand, until it is full. Returns how many were added.
		/// </summary>
		internal static int AddRandomToHand(Game g, Controller c, IEntity source, int count, Func<Card, bool> match)
		{
			var pool = RandomCardTask.GetCardList(source).Where(match).ToArray();
			int added = 0;
			for (int i = 0; i < count && pool.Length > 0 && !c.HandZone.IsFull && g.State != State.COMPLETE; i++)
			{
				var card = Entity.FromCard(c, pool[g.Random.Next(pool.Length)], new EntityData
				{
					[GameTag.CREATOR] = source.Id, [GameTag.DISPLAYED_CREATOR] = source.Id,
				});
				g.OnRandomHappened(true);
				Generic.AddHandPhase.Invoke(c, card);
				added++;
			}
			return added;
		}

		/// <summary>
		/// Steals a random card of the opponent's deck that matches (game RNG) into <paramref name="c"/>'s hand (TakeFromDeck: a full
		/// hand burns it). Returns the stolen card, or null when no card of the deck matches.
		/// </summary>
		internal static IPlayable StealFromDeck(Game g, Controller c, IEntity source, Func<IPlayable, bool> match)
		{
			var op = c.Opponent;
			if (g.State == State.COMPLETE) return null;
			var pool = op.DeckZone.Where(match).ToArray();
			if (pool.Length == 0) return null;
			var stolen = pool[g.Random.Next(pool.Length)];
			g.OnRandomHappened(true);
			TakeFromDeck(g, c, stolen, source);
			return stolen;
		}

		/// <summary>
		/// Moves <paramref name="stolen"/>, a card of the opponent's deck, to <paramref name="c"/>'s hand (TRLA_186, TRLA_152): the
		/// history records its deck exit and a transfer to the new owner's hand, whose definition only the new owner sees; its
		/// enchantments change sides with it. A full hand burns it by the ordinary hand rule. Returns true when it entered the hand.
		/// </summary>
		internal static bool TakeFromDeck(Game g, Controller c, IPlayable stolen, IEntity source)
		{
			var op = c.Opponent;
			bool entered;
			using (g.Facts.CardTransfer(stolen, op.Id, Zone.DECK, source))
			{
				op.DeckZone.Remove(stolen);
				stolen.Controller = c;
				stolen[GameTag.CONTROLLER] = c.PlayerId;
				if (stolen.AppliedEnchantments != null)
					foreach (var effect in stolen.AppliedEnchantments) { effect.Controller = c; effect[GameTag.CONTROLLER] = c.PlayerId; }
				entered = Generic.AddHandPhase(c, stolen);
			}
			return entered && stolen.Zone == c.HandZone;
		}

		/// <summary>
		/// TRLA_190 Salty Looter: a random card of the opponent's hand (game RNG) moves to <paramref name="c"/>'s hand by the
		/// hand-to-hand theft rule of Gloves of Mugging and Rat Burglar (the transfer facts, costs kept, its adaptive cost dropped;
		/// a full hand burns it). Returns the stolen card, or null on an empty hand.
		/// </summary>
		internal static IPlayable StealFromHand(Game g, Controller c, IEntity source)
		{
			var op = c.Opponent;
			if (op.HandZone.IsEmpty || g.State == State.COMPLETE) return null;
			var stolen = op.HandZone[g.Random.Next(op.HandZone.Count)];
			g.OnRandomHappened(true);
			using var historyTransfer = g.Facts.CardTransfer(stolen, op.Id, Zone.HAND, source);
			using var temporaryTransfer = TemporaryRules.PreserveHandTransfer(stolen);
			if (stolen.OngoingEffect is AdaptiveCostEffect costEffect) costEffect.Remove();
			op.HandZone.RemoveForTrade(stolen);
			stolen.Controller = c;
			stolen[GameTag.CONTROLLER] = c.PlayerId;
			Generic.AddHandPhase(c, stolen);
			return stolen;
		}
	}
}
