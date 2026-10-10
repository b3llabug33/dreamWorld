using DreamWorld.Gameplay;
using UnityEngine;

namespace DreamWorld.Quests
{
    // Orion, a friendly rainbow stag beetle (and a person too). She hangs around the town square.
    // The first time you walk into the square she scuttles over and greets you; if you back off
    // before she reaches you, she reassures you first. After that, press E on her to reopen her menu.
    public class OrionNpc : Interactable
    {
        public Transform model;                 // the beetle mesh (bobs and turns)
        public float walkSpeed = 2.2f;
        public float greetDistance = 2.0f;

        Vector3 home;
        enum Mode { Idle, Approach, Talking }
        Mode mode = Mode.Idle;
        float stepPhase;
        float playerBackedOff;
        Vector3 lastPlayerPos;

        void Start()
        {
            home = transform.position;
            QuestHUD.Npcs[QuestBook.Orion] = transform;
            if (model == null && transform.childCount > 0) model = transform.GetChild(0);
        }

        public override string Prompt(PlayerStats player) => "talk to Orion";

        public override void Interact(PlayerStats player) => Talk(false);

        void Talk(bool scared)
        {
            var q = QuestManager.Instance;
            if (q == null || DialogueUI.Instance == null) return;
            mode = Mode.Talking;
            var d = QuestBook.OrionTalk(q, scared);
            d.onEnd = () => mode = Mode.Idle;
            DialogueUI.Instance.Show(d);
        }

        void Update()
        {
            var p = PlayerStats.Local;
            var q = QuestManager.Instance;
            if (p == null || q == null) return;
            Vector3 toPlayer = p.transform.position - transform.position;
            toPlayer.y = 0;

            // first arrival in the square: go say hi
            bool menus = GameHUD.Instance != null && GameHUD.Instance.MenuOpen || InventoryUI.IsOpen;   // never greet over a menu
            if (mode == Mode.Idle && !q.Has("orion_met") && !DialogueUI.IsOpen && !menus)
            {
                var zone = ZoneMap.At(p.transform.position);
                if (zone != null && zone.displayName == "town square" && toPlayer.magnitude < 14f)
                {
                    mode = Mode.Approach;
                    playerBackedOff = 0f;
                    lastPlayerPos = p.transform.position;
                }
            }

            if (mode == Mode.Approach)
            {
                // the player walking away from her while she comes over = a little scared
                Vector3 moved = p.transform.position - lastPlayerPos;
                if (Vector3.Dot(moved, toPlayer.normalized) > 0.01f) playerBackedOff += moved.magnitude;
                lastPlayerPos = p.transform.position;
                if (toPlayer.magnitude > greetDistance)
                {
                    Step(toPlayer.normalized, walkSpeed * (playerBackedOff > 1.5f ? 1.6f : 1f));
                }
                else
                {
                    Face(toPlayer);
                    if (!menus) Talk(playerBackedOff > 1.5f);   // a menu got opened on the way: wait until it closes
                }
                if (toPlayer.magnitude > 25f) mode = Mode.Idle;   // you ran off; she'll try again later
            }
            else if (mode == Mode.Idle)
            {
                // wander back toward her spot, and look at you when you're close
                Vector3 toHome = home - transform.position;
                toHome.y = 0;
                if (toHome.magnitude > 0.5f) Step(toHome.normalized, walkSpeed * 0.6f);
                else if (toPlayer.magnitude < 5f) Face(toPlayer);
                Bob(0f);
            }
            else
            {
                Face(toPlayer);
                Bob(0f);
            }
        }

        void Step(Vector3 dir, float speed)
        {
            transform.position += dir * speed * Time.deltaTime;
            Face(dir);
            stepPhase += Time.deltaTime * speed * 9f;
            Bob(1f);
        }

        void Face(Vector3 dir)
        {
            if (dir.sqrMagnitude < 0.001f) return;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 1f - Mathf.Exp(-8f * Time.deltaTime));
        }

        // a tiny scuttle wobble while walking, slow breathing otherwise
        void Bob(float walking)
        {
            if (model == null) return;
            float wobble = walking * Mathf.Sin(stepPhase) * 4f;
            float breathe = Mathf.Sin(Time.time * 2f) * 0.01f;
            model.localRotation = Quaternion.Euler(0, 0, wobble);
            model.localPosition = new Vector3(0, breathe + walking * Mathf.Abs(Mathf.Sin(stepPhase)) * 0.02f, 0);
        }
    }
}
