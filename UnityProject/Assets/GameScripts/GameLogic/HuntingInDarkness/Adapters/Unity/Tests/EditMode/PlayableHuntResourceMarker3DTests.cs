using System.Collections.Generic;
using System.Reflection;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Foundation;
using HuntingInDarkness.Hunt;
using HuntingInDarkness.Settlement;
using HuntingInDarkness.ViewLayer.Tabletop;
using NUnit.Framework;
using TMPro;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace HuntingInDarkness.Adapter.Tests
{
    public sealed class PlayableHuntResourceMarker3DTests
    {
        private const string PresentationRegistryPrefabPath = "Assets/AssetRaw/UI/Prefabs/TabletopPresentationAssets.prefab";
        private GameObject presentationAssets;

        [SetUp]
        public void SetUp()
        {
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
            if (presentationAssets != null) Object.DestroyImmediate(presentationAssets);
            presentationAssets = null;
        }

        [Test]
        public void Availability_ChangesFromTravelHintToHarvestHintAfterSquadArrives()
        {
            ItemData resource = ScriptableObject.CreateInstance<ItemData>();
            resource.itemName = "测试资源";
            var root = new GameObject("ResourceMarkerTestRoot");
            try
            {
                var manager = new HuntManager(new EventSystem(new SettlementInstance(), new SystemRandomSource(13)), 23);
                manager.OnEnter(new List<HunterInstance> { new(null, 91) });
                HexTileInstance resourceTile = null;
                foreach (HexTileInstance tile in manager.Map.Values)
                    if (tile.State == TileState.Interactable)
                    {
                        resourceTile = tile;
                        break;
                    }
                Assert.That(resourceTile, Is.Not.Null);
                resourceTile.State = TileState.Revealed;
                var point = new ResourcePointInstance { ResourceName = resource.itemName, Resource = resource, DrawCount = 2 };
                resourceTile.ResourcePoints.Add(point);
                PlayableHuntResourceMarker3D marker = PlayableHuntResourceMarker3D.Create(root.transform, manager, resourceTile.AxialCoord, 0, point, Vector3.zero);
                TextMeshPro label = marker.GetComponentInChildren<TextMeshPro>();

                marker.SetHovered(true);

                Assert.That(marker.IsAvailableForHarvest, Is.False);
                Assert.That(label.text, Does.Contain("先移动到此处"));
                Assert.That(marker.transform.localScale, Is.EqualTo(Vector3.one));

                Assert.That(manager.TryCommitTileInteraction(resourceTile.AxialCoord, HuntTileInteractionKind.Move, out _), Is.True);
                marker.RefreshAvailability();
                marker.SetHovered(true);

                Assert.That(marker.IsAvailableForHarvest, Is.True);
                Assert.That(label.text, Does.Contain("点击采集 · 抽取 2"));
                Assert.That(marker.transform.localScale.x, Is.GreaterThan(1f));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(resource);
            }
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
