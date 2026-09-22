using Cysharp.Threading.Tasks;
using HuntingInDarkness.GameCore.Hunters;

namespace HuntingInDarkness.ActionFlow.Hunt
{
    public interface IPlayableHuntConsumableInput
    {
        UniTask<HuntConsumableCommandResult> UseConsumableAsync(int ownerHunterId, string itemId, HunterBodyPart bodyPart);
    }
}
