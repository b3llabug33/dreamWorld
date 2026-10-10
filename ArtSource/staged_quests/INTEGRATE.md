# Integrating the staged quest system (after the combat/assets fork finishes)

1. Move `staged_quests/Quests/*.cs` to `Assets/Scripts/Quests/` (namespace DreamWorld.Quests).
2. Hooks in existing files:
   - `Town/NpcTalk.Interact`: `var d = QuestManager.Instance?.Conversation(npcName); if (d != null) { DialogueUI.Instance.Show(d); return; }`
     and in Start: `QuestHUD.Npcs[npcName] = transform;`. Also show normal NPC lines through DialogueUI instead of toasts
     (fixes the dialogue/inventory overlap for good).
   - `GameHUD` E handling + Esc/L: skip when `DialogueUI.IsOpen`.
   - `InventoryUI` I/Tab/E toggle: skip when `DialogueUI.IsOpen`.
   - `Monster` death: `QuestManager.Instance?.Killed(displayName)`.
3. Items to add in `Items.cs`: `moody_map` (key item, "Count Snackula's map of the fog woods"), `fog_cloak`
   (cosmetic for now), `glimmer_blade` (weapon, sparkly sword) + icons.
4. Orion model: export `Critter_RainbowStagBeetle` (female, current Critters.blend with seam fix) to
   `Assets/Resources/Critters/Critter_RainbowStagBeetle.fbx`, vertex-color PSX materials like the fork's critters.
   Optionally place an empty "OrionSpot" in the plaza via WorldBuilder.
5. Test (offline throwaway save, slot -1, delete `dreamWorld.local.progress_dev` and `dreamWorld.local.quests_dev`):
   walk into the square -> Orion approaches and greets; back off -> scared line; menu choices; quest
   complete banner; J log; "!" over Count Snackula; Snackula gives the map and starts the Mullens quest.
