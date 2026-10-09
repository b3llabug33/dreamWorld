using UnityEditor;

namespace DreamWorld.EditorTools
{
    // Any texture imported under Assets/Art or Assets/Models gets PS1-style settings:
    // nearest-neighbor filtering (crisp pixels), no mipmaps, and no compression blur.
    // UI or other textures elsewhere are left alone.
    public class PSXTextureImporter : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/Art/") && !assetPath.StartsWith("Assets/Models/")) return;

            var importer = (TextureImporter)assetImporter;
            importer.filterMode = UnityEngine.FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
        }
    }
}
