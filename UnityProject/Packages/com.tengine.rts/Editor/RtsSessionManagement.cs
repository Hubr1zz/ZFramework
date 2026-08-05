using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace TEngine.RTS.Editor
{
    internal enum RtsSessionLaunchProfile
    {
        Sandbox,
        InContext
    }

    [Serializable]
    internal sealed class RtsSessionDescriptor
    {
        public int schemaVersion = 1;
        public string id = "DefaultSession";
        public string displayName = "Default Session";
        public string entryScriptId = string.Empty;
        public RtsSessionLaunchProfile launchProfile = RtsSessionLaunchProfile.Sandbox;
        public string startupScene = string.Empty;
        public string activationProcedure = "Procedure.ProcedureStartGame";
        public string activationScene = string.Empty;
        public int startupTimeoutSeconds = 300;
        public string createdUtc = string.Empty;
        public string baseRevision = string.Empty;
        public string[] baseModules = Array.Empty<string>();
        public string[] sessionDependencies = Array.Empty<string>();
        public string[] referenceAssemblies = Array.Empty<string>();
    }

    internal sealed class RtsSessionInfo
    {
        internal RtsSessionInfo(RtsSessionDescriptor descriptor, string rootPath)
        {
            Descriptor = descriptor;
            RootPath = rootPath;
        }

        internal RtsSessionDescriptor Descriptor { get; }
        internal string RootPath { get; }
        internal string Id => Descriptor.id;
        internal string DisplayName => string.IsNullOrWhiteSpace(Descriptor.displayName) ? Id : Descriptor.displayName;
        internal string SourceRoot => Path.Combine(RootPath, "Sources");
        internal string DescriptorPath => Path.Combine(RootPath, "session.json");
        internal string AssetMappingPath => Path.Combine(RootPath, "asset-map.json");
        internal string ReuseAnalysisPath => Path.Combine(RootPath, "reuse-analysis.md");
        internal string RuntimeStatusPath => Path.Combine(RootPath, "runtime-status.json");
        internal string ArtifactsPath => Path.Combine(RootPath, "artifacts");
        internal string TaskQueuePath => Path.Combine(RootPath, "task-queue.json");
    }

    [InitializeOnLoad]
    internal static class RtsSessionCatalog
    {
        internal const string SessionsRootRelativePath = "RTSWorkspace/Sessions";
        private const string DEFAULT_SESSION = "DefaultSession";

        static RtsSessionCatalog() => EditorApplication.delayCall += EnsureActiveSession;

        internal static IReadOnlyList<RtsSessionInfo> ReadAll()
        {
            string root = Absolute(SessionsRootRelativePath);
            if (!Directory.Exists(root)) return Array.Empty<RtsSessionInfo>();
            var sessions = new List<RtsSessionInfo>();
            foreach (string directory in Directory.GetDirectories(root, "*", SearchOption.TopDirectoryOnly))
            {
                string descriptorPath = Path.Combine(directory, "session.json");
                if (!File.Exists(descriptorPath)) continue;
                try
                {
                    RtsSessionDescriptor descriptor = JsonUtility.FromJson<RtsSessionDescriptor>(File.ReadAllText(descriptorPath));
                    if (descriptor == null || string.IsNullOrWhiteSpace(descriptor.id)) continue;
                    sessions.Add(new RtsSessionInfo(descriptor, directory));
                }
                catch (Exception exception)
                {
                    Debug.LogWarning($"[RTS] Invalid session descriptor '{descriptorPath}': {exception.Message}");
                }
            }
            return sessions.OrderBy(session => session.DisplayName, StringComparer.OrdinalIgnoreCase).ToArray();
        }

        internal static RtsSessionInfo Active
        {
            get
            {
                IReadOnlyList<RtsSessionInfo> sessions = ReadAll();
                string activeId = RtsProjectSettings.instance.ActiveSessionId;
                RtsSessionInfo active = sessions.FirstOrDefault(session =>
                    session.Id.Equals(activeId, StringComparison.OrdinalIgnoreCase));
                if (active != null) return active;
                EnsureActiveSession();
                sessions = ReadAll();
                return sessions.FirstOrDefault(session => session.Id.Equals(activeId, StringComparison.OrdinalIgnoreCase))
                       ?? sessions.FirstOrDefault();
            }
        }

        internal static void EnsureActiveSession()
        {
            string requestedId = SanitizeId(RtsProjectSettings.instance.ActiveSessionId);
            if (string.IsNullOrEmpty(requestedId)) requestedId = DEFAULT_SESSION;
            string root = Path.Combine(Absolute(SessionsRootRelativePath), requestedId);
            if (!File.Exists(Path.Combine(root, "session.json")))
                Create(requestedId, requestedId, RtsProjectSettings.instance.LegacyRtsTestEntryScriptId, RtsSessionLaunchProfile.Sandbox, select: true);
            else
            {
                try
                {
                    EnsureSessionFiles(new RtsSessionInfo(ReadDescriptor(root), root));
                }
                catch (Exception exception)
                {
                    Debug.LogError($"[RTS] Active session '{requestedId}' is invalid and was not modified: {exception.Message}");
                    RtsSessionInfo fallback = ReadAll().FirstOrDefault();
                    if (fallback == null) return;
                    RtsProjectSettings.instance.ActiveSessionId = fallback.Id;
                }
            }
        }

        internal static RtsSessionInfo Create(string id, string displayName, string entryScriptId,
            RtsSessionLaunchProfile launchProfile, bool select)
        {
            string safeId = SanitizeId(id);
            if (string.IsNullOrEmpty(safeId)) throw new ArgumentException("Session Id is empty after sanitization.", nameof(id));
            if (!TryValidateScriptId(entryScriptId, out string scriptIdError))
                throw new ArgumentException(scriptIdError, nameof(entryScriptId));
            string root = Path.Combine(Absolute(SessionsRootRelativePath), safeId);
            string descriptorPath = Path.Combine(root, "session.json");
            if (File.Exists(descriptorPath)) throw new InvalidOperationException("Session already exists: " + safeId);
            Directory.CreateDirectory(root);
            var descriptor = new RtsSessionDescriptor
            {
                id = safeId,
                displayName = string.IsNullOrWhiteSpace(displayName) ? safeId : displayName.Trim(),
                entryScriptId = entryScriptId?.Trim() ?? string.Empty,
                launchProfile = launchProfile,
                createdUtc = DateTime.UtcNow.ToString("O")
            };
            var session = new RtsSessionInfo(descriptor, root);
            Save(session);
            EnsureSessionFiles(session);
            if (select) Select(safeId);
            return session;
        }

        internal static void Save(RtsSessionInfo session)
        {
            if (!TryValidateScriptId(session.Descriptor.entryScriptId, out string scriptIdError))
                throw new InvalidDataException(scriptIdError);
            Directory.CreateDirectory(session.RootPath);
            File.WriteAllText(session.DescriptorPath, JsonUtility.ToJson(session.Descriptor, true));
        }

        internal static bool TryValidateScriptId(string value, out string error)
        {
            string scriptId = value?.Trim() ?? string.Empty;
            if (scriptId.Length == 0 || scriptId.Length > 128)
            {
                error = "Entry ScriptId must contain 1-128 characters.";
                return false;
            }
            if (scriptId[0] < 'a' || scriptId[0] > 'z')
            {
                error = "Entry ScriptId must start with a lowercase letter.";
                return false;
            }
            foreach (char character in scriptId)
            {
                bool allowed = character >= 'a' && character <= 'z' || character >= '0' && character <= '9' ||
                               character == '.' || character == '-' || character == '_';
                if (allowed) continue;
                error = "Entry ScriptId may only contain lowercase letters, digits, '.', '-' and '_'.";
                return false;
            }
            error = string.Empty;
            return true;
        }

        internal static void Select(string id)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before switching RTS Session. Cross-Session hot switch is not enabled.");
            RtsSessionInfo session = ReadAll().FirstOrDefault(value => value.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
            if (session == null) throw new InvalidOperationException("Unknown RTS Session: " + id);
            RtsProjectSettings.instance.ActiveSessionId = session.Id;
            EnsureSessionFiles(session);
            RtsCompilationService.Cancel();
            RtsScriptWatcher.RefreshWatchers();
            RtsWorkspaceManifest.Write();
            RtsRuntimeStatus.Write();
        }

        internal static IEnumerable<string> ResolveActiveSourceRoots()
        {
            RtsSessionInfo active = Active;
            if (active == null) yield break;
            foreach (RtsSessionInfo session in ResolveDependencyClosure(active)) yield return session.SourceRoot;
        }

        internal static IEnumerable<string> ResolveActiveOwnedSourceRoots()
        {
            RtsSessionInfo active = Active;
            if (active != null) yield return active.SourceRoot;
        }

        internal static IEnumerable<string> ResolveActiveReferences()
        {
            RtsSessionInfo active = Active;
            if (active?.Descriptor.referenceAssemblies == null) yield break;
            foreach (string reference in active.Descriptor.referenceAssemblies)
                if (!string.IsNullOrWhiteSpace(reference)) yield return ResolveProjectPath(reference);
        }

        internal static string RuntimeStatusRelativePath => MakeRelative(Active?.RuntimeStatusPath, SessionsRootRelativePath + "/DefaultSession/runtime-status.json");
        internal static string AssetMappingRelativePath => MakeRelative(Active?.AssetMappingPath, SessionsRootRelativePath + "/DefaultSession/asset-map.json");
        internal static string ArtifactsRelativePath => MakeRelative(Active?.ArtifactsPath, SessionsRootRelativePath + "/DefaultSession/artifacts");
        internal static string TaskQueueRelativePath => MakeRelative(Active?.TaskQueuePath, SessionsRootRelativePath + "/DefaultSession/task-queue.json");

        internal static string SanitizeId(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            return new string(value.Trim().Select(character =>
                char.IsLetterOrDigit(character) || character == '_' || character == '-' ? character : '_').ToArray());
        }

        private static IReadOnlyList<RtsSessionInfo> ResolveDependencyClosure(RtsSessionInfo active)
        {
            var all = ReadAll().ToDictionary(session => session.Id, StringComparer.OrdinalIgnoreCase);
            var result = new List<RtsSessionInfo>();
            var visiting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            void Visit(RtsSessionInfo session)
            {
                if (visited.Contains(session.Id)) return;
                if (!visiting.Add(session.Id)) throw new InvalidOperationException("RTS Session dependency cycle: " + session.Id);
                foreach (string dependencyId in session.Descriptor.sessionDependencies ?? Array.Empty<string>())
                {
                    if (!all.TryGetValue(dependencyId, out RtsSessionInfo dependency))
                        throw new InvalidOperationException($"RTS Session '{session.Id}' depends on missing Session '{dependencyId}'.");
                    Visit(dependency);
                }
                visiting.Remove(session.Id);
                visited.Add(session.Id);
                result.Add(session);
            }
            Visit(active);
            return result;
        }

        private static void EnsureSessionFiles(RtsSessionInfo session)
        {
            Directory.CreateDirectory(session.SourceRoot);
            Directory.CreateDirectory(session.ArtifactsPath);
            if (!File.Exists(session.AssetMappingPath))
                File.WriteAllText(session.AssetMappingPath, "{\n  \"schemaVersion\": 1,\n  \"entries\": []\n}\n");
            if (!File.Exists(session.ReuseAnalysisPath))
                File.WriteAllText(session.ReuseAnalysisPath,
                    "# Reuse analysis\n\n- Production baseline: not analyzed\n- Reused Data/services: not analyzed\n- New session-owned code: not analyzed\n- Required capabilities: not analyzed\n");
            if (!File.Exists(session.TaskQueuePath))
                File.WriteAllText(session.TaskQueuePath,
                    "{\n  \"schemaVersion\": 1,\n  \"tasks\": [\n    { \"action\": \"compile\", \"requiresConfirmation\": false },\n    { \"action\": \"validate-runtime-data\", \"requiresConfirmation\": false }\n  ]\n}\n");
        }

        private static RtsSessionDescriptor ReadDescriptor(string root)
        {
            string path = Path.Combine(root, "session.json");
            RtsSessionDescriptor descriptor = JsonUtility.FromJson<RtsSessionDescriptor>(File.ReadAllText(path));
            if (descriptor == null || string.IsNullOrWhiteSpace(descriptor.id))
                throw new InvalidDataException("Descriptor must contain a non-empty id.");
            descriptor.baseModules ??= Array.Empty<string>();
            descriptor.sessionDependencies ??= Array.Empty<string>();
            descriptor.referenceAssemblies ??= Array.Empty<string>();
            return descriptor;
        }

        private static string MakeRelative(string absolutePath, string fallback)
        {
            if (string.IsNullOrWhiteSpace(absolutePath)) return fallback;
            return Path.GetRelativePath(RtsProjectSettings.instance.ProjectRoot, absolutePath).Replace('\\', '/');
        }

        private static string ResolveProjectPath(string path) => Path.GetFullPath(Path.IsPathRooted(path)
            ? path
            : Path.Combine(RtsProjectSettings.instance.ProjectRoot, path));

        private static string Absolute(string path) => Path.Combine(RtsProjectSettings.instance.ProjectRoot, path);
    }

    internal sealed class RtsSessionWizard : EditorWindow
    {
        private string _id = "NewSession";
        private string _displayName = "New Session";
        private string _entryScriptId = "game.new-session";
        private RtsSessionLaunchProfile _launchProfile = RtsSessionLaunchProfile.Sandbox;

        internal static void Open() => GetWindow<RtsSessionWizard>(true, "Create RTS Session").Show();

        private void OnGUI()
        {
            _id = EditorGUILayout.TextField("Session Id", _id);
            _displayName = EditorGUILayout.TextField("Display Name", _displayName);
            _entryScriptId = EditorGUILayout.TextField("Entry ScriptId", _entryScriptId);
            _launchProfile = (RtsSessionLaunchProfile)EditorGUILayout.EnumPopup("Launch Profile", _launchProfile);
            EditorGUILayout.HelpBox("Sandbox 使用固定 RTSTest；InContext 走正式主场景/Procedure，再挂载 Session 增量。", MessageType.Info);
            bool scriptIdValid = RtsSessionCatalog.TryValidateScriptId(_entryScriptId, out string scriptIdError);
            EditorGUILayout.HelpBox(scriptIdValid
                ? "Entry ScriptId 必须与入口 IScript 上的 [ScriptId(\"...\")] 完全一致，建议使用 product.feature-entry。"
                : scriptIdError, scriptIdValid ? MessageType.None : MessageType.Error);
            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(RtsSessionCatalog.SanitizeId(_id)) || !scriptIdValid))
            {
                if (!GUILayout.Button("Create and Select")) return;
                try
                {
                    RtsSessionInfo session = RtsSessionCatalog.Create(_id, _displayName, _entryScriptId, _launchProfile, select: true);
                    InternalEditorUtility.OpenFileAtLineExternal(session.ReuseAnalysisPath, 1);
                    Close();
                }
                catch (Exception exception) { EditorUtility.DisplayDialog("RTS Session", exception.Message, "确定"); }
            }
        }
    }
}
