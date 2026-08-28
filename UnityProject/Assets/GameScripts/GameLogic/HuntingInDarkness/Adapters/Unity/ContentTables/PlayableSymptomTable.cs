using System;
using System.Collections.Generic;
using HuntingInDarkness.GameCore.Content;
using HuntingInDarkness.Settlement;
using UnityEngine;

namespace HuntingInDarkness.ContentTables
{
    [Serializable]
    public sealed class SymptomStatModifierTableRecord
    {
        public int strength;
        public int accuracy;
        public int evasion;
        public int movement;
    }

    [Serializable]
    public sealed class SymptomTableRecord : IStableContentRecord
    {
        public string id;
        public string displayName;
        public List<string> aliases = new();
        public string description;
        public SymptomStatModifierTableRecord negative = new();
        public SymptomStatModifierTableRecord internalized = new();
        public int internalizationThreshold = 2;
        public int reflectionWillpowerCost = 1;
        public int overcomeCourageRequirement = 2;
        public int overcomeGrowthCost = 1;

        public string Id => id;
    }

    [Serializable]
    public sealed class SymptomTableDocument
    {
        public int version = 1;
        public List<SymptomTableRecord> symptoms = new();
    }

    public static class PlayableSymptomTable
    {
        public static bool TryLoad(TextAsset tableAsset, out IReadOnlyList<PlayableSymptomDefinition> definitions, out string reason)
        {
            definitions = null;
            TextAsset source = tableAsset;
            if (source == null)
            {
                reason = "缺少症状表内容源。";
                return false;
            }
            SymptomTableDocument document;
            try
            {
                document = JsonUtility.FromJson<SymptomTableDocument>(source.text);
            }
            catch (Exception exception)
            {
                reason = $"症状表无法解析：{exception.Message}";
                return false;
            }
            if (document?.symptoms == null || document.version != 1)
            {
                reason = $"症状表版本或结构无效：{document?.version ?? 0}";
                return false;
            }
            if (!TryCreate(document.symptoms, out List<PlayableSymptomDefinition> created, out reason)) return false;
            definitions = created.AsReadOnly();
            return true;
        }

        internal static bool TryCreate(IReadOnlyList<SymptomTableRecord> records, out List<PlayableSymptomDefinition> definitions, out string reason)
        {
            definitions = new List<PlayableSymptomDefinition>();
            var references = new HashSet<string>(StringComparer.Ordinal);
            foreach (SymptomTableRecord record in records ?? Array.Empty<SymptomTableRecord>())
            {
                string id = Normalize(record?.id);
                string displayName = Normalize(record?.displayName);
                if (record == null || !IsStableId(id) || displayName.Length == 0 || !TryAddReference(references, id) || !TryAddReference(references, displayName))
                {
                    reason = $"症状记录缺少唯一稳定 ID 或显示名：{id}";
                    return false;
                }
                var aliases = new List<string>();
                foreach (string alias in record.aliases ?? new List<string>())
                {
                    string normalized = Normalize(alias);
                    if (normalized.Length == 0 || !TryAddReference(references, normalized))
                    {
                        reason = $"症状稳定 ID、显示名或旧别名冲突：{normalized}";
                        return false;
                    }
                    aliases.Add(normalized);
                }
                if (record.internalizationThreshold < 1 || record.reflectionWillpowerCost < 0 || record.overcomeCourageRequirement < 0 || record.overcomeGrowthCost < 0)
                {
                    reason = $"症状 {id} 的成长参数无效。";
                    return false;
                }
                definitions.Add(new PlayableSymptomDefinition(id, displayName, aliases, Normalize(record.description), Read(record.negative), Read(record.internalized), record.internalizationThreshold, record.reflectionWillpowerCost, record.overcomeCourageRequirement, record.overcomeGrowthCost));
            }
            if (definitions.Count == 0)
            {
                reason = "症状表没有提供任何内容。";
                return false;
            }
            definitions.Sort((left, right) => string.CompareOrdinal(left.Id, right.Id));
            reason = string.Empty;
            return true;
        }

        private static SymptomStatModifierTableRecord Read(SymptomStatModifierTableRecord modifiers) => modifiers ?? new SymptomStatModifierTableRecord();

        private static bool TryAddReference(ISet<string> references, string value) => references.Add(value);

        private static bool IsStableId(string value)
        {
            if (value.Length == 0) return false;
            foreach (char character in value)
                if (!(character >= 'a' && character <= 'z') && !(character >= '0' && character <= '9') && character != '_' && character != '-' && character != ':' && character != '.')
                    return false;
            return true;
        }

        private static string Normalize(string value) => value?.Trim() ?? string.Empty;
    }
}
