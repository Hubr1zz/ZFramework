using System;
using System.Collections.Generic;
using System.Threading;
using CardGame.ActionQueue;
using CardTactics.CombatSystem;
using Cysharp.Threading.Tasks;
using Config;
using GameplayBase;
using GameplayBase.CombatSystem;
using HuntingInDarkness.ActionFlow;
using HuntingInDarkness.ActionFlow.Campaign;
using HuntingInDarkness.ActionFlow.Events;
using HuntingInDarkness.ActionFlow.Hunt;
using HuntingInDarkness.ActionFlow.Presentation;
using HuntingInDarkness.ActionFlow.Settlement;
using HuntingInDarkness.Combat;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Hunters;
using HuntingInDarkness.GameCore.Cards;
using HuntingInDarkness.GameCore.Combat;
using HuntingInDarkness.GameCore.Settlement;
using HuntingInDarkness.Hunt;
using HuntingInDarkness.Settlement;
using UI;
using UI.Hunt;
using UI.Settlement;
using SO.Boss.ActionCard;
using SO.Boss.HitLocation;
using UnityEngine;

namespace Core
{
    internal interface IPlayableCampaignPhasePortAccess
    {
        IPlayableSettlementPhasePort SettlementPhase { get; }
        IPlayableSettlementGameplayPort SettlementGameplay { get; }
        IPlayableHuntPhasePort HuntPhase { get; }
        IPlayableShowdownPhasePort ShowdownPhase { get; }
    }

    internal interface IPlayableSettlementPhasePort
    {
        IPlayableSettlementRuntime Current { get; }
        PlayableSettlementActionSession CurrentSession { get; }
        void ConfigureRuntime(ISettlementDepartureRequestPort departureRequestPort);
        void ConfigureGameplay(Func<IPlayableEventInput> inputProvider, ITabletopRandomInteractionPresenter tabletop, Func<IActionEnvironmentInstallerRegistry> installerProvider, Func<IPlayableCampaignPersistentEffectProjection> projectionProvider);
        void ConfigurePresentation(SettlementTable3D table, GameObject root, PlayableWorkshopCatalog workshop, PlayableSettlementContentCatalog settlementContent, Action<List<HunterInstance>> onDepartureRequested);
        bool ActivateCurrentActionSession(out string reason);
        void DeactivateCurrentActionSession();
        void EnsurePresentation(SettlementManager manager);
        void Refresh();
        void RefreshCards();
        void RefreshCrafting();
        bool IsEventRestoreReady { get; }
        string EventRestoreFailureReason { get; }
        bool QueueCurrentEvents(IReadOnlyList<SettlementEventWork> works, SettlementEventRestoreProjection restoreProjection = null, string restoredChainId = null);
        bool QueueEvents(IPlayableSettlementRuntime runtime, PlayableSettlementActionSession session, IReadOnlyList<SettlementEventWork> works, SettlementEventRestoreProjection restoreProjection = null, string restoredChainId = null);
        UniTask<bool> ResolveEventsAsync(IPlayableSettlementRuntime runtime, PlayableSettlementActionSession session, IReadOnlyList<SettlementEventWork> works, SettlementEventRestoreProjection restoreProjection = null, string restoredChainId = null);
    }

    internal interface IPlayableSettlementGameplayPort
    {
        bool CanTrainWeapon(int hunterId, string masteryId, out string reason);
        UniTask<WeaponTrainingCommandResult> TrainWeaponAsync(int hunterId, string masteryId);
        bool CanCraft(CraftRecipe recipe, out string reason);
        UniTask<SettlementCraftCommandResult> CraftAsync(CraftRecipe recipe);
        UniTask<SettlementEquipmentCommandResult> EquipItemAsync(int hunterId, ItemData item);
        UniTask<SettlementEquipmentCommandResult> UnequipItemAsync(int hunterId, int equipmentInstanceId);
        bool CanRecruitHunter(out string reason);
        UniTask<RecruitHunterCommandResult> RecruitHunterAsync(HunterData template, string requestedName);
        bool HasRecoverableHunter();
        bool CanRecoverHunter(int hunterId, HunterBodyPart bodyPart, out string reason);
        UniTask<RecoverHunterCommandResult> RecoverHunterAsync(int hunterId, HunterBodyPart bodyPart);
        UniTask<HunterGrowthCommandResult> SpendHunterGrowthAsync(int hunterId, HunterGrowthChoice choice);
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
        IPlayableShowdownGameplayPort Gameplay { get; }
        bool TryPrepare(PlayableCombatSessionConfiguration configuration, out string reason);
        void Start(IReadOnlyList<HunterInstance> hunters, HunterManagementSystem hunterManagement, Action onPartyDefeated);
        void Update();
        void DisposeCurrent();
    }

    internal interface IPlayableShowdownGameplayPort
    {
        CombatManager CombatManager { get; }
        TurnPhase CurrentPhase { get; }
        int CurrentTurnNumber { get; }
        IReadOnlyList<ICharacterState> PlayerCharacters { get; }
        IBossState Boss { get; }
        IReadOnlyList<HitLocationRuntimeState> BossHitLocationStates { get; }
        IReadOnlyList<BossActionCardData> BossRevealedCards { get; }
        Character GetCharacter(int characterId);
        CharacterRuntimeData GetCharacterData(int characterId);
        IReadOnlyList<ICharacterActionCardInstanceState> GetCardsOf(int characterId);
        ICharacterActionCardInstanceState GetCard(int cardInstanceId);
        Vector3 GetEntityWorldPosition(int entityId);
        void SelectCharacter(int characterId);
        void PlayCard(int cardInstanceId, int targetEntityId);
        void RestoreCard(int cardInstanceId);
        void DiscardCard(int cardInstanceId);
        void EndTurn();
        bool AssistOvertimeCharacter(int helperId, int targetId);
        int AddInspiration(int characterId, int amount);
        UniTask<InspirationGain> AddInspirationAsync(int characterId, CombatInspirationColor color, CancellationToken cancellationToken);
        IReadOnlyList<CombatInspirationToken> GetInspirationTokens(int characterId);
        int GetInspirationCapacity(int characterId);
        bool RelieveOvertimeCharacter(int targetId);
        TimelineActionStatus GetTimelineStatus(int characterId);
    }
}
