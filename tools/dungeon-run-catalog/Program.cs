// Shared draft catalog generator (docs/design/dungeon-run-catalog.md). Imports the original adventure bucket memberships
// (data/dungeon-run/reference/buckets.json) and the SoloStone bundles (data/dungeon-run/pool-v1.json) into one catalog,
// data/dungeon-run/catalog-v1.json, that the run layer loads (DungeonRunData.Parse). Every member is admitted or rejected
// with the run layer's own predicate (DungeonRunValidator.PoolCardReason, DungeonRunAdmission.Classify), compiled in
// unchanged; every rejection is written with its reason. Read-only on every input; writes only the two output files.
// Deterministic: the same inputs give byte-identical files (UTF-8 without BOM, LF line endings).
using System;
using System.Collections;
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

namespace DungeonRunCatalogTool
{
    /// <summary>One bucket member after the filter.</summary>
    internal sealed class Member
    {
        public string Id, WikiId, Name, Reason;
        public bool Grant;
        public bool Admitted => Reason == null;
    }

    /// <summary>One catalog entry being built.</summary>
    internal sealed class Entry
    {
        public string Id, Label, LabelKey, Draw, Withdrawn, Hero, Adventure;
        /// <summary>Why an offerable entry no playable class row can be offered (null when one can).</summary>
        public string Unreachable;
        public bool Legacy, Distinct, Offerable = true;
        public List<string> Classes = new List<string>(), ClassesAll = new List<string>(), Themes = new List<string>(), Cards = new List<string>();
        public string Mechanic, ThemeSource;
        public int Weight = 1;
        public double AvgCost;
        public List<SortedDictionary<string, object>> Provenance = new List<SortedDictionary<string, object>>();
        public List<Member> Members = new List<Member>();
        public (string CardId, bool OnFirstTake)? Quest;
        public IEnumerable<Member> Admitted => Members.Where(m => m.Admitted);
    }

    internal static class Program
    {
        private static readonly Dictionary<string, string> AdventurePrefix = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Dungeon Run"] = "dr", ["Monster Hunt"] = "mh", ["Rumble Run"] = "rr", ["The Dalaran Heist"] = "dh", ["Tombs of Terror"] = "tot",
        };
        // The adventures whose buckets are keyed by a hero, not a class (the id slug is the hero's).
        private static readonly HashSet<string> HeroKeyed = new HashSet<string>(StringComparer.Ordinal) { "Monster Hunt", "Tombs of Terror" };
        /// <summary>The provenance adventure of a curation.json custom bucket (DungeonRun.SourceTag shows it as "SoloStone").</summary>
        internal const string CustomAdventure = "SoloStone-curated";
        /// <summary>The fewest cards a custom bucket may hold: the smallest original bucket's offer pool (6).</summary>
        private const int MinCustomBucket = 6;
        // Quests were offered automatically on the first take of their bucket in every adventure but Tombs of Terror.
        private const string NoAutoQuest = "Tombs of Terror";

        /// <summary>
        /// Keyword families for theme tags, in priority order (the first match is the primary tag). Each regex is matched
        /// against the labelKey. Ids reuse pool-v1.json's 13 themes where they fit.
        /// </summary>
        private static readonly (string Theme, Regex Pattern)[] Keywords =
        {
            ("murloc", new Regex(@"\bmurloc|murgl|\bfishy\b")),
            ("pirate", new Regex(@"\bpirate|scallywag|smuggler|outlaw")),
            ("dragon", new Regex(@"\bdragon")),
            ("elemental", new Regex(@"\belemental|\belements\b")),
            ("demon", new Regex(@"\bdemon")),
            ("mechanical", new Regex(@"\bmech|robot|\bcogs\b|\bgears\b|scrapyard|rust buckets|magnetic|technolog|metalhead|\bboom\b")),
            ("undead", new Regex(@"undead|army of the dead|plaguebringer")),
            ("beast", new Regex(@"\bbeast|stampede|pack mentality|\broar\b|\bdino|jungle king|\bferal\b|on the hunt|going wild|all bark|thick hide")),
            ("jade", new Regex(@"\bjade\b")),
            ("secret", new Regex(@"\bsecret|\btrap")),
            ("quest", new Regex(@"\bquest\b")),
            ("deathrattle", new Regex(@"deathrattle|dying words|afterlife|last rites|old bones|from the graves|dead or alive")),
            ("battlecry", new Regex(@"battlecr|warcry")),
            ("taunt", new Regex(@"\btaunt|protector|\bdefense\b|tank up")),
            ("weapons", new Regex(@"weapon|arsenal|armory|kingsbane|blademaster|sacred arms|keep swingin|iron and steel")),
            ("spell-damage", new Regex(@"spell damage|spell power")),
            ("spells", new Regex(@"\bspells?\b|sssspells|multi cast|spellslinger|spellchaser")),
            ("heal", new Regex(@"\bheal|curative|restoration|regeneration|recovery|life bringing")),
            ("armor", new Regex(@"\barmou?r")),
            ("freeze", new Regex(@"\bfrost|freez|coldsnap")),
            ("overload", new Regex(@"overload")),
            ("discard", new Regex(@"discard")),
            ("combo", new Regex(@"\bcombo\b")),
            ("hero-power", new Regex(@"heroic|hero power|powered up|\binspir")),
            ("discover", new Regex(@"discover|choices|spell searching|spell generation")),
            ("buff", new Regex(@"\bbuffs?\b|handbuff|pump it up|empowering|inner strength|sharing strength|strength within|blessing")),
            ("big", new Regex(@"\bbig\b|\bgiant|go big|\bstomp\b|\bmighty\b|\bhuge\b")),
            ("swarm", new Regex(@"\bswarm|small army|little legion|wee minions|tiny|pint sized|miniature army|summon")),
            ("legendary", new Regex(@"\blegend")),
        };

        private static readonly Dictionary<string, string> Inputs = new Dictionary<string, string>(StringComparer.Ordinal);
        private static Dictionary<string, EngineCard> engine;
        private static OfflinePoolPolicy policy;
        /// <summary>curation.json "exclusionNotes": a note written on every excluded member with that listed id.</summary>
        private static Dictionary<string, string> exclusionNotes = new Dictionary<string, string>(StringComparer.Ordinal);
        private static readonly Dictionary<string, string> factNames = new Dictionary<string, string>(StringComparer.Ordinal);

        private static int Main(string[] args)
        {
            // Phase 8: `encounters` builds data/dungeon-run/encounters-v1.json from reference/encounters (Encounters.cs).
            try { return args.Contains("encounters") ? EncounterStep.Run(Arg(args, "--repo", null) ?? FindRepo()) : Run(args); }
            catch (Exception e) { Console.Error.WriteLine("dungeon-run-catalog: " + e); return 1; }
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
            Inputs[label] = Sha256(bytes);
            return new UTF8Encoding(false).GetString(bytes).TrimStart('\uFEFF');
        }

        private static string Sha256(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

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

        /// <summary>"Stomp, Stomp, STOMP" -> "stomp-stomp-stomp"; "Cult of C'Thun" -> "cult-of-cthun".</summary>
        private static string Slug(string text) => DungeonRunData.LabelKeyOf(text).Replace(' ', '-');

        private static int Run(string[] args)
        {
            if (args.Contains("--help") || args.Contains("-h"))
            {
                Console.WriteLine("DungeonRunCatalogTool [--repo DIR] [--out FILE] [--out-md FILE]");
                return 0;
            }
            string repo = Path.GetFullPath(Arg(args, "--repo", null) ?? FindRepo());
            string dataDir = Path.Combine(repo, "data", "dungeon-run");
            string refDir = Path.Combine(dataDir, "reference");
            string outJson = Arg(args, "--out", Path.Combine(dataDir, "catalog-v1.json"));
            string outMd = Arg(args, "--out-md", Path.Combine(repo, "docs", "design", "dungeon-run-catalog.md"));

            // ------------------------------------------------------------------ inputs
            NpcWorldJson.Parser = text => { using var doc = JsonDocument.Parse(text); return FromJson(doc.RootElement); };
            string runText = ReadInput("data/dungeon-run/run-v1.json", Path.Combine(dataDir, "run-v1.json"));
            string poolText = ReadInput("data/dungeon-run/pool-v1.json", Path.Combine(dataDir, "pool-v1.json"));
            string npcText = ReadInput("data/npc/decks-v1.json", Path.Combine(repo, "data", "npc", "decks-v1.json"));
            var data = DungeonRunData.Parse(runText, poolText, npcText);
            engine = Cards.All.Where(c => c?.Id != null).GroupBy(c => c.Id).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
            policy = OfflinePoolPolicy.Load();
            Console.WriteLine($"engine cards: {engine.Count}");
            using var buckets = JsonDocument.Parse(ReadInput("data/dungeon-run/reference/buckets.json", Path.Combine(refDir, "buckets.json")));
            using var adventures = JsonDocument.Parse(ReadInput("data/dungeon-run/reference/adventures.json", Path.Combine(refDir, "adventures.json")));
            using var curationDoc = JsonDocument.Parse(ReadInput("tools/dungeon-run-catalog/curation.json", Path.Combine(repo, "tools", "dungeon-run-catalog", "curation.json")));
            using (var facts = JsonDocument.Parse(ReadInput("facts/violet-hold-2026-09.json", Path.Combine(repo, "facts", "violet-hold-2026-09.json"))))
            {
                foreach (var c in facts.RootElement.GetProperty("cards").EnumerateArray())
                    if (c.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String) factNames[c.GetProperty("id").GetString()] = n.GetString();
                if (facts.RootElement.TryGetProperty("overlay", out var overlay) && overlay.TryGetProperty("cards", out var oc))
                    foreach (var c in oc.EnumerateArray())
                        if (c.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String) factNames[c.GetProperty("id").GetString()] = n.GetString();
            }
            var curation = curationDoc.RootElement;
            if (curation.GetProperty("schemaVersion").GetInt32() != 1) throw new InvalidDataException("curation.json: unsupported schemaVersion");
            var labelFixes = curation.GetProperty("labelFixes").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString(), StringComparer.Ordinal);
            var grantHeroes = new HashSet<string>(curation.GetProperty("grantHeroes").EnumerateArray().Select(e => e.GetString()), StringComparer.Ordinal);
            var distinctLabels = curation.GetProperty("distinct").EnumerateArray()
                .Select(e => (e.GetProperty("adventure").GetString(), e.GetProperty("labelKey").GetString())).ToHashSet();
            var handThemes = curation.GetProperty("themes").EnumerateObject()
                .ToDictionary(p => p.Name, p => p.Value.EnumerateArray().Select(t => t.GetString()).ToList(), StringComparer.Ordinal);
            var weights = curation.GetProperty("weights").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetInt32(), StringComparer.Ordinal);
            var exclusions = new Dictionary<(string entry, string id), string>();
            foreach (var x in curation.GetProperty("exclusions").EnumerateArray())
            {
                string reason = x.GetProperty("reason").GetString();
                if (string.IsNullOrWhiteSpace(reason)) throw new InvalidDataException("curation.json: an exclusion needs a reason");
                exclusions[(x.GetProperty("entry").GetString(), x.GetProperty("id").GetString())] = reason;
            }
            var usedExclusions = new HashSet<(string, string)>();
            exclusionNotes = curation.GetProperty("exclusionNotes").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString(), StringComparer.Ordinal);

            // Tombs of Terror class pairs: the client's guest heroes ("Mage / Rogue"), matched to bucket heroes by card name.
            var guestPairs = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (var ad in adventures.RootElement.GetProperty("adventures").EnumerateArray())
            {
                if (ad.GetProperty("key").GetString() != "tombs-of-terror") continue;
                foreach (var g in ad.GetProperty("guestHeroes").EnumerateArray())
                    guestPairs[g.GetProperty("card").GetProperty("name").GetString()] = g.GetProperty("name").GetString()
                        .Split('/').Select(s => s.Trim().ToUpperInvariant()).ToList();
            }

            // ------------------------------------------------------------------ fixed SoloStone bundles (pool-v1.json)
            var entries = new List<Entry>();
            foreach (var b in data.Bundles)
            {
                var e = new Entry
                {
                    Id = b.Id, Label = DungeonRunData.FixedLabel(b.Theme), Draw = DungeonRunBundle.KindFixed, Legacy = true, Adventure = "SoloStone",
                    Classes = new List<string> { b.Class }, Themes = new List<string> { b.Theme }, Cards = b.Cards.ToList(), ThemeSource = "pool-v1.json",
                };
                e.LabelKey = DungeonRunData.LabelKeyOf(e.Label);
                e.AvgCost = b.Cards.Average(id => (double)engine[id].Cost);
                e.Provenance.Add(Obj(("adventure", "SoloStone"), ("label", e.Label), ("source", "data/dungeon-run/pool-v1.json")));
                entries.Add(e);
            }
            string poolHash = DungeonRunData.PoolBundlesHash(data.Bundles);

            // ------------------------------------------------------------------ original buckets
            var themeFallbacks = new SortedSet<string>(StringComparer.Ordinal);
            var handTagged = new SortedSet<string>(StringComparer.Ordinal);
            var unusedHandThemes = new SortedSet<string>(handThemes.Keys, StringComparer.Ordinal);
            int viaAlternate = 0;
            var viaList = new List<(string entry, string wiki, string id)>();
            foreach (var ad in buckets.RootElement.GetProperty("adventures").EnumerateArray())
            {
                string adv = ad.GetProperty("adventure").GetString();
                if (!AdventurePrefix.TryGetValue(adv, out string prefix)) throw new InvalidDataException("buckets.json: unknown adventure " + adv);
                foreach (var b in ad.GetProperty("buckets").EnumerateArray())
                {
                    string hero = b.GetProperty("hero").GetString(), label = b.GetProperty("label").GetString();
                    string membership = b.GetProperty("membership").GetString();
                    var classes = b.GetProperty("class").GetString().Split('/').Select(s => s.Trim().ToUpperInvariant()).ToList();
                    if (adv == "Tombs of Terror")
                    {
                        var pair = guestPairs.Where(p => hero.StartsWith(p.Key, StringComparison.Ordinal)).Select(p => p.Value).FirstOrDefault()
                            ?? throw new InvalidDataException($"adventures.json: no Tombs of Terror guest hero for {hero}");
                        if (!pair.SequenceEqual(classes)) throw new InvalidDataException($"{hero}: buckets.json classes {string.Join("/", classes)} but the guest hero is {string.Join("/", pair)}");
                    }
                    foreach (string c in classes)
                        if (!Enum.TryParse(c, out CardClass cc) || cc == CardClass.NEUTRAL || cc == CardClass.INVALID) throw new InvalidDataException($"{adv} {hero}: class {c}");
                    string key = DungeonRunData.LabelKeyOf(label);
                    if (labelFixes.TryGetValue(key, out string fixedKey)) key = fixedKey;
                    string owner = HeroKeyed.Contains(adv) ? Slug(hero) : classes[0].ToLowerInvariant();
                    var e = new Entry
                    {
                        Id = $"{prefix}.{owner}.{Slug(label)}", Label = label, LabelKey = key, Draw = DungeonRunBundle.KindPool, Legacy = false,
                        Classes = classes, Hero = hero, Adventure = adv,
                    };
                    if (adv == "Tombs of Terror") e.ClassesAll = classes.ToList();
                    e.Distinct = distinctLabels.Contains((adv, key));
                    var prov = Obj(("adventure", adv), ("hero", hero), ("label", label), ("membership", membership));
                    if (b.TryGetProperty("labelCardId", out var lid) && lid.ValueKind == JsonValueKind.String) prov["labelCardId"] = lid.GetString();
                    if (b.TryGetProperty("source", out var src) && src.ValueKind == JsonValueKind.String) prov["source"] = src.GetString();
                    e.Provenance.Add(prov);

                    bool granting = adv == "Monster Hunt" && grantHeroes.Contains(hero);
                    var seen = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var m in b.GetProperty("members").EnumerateArray())
                    {
                        string wiki = m.GetProperty("cardId").GetString();
                        var memberIds = new List<string> { wiki };
                        foreach (var alt in m.GetProperty("alternates").EnumerateArray()) if (!memberIds.Contains(alt.GetString())) memberIds.Add(alt.GetString());
                        var member = Resolve(memberIds, classes, granting);
                        member.WikiId = wiki;
                        member.Name = m.GetProperty("name").GetString();
                        if (member.Admitted && !seen.Add(member.Id)) member.Reason = "curation:duplicate member";
                        if (member.Admitted && exclusions.TryGetValue((e.Id, wiki), out string why)) { member.Reason = "curation:" + why; usedExclusions.Add((e.Id, wiki)); }
                        if (member.Admitted && member.Id != wiki) { viaAlternate++; viaList.Add((e.Id, wiki, member.Id)); }
                        e.Members.Add(member);
                    }
                    var admitted = e.Admitted.ToList();
                    int usable = e.Distinct ? admitted.Select(x => x.Id).Distinct().Count() : admitted.Count;
                    if (usable < 3)
                    {
                        e.Offerable = false;
                        e.Withdrawn = membership != "published" ? "membership:" + membership : $"admitted:{usable} (fewer than 3 admitted members)";
                    }
                    e.AvgCost = admitted.Count == 0 ? 0 : admitted.Average(x => (double)engine[x.Id].Cost);
                    if (weights.TryGetValue(e.Id, out int w)) e.Weight = w;

                    // Themes: hand tags (curation) replace keyword tags; a label matching neither gets label:<key>.
                    if (handThemes.TryGetValue(key, out var hand)) { e.Themes = hand.ToList(); e.ThemeSource = "curation"; handTagged.Add(key); unusedHandThemes.Remove(key); }
                    else
                    {
                        e.Themes = Keywords.Where(k => k.Pattern.IsMatch(key)).Select(k => k.Theme).ToList();
                        e.ThemeSource = "keyword";
                    }
                    var quest = admitted.FirstOrDefault(x => engine[x.Id][GameTag.QUEST] != 0);
                    if (quest != null)
                    {
                        e.Quest = (quest.Id, adv != NoAutoQuest);
                        if (!e.Themes.Contains("quest")) e.Themes.Add("quest");
                    }
                    if (e.Themes.Count == 0) { e.Themes.Add("label:" + key); e.ThemeSource = "fallback"; themeFallbacks.Add(key); }
                    if (e.Themes.Contains("secret")) e.Mechanic = "secret";
                    entries.Add(e);
                }
            }
            // ------------------------------------------------------------------ SoloStone-curated buckets (curation.json "customBuckets")
            // Phase 6 (F16): classes the five adventures never offered (Death Knight, Demon Hunter) get themed pool entries of
            // their own, so an act run's fourteen drafts never run dry. Each member goes through the same admission, reprint and
            // validator path as a bucket member (Resolve), and a member that is not admitted fails the build: a curated bucket
            // is the curator's promise, not a filter's leftovers.
            int customBuckets = 0, customBucketCards = 0;
            if (curation.TryGetProperty("customBuckets", out var customList))
                foreach (var cb in customList.EnumerateArray())
                {
                    string id = cb.GetProperty("id").GetString(), cls = cb.GetProperty("class").GetString(), label = cb.GetProperty("label").GetString();
                    if (!Enum.TryParse(cls, out CardClass cbClass) || cbClass == CardClass.NEUTRAL || cbClass == CardClass.INVALID)
                        throw new InvalidDataException($"curation.json: custom bucket {id} has class {cls}");
                    var themes = cb.GetProperty("themes").EnumerateArray().Select(t => t.GetString()).ToList();
                    if (themes.Count == 0) throw new InvalidDataException($"curation.json: custom bucket {id} has no themes");
                    var e = new Entry
                    {
                        Id = id, Label = label, LabelKey = DungeonRunData.LabelKeyOf(label), Draw = DungeonRunBundle.KindPool, Legacy = false,
                        Classes = new List<string> { cls }, Adventure = CustomAdventure, Themes = themes, ThemeSource = "curation",
                        Weight = cb.TryGetProperty("weight", out var cw) ? cw.GetInt32() : 1,
                    };
                    e.Provenance.Add(Obj(("adventure", CustomAdventure), ("label", label), ("membership", "curated"),
                        ("source", "tools/dungeon-run-catalog/curation.json customBuckets")));
                    var seenCustom = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var card in cb.GetProperty("cards").EnumerateArray())
                    {
                        string wiki = card.GetString();
                        var member = Resolve(new List<string> { wiki }, e.Classes, false);
                        member.WikiId = wiki;
                        member.Name = factNames.TryGetValue(member.Id, out string fn) ? fn : engine.TryGetValue(member.Id, out var ec) ? ec.Name : member.Id;
                        if (!member.Admitted) throw new InvalidDataException($"curation.json: custom bucket {id} member {wiki} is not admitted ({member.Reason})");
                        if (!seenCustom.Add(member.Id)) throw new InvalidDataException($"curation.json: custom bucket {id} lists {member.Id} twice");
                        e.Members.Add(member);
                    }
                    if (e.Members.Count < MinCustomBucket) throw new InvalidDataException($"curation.json: custom bucket {id} has {e.Members.Count} cards, needs at least {MinCustomBucket}");
                    e.AvgCost = e.Members.Average(x => (double)engine[x.Id].Cost);
                    if (e.Themes.Contains("secret")) e.Mechanic = "secret";
                    entries.Add(e);
                    customBuckets++; customBucketCards += e.Members.Count;
                }

            foreach (var x in exclusions.Keys.Where(k => !usedExclusions.Contains(k))) throw new InvalidDataException($"curation.json: exclusion {x.entry} {x.id} matches no admitted member");
            foreach (string k in exclusionNotes.Keys)
                if (!entries.Any(e => e.Members.Any(m => !m.Admitted && m.WikiId == k))) throw new InvalidDataException("curation.json: exclusionNotes names " + k + ", which no member excludes");
            foreach (string k in unusedHandThemes) throw new InvalidDataException("curation.json: themes names label " + k + ", which no bucket has");
            foreach (string id in weights.Keys) if (!entries.Any(e => e.Id == id)) throw new InvalidDataException("curation.json: weight for unknown entry " + id);

            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var e in entries)
            {
                if (!ids.Add(e.Id)) throw new InvalidDataException("duplicate entry id " + e.Id);
                if (!e.Legacy && !DungeonRunData.EntryIdPattern.IsMatch(e.Id)) throw new InvalidDataException("entry id " + e.Id + " breaks the id pattern");
            }

            // ------------------------------------------------------------------ dedup: merge identical entries
            // Two entries merge only when nothing but their hero and provenance differs: the same adventure and label, class
            // set, requirements, flags, quest, themes and admitted members (grants included). Buckets of different adventures,
            // or with different labels, stay separate entries with their own label, take budget and weight (the run layer never
            // offers two entries with the same class set and candidates in one draft).
            var merges = new List<(string kept, string merged)>();
            var pools = entries.Where(e => !e.Legacy && e.Offerable).ToList();
            string MergeKey(Entry e) => string.Join("|", e.Adventure, e.LabelKey, string.Join("/", e.Classes), string.Join("/", e.ClassesAll), e.Mechanic ?? "",
                e.Distinct ? "distinct" : "", e.Quest == null ? "" : e.Quest.Value.CardId + (e.Quest.Value.OnFirstTake ? "+first" : ""), string.Join(",", e.Themes),
                string.Join(",", e.Admitted.Select(m => m.Id + (m.Grant ? "+grant" : "")).OrderBy(x => x, StringComparer.Ordinal)));
            foreach (var group in pools.GroupBy(MergeKey))
            {
                var list = group.OrderBy(e => e.Id, StringComparer.Ordinal).ToList();
                foreach (var other in list.Skip(1))
                {
                    list[0].Provenance.AddRange(other.Provenance);
                    list[0].Weight = Math.Max(list[0].Weight, other.Weight);
                    entries.Remove(other);
                    merges.Add((list[0].Id, other.Id));
                }
            }
            var ordered = entries.Where(e => e.Legacy).Concat(entries.Where(e => !e.Legacy).OrderBy(e => e.Id, StringComparer.Ordinal)).ToList();

            // ------------------------------------------------------------------ cards index
            var custom = new HashSet<string>(DungeonRunContent.All.Where(c => c.ShellId != null).Select(c => c.ShellId), StringComparer.Ordinal);
            var cardEntries = new SortedDictionary<string, SortedSet<string>>(StringComparer.Ordinal);
            var cardStarters = new SortedDictionary<string, SortedSet<string>>(StringComparer.Ordinal);
            void Index(SortedDictionary<string, SortedSet<string>> map, string card, string where)
            {
                if (!map.TryGetValue(card, out var set)) map[card] = set = new SortedSet<string>(StringComparer.Ordinal);
                set.Add(where);
            }
            foreach (var e in ordered)
            {
                foreach (string c in e.Cards) Index(cardEntries, c, e.Id);
                if (e.Offerable) foreach (var m in e.Admitted) Index(cardEntries, m.Id, e.Id);
            }
            foreach (var d in data.Decks) foreach (string c in d.Cards) Index(cardStarters, c, d.Id);
            // The catalog class rows' starter decks (run-v1.json "catalogStarterDecks": the Tombs explorers'), which the run
            // layer loads only with a catalog.
            if (NpcWorldJson.Parse(runText) is IDictionary<string, object> runRoot && runRoot.TryGetValue("catalogStarterDecks", out object catalogDecks) && catalogDecks is System.Collections.IList catalogDeckRows)
                foreach (var row in catalogDeckRows.Cast<object>().OfType<IDictionary<string, object>>())
                    // A generated starter (phase 6, the Heist and Tombs "Random" decks) lists no cards: NewRun draws them.
                    foreach (string c in NpcWorldJson.Strings(row, "cards", !row.ContainsKey("generator")) ?? new List<string>()) Index(cardStarters, c, NpcWorldJson.Text(row, "id"));
            var cards = new SortedDictionary<string, object>(StringComparer.Ordinal);
            foreach (string id in cardEntries.Keys.Concat(cardStarters.Keys).Distinct().OrderBy(x => x, StringComparer.Ordinal))
            {
                var row = Obj(("clientChecked", false), ("custom", custom.Contains(id)),
                    ("entries", (cardEntries.TryGetValue(id, out var es) ? es : new SortedSet<string>()).Cast<object>().ToList()));
                if (cardStarters.TryGetValue(id, out var ss)) row["starters"] = ss.Cast<object>().ToList();
                if (custom.Contains(id)) row["provenance"] = Obj(("adventure", "SoloStone"), ("source", "custom-card"));
                cards[id] = row;
            }

            // ------------------------------------------------------------------ summary
            var poolEntries = ordered.Where(e => !e.Legacy).ToList();
            var allMembers = poolEntries.SelectMany(e => e.Members).ToList();
            var excluded = allMembers.Where(m => !m.Admitted).ToList();
            string Family(string reason) => reason.StartsWith("curation:", StringComparison.Ordinal) ? "curation" : reason.Split(';')[0];
            var byReason = new SortedDictionary<string, object>(StringComparer.Ordinal);
            foreach (var g in excluded.GroupBy(m => Family(m.Reason))) byReason[g.Key] = (long)g.Count();
            var distinctByReason = new SortedDictionary<string, object>(StringComparer.Ordinal);
            foreach (var g in excluded.GroupBy(m => Family(m.Reason))) distinctByReason[g.Key] = (long)g.Select(m => m.WikiId).Distinct().Count();
            var perAdventure = new SortedDictionary<string, object>(StringComparer.Ordinal);
            foreach (var g in poolEntries.GroupBy(e => e.Adventure))
                perAdventure[g.Key] = Obj(("entries", (long)g.Count()), ("offerable", (long)g.Count(e => e.Offerable)),
                    ("admitted", (long)g.Sum(e => e.Admitted.Count())), ("excluded", (long)g.Sum(e => e.Members.Count(m => !m.Admitted))));
            var grants = poolEntries.SelectMany(e => e.Admitted.Where(m => m.Grant)).ToList();
            var grantsByClass = new SortedDictionary<string, object>(StringComparer.Ordinal);
            foreach (var g in grants.GroupBy(m => engine[m.Id].Class.ToString())) grantsByClass[g.Key] = (long)g.Select(m => m.Id).Distinct().Count();
            var summary = new SortedDictionary<string, object>(StringComparer.Ordinal)
            {
                ["entries"] = (long)ordered.Count,
                ["fixedEntries"] = (long)ordered.Count(e => e.Legacy),
                ["poolEntries"] = (long)poolEntries.Count,
                ["offerablePoolEntries"] = (long)poolEntries.Count(e => e.Offerable),
                ["withdrawnEntries"] = (long)poolEntries.Count(e => !e.Offerable),
                ["memberReferences"] = (long)allMembers.Count,
                ["admittedReferences"] = (long)allMembers.Count(m => m.Admitted),
                ["admittedReferencesOfferable"] = (long)poolEntries.Where(e => e.Offerable).Sum(e => e.Admitted.Count()),
                ["admittedDistinctIds"] = (long)allMembers.Where(m => m.Admitted).Select(m => m.Id).Distinct().Count(),
                ["excludedReferences"] = (long)excluded.Count,
                ["excludedDistinctIds"] = (long)excluded.Select(m => m.WikiId).Distinct().Count(),
                ["excludedByReason"] = byReason,
                ["excludedDistinctIdsByReason"] = distinctByReason,
                ["viaAlternate"] = (long)viaAlternate,
                ["grantReferences"] = (long)grants.Count,
                ["grantDistinctIdsByClass"] = grantsByClass,
                ["merges"] = (long)merges.Count,
                ["distinctEntries"] = (long)poolEntries.Count(e => e.Distinct),
                ["questEntries"] = (long)poolEntries.Count(e => e.Quest != null),
                ["secretEntries"] = (long)poolEntries.Count(e => e.Mechanic == "secret"),
                ["dualClassEntries"] = (long)poolEntries.Count(e => e.ClassesAll.Count > 0),
                ["themeFallbackLabels"] = (long)themeFallbacks.Count,
                ["handTaggedLabels"] = (long)handTagged.Count,
                ["customCards"] = (long)cards.Keys.Count(custom.Contains),
                ["customBuckets"] = (long)customBuckets,
                ["customBucketCards"] = (long)customBucketCards,
                ["perAdventure"] = perAdventure,
            };

            // ------------------------------------------------------------------ reachability
            // An offerable entry is reachable when some playable class row (run-v1.json "classes", and the "catalogClasses" rows
            // that ship enabled) can be offered it: DungeonRunBundle.EligibleFor with DungeonRunAdmission.MechanicHolds, the
            // predicate DungeonRun.OpenCatalogDraft uses, over the catalog as the run layer loads it.
            var catalogRows = new List<(string Id, List<string> Classes, bool Enabled)>();
            if (NpcWorldJson.Parse(runText) is IDictionary<string, object> runRows && runRows.TryGetValue("catalogClasses", out object catalogClassRows) && catalogClassRows is System.Collections.IList ccList)
                foreach (var row in ccList.Cast<object>().OfType<IDictionary<string, object>>())
                    catalogRows.Add((NpcWorldJson.Text(row, "id"), NpcWorldJson.Strings(row, "classes", true), NpcWorldJson.Boolean(row, "enabled", true)));
            {
                var probe = DungeonRunData.Parse(runText, poolText, npcText, Json.Write(Root(ordered, poolHash, cards, summary)) + "\n");
                var reachable = new HashSet<string>(StringComparer.Ordinal);
                foreach (var cls in probe.Classes)
                {
                    bool secret = DungeonRunAdmission.MechanicHolds(probe, cls.ClassSet, "secret");
                    foreach (var b in probe.Bundles.Where(b => b.IsPool && b.EligibleFor(cls.ClassSet, m => m == "secret" && secret))) reachable.Add(b.Id);
                }
                foreach (var e in poolEntries.Where(e => e.Offerable && !reachable.Contains(e.Id)))
                {
                    var pairRow = catalogRows.FirstOrDefault(r => e.ClassesAll.Count > 0 && r.Classes.SequenceEqual(e.ClassesAll));
                    bool pairPlayable = e.ClassesAll.Count == 0 || probe.Classes.Any(c => e.ClassesAll.All(c.ClassSet.Contains));
                    e.Unreachable = !pairPlayable
                        ? $"run:dual-class ({pairRow.Id ?? string.Join("_", e.ClassesAll)} {(pairRow.Id != null && !pairRow.Enabled ? "ships disabled" : "has no class row")})"
                        : e.Mechanic != null ? $"requires mechanic {e.Mechanic}, which no class row's run holds" : "no class row can be offered it";
                }
            }
            var unreachable = poolEntries.Where(e => e.Unreachable != null).ToList();
            summary["reachableOfferablePoolEntries"] = (long)poolEntries.Count(e => e.Offerable && e.Unreachable == null);
            summary["unreachableOfferablePoolEntries"] = (long)unreachable.Count;
            summary["admittedReferencesReachable"] = (long)poolEntries.Where(e => e.Offerable && e.Unreachable == null).Sum(e => e.Admitted.Count());
            var unreachableByReason = new SortedDictionary<string, object>(StringComparer.Ordinal);
            foreach (var g in unreachable.GroupBy(e => e.Unreachable)) unreachableByReason[g.Key] = (long)g.Count();
            summary["unreachableByReason"] = unreachableByReason;
            foreach (DictionaryEntry x in Json.Entries((IDictionary)summary["perAdventure"]))
                ((IDictionary)x.Value)["reachable"] = (long)poolEntries.Count(e => e.Adventure == (string)x.Key && e.Offerable && e.Unreachable == null);

            // ------------------------------------------------------------------ write
            var root = Root(ordered, poolHash, cards, summary);
            string json = Json.Write(root) + "\n";

            // Self-check: the runtime parser and validator accept the catalog (no client check; that is the game's).
            var loaded = DungeonRunData.Parse(runText, poolText, npcText, json);
            var errors = DungeonRunValidator.Validate(loaded, null);
            var entryIds = new HashSet<string>(loaded.Bundles.Select(b => b.Id), StringComparer.Ordinal);
            var catalogErrors = errors.Where(e => entryIds.Any(id => e.StartsWith(id + ":", StringComparison.Ordinal) || e.Contains("(" + id + ")"))).ToList();
            Console.WriteLine($"validation errors on the loaded catalog: {errors.Count} ({catalogErrors.Count} on catalog entries)");
            foreach (string e in errors.Take(40)) Console.WriteLine("  " + e);

            Directory.CreateDirectory(Path.GetDirectoryName(outJson));
            File.WriteAllText(outJson, json, new UTF8Encoding(false));
            Directory.CreateDirectory(Path.GetDirectoryName(outMd));
            File.WriteAllText(outMd, Report(ordered, summary, merges, themeFallbacks, viaList, grants, errors), new UTF8Encoding(false));
            Console.WriteLine($"wrote {outJson}: {ordered.Count} entries ({poolEntries.Count(e => e.Offerable)} offerable pool, {poolEntries.Count(e => !e.Offerable)} withdrawn, "
                + $"{ordered.Count(e => e.Legacy)} fixed), {allMembers.Count(m => m.Admitted)} admitted and {excluded.Count} excluded member references");
            Console.WriteLine($"wrote {outMd}");
            return errors.Count == 0 ? 0 : 2;
        }

        /// <summary>The catalog document: every entry, the cards index and the summary, keys in ordinal order.</summary>
        private static SortedDictionary<string, object> Root(List<Entry> ordered, string poolHash, SortedDictionary<string, object> cards,
            SortedDictionary<string, object> summary) =>
            new SortedDictionary<string, object>(StringComparer.Ordinal)
            {
                ["schemaVersion"] = (long)DungeonRunData.CatalogSchemaVersion,
                ["catalogVersion"] = 1L,
                ["generator"] = "tools/dungeon-run-catalog",
                ["sources"] = new SortedDictionary<string, object>(StringComparer.Ordinal)
                {
                    ["adventures"] = Inputs["data/dungeon-run/reference/adventures.json"],
                    ["buckets"] = Inputs["data/dungeon-run/reference/buckets.json"],
                    ["curation"] = Inputs["tools/dungeon-run-catalog/curation.json"],
                    ["pool"] = Inputs["data/dungeon-run/pool-v1.json"],
                    ["run"] = Inputs["data/dungeon-run/run-v1.json"],
                },
                ["poolBundlesHash"] = poolHash,
                ["entries"] = ordered.Select(EntryJson).Cast<object>().ToList(),
                ["cards"] = cards,
                ["summary"] = summary,
            };

        /// <summary>
        /// The member filter: the first id of <paramref name="ids"/> (primary, then reprint alternates) that passes every check
        /// is admitted; when none passes, the first id the engine knows is excluded with its first failing check.
        /// </summary>
        private static Member Resolve(List<string> ids, List<string> classes, bool grants)
        {
            Member first = null;
            foreach (string id in ids)
            {
                var m = Check(id, classes, grants);
                if (m.Admitted) return m;
                if (first == null || first.Reason == "engine:missing" && m.Reason != "engine:missing") first = m;
            }
            return first;
        }

        private static Member Check(string id, List<string> classes, bool grants)
        {
            var m = new Member { Id = id };
            if (!engine.TryGetValue(id, out var card)) { m.Reason = "engine:missing"; return m; }
            // Pool entries are drafted by catalog runs only, so the table's catalog lifts apply (a catalog run's player may
            // generate Secrets: the secret-generated draft cards are admitted).
            var verdict = DungeonRunAdmission.Classify(card, id, AdmitScope.Player, null, catalogRun: true);
            // An unscripted card the table also rejects records both, so neither lift is mistaken for the whole fix (ULD_003).
            if (!card.Implemented) { m.Reason = "engine:unscripted" + (verdict.Admitted ? "" : "; admission:" + verdict.Family); return m; }
            if (!verdict.Admitted) { m.Reason = "admission:" + verdict.Family; return m; }
            string reason = DungeonRunValidator.PoolCardReason(id, classes, false, policy, catalogRun: true);
            // An off-class member of a granting hero's bucket (Monster Hunt Shaw and Tess) is legal inside that bucket only.
            if (reason == "class" && grants && DungeonRunValidator.PoolCardReason(id, classes, true, policy, catalogRun: true) == null) { m.Grant = true; reason = null; }
            if (reason != null) { m.Reason = "validator:" + reason; return m; }
            if (DungeonRunValidator.HasRuneCost(id)) { m.Reason = "rune-cost"; return m; }
            return m;
        }

        private static SortedDictionary<string, object> Obj(params (string key, object value)[] pairs)
        {
            var d = new SortedDictionary<string, object>(StringComparer.Ordinal);
            foreach (var (k, v) in pairs) d[k] = v;
            return d;
        }

        private static double Round(double value) => Math.Round(value, 4, MidpointRounding.AwayFromZero);

        private static SortedDictionary<string, object> EntryJson(Entry e)
        {
            var requires = new SortedDictionary<string, object>(StringComparer.Ordinal);
            if (e.ClassesAll.Count > 0) requires["classesAll"] = e.ClassesAll.Cast<object>().ToList();
            if (e.Mechanic != null) requires["mechanic"] = e.Mechanic;
            var row = Obj(("id", e.Id), ("label", e.Label), ("labelKey", e.LabelKey), ("draw", e.Draw), ("legacy", e.Legacy),
                ("classes", e.Classes.Cast<object>().ToList()), ("requires", requires), ("distinct", e.Distinct),
                ("themes", e.Themes.Cast<object>().ToList()), ("weight", (long)e.Weight), ("avgCost", Round(e.AvgCost)),
                ("offerable", e.Offerable), ("provenance", e.Provenance.Cast<object>().ToList()));
            if (e.Legacy) row["cards"] = e.Cards.Cast<object>().ToList();
            else
            {
                row["members"] = e.Admitted.Select(m => (object)Obj(("id", m.Id), ("wikiId", m.WikiId), ("weight", 1L), ("grant", m.Grant))).ToList();
                row["excluded"] = e.Members.Where(m => !m.Admitted).Select(m =>
                {
                    var x = Obj(("id", m.Id), ("wikiId", m.WikiId), ("reason", m.Reason));
                    if (exclusionNotes.TryGetValue(m.WikiId, out string note)) x["note"] = note;
                    return (object)x;
                }).ToList();
                row["quest"] = e.Quest == null ? null : Obj(("cardId", e.Quest.Value.CardId), ("onFirstTake", e.Quest.Value.OnFirstTake));
            }
            if (e.Withdrawn != null) row["withdrawn"] = e.Withdrawn;
            if (e.Unreachable != null) row["unreachable"] = e.Unreachable;
            return row;
        }

        private static string Name(string id) =>
            factNames.TryGetValue(id, out string n) ? n : engine.TryGetValue(id, out var c) && c.Name != null ? c.Name : id;

        private static string Cell(string s) => (s ?? "").Replace("|", "\\|").Replace("\n", " ");

        private static string Report(List<Entry> entries, SortedDictionary<string, object> summary, List<(string kept, string merged)> merges,
            SortedSet<string> fallbacks, List<(string entry, string wiki, string id)> via, List<Member> grants, List<string> errors)
        {
            var md = new StringBuilder();
            md.Append("# Dungeon Run shared draft catalog\n\n");
            md.Append("Generated by `tools/dungeon-run-catalog` (`dotnet run -c Release`); do not edit by hand. The catalog itself is\n");
            md.Append("`data/dungeon-run/catalog-v1.json`, embedded in the plugin and loaded by `DungeonRunData.Parse`.\n\n");
            md.Append("It holds the 52 fixed SoloStone bundles of `pool-v1.json` (`draw: fixed`, `legacy: true`, offered to legacy runs)\n");
            md.Append("and one pool entry per original adventure bucket (`draw: pool`): three cards drawn from the admitted members.\n");
            md.Append("Each member was admitted with the run layer's own predicate, in this order (the first failure is recorded):\n");
            md.Append("`engine:missing`, `engine:unscripted`, `admission:<family>` (`DungeonRunAdmission.Classify`, player scope, catalog run),\n");
            md.Append("`validator:<check>` (`DungeonRunValidator.PoolCardReason` with the entry's class set; a grant passes the class\n");
            md.Append("check), `rune-cost`, then `curation:<note>`. A member is tried as its listed id, then each reprint alternate in order;\n");
            md.Append("the first id that passes is the one drawn (`id`), and `wikiId` keeps the listed id. The game drops a candidate its\n");
            md.Append("client does not know at load (`catalog: <entry> dropped <id> client-unknown` in the log) and withdraws an entry left\n");
            md.Append("with fewer than 3; the catalog marks every card `clientChecked: false` for that reason.\n\n");
            md.Append("## Inputs\n\n| Input | SHA-256 (first 16) |\n|---|---|\n");
            foreach (var p in Inputs.OrderBy(p => p.Key, StringComparer.Ordinal)) md.Append($"| `{p.Key}` | `{p.Value.Substring(0, 16)}` |\n");
            md.Append("\n## Counts\n\n| Item | Count |\n|---|---|\n");
            foreach (var p in summary.Where(p => !(p.Value is IDictionary)))
                md.Append($"| {p.Key} | {Convert.ToString(p.Value, CultureInfo.InvariantCulture)} |\n");
            foreach (var p in summary.Where(p => p.Value is IDictionary && p.Key != "perAdventure"))
                foreach (DictionaryEntry x in Json.Entries((IDictionary)p.Value)) md.Append($"| {p.Key}: {x.Key} | {Convert.ToString(x.Value, CultureInfo.InvariantCulture)} |\n");
            md.Append("\n### Per adventure\n\n| Adventure | Entries | Offerable | Reachable | Admitted refs | Excluded refs |\n|---|---|---|---|---|---|\n");
            foreach (DictionaryEntry x in Json.Entries((IDictionary)summary["perAdventure"]))
            {
                var d = (IDictionary)x.Value;
                md.Append($"| {x.Key} | {d["entries"]} | {d["offerable"]} | {d["reachable"]} | {d["admitted"]} | {d["excluded"]} |\n");
            }
            md.Append("\n## Validation\n\n");
            md.Append(errors.Count == 0 ? "`DungeonRunValidator.Validate` on the loaded catalog (no client check): no errors.\n"
                : $"`DungeonRunValidator.Validate` on the loaded catalog: {errors.Count} errors.\n\n" + string.Concat(errors.Select(e => "- " + Cell(e) + "\n")));
            md.Append("\n## Withdrawn entries\n\n| Entry | Reason |\n|---|---|\n");
            foreach (var e in entries.Where(e => !e.Offerable)) md.Append($"| `{e.Id}` | {Cell(e.Withdrawn)} |\n");
            md.Append("\n## Unreachable entries\n\nOfferable entries that no playable class row can be offered (`unreachable` in the catalog). A Tombs of Terror\n");
            md.Append("entry needs a run with both its classes (`requires.classesAll`); a class row that ships disabled in `run-v1.json`\n");
            md.Append("(`catalogClasses`, `enabled: false`, with its `disabledReason`) leaves its entries in the catalog, counted apart\n");
            md.Append("(`reachableOfferablePoolEntries`, `admittedReferencesReachable`). Since phase 6 F10 every dual row ships enabled.\n\n");
            var unreachableRows = entries.Where(e => e.Unreachable != null).ToList();
            if (unreachableRows.Count == 0) md.Append("None.\n");
            else { md.Append("| Entry | Reason |\n|---|---|\n"); foreach (var e in unreachableRows) md.Append($"| `{e.Id}` | {Cell(e.Unreachable)} |\n"); }
            md.Append("\n## Merges\n\nEntries merge only when they differ in nothing but their hero and provenance: the same adventure, label, class set,\n");
            md.Append("requirements, distinct flag, quest, themes and admitted members (grants included).\n\n");
            if (merges.Count == 0) md.Append("None.\n");
            else { md.Append("| Kept | Merged into it |\n|---|---|\n"); foreach (var m in merges) md.Append($"| `{m.kept}` | `{m.merged}` |\n"); }
            md.Append("\n## Excluded members\n\n| Entry | Listed id | Name | Reason |\n|---|---|---|---|\n");
            foreach (var e in entries.Where(e => !e.Legacy))
                foreach (var m in e.Members.Where(m => !m.Admitted))
                    md.Append($"| `{e.Id}` | `{m.WikiId}` | {Cell(m.Name ?? Name(m.WikiId))} | {Cell(m.Reason)}{(exclusionNotes.TryGetValue(m.WikiId, out string note) ? " (" + Cell(note) + ")" : "")} |\n");
            md.Append("\n## Members drawn through a reprint id\n\n| Entry | Listed id | Drawn id |\n|---|---|---|\n");
            foreach (var v in via) md.Append($"| `{v.entry}` | `{v.wiki}` | `{v.id}` |\n");
            md.Append("\n## Grants\n\nOff-class members of the Monster Hunt Shaw and Tess buckets, legal inside their own entry only.\n\n");
            md.Append("| Entry | Card | Name | Class |\n|---|---|---|---|\n");
            foreach (var e in entries.Where(e => !e.Legacy))
                foreach (var m in e.Admitted.Where(m => m.Grant))
                    md.Append($"| `{e.Id}` | `{m.Id}` | {Cell(Name(m.Id))} | {engine[m.Id].Class} |\n");
            md.Append("\n## Requirements and flags\n\n| Entry | Classes | Requires | Distinct | Quest |\n|---|---|---|---|---|\n");
            foreach (var e in entries.Where(e => !e.Legacy && (e.ClassesAll.Count > 0 || e.Mechanic != null || e.Distinct || e.Quest != null)))
            {
                string req = string.Join(", ", new[] { e.ClassesAll.Count > 0 ? "classesAll " + string.Join("+", e.ClassesAll) : null, e.Mechanic != null ? "mechanic " + e.Mechanic : null }.Where(x => x != null));
                string quest = e.Quest == null ? "" : $"`{e.Quest.Value.CardId}`{(e.Quest.Value.OnFirstTake ? " (on first take)" : "")}";
                md.Append($"| `{e.Id}` | {string.Join("/", e.Classes)} | {req} | {(e.Distinct ? "yes" : "")} | {quest} |\n");
            }
            md.Append("\n## Themes\n\nThe first tag is the primary one (DraftedThemes). Source: `keyword` (the tool's keyword map over the labelKey),\n");
            md.Append("`curation` (hand tags in `tools/dungeon-run-catalog/curation.json`), `fallback` (`label:<labelKey>`, listed below).\n\n");
            md.Append("| Entry | Label | Themes | Source |\n|---|---|---|---|\n");
            foreach (var e in entries.Where(e => !e.Legacy)) md.Append($"| `{e.Id}` | {Cell(e.Label)} | {string.Join(", ", e.Themes)} | {e.ThemeSource} |\n");
            md.Append("\n### Labels with only a fallback tag\n\n");
            md.Append(fallbacks.Count == 0 ? "None.\n" : string.Concat(fallbacks.Select(f => "- " + f + "\n")));
            return md.ToString();
        }
    }

    /// <summary>
    /// Deterministic JSON: object keys in the order given (the callers pass ordinal-sorted dictionaries), two-space
    /// indent, LF. An object or array holding only scalars is written on one line. Strings keep non-ASCII text as is.
    /// </summary>
    internal static class Json
    {
        public static string Write(object value)
        {
            var sb = new StringBuilder();
            Value(sb, value, 0);
            return sb.ToString();
        }

        private static bool Scalar(object v) => !(v is IDictionary) && !(v is IList);

        /// <summary>The pairs of a map in its own key order (a SortedDictionary enumerates KeyValuePairs, not DictionaryEntries).</summary>
        public static List<DictionaryEntry> Entries(IDictionary map) => map.Keys.Cast<object>().Select(k => new DictionaryEntry(k, map[k])).ToList();

        private static void Value(StringBuilder sb, object v, int depth)
        {
            switch (v)
            {
                case null: sb.Append("null"); break;
                case string s: Str(sb, s); break;
                case bool b: sb.Append(b ? "true" : "false"); break;
                case long l: sb.Append(l.ToString(CultureInfo.InvariantCulture)); break;
                case int i: sb.Append(i.ToString(CultureInfo.InvariantCulture)); break;
                case double d: sb.Append(d.ToString("0.####", CultureInfo.InvariantCulture)); break;
                case IDictionary map:
                {
                    var pairs = Entries(map);
                    if (pairs.Count == 0) { sb.Append("{}"); break; }
                    if (pairs.All(p => Scalar(p.Value)))
                    {
                        sb.Append("{ ");
                        for (int i = 0; i < pairs.Count; i++) { if (i > 0) sb.Append(", "); Str(sb, (string)pairs[i].Key); sb.Append(": "); Value(sb, pairs[i].Value, depth + 1); }
                        sb.Append(" }");
                        break;
                    }
                    sb.Append("{\n");
                    for (int i = 0; i < pairs.Count; i++)
                    {
                        sb.Append(' ', 2 * (depth + 1));
                        Str(sb, (string)pairs[i].Key);
                        sb.Append(": ");
                        Value(sb, pairs[i].Value, depth + 1);
                        sb.Append(i + 1 < pairs.Count ? ",\n" : "\n");
                    }
                    sb.Append(' ', 2 * depth).Append('}');
                    break;
                }
                case IList list:
                {
                    var items = list.Cast<object>().ToList();
                    if (items.Count == 0) { sb.Append("[]"); break; }
                    if (items.All(Scalar))
                    {
                        sb.Append('[');
                        for (int i = 0; i < items.Count; i++) { if (i > 0) sb.Append(", "); Value(sb, items[i], depth + 1); }
                        sb.Append(']');
                        break;
                    }
                    sb.Append("[\n");
                    for (int i = 0; i < items.Count; i++)
                    {
                        sb.Append(' ', 2 * (depth + 1));
                        Value(sb, items[i], depth + 1);
                        sb.Append(i + 1 < items.Count ? ",\n" : "\n");
                    }
                    sb.Append(' ', 2 * depth).Append(']');
                    break;
                }
                default: throw new InvalidOperationException("JSON: unsupported value " + v.GetType());
            }
        }

        private static void Str(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }
    }
}
