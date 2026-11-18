// Encounter step of the catalog tool (phase 8): `dotnet run -c Release -- encounters`.
// Reads the hand transcriptions data/dungeon-run/reference/encounters/F<NN>.json (boss rows and deck lists by card NAME),
// resolves every name against facts/violet-hold-2026-09.json (the collectible original printing preferred, as the buckets
// are), checks every card with the run layer's own predicates (scripted, a client card, admitted for its side on a catalog
// run), applies the substitution and extension rule, and writes
//   data/dungeon-run/encounters-v1.json            the encounter rows and their decks (embedded in the plugin)
//   tools/restoration-ledger/curation.json         its two lists encounterAdaptations and encounterRejections (only those)
// Deterministic: the same inputs give byte-identical files (UTF-8 without BOM, LF). The tool, not the loader, guarantees the
// file validates: it fails (exit 1, nothing written) when a combat-tier deck cannot be filled to 30 cards.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using EngineCard = SabberStoneCore.Model.Card;

namespace DungeonRunCatalogTool
{
    internal static class EncounterStep
    {
        private sealed class Fact { public string Id, Name; public int Dbf; public bool Collectible; }

        private static readonly Dictionary<string, List<Fact>> ByName = new Dictionary<string, List<Fact>>(StringComparer.Ordinal);
        private static readonly HashSet<string> ClientIds = new HashSet<string>(StringComparer.Ordinal);
        private static readonly string[] ReprintPrefixes = { "CORE_", "VAN_", "LEG_", "WON_", "Story_", "PVPDR_", "TB_", "BG", "SW_COREOLD" };

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

        private static IDictionary<string, object> Obj(object v) => v as IDictionary<string, object> ?? throw new InvalidDataException("expected a JSON object");
        private static IEnumerable<IDictionary<string, object>> Rows(IDictionary<string, object> row, string key) =>
            row.TryGetValue(key, out object v) && v is IList list ? list.Cast<object>().Select(Obj) : Enumerable.Empty<IDictionary<string, object>>();
        private static string Text(IDictionary<string, object> row, string key) => row.TryGetValue(key, out object v) ? v as string : null;
        private static List<string> Strings(IDictionary<string, object> row, string key) =>
            row.TryGetValue(key, out object v) && v is IList list ? list.Cast<object>().Select(x => (string)x).ToList() : new List<string>();
        private static OrderedDictionary Map(params (string key, object value)[] pairs)
        {
            var map = new OrderedDictionary();
            foreach (var (key, value) in pairs) if (value != null) map.Add(key, value);
            return map;
        }
        private static string Sha256(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

        private static EngineCard Engine(string id)
        {
            try { return id == null ? null : Cards.FromId(id); } catch { return null; }
        }

        /// <summary>The card id of a listed name: the collectible original printing when there is one, else the only card of that name.</summary>
        private static string Resolve(string name, out string problem)
        {
            problem = null;
            if (!ByName.TryGetValue(name, out var facts)) { problem = "no card named \"" + name + "\" in the card data"; return null; }
            var known = facts.Where(f => Engine(f.Id) != null).ToList();
            var collectible = known.Where(f => f.Collectible).ToList();
            var originals = collectible.Where(f => !ReprintPrefixes.Any(p => f.Id.StartsWith(p, StringComparison.Ordinal))).ToList();
            var pick = (originals.Count > 0 ? originals : collectible).OrderBy(f => f.Dbf).FirstOrDefault();
            if (pick != null) return pick.Id;
            if (known.Count == 1) return known[0].Id;
            problem = known.Count == 0 ? "no engine card named \"" + name + "\"" : "\"" + name + "\" names " + known.Count + " non-collectible cards (" + string.Join(", ", known.Select(f => f.Id)) + "): give its id";
            return null;
        }

        /// <summary>Why a card cannot be in an encounter deck for <paramref name="scope"/>, or null when it can.</summary>
        private static string Failure(string id, AdmitScope scope)
        {
            var card = Engine(id);
            if (card == null) return "unknown";
            if (!ClientIds.Contains(id)) return "client-unknown";
            if (!card.Implemented) return "unscripted";
            var verdict = DungeonRunAdmission.Classify(card, id, scope, null, catalogRun: true);
            return verdict.Admitted ? null : "admission:" + verdict.Family;
        }

        private sealed class Built
        {
            public List<string> Cards = new List<string>();
            public List<OrderedDictionary> Substitutions = new List<OrderedDictionary>();
            public string Kind = "authentic", Label;
            public int Listed;
        }

        /// <summary>
        /// One deck from one listed table. <paramref name="target"/> is 30 for a combat tier, 0 to keep the listed count. A failing
        /// card is replaced by the boss's named replacement, else dropped; a short deck is filled, in order, with second copies of
        /// its valid non-Legendary singles, the boss's "extend" lists, then its "fillers". Every change is one substitution record.
        /// </summary>
        private static Built Build(IDictionary<string, object> boss, string label, int target, AdmitScope scope, List<string> errors, string where)
        {
            var built = new Built { Label = label };
            var list = Rows(boss, "lists").FirstOrDefault(l => Text(l, "label") == label);
            if (list == null) { errors.Add(where + ": no list labelled \"" + label + "\""); return built; }
            var replacements = boss.TryGetValue("replacements", out object r) && r is IDictionary<string, object> map ? map : new Dictionary<string, object>();
            string Named(string nameOrId, out string problem)
            {
                problem = null;
                return Engine(nameOrId) != null && ClientIds.Contains(nameOrId) ? nameOrId : Resolve(nameOrId, out problem);
            }
            void Sub(string wiki, string listed, string used, string reason) =>
                built.Substitutions.Add(Map(("wiki", wiki), ("listed", listed), ("used", used), ("reason", reason)));
            int Copies(string id) => built.Cards.Count(c => c == id);
            foreach (var entry in Rows(list, "cards"))
            {
                string name = Text(entry, "name");
                int count = entry.TryGetValue("count", out object c) && c != null ? Convert.ToInt32(c) : 1;
                built.Listed += count;
                string id = Text(entry, "id") ?? Resolve(name, out string problem0);
                string reason = id == null ? "unknown" : Failure(id, scope);
                if (reason == null) { built.Cards.AddRange(Enumerable.Repeat(id, count)); continue; }
                string replacement = replacements.TryGetValue(name, out object rep) ? Named((string)rep, out _) : null;
                if (replacement != null && Failure(replacement, scope) == null)
                {
                    built.Cards.AddRange(Enumerable.Repeat(replacement, count));
                    for (int i = 0; i < count; i++) Sub(name, id, replacement, reason);
                }
                else for (int i = 0; i < count; i++) Sub(name, id, null, reason);
            }
            int size = target > 0 ? target : built.Listed;
            if (built.Cards.Count > size)
            {
                foreach (string id in built.Cards.Skip(size)) Sub(null, id, null, "over-size");
                built.Cards = built.Cards.Take(size).ToList();
            }
            // Fill: second copies (never a second Legendary), then the extend lists, then the fillers.
            if (built.Cards.Count < size)
                foreach (string id in built.Cards.Distinct().ToList())
                {
                    if (built.Cards.Count >= size) break;
                    if (Copies(id) != 1 || Engine(id).Rarity == Rarity.LEGENDARY) continue;
                    built.Cards.Add(id);
                    Sub(null, null, id, "fill:second-copy");
                }
            var extras = new List<(string name, string source)>();
            foreach (string other in Strings(boss, "extend"))
                foreach (var entry in Rows(Rows(boss, "lists").FirstOrDefault(l => Text(l, "label") == other) ?? new Dictionary<string, object>(), "cards"))
                    extras.Add((Text(entry, "id") ?? Text(entry, "name"), "fill:" + other));
            foreach (string filler in Strings(boss, "fillers")) extras.Add((filler, "fill:curated"));
            for (int pass = 0; pass < 2 && built.Cards.Count < size; pass++)
                foreach (var (name, source) in extras)
                {
                    if (built.Cards.Count >= size) break;
                    string id = Named(name, out _);
                    if (id == null || Failure(id, scope) != null) continue;
                    int limit = Engine(id).Rarity == Rarity.LEGENDARY ? 1 : 2;
                    if (Copies(id) > pass || Copies(id) >= limit) continue;
                    built.Cards.Add(id);
                    Sub(null, null, id, source);
                }
            if (built.Cards.Count < size)
            {
                if (target > 0) errors.Add($"{where}: list \"{label}\" fills only {built.Cards.Count} of {size} cards; add fillers");
                else if (built.Cards.Count < DungeonRunEncounterData.MinDeck) errors.Add($"{where}: list \"{label}\" leaves {built.Cards.Count} cards, fewer than {DungeonRunEncounterData.MinDeck}");
            }
            built.Kind = built.Substitutions.Count == 0 ? "authentic" : target > 0 && built.Listed < target ? "extended" : "substituted";
            return built;
        }

        internal static int Run(string repo)
        {
            // ---- facts: names, client ids
            using (var facts = JsonDocument.Parse(File.ReadAllText(Path.Combine(repo, "facts", "violet-hold-2026-09.json"))))
                foreach (var c in facts.RootElement.GetProperty("cards").EnumerateArray())
                {
                    string id = c.GetProperty("id").GetString();
                    ClientIds.Add(id);
                    if (!c.TryGetProperty("name", out var n) || n.ValueKind != JsonValueKind.String) continue;
                    bool collectible = c.TryGetProperty("tags", out var tags) && tags.EnumerateArray().Any(t => t[0].GetInt32() == 321 && t[1].GetInt32() != 0);
                    var fact = new Fact { Id = id, Name = n.GetString(), Dbf = c.TryGetProperty("dbf", out var dbf) ? dbf.GetInt32() : 0, Collectible = collectible };
                    if (!ByName.TryGetValue(fact.Name, out var list)) ByName[fact.Name] = list = new List<Fact>();
                    list.Add(fact);
                }
            NpcWorldJson.Parser = text => { using var doc = JsonDocument.Parse(text); return FromJson(doc.RootElement); };

            string dir = Path.Combine(repo, "data", "dungeon-run", "reference", "encounters");
            var errors = new List<string>();
            var sources = new SortedDictionary<string, object>(StringComparer.Ordinal);
            var decks = new SortedDictionary<string, object>(StringComparer.Ordinal);
            var encounters = new List<object>();
            var rejected = new List<object>();
            var adaptations = new List<OrderedDictionary>();
            var rejections = new List<OrderedDictionary>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            int authentic = 0, changed = 0;

            foreach (string path in Directory.GetFiles(dir, "F*.json").OrderBy(p => Path.GetFileName(p), StringComparer.Ordinal))
            {
                byte[] bytes = File.ReadAllBytes(path);
                string file = Path.GetFileName(path);
                sources["reference/encounters/" + file] = Sha256(bytes);
                IDictionary<string, object> doc;
                using (var parsed = JsonDocument.Parse(new UTF8Encoding(false).GetString(bytes).TrimStart('﻿'))) doc = Obj(FromJson(parsed.RootElement));
                string fileFamily = Text(doc, "family");
                foreach (var boss in Rows(doc, "bosses"))
                {
                    string id = Text(boss, "id"), where = file + " " + id;
                    if (!seen.Add(id)) { errors.Add(where + ": listed twice"); continue; }
                    string tier = Text(boss, "tier");
                    bool combat = DungeonRunEncounterData.ThirtyCardDeck(tier, Strings(boss, "flags"));
                    var source = boss.TryGetValue("source", out object s) && s is IDictionary<string, object> src ? src : new Dictionary<string, object>();
                    string url = Text(source, "url"), read = Text(source, "read");

                    OrderedDictionary Deck(string deckId, IDictionary<string, object> spec, int target, AdmitScope scope, string at)
                    {
                        string label = Text(spec, "list");
                        var built = Build(boss, label, target, scope, errors, at);
                        // Family 09: a list the F file states as curated (no authentic list exists: "kind": "curated") is recorded so.
                        if (Text(spec, "kind") == DungeonRunDeckSource.KindCurated) built.Kind = DungeonRunDeckSource.KindCurated;
                        if (decks.ContainsKey(deckId)) errors.Add(at + ": deck id " + deckId + " used twice");
                        decks[deckId] = built.Cards;
                        if (built.Kind == "authentic") authentic++;
                        else
                        {
                            changed++;
                            var failed = built.Substitutions.Where(x => !((string)x["reason"]).StartsWith("fill:", StringComparison.Ordinal)).ToList();
                            adaptations.Add(Map(("id", id), ("kind", "deck"), ("deck", deckId), ("deckKind", built.Kind), ("source", url),
                                ("note", $"list \"{label}\": {built.Listed} listed, {built.Cards.Count} in the deck; "
                                    + $"{failed.Count} not usable ({string.Join(", ", failed.GroupBy(x => (string)x["reason"]).OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => g.Count() + " " + g.Key))}), "
                                    + $"{built.Substitutions.Count - failed.Count} filled")));
                        }
                        return Map(("id", deckId), ("kind", built.Kind), ("source", Map(("url", url), ("read", read), ("list", label))),
                            ("substitutions", built.Substitutions.Count > 0 ? built.Substitutions.Cast<object>().ToList() : null));
                    }

                    var row = Map(("id", id), ("name", Text(boss, "name")), ("provenance", Text(boss, "provenance")), ("family", Text(boss, "family") ?? fileFamily),
                        ("hero", Text(boss, "hero")), ("power", Text(boss, "power")), ("tier", tier), ("archetype", Text(boss, "archetype")));
                    // Step 9 (lanes R and E): incompatibleTreasures, modifierReasons and the "v4" override block pass through as written.
                    foreach (string key in new[] { "level", "health", "fixedHealth", "healthOffset", "bossManaCap", "unlock", "flags", "bossGeneratedSecrets", "cards", "incompatibleRules", "incompatibleModifiers", "start",
                        "incompatibleTreasures", "modifierReasons", "v4" })
                        if (boss.TryGetValue(key, out object value) && value != null) row.Add(key, value);
                    var variants = Rows(boss, "variants").ToList();
                    // A row with shrine variants has no deck of its own: its "deck" is the variant deck its list names.
                    var deckSpec = Obj(boss["deck"]);
                    string ownLabel = Text(deckSpec, "list");
                    var variantDecks = new Dictionary<string, OrderedDictionary>(StringComparer.Ordinal);
                    var variantRows = new List<object>();
                    foreach (var v in variants)
                    {
                        string vid = Text(v, "id"), deckId = "enc-" + id + "-" + vid;
                        var d = Deck(deckId, Obj(v["deck"]), combat ? 30 : 0, AdmitScope.Boss, where + " variant " + vid);
                        variantDecks[Text(Obj(v["deck"]), "list")] = d;
                        variantRows.Add(Map(("id", vid), ("shrine", Text(v, "shrine")), ("deck", d)));
                    }
                    row.Add("deck", variantDecks.TryGetValue(ownLabel, out var shared) ? shared : Deck("enc-" + id, deckSpec, combat ? 30 : 0, AdmitScope.Boss, where));
                    if (variantRows.Count > 0) row.Add("variants", variantRows);
                    var forms = new List<object>();
                    int k = 0;
                    foreach (var f in Rows(boss, "forms"))
                    {
                        k++;
                        var form = Map(("hero", Text(f, "hero")), ("power", Text(f, "power")));
                        foreach (string key in new[] { "health", "at", "keepExcess", "draw" })
                            if (f.TryGetValue(key, out object value) && value != null) form.Add(key, value);
                        if (f.TryGetValue("deck", out object fd) && fd is IDictionary<string, object> formDeck)
                            form.Add("deck", Deck("enc-" + id + "-f" + k, formDeck, 0, AdmitScope.Boss, where + " form " + k));
                        forms.Add(form);
                    }
                    if (forms.Count > 0) row.Add("forms", forms);
                    // The run player's fight-only hero cards: the cards that join the deck are checked for the run player; a
                    // card that fails is dropped and recorded.
                    var heroCards = new List<object>();
                    foreach (var h in Rows(boss, "heroCards"))
                    {
                        string hero = Text(h, "hero");
                        var built = Build(boss, Text(Obj(h["deck"]), "list"), 0, AdmitScope.Player, errors, where + " hero card " + hero);
                        // A hero card's cards keep the listed count less the unusable ones (no minimum).
                        errors.RemoveAll(e => e.StartsWith(where + " hero card " + hero + ": list", StringComparison.Ordinal) && e.Contains("fewer than"));
                        if (built.Substitutions.Count > 0)
                            adaptations.Add(Map(("id", id), ("kind", "deck"), ("deck", "hero card " + hero), ("deckKind", "substituted"), ("source", url),
                                ("note", $"list \"{built.Label}\": {built.Listed} listed, {built.Cards.Count} join the deck; "
                                    + string.Join(", ", built.Substitutions.Select(x => (x["wiki"] ?? x["used"]) + " " + x["reason"])))));
                        heroCards.Add(Map(("hero", hero), ("deck", built.Cards), ("passives", Strings(h, "passives")), ("skipFor", Strings(h, "skipFor"))));
                    }
                    if (heroCards.Count > 0) row.Add("heroCards", heroCards);
                    var relay = new List<object>();
                    var relayEntries = new Dictionary<string, int>(StringComparer.Ordinal);
                    foreach (var h in Rows(boss, "relay"))
                    {
                        // Family 24: a hero listed more than once (a Tombs explorer with its three finale decks) numbers its later decks.
                        string hero = Text(h, "hero");
                        int entry = relayEntries[hero] = (relayEntries.TryGetValue(hero, out int seenEntries) ? seenEntries : 0) + 1;
                        string deckId = "enc-" + id + "-relay-" + hero + (entry > 1 ? "-" + entry : "");
                        Deck(deckId, Obj(h["deck"]), 0, AdmitScope.Player, where + " relay " + hero);
                        relay.Add(Map(("hero", hero), ("power", Text(h, "power")), ("deck", deckId), ("health", h.TryGetValue("health", out object hp) ? hp : null), ("skipFor", Strings(h, "skipFor"))));
                    }
                    if (relay.Count > 0) row.Add("relay", relay);
                    encounters.Add(row);
                    foreach (var a in Rows(boss, "adaptations"))
                        adaptations.Add(Map(("id", id), ("kind", Text(a, "kind")), ("source", url), ("note", Text(a, "note"))));
                }
                foreach (var r in Rows(doc, "rejected"))
                {
                    var entry = Map(("id", Text(r, "id")), ("name", Text(r, "name")), ("power", Text(r, "power")), ("reason", Text(r, "reason")), ("lift", Text(r, "lift")),
                        ("cards", Strings(r, "cards")));
                    rejected.Add(entry);
                    rejections.Add(Map(("id", Text(r, "id")), ("power", Text(r, "power")), ("reason", Text(r, "reason")), ("lift", Text(r, "lift")), ("cards", Strings(r, "cards")), ("file", file)));
                }
            }
            if (errors.Count > 0)
            {
                foreach (string e in errors) Console.Error.WriteLine("encounters: " + e);
                Console.Error.WriteLine("encounters: " + errors.Count + " error(s); nothing written");
                return 1;
            }

            var root = Map(("schemaVersion", DungeonRunEncounterData.SchemaVersion), ("encountersVersion", 1),
                ("generatedBy", "tools/dungeon-run-catalog (encounters); do not edit by hand"), ("sources", sources),
                ("decks", decks), ("encounters", encounters), ("rejected", rejected));
            string json = Json.Write(root) + "\n";
            string outPath = Path.Combine(repo, "data", "dungeon-run", "encounters-v1.json");
            // The file must load and hold what was built before anything is written.
            var check = new DungeonRunData();
            DungeonRunEncounterData.Parse(check, json);
            if (check.Bosses.Count != encounters.Count) throw new InvalidDataException("the written encounters do not parse back");
            File.WriteAllText(outPath, json, new UTF8Encoding(false));

            // curation.json: the two tool-written lists live in one block at the end of the file; everything before it is kept byte for byte.
            string curationPath = Path.Combine(repo, "tools", "restoration-ledger", "curation.json");
            string curation = new UTF8Encoding(false).GetString(File.ReadAllBytes(curationPath)).Replace("\r\n", "\n");
            const string marker = ",\n  \"encounterAdaptationsSource\":";
            int at = curation.IndexOf(marker, StringComparison.Ordinal);
            string head = at >= 0 ? curation.Substring(0, at) : curation.TrimEnd().TrimEnd('}').TrimEnd();
            var sortedAdaptations = adaptations.OrderBy(a => (string)a["id"], StringComparer.Ordinal).ThenBy(a => (string)a["kind"], StringComparer.Ordinal)
                .ThenBy(a => (string)(a.Contains("deck") ? a["deck"] : ""), StringComparer.Ordinal).ToList();
            var sortedRejections = rejections.OrderBy(a => (string)a["id"], StringComparer.Ordinal).ToList();
            string body = Json.Write(Map(
                ("encounterAdaptationsSource", "Written by tools/dungeon-run-catalog (step encounters) from data/dungeon-run/reference/encounters/F<NN>.json; never edit these two lists by hand. encounterAdaptations: every deck that is not the wiki's list unchanged (substituted, extended or curated) and every adaptation an F file states; encounterRejections: encounters and encounter cards that are not imported, with the reason and the lift."),
                ("encounterAdaptations", sortedAdaptations.Cast<object>().ToList()), ("encounterRejections", sortedRejections.Cast<object>().ToList())));
            if (!body.StartsWith("{\n  \"encounterAdaptationsSource\":", StringComparison.Ordinal)) throw new InvalidDataException("unexpected JSON writer layout");
            string tail = ",\n" + body.Substring(2) + "\n";
            using (JsonDocument.Parse(head + tail)) { }
            File.WriteAllText(curationPath, head + tail, new UTF8Encoding(false));

            Console.WriteLine($"encounters: {encounters.Count} rows, {decks.Count} decks ({authentic} authentic, {changed} changed), {adaptations.Count} adaptations, {rejected.Count} rejected");
            Console.WriteLine("wrote " + outPath);
            Console.WriteLine("wrote " + curationPath + " (encounterAdaptations, encounterRejections)");
            return 0;
        }
    }
}
