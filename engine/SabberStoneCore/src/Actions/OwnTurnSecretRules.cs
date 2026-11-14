using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.Actions
{
	/// <summary>
	/// A Secret that "can trigger on your turn" (Tombs of Terror ULDA_045t Primordial Bulwark). A Secret is exhausted at its
	/// owner's turn start (Game.MainReady), and Trigger.Validate ignores an exhausted Secret, so an ordinary Secret only
	/// triggers on the opponent's turn. A card whose printed <see cref="Tag"/> is set is never exhausted: it stays live on its
	/// owner's turn. The rule is read from the card, so every clone and every fair view (the boss AI's sampled Secret worlds
	/// included) applies it the same way.
	/// </summary>
	public static class OwnTurnSecretRules
	{
		/// <summary>Printed card tag: the Secret can trigger on its owner's turn.</summary>
		public const GameTag Tag = (GameTag)34900;

		/// <summary>True when <paramref name="secret"/> can trigger on its owner's turn.</summary>
		public static bool TriggersOnOwnTurn(IPlayable secret) => secret?.Card != null && secret.Card[Tag] != 0;

		/// <summary>The exhausted state a Secret takes at its owner's turn start: exhausted unless it triggers on that turn.</summary>
		public static bool ExhaustedOnOwnTurn(IPlayable secret) => !TriggersOnOwnTurn(secret);
	}
}
