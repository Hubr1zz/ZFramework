using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace ZFramework.RTS.Editor
{
    internal static class ScriptAssemblyLoader
    {
        private const int RESTART_STRESS_COUNT = 10;
        private static int _restartStressRemaining;
        private static DynamicAssemblyScriptProvider _healthyProvider;
        private static string _healthyPath = string.Empty;
        private static int _loadedGenerationCount;
        private static long _loadedAssemblyBytes;

        internal static int LoadedGenerationCount => _loadedGenerationCount;
        internal static long LoadedAssemblyBytes => _loadedAssemblyBytes;
        internal static string LastHealthyGeneration => _healthyProvider?.GenerationName ?? string.Empty;
        internal static string LastHealthyPath => _healthyPath;

        internal static void LoadCompiledAssembly()
        {
            string path = EditorUtility.OpenFilePanel("Load RTS assembly", string.Empty, "dll");
            if (string.IsNullOrEmpty(path)) return;
            TryLoadCompiledAssembly(path);
        }

        internal static void RequestCompileAndReload()
        {
            RtsCompilationService.RequestCompile(result =>
            {
                if (!result.Succeeded)
                {
                    Log.Error("[RTS] Compile failed ({0:F0} ms):\n{1}",
                        result.ElapsedMilliseconds, result.Diagnostics);
                    return;
                }
                Log.Info("[RTS] Compiled in {0:F0} ms: {1}", result.ElapsedMilliseconds, result.AssemblyPath);
                if (EditorApplication.isPlaying) TryLoadCompiledAssembly(result.AssemblyPath);
            });
        }

        internal static bool TryLoadCompiledAssembly(string path)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                byte[] assemblyBytes = File.ReadAllBytes(path);
                string pdbPath = Path.ChangeExtension(path, ".pdb");
                Assembly assembly = File.Exists(pdbPath)
                    ? Assembly.Load(assemblyBytes, File.ReadAllBytes(pdbPath))
                    : Assembly.Load(assemblyBytes);
                // Mono cannot unload this assembly even if staging fails, so account for every
                // load attempt rather than only successful provider swaps.
                _loadedGenerationCount++;
                _loadedAssemblyBytes += assemblyBytes.LongLength + (File.Exists(pdbPath) ? new FileInfo(pdbPath).Length : 0L);
                var provider = new DynamicAssemblyScriptProvider(assembly);
                ScriptSwapResult result = ModuleSystem.GetModule<IScriptRuntimeModule>().ReplaceProvider(
                    provider, RtsProjectSettings.instance.StateMigrationPolicy);
                if (!result.Succeeded) throw new InvalidOperationException(result.Error);
                _healthyProvider = provider;
                _healthyPath = path;
                stopwatch.Stop();
                RtsRuntimeStatus.RecordApply(true, stopwatch.Elapsed.TotalMilliseconds);
                if (_loadedGenerationCount == 20 || _loadedAssemblyBytes >= 64L * 1024L * 1024L)
                    Log.Warning("[RTS] Mono cannot unload old hot-update assemblies. Schedule a low-frequency maintenance Play/Domain reload; gameplay hot reload remains available meanwhile.");
                Log.Info("[RTS] Loaded generation '{0}', active instances: {1}.", provider.GenerationName, result.ReplacedCount);
                return true;
            }
            catch (Exception exception)
            {
                stopwatch.Stop();
                RtsRuntimeStatus.RecordApply(false, stopwatch.Elapsed.TotalMilliseconds, exception.ToString());
                Log.Error("[RTS] Failed to load '{0}':\n{1}", path, exception);
                return false;
            }
        }

        internal static bool RestoreHealthyGeneration()
        {
            if (_healthyProvider == null || !EditorApplication.isPlaying) return false;
            ScriptSwapResult result = ModuleSystem.GetModule<IScriptRuntimeModule>().ReplaceProvider(_healthyProvider);
            if (!result.Succeeded) Log.Error("[RTS] Failed to restore healthy generation: {0}", result.Error);
            return result.Succeeded;
        }

        internal static void RequestRestartCurrentScene()
        {
            IScriptRuntimeModule runtime = ModuleSystem.GetModule<IScriptRuntimeModule>();
            runtime.RestartCurrentScene(
                completed: (succeeded, error) =>
                {
                    if (succeeded) Log.Info("[RTS] Current scene restarted.");
                    else Log.Error("[RTS] Scene restart failed: {0}", error);
                });
        }

        internal static void StressRestartCurrentScene()
        {
            _restartStressRemaining = RESTART_STRESS_COUNT;
            RestartNextStressIteration();
        }

        private static void RestartNextStressIteration()
        {
            if (!EditorApplication.isPlaying || _restartStressRemaining <= 0)
            {
                if (_restartStressRemaining == 0) Log.Info("[RTS] Scene restart stress test passed ({0} iterations).", RESTART_STRESS_COUNT);
                _restartStressRemaining = 0;
                return;
            }

            int iteration = RESTART_STRESS_COUNT - _restartStressRemaining + 1;
            IScriptRuntimeModule runtime = ModuleSystem.GetModule<IScriptRuntimeModule>();
            bool started = runtime.RestartCurrentScene(
                completed: (succeeded, error) =>
                {
                    if (!succeeded)
                    {
                        _restartStressRemaining = 0;
                        Log.Error("[RTS] Scene restart stress test failed at iteration {0}: {1}", iteration, error);
                        return;
                    }

                    _restartStressRemaining--;
                    EditorApplication.delayCall += RestartNextStressIteration;
                });
            if (!started)
            {
                _restartStressRemaining = 0;
                Log.Error("[RTS] Scene restart stress test could not start iteration {0}.", iteration);
            }
        }
    }
}
