using DreamWorld.CharacterCreation;
using DreamWorld.World;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace DreamWorld.Net
{
    // The networked version of the character. The owner (the person playing it) drives movement
    // and sends their saved look once; everyone else sees the synced position, the look, and a
    // walk animation worked out from how fast the character is moving.
    public class NetPlayer : NetworkBehaviour
    {
        // The whole look is the small CharacterSave JSON (style + color per slot).
        readonly SyncVar<string> look = new SyncVar<string>();

        CharacterAppearance appearance;
        PlayerController controller;
        CharacterController body;
        IdleMotion motion;
        Vector3 lastPosition;
        float smoothedSpeed;

        void Awake()
        {
            appearance = GetComponent<CharacterAppearance>();
            controller = GetComponent<PlayerController>();
            body = GetComponent<CharacterController>();
            motion = GetComponentInChildren<IdleMotion>();
            // only the owner moves this character (and has bars, saving, a HUD); everyone else follows the network
            controller.enabled = false;
            body.enabled = false;
            SetOwnerOnly(false);
            look.OnChange += (previous, next, asServer) => ApplyLook(next);
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            ApplyLook(look.Value);
            Debug.Log($"[dreamWorld] player {OwnerId} spawned (mine: {IsOwner}) at {transform.position}");
            if (!IsOwner) return;

            body.enabled = true;
            controller.enabled = true;
            SetOwnerOnly(true);
            var cam = FindAnyObjectByType<ThirdPersonCamera>();
            if (cam != null)
            {
                cam.target = transform;
                controller.cameraTransform = cam.transform;
            }
            SendLook(CharacterSave.SavedJson);
        }

        void SetOwnerOnly(bool on)
        {
            foreach (var c in new Behaviour[] { GetComponent<DreamWorld.Gameplay.PlayerStats>(), GetComponent<DreamWorld.Gameplay.PlayerLife>() })
                if (c != null) c.enabled = on;
        }

        [ServerRpc]
        void SendLook(string json)
        {
            look.Value = json;
            Debug.Log($"[dreamWorld] server got look from player {OwnerId} ({json.Length} chars)");
        }

        void ApplyLook(string json)
        {
            if (!string.IsNullOrEmpty(json) && appearance != null) CharacterSave.ApplyJson(appearance, json);
        }

        void Update()
        {
            if (IsOwner || motion == null) { lastPosition = transform.position; return; }
            // remote players: animate from the synced movement
            var delta = transform.position - lastPosition;
            delta.y = 0;
            float speed = Time.deltaTime > 0 ? delta.magnitude / Time.deltaTime : 0;
            smoothedSpeed = Mathf.Lerp(smoothedSpeed, speed, 1f - Mathf.Exp(-10f * Time.deltaTime));
            motion.moveAmount = Mathf.Clamp01(smoothedSpeed / 4.6f);
            motion.stridePhase += smoothedSpeed * Time.deltaTime / 1.3f * Mathf.PI * 2f;
            lastPosition = transform.position;
        }
    }
}
