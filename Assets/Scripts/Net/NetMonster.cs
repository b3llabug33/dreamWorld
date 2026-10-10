using DreamWorld.Gameplay;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace DreamWorld.Net
{
    // A monster everyone shares. The server runs the real Monster (it chases the nearest player); every client's
    // copy is a puppet: its position and its squashing, hopping body come from the NetworkTransforms, its health
    // and rage from here, and its shots and shockwaves are replayed as harmless effects. Hitting a puppet sends
    // the hit to the server; a kill's rewards go to the player who landed it (NetPlayer.Rewarded).
    public class NetMonster : NetworkBehaviour
    {
        readonly SyncVar<float> health = new SyncVar<float>();
        readonly SyncVar<bool> enraged = new SyncVar<bool>();
        Monster monster;

        void Awake() => monster = GetComponent<Monster>();

        public override void OnStartServer()
        {
            base.OnStartServer();
            monster.FxSent = (kind, at, dir, size, color) => ShowFx(kind, at, dir, size, color);
            monster.RewardRedirect = Reward;
            health.Value = monster.Health;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (IsServerStarted) return;   // the host's copy is the real one
            monster.Puppet = true;
            monster.HitRedirect = (amount, source) =>
            {
                var attacker = source != null ? source.GetComponent<NetworkObject>() : null;
                SendHit(amount, attacker);
                return true;
            };
            monster.SetNetState(health.Value, enraged.Value);
        }

        void Update()
        {
            if (IsServerStarted)
            {
                if (!Mathf.Approximately(health.Value, monster.Health)) health.Value = monster.Health;
                if (enraged.Value != monster.Enraged) enraged.Value = monster.Enraged;
            }
            else if (monster.Puppet) monster.SetNetState(health.Value, enraged.Value);
        }

        [ServerRpc(RequireOwnership = false)]
        void SendHit(float amount, NetworkObject attacker)
        {
            if (!monster.Alive) return;
            amount = Mathf.Clamp(amount, 0f, 400f);   // nothing in the game hits harder than this
            var source = attacker != null ? attacker.gameObject : null;
            // the attacker has to be close by (a swing, a shot or a spell, not from across the map)
            if (source != null && Vector3.Distance(source.transform.position, transform.position) > 30f) return;
            if (monster.TakeDamage(amount, source)) Flash();
        }

        [ObserversRpc]
        void Flash()
        {
            if (!IsServerStarted) monster.FlashHit();
        }

        [ObserversRpc]
        void ShowFx(string kind, Vector3 at, Vector3 dir, float size, Color color)
        {
            if (IsServerStarted) return;   // the host already made the real one
            switch (kind)
            {
                case "shot":
                    Projectile.Fire(gameObject, at, dir, dir.magnitude, 0f, color, size).visualOnly = true;
                    break;
                case "wave":
                    Shockwave.Spawn(at, size, color);
                    break;
                case "ring":
                    SpellFx.Ring(at, color, size, dir.x);
                    break;
            }
        }

        void Reward(PlayerStats killer, int xp, int coins, string drops)
        {
            var player = killer.GetComponent<NetPlayer>();
            if (player != null) player.GiveReward(this.NetworkObject, xp, coins, drops);
        }
    }
}
