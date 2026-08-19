using System;
using System.Collections.Generic;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Foundation;
using HuntingInDarkness.GameCore.Hunt;
using HuntingInDarkness.Hunt;
using NUnit.Framework;
using UnityEngine;

namespace HuntingInDarkness.Adapter.Tests
{
    public sealed class PlayableHarvestTransactionTests
    {
        private ItemData resource;

        [SetUp]
        public void SetUp()
        {
            resource = ScriptableObject.CreateInstance<ItemData>();
            resource.itemName = "测试素材";
        }

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(resource);

        [Test]
        public void RevealAndCommit_DelaysMutationUntilEveryCardIsShown()
        {
            var system = new ResourceSystem(new SequenceRandom(0.1, 0.9));
            var hunter = new HunterInstance(null, 1);
            var point = CreatePoint(2);
            PlayableHarvestTransaction transaction = system.PrepareHarvest(point, hunter, 0.5f);

            HarvestCardResult first = transaction.RevealNext();

            Assert.IsTrue(first.IsHit);
            Assert.IsFalse(point.IsExhausted);
            Assert.IsEmpty(hunter.Collectibles);
            Assert.Throws<InvalidOperationException>(() => system.CommitHarvest(transaction));

            HarvestCardResult second = transaction.RevealNext();
            IReadOnlyList<ItemInstance> obtained = system.CommitHarvest(transaction);

            Assert.IsFalse(second.IsHit);
            Assert.IsTrue(point.IsExhausted);
            Assert.AreEqual(1, obtained.Count);
            Assert.AreEqual(1, hunter.Collectibles.Count);
            Assert.AreSame(obtained, system.CommitHarvest(transaction));
        }

        [Test]
        public void PendingTransaction_PreventsDuplicateHarvest()
        {
            var system = new ResourceSystem(new SequenceRandom(0.1));
            var point = CreatePoint(1);
            PlayableHarvestTransaction first = system.PrepareHarvest(point, new HunterInstance(null, 1));

            PlayableHarvestTransaction duplicate = system.PrepareHarvest(point, new HunterInstance(null, 2));

            Assert.IsNull(duplicate);
            Assert.IsTrue(first.Cancel());
            Assert.IsNotNull(system.PrepareHarvest(point, new HunterInstance(null, 3)));
        }

        [Test]
        public void CancelledTransaction_CannotResumeAfterPointIsReservedAgain()
        {
            var system = new ResourceSystem(new SequenceRandom(0.1));
            var point = CreatePoint(1);
            PlayableHarvestTransaction cancelled = system.PrepareHarvest(point, new HunterInstance(null, 1));

            Assert.IsTrue(cancelled.Cancel());
            Assert.IsTrue(cancelled.IsCancelled);
            Assert.IsFalse(cancelled.CanReveal);
            Assert.IsNotNull(system.PrepareHarvest(point, new HunterInstance(null, 2)));
            Assert.Throws<InvalidOperationException>(() => cancelled.RevealNext());
            Assert.Throws<InvalidOperationException>(() => system.CommitHarvest(cancelled));
        }

        [Test]
        public void RevealedTransaction_CannotCancelOrReroll()
        {
            var system = new ResourceSystem(new SequenceRandom(0.9));
            var point = CreatePoint(1);
            PlayableHarvestTransaction transaction = system.PrepareHarvest(point, new HunterInstance(null, 1));
            transaction.RevealNext();

            Assert.IsFalse(transaction.Cancel());
            Assert.IsNull(system.PrepareHarvest(point, new HunterInstance(null, 2)));
        }

        [Test]
        public void PrepareHarvest_RejectsInvalidOrExhaustedPoint()
        {
            var system = new ResourceSystem(new SequenceRandom(0.1));
            var point = CreatePoint(1);
            point.IsExhausted = true;

            Assert.IsNull(system.PrepareHarvest(null, null));
            Assert.IsNull(system.PrepareHarvest(point, null));
        }

        [Test]
        public void CommitHarvest_RejectsTransactionFromAnotherResourceSystem()
        {
            var firstSystem = new ResourceSystem(new SequenceRandom(0.1));
            var secondSystem = new ResourceSystem(new SequenceRandom(0.1));
            PlayableHarvestTransaction transaction = firstSystem.PrepareHarvest(CreatePoint(1), new HunterInstance(null, 1));
            transaction.RevealNext();

            Assert.Throws<InvalidOperationException>(() => secondSystem.CommitHarvest(transaction));
        }

        private ResourcePointInstance CreatePoint(int drawCount) => new()
        {
            ResourceName = resource.itemName,
            Resource = resource,
            DrawCount = drawCount
        };

        private sealed class SequenceRandom : IRandomSource
        {
            private readonly Queue<double> values;
            private double lastValue;

            public SequenceRandom(params double[] values)
            {
                this.values = new Queue<double>(values);
                lastValue = values.Length > 0 ? values[values.Length - 1] : 0d;
            }

            public int Next(int minInclusive, int maxExclusive) => minInclusive;

            public double NextDouble()
            {
                if (values.Count > 0)
                    lastValue = values.Dequeue();
                return lastValue;
            }
        }
    }
}
