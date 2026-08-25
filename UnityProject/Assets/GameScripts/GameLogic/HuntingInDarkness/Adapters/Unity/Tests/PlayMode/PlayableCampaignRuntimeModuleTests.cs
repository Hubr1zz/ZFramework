using System;
using System.Threading.Tasks;
using Core;
using GameplayBase;
using HuntingInDarkness.ActionFlow;
using HuntingInDarkness.ActionFlow.Campaign;
using HuntingInDarkness.ActionFlow.Hunt;
using HuntingInDarkness.Data;
using HuntingInDarkness.Settlement;
using NUnit.Framework;

namespace HuntingInDarkness.Adapter.PlayModeTests
{
    public sealed class PlayableCampaignRuntimeModuleTests
    {
        private IPlayableCampaignRuntime runtime;

        [TearDown]
        public void TearDown()
        {
            runtime?.Dispose();
            runtime = null;
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
            var settlement = new SettlementInstance();

            SettlementEventRestoreProjection candidate = runtime.CreateSettlementEventRestoreCandidate(settlement, _ => null);

            Assert.That(runtime.SettlementEventRestore, Is.Null);
            runtime.PublishSettlementEventRestore(candidate);
            Assert.That(runtime.SettlementEventRestore, Is.SameAs(candidate));

            runtime.Reset();

            Assert.That(runtime.SettlementEventRestore, Is.Null);
        }

        [Test]
        public void SettlementEventRestorePublication_RejectsNullCandidate()
        {
            runtime = GameModule.Campaign.AcquireRuntime(new RecordingHost(), null);

            Assert.Throws<ArgumentNullException>(() => runtime.PublishSettlementEventRestore(null));
            Assert.That(runtime.SettlementEventRestore, Is.Null);
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
    }
}
