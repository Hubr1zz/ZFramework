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
    public class TimelineSystem
    {
        private readonly SettlementInstance _settlement;
        private readonly IRandomSource      _rng;

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

            // 推进年份
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

            // 主线事件：检查是否有该年份的主线事件
            foreach (var evt in MainStoryEvents)
            {
                if (evt == null) continue;
                if (evt.minYear == year && evt.category == EventCategory.MainStory)
                    result.Add(evt);
            }

            // 随机事件：抽取1张
            var available = GetAvailableRandomEvents(year);
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

        /// <summary>向未来年份动态添加事件（如发明解锁触发）</summary>
        public void ScheduleEventForYear(int year, EventData evt, TimelineEntryType type)
        {
            _settlement.Timeline.Add(new AnnalEntry
            {
                Year      = year,
                EventId   = evt.name,
                EventName = evt.eventName,
                IsMilestone = evt.category == EventCategory.MainStory,
                EntryType = type
            });
            EventBus.Publish(new GameEventTriggeredEvent { EventId = evt.name });
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
