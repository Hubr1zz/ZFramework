using System;
using HuntingInDarkness.GameCore.Hunt;
using HuntingInDarkness.GameCore.Settlement;
using NUnit.Framework;

namespace HuntingInDarkness.GameCore.Tests
{
    public sealed class HuntReturnRulesTests
    {
        [Test]
        public void TryCreatePlan_AggregatesDuplicateResourcesAndParticipants()
        {
            var input = new HuntReturnInput("return-1", 1, 4, 2, 0, new[] { 7, 8 }, new[] { "stone", "stone", "herb" });
            var participants = new[]
            {
                new HuntReturnParticipantState(7, true, HunterAvailabilityState.Active, 1),
                new HuntReturnParticipantState(8, true, HunterAvailabilityState.Active, 12)
            };
            var resources = new[]
            {
                new HuntReturnResourceState("stone", 2),
                new HuntReturnResourceState("herb", 0)
            };

            Assert.That(HuntReturnRules.TryCreatePlan(input, 4, participants, resources, false, out HuntReturnPlan plan, out string reason), Is.True, reason);
            Assert.That(plan.ResourceGrants, Has.Count.EqualTo(2));
            Assert.That(plan.ResourceGrants[0].Amount, Is.EqualTo(2));
            Assert.That(plan.ParticipantPlans[0].ShouldAdvance, Is.True);
            Assert.That(plan.ParticipantPlans[1].ShouldRetire, Is.True);
        }

        [Test]
        public void TryCreatePlan_RejectsUnknownDuplicateAndOverflowBeforeMutation()
        {
            var duplicate = new HuntReturnInput("return-2", 1, 4, 2, 0, new[] { 7, 7 }, Array.Empty<string>());
            var unknown = new HuntReturnInput("return-3", 1, 4, 1, 0, new[] { 7 }, new[] { "missing" });
            var overflow = new HuntReturnInput("return-4", 1, 4, 1, 0, new[] { 7 }, new[] { "stone", "stone" });
            var participant = new[] { new HuntReturnParticipantState(7, true, HunterAvailabilityState.Active, 1) };
            var resource = new[] { new HuntReturnResourceState("stone", int.MaxValue) };

            Assert.That(HuntReturnRules.TryCreatePlan(duplicate, 4, participant, resource, false, out _, out _), Is.False);
            Assert.That(HuntReturnRules.TryCreatePlan(unknown, 4, participant, resource, false, out _, out _), Is.False);
            Assert.That(HuntReturnRules.TryCreatePlan(overflow, 4, participant, resource, false, out _, out _), Is.False);
        }

        [Test]
        public void TryCreatePlan_LegacyAndAlreadyAppliedAreIdempotent()
        {
            var legacy = new HuntReturnInput("legacy", 0, 4, 0, 0, null, null);
            var duplicate = new HuntReturnInput("done", HuntReturnRules.CurrentSchemaVersion, 999, 0, 0, null, null);

            Assert.That(HuntReturnRules.TryCreatePlan(legacy, 4, null, null, false, out HuntReturnPlan legacyPlan, out _), Is.True);
            Assert.That(legacyPlan.IsLegacyCompatibility, Is.True);
            Assert.That(HuntReturnRules.TryCreatePlan(duplicate, 4, null, null, true, out HuntReturnPlan duplicatePlan, out _), Is.True);
            Assert.That(duplicatePlan.IsAlreadyApplied, Is.True);
        }

        [Test]
        public void TryCreatePlan_RejectsFutureSchema()
        {
            var input = new HuntReturnInput("future", HuntReturnRules.CurrentSchemaVersion + 1, 1, 0, 0, Array.Empty<int>(), Array.Empty<string>());

            Assert.That(HuntReturnRules.TryCreatePlan(input, 1, null, null, false, out _, out string reason), Is.False);
            Assert.That(reason, Is.Not.Empty);
        }

        [Test]
        public void TryCreatePlan_RejectsDeathCountAndUnavailableLivingHunter()
        {
            var input = new HuntReturnInput("invalid-state", 1, 1, 1, 1, new[] { 7 }, Array.Empty<string>());
            var aliveRetired = new[] { new HuntReturnParticipantState(7, true, HunterAvailabilityState.Retired, 3) };

            Assert.That(HuntReturnRules.TryCreatePlan(input, 1, aliveRetired, null, false, out _, out _), Is.False);

            var alive = new[] { new HuntReturnParticipantState(7, true, HunterAvailabilityState.Active, 3) };
            Assert.That(HuntReturnRules.TryCreatePlan(input, 1, alive, null, false, out _, out _), Is.False);
        }

        [Test]
        public void TryCreateItemPlan_V2ClassifiesMixedLootAndPreflightsEveryStack()
        {
            var input = new HuntReturnInput("mixed", 2, 3, 1, 0, new[] { 7 }, Array.Empty<string>(), new[]
            {
                new HuntLootStack("stone", 2),
                new HuntLootStack("field_dressing", 1)
            });
            var participants = new[] { new HuntReturnParticipantState(7, true, HunterAvailabilityState.Active, 2) };
            var items = new[]
            {
                new HuntReturnItemState("stone", HuntReturnItemKind.Resource, 4),
                new HuntReturnItemState("field_dressing", HuntReturnItemKind.StoredItem, 1)
            };

            Assert.That(HuntReturnRules.TryCreateItemPlan(input, 3, participants, items, false, out HuntReturnPlan plan, out string reason), Is.True, reason);
            Assert.That(plan.ItemGrants, Has.Count.EqualTo(2));
            Assert.That(plan.CollectedResourceCount, Is.EqualTo(2));
            Assert.That(plan.CollectedItemCount, Is.EqualTo(3));
            Assert.That(plan.ItemGrants[1].Kind, Is.EqualTo(HuntReturnItemKind.StoredItem));
        }

        [Test]
        public void TryCreateItemPlan_RejectsUnknownMixedSchemaAndOverflowBeforeCommit()
        {
            var participant = new[] { new HuntReturnParticipantState(7, true, HunterAvailabilityState.Active, 2) };
            var items = new[] { new HuntReturnItemState("known", HuntReturnItemKind.StoredItem, int.MaxValue) };
            var unknown = new HuntReturnInput("unknown", 2, 3, 1, 0, new[] { 7 }, Array.Empty<string>(), new[] { new HuntLootStack("missing", 1) });
            var v1Mix = new HuntReturnInput("v1-mixed-protocol", 1, 3, 1, 0, new[] { 7 }, Array.Empty<string>(), new[] { new HuntLootStack("known", 1) });
            var v2Mix = new HuntReturnInput("v2-mixed-protocol", 2, 3, 1, 0, new[] { 7 }, new[] { "known" }, new[] { new HuntLootStack("known", 1) });
            var overflow = new HuntReturnInput("overflow", 2, 3, 1, 0, new[] { 7 }, Array.Empty<string>(), new[] { new HuntLootStack("known", 1) });

            Assert.That(HuntReturnRules.TryCreateItemPlan(unknown, 3, participant, items, false, out _, out _), Is.False);
            Assert.That(HuntReturnRules.TryCreateItemPlan(v1Mix, 3, participant, items, false, out _, out _), Is.False);
            Assert.That(HuntReturnRules.TryCreateItemPlan(v2Mix, 3, participant, items, false, out _, out _), Is.False);
            Assert.That(HuntReturnRules.TryCreateItemPlan(overflow, 3, participant, items, false, out _, out _), Is.False);
        }
    }
}
