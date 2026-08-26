using System;
using System.Collections.Generic;
using CardGame.ActionQueue;
using Cysharp.Threading.Tasks;
using GameplayBase;
using HuntingInDarkness.ActionFlow;
using HuntingInDarkness.ActionFlow.Campaign;
using HuntingInDarkness.ActionFlow.Events;
using HuntingInDarkness.ActionFlow.Hunt;
using HuntingInDarkness.ActionFlow.Presentation;
using HuntingInDarkness.ActionFlow.Settlement;
using HuntingInDarkness.Combat;
using HuntingInDarkness.Data;
using HuntingInDarkness.Hunt;
using HuntingInDarkness.Settlement;
using UI;
using UI.Hunt;
using UI.Settlement;
using UnityEngine;

namespace Core
{
    internal interface IPlayableCampaignPhasePortAccess
    {
        IPlayableSettlementPhasePort SettlementPhase { get; }
        IPlayableHuntPhasePort HuntPhase { get; }
        IPlayableShowdownPhasePort ShowdownPhase { get; }
    }

    internal interface IPlayableSettlementPhasePort
    {
        IPlayableSettlementRuntime Current { get; }
        PlayableSettlementActionSession CurrentSession { get; }
        void ConfigureRuntime(ISettlementDepartureRequestPort departureRequestPort);
        void ConfigureGameplay(Func<IPlayableEventInput> inputProvider, ITabletopRandomInteractionPresenter tabletop, Func<IActionEnvironmentInstallerRegistry> installerProvider, Func<IPlayableCampaignPersistentEffectProjection> projectionProvider);
        void ConfigurePresentation(SettlementTable3D table, GameObject root, SettlementUIManager ui, PlayableWorkshopCatalog workshop, PlayableSettlementContentCatalog settlementContent, Action<List<HunterInstance>> onDepartureRequested);
        void EnsurePresentation(SettlementManager manager);
        void Refresh();
        void RefreshCards();
        void RefreshCrafting();
        bool QueueEvents(IPlayableSettlementRuntime runtime, PlayableSettlementActionSession session, IReadOnlyList<SettlementEventWork> works, SettlementEventRestoreProjection restoreProjection = null, string restoredChainId = null);
        UniTask<bool> ResolveEventsAsync(IPlayableSettlementRuntime runtime, PlayableSettlementActionSession session, IReadOnlyList<SettlementEventWork> works, SettlementEventRestoreProjection restoreProjection = null, string restoredChainId = null);
    }

    internal interface IPlayableHuntPhasePort
    {
        IPlayableHuntRuntime Current { get; }
        HuntMapVisualizer Visualizer { get; }
        void ConfigureRuntime();
        void Configure(Func<IActionEnvironmentInstallerRegistry> installerRegistryProvider, ITabletopRandomInteractionPresenter randomInteractionPresenter, GameObject huntRoot, GameObject uiHunt, IPlayableHuntRetreatInput retreatInput, Action<CampaignEncounterRequest> encounterRequested, Action<HuntRecord> huntCompleted, Action<IPlayableHuntRuntime> checkpointCommitted);
        bool TryPrepareInitialized(IPlayableSettlementRuntime settlement, PlayableHuntStartPlan plan, out IPlayableHuntRuntime candidate, out string reason);
        bool TryStartCurrentPresentationAndSession(PlayableHuntEventOccurrenceStore restoredOccurrences, out string reason);
        void DeactivateCurrentActionSession();
        void CleanupCurrentPresentation(bool includeVisualizer = true);
        void RestorePreviousPresentation(GamePhase previousPhase, IPlayableHuntRuntime previousHunt);
        void EnsureHuntUI(HuntManager manager, IHuntExplorationPort port);
        void EnsureHuntRetreatPanel(HuntManager manager);
    }

    internal interface IPlayableShowdownPhasePort
    {
        PlayableCombatSession Current { get; }
        bool TryPrepare(PlayableCombatSessionConfiguration configuration, out string reason);
        void Start(IReadOnlyList<HunterInstance> hunters, HunterManagementSystem hunterManagement, Action onPartyDefeated);
        void Update();
        void DisposeCurrent();
    }
}
