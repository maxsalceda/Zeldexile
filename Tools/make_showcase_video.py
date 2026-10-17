"""
Builds the hero showcase video from the frames recorded by ShowcaseCaptureTests.

    python Tools/make_showcase_video.py [frames dir] [output mp4]

Defaults: Zeldexile/Logs/showcase -> Docs/media/heroi-acoes.mp4. Each frame gets the name of the action it
shows (from labels.txt). Needs: pip install pillow "imageio[ffmpeg]".
"""

import os
import sys

import imageio.v2 as imageio
from PIL import Image, ImageDraw, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
frames_dir = sys.argv[1] if len(sys.argv) > 1 else os.path.join(ROOT, "Zeldexile", "Logs", "showcase")
output = sys.argv[2] if len(sys.argv) > 2 else os.path.join(ROOT, "Docs", "media", "heroi-acoes.mp4")

labels = open(os.path.join(frames_dir, "labels.txt"), encoding="utf-8").read().splitlines()
frames = sorted(f for f in os.listdir(frames_dir) if f.endswith(".jpg"))
try:
    font = ImageFont.truetype("arialbd.ttf", 30)
except OSError:
    font = ImageFont.load_default()

os.makedirs(os.path.dirname(output), exist_ok=True)
writer = imageio.get_writer(output, fps=24, codec="libx264", quality=8, macro_block_size=8)
for i, name in enumerate(frames):
    img = Image.open(os.path.join(frames_dir, name)).convert("RGB")
    draw = ImageDraw.Draw(img)
    text = labels[i] if i < len(labels) else ""
    draw.rectangle((0, 0, img.width, 50), fill=(20, 24, 30))
    draw.text((20, 9), text, fill=(240, 230, 200), font=font)
    writer.append_data(__import__("numpy").asarray(img))
writer.close()
print(f"{len(frames)} frames -> {output}")
