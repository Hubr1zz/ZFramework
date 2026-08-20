using System;
using System.Collections.Generic;
using CardGame.ActionQueue;
using Core;
using HuntingInDarkness.ActionFlow.Events;
using HuntingInDarkness.ActionFlow.Presentation;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Settlement;
using HuntingInDarkness.Settlement;

namespace HuntingInDarkness.ActionFlow.Settlement
{
    public readonly struct SettlementInventionActiveEffectCommandResult
    {
        private SettlementInventionActiveEffectCommandResult(bool succeeded, string reason, string effectId, int year, int useCount)
        {
            Succeeded = succeeded;
            Reason = reason ?? string.Empty;
            EffectId = effectId ?? string.Empty;
            Year = year;
            UseCount = useCount;
        }

        public bool Succeeded { get; }
        public string Reason { get; }
        public string EffectId { get; }
        public int Year { get; }
        public int UseCount { get; }
        public static SettlementInventionActiveEffectCommandResult Success(string effectId, int year, int useCount) => new(true, string.Empty, effectId, year, useCount);
        public static SettlementInventionActiveEffectCommandResult Failed(string reason) => new(false, reason, string.Empty, 0, 0);
    }

    public struct SettlementInventionActiveEffectUsedEvent
    {
        public string InventionId;
        public string InventionName;
        public string EffectId;
        public string EffectName;
        public int Year;
        public int UseCount;
    }

    /// <summary>把一次发明主动效果包装为 Settlement Root，并复用现有事件链作为可注入子流程。</summary>
    public sealed class ActivateSettlementInventionEffectAction : CompositeGameAction, ISourceAction, ITargetAction
    {
        private readonly SettlementInstance settlement;
        private readonly InventionSystem inventionSystem;
        private readonly InventionData invention;
        private readonly InventionActiveEffect effect;
        private readonly EventData gameEvent;
        private readonly EventSystem eventSystem;
        private readonly IPlayableEventInput eventInput;
        private readonly Guid sessionId;
        private readonly ActionEventOutbox eventOutbox;
        private readonly Func<EventData, IReactorEntity> resolveEventEntity;
        private readonly ITabletopRandomInteractionPresenter randomInteractionPresenter;
        private ResolveSettlementEventChainAction eventChain;
        private string failureReason;
        private int activationYear;
        private bool committed;

        public ActivateSettlementInventionEffectAction(SettlementInstance settlement, InventionSystem inventionSystem, InventionData invention, InventionActiveEffect effect, EventData gameEvent, EventSystem eventSystem, IPlayableEventInput eventInput, Guid sessionId, ActionEventOutbox eventOutbox, IReactorEntity source, IReactorEntity target, Func<EventData, IReactorEntity> resolveEventEntity, ITabletopRandomInteractionPresenter randomInteractionPresenter = null)
        {
            this.settlement = settlement ?? throw new ArgumentNullException(nameof(settlement));
            this.inventionSystem = inventionSystem ?? throw new ArgumentNullException(nameof(inventionSystem));
            this.invention = invention;
            this.effect = effect;
            this.gameEvent = gameEvent;
            this.eventSystem = eventSystem ?? throw new ArgumentNullException(nameof(eventSystem));
            this.eventInput = eventInput;
            this.sessionId = sessionId;
            this.eventOutbox = eventOutbox ?? throw new ArgumentNullException(nameof(eventOutbox));
            this.resolveEventEntity = resolveEventEntity ?? throw new ArgumentNullException(nameof(resolveEventEntity));
            this.randomInteractionPresenter = randomInteractionPresenter;
            Source = source ?? throw new ArgumentNullException(nameof(source));
            Target = target ?? throw new ArgumentNullException(nameof(target));
        }

        public SettlementInventionActiveEffectCommandResult Result { get; private set; }
        public InventionData Invention => invention;
        public InventionActiveEffect Effect => effect;
        public IReactorEntity Source { get; }
        public IReactorEntity Target { get; }

        protected override GameAction GetNextChild(CompositeExecutionContext context)
        {
            if (eventChain != null)
            {
                if (!context.LastOutcome.IsSuccess)
                {
                    failureReason = context.LastOutcome.Reason;
                    eventChain = null;
                    return null;
                }
                CommitUse();
                eventChain = null;
                return null;
            }

            if (!TryValidate(out failureReason))
                return null;
            activationYear = settlement.CurrentYear;
            eventChain = new ResolveSettlementEventChainAction(eventSystem, eventInput, new[] { gameEvent }, sessionId, eventOutbox, Source, Target, resolveEventEntity, randomInteractionPresenter);
            return eventChain;
        }

        protected override ActionOutcome Resolve(CompositeExecutionContext context)
        {
            if (!string.IsNullOrWhiteSpace(failureReason))
            {
                Result = SettlementInventionActiveEffectCommandResult.Failed(failureReason);
                return ActionOutcome.Failure(failureReason);
            }
            if (!committed)
            {
                Result = SettlementInventionActiveEffectCommandResult.Failed("主动效果没有完成提交。");
                return ActionOutcome.Failure(Result.Reason);
            }
            return ActionOutcome.Success();
        }

        private bool TryValidate(out string reason)
        {
            if (invention == null || !inventionSystem.AllInventions.Contains(invention))
            {
                reason = "发明不属于当前营地。";
                return false;
            }
            if (effect == null || invention.activeEffects == null || !invention.activeEffects.Contains(effect))
            {
                reason = "主动效果不属于该发明。";
                return false;
            }
            bool eventAvailable = gameEvent != null && gameEvent.category == EventCategory.Triggered && string.Equals(gameEvent.name, effect.eventId, StringComparison.Ordinal);
            return InventionActiveEffectRules.CanActivate(inventionSystem.IsUnlocked(invention), settlement.CurrentYear, effect.effectId, effect.eventId, effect.maxUsesPerYear, settlement.InventionActiveEffectUses, eventAvailable, out reason);
        }

        private void CommitUse()
        {
            settlement.InventionActiveEffectUses ??= new List<InventionActiveEffectUsage>();
            InventionActiveEffectRules.RecordUse(settlement.InventionActiveEffectUses, effect.effectId, activationYear);
            int useCount = InventionActiveEffectRules.GetUseCount(settlement.InventionActiveEffectUses, effect.effectId, activationYear);
            Result = SettlementInventionActiveEffectCommandResult.Success(effect.effectId, activationYear, useCount);
            eventOutbox.Stage(new SettlementInventionActiveEffectUsedEvent { InventionId = invention.ContentId, InventionName = invention.inventionName, EffectId = effect.effectId, EffectName = effect.effectName, Year = activationYear, UseCount = useCount });
            eventOutbox.Stage(new SettlementTransactionCommittedEvent { TransactionId = $"invention-effect:{effect.effectId}:{activationYear}:{useCount}", Kind = SettlementTransactionKind.InventionActivation });
            committed = true;
        }
    }
}
