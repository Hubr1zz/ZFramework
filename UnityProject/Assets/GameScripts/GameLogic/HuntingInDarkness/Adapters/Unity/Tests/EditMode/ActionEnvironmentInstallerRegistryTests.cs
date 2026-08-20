using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CardGame.ActionQueue;
using Cysharp.Threading.Tasks;
using HuntingInDarkness.ActionFlow;
using NUnit.Framework;

namespace HuntingInDarkness.Adapter.Tests
{
    public sealed class ActionEnvironmentInstallerRegistryTests
    {
        [Test]
        public async Task Installer_FollowsMatchingEnvironmentReplacementAndCanBeRemoved()
        {
            using var registry = new ActionEnvironmentInstallerRegistry();
            using var campaign = CreateEnvironment(ActionEnvironmentKind.Campaign);
            using var settlement = CreateEnvironment(ActionEnvironmentKind.Settlement);
            using IDisposable campaignAttachment = registry.Attach(campaign);
            IDisposable settlementAttachment = registry.Attach(settlement);
            IDisposable installer = registry.Register(new PreventProbeInstaller(ActionEnvironmentKind.Settlement, ActionEnvironmentKind.Hunt));

            ActionOutcome campaignOutcome = await campaign.ExecuteAsync(new ProbeAction());
            ActionOutcome settlementOutcome = await settlement.ExecuteAsync(new ProbeAction());

            Assert.That(campaignOutcome.Status, Is.EqualTo(ActionStatus.Succeeded));
            Assert.That(settlementOutcome.Status, Is.EqualTo(ActionStatus.Prevented));
            Assert.That(registry.AttachedEnvironmentCount, Is.EqualTo(2));

            settlementAttachment.Dispose();
            settlement.Dispose();
            using var hunt = CreateEnvironment(ActionEnvironmentKind.Hunt);
            using IDisposable huntAttachment = registry.Attach(hunt);
            ActionOutcome huntOutcome = await hunt.ExecuteAsync(new ProbeAction());
            Assert.That(huntOutcome.Status, Is.EqualTo(ActionStatus.Prevented));

            installer.Dispose();
            ActionOutcome outcomeAfterRemoval = await hunt.ExecuteAsync(new ProbeAction());
            Assert.That(outcomeAfterRemoval.Status, Is.EqualTo(ActionStatus.Succeeded));
            Assert.That(registry.InstallerCount, Is.Zero);
        }

        [Test]
        public void Register_WhenLaterEnvironmentInstallFails_RollsBackEarlierInstallations()
        {
            using var registry = new ActionEnvironmentInstallerRegistry();
            using var settlement = CreateEnvironment(ActionEnvironmentKind.Settlement);
            using var hunt = CreateEnvironment(ActionEnvironmentKind.Hunt);
            using IDisposable settlementAttachment = registry.Attach(settlement);
            using IDisposable huntAttachment = registry.Attach(hunt);
            var installer = new FailingInstaller(ActionEnvironmentKind.Hunt);

            Assert.Throws<InvalidOperationException>(() => registry.Register(installer));

            Assert.That(installer.InstalledCount, Is.EqualTo(2));
            Assert.That(installer.DisposedCount, Is.EqualTo(2));
            Assert.That(registry.InstallerCount, Is.Zero);
            Assert.That(registry.AttachedEnvironmentCount, Is.EqualTo(2));
        }

        [Test]
        public void Attach_WhenInstallerFails_DoesNotRetainEnvironmentOrPriorLeases()
        {
            using var registry = new ActionEnvironmentInstallerRegistry();
            var first = new TrackingInstaller();
            var failing = new FailingInstaller(ActionEnvironmentKind.Hunt);
            using IDisposable firstRegistration = registry.Register(first);
            using IDisposable failingRegistration = registry.Register(failing);
            using var hunt = CreateEnvironment(ActionEnvironmentKind.Hunt);

            Assert.Throws<InvalidOperationException>(() => registry.Attach(hunt));

            Assert.That(first.InstalledCount, Is.EqualTo(1));
            Assert.That(first.DisposedCount, Is.EqualTo(1));
            Assert.That(failing.DisposedCount, Is.EqualTo(1));
            Assert.That(registry.AttachedEnvironmentCount, Is.Zero);
            Assert.That(registry.InstallerCount, Is.EqualTo(2));
        }

        [Test]
        public void EnvironmentOwnedAttachment_ReleasesInstallationOnEnvironmentDispose()
        {
            using var registry = new ActionEnvironmentInstallerRegistry();
            var installer = new TrackingInstaller();
            using IDisposable installerRegistration = registry.Register(installer);
            var environment = new ActionEnvironment(CreateConfiguration(ActionEnvironmentKind.Settlement), registry);

            Assert.That(registry.AttachedEnvironmentCount, Is.EqualTo(1));
            Assert.That(installer.InstalledCount, Is.EqualTo(1));

            environment.Dispose();

            Assert.That(registry.AttachedEnvironmentCount, Is.Zero);
            Assert.That(installer.DisposedCount, Is.EqualTo(1));
        }

        [Test]
        public void Attach_WhenInstallerDisposesRegistry_RollsBackUnattachedEnvironment()
        {
            var registry = new ActionEnvironmentInstallerRegistry();
            var installer = new RegistryDisposingInstaller(registry);
            registry.Register(installer);
            using var hunt = CreateEnvironment(ActionEnvironmentKind.Hunt);

            Assert.Throws<ObjectDisposedException>(() => registry.Attach(hunt));

            Assert.That(installer.DisposedCount, Is.EqualTo(1));
            Assert.That(registry.AttachedEnvironmentCount, Is.Zero);
            Assert.That(registry.InstallerCount, Is.Zero);
        }

        private static ActionEnvironment CreateEnvironment(ActionEnvironmentKind kind)
        {
            return new ActionEnvironment(CreateConfiguration(kind));
        }

        private static ActionEnvironmentConfiguration CreateConfiguration(ActionEnvironmentKind kind)
        {
            return new ActionEnvironmentConfiguration
            {
                Name = $"Test {kind}",
                Kind = kind,
                MaxActionsPerChain = 16,
                TraceCapacity = 8,
                SkipPresentationWaits = true
            };
        }

        private sealed class ProbeAction : CommandAction
        {
            protected override UniTask<ActionOutcome> ExecuteAsync(ActionExecutionContext context, CancellationToken cancellationToken) => UniTask.FromResult(ActionOutcome.Success());
        }

        private sealed class PreventProbeReactor : GameActionReactor<ProbeAction>
        {
            public override ReactionTiming Timing => ReactionTiming.BeforeExecution;

            protected override void React(ProbeAction action, ReactionContext context, ReactionResponse response) => response.Prevent("installed");
        }

        private sealed class PreventProbeInstaller : IActionEnvironmentInstaller
        {
            private readonly HashSet<ActionEnvironmentKind> supportedKinds;

            public PreventProbeInstaller(params ActionEnvironmentKind[] supportedKinds)
            {
                this.supportedKinds = new HashSet<ActionEnvironmentKind>(supportedKinds);
            }

            public bool Supports(ActionEnvironmentKind kind) => supportedKinds.Contains(kind);

            public void Install(IActionEnvironment environment, ActionEnvironmentInstallation installation)
            {
                installation.Add(environment.Reactors.RegisterGlobal(new PreventProbeReactor()));
            }
        }

        private class TrackingInstaller : IActionEnvironmentInstaller
        {
            public int InstalledCount { get; protected set; }
            public int DisposedCount { get; private set; }

            public virtual bool Supports(ActionEnvironmentKind kind) => true;

            public virtual void Install(IActionEnvironment environment, ActionEnvironmentInstallation installation)
            {
                InstalledCount++;
                installation.Add(new CallbackLease(() => DisposedCount++));
            }
        }

        private sealed class FailingInstaller : TrackingInstaller
        {
            private readonly ActionEnvironmentKind failingKind;

            public FailingInstaller(ActionEnvironmentKind failingKind)
            {
                this.failingKind = failingKind;
            }

            public override void Install(IActionEnvironment environment, ActionEnvironmentInstallation installation)
            {
                base.Install(environment, installation);
                if (environment.Kind == failingKind)
                    throw new InvalidOperationException("expected install failure");
            }
        }

        private sealed class RegistryDisposingInstaller : TrackingInstaller
        {
            private readonly ActionEnvironmentInstallerRegistry registry;

            public RegistryDisposingInstaller(ActionEnvironmentInstallerRegistry registry)
            {
                this.registry = registry;
            }

            public override void Install(IActionEnvironment environment, ActionEnvironmentInstallation installation)
            {
                base.Install(environment, installation);
                registry.Dispose();
            }
        }

        private sealed class CallbackLease : IDisposable
        {
            private readonly Action callback;
            private bool disposed;

            public CallbackLease(Action callback)
            {
                this.callback = callback;
            }

            public void Dispose()
            {
                if (disposed) return;
                disposed = true;
                callback();
            }
        }
    }
}
