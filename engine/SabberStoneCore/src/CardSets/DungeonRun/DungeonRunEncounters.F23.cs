using System.Linq;
using SabberStoneCore.Actions;
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
	// Phase 8, family 23: the Monster Hunt nemeses (The Witchwood's four Final Nemesis fights) and the Witchwood's Touch boss
	// spells. Infinite Toki (Chronoacceleration), Glinda Crowskin (False Heart; Crowskin Faithful, Crowskin Pact), Lord Godfrey
	// (Pistol Barrage) and Captain Shivers (Plunder; Cursed Crewmember). Hagatha the Witch, the family's other major, was
	// scripted by family 01 (DungeonRunEncounters.F01.cs: Ritual Dagger, Ominous Fog, the hunters' hero cards); Alternate
	// Timeline (GILA_BOSS_48t, a game restart) is scripted by step 9 (lane X: the restart family, EncounterRules.S9.cs) and
	// stays out of Infinite Toki's deck until the step-9 stage-3 gate (real games) admits it. The texts are the
	// client's (facts/violet-hold-2026-09.json). Every power works on public zones or on the boss's own hand and deck; every
	// random choice uses the game RNG and is reported as a random event; no new private tag.
	public static partial class DungeonRunEncounters
	{
		/// <summary>GILA_BOSS_49t2 Crowskin Pact: the minion it transforms a minion into.</summary>
		public const string CrowskinFaithful = "GILA_BOSS_49t";

		/// <summary>
		/// GILA_BOSS_49t2 Crowskin Pact, "Transform a minion into a 'Crowskin Faithful' and take control of it." The minion is
		/// transformed where it stands (Actions.Generic's transform rule: a run shrine is destroyed instead and goes dormant, and
		/// nothing more happens); a Faithful on the other side then changes controller (the engine's control rule: with the
		/// caster's board full it is destroyed). Returns the Faithful, or null when nothing was transformed.
		/// </summary>
		public static Minion CrowskinPact(Controller c, IEntity source, Minion victim)
		{
			if (victim == null || victim.Zone?.Type != Zone.PLAY) return null;
			Game g = c.Game;
			Controller owner = victim.Controller;
			int position = victim.ZonePosition;
			if (!Generic.TransformBlock.Invoke(owner, Cards.FromId(CrowskinFaithful), victim)) return null;
			if (position < 0 || position >= owner.BoardZone.Count || !(owner.BoardZone[position] is Minion faithful)) return null;
			if (owner != c && ShrineRules.CanChangeController(faithful))
				new ControlTask(EntityType.TARGET).Process(g, c, source, faithful, null);
			return faithful;
		}

		/// <summary>
		/// GILA_BOSS_60p Plunder, "Draw a weapon from your deck": one weapon of the owner's deck, by the game RNG (reported as a
		/// random event when there is a choice), drawn as an ordinary draw. Returns it, or null when the deck holds none.
		/// </summary>
		public static IPlayable Plunder(Controller c)
		{
			var weapons = c.DeckZone.Where(p => p is Weapon).ToList();
			if (weapons.Count == 0) return null;
			IPlayable pick = weapons[0];
			if (weapons.Count > 1)
			{
				pick = weapons[c.Game.Random.Next(weapons.Count)];
				c.Game.OnRandomHappened(true);
			}
			Generic.Draw(c, pick);
			return pick;
		}

		/// <summary>GILA_BOSS_99t / 99t2 / 99t3 Witchwood's Touch: "Draw N cards. Gain M Armor."</summary>
		private static CardDef WitchwoodsTouch(int draw, int armor) =>
			new CardDef(new Power { PowerTask = ComplexTask.Create(new DrawTask(draw), new ArmorTask(armor)) });

		private static void F23()
		{
			var aMinion = new[] { PlayReq.REQ_TARGET_TO_PLAY, PlayReq.REQ_MINION_TARGET };
			var aFriendlyMinion = new[] { PlayReq.REQ_TARGET_TO_PLAY, PlayReq.REQ_MINION_TARGET, PlayReq.REQ_FRIENDLY_TARGET };

			// ---------------- Infinite Toki (GILA_BOSS_48h, 60 Health)
			// Chronoacceleration (2): "Give a friendly minion Mega-Windfury. At the end of your turn, it dies." The client's own
			// enchantment, Accelerated ("Mega-Windfury. Will die at the end of this turn."): Mega-Windfury (four attacks a turn; a
			// minion that attacked already this turn may attack again), and the minion is destroyed when the turn ends (Power
			// Overwhelming's rule). A minion that cannot attack yet (just played, no Charge or Rush) gains nothing it can use.
			Power("GILA_BOSS_48p", aFriendlyMinion, () => new AddEnchantmentTask("GILA_BOSS_48e", EntityType.TARGET));
			Enchantment("GILA_BOSS_48e", () => new Power
			{
				Enchant = new Enchant(new Effect(GameTag.MEGA_WINDFURY, EffectOperator.SET, 1)),
				Trigger = new Trigger(TriggerType.TURN_END) { EitherTurn = true, SingleTask = new DestroyTask(EntityType.TARGET) },
			});
			// Alternate Timeline (10): "Start a new game, but your opponent's Health stays the same." Step 9 (lane X, D13): its cast
			// ends the fight as a restart (EncounterRules.RestartGame); the run layer restages the floor with the run player's
			// damage carried (DungeonRunState.RestartCarriedDamage). Playable only while the match lists it (RunMatchPlan.Restarts:
			// a tuned run, once per floor). Not in any boss deck and not admitted until the stage-3 gate passes.
			BossCard(RunMatchEnds.AlternateTimeline,
				() => new CardDef(new Power { PowerTask = new CustomTask((g, c, s, t, stack) => EncounterRules.RestartGame(c)) }),
				card => card.PlayAvailabilityPredicate += (c, _) => EncounterRules.RestartAllowed(c.Game, RunMatchEnds.AlternateTimeline));

			// ---------------- Glinda Crowskin (GILA_BOSS_49h, 50 Health)
			// False Heart: "Passive Hero Power. While you control a minion, your hero is Immune." Family 06's hero aura
			// (EncounterRules.HeroImmuneWhile): re-checked at every aura update; a dormant or permanent minion does not count.
			Passive("GILA_BOSS_49p", () => new Power { Aura = EncounterRules.HeroImmuneWhile() });
			// Crowskin Faithful (3 Mana 6/3): "Deathrattle: Draw a card. Shuffle this minion into your deck." Its controller draws,
			// then a new Crowskin Faithful (the card as printed) is shuffled into that controller's deck.
			BossCard(CrowskinFaithful, () => new CardDef(new Power
			{
				DeathrattleTask = ComplexTask.Create(
					new DrawTask(),
					new CustomTask((g, c, s, t, stack) => Generic.ShuffleIntoDeck(c, s, Entity.FromCard(c, Cards.FromId(CrowskinFaithful))))),
			}));
			// Crowskin Pact (5): "Transform a minion into a 'Crowskin Faithful' and take control of it." Any minion (CrowskinPact).
			BossCard("GILA_BOSS_49t2", () => new CardDef(Reqs(aMinion), new Power
			{
				PowerTask = new CustomTask((g, c, s, t, stack) => CrowskinPact(c, s, t as Minion)),
			}));

			// ---------------- Lord Godfrey (GILA_BOSS_59h, 60 Health)
			// Pistol Barrage (1): "Deal 2 damage to a minion and the minions next to it." A hero power: no Spell Damage; the three
			// hits are one damage event (Cone of Cold's rule).
			Power("GILA_BOSS_59p", aMinion, () => ComplexTask.Create(
				new IncludeAdjacentTask(EntityType.TARGET, true),
				new DamageTask(2, EntityType.STACK)));

			// ---------------- Captain Shivers (GILA_BOSS_60h, 75 Health; he starts with Kingsbane equipped: the row's start block)
			// Plunder (0): "Draw a weapon from your deck." Auto-cast (the client card carries AI_MUST_PLAY; the wiki marks it so):
			// used by rule at the start of his turn while his deck holds a weapon and his hand has room (family 09's AutoCast;
			// never offered as a click). In practice it draws Kingsbane back after its Deathrattle shuffled it in.
			AutoCast("GILA_BOSS_60p", c => !c.HandZone.IsFull && c.DeckZone.Any(p => p is Weapon), (g, c, s, stack) => Plunder(c));
			// Cursed Crewmember (2 Mana 2/1 Pirate): "Battlecry: Draw 2 cards, then shuffle a copy of your hand into your deck."
			// Dead Man's Hand's rule for the copies (a copy of each card in hand, the cards just drawn included).
			BossCard("GILA_BOSS_60t", () => new CardDef(new Power
			{
				PowerTask = ComplexTask.Create(
					new DrawTask(2),
					new IncludeTask(EntityType.HAND),
					new CopyTask(EntityType.STACK, Zone.DECK)),
			}));

			// ---------------- Witchwood's Touch (the Monster Hunt bosses' armor spells, 2 Mana each)
			// (1) "Draw a card. Gain 6 Armor." (the wiki: unused in any deck); (2) "Draw 2 cards. Gain 8 Armor." (nine Monster Hunt
			// bosses, families 10-12); (3) "Draw 3 cards. Gain 10 Armor." (Glinda Crowskin, Infinite Toki).
			BossCard("GILA_BOSS_99t", () => WitchwoodsTouch(1, 6));
			BossCard("GILA_BOSS_99t2", () => WitchwoodsTouch(2, 8));
			BossCard("GILA_BOSS_99t3", () => WitchwoodsTouch(3, 10));
		}
	}
}
