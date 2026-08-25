using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CardGame.ActionQueue;
using Core;
using Cysharp.Threading.Tasks;
using GameplayBase;
using HuntingInDarkness.ActionFlow.Campaign;
using HuntingInDarkness.ActionFlow.Hunt;
using NUnit.Framework;
using UnityEngine;

namespace HuntingInDarkness.Adapter.Tests
{
    public sealed class PlayableCampaignActionSessionTests
    {
        [Test]
        public async Task TransitionAsync_SerializesRequestsAndPublishesCommittedFacts()
        {
            var host = new RecordingHost(GamePhase.Settlement);
            using var session = new PlayableCampaignActionSession(host);
            var received = new List<GamePhase>();
            Action<CampaignPhaseTransitionCommittedEvent> handler = evt => received.Add(evt.CurrentPhase);
            EventBus.Subscribe(handler);
            try
            {
                Task<CampaignPhaseTransitionResult> combat = session.TransitionAsync(GamePhase.BossFight).AsTask();
                Task<CampaignPhaseTransitionResult> settlement = session.TransitionAsync(GamePhase.Settlement).AsTask();

                CampaignPhaseTransitionResult combatResult = await combat;
                CampaignPhaseTransitionResult settlementResult = await settlement;

                Assert.That(combatResult.Succeeded && combatResult.Changed, Is.True);
                Assert.That(settlementResult.Succeeded && settlementResult.Changed, Is.True);
                Assert.That(host.AppliedPhases, Is.EqualTo(new[] { GamePhase.BossFight, GamePhase.Settlement }));
                Assert.That(received, Is.EqualTo(new[] { GamePhase.BossFight, GamePhase.Settlement }));
            }
            finally
            {
                EventBus.Unsubscribe(handler);
            }
        }

        [Test]
        public async Task TransitionReactor_PreventionLeavesHostUntouched()
        {
            var host = new RecordingHost(GamePhase.Settlement);
            using var session = new PlayableCampaignActionSession(host);
            session.Reactors.RegisterGlobal(new PreventHuntTransitionReactor());

            CampaignPhaseTransitionResult result = await session.TransitionAsync(GamePhase.Hunt);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Reason, Is.EqualTo("测试规则阻止远征"));
            Assert.That(host.CurrentPhase, Is.EqualTo(GamePhase.Settlement));
            Assert.That(host.AppliedPhases, Is.Empty);
        }

        [Test]
        public async Task SettlementToHunt_WithoutPreparedContextIsRejectedBeforeHost()
        {
            var host = new RecordingHost(GamePhase.Settlement);
            using var session = new PlayableCampaignActionSession(host);

            CampaignPhaseTransitionResult result = await session.TransitionAsync(GamePhase.Hunt);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Reason, Does.Contain("路线上下文"));
            Assert.That(host.AppliedPhases, Is.Empty);
        }

        [Test]
        public async Task TransitionAsync_SamePhaseIsSuccessfulNoOp()
        {
            var host = new RecordingHost(GamePhase.Hunt);
            using var session = new PlayableCampaignActionSession(host);

            CampaignPhaseTransitionResult result = await session.TransitionAsync(GamePhase.Hunt);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Changed, Is.False);
            Assert.That(host.AppliedPhases, Is.Empty);
        }

        [Test]
        public async Task BeginEncounterAsync_UsesCampaignRunnerAndPublishesCommittedRequest()
        {
            var host = new RecordingHost(GamePhase.Hunt);
            using var session = new PlayableCampaignActionSession(host);
            var request = new CampaignEncounterRequest(Guid.NewGuid(), "test-boss", CampaignEncounterSourceKind.HuntEvent, GamePhase.Hunt, new Vector2Int(2, -1), "event:test", "test-route");
            CampaignEncounterRequest received = default;
            Action<CampaignEncounterStartedEvent> handler = evt => received = evt.Request;
            EventBus.Subscribe(handler);
            try
            {
                CampaignEncounterStartResult result = await session.BeginEncounterAsync(request);

                Assert.That(result.Succeeded, Is.True);
                Assert.That(host.EncounterRequests, Is.EqualTo(new[] { request }));
                Assert.That(host.CurrentPhase, Is.EqualTo(GamePhase.BossFight));
                Assert.That(received.EncounterId, Is.EqualTo("test-boss"));
                Assert.That(received.SourceSessionId, Is.EqualTo(request.SourceSessionId));
            }
            finally
            {
                EventBus.Unsubscribe(handler);
            }
        }

        [Test]
        public async Task EncounterReactor_PreventionLeavesHostUntouched()
        {
            var host = new RecordingHost(GamePhase.Hunt);
            using var session = new PlayableCampaignActionSession(host);
            session.Reactors.RegisterGlobal(new PreventEncounterReactor());
            var request = new CampaignEncounterRequest(Guid.NewGuid(), "test-boss", CampaignEncounterSourceKind.HuntBossTile, GamePhase.Hunt, Vector2Int.zero, string.Empty, string.Empty);

            CampaignEncounterStartResult result = await session.BeginEncounterAsync(request);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Reason, Is.EqualTo("测试规则阻止遭遇"));
            Assert.That(host.CurrentPhase, Is.EqualTo(GamePhase.Hunt));
            Assert.That(host.EncounterRequests, Is.Empty);
        }

        [Test]
        public async Task RestartAsync_UsesCampaignRunnerAndPublishesCommittedFact()
        {
            var host = new RecordingHost(GamePhase.BossFight);
            using var session = new PlayableCampaignActionSession(host);
            int committedCount = 0;
            Action<CampaignRestartCommittedEvent> handler = _ => committedCount++;
            EventBus.Subscribe(handler);
            try
            {
                CampaignRestartResult result = await session.RestartAsync();

                Assert.That(result.Succeeded, Is.True, result.Reason);
                Assert.That(host.RestartCount, Is.EqualTo(1));
                Assert.That(committedCount, Is.EqualTo(1));
            }
            finally
            {
                EventBus.Unsubscribe(handler);
            }
        }

        [Test]
        public async Task RestartReactor_PreventionLeavesHostUntouched()
        {
            var host = new RecordingHost(GamePhase.BossFight);
            using var session = new PlayableCampaignActionSession(host);
            session.Reactors.RegisterGlobal(new PreventRestartReactor());

            CampaignRestartResult result = await session.RestartAsync();

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Reason, Is.EqualTo("测试规则阻止重写战役"));
            Assert.That(host.RestartCount, Is.Zero);
        }

        private sealed class RecordingHost : ICampaignPhaseTransitionHost, ICampaignRestartHost
        {
            public RecordingHost(GamePhase currentPhase)
            {
                CurrentPhase = currentPhase;
            }

            public GamePhase CurrentPhase { get; private set; }
            public List<GamePhase> AppliedPhases { get; } = new();
            public List<CampaignEncounterRequest> EncounterRequests { get; } = new();
            public int RestartCount { get; private set; }

            public bool TryApplyPhaseTransition(GamePhase targetPhase, out string reason)
            {
                AppliedPhases.Add(targetPhase);
                CurrentPhase = targetPhase;
                reason = string.Empty;
                return true;
            }

            public bool TryBeginEncounter(CampaignEncounterRequest request, out string reason)
            {
                EncounterRequests.Add(request);
                CurrentPhase = GamePhase.BossFight;
                reason = string.Empty;
                return true;
            }

            public UniTask<CampaignRestartResult> RestartCampaignFromActionAsync(System.Threading.CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                RestartCount++;
                return UniTask.FromResult(CampaignRestartResult.Success());
            }
        }

        private sealed class PreventEncounterReactor : GameActionReactor<BeginCampaignEncounterAction>
        {
            public override ReactionTiming Timing => ReactionTiming.BeforeExecution;
            protected override void React(BeginCampaignEncounterAction action, ReactionContext context, ReactionResponse response) => response.Prevent("测试规则阻止遭遇");
        }

        private sealed class PreventHuntTransitionReactor : GameActionReactor<TransitionCampaignPhaseAction>
        {
            public override ReactionTiming Timing => ReactionTiming.BeforeExecution;
            public override bool Matches(ReactionContext context) => ((TransitionCampaignPhaseAction)context.Action).TargetPhase == GamePhase.Hunt;
            protected override void React(TransitionCampaignPhaseAction action, ReactionContext context, ReactionResponse response) => response.Prevent("测试规则阻止远征");
        }

        private sealed class PreventRestartReactor : GameActionReactor<RestartCampaignAction>
        {
            public override ReactionTiming Timing => ReactionTiming.BeforeExecution;
            protected override void React(RestartCampaignAction action, ReactionContext context, ReactionResponse response) => response.Prevent("测试规则阻止重写战役");
        }
    }
}
