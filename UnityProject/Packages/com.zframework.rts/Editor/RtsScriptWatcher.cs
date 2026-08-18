using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEditor;
using UnityEngine;

namespace ZFramework.RTS.Editor
{
    [InitializeOnLoad]
    internal static class RtsScriptWatcher
    {
        private const string ENABLED_KEY = "ZFramework.RTS.AutoReloadEnabled";
        private const double DEBOUNCE_SECONDS = 0.35d;

        private static readonly List<FileSystemWatcher> WATCHERS = new List<FileSystemWatcher>();
        private static long _lastChangeTicks;
        private static int _changePending;
        private static bool _reloadCallbackPending;

        static RtsScriptWatcher()
        {
            CreateWatcher();
            EditorApplication.update += Update;
            EditorApplication.quitting += Dispose;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            AssemblyReloadEvents.beforeAssemblyReload += Dispose;
        }

        internal static bool IsEnabled => EditorPrefs.GetBool(ENABLED_KEY, true);

        internal static void SetEnabled(bool enabled)
        {
            EditorPrefs.SetBool(ENABLED_KEY, enabled);
            Log.Info("[RTS] Auto reload {0}.", enabled ? "enabled" : "disabled");
        }

        private static void CreateWatcher()
        {
            DisposeWatchers();
            foreach (string sourceDirectory in RtsProjectSettings.instance.ResolveSourceRoots())
            {
                if (!Directory.Exists(sourceDirectory)) continue;
                var watcher = new FileSystemWatcher(sourceDirectory, "*.cs")
                {
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
                    EnableRaisingEvents = true
                };
                watcher.Changed += OnSourceChanged;
                watcher.Created += OnSourceChanged;
                watcher.Deleted += OnSourceChanged;
                watcher.Renamed += OnSourceRenamed;
                WATCHERS.Add(watcher);
            }
        }

        internal static void RefreshWatchers() => CreateWatcher();

        private static void OnSourceChanged(object sender, FileSystemEventArgs args)
        {
            Interlocked.Exchange(ref _lastChangeTicks, DateTime.UtcNow.Ticks);
            Interlocked.Exchange(ref _changePending, 1);
        }

        private static void OnSourceRenamed(object sender, RenamedEventArgs args) => OnSourceChanged(sender, args);

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
                Interlocked.Exchange(ref _changePending, 0);
        }

        private static void Update()
        {
            if (!IsEnabled || !EditorApplication.isPlaying || EditorApplication.isPaused) return;
            if (Interlocked.CompareExchange(ref _changePending, 0, 0) == 0) return;

            long changedAt = Interlocked.Read(ref _lastChangeTicks);
            if (new TimeSpan(DateTime.UtcNow.Ticks - changedAt).TotalSeconds < DEBOUNCE_SECONDS) return;

            Interlocked.Exchange(ref _changePending, 0);
            if (_reloadCallbackPending)
            {
                RtsCompilationService.RequestCompile();
                return;
            }

            _reloadCallbackPending = true;
            RtsCompilationService.RequestCompile(result =>
            {
                _reloadCallbackPending = false;
                if (result.Succeeded) ScriptAssemblyLoader.TryLoadCompiledAssembly(result.AssemblyPath);
                else Log.Error("[RTS] Auto reload compile failed; the previous generation is still running:\n{0}",
                    result.Diagnostics);
            });
        }

        private static void Dispose()
        {
            EditorApplication.update -= Update;
            EditorApplication.quitting -= Dispose;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= Dispose;
            DisposeWatchers();
        }

        private static void DisposeWatchers()
        {
            foreach (FileSystemWatcher watcher in WATCHERS)
            {
                watcher.EnableRaisingEvents = false;
                watcher.Changed -= OnSourceChanged;
                watcher.Created -= OnSourceChanged;
                watcher.Deleted -= OnSourceChanged;
                watcher.Renamed -= OnSourceRenamed;
                watcher.Dispose();
            }
            WATCHERS.Clear();
        }
    }
}
