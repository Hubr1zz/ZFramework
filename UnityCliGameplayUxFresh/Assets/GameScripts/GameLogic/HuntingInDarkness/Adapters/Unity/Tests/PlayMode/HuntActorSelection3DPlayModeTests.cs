using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Core;
using HuntingInDarkness.ActionFlow.Hunt;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Foundation;
using HuntingInDarkness.Hunt;
using HuntingInDarkness.Settlement;
using HuntingInDarkness.ViewLayer.Tabletop;
using NUnit.Framework;
using UI.Hunt;
using UnityEngine;
using UnityEngine.TestTools;

namespace HuntingInDarkness.Adapter.Tests
{
    public sealed class HuntActorSelection3DPlayModeTests
    {
        [UnityTest]
        public IEnumerator HunterCardClick_CommitsThroughSessionPortAndRefreshesCollectibleOwner()
        {
            var root = new GameObject("HuntActorSelection3DTest");
            HexTileData startingTile = ScriptableObject.CreateInstance<HexTileData>();
            HexTileData plainTile = ScriptableObject.CreateInstance<HexTileData>();
            ItemData resource = ScriptableObject.CreateInstance<ItemData>();
            PlayableHuntActionSession session = null;
            try
            {
                startingTile.name = "selection-view-start";
                startingTile.tileType = TileType.Starting;
                startingTile.tileName = "起点";
                plainTile.name = "selection-view-plain";
                plainTile.tileType = TileType.Plains;
                plainTile.tileName = "荒地";
                resource.name = "selection-view-resource";
                resource.ConfigureContentId("selection-view-resource");
                resource.itemName = "守望石";
                resource.itemType = ItemType.Resource;
                var first = new HunterInstance(null, 7201) { Name = "先行者" };
                var second = new HunterInstance(null, 7202) { Name = "守望者" };
                second.Collectibles.Add(new ItemInstance(resource, 2));
                var settlement = new SettlementInstance();
                settlement.Hunters.Add(first);
                settlement.Hunters.Add(second);
                var manager = new HuntManager(new EventSystem(settlement, new FirstRandom()), seed: 37)
                {
                    StartingTileConfig = startingTile,
                    TilePool = new List<HexTileData> { plainTile }
                };
                manager.OnEnter(new List<HunterInstance> { first, second });
                session = new PlayableHuntActionSession(manager);
                var runtime = new HuntExplorationRuntime(manager, session);
                HuntStatusBoard3D board = HuntStatusBoard3D.Create(root.transform);
                board.Initialize(manager, runtime.Port);
                yield return null;

                FieldInfo pendingField = typeof(HuntStatusBoard3D).GetField("selectionPending", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(pendingField, Is.Not.Null);
                pendingField.SetValue(board, true);
                board.gameObject.SetActive(false);
                board.gameObject.SetActive(true);
                Assert.That(pendingField.GetValue(board), Is.True, "禁用后重新启用不得把仍在执行的权威命令标记为空闲。");
                Assert.That(board.GetComponentsInChildren<TabletopEventChoiceCard3D>().All(card => !card.IsInteractable), Is.True);
                pendingField.SetValue(board, false);
                board.Refresh();

                TabletopEventChoiceCard3D secondCard = board.GetComponentsInChildren<TabletopEventChoiceCard3D>().Single(card => card.DisplayName == second.Name);
                Assert.That(secondCard.IsInteractable, Is.True);
                secondCard.Clicked.Invoke();
                int frames = 0;
                while (!ReferenceEquals(manager.SelectedHunter, second) && frames++ < 20)
                    yield return null;

                Assert.That(manager.SelectedHunter, Is.SameAs(second));
                Assert.That(board.SelectedHunterName, Is.EqualTo(second.Name));
                Assert.That(board.CollectibleOwnerName, Is.EqualTo(second.Name));
                Assert.That(board.CollectibleCardCount, Is.EqualTo(1));
            }
            finally
            {
                session?.Dispose();
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(resource);
                Object.DestroyImmediate(plainTile);
                Object.DestroyImmediate(startingTile);
            }
        }

        private sealed class FirstRandom : IRandomSource
        {
            public int Next(int minInclusive, int maxExclusive) => minInclusive;
            public double NextDouble() => 0d;
        }
    }
}
