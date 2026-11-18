#!/usr/bin/env python3
# Phase 8 family 20: appends the landed-family entry E20 to tools/restoration-ledger/curation.json, after the last entry of
# "landedFamilies" (a text edit of that one place: nothing else of the file is touched, no key is re-sorted). Idempotent: an
# existing E20 entry is replaced in place. Run: python3 -I verify/tools/gen/p8f20/patch_curation.py
import json
import os

REPO = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "..", ".."))
PATH = os.path.join(REPO, "tools", "restoration-ledger", "curation.json")

IDS = ["ULDA_BOSS_%02d%s" % (n, k) for n in range(19, 37) for k in ("h", "p")] + ["ULD_705"]
NOTE = (
    "Phase 8 family 20, Tombs of Terror bosses B: the eighteen Tombs of Terror encounters ULDA_BOSS_19 to ULDA_BOSS_36 and Sothis's "
    "Mogu Cultist ULD_705 (a Saviors of Uldum collectible that was a stub, scripted in its own entry of Standard/UldumCardsGen.cs "
    "with its Highkeeper Ra ULD_705t; rules in DungeonRunEncounters.F20.cs, nested class TombsB) (reference/encounters/F20.json, "
    "written by verify/tools/gen/p8f20/make_f20.py; admission rows in DungeonRunAdmission.F20.cs). Every Tombs boss hero prints 10 "
    "Health: the fight's Health is the act curve's and the tier is the boss's level band from its wiki page's deck table (family "
    "19's rule, verify/tools/gen/p8f19/tombs_tiers.py). Fourteen clickable powers (Titan Ritual, Power of Wrath, Hatching Time, "
    "Corrupted Oasis, Beetle Stampede, Gatling Wand, \"Retired\" Treasure Hunter, Mighty Windstorm, Pillaged Relics, Whispering "
    "Sentinel, Out of Control, Tomb Boomin', Vengeful Construct, Madness Incarnate) and four passive ones (Spectral Swordsman: the "
    "boss's hero has Stealth while it has a weapon equipped; Staking A Claim: every minion gains +1/+1 whenever either player plays "
    "a Discover card; Mother of Sand; Fireshaper). \"Retired\" Treasure Hunter copies a random card of the run player's hand (the boss "
    "learns that one card) and Mighty Windstorm makes each player swap a random hand minion with a random board minion (never a run "
    "shrine); in the boss's fair-view search the run player's hand is masked, so there both use only cards the boss saw go into it. "
    "Out of Control hits the center-most minions of each board, Vengeful Construct a chosen minion with 2 or less Health and its "
    "neighbours (a run shrine goes dormant), Madness Incarnate makes the boss's hero attack three different random enemy minions "
    "(forced attacks). Decks are the wiki's Normal lists: three unchanged (Colossus of the Sun, Runaway Ruby, Earthrager Ptah); "
    "shorter lists are brought to 30 by second copies and the boss's Heroic list (Zenda and Edra with one curated filler each); "
    "Ol' Toomba's 32-card table keeps one copy of each of its two Legendaries; collectibles the engine does not script yet "
    "(Conjured Mirage, Cursed Blade, Murmy, Garden Gnome, Bug Collector, Crystal Merchant, Sahket Sapper, Quicksand Elemental, "
    "Injured Tol'vir, Plague of Madness) and Hydrologist (its Discover offers Secrets) are replaced by named scripted cards of the "
    "same role, each recorded by the encounters step. Boss heroes need no script. None is rejected. Every row sits on acts-v3h and "
    "acts-v3t, which no new run follows until schedule-v1.json \"defaults\" names them (blocked_by run:default-schedule)."
)

with open(PATH, encoding="utf-8") as f:
    text = f.read()
entry = '    {"id": "E20", "note": ' + json.dumps(NOTE, ensure_ascii=False) + ",\n     \"ids\": " + json.dumps(IDS) + "}"
lines = text.split("\n")
existing = [i for i, line in enumerate(lines) if line.startswith('    {"id": "E20", ')]
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
print("E20 landed family: %d ids" % len(IDS))
