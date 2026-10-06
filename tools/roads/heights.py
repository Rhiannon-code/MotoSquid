"""Grade separation for a road network that has no heights.

Vicmap gives 2D centrelines plus which chains are bridges and tunnels. Wherever two roads cross without
sharing a junction, one passes over the other: the upper road is lifted CLEARANCE above the lower one at
the crossing, and that lift spreads along the network at no more than MAX_GRADE, so approaches and
connected ramps rise smoothly. Tunnels sink to TUNNEL_DEPTH the same way. Stacked crossings settle by
re-solving until nothing moves. Heights are relative to the ground (0); terrain adds on top later.
"""
import heapq, math
from collections import defaultdict

CLEARANCE = 6.5      # m, deck above the road beneath
MAX_GRADE = 0.05     # rise per metre along the road
TUNNEL_DEPTH = 9.0   # m below ground
SPACING = 10.0       # m, densified vertex spacing so the profile has somewhere to bend
CELL = 50.0          # m, spatial hash for crossing search
ITERATIONS = 6


def _intersect(p, p2, q, q2):
    """Proper intersection of segments p-p2 and q-q2: (t, u) along each, or None."""
    rx, rz = p2[0] - p[0], p2[1] - p[1]
    sx, sz = q2[0] - q[0], q2[1] - q[1]
    den = rx * sz - rz * sx
    if abs(den) < 1e-9:
        return None
    qpx, qpz = q[0] - p[0], q[1] - p[1]
    t = (qpx * sz - qpz * sx) / den
    u = (qpx * rz - qpz * rx) / den
    return (t, u) if 0.0 < t < 1.0 and 0.0 < u < 1.0 else None


def _upper(a, b):
    """Which of two crossing roads goes over: bridges over anything else, anything over a tunnel,
    otherwise the higher class (freeway over highway). Returns (upper, lower, decided_by_data)."""
    if (a["type"] == "bridge") != (b["type"] == "bridge"):
        return (a, b, True) if a["type"] == "bridge" else (b, a, True)
    if (a["type"] == "tunnel") != (b["type"] == "tunnel"):
        return (b, a, True) if a["type"] == "tunnel" else (a, b, True)
    first, second = sorted((a, b), key=lambda r: (r["class"], r["name"]))
    return first, second, False


def find_crossings(roads):
    grid = defaultdict(list)
    for ri, r in enumerate(roads):
        pts = r["points"]
        for si in range(len(pts) - 1):
            (x0, z0), (x1, z1) = pts[si], pts[si + 1]
            for cx in range(int(math.floor(min(x0, x1) / CELL)), int(math.floor(max(x0, x1) / CELL)) + 1):
                for cz in range(int(math.floor(min(z0, z1) / CELL)), int(math.floor(max(z0, z1) / CELL)) + 1):
                    grid[(cx, cz)].append((ri, si))
    found = set()
    crossings = []
    for cell in grid.values():
        for i in range(len(cell)):
            ri, si = cell[i]
            for j in range(i + 1, len(cell)):
                rj, sj = cell[j]
                if ri == rj or (ri, si, rj, sj) in found:
                    continue
                a, b = roads[ri], roads[rj]
                if {a["startNode"], a["endNode"]} & {b["startNode"], b["endNode"]}:
                    continue  # roads meeting at a junction are joined, not crossing
                hit = _intersect(a["points"][si], a["points"][si + 1], b["points"][sj], b["points"][sj + 1])
                if hit:
                    found.add((ri, si, rj, sj))
                    crossings.append((ri, si, hit[0], rj, sj, hit[1]))
    return crossings


def solve(roads, node_crossings=()):
    """Adds r["heights"] (one per r["points"]) to every road and returns a summary dict.
    node_crossings are pairs of node ids where two through-roads cross (see split_crossings); lines that
    cross mid-segment are found geometrically. Inserts crossing points and densifies r["points"] in place."""
    crossings = find_crossings(roads)

    # Where each crossing node sits: a road it runs through, else a road ending at it.
    node_at = {}
    for ri, r in enumerate(roads):
        node_at.setdefault(r["startNode"], (ri, 0))
        node_at.setdefault(r["endNode"], (ri, len(r["points"]) - 1))
    for ri, r in enumerate(roads):
        for node, index in r.get("passes", {}).items():
            node_at[node] = (ri, index)

    # Insert each crossing as a vertex on both roads, remembering which vertex it became.
    inserts = defaultdict(list)   # road index -> [(segment, t, crossing index)]
    for ci, (ri, si, t, rj, sj, u) in enumerate(crossings):
        inserts[ri].append((si, t, ci))
        inserts[rj].append((sj, u, ci))
    at_vertex = defaultdict(dict)  # crossing index -> {road index: vertex index}
    moved = {}                     # road index -> original vertex index -> new vertex index
    for ri, r in enumerate(roads):
        pts, out = r["points"], []
        moved[ri] = {}
        extra = sorted(inserts.get(ri, []))
        k = 0
        for si in range(len(pts) - 1):
            a, b = pts[si], pts[si + 1]
            moved[ri][si] = len(out)
            out.append(a)
            marks = []
            while k < len(extra) and extra[k][0] == si:
                marks.append(extra[k])
                k += 1
            ts = [0.0] + [m[1] for m in marks] + [1.0]
            for n in range(len(ts) - 1):
                if n > 0:
                    at_vertex[marks[n - 1][2]][ri] = len(out)
                    out.append((a[0] + (b[0] - a[0]) * ts[n], a[1] + (b[1] - a[1]) * ts[n]))
                # Densify between this point and the next mark or segment end.
                pa = out[-1]
                pb = (a[0] + (b[0] - a[0]) * ts[n + 1], a[1] + (b[1] - a[1]) * ts[n + 1])
                steps = int(math.dist(pa, pb) // SPACING)
                for s in range(1, steps + 1):
                    f = s / (steps + 1)
                    out.append((pa[0] + (pb[0] - pa[0]) * f, pa[1] + (pb[1] - pa[1]) * f))
        moved[ri][len(pts) - 1] = len(out)
        out.append(pts[-1])
        r["points"] = out

    # Graph over every vertex; a road's end vertices are its junction nodes, shared with other roads.
    def key(ri, vi):
        r = roads[ri]
        if vi == 0:
            return ("n", r["startNode"])
        if vi == len(r["points"]) - 1:
            return ("n", r["endNode"])
        return (ri, vi)

    edges = defaultdict(list)
    for ri, r in enumerate(roads):
        pts = r["points"]
        for vi in range(len(pts) - 1):
            a, b, d = key(ri, vi), key(ri, vi + 1), math.dist(pts[vi], pts[vi + 1])
            edges[a].append((b, d))
            edges[b].append((a, d))

    def spread(sources, caps=None):
        """Largest of (value - MAX_GRADE * distance) over sources, per vertex, never below 0.
        A capped vertex never rises above its cap, and passes on only the capped value."""
        caps = caps or {}
        best = {}
        order = iter(range(1 << 62))   # tie-breaker, so equal values never compare mixed vertex keys
        heap = [(-v, next(order), k) for k, v in sources.items() if v > 0]
        heapq.heapify(heap)
        while heap:
            neg, _, k = heapq.heappop(heap)
            v = min(-neg, caps.get(k, float("inf")))
            if best.get(k, 0.0) >= v:
                continue
            best[k] = v
            for nk, d in edges[k]:
                nv = v - MAX_GRADE * d
                if nv > best.get(nk, 0.0):
                    heapq.heappush(heap, (-nv, next(order), nk))
        return best

    tunnel_sources = {key(ri, vi): TUNNEL_DEPTH
                      for ri, r in enumerate(roads) if r["type"] == "tunnel"
                      for vi in range(len(r["points"]))}
    depth = spread(tunnel_sources)

    # Every crossing as (upper vertex, lower vertex).
    stacks, data_decided = [], 0
    pairs = [(ri, at_vertex[ci][ri], rj, at_vertex[ci][rj]) for ci, (ri, si, t, rj, sj, u) in enumerate(crossings)]
    for a, b in node_crossings:
        (ra, ia), (rb, ib) = node_at[a], node_at[b]
        pairs.append((ra, moved[ra][ia], rb, moved[rb][ib]))
    for ra, va, rb, vb in pairs:
        upper, lower, by_data = _upper(roads[ra], roads[rb])
        data_decided += by_data
        if upper is roads[ra]:
            stacks.append((key(ra, va), key(rb, vb)))
        else:
            stacks.append((key(rb, vb), key(ra, va)))

    lift = {}
    for _ in range(ITERATIONS):
        height = lambda k: lift.get(k, 0.0) - depth.get(k, 0.0)
        sources = {}
        for ku, kl in stacks:
            need = height(kl) + CLEARANCE + depth.get(ku, 0.0)
            sources[ku] = max(sources.get(ku, 0.0), need)
        # The road underneath stays at its own level: lift spreading in from elsewhere (along a short
        # ramp, say) must not raise the very road it has to clear, or interchanges climb without end.
        caps = {kl: sources.get(kl, 0.0) for ku, kl in stacks}
        new_lift = spread(sources, caps)
        if all(abs(new_lift.get(k, 0.0) - lift.get(k, 0.0)) < 0.01 for k in set(new_lift) | set(lift)):
            lift = new_lift
            break
        lift = new_lift

    for ri, r in enumerate(roads):
        r["heights"] = [round(lift.get(key(ri, vi), 0.0) - depth.get(key(ri, vi), 0.0), 2)
                        for vi in range(len(r["points"]))]

    final = lambda k: lift.get(k, 0.0) - depth.get(k, 0.0)
    short = sum(1 for ku, kl in stacks if final(ku) - final(kl) < CLEARANCE - 0.5)
    return {"crossings": len(stacks), "short_of_clearance": short, "at_nodes": len(node_crossings), "mid_segment": len(crossings),
            "decided_by_bridge_or_tunnel": data_decided, "guessed_by_class": len(stacks) - data_decided,
            "max_height": max(max(r["heights"]) for r in roads),
            "min_height": min(min(r["heights"]) for r in roads)}
