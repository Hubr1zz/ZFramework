using System;

namespace TEngine.RTS
{
    public interface IScript : IDisposable
    {
        void Bind(IScriptContext context, IWorldObject owner, string initialConfig);
        void RestoreState(ScriptState state);
        void Start();
        void Tick(in ScriptTime time);
        ScriptState CaptureState();
    }

    // Optional v1 lifecycle used by hot-reload aware scripts. Bind and RestoreState are
    // completed for every staged instance before Activate is called.
    public interface IRtsScriptLifecycleV1
    {
        void Activate(bool isHotReload);
        void Deactivate();
    }
}
