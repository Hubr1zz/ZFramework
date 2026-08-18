using Cysharp.Threading.Tasks;
using HuntingInDarkness.GameCore.Combat;

namespace GameplayBase.CombatSystem
{
    public interface IAttackResultDeckInputProvider
    {
        UniTask<int> RequestDrawAttackResult(string prompt, AttackResultDeckComposition composition);
    }

    public interface IAttackResultBatchInputProvider
    {
        UniTask RequestRevealAttackResult(string prompt);
    }
}
