using UnityEngine;

namespace Cards3D
{
    [CreateAssetMenu(fileName = "EquipData", menuName = "CardTactics/Test/EquipmentCardData")]
    public class EquipmentCardData : ScriptableObject
    {
        public string itemName   = "装备";
        [TextArea(2, 3)] public string description = "";
        public string slotType   = "武器"; // 武器/护甲/饰品 等
    }
}
