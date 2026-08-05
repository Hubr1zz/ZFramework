using System;

namespace TEngine.RTS
{
    public interface IScriptScope : IDisposable
    {
        bool IsDisposed { get; }
        void Register(IDisposable resource);
        void Register(Action cleanup);
    }
}
