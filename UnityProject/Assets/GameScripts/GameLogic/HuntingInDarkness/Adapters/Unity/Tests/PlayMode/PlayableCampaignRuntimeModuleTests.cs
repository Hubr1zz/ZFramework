using System;
using Core;
using GameplayBase;
using NUnit.Framework;

namespace HuntingInDarkness.Adapter.PlayModeTests
{
    public sealed class PlayableCampaignRuntimeModuleTests
    {
        private ICampaignPhaseRuntime runtime;

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
            runtime = module.AcquirePhaseRuntime(null);
            long firstGeneration = runtime.GenerationId;

            Assert.Throws<InvalidOperationException>(() => module.AcquirePhaseRuntime(null));
            runtime.Start(GamePhase.Hunt);
            Assert.That(runtime.CurrentPhase, Is.EqualTo(GamePhase.Hunt));

            runtime.Dispose();
            runtime = module.AcquirePhaseRuntime(null);

            Assert.That(runtime.GenerationId, Is.GreaterThan(firstGeneration));
            Assert.That(runtime.IsStarted, Is.False);
            Assert.That(runtime.CurrentPhase, Is.EqualTo(GamePhase.Settlement));
        }

        [Test]
        public void Reset_ReleasesFsmStateButKeepsCurrentLease()
        {
            IPlayableCampaignRuntimeModule module = GameModule.Campaign;
            runtime = module.AcquirePhaseRuntime(null);
            runtime.Start(GamePhase.Hunt);

            runtime.Reset();

            Assert.That(runtime.IsStarted, Is.False);
            Assert.That(runtime.CurrentPhase, Is.EqualTo(GamePhase.Settlement));
            Assert.Throws<InvalidOperationException>(() => module.AcquirePhaseRuntime(null));
        }
    }
}
