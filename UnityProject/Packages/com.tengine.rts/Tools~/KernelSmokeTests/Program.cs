using System;
using TEngine.RTS;

internal static class Program
{
    private static int Main()
    {
        var context = new TestLog();
        var world = new TestWorldObject();
        var kernel = new ScriptRuntimeKernel();
        Assert(kernel.ReplaceProvider(new StaticScriptProvider("g1").Register("counter", () => new CounterScript(1))).Succeeded, "g1");
        Assert(kernel.Attach(1, "counter", string.Empty, context, world, out string error), error);
        kernel.Tick(new ScriptTime(1f, 1f, 0));
        Assert(world.Value == 1, "g1 tick");
        Assert(kernel.ReplaceProvider(new StaticScriptProvider("g2").Register("counter", () => new CounterScript(2))).Succeeded, "g2");
        kernel.Tick(new ScriptTime(1f, 1f, 1));
        Assert(world.Value == 3, "state migration");
        Assert(kernel.ReplaceProvider(new StaticScriptProvider("reset").Register("counter", () => new CounterScript(4)), ScriptStateMigrationPolicy.Reset).Succeeded, "reset policy");
        kernel.Tick(new ScriptTime(1f, 1f, 2));
        Assert(world.Value == 4, "reset discards state");
        Assert(!kernel.ReplaceProvider(new StaticScriptProvider("broken")).Succeeded, "reject missing id");
        kernel.Tick(new ScriptTime(1f, 1f, 2));
        Assert(world.Value == 8, "rollback");
        Assert(!kernel.ReplaceProvider(new StaticScriptProvider("schema-2").Register("counter", () => new CounterScript(1, 2)), ScriptStateMigrationPolicy.RequireCompatibleSchema).Succeeded, "schema mismatch blocked");
        kernel.Tick(new ScriptTime(1f, 1f, 3));
        Assert(world.Value == 12, "schema mismatch keeps healthy generation");
        int deactivationCount = 0;
        Assert(!kernel.ReplaceProvider(new StaticScriptProvider("activate-failure")
            .Register("counter", () => new ActivationFailureScript(() => deactivationCount++))).Succeeded,
            "activation failure is rejected");
        Assert(deactivationCount == 1, "partially activated generation is deactivated");
        kernel.Tick(new ScriptTime(1f, 1f, 3));
        Assert(world.Value == 16, "activation failure keeps healthy generation");
        kernel.ReplaceProvider(new StaticScriptProvider("fault").Register("counter", () => new FaultingScript()));
        kernel.Tick(new ScriptTime(1f, 1f, 3));
        kernel.Tick(new ScriptTime(1f, 1f, 4));
        Assert(context.ErrorCount == 1, "fault fuse");
        int cleanupCount = 0;
        Assert(kernel.ReplaceProvider(new StaticScriptProvider("scope").Register("counter", () => new ScopedScript(() => cleanupCount++))).Succeeded, "scope provider");
        kernel.Detach(1);
        Assert(cleanupCount == 1, "scope cleanup on detach");

        Assert(kernel.ReplaceProvider(new StaticScriptProvider("stress-0").Register("counter", () => new CounterScript(0))).Succeeded, "stress setup");
        Assert(kernel.Attach(1, "counter", string.Empty, context, world, out error), error);
        for (int generation = 1; generation <= 100; generation++)
        {
            Assert(kernel.ReplaceProvider(new StaticScriptProvider($"stress-{generation}")
                .Register("counter", () => new CounterScript(1))).Succeeded, $"stress generation {generation}");
        }
        Assert(kernel.ActiveInstanceCount == 1, "stress active instance count");
        kernel.Dispose();
        Console.WriteLine("RTS kernel smoke tests passed.");
        return 0;
    }

    private static void Assert(bool value, string message) { if (!value) throw new InvalidOperationException(message); }

    private sealed class TestLog : IScriptLog
    {
        public int ErrorCount { get; private set; }
        public void LogInfo(string message) { }
        public void LogWarning(string message) { }
        public void LogError(string message) { ErrorCount++; }
    }

    private sealed class ScopedScript : IScript
    {
        private readonly Action _cleanup;
        public ScopedScript(Action cleanup) { _cleanup = cleanup; }
        public void Bind(IScriptContext context, IWorldObject owner, string config) { context.Scope.Register(_cleanup); }
        public void RestoreState(ScriptState state) { }
        public void Start() { }
        public void Tick(in ScriptTime time) { }
        public ScriptState CaptureState() => ScriptState.Empty;
        public void Dispose() { }
    }

    private sealed class TestWorldObject : IWorldObject
    {
        public int Value { get; set; }
        public int InstanceId => 1;
        public string Name => "test";
        public bool TryGetCapability<T>(out T capability) where T : class
        { capability = this as T; return capability != null; }
    }

    private sealed class CounterScript : IScript, IScriptStateSchema
    {
        private readonly int _step;
        private TestWorldObject _world;
        private int _total;
        public CounterScript(int step, int schema = 1) { _step = step; StateSchemaVersion = schema; }
        public int StateSchemaVersion { get; }
        public void Bind(IScriptContext context, IWorldObject owner, string config) { _world = (TestWorldObject)owner; }
        public void RestoreState(ScriptState state) { _total = state.SchemaVersion == 1 ? int.Parse(state.Payload) : 0; }
        public void Start() { }
        public void Tick(in ScriptTime time) { _total += _step; _world.Value = _total; }
        public ScriptState CaptureState() => new ScriptState(1, _total.ToString());
        public void Dispose() { }
    }

    private sealed class FaultingScript : IScript
    {
        public void Bind(IScriptContext context, IWorldObject owner, string config) { }
        public void RestoreState(ScriptState state) { }
        public void Start() { }
        public void Tick(in ScriptTime time) { throw new InvalidOperationException("expected"); }
        public ScriptState CaptureState() => ScriptState.Empty;
        public void Dispose() { }
    }

    private sealed class ActivationFailureScript : IScript, IRtsScriptLifecycleV1
    {
        private readonly Action _deactivated;
        public ActivationFailureScript(Action deactivated) { _deactivated = deactivated; }
        public void Bind(IScriptContext context, IWorldObject owner, string config) { }
        public void RestoreState(ScriptState state) { }
        public void Activate(bool isHotReload) { throw new InvalidOperationException("expected activation failure"); }
        public void Deactivate() { _deactivated(); }
        public void Start() { }
        public void Tick(in ScriptTime time) { }
        public ScriptState CaptureState() => ScriptState.Empty;
        public void Dispose() { }
    }
}
