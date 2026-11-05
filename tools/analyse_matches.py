"""Compares how people and bots drive, from the files MatchRecorder writes.

    python tools/analyse_matches.py                      every match in the game's folder
    python tools/analyse_matches.py path\\to\\folder      or another folder (or one file)

Each driver kind gets a column: people against the three bot personalities cannot be told apart by seat alone, so people
are "person" and every bot seat is "bot". Numbers are per minute of driving so matches of any length compare.
"""
import csv
import glob
import math
import os
import sys
from collections import defaultdict

DEFAULT = os.path.expandvars(r"%USERPROFILE%\AppData\LocalLow\*\*\matches")
TURN_BANDS = [("straight", 0, 15), ("gentle", 15, 45), ("sharp", 45, 1000)]  # degrees per second of heading change


def files(where):
    if os.path.isfile(where):
        return [where]
    found = []
    for folder in glob.glob(where):
        found += glob.glob(os.path.join(folder, "match-*.csv"))
    return sorted(found)


def new_totals():
    return defaultdict(float)


def read(path, totals):
    """Adds a match to each driver kind's totals. Returns its mode, and each seat's kind and final score"""
    mode = "?"
    golden = False
    last = {}
    first = {}
    kinds = {}
    scores = {}
    with open(path, newline="", encoding="utf-8") as f:
        for row in csv.reader(f):
            if not row:
                continue
            if row[0] == "match":
                mode = row[1]
                golden = len(row) > 4 and row[4] == "golden"
            elif row[0] == "e" and row[3].lstrip("-").isdigit() and int(row[3]) in kinds:
                totals[kinds[int(row[3])]][row[2]] += 1
            elif row[0] == "s":
                t, seat = float(row[1]), int(row[2])
                kind = "bot" if row[3] == "1" else "person"
                kinds[seat] = kind
                x, z = float(row[4]), float(row[6])
                speed = math.hypot(float(row[7]), float(row[8]))
                heading = float(row[9])
                scores[seat] = int(row[16])
                t_ = totals[kind]
                first.setdefault(seat, t)
                prev = last.get(seat)
                if prev:
                    dt = t - prev["t"]
                    if 0 < dt < 1:
                        t_["seconds"] += dt
                        t_["metres"] += math.hypot(x - prev["x"], z - prev["z"])
                        t_["speed_time"] += speed * dt
                        t_["full_throttle"] += dt if float(row[11]) > 0.9 else 0
                        t_["braking"] += dt if float(row[12]) > 0.1 else 0
                        t_["drifting"] += dt if row[14] == "1" else 0
                        t_["still"] += dt if speed < 2 else 0
                        turn = abs((heading - prev["heading"] + 180) % 360 - 180) / dt
                        for name, low, high in TURN_BANDS:
                            if low <= turn < high:
                                t_[name + "_time"] += dt
                                t_[name + "_speed"] += speed * dt
                    if row[13] == "1" and not prev["boost"]:
                        t_["boosts"] += 1
                last[seat] = {"t": t, "x": x, "z": z, "heading": heading, "boost": row[13] == "1"}
    # Scores carry on through the golden round, so its file would count the main round's twice
    for seat, score in scores.items():
        if not golden:
            totals[kinds[seat]]["score"] += score
            totals[kinds[seat]]["seats"] += 1
            totals[kinds[seat]]["main_seconds"] += last[seat]["t"] - first[seat]
    return mode, {seat: (kinds[seat], score) for seat, score in scores.items()}


def main():
    where = sys.argv[1] if len(sys.argv) > 1 else DEFAULT
    paths = files(where)
    if not paths:
        print("No matches in " + where)
        return
    totals = defaultdict(new_totals)
    for path in paths:
        mode, seats = read(path, totals)
        print(os.path.basename(path), mode, ", ".join("seat %d %s $%d" % (s, k, v) for s, (k, v) in sorted(seats.items())))

    kinds = sorted(totals)
    print()
    print("%-28s" % "" + "".join("%12s" % k for k in kinds))

    def line(name, value, form="%.1f"):
        print("%-28s" % name + "".join("%12s" % (form % value(totals[k])) for k in kinds))

    minutes = lambda t: max(t["seconds"] / 60.0, 1e-6)
    share = lambda key: (lambda t: 100.0 * t[key] / max(t["seconds"], 1e-6))
    line("minutes driven", minutes)
    line("score per seat", lambda t: t["score"] / max(t["seats"], 1), "%.0f")
    line("score per minute", lambda t: t["score"] / max(t["main_seconds"] / 60.0, 1e-6), "%.0f")
    line("deliveries per minute", lambda t: t["delivered"] / minutes(t), "%.2f")
    line("steals per minute", lambda t: t["stole"] / minutes(t), "%.2f")
    line("robbed per minute", lambda t: t["robbed"] / minutes(t), "%.2f")
    line("falls per minute", lambda t: t["fell"] / minutes(t), "%.2f")
    line("boosts per minute", lambda t: t["boosts"] / minutes(t))
    line("average speed (m/s)", lambda t: t["speed_time"] / max(t["seconds"], 1e-6))
    for name, _, _ in TURN_BANDS:
        line("speed, %s (m/s)" % name, lambda t, n=name: t[n + "_speed"] / max(t[n + "_time"], 1e-6))
    line("% full throttle", share("full_throttle"), "%.0f")
    line("% braking", share("braking"), "%.0f")
    line("% drifting", share("drifting"), "%.0f")
    line("% stopped", share("still"), "%.0f")


if __name__ == "__main__":
    main()
