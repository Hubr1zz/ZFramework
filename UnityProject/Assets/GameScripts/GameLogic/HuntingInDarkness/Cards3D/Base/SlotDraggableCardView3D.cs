using UnityEngine;

namespace Cards3D
{
    /// <summary>可拖入 CardSlot 的 3D 卡牌基类；统一处理候选槽高亮、吸附与原位回退。</summary>
    public abstract class SlotDraggableCardView3D : CardView3D
    {
        [SerializeField, Min(0.05f)] private float dropSearchRadius = 0.55f;

        private CardSlot hoverSlot;
        private CardSlot originSlot;

        protected override void OnBeginDrag()
        {
            originSlot = CurrentSlot != null && !CurrentSlot.Stackable ? CurrentSlot : null;
            originSlot?.ClearCard();
        }

        protected override void OnDragFrame()
        {
            CardSlot nearest = null;
            float nearestDistance = dropSearchRadius;
            foreach (CardSlot slot in CardSlot.AllSlots)
            {
                if (slot == null || !CanDropInto(slot)) continue;
                float distance = Vector2.Distance(new Vector2(transform.position.x, transform.position.z), new Vector2(slot.transform.position.x, slot.transform.position.z));
                if (distance >= nearestDistance) continue;
                nearest = slot;
                nearestDistance = distance;
            }

            if (hoverSlot == nearest) return;
            hoverSlot?.SetHighlight(false);
            hoverSlot = nearest;
            hoverSlot?.SetHighlight(true);
        }

        protected override void OnEndDrag()
        {
            CardSlot target = hoverSlot;
            hoverSlot?.SetHighlight(false);
            hoverSlot = null;

            if (target != null && CanDropInto(target))
            {
                transform.SetParent(_preDragParent, true);
                target.PlaceCard(this, _preDragParent);
                originSlot = null;
                OnPlacedInSlot(target);
                return;
            }

            if (TryHandleUnslottedDrop())
            {
                originSlot = null;
                return;
            }

            if (originSlot == null || !originSlot.CanAccept(this)) return;
            transform.SetParent(_preDragParent, true);
            originSlot.PlaceCard(this, _preDragParent);
            originSlot = null;
        }

        protected virtual bool CanDropInto(CardSlot slot) => slot.CanAccept(this);

        protected virtual void OnPlacedInSlot(CardSlot slot) { }

        /// <summary>返回 true 表示子类已自行接管父级和落点；false 时由 CardView3D 回到拖拽前位置。</summary>
        protected virtual bool TryHandleUnslottedDrop() => false;
    }
}
