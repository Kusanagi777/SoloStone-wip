#!/usr/bin/env python3
"""Step 9 (plan section 1 B, lane M): aggregates the run harness's logs (verify/suites/p9/c-bosspilot-run) into one JSON.

    python3 -I verify/tools/balance/agg.py <log> [<log> ...] > agg.json

Each log holds the harness's "G" lines (one per fight: key=value fields, then the timing) and, for MODE=longrun, its "R" lines
(one per run). Give each measurement once: the k = 1 logs of a mode run twice (bp.sh writes verify/.out/bp/logs/<name>-1.txt and
-2.txt); a fight that appears twice with the same fields is counted once, with different fields it is an error (exit 2), since a
twice-run mode must be identical. A win is result=player; result=boss, turn-cap and tie are losses; result=FAULT is counted apart
and never in a rate.
"""
import json
import re
import sys

FIELD = re.compile(r"(\w+)=(\S+)")
TIMING = re.compile(r"\s+[0-9.]+s$")
SLOTS = ["I-combat", "I-major", "II-combat", "II-major", "III-combat", "III-major"]


def parse(path):
    for raw in open(path, encoding="utf-8"):
        line = TIMING.sub("", raw.rstrip("\r\n"))
        if not (line.startswith("G ") or line.startswith("R ")):
            continue
        f = dict(FIELD.findall(line))
        f["_kind"] = line[0]
        f["_line"] = line
        yield f


def key(f):
    return (f["_kind"], f.get("mode"), f.get("band"), f.get("g"), f.get("fight"), f.get("row"), f.get("run"), f.get("treasure"),
            f.get("floor") if f.get("mode") == "longrun" else None)


def counts():
    return {"n": 0, "win": 0, "boss": 0, "cap": 0, "tie": 0, "escape": 0}


def add(c, f):
    r = f.get("result")
    c["n"] += 1
    if r == "player":
        c["win"] += 1
    elif r == "boss":
        c["boss"] += 1
    elif r == "turn-cap":
        c["cap"] += 1
    elif r == "tie":
        c["tie"] += 1
    elif r == "escape":
        c["escape"] += 1


def damage(f):
    m = re.match(r"(\d+)/(\d+)$", f.get("boss", ""))
    return None if not m else int(m.group(2)) - int(m.group(1))


def main(paths):
    seen = {}
    problems = []
    for p in paths:
        for f in parse(p):
            k = key(f)
            if k in seen:
                if seen[k]["_line"] != f["_line"]:
                    problems.append("differs: " + f["_line"])
                continue
            seen[k] = f
    out = {"logs": paths, "faults": 0, "failedChecks": 0, "slots": {}, "finale": {"fights": {}, "joint": {"runs": 0, "allWon": 0}},
           "tekahn": counts(), "outliers": {}, "longrun": {"runs": [], "fights": {}}, "treasure": {}, "named": {}}
    finale_runs = {}
    for f in seen.values():
        if f["_kind"] == "R":
            out["longrun"]["runs"].append({k: f.get(k) for k in ("run", "origin", "sched", "fights", "won", "last", "end", "loadout")})
            continue
        if f.get("result") == "FAULT":
            out["faults"] += 1
            continue
        if f.get("checks", "ok") != "ok":
            out["failedChecks"] += 1
        mode = f.get("mode")
        if mode == "slots":
            band = out["slots"].setdefault(f["band"], {"all": counts(), "bySchedule": {}, "lords": counts(), "nonLords": counts(), "lordDamage": []})
            add(band["all"], f)
            add(band["bySchedule"].setdefault(f.get("sched"), counts()), f)
            if f.get("lord") == "1":
                add(band["lords"], f)
                d = damage(f)
                if d is not None:
                    band["lordDamage"].append(d)
            else:
                add(band["nonLords"], f)
        elif mode == "finale":
            add(out["finale"]["fights"].setdefault(f["fight"], counts()), f)
            finale_runs.setdefault(f["g"], {})[f["fight"]] = f.get("result") == "player"
        elif mode == "tekahn":
            add(out["tekahn"], f)
        elif mode == "outliers":
            row = out["outliers"].setdefault(f["row"], dict(counts(), room=f.get("room") == "1", tier=f.get("tier"), at=f.get("at")))
            add(row, f)
        elif mode == "longrun":
            add(out["longrun"]["fights"].setdefault(f.get("slot"), counts()), f)
        elif mode == "treasure":
            out["treasure"][f["treasure"]] = {k: f.get(k) for k in ("pool", "active", "inplan", "played", "result", "loadout", "checks")}
        elif mode == "named":
            add(out["named"].setdefault(f["row"], counts()), f)
    fights_per_run = max((len(v) for v in finale_runs.values()), default=0)
    for g, fights in finale_runs.items():
        if len(fights) == fights_per_run:
            out["finale"]["joint"]["runs"] += 1
            out["finale"]["joint"]["allWon"] += all(fights.values())
    out["finale"]["joint"]["fightsPerRun"] = fights_per_run
    # Longrun: which slots each run reached (a fight played there), for B10 and B11.
    reached = {}
    for f in seen.values():
        if f["_kind"] == "G" and f.get("mode") == "longrun":
            reached.setdefault(f["run"], set()).add(int(f["floor"]))
    slot_floor = {}
    for f in seen.values():
        if f["_kind"] == "G" and f.get("mode") == "longrun":
            slot_floor.setdefault(f.get("sched"), {})[int(f["floor"])] = f.get("slot")
    out["longrun"]["slotOfFloor"] = {s: {str(k): v for k, v in sorted(m.items())} for s, m in slot_floor.items()}
    for r in out["longrun"]["runs"]:
        r["floorsPlayed"] = sorted(reached.get(r["run"], []))
    out["problems"] = problems
    json.dump(out, sys.stdout, indent=1, sort_keys=True)
    sys.stdout.write("\n")
    return 2 if problems else 0


if __name__ == "__main__":
    if len(sys.argv) < 2:
        sys.stderr.write(__doc__)
        sys.exit(2)
    sys.exit(main(sys.argv[1:]))
