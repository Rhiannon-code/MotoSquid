"""Fits a north-up satellite image to the Vicmap road network.

Finds the metres-per-pixel, the pixel the CBD origin falls on, and a small rotation that best lay Vicmap's
freeway/highway/arterial lines over grey road pixels in the image. Prints the constants RoadGuideImporter
uses to place the image under the guides.

Usage: python3 tools/roads/georeference.py [image]
"""
import math, os, sys
import numpy as np
from PIL import Image
from scipy import ndimage, optimize

import prepare_roads as pr

IMAGE = os.path.join(pr.ROOT, "Assets", "Textures", "screenshot_2026-03-12_15-43-01.png")
SAMPLE_SPACING = 8.0
GUESS = (5.6, 1160.0, 160.0, 0.0)   # metres per pixel, CBD pixel x, CBD pixel y, degrees


def road_points():
    points = []
    for f in pr.load():
        p = f["properties"]
        if p.get("road_status") != "O" or int(p["class_code"]) > 2:
            continue
        line = [pr.to_local(lon, lat) for lon, lat in f["geometry"]["coordinates"]]
        for a, b in zip(line, line[1:]):
            n = max(1, int(math.dist(a, b) / SAMPLE_SPACING))
            points += [(a[0] + (b[0] - a[0]) * i / n, a[1] + (b[1] - a[1]) * i / n) for i in range(n)]
    return np.array(points)


def road_likeness(path):
    rgb = np.asarray(Image.open(path).convert("RGB")).astype(float)
    grey = (rgb.max(2) - rgb.min(2) < 14) & (rgb.mean(2) > 45) & (rgb.mean(2) < 120)
    return ndimage.gaussian_filter(grey.astype(float), 1.5)


def main():
    likeness = road_likeness(sys.argv[1] if len(sys.argv) > 1 else IMAGE)
    height, width = likeness.shape
    points = road_points()

    def miss(params):
        metres, cx, cy, degrees = params
        c, s = math.cos(math.radians(degrees)), math.sin(math.radians(degrees))
        u = cx + (points[:, 0] * c - points[:, 1] * s) / metres
        v = cy - (points[:, 0] * s + points[:, 1] * c) / metres
        inside = (u > 5) & (u < width - 5) & (v > 5) & (v < height - 5)
        return -ndimage.map_coordinates(likeness, [v[inside], u[inside]], order=1).mean()

    best = min((optimize.minimize(miss, [metres, *GUESS[1:3], degrees], method="Powell")
                for metres in np.arange(GUESS[0] - 0.4, GUESS[0] + 0.4, 0.05) for degrees in (-1, -0.5, 0, 0.5, 1)),
               key=lambda r: r.fun)
    metres, cx, cy, degrees = best.x
    print(f"road pixels hit: {-best.fun:.3f} (first guess {-miss(GUESS):.3f})")
    print(f"SatelliteMetresPerPixel = {metres:.4f}f")
    print(f"SatelliteCbdPixel = new Vector2({cx:.2f}f, {cy:.2f}f)")
    print(f"SatelliteYaw = {degrees:.4f}f")


if __name__ == "__main__":
    main()
