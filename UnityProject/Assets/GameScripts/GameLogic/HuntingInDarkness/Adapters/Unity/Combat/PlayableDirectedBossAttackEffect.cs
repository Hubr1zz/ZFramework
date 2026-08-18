using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GameplayBase;
using GameplayBase.Card.BossActionCard;
using GameplayBase.Card.Effect;
using GameplayBase.CombatSystem;
using HuntingInDarkness.GameCore.Combat;
using HuntingInDarkness.GameCore.Foundation;
using HuntingInDarkness.GameCore.Hunters;

namespace HuntingInDarkness.Combat
{
    /// <summary>执行带目标指引的 Playable Boss 攻击；旧 BossAttackEffect 保持兼容。</summary>
    public sealed class PlayableDirectedBossAttackEffect : BossActionCardEffect
    {
        private readonly string actionName;
        private readonly int woundCount;
        private readonly int accuracy;
        private readonly int attackCount;
        private readonly BossTargetPolicy targetPolicy;
        private readonly IRandomSource random;

        public override string Description => "按行动卡指引选择目标并攻击";

        public PlayableDirectedBossAttackEffect(string actionName, int woundCount, int accuracy, int attackCount, BossTargetPolicy targetPolicy, IRandomSource random = null)
        {
            this.actionName = string.IsNullOrWhiteSpace(actionName) ? "怪物攻击" : actionName;
            this.woundCount = Math.Max(1, woundCount);
            this.accuracy = Math.Max(1, accuracy);
            this.attackCount = Math.Max(1, attackCount);
            this.targetPolicy = targetPolicy;
            this.random = random ?? new SystemRandomSource();
        }

        public override bool CanExecute(ActionCardContext context) => context?.GameContext != null;

        public override async UniTask ExecuteAsync(ActionCardContext context)
        {
            if (context.GameContext is not ICombatProvider combatProvider || combatProvider.CombatManager == null)
                return;

            List<BossTargetCandidate> candidates = BuildCandidates(context);
            var resolver = new PlayableBossTargetResolver(random);
            int targetId = await resolver.ResolveAsync(actionName, targetPolicy, candidates, combatProvider.CombatManager.InputProvider);
            if (targetId < 0)
                return;

            CharacterCombatStats defenderStats = GetCombatStats(context.GameContext, targetId);
            if (defenderStats == null || defenderStats.IsDead)
                return;

            context.TargetEntityId = targetId;
            await combatProvider.CombatManager.BossAttackCharacter(targetId, defenderStats, woundCount, accuracy: accuracy, attackCount: attackCount);
        }

        private List<BossTargetCandidate> BuildCandidates(ActionCardContext context)
        {
            var candidates = new List<BossTargetCandidate>();
            IReadOnlyList<ICharacterState> characters = context.GameContext.PlayerCharacters;
            if (characters == null)
                return candidates;

            foreach (ICharacterState character in characters)
            {
                if (character == null)
                    continue;

                CharacterCombatStats stats = GetCombatStats(context.GameContext, character.Id);
                if (stats == null || stats.IsDead)
                    continue;

                candidates.Add(new BossTargetCandidate(character.Id, GetDistance(context, character.Id), GetDamageTaken(stats.InjuryState)));
            }
            return candidates;
        }

        private int GetDistance(ActionCardContext context, int targetId)
        {
            if (targetPolicy != BossTargetPolicy.Nearest || context.BoardQuery == null || context.GameContext.Boss == null)
                return 0;

            try
            {
                var bossPosition = context.BoardQuery.GetEntityPosition(context.GameContext.Boss.Id);
                var targetPosition = context.BoardQuery.GetEntityPosition(targetId);
                return Math.Max(0, context.BoardQuery.GetDistance(bossPosition, targetPosition));
            }
            catch (KeyNotFoundException)
            {
                return int.MaxValue;
            }
        }

        private static int GetDamageTaken(HunterInjuryState injuryState)
        {
            int damage = 0;
            foreach (HunterBodyPart part in Enum.GetValues(typeof(HunterBodyPart)))
            {
                HunterBodyPartState state = injuryState.GetPart(part);
                damage += state.Definition.MaxHealth - state.CurrentHealth;
            }
            return damage;
        }

        private static CharacterCombatStats GetCombatStats(IGameContext gameContext, int characterId)
        {
            if (gameContext is Core.GameManager manager)
                return manager.GetCharacterData(characterId)?.CombatStats;
            return null;
        }
    }
}
