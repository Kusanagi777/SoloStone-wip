using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.Actions
{
	/// <summary>
	/// Rumble Run board shrines (phase 7, design S1). A run shrine is an ordinary 0-Attack minion that carries a private mark
	/// (<see cref="RunShrine"/>, set only here). When it dies it goes dormant in the slot it left (its "&lt;id&gt;t" form, a
	/// DormantRules timed sleeper) and revives at full Health as a fresh entity at the start of its owner's third turn after
	/// the death (second with TRLA_805 Pact with the Loa). Run rules: a transform, a bounce to hand or a shuffle into a deck
	/// aimed at an awake run shrine destroys it instead (so it goes dormant and its Deathrattles fire); a control change never
	/// takes it (random ones pick another minion, targeted ones cannot aim at it, any other does nothing); a copy is a plain minion; a dead run shrine stays out of resurrect pools. All state is entity tags, so clones
	/// and AI simulations replay it.
	/// </summary>
	public static class ShrineRules
	{
		/// <summary>1 on a run shrine, its dormant form and its corpse; set only by <see cref="ShrineRules"/>.</summary>
		public const GameTag RunShrine = (GameTag)32610;
		/// <summary>On a dormant form: the revive turns it was given at the death (3, or 2 with TRLA_805).</summary>
		public const GameTag ReviveTurnsTag = (GameTag)32611;
		/// <summary>How many times this shrine has revived (0 for the first body).</summary>
		public const GameTag Generation = (GameTag)32612;
		/// <summary>1 while <see cref="Revive"/> transforms a dormant form back, so the transform guard lets it through.</summary>
		public const GameTag Replacing = (GameTag)32613;
		/// <summary>
		/// The four tags above, <see cref="OverkillRules.AttackAgain"/> (32614) and the shrine families' working tags (32615-32629:
		/// <see cref="RecastRules.Recasting"/>, <see cref="Freezer"/>, <see cref="VengeanceResolving"/>,
		/// <see cref="CardSets.DungeonRun.DungeonRunShrines.FlameKill"/>, <see cref="CardSets.DungeonRun.DungeonRunShrines.HungerResolving"/>,
		/// <see cref="CardSets.DungeonRun.DungeonRunShrines.ExactorShield"/>): private, never sent in a
		/// native packet.
		/// </summary>
		public static bool IsPrivateTag(GameTag tag) => (int)tag >= 32610 && (int)tag <= 32629;

		/// <summary>
		/// On a character while a freeze aimed at it raises its freeze event (SetGameTagTask, HistoryFacts.Freeze): the entity id of
		/// the controller that freezes it, so "whenever you Freeze a character" (TRLA_129 Jan'alai's Progeny) can tell who froze.
		/// Removed again right after the event; a freeze from any other path carries none and counts for nobody.
		/// </summary>
		public const GameTag Freezer = (GameTag)32621;
		/// <summary>On the game while TRLA_137 Shirvallah's Vengeance deals its damage: a second Vengeance does not answer it.</summary>
		public const GameTag VengeanceResolving = (GameTag)32622;

		/// <summary>Raises the freeze event of <paramref name="target"/> with <paramref name="freezer"/> recorded as the one freezing it.</summary>
		internal static void Freezing(IPlayable target, Controller freezer, Action raise)
		{
			if (target == null || freezer == null) { raise(); return; }
			bool had = target.NativeTags.TryGetValue(Freezer, out int previous);
			target.NativeTags[Freezer] = freezer.Id;
			try { raise(); }
			finally { if (had) target.NativeTags[Freezer] = previous; else target.NativeTags.Remove(Freezer); }
		}

		/// <summary>The controller entity id freezing <paramref name="target"/> right now (0: none recorded).</summary>
		public static int FreezerOf(IEntity target) => target != null && target.NativeTags.TryGetValue(Freezer, out int id) ? id : 0;

		/// <summary>Owner's turns from a shrine's death to its revival.</summary>
		public const int BaseReviveTurns = 3;
		/// <summary>TRLA_805 Pact with the Loa's player enchantment: shrines revive one turn sooner.</summary>
		public const string FasterRevivalEnchantment = "TRLA_805e";
		/// <summary>The condition enchantment of every dormant shrine form (an exported 3-turn dormant condition).</summary>
		public const string DormantCondition = "TIME_046e";
		/// <summary>Start of Game priority of the shrine placement: lands before every other Start of Game source.</summary>
		public const int SetupPriority = -100;

		// The scripted shrines (CardSets.DungeonRun.DungeonRunShrines.Forms): data only, so reading them never loads the card database.
		private static IReadOnlyDictionary<string, string> FormOfShrine => CardSets.DungeonRun.DungeonRunShrines.Forms;
		private static readonly Lazy<Dictionary<string, string>> ShrineOfFormMap = new Lazy<Dictionary<string, string>>(() =>
			FormOfShrine.ToDictionary(p => p.Value, p => p.Key, StringComparer.Ordinal));

		/// <summary>The shrine ids the engine scripts (base forms), ordinal order.</summary>
		public static IReadOnlyList<string> Scripted => FormOfShrine.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();
		/// <summary>The shrine is scripted with its dormant form (the run layer offers a board shrine only then).</summary>
		public static bool IsScripted(string shrineId) => shrineId != null && FormOfShrine.ContainsKey(shrineId);
		/// <summary>The dormant form id of a scripted shrine, else null.</summary>
		public static string FormOf(string shrineId) => shrineId != null && FormOfShrine.TryGetValue(shrineId, out var f) ? f : null;
		/// <summary>The shrine a dormant form revives into, else null.</summary>
		public static string ShrineOf(string formId) => formId != null && ShrineOfFormMap.Value.TryGetValue(formId, out var s) ? s : null;

		/// <summary>Carries the run-shrine mark (awake, dormant or dead).</summary>
		public static bool IsMarked(IEntity entity) => entity != null && entity[RunShrine] == 1;
		/// <summary>An awake run shrine on a board (<see cref="Conditions.SelfCondition.IsRunShrine"/>).</summary>
		public static bool IsAwake(IPlayable p) => p is Minion m && m[RunShrine] == 1 && !m.IsDormant && m.Zone is Model.Zones.BoardZone;
		/// <summary>A run shrine's dormant form on a board.</summary>
		public static bool IsDormantForm(IPlayable p) => p is Minion m && m[RunShrine] == 1 && m.IsDormant && m.Zone is Model.Zones.BoardZone;
		/// <summary>A dead run shrine (in a graveyard): never in a resurrect or "died this game" pool.</summary>
		public static bool IsCorpse(IPlayable p) => p != null && p[RunShrine] == 1 && p.Zone?.Type == Zone.GRAVEYARD;
		/// <summary>The run shrines on <paramref name="c"/>'s board, awake and dormant, in board order.</summary>
		public static IReadOnlyList<Minion> Of(Controller c) =>
			c.BoardZone.Slots.OfType<Minion>().Where(m => m[RunShrine] == 1).ToArray();

		/// <summary>The owner's turns a shrine that dies now sleeps: 3, minus 1 with TRLA_805 (read at the death).</summary>
		public static int ReviveTurns(Controller c) =>
			Math.Max(1, BaseReviveTurns - (c.AppliedEnchantments?.Any(e => e.Card.Id == FasterRevivalEnchantment) == true ? 1 : 0));

		/// <summary>The turns left on a dormant form (0 when awake).</summary>
		public static int TurnsLeft(Minion m) => m != null && m.IsDormant ? DormantRules.TurnsRemaining(m) : 0;

		// ------------------------------------------------------------------ placement

		/// <summary>
		/// Arms <paramref name="copies"/> copies of <paramref name="shrineId"/> for <paramref name="c"/>'s Start of Game: each is a
		/// marked shrine entity in the set-aside zone, a pending Start of Game source (<see cref="StartOfGameRules.Pending"/>) that
		/// summons itself to the left-most free slot after both mulligans, before every other Start of Game effect
		/// (<see cref="SetupPriority"/>). Call after <see cref="Game.StartGame"/>, before the mulligan completes (DungeonRunSetup).
		/// </summary>
		public static IReadOnlyList<Minion> Arm(Controller c, string shrineId, int copies)
		{
			if (!IsScripted(shrineId)) throw new ArgumentException("Not a scripted run shrine: " + shrineId);
			var armed = new List<Minion>();
			for (int i = 0; i < Math.Max(1, copies); i++)
			{
				var shrine = (Minion)Entity.FromCard(c, Cards.FromId(shrineId), zone: c.SetasideZone);
				shrine[RunShrine] = 1;
				shrine[StartOfGameRules.Pending] = 1;
				armed.Add(shrine);
			}
			return armed;
		}

		/// <summary>The Start of Game effect of an armed shrine: summon it from the set-aside zone (nothing on a full board).</summary>
		internal static void EnterFromSetaside(IPlayable source)
		{
			if (!(source is Minion shrine) || shrine[RunShrine] != 1) return;
			Controller c = shrine.Controller;
			if (shrine.Zone != c.SetasideZone || c.BoardZone.IsFull) return;
			c.SetasideZone.Remove(shrine);
			Generic.SummonBlock(c.Game, shrine, NextSlot(c), c.Hero);
			CardSets.DungeonRun.DungeonRunShrines.Grant(shrine);
		}

		/// <summary>
		/// Summons a marked run shrine for <paramref name="c"/> now (test hooks and boss fixtures, e.g. TRLA_107 on the boss
		/// side); null on a full board. The run player's shrine comes through <see cref="Arm"/> instead.
		/// </summary>
		public static Minion Place(Controller c, string shrineId, IEntity source = null)
		{
			if (!IsScripted(shrineId)) throw new ArgumentException("Not a scripted run shrine: " + shrineId);
			if (c.BoardZone.IsFull) return null;
			var shrine = (Minion)Entity.FromCard(c, Cards.FromId(shrineId));
			shrine[RunShrine] = 1;
			Generic.SummonBlock(c.Game, shrine, NextSlot(c), source ?? c.Hero);
			CardSets.DungeonRun.DungeonRunShrines.Grant(shrine);
			return shrine;
		}

		// The left-most free slot, or just right of the last shrine already placed (two shrines sit side by side). Darius's
		// edge Cannons keep their edges (BoardZone.Add clamps the slot).
		private static int NextSlot(Controller c)
		{
			var placed = Of(c);
			return placed.Count == 0 ? 0 : Math.Min(placed.Max(m => m.ZonePosition) + 1, c.BoardZone.Count);
		}

		// ------------------------------------------------------------------ death and revival

		/// <summary>
		/// Game.GraveYard, right after a marked minion left the board and its Deathrattles were queued: its dormant form enters
		/// the slot it left (<paramref name="position"/>, always free), with no summon trigger and no summon fact, and starts its
		/// revival timer (3 owner's turns, 2 with TRLA_805).
		/// </summary>
		internal static void ReplaceWithDormant(Minion dead, int position)
		{
			string formId = FormOf(dead.Card.Id);
			Controller c = dead.Controller;
			if (formId == null || c.BoardZone.IsFull) return;
			var form = (Minion)Entity.FromCard(c, Cards.FromId(formId), new EntityData { [GameTag.DISPLAYED_CREATOR] = dead.Id });
			form[RunShrine] = 1;
			form[Generation] = dead[Generation];
			int turns = ReviveTurns(c);
			form[ReviveTurnsTag] = turns;
			c.BoardZone.Add(form, Math.Max(0, Math.Min(position, c.BoardZone.Count)));
			if (c.Game.History) c.Game.PowerHistory.Add(PowerHistoryBuilder.ShowEntity(form));
			if (turns < BaseReviveTurns && form.IsDormant) DormantRules.Accelerate(form, BaseReviveTurns - turns);
		}

		/// <summary>The dormant forms' awaken task: the form becomes its shrine again (<see cref="Revive"/>).</summary>
		public static ISimpleTask ReviveTask => new CustomTask((g, c, s, t, stack) => Revive(s as Minion));

		/// <summary>
		/// An awakened dormant form becomes its shrine: a fresh entity in the same slot (full Health, no damage, exhausted; its
		/// aura and trigger register again, and upgrade auras reach it again), mark kept, generation + 1. A form without the
		/// mark (a copy of a dormant form) never becomes a shrine.
		/// </summary>
		internal static void Revive(Minion form)
		{
			if (form == null || form[RunShrine] != 1 || form.Zone != form.Controller.BoardZone) return;
			string shrineId = ShrineOf(form.Card.Id);
			if (shrineId == null) return;
			Controller c = form.Controller;
			int position = form.ZonePosition;
			form[Replacing] = 1;
			bool done;
			try { done = Generic.TransformBlock(c, Cards.FromId(shrineId), form); }
			finally { form[Replacing] = 0; }
			if (!done || position >= c.BoardZone.Count) return;
			Minion shrine = c.BoardZone[position];
			if (shrine.Card.Id != shrineId) return;
			shrine[RunShrine] = 1;
			shrine[Generation] = form[Generation] + 1;
			// The shrine upgrades' grants come back with it (phase 7 U: DungeonRunShrines.Grant).
			CardSets.DungeonRun.DungeonRunShrines.Grant(shrine);
		}

		// ------------------------------------------------------------------ guards

		/// <summary>
		/// A transform aimed at a run shrine on a board: an awake one is destroyed instead (it goes dormant and its Deathrattles
		/// fire), a dormant form is left as it is. True when the transform must not happen. The revival's own transform passes.
		/// </summary>
		internal static bool InterceptTransform(Minion m)
		{
			if (m == null || m[RunShrine] != 1 || m[Replacing] != 0 || !(m.Zone is Model.Zones.BoardZone)) return false;
			if (!m.IsDormant && !m.ToBeDestroyed) m.Destroy();
			return true;
		}

		/// <summary>
		/// A bounce to hand, a shuffle into a deck or a "return / put into hand" aimed at a run shrine on a board: an awake one is
		/// destroyed instead, a dormant form stays. True when the move must not happen.
		/// </summary>
		internal static bool InterceptLeave(IPlayable p)
		{
			if (!(p is Minion m) || m[RunShrine] != 1 || !(m.Zone is Model.Zones.BoardZone)) return false;
			if (!m.IsDormant && !m.ToBeDestroyed) m.Destroy();
			return true;
		}

		/// <summary>A control change aimed at a run shrine (awake or dormant) does nothing; the card's other effects still resolve.</summary>
		internal static bool BlocksControl(IPlayable p) => p is Minion m && m[RunShrine] == 1;

		/// <summary>
		/// A run shrine cannot change controller, so it is never a choice for a control change: a random "take control of an
		/// enemy minion" (EX1_016 Sylvanas, EX1_085 Mind Control Tech, CAP_405t2) picks among the other minions, and a targeted
		/// one (<see cref="TakesControlOfTarget"/>) cannot be aimed at it. Nothing else is filtered: the minion still counts for
		/// "if your opponent has 4 or more minions".
		/// </summary>
		public static bool CanChangeController(IPlayable p) => !BlocksControl(p);

		/// <summary>The control-change candidates of a stack (IncludeTask then RandomTask): the run shrines dropped, order kept.</summary>
		internal static ISimpleTask DropUncontrollable => new FuncPlayablesTask(list =>
		{
			bool any = false;
			foreach (var p in list) if (BlocksControl(p)) { any = true; break; }
			return any ? list.Where(CanChangeController).ToList() : list;
		});

		// Targeted control changes the task tree does not show (a CustomTask or an enchantment that takes control later).
		private static readonly HashSet<string> OpaqueTargetedControl = new HashSet<string>(StringComparer.Ordinal)
		{
			"CATA_496",	// Cursed Chains: TemporaryRules.Borrow
			"ICC_849",	// Embrace Darkness: ICC_849e takes control at the owner's next turn start
		};

		/// <summary>
		/// The card's play takes control of its target: a <see cref="ControlTask"/> on TARGET (not "give to the opponent") in its
		/// play task tree (CS1_113 Mind Control, EX1_091, EX1_334, CFM_603, LOOTA_805, TIME_435, ...) or a listed opaque one.
		/// Card.TargetingRequirements then refuses a run shrine as its target. Read once per card (Card caches it).
		/// </summary>
		public static bool TakesControlOfTarget(Card card)
		{
			if (card == null) return false;
			if (OpaqueTargetedControl.Contains(card.Id)) return true;
			var task = card.Power?.PowerTask;
			return task != null && HasTargetControl(task, new HashSet<object>(SameObject.Instance), 0);
		}

		private static bool HasTargetControl(object node, HashSet<object> seen, int depth)
		{
			if (node == null || depth > 24 || !seen.Add(node)) return false;
			if (node is ControlTask control) return control.Type == EntityType.TARGET && !control.Opposite;
			if (!(node is ISimpleTask)) return false;
			for (var type = node.GetType(); type != null && type != typeof(object); type = type.BaseType)
				foreach (var field in type.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public
					| System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly))
				{
					object value = field.GetValue(node);
					if (value is ISimpleTask child) { if (HasTargetControl(child, seen, depth + 1)) return true; }
					else if (value is System.Collections.IEnumerable items && !(value is string))
						foreach (object item in items)
							if (item is ISimpleTask nested && HasTargetControl(nested, seen, depth + 1)) return true;
				}
			return false;
		}

		private sealed class SameObject : IEqualityComparer<object>
		{
			internal static readonly SameObject Instance = new SameObject();
			public new bool Equals(object x, object y) => ReferenceEquals(x, y);
			public int GetHashCode(object o) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(o);
		}

		/// <summary>A shrine upgrade's grant on a run shrine (phase 7 U): a copy of the shrine never inherits it.</summary>
		internal static bool IsUpgradeGrant(Enchantment e) => e != null && CardSets.DungeonRun.DungeonRunShrines.IsGrant(e.Card.Id);

		/// <summary>A copy of a run shrine (or of its dormant form) is a plain minion: the mark never copies.</summary>
		internal static void ClearMark(IPlayable copy)
		{
			if (copy == null) return;
			if (copy.NativeTags.ContainsKey(RunShrine)) copy.NativeTags.Remove(RunShrine);
			if (copy.NativeTags.ContainsKey(ReviveTurnsTag)) copy.NativeTags.Remove(ReviveTurnsTag);
			if (copy.NativeTags.ContainsKey(Generation)) copy.NativeTags.Remove(Generation);
			if (copy.NativeTags.ContainsKey(Replacing)) copy.NativeTags.Remove(Replacing);
		}
	}
}
