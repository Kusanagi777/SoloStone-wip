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
	// Phase 8, family 21: Tombs of Terror bosses C (ULDA_BOSS_41 to ULDA_BOSS_59 without 44, Trap Room, family 01's). Ten clickable
	// powers, four passive ones (Reliquary's Relics, Desert Warfare, Tol'vir Necromancy, Sandy Surprise), two powers cast by rule
	// (the wiki's "Auto-cast": Maw of the Desert, Arena Expert; family 09's AutoCast) and Lich Baz'hial's power swap inside the
	// match (Lich's Phylactery breaks into Broken Phylactery: the row's script form, a power-only form). The texts are the
	// client's (facts/violet-hold-2026-09.json). A power works on public zones (the boards, the heroes, the graveyards), on the
	// boss's own hand and deck, or reads a hidden zone only as its text says and only in the real game: Spoil the Ending copies a
	// random spell of the run player's hand (the boss learns that card, nothing links the copy to its source) and Arena Expert
	// summons from both decks (a public summon). A power that would do nothing is not offered, by a condition that reads only
	// public facts or the boss's own hand. Every random choice uses the game RNG and is reported as a random event. Jythiros of
	// Questions (58) is rejected (reference/encounters/F21.json). A run shrine is never copied (What's in the Jar?, Sand Shapin')
	// and a destroy aimed at one makes it go dormant (ShrineRules).
	public static partial class DungeonRunEncounters
	{
		/// <summary>
		/// Family 21 (Tombs of Terror bosses C): the constants and rules its scripts share, in one nested class so that no name of it
		/// can meet a name of another family's partial file. Tests read them.
		/// </summary>
		public static class TombsC
		{
			/// <summary>ULDA_BOSS_52p1 Lich's Phylactery.</summary>
			public const string Phylactery = "ULDA_BOSS_52p1";
			/// <summary>ULDA_BOSS_52p2 Broken Phylactery: "You take double damage from fatigue."</summary>
			public const string BrokenPhylactery = "ULDA_BOSS_52p2";
			/// <summary>ULDA_BOSS_42e Sand Rush: "Summon when Drawn" (on Sand Shapin's copy in the boss's deck).</summary>
			public const string SandRush = "ULDA_BOSS_42e";
			/// <summary>ULDA_BOSS_43p Spoil the Ending: how much less the copy costs.</summary>
			public const int SpoilDiscount = 2;
			/// <summary>ULDA_BOSS_49p Demon Emissary: the highest Cost of the Demon it summons.</summary>
			public const int EmissaryMaxCost = 3;
			/// <summary>ULDA_BOSS_59p Food Chain: its damage and the healing when the minion dies.</summary>
			public const int FoodChainDamage = 2, FoodChainHeal = 5;
			/// <summary>ULDA_BOSS_55p Fish Food: the healing each Murloc gets.</summary>
			public const int FishFoodHeal = 2;

			/// <summary>
			/// ULDA_BOSS_53p E.V.I.L. Conscription: the Lackeys (the engine's Lackey pool, LegacyGenerationV1 "Lackeys": Faceless,
			/// Kobold, Witchy, Goblin, Ethereal, Draconic and Titanic Lackey). Ids only: no card is read here.
			/// </summary>
			public static readonly IReadOnlyList<string> Lackeys = new[] { "DAL_613", "DAL_614", "DAL_615", "DAL_739", "DAL_741", "DRG_052", "ULD_616" };

			/// <summary>The Lackeys E.V.I.L. Conscription may add for <paramref name="c"/>: the scripted ones the run's generation filter allows.</summary>
			public static IReadOnlyList<Card> LackeyPool(Game g, Controller c)
			{
				using (g.GenerateFor(c))
					return Lackeys.Select(Cards.FromId).Where(card => card != null && card.Implemented && !(g.FiltersGenerated && g.BlocksGenerated(card))).ToArray();
			}

			/// <summary>ULDA_BOSS_49p Demon Emissary: the Demons it may summon for <paramref name="c"/> (any class, Cost 3 or less).</summary>
			public static IReadOnlyList<Card> EmissaryPool(Game g, Controller c, IEntity source) =>
				ScriptedPool(g, c, source, card => card.Type == CardType.MINION && card.IsRace(Race.DEMON) && card.Cost <= EmissaryMaxCost, anyClass: true);

			/// <summary>ULDA_BOSS_50p Desert Warfare: the Hunter cards it may add for <paramref name="c"/> (no Quest, no Secret).</summary>
			public static IReadOnlyList<Card> WarfarePool(Game g, Controller c, IEntity source) =>
				ScriptedPool(g, c, source, card => card.Class == CardClass.HUNTER && !card.IsQuest && !card.IsSecret, anyClass: true);

			/// <summary>
			/// ULDA_BOSS_46p Maw of the Desert: the minions of either board with the lowest Health (in play, not dying, not a dormant
			/// or permanent minion); the power destroys one of them.
			/// </summary>
			public static List<Minion> Lowest(Game g)
			{
				var all = Touchable(g.Player1).Concat(Touchable(g.Player2)).ToList();
				if (all.Count == 0) return all;
				int low = all.Min(m => m.Health);
				return all.Where(m => m.Health == low).OrderBy(m => m.Id).ToList();
			}

			/// <summary>ULDA_BOSS_55p Fish Food: the Murlocs of either board.</summary>
			public static List<Minion> Murlocs(Game g) => Touchable(g.Player1).Concat(Touchable(g.Player2)).Where(m => m.Card.IsRace(Race.MURLOC)).ToList();

			/// <summary>ULDA_BOSS_52p1 Lich's Phylactery: the Health it restores now (its script number: 20, less 1 per friendly minion that died).</summary>
			public static int PhylacteryHeal(HeroPower power) => power == null ? 0 : Math.Max(0, power[GameTag.TAG_SCRIPT_DATA_NUM_1]);

			/// <summary>The card holds Sand Shapin's "Summon when Drawn" (ULDA_BOSS_42e).</summary>
			public static bool SandShaped(IPlayable p) => p?.AppliedEnchantments?.Any(e => e.Card.Id == SandRush) == true;

			/// <summary>
			/// Draw.PreDrawPhase: the fatigue damage <paramref name="c"/>'s hero takes. Broken Phylactery doubles it while it is the
			/// hero's power (the fatigue count itself rises as usual). One string compare per fatigue hit.
			/// </summary>
			internal static int FatigueDamage(Controller c, int damage) =>
				damage > 0 && c?.Hero?.HeroPower?.Card.Id == BrokenPhylactery ? DamageArithmetic.Add(damage, damage) : damage;

			// A random element of the list by the game RNG (a random event when there is a choice); null for an empty list.
			internal static T Pick<T>(Game g, IReadOnlyList<T> items) where T : class
			{
				if (items.Count == 0) return null;
				if (items.Count == 1) return items[0];
				g.OnRandomHappened(true);
				return items[g.Random.Next(items.Count)];
			}
		}

		private static void F21()
		{
			var anyMinion = new[] { PlayReq.REQ_TARGET_TO_PLAY, PlayReq.REQ_MINION_TARGET };

			// ---------------- Alda Petrik (ULDA_BOSS_41h)
			// Running Wild (2): "Draw a minion from your deck. If it's a Beast, add another copy to your hand." A random minion of
			// the boss's own deck; a Beast gives a second, new copy of its card. Offered while the hand has room and the deck is not
			// empty (both counts are public).
			Power("ULDA_BOSS_41p", null, () => new CustomTask((g, c, s, t, stack) =>
			{
				Minion pick = TombsC.Pick(g, c.DeckZone.OfType<Minion>().ToList());
				if (pick == null) return;
				IPlayable drawn = Generic.Draw(c, pick);
				if (drawn != null && drawn.Card.IsRace(Race.BEAST) && !c.HandZone.IsFull)
					Generic.AddHandPhase.Invoke(c, Entity.FromCard(c, drawn.Card));
			}), usable: c => !c.HandZone.IsFull && !c.DeckZone.IsEmpty);

			// ---------------- Sandshaper Rhogi (ULDA_BOSS_42h)
			// Sand Shapin' (3): "Shuffle a copy of an enemy minion into your deck. Summon it when drawn." A plain copy of the card (a
			// public shuffle: the boss knows what joined its deck) carries Sand Rush (ULDA_BOSS_42e, "Summon when Drawn"). Never a
			// run shrine. Offered while the deck has room.
			Add("ULDA_BOSS_42p", EncounterCardKind.Power, () => new CardDef(Reqs(PlayReq.REQ_TARGET_TO_PLAY, PlayReq.REQ_MINION_TARGET, PlayReq.REQ_ENEMY_TARGET),
				new Power
				{
					PowerTask = new CustomTask((g, c, s, t, stack) =>
					{
						if (!(t is Minion enemy) || c.DeckZone.IsFull || ShrineRules.IsMarked(enemy)) return;
						IPlayable copy = Entity.FromCard(c, enemy.Card);
						if (Generic.ShuffleIntoDeck(c, s, copy, revealGenerated: true))
							Generic.AddEnchantmentBlock(g, Cards.FromId(TombsC.SandRush), (IPlayable)s, copy);
					}),
				}),
				card =>
				{
					card.SourceTargetingPredicate = (source, target) => target is Minion m && !ShrineRules.IsMarked(m);
					card.PlayAvailabilityPredicate += (c, _) => !c.DeckZone.IsFull;
				});
			// Sand Rush: when its card is drawn, the card is summoned from the hand (while the board has room) and its holder draws a
			// card (the Casts When Drawn rule, family 04's UldumRules.SummonDrawn); the mark leaves with the summon.
			Enchantment(TombsC.SandRush, () => new Power
			{
				Trigger = new Trigger(TriggerType.DRAW)
				{
					TriggerSource = TriggerSource.ENCHANTMENT_TARGET,
					SingleTask = new CustomTask((g, c, s, t, stack) =>
					{
						IPlayable drawn = (s as Model.Entities.Enchantment)?.Target as IPlayable ?? t;
						if (drawn == null || drawn.Zone != drawn.Controller.HandZone) return;
						Controller owner = drawn.Controller;
						if (!UldumRules.SummonDrawn(owner, drawn)) return;
						foreach (var mark in drawn.AppliedEnchantments.Where(e => e.Card.Id == TombsC.SandRush).ToArray()) mark.Remove();
						Generic.Draw(owner);
					}),
				},
			});

			// ---------------- Jolene Knottley (ULDA_BOSS_43h)
			// Spoil the Ending (2): "Copy a spell in your opponent's hand. It costs (2) less." A new copy of the card of a random spell
			// in the run player's hand (Mind Vision's rule) goes to the boss's hand, Spoiled (ULDA_BOSS_43e1). Offered while that hand
			// holds a card and the boss's hand has room (counts only): with no spell there it does nothing.
			Power("ULDA_BOSS_43p", null, () => new CustomTask((g, c, s, t, stack) =>
			{
				if (c.HandZone.IsFull) return;
				IPlayable pick = TombsC.Pick(g, c.Opponent.HandZone.Where(p => p is Spell && p.Card.Type == CardType.SPELL).ToList());
				if (pick == null) return;
				IPlayable copy = Entity.FromCard(c, pick.Card);
				Generic.AddHandPhase.Invoke(c, copy);
				if (copy.Zone == c.HandZone) Generic.AddEnchantmentBlock(g, Cards.FromId("ULDA_BOSS_43e1"), (IPlayable)s, copy);
			}), usable: c => !c.HandZone.IsFull && c.Opponent.HandZone.Count > 0);
			Enchantment("ULDA_BOSS_43e1", () => new Power { Enchant = new Enchant(Effects.ReduceCost(TombsC.SpoilDiscount)) });

			// ---------------- Belloc Brightblade (ULDA_BOSS_45h)
			// Reliquary's Relics (passive): "After your opponent casts a Spell, put a copy of it into your hand." A new copy of the
			// cast card (public by its cast), while the boss's hand has room.
			Passive("ULDA_BOSS_45p", () => new Power
			{
				Trigger = new Trigger(TriggerType.AFTER_CAST)
				{
					TriggerSource = TriggerSource.ENEMY,
					SingleTask = new CustomTask((g, c, s, t, stack) =>
					{
						IPlayable cast = t ?? g.CurrentEventData?.EventSource;
						if (!(cast is Spell) || cast.Controller == c || c.HandZone.IsFull || g.State == State.COMPLETE) return;
						Generic.AddHandPhase.Invoke(c, Entity.FromCard(c, cast.Card));
					}),
				},
			});

			// ---------------- Wildtooth (ULDA_BOSS_46h)
			// Maw of the Desert (0, the wiki's Auto-cast; the card carries AI_MUST_PLAY): "Destroy the minion with the lowest Health."
			// Cast by rule at the start of the boss's turn while a minion is on either board: the minion of either board with the
			// lowest Health (a tie by the game RNG) is destroyed.
			AutoCast("ULDA_BOSS_46p", c => TombsC.Lowest(c.Game).Count > 0, (g, c, s, stack) =>
			{
				Minion pick = TombsC.Pick(g, TombsC.Lowest(g));
				if (pick == null) return;
				new DestroyTask(EntityType.TARGET).Process(g, c, s, pick, stack);
				g.DeathProcessingAndAuraUpdate();
			});

			// ---------------- Totally Normal Jar (ULDA_BOSS_47h)
			// What's in the Jar? (2): "Choose a minion. Summon a 1/1 copy of it." A copy of any minion (Generic.Copy: its state and
			// enchantments, then ULDA_BOSS_47e sets 1/1), for the boss. Never a run shrine. Needs a free slot.
			Add("ULDA_BOSS_47p", EncounterCardKind.Power, () => new CardDef(Reqs(PlayReq.REQ_TARGET_TO_PLAY, PlayReq.REQ_MINION_TARGET, PlayReq.REQ_NUM_MINION_SLOTS),
				new Power
				{
					PowerTask = new CustomTask((g, c, s, t, stack) =>
					{
						if (!(t is Minion chosen) || c.BoardZone.IsFull || ShrineRules.IsMarked(chosen)) return;
						if (Generic.Copy(c, s, chosen, Zone.PLAY) is Minion copy && copy.Zone == c.BoardZone && !copy.ToBeDestroyed)
							Generic.AddEnchantmentBlock(g, Cards.FromId("ULDA_BOSS_47e"), (IPlayable)s, copy);
					}),
				}),
				card => card.SourceTargetingPredicate = (source, target) => target is Minion m && !ShrineRules.IsMarked(m));
			Enchantment("ULDA_BOSS_47e", () => new Power { Enchant = new Enchant(Effects.SetAttackHealth(1)) });

			// ---------------- Ilza Grimdrum (ULDA_BOSS_48h)
			// Arena Expert (0, the wiki's Auto-cast): "Summon a minion from each player's deck. They fight." Cast by rule at the start
			// of the boss's turn while its board has room: Duel!'s rule (DAL_731, family 02's DungeonRunEncounters.Duel).
			AutoCast("ULDA_BOSS_48p", c => !c.BoardZone.IsFull, (g, c, s, stack) =>
			{
				Duel.Process(g, c, s, null, stack);
				g.DeathProcessingAndAuraUpdate();
			});

			// ---------------- Craz (ULDA_BOSS_49h)
			// Demon Emissary (2): "Summon a random Demon that costs (3) or less." Any class, scripted, behind the generation filter.
			Power("ULDA_BOSS_49p", new[] { PlayReq.REQ_NUM_MINION_SLOTS }, () => new CustomTask((g, c, s, t, stack) =>
			{
				if (c.BoardZone.IsFull) return;
				Card pick = TombsC.Pick(g, TombsC.EmissaryPool(g, c, s));
				if (pick != null) Generic.SummonBlock.Invoke(g, (Minion)Entity.FromCard(c, pick), -1, s);
			}));

			// ---------------- Ol' Barkeye (ULDA_BOSS_50h)
			// Desert Warfare (passive): "After your hero attacks, add a random Hunter card to your hand." Any scripted Hunter card the
			// generation filter allows (no Quest, no Secret), while the hand has room.
			Passive("ULDA_BOSS_50p", () => new Power
			{
				Trigger = new Trigger(TriggerType.AFTER_ATTACK)
				{
					TriggerSource = TriggerSource.HERO,
					SingleTask = new CustomTask((g, c, s, t, stack) =>
					{
						if (c.HandZone.IsFull || g.State == State.COMPLETE) return;
						Card pick = TombsC.Pick(g, TombsC.WarfarePool(g, c, s));
						if (pick != null) Generic.AddHandPhase.Invoke(c, Entity.FromCard(c, pick));
					}),
				},
			});

			// ---------------- Kasmut (ULDA_BOSS_51h)
			// Tol'vir Necromancy (passive): "After a minion is Reborn, restore its Health to full." A minion of either player that
			// returned by Reborn (the engine's return: HAS_BEEN_REBORN, created by its dead self) is healed to full by the power.
			Passive("ULDA_BOSS_51p", () => new Power
			{
				Trigger = new Trigger(TriggerType.AFTER_SUMMON)
				{
					TriggerSource = TriggerSource.ALL_MINIONS,
					Condition = new SelfCondition(p => p is Minion m && m[GameTag.HAS_BEEN_REBORN] == 1 && m.Damage > 0
						&& p.Game.IdEntityDic.TryGetValue(m[GameTag.DISPLAYED_CREATOR], out IPlayable dead) && dead is Minion && dead.Card.Id == m.Card.Id
						&& dead.Zone?.Type == Zone.GRAVEYARD),
					SingleTask = new CustomTask((g, c, s, t, stack) =>
					{
						if (t is Minion back && back.Zone?.Type == Zone.PLAY && !back.ToBeDestroyed && back.Damage > 0)
							HealingRules.Heal((IPlayable)s, new ICharacter[] { back }, 0, full: true);
					}),
				},
			});

			// ---------------- Lich Baz'hial (ULDA_BOSS_52h)
			// Lich's Phylactery (0): "Restore @ Health to your Hero. Reduce by 1 for every friendly minion that dies." @ is the power's
			// script number (20 on the card); a DEATH trigger lowers it (never below 0). Used once, it breaks: the boss enters its
			// script form 1, Broken Phylactery (EncounterRules.SwapPowerForm, family 14's power-only form; the wave-6 merge folded family 21's copy into it). Offered while the boss's hero is damaged.
			Add(TombsC.Phylactery, EncounterCardKind.Power, () => new CardDef(new Power
			{
				PowerTask = new CustomTask((g, c, s, t, stack) =>
				{
					int heal = TombsC.PhylacteryHeal(s as HeroPower);
					if (heal > 0) HealingRules.Heal((IPlayable)s, new ICharacter[] { c.Hero }, heal);
					if (g.State != State.COMPLETE && c.Hero.HeroPower == s) EncounterRules.SwapPowerForm(c, 1);
				}),
				Trigger = new Trigger(TriggerType.DEATH)
				{
					TriggerSource = TriggerSource.FRIENDLY,
					Condition = new SelfCondition(p => p is Minion),
					SingleTask = new CustomTask((g, c, s, t, stack) =>
					{
						if (s is HeroPower power && power.Controller.Hero?.HeroPower == power) power[GameTag.TAG_SCRIPT_DATA_NUM_1] = Math.Max(0, power[GameTag.TAG_SCRIPT_DATA_NUM_1] - 1);
					}),
				},
			}), card => card.PlayAvailabilityPredicate += (c, _) => c.Hero.Damage > 0, activate: true);
			// Broken Phylactery (passive): "You take double damage from fatigue." Read by Draw.PreDrawPhase (TombsC.FatigueDamage).
			Passive(TombsC.BrokenPhylactery, () => new Power());

			// ---------------- Taskmaster Maru (ULDA_BOSS_53h)
			// E.V.I.L. Conscription (1): "Add a random Lackey to your hand." Offered while the hand has room.
			Power("ULDA_BOSS_53p", null, () => new CustomTask((g, c, s, t, stack) =>
			{
				if (c.HandZone.IsFull) return;
				Card pick = TombsC.Pick(g, TombsC.LackeyPool(g, c));
				if (pick != null) Generic.AddHandPhase.Invoke(c, Entity.FromCard(c, pick));
			}), usable: c => !c.HandZone.IsFull);

			// ---------------- Janak (ULDA_BOSS_54h)
			// Cavalry of E.V.I.L. (1): "Deal 1 damage to a minion and give it Charge." Any minion; Charge (ULDA_BOSS_54e) if it lives.
			Power("ULDA_BOSS_54p", anyMinion, () => new CustomTask((g, c, s, t, stack) =>
			{
				if (!(t is Minion m)) return;
				new DamageTask(1, EntityType.TARGET).Process(g, c, s, m, stack);
				if (m.Zone?.Type == Zone.PLAY && !m.ToBeDestroyed && m.Health > 0)
					Generic.AddEnchantmentBlock(g, Cards.FromId("ULDA_BOSS_54e"), (IPlayable)s, m);
			}));
			Enchantment("ULDA_BOSS_54e", () => new Power { Enchant = new Enchant(Effects.Charge) });

			// ---------------- Water-Toting Murlocs (ULDA_BOSS_55h)
			// Fish Food (1): "Restore 2 Health to all Murlocs." Every Murloc of either board. Offered while one is on a board.
			Power("ULDA_BOSS_55p", null, () => new CustomTask((g, c, s, t, stack) =>
				HealingRules.Heal((IPlayable)s, TombsC.Murlocs(g).Cast<ICharacter>(), TombsC.FishFoodHeal)), usable: c => TombsC.Murlocs(c.Game).Count > 0);

			// ---------------- Sand Puffer Frog (ULDA_BOSS_56h)
			// Sandy Surprise (passive): "After you play a 1-Cost minion, give it Stealth." A minion whose card costs (1)
			// (ULDA_BOSS_56e).
			Passive("ULDA_BOSS_56p", () => new Power
			{
				Trigger = new Trigger(TriggerType.AFTER_PLAY_MINION)
				{
					TriggerSource = TriggerSource.FRIENDLY,
					Condition = new SelfCondition(p => p.Card.Cost == 1),
					SingleTask = new CustomTask((g, c, s, t, stack) =>
					{
						if (t is Minion played && played.Zone?.Type == Zone.PLAY && !played.ToBeDestroyed)
							Generic.AddEnchantmentBlock(g, Cards.FromId("ULDA_BOSS_56e"), (IPlayable)s, played);
					}),
				},
			});
			Enchantment("ULDA_BOSS_56e", () => new Power { Enchant = new Enchant(Effects.StealthEff) });

			// ---------------- Winged Guardian (ULDA_BOSS_57h)
			// Titan's Beast (2): "Set a minion's Attack and Health to 3." Any minion (ULDA_BOSS_57e).
			Power("ULDA_BOSS_57p", anyMinion, () => new AddEnchantmentTask("ULDA_BOSS_57e", EntityType.TARGET));
			Enchantment("ULDA_BOSS_57e", () => new Power { Enchant = new Enchant(Effects.SetAttackHealth(3)) });

			// ---------------- Dire Bat (ULDA_BOSS_59h)
			// Food Chain (2): "Deal 2 damage to a minion. If it dies, restore 5 Health to your Hero."
			Power("ULDA_BOSS_59p", anyMinion, () => new CustomTask((g, c, s, t, stack) =>
			{
				if (!(t is Minion m)) return;
				new DamageTask(TombsC.FoodChainDamage, EntityType.TARGET).Process(g, c, s, m, stack);
				if (m.ToBeDestroyed || m.Health <= 0) HealingRules.Heal((IPlayable)s, new ICharacter[] { c.Hero }, TombsC.FoodChainHeal);
			}));
		}
	}
}
