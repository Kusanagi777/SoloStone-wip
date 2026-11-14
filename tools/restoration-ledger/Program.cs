// Restoration ledger generator (docs/design/restoration-ledger.md, "How the ledger is updated").
// One row per card id or run catalog entry: the 2050 original adventure-id records, every card of an original bucket,
// starter or finale deck, the original loadout heroes/powers/treasures, and SoloStone's own run content. Each row gets
// separate engine / availability / encounter / AI / validation columns and one classification. Read-only on every input;
// writes only the two output files.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using SabberStoneCore.CardSets.DungeonRun;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using EngineCard = SabberStoneCore.Model.Card;

namespace RestorationLedger
{
    internal sealed class OrderedSet : IEnumerable<string>
    {
        private readonly List<string> items = new List<string>();
        private readonly HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
        /// <summary>Called on every Add, duplicates included.</summary>
        public Action<string> OnAdd;
        public bool Add(string s) { if (string.IsNullOrEmpty(s)) return false; OnAdd?.Invoke(s); if (!seen.Add(s)) return false; items.Add(s); return true; }
        public bool Contains(string s) => s != null && seen.Contains(s);
        public void Clear() { items.Clear(); seen.Clear(); }
        public bool Remove(string s) { if (s == null || !seen.Remove(s)) return false; items.Remove(s); return true; }
        public int Count => items.Count;
        public IEnumerator<string> GetEnumerator() => items.GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => items.GetEnumerator();
        public override string ToString() => string.Join(";", items);
    }

    internal sealed class Row
    {
        public string Id, Name, Type, Class, Text, Category, Source;
        public bool? Collectible;
        public EngineCard Card;
        public readonly OrderedSet Adventures = new OrderedSet(), Roles = new OrderedSet(), Buckets = new OrderedSet(),
            Decks = new OrderedSet(), Notes = new OrderedSet(), Alternates = new OrderedSet(), BlockedBy = new OrderedSet(),
            Player = new OrderedSet(), Encounter = new OrderedSet(), AiBlocks = new OrderedSet(), Pairs = new OrderedSet();
        /// <summary>The adventure last added: every role added after it is that adventure's role (Pairs, "adventure\trole").</summary>
        private string ctx;
        public Row()
        {
            Adventures.OnAdd = a => ctx = a;
            Roles.OnAdd = r => Pairs.Add(ctx + "\t" + r);
        }
        public bool Has(string adventure, string role) => Pairs.Contains(adventure + "\t" + role);
        public bool Support, HeroUnit, Container, Custom, SpecialFinale;
        public string Owner, AdaptedBy, AdaptFit, SupportWhy;
        /// <summary>pool, player-power, boss-power, hero, custom, or "na:&lt;reason&gt;".</summary>
        public string ValidationKind;
        public string Engine, Ai, Validation, Classification, Gaps;
        public bool PlayerOffered => Player.Count > 0;
        public bool EncounterUsed => Encounter.Count > 0;
        /// <summary>
        /// Phase 8: an offer or use that exists only on a schedule no new run follows yet (acts-v3h and acts-v3t before
        /// schedule-v1.json "defaults" names them) carries this suffix. It is shown, but it does not count as reach.
        /// </summary>
        public const string Pending = " (schedule not a default yet)";
        public bool PlayerReached => Player.Any(w => !w.EndsWith(Pending, StringComparison.Ordinal));
        public bool EncounterReached => Encounter.Any(w => !w.EndsWith(Pending, StringComparison.Ordinal));
        public bool PendingReach => Player.Concat(Encounter).Any(w => w.EndsWith(Pending, StringComparison.Ordinal));
    }

    internal static class Program
    {
        private static readonly string[] AdventureOrder = { "Dungeon Run", "Monster Hunt", "Rumble Run", "The Dalaran Heist", "Tombs of Terror", "SoloStone" };
        private static readonly Dictionary<string, string> AdventureKeys = new Dictionary<string, string>
        {
            ["dungeon-run"] = "Dungeon Run", ["monster-hunt"] = "Monster Hunt", ["rumble-run"] = "Rumble Run",
            ["dalaran-heist"] = "The Dalaran Heist", ["tombs-of-terror"] = "Tombs of Terror",
        };
        private static readonly string[] Classifications = { "implemented", "partial", "missing", "duplicate/support", "intentionally adapted" };

        private static readonly Dictionary<string, Row> Rows = new Dictionary<string, Row>(StringComparer.Ordinal);
        /// <summary>NativeMatch.TransportCardId's fixed map: engine id -> id sent to the client (CS2_017 -> HERO_06bp).</summary>
        private static readonly Dictionary<string, string> transport = new Dictionary<string, string>(StringComparer.Ordinal);
        /// <summary>Client names (adventures.json) for ids the engine does not load.</summary>
        private static readonly Dictionary<string, (string name, string type)> clientNames = new Dictionary<string, (string, string)>(StringComparer.Ordinal);

        private static void ClientName(JsonElement card, string type)
        {
            string id = card.GetProperty("id").GetString();
            if (!clientNames.ContainsKey(id)) clientNames[id] = (card.TryGetProperty("name", out var n) ? n.GetString() : null, type);
        }
        private static Dictionary<string, EngineCard> engine;
        private static DungeonRunData data;
        /// <summary>The act schedules (schedule-v1.json): phase 7's teammate and shrine-upgrade drafts sit on its default schedule.</summary>
        private static DungeonRunSchedules schedules;

        /// <summary>What the shared draft catalog (catalog-v1.json) records for one card id (a member's listed id or engine id).</summary>
        private sealed class CatalogFact
        {
            /// <summary>Pool entries that admit the card; Reachable: those a playable class row can be offered.</summary>
            public readonly SortedSet<string> Admitted = new SortedSet<string>(StringComparer.Ordinal), Reachable = new SortedSet<string>(StringComparer.Ordinal);
            /// <summary>Admitting entries no playable row is eligible for because their dual-class pair has no enabled row.</summary>
            public readonly SortedSet<string> DualBlocked = new SortedSet<string>(StringComparer.Ordinal);
            /// <summary>The recorded exclusion reasons ("engine:unscripted", "admission:highlander", "validator:class", ...).</summary>
            public readonly SortedSet<string> Excluded = new SortedSet<string>(StringComparer.Ordinal);
            /// <summary>The engine id that resolves a listed reprint id, when it differs.</summary>
            public string Via;
        }
        private static readonly Dictionary<string, CatalogFact> CatalogFacts = new Dictionary<string, CatalogFact>(StringComparer.Ordinal);
        /// <summary>Pool entries some playable class row (data.Classes) is eligible for (DungeonRunBundle.EligibleFor, the draft's predicate).</summary>
        private static readonly HashSet<string> ReachableEntries = new HashSet<string>(StringComparer.Ordinal);
        private static readonly Dictionary<string, string> Inputs = new Dictionary<string, string>();

        private static int Main(string[] args)
        {
            try { return Run(args); }
            catch (Exception e) { Console.Error.WriteLine("restoration-ledger: " + e); return 1; }
        }

        private static string Arg(string[] args, string name, string fallback)
        {
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback;
        }

        private static string FindRepo()
        {
            foreach (string start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
                for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
                    if (File.Exists(Path.Combine(dir.FullName, "plugin", "DungeonRun", "DungeonRunValidator.cs"))
                        && Directory.Exists(Path.Combine(dir.FullName, "engine", "SabberStoneCore")))
                        return dir.FullName;
            throw new DirectoryNotFoundException("Repository root not found; pass --repo <path>.");
        }

        private static string ReadInput(string label, string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            Inputs[label] = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant().Substring(0, 16);
            return new UTF8Encoding(false).GetString(bytes).TrimStart('﻿');
        }

        private static int Run(string[] args)
        {
            if (args.Contains("--help") || args.Contains("-h"))
            {
                Console.WriteLine("RestorationLedger [--repo DIR] [--reference-csv FILE] [--out-csv FILE] [--out-md FILE] [--no-client]");
                return 0;
            }
            string repo = Path.GetFullPath(Arg(args, "--repo", null) ?? FindRepo());
            string refDir = Path.Combine(repo, "data", "dungeon-run", "reference");
            string csvPath = Arg(args, "--reference-csv", Path.Combine(refDir, "adventure-reference.csv"));
            string outCsv = Arg(args, "--out-csv", Path.Combine(repo, "docs", "design", "restoration-ledger.csv"));
            string outMd = Arg(args, "--out-md", Path.Combine(repo, "docs", "design", "restoration-ledger.md"));
            string curationPath = Path.Combine(repo, "tools", "restoration-ledger", "curation.json");

            // ------------------------------------------------------------------ inputs
            NpcWorldJson.Parser = text => { using var doc = JsonDocument.Parse(text); return FromJson(doc.RootElement); };
            // With the shared draft catalog (phase 3): its pool entries are what a catalog run (every new run) drafts.
            string catalogText = ReadInput("data/dungeon-run/catalog-v1.json", Path.Combine(repo, "data", "dungeon-run", "catalog-v1.json"));
            data = DungeonRunData.Parse(
                ReadInput("data/dungeon-run/run-v1.json", Path.Combine(repo, "data", "dungeon-run", "run-v1.json")),
                ReadInput("data/dungeon-run/pool-v1.json", Path.Combine(repo, "data", "dungeon-run", "pool-v1.json")),
                ReadInput("data/npc/decks-v1.json", Path.Combine(repo, "data", "npc", "decks-v1.json")), catalogText);
            // The imported encounters (phase 8): rows of data.Bosses after the custom bosses, with their decks.
            DungeonRunEncounterData.Parse(data, ReadInput("data/dungeon-run/encounters-v1.json", Path.Combine(repo, "data", "dungeon-run", "encounters-v1.json")));
            LoadCatalogFacts(catalogText);
            // The act schedules (phase 7): a shrine run's teammate and shrine-upgrade drafts are draft flags on its default schedule.
            schedules = DungeonRunSchedules.Parse(ReadInput("data/dungeon-run/schedule-v1.json", Path.Combine(repo, "data", "dungeon-run", "schedule-v1.json")));
            // Bob's tavern table (phase 5): its options are run services, its enchantments deck changes.
            tavern = DungeonRunTavernTable.Parse(ReadInput("data/dungeon-run/tavern-v1.json", Path.Combine(repo, "data", "dungeon-run", "tavern-v1.json")));
            engine = Cards.All.GroupBy(c => c.Id).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
            Console.WriteLine($"engine cards: {engine.Count}");
            using var curation = JsonDocument.Parse(ReadInput("tools/restoration-ledger/curation.json", curationPath));
            using var buckets = JsonDocument.Parse(ReadInput("data/dungeon-run/reference/buckets.json", Path.Combine(refDir, "buckets.json")));
            using var adventures = JsonDocument.Parse(ReadInput("data/dungeon-run/reference/adventures.json", Path.Combine(refDir, "adventures.json")));
            var reference = ParseCsv(ReadInput("data/dungeon-run/reference/adventure-reference.csv", csvPath));

            HashSet<string> clientIds = null;
            string clientTables = Path.Combine(refDir, "client-tables.json");
            if (!args.Contains("--no-client") && File.Exists(clientTables))
            {
                using var ct = JsonDocument.Parse(ReadInput("data/dungeon-run/reference/client-tables.json", clientTables));
                clientIds = new HashSet<string>(ct.RootElement.GetProperty("tables").GetProperty("CARD").EnumerateArray()
                    .Select(c => c.GetProperty("id").GetString()), StringComparer.Ordinal);
                Console.WriteLine($"client CARD ids: {clientIds.Count}");
            }
            // The game asks the client about NativeMatch.TransportCardId(id), the id it actually sends. That method needs the
            // client assembly, so its fixed id map is read from the source here (the Jade-tier rule is repeated below).
            transport.Clear();
            string nativeMatch = File.ReadAllText(Path.Combine(repo, "plugin", "NativeMatch.cs"));
            int start = nativeMatch.IndexOf("internal static string TransportCardId(string cardId)", StringComparison.Ordinal);
            if (start < 0) throw new InvalidDataException("plugin/NativeMatch.cs: TransportCardId(string) not found");
            string body = nativeMatch.Substring(start, nativeMatch.IndexOf("\n    }\n", start, StringComparison.Ordinal) - start);
            foreach (Match m in Regex.Matches(body, "case \"([^\"]+)\": return \"([^\"]+)\";")) transport[m.Groups[1].Value] = m.Groups[2].Value;
            Inputs["plugin/NativeMatch.cs TransportCardId map"] = transport.Count + " ids";
            string Transport(string id)
            {
                if (id != null && id.Length == 11 && id.StartsWith("CFM_712_t", StringComparison.Ordinal) && int.TryParse(id.Substring(9), out int size) && size >= 1 && size <= 30)
                    return size >= 20 ? "CFM_712_t20" : size >= 7 ? "CFM_712_t07" : size >= 4 ? "CFM_712_t04" : "CFM_712_t01";
                return id != null && transport.TryGetValue(id, out string t) ? t : id;
            }
            Func<string, bool> clientKnows = clientIds == null ? null : id => clientIds.Contains(Transport(id));

            var root = curation.RootElement;
            var playerPowers = new HashSet<string>(root.GetProperty("playerPowers").EnumerateArray().Select(e => e.GetString()));
            var treasureNames = new Dictionary<(string adv, string name), string>();
            foreach (var adv in root.GetProperty("treasureNames").EnumerateObject())
                foreach (var kind in adv.Value.EnumerateObject())
                    foreach (var n in kind.Value.EnumerateArray()) treasureNames[(adv.Name, n.GetString())] = kind.Name;
            var mutationPhrases = root.GetProperty("deckMutationPhrases").EnumerateArray().Select(e => e.GetString()).ToArray();

            // ------------------------------------------------------------------ original loadout facts (client tables)
            var guestHeroes = new Dictionary<string, string>(StringComparer.Ordinal);
            var advPowers = new Dictionary<string, string>(StringComparer.Ordinal);
            var loadout = new Dictionary<string, (string adv, string cls, string upgradeOf)>(StringComparer.Ordinal);
            var scenarioBosses = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var ad in adventures.RootElement.GetProperty("adventures").EnumerateArray())
            {
                string adv = AdventureKeys[ad.GetProperty("key").GetString()];
                foreach (var g in ad.GetProperty("guestHeroes").EnumerateArray())
                {
                    string gid = g.GetProperty("card").GetProperty("id").GetString();
                    guestHeroes[gid] = adv; ClientName(g.GetProperty("card"), "HERO");
                    // A Tombs of Terror explorer's class pair ("Mage / Rogue" -> MAGE_ROGUE), keyed by its card id stem.
                    if (adv == "Tombs of Terror")
                    {
                        var pair = g.GetProperty("name").GetString().Split('/').Select(x => x.Trim().ToUpperInvariant()).ToList();
                        ExplorerPairs[gid] = string.Join("_", pair);
                        PrimaryPairs[pair[0]] = string.Join("_", pair);
                    }
                }
                foreach (var p in ad.GetProperty("heroPowers").EnumerateArray()) { advPowers[p.GetProperty("card").GetProperty("id").GetString()] = adv; ClientName(p.GetProperty("card"), "HERO_POWER"); }
                foreach (var t in ad.GetProperty("loadoutTreasures").EnumerateArray())
                {
                    string id = t.GetProperty("card").GetProperty("id").GetString(), cls = t.GetProperty("className").GetString();
                    loadout[id] = (adv, cls, null); ClientName(t.GetProperty("card"), null);
                    if (t.TryGetProperty("upgradedCard", out var up) && up.ValueKind == JsonValueKind.Object)
                    { loadout[up.GetProperty("id").GetString()] = (adv, cls, id); ClientName(up, null); }
                }
                foreach (var s in ad.GetProperty("scenarios").EnumerateArray())
                    if (s.TryGetProperty("bossHero", out var b) && b.ValueKind == JsonValueKind.Object) scenarioBosses[b.GetProperty("id").GetString()] = adv;
            }
            foreach (string p in playerPowers) advPowers[p] = "Monster Hunt";
            // Hero powers the client tables tie to an encounter hero (its HERO_POWER tag), not to a player loadout
            // (curation.json "encounterPowers", optional): the boss power role, owned by that hero.
            if (root.TryGetProperty("encounterPowers", out var encounterPowers))
                foreach (var e in encounterPowers.EnumerateArray())
                    EncounterPowers[e.GetProperty("id").GetString()] = (e.GetProperty("owner").GetString(), e.GetProperty("note").GetString());

            // ------------------------------------------------------------------ 1. reference CSV rows
            foreach (var rec in reference)
            {
                var row = Get(rec["card_id"]);
                row.Source = "reference-csv";
                row.Category = rec["category"];
                if (row.Card == null)
                {
                    row.Name = rec["name"]; row.Type = rec["type"]; row.Class = rec["class"]; row.Text = Plain(rec["reference_text"]);
                }
                row.Adventures.Add(rec["adventure"]);
            }
            // Roles in a second pass: a shrine form (TRLA_104t) looks up its shrine (TRLA_104) among all CSV rows.
            foreach (var rec in reference)
                AssignCsvRole(Rows[rec["card_id"]], rec["adventure"], treasureNames, guestHeroes, advPowers, loadout, scenarioBosses);
            // Hero portrait groups: one owner per stem ("GILA_500h3" and "GILA_500h4" share "GILA_500h").
            foreach (var group in Rows.Values.Where(r => r.Source == "reference-csv" && r.Type == "HERO" && !r.Support)
                .GroupBy(r => Regex.Match(r.Id, @"^(.*h)\d*$") is var m && m.Success ? m.Groups[1].Value : r.Id))
            {
                var members = group.ToList();
                if (members.Count < 2) continue;
                var owner = members.FirstOrDefault(r => guestHeroes.ContainsKey(r.Id) || scenarioBosses.ContainsKey(r.Id))
                    ?? members.OrderBy(r => r.Id.Length).ThenBy(r => r.Id, StringComparer.Ordinal).First();
                foreach (var r in members.Where(r => r != owner && !guestHeroes.ContainsKey(r.Id)))
                {
                    r.Roles.Add("portrait");
                    MakeSupport(r, "hero portrait variant", owner.Id);
                }
            }

            // A hero card no run row can use as its portrait whose class forms are the run heroes (curation.json "heroBaseForms",
            // optional): a dependency of those forms, not a reward of its own.
            if (root.TryGetProperty("heroBaseForms", out var baseForms))
                foreach (var e in baseForms.EnumerateArray())
                {
                    string bid = e.GetProperty("id").GetString();
                    if (!Rows.TryGetValue(bid, out var brow)) throw new InvalidDataException($"curation.json heroBaseForms: {bid} is not a ledger row");
                    brow.Roles.Add("portrait"); brow.Notes.Add(e.GetProperty("note").GetString());
                    MakeSupport(brow, "hero base form", e.GetProperty("owner").GetString());
                }

            // ------------------------------------------------------------------ 2. original buckets
            int labelRowsAdded = 0;
            var bucketStats = new List<(string adv, string key, string labelId, string membership, List<string> members)>();
            foreach (var ad in buckets.RootElement.GetProperty("adventures").EnumerateArray())
            {
                string adv = ad.GetProperty("adventure").GetString();
                foreach (var b in ad.GetProperty("buckets").EnumerateArray())
                {
                    string hero = b.GetProperty("hero").GetString(), label = b.GetProperty("label").GetString();
                    string key = $"{adv}:{hero}:{label}";
                    string labelId = b.TryGetProperty("labelCardId", out var lid) && lid.ValueKind == JsonValueKind.String ? lid.GetString() : null;
                    string membership = b.GetProperty("membership").GetString();
                    Row labelRow;
                    if (labelId != null && Rows.ContainsKey(labelId)) labelRow = Rows[labelId];
                    else
                    {
                        labelRow = Get(labelId ?? $"label:{adv}:{hero}:{label}");
                        labelRow.Source = labelId == null ? "buckets.json (no client or CSV id)" : "buckets.json";
                        labelRow.Name ??= label; labelRow.Type ??= "LABEL"; labelRow.Class ??= hero.ToUpperInvariant();
                        labelRow.Adventures.Add(adv);
                        labelRow.Roles.Add("bucket label");
                        labelRowsAdded++;
                        if (labelId == null) labelRow.Notes.Add("label recovered from " + (b.TryGetProperty("source", out var s) && s.ValueKind == JsonValueKind.String ? s.GetString() : "buckets.json") + "; the local facts have no label record");
                    }
                    labelRow.Buckets.Add(key);
                    var members = new List<string>();
                    foreach (var m in b.GetProperty("members").EnumerateArray())
                    {
                        if (!m.TryGetProperty("cardId", out var cid) || cid.ValueKind != JsonValueKind.String) continue;
                        var row = Get(cid.GetString());
                        row.Source ??= "buckets.json";
                        row.Adventures.Add(adv);
                        row.Roles.Add("bucket member");
                        row.Buckets.Add(key);
                        if (m.TryGetProperty("alternates", out var alts)) foreach (var a in alts.EnumerateArray()) row.Alternates.Add(a.GetString());
                        if (m.TryGetProperty("alias", out var alias) && alias.ValueKind == JsonValueKind.String) row.Notes.Add("bucket name alias: " + alias.GetString());
                        members.Add(row.Id);
                    }
                    if (membership != "published")
                        labelRow.Notes.Add($"membership {membership}: " + (b.TryGetProperty("notes", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString() : "no member list"));
                    MakeSupport(labelRow, "bucket label (draft catalog entry, not a reward)", null);
                    bucketStats.Add((adv, key, labelRow.Id, membership, members));
                }
            }

            // ------------------------------------------------------------------ 3. original starter / finale decks, loadouts
            foreach (var ad in adventures.RootElement.GetProperty("adventures").EnumerateArray())
            {
                string adv = AdventureKeys[ad.GetProperty("key").GetString()];
                foreach (var d in ad.GetProperty("starterDecks").EnumerateArray())
                {
                    string note = d.GetProperty("noteName").GetString(), name = d.GetProperty("name").GetString();
                    var cards = d.GetProperty("cards").EnumerateArray().Select(c => c.GetString()).ToList();
                    foreach (string id in cards)
                    {
                        var row = Get(id); row.Source ??= "adventures.json";
                        row.Adventures.Add(adv); row.Roles.Add("starter"); row.Decks.Add($"{adv}:{note} ({name})");
                        if (cards.Count == 1) row.Notes.Add("placeholder card of the 'Random' starter deck " + note);
                    }
                }
                foreach (var d in ad.GetProperty("otherPrefixedDecks").EnumerateArray())
                {
                    string note = d.GetProperty("noteName").GetString(), name = d.GetProperty("name").GetString();
                    bool shrine = note.EndsWith("_Player", StringComparison.Ordinal), finale = note.Contains("_Tekanh_Deck_");
                    string role = shrine ? "starter" : finale ? "finale deck" : "deck";
                    string label = shrine ? $"{adv}:{note} (shrine starter deck, inferred from the name)" : finale ? $"{adv}:{note} ({name}; Inner Sanctum finale)" : $"{adv}:{note}";
                    foreach (var c in d.GetProperty("cards").EnumerateArray())
                    {
                        var row = Get(c.GetString()); row.Source ??= "adventures.json";
                        row.Adventures.Add(adv); row.Roles.Add(role); row.Decks.Add(label);
                        if (shrine) row.Notes.Add("in a Rumble Run shrine starter deck");
                    }
                }
            }
            foreach (var pair in advPowers)
            {
                var row = Get(pair.Key); row.Source ??= "adventures.json";
                row.Adventures.Add(pair.Value); row.Roles.Add("hero power");
                row.Notes.Add("original player power (" + pair.Value + ")");
            }
            foreach (var pair in guestHeroes)
            {
                var row = Get(pair.Key); row.Source ??= "adventures.json";
                row.Adventures.Add(pair.Value); row.Roles.Add("hero"); row.HeroUnit = true;
                if (row.Support && row.SupportWhy == "hero portrait variant") { row.Support = false; row.Owner = null; }
            }
            foreach (var pair in loadout)
            {
                var row = Get(pair.Key); row.Source ??= "adventures.json";
                row.Adventures.Add(pair.Value.adv); row.Roles.Add("signature treasure");
                row.Notes.Add($"signature treasure for {pair.Value.cls}" + (pair.Value.upgradeOf != null ? $"; upgraded form of {pair.Value.upgradeOf}" : ""));
            }
            foreach (var pair in scenarioBosses)
            {
                var row = Get(pair.Key); row.Source ??= "adventures.json";
                row.Adventures.Add(pair.Value); row.Roles.Add("boss hero"); row.HeroUnit = true;
                row.Notes.Add("named by a client SCENARIO row");
            }
            // Named final bosses and finale records (gap review section 9; the client has no final-boss relation).
            foreach (var f in root.GetProperty("finalBosses").EnumerateArray())
            {
                string id = f.GetProperty("id").GetString();
                if (!Rows.TryGetValue(id, out var row)) throw new InvalidDataException($"curation.json finalBosses: {id} is not a ledger row");
                row.Adventures.Add(f.GetProperty("adventure").GetString()); row.Roles.Add("final boss"); row.HeroUnit = true;
                bool special = f.TryGetProperty("special", out var sp) && sp.GetBoolean();
                row.SpecialFinale |= special;
                row.Notes.Add("final boss (gap review section 9)" + (special ? "; special finale: " + f.GetProperty("note").GetString() : ""));
            }
            foreach (var f in root.GetProperty("finaleRecords").EnumerateArray())
            {
                string id = f.GetProperty("id").GetString();
                if (!Rows.TryGetValue(id, out var row)) throw new InvalidDataException($"curation.json finaleRecords: {id} is not a ledger row");
                row.SpecialFinale = true;
                row.Notes.Add("special finale: " + f.GetProperty("note").GetString());
            }

            // ------------------------------------------------------------------ 4. SoloStone run content
            AddCustomContent();

            // Basic class powers the original adventures also offered under other ids (HERO_06bp "Shapeshift" ~ CS2_017).
            var currentPowerNames = data.Classes.SelectMany(c => c.Powers).Where(p => !DungeonRunContent.TryGet(p, out _))
                .Where(engine.ContainsKey).Distinct().ToDictionary(id => id, id => engine[id].Name);
            foreach (var row in Rows.Values.Where(r => r.Type == "HERO_POWER" && !r.Custom && advPowers.ContainsKey(r.Id)))
                foreach (var p in currentPowerNames.Where(p => p.Value == row.Name && p.Key != row.Id)) row.Alternates.Add(p.Key);
            // Ids the client knows a current engine card by (HERO_06bp is how CS2_017 Shapeshift is sent).
            foreach (var pair in transport)
                if (Rows.TryGetValue(pair.Value, out var tr) && tr.Card == null)
                {
                    tr.Alternates.Add(pair.Key);
                    tr.Notes.Add($"the client id NativeMatch sends for engine card {pair.Key}");
                    if (engine.TryGetValue(pair.Key, out var src)) { tr.Type ??= src.Type.ToString(); tr.Name ??= src.Name; tr.Class ??= src.Class.ToString(); tr.Collectible ??= src.Collectible; }
                }
            // Reprints and original printings of the same collectible card (CORE_*, legacy sets), never hero portraits.
            var collectibleByName = engine.Values.Where(c => c.Collectible && c.Type != CardType.HERO && !c.Id.StartsWith("HERO_", StringComparison.Ordinal))
                .GroupBy(c => (c.Name, c.Type)).ToDictionary(g => g.Key, g => g.Select(c => c.Id).OrderBy(i => i, StringComparer.Ordinal).ToList());
            foreach (var row in Rows.Values.Where(r => r.Card != null && r.Card.Collectible && !r.Container))
                if (collectibleByName.TryGetValue((row.Card.Name, row.Card.Type), out var same))
                    foreach (string id in same.Where(i => i != row.Id)) row.Alternates.Add(id);

            // ------------------------------------------------------------------ 5. owners of support rows
            // The neutral Tribal Shrine placeholder (TRLA_107) is the board slot every class shrine fills.
            int shrineCount = Rows.Values.Count(r => !r.Support && r.Roles.Contains("shrine") && !r.Custom);
            foreach (var row in Rows.Values.Where(r => r.Support && r.Owner == null && r.SupportWhy == "shrine placeholder"))
                row.Owner = $"{shrineCount} Rumble Run shrines (role shrine)";
            foreach (var row in Rows.Values.Where(r => r.Support && r.Owner == null && !r.Roles.Contains("bucket label")))
                row.Owner = FindOwner(row.Id);
            foreach (var (adv, key, labelId, membership, members) in bucketStats)
                Rows[labelId].Owner = members.Count == 0 ? "bucket members unknown" : $"{members.Distinct().Count()} member cards (buckets column)";

            // ------------------------------------------------------------------ 6. analogues
            foreach (var a in root.GetProperty("analogues").EnumerateArray())
            {
                string original = a.GetProperty("original").GetString(), custom = a.GetProperty("custom").GetString();
                if (!Rows.TryGetValue(original, out var row)) continue;
                var content = DungeonRunContent.All.FirstOrDefault(c => c.Key == custom);
                string name = content?.Name ?? custom;
                row.AdaptedBy = $"{name} ({custom}) ~ {row.Name}: {a.GetProperty("note").GetString()}";
                row.AdaptFit = a.GetProperty("fit").GetString();
                string customRow = content?.ShellId ?? "run:treasure:" + custom;
                if (Rows.TryGetValue(customRow, out var cr)) cr.Notes.Add($"analogue of original {row.Id} {row.Name} ({row.AdaptFit})");
            }

            // Hero-row powers the original adventure did not give that hero (curation.json heroRowAdaptations): the Witchwood
            // hunters had one power each (no client ADVENTURE_HERO_POWER rows). Recorded on the row and its portrait
            // (adapted_by); a Monster Hunt row power that is neither the hunter's own nor recorded here fails the build.
            var adaptedPowers = new HashSet<string>(StringComparer.Ordinal);
            if (root.TryGetProperty("heroRowAdaptations", out var heroRowAdaptations))
                foreach (var a in heroRowAdaptations.EnumerateArray())
                {
                    string rowId = a.GetProperty("row").GetString(), power = a.GetProperty("power").GetString();
                    var cls = data.Classes.FirstOrDefault(c => c.Id == rowId)
                        ?? throw new InvalidOperationException("curation.json heroRowAdaptations: no hero row " + rowId);
                    if (!cls.Powers.Contains(power))
                        throw new InvalidOperationException($"curation.json heroRowAdaptations: {rowId} has no power {power}");
                    adaptedPowers.Add(rowId + "/" + power);
                    string text = $"SoloStone adaptation: {power} on {cls.Name} ({rowId}): {a.GetProperty("note").GetString()}";
                    foreach (string target in new[] { "run:class:" + rowId, cls.HeroCard })
                        if (Rows.TryGetValue(target, out var adapted))
                            adapted.AdaptedBy = adapted.AdaptedBy == null ? text : adapted.AdaptedBy + " | " + text;
                }
            foreach (var cls in data.Classes.Where(c => c.IsHero && c.Adventure == "MonsterHunt"))
                foreach (string power in cls.Powers)
                    if (!playerPowers.Contains(power) && !adaptedPowers.Contains(cls.Id + "/" + power))
                        throw new InvalidOperationException($"Monster Hunt row {cls.Id} power {power} is not a hunter power (curation.json playerPowers) and has no heroRowAdaptations entry");

            // Hand notes on catalog facts the ledger cannot derive (curation.json catalogNotes).
            if (root.TryGetProperty("catalogNotes", out var catalogNotes))
                foreach (var n in catalogNotes.EnumerateArray())
                    if (Rows.TryGetValue(n.GetProperty("id").GetString(), out var noted)) noted.Notes.Add(n.GetProperty("note").GetString());

            // ------------------------------------------------------------------ 7. availability and encounter use
            Availability();
            TavernRows();
            GeneratedStarterRows();

            // ------------------------------------------------------------------ 8. validation
            Validation(clientKnows);

            // ------------------------------------------------------------------ 9. engine, AI, classification, blockers
            foreach (var row in Rows.Values)
            {
                row.Engine = EngineBehavior(row);
                Ai(row);
                Classify(row);
                Blockers(row, mutationPhrases);
            }

            // ------------------------------------------------------------------ 9b. landed families (phase 6 self-check)
            // A phase-6 family that has landed (curation.json "landedFamilies") promises every id it lists: each must now behave
            // in the engine (scripted, or vanilla with nothing to script), or carry a stated rejection (the family's "rejected"
            // reason). An id still unscripted without one fails the run before anything is written.
            var familyFailures = new List<string>();
            if (root.TryGetProperty("landedFamilies", out var families))
                foreach (var family in families.EnumerateArray())
                {
                    string fid = family.GetProperty("id").GetString();
                    var rejected = family.TryGetProperty("rejected", out var rj) ? rj.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString(), StringComparer.Ordinal)
                        : new Dictionary<string, string>(StringComparer.Ordinal);
                    foreach (var idElement in family.GetProperty("ids").EnumerateArray())
                    {
                        string fidId = idElement.GetString();
                        if (!Rows.TryGetValue(fidId, out var frow)) { familyFailures.Add($"{fid} {fidId}: no ledger row"); continue; }
                        bool behaves = frow.Engine == "scripted" || frow.Engine == "vanilla-no-script-needed" || frow.Engine == RunService;
                        if (behaves) continue;
                        if (rejected.TryGetValue(fidId, out string why) && !string.IsNullOrWhiteSpace(why)) { frow.Notes.Add($"family {fid}: rejected ({why})"); continue; }
                        familyFailures.Add($"{fid} {fidId}: engine {frow.Engine} and no stated rejection");
                    }
                }
            if (familyFailures.Count > 0)
            {
                Console.Error.WriteLine("landed family self-check failed:");
                foreach (string f in familyFailures) Console.Error.WriteLine("  " + f);
                return 3;
            }

            // ------------------------------------------------------------------ 10. output
            var ordered = Rows.Values.OrderBy(r => r.Id, StringComparer.Ordinal).ToList();
            PrintDelta(outCsv, ordered);
            Directory.CreateDirectory(Path.GetDirectoryName(outCsv));
            File.WriteAllText(outCsv, Csv(ordered), new UTF8Encoding(false));
            File.WriteAllText(outMd, Markdown(ordered, reference.Count, labelRowsAdded, bucketStats, clientIds != null, outCsv, repo), new UTF8Encoding(false));
            Console.WriteLine($"rows: {ordered.Count}");
            foreach (string c in Classifications) Console.WriteLine($"  {c}: {ordered.Count(r => r.Classification == c)}");
            Console.WriteLine("wrote " + outCsv);
            Console.WriteLine("wrote " + outMd);
            return 0;
        }

        // ====================================================================== rows

        private static Row Get(string id)
        {
            if (Rows.TryGetValue(id, out var row)) return row;
            row = new Row { Id = id };
            if (engine.TryGetValue(id, out var card))
            {
                row.Card = card;
                row.Name = card.Name; row.Type = card.Type.ToString(); row.Class = card.Class.ToString();
                row.Text = Plain(card.Text); row.Collectible = card.Collectible;
            }
            else if (clientNames.TryGetValue(id, out var cn)) { row.Name = cn.name; row.Type = cn.type; row.Notes.Add("name from the client tables"); }
            Rows[id] = row;
            return row;
        }

        private static void MakeSupport(Row row, string why, string owner)
        {
            row.Support = true; row.SupportWhy ??= why;
            if (owner != null) row.Owner = owner;
        }

        private static void AssignCsvRole(Row row, string adv, Dictionary<(string, string), string> treasureNames,
            Dictionary<string, string> guests, Dictionary<string, string> advPowers,
            Dictionary<string, (string adv, string cls, string upgradeOf)> loadout, Dictionary<string, string> scenarioBosses)
        {
            string id = row.Id;
            switch (row.Category)
            {
                case "Bucket label (pool membership not present)":
                    row.Roles.Add("bucket label"); MakeSupport(row, "bucket label (draft catalog entry, not a reward)", null); break;
                case "Hero power":
                    if (EncounterPowers.TryGetValue(id, out var encounterPower))
                    {
                        row.Roles.Add("boss power"); row.Owner = encounterPower.Owner; row.Notes.Add(encounterPower.Note);
                        break;
                    }
                    if (advPowers.ContainsKey(id)) row.Roles.Add("hero power");
                    else row.Roles.Add(id.Contains("BOSS") ? "boss power" : "hero power");
                    if (Regex.IsMatch(id, @"px\d*$"))
                    {
                        // The variant belongs to its Normal-mode power (DALA_BOSS_71px -> DALA_BOSS_71p), not to the boss portrait.
                        // ULDA_BOSS_39px2 -> ULDA_BOSS_39p2 (phase power), else the unnumbered power.
                        string phase = Regex.Replace(id, @"px(\d*)$", "p$1"), normal = Regex.Replace(id, @"px\d*$", "p");
                        row.Notes.Add("Heroic-mode variant");
                        MakeSupport(row, "Heroic-mode power variant", Rows.ContainsKey(phase) ? phase : Rows.ContainsKey(normal) ? normal : null);
                    }
                    break;
                case "Enchantment/support definition":
                    row.Roles.Add("enchantment"); MakeSupport(row, "enchantment", null); break;
                case "Dummy/support definition":
                    row.Roles.Add("support"); MakeSupport(row, "dummy definition", null); break;
                case "Shrine support/placeholder":
                    row.Roles.Add("support"); MakeSupport(row, "shrine placeholder", null); break;
                case "Hero/encounter portrait or variant":
                    if (guests.ContainsKey(id)) { row.Roles.Add("hero"); row.HeroUnit = true; }
                    else { row.Roles.Add("boss hero"); row.HeroUnit = true; }
                    break;
                case "Passive treasure or shrine upgrade":
                    row.Roles.Add(adv == "Rumble Run" ? "shrine upgrade" : "passive treasure"); break;
                case "Boss card/token": row.Roles.Add("boss card"); break;
                case "Teammate": row.Roles.Add("teammate"); break;
                case "Anomaly": row.Roles.Add("anomaly"); break;
                case "Shrine": row.Roles.Add("shrine"); break;
                case "Tavern service": row.Roles.Add("tavern"); break;
                case "Chapter twist": row.Roles.Add("twist"); break;
                default:
                {
                    var token = Regex.Match(id, @"^(.*?(?:\d|p\d*))t\d*$");
                    if (loadout.ContainsKey(id)) row.Roles.Add("signature treasure");
                    else if (treasureNames.TryGetValue((adv, row.Name), out string kind)) row.Roles.Add(kind == "passive" ? "passive treasure" : "treasure");
                    else if (token.Success && adv == "Rumble Run" && Rows.ContainsKey(token.Groups[1].Value))
                    { row.Roles.Add("shrine form"); MakeSupport(row, "shrine board form", token.Groups[1].Value); }
                    else if (token.Success) { row.Roles.Add("token"); MakeSupport(row, "token", null); }
                    else row.Roles.Add("card");
                    break;
                }
            }
            if (row.Type == "ENCHANTMENT" && !row.Support) { row.Roles.Add("enchantment"); MakeSupport(row, "enchantment", null); }
        }

        /// <summary>The record a support row belongs to: strip one id suffix at a time (e, t, d, px, h2...) until a row exists.</summary>
        private static string FindOwner(string id)
        {
            string s = id;
            for (int i = 0; i < 4; i++)
            {
                string next = Regex.Replace(s, @"[A-Za-z]+\d*$", "");
                if (next == s || next.Length == 0 || next.EndsWith("_", StringComparison.Ordinal)) break;
                s = next;
                foreach (string candidate in new[] { s, s + "h", s + "p", s + "t" })
                    if (candidate != id && Rows.TryGetValue(candidate, out var r) && !r.Support) return candidate;
                var prefixed = Rows.Values.Where(r => r.Id != id && !r.Support && r.Id.StartsWith(s, StringComparison.Ordinal)
                    && r.Id.Length > s.Length && char.IsLetter(r.Id[s.Length])).OrderBy(r => r.Id.Length).ThenBy(r => r.Id, StringComparer.Ordinal).FirstOrDefault();
                if (prefixed != null) return prefixed.Id;
            }
            // Loadout watchers (DALA_Druid_HPe, ULDA_Brann_PDWatcher): the hero powers of that loadout, else its hero.
            var watcher = Regex.Match(id, @"^(.+)_(HP|PD)(e\d*|Watcher)$");
            if (watcher.Success)
            {
                string stem = watcher.Groups[1].Value;
                var powers = Rows.Values.Where(r => !r.Support && Regex.IsMatch(r.Id, "^" + Regex.Escape(stem) + @"_HP\d+$"))
                    .Select(r => r.Id).OrderBy(i => i, StringComparer.Ordinal).ToList();
                bool heroFirst = watcher.Groups[2].Value == "PD";
                if (heroFirst && Rows.TryGetValue(stem, out var hero) && !hero.Support) return stem;
                if (powers.Count > 0) return string.Join(";", powers);
                if (Rows.TryGetValue(stem, out hero) && !hero.Support) return stem;
            }
            // No record to attach to (ULDA_701e2: neither ULDA_701 nor a sibling is a ledger row). Say so instead of a blank.
            string bare = Regex.Replace(id, @"[A-Za-z]+\d*$", "");
            return "unowned: no ledger row for " + (bare.Length > 0 && bare != id && !bare.EndsWith("_", StringComparison.Ordinal) ? bare : id);
        }

        private static void AddCustomContent()
        {
            const string Solo = "SoloStone";
            Row CustomRow(string id, string type, string name)
            {
                var row = Get(id);
                row.Custom = true; row.Source ??= "SoloStone run data";
                row.Adventures.Add(Solo); row.Roles.Add("custom");
                if (type != null && row.Card == null) row.Type = type;
                if (name != null) row.Name = name;
                return row;
            }
            void CardIn(string id, string role, string deckLabel = null, string bucketLabel = null)
            {
                var row = Get(id);
                row.Source ??= "SoloStone run data";
                row.Adventures.Add(Solo); row.Roles.Add(role);
                if (deckLabel != null) row.Decks.Add(deckLabel);
                if (bucketLabel != null) row.Buckets.Add(bucketLabel);
            }
            string ShellName(string id) => id != null && engine.TryGetValue(id, out var c) ? c.Name : null;

            foreach (var cls in data.Classes)
            {
                var row = CustomRow("run:class:" + cls.Id, "CLASS", cls.Name);
                row.Container = true; row.Class = cls.Id; row.Roles.Add("class"); row.ValidationKind = "custom";
                row.Notes.Add($"portrait {cls.HeroCard}; powers {string.Join(", ", cls.Powers)}; decks {string.Join(", ", cls.Decks)}");
                // A named hero row's portrait (phase 6) is the adventure hero itself, not a support record: its own row (role
                // hero) is judged as a hero and is offered through the row (Availability). A class row's portrait stays support.
                if (cls.IsHero && DungeonRunContent.TryGet(cls.HeroCard, out var named) && named.Kind == RunContentKind.Hero)
                {
                    Get(cls.HeroCard).Roles.Add("hero");
                    continue;
                }
                var hero = CustomRow(cls.HeroCard, null, null);
                hero.Roles.Add("hero"); MakeSupport(hero, "class portrait", row.Id);
            }
            // A catalog class row that ships disabled (a Tombs pair with no starter that passes the pool checks): a row that is
            // never offered, blocked by run:dual-class and by its recorded disabledReason.
            foreach (var cls in data.DisabledClasses)
            {
                var row = CustomRow("run:class:" + cls.Id, "CLASS", cls.Name);
                row.Container = true; row.Class = cls.Id; row.Roles.Add("class"); row.ValidationKind = "custom";
                row.Notes.Add($"ships disabled: {cls.DisabledReason}");
                DisabledClassRows[row.Id] = cls.DisabledReason;
                if (cls.ClassSet.Count == 2) DisabledDualRows.Add(row.Id);
                if (cls.IsHero) row.Notes.Add($"named hero row ({cls.Adventure}, portrait {cls.HeroCard}, {RowClasses(cls)})");
            }
            foreach (var deck in data.Decks)
            {
                var row = CustomRow("run:starter:" + deck.Id, "STARTER_DECK", deck.Name);
                row.Container = true; row.Class = deck.Class; row.Roles.Add("starter"); row.ValidationKind = "custom";
                row.Text = deck.Description;
                row.Notes.Add("cards " + string.Join(", ", deck.Cards) + (deck.Unlock != null ? "; unlock " + deck.Unlock : ""));
                foreach (string id in deck.Cards) CardIn(id, "starter", "SoloStone:" + deck.Id);
            }
            foreach (var b in data.LegacyBundles)
            {
                var row = CustomRow("run:bundle:" + b.Id, "BUNDLE", b.Id);
                row.Container = true; row.Class = b.Class; row.Roles.Add("bundle"); row.ValidationKind = "custom";
                row.Text = "theme " + b.Theme;
                row.Notes.Add("cards " + string.Join(", ", b.Cards));
                foreach (string id in b.Cards) CardIn(id, "bundle member", bucketLabel: "SoloStone:" + b.Id);
            }
            // Every content entry: powers, shrines, boss passives, effects, treasures.
            var dataTreasures = data.Treasures.ToDictionary(t => t.Id);
            foreach (var content in DungeonRunContent.All)
            {
                // A named hero portrait (phase 6) is the real adventure card, scripted as a run hero: its row stays the card's
                // own (hero role, hero validation), with a note naming the content and its run Health.
                if (content.Kind == RunContentKind.Hero)
                {
                    var portrait = Get(content.ShellId);
                    portrait.Notes.Add($"content {content.Key} (Hero): scripted run hero portrait at {content.Health} Health");
                    continue;
                }
                string id = content.ShellId ?? "run:treasure:" + content.Key;
                var row = CustomRow(id, content.Kind == RunContentKind.PassiveTreasure ? "PASSIVE_TREASURE" : null, content.Name);
                row.Text = Plain(content.Text);
                row.Notes.Add("content " + content.Key + " (" + content.Kind + ")" + (content.ShellId != null ? $"; client shell {content.ShellId} '{ShellName(content.ShellId)}'" : ""));
                switch (content.Kind)
                {
                    case RunContentKind.HeroPower:
                        row.Roles.Add(data.Bosses.Any(b => b.Power == content.Key) && !data.Classes.Any(c => c.Powers.Contains(content.Key)) ? "boss power" : "hero power");
                        row.ValidationKind = "custom";
                        if (!data.Classes.Any(c => c.Powers.Contains(content.Key)) && !data.Bosses.Any(b => b.Power == content.Key))
                        {
                            string legacy = data.LegacyHeroes.Where(l => l.Value.Power == content.Key).Select(l => l.Key).FirstOrDefault();
                            if (legacy != null) { row.Notes.Add("version-1 save power of legacy hero " + legacy); MakeSupport(row, "legacy save support", "legacy hero " + legacy); }
                            else if (PowersGivenByTreasures.TryGetValue(content.Key, out var givers))
                                row.Notes.Add("no class or boss starts with this power; given in the match by " + string.Join(", ", givers));
                            else row.Notes.Add("no class or boss uses this power");
                        }
                        foreach (string t in content.Tokens) { var tok = Get(t); tok.Source ??= "SoloStone run data"; tok.Adventures.Add(Solo); tok.Roles.Add("token"); MakeSupport(tok, "power token", id); }
                        break;
                    case RunContentKind.PassivePower:
                        bool boss = data.Bosses.Any(b => b.Power == content.Key);
                        // A passive hero power a class or hero row starts with (phase 6 F9: Elise's Starseeker, Brann's Dino
                        // Tracking) is a player power, judged by its offer; one no class row uses stays a boss power.
                        bool playerPassive = data.Classes.Any(c => c.Powers.Contains(content.Key));
                        if (content.Key.StartsWith("shrine_", StringComparison.Ordinal)) row.Roles.Add("shrine");
                        else
                        {
                            if (playerPassive) row.Roles.Add("hero power");
                            if (boss || !playerPassive) row.Roles.Add("boss power");
                        }
                        row.ValidationKind = "custom";
                        if (!boss && playerPassive) row.Notes.Add("passive hero power of " + string.Join(", ", data.Classes.Where(c => c.Powers.Contains(content.Key)).Select(c => c.Id)));
                        else if (!boss)
                        {
                            row.Notes.Add(content.Key.StartsWith("shrine_", StringComparison.Ordinal)
                                ? "legacy custom shrine passive (not Rumble Run's board shrine; not selectable in a new run)" : "no boss uses this passive");
                            if (content.Key.StartsWith("shrine_", StringComparison.Ordinal)) MakeSupport(row, "legacy save support", "legacy shrine field");
                        }
                        break;
                    case RunContentKind.Effect:
                        row.Roles.Add("enchantment");
                        var users = DungeonRunContent.All.Where(c => c.EffectKey == content.Key).ToList();
                        MakeSupport(row, "run effect", users.Count > 0 ? string.Join(",", users.Select(u => u.ShellId ?? "run:treasure:" + u.Key)) : "run scripts (buff/cost enchantment)");
                        break;
                    case RunContentKind.PassiveTreasure:
                    case RunContentKind.ActiveTreasure:
                        row.Roles.Add(content.Kind == RunContentKind.PassiveTreasure ? "passive treasure" : "treasure");
                        row.ValidationKind = "custom";
                        if (dataTreasures.TryGetValue(content.Key, out var t2))
                        {
                            row.Notes.Add($"rarity {t2.Rarity}" + (t2.Tags.Count > 0 ? "; tags " + string.Join(",", t2.Tags) : "")
                                + (t2.UpgradesTo != null ? "; upgrades to " + t2.UpgradesTo : "")
                                + (t2.Scope.IsEmpty ? "" : "; scope " + string.Join(",", t2.Scope.Classes.Concat(t2.Scope.Decks).Concat(t2.Scope.Powers))));
                            if (t2.Legacy) { row.Notes.Add("legacy: kept for version-1 saves, never offered"); MakeSupport(row, "legacy save support", "version-1 saves"); }
                        }
                        else if (ChooseOneParent(content.Key) is string parent)
                            row.Notes.Add("no run-v1.json treasure entry: a Choose One option of the power " + parent);
                        else row.Notes.Add("no run-v1.json treasure entry");
                        // A run treasure has no card class: it is class-scoped only through its scope list, else offered to any class.
                        if (row.Class == null)
                            row.Class = t2 != null && t2.Scope.Classes.Count > 0 ? string.Join(";", t2.Scope.Classes) : "NEUTRAL";
                        break;
                }
            }
            // Basic class powers (plain card ids) and the bosses.
            foreach (var cls in data.Classes)
                foreach (string p in cls.Powers.Where(p => !DungeonRunContent.TryGet(p, out _)))
                {
                    var row = CustomRow(p, null, null); row.Roles.Add("hero power"); row.ValidationKind = "custom";
                    row.Notes.Add("basic power of class " + cls.Id);
                }
            foreach (var boss in data.Bosses)
            {
                if (boss.IsEncounter)
                {
                    // An imported encounter (phase 8): its hero, power and boss cards are the original ledger rows (never support
                    // rows); the container row names its provenance and pool; every deck card gets the boss-deck role.
                    var erow = CustomRow("run:boss:" + boss.Id, "BOSS", boss.Name);
                    erow.Container = true; erow.Roles.Add("boss"); erow.ValidationKind = "custom";
                    erow.Class = engine.TryGetValue(boss.HeroCard, out var ehc) ? ehc.Class.ToString() : null;
                    erow.Notes.Add($"{boss.Provenance} encounter, pool {boss.Pools[0]}; hero {boss.HeroCard}; power {boss.Power}; deck {boss.Deck} ({boss.Source?.Kind}, {boss.Source?.Url})"
                        + (boss.Forms.Count > 0 ? "; forms " + string.Join(", ", boss.Forms.Select(f => (f.Hero ?? "-") + "/" + (f.Power ?? "-"))) : "")
                        + (boss.Variants.Count > 0 ? "; variants " + string.Join(", ", boss.Variants.Select(v => v.Id + ":" + v.Shrine)) : "")
                        + (boss.Flags.Count > 0 ? "; flags " + string.Join(",", boss.Flags) : ""));
                    foreach (string deckId in EncounterDecks(boss))
                        if (data.BossDecks.TryGetValue(deckId, out var ecards))
                            // While no new run follows a schedule that draws the encounter, its deck cards are listed as cards of
                            // that deck (a neutral role), so a card the run player is already offered keeps its own reach.
                            foreach (string id in ecards.Distinct()) CardIn(id, EncounterReachable(boss) ? "boss deck" : "card", boss.Provenance + ":boss-deck:" + deckId);
                    continue;
                }
                var row = CustomRow("run:boss:" + boss.Id, "BOSS", boss.Name);
                row.Container = true; row.Roles.Add("boss"); row.ValidationKind = "custom";
                row.Class = engine.TryGetValue(boss.HeroCard, out var hc) ? hc.Class.ToString() : null;
                row.Notes.Add($"hero {boss.HeroCard}; power {boss.Power}; deck {boss.Deck}; " + (boss.Final ? "final" : "band " + boss.Band)
                    + (boss.Unlock != null ? "; unlock " + boss.Unlock : "") + (boss.Archetype != null ? "; " + boss.Archetype : ""));
                var hero = CustomRow(boss.HeroCard, null, null); hero.Roles.Add("boss hero"); MakeSupport(hero, "boss portrait", hero.Owner ?? row.Id);
                if (boss.Power != null && !DungeonRunContent.TryGet(boss.Power, out _))
                {
                    var pr = CustomRow(boss.Power, null, null); pr.Roles.Add("boss power"); pr.ValidationKind ??= "custom";
                }
                if (data.BossDecks.TryGetValue(boss.Deck, out var cards))
                    foreach (string id in cards.Distinct()) CardIn(id, "boss deck", "SoloStone:boss-deck:" + boss.Deck);
                if (data.BossSideboards.TryGetValue(boss.Deck, out var boards))
                    foreach (var kv in boards) foreach (string id in kv.Value.Distinct()) CardIn(id, "boss deck", $"SoloStone:boss-deck:{boss.Deck} sideboard of {kv.Key}");
            }
        }

        private static void Availability()
        {
            void Offer(string id, string what) { if (id != null && Rows.TryGetValue(id, out var r)) r.Player.Add(what); }
            void Use(string id, string what) { if (id != null && Rows.TryGetValue(id, out var r)) r.Encounter.Add(what); }
            var offerById = new Dictionary<string, OrderedSet>(StringComparer.Ordinal);
            void Mark(string id, string what) { if (!offerById.TryGetValue(id, out var s)) offerById[id] = s = new OrderedSet(); s.Add(what); }

            foreach (var cls in data.Classes)
            {
                // A named hero row (phase 6) is offered as a hero; its signature treasures are offered through its loadout.
                Offer("run:class:" + cls.Id, cls.IsHero ? "hero row" : "class");
                // A treasure's ledger row is its content shell (or run:treasure:<key> for a shell-less passive).
                string TreasureRow(string key) => DungeonRunContent.TryGet(key, out var tc) ? tc.ShellId ?? "run:treasure:" + key : key;
                foreach (string sig in cls.Signatures)
                {
                    Mark(TreasureRow(sig), "signature:" + cls.Id);
                    string sr = data.FindTreasure(sig)?.UpgradeUnlock != null ? data.FindTreasure(sig).UpgradesTo : null;
                    if (sr != null) Mark(TreasureRow(sr), "signature upgrade:" + cls.Id);
                }
                foreach (string p in cls.Powers) Mark(DungeonRunSetup.PowerCardId(p), "power:" + cls.Id);
                foreach (string d in cls.Decks)
                {
                    Offer("run:starter:" + d, "starter:" + cls.Id);
                    var deck = data.FindDeck(d);
                    if (deck != null) foreach (string id in deck.Cards) Mark(id, "starter");
                    if (deck?.DisplayCard != null) Offer(deck.DisplayCard, "generated starter:" + cls.Id);
                }
                Offer(cls.HeroCard, cls.IsHero ? "hero portrait:" + cls.Id : "class portrait");
            }
            foreach (var b in data.LegacyBundles) { Offer("run:bundle:" + b.Id, "bundle"); foreach (string id in b.Cards) Mark(id, "bundle"); }
            // Catalog pool entries a playable class row can be offered (a reprint-resolved member gets "via <id>" below).
            foreach (var e in data.Bundles.Where(b => b.IsPool && ReachableEntries.Contains(b.Id)))
                foreach (var c in e.Candidates) Mark(c.Id, "bucket " + e.Id);
            var offeredTreasures = new HashSet<string>(data.Treasures.Where(t => t.Offered && !t.Legacy).Select(t => t.Id));
            for (bool grew = true; grew;)
            {
                grew = false;
                foreach (var t in data.Treasures.Where(t => offeredTreasures.Contains(t.Id) && t.UpgradesTo != null))
                    if (offeredTreasures.Add(t.UpgradesTo)) grew = true;
            }
            foreach (string key in offeredTreasures)
                if (DungeonRunContent.TryGet(key, out var c)) Mark(c.ShellId ?? "run:treasure:" + key, data.Treasure(key).Offered ? "treasure" : "treasure-upgrade");
            // Phase 7: the board shrines a new run starts with (a scripted, admitted shrine named by a single-class row's starter
            // deck, on a scheduleVersion-2 default schedule: DungeonRun.BoardShrineFor), and what that run's teammate and
            // shrine-upgrade drafts can offer (DungeonRun.TeammatePool and ShrineUpgradePool: scripted and admitted).
            bool Admits(string id) => DungeonRunAdmission.Classify(null, id, AdmitScope.Player, null, true).Admitted;
            var defaultSchedule = schedules?.ScheduleVersion >= 2 ? schedules.Find(schedules.Default) : null;
            var shrineRows = new List<DungeonRunClass>();
            foreach (var cls in data.Classes.Where(c => c.ClassSet.Count == 1))
                foreach (var deck in cls.Decks.Select(data.FindDeck).Where(d => d?.Shrine != null))
                    if (defaultSchedule != null && SabberStoneCore.Actions.ShrineRules.IsScripted(deck.Shrine) && Admits(deck.Shrine))
                    {
                        Mark(deck.Shrine, "board shrine:" + deck.Id);
                        // The cards the shrine makes during a game (TRLA_129s Frostfire from TRLA_129), when scripted and admitted.
                        if (DungeonRunShrines.ShrineCards.TryGetValue(deck.Shrine, out var made))
                            foreach (string id in made.Where(i => engine.TryGetValue(i, out var mc) && mc.Implemented && Admits(i)))
                                Mark(id, "made by board shrine:" + deck.Shrine);
                        if (!shrineRows.Contains(cls)) shrineRows.Add(cls);
                    }
            if (defaultSchedule != null && defaultSchedule.Nodes.Any(n => n.TeammateDraft))
                foreach (var cls in shrineRows)
                    if (data.Teammates.TryGetValue(cls.ClassSet[0], out var mates))
                        foreach (string id in mates.Where(i => DungeonRunShrines.ScriptedTeammates.Contains(i) && engine.TryGetValue(i, out var tc) && tc.Implemented && Admits(i)))
                            Mark(id, "teammate draft:" + cls.ClassSet[0]);
            if (defaultSchedule != null && shrineRows.Count > 0 && defaultSchedule.Nodes.Any(n => n.ShrineUpgradeDraft))
                foreach (string id in data.ShrineUpgrades.Where(i => DungeonRunShrines.ScriptedUpgrades.ContainsKey(i) && Admits(i)))
                    Mark(id, "shrine-upgrade draft");
            foreach (var pair in offerById)
                if (Rows.TryGetValue(pair.Key, out var r)) foreach (string w in pair.Value) r.Player.Add(w);
            // Phase 8: the imported encounters and the global rules (schedule-v1.json "rules").
            // A new run follows schedule-v1.json "default" or one of "defaults": what only another schedule reaches is pending.
            var reachable = new HashSet<string>(new[] { schedules.Default }.Concat(schedules.Defaults), StringComparer.Ordinal);
            string Reached(DungeonRunBoss boss) => EncounterReachable(boss) ? "" : Row.Pending;
            foreach (var boss in data.Bosses.Where(b => b.IsEncounter))
            {
                string at = boss.Provenance + " " + boss.Pools[0] + Reached(boss);
                foreach (var form in boss.Forms)
                {
                    Use(form.Hero, "boss form " + at);
                    Use(form.Power, "boss form power " + at);
                }
                foreach (string deckId in EncounterDecks(boss).Skip(1))
                    if (data.BossDecks.TryGetValue(deckId, out var cards)) foreach (string id in cards.Distinct()) Use(id, "boss deck" + Reached(boss));
                foreach (string id in boss.Cards) Use(id, "boss card " + at);
                if (boss.Start != null) foreach (string id in boss.Start.BossCards.Concat(boss.Start.PlayerCards).Distinct()) Use(id, "encounter start " + at);
                foreach (var v in boss.Variants.Where(v => v.Shrine != null)) Use(v.Shrine, "boss shrine " + boss.Id + Reached(boss));
                foreach (var h in boss.HeroCards)
                {
                    Offer(h.Hero, "fight hero card:" + boss.Id + Reached(boss));
                    foreach (string id in h.Deck.Distinct()) Use(id, "fight hero card deck " + boss.Id + Reached(boss));
                }
                foreach (var h in boss.Relay) Offer(h.Hero, "relay hero:" + boss.Id + Reached(boss));
            }
            foreach (var schedule in schedules.Schedules.Values.OrderBy(x => x.Id, StringComparer.Ordinal))
            {
                foreach (var act in schedule.Acts.Where(a => a.IsFinale))
                    for (int k = 0; k < act.FixedBosses.Count; k++) Use("run:boss:" + act.FixedBosses[k], $"finale {schedule.Id} {k + 1}/{act.FixedBosses.Count}");
                if (!schedule.Acts.Any(a => a.AnomalyAtStart || a.TwistPool.Count > 0)) continue;
                foreach (var rule in schedules.Rules.Where(r => r.Admitted && (r.Effect != DungeonRunGlobalRule.EffectMatch || DungeonRunGlobalRules.IsScripted(r.Id))))
                {
                    string pending = reachable.Contains(schedule.Id) ? "" : Row.Pending;
                    if (rule.Kind == DungeonRunGlobalRule.KindAnomaly && schedule.Acts.Any(a => a.AnomalyAtStart)) Offer(rule.Id, "anomaly offer " + schedule.Id + pending);
                    foreach (var act in schedule.Acts.Where(a => a.TwistPool.Contains(rule.Id))) Offer(rule.Id, $"twist roll {schedule.Id} act {act.Id}" + pending);
                    string effect = DungeonRunGlobalRules.EffectOf(rule.Id);
                    if (effect != null) Use(effect, "global rule effect " + rule.Id + pending);
                }
            }
            foreach (var boss in data.Bosses)
            {
                string pending = boss.IsEncounter ? Reached(boss) : "";
                Use("run:boss:" + boss.Id, (boss.IsEncounter ? boss.Provenance + " " + boss.Pools[0] : boss.Final ? "final boss" : "band " + boss.Band) + pending);
                Use(boss.HeroCard, "boss hero" + pending);
                if (boss.Power != null)
                {
                    string shell = DungeonRunSetup.PowerCardId(boss.Power);
                    Use(shell, "boss power" + pending);
                    if (DungeonRunContent.TryGet(boss.Power, out var c) && c.EffectKey != null) Use(DungeonRunContent.Get(c.EffectKey).ShellId, "boss passive effect");
                }
                if (data.BossDecks.TryGetValue(boss.Deck, out var cards)) foreach (string id in cards.Distinct()) Use(id, "boss deck" + pending);
                if (data.BossSideboards.TryGetValue(boss.Deck, out var boards)) foreach (var kv in boards) foreach (string id in kv.Value.Distinct()) Use(id, "boss sideboard");
            }
            // Original rows reached through a reprint or the same basic power under another id.
            foreach (var row in Rows.Values.Where(r => !r.PlayerOffered && r.Alternates.Count > 0))
                foreach (string alt in row.Alternates)
                    if (offerById.TryGetValue(alt, out var s)) foreach (string w in s) row.Player.Add(w + " via " + alt);
            foreach (var row in Rows.Values.Where(r => !r.EncounterUsed && r.Alternates.Count > 0))
                foreach (string alt in row.Alternates)
                    if (Rows.TryGetValue(alt, out var ar) && ar.Encounter.Count > 0) foreach (string w in ar.Encounter) row.Encounter.Add(w + " via " + alt);
            // Supporting effect shells of an offered passive treasure are reachable with it.
            foreach (var content in DungeonRunContent.All.Where(c => c.EffectKey != null))
            {
                string owner = content.ShellId ?? "run:treasure:" + content.Key;
                if (Rows.TryGetValue(owner, out var o) && o.PlayerOffered) Offer(DungeonRunContent.Get(content.EffectKey).ShellId, "effect of " + content.Key);
            }
            // A card an offered active or passive treasure makes in the match and no other source offers (phase 6 F2: Quel'Delar,
            // which LOOTA_842a and LOOTA_842b form in hand; F15: the Secret ULDA_045t that ULDA_045 puts into play) is reachable with it.
            foreach (var content in DungeonRunContent.All.Where(c => (c.Kind == RunContentKind.ActiveTreasure || c.Kind == RunContentKind.PassiveTreasure)
                && c.ShellId != null && c.Tokens.Count > 0))
                if (Rows.TryGetValue(content.ShellId, out var o) && o.PlayerOffered)
                    foreach (string token in content.Tokens)
                        if (Rows.TryGetValue(token, out var t) && !t.PlayerOffered) Offer(token, "token of " + content.Key);
            // A hero power an offered treasure gives in the match and no class row starts with (phase 6 F11: Jr. and Sr. Tomb
            // Diver's Battlecry makes the hero power ULDA_039 Uldum Treasure Cache) is reachable with it.
            foreach (var pair in PowersGivenByTreasures)
            {
                if (!Rows.TryGetValue(pair.Key, out var power) || power.PlayerOffered) continue;
                foreach (string giver in pair.Value)
                    if (Rows.TryGetValue(giver, out var o) && o.PlayerOffered) Offer(pair.Key, "power of " + giver);
            }
            // The Choose One options of an offered hero power (ULDA_Elise_HP1 Elise's Might: Defensive Posture, Offensive
            // Posture) are what that power plays, so they are reachable with it.
            foreach (var content in DungeonRunContent.All)
                if (ChooseOneParent(content.Key) is string parent && Rows.TryGetValue(parent, out var o) && o.PlayerOffered
                    && Rows.TryGetValue(content.ShellId ?? content.Key, out var option) && !option.PlayerOffered)
                    Offer(content.ShellId ?? content.Key, "option of " + parent);
        }

        /// <summary>
        /// Hero powers a treasure's script gives the run player in the match, by power: the treasures that give it (phase 6 F11:
        /// ULDA_016 Jr. and ULDA_021 Sr. Tomb Diver, whose Battlecry makes the hero power ULDA_039 Uldum Treasure Cache).
        /// </summary>
        private static readonly Dictionary<string, string[]> PowersGivenByTreasures = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            [DungeonRunContent.UldumTreasureCache] = new[] { "ULDA_016", "ULDA_021" },
        };

        /// <summary>
        /// The run hero power whose Choose One option <paramref name="key"/> is (its card id plus "a" or "b", as the engine's
        /// Choose One cards pair them), or null: a content power whose card has Choose One.
        /// </summary>
        private static string ChooseOneParent(string key)
        {
            if (key == null || key.Length < 2 || (key[key.Length - 1] != 'a' && key[key.Length - 1] != 'b')) return null;
            string parent = key.Substring(0, key.Length - 1);
            if (!DungeonRunContent.TryGet(parent, out var power) || power.Kind != RunContentKind.HeroPower) return null;
            SabberStoneCore.Model.Card card;
            try { card = SabberStoneCore.Model.Cards.FromId(power.ShellId ?? parent); } catch { return null; }
            return card != null && card.ChooseOne ? power.ShellId ?? parent : null;
        }

        // ====================================================================== Bob's tavern (phase 5)

        private static DungeonRunTavernTable tavern;
        /// <summary>engine_behavior of a tavern option: the run layer runs it between fights (DungeonRunTavernNode), no engine script.</summary>
        private const string RunService = "run-service";
        /// <summary>Tavern options a tavern node offers (weight above 0, or an alias of one), by id: their op.</summary>
        private static readonly Dictionary<string, string> TavernOps = new Dictionary<string, string>(StringComparer.Ordinal);
        /// <summary>
        /// The tavern-adjacent rows phase 6 still owns: none. The match-to-run deck edit DALA_721 landed with family F7 (producer
        /// RunHelpers.EditReplaceSlot into RunMatchResult, applied by DungeonRun.ApplyMatchResult); the tavern upgrade ULDA_011 VIP
        /// Membership landed with family F13 (a run-layer flag DungeonRunTavernNode reads: DungeonRun.HasVip).
        /// </summary>
        private static readonly string[] Phase6DeckEdits = { };
        private const string Phase6DeckEditsNote = "deferred: phase 6 (match-to-run deck edits / tavern upgrade); producer RunHelpers -> RunMatchResult, applied by DungeonRun.ApplyMatchResult";
        /// <summary>The deck-edit rows phase 6 deferred to phase 9 (a turn restart, a match exit: design section 8).</summary>
        private static readonly string[] Phase9DeckEdits = { "GILA_911", "ULDA_111" };
        private const string Phase9DeckEditsNote = "deferred: phase 9 (turn restart / match exit, decided in phase 6); the ApplyMatchEdits remove path is ready for it";

        /// <summary>
        /// The display cards of the generated starters (phase 6: the Heist DALA_800-808 and Tombs ULDA_900-903 "Random" decks), by
        /// id: the deck that shows it. The card is never a match card: DungeonRun.GenerateStarter builds the deck at NewRun from
        /// the shared draft catalog, so it is a run service, validated as its generated deck is (DungeonRunValidator).
        /// </summary>
        private static readonly Dictionary<string, string> GeneratedDisplayCards = new Dictionary<string, string>(StringComparer.Ordinal);

        private static void GeneratedStarterRows()
        {
            GeneratedDisplayCards.Clear();
            foreach (var deck in data.Decks.Where(d => d.IsGenerated && d.DisplayCard != null && data.Classes.Any(c => c.Decks.Contains(d.Id))))
            {
                if (!Rows.TryGetValue(deck.DisplayCard, out var row)) continue;
                GeneratedDisplayCards[deck.DisplayCard] = deck.Id;
                row.ValidationKind = "generated";
                row.Notes.Add($"the display card of the generated starter {deck.Id} ({deck.Generator}): never a match card; DungeonRun.GenerateStarter "
                    + $"draws {data.Rules.StarterSize} admitted cards of the run's class from the shared draft catalog at NewRun, seeded by the run seed");
            }
            // A disabled hero row's generated starter (phase 6 F9: the Tombs ULDA_900, 902 and 903 ship with their disabled rows, which
            // the validator checks whole): the same run service, never offered until the row is enabled.
            DisabledGeneratedDecks.Clear();
            foreach (var deck in data.DisabledDecks.Where(d => d.IsGenerated && d.DisplayCard != null))
            {
                var owner = data.DisabledClasses.FirstOrDefault(c => c.Decks.Contains(deck.Id));
                if (owner == null || !Rows.TryGetValue(deck.DisplayCard, out var row) || GeneratedDisplayCards.ContainsKey(deck.DisplayCard)) continue;
                GeneratedDisplayCards[deck.DisplayCard] = deck.Id;
                DisabledGeneratedDecks[deck.Id] = owner.Id;
                row.ValidationKind = "generated";
                row.Notes.Add($"the display card of the generated starter {deck.Id} ({deck.Generator}) of the disabled row {owner.Id}: never a match card; "
                    + $"the run layer builds it as every generated starter, but it is never offered while the row ships disabled ({owner.DisabledReason})");
            }
        }

        /// <summary>A disabled hero row's generated starters, by deck id: the row that owns it.</summary>
        private static readonly Dictionary<string, string> DisabledGeneratedDecks = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>
        /// tavern-v1.json: each option with weight above 0, or an alias of one, is offered at a tavern node (a run service,
        /// validated by DungeonRunValidator.ValidateTavern); each enchantment an option names is a tavern deck enchantment
        /// (DungeonRunContent TavernEnchantments); a deferred card keeps its reason; the phase-6 deck-edit rows say so.
        /// </summary>
        private static void TavernRows()
        {
            TavernOps.Clear();
            foreach (var o in tavern.Options)
            {
                bool offered = o.Weight > 0 || o.AliasOf != null && tavern.Option(o.AliasOf)?.Weight > 0;
                if (!offered || !Rows.TryGetValue(o.Id, out var row)) continue;
                TavernOps[o.Id] = o.Op;
                row.Player.Add("tavern tavern-v1.json " + o.Op);
                row.ValidationKind = "tavern";
                row.Notes.Add("tavern option (tavern-v1.json " + o.Op + ")" + (o.AliasOf != null ? "; aliasOf " + o.AliasOf : ""));
            }
            foreach (string ench in tavern.Options.Where(o => o.Ench != null && TavernOps.ContainsKey(o.Id)).Select(o => o.Ench).Distinct())
                if (Rows.TryGetValue(ench, out var row))
                {
                    row.Notes.Add("tavern deck enchantment (tavern-v1.json; DungeonRunContent TavernEnchantments)");
                    // Owned by the tavern options that apply it (the content entry alone would name the run scripts).
                    row.Owner = string.Join(",", tavern.Options.Where(o => o.Ench == ench && TavernOps.ContainsKey(o.Id)).Select(o => o.Id));
                }
            foreach (var d in tavern.Deferred)
                if (Rows.TryGetValue(d.Key, out var row)) row.Notes.Add("deferred: " + d.Value);
            foreach (string id in Phase6DeckEdits)
                if (Rows.TryGetValue(id, out var row)) row.Notes.Add(Phase6DeckEditsNote);
            foreach (string id in Phase9DeckEdits)
                if (Rows.TryGetValue(id, out var row)) row.Notes.Add(Phase9DeckEditsNote);
        }

        // ====================================================================== validation

        /// <summary>The validator's check text, shortened to a stable rule name.</summary>
        private static string Rule(string check)
        {
            string c = Regex.Replace(check, @" \((?:[^()]|\([^()]*\))*\)$", "");
            if (c.StartsWith("not in the card data")) return "not-in-engine";
            if (c.StartsWith("no client entity definition")) return "no-client-entity";
            if (c.StartsWith("not scripted")) return "not-scripted";
            if (c.StartsWith("not in the active meta")) return "not-in-active-meta";
            if (c.StartsWith("changes deck size")) return "deck-size";
            if (c.StartsWith("needs deckbuilding selections")) return "deckbuilding-selection";
            if (c.StartsWith("can change who goes first")) return "seat-order";
            var admission = Regex.Match(c, @"^admission (\S+):");
            if (admission.Success) return "admission:" + admission.Groups[1].Value;
            if (c.StartsWith("class ")) return "class";
            if (c.StartsWith("has a card with a rune cost")) return "rune-cost-in-bundle";
            if (c.StartsWith("is a choose-one power")) return "choose-one-power";
            if (c.StartsWith("is a passive power")) return "passive-power";
            var type = Regex.Match(c, @"^is (\w+), not a (hero power|hero|clickable hero power|minion)");
            if (type.Success) return "wrong-type:" + type.Groups[1].Value;
            return Regex.Replace(c.ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
        }

        private static Dictionary<string, OrderedSet> ErrorsById(IEnumerable<string> errors, IEnumerable<string> ignore, Func<string, string> mapKey)
        {
            var skip = new HashSet<string>(ignore);
            var map = new Dictionary<string, OrderedSet>(StringComparer.Ordinal);
            foreach (string e in errors)
            {
                if (skip.Contains(e)) continue;
                int i = e.IndexOf(": ", StringComparison.Ordinal);
                if (i < 0) continue;
                string key = mapKey(e.Substring(0, i));
                if (key == null) continue;
                string rule = Rule(e.Substring(i + 2));
                if (rule == "rune-cost-in-bundle") { RuneCost.Add(key); continue; }   // bundle-only rule: a starter may hold it
                if (!map.TryGetValue(key, out var s)) map[key] = s = new OrderedSet();
                s.Add(rule);
            }
            return map;
        }

        private static DungeonRunData Synthetic()
        {
            var s = new DungeonRunData { DataVersion = "ledger", Rules = data.Rules };
            return s;
        }

        private static void Validation(Func<string, bool> clientKnows)
        {
            var emptyErrors = DungeonRunValidator.Validate(Synthetic(), clientKnows);

            // Current data as shipped: the ledger states it, and any error names the rows it concerns.
            var baseline = DungeonRunValidator.Validate(data, clientKnows);
            Console.WriteLine($"current data validation errors: {baseline.Count}");
            foreach (string e in baseline) Console.WriteLine("  " + e);
            var baseById = ErrorsById(baseline, Array.Empty<string>(), k =>
                k.StartsWith("class ") ? "run:class:" + k.Substring(6) : k.StartsWith("deck ") ? "run:starter:" + k.Substring(5)
                : k.StartsWith("boss ") ? "run:boss:" + k.Substring(5) : data.Bundles.Any(b => b.Id == k) ? "run:bundle:" + k : k);
            BaselineErrors = baseline;

            foreach (var row in Rows.Values)
            {
                if (row.ValidationKind != null) continue;
                if (row.Support) { row.ValidationKind = "na:support record"; continue; }
                if (row.HeroUnit || row.Roles.Contains("hero")) { row.ValidationKind = "hero"; continue; }
                // An original bucket member the catalog recorded: its admission (the run layer's own predicate for the entry's
                // class set) or its recorded exclusion, HERO-type members included.
                if (row.Roles.Contains("bucket member") && CatalogFacts.ContainsKey(row.Id)) { row.ValidationKind = "catalog"; continue; }
                if (row.Type == "HERO_POWER") { row.ValidationKind = row.Roles.Contains("boss power") && !row.Roles.Contains("hero power") ? "boss-power" : "player-power"; continue; }
                string role = row.Roles.FirstOrDefault(r => r != "custom");
                switch (role)
                {
                    case "passive treasure": row.ValidationKind = "na:passive treasures are admitted as run content entries; this one has none"; break;
                    case "shrine": case "shrine upgrade": row.ValidationKind = "na:board shrines and shrine upgrades are checked by DungeonRunValidator.ValidateShrines (phase 7)"; break;
                    case "anomaly": case "twist":
                        // Phase 8: a rule the schedule lists is validated as one (DungeonRunValidator.ValidateRules).
                        row.ValidationKind = schedules.Rule(row.Id) != null ? "rule" : "na:not in schedule-v1.json \"rules\" yet (its family scripts and admits it)";
                        break;
                    case "tavern": row.ValidationKind = "na:no admission rule for tavern services yet"; break;
                    case "boss card": row.ValidationKind = "na:boss-deck admission needs an original boss deck"; break;
                    default:
                        row.ValidationKind = row.Type == "MINION" || row.Type == "SPELL" || row.Type == "WEAPON" || row.Type == "LOCATION" ? "pool" : "na:type " + row.Type;
                        break;
                }
            }

            // Pool-card admission: one three-copy bundle per card id (and each reprint), in the card's own class.
            var poolIds = Rows.Values.Where(r => r.ValidationKind == "pool").SelectMany(r => new[] { r.Id }.Concat(r.Alternates))
                .Where(engine.ContainsKey).Distinct().ToList();
            var pool = Synthetic();
            foreach (string id in poolIds)
            {
                var card = engine[id];
                pool.Bundles.Add(new DungeonRunBundle { Id = "ledger|" + id, Class = card.Class == CardClass.NEUTRAL ? "NEUTRAL" : card.Class.ToString(), Theme = "ledger", Cards = new List<string> { id, id, id } });
            }
            var poolErrors = ErrorsById(DungeonRunValidator.Validate(pool, clientKnows), emptyErrors, k => k.StartsWith("ledger|") ? k.Substring(7) : k);

            // Player powers: one single-power class each (class-level errors are ignored, power errors are keyed by id).
            var powers = Synthetic();
            foreach (var row in Rows.Values.Where(r => r.ValidationKind == "player-power" && r.Card != null))
                powers.Classes.Add(new DungeonRunClass { Id = "MAGE", Name = "ledger", HeroCard = "HERO_08", Powers = new List<string> { row.Id } });
            var powerErrors = ErrorsById(DungeonRunValidator.Validate(powers, clientKnows), emptyErrors, k => k.StartsWith("class ") ? null : k);
            // Phase 8: the global rules of schedule-v1.json, validated against the loaded bosses.
            var ruleErrors = DungeonRunValidator.ValidateRules(new DungeonRunData { Schedules = schedules, Bosses = data.Bosses });

            // Boss powers and heroes: one boss each, on a real boss deck.
            var bosses = Synthetic();
            foreach (var pair in data.BossDecks) bosses.BossDecks[pair.Key] = pair.Value;
            foreach (var pair in data.BossSideboards) bosses.BossSideboards[pair.Key] = pair.Value;
            var anyBoss = data.Bosses.First();
            foreach (var row in Rows.Values.Where(r => (r.ValidationKind == "boss-power" || r.ValidationKind == "hero") && r.Card != null))
                bosses.Bosses.Add(new DungeonRunBoss
                {
                    Id = "ledger|" + row.Id, Name = "ledger", Band = 1, Archetype = "midrange", Deck = anyBoss.Deck,
                    HeroCard = row.ValidationKind == "hero" ? row.Id : anyBoss.HeroCard, Power = row.ValidationKind == "boss-power" ? row.Id : null,
                });
            var bossErrors = ErrorsById(DungeonRunValidator.Validate(bosses, clientKnows), emptyErrors.Concat(DungeonRunValidator.Validate(new Func<DungeonRunData>(() =>
            {
                var b = Synthetic(); foreach (var pair in data.BossDecks) b.BossDecks[pair.Key] = pair.Value; foreach (var pair in data.BossSideboards) b.BossSideboards[pair.Key] = pair.Value; return b;
            })(), clientKnows)), k => k.StartsWith("boss ") ? null : k);

            // Player heroes (adventure heroes and explorers): also as a run class row's portrait, which the validator requires to
            // be scripted (an unscripted adventure hero keeps its adventure stats), so the ledger never reports passes:hero for a
            // portrait a run row cannot use (ULDA_Reno: the MAGE_ROGUE row uses HERO_08). Class-level errors are ignored.
            var portraits = Synthetic();
            foreach (var row in Rows.Values.Where(r => r.ValidationKind == "hero" && r.Card != null && r.Card.Type == CardType.HERO && !r.Roles.Contains("boss hero")
                && r.Card.Class != CardClass.NEUTRAL && r.Card.Class != CardClass.INVALID))
                portraits.Classes.Add(new DungeonRunClass { Id = row.Card.Class.ToString(), Name = "ledger", HeroCard = row.Id, Powers = new List<string>() });
            var portraitErrors = ErrorsById(DungeonRunValidator.Validate(portraits, clientKnows), emptyErrors, k => k.StartsWith("class ") ? null : k);

            string Verdict(Dictionary<string, OrderedSet> map, string id, string context) =>
                !engine.ContainsKey(id) ? "rejected:not-in-engine" : map.TryGetValue(id, out var s) ? "rejected:" + string.Join(",", s) : "passes" + context;

            foreach (var row in Rows.Values)
            {
                string kind = row.ValidationKind;
                if (kind == "custom")
                {
                    var keys = new[] { row.Id }.Concat(row.Notes.Select(n => Regex.Match(n, @"^content (\S+)")).Where(m => m.Success).Select(m => m.Groups[1].Value));
                    var errs = keys.Where(baseById.ContainsKey).SelectMany(k => baseById[k]).Distinct().ToList();
                    row.Validation = errs.Count == 0 ? "passes:current data" : "rejected:" + string.Join(",", errs);
                }
                else if (kind.StartsWith("na:")) row.Validation = "not applicable:" + kind.Substring(3);
                else if (kind == "tavern") row.Validation = "passes:tavern-v1.json " + TavernOps[row.Id];
                else if (kind == "generated")
                {
                    string deck = GeneratedDisplayCards[row.Id];
                    if (DisabledGeneratedDecks.TryGetValue(deck, out string disabledOwner))
                        row.Validation = "rejected:disabled-row " + disabledOwner;
                    else row.Validation = baseById.TryGetValue("run:starter:" + deck, out var deckErrors)
                        ? "rejected:" + string.Join(",", deckErrors) : "passes:generated starter " + deck;
                }
                else if (kind == "catalog")
                {
                    var fact = CatalogFacts[row.Id];
                    row.Validation = fact.Admitted.Count > 0 ? "passes:catalog" + (fact.Via != null ? " via " + fact.Via : "")
                        : "rejected:" + string.Join(",", fact.Excluded.SelectMany(r => r.Split(new[] { "; " }, StringSplitOptions.RemoveEmptyEntries)).Select(CatalogReasonRule).Distinct());
                    if (fact.Admitted.Count > 0 && fact.Excluded.Count > 0) row.Notes.Add("catalog: excluded elsewhere as " + string.Join(", ", fact.Excluded));
                }
                else if (kind == "pool")
                {
                    string v = CatalogLifted(row, Verdict(poolErrors, row.Id, ""));
                    if (!v.StartsWith("passes"))
                        foreach (string alt in row.Alternates.Where(engine.ContainsKey))
                            if (!poolErrors.ContainsKey(alt)) { row.Notes.Add($"{row.Id} itself: {v}"); v = "passes:via " + alt; break; }
                    // Only on a boss deck: the boss checks, not the pool checks, admit it (and the current data passes).
                    if (!row.PlayerOffered && row.EncounterUsed && !baseById.ContainsKey(row.Id))
                    { row.Notes.Add("as a pool card: " + v); v = "passes:boss deck"; }
                    if (RuneCost.Contains(row.Id)) row.Notes.Add("has a Death Knight rune cost: a starter card, never a bundle card (validator bundle rule)");
                    row.Validation = v;
                }
                else if (kind == "player-power")
                {
                    string v = Verdict(powerErrors, row.Id, ":player power");
                    if (!v.StartsWith("passes"))
                        foreach (string alt in row.Alternates.Where(engine.ContainsKey))
                            if (!powerErrors.ContainsKey(alt) && Rows.TryGetValue(alt, out var ar) && ar.PlayerOffered) { row.Notes.Add($"{row.Id} itself: {v}"); v = "passes:via " + alt; break; }
                    row.Validation = v;
                }
                else if (kind == "boss-power") row.Validation = Verdict(bossErrors, row.Id, ":boss power");
                else if (kind == "rule")
                {
                    var rule = schedules.Rule(row.Id);
                    var errs = ruleErrors.Where(e => e.StartsWith("rule " + row.Id + ":", StringComparison.Ordinal)).Select(e => e.Substring(e.IndexOf(':') + 2)).ToList();
                    row.Validation = !rule.Admitted ? "rejected:rule-rejected" : errs.Count == 0 ? "passes:global rule" : "rejected:" + string.Join(",", errs);
                    if (!rule.Admitted) row.Notes.Add("rule rejected: " + rule.Reason);
                }
                else if (kind == "hero")
                    row.Validation = portraitErrors.TryGetValue(row.Id, out var portrait) && engine.ContainsKey(row.Id)
                        ? "rejected:" + string.Join(",", portrait.Concat(bossErrors.TryGetValue(row.Id, out var bs) ? bs : Enumerable.Empty<string>()).Distinct())
                        : Verdict(bossErrors, row.Id, ":hero");
                if (baseById.TryGetValue(row.Id, out var cur) && kind != "custom") row.Notes.Add("current data error: " + string.Join(",", cur));
            }
            // Phase 8: a card an imported encounter uses is validated on its real encounter row (the whole data, as a catalog run
            // sees it), not on the synthetic band-1 boss: with no error of its own there it passes as that encounter's.
            var encounterErrorIds = new HashSet<string>(DungeonRunValidator.Validate(data, clientKnows).Select(e => e.Substring(0, Math.Max(0, e.IndexOf(':')))), StringComparer.Ordinal);
            foreach (var boss in data.Bosses.Where(b => b.IsEncounter))
            {
                var members = new List<string> { boss.HeroCard, DungeonRunSetup.PowerCardId(boss.Power) };
                members.AddRange(boss.Forms.SelectMany(f => new[] { f.Hero, f.Power }));
                members.AddRange(boss.Cards);
                members.AddRange(boss.Variants.Select(v => v.Shrine));
                members.AddRange(boss.HeroCards.Select(h => h.Hero));
                foreach (string deckId in EncounterDecks(boss)) if (data.BossDecks.TryGetValue(deckId, out var cards)) members.AddRange(cards);
                foreach (string id in members.Where(m => m != null).Distinct())
                {
                    if (!Rows.TryGetValue(id, out var row) || row.Validation.StartsWith("passes", StringComparison.Ordinal)) continue;
                    if (encounterErrorIds.Contains(id) || encounterErrorIds.Contains("boss " + boss.Id)) continue;
                    row.Notes.Add("outside its encounter: " + row.Validation);
                    row.Validation = "passes:encounter " + boss.Id;
                }
            }
            // An encounter (or encounter card) the encounter file lists as not imported: rejected with its stated reason.
            foreach (var rejected in data.RejectedEncounters)
                foreach (string id in new[] { rejected.Id }.Concat(rejected.Cards))
                    if (Rows.TryGetValue(id, out var row))
                    {
                        if (!row.Validation.StartsWith("rejected:", StringComparison.Ordinal)) row.Validation = "rejected:encounter-rejected";
                        row.Notes.Add("encounter rejected: " + rejected.Reason + "; lifted by " + rejected.Lift);
                    }
        }

        /// <summary>
        /// A new run can meet the encounter: a schedule a new run follows (schedule-v1.json "default" or one of "defaults") draws
        /// its pool or fixes it in its finale.
        /// </summary>
        private static bool EncounterReachable(DungeonRunBoss boss) =>
            schedules.Schedules.Values.Any(x => (x.Id == schedules.Default || schedules.Defaults.Contains(x.Id))
                && x.Acts.Any(a => a.FixedBosses.Contains(boss.Id) || a.MajorPool != null && schedules.Member(boss, a.MajorPool) || a.Combat.Any(c => schedules.Member(boss, c.Pool))));

        /// <summary>The deck ids of an encounter: its own first, then its forms' and its shrine variants', distinct.</summary>
        private static List<string> EncounterDecks(DungeonRunBoss boss) =>
            new[] { boss.Deck }.Concat(boss.Forms.Select(f => f.Deck)).Concat(boss.Variants.Select(v => v.Deck)).Where(d => d != null).Distinct().ToList();

        private static List<string> BaselineErrors = new List<string>();

        /// <summary>
        /// A pool row's validation as a catalog run sees it: the synthetic bundles validate without the admission table's catalog
        /// lifts (they are legacy fixed bundles), so an admission:&lt;family&gt; rejection the lift admits for the row's scope is
        /// dropped, as ai_support (also a catalog run) states. Every new run is a catalog run.
        /// </summary>
        private static string CatalogLifted(Row row, string verdict)
        {
            if (!verdict.StartsWith("rejected:", StringComparison.Ordinal) || row.Card == null) return verdict;
            var kept = verdict.Substring(9).Split(',').Where(r =>
            {
                if (!r.StartsWith("admission:", StringComparison.Ordinal)) return true;
                var v = DungeonRunAdmission.Classify(row.Card, row.Id, Scope(row), null, catalogRun: true);
                return !(v.Family == r.Substring(10) && v.CatalogLifted && v.AdmittedSome);
            }).ToList();
            return kept.Count == 0 ? "passes" : "rejected:" + string.Join(",", kept);
        }
        /// <summary>Tombs of Terror explorer card id (ULDA_Reno) -> its class pair (MAGE_ROGUE); its primary class -> the pair.</summary>
        private static readonly Dictionary<string, (string Owner, string Note)> EncounterPowers = new Dictionary<string, (string Owner, string Note)>(StringComparer.Ordinal);
        private static readonly Dictionary<string, string> ExplorerPairs = new Dictionary<string, string>(StringComparer.Ordinal), PrimaryPairs = new Dictionary<string, string>(StringComparer.Ordinal);
        /// <summary>run:class rows of the catalog class rows that ship disabled, with their disabledReason.</summary>
        private static readonly Dictionary<string, string> DisabledClassRows = new Dictionary<string, string>(StringComparer.Ordinal);
        /// <summary>The disabled rows of <see cref="DisabledClassRows"/> that are dual-class (Tombs pairs): also blocked by run:dual-class.</summary>
        private static readonly HashSet<string> DisabledDualRows = new HashSet<string>(StringComparer.Ordinal);
        private static string RowClasses(DungeonRunClass cls) => string.Join("/", cls.ClassSet);

        /// <summary>
        /// The Tombs of Terror class pairs a row belongs to: the explorer its id, a starter deck or a signature treasure names
        /// (ULDA_Reno, ULDA_Reno_HP1, "ULDA_Reno_Deck2", "signature treasure for MAGE"). Empty when none is named.
        /// </summary>
        private static SortedSet<string> TombsPairs(Row row)
        {
            var pairs = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var p in ExplorerPairs)
                if (row.Id == p.Key || row.Id.StartsWith(p.Key + "_", StringComparison.Ordinal) || row.Decks.Any(d => d.Contains(":" + p.Key + "_"))) pairs.Add(p.Value);
            foreach (string note in row.Notes)
            {
                var m = Regex.Match(note, @"^signature treasure for ([A-Z]+)");
                if (m.Success && PrimaryPairs.TryGetValue(m.Groups[1].Value, out string pair)) pairs.Add(pair);
            }
            return pairs;
        }

        // The validator rule name of a catalog exclusion reason (Blockers maps not-scripted to engine:unscripted, admission:X to ai:X).
        private static string CatalogReasonRule(string reason) =>
            reason == "engine:unscripted" ? "not-scripted" : reason == "engine:missing" ? "not-in-engine"
            : reason.StartsWith("validator:", StringComparison.Ordinal) ? reason.Substring(10) : reason;

        /// <summary>
        /// Reads catalog-v1.json: per card id, the pool entries admitting it (and which a playable class row reaches), the
        /// recorded exclusions, and a reprint resolution. Reachable = DungeonRunBundle.EligibleFor for some row of data.Classes,
        /// with DungeonRunAdmission.MechanicHolds (the predicate DungeonRun.OpenCatalogDraft uses).
        /// </summary>
        private static void LoadCatalogFacts(string catalogText)
        {
            foreach (var cls in data.Classes)
            {
                bool secret = DungeonRunAdmission.MechanicHolds(data, cls.ClassSet, "secret");
                foreach (var e in data.Bundles.Where(b => b.IsPool && b.EligibleFor(cls.ClassSet, m => m == "secret" && secret))) ReachableEntries.Add(e.Id);
            }
            var playablePairs = new HashSet<string>(data.Classes.Where(c => c.ClassSet.Count == 2).Select(c => string.Join("_", c.ClassSet)), StringComparer.Ordinal);
            CatalogFact Fact(string id) { if (!CatalogFacts.TryGetValue(id, out var f)) CatalogFacts[id] = f = new CatalogFact(); return f; }
            using var doc = JsonDocument.Parse(catalogText);
            foreach (var e in doc.RootElement.GetProperty("entries").EnumerateArray())
            {
                if (e.GetProperty("draw").GetString() != "pool") continue;
                string entry = e.GetProperty("id").GetString();
                bool offerable = !e.TryGetProperty("offerable", out var off) || off.GetBoolean();
                var pair = e.TryGetProperty("requires", out var req) && req.TryGetProperty("classesAll", out var all)
                    ? string.Join("_", all.EnumerateArray().Select(x => x.GetString())) : null;
                foreach (var m in e.GetProperty("members").EnumerateArray())
                {
                    string id = m.GetProperty("id").GetString(), wiki = m.TryGetProperty("wikiId", out var w) ? w.GetString() : id;
                    foreach (string key in new[] { wiki, id }.Distinct())
                    {
                        var f = Fact(key);
                        if (!offerable) continue;
                        f.Admitted.Add(entry);
                        if (ReachableEntries.Contains(entry)) f.Reachable.Add(entry);
                        else if (pair != null && !playablePairs.Contains(pair)) f.DualBlocked.Add(entry);
                        if (key == wiki && id != wiki) f.Via = id;
                    }
                }
                if (e.TryGetProperty("excluded", out var ex))
                    foreach (var x in ex.EnumerateArray())
                    {
                        string id = x.GetProperty("id").GetString(), wiki = x.TryGetProperty("wikiId", out var w) ? w.GetString() : id;
                        foreach (string key in new[] { wiki, id }.Distinct()) Fact(key).Excluded.Add(x.GetProperty("reason").GetString());
                    }
            }
        }

        /// <summary>The delta against the ledger on disk before this run: partial rows and each blocked_by token, before and after.</summary>
        private static void PrintDelta(string oldCsv, List<Row> rows)
        {
            if (!File.Exists(oldCsv)) return;
            var old = ParseCsv(new UTF8Encoding(false).GetString(File.ReadAllBytes(oldCsv)).TrimStart('\uFEFF'));
            int Partial(IEnumerable<string> classes) => classes.Count(c => c == "partial");
            var before = new SortedDictionary<string, int>(StringComparer.Ordinal); var after = new SortedDictionary<string, int>(StringComparer.Ordinal);
            void Count(SortedDictionary<string, int> map, string cell) { foreach (string t in (cell ?? "").Split(';').Where(t => t.Length > 0)) map[t] = map.TryGetValue(t, out int n) ? n + 1 : 1; }
            foreach (var r in old) Count(before, r.TryGetValue("blocked_by", out var b) ? b : "");
            foreach (var r in rows) Count(after, r.BlockedBy.ToString());
            Console.WriteLine($"ledger delta: rows {old.Count} -> {rows.Count}; partial {Partial(old.Select(r => r.TryGetValue("classification", out var c) ? c : ""))} -> {Partial(rows.Select(r => r.Classification))}"
                + $"; implemented {old.Count(r => r.TryGetValue("classification", out var c) && c == "implemented")} -> {rows.Count(r => r.Classification == "implemented")}");
            foreach (string token in before.Keys.Union(after.Keys).OrderBy(t => t, StringComparer.Ordinal))
            {
                int b = before.TryGetValue(token, out int x) ? x : 0, a = after.TryGetValue(token, out int y) ? y : 0;
                if (a != b) Console.WriteLine($"ledger delta: blocked_by {token}: {b} -> {a}");
            }
        }
        private static readonly HashSet<string> RuneCost = new HashSet<string>(StringComparer.Ordinal);

        // ====================================================================== status columns

        private static string EngineBehavior(Row row)
        {
            if (row.Support) return "support-only";
            if (row.Container) return "not-a-card";
            if (row.Type == "PASSIVE_TREASURE") return "scripted";   // run content entry, applied by DungeonRunSetup
            if (TavernOps.ContainsKey(row.Id)) return RunService;   // a tavern option: the run layer runs it (DungeonRunTavernNode)
            if (GeneratedDisplayCards.ContainsKey(row.Id)) return RunService;   // a Random deck's card: the run layer builds the deck
            // Phase 8 family 06: a run-scope global rule (schedule-v1.json effect "run") has no match script: the run layer carries it.
            if (schedules?.Rule(row.Id) is DungeonRunGlobalRule runRule && runRule.Admitted && runRule.Effect == DungeonRunGlobalRule.EffectRun) return RunService;
            string Own(EngineCard card, string text)
            {
                // A non-collectible adventure hero card is a portrait: its text is the encounter description shown on the boss
                // card ("Chomper survives the sewers..."), not rules text. Only a hero with rules text (DALA_BOSS_06dk
                // "Battlecry: Equip...") needs a script.
                if (!card.Implemented)
                    return row.Type == "HERO" && (string.IsNullOrWhiteSpace(text) || (!card.Collectible && !HeroRulesText.IsMatch(text)))
                        ? "vanilla-no-script-needed" : "unscripted";
                return string.IsNullOrWhiteSpace(text) && card.Power == null ? "vanilla-no-script-needed" : "scripted";
            }
            string own = row.Card == null ? "unscripted" : Own(row.Card, row.Text);
            if (own == "unscripted")
                foreach (string alt in row.Alternates)
                    if (engine.TryGetValue(alt, out var ac) && ac.Implemented)
                    {
                        row.Notes.Add($"{row.Id} itself: {(row.Card == null ? "not in the engine card data" : "unscripted")}; behaves as {alt}");
                        return Own(ac, Plain(ac.Text));
                    }
            if (row.Card == null) row.Notes.Add("not in the engine card data");
            return own;
        }

        private static readonly Regex HeroRulesText = new Regex(@"^(Battlecry|Deathrattle)\s*:|^Passive\b|\bHero Power\b", RegexOptions.CultureInvariant);

        /// <summary>
        /// ai_support from the admission table (plugin/DungeonRun/DungeonRunAdmission.cs), the same table the validator and
        /// the engine's generation block use. The scope is the row's roles: player roles ask Player, encounter roles Boss,
        /// neutral-only rows (an adventure card, a token) both.
        /// </summary>
        private static void Ai(Row row)
        {
            if (row.Support) { row.Ai = "not applicable"; return; }
            if (row.Container)
            {
                var members = row.Notes.SelectMany(n => Regex.Matches(n, @"[A-Z][A-Za-z0-9_]+").Cast<Match>().Select(m => m.Value))
                    .Where(Rows.ContainsKey).Select(id => Rows[id]).ToList();
                foreach (var m in members) { Ai(m); foreach (string b in m.AiBlocks) row.AiBlocks.Add(b); }
                row.Ai = row.AiBlocks.Count == 0 ? "ok" : "blocked:" + string.Join(",", row.AiBlocks);
                return;
            }
            bool behaves = row.Engine == "scripted" || row.Engine == "vanilla-no-script-needed" || row.Engine == RunService;
            // An adventure hero portrait's text is its encounter description: no phrase in it is a rule the bot plays
            // (DALA_BOSS_21h "What's the secret to knowing all secrets?").
            bool portrait = row.Type == "HERO" && row.Engine == "vanilla-no-script-needed";
            // Every new run is a catalog run, so the table's catalog lifts apply (a legacy run only continues an older save).
            var v = portrait ? new AdmissionVerdict() : DungeonRunAdmission.Classify(row.Card, row.Id, Scope(row), row.Card == null ? row.Text : null, catalogRun: true);
            if (row.Id == DungeonRunValidator.AyaId) row.AiBlocks.Add("seat-order");
            if (v.Subject && !v.AdmittedSome) row.AiBlocks.Add(v.Family);
            if (row.AiBlocks.Count > 0) { row.Ai = "blocked:" + string.Join(",", row.AiBlocks) + (v.Subject && !v.AdmittedSome ? " (" + v.Lift + ")" : ""); return; }
            if (!v.Subject) { row.Ai = behaves ? "ok" : "unknown:unscripted"; return; }
            AiFamilies.Add(row.Id);
            if (!v.Admitted)
            {
                string other = (v.Asked & ~v.Admit & AdmitScope.Both) == AdmitScope.Boss ? "boss" : "player";
                row.Ai = $"ok:{v.Family} ({(other == "boss" ? "player" : "boss")} only; {other} after {v.Lift})";
                return;
            }
            // Admitted for the row's own roles; say so when the rule still rejects the other side with pending work.
            var missing = ~v.Admit & AdmitScope.Both;
            row.Ai = "ok:" + v.Family + (missing != AdmitScope.None && missing != AdmitScope.Both && !DungeonRunAdmission.IsNoneLift(v.Lift)
                ? $" ({(missing == AdmitScope.Boss ? "player" : "boss")} only; {(missing == AdmitScope.Boss ? "boss" : "player")} after {v.Lift})" : "");
        }

        /// <summary>Rows the admission table admitted (ai_support ok:&lt;family&gt;).</summary>
        private static readonly HashSet<string> AiFamilies = new HashSet<string>(StringComparer.Ordinal);

        private static string AdmitText(AdmitScope a) =>
            a == AdmitScope.None ? "none" : string.Join(", ", new[] { AdmitScope.Player, AdmitScope.Boss, AdmitScope.Generated }.Where(x => (a & x) != 0).Select(x => x.ToString().ToLowerInvariant()));

        /// <summary>The admission scope of a row's roles (EncounterRoles, NeutralRoles); neutral-only rows ask both.</summary>
        private static AdmitScope Scope(Row row)
        {
            // Phase 8: a global rule (an anomaly or a twist) is admitted as a rule, never as a player or boss card.
            if (row.Roles.Contains("anomaly") || row.Roles.Contains("twist")) return AdmitScope.Rule;
            bool player = row.Roles.Any(r => !EncounterRoles.Contains(r) && !NeutralRoles.Contains(r));
            bool encounter = row.Roles.Any(EncounterRoles.Contains);
            var scope = (player ? AdmitScope.Player : AdmitScope.None) | (encounter ? AdmitScope.Boss : AdmitScope.None);
            return scope == AdmitScope.None ? AdmitScope.Both : scope;
        }

        private static void Classify(Row row)
        {
            var gaps = new List<string>();
            if (row.Support) { row.Classification = "duplicate/support"; row.Gaps = ""; return; }
            bool reach = Reach(row);
            bool behaves = row.HeroUnit ? reach : row.Engine == "scripted" || row.Engine == "vanilla-no-script-needed" || row.Engine == "not-a-card" || row.Engine == RunService;
            bool ai = row.Ai.StartsWith("ok", StringComparison.Ordinal);
            bool valid = row.Validation.StartsWith("passes") || row.Validation.StartsWith("not applicable");
            if (!behaves) gaps.Add("engine");
            if (!reach) gaps.Add("reach");
            if (!ai) gaps.Add("ai");
            if (!valid) gaps.Add("validation");
            row.Gaps = string.Join(",", gaps);
            if (gaps.Count == 0) row.Classification = "implemented";
            else if (row.AdaptFit == "exact") row.Classification = "intentionally adapted";
            else if (row.AdaptFit == "partial") row.Classification = "partial";
            else if (!behaves && !reach) row.Classification = "missing";
            else row.Classification = "partial";
        }

        private static readonly HashSet<string> EncounterRoles = new HashSet<string> { "boss hero", "final boss", "boss power", "boss card", "boss deck", "boss" };
        private static readonly HashSet<string> NeutralRoles = new HashSet<string> { "custom", "card", "token", "enchantment", "portrait", "support", "bucket label" };

        /// <summary>
        /// Reachable in the mode, for every kind of role the row has: a player role (bucket member, starter, hero, power,
        /// treasure, shrine, teammate, global rule, tavern) needs a current offer, an encounter role a current boss using it.
        /// A row with only neutral roles (an adventure card, a token) is reached either way.
        /// </summary>
        private static bool Reach(Row row)
        {
            bool player = row.Roles.Any(r => !EncounterRoles.Contains(r) && !NeutralRoles.Contains(r));
            bool encounter = row.Roles.Any(EncounterRoles.Contains);
            if (!player && !encounter) return row.PlayerReached || row.EncounterReached;
            return (!player || row.PlayerReached) && (!encounter || row.EncounterReached);
        }

        private static readonly Dictionary<string, string> RoleCapability = new Dictionary<string, string>
        {
            ["bucket member"] = "run:bucket-pools", ["starter"] = "run:adventure-loadouts", ["finale deck"] = "run:special-finales",
            ["hero"] = "run:adventure-heroes", ["hero power"] = "run:adventure-heroes", ["treasure"] = "run:treasure-content",
            ["passive treasure"] = "run:treasure-content", ["signature treasure"] = "run:signature-treasures",
            ["shrine"] = "run:shrine-lifecycle", ["shrine upgrade"] = "run:shrine-lifecycle", ["teammate"] = "run:teammate-draft",
            ["anomaly"] = "run:global-rules", ["twist"] = "run:global-rules", ["tavern"] = "run:tavern",
            ["boss hero"] = "run:encounter-roster", ["final boss"] = "run:encounter-roster", ["boss power"] = "run:encounter-roster", ["boss card"] = "run:encounter-roster",
            ["card"] = "run:adventure-cards",
        };

        private static void Blockers(Row row, string[] mutationPhrases)
        {
            if (row.Support || row.Classification == "implemented") return;
            if (DisabledClassRows.TryGetValue(row.Id, out string disabledReason))
            {
                // A disabled dual row waits on dual-class eligibility too; a disabled single-class hero row only on its reason.
                if (DisabledDualRows.Contains(row.Id)) row.BlockedBy.Add("run:dual-class");
                int colon = disabledReason.IndexOf(": ", StringComparison.Ordinal);
                if (colon > 0) row.BlockedBy.Add(disabledReason.Substring(0, colon));
            }
            if (!row.HeroUnit && !(row.Engine == "scripted" || row.Engine == "vanilla-no-script-needed" || row.Engine == "not-a-card" || row.Engine == RunService)) row.BlockedBy.Add("engine:unscripted");
            foreach (string b in row.AiBlocks) row.BlockedBy.Add("ai:" + b);
            // A validator rejection is listed under the capability that causes it (not-scripted is the engine gap, an admission
            // family and the deck-size and deckbuilding checks are AI gaps); any other rule is its own validation gap.
            if (row.Validation.StartsWith("rejected:"))
                foreach (string r in row.Validation.Substring(9).Split(','))
                    row.BlockedBy.Add(r == "not-scripted" || r == "not-in-engine" ? "engine:unscripted"
                        : r.StartsWith("admission:") ? "ai:" + r.Substring(10)
                        : r == "deck-size" ? "ai:deck-size" : r == "deckbuilding-selection" ? "ai:deckbuilding"
                        : r == "seat-order" ? "ai:seat-order" : r == "unscripted-portrait" ? "run:adventure-heroes" : "validation:" + r);
            bool reach = Reach(row);
            if (!reach && !row.Custom)
            {
                CatalogFacts.TryGetValue(row.Id, out var fact);
                foreach (string role in row.Roles)
                {
                    // A bucket member the catalog recorded has its own reason: an exclusion (in the validation column), or an
                    // admitting entry no playable class row reaches (a Tombs pair whose dual row ships disabled).
                    if (role == "bucket member" && fact != null) continue;
                    if (RoleCapability.TryGetValue(role, out string cap)) row.BlockedBy.Add(cap);
                }
                if (fact != null && fact.Admitted.Count > 0 && fact.Reachable.Count == 0)
                {
                    if (fact.DualBlocked.Count > 0) { row.BlockedBy.Add("run:dual-class"); row.BlockedBy.Add("run:adventure-loadouts"); }
                    if (fact.DualBlocked.Count < fact.Admitted.Count) row.BlockedBy.Add("run:bucket-pools");
                }
                // Dual-class eligibility blocks an explorer's hero, power, signature treasure or starter only while none of its
                // pairs has a playable row (MAGE_ROGUE ships; a row naming no explorer stays blocked).
                if (row.Adventures.Contains("Tombs of Terror") && row.Roles.Any(r => r == "hero" || r == "hero power" || r == "signature treasure" || r == "starter"))
                {
                    var pairs = TombsPairs(row);
                    if (pairs.Count == 0 || !pairs.Any(p => data.Classes.Any(c => c.ClassSet.Count == 2 && string.Join("_", c.ClassSet) == p)))
                        row.BlockedBy.Add("run:dual-class");
                }
                if (row.Roles.Contains("boss hero")) row.BlockedBy.Add("data:original-boss-decks-absent");
                if (row.SpecialFinale) row.BlockedBy.Add("run:special-finales");
                // Phase 8: scripted, admitted and placed on acts-v3h / acts-v3t, which no new run follows until "defaults" names them.
                if (row.PendingReach && !Reach(row)) row.BlockedBy.Add("run:default-schedule");
            }
            // A finale record's "this run" is the campaign length (The Finale twist), not a change to the run deck; a hero
            // portrait's text is its encounter description.
            string text = row.Type == "HERO" && row.Engine == "vanilla-no-script-needed" ? "" : DungeonRunValidator.MatchText(row.Text);
            if (!row.SpecialFinale && mutationPhrases.Any(text.Contains)) row.BlockedBy.Add("run:deck-mutation");
            // Phase 8: a row an imported encounter uses (or a rule a version-3 schedule offers) is in the roster with its deck:
            // the roster, global-rule and finale capabilities and the missing-deck marker no longer block it. What is left is
            // its own gap, or run:default-schedule while no new run follows that schedule.
            if (row.Encounter.Count > 0 || row.Player.Any(w => w.StartsWith("anomaly offer", StringComparison.Ordinal) || w.StartsWith("twist roll", StringComparison.Ordinal)
                || w.StartsWith("fight hero card", StringComparison.Ordinal) || w.StartsWith("relay hero", StringComparison.Ordinal)))
                foreach (string cap in new[] { "run:encounter-roster", "run:global-rules", "run:special-finales", "data:original-boss-decks-absent" }) row.BlockedBy.Remove(cap);
        }

        // ====================================================================== output

        private static string Plain(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            string t = Regex.Replace(text, "<[^>]*>", "").Replace("[x]", "").Replace('_', ' ').Replace('&', ' ').Replace("\\n", " ");
            t = Regex.Replace(t, @"[$#](\d)", "$1");
            int at = t.IndexOf('@');   // "@spelldmg" and alternate-text tails
            if (at >= 0) t = t.Substring(0, at);
            return Regex.Replace(t, @"\s+", " ").Trim();
        }

        private static string Q(string s)
        {
            s ??= "";
            return s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
        }

        private static IEnumerable<string> AdvSorted(OrderedSet s) =>
            s.OrderBy(a => Array.IndexOf(AdventureOrder, a) is int i && i >= 0 ? i : 99).ThenBy(a => a, StringComparer.Ordinal);

        private static readonly string[] Columns =
        {
            "card_id", "name", "type", "class", "collectible", "adventures", "roles", "buckets", "decks", "classification", "gaps",
            "engine_behavior", "player_availability", "encounter_use", "ai_support", "validation", "adapted_by", "owner", "blocked_by",
            "source", "source_category", "text", "notes",
        };

        private static string Csv(List<Row> rows)
        {
            var sb = new StringBuilder();
            sb.Append(string.Join(",", Columns)).Append('\n');
            foreach (var r in rows)
            {
                string[] v =
                {
                    r.Id, r.Name, r.Type, r.Class, r.Collectible == null ? "" : r.Collectible.Value ? "true" : "false",
                    string.Join(";", AdvSorted(r.Adventures)), r.Roles.ToString(), r.Buckets.ToString(), r.Decks.ToString(), r.Classification, r.Gaps,
                    r.Engine, r.PlayerOffered ? "offered:" + string.Join(",", r.Player) : "not offered",
                    r.EncounterUsed ? "used:" + string.Join(",", r.Encounter) : "none", r.Ai, r.Validation, r.AdaptedBy, r.Owner,
                    r.BlockedBy.ToString(), r.Source, r.Category, r.Text,
                    string.Join(" | ", r.Notes.Concat(r.Support && r.SupportWhy != null ? new[] { "support: " + r.SupportWhy } : Array.Empty<string>())),
                };
                sb.Append(string.Join(",", v.Select(Q))).Append('\n');
            }
            return sb.ToString();
        }

        private static string Table(IEnumerable<string> header, IEnumerable<IEnumerable<string>> body, int textColumns = 1)
        {
            var sb = new StringBuilder();
            var h = header.ToList();
            sb.Append("| ").Append(string.Join(" | ", h)).Append(" |\n");
            var lines = body.Select(l => l.ToList()).ToList();
            bool Numeric(int i) => i >= textColumns && lines.All(l => i >= l.Count || Regex.IsMatch((l[i] ?? "").Trim('*'), @"^[0-9,]*$"));
            sb.Append("|").Append(string.Join("|", h.Select((x, i) => Numeric(i) ? " ---: " : " --- "))).Append("|\n");
            foreach (var line in lines) sb.Append("| ").Append(string.Join(" | ", line.Select(c => (c ?? "").Replace("|", "\\|")))).Append(" |\n");
            return sb.ToString();
        }

        private static string Markdown(List<Row> rows, int csvRecords, int labelRowsAdded,
            List<(string adv, string key, string labelId, string membership, List<string> members)> bucketStats, bool client, string outCsv, string repo)
        {
            var sb = new StringBuilder();
            string N(int n) => n.ToString("N0", CultureInfo.InvariantCulture);
            var advs = AdventureOrder.ToList();
            var roles = rows.SelectMany(r => r.Roles).Where(r => r != "custom").GroupBy(r => r).OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal).Select(g => g.Key).ToList();

            sb.Append("# Restoration Ledger\n\n");
            sb.Append("> **Generated file.** Do not edit by hand: run the generator in `tools/restoration-ledger/` (see *How the ledger is updated*). ")
              .Append("Hand-curated facts live in `tools/restoration-ledger/curation.json`.\n\n");
            sb.Append("One ledger for the unified Dungeon Run (gap review 2026-11-10, implementation step 1). Every original adventure-id record, ")
              .Append("every card of an original bucket, starter or finale deck, the original loadout heroes, powers and signature treasures, and all of ")
              .Append("SoloStone's own run content, each classified once and tracked on five separate status columns. The rows are in ")
              .Append("[`restoration-ledger.csv`](restoration-ledger.csv).\n\n");

            // ---------------------------------------------------------- totals
            int fromCsv = rows.Count(r => r.Source == "reference-csv");
            int imported = rows.Count(r => r.Source != "reference-csv" && r.Adventures.Any(a => a != "SoloStone"));
            int shared = rows.Count(r => r.Source != "reference-csv" && r.Adventures.Any(a => a != "SoloStone") && r.Adventures.Contains("SoloStone"));
            int customOnly = rows.Count(r => r.Adventures.All(a => a == "SoloStone"));
            sb.Append("## Counts\n\n");
            sb.Append(Table(new[] { "Rows", "Count" }, new[]
            {
                new[] { "Reference-CSV records (original adventure ids)", N(fromCsv) },
                new[] { "Original bucket / starter / finale-deck / loadout cards and labels not in the CSV", N(imported) },
                new[] { "Of those, original cards also in SoloStone's current pool", N(shared) },
                new[] { "SoloStone-only rows (run catalog entries and current pool cards)", N(customOnly) },
                new[] { "**Total rows**", "**" + N(rows.Count) + "**" },
            }));
            sb.Append($"\nThe reference CSV has {N(csvRecords)} records and every one is a row. {labelRowsAdded} bucket labels without a CSV record ")
              .Append("(Monster Hunt's Darius and Toki labels) were added as `label:` rows. A card that has several provenances is one row ")
              .Append("whose `adventures`, `roles`, `buckets` and `decks` columns list all of them.\n\n");

            sb.Append("### By classification\n\n");
            sb.Append(Table(new[] { "Classification", "Rows", "Original (non-SoloStone) rows", "SoloStone rows" },
                Classifications.Select(c => new[] { c, N(rows.Count(r => r.Classification == c)),
                    N(rows.Count(r => r.Classification == c && r.Adventures.Any(a => a != "SoloStone"))),
                    N(rows.Count(r => r.Classification == c && r.Adventures.Contains("SoloStone"))) })
                .Concat(new[] { new[] { "**Total**", N(rows.Count), N(rows.Count(r => r.Adventures.Any(a => a != "SoloStone"))), N(rows.Count(r => r.Adventures.Contains("SoloStone"))) } })));
            sb.Append("\nA row with several adventures counts once in each column it belongs to, so the last two columns can overlap.\n\n");

            sb.Append("### Classification by adventure\n\n");
            sb.Append(Table(new[] { "Adventure" }.Concat(Classifications).Concat(new[] { "Rows" }),
                advs.Select(a => new[] { a }.Concat(Classifications.Select(c => N(rows.Count(r => r.Classification == c && r.Adventures.Contains(a)))))
                    .Concat(new[] { N(rows.Count(r => r.Adventures.Contains(a))) }))));
            sb.Append("\n### Classification by role\n\n");
            sb.Append(Table(new[] { "Role" }.Concat(Classifications).Concat(new[] { "Rows" }),
                roles.Select(role => new[] { role }.Concat(Classifications.Select(c => N(rows.Count(r => r.Classification == c && r.Roles.Contains(role)))))
                    .Concat(new[] { N(rows.Count(r => r.Roles.Contains(role))) }))));
            sb.Append("\nA row is counted under every role it has (a card can be a bucket member and a starter card).\n\n");

            sb.Append("### Classification by adventure and role\n\n");
            var cube = new List<string[]>();
            foreach (string a in advs)
                foreach (string role in roles)
                {
                    var set = rows.Where(r => r.Has(a, role)).ToList();
                    if (set.Count == 0) continue;
                    cube.Add(new[] { a, role }.Concat(Classifications.Select(c => { int n = set.Count(r => r.Classification == c); return n == 0 ? "" : N(n); }))
                        .Concat(new[] { N(set.Count) }).ToArray());
                }
            sb.Append(Table(new[] { "Adventure", "Role" }.Concat(Classifications).Concat(new[] { "Rows" }), cube, 2));
            sb.Append("\nEmpty cells are zero. A row is counted under each adventure-role pair it carries (a role is paired with the adventure that gave it).\n\n");

            sb.Append("### What keeps partial rows partial\n\n");
            sb.Append("`gaps` names the criteria a row fails (engine, reach, ai, validation; see *Classification rules*).\n\n");
            sb.Append(Table(new[] { "gaps", "partial rows", "of which original (non-SoloStone)" },
                rows.Where(r => r.Classification == "partial").GroupBy(r => r.Gaps).OrderByDescending(g => g.Count())
                    .Select(g => new[] { g.Key == "" ? "(analogue only)" : g.Key, N(g.Count()), N(g.Count(r => r.Adventures.Any(a => a != "SoloStone"))) })));
            int engineReadyCollectible = rows.Count(r => r.Classification == "partial" && r.Gaps == "reach" && r.Collectible == true);
            sb.Append($"\n{N(engineReadyCollectible)} partial rows are collectible cards that only need a place in the draft catalog (engine, AI and admission already pass).\n\n");

            // ---------------------------------------------------------- blockers
            sb.Append("## Blocking capability gaps\n\n");
            sb.Append("Each non-implemented, non-support row lists in `blocked_by` every gap that stands between it and *implemented*. ")
              .Append("A row can carry several, so the counts overlap; *only blocker* counts rows for which this is the single remaining gap.\n\n");
            var described = new Dictionary<string, string>
            {
                ["ai:seat-order"] = "Seat-changing cards (the run player always goes first).",
                ["engine:unscripted"] = "No engine script for the card id (CardDefs has no implemented entry).",
                ["run:bucket-pools"] = "Original bucket pools as randomized, repeatable three-card draft offers. In since phase 3 (data/dungeon-run/catalog-v1.json, catalog runs): a bucket member the catalog records is offered (`bucket <entry>`) or blocked by its own recorded reason; the gap stays only on a member whose admitting entries no playable class row reaches for another reason.",
                ["run:adventure-loadouts"] = "Original starter decks (Heist, Tombs, Rumble Run shrine decks) as selectable loadouts.",
                ["run:adventure-heroes"] = "The 18 adventure heroes and their special player powers.",
                ["run:dual-class"] = "Dual-class explorers: class eligibility for heroes, starters and signature treasures (Tombs of Terror). Phase 3 ships the dual-class rows; a Tombs bucket member admitted only in entries of a pair whose row ships disabled (no original starter passes the pool checks) is blocked here and under run:adventure-loadouts, as are the disabled rows themselves (run:class:<pair>, with their disabledReason) and an explorer's hero, power, signature treasure or starter while none of its pairs has a playable row.",
                ["run:treasure-content"] = "Original passive and active treasures as run content entries (DungeonRunContent + run-v1.json).",
                ["run:signature-treasures"] = "Selectable signature treasures and their junior/senior upgrades.",
                ["run:shrine-lifecycle"] = "Rumble Run board shrines: initial board entity, dormancy, revival after three turns, shrine upgrades, shrine starter decks.",
                ["run:teammate-draft"] = "Rumble Run teammates as draft rewards.",
                ["run:global-rules"] = "Anomalies and chapter twists as validated global rules on the match plan.",
                ["run:tavern"] = "Bob's tavern services between fights (deck edits, recruits).",
                ["run:encounter-roster"] = "Original encounters: boss heroes, powers, special cards and setups in the encounter pool.",
                ["run:special-finales"] = "Special finales (Tekahn Inner Sanctum hand-off, Hagatha, Heist finale).",
                ["run:default-schedule"] = "Placed on the version-3 schedules (acts-v3h, acts-v3t), which a new run follows only once schedule-v1.json \"defaults\" names them (the phase-8 close-out, after the encounter families fill the pools and enable the finales).",
                ["run:adventure-cards"] = "Adventure-only cards (boss/treasure support cards in the original namespaces) that need an owning feature to appear.",
                ["run:deck-mutation"] = "Persistent run-deck mutation and run-scoped state (\"Dungeon Deck\", \"this run\", bosses defeated).",
                ["data:original-boss-decks-absent"] = "Original boss decks are server-side: the client database has none, so an encounter needs a reconstructed deck.",
                ["ai:unclassified"] = "A Secret or deck-dependent card with no admission rule: add a rule to `plugin/DungeonRun/DungeonRunAdmission.cs`.",
            };
            // ai:<family> gaps are described by the admission table itself (plugin/DungeonRun/DungeonRunAdmission.cs).
            foreach (var family in DungeonRunAdmission.Rules.GroupBy(r => r.Family))
            {
                var rejecting = family.Where(r => (r.Admit & AdmitScope.Both) != AdmitScope.Both && !DungeonRunAdmission.IsNoneLift(r.Lift)).ToList();
                if (rejecting.Count == 0) continue;
                described["ai:" + family.Key] = "Admission family `" + family.Key + "`: " + string.Join(" ", rejecting.Select(r =>
                    $"{r.Reason} (admitted: {AdmitText(r.Admit)}; lifted by {r.Lift}{(r.Ids == null ? "" : "; " + string.Join(", ", r.Ids))})."));
            }
            var gapRows = rows.Where(r => r.BlockedBy.Count > 0).ToList();
            var gapKeys = gapRows.SelectMany(r => r.BlockedBy).Distinct().OrderBy(g => g.StartsWith("ai:") ? 0 : g.StartsWith("engine:") ? 1 : g.StartsWith("validation:") ? 2 : g.StartsWith("run:") ? 3 : 4)
                .ThenByDescending(g => gapRows.Count(r => r.BlockedBy.Contains(g))).ThenBy(g => g, StringComparer.Ordinal).ToList();
            sb.Append(Table(new[] { "Gap", "Rows blocked", "Only blocker", "Collectible rows", "What it needs" },
                gapKeys.Select(g => new[]
                {
                    "`" + g + "`", N(gapRows.Count(r => r.BlockedBy.Contains(g))), N(gapRows.Count(r => r.BlockedBy.Count == 1 && r.BlockedBy.Contains(g))),
                    N(gapRows.Count(r => r.BlockedBy.Contains(g) && r.Collectible == true)),
                    described.TryGetValue(g, out string d) ? d : g.StartsWith("validation:") ? "Current DungeonRunValidator admission rule `" + g.Substring(11) + "` rejects the card." : "",
                })));
            sb.Append($"\n{N(gapRows.Count)} rows carry at least one gap.\n\n");

            sb.Append("### AI support\n\n");
            sb.Append(Table(new[] { "ai_support", "Rows" }, rows.GroupBy(r => r.Ai).OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal).Select(g => new[] { g.Key, N(g.Count()) })));
            sb.Append("\n### Admission families\n\n");
            sb.Append("`ai_support` for Secrets and deck-dependent cards comes from the admission table in `plugin/DungeonRun/DungeonRunAdmission.cs`, the same table the validator applies to pool cards, player and legacy powers, active treasures and their tokens, passive treasures and their effects, run content shells (shrines included), boss decks and boss powers, and the engine applies to generated cards over the whole card pool (`RunMatchPlan.ExcludedGenerated` for both sides, `ExcludedGeneratedBoss` for the boss's generation only). ")
              .Append("A card is a subject when it is a Secret, has tag 997 or a sideboard, has a Secret or deck phrase in its text, or is listed; a subject with no rule is rejected as `unclassified`. Scopes: player (run player cards), boss (boss decks and powers), generated (Discover and random cards in a run match). A lift names the numbered goal phase that owns the work; a lift starting `none:` names no pending work: nothing in the five adventures needs that scope. A rule with a pending requirement admits no scope until its lift.\n\n");
            sb.Append(Table(new[] { "Family", "Admitted", "Reason", "Lifted by", "Requires", "Ledger rows" }, textColumns: 5, body:
                DungeonRunAdmission.Rules.Select(r => new[]
                {
                    "`" + r.Family + "`", AdmitText(r.Admit), r.Reason, r.Lift, r.Requires ?? "",
                    r.Ids == null ? "family default" : N(r.Ids.Count(Rows.ContainsKey)) + " of " + N(r.Ids.Length),
                })));
            sb.Append("\n### Validation (current DungeonRunValidator admission)\n\n");
            sb.Append(client
                ? "Client entity checks ran against the CARD table in `data/dungeon-run/reference/client-tables.json`, on the id NativeMatch.TransportCardId sends (its fixed map is read from `plugin/NativeMatch.cs`).\n\n"
                : "Client entity checks did **not** run (no client-tables.json, or `--no-client`).\n\n");
            var ruleCounts = rows.Where(r => r.Validation.StartsWith("rejected:")).SelectMany(r => r.Validation.Substring(9).Split(','))
                .GroupBy(x => x).OrderByDescending(g => g.Count()).Select(g => new[] { "rejected: `" + g.Key + "`", N(g.Count()) });
            sb.Append(Table(new[] { "Validation", "Rows" },
                new[]
                {
                    new[] { "passes (all forms)", N(rows.Count(r => r.Validation.StartsWith("passes"))) },
                    new[] { "rejected (one rule or more)", N(rows.Count(r => r.Validation.StartsWith("rejected"))) },
                    new[] { "not applicable", N(rows.Count(r => r.Validation.StartsWith("not applicable"))) },
                }.Concat(ruleCounts)));
            sb.Append($"\nCurrent run data as shipped: {(BaselineErrors.Count == 0 ? "**0 validator errors**" : $"**{BaselineErrors.Count} validator errors**")}.\n\n");

            sb.Append("### Data gaps in the reference inputs\n\n");
            int unknown = bucketStats.Count(b => b.membership != "published");
            sb.Append($"- **Original boss decks: 0 rows.** The client database has no boss decks or boss rotation for these five adventures (server-side). ")
              .Append($"Boss heroes, powers and boss cards are rows; their decks are not, and `data:original-boss-decks-absent` marks every boss hero.\n");
            sb.Append($"- **Bucket memberships:** {bucketStats.Count} labels, {bucketStats.Count - unknown} with published members, {unknown} unknown ")
              .Append($"({string.Join(", ", bucketStats.Where(b => b.membership != "published").Select(b => b.key))}). Members are the wiki tables as of 2026-11-10 and can include later-patch cards.\n");
            sb.Append("- **Dungeon Run, Monster Hunt, Rumble Run loadouts:** no starter decks, powers or treasures in the client tables; their powers come from `curation.json`, Rumble Run's ten-card decks are inferred to be shrine starter decks from their names.\n");
            sb.Append("- **Dalaran Heist treasures:** no loadout table; treasure roles come from the gap review's named-treasure list in `curation.json`.\n\n");

            // ---------------------------------------------------------- definitions
            sb.Append("## Columns\n\n");
            sb.Append(Table(new[] { "Column", "Meaning" }, textColumns: 2, body: new[]
            {
                new[] { "card_id", "Engine/client card id; `run:<kind>:<key>` for a SoloStone catalog entry without its own card (class, starter, bundle, boss, shell-less passive treasure); `label:<adventure>:<hero>:<label>` for a bucket label with no record." },
                new[] { "name, type, class, collectible", "From the loaded engine; from the reference CSV or run data when the engine has no card. A run shell shows SoloStone's run name." },
                new[] { "adventures", "Every provenance, `;`-joined: the five adventures and `SoloStone` (current run data)." },
                new[] { "roles", "bucket member, bucket label, starter, finale deck, hero, hero power, boss hero, final boss, boss power, boss card, treasure, passive treasure, signature treasure, shrine, shrine form, shrine upgrade, teammate, anomaly, twist, tavern, token, enchantment, portrait, support, card; SoloStone rows add custom, class, bundle, bundle member, boss, boss deck." },
                new[] { "buckets", "`adventure:hero:label` per original bucket; `SoloStone:<bundle id>` per current bundle." },
                new[] { "decks", "Original starter/finale decks (`adventure:DECK_NOTE (name)`) and current starter and boss decks (`SoloStone:...`)." },
                new[] { "classification", "implemented, partial, missing, duplicate/support, intentionally adapted (rules below)." },
                new[] { "gaps", "Which of the four criteria fail: engine, reach, ai, validation." },
                new[] { "engine_behavior", "scripted (engine Implemented flag with text or a power); vanilla-no-script-needed (implemented, no text and no power; a hero portrait with no text, or a non-collectible adventure hero whose text is only its encounter description); unscripted; run-service (a tavern option the run layer runs between fights, tavern-v1.json); support-only (support rows); not-a-card (catalog containers)." },
                new[] { "player_availability", "`offered:` starter / bundle / bucket <catalog entry> / power:<class> / treasure / treasure-upgrade / class / effect of <treasure> / token of <treasure> / power of <treasure> / option of <power>, with `via <id>` when a reprint or same-named basic power is what is offered; else `not offered`." },
                new[] { "encounter_use", "`used:` boss deck / boss sideboard / boss power / boss hero / boss passive effect / band n / final boss, in a current boss (run-v1.json); else `none`." },
                new[] { "ai_support", "ok; ok:<family> (admitted by the admission table; `(player only; boss after <lift>)` when the other side waits on a step); blocked:<family> (<lift>) or blocked:seat-order; unknown:unscripted; not applicable. Families: see *Admission families*." },
                new[] { "validation", "passes (pool admission as a three-copy bundle in the card's class; `:player power`, `:boss power`, `:hero`; `:via <reprint>`; `:boss deck`; `:current data` for SoloStone entries; `:catalog` for an original bucket member the shared draft catalog admits, with the catalog's recorded exclusion as rejected:<rules> otherwise) / rejected:<rules> / not applicable:<why>." },
                new[] { "adapted_by", "The deliberate SoloStone analogue and how it differs (`curation.json` analogues)." },
                new[] { "owner", "For support rows: the record it depends on (portrait to hero, enchantment to card, label to its members)." },
                new[] { "blocked_by", "Capability gaps blocking the row (table above)." },
                new[] { "source, source_category", "Which input created the row; the reference CSV category." },
            }));

            sb.Append("\n## Classification rules\n\n");
            sb.Append("Four criteria, each from its own column:\n\n")
              .Append("- **engine**: `engine_behavior` is scripted or vanilla-no-script-needed (catalog containers count as behaving). For heroes (playable or boss) the portrait is not the content: the criterion holds only when a current loadout or encounter uses it.\n")
              .Append("- **reach**: for every kind of role the row has. A player role (bucket member, starter, finale deck, hero, hero power, treasures, shrine, teammate, anomaly, twist, tavern) needs `player_availability` offered; an encounter role (boss hero, boss power, boss card, boss deck) needs `encounter_use` used. An original bucket card that only appears in a SoloStone boss deck is therefore not reached. Rows with only neutral roles (adventure card, token) need either.\n")
              .Append("- **ai**: `ai_support` is ok or ok:<family> (the admission table admits the card for the row's roles; an unscripted admitted card keeps its engine gap).\n")
              .Append("- **validation**: `validation` passes or is not applicable.\n\n")
              .Append("Then, in order:\n\n")
              .Append("1. **duplicate/support**: portraits and variants, Heroic-mode power variants, enchantments, dummies, tokens and shrine board forms, bucket labels, legacy save-only content and run effects. Dependencies, not rewards; `owner` links the owning record.\n")
              .Append("2. **implemented**: all four criteria hold.\n")
              .Append("3. **intentionally adapted**: an exact SoloStone analogue covers the original (`adapted_by`).\n")
              .Append("4. **partial**: a partial analogue exists, or at least one of engine/reach holds but not all four criteria.\n")
              .Append("5. **missing**: neither engine nor reach holds: original gameplay content that neither behaves nor appears.\n\n");

            sb.Append("## How the ledger is updated\n\n");
            sb.Append("The ledger is generated, never hand-edited. Every later phase (Secret-aware AI, the draft catalog import, loadouts, treasures, shrines, encounters, global rules) changes the engine, the run data or the validator, and then re-runs the generator:\n\n");
            sb.Append("```sh\ncd tools/restoration-ledger\ndotnet run -c Release\n```\n\n");
            sb.Append("It builds the engine (`engine/SabberStoneCore`, net10.0) and compiles `plugin/DungeonRun/DungeonRunData.cs`, `DungeonRunValidator.cs`, `DungeonRunAdmission.cs` and `plugin/Npc/World/NpcWorldJson.cs` unchanged, so ")
              .Append("availability and admission are exactly what the run layer would decide. Then:\n\n")
              .Append("1. Review the diff of `restoration-ledger.csv` and of the counts here; a phase is done when its rows move to *implemented* (or to *intentionally adapted* with a cited analogue).\n")
              .Append("2. A new admission rule (Secrets, deck-dependent cards) goes into `plugin/DungeonRun/DungeonRunAdmission.cs`; a new analogue or treasure-name fact goes into `tools/restoration-ledger/curation.json`; never into the CSV.\n")
              .Append("3. New reference inputs (bucket members, reconstructed boss decks) go under `data/dungeon-run/reference/`; the generator reads them by path.\n")
              .Append("4. When a phase adds a new kind of content or admission rule (board shrines, anomalies, boss-deck import), extend the role or validation mapping in `Program.cs` so those rows stop reading *not applicable*.\n\n");
            sb.Append("### Inputs of this run\n\n");
            sb.Append(Table(new[] { "Input", "SHA-256 (first 16)" }, Inputs.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => new[] { "`" + p.Key + "`", "`" + p.Value + "`" })));
            sb.Append("\nPlus the engine card data and DungeonRunContent compiled from the working tree.\n");
            return sb.ToString();
        }

        // ====================================================================== parsing

        private static List<Dictionary<string, string>> ParseCsv(string text)
        {
            var records = new List<List<string>>();
            var field = new StringBuilder(); var record = new List<string>();
            bool quoted = false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (quoted)
                {
                    if (c == '"') { if (i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; } else quoted = false; }
                    else field.Append(c);
                }
                else if (c == '"') quoted = true;
                else if (c == ',') { record.Add(field.ToString()); field.Clear(); }
                else if (c == '\n' || c == '\r')
                {
                    if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                    record.Add(field.ToString()); field.Clear(); records.Add(record); record = new List<string>();
                }
                else field.Append(c);
            }
            if (field.Length > 0 || record.Count > 0) { record.Add(field.ToString()); records.Add(record); }
            var header = records[0];
            return records.Skip(1).Where(r => r.Count > 1).Select(r =>
            {
                if (r.Count != header.Count) throw new InvalidDataException("reference CSV: a record has " + r.Count + " fields, expected " + header.Count);
                return header.Select((h, i) => (h, i)).ToDictionary(p => p.h, p => r[p.i]);
            }).ToList();
        }

        private static object FromJson(JsonElement e)
        {
            switch (e.ValueKind)
            {
                case JsonValueKind.Object: { var d = new Dictionary<string, object>(); foreach (var p in e.EnumerateObject()) d[p.Name] = FromJson(p.Value); return d; }
                case JsonValueKind.Array: { var l = new List<object>(); foreach (var x in e.EnumerateArray()) l.Add(FromJson(x)); return l; }
                case JsonValueKind.String: return e.GetString();
                case JsonValueKind.Number: return e.TryGetInt64(out long v) ? (object)v : e.GetDouble();
                case JsonValueKind.True: return true;
                case JsonValueKind.False: return false;
                default: return null;
            }
        }
    }
}
