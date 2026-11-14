using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.src.Loader;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.DungeonRun
{
	/// <summary>What an encounter card is (how the validator, the ledger and the tests treat it).</summary>
	public enum EncounterCardKind
	{
		/// <summary>A clickable boss hero power, on its own client id.</summary>
		Power,
		/// <summary>A passive boss hero power: its trigger and aura start at game start and at a form change.</summary>
		Passive,
		/// <summary>A boss-only card a boss deck lists or a power creates (a spell, a minion, a weapon, a hero card).</summary>
		BossCard,
		/// <summary>A token with no behaviour of its own that a power or card summons or equips.</summary>
		Token,
		/// <summary>An enchantment a power, card or rule attaches.</summary>
		Enchantment,
		/// <summary>A hero card the run player is given for one fight (Hagatha's monster hunters).</summary>
		PlayerHeroCard,
		/// <summary>A global rule card (an anomaly or a twist); its effect is its player enchantment.</summary>
		Rule,
	}

	/// <summary>One scripted encounter card: metadata only; the script is built while the card database loads.</summary>
	public sealed class EncounterCard
	{
		public string Id { get; internal set; }
		public EncounterCardKind Kind { get; internal set; }
		/// <summary>The family that scripted it ("01".."25").</summary>
		public string Family { get; internal set; }
		/// <summary>A global rule's player enchantment (<see cref="EncounterCardKind.Rule"/>), else null.</summary>
		public string EffectId { get; internal set; }
		/// <summary>The power's trigger or aura must be started by hand (EncounterRules.ActivatePower).</summary>
		public bool NeedsActivation { get; internal set; }
		/// <summary>A hero power whose Cost follows the game: what is added to its printed Cost (EncounterRules.RefreshCosts); null for a fixed Cost.</summary>
		internal Func<Game, int> DynamicCost { get; set; }
		internal Func<CardDef> Script { get; set; }
		internal Action<Card> Shape { get; set; }
		/// <summary>A global rule's hooks beyond its player enchantment (<see cref="EncounterCardKind.Rule"/>), or null.</summary>
		internal RuleHooks Hooks { get; set; }
		/// <summary>A rule with <see cref="RuleHooks"/>: its bit in EncounterRules.RuleApplied (0 to 30), else -1.</summary>
		internal int HookIndex { get; set; } = -1;
	}

	/// <summary>
	/// What a global rule does beyond its player enchantment (phase 8 family 05). A rule whose text is a start condition has no
	/// "Player Enchantment" card in the client data (DALA_829 The Coffers, DALA_832 The Prisons, DALA_833 The Carts, DALA_859
	/// Gorged, DALA_863 Hallucinating): it is hooks only. Every hook reads and writes entity tags only, and each runs once per
	/// match (DungeonRunGlobalRules keeps the marks in EncounterRules.RuleApplied and EncounterRules.StartDone).
	/// </summary>
	public sealed class RuleHooks
	{
		/// <summary>Extra opening-hand cards for both players, dealt before the mulligan (DungeonRunSetup.Configure; the engine's cap applies).</summary>
		public int OpeningCards { get; set; }
		/// <summary>
		/// Family 06: the cards both players start with in hand (ULDA_715 Bountiful: 9; the second player's Coin is one of
		/// them), dealt before the mulligan; 0 = the rule states none. DungeonRunSetup.Configure raises each player's
		/// opening-hand bonus to reach it.
		/// </summary>
		public int OpeningHand { get; set; }
		/// <summary>At match start, before the mulligan completes (DungeonRunSetup.ApplyAfterStart), after the rule's enchantment is attached.</summary>
		public Action<Game> OnApply { get; set; }
		/// <summary>
		/// After both mulligans and every Start of Game source, after the encounter's own start block (EncounterRules.ResolveStart):
		/// what the rule puts on the boards or into the decks. Nothing of it exists before the mulligan.
		/// </summary>
		public Action<Game> OnStart { get; set; }
		/// <summary>The cards the rule creates (tokens, the enchantments it puts on cards): the validator checks the client knows each.</summary>
		public string[] Cards { get; set; }
	}

	/// <summary>
	/// The imported adventure encounters' cards (phase 8): boss hero powers, boss-only cards, tokens, the run player's fight-only
	/// hero cards and the global rules. Like the Rumble Run shrines they are the real client cards, keyed by their own ids (no
	/// borrowed shell and no DungeonRunContent entry, so the run content set and every earlier run are unchanged). One file per
	/// family (DungeonRunEncounters.F&lt;NN&gt;.cs) registers its cards from its own static method, listed in the constructor
	/// below; the registry is metadata, so reading <see cref="PassivePowers"/> never loads the card database. Every random
	/// choice in a script uses the game RNG and every piece of state is an entity tag (Actions.EncounterRules' tag table).
	/// </summary>
	public static partial class DungeonRunEncounters
	{
		private static readonly List<EncounterCard> Entries = new List<EncounterCard>();
		private static readonly Dictionary<string, EncounterCard> ById;
		private static readonly HashSet<string> ExistingPassives = new HashSet<string>(StringComparer.Ordinal);
		private static string family;

		static DungeonRunEncounters()
		{
			// One line per family, in family order; each family fills its own DungeonRunEncounters.F<NN>.cs (pre-created empty), so
			// no family ever edits this file.
			Register("01", F01);
			Register("02", F02);
			Register("03", F03);
			Register("04", F04);
			Register("05", F05);
			Register("06", F06);
			Register("07", F07);
			Register("08", F08);
			Register("09", F09);
			Register("10", F10);
			Register("11", F11);
			Register("12", F12);
			Register("13", F13);
			Register("14", F14);
			Register("15", F15);
			Register("16", F16);
			Register("17", F17);
			Register("18", F18);
			Register("19", F19);
			Register("20", F20);
			Register("21", F21);
			Register("22", F22);
			Register("23", F23);
			Register("24", F24);
			Register("25", F25);
			ById = Entries.ToDictionary(e => e.Id, StringComparer.Ordinal);
		}

		private static void Register(string name, Action add)
		{
			family = name;
			add();
			family = null;
		}

		/// <summary>Every scripted encounter card, in registration order.</summary>
		public static IReadOnlyList<EncounterCard> All => Entries;
		/// <summary>The entry of a card id, or null.</summary>
		public static EncounterCard Find(string id) => id != null && ById.TryGetValue(id, out var e) ? e : null;
		/// <summary>The id is a scripted encounter card.</summary>
		public static bool IsScripted(string id) => Find(id) != null;
		/// <summary>The passive boss powers registered here (they join DungeonRunCardsGen.PassiveHeroPowers).</summary>
		public static IEnumerable<string> PassivePowers => Entries.Where(e => e.Kind == EncounterCardKind.Passive).Select(e => e.Id);
		/// <summary>
		/// The power's trigger or aura is started by EncounterRules.ActivatePower: a passive registered here, a clickable power
		/// with an aura (a cost that changes), or an existing client passive listed with <see cref="ExistingPassive"/>.
		/// </summary>
		public static bool NeedsActivation(string powerId) =>
			powerId != null && (ExistingPassives.Contains(powerId) || Find(powerId)?.NeedsActivation == true);

		/// <summary>An existing client passive power listed with <see cref="ExistingPassive"/> (unclickable once EncounterRules started it).</summary>
		public static bool IsExistingPassive(string powerId) => powerId != null && ExistingPassives.Contains(powerId);

		/// <summary>Registers the scripts. Called by DungeonRunCardsGen.AddAll while the card database loads.</summary>
		internal static void AddAll(Dictionary<string, CardDef> cards)
		{
			foreach (EncounterCard entry in Entries)
			{
				CardDef def = entry.Script != null ? entry.Script() : new CardDef();
				Action<Card> shape = entry.Shape;
				def.Configure = card =>
				{
					card.Implemented = true;
					shape?.Invoke(card);
				};
				cards.Add(entry.Id, def);
			}
		}

		// ---------------------------------------------------------------- registration helpers (the family API)

		private static EncounterCard Add(string id, EncounterCardKind kind, Func<CardDef> script, Action<Card> shape = null, bool activate = false, string effect = null)
		{
			if (family == null) throw new InvalidOperationException("Encounter cards register inside Register(family, ...).");
			if (Entries.Any(e => e.Id == id)) throw new InvalidOperationException("Encounter card registered twice: " + id);
			var entry = new EncounterCard { Id = id, Kind = kind, Family = family, Script = script, Shape = shape, NeedsActivation = activate, EffectId = effect };
			Entries.Add(entry);
			return entry;
		}

		private static Dictionary<PlayReq, int> Reqs(params PlayReq[] reqs) => reqs.ToDictionary(r => r, r => 0);

		/// <summary>
		/// A clickable boss power on its own client id (its printed Cost and text are the client's). <paramref name="reqs"/> are
		/// its play requirements (none for an untargeted power). <paramref name="cost"/> is added to the printed Cost while the
		/// power is on the board (Wrath Smash: the damaged minions): the engine's adaptive cost effects cover hand cards only, so
		/// EncounterRules.RefreshCosts sets a hero power's Cost at every aura update once EncounterRules.ActivatePower started it.
		/// <paramref name="usable"/> is an extra condition for offering the power at all (Dampen Magic: no Counterspell in play
		/// yet); it joins the card's play requirements. It may read only what is exact in the owner's fair view (the boards, its
		/// own hand and Secrets, zone counts), never its deck or the opponent's hidden zones: the boss's search samples those, and
		/// an option that is legal in a sample but not in the real game cannot be taken (a fallback).
		/// </summary>
		private static void Power(string id, PlayReq[] reqs, Func<ISimpleTask> task, Func<Game, int> cost = null, Func<Controller, bool> usable = null) =>
			Add(id, EncounterCardKind.Power, () =>
			{
				var power = new Power { PowerTask = task() };
				return reqs == null || reqs.Length == 0 ? new CardDef(power) : new CardDef(Reqs(reqs), power);
			}, usable == null ? (Action<Card>)null : card => card.PlayAvailabilityPredicate += (c, _) => usable(c), activate: cost != null).DynamicCost = cost;

		/// <summary>
		/// A passive boss power on its own client id: <paramref name="power"/> holds its Trigger and/or Aura (no PowerTask). The
		/// engine never offers it as an option (it joins the passive ids); its trigger and aura start at game start and at a form
		/// change (EncounterRules.ActivatePower), with no task, so a form change inside death processing is safe.
		/// </summary>
		private static void Passive(string id, Func<Power> power) =>
			Add(id, EncounterCardKind.Passive, () =>
			{
				Power p = power();
				if (p.PowerTask != null) throw new InvalidOperationException("A passive encounter power has no PowerTask: " + id);
				p.PowerTask = new CustomTask((g, c, s, t, stack) => EncounterRules.ActivatePower(s as HeroPower));
				return new CardDef(p);
			}, activate: true);

		/// <summary>
		/// An existing, already scripted client hero power that is passive through its Trigger (GIL_504h Bewitch): the engine
		/// starts such a trigger only when its hero card is played, so a boss that starts with it needs EncounterRules to start it.
		/// </summary>
		private static void ExistingPassive(string id) => ExistingPassives.Add(id);

		/// <summary>A boss-only card (spell, minion, weapon): <paramref name="script"/> builds its CardDef.</summary>
		private static void BossCard(string id, Func<CardDef> script, Action<Card> shape = null) => Add(id, EncounterCardKind.BossCard, script, shape);

		/// <summary>A token with no behaviour (a vanilla minion or weapon a power summons or equips).</summary>
		private static void Token(string id) => Add(id, EncounterCardKind.Token, null);

		/// <summary>An enchantment a power, card or rule attaches.</summary>
		private static void Enchantment(string id, Func<Power> power) => Add(id, EncounterCardKind.Enchantment, () => new CardDef(power()));

		/// <summary>A fight-only hero card for the run player (its power comes from the card's own HERO_POWER tag).</summary>
		private static void PlayerHeroCard(string id, Func<ISimpleTask> battlecry) =>
			Add(id, EncounterCardKind.PlayerHeroCard, () => new CardDef(new Power { PowerTask = battlecry() }));

		/// <summary>
		/// A global rule (an anomaly or a twist): the rule card <paramref name="id"/> is the offer and display card (never played),
		/// <paramref name="effectId"/> its "Player Enchantment" card, whose <paramref name="effect"/> (Trigger and/or Aura) lands on
		/// both players at match start (DungeonRunGlobalRules.Apply).
		/// </summary>
		/// <remarks>
		/// Family 05: <paramref name="hooks"/> adds what an enchantment cannot carry (extra opening cards, a start block); a rule
		/// that is hooks only passes null for <paramref name="effectId"/> and <paramref name="effect"/>.
		/// </remarks>
		private static void Rule(string id, string effectId, Func<Power> effect, RuleHooks hooks = null)
		{
			if (effectId == null && hooks == null) throw new InvalidOperationException("A global rule needs a player enchantment or hooks: " + id);
			EncounterCard rule = Add(id, EncounterCardKind.Rule, null, effect: effectId);
			if (hooks != null)
			{
				rule.Hooks = hooks;
				rule.HookIndex = Entries.Count(e => e.Hooks != null) - 1;
				if (rule.HookIndex > 30) throw new InvalidOperationException("More than 31 hooked global rules: EncounterRules.RuleApplied needs a second tag.");
			}
			if (effectId != null) Add(effectId, EncounterCardKind.Enchantment, () => new CardDef(effect()));
		}
	}

	/// <summary>
	/// The global rules (phase 8: Dalaran Heist anomalies, Tombs of Terror twists). A rule is active for a whole match on both
	/// players: its player enchantment (the client's "... Player Enchantment" card) is attached to each controller, where a
	/// FRIENDLY trigger fires for that player's own events and an aura covers that player's own zones. Run-layer rules
	/// (ULDA_720, ULDA_729) have no match script and never reach the plan.
	/// </summary>
	public static class DungeonRunGlobalRules
	{
		/// <summary>The rule card ids the engine scripts (DungeonRunEncounters entries of kind Rule), ordinal order.</summary>
		public static IReadOnlyList<string> Scripted =>
			DungeonRunEncounters.All.Where(e => e.Kind == EncounterCardKind.Rule).Select(e => e.Id).OrderBy(id => id, StringComparer.Ordinal).ToList();

		public static bool IsScripted(string ruleId) => DungeonRunEncounters.Find(ruleId)?.Kind == EncounterCardKind.Rule;

		/// <summary>The rule's player enchantment card id, or null for an unscripted rule.</summary>
		public static string EffectOf(string ruleId) => IsScripted(ruleId) ? DungeonRunEncounters.Find(ruleId).EffectId : null;

		private static RuleHooks HooksOf(string ruleId) => IsScripted(ruleId) ? DungeonRunEncounters.Find(ruleId).Hooks : null;

		/// <summary>The rule places something after both mulligans (<see cref="RuleHooks.OnStart"/>).</summary>
		public static bool HasStart(string ruleId) => HooksOf(ruleId)?.OnStart != null;

		/// <summary>The extra opening-hand cards the rules give both players (<see cref="RuleHooks.OpeningCards"/>), summed.</summary>
		public static int OpeningCards(IEnumerable<string> ruleIds) => (ruleIds ?? Enumerable.Empty<string>()).Sum(id => Math.Max(0, HooksOf(id)?.OpeningCards ?? 0));

		/// <summary>The opening hand size the rules state for both players (<see cref="RuleHooks.OpeningHand"/>: the largest), 0 for none.</summary>
		public static int OpeningHand(IEnumerable<string> ruleIds) =>
			(ruleIds ?? Enumerable.Empty<string>()).Select(id => Math.Max(0, HooksOf(id)?.OpeningHand ?? 0)).DefaultIfEmpty(0).Max();

		/// <summary>The cards the rule creates beyond its player enchantment (<see cref="RuleHooks.Cards"/>); empty for none.</summary>
		public static IReadOnlyList<string> CardsOf(string ruleId) => HooksOf(ruleId)?.Cards ?? Array.Empty<string>();

		/// <summary>
		/// EncounterRules.ResolveStart (once per match, after both mulligans and the encounter's own start block): each active
		/// rule's start hook, in the plan's order.
		/// </summary>
		internal static void ResolveStart(Game game)
		{
			foreach (string ruleId in game.EncounterOrNull?.GlobalRules ?? Array.Empty<string>())
			{
				HooksOf(ruleId)?.OnStart?.Invoke(game);
				game.ProcessTasks();
			}
		}

		/// <summary>
		/// Attaches <paramref name="ruleId"/>'s player enchantment to both players (first player first), starting its trigger and
		/// aura. Call after Game.StartGame and before the mulligan completes (DungeonRunSetup.ApplyAfterStart), so it is live from
		/// turn 1. A rule applied twice stacks nothing (one enchantment per player).
		/// </summary>
		public static void Apply(Game game, string ruleId)
		{
			if (!IsScripted(ruleId)) throw new ArgumentException("Not a scripted global rule: " + ruleId);
			EncounterCard rule = DungeonRunEncounters.Find(ruleId);
			Card card = rule.EffectId == null ? null : Cards.FromId(rule.EffectId);
			foreach (Controller c in new[] { game.FirstPlayer, game.FirstPlayer.Opponent })
			{
				if (card == null || c.AppliedEnchantments?.Any(e => e.Card.Id == card.Id) == true) continue;
				QuestRules.AddPlayerEffect(c, c.Hero, card.Id);
				if (card.Power?.Aura != null) card.Power.Aura.Activate(c.AppliedEnchantments.Last(e => e.Card.Id == card.Id));
			}
			// Family 05: a hooked rule is marked on the game (one bit per hooked rule), so its apply hook runs once however often
			// the rule is applied, in the game and in every clone.
			if (rule.Hooks == null) return;
			int bit = 1 << rule.HookIndex, applied = game[EncounterRules.RuleApplied];
			if ((applied & bit) != 0) return;
			game[EncounterRules.RuleApplied] = applied | bit;
			rule.Hooks.OnApply?.Invoke(game);
		}

		/// <summary>The rule is active for <paramref name="c"/> in this match (its player enchantment is attached).</summary>
		public static bool IsActive(Controller c, string ruleId)
		{
			string effectId = EffectOf(ruleId);
			if (effectId != null) return c.AppliedEnchantments?.Any(e => e.Card.Id == effectId) == true;
			// A rule that is hooks only (no player enchantment): its mark on the game.
			EncounterCard rule = IsScripted(ruleId) ? DungeonRunEncounters.Find(ruleId) : null;
			return rule?.Hooks != null && (c.Game[EncounterRules.RuleApplied] & (1 << rule.HookIndex)) != 0;
		}
	}
}
