using System.Collections.Generic;
using HuntingInDarkness.GameCore.Settlement;
using NUnit.Framework;

namespace HuntingInDarkness.GameCore.Tests
{
    public sealed class EventOptionAvailabilityRulesTests
    {
        [Test]
        public void Evaluate_RequiresEveryConfiguredCondition()
        {
            var hunter = new HunterState { Courage = 3 };
            hunter.Traits.Add("守望者");
            var conditions = new List<EventOptionConditionDefinition>
            {
                new EventOptionConditionDefinition(EventOptionConditionKind.MinimumCourage, "", 2, false),
                new EventOptionConditionDefinition(EventOptionConditionKind.HasTrait, "守望者", 0, false),
                new EventOptionConditionDefinition(EventOptionConditionKind.MinimumResource, "碎石", 2, false)
            };

            Assert.That(EventOptionAvailabilityRules.Evaluate(conditions, hunter, key => key == "碎石" ? 2 : 0, null, out string reason), Is.True, reason);
            Assert.That(EventOptionAvailabilityRules.Evaluate(conditions, hunter, key => 1, null, out reason), Is.False);
            Assert.That(reason, Does.Contain("碎石"));
        }

        [Test]
        public void Evaluate_InvertedConditionRejectsMatchingHunter()
        {
            var hunter = new HunterState();
            hunter.Ailments.Add("胆怯");
            var conditions = new[] { new EventOptionConditionDefinition(EventOptionConditionKind.HasAilment, "胆怯", 0, true) };

            Assert.That(EventOptionAvailabilityRules.Evaluate(conditions, hunter, null, null, out string reason), Is.False);
            Assert.That(reason, Does.Contain("不可"));
        }

        [Test]
        public void Evaluate_HasKeywordUsesSharedRuleLanguage()
        {
            var conditions = new[] { new EventOptionConditionDefinition(EventOptionConditionKind.HasKeyword, "stone", 0, false) };

            Assert.That(EventOptionAvailabilityRules.Evaluate(conditions, new HunterState(), null, null, new[] { "ritual", "stone" }, out string reason), Is.True, reason);
            Assert.That(EventOptionAvailabilityRules.Evaluate(conditions, new HunterState(), null, null, new[] { "wood" }, out reason), Is.False);
            Assert.That(reason, Does.Contain("stone"));
        }
    }
}
