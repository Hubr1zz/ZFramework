using System;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
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
        public void CardResultValidator_RequiresStableUniqueCardsAndBoundedValues()
        {
            var request = new TabletopRandomInteractionRequest("cards-1", TabletopRandomInteractionKind.FlipCards, "7", "event", 2, 10, "bone-omens");

            Assert.That(TabletopRandomInteractionResultValidator.TryGetCheckTotal(request, new TabletopRandomInteractionResult("cards-1", new[] { 3, 8 }, new[] { "bone-omens:3", "bone-omens:8" }), out int total), Is.True);
            Assert.That(total, Is.EqualTo(11));
            Assert.That(TabletopRandomInteractionResultValidator.TryGetCheckTotal(request, new TabletopRandomInteractionResult("cards-1", new[] { 3, 8 }, new[] { "same", "same" }), out _), Is.False);
            Assert.That(TabletopRandomInteractionResultValidator.TryGetCheckTotal(request, new TabletopRandomInteractionResult("cards-1", new[] { 3, 8 }, new[] { "foreign:3", "foreign:8" }), out _), Is.False);
            Assert.That(TabletopRandomInteractionResultValidator.TryGetCheckTotal(request, new TabletopRandomInteractionResult("cards-1", new[] { 3, 11 }, new[] { "bone-omens:3", "bone-omens:11" }), out _), Is.False);
            Assert.That(TabletopRandomInteractionResultValidator.TryGetCheckTotal(request, new TabletopRandomInteractionResult("cards-1", new[] { 3, 8 }, new[] { "bone-omens:3" }), out _), Is.False);
        }

        [Test]
        public async Task Router_DispatchesDiceAndCardsToDedicatedPresenters()
        {
            var dice = new RecordingPresenter();
            var cards = new RecordingPresenter();
            var router = new TabletopRandomInteractionRouter(dice, cards);

            await router.PresentAsync(new TabletopRandomInteractionRequest("dice", TabletopRandomInteractionKind.PhysicalDice, "", ""), CancellationToken.None);
            await router.PresentAsync(new TabletopRandomInteractionRequest("cards", TabletopRandomInteractionKind.OldMaid, "", "", deckId: "omens"), CancellationToken.None);

            Assert.That(dice.CallCount, Is.EqualTo(1));
            Assert.That(cards.CallCount, Is.EqualTo(1));
        }

        [Test]
        public void ResolveUpwardValue_UsesWorldRotationInsteadOfSpawnOrder()
        {
            Vector3[] normals = { Vector3.up, Vector3.right };
            int[] values = { 1, 7 };

            int result = PhysicalDie3D.ResolveUpwardValue(normals, values, Quaternion.Euler(0f, 0f, 90f));

            Assert.That(result, Is.EqualTo(7));
        }

        private sealed class RecordingPresenter : ITabletopRandomInteractionPresenter
        {
            public int CallCount { get; private set; }

            public UniTask<TabletopRandomInteractionResult> PresentAsync(TabletopRandomInteractionRequest request, CancellationToken cancellationToken)
            {
                CallCount++;
                string[] cardIds = request.Kind == TabletopRandomInteractionKind.PhysicalDice ? Array.Empty<string>() : new[] { $"{request.DeckId}:card" };
                return UniTask.FromResult(new TabletopRandomInteractionResult(request.InteractionId, new[] { 1 }, cardIds));
            }
        }
    }
}
