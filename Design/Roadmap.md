# dreamWorld roadmap

The game: a cozy PS1-style online world. You make your player, level up from 0, and earn coins doing
bounties, quests, crafting, minigames, PvP and monster hunting. Combat and movement feel like Elden Ring.
The town square and paths are safe; everywhere else (including homes) has monsters and PvP.

## Milestone 1: core systems (done, solo)
- Bars: health, stamina, magic (starts small), food (drains over ~25 min; snacks refill it, R to eat)
- Level 0 start, XP to level, 1 point per level into health / stamina / magic / strength / luck (L menu)
- Coins: carried vs banked. Home computer (E) deposits/withdraws. Dying loses carried coins only
- Death: "YOU WOKE UP" screen, then you wake up at your home computer
- Zones: town square + paths are safe (no damage at all, monsters stop at the edge); wilds + homes are dangerous
- Elden Ring-style controls: space tap roll (i-frames) / hold sprint, F jump, click attack (3-hit combo),
  shift+click heavy, right click guard, Q lock-on, stamina on everything, stagger on big hits
- First monster: shroomling (telegraphed hop attack, drops XP + coins, respawns)
- Snack stand in town (10 coins), progress saved per character (Cloud Save online, local offline)

## Inventory (done, 2026-10-09)
- 28-slot bag (I / Tab), click to move/stack/swap, right click to use; 1-4 quick slots on the HUD
- Items: snack, glow jelly (heal), spore cap + dream shard (shroomling drops, sell them)
- Snack stand is a shop (buy + sell), home computer has 56-slot item storage; all saved per character

## Milestone 2: the town comes alive
- NPCs with dialogue in the town square; a bounty board (kill X, collect Y) paying coins + XP
- First quest chain; quest log UI
- Shops: buy and sell (sell monster drops, buy food/gear)

## Milestone 3: combat depth
- Weapons (and real swing animations), magic spells that use the magic bar
- More monsters + a mini-boss; enemy variety by area
- Proper animations (Mixamo-style clips or more procedural poses)

## Milestone 4: homes
- Each player gets their own base (instanced plot); place / move / rotate decor
- Home computer upgrades (storage, crafting station)

## Milestone 5: crafting + economy
- Make clothing and decor (uses the character creator pieces + patterns); sell to NPCs or players
- Player market in town

## Milestone 6: online for real
- Server-authoritative stats, damage and coins (no cheating): PvP outside safe zones,
  player bounties, networked monsters
- Public hosting so anyone can join (see the multiplayer notes)
- Minigames in town
