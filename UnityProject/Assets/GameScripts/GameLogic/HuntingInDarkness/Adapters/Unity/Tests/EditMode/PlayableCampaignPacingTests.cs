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
