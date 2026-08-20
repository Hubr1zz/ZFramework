#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AgentWorkflow.Editor
{
    public sealed partial class AgentWorkbenchWindow
    {
        [Serializable]
        private sealed class CodeIndexProgress
        {
            public string stage;
            public int completed;
            public int total;
            public float percent;
            public string message;
        }

        [Serializable]
        private sealed class CodeIndexBuildResult
        {
            public string root;
            public string generatedAtUtc;
            public int fileCount;
            public int parsedFileCount;
            public int reusedFileCount;
            public int typeCount;
            public int methodCount;
            public int qualifiedTypeCount;
            public int resolvedCallCount;
            public int discoveredFileCount;
            public int indexedFileCount;
            public int missingFileCount;
            public int unexpectedFileCount;
            public float coveragePercent;
            public bool includesAllSourceFiles;
        }

        private Process codeIndexProcess;
        private CodeIndexProgress codeIndexProgress;
        private CodeIndexBuildResult codeIndexResult;
        private string codeIndexStatusKey = "codeIndex.idle";
        private string codeIndexError = string.Empty;
        private double codeIndexStartedAt;
        private double codeIndexElapsedSeconds;
        private int codeIndexDiskFileCount;

        private string CodeIndexPath => Path.Combine(_projectRoot, ".agent-memory", "zworkflow", "local", "code-query-index.json");
        private string CodeIndexProgressPath => Path.Combine(_projectRoot, ".agent-memory", "zworkflow", "local", "code-query-progress.json");
        private bool IsCodeIndexBuilding => codeIndexProcess != null && !codeIndexProcess.HasExited;

        private void DrawCodeIndexTab()
        {
            EnsureCodeIndexSnapshot();
            EditorGUILayout.LabelField(L("codeIndex.title"), EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(L("codeIndex.summary"), MessageType.Info);
            EditorGUILayout.LabelField(L("codeIndex.fullScope"), EditorStyles.miniLabel);
            EditorGUILayout.Space(8);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.enabled = !IsCodeIndexBuilding;
                if (GUILayout.Button(L("codeIndex.build"), GUILayout.Height(30)))
                    StartCodeIndexBuild();
                GUI.enabled = IsCodeIndexBuilding;
                if (GUILayout.Button(L("codeIndex.cancel"), GUILayout.Width(90), GUILayout.Height(30)))
                    CancelCodeIndexBuild();
                GUI.enabled = File.Exists(CodeIndexPath);
                if (GUILayout.Button(L("codeIndex.open"), GUILayout.Width(130), GUILayout.Height(30)))
                    EditorUtility.RevealInFinder(CodeIndexPath);
                GUI.enabled = true;
            }

            EditorGUILayout.Space(8);
            DrawCodeIndexProgress();
            DrawCodeIndexMetrics();
            if (!string.IsNullOrWhiteSpace(codeIndexError))
                EditorGUILayout.HelpBox(codeIndexError, MessageType.Error);
        }

        private void DrawCodeIndexProgress()
        {
            var message = IsCodeIndexBuilding ? string.Format(L("codeIndex.running"), codeIndexProgress?.message ?? "scan") : L(codeIndexStatusKey);
            var messageType = codeIndexStatusKey == "codeIndex.failed" ? MessageType.Error : codeIndexStatusKey == "codeIndex.succeeded" ? MessageType.Info : MessageType.None;
            EditorGUILayout.HelpBox(message, messageType);
            var progress = IsCodeIndexBuilding ? Mathf.Clamp01((codeIndexProgress?.percent ?? 0f) / 100f) : codeIndexResult?.coveragePercent / 100f ?? 0f;
            var label = IsCodeIndexBuilding && codeIndexProgress != null && codeIndexProgress.total > 0 ? $"{codeIndexProgress.completed}/{codeIndexProgress.total} · {codeIndexProgress.percent:0.0}%" : $"{progress * 100f:0.00}%";
            var rect = EditorGUILayout.GetControlRect(false, 20f);
            EditorGUI.ProgressBar(rect, progress, label);
        }

        private void DrawCodeIndexMetrics()
        {
            if (codeIndexResult == null)
                return;
            using (new EditorGUILayout.VerticalScope("box"))
            {
                DrawCodeIndexMetric(L("codeIndex.files"), $"{codeIndexResult.indexedFileCount}/{codeIndexResult.discoveredFileCount}");
                DrawCodeIndexMetric(L("codeIndex.parsed"), codeIndexResult.parsedFileCount.ToString());
                DrawCodeIndexMetric(L("codeIndex.reused"), codeIndexResult.reusedFileCount.ToString());
                DrawCodeIndexMetric(L("codeIndex.coverage"), $"{codeIndexResult.coveragePercent:0.00}%");
                DrawCodeIndexMetric(L("codeIndex.types"), codeIndexResult.typeCount.ToString());
                DrawCodeIndexMetric(L("codeIndex.methods"), codeIndexResult.methodCount.ToString());
                DrawCodeIndexMetric(L("codeIndex.calls"), codeIndexResult.resolvedCallCount.ToString());
                DrawCodeIndexMetric(L("codeIndex.missing"), codeIndexResult.missingFileCount.ToString());
                DrawCodeIndexMetric(L("codeIndex.unexpected"), codeIndexResult.unexpectedFileCount.ToString());
                DrawCodeIndexMetric(L("codeIndex.elapsed"), $"{codeIndexElapsedSeconds:0.00}s");
            }
            if (codeIndexDiskFileCount != codeIndexResult.indexedFileCount)
                EditorGUILayout.HelpBox(string.Format(L("codeIndex.diskMismatch"), codeIndexDiskFileCount, codeIndexResult.indexedFileCount), MessageType.Warning);
        }

        private static void DrawCodeIndexMetric(string label, string value)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(label, GUILayout.Width(150));
                EditorGUILayout.SelectableLabel(value, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
            }
        }

        private void StartCodeIndexBuild()
        {
            var scriptPath = Path.Combine(_projectRoot, ".agents", "skills", "codebase-query", "scripts", "run.ps1");
            if (!File.Exists(scriptPath))
            {
                codeIndexStatusKey = "codeIndex.failed";
                codeIndexError = $"codebase-query entry not found: {scriptPath}";
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(CodeIndexProgressPath) ?? _projectRoot);
            if (File.Exists(CodeIndexProgressPath))
                File.Delete(CodeIndexProgressPath);
            codeIndexProgress = null;
            codeIndexResult = null;
            codeIndexError = string.Empty;
            codeIndexStatusKey = "codeIndex.running";
            codeIndexStartedAt = EditorApplication.timeSinceStartup;
            var startInfo = new ProcessStartInfo
            {
                FileName = "pwsh",
                Arguments = $"-NoProfile -File {QuoteProcessArgument(scriptPath)} build -Root {QuoteProcessArgument(_projectRoot)} -ProgressPath {QuoteProcessArgument(CodeIndexProgressPath)} -IncludeAll",
                WorkingDirectory = _projectRoot,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            try
            {
                codeIndexProcess = Process.Start(startInfo);
                if (codeIndexProcess != null)
                    return;
                codeIndexStatusKey = "codeIndex.failed";
                codeIndexError = "Failed to start pwsh.";
            }
            catch (Exception exception)
            {
                codeIndexStatusKey = "codeIndex.failed";
                codeIndexError = exception.Message;
            }
        }

        private void PollCodeIndexBuild()
        {
            if (codeIndexProcess == null)
                return;
            TryReadCodeIndexProgress();
            codeIndexElapsedSeconds = EditorApplication.timeSinceStartup - codeIndexStartedAt;
            if (!codeIndexProcess.HasExited)
            {
                Repaint();
                return;
            }

            var output = codeIndexProcess.StandardOutput.ReadToEnd();
            var error = codeIndexProcess.StandardError.ReadToEnd();
            var exitCode = codeIndexProcess.ExitCode;
            codeIndexProcess.Dispose();
            codeIndexProcess = null;
            if (exitCode != 0)
            {
                codeIndexStatusKey = "codeIndex.failed";
                codeIndexError = string.IsNullOrWhiteSpace(error) ? output : error;
                Repaint();
                return;
            }

            codeIndexResult = JsonUtility.FromJson<CodeIndexBuildResult>(output);
            codeIndexDiskFileCount = CountCodeIndexSourceFiles();
            var complete = codeIndexResult != null && codeIndexResult.includesAllSourceFiles && codeIndexResult.coveragePercent >= 100f && codeIndexResult.missingFileCount == 0 && codeIndexResult.unexpectedFileCount == 0 && codeIndexResult.indexedFileCount == codeIndexDiskFileCount;
            codeIndexStatusKey = complete ? "codeIndex.succeeded" : "codeIndex.failed";
            if (!complete)
                codeIndexError = "Coverage verification failed. Rebuild after checking the source roots and index output.";
            Repaint();
        }

        private void CancelCodeIndexBuild()
        {
            if (!IsCodeIndexBuilding)
                return;
            codeIndexProcess.Kill();
            codeIndexProcess.Dispose();
            codeIndexProcess = null;
            codeIndexStatusKey = "codeIndex.cancelled";
            codeIndexElapsedSeconds = EditorApplication.timeSinceStartup - codeIndexStartedAt;
        }

        private void TryReadCodeIndexProgress()
        {
            if (!File.Exists(CodeIndexProgressPath))
                return;
            try
            {
                codeIndexProgress = JsonUtility.FromJson<CodeIndexProgress>(File.ReadAllText(CodeIndexProgressPath));
            }
            catch (IOException)
            {
                // The worker replaces this local progress snapshot while the editor polls it.
            }
        }

        private void EnsureCodeIndexSnapshot()
        {
            if (codeIndexResult != null || IsCodeIndexBuilding || !File.Exists(CodeIndexPath))
                return;
            try
            {
                codeIndexResult = JsonUtility.FromJson<CodeIndexBuildResult>(File.ReadAllText(CodeIndexPath));
                codeIndexResult.discoveredFileCount = codeIndexResult.fileCount;
                codeIndexResult.indexedFileCount = codeIndexResult.fileCount;
                codeIndexResult.coveragePercent = 100f;
                codeIndexDiskFileCount = CountCodeIndexSourceFiles();
                codeIndexStatusKey = codeIndexDiskFileCount == codeIndexResult.fileCount ? "codeIndex.succeeded" : "codeIndex.idle";
            }
            catch (Exception exception)
            {
                codeIndexStatusKey = "codeIndex.failed";
                codeIndexError = exception.Message;
            }
        }

        private int CountCodeIndexSourceFiles() => Directory.Exists(Path.Combine(_projectRoot, "Assets")) ? Directory.GetFiles(Path.Combine(_projectRoot, "Assets"), "*.cs", SearchOption.AllDirectories).Length : 0;
    }
}
#endif
