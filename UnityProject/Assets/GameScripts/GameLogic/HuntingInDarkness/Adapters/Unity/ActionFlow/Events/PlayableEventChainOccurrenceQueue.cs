using System;
using System.Collections.Generic;

namespace HuntingInDarkness.ActionFlow.Events
{
    public readonly struct PlayableEventChainOccurrence
    {
        public PlayableEventChainOccurrence(int sequence, string eventId, string eventName, int year, int actorId, IReadOnlyList<string> ancestorEventIds = null)
        {
            Sequence = sequence;
            EventId = eventId ?? string.Empty;
            EventName = eventName ?? string.Empty;
            Year = year;
            ActorId = actorId;
            AncestorEventIds = ancestorEventIds ?? Array.Empty<string>();
        }

        public int Sequence { get; }
        public string EventId { get; }
        public string EventName { get; }
        public int Year { get; }
        public int ActorId { get; }
        public IReadOnlyList<string> AncestorEventIds { get; }
    }

    public readonly struct PlayableEventChainCommitResult
    {
        public PlayableEventChainCommitResult(IReadOnlyList<PlayableEventChainOccurrence> appendedOccurrences, bool hasPendingOccurrences, string diagnostic)
        {
            AppendedOccurrences = appendedOccurrences ?? Array.Empty<PlayableEventChainOccurrence>();
            HasPendingOccurrences = hasPendingOccurrences;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public IReadOnlyList<PlayableEventChainOccurrence> AppendedOccurrences { get; }
        public bool HasPendingOccurrences { get; }
        public string Diagnostic { get; }
        public bool HasDiagnostic => !string.IsNullOrWhiteSpace(Diagnostic);
    }

    /// <summary>不依赖存档 DTO 的单链 occurrence 顺序、幂等与上限算法。</summary>
    public sealed class PlayableEventChainOccurrenceQueue
    {
        public const int CurrentSchemaVersion = 2;

        private readonly int maxPendingOccurrences;
        private readonly List<int> committedSequences = new();
        private readonly HashSet<int> committedSequenceSet = new();
        private readonly List<PlayableEventChainOccurrence> pendingOccurrences = new();
        private int nextSequence;
        private int nextRootSequence = -1;
        private string diagnostic;

        public PlayableEventChainOccurrenceQueue(int maxPendingOccurrences, int nextSequence = 1, IEnumerable<int> committedSequences = null, IEnumerable<PlayableEventChainOccurrence> pendingOccurrences = null, string diagnostic = null, int nextRootSequence = -1)
        {
            if (maxPendingOccurrences <= 0) throw new ArgumentOutOfRangeException(nameof(maxPendingOccurrences));
            this.maxPendingOccurrences = maxPendingOccurrences;
            this.nextSequence = Math.Max(1, nextSequence);
            this.nextRootSequence = Math.Min(-1, nextRootSequence);
            this.diagnostic = diagnostic ?? string.Empty;
            if (committedSequences != null)
                foreach (int sequence in committedSequences)
                    AddCommittedSequence(sequence);
            if (pendingOccurrences != null)
                foreach (PlayableEventChainOccurrence occurrence in pendingOccurrences)
                {
                    if (occurrence.Sequence == 0 || committedSequenceSet.Contains(occurrence.Sequence) || this.pendingOccurrences.Exists(candidate => candidate.Sequence == occurrence.Sequence)) continue;
                    if (this.pendingOccurrences.Count >= maxPendingOccurrences)
                    {
                        this.diagnostic = $"事件链检查点超过待恢复 occurrence 上限 {maxPendingOccurrences}。";
                        break;
                    }
                    this.pendingOccurrences.Add(occurrence);
                    if (occurrence.Sequence > 0)
                        this.nextSequence = occurrence.Sequence == int.MaxValue || this.nextSequence == int.MaxValue ? int.MaxValue : Math.Max(this.nextSequence, occurrence.Sequence + 1);
                    else
                        this.nextRootSequence = occurrence.Sequence == int.MinValue ? int.MinValue : Math.Min(this.nextRootSequence, occurrence.Sequence - 1);
                }
        }

        public int NextSequence => nextSequence;
        public int NextRootSequence => nextRootSequence;
        public IReadOnlyList<int> CommittedSequences => committedSequences;
        public IReadOnlyList<PlayableEventChainOccurrence> PendingOccurrences => pendingOccurrences;
        public string Diagnostic => diagnostic;
        public bool HasPendingOccurrences => pendingOccurrences.Count > 0;

        /// <summary>为当前 Action 根分配受限的负序号；正序号只保留给 commit 产生的 child occurrence。</summary>
        public bool TryScheduleRoot(string eventId, string eventName, int year, int actorId, out PlayableEventChainOccurrence occurrence)
        {
            occurrence = default;
            if (nextRootSequence == int.MinValue)
            {
                diagnostic = "事件链检查点根 occurrence 序号已耗尽。";
                return false;
            }
            int sequence = nextRootSequence;
            if (!TrySchedule(sequence, eventId, eventName, year, actorId, out occurrence)) return false;
            nextRootSequence--;
            return true;
        }

        public bool TrySchedule(int sequence, string eventId, string eventName, int year, int actorId, out PlayableEventChainOccurrence occurrence)
        {
            occurrence = default;
            string normalizedEventId = eventId?.Trim() ?? string.Empty;
            if (sequence == 0 || normalizedEventId.Length == 0 || committedSequenceSet.Contains(sequence) || pendingOccurrences.Exists(candidate => candidate.Sequence == sequence)) return false;
            if (pendingOccurrences.Count >= maxPendingOccurrences)
            {
                diagnostic = $"事件链检查点超过待恢复 occurrence 上限 {maxPendingOccurrences}。";
                return false;
            }
            occurrence = new PlayableEventChainOccurrence(sequence, normalizedEventId, eventName, year, actorId);
            pendingOccurrences.Add(occurrence);
            return true;
        }

        public PlayableEventChainCommitResult Commit(int completedSequence, IReadOnlyList<string> childEventIds, int year, int actorId, IReadOnlyList<string> ancestorEventIds = null)
        {
            bool hasChildren = childEventIds != null && childEventIds.Count > 0;
            if (committedSequenceSet.Contains(completedSequence))
                return CreateResult(Array.Empty<PlayableEventChainOccurrence>());

            AddCommittedSequence(completedSequence);
            pendingOccurrences.RemoveAll(occurrence => occurrence.Sequence == completedSequence);
            var appendedOccurrences = new List<PlayableEventChainOccurrence>();
            if (hasChildren)
                foreach (string childEventId in childEventIds)
                {
                    string normalizedEventId = childEventId?.Trim() ?? string.Empty;
                    if (normalizedEventId.Length == 0) continue;
                    if (pendingOccurrences.Count >= maxPendingOccurrences)
                    {
                        diagnostic = $"事件链检查点超过待恢复 occurrence 上限 {maxPendingOccurrences}。";
                        break;
                    }
                    if (nextSequence <= 0 || nextSequence == int.MaxValue)
                    {
                        diagnostic = "事件链检查点 occurrence 序号已耗尽。";
                        break;
                    }
                    var occurrence = new PlayableEventChainOccurrence(nextSequence++, normalizedEventId, normalizedEventId, year, actorId, ancestorEventIds);
                    pendingOccurrences.Add(occurrence);
                    appendedOccurrences.Add(occurrence);
                }
            return CreateResult(appendedOccurrences);
        }

        private PlayableEventChainCommitResult CreateResult(IReadOnlyList<PlayableEventChainOccurrence> appendedOccurrences)
        {
            return new PlayableEventChainCommitResult(appendedOccurrences, HasPendingOccurrences, diagnostic);
        }

        private void AddCommittedSequence(int sequence)
        {
            if (!committedSequenceSet.Add(sequence)) return;
            committedSequences.Add(sequence);
        }
    }
}
