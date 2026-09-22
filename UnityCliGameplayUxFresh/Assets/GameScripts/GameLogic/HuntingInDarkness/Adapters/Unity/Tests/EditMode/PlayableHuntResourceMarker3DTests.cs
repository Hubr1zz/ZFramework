using System.Collections.Generic;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Foundation;
using HuntingInDarkness.Hunt;
using HuntingInDarkness.Settlement;
using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace HuntingInDarkness.Adapter.Tests
{
    public sealed class PlayableHuntResourceMarker3DTests
    {
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
    }
}
