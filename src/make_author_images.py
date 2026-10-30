"""把作者名片和头像压成适合放进仓库 / 嵌进程序的尺寸。"""
import os
from PIL import Image

SRC = r"C:\Users\shiqi\.dsh\attachments\v1\objects"
CARD = os.path.join(SRC, "a0", "a09556e9c42a7d8bce469f1c4ca464a060bfe0219e3f78b90625c8605c25fd94")
FACE = os.path.join(SRC, "30", "3085eda84a5513e65cbf9ed352ecd15c7234dc2d97fceea0039dce1c6439129e")  # 鲜美.png
FULL = os.path.join(SRC, "f7", "f7c975475a8f166151730b3d21feaa2b5f1732f25dfd8712f1244314bc3bfd58")  # 忧郁之物.png

OUT = r"C:\Users\shiqi\Documents\deepseek-harness\default-workspace\chutouniao\docs"
SRC_OUT = r"C:\Users\shiqi\Documents\deepseek-harness\default-workspace\chutouniao\src"

os.makedirs(OUT, exist_ok=True)

def save_jpg(im, path, quality=84):
    im.convert("RGB").save(path, "JPEG", quality=quality, optimize=True, progressive=True)

# 1) 名片：宽 760
im = Image.open(CARD).convert("RGB")
w = 760
im2 = im.resize((w, round(im.height * w / im.width)), Image.LANCZOS)
save_jpg(im2, os.path.join(OUT, "author-card.jpg"), 85)

# 2) 大头照：900 见方
im = Image.open(FACE).convert("RGB")
s = 900
im2 = im.resize((s, round(im.height * s / im.width)), Image.LANCZOS)
save_jpg(im2, os.path.join(OUT, "author.jpg"), 86)

# 3) 全身照：宽 620
im = Image.open(FULL).convert("RGB")
w = 620
im2 = im.resize((w, round(im.height * w / im.width)), Image.LANCZOS)
save_jpg(im2, os.path.join(OUT, "author-full.jpg"), 85)

# 4) 程序里用的小头像：从大头照裁一张方脸，缩到 96
im = Image.open(FACE).convert("RGB")
w, h = im.size
side = int(min(w, h) * 0.62)
left = (w - side) // 2
top = int(h * 0.16)
crop = im.crop((left, top, left + side, top + side)).resize((96, 96), Image.LANCZOS)
# 圆角
mask = Image.new("L", (96, 96), 0)
from PIL import ImageDraw
ImageDraw.Draw(mask).ellipse((0, 0, 95, 95), fill=255)
out = Image.new("RGBA", (96, 96), (0, 0, 0, 0))
out.paste(crop, (0, 0), mask)
out.save(os.path.join(SRC_OUT, "avatar.png"), optimize=True)

for name in ("author-card.jpg", "author.jpg", "author-full.jpg"):
    p = os.path.join(OUT, name)
    print(name, round(os.path.getsize(p) / 1024, 1), "KB")
print("avatar.png", round(os.path.getsize(os.path.join(SRC_OUT, "avatar.png")) / 1024, 1), "KB")
