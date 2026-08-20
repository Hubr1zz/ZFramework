using System.Collections.Generic;
using HuntingInDarkness.GameCore.Settlement;
using UnityEngine;

namespace HuntingInDarkness.Data
{
    // ═══════════════════════════════════════════════════════════════
    // Hunter 数据模型
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// 猎人 ScriptableObject 模板 — 策划配置初始属性。
    /// 运行时用 HunterInstance。
    /// </summary>
    [CreateAssetMenu(fileName = "NewHunter", menuName = "HuntingInDarkness/Hunter Template")]
    public class HunterData : ScriptableObject
    {
        [Header("基础")]
        [SerializeField, Tooltip("稳定内容 ID。写入存档和跨表引用时使用；旧资产为空时暂以资产名兼容。")]
        private string contentId;
        public string hunterName = "新猎人";

        [Header("初始战斗属性")]
        public HunterCombatStats initialStats = new();

        [Header("初始意志/命运")]
        public int initialWillpower  = 2; // 意志点上限
        public int initialLuck       = 0; // 命运值
        public int initialInsanity   = 0; // 压抑值

        [Header("初始装备")]
        public List<ItemData> startingEquipment = new();

        [Header("特性/症状")]
        public List<string> startingTraits   = new();
        public List<string> startingAilments = new();

        public string ContentId
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(contentId)) return contentId.Trim();
                if (!string.IsNullOrWhiteSpace(name)) return name.Trim();
                return hunterName?.Trim() ?? string.Empty;
            }
        }

        public bool HasExplicitContentId => !string.IsNullOrWhiteSpace(contentId);
        public void ConfigureContentId(string value) => contentId = value?.Trim() ?? string.Empty;
    }

    // ─── 战斗属性 ────────────────────────────────────────────────

    [System.Serializable]
    public class HunterCombatStats : HunterStats { }

    // ─── 部位血量 ────────────────────────────────────────────────

    [System.Serializable]
    public class HunterHitPoints : HuntingInDarkness.GameCore.Settlement.HunterHitPoints { }

    // ─── 猎人运行时实例 ──────────────────────────────────────────

    /// <summary>
    /// 猎人运行时状态。从 HunterData 模板创建，存入 SettlementInstance。
    /// 所有字段参与 JsonUtility 序列化用于存档。
    /// </summary>
    [System.Serializable]
    public class HunterInstance : HunterState
    {
        // ─── 装备栏（9格）───

        [System.NonSerialized]
        public List<ItemInstance> Equipment = new(); // 运行时用，存档用 EquipmentIds

        public List<string> EquippedItemIds = new(); // 稳定 ContentId，用于读档后恢复 ItemData 引用

        [HideInInspector]
        public List<string> EquippedItemNames = new(); // 旧存档兼容；内容迁移后保持为空

        // ─── 战斗状态 ───

        [System.NonSerialized]
        public List<ItemInstance> Collectibles = new(); // 本次狩猎采集物

        // ─── 构造 ───

        private static int _nextId = 100;

        public HunterInstance(HunterData template, int id = -1)
        {
            InstanceId = id >= 0 ? id : _nextId++;
            OriginTemplateId = template != null ? template.ContentId : string.Empty;
            Name       = template != null ? template.hunterName : "猎人";

            if (template != null)
            {
                HunterCombatStats s = template.initialStats ?? new HunterCombatStats();
                Stats = new HunterCombatStats
                {
                    strength = s.strength,
                    accuracy = s.accuracy,
                    evasion  = s.evasion,
                    movement = s.movement,
                    luck     = s.luck,
                    speed    = s.speed,
                    armorHead = s.armorHead,
                    armorBody = s.armorBody,
                    armorArms = s.armorArms,
                    armorLegs = s.armorLegs
                };

                Willpower    = template.initialWillpower;
                WillpowerMax = template.initialWillpower;
                Luck         = template.initialLuck;
                Insanity     = template.initialInsanity;

                Traits = template.startingTraits != null ? new List<string>(template.startingTraits) : new List<string>();
                Ailments = template.startingAilments != null ? new List<string>(template.startingAilments) : new List<string>();

                if (template.startingEquipment != null)
                    foreach (ItemData item in template.startingEquipment)
                    {
                        if (item == null || item.itemType == ItemType.Resource || Equipment.Count >= EquipmentRules.MaximumEquipmentCount) continue;
                        Equipment.Add(new ItemInstance(item));
                        EquippedItemIds.Add(item.ContentId);
                    }
            }

            // 部位血量初始值
            MaxHP = new HunterHitPoints();
            HP    = new HunterHitPoints { head = MaxHP.head, body = MaxHP.body,
                                          arms = MaxHP.arms, legs = MaxHP.legs };
        }

    }
}
