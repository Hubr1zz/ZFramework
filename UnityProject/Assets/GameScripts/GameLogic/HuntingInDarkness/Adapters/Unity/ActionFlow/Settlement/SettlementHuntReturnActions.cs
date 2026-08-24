using System;
using System.Collections.Generic;
using System.Threading;
using CardGame.ActionQueue;
using Core;
using Cysharp.Threading.Tasks;
using HuntingInDarkness.Data;
using HuntingInDarkness.Settlement;

namespace HuntingInDarkness.ActionFlow.Settlement
{
    public readonly struct SettlementHuntReturnCommandResult
    {
        public SettlementHuntReturnCommandResult(bool succeeded, bool applied, string reason, IReadOnlyList<EventData> events)
        {
            Succeeded = succeeded;
            Applied = applied;
            Reason = reason ?? string.Empty;
            Events = events ?? Array.Empty<EventData>();
        }

        public bool Succeeded { get; }
        public bool Applied { get; }
        public string Reason { get; }
        public IReadOnlyList<EventData> Events { get; }

        public static SettlementHuntReturnCommandResult Failed(string reason) => new(false, false, reason, Array.Empty<EventData>());
    }

    /// <summary>在 Settlement Runner 的单个 root 内提交远征记录、年份和年度 Timeline。</summary>
    public sealed class ApplySettlementHuntReturnAction : CommandAction, ISourceAction, ITargetAction
    {
        private readonly TimelineSystem timeline;
        private readonly HuntRecord huntRecord;
        private readonly ActionEventOutbox eventOutbox;

        public ApplySettlementHuntReturnAction(TimelineSystem timeline, HuntRecord huntRecord, ActionEventOutbox eventOutbox, IReactorEntity source, IReactorEntity target)
        {
            this.timeline = timeline ?? throw new ArgumentNullException(nameof(timeline));
            this.huntRecord = huntRecord ?? throw new ArgumentNullException(nameof(huntRecord));
            this.eventOutbox = eventOutbox ?? throw new ArgumentNullException(nameof(eventOutbox));
            Source = source ?? throw new ArgumentNullException(nameof(source));
            Target = target ?? throw new ArgumentNullException(nameof(target));
        }

        public SettlementHuntReturnCommandResult Result { get; private set; }
        public IReactorEntity Source { get; }
        public IReactorEntity Target { get; }

        protected override UniTask<ActionOutcome> ExecuteAsync(ActionExecutionContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(huntRecord.RecordId))
            {
                Result = SettlementHuntReturnCommandResult.Failed("远征归来记录缺少稳定 ID。");
                return UniTask.FromResult(ActionOutcome.Failure(Result.Reason));
            }
            bool applied = !timeline.HasAppliedHuntRecord(huntRecord);
            if (applied && huntRecord.Year != timeline.CurrentYear)
            {
                Result = SettlementHuntReturnCommandResult.Failed($"远征归来年份 {huntRecord.Year} 与营地当前年份 {timeline.CurrentYear} 不一致。");
                return UniTask.FromResult(ActionOutcome.Failure(Result.Reason));
            }
            IReadOnlyList<EventData> events = timeline.AdvanceYear(huntRecord);
            Result = new SettlementHuntReturnCommandResult(true, applied, string.Empty, events);
            if (applied)
            {
                eventOutbox.StageAfterCommit(new HuntCompletedEvent
                {
                    CompletedYear = huntRecord.Year,
                    TotalHunts = timeline.TotalHunts,
                    HuntersDeployed = huntRecord.HuntersDeployed,
                    HuntersLost = huntRecord.HuntersLost,
                    CollectedResourceCount = huntRecord.CollectedResources?.Count ?? 0,
                    BossDefeated = huntRecord.BossDefeated,
                    AdvancedToYear = timeline.CurrentYear
                });
                eventOutbox.StageAfterCommit(new YearAdvancedEvent { NewYear = timeline.CurrentYear });
            }
            return UniTask.FromResult(ActionOutcome.Success());
        }
    }
}
