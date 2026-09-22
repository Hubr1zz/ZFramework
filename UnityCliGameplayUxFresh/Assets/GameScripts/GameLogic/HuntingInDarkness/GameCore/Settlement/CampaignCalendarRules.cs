using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace HuntingInDarkness.GameCore.Settlement
{
    public sealed class SeasonDefinition
    {
        public SeasonDefinition(string id, string displayName, int order)
        {
            Id = id ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            Order = order;
        }

        public string Id { get; }
        public string DisplayName { get; }
        public int Order { get; }
    }

    public sealed class CampaignCalendarDefinition
    {
        public CampaignCalendarDefinition(string calendarId, IReadOnlyList<SeasonDefinition> seasons, int defaultSeasonIndex = 0)
        {
            CalendarId = calendarId ?? string.Empty;
            var copiedSeasons = new List<SeasonDefinition>();
            foreach (SeasonDefinition season in seasons ?? Array.Empty<SeasonDefinition>())
                copiedSeasons.Add(season);
            Seasons = new ReadOnlyCollection<SeasonDefinition>(copiedSeasons);
            DefaultSeasonIndex = defaultSeasonIndex;
        }

        public string CalendarId { get; }
        public IReadOnlyList<SeasonDefinition> Seasons { get; }
        public int DefaultSeasonIndex { get; }

        public bool TryGetSeason(int index, out SeasonDefinition season)
        {
            season = null;
            if (index < 0 || index >= Seasons.Count) return false;
            season = Seasons[index];
            return season != null;
        }

    }

    public readonly struct CampaignCalendarAdvancePlan
    {
        internal CampaignCalendarAdvancePlan(int currentYear, int currentSeasonIndex, int nextYear, int nextSeasonIndex, bool yearAdvanced)
        {
            CurrentYear = currentYear;
            CurrentSeasonIndex = currentSeasonIndex;
            NextYear = nextYear;
            NextSeasonIndex = nextSeasonIndex;
            YearAdvanced = yearAdvanced;
        }

        public int CurrentYear { get; }
        public int CurrentSeasonIndex { get; }
        public int NextYear { get; }
        public int NextSeasonIndex { get; }
        public bool YearAdvanced { get; }
        public bool SeasonAdvanced => CurrentYear != NextYear || CurrentSeasonIndex != NextSeasonIndex;
    }

    public static class CampaignCalendarRules
    {
        public static bool TryValidateDefinition(CampaignCalendarDefinition definition, out string reason)
        {
            reason = string.Empty;
            if (definition == null || !IsStableId(definition.CalendarId))
                return Fail("战役日历 ID 无效。", out reason);
            if (definition.Seasons == null || definition.Seasons.Count == 0 || definition.Seasons.Count > 64)
                return Fail("战役日历必须包含 1 至 64 个季节。", out reason);
            if (definition.DefaultSeasonIndex < 0 || definition.DefaultSeasonIndex >= definition.Seasons.Count)
                return Fail("战役日历默认季节越界。", out reason);
            if (definition.DefaultSeasonIndex != 0)
                return Fail("战役日历默认季节必须是列表首项。", out reason);

            var ids = new HashSet<string>(StringComparer.Ordinal);
            var orders = new HashSet<int>();
            for (int index = 0; index < definition.Seasons.Count; index++)
            {
                SeasonDefinition season = definition.Seasons[index];
                if (season == null || !IsStableId(season.Id) || string.IsNullOrWhiteSpace(season.DisplayName) || season.DisplayName != season.DisplayName.Trim())
                    return Fail("战役日历包含无效季节定义。", out reason);
                if (season.Order != index || !ids.Add(season.Id.Trim()) || !orders.Add(season.Order))
                    return Fail("战役日历包含重复季节 ID 或顺序。", out reason);
            }

            return true;
        }

        public static bool TryCreateAdvancePlan(CampaignCalendarDefinition definition, int currentYear, int currentSeasonIndex, out CampaignCalendarAdvancePlan plan, out string reason)
        {
            plan = default;
            if (!TryValidateDefinition(definition, out reason)) return false;
            if (currentYear < 1)
                return Fail("当前年份无效。", out reason);
            if (currentSeasonIndex < 0 || currentSeasonIndex >= definition.Seasons.Count)
                return Fail("当前季节越界。", out reason);

            bool yearAdvanced = currentSeasonIndex == definition.Seasons.Count - 1;
            if (yearAdvanced && currentYear == int.MaxValue)
                return Fail("年份已达到可表示上限。", out reason);

            plan = new CampaignCalendarAdvancePlan(currentYear, currentSeasonIndex, yearAdvanced ? currentYear + 1 : currentYear, yearAdvanced ? 0 : currentSeasonIndex + 1, yearAdvanced);
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
