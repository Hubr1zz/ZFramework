using System.Collections.Generic;
using HuntingInDarkness.GameCore.Settlement;
using UnityEngine;
using UnityEngine.Serialization;

namespace HuntingInDarkness.Data
{
    // ═══════════════════════════════════════════════════════════════
    // Event 数据模型
    // ═══════════════════════════════════════════════════════════════

    public enum GameEventType
    {
        Narrative, // 叙事事件：纯展示+效果，无选择
        Choice,    // 抉择事件：有选项+判定
        Combat     // 战斗事件：触发战斗
    }

    /// <summary>判定类型（用于抉择事件的骰子roll）</summary>
    public enum CheckType
    {
        None,
        Courage,       // 胆识判定
        Luck,          // 幸运判定
        Strength,      // 力量判定
        Evasion,       // 敏捷判定
        Understanding, // 阅历判定
        Custom         // 自定义（值由 customCheckStat 指定）
    }

    public enum EventCheckPresentationKind
    {
        PhysicalDice,
        DrawCards,
        FlipCards,
        OldMaid
    }

    /// <summary>
    /// 游戏事件 ScriptableObject 模板。
    /// 支持：叙事/抉择/战斗三种类型、子事件链、条件判定、结果隐藏/显示。
    /// </summary>
    [CreateAssetMenu(fileName = "NewEvent", menuName = "HuntingInDarkness/Event")]
    public class EventData : ScriptableObject
    {
        [Header("基础")]
        public string    eventName = "新事件";
        public GameEventType eventType = GameEventType.Narrative;

        [Header("文本")]
        [TextArea(3, 6)]
        public string displayText = "【测试事件：此处填写玩家可见的事件描述】";

        [TextArea(2, 4)]
        public string hiddenText  = "【隐藏内容：判定/选择后才揭示的结果描述】";

        [Header("选项（抉择类事件）")]
        public List<EventOption> options = new();

        [Header("直接效果（叙事类/战斗类不需选项时直接执行）")]
        public List<EventEffect> immediateEffects = new();

        [Header("战斗配置（eventType = Combat 时使用）")]
        [FormerlySerializedAs("combatTargetBossName")]
        public string combatEncounterId;

        [Header("子事件链（本事件结束后触发）")]
        public List<EventData> chainedEvents = new();

        [Header("触发条件（满足才会出现在抽牌池中）")]
        public int minYear = 1;  // 最早触发年份
        public int maxYear = 99; // 最晚触发年份（0=无限）
        public int drawWeight = 1; // 抽取权重

        [Header("类别")]
        public EventCategory category = EventCategory.Random;
    }

    public enum EventCategory
    {
        MainStory, // 主线强制事件（特定年份触发）
        Random,    // 随机抽取事件
        Hunt,      // 狩猎阶段专属
        Settlement, // 营地阶段专属
        Scheduled, // 只允许通过 Timeline 延时调度
        Triggered // 只允许由发明、特性等显式入口启动
    }

    // ─── 事件选项 ────────────────────────────────────────────────

    [System.Serializable]
    public class EventOptionCondition
    {
        public EventOptionConditionKind conditionKind;
        public string key = "";
        public string displayName = "";
        public int value;
        public bool inverted;

        public EventOptionConditionDefinition ToDomain() => new EventOptionConditionDefinition(conditionKind, key, value, inverted, displayName);
    }

    [System.Serializable]
    public class EventOption
    {
        [Header("选项文本")]
        public string optionText = "选择此选项";

        [Header("判定（可选）")]
        public CheckType checkType    = CheckType.None;
        public int       checkTarget  = 0; // 成功需满足的值（如：≥3）

        [Header("桌面随机表现")]
        public EventCheckPresentationKind checkPresentation = EventCheckPresentationKind.PhysicalDice;
        [Min(1)] public int checkCount = 1;
        [Min(2)] public int checkSides = 10;
        public string checkDeckId = "";
        public string checkInstruction = "";

        [Header("成功结果")]
        [TextArea] public string successText   = "成功了";
        public List<EventEffect> successEffects = new();
        public List<EventData>   successChain   = new();

        [Header("失败结果（仅在有判定时生效）")]
        [TextArea] public string failText      = "失败了";
        public List<EventEffect> failEffects    = new();
        public List<EventData>   failChain      = new();

        [Header("可用条件（满足才显示此选项）")]
        public bool alwaysAvailable = true;
        public List<EventOptionCondition> conditions = new();
    }

    // ─── 事件效果 ────────────────────────────────────────────────

    [System.Serializable]
    public class EventEffect
    {
        public EventEffectType effectType = EventEffectType.AddResource;
        public string          targetName = ""; // 兼容字段；资源/发明等跨内容引用保存稳定 ContentId
        public int             value      = 1;
        [TextArea] public string description = "";
    }

    public enum EventEffectType
    {
        AddResource,        // 增加资源：targetName=资源名, value=数量
        RemoveResource,     // 减少资源
        AddWillpower,       // 指定猎人增加意志点
        RemoveWillpower,    // 减少意志点
        AddLuck,            // 增加命运值
        AddInsanity,        // 增加压抑值
        AddCourage,         // 增加胆识
        AddUnderstanding,   // 增加知识
        AddTrait,           // 添加特性：targetName=特性名
        AddAilment,         // 添加症状
        KillHunter,         // 猎人死亡判定
        UnlockInvention,    // 解锁发明：targetName=稳定发明 ContentId
        TriggerCombat,      // 触发战斗
        AdvanceYear,        // 推进年份
        ScheduleEvent,      // 安排未来事件：targetName=稳定事件ID, value=延迟年数
        ActivateBloodline   // 激活血脉：targetName=稳定血脉ID
    }
}
