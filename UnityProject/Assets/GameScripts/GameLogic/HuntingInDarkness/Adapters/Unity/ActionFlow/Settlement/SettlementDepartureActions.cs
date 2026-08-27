using System;
using System.Collections.Generic;
using System.Threading;
using CardGame.ActionQueue;
using Core;
using Cysharp.Threading.Tasks;
using HuntingInDarkness.Data;
using HuntingInDarkness.ActionFlow.Campaign;
using HuntingInDarkness.GameCore.Settlement;

namespace HuntingInDarkness.ActionFlow.Settlement
{
    /// <summary>营地兼容入口使用的出猎请求端口，由战役组合根注入。</summary>
    public interface ISettlementDepartureRequestPort
    {
        bool RequestDeparture(IReadOnlyList<int> hunterIds);
    }

    public interface IPlayableHuntDepartureInput
    {
        void RequestDeparture(IReadOnlyList<int> hunterIds);
    }

    public readonly struct SettlementDepartureCommandResult
    {
        public SettlementDepartureCommandResult(bool succeeded, string reason, IReadOnlyList<int> hunterIds)
        {
            Succeeded = succeeded;
            Reason = reason ?? string.Empty;
            HunterIds = hunterIds ?? Array.Empty<int>();
        }

        public bool Succeeded { get; }
        public string Reason { get; }
        public IReadOnlyList<int> HunterIds { get; }

        public static SettlementDepartureCommandResult Failed(string reason) => new(false, reason, Array.Empty<int>());
    }

    public struct SettlementDeparturePreparedEvent
    {
        public int[] HunterIds;
    }

    /// <summary>在营地 Runner 内验证并提交本次远征名册；阶段切换由 Campaign Runner 在其完成后执行。</summary>
    public sealed class PrepareSettlementDepartureAction : CommandAction, ISourceAction, ITargetAction
    {
        private readonly SettlementInstance settlement;
        private readonly IReadOnlyList<int> requestedHunterIds;
        private readonly ActionEventOutbox eventOutbox;

        public PrepareSettlementDepartureAction(SettlementInstance settlement, IReadOnlyList<int> requestedHunterIds, ActionEventOutbox eventOutbox, IReactorEntity source, IReactorEntity target)
        {
            this.settlement = settlement ?? throw new ArgumentNullException(nameof(settlement));
            this.requestedHunterIds = requestedHunterIds;
            this.eventOutbox = eventOutbox ?? throw new ArgumentNullException(nameof(eventOutbox));
            Source = source ?? throw new ArgumentNullException(nameof(source));
            Target = target ?? throw new ArgumentNullException(nameof(target));
        }

        public SettlementDepartureCommandResult Result { get; private set; }
        public IReactorEntity Source { get; }
        public IReactorEntity Target { get; }

        protected override UniTask<ActionOutcome> ExecuteAsync(ActionExecutionContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!DepartureRules.CanDepart(requestedHunterIds, out string reason))
                return Fail(reason);
            if (settlement.HasDueFacilityDuty(settlement.CurrentYear, settlement.CurrentSeasonIndex))
                return Fail("存在已到期的设施值守，结算后才能出猎。");

            var committedIds = new List<int>(requestedHunterIds.Count);
            var uniqueIds = new HashSet<int>();
            foreach (int hunterId in requestedHunterIds)
            {
                if (!uniqueIds.Add(hunterId))
                    return Fail("出发小队中存在重复猎人。");
                HunterInstance hunter = settlement.GetHunter(hunterId);
                if (hunter == null || !settlement.CanHunterDepart(hunterId, settlement.CurrentYear, settlement.CurrentSeasonIndex))
                    return Fail("小队包含无法出发的猎人。");
                committedIds.Add(hunterId);
            }

            cancellationToken.ThrowIfCancellationRequested();
            PlayableCampaignLoopContract.CommitDepartureRoster(settlement, committedIds);
            int[] snapshot = committedIds.ToArray();
            Result = new SettlementDepartureCommandResult(true, string.Empty, snapshot);
            eventOutbox.StageAfterCommit(new SettlementDeparturePreparedEvent { HunterIds = (int[])snapshot.Clone() });
            return UniTask.FromResult(ActionOutcome.Success());
        }

        private UniTask<ActionOutcome> Fail(string reason)
        {
            Result = SettlementDepartureCommandResult.Failed(reason);
            return UniTask.FromResult(ActionOutcome.Failure(reason));
        }
    }
}
