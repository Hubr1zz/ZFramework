using System.Collections.Generic;
using System.Linq;
using GameplayBase;
using GameplayBase.Card.BossActionCard;
using GameplayBase.Card.CharacterActionCard;
using GameplayBase.CombatSystem;
using GameplayBase.CombatSystem.Cards.FlipConditions;
using HuntingInDarkness.GameCore.Cards;
using SO.Boss.ActionCard;
using UI;
using UnityEngine;

namespace Core
{
    /// <summary>
    /// 运行时卡牌实例。从 CardData 模板创建，持有运行时状态。
    /// </summary>
    public class CharacterActionCardInstance : ICharacterActionCardInstanceState
    {
        // ─── 静态ID分配 ───
        private static int _nextId = 1;
        public static void ResetIdCounter() => _nextId = 1;

        // ─── 身份 ───
        public int InstanceId { get; }
        private readonly ActionCardState _domainState;
        public int OwnerCharacterId
        {
            get => _domainState.OwnerId;
            set => _domainState.OwnerId = value;
        }
        public CharacterActionCardData Template { get; }
        public ActionCardDefinition Definition { get; }
        public IReadOnlyList<ActionCardCostDefinition> Costs => Definition.Costs;

        // ─── 状态 ───
        public CardFace CurrentFace => (CardFace)_domainState.Face;

        // ─── 运行时效果 & 条件（从模板工厂化） ───
        public List<CharacterActionCardEffect> FaceUpEffects { get; } = new();
        public List<CharacterActionCardEffect> FaceDownEffects { get; } = new();
        public List<IFlipCondition> FlipConditions { get; } = new();    // 正→背
        public List<IFlipCondition> RestoreConditions { get; } = new(); // 背→正

        // ─── ICardInstanceState 实现 ───
        public string CardName => Template.cardName;
        public bool CanPlay => _domainState.CanPlay;
        public bool CanFlip => false;    // 由外部 Evaluator 判定
        public bool CanRestore => CurrentFace == CardFace.FaceDown && RestoreConditions.Any(condition => condition.Timing == FlipTriggerTiming.OnPayCost);
        public bool CanDiscard => _domainState.CanDiscard;
        public string FaceUpDescription => Template.faceUpDescription;
        public string FaceDownDescription => Template.faceDownDescription;
        public int TimePointCost
        {
            get
            {
                foreach (ActionCardCostDefinition cost in Costs)
                {
                    if (cost.Kind == ActionCardCostKind.TimePoint)
                        return cost.Amount;
                }
                return 0;
            }
        }
        public bool IsWillAction => Definition.IsWillAction;
        public string CostDescription => string.Join(" ", Costs.Select(DescribeCost));
        public bool IsAvailableThisTurn => _domainState.IsAvailableThisTurn;

        /// <summary>弃置奖励配置（可能为 null）</summary>
        public BurstRewardData BurstReward => Template.burstReward;

        private static string DescribeCost(ActionCardCostDefinition cost)
        {
            return cost.Kind switch
            {
                ActionCardCostKind.TimePoint => $"时:{cost.Amount}",
                ActionCardCostKind.CombatInspiration => $"{DescribeInspiration(cost.InspirationRequirement)}:{cost.Amount}",
                ActionCardCostKind.Willpower => $"意:{cost.Amount}",
                ActionCardCostKind.FlipOtherCard => $"翻:{cost.Amount}",
                _ => string.Empty
            };
        }

        private static string DescribeInspiration(InspirationRequirement requirement)
        {
            return requirement switch
            {
                InspirationRequirement.Red => "红",
                InspirationRequirement.Blue => "蓝",
                InspirationRequirement.Yellow => "黄",
                _ => "灵"
            };
        }

        // ─── 构造 ───

        public CharacterActionCardInstance(CharacterActionCardData template, int ownerCharacterId)
        {
            InstanceId = _nextId++;
            Template = template;
            Definition = template.CreateRuntimeDefinition();
            _domainState = new ActionCardState(
                InstanceId,
                ownerCharacterId,
                Definition.AllowsBurst,
                Definition.ResetsEachTurn);

            // 从模板构建运行时对象（注入目标/范围规则）
            foreach (var effectData in template.faceUpEffects)
                FaceUpEffects.Add(BuildEffect(effectData));
            foreach (var effectData in template.faceDownEffects)
                FaceDownEffects.Add(BuildEffect(effectData));
            foreach (var condData in template.flipConditions)
                FlipConditions.Add(condData.CreateRuntime());
            foreach (var condData in template.restoreConditions)
                RestoreConditions.Add(condData.CreateRuntime());
        }

        /// <summary>工厂化一个效果运行时并注入其目标/范围规则。</summary>
        private static CharacterActionCardEffect BuildEffect(CharacterActionCardEffectData data)
        {
            var effect = data.CreateRuntime();
            if (effect != null) effect.Targeting = data.targeting;
            return effect;
        }

        // ─── 翻面操作（仅修改状态，事件由 Resolver 发送） ───

        public void SetFace(CardFace newFace)
        {
            _domainState.SetFace((ActionCardFace)newFace);
        }

        public void MarkUsed() => _domainState.MarkUsed();

        public void ResetForNewTurn() => _domainState.ResetForNewTurn();
    }
}
