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
	// Phase 8, family 17: Dalaran Heist bosses D (DALA_BOSS_46 to 59) and the bank and guard cards. Fourteen encounters and
	// their boss-only cards: Ichoron (Protective Bubble), Lavanthor (Lava Belch, Molten Rock), Zuramat the Obliterator (To The
	// Void, Void Shift), Cyanigosa (Uncontrollable Energy), Nozari (Sand Breath), Millificent Manastorm (Tinker, Elementium
	// Squirrel Bomb, Thorium Chicken), Lieutenant Sinclari (Hold the Gates!, Kirin Tor Guard), Dancin' Deryl (Boogie Woogie),
	// Locksmith Zibb (Spell Lock), Captain Hannigan (Raise the Alarm, Kirin Tor Guard), Queen Wagtoggle (Bribery, Rival
	// Accomplice), Trade Prince Gallywix (Greed is Good), Boommaster Flark (Blast Powder, The Wire, The Other Wire) and Madam
	// Goya (Blackmail, Extortion, Lotus Bruiser, Fruit Slasher), plus Infuriated Banker (DALA_502, a bank deck card). The texts
	// are the client's (facts/violet-hold-2026-09.json). A clickable power that would do nothing is not offered, by a condition
	// that reads only the public boards or the boss's own hand; four powers are cast by rule (family 09's AutoCast): the two the
	// wiki marks auto-cast (Sand Breath, Raise the Alarm) and the two whose worth lies in the run player's hidden zones, which the
	// boss's fair view cannot see (Bribery, Blackmail). Every random choice uses the game RNG and is reported as a random event;
	// the state is board and zone state, the set-aside store's link (the Void), the private tag ExtortionEscrow (Extortion) and a
	// public counter on Greed is Good (TAG_SCRIPT_DATA_NUM_1: the turn of its last purchase). A run player's board shrine never
	// enters the Void; a destroy aimed at one makes it go dormant (Actions.ShrineRules).
	public static partial class DungeonRunEncounters
	{
		/// <summary>DALA_BOSS_47p Lava Belch: the 0/3 Molten Rock.</summary>
		public const string MoltenRock = "DALA_BOSS_47t";
		/// <summary>DALA_BOSS_48t Void Shift.</summary>
		public const string VoidShiftCard = "DALA_BOSS_48t";
		/// <summary>DALA_BOSS_51p Tinker: the 0/4 Elementium Squirrel Bomb; DALA_BOSS_51t2 Thorium Chicken (a deck card).</summary>
		public const string SquirrelBomb = "DALA_BOSS_51t", ThoriumChicken = "DALA_BOSS_51t2";
		/// <summary>DALA_BOSS_52p Hold the Gates!: Hold Fast! (+2 Health and Taunt).</summary>
		public const string HoldFast = "DALA_BOSS_52e";
		/// <summary>DALA_BOSS_53p Boogie Woogie: Boogie Beyond ("Deathrattle: Resummon this minion.").</summary>
		public const string BoogieBeyond = "DALA_BOSS_53e";
		/// <summary>DALA_BOSS_56p Bribery: Bribed ("Deathrattle: Return this to your opponent's hand."); Rival Accomplice, the Heroic boss card.</summary>
		public const string Bribed = "DALA_BOSS_56e", RivalAccomplice = "DALA_BOSS_56t";
		/// <summary>DALA_BOSS_58p Blast Powder: its mark (DALA_BOSS_58e, "If this minion takes damage, it will be destroyed.").</summary>
		public const string BlastPowderMark = "DALA_BOSS_58e";
		/// <summary>The Wire's damage to its player's hero while the wires touch.</summary>
		public const int WireDamage = 5;
		/// <summary>DALA_BOSS_59 Madam Goya's boss cards and Lotus Bruiser's Payday (+2/+2).</summary>
		public const string LotusBruiser = "DALA_BOSS_59t", FruitSlasher = "DALA_BOSS_59t2", Payday = "DALA_BOSS_59e";
		/// <summary>DALA_502 Infuriated Banker and its Rich (+1/+1); DALA_503 Kirin Tor Guard.</summary>
		public const string InfuriatedBanker = "DALA_502", BankerRich = "DALA_502e", KirinTorGuard = "DALA_503";
		/// <summary>DALA_BOSS_57p Greed is Good: the Coin it gives (Gallywix's Coin, "Won't trigger Gallywix.").</summary>
		public const string GallywixCoin = "GVG_028t";

		/// <summary>
		/// A Coin for Infuriated Banker ("Whenever a Coin is played"): the cards whose text is "Gain 1 Mana Crystal this turn only.":
		/// The Coin, Counterfeit Coin and Gallywix's Coin.
		/// </summary>
		public static readonly IReadOnlyList<string> BankCoins = new[] { "GAME_005", "CFM_630", GallywixCoin };

		/// <summary>Infuriated Banker: the card is a Coin (<see cref="BankCoins"/>).</summary>
		public static bool IsBankCoin(Card card) => card != null && BankCoins.Contains(card.Id);

		private static T HeistDPick<T>(Game g, IReadOnlyList<T> list)
		{
			if (list.Count == 1) return list[0];
			g.OnRandomHappened(true);
			return list[g.Random.Next(list.Count)];
		}

		/// <summary>DALA_BOSS_50p Sand Breath: the enemies it may hit (the enemy hero and the enemy minions an effect can pick).</summary>
		public static List<ICharacter> SandBreathTargets(Controller c)
		{
			var enemies = new List<ICharacter>();
			if (!c.Opponent.Hero.ToBeDestroyed && c.Opponent.Hero.Health > 0) enemies.Add(c.Opponent.Hero);
			enemies.AddRange(Touchable(c.Opponent));
			return enemies;
		}

		/// <summary>
		/// DALA_BOSS_57p Greed is Good, "Buy a copy of the first card your opponent plays each turn for a Coin.": the first card
		/// <paramref name="boss"/>'s opponent plays in a turn (Gallywix's Coin does not count: "Won't trigger Gallywix") puts a new
		/// copy of that card in the boss's hand and a Gallywix's Coin in the opponent's. The power remembers the turn of its last
		/// purchase (TAG_SCRIPT_DATA_NUM_1). With the boss's hand full nothing is bought and the turn's purchase stays open.
		/// </summary>
		public static bool GreedIsGood(Game g, Controller boss, IEntity power, IPlayable played)
		{
			if (played == null || played.Controller != boss.Opponent || played.Card.Id == GallywixCoin || power == null) return false;
			if (power[GameTag.TAG_SCRIPT_DATA_NUM_1] == g.Turn || boss.HandZone.IsFull || g.State == State.COMPLETE) return false;
			power[GameTag.TAG_SCRIPT_DATA_NUM_1] = g.Turn;
			Generic.AddHandPhase(boss, Entity.FromCard(boss, played.Card));
			Generic.AddHandPhase(boss.Opponent, Entity.FromCard(boss.Opponent, Cards.FromId(GallywixCoin)));
			return true;
		}

		/// <summary>
		/// DALA_BOSS_56p Bribery, "Summon a minion from your opponent's deck. When it dies, give it back.": Tad's Pole's rule
		/// (<see cref="PoleCatch"/>: a random minion of the opponent's deck changes sides and is summoned, a public summon) and the
		/// minion is Bribed. Null with a full board or no minion in that deck.
		/// </summary>
		public static Minion Bribe(Controller c, IEntity source)
		{
			Minion pick = PoleCatch(c, source);
			if (pick != null) Generic.AddEnchantmentBlock(c.Game, Cards.FromId(Bribed), source as IPlayable ?? c.Hero, pick);
			return pick;
		}

		/// <summary>
		/// DALA_BOSS_54p Spell Lock: the Overload a spell played brings, the Cost it was played for (the Cost it last had in hand,
		/// TAG_LAST_KNOWN_COST_IN_HAND, which the engine keeps as the play's paid Cost; never below 0).
		/// </summary>
		public static int SpellLockOverload(IPlayable spell) => spell == null ? 0 : System.Math.Max(0, spell[GameTag.TAG_LAST_KNOWN_COST_IN_HAND]);

		private static void F17()
		{
			var aMinion = new[] { PlayReq.REQ_TARGET_TO_PLAY, PlayReq.REQ_MINION_TARGET };
			var aFriendlyMinion = new[] { PlayReq.REQ_TARGET_TO_PLAY, PlayReq.REQ_MINION_TARGET, PlayReq.REQ_FRIENDLY_TARGET };

			// ---------------- Ichoron (DALA_BOSS_46h)
			// Protective Bubble (1): "Give a friendly minion Divine Shield." A minion of the boss without one.
			Add("DALA_BOSS_46p", EncounterCardKind.Power, () => new CardDef(Reqs(aFriendlyMinion),
				new Power { PowerTask = new SetGameTagTask(GameTag.DIVINE_SHIELD, 1, EntityType.TARGET) }),
				card => card.SourceTargetingPredicate = (source, target) => target is Minion m && !m.HasDivineShield);

			// ---------------- Lavanthor (DALA_BOSS_47h)
			// Lava Belch (1): "Summon a 0/3 Molten Rock." Needs a free board slot.
			Power("DALA_BOSS_47p", new[] { PlayReq.REQ_NUM_MINION_SLOTS }, () => new SummonTask(MoltenRock));
			// Molten Rock (0/3): "At the end of your turn, deal 1 damage to all characters." Both heroes and every minion, itself too.
			BossCard(MoltenRock, () => new CardDef(new Power
			{
				Trigger = new Trigger(TriggerType.TURN_END) { SingleTask = new DamageTask(1, EntityType.ALL) },
			}));

			// ---------------- Zuramat the Obliterator (DALA_BOSS_48h)
			// To The Void (2): "Banish a minion to the Void." Any minion the set-aside store can take (never a run shrine): it
			// leaves its board with no death (Actions.EncounterRules.Banish).
			Add("DALA_BOSS_48p", EncounterCardKind.Power, () => new CardDef(Reqs(aMinion),
				new Power { PowerTask = new CustomTask((g, c, s, t, stack) => EncounterRules.Banish((IPlayable)s, t as Minion)) }),
				card => card.SourceTargetingPredicate = (source, target) => target is Minion m && EncounterRules.CanStore(m));
			Enchantment(EncounterRules.VoidedCard, () => new Power());
			// Void Shift (3): "Destroy all minions. Summon all minions from the Void." For its caster, the boss or (two copies in
			// hand from the start, the wiki's Normal mode) the run player (Actions.EncounterRules.VoidShift).
			BossCard(VoidShiftCard, () => new CardDef(new Power
			{
				PowerTask = new CustomTask((g, c, s, t, stack) => EncounterRules.VoidShift(c, (IPlayable)s)),
			}));

			// ---------------- Cyanigosa (DALA_BOSS_49h)
			// Uncontrollable Energy (passive): "After you cast a spell, deal $1 damage to all enemies." A hero power's damage.
			Passive("DALA_BOSS_49p", () => new Power
			{
				Trigger = new Trigger(TriggerType.AFTER_CAST) { TriggerSource = TriggerSource.FRIENDLY, SingleTask = new DamageTask(1, EntityType.ENEMIES) },
			});

			// ---------------- Nozari (DALA_BOSS_50h)
			// Sand Breath (2): "Deal $2 damage to a random enemy." The wiki's auto-cast: cast by rule at the start of the boss's
			// turn when it can pay; the enemy hero or an enemy minion, by the game RNG. A hero power's damage.
			AutoCast("DALA_BOSS_50p", c => SandBreathTargets(c).Count > 0, (g, c, s, stack) =>
			{
				var targets = SandBreathTargets(c);
				if (targets.Count > 0) Generic.DamageCharFunc.Invoke((IPlayable)s, HeistDPick(g, targets), 2, false);
			});

			// ---------------- Millificent Manastorm (DALA_BOSS_51h)
			// Tinker (2): "Summon a 0/4 Squirrel Bomb." Needs a free board slot.
			Power("DALA_BOSS_51p", new[] { PlayReq.REQ_NUM_MINION_SLOTS }, () => new SummonTask(SquirrelBomb));
			// Elementium Squirrel Bomb (0/4): "At the start of your turn, destroy this and deal 2 damage to all enemies."
			BossCard(SquirrelBomb, () => new CardDef(new Power
			{
				Trigger = new Trigger(TriggerType.TURN_START)
				{
					SingleTask = ComplexTask.Create(new DestroyTask(EntityType.SOURCE), new DamageTask(2, EntityType.ENEMIES)),
				},
			}));
			// Thorium Chicken (5/1): "After your opponent plays a card, deal 3 damage to their hero."
			BossCard(ThoriumChicken, () => new CardDef(new Power
			{
				Trigger = new Trigger(TriggerType.AFTER_PLAY_CARD) { TriggerSource = TriggerSource.ENEMY, SingleTask = new DamageTask(3, EntityType.OP_HERO) },
			}));

			// ---------------- Lieutenant Sinclari (DALA_BOSS_52h)
			// Hold the Gates! (2): "Give a minion +2 Health and Taunt." Any minion (Hold Fast!).
			Power("DALA_BOSS_52p", aMinion, () => new AddEnchantmentTask(HoldFast, EntityType.TARGET));
			Enchantment(HoldFast, () => new Power { Enchant = new Enchant(Effects.Health_N(2), Effects.TauntEff) });
			// Kirin Tor Guard (1/4): "Has +1 Attack for each other friendly Kirin Tor Guard." (Sinclari's deck, Hannigan's power)
			BossCard(KirinTorGuard, () => new CardDef(new Power
			{
				Aura = new AdaptiveEffect(GameTag.ATK, EffectOperator.ADD, p =>
					p.Controller.BoardZone.Count(m => m.Card.Id == KirinTorGuard && !ReferenceEquals(m, p))),
			}));

			// ---------------- Dancin' Deryl (DALA_BOSS_53h)
			// Boogie Woogie (3): "Give a friendly minion 'Deathrattle: Resummon this minion.'" (Boogie Beyond, Ancestral Spirit's rule.)
			Power("DALA_BOSS_53p", aFriendlyMinion, () => new AddEnchantmentTask(BoogieBeyond, EntityType.TARGET));
			Enchantment(BoogieBeyond, () => new Power { DeathrattleTask = new CopyTask(EntityType.SOURCE, Zone.PLAY) });

			// ---------------- Locksmith Zibb (DALA_BOSS_54h)
			// Spell Lock (passive): "Spells Overload for their cost." Every spell either player plays Overloads its player for the
			// Cost it was played for, on top of its own Overload (Actions.EncounterRules.OverloadFor).
			Passive("DALA_BOSS_54p", () => new Power
			{
				Trigger = new Trigger(TriggerType.CAST_SPELL)
				{
					TriggerSource = TriggerSource.ALL,
					SingleTask = new CustomTask((g, c, s, t, stack) =>
					{
						if (t is Spell spell && !spell.IsCountered) EncounterRules.OverloadFor(spell.Controller, spell, SpellLockOverload(spell));
					}),
				},
			});

			// ---------------- Captain Hannigan (DALA_BOSS_55h)
			// Raise the Alarm (2): "Summon a 1/4 Kirin Tor Guard." The wiki's auto-cast: cast by rule at the start of the boss's
			// turn when it can pay and its board has room.
			AutoCast("DALA_BOSS_55p", c => !c.BoardZone.IsFull, (g, c, s, stack) =>
			{
				if (!c.BoardZone.IsFull) Generic.SummonBlock(g, (Minion)Entity.FromCard(c, Cards.FromId(KirinTorGuard)), -1, s);
			});

			// ---------------- Queen Wagtoggle (DALA_BOSS_56h)
			// Bribery (3): "Summon a minion from your opponent's deck. When it dies, give it back." Cast by rule at the start of the
			// boss's turn when it can pay, its board has room and the run player's deck holds a card: the worth of a card drawn
			// from a hidden deck is outside the boss's fair view (family 09's rule for such powers). Bribed: when it dies, a new
			// copy goes to the hand of its controller's opponent.
			AutoCast("DALA_BOSS_56p", c => !c.BoardZone.IsFull && c.Opponent.DeckZone.Count > 0, (g, c, s, stack) => Bribe(c, s));
			Enchantment(Bribed, () => new Power
			{
				DeathrattleTask = new CustomTask((g, c, s, t, stack) =>
				{
					IPlayable host = s is Enchantment e ? e.Target as IPlayable : s as IPlayable;
					if (host == null || g.State == State.COMPLETE) return;
					Generic.AddHandPhase(c.Opponent, Entity.FromCard(c.Opponent, host.Card));
				}),
			});
			// Rival Accomplice (4/4, no text): the Heroic boss card.
			Token(RivalAccomplice);

			// ---------------- Trade Prince Gallywix (DALA_BOSS_57h)
			// Greed is Good (passive): "Buy a copy of the first card your opponent plays each turn for a Coin." (GreedIsGood)
			Passive("DALA_BOSS_57p", () => new Power
			{
				Trigger = new Trigger(TriggerType.PLAY_CARD)
				{
					TriggerSource = TriggerSource.ENEMY,
					SingleTask = new CustomTask((g, c, s, t, stack) => GreedIsGood(g, c, s, t as IPlayable)),
				},
			});

			// ---------------- Boommaster Flark (DALA_BOSS_58h)
			// Blast Powder (2): "Coat a minion in Blast Powder. When it takes damage, destroy it." Any minion not coated yet.
			Add("DALA_BOSS_58p", EncounterCardKind.Power, () => new CardDef(Reqs(aMinion),
				new Power { PowerTask = new AddEnchantmentTask(BlastPowderMark, EntityType.TARGET) }),
				card => card.SourceTargetingPredicate = (source, target) => target is Minion m && m.AppliedEnchantments?.Any(e => e.Card.Id == BlastPowderMark) != true);
			Enchantment(BlastPowderMark, () => new Power
			{
				Trigger = new Trigger(TriggerType.TAKE_DAMAGE)
				{
					TriggerSource = TriggerSource.ENCHANTMENT_TARGET,
					SingleTask = new CustomTask((g, c, s, t, stack) =>
					{
						if (!(s is Enchantment e) || !(e.Target is Minion m) || m.ToBeDestroyed || !(m.Zone is Model.Zones.BoardZone)) return;
						DeathContextRules.RecordDestroy(m, e);
						m.Destroy();
					}),
				},
			});
			// The Wire / The Other Wire (3/3 permanents): "While wires are touching, take 5 damage at the start of your turn." The
			// run player starts with The Wire, a Murloc Tinyfin and The Other Wire on its board (the row's start block). At the start
			// of their player's turn, a touching pair deals 5 damage to that player's hero once (counted by its left wire).
			foreach (string wire in new[] { EncounterRules.TheWireCard, EncounterRules.TheOtherWireCard })
				BossCard(wire, () => new CardDef(new Power
				{
					Trigger = new Trigger(TriggerType.TURN_START)
					{
						SingleTask = new CustomTask((g, c, s, t, stack) =>
						{
							if (s is Minion self && EncounterRules.TouchesOnRight(self) && g.State != State.COMPLETE)
								Generic.DamageCharFunc.Invoke(self, c.Hero, WireDamage, false);
						}),
					},
				}));

			// ---------------- Madam Goya (DALA_BOSS_59h)
			// Blackmail (2): "Replace a random card in your opponent's hand with an 'Extortion.'" Cast by rule at the start of the
			// boss's turn when it can pay and the run player holds a card: what it takes is hidden from the boss (family 09's rule
			// for such powers; Actions.EncounterRules.Blackmail).
			AutoCast("DALA_BOSS_59p", c => c.Opponent.HandZone.Count > 0, (g, c, s, stack) => EncounterRules.Blackmail(c, s));
			// Extortion (2): "Return your '{0}' to your hand." The card it holds, itself.
			BossCard(EncounterRules.ExtortionCard, () => new CardDef(new Power
			{
				PowerTask = new CustomTask((g, c, s, t, stack) => EncounterRules.ReturnEscrow(c, s)),
			}));
			// Lotus Bruiser (2/3): "Whenever an 'Extortion' is played, gain +2/+2." By either player (Payday).
			BossCard(LotusBruiser, () => new CardDef(new Power
			{
				Trigger = new Trigger(TriggerType.PLAY_CARD)
				{
					TriggerSource = TriggerSource.ALL,
					Condition = new SelfCondition(p => p.Card.Id == EncounterRules.ExtortionCard),
					SingleTask = new AddEnchantmentTask(Payday, EntityType.SOURCE),
				},
			}));
			Enchantment(Payday, () => new Power { Enchant = new Enchant(Effects.AttackHealth_N(2)) });
			// Fruit Slasher (4/2): "Battlecry: Deal 4 damage to an enemy. Combo: And 1 damage to all other enemies."
			BossCard(FruitSlasher, () => new CardDef(
				new Dictionary<PlayReq, int> { { PlayReq.REQ_TARGET_IF_AVAILABLE, 0 }, { PlayReq.REQ_ENEMY_TARGET, 0 } },
				new Power
				{
					PowerTask = new DamageTask(4, EntityType.TARGET),
					ComboTask = ComplexTask.Create(new DamageTask(4, EntityType.TARGET), new DamageTask(1, EntityType.ENEMIES_NOTARGET)),
				}));

			// ---------------- the bank: Infuriated Banker (DALA_502)
			// Infuriated Banker (1/1): "Whenever a Coin is played, gain +1/+1." By either player (Rich).
			BossCard(InfuriatedBanker, () => new CardDef(new Power
			{
				Trigger = new Trigger(TriggerType.PLAY_CARD)
				{
					TriggerSource = TriggerSource.ALL,
					Condition = new SelfCondition(p => IsBankCoin(p.Card)),
					SingleTask = new AddEnchantmentTask(BankerRich, EntityType.SOURCE),
				},
			}));
			Enchantment(BankerRich, () => new Power { Enchant = new Enchant(Effects.AttackHealth_N(1)) });
		}
	}
}
