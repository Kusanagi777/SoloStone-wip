using SabberStoneCore.CardSets.DungeonRun;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.Actions
{
	// Phase 8, family 14 (Dalaran Heist bosses A): a "script" form that changes only the boss's hero power (Flight Master
	// Belnaara: Take Flight! becomes Flying! and back). SwapForm replaces the hero entity for a form that names a hero (form 0
	// always does: the row's own hero) and starts every form at its full Health; a power-only form keeps the hero entity, so its
	// damage, Armor, weapon and enchantments stay. The form index and the change count are the model's tags (FormIndex,
	// FormChanges), so Harvest and the run layer read them as for any form. No new tag.
	public static partial class EncounterRules
	{
		/// <summary>
		/// The boss enters form <paramref name="index"/> by its power only: the hero, its Health, Armor, weapon and enchantments
		/// stay; the old power's trigger and aura stop (it goes to the set-aside zone) and the form's power enters fresh, its
		/// trigger and aura started (<see cref="ActivatePower"/>). Nothing outside an encounter match or past the last form.
		/// </summary>
		public static void SwapPowerForm(Controller boss, int index)
		{
			Game game = boss.Game;
			RunEncounter enc = Of(game);
			if (enc == null || index < 0 || index >= enc.BossForms.Count || game.State == Enums.State.COMPLETE) return;
			Card power = enc.BossForms[index].Power;
			HeroPower old = boss.Hero.HeroPower;
			if (power != null)
			{
				DeactivatePower(old);
				new ReplaceHeroPower(power).Process(game, boss, boss.Hero, null);
			}
			boss[FormIndex] = index;
			boss[FormChanges] = boss[FormChanges] + 1;
			ActivatePower(boss.Hero.HeroPower);
		}
	}
}
