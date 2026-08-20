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
            => invention != null && _settlement.IsInventionUnlocked(invention.ContentId);

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
            var modifiers = new List<SettlementModifierState>();
            if (!PlayableSettlementModifierRuntime.TryCreateInventionModifiers(invention, modifiers, out string reason) || !PlayableSettlementModifierRuntime.TryCreateRegistrationPlan(_settlement, modifiers, out SettlementModifierRegistrationPlan plan, out reason))
            {
                Debug.LogWarning($"[InventionSystem] 无法准备持续效果：{reason}");
                return false;
            }
            if (!TryCommitUnlock(invention)) return false;
            PlayableSettlementModifierRuntime.ApplyRegistrationPlan(_settlement, plan);
            ApplyLegacyEffect(invention);
            return true;
        }

        /// <summary>只提交成本、解锁状态与年鉴；正式流程由 Settlement ActionQueue 继续展开结构化效果。</summary>
        public bool TryCommitUnlock(InventionData invention)
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
                    _settlement.SpendResource(cost.resource, cost.count);
            }

            // 解锁
            _settlement.UnlockInvention(invention.ContentId);
            Debug.Log($"[InventionSystem] 解锁发明：{invention.inventionName}");

            SettlementTimelineJournal.RecordInvention(_settlement, invention.ContentId, invention.inventionName);

            return true;
        }

        // ─── 效果应用 ─────────────────────────────────────────────

        /// <summary>旧直调入口的兼容效果；正式 3D 流程不经过此处。</summary>
        private void ApplyLegacyEffect(InventionData invention)
        {
            if (invention.unlockEffects != null && invention.unlockEffects.Count > 0)
            {
                foreach (InventionPassiveEffect effect in invention.unlockEffects)
                {
                    if (effect == null || effect.lifetime != InventionEffectLifetime.Unlock) continue;
                    foreach (HunterInstance hunter in _settlement.Hunters)
                        if (InventionEffectRules.IsEligible(hunter, effect.target))
                            InventionEffectRules.TryApply(hunter, effect.kind, effect.value, out _, out _);
                }
                return;
            }

            // 仅保留旧资产兼容；新内容不得依赖本地化文案解析。
            var desc = invention.effectDescription ?? "";

            if (desc.Contains("+1 力量"))
            {
                foreach (var h in _settlement.GetAvailableHunters())
                    h.Stats.strength++;
                Debug.Log("[InventionSystem] 全员力量+1");
            }

            if (desc.Contains("意志点上限"))
            {
                foreach (var h in _settlement.GetAvailableHunters())
                    h.WillpowerMax++;
                Debug.Log("[InventionSystem] 全员意志点上限+1");
            }

            Debug.LogWarning($"[InventionSystem] 发明 {invention.inventionName} 仍在使用旧文本效果兼容路径。请迁移为 unlockEffects。");
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
                if (item != null) prerequisites.Add(item.ContentId);
            var exclusive = new List<string>();
            foreach (InventionData item in invention.exclusiveWith)
                if (item != null) exclusive.Add(item.ContentId);
            var costs = new List<ResourceCost>();
            foreach (InventionCost cost in invention.costs)
                if (cost?.resource != null)
                    costs.Add(new ResourceCost(cost.resource.ContentId, cost.count));
            return new InventionDefinition(
                invention.ContentId, prerequisites, exclusive, costs);
        }
    }
}
