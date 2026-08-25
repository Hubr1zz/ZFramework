using HuntingInDarkness.GameCore.Hunters;
using HuntingInDarkness.GameCore.Settlement;
using NUnit.Framework;

namespace HuntingInDarkness.GameCore.Tests
{
    public sealed class HunterRecoveryRulesTests
    {
        [Test]
        public void TryRecover_HealsSelectedBodyPartAndClampsAtMaximum()
        {
            var hunter = new HunterState();
            hunter.HP.arms = 1;
            hunter.MaxHP.arms = 3;

            bool recovered = HunterRecoveryRules.TryRecover(hunter, HunterBodyPart.Arms, 5, out HunterRecoveryResult result, out string reason);

            Assert.That(recovered, Is.True, reason);
            Assert.That(result.PreviousHealth, Is.EqualTo(1));
            Assert.That(result.CurrentHealth, Is.EqualTo(3));
            Assert.That(result.RecoveredHealth, Is.EqualTo(2));
            Assert.That(hunter.HP.arms, Is.EqualTo(3));
            Assert.That(hunter.HP.head, Is.EqualTo(hunter.MaxHP.head));
        }

        [Test]
        public void CanRecover_RejectsHealthyDeadOrIncompleteHunter()
        {
            var healthy = new HunterState();
            Assert.That(HunterRecoveryRules.CanRecover(healthy, HunterBodyPart.Head, out string healthyReason), Is.False);
            Assert.That(healthyReason, Does.Contain("没有"));

            var dead = new HunterState { IsAlive = false };
            dead.HP.head = 0;
            Assert.That(HunterRecoveryRules.CanRecover(dead, HunterBodyPart.Head, out string deadReason), Is.False);
            Assert.That(deadReason, Does.Contain("逝去"));

            var incomplete = new HunterState { HP = null };
            Assert.That(HunterRecoveryRules.CanRecover(incomplete, HunterBodyPart.Head, out string incompleteReason), Is.False);
            Assert.That(incompleteReason, Does.Contain("不完整"));
        }

        [Test]
        public void TryRecover_ZeroAmountStillRecoversMinimumOneHealth()
        {
            var hunter = new HunterState();
            hunter.HP.body = 0;

            Assert.That(HunterRecoveryRules.TryRecover(hunter, HunterBodyPart.Torso, 0, out HunterRecoveryResult result, out _), Is.True);
            Assert.That(result.RecoveredHealth, Is.EqualTo(1));
            Assert.That(hunter.HP.body, Is.EqualTo(1));
        }

        [Test]
        public void TryApplyRecoverableWound_DamagesExplicitPartAndClampsAboveFatalState()
        {
            var hunter = new HunterState();
            hunter.HP.arms = 3;

            Assert.That(HunterRecoveryRules.TryApplyRecoverableWound(hunter, "arms", 5, out HunterRecoverableWoundResult result, out string reason), Is.True, reason);
            Assert.That(result.PreviousHealth, Is.EqualTo(3));
            Assert.That(result.CurrentHealth, Is.EqualTo(1));
            Assert.That(result.HealthLost, Is.EqualTo(2));
            Assert.That(hunter.HP.arms, Is.EqualTo(1));
            Assert.That(hunter.IsDead, Is.False);
        }

        [Test]
        public void TryApplyRecoverableWound_RejectsInvalidDamagePartAndFatalState()
        {
            var hunter = new HunterState();

            Assert.That(HunterRecoveryRules.TryApplyRecoverableWound(hunter, "wings", 1, out _, out _), Is.False);
            Assert.That(HunterRecoveryRules.TryApplyRecoverableWound(hunter, "head", 0, out _, out _), Is.False);
            hunter.HP.head = 0;
            Assert.That(HunterRecoveryRules.TryApplyRecoverableWound(hunter, "head", 1, out _, out _), Is.False);
        }
    }
}
