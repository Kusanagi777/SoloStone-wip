// c-treasures-F1: phase 6 family F1 of the unified Dungeon Run, the original Dungeon Run (Kobolds and Catacombs) passive
// treasures LOOTA_800, 801, 802, 803, 804, 818, 824, 825, 828, 831, 832, 833, 845 and 846. For every id: the engine content
// (scripted, keyed and shelled by its own client id, name and text word for word from facts/violet-hold-2026-09.json), its
// aura shell, the run data row (act runs only; LOOTA_833 on catalog runs only; LOOTA_802 scoped to the powers it can upgrade;
// spare_crystal kept for legacy runs), the validator and admission checks, a scripted regression game through the run setup
// path (DungeonRun.NewRun -> BuildPlan -> DungeonRunSetup.Configure -> Game -> ApplyAfterStart) asserting the board, hand and
// hero outcome, and a full game against the real boss AI (DungeonRunBossPilot + GoldenPilot, the run's Secret prior) that must
// end without a fault. Headless: compiles C:\SoloStone\plugin\DungeonRun\*.cs and the NPC AI against p6f1/engine-out.
// Run it twice and compare the output (determinism).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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

string root = VerifyPaths.Data;
string R(string f) => File.ReadAllText(Path.Combine(root, f));
string runJson = R("dungeon-run/run-v1.json"), poolJson = R("dungeon-run/pool-v1.json"), npcJson = R("npc/decks-v1.json"),
    catalogJson = R("dungeon-run/catalog-v1.json"), scheduleJson = R("dungeon-run/schedule-v1.json"), tavernJson = R("dungeon-run/tavern-v1.json");
var data = DungeonRunData.Parse(runJson, poolJson, npcJson, catalogJson, scheduleJson, tavernJson);
var run = new DungeonRun(data);
var all = new DungeonRunProfile(); all.Unlocked.AddRange(data.Unlocks.Keys);
var facts = System.Text.Json.JsonDocument.Parse(File.ReadAllText(VerifyPaths.Repo("facts/violet-hold-2026-09.json")));
var factById = facts.RootElement.GetProperty("cards").EnumerateArray().ToDictionary(c => c.GetProperty("id").GetString(), c => c);
string FactText(string id) => factById[id].TryGetProperty("text", out var t) ? t.GetString() : null;
Card C(string id) => Cards.FromId(id);

string[] f1 = { "LOOTA_800", "LOOTA_801", "LOOTA_802", "LOOTA_803", "LOOTA_804", "LOOTA_818", "LOOTA_824", "LOOTA_825", "LOOTA_828",
    "LOOTA_831", "LOOTA_832", "LOOTA_833", "LOOTA_845", "LOOTA_846" };
var effectShell = new Dictionary<string, string>
{
    ["LOOTA_802"] = "LOOTA_802e", ["LOOTA_803"] = "LOOTA_803e", ["LOOTA_818"] = "LOOTA_818e", ["LOOTA_824"] = "LOOTA_824e", ["LOOTA_825"] = "LOOTA_825e",
    ["LOOTA_828"] = "LOOTA_828e2", ["LOOTA_831"] = "LOOTA_831e", ["LOOTA_832"] = "LOOTA_832e2", ["LOOTA_845"] = "TB_001", ["LOOTA_846"] = "TB_013",
};
// The 37.x client's ids for the engine's basic and upgraded powers (NativeMatch.TransportCardId).
var transport = new Dictionary<string, string>
{
    ["CS2_034"] = "HERO_08bp", ["CS2_102"] = "HERO_01bp", ["CS1h_001"] = "HERO_09bp", ["CS2_101"] = "HERO_04bp", ["CS2_017"] = "HERO_06bp",
    ["DS1h_292"] = "HERO_05bp", ["CS2_083b"] = "HERO_03bp", ["CS2_056"] = "HERO_07bp", ["CS2_049"] = "HERO_02bp",
    ["AT_132_WARRIOR"] = "HERO_01bp2", ["AT_132_SHAMAN"] = "HERO_02bp2", ["AT_132_ROGUE"] = "HERO_03bp2", ["AT_132_PALADIN"] = "HERO_04bp2",
    ["AT_132_HUNTER"] = "HERO_05bp2", ["AT_132_DRUID"] = "HERO_06bp2", ["AT_132_WARLOCK"] = "HERO_07bp2", ["AT_132_MAGE"] = "HERO_08bp2", ["AT_132_PRIEST"] = "HERO_09bp2",
};
string Sent(string id) => transport.TryGetValue(id, out var s) ? s : id;
// The pinned client's entity table (client-tables.json CARD), as the validator's client check reads it.
var clientIds = new HashSet<string>(System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "dungeon-run/reference/client-tables.json")))
    .RootElement.GetProperty("tables").GetProperty("CARD").EnumerateArray().Select(r => r.GetProperty("id").GetString()), StringComparer.Ordinal);
bool ClientKnows(string id) => clientIds.Contains(Sent(id));

// ---------------------------------------------------------------- T1 content: scripted, own shells, client text word for word
foreach (string id in f1)
{
    var content = DungeonRunContent.Get(id);
    var card = C(id);
    Check($"T1 {id}: a PassiveTreasure keyed and shelled by its own id, scripted, the client's SPELL",
        content.Kind == RunContentKind.PassiveTreasure && content.ShellId == id && card != null && card.Implemented && card.Type == CardType.SPELL
        && DungeonRunContent.ForShell(id) == content);
    Check($"T1 ids-text-match {id}: content name and text equal facts/violet-hold-2026-09.json",
        content.Name == factById[id].GetProperty("name").GetString() && content.Text == FactText(id), content.Name + " | " + content.Text);
    if (effectShell.TryGetValue(id, out string fx))
    {
        var effect = DungeonRunContent.Get(content.EffectKey);
        var fxCard = C(fx);
        Check($"T1 {id}: its aura is the Effect {content.EffectKey} on {fx}, a scripted client ENCHANTMENT (Power holds an aura or a trigger)",
            effect.Kind == RunContentKind.Effect && effect.ShellId == fx && fxCard != null && fxCard.Implemented && fxCard.Type == CardType.ENCHANTMENT
            && (fxCard.Power?.Aura != null || fxCard.Power?.Trigger != null) && ClientKnows(fx) && factById.ContainsKey(fx));
        if (fx.StartsWith("LOOTA_", StringComparison.Ordinal))
            Check($"T1 ids-text-match {fx}: the client's own player enchantment keeps its name and text",
                effect.Name == factById[fx].GetProperty("name").GetString() && effect.Text == FactText(fx), effect.Name + " | " + effect.Text);
        else
            Check($"T1 {fx}: a borrowed Tavern Brawl shell shows the treasure's own name and text", effect.Name == content.Name
                && effect.Text == FactText(id).Replace("<b>Passive</b>\n", "").Replace("trigger_twice", "trigger twice"));
    }
    else Check($"T1 {id}: a plan modifier or start effect, no aura", content.EffectKey == null
        && (content.DoublesHeroHealth || content.OpeningHandBonus > 0 || content.OnMatchStart != null));
}
Check("T1 the F1 effect shells are used once each (no other content shares one)",
    effectShell.Values.All(s => DungeonRunContent.All.Count(c => c.ShellId == s) == 1));
Check("T1 no F1 id is a PassivePower (PassiveHeroPowers unchanged)", !DungeonRunCardsGen.PassiveHeroPowers.Any(f1.Contains));
Check("T1 Justicar's Ring table: 11 basic powers, each upgraded form scripted, a hero power the client knows (transported id)",
    DungeonRunContent.HeroPowerUpgrades.Count == 11 && DungeonRunContent.HeroPowerUpgrades.All(p => C(p.Value) is Card u && u.Implemented
        && u.Type == CardType.HERO_POWER && ClientKnows(p.Value) && C(p.Key)?.Type == CardType.HERO_POWER),
    string.Join(",", DungeonRunContent.HeroPowerUpgrades.Where(p => !(C(p.Value)?.Implemented ?? false) || !ClientKnows(p.Value)).Select(p => p.Value)));

// ---------------------------------------------------------------- T2 data, validator, admission
{
    var errors = DungeonRunValidator.Validate(data, ClientKnows);
    Check("T2 shipped data validates with 0 errors (catalog, schedules, tavern; client check through the transported id)", errors.Count == 0, string.Join(" | ", errors.Take(5)));
    var legacyData = DungeonRunData.Parse(runJson, poolJson, npcJson);
    Check("T2 the data without the catalog validates with 0 errors (LOOTA_833 is admitted on catalog runs only, never offered there)",
        DungeonRunValidator.Validate(legacyData, null).Count == 0, string.Join(" | ", DungeonRunValidator.Validate(legacyData, null).Take(3)));
    foreach (string id in f1)
    {
        var t = data.FindTreasure(id);
        Check($"T2 {id}: a passive, offered, act-run treasure row", t != null && !t.Active && t.Offered && !t.Legacy && !t.Signature && t.Runs == DungeonRunTreasure.RunsAct);
    }
    var spare = data.FindTreasure("spare_crystal");
    Check("T2 spare_crystal stays for legacy runs (and saves) only: LOOTA_801 replaces it on act runs", spare != null && spare.Runs == DungeonRunTreasure.RunsLegacy && spare.Offered
        && DungeonRunContent.Get("spare_crystal") != null);
    Check("T2 LOOTA_802 is scoped to exactly the powers its table upgrades", data.FindTreasure("LOOTA_802").Scope.Powers.OrderBy(x => x, StringComparer.Ordinal)
        .SequenceEqual(DungeonRunContent.HeroPowerUpgrades.Keys.OrderBy(x => x, StringComparer.Ordinal)) && data.FindTreasure("LOOTA_802").Scope.Classes.Count == 0);
    var v833 = DungeonRunAdmission.Classify(null, "LOOTA_833", AdmitScope.Player);
    var c833 = DungeonRunAdmission.Classify(null, "LOOTA_833", AdmitScope.Player, null, catalogRun: true);
    Check("T2 admission: LOOTA_833 secret-generated, rejected on a legacy run, admitted for the run player (only) on a catalog run",
        v833.Family == "secret-generated" && !v833.Admitted && c833.Admitted
        && !DungeonRunAdmission.Classify(null, "LOOTA_833", AdmitScope.Boss, null, catalogRun: true).Admitted);
    Check("T2 admission: the other 13 ids are no admission subject (no Secret, deck or start-of-game phrase)",
        f1.Where(id => id != "LOOTA_833").All(id => !DungeonRunAdmission.Classify(C(id), id, AdmitScope.Any, null, catalogRun: true).Subject));
    // Negative: a scope power with no upgraded form, an unknown runs value.
    string bad802 = runJson.Replace("\"scope\": {\"powers\": [\"CS2_017\", \"DS1h_292\"", "\"scope\": {\"powers\": [\"druid_rootcall\", \"CS2_017\", \"DS1h_292\"");
    var e802 = DungeonRunValidator.Validate(DungeonRunData.Parse(bad802, poolJson, npcJson, catalogJson, scheduleJson, tavernJson), null);
    Check("T2 validator: a Justicar's Ring scope power without an upgraded form is an error", bad802 != runJson && e802.Any(e => e.Contains("LOOTA_802") && e.Contains("druid_rootcall") && e.Contains("no upgraded form")),
        string.Join(" | ", e802.Take(3)));
    string badRuns = runJson.Replace("{\"id\": \"LOOTA_804\", \"rarity\": \"common\", \"universal\": true, \"runs\": \"act\"}", "{\"id\": \"LOOTA_804\", \"rarity\": \"common\", \"universal\": true, \"runs\": \"sometimes\"}");
    var eRuns = DungeonRunValidator.Validate(DungeonRunData.Parse(badRuns, poolJson, npcJson, catalogJson, scheduleJson, tavernJson), null);
    Check("T2 validator: an unknown runs value is an error", badRuns != runJson && eRuns.Any(e => e.Contains("LOOTA_804") && e.Contains("runs sometimes")), string.Join(" | ", eRuns.Take(3)));
}

// ---------------------------------------------------------------- T3 offers: act runs only, scopes, catalog-only Tome
{
    var offered = new Dictionary<string, HashSet<string>>(); // treasure -> classes offered to
    var offeredPowers = new Dictionary<string, HashSet<string>>();
    foreach (var cls in data.Classes)
        for (int p = 0; p < cls.Powers.Count; p++)
            for (long seed = 1; seed <= 60; seed++)
            {
                var s = run.NewRun(all, cls.Id, cls.DefaultDeck, cls.Powers[p], seed, signatureId: run.SignatureOffer(all, run.Data.FindClass(cls.Id), seed).FirstOrDefault());
                if (!s.IsActRun || !s.IsCatalogRun) throw new Exception("not a catalog act run");
                if (!run.OpenTreasureDraft(s)) continue;
                foreach (string id in s.OpenDraft.Offered)
                {
                    if (!offered.TryGetValue(id, out var set)) offered[id] = set = new HashSet<string>();
                    set.Add(cls.Id);
                    if (!offeredPowers.TryGetValue(id, out var ps)) offeredPowers[id] = ps = new HashSet<string>();
                    ps.Add(cls.Powers[p]);
                }
            }
    foreach (string id in f1) Check($"T3 {id} is offered on catalog act runs", offered.ContainsKey(id), offered.ContainsKey(id) ? string.Join(",", offered[id].OrderBy(x => x)) : "never");
    Check("T3 spare_crystal is never offered on an act run", !offered.ContainsKey("spare_crystal"));
    Check("T3 LOOTA_802 is offered only to runs with a basic power", offeredPowers.TryGetValue("LOOTA_802", out var ring) && ring.All(DungeonRunContent.HeroPowerUpgrades.ContainsKey),
        ring == null ? "" : string.Join(",", ring));
    var tomeScope = data.FindTreasure("LOOTA_833").Scope.Classes;
    Check("T3 LOOTA_833 is offered only to its scope classes' rows", offered.TryGetValue("LOOTA_833", out var tome)
        && tome.All(c => data.FindClass(c).ClassSet.Any(tomeScope.Contains)), tome == null ? "" : string.Join(",", tome));

    var legacyOffered = new HashSet<string>();
    foreach (var cls in data.ClassRows)
        for (long seed = 1; seed <= 60; seed++)
        {
            var s = run.NewRun(all, cls.Id, cls.DefaultDeck, cls.DefaultPower, seed, schedule: DungeonRunSchedules.LegacyId, signatureId: run.SignatureOffer(all, run.Data.FindClass(cls.Id), seed).FirstOrDefault());
            if (s.IsActRun) throw new Exception("not a legacy run");
            if (run.OpenTreasureDraft(s)) legacyOffered.UnionWith(s.OpenDraft.Offered);
        }
    Check("T3 a legacy run is never offered an F1 treasure (its offers stay as released)", !legacyOffered.Any(f1.Contains), string.Join(",", legacyOffered.Where(f1.Contains)));
    Check("T3 a legacy run is still offered spare_crystal", legacyOffered.Contains("spare_crystal"));

    // Act runs on data without the shared catalog (CatalogVersion 0): legacy generated-Secret rules, so no Mysterious Tome.
    var plainData = DungeonRunData.Parse(runJson, poolJson, npcJson, null, scheduleJson, tavernJson);
    var plainRun = new DungeonRun(plainData);
    var plainOffered = new HashSet<string>();
    var plainAll = new DungeonRunProfile(); plainAll.Unlocked.AddRange(plainData.Unlocks.Keys);
    foreach (var cls in plainData.Classes.Where(c => tomeScope.Contains(c.Id)))
        for (long seed = 1; seed <= 120; seed++)
        {
            var s = plainRun.NewRun(plainAll, cls.Id, cls.DefaultDeck, cls.DefaultPower, seed, signatureId: plainRun.SignatureOffer(plainAll, plainRun.Data.FindClass(cls.Id), seed).FirstOrDefault());
            if (!s.IsActRun || s.IsCatalogRun) throw new Exception("not a non-catalog act run");
            if (plainRun.OpenTreasureDraft(s)) plainOffered.UnionWith(s.OpenDraft.Offered);
        }
    Check("T3 an act run without the catalog never offers LOOTA_833 (its Secrets need the catalog run's boss prior), but offers other F1 ids",
        !plainOffered.Contains("LOOTA_833") && f1.Count(plainOffered.Contains) >= 10, string.Join(",", f1.Where(plainOffered.Contains)));
}

// ---------------------------------------------------------------- the run setup path
DungeonRunState NewAct(string classId, long seed, string power = null, string deck = null)
{
    var cls = data.Class(classId);
    var s = run.NewRun(all, cls.Id, deck ?? cls.DefaultDeck, power ?? cls.DefaultPower, seed, signatureId: run.SignatureOffer(all, run.Data.FindClass(cls.Id), seed).FirstOrDefault());
    if (!s.IsActRun || s.Phase != DungeonRunPhase.PreMatch || s.Floor != 1) throw new Exception("act run not at floor 1 pre-match");
    return s;
}
// A passive pick as DungeonRun.PickTreasure records it (the draft's own choice is checked in T3).
void Hold(DungeonRunState s, params string[] ids) { foreach (string id in ids) s.Treasures.Add(id); }
Game StartFight(RunMatchPlan plan, int seed, bool mulligan = true)
{
    var cfg = new GameConfig { RandomSeed = seed, Shuffle = true, SkipMulligan = false, History = true, Logging = false, FormatType = FormatType.FT_WILD };
    DungeonRunSetup.Configure(cfg, plan);
    var g = new Game(cfg);
    g.StartGame();
    DungeonRunSetup.ApplyAfterStart(g, plan);
    if (!mulligan) return g;
    g.Process(ChooseTask.Mulligan(g.Player1, new List<int>()));
    g.Process(ChooseTask.Mulligan(g.Player2, new List<int>()));
    g.MainBegin();
    if (g.CurrentPlayer != g.Player1) throw new Exception("run player not first");
    return g;
}
(DungeonRunState s, RunMatchPlan plan, Game g) Fight(string classId, long seed, string[] treasures, string power = null, bool mulligan = true)
{
    var s = NewAct(classId, seed, power);
    Hold(s, treasures);
    var plan = run.BuildPlan(s);
    return (s, plan, StartFight(plan, run.MatchSeed(s), mulligan));
}
IPlayable ToHand(Game g, Controller c, string id)
{
    var e = Entity.FromCard(c, C(id));
    Generic.AddHandPhase.Invoke(c, e);
    g.DeathProcessingAndAuraUpdate();
    return e;
}
void EndTurn(Game g) => g.Process(EndTurnTask.Any(g.CurrentPlayer));

// ---------------------------------------------------------------- T4 LOOTA_800 Potion of Vitality
{
    var (s, plan, g) = Fight("WARRIOR", 11, new[] { "LOOTA_800" });
    var (_, _, g0) = Fight("WARRIOR", 11, new string[0]);
    Check("T4 LOOTA_800: the run hero starts at twice its 30 Health (60, max 60); without it 30", g.Player1.Hero.Health == 60 && g.Player1.Hero.BaseHealth == 60 && g0.Player1.Hero.Health == 30,
        g.Player1.Hero.Health + "/" + g.Player1.Hero.BaseHealth + " vs " + g0.Player1.Hero.Health);
    var (_, _, g2) = Fight("MAGE_ROGUE", 12, new[] { "LOOTA_800", "hearty_tonic" });
    Check("T4 LOOTA_800 on a named hero row (run Health 30) with Hearty Tonic: 30 x 2 + 10 = 70", g2.Player1.Hero.Health == 70, g2.Player1.Hero.Health.ToString());
    var s3 = NewAct("PRIEST", 13); Hold(s3, "LOOTA_800"); s3.HealthBonus = 4;
    var g3 = StartFight(run.BuildPlan(s3), run.MatchSeed(s3));
    Check("T4 LOOTA_800 doubles the base Health only: a tavern Good Food bonus (+4) is added after (64)", g3.Player1.Hero.Health == 64, g3.Player1.Hero.Health.ToString());
    Check("T4 LOOTA_800 leaves the boss's Health alone", g.Player2.Hero.Health == plan.BossHealth);
}

// ---------------------------------------------------------------- T5 LOOTA_801 Crystal Gem
{
    var (_, _, g) = Fight("DRUID", 21, new[] { "LOOTA_801" });
    var (_, _, g0) = Fight("DRUID", 21, new string[0]);
    Check("T5 LOOTA_801: turn 1 with 2 Mana Crystals (1 without)", g.Player1.BaseMana == 2 && g.Player1.RemainingMana == 2 && g0.Player1.BaseMana == 1,
        g.Player1.BaseMana + " vs " + g0.Player1.BaseMana);
    EndTurn(g); EndTurn(g);
    Check("T5 LOOTA_801: the extra crystal stays (turn 2: 3)", g.Player1.BaseMana == 3, g.Player1.BaseMana.ToString());
    Check("T5 LOOTA_801 leaves the boss's mana alone", g.Player2.BaseMana == 1, g.Player2.BaseMana.ToString());
}

// ---------------------------------------------------------------- T6 LOOTA_802 Justicar's Ring
foreach (var cls in data.Classes.Where(c => DungeonRunContent.HeroPowerUpgrades.ContainsKey(c.DefaultPower)))
{
    string basic = cls.DefaultPower, upgraded = DungeonRunContent.HeroPowerUpgrades[basic];
    var (s, plan, g) = Fight(cls.Id, 31, new[] { "LOOTA_802" });
    var power = g.Player1.Hero.HeroPower;
    Check($"T6 LOOTA_802 {cls.Id}: {basic} plays as {upgraded} and costs (1)", plan.PlayerHeroPowerId == upgraded && power.Card.Id == upgraded && power.Cost == 1,
        plan.PlayerHeroPowerId + " " + power.Card.Id + " cost " + power.Cost);
    var option = g.Player1.Options().OfType<HeroPowerTask>().FirstOrDefault(o => !o.HasTarget || o.Target == g.Player2.Hero)
        ?? g.Player1.Options().OfType<HeroPowerTask>().FirstOrDefault();
    int mana = g.Player1.RemainingMana;
    bool used = option != null && g.Process(option);
    while (used && g.Player1.Choice != null) g.Process(ChooseTask.Pick(g.Player1, g.Player1.Choice.Choices[0]));
    // Phase 7: Rikkar's Druid row starts with its deck's board shrine TRLA_112 Gonk's Armament, whose "gain Armor: refresh your
    // Mana Crystals" refunds the power's mana (Shapeshift gains Armor); the power still costs (1) and is used once.
    bool refunded = s.BoardShrine == "TRLA_112";
    Check($"T6 LOOTA_802 {cls.Id}: the upgraded power is usable on turn 1 for 1 mana", used && g.Player1.RemainingMana == (refunded ? mana : mana - 1) && power.IsExhausted,
        option == null ? "no option" : "mana " + mana + " -> " + g.Player1.RemainingMana);
}
{
    var (_, plan, g) = Fight("MAGE", 32, new[] { "LOOTA_802" });
    int bossHp = g.Player2.Hero.Health;
    g.Process(HeroPowerTask.Any(g.Player1, g.Player2.Hero));
    Check("T6 LOOTA_802 Mage: Fireblast Rank 2 deals 2 to the boss", g.Player2.Hero.Health == bossHp - 2, bossHp + " -> " + g.Player2.Hero.Health);
    var (_, plan0, g0) = Fight("MAGE", 32, new string[0]);
    Check("T6 without the ring the Mage plays Fireblast at (2)", plan0.PlayerHeroPowerId == "CS2_034" && g0.Player1.Hero.HeroPower.Cost == 2);
    var (_, planR, gR) = Fight("DRUID", 33, new[] { "LOOTA_802" }, power: "druid_rootcall");
    Check("T6 a power with no upgraded form is played unchanged (the draft never offers the ring to it, T3), at cost (1) if held",
        planR.PlayerHeroPowerId == "druid_rootcall" && gR.Player1.Hero.HeroPower.Card.Id == DungeonRunSetup.PowerCardId("druid_rootcall") && gR.Player1.Hero.HeroPower.Cost == 1);
    Check("T6 PlanPower: no ring, no change", DungeonRunSetup.PlanPower("CS2_034", new[] { "hearty_tonic" }) == "CS2_034"
        && DungeonRunSetup.PlanPower("CS2_034", new[] { "LOOTA_802" }) == "AT_132_MAGE");
}

// ---------------------------------------------------------------- T7 LOOTA_803 Scepter of Summoning
{
    var (_, _, g) = Fight("WARLOCK", 41, new[] { "LOOTA_803" });
    var p = g.Player1;
    var golem = ToHand(g, p, "CS2_186"); var yeti = ToHand(g, p, "CS2_182"); var five = ToHand(g, p, "CS2_150"); var mountain = ToHand(g, p, "CS2_232");
    var fireball = ToHand(g, p, "CS2_029");
    var bossGolem = ToHand(g, g.Player2, "CS2_186");
    Check("T7 LOOTA_803: minions that cost (5) or more cost (5): War Golem 7 -> 5, Ironbark Protector 8 -> 5, a 5-cost stays 5, Yeti 4 stays 4",
        golem.Cost == 5 && mountain.Cost == 5 && five.Cost == 5 && yeti.Cost == 4, $"{golem.Cost} {mountain.Cost} {five.Cost} {yeti.Cost}");
    Check("T7 LOOTA_803: spells and the boss's minions are unchanged", fireball.Cost == 4 && bossGolem.Cost == 7, fireball.Cost + " " + bossGolem.Cost);
    var (_, _, g0) = Fight("WARLOCK", 41, new string[0]);
    Check("T7 without it War Golem costs 7", ToHand(g0, g0.Player1, "CS2_186").Cost == 7);
}

// ---------------------------------------------------------------- T8 LOOTA_804 Small Backpacks
{
    var (_, _, g) = Fight("HUNTER", 51, new[] { "LOOTA_804" }, mulligan: false);
    var (_, _, g0) = Fight("HUNTER", 51, new string[0], mulligan: false);
    Check("T8 LOOTA_804: two more cards in the opening hand (3 + 2), drawn from the run deck", g.Player1.HandZone.Count == g0.Player1.HandZone.Count + 2
        && g.Player1.HandZone.Count == 5 && g.Player1.DeckZone.Count == g0.Player1.DeckZone.Count - 2, g.Player1.HandZone.Count + " vs " + g0.Player1.HandZone.Count);
    Check("T8 LOOTA_804 leaves the boss's hand alone", g.Player2.HandZone.Count == g0.Player2.HandZone.Count);
}

// ---------------------------------------------------------------- T9 LOOTA_818 Grommash's Armguards
{
    var (_, _, g) = Fight("WARRIOR", 61, new[] { "LOOTA_818" });
    var axe = ToHand(g, g.Player1, "CS2_106"); var gorehowl = ToHand(g, g.Player1, "EX1_411"); var yeti = ToHand(g, g.Player1, "CS2_182");
    var bossAxe = ToHand(g, g.Player2, "CS2_106");
    Check("T9 LOOTA_818: weapons cost (1): Fiery War Axe 3 -> 1, Gorehowl 7 -> 1; minions and the boss's weapons unchanged",
        axe.Cost == 1 && gorehowl.Cost == 1 && yeti.Cost == 4 && bossAxe.Cost == 3, $"{axe.Cost} {gorehowl.Cost} {yeti.Cost} {bossAxe.Cost}");
    bool equipped = g.Process(PlayCardTask.Any(g.Player1, axe));
    Check("T9 LOOTA_818: the 1-cost axe is played and equipped on turn 1", equipped && g.Player1.Hero.Weapon?.Card.Id == "CS2_106" && g.Player1.RemainingMana == 0);
}

// ---------------------------------------------------------------- T10 LOOTA_824 Khadgar's Scrying Orb
{
    var (_, _, g) = Fight("MAGE", 71, new[] { "LOOTA_824" });
    var fireball = ToHand(g, g.Player1, "CS2_029"); var explosion = ToHand(g, g.Player1, "CS2_025"); var moonfire = ToHand(g, g.Player1, "CS2_008");
    var yeti = ToHand(g, g.Player1, "CS2_182"); var bossFireball = ToHand(g, g.Player2, "CS2_029");
    Check("T10 LOOTA_824: spells cost (1) less (Fireball 4 -> 3, Arcane Explosion 2 -> 1, Moonfire stays 0); minions and the boss's spells unchanged",
        fireball.Cost == 3 && explosion.Cost == 1 && moonfire.Cost == 0 && yeti.Cost == 4 && bossFireball.Cost == 4,
        $"{fireball.Cost} {explosion.Cost} {moonfire.Cost} {yeti.Cost} {bossFireball.Cost}");
    Check("T10 LOOTA_824: Arcane Explosion is played for 1 on turn 1", g.Process(PlayCardTask.Any(g.Player1, explosion)) && g.Player1.RemainingMana == 0);
}

// ---------------------------------------------------------------- T11 LOOTA_825 Robe of the Magi
{
    var (_, _, g) = Fight("MAGE", 81, new[] { "LOOTA_825" });
    Check("T11 LOOTA_825: Spell Damage +3", g.Player1.CurrentSpellPower == 3 && g.Player2.CurrentSpellPower == 0, g.Player1.CurrentSpellPower.ToString());
    var moonfire = ToHand(g, g.Player1, "CS2_008");
    int hp = g.Player2.Hero.Health;
    g.Process(PlayCardTask.SpellTarget(g.Player1, moonfire, g.Player2.Hero));
    Check("T11 LOOTA_825: Moonfire deals 1 + 3 to the boss", g.Player2.Hero.Health == hp - 4, hp + " -> " + g.Player2.Hero.Health);
    var (_, _, g0) = Fight("MAGE", 81, new string[0]);
    var m0 = ToHand(g0, g0.Player1, "CS2_008"); int hp0 = g0.Player2.Hero.Health;
    g0.Process(PlayCardTask.SpellTarget(g0.Player1, m0, g0.Player2.Hero));
    Check("T11 without it Moonfire deals 1", g0.Player2.Hero.Health == hp0 - 1);
}

// ---------------------------------------------------------------- T12 LOOTA_828 Captured Flag
{
    var (_, _, g) = Fight("PALADIN", 91, new[] { "LOOTA_828" });
    var wisp = ToHand(g, g.Player1, "CS2_231");
    g.Process(PlayCardTask.Any(g.Player1, wisp));
    var m = (Minion)wisp;
    Check("T12 LOOTA_828: a played Wisp is 2/2", m.Zone == g.Player1.BoardZone && m.AttackDamage == 2 && m.Health == 2, m.AttackDamage + "/" + m.Health);
    EndTurn(g);
    var bossWisp = (Minion)ToHand(g, g.Player2, "CS2_231"); g.Process(PlayCardTask.Any(g.Player2, bossWisp));
    Check("T12 LOOTA_828: the boss's minions keep their stats (1/1)", bossWisp.Zone == g.Player2.BoardZone && bossWisp.AttackDamage == 1 && bossWisp.Health == 1);
    var (_, _, g0) = Fight("PALADIN", 91, new string[0]);
    var w0 = (Minion)ToHand(g0, g0.Player1, "CS2_231"); g0.Process(PlayCardTask.Any(g0.Player1, w0));
    Check("T12 without it a Wisp is 1/1", w0.AttackDamage == 1 && w0.Health == 1);
    // The Silver Hand Recruits a Paladin's power summons get the aura too.
    var (_, _, gp) = Fight("PALADIN", 92, new[] { "LOOTA_828" });
    gp.Process(HeroPowerTask.Any(gp.Player1)); // costs 2: turn 1 has 1 mana, so it is refused
    EndTurn(gp); EndTurn(gp);
    gp.Process(HeroPowerTask.Any(gp.Player1));
    var recruit = gp.Player1.BoardZone.FirstOrDefault();
    Check("T12 LOOTA_828: a summoned Silver Hand Recruit is 2/2", recruit != null && recruit.AttackDamage == 2 && recruit.Health == 2,
        recruit == null ? "none" : recruit.AttackDamage + "/" + recruit.Health);
}

// ---------------------------------------------------------------- T13 LOOTA_831 Glyph of Warding
{
    var (_, _, g) = Fight("SHAMAN", 101, new[] { "LOOTA_831" });
    var boss = g.Player2;
    var bossMinions = boss.HandZone.Where(e => e is Minion).ToList();
    var bossOther = boss.HandZone.Where(e => !(e is Minion)).ToList();
    var (_, _, g0) = Fight("SHAMAN", 101, new string[0]);
    var before = g0.Player2.HandZone.ToDictionary(e => e.Id, e => e.Cost);
    Check("T13 LOOTA_831: every minion in the boss's hand costs (1) more; its other cards do not",
        bossMinions.Count > 0 && bossMinions.All(e => before.TryGetValue(e.Id, out int c) && e.Cost == c + 1) && bossOther.All(e => before.TryGetValue(e.Id, out int c) && e.Cost == c),
        string.Join(",", boss.HandZone.Select(e => e.Card.Id + ":" + e.Cost + "/" + (before.TryGetValue(e.Id, out int c) ? c : -1))));
    var drawn = (Minion)ToHand(g, boss, "CS2_182"); var mine = ToHand(g, g.Player1, "CS2_182");
    Check("T13 LOOTA_831: a minion the boss draws later costs 5 (Yeti); the run player's Yeti stays 4", drawn.Cost == 5 && mine.Cost == 4);
    EndTurn(g);
    var legal = g.Player2.Options().OfType<PlayCardTask>().Where(o => o.Source == drawn).ToList();
    Check("T13 LOOTA_831: the boss's legal moves read the raised cost (Yeti not playable with 1 mana)", legal.Count == 0 && g.Player2.RemainingMana == 1);
}

// ---------------------------------------------------------------- T14 LOOTA_832 Cloak of Invisibility
{
    var (_, _, g) = Fight("ROGUE", 111, new[] { "LOOTA_832" });
    var wisp = (Minion)ToHand(g, g.Player1, "CS2_231");
    g.Process(PlayCardTask.Any(g.Player1, wisp));
    Check("T14 LOOTA_832: a played minion has Stealth", wisp.HasStealth && wisp[GameTag.STEALTH] == 1);
    EndTurn(g);
    var bossWisp = (Minion)ToHand(g, g.Player2, "CS2_231"); g.Process(PlayCardTask.Any(g.Player2, bossWisp));
    Check("T14 LOOTA_832: the boss's minion has no Stealth; the boss cannot attack the stealthed Wisp", !bossWisp.HasStealth
        && !g.Player2.Options().OfType<MinionAttackTask>().Any(o => o.Target == wisp));
    EndTurn(g);
    int hp = g.Player2.Hero.Health;
    bool attacked = g.Process(MinionAttackTask.Any(g.Player1, wisp, g.Player2.Hero));
    Check("T14 LOOTA_832: the Wisp attacks the boss and keeps its Stealth (permanent)", attacked && g.Player2.Hero.Health == hp - 1 && wisp.HasStealth);
    EndTurn(g);
    Check("T14 LOOTA_832: on the boss's turn the Wisp still cannot be attacked", !g.Player2.Options().OfType<MinionAttackTask>().Any(o => o.Target == wisp));
    var (_, _, g0) = Fight("ROGUE", 111, new string[0]);
    var w0 = (Minion)ToHand(g0, g0.Player1, "CS2_231"); g0.Process(PlayCardTask.Any(g0.Player1, w0));
    Check("T14 without it a Wisp has no Stealth", !w0.HasStealth);
    var (_, _, gs) = Fight("ROGUE", 112, new[] { "LOOTA_832" });
    Generic.SummonBlock(gs, (Minion)Entity.FromCard(gs.Player1, C("CS2_231")), -1, gs.Player1.Hero);
    gs.DeathProcessingAndAuraUpdate();
    Check("T14 LOOTA_832: a summoned (not played) minion gets Stealth too", gs.Player1.BoardZone.Count == 1 && gs.Player1.BoardZone[0].HasStealth);
}

// ---------------------------------------------------------------- T15 LOOTA_833 Mysterious Tome
{
    var tomeScope = data.FindTreasure("LOOTA_833").Scope.Classes;
    var withThree = new List<string>();
    foreach (var cls in data.ClassRows)
    {
        var (_, _, g) = Fight(cls.Id, 121, new string[0], mulligan: false);
        if (DungeonRunContent.RandomSecretCandidates(g.Player1).Count >= DungeonRunContent.MysteriousTomeSecrets) withThree.Add(cls.Id);
    }
    Check("T15 LOOTA_833 scope = the classes whose scripted Secret pool holds at least 3 (a catalog act run)", withThree.OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(tomeScope.OrderBy(x => x, StringComparer.Ordinal)),
        string.Join(",", withThree));
    foreach (string clsId in tomeScope)
    {
        var (s, plan, g) = Fight(clsId, 122, new[] { "LOOTA_833" }, mulligan: false);
        var secrets = g.Player1.SecretZone.Select(e => e.Card.Id).ToList();
        var prior = DungeonRunAdmission.SecretPrior(data, run.RunClasses(s), plan.PlayerGeneratedSecrets);
        var candidates = DungeonRunContent.RandomSecretCandidates(g.Player1).Select(c => c.Id).ToList();
        Check($"T15 LOOTA_833 {clsId}: 3 different scripted {clsId} Secrets in play before the mulligan, each in the boss's Secret prior",
            secrets.Count == 3 && secrets.Distinct().Count() == 3 && secrets.All(candidates.Contains) && secrets.All(id => C(id).Implemented && C(id).Class.ToString() == clsId)
            && prior != null && secrets.All(prior.ContainsKey) && g.Player1.SecretZone.All(e => e.Zone == g.Player1.SecretZone),
            string.Join(",", secrets) + " prior " + (prior?.Count ?? 0));
        Check($"T15 LOOTA_833 {clsId}: none is in the generation exclusions, and no 'cast' happened (spells played this game 0)",
            secrets.All(id => !(plan.ExcludedGenerated ?? new string[0]).Contains(id)) && g.Player1.NumSpellsPlayedThisGame == 0 && g.Player2.SecretZone.Count == 0);
        var (_, _, again) = Fight(clsId, 122, new[] { "LOOTA_833" }, mulligan: false);
        Check($"T15 LOOTA_833 {clsId}: the same seed puts the same Secrets in play (game RNG)", again.Player1.SecretZone.Select(e => e.Card.Id).SequenceEqual(secrets));
    }
    // A plan whose player may not generate Secrets (every legacy run): the Tome does nothing, as the engine refuses generation.
    var sl = NewAct("MAGE", 123); Hold(sl, "LOOTA_833");
    var pl = run.BuildPlan(sl); pl.PlayerGeneratedSecrets = false;
    var gl = StartFight(pl, 5, mulligan: false);
    Check("T15 LOOTA_833 with generated Secrets off for the player: no Secret is played", gl.Player1.SecretZone.Count == 0);
    // A Secret already in play is never doubled; a full zone stops.
    var (_, _, gf) = Fight("MAGE", 124, new[] { "LOOTA_833" }, mulligan: false);
    Check("T15 LOOTA_833: the Secret zone holds no duplicate", gf.Player1.SecretZone.Select(e => e.Card.AssetId).Distinct().Count() == gf.Player1.SecretZone.Count);
    // The Secrets trigger for real: an Explosive Trap / Counterspell style Secret reacts on the boss's turn.
    var (_, _, gt) = Fight("HUNTER", 125, new[] { "LOOTA_833" });
    int before = gt.Player1.SecretZone.Count;
    EndTurn(gt);
    var boar = (Minion)ToHand(gt, gt.Player2, "CS2_171"); gt.Process(PlayCardTask.Any(gt.Player2, boar));
    if (gt.Player2.BoardZone.Contains(boar)) gt.Process(MinionAttackTask.Any(gt.Player2, boar, gt.Player1.Hero));
    Check("T15 LOOTA_833 Hunter: the boss's Charge minion attacking the hero fires at least one Secret", gt.Player1.SecretZone.Count < before,
        before + " -> " + gt.Player1.SecretZone.Count);
}

// ---------------------------------------------------------------- T16 LOOTA_845 Totem of the Dead
{
    var (_, _, g) = Fight("DEATHKNIGHT", 131, new[] { "LOOTA_845" });
    var gnome = (Minion)ToHand(g, g.Player1, "EX1_029");
    g.Process(PlayCardTask.Any(g.Player1, gnome));
    int hp = g.Player2.Hero.Health;
    gnome.Destroy(); g.DeathProcessingAndAuraUpdate();
    Check("T16 LOOTA_845: Leper Gnome's Deathrattle deals 2 twice (4)", g.Player2.Hero.Health == hp - 4, hp + " -> " + g.Player2.Hero.Health);
    var (_, _, g0) = Fight("DEATHKNIGHT", 131, new string[0]);
    var gn0 = (Minion)ToHand(g0, g0.Player1, "EX1_029"); g0.Process(PlayCardTask.Any(g0.Player1, gn0));
    int hp0 = g0.Player2.Hero.Health; gn0.Destroy(); g0.DeathProcessingAndAuraUpdate();
    Check("T16 without it the Deathrattle deals 2 once", g0.Player2.Hero.Health == hp0 - 2);
    var (_, _, gb) = Fight("ROGUE", 133, new[] { "LOOTA_845" });
    EndTurn(gb);
    var bossGnome = (Minion)ToHand(gb, gb.Player2, "EX1_029"); gb.Process(PlayCardTask.Any(gb.Player2, bossGnome));
    int myHp = gb.Player1.Hero.Health; bossGnome.Destroy(); gb.DeathProcessingAndAuraUpdate();
    Check("T16 LOOTA_845: a boss Leper Gnome deals 2 once", gb.Player1.Hero.Health == myHp - 2, myHp + " -> " + gb.Player1.Hero.Health);
}

// ---------------------------------------------------------------- T17 LOOTA_846 Battle Totem
{
    var (_, _, g) = Fight("HUNTER", 141, new[] { "LOOTA_846" });
    var archer = ToHand(g, g.Player1, "CS2_189");
    int hp = g.Player2.Hero.Health;
    g.Process(PlayCardTask.Any(g.Player1, archer, g.Player2.Hero));
    Check("T17 LOOTA_846: Elven Archer's Battlecry deals 1 twice (2)", g.Player2.Hero.Health == hp - 2, hp + " -> " + g.Player2.Hero.Health);
    var (_, _, g0) = Fight("HUNTER", 141, new string[0]);
    var a0 = ToHand(g0, g0.Player1, "CS2_189"); int hp0 = g0.Player2.Hero.Health;
    g0.Process(PlayCardTask.Any(g0.Player1, a0, g0.Player2.Hero));
    Check("T17 without it the Battlecry deals 1", g0.Player2.Hero.Health == hp0 - 1);
    EndTurn(g);
    var bossArcher = ToHand(g, g.Player2, "CS2_189"); int myHp = g.Player1.Hero.Health;
    g.Process(PlayCardTask.Any(g.Player2, bossArcher, g.Player1.Hero));
    Check("T17 LOOTA_846: the boss's Battlecries trigger once", g.Player1.Hero.Health == myHp - 1, myHp + " -> " + g.Player1.Hero.Health);
}

// ---------------------------------------------------------------- T18 full games against the real boss AI
var testBudget = new Budget(6000, 2000, 300, 6, 3, 100, 8, turnSearchSeconds: 6, turnWallSeconds: 8);
var aiClass = new Dictionary<string, string>
{
    ["LOOTA_800"] = "WARRIOR", ["LOOTA_801"] = "DRUID", ["LOOTA_802"] = "MAGE", ["LOOTA_803"] = "WARLOCK", ["LOOTA_804"] = "PRIEST",
    ["LOOTA_818"] = "WARRIOR", ["LOOTA_824"] = "MAGE", ["LOOTA_825"] = "SHAMAN", ["LOOTA_828"] = "PALADIN", ["LOOTA_831"] = "DEMONHUNTER",
    ["LOOTA_832"] = "ROGUE", ["LOOTA_833"] = "HUNTER", ["LOOTA_845"] = "DEATHKNIGHT", ["LOOTA_846"] = "MAGE_ROGUE",
};
string AiGame(string treasure, string clsId, long seed)
{
    var s = NewAct(clsId, seed);
    Hold(s, treasure);
    var boss = run.CurrentBoss(s);
    var plan = run.BuildPlan(s);
    var pilot = DungeonRunBossPilot.Build(boss, plan, data, runClasses: run.RunClasses(s));
    var profile = pilot.Profile.WithBudget(testBudget);
    var golden = new GoldenPilot(profile, pilot.Plan, null, pilot.SecretPrior);
    var cfg = new GameConfig { RandomSeed = run.MatchSeed(s), Shuffle = true, SkipMulligan = false, History = false, Logging = false, FormatType = FormatType.FT_WILD };
    DungeonRunSetup.Configure(cfg, plan);
    var game = new Game(cfg);
    game.StartGame();
    DungeonRunSetup.ApplyAfterStart(game, plan);
    game.Process(ChooseTask.Mulligan(game.Player1, new List<int>()));
    var choice = game.Player2.Choice;
    var view = new MulliganView(choice.Choices.Select(id => new MulliganCard(id, game.IdEntityDic[id].Card.Id, game.IdEntityDic[id].Card.Cost)),
        false, pilot.Deck.CardIds, game.Player1.Hero.Card.Class);
    game.Process(ChooseTask.Mulligan(game.Player2, BotDriver.Mulligan(view, new DeckPlanMulligan(pilot.Plan)).ToList()));
    game.MainBegin();
    int decisionIndex = 0, actions = 0, decisions = 0, fallbacks = 0;
    void Act(PlayerTask task, string who)
    {
        if (++actions > 1500) throw new Exception("stuck after 1500 actions");
        if (task is ChooseTask pick && game.Step != Step.BEGIN_MULLIGAN)
        {
            var decision = game.GetPendingDecisions().Single(d => d.ControllerId == task.Controller.Id);
            if (!game.TryAnswer(new SabberStoneCore.Model.Decisions.DecisionAnswer(decision.Id, decision.ControllerId, pick.Choices.ToArray()), out var why))
                throw new Exception($"engine rejected {who} choice ({why})");
        }
        else if (!game.Process(task)) throw new Exception($"engine rejected {who} task {task}");
    }
    while (game.State != State.COMPLETE && game.Turn <= 60)
    {
        if (game.CurrentPlayer == game.Player2)
        {
            var (task, trace) = BotDriver.Decide(game, game.Player2.PlayerId, pilot.Deck.Style,
                st => golden.DecidePilot(st, new GoldenContext(game.Turn, decisionIndex, unchecked(997 * game.Turn + decisionIndex * 31), profile)),
                ref decisionIndex, golden.Options);
            decisions++;
            if (trace?.Kind == "fallback") fallbacks++;
            Act(task, "boss");
        }
        else Act(LocalBot.Choose(game.Player1.Options()), "player");
    }
    string result = game.State != State.COMPLETE ? "turn-cap" : game.Player2.PlayState == PlayState.WON ? "boss" : game.Player1.PlayState == PlayState.WON ? "player" : "tie";
    return $"{result} turns {game.Turn} decisions {decisions} fallbacks {fallbacks} boss {boss.Id} power {plan.PlayerHeroPowerId}";
}
foreach (string id in f1)
{
    string row;
    try { row = AiGame(id, aiClass[id], 151); }
    catch (Exception e) { row = "FAULT " + e.GetType().Name + ": " + e.Message.Split('\n')[0]; }
    Console.WriteLine($"T18 {id} {aiClass[id],-12} {row}");
    Check($"T18 {id}: a full game against the boss AI ends without a fault or an AI fallback", !row.StartsWith("FAULT") && row.Contains("fallbacks 0 "), row);
}

Console.WriteLine($"{passes} passed, {failures} failed");
return failures == 0 ? 0 : 1;
