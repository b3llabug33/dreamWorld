using DreamWorld.Rendering;
using DreamWorld.World;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DreamWorld.Gameplay
{
    // Player options (pause menu -> settings). Saved in PlayerPrefs under "dreamWorld.settings.*",
    // loaded before the first scene and re-applied every time a scene loads.
    //   volumes: master goes straight to AudioListener.volume; music / sfx are read by the audio system.
    //   mouse sensitivity + invert Y: the follow camera.   toggle sprint: PlayerController.
    //   pixel size: the world camera's PSXCamera.targetHeight.   fullscreen, controls hint (WorldHUD).
    public static class Settings
    {
        const string Prefix = "dreamWorld.settings.";

        // "chunky / classic / sharp": vertical render height of the 3D image (PSXCamera).
        public static readonly int[] PixelHeights = { 300, 400, 540 };
        public static readonly string[] PixelNames = { "chunky", "classic", "sharp" };

        public static float MasterVolume { get; set; } = 1f;
        public static float MusicVolume { get; set; } = 0.8f;
        public static float SfxVolume { get; set; } = 1f;
        public static float MouseSensitivity { get; set; } = 2.2f;
        public static bool InvertY { get; set; }
        public static bool ToggleSprint { get; set; }
        public static int PixelLevel { get; set; } = 1;   // index into PixelHeights
        public static bool Fullscreen { get; set; } = true;
        public static bool ShowControlsHint { get; set; } = true;

        public static int PixelHeight => PixelHeights[Mathf.Clamp(PixelLevel, 0, PixelHeights.Length - 1)];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Init()
        {
            Load();
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Apply();

        public static void Load()
        {
            MasterVolume = PlayerPrefs.GetFloat(Prefix + "masterVolume", 1f);
            MusicVolume = PlayerPrefs.GetFloat(Prefix + "musicVolume", 0.8f);
            SfxVolume = PlayerPrefs.GetFloat(Prefix + "sfxVolume", 1f);
            MouseSensitivity = PlayerPrefs.GetFloat(Prefix + "mouseSensitivity", 2.2f);
            InvertY = PlayerPrefs.GetInt(Prefix + "invertY", 0) == 1;
            ToggleSprint = PlayerPrefs.GetInt(Prefix + "toggleSprint", 0) == 1;
            PixelLevel = Mathf.Clamp(PlayerPrefs.GetInt(Prefix + "pixelLevel", 1), 0, PixelHeights.Length - 1);
            Fullscreen = PlayerPrefs.GetInt(Prefix + "fullscreen", Screen.fullScreen ? 1 : 0) == 1;
            ShowControlsHint = PlayerPrefs.GetInt(Prefix + "showControlsHint", 1) == 1;
        }

        public static void Save()
        {
            PlayerPrefs.SetFloat(Prefix + "masterVolume", MasterVolume);
            PlayerPrefs.SetFloat(Prefix + "musicVolume", MusicVolume);
            PlayerPrefs.SetFloat(Prefix + "sfxVolume", SfxVolume);
            PlayerPrefs.SetFloat(Prefix + "mouseSensitivity", MouseSensitivity);
            PlayerPrefs.SetInt(Prefix + "invertY", InvertY ? 1 : 0);
            PlayerPrefs.SetInt(Prefix + "toggleSprint", ToggleSprint ? 1 : 0);
            PlayerPrefs.SetInt(Prefix + "pixelLevel", PixelLevel);
            PlayerPrefs.SetInt(Prefix + "fullscreen", Fullscreen ? 1 : 0);
            PlayerPrefs.SetInt(Prefix + "showControlsHint", ShowControlsHint ? 1 : 0);
            PlayerPrefs.Save();
        }

        // Push the values onto the things that use them. (Toggle sprint, invert Y, the controls hint and
        // the music/sfx volumes are read live, so they need nothing here.)
        public static void Apply()
        {
            AudioListener.volume = Mathf.Clamp01(MasterVolume);
            if (!Application.isEditor && Screen.fullScreen != Fullscreen) Screen.fullScreen = Fullscreen;

            // Only the in-world follow camera: the character creator drives its own render height while zooming.
            foreach (var cam in Object.FindObjectsByType<ThirdPersonCamera>(FindObjectsSortMode.None))
            {
                cam.mouseSensitivity = MouseSensitivity;
                var psx = cam.GetComponent<PSXCamera>();
                if (psx != null) psx.targetHeight = PixelHeight;
            }
        }
    }
}
