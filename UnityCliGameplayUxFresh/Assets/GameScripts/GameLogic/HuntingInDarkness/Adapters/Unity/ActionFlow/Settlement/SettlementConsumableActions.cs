using System;
using System.Collections.Generic;
using System.Threading;
using CardGame.ActionQueue;
using Core;
using Cysharp.Threading.Tasks;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Hunters;
using HuntingInDarkness.GameCore.Settlement;
using HuntingInDarkness.Settlement;

namespace HuntingInDarkness.ActionFlow.Settlement
{
    public readonly struct ConsumableUsePlan
    {
        public ConsumableUsePlan(ItemData item, ConsumableEffectKind effect, int amount)
        {
            Item = item;
            Effect = effect;
            Amount = amount;
        }

        public ItemData Item { get; }
        public ConsumableEffectKind Effect { get; }
        public int Amount { get; }
    }

    public interface ISettlementConsumableContent
    {
        bool TryGet(ItemData item, out ConsumableUsePlan plan);
    }

    public sealed class PlayableSettlementConsumableContentAdapter : ISettlementConsumableContent
    {
        private readonly IReadOnlyList<ItemData> items;

        public PlayableSettlementConsumableContentAdapter(IReadOnlyList<ItemData> items) => this.items = items ?? Array.Empty<ItemData>();

        public bool TryGet(ItemData item, out ConsumableUsePlan plan)
        {
            if (item != null && item.itemType == ItemType.Consumable && item.ConsumableEffect != ConsumableEffectKind.None && item.ConsumableEffectAmount > 0 && item.ConsumableEffectAmount <= 99 && item.HuntNoise == 0)
                foreach (ItemData candidate in items)
                    if (ReferenceEquals(candidate, item))
                    {
                        plan = new ConsumableUsePlan(item, item.ConsumableEffect, item.ConsumableEffectAmount);
                        return true;
                    }
            plan = default;
            return false;
        }
    }

    public readonly struct SettlementConsumableCommandResult
    {
        public SettlementConsumableCommandResult(bool succeeded, string reason, int hunterId, string itemId, HunterRecoveryResult recovery, int storedCount)
        {
            Succeeded = succeeded;
            Reason = reason ?? string.Empty;
            HunterId = hunterId;
            ItemId = itemId ?? string.Empty;
            Recovery = recovery;
            StoredCount = storedCount;
        }

        public bool Succeeded { get; }
        public string Reason { get; }
        public int HunterId { get; }
        public string ItemId { get; }
        public HunterRecoveryResult Recovery { get; }
        public int StoredCount { get; }

        public static SettlementConsumableCommandResult Failed(string reason) => new(false, reason, 0, string.Empty, default, 0);
    }

    public struct HunterConsumableUsedEvent
    {
        public int HunterId;
        public string ItemId;
        public HunterBodyPart BodyPart;
        public int PreviousHealth;
        public int CurrentHealth;
        public int MaximumHealth;
        public int StoredCount;
    }

    public sealed class UseSettlementConsumableAction : CommandAction, ISourceAction, ITargetAction
    {
        private readonly SettlementInstance settlement;
        private readonly HunterInstance hunter;
        private readonly ItemData item;
        private readonly HunterBodyPart bodyPart;
        private readonly ISettlementConsumableContent content;
        private readonly ActionEventOutbox eventOutbox;

        public UseSettlementConsumableAction(SettlementInstance settlement, HunterInstance hunter, ItemData item, HunterBodyPart bodyPart, ISettlementConsumableContent content, ActionEventOutbox eventOutbox, IReactorEntity source, IReactorEntity target)
        {
            this.settlement = settlement ?? throw new ArgumentNullException(nameof(settlement));
            this.hunter = hunter ?? throw new ArgumentNullException(nameof(hunter));
            this.item = item;
            this.bodyPart = bodyPart;
            this.content = content ?? throw new ArgumentNullException(nameof(content));
            this.eventOutbox = eventOutbox ?? throw new ArgumentNullException(nameof(eventOutbox));
            Source = source ?? throw new ArgumentNullException(nameof(source));
            Target = target ?? throw new ArgumentNullException(nameof(target));
        }

        public SettlementConsumableCommandResult Result { get; private set; }
        public IReactorEntity Source { get; }
        public IReactorEntity Target { get; }

        protected override UniTask<ActionOutcome> ExecuteAsync(ActionExecutionContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!ReferenceEquals(settlement.GetHunter(hunter.InstanceId), hunter) || !hunter.IsAvailable) return Fail("猎人不属于当前营地或当前不可用。");
            if (!content.TryGet(item, out ConsumableUsePlan plan)) return Fail("消耗品内容尚未配置。");
            if (plan.Effect != ConsumableEffectKind.RecoverBodyPart) return Fail("消耗品效果尚未支持。");
            if (!HunterRecoveryRules.CanRecover(hunter, bodyPart, out string reason)) return Fail(reason);
            int oldAmount = settlement.GetStoredItem(item);
            if (oldAmount <= 0) return Fail("营地库存中没有该消耗品。");
            cancellationToken.ThrowIfCancellationRequested();
            if (!settlement.SpendStoredItem(item, 1)) return Fail("消耗品库存已经发生变化。");
            if (!HunterRecoveryRules.TryRecover(hunter, bodyPart, plan.Amount, out HunterRecoveryResult recovery, out reason))
            {
                settlement.AddStoredItem(item, 1);
                return Fail(reason);
            }

            int storedCount = settlement.GetStoredItem(item);
            Result = new SettlementConsumableCommandResult(true, string.Empty, hunter.InstanceId, item.ContentId, recovery, storedCount);
            eventOutbox.Stage(new HunterConsumableUsedEvent { HunterId = hunter.InstanceId, ItemId = item.ContentId, BodyPart = recovery.BodyPart, PreviousHealth = recovery.PreviousHealth, CurrentHealth = recovery.CurrentHealth, MaximumHealth = recovery.MaximumHealth, StoredCount = storedCount });
            eventOutbox.Stage(new SettlementTransactionCommittedEvent { TransactionId = $"consumable:{hunter.InstanceId}:{item.ContentId}:{bodyPart}", Kind = SettlementTransactionKind.Consumable });
            return UniTask.FromResult(ActionOutcome.Success());
        }

        private UniTask<ActionOutcome> Fail(string reason)
        {
            Result = SettlementConsumableCommandResult.Failed(reason);
            return UniTask.FromResult(ActionOutcome.Failure(reason));
        }
    }
}
