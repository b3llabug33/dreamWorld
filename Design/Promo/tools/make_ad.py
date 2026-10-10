"""
Cuts the recorded gameplay frames into a ~35 second vertical (1080x1920) ad for TikTok/Reels:
pixel-font captions that type themselves out, soft flash cuts, and an end card.
  python make_ad.py <frames root> <output.mp4>
<frames root> holds one folder of 1920x1080 PNG (or older JPEG) frames per clip (creator, hair, eyes, walk, fight, death, town),
as written by dreamWorld's ClipRecorder. Needs Pillow + imageio-ffmpeg (for H.264).
"""
import glob
import os
import random
import subprocess
import sys

import imageio_ffmpeg
from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT, OUT = sys.argv[1], sys.argv[2]
HERE = os.path.dirname(os.path.abspath(__file__))
FONTS = os.path.join(HERE, "..", "..", "..", "Assets", "Fonts")
TITLE = os.path.join(FONTS, "PixelifySans.ttf")
BODY = os.path.join(FONTS, "VT323-Regular.ttf")

W, H, FPS = 1080, 1920, 30
CREAM, PINK, LAVENDER, DIM = (255, 245, 235), (250, 140, 190), (200, 184, 250), (160, 148, 200)
WIN = (40, 300, 1040, 1550)          # video window: 1000 x 1250 (4:5)

# (clip, first frame, frames, focus x 0-1, big caption, small caption)
SEGMENTS = [
    ("town", 0, 75, 0.5, "i'm making a cozy retro game", "> dreamWorld  (in development)"),
    ("hair", 0, 205, 0.5, "make you", "every hairstyle, any color"),
    ("creator", 117, 90, 0.5, "mix + match outfits", "stripes, hearts, leopard + more"),
    ("eyes", 20, 150, 0.5, "every little detail", "12 eye styles + any eye color"),
    ("walk", 0, 72, 0.5, "explore mushroom hollow", "a glowing village in the dream woods"),
    ("fight", 262, 80, 0.55, "elden ring style combat", "roll, lock on, bonk the shroomling"),
    ("fight", 342, 75, 0.5, "level up from 0", "health, stamina, magic, strength, luck"),
    ("death", 120, 100, 0.5, "respawn at your home base", "bank your coins at home to keep them safe"),
]
END_FRAMES = 100
SOON_FRAMES = 120
COMING_SOON = ["pvp", "base customization", "fashion design", "quests", "vampires"]


def font(path, size):
    return ImageFont.truetype(path, size)


F_BIG, F_SMALL, F_FOOT = font(TITLE, 74), font(BODY, 50), font(BODY, 44)
F_LOGO, F_END, F_URL = font(TITLE, 150), font(BODY, 58), font(TITLE, 64)


def fit(d, text, size, width):
    """The title font at the biggest size (up to `size`) where the full caption fits in `width`."""
    while size > 40 and d.textlength(text, font=font(TITLE, size)) > width:
        size -= 2
    return font(TITLE, size)


def frames(clip):
    found = glob.glob(os.path.join(ROOT, clip, "*.png")) or glob.glob(os.path.join(ROOT, clip, "*.jpg"))
    return sorted(found, key=lambda f: int(f.split("_")[-1].split(".")[0]))


CLIPS = {c: frames(c) for c in {s[0] for s in SEGMENTS}}


def background():
    bg = Image.new("RGB", (W, H))
    d = ImageDraw.Draw(bg)
    for y in range(H):   # twilight gradient
        t = y / H
        d.line([(0, y), (W, y)], fill=(int(20 + 26 * t), int(12 + 8 * t), int(42 + 26 * t)))
    rng = random.Random(4)
    for _ in range(140):   # pixel stars
        x, y = rng.randrange(W), rng.randrange(H)
        s = rng.choice((2, 3, 4))
        d.rectangle([x, y, x + s, y + s], fill=(255, 240, 255) if rng.random() < 0.7 else LAVENDER)
    return bg


BG = background()

def shadow_text(d, xy, text, f, fill, shadow=PINK, off=6, anchor="mm"):
    d.text((xy[0] + off, xy[1] + off), text, font=f, fill=shadow, anchor=anchor)
    d.text(xy, text, font=f, fill=fill, anchor=anchor)


def game_frame(path, focus, zoom):
    src = Image.open(path).convert("RGB")
    ch = 1040                             # drop the bottom hint line; no push-in (re-scaling pixel art every frame makes it shimmer)
    cw = ch * (WIN[2] - WIN[0]) / (WIN[3] - WIN[1])
    cx = min(max(focus * 1920, cw / 2), 1920 - cw / 2)
    cy = 520
    box = (cx - cw / 2, cy - ch / 2, cx + cw / 2, cy + ch / 2)
    return src.resize((WIN[2] - WIN[0], WIN[3] - WIN[1]), Image.LANCZOS, box=box)


def frame_border(d):
    x0, y0, x1, y1 = WIN
    for i, c in enumerate((LAVENDER, (60, 40, 100), LAVENDER)):
        d.rectangle([x0 - 8 + i * 3, y0 - 8 + i * 3, x1 + 7 - i * 3, y1 + 7 - i * 3], outline=c, width=2)
    for cx, cy in ((x0 - 8, y0 - 8), (x1 + 2, y0 - 8), (x0 - 8, y1 + 2), (x1 + 2, y1 + 2)):   # notched corners
        d.rectangle([cx, cy, cx + 5, cy + 5], fill=BG.getpixel((cx, cy)))


def footer(d, i):
    cursor = "_" if (i // 15) % 2 == 0 else " "
    d.text((W // 2, 1660), "> dreamWorld.exe" + cursor, font=F_FOOT, fill=CREAM, anchor="mm")
    d.text((W // 2, 1730), "follow the devlog  |  bellabug.dev", font=F_FOOT, fill=DIM, anchor="mm")


def glitch(img, k, rng):
    """First frames of a cut: a quick soft flash (the old torn-band glitch read as video errors)."""
    if k >= 3:
        return img
    return Image.blend(img, Image.new("RGB", img.size, (255, 230, 250)), (0.3, 0.15, 0.06)[k])


def segment_frames():
    rng = random.Random(7)
    n = 0
    for clip, start, count, focus, big, small in SEGMENTS:
        files = CLIPS[clip]
        for k in range(count):
            path = files[min(start + k, len(files) - 1)]
            img = BG.copy()
            img.paste(game_frame(path, focus, 1.0 + 0.06 * k / count), (WIN[0], WIN[1]))
            d = ImageDraw.Draw(img)
            frame_border(d)
            typed = big[: max(0, int(k * 32 / FPS) + 1)]
            shadow_text(d, (W // 2, 140), typed, fit(d, big, 74, 960), CREAM)
            if k > 8:
                d.text((W // 2, 232), small, font=F_SMALL, fill=LAVENDER, anchor="mm")
            footer(d, n)
            img = glitch(img, k, rng)
            n += 1
            yield img
    # end card: the town, blurred and dimmed, with the logo
    last = Image.open(CLIPS["town"][len(CLIPS["town"]) // 2]).convert("RGB")
    scale = H / last.height
    back = last.resize((int(last.width * scale), H), Image.LANCZOS)
    back = back.crop(((back.width - W) // 2, 0, (back.width - W) // 2 + W, H)).filter(ImageFilter.GaussianBlur(10))
    back = Image.blend(back, Image.new("RGB", (W, H), (16, 8, 34)), 0.6)
    # coming soon: the roadmap typed out like a terminal checklist
    for k in range(SOON_FRAMES):
        img = back.copy()
        d = ImageDraw.Draw(img)
        shadow_text(d, (W // 2, 560), "coming soon"[: max(0, int(k * 24 / FPS) + 1)], F_LOGO, CREAM, off=8)
        for j, item in enumerate(COMING_SOON):
            appear = 14 + j * 12
            if k < appear:
                continue
            typed = item[: max(0, int((k - appear) * 30 / FPS) + 1)]
            d.text((300, 760 + j * 92), "[ ] " + typed, font=F_URL, fill=CREAM if j % 2 == 0 else LAVENDER, anchor="lm")
        footer(d, n + k)
        img = glitch(img, k, rng)
        yield img
    for k in range(END_FRAMES):
        img = back.copy()
        d = ImageDraw.Draw(img)
        logo = "dreamWorld"[: max(0, int(k * 20 / FPS) + 1)]
        shadow_text(d, (W // 2, 760), logo, F_LOGO, CREAM, off=9)
        if k > 18:
            d.text((W // 2, 900), "a cozy retro world in the making", font=F_END, fill=LAVENDER, anchor="mm")
        if k > 32:
            d.text((W // 2, 990), "follow along for the devlog", font=F_END, fill=CREAM, anchor="mm")
        if k > 46:
            cursor = "_" if (k // 12) % 2 == 0 else " "
            shadow_text(d, (W // 2, 1130), "bellabug.dev" + cursor, F_URL, PINK, shadow=(40, 20, 70), off=5)
        img = glitch(img, k, rng)
        yield img


def main():
    os.makedirs(os.path.dirname(os.path.abspath(OUT)), exist_ok=True)
    cmd = [imageio_ffmpeg.get_ffmpeg_exe(), "-y", "-loglevel", "error", "-f", "rawvideo", "-pix_fmt", "rgb24",
           "-s", f"{W}x{H}", "-r", str(FPS), "-i", "-", "-c:v", "libx264", "-preset", "slow", "-crf", "15", "-tune", "animation",
           "-pix_fmt", "yuv420p", "-movflags", "+faststart", OUT]
    proc = subprocess.Popen(cmd, stdin=subprocess.PIPE)
    count = 0
    for img in segment_frames():
        proc.stdin.write(img.tobytes())
        count += 1
        if count % 100 == 0:
            print("frames", count, flush=True)
    proc.stdin.close()
    proc.wait()
    print("wrote", OUT, count, "frames", f"{count / FPS:.1f}s")


if __name__ == "__main__":
    main()
