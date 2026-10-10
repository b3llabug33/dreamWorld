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
            if (mode != NetMode.Server) ChatUI.Create();
            var transport = networkManager.TransportManager.Transport;
            transport.SetPort(NetSession.Port);
            if (mode == NetMode.Host || mode == NetMode.Server)
            {
                networkManager.ServerManager.StartConnection();
                Gameplay.Monster.TargetPicker = NearestPlayer;   // the server's monsters chase whoever is closest
            }
            if (mode == NetMode.Host)
                networkManager.ClientManager.StartConnection("localhost");
            if (mode == NetMode.Join)
                networkManager.ClientManager.StartConnection(address);
            if (mode == NetMode.Server)
                Debug.Log($"[dreamWorld] dedicated server listening on UDP {NetSession.Port}");
            // testing: "-autoshot <folder>" saves a screenshot every 1.5 s (development builds only)
            var args = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(args, "-autoshot");
            if (Debug.isDebugBuild && i >= 0 && i + 1 < args.Length) StartCoroutine(AutoShots(args[i + 1]));
        }

        System.Collections.IEnumerator AutoShots(string folder)
        {
            System.IO.Directory.CreateDirectory(folder);
            for (int n = 0; ; n++)
            {
                yield return new WaitForSecondsRealtime(1.5f);
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(folder, $"shot_{n:000}.png"));
            }
        }

        // The closest player who's up and about (monsters only notice players within 40 m anyway).
        static Gameplay.PlayerStats NearestPlayer(Vector3 from)
        {
            Gameplay.PlayerStats best = null;
            float bestDist = 40f;
            foreach (var p in NetPlayer.All)
            {
                if (p == null || p.IsDown) continue;
                float d = Vector3.Distance(p.transform.position, from);
                if (d < bestDist) { bestDist = d; best = p.GetComponent<Gameplay.PlayerStats>(); }
            }
            return best;
        }

        // Leaving the world (Esc back to the creator) closes the connection cleanly.
        void OnDestroy()
        {
            Gameplay.Monster.TargetPicker = null;
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
