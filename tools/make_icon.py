#!/usr/bin/env python3
"""
Draws the One Two Punch plugin icon.

Dalamud shows this at roughly 64 pixels in the installer list, so the mark is built to
survive that: two shapes, high contrast against the background, and numerals heavy enough
to stay legible when they are a dozen pixels tall. Everything is drawn at 4x and
downsampled, which is cheaper than fighting Pillow's aliasing.

Usage:  python3 tools/make_icon.py [out.png]
"""

import pathlib
import sys

from PIL import Image, ImageDraw, ImageFilter, ImageFont

SIZE = 512
SS = 4                      # supersample factor
S = SIZE * SS

FONT = "/mnt/skills/examples/canvas-design/canvas-fonts/Outfit-Bold.ttf"

INK = (14, 18, 34)          # background, deepest
INK_TOP = (32, 40, 72)      # background, lighter towards the top
RIM = (86, 102, 156)
ONE = (78, 201, 193)        # the first punch: cool
TWO = (232, 168, 62)        # the second: warm, and lands in front
BURST = (232, 96, 72)


def rounded_mask(size, radius):
    mask = Image.new("L", (size, size), 0)
    ImageDraw.Draw(mask).rounded_rectangle([0, 0, size - 1, size - 1], radius, fill=255)
    return mask


def background():
    """Vertical gradient, lighter at the top, inside a rounded square."""
    grad = Image.new("RGB", (1, S))
    for y in range(S):
        t = y / (S - 1)
        # Ease so most of the lift sits in the upper third.
        t = t ** 0.75
        grad.putpixel((0, y), tuple(
            round(INK_TOP[i] + (INK[i] - INK_TOP[i]) * t) for i in range(3)))

    img = grad.resize((S, S), Image.BILINEAR).convert("RGBA")
    img.putalpha(rounded_mask(S, int(S * 0.22)))
    return img


def impact(draw, cx, cy, r, away):
    """
    Rays on the far side of the second punch only. Spread evenly around the circle they
    read as a sun; confined to the arc the blow is travelling into, they read as impact.
    """
    import math

    for i in range(7):
        # Fan across roughly 150 degrees centred on the direction of travel.
        angle = away + math.radians((i - 3) * 25)
        inner = r * 1.13
        outer = r * (1.46 if i % 2 == 0 else 1.30)
        draw.line(
            [cx + math.cos(angle) * inner, cy + math.sin(angle) * inner,
             cx + math.cos(angle) * outer, cy + math.sin(angle) * outer],
            fill=BURST + (225,), width=int(r * 0.125))


def punch(img, cx, cy, r, colour, label, font_scale=1.06):
    """One circular button: body, top highlight, rim, numeral."""
    draw = ImageDraw.Draw(img, "RGBA")

    # A soft drop shadow so the second reads as in front of the first.
    shadow = Image.new("RGBA", img.size, (0, 0, 0, 0))
    ImageDraw.Draw(shadow).ellipse(
        [cx - r, cy - r + r * 0.10, cx + r, cy + r + r * 0.10], fill=(0, 0, 0, 150))
    shadow = shadow.filter(ImageFilter.GaussianBlur(r * 0.11))
    img.alpha_composite(shadow)

    draw.ellipse([cx - r, cy - r, cx + r, cy + r], fill=colour + (255,))

    # Highlight across the top third.
    hi = Image.new("RGBA", img.size, (0, 0, 0, 0))
    ImageDraw.Draw(hi).ellipse(
        [cx - r * 0.82, cy - r * 0.92, cx + r * 0.82, cy + r * 0.12],
        fill=(255, 255, 255, 46))
    hi = hi.filter(ImageFilter.GaussianBlur(r * 0.09))
    img.alpha_composite(hi)

    draw.ellipse([cx - r, cy - r, cx + r, cy + r], outline=INK + (255,), width=int(r * 0.10))

    font = ImageFont.truetype(FONT, int(r * font_scale))
    box = draw.textbbox((0, 0), label, font=font)
    draw.text(
        (cx - (box[0] + box[2]) / 2, cy - (box[1] + box[3]) / 2),
        label, font=font, fill=INK + (255,))


def build():
    import math

    img = background()
    draw = ImageDraw.Draw(img, "RGBA")

    # Inner rim, so the icon keeps an edge against a dark plugin list.
    inset = int(S * 0.018)
    draw.rounded_rectangle(
        [inset, inset, S - inset, S - inset],
        int(S * 0.20), outline=RIM + (90,), width=int(S * 0.010))

    first = (S * 0.330, S * 0.658, S * 0.208)
    second = (S * 0.612, S * 0.402, S * 0.238)

    # A motion streak from the first punch into the second: the combo, in one stroke.
    trail = Image.new("RGBA", img.size, (0, 0, 0, 0))
    ImageDraw.Draw(trail).line(
        [first[0], first[1], second[0], second[1]],
        fill=(255, 255, 255, 58), width=int(S * 0.062))
    img.alpha_composite(trail.filter(ImageFilter.GaussianBlur(S * 0.016)))

    away = math.atan2(second[1] - first[1], second[0] - first[0])
    impact(draw, second[0], second[1], second[2], away)

    punch(img, *first, ONE, "1")
    punch(img, *second, TWO, "2")

    return img.resize((SIZE, SIZE), Image.LANCZOS)


def main():
    out = pathlib.Path(sys.argv[1] if len(sys.argv) > 1 else "assets/icon.png")
    out.parent.mkdir(parents=True, exist_ok=True)
    icon = build()
    icon.save(out, "PNG", optimize=True)
    print(f"wrote {out} ({icon.size[0]}x{icon.size[1]})")

    # The size that actually matters is the installer list.
    preview = out.with_name("icon-64-preview.png")
    icon.resize((64, 64), Image.LANCZOS).save(preview, "PNG", optimize=True)
    print(f"wrote {preview} (64x64, how Dalamud shows it)")


if __name__ == "__main__":
    main()
