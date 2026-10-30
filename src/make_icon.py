"""生成 PsCtrlGuard 的图标：深色圆角方块 + 白色光标 + 红色禁止斜杠。"""
from PIL import Image, ImageDraw

S = 1024  # 超采样画布
img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
d = ImageDraw.Draw(img)

# 圆角底
pad = int(S * 0.06)
d.rounded_rectangle([pad, pad, S - pad, S - pad], radius=int(S * 0.22),
                    fill=(32, 34, 44, 255))

# 光标箭头
arrow = [(0.34, 0.16), (0.34, 0.80), (0.49, 0.65), (0.58, 0.88),
         (0.68, 0.83), (0.59, 0.61), (0.78, 0.61)]
pts = [(x * S, y * S) for x, y in arrow]
d.polygon(pts, fill=(245, 246, 250, 255))

# 禁止斜杠：先画深色描边，再画红色
def slash(width, color):
    w = int(S * width)
    d.line([(0.16 * S, 0.16 * S), (0.84 * S, 0.84 * S)], fill=color, width=w)
    r = w // 2
    for cx, cy in ((0.16 * S, 0.16 * S), (0.84 * S, 0.84 * S)):
        d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=color)

slash(0.135, (22, 23, 30, 255))
slash(0.095, (232, 72, 77, 255))

img = img.resize((256, 256), Image.LANCZOS)
img.save("icon_preview.png")
img.save("icon.ico", sizes=[(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)])
print("ok")
