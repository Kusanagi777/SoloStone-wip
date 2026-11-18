#!/usr/bin/env python3
# Phase 8 family 21: appends the landed-family entry E21 to tools/restoration-ledger/curation.json, after the last entry of
# "landedFamilies" (a text edit of that one place: nothing else of the file is touched, no key is re-sorted). Idempotent: an
# existing E21 entry is replaced in place. Run: python3 -I verify/tools/gen/p8f21/patch_curation.py
import json
import os

REPO = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "..", ".."))
PATH = os.path.join(REPO, "tools", "restoration-ledger", "curation.json")

NUMS = [41, 42, 43, 45, 46, 47, 48, 49, 50, 51, 53, 54, 55, 56, 57, 58, 59]
IDS = []
for n in NUMS:
    IDS.append("ULDA_BOSS_%02dh" % n)
    if n != 58:
        IDS.append("ULDA_BOSS_%02dp" % n)
IDS[IDS.index("ULDA_BOSS_51p") + 1:IDS.index("ULDA_BOSS_51p") + 1] = ["ULDA_BOSS_52h", "ULDA_BOSS_52p1", "ULDA_BOSS_52p2"]
IDS.insert(IDS.index("ULDA_BOSS_58h") + 1, "ULDA_BOSS_58p")
REJECTED = {
    "ULDA_BOSS_58h": "Jythiros of Questions: his power Riddle of the Sphinx needs a decision of the run player on the boss's turn and a Counter "
                     "for every card type (phase 9 / client); the encounter is left out",
    "ULDA_BOSS_58p": "Riddle of the Sphinx (\"Guess which card the Boss will play to banish it\"): a decision of the non-current player in the "
                     "engine's decision loop, NativeMatch and the harnesses, and a Counter for minion, weapon, hero and location plays "
                     "(phase 9 / client)",
}
NOTE = (
    "Phase 8 family 21, Tombs of Terror bosses C: seventeen Tombs of Terror encounters ULDA_BOSS_41 to ULDA_BOSS_59 without 44 (Trap Room, "
    "family 01) and 58 (reference/encounters/F21.json, written by verify/tools/gen/p8f21/make_f21.py; scripts in "
    "DungeonRunEncounters.F21.cs, nested class TombsC; admission rows in DungeonRunAdmission.F21.cs). Tier by family 19's Tombs rule "
    "(verify/tools/gen/p8f19/tombs_tiers.py): Health is the act curve's, the tier the boss's level band from its wiki deck table. Ten "
    "clickable powers (Running Wild, Sand Shapin' with its Summon-when-Drawn mark ULDA_BOSS_42e, Spoil the Ending: a copy of a random "
    "spell of the run player's hand, the boss learning only that card, What's in the Jar?, Demon Emissary, E.V.I.L. Conscription, "
    "Cavalry of E.V.I.L., Fish Food, Titan's Beast, Food Chain), four passive ones (Reliquary's Relics, Desert Warfare, Tol'vir "
    "Necromancy, Sandy Surprise), two cast by rule at the boss's turn start as the wiki's Auto-cast (Maw of the Desert, Arena Expert: "
    "Duel!'s rule) and Lich Baz'hial's power swap inside the match: Lich's Phylactery (restores 20 less 1 per friendly minion death) "
    "breaks when used into Broken Phylactery, the row's script form 1 (power only), which doubles the boss's fatigue damage "
    "(Draw.PreDrawPhase). Jythiros of Questions (ULDA_BOSS_58h/58p) is rejected: Riddle of the Sphinx needs a decision of the run "
    "player on the boss's turn and a Counter for every card type (phase 9 / client). ULDA_BOSS_42d is the client's Sand Shapin "
    "Dummy (a script holder, never created): a support record. Decks are the wiki's Normal lists brought to 30 by second copies and the "
    "boss's Heroic list; unscripted Saviors of Uldum collectibles (Murmy, Generous Mummy, Kobold Sandtrooper, Injured Tol'vir, "
    "Sunstruck Henchman, Sinister Deal, Livewire Lance, Expired Merchant, EVIL Recruiter, Quicksand Elemental) and Secret generators "
    "(Secret Plan, Hunter's Pack) are replaced by named scripted cards, each recorded by the encounters step. Every row sits on acts-v3h "
    "and acts-v3t, which no new run follows until schedule-v1.json \"defaults\" names them (blocked_by run:default-schedule)."
)

with open(PATH, encoding="utf-8") as f:
    text = f.read()
entry = ('    {"id": "E21", "note": ' + json.dumps(NOTE, ensure_ascii=False) + ",\n     \"ids\": " + json.dumps(IDS)
         + ",\n     \"supportRecords\": [\"ULDA_BOSS_42d\"],\n     \"rejected\": " + json.dumps(REJECTED, ensure_ascii=False) + "}")
lines = text.split("\n")
existing = [i for i, line in enumerate(lines) if line.startswith('    {"id": "E21", ')]
if existing:
    i = existing[0]
    j = i
    while not lines[j].rstrip(",").endswith("}"):
        j += 1
    trail = "," if lines[j].endswith(",") else ""
    new = entry.split("\n")
    new[-1] += trail
    lines[i:j + 1] = new
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
print("E21 landed family: %d ids" % len(IDS))
