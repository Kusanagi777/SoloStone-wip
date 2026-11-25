using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;

// Dungeon Run save (design R1, R11, R13): one sidecar per profile directory, dungeon-run.xml.
// Kept out of LocalProfileData so the strict V15 profile schema is untouched. Public for XmlSerializer.
// Version 2 (class, deck, power) adds fields only; a version-1 file loads as-is and DungeonRunMigration upgrades it.
// Version 3 (the shared draft catalog) adds fields only: a run's CatalogVersion (absent = 0, a legacy run pinned to the
// fixed bundles), its bucket takes and the cards (and grant cards) of each bundle offer.
// Version 4 (phases 4 and 5: the act/floor schedule) adds fields only: a run's ScheduleVersion (absent = 0, a legacy run on
// the fixed eight floors of run-v1.json "rules"), its schedule node, per-card records, active rules and the open tavern or
// modifier offer. Every new field is omitted at its default, so a legacy run re-saved at version 4 writes the same body as at
// version 3 (only the root Version differs).
// Version 5 (phase 7: Rumble Run board shrines and teammates) adds fields only: a run's BoardShrine (the shrine its starter
// deck names) and ShrineUpgrades, and the "teammate" and "shrine-upgrade" draft kinds; an archive entry adds BoardShrine and
// ShrineUpgrades. All are omitted at their defaults, so a run without a shrine writes the same body as at version 4.
// Version 6 (phase 8: encounters, anomalies and twists) adds fields only: a run's HeroSwitch (the fight-only heroes of the
// floor being fought) and HeroDamage (ULDA_720 Battle Scars), a Kind on an active rule and on the open rule offer, the
// encounter facts of a match summary, and the profile's BossDamage (persistent boss damage, per encounter). All are omitted
// at their defaults, so a run on an older schedule version writes the same body as at version 5.

// New members are appended: the existing values keep their serialized names.
public enum DungeonRunPhase { PreMatch, InMatch, TreasureDraft, BundleDraft, Victory, Defeat, Tavern, Modifier }

public enum DungeonRunOutcome { Win, Loss, Tie, Concede, TurnLimit, Fault }

/// <summary>One offer shown to the player and what they took (null until picked).</summary>
public sealed class DungeonRunDraft
{
    [XmlAttribute] public int Floor;
    /// <summary>
    /// "treasure" or "bundle"; phase 7 adds "shrine-upgrade" and "teammate" (a run with a board shrine), which reuse
    /// <see cref="Offered"/> and <see cref="Picked"/> and are picked on the treasure screen (DungeonRunPhase.TreasureDraft).
    /// </summary>
    [XmlAttribute] public string Kind;
    /// <summary>
    /// The offered ids: a treasure draft's in ordinal order; a legacy bundle draft's in ordinal order; a catalog bundle
    /// draft's in slot order (the same order as <see cref="Offers"/>).
    /// </summary>
    public List<string> Offered = new List<string>();
    /// <summary>
    /// A catalog run's bundle offers with their drawn cards, in slot order (save version 3). Empty for a treasure draft and a
    /// legacy bundle draft, whose bundles are fixed in the data. A pick takes the stored cards, so a save and resume (or a
    /// data change) never redraws an offer.
    /// </summary>
    public List<DungeonRunOffer> Offers = new List<DungeonRunOffer>();
    [XmlAttribute] public string Picked;
    /// <summary>A treasure draft and a legacy bundle draft write no Offers element.</summary>
    public bool ShouldSerializeOffers() => Offers != null && Offers.Count > 0;
    /// <summary>
    /// An act run's bundle draft that keeps fewer than an offer's three cards (schedule act "draft.takes", e.g. 2): how many.
    /// 0 (omitted) is a whole-bundle pick (three cards), as on every legacy draft and an act draft whose takes is 3.
    /// </summary>
    [XmlAttribute] public int Takes;
    public bool ShouldSerializeTakes() => Takes != 0;
    /// <summary>A partial pick of a <see cref="Takes"/> draft: the offer the first card came from (null, omitted, until then).</summary>
    [XmlAttribute] public string Choosing;
    /// <summary>The cards kept so far from <see cref="Choosing"/>, in pick order (a save between two picks keeps them).</summary>
    public List<string> Chosen = new List<string>();
    public bool ShouldSerializeChosen() => Chosen != null && Chosen.Count > 0;
    /// <summary>The cards one pick keeps: <see cref="Takes"/>, or 3 for a whole-bundle draft.</summary>
    [XmlIgnore] public int CardsPerPick => Takes > 0 ? Takes : 3;

    /// <summary>The stored offer of <paramref name="id"/>, or null (a legacy draft, or not offered).</summary>
    internal DungeonRunOffer Offer(string id) => Offers.FirstOrDefault(o => o.Id == id);
}

/// <summary>One catalog bundle offer: the entry, its label and primary theme, and the three cards drawn for it.</summary>
public sealed class DungeonRunOffer
{
    [XmlAttribute] public string Id;
    [XmlAttribute] public string Label;
    [XmlAttribute] public string Theme;
    /// <summary>"fixed" (a SoloStone bundle: taken once) or "pool" (an original bucket: taken up to draft.bucketMaxTakes).</summary>
    [XmlAttribute] public string Kind;
    /// <summary>The adventure short tag of the entry's provenance ("DR", "MH", "RR", "DH", "ToT", "SoloStone").</summary>
    [XmlAttribute] public string Source;
    public List<string> Cards = new List<string>();
    /// <summary>
    /// The offered cards that are grants of the entry (off-class, legal inside it only: Monster Hunt Shaw and Tess), so the
    /// pick's recheck needs no current candidate. Not written when empty.
    /// </summary>
    public List<string> Grants = new List<string>();
    public bool ShouldSerializeGrants() => Grants != null && Grants.Count > 0;
}

/// <summary>How many times a catalog run took one pool entry (a list: XmlSerializer cannot write a dictionary).</summary>
public sealed class DungeonRunTake
{
    [XmlAttribute] public string Id;
    [XmlAttribute] public int Count;
}

public sealed class DungeonRunMatchSummary
{
    [XmlAttribute] public int Floor;
    [XmlAttribute] public string Boss;
    [XmlAttribute] public DungeonRunOutcome Outcome;
    [XmlAttribute] public int Attempt;
    /// <summary>The boss's encounter family (run-v1.json boss "family"; a phase-8 hook for twists and anomalies). Omitted when null.</summary>
    [XmlAttribute] public string Family;
    // ---- phase 8 (save version 6): what the fight's harvest left (RunMatchEnd). Each is omitted at its default.
    /// <summary>The boss's Health when the fight ended without a win (a phased boss's persistent damage); omitted when 0.</summary>
    [XmlAttribute] public int BossRemaining;
    public bool ShouldSerializeBossRemaining() => BossRemaining != 0;
    /// <summary>The boss's form index when the fight ended; omitted when 0.</summary>
    [XmlAttribute] public int BossForm;
    public bool ShouldSerializeBossForm() => BossForm != 0;
    /// <summary>The hero cards the run player's hero was this fight, comma-separated, when it changed (a hero card or a relay); omitted otherwise.</summary>
    [XmlAttribute] public string HeroesUsed;
    /// <summary>The global rules active in the fight, comma-separated; omitted when none.</summary>
    [XmlAttribute] public string Rules;
    /// <summary>The fight ended with no loss and no boss kill (a script ended it: Treasure Vault); omitted when false.</summary>
    [XmlAttribute] public bool NoLoss;
    public bool ShouldSerializeNoLoss() => NoLoss;
    // ---- step 9 (still save version 6, the NoLoss pattern): each is omitted at its default, so no earlier summary changes.
    /// <summary>
    /// ULDA_111 Hearthstone: the fight was escaped (<see cref="Outcome"/> Win with this mark: no major credit, no kill credit,
    /// no drafts); omitted when false.
    /// </summary>
    [XmlAttribute] public bool Escaped;
    public bool ShouldSerializeEscaped() => Escaped;
    /// <summary>GILA_911 Rewind, GILA_BOSS_48t: the fight was restarted (<see cref="Outcome"/> Fault with this mark); omitted when false.</summary>
    [XmlAttribute] public bool Restarted;
    public bool ShouldSerializeRestarted() => Restarted;
}

/// <summary>An active treasure card that grows: where it sits and when it was last upgraded.</summary>
public sealed class DungeonRunUpgrade
{
    [XmlAttribute] public string Key;
    [XmlAttribute] public int SinceFloor;
    /// <summary>An act run's shell card record (<see cref="DungeonRunCard.Uid"/>); 0 (omitted) on a legacy run, which finds the shell by id.</summary>
    [XmlAttribute] public int Uid;
    public bool ShouldSerializeUid() => Uid != 0;
    /// <summary>
    /// What the upgrade swaps (phase 6): "deck" (the card record <see cref="Uid"/>; null, omitted, means deck, as on every
    /// earlier save), "power" (the run's hero power) or "passive" (a passive treasure of <see cref="DungeonRunState.Treasures"/>).
    /// </summary>
    [XmlAttribute] public string Slot;
    /// <summary>Won fights counted toward a "bossWins:N" upgrade (phase 6); 0 (omitted) otherwise.</summary>
    [XmlAttribute] public int Progress;
    public bool ShouldSerializeProgress() => Progress != 0;
}

/// <summary>
/// One card of an act run's deck (save version 4): a stable uid, the card id, where it came from and its persistent changes.
/// <see cref="DungeonRunState.Cards"/> is authoritative on an act run; <see cref="DungeonRunState.Deck"/> mirrors its ids.
/// </summary>
public sealed class DungeonRunCard
{
    [XmlAttribute] public int Uid;
    [XmlAttribute] public string Id;
    /// <summary>starter, draft, treasure, tavern, copy or bombastic (<see cref="DungeonRunDeck"/> origin constants).</summary>
    [XmlAttribute] public string Origin;
    /// <summary>The uid this card copies; 0 (omitted) for an original.</summary>
    [XmlAttribute] public int CopyOf;
    public bool ShouldSerializeCopyOf() => CopyOf != 0;
    [XmlAttribute] public int AddedFloor;
    /// <summary>The opening-hand flag (a phase-5 tavern change); omitted when false.</summary>
    [XmlAttribute] public bool Opening;
    public bool ShouldSerializeOpening() => Opening;
    /// <summary>Client enchantment card ids applied to the card, in application order (phase 5).</summary>
    [XmlElement("Mod")] public List<string> Mods = new List<string>();
    public bool ShouldSerializeMods() => Mods != null && Mods.Count > 0;
    /// <summary>
    /// The card's run log (phase 6: what DALA_718 recasts, what DALA_719 summons), one line per entry, oldest first; appended
    /// after a won fight from the match's slot logs (RunMatchResult.SlotLogAppends). Omitted when empty.
    /// </summary>
    [XmlElement("Log")] public List<string> Log = new List<string>();
    public bool ShouldSerializeLog() => Log != null && Log.Count > 0;
}

/// <summary>A one-fight bonus a treasure granted (ULDA_504 Tea Time): applied to the next fight, then cleared.</summary>
public sealed class DungeonRunNextFight
{
    [XmlAttribute] public int Mana;
    [XmlAttribute] public int Draws;
}

/// <summary>A schedule modifier the run took (schedule-v1.json "modifiers"): active for the rest of act <see cref="Act"/>.</summary>
public sealed class DungeonRunRule
{
    [XmlAttribute] public string Id;
    /// <summary>The act (1-based) the rule lasts for.</summary>
    [XmlAttribute] public int Act;
    /// <summary>The node it was taken at.</summary>
    [XmlAttribute] public int FromNode;
    /// <summary>
    /// Phase 8: null (omitted) for a schedule modifier; "anomaly" or "twist" for a global rule (schedule-v1.json "rules"), whose
    /// <see cref="Id"/> is the rule's card id. A run-scope rule has <see cref="Act"/> 0 and lasts the whole run; a rolled twist
    /// has <see cref="FromNode"/> -1.
    /// </summary>
    [XmlAttribute] public string Kind;
}

/// <summary>One hero the run player may become for one fight (a hero card or a relay hero).</summary>
public sealed class DungeonRunHeroOption
{
    [XmlAttribute] public string Hero;
    [XmlAttribute] public string Power;
    /// <summary>A relay hero's deck id (DungeonRunData.BossDecks); omitted for a hero card.</summary>
    [XmlAttribute] public string Deck;
    [XmlAttribute] public int Health;
    public bool ShouldSerializeHealth() => Health != 0;
}

/// <summary>
/// Temporary hero switching (phase 8): the heroes the run player may become in the fight of <see cref="Floor"/>, and no
/// longer. Set by BeginMatch for a fight that offers them, kept through a fault (the floor is restaged), cleared by every
/// other outcome. The run's own Class, Power, Deck and Cards are never written by a switch.
/// </summary>
public sealed class DungeonRunHeroSwitch
{
    [XmlAttribute] public int Floor;
    /// <summary>"hunters" (hero cards played from hand: Hagatha) or "relay" (the next hero enters when one dies: the Tombs finale).</summary>
    [XmlAttribute] public string Kind;
    public List<DungeonRunHeroOption> Options = new List<DungeonRunHeroOption>();
    public const string KindHunters = "hunters", KindRelay = "relay";
}

/// <summary>Persistent boss damage (phase 8): what a lost fight left of one encounter's Health, kept across runs.</summary>
public sealed class DungeonRunBossDamage
{
    /// <summary>The encounter's run id (DungeonRunBoss.Id).</summary>
    [XmlAttribute] public string Boss;
    /// <summary>Its Health left, at least 1 and below its full Health.</summary>
    [XmlAttribute] public int Remaining;
    /// <summary>Lost fights against it since the record began.</summary>
    [XmlAttribute] public int Attempts;
}

/// <summary>
/// The run's starting loadout (phase 6): the named hero row it began on and the signature treasure it picked (Tombs). Null on
/// every class-row run, and on every run before phase 6.
/// </summary>
public sealed class DungeonRunLoadout
{
    [XmlAttribute] public string Id;
    public List<string> Signature = new List<string>();
    public bool ShouldSerializeSignature() => Signature != null && Signature.Count > 0;
}

/// <summary>An open modifier node: the modifiers offered (in draw order), one of which the player takes.</summary>
public sealed class DungeonRunModifierOffer
{
    [XmlAttribute] public int Node;
    /// <summary>
    /// Phase 8: null (omitted) for a modifier node; "anomaly" for an anomaly node, whose <see cref="Offered"/> holds the rolled
    /// rule card ids and then "none" (DungeonRunGlobalRule.None: take no rule).
    /// </summary>
    [XmlAttribute] public string Kind;
    public List<string> Offered = new List<string>();
}

/// <summary>One tavern purchase: the option and the targets it was resolved on (card uids or ids, as text).</summary>
public sealed class DungeonRunTavernBuy
{
    [XmlAttribute] public string Option;
    public List<string> Targets = new List<string>();
    public bool ShouldSerializeTargets() => Targets != null && Targets.Count > 0;
}

/// <summary>A tavern purchase waiting for its target: the option and the candidates offered (card uids or ids, as text).</summary>
public sealed class DungeonRunTavernPending
{
    [XmlAttribute] public string Option;
    /// <summary>A two-step purchase's first choice (Hiring Replacements: the friendly minion's uid); 0 (omitted) before it.</summary>
    [XmlAttribute] public int Target;
    public bool ShouldSerializeTarget() => Target != 0;
    public List<string> Candidates = new List<string>();
}

/// <summary>
/// The random choices one tavern option drew this visit (a Discover's three cards, a sample, Party of Four's minions, Hiring
/// Replacements' discovered replacements), kept until the tavern closes: buying the option again after Back, or choosing
/// another Hiring Replacements target, shows the same cards, so Back is never a free reroll.
/// </summary>
public sealed class DungeonRunTavernDraw
{
    [XmlAttribute] public string Option;
    /// <summary>0: the purchase's own choices; 1: Hiring Replacements' second step (the replacements). Omitted when 0.</summary>
    [XmlAttribute] public int Stage;
    public bool ShouldSerializeStage() => Stage != 0;
    public List<string> Candidates = new List<string>();
}

/// <summary>
/// An open Bob's tavern node (phase 5 fills the options): gold, the random step counter, both boards, the offers and what was
/// bought. Saved after every change, so a resume continues exactly where it was.
/// </summary>
public sealed class DungeonRunTavern
{
    [XmlAttribute] public int Node;
    [XmlAttribute] public int Gold;
    /// <summary>Random actions taken so far (the StableSeed step of the tavern stream).</summary>
    [XmlAttribute] public int Step;
    [XmlAttribute] public bool Rerolled;
    /// <summary>Four deck minion uids, in board order.</summary>
    public List<int> Friendly = new List<int>();
    /// <summary>Bob's four card ids, in board order.</summary>
    public List<string> Bob = new List<string>();
    /// <summary>The tavern option ids on offer.</summary>
    public List<string> Offers = new List<string>();
    public List<DungeonRunTavernBuy> Bought = new List<DungeonRunTavernBuy>();
    /// <summary>A purchase waiting for its target, or null.</summary>
    public DungeonRunTavernPending Pending;
    /// <summary>The random choices drawn this visit, per option (see <see cref="DungeonRunTavernDraw"/>); omitted when empty.</summary>
    public List<DungeonRunTavernDraw> Draws = new List<DungeonRunTavernDraw>();
    public bool ShouldSerializeDraws() => Draws != null && Draws.Count > 0;
}

public sealed class DungeonRunState
{
    /// <summary>The run data's dataVersion when the run began (run-v1.json "dataVersion").</summary>
    [XmlAttribute] public string DataVersion;
    /// <summary>
    /// The draft catalog the run drafts from: 0 = legacy (the 52 fixed SoloStone bundles, the pre-catalog draft and the
    /// closed Secret prior, byte for byte; every run of a save before version 3); 1 or more = the shared draft catalog
    /// (catalog-v1.json "catalogVersion"). Stamped by NewRun and never changed.
    /// </summary>
    [XmlAttribute] public int CatalogVersion;
    /// <summary>A legacy run (0) writes no CatalogVersion attribute, so its file reads as before.</summary>
    public bool ShouldSerializeCatalogVersion() => CatalogVersion != 0;
    public bool ShouldSerializeBucketTakes() => BucketTakes != null && BucketTakes.Count > 0;
    /// <summary>The run seed; shown as 16 hex digits. Every random choice derives from it (design F1).</summary>
    [XmlAttribute] public long Seed;
    [XmlAttribute] public bool TypedSeed;
    /// <summary>
    /// Build Deck (still save version 6, the step-9 pattern): the player built the starter (<see cref="StarterDeck"/> is the row's
    /// build deck, DungeonRunClass.BuildDeck); the run counts as every deck of its row for treasure scope and deck tags. Omitted
    /// when false, so no earlier run's body changes.
    /// </summary>
    [XmlAttribute] public bool BuiltDeck;
    public bool ShouldSerializeBuiltDeck() => BuiltDeck;
    /// <summary>The run's class (an engine CardClass name, "MAGE"); set on every version-2 run.</summary>
    [XmlAttribute] public string Class;
    /// <summary>The starter deck the run began with (pool "starterDecks" id).</summary>
    [XmlAttribute] public string StarterDeck;
    /// <summary>Version-1 hero id; kept on migrated runs for the archive and logs. New runs leave it null.</summary>
    [XmlAttribute] public string Hero;
    /// <summary>Chosen hero power (a card id or content key); null only on a version-1 shrine run.</summary>
    [XmlAttribute] public string Power;
    /// <summary>Version-1 shrine base key (the tier is added per floor). New runs never set it.</summary>
    [XmlAttribute] public string Shrine;
    /// <summary>Version-1 signature card. New runs never set it.</summary>
    [XmlAttribute] public string Signature;
    [XmlAttribute] public int Floor = 1;
    [XmlAttribute] public DungeonRunPhase Phase = DungeonRunPhase.PreMatch;
    /// <summary>Times the current floor was staged; a fault or crash restages it with the same seed.</summary>
    [XmlAttribute] public int Attempt;

    /// <summary>
    /// The act/floor schedule the run follows: 0 = legacy (the fixed eight floors of run-v1.json "rules", byte for byte; every
    /// run of a save before version 4); 1 or more = schedule-v1.json "scheduleVersion". Stamped by NewRun and never changed.
    /// </summary>
    [XmlAttribute] public int ScheduleVersion;
    public bool ShouldSerializeScheduleVersion() => ScheduleVersion != 0;
    /// <summary>An act run's schedule id (schedule-v1.json "schedules" key, "acts-v1"); null (omitted) on a legacy run.</summary>
    [XmlAttribute] public string Schedule;
    /// <summary>
    /// An act run's index into its schedule's flattened node list (DungeonRunSchedule.Nodes). <see cref="Floor"/> stays the
    /// global combat floor (1..N over the whole run); act and floor-in-act are derived from the node, never stored.
    /// </summary>
    [XmlAttribute] public int Node;
    public bool ShouldSerializeNode() => ScheduleVersion != 0;
    /// <summary>The next card record uid (<see cref="DungeonRunCard.Uid"/>); omitted when 0.</summary>
    [XmlAttribute] public int NextUid;
    public bool ShouldSerializeNextUid() => NextUid != 0;
    /// <summary>Permanent extra hero Health (tavern Good Food, phase 5); omitted when 0.</summary>
    [XmlAttribute] public int HealthBonus;
    public bool ShouldSerializeHealthBonus() => HealthBonus != 0;
    /// <summary>Act majors this run has beaten; added to the profile at Finish (unlock condition majorsCleared). Omitted when 0.</summary>
    [XmlAttribute] public int MajorsCleared;
    public bool ShouldSerializeMajorsCleared() => MajorsCleared != 0;
    /// <summary>An act run's deck as card records, authoritative; <see cref="Deck"/> mirrors their ids (DungeonRunDeck.Sync).</summary>
    public List<DungeonRunCard> Cards = new List<DungeonRunCard>();
    public bool ShouldSerializeCards() => Cards != null && Cards.Count > 0;
    /// <summary>Schedule modifiers taken, each active for the rest of its act.</summary>
    public List<DungeonRunRule> ActiveRules = new List<DungeonRunRule>();
    public bool ShouldSerializeActiveRules() => ActiveRules != null && ActiveRules.Count > 0;
    /// <summary>The open tavern node, or null (XmlSerializer omits null).</summary>
    public DungeonRunTavern Tavern;
    /// <summary>The open modifier node, or null.</summary>
    public DungeonRunModifierOffer ModifierOffer;
    /// <summary>The named hero loadout the run began with (phase 6), or null (a class-row run).</summary>
    public DungeonRunLoadout Loadout;
    /// <summary>
    /// A hero power that replaced the run's for the rest of the run (phase 6, ULDA_306: a won fight's
    /// RunMatchResult.PowerOverride); null (omitted) when none. <see cref="Power"/> keeps the power the run began with.
    /// </summary>
    [XmlAttribute] public string PowerOverride;
    /// <summary>The bonus the next fight starts with (phase 6, ULDA_504), or null (omitted); cleared once that fight is recorded.</summary>
    public DungeonRunNextFight NextFight;
    /// <summary>
    /// The run's board shrine (phase 7): the base Rumble Run shrine card id its starter deck names (run-v1.json deck "shrine",
    /// e.g. TRLA_115), set by NewRun only on a new run of schedule version 2 or later whose shrine the engine scripts. Null
    /// (omitted) on every other run. Never the version-1 Pilgrim <see cref="Shrine"/> power and never a hero power: it lands
    /// on the board at Start of Game (RunMatchPlan.PlayerShrine).
    /// </summary>
    [XmlAttribute] public string BoardShrine;
    public bool ShouldSerializeBoardShrine() => BoardShrine != null;
    /// <summary>
    /// Shrine upgrade ids (TRLA_801..809) in pick order (phase 7, the "shrine-upgrade" draft); omitted when empty. Kept apart
    /// from <see cref="Treasures"/>, so treasure offers, the validator's treasure checks and the archive counts do not change.
    /// </summary>
    public List<string> ShrineUpgrades = new List<string>();
    public bool ShouldSerializeShrineUpgrades() => ShrineUpgrades != null && ShrineUpgrades.Count > 0;
    /// <summary>A run with a board shrine (phase 7): teammate and shrine-upgrade drafts open only on such a run.</summary>
    [XmlIgnore] public bool HasBoardShrine => BoardShrine != null;
    /// <summary>
    /// Phase 8: the fight-only heroes of the floor being fought (Hagatha's hero cards, the Tombs finale's relay), or null
    /// (omitted): outside that floor's fight, and on every run before phase 8.
    /// </summary>
    public DungeonRunHeroSwitch HeroSwitch;
    /// <summary>
    /// Phase 8: damage the run's own hero carries from fight to fight while ULDA_720 Battle Scars is active; 0 (omitted)
    /// otherwise. Written after a won fight the run's own hero finished.
    /// </summary>
    [XmlAttribute] public int HeroDamage;
    public bool ShouldSerializeHeroDamage() => HeroDamage != 0;
    /// <summary>
    /// Phase 8 family 24: damage banked for the run's next Plague Lord fight (a persistentDamage encounter) by ULDA_115
    /// "Gnomebliterator", 10 plus the cast's Spell Damage per cast in the run's other fights, by either player ("Also deal 10
    /// damage to the Plague Lord when you fight it"; a cast inside a Plague Lord fight deals it to the lord at once);
    /// that fight's start spends it (DungeonRun.PersistentRemaining) and its end clears it. 0 (omitted) otherwise.
    /// </summary>
    [XmlAttribute] public int PlagueLordDamage;
    public bool ShouldSerializePlagueLordDamage() => PlagueLordDamage != 0;
    /// <summary>
    /// The close-out (family 25): the starting Hero Powers the run's row offered when it began (its powers whose unlock the
    /// profile held then), space-separated in row order, for ULDA_038 Explorer Retraining ("Choose a new starting Hero Power",
    /// a tavern option of encounter schedules: DungeonRun.RetrainPowers). Recorded by NewRun on a run of schedule stamp 3 and
    /// above when two or more were open; null (omitted) otherwise, and on every run saved before the close-out.
    /// </summary>
    [XmlAttribute] public string PowerChoices;
    public bool ShouldSerializePowerChoices() => PowerChoices != null;
    // ---- step 9 (still save version 6): guarded attributes, each omitted at its default, so every earlier run writes the same body.
    /// <summary>Gold banked for the next tavern (step 9 D5: a bundle draft skipped for room); 0 (omitted) otherwise.</summary>
    [XmlAttribute] public int BankedGold;
    public bool ShouldSerializeBankedGold() => BankedGold != 0;
    /// <summary>
    /// Step 9 D13, match scope: how many times the floor being fought was restarted (GILA_911 Rewind, GILA_BOSS_48t); 0
    /// (omitted) before the first restart and once the floor is decided.
    /// </summary>
    [XmlAttribute] public int RestartIndex;
    public bool ShouldSerializeRestartIndex() => RestartIndex != 0;
    /// <summary>Step 9 D13, match scope: the restart sources used on the floor being fought (card ids, comma-separated, for the per-source caps); null (omitted) otherwise.</summary>
    [XmlAttribute] public string RestartSources;
    public bool ShouldSerializeRestartSources() => RestartSources != null;
    /// <summary>Step 9 D13, match scope: the damage the run hero carries into the restarted game (GILA_BOSS_48t Alternate Timeline); 0 (omitted) otherwise.</summary>
    [XmlAttribute] public int RestartCarriedDamage;
    public bool ShouldSerializeRestartCarriedDamage() => RestartCarriedDamage != 0;
    /// <summary>
    /// Step 9 D13 (lane X), match scope: the game turns the restarted games of the floor being fought took (the cumulative
    /// turn budget: a floor's games take at most twice the fight's TurnCap); 0 (omitted) otherwise.
    /// </summary>
    [XmlAttribute] public int RestartTurns;
    public bool ShouldSerializeRestartTurns() => RestartTurns != 0;

    /// <summary>The run deck as card ids (pool cards and active-treasure shells).</summary>
    public List<string> Deck = new List<string>();
    /// <summary>Passive treasure content keys, in pick order.</summary>
    public List<string> Treasures = new List<string>();
    /// <summary>Active treasure content keys held (for offers and upgrades).</summary>
    public List<string> ActiveTreasures = new List<string>();
    public List<DungeonRunUpgrade> Upgrades = new List<DungeonRunUpgrade>();
    public List<string> BossSequence = new List<string>();
    /// <summary>Fixed bundles taken (each at most once).</summary>
    public List<string> TakenBundles = new List<string>();
    /// <summary>A catalog run's takes of pool entries (an entry is offered while its count is below draft.bucketMaxTakes).</summary>
    public List<DungeonRunTake> BucketTakes = new List<DungeonRunTake>();
    /// <summary>"bundleId@floor": offered and not taken; offerable again from that floor on.</summary>
    public List<string> BundleCooldowns = new List<string>();
    /// <summary>Theme of every drafted card, for bundle affinity (design F5).</summary>
    public List<string> DraftedThemes = new List<string>();
    [XmlAttribute] public int DraftsWithoutDominant;
    public List<DungeonRunDraft> Drafts = new List<DungeonRunDraft>();
    public List<DungeonRunMatchSummary> Matches = new List<DungeonRunMatchSummary>();
    /// <summary>Unlock keys this run earned when it ended, for the summary page.</summary>
    public List<string> UnlockedThisRun = new List<string>();

    [XmlIgnore] public bool Finished => Phase == DungeonRunPhase.Victory || Phase == DungeonRunPhase.Defeat;
    [XmlIgnore] public DungeonRunDraft OpenDraft => Drafts.LastOrDefault(d => d.Picked == null);
    [XmlIgnore] public string SeedText => Seed.ToString("X16");
    [XmlIgnore] public bool IsCatalogRun => CatalogVersion >= 1;
    /// <summary>A run on an act/floor schedule (save version 4 and later); false for every legacy run.</summary>
    [XmlIgnore] public bool IsActRun => ScheduleVersion >= 1;

    /// <summary>How many times this run took pool entry <paramref name="id"/>.</summary>
    internal int TakesOf(string id) => BucketTakes.FirstOrDefault(t => t.Id == id)?.Count ?? 0;

    internal void AddTake(string id)
    {
        var take = BucketTakes.FirstOrDefault(t => t.Id == id);
        if (take == null) BucketTakes.Add(take = new DungeonRunTake { Id = id });
        take.Count++;
    }
}

/// <summary>Per-class stats (the element keeps its version-1 name). Hero and Archetype are version-1 fields: null once merged.</summary>
public sealed class DungeonRunHeroStats
{
    [XmlAttribute] public string Class;
    [XmlAttribute] public string Hero;
    [XmlAttribute] public string Archetype;
    [XmlAttribute] public int Runs;
    [XmlAttribute] public int Wins;
    [XmlAttribute] public int BestFloor;
    /// <summary>Act majors beaten by random-seed act runs of this class (unlock condition majorsCleared); omitted when 0.</summary>
    [XmlAttribute] public int MajorsCleared;
    public bool ShouldSerializeMajorsCleared() => MajorsCleared != 0;
}

public sealed class DungeonRunArchiveEntry
{
    [XmlAttribute] public string Class;
    [XmlAttribute] public string StarterDeck;
    [XmlAttribute] public string Power;
    /// <summary>Version-1 hero id (old entries, and runs migrated from version 1).</summary>
    [XmlAttribute] public string Hero;
    [XmlAttribute] public string Seed;
    [XmlAttribute] public bool TypedSeed;
    /// <summary>Build Deck: the run's starter was built (DungeonRunState.BuiltDeck); omitted when false.</summary>
    [XmlAttribute] public bool BuiltDeck;
    public bool ShouldSerializeBuiltDeck() => BuiltDeck;
    [XmlAttribute] public int FloorReached;
    [XmlAttribute] public bool Won;
    [XmlAttribute] public string LastBoss;
    /// <summary>The run's board shrine (phase 7), next to the legacy shrine in <see cref="Power"/>; omitted when null.</summary>
    [XmlAttribute] public string BoardShrine;
    public bool ShouldSerializeBoardShrine() => BoardShrine != null;
    public List<string> Treasures = new List<string>();
    /// <summary>The run's shrine upgrades in pick order (phase 7), kept apart from <see cref="Treasures"/>; omitted when empty.</summary>
    public List<string> ShrineUpgrades = new List<string>();
    public bool ShouldSerializeShrineUpgrades() => ShrineUpgrades != null && ShrineUpgrades.Count > 0;
}

/// <summary>Everything that persists across runs (design R13): stats, unlocks, archive.</summary>
public sealed class DungeonRunProfile
{
    /// <summary>Act majors beaten by random-seed act runs, every class (the per-class value is on the stats row). Omitted when 0.</summary>
    [XmlAttribute] public int MajorsCleared;
    public bool ShouldSerializeMajorsCleared() => MajorsCleared != 0;
    public List<DungeonRunHeroStats> Heroes = new List<DungeonRunHeroStats>();
    public List<string> Unlocked = new List<string>();
    public List<DungeonRunArchiveEntry> Archive = new List<DungeonRunArchiveEntry>();
    /// <summary>
    /// Phase 8: persistent boss damage, one record per encounter that carries it (flag "persistentDamage": the Plague Lords).
    /// Profile scope, because a lost fight ends the run: the next random-seed run that meets the boss starts it at this Health.
    /// A typed-seed run neither reads nor writes it. Omitted when empty.
    /// </summary>
    public List<DungeonRunBossDamage> BossDamage = new List<DungeonRunBossDamage>();
    public bool ShouldSerializeBossDamage() => BossDamage != null && BossDamage.Count > 0;
    /// <summary>The record of encounter <paramref name="bossId"/>, or null.</summary>
    internal DungeonRunBossDamage DamageOf(string bossId) => BossDamage?.FirstOrDefault(d => d.Boss == bossId);
    [XmlIgnore] internal const int ArchiveCap = 200;

    /// <summary>The stats row of a class, created on first use.</summary>
    internal DungeonRunHeroStats StatsFor(string cls)
    {
        var stats = Heroes.FirstOrDefault(h => h.Class == cls);
        if (stats == null) Heroes.Add(stats = new DungeonRunHeroStats { Class = cls });
        return stats;
    }
}

[XmlRoot("DungeonRun")]
public sealed class DungeonRunSave
{
    [XmlAttribute] public int Version = CurrentVersion;
    /// <summary>
    /// 2: classes, starter decks and class-scoped stats (DungeonRunMigration upgrades 1). 3: the shared draft catalog (a run's
    /// CatalogVersion, bucket takes and stored bundle offers; DungeonRunMigration upgrades 2). An older plugin refuses a
    /// version-3 file rather than drop its stored offers. 4: the act/floor schedule (a run's ScheduleVersion, node, card
    /// records, active rules, tavern and modifier offers; DungeonRunMigration upgrades 3). An older plugin refuses a version-4 file.
    /// 5: board shrines and teammates (a run's BoardShrine and ShrineUpgrades, the teammate and shrine-upgrade draft kinds;
    /// DungeonRunMigration upgrades 4 as the identity). An older plugin refuses a version-5 file rather than strip them.
    /// 6: encounters, anomalies and twists (a run's HeroSwitch and HeroDamage, rule kinds, a match summary's encounter facts,
    /// the profile's BossDamage; DungeonRunMigration upgrades 5 as the identity). An older plugin refuses a version-6 file.
    /// </summary>
    [XmlIgnore] public const int CurrentVersion = 6;
    [XmlIgnore] public const int OldestVersion = 1;
    /// <summary>The version the file had when it was loaded; drives the one-time backup of an upgraded file.</summary>
    [XmlIgnore] public int LoadedVersion = CurrentVersion;
    public DungeonRunProfile Profile = new DungeonRunProfile();
    public DungeonRunState Active;
}

/// <summary>Reads and writes the sidecar with the same temp-then-replace pattern as LocalProfile.Commit.</summary>
internal static class DungeonRunStore
{
    internal const string FileName = "dungeon-run.xml";
    /// <summary>The untouched version-1 file, copied once before an upgraded save is first written.</summary>
    internal const string V1BackupName = "dungeon-run.v1.xml";
    /// <summary>The one-time copy of an older file (version <paramref name="version"/>), made before an upgraded save is first written.</summary>
    internal static string BackupName(int version) => "dungeon-run.v" + version + ".xml";
    private static readonly XmlSerializer Serializer = new XmlSerializer(typeof(DungeonRunSave));

    internal static DungeonRunSave Load(string profileDirectory)
    {
        string path = Path.Combine(profileDirectory, FileName);
        if (!File.Exists(path)) return new DungeonRunSave();
        using (var stream = File.OpenRead(path))
        {
            var save = (DungeonRunSave)Serializer.Deserialize(stream);
            // An older version loads as-is (the runtime upgrades it once the rules are loaded); a newer one is refused, so a
            // downgrade never overwrites data it cannot read.
            if (save.Version < DungeonRunSave.OldestVersion || save.Version > DungeonRunSave.CurrentVersion)
                throw new InvalidDataException($"{FileName}: version {save.Version}, expected {DungeonRunSave.OldestVersion} to {DungeonRunSave.CurrentVersion}");
            save.LoadedVersion = save.Version;
            return save;
        }
    }

    /// <summary>A deep copy through the same serializer as the file, so a failed change can be rolled back exactly.</summary>
    internal static DungeonRunSave Clone(DungeonRunSave save)
    {
        using (var stream = new MemoryStream())
        {
            Serializer.Serialize(stream, save);
            stream.Position = 0;
            return (DungeonRunSave)Serializer.Deserialize(stream);
        }
    }

    /// <summary>
    /// Rolls <paramref name="save"/> back to <paramref name="snapshot"/> (a <see cref="Clone"/> taken before a change): its
    /// profile, run in progress and version. A change can raise the version (the first commit of an upgraded file), so a
    /// rollback that kept it would leave memory a version ahead of the file. <see cref="DungeonRunSave.LoadedVersion"/> is not
    /// touched: only a successful <see cref="Save"/> moves it. Used by the runtime's Mutate and RecordFightOutcome (step 9 D6).
    /// </summary>
    internal static void Restore(DungeonRunSave save, DungeonRunSave snapshot)
    {
        save.Profile = snapshot.Profile;
        save.Active = snapshot.Active;
        save.Version = snapshot.Version;
    }

    internal static void Save(string profileDirectory, DungeonRunSave save)
    {
        // An act run's Deck mirrors its card records (DungeonRunDeck.Sync): a mismatch is refused before anything is written,
        // so the runtime's Mutate rolls the change back and the file keeps the last consistent run.
        string mirror = save?.Active != null ? DungeonRunDeck.MirrorError(save.Active) : null;
        if (mirror != null) throw new InvalidDataException(FileName + ": " + mirror);
        Directory.CreateDirectory(profileDirectory);
        string path = Path.Combine(profileDirectory, FileName);
        // The file as it was loaded, copied once (dungeon-run.v1.xml, dungeon-run.v2.xml) before the first write of a newer version.
        string backup = Path.Combine(profileDirectory, BackupName(save.LoadedVersion));
        if (save.LoadedVersion < DungeonRunSave.CurrentVersion && File.Exists(path) && !File.Exists(backup)) File.Copy(path, backup);
        string temp = path + ".tmp";
        using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            Serializer.Serialize(stream, save);
            stream.Flush(true);
        }
        if (File.Exists(path)) File.Replace(temp, path, null);
        else File.Move(temp, path);
        save.LoadedVersion = save.Version;
    }
}
