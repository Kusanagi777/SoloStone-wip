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
	public static partial class DungeonRunContent
	{
		// ---------------------------------------------------------------- Deferrals
		// The cards earlier phases deferred to phase 6 (family F15), keyed and shelled by their own client ids; every name and
		// text is the client's own, word for word (facts/violet-hold-2026-09.json).
		// (a) The wish (H12 Wish): Zephrys the Great (ULD_003, scripted in UldumCardsGen with its no-duplicates condition) and
		//     the Tombs treasures Zephrys's Lamp (ULDA_013) and Sack of Lamps (ULDA_040, which fills the hand with Lamps) share
		//     one rule. The "perfect card" is three Discover options from a fixed, curated list of scripted Basic and Classic
		//     cards, chosen by public information only: the enemy board, the enemy hero's Health and Armor, the enemy hand count,
		//     and the owner's own hand, board, mana and Health. It never reads a hidden zone and uses no RNG, so the same public
		//     position always offers the same three cards.
		// (b) "Start of Game: Draw this." (H4 SogDraw): DALA_707 Loyal Henchman, ULDA_407 Tracking Device, ULDA_013, ULDA_040
		//     and ULDA_302 Academic Research (Elise's signature) are declared through StartOfGameRules as self-drawing sources
		//     that are never dealt in an opening hand (neverOpening): they are drawn after both mulligans, before the first
		//     decision (StartOfGameRules.Resolve in MainBegin), so the draw is extra and public to both players.
		// (c) ULDA_045 Primordial Bulwark (a passive treasure) puts its Secret ULDA_045t into play at match start (OnMatchStart,
		//     before the mulligan). The Secret blocks fatal damage and deals $20 damage to the enemy hero, and it can trigger on
		//     its owner's turn (OwnTurnSecretRules: its owner's turn start does not exhaust it, in the game, in every clone and in
		//     the boss AI's sampled Secret worlds).
		// (d) GILA_511 Sticky Fingers (a passive treasure): a hand aura on its own player enchantment (GILA_511e), cards that did
		//     not start in the owner's deck cost (1) less (SelfCondition.IsNotStartInDeck: the history's frozen starting decks).
		// ULDA_111 Hearthstone, GILA_900p Temporal Loop and GILA_911 Rewind ship no content (DungeonRunAdmission: phase 9).

		/// <summary>The 9 family F15 ids that ship content, in id order (ULD_003 is scripted in UldumCardsGen).</summary>
		public static readonly IReadOnlyList<string> DeferralIds = new[]
		{
			"DALA_707", "GILA_511", "ULDA_013", "ULDA_040", "ULDA_045", "ULDA_045t", "ULDA_302", "ULDA_407", "ULD_003",
		};

		/// <summary>The family F15 ids that stay rejected (no content): turn and game restart, and the match exit (phase 9).</summary>
		public static readonly IReadOnlyList<string> DeferredToPhase9Ids = new[] { "GILA_900p", "GILA_911", "ULDA_111" };

		/// <summary>The cards that are "Start of Game: Draw this." self-drawing sources (StartOfGameRules, never in an opening hand).</summary>
		public static readonly IReadOnlyList<string> StartOfGameDrawIds = new[] { "DALA_707", "ULDA_013", "ULDA_040", "ULDA_302", "ULDA_407" };

		/// <summary>ULDA_013 Zephrys's Lamp, the card Sack of Lamps fills the hand with.</summary>
		public const string ZephrysLamp = "ULDA_013";
		/// <summary>ULDA_045t: the Secret Primordial Bulwark (ULDA_045) puts into play.</summary>
		public const string BulwarkSecret = "ULDA_045t";
		/// <summary>ULDA_045t: the damage it deals to the enemy hero (Spell Damage applies).</summary>
		public const int BulwarkDamage = 20;
		/// <summary>ULDA_302 Academic Research: the Mana Crystals it gains before any friendly minion died.</summary>
		public const int AcademicResearchBase = 1;
		/// <summary>ULDA_407 Tracking Device: the Costs of the cards it draws, in order.</summary>
		public static readonly IReadOnlyList<int> TrackingDeviceCosts = new[] { 2, 3, 4, 5 };
		/// <summary>The number of wish options (a Discover).</summary>
		public const int WishOptions = 3;

		/// <summary>The wish's burn: a spell that deals its damage to any target (to the enemy hero for lethal), by damage.</summary>
		public static readonly IReadOnlyList<(string Id, int Damage)> WishBurn = new[]
		{
			("DS1_185", 2), ("CS2_024", 3), ("CS2_029", 6), ("EX1_279", 10),
		};
		/// <summary>The wish's board clears: damage to every enemy minion (0 = destroy every minion), smallest first.</summary>
		public static readonly IReadOnlyList<(string Id, int Damage)> WishClear = new[]
		{
			("CS2_025", 1), ("CS2_093", 2), ("CS2_032", 4), ("EX1_312", 0),
		};
		/// <summary>The wish's heal (Healing Touch: restore 8 Health).</summary>
		public const string WishHeal = "CS2_007";
		public const int WishHealAmount = 8;
		/// <summary>The wish's Taunts: Sen'jin Shieldmasta, and Lord of the Arena when the owner has 6 mana.</summary>
		public const string WishTaunt = "CS2_179", WishBigTaunt = "CS2_162";
		/// <summary>The wish's hand fill (Arcane Intellect: draw 2 cards) when the owner's hand holds at most one card.</summary>
		public const string WishFill = "CS2_023";
		/// <summary>The wish's tempo minions, by Cost (the one the owner can afford first).</summary>
		public static readonly IReadOnlyList<string> WishTempo = new[] { "CS2_120", "CS2_182", "CS2_200", "CS2_201" };

		/// <summary>Every card a wish can offer (the curated candidate list), in a fixed order.</summary>
		public static IEnumerable<string> WishCandidates =>
			WishBurn.Select(b => b.Id).Concat(WishClear.Select(c => c.Id)).Concat(new[] { WishHeal, WishTaunt, WishBigTaunt, WishFill }).Concat(WishTempo);

		/// <summary>
		/// H12: the wish's three options for <paramref name="c"/>, from public information only (no hidden zone, no RNG):
		/// lethal burn first, then a board clear, a heal, a Taunt and a hand fill when the position calls for them, then the
		/// tempo minion the owner can afford, then the remaining candidates in their fixed order.
		/// </summary>
		public static IReadOnlyList<string> WishOffer(Controller c)
		{
			var op = c.Opponent;
			var picks = new List<string>();
			void Take(string id) { if (id != null && picks.Count < WishOptions && !picks.Contains(id)) picks.Add(id); }
			int spellDamage = c.CurrentSpellPower;
			int enemyLife = op.Hero.Health + op.Hero.Armor;
			var enemies = op.BoardZone.GetAll().Where(m => !m.ToBeDestroyed).ToList();
			int threat = enemies.Sum(m => Math.Max(0, m.AttackDamage)) + (op.Hero.Weapon?.AttackDamage ?? 0);
			int ownLife = c.Hero.Health + c.Hero.Armor;
			int mana = c.BaseMana;
			// Lethal: the smallest burn whose damage reaches the enemy hero's Health and Armor (Spell Damage counts).
			if (!op.Hero.IsImmune)
				Take(WishBurn.Where(b => b.Damage + spellDamage >= enemyLife).Select(b => b.Id).FirstOrDefault());
			// Board clear: two or more enemy minions that threaten, or three or more; the smallest clear that kills them all.
			if (enemies.Count >= 3 || enemies.Count >= 2 && threat >= 5 || enemies.Count >= 1 && threat >= ownLife)
			{
				int toughest = enemies.Max(m => m.Health + (m.HasDivineShield ? 100 : 0));
				Take(WishClear.Where(x => x.Damage == 0 || x.Damage + spellDamage >= toughest).Select(x => x.Id).First());
			}
			// Heal: the owner is low and 8 Health would not be wasted.
			if (c.Hero.Health <= 15 && c.Hero.BaseHealth - c.Hero.Health >= WishHealAmount / 2) Take(WishHeal);
			// Taunt: the enemy board threatens at least half the owner's life.
			if (threat > 0 && threat * 2 >= ownLife) Take(mana >= 6 ? WishBigTaunt : WishTaunt);
			// Fill: an empty or one-card hand (the wished card itself is not in it).
			if (c.HandZone.Count <= 1) Take(WishFill);
			// Tempo: the largest minion the owner can afford, then the rest by Cost.
			Take(WishTempo.LastOrDefault(id => Cards.FromId(id).Cost <= Math.Max(2, mana)));
			foreach (string id in WishCandidates) Take(id);
			return picks;
		}

		/// <summary>H12: Discover one of the wish's options (<see cref="WishOffer"/>) into the owner's hand.</summary>
		internal static ISimpleTask WishTask() => new CustomTask((g, c, s, t, stack) =>
		{
			var offer = WishOffer(c).Select(Cards.FromId).ToArray();
			if (offer.Length == 0) return;
			new EffectContext(g, c.Id, s.Id, stack: stack).Discover(offer, ChoiceAction.HAND);
		});

		/// <summary>ULD_003 Zephrys the Great's Battlecry: if the deck has no duplicates, wish (UldumCardsGen uses it).</summary>
		public static ISimpleTask ZephrysBattlecry() => ComplexTask.Create(
			new ConditionTask(EntityType.SOURCE, SelfCondition.IsNoDupeInDeck),
			new FlagTask(true, WishTask()));

		/// <summary>H4: "Start of Game: Draw this." The source is drawn from its owner's deck (after both mulligans).</summary>
		internal static void SogDraw(IPlayable source)
		{
			if (source?.Zone?.Type != Zone.DECK) return;
			Generic.Draw(source.Controller, source);
		}

		/// <summary>
		/// H4: declares <paramref name="card"/> a self-drawing Start of Game source (once per card load). StartOfGameRules resolves
		/// the lowest <paramref name="priority"/> first: the treasures here keep 0; the Rumble Run companions (phase 7 D,
		/// DungeonRunShrines.CompanionDrawPriority) draw after the board shrine (ShrineRules.SetupPriority) and the Start of Game
		/// treasures.
		/// </summary>
		internal static void DeclareSogDraw(Card card, int priority = 0)
		{
			if (!StartOfGameRules.HasEffect(card)) StartOfGameRules.Declare(card, SogDraw, priority: priority, neverOpening: true);
		}

		/// <summary>ULDA_302 Academic Research: the Mana Crystals it gains now (its script data, the printed 1 when unset).</summary>
		public static int AcademicResearchAmount(IPlayable p) =>
			p[GameTag.TAG_SCRIPT_DATA_NUM_1] > 0 ? p[GameTag.TAG_SCRIPT_DATA_NUM_1] : AcademicResearchBase;

		/// <summary>ULDA_407 Tracking Device: for each Cost 2, 3, 4 and 5, a random card of that Cost from the owner's deck.</summary>
		private static void TrackingDevice(Game g, Controller c)
		{
			foreach (int cost in TrackingDeviceCosts)
			{
				var pool = c.DeckZone.Where(p => p.Cost == cost).ToArray();
				if (pool.Length == 0) continue;
				var pick = pool[g.Random.Next(pool.Length)];
				if (pool.Length > 1) g.OnRandomHappened(true);
				Generic.Draw(c, pick);
			}
		}

		/// <summary>ULDA_040 Sack of Lamps: Zephrys's Lamps until the owner's hand is full.</summary>
		private static void SackOfLamps(Game g, Controller c, IEntity s)
		{
			var lamp = Cards.FromId(ZephrysLamp);
			while (!c.HandZone.IsFull)
				Generic.AddHandPhase.Invoke(c, Entity.FromCard(c, lamp, new EntityData { [GameTag.DISPLAYED_CREATOR] = s.Id }));
		}

		/// <summary>ULDA_045 Primordial Bulwark at match start: its Secret ULDA_045t enters its owner's Secret zone, live at once.</summary>
		private static void PrimordialBulwark(Controller c)
		{
			if (c.SecretZone.IsFull || c.SecretZone.Any(p => p.Card.Id == BulwarkSecret)) return;
			IPlayable secret = Entity.FromCard(c, Cards.FromId(BulwarkSecret), null, c.SecretZone);
			secret.Card.Power?.Trigger?.Activate(secret);
			secret.IsExhausted = !OwnTurnSecretRules.TriggersOnOwnTurn(secret) && c == c.Game.CurrentPlayer;
		}

		/// <summary>ULDA_045t: the fatal damage is blocked (the damage event's amount becomes 0) and the enemy hero takes $20.</summary>
		private static void BulwarkBlock(Game g, Controller c, IEntity s, TaskStack stack)
		{
			g.CurrentEventData.EventNumber = 0;
			new EffectContext(g, c.Id, s.Id, stack: stack).Damage(c.Opponent.Hero.Id, BulwarkDamage, spellDamage: true);
		}

		private static void Deferrals()
		{
			// ---------------- enchantments (the client's own shells, names and texts)
			Buff("fx_DALA_707e", "DALA_707e", "Scheming", "Increased stats.", Effects.AttackHealth_N(1));
			Effect("fx_GILA_511", "GILA_511e", "Sticky Fingers Player Enchant", null,
				() => new Power { Aura = new Aura(AuraType.HAND, Effects.ReduceCost(1)) { Condition = SelfCondition.IsNotStartInDeck } });

			// ---------------- (b) Start of Game draws
			Active("DALA_707", "Loyal Henchman", "[x]<b>Start of Game:</b> Draw this.\n<b>Taunt</b>. Each turn this is in\nyour hand, gain +1/+1.", 1,
				() => new CardDef(new Power
				{
					Trigger = new Trigger(TriggerType.TURN_START)
					{
						TriggerActivation = TriggerActivation.HAND,
						SingleTask = new AddEnchantmentTask("DALA_707e", EntityType.SOURCE),
					},
				}),
				card => { card.SetPrintedStats(1, 1); card.SetPrintedTaunt(true); DeclareSogDraw(card); });
			Active("ULDA_407", "Tracking Device", "[x]<b>Start of Game</b>: Draw this.\nDraw a 2, 3, 4, and 5-Cost\ncard from your deck.", 1,
				() => Def(new Power { PowerTask = new CustomTask((g, c, s, t, stack) => TrackingDevice(g, c)) }), card => DeclareSogDraw(card));
			Active("ULDA_302", "Academic Research",
				"[x]<b>Start of Game:</b> Draw this.\nGain @ Mana |4(Crystal, Crystals).\n<i>(Upgrades each time\na friendly minion dies)</i>.", 0,
				() => Def(new Power
				{
					PowerTask = new CustomTask((g, c, s, t, stack) => Generic.ChangeManaCrystal.Invoke(c, AcademicResearchAmount((IPlayable)s), true)),
					Trigger = new Trigger(TriggerType.DEATH)
					{
						TriggerSource = TriggerSource.MINIONS,
						TriggerActivation = TriggerActivation.HAND_OR_DECK,
						SingleTask = new CustomTask((g, c, s, t, stack) =>
						{
							if (s is IPlayable research) research[GameTag.TAG_SCRIPT_DATA_NUM_1] = AcademicResearchAmount(research) + 1;
						}),
					},
				}),
				card => { card.Tags[GameTag.TAG_SCRIPT_DATA_NUM_1] = AcademicResearchBase; DeclareSogDraw(card); });

			// ---------------- (a) the wish
			Active("ULDA_013", "Zephrys's Lamp", "[x]<b>Start of Game</b>: Draw this.\nWish for the perfect card.", 0,
				() => Def(new Power { PowerTask = WishTask() }), card => DeclareSogDraw(card));
			Active("ULDA_040", "Sack of Lamps", "<b>Start of Game:</b> Draw this.\nFill your hand with Zephrys's Lamps.", 0,
				() => Def(new Power { PowerTask = new CustomTask((g, c, s, t, stack) => SackOfLamps(g, c, s)) }), card => DeclareSogDraw(card), ZephrysLamp);

			// ---------------- (c) Primordial Bulwark and its Secret
			Active(BulwarkSecret, "Primordial Bulwark",
				"[x]<b>Secret:</b> Block fatal damage\nand deal $20 damage\nto the enemy hero\n<i>(can trigger on your turn)</i>.", 0,
				() => new CardDef(new Power
				{
					Trigger = new Trigger(TriggerType.PREDAMAGE)
					{
						TriggerSource = TriggerSource.HERO,
						Condition = SelfCondition.IsHeroLethalPreDamaged,
						FastExecution = true,
						SingleTask = ComplexTask.Secret(new CustomTask((g, c, s, t, stack) => BulwarkBlock(g, c, s, stack))),
					},
				}),
				card => { card.SetPrintedSecret(); card.Tags[OwnTurnSecretRules.Tag] = 1; });
			Treasure("ULDA_045", "Primordial Bulwark",
				"<b>Passive</b>\nBlock lethal damage and deal $20 damage to the enemy hero\n<i>(once per game)</i>.",
				onStart: PrimordialBulwark, ownShell: true);
			Get("ULDA_045").Tokens = new[] { BulwarkSecret };

			// ---------------- (d) Sticky Fingers
			Treasure("GILA_511", "Sticky Fingers", "<b>Passive</b>\nCards that didn't start in your deck cost (1) less.",
				effect: "fx_GILA_511", ownShell: true);
		}
	}
}
