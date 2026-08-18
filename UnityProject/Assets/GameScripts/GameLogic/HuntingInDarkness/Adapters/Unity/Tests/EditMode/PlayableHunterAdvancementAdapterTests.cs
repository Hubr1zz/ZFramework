using System.Collections.Generic;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Foundation;
using HuntingInDarkness.GameCore.Settlement;
using HuntingInDarkness.Settlement;
using NUnit.Framework;
using UnityEngine;

namespace HuntingInDarkness.Tests
{
    public sealed class PlayableHunterAdvancementAdapterTests
    {
        [Test]
        public void ApplyAfterHunt_DeduplicatesRosterAndSkipsDeadHunters()
        {
            var survivor = new HunterInstance(null, 101) { Age = 1, IsAlive = true };
            var deadHunter = new HunterInstance(null, 102) { Age = 1, IsAlive = false };
            var settlement = new SettlementInstance();
            var management = new HunterManagementSystem(settlement, new SystemRandomSource(1));

            List<HunterAdvancementOutcome> outcomes = PlayableHunterAdvancementAdapter.ApplyAfterHunt(new[] { survivor, survivor, deadHunter }, management);

            Assert.That(outcomes, Has.Count.EqualTo(1));
            Assert.That(survivor.Age, Is.EqualTo(2));
            Assert.That(survivor.UnspentGrowth, Is.EqualTo(1));
            Assert.That(deadHunter.Age, Is.EqualTo(1));
        }

        [Test]
        public void UnspentGrowth_SurvivesJsonRoundTrip()
        {
            var source = new SettlementInstance();
            source.Hunters.Add(new HunterInstance(null, 103) { UnspentGrowth = 2, Courage = 3, Understanding = 4 });

            string json = JsonUtility.ToJson(source);
            SettlementInstance restored = JsonUtility.FromJson<SettlementInstance>(json);

            Assert.That(restored.Hunters[0].UnspentGrowth, Is.EqualTo(2));
            Assert.That(restored.Hunters[0].Courage, Is.EqualTo(3));
            Assert.That(restored.Hunters[0].Understanding, Is.EqualTo(4));
        }
    }
}
