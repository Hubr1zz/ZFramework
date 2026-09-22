namespace GameplayBase
{
    /// <summary>供战斗 Adapter 读取和提交 Boss 全局生命的窄接口。</summary>
    public interface IBossVitalityState
    {
        int MaxHealth { get; }
        int CurrentHealth { get; }
        bool IsDefeated { get; }
        int ApplyBossDamage(int damage);
        bool TryClaimDefeat();
    }
}
