using System.Collections.Generic;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Combat;
using HuntingInDarkness.GameCore.Foundation;
using HuntingInDarkness.GameCore.Settlement;
using UnityEngine;

namespace HuntingInDarkness.Settlement
{
    /// <summary>
    /// 年鉴/Timeline 系统（纯 C#）。
    /// 职责：管理年份推进、事件调度（主线 + 随机）、年度狩猎记录。
    /// </summary>
    public class TimelineSystem : IDelayedEventScheduler
    {
        private readonly SettlementInstance _settlement;
        private readonly IRandomSource      _rng;
        private readonly HashSet<string> presentedMainStoryIds = new();
        private readonly HashSet<AnnalEntry> presentedScheduledEntries = new();
        private CampaignCalendarDefinition calendar;

        // 所有可用的事件模板（运行时加载，由 SettlementManager 注入）
        public List<EventData> RandomEventPool  { get; set; } = new();
        public List<EventData> MainStoryEvents  { get; set; } = new();
        public int CurrentYear => _settlement.CurrentYear;
        public int CurrentSeasonIndex => _settlement.CurrentSeasonIndex;
        public CampaignCalendarDefinition Calendar => calendar;
        public SeasonDefinition CurrentSeason
        {
            get
            {
                if (calendar == null || !calendar.TryGetSeason(CurrentSeasonIndex, out SeasonDefinition season)) return null;
                return season;
            }
        }
        public int TotalHunts => _settlement.HuntHistory?.Count ?? 0;

        public TimelineSystem(SettlementInstance settlement, IRandomSource rng)
        {
            _settlement = settlement;
            _rng        = rng;
        }

        public bool TryBindCalendar(CampaignCalendarDefinition definition, out string reason)
        {
            if (!CampaignCalendarRules.TryValidateDefinition(definition, out reason)) return false;
            calendar = definition;
            return true;
        }

        // ─── 年份推进 ────────────────────────────────────────────

        public List<EventData> AdvanceCalendar(HuntRecord huntRecord, out CampaignCalendarAdvancePlan advancePlan, out string reason)
        {
            advancePlan = default;
            if (!TryCreateCalendarAdvancePlan(huntRecord, out advancePlan, out reason)) return new List<EventData>();
            if (!_settlement.TryAppendHuntRecord(huntRecord))
            {
                reason = "远征归来记录无法追加到历史。";
                return new List<EventData>();
            }

            _settlement.CurrentYear = advancePlan.NextYear;
            _settlement.CurrentSeasonIndex = advancePlan.NextSeasonIndex;
            _settlement.HuntsCompletedThisYear = 0;
            if (!advancePlan.YearAdvanced) return new List<EventData>();
            Debug.Log($"[Timeline] 年份推进 → {advancePlan.NextYear}");
            return GetEventsForYear(advancePlan.NextYear);
        }

        public bool TryCreateCalendarAdvancePlan(HuntRecord huntRecord, out CampaignCalendarAdvancePlan advancePlan, out string reason)
        {
            advancePlan = default;
            reason = string.Empty;
            if (huntRecord == null || string.IsNullOrWhiteSpace(huntRecord.RecordId))
            {
                reason = "远征归来记录缺少稳定 ID。";
                return false;
            }
            if (HasAppliedHuntRecord(huntRecord))
            {
                reason = "远征归来记录已经提交。";
                return false;
            }
            if (huntRecord.Year != _settlement.CurrentYear)
            {
                reason = "远征归来年份与营地当前年份不一致。";
                return false;
            }
            if (calendar == null)
            {
                reason = "战役日历尚未绑定，拒绝推进营地时间。";
                return false;
            }
            return CampaignCalendarRules.TryCreateAdvancePlan(calendar, _settlement.CurrentYear, _settlement.CurrentSeasonIndex, out advancePlan, out reason);
        }

        public bool HasAppliedHuntRecord(HuntRecord huntRecord) => huntRecord != null && _settlement.HasHuntRecord(huntRecord.RecordId);

        // ─── 事件调度 ────────────────────────────────────────────

        /// <summary>
        /// 获取指定年份应触发的事件列表：
        /// 1. 主线事件（固定年份）
        /// 2. 随机事件（从可用池中抽取）
        /// </summary>
        public List<EventData> GetEventsForYear(int year)
        {
            var workItems = GetEventWorkItemsForYear(year);
            var events = new List<EventData>(workItems.Count);
            foreach (SettlementEventWork workItem in workItems)
                if (workItem.Event != null) events.Add(workItem.Event);
            return events;
        }

        /// <summary>获取事件及其精确年鉴条目，供 ActionQueue 提交时完成对应 occurrence。</summary>
        public List<SettlementEventWork> GetEventWorkItemsForYear(int year)
        {
            var result = new List<SettlementEventWork>();
            AppendDueScheduledEvents(year, result);

            // 主线事件：检查是否有该年份的主线事件
            foreach (var evt in MainStoryEvents)
            {
                if (evt == null) continue;
                string eventId = evt.ContentId;
                bool completed = _settlement.Timeline.Exists(entry => entry != null && entry.EventId == eventId && entry.IsCompleted);
                if (evt.minYear == year && evt.category == EventCategory.MainStory && !completed && presentedMainStoryIds.Add(eventId))
                {
                    var entry = _settlement.Timeline.Find(candidate => candidate != null && candidate.EventId == eventId && candidate.EntryType == TimelineEntryType.MainStory);
                    if (entry == null)
                    {
                        entry = new AnnalEntry { Year = year, EventId = eventId, EventName = evt.eventName, IsMilestone = true, EntryType = TimelineEntryType.MainStory };
                        _settlement.Timeline.Add(entry);
                    }
                    result.Add(new SettlementEventWork(evt, entry));
                }
            }

            // 随机事件：抽取1张
            if (_settlement.Timeline.Exists(entry => entry != null && entry.Year == year && entry.EntryType == TimelineEntryType.Random)) return result;
            var available = GetAvailableRandomEvents(year);
            string mostRecentEventId = _settlement.Timeline.FindLast(entry => entry != null && entry.EntryType == TimelineEntryType.Random)?.EventId;
            bool hasAlternative = available.Exists(gameEvent => !string.Equals(gameEvent.ContentId, mostRecentEventId, System.StringComparison.Ordinal));
            available.RemoveAll(gameEvent => EventRecencyRules.ShouldExcludeMostRecent(gameEvent.ContentId, mostRecentEventId, hasAlternative));
            if (available.Count > 0)
            {
                var picked = WeightedRandom(available, _rng);
                if (picked != null)
                {
                    // 将其标记为该年的记录
                    var entry = new AnnalEntry
                    {
                        Year      = year,
                        EventId   = picked.ContentId,
                        EventName = picked.eventName,
                        IsMilestone = picked.category == EventCategory.MainStory,
                        EntryType = TimelineEntryType.Random
                    };
                    _settlement.Timeline.Add(entry);
                    result.Add(new SettlementEventWork(picked, entry));
                }
            }

            return result;
        }

        private List<EventData> GetAvailableRandomEvents(int year)
        {
            var result = new List<EventData>();
            foreach (var evt in RandomEventPool)
            {
                if (evt == null) continue;
                if (evt.category != EventCategory.Random &&
                    evt.category != EventCategory.Settlement) continue;
                if (!SettlementTimelineRules.IsAvailableForYear(year, evt.minYear, evt.maxYear))
                    continue;
                result.Add(evt);
            }
            return result;
        }

        /// <summary>用稳定事件 ID 安排未来后果，同一事件同时只保留一个未完成日程。</summary>
        public bool TryScheduleEventAfterYears(string eventId, int delayYears, out string reason)
        {
            if (!DelayedEventRules.TryCreatePlan(_settlement.CurrentYear, delayYears, eventId, out DelayedEventPlan plan, out reason))
                return false;

            EventData gameEvent = ResolveEvent(plan.EventId);
            if (gameEvent == null)
            {
                reason = $"找不到事件内容：{plan.EventId}";
                return false;
            }
            if (gameEvent.category != EventCategory.Scheduled)
            {
                reason = $"事件 {plan.EventId} 不是 Scheduled 类别。";
                return false;
            }
            string canonicalEventId = gameEvent.ContentId;
            if (_settlement.Timeline.Exists(entry => entry != null && entry.EntryType == TimelineEntryType.Scheduled && entry.EventId == canonicalEventId && !entry.IsCompleted))
            {
                reason = $"事件 {canonicalEventId} 已经在未来时间线上。";
                return false;
            }

            _settlement.Timeline.Add(new AnnalEntry
            {
                Year = plan.DueYear,
                EventId = canonicalEventId,
                EventName = gameEvent.eventName,
                IsMilestone = gameEvent.category == EventCategory.MainStory,
                EntryType = TimelineEntryType.Scheduled
            });
            reason = string.Empty;
            return true;
        }

        private void AppendDueScheduledEvents(int year, List<SettlementEventWork> result)
        {
            foreach (AnnalEntry entry in _settlement.Timeline)
            {
                if (entry == null || entry.EntryType != TimelineEntryType.Scheduled || entry.IsCompleted || entry.Year > year || !presentedScheduledEntries.Add(entry))
                    continue;

                EventData gameEvent = ResolveEvent(entry.EventId);
                if (gameEvent != null)
                {
                    result.Add(new SettlementEventWork(gameEvent, entry));
                    continue;
                }

                presentedScheduledEntries.Remove(entry);
                Debug.LogWarning($"[Timeline] 延时事件内容缺失，将保留日程等待恢复：{entry.EventId}");
            }
        }

        public EventData ResolveEvent(string eventId)
        {
            if (string.IsNullOrWhiteSpace(eventId)) return null;
            string canonicalId = eventId.Trim();
            EventData resolved = null;
            foreach (EventData candidate in MainStoryEvents)
            {
                if (candidate == null || candidate.ContentId != canonicalId) continue;
                if (resolved != null) return null;
                resolved = candidate;
            }
            foreach (EventData candidate in RandomEventPool)
            {
                if (candidate == null || candidate.ContentId != canonicalId) continue;
                if (resolved != null) return null;
                resolved = candidate;
            }
            return resolved;
        }

        // ─── 工具 ────────────────────────────────────────────────

        private static T WeightedRandom<T>(List<T> items, IRandomSource rng)
            where T : EventData
        {
            if (items.Count == 0) return null;
            List<T> drawn = WeightedSelection.DrawWithoutReplacement(
                items, 1, item => item.drawWeight, rng);
            return drawn.Count > 0 ? drawn[0] : items[items.Count - 1];
        }
    }
}
