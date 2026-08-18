using Cysharp.Threading.Tasks;
using GameplayBase.CombatSystem;
using HuntingInDarkness.GameCore.Cards;
using HuntingInDarkness.GameCore.Combat;

namespace HuntingInDarkness.Combat
{
    /// <summary>行动效果读取本场角色战斗数据的窄接口。</summary>
    public interface ICombatRuntimeDataProvider
    {
        CharacterRuntimeData GetCharacterData(int characterId);
    }

    /// <summary>行动卡效果所需的本场战斗命令，避免依赖组合根或具体 GameManager。</summary>
    public interface ICombatActionCommands : ICombatInspirationReadModel
    {
        UniTask<InspirationGain> AddCombatInspirationAsync(int characterId, CombatInspirationColor color);
        TimelineActionStatus GetTimelineStatus(int characterId);
        bool TryRelieveOvertimeCharacter(int targetId);
    }
}
