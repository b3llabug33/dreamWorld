using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.CloudSave;
using Unity.Services.CloudSave.Models.Data.Player;
using Unity.Services.Core;
using UnityEngine;

namespace DreamWorld.Account
{
    // One saved character: which slot it's in, its name, and its look (CharacterSave JSON).
    [Serializable]
    public class CharacterRecord
    {
        public int slot;
        public string name;
        public string look;
    }

    public enum AccountMode { None, Online, Offline }

    // Accounts and saved characters.
    //  - Online: Unity Gaming Services username/password accounts; characters live in Cloud Save,
    //    so they follow you to any computer.
    //  - Offline: no account; characters are saved on this computer only (PlayerPrefs).
    // Both modes expose the same calls, so the screens don't care which one is active.
    public static class AccountService
    {
        public const int MaxCharacters = 4;
        const string OfflinePrefix = "dreamWorld.local.char_";
        const string OnlinePrefix = "char_";

        public static AccountMode Mode { get; private set; }
        public static string Username { get; private set; } = "";
        public static bool LoggedIn => Mode != AccountMode.None;

        // The character being played/edited right now (-1 = a new one that hasn't been saved yet).
        public static int CurrentSlot = -1;
        public static string CurrentName = "";

        static bool initialized;

        static async Task<string> Init()
        {
            if (initialized) return null;
            try
            {
                await UnityServices.InitializeAsync();
                initialized = true;
                return null;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[dreamWorld] Unity Services init failed: " + e.Message);
                return "online accounts aren't connected yet, so play offline for now";
            }
        }

        // Signs back in automatically if this computer is still logged in from last time.
        public static async Task<bool> TryResume()
        {
            if (LoggedIn) return true;
            if (await Init() != null) return false;
            if (!AuthenticationService.Instance.SessionTokenExists) return false;
            try
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();   // reuses the cached session
                Mode = AccountMode.Online;
                Username = PlayerPrefs.GetString("dreamWorld.lastUser", "");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[dreamWorld] resume failed: " + e.Message);
                return false;
            }
        }

        // Returns null on success, or a short message to show the player.
        // Usernames: 3-20 characters, letters required, numbers and . - @ _ optional.
        // (Unique per game: the account server refuses a name someone already has.)
        public static string CheckUsername(string username)
        {
            if (username.Length < 3 || username.Length > 20) return "username needs 3-20 characters";
            foreach (char c in username)
                if (!char.IsLetterOrDigit(c) && c != '.' && c != '-' && c != '@' && c != '_')
                    return "username can only use letters, numbers and . - @ _";
            return null;
        }

        public static async Task<string> SignIn(string username, string password, bool createAccount)
        {
            username = username.Trim();
            var nameError = CheckUsername(username);
            if (nameError != null) return nameError;
            var initError = await Init();
            if (initError != null) return initError;
            try
            {
                if (AuthenticationService.Instance.IsSignedIn) AuthenticationService.Instance.SignOut(true);
                if (createAccount)
                    await AuthenticationService.Instance.SignUpWithUsernamePasswordAsync(username, password);
                else
                    await AuthenticationService.Instance.SignInWithUsernamePasswordAsync(username, password);
                Mode = AccountMode.Online;
                Username = username;
                PlayerPrefs.SetString("dreamWorld.lastUser", username);
                PlayerPrefs.Save();
                return null;
            }
            catch (AuthenticationException e) { return Friendly(e.Message, createAccount); }
            catch (RequestFailedException e) { return Friendly(e.Message, createAccount); }
            catch (Exception e) { return "couldn't connect: " + e.Message; }
        }

        static string Friendly(string message, bool creating)
        {
            var m = message.ToLowerInvariant();
            if (m.Contains("provider is not available") || m.Contains("permission_denied"))
                return "online accounts are switched off in the Unity dashboard (turn on Username & Password)";
            if (m.Contains("password") && (m.Contains("requirement") || m.Contains("invalid") && creating))
                return "password needs 8-30 characters with an uppercase, a lowercase, a number and a symbol";
            if (creating && (m.Contains("exist") || m.Contains("taken") || m.Contains("conflict") || m.Contains("already")))
                return "that username is taken, try another";
            if (m.Contains("username") && creating)
                return "username needs 3-20 characters: letters, numbers, . - @ _";
            if (m.Contains("invalid") || m.Contains("not found") || m.Contains("unauthorized"))
                return "wrong username or password";
            if (m.Contains("project") || m.Contains("environment"))
                return "online accounts aren't connected yet, so play offline for now";
            return message;
        }

        public static void PlayOffline()
        {
            Mode = AccountMode.Offline;
            Username = "offline";
        }

        public static void SignOut()
        {
            if (Mode == AccountMode.Online && initialized && AuthenticationService.Instance.IsSignedIn)
                AuthenticationService.Instance.SignOut(true);
            Mode = AccountMode.None;
            Username = "";
            CurrentSlot = -1;
            CurrentName = "";
        }

        // ---------- characters ----------

        public static async Task<List<CharacterRecord>> LoadCharacters()
        {
            var list = new List<CharacterRecord>();
            if (Mode == AccountMode.Online)
            {
                try
                {
                    var items = await CloudSaveService.Instance.Data.Player.LoadAllAsync(new LoadAllOptions());
                    foreach (var pair in items)
                        if (pair.Key.StartsWith(OnlinePrefix)) Add(list, pair.Value.Value.GetAsString());
                }
                catch (Exception e) { Debug.LogWarning("[dreamWorld] cloud load failed: " + e.Message); }
            }
            else
            {
                for (int i = 0; i < MaxCharacters; i++)
                    if (PlayerPrefs.HasKey(OfflinePrefix + i)) Add(list, PlayerPrefs.GetString(OfflinePrefix + i));
            }
            list.Sort((a, b) => a.slot.CompareTo(b.slot));
            return list;
        }

        static void Add(List<CharacterRecord> list, string json)
        {
            try
            {
                var record = JsonUtility.FromJson<CharacterRecord>(json);
                if (record != null && record.slot >= 0 && record.slot < MaxCharacters) list.Add(record);
            }
            catch (ArgumentException) { }
        }

        // Returns null on success, or a message.
        public static async Task<string> SaveCharacter(CharacterRecord record)
        {
            var json = JsonUtility.ToJson(record);
            if (Mode == AccountMode.Online)
            {
                try
                {
                    await CloudSaveService.Instance.Data.Player.SaveAsync(
                        new Dictionary<string, object> { { OnlinePrefix + record.slot, json } }, new Unity.Services.CloudSave.Models.Data.Player.SaveOptions());
                }
                catch (Exception e) { return "couldn't save online: " + e.Message; }
            }
            else
            {
                PlayerPrefs.SetString(OfflinePrefix + record.slot, json);
                PlayerPrefs.Save();
            }
            return null;
        }

        public static async Task DeleteCharacter(int slot)
        {
            if (Mode == AccountMode.Online)
            {
                try { await CloudSaveService.Instance.Data.Player.DeleteAsync(OnlinePrefix + slot, new Unity.Services.CloudSave.Models.Data.Player.DeleteOptions()); }
                catch (Exception e) { Debug.LogWarning("[dreamWorld] cloud delete failed: " + e.Message); }
            }
            else
            {
                PlayerPrefs.DeleteKey(OfflinePrefix + slot);
                PlayerPrefs.Save();
            }
        }
    }
}
