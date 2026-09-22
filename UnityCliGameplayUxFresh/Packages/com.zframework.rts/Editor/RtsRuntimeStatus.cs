using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZFramework.RTS.Editor
{
    [Serializable]
    internal sealed class RtsRuntimeStatusData
    {
        public int schemaVersion = 1;
        public string updatedUtc;
        public string activeSession;
        public bool isPlaying;
        public string activeScene;
        public int editorDomainGeneration;
        public int sceneLoadCount;
        public bool lastCompileSucceeded;
        public double lastCompileMs;
        public double compileP95Ms;
        public double lastApplyMs;
        public string healthyGeneration;
        public int activeInstances;
        public int loadedGenerationCount;
        public long loadedAssemblyBytes;
        public bool maintenanceReloadSuggested;
        public string lastError;
    }

    [InitializeOnLoad]
    internal static class RtsRuntimeStatus
    {
        internal static string RelativePath => RtsSessionCatalog.RuntimeStatusRelativePath;
        private const string DOMAIN_KEY = "ZFramework.RTS.Status.DomainGeneration";
        private const string SCENE_KEY = "ZFramework.RTS.Status.SceneLoadCount";
        private const int GENERATION_SOFT_LIMIT = 20;
        private const long BYTE_SOFT_LIMIT = 64L * 1024L * 1024L;
        private static readonly Queue<double> COMPILE_SAMPLES = new Queue<double>();
        private static readonly RtsRuntimeStatusData DATA = new RtsRuntimeStatusData();

        static RtsRuntimeStatus()
        {
            DATA.editorDomainGeneration = SessionState.GetInt(DOMAIN_KEY, 0) + 1;
            SessionState.SetInt(DOMAIN_KEY, DATA.editorDomainGeneration);
            DATA.sceneLoadCount = SessionState.GetInt(SCENE_KEY, 0);
            SceneManager.sceneLoaded += OnSceneLoaded;
            EditorApplication.playModeStateChanged += _ => Write();
            EditorApplication.quitting += () => Write(false);
            EditorApplication.delayCall += Write;
        }

        internal static void RecordCompile(in RtsCompileResult result)
        {
            DATA.lastCompileSucceeded = result.Succeeded;
            DATA.lastCompileMs = result.ElapsedMilliseconds;
            if (result.ElapsedMilliseconds > 0d)
            {
                COMPILE_SAMPLES.Enqueue(result.ElapsedMilliseconds);
                while (COMPILE_SAMPLES.Count > 50) COMPILE_SAMPLES.Dequeue();
            }
            DATA.lastError = result.Succeeded ? string.Empty : result.Diagnostics;
            Write();
        }

        internal static void RecordApply(bool succeeded, double elapsedMilliseconds, string error = null)
        {
            DATA.lastApplyMs = elapsedMilliseconds;
            DATA.lastError = succeeded ? string.Empty : (error ?? "Unknown provider apply failure.");
            Write();
        }

        internal static bool TryValidateRuntimeData(out string summary)
        {
            Write();
            var failures = new List<string>();
            if (!DATA.lastCompileSucceeded) failures.Add("the latest external compile did not succeed");
            if (!string.IsNullOrWhiteSpace(DATA.lastError)) failures.Add("runtime error: " + DATA.lastError);
            if (EditorApplication.isPlaying)
            {
                if (string.IsNullOrWhiteSpace(DATA.healthyGeneration)) failures.Add("no healthy generation is active");
                if (DATA.loadedGenerationCount <= 0) failures.Add("no dynamic generation has been loaded");
                if (DATA.activeInstances <= 0) failures.Add("the entry script has no active instance");
            }
            summary = failures.Count == 0
                ? $"session={DATA.activeSession}; compileMs={DATA.lastCompileMs:F1}; applyMs={DATA.lastApplyMs:F2}; healthy={DATA.healthyGeneration}; instances={DATA.activeInstances}; generations={DATA.loadedGenerationCount}"
                : string.Join("; ", failures);
            return failures.Count == 0;
        }

        internal static void Write() => Write(null);

        private static void Write(bool? isPlayingOverride)
        {
            try
            {
                DATA.updatedUtc = DateTime.UtcNow.ToString("O");
                DATA.activeSession = RtsProjectSettings.instance.ActiveSessionId;
                DATA.isPlaying = isPlayingOverride ?? EditorApplication.isPlaying;
                DATA.activeScene = SceneManager.GetActiveScene().path;
                DATA.healthyGeneration = ScriptAssemblyLoader.LastHealthyGeneration;
                DATA.loadedGenerationCount = ScriptAssemblyLoader.LoadedGenerationCount;
                DATA.loadedAssemblyBytes = ScriptAssemblyLoader.LoadedAssemblyBytes;
                DATA.maintenanceReloadSuggested = DATA.loadedGenerationCount >= GENERATION_SOFT_LIMIT || DATA.loadedAssemblyBytes >= BYTE_SOFT_LIMIT;
                DATA.compileP95Ms = Percentile95();
                try { DATA.activeInstances = ModuleSystem.GetModule<IScriptRuntimeModule>().ActiveInstanceCount; }
                catch { DATA.activeInstances = 0; }
                string path = Path.Combine(RtsProjectSettings.instance.ProjectRoot, RelativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(path) ?? string.Empty);
                string temporary = path + ".tmp";
                File.WriteAllText(temporary, JsonUtility.ToJson(DATA, true));
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
            }
            catch (Exception exception) { Debug.LogWarning("[RTS] Runtime status: " + exception.Message); }
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            DATA.sceneLoadCount++;
            SessionState.SetInt(SCENE_KEY, DATA.sceneLoadCount);
            Write();
        }

        private static double Percentile95()
        {
            if (COMPILE_SAMPLES.Count == 0) return RtsCompilationService.P95Milliseconds;
            double[] samples = COMPILE_SAMPLES.ToArray();
            Array.Sort(samples);
            return samples[Math.Min(samples.Length - 1, (int)Math.Ceiling(samples.Length * .95d) - 1)];
        }
    }
}
