using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using System.Threading;
using UnityEditor;
using Debug = UnityEngine.Debug;

namespace TEngine.RTS.Editor
{
    internal readonly struct RtsCompileResult
    {
        internal RtsCompileResult(bool succeeded, string assemblyPath, string diagnostics, double elapsedMilliseconds)
        {
            Succeeded = succeeded;
            AssemblyPath = assemblyPath ?? string.Empty;
            Diagnostics = diagnostics ?? string.Empty;
            ElapsedMilliseconds = elapsedMilliseconds;
        }

        internal bool Succeeded { get; }
        internal string AssemblyPath { get; }
        internal string Diagnostics { get; }
        internal double ElapsedMilliseconds { get; }
    }

    [InitializeOnLoad]
    internal static class RtsCompilationService
    {
        private const string COMPILER_PROJECT = "Packages/com.tengine.rts/Tools~/Compiler/TEngine.RTS.Compiler.csproj";
        private static readonly object OUTPUT_LOCK = new object();
        private static readonly List<Action<RtsCompileResult>> CALLBACKS = new List<Action<RtsCompileResult>>();
        private static readonly StringBuilder STANDARD_OUTPUT = new StringBuilder();
        private static readonly StringBuilder STANDARD_ERROR = new StringBuilder();
        private static readonly Queue<double> LATENCIES = new Queue<double>();

        private static Process _process;
        private static Process _daemon;
        private static bool _daemonBusy;
        private static Stopwatch _stopwatch;
        private static volatile bool _processFinished;
        private static volatile bool _cancelRequested;
        private static bool _rerunPending;
        private static int _exitCode;
        private static FileStream _compileUnitLock;

        internal static bool IsCompiling => _process != null || _daemonBusy;
        internal static bool HasPendingCompile => _rerunPending;
        internal static RtsCompileResult LastResult { get; private set; }
        internal static double P95Milliseconds
        {
            get
            {
                if (LATENCIES.Count == 0) return 0d;
                double[] values = LATENCIES.OrderBy(x => x).ToArray();
                return values[Math.Min(values.Length - 1, (int)Math.Ceiling(values.Length * .95) - 1)];
            }
        }
        internal static event Action StateChanged;

        static RtsCompilationService()
        {
            EditorApplication.update += Update;
            AssemblyReloadEvents.beforeAssemblyReload += Shutdown;
            EditorApplication.quitting += Shutdown;
        }

        internal static void RequestCompile(Action<RtsCompileResult> completed = null)
        {
            if (completed != null) CALLBACKS.Add(completed);
            if (IsCompiling)
            {
                _rerunPending = true;
                StateChanged?.Invoke();
                return;
            }
            StartCompile();
        }

        internal static void Cancel()
        {
            if (_process == null && !_daemonBusy) return;
            _cancelRequested = true;
            _rerunPending = false;
            try { if (_process != null && !_process.HasExited) _process.Kill(); }
            catch (Exception exception) { Debug.LogWarning($"[RTS] Failed to cancel compiler: {exception.Message}"); }
            if (_daemonBusy)
            {
                double elapsed = _stopwatch?.Elapsed.TotalMilliseconds ?? 0d;
                StopDaemon();
                DisposeProcess();
                CompleteFinal(new RtsCompileResult(false, string.Empty, "RTS compiler daemon was cancelled; the healthy generation is unchanged.", elapsed));
            }
        }

        private static void StartCompile()
        {
            try
            {
                RtsProjectSettings settings = RtsProjectSettings.instance;
                string[] sources = settings.ResolveSourceRoots().Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                string[] references = settings.ResolveReferenceAssemblies().Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                if (sources.Length == 0) throw new InvalidOperationException("No RTS source roots are configured.");
                foreach (string source in sources)
                    if (!Directory.Exists(source)) throw new DirectoryNotFoundException(source);
                foreach (string reference in references)
                    if (!File.Exists(reference)) throw new FileNotFoundException("RTS reference assembly was not found.", reference);

                string compilerProject = Path.Combine(settings.ProjectRoot, COMPILER_PROJECT);
                string lockDirectory = Path.Combine(settings.ProjectRoot, "Library", "TEngineRTS", "locks");
                Directory.CreateDirectory(lockDirectory);
                string outputDirectory = settings.ResolveOutputDirectory();
                string outputKey;
                using (SHA256 sha = SHA256.Create()) outputKey = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(outputDirectory.ToUpperInvariant()))).Replace("-", string.Empty).Substring(0, 16);
                _compileUnitLock = new FileStream(Path.Combine(lockDirectory, "output-" + outputKey + ".compile.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                var compilerArgs = new List<string>();
                foreach (string source in sources) { compilerArgs.Add("--source"); compilerArgs.Add(source); }
                compilerArgs.Add("--output"); compilerArgs.Add(outputDirectory);
                foreach (string reference in references) { compilerArgs.Add("--reference"); compilerArgs.Add(reference); }

                lock (OUTPUT_LOCK)
                {
                    STANDARD_OUTPUT.Clear();
                    STANDARD_ERROR.Clear();
                }
                _processFinished = false;
                _cancelRequested = false;
                _exitCode = -1;
                _stopwatch = Stopwatch.StartNew();
                if (settings.UseCompilerDaemon)
                {
                    EnsureDaemon(compilerProject);
                    _daemonBusy = true;
                    _daemon.StandardInput.WriteLine(string.Join("\t", compilerArgs.Select(Encode)));
                    _daemon.StandardInput.Flush();
                    StateChanged?.Invoke();
                    return;
                }
                string arguments = "run --project " + Quote(compilerProject) + " --configuration Release -- " + string.Join(" ", compilerArgs.Select(Quote));
                _process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "dotnet",
                        Arguments = arguments,
                        WorkingDirectory = settings.ProjectRoot,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    },
                    EnableRaisingEvents = true
                };
                _process.OutputDataReceived += (_, args) => AppendLine(STANDARD_OUTPUT, args.Data);
                _process.ErrorDataReceived += (_, args) => AppendLine(STANDARD_ERROR, args.Data);
                _process.Exited += (_, __) => ThreadPool.QueueUserWorkItem(_ => FinishProcessOnWorker());
                if (!_process.Start()) throw new InvalidOperationException("Failed to start the RTS compiler process.");
                _process.BeginOutputReadLine();
                _process.BeginErrorReadLine();
                StateChanged?.Invoke();
            }
            catch (Exception exception)
            {
                DisposeProcess();
                CompleteFinal(new RtsCompileResult(false, string.Empty, exception.ToString(), 0d));
            }
        }

        private static void EnsureDaemon(string compilerProject)
        {
            if (_daemon != null && !_daemon.HasExited) return;
            _daemon?.Dispose();
            _daemon = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "dotnet", Arguments = "run --project " + Quote(compilerProject) + " --configuration Release -- --server",
                    WorkingDirectory = RtsProjectSettings.instance.ProjectRoot, UseShellExecute = false, CreateNoWindow = true,
                    RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true
                }, EnableRaisingEvents = true
            };
            _daemon.OutputDataReceived += (_, args) => HandleDaemonOutput(args.Data);
            _daemon.ErrorDataReceived += (_, args) => AppendLine(STANDARD_ERROR, args.Data);
            _daemon.Exited += (_, __) => { if (_daemonBusy) { _exitCode = -1; _processFinished = true; } };
            if (!_daemon.Start()) throw new InvalidOperationException("Failed to start RTS compiler daemon.");
            _daemon.BeginOutputReadLine(); _daemon.BeginErrorReadLine();
        }

        private static void HandleDaemonOutput(string line)
        {
            if (line == null) return;
            if (!line.StartsWith("@@RTS_RESULT\t", StringComparison.Ordinal)) { AppendLine(STANDARD_OUTPUT, line); return; }
            try
            {
                string[] parts = line.Split('\t'); _exitCode = int.Parse(parts[1]);
                AppendLine(STANDARD_OUTPUT, Decode(parts[2])); AppendLine(STANDARD_ERROR, Decode(parts[3])); _processFinished = true;
            }
            catch (Exception exception) { AppendLine(STANDARD_ERROR, exception.ToString()); _exitCode = -1; _processFinished = true; }
        }

        private static void FinishProcessOnWorker()
        {
            try
            {
                _process.WaitForExit();
                _exitCode = _process.ExitCode;
            }
            catch (Exception exception)
            {
                AppendLine(STANDARD_ERROR, exception.ToString());
                _exitCode = -1;
            }
            _processFinished = true;
        }

        private static void Update()
        {
            if ((_process == null && !_daemonBusy) || !_processFinished) return;

            _stopwatch.Stop();
            string output;
            string error;
            lock (OUTPUT_LOCK)
            {
                output = STANDARD_OUTPUT.ToString();
                error = STANDARD_ERROR.ToString();
            }
            string assemblyPath = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .LastOrDefault(line => line.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) ?? string.Empty;
            bool succeeded = !_cancelRequested && _exitCode == 0 && File.Exists(assemblyPath);
            string diagnostics = string.IsNullOrWhiteSpace(error) ? output.Trim() : error.Trim();
            var result = new RtsCompileResult(succeeded, assemblyPath, diagnostics, _stopwatch.Elapsed.TotalMilliseconds);

            DisposeProcess();
            if (_rerunPending && !_cancelRequested)
            {
                _rerunPending = false;
                StartCompile();
                return;
            }
            CompleteFinal(result);
        }

        private static void CompleteFinal(RtsCompileResult result)
        {
            LastResult = result;
            if (result.ElapsedMilliseconds > 0d)
            {
                LATENCIES.Enqueue(result.ElapsedMilliseconds);
                while (LATENCIES.Count > 50) LATENCIES.Dequeue();
            }
            RtsRuntimeStatus.RecordCompile(in result);
            Action<RtsCompileResult>[] callbacks = CALLBACKS.ToArray();
            CALLBACKS.Clear();
            StateChanged?.Invoke();
            foreach (Action<RtsCompileResult> callback in callbacks)
            {
                try { callback(result); }
                catch (Exception exception) { Debug.LogException(exception); }
            }
        }

        private static void AppendLine(StringBuilder builder, string value)
        {
            if (value == null) return;
            lock (OUTPUT_LOCK) builder.AppendLine(value);
        }

        private static string Quote(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";
        private static string Encode(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
        private static string Decode(string value) => Encoding.UTF8.GetString(Convert.FromBase64String(value));

        private static void Shutdown()
        {
            Cancel();
            DisposeProcess();
            StopDaemon();
            CALLBACKS.Clear();
        }

        private static void DisposeProcess()
        {
            _process?.Dispose();
            _process = null;
            _stopwatch = null;
            _processFinished = false;
            _daemonBusy = false;
            _compileUnitLock?.Dispose();
            _compileUnitLock = null;
        }

        private static void StopDaemon()
        {
            try { if (_daemon != null && !_daemon.HasExited) { _daemon.StandardInput.WriteLine("shutdown"); if (!_daemon.WaitForExit(500)) _daemon.Kill(); } }
            catch { }
            _daemon?.Dispose(); _daemon = null; _daemonBusy = false;
        }
    }
}
