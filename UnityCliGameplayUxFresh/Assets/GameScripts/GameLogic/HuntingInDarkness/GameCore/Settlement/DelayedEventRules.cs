using System;

namespace HuntingInDarkness.GameCore.Settlement
{
    public interface IDelayedEventScheduler
    {
        bool TryScheduleEventAfterYears(string eventId, int delayYears, out string reason);
    }

    public readonly struct DelayedEventPlan
    {
        public string EventId { get; }
        public int DueYear { get; }

        public DelayedEventPlan(string eventId, int dueYear)
        {
            EventId = eventId;
            DueYear = dueYear;
        }
    }

    /// <summary>验证事件延时，不依赖 Unity 内容资产或存档结构。</summary>
    public static class DelayedEventRules
    {
        public static bool TryCreatePlan(int currentYear, int delayYears, string eventId, out DelayedEventPlan plan, out string reason)
        {
            plan = default;
            if (currentYear < 1)
            {
                reason = "当前年份无效。";
                return false;
            }
            if (delayYears < 1)
            {
                reason = "延时事件必须安排在未来年份。";
                return false;
            }
            if (string.IsNullOrWhiteSpace(eventId))
            {
                reason = "延时事件缺少稳定事件 ID。";
                return false;
            }
            if (currentYear > int.MaxValue - delayYears)
            {
                reason = "延时事件年份超出可表示范围。";
                return false;
            }

            plan = new DelayedEventPlan(eventId.Trim(), currentYear + delayYears);
            reason = string.Empty;
            return true;
        }
    }
}
