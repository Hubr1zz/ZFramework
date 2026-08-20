using System;
using System.Threading.Tasks;
using CardGame.ActionQueue;
using Core;
using Cysharp.Threading.Tasks;
using HuntingInDarkness.ActionFlow.Settlement;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Settlement;
using HuntingInDarkness.Settlement;
using NUnit.Framework;
using UnityEngine;

namespace HuntingInDarkness.Adapter.Tests
{
    public sealed class PlayableSettlementWorkshopConstructionActionTests
    {
        [Test]
        public async Task BuildWorkshopAsync_CommitsResourcesWorkshopAndFacts()
        {
            TestContext context = CreateContext();
            int builtFacts = 0;
            int commits = 0;
            Action<SettlementWorkshopBuiltEvent> builtHandler = _ => builtFacts++;
            Action<SettlementTransactionCommittedEvent> commitHandler = evt => commits += evt.Kind == SettlementTransactionKind.WorkshopConstruction ? 1 : 0;
            EventBus.Subscribe(builtHandler);
            EventBus.Subscribe(commitHandler);
            try
            {
                using var session = CreateSession(context);
                SettlementWorkshopConstructionResult result = await session.BuildWorkshopAsync(context.Definition);

                Assert.That(result.Succeeded, Is.True);
                Assert.That(context.Settlement.IsWorkshopBuilt(context.Definition.WorkshopId), Is.True);
                Assert.That(builtFacts, Is.EqualTo(1));
                Assert.That(commits, Is.EqualTo(1));
                foreach (PlayableWorkshopCost cost in context.Definition.Costs)
                    Assert.That(context.Settlement.GetResource(cost.Item), Is.EqualTo(1));
            }
            finally
            {
                EventBus.Unsubscribe(builtHandler);
                EventBus.Unsubscribe(commitHandler);
            }
        }

        [Test]
        public async Task BuildWorkshopAsync_ConcurrentRequestsSpendOnlyOnce()
        {
            TestContext context = CreateContext();
            using var session = CreateSession(context);

            Task<SettlementWorkshopConstructionResult> first = session.BuildWorkshopAsync(context.Definition).AsTask();
            Task<SettlementWorkshopConstructionResult> second = session.BuildWorkshopAsync(context.Definition).AsTask();
            SettlementWorkshopConstructionResult[] results = await Task.WhenAll(first, second);

            Assert.That(Array.FindAll(results, result => result.Succeeded).Length, Is.EqualTo(1));
            foreach (PlayableWorkshopCost cost in context.Definition.Costs)
                Assert.That(context.Settlement.GetResource(cost.Item), Is.EqualTo(1));
        }

        [Test]
        public async Task BuildWorkshopAsync_PreventedReactorLeavesStateUntouched()
        {
            TestContext context = CreateContext();
            using var session = CreateSession(context);
            session.Reactors.RegisterGlobal(new PreventConstructionReactor());

            SettlementWorkshopConstructionResult result = await session.BuildWorkshopAsync(context.Definition);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Reason, Is.EqualTo("测试规则阻止建造"));
            Assert.That(context.Settlement.IsWorkshopBuilt(context.Definition.WorkshopId), Is.False);
            foreach (PlayableWorkshopCost cost in context.Definition.Costs)
                Assert.That(context.Settlement.GetResource(cost.Item), Is.EqualTo(2));
        }

        [Test]
        public async Task BuildWorkshopAsync_ForeignBlueprintCannotMutateSettlement()
        {
            TestContext context = CreateContext();
            using var session = CreateSession(context);
            var foreign = new PlayableWorkshopDefinition();

            SettlementWorkshopConstructionResult result = await session.BuildWorkshopAsync(foreign);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(context.Settlement.BuiltWorkshops, Is.Empty);
        }

        private static TestContext CreateContext()
        {
            PlayableWorkshopCatalog catalog = Resources.Load<PlayableWorkshopCatalog>("HuntingInDarkness/PlayableWorkshopCatalog");
            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.Workshops, Is.Not.Empty);
            PlayableWorkshopDefinition definition = catalog.Workshops[0];
            var settlement = new SettlementInstance();
            if (definition.RequiredInvention != null)
                settlement.UnlockInvention(definition.RequiredInvention.inventionName);
            foreach (PlayableWorkshopCost cost in definition.Costs)
                settlement.AddResource(cost.Item, 2);
            return new TestContext(settlement, catalog, definition);
        }

        private static PlayableSettlementActionSession CreateSession(TestContext context) => new(context.Settlement, new EmptyWeaponTrainingContent(), workshopCatalog: context.Catalog);

        private readonly struct TestContext
        {
            public TestContext(SettlementInstance settlement, PlayableWorkshopCatalog catalog, PlayableWorkshopDefinition definition)
            {
                Settlement = settlement;
                Catalog = catalog;
                Definition = definition;
            }

            public SettlementInstance Settlement { get; }
            public PlayableWorkshopCatalog Catalog { get; }
            public PlayableWorkshopDefinition Definition { get; }
        }

        private sealed class PreventConstructionReactor : GameActionReactor<BuildSettlementWorkshopAction>
        {
            public override ReactionTiming Timing => ReactionTiming.BeforeExecution;
            protected override void React(BuildSettlementWorkshopAction action, ReactionContext context, ReactionResponse response) => response.Prevent("测试规则阻止建造");
        }

        private sealed class EmptyWeaponTrainingContent : IWeaponTrainingContent
        {
            public string RequiredInventionId => string.Empty;
            public string CostResourceId => string.Empty;
            public int ResourceCost => 0;
            public int Experience => 0;
            public bool TryGetFamily(string masteryId, out WeaponMasteryFamilyDefinition family)
            {
                family = null;
                return false;
            }
        }
    }
}
