using DreamWorld.Gameplay;
using DreamWorld.Net;
using FishNet.Component.Transforming;
using FishNet.Object;
using UnityEditor;
using UnityEngine;

namespace DreamWorld.EditorTools
{
    public static partial class WorldBuilder
    {
        // Every monster prefab gets what online play needs: a NetworkObject, a NetworkTransform for where it is,
        // another on its body (the squash, hop and lunge are the body moving and scaling), and NetMonster.
        // Solo play ignores all of it. FishNet adds saved prefabs with a NetworkObject to its spawnable list.
        static void NetworkMonsters()
        {
            int changed = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset == null || asset.GetComponent<Monster>() == null) continue;
                var root = PrefabUtility.LoadPrefabContents(path);
                var monster = root.GetComponent<Monster>();
                var body = monster.body != null ? monster.body : root.transform.GetChild(0);
                bool dirty = false;
                var nob = root.GetComponent<NetworkObject>();
                if (nob == null) { nob = root.AddComponent<NetworkObject>(); dirty = true; }
                // not networked by default: solo play uses the same prefab (FishNet would hide an unspawned
                // networked object); the server's Spawn() still makes its copies networked
                if (nob.GetIsNetworked()) { nob.SetIsNetworked(false); dirty = true; }
                if (root.GetComponent<NetworkTransform>() == null) { root.AddComponent<NetworkTransform>(); dirty = true; }
                if (body.GetComponent<NetworkTransform>() == null) { body.gameObject.AddComponent<NetworkTransform>(); dirty = true; }
                if (root.GetComponent<NetMonster>() == null) { root.AddComponent<NetMonster>(); dirty = true; }
                if (dirty)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    changed++;
                }
                PrefabUtility.UnloadPrefabContents(root);
            }
            Debug.Log($"[World] networked {changed} monster prefabs");
        }
    }
}
