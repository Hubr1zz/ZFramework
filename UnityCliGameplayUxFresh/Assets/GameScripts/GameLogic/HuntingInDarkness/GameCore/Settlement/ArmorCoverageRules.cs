using System;

namespace HuntingInDarkness.GameCore.Settlement
{
    [Flags]
    public enum ArmorCoverage
    {
        None = 0,
        Head = 1 << 0,
        Torso = 1 << 1,
        Arms = 1 << 2,
        Legs = 1 << 3
    }

    /// <summary>与 Unity 物品资产解耦的防具部位占用规则。</summary>
    public static class ArmorCoverageRules
    {
        public static bool CanEquip(ArmorCoverage occupied, ArmorCoverage candidate, out string reason)
        {
            if (candidate == ArmorCoverage.None)
            {
                reason = "防具未配置保护部位";
                return false;
            }
            if ((occupied & candidate) != ArmorCoverage.None)
            {
                reason = "对应部位已经装备防具";
                return false;
            }

            reason = string.Empty;
            return true;
        }
    }
}
