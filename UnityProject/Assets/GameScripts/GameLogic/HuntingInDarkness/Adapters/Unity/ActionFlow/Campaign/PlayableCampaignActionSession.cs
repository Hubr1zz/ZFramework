using System;
using System.Threading;
using CardGame.ActionQueue;
using Core;
using Cysharp.Threading.Tasks;
using GameplayBase;

namespace HuntingInDarkness.ActionFlow.Campaign
{
    public interface ICampaignPhaseTransitionHost
    {
        GamePhase CurrentPhase { get; }
        bool TryApplyPhaseTransition(GamePhase targetPhase, out string reason);
        bool TryBeginEncounter(CampaignEncounterRequest request, out string reason);
    }

    public readonly struct CampaignPhaseTransitionResult
    {
        public CampaignPhaseTransitionResult(bool succeeded, bool changed, GamePhase previousPhase, GamePhase currentPhase, string reason)
        {
            Succeeded = succeeded;
            Changed = changed;
            PreviousPhase = previousPhase;
            CurrentPhase = currentPhase;
            Reason = reason ?? string.Empty;
        }

        public bool Succeeded { get; }
        public bool Changed { get; }
        public GamePhase PreviousPhase { get; }
        public GamePhase CurrentPhase { get; }
        public string Reason { get; }
        public static CampaignPhaseTransitionResult Failed(GamePhase currentPhase, string reason) => new(false, false, currentPhase, currentPhase, reason);
    }

    public struct CampaignPhaseTransitionCommittedEvent
    {
        public GamePhase PreviousPhase;
        public GamePhase CurrentPhase;
    }

    public readonly struct CampaignEncounterStartResult
    {
        public CampaignEncounterStartResult(bool succeeded, string encounterId, string reason)
        {
            Succeeded = succeeded;
            EncounterId = encounterId ?? string.Empty;
            Reason = reason ?? string.Empty;
        }

        public bool Succeeded { get; }
        public string EncounterId { get; }
        public string Reason { get; }
        public static CampaignEncounterStartResult Failed(string encounterId, string reason) => new(false, encounterId, reason);
    }

    public struct CampaignEncounterStartedEvent
    {
        public CampaignEncounterRequest Request;
    }

    /// <summary>随整场战役存活的跨阶段 Runner；阶段内部 Runner 不互相嵌套调用。</summary>
    public sealed class PlayableCampaignActionSession : IDisposable
    {
        private readonly ICampaignPhaseTransitionHost host;
        private readonly ActionEnvironment environment;

        public PlayableCampaignActionSession(ICampaignPhaseTransitionHost host, IActionEnvironmentInstallerRegistry installerRegistry = null)
        {
            this.host = host ?? throw new ArgumentNullException(nameof(host));
            environment = new ActionEnvironment(new ActionEnvironmentConfiguration
            {
                Name = "Campaign",
                Kind = ActionEnvironmentKind.Campaign,
                MaxActionsPerChain = 128,
                TraceCapacity = 48
            }, installerRegistry);
        }

        public bool IsActive => !environment.IsDisposed;
        public ReactorRegistry Reactors => environment.Reactors;
        public ReactionGateRegistry ReactionGates => environment.ReactionGates;

        public async UniTask<CampaignPhaseTransitionResult> TransitionAsync(GamePhase targetPhase, CancellationToken cancellationToken = default)
        {
            if (!IsActive) return CampaignPhaseTransitionResult.Failed(host.CurrentPhase, "战役流程已经结束");
            var outbox = new ActionEventOutbox();
            ReactorEntityHandle campaign = environment.EntityHandles.GetOrCreate("campaign", "active", "当前战役");
            ReactorEntityHandle phase = environment.EntityHandles.GetOrCreate("game-phase", targetPhase.ToString(), targetPhase.ToString());
            var action = new TransitionCampaignPhaseAction(host, targetPhase, outbox, campaign, phase);
            ActionOutcome outcome = await environment.ExecuteAsync(action, outbox, cancellationToken: cancellationToken);
            if (outcome.IsSuccess) return action.Result;
            return CampaignPhaseTransitionResult.Failed(host.CurrentPhase, string.IsNullOrWhiteSpace(action.Result.Reason) ? outcome.Reason : action.Result.Reason);
        }

        public async UniTask<CampaignEncounterStartResult> BeginEncounterAsync(CampaignEncounterRequest request, CancellationToken cancellationToken = default)
        {
            if (!IsActive) return CampaignEncounterStartResult.Failed(request.EncounterId, "战役流程已经结束");
            var outbox = new ActionEventOutbox();
            ReactorEntityHandle campaign = environment.EntityHandles.GetOrCreate("campaign", "active", "当前战役");
            ReactorEntityHandle encounter = environment.EntityHandles.GetOrCreate("encounter", request.EncounterId ?? string.Empty, request.EncounterId ?? "遭遇");
            var action = new BeginCampaignEncounterAction(host, request, outbox, campaign, encounter);
            ActionOutcome outcome = await environment.ExecuteAsync(action, outbox, cancellationToken: cancellationToken);
            if (outcome.IsSuccess) return action.Result;
            return CampaignEncounterStartResult.Failed(request.EncounterId, string.IsNullOrWhiteSpace(action.Result.Reason) ? outcome.Reason : action.Result.Reason);
        }

        public void Dispose() => environment.Dispose();
    }

    /// <summary>验证来源远征、解析遭遇并切入战斗的战役级命令。</summary>
    public sealed class BeginCampaignEncounterAction : CommandAction, ISourceAction, ITargetAction
    {
        private readonly ICampaignPhaseTransitionHost host;
        private readonly CampaignEncounterRequest request;
        private readonly ActionEventOutbox eventOutbox;

        public BeginCampaignEncounterAction(ICampaignPhaseTransitionHost host, CampaignEncounterRequest request, ActionEventOutbox eventOutbox, IReactorEntity source, IReactorEntity target)
        {
            this.host = host ?? throw new ArgumentNullException(nameof(host));
            this.request = request;
            this.eventOutbox = eventOutbox ?? throw new ArgumentNullException(nameof(eventOutbox));
            Source = source ?? throw new ArgumentNullException(nameof(source));
            Target = target ?? throw new ArgumentNullException(nameof(target));
        }

        public CampaignEncounterRequest Request => request;
        public CampaignEncounterStartResult Result { get; private set; }
        public IReactorEntity Source { get; }
        public IReactorEntity Target { get; }

        protected override UniTask<ActionOutcome> ExecuteAsync(ActionExecutionContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!request.IsValid)
            {
                Result = CampaignEncounterStartResult.Failed(request.EncounterId, "遭遇请求缺少有效的来源会话或配置 ID");
                return UniTask.FromResult(ActionOutcome.Failure(Result.Reason));
            }
            if (!host.TryBeginEncounter(request, out string reason))
            {
                Result = CampaignEncounterStartResult.Failed(request.EncounterId, reason);
                return UniTask.FromResult(ActionOutcome.Failure(reason));
            }

            Result = new CampaignEncounterStartResult(true, request.EncounterId, string.Empty);
            eventOutbox.StageAfterCommit(new CampaignEncounterStartedEvent { Request = request });
            return UniTask.FromResult(ActionOutcome.Success());
        }
    }

    /// <summary>跨功能切换的唯一权威入口；Before Reactor 可阻止或注入战役级前置流程。</summary>
    public sealed class TransitionCampaignPhaseAction : CommandAction, ISourceAction, ITargetAction
    {
        private readonly ICampaignPhaseTransitionHost host;
        private readonly GamePhase targetPhase;
        private readonly ActionEventOutbox eventOutbox;

        public TransitionCampaignPhaseAction(ICampaignPhaseTransitionHost host, GamePhase targetPhase, ActionEventOutbox eventOutbox, IReactorEntity source, IReactorEntity target)
        {
            this.host = host ?? throw new ArgumentNullException(nameof(host));
            this.targetPhase = targetPhase;
            this.eventOutbox = eventOutbox ?? throw new ArgumentNullException(nameof(eventOutbox));
            Source = source ?? throw new ArgumentNullException(nameof(source));
            Target = target ?? throw new ArgumentNullException(nameof(target));
        }

        public GamePhase TargetPhase => targetPhase;
        public CampaignPhaseTransitionResult Result { get; private set; }
        public IReactorEntity Source { get; }
        public IReactorEntity Target { get; }

        protected override UniTask<ActionOutcome> ExecuteAsync(ActionExecutionContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            GamePhase previousPhase = host.CurrentPhase;
            if (previousPhase == targetPhase)
            {
                Result = new CampaignPhaseTransitionResult(true, false, previousPhase, previousPhase, string.Empty);
                return UniTask.FromResult(ActionOutcome.Success());
            }
            if (!host.TryApplyPhaseTransition(targetPhase, out string reason))
            {
                Result = CampaignPhaseTransitionResult.Failed(host.CurrentPhase, reason);
                return UniTask.FromResult(ActionOutcome.Failure(reason));
            }

            Result = new CampaignPhaseTransitionResult(true, true, previousPhase, host.CurrentPhase, string.Empty);
            eventOutbox.StageAfterCommit(new CampaignPhaseTransitionCommittedEvent
            {
                PreviousPhase = previousPhase,
                CurrentPhase = host.CurrentPhase
            });
            return UniTask.FromResult(ActionOutcome.Success());
        }
    }
}
