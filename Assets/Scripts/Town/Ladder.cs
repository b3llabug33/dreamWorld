using DreamWorld.Gameplay;
using DreamWorld.World;
using UnityEngine;

namespace DreamWorld.Town
{
    // A ladder you climb with E (the stilt watchtower): takes you to the other end.
    public class Ladder : Interactable
    {
        public Transform other;   // where you come out
        public string verb = "climb up";

        public override string Prompt(PlayerStats player) => $"{verb} the ladder";
        public override void Interact(PlayerStats player)
        {
            var pc = player.GetComponent<PlayerController>();
            if (pc == null || other == null || pc.Busy) return;
            pc.Warp(other.position, Quaternion.Euler(0, other.eulerAngles.y, 0));
        }
    }
}
