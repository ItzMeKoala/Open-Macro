"""Draws the Open-Macro app icon (a gamepad) and writes the files each platform needs.

Run:  python3 assets/icon/make_icon.py
Writes: src/ReWASD.UI/Assets/ReWASD.ico, src/ReWASD.UI.Mac/Assets/ReWASD.icns (on a Mac), src/ReWASD.UI.Mac/Assets/tray.png,
        src/ReWASD.UI.Mac/Assets/icon.png and assets/icon/rewasd-1024.png
Needs Pillow (pip install pillow). The artwork is original to this project.
"""
import io, os, struct, subprocess, sys, tempfile
from PIL import Image, ImageDraw

GOLD = (233, 196, 106, 255)
INK = (27, 29, 36, 255)
SCALE = 4  # draw big, shrink for smooth edges


def glyph(size, color, cutout):
    """The gamepad silhouette as an RGBA image of the given size (transparent background)."""
    s = size * SCALE
    img = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    u = s / 100.0  # 100 units across

    def rr(x0, y0, x1, y1, r, fill):
        d.rounded_rectangle([x0 * u, y0 * u, x1 * u, y1 * u], radius=r * u, fill=fill)

    def circle(cx, cy, r, fill):
        d.ellipse([(cx - r) * u, (cy - r) * u, (cx + r) * u, (cy + r) * u], fill=fill)

    # body: a wide rounded bar with a grip hanging down on each side
    rr(12, 30, 88, 66, 17, color)
    circle(27, 64, 15, color)
    circle(73, 64, 15, color)
    rr(21, 49, 79, 74, 8, color)

    # cut-outs: d-pad on the left, two buttons on the right, a small bar between
    rr(25, 44, 43, 50, 1.5, cutout)
    rr(31, 38, 37, 56, 1.5, cutout)
    circle(66, 42, 3.8, cutout)
    circle(75, 51, 3.8, cutout)
    rr(45, 46, 49, 50, 1, cutout)
    rr(52, 46, 56, 50, 1, cutout)
    return img.resize((size, size), Image.LANCZOS)


def tile(size):
    """The full app icon: a dark rounded square with the gold gamepad."""
    s = size * SCALE
    img = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    margin = int(s * 0.06)
    d.rounded_rectangle([margin, margin, s - margin, s - margin], radius=int(s * 0.22), fill=INK)
    img = img.resize((size, size), Image.LANCZOS)
    g = glyph(int(size * 0.74), GOLD, INK)
    off = (size - g.width) // 2
    img.alpha_composite(g, (off, off + int(size * 0.02)))
    return img


def write_ico(path, images):
    blobs = []
    for im in images:
        buf = io.BytesIO()
        im.save(buf, "PNG")
        blobs.append((im.width, buf.getvalue()))
    header = struct.pack("<HHH", 0, 1, len(blobs))
    entries, data, offset = b"", b"", 6 + 16 * len(blobs)
    for w, blob in blobs:
        entries += struct.pack("<BBBBHHII", w % 256, w % 256, 0, 0, 1, 32, len(blob), offset)
        data += blob
        offset += len(blob)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "wb") as f:
        f.write(header + entries + data)


def main():
    root = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
    big = tile(1024)
    big.save(os.path.join(root, "assets", "icon", "rewasd-1024.png"))

    write_ico(os.path.join(root, "src", "ReWASD.UI", "Assets", "ReWASD.ico"), [tile(s) for s in (16, 24, 32, 48, 64, 128, 256)])

    mac_assets = os.path.join(root, "src", "ReWASD.UI.Mac", "Assets")
    os.makedirs(mac_assets, exist_ok=True)
    tile(256).save(os.path.join(mac_assets, "icon.png"))
    glyph(64, GOLD, (0, 0, 0, 0)).save(os.path.join(mac_assets, "tray.png"))

    if sys.platform == "darwin":
        with tempfile.TemporaryDirectory() as tmp:
            iconset = os.path.join(tmp, "ReWASD.iconset")
            os.makedirs(iconset)
            for base in (16, 32, 128, 256, 512):
                tile(base).save(os.path.join(iconset, f"icon_{base}x{base}.png"))
                tile(base * 2).save(os.path.join(iconset, f"icon_{base}x{base}@2x.png"))
            subprocess.run(["iconutil", "-c", "icns", iconset, "-o", os.path.join(mac_assets, "ReWASD.icns")], check=True)
    print("icons written")


main()
