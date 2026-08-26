using System;
using System.Collections.Generic;
using HuntingInDarkness.GameCore.Settlement;
using UnityEngine;

namespace HuntingInDarkness.Settlement
{
    [Serializable]
    public sealed class CampaignCalendarSeasonConfig
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private int order;

        public string Id => id;
        public string DisplayName => displayName;
        public int Order => order;
    }

    [CreateAssetMenu(fileName = "CampaignCalendarConfig", menuName = "Hunting in Darkness/Campaign Calendar Config")]
    public sealed class CampaignCalendarConfig : ScriptableObject
    {
        [SerializeField] private string calendarId;
        [SerializeField] private string defaultSeasonId;
        [SerializeField] private List<CampaignCalendarSeasonConfig> seasons = new();

        public string CalendarId => calendarId;
        public string DefaultSeasonId => defaultSeasonId;
        public IReadOnlyList<CampaignCalendarSeasonConfig> Seasons => seasons;

        public bool TryCreateDefinition(out CampaignCalendarDefinition definition, out string reason)
        {
            definition = null;
            reason = string.Empty;
            var seasonDefinitions = new List<SeasonDefinition>();
            int defaultSeasonIndex = -1;
            foreach (CampaignCalendarSeasonConfig season in seasons ?? new List<CampaignCalendarSeasonConfig>())
            {
                if (season == null)
                {
                    reason = "战役日历包含空季节配置。";
                    return false;
                }
                if (string.Equals(season.Id, defaultSeasonId, StringComparison.Ordinal))
                    defaultSeasonIndex = seasonDefinitions.Count;
                seasonDefinitions.Add(new SeasonDefinition(season.Id, season.DisplayName, season.Order));
            }
            if (defaultSeasonIndex < 0)
            {
                reason = $"战役日历默认季节不存在：{defaultSeasonId}";
                return false;
            }

            definition = new CampaignCalendarDefinition(calendarId, seasonDefinitions, defaultSeasonIndex);
            if (!CampaignCalendarRules.TryValidateDefinition(definition, out reason))
            {
                definition = null;
                return false;
            }
            return true;
        }
    }
}
