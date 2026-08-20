using System.Reflection;
using Core;
using HuntingInDarkness.Settlement;
using NUnit.Framework;
using UI;
using UnityEngine;
using UnityEngine.TestTools;

namespace HuntingInDarkness.Adapter.Tests
{
    public sealed class SettlementTableInitializationTests
    {
        [SetUp]
        public void SetUp() => EventBus.Clear();

        [TearDown]
        public void TearDown() => EventBus.Clear();

        [Test]
        public void Init_RebindsWithoutDuplicatingFallbackHierarchy()
        {
            var root = new GameObject("SettlementTableTest");
            SettlementTable3D table = root.AddComponent<SettlementTable3D>();
            var firstManager = new SettlementManager(1);
            var secondManager = new SettlementManager(2);
            bool previousIgnoreState = LogAssert.ignoreFailingMessages;
            try
            {
                LogAssert.ignoreFailingMessages = true;
                table.Init(firstManager);
                int firstChildCount = root.GetComponentsInChildren<Transform>(true).Length;

                table.Init(secondManager);
                int secondChildCount = root.GetComponentsInChildren<Transform>(true).Length;

                Assert.That(secondChildCount, Is.EqualTo(firstChildCount));
                Assert.That(secondChildCount, Is.GreaterThan(1));
                Assert.DoesNotThrow(() => EventBus.Publish(new YearAdvancedEvent { NewYear = 2 }));
            }
            finally
            {
                LogAssert.ignoreFailingMessages = previousIgnoreState;
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void GameManager_ExposesSettlementTableForSceneAssembly()
        {
            FieldInfo field = typeof(GameManager).GetField("_settlementTable3D", BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(field, Is.Not.Null);
            Assert.That(field.GetCustomAttribute<SerializeField>(), Is.Not.Null);
        }

        [Test]
        public void Init_RejectsPartiallyWiredSceneZones()
        {
            var root = new GameObject("PartialSettlementTableTest");
            var table = root.AddComponent<SettlementTable3D>();
            var hunterZone = new GameObject("HunterZone").AddComponent<HunterZone>();
            hunterZone.transform.SetParent(root.transform, false);
            FieldInfo hunterZoneField = typeof(SettlementTable3D).GetField("_hunterZone", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(hunterZoneField, Is.Not.Null);
            hunterZoneField.SetValue(table, hunterZone);

            try
            {
                var exception = Assert.Throws<System.InvalidOperationException>(() => table.Init(new SettlementManager(1)));
                Assert.That(exception.Message, Does.Contain("四个分区必须全部连线"));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
