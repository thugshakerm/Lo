"""Render the original bubbablox badge SVGs to PNG for Discord.

Discord only reliably previews PNG image attachments / icon urls, so the
originals (proxy/badges/*.svg — untouched bubbablox-v2 files) are rendered
1:1 to PNG. The SVGs stay the source of truth; re-run after touching them:

    .venv/bin/python make_badges_png.py

Geometry comes straight from the original SVG path data:
  verified.svg : 22.89px square tilted 15deg + the original white check
  admin.svg    : 76x76 red shield quad (21.6,10.4)(10.3,54.5)(54.4,65.9)(65.8,21.8)
                 with the white diamond hole
                 (41.2,43.5)(32.6,41.3)(34.8,32.7)(43.4,34.9)
"""
from pathlib import Path

from PIL import Image, ImageDraw

BADGES = Path(__file__).with_name("proxy") / "badges"

BLUE = (0, 102, 255)      # verified.svg fill #0066FF
RED = (226, 35, 26)       # admin.svg shield fill #E2231A
WHITE = (255, 255, 255)


def render_verified(out: Path, k: int = 2) -> None:
    """verified.svg at 2x -> 56x56. Tilted 22.89 square + original check."""
    img = Image.new("RGBA", (28 * k, 28 * k), (0, 0, 0, 0))
    tile = Image.new("RGBA", (28 * k, 28 * k), (0, 0, 0, 0))
    ImageDraw.Draw(tile).rectangle(
        [5.88818 * k, 0, (5.88818 + 22.89) * k, 22.89 * k], fill=BLUE + (255,)
    )
    # svg rotate(15 5.88818 0) = 15deg clockwise on screen; PIL positive = ccw
    tile = tile.rotate(-15, center=(5.88818 * k, 0), resample=Image.BICUBIC)
    img.alpha_composite(tile)
    d = ImageDraw.Draw(img)
    # check corners from the verified.svg path (7.45,15.30)(11.82,19.66)(20.55,8.75)
    pts = [(7.45 * k, 15.30 * k), (11.82 * k, 19.66 * k), (20.55 * k, 8.75 * k)]
    w = 2.2 * k
    d.line(pts, fill=WHITE + (255,), width=int(w), joint="curve")
    for x, y in (pts[0], pts[-1]):
        d.ellipse([x - w / 2, y - w / 2, x + w / 2, y + w / 2], fill=WHITE + (255,))
    img.save(out)
    print(f"wrote {out} ({img.width}x{img.height})")


def render_admin(out: Path, k: int = 2) -> None:
    """admin.svg at 2x -> 152x152. Red shield quad + white diamond hole."""
    shield = [(21.6, 10.4), (10.3, 54.5), (54.4, 65.9), (65.8, 21.8)]
    diamond = [(41.2, 43.5), (32.6, 41.3), (34.8, 32.7), (43.4, 34.9)]
    img = Image.new("RGBA", (76 * k, 76 * k), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.polygon([(x * k, y * k) for x, y in shield], fill=RED + (255,))
    d.polygon([(x * k, y * k) for x, y in diamond], fill=WHITE + (255,))
    img.save(out)
    print(f"wrote {out} ({img.width}x{img.height})")


if __name__ == "__main__":
    render_verified(BADGES / "verified.png")
    render_admin(BADGES / "admin.png")
