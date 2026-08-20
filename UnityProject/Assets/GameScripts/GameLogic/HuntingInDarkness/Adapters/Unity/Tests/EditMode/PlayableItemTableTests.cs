using System.Collections.Generic;
using HuntingInDarkness.ContentTables;
using HuntingInDarkness.Data;
using NUnit.Framework;
using UnityEngine;

namespace HuntingInDarkness.Adapter.Tests
{
    public sealed class PlayableItemTableTests
    {
        private readonly List<ItemData> createdItems = new();

        [TearDown]
        public void TearDown()
        {
            foreach (ItemData item in createdItems)
                if (item != null)
                    Object.DestroyImmediate(item);
            createdItems.Clear();
        }

        [Test]
        public void Build_MapsStableItemAndNormalizesKeywords()
        {
            var record = new ItemTableRecord
            {
                id = "stone_charm",
                itemName = "石符",
                itemType = "Consumable",
                tags = new List<string> { "Stone", "Rare" },
                keywords = new List<string> { " Ritual ", "ritual" },
                stackLimit = 0
            };

            List<ItemData> items = Build(record);

            Assert.That(items, Has.Count.EqualTo(1));
            Assert.That(items[0].name, Is.EqualTo("stone_charm"));
            Assert.That(items[0].tags, Is.EquivalentTo(new[] { ItemTag.Stone, ItemTag.Rare }));
            Assert.That(items[0].keywords, Is.EqualTo(new[] { "ritual" }));
            Assert.That(items[0].stackLimit, Is.EqualTo(1));
        }

        [Test]
        public void Build_RejectsEveryAmbiguousDuplicate()
        {
            var errors = new List<string>();
            var records = new[]
            {
                new ItemTableRecord { id = "duplicate", itemName = "甲", itemType = "Resource" },
                new ItemTableRecord { id = "duplicate", itemName = "乙", itemType = "Resource" },
                new ItemTableRecord { id = "unique-a", itemName = "同名", itemType = "Resource" },
                new ItemTableRecord { id = "unique-b", itemName = "同名", itemType = "Resource" }
            };

            List<ItemData> items = PlayableItemTableRuntime.Build(records, errors.Add);
            createdItems.AddRange(items);

            Assert.That(items, Is.Empty);
            Assert.That(errors, Has.Count.EqualTo(2));
        }

        [Test]
        public void RuntimeTable_ProvidesResourceContent()
        {
            ItemData item = null;
            foreach (ItemData candidate in PlayableItemTableRuntime.GetItems())
                if (candidate != null && candidate.name == "black_salt")
                    item = candidate;

            Assert.That(item, Is.Not.Null);
            Assert.That(item.itemName, Is.EqualTo("黑盐"));
            Assert.That(item.itemType, Is.EqualTo(ItemType.Resource));
            Assert.That(item.keywords, Does.Contain("ritual"));
        }

        private List<ItemData> Build(params ItemTableRecord[] records)
        {
            List<ItemData> items = PlayableItemTableRuntime.Build(records);
            createdItems.AddRange(items);
            return items;
        }
    }
}
