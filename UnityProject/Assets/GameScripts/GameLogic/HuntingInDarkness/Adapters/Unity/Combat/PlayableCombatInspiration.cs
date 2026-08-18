using System.Collections.Generic;
using HuntingInDarkness.GameCore.Cards;

namespace HuntingInDarkness.Combat
{
    public interface ICombatInspirationReadModel
    {
        IReadOnlyList<CombatInspirationToken> GetCombatInspirationTokens(int characterId);
        int GetCombatInspirationCapacity(int characterId);
    }

    public struct CombatInspirationChangedEvent
    {
        public int CharacterId;
        public int OldCount;
        public int NewCount;
    }

    public static class CombatInspirationPresentation
    {
        public static string GetName(CombatInspirationColor color)
        {
            return color switch
            {
                CombatInspirationColor.Red => "红·残暴",
                CombatInspirationColor.Blue => "蓝·精湛",
                CombatInspirationColor.Yellow => "黄·速度",
                _ => "未知灵感"
            };
        }
    }
}
