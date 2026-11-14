using System;
using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.Actions
{
	/// <summary>
	/// How many extra times a Battlecry resolves (phase 7 S3). The doubling (Brann Bronzebeard, LOOTA_846 Battle Totem, Mug'Zee's
	/// fifth minion, Dark Gifts) stays one more resolution, as before; on top of it comes EXTRA_BATTLECRIES_ADDITIONAL (1126),
	/// which TRLA_154 Tribute from the Tides adds 2 of ("Your Battlecries trigger two additional times."). That tag is read as
	/// EXTRA_DEATHRATTLES_ADDITIONAL is (<see cref="DeathrattleRules"/>): from the controller's live source auras, never a
	/// controller field, so an aura's owner leaving play, dying or being silenced ends it and clones replay it.
	/// </summary>
	public static class BattlecryRules
	{
		/// <summary>The additional Battlecry resolutions <paramref name="c"/>'s auras grant now (0 without one).</summary>
		public static int Additional(Controller c) => c == null ? 0 : Math.Max(0, c[GameTag.EXTRA_BATTLECRIES_ADDITIONAL]);

		/// <summary>
		/// The resolutions of a Battlecry <paramref name="source"/> of <paramref name="c"/> plays: 1, plus 1 with a doubling
		/// (<see cref="Controller.ExtraBattlecry"/>, Mug'Zee's), plus <see cref="Additional"/>. Battle Totem with Tribute from the
		/// Tides: 1 + 1 + 2.
		/// </summary>
		public static int Repeats(Controller c, IPlayable source) =>
			1 + (c.ExtraBattlecry || CardSets.Client.Sets.MugZee.DoubleBattlecry(source) ? 1 : 0) + Additional(c);
	}
}
