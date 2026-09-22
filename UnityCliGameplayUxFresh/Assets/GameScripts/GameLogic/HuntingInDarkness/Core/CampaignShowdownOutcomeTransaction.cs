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

        internal CampaignShowdownOutcomeTransaction(ICampaignShowdownOutcomeHost host)
        {
            this.host = host ?? throw new ArgumentNullException(nameof(host));
        }

        internal void HandleBossDefeated()
        {
            if (host.CurrentPhase != GamePhase.BossFight || host.ShowdownSession == null) return;
            host.ShowdownSession.AccumulateDefeatLoot();
            host.ShowdownSession.SettleWeaponMastery();
            if (host.HuntManager != null && host.SettlementData != null)
            {
                host.HuntManager.CompleteHunt(true, host.SettlementData);
                return;
            }
            host.RequestSettlementTransition();
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
            await UniTask.NextFrame();
            CompleteDefeatedHunt();
        }
    }
}
