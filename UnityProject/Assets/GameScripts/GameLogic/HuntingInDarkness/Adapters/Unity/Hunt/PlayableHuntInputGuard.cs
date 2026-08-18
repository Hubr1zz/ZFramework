using System.Collections.Generic;
using UnityEngine;

namespace HuntingInDarkness.Hunt
{
    /// <summary>让叙事 View 在等待玩家选择时暂时冻结狩猎命令，支持多个独立遮罩安全嵌套。</summary>
    public static class PlayableHuntInputGuard
    {
        private static readonly HashSet<int> blockers = new();

        public static bool IsBlocked => blockers.Count > 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeState() => blockers.Clear();

        public static void Acquire(int ownerId)
        {
            if (ownerId != 0)
                blockers.Add(ownerId);
        }

        public static void Release(int ownerId)
        {
            if (ownerId != 0)
                blockers.Remove(ownerId);
        }
    }
}
