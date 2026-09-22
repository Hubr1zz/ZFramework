using Core;
using GameplayBase.CombatSystem;
using HuntingInDarkness.Combat;
using HuntingInDarkness.Data;
using NUnit.Framework;
using SO.Character;
using UnityEngine;

namespace HuntingInDarkness.Adapter.Tests
{
    public sealed class PlayableWeaponMasteryTrackerTests
    {
        [Test]
        public void SettleVictory_PrimaryWeaponDealtDamage_IncrementsOnceAndPublishesFeedback()
        {
            var hunter = new HunterInstance(null, 78123) { Name = "持刃者", WeaponProficiency = 2 };
            var weapon = ScriptableObject.CreateInstance<WeaponData>();
            weapon.weaponName = "骨刃";
            var character = new CharacterRuntimeData { Id = 78123, EquippedWeapon = weapon };
            var tracker = new PlayableWeaponMasteryTracker();
            int feedbackCount = 0;
            void OnChanged(WeaponMasteryChangedEvent _) => feedbackCount++;
            EventBus.Subscribe<WeaponMasteryChangedEvent>(OnChanged);

            try
            {
                tracker.Bind(new[] { hunter }, new[] { character });
                EventBus.Publish(new EffectiveWeaponDamageEvent { CharacterId = character.Id, WeaponName = weapon.weaponName });

                Assert.That(tracker.SettleVictory(), Is.EqualTo(1));
                Assert.That(tracker.SettleVictory(), Is.Zero);
                Assert.That(hunter.WeaponProficiency, Is.EqualTo(3));
                Assert.That(hunter.WeaponMasteries, Has.Count.EqualTo(1));
                Assert.That(hunter.WeaponMasteries[0].MasteryId, Is.EqualTo("weapon:骨刃"));
                Assert.That(hunter.WeaponMasteries[0].Experience, Is.EqualTo(3));
                Assert.That(feedbackCount, Is.EqualTo(1));
            }
            finally
            {
                tracker.Dispose();
                EventBus.Unsubscribe<WeaponMasteryChangedEvent>(OnChanged);
                Object.DestroyImmediate(weapon);
            }
        }
    }
}
