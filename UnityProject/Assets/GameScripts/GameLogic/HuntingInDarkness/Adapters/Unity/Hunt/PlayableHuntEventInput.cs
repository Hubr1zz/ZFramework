using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using HuntingInDarkness.Data;
using HuntingInDarkness.Settlement;

namespace HuntingInDarkness.Hunt
{
    public enum HuntEventCheckDecision
    {
        Accept,
        Reroll
    }

    public readonly struct HuntEventChoiceSelection
    {
        public HuntEventChoiceSelection(int optionIndex, HunterInstance actor)
        {
            OptionIndex = optionIndex;
            Actor = actor;
        }

        public int OptionIndex { get; }
        public HunterInstance Actor { get; }
        public bool IsValid => OptionIndex >= 0;
    }

    /// <summary>狩猎事件的纯输入/表现端口；实现者只返回玩家决定，不提交游戏状态。</summary>
    public interface IHuntEventInput
    {
        UniTask ConfirmNarrativeAsync(EventData gameEvent, HunterInstance actor, CancellationToken cancellationToken);
        UniTask<HuntEventChoiceSelection> SelectChoiceAsync(EventData gameEvent, HunterInstance actor, IReadOnlyList<HunterInstance> hunters, CancellationToken cancellationToken);
        UniTask<HuntEventCheckDecision> PresentCheckAsync(PlayableEventChoiceTransaction transaction, CancellationToken cancellationToken);
        UniTask ConfirmResultAsync(EventData gameEvent, EventResolutionResult result, CancellationToken cancellationToken);
    }
}
