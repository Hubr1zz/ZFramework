using System;
using System.Collections.Generic;
using System.Threading;
using CardGame.ActionQueue;
using Core;
using Cysharp.Threading.Tasks;
using HuntingInDarkness.ActionFlow.Presentation;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Settlement;
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
        public PlayableEventCommitCheckpoint(PlayableEventCommitKind kind, string eventId, int actorId, IReadOnlyList<string> chainedEventIds = null, IReadOnlyList<EventData> chainedEvents = null)
        {
            Kind = kind;
            EventId = eventId ?? string.Empty;
            ActorId = actorId;
            ChainedEventIds = chainedEventIds ?? Array.Empty<string>();
            ChainedEvents = chainedEvents ?? Array.Empty<EventData>();
        }

        public PlayableEventCommitKind Kind { get; }
        public string EventId { get; }
        public int ActorId { get; }
        public IReadOnlyList<string> ChainedEventIds { get; }
        public IReadOnlyList<EventData> ChainedEvents { get; }
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
        private readonly IPlayableEventResourceCommand resourceCommand;
        private readonly IPlayableEventResourceAvailability resourceAvailability;
        private readonly IPlayableEventWorldCommand worldCommand;
        private readonly IPlayableEventSettlementCommand settlementCommand;

        public ResolvePlayableEventNodeAction(EventSystem eventSystem, IPlayableEventInput eventInput, EventData gameEvent, HunterInstance defaultActor, IReadOnlyList<HunterInstance> hunters, ActionEventOutbox eventOutbox, Action<PlayableEventCommitCheckpoint> stageCommitCheckpoint, IReactorEntity source, IReactorEntity target, ITabletopRandomInteractionPresenter randomInteractionPresenter = null, IPlayableEventResourceCommand resourceCommand = null, IPlayableEventWorldCommand worldCommand = null, IPlayableEventSettlementCommand settlementCommand = null, IPlayableEventResourceAvailability resourceAvailability = null)
        {
            this.eventSystem = eventSystem ?? throw new ArgumentNullException(nameof(eventSystem));
            this.eventInput = eventInput;
            this.gameEvent = gameEvent ?? throw new ArgumentNullException(nameof(gameEvent));
            this.defaultActor = defaultActor;
            this.hunters = hunters ?? Array.Empty<HunterInstance>();
            this.eventOutbox = eventOutbox ?? throw new ArgumentNullException(nameof(eventOutbox));
            this.stageCommitCheckpoint = stageCommitCheckpoint;
            this.randomInteractionPresenter = randomInteractionPresenter;
            this.resourceCommand = resourceCommand;
            this.resourceAvailability = resourceAvailability ?? (IPlayableEventResourceAvailability)resourceCommand ?? new SettlementEventResourceAvailability(eventSystem.Settlement);
            this.worldCommand = worldCommand;
            this.settlementCommand = settlementCommand;
            Source = source ?? throw new ArgumentNullException(nameof(source));
            Target = target ?? throw new ArgumentNullException(nameof(target));
        }

        public IReadOnlyList<EventData> ChainedEvents { get; private set; } = Array.Empty<EventData>();
        public IReadOnlyList<string> EncounterIds { get; private set; } = Array.Empty<string>();
        public PlayableEventEffectBatchResult EffectResults { get; private set; } = PlayableEventEffectBatchResult.Empty;
        public bool ResolutionCheckpointPublished { get; private set; }
        public string EventId => gameEvent.ContentId;
        public EventData GameEvent => gameEvent;
        public HunterInstance DefaultActor => defaultActor;
        public IReadOnlyList<HunterInstance> CandidateHunters => hunters;
        public IReactorEntity Source { get; }
        public IReactorEntity Target { get; }

        protected override async UniTask<ActionOutcome> ExecuteAsync(ActionExecutionContext context, CancellationToken cancellationToken)
        {
            try
            {
                return await ExecuteCoreAsync(context, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                return ActionOutcome.Failure(string.IsNullOrWhiteSpace(exception.Message) ? "事件节点执行失败" : exception.Message);
            }
        }

        private async UniTask<ActionOutcome> ExecuteCoreAsync(ActionExecutionContext context, CancellationToken cancellationToken)
        {
            eventOutbox.Stage(new GameEventTriggeredEvent { EventId = gameEvent.ContentId });
            eventOutbox.PublishCheckpoint();
            if (gameEvent.eventType != GameEventType.Choice || gameEvent.options == null || gameEvent.options.Count == 0)
            {
                if (eventInput != null)
                    await eventInput.ConfirmNarrativeAsync(gameEvent, defaultActor, cancellationToken);
                PlayableEventNodeCommitResult narrativeResult = eventSystem.ResolveNarrativeNodeStandalone(gameEvent, defaultActor, resourceCommand, worldCommand, settlementCommand);
                ChainedEvents = narrativeResult.ChainedEvents;
                EncounterIds = narrativeResult.EncounterIds;
                EffectResults = narrativeResult.EffectResults;
                PublishCommitCheckpoint(PlayableEventCommitKind.Resolution, defaultActor, ChainedEvents);
                return ActionOutcome.Success();
            }

            bool hasPlayerInput = eventInput != null;
            PlayableEventChoiceSelection selection = hasPlayerInput
                ? await eventInput.SelectChoiceAsync(gameEvent, defaultActor, hunters, resourceAvailability, cancellationToken)
                : FindAutomaticSelection();
            if (!IsAllowedActor(selection.Actor))
                selection = new PlayableEventChoiceSelection(-1, null);
            PlayableEventChoiceTransaction transaction = selection.IsValid ? await PrepareChoiceAsync(selection, cancellationToken) : null;
            if (selection.IsValid && transaction == null && hasPlayerInput)
                return ActionOutcome.Failure("事件选项条件已经变化，请重新选择。");
            if (transaction == null)
            {
                selection = FindAutomaticSelection();
                transaction = selection.IsValid ? await PrepareChoiceAsync(selection, cancellationToken) : null;
            }
            if (transaction == null)
            {
                PlayableEventNodeCommitResult fallbackResult = eventSystem.ResolveNarrativeNodeStandalone(gameEvent, defaultActor, resourceCommand, worldCommand, settlementCommand);
                ChainedEvents = fallbackResult.ChainedEvents;
                EncounterIds = fallbackResult.EncounterIds;
                EffectResults = fallbackResult.EffectResults;
                PublishCommitCheckpoint(PlayableEventCommitKind.Resolution, defaultActor, ChainedEvents);
                return ActionOutcome.Success();
            }

            while (transaction.RequiresCheck && eventInput != null)
            {
                PlayableEventCheckDecision decision = await eventInput.PresentCheckAsync(transaction, cancellationToken);
                if (decision != PlayableEventCheckDecision.Reroll) break;
                if (!transaction.CanReroll) break;
                int? rerollValue = randomInteractionPresenter != null ? await ResolveTabletopCheckAsync(transaction.Option, transaction.Actor, "reroll", cancellationToken) : null;
                if (!transaction.TryReroll(rerollValue)) break;
                PublishCommitCheckpoint(PlayableEventCommitKind.Reroll, transaction.Actor);
            }
            PlayableEventCommitResult result = transaction.CommitStandalone(true);
            bool campaignEnded = eventSystem.Settlement.GetAliveHunters().Count == 0;
            ChainedEvents = campaignEnded ? System.Array.Empty<EventData>() : result.ChainedEvents;
            EncounterIds = campaignEnded ? System.Array.Empty<string>() : result.EncounterIds;
            EffectResults = result.EffectResults;
            PublishCommitCheckpoint(PlayableEventCommitKind.Resolution, transaction.Actor, ChainedEvents);
            if (eventInput != null && !campaignEnded)
                await eventInput.ConfirmResultAsync(gameEvent, result.Result, cancellationToken);
            return ActionOutcome.Success();
        }

        private async UniTask<PlayableEventChoiceTransaction> PrepareChoiceAsync(PlayableEventChoiceSelection selection, CancellationToken cancellationToken)
        {
            if (selection.OptionIndex < 0 || selection.OptionIndex >= gameEvent.options.Count) return null;
            EventOption option = gameEvent.options[selection.OptionIndex];
            if (!PlayableEventOptionAvailability.CanUse(option, selection.Actor, eventSystem.Settlement, resourceAvailability, out _)) return null;
            int? rollValue = option.checkType != CheckType.None && randomInteractionPresenter != null ? await ResolveTabletopCheckAsync(option, selection.Actor, "initial", cancellationToken) : null;
            return eventSystem.PrepareChoice(gameEvent, selection.OptionIndex, selection.Actor, rollValue, resourceCommand, worldCommand, settlementCommand, resourceAvailability);
        }

        private async UniTask<int> ResolveTabletopCheckAsync(EventOption option, HunterInstance actor, string step, CancellationToken cancellationToken)
        {
            string actorId = actor != null ? actor.InstanceId.ToString() : string.Empty;
            TabletopRandomInteractionKind kind = option.checkPresentation switch
            {
                EventCheckPresentationKind.DrawCards => TabletopRandomInteractionKind.DrawCards,
                EventCheckPresentationKind.FlipCards => TabletopRandomInteractionKind.FlipCards,
                EventCheckPresentationKind.OldMaid => TabletopRandomInteractionKind.OldMaid,
                _ => TabletopRandomInteractionKind.PhysicalDice
            };
            string instruction = string.IsNullOrWhiteSpace(option.checkInstruction) ? DefaultInstruction(kind) : option.checkInstruction;
            var request = new TabletopRandomInteractionRequest($"event:{gameEvent.ContentId}:{actorId}:{step}:{Guid.NewGuid():N}", kind, actorId, gameEvent.ContentId, option.checkCount, option.checkSides, option.checkDeckId, instruction);
            TabletopRandomInteractionResult result = await randomInteractionPresenter.PresentAsync(request, cancellationToken);
            if (result.Cancelled)
                throw new OperationCanceledException("玩家取消了桌面随机交互。", cancellationToken);
            if (!TabletopRandomInteractionResultValidator.TryGetCheckTotal(request, result, out int total))
                throw new InvalidOperationException("桌面随机交互没有返回有效的事件判定结果。");
            return total;
        }

        private static string DefaultInstruction(TabletopRandomInteractionKind kind)
        {
            return kind switch
            {
                TabletopRandomInteractionKind.DrawCards => "从牌堆抽取事件判定牌",
                TabletopRandomInteractionKind.FlipCards => "选择并翻开事件判定牌",
                TabletopRandomInteractionKind.OldMaid => "抽取一张牌，避开鬼牌",
                _ => "投掷事件判定骰"
            };
        }

        private PlayableEventChoiceSelection FindAutomaticSelection()
        {
            for (int optionIndex = 0; optionIndex < gameEvent.options.Count; optionIndex++)
            {
                EventOption option = gameEvent.options[optionIndex];
                bool needsHunter = option.checkType != CheckType.None || PlayableEventOptionAvailability.RequiresHunter(option);
                if (defaultActor != null && PlayableEventOptionAvailability.CanUse(option, defaultActor, eventSystem.Settlement, resourceAvailability, out _))
                    return new PlayableEventChoiceSelection(optionIndex, defaultActor);
                if (!needsHunter && PlayableEventOptionAvailability.CanUse(option, null, eventSystem.Settlement, resourceAvailability, out _))
                    return new PlayableEventChoiceSelection(optionIndex, null);
                foreach (HunterInstance hunter in hunters)
                    if (PlayableEventOptionAvailability.CanUse(option, hunter, eventSystem.Settlement, resourceAvailability, out _))
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

        private void PublishCommitCheckpoint(PlayableEventCommitKind kind, HunterInstance actor, IReadOnlyList<EventData> chainedEvents = null)
        {
            if (kind == PlayableEventCommitKind.Resolution)
                StageCommittedEffectFacts();
            var chainedEventIds = new List<string>();
            if (chainedEvents != null)
                foreach (EventData chainedEvent in chainedEvents)
                {
                    string eventId = chainedEvent?.ContentId ?? string.Empty;
                    if (eventId.Length > 0) chainedEventIds.Add(eventId);
                }
            stageCommitCheckpoint?.Invoke(new PlayableEventCommitCheckpoint(kind, gameEvent.ContentId, actor?.InstanceId ?? 0, chainedEventIds, chainedEvents));
            if (kind == PlayableEventCommitKind.Resolution)
                ResolutionCheckpointPublished = true;
            eventOutbox.PublishCheckpoint();
        }

        private void StageCommittedEffectFacts()
        {
            foreach (PlayableEventEffectResult effect in EffectResults.Effects)
            {
                if (!effect.Succeeded || !effect.StateChanged || effect.TargetActorId <= 0 || string.IsNullOrWhiteSpace(effect.ResolvedTargetId)) continue;
                if (effect.EffectType == EventEffectType.AddAilment)
                {
                    string symptomName = PlayableSymptomRuntime.Catalog != null && PlayableSymptomRuntime.Catalog.TryGetById(effect.ResolvedTargetId, out SymptomDefinition definition) ? definition.DisplayName : string.Empty;
                    eventOutbox.Stage(new HunterSymptomAcquiredEvent { SourceEventId = effect.EventId, EffectIndex = effect.EffectIndex, HunterId = effect.TargetActorId, SymptomId = effect.ResolvedTargetId, SymptomName = symptomName });
                }
                if (effect.EffectType == EventEffectType.AddRecoverableWound)
                    eventOutbox.Stage(new HunterWoundedEvent { SourceEventId = effect.EventId, EffectIndex = effect.EffectIndex, HunterId = effect.TargetActorId, BodyPartId = effect.ResolvedTargetId, PreviousHealth = effect.PreviousValue, CurrentHealth = effect.CurrentValue });
            }
        }
    }
}
