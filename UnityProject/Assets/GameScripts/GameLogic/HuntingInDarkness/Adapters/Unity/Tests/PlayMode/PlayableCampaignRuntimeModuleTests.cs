using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Core;
using Cysharp.Threading.Tasks;
using GameplayBase;
using HuntingInDarkness.ActionFlow;
using HuntingInDarkness.ActionFlow.Campaign;
using HuntingInDarkness.ActionFlow.Events;
using HuntingInDarkness.ActionFlow.Hunt;
using HuntingInDarkness.ActionFlow.Settlement;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Foundation;
using HuntingInDarkness.GameCore.Hunters;
using HuntingInDarkness.Hunt;
using HuntingInDarkness.Settlement;
using NUnit.Framework;
using UI;
using UnityEngine;
using UnityEngine.TestTools;

namespace HuntingInDarkness.Adapter.PlayModeTests
{
    public sealed class PlayableCampaignRuntimeModuleTests
    {
        private IPlayableCampaignRuntime runtime;
        private GameObject settlementPresentationRoot;

        [TearDown]
        public void TearDown()
        {
            runtime?.Dispose();
            runtime = null;
            if (settlementPresentationRoot != null)
                UnityEngine.Object.DestroyImmediate(settlementPresentationRoot);
            settlementPresentationRoot = null;
        }

        [Test]
        public void PhaseRuntime_IsExclusiveAndAdvancesGenerationAfterRelease()
        {
            IPlayableCampaignRuntimeModule module = GameModule.Campaign;
            runtime = module.AcquireRuntime(new RecordingHost(), null);
            long firstGeneration = runtime.GenerationId;

            Assert.Throws<InvalidOperationException>(() => module.AcquireRuntime(new RecordingHost(), null));
            runtime.Start(GamePhase.Hunt);
            Assert.That(runtime.CurrentPhase, Is.EqualTo(GamePhase.Hunt));

            runtime.Dispose();
            runtime = module.AcquireRuntime(new RecordingHost(), null);

            Assert.That(runtime.GenerationId, Is.GreaterThan(firstGeneration));
            Assert.That(runtime.IsStarted, Is.False);
            Assert.That(runtime.CurrentPhase, Is.EqualTo(GamePhase.Settlement));
        }

        [Test]
        public void Reset_ReleasesFsmStateButKeepsCurrentLease()
        {
            IPlayableCampaignRuntimeModule module = GameModule.Campaign;
            runtime = module.AcquireRuntime(new RecordingHost(), null);
            runtime.Start(GamePhase.Hunt);

            runtime.Reset();

            Assert.That(runtime.IsStarted, Is.False);
            Assert.That(runtime.CurrentPhase, Is.EqualTo(GamePhase.Settlement));
            Assert.Throws<InvalidOperationException>(() => module.AcquireRuntime(new RecordingHost(), null));
        }

        [Test]
        public void PhaseAccess_ExposesNarrowPortsInsteadOfConcreteManagers()
        {
            runtime = GameModule.Campaign.AcquireRuntime(new RecordingHost(), null);
            Type accessType = runtime.GetType().GetInterface("Core.IPlayableCampaignPhasePortAccess", true);
            FieldInfo[] gameManagerFields = typeof(GameManager).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            string[] forbiddenFieldTypes = { "Core.PlayableSettlementPhaseManager", "Core.PlayableSettlementPhaseCoordinator", "Core.PlayableHuntPhaseManager", "Core.PlayableHuntPhaseCoordinator", "Core.PlayableShowdownPhaseManager" };
            Type flowType = typeof(GameManager).Assembly.GetType("Core.CampaignFlowCoordinator", true);
            FieldInfo[] flowFields = flowType.GetFields(BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(accessType, Is.Not.Null);
            Assert.That(accessType.GetProperty("SettlementPhase").PropertyType.Name, Is.EqualTo("IPlayableSettlementPhasePort"));
            Assert.That(accessType.GetProperty("HuntPhase").PropertyType.Name, Is.EqualTo("IPlayableHuntPhasePort"));
            Assert.That(accessType.GetProperty("ShowdownPhase").PropertyType.Name, Is.EqualTo("IPlayableShowdownPhasePort"));
            Assert.That(accessType.GetProperty("SettlementPhase").PropertyType.GetMethod("CreateActionSession"), Is.Null);
            Assert.That(accessType.GetProperty("HuntPhase").PropertyType.GetMethod("CreateManager"), Is.Null);
            Assert.That(accessType.GetProperty("HuntPhase").PropertyType.GetMethod("CreateActionSession"), Is.Null);
            Assert.That(accessType.GetProperty("HuntPhase").PropertyType.GetMethod("Configure").GetParameters().Any(parameter => parameter.ParameterType.IsGenericType && parameter.ParameterType.GetGenericTypeDefinition() == typeof(Func<>) && parameter.ParameterType.GetGenericArguments()[0] == typeof(IPlayableHuntRuntime)), Is.False);
            Assert.That(typeof(IPlayableCampaignRuntime).GetMethod("ConfigureSettlementRuntime"), Is.Null);
            Assert.That(typeof(IPlayableCampaignRuntime).GetMethod("ConfigureHuntRuntime"), Is.Null);
            Assert.That(gameManagerFields.Any(field => forbiddenFieldTypes.Contains(field.FieldType.FullName)), Is.False);
            Assert.That(gameManagerFields.Count(field => field.FieldType == flowType), Is.EqualTo(1));
            Assert.That(typeof(GameManager).GetInterfaces().Any(contract => contract.Name.StartsWith("ICampaign") && contract.Name.EndsWith("Host")), Is.False);
            Assert.That(flowFields.Count(field => typeof(IPlayableCampaignRuntime).IsAssignableFrom(field.FieldType)), Is.EqualTo(1));
            Assert.That(flowType.GetInterfaces().Count(contract => contract.Name.StartsWith("ICampaign") && contract.Name.EndsWith("Host")), Is.GreaterThanOrEqualTo(8));
        }

        [Test]
        public void RuntimeOwnsReusablePhaseManagersAcrossReset()
        {
            runtime = GameModule.Campaign.AcquireRuntime(new RecordingHost(), null);
            object phaseManagers = GetPhaseManagers(runtime);
            ConfigureSettlementRuntime();
            ConfigureHuntRuntime();
            Assert.That(runtime.TryPrepareNewSettlement(out IPlayableSettlementRuntime settlement, out string settlementReason), Is.True, settlementReason);
            Assert.That(runtime.TrySwapSettlement(null, settlement, out string settlementSwapReason), Is.True, settlementSwapReason);
            Assert.That(runtime.TryPrepareNewHunt(settlement, out IPlayableHuntRuntime hunt, out string huntReason), Is.True, huntReason);
            Assert.That(runtime.TrySwapHunt(null, hunt, out string huntSwapReason), Is.True, huntSwapReason);

            runtime.Reset();

            Assert.That(GetManagerCurrent(phaseManagers, "SettlementPhase"), Is.Null);
            Assert.That(GetManagerCurrent(phaseManagers, "HuntPhase"), Is.Null);
            Assert.That(GetManagerCurrent(phaseManagers, "ShowdownPhase"), Is.Null);
            Assert.That(runtime.TryPrepareNewSettlement(out IPlayableSettlementRuntime nextSettlement, out string nextReason), Is.True, nextReason);
            runtime.ReleaseSettlement(nextSettlement);
        }

        [Test]
        public void HuntInitializedStart_ReleasesCandidateWhenContentInitializationFails()
        {
            runtime = GameModule.Campaign.AcquireRuntime(new RecordingHost(), null);
            ConfigureSettlementRuntime();
            ConfigureHuntRuntime();
            Assert.That(runtime.TryPrepareNewSettlement(out IPlayableSettlementRuntime settlement, out string settlementReason), Is.True, settlementReason);
            Assert.That(runtime.TrySwapSettlement(null, settlement, out string settlementSwapReason), Is.True, settlementSwapReason);

            object phaseManagers = GetPhaseManagers(runtime);
            Type accessType = phaseManagers.GetType().GetInterface("Core.IPlayableCampaignPhasePortAccess", true);
            object huntManager = accessType.GetProperty("HuntPhase").GetValue(phaseManagers);
            Type planType = huntManager.GetType().Assembly.GetType("Core.PlayableHuntStartPlan");
            object invalidRoute = Activator.CreateInstance(typeof(PlayableHuntRoutePlan), BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { null, null, "invalid", 1, null, Array.Empty<HexTileData>(), Array.Empty<EventData>(), new Dictionary<string, EventData>(), null }, null);
            object plan = Activator.CreateInstance(planType, BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { new[] { new HunterInstance(null, 901) }, 1, invalidRoute, null }, null);

            Assert.That((bool)InvokeInternalResult(huntManager, "TryPrepareInitialized", settlement, plan, null, null), Is.False);
            FieldInfo runtimesField = huntManager.GetType().GetField("runtimes", BindingFlags.Instance | BindingFlags.NonPublic);
            int runtimeCount = (int)runtimesField.FieldType.GetProperty("Count").GetValue(runtimesField.GetValue(huntManager));
            Assert.That(runtimeCount, Is.EqualTo(0));
            Assert.That(runtime.Hunt, Is.Null);
        }

        [Test]
        public void RuntimeOwnedPhaseManagersRejectUseAfterDispose()
        {
            runtime = GameModule.Campaign.AcquireRuntime(new RecordingHost(), null);
            runtime.Dispose();

            Assert.Throws<ObjectDisposedException>(() => runtime.TryPrepareNewSettlement(out _, out _));
            Assert.Throws<ObjectDisposedException>(() => runtime.TryPrepareNewHunt(null, out _, out _));
            Assert.Throws<ObjectDisposedException>(() => runtime.TryPrepareHuntRestore(null, "disposed", out _, out _));
        }

        private static object GetPhaseManagers(IPlayableCampaignRuntime runtime)
        {
            Type accessType = runtime.GetType().GetInterface("Core.IPlayableCampaignPhasePortAccess", true);
            Assert.That(accessType, Is.Not.Null);
            return runtime;
        }

        private static object GetManagerCurrent(object phaseManagers, string propertyName)
        {
            Type accessType = phaseManagers.GetType().GetInterface("Core.IPlayableCampaignPhasePortAccess", true);
            PropertyInfo property = accessType.GetProperty(propertyName);
            object manager = property.GetValue(phaseManagers);
            return manager.GetType().GetProperty("Current", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(manager);
        }

        [Test]
        public void Reset_ReleasesGameplayShellAndPreservesExternalInstallers()
        {
            IPlayableCampaignRuntimeModule module = GameModule.Campaign;
            runtime = module.AcquireRuntime(new RecordingHost(), null);
            IDisposable externalInstallation = runtime.ActionEnvironmentInstallers.Register(new NoopInstaller());
            runtime.EnsureGameplayRuntime(new NoopInstaller());
            Assert.That(runtime.IsActionSessionActive, Is.True);
            Assert.That(runtime.ActionEnvironmentInstallers.InstallerCount, Is.EqualTo(2));
            Assert.That(runtime.ActionEnvironmentInstallers.AttachedEnvironmentCount, Is.EqualTo(1));

            runtime.Reset();

            Assert.That(runtime.IsActionSessionActive, Is.False);
            Assert.That(runtime.ActionEnvironmentInstallers.InstallerCount, Is.EqualTo(1));
            Assert.That(runtime.ActionEnvironmentInstallers.AttachedEnvironmentCount, Is.Zero);
            externalInstallation.Dispose();
            Assert.That(runtime.ActionEnvironmentInstallers.InstallerCount, Is.Zero);
        }

        [Test]
        public void SettlementCoordinatorOwnsSessionAcrossResetWithoutStaleSession()
        {
            runtime = GameModule.Campaign.AcquireRuntime(new RecordingHost(), null);
            ConfigureSettlementRuntime(new PlayableSettlementRuntimeConfiguration(new RecordingDeparturePort(), manager => new PlayableSettlementActionSession(manager.Data, new PlayableWeaponTrainingContentAdapter(null))));
            Assert.That(runtime.TryPrepareNewSettlement(out IPlayableSettlementRuntime first, out string prepareReason), Is.True, prepareReason);
            Assert.That(runtime.TrySwapSettlement(null, first, out string swapReason), Is.True, swapReason);
            Assert.That(first.TryActivateActionSession(out string activationReason), Is.True, activationReason);
            PlayableSettlementActionSession firstSession = first.ActionSession;
            Assert.That(firstSession, Is.Not.Null);
            Assert.That(firstSession.IsActive, Is.True);
            Assert.That(runtime.TryPrepareNewSettlement(out IPlayableSettlementRuntime second, out prepareReason), Is.True, prepareReason);
            Assert.That(runtime.TrySwapSettlement(first, second, out swapReason), Is.True, swapReason);

            Assert.That(firstSession.IsActive, Is.False);
            Assert.That(first.ActionSession, Is.Null);
            Assert.That(second.TryActivateActionSession(out activationReason), Is.True, activationReason);
            PlayableSettlementActionSession secondSession = second.ActionSession;
            Assert.That(secondSession, Is.Not.SameAs(firstSession));

            runtime.Reset();

            Assert.That(secondSession.IsActive, Is.False);
            Assert.That(second.ActionSession, Is.Null);
        }

        [Test]
        public void SettlementCoordinatorRejectsStaleTableCallbackAfterGenerationSwap()
        {
            int departureCount = 0;
            runtime = GameModule.Campaign.AcquireRuntime(new RecordingHost(), null);
            ConfigureSettlementRuntime(new PlayableSettlementRuntimeConfiguration(new RecordingDeparturePort(), manager => new PlayableSettlementActionSession(manager.Data, new PlayableWeaponTrainingContentAdapter(null))));
            object phaseManagers = GetPhaseManagers(runtime);
            Type accessType = phaseManagers.GetType().GetInterface("Core.IPlayableCampaignPhasePortAccess", true);
            object phaseManager = accessType.GetProperty("SettlementPhase").GetValue(phaseManagers);
            object coordinator = phaseManager.GetType().GetProperty("Coordinator", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(phaseManager);
            settlementPresentationRoot = new GameObject("Settlement Coordinator Test Root");
            SettlementTable3D table = settlementPresentationRoot.AddComponent<SettlementTable3D>();
            InvokeInternal(coordinator, "ConfigurePresentation", table, settlementPresentationRoot, null, null, null, new Action<List<HunterInstance>>(_ => departureCount++));
            Assert.That(runtime.TryPrepareNewSettlement(out IPlayableSettlementRuntime first, out string prepareReason), Is.True, prepareReason);
            Assert.That(runtime.TrySwapSettlement(null, first, out string swapReason), Is.True, swapReason);
            Assert.That(first.TryActivateActionSession(out string activationReason), Is.True, activationReason);
            InvokeInternal(coordinator, "EnsurePresentation", first.Manager);
            Action<List<HunterInstance>> staleCallback = table.OnDepartureRequested;
            staleCallback(new List<HunterInstance>());
            Assert.That(departureCount, Is.EqualTo(1));

            Assert.That(runtime.TryPrepareNewSettlement(out IPlayableSettlementRuntime second, out prepareReason), Is.True, prepareReason);
            Assert.That(runtime.TrySwapSettlement(first, second, out swapReason), Is.True, swapReason);
            staleCallback(new List<HunterInstance>());
            Assert.That(departureCount, Is.EqualTo(1));
            Assert.That(second.TryActivateActionSession(out activationReason), Is.True, activationReason);
            InvokeInternal(coordinator, "EnsurePresentation", second.Manager);
            table.OnDepartureRequested(new List<HunterInstance>());
            Assert.That(departureCount, Is.EqualTo(2));
        }

        private static void InvokeInternal(object target, string methodName, params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"缺少内部方法 {methodName}。");
            method.Invoke(target, arguments);
        }

        private static object InvokeInternalResult(object target, string methodName, params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethods(BindingFlags.Instance | BindingFlags.NonPublic).Single(candidate => candidate.Name == methodName && candidate.GetParameters().Length == arguments.Length);
            return method.Invoke(target, arguments);
        }

        [Test]
        public async Task TransitionWithoutGameplayRuntime_IsRejectedWithoutHostMutation()
        {
            var host = new RecordingHost();
            runtime = GameModule.Campaign.AcquireRuntime(host, null);

            CampaignPhaseTransitionResult result = await runtime.TransitionAsync(GamePhase.Hunt);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(host.CurrentPhase, Is.EqualTo(GamePhase.Settlement));
        }

        [Test]
        public void SettlementEventRestoreCandidate_IsInvisibleUntilPublishedAndClearedByReset()
        {
            runtime = GameModule.Campaign.AcquireRuntime(new RecordingHost(), null);
            ConfigureSettlementRuntime();
            Assert.That(runtime.TryPrepareNewSettlement(out IPlayableSettlementRuntime settlement, out string prepareReason), Is.True, prepareReason);
            Assert.That(runtime.TrySwapSettlement(null, settlement, out string swapReason), Is.True, swapReason);

            SettlementEventRestoreProjection candidate = settlement.CreateEventRestoreCandidate();

            Assert.That(runtime.Settlement.EventRestore, Is.Null);
            settlement.PublishEventRestore(candidate);
            Assert.That(runtime.Settlement.EventRestore, Is.SameAs(candidate));

            runtime.Reset();

            Assert.That(runtime.Settlement, Is.Null);
        }

        [Test]
        public void SettlementEventRestorePublication_RejectsNullCandidate()
        {
            runtime = GameModule.Campaign.AcquireRuntime(new RecordingHost(), null);
            ConfigureSettlementRuntime();
            Assert.That(runtime.TryPrepareNewSettlement(out IPlayableSettlementRuntime settlement, out string prepareReason), Is.True, prepareReason);
            Assert.That(runtime.TrySwapSettlement(null, settlement, out string swapReason), Is.True, swapReason);

            Assert.Throws<ArgumentNullException>(() => settlement.PublishEventRestore(null));
            Assert.That(runtime.Settlement.EventRestore, Is.Null);
        }

        [Test]
        public async Task SettlementCoordinator_ResetCancelsStaleEventRunnerBeforeProjectionContinuation()
        {
            var input = new BlockingEventInput();
            runtime = GameModule.Campaign.AcquireRuntime(new RecordingHost(), null);
            ConfigureSettlementRuntime(new PlayableSettlementRuntimeConfiguration(new RecordingDeparturePort(), manager => new PlayableSettlementActionSession(manager.Data, new PlayableWeaponTrainingContentAdapter(null), new EventSystem(manager.Data, new FirstRandom()), input)));
            object phaseManagers = GetPhaseManagers(runtime);
            Type accessType = phaseManagers.GetType().GetInterface("Core.IPlayableCampaignPhasePortAccess", true);
            object phaseManager = accessType.GetProperty("SettlementPhase").GetValue(phaseManagers);
            object coordinator = phaseManager.GetType().GetProperty("Coordinator", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(phaseManager);
            InvokeInternal(coordinator, "ConfigureGameplay", new Func<IPlayableEventInput>(() => input), null, new Func<IActionEnvironmentInstallerRegistry>(() => runtime.ActionEnvironmentInstallers), new Func<IPlayableCampaignPersistentEffectProjection>(() => null));

            Assert.That(runtime.TryPrepareNewSettlement(out IPlayableSettlementRuntime settlement, out string prepareReason), Is.True, prepareReason);
            Assert.That(runtime.TrySwapSettlement(null, settlement, out string swapReason), Is.True, swapReason);
            Assert.That(settlement.TryActivateActionSession(out string activationReason), Is.True, activationReason);

            EventData gameEvent = ScriptableObject.CreateInstance<EventData>();
            gameEvent.name = "stale-settlement-event";
            gameEvent.eventName = gameEvent.name;
            gameEvent.eventType = GameEventType.Narrative;
            AnnalEntry timelineEntry = new() { EventId = gameEvent.ContentId, EventName = gameEvent.eventName, EntryType = TimelineEntryType.Random };
            settlement.Data.Timeline.Add(timelineEntry);
            var projection = new SettlementEventRestoreProjection(settlement.Data, id => id == gameEvent.ContentId ? gameEvent : null);
            SettlementEventRestorePlan plan = projection.Prepare();
            Assert.That(plan.Succeeded, Is.True, plan.FailureReason);

            PlayableSettlementActionSession session = settlement.ActionSession;
            UniTask<bool> resolve = (UniTask<bool>)InvokeInternalResult(coordinator, "ResolveEventsAsync", settlement, session, plan.WorkItems, projection, plan.ChainId);
            await input.Started.Task;

            LogAssert.Expect(LogType.Error, "[SettlementPhase] 拒绝并行执行营地事件链。");
            Assert.That((bool)InvokeInternalResult(coordinator, "QueueEvents", settlement, session, plan.WorkItems, projection, plan.ChainId), Is.False);
            runtime.Reset();
            Assert.That(await resolve, Is.False);
            Assert.That(timelineEntry.IsCompleted, Is.False);
            Assert.That(projection.IsReady, Is.False);
            UnityEngine.Object.DestroyImmediate(gameEvent);
        }

        [Test]
        public void SettlementSwap_RejectsStaleExpectedAndAllowsRollbackBeforeRelease()
        {
            runtime = GameModule.Campaign.AcquireRuntime(new RecordingHost(), null);
            ConfigureSettlementRuntime();
            Assert.That(runtime.TryPrepareNewSettlement(out IPlayableSettlementRuntime first, out string firstReason), Is.True, firstReason);
            Assert.That(runtime.TryPrepareNewSettlement(out IPlayableSettlementRuntime second, out string secondReason), Is.True, secondReason);
            Assert.That(runtime.TrySwapSettlement(null, first, out string firstSwapReason), Is.True, firstSwapReason);

            Assert.That(runtime.TrySwapSettlement(null, second, out string staleReason), Is.False);
            Assert.That(staleReason, Is.Not.Empty);
            Assert.That(runtime.Settlement, Is.SameAs(first));

            Assert.That(runtime.TrySwapSettlement(first, second, out string secondSwapReason), Is.True, secondSwapReason);
            Assert.That(runtime.TrySwapSettlement(second, first, out string rollbackReason), Is.True, rollbackReason);
            runtime.ReleaseSettlement(second);

            Assert.That(runtime.Settlement, Is.SameAs(first));
            Assert.Throws<InvalidOperationException>(() => runtime.ReleaseSettlement(first));
        }

        [Test]
        public void Reset_DisposesCurrentAndDetachedSettlementGenerations()
        {
            runtime = GameModule.Campaign.AcquireRuntime(new RecordingHost(), null);
            ConfigureSettlementRuntime();
            Assert.That(runtime.TryPrepareNewSettlement(out IPlayableSettlementRuntime current, out string currentReason), Is.True, currentReason);
            Assert.That(runtime.TryPrepareNewSettlement(out IPlayableSettlementRuntime detached, out string detachedReason), Is.True, detachedReason);
            Assert.That(runtime.TrySwapSettlement(null, current, out string swapReason), Is.True, swapReason);

            runtime.Reset();

            Assert.That(runtime.Settlement, Is.Null);
            Assert.Throws<ObjectDisposedException>(() => current.CreateEventRestoreCandidate());
            Assert.Throws<ObjectDisposedException>(() => detached.CreateEventRestoreCandidate());
        }

        [Test]
        public void HuntSwap_RejectsStaleExpectedAndAllowsRollbackBeforeRelease()
        {
            runtime = GameModule.Campaign.AcquireRuntime(new RecordingHost(), null);
            ConfigureSettlementRuntime();
            ConfigureHuntRuntime();
            Assert.That(runtime.TryPrepareNewSettlement(out IPlayableSettlementRuntime settlement, out string settlementReason), Is.True, settlementReason);
            Assert.That(runtime.TrySwapSettlement(null, settlement, out string settlementSwapReason), Is.True, settlementSwapReason);
            Assert.That(runtime.TryPrepareNewHunt(settlement, out IPlayableHuntRuntime first, out string firstReason), Is.True, firstReason);
            Assert.That(runtime.TryPrepareNewHunt(settlement, out IPlayableHuntRuntime second, out string secondReason), Is.True, secondReason);
            Assert.That(runtime.TrySwapHunt(null, first, out string firstSwapReason), Is.True, firstSwapReason);
            Assert.That(first.TryActivateActionSession(null, out string activationReason), Is.False);
            Assert.That(activationReason, Is.Not.Empty);
            Assert.That(first.ActionSession, Is.Null);
            Assert.That(first.Exploration, Is.Null);

            Assert.That(runtime.TrySwapHunt(null, second, out string staleReason), Is.False);
            Assert.That(staleReason, Is.Not.Empty);
            Assert.That(runtime.Hunt, Is.SameAs(first));
            Assert.That(runtime.TrySwapHunt(first, second, out string secondSwapReason), Is.True, secondSwapReason);
            Assert.That(runtime.TrySwapHunt(second, first, out string rollbackReason), Is.True, rollbackReason);
            runtime.ReleaseHunt(second);

            Assert.That(runtime.Hunt, Is.SameAs(first));
            Assert.That(first.ExpeditionId, Is.Not.Empty);
            Assert.Throws<InvalidOperationException>(() => runtime.ReleaseHunt(first));
        }

        [Test]
        public void HuntNoiseLease_RequiresCanonicalIdentityAndIsIdempotent()
        {
            runtime = GameModule.Campaign.AcquireRuntime(new RecordingHost(), null);
            ConfigureSettlementRuntime();
            runtime.ConfigurePersistentEffectProjection(registry => new HuntNoiseLeaseProjection(registry));
            Assert.That(runtime.TryPrepareNewSettlement(out IPlayableSettlementRuntime settlement, out string settlementReason), Is.True, settlementReason);
            var lease = new PendingHuntNoiseLease { SchemaVersion = PendingHuntNoiseLease.CurrentSchemaVersion, SourceEventId = "stone_vigil_risk", LeaseId = "hunt-noise:stone_vigil_risk", NoiseModifier = 2 };
            settlement.Data.PendingHuntNoiseLease = lease;
            Assert.That(runtime.TrySwapSettlement(null, settlement, out string settlementSwapReason), Is.True, settlementSwapReason);
            Assert.That(runtime.ActionEnvironmentInstallers.InstallerCount, Is.EqualTo(1));
            Assert.That(runtime.PersistentEffectProjection.TrySynchronize(settlement.Data, out string repeatReason), Is.True, repeatReason);
            Assert.That(runtime.ActionEnvironmentInstallers.InstallerCount, Is.EqualTo(1));

            settlement.Data.PendingHuntNoiseLease = new PendingHuntNoiseLease { SchemaVersion = lease.SchemaVersion, SourceEventId = lease.SourceEventId, LeaseId = lease.LeaseId, NoiseModifier = 3 };
            Assert.That(runtime.PersistentEffectProjection.TrySynchronize(settlement.Data, out string conflictReason), Is.True, conflictReason);
            Assert.That(runtime.ActionEnvironmentInstallers.InstallerCount, Is.EqualTo(1));
            Assert.That(runtime.PersistentEffectProjection.TrySynchronize(new SettlementInstance { PendingHuntNoiseLease = new PendingHuntNoiseLease { SchemaVersion = lease.SchemaVersion, SourceEventId = lease.SourceEventId, LeaseId = "wrong", NoiseModifier = lease.NoiseModifier } }, out string idReason), Is.False);
            Assert.That(runtime.PersistentEffectProjection.TrySynchronize(new SettlementInstance { PendingHuntNoiseLease = new PendingHuntNoiseLease { SchemaVersion = 0, SourceEventId = lease.SourceEventId, LeaseId = lease.LeaseId, NoiseModifier = lease.NoiseModifier } }, out string versionReason), Is.False);
            Assert.That(runtime.PersistentEffectProjection.TryClear(settlement.Data, out string clearReason), Is.True, clearReason);
            Assert.That(settlement.Data.PendingHuntNoiseLease, Is.Null);
            Assert.That(runtime.ActionEnvironmentInstallers.InstallerCount, Is.Zero);
        }

        [Test]
        public void HuntNoiseProjection_ReattachesAfterSessionDisposeAndResetsWithCampaign()
        {
            runtime = GameModule.Campaign.AcquireRuntime(new RecordingHost(), null);
            ConfigureSettlementRuntime();
            runtime.ConfigurePersistentEffectProjection(registry => new HuntNoiseLeaseProjection(registry));
            ConfigureHuntRuntime(new PlayableHuntRuntimeConfiguration(settlement => new HuntManager(settlement.Events, bindInitialContent: false), (manager, _) => new PlayableHuntActionSession(manager, installerRegistry: runtime.ActionEnvironmentInstallers)));
            Assert.That(runtime.TryPrepareNewSettlement(out IPlayableSettlementRuntime settlement, out string settlementReason), Is.True, settlementReason);
            settlement.Data.PendingHuntNoiseLease = new PendingHuntNoiseLease { LeaseId = "hunt-noise:stone_vigil_risk", SourceEventId = "stone_vigil_risk", NoiseModifier = 2 };
            Assert.That(runtime.TrySwapSettlement(null, settlement, out string settlementSwapReason), Is.True, settlementSwapReason);
            Assert.That(runtime.TryPrepareNewHunt(settlement, out IPlayableHuntRuntime hunt, out string huntReason), Is.True, huntReason);
            Assert.That(runtime.TrySwapHunt(null, hunt, out string huntSwapReason), Is.True, huntSwapReason);
            Assert.That(hunt.TryActivateActionSession(null, out string activationReason), Is.True, activationReason);
            Assert.That(runtime.ActionEnvironmentInstallers.AttachedEnvironmentCount, Is.EqualTo(1));

            hunt.DeactivateActionSession();
            Assert.That(runtime.ActionEnvironmentInstallers.AttachedEnvironmentCount, Is.Zero);
            Assert.That(hunt.TryActivateActionSession(null, out activationReason), Is.True, activationReason);
            Assert.That(runtime.ActionEnvironmentInstallers.AttachedEnvironmentCount, Is.EqualTo(1));

            runtime.Reset();
            Assert.That(runtime.ActionEnvironmentInstallers.InstallerCount, Is.Zero);
            Assert.That(runtime.ActionEnvironmentInstallers.AttachedEnvironmentCount, Is.Zero);
        }

        [Test]
        public void Reset_DisposesCurrentAndDetachedHuntGenerations()
        {
            runtime = GameModule.Campaign.AcquireRuntime(new RecordingHost(), null);
            ConfigureSettlementRuntime();
            ConfigureHuntRuntime();
            Assert.That(runtime.TryPrepareNewSettlement(out IPlayableSettlementRuntime settlement, out string settlementReason), Is.True, settlementReason);
            Assert.That(runtime.TrySwapSettlement(null, settlement, out string settlementSwapReason), Is.True, settlementSwapReason);
            Assert.That(runtime.TryPrepareNewHunt(settlement, out IPlayableHuntRuntime current, out string currentReason), Is.True, currentReason);
            Assert.That(runtime.TryPrepareHuntRestore(settlement, "restored-expedition", out IPlayableHuntRuntime detached, out string detachedReason), Is.True, detachedReason);
            Assert.That(runtime.TrySwapHunt(null, current, out string swapReason), Is.True, swapReason);

            runtime.Reset();

            Assert.That(runtime.Hunt, Is.Null);
            Assert.Throws<ObjectDisposedException>(() => current.TryActivateActionSession(null, out _));
            Assert.Throws<ObjectDisposedException>(() => detached.TryActivateActionSession(null, out _));
        }

        private void ConfigureSettlementRuntime() => ConfigureSettlementRuntime(new PlayableSettlementRuntimeConfiguration(new RecordingDeparturePort(), _ => null));

        private void ConfigureSettlementRuntime(PlayableSettlementRuntimeConfiguration configuration)
        {
            object phaseManagers = GetPhaseManagers(runtime);
            Type accessType = phaseManagers.GetType().GetInterface("Core.IPlayableCampaignPhasePortAccess", true);
            object phase = accessType.GetProperty("SettlementPhase").GetValue(phaseManagers);
            InvokeInternal(phase, "Configure", configuration);
        }

        private void ConfigureHuntRuntime() => ConfigureHuntRuntime(new PlayableHuntRuntimeConfiguration(settlement => new HuntManager(settlement.Events, bindInitialContent: false), (_, _) => null));

        private void ConfigureHuntRuntime(PlayableHuntRuntimeConfiguration configuration)
        {
            object phaseManagers = GetPhaseManagers(runtime);
            Type accessType = phaseManagers.GetType().GetInterface("Core.IPlayableCampaignPhasePortAccess", true);
            object phase = accessType.GetProperty("HuntPhase").GetValue(phaseManagers);
            InvokeInternal(phase, "Configure", configuration);
        }

        private sealed class RecordingHost : ICampaignPhaseTransitionHost
        {
            public GamePhase CurrentPhase { get; private set; } = GamePhase.Settlement;

            public bool TryApplyPhaseTransition(GamePhase targetPhase, out string reason)
            {
                CurrentPhase = targetPhase;
                reason = string.Empty;
                return true;
            }

            public bool TryBeginEncounter(CampaignEncounterRequest request, out string reason)
            {
                CurrentPhase = GamePhase.BossFight;
                reason = string.Empty;
                return true;
            }
        }

        private sealed class NoopInstaller : IActionEnvironmentInstaller
        {
            public bool Supports(ActionEnvironmentKind kind) => true;

            public void Install(IActionEnvironment environment, ActionEnvironmentInstallation installation)
            {
            }
        }

        private sealed class RecordingDeparturePort : ISettlementDepartureRequestPort
        {
            public bool RequestDeparture(IReadOnlyList<int> hunterIds) => true;
        }

        private sealed class FirstRandom : IRandomSource
        {
            public int Next(int minInclusive, int maxExclusive) => minInclusive;
            public double NextDouble() => 0d;
        }

        private sealed class BlockingEventInput : IPlayableEventInput
        {
            private readonly UniTaskCompletionSource<bool> completion = new();
            public UniTaskCompletionSource<bool> Started { get; } = new();

            public async UniTask ConfirmNarrativeAsync(EventData gameEvent, HunterInstance actor, CancellationToken cancellationToken)
            {
                Started.TrySetResult(true);
                using CancellationTokenRegistration registration = cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken));
                await completion.Task;
            }

            public UniTask<PlayableEventChoiceSelection> SelectChoiceAsync(EventData gameEvent, HunterInstance actor, IReadOnlyList<HunterInstance> hunters, IPlayableEventResourceAvailability resourceAvailability, CancellationToken cancellationToken) => UniTask.FromResult(new PlayableEventChoiceSelection(-1, null));
            public UniTask<PlayableEventCheckDecision> PresentCheckAsync(PlayableEventChoiceTransaction transaction, CancellationToken cancellationToken) => UniTask.FromResult(PlayableEventCheckDecision.Accept);
            public UniTask ConfirmResultAsync(EventData gameEvent, EventResolutionResult result, CancellationToken cancellationToken) => UniTask.CompletedTask;
        }
    }
}
