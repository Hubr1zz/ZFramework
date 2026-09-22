using System;
using System.Collections.Generic;
using HuntingInDarkness.Combat;
using NUnit.Framework;

namespace HuntingInDarkness.Adapter.Tests
{
    public sealed class PlayableCombatSessionScopeTests
    {
        [Test]
        public void Dispose_RunsCleanupInReverseOrderOnlyOnce()
        {
            var calls = new List<int>();
            var scope = new PlayableCombatSessionScope(null);
            scope.RegisterCleanup(() => calls.Add(1));
            scope.RegisterCleanup(() => calls.Add(2));

            scope.Dispose();
            scope.Dispose();

            Assert.That(calls, Is.EqualTo(new[] { 2, 1 }));
            Assert.That(scope.Root == null, Is.True);
        }

        [Test]
        public void RegisterCleanup_AfterDispose_Throws()
        {
            var scope = new PlayableCombatSessionScope(null);
            scope.Dispose();

            Assert.Throws<ObjectDisposedException>(() => scope.RegisterCleanup(() => { }));
        }
    }
}
