using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using NUnit.Framework;

namespace CardGame.ActionQueue.Tests
{
    public sealed class ActionQueueEngineTests
    {
        [Test]
        public async Task Enqueue_ConcurrentRootsExecuteInFifoOrder()
        {
            var log = new List<string>();
            var releaseFirst = new UniTaskCompletionSource<bool>();
            using var engine = new ActionQueueEngine();

            UniTask<ActionOutcome> first = engine.Enqueue(new WaitingRecordAction("first", log, releaseFirst.Task));
            UniTask<ActionOutcome> second = engine.Enqueue(new RecordAction("second", log));
            Assert.That(log, Is.Empty);

            releaseFirst.TrySetResult(true);
            ActionOutcome firstOutcome = await first;
            ActionOutcome secondOutcome = await second;

            Assert.That(firstOutcome.Status, Is.EqualTo(ActionStatus.Succeeded));
            Assert.That(secondOutcome.Status, Is.EqualTo(ActionStatus.Succeeded));
            Assert.That(log, Is.EqualTo(new[] { "first", "second" }));
        }

        [Test]
        public async Task Reactors_RunByPriorityBeforeActionExecution()
        {
            var log = new List<string>();
            using var engine = new ActionQueueEngine();
            engine.Reactors.RegisterGlobal(new RecordingReactor("low", 0, log));
            engine.Reactors.RegisterGlobal(new RecordingReactor("high", 100, log));

            ActionOutcome outcome = await engine.Enqueue(new RecordAction("action", log));

            Assert.That(outcome.Status, Is.EqualTo(ActionStatus.Succeeded));
            Assert.That(log, Is.EqualTo(new[] { "high", "low", "action" }));
        }

        [Test]
        public async Task Engines_KeepReactorRegistriesIsolated()
        {
            var firstLog = new List<string>();
            var secondLog = new List<string>();
            using var firstEngine = new ActionQueueEngine();
            using var secondEngine = new ActionQueueEngine();
            firstEngine.Reactors.RegisterGlobal(new RecordingReactor("first-reactor", 0, firstLog));

            await firstEngine.Enqueue(new RecordAction("first-action", firstLog));
            await secondEngine.Enqueue(new RecordAction("second-action", secondLog));

            Assert.That(firstLog, Is.EqualTo(new[] { "first-reactor", "first-action" }));
            Assert.That(secondLog, Is.EqualTo(new[] { "second-action" }));
        }

        [Test]
        public async Task StopAndClear_CancelsActiveAndPendingRoots()
        {
            var started = new UniTaskCompletionSource<bool>();
            using var engine = new ActionQueueEngine();
            UniTask<ActionOutcome> active = engine.Enqueue(new CancellationWaitAction(started));
            await started.Task;
            UniTask<ActionOutcome> pending = engine.Enqueue(new RecordAction("pending", new List<string>()));

            engine.StopAndClear();
            ActionOutcome activeOutcome = await active;
            ActionOutcome pendingOutcome = await pending;

            Assert.That(activeOutcome.Status, Is.EqualTo(ActionStatus.Cancelled));
            Assert.That(pendingOutcome.Status, Is.EqualTo(ActionStatus.Cancelled));
            Assert.That(engine.IsRunning, Is.False);
            Assert.That(engine.PendingRootCount, Is.Zero);
        }

        [Test]
        public async Task IndirectReactionLoop_FailsAtConfiguredBudget()
        {
            int executionCount = 0;
            var options = new ActionQueueOptions { MaxActionsPerChain = 4 };
            using var engine = new ActionQueueEngine(options);
            engine.Reactors.RegisterGlobal(new LoopReactor());

            ActionOutcome outcome = await engine.Enqueue(new LoopAction(() => executionCount++));

            Assert.That(outcome.Status, Is.EqualTo(ActionStatus.Failed));
            Assert.That(outcome.Reason, Does.Contain("Loop guard"));
            Assert.That(executionCount, Is.EqualTo(4));
        }

        private class RecordAction : CommandAction
        {
            private readonly string label;
            private readonly List<string> log;

            public RecordAction(string label, List<string> log)
            {
                this.label = label;
                this.log = log;
            }

            protected override UniTask<ActionOutcome> ExecuteAsync(ActionExecutionContext context, CancellationToken cancellationToken)
            {
                log.Add(label);
                return UniTask.FromResult(ActionOutcome.Success());
            }
        }

        private sealed class WaitingRecordAction : RecordAction
        {
            private readonly UniTask<bool> release;

            public WaitingRecordAction(string label, List<string> log, UniTask<bool> release) : base(label, log)
            {
                this.release = release;
            }

            protected override async UniTask<ActionOutcome> ExecuteAsync(ActionExecutionContext context, CancellationToken cancellationToken)
            {
                await release;
                return await base.ExecuteAsync(context, cancellationToken);
            }
        }

        private sealed class CancellationWaitAction : CommandAction
        {
            private readonly UniTaskCompletionSource<bool> started;

            public CancellationWaitAction(UniTaskCompletionSource<bool> started)
            {
                this.started = started;
            }

            protected override async UniTask<ActionOutcome> ExecuteAsync(ActionExecutionContext context, CancellationToken cancellationToken)
            {
                var cancelled = new UniTaskCompletionSource<bool>();
                using CancellationTokenRegistration registration = cancellationToken.Register(() => cancelled.TrySetResult(true));
                started.TrySetResult(true);
                await cancelled.Task;
                cancellationToken.ThrowIfCancellationRequested();
                return ActionOutcome.Success();
            }
        }

        private sealed class RecordingReactor : GameActionReactor<RecordAction>
        {
            private readonly string label;
            private readonly int priority;
            private readonly List<string> log;

            public RecordingReactor(string label, int priority, List<string> log)
            {
                this.label = label;
                this.priority = priority;
                this.log = log;
            }

            public override ReactionTiming Timing => ReactionTiming.BeforeExecution;
            public override int Priority => priority;

            protected override void React(RecordAction action, ReactionContext context, ReactionResponse response)
            {
                log.Add(label);
            }
        }

        private sealed class LoopAction : CommandAction
        {
            private readonly Action onExecute;

            public LoopAction(Action onExecute)
            {
                this.onExecute = onExecute;
            }

            public Action OnExecute => onExecute;

            protected override UniTask<ActionOutcome> ExecuteAsync(ActionExecutionContext context, CancellationToken cancellationToken)
            {
                onExecute();
                return UniTask.FromResult(ActionOutcome.Success());
            }
        }

        private sealed class LoopReactor : GameActionReactor<LoopAction>
        {
            public override ReactionTiming Timing => ReactionTiming.AfterResolved;

            protected override void React(LoopAction action, ReactionContext context, ReactionResponse response)
            {
                response.EnqueueImmediate(new LoopAction(action.OnExecute), "test-loop");
            }
        }
    }
}
