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

        public PlayableEventChainOccurrence Occurrence { get; }
        public EventData Event { get; }
        public Vector2Int Coordinate { get; }
        public IReadOnlyList<string> AncestorContentIds { get; }
        public string AncestorContentId => AncestorContentIds.Count == 0 ? string.Empty : AncestorContentIds[AncestorContentIds.Count - 1];
        public int Sequence => Occurrence.Sequence;
        public string EventId => Event?.ContentId ?? Occurrence.EventId;
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

        public PlayableHuntEventOccurrenceStore()
        {
            queue = new PlayableEventChainOccurrenceQueue(MaxPendingOccurrences);
        }

        private PlayableHuntEventOccurrenceStore(PlayableEventChainOccurrenceQueue queue)
        {
            this.queue = queue ?? throw new ArgumentNullException(nameof(queue));
        }

        public bool HasPendingOccurrences => queue.HasPendingOccurrences;
        public IReadOnlyList<PlayableEventChainOccurrence> PendingSequences => queue.PendingOccurrences;
        public string Diagnostic => queue.Diagnostic;
        public bool ContainsPendingSequence(int sequence)
        {
            foreach (PlayableEventChainOccurrence occurrence in queue.PendingOccurrences)
                if (occurrence.Sequence == sequence) return true;
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
                Diagnostic = queue.Diagnostic
            };
        }

        public static bool TryRestore(PlayableHuntEventOccurrenceStoreState state, Func<string, EventData> resolveEvent, out PlayableHuntEventOccurrenceStore store, out string reason)
        {
            store = null;
            if (state == null || resolveEvent == null)
            {
                reason = "狩猎事件检查点或内容解析器为空。";
                return false;
            }
            var pendingQueue = new List<PlayableEventChainOccurrence>();
            var resolved = new List<(PlayableHuntEventOccurrenceRecord Record, EventData Event)>();
            foreach (PlayableHuntEventOccurrenceRecord record in state.PendingOccurrences ?? Array.Empty<PlayableHuntEventOccurrenceRecord>())
            {
                EventData gameEvent = resolveEvent(record.Occurrence.EventId);
                if (gameEvent == null || !string.Equals(gameEvent.ContentId, record.Occurrence.EventId, StringComparison.Ordinal))
                {
                    reason = $"无法解析待恢复狩猎事件：{record.Occurrence.EventId}";
                    return false;
                }
                pendingQueue.Add(record.Occurrence);
                resolved.Add((record, gameEvent));
            }
            var queue = new PlayableEventChainOccurrenceQueue(MaxPendingOccurrences, state.NextSequence, state.CommittedSequences, pendingQueue, state.Diagnostic, state.NextRootSequence);
            store = new PlayableHuntEventOccurrenceStore(queue);
            foreach ((PlayableHuntEventOccurrenceRecord record, EventData gameEvent) in resolved)
                store.AddOccurrence(record.Occurrence, gameEvent, record.Coordinate, new List<string>(record.AncestorContentIds ?? Array.Empty<string>()));
            reason = string.Empty;
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
