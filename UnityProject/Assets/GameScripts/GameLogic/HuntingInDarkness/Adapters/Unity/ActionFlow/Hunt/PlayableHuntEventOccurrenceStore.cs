using System;
using System.Collections.Generic;
using HuntingInDarkness.ActionFlow.Events;
using HuntingInDarkness.Data;
using UnityEngine;

namespace HuntingInDarkness.ActionFlow.Hunt
{
    /// <summary>狩猎会话内的事件 occurrence；运行时归 session 所有，可通过活动狩猎快照恢复。</summary>
    public sealed class PlayableHuntEventOccurrence
    {
        internal PlayableHuntEventOccurrence(PlayableEventChainOccurrence occurrence, EventData gameEvent, Vector2Int coordinate, IReadOnlyList<string> ancestorContentIds)
        {
            Occurrence = occurrence;
            Event = gameEvent;
            Coordinate = coordinate;
            AncestorContentIds = ancestorContentIds ?? Array.Empty<string>();
        }

        public PlayableEventChainOccurrence Occurrence { get; private set; }
        public EventData Event { get; }
        public Vector2Int Coordinate { get; }
        public IReadOnlyList<string> AncestorContentIds { get; }
        public string AncestorContentId => AncestorContentIds.Count == 0 ? string.Empty : AncestorContentIds[AncestorContentIds.Count - 1];
        public int Sequence => Occurrence.Sequence;
        public string EventId => Event?.ContentId ?? Occurrence.EventId;
        public PlayableEventRerollCheckpoint RerollCheckpoint => Occurrence.RerollCheckpoint;

        internal void UpdateOccurrence(PlayableEventChainOccurrence occurrence) => Occurrence = occurrence;
    }

    public readonly struct PlayableHuntEventOccurrenceRecord
    {
        public PlayableHuntEventOccurrenceRecord(PlayableEventChainOccurrence occurrence, Vector2Int coordinate, IReadOnlyList<string> ancestorContentIds)
        {
            Occurrence = occurrence;
            Coordinate = coordinate;
            AncestorContentIds = ancestorContentIds ?? Array.Empty<string>();
        }

        public PlayableEventChainOccurrence Occurrence { get; }
        public Vector2Int Coordinate { get; }
        public IReadOnlyList<string> AncestorContentIds { get; }
    }

    public sealed class PlayableHuntEventOccurrenceStoreState
    {
        public int NextSequence { get; set; } = 1;
        public int NextRootSequence { get; set; } = -1;
        public IReadOnlyList<int> CommittedSequences { get; set; } = Array.Empty<int>();
        public IReadOnlyList<PlayableHuntEventOccurrenceRecord> PendingOccurrences { get; set; } = Array.Empty<PlayableHuntEventOccurrenceRecord>();
        public IReadOnlyList<EventResolutionMemory> Memories { get; set; } = Array.Empty<EventResolutionMemory>();
        public string Diagnostic { get; set; } = string.Empty;
    }

    public readonly struct PlayableHuntEventOccurrenceCommitResult
    {
        public PlayableHuntEventOccurrenceCommitResult(bool applied, IReadOnlyList<PlayableHuntEventOccurrence> appendedOccurrences, IReadOnlyList<string> preventedEventIds, int truncatedChildCount, string diagnostic)
        {
            Applied = applied;
            AppendedOccurrences = appendedOccurrences ?? Array.Empty<PlayableHuntEventOccurrence>();
            PreventedEventIds = preventedEventIds ?? Array.Empty<string>();
            TruncatedChildCount = Math.Max(0, truncatedChildCount);
            Diagnostic = diagnostic ?? string.Empty;
        }

        public bool Applied { get; }
        public IReadOnlyList<PlayableHuntEventOccurrence> AppendedOccurrences { get; }
        public IReadOnlyList<string> PreventedEventIds { get; }
        public int TruncatedChildCount { get; }
        public string Diagnostic { get; }
        public bool Succeeded => Applied;
    }

    /// <summary>
    /// Hunt 专属 occurrence store。运行时保留 EventData 引用，持久化时只导出稳定 ContentId、坐标与顺序状态。
    /// </summary>
    public sealed class PlayableHuntEventOccurrenceStore
    {
        private const int MaxPendingOccurrences = 64;
        private readonly PlayableEventChainOccurrenceQueue queue;
        private readonly Dictionary<int, PlayableHuntEventOccurrence> occurrences = new();
        private readonly List<EventResolutionMemory> memories = new();
        public string ExpeditionId { get; }

        public PlayableHuntEventOccurrenceStore(string expeditionId = "")
        {
            queue = new PlayableEventChainOccurrenceQueue(MaxPendingOccurrences);
            ExpeditionId = expeditionId?.Trim() ?? string.Empty;
        }

        private PlayableHuntEventOccurrenceStore(PlayableEventChainOccurrenceQueue queue, string expeditionId)
        {
            this.queue = queue ?? throw new ArgumentNullException(nameof(queue));
            ExpeditionId = expeditionId?.Trim() ?? string.Empty;
        }

        public bool HasPendingOccurrences => queue.HasPendingOccurrences;
        public IReadOnlyList<PlayableEventChainOccurrence> PendingSequences => queue.PendingOccurrences;
        public string Diagnostic => queue.Diagnostic;
        public IReadOnlyList<EventResolutionMemory> Memories => memories;
        public bool ContainsPendingSequence(int sequence)
        {
            foreach (PlayableEventChainOccurrence occurrence in queue.PendingOccurrences)
                if (occurrence.Sequence == sequence) return true;
            return false;
        }

        public bool TrySetRerollCheckpoint(PlayableHuntEventOccurrence occurrence, PlayableEventRerollCheckpoint checkpoint, out string reason)
        {
            if (occurrence == null || checkpoint == null || !occurrences.TryGetValue(occurrence.Sequence, out PlayableHuntEventOccurrence current) || !ReferenceEquals(current, occurrence) || !queue.TrySetRerollCheckpoint(occurrence.Sequence, checkpoint))
            {
                reason = "狩猎事件重投检查点无法绑定当前 occurrence。";
                return false;
            }
            foreach (PlayableEventChainOccurrence pending in queue.PendingOccurrences)
                if (pending.Sequence == occurrence.Sequence)
                {
                    occurrence.UpdateOccurrence(pending);
                    reason = string.Empty;
                    return true;
                }
            reason = "狩猎事件重投检查点写入后 occurrence 丢失。";
            return false;
        }

        public PlayableHuntEventOccurrenceStoreState CaptureState()
        {
            var pending = new List<PlayableHuntEventOccurrenceRecord>();
            foreach (PlayableEventChainOccurrence occurrence in queue.PendingOccurrences)
                if (occurrences.TryGetValue(occurrence.Sequence, out PlayableHuntEventOccurrence runtimeOccurrence))
                    pending.Add(new PlayableHuntEventOccurrenceRecord(occurrence, runtimeOccurrence.Coordinate, runtimeOccurrence.AncestorContentIds));
            return new PlayableHuntEventOccurrenceStoreState
            {
                NextSequence = queue.NextSequence,
                NextRootSequence = queue.NextRootSequence,
                CommittedSequences = new List<int>(queue.CommittedSequences),
                PendingOccurrences = pending,
                Memories = EventResolutionMemoryRules.CloneList(memories),
                Diagnostic = queue.Diagnostic
            };
        }

        public static bool TryRestore(PlayableHuntEventOccurrenceStoreState state, Func<string, EventData> resolveEvent, out PlayableHuntEventOccurrenceStore store, out string reason, string expeditionId = "")
        {
            store = null;
            if (state == null || resolveEvent == null)
            {
                reason = "狩猎事件检查点或内容解析器为空。";
                return false;
            }
            IReadOnlyList<PlayableHuntEventOccurrenceRecord> pendingRecords = state.PendingOccurrences ?? Array.Empty<PlayableHuntEventOccurrenceRecord>();
            if (pendingRecords.Count > MaxPendingOccurrences)
            {
                reason = $"狩猎事件检查点超过待恢复 occurrence 上限 {MaxPendingOccurrences}。";
                return false;
            }
            IReadOnlyList<int> committedSequences = state.CommittedSequences ?? Array.Empty<int>();
            var committedSet = new HashSet<int>();
            int highestObservedPositiveSequence = 0;
            int lowestObservedNegativeSequence = -1;
            bool hasObservedNegativeSequence = false;
            foreach (int sequence in committedSequences)
            {
                if (sequence == 0)
                {
                    reason = "狩猎事件检查点包含无效的 committed occurrence 序号 0。";
                    return false;
                }
                if (!committedSet.Add(sequence))
                {
                    reason = $"狩猎事件检查点包含重复 committed occurrence 序号：{sequence}";
                    return false;
                }
                if (sequence == int.MaxValue || sequence == int.MinValue)
                {
                    reason = $"无法恢复序号达到 {sequence} 的 committed occurrence。";
                    return false;
                }
                if (sequence > 0)
                    highestObservedPositiveSequence = Math.Max(highestObservedPositiveSequence, sequence);
                else
                {
                    hasObservedNegativeSequence = true;
                    lowestObservedNegativeSequence = Math.Min(lowestObservedNegativeSequence, sequence);
                }
            }
            var pendingQueue = new List<PlayableEventChainOccurrence>();
            var resolved = new List<(PlayableHuntEventOccurrenceRecord Record, EventData Event)>();
            var pendingSequenceSet = new HashSet<int>();
            foreach (PlayableHuntEventOccurrenceRecord record in pendingRecords)
            {
                int sequence = record.Occurrence.Sequence;
                if (sequence == 0)
                {
                    reason = "无法恢复序号为 0 的狩猎事件 occurrence。";
                    return false;
                }
                if (!pendingSequenceSet.Add(sequence))
                {
                    reason = $"狩猎事件检查点包含重复 occurrence 序号：{sequence}";
                    return false;
                }
                if (committedSet.Contains(sequence))
                {
                    reason = $"狩猎事件 occurrence 序号同时存在于 pending 与 committed：{sequence}";
                    return false;
                }
                if (sequence == int.MaxValue)
                {
                    reason = "无法恢复序号达到 int.MaxValue 的狩猎事件 occurrence。";
                    return false;
                }
                if (sequence == int.MinValue)
                {
                    reason = "无法恢复序号达到 int.MinValue 的狩猎事件 occurrence。";
                    return false;
                }
                if (sequence > 0)
                    highestObservedPositiveSequence = Math.Max(highestObservedPositiveSequence, sequence);
                else
                {
                    hasObservedNegativeSequence = true;
                    lowestObservedNegativeSequence = Math.Min(lowestObservedNegativeSequence, sequence);
                }
                pendingQueue.Add(record.Occurrence);
            }
            if (state.NextSequence < 1 || state.NextSequence <= highestObservedPositiveSequence)
            {
                reason = $"狩猎事件检查点的 NextSequence 必须大于所有正序号：{state.NextSequence}";
                return false;
            }
            if (state.NextRootSequence > -1 || hasObservedNegativeSequence && state.NextRootSequence >= lowestObservedNegativeSequence)
            {
                reason = $"狩猎事件检查点的 NextRootSequence 必须小于所有负序号：{state.NextRootSequence}";
                return false;
            }
            foreach (PlayableHuntEventOccurrenceRecord record in pendingRecords)
            {
                EventData gameEvent = resolveEvent(record.Occurrence.EventId);
                if (gameEvent == null || !string.Equals(gameEvent.ContentId, record.Occurrence.EventId, StringComparison.Ordinal))
                {
                    reason = $"无法解析待恢复狩猎事件：{record.Occurrence.EventId}";
                    return false;
                }
                resolved.Add((record, gameEvent));
            }
            var queue = new PlayableEventChainOccurrenceQueue(MaxPendingOccurrences, state.NextSequence, committedSequences, pendingQueue, state.Diagnostic, state.NextRootSequence);
            if (queue.PendingOccurrences.Count != pendingRecords.Count)
            {
                reason = "狩猎事件检查点恢复后 occurrence 数量不一致。";
                return false;
            }
            store = new PlayableHuntEventOccurrenceStore(queue, expeditionId);
            if (!EventResolutionMemoryRules.TryValidateHuntList(state.Memories, expeditionId, committedSequences, out reason))
            {
                store = null;
                return false;
            }
            store.memories.AddRange(EventResolutionMemoryRules.CloneList(state.Memories));
            foreach ((PlayableHuntEventOccurrenceRecord record, EventData gameEvent) in resolved)
                store.AddOccurrence(record.Occurrence, gameEvent, record.Coordinate, new List<string>(record.AncestorContentIds ?? Array.Empty<string>()));
            if (store.occurrences.Count != pendingRecords.Count)
            {
                store = null;
                reason = "狩猎事件检查点恢复后 occurrence 身份数量不一致。";
                return false;
            }
            reason = string.Empty;
            return true;
        }

        public bool CanRecordMemory(EventResolutionMemory memory, out string reason)
        {
            reason = string.Empty;
            if (!EventResolutionMemoryRules.TryValidate(memory, out reason)) return false;
            EventResolutionMemory existing = memories.Find(candidate => candidate != null && string.Equals(candidate.MemoryId, memory.MemoryId, StringComparison.Ordinal));
            if (existing == null || EventResolutionMemoryRules.Equivalent(existing, memory)) return true;
            reason = $"狩猎事件结果记忆 {memory.MemoryId} 已存在但事实不一致。";
            return false;
        }

        public bool TryCommitResolution(PlayableHuntEventOccurrence parent, EventResolutionMemory memory, IReadOnlyList<EventData> chainedEvents, int year, int actorId, out PlayableHuntEventOccurrenceCommitResult result)
        {
            result = default;
            if (!CanRecordMemory(memory, out string reason))
            {
                result = new PlayableHuntEventOccurrenceCommitResult(false, Array.Empty<PlayableHuntEventOccurrence>(), Array.Empty<string>(), 0, reason);
                return false;
            }
            if (!memories.Exists(candidate => candidate != null && string.Equals(candidate.MemoryId, memory.MemoryId, StringComparison.Ordinal)) && memories.Count >= EventResolutionMemoryRules.MaximumMemories)
            {
                result = new PlayableHuntEventOccurrenceCommitResult(false, Array.Empty<PlayableHuntEventOccurrence>(), Array.Empty<string>(), 0, $"狩猎事件结果记忆数量超过上限 {EventResolutionMemoryRules.MaximumMemories}。");
                return false;
            }
            result = Commit(parent, chainedEvents, year, actorId);
            if (!result.Succeeded) return false;
            if (!memories.Exists(candidate => candidate != null && string.Equals(candidate.MemoryId, memory.MemoryId, StringComparison.Ordinal))) memories.Add(EventResolutionMemoryRules.Clone(memory));
            return true;
        }

        public bool TryScheduleRoot(EventData gameEvent, Vector2Int coordinate, int year, int actorId, out PlayableHuntEventOccurrence occurrence)
        {
            occurrence = null;
            if (!IsValidEvent(gameEvent)) return false;
            if (!queue.TryScheduleRoot(gameEvent.ContentId, gameEvent.eventName, year, actorId, out PlayableEventChainOccurrence scheduled)) return false;
            occurrence = AddOccurrence(scheduled, gameEvent, coordinate, Array.Empty<string>());
            return true;
        }

        public bool TryGetNextPending(out PlayableHuntEventOccurrence occurrence)
        {
            foreach (PlayableEventChainOccurrence pending in queue.PendingOccurrences)
                if (occurrences.TryGetValue(pending.Sequence, out occurrence))
                    return true;
            occurrence = null;
            return false;
        }

        public PlayableHuntEventOccurrenceCommitResult Commit(PlayableHuntEventOccurrence parent, IReadOnlyList<EventData> chainedEvents, int year, int actorId)
        {
            if (parent == null || !occurrences.ContainsKey(parent.Sequence)) return new PlayableHuntEventOccurrenceCommitResult(false, Array.Empty<PlayableHuntEventOccurrence>(), Array.Empty<string>(), 0, "狩猎事件 occurrence 不存在或已经提交。");

            var childCandidates = new List<EventData>();
            var preventedEventIds = new List<string>();
            if (chainedEvents != null)
                foreach (EventData child in chainedEvents)
                {
                    if (!IsValidEvent(child)) continue;
                    if (ContainsAncestor(parent, child.ContentId))
                    {
                        preventedEventIds.Add(child.ContentId);
                        continue;
                    }
                    childCandidates.Add(child);
                }

            var childIds = new List<string>(childCandidates.Count);
            foreach (EventData child in childCandidates)
                childIds.Add(child.ContentId);
            string previousDiagnostic = queue.Diagnostic;
            PlayableEventChainCommitResult committed = queue.Commit(parent.Sequence, childIds, year, actorId);
            occurrences.Remove(parent.Sequence);
            var appended = new List<PlayableHuntEventOccurrence>(committed.AppendedOccurrences.Count);
            for (int index = 0; index < committed.AppendedOccurrences.Count && index < childCandidates.Count; index++)
            {
                EventData child = childCandidates[index];
                var ancestors = new List<string>(parent.AncestorContentIds.Count + 1);
                ancestors.AddRange(parent.AncestorContentIds);
                ancestors.Add(parent.EventId);
                appended.Add(AddOccurrence(committed.AppendedOccurrences[index], child, parent.Coordinate, ancestors));
            }
            int truncatedChildCount = Math.Max(0, childCandidates.Count - committed.AppendedOccurrences.Count);
            string newDiagnostic = string.Equals(previousDiagnostic, committed.Diagnostic, StringComparison.Ordinal) ? string.Empty : committed.Diagnostic;
            return new PlayableHuntEventOccurrenceCommitResult(true, appended, preventedEventIds, truncatedChildCount, newDiagnostic);
        }

        private PlayableHuntEventOccurrence AddOccurrence(PlayableEventChainOccurrence occurrence, EventData gameEvent, Vector2Int coordinate, IReadOnlyList<string> ancestors)
        {
            var result = new PlayableHuntEventOccurrence(occurrence, gameEvent, coordinate, ancestors);
            occurrences[occurrence.Sequence] = result;
            return result;
        }

        private static bool IsValidEvent(EventData gameEvent) => gameEvent != null && !string.IsNullOrWhiteSpace(gameEvent.ContentId);

        private static bool ContainsAncestor(PlayableHuntEventOccurrence parent, string eventId)
        {
            if (string.Equals(parent.EventId, eventId, StringComparison.Ordinal)) return true;
            foreach (string ancestor in parent.AncestorContentIds)
                if (string.Equals(ancestor, eventId, StringComparison.Ordinal)) return true;
            return false;
        }

    }
}
