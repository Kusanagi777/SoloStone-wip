# Dungeon Run catalog generator

Writes the shared draft catalog of the unified Dungeon Run:

- `data/dungeon-run/catalog-v1.json`: every draftable entry. The 52 fixed SoloStone bundles of `pool-v1.json`
  (`draw: fixed`, `legacy: true`) and one pool entry per original adventure bucket (`draw: pool`), with its admitted
  members, its excluded members and their reasons, classes, requirements, themes and provenance. Embedded in the plugin
  (`SoloStone.DungeonRun.catalog-v1.json`) and loaded by `DungeonRunData.Parse`.
- `docs/design/dungeon-run-catalog.md`: counts, every exclusion, every merge, grants, reprint resolutions and themes.

Both files are generated. Change the inputs below and run the generator again; never edit the outputs by hand. The
plugin refuses a catalog whose `poolBundlesHash` does not match `pool-v1.json` ("catalog-v1.json is stale: run
tools/dungeon-run-catalog"), and the validator rejects a pool candidate the engine or the admission table no longer
admits, so a stale catalog disables the mode instead of drafting wrong cards.

## Run

```sh
cd tools/dungeon-run-catalog
dotnet run -c Release
```

This builds the engine (`engine/SabberStoneCore`, net10.0) through a project reference, then runs in a few seconds.
The engine's `obj\` folder is shared with every other engine build; do not run two engine builds at the same time.
To use an engine DLL that is already built instead: `dotnet run -c Release -p:EngineDll=C:\path\to\net10.0\SabberStoneCore.dll`.

| Option | Default |
|---|---|
| `--repo DIR` | the folder above this one that holds `plugin/` and `engine/` |
| `--out FILE` | `data/dungeon-run/catalog-v1.json` |
| `--out-md FILE` | `docs/design/dungeon-run-catalog.md` |

The output is deterministic: the same inputs give byte-identical files (UTF-8 without BOM, LF line endings, object
keys in ordinal order). The catalog's `sources` holds the SHA-256 of every data input. The tool loads its own output
with the run layer's parser and validator before writing and exits 2 if validation reports an error.

## What it reads

| Input | Used for |
|---|---|
| Loaded engine (`Cards.All`, `OfflinePoolPolicy`, `DungeonRunContent`) | existence, `Implemented`, class, cost, quest tag; SoloStone's content shells (custom cards) |
| `plugin/DungeonRun/DungeonRunData.cs`, `DungeonRunValidator.cs`, `DungeonRunAdmission.cs`, `plugin/Npc/World/NpcWorldJson.cs` | compiled in unchanged: the parser, `PoolCardReason`, `HasRuneCost`, `Classify` |
| `data/dungeon-run/reference/buckets.json` | the 440 original bucket labels and their members (primary id and reprint alternates) |
| `data/dungeon-run/reference/adventures.json` | the Tombs of Terror guest heroes' class pairs (checked against the bucket classes) |
| `data/dungeon-run/pool-v1.json`, `run-v1.json`, `data/npc/decks-v1.json` | the fixed bundles, starter decks (for the `cards` index) and the run data the self-check validates |
| `facts/violet-hold-2026-09.json` | card names for the report |
| `curation.json` (this folder) | label typo fixes, granting heroes, distinct (Unique) labels, hand exclusions, exclusion notes, entry weights, hand theme tags, adaptations |

## How a member is decided

The member's listed id is tried first, then each reprint alternate in order; the first id that passes every check is
the one drawn (`id`; `wikiId` keeps the listed id). When none passes, the member is excluded with the first failing
check of the first id the engine knows:

1. `engine:missing`: not in the engine card data.
2. `engine:unscripted`: `Implemented` is false. When the admission table also rejects it, both are recorded
   (`engine:unscripted; admission:highlander` for Zephrys the Great).
3. `admission:<family>`: `DungeonRunAdmission.Classify` does not admit it for the player.
4. `validator:<check>`: `DungeonRunValidator.PoolCardReason` with the entry's class set fails (`not-in-active-meta`,
   `deck-size`, `sideboard`, `first-player`, `class`). In a granting hero's bucket (Monster Hunt Shaw and Tess) an
   off-class card that passes every other check is admitted as a `grant`, legal inside that entry only.
5. `rune-cost`: drafted cards carry no Death Knight rune cost.
6. `curation:<note>`: a hand exclusion in `curation.json` (it must give a reason).

An entry with fewer than 3 admitted members (distinct members for a `distinct` entry) is written `offerable: false`
with `withdrawn: <reason>`. Two offerable entries that differ only in their hero and provenance (same adventure, label,
class set, requirements, distinct flag, quest, themes and admitted members) are merged into one entry with both
provenances; every other entry keeps its own provenance, grouped by `labelKey`. An offerable entry no playable class row
can be offered (`DungeonRunBundle.EligibleFor` with `DungeonRunAdmission.MechanicHolds` over every enabled row: a Tombs pair
whose `catalogClasses` row ships disabled) is written `unreachable: <reason>` and counted apart
(`reachableOfferablePoolEntries`, `admittedReferencesReachable`).

The game checks the one fact the tool cannot: whether the client knows each candidate (`clientChecked: false`). At load
it drops a client-unknown candidate, logs `catalog: <entry> dropped <id> client-unknown`, counts it on the class page,
clears a dropped quest card (`catalog: <entry> quest <id> dropped client-unknown`), and withdraws an entry left with
fewer than 3.

## Encounters (phase 8)

`dotnet run -c Release -- encounters` builds the imported adventure encounters instead of the catalog:

- reads `data/dungeon-run/reference/encounters/F<NN>.json` (one file per encounter family: boss rows and their deck lists
  by card name, transcribed from the wiki page each row names);
- resolves every name against `facts/violet-hold-2026-09.json` (the collectible original printing; an ambiguous
  boss-only card carries its `id`), and checks every card: an engine card, a client card, scripted, admitted for its side
  on a catalog run (`DungeonRunAdmission.Classify`);
- builds each deck: a combat tier (`"1"`..`"4"`) holds exactly 30 cards, a major, a finale boss and a form deck keep the
  listed count (10 to 60). A card that fails is replaced by the row's `replacements` entry, else dropped; a short deck is
  filled with second copies of its non-Legendary singles, then the row's `extend` lists, then its `fillers`. The step
  fails, writing nothing, when a combat deck cannot reach 30;
- writes `data/dungeon-run/encounters-v1.json` (embedded in the plugin as `SoloStone.DungeonRun.encounters-v1.json`) and
  the two tool-owned lists `encounterAdaptations` and `encounterRejections` at the end of
  `tools/restoration-ledger/curation.json`. Every deck that is not the listed cards unchanged is one adaptation entry.

Run it after any change to an F file, and again when a card a deck dropped as `unscripted` gets its script. Like every
engine build, never run it while another engine build is running.
