using System.Reflection;
using HuntingInDarkness.Bootstrap;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HuntingInDarkness.Adapter.Tests
{
    internal static class PlayableContentSourceTestAssets
    {
        private const string ManifestPath = "Assets/AssetRaw/Configs/HuntingInDarkness/HuntingInDarknessContentSources.asset";
        private static readonly FieldInfo settingsField = typeof(PlayableContentSourceManifest).GetField("settings", BindingFlags.Instance | BindingFlags.NonPublic);

        public static PlayableContentSourceBundle LoadBundle(PlayableBootstrapSettings settings = null)
        {
            PlayableContentSourceManifest template = AssetDatabase.LoadAssetAtPath<PlayableContentSourceManifest>(ManifestPath);
            Assert.That(template, Is.Not.Null);
            if (settings == null)
            {
                Assert.That(template.TryCreateBundle(out PlayableContentSourceBundle bundle, out PlayableContentDiagnosticReport report), Is.True, report.ToString());
                return bundle;
            }

            PlayableContentSourceManifest clone = Object.Instantiate(template);
            clone.hideFlags = HideFlags.HideAndDontSave;
            settingsField.SetValue(clone, settings);
            bool valid = clone.TryCreateBundle(out PlayableContentSourceBundle clonedBundle, out PlayableContentDiagnosticReport clonedReport);
            Object.DestroyImmediate(clone);
            Assert.That(valid, Is.True, clonedReport.ToString());
            return clonedBundle;
        }
    }
}
