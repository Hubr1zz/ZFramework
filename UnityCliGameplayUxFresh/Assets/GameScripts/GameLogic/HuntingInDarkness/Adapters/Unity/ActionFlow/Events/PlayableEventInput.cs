using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using HuntingInDarkness.Data;
using HuntingInDarkness.Settlement;

namespace HuntingInDarkness.ActionFlow.Events
{
    public enum PlayableEventCheckDecision
    {
        Accept,
        Reroll
    }

    public readonly struct PlayableEventChoiceSelection
    {
        public PlayableEventChoiceSelection(int optionIndex, HunterInstance actor)
        {
            OptionIndex = optionIndex;
            Actor = actor;
        }

        public int OptionIndex { get; }
        public HunterInstance Actor { get; }
        public bool IsValid => OptionIndex >= 0;
    }

    /// <summary>跨阶段事件的纯输入端口；实现者只返回玩家决定，不提交游戏状态。</summary>
    public interface IPlayableEventInput
    {
        UniTask ConfirmNarrativeAsync(EventData gameEvent, HunterInstance actor, CancellationToken cancellationToken);
        UniTask<PlayableEventChoiceSelection> SelectChoiceAsync(EventData gameEvent, HunterInstance actor, IReadOnlyList<HunterInstance> hunters, IPlayableEventResourceAvailability resourceAvailability, CancellationToken cancellationToken);
        UniTask<PlayableEventCheckDecision> PresentCheckAsync(PlayableEventChoiceTransaction transaction, CancellationToken cancellationToken);
        UniTask ConfirmResultAsync(EventData gameEvent, EventResolutionResult result, CancellationToken cancellationToken);
    }
}
