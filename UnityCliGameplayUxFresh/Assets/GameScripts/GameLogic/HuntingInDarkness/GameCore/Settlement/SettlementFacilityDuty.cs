using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace HuntingInDarkness.GameCore.Settlement
{
    public enum SettlementFacilityDutyCheckType
    {
        PhysicalDice
    }

    public enum SettlementFacilityDutyStateStatus
    {
        Active,
        Resolved,
        Cancelled
    }

    public sealed class SettlementFacilityDutyPopulationBand
    {
        public SettlementFacilityDutyPopulationBand(int minimumRoll, int maximumRoll, int populationGain)
        {
            MinimumRoll = minimumRoll;
            MaximumRoll = maximumRoll;
            PopulationGain = populationGain;
        }

        public int MinimumRoll { get; }
        public int MaximumRoll { get; }
        public int PopulationGain { get; }
    }

    public sealed class SettlementFacilityDutyDefinition
    {
        public SettlementFacilityDutyDefinition(string dutyId, string requiredFacilityId, int durationSeasons, SettlementFacilityDutyCheckType checkType, IReadOnlyList<SettlementFacilityDutyPopulationBand> populationBands, string requiredInventionId = null, string displayName = null, string description = null, string resultText = null, int diceCount = 1, int diceSides = 6)
        {
            DutyId = dutyId ?? string.Empty;
            RequiredFacilityId = requiredFacilityId ?? string.Empty;
            RequiredInventionId = requiredInventionId ?? string.Empty;
            DurationSeasons = durationSeasons;
            CheckType = checkType;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? DutyId : displayName.Trim();
            Description = description ?? string.Empty;
            ResultText = resultText ?? string.Empty;
            DiceCount = diceCount;
            DiceSides = diceSides;
            var copiedBands = new List<SettlementFacilityDutyPopulationBand>();
            foreach (SettlementFacilityDutyPopulationBand band in populationBands ?? Array.Empty<SettlementFacilityDutyPopulationBand>())
                copiedBands.Add(band);
            PopulationBands = new ReadOnlyCollection<SettlementFacilityDutyPopulationBand>(copiedBands);
        }

        public string DutyId { get; }
        public string RequiredFacilityId { get; }
        public string RequiredInventionId { get; }
        public int DurationSeasons { get; }
        public SettlementFacilityDutyCheckType CheckType { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public string ResultText { get; }
        public int DiceCount { get; }
        public int DiceSides { get; }
        public IReadOnlyList<SettlementFacilityDutyPopulationBand> PopulationBands { get; }
    }

    [Serializable]
    public sealed class SettlementFacilityDutyState
    {
        public string DutyId;
        public string AssignmentId;
        public string CalendarId;
        public string FacilityId;
        public int AssignedHunterId;
        public int StartYear;
        public int StartSeasonIndex;
        public int DueYear;
        public int DueSeasonIndex;
        public SettlementFacilityDutyStateStatus Status;
    }

    public readonly struct SettlementFacilityDutyResolution
    {
        public SettlementFacilityDutyResolution(bool succeeded, int populationGain, int roll, string reason)
        {
            Succeeded = succeeded;
            PopulationGain = populationGain;
            Roll = roll;
            Reason = reason ?? string.Empty;
        }

        public bool Succeeded { get; }
        public int PopulationGain { get; }
        public int Roll { get; }
        public string Reason { get; }

        public static SettlementFacilityDutyResolution Failed(string reason) => new(false, 0, 0, reason);
    }

    public static class SettlementFacilityDutyRules
    {
        public static bool TryValidateDefinition(SettlementFacilityDutyDefinition definition, out string reason)
        {
            reason = string.Empty;
            if (definition == null || !IsStableId(definition.DutyId) || !IsStableId(definition.RequiredFacilityId))
                return Fail("值守定义缺少有效稳定 ID。", out reason);
            if (!string.IsNullOrWhiteSpace(definition.RequiredInventionId) && !IsStableId(definition.RequiredInventionId))
                return Fail("值守定义的前置发明 ID 无效。", out reason);
            if (definition.DurationSeasons < 1 || definition.DurationSeasons > 64)
                return Fail("值守持续季数必须为 1 至 64。", out reason);
            if (definition.CheckType != SettlementFacilityDutyCheckType.PhysicalDice)
                return Fail("值守判定类型不受支持。", out reason);
            if (definition.DiceCount < 1 || definition.DiceCount > 8 || definition.DiceSides < 2 || definition.DiceSides > 100)
                return Fail("值守骰子数量或面数无效。", out reason);
            if (definition.PopulationBands == null || definition.PopulationBands.Count == 0)
                return Fail("值守定义缺少人口结果区间。", out reason);

            int maximumRoll = definition.DiceCount * definition.DiceSides;
            int nextMinimum = definition.DiceCount;
            foreach (SettlementFacilityDutyPopulationBand band in definition.PopulationBands)
            {
                if (band == null || band.MinimumRoll != nextMinimum || band.MaximumRoll < band.MinimumRoll || band.MaximumRoll > maximumRoll || band.PopulationGain < 0)
                    return Fail($"值守人口结果区间必须完整覆盖 {definition.DiceCount} 至 {maximumRoll}。", out reason);
                nextMinimum = band.MaximumRoll + 1;
            }
            return nextMinimum == maximumRoll + 1 ? true : Fail($"值守人口结果区间必须完整覆盖 {definition.DiceCount} 至 {maximumRoll}。", out reason);
        }

        public static bool TryCreateState(SettlementFacilityDutyDefinition definition, string facilityId, int hunterId, int currentYear, int currentSeasonIndex, int seasonsPerYear, out SettlementFacilityDutyState state, out string reason)
        {
            state = null;
            if (!TryValidateDefinition(definition, out reason)) return false;
            if (!IsStableId(facilityId) || !string.Equals(facilityId.Trim(), definition.RequiredFacilityId, StringComparison.Ordinal))
                return Fail("值守设施与定义不匹配。", out reason);
            if (hunterId <= 0) return Fail("值守猎人 ID 无效。", out reason);
            if (!TryCalculateDueCoordinate(definition, currentYear, currentSeasonIndex, seasonsPerYear, out int dueYear, out int dueSeasonIndex, out reason)) return false;
            state = new SettlementFacilityDutyState
            {
                DutyId = definition.DutyId,
                AssignmentId = $"facility-duty:{definition.DutyId}:{Guid.NewGuid():N}",
                FacilityId = facilityId.Trim(),
                AssignedHunterId = hunterId,
                StartYear = currentYear,
                StartSeasonIndex = currentSeasonIndex,
                DueYear = dueYear,
                DueSeasonIndex = dueSeasonIndex,
                Status = SettlementFacilityDutyStateStatus.Active
            };
            return true;
        }

        public static bool TryCalculateDueCoordinate(SettlementFacilityDutyDefinition definition, int startYear, int startSeasonIndex, int seasonsPerYear, out int dueYear, out int dueSeasonIndex, out string reason)
        {
            dueYear = 0;
            dueSeasonIndex = 0;
            if (!TryValidateDefinition(definition, out reason) || !TryValidateCoordinate(startYear, startSeasonIndex, seasonsPerYear, out reason)) return false;
            long absoluteDue = ((long)startYear - 1L) * seasonsPerYear + startSeasonIndex + definition.DurationSeasons;
            long maximumAbsolute = ((long)int.MaxValue * seasonsPerYear) - 1L;
            if (absoluteDue > maximumAbsolute) return Fail("值守到期季节超过可表示范围。", out reason);
            dueYear = (int)(absoluteDue / seasonsPerYear) + 1;
            dueSeasonIndex = (int)(absoluteDue % seasonsPerYear);
            return true;
        }

        public static bool IsDue(SettlementFacilityDutyState state, int currentYear, int currentSeasonIndex)
        {
            if (state == null || state.Status != SettlementFacilityDutyStateStatus.Active) return false;
            return currentYear > state.DueYear || currentYear == state.DueYear && currentSeasonIndex >= state.DueSeasonIndex;
        }

        public static bool IsAssigned(SettlementFacilityDutyState state, int hunterId)
        {
            return state != null && state.Status == SettlementFacilityDutyStateStatus.Active && state.AssignedHunterId == hunterId;
        }

        public static bool TryResolve(SettlementFacilityDutyDefinition definition, int roll, out SettlementFacilityDutyResolution resolution)
        {
            resolution = SettlementFacilityDutyResolution.Failed(string.Empty);
            if (!TryValidateDefinition(definition, out string reason))
            {
                resolution = SettlementFacilityDutyResolution.Failed(reason);
                return false;
            }
            int maximumRoll = definition.DiceCount * definition.DiceSides;
            if (roll < definition.DiceCount || roll > maximumRoll)
            {
                resolution = SettlementFacilityDutyResolution.Failed($"值守骰点必须为 {definition.DiceCount} 至 {maximumRoll}。");
                return false;
            }
            foreach (SettlementFacilityDutyPopulationBand band in definition.PopulationBands)
                if (roll >= band.MinimumRoll && roll <= band.MaximumRoll)
                {
                    resolution = new SettlementFacilityDutyResolution(true, band.PopulationGain, roll, string.Empty);
                    return true;
                }
            resolution = SettlementFacilityDutyResolution.Failed("值守骰点没有对应人口结果。");
            return false;
        }

        public static int SaturatePopulation(int currentPopulation, int populationGain)
        {
            long next = (long)Math.Max(0, currentPopulation) + Math.Max(0, populationGain);
            return next >= int.MaxValue ? int.MaxValue : (int)next;
        }

        public static bool HasDueDuty(IReadOnlyList<SettlementFacilityDutyState> duties, int currentYear, int currentSeasonIndex)
        {
            if (duties == null) return false;
            foreach (SettlementFacilityDutyState duty in duties)
                if (IsDue(duty, currentYear, currentSeasonIndex)) return true;
            return false;
        }

        private static bool TryValidateCoordinate(int year, int seasonIndex, int seasonsPerYear, out string reason)
        {
            reason = string.Empty;
            if (year < 1 || seasonsPerYear < 1 || seasonsPerYear > 64 || seasonIndex < 0 || seasonIndex >= seasonsPerYear)
                return Fail("值守季节坐标无效。", out reason);
            return true;
        }

        private static bool IsStableId(string value)
        {
            return !string.IsNullOrWhiteSpace(value) && value.Length <= 64 && value == value.Trim();
        }

        private static bool Fail(string message, out string reason)
        {
            reason = message;
            return false;
        }
    }
}
