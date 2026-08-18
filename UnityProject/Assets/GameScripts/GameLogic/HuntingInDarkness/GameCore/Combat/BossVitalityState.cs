using System;

namespace HuntingInDarkness.GameCore.Combat
{
    public readonly struct BossVitalityDamageResult
    {
        public int IncomingDamage { get; }
        public int AppliedDamage { get; }
        public int PreviousHealth { get; }
        public int CurrentHealth { get; }
        public bool WasDefeated { get; }
        public bool IsDefeated { get; }

        public BossVitalityDamageResult(int incomingDamage, int appliedDamage, int previousHealth, int currentHealth, bool wasDefeated, bool isDefeated)
        {
            IncomingDamage = incomingDamage;
            AppliedDamage = appliedDamage;
            PreviousHealth = previousHealth;
            CurrentHealth = currentHealth;
            WasDefeated = wasDefeated;
            IsDefeated = isDefeated;
        }
    }

    /// <summary>Boss 全局生命；受击部位耐久与此状态保持独立。</summary>
    public sealed class BossVitalityState
    {
        private bool defeatClaimed;

        public int MaxHealth { get; }
        public int CurrentHealth { get; private set; }
        public bool IsDefeated => CurrentHealth == 0;

        public BossVitalityState(int maxHealth)
        {
            MaxHealth = Math.Max(1, maxHealth);
            CurrentHealth = MaxHealth;
        }

        public BossVitalityDamageResult ApplyDamage(int damage)
        {
            int safeDamage = Math.Max(0, damage);
            int previous = CurrentHealth;
            bool wasDefeated = IsDefeated;
            CurrentHealth = Math.Max(0, CurrentHealth - safeDamage);
            return new BossVitalityDamageResult(safeDamage, previous - CurrentHealth, previous, CurrentHealth, wasDefeated, IsDefeated);
        }

        public bool TryClaimDefeat()
        {
            if (!IsDefeated || defeatClaimed) return false;

            defeatClaimed = true;
            return true;
        }
    }
}
