using System.Collections;
using HuntingInDarkness.Data;
using NUnit.Framework;
using UI.Hunt;
using UnityEngine;
using UnityEngine.TestTools;

namespace HuntingInDarkness.Adapter.PlayModeTests
{
    public sealed class HuntCollectibleTray3DPlayModeTests
    {
        [UnityTest]
        public IEnumerator Present_ProjectsSelectedHunterStacksAndRefreshesChangedInventory()
        {
            var root = new GameObject("HuntCollectibleTray3DPlayModeTests");
            ItemData stone = CreateItem("stone", "石片");
            ItemData herb = CreateItem("herb", "草药");
            var hunter = new HunterInstance(null, 1201) { Name = "拾荒者" };
            hunter.Collectibles.Add(new ItemInstance(stone, 2));
            hunter.Collectibles.Add(new ItemInstance(stone, 3));
            hunter.Collectibles.Add(new ItemInstance(herb, 1));

            HuntCollectibleTray3D tray = HuntCollectibleTray3D.Create(root.transform);
            tray.Present(hunter);

            Assert.That(tray.OwnerName, Is.EqualTo("拾荒者"));
            Assert.That(tray.CardCount, Is.EqualTo(2));
            Assert.That(tray.GetComponentsInChildren<HuntCollectibleCard3D>(), Has.Exactly(1).Matches<HuntCollectibleCard3D>(card => card.ContentId == "stone" && card.Count == 5));

            var secondHunter = new HunterInstance(null, 1202) { Name = "药草师" };
            secondHunter.Collectibles.Add(new ItemInstance(herb, 4));
            tray.Present(secondHunter);
            yield return null;

            Assert.That(tray.OwnerName, Is.EqualTo("药草师"));
            Assert.That(tray.CardCount, Is.EqualTo(1));
            Assert.That(tray.GetComponentsInChildren<HuntCollectibleCard3D>(), Has.Exactly(1).Matches<HuntCollectibleCard3D>(card => card.ContentId == "herb" && card.Count == 4));

            hunter.Collectibles.Clear();
            tray.Present(hunter);
            yield return null;

            Assert.That(tray.CardCount, Is.Zero);
            Assert.That(tray.GetComponentsInChildren<HuntCollectibleCard3D>(), Is.Empty);

            Object.Destroy(root);
            Object.Destroy(stone);
            Object.Destroy(herb);
            yield return null;
        }

        private static ItemData CreateItem(string contentId, string displayName)
        {
            ItemData item = ScriptableObject.CreateInstance<ItemData>();
            item.ConfigureContentId(contentId);
            item.itemName = displayName;
            return item;
        }
    }
}
