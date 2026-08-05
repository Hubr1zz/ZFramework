using System;

namespace TEngine.RTS
{
    public interface IScriptRuntimeModule
    {
        string ActiveGeneration { get; }
        int ActiveInstanceCount { get; }
        bool IsRestartingScene { get; }
        void Attach(ScriptAnchor anchor);
        void Detach(ScriptAnchor anchor);
        ScriptSwapResult ReplaceProvider(IScriptProvider provider,
            ScriptStateMigrationPolicy statePolicy = ScriptStateMigrationPolicy.PreserveWhenCompatible);
        bool RestartCurrentScene(Action<float> progress = null, Action<bool, string> completed = null);
    }
}
