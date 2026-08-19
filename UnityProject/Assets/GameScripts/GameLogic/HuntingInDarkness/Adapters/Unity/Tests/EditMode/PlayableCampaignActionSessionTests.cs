using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CardGame.ActionQueue;
using Core;
using Cysharp.Threading.Tasks;
using GameplayBase;
using HuntingInDarkness.ActionFlow.Campaign;
using NUnit.Framework;

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
                Task<CampaignPhaseTransitionResult> hunt = session.TransitionAsync(GamePhase.Hunt).AsTask();
                Task<CampaignPhaseTransitionResult> combat = session.TransitionAsync(GamePhase.BossFight).AsTask();

                CampaignPhaseTransitionResult huntResult = await hunt;
                CampaignPhaseTransitionResult combatResult = await combat;

                Assert.That(huntResult.Succeeded && huntResult.Changed, Is.True);
                Assert.That(combatResult.Succeeded && combatResult.Changed, Is.True);
                Assert.That(host.AppliedPhases, Is.EqualTo(new[] { GamePhase.Hunt, GamePhase.BossFight }));
                Assert.That(received, Is.EqualTo(new[] { GamePhase.Hunt, GamePhase.BossFight }));
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
        public async Task TransitionAsync_SamePhaseIsSuccessfulNoOp()
        {
            var host = new RecordingHost(GamePhase.Hunt);
            using var session = new PlayableCampaignActionSession(host);

            CampaignPhaseTransitionResult result = await session.TransitionAsync(GamePhase.Hunt);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Changed, Is.False);
            Assert.That(host.AppliedPhases, Is.Empty);
        }

        private sealed class RecordingHost : ICampaignPhaseTransitionHost
        {
            public RecordingHost(GamePhase currentPhase)
            {
                CurrentPhase = currentPhase;
            }

            public GamePhase CurrentPhase { get; private set; }
            public List<GamePhase> AppliedPhases { get; } = new();

            public bool TryApplyPhaseTransition(GamePhase targetPhase, out string reason)
            {
                AppliedPhases.Add(targetPhase);
                CurrentPhase = targetPhase;
                reason = string.Empty;
                return true;
            }
        }

        private sealed class PreventHuntTransitionReactor : GameActionReactor<TransitionCampaignPhaseAction>
        {
            public override ReactionTiming Timing => ReactionTiming.BeforeExecution;
            public override bool Matches(ReactionContext context) => ((TransitionCampaignPhaseAction)context.Action).TargetPhase == GamePhase.Hunt;
            protected override void React(TransitionCampaignPhaseAction action, ReactionContext context, ReactionResponse response) => response.Prevent("测试规则阻止远征");
        }
    }
}
