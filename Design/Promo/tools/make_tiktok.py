"""
Cuts recorded gameplay frames into a vertical (1080x1920) TikTok/Reels video, in the same style as
make_ad.py: pixel-font captions that type themselves out, a framed game window, soft flash cuts, a
"coming soon" checklist and an end card.
  python make_tiktok.py <video name> <frames root> <output.mp4>
<video name> is one of the VIDEOS below. <frames root> holds one folder of 1920x1080 PNG frames per clip,
as written by dreamWorld's ClipRecorder (ClipRecorder.Record(scene, folder, framesFolder)).
The game window is square; clips with wide menus (dialogue, the workbench) use a wider crop and get
soft blurred bars above and below instead of cutting the menu off.
Needs Pillow + imageio-ffmpeg.
"""
import glob
import os
import random
import subprocess
import sys

import imageio_ffmpeg
from PIL import Image, ImageDraw, ImageFilter, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
FONTS = os.path.join(HERE, "..", "..", "..", "Assets", "Fonts")
TITLE = os.path.join(FONTS, "PixelifySans.ttf")
BODY = os.path.join(FONTS, "VT323-Regular.ttf")

W, H, FPS = 1080, 1920, 30
CREAM, PINK, LAVENDER, DIM = (255, 245, 235), (250, 140, 190), (200, 184, 250), (160, 148, 200)
WIN = (20, 420, 1060, 1460)          # video window: 1040 x 1040 (below TikTok's top tabs, above its caption area)

# Each segment: (clip, first frame, frames, focus x 0-1, crop width in the 1920x1080 frame, big caption, small caption
#                [, speed: source frames per output frame, e.g. 1.6 plays it 60% faster])
VIDEOS = {
    "make_you": {
        "segments": [
            ("bodies", 14, 50, 0.5, 1080, "i'm making a cozy retro game", "> dreamWorld  (in development)"),
            ("bodies", 64, 150, 0.5, 1080, "make your character", "any body type, any height"),
            ("hairs", 18, 240, 0.48, 1080, "so many hairstyles", "in any color you want"),
            ("outfits", 18, 270, 0.5, 1080, "mix and match outfits", "patterns, plaid, boots and more"),
            ("details", 24, 160, 0.48, 1080, "every little detail", "pick your eyes and little extras"),
        ],
        "end_clip": "outfits",
    },
    "the_town": {
        "segments": [
            ("walk", 10, 120, 0.5, 1080, "welcome to mushroom hollow", "a cozy town in the dream woods"),
            ("orion", 60, 240, 0.5, 1200, "this is orion", "she's based on my real pet beetle"),
            ("snackula", 26, 165, 0.5, 1200, "everyone here has something to say", "count snackula has gossip"),
            ("craft", 168, 165, 0.5, 1400, "craft your own furniture", "pick the wood and it changes color"),
            ("decor", 40, 190, 0.5, 1080, "then decorate your home", "place it anywhere you like", 1.6),
            ("mistpine", 112, 100, 0.5, 1080, "there's a foggy forest too", "home to a very sparkly vampire family"),
        ],
        "end_clip": "walk",
    },
}
COMING_SOON = ["trading", "fashion design", "base design", "pvp"]
END_FRAMES = 110
SOON_FRAMES = 110


def font(path, size):
    return ImageFont.truetype(path, size)


F_SMALL, F_FOOT = font(BODY, 50), font(BODY, 44)
F_LOGO, F_END, F_URL = font(TITLE, 150), font(BODY, 56), font(TITLE, 64)


def fit(d, text, size, width):
    """The title font at the biggest size (up to `size`) where the full caption fits in `width`."""
    while size > 40 and d.textlength(text, font=font(TITLE, size)) > width:
        size -= 2
    return font(TITLE, size)


def frames(root, clip):
    found = glob.glob(os.path.join(root, clip, "*.png"))
    return sorted(found, key=lambda f: int(f.split("_")[-1].split(".")[0]))


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


def game_frame(path, focus, crop_w):
    """The game frame for the window: a square crop, or a wider crop letterboxed over a blurred copy."""
    src = Image.open(path).convert("RGB")
    ww, wh = WIN[2] - WIN[0], WIN[3] - WIN[1]
    ch = 1080
    cx = min(max(focus * 1920, crop_w / 2), 1920 - crop_w / 2)
    box = (cx - crop_w / 2, 0, cx + crop_w / 2, ch)
    if crop_w <= ch:
        return src.resize((ww, wh), Image.LANCZOS, box=box)
    back = src.resize((ww, wh), Image.BILINEAR, box=(cx - ch / 2, 0, cx + ch / 2, ch)).filter(ImageFilter.GaussianBlur(18))
    back = Image.blend(back, Image.new("RGB", (ww, wh), (16, 8, 34)), 0.45)
    h = round(ww * ch / crop_w)
    back.paste(src.resize((ww, h), Image.LANCZOS, box=box), (0, (wh - h) // 2))
    return back


def frame_border(d):
    x0, y0, x1, y1 = WIN
    for i, c in enumerate((LAVENDER, (60, 40, 100), LAVENDER)):
        d.rectangle([x0 - 8 + i * 3, y0 - 8 + i * 3, x1 + 7 - i * 3, y1 + 7 - i * 3], outline=c, width=2)


def footer(d, i):
    cursor = "_" if (i // 15) % 2 == 0 else " "
    d.text((W // 2, 1535), "> dreamWorld.exe" + cursor, font=F_FOOT, fill=CREAM, anchor="mm")
    d.text((W // 2, 1595), "follow the devlog  |  bellabug.dev", font=F_FOOT, fill=DIM, anchor="mm")


def flash(img, k):
    """First frames of a cut: a quick soft flash."""
    if k >= 3:
        return img
    return Image.blend(img, Image.new("RGB", img.size, (255, 230, 250)), (0.3, 0.15, 0.06)[k])


def video_frames(video, root):
    spec = VIDEOS[video]
    clips = {s[0]: frames(root, s[0]) for s in spec["segments"]}
    for name, files in clips.items():
        if not files:
            sys.exit(f"no frames for {name} in {root}")
    n = 0
    prev_caption = None
    for seg in spec["segments"]:
        clip, start, count, focus, crop_w, big, small = seg[:7]
        speed = seg[7] if len(seg) > 7 else 1.0
        files = clips[clip]
        new_caption = big != prev_caption
        for k in range(count):
            path = files[min(start + int(k * speed), len(files) - 1)]
            img = BG.copy()
            img.paste(game_frame(path, focus, crop_w), (WIN[0], WIN[1]))
            d = ImageDraw.Draw(img)
            frame_border(d)
            typed = big[: max(0, int(k * 32 / FPS) + 1)] if new_caption else big
            shadow_text(d, (W // 2, 262), typed, fit(d, big, 74, 1000), CREAM)
            if k > 8 or not new_caption:
                d.text((W // 2, 352), small, font=F_SMALL, fill=LAVENDER, anchor="mm")
            footer(d, n)
            n += 1
            yield flash(img, k) if new_caption or k < 3 else img
        prev_caption = big
    # end: a blurred, dimmed game frame behind the coming soon list and the logo
    end = clips[spec["end_clip"]]
    last = Image.open(end[len(end) // 2]).convert("RGB")
    scale = H / last.height
    back = last.resize((int(last.width * scale), H), Image.LANCZOS)
    back = back.crop(((back.width - W) // 2, 0, (back.width - W) // 2 + W, H)).filter(ImageFilter.GaussianBlur(10))
    back = Image.blend(back, Image.new("RGB", (W, H), (16, 8, 34)), 0.6)
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
        yield flash(img, k)
    for k in range(END_FRAMES):
        img = back.copy()
        d = ImageDraw.Draw(img)
        shadow_text(d, (W // 2, 720), "dreamWorld"[: max(0, int(k * 20 / FPS) + 1)], F_LOGO, CREAM, off=9)
        if k > 18:
            d.text((W // 2, 860), "a cozy retro online game", font=F_END, fill=LAVENDER, anchor="mm")
        if k > 30:
            d.text((W // 2, 940), "for anyone who misses the online", font=F_END, fill=CREAM, anchor="mm")
            d.text((W // 2, 1000), "games we grew up with", font=F_END, fill=CREAM, anchor="mm")
        if k > 46:
            d.text((W // 2, 1100), "follow along while i make it", font=F_END, fill=LAVENDER, anchor="mm")
            cursor = "_" if (k // 12) % 2 == 0 else " "
            shadow_text(d, (W // 2, 1210), "bellabug.dev" + cursor, F_URL, PINK, shadow=(40, 20, 70), off=5)
        yield flash(img, k)


def main():
    video, root, out = sys.argv[1], sys.argv[2], sys.argv[3]
    os.makedirs(os.path.dirname(os.path.abspath(out)), exist_ok=True)
    cmd = [imageio_ffmpeg.get_ffmpeg_exe(), "-y", "-loglevel", "error", "-f", "rawvideo", "-pix_fmt", "rgb24",
           "-s", f"{W}x{H}", "-r", str(FPS), "-i", "-", "-c:v", "libx264", "-preset", "slow", "-crf", "15", "-tune", "animation",
           "-pix_fmt", "yuv420p", "-movflags", "+faststart", out]
    proc = subprocess.Popen(cmd, stdin=subprocess.PIPE)
    count = 0
    for img in video_frames(video, root):
        proc.stdin.write(img.tobytes())
        count += 1
        if count % 100 == 0:
            print("frames", count, flush=True)
    proc.stdin.close()
    proc.wait()
    print("wrote", out, count, "frames", f"{count / FPS:.1f}s")


if __name__ == "__main__":
    main()
