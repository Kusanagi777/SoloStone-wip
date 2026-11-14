using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.Actions
{
	/// <summary>
	/// Copy-casts of a spell the owner just cast (phase 7: TRLA_138 Shirvallah's Grace casts a copy onto the shrine; TRLA_187
	/// recasts on the same target). It follows Lynessa Sunsorrow's cast (SpecificTask.LynessaSunsorrow): a fresh copy of the
	/// card, cast as a Repeat cast with the target locked (a target the copy may not have loses only the targeted part), any
	/// choice it opens picked with the game RNG. A copy carries <see cref="Recasting"/>, so a recast never triggers another
	/// recast. All state is entity tags, so clones and AI simulations replay it.
	/// </summary>
	public static class RecastRules
	{
		/// <summary>1 on a spell copy a recast made (ShrineRules.IsPrivateTag: never sent in a native packet).</summary>
		public const GameTag Recasting = (GameTag)32620;

		/// <summary>The spell is a recast's copy: a recast trigger must not answer it.</summary>
		public static bool IsRecast(IPlayable spell) => spell != null && spell.NativeTags.TryGetValue(Recasting, out int v) && v != 0;

		/// <summary>The Choose One option the owner picked when <paramref name="spell"/> was played (0: none recorded).</summary>
		public static int ChosenOption(Spell spell)
		{
			var history = spell.Controller.PlayHistory;
			for (int i = history.Count - 1; i >= 0; i--)
				if (history[i].SourceId == spell.Id) return history[i].SubOption > 0 ? history[i].SubOption : 0;
			return 0;
		}

		/// <summary>
		/// Casts a copy of <paramref name="spell"/> for its controller onto <paramref name="onto"/> (null: untargeted). Returns the
		/// copy, or null when nothing was cast (the spell is itself a recast's copy, or the game is over).
		/// </summary>
		public static Spell Recast(Spell spell, ICharacter onto, int chooseOne = 0)
		{
			if (spell == null || IsRecast(spell)) return null;
			Controller c = spell.Controller;
			Game g = c.Game;
			if (g.State == State.COMPLETE) return null;
			var copy = (Spell)Entity.FromCard(c, spell.Card);
			copy.NativeTags[Recasting] = 1;
			using (var cast = g.Facts.Cast(copy, HistoryCastKind.Repeat))
			{
				cast.AllowBodyClaim = true;
				Generic.CastAutomaticSpell(c, g, copy, onto, chooseOne, lockedTarget: onto != null);
				cast.Complete();
			}
			while (c.Choice != null && g.State != State.COMPLETE)
				Generic.ChoicePick(c, g, c.Choice.Choices.Choose(g.Random));
			return copy;
		}
	}
}
