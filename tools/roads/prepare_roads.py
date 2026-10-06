#!/usr/bin/env python3
"""Stage 1 of the road import: Vicmap major roads -> metre-space road chains for Unity.

Reads RoadData/vicmap/tr_road_major_*.json (WFS pages, GDA2020 lon/lat), keeps open drivable roads
inside the 25 km circle, converts to metres on a local tangent plane centred on the CBD (x = east,
z = north; error under a metre at 25 km), and joins segments into chains between real junctions
using Vicmap's from_ufi/to_ufi node IDs. Writes RoadData/major_roads.json.

  python3 tools/roads/prepare_roads.py [--stats]
"""
import glob, json, math, os, sys

import heights
import lanes
from collections import Counter, defaultdict

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SRC = os.path.join(ROOT, "RoadData", "vicmap")
OUT = os.path.join(ROOT, "RoadData", "major_roads.json")

ORIGIN_LAT, ORIGIN_LON = -37.8136, 144.9631   # Melbourne CBD
RADIUS_M = 12500.0     # real metres around the CBD; the game shows it at half scale (see RoadGuideImporter)
MAX_CLASS = 1          # 0 freeway, 1 highway (arterials, class 2, are downloaded but left out)
DRIVABLE_TYPES = {"road", "bridge", "tunnel"}

# WGS84 radii of curvature at the origin (GDA2020 differs from WGS84 by under 2 m, irrelevant here)
_A, _E2 = 6378137.0, 6.69437999014e-3
_s = math.sin(math.radians(ORIGIN_LAT))
_M = _A * (1 - _E2) / (1 - _E2 * _s * _s) ** 1.5
_N = _A / math.sqrt(1 - _E2 * _s * _s)
_COS = math.cos(math.radians(ORIGIN_LAT))


def to_local(lon, lat):
    return (math.radians(lon - ORIGIN_LON) * _N * _COS, math.radians(lat - ORIGIN_LAT) * _M)


def load():
    features = {}
    for path in sorted(glob.glob(os.path.join(SRC, "tr_road_major_*.json"))):
        for f in json.load(open(path))["features"]:
            features[f["properties"]["ufi"]] = f
    return list(features.values())


def keep(f):
    p = f["properties"]
    return (p.get("class_code", 99) <= MAX_CLASS
            and p.get("feature_type_code") in DRIVABLE_TYPES and p.get("road_status") == "O"
            and str(p.get("vehicular_access")) == "1" and f["geometry"]["type"] == "LineString")


def _heading_away(seg, node):
    pts = seg["points"]
    a, b = (pts[0], pts[1]) if seg["from"] == node else (pts[-1], pts[-2])
    return math.degrees(math.atan2(b[0] - a[0], b[1] - a[1])) % 360


def split_crossings(segments):
    """Vicmap breaks lines wherever they cross, even where one passes over the other, so a bridge over a
    road looks like a four-way junction. Where the four ends pair up into two straight-through roads (same
    name and type, opposite headings) of different types, or two bridges, the node is a crossing, not a
    junction: give each through-road its own node so it runs straight on. Returns the node pairs."""
    at_node = defaultdict(list)
    for s in segments:
        at_node[s["from"]].append(s)
        at_node[s["to"]].append(s)

    crossings = []
    for node, segs in at_node.items():
        if len(segs) != 4 or len({id(s) for s in segs}) != 4:
            continue
        ends = [(seg, _heading_away(seg, node)) for seg in segs]
        pairs, taken = [], set()
        for i in range(4):
            for j in range(i + 1, 4):
                (a, ha), (b, hb) = ends[i], ends[j]
                turn = abs((ha - hb + 180) % 360 - 180)
                if i not in taken and j not in taken and a["name"] == b["name"] and a["type"] == b["type"] and turn > 150:
                    pairs.append((i, j))
                    taken |= {i, j}
        if len(pairs) != 2:
            continue
        type1, type2 = ends[pairs[0][0]][0]["type"], ends[pairs[1][0]][0]["type"]
        if type1 == type2 and type1 != "bridge":
            continue  # two plain roads meeting: a real junction
        ids = []
        for k, (i, j) in enumerate(pairs):
            virtual = -(node * 2 + k) - 1
            ids.append(virtual)
            for idx in (i, j):
                seg = ends[idx][0]
                if seg["from"] == node:
                    seg["from"] = virtual
                else:
                    seg["to"] = virtual
        crossings.append(tuple(ids))
    return crossings


def chains(segments):
    """Join segments end to end through nodes where exactly two segments meet and nothing changes
    (name, class, one-way direction, divided, bridge/tunnel). Everything else is a junction."""
    at_node = defaultdict(list)
    for s in segments:
        at_node[s["from"]].append(s)
        at_node[s["to"]].append(s)

    def same(a, b):
        return all(a[k] == b[k] for k in ("name", "class", "direction", "divided", "type"))

    used, out = set(), []
    for start in segments:
        if start["ufi"] in used:
            continue
        # Walk backwards to the chain's first segment, then forwards collecting it.
        cur, node = start, start["from"]
        seen = {cur["ufi"]}
        while len(at_node[node]) == 2:
            prev = next(s for s in at_node[node] if s is not cur)
            if prev["ufi"] in seen or prev["ufi"] in used or not same(prev, cur):
                break
            seen.add(prev["ufi"])
            node = prev["to"] if prev["from"] == node else prev["from"]
            cur = prev
        chain_node, points, members, passes = node, [], [], {}
        seg = cur
        while True:
            used.add(seg["ufi"])
            members.append(seg["ufi"])
            pts = seg["points"] if seg["from"] == chain_node else seg["points"][::-1]
            points.extend(pts if not points else pts[1:])
            chain_node = seg["to"] if seg["from"] == chain_node else seg["from"]
            if len(at_node[chain_node]) != 2:
                break
            nxt = next(s for s in at_node[chain_node] if s is not seg)
            if nxt["ufi"] in used or not same(nxt, seg):
                break
            passes[chain_node] = len(points) - 1   # where this chain runs through a node it doesn't end at
            seg = nxt
        out.append({
            "name": seg["name"], "class": seg["class"], "direction": seg["direction"],
            "divided": seg["divided"], "type": seg["type"],
            "startNode": node, "endNode": chain_node, "segments": members, "passes": passes,
            "points": [[round(x, 2), round(z, 2)] for x, z in points],
        })
    return out


def main():
    raw = load()
    segments, dropped = [], Counter()
    for f in raw:
        if not keep(f):
            dropped["filtered (class/type/status)"] += 1
            continue
        pts = [to_local(lon, lat) for lon, lat in f["geometry"]["coordinates"]]
        if not any(x * x + z * z <= RADIUS_M * RADIUS_M for x, z in pts):
            dropped[f"outside {RADIUS_M / 1000:g} km"] += 1
            continue
        p = f["properties"]
        segments.append({
            "ufi": p["ufi"], "from": p["from_ufi"], "to": p["to_ufi"], "points": pts,
            "name": p.get("ezi_road_name_label") or "", "class": p["class_code"],
            "direction": p.get("direction_code") or "B", "divided": p.get("div_rd") or "",
            "type": p["feature_type_code"],
        })

    node_crossings = split_crossings(segments)
    result = chains(segments)
    height_summary = heights.solve(result, node_crossings)
    osm_grid, osm_tagged = lanes.load_osm(os.path.join(ROOT, "RoadData", "osm", "major_ways.json"), to_local)
    lane_summary = lanes.assign(result, osm_grid)
    # Flat coordinates and a stable id per road, the shape Unity's JsonUtility can read. The id is the
    # first Vicmap segment's ufi, so it survives re-running this script against fresh data.
    unity_roads = [{
        "id": f"vm{r['segments'][0]}", "name": r["name"], "roadClass": r["class"], "direction": r["direction"],
        "divided": r["divided"], "type": r["type"], "startNode": r["startNode"], "endNode": r["endNode"],
        "xz": [round(c, 2) for pt in r["points"] for c in pt],
        "y": r["heights"], "lanes": r["lanes"], "lanesFromOsm": r["lanesFromOsm"],
    } for r in result]
    json.dump({"originLat": ORIGIN_LAT, "originLon": ORIGIN_LON, "radiusM": RADIUS_M, "roads": unity_roads},
              open(OUT, "w"))

    def length(pts):
        return sum(math.dist(pts[i], pts[i + 1]) for i in range(len(pts) - 1))

    km = Counter()
    for r in result:
        km[r["class"]] += length(r["points"]) / 1000
    nodes = Counter()
    for r in result:
        nodes[r["startNode"]] += 1
        nodes[r["endNode"]] += 1
    print(f"downloaded {len(raw)}, kept {len(segments)} segments, dropped {dict(dropped)}")
    print(f"chains (roads between junctions): {len(result)}; junction nodes: {sum(1 for n in nodes.values() if n >= 3)}")
    names = {0: "freeway", 1: "highway", 2: "arterial"}
    for c in sorted(km):
        print(f"  class {c} {names.get(c, '?'):8s}: {sum(1 for r in result if r['class'] == c):5d} roads, {km[c]:7.1f} km")
    print("direction:", dict(Counter(r["direction"] for r in result)))
    print("divided:", dict(Counter(r["divided"] for r in result)))
    print("type:", dict(Counter(r["type"] for r in result)))
    print("points per road: median", sorted(len(r["points"]) for r in result)[len(result) // 2],
          "max", max(len(r["points"]) for r in result))
    # Connected pieces: a network that falls apart into islands can't be ridden end to end.
    parent = {}
    def find(n):
        parent.setdefault(n, n)
        while parent[n] != n:
            parent[n] = parent[parent[n]]
            n = parent[n]
        return n
    for r in result:
        parent[find(r["startNode"])] = find(r["endNode"])
    comp_km = Counter()
    for r in result:
        comp_km[find(r["startNode"])] += length(r["points"]) / 1000
    sizes = sorted(comp_km.values(), reverse=True)
    print(f"connected pieces: {len(sizes)}; largest {sizes[0]:.0f} km ({100 * sizes[0] / sum(sizes):.0f}% of the network); "
          f"next: {', '.join(f'{k:.0f}' for k in sizes[1:6])} km")
    print("heights:", height_summary)
    print(f"lanes: {lane_summary} (OSM ways with a lanes tag: {osm_tagged})")
    print(f"wrote {OUT} ({os.path.getsize(OUT) / 1e6:.1f} MB)")


if __name__ == "__main__":
    main()
