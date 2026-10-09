# Recording dreamWorld for TikTok (vertical 9:16 on Windows)

Goal: crisp 1080x1920 clips where the PS1 pixels still look like pixels after TikTok compresses
them. Everything below works with free tools.

> Anything marked **SUGGESTION (main session)** is a change to the Unity project. It has not been
> made. It's listed for whoever is working in the Unity project to decide on.

## 0. Know the two problems first

1. **The UI is built for landscape.** The creator and login canvases use a 1920x1080 reference
   resolution, so a portrait Game view squeezes the side panels together.
2. **The PS1 effect is based on screen height.** `PSXCamera` renders the 3D image 240 px tall and
   picks the width from the aspect ratio. In a portrait 1080x1920 Game view that's only
   **135 px wide**, which looks much chunkier than the landscape game.

So the easiest, most faithful approach is: **record landscape, crop or frame to vertical later.**

## 1. Pick a framing per shot

| Framing | Use it for | How |
|---|---|---|
| **A. Center crop** (9:16 slice out of the 16:9 frame) | Face close-ups, hair cycling, character spins, walking in the village | Crop the middle ~608x1080 of a 1920x1080 recording and scale it up 1.78x |
| **B. Framed landscape** (full 16:9 frame in the middle, text above and below on a `#1a1030` background) | Anything where the menus matter: category list, color sliders, login, players screen | Place the landscape clip at full width, add your caption in the top band |
| **C. True portrait capture** | Clean b-roll without UI | Unity Recorder "Targeted Camera" at 1080x1920 (section 3), ideally with the PSX suggestion in section 6 |

Framing A and B together cover almost every script in `Scripts.md`.

## 2. OBS Studio (free, recommended starting point)

Download from obsproject.com.

**Option 1: vertical canvas, crop in OBS (one step, ready to post)**

1. *Settings > Video*: Base (canvas) **1080x1920**, Output **1080x1920**, FPS **30** (60 is fine
   too but 30 keeps file sizes small and TikTok shows 30 fps for most uploads anyway).
2. *Settings > Output > Recording*: Recording Quality **Indistinguishable Quality** (or "High
   Quality, Medium File Size"), Format **MP4** (newer OBS) or MKV then *File > Remux Recordings*
   to MP4. Encoder: hardware (NVENC/AMD/QuickSync) if available.
3. Add a **Window Capture** source pointed at the Unity Editor (or at a game build, see below).
4. Right-click the source > **Scale Filtering > Point**. This is the important one: it keeps the
   PS1 pixels as hard squares instead of blurring them.
5. Hold **Alt** and drag the source edges to crop down to the Game view only, then to the slice you
   want. Scale it to fill the 1080x1920 canvas. Use *Edit Transform* (Ctrl+E) for exact numbers.
6. Make a second **scene** in OBS for framing B: background color source `#1a1030`, the same
   window capture cropped to the whole Game view, scaled to 1080 wide in the middle.
7. Set hotkeys for Start/Stop Recording so you don't have to alt-tab mid-take.

**Option 2: record landscape, crop in CapCut later**

Keep OBS at 1920x1080 (or 2560x1440) and do all framing in CapCut. More flexible, because one
recording can become a center-crop clip *and* a framed clip. Good when batch-recording.

**Getting a clean Game view to capture (no project changes):**

- Game view toolbar: set the aspect dropdown to a fixed **1920x1080** (add it with the `+` if it
  isn't listed), turn **Gizmos off**, **Stats off**, Scale slider at **1x**.
- Uncheck **Low Resolution Aspect Ratios** (only matters on high-DPI displays).
- Enter Play mode with **Play Maximized** (dropdown next to the Game tab) or press **Shift+Space**
  over the Game view so it fills the monitor. On a 1080p monitor the editor toolbar still takes
  some rows, so the Game view may be slightly smaller than 1080 tall; that's fine for crops.
- Better quality, if you have time: make a Windows build (*File > Build Profiles > Windows*,
  Windowed 1920x1080) and capture that window with **Game Capture**. No editor chrome, steadier
  frame rate.
- Hide your mouse cursor in OBS (uncheck "Capture Cursor") for shots that don't need clicks. For
  click-drag spins you'll see no cursor, which actually looks cleaner.

## 3. Unity Recorder (inside Unity, frame-perfect)

**SUGGESTION (main session):** add the **Unity Recorder** package (`com.unity.recorder`) through
*Window > Package Manager > Unity Registry*. It's free and records at an exact resolution and
frame rate even if the editor stutters.

Once installed (*Window > General > Recorder > Recorder Window*):

- Add a **Movie** recorder. Format **MP4 (H.264)**, Quality **High**, Frame Rate **30**, Playback
  **Constant**, *Cap FPS* on.
- **Source: Game View**, Output Resolution **1920x1080** (landscape, keeps the UI correct). Crop
  to vertical in CapCut.
- **Source: Targeted Camera**, camera **MainCamera**, Output Resolution **1080x1920**: records only
  the 3D camera, so screen-space UI is left out. This is a free "photo mode" for clean b-roll.
  Check the result once: the PSX post effect should still apply, but in portrait it will be the
  extra-chunky 135 px-wide version described in section 0 unless suggestion 6.4 is in.
- Output files go to `<project>/Recordings/` by default. **SUGGESTION:** add `/Recordings/` to
  `.gitignore` so videos never get committed.

## 4. Settings for the Game view size (cheat sheet)

| What you're recording | Game view | PSX `targetHeight` | Result |
|---|---|---|---|
| Normal landscape (default) | 1920x1080 | 240 | each PS1 pixel = 4.5 screen px |
| Extra-crisp landscape | 2160 tall (3840x2160 build or Recorder) | 240 | exactly 9 px per PS1 pixel, crops cleanly |
| Face close-ups | any | creator already raises it to 480 when zoomed in | sharper face, still pixel-y |
| Portrait (1080x1920) | 1080x1920 | 240 | only 135 px wide, very chunky; UI squeezed |
| Portrait, matched look | 1080x1920 | ~427 | about 240 px wide, matches landscape (needs 6.4 or a Play-mode inspector tweak) |

Changing `targetHeight` in the inspector **during Play mode** reverts when you stop, so it's a safe
way to test, but it's still the main session's call.

## 5. Making the PS1 look survive TikTok compression

TikTok re-encodes everything. Dithering and fine noise are exactly what compression smears, so:

- **Make the pixels bigger.** Zoom in (creator `Z` key) and use center crops. A PS1 pixel that's
  6-10 output pixels wide survives; a 2-3 px one turns to mush.
- **Point / nearest-neighbor scaling at every step:** OBS source scale filter Point, CapCut zoom
  kept moderate, never "smooth" upscales.
- **Move the camera slowly.** Fast pans over dithered grass become blocky smears. Slow spins and
  slow walks compress far better. Quick cuts between still shots are fine.
- **Lift the shadows slightly** in CapCut (Adjust: brightness +5 to +10 or shadows up). Very dark
  purple scenes get banding after upload.
- **No extra filters:** no sharpening, beauty, HDR or "enhance" in CapCut. They fight the dither.
- **Export exactly 1080x1920, 30 fps, high bitrate.** Uploading 4K doesn't help; TikTok
  downsizes it and you lose control of how.
- **Upload with high quality on.** In the TikTok post screen look for "Allow high-quality
  uploads" (under more options / settings in recent app versions), or upload from a computer at
  tiktok.com/upload, which tends to keep more detail than a phone upload over mobile data.
- **Photo carousels** (`carousel_*/post/*.png`) skip video compression entirely, which is one
  reason they're a good low-effort option for this art style.

## 6. Suggestions for the main session (NOT implemented)

Ranked by how much they'd help recording:

1. **Photo mode / hide-UI hotkey** (e.g. `F1`): toggles every UI canvas (creator panels, login and
   players panels, World HUD, the bottom control-hint line) and hides the cursor. Needed for
   clean b-roll like the overgrown-computer pan. The creator's number keys and WASD are already
   used, so an F-key avoids conflicts.
2. **PSX filter toggle hotkey** (e.g. `F2`): flips `PSXCamera.effectEnabled` so "filter off vs on"
   (Script 5) is one take.
3. **Cinematic camera mode** (e.g. `F3`): a slow automatic orbit around the character in the
   creator (the turntable already eases rotation, so an auto-spin speed is a small addition), a
   slow drift across the login backdrop, and a gentle dolly along the cobble path in Mushroom
   Hollow. Smooth, repeatable moves are what make b-roll look professional.
4. **Portrait-aware PSX resolution:** have `PSXCamera.LowRes` base the 240 on the *shorter* side
   (or expose a "recording" preset), so a 1080x1920 capture keeps the same pixel size as the
   landscape game.
5. **Randomize button / key** in the creator: great for "randomizer challenge" content and quick
   variety in hair/outfit montages.
6. **Unity Recorder package** + `/Recordings/` in `.gitignore` (section 3).
7. **Demo account for filming:** a throwaway login so real usernames never appear on screen.

## 7. Quick edit in CapCut (free, desktop or phone)

1. New project, set **Ratio 9:16**.
2. Import clips. For landscape clips: *Background > Color* `#1a1030` (framing B), or use *Crop*
   and scale up to fill (framing A).
3. **Cut on the beat:** add the sound first, then tap *Beat* / auto beat-marking and trim clips to
   the markers. Style changes landing on beats is most of what makes these videos feel good.
4. **Text:** lowercase, short, matching the game's terminal UI (`> make you_`). Use a pixel or
   monospace font from CapCut's font list; if your CapCut version lets you add fonts, the game's
   own `Assets/Fonts/VT323-Regular.ttf` and `PixelifySans.ttf` are both OFL-licensed and fine to
   use. Cream `#FFF5EB` text, pink `#FAA8C7` accent, keep it out of the top ~170 px, bottom
   ~420 px and right ~140 px.
5. **Auto captions** if you add a voiceover (*Text > Auto captions*), then fix any typos.
6. **Speed:** use 2-4x for "make me" build-ups, then drop to 1x for the reveal.
7. **Export:** 1080p, 30 fps, bitrate "Higher" (or Recommended), no HDR. Export from CapCut
   rather than downloading from TikTok, so reposts to Reels and Shorts don't carry a watermark.

## 8. A 45-minute weekly batch session

1. (5 min) Open `Scripts.md`, pick next week's 2-3 scripts, list the shots.
2. (5 min) OBS scenes ready, Game view 1920x1080, Gizmos off, Play Maximized.
3. (25 min) Record every shot twice, each 3-5 s longer than needed. Record a few extra
   "evergreen" clips each time (slow character spin, walking to the plaza, eye cycle) for future
   "day X" posts.
4. (10 min) Drop clips into a dated folder (outside the repo, e.g. `Videos/dreamWorld/2026-10-12/`)
   and rename them by script number.

Edit later on your phone in CapCut between classes.
