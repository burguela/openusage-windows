#!/usr/bin/env python3
"""Regenerates the Windows tray app's generated assets from the shared provider icons.

- Assets/provider-marks.json: every provider's SVG path data, normalized so WPF's path mini-language
  (`Geometry.Parse`) reads it (explicit command letters, space-separated numbers, split arc flags).
- Assets/QuotaTray.ico: the app icon (two meters on a green tile), for the .exe.

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
ASSETS = ROOT / "windows/QuotaTray/Assets"

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
        if svg.stem == "openusage":
            continue  # the original project's logo; this fork doesn't use it
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


def app_icon():
    """Quota Tray's own icon: two usage meters on a green tile (not the OpenUsage logo, which is a
    trademark of the original project)."""
    from PIL import Image, ImageDraw

    size = 1024
    tile = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(tile)
    draw.rounded_rectangle((32, 32, size - 32, size - 32), radius=220, fill=(16, 150, 110, 255))
    left, right, height = 208, size - 208, 168
    for center, fraction in ((404, 0.78), (620, 0.46)):
        top, bottom = center - height // 2, center + height // 2
        track = Image.new("RGBA", (size, size), (0, 0, 0, 0))
        ImageDraw.Draw(track).rounded_rectangle((left, top, right, bottom), radius=height // 2, fill=(255, 255, 255, 90))
        tile = Image.alpha_composite(tile, track)
        fill_right = left + (right - left) * fraction
        ImageDraw.Draw(tile).rounded_rectangle((left, top, fill_right, bottom), radius=height // 2, fill=(255, 255, 255, 255))
    sizes = [(16, 16), (20, 20), (24, 24), (32, 32), (40, 40), (48, 48), (64, 64), (128, 128), (256, 256)]
    tile.save(ASSETS / "QuotaTray.ico", sizes=sizes)


if __name__ == "__main__":
    ASSETS.mkdir(parents=True, exist_ok=True)
    (ASSETS / "provider-marks.json").write_text(json.dumps(svg_marks(), indent=1) + "\n")
    app_icon()
    print("wrote", ASSETS / "provider-marks.json", "and", ASSETS / "QuotaTray.ico")
