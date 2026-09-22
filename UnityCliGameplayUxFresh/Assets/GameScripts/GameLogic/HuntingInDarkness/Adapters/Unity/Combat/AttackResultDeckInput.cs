using System.Threading;
using Cysharp.Threading.Tasks;
using HuntingInDarkness.GameCore.Combat;

namespace GameplayBase.CombatSystem
{
    public interface IAttackResultDeckInputProvider
    {
        UniTask<int> RequestDrawAttackResult(string prompt, AttackResultDeckComposition composition, CancellationToken cancellationToken = default);
    }

    public interface IAttackResultBatchInputProvider
    {
        UniTask RequestRevealAttackResult(string prompt, CancellationToken cancellationToken = default);
    }
}
