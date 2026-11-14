// c-shrine-upgrades: phase 7 family P7-U, the Rumble Run shrine upgrades TRLA_801..808 (passive run rewards that modify the
// board shrine, never hero powers or passive treasures), their client player enchantments (TRLA_801e2/802e2/803e2, 805e,
// 806e/807e/808e) and the grants every run shrine carries (TRLA_801e/802e/803e, 806e2/807e2/808e2). Every game check plays a
// real engine game through the run setup path (NewRun -> BuildPlan -> DungeonRunSetup.Configure -> Game -> ApplyAfterStart ->
// mulligan -> MainBegin), where the run player's shrine lands at Start of Game and receives its grants. Boss-side moves are
// played by hand on the boss's turn; the boss AI is c-bosspilot-shrine-U.
//   A  data, admission, the shrine-upgrade pool and draft through the run flow, the plan, save/resume.
//   S  801 Sacred Ritual; F 802 Favor of the Gods; H 803 Fortified Spirit; W 806 Wrath of the Loas (against the TRLA_107
//      fixture); B 807 Boon Reaper; R 808 Roaring Edifice; D 804/805 with the grants; each with dormancy, revival, Silence,
//      copies, the transform/bounce guards, treasures (LOOTA_824/845/832, DALA_743) and the other shrines (113/114/147/155/106).
//   N  History=false games (the mode of the AI's simulations and of c-bosspilot): the grants are entities there too.
// Headless: compiles C:\SoloStone\plugin\DungeonRun\*.cs and the NPC AI against audit-verify/engine-out. Run it twice and compare.
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

int failures = 0, passes = 0;
void Check(string name, bool ok, string detail = "")
{
    if (ok) passes++; else failures++;
    Console.WriteLine((ok ? "PASS " : "FAIL ") + name + (detail.Length > 0 ? "  (" + detail + ")" : ""));
}
void Guard(string name, Action body)
{
    try { body(); }
    catch (Exception e) { failures++; Console.WriteLine("FAIL " + name + " threw " + e.GetType().Name + ": " + e.Message.Split('\n')[0]); }
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
var data = DungeonRunData.Parse(R("dungeon-run/run-v1.json"), R("dungeon-run/pool-v1.json"), R("npc/decks-v1.json"),
    R("dungeon-run/catalog-v1.json"), R("dungeon-run/schedule-v1.json"), R("dungeon-run/tavern-v1.json"));
var run = new DungeonRun(data);
var all = new DungeonRunProfile(); all.Unlocked.AddRange(data.Unlocks.Keys);
Card C(string id) => Cards.FromId(id);
string[] Eight = { "TRLA_801", "TRLA_802", "TRLA_803", "TRLA_804", "TRLA_805", "TRLA_806", "TRLA_807", "TRLA_808" };
// Phase 7 D scripted TRLA_809 Windfall Blessing: the pools and offers now hold all nine (c-companions-D tests TRLA_809 itself).
string[] Nine = Eight.Append("TRLA_809").ToArray();
const string Sacred = "TRLA_801", Favor = "TRLA_802", Fortified = "TRLA_803", Devotion = "TRLA_804", Pact = "TRLA_805",
    Wrath = "TRLA_806", Reaper = "TRLA_807", Edifice = "TRLA_808";
const string Armament = "rumble_druid_112"; // Gonk's Armament (5 Health): its trigger (Armor -> refresh Mana) stays out of the way

string RowOf(string deck)
{
    string cls = deck.Split('_')[1];
    return "TRLA_209h_" + char.ToUpperInvariant(cls[0]) + cls.Substring(1);
}
DungeonRunState NewRun(string row, string deck, long seed)
{
    var cls = data.Class(row);
    return run.NewRun(all, cls.Id, deck ?? cls.DefaultDeck, cls.DefaultPower, seed,
        signatureId: run.SignatureOffer(all, data.FindClass(cls.Id), seed).FirstOrDefault());
}
Game Start(RunMatchPlan plan, int seed, bool history)
{
    var cfg = new GameConfig { RandomSeed = seed, Shuffle = true, SkipMulligan = false, History = history, Logging = false, FormatType = FormatType.FT_WILD };
    DungeonRunSetup.Configure(cfg, plan);
    var g = new Game(cfg);
    g.StartGame();
    DungeonRunSetup.ApplyAfterStart(g, plan);
    return g;
}
(DungeonRunState s, RunMatchPlan plan, Game g) F(string deck, long seed, string[] upgrades = null, string[] treasures = null, bool history = true)
{
    var s = NewRun(RowOf(deck), deck, seed);
    foreach (string u in upgrades ?? Array.Empty<string>()) s.ShrineUpgrades.Add(u);
    foreach (string t in treasures ?? Array.Empty<string>()) s.Treasures.Add(t);
    var plan = run.BuildPlan(s);
    var g = Start(plan, run.MatchSeed(s), history);
    g.Process(ChooseTask.Mulligan(g.Player1, new List<int>()));
    g.Process(ChooseTask.Mulligan(g.Player2, new List<int>()));
    g.MainBegin();
    if (g.CurrentPlayer != g.Player1) throw new Exception("run player not first");
    return (s, plan, g);
}
void Mana(Controller c, int n) { c.BaseMana = n; c.UsedMana = 0; c.OverloadLocked = 0; c.OverloadOwed = 0; }
IPlayable ToHand(Game g, Controller c, string id)
{
    while (c.HandZone.Count >= 9) { var old = c.HandZone[0]; c.HandZone.Remove(old); c.SetasideZone.Add(old); }
    var e = Entity.FromCard(c, C(id)); Generic.AddHandPhase.Invoke(c, e); g.DeathProcessingAndAuraUpdate(); return e;
}
void ClearHand(Game g, Controller c) { foreach (var p in c.HandZone.ToArray()) { c.HandZone.Remove(p); c.SetasideZone.Add(p); } g.DeathProcessingAndAuraUpdate(); }
// Tops a starter deck (10 cards at floor 1) up with Wisps, so "draw until the hand is full" has cards to draw.
void FillDeck(Controller c, int n) { for (int i = 0; i < n; i++) Entity.FromCard(c, C("CS2_231"), zone: c.DeckZone); }
void ClearDeck(Game g, Controller c) { foreach (var p in c.DeckZone.ToArray()) { c.DeckZone.Remove(p); c.SetasideZone.Add(p); } }
Minion Board(Game g, Controller c, string id, int pos = -1) { var m = (Minion)Entity.FromCard(c, C(id)); Generic.SummonBlock(g, m, pos, c.Hero); g.DeathProcessingAndAuraUpdate(); return m; }
bool Play(Game g, IPlayable card, ICharacter target = null) => g.Process(PlayCardTask.Any(g.CurrentPlayer, card, target, -1, 0));
void EndTurn(Game g) => g.Process(EndTurnTask.Any(g.CurrentPlayer));
void ClearExceptShrines(Game g)
{
    foreach (var c in new[] { g.Player1, g.Player2 })
        foreach (var m in c.BoardZone.Slots.OfType<Minion>().Where(m => !ShrineRules.IsMarked(m)).ToArray()) { c.BoardZone.Remove(m); c.SetasideZone.Add(m); }
    g.DeathProcessingAndAuraUpdate();
}
Minion ShrineOf(Game g, Controller c) => ShrineRules.Of(c).FirstOrDefault();
void Kill(Game g, params Minion[] ms) { foreach (var m in ms) m.Destroy(); g.DeathProcessingAndAuraUpdate(); }
void Damage(Game g, Minion m, int n) { Generic.DamageCharFunc(m.Controller.Opponent.Hero, m, n, false); g.DeathProcessingAndAuraUpdate(); }
void ToTurn(Game g, int turn) { while (g.Turn < turn && g.State != State.COMPLETE) EndTurn(g); }
// The shrine-upgrade grants an entity carries (client card ids, attach order).
List<string> GrantsOn(IPlayable p) => p?.AppliedEnchantments?.Where(e => DungeonRunShrines.IsGrant(e.Card.Id)).Select(e => e.Card.Id).ToList() ?? new List<string>();
bool HasPlayerEnchant(Controller c, string id) => c.AppliedEnchantments?.Any(e => e.Card.Id == id) == true;
string BoardIds(Controller c) => string.Join(",", c.BoardZone.Slots.Select(m => m.Card.Id + (ShrineRules.IsMarked(m) ? "*" : "") + (m is Minion x && x.HasTaunt ? "T" : "")));
string Serialize(DungeonRunSave save)
{
    var ser = new System.Xml.Serialization.XmlSerializer(typeof(DungeonRunSave));
    using var w = new StringWriter();
    ser.Serialize(w, save);
    return w.ToString();
}
DungeonRunState Walk(DungeonRunState s, Func<DungeonRunState, bool> stop, List<string> log, Func<DungeonRunState, string> pickUpgrade = null)
{
    var scratch = new DungeonRunProfile();
    for (int guard = 0; guard < 500 && !stop(s) && !s.Finished; guard++)
    {
        switch (s.Phase)
        {
            case DungeonRunPhase.PreMatch: run.BeginMatch(s); run.RecordOutcome(s, scratch, DungeonRunOutcome.Win); break;
            case DungeonRunPhase.TreasureDraft:
                log?.Add($"f{s.Floor}:{s.OpenDraft.Kind}[{string.Join(",", s.OpenDraft.Offered)}]");
                run.PickTreasure(s, s.OpenDraft.Kind == DungeonRun.ShrineUpgradeKind && pickUpgrade != null ? pickUpgrade(s) : s.OpenDraft.Offered[0]); break;
            case DungeonRunPhase.Modifier: run.PickModifier(s, s.ModifierOffer.Offered[0]); break;
            case DungeonRunPhase.Tavern: run.LeaveTavern(s); break;
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
    return s;
}

// ================================================================ A data, admission, pool, draft, plan
Guard("A", () =>
{
    var expected = new Dictionary<string, (string Player, string Grant)>
    {
        ["TRLA_801"] = ("TRLA_801e2", "TRLA_801e"), ["TRLA_802"] = ("TRLA_802e2", "TRLA_802e"), ["TRLA_803"] = ("TRLA_803e2", "TRLA_803e"),
        ["TRLA_804"] = (null, null), ["TRLA_805"] = ("TRLA_805e", null),
        ["TRLA_806"] = ("TRLA_806e", "TRLA_806e2"), ["TRLA_807"] = ("TRLA_807e", "TRLA_807e2"), ["TRLA_808"] = ("TRLA_808e", "TRLA_808e2"),
    };
    Check("A1 the eight upgrades are scripted with their client player enchantment and grant",
        expected.All(p => DungeonRunShrines.ScriptedUpgrades.TryGetValue(p.Key, out var pe) && pe == p.Value.Player
            && (p.Value.Grant == null || DungeonRunShrines.Grants.TryGetValue(p.Value.Player, out var gr) && gr == p.Value.Grant))
        && DungeonRunShrines.Grants.Count == 7 && DungeonRunShrines.ScriptedUpgrades.TryGetValue("TRLA_809", out var windfall) && windfall == "TRLA_809e"
        && DungeonRunShrines.Grants["TRLA_809e"] == "TRLA_809e2");
    var ids = new[] { "TRLA_801", "TRLA_801e", "TRLA_801e2", "TRLA_802", "TRLA_802e", "TRLA_802e2", "TRLA_803", "TRLA_803e", "TRLA_803e2", "TRLA_804",
        "TRLA_805", "TRLA_805e", "TRLA_806", "TRLA_806e", "TRLA_806e2", "TRLA_807", "TRLA_807e", "TRLA_807e2", "TRLA_808", "TRLA_808e", "TRLA_808e2" };
    Check("A2 all 21 ids of the family are engine scripts (Implemented; every enchantment with its Power; the passives are run rewards with no in-game power)",
        ids.All(id => C(id) != null && C(id).Implemented && (Eight.Contains(id) || C(id).Power != null)),
        string.Join(",", ids.Where(id => C(id) == null || !C(id).Implemented || !Eight.Contains(id) && C(id).Power == null)));
    Check("A3 the upgrades are Passive spells of the client data (DUNGEON_PASSIVE_BUFF 783, CARDTYPE spell), never hero powers",
        Eight.All(id => C(id)[(GameTag)783] == 1 && C(id).Type == CardType.SPELL));
    AdmissionVerdict V(string id, AdmitScope scope) => DungeonRunAdmission.Classify(null, id, scope, null, true);
    Check("A4 admission: the eight are the run player's only (never the boss's, never generated), family shrine-upgrade",
        Eight.All(id => V(id, AdmitScope.Player).Admitted && !V(id, AdmitScope.Boss).Admitted && !V(id, AdmitScope.Generated).Admitted
            && DungeonRunAdmission.RuleFor(id).Family == "shrine-upgrade"),
        string.Join(",", Eight.Where(id => !V(id, AdmitScope.Player).Admitted)));
    Check("A5 TRLA_809 Windfall Blessing is admitted for the run player since phase 7 D (deck-wide-change; never the boss's, never generated)",
        V("TRLA_809", AdmitScope.Player).Admitted && !V("TRLA_809", AdmitScope.Boss).Admitted && !V("TRLA_809", AdmitScope.Generated).Admitted
        && DungeonRunAdmission.RuleFor("TRLA_809").Family == "deck-wide-change");
    var gen = new HashSet<string>(DungeonRunAdmission.GeneratedRejects(true));
    Check("A6 generation never yields an upgrade", Eight.All(gen.Contains));

    var s = NewRun("TRLA_209h_Druid", Armament, 11);
    Check("A7 a shrine run's shrine-upgrade pool is the nine (TRLA_809 since phase 7 D), ordinal", run.ShrineUpgradePool(s).SequenceEqual(Nine), string.Join(",", run.ShrineUpgradePool(s)));
    s.ShrineUpgrades.Add(Wrath);
    Check("A7 an upgrade held leaves the pool", run.ShrineUpgradePool(s).SequenceEqual(Nine.Where(i => i != Wrath)));
    var cls = NewRun("DRUID", null, 12);
    Check("A8 a run with no board shrine has no pool (the upgrades are offered only with a shrine)", cls.BoardShrine == null && run.ShrineUpgradePool(cls).Count == 0);
    Check("A9 no treasure pool lists an upgrade", !run.TreasurePool(s).Any(t => t.StartsWith("TRLA_80", StringComparison.Ordinal))
        && !data.Treasures.Any(t => t.Id.StartsWith("TRLA_80", StringComparison.Ordinal)));
    var plan = run.BuildPlan(s);
    Check("A10 the plan carries the upgrade beside the shrine, not among the passive treasures",
        plan.PlayerShrineUpgrades.SequenceEqual(new[] { Wrath }) && !plan.PlayerTreasures.Contains(Wrath) && plan.PlayerShrine == "TRLA_112");
    string curation = File.ReadAllText(VerifyPaths.Repo("tools/restoration-ledger/curation.json"));
    Check("A11 the reward-schedule decision is recorded in curation.json (shrineUpgradeSchedule, acts-v2 nodes, not treasureAfter/passiveAtStart)",
        curation.Contains("\"shrineUpgradeSchedule\"") && curation.Contains("\"shrine-upgrade-draft\"") && curation.Contains("passiveAtStart"));
});
Guard("A12", () =>
{
    // The draft through the run flow: after the act I major's treasure draft; 3 of the pool, from the run seed; resume keeps it.
    var s = NewRun("TRLA_209h_Druid", Armament, 21);
    var log = new List<string>();
    Walk(s, x => x.Phase == DungeonRunPhase.TreasureDraft && x.OpenDraft.Kind == DungeonRun.ShrineUpgradeKind, log);
    var offer = s.OpenDraft?.Offered ?? new List<string>();
    Check("A12 the act I shrine-upgrade draft offers 3 distinct scripted upgrades, ordinal", s.Floor == 5 && offer.Count == 3 && offer.Distinct().Count() == 3
        && offer.All(Nine.Contains) && offer.SequenceEqual(offer.OrderBy(x => x, StringComparer.Ordinal)), $"f{s.Floor} [{string.Join(",", offer)}]");
    var twin = NewRun("TRLA_209h_Druid", Armament, 21);
    Walk(twin, x => x.Phase == DungeonRunPhase.TreasureDraft && x.OpenDraft.Kind == DungeonRun.ShrineUpgradeKind, null);
    Check("A12 the offer is drawn from the run seed (the same run seed, the same offer)", twin.OpenDraft.Offered.SequenceEqual(offer));
    var seen = new SortedSet<string>(StringComparer.Ordinal);
    for (long seed = 100; seed < 130; seed++)
    {
        var x = NewRun("TRLA_209h_Druid", Armament, seed);
        Walk(x, y => y.Phase == DungeonRunPhase.TreasureDraft && y.OpenDraft.Kind == DungeonRun.ShrineUpgradeKind, null);
        foreach (string id in x.OpenDraft.Offered) seen.Add(id);
    }
    Check("A12 over 30 runs every one of the nine is offered (TRLA_809 since phase 7 D)", seen.SetEquals(Nine), string.Join(",", seen));
    var ser = new System.Xml.Serialization.XmlSerializer(typeof(DungeonRunSave));
    var back = ((DungeonRunSave)ser.Deserialize(new StringReader(Serialize(new DungeonRunSave { Active = s })))).Active;
    run.Resume(back);
    Check("A13 save and resume keep the open shrine-upgrade draft and its offer", back.OpenDraft?.Kind == DungeonRun.ShrineUpgradeKind && back.OpenDraft.Offered.SequenceEqual(offer));
    string pick = offer[offer.Count - 1];
    int treasures = back.Treasures.Count;
    run.PickTreasure(back, pick);
    Check("A14 the pick joins ShrineUpgrades (not Treasures), then the bundle draft", back.ShrineUpgrades.SequenceEqual(new[] { pick }) && back.Treasures.Count == treasures
        && back.Phase == DungeonRunPhase.BundleDraft);
    var log2 = new List<string>();
    Walk(back, x => x.Phase == DungeonRunPhase.TreasureDraft && x.OpenDraft.Kind == DungeonRun.ShrineUpgradeKind || x.Floor > 10, log2);
    var offer2 = back.OpenDraft?.Offered ?? new List<string>();
    Check("A15 the act II draft offers 3 more, never the one held", back.Floor == 10 && offer2.Count == 3 && !offer2.Contains(pick) && offer2.All(Eight.Contains),
        $"f{back.Floor} [{string.Join(",", offer2)}]");
    string pick2 = offer2[0];
    run.PickTreasure(back, pick2);
    var walked = Walk(back, x => x.Phase == DungeonRunPhase.PreMatch && x.Floor == 11, null);
    var plan = run.BuildPlan(walked);
    Check("A16 a later fight's plan carries both picks in order", plan.PlayerShrineUpgrades.SequenceEqual(new[] { pick, pick2 })
        && plan.PlayerShrineCopies == (plan.PlayerShrineUpgrades.Contains(Devotion) ? 2 : 1));
    var g = Start(plan, run.MatchSeed(walked), true);
    g.Process(ChooseTask.Mulligan(g.Player1, new List<int>())); g.Process(ChooseTask.Mulligan(g.Player2, new List<int>()));
    g.MainBegin();
    var wantGrants = plan.PlayerShrineUpgrades.Select(u => DungeonRunShrines.ScriptedUpgrades[u]).Where(pe => pe != null && DungeonRunShrines.Grants.ContainsKey(pe))
        .Select(pe => DungeonRunShrines.Grants[pe]).ToList();
    Check("A16 that fight: the player enchantments attached, each shrine carries the grants in pick order",
        plan.PlayerShrineUpgrades.Select(u => DungeonRunShrines.ScriptedUpgrades[u]).Where(pe => pe != null).All(pe => HasPlayerEnchant(g.Player1, pe))
        && ShrineRules.Of(g.Player1).Count == plan.PlayerShrineCopies && ShrineRules.Of(g.Player1).All(m => GrantsOn(m).SequenceEqual(wantGrants)),
        $"[{string.Join(",", plan.PlayerShrineUpgrades)}] grants [{string.Join(";", ShrineRules.Of(g.Player1).Select(m => string.Join(",", GrantsOn(m))))}]");
    Check("A17 the boss holds no upgrade and its shrines would get no grant", !g.Player2.AppliedEnchantments?.Any(e => DungeonRunShrines.Grants.ContainsKey(e.Card.Id)) ?? true);
});

// ================================================================ S TRLA_801 Sacred Ritual
Guard("S1", () =>
{
    var (s, plan, g) = F(Armament, 101, upgrades: new[] { Sacred });
    var shrine = ShrineOf(g, g.Player1);
    Check("S1 the player enchantment TRLA_801e2 is attached; the shrine carries TRLA_801e Sacred", HasPlayerEnchant(g.Player1, "TRLA_801e2")
        && GrantsOn(shrine).SequenceEqual(new[] { "TRLA_801e" }), string.Join(",", GrantsOn(shrine)));
    var fireball = ToHand(g, g.Player1, "CS2_029"); var moonfire = ToHand(g, g.Player1, "CS2_008"); var yeti = ToHand(g, g.Player1, "CS2_182");
    var bossFireball = ToHand(g, g.Player2, "CS2_029");
    Check("S1 the owner's spells cost (1) less (Fireball 3, Moonfire stays 0), minions do not, the boss's spells do not",
        fireball.Cost == 3 && moonfire.Cost == 0 && yeti.Cost == 4 && bossFireball.Cost == 4, $"{fireball.Cost} {moonfire.Cost} {yeti.Cost} boss {bossFireball.Cost}");
    var drawnSpell = ToHand(g, g.Player1, "CS2_032");
    Check("S1 a spell that enters the hand later is discounted too (Flamestrike 7 -> 6)", drawnSpell.Cost == 6, drawnSpell.Cost.ToString());
    Mana(g.Player1, 3);
    int bossBefore = g.Player2.Hero.Health;
    bool played = Play(g, fireball, g.Player2.Hero);
    Check("S1 the discount pays: Fireball for 3 Mana", played && g.Player1.RemainingMana == 0 && g.Player2.Hero.Health == bossBefore - 6,
        $"played {played} mana {g.Player1.RemainingMana}");
    Kill(g, shrine);
    var spell = ToHand(g, g.Player1, "CS2_029");
    Check("S2 a dormant shrine has no grant: spells cost their own Cost (Fireball 4)", ShrineOf(g, g.Player1).IsDormant && spell.Cost == 4
        && GrantsOn(ShrineOf(g, g.Player1)).Count == 0, $"{spell.Cost}");
    ToTurn(g, 7);
    var back = ShrineOf(g, g.Player1);
    Check("S2 revived at the start of turn 7 with Sacred again: Fireball 3", back.Card.Id == "TRLA_112" && !back.IsDormant && GrantsOn(back).SequenceEqual(new[] { "TRLA_801e" })
        && spell.Cost == 3, $"{back.Card.Id} {spell.Cost}");
});
Guard("S3", () =>
{
    var (s, plan, g) = F(Armament, 102, upgrades: new[] { Sacred });
    var fireball = ToHand(g, g.Player1, "CS2_029");
    EndTurn(g); Mana(g.Player2, 10);
    var shrine = ShrineOf(g, g.Player1);
    Play(g, ToHand(g, g.Player2, "EX1_332"), shrine); // Silence
    Check("S3 Silence removes the grant (Fireball back to 4); the shrine stays an awake run shrine", shrine.IsSilenced && ShrineRules.IsAwake(shrine)
        && GrantsOn(shrine).Count == 0 && fireball.Cost == 4, $"{fireball.Cost}");
    Kill(g, shrine);
    ToTurn(g, 9);
    Check("S3 it revives un-silenced with the grant (Fireball 3)", ShrineOf(g, g.Player1).Card.Id == "TRLA_112" && fireball.Cost == 3, fireball.Cost.ToString());
});
Guard("S4", () =>
{
    var (s, plan, g) = F(Armament, 103, upgrades: new[] { Devotion, Sacred });
    var fireball = ToHand(g, g.Player1, "CS2_029");
    Check("S4 TRLA_804: two shrines, each with Sacred: spells cost (2) less (Fireball 2)", ShrineRules.Of(g.Player1).Count == 2
        && ShrineRules.Of(g.Player1).All(m => GrantsOn(m).SequenceEqual(new[] { "TRLA_801e" })) && fireball.Cost == 2, fireball.Cost.ToString());
    Kill(g, ShrineRules.Of(g.Player1)[1]);
    Check("S4 one dormant: (1) less (Fireball 3)", fireball.Cost == 3, fireball.Cost.ToString());
});
Guard("S5", () =>
{
    var (s, plan, g) = F(Armament, 104, upgrades: new[] { Sacred });
    Mana(g.Player1, 10); ClearExceptShrines(g);
    var fireball = ToHand(g, g.Player1, "CS2_029");
    var shrine = ShrineOf(g, g.Player1);
    Play(g, ToHand(g, g.Player1, "EX1_564"), shrine); // Faceless Manipulator on the own shrine
    var copy = g.Player1.BoardZone.OfType<Minion>().FirstOrDefault(m => m.Card.Id == "TRLA_112" && !ShrineRules.IsMarked(m));
    Check("S5 a Faceless copy of the shrine is a plain minion without the grant: still (1) less, not (2)", copy != null && GrantsOn(copy).Count == 0 && fireball.Cost == 3,
        $"copy {copy?.Card.Id} grants [{string.Join(",", GrantsOn(copy))}] fireball {fireball.Cost}");
});
Guard("S6", () =>
{
    var (s, plan, g) = F(Armament, 105, upgrades: new[] { Sacred }, treasures: new[] { "LOOTA_824" }); // Khadgar's Scrying Orb
    var fireball = ToHand(g, g.Player1, "CS2_029"); var arcane = ToHand(g, g.Player1, "CS2_025");
    Check("S6 with Khadgar's Scrying Orb (spells cost (1) less): Fireball 2, Arcane Explosion floors at 0", fireball.Cost == 2 && arcane.Cost == 0, $"{fireball.Cost} {arcane.Cost}");
    var (s2, p2, g2) = F("rumble_shaman_155", 106, upgrades: new[] { Sacred });
    var fb2 = ToHand(g2, g2.Player1, "CS2_029"); var fs = ToHand(g2, g2.Player1, "CS2_032");
    Check("S6 with Krag'wa's Grace (its spells (2) less) and Sacred on it: Fireball 1, Flamestrike 4", fb2.Cost == 1 && fs.Cost == 4
        && GrantsOn(ShrineOf(g2, g2.Player1)).SequenceEqual(new[] { "TRLA_801e" }), $"{fb2.Cost} {fs.Cost}");
    var (s3, p3, g3) = F("rumble_warlock_113", 107, upgrades: new[] { Sacred });
    Mana(g3.Player1, 0);
    var fb3 = ToHand(g3, g3.Player1, "CS2_029");
    int health = g3.Player1.Hero.Health, cost3 = fb3.Cost;
    bool cast = Play(g3, fb3, g3.Player2.Hero);
    Check("S6 with Blood Pact (spells cost Health): Fireball costs 3, paid as 3 Health with no Mana", cast && cost3 == 3 && g3.Player1.Hero.Health == health - 3,
        $"cast {cast} cost {cost3} {health}->{g3.Player1.Hero.Health}");
});

// ================================================================ F TRLA_802 Favor of the Gods
Guard("F1", () =>
{
    var (s, plan, g) = F(Armament, 201, upgrades: new[] { Favor });
    Mana(g.Player1, 10); ClearExceptShrines(g);
    var shrine = ShrineOf(g, g.Player1);
    var a = Board(g, g.Player1, "CS2_182"); var b = Board(g, g.Player1, "CS2_120");
    Check("F1 the shrine carries Favored (TRLA_802e) and the player TRLA_802e2", HasPlayerEnchant(g.Player1, "TRLA_802e2") && GrantsOn(shrine).SequenceEqual(new[] { "TRLA_802e" }));
    Check("F1 the minion next to the shrine has Taunt; the one beyond does not; the shrine itself does not", a.HasTaunt && !b.HasTaunt && !shrine.HasTaunt, BoardIds(g.Player1));
    var left = Board(g, g.Player1, "CS2_172", 0);
    Check("F1 a minion summoned on the shrine's other side gains Taunt too; the old neighbour keeps it", left.HasTaunt && a.HasTaunt && !b.HasTaunt, BoardIds(g.Player1));
    EndTurn(g); Mana(g.Player2, 10);
    var raider = Board(g, g.Player2, "CS2_124"); raider.IsExhausted = false; // Wolfrider (Charge)
    var targets = g.Player2.Options().OfType<MinionAttackTask>().Where(o => o.Source == raider).Select(o => o.Target).ToList();
    Check("F1 the boss's attacker must attack a Taunt neighbour (not the hero, not the shrine)", targets.Count == 2 && targets.Contains(a) && targets.Contains(left),
        string.Join(",", targets.Select(t => t.Card.Id)));
    Play(g, ToHand(g, g.Player2, "EX1_332"), a); // Silence the neighbour
    Check("F1 a silenced neighbour keeps the aura's Taunt (Silence does not remove an aura)", a.IsSilenced && a.HasTaunt);
    Kill(g, shrine);
    Check("F2 the shrine goes dormant: no neighbour has Taunt; the dormant form has none", !a.HasTaunt && !left.HasTaunt && !ShrineOf(g, g.Player1).HasTaunt
        && ShrineOf(g, g.Player1).IsDormant, BoardIds(g.Player1));
    ToTurn(g, 9);
    Check("F2 revived: the neighbours have Taunt again", ShrineOf(g, g.Player1).Card.Id == "TRLA_112" && a.HasTaunt && left.HasTaunt && !b.HasTaunt, BoardIds(g.Player1));
    Kill(g, a);
    Check("F2 when a neighbour dies the board closes up: the minion beyond it, now adjacent, gains Taunt", b.HasTaunt && left.HasTaunt, BoardIds(g.Player1));
});
Guard("F3", () =>
{
    var (s, plan, g) = F(Armament, 202, upgrades: new[] { Favor });
    Mana(g.Player1, 10); ClearExceptShrines(g);
    var a = Board(g, g.Player1, "CS2_182");
    EndTurn(g); Mana(g.Player2, 10);
    var shrine = ShrineOf(g, g.Player1);
    Play(g, ToHand(g, g.Player2, "EX1_332"), shrine); // Silence the shrine
    Check("F3 Silence on the shrine takes the Taunt off its neighbour", shrine.IsSilenced && !a.HasTaunt && GrantsOn(shrine).Count == 0, BoardIds(g.Player1));
    var (s2, p2, g2) = F(Armament, 203, upgrades: new[] { Devotion, Favor });
    Mana(g2.Player1, 10); ClearExceptShrines(g2);
    var c = Board(g2, g2.Player1, "CS2_182");
    var two = ShrineRules.Of(g2.Player1);
    Check("F3 TRLA_804: two shrines side by side taunt each other and the minion after them", two.Count == 2 && two.All(m => m.HasTaunt) && c.HasTaunt, BoardIds(g2.Player1));
    Kill(g2, two[0]);
    Check("F3 one dormant: the awake one still taunts the minion; the dormant form has no Taunt; the awake one is not next to an awake shrine",
        c.HasTaunt && !ShrineRules.Of(g2.Player1)[0].HasTaunt && !ShrineRules.Of(g2.Player1)[1].HasTaunt, BoardIds(g2.Player1));
});
Guard("F4", () =>
{
    // Start of Game order: the shrine lands first, then Dr. Boom's Remote's Boom Bots; the bot next to the shrine has Taunt.
    var (s, plan, g) = F(Armament, 204, upgrades: new[] { Favor }, treasures: new[] { "DALA_743" });
    var slots = g.Player1.BoardZone.Slots.OfType<Minion>().ToList();
    Check("F4 Start of Game: shrine, then three Boom Bots; only the first bot (adjacent) has Taunt",
        slots.Count == 4 && ShrineRules.IsMarked(slots[0]) && slots.Skip(1).All(m => m.Card.Id == "GVG_110t") && slots[1].HasTaunt && !slots[2].HasTaunt && !slots[3].HasTaunt,
        BoardIds(g.Player1));
});

// ================================================================ H TRLA_803 Fortified Spirit
Guard("H1", () =>
{
    var (s, plan, g) = F(Armament, 301, upgrades: new[] { Fortified });
    Mana(g.Player1, 10); ClearExceptShrines(g);
    var shrine = ShrineOf(g, g.Player1);
    Check("H1 the shrine carries Fortified (TRLA_803e)", HasPlayerEnchant(g.Player1, "TRLA_803e2") && GrantsOn(shrine).SequenceEqual(new[] { "TRLA_803e" }));
    var cleric = Board(g, g.Player1, "CS2_235"); // Northshire Cleric: whenever a minion is healed, draw a card
    int hand = g.Player1.HandZone.Count;
    EndTurn(g);
    Check("H1 an undamaged shrine is not healed (no Cleric draw at the boss's turn start)", g.Player1.HandZone.Count == hand, $"{hand}->{g.Player1.HandZone.Count}");
    Damage(g, shrine, 3);
    Check("H1 damaged to 2", shrine.Health == 2);
    EndTurn(g);
    Check("H1 at the start of the owner's turn it is restored to full (5), an ordinary heal (Cleric draws: 1 + the turn's draw)",
        shrine.Health == 5 && g.Player1.HandZone.Count == hand + 2, $"{shrine.Health} hand {hand}->{g.Player1.HandZone.Count}");
    Damage(g, shrine, 4);
    EndTurn(g);
    Check("H1 at the start of the boss's turn too (each turn)", shrine.Health == 5, shrine.Health.ToString());
});
Guard("H2", () =>
{
    var (s, plan, g) = F(Armament, 302, upgrades: new[] { Fortified });
    var shrine = ShrineOf(g, g.Player1);
    Kill(g, shrine);
    EndTurn(g);
    Check("H2 the dormant form is untouched by the heal (asleep, untouchable)", ShrineOf(g, g.Player1).IsDormant);
    ToTurn(g, 7);
    var back = ShrineOf(g, g.Player1);
    Damage(g, back, 4);
    EndTurn(g);
    Check("H2 the revived shrine carries Fortified again and is restored next turn", GrantsOn(back).SequenceEqual(new[] { "TRLA_803e" }) && back.Health == 5,
        $"{back.Card.Id} {back.Health}");
    // Now the boss's turn: it silences the shrine, then no more heal.
    Mana(g.Player2, 10);
    Play(g, ToHand(g, g.Player2, "EX1_332"), back);
    Damage(g, back, 2);
    EndTurn(g);
    Check("H2 silenced: no heal", back.IsSilenced && back.Health == 3, back.Health.ToString());
});
Guard("H3", () =>
{
    var (s, plan, g) = F(Armament, 303, upgrades: new[] { Devotion, Fortified });
    var two = ShrineRules.Of(g.Player1);
    Damage(g, two[0], 2); Damage(g, two[1], 4);
    EndTurn(g);
    Check("H3 TRLA_804: both shrines restored", two.All(m => m.Health == 5), string.Join(",", two.Select(m => m.Health)));
    var (s2, p2, g2) = F("rumble_priest_147", 304, upgrades: new[] { Fortified });
    var cov = ShrineOf(g2, g2.Player1);
    int full = cov.Health;
    Damage(g2, cov, 3);
    EndTurn(g2);
    Check("H3 with Bwonsamdi's Covenant (its heal inversion is enemy-only): the own shrine is healed to full", cov.Health == full && full == 8, $"{cov.Health}/{full}");
    var (s3, p3, g3) = F(Armament, 305, upgrades: new[] { Pact, Fortified });
    Kill(g3, ShrineOf(g3, g3.Player1));
    ToTurn(g3, 5);
    var back = ShrineOf(g3, g3.Player1);
    Damage(g3, back, 3);
    EndTurn(g3);
    Check("H3 with TRLA_805: revived at turn 5 with Fortified, healed next turn", back.Card.Id == "TRLA_112" && back.Health == 5, $"{back.Card.Id} {back.Health}");
});

// ================================================================ W TRLA_806 Wrath of the Loas (the TRLA_107 enemy-shrine fixture)
Guard("W1", () =>
{
    var (s, plan, g) = F(Armament, 401, upgrades: new[] { Wrath });
    ClearExceptShrines(g);
    var fixture = ShrineRules.Place(g.Player2, DungeonRunShrines.FixtureShrine); g.DeathProcessingAndAuraUpdate();
    var yeti = Board(g, g.Player2, "CS2_182");
    var shrine = ShrineOf(g, g.Player1);
    Check("W1 the shrine carries the granted Deathrattle TRLA_806e2 (it has Deathrattle)", GrantsOn(shrine).SequenceEqual(new[] { "TRLA_806e2" }) && shrine.HasDeathrattle
        && HasPlayerEnchant(g.Player1, "TRLA_806e"));
    int bossHealth = g.Player2.Hero.Health;
    Kill(g, shrine);
    var enemyForm = ShrineOf(g, g.Player2);
    Check("W1 the shrine dies: Deathrattle: the boss's Tribal Shrine is destroyed and goes dormant; its other minion and hero are untouched",
        enemyForm?.Card.Id == "TRLA_107t" && enemyForm.IsDormant && !yeti.IsDead && yeti.Zone == g.Player2.BoardZone && g.Player2.Hero.Health == bossHealth
        && ShrineOf(g, g.Player1).Card.Id == "TRLA_112t", $"p2 {BoardIds(g.Player2)} p1 {BoardIds(g.Player1)}");
    Check("W1 the boss's shrine death counts as a death (its corpse is in the graveyard, out of resurrect pools)",
        g.Player2.GraveyardZone.Any(p => p.Card.Id == "TRLA_107" && ShrineRules.IsCorpse(p)));
});
Guard("W2", () =>
{
    var (s, plan, g) = F(Armament, 402, upgrades: new[] { Wrath });
    ClearExceptShrines(g);
    var fixture = ShrineRules.Place(g.Player2, DungeonRunShrines.FixtureShrine); g.DeathProcessingAndAuraUpdate();
    Kill(g, fixture);
    var form = ShrineOf(g, g.Player2);
    int turns = ShrineRules.TurnsLeft(form);
    Kill(g, ShrineOf(g, g.Player1));
    Check("W2 an enemy shrine already dormant is not a target (its timer unchanged)", ShrineOf(g, g.Player2) == form && form.IsDormant && ShrineRules.TurnsLeft(form) == turns,
        $"{turns} -> {ShrineRules.TurnsLeft(form)}");
    var (s2, p2, g2) = F(Armament, 403, upgrades: new[] { Wrath });
    ClearExceptShrines(g2);
    var fx2 = ShrineRules.Place(g2.Player2, DungeonRunShrines.FixtureShrine); g2.DeathProcessingAndAuraUpdate();
    EndTurn(g2); Mana(g2.Player2, 10);
    var mine = ShrineOf(g2, g2.Player1);
    Play(g2, ToHand(g2, g2.Player2, "EX1_564"), fx2); // the boss copies its own Tribal Shrine: a plain Shrine card
    var copy = g2.Player2.BoardZone.OfType<Minion>().FirstOrDefault(m => m.Card.Id == "TRLA_107" && !ShrineRules.IsMarked(m));
    Play(g2, ToHand(g2, g2.Player2, "EX1_246"), mine); // Hex on the run shrine: destroyed instead (dormant), its Deathrattle fires
    Check("W3 Hex on the run shrine: it goes dormant and Wrath fires: the fixture goes dormant, the plain copy (a Shrine card) dies for good",
        ShrineOf(g2, g2.Player1)?.Card.Id == "TRLA_112t" && ShrineOf(g2, g2.Player2)?.Card.Id == "TRLA_107t" && copy != null && copy.Zone == g2.Player2.GraveyardZone
        && g2.Player2.BoardZone.Count(m => m.Card.Id.StartsWith("TRLA_107", StringComparison.Ordinal)) == 1, $"p2 {BoardIds(g2.Player2)} p1 {BoardIds(g2.Player1)}");
});
Guard("W4", () =>
{
    var (s, plan, g) = F(Armament, 404, upgrades: new[] { Wrath });
    Mana(g.Player1, 10); ClearExceptShrines(g);
    var fixture = ShrineRules.Place(g.Player2, DungeonRunShrines.FixtureShrine); g.DeathProcessingAndAuraUpdate();
    Play(g, ToHand(g, g.Player1, "EX1_564"), ShrineOf(g, g.Player1)); // Faceless on the own shrine
    var copy = g.Player1.BoardZone.OfType<Minion>().FirstOrDefault(m => m.Card.Id == "TRLA_112" && !ShrineRules.IsMarked(m));
    Kill(g, copy);
    Check("W4 a copy of the shrine has no Wrath: killing it leaves the fixture awake", copy != null && GrantsOn(copy).Count == 0 && ShrineRules.IsAwake(fixture), $"p2 {BoardIds(g.Player2)}");
    EndTurn(g); Mana(g.Player2, 10);
    var shrine = ShrineOf(g, g.Player1);
    Play(g, ToHand(g, g.Player2, "EX1_332"), shrine);
    Kill(g, shrine);
    Check("W4 a silenced shrine has no Wrath: the fixture survives its death", ShrineRules.IsAwake(fixture), BoardIds(g.Player2));
});
Guard("W5", () =>
{
    var (s, plan, g) = F(Armament, 405, upgrades: new[] { Wrath }, treasures: new[] { "LOOTA_845" }); // Totem of the Dead
    ClearExceptShrines(g);
    var fixture = ShrineRules.Place(g.Player2, DungeonRunShrines.FixtureShrine); g.DeathProcessingAndAuraUpdate();
    Kill(g, ShrineOf(g, g.Player1));
    Check("W5 with Totem of the Dead (twice): one destroy, the second finds no awake Shrine; the fixture dormant once",
        ShrineOf(g, g.Player2)?.Card.Id == "TRLA_107t" && g.Player2.GraveyardZone.Count(p => p.Card.Id == "TRLA_107") == 1, BoardIds(g.Player2));
    var (s2, p2, g2) = F(Armament, 406, upgrades: new[] { Pact, Wrath });
    ClearExceptShrines(g2);
    ShrineRules.Place(g2.Player2, DungeonRunShrines.FixtureShrine); g2.DeathProcessingAndAuraUpdate();
    Kill(g2, ShrineOf(g2, g2.Player1));
    ToTurn(g2, 5);
    var back = ShrineOf(g2, g2.Player1);
    Check("W5 with TRLA_805: revived at turn 5 carrying Wrath again", back.Card.Id == "TRLA_112" && GrantsOn(back).SequenceEqual(new[] { "TRLA_806e2" }));
});

// ================================================================ B TRLA_807 Boon Reaper
Guard("B1", () =>
{
    var (s, plan, g) = F(Armament, 501, upgrades: new[] { Reaper });
    FillDeck(g.Player1, 12); ClearHand(g, g.Player1);
    ToHand(g, g.Player1, "CS2_029"); ToHand(g, g.Player1, "CS2_182"); ToHand(g, g.Player1, "CS2_008");
    int deck = g.Player1.DeckZone.Count, health = g.Player1.Hero.Health;
    var shrine = ShrineOf(g, g.Player1);
    Check("B1 the shrine carries TRLA_807e2", GrantsOn(shrine).SequenceEqual(new[] { "TRLA_807e2" }) && HasPlayerEnchant(g.Player1, "TRLA_807e"));
    Kill(g, shrine);
    Check("B1 Deathrattle: the owner draws until the hand is full (3 -> 10, 7 cards from the deck)", g.Player1.HandZone.Count == 10 && g.Player1.DeckZone.Count == deck - 7,
        $"hand {g.Player1.HandZone.Count} deck {deck}->{g.Player1.DeckZone.Count}");
    var (s2, p2, g2) = F(Armament, 502, upgrades: new[] { Reaper });
    ClearHand(g2, g2.Player1); ClearDeck(g2, g2.Player1);
    ToHand(g2, g2.Player1, "CS2_029");
    int h2 = g2.Player1.Hero.Health;
    Kill(g2, ShrineOf(g2, g2.Player1));
    Check("B2 an empty deck stops it: no card, no fatigue (as Bag of Stuffing)", g2.Player1.HandZone.Count == 1 && g2.Player1.Hero.Health == h2, $"{g2.Player1.HandZone.Count} {h2}->{g2.Player1.Hero.Health}");
});
Guard("B3", () =>
{
    var (s, plan, g) = F(Armament, 503, upgrades: new[] { Reaper }, treasures: new[] { "LOOTA_845" });
    FillDeck(g.Player1, 12); ClearHand(g, g.Player1);
    ToHand(g, g.Player1, "CS2_029");
    int deck = g.Player1.DeckZone.Count;
    Kill(g, ShrineOf(g, g.Player1));
    Check("B3 with Totem of the Dead: the second run draws nothing more (hand full), no burn", g.Player1.HandZone.Count == 10 && g.Player1.DeckZone.Count == deck - 9,
        $"hand {g.Player1.HandZone.Count} deck {deck}->{g.Player1.DeckZone.Count}");
    var (s2, p2, g2) = F("rumble_rogue_106", 504, upgrades: new[] { Reaper });
    Mana(g2.Player1, 10); ClearExceptShrines(g2); ClearHand(g2, g2.Player1); FillDeck(g2.Player1, 12);
    var ally = Board(g2, g2.Player1, "CS2_182");
    int atk = ally.AttackDamage;
    Kill(g2, ShrineOf(g2, g2.Player1));
    Check("B4 with Bottled Terror: its own Deathrattle (+2 Attack) and the granted one (draw to full) both fire", ally.AttackDamage == atk + 2 && g2.Player1.HandZone.Count == 10,
        $"atk {atk}->{ally.AttackDamage} hand {g2.Player1.HandZone.Count}");
    var (s3, p3, g3) = F(Armament, 505, upgrades: new[] { Reaper });
    ClearHand(g3, g3.Player1); FillDeck(g3.Player1, 12);
    EndTurn(g3); Mana(g3.Player2, 10);
    Play(g3, ToHand(g3, g3.Player2, "CS1_113"), ShrineOf(g3, g3.Player1)); // Mind Control: nothing happens
    Check("B5 Mind Control on the shrine: still the owner's, grant kept, no Deathrattle", ShrineRules.IsAwake(ShrineOf(g3, g3.Player1)) && g3.Player1.HandZone.Count == 0
        && GrantsOn(ShrineOf(g3, g3.Player1)).SequenceEqual(new[] { "TRLA_807e2" }));
    Mana(g3.Player2, 10);
    Play(g3, ToHand(g3, g3.Player2, "EX1_581"), ShrineOf(g3, g3.Player1)); // Sap: returned to hand -> destroyed instead
    Check("B5 Sap on the shrine: destroyed instead (dormant), and the Deathrattle draws the owner to full on the boss's turn",
        ShrineOf(g3, g3.Player1)?.Card.Id == "TRLA_112t" && g3.Player1.HandZone.Count == 10 && !g3.Player1.HandZone.Any(p => p.Card.Id == "TRLA_112"), $"{g3.Player1.HandZone.Count}");
});

// ================================================================ R TRLA_808 Roaring Edifice
Guard("R1", () =>
{
    var (s, plan, g) = F(Armament, 601, upgrades: new[] { Edifice });
    ClearHand(g, g.Player1);
    var fb = ToHand(g, g.Player1, "CS2_029"); var yeti = ToHand(g, g.Player1, "CS2_182"); var moon = ToHand(g, g.Player1, "CS2_008");
    var shrine = ShrineOf(g, g.Player1);
    Check("R1 the shrine carries TRLA_808e2", GrantsOn(shrine).SequenceEqual(new[] { "TRLA_808e2" }) && HasPlayerEnchant(g.Player1, "TRLA_808e"));
    Kill(g, shrine);
    Check("R1 Deathrattle: the cards in hand cost (2) less (Fireball 2, Yeti 2, Moonfire stays 0)", fb.Cost == 2 && yeti.Cost == 2 && moon.Cost == 0, $"{fb.Cost} {yeti.Cost} {moon.Cost}");
    var later = ToHand(g, g.Player1, "CS2_029");
    Check("R1 a card that enters the hand afterwards is full price", later.Cost == 4);
    var bossCard = ToHand(g, g.Player2, "CS2_029");
    Check("R1 the boss's hand is untouched", bossCard.Cost == 4);
});
Guard("R2", () =>
{
    var (s, plan, g) = F(Armament, 602, upgrades: new[] { Edifice }, treasures: new[] { "LOOTA_845" });
    ClearHand(g, g.Player1);
    var deathwing = ToHand(g, g.Player1, "NEW1_030");
    Kill(g, ShrineOf(g, g.Player1));
    Check("R2 with Totem of the Dead: twice, (4) less (Deathwing 10 -> 6)", deathwing.Cost == 6, deathwing.Cost.ToString());
    var (s2, p2, g2) = F(Armament, 603, upgrades: new[] { Devotion, Edifice });
    ClearHand(g2, g2.Player1);
    var dw2 = ToHand(g2, g2.Player1, "NEW1_030");
    Kill(g2, ShrineRules.Of(g2.Player1).ToArray());
    Check("R2 TRLA_804: both shrines die at once, two Deathrattles, (4) less", dw2.Cost == 6 && ShrineRules.Of(g2.Player1).All(m => m.IsDormant), dw2.Cost.ToString());
    var (s3, p3, g3) = F("rumble_priest_114", 604, upgrades: new[] { Devotion, Edifice });
    ClearHand(g3, g3.Player1);
    var dw3 = ToHand(g3, g3.Player1, "NEW1_030");
    Kill(g3, ShrineRules.Of(g3.Player1)[0]);
    Check("R3 with two Bwonsamdi's Sanctums: one dies while the other lives (Deathrattles trigger 2 more times): 3 x (2) less (Deathwing 4)", dw3.Cost == 4, dw3.Cost.ToString());
    var (s4, p4, g4) = F(Armament, 605, upgrades: new[] { Sacred, Edifice });
    ClearHand(g4, g4.Player1);
    var fb4 = ToHand(g4, g4.Player1, "CS2_029");
    Check("R4 with Sacred Ritual the Fireball costs 3", fb4.Cost == 3);
    Kill(g4, ShrineOf(g4, g4.Player1));
    Check("R4 the shrine dies: Sacred's (1) goes with it, Roaring Edifice's (2) stays: Fireball 2", fb4.Cost == 2, fb4.Cost.ToString());
});

// ================================================================ D TRLA_804 / TRLA_805 with the grants
Guard("D1", () =>
{
    var (s, plan, g) = F(Armament, 701, upgrades: new[] { Wrath, Devotion, Reaper });
    var two = ShrineRules.Of(g.Player1);
    Check("D1 TRLA_804 with two grant upgrades: two shrines, each with both grants in pick order",
        two.Count == 2 && two.All(m => GrantsOn(m).SequenceEqual(new[] { "TRLA_806e2", "TRLA_807e2" })), string.Join(";", two.Select(m => string.Join(",", GrantsOn(m)))));
    ClearHand(g, g.Player1); FillDeck(g.Player1, 12);
    ShrineRules.Place(g.Player2, DungeonRunShrines.FixtureShrine); g.DeathProcessingAndAuraUpdate();
    Kill(g, two[0]);
    Check("D1 one dies: the fixture goes dormant, the hand fills; the other shrine stays awake with its grants",
        ShrineOf(g, g.Player2)?.IsDormant == true && g.Player1.HandZone.Count == 10 && ShrineRules.IsAwake(ShrineRules.Of(g.Player1)[1])
        && GrantsOn(ShrineRules.Of(g.Player1)[1]).Count == 2);
    var (s2, p2, g2) = F(Armament, 702, upgrades: new[] { Pact });
    Check("D2 TRLA_805 alone has no grant (its effect is the revival timer)", GrantsOn(ShrineOf(g2, g2.Player1)).Count == 0 && ShrineRules.ReviveTurns(g2.Player1) == 2);
    Kill(g2, ShrineOf(g2, g2.Player1));
    Check("D2 the dormant form shows 2 turns", ShrineRules.TurnsLeft(ShrineOf(g2, g2.Player1)) == 2);
});

// ================================================================ N History=false (AI simulation mode)
Guard("N1", () =>
{
    var (s, plan, g) = F(Armament, 801, upgrades: new[] { Devotion, Sacred, Favor, Fortified, Wrath, Reaper, Edifice }, history: false);
    var two = ShrineRules.Of(g.Player1);
    Check("N1 History=false: both shrines carry the six grants as entities", two.Count == 2 && two.All(m => GrantsOn(m).Count == 6),
        string.Join(";", two.Select(m => string.Join(",", GrantsOn(m)))));
    ClearExceptShrines(g); ClearHand(g, g.Player1);
    var fb = ToHand(g, g.Player1, "CS2_029");
    var a = Board(g, g.Player1, "CS2_182");
    Check("N1 History=false: Sacred x2 (Fireball 2), Favor (the neighbour and both shrines have Taunt)", fb.Cost == 2 && a.HasTaunt && two.All(m => m.HasTaunt), $"{fb.Cost} {BoardIds(g.Player1)}");
    Damage(g, two[1], 3);
    EndTurn(g);
    Check("N1 History=false: Fortified heals", two[1].Health == 5);
    EndTurn(g);
    ShrineRules.Place(g.Player2, DungeonRunShrines.FixtureShrine); g.DeathProcessingAndAuraUpdate();
    ClearHand(g, g.Player1); FillDeck(g.Player1, 12);
    var dw = ToHand(g, g.Player1, "NEW1_030");
    Kill(g, two[1]);
    Check("N1 History=false: the granted Deathrattles fire (fixture dormant, hand full, the Deathwing in hand (2) less: 8)",
        ShrineOf(g, g.Player2)?.IsDormant == true && g.Player1.HandZone.Count == 10 && dw.Cost == 8, $"{ShrineOf(g, g.Player2)?.Card.Id} hand {g.Player1.HandZone.Count} dw {dw.Cost}");
    var clone = g.Clone();
    var cloneShrine = ShrineRules.Of(clone.Player1).First(m => !m.IsDormant);
    Check("N2 a game clone (the AI's search) keeps the grants and their effects", GrantsOn(cloneShrine).Count == 6 && cloneShrine.HasTaunt == ShrineRules.Of(g.Player1)[0].HasTaunt);
});

Console.WriteLine($"SUMMARY checks passed {passes} failed {failures}");
return failures == 0 ? 0 : 1;
