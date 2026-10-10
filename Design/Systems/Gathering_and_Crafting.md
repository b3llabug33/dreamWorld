# Survival: gathering and crafting (plan)

Four core materials, gathered out in the world, used to craft house decor (and later tools/gear).

| Material | Where it comes from | Tool |
|---|---|---|
| **wood** | chop trees (pines, round trees, birch, glow trees), fallen logs, stumps | axe (or any weapon, slower) |
| **rock** | break boulders and rock piles | pickaxe (or weapon, slower) |
| **ore** | glowing ore veins in rocks (copper, silver, dream crystal) | pickaxe |
| **thread** | cotton puff bushes, sheep-moth cocoons, old spiderwebs | bare hands / bug net |

Extras: berries and mushrooms from bushes (food), sap from trees, moss from mossy rocks.

## How gathering works
- Walk up to a resource node, E (or swing at it) to gather. Each hit gives 1-2 of the material and
  shakes the node; after a few hits the node is used up (tree becomes a stump, rock shrinks to rubble)
  and regrows after a while (minutes for bushes, longer for trees and ore).
- Gathering uses a little stamina, so there's a rhythm to it.
- Better tools gather faster and can reach better ore (copper -> silver -> dream crystal).
- Nodes are spread through the woods; rarer ones are in dangerous areas (survival risk vs reward).

## Survival layer
- Food bar already drains; berries/mushrooms you gather are snacks. Later: cook raw food at a campfire.
- Night is darker and more dangerous (night bats, glitch moths); a campfire/lantern keeps you safer.
- Storage at your home computer for materials; the bag is limited (28 slots) so trips home matter.

## Crafting (decor first)
- A crafting bench at your base (and a public one in town).
- Recipes use the 4 materials, e.g.
  - mushroom stool: 3 wood + 2 thread
  - stone fountain: 12 rock + 2 ore
  - plaid armchair: 6 wood + 6 thread
  - crystal lamp: 2 ore + 1 dream shard + 2 wood
  - bookshelf: 8 wood
- Crafted decor goes into the bag and can be placed at your base (base building comes next).
- Most of the 102 props in Design/Props can become recipes.

## Wood types by biome (requested 2026-10-09)

Each biome's trees drop their own wood, and the wood you build with sets the look (color, grain, trim) of the furniture, decor and wooden weapons you craft. Same recipe, different wood = different finish, so players collect woods for style and can trade them.

| Wood | Where | Look | Example crafts |
|---|---|---|---|
| Pine | town paths, starter woods | pale honey, knotty | basic chairs, crates, starter club |
| Oak | town square, whimsical woods | warm mid brown, straight grain | tables, bookshelves, sturdy staff |
| Birch | whimsical woods | creamy white bark trim | cottagecore stools, picture frames, bow |
| Mistpine fir | Mistpine Hollow | dark gray-brown, damp | moody glass-house furniture, Mullen-style shelves |
| Red cedar | Mistpine Hollow | reddish, fluted | chests, wardrobes, cedar bat (baseball clearing nod) |
| Mossy maple | Mistpine Hollow | brown with green moss inlay | planters, moss lamps, mossy staff |
| Glow wood | dream glades (later) | violet with faint glow seams | glowing bed frames, night lamps, wand |
| Driftwood | Quillet Riverbank | silver-gray, smooth | rustic benches, wind chimes, spear |

Implementation notes:
- Items: `wood_<type>` (stackable). Tree nodes (`Res_Tree_*`) carry a `woodType` field set by the zone/biome they spawn in.
- Recipes ask for "any wood" or a specific one. The crafted item stores `material = wood type`; one model per item with the wood applied as a palette/texture swap (vertex-color tint in PSX Lit) so we don't need a model per wood.
- Wooden weapons (club, staff, bow, bat, spear, wand) use the same swap, and rarer woods can add a small stat perk (glow wood +magic, cedar +durability).
- Shop/trade value scales with rarity: pine < oak/birch < Mistpine woods < driftwood < glow wood.

## Models (made): ArtSource/build_resources.py
Choppable trees (with stumps), rock nodes (with rubble), ore veins, thread sources (cotton bush, cocoon
cluster, web), berry bushes, plus their "used up" states.
