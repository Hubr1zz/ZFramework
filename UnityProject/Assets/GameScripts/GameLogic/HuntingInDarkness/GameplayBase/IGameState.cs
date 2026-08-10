// ─── 游戏全局状态接口 ────────────────────────────────────────────────────────

using System.Collections.Generic;
using GameplayBase.CombatSystem;
using SO.Boss.ActionCard;
using UnityEngine;

namespace GameplayBase
{
    public interface ICharacterState
    {
        int    Id     { get; }
        string Name   { get; }
        int    CurrentTimePoints { get; }
        int    Willpower { get; }
        int    CombatInspiration { get; }
        CharacterActionState ActionState { get; }
        IReadOnlyList<ICharacterActionCardInstanceState> Hand { get; }
    }

    public interface IBossState
    {
        int    Id   { get; }
        string Name { get; }
        int    CurrentTimePoints { get; }
        IReadOnlyList<int> PendingActionCardIds { get; }
        IReadOnlyList<int> RevealedNextCardIds  { get; }
    }

    public interface ICharacterActionCardInstanceState
    {
        int       InstanceId        { get; }
        int       OwnerCharacterId  { get; }
        string    CardName          { get; }
        CardFace  CurrentFace       { get; }
        bool      CanPlay           { get; }
        bool      CanFlip           { get; }
        bool      CanRestore        { get; }
        bool      CanDiscard        { get; }
        string    FaceUpDescription  { get; }
        string    FaceDownDescription{ get; }
        int       TimePointCost     { get; }
        bool      IsAvailableThisTurn { get; }
        bool      IsWillAction      { get; }
    }
}
