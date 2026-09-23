#!/usr/bin/env python3
"""Regenerates the Windows tray app's generated assets from the shared provider icons.

- Assets/provider-marks.json: every provider's SVG path data, normalized so WPF's path mini-language
  (`Geometry.Parse`) reads it (explicit command letters, space-separated numbers, split arc flags).
- Assets/OpenUsage.ico: the app icon (the OpenUsage gauge on the brand-blue tile), for the .exe.

Run from the repository root after changing `Sources/OpenUsage/Resources/ProviderIcons`:
    python3 windows/scripts/generate_assets.py
Needs Pillow (`python3 -m pip install pillow`) for the icon.
"""
import json
import math
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
ICONS = ROOT / "Sources/OpenUsage/Resources/ProviderIcons"
ASSETS = ROOT / "windows/OpenUsage.Windows/Assets"

ARG_COUNTS = {"m": 2, "l": 2, "h": 1, "v": 1, "c": 6, "s": 4, "q": 4, "t": 2, "a": 7, "z": 0}
NUMBER = re.compile(r"[-+]?(?:\d+\.?\d*|\.\d+)(?:[eE][-+]?\d+)?")


def parse_path(d):
    """SVG path data -> [(command, [numbers])], one entry per segment (implicit repeats expanded)."""
    segments = []
    i, command = 0, None
    while i < len(d):
        ch = d[i]
        if ch.isspace() or ch == ",":
            i += 1
            continue
        if ch.isalpha():
            command = ch
            i += 1
            if command.lower() == "z":
                segments.append((command, []))
            continue
        if command is None:
            raise ValueError(f"number before command at {i}")
        count = ARG_COUNTS[command.lower()]
        args = []
        while len(args) < count:
            while i < len(d) and (d[i].isspace() or d[i] == ","):
                i += 1
            if command.lower() == "a" and len(args) in (3, 4):
                args.append(float(d[i]))  # arc flags are single digits, often written unseparated
                i += 1
                continue
            match = NUMBER.match(d, i)
            if not match:
                raise ValueError(f"bad number at {i}: {d[i:i + 12]!r}")
            args.append(float(match.group()))
            i = match.end()
        segments.append((command, args))
        # After a moveto, further coordinate pairs are implicit linetos.
        if command == "M":
            command = "L"
        elif command == "m":
            command = "l"
    return segments


def fmt(value):
    text = f"{value:.4f}".rstrip("0").rstrip(".")
    return "0" if text in ("-0", "") else text


def normalize(d):
    return " ".join(
        cmd if not args else f"{cmd} " + " ".join(fmt(a) for a in args) for cmd, args in parse_path(d)
    )


def svg_marks():
    marks = {}
    for svg in sorted(ICONS.glob("*.svg")):
        text = svg.read_text()
        view_box = [float(v) for v in re.search(r'viewBox="([^"]+)"', text).group(1).replace(",", " ").split()]
        paths = []
        for tag in re.findall(r"<path\b[^>]*>", text):
            d = re.search(r'\bd="([^"]+)"', tag)
            if not d:
                continue
            even_odd = 'fill-rule="evenodd"' in tag
            paths.append({"data": normalize(d.group(1)), "evenOdd": even_odd})
        marks[svg.stem] = {"viewBox": view_box, "paths": paths}
    return marks


def flatten(segments, steps=24):
    """Absolute polylines (one per subpath) for rasterizing the app icon."""
    polys, current = [], []
    x = y = sx = sy = 0.0
    last_ctrl = None
    for cmd, args in segments:
        rel = cmd.islower()
        c = cmd.lower()
        ox, oy = (x, y) if rel else (0.0, 0.0)
        if c == "m":
            if current:
                polys.append(current)
            x, y = args[0] + ox, args[1] + oy
            sx, sy = x, y
            current = [(x, y)]
            last_ctrl = None
        elif c == "l":
            x, y = args[0] + ox, args[1] + oy
            current.append((x, y))
            last_ctrl = None
        elif c == "h":
            x = args[0] + (x if rel else 0)
            current.append((x, y))
        elif c == "v":
            y = args[0] + (y if rel else 0)
            current.append((x, y))
        elif c in ("c", "s"):
            if c == "c":
                x1, y1 = args[0] + ox, args[1] + oy
                x2, y2, ex, ey = args[2] + ox, args[3] + oy, args[4] + ox, args[5] + oy
            else:
                x1, y1 = (2 * x - last_ctrl[0], 2 * y - last_ctrl[1]) if last_ctrl else (x, y)
                x2, y2, ex, ey = args[0] + ox, args[1] + oy, args[2] + ox, args[3] + oy
            for k in range(1, steps + 1):
                t = k / steps
                mt = 1 - t
                px = mt**3 * x + 3 * mt * mt * t * x1 + 3 * mt * t * t * x2 + t**3 * ex
                py = mt**3 * y + 3 * mt * mt * t * y1 + 3 * mt * t * t * y2 + t**3 * ey
                current.append((px, py))
            last_ctrl = (x2, y2)
            x, y = ex, ey
        elif c == "q":
            x1, y1, ex, ey = args[0] + ox, args[1] + oy, args[2] + ox, args[3] + oy
            for k in range(1, steps + 1):
                t = k / steps
                mt = 1 - t
                current.append((mt * mt * x + 2 * mt * t * x1 + t * t * ex, mt * mt * y + 2 * mt * t * y1 + t * t * ey))
            x, y = ex, ey
        elif c == "a":  # not used by the app glyph; approximate with a straight line
            x, y = args[5] + ox, args[6] + oy
            current.append((x, y))
        elif c == "z":
            x, y = sx, sy
            if current:
                polys.append(current)
            current = []
    if current:
        polys.append(current)
    return polys


def app_icon():
    from PIL import Image, ImageChops, ImageDraw

    text = (ICONS / "openusage.svg").read_text()
    d = re.search(r'\bd="([^"]+)"', text).group(1)
    polys = flatten(parse_path(d))
    size = 1024
    tile = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    ImageDraw.Draw(tile).rounded_rectangle((32, 32, size - 32, size - 32), radius=220, fill=(43, 126, 255, 255))
    # Even-odd fill: XOR each subpath's mask, so the gauge ring keeps its hole.
    mask = Image.new("1", (size, size), 0)
    scale, offset = size * 0.66 / 24, size * 0.17
    for poly in polys:
        layer = Image.new("1", (size, size), 0)
        ImageDraw.Draw(layer).polygon([(offset + px * scale, offset + py * scale) for px, py in poly], fill=1)
        mask = ImageChops.logical_xor(mask, layer)
    white = Image.new("RGBA", (size, size), (255, 255, 255, 255))
    tile.paste(white, (0, 0), mask.convert("L"))
    sizes = [(16, 16), (20, 20), (24, 24), (32, 32), (40, 40), (48, 48), (64, 64), (128, 128), (256, 256)]
    tile.save(ASSETS / "OpenUsage.ico", sizes=sizes)


if __name__ == "__main__":
    ASSETS.mkdir(parents=True, exist_ok=True)
    (ASSETS / "provider-marks.json").write_text(json.dumps(svg_marks(), indent=1) + "\n")
    app_icon()
    print("wrote", ASSETS / "provider-marks.json", "and", ASSETS / "OpenUsage.ico")
