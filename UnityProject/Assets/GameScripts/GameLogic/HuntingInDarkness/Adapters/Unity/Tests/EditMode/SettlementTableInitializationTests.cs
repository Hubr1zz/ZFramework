using System.Collections.Generic;
using System.Reflection;
using Cards3D;
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

        [Test]
        public void Init_FallbackWorkshopGridFitsProjectedCards()
        {
            var root = new GameObject("FallbackWorkshopLayoutTest");
            SettlementTable3D table = root.AddComponent<SettlementTable3D>();
            PlayableWorkshopCatalog catalog = ScriptableObject.CreateInstance<PlayableWorkshopCatalog>();
            SetPrivateField(catalog, "workshops", new List<PlayableWorkshopDefinition>
            {
                CreateWorkshop("workshop_1"),
                CreateWorkshop("workshop_2"),
                CreateWorkshop("workshop_3"),
                CreateWorkshop("workshop_4")
            });
            bool previousIgnoreState = LogAssert.ignoreFailingMessages;
            try
            {
                LogAssert.ignoreFailingMessages = true;
                table.Init(new SettlementManager(1), catalog);

                WorkshopZone workshopZone = GetPrivateField<WorkshopZone>(table, "_workshopZone");
                SlotGrid grid = GetPrivateField<SlotGrid>(workshopZone, "_grid");
                Assert.That(grid.Columns, Is.EqualTo(3));
                Assert.That(grid.Rows, Is.EqualTo(2));
                Assert.That(grid.Slots, Has.Count.EqualTo(6));
                Assert.That(workshopZone.GetComponentsInChildren<WorkshopBlueprintCard3D>(true), Has.Length.EqualTo(4));
            }
            finally
            {
                LogAssert.ignoreFailingMessages = previousIgnoreState;
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(catalog);
            }
        }

        [Test]
        public void Init_FallbackConsumableUseSlotDoesNotOverlapStoragePaging()
        {
            var root = new GameObject("FallbackConsumableLayoutTest");
            SettlementTable3D table = root.AddComponent<SettlementTable3D>();
            bool previousIgnoreState = LogAssert.ignoreFailingMessages;
            try
            {
                LogAssert.ignoreFailingMessages = true;
                table.Init(new SettlementManager(1));

                HunterEquipmentPanel3D panel = GetPrivateField<HunterEquipmentPanel3D>(table, "hunterEquipmentPanel");
                SlotGrid useGrid = GetPrivateField<SlotGrid>(panel, "consumableUseGrid");
                GameObject nextPageButton = GetPrivateField<GameObject>(panel, "nextPageButton");
                Assert.That(useGrid, Is.Not.Null);
                Assert.That(nextPageButton, Is.Not.Null);
                Assert.That(Vector3.Distance(useGrid.transform.localPosition, nextPageButton.transform.localPosition), Is.GreaterThan(CardView3D.CW * 0.5f));
            }
            finally
            {
                LogAssert.ignoreFailingMessages = previousIgnoreState;
                Object.DestroyImmediate(root);
            }
        }

        private static PlayableWorkshopDefinition CreateWorkshop(string workshopId)
        {
            var definition = new PlayableWorkshopDefinition();
            SetPrivateField(definition, "workshopId", workshopId);
            SetPrivateField(definition, "displayName", workshopId);
            return definition;
        }

        private static T GetPrivateField<T>(object instance, string fieldName)
        {
            FieldInfo field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field: {fieldName}");
            return (T)field.GetValue(instance);
        }

        private static void SetPrivateField(object instance, string fieldName, object value)
        {
            FieldInfo field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field: {fieldName}");
            field.SetValue(instance, value);
        }
    }
}
