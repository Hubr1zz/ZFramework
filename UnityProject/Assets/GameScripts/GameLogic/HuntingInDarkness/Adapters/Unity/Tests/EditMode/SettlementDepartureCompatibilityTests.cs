using System;
using System.Collections.Generic;
using Core;
using HuntingInDarkness.ActionFlow.Settlement;
using HuntingInDarkness.Settlement;
using NUnit.Framework;

namespace HuntingInDarkness.Tests
{
    public sealed class SettlementDepartureCompatibilityTests
    {
        [Test]
        public void TryDepartWithoutRequestPortFailsWithoutMutatingOrPublishing()
        {
            var manager = new SettlementManager(1);
            manager.Data.DepartingHunterIds = new List<int> { 99 };
            int eventCount = 0;
            Action<HuntDepartedEvent> handler = _ => eventCount++;
            EventBus.Subscribe(handler);
            try
            {
                bool result = manager.TryDepart(new List<int> { 1 });

                Assert.That(result, Is.False);
                Assert.That(manager.Data.DepartingHunterIds, Is.EqualTo(new[] { 99 }));
                Assert.That(eventCount, Is.Zero);
            }
            finally
            {
                EventBus.Unsubscribe(handler);
            }
        }

        [Test]
        public void TryDepartDelegatesToInjectedPortWithoutOwningRosterOrEvents()
        {
            var manager = new SettlementManager(1);
            manager.Data.DepartingHunterIds = new List<int> { 99 };
            var port = new RecordingDeparturePort { Result = true };
            manager.DepartureRequestPort = port;

            bool result = manager.TryDepart(new List<int> { 1, 2 });

            Assert.That(result, Is.True);
            Assert.That(port.RequestCount, Is.EqualTo(1));
            Assert.That(port.LastHunterIds, Is.EqualTo(new[] { 1, 2 }));
            Assert.That(manager.Data.DepartingHunterIds, Is.EqualTo(new[] { 99 }));
        }

        [Test]
        public void TryDepartPropagatesInjectedPortFailure()
        {
            var manager = new SettlementManager(1);
            var port = new RecordingDeparturePort { Result = false };
            manager.DepartureRequestPort = port;

            bool result = manager.TryDepart(new List<int> { 1 });

            Assert.That(result, Is.False);
            Assert.That(port.RequestCount, Is.EqualTo(1));
        }

        private sealed class RecordingDeparturePort : ISettlementDepartureRequestPort
        {
            public bool Result { get; set; }
            public int RequestCount { get; private set; }
            public IReadOnlyList<int> LastHunterIds { get; private set; }

            public bool RequestDeparture(IReadOnlyList<int> hunterIds)
            {
                RequestCount++;
                LastHunterIds = hunterIds == null ? null : new List<int>(hunterIds);
                return Result;
            }
        }
    }
}
