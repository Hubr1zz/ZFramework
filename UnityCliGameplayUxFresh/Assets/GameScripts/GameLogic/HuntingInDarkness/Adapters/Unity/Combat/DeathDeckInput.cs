using System.Threading;
using Cysharp.Threading.Tasks;

namespace GameplayBase.CombatSystem
{
    public readonly struct DeathDeckComposition
    {
        public int SurvivalCards { get; }
        public int SurvivalEventCards { get; }
        public int OrdinarySurvivalCards => SurvivalCards - SurvivalEventCards;
        public int DeathCards { get; }
        public int TotalCards => SurvivalCards + DeathCards;

        public DeathDeckComposition(int survivalCards, int deathCards, int survivalEventCards = 0)
        {
            SurvivalCards = System.Math.Max(0, survivalCards);
            DeathCards = System.Math.Max(0, deathCards);
            SurvivalEventCards = System.Math.Max(0, System.Math.Min(SurvivalCards, survivalEventCards));
        }
    }

    public interface IDeathDeckInputProvider
    {
        UniTask<int> RequestDrawDeathCard(string prompt, DeathDeckComposition composition, CancellationToken cancellationToken = default);
    }
}
