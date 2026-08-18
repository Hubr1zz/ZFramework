using Core;
using Cysharp.Threading.Tasks;
using GameplayBase;
using GameplayBase.Card.CharacterActionCard;
using GameplayBase.CombatSystem;
using HuntingInDarkness.GameCore.Cards;

namespace HuntingInDarkness.Combat
{
    [System.Serializable]
    public sealed class GainCombatInspirationEffectData : CharacterActionCardEffectData
    {
        public CombatInspirationColor color = CombatInspirationColor.Blue;

        public override CharacterActionCardEffect CreateRuntime() => new GainCombatInspirationEffect(color);
    }

    public sealed class GainCombatInspirationEffect : CharacterActionCardEffect
    {
        private readonly CombatInspirationColor color;

        public override string Description => $"获得{CombatInspirationPresentation.GetName(color)}灵感";
        public override TargetType TargetType => TargetType.Self;

        public GainCombatInspirationEffect(CombatInspirationColor color) => this.color = color;

        public override bool CanExecute(ActionCardContext context) => context?.GameContext is ICombatActionCommands;

        public override void Execute(ActionCardContext context) => ExecuteAsync(context).Forget();

        public override async UniTask ExecuteAsync(ActionCardContext context)
        {
            if (context?.GameContext is not ICombatActionCommands commands) return;
            await commands.AddCombatInspirationAsync(context.SourceCharacterId, color);
        }
    }
}
