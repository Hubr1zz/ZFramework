using System.Collections.Generic;
using Core;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Foundation;
using UnityEngine;

namespace HuntingInDarkness.Settlement
{
    /// <summary>
    /// 猎人管理系统（纯 C#）。
    /// 职责：招募猎人、装备管理、死亡判定、属性查询。
    /// </summary>
    public class HunterManagementSystem
    {
        private readonly SettlementInstance _settlement;
        private readonly IRandomSource      _rng;

        private static int _nextHunterId = 100;

        public HunterManagementSystem(SettlementInstance settlement, IRandomSource rng)
        {
            _settlement = settlement;
            _rng        = rng;
        }

        // ─── 招募 ─────────────────────────────────────────────────

        /// <summary>从模板招募新猎人（加入营地名单）</summary>
        public HunterInstance Recruit(HunterData template, string customName = null)
        {
            var hunter = new HunterInstance(template, _nextHunterId++);
            if (!string.IsNullOrEmpty(customName)) hunter.Name = customName;
            _settlement.Hunters.Add(hunter);
            Debug.Log($"[HunterMgmt] 招募猎人：{hunter.Name}（ID={hunter.InstanceId}）");
            EventBus.Publish(new HunterRosterChangedEvent());
            return hunter;
        }

        /// <summary>开局添加初始猎人（不触发招募事件）</summary>
        public void AddStartingHunter(string name, HunterData template = null)
        {
            var hunter = new HunterInstance(template, _nextHunterId++);
            hunter.Name = name;
            _settlement.Hunters.Add(hunter);
        }

        // ─── 装备管理 ─────────────────────────────────────────────

        /// <summary>给猎人装备物品（从资源存储消耗）</summary>
        public bool EquipItem(HunterInstance hunter, ItemData item)
        {
            if (hunter == null || item == null) return false;
            if (hunter.Equipment.Count >= 9) { Debug.Log("[HunterMgmt] 装备栏已满"); return false; }
            if (item.itemType == ItemType.Resource) return false;

            // 从仓库消耗1件
            if (_settlement.GetResource(item.itemName) <= 0) return false;
            _settlement.SpendResource(item.itemName, 1);

            hunter.Equipment.Add(new ItemInstance(item));
            Debug.Log($"[HunterMgmt] {hunter.Name} 装备：{item.itemName}");
            return true;
        }

        /// <summary>卸下装备（物品返回资源存储）</summary>
        public bool UnequipItem(HunterInstance hunter, int slotIndex)
        {
            if (hunter == null || slotIndex < 0 || slotIndex >= hunter.Equipment.Count)
                return false;

            var item = hunter.Equipment[slotIndex];
            hunter.Equipment.RemoveAt(slotIndex);

            // 返还到仓库
            _settlement.AddResource(item.Data.itemName, 1);
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
                    dead.Add(h);
                    Debug.Log($"[HunterMgmt] {h.Name} 在死亡判定中阵亡");
                    EventBus.Publish(new HunterRosterChangedEvent());
                }
            }
            return dead;
        }

        /// <summary>直接杀死猎人（来自事件/伤害）</summary>
        public void KillHunter(HunterInstance hunter)
        {
            if (hunter == null) return;
            hunter.IsAlive = false;
            Debug.Log($"[HunterMgmt] {hunter.Name} 死亡");
            EventBus.Publish(new HunterRosterChangedEvent());
        }

        // ─── 统计 ─────────────────────────────────────────────────

        public int AliveCount  => _settlement.GetAliveHunters().Count;
        public bool AllDead    => AliveCount == 0;
    }
}
