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

        public int GetAvailableAmount(string itemId, HunterInstance actor)
        {
            if (!TryResolveItem(itemId, out ItemData item, out _) || !IsActiveActor(actor)) return 0;
            return TryCount(actor.Collectibles, item.ContentId, out int amount) ? amount : 0;
        }

        public bool CanRemove(string itemId, int amount, HunterInstance actor, out string reason)
        {
            if (amount <= 0)
                return Fail("事件消耗物品数量无效。", out reason);
            if (!TryResolveItem(itemId, out ItemData item, out reason)) return false;
            if (!IsActiveActor(actor))
                return Fail("事件消耗没有属于当前狩猎小队的存活猎人。", out reason);
            if (!TryCount(actor.Collectibles, item.ContentId, out int available))
                return Fail("事件携带物数量溢出。", out reason);
            if (available < amount)
                return Fail($"{actor.Name}携带的{item.itemName}不足。", out reason);
            reason = string.Empty;
            return true;
        }

        public bool TryAdd(string itemId, int amount, HunterInstance actor, out PlayableEventItemChange change, out string reason)
        {
            change = default;
            reason = string.Empty;
            if (amount <= 0)
                return Fail("事件奖励物品 ID 或数量无效。", out reason);
            if (!TryResolveItem(itemId, out ItemData item, out reason)) return false;
            if (!IsActiveActor(actor))
                return Fail("事件奖励没有属于当前狩猎小队的存活猎人。", out reason);
            if (!TryCount(actor.Collectibles, item.ContentId, out int oldAmount))
                return Fail("事件奖励物品数量溢出。", out reason);
            if (oldAmount > int.MaxValue - amount)
                return Fail("事件奖励物品数量溢出。", out reason);
            actor.Collectibles ??= new List<ItemInstance>();
            actor.Collectibles.Add(new ItemInstance(item, amount));
            change = new PlayableEventItemChange(item.ContentId, actor.InstanceId, oldAmount, oldAmount + amount);
            return true;
        }

        public bool TryRemove(string itemId, int amount, HunterInstance actor, out PlayableEventItemChange change, out string reason)
        {
            change = default;
            if (!CanRemove(itemId, amount, actor, out reason)) return false;
            TryResolveItem(itemId, out ItemData item, out _);
            TryCount(actor.Collectibles, item.ContentId, out int oldAmount);
            Remove(actor.Collectibles, item.ContentId, amount);
            change = new PlayableEventItemChange(item.ContentId, actor.InstanceId, oldAmount, oldAmount - amount);
            return true;
        }

        private bool IsActiveActor(HunterInstance actor) => actor != null && !actor.IsDead && ContainsReference(manager.ActiveHunters, actor);

        private static bool TryResolveItem(string itemId, out ItemData item, out string reason)
        {
            string resolvedId = itemId?.Trim() ?? string.Empty;
            if (resolvedId.Length == 0)
            {
                item = null;
                return Fail("事件物品 ID 无效。", out reason);
            }
            if (!PlayableSettlementItemRegistry.TryGet(resolvedId, out item) || item == null)
                return Fail($"事件引用了未知物品：{resolvedId}", out reason);
            if (item.itemType == ItemType.Resource)
            {
                item = null;
                return Fail("资源必须使用事件资源端口。", out reason);
            }
            reason = string.Empty;
            return true;
        }

        private static bool TryCount(IReadOnlyList<ItemInstance> items, string itemId, out int count)
        {
            long total = 0;
            if (items == null)
            {
                count = 0;
                return true;
            }
            foreach (ItemInstance item in items)
            {
                if (item?.Data == null || !string.Equals(item.Data.ContentId, itemId, StringComparison.Ordinal) || item.Count <= 0) continue;
                total += item.Count;
                if (total > int.MaxValue)
                {
                    count = 0;
                    return false;
                }
            }
            count = (int)total;
            return true;
        }

        private static void Remove(List<ItemInstance> items, string itemId, int amount)
        {
            for (int index = items.Count - 1; index >= 0 && amount > 0; index--)
            {
                ItemInstance item = items[index];
                if (item?.Data == null || !string.Equals(item.Data.ContentId, itemId, StringComparison.Ordinal)) continue;
                int removed = Math.Min(Math.Max(0, item.Count), amount);
                item.Count -= removed;
                amount -= removed;
                if (item.Count <= 0)
                    items.RemoveAt(index);
            }
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
