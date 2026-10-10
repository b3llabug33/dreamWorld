using DreamWorld.Gameplay;
using UnityEngine;

namespace DreamWorld.World
{
    // The weather of an area that looks different from the town (Mistpine Hollow: overcast, misty, drizzly).
    // While the player walks from `fadeStart` to `fadeEnd` (the trail out of town), the fog, ambient light,
    // moonlight and sky blend from the scene's own settings to this area's, and a light drizzle fades in.
    public class AreaAtmosphere : MonoBehaviour
    {
        [Header("where it fades in (x positions along the trail)")]
        public float fadeStart = -66f;
        public float fadeEnd = -92f;

        [Header("the area's look")]
        public Color fogColor = new Color(0.42f, 0.47f, 0.50f);
        public float fogStart = 4f;
        public float fogEnd = 52f;
        public Color ambient = new Color(0.40f, 0.44f, 0.48f);
        public Color sunColor = new Color(0.78f, 0.84f, 0.95f);
        public float sunIntensity = 0.55f;
        [Range(0, 1)] public float drizzle = 0.45f;

        Color baseFog, baseAmbient, baseSun;
        float baseFogStart, baseFogEnd, baseSunIntensity;
        Light sun;
        Camera cam;
        CameraClearFlags baseClear;
        Color baseBackground;
        ParticleSystem rain;
        float blend;

        public static float Blend { get; private set; }   // 0 = town, 1 = fully in the area

        void Start()
        {
            baseFog = RenderSettings.fogColor;
            baseFogStart = RenderSettings.fogStartDistance;
            baseFogEnd = RenderSettings.fogEndDistance;
            baseAmbient = RenderSettings.ambientLight;
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) { sun = l; break; }
            if (sun != null) { baseSun = sun.color; baseSunIntensity = sun.intensity; }
        }

        void OnDisable() => Apply(0f);

        void Update()
        {
            var p = PlayerStats.Local != null ? PlayerStats.Local.transform.position : (Camera.main != null ? Camera.main.transform.position : Vector3.zero);
            float t = Mathf.InverseLerp(fadeStart, fadeEnd, p.x);
            t = t * t * (3f - 2f * t);
            blend = Mathf.MoveTowards(blend, t, Time.deltaTime * 2f);   // no pops after a respawn warp
            Apply(blend);
        }

        void Apply(float t)
        {
            Blend = t;
            RenderSettings.fogColor = Color.Lerp(baseFog, fogColor, t);
            RenderSettings.fogStartDistance = Mathf.Lerp(baseFogStart, fogStart, t);
            RenderSettings.fogEndDistance = Mathf.Lerp(baseFogEnd, fogEnd, t);
            RenderSettings.ambientLight = Color.Lerp(baseAmbient, ambient, t);
            if (sun != null)
            {
                sun.color = Color.Lerp(baseSun, sunColor, t);
                sun.intensity = Mathf.Lerp(baseSunIntensity, sunIntensity, t);
            }
            if (cam == null || cam != Camera.main)
            {
                cam = Camera.main;
                if (cam != null) { baseClear = cam.clearFlags; baseBackground = cam.backgroundColor; }
            }
            if (cam != null)
            {
                // the starry sky gives way to a flat overcast gray halfway along the trail
                cam.clearFlags = t > 0.5f ? CameraClearFlags.SolidColor : baseClear;
                cam.backgroundColor = t > 0.5f ? RenderSettings.fogColor : baseBackground;
                Rain(t);
            }
        }

        void Rain(float t)
        {
            if (drizzle <= 0f) return;
            if (rain == null)
            {
                if (t <= 0.01f) return;
                rain = MakeRain();
            }
            if (rain.transform.parent != cam.transform) rain.transform.SetParent(cam.transform, false);
            var em = rain.emission;
            em.rateOverTime = 420f * drizzle * t;
        }

        ParticleSystem MakeRain()
        {
            var go = new GameObject("Drizzle");
            go.transform.SetParent(cam.transform, false);
            go.transform.localPosition = new Vector3(0, 6f, 6f);
            go.transform.localRotation = Quaternion.identity;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.startLifetime = 1.1f;
            main.startSpeed = 0f;
            main.startSize = 0.03f;
            main.startColor = new Color(0.82f, 0.88f, 0.95f, 0.45f);
            main.maxParticles = 1200;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(26f, 1f, 26f);
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-0.6f, -0.4f);
            vel.y = new ParticleSystem.MinMaxCurve(-12f, -10f);
            vel.z = new ParticleSystem.MinMaxCurve(0.2f, 0.3f);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = 0.06f;
            r.lengthScale = 1f;
            r.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
            ps.Play();
            return ps;
        }
    }
}
