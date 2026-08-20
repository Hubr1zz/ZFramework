using System.Collections.Generic;
using Core;
using HuntingInDarkness.Data;
using HuntingInDarkness.ContentTables;
using HuntingInDarkness.GameCore.Foundation;
using HuntingInDarkness.GameCore.Settlement;
using UnityEngine;

namespace HuntingInDarkness.Settlement
{
    /// <summary>
    /// 猎人管理系统（纯 C#）。
    /// 职责：招募猎人、装备管理、死亡判定、属性查询。
    /// </summary>
    public class HunterManagementSystem : IHunterDeathCommand
    {
        private readonly SettlementInstance _settlement;
        private readonly IRandomSource      _rng;
        private int deathInspirationGrowth = 1;
        private int deathInspirationMinimumAge = 2;

        public HunterManagementSystem(SettlementInstance settlement, IRandomSource rng)
        {
            _settlement = settlement;
            _rng        = rng;
        }

        public void ConfigureDeathInspiration(int growthPerHunter, int minimumDeceasedAge)
        {
            deathInspirationGrowth = Mathf.Max(0, growthPerHunter);
            deathInspirationMinimumAge = Mathf.Max(1, minimumDeceasedAge);
        }

        // ─── 招募 ─────────────────────────────────────────────────

        /// <summary>从模板招募新猎人（加入营地名单）</summary>
        public HunterInstance Recruit(HunterData template, string customName = null)
        {
            var hunter = new HunterInstance(template, HunterIdentityRules.NextAvailableId(_settlement.Hunters));
            if (!string.IsNullOrEmpty(customName)) hunter.Name = customName;
            if (!PlayableBloodlineRuntime.TryAssign(hunter, out string bloodlineReason))
            {
                Debug.LogError($"[HunterMgmt] 招募失败：{bloodlineReason}");
                return null;
            }
            if (!PlayableSettlementModifierRuntime.TryReconcileHunter(_settlement, hunter, out string reason))
            {
                Debug.LogError($"[HunterMgmt] 招募失败：{reason}");
                return null;
            }
            _settlement.Hunters.Add(hunter);
            Debug.Log($"[HunterMgmt] 招募猎人：{hunter.Name}（ID={hunter.InstanceId}）");
            EventBus.Publish(new HunterRosterChangedEvent());
            return hunter;
        }

        /// <summary>开局添加初始猎人（不触发招募事件）</summary>
        public void AddStartingHunter(string name, HunterData template = null)
        {
            var hunter = new HunterInstance(template, HunterIdentityRules.NextAvailableId(_settlement.Hunters));
            hunter.Name = name;
            if (!PlayableBloodlineRuntime.TryAssign(hunter, out string bloodlineReason))
            {
                Debug.LogError($"[HunterMgmt] 无法添加初始猎人：{bloodlineReason}");
                return;
            }
            if (!PlayableSettlementModifierRuntime.TryReconcileHunter(_settlement, hunter, out string reason))
            {
                Debug.LogError($"[HunterMgmt] 无法添加初始猎人：{reason}");
                return;
            }
            _settlement.Hunters.Add(hunter);
        }

        // ─── 装备管理 ─────────────────────────────────────────────

        /// <summary>给猎人装备物品（从装备仓库消耗）</summary>
        public bool EquipItem(HunterInstance hunter, ItemData item)
        {
            if (hunter == null || item == null) return false;
            if (item.itemType == ItemType.Resource) return false;
            hunter.Equipment ??= new List<ItemInstance>();
            hunter.EquippedItemIds ??= new List<string>();
            if (!PlayableEquipmentRules.CanEquip(hunter, item, out string reason))
            {
                Debug.Log($"[HunterMgmt] 无法装备：{reason}");
                return false;
            }

            // 从仓库消耗1件
            if (!_settlement.SpendStoredEquipment(item, 1)) return false;

            hunter.Equipment.Add(new ItemInstance(item));
            hunter.EquippedItemIds.Add(item.ContentId);
            Debug.Log($"[HunterMgmt] {hunter.Name} 装备：{item.itemName}");
            return true;
        }

        /// <summary>卸下装备（物品返回装备仓库）</summary>
        public bool UnequipItem(HunterInstance hunter, int slotIndex)
        {
            if (hunter?.Equipment == null || slotIndex < 0 || slotIndex >= hunter.Equipment.Count) return false;

            var item = hunter.Equipment[slotIndex];
            if (item?.Data == null) return false;
            hunter.EquippedItemIds ??= new List<string>();
            hunter.Equipment.RemoveAt(slotIndex);
            int savedIndex = hunter.EquippedItemIds.IndexOf(item.Data.ContentId);
            if (savedIndex >= 0)
                hunter.EquippedItemIds.RemoveAt(savedIndex);

            // 返还到仓库
            _settlement.AddStoredEquipment(item.Data, 1);
            Debug.Log($"[HunterMgmt] {hunter.Name} 卸下：{item.Data.itemName}");
            return true;
        }

        // ─── 死亡判定 ─────────────────────────────────────────────

        /// <summary>
        /// 狩猎结束后对参与的猎人进行存亡判定。
        /// 仅对指定 hunterIds 列表中的猎人进行判定。
        /// 返回死亡猎人列表（供UI展示）。
        /// </summary>
        public List<HunterInstance> RollSurvival(List<int> hunterIds)
        {
            var dead = new List<HunterInstance>();
            foreach (var id in hunterIds)
            {
                var h = _settlement.GetHunter(id);
                if (h == null || !h.IsAlive) continue;
                if (h.RollDeath(_rng))
                {
                    if (CommitDeath(h, "hunt_survival", "未能从狩猎中归来"))
                        dead.Add(h);
                }
            }
            return dead;
        }

        /// <summary>直接杀死猎人（来自事件/伤害）</summary>
        public void KillHunter(HunterInstance hunter)
        {
            TryKill(hunter, "combat", string.Empty, out _);
        }

        public bool TryKill(HunterInstance hunter, string causeId, string causeText, out string reason)
        {
            if (hunter == null)
            {
                reason = "死亡目标不存在";
                return false;
            }
            if (!ReferenceEquals(_settlement.GetHunter(hunter.InstanceId), hunter))
            {
                reason = "死亡目标不属于当前营地";
                return false;
            }

            hunter.IsAlive = false;
            CommitDeath(hunter, NormalizeCause(causeId, 64), NormalizeCause(causeText, 96));
            reason = string.Empty;
            return true;
        }

        /// <summary>提交规则层已经判定的退休，归还装备并保留猎人历史。</summary>
        public void CompleteRetirement(HunterInstance hunter)
        {
            if (hunter == null || !hunter.IsAlive || hunter.Availability != HunterAvailabilityState.Retired)
                return;

            _settlement.Timeline ??= new List<AnnalEntry>();
            string eventId = $"retirement:{hunter.InstanceId}:{_settlement.CurrentYear}";
            if (_settlement.Timeline.Exists(entry => entry.EventId == eventId))
                return;

            ReturnEquipmentToStorage(hunter);
            _settlement.Timeline.Add(new AnnalEntry
            {
                Year = _settlement.CurrentYear,
                EventId = eventId,
                EventName = $"{hunter.Name} 退休",
                IsCompleted = true,
                EntryType = TimelineEntryType.RosterChanged
            });
            EventBus.Publish(new HunterRosterChangedEvent());
        }

        // ─── 统计 ─────────────────────────────────────────────────

        public int AliveCount  => _settlement.GetAliveHunters().Count;
        public int AvailableCount => _settlement.GetAvailableHunters().Count;
        public bool AllDead    => AliveCount == 0;

        private void ReturnEquipmentToStorage(HunterInstance hunter)
        {
            if (hunter == null) return;
            hunter.EquippedItemIds ??= new List<string>();
            hunter.EquippedItemNames ??= new List<string>();
            IReadOnlyList<string> savedItems = hunter.EquippedItemIds.Count > 0 ? hunter.EquippedItemIds : hunter.EquippedItemNames;
            foreach (string itemId in savedItems)
                if (!string.IsNullOrEmpty(itemId))
                    _settlement.AddStoredEquipment(PlayableSettlementItemRegistry.ResolveContentId(itemId), 1);
            hunter.Equipment?.Clear();
            hunter.EquippedItemIds.Clear();
            hunter.EquippedItemNames.Clear();
        }

        private bool CommitDeath(HunterInstance hunter, string causeId, string causeText)
        {
            if (hunter == null || hunter.IsAlive)
                return false;

            _settlement.Timeline ??= new List<AnnalEntry>();
            string eventId = $"death:{hunter.InstanceId}";
            if (_settlement.Timeline.Exists(entry => entry.EventId == eventId))
                return false;

            ReturnEquipmentToStorage(hunter);
            _settlement.Timeline.Add(new AnnalEntry
            {
                Year = _settlement.CurrentYear,
                EventId = eventId,
                EventName = string.IsNullOrWhiteSpace(causeText) ? $"{hunter.Name} 死亡" : $"{hunter.Name} 死亡：{causeText}",
                IsCompleted = true,
                EntryType = TimelineEntryType.RosterChanged
            });

            var roster = new List<HunterState>(_settlement.Hunters.Count);
            foreach (HunterInstance candidate in _settlement.Hunters)
                roster.Add(candidate);
            HunterLossInspirationPlan inspiration = HunterLossInspirationRules.CreatePlan(hunter, roster, deathInspirationGrowth, deathInspirationMinimumAge);
            foreach (int hunterId in inspiration.HunterIds)
            {
                HunterInstance survivor = _settlement.GetHunter(hunterId);
                if (survivor != null)
                    survivor.UnspentGrowth += inspiration.GrowthPerHunter;
            }

            Debug.Log($"[HunterMgmt] {hunter.Name} 死亡；{inspiration.HunterIds.Count} 名猎人各获得 {inspiration.GrowthPerHunter} 点激励成长");
            EventBus.Publish(new HunterDiedEvent(hunter.InstanceId, hunter.Name, _settlement.CurrentYear, inspiration.GrowthPerHunter, inspiration.HunterIds.Count, causeId, causeText));
            EventBus.Publish(new HunterRosterChangedEvent());
            return true;
        }

        private static string NormalizeCause(string value, int maximumLength)
        {
            string normalized = value?.Trim() ?? string.Empty;
            foreach (char character in normalized)
                if (char.IsControl(character))
                    return string.Empty;
            return normalized.Length <= maximumLength ? normalized : normalized.Substring(0, maximumLength);
        }
    }
}
