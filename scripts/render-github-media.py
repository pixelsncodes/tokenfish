"""Render illustrative TokenFish product mockups with synthetic usage data.

Requires Pillow. Run on Windows for the application's Segoe UI typography.
The native fish geometry, palette, quota position, and 340 ms mouth cycle are
mirrored from FishQuotaRail.cs and TokenFishAppearance.cs. This does not launch
providers, collect account data, or modify application settings.
"""

from pathlib import Path
import math
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "docs" / "media"
OUT.mkdir(parents=True, exist_ok=True)
SCALE = 2
BG, SURFACE, RAISED = "#10151c", "#161a21", "#20252e"
TEXT, MUTED, TRACK = "#edf1f7", "#a3adbc", "#343b47"
MINT, PEACH, LIME = "#74d9c0", "#e8ad8b", "#ddf87b"


def font(size, bold=False):
    names = [Path("C:/Windows/Fonts") / ("seguisb.ttf" if bold else "segoeui.ttf"),
             Path("/usr/share/fonts/truetype/dejavu") / ("DejaVuSans-Bold.ttf" if bold else "DejaVuSans.ttf")]
    for name in names:
        if name.exists():
            return ImageFont.truetype(str(name), round(size * SCALE))
    raise RuntimeError("Install Segoe UI or DejaVu Sans to render media.")


class Canvas:
    def __init__(self, width, height, background=BG):
        self.image = Image.new("RGB", (width * SCALE, height * SCALE), background)
        self.draw = ImageDraw.Draw(self.image)

    def rect(self, box, fill, radius=0, outline=None, width=1):
        self.draw.rounded_rectangle(tuple(round(v * SCALE) for v in box), radius=round(radius * SCALE),
                                    fill=fill, outline=outline, width=round(width * SCALE))

    def line(self, points, fill, width=1):
        self.draw.line([(round(x * SCALE), round(y * SCALE)) for x, y in points], fill=fill, width=round(width * SCALE))

    def text(self, x, y, value, size=14, color=TEXT, bold=False, anchor="lt"):
        self.draw.text((round(x * SCALE), round(y * SCALE)), value, font=font(size, bold), fill=color, anchor=anchor)

    def wrap(self, x, y, value, width, size=13, color=MUTED, line_height=20):
        words, line = value.split(), ""
        for word in words:
            candidate = (line + " " + word).strip()
            if self.draw.textlength(candidate, font=font(size)) > width * SCALE and line:
                self.text(x, y, line, size, color)
                y += line_height
                line = word
            else:
                line = candidate
        self.text(x, y, line, size, color)
        return y + line_height

    def fish(self, x, y, color, scale=1, opened=True, eye=SURFACE):
        # Exact polygon and mouth poses from the shared native fish rail.
        points = [(0, 0), (17.94, 0), (23, 7.82 if opened else 10.12),
                  (16.79 if opened else 18.86, 11.5), (23, 15.18 if opened else 12.88),
                  (17.94, 23), (0, 23), (2.76, 14.26), (0, 11.5), (2.76, 8.74)]
        self.draw.polygon([((x + (px + 6) * scale) * SCALE, (y + (py + 1.5) * scale) * SCALE)
                           for px, py in points], fill=color)
        tail = [(5, 7.34), (10.66, 13), (5, 18.66), (-0.66, 13)]
        self.draw.polygon([((x + px * scale) * SCALE, (y + py * scale) * SCALE) for px, py in tail], fill=color)
        self.rect((x + 16 * scale, y + 7.5 * scale, x + 20 * scale, y + 11.5 * scale), eye)

    def rail(self, x, y, width, used, color, opened=True, scale=1):
        self.rect((x, y + 11 * scale, x + width, y + 15 * scale), TRACK, 2 * scale)
        offset = max(0, width - 30 * scale) * max(0, min(100, used)) / 100
        mouth = min(width, offset + 30 * scale)
        dim = "#3a706a" if color == MINT else "#735a4e"
        self.rect((x + mouth, y + 11 * scale, x + width, y + 15 * scale), dim, 2 * scale)
        for i in range(12):
            px = width * (i + .5) / 12
            self.draw.ellipse(((x + px - 2.5 * scale) * SCALE, (y + 10.5 * scale) * SCALE,
                               (x + px + 2.5 * scale) * SCALE, (y + 15.5 * scale) * SCALE),
                              fill=color if px >= mouth else TRACK)
        self.fish(x + offset, y, color, scale, opened)

    def save(self, name):
        image = self.image.resize((self.image.width // SCALE, self.image.height // SCALE), Image.Resampling.LANCZOS)
        image.save(OUT / name, optimize=True)
        return image


def button(c, x, y, w, label, selected=False, scale=1):
    c.rect((x, y, x + w, y + 32 * scale), "#303743" if selected else RAISED, 5 * scale, "#3f4856")
    cx, cy = x + w / 2, y + 16 * scale
    if label in ("⋮", "⋯"):
        for i in (-1, 0, 1):
            dx, dy = (0, i * 4 * scale) if label == "⋮" else (i * 4 * scale, 0)
            c.draw.ellipse(((cx + dx - scale) * SCALE, (cy + dy - scale) * SCALE,
                            (cx + dx + scale) * SCALE, (cy + dy + scale) * SCALE), fill=TEXT)
    elif label == "⚙":
        for i in range(8):
            angle = i * math.pi / 4
            c.line([(cx + math.cos(angle) * 5, cy + math.sin(angle) * 5),
                    (cx + math.cos(angle) * 8, cy + math.sin(angle) * 8)], TEXT, 2)
        c.draw.ellipse(((cx - 5) * SCALE, (cy - 5) * SCALE, (cx + 5) * SCALE, (cy + 5) * SCALE), outline=TEXT, width=2 * SCALE)
        c.draw.ellipse(((cx - 1.5) * SCALE, (cy - 1.5) * SCALE, (cx + 1.5) * SCALE, (cy + 1.5) * SCALE), fill=TEXT)
    elif label == "↻":
        c.draw.arc(((cx - 7) * SCALE, (cy - 7) * SCALE, (cx + 7) * SCALE, (cy + 7) * SCALE), 35, 315, fill=TEXT, width=2 * SCALE)
        c.line([(cx + 3, cy - 6), (cx + 7, cy - 5), (cx + 7, cy - 10)], TEXT, 2)
    else:
        c.text(cx, cy, label, 12 * scale, anchor="mm")


def chevron(c, x, y):
    c.line([(x - 4, y - 2), (x, y + 2), (x + 4, y - 2)], MUTED, 1)


def heading(c, title, subtitle):
    c.text(42, 28, "TOKENFISH  /  PRODUCT PREVIEW", 11, LIME, True)
    c.text(42, 54, title, 27, bold=True)
    c.text(42, 94, subtitle, 14, MUTED)


def footer(c, width, height):
    c.text(width / 2, height - 26, "Illustrative mockup · Sample data · 0.2.0-beta.1", 11, MUTED, anchor="mm")


def full_view():
    c = Canvas(720, 970)
    heading(c, "The full picture.", "Quota, resets and freshness for both providers.")
    x, y, w = 154, 146, 412
    c.rect((x + 7, y + 10, x + w + 7, 915), "#080b10", 16)
    c.rect((x, y, x + w, 905), SURFACE, 13)
    c.fish(x + 23, y + 22, LIME)
    c.text(x + 61, y + 18, "TokenFish", 18, bold=True)
    c.text(x + 61, y + 43, "Updated just now", 12, MUTED)
    for bx, label in [(x + 275, "⚙"), (x + 317, "↻"), (x + 359, "×")]:
        button(c, bx, y + 18, 31, label)

    def quota(top, label, left, used, color, reset):
        c.text(x + 36, top, label, 12, MUTED)
        c.text(x + w - 36, top - 3, left, 19, bold=True, anchor="rt")
        c.rail(x + 36, top + 35, w - 72, used, color)
        c.text(x + 36, top + 76, f"{used}% used", 11, MUTED)
        c.text(x + w - 36, top + 76, reset, 11, MUTED, anchor="rt")

    c.rect((x + 20, y + 86, x + w - 20, y + 337), RAISED, 12)
    c.text(x + 36, y + 103, "Codex", 14, bold=True)
    c.text(x + w - 36, y + 105, "Connected", 11, MUTED, anchor="rt")
    quota(y + 139, "Weekly usage", "64% left", 36, MINT, "Resets in 2d 8h")
    c.rect((x + 36, y + 248, x + w - 36, y + 292), "#2a303a", 4, TRACK)
    c.text(x + 50, y + 260, "Codex activity", 13)
    chevron(c, x + w - 48, y + 270)
    c.text(x + 36, y + 308, "Updated just now · Reported by Codex", 11, MUTED)

    c.rect((x + 20, y + 349, x + w - 20, y + 655), RAISED, 12)
    c.text(x + 36, y + 366, "Claude", 14, bold=True)
    c.text(x + w - 36, y + 368, "Connected", 11, MUTED, anchor="rt")
    quota(y + 405, "5h usage", "72% left", 28, PEACH, "Resets in 3h 12m")
    quota(y + 520, "7d usage", "93% left", 7, PEACH, "Resets in 4d 6h")
    c.text(x + 36, y + 629, "Updated just now · Reported by Claude", 11, MUTED)
    c.text(x + 20, y + 707, "Local integrations", 12, MUTED)
    button(c, x + 237, y + 695, 154, "Hide desktop widget")
    footer(c, 720, 970)
    c.save("full-view.png")


def widget_canvas(opened=True, animated=False):
    c = Canvas(1080, 520)
    heading(c, "A little fish in your corner.", "The desktop widget. Both providers, one small glance.")
    x, y, s = 190, 167, 2.45
    w, h = 282 * s, 125 * s
    c.rect((x + 9, y + 11, x + w + 9, y + h + 11), "#080b10", 14)
    c.rect((x, y, x + w, y + h), SURFACE, 11)
    button(c, x + 10 * s, y + 47 * s, 20 * s, "⋮", scale=s)
    for top, label, left, used, color in [(y + 16 * s, "Codex · Weekly usage", "64% left", 36, MINT),
                                        (y + 69 * s, "Claude · 5h usage", "72% left", 28, PEACH)]:
        c.text(x + 37 * s, top, label, 11 * s, MUTED)
        c.text(x + 243 * s, top, left, 12 * s, bold=True, anchor="rt")
        c.rail(x + 37 * s, top + 15 * s, 206 * s, used, color, opened, s)
    button(c, x + 248 * s, y + 47 * s, 24 * s, "⋯", scale=s)
    footer(c, 1080, 520)
    return c


def checkbox(c, x, y, label, checked):
    c.rect((x, y, x + 20, y + 20), MINT if checked else SURFACE, 3, MINT if checked else MUTED)
    if checked:
        c.line([(x + 5, y + 10), (x + 9, y + 14), (x + 16, y + 6)], SURFACE, 2)
    c.text(x + 32, y + 2, label, 14)


def settings(section):
    height = 1000 if section == "Connections" else 900
    c = Canvas(800, height)
    heading(c, "Make it feel at home.", f"Settings / {section}")
    x, y, w, h = 132, 149, 536, height - 223
    c.rect((x + 7, y + 10, x + w + 7, y + h + 10), "#080b10", 14)
    c.rect((x, y, x + w, y + h), SURFACE, 10)
    c.rect((x, y, x + w, y + 32), "#1c2129", 10)
    c.text(x + 14, y + 9, "TokenFish Settings", 11, MUTED)
    c.text(x + w - 20, y + 7, "×", 16, MUTED)
    c.text(x + 24, y + 55, "TokenFish Settings", 20, bold=True)
    c.text(x + 24, y + 90, "Make TokenFish feel at home on your desktop.", 13, MUTED)
    for bx, bw, title in [(x + 24, 113, "Connections"), (x + 145, 88, "Desktop"), (x + 241, 111, "Appearance")]:
        button(c, bx, y + 123, bw, title, title == section)
    left, top = x + 24, y + 185
    if section == "Desktop":
        c.text(left, top, "A little fish in your corner", 18, bold=True)
        checkbox(c, left, top + 49, "Show desktop widget", True)
        c.wrap(left, top + 84, "Shows one usage line per enabled provider. Click a fish to open the full view. Hiding the widget keeps TokenFish in the tray.", w - 48)
        checkbox(c, left, top + 163, "Always on top", False)
        c.wrap(left, top + 199, "Keep the widget above other applications when you want it in view.", w - 48)
        c.text(left, top + 260, "Corner", 13)
        c.rect((left, top + 286, x + w - 24, top + 322), RAISED, 5, TRACK)
        c.text(left + 12, top + 295, "Bottom right", 13)
        chevron(c, x + w - 43, top + 303)
        c.wrap(left, top + 348, "Drag the handle to move between displays. Settings and the full view open beside the surface you used.", w - 48)
    elif section == "Appearance":
        c.text(left, top, "One look across TokenFish", 18, bold=True)
        c.text(left, top + 52, "Appearance", 13)
        c.rect((left, top + 79, x + w - 24, top + 115), RAISED, 5, TRACK)
        c.text(left + 12, top + 88, "Dark", 13)
        chevron(c, x + w - 43, top + 97)
        c.wrap(left, top + 141, "Applies immediately to the widget, usage view, and Settings. Windows high contrast takes priority.", w - 48)
        c.text(left, top + 211, "TokenFish 0.2.0-beta.1", 13, MUTED)
    else:
        c.text(left, top, "Providers", 16, bold=True)
        c.wrap(left, top + 32, "Choose which local provider data TokenFish shows after the next launch.", w - 48)
        for dy, label, selected in [(87, "Codex", False), (130, "Claude", False), (173, "Codex and Claude", True)]:
            c.draw.ellipse(((left) * SCALE, (top + dy) * SCALE, (left + 20) * SCALE, (top + dy + 20) * SCALE), outline=MINT if selected else MUTED, width=2 * SCALE)
            if selected:
                c.draw.ellipse(((left + 5) * SCALE, (top + dy + 5) * SCALE, (left + 15) * SCALE, (top + dy + 15) * SCALE), fill=MINT)
            c.text(left + 32, top + dy + 2, label, 14)
        c.text(left, top + 225, "Current provider readiness", 16, bold=True)
        c.text(left, top + 259, "Codex · Ready", 13, MUTED)
        c.text(left, top + 287, "Claude · Ready", 13, MUTED)
        c.rect((left, top + 326, x + w - 24, top + 366), RAISED, 5, TRACK)
        c.text(left + 12, top + 338, "Claude setup instructions", 13)
        chevron(c, x + w - 43, top + 347)
        c.text(left, top + 395, "Codex runtime", 16, bold=True)
        c.rect((left, top + 428, x + w - 24, top + 464), RAISED, 5, TRACK)
        c.text(left + 12, top + 437, "Native Windows", 13)
        chevron(c, x + w - 43, top + 446)
        # The real Connections panel scrolls to additional runtime options.
        c.rect((x + w - 11, y + 185, x + w - 7, y + 421), "#596372", 2)
    button(c, x + w - 171, y + h - 55, 66, "Close")
    button(c, x + w - 97, y + h - 55, 73, "Save")
    footer(c, 800, height)
    c.save(f"settings-{section.lower()}.png")


if __name__ == "__main__":
    full_view()
    widget_canvas().save("desktop-widget.png")
    for section in ("Connections", "Desktop", "Appearance"):
        settings(section)
    frames = [widget_canvas(opened=opened).image.resize((864, 416), Image.Resampling.LANCZOS)
              for opened in (True, False)]
    # One shared palette avoids color flicker between the two mouth poses.
    palette = frames[0].quantize(colors=128)
    frames = [frame.quantize(palette=palette, dither=Image.Dither.NONE) for frame in frames]
    frames[0].save(OUT / "fish-chomping.gif", save_all=True, append_images=frames[1:],
                   duration=[170, 170], loop=0, disposal=2, optimize=False)
    print(f"Rendered five mockups and the 340 ms fish animation in {OUT}")
