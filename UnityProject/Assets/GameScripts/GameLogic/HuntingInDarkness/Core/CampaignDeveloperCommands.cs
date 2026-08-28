using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameplayBase;
using HuntingInDarkness.Data;

namespace Core
{
    internal interface ICampaignDeveloperCommands
    {
        void Transition(GamePhase phase);
        void SignalBossDefeated();
        void AddHunter(string name);
        void AddResource(string resourceName, int amount);
        void Save();
        void Load();
        void DeleteSave();
    }

    /// <summary>隔离开发者面板的非生产命令，避免 GameManager 继续扩张测试逃生口。</summary>
    internal sealed class CampaignDeveloperCommands : ICampaignDeveloperCommands
    {
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

        public void Transition(GamePhase phase) => flow.TransitionToPhase(phase);
        public void SignalBossDefeated() => flow.HandleBossDefeated();

        public void AddHunter(string name)
        {
            HunterInstance hunter = flow.DevAddHunter(name);
            if (hunter == null)
            {
                warning?.Invoke("DevAddHunter: 营地运行态尚未初始化。");
                return;
            }
            info?.Invoke($"招募猎人：{hunter.Name}");
        }

        public void AddResource(string resourceName, int amount)
        {
            if (!flow.DevAddResource(resourceName, amount))
            {
                warning?.Invoke("DevAddResource: 营地运行态尚未初始化。");
                return;
            }
            info?.Invoke($"添加资源 {resourceName} ×{amount}");
        }

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
    }
}
