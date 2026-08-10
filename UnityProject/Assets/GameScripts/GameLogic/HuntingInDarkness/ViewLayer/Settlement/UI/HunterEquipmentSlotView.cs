using HuntingInDarkness.Data;
using HuntingInDarkness.Settlement;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Settlement
{
    /// <summary>
    /// 装备栏单格视图：Prefab 模板，由 <see cref="HunterDetailPanel"/> 按数据实例化并 Bind。
    /// 子物体（标签 / 卸下按钮）在 Prefab 中搭好，引用 Inspector 注入。
    /// </summary>
    public class HunterEquipmentSlotView : MonoBehaviour
    {
        [SerializeField] private Text   _label;          // 槽位文字
        [SerializeField] private Button _unequipButton;  // 卸下按钮（空格时隐藏）

        /// <summary>用某一格的数据初始化此行。</summary>
        public void Bind(int index, HunterInstance hunter, SettlementManager mgr, System.Action onChanged)
        {
            bool equipped = index < hunter.Equipment.Count;

            _label.text = equipped
                ? $"[{index + 1}] {hunter.Equipment[index].Data.itemName}"
                : $"[{index + 1}] —（空）";

            _unequipButton.gameObject.SetActive(equipped);
            _unequipButton.onClick.RemoveAllListeners();
            if (equipped)
            {
                int idx = index;
                _unequipButton.onClick.AddListener(() =>
                {
                    mgr.HunterMgmt.UnequipItem(hunter, idx);
                    onChanged?.Invoke();
                });
            }
        }
    }
}
