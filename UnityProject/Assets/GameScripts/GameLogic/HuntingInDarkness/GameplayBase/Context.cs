using System.Collections.Generic;
using GameplayBase.CombatSystem;
using HuntingInDarkness.GameCore.Cards;
using SO.Boss.ActionCard;
using UnityEngine;

namespace GameplayBase
{
    /// <summary>
    /// 游戏全局上下文的只读视图。
    /// 各系统通过此接口查询状态，不直接持有其他系统引用。
    /// </summary>
    public interface IGameContext
    {
        TurnPhase CurrentPhase   { get; }
        int CurrentTurnNumber    { get; }

        IReadOnlyList<ICharacterState> PlayerCharacters { get; }
        IBossState Boss { get; }

        Character GetCharacter(int characterId);
        IReadOnlyList<ICharacterActionCardInstanceState> GetCardsOf(int characterId);
        ICharacterActionCardInstanceState GetCard(int cardInstanceId);

        /// <summary>Boss所有受击部位卡运行时状态（UI只读访问）</summary>
        IReadOnlyList<HitLocationRuntimeState> BossHitLocationStates { get; }

        /// <summary>Boss最近一次抽取的行动卡（UI Setup时同步初始状态用）</summary>
        IReadOnlyList<BossActionCardData> BossRevealedCards { get; }

        Vector3 GetEntityWorldPosition(int entityId);
    }
    
    /// <summary>
    /// 行动卡效果执行上下文。
    /// 角色行动卡效果与Boss行动卡效果共用此上下文类型。
    /// </summary>
    public class ActionCardContext
    {
        public int             SourceCharacterId;
        public int             TargetEntityId;
        public Vector2Int?     TargetTile;
        public IGameContext    GameContext;
        public IBoardQuery     BoardQuery;
        public IBoardCommand   BoardCommand;
        [System.Obsolete("Compatibility-only combat card queue. New effects receive the active CardGame.ActionQueue execution context.")]
        public ActionQueue ActionQueue;
    }
    
    public class FlipConditionContext
    {
        public int CardInstanceId;
        public int OwnerCharacterId;
        public IGameContext GameContext;

        public int? TriggerSourceCardId;
        public int? TriggerSourceCharacterId;
    }
}
