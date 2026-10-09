"""Assemble QA evidence into contact sheets for actual visual inspection."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageOps
import hashlib
import json

ROOT = Path(__file__).resolve().parents[1]
for kind in ("hatch", "coupe", "muscle", "pickup"):
    package = ROOT / "art/vehicles" / kind
    qa = package / "qa"
    sheet = Image.new("RGB", (1500, 1540), "#223048")
    d = ImageDraw.Draw(sheet)
    d.text((20, 15), kind.upper() + " / revision 2 / visual review evidence", fill="white")
    # Complete assembled car, alpha backgrounds, recolor, paint/glass/trim isolation.
    for i, filename in enumerate(("on-white.jpg", "on-black.jpg", "checkerboard.jpg", "recolor-check.jpg",
                                   "layer-body-paint.jpg", "wheels-hidden.png", "layer-body-glass.jpg", "layer-body-trim.jpg")):
        x, y = 15 + (i % 2) * 740, 45 + (i // 2) * 210
        d.text((x, y), filename, fill="white")
        im = Image.open(qa / filename).convert("RGB")
        im = ImageOps.contain(im, (710, 180))
        sheet.paste(im, (x, y + 18))
    # Native-sized wheel crops at all review angles. No pivot drift should appear.
    for i, angle in enumerate((0, 45, 90, 180)):
        x, y = 15 + i * 370, 900
        d.text((x, y), f"Rear hub / {angle} degrees / native crop", fill="white")
        im = Image.open(qa / f"rotation-{angle}.png").convert("RGBA")
        bg = Image.new("RGBA", im.size, "#8094aa")
        bg.alpha_composite(im)
        sheet.paste(bg.crop((140, 176, 340, 366)).convert("RGB"), (x, y + 20))
        d.text((x, y + 220), "Fixed caliper only / " + str(angle), fill="white")
        cal = Image.open(qa / f"calipers-{angle}.jpg")
        sheet.paste(cal.crop((140, 176, 340, 366)), (x, y + 240))
    for i, name in enumerate(("isolated-wheel.jpg", "isolated-rotor.jpg", "isolated-caliper.jpg", "isolated-underlay.jpg")):
        x, y = 15 + i * 370, 1370
        d.text((x, y), name, fill="white")
        im = ImageOps.contain(Image.open(qa / name).convert("RGB"), (340, 140))
        sheet.paste(im, (x, y + 20))
    sheet.save(ROOT / "art/previews" / (kind + "-review.jpg"), quality=95)
    for part in ("wheel.png", "rotor.png", "caliper.png"):
        a = hashlib.sha256((package / part).read_bytes()).hexdigest()
        b = hashlib.sha256((ROOT / "art/revisions/v1/Vehicles/hatch" / part).read_bytes()).hexdigest()
        assert a == b, (kind, part, "Approved mechanics changed")
    print(kind, "approved mechanical hashes unchanged")
