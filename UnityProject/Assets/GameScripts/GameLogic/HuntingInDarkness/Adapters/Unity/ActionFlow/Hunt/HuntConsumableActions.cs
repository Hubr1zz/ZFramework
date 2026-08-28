using System;
using System.Collections.Generic;
using System.Threading;
using CardGame.ActionQueue;
using Core;
using Cysharp.Threading.Tasks;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Hunters;
using HuntingInDarkness.GameCore.Settlement;
using HuntingInDarkness.Hunt;
using HuntingInDarkness.Settlement;

namespace HuntingInDarkness.ActionFlow.Hunt
{
    public readonly struct HuntConsumableUsePlan
    {
        public HuntConsumableUsePlan(ItemData item, ConsumableEffectKind effect, int amount)
        {
            Item = item;
            Effect = effect;
            Amount = amount;
        }

        public ItemData Item { get; }
        public ConsumableEffectKind Effect { get; }
        public int Amount { get; }
    }

    public interface IHuntConsumableContent
    {
        bool TryGet(string itemId, out HuntConsumableUsePlan plan);
    }

    public sealed class PlayableHuntConsumableContentAdapter : IHuntConsumableContent
    {
        private readonly HuntManager manager;

        public PlayableHuntConsumableContentAdapter(HuntManager manager) => this.manager = manager ?? throw new ArgumentNullException(nameof(manager));

        public bool TryGet(string itemId, out HuntConsumableUsePlan plan)
        {
            string resolvedId = itemId?.Trim() ?? string.Empty;
            ItemData item = null;
            bool resolved = manager.BoundRoute != null
                ? manager.BoundRoute.TryResolveItem(resolvedId, out item)
                : PlayableSettlementItemRegistry.TryGet(resolvedId, out item);
            if (resolved && item != null && item.itemType == ItemType.Consumable && item.ConsumableEffect != ConsumableEffectKind.None && item.ConsumableEffectAmount > 0 && item.ConsumableEffectAmount <= 99 && item.HuntNoise == 0)
            {
                plan = new HuntConsumableUsePlan(item, item.ConsumableEffect, item.ConsumableEffectAmount);
                return true;
            }
            plan = default;
            return false;
        }
    }

    public readonly struct HuntConsumableCommandResult
    {
        public HuntConsumableCommandResult(bool succeeded, string reason, int hunterId, string itemId, HunterRecoveryResult recovery, int remainingCount)
        {
            Succeeded = succeeded;
            Reason = reason ?? string.Empty;
            HunterId = hunterId;
            ItemId = itemId ?? string.Empty;
            Recovery = recovery;
            RemainingCount = remainingCount;
        }

        public bool Succeeded { get; }
        public string Reason { get; }
        public int HunterId { get; }
        public string ItemId { get; }
        public HunterRecoveryResult Recovery { get; }
        public int RemainingCount { get; }

        public static HuntConsumableCommandResult Failed(string reason) => new(false, reason, 0, string.Empty, default, 0);
    }

    public struct HuntConsumableUsedEvent
    {
        public Guid SessionId;
        public int HunterId;
        public string ItemId;
        public HunterBodyPart BodyPart;
        public int PreviousHealth;
        public int CurrentHealth;
        public int MaximumHealth;
        public int RemainingCount;
    }

    public sealed class UseHuntConsumableAction : CommandAction, ISourceAction, ITargetAction
    {
        private readonly HuntManager manager;
        private readonly Guid sessionId;
        private readonly int ownerHunterId;
        private readonly string itemId;
        private readonly IHuntConsumableContent content;
        private readonly ActionEventOutbox eventOutbox;

        public UseHuntConsumableAction(HuntManager manager, Guid sessionId, int ownerHunterId, string itemId, HunterBodyPart bodyPart, IHuntConsumableContent content, ActionEventOutbox eventOutbox, IReactorEntity source, IReactorEntity target)
        {
            this.manager = manager ?? throw new ArgumentNullException(nameof(manager));
            this.sessionId = sessionId;
            this.ownerHunterId = ownerHunterId;
            this.itemId = itemId?.Trim() ?? string.Empty;
            this.content = content ?? throw new ArgumentNullException(nameof(content));
            this.eventOutbox = eventOutbox ?? throw new ArgumentNullException(nameof(eventOutbox));
            BodyPart = bodyPart;
            Source = source ?? throw new ArgumentNullException(nameof(source));
            Target = target ?? throw new ArgumentNullException(nameof(target));
        }

        public HunterBodyPart BodyPart { get; set; }
        public HuntConsumableCommandResult Result { get; private set; }
        public IReactorEntity Source { get; }
        public IReactorEntity Target { get; }

        protected override UniTask<ActionOutcome> ExecuteAsync(ActionExecutionContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            HunterInstance hunter = manager.ActiveHunters?.Find(candidate => candidate != null && candidate.InstanceId == ownerHunterId);
            if (hunter == null || !ReferenceEquals(manager.SelectedHunter, hunter) || !hunter.IsAlive || !hunter.IsAvailable) return Fail("只能由当前行动猎人使用自己的携带物。");
            if (!content.TryGet(itemId, out HuntConsumableUsePlan plan)) return Fail("狩猎消耗品内容不存在、已经换代或尚未配置。");
            if (plan.Effect != ConsumableEffectKind.RecoverBodyPart) return Fail("狩猎中尚不支持该消耗品效果。");
            if (!TryFindStack(hunter.Collectibles, plan.Item, out ItemInstance stack, out int remainingCount, out string reason)) return Fail(reason);
            if (!HunterRecoveryRules.CanRecover(hunter, BodyPart, out reason)) return Fail(reason);

            cancellationToken.ThrowIfCancellationRequested();
            int stackIndex = hunter.Collectibles.IndexOf(stack);
            stack.Count--;
            if (stack.Count == 0) hunter.Collectibles.RemoveAt(stackIndex);
            if (!HunterRecoveryRules.TryRecover(hunter, BodyPart, plan.Amount, out HunterRecoveryResult recovery, out reason))
            {
                if (stack.Count == 0) hunter.Collectibles.Insert(stackIndex, stack);
                stack.Count++;
                return Fail(reason);
            }

            remainingCount--;
            Result = new HuntConsumableCommandResult(true, string.Empty, hunter.InstanceId, plan.Item.ContentId, recovery, remainingCount);
            eventOutbox.Stage(new HuntConsumableUsedEvent { SessionId = sessionId, HunterId = hunter.InstanceId, ItemId = plan.Item.ContentId, BodyPart = recovery.BodyPart, PreviousHealth = recovery.PreviousHealth, CurrentHealth = recovery.CurrentHealth, MaximumHealth = recovery.MaximumHealth, RemainingCount = remainingCount });
            return UniTask.FromResult(ActionOutcome.Success());
        }

        private static bool TryFindStack(IReadOnlyList<ItemInstance> items, ItemData canonicalItem, out ItemInstance stack, out int count, out string reason)
        {
            stack = null;
            count = 0;
            if (items != null)
                foreach (ItemInstance candidate in items)
                {
                    if (candidate?.Data == null || !string.Equals(candidate.Data.ContentId, canonicalItem.ContentId, StringComparison.Ordinal)) continue;
                    if (!ReferenceEquals(candidate.Data, canonicalItem))
                    {
                        reason = "携带物属于旧内容世代，不能在当前狩猎中使用。";
                        return false;
                    }
                    if (candidate.Count <= 0 || count > int.MaxValue - candidate.Count)
                    {
                        reason = "携带物数量无效。";
                        return false;
                    }
                    stack ??= candidate;
                    count += candidate.Count;
                }
            if (stack != null)
            {
                reason = string.Empty;
                return true;
            }
            reason = "当前猎人没有携带该消耗品。";
            return false;
        }

        private UniTask<ActionOutcome> Fail(string reason)
        {
            Result = HuntConsumableCommandResult.Failed(reason);
            return UniTask.FromResult(ActionOutcome.Failure(reason));
        }
    }
}
