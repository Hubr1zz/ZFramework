using System;
using System.Collections.Generic;

namespace TEngine.RTS
{
    public sealed class ScriptRuntimeKernel : IDisposable
    {
        private sealed class Entry
        {
            public int InstanceId;
            public string ScriptId;
            public string InitialConfig;
            public IScriptLog HostLog;
            public IScriptContext Context;
            public IWorldObject WorldObject;
            public IScript Script;
            public bool Faulted;
            public bool Activated;
        }

        private sealed class ScopedContext : IScriptContext
        {
            private readonly IScriptLog _hostLog;

            public ScopedContext(IScriptLog hostLog)
            {
                _hostLog = hostLog ?? throw new ArgumentNullException(nameof(hostLog));
                Scope = new ScriptScope();
            }

            public IScriptScope Scope { get; }
            public void LogInfo(string message) => _hostLog.LogInfo(message);
            public void LogWarning(string message) => _hostLog.LogWarning(message);
            public void LogError(string message) => _hostLog.LogError(message);
            public bool TryGetService<T>(out T service) where T : class
            {
                if (_hostLog is IScriptServiceResolver resolver) return resolver.TryGetService(out service);
                service = null;
                return false;
            }
        }

        private sealed class ScriptScope : IScriptScope
        {
            private readonly List<Action> _cleanups = new List<Action>();
            public bool IsDisposed { get; private set; }

            public void Register(IDisposable resource)
            {
                if (resource == null) throw new ArgumentNullException(nameof(resource));
                Register(resource.Dispose);
            }

            public void Register(Action cleanup)
            {
                if (cleanup == null) throw new ArgumentNullException(nameof(cleanup));
                if (IsDisposed) throw new ObjectDisposedException(nameof(ScriptScope));
                _cleanups.Add(cleanup);
            }

            public void Dispose()
            {
                if (IsDisposed) return;
                IsDisposed = true;
                List<Exception> errors = null;
                for (int i = _cleanups.Count - 1; i >= 0; i--)
                {
                    try { _cleanups[i](); }
                    catch (Exception exception)
                    {
                        if (errors == null) errors = new List<Exception>();
                        errors.Add(exception);
                    }
                }
                _cleanups.Clear();
                if (errors != null) throw new AggregateException("One or more RTS scope cleanups failed.", errors);
            }
        }

        private readonly Dictionary<int, Entry> _entries = new Dictionary<int, Entry>();
        private readonly List<Entry> _tickSnapshot = new List<Entry>();
        private IScriptProvider _provider;

        public string ActiveGeneration => _provider?.GenerationName ?? string.Empty;
        public int ActiveInstanceCount => _entries.Count;

        public bool Attach(int instanceId, string scriptId, string initialConfig, IScriptLog hostLog,
            IWorldObject worldObject, out string error)
        {
            error = string.Empty;
            if (_entries.ContainsKey(instanceId)) return true;
            if (_provider == null) { error = "No script provider is active."; return false; }

            IScript script = null;
            ScopedContext context = null;
            try
            {
                if (!_provider.TryCreate(scriptId, out script) || script == null)
                {
                    error = $"Script '{scriptId}' was not found in generation '{_provider.GenerationName}'.";
                    return false;
                }

                context = new ScopedContext(hostLog);
                var entry = new Entry
                {
                    InstanceId = instanceId,
                    ScriptId = scriptId,
                    InitialConfig = initialConfig ?? string.Empty,
                    HostLog = hostLog,
                    Context = context,
                    WorldObject = worldObject ?? throw new ArgumentNullException(nameof(worldObject)),
                    Script = script
                };
                script.Bind(context, worldObject, entry.InitialConfig);
                script.RestoreState(ScriptState.Empty);
                Activate(entry, false);
                _entries.Add(instanceId, entry);
                return true;
            }
            catch (Exception exception)
            {
                SafeDispose(script, context, hostLog);
                error = exception.ToString();
                return false;
            }
        }

        public void Detach(int instanceId)
        {
            if (!_entries.TryGetValue(instanceId, out Entry entry)) return;
            _entries.Remove(instanceId);
            SafeDispose(entry);
        }

        public void Tick(in ScriptTime time)
        {
            _tickSnapshot.Clear();
            _tickSnapshot.AddRange(_entries.Values);
            for (int i = 0; i < _tickSnapshot.Count; i++)
            {
                Entry entry = _tickSnapshot[i];
                if (entry.Faulted || !_entries.ContainsKey(entry.InstanceId)) continue;
                try { entry.Script.Tick(in time); }
                catch (Exception exception)
                {
                    entry.Faulted = true;
                    entry.Context.LogError($"RTS script '{entry.ScriptId}' on '{entry.WorldObject.Name}' was disabled:\n{exception}");
                    Deactivate(entry);
                    try { entry.Context.Scope.Dispose(); }
                    catch (Exception cleanupException) { entry.Context.LogError($"RTS fault cleanup failed:\n{cleanupException}"); }
                }
            }
        }

        public ScriptSwapResult ReplaceProvider(IScriptProvider provider,
            ScriptStateMigrationPolicy statePolicy = ScriptStateMigrationPolicy.PreserveWhenCompatible)
        {
            if (provider == null) return ScriptSwapResult.Failure("Provider cannot be null.");
            var staged = new Dictionary<int, Entry>(_entries.Count);
            try
            {
                foreach (Entry oldEntry in _entries.Values)
                {
                    ScriptState state = statePolicy == ScriptStateMigrationPolicy.Reset
                        ? ScriptState.Empty
                        : oldEntry.Script.CaptureState() ?? ScriptState.Empty;
                    if (!provider.TryCreate(oldEntry.ScriptId, out IScript newScript) || newScript == null)
                        throw new InvalidOperationException($"Script '{oldEntry.ScriptId}' is missing from '{provider.GenerationName}'.");

                    var newEntry = new Entry
                    {
                        InstanceId = oldEntry.InstanceId,
                        ScriptId = oldEntry.ScriptId,
                        InitialConfig = oldEntry.InitialConfig,
                        HostLog = oldEntry.HostLog,
                        Context = new ScopedContext(oldEntry.HostLog),
                        WorldObject = oldEntry.WorldObject,
                        Script = newScript
                    };
                    staged.Add(newEntry.InstanceId, newEntry);
                    newScript.Bind(newEntry.Context, newEntry.WorldObject, newEntry.InitialConfig);
                    if (statePolicy == ScriptStateMigrationPolicy.RequireCompatibleSchema && state.SchemaVersion != 0 &&
                        (!(newScript is IScriptStateSchema schema) || schema.StateSchemaVersion != state.SchemaVersion))
                        throw new InvalidOperationException($"State schema {state.SchemaVersion} is incompatible with '{oldEntry.ScriptId}'.");
                    try { newScript.RestoreState(state); }
                    catch when (statePolicy == ScriptStateMigrationPolicy.PreserveWhenCompatible)
                    {
                        newScript.RestoreState(ScriptState.Empty);
                    }
                }
                foreach (Entry entry in staged.Values) Activate(entry, true);
            }
            catch (Exception exception)
            {
                foreach (Entry entry in staged.Values) SafeDispose(entry);
                return ScriptSwapResult.Failure(exception.ToString());
            }

            Dictionary<int, Entry> previous = new Dictionary<int, Entry>(_entries);
            _entries.Clear();
            foreach (KeyValuePair<int, Entry> pair in staged) _entries.Add(pair.Key, pair.Value);
            _provider = provider;
            foreach (Entry entry in previous.Values) SafeDispose(entry);
            return ScriptSwapResult.Success(staged.Count);
        }

        public void Dispose()
        {
            foreach (Entry entry in _entries.Values) SafeDispose(entry);
            _entries.Clear();
            _tickSnapshot.Clear();
            _provider = null;
        }

        private static void SafeDispose(Entry entry)
        {
            if (entry == null) return;
            Deactivate(entry);
            SafeDispose(entry.Script, entry.Context, entry.HostLog);
        }

        private static void Activate(Entry entry, bool isHotReload)
        {
            entry.Activated = true;
            if (entry.Script is IRtsScriptLifecycleV1 lifecycle) lifecycle.Activate(isHotReload);
            else entry.Script.Start();
        }

        private static void Deactivate(Entry entry)
        {
            if (entry == null || !entry.Activated) return;
            entry.Activated = false;
            try { (entry.Script as IRtsScriptLifecycleV1)?.Deactivate(); }
            catch (Exception exception) { entry.HostLog?.LogError($"RTS script Deactivate failed:\n{exception}"); }
        }

        private static void SafeDispose(IScript script, IScriptContext context, IScriptLog hostLog)
        {
            try { script?.Dispose(); }
            catch (Exception exception) { hostLog?.LogError($"RTS script Dispose failed:\n{exception}"); }
            try { context?.Scope.Dispose(); }
            catch (Exception exception) { hostLog?.LogError($"RTS script scope cleanup failed:\n{exception}"); }
        }
    }
}
