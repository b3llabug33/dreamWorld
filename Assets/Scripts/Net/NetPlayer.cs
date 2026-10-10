using DreamWorld.Account;
using DreamWorld.CharacterCreation;
using DreamWorld.Gameplay;
using DreamWorld.World;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace DreamWorld.Net
{
    // The networked version of the character. The owner (the person playing it) drives movement and
    // sends what everyone else needs to see: their look, name, weapon and shield, whether they're guarding,
    // and each roll, swing and spell as it starts. Everyone else's copy replays those on the same model
    // (PlayerController.PlayRemoteAction / Abilities.PlayRemote) without doing any damage of its own.
    public class NetPlayer : NetworkBehaviour
    {
        // The whole look is the small CharacterSave JSON (style + color per slot).
        readonly SyncVar<string> look = new SyncVar<string>();
        readonly SyncVar<string> displayName = new SyncVar<string>();
        readonly SyncVar<string> gear = new SyncVar<string>();      // "weapon|shield" item ids
        readonly SyncVar<bool> guarding = new SyncVar<bool>();
        readonly SyncVar<bool> down = new SyncVar<bool>();          // knocked out (hidden until they wake at home)

        public static readonly System.Collections.Generic.List<NetPlayer> All = new System.Collections.Generic.List<NetPlayer>();
        public static NetPlayer Mine { get; private set; }   // the one this game is playing
        public string DisplayName => displayName.Value;
        public bool IsDown => down.Value;

        CharacterAppearance appearance;
        PlayerController controller;
        CharacterController body;
        PlayerStats stats;
        Abilities abilities;
        IdleMotion motion;
        NameTag nameTag;
        Vector3 lastPosition;
        float smoothedSpeed;
        string sentGear;
        bool sentGuard, sentDown;

        void Awake()
        {
            appearance = GetComponent<CharacterAppearance>();
            controller = GetComponent<PlayerController>();
            body = GetComponent<CharacterController>();
            stats = GetComponent<PlayerStats>();
            abilities = GetComponent<Abilities>();
            motion = GetComponentInChildren<IdleMotion>();
            // only the owner moves this character (and has bars, saving, spells, a HUD); everyone else follows the
            // network. These start switched off in the prefab, so a remote copy never claims PlayerStats.Local.
            controller.enabled = false;
            body.enabled = false;
            SetOwnerOnly(false);
            look.OnChange += (previous, next, asServer) => ApplyLook(next);
            gear.OnChange += (previous, next, asServer) => ApplyGear(next);
            displayName.OnChange += (previous, next, asServer) => { if (nameTag != null) nameTag.Set(next); };
            down.OnChange += (previous, next, asServer) => ApplyDown(next);
        }

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public override void OnStartClient()
        {
            base.OnStartClient();
            ApplyLook(look.Value);
            ApplyGear(gear.Value);
            ApplyDown(down.Value);
            Debug.Log($"[dreamWorld] player {OwnerId} spawned (mine: {IsOwner}) at {transform.position}");
            if (!IsOwner)
            {
                nameTag = NameTag.Create(transform, displayName.Value);
                MakeHitbox();
                // hits on someone else's character go to their game (monsters on the server, PvP from anyone)
                stats.DamageRedirect = (amount, source) => { RelayHurt(amount, source != null ? source.GetComponent<NetworkObject>() : null); return true; };
                return;
            }

            Mine = this;
            foreach (var other in All)   // walk through other players (their hitboxes are only for hits)
                if (other != this && other.hitbox != null) Physics.IgnoreCollision(body, other.hitbox);
            body.enabled = true;
            controller.enabled = true;
            SetOwnerOnly(true);
            var cam = FindAnyObjectByType<ThirdPersonCamera>();
            if (cam != null)
            {
                cam.target = transform;
                controller.cameraTransform = cam.transform;
            }
            controller.ActionStarted += OnActionStarted;
            // testing: "-spawnat x z" starts this player somewhere else (development builds only)
            var args = System.Environment.GetCommandLineArgs();
            int at = System.Array.IndexOf(args, "-spawnat");
            if (Debug.isDebugBuild && at >= 0 && at + 2 < args.Length && float.TryParse(args[at + 1], out float sx) && float.TryParse(args[at + 2], out float sz))
            {
                body.enabled = false;   // (the controller hasn't started yet, so move it directly)
                transform.position = new Vector3(sx, transform.position.y, sz);
                body.enabled = true;
            }
            if (abilities != null) abilities.SpellCast += OnSpellCast;
            SendLook(CharacterSave.SavedJson);
            string name = !string.IsNullOrEmpty(AccountService.CurrentName) ? AccountService.CurrentName
                        : !string.IsNullOrEmpty(AccountService.Username) ? AccountService.Username : "player";
            SendName(name);
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            if (Mine == this) Mine = null;
            if (controller != null) controller.ActionStarted -= OnActionStarted;
            if (abilities != null) abilities.SpellCast -= OnSpellCast;
        }

        void SetOwnerOnly(bool on)
        {
            foreach (var c in new Behaviour[] { stats, GetComponent<PlayerLife>(), abilities })
                if (c != null) c.enabled = on;
        }

        // ---------------------------------------------------------------- owner -> server -> everyone

        [ServerRpc]
        void SendLook(string json)
        {
            look.Value = json;
            Debug.Log($"[dreamWorld] server got look from player {OwnerId} ({json.Length} chars)");
        }

        [ServerRpc]
        void SendName(string name) => displayName.Value = Clean(name);

        [ServerRpc]
        void SendGear(string value) => gear.Value = value;

        [ServerRpc]
        void SendGuard(bool on) => guarding.Value = on;

        [ServerRpc]
        void SendDown(bool on) => down.Value = on;

        void OnActionStarted(int kind, int combo, float length, Vector3 dir) => SendAction(kind, combo, length, dir);

        [ServerRpc]
        void SendAction(int kind, int combo, float length, Vector3 dir) => ShowAction(kind, combo, length, dir);

        [ObserversRpc(ExcludeOwner = true)]
        void ShowAction(int kind, int combo, float length, Vector3 dir)
        {
            if (controller != null) controller.PlayRemoteAction(kind, combo, length, dir);
        }

        void OnSpellCast(string id, Vector3 from, Vector3 dir) => SendSpell(id, from, dir);

        [ServerRpc]
        void SendSpell(string id, Vector3 from, Vector3 dir) => ShowSpell(id, from, dir);

        [ObserversRpc(ExcludeOwner = true)]
        void ShowSpell(string id, Vector3 from, Vector3 dir)
        {
            if (abilities != null) abilities.PlayRemote(id, from, dir);
        }

        // ---------------------------------------------------------------- getting hit, rewards

        CapsuleCollider hitbox;

        // Other players' copies have no CharacterController running, so they get a capsule to be hit by swings,
        // shots and monsters. Your own character passes through it.
        void MakeHitbox()
        {
            hitbox = gameObject.AddComponent<CapsuleCollider>();
            hitbox.center = new Vector3(0, 0.82f, 0);
            hitbox.height = 1.6f;
            hitbox.radius = 0.3f;
            if (Mine != null && Mine.body != null) Physics.IgnoreCollision(Mine.body, hitbox);
        }

        void RelayHurt(float amount, NetworkObject source)
        {
            if (IsServerStarted) HurtFromServer(amount, source);
            else SendHurt(amount, source);
        }

        [ServerRpc(RequireOwnership = false)]
        void SendHurt(float amount, NetworkObject source) => HurtFromServer(amount, source);

        // Server: checks the hit makes sense, then tells the player's own game to take it.
        void HurtFromServer(float amount, NetworkObject source)
        {
            if (down.Value) return;
            amount = Mathf.Clamp(amount, 0f, 400f);
            if (source != null)
            {
                if (Vector3.Distance(source.transform.position, transform.position) > 30f) return;
                // PvP: nobody can attack from a safe zone (the victim's own game checks theirs)
                if (source.GetComponent<NetPlayer>() != null && ZoneMap.IsSafe(source.transform.position)) return;
            }
            Hurt(Owner, amount, source);
        }

        [TargetRpc]
        void Hurt(NetworkConnection conn, float amount, NetworkObject source)
        {
            if (stats != null) stats.TakeDamage(amount, source != null ? source.gameObject : null);
        }

        // Server: a monster this player finished off. Their game adds what it rolled.
        public void GiveReward(NetworkObject monster, int xp, int coins, string drops) => Rewarded(Owner, monster, xp, coins, drops);

        [TargetRpc]
        void Rewarded(NetworkConnection conn, NetworkObject monster, int xp, int coins, string drops)
        {
            if (stats == null) return;
            stats.AddXp(xp);
            if (coins > 0) stats.AddCoins(coins);
            foreach (var entry in (drops ?? "").Split(';'))
            {
                var parts = entry.Split(':');
                if (parts.Length != 2 || !int.TryParse(parts[1], out int n)) continue;
                int got = stats.AddItem(parts[0], n);
                var def = Items.Get(parts[0]);
                if (got > 0 && def != null) GameHUD.Instance?.Toast($"+{got} {def.name}");
                if (got < n) GameHUD.Instance?.Toast("your bag is full");
            }
            var m = monster != null ? monster.GetComponent<Monster>() : null;
            if (m != null) Monster.RaiseKilled(m, stats);   // quests, bounties and the kill toast count it
        }

        // ---------------------------------------------------------------- chat

        float lastChat = -9f;

        public void Say(string message) => SendChat(message);

        [ServerRpc]
        void SendChat(string message)
        {
            if (Time.time - lastChat < 0.6f) return;   // no spamming
            lastChat = Time.time;
            message = (message ?? "").Replace('\n', ' ').Replace('\r', ' ').Trim();
            if (message.Length == 0) return;
            if (message.Length > ChatUI.MaxLength) message = message.Substring(0, ChatUI.MaxLength);
            ShowChat(message);
        }

        [ObserversRpc]
        void ShowChat(string message)
        {
            if (ChatUI.Instance != null) ChatUI.Instance.Add(DisplayName, message, IsOwner);
            if (nameTag != null) nameTag.Say(message);
        }

        // ---------------------------------------------------------------- applying synced state

        void ApplyLook(string json)
        {
            if (!string.IsNullOrEmpty(json) && appearance != null) CharacterSave.ApplyJson(appearance, json);
        }

        void ApplyGear(string value)
        {
            if (IsOwner || stats == null || string.IsNullOrEmpty(value)) return;
            var parts = value.Split('|');
            stats.progress.equipped = parts[0];
            stats.progress.offhand = parts.Length > 1 ? parts[1] : "";
        }

        void ApplyDown(bool on)
        {
            if (IsOwner) return;
            foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = !on;
            if (nameTag != null) nameTag.gameObject.SetActive(!on);
            if (hitbox != null) hitbox.enabled = !on;
        }

        // Names are shown over heads: one short line of plain text.
        static string Clean(string name)
        {
            if (string.IsNullOrEmpty(name)) return "player";
            name = name.Replace("<", "").Replace(">", "").Trim();
            return name.Length > 20 ? name.Substring(0, 20) : name;
        }

        void Update()
        {
            if (IsOwner)
            {
                // send changes as they happen (equipping from the bag, raising a shield, getting knocked out)
                string g = stats.progress.equipped + "|" + stats.progress.offhand;
                if (g != sentGear) { sentGear = g; SendGear(g); }
                bool guard = stats.GuardReduction > 0f;
                if (guard != sentGuard) { sentGuard = guard; SendGuard(guard); }
                if (stats.Dead != sentDown) { sentDown = stats.Dead; SendDown(stats.Dead); }
                lastPosition = transform.position;
                return;
            }
            if (motion == null) { lastPosition = transform.position; return; }
            // remote players: walk cycle from the synced movement, plus whatever roll/swing their owner sent
            var delta = transform.position - lastPosition;
            delta.y = 0;
            float speed = Time.deltaTime > 0 ? delta.magnitude / Time.deltaTime : 0;
            smoothedSpeed = Mathf.Lerp(smoothedSpeed, speed, 1f - Mathf.Exp(-10f * Time.deltaTime));
            motion.stridePhase += smoothedSpeed * Time.deltaTime / 1.3f * Mathf.PI * 2f;
            stats.GuardReduction = guarding.Value ? 0.75f : 0f;   // the guard pose (WeaponPose reads it)
            controller.TickRemote(smoothedSpeed);
            lastPosition = transform.position;
        }
    }
}
