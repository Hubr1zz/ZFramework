using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace GameplayBase.CombatSystem
{
    public readonly struct PlayerChoiceOption
    {
        public int Id { get; }
        public string Label { get; }

        public PlayerChoiceOption(int id, string label)
        {
            Id = id;
            Label = label ?? string.Empty;
        }
    }

    /// <summary>Optional extension for labelled choices that are not world entities or cards.</summary>
    public interface IPlayerOptionInputProvider
    {
        UniTask<int> RequestSelectOption(string prompt, List<PlayerChoiceOption> options, int cancelOptionId = -1, string cancelLabel = "取消", CancellationToken cancellationToken = default);
    }
}
