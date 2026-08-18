using Core;
using Cysharp.Threading.Tasks;
using GameplayBase.Card.BossActionCard;
using GameplayBase.Card.CharacterActionCard;
using GameplayBase.CombatSystem;
using HuntingInDarkness.Combat;
using SO.Character;
using UnityEngine;

namespace GameplayBase.Card.Effect
{
    // ═══════════════════════════════════════════
    // 角色攻击效果基类
    // ═══════════════════════════════════════════

    /// <summary>
    /// 角色攻击效果抽象基类。继承自 CharacterActionCardEffect。
    /// Execute 内部以 fire-and-forget 启动异步攻击管线，
    /// 不阻塞卡牌翻面等后续同步流程。
    /// </summary>
    public abstract class AttackEffectBase : CharacterActionCardEffect
    {
        public override bool CanExecute(ActionCardContext context) => true;

        public override void Execute(ActionCardContext context)
        {
            ExecuteAsync(context).Forget();
        }

        public override async UniTask ExecuteAsync(ActionCardContext context)
        {
            var combatManager = GetCombatManager(context);
            if (combatManager == null)
            {
                Debug.LogError("[AttackEffect] 无法获取 CombatManager");
                return;
            }
            await ExecuteAttackAsync(context, combatManager);
        }

        protected abstract UniTask ExecuteAttackAsync(
            ActionCardContext context, CardTactics.CombatSystem.CombatManager combatManager);

        protected static CardTactics.CombatSystem.CombatManager GetCombatManager(ActionCardContext context)
        {
            if (context.GameContext is ICombatProvider provider)
                return provider.CombatManager;
            Debug.LogWarning("[AttackEffect] GameContext 未实现 ICombatProvider");
            return null;
        }
    }

    /// <summary>
    /// CombatManager 提供者接口。GameManager 实现此接口，
    /// 供 AttackEffectBase 和 BossAttackEffect 获取 CombatManager。
    /// </summary>
    public interface ICombatProvider
    {
        CardTactics.CombatSystem.CombatManager CombatManager { get; }
    }

    // ═══════════════════════════════════════════
    // 角色攻击Boss效果
    // ═══════════════════════════════════════════

    /// <summary>
    /// 角色攻击Boss：抽受击部位卡 → 力量判定 → 触发效果。
    /// 武器由注入的 IWeaponResolver 决定，默认使用 EquippedWeaponResolver。
    /// </summary>
    public class CharacterAttackEffect : AttackEffectBase
    {
        private readonly IWeaponResolver _weaponResolver;

        public override string     Description => "攻击Boss";
        public override TargetType TargetType  => TargetType.SingleEnemy;

        public CharacterAttackEffect(IWeaponResolver weaponResolver = null)
        {
            _weaponResolver = weaponResolver ?? new PlayableLoadoutWeaponResolver();
        }

        protected override async UniTask ExecuteAttackAsync(
            ActionCardContext context, CardTactics.CombatSystem.CombatManager combatManager)
        {
            var stats = GetCharacterStats(context);
            if (stats == null)
            {
                Debug.LogError($"[CharacterAttackEffect] 找不到角色#{context.SourceCharacterId}的战斗属性");
                return;
            }

            // 范围门控：若配置了 targeting，Boss 必须位于合法目标格内才可攻击（如「正前方」）。
            if (Targeting != null && context.BoardQuery != null)
            {
                var validTiles = Targeting.GetValidTiles(context.BoardQuery, context.SourceCharacterId);
                var bossTile   = context.BoardQuery.GetEntityPosition(context.GameContext.Boss.Id);
                if (!validTiles.Contains(bossTile))
                {
                    Debug.Log($"[CharacterAttackEffect] Boss 不在攻击范围（{Targeting.Describe()}）内，攻击取消。");
                    return;
                }
            }

            var weapon = await _weaponResolver.ResolveAsync(context, combatManager.InputProvider);
            if (weapon == null)
            {
                Debug.LogError($"[CharacterAttackEffect] 角色#{context.SourceCharacterId}没有可用武器，攻击取消");
                return;
            }

            await combatManager.CharacterAttackBoss(context.SourceCharacterId, stats, weapon);
        }

        private static CharacterCombatStats GetCharacterStats(ActionCardContext context)
        {
            if (context.GameContext is ICombatRuntimeDataProvider provider)
                return provider.GetCharacterData(context.SourceCharacterId)?.CombatStats;
            Debug.LogWarning("[CharacterAttackEffect] GameContext 未提供角色战斗数据");
            return null;
        }
    }

    // ═══════════════════════════════════════════
    // Boss攻击角色效果
    // ═══════════════════════════════════════════

    /// <summary>
    /// Boss行动卡效果数据：Boss攻击角色。
    /// 继承 BossActionCardEffect，由 BossActionCardEffectData.CreateRuntime() 创建。
    /// </summary>
    public class BossAttackEffect : BossActionCardEffect
    {
        private readonly int _woundCount;
        private readonly int _accuracy;
        private readonly int _attackCount;
        private readonly int _targetCharacterId; // -1 = 随机选存活角色

        public override string Description => "Boss攻击";

        public BossAttackEffect(int woundCount, int targetCharacterId = -1, int accuracy = 1, int attackCount = 1)
        {
            _woundCount          = woundCount;
            _targetCharacterId   = targetCharacterId;
            _accuracy            = accuracy;
            _attackCount         = attackCount;
        }

        public override bool CanExecute(ActionCardContext context) => true;

        public override async UniTask ExecuteAsync(ActionCardContext context)
        {
            var combatManager = GetCombatManager(context);
            if (combatManager == null)
            {
                Debug.LogError("[BossAttackEffect] 无法获取 CombatManager");
                return;
            }
            await ExecuteAttackAsync(context, combatManager);
        }

        private async UniTask ExecuteAttackAsync(
            ActionCardContext context, CardTactics.CombatSystem.CombatManager combatManager)
        {
            int targetId = ResolveTarget(context);
            if (targetId < 0) return;

            CharacterCombatStats defenderStats = null;
            if (context.GameContext is ICombatRuntimeDataProvider provider)
                defenderStats = provider.GetCharacterData(targetId)?.CombatStats;

            if (defenderStats == null)
            {
                Debug.LogError($"[BossAttackEffect] 找不到角色#{targetId}的战斗属性");
                return;
            }

            await combatManager.BossAttackCharacter(targetId, defenderStats, _woundCount, accuracy: _accuracy, attackCount: _attackCount);
        }

        private int ResolveTarget(ActionCardContext context)
        {
            if (_targetCharacterId >= 0) return _targetCharacterId;

            // 随机选择一个存活角色
            var characters = context.GameContext.PlayerCharacters;
            var alive = new System.Collections.Generic.List<ICharacterState>();
            foreach (var c in characters)
            {
                bool isDead = false;
                if (context.GameContext is ICombatRuntimeDataProvider provider)
                    isDead = provider.GetCharacterData(c.Id)?.CombatStats?.IsDead ?? false;
                if (!isDead) alive.Add(c);
            }

            if (alive.Count == 0) return -1;
            return alive[Random.Range(0, alive.Count)].Id;
        }

        private static CardTactics.CombatSystem.CombatManager GetCombatManager(ActionCardContext context)
        {
            if (context.GameContext is ICombatProvider provider)
                return provider.CombatManager;
            Debug.LogWarning("[BossAttackEffect] GameContext 未实现 ICombatProvider");
            return null;
        }
    }
}
