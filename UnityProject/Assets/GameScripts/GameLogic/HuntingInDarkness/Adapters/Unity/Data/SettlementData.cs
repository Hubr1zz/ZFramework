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
        [SerializeField, Tooltip("稳定内容 ID。写入存档及跨内容引用时使用；旧资产为空时暂以资产名兼容。")]
        private string contentId;
        public string inventionName = "新发明";
        [TextArea] public string description;

        [Header("前置依赖（全部解锁后才可解锁此发明）")]
        public List<InventionData> prerequisites = new();

        [Header("解锁成本")]
        public List<InventionCost> costs = new();

        [Header("互斥（只能选其一）")]
        public List<InventionData> exclusiveWith = new();

        [Header("发明效果说明（仅供玩家阅读）")]
        [TextArea] public string effectDescription;

        [Header("解锁时结构化效果")]
        public List<InventionPassiveEffect> unlockEffects = new();

        [Header("跨阶段 Action 效果")]
        public List<InventionActionEffect> actionEffects = new();

        [Header("发明类别")]
        public InventionCategory category = InventionCategory.Basic;

        [Header("卡牌图标")]
        public Sprite icon;

        [Header("主动效果选项（有多个时点击卡牌弹出选择）")]
        public List<InventionActiveEffect> activeEffects = new();

        public string ContentId
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(contentId)) return contentId.Trim();
                if (!string.IsNullOrWhiteSpace(name)) return name.Trim();
                return inventionName?.Trim() ?? string.Empty;
            }
        }

        public bool HasExplicitContentId => !string.IsNullOrWhiteSpace(contentId);
        public void ConfigureContentId(string value) => contentId = value?.Trim() ?? string.Empty;
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
    public sealed class InventionPassiveEffect
    {
        [Tooltip("Unlock 只在掌握瞬间结算；Campaign 作为持续来源投影到当前及未来猎人。")]
        public InventionEffectLifetime lifetime;
        [Tooltip("Campaign 效果必填的稳定修正 ID；内容重排或改名时保持不变。")]
        public string modifierId;
        public InventionEffectKind kind;
        public InventionEffectTarget target = InventionEffectTarget.AvailableHunters;
        public int value = 1;
    }

    [System.Serializable]
    public class InventionActiveEffect
    {
        [Tooltip("稳定效果 ID；用于年度次数与存档。")]
        public string effectId;
        public string effectName;
        [TextArea] public string description;
        [Tooltip("启动的稳定事件 ID。事件负责角色选择、判定、随机表现与结算。")]
        public string eventId;
        [Min(0), Tooltip("每年最多使用次数；0 表示不限次数。")]
        public int maxUsesPerYear = 1;
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

    /// <summary>
    /// 已提交事件节点留下的最小持久化链检查点。
    /// 只保存稳定内容 ID 和顺序，不保存 ScriptableObject、效果快照或运行时引用。
    /// </summary>
    [System.Serializable]
    public sealed class SettlementEventChainCheckpoint
    {
        public int SchemaVersion = 1;
        public string ChainId;
        public int NextSequence = 1;
        public string Diagnostic;
        public List<int> CommittedSequences = new();
        public List<SettlementEventChainOccurrence> PendingOccurrences = new();
    }

    [System.Serializable]
    public sealed class SettlementEventChainOccurrence
    {
        public int Sequence;
        public string EventId;
        public string EventName;
        public int Year;
        public int ActorId;
    }

    public enum TimelineEntryType
    {
        MainStory,  // 主线强制触发
        Random,     // 随机抽取
        PlayerAdded, // 玩家行为/发明触发
        RosterChanged, // 猎人退休等名册历史变化
        Scheduled, // 由事件结果动态加入的未来事件
        Invention // 已掌握的发明
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
        public List<string> CollectedResources = new(); // 稳定资源 ContentId 列表
    }

    // ─── 营地运行时状态 ──────────────────────────────────────────

    /// <summary>
    /// 营地运行时状态（完整存档数据）。
    /// 用 JsonUtility 序列化到 Application.persistentDataPath。
    /// 注意：ItemData 引用使用稳定 ContentId 存档；旧显示名由内容目录在加载后幂等迁移。
    /// </summary>
    [System.Serializable]
    public class SettlementInstance
    {
        public const int MaxPendingEventChainOccurrences = 64;
        [Header("内容存档版本")]
        public int ItemIdentitySchemaVersion;
        public int InventionIdentitySchemaVersion;
        public int SettlementModifierSchemaVersion;

        [Header("时间线")]
        public int CurrentYear = 1;
        public int HuntsCompletedThisYear;
        public int HuntsPerYear = 2;
        public int LastRecruitmentYear;

        [Header("猎人名单（按 InstanceId 索引）")]
        public List<HunterInstance> Hunters = new();

        [Header("资源存储（稳定物品 ID → 数量）")]
        public List<ResourceEntry> Resources = new();

        [Header("装备仓库（稳定物品 ID → 数量）")]
        public List<ResourceEntry> EquipmentStorage = new();

        [Header("发明解锁状态（稳定发明 ID → 是否解锁）")]
        public List<StringBoolEntry> UnlockedInventions = new();

        [Header("战役持续修正（稳定 ModifierId → 实际生效值）")]
        public List<SettlementModifierState> ActiveModifiers = new();

        [Header("发明主动效果年度使用状态")]
        public List<InventionActiveEffectUsage> InventionActiveEffectUses = new();

        [Header("已建工坊（稳定工坊 ID → 是否建成）")]
        public List<StringBoolEntry> BuiltWorkshops = new();

        [Header("Timeline")]
        public List<AnnalEntry> Timeline = new();
        public List<HuntRecord>    HuntHistory = new();

        [Header("事件链恢复检查点")]
        public List<SettlementEventChainCheckpoint> PendingEventChains = new();

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

        public int GetResource(ItemData item) => item == null ? 0 : GetResource(item.ContentId);
        public void AddResource(ItemData item, int amount)
        {
            if (item != null) AddResource(item.ContentId, amount);
        }
        public bool SpendResource(ItemData item, int amount) => item != null && SpendResource(item.ContentId, amount);

        // ─── 发明操作 ─────────────────────────────────────────────

        public bool IsInventionUnlocked(string inventionId)
        {
            string normalizedId = inventionId?.Trim() ?? string.Empty;
            return normalizedId.Length > 0 && UnlockedInventions != null && UnlockedInventions.Exists(entry => entry != null && entry.Key == normalizedId && entry.Value);
        }

        public void UnlockInvention(string inventionId)
        {
            string normalizedId = inventionId?.Trim() ?? string.Empty;
            if (normalizedId.Length == 0) return;
            UnlockedInventions ??= new List<StringBoolEntry>();
            StringBoolEntry entry = UnlockedInventions.Find(candidate => candidate != null && candidate.Key == normalizedId);
            if (entry == null)
            {
                entry = new StringBoolEntry { Key = normalizedId };
                UnlockedInventions.Add(entry);
            }
            entry.Value = true;
        }

        public bool IsWorkshopBuilt(string workshopId)
        {
            return BuiltWorkshops != null && BuiltWorkshops.Exists(entry => entry.Key == workshopId && entry.Value);
        }

        public void BuildWorkshop(string workshopId)
        {
            BuiltWorkshops ??= new List<StringBoolEntry>();
            StringBoolEntry entry = BuiltWorkshops.Find(candidate => candidate.Key == workshopId);
            if (entry == null)
            {
                entry = new StringBoolEntry { Key = workshopId };
                BuiltWorkshops.Add(entry);
            }
            entry.Value = true;
        }

        // ─── 猎人操作 ─────────────────────────────────────────────

        public HunterInstance GetHunter(int id) => Hunters.Find(h => h.InstanceId == id);
        public List<HunterInstance> GetAliveHunters() => Hunters.FindAll(h => h.IsAlive);
        public List<HunterInstance> GetAvailableHunters() => Hunters.FindAll(h => h.IsAvailable);

        public bool HasPendingEventChainOccurrences => PendingEventChains != null && PendingEventChains.Exists(chain => chain != null && chain.PendingOccurrences != null && chain.PendingOccurrences.Count > 0);

        public IReadOnlyList<SettlementEventChainOccurrence> GetPendingEventChainOccurrences(string chainId)
        {
            string normalizedChainId = chainId?.Trim() ?? string.Empty;
            SettlementEventChainCheckpoint checkpoint = PendingEventChains?.Find(candidate => candidate != null && candidate.ChainId == normalizedChainId);
            if (checkpoint?.PendingOccurrences == null) return System.Array.Empty<SettlementEventChainOccurrence>();
            return checkpoint.PendingOccurrences;
        }

        public string GetEventChainDiagnostic(string chainId)
        {
            string normalizedChainId = chainId?.Trim() ?? string.Empty;
            return PendingEventChains?.Find(candidate => candidate != null && candidate.ChainId == normalizedChainId)?.Diagnostic ?? string.Empty;
        }

        /// <summary>在同一同步提交边界中消费当前 occurrence，并追加直接子 occurrence。</summary>
        public IReadOnlyList<SettlementEventChainOccurrence> CommitEventChainOccurrence(string chainId, int completedSequence, IReadOnlyList<string> childEventIds, int year, int actorId)
        {
            string normalizedChainId = chainId?.Trim() ?? string.Empty;
            if (normalizedChainId.Length == 0) return System.Array.Empty<SettlementEventChainOccurrence>();
            PendingEventChains ??= new List<SettlementEventChainCheckpoint>();
            SettlementEventChainCheckpoint checkpoint = PendingEventChains.Find(candidate => candidate != null && candidate.ChainId == normalizedChainId);
            bool hasChildren = childEventIds != null && childEventIds.Count > 0;
            if (checkpoint == null)
            {
                if (!hasChildren) return System.Array.Empty<SettlementEventChainOccurrence>();
                checkpoint = new SettlementEventChainCheckpoint { ChainId = normalizedChainId };
                PendingEventChains.Add(checkpoint);
            }

            checkpoint.CommittedSequences ??= new List<int>();
            checkpoint.PendingOccurrences ??= new List<SettlementEventChainOccurrence>();
            var appendedOccurrences = new List<SettlementEventChainOccurrence>();
            if (!checkpoint.CommittedSequences.Contains(completedSequence))
            {
                checkpoint.CommittedSequences.Add(completedSequence);
                checkpoint.PendingOccurrences.RemoveAll(occurrence => occurrence != null && occurrence.Sequence == completedSequence);
                if (hasChildren)
                    foreach (string childEventId in childEventIds)
                    {
                        if (checkpoint.PendingOccurrences.Count >= MaxPendingEventChainOccurrences)
                        {
                            checkpoint.Diagnostic = $"事件链检查点超过待恢复 occurrence 上限 {MaxPendingEventChainOccurrences}。";
                            break;
                        }
                        string normalizedEventId = childEventId?.Trim() ?? string.Empty;
                        if (normalizedEventId.Length == 0) continue;
                        var occurrence = new SettlementEventChainOccurrence
                        {
                            Sequence = checkpoint.NextSequence++,
                            EventId = normalizedEventId,
                            EventName = normalizedEventId,
                            Year = year,
                            ActorId = actorId
                        };
                        checkpoint.PendingOccurrences.Add(occurrence);
                        appendedOccurrences.Add(occurrence);
                    }
            }

            if (checkpoint.PendingOccurrences.Count == 0)
            {
                PendingEventChains.Remove(checkpoint);
                return System.Array.Empty<SettlementEventChainOccurrence>();
            }
            return appendedOccurrences;
        }
    }

    // ─── 序列化辅助（JsonUtility 不支持 Dictionary） ────────────

    [System.Serializable]
    public class ResourceEntry : ResourceAmount { }

    [System.Serializable]
    public class StringBoolEntry : NamedFlag { }
}
