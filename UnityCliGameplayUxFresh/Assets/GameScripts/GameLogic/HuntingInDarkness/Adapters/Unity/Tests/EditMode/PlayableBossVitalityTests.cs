using System.Collections.Generic;
using Config;
using Core;
using GameplayBase;
using HuntingInDarkness.Combat;
using NUnit.Framework;
using SO.Boss.HitLocation;
using UnityEditor;
using UnityEngine;

namespace HuntingInDarkness.Adapter.Tests
{
    public sealed class PlayableBossVitalityTests
    {
        private readonly List<Object> createdObjects = new();

        [TearDown]
        public void TearDown()
        {
            foreach (Object createdObject in createdObjects)
                Object.DestroyImmediate(createdObject);

            createdObjects.Clear();
        }

        [Test]
        public void BossRuntimeData_ExposesClampedDomainVitality()
        {
            var boss = new BossRuntimeData { MaxHealth = 3 };

            Assert.That(boss.ApplyBossDamage(1), Is.EqualTo(1));
            Assert.That(boss.CurrentHealth, Is.EqualTo(2));
            Assert.That(boss.IsDefeated, Is.False);
            Assert.That(boss.ApplyBossDamage(5), Is.EqualTo(2));
            Assert.That(boss.IsDefeated, Is.True);
        }

        [Test]
        public void DefeatStep_GlobalVitalityOverridesPartDestructionFallback()
        {
            var boss = new BossRuntimeData { MaxHealth = 2 };
            var states = new List<GameplayBase.CombatSystem.HitLocationRuntimeState> { CreateDestroyedPart() };
            var step = new PlayableBossDefeatStep();

            Assert.That(step.TryPublish(boss, states), Is.False);
            boss.ApplyBossDamage(2);
            Assert.That(step.TryPublish(boss, states), Is.True);
            Assert.That(step.TryPublish(boss, states), Is.False);
        }

        [Test]
        public void PlayableBoss_MaxHealthDoesNotExceedAvailablePartDurability()
        {
            BossConfigSO config = AssetDatabase.LoadAssetAtPath<BossConfigSO>("Assets/GameScripts/GameLogic/HuntingInDarkness/Content/Combat/PlayableBoss/PlayableBossConfig.asset");
            int durability = 0;
            foreach (HitLocationCardData part in config.bossHitLocationPool)
                if (part != null)
                    durability += System.Math.Max(1, part.maxHp);

            Assert.That(config.maxHealth, Is.GreaterThan(0));
            Assert.That(config.maxHealth, Is.LessThanOrEqualTo(durability));
        }

        private GameplayBase.CombatSystem.HitLocationRuntimeState CreateDestroyedPart()
        {
            var data = ScriptableObject.CreateInstance<HitLocationCardData>();
            createdObjects.Add(data);
            data.locationName = "测试部位";
            data.maxHp = 1;
            var state = new GameplayBase.CombatSystem.HitLocationRuntimeState(data);
            state.ApplyDamage(1);
            return state;
        }
    }
}
