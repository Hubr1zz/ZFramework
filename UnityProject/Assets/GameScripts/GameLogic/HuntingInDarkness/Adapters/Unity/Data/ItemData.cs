using System.Collections.Generic;
using UnityEngine;

namespace HuntingInDarkness.Data
{
    // ═══════════════════════════════════════════════════════════════
    // Item 数据模型
    // ═══════════════════════════════════════════════════════════════

    public enum ItemType { Resource, Weapon, Armor, Consumable }

    /// <summary>物品词条标签（用于发明/工坊配方筛选）</summary>
    public enum ItemTag
    {
        Bone, Stone, Hide, Organ,            // 怪物素材
        Herb, Wood, Mineral,                 // 环境素材
        Gear, Weapon, Shield, Cloth,         // 装备类型
        Rare, Cursed, Sacred                 // 稀有/特殊
    }

    /// <summary>
    /// 物品 ScriptableObject 模板（资源/装备/消耗品）。
    /// 通过词条标签系统参与发明/工坊配方筛选。
    /// </summary>
    [CreateAssetMenu(fileName = "NewItem", menuName = "HuntingInDarkness/Item")]
    public class ItemData : ScriptableObject
    {
        [Header("基础")]
        [SerializeField, Tooltip("稳定内容 ID。写入存档和跨表引用时使用；旧资产为空时暂以资产名兼容。")]
        private string contentId;
        public string itemName = "新物品";
        public ItemType itemType = ItemType.Resource;
        [TextArea] public string description;

        [Header("词条标签")]
        public List<ItemTag> tags = new();

        [Tooltip("稳定规则关键词。使用小写短语；旧 ItemTag 会由 Adapter 自动映射为同名关键词。")]
        public List<string> keywords = new();

        [Header("武器属性（itemType = Weapon 时生效）")]
        public WeaponStats weaponStats;

        [Header("防具属性（itemType = Armor 时生效）")]
        public ArmorStats armorStats;

        [Header("资源数量（堆叠上限）")]
        public int stackLimit = 99;

        public string ContentId
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(contentId)) return contentId.Trim();
                if (!string.IsNullOrWhiteSpace(name)) return name.Trim();
                return itemName?.Trim() ?? string.Empty;
            }
        }

        public void ConfigureContentId(string value) => contentId = value?.Trim() ?? string.Empty;

        /// <summary>是否含有指定词条标签</summary>
        public bool HasTag(ItemTag tag) => tags.Contains(tag);
    }

    // ─── 武器属性 ────────────────────────────────────────────────

    [System.Serializable]
    public class WeaponStats
    {
        public int speed    = 1; // 速度（行动前置条件）
        public int power    = 1; // 威力（基础伤害）
        public int accuracy = 0; // 精准加成
        public int range    = 1; // 攻击范围（格）
        [TextArea] public string specialRule; // 特殊规则文字
    }

    // ─── 防具属性 ────────────────────────────────────────────────

    [System.Serializable]
    public class ArmorStats
    {
        public int armorHead = 0;
        public int armorBody = 0;
        public int armorArms = 0;
        public int armorLegs = 0;
    }

    // ─── 工坊配方 ────────────────────────────────────────────────

    [System.Serializable]
    public class CraftRecipe
    {
        public string recipeName = "新配方";
        public List<RecipeIngredient> ingredients = new();
        public ItemData outputItem;
        public int outputCount = 1;

        [Header("解锁条件")]
        public InventionData requiredInvention; // 可为空（基础配方无需发明）
        public bool unlockedByMaterial = false; // 收集到指定素材时解锁
        public string requiredWorkshopId;
    }

    [System.Serializable]
    public class RecipeIngredient
    {
        public ItemData item;
        public int count = 1;
    }

    // ─── 物品运行时实例 ──────────────────────────────────────────

    /// <summary>
    /// 物品运行时实例（主要用于猎人装备槽/收集物）。
    /// 资源存储用 Dictionary&lt;ItemData, int&gt; 计数，不用实例。
    /// </summary>
    [System.Serializable]
    public class ItemInstance
    {
        private static int _nextId = 1000;

        public int      InstanceId;
        public ItemData Data;
        public int      Count = 1; // 叠加数量

        public ItemInstance(ItemData data, int count = 1)
        {
            InstanceId = _nextId++;
            Data = data;
            Count = count;
        }
    }
}
