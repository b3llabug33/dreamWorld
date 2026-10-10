using DreamWorld.CharacterCreation;
using UnityEngine;

namespace DreamWorld.Town
{
    // Strolls between random spots near where it started, pausing now and then. Works for townsfolk
    // (drives IdleMotion's walk cycle) and animals (plays their Walk / Idle animation clips).
    // Won't step into a building or a tree: it checks for something solid ahead before moving.
    public class Wanderer : MonoBehaviour
    {
        public float radius = 6f;
        public float speed = 1.1f;
        public Vector2 pauseSeconds = new Vector2(1.5f, 5f);
        [Tooltip("Animals: clip names (legacy Animation) to play while walking / standing.")]
        public string walkClip = "", idleClip = "", idle2Clip = "";

        Vector3 home, target;
        float waitUntil;
        IdleMotion motion;
        Animation anim;
        float stride;

        void Start()
        {
            home = transform.position;
            target = home;
            motion = GetComponentInChildren<IdleMotion>();
            anim = GetComponentInChildren<Animation>();
            waitUntil = Time.time + Random.Range(0f, 3f);
        }

        // Stop and look at someone for a moment (when they talk to us).
        public void PauseFor(float seconds, Vector3 lookAt)
        {
            waitUntil = Time.time + seconds;
            target = transform.position;
            Vector3 d = lookAt - transform.position;
            d.y = 0;
            if (d.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(d);
        }

        void Update()
        {
            Vector3 to = target - transform.position;
            to.y = 0;
            bool walking = Time.time >= waitUntil && to.magnitude > 0.2f;
            if (walking)
            {
                Vector3 dir = to.normalized;
                if (Physics.SphereCast(transform.position + Vector3.up * 0.6f, 0.25f, dir, out var hit, 0.8f, ~0, QueryTriggerInteraction.Ignore)
                    && !hit.transform.IsChildOf(transform))
                {
                    target = transform.position;     // bumped into something: pick somewhere else next time
                    walking = false;
                }
                else
                {
                    transform.position += dir * speed * Time.deltaTime;
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 1f - Mathf.Exp(-6f * Time.deltaTime));
                    stride += speed * Time.deltaTime / 1.3f * Mathf.PI * 2f;
                }
            }
            else if (Time.time >= waitUntil)
            {
                var r = Random.insideUnitCircle * radius;
                target = home + new Vector3(r.x, 0, r.y);
                waitUntil = Time.time + Random.Range(pauseSeconds.x, pauseSeconds.y);
            }

            if (motion != null)
            {
                motion.moveAmount = Mathf.MoveTowards(motion.moveAmount, walking ? 0.4f : 0f, Time.deltaTime * 3f);
                motion.stridePhase = stride;
            }
            if (anim != null)
            {
                string clip = walking ? walkClip : (Mathf.Repeat(Time.time + home.x, 14f) < 7f ? idleClip : idle2Clip);
                if (!string.IsNullOrEmpty(clip) && anim.GetClip(clip) != null && !anim.IsPlaying(clip)) anim.CrossFade(clip, 0.25f);
            }
        }
    }
}
