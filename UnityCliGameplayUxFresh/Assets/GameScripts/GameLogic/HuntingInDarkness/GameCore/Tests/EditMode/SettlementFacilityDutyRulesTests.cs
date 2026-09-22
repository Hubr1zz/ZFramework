using HuntingInDarkness.GameCore.Settlement;
using NUnit.Framework;

namespace HuntingInDarkness.GameCore.Tests
{
    public sealed class SettlementFacilityDutyRulesTests
    {
        [Test]
        public void ShelterWatchDefinition_ValidatesConfiguredDiceAndBands()
        {
            var definition = new SettlementFacilityDutyDefinition(
                "shelter_watch",
                "shelter",
                1,
                SettlementFacilityDutyCheckType.PhysicalDice,
                new[]
                {
                    new SettlementFacilityDutyPopulationBand(1, 2, 0),
                    new SettlementFacilityDutyPopulationBand(3, 5, 1),
                    new SettlementFacilityDutyPopulationBand(6, 6, 2)
                },
                "shelter",
                "庇护所值守",
                "守护庇护所",
                "值守成果",
                1,
                6);

            Assert.That(SettlementFacilityDutyRules.TryValidateDefinition(definition, out string reason), Is.True, reason);
            Assert.That(SettlementFacilityDutyRules.TryResolve(definition, 6, out SettlementFacilityDutyResolution resolution), Is.True, resolution.Reason);
            Assert.That(resolution.PopulationGain, Is.EqualTo(2));
        }

        [Test]
        public void Rules_RejectIncompleteBandsAndUnsupportedDice()
        {
            var incomplete = new SettlementFacilityDutyDefinition("watch", "shelter", 1, SettlementFacilityDutyCheckType.PhysicalDice, new[] { new SettlementFacilityDutyPopulationBand(1, 5, 1) }, diceSides: 6);
            var unsupported = new SettlementFacilityDutyDefinition("watch", "shelter", 1, SettlementFacilityDutyCheckType.PhysicalDice, new[]
            {
                new SettlementFacilityDutyPopulationBand(1, 6, 0)
            }, diceCount: 0, diceSides: 6);

            Assert.That(SettlementFacilityDutyRules.TryValidateDefinition(incomplete, out _), Is.False);
            Assert.That(SettlementFacilityDutyRules.TryValidateDefinition(unsupported, out string reason), Is.False);
            Assert.That(reason, Does.Contain("骰子"));
        }

        [Test]
        public void Rules_SaturatePopulationAtIntegerMaximum()
        {
            Assert.That(SettlementFacilityDutyRules.SaturatePopulation(int.MaxValue - 1, 10), Is.EqualTo(int.MaxValue));
            Assert.That(SettlementFacilityDutyRules.SaturatePopulation(5, -10), Is.EqualTo(5));
        }

        [Test]
        public void Rules_CreateState_UsesAbsoluteDueSeasonAndUniqueAssignment()
        {
            var definition = new SettlementFacilityDutyDefinition("watch", "shelter", 1, SettlementFacilityDutyCheckType.PhysicalDice, new[]
            {
                new SettlementFacilityDutyPopulationBand(1, 6, 0)
            });

            Assert.That(SettlementFacilityDutyRules.TryCreateState(definition, "shelter", 7, 2, 1, 2, out SettlementFacilityDutyState first, out string firstReason), Is.True, firstReason);
            Assert.That(SettlementFacilityDutyRules.TryCreateState(definition, "shelter", 7, 2, 1, 2, out SettlementFacilityDutyState second, out string secondReason), Is.True, secondReason);
            Assert.That(first.DueYear, Is.EqualTo(3));
            Assert.That(first.DueSeasonIndex, Is.Zero);
            Assert.That(first.AssignmentId, Is.Not.EqualTo(second.AssignmentId));
            Assert.That(SettlementFacilityDutyRules.IsDue(first, 2, 1), Is.False);
            Assert.That(SettlementFacilityDutyRules.IsDue(first, 3, 0), Is.True);

            Assert.That(SettlementFacilityDutyRules.TryCalculateDueCoordinate(definition, 2, 2, 3, out int threeSeasonDueYear, out int threeSeasonDueIndex, out string threeSeasonReason), Is.True, threeSeasonReason);
            Assert.That(threeSeasonDueYear, Is.EqualTo(3));
            Assert.That(threeSeasonDueIndex, Is.Zero);
        }
    }
}
