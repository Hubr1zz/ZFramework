using System.Collections.Generic;
using Core;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Settlement;
using UnityEngine;

namespace HuntingInDarkness.Settlement
{
    /// <summary>
    /// 发明解锁系统（纯 C#）。
    /// 职责：检查前置依赖/互斥/资源 → 解锁 → 应用效果。
    /// </summary>
    public class InventionSystem
    {
        private readonly SettlementInstance _settlement;

        public List<InventionData> AllInventions { get; set; } = new();

        public InventionSystem(SettlementInstance settlement)
        {
            _settlement = settlement;
        }

        // ─── 查询 ─────────────────────────────────────────────────

        public bool IsUnlocked(InventionData invention)
            => invention != null && _settlement.IsInventionUnlocked(invention.inventionName);

        /// <summary>是否可以解锁（前置已满足 + 未互斥 + 资源足够）</summary>
        public bool CanUnlock(InventionData invention, out string reason)
        {
            return InventionRules.CanUnlock(
                ToDefinition(invention),
                _settlement.IsInventionUnlocked,
                _settlement.GetResource,
                out reason);
        }

        // ─── 解锁 ─────────────────────────────────────────────────

        /// <summary>尝试解锁发明。返回是否成功。</summary>
        public bool TryUnlock(InventionData invention)
        {
            if (!CanUnlock(invention, out var reason))
            {
                Debug.LogWarning($"[InventionSystem] 无法解锁 {invention?.inventionName}: {reason}");
                return false;
            }

            // 消耗资源
            foreach (var cost in invention.costs)
            {
                if (cost.resource != null)
                    _settlement.SpendResource(cost.resource.itemName, cost.count);
            }

            // 解锁
            _settlement.UnlockInvention(invention.inventionName);
            Debug.Log($"[InventionSystem] 解锁发明：{invention.inventionName}");

            // 应用效果
            ApplyEffect(invention);

            return true;
        }

        // ─── 效果应用 ─────────────────────────────────────────────

        /// <summary>将发明效果应用到营地/猎人（根据 effectDescription 解析）</summary>
        private void ApplyEffect(InventionData invention)
        {
            // 根据发明名称/类别分发效果
            // 实际项目中可用 SO 上挂 EventEffect 列表；此处用关键词匹配简化
            var desc = invention.effectDescription ?? "";

            if (desc.Contains("+1 力量"))
            {
                foreach (var h in _settlement.GetAliveHunters())
                    h.Stats.strength++;
                Debug.Log("[InventionSystem] 全员力量+1");
            }

            if (desc.Contains("意志点上限"))
            {
                foreach (var h in _settlement.GetAliveHunters())
                    h.WillpowerMax++;
                Debug.Log("[InventionSystem] 全员意志点上限+1");
            }

            // 其他效果在此扩展...
            Debug.Log($"[InventionSystem] 发明效果已应用：{invention.inventionName} — {desc}");
        }

        // ─── 查询工具 ─────────────────────────────────────────────

        /// <summary>获取当前可解锁的发明列表</summary>
        public List<InventionData> GetUnlockable()
        {
            var result = new List<InventionData>();
            foreach (var inv in AllInventions)
                if (CanUnlock(inv, out _)) result.Add(inv);
            return result;
        }

        /// <summary>获取指定发明的所有后置发明（直接子节点）</summary>
        public List<InventionData> GetChildren(InventionData parent)
        {
            var result = new List<InventionData>();
            foreach (var inv in AllInventions)
                if (inv.prerequisites.Contains(parent)) result.Add(inv);
            return result;
        }

        private static InventionDefinition ToDefinition(InventionData invention)
        {
            if (invention == null) return null;
            var prerequisites = new List<string>();
            foreach (InventionData item in invention.prerequisites)
                if (item != null) prerequisites.Add(item.inventionName);
            var exclusive = new List<string>();
            foreach (InventionData item in invention.exclusiveWith)
                if (item != null) exclusive.Add(item.inventionName);
            var costs = new List<ResourceCost>();
            foreach (InventionCost cost in invention.costs)
                if (cost?.resource != null)
                    costs.Add(new ResourceCost(cost.resource.itemName, cost.count));
            return new InventionDefinition(
                invention.inventionName, prerequisites, exclusive, costs);
        }
    }
}
