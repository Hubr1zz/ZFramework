using System.Collections.Generic;
using HuntingInDarkness.Data;

namespace HuntingInDarkness.Hunt
{
    /// <summary>集中定义狩猎行动者资格，避免地图、事件与采集各自接受不同的猎人状态。</summary>
    public static class PlayableHuntSquadAvailability
    {
        public static bool HasLivingHunter(IReadOnlyList<HunterInstance> hunters)
        {
            if (hunters == null) return false;
            foreach (HunterInstance hunter in hunters)
                if (hunter != null && hunter.IsAlive)
                    return true;
            return false;
        }

        public static HunterInstance ResolveSelectedHunter(IReadOnlyList<HunterInstance> hunters, HunterInstance selectedHunter)
        {
            if (hunters == null) return null;
            foreach (HunterInstance hunter in hunters)
                if (hunter != null && ReferenceEquals(hunter, selectedHunter) && hunter.IsAlive)
                    return hunter;
            foreach (HunterInstance hunter in hunters)
                if (hunter != null && hunter.IsAlive)
                    return hunter;
            return null;
        }
    }
}
