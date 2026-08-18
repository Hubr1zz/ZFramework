using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Foundation;
using HuntingInDarkness.Settlement;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HuntingInDarkness.Tests
{
    public sealed class PlayableGrowthMilestoneTests
    {
        private const string CatalogPath = "Assets/GameScripts/GameLogic/HuntingInDarkness/Content/Settlement/Growth/PlayableGrowthMilestoneCatalog.asset";

        [Test]
        public void ConfiguredCatalog_ClaimsAllReachedMilestonesAndPersistsIds()
        {
            PlayableGrowthMilestoneCatalog catalog = AssetDatabase.LoadAssetAtPath<PlayableGrowthMilestoneCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.IsConfigured, Is.True);
            Assert.That(catalog.GetDefinitions(), Has.Count.EqualTo(6));
            PlayableGrowthMilestoneRuntime.Configure(catalog);
            var hunter = new HunterInstance(null, 9501) { Courage = 8, Understanding = 8, Willpower = 2, WillpowerMax = 2 };

            Assert.That(PlayableGrowthMilestoneRuntime.SynchronizeHunter(hunter), Has.Count.EqualTo(6));
            Assert.That(PlayableGrowthMilestoneRuntime.SynchronizeHunter(hunter), Is.Empty);
            Assert.That(hunter.Traits, Has.Count.EqualTo(6));
            Assert.That(hunter.ClaimedGrowthMilestoneIds, Has.Count.EqualTo(6));
            Assert.That(hunter.WillpowerMax, Is.EqualTo(3));

            string json = JsonUtility.ToJson(hunter);
            HunterInstance restored = JsonUtility.FromJson<HunterInstance>(json);
            Assert.That(restored.ClaimedGrowthMilestoneIds, Has.Count.EqualTo(6));
            Assert.That(restored.Traits, Has.Count.EqualTo(6));
            Assert.That(restored.WillpowerMax, Is.EqualTo(3));
        }

        [Test]
        public void EventEffect_ReachingThresholdClaimsMilestone()
        {
            PlayableGrowthMilestoneCatalog catalog = AssetDatabase.LoadAssetAtPath<PlayableGrowthMilestoneCatalog>(CatalogPath);
            PlayableGrowthMilestoneRuntime.Configure(catalog);
            var settlement = new SettlementInstance();
            var hunter = new HunterInstance(null, 9502);
            settlement.Hunters.Add(hunter);
            var eventSystem = new EventSystem(settlement, new FirstRandom());

            eventSystem.ApplyEffect(new EventEffect { effectType = EventEffectType.AddCourage, targetName = "selected", value = 2 }, hunter);

            Assert.That(hunter.Courage, Is.EqualTo(2));
            Assert.That(hunter.ClaimedGrowthMilestoneIds, Has.Count.EqualTo(1));
            Assert.That(hunter.Traits, Contains.Item("直面黑暗"));
        }

        private sealed class FirstRandom : IRandomSource
        {
            public int Next(int minInclusive, int maxExclusive) => minInclusive;
            public double NextDouble() => 0d;
        }
    }
}
