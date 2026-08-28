using System;
using HuntingInDarkness.ActionFlow.Settlement;
using HuntingInDarkness.Data;
using HuntingInDarkness.Settlement;

namespace Core
{
    public sealed class PlayableSettlementRuntimeConfiguration
    {
        public Func<SettlementManager, PlayableSettlementActionSession> CreateActionSession { get; }

        public PlayableSettlementRuntimeConfiguration(Func<SettlementManager, PlayableSettlementActionSession> createActionSession)
        {
            CreateActionSession = createActionSession ?? throw new ArgumentNullException(nameof(createActionSession));
        }
    }

    public interface IPlayableSettlementRuntime
    {
        long GenerationId { get; }
        SettlementManager Manager { get; }
        SettlementInstance Data { get; }
        PlayableSettlementActionSession ActionSession { get; }
        SettlementEventRestoreProjection EventRestore { get; }
        bool IsActionSessionActive { get; }
        bool IsActionSessionRunning { get; }
        bool TryActivateActionSession(out string reason);
        void DeactivateActionSession();
        SettlementEventRestoreProjection CreateEventRestoreCandidate();
        void PublishEventRestore(SettlementEventRestoreProjection candidate);
        void ClearEventRestore();
    }

    internal sealed class PlayableSettlementRuntime : IPlayableSettlementRuntime, IDisposable
    {
        private enum RuntimeState
        {
            Detached,
            Current,
            Disposed
        }

        private readonly PlayableSettlementPhaseCoordinator coordinator;
        private SettlementEventRestoreProjection eventRestore;
        private RuntimeState state;
        private bool preparedCandidatePending;

        public long GenerationId { get; }
        public SettlementManager Manager { get; }
        public SettlementInstance Data => Manager.Data;
        public PlayableSettlementActionSession ActionSession => coordinator.GetSession(this);
        public SettlementEventRestoreProjection EventRestore => eventRestore;
        public bool IsActionSessionActive => ActionSession?.IsActive == true;
        public bool IsActionSessionRunning => ActionSession?.IsRunning == true;
        internal bool IsCurrent => state == RuntimeState.Current;
        internal bool IsDetached => state == RuntimeState.Detached;

        internal PlayableSettlementRuntime(long generationId, SettlementManager manager, PlayableSettlementRuntimeConfiguration configuration, bool preparedCandidatePending, PlayableSettlementPhaseCoordinator coordinator)
        {
            GenerationId = generationId;
            Manager = manager ?? throw new ArgumentNullException(nameof(manager));
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));
            this.coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
            this.preparedCandidatePending = preparedCandidatePending;
        }

        public bool TryActivateActionSession(out string reason)
        {
            ThrowIfDisposed();
            return coordinator.TryActivate(this, out reason);
        }

        public void DeactivateActionSession()
        {
            if (state == RuntimeState.Disposed) return;
            coordinator.Deactivate(this);
        }

        public SettlementEventRestoreProjection CreateEventRestoreCandidate()
        {
            ThrowIfDisposed();
            return new SettlementEventRestoreProjection(Data, Manager.Timeline.ResolveEvent);
        }

        public void PublishEventRestore(SettlementEventRestoreProjection candidate)
        {
            ThrowIfDisposed();
            eventRestore = candidate ?? throw new ArgumentNullException(nameof(candidate));
        }

        public void ClearEventRestore()
        {
            ThrowIfDisposed();
            eventRestore = null;
        }

        internal bool TryPreparePublication(out string reason)
        {
            ThrowIfDisposed();
            if (!IsDetached)
            {
                reason = "营地运行世代不是可发布候选。";
                return false;
            }
            if (preparedCandidatePending && !Manager.TryConsumePreparedCandidate(out reason)) return false;
            preparedCandidatePending = false;
            reason = string.Empty;
            return true;
        }

        internal void Publish()
        {
            ThrowIfDisposed();
            if (!IsDetached) throw new InvalidOperationException("营地运行世代不是可发布候选。");
            state = RuntimeState.Current;
        }

        internal void Detach()
        {
            ThrowIfDisposed();
            state = RuntimeState.Detached;
        }

        public void Dispose()
        {
            if (state == RuntimeState.Disposed) return;
            DeactivateActionSession();
            eventRestore = null;
            state = RuntimeState.Disposed;
        }

        private void ThrowIfDisposed()
        {
            if (state == RuntimeState.Disposed)
                throw new ObjectDisposedException(nameof(PlayableSettlementRuntime));
        }
    }
}
