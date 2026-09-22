using System;
using HuntingInDarkness.ActionFlow.Events;
using HuntingInDarkness.Data;

namespace HuntingInDarkness.Hunt
{
    /// <summary>狩猎事件救援人口的唯一写入端；人口先属于当前远征，回营后才进入 Settlement。</summary>
    public sealed class HuntEventPopulationCommand : IPlayableEventPopulationCommand
    {
        private readonly HuntManager manager;

        public HuntEventPopulationCommand(HuntManager manager)
        {
            this.manager = manager ?? throw new ArgumentNullException(nameof(manager));
        }

        public bool TryRescue(int amount, HunterInstance actor, out PlayableEventPopulationChange change, out string reason)
        {
            change = default;
            reason = string.Empty;
            if (amount <= 0)
                return Fail("救援人口数量必须为正数。", out reason);
            if (actor == null || actor.InstanceId <= 0 || actor.IsDead || !ContainsReference(manager.ActiveHunters, actor))
                return Fail("救援事件没有属于当前狩猎小队的存活猎人。", out reason);
            int oldAmount = manager.RescuedPopulation;
            if (oldAmount > int.MaxValue - amount)
                return Fail("救援人口数量溢出。", out reason);
            if (!manager.TrySetRescuedPopulation(oldAmount + amount, out reason)) return false;
            change = new PlayableEventPopulationChange(oldAmount, manager.RescuedPopulation);
            return true;
        }

        private static bool ContainsReference(System.Collections.Generic.IReadOnlyList<HunterInstance> hunters, HunterInstance actor)
        {
            if (hunters == null) return false;
            foreach (HunterInstance hunter in hunters)
                if (ReferenceEquals(hunter, actor)) return true;
            return false;
        }

        private static bool Fail(string message, out string reason)
        {
            reason = message;
            return false;
        }
    }
}
