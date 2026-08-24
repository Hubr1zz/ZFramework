using System;
using System.Collections.Generic;
using HuntingInDarkness.Data;

namespace HuntingInDarkness.Settlement
{
    /// <summary>读档后把未完成年鉴事件投影到营地 ActionQueue 的一次性计划。</summary>
    public readonly struct SettlementEventRestorePlan
    {
        private SettlementEventRestorePlan(bool succeeded, IReadOnlyList<EventData> events, string failureReason, bool alreadyInProgress)
        {
            Succeeded = succeeded;
            Events = events ?? Array.Empty<EventData>();
            FailureReason = failureReason ?? string.Empty;
            AlreadyInProgress = alreadyInProgress;
        }

        public bool Succeeded { get; }
        public IReadOnlyList<EventData> Events { get; }
        public string FailureReason { get; }
        public bool AlreadyInProgress { get; }
        public bool HasPendingEvents => Events.Count > 0;

        public static SettlementEventRestorePlan Success(IReadOnlyList<EventData> events) => new(true, events, string.Empty, false);
        public static SettlementEventRestorePlan InProgress() => new(true, Array.Empty<EventData>(), string.Empty, true);
        public static SettlementEventRestorePlan Failed(string reason) => new(false, Array.Empty<EventData>(), reason, false);
    }

    /// <summary>
    /// 维护一次读档数据绑定期间的恢复投影状态。
    /// 以 AnnalEntry 引用区分同一事件 ID 的多个年鉴实例，不按 EventId 去重。
    /// </summary>
    public sealed class SettlementEventRestoreProjection
    {
        private readonly HashSet<AnnalEntry> projectedEntries = new();
        private SettlementInstance settlement;
        private Func<string, EventData> resolveEvent;
        private bool restoreInProgress;
        private bool ready = true;
        private string failureReason = string.Empty;

        public SettlementEventRestoreProjection(SettlementInstance settlement, Func<string, EventData> resolveEvent)
        {
            Bind(settlement, resolveEvent);
        }

        public bool IsReady => ready;
        public bool HasPendingEvents => !ready && string.IsNullOrEmpty(failureReason);
        public string FailureReason => failureReason;

        public void Bind(SettlementInstance nextSettlement, Func<string, EventData> nextResolveEvent)
        {
            settlement = nextSettlement;
            resolveEvent = nextResolveEvent;
            projectedEntries.Clear();
            restoreInProgress = false;
            ready = true;
            failureReason = string.Empty;
        }

        public SettlementEventRestorePlan Prepare()
        {
            if (restoreInProgress) return SettlementEventRestorePlan.InProgress();
            if (!ready) return SettlementEventRestorePlan.Failed(failureReason);
            if (settlement == null) return Reject("营地存档为空，无法恢复未完成事件。");
            if (resolveEvent == null) return Reject("营地事件内容解析器尚未配置。");

            var events = new List<EventData>();
            var newEntries = new List<AnnalEntry>();
            foreach (AnnalEntry entry in settlement.Timeline ?? new List<AnnalEntry>())
            {
                if (entry == null || entry.IsCompleted || projectedEntries.Contains(entry)) continue;
                if (string.IsNullOrWhiteSpace(entry.EventId)) return Reject("未完成年鉴条目缺少事件 ID。");

                EventData gameEvent = resolveEvent(entry.EventId);
                if (gameEvent == null) return Reject($"找不到未完成年鉴事件内容：{entry.EventId}");

                events.Add(gameEvent);
                newEntries.Add(entry);
            }

            if (events.Count == 0) return SettlementEventRestorePlan.Success(Array.Empty<EventData>());

            foreach (AnnalEntry entry in newEntries)
                projectedEntries.Add(entry);
            restoreInProgress = true;
            ready = false;
            failureReason = string.Empty;
            return SettlementEventRestorePlan.Success(events);
        }

        public bool Complete(bool succeeded)
        {
            restoreInProgress = false;
            if (!succeeded)
            {
                ready = false;
                failureReason = "未完成营地事件恢复失败，已保持出猎门禁。";
                RemoveUncompletedProjection();
                return false;
            }

            foreach (AnnalEntry entry in projectedEntries)
                if (entry != null && !entry.IsCompleted)
                {
                    ready = false;
                    failureReason = $"营地事件恢复后仍有未完成年鉴条目：{entry.EventId}";
                    return false;
                }

            ready = true;
            failureReason = string.Empty;
            return true;
        }

        public void Fail(string reason)
        {
            restoreInProgress = false;
            ready = false;
            failureReason = string.IsNullOrWhiteSpace(reason) ? "营地事件恢复失败，已保持出猎门禁。" : reason.Trim();
        }

        private SettlementEventRestorePlan Reject(string reason)
        {
            Fail(reason);
            return SettlementEventRestorePlan.Failed(failureReason);
        }

        private void RemoveUncompletedProjection()
        {
            var completedEntries = new List<AnnalEntry>();
            foreach (AnnalEntry entry in projectedEntries)
                if (entry == null || entry.IsCompleted)
                    completedEntries.Add(entry);
            foreach (AnnalEntry entry in completedEntries)
                projectedEntries.Remove(entry);
        }
    }
}
