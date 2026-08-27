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
        public string EventId;         // 事件型条目使用 EventData.ContentId；动态条目保留自身稳定 ID
        public string EventName;       // 缓存名称
        public bool   IsCompleted;
        public bool   IsMilestone;     // 主线事件（金色标记）
        public TimelineEntryType EntryType = TimelineEntryType.Random;
        public string ResolutionMemoryId;
    }

    [System.Serializable]
    public sealed class SettlementEventMemoryEffect
    {
        public int EffectIndex;
        public string EffectType;
        public string TargetName;
        public string ResolvedTargetId;
        public bool Applied;
        public string Reason;
        public int TargetActorId;
        public bool StateChanged;
        public int PreviousValue;
        public int CurrentValue;
    }

    [System.Serializable]
    public sealed class SettlementEventMemory
    {
        public string MemoryId;
        public string EventId;
        public string EventName;
        public string ResolutionMode;
        public EventResolutionSelectionMode SelectionMode;
        public string OptionId;
        public string OptionText;
        public int Year;
        public int ActorId;
        public string CheckType;
        public bool HasCheck;
        public bool Success;
        public int RollValue;
        public int Bonus;
        public int Total;
        public int Target;
        public bool WasRerolled;
        public string ResultText;
        public List<SettlementEventMemoryEffect> Effects = new();
    }

    /// <summary>
    /// 已提交事件节点留下的最小持久化链检查点。
    /// 只保存稳定内容 ID 和顺序，不保存 ScriptableObject、效果快照或运行时引用。
    /// </summary>
    [System.Serializable]
    public sealed class SettlementEventChainCheckpoint
    {
        public const int CurrentSchemaVersion = 2;
        public int SchemaVersion = CurrentSchemaVersion;
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
        public List<string> AncestorEventIds = new();
    }

    [System.Serializable]
    public sealed class PendingHuntNoiseLease
    {
        public const int CurrentSchemaVersion = 1;
        public int SchemaVersion = CurrentSchemaVersion;
        public string LeaseId;
        public string SourceEventId;
        public int NoiseModifier;
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
        public const int CurrentReturnSchemaVersion = 1;
        [Tooltip("稳定的本次远征实例 ID；旧存档为空时保持兼容，不自动伪造身份。")]
        public string RecordId;
        [Tooltip("主动回营结果协议版本；0 表示旧流程已经转移资源/成长的兼容记录。")]
        public int ReturnSchemaVersion;
        public int  Year;
        public int  HuntersDeployed;
        public int  HuntersLost;
        public bool BossDefeated;
        public List<int> ParticipantHunterIds = new();
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
        public const int CurrentCampaignPacingSchemaVersion = 2;
        public const int CurrentMaterialDiscoverySchemaVersion = 1;
        public const int MaxLegacyHuntsPerYear = 8;
        public const int MaxPendingEventChainOccurrences = 64;
        [Header("内容存档版本")]
        public int ItemIdentitySchemaVersion;
        public int TraitIdentitySchemaVersion;
        public int InventionIdentitySchemaVersion;
        public int TimelineEventIdentitySchemaVersion;
        public string TimelineEventIdentityMigrationDiagnostic;
        public int SettlementModifierSchemaVersion;
        public int MaterialDiscoverySchemaVersion;
        public const int CurrentEventMemorySchemaVersion = 1;
        public int EventMemorySchemaVersion;
        public string EventMemoryMigrationDiagnostic;

        [Header("时间线")]
        public int CurrentYear = 1;
        public string CampaignCalendarId;
        public int CurrentSeasonIndex;
        public int HuntsCompletedThisYear;
        public int HuntsPerYear = 2;
        public int CampaignPacingSchemaVersion;
        public string CampaignPacingMigrationDiagnostic;
        public int LastRecruitmentYear;

        [Header("猎人名单（按 InstanceId 索引）")]
        public List<HunterInstance> Hunters = new();

        [Header("资源存储（稳定物品 ID → 数量）")]
        public List<ResourceEntry> Resources = new();

        [Header("已发现素材（稳定物品 ID）")]
        public List<string> DiscoveredMaterialIds = new();

        [Header("装备仓库（稳定物品 ID → 数量）")]
        public List<ResourceEntry> EquipmentStorage = new();

        [Header("发明解锁状态（稳定发明 ID → 是否解锁）")]
        public List<StringBoolEntry> UnlockedInventions = new();

        [Header("战役持续修正（稳定 ModifierId → 实际生效值）")]
        public List<SettlementModifierState> ActiveModifiers = new();

        [Header("下一次狩猎一次性风险租约")]
        public PendingHuntNoiseLease PendingHuntNoiseLease;

        [Header("发明主动效果年度使用状态")]
        public List<InventionActiveEffectUsage> InventionActiveEffectUses = new();

        [Header("已建工坊（稳定工坊 ID → 是否建成）")]
        public List<StringBoolEntry> BuiltWorkshops = new();

        [Header("Timeline")]
        public List<AnnalEntry> Timeline = new();
        public List<SettlementEventMemory> EventMemories = new();
        public List<HuntRecord>    HuntHistory = new();
        [Header("待完成的远征归来结算")]
        public HuntRecord PendingHuntReturn;

        [Header("事件链恢复检查点")]
        public List<SettlementEventChainCheckpoint> PendingEventChains = new();

        [Header("本年出发的猎人（狩猎阶段用）")]
        public List<int> DepartingHunterIds = new();
        public int DeparturePreparedYear;
        public string DeparturePreparationToken;
        [System.NonSerialized] public string RuntimeDeparturePreparationToken;

        /// <summary>
        /// 旧存档仍带有按年配额字段。它们只作为反序列化与迁移输入保留，生产规则以绑定日历的季节列表为权威。
        /// </summary>
        public void NormalizeLegacyHuntProgress()
        {
            HuntsCompletedThisYear = 0;
            HuntsPerYear = 1;
        }

        public bool HasHuntRecord(string recordId)
        {
            string normalizedId = recordId?.Trim() ?? string.Empty;
            return normalizedId.Length > 0 && HuntHistory != null && HuntHistory.Exists(record => record != null && string.Equals(record.RecordId?.Trim(), normalizedId, System.StringComparison.Ordinal));
        }

        public bool TryAppendHuntRecord(HuntRecord record)
        {
            if (record == null) return false;
            HuntHistory ??= new List<HuntRecord>();
            string recordId = record.RecordId?.Trim() ?? string.Empty;
            if (recordId.Length == 0 || HuntHistory.Exists(existing => existing != null && string.Equals(existing.RecordId?.Trim(), recordId, System.StringComparison.Ordinal))) return false;
            HuntHistory.Add(record);
            return true;
        }

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

        public bool HasDiscoveredMaterial(string materialId)
        {
            string normalizedId = materialId?.Trim() ?? string.Empty;
            return normalizedId.Length > 0 && DiscoveredMaterialIds != null && DiscoveredMaterialIds.Exists(id => string.Equals(id?.Trim(), normalizedId, System.StringComparison.Ordinal));
        }

        public bool DiscoverMaterial(string materialId)
        {
            string normalizedId = materialId?.Trim() ?? string.Empty;
            if (normalizedId.Length == 0 || HasDiscoveredMaterial(normalizedId)) return false;
            DiscoveredMaterialIds ??= new List<string>();
            DiscoveredMaterialIds.Add(normalizedId);
            return true;
        }

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

        public bool CanRecordEventMemory(SettlementEventMemory memory, out string reason)
        {
            reason = string.Empty;
            if (memory == null || string.IsNullOrWhiteSpace(memory.MemoryId) || string.IsNullOrWhiteSpace(memory.EventId))
            {
                reason = "事件记忆缺少稳定身份。";
                return false;
            }
            if (EventMemorySchemaVersion > CurrentEventMemorySchemaVersion)
            {
                reason = $"事件记忆 schema {EventMemorySchemaVersion} 高于当前版本 {CurrentEventMemorySchemaVersion}。";
                return false;
            }
            SettlementEventMemory existing = EventMemories?.Find(candidate => candidate != null && string.Equals(candidate.MemoryId, memory.MemoryId, System.StringComparison.Ordinal));
            if (existing == null) return true;
            if (AreEventMemoriesEquivalent(existing, memory)) return true;
            reason = $"事件记忆 {memory.MemoryId} 已存在但事实不一致。";
            return false;
        }

        public bool TryRecordEventMemory(SettlementEventMemory memory, out string reason)
        {
            if (!CanRecordEventMemory(memory, out reason)) return false;
            EventMemories ??= new List<SettlementEventMemory>();
            if (EventMemories.Exists(candidate => candidate != null && string.Equals(candidate.MemoryId, memory.MemoryId, System.StringComparison.Ordinal))) return true;
            EventMemories.Add(CloneEventMemory(memory));
            EventMemorySchemaVersion = CurrentEventMemorySchemaVersion;
            EventMemoryMigrationDiagnostic = string.Empty;
            return true;
        }

        private static SettlementEventMemory CloneEventMemory(SettlementEventMemory source)
        {
            var clone = new SettlementEventMemory
            {
                MemoryId = source.MemoryId,
                EventId = source.EventId,
                EventName = source.EventName,
                ResolutionMode = source.ResolutionMode,
                SelectionMode = source.SelectionMode,
                OptionId = source.OptionId,
                OptionText = source.OptionText,
                Year = source.Year,
                ActorId = source.ActorId,
                CheckType = source.CheckType,
                HasCheck = source.HasCheck,
                Success = source.Success,
                RollValue = source.RollValue,
                Bonus = source.Bonus,
                Total = source.Total,
                Target = source.Target,
                WasRerolled = source.WasRerolled,
                ResultText = source.ResultText
            };
            foreach (SettlementEventMemoryEffect effect in source.Effects ?? new List<SettlementEventMemoryEffect>())
                if (effect != null)
                    clone.Effects.Add(new SettlementEventMemoryEffect
                    {
                        EffectIndex = effect.EffectIndex,
                        EffectType = effect.EffectType,
                        TargetName = effect.TargetName,
                        ResolvedTargetId = effect.ResolvedTargetId,
                        Applied = effect.Applied,
                        Reason = effect.Reason,
                        TargetActorId = effect.TargetActorId,
                        StateChanged = effect.StateChanged,
                        PreviousValue = effect.PreviousValue,
                        CurrentValue = effect.CurrentValue
                    });
            return clone;
        }

        private static bool AreEventMemoriesEquivalent(SettlementEventMemory left, SettlementEventMemory right)
        {
            if (left == null || right == null || !string.Equals(left.EventId, right.EventId, System.StringComparison.Ordinal) || !string.Equals(left.EventName, right.EventName, System.StringComparison.Ordinal) || !string.Equals(left.ResolutionMode, right.ResolutionMode, System.StringComparison.Ordinal) || left.SelectionMode != right.SelectionMode || !string.Equals(left.OptionId, right.OptionId, System.StringComparison.Ordinal) || !string.Equals(left.OptionText, right.OptionText, System.StringComparison.Ordinal) || left.Year != right.Year || left.ActorId != right.ActorId || !string.Equals(left.CheckType, right.CheckType, System.StringComparison.Ordinal) || left.HasCheck != right.HasCheck || left.Success != right.Success || left.RollValue != right.RollValue || left.Bonus != right.Bonus || left.Total != right.Total || left.Target != right.Target || left.WasRerolled != right.WasRerolled || !string.Equals(left.ResultText, right.ResultText, System.StringComparison.Ordinal)) return false;
            if ((left.Effects?.Count ?? 0) != (right.Effects?.Count ?? 0)) return false;
            for (int index = 0; index < (left.Effects?.Count ?? 0); index++)
            {
                SettlementEventMemoryEffect a = left.Effects[index];
                SettlementEventMemoryEffect b = right.Effects[index];
                if (a == null || b == null || a.EffectIndex != b.EffectIndex || !string.Equals(a.EffectType, b.EffectType, System.StringComparison.Ordinal) || !string.Equals(a.TargetName, b.TargetName, System.StringComparison.Ordinal) || !string.Equals(a.ResolvedTargetId, b.ResolvedTargetId, System.StringComparison.Ordinal) || a.Applied != b.Applied || !string.Equals(a.Reason, b.Reason, System.StringComparison.Ordinal) || a.TargetActorId != b.TargetActorId || a.StateChanged != b.StateChanged || a.PreviousValue != b.PreviousValue || a.CurrentValue != b.CurrentValue) return false;
            }
            return true;
        }

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
        public IReadOnlyList<SettlementEventChainOccurrence> CommitEventChainOccurrence(string chainId, int completedSequence, IReadOnlyList<string> childEventIds, int year, int actorId, IReadOnlyCollection<string> ancestorEventIds = null)
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
            checkpoint.SchemaVersion = SettlementEventChainCheckpoint.CurrentSchemaVersion;
            var appendedOccurrences = new List<SettlementEventChainOccurrence>();
            if (!checkpoint.CommittedSequences.Contains(completedSequence))
            {
                checkpoint.CommittedSequences.Add(completedSequence);
                checkpoint.PendingOccurrences.RemoveAll(occurrence => occurrence != null && occurrence.Sequence == completedSequence);
                if (hasChildren)
                    foreach (string childEventId in childEventIds)
                    {
                        string normalizedEventId = childEventId?.Trim() ?? string.Empty;
                        if (normalizedEventId.Length == 0) continue;
                        if (checkpoint.PendingOccurrences.Count >= MaxPendingEventChainOccurrences)
                        {
                            checkpoint.Diagnostic = $"事件链检查点超过待恢复 occurrence 上限 {MaxPendingEventChainOccurrences}。";
                            break;
                        }
                        if (checkpoint.NextSequence <= 0 || checkpoint.NextSequence == int.MaxValue)
                        {
                            checkpoint.Diagnostic = "事件链检查点 occurrence 序号已耗尽。";
                            break;
                        }
                        var occurrence = new SettlementEventChainOccurrence
                        {
                            Sequence = checkpoint.NextSequence++,
                            EventId = normalizedEventId,
                            EventName = normalizedEventId,
                            Year = year,
                            ActorId = actorId,
                            AncestorEventIds = ancestorEventIds == null ? new List<string>() : new List<string>(ancestorEventIds)
                        };
                        checkpoint.PendingOccurrences.Add(occurrence);
                        appendedOccurrences.Add(occurrence);
                    }
            }

            if (checkpoint.PendingOccurrences.Count == 0 && string.IsNullOrWhiteSpace(checkpoint.Diagnostic))
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
