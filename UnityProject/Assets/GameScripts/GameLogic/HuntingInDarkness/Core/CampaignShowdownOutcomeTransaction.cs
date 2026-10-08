using System;
using Cysharp.Threading.Tasks;
using GameplayBase;
using HuntingInDarkness.Combat;
using HuntingInDarkness.Data;
using HuntingInDarkness.Hunt;

namespace Core
{
    internal interface ICampaignShowdownOutcomeHost
    {
        GamePhase CurrentPhase { get; }
        PlayableCombatSession ShowdownSession { get; }
        HuntManager HuntManager { get; }
        SettlementInstance SettlementData { get; }
        void ApplyBossFightLoot();
        void RequestSettlementTransition();
    }

    internal sealed class CampaignShowdownOutcomeTransaction
    {
        private readonly ICampaignShowdownOutcomeHost host;
        private PlayableCombatSession victoryRewardSession;
        private PlayableCombatSession victoryCompletionSession;

        internal CampaignShowdownOutcomeTransaction(ICampaignShowdownOutcomeHost host)
        {
            this.host = host ?? throw new ArgumentNullException(nameof(host));
        }

        internal void HandleBossDefeated()
        {
            PlayableCombatSession capturedSession = host.ShowdownSession;
            if (host.CurrentPhase != GamePhase.BossFight || capturedSession == null || !capturedSession.IsActive || ReferenceEquals(victoryRewardSession, capturedSession)) return;
            victoryRewardSession = capturedSession;
            capturedSession.AccumulateDefeatLoot();
            capturedSession.SettleWeaponMastery();
            if (ReferenceEquals(victoryCompletionSession, capturedSession)) return;
            victoryCompletionSession = capturedSession;
            CompleteVictoryAfterActionAsync(capturedSession).Forget();
        }

        internal void CompleteDefeatedHunt()
        {
            if (host.CurrentPhase != GamePhase.BossFight) return;
            if (host.HuntManager != null && host.SettlementData != null)
            {
                host.HuntManager.CompleteHunt(false, host.SettlementData);
                return;
            }
            host.RequestSettlementTransition();
        }

        internal void ApplyCommittedLoot()
        {
            host.ApplyBossFightLoot();
        }

        internal async UniTask CompleteDefeatedHuntAfterActionAsync()
        {
            PlayableCombatSession capturedSession = host.ShowdownSession;
            if (capturedSession == null) return;
            while (host.CurrentPhase == GamePhase.BossFight && ReferenceEquals(host.ShowdownSession, capturedSession) && capturedSession.IsActive && capturedSession.HasRunningAction)
                await UniTask.NextFrame();
            if (host.CurrentPhase != GamePhase.BossFight || !ReferenceEquals(host.ShowdownSession, capturedSession) || !capturedSession.IsActive) return;
            CompleteDefeatedHunt();
        }

        private async UniTask CompleteVictoryAfterActionAsync(PlayableCombatSession capturedSession)
        {
            while (host.CurrentPhase == GamePhase.BossFight && ReferenceEquals(host.ShowdownSession, capturedSession) && capturedSession.IsActive && capturedSession.HasRunningAction)
                await UniTask.NextFrame();
            if (host.CurrentPhase != GamePhase.BossFight || !ReferenceEquals(host.ShowdownSession, capturedSession) || !capturedSession.IsActive) return;
            if (host.HuntManager != null && host.SettlementData != null)
            {
                host.HuntManager.CompleteHunt(true, host.SettlementData);
                return;
            }
            host.RequestSettlementTransition();
        }
    }
}
