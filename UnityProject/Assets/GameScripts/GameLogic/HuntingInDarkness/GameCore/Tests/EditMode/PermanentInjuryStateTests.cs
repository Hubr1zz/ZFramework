using HuntingInDarkness.GameCore.Hunters;
using NUnit.Framework;

namespace HuntingInDarkness.GameCore.Tests.EditMode
{
    public sealed class PermanentInjuryStateTests
    {
        [Test]
        public void AddPermanentInjury_DeduplicatesStableId()
        {
            var state = new HunterInjuryState(HunterInjuryProfile.CreateDefault());
            var first = new PermanentInjury("injury_blind_eye", "一眼失明", new PermanentInjuryStatModifiers(0, -1, 0, 0));
            var duplicate = new PermanentInjury("injury_blind_eye", "另一份展示名");

            bool firstAdded = state.AddPermanentInjury(first);
            bool duplicateAdded = state.AddPermanentInjury(duplicate);

            Assert.That(firstAdded, Is.True);
            Assert.That(duplicateAdded, Is.False);
            Assert.That(state.PermanentInjuries, Has.Count.EqualTo(1));
            Assert.That(state.PermanentInjuries[0].StatModifiers.Accuracy, Is.EqualTo(-1));
        }
    }
}
