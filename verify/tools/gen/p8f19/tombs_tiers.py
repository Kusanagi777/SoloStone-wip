#!/usr/bin/env python3
# The Tombs of Terror tier rule (phase 8 family 19; for the Tombs families 19-22). Import it from a family generator:
#   sys.path.insert(0, os.path.join(<repo>, "verify", "tools", "gen", "p8f19")); import tombs_tiers
# (python3 -I keeps the script's own folder off sys.path, so the generator inserts it.)
#
# Every Tombs boss hero prints 10 Health: the adventure set a boss's Health by its level, server-side, so a row takes no
# "health" (the fight's Health is the act curve's at its floor) and its tier comes from the boss's LEVEL in its chapter. The
# Tombs of Terror page: a chapter is eight bosses of rising difficulty, the eighth its Plague Lord. A boss page sizes the
# boss's deck by level, and that size gives the level band:
#   "1"   a table "Total cards 10; 15" of possible cards without counts: levels 1-2 (10 cards on 1, 15 on 2)  -> tier 1, level 2
#   "2a"  a table "Total cards 15; 20": levels 2-3 (15 on 2, 20 on 3); the upper level is taken          -> tier 2, level 3
#   "2"   a 25-card list ("These decks correspond only to level 4. If this boss is encountered on level 3, his deck will
#         be missing 5 random cards", or the same 25-card shape without the note): levels 3-4               -> tier 2, level 4
#   "3"   a 30-card list with the note "correspond only to level 6 ... on level 5 ... missing 5 random cards": levels 5-6
#                                                                                                            -> tier 3, level 6
#   "4"   a full 30-card list with no lower-level note: level 7, the last boss of a chapter before its Plague Lord (the one
#         level whose deck has no smaller version)                                                           -> tier 4, level 7
# These are the design's level bands (handoff/phase8/reports/design.md 1.2: tier 1 = levels 1-2, 2 = 3-4, 3 = 5-6, 4 = 7-8
# non-final). The chapter a page names ("can be encountered initially in Chapter N"; the client's hero tags 1202-1205 say the
# same) is recorded, not used: after its chapter a boss is met in any completed chapter, so the chapter does not set its level.
# Pages of chapter 5 ("level 6 in Chapter 5 ... on levels 4-5"): not met by family 19; a later family states its own band.
# The row records "level": {"source": "wiki-level-table", "original": <level>} and one "schedule" adaptation (schedule_note).

SHAPES = {
    "1": ("1", 2, "levels 1-2 (a \"10; 15\" table of possible cards: 10 cards on level 1, 15 on level 2) = tier 1"),
    "2a": ("2", 3, "levels 2-3 (a \"15; 20\" table: 15 cards on level 2, 20 on level 3); the upper level, 3, = tier 2"),
    "2": ("2", 4, "levels 3-4 (the 25-card level-4 list; on level 3 five random cards are missing) = tier 2"),
    "3": ("3", 6, "levels 5-6 (the 30-card level-6 list; the page notes that on level 5 five random cards are missing) = tier 3"),
    "4": ("4", 7, "level 7 (a full 30-card list with no smaller lower-level version, the shape of a chapter's seventh boss, the last "
                  "before its Plague Lord) = tier 4"),
}


def tier(shape):
    """The row's "tier" for a deck-table shape."""
    return SHAPES[shape][0]


def level(shape):
    """The row's "level.original" (the level whose list the row uses) for a deck-table shape."""
    return SHAPES[shape][1]


def schedule_note(chapter, shape):
    """The row's "schedule" adaptation: the Health policy and the tier rule applied to this row."""
    return {"kind": "schedule", "note": "Tombs boss heroes print 10 Health (the adventure set a boss's Health by level server-side): "
            "the fight's Health is the act curve's and the tier is the boss's level in its chapter, from its wiki page's deck table: "
            + SHAPES[shape][2] + ". The page names chapter " + str(chapter) + " as the chapter the boss is first met in (after it, any "
            "completed chapter), which does not set its level."}


def deck_note(shape):
    """The row's "deck" adaptation for a list shorter than 30 (None for a 30-card list)."""
    if shape in ("1", "2a"):
        sizes = "10 or 15" if shape == "1" else "15 or 20"
        band = "1-2" if shape == "1" else "2-3"
        return {"kind": "deck", "note": "The wiki lists the possible cards of the level " + band + " deck (" + sizes + " of them, no "
                "counts). A combat tier fights with 30: each listed card once, a second copy of each, then the Heroic table's cards "
                "(once, then twice) until 30."}
    if shape == "2":
        return {"kind": "deck", "note": "The wiki's level-4 list has 25 cards. A combat tier fights with 30: the listed cards, a second "
                "copy of each non-Legendary single in table order, then the Heroic table's cards (once, then twice) until 30."}
    if shape == "3":
        return {"kind": "deck", "note": "The wiki's list is the level 6 deck; on level 5 the original removes 5 random cards. Here the "
                "row always uses the level 6 list."}
    return None
