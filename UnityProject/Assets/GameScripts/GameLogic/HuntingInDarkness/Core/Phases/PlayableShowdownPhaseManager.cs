using System;
using System.Collections.Generic;
using HuntingInDarkness.Combat;
using HuntingInDarkness.Data;
using HuntingInDarkness.Settlement;

namespace Core
{
    internal sealed class PlayableShowdownPhaseManager : IDisposable, IPlayableShowdownPhasePort
    {
        private PlayableCombatSession current;
        private bool disposed;

        internal PlayableCombatSession Current => current;

        PlayableCombatSession IPlayableShowdownPhasePort.Current => Current;
        bool IPlayableShowdownPhasePort.TryPrepare(PlayableCombatSessionConfiguration configuration, out string reason) => TryPrepare(configuration, out reason);
        void IPlayableShowdownPhasePort.Start(IReadOnlyList<HunterInstance> hunters, HunterManagementSystem hunterManagement, Action onPartyDefeated) => Start(hunters, hunterManagement, onPartyDefeated);
        void IPlayableShowdownPhasePort.Update() => Update();
        void IPlayableShowdownPhasePort.DisposeCurrent() => DisposeCurrent();

        internal bool TryPrepare(PlayableCombatSessionConfiguration configuration, out string reason)
        {
            ThrowIfDisposed();
            if (current?.IsActive == true)
            {
                reason = string.Empty;
                return true;
            }

            current = null;
            try
            {
                current = new PlayableCombatSession(configuration);
                current.PublishReady();
                reason = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                current?.Dispose();
                current = null;
                reason = $"决战运行态初始化异常：{exception.Message}";
                return false;
            }
        }

        internal void Start(IReadOnlyList<HunterInstance> hunters, HunterManagementSystem hunterManagement, Action onPartyDefeated)
        {
            ThrowIfDisposed();
            current?.Start(hunters, hunterManagement, onPartyDefeated);
        }

        internal void Update()
        {
            if (disposed) return;
            current?.Update();
        }

        internal void DisposeCurrent()
        {
            if (disposed) return;
            current?.Dispose();
            current = null;
        }

        internal void ResetCurrent() => DisposeCurrent();

        public void Dispose()
        {
            if (disposed) return;
            DisposeCurrent();
            disposed = true;
        }

        private void ThrowIfDisposed()
        {
            if (disposed) throw new ObjectDisposedException(nameof(PlayableShowdownPhaseManager));
        }
    }
}
