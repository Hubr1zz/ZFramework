using System.Collections.Generic;
using HuntingInDarkness.Data;

namespace HuntingInDarkness.ActionFlow.Events
{
    public struct PlayableEventDuplicatePreventedEvent
    {
        public string EventId;
    }

    /// <summary>单个事件 Root 的有限去重边界；阻止循环引用和重复资产在同一因果链中反复结算。</summary>
    public sealed class PlayableEventChainGuard
    {
        private readonly HashSet<EventData> scheduledEvents = new();

        public bool TrySchedule(EventData gameEvent)
        {
            return gameEvent != null && scheduledEvents.Add(gameEvent);
        }
    }
}
