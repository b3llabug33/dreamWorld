using System.Collections;
using DreamWorld.Account;
using DreamWorld.World;
using UnityEngine;

namespace DreamWorld.Gameplay
{
    // Loads and saves the player's progress, and handles dying: the death screen ("you woke up"),
    // losing the coins you were carrying, and waking up at your home computer (or in town if you don't own a plot yet).
    // Saves every 30 seconds and right after anything important (level up, banking, dying).
    [RequireComponent(typeof(PlayerStats))]
    public class PlayerLife : MonoBehaviour
    {
        public float autosaveSeconds = 30f;
        public float deathScreenSeconds = 3.5f;

        PlayerStats stats;
        PlayerController controller;
        bool loaded;
        float saveTimer;

        async void Start()
        {
            stats = GetComponent<PlayerStats>();
            controller = GetComponent<PlayerController>();
            stats.Died += OnDied;
            stats.LeveledUp += _ => Save();
            var json = await AccountService.LoadProgress();
            if (this == null) return;   // left the scene while loading
            if (!string.IsNullOrEmpty(json))
            {
                try { stats.Load(JsonUtility.FromJson<PlayerProgress>(json)); }
                catch (System.ArgumentException) { Debug.LogWarning("[dreamWorld] couldn't read saved progress, starting fresh"); }
            }
            loaded = true;
        }

        void Update()
        {
            if (!loaded) return;
            saveTimer += Time.deltaTime;
            if (saveTimer >= autosaveSeconds) Save();
        }

        void OnDestroy()
        {
            if (stats != null) stats.Died -= OnDied;
            if (loaded) Save();
        }

        public void Save()
        {
            if (!loaded || stats == null) return;
            saveTimer = 0f;
            _ = AccountService.SaveProgress(JsonUtility.ToJson(stats.progress));
        }

        void OnDied(int coinsLost)
        {
            Save();
            StartCoroutine(WakeUp(coinsLost));
        }

        IEnumerator WakeUp(int coinsLost)
        {
            if (controller != null) controller.InputEnabled = false;
            GameHUD.Instance?.ShowDeath(coinsLost);
            yield return new WaitForSeconds(deathScreenSeconds);
            var home = HomeComputer.Mine;
            var town = DreamWorld.Town.RespawnPoint.Town;
            if (controller != null && home != null && home.isActiveAndEnabled) controller.Warp(home.SpawnPosition, home.SpawnRotation);
            else if (controller != null && town != null) controller.Warp(town.transform.position, town.transform.rotation);   // no plot yet: wake up in town
            stats.Respawn();
            if (controller != null) controller.InputEnabled = true;
            GameHUD.Instance?.HideDeath();
            Save();
        }
    }
}
