using HuntingInDarkness.Data;

namespace HuntingInDarkness.Settlement
{
    /// <summary>跨阶段事件只通过该端口请求永久死亡，不直接改写猎人或死亡后果。</summary>
    public interface IHunterDeathCommand
    {
        bool TryKill(HunterInstance hunter, string causeId, string causeText, out string reason);
    }
}
