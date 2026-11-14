using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.CardSets.DungeonRun;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

int failures = 0, passes = 0;
void Check(string name, bool ok, string detail = "")
{
    if (ok) passes++; else failures++;
    if (!ok || Environment.GetEnvironmentVariable("VERBOSE") == "1")
        Console.WriteLine((ok ? "PASS " : "FAIL ") + name + (detail.Length > 0 ? "  (" + detail + ")" : ""));
}

const string Yeti = "CS2_182"; // 4/5 vanilla
const string Wisp = "CS2_231"; // 1/1 vanilla
List<Card> Deck(int n, string id = Yeti) => Enumerable.Repeat(Cards.FromId(id), n).ToList();

Game Start(Action<RunMatchPlan> tweak = null, bool applyTreasures = true)
{
    var plan = new RunMatchPlan
    {
        PlayerHero = Cards.FromId("HERO_08"), PlayerDeck = Deck(10),
        BossHero = Cards.FromId("HERO_01"), BossDeck = Deck(30), BossHealth = 30,
    };
    tweak?.Invoke(plan);
    var cfg = new GameConfig { RandomSeed = 99, Shuffle = true, SkipMulligan = false, History = true, Logging = false, FormatType = FormatType.FT_WILD };
    DungeonRunSetup.Configure(cfg, plan);
    var g = new Game(cfg);
    g.StartGame();
    if (applyTreasures) DungeonRunSetup.ApplyAfterStart(g, plan);
    g.Process(ChooseTask.Mulligan(g.Player1, new List<int>()));
    g.Process(ChooseTask.Mulligan(g.Player2, new List<int>()));
    g.MainBegin();
    return g;
}
Minion Summon(Controller c, string id = Yeti)
{
    var m = (Minion)Entity.FromCard(c, Cards.FromId(id));
    Generic.SummonBlock(c.Game, m, -1, c.Hero);
    return m;
}
void EndTurn(Game g) => g.Process(EndTurnTask.Any(g.CurrentPlayer));
void Round(Game g) { EndTurn(g); EndTurn(g); } // player -> boss -> player
IPlayable InHand(Controller c, string key)
{
    var card = Cards.FromId(DungeonRunContent.Get(key).ShellId);
    var e = Entity.FromCard(c, card);
    Generic.AddHandPhase(c, e);
    return e;
}
void GiveMana(Controller c, int n) { c.BaseMana = n; c.UsedMana = 0; }

// ---------------------------------------------------------------- 1. every shell loads with its shape
foreach (var content in DungeonRunContent.All.Where(c => c.ShellId != null))
{
    Card card = null;
    try { card = Cards.FromId(content.ShellId); } catch { }
    bool typeOk = card != null && content.Kind switch
    {
        RunContentKind.HeroPower or RunContentKind.PassivePower => card.Type == CardType.HERO_POWER,
        RunContentKind.Effect => card.Type == CardType.ENCHANTMENT,
        // Phase 6 F2: an active treasure may be a weapon (LOOTA_819, 821, 835, 842, 842a), shelled by its own id.
        RunContentKind.ActiveTreasure => card.Type == CardType.SPELL || card.Type == CardType.MINION || card.Type == CardType.WEAPON && content.ShellId == content.Key,
        // Phase 6: a named hero portrait, and a passive treasure keyed by its own real id (its display shell is a SPELL).
        RunContentKind.Hero => card.Type == CardType.HERO && card.Health == content.Health,
        RunContentKind.PassiveTreasure => card.Type == CardType.SPELL && content.ShellId == content.Key,
        _ => false
    };
    Check($"shell {content.Key} -> {content.ShellId}", card != null && card.Implemented && typeOk
        && (content.Kind is RunContentKind.Effect || card.Cost == content.Cost || content.Kind == RunContentKind.PassivePower),
        card == null ? "missing" : $"type={card.Type} impl={card.Implemented} cost={card.Cost}/{content.Cost}");
}
Check("passive power registry = passive content (plus the passive encounter powers: phase 8 family 01, Trapped Room's Alarm; family 08, the three totems, the two Gutmooks, Candlebeard and the Lava-Filled Chamber; family 09, Xol's Beam Me Up! and the four rule-cast powers Dispel Ward, Devour, Doors Are Closing and Stolen Time; family 15, the rule-cast Equivalent Exchange and Totemic Summons, Twin Paths, Stormswell, Assembly, Cold Water and Scroll Savvy)", DungeonRunCardsGen.PassiveHeroPowers.Count == DungeonRunContent.All.Count(c => c.Kind == RunContentKind.PassivePower) + DungeonRunEncounters.PassivePowers.Count()
    && DungeonRunEncounters.PassivePowers.SequenceEqual(new[] { "LOOTA_BOSS_48p", "LOOTA_BOSS_16p", "LOOTA_BOSS_17p", "LOOTA_BOSS_38p", "LOOTA_BOSS_19p", "LOOTA_BOSS_33p", "LOOTA_BOSS_31p", "LOOTA_BOSS_47p",
        "LOOTA_BOSS_20p", "LOOTA_BOSS_26p8", "LOOTA_BOSS_34p", "LOOTA_BOSS_52p", "LOOTA_BOSS_53p",
        "DALA_BOSS_16p", "DALA_BOSS_17p", "DALA_BOSS_18p", "DALA_BOSS_20p", "DALA_BOSS_25p", "DALA_BOSS_26p", "DALA_BOSS_28p" }));
Check("no class power treated as run passive", new[] { "CS2_017","DS1h_292","CS2_034","CS2_101","CS1h_001","CS2_083b","CS2_049","CS2_056","CS2_102","HERO_11bp","HERO_10bp" }.All(id => !HeroPower.ActivatesAtGameStart(Cards.FromId(id))));

// ---------------------------------------------------------------- 2. start-state treasures
{
    var g = Start(p => p.PlayerTreasures = new List<string> { "hearty_tonic" });
    Check("Hearty Tonic: 40 health", g.Player1.Hero.Health == 40, $"{g.Player1.Hero.Health}");
    var g0 = Start();
    var g1 = Start(p => p.PlayerTreasures = new List<string> { "deep_satchel" });
    Check("Deep Satchel: +1 opening card", g1.Player1.HandZone.Count == g0.Player1.HandZone.Count + 1, $"{g0.Player1.HandZone.Count}->{g1.Player1.HandZone.Count}");
    var g2 = Start(p => p.PlayerTreasures = new List<string> { "spare_crystal" });
    Check("Spare Crystal: 2 mana on turn 1", g2.Player1.BaseMana == 2 && g0.Player1.BaseMana == 1, $"base {g0.Player1.BaseMana} vs {g2.Player1.BaseMana}");
    var g3 = Start(p => p.PlayerTreasures = new List<string> { "lucky_coin" });
    Check("Lucky Coin: a Coin in hand", g3.Player1.HandZone.Count(c => c.Card.Id == "GAME_005") == 1);
    var g4 = Start(p => p.PlayerTreasures = new List<string> { "hearty_tonic", "deep_satchel", "lucky_coin" });
    Check("treasures stack", g4.Player1.Hero.Health == 40 && g4.Player1.HandZone.Count == g0.Player1.HandZone.Count + 2);
}

// ---------------------------------------------------------------- 3. trigger treasures
{
    var g = Start(p => p.PlayerTreasures = new List<string> { "iron_skin" });
    EndTurn(g);
    Check("Iron Skin: 2 Armor at end of turn", g.Player1.Hero.Armor == 2, $"{g.Player1.Hero.Armor}");
    EndTurn(g);
    Check("Iron Skin: not on the boss's turn", g.Player1.Hero.Armor == 2, $"{g.Player1.Hero.Armor}");
}
{
    var g = Start(p => p.PlayerTreasures = new List<string> { "mending_charm" });
    g.Player1.Hero.Damage = 10; EndTurn(g);
    Check("Mending Charm: heals 3", g.Player1.Hero.Health == 23, $"{g.Player1.Hero.Health}");
}
{
    var g = Start(p => p.PlayerTreasures = new List<string> { "whetstone_charm" });
    var m = Summon(g.Player1); Round(g);
    Check("Whetstone Charm: +1 Attack at turn start", m.AttackDamage == 5, $"{m.AttackDamage}");
}
{
    var g = Start(p => p.PlayerTreasures = new List<string> { "stoneskin_idol" });
    var a = Summon(g.Player1); var b = Summon(g.Player1); Round(g);
    Check("Stoneskin Idol: all minions +1 Health", a.Health == 6 && b.Health == 6, $"{a.Health},{b.Health}");
}
{
    var g = Start(p => p.PlayerTreasures = new List<string> { "spark_lantern" });
    var e = Summon(g.Player2); EndTurn(g); EndTurn(g);
    Check("Spark Lantern: 1 damage to enemy minion", e.Damage == 1, $"dmg {e.Damage}");
}
{
    var g = Start(p => p.PlayerTreasures = new List<string> { "quiet_satchel" });
    while (g.Player1.HandZone.Count > 2) g.Player1.HandZone.Remove(g.Player1.HandZone[0]);
    Round(g);
    Check("Quiet Satchel: extra draw with a small hand", g.Player1.HandZone.Count == 4, $"hand {g.Player1.HandZone.Count} (2 + turn draw + satchel)");
}
{
    var g = Start(p => p.PlayerTreasures = new List<string> { "thrifty_gloves" });
    Check("Thrifty Gloves: hero power costs 1", g.Player1.Hero.HeroPower.Cost == 1, $"{g.Player1.Hero.HeroPower.Cost}");
}
{
    var g = Start(p => p.PlayerTreasures = new List<string> { "champions_banner" });
    var m = Summon(g.Player1);
    Check("Champion's Banner: summoned minion +1 Health", m.Health == 6, $"{m.Health}");
}
{
    var g = Start(p => p.PlayerTreasures = new List<string> { "grave_candle" });
    var dies = Summon(g.Player1, Wisp); var lives = Summon(g.Player1);
    Generic.DamageCharFunc(g.Player2.Hero, dies, 5, false); g.DeathProcessingAndAuraUpdate();
    Check("Grave Candle: survivor +1/+1 after a death", lives.AttackDamage == 5 && lives.Health == 6, $"{lives.AttackDamage}/{lives.Health}");
}
{
    var g = Start(p => p.PlayerTreasures = new List<string> { "ember_brand" });
    var rod = InHand(g.Player1, "iron_ward"); GiveMana(g.Player1, 10);
    int before = g.Player2.Hero.Health + g.Player2.BoardZone.Sum(m => m.Health);
    g.Process(PlayCardTask.Any(g.Player1, rod));
    int after = g.Player2.Hero.Health + g.Player2.BoardZone.Sum(m => m.Health);
    Check("Ember Brand: 1 damage after a spell", before - after == 1, $"{before}->{after}");
}
{
    var g = Start(p => p.PlayerTreasures = new List<string> { "old_map" });
    while (g.Player1.HandZone.Count > 1) g.Player1.HandZone.Remove(g.Player1.HandZone[0]);
    EndTurn(g); EndTurn(g);
    Check("Old Map: a hand card costs 1 less", g.Player1.HandZone.Any(c => c.Cost == 3), string.Join(",", g.Player1.HandZone.Select(c => c.Cost)));
}
{
    var g = Start(p => { p.PlayerTreasures = new List<string> { "keen_eye" }; p.PlayerHeroPowerId = "trapper_snare"; });
    var e = Summon(g.Player2); GiveMana(g.Player1, 10);
    g.Process(HeroPowerTask.Any(g.Player1, e));
    Check("Set Snare + Keen Eye: frozen and 1 damage", e.IsFrozen && e.Damage == 1, $"frozen={e.IsFrozen} dmg={e.Damage}");
}

// ---------------------------------------------------------------- 4. player hero powers
{
    var g = Start(p => p.PlayerHeroPowerId = "trapper_snare");
    var e = Summon(g.Player2); GiveMana(g.Player1, 10);
    g.Process(HeroPowerTask.Any(g.Player1, e));
    Check("Set Snare: freezes", e.IsFrozen && e.Damage == 0);
    g.Player1.Hero.HeroPower.IsExhausted = false;
    g.Process(HeroPowerTask.Any(g.Player1, e));
    Check("Set Snare: 2 damage to a frozen minion", e.Damage == 2, $"dmg {e.Damage}");
}
{
    var g = Start(p => p.PlayerHeroPowerId = "sellsword_shield_wall");
    var m = Summon(g.Player1); GiveMana(g.Player1, 10);
    g.Process(HeroPowerTask.Any(g.Player1, m));
    Check("Shield Wall: +2 Health and Taunt", m.Health == 7 && m.HasTaunt, $"{m.Health} taunt={m.HasTaunt}");
}
{
    var g = Start(p => p.PlayerHeroPowerId = "sellsword_war_cry");
    var m = Summon(g.Player1); GiveMana(g.Player1, 10);
    g.Process(HeroPowerTask.Any(g.Player1));
    bool during = m.AttackDamage == 5;
    Round(g);
    Check("War Cry: +1 Attack this turn only", during && m.AttackDamage == 4, $"during={during} after={m.AttackDamage}");
}
{
    var g = Start(p => p.PlayerHeroPowerId = "carto_survey"); GiveMana(g.Player1, 10);
    g.Process(HeroPowerTask.Any(g.Player1));
    Check("Survey: offers a Discover choice", g.Player1.Choice != null && g.Player1.Choice.Choices.Count == 3);
}
{
    var g = Start(p => p.PlayerHeroPowerId = "carto_shortcut"); GiveMana(g.Player1, 10);
    int hand = g.Player1.HandZone.Count; g.Process(HeroPowerTask.Any(g.Player1));
    Check("Shortcut: draws, costs 3", g.Player1.HandZone.Count == hand + 1 && g.Player1.UsedMana == 3);
}
{
    var g = Start(p => p.PlayerHeroPowerId = "carto_ambush");
    var e = Summon(g.Player2); GiveMana(g.Player1, 10);
    g.Process(HeroPowerTask.Any(g.Player1, e));
    Check("Ambush Point: 1 to minion and enemy hero", e.Damage == 1 && g.Player2.Hero.Health == 29);
}

// ---------------------------------------------------------------- 5. shrines (passive, tiered)
{
    var g = Start(p => p.PlayerHeroPowerId = "shrine_dawn_1");
    var m = Summon(g.Player1); Round(g);
    Check("Shrine of Dawn I: passive, +1/+1 at turn start", g.Player1.Hero.HeroPower.IsPassiveHeroPower && !g.Player1.Hero.HeroPower.IsPlayableByPlayer && m.AttackDamage == 5 && m.Health == 6, $"{m.AttackDamage}/{m.Health}");
    var g3 = Start(p => p.PlayerHeroPowerId = "shrine_dawn_3");
    var a = Summon(g3.Player1); var b = Summon(g3.Player1); Round(g3);
    Check("Shrine of Dawn III: all minions +1/+1", a.AttackDamage == 5 && b.AttackDamage == 5);
}
{
    var g = Start(p => p.PlayerHeroPowerId = "shrine_bulwark_2"); EndTurn(g);
    Check("Shrine of the Bulwark II: 4 Armor", g.Player1.Hero.Armor == 4, $"{g.Player1.Hero.Armor}");
}
{
    var g = Start(p => p.PlayerHeroPowerId = "shrine_embers_3");
    var yeti = InHand(g.Player1, "warden_golem"); GiveMana(g.Player1, 10);
    int before = g.Player2.Hero.Health; g.Process(PlayCardTask.Any(g.Player1, yeti));
    Check("Shrine of Embers III: 3 damage after playing a minion", before - g.Player2.Hero.Health == 3, $"{before}->{g.Player2.Hero.Health}");
}

// ---------------------------------------------------------------- 6. boss powers and passives
{
    var g = Start(p => p.BossHeroPowerId = "boss_stomp");
    var a = Summon(g.Player1); var b = Summon(g.Player1); EndTurn(g); GiveMana(g.Player2, 10);
    g.Process(HeroPowerTask.Any(g.Player2));
    Check("Stomp: 1 damage to all enemy minions", a.Damage == 1 && b.Damage == 1);
}
{
    var g = Start(p => p.BossHeroPowerId = "boss_cinder_rain"); EndTurn(g); GiveMana(g.Player2, 10);
    g.Process(HeroPowerTask.Any(g.Player2));
    Check("Cinder Rain: 2 damage to the only enemy (hero)", g.Player1.Hero.Health == 28, $"{g.Player1.Hero.Health}");
}
{
    var g = Start(p => p.BossHeroPowerId = "passive_tithe"); EndTurn(g); EndTurn(g);
    Check("Tithe: 2 damage to the player at the boss's end of turn", g.Player1.Hero.Health == 28 && !g.Player2.Hero.HeroPower.IsPlayableByPlayer, $"{g.Player1.Hero.Health}");
}
{
    var g = Start(p => { p.BossHeroPowerId = "passive_vigil"; p.BossHealth = 40; });
    g.Player2.Hero.Damage = 10; EndTurn(g);
    Check("Vigil: boss heals 4 at its turn start", g.Player2.Hero.Health == 34, $"{g.Player2.Hero.Health}");
}
{
    var g = Start(p => p.BossHeroPowerId = "passive_devour");
    var a = Summon(g.Player1); EndTurn(g);
    Check("Devour: 2 damage to player minions at boss turn start", a.Damage == 2, $"{a.Damage}");
}
{
    var g = Start(p => p.BossHeroPowerId = "passive_undying");
    var m = Summon(g.Player2, Wisp); EndTurn(g);
    Generic.DamageCharFunc(g.Player1.Hero, m, 5, false); g.DeathProcessingAndAuraUpdate();
    Check("Undying: a 1/1 replaces a dead boss minion", g.Player2.BoardZone.Any(x => x.Card.Id == "CS2_101t"));
}
{
    var g = Start(p => p.BossHeroPowerId = "passive_pilfer"); EndTurn(g); EndTurn(g);
    Check("Pilfer: boss gets a Coin at end of turn", g.Player2.HandZone.Count(c => c.Card.Id == "GAME_005") == 2, "boss holds the second-player Coin too");
}

// ---------------------------------------------------------------- 7. active treasures
{
    var g = Start(); GiveMana(g.Player1, 10);
    var rod = InHand(g.Player1, "blazing_rod"); g.Process(PlayCardTask.Any(g.Player1, rod, g.Player2.Hero));
    Check("Blazing Rod: 4 damage", g.Player2.Hero.Health == 26);
    var ward = InHand(g.Player1, "iron_ward"); g.Process(PlayCardTask.Any(g.Player1, ward));
    Check("Iron Ward: 6 Armor", g.Player1.Hero.Armor == 6);
    int hand = g.Player1.HandZone.Count; var tome = InHand(g.Player1, "bottomless_tome"); g.Process(PlayCardTask.Any(g.Player1, tome));
    Check("Bottomless Tome: draws 3", g.Player1.HandZone.Count == hand + 3, $"{hand}->{g.Player1.HandZone.Count}");
}
{
    var g = Start(); GiveMana(g.Player1, 10);
    var m = Summon(g.Player1); var shard = InHand(g.Player1, "mirror_shard"); g.Process(PlayCardTask.Any(g.Player1, shard, m));
    Check("Mirror Shard: copies a friendly minion", g.Player1.BoardZone.Count(x => x.Card.Id == Yeti) == 2);
    var e1 = Summon(g.Player2); var e2 = Summon(g.Player2);
    var frost = InHand(g.Player1, "frost_lantern"); g.Process(PlayCardTask.Any(g.Player1, frost));
    Check("Frost Lantern: freezes all enemy minions", e1.IsFrozen && e2.IsFrozen);
    var quake = InHand(g.Player1, "quake_stone"); g.Process(PlayCardTask.Any(g.Player1, quake));
    Check("Quake Stone: 2 to all minions", e1.Damage == 2 && m.Damage == 2);
}
{
    var g = Start(); GiveMana(g.Player1, 10); g.Player1.Hero.Damage = 12;
    var spring = InHand(g.Player1, "healing_spring"); g.Process(PlayCardTask.Any(g.Player1, spring));
    Check("Healing Spring: restores 8", g.Player1.Hero.Health == 26);
    var golem = InHand(g.Player1, "warden_golem"); g.Process(PlayCardTask.Any(g.Player1, golem));
    var gm = g.Player1.BoardZone.Last();
    Check("Warden Golem: 3/5 Taunt for 3", gm.AttackDamage == 3 && gm.Health == 5 && gm.HasTaunt && golem.Cost == 3);
    var hound = InHand(g.Player1, "swift_hound"); g.Process(PlayCardTask.Any(g.Player1, hound));
    var hm = g.Player1.BoardZone.Last();
    Check("Swift Hound: 3/2 Rush", hm.AttackDamage == 3 && hm.Health == 2 && hm.IsRush);
    GiveMana(g.Player1, 10); int hand = g.Player1.HandZone.Count; var idol = InHand(g.Player1, "gilded_idol"); g.Process(PlayCardTask.Any(g.Player1, idol));
    Check("Gilded Idol: 4/4, Battlecry draws", g.Player1.BoardZone.Last().AttackDamage == 4 && g.Player1.HandZone.Count == hand + 1, $"last={g.Player1.BoardZone.Last().Card.Id} {g.Player1.BoardZone.Last().AttackDamage}/{g.Player1.BoardZone.Last().Health} hand {hand}->{g.Player1.HandZone.Count} deck={g.Player1.DeckZone.Count} board={g.Player1.BoardZone.Count} mana={g.Player1.RemainingMana} idolZone={idol.Zone?.Type}");
}
{
    var g = Start(); GiveMana(g.Player1, 10);
    var a = Summon(g.Player2); var b = Summon(g.Player2);
    var net = InHand(g.Player1, "hunting_net"); g.Process(PlayCardTask.Any(g.Player1, net));
    Check("Hunting Net: freezes two", a.IsFrozen && b.IsFrozen);
    var e = Summon(g.Player2, Wisp); var flask = InHand(g.Player1, "sig_smoke_flask"); g.Process(PlayCardTask.Any(g.Player1, flask, e));
    Check("Smoke Flask: returns to hand", g.Player2.HandZone.Any(c => c.Card.Id == Wisp));
    var chart = InHand(g.Player1, "sig_star_chart"); g.Process(PlayCardTask.Any(g.Player1, chart));
    Check("Star Chart: 2 to enemy minions", a.Damage == 2 && b.Damage == 2);
    int hand = g.Player1.HandZone.Count; var bloom = InHand(g.Player1, "charm_blooming"); g.Process(PlayCardTask.Any(g.Player1, bloom));
    Check("Blooming Charm: 6 Armor, draws 2", g.Player1.Hero.Armor == 6 && g.Player1.HandZone.Count == hand + 2);
}

// ---------------------------------------------------------------- 7b. one-turn tags (final review finding)
foreach (var fx in DungeonRunContent.All.Where(c => c.Kind == RunContentKind.Effect))
{
    bool oneTurn = Cards.FromId(fx.ShellId)[GameTag.TAG_ONE_TURN_EFFECT] != 0;
    // Phase 6 F2: Boots of Haste's own client enchantment (LOOTA_812e, "this turn") keeps the client's one-turn tag. Phase 6 F3:
    // Victorious Rally's Strategic Initiative (GILA_602e3, "Increased Attack this turn", the client's own tag) and Extra Powder's
    // player enchantment (GILA_604e2, "this turn") last one turn too.
    Check($"one-turn tag on {fx.Key} only if meant", oneTurn == (fx.Key == "buff_atk_1_turn" || fx.Key == "fx_LOOTA_812" || fx.Key == "fx_GILA_602e3" || fx.Key == "fx_GILA_604"
        // Phase 6 F6: Opportunist (DALA_Hunter_HP1e, "+2 Attack this turn", the client's own tag).
        || fx.Key == "fx_DALA_Hunter_HP1e"
        // Phase 6 F12: Staff of Ammunae's player enchantment (ULDA_041e2, "this turn", the client's own tag).
        || fx.Key == "fx_ULDA_041e2"
        // Phase 6 F7: Overpowered's player enchantment (DALA_715e, "this turn", the client's own tag).
        || fx.Key == "fx_DALA_715e"
        // Phase 6 F9: "Relicologist"'s Relic Power (ULDA_Reno_HP2e, "your next spell this turn", the client's own tag).
        || fx.Key == "fx_ULDA_Reno_HP2e"
        // Phase 6 F13: Darklight Torch's hero power discount (ULDA_042e2, "It costs (0) this turn") and Fleeting Knowledge (ULDA_508e2,
        // "reduce its Cost to (0) this turn"): one-turn Cost enchantments.
        || fx.Key == "fx_ULDA_042e2" || fx.Key == "fx_ULDA_508e2"), $"tag={oneTurn}");
}
{
    var g = Start(p => p.PlayerHeroPowerId = "sellsword_shield_wall");
    var m = Summon(g.Player1); GiveMana(g.Player1, 10);
    g.Process(HeroPowerTask.Any(g.Player1, m));
    Round(g);
    Check("Shield Wall buff entity survives the turn with its Health", m.Health == 7 && m.HasTaunt
        && m.AppliedEnchantments.Any(e => e.Card.Id == DungeonRunContent.Get("buff_hp_2").ShellId), $"{m.Health} taunt={m.HasTaunt}");
}

// ---------------------------------------------------------------- 8. determinism and cloning with run content
{
    Func<Game> make = () => Start(p => { p.PlayerTreasures = new List<string> { "spark_lantern", "whetstone_charm" }; p.BossHeroPowerId = "passive_command"; });
    var a = make(); var b = make();
    for (int i = 0; i < 3; i++) { Round(a); Round(b); }
    Check("same seed + same run setup = same game", a.Hash() == b.Hash());
    var clone = a.CloneExact();
    Round(clone);
    Check("clone keeps run effects", clone.Player1.AppliedEnchantments.Count(e => e.Card.Id == DungeonRunContent.Get("fx_spark_lantern").ShellId) == 1);
}

// ---------------------------------------------------------------- 9. the 12 class powers (content2): cast through HeroPowerTask in a real game
bool HasPowerOption(Game g) => g.Player1.Options().Any(o => o is HeroPowerTask);
bool HasPowerOptionOn(Game g, IPlayable target) => g.Player1.Options().OfType<HeroPowerTask>().Any(o => o.Target?.Id == target.Id);
string Board(Game g)
{
    string Side(Controller c) => $"hp{c.Hero.Health}+{c.Hero.Armor} hand{c.HandZone.Count} board[" + string.Join(",", c.BoardZone.Select(m =>
        $"{m.Card.Id}:{m.AttackDamage}/{m.Health}{(m.IsFrozen ? "F" : "")}{(m.HasDivineShield ? "D" : "")}{(m.HasCharge ? "C" : "")}{(m.HasTaunt ? "T" : "")}")) + "]";
    return Side(g.Player1) + " | " + Side(g.Player2);
}
// The board every power is cast on: player hero at 20, one damaged friendly Yeti, an enemy Yeti and an enemy Wisp.
(Game g, Minion mine, Minion foe) PowerBoard(string power, int seed = 99)
{
    var g = Start(p => p.PlayerHeroPowerId = power);
    g.Player1.Hero.Damage = 10;
    var mine = Summon(g.Player1); mine.Damage = 2;
    var foe = Summon(g.Player2); Summon(g.Player2, Wisp);
    GiveMana(g.Player1, 10);
    return (g, mine, foe);
}
var newPowers = new (string key, string target, Func<Game, Minion, Minion, string, bool> effect, bool random)[]
{
    ("druid_rootcall", null, (g, m, f, b) => g.Player1.BoardZone.Count == 2 && g.Player1.BoardZone.Last().Card.Id == "AT_037t", false),
    ("druid_thornhide", null, (g, m, f, b) => g.Player1.Hero.Armor == 1 && (g.Player2.Hero.Health == 29 || f.Damage == 1 || g.Player2.BoardZone.Count == 1), true),
    ("hunter_pack_whistle", null, (g, m, f, b) => g.Player1.BoardZone.Last().Card.Id == "EX1_538t" && g.Player1.BoardZone.Last().HasCharge
        && g.Player1.BoardZone.Last().CanAttack, false),
    ("mage_rimebite", "foe", (g, m, f, b) => f.Damage == 1 && f.IsFrozen, false),
    ("paladin_vow_of_light", "mine", (g, m, f, b) => m.HasDivineShield, false),
    ("paladin_lantern_march", null, (g, m, f, b) => g.Player1.Hero.Health == 22 && m.Damage == 0, false),
    ("priest_ward_of_faith", "mine", (g, m, f, b) => m.Health == 5 && m.BaseHealth == 7, false),
    ("priest_penitent_lash", "foe", (g, m, f, b) => f.Damage == 1 && g.Player1.Hero.Health == 22, false),
    ("shaman_spirit_kindling", null, (g, m, f, b) => m.AttackDamage == 5 && m.Health == 4, false), // the only friendly minion
    ("shaman_spark_lash", null, (g, m, f, b) => f.Damage == 2 || g.Player2.BoardZone.Count == 1, true),
    ("warlock_fiend_gate", null, (g, m, f, b) => g.Player1.BoardZone.Last().Card.Id == "EX1_598", false),
    ("warlock_sanguine_pact", "mine", (g, m, f, b) => g.Player1.Hero.Health == 18 && m.AttackDamage == 6 && m.Health == 5, false),
    // audit-classes: Death Knight and Demon Hunter
    ("deathknight_grave_call", null, (g, m, f, b) => g.Player1.BoardZone.Count == 2 && g.Player1.BoardZone.Last().Card.Id == "EDR_813at"
        && g.Player1.BoardZone.Last().AttackDamage == 1 && g.Player1.BoardZone.Last().Health == 1, false),
    ("deathknight_rime_wind", null, (g, m, f, b) => f.Damage == 1 && g.Player2.BoardZone.Count == 1 && m.Damage == 2 && g.Player2.Hero.Health == 30
        && g.Player1.Hero.Health == 20, false),
    ("demonhunter_glaivewhirl", null, (g, m, f, b) => g.Player1.Hero.AttackDamage == 1 && g.Player1.HandZone.Count == HandOf(b) + 1, false),
    ("demonhunter_felbat_call", null, (g, m, f, b) => g.Player1.BoardZone.Last().Card.Id == "TTN_843t1" && g.Player1.BoardZone.Last()[GameTag.RUSH] == 1
        && g.Player1.BoardZone.Last().IsRace(Race.DEMON), false),
};
int HandOf(string board) => int.Parse(board.Substring(board.IndexOf("hand") + 4).Split(' ')[0]);
Check("16 new class powers registered as clickable hero powers", newPowers.All(p => DungeonRunContent.TryGet(p.key, out var c) && c.Kind == RunContentKind.HeroPower));
foreach (var (key, target, effect, random) in newPowers)
{
    try
    {
        var content = DungeonRunContent.Get(key);
        var shell = Cards.FromId(content.ShellId);
        Check($"{key}: shell {content.ShellId} scripted, cost {content.Cost}, not choose-one, no Secret, not passive", shell.Implemented && shell.Cost == content.Cost
            && (content.Cost == 2 || key == "deathknight_rime_wind" && content.Cost == 3)
            && shell[GameTag.CHOOSE_ONE] == 0 && shell[GameTag.SECRET] == 0 && !HeroPower.ActivatesAtGameStart(shell) && content.Text.StartsWith("<b>Hero Power</b>\n"),
            $"cost {shell.Cost}");
        var (g, mine, foe) = PowerBoard(key);
        IPlayable t = target == "mine" ? mine : target == "foe" ? foe : null;
        var clone = g.Clone();
        string before = Board(g);
        bool offered = t == null ? HasPowerOption(g) : HasPowerOptionOn(g, t);
        bool processed = g.Process(t == null ? HeroPowerTask.Any(g.Player1) : HeroPowerTask.Any(g.Player1, (ICharacter)t));
        string after = Board(g);
        Check($"{key}: legal option, cast via HeroPowerTask, effect holds", offered && processed && g.Player1.UsedMana == content.Cost && g.Player1.Hero.HeroPower.IsExhausted
            && effect(g, mine, foe, before), before + " -> " + after);
        // The same cast on a clone made before it (the AI search path): same result unless the power rolls a random target.
        var cMine = (Minion)clone.IdEntityDic[mine.Id]; var cFoe = (Minion)clone.IdEntityDic[foe.Id];
        IPlayable ct = target == "mine" ? cMine : target == "foe" ? cFoe : null;
        clone.Process(ct == null ? HeroPowerTask.Any(clone.Player1) : HeroPowerTask.Any(clone.Player1, (ICharacter)ct));
        Check($"{key}: clone cast " + (random ? "valid (random target, may roll differently)" : "matches"), random ? effect(clone, cMine, cFoe, before) : Board(clone) == after,
            Board(clone) + " vs " + after);
        // A second game from the same seed and the same moves: the same result (determinism, random powers included).
        var (g2, mine2, foe2) = PowerBoard(key);
        IPlayable t2 = target == "mine" ? mine2 : target == "foe" ? foe2 : null;
        g2.Process(t2 == null ? HeroPowerTask.Any(g2.Player1) : HeroPowerTask.Any(g2.Player1, (ICharacter)t2));
        Check($"{key}: same seed, same game, same result", Board(g2) == after && g2.Hash() == g.Hash(), Board(g2));
        // A cloned game keeps playing after the cast (CloneExact path used by the match runtime).
        var exact = g.CloneExact(); Round(exact);
        Check($"{key}: exact clone plays on", exact.State == State.RUNNING);
    }
    catch (Exception e) { Check($"{key}: runs", false, e.GetType().Name + ": " + e.Message); }
}
// Summoning powers: unplayable with a full board. Targeted powers: no legal play without a legal target.
foreach (string key in new[] { "druid_rootcall", "hunter_pack_whistle", "warlock_fiend_gate", "deathknight_grave_call", "demonhunter_felbat_call" })
{
    var g = Start(p => p.PlayerHeroPowerId = key); GiveMana(g.Player1, 10);
    for (int i = 0; i < 7; i++) Summon(g.Player1, Wisp);
    Check($"{key}: unplayable with a full board", !HasPowerOption(g) && !g.Player1.Hero.HeroPower.IsPlayable);
}
foreach (string key in new[] { "mage_rimebite", "paladin_vow_of_light", "priest_ward_of_faith", "priest_penitent_lash", "warlock_sanguine_pact" })
{
    var g = Start(p => p.PlayerHeroPowerId = key); GiveMana(g.Player1, 10);
    Check($"{key}: no legal play with no minions", !HasPowerOption(g));
}
{
    // Friendly-only and enemy-only targeting.
    var g = Start(p => p.PlayerHeroPowerId = "paladin_vow_of_light"); GiveMana(g.Player1, 10);
    var foe = Summon(g.Player2);
    Check("Vow of Light: an enemy minion is not a legal target", !HasPowerOption(g) && !HasPowerOptionOn(g, foe));
    var g2 = Start(p => p.PlayerHeroPowerId = "priest_penitent_lash"); GiveMana(g2.Player1, 10);
    var mine = Summon(g2.Player1);
    Check("Penitent Lash: a friendly minion is not a legal target", !HasPowerOption(g2) && !HasPowerOptionOn(g2, mine));
    var g3 = Start(p => p.PlayerHeroPowerId = "mage_rimebite"); GiveMana(g3.Player1, 10);
    var own = Summon(g3.Player1);
    Check("Rimebite: any minion, friendly included; heroes are not targets", HasPowerOptionOn(g3, own) && !HasPowerOptionOn(g3, g3.Player2.Hero));
}
{
    // Clickable with an empty board, and then nothing happens (as War Cry): Spirit Kindling, Spark Lash.
    var g = Start(p => p.PlayerHeroPowerId = "shaman_spark_lash"); GiveMana(g.Player1, 10);
    int hp = g.Player2.Hero.Health;
    Check("Spark Lash: never hits the enemy hero", HasPowerOption(g) && g.Process(HeroPowerTask.Any(g.Player1)) && g.Player2.Hero.Health == hp);
    var k = Start(p => p.PlayerHeroPowerId = "warlock_sanguine_pact"); GiveMana(k.Player1, 10); k.Player1.Hero.Damage = 28;
    var m = Summon(k.Player1);
    k.Process(HeroPowerTask.Any(k.Player1, m));
    Check("Sanguine Pact at 2 Health kills its own hero (as Life Tap can)", k.Player1.Hero.IsDead || k.State == State.COMPLETE);
}
// Tokens: what the summoning powers declare, with their names and stats.
{
    var expect = new Dictionary<string, (string token, string name, int atk, int hp, bool charge, Race race)>
    {
        ["druid_rootcall"] = ("AT_037t", "Treant", 1, 1, false, Race.INVALID),
        ["hunter_pack_whistle"] = ("EX1_538t", "Hound", 1, 1, true, Race.BEAST),
        ["warlock_fiend_gate"] = ("EX1_598", "Imp", 1, 1, false, Race.DEMON),
        ["deathknight_grave_call"] = ("EDR_813at", "Ant Husk", 1, 1, false, Race.UNDEAD),
        ["demonhunter_felbat_call"] = ("TTN_843t1", "Invading Felbat", 1, 1, false, Race.DEMON),
        // Phase 6 F4: Houndmaster Shaw's Dog Whistle summons his 1/1 Bloodhound (Rush, not Charge).
        ["GILA_400p"] = ("GILA_400t", "Bloodhound", 1, 1, false, Race.BEAST),
        // Phase 6 F6: Tekahn's The Pact summons a 2/2 Devious Imp.
        ["DALA_Warlock_HP2"] = ("DALA_Warlock_HP2t", "Devious Imp", 2, 2, false, Race.DEMON),
        // Phase 6 F9: Sir Finley's New "Recruits" summons a 2/1 Amalgam Explorer (every minion type).
        ["ULDA_Finley_HP1"] = ("ULDA_Finley_HP1t", "Amalgam Explorer", 2, 1, false, Race.MURLOC),
    };
    foreach (var pair in expect)
    {
        var content = DungeonRunContent.Get(pair.Key);
        var card = Cards.FromId(pair.Value.token);
        Check($"{pair.Key}: Tokens = [{pair.Value.token}], {card.Name} {card.ATK}/{card.Health}" + (pair.Value.charge ? " Charge" : "") + (pair.Value.race != Race.INVALID ? " " + pair.Value.race : ""),
            content.Tokens.SequenceEqual(new[] { pair.Value.token }) && card.ATK == pair.Value.atk && card.Health == pair.Value.hp && card.Charge == pair.Value.charge
            && card.Implemented && card.Type == CardType.MINION && (pair.Value.race == Race.INVALID || card.IsRace(pair.Value.race)),
            $"{card.Name} races={string.Join("/", card.Races ?? new Race[0])}");
    }
    Check("non-summoning powers declare no tokens", DungeonRunContent.All.Where(c => c.Kind == RunContentKind.HeroPower && !expect.ContainsKey(c.Key)).All(c => c.Tokens.Count == 0));
}
{
    // Determinism over a whole turn sequence with a new power in the plan (same seed, same setup -> same hash), clone keeps it.
    Func<Game> make = () => Start(p => { p.PlayerHeroPowerId = "hunter_pack_whistle"; p.BossHeroPowerId = "boss_stomp"; });
    var a = make(); var b = make();
    foreach (var g in new[] { a, b }) { GiveMana(g.Player1, 10); g.Process(HeroPowerTask.Any(g.Player1)); Round(g); Round(g); }
    Check("Pack Whistle game: same seed + same moves = same hash", a.Hash() == b.Hash());
    var c = a.CloneExact();
    Check("Pack Whistle game: exact clone keeps the power", c.Player1.Hero.HeroPower.Card.Id == DungeonRunContent.Get("hunter_pack_whistle").ShellId && c.Hash() == a.Hash());
}

// ---------------------------------------------------------------- audit-classes: Death Knight and Demon Hunter specifics
{
    // Borrowed Tavern Brawl tags are dropped from the run powers (the client is sent the engine's tags).
    foreach (var (key, tag) in new[] { ("deathknight_rime_wind", GameTag.AI_MUST_PLAY), ("demonhunter_glaivewhirl", GameTag.DISCOVER), ("demonhunter_felbat_call", GameTag.DEATHRATTLE) })
        Check($"{key}: shell carries no {tag}", Cards.FromId(DungeonRunContent.Get(key).ShellId)[tag] == 0);
    // Glaivewhirl's Attack lasts this turn only; the draw stays.
    var g = Start(p => p.PlayerHeroPowerId = "demonhunter_glaivewhirl"); GiveMana(g.Player1, 10);
    g.Process(HeroPowerTask.Any(g.Player1));
    bool now = g.Player1.Hero.AttackDamage == 1 && g.Player1.Options().Any(o => o is HeroAttackTask);
    Round(g);
    Check("Glaivewhirl: +1 Attack this turn (hero can attack), gone next turn", now && g.Player1.Hero.AttackDamage == 0, $"atk {g.Player1.Hero.AttackDamage}");
    // Rime Wind with no enemy minions: castable, hits nothing else.
    var r = Start(p => p.PlayerHeroPowerId = "deathknight_rime_wind"); GiveMana(r.Player1, 10);
    var own = Summon(r.Player1);
    Check("Rime Wind: costs 3, never hits heroes or friendly minions", HasPowerOption(r) && r.Process(HeroPowerTask.Any(r.Player1))
        && r.Player1.UsedMana == 3 && r.Player2.Hero.Health == 30 && own.Damage == 0);
    // Felbat's Rush: can attack a minion on the turn it is summoned, not the hero.
    var fb = Start(p => p.PlayerHeroPowerId = "demonhunter_felbat_call"); GiveMana(fb.Player1, 10);
    var foe = Summon(fb.Player2, Wisp);
    fb.Process(HeroPowerTask.Any(fb.Player1));
    var bat = fb.Player1.BoardZone.Last();
    var attacks = fb.Player1.Options().OfType<MinionAttackTask>().Where(o => o.Source == bat).ToList();
    Check("Felbat Call: the Felbat attacks minions, not the hero, this turn", attacks.Any(o => o.Target == foe) && attacks.All(o => o.Target != fb.Player2.Hero));
}
{
    // Death Knight basics in a run match: the class hero and power, Corpses on (DungeonRunSetup), gained on death and spent.
    Game Dk()
    {
        var g = Start(p => { p.PlayerHero = Cards.FromId("HERO_11"); });
        GiveMana(g.Player1, 10);
        return g;
    }
    IPlayable Play(Game game, string id, ICharacter target = null)
    {
        var e = Entity.FromCard(game.Player1, Cards.FromId(id)); Generic.AddHandPhase(game.Player1, e);
        var task = game.Player1.Options().FirstOrDefault(o => o is PlayCardTask pc && pc.Source == e && (target == null || pc.Target == target));
        if (task == null) return null;
        return game.Process(task) ? e : null;
    }
    var g = Dk();
    Check("run match: Corpses enabled, also on clones", g.EnableCorpses && g.Clone().EnableCorpses && g.CloneExact().EnableCorpses);
    Check("Death Knight hero keeps Ghoul Charge (HERO_11bp) when no power is given", g.Player1.Hero.HeroPower.Card.Id == "HERO_11bp");
    g.Process(HeroPowerTask.Any(g.Player1));
    bool ghoul = g.Player1.BoardZone.Count == 1 && g.Player1.BoardZone[0].Card.Id == "HERO_11bpt" && g.Player1.BoardZone[0].HasCharge;
    EndTurn(g);
    Check("Ghoul Charge: a 1/1 Charge Ghoul that dies at the end of the turn", ghoul && g.Player1.BoardZone.Count == 0, $"corpses {CorpseRules.Available(g.Player1)}");
    var w = Dk();
    var dies = Summon(w.Player1, Wisp);
    Generic.DamageCharFunc(w.Player2.Hero, dies, 5, false); w.DeathProcessingAndAuraUpdate();
    Check("a friendly minion's death gives a Corpse", CorpseRules.Available(w.Player1) == 1, $"{CorpseRules.Available(w.Player1)}");
    Check("Body Bagger (RLK_503): Battlecry gains a Corpse", Play(w, "RLK_503") != null && CorpseRules.Available(w.Player1) == 2, $"{CorpseRules.Available(w.Player1)}");
    int before = w.Player1.BoardZone.Count;
    Check("Army of the Dead (RLK_060): raises 2 Corpses as 2/2 Rush Risen Ghouls", Play(w, "RLK_060") != null && w.Player1.BoardZone.Count == before + 2
        && CorpseRules.Available(w.Player1) == 0 && w.Player1.BoardZone.Skip(before).All(m => m.Card.Id == "RLK_008t" && m[GameTag.RUSH] == 1),
        $"board {string.Join(",", w.Player1.BoardZone.Select(m => m.Card.Id))} corpses {CorpseRules.Available(w.Player1)}");
    var t = Dk();
    CorpseRules.Gain(t.Player1, 4);
    Play(t, "CORE_RLK_118");
    Check("Tomb Guardians (CORE_RLK_118): two 2/2 Taunt zombies, Reborn with 4 Corpses spent", t.Player1.BoardZone.Count == 2 && t.Player1.BoardZone.All(m => m.HasTaunt && m[GameTag.REBORN] == 1)
        && CorpseRules.Available(t.Player1) == 0, $"{string.Join(",", t.Player1.BoardZone.Select(m => m.Card.Id + (m[GameTag.REBORN] == 1 ? "R" : "")))} corpses {CorpseRules.Available(t.Player1)}");
    var t2 = Dk(); Play(t2, "CORE_RLK_118");
    Check("Tomb Guardians without Corpses: no Reborn", t2.Player1.BoardZone.Count == 2 && t2.Player1.BoardZone.All(m => m.HasTaunt && m[GameTag.REBORN] == 0));
    var n = Dk(); CorpseRules.Gain(n.Player1, 1); Play(n, "RLK_061"); EndTurn(n);
    Check("Battlefield Necromancer (RLK_061): end of turn raises a 1/3 Taunt Footman", n.Player1.BoardZone.Any(m => m.Card.Id == "RLK_061t" && m.HasTaunt) && CorpseRules.Available(n.Player1) == 0,
        $"{string.Join(",", n.Player1.BoardZone.Select(m => m.Card.Id))} corpses {CorpseRules.Available(n.Player1)}");
    var a = Start(p => { p.PlayerHero = Cards.FromId("HERO_11"); p.PlayerHeroPowerId = "deathknight_grave_call"; }); GiveMana(a.Player1, 10);
    a.Process(HeroPowerTask.Any(a.Player1));
    var ant = a.Player1.BoardZone.Last(); Generic.DamageCharFunc(a.Player2.Hero, ant, 5, false); a.DeathProcessingAndAuraUpdate();
    Check("Grave Call: the Ant Husk leaves a Corpse when it dies", CorpseRules.Available(a.Player1) == 1);
    var b = Dk(); b.Player1.Hero.Damage = 10; var target = Summon(b.Player2);
    b.Process(HeroPowerTask.Any(b.Player1));
    Play(b, "JAIL_441", target);
    Check("Drink Blood (JAIL_441): 3 Lifesteal damage, hero power refreshed", target.Damage == 3 && b.Player1.Hero.Health == 23 && !b.Player1.Hero.HeroPower.IsExhausted,
        $"dmg {target.Damage} hp {b.Player1.Hero.Health} exhausted {b.Player1.Hero.HeroPower.IsExhausted}");
    var lr = Summon(b.Player2);
    var liferender = Play(b, "TIME_614", lr);
    Check("Liferender (TIME_614): health changed this turn, 6 damage to an enemy minion", liferender != null && (lr.IsDead || lr.Damage == 6), $"dmg {lr.Damage}");
    var ds = Dk(); ds.Player1.Hero.Damage = 10; var big = Summon(ds.Player2);
    Play(ds, "RLK_024", big);
    Check("Death Strike (RLK_024): 6 Lifesteal damage", big.IsDead && ds.Player1.Hero.Health == 26, $"hp {ds.Player1.Hero.Health}");
    var es = Dk(); var victim = Summon(es.Player2); es.Player1.Hero.Damage = 10;
    var surgery = Play(es, "JAIL_454");
    int guard = 0;
    while (es.Player1.Choice != null && guard++ < 5) es.Process(ChooseTask.Pick(es.Player1, es.Player1.Choice.Choices[0]));
    Check("Emergency Surgery (JAIL_454): resolves, the chosen minion dies, hero healed by Lifesteal", surgery != null && victim.IsDead && es.Player1.Hero.Health > 20,
        $"victim {victim.Damage}/{victim.Health} hp {es.Player1.Hero.Health} board {string.Join(",", es.Player1.BoardZone.Select(m => m.Card.Id))}");
}
// ---------------------------------------------------------------- 13. run games generate no Secrets (review 1, defect 1)
{
    var g = Start();
    Check("run match: Configure sets ExcludeGeneratedSecrets; clones keep it", g.ExcludeGeneratedSecrets && g.Clone().ExcludeGeneratedSecrets && g.CloneExact().ExcludeGeneratedSecrets);
    bool snap; try { snap = Game.LoadSnapshot(g.SaveSnapshot()).ExcludeGeneratedSecrets; } catch (Exception ex) { snap = false; Console.WriteLine("snapshot: " + ex.Message); }
    Check("run match: a snapshot round trip keeps ExcludeGeneratedSecrets; a plain game has it off", snap && !new Game(new GameConfig { FormatType = FormatType.FT_WILD }).ExcludeGeneratedSecrets);
    // The Survey power, Magic Trick, Ethereal Conjurer, Runed Orb and Shifting Scroll (a random Mage spell each turn) never put a
    // Secret in reach in a run match; the same cards in a match without the rule do (control), so the rule is what removes them.
    int Offered(bool run, Func<Game, IPlayable> cast, int tries, bool catalogRun = false)
    {
        int secrets = 0;
        for (int i = 0; i < tries; i++)
        {
            var plan = new RunMatchPlan { PlayerHero = Cards.FromId("HERO_08"), PlayerDeck = Deck(10), PlayerHeroPowerId = "carto_survey",
                BossHero = Cards.FromId("HERO_01"), BossDeck = Deck(30), BossHealth = 30, PlayerGeneratedSecrets = catalogRun };
            var cfg = new GameConfig { RandomSeed = 4000 + i, Shuffle = true, SkipMulligan = false, History = false, Logging = false, FormatType = FormatType.FT_WILD };
            DungeonRunSetup.Configure(cfg, plan);
            if (!run) cfg.ExcludeGeneratedSecrets = false;
            var game = new Game(cfg); game.StartGame();
            game.Process(ChooseTask.Mulligan(game.Player1, new List<int>())); game.Process(ChooseTask.Mulligan(game.Player2, new List<int>())); game.MainBegin();
            GiveMana(game.Player1, 10);
            cast(game);
            if (game.IdEntityDic.Values.Any(e => e.Controller == game.Player1 && e.Card.IsSecret)) secrets++;
        }
        return secrets;
    }
    IPlayable Play(Game game, string id)
    {
        var e = Entity.FromCard(game.Player1, Cards.FromId(id)); Generic.AddHandPhase(game.Player1, e);
        var task = game.Player1.Options().FirstOrDefault(o => o is PlayCardTask pc && pc.Source == e);
        if (task != null) game.Process(task);
        return e;
    }
    var casts = new (string name, Func<Game, IPlayable> cast)[]
    {
        ("Survey (carto_survey)", game => { game.Process(HeroPowerTask.Any(game.Player1)); return null; }),
        ("Magic Trick DAL_608", game => Play(game, "DAL_608")),
        ("Ethereal Conjurer LOE_003", game => Play(game, "LOE_003")),
        ("Runed Orb CORE_BAR_541", game => Play(game, "CORE_BAR_541")),
        ("Shifting Scroll LOOT_104", game => { var e = Entity.FromCard(game.Player1, Cards.FromId("LOOT_104")); Generic.AddHandPhase(game.Player1, e); Round(game); Round(game); Round(game); return e; }),
    };
    foreach (var (name, cast) in casts)
    {
        int inRun = Offered(true, cast, 60), control = Offered(false, cast, 60);
        Check($"{name}: no Secret in 60 run matches; the same casts without the rule do offer them", inRun == 0 && control > 0, $"run {inRun}, control {control}");
    }
    // Phase 3 I2: a catalog run match (RunMatchPlan.PlayerGeneratedSecrets) lifts the rule for the run player only.
    foreach (var (name, cast) in casts.Where(c => c.name.StartsWith("Magic Trick") || c.name.StartsWith("Ethereal Conjurer")))
    {
        int inCatalog = Offered(true, cast, 60, catalogRun: true);
        Check($"{name}: a catalog run's player is offered Secrets (the generated-Secret lift)", inCatalog > 0, $"catalog {inCatalog}");
    }
    var cplan = new RunMatchPlan { PlayerHero = Cards.FromId("HERO_08"), PlayerDeck = Deck(10), BossHero = Cards.FromId("HERO_01"), BossDeck = Deck(30), BossHealth = 30, PlayerGeneratedSecrets = true };
    var ccfg = new GameConfig { RandomSeed = 77, Shuffle = true, SkipMulligan = false, History = false, Logging = false, FormatType = FormatType.FT_WILD };
    DungeonRunSetup.Configure(ccfg, cplan);
    var cg = new Game(ccfg); cg.StartGame();
    bool Sides(Game x) => x != null && !x.ExcludesGeneratedSecretsFor(1) && x.ExcludesGeneratedSecretsFor(2) && !x.ExcludeGeneratedSecrets;
    Game csnap; try { csnap = Game.LoadSnapshot(cg.SaveSnapshot()); } catch (Exception ex) { csnap = null; Console.WriteLine("snapshot: " + ex.Message); }
    Check("catalog run match: player 1 may generate Secrets, player 2 (the boss) may not; clones and a snapshot keep both flags",
        Sides(cg) && Sides(cg.Clone()) && Sides(cg.CloneExact()) && Sides(csnap));
}
{
    // Diamond Spellstone (bundle_priest_spells) cast with an empty graveyard used to throw (list[0] of an empty list).
    var g = Start();
    GiveMana(g.Player1, 10);
    var e = Entity.FromCard(g.Player1, Cards.FromId("LOOT_507")); Generic.AddHandPhase(g.Player1, e);
    bool ok; try { ok = g.Process(g.Player1.Options().First(o => o is PlayCardTask pc && pc.Source == e)); } catch (Exception) { ok = false; }
    Check("Diamond Spellstone with an empty graveyard resurrects nothing, no fault", ok && g.Player1.BoardZone.Count == 0 && e.Zone?.Type == Zone.GRAVEYARD);
}

Console.WriteLine($"{passes} passed, {failures} failed");
return failures;


