// c-deckops: phase 5 (I2) of the unified Dungeon Run: per-card deck records in the engine (deck slots, opening flags, deck
// enchantments, boss start mana), the persistent deck changes (DungeonRunDeck) and Bob's taverns (DungeonRunTavernNode,
// tavern-v1.json), the tavern enchantment content and the regenerated restoration ledger. Headless: compiles
// C:\SoloStone\plugin\DungeonRun\*.cs and the boss pilot against audit-verify/engine-out.
// usage: AiHarness [d12 golden file]   (the golden is written by p5/d12golden from the PRE-CHANGE engine, p5/engine-before)
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
void Note(string text) => Console.WriteLine("NOTE " + text);
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

string goldenFile = args.Length > 0 ? args[0] : VerifyPaths.Fixture("p5/d12golden/d12-golden.txt");
string root = VerifyPaths.Data;
string Embedded(string name)
{
    using var stream = typeof(DungeonRun).Assembly.GetManifestResourceStream(name) ?? throw new InvalidDataException(name + " is not embedded");
    using var reader = new StreamReader(stream);
    return reader.ReadToEnd();
}
string runJson = File.ReadAllText(Path.Combine(root, "dungeon-run", "run-v1.json"));
string poolJson = File.ReadAllText(Path.Combine(root, "dungeon-run", "pool-v1.json"));
string npcJson = File.ReadAllText(Path.Combine(root, "npc", "decks-v1.json"));
string catalogJson = File.ReadAllText(Path.Combine(root, "dungeon-run", "catalog-v1.json"));
string scheduleJson = Embedded("SoloStone.DungeonRun.schedule-v1.json");
string tavernJson = Embedded("SoloStone.DungeonRun.tavern-v1.json");
Check("tavern-v1.json is embedded and equals the data file", tavernJson == File.ReadAllText(Path.Combine(root, "dungeon-run", "tavern-v1.json")));
var tavernBytes = File.ReadAllBytes(Path.Combine(root, "dungeon-run", "tavern-v1.json"));
Check("tavern-v1.json: UTF-8 without BOM, LF only", !(tavernBytes.Length > 2 && tavernBytes[0] == 0xEF) && !tavernBytes.Contains((byte)'\r'));

var data = DungeonRunData.Parse(runJson, poolJson, npcJson, catalogJson, scheduleJson, tavernJson);
var run = new DungeonRun(data);
var all = new DungeonRunProfile(); all.Unlocked.AddRange(data.Unlocks.Keys);
var table = data.Tavern;
var ser = new System.Xml.Serialization.XmlSerializer(typeof(DungeonRunSave));
string Xml(DungeonRunState s) { using var w = new StringWriter(); ser.Serialize(w, new DungeonRunSave { Active = s }); return w.ToString(); }
DungeonRunState Clone(DungeonRunState s) { using var r = new StringReader(Xml(s)); return ((DungeonRunSave)ser.Deserialize(r)).Active; }
// The recorded L1 digest was taken over Windows (CRLF) XML: hash those bytes on every platform.
string Hash(string text) { using var sha = System.Security.Cryptography.SHA256.Create(); return BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(VerifyPaths.WindowsText(text)))).Replace("-", "").Substring(0, 16); }
Card C(string id) => Cards.FromId(id);
string Ids(DungeonRunState s) => string.Join(",", s.Cards.Select(c => c.Uid + ":" + c.Id + ":" + c.Origin + ":" + c.CopyOf + ":" + (c.Opening ? "o" : "") + ":" + string.Join("+", c.Mods)));

// An act run of class cls (default deck and basic power).
DungeonRunState NewAct(string cls = "MAGE", long seed = 4242)
{
    var c = data.FindClass(cls);
    return run.NewRun(all, c.Id, c.Decks[0], c.Powers[0], seed, signatureId: run.SignatureOffer(all, run.Data.FindClass(c.Id), seed).FirstOrDefault());
}
// The scripted walk (first offers, first cards) until stop(s) holds; taverns are left unless shop is set.
void Walk(DungeonRunState s, Func<DungeonRunState, bool> stop, Action<DungeonRunState> shop = null, Action<DungeonRunState> atFight = null)
{
    var scratch = new DungeonRunProfile();
    for (int guard = 0; guard < 400 && !s.Finished && !stop(s); guard++)
    {
        switch (s.Phase)
        {
            case DungeonRunPhase.PreMatch: atFight?.Invoke(s); run.BeginMatch(s); run.RecordOutcome(s, scratch, DungeonRunOutcome.Win); break;
            case DungeonRunPhase.TreasureDraft: run.PickTreasure(s, s.OpenDraft.Offered[0]); break;
            case DungeonRunPhase.Modifier: run.PickModifier(s, s.ModifierOffer.Offered[0]); break;
            case DungeonRunPhase.Tavern: shop?.Invoke(s); run.LeaveTavern(s); break;
            case DungeonRunPhase.BundleDraft:
                if (s.OpenDraft.Takes == 0) { run.PickBundle(s, s.OpenDraft.Offered[0]); break; }
                string bundle = s.OpenDraft.Choosing ?? s.OpenDraft.Offered[0];
                var left = run.OfferCards(s.OpenDraft, bundle);
                foreach (string kept in s.OpenDraft.Chosen) left.Remove(kept);
                run.PickDraftCard(s, bundle, left[0]);
                break;
            default: throw new Exception("walk stopped at " + s.Phase);
        }
    }
}
// Grows the deck with admitted catalog minions (cost at least minCost) until it holds n cards.
void Grow(DungeonRunState s, int n, int minCost = 0)
{
    var pool = DungeonRunTavernNode.DiscoverPool(run, s, "minion").Where(id => C(id).Cost >= minCost && C(id).Rarity != Rarity.LEGENDARY).ToList();
    for (int i = 0; s.Cards.Count < n; i++)
    {
        var r = DungeonRunDeck.TryAdd(run, s, pool[i % pool.Count], DungeonRunDeck.OriginTavern);
        if (!r.Ok) throw new Exception("grow failed: " + r);
    }
}
int FirstMinion(DungeonRunState s, int skip = 0) => s.Cards.Where(c => c.Origin != DungeonRunDeck.OriginTreasure && C(c.Id).Type == CardType.MINION).Skip(skip).First().Uid;
int FirstSpell(DungeonRunState s) => s.Cards.First(c => c.Origin != DungeonRunDeck.OriginTreasure && C(c.Id).Type == CardType.SPELL).Uid;

Game StartPlan(RunMatchPlan plan, int seed, bool history = true, bool shuffle = true)
{
    var cfg = new GameConfig { RandomSeed = seed, Shuffle = shuffle, SkipMulligan = false, History = history, Logging = false, FormatType = FormatType.FT_WILD };
    DungeonRunSetup.Configure(cfg, plan);
    var g = new Game(cfg); g.StartGame(); DungeonRunSetup.ApplyAfterStart(g, plan);
    return g;
}
List<string> EnchIds(IPlayable e) => e.AppliedEnchantments?.Select(x => x.Card.Id).ToList() ?? new List<string>();
string[] tavernEnchants = table.Options.Where(o => o.Ench != null).Select(o => o.Ench).Distinct().OrderBy(x => x, StringComparer.Ordinal).ToArray();

// ================================================================ T6 content: the nine tavern enchantments
{
    var expected = new Dictionary<string, (int atk, int hp, int dur, int cost, string op)>
    {
        ["DALA_903e"] = (0, 0, 0, 0, "SET"), ["DALA_905e"] = (0, 0, 0, 0, ""), ["DALA_906e"] = (1, 1, 0, 0, ""), ["DALA_908e"] = (2, 2, 0, 0, ""),
        ["DALA_913e"] = (4, 4, 0, 2, "ADD"), ["DALA_914e"] = (0, 0, 0, 3, "SUB"), ["ULDA_603e"] = (2, 0, 2, 0, ""), ["ULDA_605e"] = (0, 0, 0, 1, "SUB"),
        ["ULDA_606e"] = (2, 2, 0, 0, ""),
    };
    Check("T6 tavern-v1.json names exactly the nine known enchantments", tavernEnchants.SequenceEqual(expected.Keys.OrderBy(x => x, StringComparer.Ordinal))
        && DungeonRunTavernTable.KnownEnchantments.Keys.OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(expected.Keys.OrderBy(x => x, StringComparer.Ordinal)), string.Join(",", tavernEnchants));
    foreach (var pair in expected)
    {
        var card = C(pair.Key);
        bool content = DungeonRunContent.TavernEnchantmentKeys.TryGetValue(pair.Key, out string key) && DungeonRunContent.TryGet(key, out var rc)
            && rc.ShellId == pair.Key && rc.Kind == RunContentKind.Effect;
        Check($"T6 {pair.Key}: content entry, Power set, implemented", content && card.Power != null && card.Implemented && card.Type == CardType.ENCHANTMENT);
        var effects = card.Power?.Enchant?.Effects ?? new SabberStoneCore.Enchants.IEffect[0];
        int Sum(GameTag tag) => effects.Where(x => x.Tag == tag && x.Operator == SabberStoneCore.Enchants.EffectOperator.ADD).Sum(x => x.Value);
        var costEffect = effects.FirstOrDefault(x => x.Tag == GameTag.COST);
        bool ok = Sum(GameTag.ATK) == pair.Value.atk && Sum(GameTag.HEALTH) == pair.Value.hp && Sum(GameTag.DURABILITY) == pair.Value.dur
            && (pair.Value.op == "" ? costEffect == null : costEffect != null && costEffect.Operator.ToString() == pair.Value.op && costEffect.Value == pair.Value.cost);
        Check($"T6 {pair.Key}: effect tags match the client text", ok, string.Join(" ", effects.Select(x => x.Tag + " " + x.Operator + " " + x.Value)));
        Check($"T6 {pair.Key}: not one-turn", card[GameTag.TAG_ONE_TURN_EFFECT] == 0);
    }
    var errs = DungeonRunValidator.Validate(data, null);
    Check("T6 the whole data (with tavern-v1.json and the new content) validates", errs.Count == 0, string.Join(" | ", errs.Take(5)));
}

// ================================================================ D14 boss start mana
{
    var plan = new RunMatchPlan { PlayerHero = C("HERO_08"), PlayerDeck = D12.PlayerDeck.Select(C).ToList(), BossHero = C("HERO_01"),
        BossDeck = Enumerable.Repeat(C("CS2_182"), 30).ToList(), BossStartManaBonus = 2 };
    var g = StartPlan(plan, 7);
    Check("D14 bonus 2: the boss's BaseMana is 2 before its first turn", g.Player2.BaseMana == 2, g.Player2.BaseMana.ToString());
    g.Process(ChooseTask.Mulligan(g.Player1, new List<int>())); g.Process(ChooseTask.Mulligan(g.Player2, new List<int>())); g.MainBegin();
    g.Process(EndTurnTask.Any(g.Player1));
    Check("D14 bonus 2: the boss has 3 mana on its first turn", g.Player2.BaseMana == 3, g.Player2.BaseMana.ToString());
    plan.BossStartManaBonus = 12;
    var g2 = StartPlan(plan, 7);
    Check("D14 bonus 12 clamps to MaxResources", g2.Player2.BaseMana == Controller.MaxResources, g2.Player2.BaseMana.ToString());
    plan.BossStartManaBonus = 0;
    Check("D14 bonus 0: unchanged", StartPlan(plan, 7).Player2.BaseMana == 0);
    // The run layer's plan carries the act curve's bonus; a legacy plan carries none.
    var legacy = run.NewRun(all, "MAGE", data.FindClass("MAGE").Decks[0], data.FindClass("MAGE").Powers[0], 1, schedule: DungeonRunSchedules.LegacyId, signatureId: run.SignatureOffer(all, run.Data.FindClass("MAGE"), 1).FirstOrDefault());
    var lp = run.BuildPlan(legacy);
    Check("D14 a legacy plan has no slots, no mana bonus, no extra health or opening cards",
        lp.PlayerDeckSlots == null && lp.BossStartManaBonus == 0 && lp.PlayerHealthBonus == 0 && lp.PlayerOpeningHandBonus == 0);
    var s = NewAct("MAGE", 77);
    Walk(s, x => x.Floor == 5 && x.Phase == DungeonRunPhase.PreMatch);
    var p5 = run.BuildPlan(s);
    Check("D14 act I major (floor 5): the plan's mana bonus follows the curve (1) and the slots align with the deck",
        p5.BossStartManaBonus == run.BossStartManaBonus(s) && p5.BossStartManaBonus == 1 && p5.PlayerDeckSlots.Count == p5.PlayerDeck.Count, p5.BossStartManaBonus.ToString());
    var g5 = StartPlan(p5, run.MatchSeed(s));
    Check("D14 act I major: the boss starts with 1 mana", g5.Player2.BaseMana == 1);
}

// ================================================================ D12 the no-op engine path
{
    var golden = File.Exists(goldenFile) ? File.ReadAllLines(goldenFile).Where(l => l.Length > 0).ToList() : new List<string>();
    Check("D12 golden digests exist (p5/d12golden, pre-change engine)", golden.Count == 20, goldenFile);
    int same = 0;
    foreach (string line in golden)
    {
        var parts = line.Split(' ');
        if (D12.Digest(int.Parse(parts[0])) == parts[1]) same++;
    }
    Check("D12 no slots, flags or bonuses: PowerHistory identical to the pre-change engine for 20 seeds", same == 20 && golden.Count == 20, same + " of " + golden.Count);
    // A plan with empty slot records (no mods, no flags) also plays identically.
    var plan = new RunMatchPlan { PlayerHero = C("HERO_08"), PlayerDeck = D12.PlayerDeck.Select(C).ToList(), BossHero = C("HERO_01"),
        BossDeck = Enumerable.Repeat(C("CS2_182"), 30).ToList() };
    var cfgA = new GameConfig { RandomSeed = 3, Shuffle = true, History = true, FormatType = FormatType.FT_WILD };
    DungeonRunSetup.Configure(cfgA, plan);
    var gA = new Game(cfgA); gA.StartGame(); DungeonRunSetup.ApplyAfterStart(gA, plan);
    plan.PlayerDeck = D12.PlayerDeck.Select(C).ToList();
    plan.PlayerDeckSlots = D12.PlayerDeck.Select(_ => new RunDeckSlot()).ToList();
    var cfgB = new GameConfig { RandomSeed = 3, Shuffle = true, History = true, FormatType = FormatType.FT_WILD };
    DungeonRunSetup.Configure(cfgB, plan);
    var gB = new Game(cfgB); gB.StartGame(); DungeonRunSetup.ApplyAfterStart(gB, plan);
    Check("D12 empty slot records (no mods, no flags): identical history to no records", gA.PowerHistory.Print(true) == gB.PowerHistory.Print(true) && cfgB.Player1OpeningSlots == null);
}

// ================================================================ D11 opening flags
{
    var deck = Enumerable.Repeat("CS2_231", 15).ToList();
    deck[5] = "CS2_182"; deck[9] = "CS2_120";
    RunMatchPlan Plan(List<string> ids, params int[] flagged) => new RunMatchPlan
    {
        PlayerHero = C("HERO_08"), PlayerDeck = ids.Select(C).ToList(), BossHero = C("HERO_01"), BossDeck = Enumerable.Repeat(C("CS2_182"), 30).ToList(),
        PlayerDeckSlots = ids.Select((_, i) => new RunDeckSlot { Opening = flagged.Contains(i) }).ToList(),
    };
    int both = 0;
    for (int seed = 1; seed <= 100; seed++)
    {
        var g = StartPlan(Plan(deck, 5, 9), seed, history: false);
        var a = g.IdEntityDic[g.Player1DeckSlotIds[5]]; var b = g.IdEntityDic[g.Player1DeckSlotIds[9]];
        if (a.Zone == g.Player1.HandZone && b.Zone == g.Player1.HandZone && g.Player1.HandZone.Count == 3 && g.Step == Step.BEGIN_MULLIGAN) both++;
    }
    Check("D11 two flagged slots, shuffle on: both in the opening hand before the mulligan, 100 seeds of 100", both == 100, both.ToString());
    // The slot map follows the list as given even when the engine reverses an unshuffled deck.
    var unshuffled = StartPlan(Plan(deck, 5, 9), 1, history: false, shuffle: false);
    Check("D11 unshuffled deck: slot ids still name the given cards",
        unshuffled.IdEntityDic[unshuffled.Player1DeckSlotIds[5]].Card.Id == "CS2_182" && unshuffled.IdEntityDic[unshuffled.Player1DeckSlotIds[9]].Card.Id == "CS2_120"
        && unshuffled.IdEntityDic[unshuffled.Player1DeckSlotIds[5]].Zone == unshuffled.Player1.HandZone);
    var quest = Cards.All.FirstOrDefault(c => c.Type == CardType.SPELL && c[GameTag.QUEST] == 1 && c.Implemented && c[(GameTag)1192] == 0 && c.Collectible);
    if (quest == null) Check("D11 a quest card exists", false);
    else
    {
        var qdeck = new List<string>(deck); qdeck[12] = quest.Id;
        int ok = 0;
        for (int seed = 1; seed <= 20; seed++)
        {
            var g = StartPlan(Plan(qdeck, 5, 9), seed, history: false);
            if (g.Player1.HandZone.Count == 3 && g.Player1.HandZone[0].Card.Id == quest.Id
                && g.IdEntityDic[g.Player1DeckSlotIds[5]].Zone == g.Player1.HandZone && g.IdEntityDic[g.Player1DeckSlotIds[9]].Zone == g.Player1.HandZone) ok++;
        }
        Check("D11 a quest in the deck comes first, then both flags (20 seeds)", ok == 20, quest.Id + " " + ok);
        var g3 = StartPlan(Plan(qdeck, 5, 9, 0), 1, history: false);
        Check("D11 quest + three flags: the flags fill the two slots left in slot order (0, 5); slot 9 stays in the deck", g3.Player1.HandZone.Count == 3
            && g3.IdEntityDic[g3.Player1DeckSlotIds[0]].Zone == g3.Player1.HandZone && g3.IdEntityDic[g3.Player1DeckSlotIds[5]].Zone == g3.Player1.HandZone
            && g3.IdEntityDic[g3.Player1DeckSlotIds[9]].Zone == g3.Player1.DeckZone);
    }
    bool refused = false;
    try { StartPlan(Plan(deck, 0, 1, 2, 3), 1, history: false); } catch (ArgumentOutOfRangeException) { refused = true; }
    Check("D11 four flags are refused by ValidateRunSetup", refused);
    // A flagged self-removing Start of Game source is skipped.
    var never = (HashSet<int>)typeof(StartOfGameRules).GetField("NeverOpening", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
    string tested = null; bool skipped = false;
    foreach (int asset in never.OrderBy(x => x))
    {
        var card = Cards.FromAssetId(asset);
        if (card == null || !card.Collectible) continue;
        var sdeck = new List<string>(deck); sdeck[3] = card.Id;
        Game g;
        try { g = StartPlan(Plan(sdeck, 3, 9), 1, history: false); } catch { continue; }
        var src = g.IdEntityDic[g.Player1DeckSlotIds[3]];
        if (src[StartOfGameRules.Pending] != 1 && src.Zone == g.Player1.DeckZone) continue; // not eligible in this deck: not a self-remover here
        tested = card.Id;
        skipped = src.Zone != g.Player1.HandZone && g.IdEntityDic[g.Player1DeckSlotIds[9]].Zone == g.Player1.HandZone;
        break;
    }
    Check("D11 a flagged self-removing Start of Game source is skipped (the other flag still dealt)", tested != null && skipped, tested ?? "no eligible source found");
}

// ================================================================ D1-D8 one per op, D9 refusals
{
    var s = NewAct("MAGE", 101);
    Walk(s, x => x.Phase == DungeonRunPhase.Tavern);
    run.LeaveTavern(s);
    Check("D0 fixture: an act run at pre-match after its first tavern", s.IsActRun && s.Phase == DungeonRunPhase.PreMatch && s.Cards.Count >= 15, s.Cards.Count + " cards");
    var minions = DungeonRunTavernNode.DiscoverPool(run, s, "minion");
    var spells = DungeonRunTavernNode.DiscoverPool(run, s, "spell");
    Check("D0 the run's minion and spell pools hold cards", minions.Count >= 3 && spells.Count >= 3, minions.Count + "/" + spells.Count);

    // D1 Add
    var before = s.Cards.Select(c => c.Uid).ToList(); int uid0 = s.NextUid;
    var r = DungeonRunDeck.TryAdd(run, s, minions[0], DungeonRunDeck.OriginTavern);
    var added = s.Cards.Last();
    Check("D1 Add: a new uid appended, origin tavern, floor stamped, no mods; the mirror follows", r.Ok && added.Uid == uid0 + 1 && s.NextUid == uid0 + 1
        && added.Id == minions[0] && added.Origin == "tavern" && added.AddedFloor == s.Floor && added.Mods.Count == 0 && added.CopyOf == 0
        && s.Cards.Take(before.Count).Select(c => c.Uid).SequenceEqual(before) && s.Deck.SequenceEqual(s.Cards.Select(c => c.Id)) && DungeonRunDeck.MirrorError(s) == null, r.ToString());
    // D2 Remove
    int victim = FirstMinion(s);
    string beforeIds = Ids(s);
    r = DungeonRunDeck.Remove(run, s, victim);
    Check("D2 Remove: exactly that card leaves, the rest keep their order", r.Ok && DungeonRunDeck.Find(s, victim) == null
        && Ids(s) == string.Join(",", beforeIds.Split(',').Where(x => !x.StartsWith(victim + ":"))) && DungeonRunDeck.MirrorError(s) == null, r.ToString());
    // D6 Buff (and the derived view)
    int bufUid = FirstMinion(s);
    var bufCard = DungeonRunDeck.Find(s, bufUid);
    int atk0 = C(bufCard.Id).ATK, hp0 = C(bufCard.Id).Health, cost0 = C(bufCard.Id).Cost;
    r = DungeonRunDeck.Buff(run, s, bufUid, "DALA_908e");
    var bufAfter = DungeonRunDeck.Find(s, bufUid);
    Check("D6 Buff: DALA_908e appended; derived stats +2/+2", r.Ok && bufAfter.Mods.SequenceEqual(new[] { "DALA_908e" })
        && DungeonRunDeck.EffectiveAttack(bufAfter) == atk0 + 2 && DungeonRunDeck.EffectiveHealth(bufAfter) == hp0 + 2 && DungeonRunDeck.EffectiveCost(bufAfter) == cost0);
    r = DungeonRunDeck.Buff(run, s, bufUid, "DALA_913e");
    bufAfter = DungeonRunDeck.Find(s, bufUid);
    Check("D6 Buff stacks in order: 908e then 913e = +6/+6, cost +2", r.Ok && bufAfter.Mods.SequenceEqual(new[] { "DALA_908e", "DALA_913e" })
        && DungeonRunDeck.EffectiveAttack(bufAfter) == atk0 + 6 && DungeonRunDeck.EffectiveHealth(bufAfter) == hp0 + 6 && DungeonRunDeck.EffectiveCost(bufAfter) == cost0 + 2);
    Check("D6 Describe shows the changed stats", DungeonRunDeck.Describe(bufAfter, id => "X") == $"X ({atk0 + 6}/{hp0 + 6}, cost {cost0 + 2})", DungeonRunDeck.Describe(bufAfter, id => "X"));
    // D7 Discount
    int spellUid = FirstSpell(s);
    var sp = DungeonRunDeck.Find(s, spellUid); int sc = C(sp.Id).Cost;
    r = DungeonRunDeck.Discount(run, s, spellUid, "DALA_914e");
    Check("D7 Discount: DALA_914e appended; cost max(0, cost - 3)", r.Ok && DungeonRunDeck.Find(s, spellUid).Mods.SequenceEqual(new[] { "DALA_914e" })
        && DungeonRunDeck.EffectiveCost(DungeonRunDeck.Find(s, spellUid)) == Math.Max(0, sc - 3));
    r = DungeonRunDeck.Discount(run, s, spellUid, "DALA_903e");
    var r2 = DungeonRunDeck.Discount(run, s, spellUid, "ULDA_605e");
    Check("D7 Discount: DALA_903e sets 0, a later -1 stays 0", r.Ok && r2.Ok && DungeonRunDeck.EffectiveCost(DungeonRunDeck.Find(s, spellUid)) == 0
        && DungeonRunDeck.Find(s, spellUid).Mods.SequenceEqual(new[] { "DALA_914e", "DALA_903e", "ULDA_605e" }));
    // D8 SetOpening
    r = DungeonRunDeck.SetOpening(run, s, bufUid, true);
    Check("D8 SetOpening: the flag is set; the plan slot carries the marker after the mods", r.Ok && DungeonRunDeck.Find(s, bufUid).Opening
        && DungeonRunDeck.PlanSlots(s)[s.Cards.FindIndex(c => c.Uid == bufUid)].Mods.SequenceEqual(new[] { "DALA_908e", "DALA_913e", "DALA_905e" })
        && DungeonRunDeck.Find(s, bufUid).Mods.Count == 2);
    // D4 Copy
    int next0 = s.NextUid; int count0 = s.Cards.Count;
    r = DungeonRunDeck.Copy(run, s, bufUid, 3);
    var copies = s.Cards.Skip(count0).ToList();
    Check("D4 Copy x3: origin copy, CopyOf the source, mods kept, opening cleared, new uids", r.Ok && copies.Count == 3
        && copies.All(c => c.Id == bufAfter.Id && c.Origin == "copy" && c.CopyOf == bufUid && !c.Opening && c.Mods.SequenceEqual(new[] { "DALA_908e", "DALA_913e" }))
        && copies.Select(c => c.Uid).SequenceEqual(new[] { next0 + 1, next0 + 2, next0 + 3 }) && DungeonRunDeck.Find(s, bufUid).Opening);
    r = DungeonRunDeck.Copy(run, s, bufUid, 1, keepMods: false);
    Check("D4 Copy without mods", r.Ok && s.Cards.Last().Mods.Count == 0 && s.Cards.Last().CopyOf == bufUid);
    // D5 Replace
    int at = s.Cards.FindIndex(c => c.Uid == bufUid);
    r = DungeonRunDeck.Replace(run, s, bufUid, minions[1], DungeonRunDeck.OriginTavern);
    var rep = DungeonRunDeck.Find(s, bufUid);
    Check("D5 Replace: uid and slot kept, new id, mods and the opening flag dropped", r.Ok && rep.Id == minions[1] && rep.Mods.Count == 0 && !rep.Opening
        && rep.CopyOf == 0 && rep.Origin == "tavern" && s.Cards.FindIndex(c => c.Uid == bufUid) == at && s.Deck[at] == minions[1]);
    // D3 RemoveWhere
    Grow(s, 30, minCost: 3);
    var cheap = s.Cards.Where(c => c.Origin != "treasure" && DungeonRunDeck.EffectiveCost(c) <= 2).Select(c => c.Uid).ToList();
    var keep = s.Cards.Where(c => !cheap.Contains(c.Uid)).Select(c => c.Uid).ToList();
    r = DungeonRunDeck.RemoveWhere(run, s, c => DungeonRunDeck.EffectiveCost(c) <= 2);
    Check("D3 RemoveWhere cost <= 2: all of them at once, nothing else", r.Ok && r.Uids.OrderBy(x => x).SequenceEqual(cheap.OrderBy(x => x))
        && s.Cards.Select(c => c.Uid).SequenceEqual(keep) && cheap.Count > 0, cheap.Count + " removed, " + s.Cards.Count + " left");

    // D9 refusals: each returns its error and leaves the state byte-identical.
    void Refused(string name, DungeonRunState st, Func<DeckEditResult> op, string error)
    {
        string snap = Xml(st);
        var res = op();
        Check("D9 " + name, !res.Ok && res.Error != null && res.Error.StartsWith(error) && Xml(st) == snap, res.ToString());
    }
    var small = NewAct("MAGE", 5);
    Refused("below deckMin: removing from a 10-card deck", small, () => DungeonRunDeck.Remove(run, small, small.Cards[0].Uid), DungeonRunDeck.ErrDeckMin);
    Refused("below deckMin: RemoveWhere that empties too much", small, () => DungeonRunDeck.RemoveWhere(run, small, c => true), DungeonRunDeck.ErrDeckMin);
    var big = Clone(s); Grow(big, 50);
    Refused("above deckMax: adding to a 50-card deck", big, () => DungeonRunDeck.TryAdd(run, big, minions[0], "tavern"), DungeonRunDeck.ErrDeckMax);
    Refused("above deckMax: three copies past 50", big, () => DungeonRunDeck.Copy(run, big, big.Cards.First(c => c.Origin != "treasure").Uid, 3), DungeonRunDeck.ErrDeckMax);
    Refused("an inadmissible id (Aya Blackpaw: seat order)", s, () => DungeonRunDeck.TryAdd(run, s, DungeonRunValidator.AyaId, "tavern"), DungeonRunDeck.ErrInadmissible);
    Refused("an unknown card id", s, () => DungeonRunDeck.TryAdd(run, s, "NOT_A_CARD", "tavern"), DungeonRunDeck.ErrUnknownId);
    string warrior = Cards.All.Where(c => c.Class == CardClass.WARRIOR && c.Collectible && c.Implemented && c.Type == CardType.MINION).Select(c => c.Id)
        .FirstOrDefault(id => DungeonRunValidator.PoolCardReason(id, new[] { "WARRIOR" }, false, null, true) == null);
    Refused("an off-class Replace (a Warrior minion in a Mage run)", s, () => DungeonRunDeck.Replace(run, s, FirstMinion(s), warrior, "tavern"), DungeonRunDeck.ErrClass);
    Refused("an off-class Add", s, () => DungeonRunDeck.TryAdd(run, s, warrior, "tavern"), DungeonRunDeck.ErrClass);
    var withShell = Clone(s);
    string shell = DungeonRunContent.All.First(c => c.Kind == RunContentKind.ActiveTreasure && C(c.ShellId)?.Type == CardType.MINION).ShellId;
    var shellCard = DungeonRunDeck.Add(withShell, shell, DungeonRunDeck.OriginTreasure);
    Refused("a treasure-shell target (Buff)", withShell, () => DungeonRunDeck.Buff(run, withShell, shellCard.Uid, "DALA_908e"), DungeonRunDeck.ErrTreasureShell);
    Refused("a treasure-shell target (Copy)", withShell, () => DungeonRunDeck.Copy(run, withShell, shellCard.Uid, 1), DungeonRunDeck.ErrTreasureShell);
    Refused("a treasure-shell target (Remove)", withShell, () => DungeonRunDeck.Remove(run, withShell, shellCard.Uid), DungeonRunDeck.ErrTreasureShell);
    Check("D9 RemoveWhere never removes a treasure shell", DungeonRunDeck.RemoveWhere(run, withShell, c => c.Uid == shellCard.Uid).Ok && DungeonRunDeck.Find(withShell, shellCard.Uid) != null);
    Refused("an unknown enchantment", s, () => DungeonRunDeck.Buff(run, s, FirstMinion(s), "CS2_182"), DungeonRunDeck.ErrUnknownEnchantment);
    Refused("the opening marker is not a buff", s, () => DungeonRunDeck.Buff(run, s, FirstMinion(s), "DALA_905e"), DungeonRunDeck.ErrUnknownEnchantment);
    Refused("a stat buff on a spell", s, () => DungeonRunDeck.Buff(run, s, FirstSpell(s), "DALA_908e"), DungeonRunDeck.ErrEnchantmentTarget);
    Refused("a cost enchantment as a buff", s, () => DungeonRunDeck.Buff(run, s, FirstMinion(s), "DALA_914e"), DungeonRunDeck.ErrEnchantmentTarget);
    Refused("a weapon buff on a minion", s, () => DungeonRunDeck.Buff(run, s, FirstMinion(s), "ULDA_603e"), DungeonRunDeck.ErrEnchantmentTarget);
    var flags = Clone(s);
    foreach (int k in Enumerable.Range(0, 3)) DungeonRunDeck.SetOpening(run, flags, FirstMinion(flags, k), true);
    Check("D9 fixture: three flags set", flags.Cards.Count(c => c.Opening) == 3);
    Refused("a 4th opening flag", flags, () => DungeonRunDeck.SetOpening(run, flags, FirstMinion(flags, 3), true), DungeonRunDeck.ErrOpeningFlags);
    Refused("an unknown uid", s, () => DungeonRunDeck.Remove(run, s, 99999), DungeonRunDeck.ErrNoCard);
    var legacy = run.NewRun(all, "MAGE", data.FindClass("MAGE").Decks[0], data.FindClass("MAGE").Powers[0], 1, schedule: DungeonRunSchedules.LegacyId, signatureId: run.SignatureOffer(all, run.Data.FindClass("MAGE"), 1).FirstOrDefault());
    Refused("a legacy run has no card records", legacy, () => DungeonRunDeck.TryAdd(run, legacy, minions[0], "tavern"), DungeonRunDeck.ErrNotActRun);
    // Death Knight runes: a card whose runes the deck cannot add.
    var dk = NewAct("DEATHKNIGHT", 11);
    var dkIds = dk.Cards.Select(c => c.Id).ToList();
    var needs = RuneRules.FromCards(dkIds.Select(C));
    string breaker = Cards.All.Where(c => c.Class == CardClass.DEATHKNIGHT && c.Collectible && c.Implemented && DungeonRunValidator.HasRuneCost(c.Id))
        .Select(c => c.Id).OrderBy(x => x, StringComparer.Ordinal)
        .FirstOrDefault(id => !DungeonRunDeck.RunesLegal(run, dk, dkIds.Concat(new[] { id })) && DungeonRunDeck.AddError(run, dk, id, false, out _) == null);
    if (breaker == null) Check("D9 a Death Knight card that breaks the starter's runes exists", false, $"starter runes B{needs.Blood} F{needs.Frost} U{needs.Unholy}");
    else Refused($"a Death Knight rune violation ({breaker} on a B{needs.Blood}/F{needs.Frost}/U{needs.Unholy} deck)", dk, () => DungeonRunDeck.TryAdd(run, dk, breaker, "tavern"), DungeonRunDeck.ErrRunes);
    string fits = Cards.All.Where(c => c.Class == CardClass.DEATHKNIGHT && c.Collectible && c.Implemented && DungeonRunValidator.HasRuneCost(c.Id))
        .Select(c => c.Id).OrderBy(x => x, StringComparer.Ordinal)
        .FirstOrDefault(id => DungeonRunDeck.RunesLegal(run, dk, dkIds.Concat(new[] { id })) && DungeonRunDeck.AddError(run, dk, id, false, out _) == null);
    if (fits != null) Check("D9 a Death Knight card that fits the runes is added", DungeonRunDeck.TryAdd(run, dk, fits, "tavern").Ok, fits);
    else Note("no rune card that fits the Death Knight starter passes admission");

    // Match edits hook (phase 6): slot indices resolve to uids first; all or none.
    var me = Clone(s); int n0 = me.Cards.Count; string id2 = me.Cards[2].Id; int u3 = me.Cards[3].Uid;
    var mr = DungeonRunDeck.ApplyMatchEdits(run, me, new[] { new RunDeckEdit { Op = "remove", SlotIndex = 1 }, new RunDeckEdit { Op = "replace", SlotIndex = 3, NewId = minions[2] } });
    Check("D9b ApplyMatchEdits: a remove then a replace by plan slot", mr.Ok && me.Cards.Count == n0 - 1 && DungeonRunDeck.Find(me, u3).Id == minions[2] && me.Cards[1].Id == id2, mr.ToString());
    string meSnap = Xml(me);
    mr = DungeonRunDeck.ApplyMatchEdits(run, me, new[] { new RunDeckEdit { Op = "remove", SlotIndex = 0 }, new RunDeckEdit { Op = "replace", SlotIndex = 1, NewId = warrior } });
    Check("D9b ApplyMatchEdits: a refused second edit rolls the first back", !mr.Ok && Xml(me) == meSnap, mr.ToString());
}

// ================================================================ D10 engine buffs, D13 fair view
{
    var s = NewAct("MAGE", 202);
    Walk(s, x => x.Phase == DungeonRunPhase.Tavern);
    run.LeaveTavern(s);
    var weapons = DungeonRunTavernNode.DiscoverPool(run, s, "anyClassWeapon");
    Check("D10 fixture: the any-class weapon pool holds weapons", weapons.Count >= 3, weapons.Count.ToString());
    var wr = DungeonRunDeck.TryAdd(run, s, weapons[0], DungeonRunDeck.OriginTavern, new[] { "ULDA_603e" }, classWaived: true);
    int m1 = FirstMinion(s), m2 = FirstMinion(s, 1), sp = FirstSpell(s);
    DungeonRunDeck.Buff(run, s, m1, "DALA_908e");
    DungeonRunDeck.Buff(run, s, m2, "DALA_908e"); DungeonRunDeck.Buff(run, s, m2, "DALA_913e");
    DungeonRunDeck.Discount(run, s, sp, "DALA_914e");
    DungeonRunDeck.SetOpening(run, s, m1, true);
    DungeonRunDeck.SetOpening(run, s, wr.Uids[0], true);
    var plan = run.BuildPlan(s);
    var g = StartPlan(plan, run.MatchSeed(s));
    Check("D10 Player1DeckSlotIds aligns with the plan deck", g.Player1DeckSlotIds.Count == plan.PlayerDeck.Count
        && Enumerable.Range(0, plan.PlayerDeck.Count).All(i => g.IdEntityDic[g.Player1DeckSlotIds[i]].Card.Id == plan.PlayerDeck[i].Id));
    int good = 0, total = 0; var bad = new List<string>();
    for (int i = 0; i < s.Cards.Count; i++)
    {
        var rec = s.Cards[i];
        if (rec.Mods.Count == 0 && !rec.Opening) continue;
        total++;
        var e = g.IdEntityDic[g.Player1DeckSlotIds[i]];
        var card = C(rec.Id);
        bool stats = card.Type == CardType.MINION ? e[GameTag.ATK] == DungeonRunDeck.EffectiveAttack(rec) && e[GameTag.HEALTH] == DungeonRunDeck.EffectiveHealth(rec)
            : card.Type == CardType.WEAPON ? e[GameTag.ATK] == DungeonRunDeck.EffectiveAttack(rec) && e[GameTag.DURABILITY] == DungeonRunDeck.EffectiveHealth(rec) : true;
        bool cost = e.Cost == DungeonRunDeck.EffectiveCost(rec);
        bool order = EnchIds(e).SequenceEqual(plan.PlayerDeckSlots[i].Mods);
        bool zone = rec.Opening ? e.Zone == g.Player1.HandZone : true;
        if (stats && cost && order && zone) good++;
        else bad.Add($"{rec.Id} {e.Zone?.Type} atk {e[GameTag.ATK]} hp {e[GameTag.HEALTH]} dur {e[GameTag.DURABILITY]} cost {e.Cost} vs {DungeonRunDeck.EffectiveAttack(rec)}/{DungeonRunDeck.EffectiveHealth(rec)} c{DungeonRunDeck.EffectiveCost(rec)} [{string.Join(",", EnchIds(e))}]");
    }
    Check("D10 every changed card's entity (deck or hand) has the derived ATK, HEALTH/DURABILITY, COST and its enchantments in order",
        good == total && total == 4, good + " of " + total + " " + string.Join(" | ", bad));
    var e2 = g.IdEntityDic[g.Player1DeckSlotIds[s.Cards.FindIndex(c => c.Uid == m2)]];
    Check("D10 stacked 908e then 913e: +6/+6 and cost +2 on the entity", e2[GameTag.ATK] == C(e2.Card.Id).ATK + 6 && e2[GameTag.HEALTH] == C(e2.Card.Id).Health + 6
        && e2.Cost == C(e2.Card.Id).Cost + 2);
    // Through the mulligan and a few turns: no exception, the buff stays.
    g.Process(ChooseTask.Mulligan(g.Player1, new List<int>())); g.Process(ChooseTask.Mulligan(g.Player2, new List<int>())); g.MainBegin();
    var e1 = g.IdEntityDic[g.Player1DeckSlotIds[s.Cards.FindIndex(c => c.Uid == m1)]];
    // An opening-flag card can be mulliganed like any opening card (a recorded decision); its buff stays either way.
    Check("D10 the flagged buffed minion keeps its +2/+2 through the mulligan (hand or deck)", e1[GameTag.ATK] == C(e1.Card.Id).ATK + 2
        && (e1.Zone == g.Player1.HandZone || e1.Zone == g.Player1.DeckZone), e1.Zone?.Type.ToString());
    // D13 the boss's fair view: no exception; hidden player cards show no deck enchantment.
    g.Process(EndTurnTask.Any(g.Player1));
    var boss = run.CurrentBoss(s);
    var pilot = DungeonRunBossPilot.Build(boss, plan, data, runClasses: run.RunClasses(s));
    var testBudget = new Budget(6000, 2000, 300, 6, 3, 100, 8, turnSearchSeconds: 6, turnWallSeconds: 8);
    var golden = new GoldenPilot(pilot.Profile.WithBudget(testBudget), pilot.Plan, null, pilot.SecretPrior);
    FairBotState view = null; string why = "";
    try { view = golden.Options.FairViewFactory(g, g.Player2.PlayerId, 12345, pilot.Deck.Style); } catch (Exception ex) { why = ex.GetType().Name + ": " + ex.Message; }
    Check("D13 the boss's fair view of an act-run match with deck enchantments builds", view != null, why);
    if (view != null)
    {
        var copy = view.FingerprintGame;
        var hidden = copy.Player1.DeckZone.Concat(copy.Player1.HandZone).ToList();
        bool clean = hidden.All(e => !EnchIds(e).Any(tavernEnchants.Contains))
            && copy.IdEntityDic.Values.OfType<Enchantment>().Where(x => tavernEnchants.Contains(x.Card.Id)).All(x => !hidden.Contains(x.Target as IPlayable));
        bool real = g.Player1.DeckZone.Concat(g.Player1.HandZone).Any(e => EnchIds(e).Any(tavernEnchants.Contains));
        Check("D13 hidden deck and hand entities in the boss's view carry no tavern enchantment (the real game does)", clean && real);
        int decided = 0; string fault = "";
        try
        {
            int idx = 0;
            var (task, trace) = BotDriver.Decide(g, g.Player2.PlayerId, pilot.Deck.Style,
                st => golden.DecidePilot(st, new GoldenContext(g.Turn, idx, 997 * g.Turn, pilot.Profile.WithBudget(testBudget))), ref idx, golden.Options);
            if (task != null) decided++;
        }
        catch (Exception ex) { fault = ex.GetType().Name + ": " + ex.Message; }
        Check("D13 the boss decides a move against the enchanted deck without a fault", decided == 1, fault);
    }
}

// ================================================================ T1 tavern open, T4 validator
{
    int identical = 0, applicable = 0, shells = 0, opened = 0, minionsOnly = 0;
    var offeredCounts = new Dictionary<string, int>(StringComparer.Ordinal);
    for (int seed = 1; seed <= 300; seed++)
    {
        var s = NewAct(seed % 3 == 0 ? "WARRIOR" : seed % 3 == 1 ? "MAGE" : "PRIEST", 1000 + seed);
        Walk(s, x => x.Phase == DungeonRunPhase.Tavern);
        if (s.Phase != DungeonRunPhase.Tavern) continue;
        opened++;
        var t = s.Tavern;
        var again = Clone(s); again.Tavern = null; again.Phase = DungeonRunPhase.PreMatch;
        DungeonRunTavernNode.Open(run, again);
        if (Xml(again) == Xml(s)) identical++;
        // Inapplicable options are never offered: re-check each offer against the state as opened.
        var check = Clone(s);
        if (t.Offers.All(id => DungeonRunTavernNode.Applicable(run, check, table.Option(id)))) applicable++;
        if (t.Friendly.All(u => DungeonRunDeck.Find(s, u)?.Origin != "treasure")) shells++;
        if (t.Friendly.All(u => C(DungeonRunDeck.Find(s, u).Id).Type == CardType.MINION) && t.Bob.All(id => C(id).Type == CardType.MINION && C(id).Cost <= table.BobMaxCostByAct[0])) minionsOnly++;
        foreach (string id in t.Offers) offeredCounts[id] = offeredCounts.TryGetValue(id, out int n) ? n + 1 : 1;
        if (seed == 1) Check("T1 a tavern opens with its act's offer count, 4 friendly and 4 Bob minions", t.Offers.Count == run.CurrentAct(s).TavernOffers && t.Friendly.Count == 4 && t.Bob.Count == 4,
            $"{t.Offers.Count}/{t.Friendly.Count}/{t.Bob.Count}");
    }
    Check("T1 300 seeds reach their first tavern", opened == 300, opened.ToString());
    Check("T1 same seed and node: same Friendly, Bob and Offers (300 seeds)", identical == opened, identical.ToString());
    Check("T1 every offer is applicable to the deck it was offered to (300 seeds)", applicable == opened, applicable.ToString());
    Check("T1 the friendly board never holds a treasure shell; boards hold minions within Bob's act cap", shells == opened && minionsOnly == opened);
    Check("T1 House Special and the weight-0 alias are never offered", !offeredCounts.ContainsKey("ULDA_607") && !offeredCounts.ContainsKey("ULDA_912"));
    Note("T1 offers over 300 openings: " + string.Join(" ", offeredCounts.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => p.Key + "=" + p.Value)));
    // A deck at deckMin with no spell: no removal and no spell option is ever offered.
    int clean = 0;
    for (int seed = 1; seed <= 60; seed++)
    {
        var s = NewAct("WARRIOR", 5000 + seed);
        Walk(s, x => x.Phase == DungeonRunPhase.Tavern);
        s.Tavern = null; s.Phase = DungeonRunPhase.PreMatch;
        var keep = s.Cards.Where(c => C(c.Id).Type == CardType.MINION && c.Origin != "treasure").Take(10).ToList();
        s.Cards = keep; DungeonRunDeck.Sync(s);
        Grow(s, 10);   // a deck with fewer than ten minions is topped up with catalog minions
        DungeonRunTavernNode.Open(run, s);
        var ops = s.Tavern.Offers.Select(id => table.Option(id).Op).ToList();
        if (s.Cards.Count == 10 && !ops.Any(op => op.StartsWith("remove") || op == "discountFromSample")) clean++;
        else if (clean + 3 > seed) Note($"T1 low-deck seed {seed}: {s.Cards.Count} cards, offers {string.Join(",", s.Tavern.Offers.Select(id => id + ":" + table.Option(id).Op))}");
    }
    Check("T1 a 10-card all-minion deck is never offered a removal, Kindle or The Upper Hand (60 seeds)", clean == 60, clean.ToString());

    // T4 the tavern validator.
    Check("T4 tavern-v1.json validates", DungeonRunValidator.ValidateTavern(table, null).Count == 0, string.Join(" | ", DungeonRunValidator.ValidateTavern(table, null)));
    var house = DungeonRunTavernTable.Parse(tavernJson.Replace("{\"id\": \"DALA_901\", \"op\": \"recruit\"", "{\"id\": \"ULDA_607\", \"op\": \"recruit\", \"cost\": 1, \"weight\": 1}, {\"id\": \"DALA_901\", \"op\": \"recruit\""));
    Check("T4 ULDA_607 in options is refused", DungeonRunValidator.ValidateTavern(house, null).Any(e => e.Contains("ULDA_607")), string.Join(" | ", DungeonRunValidator.ValidateTavern(house, null)));
    var badEnch = DungeonRunTavernTable.Parse(tavernJson.Replace("\"ench\": \"DALA_908e\"", "\"ench\": \"DALA_908e2\""));
    Check("T4 an unknown ench is refused", DungeonRunValidator.ValidateTavern(badEnch, null).Any(e => e.Contains("DALA_908e2")));
    var badOp = DungeonRunTavernTable.Parse(tavernJson.Replace("\"op\": \"health\"", "\"op\": \"heal\""));
    Check("T4 an unknown op is refused", DungeonRunValidator.ValidateTavern(badOp, null).Any(e => e.Contains("unknown op heal")));
    var badPool = DungeonRunTavernTable.Parse(tavernJson.Replace("\"pool\": \"legendaryMinion\"", "\"pool\": \"legends\""));
    Check("T4 an unknown pool is refused", DungeonRunValidator.ValidateTavern(badPool, null).Any(e => e.Contains("unknown pool legends")));
    // Pool sizes per class (measured, not a load-time rule).
    var shortPools = new List<string>();
    foreach (var cls in data.Classes)
    {
        var s = run.NewRun(all, cls.Id, cls.Decks[0], cls.Powers[0], 1, signatureId: run.SignatureOffer(all, run.Data.FindClass(cls.Id), 1).FirstOrDefault());
        foreach (string pool in DungeonRunTavernTable.KnownPools)
        {
            int n = DungeonRunTavernNode.DiscoverPool(run, s, pool).Count;
            if (n < 3) shortPools.Add(cls.Id + ":" + pool + "=" + n);
        }
        int bob = DungeonRunTavernNode.BobPool(run, s).Count;
        if (bob < 4) shortPools.Add(cls.Id + ":bob(act I)=" + bob);
    }
    Note("T4 tavern pools under 3 cards (Bob under 4): " + (shortPools.Count == 0 ? "none" : string.Join(" ", shortPools)));
    Check("T4 every pool resolves to at least 3 cards for every class with catalog entries",
        shortPools.All(p => p.StartsWith("DEATHKNIGHT") || p.StartsWith("DEMONHUNTER")), string.Join(" ", shortPools));
}

// ================================================================ T2 per option, T3 resume mid-tavern
{
    var fixture = NewAct("MAGE", 303);
    Walk(fixture, x => x.Phase == DungeonRunPhase.Tavern);
    Grow(fixture, 24, minCost: 3);
    var friendlyAll = DungeonRunTavernNode.FriendlyCandidates(fixture).Select(c => c.Uid).ToList();
    var bobAll = DungeonRunTavernNode.BobPool(run, fixture);
    DungeonRunState Fix(string option, int gold = 10)
    {
        var s = Clone(fixture);
        s.Tavern = new DungeonRunTavern { Node = s.Node, Gold = gold, Friendly = friendlyAll.Take(4).ToList(), Bob = bobAll.Take(4).ToList(), Offers = new List<string> { option } };
        s.Phase = DungeonRunPhase.Tavern;
        return s;
    }
    int Gold(DungeonRunState s) => s.Tavern.Gold;
    int Cost(string option) => table.Option(option).Cost;
    DeckEditResult Buy(DungeonRunState s, string option) => DungeonRunTavernNode.Buy(run, s, option);
    DeckEditResult Choose(DungeonRunState s, string candidate) => DungeonRunTavernNode.Choose(run, s, candidate);
    string U(int uid) => uid.ToString();
    var tested = new HashSet<string>(StringComparer.Ordinal);

    foreach (string recruit in new[] { "DALA_901", "ULDA_912" })
    {
        var s = Fix(recruit); int n = s.Cards.Count; string target = s.Tavern.Bob[1];
        var b = Buy(s, recruit);
        bool pending = b.Ok && s.Tavern.Pending != null && s.Tavern.Pending.Candidates.SequenceEqual(s.Tavern.Bob.Distinct()) && Gold(s) == 10;
        var c = Choose(s, target);
        Check($"T2 {recruit} Recruit: choose a Bob minion; it joins the deck (origin tavern), leaves Bob's board, 1 gold",
            pending && c.Ok && s.Cards.Count == n + 1 && s.Cards.Last().Id == target && s.Cards.Last().Origin == "tavern" && !s.Tavern.Bob.Contains(target)
            && Gold(s) == 10 - Cost(recruit) && s.Tavern.Bought.Single().Option == recruit && s.Tavern.Pending == null, b + " / " + c);
        tested.Add(recruit);
    }
    {
        var s = Fix("DALA_902"); int n = s.Cards.Count; int target = s.Tavern.Friendly[0];
        var b = Buy(s, "DALA_902"); var c = Choose(s, U(target));
        Check("T2 DALA_902 Dismiss: the chosen friendly minion leaves the deck and the board", b.Ok && c.Ok && s.Cards.Count == n - 1 && DungeonRunDeck.Find(s, target) == null
            && !s.Tavern.Friendly.Contains(target) && Gold(s) == 10 - Cost("DALA_902"));
        tested.Add("DALA_902");
    }
    {
        var s = Fix("DALA_903"); string before = Ids(s);
        var b = Buy(s, "DALA_903");
        var changed = s.Cards.Where(c => c.Mods.Contains("DALA_903e")).ToList();
        var again = Fix("DALA_903"); Buy(again, "DALA_903");
        Check("T2 DALA_903 Take a Chance: one random card costs (0), deterministic", b.Ok && changed.Count == 1 && DungeonRunDeck.EffectiveCost(changed[0]) == 0
            && Ids(s) == Ids(again) && Gold(s) == 10 - Cost("DALA_903") && s.Tavern.Pending == null, b.ToString());
        tested.Add("DALA_903");
    }
    {
        var s = Fix("DALA_904"); int hb = s.HealthBonus;
        var b = Buy(s, "DALA_904");
        Check("T2 DALA_904 Good Food: +5 starting Health for the run", b.Ok && s.HealthBonus == hb + 5 && Gold(s) == 10 - Cost("DALA_904"));
        run.LeaveTavern(s);
        Walk(s, x => x.Phase == DungeonRunPhase.PreMatch);
        var plan = run.BuildPlan(s);
        var g = StartPlan(plan, 1, history: false);
        Check("T2 DALA_904: the plan and the engine hero carry the +5", plan.PlayerHealthBonus == run.PlayerHealthBonus(s) && plan.PlayerHealthBonus >= 5
            && g.Player1.Hero.BaseHealth == plan.PlayerHero.Health + plan.PlayerHealthBonus + plan.PlayerTreasures.Sum(k => DungeonRunContent.Get(k).HeroHealthBonus), g.Player1.Hero.BaseHealth.ToString());
        tested.Add("DALA_904");
    }
    {
        var s = Fix("DALA_905"); int target = s.Tavern.Friendly[1];
        var b = Buy(s, "DALA_905");
        bool cands = s.Tavern.Pending.Candidates.SequenceEqual(s.Tavern.Friendly.Select(U));
        var c = Choose(s, U(target));
        Check("T2 DALA_905 Right Hand Man: the chosen minion always starts in hand (flag; no stored mod)", b.Ok && cands && c.Ok && DungeonRunDeck.Find(s, target).Opening
            && DungeonRunDeck.Find(s, target).Mods.Count == 0 && Gold(s) == 10 - Cost("DALA_905"));
        tested.Add("DALA_905");
    }
    foreach (var (opt, ench) in new[] { ("DALA_906", "DALA_906e"), ("ULDA_605", "ULDA_605e") })
    {
        var s = Fix(opt); int n = s.Cards.Count; var bob = s.Tavern.Bob.ToList();
        var b = Buy(s, opt);
        var addedCards = s.Cards.Skip(n).ToList();
        Check($"T2 {opt}: all of Bob's minions join the deck with {ench}; Bob's board empties", b.Ok && addedCards.Select(c => c.Id).SequenceEqual(bob)
            && addedCards.All(c => c.Mods.SequenceEqual(new[] { ench }) && c.Origin == "tavern") && s.Tavern.Bob.Count == 0 && Gold(s) == 10 - Cost(opt), b.ToString());
        tested.Add(opt);
    }
    foreach (var (opt, pool) in new[] { ("DALA_907", "legendaryMinion"), ("ULDA_602", "spell"), ("ULDA_603", "anyClassWeapon") })
    {
        var s = Fix(opt); int n = s.Cards.Count;
        var b = Buy(s, opt);
        var cands = s.Tavern.Pending?.Candidates ?? new List<string>();
        var poolIds = DungeonRunTavernNode.DiscoverPool(run, s, pool);
        bool shape = cands.Count == 3 && cands.Distinct().Count() == 3 && cands.All(poolIds.Contains);
        var c = Choose(s, cands.LastOrDefault());
        var last = s.Cards.Last();
        bool effect = opt == "DALA_907" ? C(last.Id).Rarity == Rarity.LEGENDARY && C(last.Id).Type == CardType.MINION && last.Mods.Count == 0
            : opt == "ULDA_602" ? C(last.Id).Type == CardType.SPELL && last.Opening
            : C(last.Id).Type == CardType.WEAPON && last.Mods.SequenceEqual(new[] { "ULDA_603e" });
        Check($"T2 {opt}: discover three {pool} cards; the chosen one joins the deck as described", b.Ok && shape && c.Ok && s.Cards.Count == n + 1
            && last.Id == cands.Last() && effect && Gold(s) == 10 - Cost(opt), $"{b} / {c} / {string.Join(",", cands)}");
        tested.Add(opt);
    }
    foreach (var (opt, ench) in new[] { ("DALA_908", "DALA_908e"), ("DALA_913", "DALA_913e") })
    {
        var s = Fix(opt); int target = s.Tavern.Friendly[2];
        var b = Buy(s, opt); var c = Choose(s, U(target));
        Check($"T2 {opt}: the chosen friendly minion gets {ench}", b.Ok && c.Ok && DungeonRunDeck.Find(s, target).Mods.SequenceEqual(new[] { ench }) && Gold(s) == 10 - Cost(opt));
        tested.Add(opt);
    }
    {
        var s = Fix("DALA_909"); var board = s.Tavern.Friendly.ToList(); int n = s.Cards.Count;
        var b = Buy(s, "DALA_909");
        Check("T2 DALA_909 You're All Fired!: every friendly board minion leaves the deck", b.Ok && s.Cards.Count == n - board.Count && board.All(u => DungeonRunDeck.Find(s, u) == null)
            && s.Tavern.Friendly.Count == 0 && Gold(s) == 10 - Cost("DALA_909"));
        tested.Add("DALA_909");
    }
    {
        var s = Fix("DALA_910"); int target = s.Tavern.Friendly[0]; int n = s.Cards.Count;
        DungeonRunDeck.Buff(run, s, target, "DALA_908e");
        var b = Buy(s, "DALA_910"); var c = Choose(s, U(target));
        var copies = s.Cards.Skip(n).ToList();
        Check("T2 DALA_910 The Gang's All Here!: three copies, mods kept", b.Ok && c.Ok && copies.Count == 3 && copies.All(x => x.CopyOf == target && x.Mods.SequenceEqual(new[] { "DALA_908e" }))
            && Gold(s) == 10 - Cost("DALA_910"));
        tested.Add("DALA_910");
    }
    foreach (var opt in new[] { "DALA_911", "DALA_914" })
    {
        var s = Fix(opt); int n = s.Cards.Count;
        var b = Buy(s, opt);
        var cands = s.Tavern.Pending?.Candidates ?? new List<string>();
        int spellsInDeck = DungeonRunTavernNode.SampleCards(s, "spell").Count;
        bool shape = cands.Count == Math.Min(4, spellsInDeck) && cands.All(x => C(DungeonRunDeck.Find(s, int.Parse(x)).Id).Type == CardType.SPELL);
        int target = int.Parse(cands[0]);
        var c = Choose(s, cands[0]);
        bool effect = opt == "DALA_911" ? s.Cards.Count == n - 1 && DungeonRunDeck.Find(s, target) == null : DungeonRunDeck.Find(s, target).Mods.SequenceEqual(new[] { "DALA_914e" });
        Check($"T2 {opt}: look at up to four deck spells, change the chosen one", b.Ok && shape && c.Ok && effect && Gold(s) == 10 - Cost(opt), $"{cands.Count} of {spellsInDeck} spells");
        tested.Add(opt);
    }
    {
        var s = Fix("DALA_912"); var bob = s.Tavern.Bob.ToList();
        var b = Buy(s, "DALA_912");
        var again = Fix("DALA_912"); Buy(again, "DALA_912");
        Check("T2 DALA_912 Brood: Bob's board refills once, deterministically", b.Ok && s.Tavern.Rerolled && s.Tavern.Bob.Count == 4 && !s.Tavern.Bob.SequenceEqual(bob)
            && s.Tavern.Bob.SequenceEqual(again.Tavern.Bob) && Gold(s) == 10 && !Buy(s, "DALA_912").Ok);
        tested.Add("DALA_912");
    }
    {
        var s = Fix("ULDA_601"); int target = s.Tavern.Friendly[1]; int at = s.Cards.FindIndex(x => x.Uid == target);
        var b = Buy(s, "ULDA_601"); var c1 = Choose(s, U(target));
        var cands = s.Tavern.Pending?.Candidates ?? new List<string>();
        bool step = c1.Ok && s.Tavern.Pending?.Target == target && cands.Count == 3 && cands.All(id => C(id).Type == CardType.MINION) && Gold(s) == 10;
        var c2 = Choose(s, cands.FirstOrDefault());
        var rec = DungeonRunDeck.Find(s, target);
        Check("T2 ULDA_601 Hiring Replacements: choose a minion, then discover its replacement in the same slot", b.Ok && step && c2.Ok && rec.Id == cands[0]
            && s.Cards.FindIndex(x => x.Uid == target) == at && rec.Mods.Count == 0 && Gold(s) == 10 - Cost("ULDA_601") && s.Tavern.Pending == null, $"{b} {c1} {c2}");
        tested.Add("ULDA_601");
    }
    {
        var s = Fix("ULDA_604");
        var b = Buy(s, "ULDA_604");
        var cands = s.Tavern.Pending?.Candidates ?? new List<string>();
        var c = Choose(s, cands.FirstOrDefault());
        var race = C(cands.FirstOrDefault() ?? "CS2_231").GetRawRace();
        Check("T2 ULDA_604 Party of Four: discover a minion; Bob's board becomes minions of its type", b.Ok && c.Ok && s.Tavern.Bob.Count > 0 && s.Tavern.Bob.All(id => C(id).IsRace(race))
            && Gold(s) == 10 - Cost("ULDA_604"), race + " " + string.Join(",", s.Tavern.Bob));
        tested.Add("ULDA_604");
    }
    {
        var s = Fix("ULDA_606"); var board = s.Tavern.Friendly.ToList();
        var b = Buy(s, "ULDA_606"); var c = Choose(s, U(board[1]));
        Check("T2 ULDA_606 Work, Work!: the chosen minion leaves; both neighbours get +2/+2", b.Ok && c.Ok && DungeonRunDeck.Find(s, board[1]) == null
            && DungeonRunDeck.Find(s, board[0]).Mods.SequenceEqual(new[] { "ULDA_606e" }) && DungeonRunDeck.Find(s, board[2]).Mods.SequenceEqual(new[] { "ULDA_606e" })
            && DungeonRunDeck.Find(s, board[3]).Mods.Count == 0 && Gold(s) == 10 - Cost("ULDA_606"));
        var edge = Fix("ULDA_606");
        Buy(edge, "ULDA_606"); Choose(edge, U(board[0]));
        Check("T2 ULDA_606 at the board's edge: only the one neighbour", DungeonRunDeck.Find(edge, board[0]) == null && DungeonRunDeck.Find(edge, board[1]).Mods.SequenceEqual(new[] { "ULDA_606e" })
            && DungeonRunDeck.Find(edge, board[2]).Mods.Count == 0);
        tested.Add("ULDA_606");
    }
    {
        var s = Fix("ULDA_608");
        var cheap = s.Cards.Where(x => x.Origin != "treasure" && DungeonRunDeck.EffectiveCost(x) <= 2).Select(x => x.Uid).ToList();
        bool canRun = s.Cards.Count - cheap.Count >= run.DeckMin(s);
        var b = Buy(s, "ULDA_608");
        Check("T2 ULDA_608 Do the Math: every card costing (2) or less leaves at once", canRun && b.Ok && cheap.All(u => DungeonRunDeck.Find(s, u) == null)
            && s.Cards.All(x => x.Origin == "treasure" || DungeonRunDeck.EffectiveCost(x) > 2) && Gold(s) == 10 - Cost("ULDA_608"), $"{cheap.Count} cheap of {s.Cards.Count} / {b}");
        // Below deckMin: a 10-card deck of cheap cards is refused (and not applicable, so never offered).
        var low = NewAct("MAGE", 9);
        low.Tavern = new DungeonRunTavern { Node = low.Node, Gold = 10, Offers = new List<string> { "ULDA_608" } }; low.Phase = DungeonRunPhase.Tavern;
        string snap = Xml(low);
        var refused = Buy(low, "ULDA_608");
        // Buy re-checks Applicable first (review fix), so the counter refuses it as "nothing"; the deck change itself is deckMin.
        var direct = DungeonRunDeck.RemoveWhere(run, low, c => DungeonRunDeck.EffectiveCost(c) <= 2);
        Check("T2 ULDA_608 respects deckMin: refused, nothing changed, and not applicable", !refused.Ok && refused.Error == DungeonRunTavernNode.ErrNothing && Xml(low) == snap
            && !DungeonRunTavernNode.Applicable(run, low, table.Option("ULDA_608")) && direct.Error == DungeonRunDeck.ErrDeckMin, refused + " / " + direct);
        tested.Add("ULDA_608");
    }
    var active = table.Options.Where(o => o.Weight > 0 || o.AliasOf != null).Select(o => o.Id).ToList();
    Check("T2 every active option (21, plus the alias) was exercised", active.Count == 22 && active.All(tested.Contains), string.Join(",", active.Where(o => !tested.Contains(o))));
    // Refusals at the counter.
    {
        var s = Fix("DALA_908", gold: 0);
        string snap = Xml(s);
        var r = Buy(s, "DALA_908");
        Check("T2 not enough gold: refused, nothing changed", !r.Ok && r.Error == DungeonRunTavernNode.ErrGold && Xml(s) == snap);
        var s2 = Fix("DALA_908");
        Check("T2 an option not on offer is refused", Buy(s2, "DALA_902").Error == DungeonRunTavernNode.ErrNotOffered);
        Buy(s2, "DALA_908");
        Check("T2 a second purchase while one is pending is refused", Buy(s2, "DALA_908").Error == DungeonRunTavernNode.ErrPending);
        Check("T2 a candidate not offered is refused", Choose(s2, "99999").Error == DungeonRunTavernNode.ErrNotCandidate);
        bool leave = false; try { run.LeaveTavern(s2); } catch (InvalidOperationException) { leave = true; }
        Check("T2 leaving with a pending purchase is refused", leave);
    }
    // T3 resume mid-tavern: save while Pending, reload, Choose: as without a reload; Back refunds nothing because nothing was charged.
    {
        var s = Fix("ULDA_601"); int target = s.Tavern.Friendly[0];
        Buy(s, "ULDA_601"); Choose(s, U(target));
        var reloaded = Clone(s);
        Check("T3 a pending two-step purchase serializes byte-identical after a reload", Xml(reloaded) == Xml(s));
        string pick = s.Tavern.Pending.Candidates[1];
        Choose(s, pick); Choose(reloaded, pick);
        Check("T3 continuing after the reload gives the same state", Xml(reloaded) == Xml(s) && DungeonRunDeck.Find(s, target).Id == pick);
        var b = Fix("DALA_907"); int g0 = Gold(b);
        Buy(b, "DALA_907");
        var back = DungeonRunTavernNode.Back(b);
        Check("T3 Back from Pending: no purchase recorded, gold unchanged (nothing was charged)", back.Ok && b.Tavern.Pending == null && b.Tavern.Bought.Count == 0 && Gold(b) == g0);
        Check("T3 after Back the option can be bought again", Buy(b, "DALA_907").Ok && b.Tavern.Pending != null);
    }
}

// ================================================================ L1 long seeded runs with taverns (all nodes, every purchase)
{
    string Pass()
    {
        var digests = new List<string>();
        int bad = 0, plans = 0, slotsWithMods = 0, flagged = 0, bought = 0, faults = 0;
        for (int seed = 1; seed <= 30; seed++)
        {
            var cls = data.Classes[seed % data.Classes.Count];
            var s = run.NewRun(all, cls.Id, cls.Decks[seed % cls.Decks.Count], cls.Powers[0], 70000 + seed, signatureId: run.SignatureOffer(all, run.Data.FindClass(cls.Id), 70000 + seed).FirstOrDefault());
            Walk(s, x => false, shop: t =>
            {
                foreach (string id in t.Tavern.Offers.ToList())
                {
                    var r = DungeonRunTavernNode.Buy(run, t, id);
                    while (r.Ok && t.Tavern.Pending != null) r = DungeonRunTavernNode.Choose(run, t, t.Tavern.Pending.Candidates[seed % t.Tavern.Pending.Candidates.Count]);
                    if (t.Tavern.Pending != null) DungeonRunTavernNode.Back(t);
                    if (r.Ok) bought++;
                }
                if (t.Cards.Count < run.DeckMin(t) || t.Cards.Count > run.DeckMax(t) || DungeonRunDeck.MirrorError(t) != null) bad++;
            }, atFight: f =>
            {
                if (f.Cards.Count < run.DeckMin(f) || f.Cards.Count > run.DeckMax(f) || DungeonRunDeck.MirrorError(f) != null) bad++;
                var plan = run.BuildPlan(f); plans++;
                slotsWithMods += plan.PlayerDeckSlots.Count(x => x.Mods.Length > 0);
                flagged += plan.PlayerDeckSlots.Count(x => x.Opening);
                try { var g = StartPlan(plan, run.MatchSeed(f), history: false); if (g.Player1DeckSlotIds.Count != plan.PlayerDeck.Count) faults++; }
                catch (Exception) { faults++; }
            });
            if (s.Phase != DungeonRunPhase.Victory) bad++;
            digests.Add(Hash(Xml(s)));
        }
        return $"{Hash(string.Join(",", digests))} bad {bad} plans {plans} mods {slotsWithMods} flags {flagged} bought {bought} faults {faults}";
    }
    string a = Pass(), b = Pass();
    Note("L1 " + a);
    Check("L1 30 seeds bought every tavern offer they could, through all nodes to Victory: decks within 10..50, mirrors intact, every plan starts",
        a.Contains(" bad 0 ") && a.Contains(" faults 0") && !a.Contains(" mods 0 ") && !a.Contains(" bought 0 "), a);
    Check("L1 the digest of the final states is stable across two passes", a == b, b);
}

// ================================================================ L2 a full deck skips drafts that would overflow deckMax
{
    var s = NewAct("MAGE", 404);
    Walk(s, x => x.Floor == 7 && x.Phase == DungeonRunPhase.PreMatch);
    Grow(s, run.DeckMax(s) - 1);
    var scratch = new DungeonRunProfile();
    run.BeginMatch(s); run.RecordOutcome(s, scratch, DungeonRunOutcome.Win);
    Check("L2 a 49-card act deck after a won fight: no two-card bundle draft opens (deckMax 50)", s.Phase != DungeonRunPhase.BundleDraft && s.Deck.Count <= run.DeckMax(s), s.Phase + " " + s.Deck.Count);
    Walk(s, x => x.Phase == DungeonRunPhase.PreMatch);
    Check("L2 the run continues to its next fight with a legal deck", s.Phase == DungeonRunPhase.PreMatch && run.BuildPlan(s).PlayerDeck.Count <= run.DeckMax(s));
}

// ================================================================ R review fixes: no free rerolls, stale offers, copies, cost through the mulligan
{
    var baseRun = NewAct("MAGE", 4242);
    Walk(baseRun, x => x.Phase == DungeonRunPhase.Tavern);
    DungeonRunState Offer(string option, int gold = 9)
    {
        var t = Clone(baseRun);
        t.Tavern.Offers = new List<string> { option }; t.Tavern.Gold = gold; t.Tavern.Bought.Clear(); t.Tavern.Draws.Clear();
        return t;
    }
    // R1 Buy, Back, Buy shows the same choices for every sampling op; the step does not move on the re-buy; a reload keeps them.
    foreach (string opt in new[] { "DALA_907", "ULDA_602", "ULDA_603", "DALA_911", "DALA_914", "ULDA_604" })
    {
        var t = Offer(opt);
        var first = DungeonRunTavernNode.Buy(run, t, opt);
        var c1 = t.Tavern.Pending?.Candidates.ToList() ?? new List<string>(); int step1 = t.Tavern.Step;
        bool same = first.Ok && c1.Count > 0, stepSame = true;
        for (int i = 0; i < 5 && same; i++)
        {
            DungeonRunTavernNode.Back(t);
            if (i == 2) t = Clone(t);
            var again = DungeonRunTavernNode.Buy(run, t, opt);
            same &= again.Ok && t.Tavern.Pending.Candidates.SequenceEqual(c1);
            stepSame &= t.Tavern.Step == step1;
        }
        Check($"R1 {opt}: Buy, Back, Buy (five times, one reload) shows the same choices and draws nothing new", same && stepSame && t.Tavern.Gold == 9,
            first + " " + string.Join(",", c1) + " / " + string.Join(",", t.Tavern.Pending?.Candidates ?? new List<string>()) + " step " + step1 + "->" + t.Tavern.Step);
    }
    {
        var t = Offer("ULDA_601");
        int a = t.Tavern.Friendly[0], b = t.Tavern.Friendly[1];
        DungeonRunTavernNode.Buy(run, t, "ULDA_601"); DungeonRunTavernNode.Choose(run, t, a.ToString());
        var r1 = t.Tavern.Pending.Candidates.ToList(); int step = t.Tavern.Step;
        DungeonRunTavernNode.Back(t);
        DungeonRunTavernNode.Buy(run, t, "ULDA_601"); DungeonRunTavernNode.Choose(run, t, b.ToString());
        var r2 = t.Tavern.Pending.Candidates.ToList();
        var done = DungeonRunTavernNode.Choose(run, t, r2[0]);
        Check("R1 ULDA_601 step two: Back and another target shows the same replacements; the purchase then completes",
            r1.Count > 0 && r1.SequenceEqual(r2) && t.Tavern.Step == step && done.Ok && DungeonRunDeck.Find(t, b).Id == r2[0], string.Join(",", r1) + " / " + string.Join(",", r2));
    }
    {
        // The kept draw is saved on the tavern, equals the pending choices and survives a reload byte for byte.
        var x = Offer("DALA_907"); var y = Offer("DALA_907");
        DungeonRunTavernNode.Buy(run, x, "DALA_907"); DungeonRunTavernNode.Buy(run, y, "DALA_907");
        Check("R1 the kept draw is saved on the tavern and equals the pending choices", x.Tavern.Draws.Count == 1 && x.Tavern.Draws[0].Option == "DALA_907"
            && x.Tavern.Draws[0].Candidates.SequenceEqual(x.Tavern.Pending.Candidates) && Xml(x) == Xml(y) && Xml(Clone(x)) == Xml(x));
        var left = Offer("DALA_912");
        run.LeaveTavern(left);
        Check("R1 leaving the tavern drops its draws with it", left.Tavern == null);
    }
    // R2 Buy re-checks Applicable: an offer an earlier purchase made useless is refused before any choice opens.
    {
        var t = Offer("ULDA_602");
        foreach (var uid in t.Cards.Where(c => c.Origin != "treasure").Take(3).Select(c => c.Uid).ToList()) DungeonRunDeck.SetOpening(run, t, uid, true);
        string snap = Xml(t);
        var r = DungeonRunTavernNode.Buy(run, t, "ULDA_602");
        Check("R2 three opening flags already: ULDA_602 is refused at Buy (nothing), state unchanged, not applicable",
            !r.Ok && r.Error == DungeonRunTavernNode.ErrNothing && Xml(t) == snap && !DungeonRunTavernNode.Applicable(run, t, table.Option("ULDA_602")), r.ToString());
        var full = Offer("DALA_907");
        Grow(full, run.DeckMax(full));
        string fsnap = Xml(full);
        var rf = DungeonRunTavernNode.Buy(run, full, "DALA_907");
        Check("R2 a full deck: a discover is refused at Buy, state unchanged", !rf.Ok && rf.Error == DungeonRunTavernNode.ErrNothing && Xml(full) == fsnap, rf.ToString());
        var empty = Offer("DALA_902");
        foreach (int uid in empty.Tavern.Friendly.ToList()) DungeonRunDeck.Remove(run, empty, uid);
        var re = DungeonRunTavernNode.Buy(run, empty, "DALA_902");
        Check("R2 an emptied friendly board: Dismiss is refused at Buy", !re.Ok && re.Error == DungeonRunTavernNode.ErrNothing && empty.Tavern.Pending == null, re.ToString());
    }
    // R3 a copy obeys the class rule: an off-class grant minion is never multiplied; every starter card can be copied.
    {
        var grants = data.Bundles.Where(b => b.IsPool).SelectMany(b => b.Candidates.Where(c => c.Grant)).Select(c => c.Id).Distinct()
            .Where(id => C(id).Type == CardType.MINION && C(id).Class != CardClass.NEUTRAL && C(id).Class != CardClass.MAGE).OrderBy(x => x, StringComparer.Ordinal).ToList();
        Check("R3 fixture: an off-class grant minion exists in the catalog", grants.Count > 0, grants.Count.ToString());
        var t = Offer("DALA_910", 9);
        var rec = DungeonRunDeck.Add(t, grants[0], DungeonRunDeck.OriginDraft);
        int before = t.Cards.Count;
        var copy = DungeonRunDeck.Copy(run, t, rec.Uid, 3);
        Check("R3 Copy of an off-class grant minion is refused (class), the deck unchanged", !copy.Ok && copy.Error == DungeonRunDeck.ErrClass && t.Cards.Count == before, grants[0] + " " + copy);
        t.Tavern.Friendly = new List<int> { rec.Uid };
        Check("R3 ... and The Gang's All Here! is not applicable with only that minion on the board", !DungeonRunTavernNode.Applicable(run, t, table.Option("DALA_910"))
            && DungeonRunTavernNode.Buy(run, t, "DALA_910").Error == DungeonRunTavernNode.ErrNothing);
        int legal = FirstMinion(t);
        t.Tavern.Friendly = new List<int> { rec.Uid, legal };
        var b = DungeonRunTavernNode.Buy(run, t, "DALA_910");
        Check("R3 ... with a class-legal minion beside it, only that one is a copy choice", b.Ok && t.Tavern.Pending.Candidates.SequenceEqual(new[] { legal.ToString() }),
            string.Join(",", t.Tavern.Pending?.Candidates ?? new List<string>()));
        var bad = new List<string>();
        foreach (var cls in data.Classes)
            foreach (string deckId in cls.Decks)
            {
                var r = run.NewRun(all, cls.Id, deckId, cls.Powers[0], 11, signatureId: run.SignatureOffer(all, run.Data.FindClass(cls.Id), 11).FirstOrDefault());
                foreach (var c in r.Cards) if (DungeonRunDeck.CopyError(run, r, c.Uid) != null) bad.Add(cls.Id + ":" + c.Id);
            }
        Check("R3 every starter card of every class row can be copied", bad.Count == 0, string.Join(" ", bad.Distinct().Take(8)));
    }
    // R4 tavern cost enchantments survive the mulligan and the redraw, and are what the player pays.
    {
        var s = NewAct("MAGE", 202);
        Walk(s, x => x.Phase == DungeonRunPhase.Tavern);
        run.LeaveTavern(s);
        var mins = s.Cards.Where(c => c.Origin != "treasure" && C(c.Id).Type == CardType.MINION && C(c.Id).Cost >= 3).Select(c => c.Uid).ToList();
        int a = mins[0], b = mins[1], c3 = mins[2];
        DungeonRunDeck.Discount(run, s, a, "DALA_914e"); DungeonRunDeck.SetOpening(run, s, a, true);
        DungeonRunDeck.Discount(run, s, b, "DALA_903e"); DungeonRunDeck.Buff(run, s, b, "DALA_913e"); DungeonRunDeck.SetOpening(run, s, b, true);
        DungeonRunDeck.Discount(run, s, c3, "ULDA_605e"); DungeonRunDeck.Buff(run, s, c3, "DALA_908e"); DungeonRunDeck.SetOpening(run, s, c3, true);
        var cp = DungeonRunDeck.Copy(run, s, c3, 1);
        var watch = new List<int> { a, b, c3, cp.Uids[0] };
        var plan = run.BuildPlan(s);
        var g = StartPlan(plan, run.MatchSeed(s));
        IPlayable E(int uid) => g.IdEntityDic[g.Player1DeckSlotIds[s.Cards.FindIndex(x => x.Uid == uid)]];
        int Want(int uid) => DungeonRunDeck.EffectiveCost(DungeonRunDeck.Find(s, uid));
        string Show() => string.Join(" ", watch.Select(u => $"{E(u).Card.Id} {E(u).Zone?.Type} {E(u).Cost}/{Want(u)}"));
        bool atStart = watch.All(u => E(u).Cost == Want(u));
        string start = Show();
        var flaggedIds = new[] { a, b, c3 }.Select(u => E(u).Id).ToList();
        var keep = g.Player1.HandZone.Where(e => !flaggedIds.Contains(e.Id)).Select(e => e.Id).ToList();
        g.Process(ChooseTask.Mulligan(g.Player1, keep)); g.Process(ChooseTask.Mulligan(g.Player2, new List<int>()));
        g.MainBegin();
        var p1 = g.Player1;
        bool sentBack = new[] { a, b, c3 }.All(u => E(u).Zone == p1.DeckZone);
        string after = Show();
        bool afterMulligan = watch.All(u => E(u).Cost == Want(u));
        bool stats = E(b)[GameTag.ATK] == C(E(b).Card.Id).ATK + 4 && E(c3)[GameTag.ATK] == C(E(c3).Card.Id).ATK + 2;
        foreach (int u in watch) if (E(u).Zone == p1.DeckZone) Generic.Draw(p1, E(u));
        bool redrawn = watch.All(u => E(u).Zone == p1.HandZone && E(u).Cost == Want(u));
        string drawn = Show();
        var paid = new List<string>(); bool pays = true;
        foreach (int u in watch)
        {
            var e = E(u);
            p1.BaseMana = 10; p1.UsedMana = 0; p1.TemporaryMana = 0; int m0 = p1.RemainingMana;
            bool ok = g.Process(PlayCardTask.Any(p1, e));
            paid.Add(e.Card.Id + " " + (m0 - p1.RemainingMana) + "/" + Want(u));
            pays &= ok && m0 - p1.RemainingMana == Want(u);
        }
        Check("R4 discounted, Lucky+Gossip and Satiated+Stories cards (and a copy) cost their run cost at the start", atStart, start);
        Check("R4 after the mulligan sends the three flagged cards back to the deck: costs still equal the run's EffectiveCost, stat buffs kept",
            sentBack && afterMulligan && stats, after);
        Check("R4 redrawn: the same costs in hand", redrawn, drawn);
        Check("R4 played: the mana paid is the run cost", pays, string.Join(" ", paid));
    }
}

// ================================================================ T5 the regenerated restoration ledger
{
    string csv = VerifyPaths.Repo("docs/design/restoration-ledger.csv");
    var lines = File.ReadAllLines(csv);
    var header = lines[0].Split(',');
    List<string> Fields(string line)
    {
        var f = new List<string>(); var sb = new System.Text.StringBuilder(); bool q = false;
        for (int i = 0; i < line.Length; i++)
        {
            char ch = line[i];
            if (q) { if (ch == '"' && i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; } else if (ch == '"') q = false; else sb.Append(ch); }
            else if (ch == '"') q = true; else if (ch == ',') { f.Add(sb.ToString()); sb.Clear(); } else sb.Append(ch);
        }
        f.Add(sb.ToString()); return f;
    }
    var rows = lines.Skip(1).Where(l => l.Length > 0).Select(Fields).ToDictionary(f => f[0], f => f);
    int Col(string name) => Array.IndexOf(header, name);
    // Phase 6 F1's regeneration added 2 rows (4440 -> 4442); F2 adds none; F3 adds 5 (the hero rows run:class:GILA_500h3 and
    // run:class:GILA_600h, their starters run:starter:mh_tess_scavenger and run:starter:mh_darius_militia, and the borrowed
    // Battlegrounds enchantment shell TB_BaconShopBadsongE of GILA_608).
    Check("T5 the ledger keeps 4726 rows (phase 8 family 21: + 17 run:boss rows and + 5 cards of their decks; phase 8 wave 3: + 26 run:boss rows, 11 of family 09 and 15 of family 10, and + 34 cards of their decks; phase 8 wave 2: + 35 run:boss rows, 18 of family 07 and 17 of family 08, and + 30 cards of their decks; phase 8 family 01: + 12 encounter rows and + 12 for the four Secret bosses; phase 6 F6: + the 9 Heist hero rows, their 35 starters and CORE_WON_096; F7/F8 + 5; F9 + run:starter:tombs_reno_random; F10 + 17: the 12 new starters, the 3 Random decks of the enabled dual rows and 2 rows they bring; F14 + 36: Rikkar's 9 run:class rows and 27 shrine starters)", rows.Count == 4726, rows.Count.ToString());
    var tavernRows = table.Options.Where(o => o.Weight > 0 || o.AliasOf != null).Select(o => o.Id).ToList();
    var notImplemented = tavernRows.Where(id => !rows.ContainsKey(id) || rows[id][Col("classification")] != "implemented").ToList();
    Check("T5 every active tavern option row is implemented", notImplemented.Count == 0, string.Join(" ", notImplemented.Select(id => id + "=" + (rows.ContainsKey(id) ? rows[id][Col("classification")] + "/" + rows[id][Col("gaps")] : "missing"))));
    Check("T5 House Special stays missing with its deferred reason", rows["ULDA_607"][Col("classification")] == "missing" && rows["ULDA_607"][Col("notes")].Contains("Bombastic"));
    Check("T5 the tavern enchantments are noted as tavern deck enchantments", tavernEnchants.All(id => rows[id][Col("notes")].Contains("tavern deck enchantment")));
    // Phase 6 deferred GILA_911 and ULDA_111 to phase 9; DALA_721 landed with family F7 and ULDA_011 VIP Membership with family
    // F13 (a run-layer tavern flag): both rows are implemented, their deferral notes gone.
    Check("T5 DALA_721 (phase 6 F7) is implemented with no deferral note",
        rows["DALA_721"][Col("classification")] == "implemented" && !rows["DALA_721"][Col("notes")].Contains("deferred: phase"));
    Check("T5 ULDA_011 (phase 6 F13) is implemented with no deferral note; the turn-restart and match-exit rows say phase 9",
        rows["ULDA_011"][Col("classification")] == "implemented" && !rows["ULDA_011"][Col("notes")].Contains("deferred: phase")
        && new[] { "GILA_911", "ULDA_111" }.All(id => rows[id][Col("notes")].Contains("deferred: phase 9") && !rows[id][Col("notes")].Contains("deferred: phase 6")));
    var lift = rows.Values.Where(f => f[Col("ai_support")].Contains("own-deck ledger")).ToList();
    // Phase 7 D: TRLA_809 Windfall Blessing (deck-wide-change, the run player's) joined the BossDeckLedgerLift rows: ten.
    Check("T5 the ten BossDeckLedgerLift rows (TRLA_809 since phase 7 D) carry the phase-8 relabel", lift.Count == 10 && lift.Any(f => f[0] == "TRLA_809")
        && lift.All(f => f[Col("ai_support")].Contains("phase 8 (encounters/anomalies)")),
        lift.Count + " " + string.Join(" ", lift.Select(f => f[0])));
}

Console.WriteLine($"{passes} passed, {failures} failed");
return failures == 0 ? 0 : 1;
