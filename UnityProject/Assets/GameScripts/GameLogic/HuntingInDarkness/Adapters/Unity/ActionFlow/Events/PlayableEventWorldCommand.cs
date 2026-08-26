using HuntingInDarkness.Data;

namespace HuntingInDarkness.ActionFlow.Events
{
    public readonly struct PlayableEventWorldChange
    {
        public PlayableEventWorldChange(string targetId, int affectedCount)
        {
            TargetId = targetId ?? string.Empty;
            AffectedCount = affectedCount < 0 ? 0 : affectedCount;
        }

        public string TargetId { get; }
        public int AffectedCount { get; }
        public bool Changed => AffectedCount > 0;
    }

    /// <summary>由阶段 Runner 注入的世界状态写入端口；目标由绑定的运行时上下文决定。</summary>
    public interface IPlayableEventWorldCommand
    {
        bool TryApply(EventEffect effect, out PlayableEventWorldChange change, out string reason);
    }
}
