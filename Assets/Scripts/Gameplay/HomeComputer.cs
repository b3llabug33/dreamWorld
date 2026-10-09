using UnityEngine;

namespace DreamWorld.Gameplay
{
    // Your home computer: deposit coins so you don't lose them when you die, or take some out.
    // It also marks where you wake up after dying. (Each player gets their own home later.)
    public class HomeComputer : Interactable
    {
        public static HomeComputer Mine { get; private set; }
        public Transform spawnPoint;

        protected override void OnEnable()
        {
            base.OnEnable();
            Mine = this;
        }

        public override string Prompt(PlayerStats player) => "use home computer (bank)";
        public override void Interact(PlayerStats player) => GameHUD.Instance?.OpenBank();

        public Vector3 SpawnPosition => spawnPoint != null ? spawnPoint.position : transform.position + transform.forward * 1.5f;
        public Quaternion SpawnRotation => spawnPoint != null ? spawnPoint.rotation : transform.rotation;
    }
}
