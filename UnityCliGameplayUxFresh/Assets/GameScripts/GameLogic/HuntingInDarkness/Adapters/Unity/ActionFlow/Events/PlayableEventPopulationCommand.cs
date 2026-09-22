using HuntingInDarkness.Data;

namespace HuntingInDarkness.ActionFlow.Events
{
    public readonly struct PlayableEventPopulationChange
    {
        public PlayableEventPopulationChange(int oldAmount, int newAmount)
        {
            OldAmount = oldAmount;
            NewAmount = newAmount;
        }

        public int OldAmount { get; }
        public int NewAmount { get; }
        public bool Changed => OldAmount != NewAmount;
    }

    /// <summary>由 Hunt Runner 注入的人口救援端口；共享事件流程不直接写入营地状态。</summary>
    public interface IPlayableEventPopulationCommand
    {
        bool TryRescue(int amount, HunterInstance actor, out PlayableEventPopulationChange change, out string reason);
    }

    public struct HuntPopulationRescuedEvent
    {
        public string SourceEventId;
        public int EffectIndex;
        public int ActorId;
        public int OldAmount;
        public int NewAmount;
    }
}
