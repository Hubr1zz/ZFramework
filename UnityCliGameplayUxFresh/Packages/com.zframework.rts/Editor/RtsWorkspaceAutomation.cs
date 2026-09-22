using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditorInternal;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace ZFramework.RTS.Editor
{
    [Serializable] internal sealed class RtsDummyEntry { public string key, kind, dummyAsset, productionAsset, notes; }
    [Serializable] internal sealed class RtsDummyManifest { public int schemaVersion = 1; public List<RtsDummyEntry> entries = new List<RtsDummyEntry>(); }

    internal static class RtsDummySandbox
    {
        internal const string AssetRootBase = "Assets/RTSDummy";
        internal static string AssetRoot => AssetRootBase + "/" + RtsSessionCatalog.SanitizeId(RtsProjectSettings.instance.ActiveSessionId);
        internal static string ManifestPath => RtsSessionCatalog.AssetMappingRelativePath;

        internal static RtsDummyManifest Load()
        {
            string path = Absolute(ManifestPath);
            if (!File.Exists(path)) return new RtsDummyManifest();
            return JsonUtility.FromJson<RtsDummyManifest>(File.ReadAllText(path)) ?? new RtsDummyManifest();
        }

        internal static void Save(RtsDummyManifest manifest)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Absolute(ManifestPath)) ?? string.Empty);
            File.WriteAllText(Absolute(ManifestPath), JsonUtility.ToJson(manifest, true));
        }

        internal static List<string> FindUnresolved()
        {
            return Load().entries.Where(x => x != null && !string.IsNullOrWhiteSpace(x.dummyAsset) &&
                (string.IsNullOrWhiteSpace(x.productionAsset) || AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(x.productionAsset) == null))
                .Select(x => $"Dummy '{x.key}' 尚未映射正式资产 ({x.dummyAsset})").ToList();
        }

        internal static List<string> FindBuildBlockers()
        {
            var blockers = FindUnresolved();
            if (AssetDatabase.IsValidFolder(AssetRoot) && AssetDatabase.FindAssets(string.Empty, new[] { AssetRoot }).Length > 0)
                blockers.Add("Dummy 沙箱仍含资产；正式化映射完成后请替换引用并移除 " + AssetRoot);
            return blockers;
        }

        internal static void ValidateBuildOrThrow()
        {
            var unresolved = new List<string>();
            foreach (RtsSessionInfo session in RtsSessionCatalog.ReadAll())
            {
                if (!File.Exists(session.AssetMappingPath)) continue;
                RtsDummyManifest manifest = JsonUtility.FromJson<RtsDummyManifest>(File.ReadAllText(session.AssetMappingPath)) ?? new RtsDummyManifest();
                unresolved.AddRange(manifest.entries.Where(x => x != null && !string.IsNullOrWhiteSpace(x.dummyAsset) &&
                    (string.IsNullOrWhiteSpace(x.productionAsset) || AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(x.productionAsset) == null))
                    .Select(x => $"Session '{session.Id}' 的资产键 '{x.key}' 尚未映射正式资产。"));
            }
            string[] buildScenes = EditorBuildSettings.scenes.Where(x => x.enabled).Select(x => x.path).ToArray();
            foreach (string dependency in AssetDatabase.GetDependencies(buildScenes, true))
                if (dependency.StartsWith(AssetRootBase + "/", StringComparison.OrdinalIgnoreCase))
                    unresolved.Add("正式场景引用 Dummy 资产: " + dependency);
            if (unresolved.Count > 0) throw new BuildFailedException("RTS Dummy 门禁失败:\n" + string.Join("\n", unresolved));
        }

        internal static void OpenMapping()
        {
            if (!File.Exists(Absolute(ManifestPath))) Save(new RtsDummyManifest());
            InternalEditorUtility.OpenFileAtLineExternal(Absolute(ManifestPath), 1);
        }

        private static string Absolute(string path) => Path.Combine(RtsProjectSettings.instance.ProjectRoot, path);
    }

    [Serializable] internal sealed class RtsWorkspaceData
    {
        public int schemaVersion = 1;
        public string generatedUtc, activeCompileUnit, activeSession, sessionDescriptor, reuseAnalysis, launchProfile,
            rtsTestEntryScriptId, dummyManifest, runtimeStatus;
        public string[] sessions, sessionDependencies, sourceRoots, referenceAssemblies, baseModules, capabilities, allowedTasks, tests;
    }

    [InitializeOnLoad]
    internal static class RtsWorkspaceManifest
    {
        internal const string PathName = "RTSWorkspace/rts-workspace.json";
        static RtsWorkspaceManifest() => EditorApplication.delayCall += Write;
        internal static void Write() => Write(0);

        private static void Write(int attempt)
        {
            try
            {
                var settings = RtsProjectSettings.instance;
                RtsSessionInfo activeSession = RtsSessionCatalog.Active;
                var data = new RtsWorkspaceData
                {
                    generatedUtc = DateTime.UtcNow.ToString("O"), activeCompileUnit = settings.ActiveCompileUnitName,
                    activeSession = settings.ActiveSessionId,
                    sessions = RtsSessionCatalog.ReadAll().Select(session => session.Id).ToArray(),
                    sessionDescriptor = activeSession == null ? string.Empty : Relative(activeSession.DescriptorPath),
                    reuseAnalysis = activeSession == null ? string.Empty : Relative(activeSession.ReuseAnalysisPath),
                    launchProfile = activeSession?.Descriptor.launchProfile.ToString() ?? string.Empty,
                    rtsTestEntryScriptId = settings.RtsTestEntryScriptId,
                    baseModules = activeSession?.Descriptor.baseModules ?? Array.Empty<string>(),
                    sessionDependencies = activeSession?.Descriptor.sessionDependencies ?? Array.Empty<string>(),
                    sourceRoots = settings.ResolveSourceRoots().Select(Relative).ToArray(), referenceAssemblies = settings.ResolveReferenceAssemblies().Select(Relative).ToArray(),
                    dummyManifest = RtsDummySandbox.ManifestPath,
                    runtimeStatus = RtsRuntimeStatus.RelativePath,
                    capabilities = new[] { "IRtsWorldServiceV1", "IRtsTargetQueryV1", "IRtsDamageServiceV1", "IRtsProjectileServiceV1", "IRtsEffectServiceV1", "IRtsObjectPoolV1", "IRtsAnimationServiceV1", "IRtsAudioServiceV1", "IRtsTimerServiceV1" },
                    allowedTasks = new[] { "compile", "validate-runtime-data", "restart-scene", "validate-production" },
                    tests = new[] { "dotnet run --project Packages/com.zframework.rts/Tools~/KernelSmokeTests", "dotnet build Packages/com.zframework.rts/Tools~/ProductionCompileSmoke" }
                };
                string path = System.IO.Path.Combine(settings.ProjectRoot, PathName); Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
                File.WriteAllText(path, JsonUtility.ToJson(data, true));
            }
            catch (IOException) when (attempt < 3)
            {
                EditorApplication.delayCall += () => Write(attempt + 1);
            }
            catch (Exception exception) { Debug.LogWarning("[RTS] Workspace manifest: " + exception.Message); }
        }

        private static string Relative(string path) => Path.GetRelativePath(RtsProjectSettings.instance.ProjectRoot, path).Replace('\\', '/');
    }

    [Serializable] internal sealed class RtsAgentTask { public string action; public bool requiresConfirmation; }
    [Serializable] internal sealed class RtsAgentQueue { public int schemaVersion = 1; public List<RtsAgentTask> tasks = new List<RtsAgentTask>(); }
    [Serializable] internal sealed class RtsAgentReport { public string startedUtc, finishedUtc, status, lastAction, healthyGeneration; public double compileP95Ms; public List<string> logs = new List<string>(); }

    internal static class RtsAgentTaskRunner
    {
        internal static string QueuePath => RtsSessionCatalog.TaskQueueRelativePath;
        private static RtsAgentQueue _queue; private static int _index; private static double _started; private static bool _running, _waiting;
        private static RtsAgentReport _report;
        internal static bool IsRunning => _running;

        internal static void CreateExample()
        {
            var queue = new RtsAgentQueue(); queue.tasks.Add(new RtsAgentTask { action = "compile" });
            queue.tasks.Add(new RtsAgentTask { action = "validate-runtime-data" });
            Write(queue); InternalEditorUtility.OpenFileAtLineExternal(Absolute(QueuePath), 1);
        }
        internal static void Run()
        {
            if (_running) return;
            string path = Absolute(QueuePath); if (!File.Exists(path)) { Debug.LogError("[RTS] Task queue missing: " + path); return; }
            _queue = JsonUtility.FromJson<RtsAgentQueue>(File.ReadAllText(path));
            int count = _queue?.tasks?.Count ?? 0;
            if (count == 0 || count > RtsProjectSettings.instance.MaxAgentTaskSteps) { Debug.LogError("[RTS] Task count is empty or exceeds configured bound."); return; }
            if (_queue.tasks.Any(x => x.requiresConfirmation) && !EditorUtility.DisplayDialog("RTS Agent Tasks", "队列包含人工确认点，允许本轮执行？", "执行", "取消")) return;
            _index = 0; _started = EditorApplication.timeSinceStartup; _running = true; _waiting = false;
            _report = new RtsAgentReport { startedUtc = DateTime.UtcNow.ToString("O"), status = "running" };
            Application.logMessageReceived += CaptureLog; EditorApplication.update += Pump;
        }
        internal static void Cancel() { if (_running) Complete("cancelled"); else Stop(); }
        private static void Pump()
        {
            if (!_running) return;
            if (EditorApplication.timeSinceStartup - _started > RtsProjectSettings.instance.AgentTaskTimeoutSeconds) { Fail("任务超时"); return; }
            if (RtsCompilationService.IsCompiling || _waiting) return;
            if (_index >= _queue.tasks.Count) { Complete("completed"); Debug.Log("[RTS] Agent task queue completed."); return; }
            string action = _queue.tasks[_index++].action;
            _report.lastAction = action;
            switch (action)
            {
                case "compile": RtsCompilationService.RequestCompile(result => { if (!result.Succeeded) Fail(result.Diagnostics); else if (EditorApplication.isPlaying) ScriptAssemblyLoader.TryLoadCompiledAssembly(result.AssemblyPath); }); break;
                case "restart-scene":
                    if (!EditorApplication.isPlaying) { Fail("无法重启场景：当前不在 Play Mode"); break; }
                    _waiting = true;
                    if (!ModuleSystem.GetModule<IScriptRuntimeModule>().RestartCurrentScene(completed: (ok, error) => { _waiting = false; if (!ok) Fail(error); }))
                    { _waiting = false; Fail("无法重启场景"); }
                    break;
                case "validate-runtime-data":
                    if (!RtsRuntimeStatus.TryValidateRuntimeData(out string validationSummary)) Fail(validationSummary);
                    else _report.logs.Add("Data validation: " + validationSummary);
                    break;
                case "validate-production": try { RtsZeroBuildGuard.ValidateOrThrow(); } catch (Exception e) { Fail(e.Message); } break;
                default: Fail("不允许的任务: " + action); break;
            }
        }
        private static void CaptureLog(string condition, string stack, LogType type)
        { if (_report != null && _report.logs.Count < 200) _report.logs.Add(type + ": " + condition); }
        private static void Complete(string status)
        {
            if (_report != null)
            {
                _report.status = status; _report.finishedUtc = DateTime.UtcNow.ToString("O"); _report.compileP95Ms = RtsCompilationService.P95Milliseconds;
                _report.healthyGeneration = ScriptAssemblyLoader.LastHealthyGeneration;
                Directory.CreateDirectory(Absolute(RtsSessionCatalog.ArtifactsRelativePath));
                File.WriteAllText(Absolute(RtsSessionCatalog.ArtifactsRelativePath + "/run-report.json"), JsonUtility.ToJson(_report, true));
            }
            Stop();
        }
        private static void Stop() { _running = false; _waiting = false; EditorApplication.update -= Pump; Application.logMessageReceived -= CaptureLog; }
        private static void Fail(string error) { if (_report != null) _report.logs.Add("Error: " + error); Complete("failed"); Debug.LogError("[RTS] Agent queue stopped: " + error); }
        private static void Write(RtsAgentQueue queue) { Directory.CreateDirectory(Absolute("RTSWorkspace")); File.WriteAllText(Absolute(QueuePath), JsonUtility.ToJson(queue, true)); }
        private static string Absolute(string value) => System.IO.Path.Combine(RtsProjectSettings.instance.ProjectRoot, value);
    }
}
