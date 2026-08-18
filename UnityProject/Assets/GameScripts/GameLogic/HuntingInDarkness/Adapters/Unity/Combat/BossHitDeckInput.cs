using System.Threading;
using Cysharp.Threading.Tasks;
using HuntingInDarkness.GameCore.Combat;

namespace GameplayBase.CombatSystem
{
    public interface IBossHitDeckInputProvider
    {
        UniTask<int> RequestDrawBossHitResult(string prompt, BossHitDeckComposition composition, CancellationToken cancellationToken = default);
    }
}
