#!/usr/bin/env python3
# Phase 8 family 19: appends the landed-family entry E19 to tools/restoration-ledger/curation.json, after the last entry of
# "landedFamilies" (a text edit of that one place: nothing else of the file is touched, no key is re-sorted). Idempotent: an
# existing E19 entry is replaced in place. Run: python3 -I verify/tools/gen/p8f19/patch_curation.py
import json
import os

REPO = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "..", ".."))
PATH = os.path.join(REPO, "tools", "restoration-ledger", "curation.json")

IDS = ["ULDA_BOSS_%02d%s" % (n, k) for n in range(1, 19) for k in ("h", "p")] + ["ULDA_030"]
NOTE = (
    "Phase 8 family 19, Tombs of Terror bosses A: the eighteen Tombs of Terror encounters ULDA_BOSS_01 to ULDA_BOSS_18 and Sinkhole's "
    "Sand Trap ULDA_030 (reference/encounters/F19.json, written by verify/tools/gen/p8f19/make_f19.py; scripts in "
    "DungeonRunEncounters.F19.cs, nested class TombsA; admission rows in DungeonRunAdmission.F19.cs). Every Tombs boss hero prints "
    "10 Health (the adventure set Health by level server-side): the fight's Health is the act curve's and the tier is the boss's "
    "level band from its wiki page's deck table (\"10; 15\" possible cards: levels 1-2, tier 1; \"15; 20\": levels 2-3, tier 2; a "
    "25-card level-4 list: levels 3-4, tier 2; a 30-card level-6 list: levels 5-6, tier 3; a full 30-card list with no lower-level "
    "version: level 7, tier 4; verify/tools/gen/p8f19/tombs_tiers.py, for the later Tombs families too). Thirteen clickable powers "
    "(Boom Bullets, Mummy Ritual, Belch, Venomous Stinger, Druid of the Rat, Sand Surge, Armor Repairs, Well Equipped, Brick by "
    "Brick, Snakeshot, Puffertrooper, Spreading the Curse, Plagued Horde) and five passive ones (E.V.I.L. Promotion: a played "
    "Lackey gets +N/+N, N rising per Lackey and kept in the power's own script number; \"Confiscated\" Goods; Unstable Weaponry; "
    "Sword Dance: a destroyed weapon's copy with its enchantments is shuffled into the boss's deck, Kingsbane's rule; Under Wraps: "
    "Reborn for each minion the boss plays). Mummy Ritual and Belch read the graveyards (public) and never raise a dead run shrine. "
    "Sand Surge shuffles Sand Trap into the run player's deck as a public shuffle; drawn, it moves the top two cards of its holder's "
    "deck to the graveyard and draws (Casts When Drawn); it is never written into the run deck records. Brick by Brick is a "
    "boss-side Discover of an Elemental behind the generation filter, the boss's AI picking. Decks are the wiki's Normal lists: two "
    "unchanged (Nash, Sirinell); shorter lists are brought to 30 by second copies and the boss's Heroic list; Saviors of Uldum "
    "collectibles the engine does not script yet (Murmy, Generous Mummy, Kobold Sandtrooper, Injured Tol'vir, Sunstruck Henchman, "
    "Weaponized Wasp, Sinister Deal, Livewire Lance, Crystal Merchant, Quicksand Elemental, Living Monument, Spitting Camel, "
    "Expired Merchant, EVIL Recruiter; also Ogre Brute and Hunter's Pack) are replaced by named scripted cards of the same role, "
    "each recorded by the encounters step. Boss heroes need no script. None is rejected. Every row sits on acts-v3h and acts-v3t, "
    "which no new run follows until schedule-v1.json \"defaults\" names them (blocked_by run:default-schedule)."
)

with open(PATH, encoding="utf-8") as f:
    text = f.read()
entry = '    {"id": "E19", "note": ' + json.dumps(NOTE, ensure_ascii=False) + ",\n     \"ids\": " + json.dumps(IDS) + "}"
lines = text.split("\n")
existing = [i for i, line in enumerate(lines) if line.startswith('    {"id": "E19", ')]
if existing:
    i = existing[0]
    lines[i:i + 2] = entry.split("\n")
else:
    start = next(i for i, line in enumerate(lines) if line.startswith('  "landedFamilies": ['))
    end = next(i for i in range(start, len(lines)) if lines[i] == "  ],")
    last = end - 1
    if not lines[last].endswith("}"):
        raise SystemExit("unexpected landedFamilies layout at line %d" % (last + 1))
    lines[last] = lines[last] + ","
    lines[end:end] = entry.split("\n")
out = "\n".join(lines)
json.loads(out)
with open(PATH, "w", encoding="utf-8", newline="\n") as f:
    f.write(out)
print("E19 landed family: %d ids" % len(IDS))
