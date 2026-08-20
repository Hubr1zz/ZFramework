using System;
using System.Collections.Generic;
using System.Threading;
using CardGame.ActionQueue;
using Core;
using Cysharp.Threading.Tasks;
using HuntingInDarkness.ActionFlow.Presentation;
using HuntingInDarkness.Data;
using HuntingInDarkness.Settlement;

namespace HuntingInDarkness.ActionFlow.Events
{
    public enum PlayableEventCommitKind
    {
        Reroll,
        Resolution
    }

    public readonly struct PlayableEventCommitCheckpoint
    {
        public PlayableEventCommitCheckpoint(PlayableEventCommitKind kind, string eventId, int actorId)
        {
            Kind = kind;
            EventId = eventId ?? string.Empty;
            ActorId = actorId;
        }

        public PlayableEventCommitKind Kind { get; }
        public string EventId { get; }
        public int ActorId { get; }
    }

    /// <summary>跨阶段复用的单事件节点；阶段 Runner 只注入提交事实，不复制选择、重投与效果流程。</summary>
    public sealed class ResolvePlayableEventNodeAction : CommandAction, ISourceAction, ITargetAction
    {
        private readonly EventSystem eventSystem;
        private readonly IPlayableEventInput eventInput;
        private readonly EventData gameEvent;
        private readonly HunterInstance defaultActor;
        private readonly IReadOnlyList<HunterInstance> hunters;
        private readonly ActionEventOutbox eventOutbox;
        private readonly Action<PlayableEventCommitCheckpoint> stageCommitCheckpoint;
        private readonly ITabletopRandomInteractionPresenter randomInteractionPresenter;

        public ResolvePlayableEventNodeAction(EventSystem eventSystem, IPlayableEventInput eventInput, EventData gameEvent, HunterInstance defaultActor, IReadOnlyList<HunterInstance> hunters, ActionEventOutbox eventOutbox, Action<PlayableEventCommitCheckpoint> stageCommitCheckpoint, IReactorEntity source, IReactorEntity target, ITabletopRandomInteractionPresenter randomInteractionPresenter = null)
        {
            this.eventSystem = eventSystem ?? throw new ArgumentNullException(nameof(eventSystem));
            this.eventInput = eventInput;
            this.gameEvent = gameEvent ?? throw new ArgumentNullException(nameof(gameEvent));
            this.defaultActor = defaultActor;
            this.hunters = hunters ?? Array.Empty<HunterInstance>();
            this.eventOutbox = eventOutbox ?? throw new ArgumentNullException(nameof(eventOutbox));
            this.stageCommitCheckpoint = stageCommitCheckpoint;
            this.randomInteractionPresenter = randomInteractionPresenter;
            Source = source ?? throw new ArgumentNullException(nameof(source));
            Target = target ?? throw new ArgumentNullException(nameof(target));
        }

        public IReadOnlyList<EventData> ChainedEvents { get; private set; } = Array.Empty<EventData>();
        public IReadOnlyList<string> EncounterIds { get; private set; } = Array.Empty<string>();
        public string EventId => gameEvent.name;
        public EventData GameEvent => gameEvent;
        public HunterInstance DefaultActor => defaultActor;
        public IReadOnlyList<HunterInstance> CandidateHunters => hunters;
        public IReactorEntity Source { get; }
        public IReactorEntity Target { get; }

        protected override async UniTask<ActionOutcome> ExecuteAsync(ActionExecutionContext context, CancellationToken cancellationToken)
        {
            eventOutbox.Stage(new GameEventTriggeredEvent { EventId = gameEvent.name });
            eventOutbox.PublishCheckpoint();
            if (gameEvent.eventType != GameEventType.Choice || gameEvent.options == null || gameEvent.options.Count == 0)
            {
                if (eventInput != null)
                    await eventInput.ConfirmNarrativeAsync(gameEvent, defaultActor, cancellationToken);
                PlayableEventNodeCommitResult narrativeResult = eventSystem.ResolveNarrativeNodeStandalone(gameEvent, defaultActor);
                ChainedEvents = narrativeResult.ChainedEvents;
                EncounterIds = narrativeResult.EncounterIds;
                PublishCommitCheckpoint(PlayableEventCommitKind.Resolution, defaultActor);
                return ActionOutcome.Success();
            }

            PlayableEventChoiceSelection selection = eventInput != null
                ? await eventInput.SelectChoiceAsync(gameEvent, defaultActor, hunters, cancellationToken)
                : FindAutomaticSelection();
            if (!IsAllowedActor(selection.Actor))
                selection = new PlayableEventChoiceSelection(-1, null);
            PlayableEventChoiceTransaction transaction = selection.IsValid ? await PrepareChoiceAsync(selection, cancellationToken) : null;
            if (transaction == null)
            {
                selection = FindAutomaticSelection();
                transaction = selection.IsValid ? await PrepareChoiceAsync(selection, cancellationToken) : null;
            }
            if (transaction == null)
            {
                PlayableEventNodeCommitResult fallbackResult = eventSystem.ResolveNarrativeNodeStandalone(gameEvent, defaultActor);
                ChainedEvents = fallbackResult.ChainedEvents;
                EncounterIds = fallbackResult.EncounterIds;
                PublishCommitCheckpoint(PlayableEventCommitKind.Resolution, defaultActor);
                return ActionOutcome.Success();
            }

            while (transaction.RequiresCheck && eventInput != null)
            {
                PlayableEventCheckDecision decision = await eventInput.PresentCheckAsync(transaction, cancellationToken);
                if (decision != PlayableEventCheckDecision.Reroll) break;
                if (!transaction.CanReroll) break;
                int? rerollValue = randomInteractionPresenter != null ? await RollPhysicalDiceAsync(transaction.Actor, "reroll", cancellationToken) : null;
                if (!transaction.TryReroll(rerollValue)) break;
                PublishCommitCheckpoint(PlayableEventCommitKind.Reroll, transaction.Actor);
            }
            PlayableEventCommitResult result = transaction.CommitStandalone(true);
            ChainedEvents = result.ChainedEvents;
            EncounterIds = result.EncounterIds;
            PublishCommitCheckpoint(PlayableEventCommitKind.Resolution, transaction.Actor);
            if (eventInput != null)
                await eventInput.ConfirmResultAsync(gameEvent, result.Result, cancellationToken);
            return ActionOutcome.Success();
        }

        private async UniTask<PlayableEventChoiceTransaction> PrepareChoiceAsync(PlayableEventChoiceSelection selection, CancellationToken cancellationToken)
        {
            if (selection.OptionIndex < 0 || selection.OptionIndex >= gameEvent.options.Count) return null;
            EventOption option = gameEvent.options[selection.OptionIndex];
            if (!PlayableEventOptionAvailability.CanUse(option, selection.Actor, eventSystem.Settlement, out _)) return null;
            int? rollValue = option.checkType != CheckType.None && randomInteractionPresenter != null ? await RollPhysicalDiceAsync(selection.Actor, "initial", cancellationToken) : null;
            return eventSystem.PrepareChoice(gameEvent, selection.OptionIndex, selection.Actor, rollValue);
        }

        private async UniTask<int> RollPhysicalDiceAsync(HunterInstance actor, string step, CancellationToken cancellationToken)
        {
            string actorId = actor != null ? actor.InstanceId.ToString() : string.Empty;
            var request = new TabletopRandomInteractionRequest($"event:{gameEvent.name}:{actorId}:{step}:{Guid.NewGuid():N}", TabletopRandomInteractionKind.PhysicalDice, actorId, gameEvent.name, 1, 10, instruction: "投掷事件判定骰");
            TabletopRandomInteractionResult result = await randomInteractionPresenter.PresentAsync(request, cancellationToken);
            if (result.Cancelled)
                throw new OperationCanceledException("玩家取消了桌面随机交互。", cancellationToken);
            if (!TabletopRandomInteractionResultValidator.TryGetDiceTotal(request, result, out int total))
                throw new InvalidOperationException("物理骰子没有返回有效的事件判定结果。");
            return total;
        }

        private PlayableEventChoiceSelection FindAutomaticSelection()
        {
            for (int optionIndex = 0; optionIndex < gameEvent.options.Count; optionIndex++)
            {
                EventOption option = gameEvent.options[optionIndex];
                bool needsHunter = option.checkType != CheckType.None || PlayableEventOptionAvailability.RequiresHunter(option);
                if (defaultActor != null && PlayableEventOptionAvailability.CanUse(option, defaultActor, eventSystem.Settlement, out _))
                    return new PlayableEventChoiceSelection(optionIndex, defaultActor);
                if (!needsHunter && PlayableEventOptionAvailability.CanUse(option, null, eventSystem.Settlement, out _))
                    return new PlayableEventChoiceSelection(optionIndex, null);
                foreach (HunterInstance hunter in hunters)
                    if (PlayableEventOptionAvailability.CanUse(option, hunter, eventSystem.Settlement, out _))
                        return new PlayableEventChoiceSelection(optionIndex, hunter);
            }
            return new PlayableEventChoiceSelection(-1, null);
        }

        private bool IsAllowedActor(HunterInstance actor)
        {
            if (actor == null || ReferenceEquals(actor, defaultActor)) return true;
            foreach (HunterInstance hunter in hunters)
                if (ReferenceEquals(hunter, actor))
                    return true;
            return false;
        }

        private void PublishCommitCheckpoint(PlayableEventCommitKind kind, HunterInstance actor)
        {
            stageCommitCheckpoint?.Invoke(new PlayableEventCommitCheckpoint(kind, gameEvent.name, actor?.InstanceId ?? 0));
            eventOutbox.PublishCheckpoint();
        }
    }
}
