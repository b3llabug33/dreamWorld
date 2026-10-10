using UnityEngine;

namespace DreamWorld.Gameplay
{
    // A glowing shot that flies straight and hits the first thing it touches: slingshot pebbles,
    // rot slug spit and static wisp zaps. Players' shots hit monsters, monsters' shots hit players.
    public class Projectile : MonoBehaviour
    {
        public float speed = 10f, damage = 10f, radius = 0.25f, life = 2.5f, gravity;
        public GameObject owner;
        Vector3 velocity;
        float age;
        bool fromPlayer;

        static Material shotMaterial;

        // Builds one: a small glowing ball (and a tiny light) flying from `from` toward `dir`.
        public static Projectile Fire(GameObject owner, Vector3 from, Vector3 dir, float speed, float damage, Color color,
                                      float size = 0.22f, float gravity = 0f)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Shot";
            Destroy(go.GetComponent<Collider>());
            go.transform.position = from;
            go.transform.localScale = Vector3.one * size;
            if (shotMaterial == null)
            {
                shotMaterial = new Material(Shader.Find("DreamWorld/PSX Lit"));
                shotMaterial.SetFloat("_Unlit", 1f);
            }
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = shotMaterial;
            var block = new MaterialPropertyBlock();
            block.SetColor("_Color", color);
            r.SetPropertyBlock(block);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var light = new GameObject("Glow").AddComponent<Light>();
            light.transform.SetParent(go.transform, false);
            light.type = LightType.Point;
            light.color = color;
            light.range = 2.5f;
            light.intensity = 1.2f;
            light.renderMode = LightRenderMode.ForceVertex;
            var p = go.AddComponent<Projectile>();
            p.owner = owner;
            p.speed = speed;
            p.damage = damage;
            p.radius = size * 0.6f + 0.12f;
            p.gravity = gravity;
            p.velocity = dir.normalized * speed;
            p.fromPlayer = owner != null && owner.GetComponent<PlayerStats>() != null;
            return p;
        }

        void Update()
        {
            age += Time.deltaTime;
            if (age > life) { Destroy(gameObject); return; }
            velocity += Vector3.down * gravity * Time.deltaTime;
            Vector3 step = velocity * Time.deltaTime;
            foreach (var hit in Physics.SphereCastAll(transform.position, radius, step.normalized, step.magnitude, ~0, QueryTriggerInteraction.Ignore))
            {
                if (owner != null && hit.transform.IsChildOf(owner.transform)) continue;
                var target = hit.collider.GetComponentInParent<IDamageable>();
                if (target != null)
                {
                    bool isPlayer = target is PlayerStats;
                    if (isPlayer == fromPlayer) continue;   // no friendly fire
                    if (target.Alive && !(isPlayer && ZoneMap.IsSafe(target.Body.position))) target.TakeDamage(damage, owner);
                    Destroy(gameObject);
                    return;
                }
                if (hit.collider.GetComponentInParent<CharacterController>() != null) continue;
                Destroy(gameObject);   // hit a wall, a tree or the ground
                return;
            }
            transform.position += step;
            if (transform.position.y < -0.2f) Destroy(gameObject);
        }
    }
}
