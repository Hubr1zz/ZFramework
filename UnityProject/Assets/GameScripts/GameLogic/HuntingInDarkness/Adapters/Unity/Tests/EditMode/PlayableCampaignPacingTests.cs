using System;
using Core;
using HuntingInDarkness.Data;
using HuntingInDarkness.Settlement;
using NUnit.Framework;
using UnityEditor;

namespace HuntingInDarkness.Tests
{
    public sealed class PlayableCampaignPacingTests
    {
        [Test]
        public void AdvanceYear_RequiresConfiguredNumberOfHunts()
        {
            var settlement = new SettlementInstance { CurrentYear = 1, HuntsPerYear = 2 };
            var timeline = new TimelineSystem(settlement, new FirstRandom());

            var firstEvents = timeline.AdvanceYear(new HuntRecord { Year = 1 });

            Assert.That(firstEvents, Is.Empty);
            Assert.That(settlement.CurrentYear, Is.EqualTo(1));
            Assert.That(settlement.HuntsCompletedThisYear, Is.EqualTo(1));
            Assert.That(settlement.HuntHistory, Has.Count.EqualTo(1));

            timeline.AdvanceYear(new HuntRecord { Year = 1 });

            Assert.That(settlement.CurrentYear, Is.EqualTo(2));
            Assert.That(settlement.HuntsCompletedThisYear, Is.Zero);
            Assert.That(settlement.HuntHistory, Has.Count.EqualTo(2));
        }

        [Test]
        public void AdvanceYear_PublishesCommittedHuntProgressForEveryReturn()
        {
            var settlement = new SettlementInstance { CurrentYear = 1, HuntsPerYear = 2 };
            var timeline = new TimelineSystem(settlement, new FirstRandom());
            HuntCompletedEvent received = default;
            int receivedCount = 0;
            Action<HuntCompletedEvent> handler = evt =>
            {
                received = evt;
                receivedCount++;
            };
            EventBus.Subscribe(handler);
            try
            {
                timeline.AdvanceYear(new HuntRecord { Year = 1, HuntersDeployed = 2, HuntersLost = 1, CollectedResources = { "碎石", "碎石" } });

                Assert.That(receivedCount, Is.EqualTo(1));
                Assert.That(received.CompletedYear, Is.EqualTo(1));
                Assert.That(received.HuntsCompletedInYear, Is.EqualTo(1));
                Assert.That(received.HuntsPerYear, Is.EqualTo(2));
                Assert.That(received.TotalHunts, Is.EqualTo(1));
                Assert.That(received.HuntersDeployed, Is.EqualTo(2));
                Assert.That(received.HuntersLost, Is.EqualTo(1));
                Assert.That(received.CollectedResourceCount, Is.EqualTo(2));
                Assert.That(received.AdvancedToYear, Is.Zero);

                timeline.AdvanceYear(new HuntRecord { Year = 1, HuntersDeployed = 1, BossDefeated = true });

                Assert.That(receivedCount, Is.EqualTo(2));
                Assert.That(received.HuntsCompletedInYear, Is.EqualTo(2));
                Assert.That(received.TotalHunts, Is.EqualTo(2));
                Assert.That(received.BossDefeated, Is.True);
                Assert.That(received.AdvancedToYear, Is.EqualTo(2));
            }
            finally
            {
                EventBus.Unsubscribe(handler);
            }
        }

        [Test]
        public void CompleteHunt_InvalidPacingFallsBackToOneHuntPerYear()
        {
            var settlement = new SettlementInstance { CurrentYear = 3, HuntsPerYear = 0 };
            var timeline = new TimelineSystem(settlement, new FirstRandom());

            timeline.AdvanceYear(new HuntRecord { Year = 3 });

            Assert.That(settlement.CurrentYear, Is.EqualTo(4));
            Assert.That(settlement.HuntsCompletedThisYear, Is.Zero);
        }

        [Test]
        public void ContentCatalog_MigratesMissingAndOutOfRangePacing()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<PlayableSettlementContentCatalog>("Assets/GameScripts/GameLogic/HuntingInDarkness/Content/Settlement/PlayableSettlementContentCatalog.asset");
            var manager = new SettlementManager(1);
            manager.Data.HuntsPerYear = 0;
            manager.Data.HuntsCompletedThisYear = 99;

            bool applied = catalog.ApplyTo(manager);

            Assert.That(applied, Is.True);
            Assert.That(manager.Data.HuntsPerYear, Is.EqualTo(2));
            Assert.That(manager.Data.HuntsCompletedThisYear, Is.EqualTo(1));
        }

        private sealed class FirstRandom : HuntingInDarkness.GameCore.Foundation.IRandomSource
        {
            public int Next(int minInclusive, int maxExclusive) => minInclusive;
            public double NextDouble() => 0d;
        }
    }
}
