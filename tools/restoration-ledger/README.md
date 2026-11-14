# Restoration ledger generator

Writes the one restoration ledger for the unified Dungeon Run:

- `docs/design/restoration-ledger.csv`: one row per card id or run catalog entry
- `docs/design/restoration-ledger.md`: counts, blocking capability gaps, column and classification definitions

Both files are generated. Change the inputs below and run the generator again; never edit the outputs by hand.

## Run

```sh
cd tools/restoration-ledger
dotnet run -c Release
```

This builds the engine (`engine/SabberStoneCore`, net10.0) through a project reference, then runs. A run takes a few
seconds after the build. Options:

| Option | Default |
|---|---|
| `--repo DIR` | the folder above this one that holds `plugin/` and `engine/` |
| `--reference-csv FILE` | `data/dungeon-run/reference/adventure-reference.csv` |
| `--out-csv FILE` | `docs/design/restoration-ledger.csv` |
| `--out-md FILE` | `docs/design/restoration-ledger.md` |
| `--no-client` | skip the client entity check (otherwise read from `client-tables.json` when it exists) |

To use an engine DLL that is already built instead of building the engine:
`dotnet run -c Release -p:EngineDll=C:\path\to\net10.0\SabberStoneCore.dll`.

The engine's `obj\` folder is shared with every other engine build; do not run two engine builds at the same time.

The output is deterministic: the same inputs give byte-identical files (UTF-8 without BOM, LF line endings). The
`.md` lists the SHA-256 prefix of every input, so a diff shows which input changed.

## What it reads

| Input | Used for |
|---|---|
| Loaded engine (`Cards.All`, `DungeonRunContent`) | names, types, classes, text, `Implemented`, Secret and deck-size tags; SoloStone's powers, treasures, shrines and effects |
| `plugin/DungeonRun/DungeonRunData.cs`, `DungeonRunValidator.cs`, `plugin/Npc/World/NpcWorldJson.cs` | compiled in unchanged: the run data parser and the real admission rules |
| `data/dungeon-run/run-v1.json`, `pool-v1.json`, `data/npc/decks-v1.json` | current classes, starters, bundles, treasures, bosses and boss decks |
| `data/dungeon-run/catalog-v1.json` | the shared draft catalog: which pool entries admit each original bucket member (offered as `bucket <entry>` when a playable class row is eligible for the entry), and each recorded exclusion reason (the validation and `blocked_by` of a member no entry admits); loaded with the run data, so the dual-class rows load too |
| `data/dungeon-run/reference/adventure-reference.csv` | the 2050 original adventure-id records (gap review 2026-11-10 CSV, copied here as LF without BOM) |
| `data/dungeon-run/reference/buckets.json` | original bucket labels and members |
| `data/dungeon-run/reference/adventures.json` | original starter and finale decks, player powers, guest heroes, signature treasures, scenario bosses |
| `data/dungeon-run/reference/client-tables.json` (CARD table only) | the validator's client entity check |
| `plugin/NativeMatch.cs` | the fixed id map of `TransportCardId(string)`, read from the source, so the client check uses the id actually sent |
| `curation.json` (this folder) | facts the inputs above do not hold: treasure names, Monster Hunt player powers, custom analogues, AI-block phrases, named final bosses and finale records (gap review section 9), catalog notes, encounter-owned hero powers (`encounterPowers`), hero base forms that are no portrait (`heroBaseForms`), and the landed phase-6 families with their stated rejections |

Nothing is written anywhere except the two output files.

## How each column is decided

- **engine_behavior**: the engine's own `Implemented` flag. No text and no power is `vanilla-no-script-needed`. When the
  row's id has no script but a reprint or the client id of a current card does (`HERO_06bp` for `CS2_017`), the row
  behaves as that card and says so in `notes`.
- **player_availability / encounter_use**: membership in a current class's starter decks, bundles, powers and offered
  treasures (and their upgrade stages); in a current boss's deck, sideboard, power or portrait. Reprints count, noted
  `via <id>`.
- **validation**: `DungeonRunValidator.Validate` run on synthetic data: each card as a three-copy bundle in its own
  class, each player power as a one-power class, each boss power and hero as a boss on a real boss deck. Errors are
  keyed by id and shortened to rule names. The bundle-only rune-cost rule is a note, not a rejection. The current data is
  validated too and must stay at 0 errors.
- **ai_support**: Secret tag or the phrase lists in `curation.json` (`aiBlocks`). Deck-dependent text in a fixed boss
  deck the current data already uses is `ok:fixed boss deck`.
- **classification** and **blocked_by**: see the `.md` (*Classification rules*, *Blocking capability gaps*).

## Updating

- A phase that adds scripts, catalog entries, encounters or validator rules changes the engine, the run data or the
  validator, then re-runs this generator and commits the regenerated ledger with that change.
- New hand-curated facts go into `curation.json`. New reference data goes under `data/dungeon-run/reference/`.
- A new kind of content (board shrines, anomalies, original boss decks) needs its role, reach and validation mapping
  in `Program.cs` (`AssignCsvRole`, `Availability`, `Validation`, `RoleCapability`), so its rows stop reading
  *not applicable*.
