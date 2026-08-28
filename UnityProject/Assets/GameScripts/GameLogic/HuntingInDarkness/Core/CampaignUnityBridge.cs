using System;
using CardTactics.CombatSystem;
using GameplayBase;
using GameplayBase.CombatSystem;
using HuntingInDarkness.ActionFlow.Campaign;
using HuntingInDarkness.ActionFlow.Events;
using HuntingInDarkness.ActionFlow.Settlement;
using HuntingInDarkness.Data;
using HuntingInDarkness.Settlement;

namespace Core
{
    /// <summary>把 Unity EventBus 消息转发到唯一 Campaign flow 或全局桌面表现，不持有玩法状态。</summary>
    internal sealed class CampaignUnityBridge : IDisposable
    {
        private readonly CampaignFlowCoordinator flow;
        private readonly CampaignTerminalProjection terminalProjection;
        private readonly GlobalTabletopPresentation presentation;
        private readonly Action<string> info;
        private bool disposed;

        internal CampaignUnityBridge(CampaignFlowCoordinator flow, ICampaignReadModel readModel, GlobalTabletopPresentation presentation, Action<string> info)
        {
            this.flow = flow ?? throw new ArgumentNullException(nameof(flow));
            terminalProjection = new CampaignTerminalProjection(readModel);
            this.presentation = presentation ?? throw new ArgumentNullException(nameof(presentation));
            this.info = info;
            EventBus.Subscribe<BossDefeatedEvent>(OnBossDefeated);
            EventBus.Subscribe<GameOverEvent>(OnGameOver);
            EventBus.Subscribe<HunterRosterChangedEvent>(OnHunterRosterChanged);
            EventBus.Subscribe<CardHoverPreviewEvent>(OnCardHoverPreview);
            EventBus.Subscribe<CardHoverPreviewEndEvent>(OnCardHoverPreviewEnd);
            EventBus.Subscribe<SettlementTransactionCommittedEvent>(OnSettlementTransactionCommitted);
            EventBus.Subscribe<CampaignEncounterRequestedEvent>(OnCampaignEncounterRequested);
            EventBus.Subscribe<PlayableEventEncounterRequestedEvent>(OnPlayableEventEncounterRequested);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            EventBus.Unsubscribe<BossDefeatedEvent>(OnBossDefeated);
            EventBus.Unsubscribe<GameOverEvent>(OnGameOver);
            EventBus.Unsubscribe<HunterRosterChangedEvent>(OnHunterRosterChanged);
            EventBus.Unsubscribe<CardHoverPreviewEvent>(OnCardHoverPreview);
            EventBus.Unsubscribe<CardHoverPreviewEndEvent>(OnCardHoverPreviewEnd);
            EventBus.Unsubscribe<SettlementTransactionCommittedEvent>(OnSettlementTransactionCommitted);
            EventBus.Unsubscribe<CampaignEncounterRequestedEvent>(OnCampaignEncounterRequested);
            EventBus.Unsubscribe<PlayableEventEncounterRequestedEvent>(OnPlayableEventEncounterRequested);
        }

        private void OnBossDefeated(BossDefeatedEvent _)
        {
            if (terminalProjection.CurrentPhase != GamePhase.BossFight) return;
            info?.Invoke("收到 BossDefeatedEvent → 狩猎结算 → 营地");
            flow.HandleBossDefeated();
        }

        private void OnGameOver(GameOverEvent evt)
        {
            info?.Invoke($"游戏结束：{evt.Reason}");
            presentation.ShowGameOver(evt.Reason);
        }

        private void OnHunterRosterChanged(HunterRosterChangedEvent _)
        {
            if (terminalProjection.TryCreateGameOver(out GameOverEvent gameOver)) EventBus.Publish(gameOver);
        }

        private void OnCardHoverPreview(CardHoverPreviewEvent evt) => flow.HighlightCardPreview(evt.CardInstanceId);
        private void OnCardHoverPreviewEnd(CardHoverPreviewEndEvent _) => flow.ClearCardPreview();
        private void OnSettlementTransactionCommitted(SettlementTransactionCommittedEvent evt) => flow.HandleSettlementTransactionCommitted(evt);
        private void OnCampaignEncounterRequested(CampaignEncounterRequestedEvent evt) => flow.HandleCampaignEncounterRequested(evt);
        private void OnPlayableEventEncounterRequested(PlayableEventEncounterRequestedEvent evt) => flow.HandlePlayableEventEncounterRequested(evt);
    }

    /// <summary>从已提交战役读模型投影终局事实；不读取 View，也不直接修改权威数据。</summary>
    internal sealed class CampaignTerminalProjection
    {
        private readonly ICampaignReadModel readModel;

        internal CampaignTerminalProjection(ICampaignReadModel readModel)
        {
            this.readModel = readModel ?? throw new ArgumentNullException(nameof(readModel));
        }

        internal GamePhase CurrentPhase => readModel.CurrentPhase;

        internal bool TryCreateGameOver(out GameOverEvent gameOver)
        {
            gameOver = default;
            SettlementInstance settlement = readModel.Settlement;
            if (!readModel.IsCampaignActive || settlement == null || settlement.GetAliveHunters().Count > 0) return false;
            gameOver = new GameOverEvent { Reason = "营地中所有猎人已经死亡。\n黑暗吞噬了这片聚落。" };
            return true;
        }
    }
}
