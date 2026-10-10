using System.Collections.Generic;
using DreamWorld.Gameplay;
using DreamWorld.World;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace DreamWorld.Audio
{
    // All of the game's sound. Made by itself when the game starts and kept across scenes.
    //   Music: one loop per area (town square, the woods, Mistpine Hollow), crossfaded as you walk between them,
    //          plus a tense combat layer that fades in on top while a monster is after you.
    //   Sound effects: AudioManager.Play("coin") for flat 2D sounds, Play3D("hit", position) for ones in the world.
    // Clips live in Assets/Resources/Audio/Music and Assets/Resources/Audio/Sfx (made by ArtSource/build_audio.py).
    // A name with numbered variations (step_grass_1, _2, ...) plays a random one when asked for "step_grass".
    // It also listens for a few things itself so other scripts don't need edits: the bag opening and closing,
    // pressing E at something, clicks and hovers on menu buttons, and monsters being defeated.
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        const float CrossfadeSeconds = 2.5f;
        static readonly Vector3 TownCenter = new Vector3(0f, 0f, 8f);
        const float TownRadius = 34f;
        const float MistpineX = -84f;

        AudioSource musicA, musicB, combatLayer;
        AudioSource current;                 // the area track fading in (the other one fades out)
        string currentTrack, wantedTrack;
        readonly Dictionary<string, float> resumeAt = new Dictionary<string, float>();
        float combatTarget, combatLevel;
        float checkTimer;
        bool inTown;

        readonly List<AudioSource> pool = new List<AudioSource>();
        int nextVoice;
        static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        static readonly Dictionary<string, AudioClip[]> variants = new Dictionary<string, AudioClip[]>();
        readonly Dictionary<AudioClip, float> lastPlayed = new Dictionary<AudioClip, float>();

        bool bagWasOpen;
        GameObject hovered;
        readonly List<RaycastResult> uiHits = new List<RaycastResult>();
        PointerEventData pointer;
        EventSystem pointerSystem;

        // ---------- start-up ----------

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Instance != null) return;
            var go = new GameObject("AudioManager");
            DontDestroyOnLoad(go);
            go.AddComponent<AudioManager>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            musicA = NewSource("Music A", true);
            musicB = NewSource("Music B", true);
            combatLayer = NewSource("Combat", true);
            for (int i = 0; i < 14; i++) pool.Add(NewSource("Sfx " + i, false));
            current = musicA;
        }

        void OnEnable()
        {
            Monster.Killed += OnMonsterKilled;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnDisable()
        {
            Monster.Killed -= OnMonsterKilled;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            hovered = null;
            bagWasOpen = false;
        }

        AudioSource NewSource(string name, bool loop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.loop = loop;
            s.spatialBlend = 0f;
            s.dopplerLevel = 0f;
            s.rolloffMode = AudioRolloffMode.Linear;
            s.minDistance = 3f;
            s.maxDistance = 32f;
            s.volume = 0f;
            return s;
        }

        // ---------- public API ----------

        // A 2D sound (UI, the player's own actions). volume is on top of the sfx volume setting.
        public static void Play(string name, float volume = 1f, float pitchJitter = 0.04f)
        {
            if (Instance != null) Instance.PlayAt(name, null, volume, pitchJitter);
        }

        // A sound at a place in the world: quieter the further it is from the camera.
        public static void Play3D(string name, Vector3 position, float volume = 1f, float pitchJitter = 0.06f)
        {
            if (Instance != null) Instance.PlayAt(name, position, volume, pitchJitter);
        }

        // One footstep: stone in the town square, grass everywhere else.
        public static void Footstep(Vector3 position)
        {
            if (Instance == null) return;
            bool stone = InTownSquare(position);
            Instance.PlayAt(stone ? "step_stone" : "step_grass", null, stone ? 0.55f : 0.7f, 0.08f);
        }

        public static string CurrentMusic => Instance != null ? Instance.currentTrack : null;
        public static float CombatLevel => Instance != null ? Instance.combatLevel : 0f;

        public static bool InTownSquare(Vector3 p)
        {
            var zone = ZoneMap.At(p);
            if (zone != null && zone.displayName == "town square") return true;
            var flat = new Vector3(p.x - TownCenter.x, 0f, p.z - TownCenter.z);
            return flat.magnitude < TownRadius;
        }

        // ---------- sound effects ----------

        void PlayAt(string name, Vector3? position, float volume, float pitchJitter)
        {
            var clip = Pick(name);
            if (clip == null) return;
            // the same sound twice in one instant (a kill granting coins twice, etc.) just sounds louder: skip it
            if (lastPlayed.TryGetValue(clip, out float last) && Time.unscaledTime - last < 0.04f) return;
            lastPlayed[clip] = Time.unscaledTime;

            var s = FreeVoice();
            s.clip = clip;
            s.volume = Mathf.Clamp01(volume) * VolumeSettings.Sfx;
            s.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
            if (position.HasValue)
            {
                s.spatialBlend = 1f;
                s.transform.position = position.Value;
            }
            else
            {
                s.spatialBlend = 0f;
                s.transform.localPosition = Vector3.zero;
            }
            s.Play();
        }

        AudioSource FreeVoice()
        {
            foreach (var s in pool)
                if (!s.isPlaying) return s;
            var steal = pool[nextVoice];   // all busy: cut off the oldest-ish one
            nextVoice = (nextVoice + 1) % pool.Count;
            return steal;
        }

        static AudioClip Pick(string name)
        {
            var one = Load("Audio/Sfx/" + name);
            if (one != null) return one;
            if (!variants.TryGetValue(name, out var list))
            {
                var found = new List<AudioClip>();
                for (int i = 1; i <= 8; i++)
                {
                    var c = Load($"Audio/Sfx/{name}_{i}");
                    if (c == null) break;
                    found.Add(c);
                }
                list = found.ToArray();
                variants[name] = list;
                if (list.Length == 0) Debug.LogWarning($"[dreamWorld] no sound called '{name}'");
            }
            return list.Length > 0 ? list[Random.Range(0, list.Length)] : null;
        }

        static AudioClip Load(string path)
        {
            if (!clips.TryGetValue(path, out var clip))
            {
                clip = Resources.Load<AudioClip>(path);
                clips[path] = clip;
            }
            return clip;
        }

        // ---------- every frame ----------

        void Update()
        {
            var player = PlayerStats.Local;
            checkTimer -= Time.unscaledDeltaTime;
            if (checkTimer <= 0f)
            {
                checkTimer = 0.25f;
                wantedTrack = player != null ? AreaTrack(player.transform.position) : null;
                combatTarget = player != null && !player.Dead && InDanger(player) ? 1f : 0f;
                EnsureListener();
            }
            UpdateMusic();
            WatchUi(player);
        }

        string AreaTrack(Vector3 p)
        {
            if (p.x < MistpineX || AreaAtmosphere.Blend > 0.85f) return "mistpine";
            // a little give at the edge of town, so standing on the line doesn't flip back and forth
            var flat = new Vector3(p.x - TownCenter.x, 0f, p.z - TownCenter.z);
            var zone = ZoneMap.At(p);
            bool town = (zone != null && zone.displayName == "town square") || flat.magnitude < (inTown ? TownRadius + 3f : TownRadius);
            inTown = town;
            return town ? "town" : "woods";
        }

        // Something is chasing you: a monster that's alive and close enough to have seen you (and you're not safe),
        // or you've been hit in the last few seconds.
        static bool InDanger(PlayerStats player)
        {
            var p = player.transform.position;
            if (ZoneMap.IsSafe(p)) return false;
            if (Time.time - player.LastHitTime < 4f) return true;
            foreach (var m in FindObjectsByType<Monster>(FindObjectsSortMode.None))
            {
                if (!m.Alive || !m.isActiveAndEnabled) continue;
                if (Vector3.Distance(m.transform.position, p) < m.sightRange) return true;
            }
            return false;
        }

        void UpdateMusic()
        {
            float dt = Time.unscaledDeltaTime;
            if (wantedTrack != currentTrack)
            {
                // remember where the old track was so coming back picks up where it left off
                if (currentTrack != null && current.clip != null) resumeAt[currentTrack] = current.time;
                currentTrack = wantedTrack;
                var other = current == musicA ? musicB : musicA;
                if (currentTrack != null)
                {
                    var clip = Load("Audio/Music/" + currentTrack);
                    if (other.clip != clip)
                    {
                        other.Stop();
                        other.clip = clip;
                        other.volume = 0f;
                    }
                    if (clip != null && !other.isPlaying)
                    {
                        resumeAt.TryGetValue(currentTrack, out float at);
                        other.time = clip.length > 0f ? at % clip.length : 0f;
                        other.Play();
                    }
                }
                current = other;
            }

            float music = VolumeSettings.Music;
            float step = dt / CrossfadeSeconds;
            float duck = 1f - 0.35f * combatLevel;   // the area track steps back a little under the combat layer
            Fade(musicA, step, music * duck);
            Fade(musicB, step, music * duck);

            combatLevel = Mathf.MoveTowards(combatLevel, combatTarget, dt / (combatTarget > combatLevel ? 1.5f : 3.5f));
            if (combatLevel > 0f)
            {
                if (combatLayer.clip == null) combatLayer.clip = Load("Audio/Music/combat");
                if (combatLayer.clip != null && !combatLayer.isPlaying) combatLayer.Play();
            }
            else if (combatLayer.isPlaying) combatLayer.Stop();
            combatLayer.volume = combatLevel * music * 0.8f;
        }

        // Fade levels are kept apart from the volume setting, so moving the slider applies instantly.
        float fadeA, fadeB;
        void Fade(AudioSource s, float step, float gain)
        {
            float level = s == current && currentTrack != null ? 1f : 0f;
            ref float fade = ref (s == musicA ? ref fadeA : ref fadeB);
            fade = Mathf.MoveTowards(fade, level, step);
            s.volume = fade * fade * gain;   // squared: a smoother-sounding fade
            if (fade <= 0f && s.isPlaying && s != current) s.Stop();
        }

        // A scene without an AudioListener would be silent: give the main camera one.
        static void EnsureListener()
        {
            if (FindAnyObjectByType<AudioListener>() != null) return;
            var cam = Camera.main;
            if (cam != null) cam.gameObject.AddComponent<AudioListener>();
        }

        // ---------- sounds other scripts don't make themselves ----------

        void OnMonsterKilled(Monster monster, PlayerStats killer)
        {
            if (monster != null) Play3D("defeat", monster.transform.position + Vector3.up * 0.5f, 1f, 0.1f);
        }

        void WatchUi(PlayerStats player)
        {
            bool bagOpen = InventoryUI.IsOpen;
            if (bagOpen != bagWasOpen) Play(bagOpen ? "bag_open" : "bag_close");
            bagWasOpen = bagOpen;

            // E at a door, sign, shop or computer
            if (player != null && !player.Dead && !bagOpen && DreamWorld.Gameplay.Keys.Down(KeyCode.E) &&
                Interactable.Nearest(player.transform.position) != null)
                Play("blip");

            // menu buttons (and inventory slots): a tick when the mouse moves onto one, a click when it's pressed
            var events = EventSystem.current;
            if (events == null || !Cursor.visible) { hovered = null; return; }
            if (pointer == null || pointerSystem != events) { pointer = new PointerEventData(events); pointerSystem = events; }
            pointer.position = Input.mousePosition;
            uiHits.Clear();
            events.RaycastAll(pointer, uiHits);
            GameObject target = null;
            if (uiHits.Count > 0)
            {
                var go = uiHits[0].gameObject;
                target = ExecuteEvents.GetEventHandler<IPointerClickHandler>(go);
                if (target == null) target = ExecuteEvents.GetEventHandler<IPointerDownHandler>(go);
                var selectable = target != null ? target.GetComponent<UnityEngine.UI.Selectable>() : null;
                if (selectable != null && !selectable.IsInteractable()) target = null;
            }
            if (target != null && target != hovered) Play("ui_hover", 1f, 0.02f);
            hovered = target;
            if (target != null && Input.GetMouseButtonDown(0)) Play("ui_click", 1f, 0.02f);
        }
    }

    // The volume sliders. Reads the game's Settings (Settings.MusicVolume / Settings.SfxVolume, 0..1) when it
    // exists, otherwise the same PlayerPrefs it saves to. Looked up by name so this compiles either way.
    public static class VolumeSettings
    {
        const string MusicKey = "dreamWorld.settings.musicVolume";
        const string SfxKey = "dreamWorld.settings.sfxVolume";
        static System.Reflection.PropertyInfo musicProp, sfxProp;
        static bool looked;
        static float cacheTime = -1f, music = 0.7f, sfx = 0.8f;

        public static float Music { get { Refresh(); return music; } }
        public static float Sfx { get { Refresh(); return sfx; } }

        static void Refresh()
        {
            if (Mathf.Approximately(cacheTime, Time.unscaledTime)) return;   // once per frame is plenty
            cacheTime = Time.unscaledTime;
            if (!looked)
            {
                looked = true;
                var type = System.Type.GetType("DreamWorld.Gameplay.Settings");
                if (type == null)
                    foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
                        if ((type = asm.GetType("DreamWorld.Gameplay.Settings")) != null) break;
                if (type != null)
                {
                    var flags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static;
                    musicProp = type.GetProperty("MusicVolume", flags);
                    sfxProp = type.GetProperty("SfxVolume", flags);
                }
            }
            music = Read(musicProp, MusicKey, 0.7f);
            sfx = Read(sfxProp, SfxKey, 0.8f);
        }

        static float Read(System.Reflection.PropertyInfo prop, string key, float fallback)
        {
            if (prop != null)
            {
                try { return Mathf.Clamp01(System.Convert.ToSingle(prop.GetValue(null))); }
                catch (System.Exception) { }
            }
            return Mathf.Clamp01(PlayerPrefs.GetFloat(key, fallback));
        }
    }
}
