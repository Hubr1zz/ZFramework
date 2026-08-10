using System.Collections.Generic;
using HuntingInDarkness.GameCore.Settlement;
using UnityEngine;

namespace HuntingInDarkness.Data
{
    // ═══════════════════════════════════════════════════════════════
    // Settlement 数据模型
    // ═══════════════════════════════════════════════════════════════

    // ─── 发明树 ──────────────────────────────────────────────────

    /// <summary>
    /// 发明节点 ScriptableObject。含前置依赖引用（树形依赖图）。
    /// 经典模式下无法解锁全部发明（互斥选择由 exclusiveWith 控制）。
    /// </summary>
    [CreateAssetMenu(fileName = "NewInvention", menuName = "HuntingInDarkness/Invention")]
    public class InventionData : ScriptableObject
    {
        [Header("基础")]
        public string inventionName = "新发明";
        [TextArea] public string description;

        [Header("前置依赖（全部解锁后才可解锁此发明）")]
        public List<InventionData> prerequisites = new();

        [Header("解锁成本")]
        public List<InventionCost> costs = new();

        [Header("互斥（只能选其一）")]
        public List<InventionData> exclusiveWith = new();

        [Header("发明效果（文字描述，逻辑在 InventionSystem 中处理）")]
        [TextArea] public string effectDescription;

        [Header("发明类别")]
        public InventionCategory category = InventionCategory.Basic;

        [Header("卡牌图标")]
        public Sprite icon;

        [Header("主动效果选项（有多个时点击卡牌弹出选择）")]
        public List<InventionActiveEffect> activeEffects = new();
    }

    public enum InventionCategory
    {
        Basic,      // 基础（训练、工具等）
        Knowledge,  // 知识（故事、信仰、纸和笔等）
        Crafting,   // 制造（装备、工坊升级等）
        Combat,     // 战斗强化
        Special     // 特殊/传承
    }

    [System.Serializable]
    public class InventionCost
    {
        public ItemData resource;
        public int count = 1;
    }

    [System.Serializable]
    public class InventionActiveEffect
    {
        public string effectName;
        [TextArea] public string description;
    }

    // ─── Timeline 数据模型 ───────────────────────────────────────

    /// <summary>年鉴条目（营地 Timeline 中的一行记录）</summary>
    [System.Serializable]
    public class AnnalEntry
    {
        public int    Year;
        public string EventId;         // EventData SO 名称
        public string EventName;       // 缓存名称
        public bool   IsCompleted;
        public bool   IsMilestone;     // 主线事件（金色标记）
        public TimelineEntryType EntryType = TimelineEntryType.Random;
    }

    public enum TimelineEntryType
    {
        MainStory,  // 主线强制触发
        Random,     // 随机抽取
        PlayerAdded // 玩家行为/发明触发
    }

    /// <summary>
    /// 年度狩猎记录（每年出发记录）
    /// </summary>
    [System.Serializable]
    public class HuntRecord
    {
        public int  Year;
        public int  HuntersDeployed;
        public int  HuntersLost;
        public bool BossDefeated;
        public List<string> CollectedResources = new(); // 资源名列表
    }

    // ─── 营地运行时状态 ──────────────────────────────────────────

    /// <summary>
    /// 营地运行时状态（完整存档数据）。
    /// 用 JsonUtility 序列化到 Application.persistentDataPath。
    /// 注意：ItemData 引用用资源名称字符串存档，加载时按名重新查找 SO。
    /// </summary>
    [System.Serializable]
    public class SettlementInstance
    {
        [Header("时间线")]
        public int CurrentYear = 1;

        [Header("猎人名单（按 InstanceId 索引）")]
        public List<HunterInstance> Hunters = new();

        [Header("资源存储（资源名 → 数量）")]
        public List<ResourceEntry> Resources = new();

        [Header("发明解锁状态（发明名 → 是否解锁）")]
        public List<StringBoolEntry> UnlockedInventions = new();

        [Header("Timeline")]
        public List<AnnalEntry> Timeline = new();
        public List<HuntRecord>    HuntHistory = new();

        [Header("本年出发的猎人（狩猎阶段用）")]
        public List<int> DepartingHunterIds = new();

        // ─── 资源操作 ─────────────────────────────────────────────

        public int GetResource(string name)
        {
            return ResourceRules.Get(Resources, name);
        }

        public void AddResource(string name, int amount)
        {
            ResourceRules.Add(Resources, name, amount, () => new ResourceEntry());
        }

        public bool SpendResource(string name, int amount)
        {
            return ResourceRules.Spend(Resources, name, amount, () => new ResourceEntry());
        }

        // ─── 发明操作 ─────────────────────────────────────────────

        public bool IsInventionUnlocked(string inventionName)
        {
            return UnlockedInventions.Exists(e => e.Key == inventionName && e.Value);
        }

        public void UnlockInvention(string inventionName)
        {
            var e = UnlockedInventions.Find(e => e.Key == inventionName);
            if (e == null) { e = new StringBoolEntry { Key = inventionName }; UnlockedInventions.Add(e); }
            e.Value = true;
        }

        // ─── 猎人操作 ─────────────────────────────────────────────

        public HunterInstance GetHunter(int id) => Hunters.Find(h => h.InstanceId == id);
        public List<HunterInstance> GetAliveHunters() => Hunters.FindAll(h => h.IsAlive);
    }

    // ─── 序列化辅助（JsonUtility 不支持 Dictionary） ────────────

    [System.Serializable]
    public class ResourceEntry : ResourceAmount { }

    [System.Serializable]
    public class StringBoolEntry : NamedFlag { }
}
