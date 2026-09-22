using System;
using System.Collections.Generic;
using HuntingInDarkness.Settlement;
using UnityEngine;

namespace HuntingInDarkness.Bootstrap
{
    [CreateAssetMenu(fileName = "HuntingInDarknessContentSources", menuName = "Hunting in Darkness/Content Source Manifest")]
    public sealed class PlayableContentSourceManifest : ScriptableObject
    {
        [SerializeField] private string manifestId = "hunting-in-darkness-content-v1";
        [SerializeField] private int schemaVersion = 1;
        [SerializeField] private PlayableBootstrapSettings settings;
        [SerializeField] private TextAsset eventsTable;
        [SerializeField] private TextAsset bloodlineEventsTable;
        [SerializeField] private TextAsset cardInteractionEventsTable;
        [SerializeField] private TextAsset huntEventsTable;
        [SerializeField] private TextAsset bloodlinesTable;
        [SerializeField] private TextAsset itemsTable;
        [SerializeField] private TextAsset recipesTable;
        [SerializeField] private List<PlayableSettlementContentExtension> settlementExtensions = new();
        [SerializeField] private Font chineseFont;

        public string ManifestId => manifestId;
        public int SchemaVersion => schemaVersion;
        public PlayableBootstrapSettings Settings => settings;
        public TextAsset EventsTable => eventsTable;
        public TextAsset BloodlineEventsTable => bloodlineEventsTable;
        public TextAsset CardInteractionEventsTable => cardInteractionEventsTable;
        public TextAsset HuntEventsTable => huntEventsTable;
        public TextAsset BloodlinesTable => bloodlinesTable;
        public TextAsset ItemsTable => itemsTable;
        public TextAsset RecipesTable => recipesTable;
        public IReadOnlyList<PlayableSettlementContentExtension> SettlementExtensions => settlementExtensions;
        public Font ChineseFont => chineseFont;

        public bool TryCreateBundle(out PlayableContentSourceBundle bundle, out PlayableContentDiagnosticReport report)
        {
            bundle = null;
            report = new PlayableContentDiagnosticReport();
            if (string.IsNullOrWhiteSpace(manifestId) || manifestId.Length > 128)
                report.AddError("sources.manifest.id", "内容源 Manifest 缺少有效稳定 ID。");
            if (schemaVersion != 1)
                report.AddError("sources.manifest.schema", $"不支持的内容源 Manifest schema：{schemaVersion}。");
            ValidateRequired(settings, "settings", report);
            ValidateRequired(eventsTable, "eventsTable", report);
            ValidateRequired(bloodlineEventsTable, "bloodlineEventsTable", report);
            ValidateRequired(cardInteractionEventsTable, "cardInteractionEventsTable", report);
            ValidateRequired(huntEventsTable, "huntEventsTable", report);
            ValidateRequired(bloodlinesTable, "bloodlinesTable", report);
            ValidateRequired(itemsTable, "itemsTable", report);
            ValidateRequired(recipesTable, "recipesTable", report);
            ValidateRequired(chineseFont, "chineseFont", report);
            ValidateDistinctTables(report);
            ValidateExtensions(report);
            if (report.HasErrors) return false;

            bundle = new PlayableContentSourceBundle(this, settings, eventsTable, bloodlineEventsTable, cardInteractionEventsTable, huntEventsTable, bloodlinesTable, itemsTable, recipesTable, settlementExtensions, chineseFont);
            return true;
        }

        private void ValidateDistinctTables(PlayableContentDiagnosticReport report)
        {
            var seen = new HashSet<UnityEngine.Object>();
            AddDistinct(eventsTable, "eventsTable", seen, report);
            AddDistinct(bloodlineEventsTable, "bloodlineEventsTable", seen, report);
            AddDistinct(cardInteractionEventsTable, "cardInteractionEventsTable", seen, report);
            AddDistinct(huntEventsTable, "huntEventsTable", seen, report);
            AddDistinct(bloodlinesTable, "bloodlinesTable", seen, report);
            AddDistinct(itemsTable, "itemsTable", seen, report);
            AddDistinct(recipesTable, "recipesTable", seen, report);
        }

        private void ValidateExtensions(PlayableContentDiagnosticReport report)
        {
            var seen = new HashSet<PlayableSettlementContentExtension>();
            if (settlementExtensions == null)
            {
                report.AddError("sources.extensions.missing", "Settlement 内容扩展列表为空。");
                return;
            }
            for (int index = 0; index < settlementExtensions.Count; index++)
            {
                PlayableSettlementContentExtension extension = settlementExtensions[index];
                if (extension == null)
                {
                    report.AddError("sources.extensions.empty", $"Settlement 内容扩展 {index} 为空。");
                    continue;
                }
                if (!seen.Add(extension)) report.AddError("sources.extensions.duplicate", $"Settlement 内容扩展重复：{extension.name}。");
                if ((extension.Items?.Count ?? 0) == 0 && (extension.Recipes?.Count ?? 0) == 0)
                    report.AddError("sources.extensions.empty", $"Settlement 内容扩展没有物品或配方：{extension.name}。");
            }
        }

        private static void ValidateRequired(UnityEngine.Object value, string fieldName, PlayableContentDiagnosticReport report)
        {
            if (value == null) report.AddError("sources.required", $"内容源缺少 {fieldName}。");
        }

        private static void AddDistinct(UnityEngine.Object value, string fieldName, HashSet<UnityEngine.Object> seen, PlayableContentDiagnosticReport report)
        {
            if (value != null && !seen.Add(value)) report.AddError("sources.tables.duplicate", $"内容表重复引用：{fieldName}。");
        }
    }

    public sealed class PlayableContentSourceBundle
    {
        internal PlayableContentSourceBundle(PlayableContentSourceManifest manifest, PlayableBootstrapSettings settings, TextAsset eventsTable, TextAsset bloodlineEventsTable, TextAsset cardInteractionEventsTable, TextAsset huntEventsTable, TextAsset bloodlinesTable, TextAsset itemsTable, TextAsset recipesTable, IReadOnlyList<PlayableSettlementContentExtension> settlementExtensions, Font chineseFont)
        {
            Manifest = manifest;
            Settings = settings;
            EventsTable = eventsTable;
            BloodlineEventsTable = bloodlineEventsTable;
            CardInteractionEventsTable = cardInteractionEventsTable;
            HuntEventsTable = huntEventsTable;
            BloodlinesTable = bloodlinesTable;
            ItemsTable = itemsTable;
            RecipesTable = recipesTable;
            SettlementExtensions = new List<PlayableSettlementContentExtension>(settlementExtensions).AsReadOnly();
            ChineseFont = chineseFont;
        }

        public PlayableContentSourceManifest Manifest { get; }
        public PlayableBootstrapSettings Settings { get; }
        public TextAsset EventsTable { get; }
        public TextAsset BloodlineEventsTable { get; }
        public TextAsset CardInteractionEventsTable { get; }
        public TextAsset HuntEventsTable { get; }
        public TextAsset BloodlinesTable { get; }
        public TextAsset ItemsTable { get; }
        public TextAsset RecipesTable { get; }
        public IReadOnlyList<PlayableSettlementContentExtension> SettlementExtensions { get; }
        public Font ChineseFont { get; }
    }
}
