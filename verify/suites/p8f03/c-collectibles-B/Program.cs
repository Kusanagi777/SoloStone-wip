using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.CardSets.DungeonRun;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

// Phase 8 family 03, unscripted collectibles B (Uldum ULD_131..ULD_274): every card's text asserted in real engine games
// started through the run setup path (RunMatchPlan -> DungeonRunSetup.Configure / ApplyAfterStart), for the run player and,
// where the side matters (generation, hidden zones), for the boss; then the admission rows, the generation lists, the
// shipped encounter decks that now hold the cards, and the fixture rows the bosspilot mode plays.
int failures = 0, passes = 0;
void Check(string name, bool ok, string detail = "")
{
    if (ok) passes++; else failures++;
    Console.WriteLine((ok ? "PASS " : "FAIL ") + name + (detail.Length > 0 ? "  (" + detail + ")" : ""));
}
void Guard(string name, Action body)
{
    try { body(); }
    catch (Exception e) { failures++; Console.WriteLine("FAIL " + name + " threw " + e.GetType().Name + ": " + e.Message.Split('\n')[0] + " @ " + (e.StackTrace ?? "").Split('\n').FirstOrDefault()?.Trim()); }
}
static object FromJson(System.Text.Json.JsonElement e)
{
    switch (e.ValueKind)
    {
        case System.Text.Json.JsonValueKind.Object: { var d = new Dictionary<string, object>(); foreach (var p in e.EnumerateObject()) d[p.Name] = FromJson(p.Value); return d; }
        case System.Text.Json.JsonValueKind.Array: { var l = new List<object>(); foreach (var x in e.EnumerateArray()) l.Add(FromJson(x)); return l; }
        case System.Text.Json.JsonValueKind.String: return e.GetString();
        case System.Text.Json.JsonValueKind.Number: return e.TryGetInt64(out long v) ? (object)v : e.GetDouble();
        case System.Text.Json.JsonValueKind.True: return true;
        case System.Text.Json.JsonValueKind.False: return false;
        default: return null;
    }
}
NpcWorldJson.Parser = text => { using var doc = System.Text.Json.JsonDocument.Parse(text); return FromJson(doc.RootElement); };
Card C(string id) => Cards.FromId(id);
string root = VerifyPaths.Data;
string fixtures = VerifyPaths.Fixture("p8f03/fixtures");
string R(string f) => File.ReadAllText(Path.Combine(root, f));
DungeonRunData Load(string encounters = null) => DungeonRunData.Parse(R("dungeon-run/run-v1.json"), R("dungeon-run/pool-v1.json"), R("npc/decks-v1.json"), R("dungeon-run/catalog-v1.json"),
    R("dungeon-run/schedule-v1.json"), R("dungeon-run/tavern-v1.json"), encounters ?? R("dungeon-run/encounters-v1.json"));
var data = Load();

string[] family = { "ULD_131", "ULD_135", "ULD_138", "ULD_143", "ULD_151", "ULD_154", "ULD_158", "ULD_169", "ULD_172", "ULD_173", "ULD_177", "ULD_179", "ULD_194", "ULD_195", "ULD_205",
    "ULD_206", "ULD_208", "ULD_209", "ULD_212", "ULD_215", "ULD_216", "ULD_236", "ULD_239", "ULD_253", "ULD_256", "ULD_258", "ULD_262", "ULD_268", "ULD_270", "ULD_274" };
string[] support = { "ULD_131p", "ULD_135a", "ULD_135b", "ULD_135at", "ULD_143e", "ULD_154t", "ULD_173e", "ULD_179e", "ULD_209t", "ULD_215t", "ULD_256e", "ULD_258e", "ULD_262e" };
const string Yeti = "CS2_182", Grizzly = "CS2_125", Golem = "CS2_186";

string[] filler = Enumerable.Repeat(Yeti, 30).ToArray();
RunMatchPlan Plan(string hero = "HERO_08", string power = "CS2_034", string[] deck = null, string bossHero = "HERO_01", string bossPower = "CS2_102", string[] bossDeck = null,
    bool playerSecrets = true) => new RunMatchPlan
{
    PlayerHero = C(hero), PlayerHeroPowerId = power, PlayerDeck = (deck ?? filler).Select(C).ToList(),
    BossHero = C(bossHero), BossHeroPowerId = bossPower, BossHealth = 30, BossDeck = (bossDeck ?? filler).Select(C).ToList(),
    PlayerGeneratedSecrets = playerSecrets, BossGeneratedSecrets = false, EncounterId = "test", EncounterDeck = true,
    ExcludedGenerated = DungeonRunAdmission.GeneratedRejects(playerSecrets), ExcludedGeneratedBoss = DungeonRunAdmission.GeneratedRejectsBoss(playerSecrets),
};
Game Ready(RunMatchPlan plan, int seed = 7, bool history = true)
{
    var cfg = new GameConfig { RandomSeed = seed, Shuffle = true, SkipMulligan = false, History = history, Logging = false, FormatType = FormatType.FT_WILD };
    DungeonRunSetup.Configure(cfg, plan);
    var g = new Game(cfg);
    g.StartGame();
    DungeonRunSetup.ApplyAfterStart(g, plan);
    g.Process(ChooseTask.Mulligan(g.Player1, new List<int>()));
    g.Process(ChooseTask.Mulligan(g.Player2, new List<int>()));
    g.MainBegin();
    if (g.CurrentPlayer != g.Player1) throw new Exception("run player not first");
    Mana(g.Player1, 10);
    return g;
}
void Mana(Controller c, int n) { c.BaseMana = n; c.UsedMana = 0; c.OverloadLocked = 0; c.OverloadOwed = 0; }
void Clear(Controller c) { foreach (var e in c.HandZone.ToArray()) { c.HandZone.Remove(e); c.SetasideZone.Add(e); } }
IPlayable ToHand(Game g, Controller c, string id)
{
    while (c.HandZone.Count >= 9) { var old = c.HandZone[0]; c.HandZone.Remove(old); c.SetasideZone.Add(old); }
    var e = Entity.FromCard(c, C(id)); Generic.AddHandPhase.Invoke(c, e); g.DeathProcessingAndAuraUpdate(); return e;
}
Minion Board(Game g, Controller c, string id) { var m = (Minion)Entity.FromCard(c, C(id)); Generic.SummonBlock(g, m, -1, c.Hero); g.DeathProcessingAndAuraUpdate(); return m; }
bool Play(Game g, IPlayable card, ICharacter target = null, int chooseOne = 0) => g.Process(PlayCardTask.Any(g.CurrentPlayer, card, target, -1, chooseOne));
void EndTurn(Game g) => g.Process(EndTurnTask.Any(g.CurrentPlayer));
// A full round: back to the same player's turn, with ten Mana.
void Round(Game g) { var who = g.CurrentPlayer; EndTurn(g); EndTurn(g); Mana(who, 10); }
void ToBoss(Game g) { if (g.CurrentPlayer == g.Player1) EndTurn(g); Mana(g.Player2, 10); }
void ToPlayer(Game g) { if (g.CurrentPlayer == g.Player2) EndTurn(g); Mana(g.Player1, 10); }
void Kill(Game g, Minion m) { m.Damage += m.Health; g.DeathProcessingAndAuraUpdate(); }
bool Attack(Game g, Minion attacker, ICharacter target) => g.Process(MinionAttackTask.Any(g.CurrentPlayer, attacker, target));
bool CanAttack(Game g, Minion attacker, ICharacter target) => g.CurrentPlayer.Options().OfType<MinionAttackTask>().Any(o => o.Source == attacker && o.Target == target);
bool Pick(Game g, Controller c, int id)
{
    var decision = g.GetPendingDecisions().FirstOrDefault(d => d.ControllerId == c.Id && d.Kind != SabberStoneCore.Model.Decisions.DecisionKind.Options);
    if (decision != null) return g.TryAnswer(new SabberStoneCore.Model.Decisions.DecisionAnswer(decision.Id, decision.ControllerId, new[] { id }), out _);
    return g.Process(ChooseTask.Pick(c, id));
}
List<Card> Offers(Game g, Controller c) => c.Choice == null ? new List<Card>() : c.Choice.Choices.Select(id => g.IdEntityDic[id].Card).ToList();
List<string> HandIds(Controller c) => c.HandZone.Select(e => e.Card.Id).ToList();
string[] Deck(params (string id, int n)[] cards) => cards.SelectMany(c => Enumerable.Repeat(c.id, c.n)).ToArray();
int Tag(IPlayable p, GameTag tag) => p[tag];

// ============================================================ B1 every id is scripted
Check("B1 the thirty family cards are scripted (Card.Implemented)", family.All(id => C(id).Implemented), string.Join(",", family.Where(id => !C(id).Implemented)));
Check("B1 their options, tokens, enchantments and the reward power are scripted", support.All(id => C(id).Implemented), string.Join(",", support.Where(id => !C(id).Implemented)));

// ============================================================ B2-B6 keyword cards (ULD_194, ULD_205, ULD_206, ULD_274)
Guard("Keywords", () =>
{
    var g = Ready(Plan());
    var p = g.Player1; var b = g.Player2;
    var scorpid = (Minion)ToHand(g, p, "ULD_194");
    Check("B2 ULD_194 Wasteland Scorpid costs (7) and is a 3/9 Beast with Poisonous", scorpid.Cost == 7 && Play(g, scorpid) && scorpid.Zone == p.BoardZone && scorpid.AttackDamage == 3
        && scorpid.Health == 9 && scorpid.Poisonous && scorpid.Card.IsRace(Race.BEAST));
    var victim = Board(g, b, Golem);
    Round(g);
    Check("B3 its Poisonous destroys a minion it damages", Attack(g, scorpid, victim) && victim.Zone != b.BoardZone && scorpid.Damage == 7 && scorpid.Zone == p.BoardZone);
    var taker = (Minion)ToHand(g, p, "ULD_205");
    Mana(p, 10);
    Play(g, taker);
    Kill(g, taker);
    var reborn = p.BoardZone.FirstOrDefault(m => m.Card.Id == "ULD_205");
    Check("B4 ULD_205 Candletaker (3 mana 3/2) has Reborn: it returns once with 1 Health", taker.Card.Cost == 3 && taker.Zone != p.BoardZone && reborn != null && reborn != taker
        && reborn.Health == 1 && reborn.AttackDamage == 3 && Tag(reborn, GameTag.HAS_BEEN_REBORN) == 1);
    Kill(g, reborn);
    Check("B4 the reborn Candletaker does not return again", !p.BoardZone.Any(m => m.Card.Id == "ULD_205"));
    var enemy = Board(g, b, Yeti);
    var mummy = (Minion)ToHand(g, p, "ULD_206");
    Mana(p, 10);
    Play(g, mummy);
    Check("B5 ULD_206 Restless Mummy (4 mana 3/2) has Rush (it can attack a minion, not the hero, the turn it is played) and Reborn", mummy.Card.Cost == 4 && mummy.AttackDamage == 3 && mummy.Health == 2
        && mummy.IsRush && Tag(mummy, GameTag.REBORN) == 1 && CanAttack(g, mummy, enemy) && !CanAttack(g, mummy, b.Hero));
    Attack(g, mummy, enemy);
    var mummy2 = p.BoardZone.FirstOrDefault(m => m.Card.Id == "ULD_206");
    Check("B5 killed in its attack it is reborn with 1 Health", mummy.Zone != p.BoardZone && mummy2 != null && mummy2.Health == 1 && enemy.Damage == 3);
    var assassin = (Minion)ToHand(g, p, "ULD_274");
    Mana(p, 10);
    Play(g, assassin);
    Check("B6 ULD_274 Wasteland Assassin (5 mana 4/2) has Stealth and Reborn", assassin.Card.Cost == 5 && assassin.AttackDamage == 4 && assassin.Health == 2 && assassin.HasStealth && Tag(assassin, GameTag.REBORN) == 1);
    Kill(g, assassin);
    Check("B6 it returns with 1 Health", p.BoardZone.Any(m => m.Card.Id == "ULD_274" && m.Health == 1));
});

// ============================================================ B7-B14 ULD_131 Untapped Potential and ULD_131p Ossirian Tear
Guard("Quest", () =>
{
    var g = Ready(Plan("HERO_06", "CS2_017"));
    var p = g.Player1; var b = g.Player2;
    var quest = (Spell)ToHand(g, p, "ULD_131");
    Check("B7 ULD_131 Untapped Potential costs (1) and enters play as the owner's Quest at 0 of 4", quest.Cost == 1 && Play(g, quest) && p.SecretZone.Quest == quest && quest.QuestProgress == 0
        && quest.QuestTotalProgress == 4 && p.UsedMana == 1 && quest.ActivatedTrigger != null);
    EndTurn(g);
    Check("B8 a turn ended with unspent Mana counts one", quest.QuestProgress == 1);
    EndTurn(g);
    Check("B8 the opponent's turn end counts nothing", quest.QuestProgress == 1 && g.CurrentPlayer == p);
    p.UsedMana = p.BaseMana;
    EndTurn(g); EndTurn(g);
    Check("B9 a turn ended with all Mana spent counts nothing", quest.QuestProgress == 1);
    Mana(p, 5); p.OverloadLocked = 5;
    EndTurn(g); EndTurn(g);
    Check("B9 Mana that is only locked by Overload is not unspent", quest.QuestProgress == 1);
    EndTurn(g); EndTurn(g);
    EndTurn(g); EndTurn(g);
    Check("B10 three turns counted: the Quest is still in play and the power is still the Druid's", quest.QuestProgress == 3 && p.SecretZone.Quest == quest && p.Hero.HeroPower.Card.Id == "CS2_017" && !p.ChooseBoth);
    var oldPower = p.Hero.HeroPower;
    EndTurn(g);
    var tear = p.Hero.HeroPower;
    Check("B11 the fourth turn completes it: the Quest leaves play revealed and the hero power becomes Ossirian Tear", quest.Zone == p.GraveyardZone && p.SecretZone.Quest == null && Tag(quest, GameTag.REVEALED) == 1
        && tear.Card.Id == "ULD_131p" && oldPower.Zone == p.SetasideZone && quest.ActivatedTrigger == null, tear.Card.Id);
    Check("B12 Ossirian Tear is a passive power (never an option) and makes its owner's Choose One cards have both effects; the opponent is unaffected", tear.IsPassiveHeroPower && p.ChooseBoth && !b.ChooseBoth
        && tear[EncounterRules.PowerActive] == 1 && tear.OngoingEffect != null);
    EndTurn(g);
    Mana(p, 10);
    Check("B12 it is not offered on its owner's turn", g.CurrentPlayer == p && !p.Options().Any(o => o is HeroPowerTask));
    p.Hero.Damage = 20;
    var oasis = ToHand(g, p, "ULD_135");
    Check("B13 with it Hidden Oasis does both: a 6/6 Taunt Ancient and 12 Health restored to the target", Play(g, oasis, p.Hero) && p.BoardZone.Count(m => m.Card.Id == "ULD_135at") == 1 && p.Hero.Damage == 8);
    var clone = g.Clone();
    Check("B13 a clone of the game keeps the aura", clone.Player1.ChooseBoth && !clone.Player2.ChooseBoth && clone.Player1.Hero.HeroPower.Card.Id == "ULD_131p");
    var heroCard = new[] { "ICC_832", "ICC_481", "ICC_831", "ICC_828", "ICC_829", "ICC_830", "ICC_833", "ICC_834", "ICC_827" }.Select(C).FirstOrDefault(c => c.Implemented);
    if (heroCard == null) Check("B14 a scripted hero card exists for the replacement check", false);
    else
    {
        Mana(p, 10);
        var played = ToHand(g, p, heroCard.Id);
        bool ok = Play(g, played);
        while (p.Choice != null) Pick(g, p, p.Choice.Choices[0]);
        Check("B14 a hero card replaces the power and ends the aura (" + heroCard.Id + ")", ok && p.Hero.HeroPower.Card.Id != "ULD_131p" && !p.ChooseBoth, p.Hero.HeroPower.Card.Id + " " + p.ChooseBoth);
    }
    // The boss as the Quest's owner (a card of its deck): its power is replaced the same way.
    var g2 = Ready(Plan());
    ToBoss(g2);
    var q2 = (Spell)ToHand(g2, g2.Player2, "ULD_131");
    Play(g2, q2);
    for (int i = 0; i < 4; i++) { EndTurn(g2); EndTurn(g2); }
    Check("B14 the boss completes it too (four of its own turn ends with Mana left): its hero power becomes the passive Ossirian Tear", q2.Zone == g2.Player2.GraveyardZone
        && g2.Player2.Hero.HeroPower.Card.Id == "ULD_131p" && g2.Player2.ChooseBoth && !g2.Player1.ChooseBoth && !g2.Player2.Options().Any(o => o is HeroPowerTask), g2.Player2.Hero.HeroPower.Card.Id + " " + q2.QuestProgress);
});

// ============================================================ B15-B17 ULD_135 Hidden Oasis
Guard("Oasis", () =>
{
    var g = Ready(Plan("HERO_06", "CS2_017"));
    var p = g.Player1;
    p.Hero.Damage = 20;
    var first = ToHand(g, p, "ULD_135");
    Check("B15 ULD_135 Hidden Oasis costs (6); its first option summons a 6/6 Vir'naal Ancient with Taunt and heals nothing", first.Cost == 6 && g.Process(PlayCardTask.Spell(p, first, 1))
        && p.BoardZone.Count == 1 && p.BoardZone[0].Card.Id == "ULD_135at" && p.BoardZone[0].AttackDamage == 6 && p.BoardZone[0].Health == 6 && p.BoardZone[0].HasTaunt && p.Hero.Damage == 20);
    Mana(p, 10);
    var second = ToHand(g, p, "ULD_135");
    Check("B16 its second option restores 12 Health to the target and summons nothing", g.Process(PlayCardTask.SpellTarget(p, second, p.Hero, 2)) && p.Hero.Damage == 8 && p.BoardZone.Count == 1);
    Mana(p, 10);
    var ancient = p.BoardZone[0];
    ancient.Damage = 5;
    var third = ToHand(g, p, "ULD_135");
    Check("B17 the heal can target a minion", g.Process(PlayCardTask.SpellTarget(p, third, ancient, 2)) && ancient.Damage == 0);
});

// ============================================================ B18-B20 ULD_138 Anubisath Defender
Guard("Defender", () =>
{
    var g = Ready(Plan());
    var p = g.Player1; var b = g.Player2;
    var defender = (Minion)ToHand(g, p, "ULD_138");
    Check("B18 ULD_138 Anubisath Defender is a 5-Cost 3/5 with Taunt", defender.Cost == 5 && defender.Card[GameTag.ATK] == 3 && defender.Card[GameTag.HEALTH] == 5 && defender.Card[GameTag.TAUNT] == 1);
    Play(g, ToHand(g, p, "CS2_029"), b.Hero); // Fireball (4)
    Check("B19 a (4)-Cost spell does not change its Cost", defender.Cost == 5);
    Mana(p, 10);
    Play(g, ToHand(g, p, "CS2_032")); // Flamestrike (7)
    Check("B19 after a spell that cost (5) or more this turn it costs (0)", defender.Cost == 0);
    Round(g);
    Check("B20 the next turn it costs (5) again", defender.Cost == 5);
    ToBoss(g);
    Play(g, ToHand(g, b, "CS2_032"));
    Check("B20 the opponent's spell does not count", defender.Cost == 5);
    ToPlayer(g);
    Play(g, ToHand(g, p, "CS2_032"));
    Check("B20 played for (0) it lands as a 3/5 Taunt", defender.Cost == 0 && Play(g, defender) && p.UsedMana == 7 && defender.Zone == p.BoardZone && defender.HasTaunt);
});

// ============================================================ B21 ULD_143 Pharaoh's Blessing
Guard("Blessing", () =>
{
    var g = Ready(Plan("HERO_04", "CS2_101"));
    var p = g.Player1;
    var yeti = Board(g, p, Yeti);
    var spell = ToHand(g, p, "ULD_143");
    Check("B21 ULD_143 Pharaoh's Blessing (6): +4/+4, Divine Shield and Taunt", spell.Cost == 6 && Play(g, spell, yeti) && yeti.AttackDamage == 8 && yeti.Health == 9 && yeti.HasDivineShield && yeti.HasTaunt
        && yeti.AppliedEnchantments.Any(e => e.Card.Id == "ULD_143e"));
});

// ============================================================ B22-B23 ULD_151 Ramkahen Wildtamer
Guard("Wildtamer", () =>
{
    var g = Ready(Plan("HERO_05", "DS1h_292"));
    var p = g.Player1;
    Clear(p);
    ToHand(g, p, "CS2_120"); ToHand(g, p, Yeti);
    var tamer = (Minion)ToHand(g, p, "ULD_151");
    Check("B22 ULD_151 Ramkahen Wildtamer (3 mana 4/3) copies a random Beast of its owner's hand", tamer.Cost == 3 && Play(g, tamer) && tamer.AttackDamage == 4 && tamer.Health == 3
        && HandIds(p).OrderBy(x => x).SequenceEqual(new[] { "CS2_120", "CS2_120", Yeti }), string.Join(",", HandIds(p)));
    Clear(p);
    ToHand(g, p, Yeti);
    Mana(p, 10);
    Check("B23 with no Beast in hand it copies nothing", Play(g, ToHand(g, p, "ULD_151")) && HandIds(p).SequenceEqual(new[] { Yeti }));
});

// ============================================================ B24-B25 ULD_154 Hyena Alpha
Guard("Hyena", () =>
{
    var g = Ready(Plan("HERO_05", "DS1h_292"));
    var p = g.Player1;
    Check("B24 ULD_154 Hyena Alpha (4 mana 3/3) summons nothing without a Secret", Play(g, ToHand(g, p, "ULD_154")) && p.BoardZone.Count == 1 && p.BoardZone[0].AttackDamage == 3 && p.BoardZone[0].Health == 3);
    DungeonRunEncounters.PutSecret(p, C("EX1_610"));
    Mana(p, 10);
    var alpha = (Minion)ToHand(g, p, "ULD_154");
    Play(g, alpha);
    var hyenas = p.BoardZone.Where(m => m.Card.Id == "ULD_154t").ToList();
    Check("B25 with a Secret in play it summons two 2/2 Hyenas (Beasts) to its right", hyenas.Count == 2 && hyenas.All(h => h.AttackDamage == 2 && h.Health == 2 && h.Card.IsRace(Race.BEAST) && h.ZonePosition > alpha.ZonePosition)
        && p.SecretZone.Count == 1);
});

// ============================================================ B26 ULD_158 Sandstorm Elemental
Guard("Sandstorm", () =>
{
    var g = Ready(Plan("HERO_02", "CS2_049"));
    var p = g.Player1; var b = g.Player2;
    var mine = Board(g, p, Yeti); var e1 = Board(g, b, Yeti); var e2 = Board(g, b, Golem);
    var elemental = (Minion)ToHand(g, p, "ULD_158");
    Check("B26 ULD_158 Sandstorm Elemental (2 mana 2/2): 1 damage to all enemy minions, Overload (1)", elemental.Cost == 2 && Play(g, elemental) && e1.Damage == 1 && e2.Damage == 1 && mine.Damage == 0
        && b.Hero.Damage == 0 && p.OverloadOwed == 1 && elemental.Card.IsRace(Race.ELEMENTAL));
});

// ============================================================ B27 ULD_169 Mogu Fleshshaper
Guard("Mogu", () =>
{
    var g = Ready(Plan("HERO_02", "CS2_049"));
    var p = g.Player1; var b = g.Player2;
    var mogu = (Minion)ToHand(g, p, "ULD_169");
    bool printed = mogu.Cost == 9 && mogu.Card[GameTag.RUSH] == 1 && mogu.Card[GameTag.ATK] == 3 && mogu.Card[GameTag.HEALTH] == 4;
    Board(g, p, Yeti); Board(g, p, Yeti); Board(g, b, Yeti);
    bool three = mogu.Cost == 6;
    for (int i = 0; i < 4; i++) Board(g, b, Yeti);
    for (int i = 0; i < 4; i++) Board(g, p, Yeti);
    Check("B27 ULD_169 Mogu Fleshshaper (9 mana 3/4 Rush) costs (1) less for each minion on the battlefield, both sides, never below (0)", printed && three && mogu.Cost == 0, mogu.Cost.ToString());
});

// ============================================================ B28-B30 ULD_172 Plague of Murlocs
Guard("Plague", () =>
{
    var g = Ready(Plan("HERO_02", "CS2_049"));
    var p = g.Player1; var b = g.Player2;
    Board(g, p, Yeti); Board(g, p, Golem); Board(g, b, Yeti); Board(g, b, Grizzly);
    var spell = ToHand(g, p, "ULD_172");
    bool cast = spell.Cost == 3 && Play(g, spell);
    var all = p.BoardZone.Concat(b.BoardZone).ToList();
    Check("B28 ULD_172 Plague of Murlocs (3) transforms every minion of both boards into a scripted Murloc; each side keeps its count", cast && p.BoardZone.Count == 2 && b.BoardZone.Count == 2
        && all.All(m => m.Card.Implemented && (m.Card.IsRace(Race.MURLOC) || m.Card.IsRace(Race.ALL)) && m.Damage == 0), string.Join(",", all.Select(m => m.Card.Id)));
    var seen = new HashSet<string>();
    for (int seed = 1; seed <= 12; seed++)
    {
        var gs = Ready(Plan("HERO_02", "CS2_049"), seed, history: false);
        Board(gs, gs.Player1, Yeti); Board(gs, gs.Player2, Yeti);
        Play(gs, ToHand(gs, gs.Player1, "ULD_172"));
        foreach (var m in gs.Player1.BoardZone.Concat(gs.Player2.BoardZone)) seen.Add(m.Card.Id);
    }
    Check("B29 the Murlocs are random (several different ones over 12 seeds), none unscripted, none a run exclusion", seen.Count >= 4 && seen.All(id => C(id).Implemented && C(id).Type == CardType.MINION)
        && !seen.Intersect(DungeonRunAdmission.GeneratedRejects(true)).Any(), string.Join(",", seen.OrderBy(x => x)));
    // A run board shrine is not transformed: it goes dormant, as for every transform (ShrineRules).
    var g3 = Ready(Plan("HERO_02", "CS2_049"));
    var shrine = ShrineRules.Place(g3.Player1, "TRLA_104");
    var other = Board(g3, g3.Player1, Yeti);
    Play(g3, ToHand(g3, g3.Player1, "ULD_172"));
    var shrines = ShrineRules.Of(g3.Player1);
    Check("B30 a run board shrine goes dormant instead of becoming a Murloc; the minion next to it is transformed", shrines.Count == 1 && ShrineRules.IsDormantForm(shrines[0])
        && g3.Player1.BoardZone.Count == 2 && g3.Player1.BoardZone.Any(m => !ShrineRules.IsMarked(m) && m.Card.Id != Yeti), string.Join(",", g3.Player1.BoardZone.Select(m => m.Card.Id)));
});

// ============================================================ B31-B32 ULD_173 Vessina
Guard("Vessina", () =>
{
    var g = Ready(Plan("HERO_02", "CS2_049"));
    var p = g.Player1; var b = g.Player2;
    var vessina = (Minion)ToHand(g, p, "ULD_173");
    Play(g, vessina);
    var yeti = Board(g, p, Yeti); var foe = Board(g, b, Yeti);
    Check("B31 ULD_173 Vessina (4 mana 2/6): no bonus while her owner is not Overloaded", vessina.Card.Cost == 4 && vessina.AttackDamage == 2 && vessina.Health == 6 && yeti.AttackDamage == 4);
    Mana(p, 10);
    Play(g, ToHand(g, p, "ULD_158"));
    var elemental = p.BoardZone.First(m => m.Card.Id == "ULD_158");
    Check("B31 once her owner is Overloaded (Mana owed) her other minions have +2 Attack; she and enemy minions do not", p.OverloadOwed == 1 && yeti.AttackDamage == 6 && elemental.AttackDamage == 4
        && vessina.AttackDamage == 2 && foe.AttackDamage == 4);
    // A clone of the game (what the boss's search plays on) keeps the bonus and drops it by the same rule.
    var clone = g.Clone();
    var cloneYeti = clone.Player1.BoardZone.First(m => m.Card.Id == Yeti);
    bool cloned = cloneYeti.AttackDamage == 6;
    clone.Player1.OverloadOwed = 0; clone.DeathProcessingAndAuraUpdate();
    Check("B31 a clone of the game keeps the aura and re-checks it; the original is untouched", cloned && cloneYeti.AttackDamage == 4 && yeti.AttackDamage == 6);
    // Two Vessinas: each gives the other and every other minion +2.
    var second = Board(g, p, "ULD_173");
    Check("B31 a second Vessina: each has +2 from the other, the other minions +4", vessina.AttackDamage == 4 && second.AttackDamage == 4 && yeti.AttackDamage == 8);
    Kill(g, second);
    Round(g);
    p.OverloadLocked = 1; p.OverloadOwed = 0; g.DeathProcessingAndAuraUpdate();
    bool locked = yeti.AttackDamage == 6;
    p.OverloadLocked = 0; g.DeathProcessingAndAuraUpdate();
    Check("B32 locked Mana Crystals count as Overloaded too; with none locked or owed the bonus is gone", locked && yeti.AttackDamage == 4);
    p.OverloadOwed = 2; g.DeathProcessingAndAuraUpdate();
    Kill(g, vessina);
    Check("B32 the bonus ends when she leaves play", yeti.AttackDamage == 4);
});

// ============================================================ B33 ULD_177 Octosari
Guard("Octosari", () =>
{
    var g = Ready(Plan());
    var p = g.Player1;
    var octo = Board(g, p, "ULD_177");
    Clear(p);
    int deck = p.DeckZone.Count;
    Kill(g, octo);
    Check("B33 ULD_177 Octosari (8 mana 8/8 Beast): Deathrattle draws 8 cards", octo.Card.Cost == 8 && octo.Card[GameTag.ATK] == 8 && octo.Card[GameTag.HEALTH] == 8 && p.HandZone.Count == 8 && p.DeckZone.Count == deck - 8);
});

// ============================================================ B34-B35 ULD_179 Phalanx Commander
Guard("Phalanx", () =>
{
    var g = Ready(Plan());
    var p = g.Player1; var b = g.Player2;
    var commander = (Minion)ToHand(g, p, "ULD_179");
    Play(g, commander);
    var grizzly = Board(g, p, Grizzly); var yeti = Board(g, p, Yeti); var foe = Board(g, b, Grizzly);
    Check("B34 ULD_179 Phalanx Commander (5 mana 4/5): its owner's Taunt minions have +2 Attack; others and enemy Taunt minions do not", commander.Card.Cost == 5 && commander.AttackDamage == 4
        && commander.Health == 5 && grizzly.AttackDamage == 5 && yeti.AttackDamage == 4 && foe.AttackDamage == 3);
    Mana(p, 10);
    Play(g, ToHand(g, p, "ULD_143"), yeti);
    Check("B35 a minion that gains Taunt gains the bonus", yeti.HasTaunt && yeti.AttackDamage == 10);
    Kill(g, commander);
    Check("B35 the bonus ends when the Commander leaves play", grizzly.AttackDamage == 3 && yeti.AttackDamage == 8);
});

// ============================================================ B36-B37 ULD_195 Frightened Flunky
Guard("Flunky", () =>
{
    var g = Ready(Plan("HERO_01", "CS2_102"));
    var p = g.Player1;
    Clear(p);
    var flunky = (Minion)ToHand(g, p, "ULD_195");
    bool played = flunky.Cost == 2 && Play(g, flunky);
    var offers = Offers(g, p);
    Check("B36 ULD_195 Frightened Flunky (2 mana 2/2 Taunt) opens a Discover of three different scripted Taunt minions, never itself", played && flunky.HasTaunt && offers.Count == 3
        && offers.Select(c => c.Id).Distinct().Count() == 3 && offers.All(c => c.Implemented && c.Type == CardType.MINION && c[GameTag.TAUNT] == 1 && c.Id != "ULD_195"
            && (c.Class == CardClass.WARRIOR || c.Class == CardClass.NEUTRAL)), string.Join(",", offers.Select(c => c.Id)));
    string picked = offers[1].Id;
    Check("B37 the pick goes to the hand", Pick(g, p, p.Choice.Choices[1]) && p.Choice == null && HandIds(p).SequenceEqual(new[] { picked }), string.Join(",", HandIds(p)));
    // The boss as the Flunky's owner: the offers pass the boss's generation list too.
    var g2 = Ready(Plan());
    ToBoss(g2);
    Play(g2, ToHand(g2, g2.Player2, "ULD_195"));
    var bossOffers = Offers(g2, g2.Player2);
    Check("B37 a boss's Flunky offers three scripted Taunt minions outside the boss's generation rejections", bossOffers.Count == 3 && bossOffers.All(c => c.Implemented && c[GameTag.TAUNT] == 1)
        && !bossOffers.Select(c => c.Id).Intersect(DungeonRunAdmission.GeneratedRejects(true).Concat(DungeonRunAdmission.GeneratedRejectsBoss(true))).Any(), string.Join(",", bossOffers.Select(c => c.Id)));
});

// ============================================================ B38 ULD_208 Khartut Defender
Guard("Khartut", () =>
{
    var g = Ready(Plan());
    var p = g.Player1;
    var khartut = (Minion)ToHand(g, p, "ULD_208");
    bool played = khartut.Cost == 6 && Play(g, khartut) && khartut.AttackDamage == 3 && khartut.Health == 4 && khartut.HasTaunt && Tag(khartut, GameTag.REBORN) == 1;
    p.Hero.Damage = 10;
    Kill(g, khartut);
    var again = p.BoardZone.FirstOrDefault(m => m.Card.Id == "ULD_208");
    bool first = p.Hero.Damage == 7 && again != null && again.Health == 1 && again.HasTaunt;
    Kill(g, again);
    Check("B38 ULD_208 Khartut Defender (6 mana 3/4 Taunt, Reborn): each death restores 3 Health to its owner's hero, the reborn one's too", played && first && p.Hero.Damage == 4 && !p.BoardZone.Any(m => m.Card.Id == "ULD_208"),
        played + " " + first + " " + p.Hero.Damage);
});

// ============================================================ B39-B42 ULD_209 Vulpera Scoundrel, ULD_209t Mystery Choice!
Guard("Scoundrel", () =>
{
    var g = Ready(Plan());
    var p = g.Player1;
    Clear(p);
    var scoundrel = (Minion)ToHand(g, p, "ULD_209");
    bool played = scoundrel.Cost == 3 && Play(g, scoundrel) && scoundrel.AttackDamage == 2 && scoundrel.Health == 3;
    var offers = Offers(g, p);
    Check("B39 ULD_209 Vulpera Scoundrel (3 mana 2/3) opens a Discover of three different scripted Mage spells and, last, Mystery Choice!", played && offers.Count == 4 && offers[3].Id == "ULD_209t"
        && offers.Take(3).All(c => c.Implemented && c.Type == CardType.SPELL && c.Class == CardClass.MAGE && !c.IsQuest) && offers.Take(3).Select(c => c.Id).Distinct().Count() == 3,
        string.Join(",", offers.Select(c => c.Id)));
    string picked = offers[0].Id;
    Check("B40 a picked spell goes to the hand", Pick(g, p, p.Choice.Choices[0]) && p.Choice == null && HandIds(p).SequenceEqual(new[] { picked }), string.Join(",", HandIds(p)));
    var mystery = new HashSet<string>();
    bool oneEach = true;
    for (int seed = 1; seed <= 10; seed++)
    {
        var gs = Ready(Plan(), seed, history: false);
        Clear(gs.Player1);
        Play(gs, ToHand(gs, gs.Player1, "ULD_209"));
        var o = gs.Player1.Choice.Choices;
        Pick(gs, gs.Player1, o[o.Count - 1]);
        oneEach &= gs.Player1.HandZone.Count == 1 && gs.Player1.Choice == null;
        foreach (var card in gs.Player1.HandZone) mystery.Add(card.Card.Id);
    }
    Check("B41 the mystery choice adds one random scripted Mage spell instead (never itself; different spells over 10 seeds)", oneEach && mystery.Count >= 4 && !mystery.Contains("ULD_209t")
        && mystery.All(id => C(id).Implemented && C(id).Type == CardType.SPELL && C(id).Class == CardClass.MAGE), string.Join(",", mystery.OrderBy(x => x)));
    var g2 = Ready(Plan(), 3);
    ToBoss(g2);
    Clear(g2.Player2);
    Play(g2, ToHand(g2, g2.Player2, "ULD_209"));
    var bossOffers = Offers(g2, g2.Player2);
    Check("B42 a boss's Scoundrel offers Warrior spells that are no Secret and outside the boss's generation rejections, and the mystery choice", bossOffers.Count == 4 && bossOffers[3].Id == "ULD_209t"
        && bossOffers.Take(3).All(c => c.Implemented && c.Type == CardType.SPELL && c.Class == CardClass.WARRIOR && !c.IsSecret)
        && !bossOffers.Select(c => c.Id).Intersect(DungeonRunAdmission.GeneratedRejects(true).Concat(DungeonRunAdmission.GeneratedRejectsBoss(true))).Any(), string.Join(",", bossOffers.Select(c => c.Id)));
    // The boss's Mage Scoundrel never offers a Secret (the boss side's generated Secrets are off).
    bool noSecret = true; int rolls = 0;
    for (int seed = 1; seed <= 25; seed++)
    {
        var gs = Ready(Plan(bossHero: "HERO_08", bossPower: "CS2_034"), seed, history: false);
        ToBoss(gs);
        Clear(gs.Player2);
        Play(gs, ToHand(gs, gs.Player2, "ULD_209"));
        foreach (var c in Offers(gs, gs.Player2)) { rolls++; noSecret &= !c.IsSecret; }
        var o = gs.Player2.Choice.Choices;
        Pick(gs, gs.Player2, o[o.Count - 1]);
        noSecret &= gs.Player2.HandZone.All(c => !c.Card.IsSecret);
    }
    Check("B42 a Mage boss's Scoundrel never offers or rolls a Secret over 25 seeds", noSecret && rolls == 100);
});

// ============================================================ B43-B46 ULD_212 Wild Bloodstinger
Guard("Bloodstinger", () =>
{
    var g = Ready(Plan("HERO_05", "DS1h_292"));
    var p = g.Player1; var b = g.Player2;
    Clear(b);
    var golem = (Minion)ToHand(g, b, Golem); ToHand(g, b, "CS2_029");
    var stinger = (Minion)ToHand(g, p, "ULD_212");
    bool played = stinger.Cost == 6 && Play(g, stinger);
    Check("B43 ULD_212 Wild Bloodstinger (6 mana 6/9 Beast) summons the minion of the opponent's hand for the opponent and attacks it at once", played && golem.Zone == b.BoardZone && golem.Damage == 6
        && stinger.Damage == 7 && stinger.Zone == p.BoardZone && HandIds(b).SequenceEqual(new[] { "CS2_029" }) && golem.Controller == b, golem.Zone?.Type + " " + golem.Damage + " " + stinger.Damage);
    Check("B43 the forced attack spends no attack and the summoned minion is the hand's own entity", stinger.NumAttacksThisTurn == 0 && b.BoardZone.Count == 1 && b.BoardZone[0] == golem);
    Mana(p, 10);
    int hand = b.HandZone.Count;
    var second = (Minion)ToHand(g, p, "ULD_212");
    Check("B44 with no minion in the opponent's hand nothing is summoned or attacked", Play(g, second) && b.BoardZone.Count == 1 && second.Damage == 0 && b.HandZone.Count == hand);
    var g2 = Ready(Plan("HERO_05", "DS1h_292"));
    Clear(g2.Player2);
    var held = ToHand(g2, g2.Player2, Yeti);
    while (!g2.Player2.BoardZone.IsFull) Board(g2, g2.Player2, Yeti);
    var third = (Minion)ToHand(g2, g2.Player1, "ULD_212");
    Check("B45 with a full opposing board nothing is pulled", Play(g2, third) && held.Zone == g2.Player2.HandZone && third.Damage == 0 && g2.Player2.BoardZone.Count == 7);
    // A small minion dies to the attack; over seeds the pull is random among the hand's minions.
    var pulled = new HashSet<string>(); bool alwaysOne = true;
    for (int seed = 1; seed <= 12; seed++)
    {
        var gs = Ready(Plan("HERO_05", "DS1h_292"), seed, history: false);
        Clear(gs.Player2);
        ToHand(gs, gs.Player2, Yeti); ToHand(gs, gs.Player2, Grizzly); ToHand(gs, gs.Player2, Golem);
        var s = (Minion)ToHand(gs, gs.Player1, "ULD_212");
        Play(gs, s);
        alwaysOne &= gs.Player2.HandZone.Count == 2 && gs.Player2.BoardZone.Count + gs.Player2.GraveyardZone.Count(x => x is Minion) == 1 && s.Damage > 0;
        foreach (string id in new[] { Yeti, Grizzly, Golem }) if (!gs.Player2.HandZone.Any(c => c.Card.Id == id)) pulled.Add(id);
    }
    Check("B46 one random minion of the hand is pulled each time (all three occur over 12 seeds); a small one dies to the attack", alwaysOne && pulled.Count == 3, string.Join(",", pulled));
    // The boss plays it against the run player's hand.
    var g3 = Ready(Plan());
    Clear(g3.Player1);
    var mine = (Minion)ToHand(g3, g3.Player1, Golem);
    ToBoss(g3);
    var bossStinger = (Minion)ToHand(g3, g3.Player2, "ULD_212");
    Check("B46 a boss's Bloodstinger pulls the run player's minion the same way", Play(g3, bossStinger) && mine.Zone == g3.Player1.BoardZone && mine.Damage == 6 && bossStinger.Damage == 7 && g3.Player1.HandZone.Count == 0);
});

// ============================================================ B47 ULD_215 Wrapped Golem
Guard("WrappedGolem", () =>
{
    var g = Ready(Plan());
    var p = g.Player1; var b = g.Player2;
    var golem = (Minion)ToHand(g, p, "ULD_215");
    bool played = golem.Cost == 7 && Play(g, golem) && golem.AttackDamage == 7 && golem.Health == 5 && Tag(golem, GameTag.REBORN) == 1 && p.BoardZone.Count == 1;
    EndTurn(g);
    bool one = p.BoardZone.Count == 2 && p.BoardZone[1].Card.Id == "ULD_215t" && p.BoardZone[1].AttackDamage == 1 && p.BoardZone[1].Health == 1 && p.BoardZone[1].HasTaunt;
    EndTurn(g);
    Check("B47 ULD_215 Wrapped Golem (7 mana 7/5 Reborn): at the end of its owner's turn (not the opponent's) a 1/1 Scarab with Taunt is summoned to its right", played && one && p.BoardZone.Count == 2 && b.BoardZone.Count == 0,
        played + " " + one + " " + p.BoardZone.Count);
});

// ============================================================ B48-B52 ULD_216 Puzzle Box of Yogg-Saron
Guard("PuzzleBox", () =>
{
    // The cast history holds two facts per cast (started, then completed or countered): one per cast is read here.
    int Casts(Game g) => g.HistoryFacts.Query.Casts().Count();
    List<SpellCastFact> Started(Game g, int skip) => g.HistoryFacts.Query.Casts().Skip(skip).Where(f => f.Outcome == HistoryCastOutcome.Started).ToList();
    var cast = new List<Card>(); bool tenEach = true, alive = true; var secretsInPlay = 0;
    for (int seed = 1; seed <= 12; seed++)
    {
        var g = Ready(Plan(), seed);
        var p = g.Player1;
        Board(g, p, Yeti); Board(g, g.Player2, Yeti);
        var box = ToHand(g, p, "ULD_216");
        int before = Casts(g);
        bool ok = box.Cost == 10 && Play(g, box);
        var facts = Started(g, before);
        if (Environment.GetEnvironmentVariable("DEBUG_BOX") == "1" && seed == 1) foreach (var f in facts) Console.WriteLine($"  cast seq {f.Sequence} {(f.DefinitionKnown ? Cards.FromAssetId(f.DbfId).Id : "?")} kind {f.CasterKind} outcome {f.Outcome} id {f.CastId} controller {f.ControllerId}");
        var cards = facts.Where(f => f.DefinitionKnown).Select(f => Cards.FromAssetId(f.DbfId)).ToList();
        bool over = g.State == State.COMPLETE || g.Player1.Hero.Health <= 0 || g.Player2.Hero.Health <= 0;
        tenEach &= ok && cards.Count >= 1 && cards[0].Id == "ULD_216" && (over ? cards.Count <= 11 : cards.Count == 11) && facts.Skip(1).All(f => f.ControllerId == p.Id);
        alive &= g.Player1.Choice == null && g.Player2.Choice == null;
        cast.AddRange(cards.Skip(1));
        secretsInPlay += p.SecretZone.Count;
    }
    Check("B48 ULD_216 Puzzle Box of Yogg-Saron (10) casts ten spells for its owner (fewer only when a hero is down), leaving no open choice", tenEach && alive, cast.Count + " casts");
    Check("B49 the spells are random scripted spells: never the Box, a Quest or a hidden-stat card, and no run exclusion", cast.Select(c => c.Id).Distinct().Count() >= 40
        && cast.All(c => c.Implemented && c.Type == CardType.SPELL && c.Id != "ULD_216" && !c.IsQuest && !c.HideStat) && !cast.Select(c => c.Id).Intersect(DungeonRunAdmission.GeneratedRejects(true)).Any(),
        cast.Select(c => c.Id).Distinct().Count() + " different");
    Check("B50 a catalog run's player may cast Secrets with it (their classes are any)", cast.Any(c => c.IsSecret), cast.Count(c => c.IsSecret) + " Secret casts, " + secretsInPlay + " left in play");
    // The boss's Box: no Secret (the boss side's generated Secrets are off), nothing of the boss's generation rejections.
    var bossCast = new List<Card>(); bool bossOk = true;
    var rejected = new HashSet<string>(DungeonRunAdmission.GeneratedRejects(true).Concat(DungeonRunAdmission.GeneratedRejectsBoss(true)));
    for (int seed = 1; seed <= 12; seed++)
    {
        var g = Ready(Plan(), seed);
        ToBoss(g);
        var box = ToHand(g, g.Player2, "ULD_216");
        int before = Casts(g);
        bossOk &= Play(g, box) && g.Player2.Choice == null && g.Player1.Choice == null;
        bossCast.AddRange(Started(g, before).Skip(1).Where(f => f.DefinitionKnown).Select(f => Cards.FromAssetId(f.DbfId)));
        bossOk &= g.Player2.SecretZone.Count == 0;
    }
    Check("B51 a boss's Box casts no Secret and nothing its generation rejects", bossOk && bossCast.Count >= 100 && bossCast.All(c => !c.IsSecret && c.Implemented && c.Id != "ULD_216" && !rejected.Contains(c.Id)),
        bossCast.Count + " casts, " + bossCast.Count(c => c.IsSecret) + " Secrets");
    // A legacy run (both sides' generated Secrets off): the player's Box casts no Secret either.
    var legacyCast = new List<Card>();
    for (int seed = 1; seed <= 8; seed++)
    {
        var g = Ready(Plan(playerSecrets: false), seed);
        var box = ToHand(g, g.Player1, "ULD_216");
        int before = Casts(g);
        Play(g, box);
        legacyCast.AddRange(Started(g, before).Skip(1).Where(f => f.DefinitionKnown).Select(f => Cards.FromAssetId(f.DbfId)));
    }
    Check("B52 on a legacy run the player's Box casts no Secret", legacyCast.Count >= 60 && legacyCast.All(c => !c.IsSecret), legacyCast.Count + " casts");
});

// ============================================================ B53-B56 ULD_236 Tortollan Pilgrim
Guard("Pilgrim", () =>
{
    string[] spells = { "CS2_023", "CS2_027", "CS2_026", "CS2_025" }; // Arcane Intellect, Mirror Image, Frost Nova, Arcane Explosion
    var deck = Deck(("CS2_023", 2), ("CS2_027", 2), ("CS2_026", 2), ("CS2_025", 2), (Yeti, 22));
    var g = Ready(Plan(deck: deck));
    var p = g.Player1;
    // Every spell back into the deck, so the offers do not depend on the opening hand.
    foreach (var s in p.HandZone.Where(c => c is Spell).ToList()) { p.HandZone.Remove(s); p.DeckZone.Add(s); }
    var pilgrim = (Minion)ToHand(g, p, "ULD_236");
    int deckSpells = p.DeckZone.Count(c => c is Spell), deckCount = p.DeckZone.Count;
    bool played = pilgrim.Cost == 8 && Play(g, pilgrim) && pilgrim.AttackDamage == 5 && pilgrim.Health == 5;
    var offers = Offers(g, p);
    Check("B53 ULD_236 Tortollan Pilgrim (8 mana 5/5) opens a Discover of three different spells of its owner's deck", played && offers.Count == 3 && offers.Select(c => c.Id).Distinct().Count() == 3
        && offers.All(c => spells.Contains(c.Id)) && deckSpells == 8, string.Join(",", offers.Select(c => c.Id)));
    int index = Math.Max(0, offers.FindIndex(c => c.Id == "CS2_027"));
    string picked = offers[index].Id;
    int boardBefore = p.BoardZone.Count, handBefore = p.HandZone.Count;
    bool ok = Pick(g, p, p.Choice.Choices[index]) && p.Choice == null;
    bool effect = picked == "CS2_027" ? p.BoardZone.Count == boardBefore + 2 : picked == "CS2_023" ? p.HandZone.Count == handBefore + 2 : true;
    Check("B54 the pick is cast at once as a copy (" + picked + "): the deck keeps its own cards, and the copy does not go to the hand", ok && effect && p.DeckZone.Count(c => c is Spell) == deckSpells - (picked == "CS2_023" ? p.HandZone.Count(c => c is Spell) : 0)
        && p.DeckZone.Count == deckCount - (picked == "CS2_023" ? 2 : 0) && (picked == "CS2_023" || !p.HandZone.Any(c => c is Spell)), p.BoardZone.Count + " on board, deck " + p.DeckZone.Count + " of " + deckCount);
    var g2 = Ready(Plan(deck: Deck(("CS2_027", 2), (Yeti, 28))));
    foreach (var s in g2.Player1.HandZone.Where(c => c is Spell).ToList()) { g2.Player1.HandZone.Remove(s); g2.Player1.DeckZone.Add(s); }
    Play(g2, ToHand(g2, g2.Player1, "ULD_236"));
    Check("B55 a deck with one kind of spell offers that one card once", Offers(g2, g2.Player1).Select(c => c.Id).SequenceEqual(new[] { "CS2_027" }));
    var g3 = Ready(Plan());
    Check("B55 a deck with no spell opens no Discover", Play(g3, ToHand(g3, g3.Player1, "ULD_236")) && g3.Player1.Choice == null);
    // A targeted spell is cast at a random legal target; the boss's Pilgrim works on the boss's deck.
    var g4 = Ready(Plan(bossDeck: Deck(("CS2_029", 4), (Yeti, 26))), 5);
    ToBoss(g4);
    foreach (var s in g4.Player2.HandZone.Where(c => c is Spell && c.Card.Id != "GAME_005").ToList()) { g4.Player2.HandZone.Remove(s); g4.Player2.DeckZone.Add(s); }
    Board(g4, g4.Player1, Golem);
    int damage() => g4.Player1.Hero.Damage + g4.Player2.Hero.Damage + g4.Player1.BoardZone.Concat(g4.Player2.BoardZone).Sum(m => m.Damage);
    Play(g4, ToHand(g4, g4.Player2, "ULD_236"));
    bool offered = Offers(g4, g4.Player2).Select(c => c.Id).SequenceEqual(new[] { "CS2_029" });
    int before = damage();
    Pick(g4, g4.Player2, g4.Player2.Choice.Choices[0]);
    if (Environment.GetEnvironmentVariable("DEBUG_BOX") == "1") Console.WriteLine($"  pilgrim offered {offered} before {before} after {damage()} deck fireballs {g4.Player2.DeckZone.Count(c => c.Card.Id == "CS2_029")} offers {string.Join(",", Offers(g4, g4.Player2).Select(c => c.Id))} choice {g4.Player2.Choice != null}");
    Check("B56 a boss's Pilgrim offers its own deck's spell and casts the copy at a random target (Fireball: 6 damage somewhere)", offered && damage() == before + 6 && g4.Player2.DeckZone.Count(c => c.Card.Id == "CS2_029") == 4);
});

// ============================================================ B57-B60 ULD_239 Flame Ward
Guard("FlameWard", () =>
{
    var g = Ready(Plan());
    var p = g.Player1; var b = g.Player2;
    var ward = (Spell)ToHand(g, p, "ULD_239");
    bool played = ward.Cost == 3 && ward.Card.IsSecret && Play(g, ward) && p.SecretZone.Count == 1 && p.SecretZone[0] == ward;
    var mine = Board(g, p, Golem);
    ToBoss(g);
    var a1 = Board(g, b, Yeti); var a2 = Board(g, b, Yeti); var a3 = Board(g, b, Golem);
    Round(g);
    bool minionAttack = Attack(g, a1, mine) && p.SecretZone.Count == 1;
    Check("B57 ULD_239 Flame Ward (3) is a Secret; an enemy minion attacking a minion does not trigger it", played && minionAttack);
    int hero = p.Hero.Damage;
    bool heroAttack = Attack(g, a2, p.Hero);
    Check("B58 after an enemy minion attacks its owner's hero (the attack's damage is dealt first) all enemy minions take 3 damage and the Secret is revealed", heroAttack && p.Hero.Damage == hero + 4
        && p.SecretZone.Count == 0 && ward.Zone == p.GraveyardZone && a2.Damage == 3 && a3.Damage == 3 && a1.Zone != b.BoardZone && mine.Zone == p.BoardZone && mine.Damage == 4 && b.Hero.Damage == 0,
        "a2 " + a2.Damage + " a3 " + a3.Damage + " mine " + mine.Damage + " secrets " + p.SecretZone.Count);
    // Its owner's own turn: nothing triggers. The boss's hero attacking is no minion.
    var g2 = Ready(Plan(bossHero: "HERO_03", bossPower: "CS2_083b"));
    DungeonRunEncounters.PutSecret(g2.Player1, C("ULD_239"));
    var foe = Board(g2, g2.Player2, Yeti);
    ToBoss(g2);
    g2.Player2.Hero.AddWeapon((Weapon)Entity.FromCard(g2.Player2, C("CS2_106")));
    g2.DeathProcessingAndAuraUpdate();
    bool swung = g2.Process(HeroAttackTask.Any(g2.Player2, g2.Player1.Hero));
    Check("B59 an enemy hero's attack does not trigger it", swung && g2.Player1.SecretZone.Count == 1 && foe.Damage == 0);
    // The boss as its owner: the run player's minion attacking the boss's hero.
    var g3 = Ready(Plan(bossHero: "HERO_08", bossPower: "CS2_034"));
    var attacker = Board(g3, g3.Player1, Yeti); var other = Board(g3, g3.Player1, Golem);
    DungeonRunEncounters.PutSecret(g3.Player2, C("ULD_239"));
    Round(g3);
    Check("B60 a boss's Flame Ward answers the run player's minion attacking the boss's hero", Attack(g3, attacker, g3.Player2.Hero) && g3.Player2.SecretZone.Count == 0 && attacker.Damage == 3 && other.Damage == 3);
});

// ============================================================ B61 ULD_253 Tomb Warden
Guard("TombWarden", () =>
{
    var g = Ready(Plan("HERO_01", "CS2_102"));
    var p = g.Player1;
    var warden = (Minion)ToHand(g, p, "ULD_253");
    Check("B61 ULD_253 Tomb Warden (8 mana 3/6 Taunt Mech): Battlecry summons a copy of itself to its right", warden.Cost == 8 && Play(g, warden) && p.BoardZone.Count == 2 && p.BoardZone[0] == warden
        && p.BoardZone.All(m => m.Card.Id == "ULD_253" && m.AttackDamage == 3 && m.Health == 6 && m.HasTaunt) && warden.Card.IsRace(Race.MECHANICAL));
    while (p.BoardZone.Count < 6) Board(g, p, Yeti);
    Mana(p, 10);
    Check("B61 with one free slot the Warden lands and no copy fits", Play(g, ToHand(g, p, "ULD_253")) && p.BoardZone.Count == 7 && p.BoardZone.Count(m => m.Card.Id == "ULD_253") == 3);
});

// ============================================================ B62-B63 ULD_256 Into the Fray, ULD_258 Armagedillo
Guard("Fray", () =>
{
    var g = Ready(Plan("HERO_01", "CS2_102"));
    var p = g.Player1;
    Clear(p);
    var grizzly = (Minion)ToHand(g, p, Grizzly); var yeti = (Minion)ToHand(g, p, Yeti); var onBoard = Board(g, p, Grizzly);
    var fray = ToHand(g, p, "ULD_256");
    Check("B62 ULD_256 Into the Fray (1): the Taunt minions in its owner's hand get +2/+2; other cards and the board do not", fray.Cost == 1 && Play(g, fray) && grizzly.AttackDamage == 5 && grizzly.Health == 5
        && yeti.AttackDamage == 4 && yeti.Health == 5 && onBoard.AttackDamage == 3 && onBoard.Health == 3 && grizzly.AppliedEnchantments.Any(e => e.Card.Id == "ULD_256e"));
    Mana(p, 10);
    var dillo = (Minion)ToHand(g, p, "ULD_258");
    bool played = dillo.Cost == 6 && Play(g, dillo) && dillo.AttackDamage == 4 && dillo.Health == 7 && dillo.HasTaunt && dillo.Card.IsRace(Race.BEAST);
    bool notYet = grizzly.AttackDamage == 5;
    EndTurn(g);
    bool once = grizzly.AttackDamage == 7 && grizzly.Health == 7 && yeti.AttackDamage == 4;
    EndTurn(g);
    Check("B63 ULD_258 Armagedillo (6 mana 4/7 Taunt Beast): at the end of its owner's turn (only) the Taunt minions in its owner's hand get +2/+2", played && notYet && once && grizzly.AttackDamage == 7
        && grizzly.AppliedEnchantments.Any(e => e.Card.Id == "ULD_258e"), played + " " + notYet + " " + once + " " + grizzly.AttackDamage);
    Mana(p, 10);
    Check("B63 the buffed minion is played with its stats", Play(g, grizzly) && grizzly.Zone == p.BoardZone && grizzly.AttackDamage == 7 && grizzly.Health == 7);
});

// ============================================================ B64-B66 ULD_262 High Priest Amet
Guard("Amet", () =>
{
    var g = Ready(Plan("HERO_09", "CS1h_001"));
    var p = g.Player1; var b = g.Player2;
    var amet = (Minion)ToHand(g, p, "ULD_262");
    bool played = amet.Cost == 4 && Play(g, amet) && amet.AttackDamage == 2 && amet.Health == 7;
    var yeti = Board(g, p, Yeti); var foe = Board(g, b, Yeti);
    Check("B64 ULD_262 High Priest Amet (4 mana 2/7): a minion its owner summons has its Health set to Amet's; enemy minions and Amet do not", played && yeti.Health == 7 && yeti.AttackDamage == 4
        && foe.Health == 5 && amet.Health == 7 && yeti.AppliedEnchantments.Any(e => e.Card.Id == "ULD_262e"));
    amet.Damage = 4;
    Mana(p, 10);
    var grizzly = (Minion)ToHand(g, p, Grizzly);
    Play(g, grizzly);
    Check("B65 it is Amet's current Health (3 after damage), for a minion played from hand too; earlier minions keep theirs", grizzly.Health == 3 && grizzly.Damage == 0 && yeti.Health == 7);
    // A Reborn minion returns at 1 Health and is a summon like any other: Amet sets its Health too.
    var taker = Board(g, p, "ULD_205");
    int takerHealth = taker.Health;
    Kill(g, taker);
    var rebornTaker = p.BoardZone.FirstOrDefault(m => m.Card.Id == "ULD_205");
    Check("B65 a minion that returns by Reborn is summoned again, so its Health is set as well (3, not 1)", takerHealth == 3 && rebornTaker != null && rebornTaker != taker && rebornTaker.Health == 3 && rebornTaker.Damage == 0,
        rebornTaker?.Health.ToString() ?? "none");
    Kill(g, amet);
    var later = Board(g, p, Yeti);
    Check("B66 after Amet dies new minions keep their own Health and the set Health stays", later.Health == 5 && yeti.Health == 7 && grizzly.Health == 3);
});

// ============================================================ B67-B69 ULD_268 Psychopomp
Guard("Psychopomp", () =>
{
    var g = Ready(Plan("HERO_09", "CS1h_001"));
    var p = g.Player1; var b = g.Player2;
    var foe = Board(g, b, Golem);
    Kill(g, foe);
    var first = (Minion)ToHand(g, p, "ULD_268");
    Check("B67 ULD_268 Psychopomp (4 mana 3/1) summons nothing when no friendly minion has died (an enemy death does not count)", first.Cost == 4 && Play(g, first) && first.AttackDamage == 3 && first.Health == 1 && p.BoardZone.Count == 1);
    var yeti = Board(g, p, Yeti);
    Kill(g, yeti);
    Mana(p, 10);
    var second = (Minion)ToHand(g, p, "ULD_268");
    Play(g, second);
    var back = p.BoardZone.FirstOrDefault(m => m.Card.Id == Yeti);
    Check("B68 it summons a friendly minion that died this game anew (full Health), to its right, and gives it Reborn", back != null && back != yeti && back.Health == 5 && back.Damage == 0
        && Tag(back, GameTag.REBORN) == 1 && back.ZonePosition == second.ZonePosition + 1, string.Join(",", p.BoardZone.Select(m => m.Card.Id)));
    Kill(g, back);
    var reborn = p.BoardZone.FirstOrDefault(m => m.Card.Id == Yeti);
    Check("B68 the Reborn it gave works: the minion returns with 1 Health", reborn != null && reborn.Health == 1);
    var seen = new HashSet<string>();
    for (int seed = 1; seed <= 12; seed++)
    {
        var gs = Ready(Plan("HERO_09", "CS1h_001"), seed, history: false);
        foreach (string id in new[] { Yeti, Grizzly, Golem }) Kill(gs, Board(gs, gs.Player1, id));
        Play(gs, ToHand(gs, gs.Player1, "ULD_268"));
        foreach (var m in gs.Player1.BoardZone.Where(m => m.Card.Id != "ULD_268")) seen.Add(m.Card.Id);
    }
    Check("B69 the minion is random among the dead (all three occur over 12 seeds)", seen.Count == 3, string.Join(",", seen));
    var g3 = Ready(Plan("HERO_09", "CS1h_001"));
    var shrine = ShrineRules.Place(g3.Player1, "TRLA_104");
    Kill(g3, shrine);
    Play(g3, ToHand(g3, g3.Player1, "ULD_268"));
    Check("B69 a dead run board shrine is never resurrected", g3.Player1.BoardZone.Count(m => !ShrineRules.IsMarked(m)) == 1, string.Join(",", g3.Player1.BoardZone.Select(m => m.Card.Id)));
});

// ============================================================ B70-B71 ULD_270 Sandhoof Waterbearer
Guard("Waterbearer", () =>
{
    var g = Ready(Plan("HERO_09", "CS1h_001"));
    var p = g.Player1; var b = g.Player2;
    var bearer = (Minion)ToHand(g, p, "ULD_270");
    bool played = bearer.Cost == 5 && Play(g, bearer) && bearer.AttackDamage == 5 && bearer.Health == 5;
    b.Hero.Damage = 9;
    EndTurn(g);
    bool none = p.Hero.Damage == 0 && b.Hero.Damage == 9;
    EndTurn(g);
    p.Hero.Damage = 9;
    EndTurn(g);
    Check("B70 ULD_270 Sandhoof Waterbearer (5 mana 5/5): at the end of its owner's turn 5 Health is restored to a damaged friendly character; never an enemy", played && none && p.Hero.Damage == 4 && b.Hero.Damage == 9,
        played + " " + none + " " + p.Hero.Damage);
    var healed = new HashSet<string>();
    for (int seed = 1; seed <= 12; seed++)
    {
        var gs = Ready(Plan("HERO_09", "CS1h_001"), seed, history: false);
        Board(gs, gs.Player1, "ULD_270");
        var golem = Board(gs, gs.Player1, Golem);
        var full = Board(gs, gs.Player1, Yeti);
        golem.Damage = 6; gs.Player1.Hero.Damage = 6;
        EndTurn(gs);
        if (golem.Damage == 1 && gs.Player1.Hero.Damage == 6) healed.Add("minion");
        else if (golem.Damage == 6 && gs.Player1.Hero.Damage == 1) healed.Add("hero");
        else healed.Add("other");
    }
    Check("B71 one random damaged friendly character each time (hero or minion over 12 seeds, never both, never an undamaged one)", healed.SetEquals(new[] { "minion", "hero" }), string.Join(",", healed));
});

// ============================================================ B72-B76 admission, generation lists, shipped decks
Guard("Admission", () =>
{
    var notBoth = family.Where(id => !DungeonRunAdmission.Classify(C(id), id, AdmitScope.Player, null, catalogRun: true).Admitted
        || !DungeonRunAdmission.Classify(C(id), id, AdmitScope.Boss, null, catalogRun: true).Admitted
        || !DungeonRunAdmission.Classify(C(id), id, AdmitScope.Player).Admitted || !DungeonRunAdmission.Classify(C(id), id, AdmitScope.Boss).Admitted).ToList();
    Check("B72 every family card is admitted for the run player and for a boss, on legacy and catalog runs", notBoth.Count == 0, string.Join(",", notBoth));
    var unlisted = family.Where(id => DungeonRunAdmission.RuleFor(id) == null).ToList();
    Check("B73 every family card has an admission row (ULD_154, ULD_236 and ULD_239 keep their phase 2 rows)", unlisted.Count == 0
        && DungeonRunAdmission.RuleFor("ULD_154").Family == "secret-from-deck" && DungeonRunAdmission.RuleFor("ULD_236").Family == "from-own-deck" && DungeonRunAdmission.RuleFor("ULD_239").Family == "secret-card",
        string.Join(",", unlisted));
    Check("B73 the rows name their family: the pull from a hidden hand, the random casts, the Quest and its reward power", DungeonRunAdmission.RuleFor("ULD_212").Family == "hidden-reveal"
        && DungeonRunAdmission.RuleFor("ULD_216").Family == "random-cast" && DungeonRunAdmission.RuleFor("ULD_131").Family == "quest-hero-power" && DungeonRunAdmission.RuleFor("ULD_131p") == DungeonRunAdmission.RuleFor("ULD_131")
        && DungeonRunAdmission.RuleFor("ULD_209t") == DungeonRunAdmission.RuleFor("ULD_209"));
    var all = new HashSet<string>(DungeonRunAdmission.GeneratedRejects(true)); var boss = new HashSet<string>(DungeonRunAdmission.GeneratedRejectsBoss(true));
    var legacyAll = new HashSet<string>(DungeonRunAdmission.GeneratedRejects(false));
    Check("B74 generation: no family card is rejected for both sides; only the Secret (Flame Ward) is kept from the boss's own generation", !family.Any(all.Contains) && !family.Any(legacyAll.Contains)
        && family.Where(boss.Contains).SequenceEqual(new[] { "ULD_239" }), string.Join(",", family.Where(id => all.Contains(id) || boss.Contains(id))));
    Check("B75 Flame Ward is in the Mage Secret pool a boss with generated Secrets draws from (Whirt's Prediction), and is admitted for a boss", DungeonRunValidator.BossSecretPool(CardClass.MAGE).Any(c => c.Id == "ULD_239"));
    var errors = DungeonRunValidator.Validate(data, null);
    Check("B76 the shipped data validates with the family scripted", errors.Count == 0, string.Join(" | ", errors.Take(3)));
    var icarax = data.Boss("ulda_40_icarax");
    var decks = new[] { icarax.Deck }.Concat(icarax.Forms.Where(f => f.Deck != null).Select(f => f.Deck)).SelectMany(d => data.BossDecks[d]).ToList();
    Check("B76 Icarax's wiki decks now hold the family's cards the list names (Restless Mummy, Into the Fray, Armagedillo, Tomb Warden, Phalanx Commander, Khartut Defender)",
        new[] { "ULD_206", "ULD_256", "ULD_258", "ULD_253", "ULD_179", "ULD_208" }.All(decks.Contains), string.Join(",", new[] { "ULD_206", "ULD_256", "ULD_258", "ULD_253", "ULD_179", "ULD_208" }.Where(id => !decks.Contains(id))));
});

// ============================================================ B77-B78 the fixture rows of the bosspilot mode
Guard("Fixture", () =>
{
    string path = Path.Combine(fixtures, "encounters-f03.json");
    var fixed03 = Load(File.ReadAllText(path));
    var rows = fixed03.Encounters.Where(b => b.Id.StartsWith("fix03_")).ToList();
    var held = rows.SelectMany(b => fixed03.BossDecks[b.Deck]).Distinct().ToList();
    Check("B77 the fixture rows (curated boss decks, tiers 1 to 4) hold every family card", rows.Count >= 5 && family.All(held.Contains) && rows.All(b => fixed03.BossDecks[b.Deck].Count == 30),
        string.Join(",", family.Where(id => !held.Contains(id))));
    var errors = DungeonRunValidator.Validate(fixed03, null);
    Check("B78 the fixture data validates: every card is scripted, client-known and admitted for a boss", errors.Count == 0, string.Join(" | ", errors.Take(4)));
});

// ============================================================ B79 the fair AI pilots a boss that holds Ossirian Tear
// The enc03 bosspilot mode plays the Quest, but whether a boss completes it there depends on the game. Here the boss's
// Quest stands at 3 of 4 when its first turn ends, so the fair AI plays the rest of the game with the passive reward
// power and both effects of its Choose One cards (the Druid fixture deck: Hidden Oasis, Druid of the Claw, Nourish).
Guard("AiTear", () =>
{
    var fixed03 = Load(File.ReadAllText(Path.Combine(fixtures, "encounters-f03.json")));
    var row = fixed03.Boss("fix03_druid");
    var testBudget = new Budget(6000, 2000, 300, 6, 3, 100, 8, turnSearchSeconds: 6, turnWallSeconds: 8);
    int games = 0, fallbacks = 0, bothOases = 0, turnCaps = 0; bool tearAlways = true, neverOffered = true; var results = new List<string>();
    for (int seed = 31; seed <= 33; seed++)
    {
        var plan = Plan(bossHero: row.HeroCard, bossPower: row.Power, bossDeck: fixed03.BossDecks[row.Deck].ToArray());
        var pilot = DungeonRunBossPilot.Build(row, plan, fixed03);
        var npcProfile = pilot.Profile.WithBudget(testBudget);
        var golden = new GoldenPilot(npcProfile, pilot.Plan, null, pilot.SecretPrior);
        var g = Ready(plan, seed, history: false);
        var boss = g.Player2;
        ToBoss(g);
        var quest = (Spell)ToHand(g, boss, "ULD_131");
        Play(g, quest);
        quest.QuestProgress = 3;
        EndTurn(g);
        tearAlways &= boss.Hero.HeroPower.Card.Id == "ULD_131p" && boss.ChooseBoth;
        int decisionIndex = 0, actions = 0;
        void Act(SabberStoneCore.Tasks.PlayerTasks.PlayerTask task)
        {
            if (++actions > 1500) throw new Exception("stuck after 1500 actions");
            if (task is ChooseTask pick && g.Step != Step.BEGIN_MULLIGAN)
            {
                var decision = g.GetPendingDecisions().Single(d => d.ControllerId == task.Controller.Id);
                if (!g.TryAnswer(new SabberStoneCore.Model.Decisions.DecisionAnswer(decision.Id, decision.ControllerId, pick.Choices.ToArray()), out var why)) throw new Exception("engine rejected a choice (" + why + ")");
            }
            else if (!g.Process(task)) throw new Exception("engine rejected " + task);
        }
        while (g.State != State.COMPLETE && g.Turn <= 100)
        {
            if (g.CurrentPlayer == boss)
            {
                neverOffered &= !boss.Options().Any(o => o is HeroPowerTask);
                var (task, trace) = BotDriver.Decide(g, boss.PlayerId, pilot.Deck.Style,
                    st => golden.DecidePilot(st, new GoldenContext(g.Turn, decisionIndex, unchecked(997 * g.Turn + decisionIndex * 31), npcProfile)),
                    ref decisionIndex, golden.Options);
                if (trace?.Kind == "fallback") fallbacks++;
                bool oasis = task is PlayCardTask play && play.Source != null && play.Source.Card.Id == "ULD_135";
                int ancients = boss.BoardZone.Count(m => m.Card.Id == "ULD_135at"), free = boss.BoardZone.FreeSpace;
                Act(task);
                if (oasis && (free == 0 || boss.BoardZone.Count(m => m.Card.Id == "ULD_135at") == ancients + 1)) bothOases++;
            }
            else Act(LocalBot.Choose(g.Player1.Options()));
        }
        games++;
        if (g.State != State.COMPLETE) turnCaps++;
        tearAlways &= boss.Hero.HeroPower.Card.Id == "ULD_131p" && boss.ChooseBoth;
        results.Add(g.State != State.COMPLETE ? "turn-cap" : boss.PlayState == PlayState.WON ? "boss" : "player");
    }
    Check("B79 three games piloted by the fair AI with Ossirian Tear from the boss's first turn end: no fault, no fallback, the power is never offered, the aura holds to the end",
        games == 3 && fallbacks == 0 && tearAlways && neverOffered && turnCaps == 0 && SearchTelemetry.SwallowedLeafErrors == 0, string.Join(",", results) + "; Hidden Oasis played with both effects " + bothOases + " times");
});

Console.WriteLine($"SearchTelemetry swallowed leaf exceptions {SearchTelemetry.SwallowedLeafErrors}");
Console.WriteLine($"RESULT {passes} passed, {failures + SearchTelemetry.SwallowedLeafErrors} failed");
return failures + SearchTelemetry.SwallowedLeafErrors;
