using System;
using System.Collections.Generic;
using HuntingInDarkness.ActionFlow.Events;
using HuntingInDarkness.Data;
using HuntingInDarkness.Settlement;

namespace HuntingInDarkness.Hunt
{
    /// <summary>将狩猎事件资源变化写入小队携带物，回营前不触碰营地库存。</summary>
    public sealed class HuntEventResourceCommand : IPlayableEventResourceCommand
    {
        private readonly HuntManager manager;

        public HuntEventResourceCommand(HuntManager manager)
        {
            this.manager = manager ?? throw new ArgumentNullException(nameof(manager));
        }

        public bool TryApply(EventEffectType effectType, string resourceId, int amount, HunterInstance actor, out PlayableEventResourceChange change, out string reason)
        {
            change = default;
            string resolvedId = PlayableSettlementItemRegistry.ResolveContentId(resourceId);
            if ((effectType != EventEffectType.AddResource && effectType != EventEffectType.RemoveResource) || amount <= 0)
            {
                reason = "狩猎事件资源变化无效。";
                return false;
            }
            if (!PlayableSettlementItemRegistry.TryGet(resolvedId, out ItemData item) || item == null || item.itemType != ItemType.Resource)
            {
                reason = $"找不到狩猎资源内容：{resourceId}";
                return false;
            }

            List<HunterInstance> hunters = manager.ActiveHunters;
            HunterInstance receiver = ResolveReceiver(hunters, actor);
            if (receiver == null)
            {
                reason = "狩猎事件没有可携带资源的猎人。";
                return false;
            }

            if (!TryCountCollectibles(hunters, resolvedId, out int oldAmount))
            {
                reason = "狩猎小队携带物数量超过可结算范围。";
                return false;
            }
            if (effectType == EventEffectType.AddResource && amount > int.MaxValue - oldAmount)
            {
                reason = "狩猎事件资源奖励超过可携带数量范围。";
                return false;
            }
            if (effectType == EventEffectType.RemoveResource && oldAmount < amount)
            {
                reason = $"狩猎小队携带的 {item.itemName} 不足。";
                return false;
            }

            if (effectType == EventEffectType.AddResource)
            {
                receiver.Collectibles ??= new List<ItemInstance>();
                receiver.Collectibles.Add(new ItemInstance(item, amount));
            }
            else
                RemoveCollectibles(hunters, receiver, resolvedId, amount);

            int newAmount = effectType == EventEffectType.AddResource ? oldAmount + amount : oldAmount - amount;
            change = new PlayableEventResourceChange(PlayableEventResourceScope.HuntCollectibles, resolvedId, oldAmount, newAmount);
            reason = string.Empty;
            return true;
        }

        private static HunterInstance ResolveReceiver(IReadOnlyList<HunterInstance> hunters, HunterInstance actor)
        {
            if (hunters == null) return null;
            foreach (HunterInstance hunter in hunters)
                if (ReferenceEquals(hunter, actor) && hunter.IsAlive)
                    return hunter;
            foreach (HunterInstance hunter in hunters)
                if (hunter != null && hunter.IsAlive)
                    return hunter;
            return null;
        }

        private static bool TryCountCollectibles(IReadOnlyList<HunterInstance> hunters, string resourceId, out int count)
        {
            long total = 0;
            if (hunters == null)
            {
                count = 0;
                return true;
            }
            foreach (HunterInstance hunter in hunters)
            {
                if (hunter?.Collectibles == null) continue;
                foreach (ItemInstance item in hunter.Collectibles)
                {
                    if (item?.Data != null && string.Equals(item.Data.ContentId, resourceId, StringComparison.Ordinal))
                        total += Math.Max(0, item.Count);
                    if (total > int.MaxValue)
                    {
                        count = 0;
                        return false;
                    }
                }
            }
            count = (int)total;
            return true;
        }

        private static void RemoveCollectibles(IReadOnlyList<HunterInstance> hunters, HunterInstance receiver, string resourceId, int amount)
        {
            int remaining = RemoveFromHunter(receiver, resourceId, amount);
            if (remaining == 0) return;
            foreach (HunterInstance hunter in hunters)
            {
                if (hunter == null || ReferenceEquals(hunter, receiver)) continue;
                remaining = RemoveFromHunter(hunter, resourceId, remaining);
                if (remaining == 0) return;
            }
        }

        private static int RemoveFromHunter(HunterInstance hunter, string resourceId, int amount)
        {
            if (hunter?.Collectibles == null) return amount;
            for (int index = hunter.Collectibles.Count - 1; index >= 0 && amount > 0; index--)
            {
                ItemInstance item = hunter.Collectibles[index];
                if (item?.Data == null || !string.Equals(item.Data.ContentId, resourceId, StringComparison.Ordinal)) continue;
                int removed = Math.Min(Math.Max(0, item.Count), amount);
                item.Count -= removed;
                amount -= removed;
                if (item.Count <= 0)
                    hunter.Collectibles.RemoveAt(index);
            }
            return amount;
        }
    }
}
