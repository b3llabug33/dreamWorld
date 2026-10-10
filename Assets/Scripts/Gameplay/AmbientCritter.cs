using UnityEngine;

namespace DreamWorld.Gameplay
{
    // A harmless little bug that potters around its spot (beetles, ladybugs and snails crawl;
    // moths and fireflies drift in the air). It scoots away if you get close. Purely for looks.
    public class AmbientCritter : MonoBehaviour
    {
        public float wanderRadius = 3f;
        public float speed = 0.5f;
        public bool flies;
        public float flyHeight = 1.4f;

        Vector3 home, target;
        float wait, seed;
        Transform body;
        Vector3 bodyBase;

        void Start()
        {
            home = transform.position;
            target = home;
            seed = Random.value * 100f;
            body = transform.childCount > 0 ? transform.GetChild(0) : transform;
            bodyBase = body.localPosition;
        }

        void Update()
        {
            float t = Time.time + seed;
            var player = PlayerStats.Local;
            Vector3 to = target - transform.position;
            to.y = 0f;
            bool scared = player != null && (player.transform.position - transform.position).sqrMagnitude < 2.2f * 2.2f;
            if (scared)
            {
                Vector3 away = transform.position - player.transform.position;
                away.y = 0f;
                target = home + Vector3.ClampMagnitude(transform.position - home + away.normalized * 2f, wanderRadius * 1.3f);
                wait = 0f;
            }
            if (to.magnitude < 0.1f)
            {
                wait -= Time.deltaTime;
                if (wait <= 0f)
                {
                    var r = Random.insideUnitCircle * wanderRadius;
                    target = home + new Vector3(r.x, 0f, r.y);
                    wait = Random.Range(1f, 4f);
                }
            }
            else
            {
                float s = speed * (scared ? 3f : 1f);
                transform.position += to.normalized * Mathf.Min(to.magnitude, s * Time.deltaTime);
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to), 1f - Mathf.Exp(-6f * Time.deltaTime));
            }
            if (flies)
            {
                body.localPosition = bodyBase + Vector3.up * (flyHeight + Mathf.Sin(t * 1.7f) * 0.25f);
                body.localScale = new Vector3(1f + Mathf.Sin(t * 30f) * 0.15f, 1f, 1f);   // fluttering wings
            }
            else body.localPosition = bodyBase + Vector3.up * Mathf.Abs(Mathf.Sin(t * 10f)) * 0.01f;
        }
    }
}
