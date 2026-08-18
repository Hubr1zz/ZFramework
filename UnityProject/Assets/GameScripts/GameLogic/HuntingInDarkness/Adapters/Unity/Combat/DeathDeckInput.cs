using System.Threading;
using Cysharp.Threading.Tasks;

namespace GameplayBase.CombatSystem
{
    public readonly struct DeathDeckComposition
    {
        public int SurvivalCards { get; }
        public int DeathCards { get; }
        public int TotalCards => SurvivalCards + DeathCards;

        public DeathDeckComposition(int survivalCards, int deathCards)
        {
            SurvivalCards = System.Math.Max(0, survivalCards);
            DeathCards = System.Math.Max(0, deathCards);
        }
    }

    public interface IDeathDeckInputProvider
    {
        UniTask<int> RequestDrawDeathCard(string prompt, DeathDeckComposition composition, CancellationToken cancellationToken = default);
    }
}
