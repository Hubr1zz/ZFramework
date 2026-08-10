using Cards3D;
using UnityEngine;

namespace UI
{
    /// <summary>
    /// 战斗信息面板的三区域布局构建器（思想 / 行动 / 装备）。
    /// 供 EntityCreator 程序化回退路径在没有 Prefab 时即时搭建一套等价面板；
    /// Prefab 路径则由策划在编辑器里预放好同名结构、手动连到 CharacterEntity。
    ///
    /// 沿 +Z 自上而下：思想（占位，3×2=6）在上，行动（动态扩行）居中，装备（占位，3×3=9）在下。
    /// </summary>
    public static class CombatPanelLayout
    {
        public const float SlotGap     = 0.15f;
        public const int   MaxCols     = 3;
        public const int   ThoughtRows = 2;   // 思想区域上限 6 格
        public const int   EquipRows   = 3;   // 装备区域上限 9 格
        public const float RegionGap   = 0.7f;

        public struct Regions
        {
            public GameObject PanelRoot;
            public SlotGrid   Thought;
            public SlotGrid   Action;
            public SlotGrid   Equip;
        }

        /// <summary>在 parent 下建一个 InfoPanel 根 + 三个区域 SlotGrid（行动区初始 1 行，填卡时再扩）。</summary>
        public static Regions BuildRegions(Transform parent)
        {
            var panelRoot = new GameObject("InfoPanel");
            panelRoot.transform.SetParent(parent, false);

            float rowStep     = CardView3D.CH + SlotGap;
            float actionHalf  = 1          * rowStep * 0.5f;
            float thoughtHalf = ThoughtRows * rowStep * 0.5f;
            float equipHalf   = EquipRows   * rowStep * 0.5f;

            float thoughtZ =  actionHalf + RegionGap + thoughtHalf;
            float equipZ   = -(actionHalf + RegionGap + equipHalf);

            var thought = SlotGrid.Create(
                panelRoot.transform, new Vector3(0f, 0f, thoughtZ),
                MaxCols, ThoughtRows, CardView3D.CW, CardView3D.CH, SlotGap,
                false, CardCategory.Any);
            thought.AddLabel("思想");

            var action = SlotGrid.Create(
                panelRoot.transform, Vector3.zero,
                MaxCols, 1, CardView3D.CW, CardView3D.CH, SlotGap,
                false, CardCategory.HunterAction);
            action.AddLabel("行动");

            var equip = SlotGrid.Create(
                panelRoot.transform, new Vector3(0f, 0f, equipZ),
                MaxCols, EquipRows, CardView3D.CW, CardView3D.CH, SlotGap,
                false, CardCategory.Any);
            equip.AddLabel("装备");

            return new Regions
            {
                PanelRoot = panelRoot,
                Thought   = thought,
                Action    = action,
                Equip     = equip
            };
        }
    }
}
