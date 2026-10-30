"""生成 GitHub 仓库的 Social Preview 图（1280x640）。"""
import os
from PIL import Image, ImageDraw, ImageFont

W, H = 1280, 640
OUT = r"C:\Users\shiqi\Documents\deepseek-harness\default-workspace\chutouniao\.github\social-preview.png"
os.makedirs(os.path.dirname(OUT), exist_ok=True)

BG = (31, 34, 48)
ACCENT = (232, 72, 77)

img = Image.new("RGB", (W, H), BG)
d = ImageDraw.Draw(img)

# 背景几何：几道斜线
for i in range(-8, 22):
    x = i * 90
    d.line([(x, H + 40), (x + 300, -40)], fill=(38, 42, 58), width=2)

# 右下角渐变块
overlay = Image.new("RGBA", (W, H), (0, 0, 0, 0))
od = ImageDraw.Draw(overlay)
od.ellipse([W - 520, H - 220, W + 260, H + 380], fill=(232, 72, 77, 26))
img = Image.alpha_composite(img.convert("RGBA"), overlay).convert("RGB")
d = ImageDraw.Draw(img)


def icon(size=300):
    S = size * 4
    im = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    dd = ImageDraw.Draw(im)
    pad = int(S * 0.06)
    dd.rounded_rectangle([pad, pad, S - pad, S - pad], radius=int(S * 0.22), fill=(46, 50, 70, 255))
    dd.rounded_rectangle([pad, pad, S - pad, S - pad], radius=int(S * 0.22),
                         outline=(72, 78, 104, 255), width=max(2, S // 128))
    arrow = [(0.34, 0.16), (0.34, 0.80), (0.49, 0.65), (0.58, 0.88),
             (0.68, 0.83), (0.59, 0.61), (0.78, 0.61)]
    dd.polygon([(x * S, y * S) for x, y in arrow], fill=(245, 246, 250, 255))

    def slash(width, color):
        w = int(S * width)
        dd.line([(0.16 * S, 0.16 * S), (0.84 * S, 0.84 * S)], fill=color, width=w)
        r = w // 2
        for cx, cy in ((0.16 * S, 0.16 * S), (0.84 * S, 0.84 * S)):
            dd.ellipse([cx - r, cy - r, cx + r, cy + r], fill=color)

    slash(0.135, (22, 23, 30, 255))
    slash(0.095, (232, 72, 77, 255))
    return im.resize((size, size), Image.LANCZOS)


ICON = icon(280)
img.paste(ICON, (W - 280 - 96, 168), ICON)

font_dir = r"C:\Windows\Fonts"
def load(name, size):
    try:
        return ImageFont.truetype(os.path.join(font_dir, name), size)
    except Exception:
        return ImageFont.load_default()

f_title = load("msyhbd.ttc", 92)
f_sub = load("msyh.ttc", 40)
f_small = load("msyh.ttc", 26)
f_tag = load("msyh.ttc", 24)

x = 92
d.text((x, 132), "出头鸟", font=f_title, fill=(255, 255, 255))
tw = d.textlength("出头鸟", font=f_title)
d.text((x + tw + 20, 176), "Chutouniao", font=load("msyh.ttc", 52), fill=(150, 160, 190))

d.text((x, 262), "世界若无出头鸟，人人皆为待宰鸡", font=f_sub, fill=(232, 235, 245))

# 红色小竖条
d.rectangle([x, 340, x + 6, 396], fill=ACCENT)
d.text((x + 24, 344), "把改不掉的快捷键，改掉。", font=f_small, fill=(170, 178, 200))

d.text((x, 468), "Windows 10 / 11　·　MIT License　·　墨琛COMU", font=f_tag, fill=(120, 128, 150))

img.save(OUT, "PNG", optimize=True)
print("saved", OUT, os.path.getsize(OUT) // 1024, "KB")
