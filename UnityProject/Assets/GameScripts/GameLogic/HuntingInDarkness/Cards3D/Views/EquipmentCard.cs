using TMPro;
using UI;
using UnityEngine;

namespace Cards3D
{
    /// <summary>
    /// 装备卡3D视图。继承 CardView3D，展示装备名/类型/描述。
    /// </summary>
    public class EquipmentCard : CardView3D
    {
        static readonly Color ColCard  = new(0.28f, 0.32f, 0.42f);
        static readonly Color ColHover = new(0.38f, 0.44f, 0.58f);

        public EquipmentCardData Data { get; private set; }
        protected override CardCategory GetDefaultCategory() => CardCategory.Equipment;

        TextMeshPro _nameText;
        TextMeshPro _slotText;
        TextMeshPro _descText;

        // ─── 工厂 ──────────────────────────────────────────────────────────

        public static EquipmentCard Create(EquipmentCardData data, Transform parent, Vector3 localPos)
        {
            var go = new GameObject($"EquipCard_{data.itemName}");
            go.transform.SetParent(parent, false);
            var card = go.AddComponent<EquipmentCard>();
            card.Data       = data;
            card.EnableDrag = true;
            card.InitView(localPos);
            return card;
        }

        // ─── CardView3D ────────────────────────────────────────────────────

        protected override void BuildTextFields()
        {
            float ty = CD * 0.5f + 0.003f;

            _nameText = MakeText("Name",
                new Vector3(0f, ty, CH * 0.36f), 0.115f,
                TextAlignmentOptions.Center,
                new Vector2(CW - 0.08f, 0.18f));

            _slotText = MakeText("Slot",
                new Vector3(0f, ty, CH * 0.14f), 0.08f,
                TextAlignmentOptions.Center,
                new Vector2(CW - 0.08f, 0.14f));

            _descText = MakeText("Desc",
                new Vector3(0f, ty, -CH * 0.08f), 0.075f,
                TextAlignmentOptions.Center,
                new Vector2(CW - 0.10f, 0.46f));
        }

        protected override void ApplyVisuals()
        {
            if (_bodyRenderer == null) return;
            _bodyRenderer.material.color = IsHovered ? ColHover : ColCard;

            if (_nameText == null) return;
            var textColor = new Color(0.85f, 0.88f, 0.95f);
            _nameText.color = textColor;
            _slotText.color = new Color(0.60f, 0.68f, 0.80f);
            _descText.color = new Color(0.70f, 0.72f, 0.78f);

            _nameText.text = Data?.itemName   ?? "";
            _slotText.text = Data?.slotType   ?? "";
            _descText.text = Data?.description ?? "";
        }
    }
}
