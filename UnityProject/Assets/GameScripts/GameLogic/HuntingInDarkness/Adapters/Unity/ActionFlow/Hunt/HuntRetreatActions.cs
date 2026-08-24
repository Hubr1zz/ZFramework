using System;
using System.Collections.Generic;
using System.Threading;
using CardGame.ActionQueue;
using Core;
using Cysharp.Threading.Tasks;
using HuntingInDarkness.Data;
using HuntingInDarkness.Hunt;

namespace HuntingInDarkness.ActionFlow.Hunt
{
    public interface IPlayableHuntRetreatInput
    {
        bool IsReturnCheckpointLocked { get; }
        UniTask<HuntRetreatCommandResult> RequestRetreatAsync();
    }

    public readonly struct HuntRetreatCommandResult
    {
        private HuntRetreatCommandResult(bool succeeded, string reason, HuntRecord record)
        {
            Succeeded = succeeded;
            Reason = reason ?? string.Empty;
            Record = record;
        }

        public bool Succeeded { get; }
        public string Reason { get; }
        public HuntRecord Record { get; }

        public static HuntRetreatCommandResult Success(HuntRecord record) => new(true, string.Empty, record);
        public static HuntRetreatCommandResult Failed(string reason) => new(false, reason, null);
    }

    public struct HuntRetreatPreparedEvent
    {
        public int Year;
        public int HuntersDeployed;
        public int HuntersLost;
        public string[] CollectedResources;
    }

    /// <summary>在 Hunt Runner 内生成不可变更权威状态的回营快照；资源转移只在 Campaign 接受阶段切换后执行。</summary>
    public sealed class PrepareHuntRetreatAction : CommandAction, ISourceAction, ITargetAction
    {
        private readonly HuntManager manager;
        private readonly int currentYear;
        private readonly ActionEventOutbox eventOutbox;

        public PrepareHuntRetreatAction(HuntManager manager, int currentYear, ActionEventOutbox eventOutbox, IReactorEntity source, IReactorEntity target)
        {
            this.manager = manager ?? throw new ArgumentNullException(nameof(manager));
            this.currentYear = currentYear;
            this.eventOutbox = eventOutbox ?? throw new ArgumentNullException(nameof(eventOutbox));
            Source = source ?? throw new ArgumentNullException(nameof(source));
            Target = target ?? throw new ArgumentNullException(nameof(target));
        }

        public HuntRetreatCommandResult Result { get; private set; }
        public IReactorEntity Source { get; }
        public IReactorEntity Target { get; }

        protected override UniTask<ActionOutcome> ExecuteAsync(ActionExecutionContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (currentYear < 1)
                return Fail("营地年份无效，无法结算本次狩猎。");

            HuntRecord record = manager.CreateHuntRecord(false, currentYear);
            HuntRecord resultRecord = CloneRecord(record);
            Result = HuntRetreatCommandResult.Success(resultRecord);
            eventOutbox.StageAfterCommit(new HuntRetreatPreparedEvent
            {
                Year = record.Year,
                HuntersDeployed = record.HuntersDeployed,
                HuntersLost = record.HuntersLost,
                CollectedResources = record.CollectedResources.ToArray()
            });
            return UniTask.FromResult(ActionOutcome.Success());
        }

        private UniTask<ActionOutcome> Fail(string reason)
        {
            Result = HuntRetreatCommandResult.Failed(reason);
            return UniTask.FromResult(ActionOutcome.Failure(reason));
        }

        private static HuntRecord CloneRecord(HuntRecord source)
        {
            return new HuntRecord
            {
                RecordId = source.RecordId,
                ReturnSchemaVersion = source.ReturnSchemaVersion,
                Year = source.Year,
                HuntersDeployed = source.HuntersDeployed,
                HuntersLost = source.HuntersLost,
                BossDefeated = source.BossDefeated,
                ParticipantHunterIds = source.ParticipantHunterIds != null ? new List<int>(source.ParticipantHunterIds) : new List<int>(),
                CollectedResources = source.CollectedResources != null ? new List<string>(source.CollectedResources) : new List<string>()
            };
        }
    }
}
