# Hair references (round 25: curves)

What makes each shape read at PS1 resolution. These are silhouette notes, not copies of any specific item.
Sources looked at: real 2025 boy-hair trend roundups (relaxed "chickencut" with soft fringe, undercut with
rolled layers, longer faux hawk), anime male hairstyle guides (Miyamura's wolf cut with a fringe focus,
curtain bangs on calm characters, Dazai's shaggy mod, Kirito's short sides / longer top), shaggy mullet
guides (short layered front, long back, tousled), plus the Roblox avatar-shop look: chunky, fluffy, messy
hair with lots of pointed pieces and clear volume at the crown.

| Style | Read it by | Build notes |
|---|---|---|
| Crew (redone) | short chunky spikes that hug the skull, pointed fringe | 18 crown locks with tiny flare, uneven tips, tight tapered nape; Long = "anime boy" hair |
| Undercut (redone) | the K-pop two-block: full top over short buzzed sides, side-swept fringe | hair cap at 1.045 is the buzz; 17 top locks drape over it; fringe swept from a -x part |
| Shaggy | fluffy, messy, flicky everywhere ("messy boy hair") | three hang layers with flare 0.035-0.05 and jitter, choppy fringe to the brows |
| Mullet | spiky short top + cropped sides, long flicky back | crown spikes, side_cut on the cap, two back layers (35-145 deg) |
| Curtains | middle part, curtains framing the eyes, fluffy over the ears | curtain_fringe tips above the eyes (z >= 1.44 at the edges) |
| EmoSwoop | long side swoop across one eye, spiky flicked-out crown | 8 swoop locks from a -x part, crown tips 0.22 with big flare |
| ManBun | combed straight back, bun on the crown; Long = half-up | ridged back-combed strands, icosphere bun + tie |
| LongLayered | men's or women's long hair, middle part, layered | curtain fringe to the jaw, top layer + long layer |
| Butterfly | big blowout: short "wing" layer flipping out, long curled ends | wing hang with flare 0.08 at scale 1.14 |
| Hime | blunt bangs, blunt cheek-length sidelocks, straight back | Straight bangs only, tip 1.0 everywhere |
| Curly | a cloud of round curls on top and over the forehead | 60-105 subdivided icospheres on the shell |

## Round 25: curves instead of boxes

Feedback: "a good start, but fine tune it, look at references and add more curves, it's all very boxy."

What the references say (Roblox avatar-shop hair such as "Fluffy Messy Wolfcut" and "Fluffy Messy Boy Hair",
low-poly / PS1 anime hair breakdowns on Blender Artists and Polycount, and butterfly-cut vs long-layers guides):

- Low-poly anime hair is a handful of big chunks, each a curved strip with 4-8 loops along its length, bent so
  the silhouette reads. The curve is what sells it, not the polycount. Keep faces flat-shaded for the PS1 look.
- A chunk is not a flat ribbon: it has a domed top (a ridge down the middle) so each one catches light as its own
  strand. Tips taper to a point; a chunk is widest about a third of the way down, not at the root.
- Roblox fluffy / messy hair: round crown volume first, then pieces that bow out and flick at the ends in
  slightly different directions. Nothing hangs in a straight tube.
- Butterfly cut vs long layers: the butterfly has two zones, short face-framing layers at the cheek/chin that
  fan away from the face, plus long cascading layers; it is bouncy and blown out. Long layers are even and sleek,
  framing pieces turn in toward the face. So: Butterfly = wide, flipped, curled; LongLayered = narrow, straight.
- Man bun: from the front the bun has to clear the top of the head (high and big), and the sides read as
  combed-back lines running up toward it, with a nape hairline that curves up behind the ears.
- Emo swoop: one heavy diagonal sweep from a deep side part that covers one eye completely.

How it's built now (build_dreamer.py):

| Helper | Change |
|---|---|
| lock() | 5-vertex rounded cross-section (flat underside, domed ridge), thickness tapers to the tip, width bellies out then eases to the tip, up to 12 segments on long locks. New curve options: `arc` (bow out then in, on by default scaled to length), `bend` (sideways sway), `flick` (tip flips out and up), `curl` (tip rolls under), `twist` (tip turns toward the head), `belly`. The lock's width now follows its path (perpendicular to it), so swept bangs aren't sheared parallelograms. |
| hang() / crown() | pass the curve options through; `sway` = random bend per lock, `fan` = tips gather/spread around the back |
| fringe_lock / fringe_row / curtain_fringe / side_locks | pass curve options; `bend` > 0 bows a lock outward (mirrored per side) |
| hair_cap() | the shell tucks in toward the neck below the ears (no straight helmet wall); optional `nape_rise` curves the nape hairline up toward the ears (ManBun) |

| Style | Now reads by |
|---|---|
| Crew | domed spiky crown, tips flick a little, pointed fringe curving to one side |
| Undercut | full top domes up and rolls over the buzz line, fringe curving across from a -x part |
| Shaggy | rounded crown, every layer bows out and flicks, fringe pieces leaning different ways |
| Mullet | domed spiky top, long back that sways, fans forward a little and flicks out |
| Curtains | curtains arc out from the part, fluffy flicked sides |
| EmoSwoop | 9 swoop locks from a deep -x part; tips reach 1.405 (Medium) / 1.37 (Long) so the +x eye is fully covered, the other eye stays clear; Short stays above the brow |
| ManBun | bun bigger and higher (clears the head from the front), forehead locks flush (no visor), sides swept back and up as separate ridged locks, curved nape |
| LongLayered | sleek: low flare, blunt-ish tips that turn in, long face-framing curtains past the jaw |
| Butterfly | big round crown, flipped-out "wing" layer at the cheeks, shoulder layer flipping out, long ends bowing out and curling under |
| Hime | still blunt, but sidelocks and ends turn in softly |
| WolfCut (older) | layers now flick at the ends; other older styles only pick up the rounder lock shape |

Sources: [Rolimons: Black Messy Girl Hair](https://www.rolimons.com/item/77765970525405),
[Roblox hair code catalog](https://robloxden.com/item-codes/categories/accessories/hair/125),
[BlenderNation: how to model low poly hair](https://www.blendernation.com/2014/08/17/how-to-model-low-poly-hair/),
[Blender Artists: low-poly anime hair](https://blenderartists.org/t/anime-help/422678),
[Butterfly haircut vs long layers](https://www.therapyhairstudio.com/butterfly-haircut-vs-long-layers-which-cut-gives-you-more-movement/),
[Davines: butterfly haircut](https://us.davines.com/blogs/news/butterfly-haircut).
