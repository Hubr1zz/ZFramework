using System.Collections.Generic;
using Core;
using HuntingInDarkness.GameCore.Cards;
using UnityEngine;

namespace SO.Boss.ActionCard
{
    /// <summary>
    /// 角色行动卡
    /// </summary>
    [CreateAssetMenu(fileName = "", menuName = "CardTactics/Character/ActionCard")]
    public class CharacterActionCardData : ScriptableObject
    {
        [Header("基础信息")]
        public string cardName;
        public string cardId; // 唯一标识模板
        public Sprite cardImage; // 配图
        [TextArea] public string faceUpDescription;
        [TextArea] public string faceDownDescription;

        [Header("时点消耗")]
        [Tooltip("旧资产兼容字段；未配置费用列表时映射为时点费用")]
        public int timePointCost;

        [Header("行动费用")]
        [Tooltip("为空时继续使用旧 timePointCost；支持时点、战斗灵感、意志和特殊费用")]
        [SerializeReference] public List<ActionCardCostData> costs = new();

        [Header("每回合可用状态")]
        [Tooltip("启用后每回合只能使用一次；包含意志费用的行动会自动启用该规则")]
        public bool oncePerTurn;

        [Header("正面效果")]
        [Tooltip("通过子类或工厂创建")]
        [SerializeReference] public List<CharacterActionCardEffectData> faceUpEffects = new();

        [Header("背面效果（通常为空）")]
        [SerializeReference] public List<CharacterActionCardEffectData> faceDownEffects = new();

        [Header("翻面条件（正→背）")]
        [SerializeReference] public List<FlipConditionData> flipConditions = new();

        [Header("恢复条件（背→正）")]
        [SerializeReference] public List<FlipConditionData> restoreConditions = new();

        [Header("弃置换资源（右键交互）")]
        [Tooltip("为空则不可弃置")]
        public BurstRewardData burstReward;

        /// <summary>该卡是否支持弃置换资源</summary>
        public bool IsDiscardable =>
            burstReward != null && burstReward.enabled && !IsWillAction;

        public bool IsWillAction
        {
            get
            {
                foreach (ActionCardCostDefinition cost in CreateCostDefinitions())
                {
                    if (cost.Kind == ActionCardCostKind.Willpower)
                        return true;
                }
                return false;
            }
        }

        public List<ActionCardCostDefinition> CreateCostDefinitions()
        {
            var definitions = new List<ActionCardCostDefinition>();
            if (costs != null && costs.Count > 0)
            {
                foreach (ActionCardCostData cost in costs)
                {
                    ActionCardCostDefinition definition = cost?.CreateRuntime();
                    if (definition != null)
                        definitions.Add(definition);
                }
            }
            else if (timePointCost > 0)
            {
                definitions.Add(new ActionCardCostDefinition(
                    ActionCardCostKind.TimePoint,
                    timePointCost));
            }
            return definitions;
        }

        public ActionCardDefinition CreateRuntimeDefinition()
        {
            List<ActionCardCostDefinition> definitions = CreateCostDefinitions();
            bool isWillAction = definitions.Exists(
                cost => cost.Kind == ActionCardCostKind.Willpower);
            return new ActionCardDefinition(
                cardId,
                definitions,
                oncePerTurn || isWillAction,
                burstReward != null && burstReward.enabled);
        }

        [Header("标签（用于条件筛选、联动等）")]
        public List<string> tags = new();
    }

    [System.Serializable]
    public abstract class ActionCardCostData
    {
        [Min(1)] public int amount = 1;
        public abstract ActionCardCostDefinition CreateRuntime();
    }

    [System.Serializable]
    public sealed class TimePointActionCardCostData : ActionCardCostData
    {
        public override ActionCardCostDefinition CreateRuntime() =>
            new ActionCardCostDefinition(ActionCardCostKind.TimePoint, amount);
    }

    [System.Serializable]
    public sealed class CombatInspirationActionCardCostData : ActionCardCostData
    {
        public override ActionCardCostDefinition CreateRuntime() =>
            new ActionCardCostDefinition(ActionCardCostKind.CombatInspiration, amount);
    }

    [System.Serializable]
    public sealed class WillpowerActionCardCostData : ActionCardCostData
    {
        public override ActionCardCostDefinition CreateRuntime() =>
            new ActionCardCostDefinition(ActionCardCostKind.Willpower, amount);
    }

    [System.Serializable]
    public sealed class FlipOtherCardActionCardCostData : ActionCardCostData
    {
        [Tooltip("为空则任意其他正面行动卡均可作为费用")]
        public string requiredCardTag;

        public override ActionCardCostDefinition CreateRuntime() =>
            new ActionCardCostDefinition(
                ActionCardCostKind.FlipOtherCard,
                amount,
                requiredCardTag);
    }
}
