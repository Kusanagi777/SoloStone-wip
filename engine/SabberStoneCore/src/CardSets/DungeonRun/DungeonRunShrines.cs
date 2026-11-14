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
	/// Rumble Run board shrines, their shrine upgrades and the teammates (phase 7). The cards are the real client TRLA cards,
	/// keyed and shelled by their own ids (no borrowed shell, no run content entry): each script sits on the card itself, so
	/// the client shows it as it is. The shrine model (placement, dormancy, revival, guards) is <see cref="ShrineRules"/>.
	/// Phase 7 S1 scripts the model and the cards it needs to be testable; the other shrines, upgrades and teammates are
	/// listed in the run data (run-v1.json "shrine", "teammates", "shrineUpgrades") and offered once they appear here.
	/// Every random choice uses the game RNG; every piece of state is an entity tag.
	/// </summary>
	public static partial class DungeonRunShrines
	{
		/// <summary>The scripted shrines and their dormant forms (TRLA_107 Tribal Shrine is a test fixture, never offered).</summary>
		internal static readonly IReadOnlyDictionary<string, string> Forms = new Dictionary<string, string>(StringComparer.Ordinal)
		{
			// Druid
			["TRLA_112"] = "TRLA_112t", ["TRLA_115"] = "TRLA_115t", ["TRLA_116"] = "TRLA_116t",
			// Hunter
			["TRLA_110"] = "TRLA_110t", ["TRLA_162"] = "TRLA_162t", ["TRLA_163"] = "TRLA_163t",
			// Warrior
			["TRLA_104"] = "TRLA_104t", ["TRLA_170"] = "TRLA_170t", ["TRLA_171"] = "TRLA_171t",
			// Mage, Paladin, Priest (phase 7 S2: DungeonRunShrines.S2.cs)
			["TRLA_108"] = "TRLA_108t", ["TRLA_128"] = "TRLA_128t", ["TRLA_129"] = "TRLA_129t",
			["TRLA_105"] = "TRLA_105t", ["TRLA_137"] = "TRLA_137t", ["TRLA_138"] = "TRLA_138t",
			["TRLA_114"] = "TRLA_114t", ["TRLA_146"] = "TRLA_146t", ["TRLA_147"] = "TRLA_147t",
			// Rogue, Shaman, Warlock (phase 7 S3: DungeonRunShrines.S3.cs)
			["TRLA_106"] = "TRLA_106t", ["TRLA_186"] = "TRLA_186t", ["TRLA_187"] = "TRLA_187t",
			["TRLA_109"] = "TRLA_109t", ["TRLA_154"] = "TRLA_154t", ["TRLA_155"] = "TRLA_155t",
			["TRLA_113"] = "TRLA_113t", ["TRLA_178"] = "TRLA_178t", ["TRLA_179"] = "TRLA_179t",
			// the enemy-shrine fixture
			["TRLA_107"] = "TRLA_107t",
		};

		// The shrines' printed Health (the client data's), given to their dormant forms: the card database is still loading
		// when a form is configured, so it cannot be read from the shrine card there.
		private static readonly IReadOnlyDictionary<string, int> PrintedHealth = new Dictionary<string, int>(StringComparer.Ordinal)
		{
			["TRLA_112"] = 5, ["TRLA_115"] = 3, ["TRLA_116"] = 7,
			["TRLA_110"] = 8, ["TRLA_162"] = 5, ["TRLA_163"] = 6,
			["TRLA_104"] = 8, ["TRLA_170"] = 8, ["TRLA_171"] = 5,
			["TRLA_108"] = 5, ["TRLA_128"] = 8, ["TRLA_129"] = 7,
			["TRLA_105"] = 4, ["TRLA_137"] = 5, ["TRLA_138"] = 5,
			["TRLA_114"] = 3, ["TRLA_146"] = 5, ["TRLA_147"] = 8,
			["TRLA_106"] = 2, ["TRLA_186"] = 2, ["TRLA_187"] = 5,
			["TRLA_109"] = 2, ["TRLA_154"] = 3, ["TRLA_155"] = 10,
			["TRLA_113"] = 10, ["TRLA_178"] = 4, ["TRLA_179"] = 8,
			["TRLA_107"] = 1,
		};

		/// <summary>TRLA_107 Tribal Shrine: a scripted enemy-shrine fixture (tests place it on the boss side); never offered.</summary>
		public const string FixtureShrine = "TRLA_107";

		/// <summary>The teammates the engine scripts (the run layer's teammate draft offers only these).</summary>
		public static readonly IReadOnlyCollection<string> ScriptedTeammates = new HashSet<string>(StringComparer.Ordinal)
		{
			"TRLA_124", "TRLA_125", "TRLA_127",
			// phase 7 T1 (DungeonRunShrines.T1.cs): the rest of the Druid, Hunter, Mage and Paladin teammates (their companions,
			// TRLA_121/164/135/139, are phase 7 D's, below)
			"TRLA_122", "TRLA_123",
			"TRLA_165", "TRLA_166", "TRLA_167", "TRLA_168", "TRLA_169",
			"TRLA_130", "TRLA_131", "TRLA_132", "TRLA_133", "TRLA_134",
			"TRLA_140", "TRLA_141", "TRLA_142", "TRLA_143", "TRLA_144",
			// phase 7 T2 (DungeonRunShrines.T2.cs): the Priest, Rogue, Shaman, Warlock and Warrior teammates (their companions,
			// TRLA_148/188/156/180/172, are phase 7 D's, below)
			"TRLA_149", "TRLA_150", "TRLA_151", "TRLA_152", "TRLA_153",
			"TRLA_189", "TRLA_190", "TRLA_191", "TRLA_192", "TRLA_193",
			"TRLA_157", "TRLA_158", "TRLA_159", "TRLA_160", "TRLA_161",
			"TRLA_181", "TRLA_182", "TRLA_183", "TRLA_184", "TRLA_185",
			"TRLA_173", "TRLA_174", "TRLA_175", "TRLA_176", "TRLA_177",
			// phase 7 D (DungeonRunShrines.D.cs): the nine start-of-game companions, one per class (CompanionIds)
			"TRLA_121", "TRLA_135", "TRLA_139", "TRLA_148", "TRLA_156", "TRLA_164", "TRLA_172", "TRLA_180", "TRLA_188",
		};

		/// <summary>TRLA_804 Everlasting Devotion: "Start the game with a copy of your Shrine." (RunMatchPlan.PlayerShrineCopies).</summary>
		public const string EverlastingDevotion = "TRLA_804";
		/// <summary>TRLA_805 Pact with the Loa: "Your Shrines revive in 2 turns instead of 3." (its player enchantment TRLA_805e).</summary>
		public const string PactWithTheLoa = "TRLA_805";

		/// <summary>The shrine upgrades the engine scripts, and the player enchantment each attaches at match start (null: none).</summary>
		public static readonly IReadOnlyDictionary<string, string> ScriptedUpgrades = new Dictionary<string, string>(StringComparer.Ordinal)
		{
			[EverlastingDevotion] = null, [PactWithTheLoa] = ShrineRules.FasterRevivalEnchantment,
			// phase 7 U (DungeonRunShrines.U.cs): the client's "Player Enchant" record of each upgrade; Grants maps it to the
			// enchantment every run shrine of its owner carries.
			[SacredRitual] = "TRLA_801e2", [FavorOfTheGods] = "TRLA_802e2", [FortifiedSpirit] = "TRLA_803e2",
			[WrathOfTheLoas] = "TRLA_806e", [BoonReaper] = "TRLA_807e", [RoaringEdifice] = "TRLA_808e",
			// phase 7 D (DungeonRunShrines.D.cs)
			[WindfallBlessing] = "TRLA_809e",
		};

		/// <summary>The shrine copies a run starts with: 2 with TRLA_804, else 1.</summary>
		public static int Copies(IEnumerable<string> upgrades) => upgrades != null && upgrades.Contains(EverlastingDevotion) ? 2 : 1;

		/// <summary>
		/// Attaches the upgrades' player enchantments (TRLA_805e, TRLA_801e2 ...), in pick order, with their auras live (TRLA_802e2's
		/// Taunt). DungeonRunSetup.ApplyAfterStart calls it before the shrines are armed, so each shrine that lands gets its grants
		/// (<see cref="Grant"/>); an unscripted upgrade is refused (the run layer never plans one).
		/// </summary>
		public static void ApplyUpgrades(Controller c, IEnumerable<string> upgrades)
		{
			foreach (string id in upgrades ?? Array.Empty<string>())
			{
				if (!ScriptedUpgrades.TryGetValue(id, out string enchantment)) throw new ArgumentException("Not a scripted shrine upgrade: " + id);
				if (enchantment == null) continue;
				bool fresh = c.AppliedEnchantments?.Any(e => e.Card.Id == enchantment) != true;
				QuestRules.AddPlayerEffect(c, c.Hero, enchantment);
				var aura = Cards.FromId(enchantment).Power?.Aura;
				if (fresh && aura != null) aura.Activate(c.AppliedEnchantments.Last(e => e.Card.Id == enchantment));
			}
		}

		/// <summary>Registers the scripts. Called by DungeonRunCardsGen.AddAll while the card database loads.</summary>
		internal static void AddAll(Dictionary<string, CardDef> cards)
		{
			// ---------------- shrines (Druid: TRLA_112, TRLA_115, TRLA_116) and the TRLA_107 fixture
			Shrine(cards, "TRLA_112", new Power
			{
				// "Whenever you gain Armor, refresh your Mana Crystals."
				Trigger = new Trigger(TriggerType.ARMOR)
				{
					TriggerSource = TriggerSource.FRIENDLY,
					SingleTask = new CustomTask((g, c, s, t, stack) =>
					{
						if (c.UsedMana > 0) new EffectContext(g, c.Id, s.Id, stack: stack).RefreshMana(c.UsedMana);
					}),
				},
			});
			Shrine(cards, "TRLA_115", new Power
			{
				// "After you summon a minion, give it +1/+1."
				Trigger = new Trigger(TriggerType.AFTER_SUMMON)
				{
					TriggerSource = TriggerSource.MINIONS_EXCEPT_SELF,
					SingleTask = new AddEnchantmentTask("TRLA_115e", EntityType.TARGET),
				},
			});
			cards.Add("TRLA_115e", Implemented(new CardDef(new Power { Enchant = new Enchant(Effects.AttackHealth_N(1)) })));
			Shrine(cards, "TRLA_116", new Power
			{
				// "At the end of your turn, gain Attack equal to your hero's Attack."
				Trigger = new Trigger(TriggerType.TURN_END)
				{
					SingleTask = new CustomTask((g, c, s, t, stack) =>
					{
						int attack = c.Hero.AttackDamage;
						if (attack > 0 && s is Minion shrine && shrine.Zone == c.BoardZone)
							Generic.AddEnchantmentBlock(g, Cards.FromId("TRLA_116e"), shrine, shrine, attack);
					}),
				},
			});
			cards.Add("TRLA_116e", Implemented(new CardDef(new Power { Enchant = new Enchant(Effects.Attack_N(0)) { UseScriptTag = true } })));

			// ---------------- shrines (Hunter: TRLA_110, TRLA_162, TRLA_163)
			Shrine(cards, "TRLA_110", new Power
			{
				// "After you cast a spell, put a random Hunter Secret into the battlefield." Put, not cast (no "cast a spell"
				// trigger, so it never feeds itself): a scripted Hunter Secret not already in play, drawn with the game RNG from
				// the Secrets this game lets the owner's generation yield (DungeonRunContent.RandomSecretCandidates; a board-shrine
				// run is a catalog run, whose player may generate Secrets). Nothing when the Secret zone is full.
				Trigger = new Trigger(TriggerType.AFTER_CAST)
				{
					TriggerSource = TriggerSource.FRIENDLY,
					SingleTask = new CustomTask((g, c, s, t, stack) =>
					{
						if (s is Minion shrine && shrine.Zone == c.BoardZone) DungeonRunContent.PlayRandomSecrets(c, 1, CardClass.HUNTER);
					}),
				},
			});
			Shrine(cards, "TRLA_162", new Power
			{
				// "Your minions have 'Overkill: Cards in your hand cost (1) less.'" (TRLA_162e, an Overkill an aura grants:
				// OverkillRules). The shrine itself is one of "your minions".
				Aura = new Aura(AuraType.BOARD, "TRLA_162e"),
			});
			cards.Add("TRLA_162e", Implemented(new CardDef(new Power
			{
				Enchant = new Enchant(Array.Empty<IEffect>()),
				OverkillTask = new AddEnchantmentTask("TRLA_162e2", EntityType.HAND),
			})));
			cards.Add("TRLA_162e2", Implemented(new CardDef(new Power { Enchant = new Enchant(Effects.ReduceCost(1)) })));
			Shrine(cards, "TRLA_163", new Power
			{
				// "Whenever you summon a Beast, it gains Rush, Taunt, or Poisonous" (one of the three, game RNG).
				Trigger = new Trigger(TriggerType.SUMMON)
				{
					TriggerSource = TriggerSource.MINIONS_EXCEPT_SELF,
					Condition = SelfCondition.IsRace(Race.BEAST),
					SingleTask = new CustomTask((g, c, s, t, stack) =>
					{
						if (!(t is Minion beast) || !(s is Minion shrine) || shrine.Zone != c.BoardZone) return;
						string grant = GuiseGrants[g.Random.Next(GuiseGrants.Length)];
						Generic.AddEnchantmentBlock(g, Cards.FromId(grant), shrine, beast);
					}),
				},
			});
			cards.Add("TRLA_163e", Implemented(new CardDef(new Power { Enchant = new Enchant(Effects.Rush) })));
			cards.Add("TRLA_163e3", Implemented(new CardDef(new Power { Enchant = new Enchant(Effects.TauntEff) })));
			cards.Add("TRLA_163e4", Implemented(new CardDef(new Power { Enchant = new Enchant(new Effect(GameTag.POISONOUS, EffectOperator.SET, 1)) })));

			// ---------------- shrines (Warrior: TRLA_104, TRLA_170, TRLA_171)
			Shrine(cards, "TRLA_104", new Power
			{
				// "This minion's Attack is always equal to your Armor." (an adaptive Attack, as Lightspawn's).
				Aura = new AdaptiveEffect(GameTag.ATK, EffectOperator.SET, p => p.Controller.Hero.Armor),
			});
			Shrine(cards, "TRLA_170", new Power
			{
				// "After you summon a Dragon, reduce the Costs of Dragons in your hand by (1)." (DRG_300e: "Costs (1) less.";
				// the client has no TRLA_170 enchantment).
				Trigger = new Trigger(TriggerType.AFTER_SUMMON)
				{
					TriggerSource = TriggerSource.MINIONS_EXCEPT_SELF,
					Condition = SelfCondition.IsRace(Race.DRAGON),
					SingleTask = ComplexTask.Create(
						new IncludeTask(EntityType.HAND),
						new FilterStackTask(SelfCondition.IsRace(Race.DRAGON)),
						new AddEnchantmentTask(WarDrumEnchantment, EntityType.STACK)),
				},
			});
			Shrine(cards, "TRLA_171", new Power
			{
				// "Your minions have 'Overkill: This can attack again.'" (TRLA_171e, an aura-granted Overkill: OverkillRules).
				Aura = new Aura(AuraType.BOARD, "TRLA_171e"),
			});
			cards.Add("TRLA_171e", Implemented(new CardDef(new Power
			{
				Enchant = new Enchant(Array.Empty<IEffect>()),
				OverkillTask = OverkillRules.AttackAgainTask,
			})));

			// "Shrine. Taunt." (Taunt from the card data).
			Shrine(cards, FixtureShrine, new Power());

			// ---------------- shrines (Mage, Paladin, Priest: phase 7 S2)
			AddS2(cards);

			// ---------------- shrines (Rogue, Shaman, Warlock: phase 7 S3)
			AddS3(cards);

			// ---------------- shrine upgrades: their player enchantments (the passives themselves are run rewards, no deck card)
			cards.Add(ShrineRules.FasterRevivalEnchantment, Implemented(new CardDef(new Power())));
			AddU(cards);
			// ---------------- the start-of-game companions and TRLA_809's records (phase 7 D)
			AddD(cards);
			foreach (string id in ScriptedUpgrades.Keys) cards.Add(id, Implemented(new CardDef()));

			// ---------------- teammates (Druid)
			// TRLA_124 Zandalari Striker: "After your hero attacks, give your minions +2 Attack."
			cards.Add("TRLA_124", Implemented(new CardDef(new Power
			{
				Trigger = new Trigger(TriggerType.AFTER_ATTACK)
				{
					TriggerSource = TriggerSource.HERO,
					SingleTask = new AddEnchantmentTask("TRLA_124e", EntityType.MINIONS),
				},
			})));
			cards.Add("TRLA_124e", Implemented(new CardDef(new Power { Enchant = new Enchant(Effects.Attack_N(2)) })));
			// TRLA_125 The Fan Favorite: "Overkill: Give your hero +10 Attack this turn."
			cards.Add("TRLA_125", Implemented(new CardDef(new Power { OverkillTask = new AddEnchantmentTask("TRLA_125e", EntityType.HERO) })));
			cards.Add("TRLA_125e", Implemented(new CardDef(new Power { Enchant = new Enchant(Effects.Attack_N(10)) { IsOneTurnEffect = true } })));
			// TRLA_127 Astral Raptor: "Rush. Overkill: Summon another Astral Raptor." (Rush from the card data).
			cards.Add("TRLA_127", Implemented(new CardDef(new Power { OverkillTask = new SummonTask("TRLA_127") })));

			// ---------------- teammates (phase 7 T1: Druid, Hunter, Mage, Paladin)
			AddT1(cards);

			// ---------------- teammates (phase 7 T2: Priest, Rogue, Shaman, Warlock, Warrior)
			AddT2(cards);
		}

		/// <summary>TRLA_163 Halazzi's Guise's three grants: Rush (TRLA_163e), Taunt (TRLA_163e3), Poisonous (TRLA_163e4).</summary>
		internal static readonly string[] GuiseGrants = { "TRLA_163e", "TRLA_163e3", "TRLA_163e4" };
		/// <summary>TRLA_170 Akali's War Drum's cost reduction on the Dragons in hand: the client's DRG_300e "Costs (1) less."</summary>
		internal const string WarDrumEnchantment = "DRG_300e";

		private static CardDef Implemented(CardDef def)
		{
			var configure = def.Configure;
			def.Configure = card => { card.Implemented = true; configure?.Invoke(card); };
			return def;
		}

		/// <summary>
		/// A board shrine and its dormant form: the shrine's script and its Start of Game placement (an armed set-aside shrine
		/// summons itself, ShrineRules.Arm); the form is a timed sleeper (3 owner's turns, ShrineRules.DormantCondition) whose
		/// awakening revives the shrine (ShrineRules.ReviveTask). The form gets the shrine's Health so DormantRules can hold it.
		/// </summary>
		private static void Shrine(Dictionary<string, CardDef> cards, string id, Power power)
		{
			string form = Forms[id];
			cards.Add(id, Implemented(new CardDef(power) { Configure = card =>
			{
				if (!StartOfGameRules.HasEffect(card)) StartOfGameRules.Declare(card, ShrineRules.EnterFromSetaside, priority: ShrineRules.SetupPriority);
			} }));
			cards.Add(form, Implemented(new CardDef { Configure = card =>
			{
				if (card.Health <= 0) card.SetPrintedHealth(PrintedHealth[id]);
				card.Dormant = new DormantDefinition(ShrineRules.BaseReviveTurns, ShrineRules.DormantCondition, onAwaken: ShrineRules.ReviveTask);
			} }));
		}
	}
}
