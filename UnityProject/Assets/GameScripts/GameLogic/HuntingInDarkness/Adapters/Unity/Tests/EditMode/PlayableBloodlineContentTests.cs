using System.Collections.Generic;
using HuntingInDarkness.ContentTables;
using HuntingInDarkness.GameCore.Settlement;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HuntingInDarkness.Adapter.Tests
{
    public sealed class PlayableBloodlineContentTests
    {
        [Test]
        public void ResourcesTable_ProvidesUniquePlayableBloodlines()
        {
            var table = new PlayableBloodlineTable();
            var ids = new HashSet<string>();

            Assert.That(table.Definitions, Has.Count.GreaterThanOrEqualTo(3));
            foreach (HunterBloodlineDefinition definition in table.Definitions)
            {
                Assert.That(definition.Id, Is.Not.Empty);
                Assert.That(definition.DisplayName, Is.Not.Empty);
                Assert.That(definition.DrawWeight, Is.GreaterThan(0));
                Assert.That(ids.Add(definition.Id), Is.True, $"重复血脉 ID：{definition.Id}");
            }
        }

        [Test]
        public void DuplicateIdentity_RejectsEveryAmbiguousRecord()
        {
            var source = new TextAsset("{\"version\":1,\"bloodlines\":[{\"id\":\"same\",\"displayName\":\"甲\",\"drawWeight\":1},{\"id\":\"same\",\"displayName\":\"乙\",\"drawWeight\":1},{\"id\":\"valid\",\"displayName\":\"丙\",\"drawWeight\":1}]}");
            LogAssert.Expect(LogType.Error, "[ContentTable] 血脉表存在重复 id，全部同名条目已拒绝：same");
            try
            {
                var table = new PlayableBloodlineTable(source);

                Assert.That(table.TryGet("same", out _), Is.False);
                Assert.That(table.TryGet("valid", out _), Is.True);
                Assert.That(table.Definitions, Has.Count.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(source);
            }
        }
    }
}
