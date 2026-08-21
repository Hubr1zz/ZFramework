using HuntingInDarkness.Data;

namespace HuntingInDarkness.ActionFlow.Events
{
    public enum PlayableEventResourceScope
    {
        Settlement,
        HuntCollectibles
    }

    public readonly struct PlayableEventResourceChange
    {
        public PlayableEventResourceChange(PlayableEventResourceScope scope, string resourceId, int oldAmount, int newAmount)
        {
            Scope = scope;
            ResourceId = resourceId ?? string.Empty;
            OldAmount = oldAmount;
            NewAmount = newAmount;
        }

        public PlayableEventResourceScope Scope { get; }
        public string ResourceId { get; }
        public int OldAmount { get; }
        public int NewAmount { get; }
        public bool Changed => OldAmount != NewAmount;
    }

    /// <summary>由阶段 Runner 注入的资源写入端口，避免共享事件系统越过阶段权威状态。</summary>
    public interface IPlayableEventResourceCommand
    {
        bool TryApply(EventEffectType effectType, string resourceId, int amount, HunterInstance actor, out PlayableEventResourceChange change, out string reason);
    }

    public struct PlayableEventResourceChangedEvent
    {
        public PlayableEventResourceScope Scope;
        public string ResourceId;
        public int OldAmount;
        public int NewAmount;
    }
}
