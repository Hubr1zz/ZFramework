using HuntingInDarkness.Data;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Settlement
{
    /// <summary>
    /// 旧 2D 猎人详情中的只读装备行。装备修改只允许通过 3D 装备桌提交正式命令。
    /// </summary>
    public class HunterEquipmentSlotView : MonoBehaviour
    {
        [SerializeField] private Text   _label;          // 槽位文字
        [SerializeField] private Button _unequipButton;  // 卸下按钮（空格时隐藏）

        /// <summary>用某一格的数据初始化此行。</summary>
        public void Bind(int index, HunterInstance hunter)
        {
            bool equipped = index < hunter.Equipment.Count;

            _label.text = equipped
                ? $"[{index + 1}] {hunter.Equipment[index].Data.itemName}"
                : $"[{index + 1}] —（空）";

            _unequipButton.gameObject.SetActive(false);
            _unequipButton.onClick.RemoveAllListeners();
        }
    }
}
