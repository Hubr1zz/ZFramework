using System.Collections.Generic;
using HuntingInDarkness.GameCore.Hunt;
using UnityEngine;

namespace HuntingInDarkness.Data
{
    // ═══════════════════════════════════════════════════════════════
    // Map 地块数据模型
    // ═══════════════════════════════════════════════════════════════

    /// <summary>地块三状态</summary>
    public enum TileState
    {
        Locked,       // 未解锁：背面朝上，不可交互
        Interactable, // 可交互：正面朝上但灰度/半透明
        Revealed      // 已翻开：完整显示
    }

    /// <summary>地块类型（影响生成规则和资源池）</summary>
    public enum TileType
    {
        Plains,   // 平原
        Forest,   // 森林
        Ruins,    // 废墟
        Cave,     // 洞穴
        Swamp,    // 沼泽
        Mountain, // 山地
        Starting  // 起始地块（特殊）
    }

    // ─── 资源点配置 ──────────────────────────────────────────────

    [System.Serializable]
    public sealed class ResourceMaterialConfig
    {
        public ItemData material;
        [Tooltip("可选稳定物品 ID；设置后由当前战役内容 Registry 解析，优先于直接资产引用。")]
        public string materialId;
        [Min(1)] public int copies = 1;
    }

    /// <summary>地块上的资源点配置（翻开时按概率生成）</summary>
    [System.Serializable]
    public class ResourcePointConfig
    {
        [Tooltip("稳定资源点 ID；为空时兼容使用旧单素材 ContentId。")]
        public string resourcePointId;
        public string displayName;
        [Tooltip("旧单素材配置；materialPool 为空时按 drawCount 生成同素材卡。")]
        public ItemData resource;
        public List<ResourceMaterialConfig> materialPool = new();
        public int spawnWeight = 1; // 生成权重
        public int drawCount = 2; // 允许翻开的素材卡数
        public int maxPerTile = 1; // 同一地块最多几个（0=无限）
    }

    // ─── 地块配置 SO ─────────────────────────────────────────────

    /// <summary>
    /// 六边形地块配置 ScriptableObject。
    /// 定义地块类型、翻开时的地块规则、资源点池。
    /// </summary>
    [CreateAssetMenu(fileName = "NewTile", menuName = "HuntingInDarkness/HexTile Config")]
    public class HexTileData : ScriptableObject
    {
        [Header("基础")]
        [SerializeField, Tooltip("稳定内容 ID。地图快照与读表引用使用；旧资产为空时暂以资产名兼容。")]
        private string contentId;
        public string   tileName = "新地块";
        public TileType tileType = TileType.Plains;
        [TextArea] public string description;

        [Header("地块规则（翻开时触发的事件/效果）")]
        public EventData tileRevealEvent;         // 可为 null
        [TextArea] public string tileRule = "";   // 规则文字描述

        [Header("生成规则")]
        public int spawnWeight       = 1;         // 在地图生成池中的权重
        public bool spawnInGroup     = false;      // 是否成组生成（如"雕像平原"一次4块）
        public int  groupSize        = 1;          // 成组时同时生成的数量
        public bool mustBeAdjacent   = true;       // 成组时是否要求相邻

        [Header("资源点池（翻开时按权重随机出现）")]
        public List<ResourcePointConfig> resourcePoints = new();
        public int maxResourcePoints = 2;          // 该地块最多几个资源点

        [Header("怪物遭遇（翻开时的Boss遭遇概率，0=无）")]
        public int bossEncounterWeight = 0;
        public string bossEncounterId = "";

        public string ContentId
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(contentId)) return contentId.Trim();
                if (!string.IsNullOrWhiteSpace(name)) return name.Trim();
                return tileName?.Trim() ?? string.Empty;
            }
        }

        public bool HasExplicitContentId => !string.IsNullOrWhiteSpace(contentId);
        public void ConfigureContentId(string value) => contentId = value?.Trim() ?? string.Empty;
    }

    // ─── 地块运行时实例 ──────────────────────────────────────────

    /// <summary>地块运行时状态（一次狩猎的地图数据）</summary>
    [System.Serializable]
    public class HexTileInstance
    {
        public Vector2Int AxialCoord;
        public string     ConfigName;   // 旧存档资产名兼容
        public string     ConfigId;     // 稳定 HexTileData.ContentId

        [System.NonSerialized]
        public HexTileData Config;

        public TileState State = TileState.Locked;
        public bool      HasBossEncounter = false;

        [System.NonSerialized]
        public HuntTileState DomainState;

        // 已生成的资源点（翻开时确定）
        public List<ResourcePointInstance> ResourcePoints = new();

        // ─── 便捷属性 ───

        public bool IsStartingTile => Config != null && Config.tileType == TileType.Starting;

        public void AttachDomainState(HuntTileState state)
        {
            DomainState = state;
            SyncFromDomain();
        }

        public void SyncFromDomain()
        {
            if (DomainState == null) return;
            State = (TileState)DomainState.Visibility;
            HasBossEncounter = DomainState.HasBossEncounter;
        }
    }

    /// <summary>地块上的单个资源点运行时状态</summary>
    [System.Serializable]
    public class ResourcePointInstance
    {
        public string ResourcePointId;
        public string ResourceName;  // 采集过程中的玩家可见名称；持久化时改用 Resource.ContentId

        [System.NonSerialized]
        public ItemData Resource;

        [System.NonSerialized]
        public List<ItemData> MaterialPool = new();

        public int  DrawCount   = 2;  // 采集时可抽取的卡数
        public bool IsExhausted = false; // 已被完全采集

        public bool HasMaterialPool => MaterialPool != null && MaterialPool.Exists(material => material != null);
        public string StableId
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(ResourcePointId)) return ResourcePointId.Trim();
                if (Resource != null) return Resource.ContentId;
                if (MaterialPool != null)
                    foreach (ItemData material in MaterialPool)
                        if (material != null)
                            return material.ContentId;
                return string.Empty;
            }
        }

        public ItemData ResolveMaterial(string contentId)
        {
            if (string.IsNullOrWhiteSpace(contentId)) return null;
            if (MaterialPool != null)
                foreach (ItemData material in MaterialPool)
                    if (material != null && string.Equals(material.ContentId, contentId, System.StringComparison.Ordinal))
                        return material;
            return Resource != null && string.Equals(Resource.ContentId, contentId, System.StringComparison.Ordinal) ? Resource : null;
        }
    }
}
