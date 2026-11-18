using System;
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
	// Phase 8, family 14: Dalaran Heist bosses A (DALA_BOSS_01 to 15). Fifteen encounters and their boss-only cards: Chomper
	// (Chomp), Awilo (The Feast), The Great Akazamzarak (Prestidigitation and the four magical feats), Kaye Toogie (Open
	// Wormhole), The Amazing "Bonepaw" (Mana Echoes), Dazzik "Hellscream" ("Armor Up" and the hero card "Scourgelord" Dazzik),
	// Flight Master Belnaara (Take Flight! and Flying!, a power-only script form), Applebough (Apple Toss), Archivist Oshi (Repeat
	// History), Mo Eniwhiskers (Street Smarts), Noz Timbertail (Backstabber), Cravitz Lorent (Forbidden Love), Albin Eastoft
	// (Slice and Dice), Tierra Blythe (Order Up! and the "Kind" Waitress) and Moon Priestess Nici (Blessing of Elune). The texts
	// are the client's (facts/violet-hold-2026-09.json). A clickable power that would do nothing is not offered, by a condition
	// that reads only what is exact in the boss's fair view (the boards, the graveyards, its own hand, the public play history);
	// every random choice uses the game RNG and is reported as a random event; the only state is the model's form tags and the
	// public counters the client shows on Belnaara's powers (TAG_SCRIPT_DATA_NUM_1). A run player's board shrine is never
	// transformed or taken: a transform or destroy aimed at one makes it go dormant (Actions.ShrineRules), and a dormant form is
	// never picked (it is untouchable).
	public static partial class DungeonRunEncounters
	{
		/// <summary>DALA_BOSS_03p Prestidigitation: the four magical feats it adds, each with the same chance.</summary>
		public static readonly IReadOnlyList<string> MagicalFeats = new[] { "DALA_BOSS_03t", "DALA_BOSS_03t2", "DALA_BOSS_03t3", "DALA_BOSS_03t4" };
		/// <summary>DALA_BOSS_03t2 Bunnifitronus: the critters (the wiki's three: Hex's Frog, Polymorph's Sheep, a Squirrel).</summary>
		public static readonly IReadOnlyList<string> Critters = new[] { "hexfrog", "CS2_tk1", "EX1_tk28" };
		/// <summary>DALA_BOSS_03t Reductomara: the enchantment it puts on every spell of both hands and decks ("Costs (1) less.").</summary>
		public const string Reductomarad = "DALA_BOSS_03e";
		/// <summary>DALA_BOSS_03t4 Yoggers Poggers: the Scroll of Wonder ("Casts When Drawn. Cast a random spell.") and how many per deck.</summary>
		public const string ScrollOfWonder = "LOOT_106t";
		public const int ScrollsPerDeck = 3;
		/// <summary>DALA_BOSS_07: Take Flight! (form 0), Flying! (form 1) and Flying!'s Immune enchantment on Belnaara's hero.</summary>
		public const string TakeFlight = "DALA_BOSS_07p", Flying = "DALA_BOSS_07p2", FlyingImmune = "DALA_BOSS_07e";
		/// <summary>DALA_BOSS_08p Apple Toss: the most hits one use makes (it repeats while its target dies).</summary>
		public const int AppleTossMax = 10;
		/// <summary>DALA_BOSS_14p Order Up!: the 2/2 Kind Waitress.</summary>
		public const string KindWaitress = "DALA_BOSS_14t";
		/// <summary>DALA_BOSS_10p Street Smarts: the Coins ("Gain 1 Mana Crystal this turn only.": The Coin, Counterfeit Coin).</summary>
		public static readonly IReadOnlyList<string> Coins = new[] { "GAME_005", "CFM_630" };

		/// <summary>DALA_BOSS_10p Street Smarts: the card is a Coin.</summary>
		public static bool IsCoin(Card card) => card != null && Coins.Contains(card.Id);

		/// <summary>
		/// DALA_BOSS_03t Reductomara: the spells of <paramref name="c"/>'s hand and deck it makes cost (1) less. An Echo or
		/// "Repeatable this turn" spell (<see cref="Repeatable"/>) at (1) or less is left alone: at (0) it would repeat without end.
		/// </summary>
		public static List<IPlayable> ReductomaraTargets(Controller c) =>
			c.HandZone.Concat(c.DeckZone).Where(p => p is Spell && !(Repeatable(p) && p.Cost <= 1)).ToList();

		/// <summary>
		/// DALA_BOSS_05p Mana Echoes, "Cast a random spell that was cast this game": the spells either player played from hand this
		/// game (the public play history, one entry per play) that <paramref name="c"/> may cast now. Scripted and never a Quest
		/// (it could not be in play twice). A Secret only while <paramref name="c"/>'s Secret zone has room and holds no copy of
		/// it (a cast Secret enters play, as the Tombs Monocle's casts do), and only one <paramref name="c"/> itself played this
		/// game: a boss's Secret is a card of its own deck, admitted for it, while a Secret of the run player's is a boss-generated
		/// Secret outside any closed boss pool (the generation filter blocks every boss-generated Secret unless an encounter row
		/// turns them on for its class pool). Every other spell passes the run's generation filter for <paramref name="c"/>.
		/// </summary>
		public static List<Card> ManaEchoes(Controller c)
		{
			Game g = c.Game;
			bool SecretFits(Card card) => !c.SecretZone.IsFull && !c.SecretZone.Any(p => p.Card.AssetId == card.AssetId);
			bool Castable(Card card) => card != null && card.Type == CardType.SPELL && card.Implemented && !card.IsQuest;
			var own = c.PlayHistory.Select(h => h.SourceCard).Where(Castable).ToList();
			var theirs = c.Opponent.PlayHistory.Select(h => h.SourceCard).Where(card => Castable(card) && !card.IsSecret).ToList();
			if (own.Count + theirs.Count == 0) return own;
			var found = new List<Card>();
			using (g.GenerateFor(c))
			{
				found.AddRange(own.Where(card => card.IsSecret ? SecretFits(card) : !g.BlocksGenerated(card)));
				found.AddRange(theirs.Where(card => !g.BlocksGenerated(card)));
			}
			return found;
		}

		/// <summary>
		/// DALA_BOSS_09p Repeat History: the friendly minions of <paramref name="c"/> that died this turn (the last ones of its
		/// graveyard, as ComplexTask.SummonAllFriendlyDiedThisTurn reads them), never a run shrine's corpse.
		/// </summary>
		public static List<IPlayable> DiedThisTurn(Controller c)
		{
			var found = new List<IPlayable>();
			int num = c.NumFriendlyMinionsThatDiedThisTurn;
			var graveyard = c.GraveyardZone.ToList();
			for (int i = graveyard.Count - 1, j = 0; i >= 0 && j < num; --i)
			{
				IPlayable p = graveyard[i];
				if (!p.ToBeDestroyed || p.Card.Type != CardType.MINION) continue;
				j++;
				if (!ShrineRules.IsCorpse(p)) found.Add(p);
			}
			found.Reverse();
			return found;
		}

		/// <summary>
		/// DALA_BOSS_08p Apple Toss: the enemy characters with the lowest Health (the hero and the minions an effect can pick, none
		/// already dying), in board order with the hero first.
		/// </summary>
		public static List<ICharacter> LowestEnemies(Controller c)
		{
			var enemies = new List<ICharacter>();
			Hero hero = c.Opponent.Hero;
			if (!hero.ToBeDestroyed && hero.Health > 0) enemies.Add(hero);
			enemies.AddRange(Touchable(c.Opponent).Where(m => m.Health > 0));
			if (enemies.Count == 0) return enemies;
			int low = enemies.Min(e => e.Health);
			return enemies.Where(e => e.Health == low).ToList();
		}

		private static T Pick<T>(Game g, IReadOnlyList<T> list)
		{
			if (list.Count == 1) return list[0];
			g.OnRandomHappened(true);
			return list[g.Random.Next(list.Count)];
		}

		/// <summary>
		/// DALA_BOSS_07p Take Flight!: the damage Belnaara's hero took is counted down on the power (TAG_SCRIPT_DATA_NUM_1, the
		/// client's "@ damage remaining", printed 15); at 0 she takes flight: form 1 (Flying!, power only) and her hero becomes
		/// Immune (Flying!, DALA_BOSS_07e). Not when the hit leaves her dead (the fight ends) or the game is over.
		/// </summary>
		private static void TakeFlightHit(Game g, Controller c, IEntity source)
		{
			if (!(source is HeroPower power) || !ReferenceEquals(c.Hero.HeroPower, power) || g.State == State.COMPLETE) return;
			int amount = g.CurrentEventData?.EventNumber ?? 0;
			if (amount <= 0 || c.Hero.ToBeDestroyed || c.Hero.Health <= 0) return;
			int left = power[GameTag.TAG_SCRIPT_DATA_NUM_1] - amount;
			if (left > 0) { power[GameTag.TAG_SCRIPT_DATA_NUM_1] = left; return; }
			power[GameTag.TAG_SCRIPT_DATA_NUM_1] = 0;
			EncounterRules.SwapPowerForm(c, 1);
			Generic.AddEnchantmentBlock(g, Cards.FromId(FlyingImmune), c.Hero.HeroPower, c.Hero);
		}

		/// <summary>
		/// DALA_BOSS_07p2 Flying!: at the start of each of Belnaara's turns the count on the power (printed 2, "@ turns remaining")
		/// falls by one; at 0 she lands: Flying!'s Immune ends and Take Flight! returns with a fresh count (form 0).
		/// </summary>
		private static void FlyingTurn(Game g, Controller c, IEntity source)
		{
			if (!(source is HeroPower power) || !ReferenceEquals(c.Hero.HeroPower, power) || g.State == State.COMPLETE) return;
			int left = power[GameTag.TAG_SCRIPT_DATA_NUM_1] - 1;
			if (left > 0) { power[GameTag.TAG_SCRIPT_DATA_NUM_1] = left; return; }
			power[GameTag.TAG_SCRIPT_DATA_NUM_1] = 0;
			// RemoveEnchantmentTask reverts the enchantment's Immune and removes it (Enchantment.Remove alone keeps the tag).
			foreach (var e in c.Hero.AppliedEnchantments?.Where(x => x.Card.Id == FlyingImmune).ToList() ?? new List<Model.Entities.Enchantment>())
				RemoveEnchantmentTask.Task.Process(g, c, e, null);
			EncounterRules.SwapPowerForm(c, 0);
		}

		private static void F14()
		{
			var aMinion = new[] { PlayReq.REQ_TARGET_TO_PLAY, PlayReq.REQ_MINION_TARGET };
			bool AnyMinion(Controller c) => Touchable(c).Any() || Touchable(c.Opponent).Any();

			// ---------------- Chomper (DALA_BOSS_01h)
			// Chomp (2): "Lifesteal. Deal 1 damage to a minion." The power card's own Lifesteal heals the boss's hero by the damage dealt.
			Power("DALA_BOSS_01p", aMinion, () => new DamageTask(1, EntityType.TARGET));

			// ---------------- Awilo, Cooking Trainer (DALA_BOSS_02h)
			// The Feast (passive): "After you cast a spell, restore 3 Health to your hero."
			Passive("DALA_BOSS_02p", () => new Power
			{
				Trigger = new Trigger(TriggerType.AFTER_CAST) { TriggerSource = TriggerSource.FRIENDLY, SingleTask = new HealTask(3, EntityType.HERO) },
			});

			// ---------------- The Great Akazamzarak (DALA_BOSS_03h)
			// Prestidigitation (1): "Add a random magical feat to your hand." One of the four feats, each with the same chance;
			// offered only while the hand has room.
			Power("DALA_BOSS_03p", null, () => new CustomTask((g, c, s, t, stack) =>
			{
				if (c.HandZone.IsFull) return;
				Generic.AddHandPhase.Invoke(c, Entity.FromCard(c, Cards.FromId(Pick(g, MagicalFeats))));
			}), usable: c => !c.HandZone.IsFull);
			// Reductomara (1): "Reduce the cost of all spells in both player's hand and deck by (1)." Reductomara'd on each spell
			// of the caster's hand and deck, then the opponent's; each owner's deck is recorded as rewritten (its own-deck ledger).
			BossCard("DALA_BOSS_03t", () => new CardDef(new Power
			{
				PowerTask = new CustomTask((g, c, s, t, stack) =>
				{
					Card mark = Cards.FromId(Reductomarad);
					foreach (Controller side in new[] { c, c.Opponent })
					{
						foreach (IPlayable spell in ReductomaraTargets(side)) Generic.AddEnchantmentBlock(g, mark, (IPlayable)s, spell);
						EncounterRules.DeckRewritten(side, side.DeckZone.Select(p => p.Card.Id).OrderBy(id => id, StringComparer.Ordinal));
					}
				}),
			}));
			Enchantment(Reductomarad, () => new Power { Enchant = new Enchant(Effects.ReduceCost(1)) });
			// Bunnifitronus (1): "Transform a random minion into a random critter." Any minion of either board an effect can pick;
			// a run shrine picked is destroyed instead and goes dormant (the transform rule of Actions.Generic).
			BossCard("DALA_BOSS_03t2", () => new CardDef(new Power
			{
				PowerTask = new CustomTask((g, c, s, t, stack) =>
				{
					var minions = Touchable(c).Concat(Touchable(c.Opponent)).ToList();
					if (minions.Count == 0) return;
					Minion victim = Pick(g, minions);
					Generic.TransformBlock.Invoke(victim.Controller, Cards.FromId(Pick(g, Critters)), victim);
				}),
			}));
			// Presto Legendaro (1): "Give each player a random Golden Legendary minion." To each hand (the caster's first), a random
			// scripted collectible Legendary minion of any class that side's generation filter allows (golden is not modelled).
			BossCard("DALA_BOSS_03t3", () => new CardDef(new Power
			{
				PowerTask = new CustomTask((g, c, s, t, stack) =>
				{
					foreach (Controller side in new[] { c, c.Opponent })
					{
						if (side.HandZone.IsFull || g.State == State.COMPLETE) continue;
						var pool = ScriptedPool(g, side, s, card => card.Type == CardType.MINION && card.Rarity == Rarity.LEGENDARY && card.Collectible, anyClass: true);
						if (pool.Count == 0) continue;
						Generic.AddHandPhase.Invoke(side, Entity.FromCard(side, Pick(g, pool)));
					}
				}),
			}));
			// Yoggers Poggers (1): "Shuffle three Scrolls of Wonder into each player's deck." The caster's deck first; each owner's
			// deck is recorded as rewritten (its own-deck ledger knows the three Scrolls).
			BossCard("DALA_BOSS_03t4", () => new CardDef(new Power
			{
				PowerTask = new CustomTask((g, c, s, t, stack) =>
				{
					foreach (Controller side in new[] { c, c.Opponent })
					{
						for (int i = 0; i < ScrollsPerDeck; i++) Generic.ShuffleIntoDeck(side, s, Entity.FromCard(side, Cards.FromId(ScrollOfWonder)));
						EncounterRules.DeckRewritten(side, side.DeckZone.Select(p => p.Card.Id).OrderBy(id => id, StringComparer.Ordinal));
					}
				}),
			}));

			// ---------------- Kaye Toogie (DALA_BOSS_04h)
			// Open Wormhole (2): "Both players summon a minion from their deck. They fight." Duel!'s rule (DAL_731): a random minion
			// of each deck is summoned (a deck with none, or a full board, summons none) and the boss's attacks the other.
			Power("DALA_BOSS_04p", null, () => Duel);

			// ---------------- The Amazing "Bonepaw" (DALA_BOSS_05h)
			// Mana Echoes (2): "Cast a random spell that was cast this game (targets chosen randomly)." A new copy of one played
			// spell (ManaEchoes: one entry per play), cast by the effect play rule (random targets and choices). Offered only after
			// such a spell was played.
			Power("DALA_BOSS_05p", null, () => new CustomTask((g, c, s, t, stack) =>
			{
				var spells = ManaEchoes(c);
				if (spells.Count == 0) return;
				var copy = (Spell)Entity.FromCard(c, Pick(g, spells));
				g.OnRandomHappened(true);
				AutoPlayRules.Play(copy, s);
			}), usable: c => ManaEchoes(c).Count > 0);

			// ---------------- Dazzik "Hellscream" (DALA_BOSS_06h)
			// "Armor Up" (2): "Gain 2 Armor."
			Power("DALA_BOSS_06p", null, () => new ArmorTask(2));
			// "Scourgelord" Dazzik (8, hero card, 10 Armor): "Battlecry: Equip a 4/3 Shadowmourne that also damages adjacent
			// minions." Scourgelord Garrosh's rule: played, it becomes the boss's hero with Bladestorm (ICC_834h, the card's hero
			// power) and equips Shadowmourne (ICC_834w).
			BossCard("DALA_BOSS_06dk", () => new CardDef(new Power { PowerTask = new WeaponTask("ICC_834w") }));

			// ---------------- Flight Master Belnaara (DALA_BOSS_07h)
			// Take Flight! (passive): "After taking 15 damage, Belnaara will take flight." Flying! (passive): "Belnaara will land in
			// 2 turns." Flying! (DALA_BOSS_07e): "Belnaara is Immune for now."
			Passive(TakeFlight, () => new Power
			{
				Trigger = new Trigger(TriggerType.TAKE_DAMAGE)
				{
					TriggerSource = TriggerSource.HERO,
					FastExecution = true,
					SingleTask = new CustomTask((g, c, s, t, stack) => TakeFlightHit(g, c, s)),
				},
			});
			Passive(Flying, () => new Power
			{
				Trigger = new Trigger(TriggerType.TURN_START)
				{
					TriggerSource = TriggerSource.FRIENDLY,
					SingleTask = new CustomTask((g, c, s, t, stack) => FlyingTurn(g, c, s)),
				},
			});
			Enchantment(FlyingImmune, () => new Power { Enchant = new Enchant(Effects.Immune) });

			// ---------------- Applebough (DALA_BOSS_08h)
			// Apple Toss (2): "Deal 1 damage to the lowest Health enemy. If it dies, repeat this." The lowest Health among the enemy
			// hero and minions, a tie broken by the game RNG; a hero power's damage (no Spell Damage). It repeats while its target
			// dies (Health 0 or less, or destroyed), at most AppleTossMax hits.
			Power("DALA_BOSS_08p", null, () => new CustomTask((g, c, s, t, stack) =>
			{
				for (int i = 0; i < AppleTossMax && g.State != State.COMPLETE; i++)
				{
					var lowest = LowestEnemies(c);
					if (lowest.Count == 0) return;
					ICharacter target = Pick(g, lowest);
					Generic.DamageCharFunc.Invoke((IPlayable)s, target, 1, false);
					if (!(target.ToBeDestroyed || target.Health <= 0) || target is Hero) return;
				}
			}));

			// ---------------- Archivist Oshi (DALA_BOSS_09h)
			// Repeat History (2): "Summon a random friendly minion that died this turn." A new copy of one of them (a run shrine's
			// corpse is never one); offered only while one died this turn and the board has room.
			Power("DALA_BOSS_09p", new[] { PlayReq.REQ_NUM_MINION_SLOTS }, () => new CustomTask((g, c, s, t, stack) =>
			{
				var died = DiedThisTurn(c);
				if (died.Count == 0 || c.BoardZone.IsFull) return;
				Generic.SummonBlock(g, (Minion)Entity.FromCard(c, Pick(g, died).Card), -1, s);
			}), usable: c => DiedThisTurn(c).Count > 0);

			// ---------------- Mo Eniwhiskers (DALA_BOSS_10h)
			// Street Smarts (passive): "Whenever you play a Coin, draw a card."
			Passive("DALA_BOSS_10p", () => new Power
			{
				Trigger = new Trigger(TriggerType.PLAY_CARD)
				{
					TriggerSource = TriggerSource.FRIENDLY,
					Condition = new SelfCondition(p => IsCoin(p.Card)),
					SingleTask = new DrawTask(),
				},
			});

			// ---------------- Noz Timbertail (DALA_BOSS_11h)
			// Backstabber (passive): "Whenever a Stealthed minion is summoned, give it +1 Attack." Either side's (Backstabby).
			Passive("DALA_BOSS_11p", () => new Power
			{
				Trigger = new Trigger(TriggerType.SUMMON)
				{
					TriggerSource = TriggerSource.ALL_MINIONS,
					Condition = new SelfCondition(p => p is Minion m && m.HasStealth && !m.Untouchable),
					SingleTask = new AddEnchantmentTask("DALA_BOSS_11e", EntityType.TARGET),
				},
			});
			Enchantment("DALA_BOSS_11e", () => new Power { Enchant = new Enchant(Effects.Attack_N(1)) });

			// ---------------- Cravitz Lorent (DALA_BOSS_12h)
			// Forbidden Love (3): "Destroy a random minion for each player." One random minion of each board an effect can pick (the
			// boss's first); a board with none loses nothing. Offered only while a minion is on a board.
			Power("DALA_BOSS_12p", null, () => new CustomTask((g, c, s, t, stack) =>
			{
				foreach (Controller side in new[] { c, c.Opponent })
				{
					var minions = Touchable(side).ToList();
					if (minions.Count == 0) continue;
					Minion victim = Pick(g, minions);
					DeathContextRules.RecordDestroy(victim, s);
					victim.Destroy();
				}
			}), usable: AnyMinion);

			// ---------------- Albin Eastoft (DALA_BOSS_13h)
			// Slice and Dice (2): "Deal $1-$3 damage to three random minions." Three different minions of both boards (all of them
			// when fewer), picked first, then each hit in board order (the boss's first) for 1 to 3, by the game RNG; a hero
			// power's damage. Offered only while a minion is on a board.
			Power("DALA_BOSS_13p", null, () => new CustomTask((g, c, s, t, stack) =>
			{
				var pool = Touchable(c).Concat(Touchable(c.Opponent)).ToList();
				if (pool.Count == 0) return;
				var picked = pool;
				if (pool.Count > 3)
				{
					var order = Enumerable.Range(0, pool.Count).ToList();
					var take = new List<int>();
					for (int i = 0; i < 3; i++)
					{
						int k = g.Random.Next(order.Count);
						take.Add(order[k]);
						order.RemoveAt(k);
					}
					take.Sort();
					picked = take.Select(i => pool[i]).ToList();
				}
				g.OnRandomHappened(true);
				foreach (Minion m in picked)
				{
					if (g.State == State.COMPLETE) return;
					Generic.DamageCharFunc.Invoke((IPlayable)s, m, 1 + g.Random.Next(3), false);
				}
			}), usable: AnyMinion);

			// ---------------- Tierra Blythe (DALA_BOSS_14h)
			// Order Up! (2): "Summon a 2/2 Kind Waitress." Needs a free board slot.
			Power("DALA_BOSS_14p", new[] { PlayReq.REQ_NUM_MINION_SLOTS }, () => new SummonTask(KindWaitress));
			// "Kind" Waitress (2/2): "Enemies attacking this have a 50% chance to attack someone else." As the attack is prepared,
			// an enemy attacking it rolls the wrong-target coin (Actions.EncounterRules.WrongTarget50: another character of the
			// Waitress's side, the hero included, uniformly; Taunt does not matter). Silenced, it has no such rule.
			BossCard(KindWaitress, () => new CardDef(new Power
			{
				Trigger = new Trigger(TriggerType.ATTACK) { TriggerSource = TriggerSource.SELF_AS_TARGET, FastExecution = true, SingleTask = EncounterRules.WrongTargetTask },
			}));

			// ---------------- Moon Priestess Nici (DALA_BOSS_15h)
			// Blessing of Elune (passive): "All healing is doubled." Both players' (Crystalsmith Kangor's ALL_HEALING_DOUBLE, a
			// controller aura on both sides): a heal that Auchenai Soulpriest or Bwonsamdi's Covenant turns into damage deals the
			// doubled amount.
			Passive("DALA_BOSS_15p", () => new Power
			{
				Aura = new Aura(AuraType.CONTROLLERS, new Effect(GameTag.ALL_HEALING_DOUBLE, EffectOperator.ADD, 1)),
			});
		}
	}
}
