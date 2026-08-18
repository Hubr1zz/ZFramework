using System.Collections.Generic;
using System.Threading;
using CardGame.ActionQueue;
using Core;
using Cysharp.Threading.Tasks;
using UnityEngine;
using GameplayBase;
using GameplayBase.CombatSystem;
using HuntingInDarkness.GameCore.Foundation;
using HuntingInDarkness.GameCore.Hunters;
using HuntingInDarkness.Combat;
using HuntingInDarkness.ActionFlow;
using HuntingInDarkness.ActionFlow.Combat;
using SO.Character;

namespace CardTactics.CombatSystem
{
    /// <summary>
    /// 战斗管理器。
    /// 1. 根据攻击类型构建 AttackPipeline
    /// 2. 构造 AttackContext（攻击方/防御方数据分开）
    /// 3. 运行管线
    /// 4. 发布 AttackCompletedEvent
    /// </summary>
    public class CombatManager
    {
        private readonly IGameContext         _gameContext;
        private readonly IBoardQuery          _boardQuery;
        private readonly IPlayerInputProvider _inputProvider;
        private List<HitLocationRuntimeState> _bossHitLocationStates;
        private readonly IHitLocationEffectResolver _hitLocationResolver;
        private readonly IRandomSource _random;
        private readonly IArmorMitigationRule _armorRule;
        private readonly IPermanentInjuryResolver _permanentInjuryResolver;
        private readonly ISurvivalEventResolver _survivalEventResolver;
        private readonly int bossToughness;
        private readonly List<ITargetSelectorOverride> _targetSelectorOverrides = new();

        public IPlayerInputProvider InputProvider => _inputProvider;

        public delegate void PipelineModifier(AttackPipeline pipeline, AttackContext context);
        public event PipelineModifier OnCharacterAttackPipelineBuilt;
        public event PipelineModifier OnBossAttackPipelineBuilt;

        public void AddTargetSelectorOverride(ITargetSelectorOverride ov) =>
            _targetSelectorOverrides.Add(ov);
        public void RemoveTargetSelectorOverride(ITargetSelectorOverride ov) =>
            _targetSelectorOverrides.Remove(ov);

        private ITargetSelector ResolveTargetSelector(AttackContext ctx)
        {
            foreach (var ov in _targetSelectorOverrides)
                if (ov.TryOverride(ctx, out var sel)) return sel;
            return null;
        }

        public CombatManager(
            IGameContext gameContext,
            IBoardQuery  boardQuery,
            IPlayerInputProvider inputProvider,
            List<HitLocationRuntimeState> bossHitLocationStates,
            IRandomSource random = null,
            IArmorMitigationRule armorRule = null,
            IPermanentInjuryResolver permanentInjuryResolver = null,
            ISurvivalEventResolver survivalEventResolver = null,
            int bossToughness = 1)
        {
            _gameContext            = gameContext;
            _boardQuery             = boardQuery;
            _inputProvider          = inputProvider;
            _bossHitLocationStates  = bossHitLocationStates ?? new List<HitLocationRuntimeState>();
            _hitLocationResolver    = new DefaultHitLocationEffectResolver(gameContext, boardQuery);
            _random                 = random ?? new SystemRandomSource();
            _armorRule              = armorRule;
            _permanentInjuryResolver = permanentInjuryResolver;
            _survivalEventResolver = survivalEventResolver;
            this.bossToughness = Mathf.Max(0, bossToughness);
        }

        public void SetHitLocationPool(List<HitLocationRuntimeState> states)
        {
            _bossHitLocationStates = states ?? new List<HitLocationRuntimeState>();
        }

        // ═══════════════════════════════════════════
        // 角色攻击Boss
        // ═══════════════════════════════════════════

        public async UniTask<AttackResult> CharacterAttackBoss(int characterId, CharacterCombatStats stats, WeaponData weapon, CancellationToken cancellationToken = default)
        {
            AttackContext context = CreateCharacterAttackContext(characterId, stats, weapon);
            var pipeline = BuildCharacterAttackPipeline(context);
            OnCharacterAttackPipelineBuilt?.Invoke(pipeline, context);

            var result = await pipeline.Run(context, _inputProvider, cancellationToken);

            Debug.Log($"[CombatManager] 角色#{characterId}攻击Boss完成. Completed={result.Completed}");

            EventBus.Publish(new AttackCompletedEvent
            {
                AttackerId    = characterId,
                DefenderId    = context.DefenderId,
                AttackerIsBoss = false,
                Completed     = result.Completed,
                AbortReason   = result.AbortReason
            });

            return result;
        }

        public GameAction CreateCharacterAttackAction(int characterId, CharacterCombatStats stats, WeaponData weapon, ActionEventOutbox eventOutbox, IReactorEntity source, IReactorEntity target)
        {
            AttackContext context = CreateCharacterAttackContext(characterId, stats, weapon);
            if (context.EffectiveTargetSelector?.SelectionType == TargetSelectionType.Tile)
            {
                var pipeline = BuildCharacterAttackPipeline(context);
                OnCharacterAttackPipelineBuilt?.Invoke(pipeline, context);
                return new LegacyCharacterAttackPipelineAction(context, pipeline, _inputProvider, eventOutbox, source, target);
            }
            return new CharacterAttackFlowAction(context, _inputProvider, _hitLocationResolver, _random, eventOutbox, source, target);
        }

        private AttackContext CreateCharacterAttackContext(int characterId, CharacterCombatStats stats, WeaponData weapon)
        {
            var context = new AttackContext
            {
                AttackerId = characterId,
                DefenderId = _gameContext.Boss.Id,
                AttackerIsBoss = false,
                AttackerStats = stats,
                Weapon = weapon,
                DefenderToughness = bossToughness,
                AllHitLocationStates = _bossHitLocationStates,
                GameContext = _gameContext,
                BoardQuery = _boardQuery
            };
            context.EffectiveTargetSelector = ResolveTargetSelector(context);
            return context;
        }

        private AttackPipeline BuildCharacterAttackPipeline(AttackContext context)
        {
            if (context.EffectiveTargetSelector?.SelectionType == TargetSelectionType.Tile)
            {
                return new AttackPipeline(new IAttackStep[]
                {
                    new TileSelectAttackStep(),
                });
            }

            return new AttackPipeline(new IAttackStep[]
            {
                new DrawHitLocationStep(_random),
                new ResolveHitLocationsStep(_hitLocationResolver, _random),
                new PlayableBossDefeatStep(),
            });
        }

        // ═══════════════════════════════════════════
        // Boss攻击角色
        // ═══════════════════════════════════════════

        public async UniTask<AttackResult> BossAttackCharacter(
            int targetCharacterId,
            CharacterCombatStats defenderStats,
            int woundCount = 1,
            HunterBodyPart targetBodyPart = HunterBodyPart.Torso,
            int accuracy = 1,
            int attackCount = 1)
        {
            var context = new AttackContext
            {
                AttackerId     = _gameContext.Boss.Id,
                DefenderId     = targetCharacterId,
                AttackerIsBoss = true,
                DefenderStats  = defenderStats,
                GameContext    = _gameContext,
                BoardQuery     = _boardQuery
            };

            var pipeline = BuildBossAttackPipeline(woundCount, targetBodyPart, accuracy, attackCount);
            OnBossAttackPipelineBuilt?.Invoke(pipeline, context);

            var result = await pipeline.Run(context, _inputProvider);

            Debug.Log($"[CombatManager] Boss攻击角色#{targetCharacterId}完成. Completed={result.Completed}");

            EventBus.Publish(new AttackCompletedEvent
            {
                AttackerId     = _gameContext.Boss.Id,
                DefenderId     = targetCharacterId,
                AttackerIsBoss = true,
                Completed      = result.Completed,
                AbortReason    = result.AbortReason
            });

            return result;
        }

        private AttackPipeline BuildBossAttackPipeline(
            int woundCount,
            HunterBodyPart targetBodyPart,
            int accuracy,
            int attackCount)
        {
            int safeAttackCount = Mathf.Max(1, attackCount);
            var steps = new List<IAttackStep>();
            for (int index = 0; index < safeAttackCount; index++)
            {
                steps.Add(new BossAttackDodgeStep(accuracy, index + 1, safeAttackCount));
                steps.Add(new BossAttackWoundStep(
                    woundCount,
                    targetBodyPart,
                    _random,
                    _armorRule,
                    _permanentInjuryResolver,
                    _survivalEventResolver));
            }
            return new AttackPipeline(steps);
        }
    }
}
