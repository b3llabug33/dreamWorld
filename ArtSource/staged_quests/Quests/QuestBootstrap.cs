using DreamWorld.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DreamWorld.Quests
{
    // Adds the quest pieces to any world scene that has the game HUD: the quest manager, the dialogue box,
    // the quest HUD, and Orion standing in the town square. Done at runtime so the scene builder doesn't
    // need to know about quests.
    public static class QuestBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Hook()
        {
            SceneManager.sceneLoaded -= OnLoaded;
            SceneManager.sceneLoaded += OnLoaded;
            OnLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        static void OnLoaded(Scene scene, LoadSceneMode mode)
        {
            var hud = Object.FindAnyObjectByType<GameHUD>();
            if (hud == null || Object.FindAnyObjectByType<QuestManager>() != null) return;
            var root = new GameObject("Quests");
            root.AddComponent<QuestManager>();
            var dialogue = root.AddComponent<DialogueUI>();
            dialogue.titleFont = hud.titleFont;
            dialogue.bodyFont = hud.bodyFont;
            var questHud = root.AddComponent<QuestHUD>();
            questHud.titleFont = hud.titleFont;
            questHud.bodyFont = hud.bodyFont;
            SpawnOrion();
        }

        static void SpawnOrion()
        {
            var spot = GameObject.Find("OrionSpot");
            Vector3 pos = spot != null ? spot.transform.position : new Vector3(3.5f, 0.02f, 1.0f);
            var orion = new GameObject("Orion");
            orion.transform.position = pos;
            orion.transform.rotation = Quaternion.Euler(0, 180, 0);
            var prefab = Resources.Load<GameObject>("Critters/Critter_RainbowStagBeetle");
            Transform model;
            if (prefab != null)
            {
                model = Object.Instantiate(prefab, orion.transform).transform;
                model.localPosition = Vector3.zero;
                model.localScale = Vector3.one * 1.1f;      // about knee-high: a big friendly beetle
            }
            else
            {
                model = GameObject.CreatePrimitive(PrimitiveType.Sphere).transform;   // fallback stand-in
                Object.Destroy(model.GetComponent<Collider>());
                model.SetParent(orion.transform, false);
                model.localScale = new Vector3(0.5f, 0.3f, 0.7f);
            }
            var col = orion.AddComponent<CapsuleCollider>();
            col.radius = 0.4f;
            col.height = 0.6f;
            col.center = new Vector3(0, 0.3f, 0);
            col.direction = 2;
            var npc = orion.AddComponent<OrionNpc>();
            npc.model = model;
            npc.range = 2.6f;
        }
    }
}
