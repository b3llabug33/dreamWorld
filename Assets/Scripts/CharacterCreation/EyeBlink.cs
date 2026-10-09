using UnityEngine;

namespace DreamWorld.CharacterCreation
{
    // Makes the character blink. Each Eyes_* mesh has five layers (submeshes):
    // 0 face, 1 eye base, 2 iris, 3 pupil+shine, 4 closed lids. Normally layer 4 uses the
    // invisible material; during a blink layers 1-3 swap to it and layer 4 shows instead.
    public class EyeBlink : MonoBehaviour
    {
        public CharacterAppearance appearance;
        public Material hiddenMaterial;
        public Vector2 secondsBetweenBlinks = new Vector2(2.2f, 5.5f);
        public float blinkDuration = 0.12f;
        [Range(0, 1)] public float doubleBlinkChance = 0.2f;

        float nextBlink;
        float blinkEnd;
        bool doubleQueued;
        SkinnedMeshRenderer closedRenderer;
        Material[] openMaterials;

        void Start() => Schedule();

        void Schedule() => nextBlink = Time.time + Random.Range(secondsBetweenBlinks.x, secondsBetweenBlinks.y);

        void Update()
        {
            if (closedRenderer == null && Time.time >= nextBlink) Close();
            else if (closedRenderer != null && Time.time >= blinkEnd)
            {
                Open();
                if (!doubleQueued && Random.value < doubleBlinkChance)
                {
                    doubleQueued = true;
                    nextBlink = Time.time + 0.14f;
                }
                else
                {
                    doubleQueued = false;
                    Schedule();
                }
            }
        }

        void Close()
        {
            var eyes = appearance.GetSlot(OutfitCategory.Eyes)?.CurrentStyle;
            if (eyes == null) { Schedule(); return; }
            var r = eyes.GetComponent<SkinnedMeshRenderer>();
            if (r.sharedMaterials.Length < 5) { Schedule(); return; }
            openMaterials = r.sharedMaterials;
            var eyeMaterial = openMaterials[0];
            r.sharedMaterials = new[] { eyeMaterial, hiddenMaterial, hiddenMaterial, hiddenMaterial, eyeMaterial };
            closedRenderer = r;
            blinkEnd = Time.time + blinkDuration;
        }

        void Open()
        {
            if (closedRenderer != null) closedRenderer.sharedMaterials = openMaterials;
            closedRenderer = null;
        }

        void OnDisable() => Open();
    }
}
