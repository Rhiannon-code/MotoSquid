"""Lane counts for Vicmap roads, borrowed from OpenStreetMap.

Vicmap has no lane data. OSM maps each carriageway of a divided road as its own one-way line, as Vicmap
does, so every Vicmap road is matched to the OSM line running alongside it in the same direction, and the
most common `lanes` value along its length wins. OSM data is ODbL: credit "© OpenStreetMap contributors".
"""
import json, math
from collections import Counter, defaultdict

MATCH_DISTANCE = 12.0   # m between a Vicmap sample and the OSM line
MATCH_ANGLE = 30.0      # degrees between their headings
SAMPLE_SPACING = 20.0   # m along each Vicmap road
CELL = 25.0

# When OSM has no lanes tag: lanes in the direction of travel for one-way roads, total otherwise.
DEFAULT_LANES = {"ramp": 1, "freeway": 3, "highway_oneway": 2, "highway_twoway": 4}


def _heading(a, b):
    return math.degrees(math.atan2(b[0] - a[0], b[1] - a[1])) % 360


def _angle_between(h1, h2):
    return abs((h1 - h2 + 180) % 360 - 180)


def load_osm(path, to_local):
    """OSM ways with a parsable lanes tag, as local-space segments in a spatial grid."""
    grid = defaultdict(list)
    tagged = 0
    for way in json.load(open(path))["elements"]:
        tags = way.get("tags", {})
        try:
            lanes = int(str(tags.get("lanes", "")).split(";")[0])
        except ValueError:
            continue
        tagged += 1
        oneway = tags.get("oneway") in ("yes", "1", "-1") or tags.get("highway", "").startswith("motorway")
        pts = [to_local(p["lon"], p["lat"]) for p in way.get("geometry", [])]
        if tags.get("oneway") == "-1":
            pts.reverse()
        for a, b in zip(pts, pts[1:]):
            seg = (a, b, lanes, oneway, _heading(a, b))
            for cx in range(int(min(a[0], b[0]) // CELL), int(max(a[0], b[0]) // CELL) + 1):
                for cz in range(int(min(a[1], b[1]) // CELL), int(max(a[1], b[1]) // CELL) + 1):
                    grid[(cx, cz)].append(seg)
    return grid, tagged


def _distance(p, a, b):
    ax, az = b[0] - a[0], b[1] - a[1]
    l2 = ax * ax + az * az
    t = max(0.0, min(1.0, ((p[0] - a[0]) * ax + (p[1] - a[1]) * az) / l2)) if l2 > 1e-9 else 0.0
    return math.hypot(p[0] - a[0] - ax * t, p[1] - a[1] - az * t)


def assign(roads, grid):
    """Sets r["lanes"] and r["lanesFromOsm"] on every road; returns coverage numbers."""
    from_osm = 0
    for r in roads:
        pts = r["points"]
        votes = Counter()
        travelled = 0.0
        next_sample = 0.0
        for a, b in zip(pts, pts[1:]):
            seg_len = math.dist(a, b)
            heading = _heading(a, b)
            while next_sample <= travelled + seg_len:
                t = (next_sample - travelled) / seg_len if seg_len > 0 else 0.0
                p = (a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t)
                best = None
                cx, cz = int(p[0] // CELL), int(p[1] // CELL)
                nearby = (seg for dx in (-1, 0, 1) for dz in (-1, 0, 1) for seg in grid.get((cx + dx, cz + dz), ()))
                for oa, ob, lanes, oneway, oh in nearby:
                    turn = _angle_between(heading, oh)
                    if not oneway:
                        turn = min(turn, 180 - turn)
                    if turn > MATCH_ANGLE:
                        continue
                    d = _distance(p, oa, ob)
                    if d <= MATCH_DISTANCE and (best is None or d < best[0]):
                        best = (d, lanes)
                if best:
                    votes[best[1]] += 1
                next_sample += SAMPLE_SPACING
            travelled += seg_len

        if votes:
            r["lanes"], r["lanesFromOsm"] = votes.most_common(1)[0][0], True
            from_osm += 1
        else:
            if "Ramp" in r["name"]:
                kind = "ramp"
            elif r["class"] == 0:
                kind = "freeway"
            else:
                kind = "highway_oneway" if r["direction"] != "B" else "highway_twoway"
            r["lanes"], r["lanesFromOsm"] = DEFAULT_LANES[kind], False
    return {"from_osm": from_osm, "defaulted": len(roads) - from_osm,
            "distribution": dict(sorted(Counter(r["lanes"] for r in roads).items()))}
