using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DreamWorld.Account;
using Unity.Services.CloudSave;
using UnityEngine;

namespace DreamWorld.Quests
{
    // Saves quest progress per character next to the rest of their progress:
    // Cloud Save "quests_<slot>" when logged in online, PlayerPrefs otherwise ("dev" without a slot).
    public static class QuestStore
    {
        const string CloudPrefix = "quests_";
        const string LocalPrefix = "dreamWorld.local.quests_";

        static string LocalKey => LocalPrefix + (AccountService.CurrentSlot >= 0 ? AccountService.CurrentSlot.ToString() : "dev");

        public static async Task<string> Load()
        {
            if (AccountService.Mode == AccountMode.Online && AccountService.CurrentSlot >= 0)
            {
                try
                {
                    var key = CloudPrefix + AccountService.CurrentSlot;
                    var items = await CloudSaveService.Instance.Data.Player.LoadAsync(new HashSet<string> { key },
                        new Unity.Services.CloudSave.Models.Data.Player.LoadOptions());
                    return items.TryGetValue(key, out var item) ? item.Value.GetAsString() : null;
                }
                catch (Exception e) { Debug.LogWarning("[dreamWorld] quest load failed: " + e.Message); return null; }
            }
            return PlayerPrefs.HasKey(LocalKey) ? PlayerPrefs.GetString(LocalKey) : null;
        }

        public static async Task Save(string json)
        {
            if (AccountService.Mode == AccountMode.Online && AccountService.CurrentSlot >= 0)
            {
                try
                {
                    await CloudSaveService.Instance.Data.Player.SaveAsync(
                        new Dictionary<string, object> { { CloudPrefix + AccountService.CurrentSlot, json } },
                        new Unity.Services.CloudSave.Models.Data.Player.SaveOptions());
                }
                catch (Exception e) { Debug.LogWarning("[dreamWorld] quest save failed: " + e.Message); }
                return;
            }
            PlayerPrefs.SetString(LocalKey, json);
            PlayerPrefs.Save();
        }
    }
}
