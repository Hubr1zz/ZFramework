using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using Cards3D;
using Core;
using HuntingInDarkness.ViewLayer.Tabletop;
using HuntingInDarkness.Settlement;
using NUnit.Framework;
using TMPro;
using UI;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace HuntingInDarkness.Adapter.Tests
{
        public sealed class SettlementTableInitializationTests
    {
        private const string PresentationRegistryPrefabPath = "Assets/AssetRaw/UI/Prefabs/TabletopPresentationAssets.prefab";
        private GameObject presentationAssets;

        [SetUp]
        public void SetUp()
        {
            EventBus.Clear();
#if UNITY_EDITOR
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PresentationRegistryPrefabPath);
            Assert.That(prefab, Is.Not.Null, $"缺少已安装的事件/年鉴注册 Prefab：{PresentationRegistryPrefabPath}");
            presentationAssets = Object.Instantiate(prefab);
            InvokePrivateLifecycle(presentationAssets.GetComponent<TabletopPresentationAssets>(), "OnEnable");
#endif
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Clear();
            CardPrefabRegistry.Configure(null);
            if (presentationAssets != null) Object.DestroyImmediate(presentationAssets);
            presentationAssets = null;
        }

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
                CampLedgerPanel3D ledger = GetPrivateField<CampLedgerPanel3D>(table, "campLedgerPanel");
                ledger.SetCalendarSeason(new HuntingInDarkness.GameCore.Settlement.SeasonDefinition("first-season", "旧季名", 0));

                table.Init(secondManager);
                int secondChildCount = root.GetComponentsInChildren<Transform>(true).Length;
                ledger.Open(secondManager.Data, Vector3.zero);
                TMP_Text ledgerTitle = ledger.GetComponentsInChildren<TMP_Text>(true).Single(text => text.name == "Title");

                Assert.That(secondChildCount, Is.EqualTo(firstChildCount));
                Assert.That(secondChildCount, Is.GreaterThan(1));
                Assert.That(ledgerTitle.text, Does.Contain("第 1 年 · 第 1 季"));
                Assert.That(ledgerTitle.text, Does.Not.Contain("旧季名"));
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
        public void Init_PersistedWorkshopAreaFitsProjectedCards()
        {
            GameObject root = new("PersistedWorkshopLayoutTest");
            PlayableWorkshopCatalog catalog = ScriptableObject.CreateInstance<PlayableWorkshopCatalog>();
            SetPrivateField(catalog, "workshops", new List<PlayableWorkshopDefinition>
            {
                CreateWorkshop("workshop_1"),
                CreateWorkshop("workshop_2"),
                CreateWorkshop("workshop_3"),
                CreateWorkshop("workshop_4")
            });
            SettlementTable3D table = root.AddComponent<SettlementTable3D>();
            bool previousIgnoreState = LogAssert.ignoreFailingMessages;
            try
            {
                LogAssert.ignoreFailingMessages = true;
                table.Init(new SettlementManager(1), catalog);

                SettlementTableVisualLayout layout = root.GetComponentInChildren<SettlementTableVisualLayout>(true);
                Assert.That(layout, Is.Not.Null);
                layout.WorkshopExpansion.Open();
                WorkshopZone workshopZone = layout.WorkshopZone;
                SlotGrid grid = workshopZone.GetComponentInChildren<SlotGrid>(true);
                Assert.That(grid, Is.Not.Null);
                Assert.That(grid.Slots, Has.Count.GreaterThanOrEqualTo(4));
                WorkshopBlueprintCard3D[] cards = workshopZone.GetComponentsInChildren<WorkshopBlueprintCard3D>(true);
                Assert.That(cards, Has.Length.EqualTo(4));
                foreach (WorkshopBlueprintCard3D card in cards)
                {
                    Assert.That(card.Width, Is.LessThanOrEqualTo(grid.SlotW + 0.001f), card.name);
                    Assert.That(card.Height, Is.LessThanOrEqualTo(grid.SlotH + 0.001f), card.name);
                    Renderer renderer = card.transform.Find("Body")?.GetComponent<Renderer>();
                    Assert.That(renderer, Is.Not.Null, card.name);
                    Assert.That(renderer.bounds.size.x, Is.LessThanOrEqualTo(grid.SlotW + 0.001f), card.name);
                    Assert.That(renderer.bounds.size.z, Is.LessThanOrEqualTo(grid.SlotH + 0.001f), card.name);
                }
                for (int firstIndex = 0; firstIndex < cards.Length; firstIndex++)
                {
                    Renderer firstRenderer = cards[firstIndex].transform.Find("Body")?.GetComponent<Renderer>();
                    for (int secondIndex = firstIndex + 1; secondIndex < cards.Length; secondIndex++)
                    {
                        Renderer secondRenderer = cards[secondIndex].transform.Find("Body")?.GetComponent<Renderer>();
                        Bounds firstBounds = firstRenderer.bounds;
                        Bounds secondBounds = secondRenderer.bounds;
                        bool overlapX = firstBounds.min.x < secondBounds.max.x && secondBounds.min.x < firstBounds.max.x;
                        bool overlapZ = firstBounds.min.z < secondBounds.max.z && secondBounds.min.z < firstBounds.max.z;
                        Assert.That(overlapX && overlapZ, Is.False, $"工坊蓝图卡面重叠：{cards[firstIndex].name} / {cards[secondIndex].name}");
                    }
                }
                Assert.That(layout.TableRenderer, Is.Not.Null);
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

        [Test]
        public void LedgerOpenAndHideUsesScreenModalState()
        {
            var root = new GameObject("ContextPanelFramingTest");
                SettlementTable3D table = root.AddComponent<SettlementTable3D>();
            var manager = new SettlementManager(1);
            bool previousIgnoreState = LogAssert.ignoreFailingMessages;
            try
            {
                LogAssert.ignoreFailingMessages = true;
                table.Init(manager);
                CampLedgerPanel3D ledger = GetPrivateField<CampLedgerPanel3D>(table, "campLedgerPanel");
                ledger.Open(manager.Data, new Vector3(3f, 0.1f, -2f));

                Assert.That(ledger.IsOpen, Is.True);
                Assert.That(ScreenModalInputGate.IsBlocked, Is.True);

                ledger.Hide();
                Assert.That(ledger.IsOpen, Is.False);
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

        private static void InvokePrivateLifecycle(object instance, string methodName)
        {
            Assert.That(instance, Is.Not.Null, methodName);
            MethodInfo method = instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, methodName);
            method.Invoke(instance, null);
        }
    }
}
