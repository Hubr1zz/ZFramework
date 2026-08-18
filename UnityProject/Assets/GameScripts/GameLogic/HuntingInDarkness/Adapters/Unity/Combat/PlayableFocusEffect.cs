using System.Collections.Generic;
using Core;
using Cysharp.Threading.Tasks;
using GameplayBase;
using GameplayBase.Card.CharacterActionCard;
using GameplayBase.Card.Effect;
using GameplayBase.CombatSystem;
using HuntingInDarkness.GameCore.Cards;

namespace HuntingInDarkness.Combat
{
    [System.Serializable]
    public sealed class PlayableFocusEffectData : CharacterActionCardEffectData
    {
        public override CharacterActionCardEffect CreateRuntime() => new PlayableFocusEffect();
    }

    public sealed class PlayableFocusEffect : CharacterActionCardEffect
    {
        public override string Description => "投掷两枚三色骰并获得对应灵感";
        public override TargetType TargetType => TargetType.Self;

        public override bool CanExecute(ActionCardContext context) => context?.GameContext is ICombatProvider combatProvider && combatProvider.CombatManager?.InputProvider != null && context.GameContext is ICombatActionCommands;

        public override void Execute(ActionCardContext context) => ExecuteAsync(context).Forget();

        public override async UniTask ExecuteAsync(ActionCardContext context)
        {
            if (context?.GameContext is not ICombatProvider combatProvider || context.GameContext is not ICombatActionCommands commands) return;
            IPlayerInputProvider input = combatProvider.CombatManager?.InputProvider;
            if (input == null) return;

            int roll = await input.RequestRoll("投掷两枚三色专注骰", FocusInspirationRules.OutcomeCount);
            (CombatInspirationColor first, CombatInspirationColor second) = FocusInspirationRules.ResolveRoll(roll);
            InspirationGain firstGain = await commands.AddCombatInspirationAsync(context.SourceCharacterId, first);
            InspirationGain secondGain = await commands.AddCombatInspirationAsync(context.SourceCharacterId, second);

            var lines = new List<string>
            {
                $"第一枚：{CombatInspirationPresentation.GetName(first)}（{Describe(firstGain.Result)}）",
                $"第二枚：{CombatInspirationPresentation.GetName(second)}（{Describe(secondGain.Result)}）",
                $"思维区：{commands.GetCombatInspirationTokens(context.SourceCharacterId).Count}/{commands.GetCombatInspirationCapacity(context.SourceCharacterId)}"
            };
            await input.ShowResult(string.Join("\n", lines));
        }

        private static string Describe(InspirationGainResult result)
        {
            return result switch
            {
                InspirationGainResult.Added => "已加入",
                InspirationGainResult.Replaced => "已替换",
                InspirationGainResult.Discarded => "已丢弃",
                _ => "未生效"
            };
        }
    }
}
