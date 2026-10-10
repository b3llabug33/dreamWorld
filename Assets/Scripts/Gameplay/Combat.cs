using UnityEngine;

namespace DreamWorld.Gameplay
{
    // Anything that can be hit: the player, monsters, later other players and breakable things.
    public interface IDamageable
    {
        // Returns true if the hit landed (false: dodged, dead, or in a safe zone).
        bool TakeDamage(float amount, GameObject source);
        Transform Body { get; }
        bool Alive { get; }
    }

    // Shared hit check for swings and lunges: everything damageable in a sphere in front of the attacker,
    // each object hit at most once per swing (the caller keeps the `already` set).
    public static class Hits
    {
        static readonly Collider[] buffer = new Collider[32];

        public static int Sweep(Vector3 center, float radius, GameObject attacker, float damage,
                                System.Collections.Generic.HashSet<IDamageable> already)
        {
            int hits = 0;
            int n = Physics.OverlapSphereNonAlloc(center, radius, buffer, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var target = buffer[i].GetComponentInParent<IDamageable>();
                if (target == null || !target.Alive || already.Contains(target)) continue;
                if (target.Body.gameObject == attacker) continue;
                already.Add(target);
                if (target.TakeDamage(damage, attacker)) { hits++; DreamWorld.Audio.AudioManager.Play3D("hit", target.Body.position + Vector3.up * 0.6f); }
            }
            return hits;
        }
    }
}
