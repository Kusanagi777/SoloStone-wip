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
	// Phase 8, family 24: the Plague Lords and Tekahn (Tombs of Terror). Three Plague Lords beside family 01's Icarax, each a
	// phased boss of three forms (300, 200, 100 Health; the deck replaced at each phase change), and Tekahn, the Tombs finale,
	// fought by the run player's hero and then the explorers in relay:
	//   Xatma, Plague Lord of Death (ULDA_BOSS_37h): Foreboding Fate, Death's Embrace, Death's Gaze; Untimely Demise.
	//   K'zrath, Plague Lord of Madness (ULDA_BOSS_38h): Systematic Betrayal (form 1), Evoke Panic (form 2), Murmurs of Mortality.
	//   Vesh, Plague Lord of Murlocs (ULDA_BOSS_39h): Made of Murlocs, Ashes Anew, Mur-nado; Scaly Golem.
	//   Tekahn, Plague Lord of Flame (ULDA_BOSS_67h): Awaken the Flame, Curse of Flame, Soul Pyre; Sanctum Golem, Enflamed
	//   Golem, Rakanishu, The Final Plague?!.
	// The texts are the client's (facts/violet-hold-2026-09.json). A power that would do nothing is offered by a condition that
	// reads only the public boards; every random choice uses the game RNG and is reported as a random event; the only state is
	// one private tag (Actions.EncounterRules.PlagueCount). A run shrine never changes sides (K'zrath's rotation passes it over),
	// never leaves its board for a deck (Mur-nado: it goes dormant, as from any such effect: Actions.ShrineRules), and is never a
	// minion that "dies" for these powers.
	public static partial class DungeonRunEncounters
	{
		/// <summary>ULDA_BOSS_37p3 Death's Gaze: the card it adds when a minion dies (Untimely Demise).</summary>
		public const string UntimelyDemise = "ULDA_BOSS_37t";
		/// <summary>ULDA_BOSS_39p2 Ashes Anew: the minion it summons (Scaly Golem).</summary>
		public const string ScalyGolem = "ULDA_BOSS_39m";
		/// <summary>Tekahn's golems: ULDA_BOSS_67p1 Awaken the Flame turns a Sanctum Golem into an Enflamed Golem.</summary>
		public const string SanctumGolem = "ULDA_031", EnflamedGolem = "ULDA_032";
		/// <summary>ULDA_BOSS_38p2 Systematic Betrayal: the +1/+1 a minion that switched sides gains.</summary>
		public const string ChosenBetrayer = "ULDA_BOSS_38p2e1";
		/// <summary>ULDA_BOSS_39p1 Made of Murlocs: Health the boss loses after a Murloc dies.</summary>
		public const int MurlocHealthLoss = 3;
		/// <summary>ULDA_BOSS_67p2 Curse of Flame: damage to all characters when the cursed minion lives to the boss's next turn.</summary>
		public const int CurseOfFlameDamage = 5;
		/// <summary>ULDA_115 "Gnomebliterator": damage to its target, and damage banked for the run's next Plague Lord per cast.</summary>
		public const int GnomebliteratorDamage = 10;
		/// <summary>
		/// ULDA_BOSS_67t The Final Plague?! ("Resummon that which was long dead"): the four Old Gods the wiki names as its summoned
		/// minions (C'Thun, N'Zoth, Y'Shaarj, Yogg-Saron), summoned in this order while the board has room (no Battlecry).
		/// </summary>
		public static readonly IReadOnlyList<string> FinalPlagueGods = new[] { "OG_280", "OG_133", "OG_042", "OG_134" };

		/// <summary>A Murloc minion (an Amalgam counts).</summary>
		public static bool IsMurloc(IPlayable p) => p is Minion m && m.Card.IsRace(Race.MURLOC);

		/// <summary>A Lackey (the client's MARK_OF_EVIL card tag, as "if you control a Lackey" reads it).</summary>
		public static bool IsLackey(IPlayable p) => p is Minion && p.Card[GameTag.MARK_OF_EVIL] == 1;

		/// <summary>
		/// ULDA_BOSS_39p3 Mur-nado: how much less it costs, the Murlocs that died while the power was active (the counter is on
		/// the power: Actions.EncounterRules.PlagueCount), as the negative value EncounterRules.RefreshCosts adds.
		/// </summary>
		public static int MurnadoReduction(Game g)
		{
			foreach (Controller c in new[] { g.Player2, g.Player1 })
			{
				HeroPower power = c.Hero?.HeroPower;
				if (power != null && power.Card.Id == "ULDA_BOSS_39p3") return -power[EncounterRules.PlagueCount];
			}
			return 0;
		}

		/// <summary>
		/// ULDA_BOSS_37p2 Death's Embrace, at a minion's death (a fast trigger: before the engine reads the dying minion's Reborn):
		/// the first minion of its player to die this turn gets Reborn (a minion reborn before is reborn again). A run shrine or
		/// a permanent is never given it.
		/// </summary>
		private static void Embrace(Game g, IPlayable t)
		{
			if (!Died(t, out Minion m)) return;
			// The deaths of its player this turn before this one (the history records each death before its triggers run); a run
			// shrine that went dormant or a permanent has not "died" for this power, so it does not use up the turn's first death.
			int earlier = g.HistoryFacts.Query.Deaths().Count(f => f.Turn == g.Turn && f.ControllerId == m.Controller.Id && f.EntityId != m.Id
				&& !(g.IdEntityDic.TryGetValue(f.EntityId, out IPlayable dead) && (ShrineRules.IsMarked(dead) || dead.Card.Untouchable)));
			if (earlier != 0) return;
			m[GameTag.REBORN] = 1;
			m[GameTag.HAS_BEEN_REBORN] = 0;
		}

		/// <summary>A Plague Lord's hero (any form of ULDA_BOSS_37h-40h: Xatma, K'zrath, Vesh, Icarax; Tekahn is not one).</summary>
		public static bool IsPlagueLordHero(Hero h) => h != null && System.Text.RegularExpressions.Regex.IsMatch(h.Card.Id, @"^ULDA_BOSS_(37|38|39|40)h[23]?$");

		/// <summary>
		/// ULDA_115 "Gnomebliterator"'s second half, after its 10 damage to the target: "Also deal $10 damage to the Plague Lord when
		/// you fight it". The amount is 10 with the cast's Spell Damage. In a Plague Lord fight it is dealt to the Plague Lord at
		/// once (the wiki: an additional 10 damage); in any other fight it is banked on the caster's controller
		/// (Actions.EncounterRules.PlagueCount, either player: a copied or stolen cast counts) for the run's next Plague Lord.
		/// </summary>
		internal static void Gnomebliterate(Game g, Controller c, IPlayable source)
		{
			int amount = SpellDamageRules.Amount(source, GnomebliteratorDamage);
			Hero lord = new[] { c.Opponent.Hero, c.Hero }.FirstOrDefault(IsPlagueLordHero);
			if (lord != null) { if (!lord.ToBeDestroyed && lord.Health > 0) Generic.DamageCharFunc(source, lord, amount, false); }
			else c[EncounterRules.PlagueCount] = c[EncounterRules.PlagueCount] + amount;
		}

		/// <summary>
		/// ULDA_BOSS_39p2 Ashes Anew: the friendly Murloc is destroyed (its death resolves first, so a full board has room), then
		/// a Scaly Golem is summoned where it stood (the right end when that slot is gone).
		/// </summary>
		private static void AshesAnew(Game g, Controller c, IEntity s, IPlayable t)
		{
			if (!(t is Minion murloc) || murloc.Controller != c || !IsMurloc(murloc)) return;
			int position = murloc.ZonePosition;
			DeathContextRules.RecordDestroy(murloc, s);
			murloc.Destroy();
			g.DeathProcessingAndAuraUpdate();
			if (g.State == State.COMPLETE || c.BoardZone.IsFull) return;
			var golem = (Minion)Entity.FromCard(c, Cards.FromId(ScalyGolem));
			Generic.SummonBlock(g, golem, System.Math.Min(position, c.BoardZone.Count), s);
		}

		/// <summary>
		/// ULDA_BOSS_37p3 Death's Gaze: 1 damage to every minion (a hero power: no Spell Damage), then, if any of them is dying,
		/// an Untimely Demise is added to the boss's hand (none when the hand is full).
		/// </summary>
		private static void DeathsGaze(Game g, Controller c, IEntity s)
		{
			var minions = g.Player1.BoardZone.Concat(g.Player2.BoardZone).Where(m => !m.Untouchable).OrderBy(m => m.OrderOfPlay).ToList();
			foreach (Minion m in minions)
				if (m.Zone?.Type == Zone.PLAY) Generic.DamageCharFunc((IPlayable)s, m, 1, false);
			bool died = minions.Any(m => !ShrineRules.IsMarked(m) && (m.ToBeDestroyed || m.Health <= 0));
			if (died && !c.HandZone.IsFull) Generic.AddHandPhase.Invoke(c, Entity.FromCard(c, Cards.FromId(UntimelyDemise)));
		}

		/// <summary>
		/// ULDA_BOSS_39p3 Mur-nado: every minion of both boards (in play order) is shuffled into the boss's deck (MoveToDeck: an
		/// enemy minion changes controller as it goes); a run shrine goes dormant instead and a permanent stays.
		/// </summary>
		private static void Murnado(Game g, Controller c, IEntity s)
		{
			var stack = new TaskStack
			{
				Playables = g.Player1.BoardZone.Concat(g.Player2.BoardZone).Where(m => !m.Untouchable).OrderBy(m => m.OrderOfPlay)
					.Cast<IPlayable>().ToList(),
			};
			if (stack.Playables.Count == 0) return;
			new MoveToDeck(EntityType.STACK).Process(g, c, s, null, stack);
		}

		/// <summary>
		/// ULDA_BOSS_67t The Final Plague?!: the four Old Gods (<see cref="FinalPlagueGods"/>) are summoned for its caster, board
		/// permitting.
		/// </summary>
		private static void FinalPlague(Game g, Controller c, IEntity s)
		{
			foreach (string id in FinalPlagueGods)
			{
				if (c.BoardZone.IsFull || g.State == State.COMPLETE) break;
				Generic.SummonBlock(g, (Minion)Entity.FromCard(c, Cards.FromId(id), new EntityData { [GameTag.DISPLAYED_CREATOR] = s.Id }), -1, s);
			}
		}

		/// <summary>
		/// ULDA_BOSS_67p2e Curse of Flame's enchantment at the start of its owner's (the boss's) turn: the curse is removed; when the
		/// cursed minion is still in play, 5 damage to every character (both heroes, then the minions in play order).
		/// </summary>
		private static void CurseOfFlame(Game g, IEntity s)
		{
			if (!(s is Enchantment curse)) return;
			bool alive = curse.Target is Minion cursed && cursed.Zone?.Type == Zone.PLAY && !cursed.ToBeDestroyed;
			curse.Remove();
			if (!alive || g.State == State.COMPLETE) return;
			var all = new List<ICharacter> { g.CurrentPlayer.Hero, g.CurrentPlayer.Opponent.Hero };
			all.AddRange(g.Player1.BoardZone.Concat(g.Player2.BoardZone).Where(m => !m.Untouchable).OrderBy(m => m.OrderOfPlay));
			foreach (ICharacter x in all) Generic.DamageCharFunc(curse, x, CurseOfFlameDamage, false);
		}

		// "Summon N Murlocs that died this game." (Anyfin Can Happen's rule, LOE_026: the graveyard's Murloc minions, N of them at
		// random, summoned as copies.)
		private static ISimpleTask MurlocsThatDied(int n) => ComplexTask.Create(
			new IncludeTask(EntityType.GRAVEYARD),
			new FilterStackTask(SelfCondition.IsMinion, SelfCondition.IsRace(Race.MURLOC)),
			new RandomTask(n, EntityType.STACK),
			new SummonCopyTask(EntityType.STACK));

		private static void F24()
		{
			// ================ Xatma, Plague Lord of Death (ULDA_BOSS_37h / h2 / h3: 300, 200, 100)
			// Foreboding Fate (0): "Trigger a friendly minion's Deathrattle." (Play Dead's rule.)
			Add("ULDA_BOSS_37p1", EncounterCardKind.Power, () => new CardDef(
				new Dictionary<PlayReq, int>
				{
					{ PlayReq.REQ_TARGET_TO_PLAY, 0 }, { PlayReq.REQ_MINION_TARGET, 0 }, { PlayReq.REQ_FRIENDLY_TARGET, 0 }, { PlayReq.REQ_TARGET_WITH_DEATHRATTLE, 0 },
				},
				new Power { PowerTask = new ActivateDeathrattleTask(EntityType.TARGET) }));
			// Death's Embrace: "Passive Hero Power. The first minion for each player that dies in a turn has Reborn."
			Passive("ULDA_BOSS_37p2", () => new Power
			{
				Trigger = new Trigger(TriggerType.DEATH)
				{
					TriggerSource = TriggerSource.ALL_MINIONS,
					FastExecution = true,
					SingleTask = new CustomTask((g, c, s, t, stack) => Embrace(g, t)),
				},
			});
			// Death's Gaze (1): "Deal 1 damage to all minions. If any die, add an Untimely Demise to your hand."
			Power("ULDA_BOSS_37p3", null, () => new CustomTask((g, c, s, t, stack) => DeathsGaze(g, c, s)));
			// Untimely Demise (3): "Deal $3 damage to all enemies."
			BossCard(UntimelyDemise, () => new CardDef(new Power { PowerTask = new DamageTask(3, EntityType.ENEMIES, true) }));

			// ================ K'zrath, Plague Lord of Madness (ULDA_BOSS_38h / h2 / h3: 300, 200, 100)
			// The client links the 300 form to Systematic Betrayal (38p2) and the 200 form to Evoke Panic (38p1); the rows follow it.
			// Systematic Betrayal (0): "Rotate all minions counter-clockwise. Minions that switch sides gain +1/+1." Counter-clockwise
			// as the run player sees the boards (its row at the bottom): family 16's EncounterRules.Rotate; a run shrine keeps its
			// place. Offered while any minion can rotate.
			Power("ULDA_BOSS_38p2", null, () => new CustomTask((g, c, s, t, stack) =>
			{
				foreach (Minion crossed in EncounterRules.Rotate(c.Opponent, false, s))
					if (crossed.Zone?.Type == Zone.PLAY) Generic.AddEnchantmentBlock(g, Cards.FromId(ChosenBetrayer), (IPlayable)s, crossed);
			}), usable: c => EncounterRules.RotationRing(c).Count + EncounterRules.RotationRing(c.Opponent).Count > 0);
			Enchantment(ChosenBetrayer, () => new Power { Enchant = new Enchant(Effects.AttackHealth_N(1)) });
			// Evoke Panic (0): "Force the minion with the lowest Health to attack one of its neighbors." (Either row; offered while
			// a row holds two neighbors that can fight.)
			Power("ULDA_BOSS_38p1", null, () => new CustomTask((g, c, s, t, stack) => EncounterRules.EvokePanic(g)),
				usable: c => EncounterRules.PanicPossible(c.Game));
			// Murmurs of Mortality: "Passive Hero Power. After a friendly minion dies, bring an enemy minion to 1 Health."
			Passive("ULDA_BOSS_38p3", () => new Power
			{
				Trigger = new Trigger(TriggerType.DEATH)
				{
					TriggerSource = TriggerSource.MINIONS,
					SingleTask = new CustomTask((g, c, s, t, stack) =>
					{
						if (Died(t, out _)) EncounterRules.BringToOne(c.Opponent);
					}),
				},
			});

			// ================ Vesh, Plague Lord of Murlocs (ULDA_BOSS_39h / h2 / h3: 300, 200, 100)
			// Made of Murlocs: "Passive Hero Power. You are Immune. After a Murloc dies, lose 3 Health." Any Murloc of either board;
			// Health lost, not damage (EncounterRules.LoseHealth), so the Immune hero still loses it and its next form enters at once.
			Passive("ULDA_BOSS_39p1", () => new Power
			{
				Aura = new Aura(AuraType.HERO, Effects.Immune),
				Trigger = new Trigger(TriggerType.DEATH)
				{
					TriggerSource = TriggerSource.ALL_MINIONS,
					SingleTask = new CustomTask((g, c, s, t, stack) =>
					{
						if (Died(t, out Minion m) && IsMurloc(m)) EncounterRules.LoseHealth(c.Hero, MurlocHealthLoss);
					}),
				},
			});
			// Ashes Anew (0): "Destroy a friendly Murloc to summon a Scaly Golem."
			Add("ULDA_BOSS_39p2", EncounterCardKind.Power, () => new CardDef(
				new Dictionary<PlayReq, int> { { PlayReq.REQ_TARGET_TO_PLAY, 0 }, { PlayReq.REQ_MINION_TARGET, 0 }, { PlayReq.REQ_FRIENDLY_TARGET, 0 } },
				new Power { PowerTask = new CustomTask((g, c, s, t, stack) => AshesAnew(g, c, s, t)) }),
				card => card.SourceTargetingPredicate = (source, target) => IsMurloc(target));
			// Mur-nado (15): "Shuffle all minions into your deck. After a Murloc dies, reduce this Cost by (1)." The Murlocs that
			// died while the power was active are counted on it (EncounterRules.PlagueCount); its Cost follows at every aura update.
			Add("ULDA_BOSS_39p3", EncounterCardKind.Power, () => new CardDef(new Power
			{
				PowerTask = new CustomTask((g, c, s, t, stack) => Murnado(g, c, s)),
				Trigger = new Trigger(TriggerType.DEATH)
				{
					TriggerSource = TriggerSource.ALL_MINIONS,
					SingleTask = new CustomTask((g, c, s, t, stack) =>
					{
						if (s is HeroPower power && Died(t, out Minion m) && IsMurloc(m)) power[EncounterRules.PlagueCount] = power[EncounterRules.PlagueCount] + 1;
					}),
				},
			}), activate: true).DynamicCost = MurnadoReduction;
			// Scaly Golem (1, 0/4 Taunt): "Deathrattle: Give all Murlocs +1/+1." Every Murloc of both boards.
			BossCard(ScalyGolem, () => new CardDef(new Power
			{
				DeathrattleTask = new CustomTask((g, c, s, t, stack) =>
				{
					Card earthen = Cards.FromId("ULDA_BOSS_39me");
					foreach (Minion m in g.Player1.BoardZone.Concat(g.Player2.BoardZone).Where(IsMurloc).OrderBy(m => m.OrderOfPlay).ToList())
						Generic.AddEnchantmentBlock(g, earthen, (IPlayable)s, m);
				}),
			}));
			Enchantment("ULDA_BOSS_39me", () => new Power { Enchant = new Enchant(Effects.AttackHealth_N(1)) });
			// Vesh's special cards (the wiki's "Special cards" of his page; boss-only cards of older adventures his phase decks list).
			// Mrgl Mrgl Nyah Nyah (League of Explorers; 5 / Heroic 3): "Summon 3 (5) Murlocs that died this game." Anyfin Can Happen's
			// rule (LOE_026) with its own count.
			BossCard("LOEA10_5", () => new CardDef(new Power { PowerTask = MurlocsThatDied(3) }));
			BossCard("LOEA10_5H", () => new CardDef(new Power { PowerTask = MurlocsThatDied(5) }));
			// Demonic Presence (Karazhan, Heroic; 3): "Draw 3 cards. Gain 10 Armor." (also listed by Tekahn's Heroic decks)
			BossCard("KARA_13_12H", () => new CardDef(new Power { PowerTask = ComplexTask.Create(new DrawTask(3), new ArmorTask(10)) }));
			// Giantfin (League of Explorers, Heroic; 10, 10/10 Murloc): "At the end of your turn, draw 2 cards."
			BossCard("LOEA16_24H", () => new CardDef(new Power
			{
				Trigger = new Trigger(TriggerType.TURN_END) { SingleTask = new DrawTask(2) },
			}));

			// ================ Tekahn, Plague Lord of Flame (ULDA_BOSS_67h: 300; the Tombs finale)
			// Awaken the Flame (2): "Imbue a Sanctum Golem with the power of fire." The Sanctum Golem becomes an Enflamed Golem in
			// place, keeping its enchantments, stats and damage (the wiki's note).
			Add("ULDA_BOSS_67p1", EncounterCardKind.Power, () => new CardDef(
				new Dictionary<PlayReq, int> { { PlayReq.REQ_TARGET_TO_PLAY, 0 }, { PlayReq.REQ_MINION_TARGET, 0 } },
				new Power { PowerTask = new ChangeEntityTask(EnflamedGolem, EntityType.TARGET) }),
				card => card.SourceTargetingPredicate = (source, target) => target.Card.Id == SanctumGolem);
			// Curse of Flame (2): "Choose an enemy minion. If it lives until your next turn, deal 5 damage to all characters."
			Power("ULDA_BOSS_67p2", new[] { PlayReq.REQ_TARGET_TO_PLAY, PlayReq.REQ_MINION_TARGET, PlayReq.REQ_ENEMY_TARGET },
				() => new AddEnchantmentTask("ULDA_BOSS_67p2e", EntityType.TARGET));
			// Its enchantment: "On Tekahn's turn, deal 5 damage to all characters." The enchantment belongs to the boss, so its
			// trigger fires at the start of the boss's turn (CurseOfFlame).
			Enchantment("ULDA_BOSS_67p2e", () => new Power
			{
				Trigger = new Trigger(TriggerType.TURN_START) { SingleTask = new CustomTask((g, c, s, t, stack) => CurseOfFlame(g, s)) },
			});
			// Soul Pyre: "Passive Hero Power. After a player plays a card, they take damage equal to its Cost." The Cost paid for it
			// (the engine's last known Cost in hand), dealt by the power to the hero of the card's controller.
			Passive("ULDA_BOSS_67p3", () => new Power
			{
				Trigger = new Trigger(TriggerType.AFTER_PLAY_CARD)
				{
					TriggerSource = TriggerSource.ALL,
					SingleTask = new CustomTask((g, c, s, t, stack) =>
					{
						if (t == null || g.State == State.COMPLETE) return;
						int cost = t[GameTag.TAG_LAST_KNOWN_COST_IN_HAND];
						if (cost > 0) Generic.DamageCharFunc((IPlayable)s, t.Controller.Hero, cost, false);
					}),
				},
			});
			// Sanctum Golem (4, 6/6): "Can't Attack." (the client's CANT_ATTACK tag).
			Token(SanctumGolem);
			// Enflamed Golem (4, 6/6): "Deathrattle: Deal 3 damage to all minions and summon a Sanctum Golem."
			BossCard(EnflamedGolem, () => new CardDef(new Power
			{
				DeathrattleTask = ComplexTask.Create(new DamageTask(3, EntityType.ALLMINIONS), new SummonTask(SanctumGolem, SummonSide.DEATHRATTLE)),
			}));
			// Rakanishu (2, 4/4): "Battlecry: For the rest of the game, your Lackeys are 4/4." Dark Pharaoh Tekahn's rule: the
			// client's player enchantment ULD_168e (a hand-and-board aura of ULD_168e3, 4/4, on every Lackey).
			BossCard("ULDA_033", () => new CardDef(new Power { PowerTask = new AddEnchantmentTask("ULD_168e", EntityType.CONTROLLER) }));
			// The Final Plague?! (10): "Resummon that which was long dead." The four Old Gods.
			BossCard("ULDA_BOSS_67t", () => new CardDef(new Power { PowerTask = new CustomTask((g, c, s, t, stack) => FinalPlague(g, c, s)) }));
		}
	}

	public static partial class DungeonRunContent
	{
		// ---------------------------------------------------------------- PlagueLordTreasures (phase 8, family 24)
		// ULDA_115 "Gnomebliterator", the Tombs active treasure phase 6 left out until the Plague Lords existed: keyed and shelled
		// by its own client id like the other Tombs actives (name and text the client's). Its first half is a spell (10 damage,
		// Spell Damage applies); its second half (DungeonRunEncounters.Gnomebliterate) deals as much again to the Plague Lord at
		// once in a Plague Lord fight, and otherwise banks it on the caster's controller (Actions.EncounterRules.PlagueCount): the
		// match harvest reports both players' banked damage (RunMatchEnd.GnomebliteratorDamage) and the run carries it into its
		// next Plague Lord fight (DungeonRunState.PlagueLordDamage).
		private static void PlagueLordTreasures()
		{
			Active(Gnomebliterator, "\"Gnomebliterator\"", "Deal $10 damage. Also deal $10 damage to the <b>Plague Lord</b> when you fight it.", 2,
				() => Def(new Power
				{
					PowerTask = ComplexTask.Create(
						new DamageTask(DungeonRunEncounters.GnomebliteratorDamage, EntityType.TARGET, true),
						new CustomTask((g, c, s, t, stack) => DungeonRunEncounters.Gnomebliterate(g, c, (IPlayable)s))),
				}, PlayReq.REQ_TARGET_TO_PLAY));
		}
	}
}
