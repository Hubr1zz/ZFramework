using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using HuntingInDarkness.ActionFlow;
using HuntingInDarkness.ActionFlow.Events;
using HuntingInDarkness.ActionFlow.Settlement;
using HuntingInDarkness.ActionFlow.Presentation;
using HuntingInDarkness.Combat;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Hunters;
using HuntingInDarkness.GameCore.Settlement;
using HuntingInDarkness.Settlement;
using HuntingInDarkness.ActionFlow.Inventions;
using UI;
using UI.Settlement;
using UnityEngine;

namespace Core
{
    internal sealed class PlayableSettlementPhaseCoordinator : IDisposable
    {
        private readonly Func<IPlayableSettlementRuntime> currentRuntimeProvider;
        private PlayableSettlementRuntimeConfiguration configuration;
        private PlayableSettlementActionSession actionSession;
        private IPlayableSettlementRuntime actionSessionOwner;
        private SettlementTable3D settlementTable;
        private GameObject settlementRoot;
        private SettlementUIManager settlementUI;
        private PlayableWorkshopCatalog workshopCatalog;
        private PlayableSettlementContentCatalog settlementContentCatalog;
        private Func<IPlayableEventInput> eventInputProvider;
        private ITabletopRandomInteractionPresenter tabletopInteraction;
        private Func<IActionEnvironmentInstallerRegistry> installerRegistryProvider;
        private Func<IPlayableCampaignPersistentEffectProjection> persistentEffectProjectionProvider;
        private Action<List<HunterInstance>> departureRequested;
        private SettlementManager presentationManager;
        private CancellationTokenSource eventRunnerCancellation;
        private SettlementEventRestoreProjection eventRunnerProjection;
        private long eventRunnerId;
        private bool presentationConfigured;
        private bool missingUIWarningLogged;
        private bool disposed;

        internal PlayableSettlementActionSession CurrentSession => GetSession(currentRuntimeProvider());

        internal PlayableSettlementPhaseCoordinator(Func<IPlayableSettlementRuntime> currentRuntimeProvider)
        {
            this.currentRuntimeProvider = currentRuntimeProvider ?? throw new ArgumentNullException(nameof(currentRuntimeProvider));
        }

        internal void Configure(PlayableSettlementRuntimeConfiguration nextConfiguration)
        {
            ThrowIfDisposed();
            if (configuration != null) throw new InvalidOperationException("营地阶段协调器已经配置。");
            configuration = nextConfiguration ?? throw new ArgumentNullException(nameof(nextConfiguration));
        }

        internal void ConfigurePresentation(SettlementTable3D table, GameObject root, SettlementUIManager ui, PlayableWorkshopCatalog workshop, PlayableSettlementContentCatalog settlementContent, Action<List<HunterInstance>> onDepartureRequested)
        {
            ThrowIfDisposed();
            if (presentationConfigured)
                throw new InvalidOperationException("营地阶段表现已经配置。");
            presentationConfigured = true;
            settlementTable = table;
            settlementRoot = root;
            settlementUI = ui;
            workshopCatalog = workshop;
            settlementContentCatalog = settlementContent;
            departureRequested = onDepartureRequested;
        }

        internal void ConfigureGameplay(Func<IPlayableEventInput> inputProvider, ITabletopRandomInteractionPresenter tabletop, Func<IActionEnvironmentInstallerRegistry> installerProvider, Func<IPlayableCampaignPersistentEffectProjection> projectionProvider)
        {
            ThrowIfDisposed();
            if (installerRegistryProvider != null || persistentEffectProjectionProvider != null)
                throw new InvalidOperationException("营地阶段玩法组合已经配置。");
            eventInputProvider = inputProvider ?? throw new ArgumentNullException(nameof(inputProvider));
            tabletopInteraction = tabletop;
            installerRegistryProvider = installerProvider ?? throw new ArgumentNullException(nameof(installerProvider));
            persistentEffectProjectionProvider = projectionProvider ?? throw new ArgumentNullException(nameof(projectionProvider));
        }

        internal PlayableSettlementActionSession CreateActionSession(SettlementManager manager)
        {
            ThrowIfDisposed();
            if (manager == null) throw new ArgumentNullException(nameof(manager));
            if (installerRegistryProvider == null || persistentEffectProjectionProvider == null)
                throw new InvalidOperationException("营地阶段玩法组合尚未配置。");
            return new PlayableSettlementActionSession(manager.Data, new PlayableWeaponTrainingContentAdapter(PlayableWeaponMasteryRuntime.Catalog), manager.Events, eventInputProvider(), new PlayableSettlementCareContentAdapter(settlementContentCatalog), new PlayableSettlementEquipmentContentAdapter(PlayableSettlementContentRuntime.Items), tabletopInteraction, manager.Workshop, manager.Inventions, workshopCatalog, PlayableSymptomRuntime.Catalog, installerRegistryProvider(), manager.Timeline.ResolveEvent, manager.Timeline, persistentEffectProjection: persistentEffectProjectionProvider(), hunterManagement: manager.HunterMgmt, consumableContent: new PlayableSettlementConsumableContentAdapter(PlayableSettlementContentRuntime.Items));
        }

        internal bool TryActivate(PlayableSettlementRuntime runtime, out string reason)
        {
            ThrowIfDisposed();
            if (!ReferenceEquals(currentRuntimeProvider(), runtime) || !runtime.IsCurrent)
            {
                reason = "营地运行世代不是当前权威，无法启动 ActionSession。";
                return false;
            }
            if (actionSessionOwner != null && !ReferenceEquals(actionSessionOwner, runtime))
                Deactivate(actionSessionOwner);
            if (actionSession?.IsActive == true)
            {
                reason = string.Empty;
                return true;
            }
            if (configuration == null)
            {
                reason = "营地运行态组合配置尚未安装。";
                return false;
            }

            PlayableSettlementActionSession candidate = null;
            try
            {
                candidate = configuration.CreateActionSession(runtime.Manager);
                if (candidate == null)
                {
                    reason = "营地 ActionSession 工厂返回空结果。";
                    return false;
                }
                actionSession = candidate;
                actionSessionOwner = runtime;
                reason = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                candidate?.Dispose();
                reason = $"营地 ActionSession 初始化异常：{exception.Message}";
                return false;
            }
        }

        internal void Deactivate(IPlayableSettlementRuntime runtime)
        {
            if (disposed || !ReferenceEquals(actionSessionOwner, runtime)) return;
            CancelEventRunner();
            PlayableSettlementActionSession stale = actionSession;
            actionSession = null;
            actionSessionOwner = null;
            stale?.Dispose();
        }

        internal void EnsureTable(SettlementManager manager)
        {
            ThrowIfDisposed();
            if (!IsCurrentManager(manager)) return;
            if (settlementTable == null)
            {
                if (settlementRoot == null) return;
                var tableObject = new GameObject("SettlementTable3D");
                tableObject.transform.SetParent(settlementRoot.transform, false);
                settlementTable = tableObject.AddComponent<SettlementTable3D>();
            }

            settlementTable.OnHunterClicked = hunter =>
            {
                if (IsCurrentManager(manager)) settlementUI?.ShowHunterDetail(hunter);
            };
            settlementTable.OnEquipRequested = (hunterId, item) => Equip(manager, hunterId, item);
            settlementTable.OnUnequipRequested = (hunterId, equipmentInstanceId) => Unequip(manager, hunterId, equipmentInstanceId);
            settlementTable.OnConsumableRequested = (hunterId, item, bodyPart) => UseConsumable(manager, hunterId, item, bodyPart);
            settlementTable.OnCraftRequested = recipe => Craft(manager, recipe);
            settlementTable.OnInventionUnlockRequested = invention => UnlockInvention(manager, invention);
            settlementTable.OnInventionEffectRequested = (invention, effect) => ActivateInventionEffect(manager, invention, effect);
            settlementTable.OnWorkshopConstructionRequested = definition => BuildWorkshop(manager, definition);
            settlementTable.OnRecoveryRequested = (hunterId, bodyPart) => Recover(manager, hunterId, bodyPart);
            settlementTable.OnRecruitRequested = (template, requestedName) => Recruit(manager, template, requestedName);
            settlementTable.OnGrowthRequested = (hunterId, choice) => SpendGrowth(manager, hunterId, choice);
            settlementTable.OnWeaponTrainingRequested = (hunterId, masteryId) => TrainWeapon(manager, hunterId, masteryId);
            settlementTable.OnSymptomRequested = (hunterId, symptomId, choice) => ResolveSymptom(manager, hunterId, symptomId, choice);
            settlementTable.OnDepartureRequested = squad =>
            {
                if (IsCurrentManager(manager)) departureRequested?.Invoke(squad);
            };
            settlementTable.Init(manager, workshopCatalog, settlementContentCatalog);
        }

        internal void EnsurePresentation(SettlementManager manager)
        {
            ThrowIfDisposed();
            if (!IsCurrentManager(manager)) return;
            if (!ReferenceEquals(presentationManager, manager))
            {
                if (settlementUI == null)
                {
                    if (!missingUIWarningLogged)
                    {
                        missingUIWarningLogged = true;
                        Debug.LogWarning("[SettlementPhase] 未配置 SettlementUIManager，将保留 3D 营地桌面与外部流程控件。");
                    }
                }
                else
                    settlementUI.Init(manager);
                presentationManager = manager;
            }
            EnsureTable(manager);
        }

        internal void Refresh()
        {
            if (!HasCurrentSession()) return;
            settlementUI?.Refresh();
            settlementTable?.Refresh();
        }

        internal void RefreshCards()
        {
            if (!HasCurrentSession()) return;
            settlementUI?.Refresh();
            settlementTable?.RefreshCards();
        }

        internal void RefreshCrafting()
        {
            if (!HasCurrentSession()) return;
            settlementUI?.Refresh();
            settlementTable?.RefreshCrafting();
        }

        internal bool QueueEvents(IPlayableSettlementRuntime runtime, PlayableSettlementActionSession session, IReadOnlyList<SettlementEventWork> works, SettlementEventRestoreProjection restoreProjection = null, string restoredChainId = null)
        {
            if (works == null || works.Count == 0) return false;
            if (!TryBeginEventRunner(runtime, session, restoreProjection, out long runnerId, out CancellationToken cancellationToken, out string reason))
            {
                FailRejectedProjection(restoreProjection, reason);
                Debug.LogError($"[SettlementPhase] {reason}");
                return false;
            }
            ResolveEventsAsync(runtime, session, works, restoreProjection, restoredChainId, runnerId, cancellationToken).Forget();
            return true;
        }

        internal UniTask<bool> ResolveEventsAsync(IPlayableSettlementRuntime runtime, PlayableSettlementActionSession session, IReadOnlyList<SettlementEventWork> works, SettlementEventRestoreProjection restoreProjection = null, string restoredChainId = null)
        {
            if (works == null || works.Count == 0)
                return UniTask.FromResult(true);
            if (!TryBeginEventRunner(runtime, session, restoreProjection, out long runnerId, out CancellationToken cancellationToken, out string reason))
            {
                FailRejectedProjection(restoreProjection, reason);
                Debug.LogError($"[SettlementPhase] {reason}");
                return UniTask.FromResult(false);
            }
            return ResolveEventsAsync(runtime, session, works, restoreProjection, restoredChainId, runnerId, cancellationToken);
        }

        internal void Reset()
        {
            if (disposed) return;
            CancelEventRunner();
            if (actionSessionOwner != null) Deactivate(actionSessionOwner);
        }

        public void Dispose()
        {
            if (disposed) return;
            Reset();
            disposed = true;
            configuration = null;
            settlementTable = null;
            settlementRoot = null;
            settlementUI = null;
            departureRequested = null;
            presentationManager = null;
            eventInputProvider = null;
            tabletopInteraction = null;
            installerRegistryProvider = null;
            persistentEffectProjectionProvider = null;
        }

        internal PlayableSettlementActionSession GetSession(IPlayableSettlementRuntime runtime)
        {
            return runtime != null && ReferenceEquals(actionSessionOwner, runtime) && actionSession?.IsActive == true ? actionSession : null;
        }

        private bool IsCurrentSession(IPlayableSettlementRuntime runtime, PlayableSettlementActionSession session)
        {
            return session != null && ReferenceEquals(GetSession(runtime), session);
        }

        private bool IsCurrentManager(SettlementManager manager)
        {
            if (disposed) return false;
            IPlayableSettlementRuntime runtime = currentRuntimeProvider();
            return runtime?.Manager == manager && ReferenceEquals(actionSessionOwner, runtime) && actionSession?.IsActive == true;
        }

        private bool HasCurrentSession() => IsCurrentManager(currentRuntimeProvider()?.Manager);

        private bool TryBeginEventRunner(IPlayableSettlementRuntime runtime, PlayableSettlementActionSession session, SettlementEventRestoreProjection restoreProjection, out long runnerId, out CancellationToken cancellationToken, out string reason)
        {
            runnerId = 0;
            cancellationToken = default;
            if (!IsCurrentSession(runtime, session))
            {
                reason = "营地事件请求不属于当前运行世代或活动 Session。";
                return false;
            }
            if (eventRunnerCancellation != null)
            {
                reason = "拒绝并行执行营地事件链。";
                return false;
            }
            eventRunnerCancellation = new CancellationTokenSource();
            eventRunnerProjection = restoreProjection;
            runnerId = ++eventRunnerId;
            cancellationToken = eventRunnerCancellation.Token;
            reason = string.Empty;
            return true;
        }

        private void FailRejectedProjection(SettlementEventRestoreProjection restoreProjection, string reason)
        {
            if (restoreProjection == null || ReferenceEquals(eventRunnerProjection, restoreProjection)) return;
            restoreProjection.Fail(reason);
        }

        private async UniTask<bool> ResolveEventsAsync(IPlayableSettlementRuntime runtime, PlayableSettlementActionSession session, IReadOnlyList<SettlementEventWork> works, SettlementEventRestoreProjection restoreProjection, string restoredChainId, long runnerId, CancellationToken cancellationToken)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                SettlementEventCommandResult result = await session.ResolveEventsAsync(works, restoredChainId);
                if (!IsCurrentEventRunner(runtime, session, runnerId, cancellationToken)) return false;
                if (restoreProjection != null)
                {
                    bool restoreCompleted = restoreProjection.Complete(result.Succeeded);
                    if (result.Succeeded && !restoreCompleted && restoreProjection.HasRecoverableCheckpoint)
                    {
                        SettlementEventRestorePlan nextRestorePlan = restoreProjection.Prepare();
                        if (!nextRestorePlan.Succeeded)
                            Debug.LogError($"[SettlementPhase] 下一条营地事件链恢复失败：{nextRestorePlan.FailureReason}");
                        else if (nextRestorePlan.HasPendingEvents)
                            return await ResolveEventsAsync(runtime, session, nextRestorePlan.WorkItems, restoreProjection, nextRestorePlan.ChainId, runnerId, cancellationToken);
                    }
                }
                if (!result.Succeeded)
                    Debug.LogWarning($"[SettlementPhase] 营地事件链未完成：{result.Reason}");
                return result.Succeeded;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (Exception exception)
            {
                if (IsCurrentEventRunner(runtime, session, runnerId, cancellationToken))
                {
                    restoreProjection?.Fail($"营地事件恢复异常：{exception.Message}");
                    Debug.LogError($"[SettlementPhase] 营地事件链执行异常：{exception}");
                }
                return false;
            }
            finally
            {
                if (eventRunnerId == runnerId)
                    CancelEventRunner();
            }
        }

        private bool IsCurrentEventRunner(IPlayableSettlementRuntime runtime, PlayableSettlementActionSession session, long runnerId, CancellationToken cancellationToken)
        {
            return eventRunnerId == runnerId && !cancellationToken.IsCancellationRequested && IsCurrentSession(runtime, session);
        }

        private void CancelEventRunner()
        {
            eventRunnerId++;
            CancellationTokenSource cancellation = eventRunnerCancellation;
            eventRunnerCancellation = null;
            eventRunnerProjection = null;
            cancellation?.Cancel();
            cancellation?.Dispose();
        }

        private PlayableSettlementActionSession GetSession(SettlementManager manager)
        {
            IPlayableSettlementRuntime runtime = currentRuntimeProvider();
            return runtime?.Manager == manager ? GetSession(runtime) : null;
        }

        private UniTask<SettlementEquipmentCommandResult> Equip(SettlementManager manager, int hunterId, ItemData item)
        {
            PlayableSettlementActionSession session = GetSession(manager);
            return session != null ? session.EquipItemAsync(hunterId, item) : UniTask.FromResult(SettlementEquipmentCommandResult.Failed("当前不在营地阶段。"));
        }

        private UniTask<SettlementEquipmentCommandResult> Unequip(SettlementManager manager, int hunterId, int equipmentInstanceId)
        {
            PlayableSettlementActionSession session = GetSession(manager);
            return session != null ? session.UnequipItemAsync(hunterId, equipmentInstanceId) : UniTask.FromResult(SettlementEquipmentCommandResult.Failed("当前不在营地阶段。"));
        }

        private UniTask<SettlementConsumableCommandResult> UseConsumable(SettlementManager manager, int hunterId, ItemData item, HunterBodyPart bodyPart)
        {
            PlayableSettlementActionSession session = GetSession(manager);
            return session != null ? session.UseConsumableAsync(hunterId, item, bodyPart) : UniTask.FromResult(SettlementConsumableCommandResult.Failed("当前不在营地阶段。"));
        }

        private UniTask<SettlementCraftCommandResult> Craft(SettlementManager manager, CraftRecipe recipe)
        {
            PlayableSettlementActionSession session = GetSession(manager);
            return session != null ? session.CraftAsync(recipe) : UniTask.FromResult(SettlementCraftCommandResult.Failed("当前不在营地阶段。"));
        }

        private UniTask<SettlementInventionCommandResult> UnlockInvention(SettlementManager manager, InventionData invention)
        {
            PlayableSettlementActionSession session = GetSession(manager);
            return session != null ? session.UnlockInventionAsync(invention) : UniTask.FromResult(SettlementInventionCommandResult.Failed("当前不在营地阶段。"));
        }

        private UniTask<SettlementInventionActiveEffectCommandResult> ActivateInventionEffect(SettlementManager manager, InventionData invention, InventionActiveEffect effect)
        {
            PlayableSettlementActionSession session = GetSession(manager);
            return session != null ? session.ActivateInventionEffectAsync(invention, effect) : UniTask.FromResult(SettlementInventionActiveEffectCommandResult.Failed("当前不在营地阶段。"));
        }

        private UniTask<SettlementWorkshopConstructionResult> BuildWorkshop(SettlementManager manager, PlayableWorkshopDefinition definition)
        {
            PlayableSettlementActionSession session = GetSession(manager);
            return session != null ? session.BuildWorkshopAsync(definition) : UniTask.FromResult(SettlementWorkshopConstructionResult.Failed("当前不在营地阶段。"));
        }

        private UniTask<RecoverHunterCommandResult> Recover(SettlementManager manager, int hunterId, HunterBodyPart bodyPart)
        {
            PlayableSettlementActionSession session = GetSession(manager);
            return session != null ? session.RecoverHunterAsync(hunterId, bodyPart) : UniTask.FromResult(RecoverHunterCommandResult.Failed("当前不在营地阶段。"));
        }

        private UniTask<RecruitHunterCommandResult> Recruit(SettlementManager manager, HunterData template, string requestedName)
        {
            PlayableSettlementActionSession session = GetSession(manager);
            return session != null ? session.RecruitHunterAsync(template, requestedName) : UniTask.FromResult(RecruitHunterCommandResult.Failed("当前不在营地阶段。"));
        }

        private UniTask<HunterGrowthCommandResult> SpendGrowth(SettlementManager manager, int hunterId, HunterGrowthChoice choice)
        {
            PlayableSettlementActionSession session = GetSession(manager);
            return session != null ? session.SpendHunterGrowthAsync(hunterId, choice) : UniTask.FromResult(HunterGrowthCommandResult.Failed("当前不在营地阶段。"));
        }

        private UniTask<WeaponTrainingCommandResult> TrainWeapon(SettlementManager manager, int hunterId, string masteryId)
        {
            PlayableSettlementActionSession session = GetSession(manager);
            return session != null ? session.TrainWeaponAsync(hunterId, masteryId) : UniTask.FromResult(WeaponTrainingCommandResult.Failed("当前不在营地阶段。"));
        }

        private UniTask<HunterSymptomCommandResult> ResolveSymptom(SettlementManager manager, int hunterId, string symptomId, SymptomResolutionChoice choice)
        {
            PlayableSettlementActionSession session = GetSession(manager);
            return session != null ? session.ResolveHunterSymptomAsync(hunterId, symptomId, choice) : UniTask.FromResult(HunterSymptomCommandResult.Failed("当前不在营地阶段。"));
        }

        private void ThrowIfDisposed()
        {
            if (disposed) throw new ObjectDisposedException(nameof(PlayableSettlementPhaseCoordinator));
        }
    }
}
