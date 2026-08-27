using System.Collections.Generic;
using System.Linq;
using HuntingInDarkness.Data;
using HuntingInDarkness.ViewLayer.Hunt;
using NUnit.Framework;
using UnityEngine;

namespace HuntingInDarkness.Adapter.Tests
{
    public sealed class HuntCollectiblePresentationTests
    {
        private readonly List<ItemData> items = new();

        [TearDown]
        public void TearDown()
        {
            foreach (ItemData item in items)
                Object.DestroyImmediate(item);
            items.Clear();
        }

        [Test]
        public void Create_AggregatesStacksAndLimitsVisibleLabels()
        {
            ItemData stone = CreateItem("stone", "石片");
            ItemData hide = CreateItem("hide", "兽皮");
            ItemData herb = CreateItem("herb", "草药");
            var collectibles = new[]
            {
                new ItemInstance(stone, 2),
                new ItemInstance(stone, 3),
                new ItemInstance(hide, 1),
                new ItemInstance(herb, 4)
            };

            HuntCollectiblePresentation presentation = HuntCollectiblePresentation.Create(collectibles, 2);

            Assert.That(presentation.TotalCount, Is.EqualTo(10));
            Assert.That(presentation.DistinctCount, Is.EqualTo(3));
            Assert.That(presentation.Stacks.Select(stack => stack.ContentId).ToArray(), Is.EqualTo(new[] { "herb", "hide", "stone" }));
            Assert.That(presentation.Stacks.Single(stack => stack.ContentId == "stone").Count, Is.EqualTo(5));
            Assert.That(presentation.Summary, Does.Contain("草药×4"));
            Assert.That(presentation.Summary, Does.Contain("兽皮×1"));
            Assert.That(presentation.Summary, Does.Contain("另 1 类"));
            Assert.That(presentation.Summary.Split('、').Length, Is.EqualTo(3));
        }

        [Test]
        public void Create_IgnoresInvalidEntriesAndProvidesEmptyState()
        {
            ItemData stone = CreateItem("stone", "石片");
            var collectibles = new ItemInstance[] { null, new(null, 4), new(stone, 0), new(stone, -2) };

            HuntCollectiblePresentation presentation = HuntCollectiblePresentation.Create(collectibles);

            Assert.That(presentation.TotalCount, Is.Zero);
            Assert.That(presentation.DistinctCount, Is.Zero);
            Assert.That(presentation.Stacks, Is.Empty);
            Assert.That(presentation.Summary, Is.EqualTo("无"));
        }

        [Test]
        public void Create_SaturatesCorruptedAggregateInsteadOfOverflowing()
        {
            ItemData stone = CreateItem("stone", "石片");
            var collectibles = new[] { new ItemInstance(stone, int.MaxValue), new ItemInstance(stone, 1) };

            HuntCollectiblePresentation presentation = HuntCollectiblePresentation.Create(collectibles);

            Assert.That(presentation.TotalCount, Is.EqualTo(int.MaxValue));
            Assert.That(presentation.Summary, Does.Contain($"石片×{int.MaxValue}"));
        }

        private ItemData CreateItem(string id, string displayName)
        {
            ItemData item = ScriptableObject.CreateInstance<ItemData>();
            item.ConfigureContentId(id);
            item.itemName = displayName;
            items.Add(item);
            return item;
        }
    }
}
