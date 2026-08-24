using System;
using System.Collections.Generic;
using HuntingInDarkness.Data;

namespace HuntingInDarkness.Settlement
{
    /// <summary>读档后把未完成年鉴事件投影到营地 ActionQueue 的一次性计划。</summary>
    public readonly struct SettlementEventRestorePlan
    {
        private SettlementEventRestorePlan(bool succeeded, IReadOnlyList<EventData> events, IReadOnlyList<SettlementEventWork> workItems, string failureReason, bool alreadyInProgress, string chainId, IReadOnlyList<SettlementEventChainOccurrence> occurrences)
        {
            Succeeded = succeeded;
            Events = events ?? Array.Empty<EventData>();
            WorkItems = workItems ?? Array.Empty<SettlementEventWork>();
            FailureReason = failureReason ?? string.Empty;
            AlreadyInProgress = alreadyInProgress;
            ChainId = chainId ?? string.Empty;
            Occurrences = occurrences ?? Array.Empty<SettlementEventChainOccurrence>();
        }

        public bool Succeeded { get; }
        public IReadOnlyList<EventData> Events { get; }
        public IReadOnlyList<SettlementEventWork> WorkItems { get; }
        public string FailureReason { get; }
        public bool AlreadyInProgress { get; }
        public string ChainId { get; }
        public IReadOnlyList<SettlementEventChainOccurrence> Occurrences { get; }
        public bool HasPendingEvents => Events.Count > 0;

        public static SettlementEventRestorePlan Success(IReadOnlyList<EventData> events) => new(true, events, ToWorkItems(events), string.Empty, false, string.Empty, Array.Empty<SettlementEventChainOccurrence>());
        public static SettlementEventRestorePlan Success(IReadOnlyList<EventData> events, string chainId, IReadOnlyList<SettlementEventChainOccurrence> occurrences) => new(true, events, ToWorkItems(events, occurrences), string.Empty, false, chainId, occurrences);
        public static SettlementEventRestorePlan Success(IReadOnlyList<SettlementEventWork> workItems) => new(true, ToEvents(workItems), workItems, string.Empty, false, string.Empty, Array.Empty<SettlementEventChainOccurrence>());
        public static SettlementEventRestorePlan Success(IReadOnlyList<SettlementEventWork> workItems, string chainId) => new(true, ToEvents(workItems), workItems, string.Empty, false, chainId, ToOccurrences(workItems));
        public static SettlementEventRestorePlan InProgress() => new(true, Array.Empty<EventData>(), Array.Empty<SettlementEventWork>(), string.Empty, true, string.Empty, Array.Empty<SettlementEventChainOccurrence>());
        public static SettlementEventRestorePlan Failed(string reason) => new(false, Array.Empty<EventData>(), Array.Empty<SettlementEventWork>(), reason, false, string.Empty, Array.Empty<SettlementEventChainOccurrence>());

        private static IReadOnlyList<SettlementEventWork> ToWorkItems(IReadOnlyList<EventData> events, IReadOnlyList<SettlementEventChainOccurrence> occurrences = null)
        {
            if (events == null) return Array.Empty<SettlementEventWork>();
            var works = new List<SettlementEventWork>(events.Count);
            bool hasOccurrences = occurrences != null && occurrences.Count == events.Count;
            for (int index = 0; index < events.Count; index++)
                works.Add(new SettlementEventWork(events[index], null, hasOccurrences ? occurrences[index] : null));
            return works;
        }

        private static IReadOnlyList<EventData> ToEvents(IReadOnlyList<SettlementEventWork> workItems)
        {
            if (workItems == null) return Array.Empty<EventData>();
            var events = new List<EventData>(workItems.Count);
            foreach (SettlementEventWork work in workItems) events.Add(work.Event);
            return events;
        }

        private static IReadOnlyList<SettlementEventChainOccurrence> ToOccurrences(IReadOnlyList<SettlementEventWork> workItems)
        {
            var occurrences = new List<SettlementEventChainOccurrence>();
            if (workItems == null) return occurrences;
            foreach (SettlementEventWork work in workItems)
                if (work.RestoredOccurrence != null) occurrences.Add(work.RestoredOccurrence);
            return occurrences;
        }
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
        private bool retryableFailure;
        private string failureReason = string.Empty;

        public SettlementEventRestoreProjection(SettlementInstance settlement, Func<string, EventData> resolveEvent)
        {
            Bind(settlement, resolveEvent);
        }

        public bool IsReady => ready;
        public bool HasPendingEvents => !ready && string.IsNullOrEmpty(failureReason);
        public bool HasRecoverableCheckpoint => settlement != null && settlement.HasPendingEventChainOccurrences;
        public string FailureReason => failureReason;

        public void Bind(SettlementInstance nextSettlement, Func<string, EventData> nextResolveEvent)
        {
            settlement = nextSettlement;
            resolveEvent = nextResolveEvent;
            projectedEntries.Clear();
            restoreInProgress = false;
            ready = true;
            retryableFailure = false;
            failureReason = string.Empty;
        }

        public SettlementEventRestorePlan Prepare()
        {
            if (restoreInProgress) return SettlementEventRestorePlan.InProgress();
            if (!ready && !retryableFailure) return SettlementEventRestorePlan.Failed(failureReason);
            if (retryableFailure)
            {
                ready = true;
                retryableFailure = false;
                failureReason = string.Empty;
            }
            if (settlement == null) return Reject("营地存档为空，无法恢复未完成事件。");
            if (resolveEvent == null) return Reject("营地事件内容解析器尚未配置。");

            SettlementEventChainCheckpoint checkpoint = settlement.PendingEventChains?.Find(candidate => candidate != null && candidate.PendingOccurrences != null && candidate.PendingOccurrences.Count > 0);
            if (checkpoint != null)
            {
                if (string.IsNullOrWhiteSpace(checkpoint.ChainId)) return Reject("事件链检查点缺少稳定链 ID。");
                if (!string.IsNullOrWhiteSpace(checkpoint.Diagnostic)) return Reject(checkpoint.Diagnostic);
                var checkpointOccurrences = new List<SettlementEventChainOccurrence>(checkpoint.PendingOccurrences);
                checkpointOccurrences.Sort((left, right) => left.Sequence.CompareTo(right.Sequence));
                var checkpointWorks = new List<SettlementEventWork>(checkpointOccurrences.Count);
                foreach (SettlementEventChainOccurrence occurrence in checkpointOccurrences)
                {
                    if (occurrence == null || string.IsNullOrWhiteSpace(occurrence.EventId)) return Reject("事件链检查点包含缺少事件 ID 的 occurrence。");
                    EventData gameEvent = resolveEvent(occurrence.EventId);
                    if (gameEvent == null) return Reject($"找不到事件链检查点内容：{occurrence.EventId}");
                    checkpointWorks.Add(new SettlementEventWork(gameEvent, null, occurrence));
                }
                restoreInProgress = true;
                ready = false;
                failureReason = string.Empty;
                return SettlementEventRestorePlan.Success(checkpointWorks, checkpoint.ChainId);
            }

            var workItems = new List<SettlementEventWork>();
            var newEntries = new List<AnnalEntry>();
            foreach (AnnalEntry entry in settlement.Timeline ?? new List<AnnalEntry>())
            {
                if (entry == null || entry.IsCompleted || projectedEntries.Contains(entry) || !PlayableSettlementEventRegistry.IsTimelineEventEntry(entry)) continue;
                if (string.IsNullOrWhiteSpace(entry.EventId)) return Reject("未完成年鉴条目缺少事件 ID。");

                EventData gameEvent = resolveEvent(entry.EventId);
                if (gameEvent == null) return Reject($"找不到未完成年鉴事件内容：{entry.EventId}");

                workItems.Add(new SettlementEventWork(gameEvent, entry));
                newEntries.Add(entry);
            }

            if (workItems.Count == 0) return SettlementEventRestorePlan.Success(Array.Empty<SettlementEventWork>());

            foreach (AnnalEntry entry in newEntries)
                projectedEntries.Add(entry);
            restoreInProgress = true;
            ready = false;
            failureReason = string.Empty;
            return SettlementEventRestorePlan.Success(workItems);
        }

        public bool Complete(bool succeeded)
        {
            restoreInProgress = false;
            if (!succeeded)
            {
                if (HasRecoverableCheckpoint)
                {
                    ready = false;
                    retryableFailure = true;
                    failureReason = string.Empty;
                    projectedEntries.Clear();
                    return false;
                }
                ready = false;
                retryableFailure = true;
                failureReason = "未完成营地事件恢复失败，已保持出猎门禁。";
                projectedEntries.Clear();
                return false;
            }

            if (settlement != null && settlement.HasPendingEventChainOccurrences)
            {
                ready = false;
                retryableFailure = true;
                failureReason = string.Empty;
                projectedEntries.Clear();
                return false;
            }

            foreach (AnnalEntry entry in projectedEntries)
                if (entry != null && !entry.IsCompleted)
                {
                    ready = false;
                    retryableFailure = true;
                    failureReason = $"营地事件恢复后仍有未完成年鉴条目：{entry.EventId}";
                    return false;
                }

            ready = true;
            retryableFailure = false;
            failureReason = string.Empty;
            return true;
        }

        public void Fail(string reason)
        {
            restoreInProgress = false;
            ready = false;
            retryableFailure = false;
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
