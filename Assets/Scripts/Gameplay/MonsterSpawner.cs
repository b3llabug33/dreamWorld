using System.Collections;
using System.Collections.Generic;
using DreamWorld.Net;
using FishNet;
using UnityEngine;

namespace DreamWorld.Gameplay
{
    // Keeps one monster at each of its child spawn points, bringing them back a while after they die.
    // Online, only the server spawns them (as networked objects everyone shares, see Net/NetMonster.cs);
    // a client's copies arrive from the server, so a client's spawners do nothing.
    public class MonsterSpawner : MonoBehaviour
    {
        public Monster prefab;
        public float respawnSeconds = 25f;
        readonly Dictionary<Monster, Transform> spots = new Dictionary<Monster, Transform>();
        bool online;

        IEnumerator Start()
        {
            if (NetSession.Resolve(out _) != NetMode.Solo)
            {
                // wait until this game knows whether it's the server (host/dedicated) or a client
                while (!InstanceFinder.IsServerStarted && !InstanceFinder.IsClientStarted) yield return null;
                if (!InstanceFinder.IsServerStarted) { enabled = false; yield break; }
                online = true;
            }
            foreach (Transform spot in transform) Spawn(spot);
        }

        void Spawn(Transform spot)
        {
            var m = Instantiate(prefab, spot.position, spot.rotation, online ? null : transform.parent);
            m.Spawner = this;
            spots[m] = spot;
            if (online) InstanceFinder.ServerManager.Spawn(m.gameObject);
        }

        public void Despawned(Monster m)
        {
            if (online)
            {
                // a fresh one comes back later; this one goes away for everyone
                var spot = spots[m];
                spots.Remove(m);
                InstanceFinder.ServerManager.Despawn(m.gameObject);
                StartCoroutine(RespawnAt(spot));
                return;
            }
            m.gameObject.SetActive(false);
            StartCoroutine(Respawn(m));
        }

        IEnumerator Respawn(Monster m)
        {
            yield return new WaitForSeconds(respawnSeconds);
            var spot = spots[m];
            m.transform.SetPositionAndRotation(spot.position, spot.rotation);
            m.gameObject.SetActive(true);
        }

        IEnumerator RespawnAt(Transform spot)
        {
            yield return new WaitForSeconds(respawnSeconds);
            if (InstanceFinder.IsServerStarted) Spawn(spot);
        }
    }
}
