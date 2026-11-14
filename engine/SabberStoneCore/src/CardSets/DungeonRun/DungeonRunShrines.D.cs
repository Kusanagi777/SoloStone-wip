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

namespace SabberStoneCore.CardSets.DungeonRun
{
	/// <summary>
	/// Phase 7 D: the nine Rumble Run start-of-game companions (one per class, the sixth teammate of each class's teammate row)
	/// and TRLA_809 Windfall Blessing, the last shrine upgrade. Each script sits on the client card itself.
	/// A companion is a "Start of Game: Draw this." teammate: it is declared through StartOfGameRules as a self-drawing source
	/// that is never dealt in an opening hand (DungeonRunContent.DeclareSogDraw, as DALA_707 and ULDA_407), so after both
	/// mulligans it is drawn from its owner's deck in a public trigger block, after the board shrine lands
	/// (ShrineRules.SetupPriority) and after the Start of Game treasures (priority 0): <see cref="CompanionDrawPriority"/>.
	/// TRLA_809's grant is the client's TRLA_809e2 Windfall Glyph on every run shrine of its owner (DungeonRunShrines.Grant):
	/// its Deathrattle gives each minion in its owner's in-game deck the client's TRLA_809e3 (+1/+1). The run deck records are
	/// never changed. Every random choice uses the game RNG; every piece of state is an entity tag.
	/// </summary>
	public static partial class DungeonRunShrines
	{
		/// <summary>The nine start-of-game companions, in id order (Druid, Mage, Paladin, Priest, Shaman, Hunter, Warrior, Warlock, Rogue).</summary>
		public static readonly IReadOnlyList<string> CompanionIds = new[]
		{
			"TRLA_121", "TRLA_135", "TRLA_139", "TRLA_148", "TRLA_156", "TRLA_164", "TRLA_172", "TRLA_180", "TRLA_188",
		};

		/// <summary>
		/// The companions' Start of Game priority: after the board shrine (<see cref="ShrineRules.SetupPriority"/>, -100) and after
		/// every Start of Game treasure (0: DALA_743 Dr. Boom's Remote's Boom Bots, the self-drawing treasures), lowest first.
		/// </summary>
		public const int CompanionDrawPriority = 10;

		/// <summary>TRLA_809 Windfall Blessing: "Your Shrines have 'Deathrattle: Give minions in your deck +1/+1.'"</summary>
		public const string WindfallBlessing = "TRLA_809";
		/// <summary>TRLA_809e3: the +1/+1 each minion in the owner's deck gets from TRLA_809e2's Deathrattle.</summary>
		public const string WindfallBuff = "TRLA_809e3";
		/// <summary>TRLA_121 Water Spirit: the full Mana Crystals its Deathrattle gives (10 caps, as Nourish's).</summary>
		public const int WaterSpiritCrystals = 2;
		/// <summary>TRLA_139 Parading Marshal's weapon: CS2_097 Truesilver Champion.</summary>
		public const string MarshalWeapon = "CS2_097";

		/// <summary>Whether <paramref name="cardId"/> is one of the nine start-of-game companions.</summary>
		public static bool IsCompanion(string cardId) => cardId != null && CompanionIds.Contains(cardId);

		/// <summary>
		/// TRLA_135 Fan of Flames: "Upgrade your Hero Power." The owner's hero power, when it is a basic power with an upgraded form
		/// (DungeonRunContent.HeroPowerUpgrades, the table LOOTA_802 Justicar's Ring and Genn Greymane use), is replaced by that
		/// form (a fresh, ready power) and re-enrolled in the owner's hero-power auras (TRLA_128 Jan'alai's Flame's (0)); an
		/// upgraded or non-basic power is kept.
		/// </summary>
		internal static void UpgradeHeroPower(Game g, Controller c, IEntity source)
		{
			HeroPower power = c.Hero.HeroPower;
			if (power == null || g.State == State.COMPLETE || !DungeonRunContent.HeroPowerUpgrades.TryGetValue(power.Card.Id, out string upgraded)) return;
			new ReplaceHeroPower(Cards.FromId(upgraded)).Process(g, c, source, null);
			BasicHeroPowerRules.EnrollPowerAuras(c, c.Hero.HeroPower);
		}

		// A companion: its script and its Start of Game draw (never dealt in an opening hand).
		private static CardDef Companion(Power power, Action<Card> more = null) => Implemented(new CardDef(power) { Configure = card =>
		{
			more?.Invoke(card);
			DungeonRunContent.DeclareSogDraw(card, CompanionDrawPriority);
		} });

		private static void AddD(Dictionary<string, CardDef> cards)
		{
			// ---------------- the companions ("Start of Game: Draw this.")
			// TRLA_121 Water Spirit (Druid): "Deathrattle: Gain 2 Mana Crystals." (full ones, Nourish's rule; 10 caps).
			cards.Add("TRLA_121", Companion(new Power { DeathrattleTask = new ManaCrystalFullTask(WaterSpiritCrystals) }));
			// TRLA_164 Shadowmaw Panther (Hunter): "Your Beasts have Rush." A board aura of the client's TRLA_164e "Has Rush." on the
			// owner's Beasts, the Panther included (the text has no "other"); a dormant form is never a target of an aura.
			cards.Add("TRLA_164", Companion(new Power
			{
				Aura = new Aura(AuraType.BOARD, "TRLA_164e") { Condition = SelfCondition.IsRace(Race.BEAST) },
			}));
			cards.Add("TRLA_164e", Implemented(new CardDef(new Power { Enchant = new Enchant(Effects.Rush) })));
			// TRLA_135 Fan of Flames (Mage): "Battlecry: Upgrade your Hero Power."
			cards.Add("TRLA_135", Companion(new Power
			{
				PowerTask = new CustomTask((g, c, s, t, stack) => UpgradeHeroPower(g, c, s)),
			}));
			// TRLA_139 Parading Marshal (Paladin): "Battlecry: Equip a Truesilver Champion."
			cards.Add("TRLA_139", Companion(new Power { PowerTask = new WeaponTask(MarshalWeapon) }));
			// TRLA_148 Weaponized Zombie (Priest): "Deathrattle: Give a random friendly minion +1/+1." (the client's TRLA_148e; a
			// minion in play, the run shrine included; a dormant form is untouchable and never picked; none, nothing).
			cards.Add("TRLA_148", Companion(new Power
			{
				DeathrattleTask = ComplexTask.Create(
					new IncludeTask(EntityType.MINIONS),
					new RandomTask(1, EntityType.STACK),
					new AddEnchantmentTask("TRLA_148e", EntityType.STACK)),
			}));
			cards.Add("TRLA_148e", Implemented(new CardDef(new Power { Enchant = new Enchant(Effects.AttackHealth_N(1)) })));
			// TRLA_188 Pesky Rascal (Rogue): "Immune." The client data names the keyword only in its text and refs, and the engine
			// reads Immune from auras and effects, not from a card tag, so the Rascal's Immune is a self aura (Survivalist's): it
			// cannot be damaged, attacked or targeted by the enemy; Silence removes it; a destroy effect still kills it.
			cards.Add("TRLA_188", Companion(new Power { Aura = new Aura(AuraType.SELF, new Effect(GameTag.IMMUNE, EffectOperator.ADD, 1)) }));
			// TRLA_156 Deepsea Diver (Shaman): "Battlecry: Unlock your Overloaded Mana Crystals." (Lava Shock's rule: the locked
			// crystals and the Overload owed for next turn are both cleared).
			cards.Add("TRLA_156", Companion(new Power
			{
				PowerTask = ComplexTask.Create(
					new SetControllerGameTagTask(GameTag.OVERLOAD_LOCKED, 0),
					new SetControllerGameTagTask(GameTag.OVERLOAD_OWED, 0)),
			}));
			// TRLA_180 Leering Bat (Warlock): "Your hero is Immune on your turn." A restless hero aura of the client's TRLA_180e
			// (Violet Illusionist's rule): the hero is Immune while its owner is the current player.
			cards.Add("TRLA_180", Companion(new Power
			{
				Aura = new Aura(AuraType.HERO, "TRLA_180e") { Condition = SelfCondition.IsMyTurn, Restless = true },
			}));
			cards.Add("TRLA_180e", Implemented(new CardDef(new Power { Enchant = new Enchant(Effects.Immune) })));
			// TRLA_172 Rallying Quilboar (Warrior): "Adjacent minions have Taunt." (Dire Wolf Alpha's adjacency aura of the client's
			// TRLA_172e; a dormant shrine form beside it is untouchable and gets nothing).
			cards.Add("TRLA_172", Companion(new Power { Aura = new AdjacentAura("TRLA_172e") }));
			cards.Add("TRLA_172e", Implemented(new CardDef(new Power { Enchant = new Enchant(Effects.TauntEff) })));

			// ---------------- TRLA_809 Windfall Blessing (a shrine upgrade: ScriptedUpgrades, Grants)
			// TRLA_809e: the client's "Windfall Blessing Player Enchant", a record of the upgrade on its owner.
			cards.Add("TRLA_809e", Implemented(new CardDef(new Power())));
			// TRLA_809e2 Windfall Glyph: "Deathrattle: Give all minions in your deck +1/+1." It fires as the shrine goes dormant
			// (ShrineRules), as the other granted Deathrattles, and the Deathrattle doublers repeat it. Each minion in the owner's
			// in-game deck gets TRLA_809e3 (+1/+1); the run's deck records are not changed. A copied grant (a plain copy of a shrine)
			// does nothing.
			cards.Add("TRLA_809e2", Implemented(new CardDef(new Power
			{
				DeathrattleTask = new CustomTask((g, c, s, t, stack) =>
				{
					if (!ShrineRules.IsMarked(s) || !(s is IPlayable shrine) || g.State == State.COMPLETE) return;
					Card buff = Cards.FromId(WindfallBuff);
					foreach (IPlayable card in c.DeckZone.ToArray())
						if (card is Minion) Generic.AddEnchantmentBlock(g, buff, shrine, card);
				}),
			})));
			cards.Add(WindfallBuff, Implemented(new CardDef(new Power { Enchant = new Enchant(Effects.AttackHealth_N(1)) })));
		}
	}
}
