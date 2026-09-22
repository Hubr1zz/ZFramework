using System;
using System.Collections.Generic;
using HuntingInDarkness.GameCore.Content;
using HuntingInDarkness.GameCore.Foundation;
using UnityEngine;

namespace HuntingInDarkness.ContentTables
{
    [Serializable]
    public sealed class TraitTableRecord : IStableContentRecord
    {
        public string id;
        public string displayName;
        public List<string> aliases = new();
        public List<string> keywords = new();

        public string Id => id;
    }

    [Serializable]
    public sealed class TraitTableDocument
    {
        public int version = 1;
        public List<TraitTableRecord> traits = new();
    }

    public sealed class PlayableTraitDefinition
    {
        internal PlayableTraitDefinition(string id, string displayName, IReadOnlyList<string> aliases, IReadOnlyList<string> keywords)
        {
            Id = id;
            DisplayName = displayName;
            Aliases = aliases;
            Keywords = keywords;
        }

        public string Id { get; }
        public string DisplayName { get; }
        public IReadOnlyList<string> Aliases { get; }
        public IReadOnlyList<string> Keywords { get; }
    }

    public sealed class PlayableTraitCatalog
    {
        private readonly Dictionary<string, PlayableTraitDefinition> byIdentifier;
        private readonly HashSet<string> canonicalIds;

        private PlayableTraitCatalog(List<PlayableTraitDefinition> definitions, Dictionary<string, PlayableTraitDefinition> byIdentifier, HashSet<string> canonicalIds)
        {
            Definitions = definitions.AsReadOnly();
            this.byIdentifier = byIdentifier;
            this.canonicalIds = canonicalIds;
        }

        public IReadOnlyList<PlayableTraitDefinition> Definitions { get; }

        public bool ContainsCanonicalId(string identifier) => canonicalIds.Contains(Normalize(identifier));

        public bool TryResolve(string identifier, out PlayableTraitDefinition definition) => byIdentifier.TryGetValue(Normalize(identifier), out definition);

        public string ResolveContentId(string identifier) => TryResolve(identifier, out PlayableTraitDefinition definition) ? definition.Id : Normalize(identifier);

        public string GetDisplayName(string identifier) => TryResolve(identifier, out PlayableTraitDefinition definition) ? definition.DisplayName : Normalize(identifier);

        public void AddKeywords(ISet<string> keywords, IReadOnlyCollection<string> traitIds)
        {
            if (keywords == null) throw new ArgumentNullException(nameof(keywords));
            if (traitIds == null) return;
            foreach (string traitId in traitIds)
            {
                if (!TryResolve(traitId, out PlayableTraitDefinition definition))
                {
                    KeywordRules.TryAdd(keywords, traitId);
                    continue;
                }
                KeywordRules.TryAdd(keywords, definition.Id);
                KeywordRules.TryAdd(keywords, definition.DisplayName);
                foreach (string alias in definition.Aliases)
                    KeywordRules.TryAdd(keywords, alias);
                foreach (string keyword in definition.Keywords)
                    KeywordRules.TryAdd(keywords, keyword);
            }
        }

        public static bool TryLoad(TextAsset tableAsset, out PlayableTraitCatalog catalog, out string reason)
        {
            catalog = null;
            TextAsset source = tableAsset;
            if (source == null)
            {
                reason = "缺少特性表内容源。";
                return false;
            }
            TraitTableDocument document;
            try
            {
                document = JsonUtility.FromJson<TraitTableDocument>(source.text);
            }
            catch (Exception exception)
            {
                reason = $"特性表无法解析：{exception.Message}";
                return false;
            }
            if (document?.traits == null || document.version != 1)
            {
                reason = $"特性表版本或结构无效：{document?.version ?? 0}";
                return false;
            }
            return TryCreate(document.traits, out catalog, out reason);
        }

        internal static bool TryCreate(IReadOnlyList<TraitTableRecord> records, out PlayableTraitCatalog catalog, out string reason)
        {
            catalog = null;
            var definitions = new List<PlayableTraitDefinition>();
            var index = new Dictionary<string, PlayableTraitDefinition>(StringComparer.Ordinal);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (TraitTableRecord record in records ?? Array.Empty<TraitTableRecord>())
            {
                string id = Normalize(record?.id);
                string displayName = Normalize(record?.displayName);
                if (record == null || !IsStableId(id) || displayName.Length == 0 || !ids.Add(id))
                {
                    reason = $"特性记录缺少唯一稳定 ID 或显示名：{id}";
                    return false;
                }
                var aliases = NormalizeDistinct(record.aliases);
                var keywords = NormalizeDistinct(record.keywords);
                var definition = new PlayableTraitDefinition(id, displayName, aliases.AsReadOnly(), keywords.AsReadOnly());
                definitions.Add(definition);
                if (!TryAddIdentifier(index, id, definition, out reason) || !TryAddIdentifier(index, displayName, definition, out reason)) return false;
                foreach (string alias in aliases)
                    if (!TryAddIdentifier(index, alias, definition, out reason)) return false;
            }
            if (definitions.Count == 0)
            {
                reason = "特性表没有提供任何内容。";
                return false;
            }
            definitions.Sort((left, right) => string.CompareOrdinal(left.Id, right.Id));
            catalog = new PlayableTraitCatalog(definitions, index, ids);
            reason = string.Empty;
            return true;
        }

        private static bool TryAddIdentifier(IDictionary<string, PlayableTraitDefinition> index, string identifier, PlayableTraitDefinition definition, out string reason)
        {
            string key = Normalize(identifier);
            if (key.Length == 0)
            {
                reason = string.Empty;
                return true;
            }
            if (index.TryGetValue(key, out PlayableTraitDefinition existing) && !ReferenceEquals(existing, definition))
            {
                reason = $"特性稳定 ID、显示名或旧别名冲突：{key}";
                return false;
            }
            index[key] = definition;
            reason = string.Empty;
            return true;
        }

        private static List<string> NormalizeDistinct(IReadOnlyList<string> values)
        {
            var result = new List<string>();
            var known = new HashSet<string>(StringComparer.Ordinal);
            foreach (string value in values ?? Array.Empty<string>())
            {
                string normalized = Normalize(value);
                if (normalized.Length > 0 && known.Add(normalized)) result.Add(normalized);
            }
            return result;
        }

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
