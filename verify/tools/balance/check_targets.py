#!/usr/bin/env python3
"""Step 9 (plan section 1 B, lane M): checks an agg.py result against verify/fixtures/p9/targets.json.

    python3 -I verify/tools/balance/check_targets.py <agg.json> [--targets <targets.json>] [--strict]

One line per criterion: PASS, MISS, or NO DATA, with the numbers (n, wins, rate, Wilson 95% interval). A band passes when its
Wilson interval overlaps the band and its point estimate lies within the band +- pointTolerance; a band measured with fewer than
targets' acceptance n is marked "indicative (n below N)" (it still gets a verdict). R0 is the product of the measured slot rates
over each schedule's fights (targets.json R0.derivation). A miss sends the work back to calibration (plan stage 4): the exit code
is 0 unless --strict is given, then 1 on any MISS (2 on bad input).
"""
import json
import math
import os
import sys

Z = 1.959963984540054


def wilson(k, n):
    if n == 0:
        return (0.0, 1.0)
    p = k / n
    d = 1 + Z * Z / n
    c = (p + Z * Z / (2 * n)) / d
    h = Z * math.sqrt(p * (1 - p) / n + Z * Z / (4 * n * n)) / d
    return (max(0.0, c - h), min(1.0, c + h))


def fmt(k, n):
    if n == 0:
        return "n 0"
    lo, hi = wilson(k, n)
    return "n %d, wins %d, rate %.3f, CI %.3f-%.3f" % (n, k, k / n, lo, hi)


class Report:
    def __init__(self, need):
        self.need = need
        self.miss = 0
        self.lines = []

    def put(self, verdict, what, detail):
        if verdict == "MISS":
            self.miss += 1
        self.lines.append("%-7s %-22s %s" % (verdict, what, detail))

    def band(self, what, k, n, band, tol, need=None):
        need = need or self.need
        if n == 0:
            self.put("NO DATA", what, "band %.2f-%.2f" % tuple(band))
            return None
        lo, hi = wilson(k, n)
        p = k / n
        ok = hi >= band[0] and lo <= band[1] and band[0] - tol <= p <= band[1] + tol
        note = "" if n >= need else "; indicative (n below %d)" % need
        self.put("PASS" if ok else "MISS", what, "%s; band %.2f-%.2f%s" % (fmt(k, n), band[0], band[1], note))
        return p


def rate(c):
    return None if not c or c["n"] == 0 else c["win"] / c["n"]


def phi(x):
    return 0.5 * (1 + math.erf(x / math.sqrt(2)))


def main(argv):
    strict = "--strict" in argv
    argv = [a for a in argv if a != "--strict"]
    targets_path = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "fixtures", "p9", "targets.json")
    if "--targets" in argv:
        i = argv.index("--targets")
        targets_path = argv[i + 1]
        del argv[i:i + 2]
    if len(argv) != 1:
        sys.stderr.write(__doc__)
        return 2
    agg = json.load(open(argv[0], encoding="utf-8"))
    t = json.load(open(targets_path, encoding="utf-8"))
    acc = t["acceptance"]
    rep = Report(acc["n"])
    tol = acc["pointTolerance"]
    if agg.get("problems"):
        rep.put("MISS", "agg", "%d fights differ between logs" % len(agg["problems"]))
    rep.put("PASS" if agg.get("faults", 0) == 0 and agg.get("failedChecks", 0) == 0 else "MISS", "faults/checks",
            "faults %d, fights with failed checks %d" % (agg.get("faults", 0), agg.get("failedChecks", 0)))
    slots = agg.get("slots", {})
    measured = {}
    # B1-B4: per slot, pooled over the schedules (acts I-III are shared, sameActsAs); per schedule reported.
    for b in t["bands"]:
        s = slots.get(b["slot"])
        c = None if not s else (s["nonLords"] if b.get("excludeLords") else s["all"])
        measured[b["slot"]] = rate(s["all"]) if s else None
        rep.band(b["id"] + " " + b["slot"], c["win"] if c else 0, c["n"] if c else 0, b["band"], tol)
        for sched, sc in sorted((s or {}).get("bySchedule", {}).items()):
            rep.put("info", "  " + sched, fmt(sc["win"], sc["n"]))
    # B5: the Plague Lords as the act III major.
    b5 = t["B5"]
    s = slots.get(b5["slot"])
    lords = s["lords"] if s else None
    if not lords or lords["n"] == 0:
        rep.put("NO DATA", "B5 Plague Lords", "no lord fight in the act III major sample")
    else:
        dmg = s["lordDamage"]
        mean = sum(dmg) / len(dmg) if dmg else 0
        k1 = lords["win"] / lords["n"]
        lo, hi = wilson(lords["win"], lords["n"])
        share = lords["n"] / s["all"]["n"]
        # The one-attempt kill rate is a band like B1-B4: its interval overlaps it and its point lies within it +- the tolerance.
        kb = b5["oneAttemptKill"]
        ok = mean >= b5["meanDamageAtLeast"] and hi >= kb[0] and lo <= kb[1] and kb[0] - tol <= k1 <= kb[1] + tol
        rep.put("PASS" if ok else "MISS", "B5 Plague Lords", "mean damage %.1f (>= %d), one-attempt kill %s (band %.2f-%.2f), lord share %.3f"
                % (mean, b5["meanDamageAtLeast"], fmt(lords["win"], lords["n"]), b5["oneAttemptKill"][0], b5["oneAttemptKill"][1], share))
    # B6: the Heist finale per fight and all four in one run.
    b6 = t["B6"]
    fights = agg.get("finale", {}).get("fights", {})
    finale_rates = []
    for k in sorted(fights, key=int):
        c = fights[k]
        finale_rates.append(rep.band("B6 finale fight %s" % k, c["win"], c["n"], b6["band"], tol))
    if not fights:
        rep.put("NO DATA", "B6 Heist finale", "")
    joint = agg.get("finale", {}).get("joint", {})
    if joint.get("runs"):
        lo, hi = wilson(joint["allWon"], joint["runs"])
        # A floor, judged by the acceptance rule's both halves: the interval reaches it and the point is within the tolerance of it.
        pj = joint["allWon"] / joint["runs"]
        ok = hi >= b6["allFourAtLeast"] and pj >= b6["allFourAtLeast"] - tol and joint.get("fightsPerRun") == 4
        rep.put("PASS" if ok else "MISS", "B6 all four", "%s over runs with %s fights played (>= %.2f)"
                % (fmt(joint["allWon"], joint["runs"]), joint.get("fightsPerRun"), b6["allFourAtLeast"]))
    # B7: Tekahn.
    b7 = t["B7"]
    tk = agg.get("tekahn", {"n": 0, "win": 0, "cap": 0})
    tekahn = rep.band("B7 Tekahn", tk["win"], tk["n"], b7["band"], tol, need=acc["nTekahn"])
    if tk["n"]:
        rep.put("PASS" if tk["cap"] / tk["n"] <= b7["turnCapsAtMost"] else "MISS", "B7 turn caps", "%d of %d (<= %.0f%%)"
                % (tk["cap"], tk["n"], 100 * b7["turnCapsAtMost"]))
    # B8: ordering.
    b8 = t["B8"]
    a, c = slots.get(b8["lowAct"], {}).get("all"), slots.get(b8["highAct"], {}).get("all")
    if a and c and a["n"] and c["n"]:
        p1, p3 = a["win"] / a["n"], c["win"] / c["n"]
        p = (a["win"] + c["win"]) / (a["n"] + c["n"])
        se = math.sqrt(p * (1 - p) * (1 / a["n"] + 1 / c["n"])) if 0 < p < 1 else 0
        pv = 1 - phi((p1 - p3) / se) if se else (0.0 if p1 > p3 else 1.0)
        rep.put("PASS" if pv < b8["p"] else "MISS", "B8 I > III combat", "%.3f vs %.3f, one-sided p %.4f (< %.2f)" % (p1, p3, pv, b8["p"]))
        for pair in (("I-combat", "II-combat"), ("II-combat", "III-combat"), ("I-major", "II-major"), ("II-major", "III-major")):
            x, y = measured.get(pair[0]), measured.get(pair[1])
            if x is None or y is None:
                rep.put("NO DATA", "B8 " + pair[0] + ">=" + pair[1], "")
            else:
                rep.put("PASS" if x >= y - b8["reversalTolerance"] else "MISS", "B8 " + pair[0] + ">=" + pair[1], "%.3f vs %.3f" % (x, y))
    else:
        rep.put("NO DATA", "B8 ordering", "")
    # B9: outliers by name.
    b9 = t["B9"]
    rows = agg.get("outliers", {})
    if not rows:
        rep.put("NO DATA", "B9 outliers", "")
    flagged = []
    for row in sorted(rows):
        c = rows[row]
        if c["n"] == 0:
            continue
        lo, hi = wilson(c["win"], c["n"])
        if lo > b9["flagAbove"] or hi < b9["flagBelow"]:
            flagged.append("%s %s%s" % (row, fmt(c["win"], c["n"]), " (room: exempt)" if c.get("room") else ""))
    if rows:
        real = [f for f in flagged if "exempt" not in f]
        rep.put("PASS" if not real else "MISS", "B9 outliers", "%d rows sampled, %d flagged%s" % (len(rows), len(flagged), "" if not flagged else ": " + "; ".join(flagged)))
    # R0: the product of the measured slot rates per schedule.
    per = {"I-combat": 4, "I-major": 1, "II-combat": 4, "II-major": 1, "III-combat": 4, "III-major": 1}
    if all(measured.get(k) is not None for k in per):
        acts = 1.0
        for k, m in per.items():
            acts *= measured[k] ** m
        for sched, fin in (("acts-v4h", math.prod(r for r in finale_rates if r is not None) if len(finale_rates) == 4 and None not in finale_rates else None),
                           ("acts-v4t", tekahn)):
            if fin is None:
                rep.put("NO DATA", "R0 " + sched, "acts I-III product %.4f; no finale rate" % acts)
                continue
            r0 = acts * fin
            ok = t["R0"]["band"][0] <= r0 <= t["R0"]["band"][1]
            rep.put("PASS" if ok else "MISS", "R0 " + sched, "%.4f = acts I-III %.4f x finale %.4f (band %.2f-%.2f)" % (r0, acts, fin, t["R0"]["band"][0], t["R0"]["band"][1]))
    else:
        rep.put("NO DATA", "R0", "slots missing: " + ", ".join(k for k in per if measured.get(k) is None))
    # B10 / B11: the long runs.
    runs = agg.get("longrun", {}).get("runs", [])
    if not runs:
        rep.put("NO DATA", "B10 run depth", "")
        rep.put("NO DATA", "B11 per origin", "")
    else:
        lf = agg["longrun"]["fights"]
        slot_of = agg["longrun"].get("slotOfFloor", {})
        bad = []
        # The prediction uses the slot modes' rates (slots9; finale9 per fight on acts-v4h, tekahn9 on acts-v4t), and the long runs'
        # own rate of a slot only when that mode has none (then marked "own").
        own = []
        def slot_rate(sched, f, slot, first_finale):
            if slot != "finale":
                if measured.get(slot) is not None:
                    return measured[slot]
            elif sched == "acts-v4t" and tekahn is not None:
                return tekahn
            elif str(f - first_finale) in fights and fights[str(f - first_finale)]["n"]:
                return rate(fights[str(f - first_finale)])
            own.append("%s floor %d" % (sched, f))
            return rate(lf.get(slot))
        for sched in sorted({r["sched"] for r in runs}):
            mine = [r for r in runs if r["sched"] == sched and r["end"] != "fault"]
            if not mine:
                continue
            known = slot_of.get(sched, {})
            total = max(int(r["fights"]) for r in mine)
            first_finale = min([int(f) for f, s in known.items() if s == "finale"], default=0)
            surv = 1.0
            for f in range(1, total + 1):
                reach = sum(1 for r in mine if f in r["floorsPlayed"])
                lo, hi = wilson(reach, len(mine))
                if not (lo <= surv <= hi):
                    bad.append("%s floor %d predicted %.3f observed %d/%d (CI %.3f-%.3f)" % (sched, f, surv, reach, len(mine), lo, hi))
                if str(f) not in known:
                    break  # no run fought here: the floors after it cannot be predicted from this sample
                r = slot_rate(sched, f, known[str(f)], first_finale)
                surv *= r if r is not None else 1.0
        rep.put("PASS" if not bad else "MISS", "B10 run depth", "%d runs%s%s" % (len(runs), "" if not bad else ": " + "; ".join(bad),
                "" if not own else "; own rates for " + ", ".join(own)))
        b11 = t["B11"]
        def reached(r):
            sof = slot_of.get(r["sched"], {})
            return any(sof.get(str(f)) == b11["reach"] for f in r["floorsPlayed"])
        pooled = sum(1 for r in runs if reached(r)) / len(runs)
        parts, miss = [], False
        for origin in sorted({r["origin"] for r in runs}):
            mine = [r for r in runs if r["origin"] == origin]
            p = sum(1 for r in mine if reached(r)) / len(mine)
            miss |= abs(p - pooled) > b11["tolerance"]
            parts.append("%s %.3f (n %d%s)" % (origin, p, len(mine), "" if len(mine) >= b11["nPerOrigin"] else ", below %d" % b11["nPerOrigin"]))
        rep.put("MISS" if miss else "PASS", "B11 per origin", "pooled %.3f; %s" % (pooled, "; ".join(parts)))
    for line in rep.lines:
        print(line)
    print("check_targets: %d MISS" % rep.miss)
    return 1 if strict and rep.miss else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
