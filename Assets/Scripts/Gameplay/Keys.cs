using UnityEngine;

namespace DreamWorld.Gameplay
{
    // Every gameplay hotkey goes through here, so typing in the chat box (Typing) never also opens the bag,
    // casts a spell or eats a snack.
    public static class Keys
    {
        public static bool Typing { get; set; }

        public static bool Down(KeyCode key) => !Typing && Input.GetKeyDown(key);
    }
}
