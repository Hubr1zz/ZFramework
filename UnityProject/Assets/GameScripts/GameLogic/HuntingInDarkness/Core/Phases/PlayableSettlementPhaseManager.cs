using System;
using System.Collections.Generic;
using CardGame.ActionQueue;
using Cysharp.Threading.Tasks;
using HuntingInDarkness.ActionFlow;
using HuntingInDarkness.ActionFlow.Events;
using HuntingInDarkness.ActionFlow.Presentation;
using HuntingInDarkness.ActionFlow.Settlement;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Hunters;
using HuntingInDarkness.GameCore.Settlement;
using HuntingInDarkness.Settlement;
using UI;
using UI.Settlement;
using UnityEngine;

namespace Core
{
    internal sealed class PlayableSettlementPhaseManager : IDisposable, IPlayableSettlementPhasePort, IPlayableSettlementGameplayPort
    {
        private readonly Func<IPlayableCampaignPersistentEffectProjection> persistentEffectProjectionProvider;
        private readonly PlayableSettlementPhaseCoordinator coordinator;
        private readonly HashSet<PlayableSettlementRuntime> runtimes = new();
        private PlayableSettlementRuntimeConfiguration configuration;
        private PlayableSettlementRuntime current;
        private long nextGenerationId;
        private bool disposed;

        internal IPlayableSettlementRuntime Current => current;
        internal PlayableSettlementPhaseCoordinator Coordinator => coordinator;

        internal PlayableSettlementPhaseManager(Func<IPlayableCampaignPersistentEffectProjection> persistentEffectProjectionProvider)
        {
            this.persistentEffectProjectionProvider = persistentEffectProjectionProvider ?? throw new ArgumentNullException(nameof(persistentEffectProjectionProvider));
            coordinator = new PlayableSettlementPhaseCoordinator(() => current);
        }

        IPlayableSettlementRuntime IPlayableSettlementPhasePort.Current => Current;
        PlayableSettlementActionSession IPlayableSettlementPhasePort.CurrentSession => coordinator.CurrentSession;
        void IPlayableSettlementPhasePort.ConfigureRuntime(ISettlementDepartureRequestPort departureRequestPort) => Configure(new PlayableSettlementRuntimeConfiguration(departureRequestPort, coordinator.CreateActionSession));
        void IPlayableSettlementPhasePort.ConfigureGameplay(Func<IPlayableEventInput> inputProvider, ITabletopRandomInteractionPresenter tabletop, Func<IActionEnvironmentInstallerRegistry> installerProvider, Func<IPlayableCampaignPersistentEffectProjection> projectionProvider) => coordinator.ConfigureGameplay(inputProvider, tabletop, installerProvider, projectionProvider);
        void IPlayableSettlementPhasePort.ConfigurePresentation(SettlementTable3D table, GameObject root, SettlementUIManager ui, PlayableWorkshopCatalog workshop, PlayableSettlementContentCatalog settlementContent, Action<List<HunterInstance>> onDepartureRequested) => coordinator.ConfigurePresentation(table, root, ui, workshop, settlementContent, onDepartureRequested);
        bool IPlayableSettlementPhasePort.ActivateCurrentActionSession(out string reason)
        {
            if (current == null)
            {
                reason = "营地运行态尚未激活。";
                return false;
            }
            return current.TryActivateActionSession(out reason);
        }
        void IPlayableSettlementPhasePort.DeactivateCurrentActionSession() => current?.DeactivateActionSession();
        void IPlayableSettlementPhasePort.EnsurePresentation(SettlementManager manager) => coordinator.EnsurePresentation(manager);
        void IPlayableSettlementPhasePort.Refresh() => coordinator.Refresh();
        void IPlayableSettlementPhasePort.RefreshCards() => coordinator.RefreshCards();
        void IPlayableSettlementPhasePort.RefreshCrafting() => coordinator.RefreshCrafting();
        bool IPlayableSettlementPhasePort.IsEventRestoreReady => current?.EventRestore == null || current.EventRestore.IsReady;
        string IPlayableSettlementPhasePort.EventRestoreFailureReason => current?.EventRestore?.FailureReason;
        bool IPlayableSettlementPhasePort.QueueCurrentEvents(IReadOnlyList<SettlementEventWork> works, SettlementEventRestoreProjection restoreProjection, string restoredChainId) => coordinator.QueueEvents(current, coordinator.CurrentSession, works, restoreProjection, restoredChainId);
        bool IPlayableSettlementPhasePort.QueueEvents(IPlayableSettlementRuntime runtime, PlayableSettlementActionSession session, IReadOnlyList<SettlementEventWork> works, SettlementEventRestoreProjection restoreProjection, string restoredChainId) => coordinator.QueueEvents(runtime, session, works, restoreProjection, restoredChainId);
        UniTask<bool> IPlayableSettlementPhasePort.ResolveEventsAsync(IPlayableSettlementRuntime runtime, PlayableSettlementActionSession session, IReadOnlyList<SettlementEventWork> works, SettlementEventRestoreProjection restoreProjection, string restoredChainId) => coordinator.ResolveEventsAsync(runtime, session, works, restoreProjection, restoredChainId);
        bool IPlayableSettlementGameplayPort.CanTrainWeapon(int hunterId, string masteryId, out string reason) => coordinator.CanTrainWeapon(hunterId, masteryId, out reason);
        UniTask<WeaponTrainingCommandResult> IPlayableSettlementGameplayPort.TrainWeaponAsync(int hunterId, string masteryId) => coordinator.TrainWeaponAsync(hunterId, masteryId);
        bool IPlayableSettlementGameplayPort.CanCraft(CraftRecipe recipe, out string reason) => coordinator.CanCraft(recipe, out reason);
        UniTask<SettlementCraftCommandResult> IPlayableSettlementGameplayPort.CraftAsync(CraftRecipe recipe) => coordinator.CraftAsync(recipe);
        UniTask<SettlementEquipmentCommandResult> IPlayableSettlementGameplayPort.EquipItemAsync(int hunterId, ItemData item) => coordinator.EquipItemAsync(hunterId, item);
        UniTask<SettlementEquipmentCommandResult> IPlayableSettlementGameplayPort.UnequipItemAsync(int hunterId, int equipmentInstanceId) => coordinator.UnequipItemAsync(hunterId, equipmentInstanceId);
        bool IPlayableSettlementGameplayPort.CanRecruitHunter(out string reason) => coordinator.CanRecruitHunter(out reason);
        UniTask<RecruitHunterCommandResult> IPlayableSettlementGameplayPort.RecruitHunterAsync(HunterData template, string requestedName) => coordinator.RecruitHunterAsync(template, requestedName);
        bool IPlayableSettlementGameplayPort.HasRecoverableHunter() => coordinator.HasRecoverableHunter();
        bool IPlayableSettlementGameplayPort.CanRecoverHunter(int hunterId, HunterBodyPart bodyPart, out string reason) => coordinator.CanRecoverHunter(hunterId, bodyPart, out reason);
        UniTask<RecoverHunterCommandResult> IPlayableSettlementGameplayPort.RecoverHunterAsync(int hunterId, HunterBodyPart bodyPart) => coordinator.RecoverHunterAsync(hunterId, bodyPart);
        UniTask<HunterGrowthCommandResult> IPlayableSettlementGameplayPort.SpendHunterGrowthAsync(int hunterId, HunterGrowthChoice choice) => coordinator.SpendHunterGrowthAsync(hunterId, choice);

        internal void Configure(PlayableSettlementRuntimeConfiguration nextConfiguration)
        {
            ThrowIfDisposed();
            if (configuration != null) throw new InvalidOperationException("营地运行态配置已经安装。");
            configuration = nextConfiguration ?? throw new ArgumentNullException(nameof(nextConfiguration));
            coordinator.Configure(configuration);
        }

        internal bool TryPrepareNew(out IPlayableSettlementRuntime candidate, out string reason)
        {
            ThrowIfDisposed();
            candidate = null;
            if (!TryGetConfiguration(out reason)) return false;
            var runtime = new PlayableSettlementRuntime(++nextGenerationId, new SettlementManager(), configuration, false, coordinator);
            runtimes.Add(runtime);
            candidate = runtime;
            reason = string.Empty;
            return true;
        }

        internal bool TryPrepareRestore(SettlementInstance data, out IPlayableSettlementRuntime candidate, out string reason)
        {
            ThrowIfDisposed();
            candidate = null;
            if (!TryGetConfiguration(out reason)) return false;
            if (!SettlementManager.TryPrepareCandidate(data, out SettlementManager manager, out reason)) return false;
            var runtime = new PlayableSettlementRuntime(++nextGenerationId, manager, configuration, true, coordinator);
            runtimes.Add(runtime);
            candidate = runtime;
            reason = string.Empty;
            return true;
        }

        internal bool TrySwap(IPlayableSettlementRuntime expectedCurrent, IPlayableSettlementRuntime replacement, out string reason)
        {
            ThrowIfDisposed();
            if (!ReferenceEquals(current, expectedCurrent))
            {
                reason = "权威营地运行世代已变化，拒绝提交过期候选。";
                return false;
            }
            PlayableSettlementRuntime next = replacement as PlayableSettlementRuntime;
            if (replacement != null && (next == null || !runtimes.Contains(next) || !next.IsDetached))
            {
                reason = "替换目标不是当前战役持有的可发布营地候选。";
                return false;
            }

            IPlayableCampaignPersistentEffectProjection projection = persistentEffectProjectionProvider();
            if (projection != null && !projection.TrySynchronize(next?.Data, out reason)) return false;
            if (next != null && !next.TryPreparePublication(out reason))
            {
                projection?.TrySynchronize(current?.Data, out _);
                return false;
            }

            PlayableSettlementRuntime previous = current;
            try
            {
                previous?.Detach();
                next?.Publish();
                current = next;
                coordinator.Deactivate(previous);
                reason = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                projection?.TrySynchronize(previous?.Data, out _);
                reason = $"提交营地运行世代失败：{exception.Message}";
                return false;
            }
        }

        internal void Release(IPlayableSettlementRuntime runtime)
        {
            ThrowIfDisposed();
            if (runtime is not PlayableSettlementRuntime owned || !runtimes.Contains(owned))
                throw new InvalidOperationException("营地运行世代不属于当前战役。");
            if (owned.IsCurrent)
                throw new InvalidOperationException("不能释放当前权威营地运行世代。");
            owned.Dispose();
            runtimes.Remove(owned);
        }

        internal bool Owns(PlayableSettlementRuntime runtime) => runtime != null && runtimes.Contains(runtime);

        internal void Reset()
        {
            ThrowIfDisposed();
            coordinator.Reset();
            ResetRuntimes();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            coordinator.Dispose();
            ResetRuntimes();
            configuration = null;
        }

        private bool TryGetConfiguration(out string reason)
        {
            if (configuration != null)
            {
                reason = string.Empty;
                return true;
            }
            reason = "营地运行态组合配置尚未安装。";
            return false;
        }

        private void ResetRuntimes()
        {
            foreach (PlayableSettlementRuntime runtime in runtimes)
                runtime.Dispose();
            runtimes.Clear();
            current = null;
        }

        private void ThrowIfDisposed()
        {
            if (disposed) throw new ObjectDisposedException(nameof(PlayableSettlementPhaseManager));
        }
    }
}
