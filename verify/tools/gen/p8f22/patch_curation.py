#!/usr/bin/env python3
# Phase 8 family 22: appends the landed-family entry E22 to tools/restoration-ledger/curation.json, after the last entry of
# "landedFamilies" (a text edit of that one place: nothing else of the file is touched, no key is re-sorted). Idempotent: an
# existing E22 entry is replaced in place. Run: python3 -I verify/tools/gen/p8f22/patch_curation.py
import json
import os

REPO = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "..", ".."))
PATH = os.path.join(REPO, "tools", "restoration-ledger", "curation.json")

BOSSES = [60, 61, 62, 63, 65, 66, 68, 69, 70, 71, 72, 73, 74, 75, 76, 77, 78, 79]
IDS = ["ULDA_BOSS_%02d%s" % (n, k) for n in BOSSES for k in ("h", "p")] + ["ULDA_026"]
NOTE = (
    "Phase 8 family 22, Tombs of Terror bosses D: the eighteen Tombs of Terror encounters ULDA_BOSS_60 to ULDA_BOSS_79 without 64 "
    "and 67 and Siamat's Servant ULDA_026 (reference/encounters/F22.json, written by verify/tools/gen/p8f22/make_f22.py; scripts in "
    "DungeonRunEncounters.F22.cs, nested class TombsD; admission rows in DungeonRunAdmission.F22.cs). The tier rule is family 19's "
    "(verify/tools/gen/p8f19/tombs_tiers.py: the boss's level band from its wiki page's deck table; Health is the act curve's). "
    "Twelve clickable powers (Forbidden Power and Hunt the Weak keep their growing number in the power's own script number; Chain "
    "Lightning jumps to random adjacent minions until one dies; Ogre-Sized Thief steals a random card of the run player's deck into "
    "the boss's hand by TRLA_186's deck-steal rule, the boss learning that card only; Masterful Djinn summons the 1/4 Servant with "
    "a random keyword; \"Upgraded\" Fauna, Lord of the Sun, Lord of Life, Burst of Light, Noble Champion, Snapping Jaw; Mech "
    "\"Pilot\" forces a Mech to attack a random enemy minion, Actions.EncounterRules.ForcedAttack), three passive ones "
    "(Plaguebringer, Mistress of Magic on both players, Shockwave) and three the wiki lists as auto-cast, cast by rule at the "
    "start of the boss's turn (Mirage: a copy of a random card of the run player's hand joins that hand and one of the two is the "
    "illusion, whose play makes its player discard a random card and which leaves the hand at the end of that player's turn; "
    "the client has no illusion token and the wiki's notes describe it, private tag Actions.EncounterRules.MirageIllusion; Lord of "
    "Destruction: a stacking Cursed! enchantment; Massive Tentacles: every minion destroyed at 8 or more, a run shrine goes "
    "dormant). Decks are the wiki's Normal lists; shorter lists are brought to 30 by second copies and the boss's Heroic list; "
    "cards the engine does not script yet (Saviors of Uldum collectibles such as Faceless Lurker, Garden Gnome, Fishflinger, "
    "Conjured Mirage, Pit Crocolisk, EVIL Totem, Mischief Maker, Spitting Camel, Livewire Lance, Sinister Deal, EVIL Recruiter, "
    "Injured Tol'vir, Kobold Sandtrooper; also Ogre Brute and Wrath; Hunter's Pack and Desperate Measures, which generate Secrets) "
    "are replaced by named scripted cards of the same role, each recorded by the encounters step. Boss heroes need no script. "
    "None is rejected. Every row sits on acts-v3h and acts-v3t, which no new run follows until schedule-v1.json \"defaults\" names "
    "them (blocked_by run:default-schedule)."
)

with open(PATH, encoding="utf-8") as f:
    text = f.read()
entry = '    {"id": "E22", "note": ' + json.dumps(NOTE, ensure_ascii=False) + ",\n     \"ids\": " + json.dumps(IDS) + "}"
lines = text.split("\n")
existing = [i for i, line in enumerate(lines) if line.startswith('    {"id": "E22", ')]
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
print("E22 landed family: %d ids" % len(IDS))
