using Sirenix.OdinInspector;
using TMPro;
using UI;
using UnityEngine;

using Cards3D;

namespace CardTest3D
{
    /// <summary>
    /// 猎人3D卡牌面板。附于猎人根节点之上，XZ 平面展开，俯视可读。
    ///
    /// 布局（从左至右，X 方向）：
    ///   [装备区 EquipGridSize] [数据区 DataGridSize] [行动卡区 ActionGridSize]
    ///
    /// 运行时在 Inspector 修改槽尺寸/网格大小可即时预览（Odin OnValueChanged → RebuildLayout）。
    /// 注意：RebuildLayout 会销毁已有网格，已放入的卡牌不随其重排。
    /// </summary>
    public class HunterPanel3D : MonoBehaviour
    {
        // ─── 槽尺寸（Odin 动态预览）──────────────────────────────────────────

        [BoxGroup("槽尺寸"), LabelText("槽宽"),  OnValueChanged("RebuildLayout")]
        public float SlotW = 0.50f;

        [BoxGroup("槽尺寸"), LabelText("槽高"),  OnValueChanged("RebuildLayout")]
        public float SlotH = 0.70f;

        [BoxGroup("槽尺寸"), LabelText("槽间距"), OnValueChanged("RebuildLayout")]
        public float SlotGap = 0.05f;

        // ─── 布局（Odin 动态预览）────────────────────────────────────────────

        [BoxGroup("布局"), LabelText("区域间距"),   OnValueChanged("RebuildLayout")]
        public float SectionGap = 0.18f;

        [BoxGroup("布局"), LabelText("行动卡区"),   OnValueChanged("RebuildLayout")]
        public Vector2Int ActionGridSize = new(3, 3);

        [BoxGroup("布局"), LabelText("数据区"),     OnValueChanged("RebuildLayout")]
        public Vector2Int DataGridSize = new(1, 2);

        [BoxGroup("布局"), LabelText("装备区"),     OnValueChanged("RebuildLayout")]
        public Vector2Int EquipGridSize = new(3, 3);

        [BoxGroup("布局"), LabelText("面板高度偏移")]
        public float HeightOffset = 0.02f;

        // ─── 只读引用 ─────────────────────────────────────────────────────────
        public SlotGrid ActionGrid { get; private set; }
        public SlotGrid DataGrid   { get; private set; }
        public SlotGrid EquipGrid  { get; private set; }

        bool _built;

        // ─── 工厂 ─────────────────────────────────────────────────────────────

        public static HunterPanel3D Create(Transform parent, Vector3 localPos)
        {
            var go    = new GameObject("HunterPanel3D");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var panel = go.AddComponent<HunterPanel3D>();
            panel.Build();
            return panel;
        }

        // ─── 构建 & 重建 ──────────────────────────────────────────────────────

        [Button("重建面板"), BoxGroup("调试")]
        public void Build()
        {
            // 销毁旧子节点（除了自身挂载的组件）
            for (int i = transform.childCount - 1; i >= 0; i--)
                Destroy(transform.GetChild(i).gameObject);
            ActionGrid = DataGrid = EquipGrid = null;

            _built = true;
            BuildLayout();
        }

        private void RebuildLayout()
        {
            if (!_built) return;
            Build();
        }

        private void BuildLayout()
        {
            float equipW  = SectionWidth(EquipGridSize.x);
            float dataW   = SectionWidth(DataGridSize.x);
            float actionW = SectionWidth(ActionGridSize.x);
            float totalW  = equipW + SectionGap + dataW + SectionGap + actionW;

            float curX = -totalW * 0.5f;

            // 装备区（左）
            EquipGrid = SlotGrid.Create(transform,
                new Vector3(curX + equipW * 0.5f, HeightOffset, 0f),
                EquipGridSize.x, EquipGridSize.y,
                SlotW, SlotH, SlotGap, false,
                CardCategory.Equipment);
            EquipGrid.AddLabel("装备");
            curX += equipW + SectionGap;

            // 数据区（中）
            DataGrid = SlotGrid.Create(transform,
                new Vector3(curX + dataW * 0.5f, HeightOffset, 0f),
                DataGridSize.x, DataGridSize.y,
                SlotW, SlotH, SlotGap, false,
                CardCategory.Any);
            DataGrid.AddLabel("数据");
            curX += dataW + SectionGap;

            // 行动卡区（右）
            ActionGrid = SlotGrid.Create(transform,
                new Vector3(curX + actionW * 0.5f, HeightOffset, 0f),
                ActionGridSize.x, ActionGridSize.y,
                SlotW, SlotH, SlotGap, false,
                CardCategory.HunterAction);
            ActionGrid.AddLabel("行动");

            BuildBackground(totalW);
        }

        private float SectionWidth(int cols)
            => cols * SlotW + (cols - 1) * SlotGap;

        private void BuildBackground(float totalW)
        {
            float maxRows = Mathf.Max(
                EquipGridSize.y, DataGridSize.y, ActionGridSize.y);
            float totalH = maxRows * SlotH + (maxRows - 1) * SlotGap + 0.5f;

            var bg = GameObject.CreatePrimitive(PrimitiveType.Quad);
            bg.name = "PanelBG";
            bg.transform.SetParent(transform, false);
            bg.transform.localPosition = new Vector3(0f, HeightOffset - 0.002f, 0f);
            bg.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            bg.transform.localScale    = new Vector3(totalW + 0.25f, totalH, 1f);
            Destroy(bg.GetComponent<Collider>());
            var mr = bg.GetComponent<MeshRenderer>();
            mr.material = new Material(mr.sharedMaterial);
            mr.material.color = new Color(0.13f, 0.13f, 0.17f);

            // 面板标题
            var go = new GameObject("PanelTitle");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, HeightOffset, totalH * 0.5f - 0.05f);
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text      = "猎人面板";
            tmp.fontSize  = 0.14f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color     = new Color(0.75f, 0.72f, 0.65f);
            tmp.rectTransform.sizeDelta = new Vector2(totalW, 0.25f);
        }
    }
}
