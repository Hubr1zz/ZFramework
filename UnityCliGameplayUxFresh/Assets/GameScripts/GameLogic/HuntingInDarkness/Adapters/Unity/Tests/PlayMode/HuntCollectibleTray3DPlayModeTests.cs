using System.Collections;
using Cysharp.Threading.Tasks;
using HuntingInDarkness.ActionFlow.Hunt;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Hunters;
using HuntingInDarkness.GameCore.Settlement;
using Cards3D;
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

        [UnityTest]
        public IEnumerator ConsumableCard_OpensBodyPartCardsAndSubmitsHuntCommand()
        {
            var root = new GameObject("HuntConsumableTray3DPlayModeTests");
            ItemData dressing = CreateItem("weathered_field_dressing", "旧式包扎布");
            dressing.itemType = ItemType.Consumable;
            dressing.ConfigureConsumableEffect(ConsumableEffectKind.RecoverBodyPart, 1);
            var hunter = new HunterInstance(null, 1203) { Name = "负伤猎人" };
            hunter.HP.arms = 1;
            hunter.Collectibles.Add(new ItemInstance(dressing, 1));
            var input = new RecordingConsumableInput(hunter, dressing);
            HuntCollectibleTray3D tray = HuntCollectibleTray3D.Create(root.transform);
            tray.Initialize(input);
            tray.Present(hunter);

            HuntCollectibleCard3D itemCard = tray.GetComponentInChildren<HuntCollectibleCard3D>();
            Assert.That(itemCard.IsInteractable, Is.True);
            itemCard.gameObject.SendMessage("OnMouseDown");
            Assert.That(tray.IsConsumablePanelOpen, Is.True);
            HunterRecoveryCard3D armsCard = null;
            foreach (HunterRecoveryCard3D card in root.GetComponentsInChildren<HunterRecoveryCard3D>())
                if (card.BodyPart == HunterBodyPart.Arms) armsCard = card;
            Assert.That(armsCard, Is.Not.Null);
            armsCard.gameObject.SendMessage("OnMouseDown");
            yield return null;

            Assert.That(input.RequestCount, Is.EqualTo(1));
            Assert.That(input.OwnerHunterId, Is.EqualTo(hunter.InstanceId));
            Assert.That(input.ItemId, Is.EqualTo(dressing.ContentId));
            Assert.That(input.BodyPart, Is.EqualTo(HunterBodyPart.Arms));
            Assert.That(hunter.HP.arms, Is.EqualTo(2));
            Assert.That(hunter.Collectibles, Is.Empty);
            Assert.That(tray.CardCount, Is.Zero);

            Object.Destroy(root);
            Object.Destroy(dressing);
            yield return null;
        }

        private static ItemData CreateItem(string contentId, string displayName)
        {
            ItemData item = ScriptableObject.CreateInstance<ItemData>();
            item.ConfigureContentId(contentId);
            item.itemName = displayName;
            return item;
        }

        private sealed class RecordingConsumableInput : IPlayableHuntConsumableInput
        {
            private readonly HunterInstance hunter;
            private readonly ItemData item;

            public RecordingConsumableInput(HunterInstance hunter, ItemData item)
            {
                this.hunter = hunter;
                this.item = item;
            }

            public int RequestCount { get; private set; }
            public int OwnerHunterId { get; private set; }
            public string ItemId { get; private set; }
            public HunterBodyPart BodyPart { get; private set; }

            public UniTask<HuntConsumableCommandResult> UseConsumableAsync(int ownerHunterId, string itemId, HunterBodyPart bodyPart)
            {
                RequestCount++;
                OwnerHunterId = ownerHunterId;
                ItemId = itemId;
                BodyPart = bodyPart;
                hunter.Collectibles.Clear();
                HunterRecoveryRules.TryRecover(hunter, bodyPart, item.ConsumableEffectAmount, out HunterRecoveryResult recovery, out _);
                return UniTask.FromResult(new HuntConsumableCommandResult(true, string.Empty, ownerHunterId, itemId, recovery, 0));
            }
        }
    }
}
