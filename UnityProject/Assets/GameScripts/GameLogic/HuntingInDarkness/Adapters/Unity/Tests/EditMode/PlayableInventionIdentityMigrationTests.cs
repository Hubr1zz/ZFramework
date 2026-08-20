using System.Collections.Generic;
using HuntingInDarkness.Data;
using HuntingInDarkness.Settlement;
using NUnit.Framework;
using UnityEngine;

namespace HuntingInDarkness.Adapter.Tests
{
    public sealed class PlayableInventionIdentityMigrationTests
    {
        private readonly List<Object> createdObjects = new();

        [TearDown]
        public void TearDown()
        {
            PlayableSettlementInventionRegistry.Configure(null);
            foreach (Object createdObject in createdObjects)
                if (createdObject != null)
                    Object.DestroyImmediate(createdObject);
            createdObjects.Clear();
        }

        [Test]
        public void MigratePersistentState_ConvertsDisplayAndAssetAliasesIdempotently()
        {
            InventionData training = CreateInvention("weapon_training", "WeaponTraining", "武器训练");
            PlayableSettlementInventionRegistry.Configure(new[] { training });
            var settlement = new SettlementInstance
            {
                UnlockedInventions = new List<StringBoolEntry>
                {
                    new() { Key = "武器训练", Value = true },
                    new() { Key = "weapon_training", Value = false },
                    new() { Key = "unknown_mod_invention", Value = true }
                },
                Timeline = new List<AnnalEntry>
                {
                    new() { EventId = "invention:WeaponTraining", EventName = "旧训练名", EntryType = TimelineEntryType.Invention, IsCompleted = true },
                    new() { EventId = "invention:weapon_training", EventName = "重复记录", EntryType = TimelineEntryType.Invention, IsCompleted = true }
                }
            };

            bool first = PlayableSettlementInventionRegistry.MigratePersistentState(settlement);
            bool second = PlayableSettlementInventionRegistry.MigratePersistentState(settlement);

            Assert.That(first, Is.True);
            Assert.That(second, Is.False);
            Assert.That(settlement.InventionIdentitySchemaVersion, Is.EqualTo(PlayableSettlementInventionRegistry.CurrentIdentitySchemaVersion));
            Assert.That(settlement.UnlockedInventions, Has.Count.EqualTo(2));
            Assert.That(settlement.IsInventionUnlocked("weapon_training"), Is.True);
            Assert.That(settlement.IsInventionUnlocked("武器训练"), Is.False);
            Assert.That(settlement.IsInventionUnlocked("unknown_mod_invention"), Is.True);
            Assert.That(settlement.Timeline, Has.Count.EqualTo(1));
            Assert.That(settlement.Timeline[0].EventId, Is.EqualTo("invention:weapon_training"));
            Assert.That(settlement.Timeline[0].EventName, Is.EqualTo("武器训练"));
        }

        [Test]
        public void MigratePersistentState_DoesNotDowngradeFutureSchema()
        {
            var settlement = new SettlementInstance
            {
                InventionIdentitySchemaVersion = 99,
                UnlockedInventions = new List<StringBoolEntry> { new() { Key = " 旧发明 ", Value = true } }
            };

            bool changed = PlayableSettlementInventionRegistry.MigratePersistentState(settlement);

            Assert.That(changed, Is.False);
            Assert.That(settlement.InventionIdentitySchemaVersion, Is.EqualTo(99));
            Assert.That(settlement.UnlockedInventions[0].Key, Is.EqualTo(" 旧发明 "));
        }

        [Test]
        public void Configure_RejectsStableIdAndLegacyAliasCollisions()
        {
            InventionData first = CreateInvention("first", "First", "second");
            InventionData second = CreateInvention("second", "Second", "第二发明");

            PlayableSettlementInventionRegistry.Configure(new[] { first, second });

            Assert.That(PlayableSettlementInventionRegistry.Inventions, Is.Empty);
            Assert.That(PlayableSettlementInventionRegistry.TryGet("second", out _), Is.False);
        }

        [Test]
        public void Configure_ExplicitStableIdDoesNotRequireAnAssetAlias()
        {
            InventionData invention = CreateInvention("ritual", string.Empty, "仪式");

            PlayableSettlementInventionRegistry.Configure(new[] { invention });

            Assert.That(PlayableSettlementInventionRegistry.Inventions, Has.Count.EqualTo(1));
            Assert.That(PlayableSettlementInventionRegistry.TryGet("ritual", out InventionData resolved), Is.True);
            Assert.That(resolved, Is.SameAs(invention));
        }

        [Test]
        public void Configure_RejectsImplicitAssetNameFallback()
        {
            InventionData invention = ScriptableObject.CreateInstance<InventionData>();
            invention.name = "LegacyAssetName";
            invention.inventionName = "旧式发明";
            createdObjects.Add(invention);

            PlayableSettlementInventionRegistry.Configure(new[] { invention });

            Assert.That(PlayableSettlementInventionRegistry.Inventions, Is.Empty);
        }

        private InventionData CreateInvention(string contentId, string assetName, string displayName)
        {
            InventionData invention = ScriptableObject.CreateInstance<InventionData>();
            invention.name = assetName;
            invention.ConfigureContentId(contentId);
            invention.inventionName = displayName;
            createdObjects.Add(invention);
            return invention;
        }
    }
}
