#!/usr/bin/env python3
"""Stage 2 of the road import: RoadData/major_roads.json -> RoadData/spline_roads.txt for the road system.

Shapes the real network into a fictional Neo-Melbourne that is fun to ride:
- Joins pieces that run straight on through a node into one long road, so the only joins left are real
  junctions (no seams where Vicmap changes a name, a lane count or a bridge flag).
- Squeezes the world radially: real size inside CORE_RADIUS, compressed toward the edge so 12.5 km of
  Melbourne fits inside WORLD_RADIUS. Ramps are kept only where the world is still close to real size.
- Lifts long bridges that cross nothing in the data (rivers, rail) into a hump.
- Resamples every road to evenly spaced points, which become the knots of its spline in Unity.

Output, one block per road (read by MotoSquid.Roads.RoadDataLayout):
  road <id>|<name>|<ramp 0/1>|<one-way 0/1>|<lanes>
  <x> <y> <z>        one line per point, metres, world space with the CBD at the origin

  python3 tools/roads/build_network.py            # the West Gate / CityLink pilot
  python3 tools/roads/build_network.py --all      # everything
"""
import json, math, os, sys
from collections import defaultdict

import numpy as np
from scipy.spatial import cKDTree

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SRC = os.path.join(ROOT, "RoadData", "major_roads.json")
OUT = os.path.join(ROOT, "RoadData", "spline_roads.txt")

CORE_RADIUS = 3000.0
BLEND = 2500.0
REAL_RADIUS = 12500.0
WORLD_RADIUS = 7500.0
RAMP_MIN_SCALE = 0.85        # ramps survive where the world is at least this close to real size

THROUGH_TURN = 30.0          # degrees: two pieces meeting straighter than this are one road
HEADING_RUN = 20.0           # m of road used to measure a heading at a node

SAMPLE = 10.0                # m between points while shaping
MARKER_SPACING = 20.0        # m between spline knots
LANE_WIDTH = 6.0             # must match RoadSettings' defaults, so overlap checks match Unity
SHOULDERS = 6.0
RAMP_LANES = (1, 2)
MAINLINE_MIN_LANES = 3

SAME_SURFACE = 0.5           # m: overlapping roads closer in height than this are one surface

BRIDGE_PEAK = 12.0
MIN_BRIDGE = 250.0
MAX_GRADE = 0.05
CLEARANCE = 6.0

PILOT_CENTRE = (-2500.0, -800.0)   # real metres from the CBD: West Gate / CityLink / Bolte Bridge
PILOT_RADIUS = 1800.0


# ---------- world squeeze ----------

OUTER_SCALE = (WORLD_RADIUS - CORE_RADIUS - BLEND / 2) / (BLEND / 2 + REAL_RADIUS - CORE_RADIUS - BLEND)


def local_scale(r):
    if r <= CORE_RADIUS:
        return 1.0
    t = min(1.0, (r - CORE_RADIUS) / BLEND)
    return 1.0 + (OUTER_SCALE - 1.0) * t * t * (3 - 2 * t)


def squeezed_radius(r):
    if r <= CORE_RADIUS:
        return r
    t = min(1.0, (r - CORE_RADIUS) / BLEND)
    blended = CORE_RADIUS + BLEND * (t + (OUTER_SCALE - 1.0) * (t ** 3 - t ** 4 / 2))
    return blended + max(0.0, r - CORE_RADIUS - BLEND) * OUTER_SCALE


def squeeze(points):
    r = np.hypot(points[:, 0], points[:, 1])
    factor = np.array([squeezed_radius(v) / v if v > 1e-6 else 1.0 for v in r])
    return points * factor[:, None]


# ---------- joining pieces into roads ----------

def _heading(points, at_start):
    pts = points if at_start else points[::-1]
    travelled, i = 0.0, 1
    while i < len(pts) - 1 and travelled < HEADING_RUN:
        travelled += math.dist(pts[i - 1], pts[i])
        i += 1
    a, b = pts[0], pts[i]
    h = math.degrees(math.atan2(b[0] - a[0], b[1] - a[1]))
    return h if at_start else (h + 180) % 360   # end headings point along the direction of travel


def _turn(a, b):
    return abs((a - b + 180) % 360 - 180)


def link(pieces):
    """Pairs each piece's end with the piece that carries straight on from it. Returns next/prev maps."""
    arrivals, departures = defaultdict(list), defaultdict(list)
    for i, p in enumerate(pieces):
        departures[p["startNode"]].append(i)
        arrivals[p["endNode"]].append(i)
    nxt, prv = {}, {}
    for node in set(arrivals) & set(departures):
        candidates = []
        for i in arrivals[node]:
            for j in departures[node]:
                if i == j:
                    continue
                turn = _turn(_heading(pieces[i]["points"], False), _heading(pieces[j]["points"], True))
                if turn < THROUGH_TURN:
                    candidates.append((turn + (0 if pieces[i]["name"] == pieces[j]["name"] else 10), i, j))
        for _, i, j in sorted(candidates):
            if i not in nxt and j not in prv:
                nxt[i], prv[j] = j, i
    return nxt, prv


def runs_from(pieces, nxt, prv):
    """Walks the links into roads. Each road keeps which piece every point came from."""
    runs, seen = [], set()
    starts = [i for i in range(len(pieces)) if i not in prv] + list(range(len(pieces)))
    for start in starts:
        if start in seen:
            continue
        chain, i = [], start
        while i is not None and i not in seen:
            seen.add(i)
            chain.append(i)
            i = nxt.get(i)
        points, heights, owner, passes = [], [], [], {}
        for k, i in enumerate(chain):
            pts, ys = pieces[i]["points"], pieces[i]["y"]
            if k:
                passes[pieces[i]["startNode"]] = len(points) - 1
                pts, ys = pts[1:], ys[1:]
            points.extend(pts)
            heights.extend(ys)
            owner.extend([i] * len(pts))
        runs.append({"pieces": chain, "points": points, "y": heights, "owner": owner, "passes": passes,
                     "startNode": pieces[chain[0]]["startNode"], "endNode": pieces[chain[-1]]["endNode"]})
    return runs


# ---------- resampling ----------

def arc_lengths(points):
    return np.concatenate([[0.0], np.cumsum(np.linalg.norm(np.diff(points, axis=0), axis=1))])


def resample(points, values, spacing):
    """Evenly spaced points along a polyline, with per-point values carried along (nearest for ints)."""
    s = arc_lengths(points)
    count = max(2, int(round(s[-1] / spacing)) + 1)
    at = np.linspace(0.0, s[-1], count)
    out = np.column_stack([np.interp(at, s, points[:, k]) for k in range(points.shape[1])])
    carried = {k: (np.interp(at, s, v) if v.dtype.kind == "f" else v[np.clip(np.searchsorted(s, at), 0, len(v) - 1)])
               for k, v in values.items()}
    return out, at, carried


def moving_average(values, window):
    if window < 2 or len(values) < 3:
        return values
    pad = window // 2
    padded = np.pad(values, pad, mode="edge")
    return np.convolve(padded, np.ones(window) / window, mode="valid")[:len(values)]


# ---------- main shaping ----------

def build(pieces):
    nxt, prv = link(pieces)
    runs = runs_from(pieces, nxt, prv)

    for run in runs:
        pts = np.array(run["points"], dtype=float)
        owner = np.array(run["owner"])
        y = np.array(run["y"], dtype=float)
        real_radius = np.hypot(pts[:, 0], pts[:, 1])
        ramp_length = sum(arc_lengths(np.array(pieces[i]["points"]))[-1] for i in run["pieces"] if pieces[i]["ramp"])
        total = arc_lengths(pts)[-1]
        run["ramp"] = bool(ramp_length > total / 2)
        mid = real_radius[len(real_radius) // 2]
        run["keep"] = not run["ramp"] or local_scale(mid) >= RAMP_MIN_SCALE
        run["realPoints"] = pts

        xz, s, carried = resample(squeeze(pts), {"y": y, "owner": owner}, SAMPLE)
        run["xz"], run["s"], run["y"], run["owner"] = xz, s, carried["y"], carried["owner"]

    runs = [r for r in runs if r["keep"] and len(r["xz"]) >= 2]
    for run in runs:
        lanes = np.array([pieces[i]["lanes"] for i in run["owner"]], dtype=float)
        lanes = np.clip(lanes, *RAMP_LANES) if run["ramp"] else np.maximum(lanes, MAINLINE_MIN_LANES)
        values, counts = np.unique(lanes, return_counts=True)
        run["lanes"] = int(values[np.argmax(counts)])
        run["width"] = np.full(len(run["xz"]), run["lanes"] * LANE_WIDTH + SHOULDERS)
        run["y"] = moving_average(run["y"], 5)
    return runs, {"lifted bridges": lift_bridges(runs, pieces)}


def lift_bridges(runs, pieces):
    """Long bridges that sit at ground level cross something the data doesn't have (the Yarra, rail
    yards): give them a hump. Undone where it would clash with a road beside or under them."""
    for run in runs:
        run["hump"] = np.zeros(len(run["y"]))
        owner = run["owner"]
        for i in dict.fromkeys(owner.tolist()):
            idx = np.nonzero(owner == i)[0]
            span = run["s"][idx[-1]] - run["s"][idx[0]]
            if pieces[i]["type"] != "bridge" or span < MIN_BRIDGE or np.abs(run["y"][idx]).max() > 0.5:
                continue
            d = run["s"][idx] - run["s"][idx[0]]
            hump = np.minimum(min(BRIDGE_PEAK, MAX_GRADE * span / 2), MAX_GRADE * np.minimum(d, span - d))
            run["hump"][idx] = moving_average(hump, 5)
    a, b, dy, _ = _overlaps(runs)
    already = set(zip(a[(dy > SAME_SURFACE) & (dy < CLEARANCE)].tolist(), b[(dy > SAME_SURFACE) & (dy < CLEARANCE)].tolist()))
    for _ in range(5):
        a, b, dy, run_of = _overlaps(runs, with_hump=True)
        clash = np.array([c and (i, j) not in already for c, i, j in zip((dy > SAME_SURFACE) & (dy < CLEARANCE), a, b)], dtype=bool)
        undo = {int(k) for k in np.concatenate([run_of[a][clash], run_of[b][clash]]) if runs[k]["hump"].any()}
        if not undo:
            break
        for k in undo:
            runs[k]["hump"][:] = 0
    for run in runs:
        run["y"] = run["y"] + run["hump"]
    return sum(1 for r in runs if r["hump"].any())


def _overlaps(runs, with_hump=False):
    """Pairs of points on different roads whose full-width surfaces overlap, with their height gap."""
    xz = np.concatenate([r["xz"] for r in runs])
    y = np.concatenate([r["y"] + (r["hump"] if with_hump else 0) for r in runs])
    w = np.concatenate([r["width"] for r in runs])
    run_of = np.concatenate([np.full(len(r["xz"]), k) for k, r in enumerate(runs)])
    pairs = cKDTree(xz).query_pairs(w.max(), output_type="ndarray").reshape(-1, 2)
    a, b = pairs[:, 0], pairs[:, 1]
    hit = (run_of[a] != run_of[b]) & (np.linalg.norm(xz[a] - xz[b], axis=1) < (w[a] + w[b]) / 2)
    a, b = a[hit], b[hit]
    return a, b, np.abs(y[a] - y[b]), run_of


def in_pilot(run):
    p = run["realPoints"]
    return bool((np.hypot(p[:, 0] - PILOT_CENTRE[0], p[:, 1] - PILOT_CENTRE[1]) < PILOT_RADIUS).any())


def to_points(run, clip=None):
    """Evenly spaced spline points. With a clip circle (in world metres), only the parts inside it."""
    xz, y = run["xz"], run["y"]
    keep = np.ones(len(xz), dtype=bool)
    if clip is not None:
        keep = np.hypot(xz[:, 0] - clip[0], xz[:, 1] - clip[1]) < clip[2]
    pieces, start = [], None
    for i, k in enumerate(np.append(keep, False)):
        if k and start is None:
            start = i
        elif not k and start is not None:
            if i - start >= 3:
                pieces.append(slice(start, i))
            start = None
    out = []
    for sl in pieces:
        pts = np.column_stack([xz[sl, 0], y[sl], xz[sl, 1]])
        points, _, _ = resample(pts, {}, MARKER_SPACING)
        out.append(points)
    return out


def main():
    data = json.load(open(SRC))
    pieces = [{**r, "points": [tuple(p) for p in zip(r["xz"][0::2], r["xz"][1::2])], "ramp": "Ramp" in r["name"]}
              for r in data["roads"]]
    runs, summary = build(pieces)

    clip = None
    if "--all" not in sys.argv:
        centre = squeeze(np.array([PILOT_CENTRE]))[0]
        clip = (centre[0], centre[1], PILOT_RADIUS)
        runs = [r for r in runs if in_pilot(r)]

    lines, count, km = [], 0, 0.0
    for r in runs:
        names = [pieces[i]["name"] for i in r["pieces"]]
        name = max(set(names), key=names.count).replace("|", "/")
        one_way = all(pieces[i]["direction"] != "B" for i in r["pieces"])
        for n, points in enumerate(to_points(r, clip)):
            rid = f"vm{pieces[r['pieces'][0]]['id'][2:]}" + (f"-{n}" if n else "")
            lines.append(f"road {rid}|{name}|{int(r['ramp'])}|{int(one_way)}|{r['lanes']}")
            lines.extend(f"{x:.3f} {y:.3f} {z:.3f}" for x, y, z in points)
            count += 1
            km += np.linalg.norm(np.diff(points[:, [0, 2]], axis=0), axis=1).sum() / 1000
    with open(OUT, "w") as f:
        f.write("\n".join(lines) + "\n")

    print(f"outer scale {OUTER_SCALE:.3f}; world radius {WORLD_RADIUS / 1000:g} km")
    print(f"{len(pieces)} pieces -> {len(runs)} roads{' touching the pilot' if clip else ''}; wrote {count} roads, {km:.1f} km to {OUT}")
    print(summary)


if __name__ == "__main__":
    main()
