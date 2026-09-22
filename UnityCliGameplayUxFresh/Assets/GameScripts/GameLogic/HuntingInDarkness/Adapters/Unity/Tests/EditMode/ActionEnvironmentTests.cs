using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CardGame.ActionQueue;
using Core;
using Cysharp.Threading.Tasks;
using HuntingInDarkness.ActionFlow;
using NUnit.Framework;

namespace HuntingInDarkness.Adapter.Tests
{
    public sealed class ActionEnvironmentTests
    {
        [Test]
        public void EntityHandles_AreStableWithinEnvironmentAndIsolatedAcrossEnvironments()
        {
            using var settlement = CreateEnvironment(ActionEnvironmentKind.Settlement);
            using var combat = CreateEnvironment(ActionEnvironmentKind.Combat);

            ReactorEntityHandle first = settlement.EntityHandles.GetOrCreate("hunter", "42", "猎人");
            ReactorEntityHandle repeated = settlement.EntityHandles.GetOrCreate("hunter", "42", "不会覆盖名称");
            ReactorEntityHandle otherEnvironment = combat.EntityHandles.GetOrCreate("hunter", "42", "猎人");

            Assert.That(repeated, Is.SameAs(first));
            Assert.That(otherEnvironment, Is.Not.SameAs(first));
            Assert.That(first.ReactorName, Is.EqualTo("猎人"));
        }

        [Test]
        public async Task ExecuteAsync_SuccessPublishesStagedFactsInOrder()
        {
            var received = new List<int>();
            System.Action<TestCommittedEvent> handler = evt => received.Add(evt.Value);
            EventBus.Subscribe(handler);
            try
            {
                using var environment = CreateEnvironment(ActionEnvironmentKind.Settlement);
                var outbox = new ActionEventOutbox();

                ActionOutcome outcome = await environment.ExecuteAsync(new StageEventsAction(outbox, ActionOutcome.Success()), outbox);

                Assert.That(outcome.Status, Is.EqualTo(ActionStatus.Succeeded));
                Assert.That(outbox.State, Is.EqualTo(ActionEventOutboxState.Committed));
                Assert.That(outbox.PendingCount, Is.Zero);
                Assert.That(received, Is.EqualTo(new[] { 1, 2 }));
            }
            finally
            {
                EventBus.Unsubscribe(handler);
            }
        }

        [Test]
        public async Task ExecuteAsync_FailureDiscardsStagedFacts()
        {
            int receivedCount = 0;
            System.Action<TestCommittedEvent> handler = _ => receivedCount++;
            EventBus.Subscribe(handler);
            try
            {
                using var environment = CreateEnvironment(ActionEnvironmentKind.Hunt);
                var outbox = new ActionEventOutbox();

                ActionOutcome outcome = await environment.ExecuteAsync(new StageEventsAction(outbox, ActionOutcome.Failure("test")), outbox);

                Assert.That(outcome.Status, Is.EqualTo(ActionStatus.Failed));
                Assert.That(outbox.State, Is.EqualTo(ActionEventOutboxState.Discarded));
                Assert.That(outbox.PendingCount, Is.Zero);
                Assert.That(receivedCount, Is.Zero);
            }
            finally
            {
                EventBus.Unsubscribe(handler);
            }
        }

        [Test]
        public async Task ExecuteAsync_FailureKeepsPublishedCheckpointButDiscardsLaterFacts()
        {
            var received = new List<int>();
            System.Action<TestCommittedEvent> handler = evt => received.Add(evt.Value);
            EventBus.Subscribe(handler);
            try
            {
                using var environment = CreateEnvironment(ActionEnvironmentKind.Combat);
                var outbox = new ActionEventOutbox();

                ActionOutcome outcome = await environment.ExecuteAsync(new CheckpointThenFailAction(outbox), outbox);

                Assert.That(outcome.Status, Is.EqualTo(ActionStatus.Failed));
                Assert.That(outbox.State, Is.EqualTo(ActionEventOutboxState.Discarded));
                Assert.That(received, Is.EqualTo(new[] { 1 }));
            }
            finally
            {
                EventBus.Unsubscribe(handler);
            }
        }

        [Test]
        public async Task ExecuteAsync_AfterCommitFactsIgnoreCheckpointsAndPublishAfterRoot()
        {
            var received = new List<int>();
            int countAtCheckpoint = -1;
            System.Action<TestCommittedEvent> handler = evt => received.Add(evt.Value);
            EventBus.Subscribe(handler);
            try
            {
                using var environment = CreateEnvironment(ActionEnvironmentKind.Combat);
                var outbox = new ActionEventOutbox();

                ActionOutcome outcome = await environment.ExecuteAsync(new CheckpointThenCommitAction(outbox, () => countAtCheckpoint = received.Count), outbox);

                Assert.That(outcome.IsSuccess, Is.True);
                Assert.That(countAtCheckpoint, Is.EqualTo(1));
                Assert.That(received, Is.EqualTo(new[] { 1, 2 }));
                Assert.That(outbox.State, Is.EqualTo(ActionEventOutboxState.Committed));
            }
            finally
            {
                EventBus.Unsubscribe(handler);
            }
        }

        [Test]
        public async Task ExecuteAsync_FailureDiscardsAfterCommitFacts()
        {
            int receivedCount = 0;
            System.Action<TestCommittedEvent> handler = _ => receivedCount++;
            EventBus.Subscribe(handler);
            try
            {
                using var environment = CreateEnvironment(ActionEnvironmentKind.Campaign);
                var outbox = new ActionEventOutbox();

                ActionOutcome outcome = await environment.ExecuteAsync(new AfterCommitThenFailAction(outbox), outbox);

                Assert.That(outcome.Status, Is.EqualTo(ActionStatus.Failed));
                Assert.That(receivedCount, Is.Zero);
                Assert.That(outbox.State, Is.EqualTo(ActionEventOutboxState.Discarded));
            }
            finally
            {
                EventBus.Unsubscribe(handler);
            }
        }

        [Test]
        public async Task ExecuteAsync_AfterCommitHandlerCanDisposeSourceEnvironment()
        {
            var environment = CreateEnvironment(ActionEnvironmentKind.Combat);
            var outbox = new ActionEventOutbox();
            System.Action<TestCommittedEvent> handler = _ => environment.Dispose();
            EventBus.Subscribe(handler);
            try
            {
                ActionOutcome outcome = await environment.ExecuteAsync(new AfterCommitAction(outbox), outbox);

                Assert.That(outcome.IsSuccess, Is.True);
                Assert.That(environment.IsDisposed, Is.True);
                Assert.That(outbox.State, Is.EqualTo(ActionEventOutboxState.Committed));
            }
            finally
            {
                EventBus.Unsubscribe(handler);
                environment.Dispose();
            }
        }

        [Test]
        public async Task Dispose_CancelsActiveRootAndDiscardsFacts()
        {
            int receivedCount = 0;
            System.Action<TestCommittedEvent> handler = _ => receivedCount++;
            EventBus.Subscribe(handler);
            try
            {
                var environment = CreateEnvironment(ActionEnvironmentKind.Combat);
                var outbox = new ActionEventOutbox();
                var started = new UniTaskCompletionSource<bool>();
                UniTask<ActionOutcome> execution = environment.ExecuteAsync(new StagedCancellationWaitAction(outbox, started), outbox);
                await started.Task;

                environment.Dispose();
                ActionOutcome outcome = await execution;

                Assert.That(outcome.Status, Is.EqualTo(ActionStatus.Cancelled));
                Assert.That(outbox.State, Is.EqualTo(ActionEventOutboxState.Discarded));
                Assert.That(receivedCount, Is.Zero);
                Assert.That(environment.IsDisposed, Is.True);
                Assert.That(environment.EntityHandles.Count, Is.Zero);
                Assert.Throws<System.ObjectDisposedException>(() => environment.EntityHandles.GetOrCreate("hunter", "1"));
            }
            finally
            {
                EventBus.Unsubscribe(handler);
            }
        }

        [Test]
        public async Task ExecuteAsync_SharedOutboxCannotBelongToTwoRoots()
        {
            using var environment = CreateEnvironment(ActionEnvironmentKind.Campaign);
            var outbox = new ActionEventOutbox();
            var started = new UniTaskCompletionSource<bool>();
            var release = new UniTaskCompletionSource<bool>();
            UniTask<ActionOutcome> first = environment.ExecuteAsync(new DeferredSuccessAction(started, release), outbox);
            await started.Task;

            Assert.ThrowsAsync<System.InvalidOperationException>(async () => await environment.ExecuteAsync(new StageEventsAction(outbox, ActionOutcome.Success()), outbox));

            release.TrySetResult(true);
            ActionOutcome outcome = await first;
            Assert.That(outcome.Status, Is.EqualTo(ActionStatus.Succeeded));
        }

        private static ActionEnvironment CreateEnvironment(ActionEnvironmentKind kind)
        {
            return new ActionEnvironment(new ActionEnvironmentConfiguration
            {
                Name = $"Test {kind}",
                Kind = kind,
                MaxActionsPerChain = 16,
                TraceCapacity = 8,
                SkipPresentationWaits = true
            });
        }

        private readonly struct TestCommittedEvent
        {
            public TestCommittedEvent(int value)
            {
                Value = value;
            }

            public int Value { get; }
        }

        private sealed class StageEventsAction : CommandAction
        {
            private readonly ActionEventOutbox outbox;
            private readonly ActionOutcome outcome;

            public StageEventsAction(ActionEventOutbox outbox, ActionOutcome outcome)
            {
                this.outbox = outbox;
                this.outcome = outcome;
            }

            protected override UniTask<ActionOutcome> ExecuteAsync(ActionExecutionContext context, CancellationToken cancellationToken)
            {
                outbox.Stage(new TestCommittedEvent(1));
                outbox.Stage(new TestCommittedEvent(2));
                return UniTask.FromResult(outcome);
            }
        }

        private sealed class StagedCancellationWaitAction : CommandAction
        {
            private readonly ActionEventOutbox outbox;
            private readonly UniTaskCompletionSource<bool> started;

            public StagedCancellationWaitAction(ActionEventOutbox outbox, UniTaskCompletionSource<bool> started)
            {
                this.outbox = outbox;
                this.started = started;
            }

            protected override async UniTask<ActionOutcome> ExecuteAsync(ActionExecutionContext context, CancellationToken cancellationToken)
            {
                outbox.Stage(new TestCommittedEvent(1));
                var cancelled = new UniTaskCompletionSource<bool>();
                using CancellationTokenRegistration registration = cancellationToken.Register(() => cancelled.TrySetResult(true));
                started.TrySetResult(true);
                await cancelled.Task;
                cancellationToken.ThrowIfCancellationRequested();
                return ActionOutcome.Success();
            }
        }

        private sealed class CheckpointThenFailAction : CommandAction
        {
            private readonly ActionEventOutbox outbox;

            public CheckpointThenFailAction(ActionEventOutbox outbox)
            {
                this.outbox = outbox;
            }

            protected override UniTask<ActionOutcome> ExecuteAsync(ActionExecutionContext context, CancellationToken cancellationToken)
            {
                outbox.Stage(new TestCommittedEvent(1));
                outbox.PublishCheckpoint();
                outbox.Stage(new TestCommittedEvent(2));
                return UniTask.FromResult(ActionOutcome.Failure("test"));
            }
        }

        private sealed class DeferredSuccessAction : CommandAction
        {
            private readonly UniTaskCompletionSource<bool> started;
            private readonly UniTaskCompletionSource<bool> release;

            public DeferredSuccessAction(UniTaskCompletionSource<bool> started, UniTaskCompletionSource<bool> release)
            {
                this.started = started;
                this.release = release;
            }

            protected override async UniTask<ActionOutcome> ExecuteAsync(ActionExecutionContext context, CancellationToken cancellationToken)
            {
                started.TrySetResult(true);
                await release.Task;
                return ActionOutcome.Success();
            }
        }

        private sealed class CheckpointThenCommitAction : CommandAction
        {
            private readonly ActionEventOutbox outbox;
            private readonly System.Action afterCheckpoint;

            public CheckpointThenCommitAction(ActionEventOutbox outbox, System.Action afterCheckpoint)
            {
                this.outbox = outbox;
                this.afterCheckpoint = afterCheckpoint;
            }

            protected override UniTask<ActionOutcome> ExecuteAsync(ActionExecutionContext context, CancellationToken cancellationToken)
            {
                outbox.Stage(new TestCommittedEvent(1));
                outbox.StageAfterCommit(new TestCommittedEvent(2));
                outbox.PublishCheckpoint();
                afterCheckpoint();
                return UniTask.FromResult(ActionOutcome.Success());
            }
        }

        private sealed class AfterCommitThenFailAction : CommandAction
        {
            private readonly ActionEventOutbox outbox;

            public AfterCommitThenFailAction(ActionEventOutbox outbox)
            {
                this.outbox = outbox;
            }

            protected override UniTask<ActionOutcome> ExecuteAsync(ActionExecutionContext context, CancellationToken cancellationToken)
            {
                outbox.StageAfterCommit(new TestCommittedEvent(1));
                return UniTask.FromResult(ActionOutcome.Failure("test"));
            }
        }

        private sealed class AfterCommitAction : CommandAction
        {
            private readonly ActionEventOutbox outbox;

            public AfterCommitAction(ActionEventOutbox outbox)
            {
                this.outbox = outbox;
            }

            protected override UniTask<ActionOutcome> ExecuteAsync(ActionExecutionContext context, CancellationToken cancellationToken)
            {
                outbox.StageAfterCommit(new TestCommittedEvent(1));
                return UniTask.FromResult(ActionOutcome.Success());
            }
        }
    }
}
