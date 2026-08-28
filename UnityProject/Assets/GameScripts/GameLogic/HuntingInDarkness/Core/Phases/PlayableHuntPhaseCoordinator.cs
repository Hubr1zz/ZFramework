using System;
using CardGame.ActionQueue;
using Cysharp.Threading.Tasks;
using GameplayBase;
using HuntingInDarkness.ActionFlow;
using HuntingInDarkness.ActionFlow.Campaign;
using HuntingInDarkness.ActionFlow.Events;
using HuntingInDarkness.ActionFlow.Hunt;
using HuntingInDarkness.ActionFlow.Presentation;
using HuntingInDarkness.Combat;
using HuntingInDarkness.Data;
using HuntingInDarkness.Hunt;
using HuntingInDarkness.Settlement;
using HuntingInDarkness.ViewLayer.Tabletop;
using HuntingInDarkness.ViewLayer.Hunt;
using UI.Hunt;
using UnityEngine;

namespace Core
{
    internal sealed class PlayableHuntPhaseCoordinator : IDisposable
    {
        private Func<IPlayableHuntRuntime> currentRuntimeProvider;
        private Func<IActionEnvironmentInstallerRegistry> installerRegistryProvider;
        private ITabletopRandomInteractionPresenter randomInteractionPresenter;
        private GameObject huntRoot;
        private IPlayableHuntRetreatInput retreatInput;
        private Action<CampaignEncounterRequest> encounterRequested;
        private Action<HuntRecord> huntCompleted;
        private Action<IPlayableHuntRuntime> checkpointCommitted;
        private HuntMapVisualizer visualizer;
        private HuntUIManager huntUI;
        private HuntRetreatPanel3D retreatPanel;
        private long activeGenerationId;
        private bool disposed;

        internal HuntMapVisualizer Visualizer => visualizer;

        internal void Configure(Func<IPlayableHuntRuntime> currentRuntimeProvider, Func<IActionEnvironmentInstallerRegistry> installerRegistryProvider, ITabletopRandomInteractionPresenter randomInteractionPresenter, GameObject huntRoot, GameObject uiHunt, IPlayableHuntRetreatInput retreatInput, Action<CampaignEncounterRequest> encounterRequested, Action<HuntRecord> huntCompleted, Action<IPlayableHuntRuntime> checkpointCommitted)
        {
            if (disposed) throw new ObjectDisposedException(nameof(PlayableHuntPhaseCoordinator));
            if (this.currentRuntimeProvider != null) throw new InvalidOperationException("狩猎阶段表现协调器已经配置。");
            this.currentRuntimeProvider = currentRuntimeProvider ?? throw new ArgumentNullException(nameof(currentRuntimeProvider));
            this.installerRegistryProvider = installerRegistryProvider ?? throw new ArgumentNullException(nameof(installerRegistryProvider));
            this.randomInteractionPresenter = randomInteractionPresenter;
            this.huntRoot = huntRoot;
            _ = uiHunt;
            this.retreatInput = retreatInput ?? throw new ArgumentNullException(nameof(retreatInput));
            this.encounterRequested = encounterRequested;
            this.huntCompleted = huntCompleted;
            this.checkpointCommitted = checkpointCommitted;
        }

        internal HuntManager CreateManager(SettlementManager settlementManager)
        {
            EnsureConfigured();
            var sharedEventSystem = settlementManager?.Events ?? new EventSystem(new SettlementInstance(), new HuntingInDarkness.GameCore.Foundation.SystemRandomSource());
            var manager = new HuntManager(sharedEventSystem, bindInitialContent: false);
            manager.OnBossEncounterTriggered = () =>
            {
                IPlayableHuntRuntime source = currentRuntimeProvider();
                if (!IsCurrent(source, manager)) return;
                var request = new CampaignEncounterRequest(source.ActionSession.SessionId, PlayableEncounterRuntime.DefaultEncounterId, CampaignEncounterSourceKind.HuntBossTile, GamePhase.Hunt, manager.SquadPosition, string.Empty, manager.BoundRoute?.DestinationId ?? string.Empty);
                encounterRequested?.Invoke(request);
            };
            manager.OnHuntCompleted = record =>
            {
                IPlayableHuntRuntime source = currentRuntimeProvider();
                if (!IsCurrent(source, manager)) return;
                huntCompleted?.Invoke(record);
            };
            return manager;
        }

        internal PlayableHuntActionSession CreateActionSession(HuntManager manager, PlayableHuntEventOccurrenceStore restoredOccurrences)
        {
            EnsureConfigured();
            IPlayableHuntRuntime source = currentRuntimeProvider();
            return new PlayableHuntActionSession(manager, PlayableEncounterRuntime.DefaultEncounterId, manager.BoundRoute?.DestinationId ?? string.Empty, randomInteractionPresenter, visualizer, installerRegistryProvider(), restoredOccurrences, () =>
            {
                if (IsCurrent(source, manager)) checkpointCommitted?.Invoke(source);
            });
        }

        internal bool TryStartPresentationAndSession(PlayableHuntEventOccurrenceStore restoredOccurrences, out string reason)
        {
            EnsureConfigured();
            IPlayableHuntRuntime runtime = currentRuntimeProvider();
            if (runtime == null)
            {
                reason = "当前没有可启动的狩猎运行态。";
                return false;
            }
            if (huntRoot == null)
            {
                reason = "狩猎 3D 场景根节点不可用。";
                return false;
            }
            activeGenerationId = runtime.GenerationId;
            try
            {
                EnsureVisualizer();
                if (!runtime.TryActivateActionSession(restoredOccurrences, out reason))
                {
                    activeGenerationId = 0;
                    return false;
                }
                visualizer.Init(runtime.Manager, runtime.ExplorationPort);
                EnsureHuntRetreatPanel(runtime.Manager);
                EnsureHuntUI(runtime.Manager, runtime.ExplorationPort);
                if (!IsTabletopPresentationReady)
                    throw new InvalidOperationException("狩猎必要 3D 交互未完整创建。");
            }
            catch (Exception exception)
            {
                runtime.DeactivateActionSession();
                Cleanup(false);
                activeGenerationId = 0;
                reason = $"狩猎 3D 交互与 ActionSession 初始化失败：{exception.Message}";
                return false;
            }
            reason = string.Empty;
            return true;
        }

        internal void RestorePreviousPresentation(GamePhase previousPhase, IPlayableHuntRuntime previousHunt)
        {
            if (previousPhase == GamePhase.Hunt && previousHunt != null)
            {
                activeGenerationId = previousHunt.GenerationId;
                visualizer?.Init(previousHunt.Manager, previousHunt.ExplorationPort);
                EnsureHuntRetreatPanel(previousHunt.Manager);
                EnsureHuntUI(previousHunt.Manager, previousHunt.ExplorationPort);
                return;
            }
            activeGenerationId = 0;
            if (previousPhase == GamePhase.Settlement)
                Cleanup(true);
        }

        internal void Cleanup(bool includeVisualizer = true)
        {
            if (includeVisualizer)
                activeGenerationId = 0;
            if (retreatPanel != null)
                UnityEngine.Object.Destroy(retreatPanel.gameObject);
            if (huntUI != null)
            {
                huntUI.ReleaseBindings();
                UnityEngine.Object.Destroy(huntUI.gameObject);
            }
            if (includeVisualizer && visualizer != null)
                UnityEngine.Object.Destroy(visualizer.gameObject);
            retreatPanel = null;
            huntUI = null;
            if (includeVisualizer)
                visualizer = null;
        }

        internal void EnsureHuntUI(HuntManager manager, IHuntExplorationPort port)
        {
            EnsureConfigured();
            if (visualizer == null) throw new InvalidOperationException("狩猎地图表现尚未初始化。");
            if (huntUI != null)
            {
                huntUI.InitTabletop(manager, visualizer, port);
                return;
            }
            if (huntRoot == null) throw new InvalidOperationException("狩猎 3D 场景根节点不可用。");
            var presentationObject = new GameObject("HuntTabletopPresentation");
            presentationObject.transform.SetParent(huntRoot.transform, false);
            huntUI = presentationObject.AddComponent<HuntUIManager>();
            huntUI.InitTabletop(manager, visualizer, port);
        }

        internal void EnsureHuntRetreatPanel(HuntManager manager)
        {
            EnsureConfigured();
            if (visualizer == null) throw new InvalidOperationException("狩猎地图表现尚未初始化。");
            retreatPanel ??= HuntRetreatPanel3D.Create(visualizer.transform);
            retreatPanel.Initialize(retreatInput, manager);
        }

        private void EnsureVisualizer()
        {
            if (visualizer != null) return;
            if (huntRoot == null) throw new InvalidOperationException("狩猎 3D 场景根节点不可用。");
            var visualizerObject = new GameObject("HuntMapVisualizer");
            visualizerObject.transform.SetParent(huntRoot.transform, false);
            visualizer = visualizerObject.AddComponent<HuntMapVisualizer>();
        }

        private bool IsTabletopPresentationReady => visualizer != null && retreatPanel != null && huntUI?.IsTabletopReady == true;

        private bool IsCurrent(IPlayableHuntRuntime runtime, HuntManager manager)
        {
            return runtime != null && runtime.GenerationId == activeGenerationId && activeGenerationId != 0 && ReferenceEquals(runtime.Manager, manager) && runtime.IsActionSessionActive;
        }

        private void EnsureConfigured()
        {
            if (disposed) throw new ObjectDisposedException(nameof(PlayableHuntPhaseCoordinator));
            if (currentRuntimeProvider == null) throw new InvalidOperationException("狩猎阶段表现协调器尚未配置。");
        }

        public void Dispose()
        {
            if (disposed) return;
            Cleanup(true);
            disposed = true;
            currentRuntimeProvider = null;
            installerRegistryProvider = null;
            encounterRequested = null;
            huntCompleted = null;
            checkpointCommitted = null;
        }
    }
}
