using System.Collections.Generic;
using Core;
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

        // 所有可用的事件模板（运行时加载，由 SettlementManager 注入）
        public List<EventData> RandomEventPool  { get; set; } = new();
        public List<EventData> MainStoryEvents  { get; set; } = new();

        public TimelineSystem(SettlementInstance settlement, IRandomSource rng)
        {
            _settlement = settlement;
            _rng        = rng;
        }

        // ─── 年份推进 ────────────────────────────────────────────

        /// <summary>
        /// 狩猎结束后调用。推进年份，记录本次狩猎，调度下一年事件。
        /// </summary>
        public List<EventData> AdvanceYear(HuntRecord huntRecord)
        {
            // 记录本次狩猎
            _settlement.HuntHistory.Add(huntRecord);

            SettlementTimelineRules.HuntProgress progress = SettlementTimelineRules.CompleteHunt(_settlement.HuntsCompletedThisYear, _settlement.HuntsPerYear);
            _settlement.HuntsCompletedThisYear = progress.HuntsCompletedThisYear;
            if (!progress.ShouldAdvanceYear)
            {
                Debug.Log($"[Timeline] 本年狩猎进度 → {_settlement.HuntsCompletedThisYear}/{_settlement.HuntsPerYear}");
                return new List<EventData>();
            }

            _settlement.CurrentYear = SettlementTimelineRules.AdvanceYear(_settlement.CurrentYear);
            Debug.Log($"[Timeline] 年份推进 → {_settlement.CurrentYear}");
            EventBus.Publish(new YearAdvancedEvent { NewYear = _settlement.CurrentYear });

            // 获取该年应触发的事件列表
            var events = GetEventsForYear(_settlement.CurrentYear);
            return events;
        }

        /// <summary>仅推进年份（不记录狩猎，用于测试/跳过）</summary>
        public List<EventData> AdvanceYearOnly()
        {
            _settlement.HuntsCompletedThisYear = 0;
            _settlement.CurrentYear = SettlementTimelineRules.AdvanceYear(_settlement.CurrentYear);
            EventBus.Publish(new YearAdvancedEvent { NewYear = _settlement.CurrentYear });
            return GetEventsForYear(_settlement.CurrentYear);
        }

        // ─── 事件调度 ────────────────────────────────────────────

        /// <summary>
        /// 获取指定年份应触发的事件列表：
        /// 1. 主线事件（固定年份）
        /// 2. 随机事件（从可用池中抽取）
        /// </summary>
        public List<EventData> GetEventsForYear(int year)
        {
            var result = new List<EventData>();
            AppendDueScheduledEvents(year, result);

            // 主线事件：检查是否有该年份的主线事件
            foreach (var evt in MainStoryEvents)
            {
                if (evt == null) continue;
                bool completed = _settlement.Timeline.Exists(entry => entry.EventId == evt.name && entry.IsCompleted);
                if (evt.minYear == year && evt.category == EventCategory.MainStory && !completed && presentedMainStoryIds.Add(evt.name))
                {
                    result.Add(evt);
                    if (!_settlement.Timeline.Exists(entry => entry.EventId == evt.name))
                        _settlement.Timeline.Add(new AnnalEntry { Year = year, EventId = evt.name, EventName = evt.eventName, IsMilestone = true, EntryType = TimelineEntryType.MainStory });
                }
            }

            // 随机事件：抽取1张
            var available = GetAvailableRandomEvents(year);
            string mostRecentEventId = _settlement.Timeline.FindLast(entry => entry.EntryType == TimelineEntryType.Random)?.EventId;
            bool hasAlternative = available.Exists(gameEvent => !string.Equals(gameEvent.name, mostRecentEventId, System.StringComparison.Ordinal));
            available.RemoveAll(gameEvent => EventRecencyRules.ShouldExcludeMostRecent(gameEvent.name, mostRecentEventId, hasAlternative));
            if (available.Count > 0)
            {
                var picked = WeightedRandom(available, _rng);
                if (picked != null)
                {
                    result.Add(picked);
                    // 将其标记为该年的记录
                    _settlement.Timeline.Add(new AnnalEntry
                    {
                        Year      = year,
                        EventId   = picked.name,
                        EventName = picked.eventName,
                        IsMilestone = picked.category == EventCategory.MainStory,
                        EntryType = TimelineEntryType.Random
                    });
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

            EventData gameEvent = FindEvent(plan.EventId);
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
            if (_settlement.Timeline.Exists(entry => entry.EntryType == TimelineEntryType.Scheduled && entry.EventId == plan.EventId && !entry.IsCompleted))
            {
                reason = $"事件 {plan.EventId} 已经在未来时间线上。";
                return false;
            }

            _settlement.Timeline.Add(new AnnalEntry
            {
                Year = plan.DueYear,
                EventId = gameEvent.name,
                EventName = gameEvent.eventName,
                IsMilestone = gameEvent.category == EventCategory.MainStory,
                EntryType = TimelineEntryType.Scheduled
            });
            reason = string.Empty;
            return true;
        }

        private void AppendDueScheduledEvents(int year, List<EventData> result)
        {
            foreach (AnnalEntry entry in _settlement.Timeline)
            {
                if (entry == null || entry.EntryType != TimelineEntryType.Scheduled || entry.IsCompleted || entry.Year > year || !presentedScheduledEntries.Add(entry))
                    continue;

                EventData gameEvent = FindEvent(entry.EventId);
                if (gameEvent != null)
                {
                    result.Add(gameEvent);
                    continue;
                }

                presentedScheduledEntries.Remove(entry);
                Debug.LogWarning($"[Timeline] 延时事件内容缺失，将保留日程等待恢复：{entry.EventId}");
            }
        }

        private EventData FindEvent(string eventId)
        {
            EventData gameEvent = MainStoryEvents.Find(candidate => candidate != null && candidate.name == eventId);
            return gameEvent != null ? gameEvent : RandomEventPool.Find(candidate => candidate != null && candidate.name == eventId);
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
