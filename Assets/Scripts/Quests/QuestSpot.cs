using DreamWorld.Gameplay;
using UnityEngine;

namespace DreamWorld.Quests
{
    // A place a quest cares about ("wolf_bush_2"): when the player walks within `radius`, the quest
    // manager hears QuestManager.Reached(id). Fires each time you arrive (quests ignore repeats).
    public class QuestSpot : MonoBehaviour
    {
        public string id = "spot";
        public float radius = 2.5f;
        bool inside;

        void Update()
        {
            var p = PlayerStats.Local;
            var q = QuestManager.Instance;
            if (p == null || q == null) return;
            bool now = (p.transform.position - transform.position).sqrMagnitude < radius * radius;
            if (now && !inside) q.Reached(id);
            inside = now;
        }
    }
}
