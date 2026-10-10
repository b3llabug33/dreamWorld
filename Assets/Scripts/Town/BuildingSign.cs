using DreamWorld.Gameplay;

namespace DreamWorld.Town
{
    // A sign by a door: names the place, and what it will become.
    public class BuildingSign : Interactable
    {
        public string title = "empty shop";
        public string message = "coming soon!";

        public override string Prompt(PlayerStats player) => $"read sign: {title}";
        public override void Interact(PlayerStats player) => GameHUD.Instance?.Say(title, message);
    }
}
