using HuntingInDarkness.ActionFlow.Presentation;
using HuntingInDarkness.ViewLayer.Tabletop;
using NUnit.Framework;
using UnityEngine;

namespace HuntingInDarkness.Adapter.Tests
{
    public sealed class TabletopRandomInteractionTests
    {
        [Test]
        public void DiceResultValidator_RejectsMismatchedOrOutOfRangeResults()
        {
            var request = new TabletopRandomInteractionRequest("roll-1", TabletopRandomInteractionKind.PhysicalDice, "7", "event", 2, 10);

            Assert.That(TabletopRandomInteractionResultValidator.TryGetDiceTotal(request, new TabletopRandomInteractionResult("other", new[] { 3, 4 }, null), out _), Is.False);
            Assert.That(TabletopRandomInteractionResultValidator.TryGetDiceTotal(request, new TabletopRandomInteractionResult("roll-1", new[] { 3, 11 }, null), out _), Is.False);
            Assert.That(TabletopRandomInteractionResultValidator.TryGetDiceTotal(request, new TabletopRandomInteractionResult("roll-1", new[] { 3 }, null), out _), Is.False);
        }

        [Test]
        public void DiceResultValidator_AcceptsExactPhysicalResult()
        {
            var request = new TabletopRandomInteractionRequest("roll-2", TabletopRandomInteractionKind.PhysicalDice, "7", "event", 2, 10);

            bool valid = TabletopRandomInteractionResultValidator.TryGetDiceTotal(request, new TabletopRandomInteractionResult("roll-2", new[] { 3, 8 }, null), out int total);

            Assert.That(valid, Is.True);
            Assert.That(total, Is.EqualTo(11));
        }

        [Test]
        public void ResolveUpwardValue_UsesWorldRotationInsteadOfSpawnOrder()
        {
            Vector3[] normals = { Vector3.up, Vector3.right };
            int[] values = { 1, 7 };

            int result = PhysicalDie3D.ResolveUpwardValue(normals, values, Quaternion.Euler(0f, 0f, 90f));

            Assert.That(result, Is.EqualTo(7));
        }
    }
}
