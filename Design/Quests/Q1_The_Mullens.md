# Questline 1: "Forks, Sparkles and Wolves" (a Twilight parody)

A sparkly vampire family lives deep in the misty woods outside Mushroom Hollow. Parody names keep it
legally safe for a game you might sell later, while still being obvious to anyone who's seen Twilight.

| Twilight | dreamWorld parody | Vibe |
|---|---|---|
| The Cullens | **the Mullens** | glittery, broody, painfully polite |
| Carlisle | **Carlily Mullen** | the dad, a "doctor" who only treats mushrooms |
| Esme | **Esmay Mullen** | the mom, has redecorated the mansion 400 times |
| Edward | **Edwerd Mullen** | stares intensely, says "you smell like... spore caps" |
| Alice | **Alyss Mullen** | sees the future, always wrong about tiny details |
| Jasper | **Jaspurr Mullen** | looks like he's in pain at all times, actually just shy |
| Rosalie | **Rosaleaf Mullen** | glamorous, unimpressed by you |
| Emmett | **Emmitt Mullen** | big, loves wrestling, wants to arm-wrestle the golem |
| Bella | **Stella** | the human girlfriend, clumsy, falls over in every scene |
| the werewolves | **the Wolfpups** of the Quillet Pack | shirtless (fur), extremely warm, always eating |
| Jacob | **Jakeob** | leader of the Wolfpups, "do you even own a shirt" |

Start NPC: **Count Snackula** (already in town by the snack stand) is the gossip who tells you about them.

## Step 1: "Have you heard of the Mullens?"
- Talk to Count Snackula in the town square (the quest marker shows over him).
- Snackula: "Psst. You. You look like someone who appreciates drama. There's a family of vampires out in the
  fog woods. They don't drink blood. They drink *juice boxes*. Disgraceful. Also they sparkle. In the SUN."
- "They're very hard to find. Here, take my old map. I drew it during a moody phase."
- Reward: **Moody Map** (key item). Opens a map screen with a hand-drawn mist forest and 8 faint marks.

## Step 2: "Find the Mullens" (find all 8)
- The mansion is a glass treehouse deep in the misty woods west of town (new area: **Mistpine Hollow**,
  foggier and darker, giant pines, a rain cloud that never leaves).
- Each family member is somewhere around the mansion; talking to them checks them off on the map:
  - Carlily: in the greenhouse wing, giving a mushroom a tiny bandage.
  - Esmay: rearranging the same vase over and over.
  - Edwerd: standing perfectly still in a tree. Says nothing until you talk twice. Then: "...hello."
  - Alyss: on the balcony. "I saw you coming! Wearing red shoes!" (you aren't).
  - Jaspurr: in a corner, hand over mouth. "Sorry. I'm just... nervous. New people."
  - Rosaleaf: by a mirror. "Oh. Another one."
  - Emmitt: lifting a boulder for fun. "Arm wrestle? No? Later then."
  - Stella: tripping over the doormat at the front door, over and over.
- When all 8 are found, they gather in the living room for a family meeting.

## Step 3: "A favor for the family"
- Carlily: "We have a... situation. The Wolfpups by the river have been acting strange. Howling at noon.
  Eating all the berries. We'd go ourselves, but... we're sparkly. They'd see us coming."
- Edwerd: "Go and listen. Don't let them smell you. They will smell you."
- Alyss: "It'll go perfectly! (it won't)."
- Reward: **Fog Cloak** (cosmetic top or accessory) so the pups "can't see you" (flavor).

## Step 4: "Spy on the Wolfpups"
- New spot: **Quillet Riverbank**, east of the woods, campfire and a fallen log.
- Sneak mechanic (simple): crouch-walk (hold C) into hiding bushes near the campfire. Each bush you reach
  plays an overheard conversation. If a pup turns around while you're in the open, you're spotted and
  have to back off and try again (no damage, just "SNIFF SNIFF... nah, probably a squirrel").
- What you learn (3 bushes):
  1. "Something huge is waking up in the old quarry. It ate our snack stash."
  2. Jakeob: "The Mullens think they're SO mysterious. I'm mysterious too. I have a secret (it's that I'm cold)."
  3. "It's the Boulder Brute. Moss Golem's big angry grandpa. If it gets loose it'll flatten the Hollow."

## Step 5: "Distracted: the Boulder Brute"
- On your way back, the ground shakes and the **Boulder Brute** (a giant moss golem boss, 3x size,
  slam shockwaves you have to roll through, throws mossy boulders) blocks the path out of the riverbank.
- Mid-fight, Emmitt shows up cheering from a cliff ("GET HIM! I WANTED TO WRESTLE HIM!") but doesn't help.
- Beating it drops a **Glowing Core** and lots of coins/XP.

## Step 6: "Report back"
- Return to the Mullen mansion and tell Carlily. Each family member reacts:
  - Edwerd: "You fought it. Alone. That's... I should have been there." (stares)
  - Rosaleaf (impressed for once): "Fine. You're not boring."
  - Stella trips while hugging you.
- Rewards: **Mullen Family Crest** (unlocks the mansion as a place you can visit/rest), a sparkle weapon
  (**Glimmer Blade**: the sword sparkles in light), big XP, and a new relationship bar with the family.

## After: relationship quests (repeatable growth)
A "Mullen bond" meter (hearts) goes up with each quest; higher bond unlocks gifts, outfits and the
mansion's rooms for your base decor.
- **Baseball in a Thunderstorm**: a minigame, hit spore-balls while lightning flashes.
- **Prom Night**: help Alyss decorate; pick the outfit with the character creator.
- **Stella's Birthday**: a party where Jaspurr has to stay very calm. Don't let anyone get a paper cut.
- **The Wolfpup Truce**: carry messages back and forth, end with a beach cookout.
- **Edwerd's Piano**: a rhythm minigame on the mansion piano.
- **Rosaleaf's Garage**: fetch parts to fix her (beetle-drawn) cart.

## How it'll be built (tech notes)
- A small quest system: quest steps with objectives (talk to NPC, collect/find, reach area, kill enemy),
  saved in PlayerProgress, a quest log (J key), a marker over the next NPC.
- A dialogue box in the terminal UI style (name, line, "E: next", simple choices).
- Map item opens a full-screen map image with check marks.
- New areas: Mistpine Hollow (fog, mansion) and Quillet Riverbank, plus the Boulder Brute boss
  (scaled-up moss golem with new attacks).
- NPC looks: Mullens = very pale skin, gold or red eyes, dark stylish outfits plus a sparkle shader in
  sunlight; Wolfpups = furry animal NPCs (Quaternius husky/fox models) with name tags.
