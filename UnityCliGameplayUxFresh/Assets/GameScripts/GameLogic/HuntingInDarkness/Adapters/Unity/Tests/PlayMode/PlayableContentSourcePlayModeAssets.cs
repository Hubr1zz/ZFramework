using System;
using HuntingInDarkness.Bootstrap;
using UnityEngine;

namespace HuntingInDarkness.Adapter.PlayModeTests
{
    internal static class PlayableContentSourcePlayModeAssets
    {
        private const string ManifestPath = "Assets/AssetRaw/Configs/HuntingInDarkness/HuntingInDarknessContentSources.asset";

        public static PlayableContentSourceManifest LoadManifest() => LoadAsset<PlayableContentSourceManifest>(ManifestPath);

        public static PlayableContentSourceBundle LoadBundle()
        {
            PlayableContentSourceManifest manifest = LoadManifest();
            if (manifest == null || !manifest.TryCreateBundle(out PlayableContentSourceBundle bundle, out _)) return null;
            return bundle;
        }

        private static T LoadAsset<T>(string path) where T : UnityEngine.Object
        {
            Type assetDatabaseType = Type.GetType("UnityEditor.AssetDatabase, UnityEditor");
            if (assetDatabaseType == null) return null;
            var method = assetDatabaseType.GetMethod("LoadAssetAtPath", new[] { typeof(string) });
            if (method == null) return null;
            return method.MakeGenericMethod(typeof(T)).Invoke(null, new object[] { path }) as T;
        }
    }
}
