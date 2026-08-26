using System;
using System.Collections.Generic;
using System.Threading;
using CardGame.ActionQueue;
using Core;
using Cysharp.Threading.Tasks;
using HuntingInDarkness.Data;
using HuntingInDarkness.Settlement;

namespace HuntingInDarkness.ActionFlow.Settlement
{
    public interface ISettlementEquipmentContent
    {
        bool Contains(ItemData item);
    }

    public sealed class PlayableSettlementEquipmentContentAdapter : ISettlementEquipmentContent
    {
        private readonly IReadOnlyList<ItemData> items;

        public PlayableSettlementEquipmentContentAdapter(IReadOnlyList<ItemData> items) => this.items = items ?? Array.Empty<ItemData>();

        public bool Contains(ItemData item)
        {
            if (item == null || (item.itemType != ItemType.Weapon && item.itemType != ItemType.Armor)) return false;
            foreach (ItemData candidate in items)
                if (ReferenceEquals(candidate, item))
                    return true;
            return false;
        }
    }

    public readonly struct SettlementEquipmentCommandResult
    {
        public SettlementEquipmentCommandResult(bool succeeded, string reason, int hunterId, string itemName, int storedCount)
        {
            Succeeded = succeeded;
            Reason = reason ?? string.Empty;
            HunterId = hunterId;
            ItemName = itemName ?? string.Empty;
            StoredCount = storedCount;
        }

        public bool Succeeded { get; }
        public string Reason { get; }
        public int HunterId { get; }
        public string ItemName { get; }
        public int StoredCount { get; }

        public static SettlementEquipmentCommandResult Failed(string reason) => new(false, reason, 0, string.Empty, 0);
    }

    public struct HunterEquipmentChangedEvent
    {
        public int HunterId;
        public string ItemName;
        public bool Equipped;
        public int StoredCount;
    }

    public sealed class EquipHunterItemAction : CommandAction, ISourceAction, ITargetAction
    {
        private readonly SettlementInstance settlement;
        private readonly HunterInstance hunter;
        private readonly ItemData item;
        private readonly ISettlementEquipmentContent content;
        private readonly ActionEventOutbox eventOutbox;

        public EquipHunterItemAction(SettlementInstance settlement, HunterInstance hunter, ItemData item, ISettlementEquipmentContent content, ActionEventOutbox eventOutbox, IReactorEntity source, IReactorEntity target)
        {
            this.settlement = settlement ?? throw new ArgumentNullException(nameof(settlement));
            this.hunter = hunter ?? throw new ArgumentNullException(nameof(hunter));
            this.item = item;
            this.content = content ?? throw new ArgumentNullException(nameof(content));
            this.eventOutbox = eventOutbox ?? throw new ArgumentNullException(nameof(eventOutbox));
            Source = source ?? throw new ArgumentNullException(nameof(source));
            Target = target ?? throw new ArgumentNullException(nameof(target));
        }

        public SettlementEquipmentCommandResult Result { get; private set; }
        public IReactorEntity Source { get; }
        public IReactorEntity Target { get; }

        protected override UniTask<ActionOutcome> ExecuteAsync(ActionExecutionContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!ReferenceEquals(settlement.GetHunter(hunter.InstanceId), hunter)) return Fail("猎人不属于当前营地。");
            if (!content.Contains(item)) return Fail("装备内容尚未配置。");
            if (!PlayableEquipmentRules.CanEquip(hunter, item, out string reason)) return Fail(reason);
            if (settlement.GetStoredEquipment(item) <= 0) return Fail("装备仓库中已没有该物品。");

            cancellationToken.ThrowIfCancellationRequested();
            if (!settlement.SpendStoredEquipment(item, 1)) return Fail("装备仓库已发生变化。");
            hunter.Equipment ??= new List<ItemInstance>();
            hunter.EquippedItemIds ??= new List<string>();
            hunter.Equipment.Add(new ItemInstance(item));
            hunter.EquippedItemIds.Add(item.ContentId);

            int storedCount = settlement.GetStoredEquipment(item);
            Result = new SettlementEquipmentCommandResult(true, string.Empty, hunter.InstanceId, item.itemName, storedCount);
            eventOutbox.Stage(new HunterEquipmentChangedEvent { HunterId = hunter.InstanceId, ItemName = item.itemName, Equipped = true, StoredCount = storedCount });
            eventOutbox.Stage(new SettlementTransactionCommittedEvent { TransactionId = $"equip:{hunter.InstanceId}:{item.ContentId}", Kind = SettlementTransactionKind.Equipment });
            return UniTask.FromResult(ActionOutcome.Success());
        }

        private UniTask<ActionOutcome> Fail(string reason)
        {
            Result = SettlementEquipmentCommandResult.Failed(reason);
            return UniTask.FromResult(ActionOutcome.Failure(reason));
        }
    }

    public sealed class UnequipHunterItemAction : CommandAction, ISourceAction, ITargetAction
    {
        private readonly SettlementInstance settlement;
        private readonly HunterInstance hunter;
        private readonly int equipmentInstanceId;
        private readonly ActionEventOutbox eventOutbox;

        public UnequipHunterItemAction(SettlementInstance settlement, HunterInstance hunter, int equipmentInstanceId, ActionEventOutbox eventOutbox, IReactorEntity source, IReactorEntity target)
        {
            this.settlement = settlement ?? throw new ArgumentNullException(nameof(settlement));
            this.hunter = hunter ?? throw new ArgumentNullException(nameof(hunter));
            this.equipmentInstanceId = equipmentInstanceId;
            this.eventOutbox = eventOutbox ?? throw new ArgumentNullException(nameof(eventOutbox));
            Source = source ?? throw new ArgumentNullException(nameof(source));
            Target = target ?? throw new ArgumentNullException(nameof(target));
        }

        public SettlementEquipmentCommandResult Result { get; private set; }
        public IReactorEntity Source { get; }
        public IReactorEntity Target { get; }

        protected override UniTask<ActionOutcome> ExecuteAsync(ActionExecutionContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!ReferenceEquals(settlement.GetHunter(hunter.InstanceId), hunter)) return Fail("猎人不属于当前营地。");
            ItemInstance item = FindEquipment();
            if (item?.Data == null) return Fail("该装备已经不在猎人装备栏中。");

            cancellationToken.ThrowIfCancellationRequested();
            hunter.Equipment.Remove(item);
            hunter.EquippedItemIds ??= new List<string>();
            int savedIndex = hunter.EquippedItemIds.IndexOf(item.Data.ContentId);
            if (savedIndex >= 0)
                hunter.EquippedItemIds.RemoveAt(savedIndex);
            settlement.AddStoredEquipment(item.Data, 1);

            int storedCount = settlement.GetStoredEquipment(item.Data);
            Result = new SettlementEquipmentCommandResult(true, string.Empty, hunter.InstanceId, item.Data.itemName, storedCount);
            eventOutbox.Stage(new HunterEquipmentChangedEvent { HunterId = hunter.InstanceId, ItemName = item.Data.itemName, Equipped = false, StoredCount = storedCount });
            eventOutbox.Stage(new SettlementTransactionCommittedEvent { TransactionId = $"unequip:{hunter.InstanceId}:{equipmentInstanceId}", Kind = SettlementTransactionKind.Equipment });
            return UniTask.FromResult(ActionOutcome.Success());
        }

        private ItemInstance FindEquipment()
        {
            if (hunter.Equipment == null) return null;
            foreach (ItemInstance candidate in hunter.Equipment)
                if (candidate != null && candidate.InstanceId == equipmentInstanceId)
                    return candidate;
            return null;
        }

        private UniTask<ActionOutcome> Fail(string reason)
        {
            Result = SettlementEquipmentCommandResult.Failed(reason);
            return UniTask.FromResult(ActionOutcome.Failure(reason));
        }
    }
}
