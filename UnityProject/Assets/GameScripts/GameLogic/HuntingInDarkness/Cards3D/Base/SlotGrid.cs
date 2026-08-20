using System.Collections.Generic;
using Sirenix.OdinInspector;
using TMPro;
using UI;
using UnityEngine;

namespace Cards3D
{
    /// <summary>
    /// NxM 卡槽网格。槽间距由 Gap 保证，不会产生重叠。
    /// AutoExpand = true 时，TryPlaceCard 如果全部槽已满，会追加新列再放置。
    ///
    /// 运行时在 Inspector 修改 Columns/Rows/SlotW/SlotH/Gap 可即时预览（Odin OnValueChanged）。
    /// 注意：动态调整尺寸后已有卡牌不会随槽重排，暂无自动重布局。
    /// </summary>
    public class SlotGrid : MonoBehaviour
    {
        // ─── 配置（Odin 动态预览） ────────────────────────────────────────────

        [BoxGroup("网格"), LabelText("列数"), OnValueChanged("RebuildGrid"), MinValue(1)]
        public int Columns = 3;

        [BoxGroup("网格"), LabelText("行数"), OnValueChanged("RebuildGrid"), MinValue(1)]
        public int Rows = 3;

        [BoxGroup("网格"), LabelText("槽宽"), OnValueChanged("RebuildGrid")]
        public float SlotW = 0.55f;

        [BoxGroup("网格"), LabelText("槽高"), OnValueChanged("RebuildGrid")]
        public float SlotH = 0.77f;

        [BoxGroup("网格"), LabelText("间距"), OnValueChanged("RebuildGrid")]
        public float Gap = 0.05f;

        [BoxGroup("网格"), LabelText("自动扩展列")]
        public bool AutoExpand = false;

        [BoxGroup("过滤"), LabelText("允许类别")]
        public CardCategory[] AcceptedCategories = { CardCategory.Any };

        [BoxGroup("交互"), LabelText("允许拖出卡牌")]
        public bool OccupantsDraggable = true;

        // ─── 只读状态 ─────────────────────────────────────────────────────────
        readonly List<CardSlot> _slots = new();
        public IReadOnlyList<CardSlot> Slots => _slots;

        bool _built;

        // ─── 生命周期 ─────────────────────────────────────────────────────────

        private void Start()
        {
            // 场景预放置时：Unity 反序列化完成后才执行 Start，此时属性已就绪可安全 Build
            if (!_built) Build();
        }

        // ─── 工厂 ─────────────────────────────────────────────────────────────

        public static SlotGrid Create(
            Transform parent, Vector3 localPos,
            int columns, int rows,
            float slotW, float slotH, float gap,
            bool autoExpand = false,
            params CardCategory[] accepted)
        {
            var go   = new GameObject("SlotGrid");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;

            var grid = go.AddComponent<SlotGrid>();
            grid.Columns            = columns;
            grid.Rows               = rows;
            grid.SlotW              = slotW;
            grid.SlotH              = slotH;
            grid.Gap                = gap;
            grid.AutoExpand         = autoExpand;
            grid.AcceptedCategories = accepted.Length > 0 ? accepted : new[] { CardCategory.Any };
            grid.Build();
            return grid;
        }

        // ─── 构建 & 重建 ──────────────────────────────────────────────────────

        static void SafeDestroy(UnityEngine.Object obj)
        {
            if (Application.isPlaying) Destroy(obj);
            else DestroyImmediate(obj);
        }

        [Button("重建网格"), BoxGroup("调试")]
        public void Build()
        {
            foreach (var slot in _slots)
                if (slot != null) SafeDestroy(slot.gameObject);
            _slots.Clear();
            _built = true;

            for (int r = 0; r < Rows; r++)
                for (int c = 0; c < Columns; c++)
                    CreateSlotAt(c, r);
        }
        
        [Button("Clean Child OBJ"), BoxGroup("调试")]
        public void CleanChildren()
        {
            // 销毁所有子物体（Slot）
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if (child != null)
                    SafeDestroy(child.gameObject);
            }

            // 清空槽缓存
            _slots.Clear();
        }

        private void RebuildGrid()
        {
            if (!_built) return;
            Build();
        }

        /// <summary>
        /// 在网格中创建一个槽并注册。
        /// 关键：先 AddComponent（触发 Awake），再赋值属性，最后调用 Build()。
        /// 这样避免了 Awake 内用默认值构建导致的尺寸错误/重叠问题。
        /// </summary>
        private CardSlot CreateSlotAt(int col, int row)
        {
            // stepX/stepZ = 槽尺寸 + 间距，确保相邻槽不重叠
            float stepX = SlotW + Gap;
            float stepZ = SlotH + Gap;
            float x = (col - (Columns - 1) * 0.5f) * stepX;
            float z = (row - (Rows    - 1) * 0.5f) * stepZ;

            var go = new GameObject($"Slot_{col}_{row}");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(x, 0f, z);

            var slot = go.AddComponent<CardSlot>(); // Awake: 注册 AllSlots，不 Build
            slot.SlotW              = SlotW;        // 赋属性（此时才安全）
            slot.SlotH              = SlotH;
            slot.AcceptedCategories = AcceptedCategories;
            slot.AllowOccupantDrag  = OccupantsDraggable;
            slot.Build();                           // 属性齐全后构建视觉

            _slots.Add(slot);
            return slot;
        }

        // ─── 放置 API ─────────────────────────────────────────────────────────

        /// <summary>找到第一个可接受该卡的空槽放置。AutoExpand 时满格则新增一列。</summary>
        public bool TryPlaceCard(CardView3D card)
        {
            foreach (var slot in _slots)
                if (slot.CanAccept(card)) { slot.PlaceCard(card); return true; }

            if (!AutoExpand) return false;

            int newCol = Columns;
            Columns++;
            for (int r = 0; r < Rows; r++)
            {
                var s = CreateSlotAt(newCol, r);
                if (s.CanAccept(card)) { s.PlaceCard(card); return true; }
            }
            return false;
        }

        /// <summary>返回第一个空槽（动画目标查询用）；AutoExpand 时若满则预先扩展。</summary>
        public CardSlot GetFirstEmptySlot()
        {
            foreach (var s in _slots)
                if (s.OccupantCard == null) return s;

            if (!AutoExpand) return null;

            int newCol = Columns;
            Columns++;
            for (int r = 0; r < Rows; r++)
            {
                var s = CreateSlotAt(newCol, r);
                if (s.OccupantCard == null) return s;
            }
            return null;
        }

        // ─── 标签 ─────────────────────────────────────────────────────────────

        public void AddLabel(string text, float zOffset = 0.18f)
        {
            var go = new GameObject("GridLabel");
            go.transform.SetParent(transform, false);
            float halfZ = (Rows - 1) * 0.5f * (SlotH + Gap) + SlotH * 0.5f + zOffset;
            go.transform.localPosition = new Vector3(0f, 0.01f, halfZ);
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text      = text;
            tmp.fontSize  = 0.18f;   // 调大提升可读性
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color     = new Color(0.88f, 0.84f, 0.75f);
            tmp.rectTransform.sizeDelta = new Vector2(Columns * (SlotW + Gap) + 0.5f, 0.30f);
        }
    }
}
