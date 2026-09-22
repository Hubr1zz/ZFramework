using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Hunters;
using HuntingInDarkness.GameCore.Settlement;
using HuntingInDarkness.Settlement;
using NUnit.Framework;
using UnityEngine;

namespace HuntingInDarkness.Adapter.Tests
{
    public sealed class PlayableHunterRecoveryServiceTests
    {
        [Test]
        public void TryTreat_SpendsResourceAndRecoversOnlySelectedBodyPart()
        {
            var settlement = new SettlementInstance();
            var hunter = new HunterInstance(null, 801) { Name = "伤员" };
            hunter.HP.body = 1;
            hunter.HP.arms = 2;
            settlement.Hunters.Add(hunter);
            settlement.AddResource("蘑菇肉", 2);
            var costItem = ScriptableObject.CreateInstance<ItemData>();
            costItem.itemName = "蘑菇肉";
            var service = new PlayableHunterRecoveryService(() => settlement, costItem, 1, 1);

            try
            {
                bool treated = service.TryTreat(hunter, HunterBodyPart.Torso, out HunterRecoveryResult result, out string reason);

                Assert.That(treated, Is.True, reason);
                Assert.That(result.RecoveredHealth, Is.EqualTo(1));
                Assert.That(hunter.HP.body, Is.EqualTo(2));
                Assert.That(hunter.HP.arms, Is.EqualTo(2));
                Assert.That(settlement.GetResource("蘑菇肉"), Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(costItem);
            }
        }

        [Test]
        public void TryTreat_RejectsMissingResourceWithoutChangingHealth()
        {
            var settlement = new SettlementInstance();
            var hunter = new HunterInstance(null, 802);
            hunter.HP.head = 0;
            settlement.Hunters.Add(hunter);
            var costItem = ScriptableObject.CreateInstance<ItemData>();
            costItem.itemName = "蘑菇肉";
            var service = new PlayableHunterRecoveryService(() => settlement, costItem, 1, 1);

            try
            {
                Assert.That(service.TryTreat(hunter, HunterBodyPart.Head, out _, out string reason), Is.False);
                Assert.That(reason, Does.Contain("缺少"));
                Assert.That(hunter.HP.head, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(costItem);
            }
        }
    }
}
