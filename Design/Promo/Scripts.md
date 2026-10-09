# dreamWorld TikTok scripts

14 ready-to-film scripts. Each one lists the hook, the exact shots, the text/voiceover, a sound
vibe, length, caption, hashtags, call to action, and a **slideshow version** you can post today
from screenshots already in `Design/` (TikTok photo mode, swipe-through images with music).

**Controls cheat sheet for filming (current build):**

| Where | Keys |
|---|---|
| Creator ("make you") | `W/S` or arrows: category, `A/D`: style, `Q/E`: color swatch, `1-9, 0`: jump to category, `Z/X` or scroll: zoom, click-drag: spin, `Enter`: ready |
| Mushroom Hollow | `WASD`: walk, `Shift`: run, `Space`: jump, `Q/E`: turn camera, `Esc`: back to creator |

**Sound note:** describe-the-vibe only. Pick from TikTok's in-app library (commercial-safe list
if the account is ever a Business account). Trending sounds change weekly, so search the vibe.

**Honesty rule:** anything marked *(planned)* must be said as planned on screen.

---

## 1. "I made a game where every character is built with code"

- **Length:** 15-20s
- **Hook (0-2s), on-screen text:** `i made a game where the characters are built with code`
- **Shots:**
  1. (0-2s) Creator, zoomed all the way in on the face (`Z`), category `[2] eyes`, tap `D` 3-4
     times fast so the eyes change on beat.
  2. (2-6s) Screen recording of Blender running `ArtSource/build_dreamer.py` headless, or just
     the script scrolling in VS Code (the docstring at the top reads well on camera).
  3. (6-12s) Creator, zoomed out, `[7] hair`, hold `D` taps through 6-8 hairstyles.
  4. (12-16s) Spin the character with click-drag, end on a front-facing pose.
- **On-screen text sequence:** `python scripts build the body` -> `the clothes` -> `87 hair variations`
  -> `then unity dresses you up`
- **Voiceover (optional):** "Every piece of this character, from the body to the hair, comes out of
  a Python script I can re-run any time I want to change something."
- **Sound vibe:** upbeat lo-fi with a clear beat to cut on
- **Caption:** making a cozy PS1-style game and the characters are built entirely by Python scripts in Blender. ask me anything about how it works
- **Hashtags:** #gamedev #indiedev #ps1aesthetic #blender #python
- **CTA:** `follow for the devlog` (text on the last frame)
- **Slideshow version (today):**
  `Design/Hair/hair_bangs_lengths_check.png`, `Design/Faces/face_drafts_round1.png`,
  `Design/Outfits/look_grunge.png`, `Design/Outfits/look_girly.png`,
  `Design/Screens/creator_extras_tab.png`

## 2. "nature is taking back the computer" (backdrop pan)

- **Length:** 10-15s
- **Hook:** `this is the login screen of my game` (with the slow pan already moving)
- **Shots:**
  1. (0-10s) Login screen, slow continuous camera move past the GPU fans and CRT monitors. *Needs
     the photo-mode / hide-UI hotkey suggested in Recording_Guide.md; until then use the login
     screen as-is with the UI.*
  2. (10-13s) Cut to the logo + login box.
- **On-screen text:** `a mushroom forest growing over a giant old computer` -> `ram sticks, gpu fans, crt monitors` -> `still booting`
- **Sound vibe:** dreamy ambient synth pad, slightly eerie
- **Caption:** the login screen of my game is a glowing mushroom forest slowly overgrowing an old PC. nature wins eventually
- **Hashtags:** #ps1aesthetic #cozygames #indiegame #lowpoly #gamedev
- **CTA:** `would you log in?`
- **Slideshow version:** `Design/World/backdrop_tech_first_try.png`,
  `Design/Screens/backdrop_overgrown_login.png`, `Design/Screens/backdrop_overgrown_select.png`

## 3. "12 eye styles" (eye cycle)

- **Length:** 8-12s
- **Hook:** `pick your eyes` with the face already zoomed in
- **Shots:**
  1. Creator, `[2] eyes`, zoom fully in (`Z`). Tap `D` through all 12 styles, one per beat.
  2. Last 3s: drag the size slider big, then small, then hit reset.
- **On-screen text:** style name in the corner is already shown by the UI. Add `12 eye styles` and
  `+ size, spacing, height sliders`.
- **Sound vibe:** cute bouncy chiptune, strong beat
- **Caption:** 12 eye styles so far and every one is drawn by code. which number is yours
- **Hashtags:** #charactercreator #ps1aesthetic #indiegame #cozygames
- **CTA:** `comment your number`
- **Slideshow version:** `Design/Faces/face_drafts_round1.png`,
  `Design/Faces/face_drafts_round2_angular.png`,
  `Design/Faces/eye_sliders_default_big_smallwide_closehigh.png`

## 4. "hairstyle of the day" (series)

- **Length:** 7-10s
- **Hook:** `hairstyle of the day: wolf cut` (change name each episode)
- **Shots:**
  1. Creator, `[7] hair`, medium zoom (head and shoulders). Pick one style, cycle its bangs row
     (straight, choppy, middle, left, right) and then its length row.
  2. Click-drag to spin once so viewers see the back.
  3. End on a color change with `Q/E` or the hue slider.
- **On-screen text:** `#1 wolf cut` / `bangs + length options` / `full color picker`
- **Sound vibe:** a short catchy loop (the same one every episode helps recognition)
- **Caption:** hairstyle of the day #1. tell me which one to show tomorrow
- **Hashtags:** #hairstyleoftheday #charactercreator #ps1aesthetic #indiedev
- **CTA:** `tomorrow's style is your pick`
- **Slideshow version:** `Design/Hair/hairstyles_in_game.png`, `Design/Hair/pixie_lengths.png`,
  `Design/Hair/bangs_ingame_middle_left_choppy.png`

## 5. "PS1 filter: off vs on"

- **Length:** 8-12s
- **Hook:** `what my game looks like without the PS1 filter` (filter OFF first, then snap ON)
- **Shots:**
  1. Mushroom Hollow, walking into the plaza. 3s with the PSX effect off, then switch on.
  2. Same in the creator on a face close-up.
- **How to film:** the `PSXCamera` component has an effect toggle. *Suggestion for the main session:
  bind it to a key so the switch happens in one take (see Recording_Guide.md).* Until then, record
  two takes from the same spot and cut them together.
- **On-screen text:** `filter off` / `filter on: 240p, dithering, wobbly vertices`
- **Sound vibe:** a "before/after" transition sound, then something warm and retro
- **Caption:** the PS1 look is a render trick: a tiny 240p image, dithered colors and snapped vertices. off vs on
- **Hashtags:** #ps1aesthetic #psx #gamedev #unity3d #retrogaming
- **CTA:** `filter on or off?`
- **Slideshow version:** `Design/World/mushroom_hollow_player_view.png`,
  `Design/World/mushroom_hollow_plaza.png` (filter on only; the off frame needs a new capture)

## 6. "make me in your game" (comment requests)

- **Length:** 15-25s
- **Hook:** reply-with-video sticker of the comment + `making you in my game`
- **Shots:**
  1. Creator, start from the default character, sped up 2-4x in CapCut while you go category by
     category (skin, eyes, hair, top, bottom, shoes, extras).
  2. Final 4s at normal speed: full-body spin, then press `Enter` (ready).
- **On-screen text:** read the commenter's description in small steps: `brown wolf cut` ->
  `freckles + nose ring` -> `flannel + baggy jeans`
- **Sound vibe:** fun upbeat "transformation" track
- **Caption:** made you in my game. who's next
- **Hashtags:** #charactercreator #makeme #indiegame #cozygames
- **CTA:** `describe yourself in the comments`
- **Note:** only accept requests the creator can do today. If they ask for something missing,
  say `not in the game yet, adding it to the list`.
- **Slideshow version:** `Design/Screens/creator_name_and_tie.png`,
  `Design/Outfits/tank_stripes_shortshorts.png`, `Design/Screens/your_dreamers.png`

## 7. "making a grunge fit in my own game"

- **Length:** 12-18s
- **Hook:** `grunge girl starter pack but it's my PS1 game`
- **Shots:**
  1. Creator, full-body: wolf cut, flannel, black tee, baggy jeans (low rise), combat boots,
     choker, beanie, one piece per beat.
  2. Zoom in for the extras (piercings, tear tattoo).
  3. Walk her into Mushroom Hollow at night (optional).
- **On-screen text:** name each piece as it appears
- **Sound vibe:** 90s-style grunge guitar instrumental or a moody alt beat
- **Caption:** flannel, baggy jeans and combat boots in a cozy mushroom village. would you wear it
- **Hashtags:** #grungeaesthetic #altfashion #ps1aesthetic #indiegame #charactercreator
- **CTA:** `next fit: girly or y2k?`
- **Slideshow version:** `Design/Outfits/look_grunge.png`, `Design/Outfits/look_twilight.png`,
  `Design/Outfits/flannel_long_oversized_sleeves.png`, `Design/Outfits/pants_low_vs_high_rise.png`

## 8. "15 face extras, pick 3"

- **Length:** 10-15s
- **Hook:** `freckles, piercings, sparkles` on a face close-up
- **Shots:**
  1. Creator, `[6] extras`, zoomed in. Cycle through blush, freckles, mole, sparkles, tear tattoo,
     bandaid, nose ring, septum, lip ring, brow piercing.
  2. Turn on slots 1, 2 and 3 to show three at once.
- **On-screen text:** `15 extras` / `wear 3 at once`
- **Sound vibe:** sparkly, cute pop instrumental
- **Caption:** you can stack up to 3 face extras. my combo is freckles, blush and a nose ring
- **Hashtags:** #charactercreator #ps1aesthetic #cozygames #indiedev
- **CTA:** `drop your combo`
- **Slideshow version:** `Design/Faces/brows_and_extras_sheet.png`,
  `Design/Screens/creator_extras_tab.png`, `Design/Screens/creator_brows_tab.png`

## 9. "how I check for clipping" (gamedev)

- **Length:** 15-20s
- **Hook:** `why is my character red?`
- **Shots:**
  1. Slideshow-style zoom on the red-skin audit sheets (body rendered bright red so any skin poking
     through clothes jumps out).
  2. Before/after of one fix (e.g. hoodie no longer flares at the waist).
  3. Same outfit in game, finished.
- **On-screen text:** `red skin = clipping detector` -> `every piece, front/side/back` -> `fixed`
- **Voiceover (optional):** "I render every clothing piece on a bright red body, so if any red
  shows through, I know something's clipping."
- **Sound vibe:** curious/quirky "explainer" beat
- **Caption:** a tiny gamedev trick: render the body bright red and clipping becomes impossible to miss
- **Hashtags:** #gamedev #indiedev #blender #3dmodeling #devlog
- **CTA:** `follow for more dev tricks`
- **Slideshow version:** `Design/Audit/clothing_audit_Top.png`,
  `Design/Audit/clothing_audit_Bottom.png`, `Design/Audit/clothing_audit_legwear_shoes_head_neck.png`,
  `Design/Hair/all_hairstyles_front.png`

## 10. "day X of making a cozy PS1 game" (series template)

- **Length:** 10-20s
- **Hook:** `> day 14_` in VT323-style text with a blinking cursor (match the game UI)
- **Shots:** whatever changed that day. Good default trio: before clip (2s), after clip (4s), one
  "in context" shot in the creator or village (4s).
- **On-screen text:** one sentence on what changed, one on what's next
- **Sound vibe:** the same lo-fi loop every episode
- **Caption:** day 14. today: [thing]. tomorrow: [thing]
- **Hashtags:** #devlog #indiedev #gamedev #ps1aesthetic
- **CTA:** `see you tomorrow`
- **Slideshow version:** use the newest 2-3 images in `Design/` plus the matching old one.
  Example for "hair day": `Design/Hair/bangs_parted_choppy_v2.png`, `Design/Hair/bangs_forehead_fix.png`,
  `Design/Hair/bangs_ingame_middle_left_choppy.png`

## 11. "log in, pick your player, walk into town"

- **Length:** 15-20s
- **Hook:** `booting dreamWorld.exe` over the login screen
- **Shots:**
  1. Login screen (use a demo account; don't show a real username or password), click play offline.
  2. Players screen, pick slot 1.
  3. Creator, quick tweak, `Enter`.
  4. Mushroom Hollow: walk up the cobble path toward the plaza lamps.
- **On-screen text:** `log in` -> `pick your player (up to 4)` -> `make you` -> `walk home`
- **Sound vibe:** soft "game boot" synth into a cozy loop
- **Caption:** the full loop so far, from login to Mushroom Hollow. it's small but it's real
- **Hashtags:** #indiegame #cozygames #ps1aesthetic #gamedev #devlog
- **CTA:** `follow to see the world grow`
- **Slideshow version:** `Design/Screens/login.png`, `Design/Screens/your_dreamers.png`,
  `Design/Screens/creator_name_and_tie.png`, `Design/World/mushroom_hollow_player_view.png`

## 12. "what should I add next?"

- **Length:** 7-10s
- **Hook:** `you decide what goes in my game next`
- **Shots:** one pretty shot (face close-up spinning, or walking in the village) with a two-option
  poll on screen. Use TikTok's built-in poll sticker if available.
- **On-screen text:** examples: `cat ears or fairy wings?`, `pet companions or fishing?`,
  `more hair or more outfits?`
- **Sound vibe:** whatever is trending that week (this one rides trends best)
- **Caption:** genuinely asking. the top comment goes on my to-do list
- **Hashtags:** #indiedev #gamedev #cozygames #ps1aesthetic
- **CTA:** `vote in the comments` (then make a "you asked, I added" reply video)
- **Slideshow version:** `Design/World/mushroom_hollow_plaza.png` with the question as photo text

## 13. "first multiplayer test" *(planned; film only once it works)*

- **Length:** 15-20s
- **Hook:** `two players in my PS1 game for the first time`
- **Shots:** two windows side by side (host + client), both characters walking toward each other
  in the plaza. Then one view only, the two characters standing together under the lamps.
- **On-screen text:** `small test with friends` / `goal: anyone online can join someday`
- **Honesty:** say it's a local/friends test. Public servers are planned, not live.
- **Sound vibe:** emotional build-up, "first time" energy
- **Caption:** first multiplayer test. just two of us in Mushroom Hollow but it worked
- **Hashtags:** #gamedev #indiedev #multiplayer #ps1aesthetic #indiegame
- **CTA:** `want to join the next test? follow`
- **Slideshow version:** none yet (needs a two-player capture)

## 14. "6 weeks of devlog" recap

- **Length:** 20-30s
- **Hook:** `6 weeks ago my character looked like this` (old anime-eye render)
- **Shots:** fast montage, one shot per milestone in commit order: anime eyes -> small cartoon
  eyes -> face drafts -> soft almond default -> hair bald-spot check -> 87 hair variations ->
  tech void backdrop -> overgrown computer -> Mushroom Hollow -> whatever is newest.
- **On-screen text:** one or two words per shot (`v1`, `v2`, `the world`, `now`)
- **Sound vibe:** a build-up track that peaks on the final "now" shot
- **Caption:** 6 weeks of making a cozy PS1 game, start to now. thank you for following along
- **Hashtags:** #howitstarted #indiedev #gamedev #ps1aesthetic #devlog
- **CTA:** `follow for the next 6`
- **Slideshow version:** same order as `carousel_1_how_it_started/post/` plus the newest screenshots

---

## Suggested hooks bank (swap into any script)

- `POV: you log into a PS1 game that never existed`
- `this game doesn't exist yet but you can help design it`
- `i'm one student making this, be nice`
- `the PS1 had 2MB of RAM and i'm still making it look like this on purpose`
- `rate my character creator 1-10`
- `my game has 87 hair variations and zero quests (so far)`
