using System.Collections.Generic;
using HuntingInDarkness.ContentTables;
using HuntingInDarkness.Data;
using NUnit.Framework;
using UnityEngine;

namespace HuntingInDarkness.Adapter.Tests
{
    public sealed class PlayableHunterTemplateTableTests
    {
        [Test]
        public void Build_MapsStableIdentityEquipmentAndPersistentOrigin()
        {
            ItemData equipment = CreateItem("salt_ward", ItemType.Armor);
            equipment.armorStats = new ArmorStats { armorBody = 1 };
            var record = new HunterTemplateTableRecord
            {
                id = "ember_keeper",
                displayName = "拾火者",
                recruitable = true,
                stats = new HunterStatsTableRecord { strength = 1, movement = 6, armorBody = 1 },
                willpower = 3,
                startingEquipmentIds = new List<string> { "salt_ward" },
                traits = new List<string> { " trait_keeper_of_flame ", "trait_keeper_of_flame" }
            };
            try
            {
                List<HunterTemplateTableEntry> entries = PlayableHunterTemplateTableRuntime.Build(new[] { record }, new[] { equipment });

                Assert.That(entries, Has.Count.EqualTo(1));
                HunterData template = entries[0].Template;
                Assert.That(template.ContentId, Is.EqualTo("ember_keeper"));
                Assert.That(template.initialStats.movement, Is.EqualTo(6));
                Assert.That(template.startingTraits, Is.EqualTo(new[] { "trait_keeper_of_flame" }));
                var hunter = new HunterInstance(template, 201);
                Assert.That(hunter.OriginTemplateId, Is.EqualTo("ember_keeper"));
                Assert.That(hunter.EquippedItemIds, Is.EqualTo(new[] { "salt_ward" }));
                Assert.That(hunter.Equipment, Has.Count.EqualTo(1));
                Assert.That(hunter.Stats.armorBody, Is.EqualTo(1));
                Object.DestroyImmediate(template);
            }
            finally
            {
                Object.DestroyImmediate(equipment);
            }
        }

        [Test]
        public void Build_RejectsDuplicateIdentityAndUnknownEquipment()
        {
            var errors = new List<string>();
            var records = new[]
            {
                CreateRecord("duplicate", "甲"),
                CreateRecord("duplicate", "乙"),
                new HunterTemplateTableRecord { id = "unknown_item", displayName = "丙", recruitable = true, startingEquipmentIds = new List<string> { "missing" } }
            };

            List<HunterTemplateTableEntry> entries = PlayableHunterTemplateTableRuntime.Build(records, new ItemData[0], errors.Add);

            Assert.That(entries, Is.Empty);
            Assert.That(errors, Has.Some.Contains("重复"));
            Assert.That(errors, Has.Some.Contains("missing"));
        }

        [Test]
        public void Build_RejectsLoadoutThatBreaksWeaponLimit()
        {
            ItemData weapon = CreateItem("knife", ItemType.Weapon);
            var record = CreateRecord("overarmed", "多持者");
            record.startingEquipmentIds = new List<string> { "knife", "knife", "knife" };
            var errors = new List<string>();
            try
            {
                List<HunterTemplateTableEntry> entries = PlayableHunterTemplateTableRuntime.Build(new[] { record }, new[] { weapon }, errors.Add);

                Assert.That(entries, Is.Empty);
                Assert.That(errors, Has.Some.Contains("武器数量已达上限"));
            }
            finally
            {
                Object.DestroyImmediate(weapon);
            }
        }

        [Test]
        public void ResourceTable_ProvidesBaselineRecruitmentCandidate()
        {
            IReadOnlyList<HunterTemplateTableRecord> records = new JsonHunterTemplateTableSource("HuntingInDarkness/Tables/hunters").Load();

            List<HunterTemplateTableEntry> entries = PlayableHunterTemplateTableRuntime.Build(records, PlayableItemTableRuntime.GetItems());

            HunterTemplateTableEntry entry = entries.Find(candidate => candidate.Template != null && candidate.Template.ContentId == "ember_keeper_yao");
            Assert.That(entry.Template, Is.Not.Null);
            Assert.That(entry.Recruitable, Is.True);
            Assert.That(entry.Template.startingEquipment, Has.Count.EqualTo(1));
            Assert.That(entry.Template.startingEquipment[0].ContentId, Is.EqualTo("salt_ward"));
            HunterTemplateTableEntry listener = entries.Find(candidate => candidate.Template != null && candidate.Template.ContentId == "trail_listener_su");
            HunterTemplateTableEntry mender = entries.Find(candidate => candidate.Template != null && candidate.Template.ContentId == "stone_mender_lin");
            Assert.That(listener.Template, Is.Not.Null);
            Assert.That(listener.Recruitable, Is.True);
            Assert.That(listener.Template.startingEquipment[0].ContentId, Is.EqualTo("echo_hook_spear"));
            Assert.That(listener.Template.startingTraits, Does.Contain("trait_watcher"));
            Assert.That(mender.Template, Is.Not.Null);
            Assert.That(mender.Recruitable, Is.True);
            Assert.That(mender.Template.startingEquipment[0].ContentId, Is.EqualTo("stonewatch_mantle"));
            Assert.That(mender.Template.initialWillpower, Is.EqualTo(3));
            foreach (HunterTemplateTableEntry candidate in entries)
                Object.DestroyImmediate(candidate.Template);
        }

        [Test]
        public void Extend_RejectsBaseIdentityCollisionWithoutReplacingExistingTemplate()
        {
            HunterData existing = ScriptableObject.CreateInstance<HunterData>();
            existing.ConfigureContentId("known");
            existing.hunterName = "已知猎人";
            var table = new TextAsset("{\"version\":1,\"hunters\":[{\"id\":\"known\",\"displayName\":\"外来猎人\",\"recruitable\":true}]}");
            var errors = new List<string>();
            try
            {
                bool succeeded = PlayableHunterTemplateTableRuntime.Extend(new[] { existing }, new[] { existing }, new ItemData[0], table, out List<HunterData> starting, out List<HunterData> recruitment, errors.Add);

                Assert.That(succeeded, Is.True);
                Assert.That(starting, Is.EqualTo(new[] { existing }));
                Assert.That(recruitment, Is.EqualTo(new[] { existing }));
                Assert.That(errors, Has.Some.Contains("冲突"));
            }
            finally
            {
                Object.DestroyImmediate(table);
                Object.DestroyImmediate(existing);
            }
        }

        private static HunterTemplateTableRecord CreateRecord(string id, string displayName)
        {
            return new HunterTemplateTableRecord { id = id, displayName = displayName, recruitable = true };
        }

        private static ItemData CreateItem(string id, ItemType type)
        {
            ItemData item = ScriptableObject.CreateInstance<ItemData>();
            item.ConfigureContentId(id);
            item.itemName = id;
            item.itemType = type;
            return item;
        }
    }
}
