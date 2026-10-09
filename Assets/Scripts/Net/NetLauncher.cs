using DreamWorld.World;
using FishNet.Managing;
using FishNet.Transporting;
using UnityEngine;

namespace DreamWorld.Net
{
    // Lives in a world scene. Starts the session the player picked: solo uses the scene's
    // offline player; host/join/server start FishNet and the PlayerSpawner creates one
    // networked character per connected player.
    public class NetLauncher : MonoBehaviour
    {
        public NetworkManager networkManager;
        public GameObject offlinePlayer;
        public WorldHUD hud;

        NetMode mode;
        int lastPlayers = -1;

        void Start()
        {
            mode = NetSession.Resolve(out var address);
            if (mode == NetMode.Solo)
            {
                networkManager.gameObject.SetActive(false);
                return;
            }

            offlinePlayer.SetActive(false);
            var transport = networkManager.TransportManager.Transport;
            transport.SetPort(NetSession.Port);
            if (mode == NetMode.Host || mode == NetMode.Server)
                networkManager.ServerManager.StartConnection();
            if (mode == NetMode.Host)
                networkManager.ClientManager.StartConnection("localhost");
            if (mode == NetMode.Join)
                networkManager.ClientManager.StartConnection(address);
            if (mode == NetMode.Server)
                Debug.Log($"[dreamWorld] dedicated server listening on UDP {NetSession.Port}");
        }

        // Leaving the world (Esc back to the creator) closes the connection cleanly.
        void OnDestroy()
        {
            if (networkManager == null || mode == NetMode.Solo) return;
            if (networkManager.IsClientStarted) networkManager.ClientManager.StopConnection();
            if (networkManager.IsServerStarted) networkManager.ServerManager.StopConnection(true);
        }

        void Update()
        {
            if (hud == null || mode == NetMode.Solo) return;
            string status;
            if (networkManager.IsServerStarted)
            {
                int players = networkManager.ServerManager.Clients.Count;
                status = $"online: {mode.ToString().ToLower()} | players: {players}";
                if (players != lastPlayers) Debug.Log($"[dreamWorld] players online: {players}");
                lastPlayers = players;
            }
            else if (networkManager.IsClientStarted)
                status = "online: connected";
            else
                status = "online: connecting...";
            hud.SetStatus(status);
        }
    }
}
