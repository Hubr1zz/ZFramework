using System;
using System.Collections.Generic;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Settlement;
using UnityEngine;

namespace HuntingInDarkness.ContentTables
{
    [Serializable]
    public sealed class FacilityDutyPopulationBandTableRecord
    {
        public int minimumRoll;
        public int maximumRoll;
        public int populationGain;
    }

    [Serializable]
    public sealed class FacilityDutyTableRecord
    {
        public string id;
        public string facilityId;
        public string requiredInventionId;
        public string displayName;
        public string description;
        public string resultText;
        public int durationSeasons = 1;
        public string checkType = "PhysicalDice";
        public int diceCount = 1;
        public int diceSides = 6;
        public List<FacilityDutyPopulationBandTableRecord> populationBands = new();
    }

    [Serializable]
    public sealed class FacilityDutyTableDocument
    {
        public int version = 1;
        public List<FacilityDutyTableRecord> duties = new();
    }

    public static class PlayableFacilityDutyTable
    {
        public static bool Build(TextAsset tableAsset, IReadOnlyList<InventionData> inventions, out List<SettlementFacilityDutyDefinition> definitions, Action<string> reportError = null)
        {
            definitions = new List<SettlementFacilityDutyDefinition>();
            if (tableAsset == null)
            {
                reportError?.Invoke("设施值守表未配置。");
                return false;
            }
            FacilityDutyTableDocument document;
            try { document = JsonUtility.FromJson<FacilityDutyTableDocument>(tableAsset.text); }
            catch (Exception exception)
            {
                reportError?.Invoke($"设施值守表读取失败：{exception.Message}");
                return false;
            }
            if (document == null || document.version != 1 || document.duties == null)
            {
                reportError?.Invoke($"设施值守表版本或格式无效：{tableAsset.name}");
                return false;
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            bool hasError = false;
            foreach (FacilityDutyTableRecord record in document.duties)
            {
                if (record == null || string.IsNullOrWhiteSpace(record.id) || !ids.Add(record.id.Trim()))
                {
                    reportError?.Invoke("设施值守表包含空或重复稳定 ID。");
                    hasError = true;
                    continue;
                }
                if (!string.Equals(record.checkType?.Trim(), nameof(SettlementFacilityDutyCheckType.PhysicalDice), StringComparison.OrdinalIgnoreCase))
                {
                    reportError?.Invoke($"设施值守 {record.id} 使用不支持的判定类型。");
                    hasError = true;
                    continue;
                }
                bool knownInvention = string.IsNullOrWhiteSpace(record.requiredInventionId);
                if (!knownInvention)
                    foreach (InventionData invention in inventions ?? Array.Empty<InventionData>())
                        if (invention != null && string.Equals(invention.ContentId, record.requiredInventionId.Trim(), StringComparison.Ordinal))
                        {
                            knownInvention = true;
                            break;
                        }
                if (!knownInvention)
                {
                    reportError?.Invoke($"设施值守 {record.id} 引用未知发明：{record.requiredInventionId}");
                    hasError = true;
                    continue;
                }
                if (!string.IsNullOrWhiteSpace(record.requiredInventionId) && !string.Equals(record.facilityId?.Trim(), record.requiredInventionId.Trim(), StringComparison.Ordinal))
                {
                    reportError?.Invoke($"设施值守 {record.id} 的设施 ID 必须匹配前置发明 ID。");
                    hasError = true;
                    continue;
                }
                var bands = new List<SettlementFacilityDutyPopulationBand>();
                foreach (FacilityDutyPopulationBandTableRecord band in record.populationBands ?? new List<FacilityDutyPopulationBandTableRecord>())
                    if (band != null) bands.Add(new SettlementFacilityDutyPopulationBand(band.minimumRoll, band.maximumRoll, band.populationGain));
                var definition = new SettlementFacilityDutyDefinition(record.id.Trim(), record.facilityId?.Trim(), record.durationSeasons, SettlementFacilityDutyCheckType.PhysicalDice, bands, record.requiredInventionId?.Trim(), record.displayName, record.description, record.resultText, record.diceCount, record.diceSides);
                if (!SettlementFacilityDutyRules.TryValidateDefinition(definition, out string reason))
                {
                    reportError?.Invoke($"设施值守 {record.id} 无效：{reason}");
                    hasError = true;
                    continue;
                }
                definitions.Add(definition);
            }
            if (definitions.Count == 0)
            {
                reportError?.Invoke("设施值守表未提供有效岗位。");
                hasError = true;
            }
            return !hasError;
        }
    }
}
