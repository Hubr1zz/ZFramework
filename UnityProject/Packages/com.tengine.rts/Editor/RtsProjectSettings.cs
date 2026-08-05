using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace TEngine.RTS.Editor
{
    internal enum RtsLaunchTarget
    {
        Normal,
        RtsTest
    }

    [Serializable]
    internal sealed class RtsCompileUnitSettings
    {
        [SerializeField] internal string name = "Default";
        [SerializeField] internal List<string> sourceRoots = new List<string>();
        [SerializeField] internal List<string> referenceAssemblies = new List<string>();
        [SerializeField] internal string outputDirectory = "Library/TEngineRTS/Compiled";
    }

    [FilePath("ProjectSettings/TEngineRtsSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    internal sealed class RtsProjectSettings : ScriptableSingleton<RtsProjectSettings>
    {
        [SerializeField] private List<RtsCompileUnitSettings> compileUnits = new List<RtsCompileUnitSettings>
        {
            new RtsCompileUnitSettings()
        };
        [SerializeField] private int activeCompileUnit;
        [SerializeField] private string mainScene = "Assets/Scenes/main.unity";
        [SerializeField] private RtsLaunchTarget launchTarget = RtsLaunchTarget.RtsTest;
        [SerializeField] private string rtsTestEntryScriptId = "game.session-entry";
        [SerializeField] private string formalizationSessionName = "DefaultSession";
        [SerializeField] private ScriptStateMigrationPolicy stateMigrationPolicy = ScriptStateMigrationPolicy.PreserveWhenCompatible;
        [SerializeField] private bool useCompilerDaemon = true;
        [SerializeField, Range(1, 50)] private int maxAgentTaskSteps = 12;
        [SerializeField, Range(10, 1800)] private int agentTaskTimeoutSeconds = 300;

        internal IReadOnlyList<RtsCompileUnitSettings> CompileUnits
        {
            get { EnsureCompileUnit(); return compileUnits; }
        }
        internal int ActiveCompileUnit
        {
            get { EnsureCompileUnit(); return Math.Max(0, Math.Min(activeCompileUnit, compileUnits.Count - 1)); }
            set { EnsureCompileUnit(); activeCompileUnit = Math.Max(0, Math.Min(value, compileUnits.Count - 1)); Save(true); }
        }
        internal string ActiveCompileUnitName => ActiveUnit.name;
        internal string MainScene => mainScene;
        internal RtsLaunchTarget LaunchTarget
        {
            get => launchTarget;
            set { launchTarget = value; Save(true); }
        }
        internal ScriptStateMigrationPolicy StateMigrationPolicy => stateMigrationPolicy;
        internal bool UseCompilerDaemon => useCompilerDaemon;
        internal int MaxAgentTaskSteps => maxAgentTaskSteps;
        internal int AgentTaskTimeoutSeconds => agentTaskTimeoutSeconds;

        internal string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        private RtsCompileUnitSettings ActiveUnit
        {
            get
            {
                EnsureCompileUnit();
                return compileUnits[ActiveCompileUnit];
            }
        }

        private void EnsureCompileUnit()
        {
            if (compileUnits == null) compileUnits = new List<RtsCompileUnitSettings>();
            if (compileUnits.Count == 0) compileUnits.Add(new RtsCompileUnitSettings());
        }

        internal IEnumerable<string> ResolveSourceRoots()
        {
            foreach (string path in RtsSessionCatalog.ResolveActiveSourceRoots()) yield return path;
            foreach (string path in ActiveUnit.sourceRoots)
            {
                if (string.IsNullOrWhiteSpace(path)) continue;
                string normalized = path.Replace('\\', '/').TrimEnd('/');
                if (normalized.Equals("Packages/com.tengine.rts/UserScripts~", StringComparison.OrdinalIgnoreCase)) continue;
                yield return ResolveProjectPath(path);
            }
        }

        internal IEnumerable<string> ResolveReferenceAssemblies()
        {
            yield return Path.Combine(ProjectRoot, "Library", "ScriptAssemblies", "TEngine.RTS.Contracts.dll");
            foreach (string path in RtsSessionCatalog.ResolveActiveReferences()) yield return path;
            foreach (string path in ActiveUnit.referenceAssemblies)
                if (!string.IsNullOrWhiteSpace(path)) yield return ResolveProjectPath(path);
        }

        internal string ResolveOutputDirectory()
        {
            // Older settings wrote generated assemblies below Packages. Keep every hot-update
            // artifact outside AssetDatabase even when such a serialized setting still exists.
            string configured = ActiveUnit.outputDirectory ?? string.Empty;
            if (configured.Replace('\\', '/').StartsWith("Packages/com.tengine.rts/Temp~/", StringComparison.OrdinalIgnoreCase))
                configured = "Library/TEngineRTS/Compiled";
            string baseOutput = ResolveProjectPath(configured);
            return Path.Combine(baseOutput, "Sessions", RtsSessionCatalog.SanitizeId(ActiveSessionId));
        }
        internal string ActiveSessionId
        {
            get => string.IsNullOrWhiteSpace(formalizationSessionName) ? "DefaultSession" : formalizationSessionName.Trim();
            set { formalizationSessionName = string.IsNullOrWhiteSpace(value) ? "DefaultSession" : value.Trim(); Save(true); }
        }
        internal string FormalizationSessionName
        {
            get => ActiveSessionId;
            set => ActiveSessionId = value;
        }
        internal string LegacyRtsTestEntryScriptId => string.IsNullOrWhiteSpace(rtsTestEntryScriptId) ? string.Empty : rtsTestEntryScriptId.Trim();
        internal string RtsTestEntryScriptId => RtsSessionCatalog.Active?.Descriptor.entryScriptId ?? LegacyRtsTestEntryScriptId;
        internal string ResolveMainScene() => ResolveProjectPath(mainScene);

        internal void SaveSettings() => Save(true);

        private string ResolveProjectPath(string path) =>
            Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(ProjectRoot, path));
    }

    internal static class RtsSettingsProvider
    {
        [SettingsProvider]
        private static SettingsProvider CreateProvider()
        {
            return new SettingsProvider("Project/TEngine RTS", SettingsScope.Project)
            {
                label = "TEngine RTS",
                guiHandler = _ => DrawSettings(),
                keywords = new HashSet<string>(new[] { "TEngine", "RTS", "Roslyn", "Workspace" })
            };
        }

        private static void DrawSettings()
        {
            RtsProjectSettings settings = RtsProjectSettings.instance;
            var serialized = new SerializedObject(settings);
            serialized.Update();
            EditorGUILayout.HelpBox(
                "Session Sources are resolved from RTSWorkspace/Sessions. Compile Unit source roots are optional shared/tooling additions; references remain an explicit whitelist.",
                MessageType.Info);
            EditorGUILayout.PropertyField(serialized.FindProperty("compileUnits"), true);
            EditorGUILayout.PropertyField(serialized.FindProperty("activeCompileUnit"));
            EditorGUILayout.PropertyField(serialized.FindProperty("mainScene"));
            EditorGUILayout.PropertyField(serialized.FindProperty("launchTarget"));
            EditorGUILayout.PropertyField(serialized.FindProperty("rtsTestEntryScriptId"), new GUIContent("Legacy Entry Script Id"));
            EditorGUILayout.PropertyField(serialized.FindProperty("formalizationSessionName"), new GUIContent("Active Session Id"));
            EditorGUILayout.PropertyField(serialized.FindProperty("stateMigrationPolicy"));
            EditorGUILayout.PropertyField(serialized.FindProperty("useCompilerDaemon"));
            EditorGUILayout.PropertyField(serialized.FindProperty("maxAgentTaskSteps"));
            EditorGUILayout.PropertyField(serialized.FindProperty("agentTaskTimeoutSeconds"));
            if (serialized.ApplyModifiedProperties())
            {
                settings.SaveSettings();
                RtsScriptWatcher.RefreshWatchers();
                RtsWorkspaceManifest.Write();
            }
        }
    }
}
