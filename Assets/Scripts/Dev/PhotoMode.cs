using UnityEngine;

namespace DreamWorld.Dev
{
    // F1 anywhere: hide every menu, bar and hint so only the game shows (screenshots, TikTok clips).
    // Press F1 again to bring them back.
    public class PhotoMode : MonoBehaviour
    {
        public static bool Hidden { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create()
        {
            Hidden = false;
            var go = new GameObject("PhotoMode");
            DontDestroyOnLoad(go);
            go.AddComponent<PhotoMode>();
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.F1)) Set(!Hidden);
            if (Hidden) Apply();   // menus can be built after the toggle (new scene, new panels)
        }

        public static void Set(bool hidden)
        {
            Hidden = hidden;
            Apply();
        }

        static void Apply()
        {
            foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (canvas.isRootCanvas) canvas.enabled = !Hidden;
        }
    }
}
