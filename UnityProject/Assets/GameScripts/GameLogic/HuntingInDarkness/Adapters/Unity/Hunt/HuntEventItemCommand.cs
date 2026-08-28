using System;
using System.Collections.Generic;
using HuntingInDarkness.ActionFlow.Events;
using HuntingInDarkness.Data;
using HuntingInDarkness.Settlement;

namespace HuntingInDarkness.Hunt
{
    /// <summary>狩猎事件获得非资源物品的权威命令；奖励只进入当前存活执行者的携带物。</summary>
    public sealed class HuntEventItemCommand : IPlayableEventItemCommand
    {
        private readonly HuntManager manager;

        public HuntEventItemCommand(HuntManager manager)
        {
            this.manager = manager ?? throw new ArgumentNullException(nameof(manager));
        }

        public bool TryAdd(string itemId, int amount, HunterInstance actor, out PlayableEventItemChange change, out string reason)
        {
            change = default;
            reason = string.Empty;
            string resolvedId = itemId?.Trim() ?? string.Empty;
            if (resolvedId.Length == 0 || amount <= 0)
                return Fail("事件奖励物品 ID 或数量无效。", out reason);
            if (!PlayableSettlementItemRegistry.TryGet(resolvedId, out ItemData item) || item == null)
                return Fail($"事件奖励引用了未知物品：{resolvedId}", out reason);
            if (item.itemType == ItemType.Resource)
                return Fail("资源奖励必须使用 AddResource。", out reason);
            if (actor == null || actor.IsDead || !ContainsReference(manager.ActiveHunters, actor))
                return Fail("事件奖励没有属于当前狩猎小队的存活猎人。", out reason);
            int oldAmount = Count(actor.Collectibles, item.ContentId);
            if (oldAmount > int.MaxValue - amount)
                return Fail("事件奖励物品数量溢出。", out reason);
            actor.Collectibles ??= new List<ItemInstance>();
            actor.Collectibles.Add(new ItemInstance(item, amount));
            change = new PlayableEventItemChange(item.ContentId, actor.InstanceId, oldAmount, oldAmount + amount);
            return true;
        }

        private static int Count(IReadOnlyList<ItemInstance> items, string itemId)
        {
            int count = 0;
            if (items == null) return count;
            foreach (ItemInstance item in items)
            {
                if (item?.Data == null || !string.Equals(item.Data.ContentId, itemId, StringComparison.Ordinal) || item.Count <= 0) continue;
                if (count > int.MaxValue - item.Count) return int.MaxValue;
                count += item.Count;
            }
            return count;
        }

        private static bool ContainsReference(IReadOnlyList<HunterInstance> hunters, HunterInstance actor)
        {
            if (hunters == null) return false;
            foreach (HunterInstance hunter in hunters)
                if (ReferenceEquals(hunter, actor)) return true;
            return false;
        }

        private static bool Fail(string message, out string reason)
        {
            reason = message;
            return false;
        }
    }
}
