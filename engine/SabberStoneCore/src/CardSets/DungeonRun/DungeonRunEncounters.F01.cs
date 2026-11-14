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
	// Family 01, the encounter foundation: one encounter per mechanism of the model, so each mechanism is played in a real
	// engine game. Icarax (three Health forms, persistent boss damage), Hagatha (the run player's fight-only hero cards),
	// Whirt (the boss's generated Secrets), War Master Voone (a boss board shrine; his power and shrines are scripted already)
	// and three symmetric Dalaran Heist anomalies. The texts are the client's (facts/violet-hold-2026-09.json).
	public static partial class DungeonRunEncounters
	{
		/// <summary>ULDA_BOSS_40p2 Wrath Smash: the damaged minions of both boards (each adds (1) to its Cost).</summary>
		public static int DamagedMinions(Game game) =>
			game.Player1.BoardZone.Count(m => m.Damage > 0) + game.Player2.BoardZone.Count(m => m.Damage > 0);

		/// <summary>
		/// A card that can be played again and again in one turn: the Echo keyword, or "Repeatable this turn" (tag 1114:
		/// Unstable Evolution). A global cost cut never brings one below (1).
		/// </summary>
		public static bool Repeatable(IPlayable p) => p.IsEcho || p.Card[(GameTag)1114] != 0;

		private const string Counterspell = "EX1_287";

		/// <summary>
		/// Puts a new copy of the Secret <paramref name="card"/> into <paramref name="c"/>'s Secret zone as a Secret enters play
		/// when cast (its trigger started, exhausted until its owner's turn ends), without casting it. Nothing when the zone is
		/// full or holds that Secret already.
		/// </summary>
		public static void PutSecret(Controller c, Card card)
		{
			if (c.SecretZone.IsFull || c.SecretZone.Any(secret => secret.Card.AssetId == card.AssetId)) return;
			var secret = (Spell)Entity.FromCard(c, card);
			secret.Power?.Trigger?.Activate(secret);
			c.SecretZone.Add(secret);
			secret.IsExhausted = true;
		}

		/// <summary>
		/// The Secrets of <paramref name="c"/>'s deck (and hand, with <paramref name="hand"/>) a power can put into play now: none
		/// when the Secret zone is full, never one whose Secret is in play already; hand first, then deck, in zone order.
		/// </summary>
		public static List<Spell> SecretsToPlay(Controller c, bool hand)
		{
			var found = new List<Spell>();
			if (c.SecretZone.IsFull) return found;
			bool Fits(IPlayable p) => p is Spell spell && spell.Card.IsSecret && !c.SecretZone.Any(secret => secret.Card.AssetId == p.Card.AssetId);
			if (hand) found.AddRange(c.HandZone.Where(Fits).Cast<Spell>());
			found.AddRange(c.DeckZone.Where(Fits).Cast<Spell>());
			return found;
		}

		private static void F01()
		{
			// ---------------- Icarax, Plague Lord of Wrath (ULDA_BOSS_40h / h2 / h3: 300, 200, 100 Health)
			// Commanding Scream: "Your minions can't be reduced below 1 Health this turn." The rule is Commanding Shout's
			// (NEW1_036e2: a one-turn board aura of NEW1_036e), so the shown enchantment is Commanding Shout's.
			Power("ULDA_BOSS_40p1", null, () => new AddEnchantmentTask("NEW1_036e2", EntityType.CONTROLLER));
			// Wrath Smash: "Destroy a damaged minion. Costs (1) more for each damaged minion."
			Power("ULDA_BOSS_40p2", new[] { PlayReq.REQ_TARGET_TO_PLAY, PlayReq.REQ_MINION_TARGET, PlayReq.REQ_DAMAGED_TARGET },
				() => new DestroyTask(EntityType.TARGET),
				DamagedMinions);
			// Hand of Defiance: "Swap the Attack and Health of a minion." (Crazed Alchemist's rule, its own enchantment)
			Power("ULDA_BOSS_40p3", new[] { PlayReq.REQ_TARGET_TO_PLAY, PlayReq.REQ_MINION_TARGET },
				() => new SwapAttackHealthTask(EntityType.TARGET, "ULDA_BOSS_40p3e"));
			Enchantment("ULDA_BOSS_40p3e", () => new Power { Enchant = Enchants.Enchants.SetAttackHealthScriptTag });

			// Looming Presence (the Plague Lords' phase decks list the Heroic card): "Draw 3 cards. Gain 6 Armor." / "Draw 2 cards. Gain 4 Armor."
			BossCard("LOEA_01H", () => new CardDef(new Power { PowerTask = ComplexTask.Create(new DrawTask(3), new ArmorTask(6)) }));
			BossCard("LOEA_01", () => new CardDef(new Power { PowerTask = ComplexTask.Create(new DrawTask(2), new ArmorTask(4)) }));

			// ---------------- Whirt the All-Knowing (DALA_BOSS_21h)
			// Prediction: "Put a random class Secret into the battlefield." The boss's class Secrets this game may generate
			// (DungeonRunContent.RandomSecretCandidates: the encounter row turns the boss side's generated Secrets on), one the
			// boss does not hold yet; nothing when its Secret zone is full or none is left.
			Power("DALA_BOSS_21p", null, () => new CustomTask((g, c, s, t, stack) =>
			{
				DungeonRunContent.PlayRandomSecrets(c, 1);
				g.OnRandomHappened(true);
			}));

			// ---------------- The four Secret bosses (Dungeon Run, Monster Hunt, Tombs of Terror)
			// Elder Brandlemar (LOOTA_BOSS_35h). Dampen Magic: "Put a 'Counterspell' Secret into the battlefield." Put, not cast (no
			// "cast a spell" trigger): one fixed, known Secret, never a second copy (Hearthstone holds one Secret of a name), so
			// the power is not offered while Counterspell is in play or the Secret zone is full.
			Power("LOOTA_BOSS_35p", null, () => new CustomTask((g, c, s, t, stack) => PutSecret(c, Cards.FromId(Counterspell))),
				usable: c => !c.SecretZone.IsFull && !c.SecretZone.Any(secret => secret.Card.Id == Counterspell));
			// Trapped Room (LOOTA_BOSS_48h). Alarm: "Passive Hero Power. Whenever you reveal a Secret, summon a 3/3 Sawblade."
			// A revealed Secret is one that triggered (ComplexTask.Secret marks it REVEALED as it leaves play); one destroyed
			// unrevealed (Flare, Eater of Secrets) summons nothing. Nothing when the boss's board is full.
			Passive("LOOTA_BOSS_48p", () => new Power
			{
				Trigger = new Trigger(TriggerType.SECRET_REVEALED) { TriggerSource = TriggerSource.FRIENDLY, SingleTask = new SummonTask("LOOTA_BOSS_48t") },
			});
			Token("LOOTA_BOSS_48t");
			// Manhunter Ivan (GILA_BOSS_25h). Trap Preparation: "Put a random Secret from your deck into the battlefield." Mad
			// Scientist's rule (ComplexTask.PutSecretFromDeck): a Secret of the deck that is not in play yet, by the game RNG; with
			// none it does nothing, as the original. Offered whenever the Secret zone has room: the offer must not depend on the
			// deck, which the boss's search only samples (an option legal in the sampled deck but not in the real one cannot be
			// taken), so whether a Secret is left is the search's estimate, not a rule.
			Power("GILA_BOSS_25p", null, () => ComplexTask.PutSecretFromDeck, usable: c => !c.SecretZone.IsFull);
			// Trap Room (ULDA_BOSS_44h). Tricks and Traps: "Play a random Secret from your hand or deck." One Secret of the hand or
			// deck that is not in play yet, by the game RNG, played by the effect: cast without payment (the engine's rule for an
			// effect play, Actions.AutoPlayRules), so it enters the Secret zone as a cast Secret does. With none it does nothing;
			// offered whenever the Secret zone has room (as Trap Preparation).
			Power("ULDA_BOSS_44p", null, () => new CustomTask((g, c, s, t, stack) =>
			{
				var secrets = SecretsToPlay(c, true);
				if (secrets.Count == 0) return;
				Spell pick = secrets[g.Random.Next(secrets.Count)];
				g.OnRandomHappened(true);
				Generic.CastSpell(c, g, pick, null, 0);
			}), usable: c => !c.SecretZone.IsFull);

			// ---------------- Hagatha the Witch (GILA_BOSS_61h; her power is the collectible hero's passive, GIL_504h Bewitch)
			ExistingPassive("GIL_504h");
			// Ritual Dagger: "Lifesteal. After you cast a spell, this gains +1 Durability."
			BossCard("GILA_BOSS_61t", () => new CardDef(new Power
			{
				Trigger = new Trigger(TriggerType.AFTER_CAST)
				{
					TriggerSource = TriggerSource.FRIENDLY,
					SingleTask = new AddEnchantmentTask("GILA_BOSS_61te", EntityType.SOURCE),
				},
			}));
			Enchantment("GILA_BOSS_61te", () => new Power { Enchant = new Enchant(GameTag.DURABILITY, EffectOperator.ADD, 1) });
			// Ominous Fog: "Corrupt all enemy minions. Destroy them at the start of your next turn." (Corrupting Mist's rule on the
			// enemy minions only: UNG_831e destroys its minion at the start of the caster's next turn.)
			BossCard("GILA_BOSS_61t3", () => new CardDef(new Power { PowerTask = new AddEnchantmentTask("UNG_831e", EntityType.OP_MINIONS) }));
			// The monster hunters' hero cards the run player is given for this fight: "Battlecry: Deal N damage to all minions and
			// choose a Passive Treasure." The passive is chosen first, then the damage is dealt, and the hunter's own cards join the
			// deck (Actions.EncounterRules.PlayHunterCard reads them from the match's encounter rules). Toki's card (GILA_900h2) is
			// not scripted: her power GILA_900p Temporal Loop restarts the turn (phase 9).
			PlayerHeroCard("GILA_500h4", () => new CustomTask((g, c, s, t, stack) => EncounterRules.PlayHunterCard(g, c, (IPlayable)s, 8, stack)));
			PlayerHeroCard("GILA_600h2", () => new CustomTask((g, c, s, t, stack) => EncounterRules.PlayHunterCard(g, c, (IPlayable)s, 5, stack)));

			// ---------------- Dalaran Heist anomalies (symmetric: the player enchantment lands on both players)
			// Rejuvenating: "At the start of each player's turn, that player restores 2 Health to their hero."
			Rule("DALA_866", "DALA_866e", () => new Power
			{
				Trigger = new Trigger(TriggerType.TURN_START) { TriggerSource = TriggerSource.FRIENDLY, SingleTask = new HealTask(2, EntityType.HERO) },
			});
			// Growing: "Minions gain +1/+1 at the end of their owner's turn."
			Rule("DALA_864", "DALA_864e", () => new Power
			{
				Trigger = new Trigger(TriggerType.TURN_END) { TriggerSource = TriggerSource.FRIENDLY, SingleTask = new AddEnchantmentTask("DALA_864e2", EntityType.MINIONS) },
			});
			Enchantment("DALA_864e2", () => new Power { Enchant = new Enchant(Effects.AttackHealth_N(1)) });
			// Arcane: "All spells cost (2) less." (each player's copy covers that player's hand). An Echo or "Repeatable this turn" spell is never brought
			// below (1): at (0) it would repeat without end (Unstable Evolution), so it is cut by 1 per printed Cost above 1,
			// at most 2 (retail keeps Echo cards at (1) or more).
			Rule("DALA_850", "DALA_850e", () => new Power
			{
				Aura = new MultiAura(
					new Aura(AuraType.HAND, Effects.ReduceCost(2)) { Condition = new SelfCondition(p => p is Spell && !Repeatable(p)) },
					new Aura(AuraType.HAND, Effects.ReduceCost(1)) { Condition = new SelfCondition(p => p is Spell && Repeatable(p) && p.Card.Cost >= 2) },
					new Aura(AuraType.HAND, Effects.ReduceCost(1)) { Condition = new SelfCondition(p => p is Spell && Repeatable(p) && p.Card.Cost >= 3) }),
			});
		}
	}
}
