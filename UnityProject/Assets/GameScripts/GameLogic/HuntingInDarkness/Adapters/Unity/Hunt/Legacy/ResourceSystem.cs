using System.Collections.Generic;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Foundation;
using HuntingInDarkness.GameCore.Hunt;
using UnityEngine;

namespace HuntingInDarkness.Hunt
{
    /// <summary>
    /// 资源采集系统（纯 C#）。
    /// 职责：
    ///   - 地块翻开时按概率/权重生成资源点
    ///   - 玩家点击资源点 → 展示素材池（抽卡）→ 采集
    ///   - 将采集物存入猎人的 Collectibles 列表
    /// </summary>
    public class ResourceSystem
    {
        private readonly IRandomSource _rng;
        private readonly HashSet<ResourcePointInstance> pendingHarvests = new();

        public ResourceSystem(IRandomSource rng)
        {
            _rng = rng;
        }

        // ─── 地块翻开时生成资源点 ─────────────────────────────────

        /// <summary>
        /// 当一个地块被翻开时调用，按配置生成资源点实例到 tile.ResourcePoints。
        /// </summary>
        public void SpawnResourcePoints(HexTileInstance tile)
        {
            tile.ResourcePoints.Clear();
            if (tile.Config == null || tile.Config.resourcePoints.Count == 0) return;

            var definitions = new List<ResourcePointDefinition>();
            var configs = new Dictionary<ResourcePointDefinition, ResourcePointConfig>();
            foreach (ResourcePointConfig config in tile.Config.resourcePoints)
            {
                if (config?.resource == null) continue;
                var definition = new ResourcePointDefinition(
                    config.resource.itemName,
                    config.spawnWeight,
                    config.drawCount,
                    config.maxPerTile);
                definitions.Add(definition);
                configs[definition] = config;
            }
            List<ResourcePointDefinition> spawned = HuntResourceRules.SpawnPoints(
                definitions, tile.Config.maxResourcePoints, _rng);
            foreach (ResourcePointDefinition definition in spawned)
            {
                ResourcePointConfig cfg = configs[definition];
                var point = new ResourcePointInstance
                {
                    ResourceName = definition.ResourceId,
                    Resource     = cfg.resource,
                    DrawCount    = definition.DrawCount,
                    IsExhausted  = false
                };
                tile.ResourcePoints.Add(point);
            }

            if (tile.ResourcePoints.Count > 0)
                Debug.Log($"[ResourceSystem] 地块 {tile.AxialCoord} 生成 {tile.ResourcePoints.Count} 个资源点");
        }

        // ─── 采集（抽卡） ─────────────────────────────────────────

        /// <summary>
        /// 猎人对资源点进行采集（翻牌）。
        /// drawCount 张牌中随机「翻开」，每张有 hitChance 概率获得资源。
        /// 采集物存入 hunter.Collectibles。
        /// 采集完后将资源点标为已耗尽。
        /// </summary>
        public List<ItemInstance> Harvest(
            ResourcePointInstance point,
            HunterInstance hunter,
            float hitChance = 0.6f)
        {
            PlayableHarvestTransaction transaction = PrepareHarvest(point, hunter, hitChance);
            if (transaction == null) return new List<ItemInstance>();
            while (transaction.CanReveal)
                transaction.RevealNext();
            IReadOnlyList<ItemInstance> result = transaction.Commit();
            var obtained = new List<ItemInstance>(result);

            LogHarvest(point.ResourceName, hunter?.Name, obtained.Count);
            return obtained;
        }

        public PlayableHarvestTransaction PrepareHarvest(ResourcePointInstance point, HunterInstance hunter, float hitChance = 0.6f, int? drawCount = null)
        {
            if (hunter == null || !hunter.IsAlive || point == null || point.IsExhausted || point.Resource == null) return null;
            if (!pendingHarvests.Add(point)) return null;

            try
            {
                HarvestDrawPlan plan = HuntResourceRules.CreateHarvestPlan(drawCount ?? point.DrawCount, hitChance, _rng);
                return new PlayableHarvestTransaction(this, point, hunter, plan, () => pendingHarvests.Remove(point));
            }
            catch
            {
                pendingHarvests.Remove(point);
                throw;
            }
        }

        public IReadOnlyList<ItemInstance> CommitHarvest(PlayableHarvestTransaction transaction)
        {
            if (transaction == null) return System.Array.Empty<ItemInstance>();
            if (!transaction.IsOwnedBy(this))
                throw new System.InvalidOperationException("The harvest transaction belongs to another resource system.");
            IReadOnlyList<ItemInstance> obtained = transaction.Commit();
            LogHarvest(transaction.ResourceName, transaction.HunterName, obtained.Count);
            return obtained;
        }

        private static void LogHarvest(string resourceName, string hunterName, int obtainedCount)
        {
            resourceName = string.IsNullOrWhiteSpace(resourceName) ? "资源" : resourceName;
            hunterName = string.IsNullOrWhiteSpace(hunterName) ? "?" : hunterName;

            if (obtainedCount > 0)
                Debug.Log($"[ResourceSystem] {hunterName} 采集 {resourceName} ×{obtainedCount}");
            else
                Debug.Log($"[ResourceSystem] {hunterName} 采集 {resourceName} — 空");
        }

        /// <summary>
        /// 将本次狩猎采集物（猎人 Collectibles）批量转入营地资源存储。
        /// 调用时机：Boss战结束或狩猎阶段主动撤退。
        /// </summary>
        public void TransferCollectibles(
            List<HunterInstance> hunters,
            HuntingInDarkness.Data.SettlementInstance settlement)
        {
            foreach (var h in hunters)
            {
                foreach (var item in h.Collectibles)
                {
                    settlement.AddResource(item.Data, item.Count);
                    Debug.Log($"[ResourceSystem] {h.Name} 收集物 {item.Data.itemName} 转入营地");
                }
                h.Collectibles.Clear();
            }
        }

    }
}
