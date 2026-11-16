using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Auras;
using SabberStoneCore.CardSets.DungeonRun;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.Actions
{
	/// <summary>
	/// Dungeon Run encounter rules (phase 8): what an imported adventure boss needs beyond a hero, a power and a deck. The run
	/// layer states it in the match plan (<see cref="RunMatchPlan"/>), <see cref="DungeonRunSetup.Configure"/> copies it into
	/// <see cref="Config.GameConfig.Encounter"/> (immutable, shared by every clone), and the hooks below read it: boss forms
	/// (a phased boss changes hero, power, Health and deck), the start block (boards, weapons, deck and hand adds after both
	/// mulligans), the run player's hero relay, carried damage, and the match-end harvest the run layer reads. Every piece of
	/// state is an entity tag of the table below, so clones and AI simulations replay it; outside an encounter match the
	/// config is null and every hook returns at once.
	/// </summary>
	public static partial class EncounterRules
	{
		// ------------------------------------------------------------------ tag table (32630-32699)
		// One allocation table for every encounter family: a family that needs a counter adds a constant here (32636-32659; family NN's
		// reserved number is 32635 + NN: taken 32637 by family 02, 32641 by family 06, 32643 by family 08, 32644 by family 09,
		// 32645 by family 10, 32646 by family 11, 32652 by family 17, 32653 by family 18, 32657 by family 22 and 32659 by family 24;
		// families 12-16, 19-21 and 23 took none),
		// never a number in its own file, so two families cannot collide. 32660-32699 are the global rules'
		// (DungeonRunGlobalRules: 32660-32662 taken by family 05 and the wave 2 integration, 32680 by family 06, whose
		// rule-state tags are 32680-32699).
		// All of them are private: NativeMatch never sends them (IsPrivateTag joins NativeMatch.IsDecisionInternalTag).
		/// <summary>On the boss's controller: the index of its current form (<see cref="RunEncounter.BossForms"/>).</summary>
		public const GameTag FormIndex = (GameTag)32630;
		/// <summary>On the game: 1 once the start block landed (<see cref="ResolveStart"/> runs once).</summary>
		public const GameTag StartDone = (GameTag)32631;
		/// <summary>On the run player's controller: how many relay heroes have entered so far.</summary>
		public const GameTag RelayIndex = (GameTag)32632;
		/// <summary>On the game: 1 when a script ended the fight with no loss for the run player (Treasure Vault's "End the game").</summary>
		public const GameTag NoLoss = (GameTag)32633;
		/// <summary>On a hero power: 1 once <see cref="ActivatePower"/> activated its trigger and aura (never twice).</summary>
		public const GameTag PowerActive = (GameTag)32634;
		/// <summary>On the boss's controller: how many form changes it has made this match (the harness asserts on it).</summary>
		public const GameTag FormChanges = (GameTag)32635;
		/// <summary>On the game: 1 once a hero power with a Cost that follows the game was started (<see cref="RefreshCosts"/> then runs).</summary>
		public const GameTag CostWatch = (GameTag)32636;
		/// <summary>
		/// On a card (a minion in play, a hero power) or as a tag of the hero itself: "Your hero can only take N damage at a
		/// time" (the value is N). Family 02 (LOE_119 Animated Armor; rules in EncounterRules.F02.cs: <see cref="HeroDamageCapOf"/>).
		/// </summary>
		public const GameTag HeroDamageCap = (GameTag)32637;
		/// <summary>
		/// On a player's controller: its hero takes double damage (the value is not read beyond non-zero). Family 06 (ULDA_714
		/// In the Face; rules in EncounterRules.F06.cs: <see cref="HeroDamageTaken"/>).
		/// </summary>
		public const GameTag HeroDamageDouble = (GameTag)32641;
		/// <summary>
		/// On the boss's controller (the tag team only): the benched hero. 0 = it has not fought yet (undamaged); n in
		/// 1..<see cref="TagTeamDefeated"/>-1 = it waits with n-1 damage; <see cref="TagTeamDefeated"/> = it is defeated.
		/// Family 08 (George and Karl; rules in EncounterRules.F08.cs).
		/// </summary>
		public const GameTag TagTeamBench = (GameTag)32643;
		/// <summary>
		/// On a controller: the special-room rules it plays by, as bits (EncounterRules.RoomEntered, RoomNoDraw, RoomNoPlay).
		/// Family 09 (Tad and the Treasure Vault hold no cards, A. F. Kay is idle; rules in EncounterRules.F09.cs).
		/// </summary>
		public const GameTag RoomRules = (GameTag)32644;
		/// <summary>
		/// On a holder (Gnomenapper's Stuffed Sack): the entity id of the minion it keeps out of play in its controller's
		/// set-aside zone, 0 for none. Family 10 (the set-aside store; rules in EncounterRules.F10.cs).
		/// </summary>
		public const GameTag SetAsideStore = (GameTag)32645;
		/// <summary>
		/// On a hero power: 1 while the use that is resolving earned it a refresh ("If any die, refresh this"); its own Inspire
		/// trigger readies it again and clears the mark. Family 11 (Raeth Ghostsong's Desecrate; rules in EncounterRules.F11.cs).
		/// </summary>
		public const GameTag PowerRefresh = (GameTag)32646;
		/// <summary>
		/// On an Extortion (DALA_BOSS_59t3): the entity id of the card it holds in its player's set-aside zone, 0 for none.
		/// Family 17 (Madam Goya's Blackmail; rules in EncounterRules.F17.cs).
		/// </summary>
		public const GameTag ExtortionEscrow = (GameTag)32652;
		/// <summary>
		/// On a hero power (Kazamon Steelskin's Steelskin, "You can't damage yourself"): damage its owner's sources deal to its
		/// owner's hero is prevented. Family 18 (rules in EncounterRules.F18.cs: <see cref="PreventsSelfDamage"/>).
		/// </summary>
		public const GameTag SelfDamageImmune = (GameTag)32653;
		/// <summary>
		/// A Beautiful Oasis's Mirage (family 22; rules in DungeonRunEncounters.TombsD): on the boss's hero power, the illusion's public
		/// hand position (1-based); on the illusion card, its twin's entity id times four, plus two, plus one when it is the added copy.
		/// </summary>
		public const GameTag MirageIllusion = (GameTag)32657;
		/// <summary>
		/// Family 24 (the Plague Lords; rules in EncounterRules.F24.cs), one counter on three kinds of entity: on a controller, the
		/// damage its ULDA_115 "Gnomebliterator" casts banked this match for the run's next Plague Lord (10 plus the cast's Spell
		/// Damage each, outside a Plague Lord fight: <see cref="Harvest"/>); on a Mur-nado hero power (ULDA_BOSS_39p3), the
		/// Murlocs that died while it was active (each makes it cost (1) less); on a spell, 1 while BOT_543 Omega Mind's Mind Meld
		/// gave it Lifesteal (taken back when the turn ends).
		/// </summary>
		public const GameTag PlagueCount = (GameTag)32659;
		/// <summary>
		/// On the game: one bit per hooked global rule (EncounterCard.HookIndex) that was applied to this match (family 05:
		/// DungeonRunGlobalRules.Apply runs a rule's apply hook once; a rule with no player enchantment is active by this mark).
		/// </summary>
		public const GameTag RuleApplied = (GameTag)32660;
		/// <summary>On a global rule's player enchantment: the spells its owner cast this turn (family 05: DALA_860 Dragon Soul).</summary>
		public const GameTag RuleSpells = (GameTag)32661;
		/// <summary>
		/// On a global rule's player enchantment: the minions it marked for its owner this turn (family 05: DALA_865 Explosive
		/// stops at DungeonRunEncounters.ExplosiveMarksPerTurn; added by the wave 2 integration).
		/// </summary>
		public const GameTag RuleSummons = (GameTag)32662;
		/// <summary>
		/// On an Eternal Tomb (ULDA_804t): the minions it summoned this turn (family 06: ULDA_804 Plague of Death stops at
		/// DungeonRunEncounters.TombSummonsPerTurn; the first of family 06's rule-state tags 32680-32699).
		/// </summary>
		public const GameTag RuleTombSummons = (GameTag)32680;
		/// <summary>First tag of the global rules' range (DungeonRunGlobalRules: one mark per attached rule copy).</summary>
		public const int RuleTagsFrom = 32660, RuleTagsTo = 32699;

		/// <summary>The encounter tags: private, never sent in a native packet.</summary>
		public static bool IsPrivateTag(GameTag tag) => (int)tag >= 32630 && (int)tag <= RuleTagsTo;

		private static RunEncounter Of(Game game) => game?.EncounterOrNull;

		// ------------------------------------------------------------------ hero powers

		/// <summary>
		/// Activates a hero power's trigger and aura on the board (a passive encounter power, a clickable power with a cost
		/// aura, or an existing client passive such as GIL_504h Bewitch whose trigger the engine starts only when its hero card
		/// is played). Direct, no task: safe inside death processing (a form change). Once per power entity.
		/// </summary>
		public static void ActivatePower(HeroPower power)
		{
			if (power == null || power[PowerActive] != 0) return;
			power[PowerActive] = 1;
			if (power.ActivatedTrigger == null) power.Power?.Trigger?.Activate(power);
			if (power.OngoingEffect == null) power.Power?.Aura?.Activate(power);
			if (DungeonRunEncounters.Find(power.Card.Id)?.DynamicCost != null)
			{
				power.Game[CostWatch] = 1;
				RefreshCosts(power.Game);
			}
		}

		/// <summary>
		/// Game.AuraUpdate: a hero power whose Cost follows the game (DungeonRunEncounters.Power's cost function: Wrath Smash costs
		/// (1) more for each damaged minion) gets its Cost set to its printed Cost plus the function's value. One game-tag read in
		/// every other game.
		/// </summary>
		internal static void RefreshCosts(Game game)
		{
			if (game[CostWatch] == 0) return;
			foreach (Controller c in new[] { game.Player1, game.Player2 })
			{
				HeroPower power = c.Hero?.HeroPower;
				Func<Game, int> extra = power == null ? null : DungeonRunEncounters.Find(power.Card.Id)?.DynamicCost;
				if (extra == null) continue;
				int cost = Math.Max(0, power.Card.Cost + extra(game));
				if (power.Cost != cost) power.Cost = cost;
			}
		}

		/// <summary>
		/// Game.MainStart, right after the turn-start draw and before the death and aura pass: the current player's rule-cast
		/// power (DungeonRunEncounters.AutoCast; the wiki's auto-cast is "the boss's first action each turn", after the
		/// start-of-turn triggered effects) is used, so a card drawn this turn counts (For The Light! buffs it). Only a started
		/// power (<see cref="PowerActive"/>) of a living hero; one dictionary lookup in every other game.
		/// </summary>
		internal static void AfterTurnStartDraw(Game game)
		{
			Controller c = game.CurrentPlayer;
			HeroPower power = c?.Hero?.HeroPower;
			if (power == null || power[PowerActive] == 0 || game.State == State.COMPLETE || c.Hero.ToBeDestroyed) return;
			Tasks.ISimpleTask cast = DungeonRunEncounters.Find(power.Card.Id)?.AfterDraw;
			if (cast == null) return;
			game.TaskQueue.Execute(cast, c, power, null, historyTrigger: true);
			game.ProcessTasks();
		}

		/// <summary>Stops a replaced hero power's trigger and aura (the old power sits in the set-aside zone afterwards).</summary>
		public static void DeactivatePower(HeroPower power)
		{
			if (power == null) return;
			power.ActivatedTrigger?.Remove();
			power.OngoingEffect?.Remove();
			if (power.NativeTags.ContainsKey(PowerActive)) power.NativeTags.Remove(PowerActive);
		}

		/// <summary>
		/// Match start (DungeonRunSetup.ApplyAfterStart): a starting power the encounter registry lists as needing activation
		/// (<see cref="DungeonRunEncounters.NeedsActivation"/>) gets its trigger and aura now, so it is live from turn 1.
		/// </summary>
		internal static void ActivateStartingPowers(Game game)
		{
			foreach (Controller c in new[] { game.Player1, game.Player2 })
			{
				HeroPower power = c.Hero?.HeroPower;
				if (power != null && DungeonRunEncounters.NeedsActivation(power.Card.Id)) ActivatePower(power);
			}
		}

		// ------------------------------------------------------------------ start block

		/// <summary>
		/// Game.MainBegin, after both mulligans and after every Start of Game source (the shrines land first, at priority
		/// <see cref="ShrineRules.SetupPriority"/>): the encounter's start block. Boss board, player board, the boss's weapon and
		/// Armor, then the deck adds (shuffled in), then the hand adds, then the run player's hero cards (to hand; a full hand
		/// shuffles the rest into the deck). Nothing of it exists before the mulligan. Runs once (<see cref="StartDone"/>).
		/// </summary>
		internal static void ResolveStart(Game game)
		{
			RunEncounter enc = Of(game);
			if (enc == null || !enc.HasStart || game[StartDone] != 0) return;
			game[StartDone] = 1;
			RunStart start = enc.Start ?? RunStart.Empty;
			Controller boss = game.Player2, player = game.Player1;
			if (game.History) game.PowerHistory.Add(PowerHistoryBuilder.BlockStart(BlockType.TRIGGER, boss.Hero.Id, "", 0, 0));
			Summon(boss, start.BossBoard);
			Summon(player, start.PlayerBoard);
			if (start.BossWeapon != null)
			{
				// As an equip effect does (WeaponTask): the weapon's aura and trigger start with it (family 10: the Scarecrow's Scythe).
				var weapon = (Weapon)Entity.FromCard(boss, start.BossWeapon);
				weapon.Card.Power?.Aura?.Activate(weapon);
				weapon.Card.Power?.Trigger?.Activate(weapon);
				boss.Hero.AddWeapon(weapon);
			}
			if (start.BossArmor > 0) boss.Hero.GainArmor(boss.Hero, start.BossArmor);
			Shuffle(boss, start.BossDeckAdd);
			Shuffle(player, start.PlayerDeckAdd);
			Give(boss, start.BossHandAdd);
			Give(player, start.PlayerHandAdd);
			Give(player, enc.PlayerHeroCards);
			game.ProcessTasks();
			// Family 05: the active global rules' start hooks (carts, prisons, deck inserts), after the encounter's own block.
			DungeonRunGlobalRules.ResolveStart(game);
			if (game.History) game.PowerHistory.Add(PowerHistoryBuilder.BlockEnd());
		}

		private static void Summon(Controller c, IReadOnlyList<Card> cards)
		{
			foreach (Card card in cards)
			{
				if (c.BoardZone.IsFull) break;
				Generic.SummonBlock(c.Game, (Minion)Entity.FromCard(c, card), -1, c.Hero);
			}
		}

		private static void Shuffle(Controller c, IReadOnlyList<Card> cards)
		{
			foreach (Card card in cards) Generic.ShuffleIntoDeck(c, c.Hero, Entity.FromCard(c, card));
		}

		private static void Give(Controller c, IReadOnlyList<Card> cards)
		{
			foreach (Card card in cards)
			{
				IPlayable entity = Entity.FromCard(c, card);
				if (c.HandZone.IsFull) Generic.ShuffleIntoDeck(c, c.Hero, entity);
				else Generic.AddHandPhase.Invoke(c, entity);
			}
		}

		// ------------------------------------------------------------------ forms

		/// <summary>
		/// Beside CorpseRules.ResurrectHeroes (Game.CheckDeadHeroes and the end of Game.GraveYard), before a dead hero loses:
		/// the boss's next form enters when its condition holds ("healthAtMost": the living boss is at or below the threshold;
		/// "onDeath": the boss died), several in a row when one hit crosses two thresholds; then the run player's relay (a dead
		/// run hero with a relay hero left is replaced). A boss killed outright past its last Health threshold dies.
		/// </summary>
		internal static void ResolveHeroForms(Game game)
		{
			RunEncounter enc = Of(game);
			if (enc == null || game.State == State.COMPLETE) return;
			ResolveUnderbellyRats(game); // family 15, The Rat King: a living Underbelly Rat replaces a dead hero (EncounterRules.F15.cs)
			if (enc.BossForms.Count > 1) ResolveBoss(game, enc);
			if (enc.PlayerRelay.Count > 0) ResolveRelay(game, enc);
		}

		private static void ResolveBoss(Game game, RunEncounter enc)
		{
			Controller boss = game.Player2;
			if (boss.PlayState == PlayState.CONCEDED) return;
			if (TagTeamDeath(boss)) return; // family 08, George and Karl: the benched hero takes over (EncounterRules.F08.cs)
			for (int next = boss[FormIndex] + 1; next < enc.BossForms.Count; next++)
			{
				RunForm form = enc.BossForms[next];
				Hero hero = boss.Hero;
				bool dead = hero.ToBeDestroyed || hero.Health <= 0;
				if (form.HealthAtMost > 0 && !dead && hero.Health <= form.HealthAtMost) { SwapForm(boss, next); continue; }
				if (form.OnDeath && dead) SwapForm(boss, next);
				break;
			}
		}

		/// <summary>
		/// The boss becomes form <paramref name="index"/> now (a "script" form calls it from its card: Tag Team, Belnaara,
		/// Baz'hial; the Health and death forms come through <see cref="ResolveHeroForms"/>). A form with a hero replaces the
		/// hero entity (Controller.AddHeroAndPower: the old hero and power go to the set-aside zone; weapon and Armor stay); a
		/// form without one keeps the hero and replaces only the power. Health: a "healthAtMost" form with keepExcess keeps the
		/// remaining Health (the new hero's maximum is the form's Health); every other form starts at its full Health. Then the
		/// new power's trigger and aura start, and a form with a deck replaces the boss's deck (<see cref="ReplaceDeck"/>).
		/// </summary>
		public static void SwapForm(Controller boss, int index)
		{
			Game game = boss.Game;
			RunEncounter enc = Of(game);
			if (enc == null || index < 0 || index >= enc.BossForms.Count) throw new ArgumentOutOfRangeException(nameof(index));
			RunForm form = enc.BossForms[index];
			Hero old = boss.Hero;
			int remaining = old.Health, armor = old.Armor;
			bool dead = old.ToBeDestroyed || remaining <= 0;
			HeroPower oldPower = old.HeroPower;
			Card powerCard = form.Power ?? oldPower.Card;
			Hero hero = old;
			if (form.Hero != null)
			{
				DeactivatePower(oldPower);
				boss.AddHeroAndPower(form.Hero, powerCard);
				hero = boss.Hero;
				hero.Armor = armor;
				hero.CarryAttackStateFrom(old);
				foreach (IAura aura in game.Auras.ToArray()) Rebind(aura, boss, old, hero);
			}
			else
			{
				old.ToBeDestroyed = false;
				if (form.Power != null && form.Power.Id != oldPower.Card.Id)
				{
					DeactivatePower(oldPower);
					new ReplaceHeroPower(form.Power).Process(game, boss, old, null);
				}
			}
			int max = form.Health > 0 ? form.Health : hero.BaseHealth;
			bool keep = form.KeepExcess && !dead && remaining > 0 && remaining < max;
			hero.BaseHealth = max;
			hero.Damage = keep ? max - remaining : 0;
			boss.PlayState = PlayState.PLAYING;
			boss[FormIndex] = index;
			boss[FormChanges] = boss[FormChanges] + 1;
			ActivatePower(boss.Hero.HeroPower);
			if (form.Deck != null) ReplaceDeck(boss, form.Deck, form.Draw);
		}

		// PlayCard.PlayHeroCore's aura rebinding for a hero that changed outside a hero card play.
		private static void Rebind(IAura aura, Controller c, Hero previous, Hero replacement)
		{
			if (aura is MultiAura multiple)
				foreach (IAura child in multiple) Rebind(child, c, previous, replacement);
			if (aura is AdaptiveEffect adaptive) adaptive.RebindHero(previous, replacement);
			if (aura is Aura fixedAura && fixedAura.Type == AuraType.HERO && fixedAura.Owner.Controller == c)
			{
				fixedAura.DeApply(previous);
				fixedAura.EntityAdded(replacement);
			}
		}

		/// <summary>
		/// Replaces <paramref name="c"/>'s deck: the cards left go to the set-aside zone, <paramref name="cards"/> are created in
		/// the deck and shuffled with the game RNG, a deck fact tells the owner's own-deck ledger its new list
		/// (<see cref="DeckReplaced"/>), and <paramref name="draw"/> cards are drawn.
		/// </summary>
		public static void ReplaceDeck(Controller c, IReadOnlyList<Card> cards, int draw)
		{
			foreach (IPlayable card in c.DeckZone.ToArray()) c.SetasideZone.Add(c.DeckZone.Remove(card));
			foreach (Card card in cards)
			{
				if (c.DeckZone.IsFull) break;
				Entity.FromCard(c, card, zone: c.DeckZone);
			}
			c.DeckZone.Shuffle();
			DeckReplaced(c, cards.Select(card => card.Id));
			for (int i = 0; i < draw && !c.DeckZone.IsEmpty; i++) Generic.Draw(c);
		}

		// ------------------------------------------------------------------ relay (the run player's side)

		private static void ResolveRelay(Game game, RunEncounter enc)
		{
			Controller player = game.Player1;
			Hero hero = player.Hero;
			if (player.PlayState == PlayState.CONCEDED || !(hero.ToBeDestroyed || hero.Health <= 0)) return;
			int used = player[RelayIndex];
			// Family 24: a hero listed more than once (a Tombs explorer with its three finale decks) is one relay slot; which of its
			// entries enters is drawn by the game RNG when it enters (as the original's player choice is made then), so a boss's
			// fair-view simulation, whose clone has its own RNG, never learns it beforehand. One entry per hero: no draw.
			var heroes = enc.PlayerRelay.Select(h => h.Hero.Id).Distinct().ToList();
			if (used >= heroes.Count) return;
			player[RelayIndex] = used + 1;
			var entries = enc.PlayerRelay.Where(h => h.Hero.Id == heroes[used]).ToList();
			RunFightHero next = entries[0];
			if (entries.Count > 1)
			{
				game.OnRandomHappened(true);
				next = entries[game.Random.Next(entries.Count)];
			}
			EnterRelayHero(player, next);
		}

		/// <summary>
		/// The run player's next relay hero enters (the Tombs finale): hero and power are replaced, the hand and the deck are
		/// replaced by the hero's own list (old cards to the set-aside zone; <see cref="RelayDraw"/> cards drawn), the board, the
		/// Mana Crystals and the player enchantments stay, and the boss keeps everything. What the boss believed about the old
		/// hand and deck no longer holds (RunHelpers.BeliefReset).
		/// </summary>
		private static void EnterRelayHero(Controller player, RunFightHero next)
		{
			Game game = player.Game;
			Hero old = player.Hero;
			DeactivatePower(old.HeroPower);
			player.AddHeroAndPower(next.Hero, next.Power);
			Hero hero = player.Hero;
			foreach (IAura aura in game.Auras.ToArray()) Rebind(aura, player, old, hero);
			if (next.Health > 0) hero.BaseHealth = next.Health;
			hero.Damage = 0;
			player.PlayState = PlayState.PLAYING;
			foreach (IPlayable card in player.HandZone.ToArray()) player.SetasideZone.Add(player.HandZone.Remove(card));
			ReplaceDeck(player, next.Deck, Math.Min(RelayDraw, next.Deck.Count));
			RunHelpers.BeliefReset(player, RunBeliefZones.HandAndDeck);
			HeroPower power = hero.HeroPower;
			if (DungeonRunEncounters.NeedsActivation(power.Card.Id)) ActivatePower(power);
		}

		/// <summary>Cards a relay hero draws when it enters.</summary>
		public const int RelayDraw = 4;

		// ------------------------------------------------------------------ carried damage

		/// <summary>
		/// Match start (DungeonRunSetup.ApplyAfterStart): the boss starts in form <paramref name="bossForm"/> (the run layer
		/// already put that form's hero, power, Health and deck in the plan) with <paramref name="bossDamage"/> damage, the run
		/// hero with <paramref name="playerDamage"/> (ULDA_720 Battle Scars). Damage never kills: at least 1 Health is left.
		/// </summary>
		internal static void ApplyCarried(Game game, int bossForm, int bossDamage, int playerDamage)
		{
			if (bossForm > 0) game.Player2[FormIndex] = bossForm;
			if (bossDamage > 0) game.Player2.Hero.Damage = Math.Min(bossDamage, game.Player2.Hero.BaseHealth - 1);
			if (playerDamage > 0) game.Player1.Hero.Damage = Math.Min(playerDamage, game.Player1.Hero.BaseHealth - 1);
		}

		// ------------------------------------------------------------------ own-deck facts

		/// <summary>
		/// Records that <paramref name="c"/>'s deck is now exactly <paramref name="definitions"/> (a form's deck replacement, a
		/// relay hero's deck): the owner's own-deck ledger (plugin OwnDeckLedger.Sample) restarts from this list. Public to the
		/// owner only; the opponent's view of the deck is its count, as always.
		/// </summary>
		public static void DeckReplaced(Controller c, IEnumerable<string> definitions) =>
			c.Game.AddDeckFact(new RunDeckFact(c.PlayerId, c.Id, RunDeckFactKind.Replaced, c.Game.HistoryFacts.RecordCount, definitions));

		/// <summary>
		/// Records that a deck-wide rewrite left <paramref name="c"/>'s deck holding exactly <paramref name="definitions"/> (the
		/// boss-deck-rewrite family: Forlorn Lovers, P.O.G.O., Reductomara): consumed like <see cref="DeckReplaced"/>.
		/// </summary>
		public static void DeckRewritten(Controller c, IEnumerable<string> definitions) =>
			c.Game.AddDeckFact(new RunDeckFact(c.PlayerId, c.Id, RunDeckFactKind.Rewritten, c.Game.HistoryFacts.RecordCount, definitions));

		/// <summary>
		/// Records that the top cards of <paramref name="c"/>'s deck are known to its owner, top first (Erekem). Recorded for the
		/// families that need it; the own-deck ledger does not order the deck yet, so it keeps the multiset only.
		/// </summary>
		public static void DeckTopKnown(Controller c, IEnumerable<string> topFirst) =>
			c.Game.AddDeckFact(new RunDeckFact(c.PlayerId, c.Id, RunDeckFactKind.TopKnown, c.Game.HistoryFacts.RecordCount, topFirst));

		// ------------------------------------------------------------------ harvest

		/// <summary>
		/// What the run layer keeps from a finished (or abandoned) fight, read from the live game: the boss's remaining Health
		/// and form (persistent boss damage), the run hero's damage (Battle Scars), every hero the run player used (a played hero
		/// card or a relay hero replaces the hero entity; the old ones sit in the set-aside zone), and the no-loss flag.
		/// </summary>
		public static RunMatchEnd Harvest(Game game)
		{
			if (game == null) return null;
			Controller player = game.Player1, boss = game.Player2;
			var heroes = game.IdEntityDic.Values.OfType<Hero>().Where(h => h.Controller == player
					&& (ReferenceEquals(player.Hero, h) || h.Zone == player.SetasideZone))
				.OrderBy(h => h.Id).Select(h => h.Card.Id).ToList();
			bool bossDead = boss.Hero.ToBeDestroyed || boss.Hero.Health <= 0;
			return new RunMatchEnd(bossDead ? 0 : boss.Hero.Health, boss[FormIndex], boss[FormChanges],
				Math.Max(0, player.Hero.Damage), heroes, game[NoLoss] == 1, player[PlagueCount] + boss[PlagueCount]);
		}
	}
}

namespace SabberStoneCore.Model
{
	using SabberStoneCore.CardSets.DungeonRun;

	public partial class Game
	{
		// Not part of the snapshot schema (like the belief resets): the owner's fair view reads the live game's deck facts.
		[NonSerialized]
		private List<RunDeckFact> _runDeckFacts;

		/// <summary>The own-deck facts of this match (Actions.EncounterRules.DeckReplaced), in order.</summary>
		public IReadOnlyList<RunDeckFact> RunDeckFacts => _runDeckFacts ?? (IReadOnlyList<RunDeckFact>)Array.Empty<RunDeckFact>();

		internal void AddDeckFact(RunDeckFact fact) => (_runDeckFacts ?? (_runDeckFacts = new List<RunDeckFact>())).Add(fact);

		/// <summary>The encounter rules of this match (GameConfig.Encounter), or null outside an encounter match.</summary>
		public RunEncounter EncounterOrNull => _gameConfig?.Encounter;
	}
}
