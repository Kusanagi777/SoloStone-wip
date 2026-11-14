public sealed partial class DeckPlanEvaluator
{
    // doomed-minions (AiFixes): a minion carrying a delayed self-destroy earns no board credit on either side; set from the
    // profile by the searches that own this evaluator. Default OFF = base.
    internal bool ZeroDoomed { get; set; }
    // The board price every caller uses; Board() itself stays the plain printed-stat price.
    private double Priced(FairCard c) => ZeroDoomed && c.Doomed ? 0 : c.IsRunShrine ? Shrine(c) : Board(c);

    // Phase 7: a Rumble Run board shrine. Awake: its body plus its engine (the per-shrine constant below). Its dormant form
    // has no body (it only holds a board slot) and regains the engine as its revival nears, so killing a shrine is priced
    // as tempo (body plus the engine for the turns it sleeps), never as a permanent removal.
    private double Shrine(FairCard c)
    {
        // A dormant form (TRLA_<n>t) is priced with its shrine's engine.
        double engine = ShrineEngine(SabberStoneCore.Actions.ShrineRules.ShrineOf(c.CardId) ?? c.CardId);
        if (c.Untouchable || c.ShrineDormantTurns > 0)
            return engine * System.Math.Max(0, ShrineRevive - c.ShrineDormantTurns) / ShrineRevive;
        return Board(c) + engine;
    }
    private const double ShrineRevive = 3;
    /// <summary>A shrine's engine value (its aura or trigger); one table for every shrine, a default for one not listed.</summary>
    internal static double ShrineEngine(string id) => id != null && ShrineEngines.TryGetValue(id, out double v) ? v : ShrineEngineDefault;
    private const double ShrineEngineDefault = 2;
    private static readonly System.Collections.Generic.Dictionary<string, double> ShrineEngines =
        new System.Collections.Generic.Dictionary<string, double>(System.StringComparer.Ordinal)
        {
            // Druid (phase 7 S1): Gonk's Armament refreshes mana on Armor, Gonk's Mark buffs every summon, Bonds of Balance grows.
            ["TRLA_112"] = 3, ["TRLA_115"] = 2.5, ["TRLA_116"] = 2,
            // Hunter (S1): Halazzi's Trap puts a Secret into play per spell, Halazzi's Hunt grants a cost-cutting Overkill,
            // Halazzi's Guise gives every Beast a keyword.
            ["TRLA_110"] = 3, ["TRLA_162"] = 2, ["TRLA_163"] = 2.5,
            // Warrior (S1): Akali's Champion is a body whose Attack is the Armor (priced by its stats), Akali's War Drum cuts
            // Dragon costs, Akali's Horn lets an Overkill attack again.
            ["TRLA_104"] = 1, ["TRLA_170"] = 1.5, ["TRLA_171"] = 2.5,
            // Mage (S2): Jan'alai's Mantle is Spell Damage per spell in hand, Jan'alai's Flame a free (and refreshing) Hero
            // Power, Jan'alai's Progeny a Frostfire per freeze.
            ["TRLA_108"] = 2, ["TRLA_128"] = 2.5, ["TRLA_129"] = 2,
            // Paladin (S2): Shirvallah's Protection shields survivors, Shirvallah's Vengeance answers hero damage with 5 to the
            // enemy hero, Shirvallah's Grace doubles a buff onto itself.
            ["TRLA_105"] = 2.5, ["TRLA_137"] = 3, ["TRLA_138"] = 1.5,
            // Priest (S2): Bwonsamdi's Sanctum triples Deathrattles, Bwonsamdi's Tome adds a spell per spell, Bwonsamdi's
            // Covenant turns healing on enemies into damage.
            ["TRLA_114"] = 2.5, ["TRLA_146"] = 2.5, ["TRLA_147"] = 2,
            // Rogue (S3): Bottled Terror stealths the other minions (and buffs them when it dies), Treasure from Below steals a
            // cheaper card each turn, Pirate's Mark casts every spell twice.
            ["TRLA_106"] = 2.5, ["TRLA_186"] = 2.5, ["TRLA_187"] = 3,
            // Shaman (S3): Krag'wa's Lure turns Overload into Attack, Tribute from the Tides triples Battlecries, Krag'wa's
            // Grace cuts spell costs by (2).
            ["TRLA_109"] = 2, ["TRLA_154"] = 2.5, ["TRLA_155"] = 2.5,
            // Warlock (S3): Blood Pact pays spells with Health, Dark Reliquary summons a Demon per discard, Hir'eek's Hunger
            // turns the owner's own-turn hero damage on the enemy hero.
            ["TRLA_113"] = 2, ["TRLA_178"] = 1.5, ["TRLA_179"] = 2.5,
            // The TRLA_107 fixture is a plain 0/1 Taunt.
            ["TRLA_107"] = 0,
        };
    // What an ENEMY doomed minion is still worth to the enemy at our end of turn: one swing in its reply, gone at our next
    // turn start (Nightmare/Corruption we cast on it). Static leaves only (Evaluate, never ReplyValue): inside reply search
    // an unspent swing would be a credit that attacking destroys, demoting the attack in the beam. Our own doomed minions
    // get nothing: at our end of turn they cannot swing, and mid-turn the same inverse incentive would discourage attacks.
    internal double DoomedSwing(FairPlayer owner, FairFacts facts)
    {
        if (!ZeroDoomed || owner.PlayerId != facts.CurrentPlayerId) return 0;
        double swing = 0;
        for (int i = 0; i < owner.Board.Count; i++) swing += Swing(owner.Board[i]);
        return swing;
    }
    internal double Swing(FairCard c) => c.Doomed && !c.Exhausted && !c.Frozen && !c.CantAttack && !c.Untouchable && c.AttacksThisTurn < c.AttackLimit
        ? c.Attack * (W.BoardAttack * k.BoardAttack) : 0;
}
