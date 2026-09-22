using System;
using System.Collections.Generic;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Foundation;
using HuntingInDarkness.GameCore.Settlement;
using UnityEngine;

namespace HuntingInDarkness.ContentTables
{
    [Serializable]
    public sealed class BloodlineTableRecord
    {
        public string id;
        public string displayName;
        public string description;
        public string activationHint;
        public int drawWeight = 1;
    }

    [Serializable]
    public sealed class BloodlineTableDocument
    {
        public int version = 1;
        public List<BloodlineTableRecord> bloodlines = new();
    }

    public interface IHunterBloodlineContent
    {
        IReadOnlyList<HunterBloodlineDefinition> Definitions { get; }
        bool TryGet(string bloodlineId, out HunterBloodlineDefinition definition);
    }

    /// <summary>从显式 JSON 内容源读取血脉，向规则层暴露稳定、只读定义。</summary>
    public sealed class PlayableBloodlineTable : IHunterBloodlineContent
    {
        private readonly List<HunterBloodlineDefinition> definitions = new();
        private bool hasErrors;

        public PlayableBloodlineTable(TextAsset source)
        {
            if (source == null)
            {
                hasErrors = true;
                Debug.LogError("[ContentTable] 缺少血脉表内容源。");
                return;
            }

            BloodlineTableDocument document;
            try
            {
                document = JsonUtility.FromJson<BloodlineTableDocument>(source.text);
            }
            catch (Exception exception)
            {
                hasErrors = true;
                Debug.LogError($"[ContentTable] 血脉表无法解析：{exception.Message}");
                return;
            }
            if (document?.bloodlines == null)
            {
                hasErrors = true;
                Debug.LogError("[ContentTable] 血脉表缺少 bloodlines 数组。");
                return;
            }
            if (document.version != 1)
                Debug.LogWarning($"[ContentTable] 血脉表版本 {document.version} 尚未显式支持，将按版本 1 读取。");

            var idCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (BloodlineTableRecord record in document.bloodlines)
            {
                string id = record?.id?.Trim() ?? string.Empty;
                if (id.Length == 0) continue;
                idCounts.TryGetValue(id, out int count);
                idCounts[id] = count + 1;
            }
            var reportedDuplicateIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (BloodlineTableRecord record in document.bloodlines)
            {
                string id = record?.id?.Trim() ?? string.Empty;
                if (id.Length > 0 && idCounts[id] > 1)
                {
                    if (reportedDuplicateIds.Add(id))
                        Debug.LogError($"[ContentTable] 血脉表存在重复 id，全部同名条目已拒绝：{id}");
                    hasErrors = true;
                    continue;
                }
                if (record == null || id.Length == 0 || string.IsNullOrWhiteSpace(record.displayName) || record.drawWeight <= 0)
                {
                    hasErrors = true;
                    Debug.LogError($"[ContentTable] 血脉表包含空白或非正权重条目：{record?.id}");
                    continue;
                }
                definitions.Add(new HunterBloodlineDefinition(record.id, record.displayName, record.description, record.activationHint, record.drawWeight));
            }
        }

        public IReadOnlyList<HunterBloodlineDefinition> Definitions => definitions;
        public bool IsValid => !hasErrors && definitions.Count > 0;

        public bool TryGet(string bloodlineId, out HunterBloodlineDefinition definition)
        {
            string normalizedId = bloodlineId?.Trim() ?? string.Empty;
            definition = definitions.Find(candidate => string.Equals(candidate.Id, normalizedId, StringComparison.Ordinal));
            return definition != null;
        }
    }

    /// <summary>组合根级血脉入口；支持测试注入随机源，运行时默认使用统一规则抽取。</summary>
    public static class PlayableBloodlineRuntime
    {
        private static IHunterBloodlineContent content;
        private static IRandomSource random;

        public static IHunterBloodlineContent Content => content ??= EmptyBloodlineContent.Instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeState()
        {
            content = null;
            random = null;
        }

        public static void Configure(IHunterBloodlineContent configuredContent, IRandomSource randomSource = null)
        {
            content = configuredContent;
            random = randomSource;
        }

        private sealed class EmptyBloodlineContent : IHunterBloodlineContent
        {
            public static readonly EmptyBloodlineContent Instance = new();
            public IReadOnlyList<HunterBloodlineDefinition> Definitions { get; } = Array.Empty<HunterBloodlineDefinition>();
            public bool TryGet(string bloodlineId, out HunterBloodlineDefinition definition)
            {
                definition = null;
                return false;
            }
        }

        public static bool TryAssign(HunterInstance hunter, out string reason)
        {
            random ??= new SystemRandomSource();
            return TryAssign(hunter, random, out reason);
        }

        public static bool TryAssign(HunterInstance hunter, IRandomSource randomSource, out string reason) => HunterBloodlineRules.TryAssign(hunter, Content.Definitions, randomSource ?? throw new ArgumentNullException(nameof(randomSource)), out _, out reason);

        public static void Synchronize(SettlementInstance settlement)
        {
            random ??= new SystemRandomSource();
            Synchronize(settlement, random);
        }

        public static void Synchronize(SettlementInstance settlement, IRandomSource randomSource)
        {
            if (settlement?.Hunters == null) return;
            foreach (HunterInstance hunter in settlement.Hunters)
            {
                if (hunter == null) continue;
                if (!TryAssign(hunter, randomSource, out string reason))
                    Debug.LogWarning($"[Bloodline] 无法同步 {hunter.Name}：{reason}");
            }
        }
    }
}
