using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HuntingInDarkness.ActionFlow;
using HuntingInDarkness.ActionFlow.Hunt;
using HuntingInDarkness.ActionFlow.Inventions;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Foundation;
using HuntingInDarkness.Hunt;
using HuntingInDarkness.Settlement;
using NUnit.Framework;
using UnityEngine;

namespace HuntingInDarkness.Adapter.Tests
{
    public sealed class InventionActionEffectInstallerTests
    {
        [Test]
        public async Task UnlockedPlantKnowledge_ModifiesOnlyHerbHarvestInHuntRunner()
        {
            ItemData herb = CreateResource("mushroom_flesh", ItemTag.Herb);
            ItemData organ = CreateResource("soft_organ", ItemTag.Organ);
            InventionData invention = ScriptableObject.CreateInstance<InventionData>();
            invention.ConfigureContentId("plant-knowledge");
            invention.actionEffects.Add(new InventionActionEffect { effectId = "plant:harvest", kind = InventionActionEffectKind.ModifyHarvestHitChance, targetKeyword = "herb", value = 0.1f });
            var settlement = new SettlementInstance();
            settlement.UnlockInvention(invention.ContentId);
            var inventions = new List<InventionData> { invention };
            using var registry = new ActionEnvironmentInstallerRegistry();
            registry.Register(new InventionActionEffectInstaller(() => settlement, () => inventions));

            try
            {
                double herbChance = await PrepareChanceAsync(herb, registry);
                double organChance = await PrepareChanceAsync(organ, registry);

                Assert.That(herbChance, Is.EqualTo(0.7d).Within(0.0001d));
                Assert.That(organChance, Is.EqualTo(0.6d).Within(0.0001d));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(herb);
                UnityEngine.Object.DestroyImmediate(organ);
                UnityEngine.Object.DestroyImmediate(invention);
            }
        }

        [Test]
        public async Task LockedInvention_DoesNotModifyHarvest()
        {
            ItemData herb = CreateResource("mushroom_flesh", ItemTag.Herb);
            InventionData invention = ScriptableObject.CreateInstance<InventionData>();
            invention.ConfigureContentId("plant-knowledge");
            invention.actionEffects.Add(new InventionActionEffect { effectId = "plant:harvest", kind = InventionActionEffectKind.ModifyHarvestHitChance, targetKeyword = "herb", value = 0.1f });
            var settlement = new SettlementInstance();
            var inventions = new List<InventionData> { invention };
            using var registry = new ActionEnvironmentInstallerRegistry();
            registry.Register(new InventionActionEffectInstaller(() => settlement, () => inventions));

            try
            {
                double chance = await PrepareChanceAsync(herb, registry);

                Assert.That(chance, Is.EqualTo(0.6d).Within(0.0001d));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(herb);
                UnityEngine.Object.DestroyImmediate(invention);
            }
        }

        private static async Task<double> PrepareChanceAsync(ItemData resource, IActionEnvironmentInstallerRegistry registry)
        {
            var hunter = new HunterInstance(null, 41) { Name = "采集者" };
            var eventSystem = new EventSystem(new SettlementInstance(), new FirstRandom());
            var manager = new HuntManager(eventSystem, seed: 31);
            manager.OnEnter(new List<HunterInstance> { hunter });
            var point = new ResourcePointInstance { ResourceName = resource.itemName, Resource = resource, DrawCount = 1 };
            manager.Map[Vector2Int.zero].ResourcePoints.Add(point);
            using var session = new PlayableHuntActionSession(manager, installerRegistry: registry);
            PlayableHarvestTransaction transaction = await session.PrepareHarvestAsync(point);
            Assert.That(transaction, Is.Not.Null);
            return transaction.HitChance;
        }

        private static ItemData CreateResource(string id, ItemTag tag)
        {
            ItemData resource = ScriptableObject.CreateInstance<ItemData>();
            resource.ConfigureContentId(id);
            resource.itemName = id;
            resource.tags.Add(tag);
            return resource;
        }

        private sealed class FirstRandom : IRandomSource
        {
            public int Next(int minInclusive, int maxExclusive) => minInclusive;
            public double NextDouble() => 0d;
        }
    }
}
