using System.Collections;
using System.Collections.Generic;
using DreamWorld.Net;
using UnityEngine;

namespace DreamWorld.Gameplay
{
    // Keeps one monster at each of its child spawn points, bringing them back a while after they die.
    // Solo only for now: online monsters need the server to run them (a later milestone).
    public class MonsterSpawner : MonoBehaviour
    {
        public Monster prefab;
        public float respawnSeconds = 25f;
        readonly Dictionary<Monster, Transform> spots = new Dictionary<Monster, Transform>();

        void Start()
        {
            if (NetSession.Mode != NetMode.Solo) { enabled = false; return; }
            foreach (Transform spot in transform) Spawn(spot);
        }

        void Spawn(Transform spot)
        {
            var m = Instantiate(prefab, spot.position, spot.rotation, transform.parent);
            m.Spawner = this;
            spots[m] = spot;
        }

        public void Despawned(Monster m)
        {
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
    }
}
