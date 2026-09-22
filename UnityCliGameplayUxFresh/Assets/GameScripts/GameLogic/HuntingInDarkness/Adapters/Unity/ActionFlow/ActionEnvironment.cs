using System;
using System.Collections.Generic;
using System.Threading;
using CardGame.ActionQueue;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace HuntingInDarkness.ActionFlow
{
    public enum ActionEnvironmentKind
    {
        Campaign,
        Settlement,
        Hunt,
        Combat
    }

    /// <summary>一个大功能的 ActionQueue 配置；数值由组合根或启动资产提供。</summary>
    public sealed class ActionEnvironmentConfiguration
    {
        public string Name { get; set; }
        public ActionEnvironmentKind Kind { get; set; }
        public int MaxActionsPerChain { get; set; } = 128;
        public int TraceCapacity { get; set; } = 24;
        public ActionQueueLogLevel LogLevel { get; set; } = ActionQueueLogLevel.WarningsAndErrors;
        public bool SkipPresentationWaits { get; set; }
    }

    public interface IActionEnvironment : IDisposable
    {
        string Name { get; }
        ActionEnvironmentKind Kind { get; }
        bool IsDisposed { get; }
        bool IsRunning { get; }
        int PendingRootCount { get; }
        CancellationToken LifetimeToken { get; }
        ReactorRegistry Reactors { get; }
        ReactionGateRegistry ReactionGates { get; }
        ActionEngineGuardSet EngineGuards { get; }
        ReactorEntityHandleRegistry EntityHandles { get; }

        UniTask<ActionOutcome> ExecuteAsync(GameAction rootAction, ActionEventOutbox eventOutbox = null, IReadOnlyList<IGameActionReactor> chainReactors = null, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// 战役、营地、狩猎或战斗各自拥有的执行边界。它不承载领域规则，只统一队列、
    /// Reactor、实体身份、生命周期取消与提交后事件发布。
    /// </summary>
    public sealed class ActionEnvironment : IActionEnvironment
    {
        private readonly CancellationTokenSource lifetimeCancellation = new();
        private readonly ActionQueueEngine engine;
        private readonly IDisposable installerAttachment;
        private bool disposed;

        public ActionEnvironment(ActionEnvironmentConfiguration configuration, IActionEnvironmentInstallerRegistry installerRegistry = null)
        {
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));
            if (configuration.MaxActionsPerChain < 1) throw new ArgumentOutOfRangeException(nameof(configuration.MaxActionsPerChain));
            if (configuration.TraceCapacity < 4) throw new ArgumentOutOfRangeException(nameof(configuration.TraceCapacity));

            Name = string.IsNullOrWhiteSpace(configuration.Name) ? configuration.Kind.ToString() : configuration.Name.Trim();
            Kind = configuration.Kind;
            EntityHandles = new ReactorEntityHandleRegistry(Name);
            engine = new ActionQueueEngine(new ActionQueueOptions
            {
                MaxActionsPerChain = configuration.MaxActionsPerChain,
                TraceCapacity = configuration.TraceCapacity,
                LogLevel = configuration.LogLevel,
                SkipPresentationWaits = configuration.SkipPresentationWaits
            });
            try
            {
                installerAttachment = installerRegistry?.Attach(this);
            }
            catch
            {
                disposed = true;
                engine.Dispose();
                EntityHandles.Dispose();
                lifetimeCancellation.Dispose();
                throw;
            }
        }

        public string Name { get; }
        public ActionEnvironmentKind Kind { get; }
        public bool IsDisposed => disposed;
        public bool IsRunning => !disposed && engine.IsRunning;
        public int PendingRootCount => disposed ? 0 : engine.PendingRootCount;
        public CancellationToken LifetimeToken
        {
            get
            {
                ThrowIfDisposed();
                return lifetimeCancellation.Token;
            }
        }
        public ReactorRegistry Reactors
        {
            get
            {
                ThrowIfDisposed();
                return engine.Reactors;
            }
        }
        public ReactionGateRegistry ReactionGates
        {
            get
            {
                ThrowIfDisposed();
                return engine.ReactionGates;
            }
        }
        public ActionEngineGuardSet EngineGuards
        {
            get
            {
                ThrowIfDisposed();
                return engine.EngineGuards;
            }
        }
        public ReactorEntityHandleRegistry EntityHandles { get; }

        public async UniTask<ActionOutcome> ExecuteAsync(GameAction rootAction, ActionEventOutbox eventOutbox = null, IReadOnlyList<IGameActionReactor> chainReactors = null, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (rootAction == null) throw new ArgumentNullException(nameof(rootAction));
            eventOutbox?.Claim();

            using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetimeCancellation.Token, cancellationToken);
            ActionOutcome outcome;
            try
            {
                outcome = await engine.Enqueue(rootAction, chainReactors, linkedCancellation.Token);
            }
            catch
            {
                eventOutbox?.Discard();
                throw;
            }

            if (outcome.IsSuccess && !disposed)
                eventOutbox?.Commit();
            else
                eventOutbox?.Discard();
            return outcome;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            ReleaseInstallerAttachment();
            try
            {
                lifetimeCancellation.Cancel();
            }
            catch (AggregateException exception)
            {
                // 单个 Action 的取消回调不得阻止环境继续释放；队列仍会在 Dispose 中再次清理。
                Debug.LogWarning($"[ActionEnvironment:{Name}] 取消回调抛出异常，已继续执行环境清理。\n{exception}");
            }
            finally
            {
                engine.Dispose();
                EntityHandles.Dispose();
                lifetimeCancellation.Dispose();
            }
        }

        private void ReleaseInstallerAttachment()
        {
            try
            {
                installerAttachment?.Dispose();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(Name);
        }
    }
}
