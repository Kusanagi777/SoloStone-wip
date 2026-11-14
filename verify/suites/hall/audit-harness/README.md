# Dungeon Run card-audit harness

A headless test runner on the SoloStone engine (`engine/SabberStoneCore` in this repository). It builds a Dungeon Run
match, places cards, plays them, ends turns and reads state. Nothing in here writes to the repository.

(Ported into `verify/suites/hall/`: the commands below are the repository-relative forms. The normal way to build and
run the whole hall is `bash verify/bin/hall.sh harness`, `bash verify/bin/hall.sh 1 38`, `bash verify/bin/hall.sh same`;
see `verify/README.md`.)

Card list to audit: `verify/fixtures/audit-inventory/cards.tsv` (columns: id, name, type, class, cost, atk, hp,
implemented, sources, parents, via, randomgen, chunk, text). `chunk` names the work chunk each card belongs to (the
original `chunks.json` with the same split was not carried over). For a run shell (treasure, power, shrine, boss
passive) the `name`/`text` columns are the borrowed client card's; the run name and text are in
`engine/SabberStoneCore/src/CardSets/DungeonRun/DungeonRunContent.cs` (the `sources` column names the content key).

## Copy it, don't edit it

Each auditor works in their own copy, a sibling folder under `verify/suites/hall/` (bash, from the repository root):

```sh
bash verify/bin/engine.sh                             # once: the engine build every harness references
mkdir -p verify/suites/hall/audit-<role>
cp -r verify/suites/hall/audit-harness/{AuditHarness.csproj,Lib.cs,Runner.cs,Tests} verify/suites/hall/audit-<role>/
cd verify/suites/hall/audit-<role>
dotnet build -c Release -nologo -v q -o out           # ~10 s
dotnet out/AiHarness.dll --list                       # test names
dotnet out/AiHarness.dll Example_Smoke                # run one test by exact name
dotnet out/AiHarness.dll 'EDR_810t_*' 'CS2_172_*'     # a trailing * is a prefix match
dotnet out/AiHarness.dll --all
VERBOSE=1 dotnet out/AiHarness.dll Example_Smoke      # also print passing checks
```

No arguments prints usage and runs nothing. Exit code: 0 all passed, 1 a check failed or a test threw, 2 a named
test does not exist. The last lines are `SUMMARY ...` and one `FAILED <test> :: <check> (detail)` per failure.

## Engine build

The harness references `$(EngineDir)/SabberStoneCore.dll`; `verify/Directory.Build.props` sets `EngineDir` to
`verify/.out/engine-out`, the Release build (net10.0) of the current engine tree made by `bash verify/bin/engine.sh`.

If you change engine source (a fix), rerun `bash verify/bin/engine.sh`, then rebuild the harness. The engine's `obj/`
folder is shared by every engine build of the clone, so do not run two engine builds at the same time; if a build
fails with a file-in-use error, wait and retry. To point the harness at another engine build:
`dotnet build -c Release -o out -p:EngineDir=<path/to/folder>`.

The assembly is named `AiHarness` because the engine grants `InternalsVisibleTo` to that name, so internal engine
types and members (Generic, EffectContext, DungeonRunContent internals) are usable from tests.

## Writing tests

Add `.cs` files under `Tests\` (all `.cs` files in the folder are compiled). A test is a static, parameterless method
with `[AuditTest]` (optional name: `[AuditTest("EDR_810t_Name")]`). Name tests `<CardId>_<Behaviour>`. See
`Tests\Examples.cs`.

```csharp
using System.Linq;
using SabberStoneCore.Model.Entities;
using static Audit.H;

namespace Audit
{
    internal static class LeechTests
    {
        [AuditTest]
        static void EDR_810t_StealsFromMinion()
        {
            var g = NewGame();                       // player 1's turn, both hands empty, 10 mana
            var p = P1(g); var o = P2(g);
            Board(p, "EDR_810t");
            var foe = Board(o, Yeti);                // 4/5; boss hero has 30
            EndTurn(g);
            Eq("victim max Health -1", 4, MaxHp(foe));
            Check("hero gained", p.Hero.BaseHealth == 31, Describe(g));
        }
    }
}
```

### Helpers (`Lib.cs`, class `Audit.H`; `using static Audit.H;`)

| Helper | Does |
|---|---|
| `NewGame(s => { ... })` | Dungeon Run match past the mulligan, on player 1's turn 1. `Setup` fields: `PlayerHero` (HERO_08), `BossHero` (HERO_01), `PlayerDeck`/`BossDeck` (lists of ids; default 10/30 Yetis), `PlayerPower`/`BossPower` (run key like `druid_rootcall`, `passive_tithe`, `shrine_dawn_2`, or a card id), `Treasures` (passive treasure keys), `BossHealth` (30), `Seed` (99), `Shuffle`, `RunSetup` (apply treasures, true), `ClearHands` (true), `Mana` (10 for player 1), `BossSideboards`, `History` |
| `P1(g)`, `P2(g)` | Player (Player1) and boss (Player2) |
| `Hand(c, id)` / `HandContent(c, key)` | Create a card in hand (any card id / a DungeonRunContent key such as `blazing_rod`) |
| `Board(c, id, pos)` | Summon a minion straight to the board (no Battlecry; "after summon" triggers fire). Null if full |
| `DeckTop(c, id)`, `ClearHand(c)`, `ClearDeck(c)` | Deck/hand setup |
| `Mana(c, n)`, `Corpses(c, n)`, `CorpseCount(c)` | Resources (Death Knight corpses = `GameTag.CORPSES`) |
| `Play(g, card, target, chooseOne, pos, keepMana)` | Play a card from hand (tops mana to 10 unless `keepMana`). Returns `g.Process` result |
| `PlayNew(g, c, id, target, chooseOne, pos)` | `Hand` + `Play`, returns the entity |
| `Power(g, c, target, chooseOne)` | Use the hero power (refreshed, 10 mana) |
| `Attack(g, minion, target)`, `HeroAttack(g, c, target)` | Attack (minion is readied first) |
| `ChoiceIds(g, c)`, `Choose(g, c, index)` | Read / pick an open Discover or choice |
| `CanPlay(card, target)`, `LegalTargets(card)` | What the engine offers as legal |
| `EndTurn(g)`, `Round(g)` (two end-of-turns), `PassTo(g, c)` | Turn flow. `EndTurn` fires end-of-turn of the current player, then start-of-turn of the other |
| `Damage(g, target, n, source)`, `Kill(g, m)`, `Silence(g, m)` | Direct effects, followed by death processing |
| `Hp`, `MaxHp`, `Atk`, `Armor(c)`, `Weapon(c)` | Read stats (`Hp` = current Health, `MaxHp` = maximum Health) |
| `HandIds`, `BoardIds`, `DeckIds`, `GraveIds`, `SecretIds`, `EnchantIds(entity)` | Zone contents as card ids |
| `Describe(g)` | One-line summary of both sides (use as a check detail) |
| `Clone(g)`, `At(clone, entity)`, `At(clone, controller)` | Exact clone (same RNG) and entity lookup in it; check effects survive cloning |
| `Check(what, ok, detail)`, `Eq(what, expected, actual)`, `Note(text)` | Assertions and notes |
| `CardText(id)` | "Name: text" from the facts |
| `Data`, `BossDeck(bossId)` | Parsed run data (`Data.Decks`, `Data.Bundles`, `Data.Bosses`, `Data.BossDecks`, `Data.BossSideboards`, `Data.Classes`); a boss's deck + sideboards |

Constants: `Yeti` (CS2_182, 4/5), `Wisp` (CS2_231, 1/1), `Golem` (CS2_186, 7/7).

### Tips

- `NewGame` empties both hands, but the boss draws normally on its turn; clear again if a test needs it.
- Board minions placed with `Board` are summoning-sick; `Attack` readies them.
- Randomness is seeded (`Setup.Seed`). For random effects, loop over a few seeds and check the invariant
  (e.g. "always hits an enemy"), not one exact outcome.
- Test the card's text from `cards.tsv` (`text` column) and what the client would show: Health loss vs damage,
  maximum Health vs current Health, which side a trigger fires on, and that the effect also works after `Clone`.
- Engine card scripts: `engine/SabberStoneCore/src/CardSets/` (newer sets under `Client/Sets/<SET>/`,
  one file per card; older sets in `*CardsGen.cs`, search for `[CARD_ID]`).
- Older passing suites to borrow patterns from: the scratchpad suites `content2-content` (powers, treasures, cloning)
  and `content2-secretscan` (plays every pool card with random choices) were not carried into `verify/`; the ported
  suites under `verify/suites/audit-verify/` (for example `c-content`, `c-secretscan`) show the same techniques.
