using System;
using CardGame.ActionQueue;
using Cysharp.Threading.Tasks;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Settlement;
using HuntingInDarkness.Settlement;

namespace HuntingInDarkness.ActionFlow.Settlement
{
    /// <summary>营地阶段的 Action 执行环境；持久营地数据由 SettlementInstance 继续拥有。</summary>
    public sealed class PlayableSettlementActionSession : IDisposable
    {
        private readonly SettlementInstance settlement;
        private readonly IWeaponTrainingContent weaponTrainingContent;
        private readonly ActionEnvironment environment;

        public PlayableSettlementActionSession(SettlementInstance settlement, IWeaponTrainingContent weaponTrainingContent)
        {
            this.settlement = settlement ?? throw new ArgumentNullException(nameof(settlement));
            this.weaponTrainingContent = weaponTrainingContent ?? throw new ArgumentNullException(nameof(weaponTrainingContent));
            environment = new ActionEnvironment(new ActionEnvironmentConfiguration
            {
                Name = "Settlement",
                Kind = ActionEnvironmentKind.Settlement,
                MaxActionsPerChain = 128,
                TraceCapacity = 24
            });
        }

        public bool IsActive => !environment.IsDisposed;
        public ReactorRegistry Reactors => environment.Reactors;
        public ReactionGateRegistry ReactionGates => environment.ReactionGates;

        public bool CanTrainWeapon(int hunterId, string masteryId, out string reason)
        {
            HunterInstance hunter = settlement.GetHunter(hunterId);
            if (hunter == null || !weaponTrainingContent.TryGetFamily(masteryId, out _))
            {
                reason = "训练内容尚未配置";
                return false;
            }
            if (!WeaponMasteryRules.CanIncrease(hunter, masteryId))
            {
                reason = "熟练度已达到上限";
                return false;
            }
            return WeaponTrainingRules.CanTrain(hunter.IsAvailable && !hunter.IsDead, settlement.IsInventionUnlocked(weaponTrainingContent.RequiredInventionId), settlement.GetResource(weaponTrainingContent.CostResourceId), weaponTrainingContent.ResourceCost, masteryId, weaponTrainingContent.Experience, out reason);
        }

        public async UniTask<WeaponTrainingCommandResult> TrainWeaponAsync(int hunterId, string masteryId)
        {
            if (!IsActive) return WeaponTrainingCommandResult.Failed("当前不在营地阶段");
            HunterInstance hunter = settlement.GetHunter(hunterId);
            if (hunter == null || !weaponTrainingContent.TryGetFamily(masteryId, out WeaponMasteryFamilyDefinition family)) return WeaponTrainingCommandResult.Failed("训练内容尚未配置");

            var outbox = new ActionEventOutbox();
            ReactorEntityHandle settlementEntity = environment.EntityHandles.GetOrCreate("settlement", "active", "营地");
            ReactorEntityHandle hunterEntity = environment.EntityHandles.GetOrCreate("hunter", hunter.InstanceId.ToString(), hunter.Name);
            var action = new TrainWeaponAction(settlement, hunter, family, weaponTrainingContent.RequiredInventionId, weaponTrainingContent.CostResourceId, weaponTrainingContent.ResourceCost, weaponTrainingContent.Experience, outbox, settlementEntity, hunterEntity);
            ActionOutcome outcome = await environment.ExecuteAsync(action, outbox);
            if (outcome.IsSuccess) return action.Result;
            return string.IsNullOrWhiteSpace(action.Result.Reason) ? WeaponTrainingCommandResult.Failed(outcome.Reason) : action.Result;
        }

        public void Dispose() => environment.Dispose();
    }
}
