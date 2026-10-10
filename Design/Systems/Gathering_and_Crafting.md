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

## Models (made): ArtSource/build_resources.py
Choppable trees (with stumps), rock nodes (with rubble), ore veins, thread sources (cotton bush, cocoon
cluster, web), berry bushes, plus their "used up" states.
