using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameplayBase;
using HuntingInDarkness.Data;
using HuntingInDarkness.Settlement;

namespace Core
{
    internal readonly struct DeveloperCommandResult
    {
        private DeveloperCommandResult(bool succeeded, string message)
        {
            Succeeded = succeeded;
            Message = message ?? string.Empty;
        }

        internal bool Succeeded { get; }
        internal string Message { get; }

        internal static DeveloperCommandResult Success(string message) => new(true, message);
        internal static DeveloperCommandResult Failed(string message) => new(false, message);
    }

    internal interface ICampaignDeveloperCommands
    {
        DeveloperCommandResult AddResource(string resourceId, int amount);
        DeveloperCommandResult UnlockWorkshop(string workshopId);
        DeveloperCommandResult AddBlankHunters(int amount);
        DeveloperCommandResult TriggerSettlementEvent(string eventId);
        void Transition(GamePhase phase);
        void SignalBossDefeated();
        void Save();
        void Load();
        void DeleteSave();
    }

    /// <summary>GM 面板的唯一命令门面。参数校验留在边界，状态修改委托给当前战役运行态。</summary>
    internal sealed class CampaignDeveloperCommands : ICampaignDeveloperCommands
    {
        internal const int MaximumBatchAmount = 10000;
        internal const int MaximumHunterBatchAmount = 100;

        private readonly CampaignFlowCoordinator flow;
        private readonly Func<CancellationToken> lifetimeToken;
        private readonly Action<string> info;
        private readonly Action<string> warning;

        internal CampaignDeveloperCommands(CampaignFlowCoordinator flow, Func<CancellationToken> lifetimeToken, Action<string> info, Action<string> warning)
        {
            this.flow = flow ?? throw new ArgumentNullException(nameof(flow));
            this.lifetimeToken = lifetimeToken ?? throw new ArgumentNullException(nameof(lifetimeToken));
            this.info = info;
            this.warning = warning;
        }

        public DeveloperCommandResult AddResource(string resourceId, int amount)
        {
            if (!TryValidateSettlement(out DeveloperCommandResult failure)) return failure;
            if (!TryValidateAmount(amount, out failure)) return failure;
            if (!PlayableSettlementItemRegistry.TryGet(resourceId?.Trim(), out ItemData item) || item == null || item.itemType != ItemType.Resource) return Fail($"资源 ID 不存在或不是资源类型：{Normalize(resourceId)}");
            int currentAmount = flow.SettlementData.GetResource(item.ContentId);
            if (currentAmount > int.MaxValue - amount) return Fail($"资源 {item.ContentId} 的数量将溢出。");
            if (!flow.DevAddResource(item.ContentId, amount)) return Fail("资源生成失败，营地运行态可能已经切换。");
            return Succeed($"已生成资源 {item.itemName} ({item.ContentId}) ×{amount}");
        }

        public DeveloperCommandResult UnlockWorkshop(string workshopId)
        {
            if (!TryValidateSettlement(out DeveloperCommandResult failure)) return failure;
            string normalizedId = Normalize(workshopId);
            if (!flow.TryResolveWorkshop(normalizedId, out PlayableWorkshopDefinition definition)) return Fail($"工坊 ID 不存在：{normalizedId}");
            if (!flow.DevUnlockWorkshop(definition.WorkshopId, out bool changed)) return Fail("工坊生成失败，营地运行态可能已经切换。");
            return Succeed(changed ? $"已解锁并生成工坊 {definition.DisplayName} ({definition.WorkshopId})" : $"工坊已存在：{definition.DisplayName} ({definition.WorkshopId})");
        }

        public DeveloperCommandResult AddBlankHunters(int amount)
        {
            if (!TryValidateSettlement(out DeveloperCommandResult failure)) return failure;
            if (!TryValidateAmount(amount, MaximumHunterBatchAmount, out failure)) return failure;
            int created = flow.DevAddBlankHunters(amount);
            if (created == amount) return Succeed($"已增加空白猎人并生成实体卡 ×{created}");
            if (created > 0) return Fail($"仅成功生成 {created}/{amount} 名空白猎人，请查看日志确认内容配置。");
            return Fail("空白猎人生成失败，营地运行态可能已经切换或血脉配置无效。");
        }

        public DeveloperCommandResult TriggerSettlementEvent(string eventId)
        {
            if (!TryValidateSettlement(out DeveloperCommandResult failure)) return failure;
            string normalizedId = Normalize(eventId);
            if (!PlayableSettlementEventRegistry.TryResolveCanonical(normalizedId, out EventData gameEvent) || gameEvent == null) return Fail($"营地事件 ID 不存在：{normalizedId}");
            if (!flow.DevTriggerSettlementEvent(gameEvent)) return Fail("事件未能启动：当前可能已有事件链运行，或营地运行态已经切换。");
            return Succeed($"已触发营地事件 {gameEvent.eventName} ({gameEvent.ContentId})");
        }

        public void Transition(GamePhase phase) => flow.TransitionToPhase(phase);
        public void SignalBossDefeated() => flow.HandleBossDefeated();

        public void Save()
        {
            if (flow.SettlementData == null)
            {
                warning?.Invoke("DevSave: 无数据可保存。");
                return;
            }
            flow.SaveCampaignAsync(flow.CurrentPhase == GamePhase.Hunt, lifetimeToken()).Forget();
        }

        public void Load() => flow.LoadSnapshotFromPersistenceAsync();
        public void DeleteSave() => flow.DeleteSaveAsync(lifetimeToken()).Forget();

        private bool TryValidateSettlement(out DeveloperCommandResult failure)
        {
            if (flow.IsSettlementDeveloperCommandAvailable)
            {
                failure = default;
                return true;
            }
            failure = Fail("GM 营地指令只能在已启动且空闲的营地阶段使用。");
            return false;
        }

        private bool TryValidateAmount(int amount, out DeveloperCommandResult failure)
        {
            return TryValidateAmount(amount, MaximumBatchAmount, out failure);
        }

        private bool TryValidateAmount(int amount, int maximum, out DeveloperCommandResult failure)
        {
            if (amount > 0 && amount <= maximum)
            {
                failure = default;
                return true;
            }
            failure = Fail($"数量必须在 1 到 {maximum} 之间。");
            return false;
        }

        private DeveloperCommandResult Succeed(string message)
        {
            info?.Invoke(message);
            return DeveloperCommandResult.Success(message);
        }

        private DeveloperCommandResult Fail(string message)
        {
            warning?.Invoke(message);
            return DeveloperCommandResult.Failed(message);
        }

        private static string Normalize(string value) => value?.Trim() ?? string.Empty;
    }
}
