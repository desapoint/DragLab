"""Reproducible, original cel-shaded placeholders. No downloaded artwork.

Run from any directory: python tools/generate_placeholder_sprites.py
Pillow >= 10 is the only dependency. Axles use top-left image coordinates.
"""
from pathlib import Path
from PIL import Image, ImageDraw, ImageChops
import hashlib
import json
import math
import shutil

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "Game/Assets/DragLab/Resources/Sprites"
PACKAGES = ROOT / "art/vehicles"
PREVIEW = ROOT / "art/previews"
SIZE = (1024, 384)
AXLES = {"rearWheel": (240, 276), "frontWheel": (786, 276)}
SCALE = 3


def canvas(size=SIZE):
    return Image.new("RGBA", (size[0] * SCALE, size[1] * SCALE))


class Pen:
    def __init__(self, image):
        self.draw = ImageDraw.Draw(image)

    def points(self, values):
        return [tuple(round(v * SCALE) for v in p) for p in values]

    def box(self, values):
        return tuple(round(v * SCALE) for v in values)

    def polygon(self, points, fill):
        self.draw.polygon(self.points(points), fill=fill)

    def line(self, points, fill, width=2):
        self.draw.line(self.points(points), fill=fill, width=width * SCALE, joint="curve")

    def ellipse(self, box, fill, outline=None, width=1):
        self.draw.ellipse(self.box(box), fill=fill, outline=outline, width=width * SCALE)

    def rect(self, box, fill, radius=0):
        self.draw.rounded_rectangle(self.box(box), radius=radius * SCALE, fill=fill)


def save(image, path, size=None):
    path.parent.mkdir(parents=True, exist_ok=True)
    size = size or (image.width // SCALE, image.height // SCALE)
    image.resize(size, Image.Resampling.LANCZOS).save(path)


def mechanics(folder):
    folder.mkdir(parents=True, exist_ok=True)
    # Keep the approved mechanical assets byte-for-byte through body revisions.
    approved = ROOT / "art/revisions/v1/Vehicles/hatch"
    if approved.is_dir():
        for name in ("wheel.png", "rotor.png", "caliper.png"):
            shutil.copyfile(approved / name, folder / name)
        return
    size = (192, 192)
    wheel = canvas(size)
    p = Pen(wheel)
    p.ellipse((20, 20, 172, 172), "#10131a", "#303747", 3)
    p.ellipse((29, 29, 163, 163), None, "#1c222e", 3)
    p.ellipse((42, 42, 150, 150), (0, 0, 0, 0))
    p.ellipse((43, 43, 149, 149), None, "#bdc6d4", 5)
    # Empty spoke gaps expose the independently drawn caliper and rotor.
    for i in range(5):
        a = i * math.tau / 5
        pts = [(96 + math.cos(a + da) * r, 96 + math.sin(a + da) * r)
               for r, da in [(12, -.4), (52, -.10), (52, .10), (12, .4)]]
        p.polygon(pts, "#c1cad6")
        p.line([(96 + math.cos(a) * 16, 96 + math.sin(a) * 16),
                (96 + math.cos(a) * 50, 96 + math.sin(a) * 50)], "#738293", 2)
    p.ellipse((83, 83, 109, 109), "#657285", "#d7e1eb", 2)
    for i in range(5):
        a = i * math.tau / 5
        x, y = 96 + math.cos(a) * 8, 96 + math.sin(a) * 8
        p.ellipse((x - 2, y - 2, x + 2, y + 2), "#232b37")
    save(wheel, folder / "wheel.png")
    rotor = canvas(size)
    p = Pen(rotor)
    p.ellipse((51, 51, 141, 141), "#727d89", "#b8c5ce", 2)
    p.ellipse((72, 72, 120, 120), "#4a5462", "#a5b2be", 2)
    p.ellipse((88, 88, 104, 104), "#273142")
    for i in range(16):
        a = i * math.tau / 16
        x, y = 96 + math.cos(a) * 36, 96 + math.sin(a) * 36
        p.ellipse((x - 1.8, y - 1.8, x + 1.8, y + 1.8), "#303b49")
    save(rotor, folder / "rotor.png")
    caliper = canvas(size)
    p = Pen(caliper)
    p.rect((124, 76, 143, 115), "#141925", 6)
    p.rect((125, 78, 141, 113), "#f2664b", 5)
    p.rect((126, 81, 132, 110), "#ffa279", 2)
    p.line([(136, 83), (136, 105)], "#822b31", 2)
    save(caliper, folder / "caliper.png")


def rounded_outline(points, radius=6):
    """Round panel corners with short quadratic segments, preserving the silhouette."""
    result = []
    for i, at in enumerate(points):
        before, after = points[i - 1], points[(i + 1) % len(points)]
        def inset(other):
            distance = math.dist(other, at)
            t = min(.22, radius / max(distance, 1))
            return (at[0] + (other[0] - at[0]) * t, at[1] + (other[1] - at[1]) * t)
        a, b = inset(before), inset(after)
        for j in range(7):
            t = j / 6
            result.append(((1-t)**2 * a[0] + 2*(1-t)*t*at[0] + t*t*b[0],
                           (1-t)**2 * a[1] + 2*(1-t)*t*at[1] + t*t*b[1]))
    return result


def vehicle(kind, color):
    folder = PACKAGES / kind
    mechanics(folder)
    body = canvas()
    p = Pen(body)
    if kind == "hatch":
        outline = [(105, 274), (108, 230), (134, 207), (153, 188), (177, 125),
                   (216, 104), (264, 99), (476, 100), (518, 109), (605, 179),
                   (734, 186), (851, 207), (906, 229), (922, 250), (924, 276),
                   (914, 292), (111, 292)]
        windows = [[(190, 132), (225, 115), (310, 113), (310, 181), (169, 181)],
                   [(327, 113), (427, 113), (427, 181), (327, 181)],
                   [(444, 114), (487, 121), (566, 181), (444, 181)]]
    elif kind == "coupe":
        outline = [(88, 275), (95, 241), (125, 221), (217, 207), (272, 190),
                   (341, 140), (393, 115), (452, 110), (511, 116), (561, 137),
                   (636, 186), (713, 191), (831, 208), (890, 225), (925, 249),
                   (936, 274), (917, 292), (101, 292)]
        windows = [[(354, 146), (397, 126), (434, 123), (434, 186), (302, 186)],
                   [(451, 124), (504, 130), (549, 150), (598, 187), (451, 187)]]
    elif kind == "muscle":
        outline = [(77, 273), (80, 239), (103, 216), (213, 205), (266, 194),
                   (335, 137), (374, 118), (429, 111), (530, 113), (560, 123),
                   (646, 185), (754, 185), (895, 194), (944, 212), (951, 238),
                   (950, 280), (931, 294), (87, 294)]
        windows = [[(346, 144), (382, 132), (425, 127), (437, 186), (291, 186)],
                   [(453, 127), (521, 127), (550, 138), (605, 185), (453, 185)]]
    else:
        outline = [(74, 274), (79, 189), (90, 177), (396, 177), (408, 118),
                   (434, 88), (484, 81), (587, 82), (614, 93), (654, 168),
                   (687, 181), (839, 186), (912, 207), (948, 231), (951, 280),
                   (935, 294), (80, 294)]
        windows = [[(428, 124), (447, 99), (506, 97), (506, 177), (417, 177)],
                   [(523, 97), (580, 98), (600, 107), (632, 177), (523, 177)]]
    outline = rounded_outline(outline, 8 if kind != "muscle" else 4)
    p.polygon(outline, "#c9cdd3")
    # A mildly lowered, deliberately generic stance: exterior covers top tire arc.
    for x, y in AXLES.values():
        p.ellipse((x - 88, y - 62, x + 88, y + 82), (0, 0, 0, 0))
    silhouette = body.getchannel("A")
    save(body, folder / "body-paint.png")
    paint_mask = Image.new("RGBA", body.size, "white")
    paint_mask.putalpha(silhouette)
    pm = Pen(paint_mask)
    for window in windows:
        pm.polygon(window, (0, 0, 0, 0))
    pm.rect((96, 268, 929, 294), (0, 0, 0, 0))
    pm.rect((100, 230, 141, 251), (0, 0, 0, 0))
    pm.rect((875, 225, 923, 246), (0, 0, 0, 0))
    save(paint_mask, folder / "paint-mask.png")
    shading = canvas()
    p = Pen(shading)
    p.polygon([(85, 251), (943, 251), (943, 300), (85, 300)], (10, 16, 25, 85))
    p.polygon([(143, 204), (593, 187), (850, 215), (604, 215), (145, 225)], (255, 255, 255, 65))
    p.line([(136, 248), (188, 247)], (255, 255, 255, 65), 2)
    p.line([(305, 247), (698, 247)], (255, 255, 255, 45), 2)
    p.line(outline + [outline[0]], "#141c29", 5)
    if kind == "hatch":
        p.line([(320, 113), (319, 222), (330, 271), (416, 271), (434, 227), (435, 183)], (28, 36, 50, 145), 2)
        p.line([(438, 182), (605, 186), (601, 262), (588, 272), (434, 272)], (28, 36, 50, 145), 2)
        p.rect((392, 199, 414, 204), "#263243", 2)
        p.rect((558, 199, 584, 204), "#263243", 2)
    elif kind == "pickup":
        p.line([(402, 177), (402, 289)], (20, 27, 40, 180), 3)
        p.line([(516, 96), (516, 267)], (28, 36, 50, 145), 2)
        p.line([(647, 180), (645, 266), (531, 269), (416, 269)], (28, 36, 50, 145), 2)
        p.rect((464, 193, 490, 199), "#263243", 2)
        p.rect((588, 193, 614, 199), "#263243", 2)
    else:
        p.line([(443, 125), (443, 190), (608, 191), (608, 260), (590, 270), (373, 270), (345, 249), (338, 191)], (28, 36, 50, 145), 2)
        p.rect((553, 200, 578, 205), "#263243", 2)
    p.line([(650, 195), (730, 201), (835, 214)], (30, 38, 52, 120), 2)
    p.line([(133, 226), (189, 217)], (30, 38, 52, 130), 2)
    shading.putalpha(ImageChops.multiply(shading.getchannel("A"), silhouette))
    save(shading, folder / "body-shading.png")
    glass = canvas()
    p = Pen(glass)
    for window in windows:
        p.polygon(window, "#182e46")
        p.line(window + [window[0]], "#111b2b", 4)
        p.line([window[0], window[1], window[2]], "#517089", 3)
    # Reflections are clipped into each individual window; no paint spill.
    reflection = canvas()
    rp = Pen(reflection)
    for window in windows:
        xmin, xmax = min(x for x, y in window), max(x for x, y in window)
        ymin, ymax = min(y for x, y in window), max(y for x, y in window)
        rp.polygon([(xmin + 18, ymax), (xmin + 38, ymax), (xmax - 12, ymin), (xmax - 34, ymin)], (143, 192, 214, 90))
    reflection.putalpha(ImageChops.multiply(reflection.getchannel("A"), glass.getchannel("A")))
    glass.alpha_composite(reflection)
    save(glass, folder / "body-glass.png")
    trim = canvas()
    p = Pen(trim)
    p.rect((112, 281, 909, 294), "#161e2b", 3)
    if kind == "pickup":
        p.rect((85, 173, 395, 184), "#1b2835", 2)
        p.rect((85, 218, 98, 258), "#e85355", 2)
        p.rect((894, 219, 930, 248), "#d4edf1", 3)
        p.rect((916, 256, 946, 269), "#202c3a", 2)
        p.rect((618, 176, 643, 190), "#142331", 3)
        p.rect((122, 281, 661, 290), "#8893a0", 2)
        p.line([(152, 189), (359, 189)], "#738092", 2)
    else:
        p.rect((106, 230, 135, 245), "#ff4e55", 3)
        p.polygon([(878, 221), (914, 231), (918, 240), (879, 237)], "#d4edf1")
        p.line([(881, 224), (911, 232)], "#ffffff", 2)
        p.rect((912, 258, 932, 273), "#14202a", 3)
        p.polygon([(599, 178), (623, 177), (631, 189), (600, 191)], "#142331")
    # Functional-looking rear fuel door and subtle lower bumper treatment.
    p.rect((143, 208, 164, 224), None, 2)
    p.line([(140, 208), (163, 208), (164, 222)], "#697280", 1)
    trim.putalpha(ImageChops.multiply(trim.getchannel("A"), silhouette))
    save(trim, folder / "body-trim.png")
    fixtures = ImageChops.lighter(glass.getchannel("A"), trim.getchannel("A"))
    paint_mask = Image.new("RGBA", body.size, "white")
    paint_mask.putalpha(ImageChops.multiply(silhouette, ImageChops.invert(fixtures)))
    save(paint_mask, folder / "paint-mask.png")
    underlay = canvas()
    p = Pen(underlay)
    # Local arch backs stop at rocker height; never become a scenery rectangle.
    for x, y in AXLES.values():
        p.ellipse((x - 88, y - 62, x + 88, y + 82), "#0b1018")
    p.rect((0, 295, 1024, 384), (0, 0, 0, 0))
    save(underlay, folder / "car-underlay.png")
    shadow = canvas()
    Pen(shadow).ellipse((87, 337, 947, 366), (0, 0, 0, 70))
    save(shadow, folder / "shadow.png")
    layers = [{"id": "shadow", "file": "shadow.png", "z": 0,
               "placement": {"mode": "canvas", "x": 0, "y": 0}},
              {"id": "car-underlay", "file": "car-underlay.png", "z": 50,
               "placement": {"mode": "canvas", "x": 0, "y": 0}}]
    for axle, name in [("rearWheel", "rear"), ("frontWheel", "front")]:
        for part, z, rotate in [("rotor", 100, True), ("caliper", 200, False), ("wheel", 300, True)]:
            layers.append({"id": f"{name}-{part}", "file": f"{part}.png", "z": z,
                           "placement": {"mode": "anchor", "anchor": axle, "offset": [0, 0],
                                         "scale": 0.88 if name == "rear" and part != "wheel" else 1.0,
                                         "pivotNormalized": [0.5, 0.5]}, "rotateWithWheel": rotate})
    for i, part in enumerate(["paint", "shading", "glass", "trim"]):
        layers.append({"id": f"body-{part}", "file": f"body-{part}.png", "z": 400 + i * 20,
                       "placement": {"mode": "canvas", "x": 0, "y": 0}})
    hashes = {p.name: hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(folder.glob("*.png"))}
    manifest = {"schemaVersion": 1, "revision": 2,
                "vehicle": {"id": f"generic-{kind}", "make": "DragLab", "model": kind.title(),
                            "year": None, "trim": "Mechanics placeholder", "facing": "right"},
                "canvas": {"width": 1024, "height": 384},
                "anchors": {k: {"x": x, "y": y, "radius": 76} for k, (x, y) in AXLES.items()},
                "layers": layers, "masks": {"paint": "paint-mask.png"},
                "notes": ["Original procedural placeholder; no OEM identity or factory measurements.",
                          "Sprite units are art coordinates, never physics dimensions.",
                          "Generic lowered stance deliberately occludes upper tires.",
                          "Shared wheel art; rear brake artwork scales to 88% for visual variety.",
                          "body-paint is neutral with fixture regions filled; fixtures render above it."],
                "sources": [{"file": "tools/generate_placeholder_sprites.py", "kind": "procedural-original"}],
                "provenance": {"generatorSha256": hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
                               "outputsSha256": hashes}}
    (folder / "car-sprite.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    runtime = OUT / "Vehicles" / kind
    runtime.mkdir(parents=True, exist_ok=True)
    for file in list(folder.glob("*.png")) + [folder / "car-sprite.json"]:
        shutil.copyfile(file, runtime / file.name)
    return folder, color


def props():
    folder = OUT / "Track"
    im = canvas((256, 128))
    p = Pen(im)
    p.rect((0, 0, 256, 128), "#29313b")
    for y in range(6, 128, 12):
        for x in range(5, 256, 17):
            p.line([(x, y), (x + 3, y)], "#313a45", 1)
    save(im, folder / "asphalt-tile.png")
    im = canvas((256, 64))
    p = Pen(im)
    p.rect((0, 0, 256, 64), "#737f8d")
    p.rect((0, 0, 256, 12), "#a4afb9")
    p.rect((0, 51, 256, 64), "#424d5c")
    p.line([(128, 14), (128, 50)], "#566170", 3)
    save(im, folder / "barrier-tile.png")
    im = canvas((128, 32))
    Pen(im).rect((4, 10, 124, 22), "#e1dfcc", 2)
    save(im, folder / "lane-stripe.png")
    im = canvas((64, 128))
    p = Pen(im)
    for y in range(0, 128, 16):
        for x in range(0, 64, 16):
            p.rect((x, y, x + 16, y + 16), "#e6e9ed" if (x + y) % 32 == 0 else "#171e28")
    save(im, folder / "finish-checker.png")
    im = canvas((96, 320))
    p = Pen(im)
    p.rect((41, 0, 55, 316), "#3a485c", 4)
    p.rect((14, 22, 82, 263), "#101720", 10)
    p.rect((18, 26, 78, 259), "#1b2533", 8)
    save(im, folder / "tree-frame.png")
    for name, color in [("amber", "#ffb648"), ("green", "#65eea5"), ("red", "#ff565f"), ("off", "#374353")]:
        im = canvas((48, 48))
        p = Pen(im)
        p.ellipse((3, 3, 45, 45), "#111b29")
        p.ellipse((7, 7, 41, 41), color)
        p.ellipse((12, 11, 23, 18), (255, 255, 255, 100))
        save(im, folder / f"bulb-{name}.png")
    im = canvas((128, 128))
    p = Pen(im)
    for x, y, r in [(47, 64, 31), (77, 68, 28), (65, 43, 26), (35, 80, 24), (89, 86, 22)]:
        p.ellipse((x - r, y - r, x + r, y + r), (196, 206, 217, 75))
    save(im, OUT / "Effects/smoke.png")
    im = canvas((128, 64))
    p = Pen(im)
    p.polygon([(119, 20), (80, 19), (55, 8), (69, 24), (10, 30), (69, 40), (47, 57), (88, 45), (120, 43)], "#ef733f")
    p.polygon([(119, 27), (79, 26), (44, 31), (82, 38), (119, 37)], "#ffd177")
    save(im, OUT / "Effects/exhaust-flame.png")


def preview(folders):
    PREVIEW.mkdir(parents=True, exist_ok=True)
    sheet = Image.new("RGBA", (1200, 1010), "#162233")
    d = ImageDraw.Draw(sheet)
    d.text((30, 20), "DRAGLAB / original generic sprite kit / mechanics placeholders", fill="#edf5fd")
    for row, (folder, color) in enumerate(folders):
        manifest = json.loads((folder / "car-sprite.json").read_text())
        assembled = Image.new("RGBA", SIZE)
        for layer in sorted(manifest["layers"], key=lambda v: v["z"]):
            im = Image.open(folder / layer["file"]).convert("RGBA")
            if layer["id"] == "body-paint":
                tint = Image.new("RGBA", im.size, color)
                tint.putalpha(im.getchannel("A"))
                im = ImageChops.multiply(im, tint)
            if layer["placement"]["mode"] == "anchor":
                sc = layer["placement"]["scale"]
                im = im.resize((round(im.width * sc), round(im.height * sc)), Image.Resampling.LANCZOS)
                x, y = AXLES[layer["placement"]["anchor"]]
                assembled.alpha_composite(im, (round(x - im.width / 2), round(y - im.height / 2)))
            else:
                assembled.alpha_composite(im)
        assembled.resize((870, 326), Image.Resampling.LANCZOS).save(PREVIEW / f"{folder.name}.png")
        sheet.alpha_composite(assembled.resize((615, 231), Image.Resampling.LANCZOS), (28, 45 + row * 235))
        d.text((675, 91 + row * 235), f"GENERIC {folder.name.upper()} / FWD, RWD, AWD configurable", fill="#edf5fd")
        d.text((675, 117 + row * 235), "Neutral body + shading + glass + trim", fill="#94acc7")
        d.text((675, 138 + row * 235), "Rotor rotates / caliper fixed / wheel rotates", fill="#94acc7")
        for i, part in enumerate(["rotor", "caliper", "wheel"]):
            im = Image.open(folder / f"{part}.png").resize((100, 100), Image.Resampling.LANCZOS)
            sheet.alpha_composite(im, (680 + i * 140, 160 + row * 235))
    sheet.convert("RGB").save(PREVIEW / "sprite-kit-overview.png")


if __name__ == "__main__":
    folders = [vehicle("hatch", "#58c7ba"), vehicle("coupe", "#6caaf8"), vehicle("muscle", "#efa04f"), vehicle("pickup", "#bd9281")]
    props()
    preview(folders)
    print(f"Generated {sum(1 for _ in OUT.rglob('*.png'))} transparent PNG sprites in {OUT}")
