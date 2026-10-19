#!/usr/bin/env python3
"""Open-world stage of the road import: Vicmap freeways, highways and arterials -> RoadData/world_roads.txt.

Real Melbourne only decides where roads run and what they connect; RoadWorldLayout (C#) redraws every road
in the game's own shapes. This script:
- loads Vicmap classes 0-2 inside REAL_RADIUS and joins segments into chains (prepare_roads);
- merges each divided road's two one-way carriageways into one centreline: the game has no centre
  divider, so both directions always share one surface;
- drops the real ramps but keeps where they were: each cluster of ramps becomes an interchange site
  (a freeway and an arterial: a diamond; two freeways: a system interchange), built later from templates;
- joins pieces that run straight on into long roads, squeezes the world radially (build_network), and
  simplifies each road to the corners that matter.

Output (read by MotoSquid.Roads.RoadWorldData):
  road <id>|<name>|<class>|<lanes each way>
  <x> <z> <elevated 0/1>     one line per point, world metres, CBD at the origin; elevated = a real bridge
  site <diamond|system>|<freeway road id>|<other road id>|<x>|<z>

  python3 tools/roads/build_world.py [--plot out.png]
"""
import math, os, sys
from collections import defaultdict

import numpy as np
from scipy.spatial import cKDTree

import prepare_roads as pr
import build_network as bn

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, "RoadData", "world_roads.txt")

SAMPLE = 10.0              # m between points while merging
PAIR_DISTANCE = 150.0      # m: a carriageway's partner is the nearest opposite one of the same name within this
PAIRED_SHARE = 0.5         # share of a chain's points that must find a partner for it to be one half of a pair
LEFTOVER_MIN = 150.0       # m: unpaired pieces of a partner shorter than this are dropped
JOIN_GAP = {0: 60.0, 1: 25.0, 2: 25.0}   # m: road ends this close, heading on within THROUGH_TURN, are one road
THROUGH_TURN = 30.0
SIMPLIFY = 12.0            # m: Douglas-Peucker tolerance after the squeeze
MIN_ROAD = 80.0            # m: shorter roads are dropped
RAMP_CLUSTER = 250.0       # m: ramps closer than this belong to one interchange
RAMP_TOUCH = 45.0          # m: a ramp end this close to a road connects to it
LANES = {0: 3, 1: 2, 2: 2}


def resample(points, flags, spacing=SAMPLE):
    pts = np.asarray(points, float)
    seg = np.hypot(*np.diff(pts, axis=0).T)
    along = np.concatenate([[0], np.cumsum(seg)])
    if along[-1] < 1e-6:
        return pts[:1], flags[:1]
    n = max(2, int(round(along[-1] / spacing)) + 1)
    s = np.linspace(0, along[-1], n)
    out = np.stack([np.interp(s, along, pts[:, 0]), np.interp(s, along, pts[:, 1])], axis=1)
    idx = np.clip(np.searchsorted(along, s, side="right") - 1, 0, len(flags) - 1)
    return out, [flags[i] for i in idx]


def headings(pts):
    d = np.gradient(pts, axis=0)
    n = np.hypot(d[:, 0], d[:, 1])[:, None]
    return d / np.maximum(n, 1e-9)


def load_chains():
    pr.MAX_CLASS = 2
    raw = pr.load()
    segments = []
    for f in raw:
        if not pr.keep(f):
            continue
        pts = [pr.to_local(lon, lat) for lon, lat in f["geometry"]["coordinates"]]
        if not any(x * x + z * z <= bn.REAL_RADIUS ** 2 for x, z in pts):
            continue
        p = f["properties"]
        segments.append({
            "ufi": p["ufi"], "from": p["from_ufi"], "to": p["to_ufi"], "points": pts,
            "name": p.get("ezi_road_name_label") or "", "class": p["class_code"],
            "direction": p.get("direction_code") or "B", "divided": p.get("div_rd") or "",
            "type": p["feature_type_code"],
        })
    pr.split_crossings(segments)
    chains = pr.chains(segments)
    for c in chains:
        c["ramp"] = "Ramp" in c["name"]
        pts, flags = resample(c["points"], [c["type"] == "bridge"] * len(c["points"]))
        c["pts"], c["elev"] = pts, flags
    return chains


SHIFT_WINDOW = 5           # points either side the carriageway offset is judged against
SHIFT_OUTLIER = 15.0       # m: a mate this far off its neighbours' offset belongs to some other piece of road


def smooth_shifts(shifts):
    """Each point's offset to the centreline, from its mate across the road: offsets far off the local median
    (a mate on another stretch of the partner) are dropped, gaps filled from the nearest known, then the whole
    run averaged so neighbouring mates at uneven spacing don't zigzag the centreline."""
    n = len(shifts)
    known = ~np.isnan(shifts[:, 0])
    median = np.array([np.nanmedian(shifts[max(0, k - SHIFT_WINDOW):k + SHIFT_WINDOW + 1], axis=0)
                       if known[max(0, k - SHIFT_WINDOW):k + SHIFT_WINDOW + 1].any() else [np.nan, np.nan] for k in range(n)])
    good = known & (np.hypot(*(shifts - median).T) <= SHIFT_OUTLIER)
    idx = np.nonzero(good if good.any() else known)[0]
    filled = shifts[idx[np.abs(np.arange(n)[:, None] - idx[None, :]).argmin(axis=1)]]
    kernel = np.ones(2 * SHIFT_WINDOW + 1) / (2 * SHIFT_WINDOW + 1)
    padded = np.pad(filled, ((SHIFT_WINDOW, SHIFT_WINDOW), (0, 0)), mode="edge")
    return np.stack([np.convolve(padded[:, d], kernel, mode="valid") for d in range(2)], axis=1)


def merge_carriageways(chains):
    """One centreline per divided road: each one-way chain's points are paired with the nearest point on an
    opposite-heading chain of the same name, and the pair becomes their midpoints. A partner's points that
    found a mate are used up; what is left of it (where the carriageways part) becomes its own road."""
    oneway = [c for c in chains if not c["ramp"] and c["direction"] == "F"]
    owner, point_index, coords = [], [], []
    for ci, c in enumerate(oneway):
        c["used"] = np.zeros(len(c["pts"]), bool)
        c["dirs"] = headings(c["pts"])
        for k, p in enumerate(c["pts"]):
            owner.append(ci); point_index.append(k); coords.append(p)
    tree = cKDTree(np.asarray(coords))
    owner, point_index = np.asarray(owner), np.asarray(point_index)

    roads = [{"name": c["name"], "class": c["class"], "pts": c["pts"], "elev": c["elev"], "divided": False}
             for c in chains if not c["ramp"] and c["direction"] != "F"]
    order = sorted(range(len(oneway)), key=lambda i: -len(oneway[i]["pts"]))
    for ci in order:
        c = oneway[ci]
        if c["used"].all():
            continue
        partner = []
        for k, p in enumerate(c["pts"]):
            best, mate = PAIR_DISTANCE, None
            for j in tree.query_ball_point(p, PAIR_DISTANCE):
                oc = owner[j]
                if oc == ci:
                    continue
                o = oneway[oc]
                ok = point_index[j]
                if o["used"][ok] or o["name"] != c["name"] or float(np.dot(o["dirs"][ok], c["dirs"][k])) > -0.6:
                    continue
                d = math.dist(p, o["pts"][ok])
                if d < best:
                    best, mate = d, (oc, ok)
            partner.append(mate)
        if sum(m is not None for m in partner) < PAIRED_SHARE * len(partner):
            continue
        c["used"][:] = True
        for k, m in enumerate(partner):
            if m is not None:
                oneway[m[0]]["used"][m[1]] = True
        shifts = np.array([[np.nan, np.nan] if m is None else (oneway[m[0]]["pts"][m[1]] - c["pts"][k]) / 2
                           for k, m in enumerate(partner)])
        roads.append({"name": c["name"], "class": c["class"], "pts": c["pts"] + smooth_shifts(shifts),
                      "elev": c["elev"], "divided": True})

    # What is left of partly used carriageways, and one-way roads that never paired, as their own roads.
    for c in oneway:
        run = []
        for k in range(len(c["pts"]) + 1):
            if k < len(c["pts"]) and not c["used"][k]:
                run.append(k)
                continue
            if len(run) * SAMPLE >= LEFTOVER_MIN:
                roads.append({"name": c["name"], "class": c["class"], "pts": c["pts"][run], "elev": [c["elev"][i] for i in run], "divided": False})
            run = []
    return roads


def comes_back(pi, pj, road_class):
    """Whether rj, run on from ri's end, would come back over ri: joining them would build a loop."""
    if len(pi) <= REVISIT or len(pj) <= REVISIT:
        return False
    return bool(cKDTree(pi[:-REVISIT]).query_ball_point(pj[REVISIT:], 2 * HALF_WIDTH[road_class] + 2.0, return_length=True).any())


def join_through(roads):
    """Pieces of the same road that end where the next one starts, heading straight on, become one road."""
    def end_heading(r, at_start):
        pts = r["pts"]
        a, b = (pts[0], pts[min(3, len(pts) - 1)]) if at_start else (pts[-1], pts[max(0, len(pts) - 4)])
        v = b - a
        return math.degrees(math.atan2(v[0], v[1]))

    roads = [r for r in roads if len(r["pts"]) >= 2]
    changed = True
    while changed:
        changed = False
        ends = []
        for i, r in enumerate(roads):
            ends.append((tuple(r["pts"][0]), i, True))
            ends.append((tuple(r["pts"][-1]), i, False))
        tree = cKDTree([e[0] for e in ends])
        joined = set()
        for a, b in sorted(tree.query_pairs(max(JOIN_GAP.values()))):
            (pa, i, si), (pb, j, sj) = ends[a], ends[b]
            # A road joined this pass has new ends; it waits for the next pass with a fresh index.
            if i == j or i in joined or j in joined:
                continue
            ri, rj = roads[i], roads[j]
            # Freeways often change name along the way (CityLink becomes West Gate); everything else joins by name.
            if ri["class"] != rj["class"] or (ri["name"] != rj["name"] and ri["class"] != 0):
                continue
            if math.dist(pa, pb) > JOIN_GAP[ri["class"]]:
                continue
            # Orient so ri runs into rj: ri's end meets rj's start.
            pi = ri["pts"] if not si else ri["pts"][::-1]
            ei = ri["elev"] if not si else ri["elev"][::-1]
            pj = rj["pts"] if sj else rj["pts"][::-1]
            ej = rj["elev"] if sj else rj["elev"][::-1]
            hi = math.degrees(math.atan2(*(pi[-1] - pi[max(0, len(pi) - 4)])))
            hj = math.degrees(math.atan2(*(pj[min(3, len(pj) - 1)] - pj[0])))
            if abs((hi - hj + 180) % 360 - 180) > THROUGH_TURN or comes_back(pi, pj, ri["class"]):
                continue
            # Pieces that overlap at the join would step back over it: skip rj's points behind ri's end.
            ahead = (pi[-1] - pi[max(0, len(pi) - 4)]) / max(np.hypot(*(pi[-1] - pi[max(0, len(pi) - 4)])), 1e-9)
            k = 1
            while k < len(pj) - 1 and np.dot(pj[k] - pi[-1], ahead) <= 1.0:
                k += 1
            roads[i] = {"name": ri["name"], "class": ri["class"], "divided": ri["divided"] or rj["divided"],
                        "pts": np.concatenate([pi, pj[k:]]), "elev": list(ei) + list(ej[k:])}
            roads[j] = None
            joined.update((i, j))
            changed = True
        roads = [r for r in roads if r is not None]
    return roads


def doubled_points(roads, distance=25.0):
    """Per road, the indices of points running parallel to another road of the same name within `distance`."""
    pts, owner, dirs = [], [], []
    for ri, r in enumerate(roads):
        h = headings(np.asarray(r["pts"]))
        for k, p in enumerate(r["pts"]):
            pts.append(p); owner.append((ri, k)); dirs.append(h[k])
    tree = cKDTree(pts)
    doubled = defaultdict(set)
    for a, b in tree.query_pairs(distance):
        (ra, ka), (rb, kb) = owner[a], owner[b]
        if ra != rb and roads[ra]["name"] == roads[rb]["name"] and abs(float(np.dot(dirs[a], dirs[b]))) > 0.9:
            doubled[ra].add(ka); doubled[rb].add(kb)
    return doubled


HALF_WIDTH = {0: 21.0, 1: 15.0, 2: 15.0}
DOUBLED_RUN = 10           # points (100 m): shorter side-by-side stretches are junction approaches, kept


def drop_doubles(roads):
    """Whatever runs alongside a road that matters more, close enough that their surfaces would touch -
    more than two carriageways, a pair that never matched, a tollway laid over a freeway, a street hugging
    one - is cut out of the lesser road (lower class, then shorter). What is left of it stays if long enough;
    side-by-side stretches under DOUBLED_RUN are where roads meet, and stay."""
    order = sorted(range(len(roads)), key=lambda i: (roads[i]["class"], -len(roads[i]["pts"])))
    kept_pts, kept_dirs, kept_names, kept_classes = [], [], [], []
    out = []
    for i in order:
        r = roads[i]
        pts = np.asarray(r["pts"])
        dirs = headings(pts)
        doubled = np.zeros(len(pts), bool)
        if kept_pts:
            tree = cKDTree(np.asarray(kept_pts))
            mine = HALF_WIDTH[r["class"]]
            for k, p in enumerate(pts):
                for j in tree.query_ball_point(p, mine + 21.0 + 2.0):
                    if math.dist(p, kept_pts[j]) > mine + HALF_WIDTH[kept_classes[j]] + 2.0:
                        continue
                    if abs(float(np.dot(kept_dirs[j], dirs[k]))) > 0.8:
                        doubled[k] = True
                        break
            k = 0
            while k < len(pts):
                if not doubled[k]:
                    k += 1
                    continue
                end = k
                while end < len(pts) and doubled[end]:
                    end += 1
                # Short side-by-side stretches mid-road are junctions; at a road's end it is a shallow merge.
                if end - k < DOUBLED_RUN and k > 0 and end < len(pts):
                    doubled[k:end] = False
                k = end
        run = []
        for k in range(len(pts) + 1):
            if k < len(pts) and not doubled[k]:
                run.append(k)
                continue
            if len(run) * SAMPLE >= LEFTOVER_MIN or (len(run) == len(pts) and len(run) >= 2):
                piece = dict(r, pts=pts[run], elev=[r["elev"][q] for q in run])
                out.append(piece)
                kept_pts.extend(piece["pts"]); kept_dirs.extend(headings(piece["pts"])); kept_names.extend([r["name"]] * len(run))
                kept_classes.extend([r["class"]] * len(run))
            run = []
    return out


REVISIT = 15              # points (150 m): a road this far further along that comes back over itself loops


def split_revisits(roads):
    """Where a road comes back within its own width of where it already ran (a carriageway joined back onto
    its partner round a dead end), it is cut there, so the overlap becomes two pieces drop_doubles can sort."""
    out = []
    for r in roads:
        pts = np.asarray(r["pts"])
        reach = 2 * HALF_WIDTH[r["class"]] + 2.0
        start = 0
        while start < len(pts) - 1:
            tree = cKDTree(pts[start:])
            cut = None
            for a, b in sorted(tree.query_pairs(reach)):
                if abs(b - a) > REVISIT:
                    cut = start + max(a, b)
                    break
            if cut is None:
                break
            out.append(dict(r, pts=pts[start:cut + 1], elev=list(r["elev"][start:cut + 1])))
            start = cut
        out.append(dict(r, pts=pts[start:], elev=list(r["elev"][start:])))
    return [o for o in out if len(o["pts"]) >= 2]


def split_reversals(roads):
    """Real streets that double back on themselves (a U round a cul-de-sac) are cut at the turn."""
    out = []
    for r in roads:
        pts, start = np.asarray(r["pts"]), 0
        for k in range(3, len(pts) - 3):
            u, v = pts[k] - pts[k - 3], pts[k + 3] - pts[k]
            if k - start > 3 and float(np.dot(u, v)) < -0.5 * np.hypot(*u) * np.hypot(*v):
                out.append(dict(r, pts=pts[start:k + 1], elev=list(r["elev"][start:k + 1])))
                start = k
        out.append(dict(r, pts=pts[start:], elev=list(r["elev"][start:])))
    return out


MIN_RADIUS = {0: 250.0, 1: 150.0, 2: 80.0}
CORNER_SHIFT = {0: 120.0, 1: 60.0, 2: 40.0}   # m: how far dropping a cramped vertex may move the road


def ease_corners(pts, keep, road_class):
    """Drops kept vertices whose corner could only be drawn far tighter than the class allows (vertices
    bunched round a bend in the real data), while that moves the road by under CORNER_SHIFT."""
    idx = list(np.nonzero(keep)[0])
    limit = MIN_RADIUS[road_class] / 2
    while len(idx) > 2:
        worst, worst_fit = None, limit
        for n in range(1, len(idx) - 1):
            a, b, c = pts[idx[n - 1]], pts[idx[n]], pts[idx[n + 1]]
            u, v = b - a, c - b
            lu, lv = np.hypot(*u), np.hypot(*v)
            turn = math.acos(max(-1.0, min(1.0, float(np.dot(u, v)) / max(lu * lv, 1e-9))))
            if turn < math.radians(0.5):
                continue
            fit = min(lu, lv) / 2 / math.tan(turn / 2)
            if fit >= worst_fit:
                continue
            ac = c - a
            shift = abs(float(ac[0] * (b - a)[1] - ac[1] * (b - a)[0])) / max(np.hypot(*ac), 1e-9)
            if shift < CORNER_SHIFT[road_class]:
                worst, worst_fit = n, fit
        if worst is None:
            break
        del idx[worst]
    eased = np.zeros(len(pts), bool)
    eased[idx] = True
    return eased


HOOK_LENGTH = 60.0         # m: a last segment shorter than this ...
HOOK_TURN = 45.0           # ... turning more than this is a road curling into a junction


def trim_hooks(pts, keep):
    """Drops end points while a road's first or last segment is a short sharp hook (the real road curling
    into a junction), which drawn at full width would fold the road back over itself."""
    idx = list(np.nonzero(keep)[0])

    def hooked(a, b, c):
        u, v = pts[b] - pts[a], pts[c] - pts[b]
        turn = math.degrees(math.acos(max(-1.0, min(1.0, float(np.dot(u, v)) / max(np.hypot(*u) * np.hypot(*v), 1e-9)))))
        return np.hypot(*v) < HOOK_LENGTH and turn > HOOK_TURN

    while len(idx) > 2 and hooked(idx[-3], idx[-2], idx[-1]):
        del idx[-1]
    while len(idx) > 2 and hooked(idx[2], idx[1], idx[0]):
        del idx[0]
    trimmed = np.zeros(len(pts), bool)
    trimmed[idx] = True
    return trimmed


def douglas_peucker(pts, tolerance):
    keep = np.zeros(len(pts), bool)
    keep[0] = keep[-1] = True
    stack = [(0, len(pts) - 1)]
    while stack:
        a, b = stack.pop()
        if b <= a + 1:
            continue
        seg = pts[b] - pts[a]
        length = np.hypot(*seg)
        rel = pts[a + 1:b] - pts[a]
        d = np.abs(rel[:, 0] * seg[1] - rel[:, 1] * seg[0]) / length if length > 1e-9 else np.hypot(rel[:, 0], rel[:, 1])
        k = int(np.argmax(d))
        if d[k] > tolerance:
            m = a + 1 + k
            keep[m] = True
            stack += [(a, m), (m, b)]
    return keep


def ramp_sites(chains, roads):
    """Each cluster of real ramps becomes one interchange site between the roads its ends touch."""
    ramps = [c for c in chains if c["ramp"]]
    for r in ramps:
        r["wpts"] = bn.squeeze(r["pts"])
    parent = list(range(len(ramps)))

    def find(x):
        while parent[x] != x:
            parent[x] = parent[parent[x]]
            x = parent[x]
        return x

    pts = [(p, i) for i, r in enumerate(ramps) for p in r["wpts"][::3]]
    tree = cKDTree([p for p, _ in pts])
    for a, b in tree.query_pairs(RAMP_CLUSTER):
        parent[find(pts[a][1])] = find(pts[b][1])
    clusters = defaultdict(list)
    for i in range(len(ramps)):
        clusters[find(i)].append(i)

    road_pts = [(p, ri) for ri, r in enumerate(roads) for p in r["pts"]]
    road_tree = cKDTree([p for p, _ in road_pts])
    sites = []
    for members in clusters.values():
        touched = defaultdict(int)
        for i in members:
            for end in (ramps[i]["wpts"][0], ramps[i]["wpts"][-1]):
                for j in road_tree.query_ball_point(end, RAMP_TOUCH):
                    touched[road_pts[j][1]] += 1
        freeways = sorted((ri for ri in touched if roads[ri]["class"] == 0), key=lambda ri: -touched[ri])
        others = sorted((ri for ri in touched if roads[ri]["class"] != 0), key=lambda ri: -touched[ri])
        centre = np.mean(np.concatenate([ramps[i]["wpts"] for i in members]), axis=0)
        # Two pieces of one freeway are still one freeway: a system interchange needs two names.
        distinct = []
        for ri in freeways:
            if all(roads[ri]["name"] != roads[d]["name"] for d in distinct):
                distinct.append(ri)
        if len(distinct) >= 2:
            sites.append(("system", distinct[0], distinct[1], centre))
        elif freeways and others:
            sites.append(("diamond", freeways[0], others[0], centre))
    return sites


STUB = 150.0               # m: a road this short with a free end is a stub


def drop_stubs(roads):
    """Short roads that only touch the network at one end (stubs left by the cuts) add a junction and nothing
    to ride; they go, and so do roads touching nothing at all."""
    ends = [(tuple(r["pts"][0]), i) for i, r in enumerate(roads)] + [(tuple(r["pts"][-1]), i) for i, r in enumerate(roads)]
    all_pts = [(p, i) for i, r in enumerate(roads) for p in r["pts"]]
    tree = cKDTree([p for p, _ in all_pts])
    out = []
    for i, r in enumerate(roads):
        length = float(np.sum(np.hypot(*np.diff(r["pts"], axis=0).T)))
        if length >= STUB:
            out.append(r)
            continue
        touching = 0
        for end in (r["pts"][0], r["pts"][-1]):
            reach = HALF_WIDTH[r["class"]] + 21.0
            if any(all_pts[j][1] != i for j in tree.query_ball_point(end, reach)):
                touching += 1
        if touching == 2:
            out.append(r)
    return out


def main():
    chains = load_chains()
    roads = merge_carriageways(chains)
    roads = join_through(roads)
    roads = drop_doubles(roads)
    roads = join_through(roads)
    roads = drop_stubs(split_reversals(join_through(drop_doubles(split_revisits(roads)))))
    for r in roads:
        r["pts"] = bn.squeeze(np.asarray(r["pts"]))
    sites = ramp_sites(chains, roads)
    out, kept = [], 0
    ids = {}
    for i, r in enumerate(roads):
        pts = r["pts"]
        if len(pts) < 2 or np.sum(np.hypot(*np.diff(pts, axis=0).T)) < MIN_ROAD:
            continue
        keep = trim_hooks(pts, ease_corners(pts, douglas_peucker(pts, SIMPLIFY), r["class"]))
        lanes = LANES[r["class"]] if r["class"] < 2 or r["divided"] else 1
        ids[i] = f"w{kept}"
        out.append(f"road {ids[i]}|{r['name']}|{r['class']}|{lanes}")
        for k in np.nonzero(keep)[0]:
            out.append(f"{pts[k][0]:.2f} {pts[k][1]:.2f} {1 if r['elev'][k] else 0}")
        kept += 1
    site_lines = [f"site {kind}|{ids[a]}|{ids[b]}|{c[0]:.1f}|{c[1]:.1f}" for kind, a, b, c in sites if a in ids and b in ids]
    with open(OUT, "w") as fh:
        fh.write("\n".join(out + site_lines) + "\n")

    km = defaultdict(float)
    for i, r in enumerate(roads):
        if i in ids:
            km[r["class"]] += float(np.sum(np.hypot(*np.diff(r["pts"], axis=0).T))) / 1000
    print(f"roads {kept}: " + ", ".join(f"class {c} {km[c]:.0f} km" for c in sorted(km)))
    print(f"sites: {sum(1 for s in site_lines if '|diamond|' in s or 'site diamond' in s)} diamond, "
          f"{sum(1 for s in site_lines if s.startswith('site system'))} system")

    # Roads still running side by side with a same-named road (carriageways that never paired).
    kept_roads = [roads[i] for i in ids]
    doubled = doubled_points(kept_roads)
    print(f"doubled road left: {sum(len(v) for v in doubled.values()) * SAMPLE / 1000:.1f} km")

    if "--plot" in sys.argv:
        import matplotlib
        matplotlib.use("Agg")
        import matplotlib.pyplot as plt
        fig, ax = plt.subplots(figsize=(14, 14), dpi=100)
        colours = {0: "tab:red", 1: "tab:orange", 2: "tab:gray"}
        for i, r in enumerate(roads):
            if i in ids:
                ax.plot(r["pts"][:, 0], r["pts"][:, 1], color=colours[r["class"]], lw={0: 2.2, 1: 1.6, 2: 0.8}[r["class"]])
        for ri, ks in doubled.items():
            arr = np.asarray(kept_roads[ri]["pts"])[sorted(ks)]
            ax.plot(arr[:, 0], arr[:, 1], ".", color="lime", ms=3)
        for kind, a, b, c in sites:
            ax.plot(c[0], c[1], "o" if kind == "diamond" else "s", color="tab:blue" if kind == "diamond" else "tab:purple", ms=7)
        ax.set_aspect("equal"); ax.grid(alpha=0.3)
        fig.tight_layout(); fig.savefig(sys.argv[sys.argv.index("--plot") + 1])


if __name__ == "__main__":
    main()
