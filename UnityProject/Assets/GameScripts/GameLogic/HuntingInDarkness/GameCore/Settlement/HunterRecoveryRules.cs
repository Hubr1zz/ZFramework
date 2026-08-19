using System;
using HuntingInDarkness.GameCore.Hunters;

namespace HuntingInDarkness.GameCore.Settlement
{
    public readonly struct HunterRecoveryResult
    {
        public HunterBodyPart BodyPart { get; }
        public int PreviousHealth { get; }
        public int CurrentHealth { get; }
        public int MaximumHealth { get; }
        public int RecoveredHealth => CurrentHealth - PreviousHealth;

        public HunterRecoveryResult(HunterBodyPart bodyPart, int previousHealth, int currentHealth, int maximumHealth)
        {
            BodyPart = bodyPart;
            PreviousHealth = previousHealth;
            CurrentHealth = currentHealth;
            MaximumHealth = maximumHealth;
        }
    }

    /// <summary>营地休养只恢复普通部位生命，不触碰死亡牌、症状或永久损伤。</summary>
    public static class HunterRecoveryRules
    {
        public static bool CanRecover(HunterState hunter, HunterBodyPart bodyPart, out string reason)
        {
            if (!IsSupportedBodyPart(bodyPart))
            {
                reason = "未知的受伤部位。";
                return false;
            }
            if (hunter == null)
            {
                reason = "没有选择需要休养的猎人。";
                return false;
            }
            if (!hunter.IsAlive)
            {
                reason = "已经逝去的猎人无法接受休养。";
                return false;
            }
            if (!hunter.IsAvailable)
            {
                reason = "已退休猎人不再参与营地休养。";
                return false;
            }
            if (hunter.HP == null || hunter.MaxHP == null)
            {
                reason = "猎人的伤势记录不完整。";
                return false;
            }

            GetHealth(hunter, bodyPart, out int currentHealth, out int maximumHealth);
            if (currentHealth >= maximumHealth)
            {
                reason = "这个部位没有需要处理的伤势。";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        public static bool TryRecover(HunterState hunter, HunterBodyPart bodyPart, int recoveryAmount, out HunterRecoveryResult result, out string reason)
        {
            result = default;
            if (!CanRecover(hunter, bodyPart, out reason)) return false;

            GetHealth(hunter, bodyPart, out int currentHealth, out int maximumHealth);
            int safeCurrentHealth = Math.Max(0, Math.Min(currentHealth, maximumHealth));
            int recoveredHealth = Math.Min(Math.Max(1, recoveryAmount), maximumHealth - safeCurrentHealth);
            int newHealth = safeCurrentHealth + recoveredHealth;
            SetHealth(hunter.HP, bodyPart, newHealth);
            result = new HunterRecoveryResult(bodyPart, safeCurrentHealth, newHealth, maximumHealth);
            reason = string.Empty;
            return true;
        }

        public static void GetHealth(HunterState hunter, HunterBodyPart bodyPart, out int currentHealth, out int maximumHealth)
        {
            HunterHitPoints current = hunter?.HP;
            HunterHitPoints maximum = hunter?.MaxHP;
            currentHealth = Math.Max(0, GetHealth(current, bodyPart));
            maximumHealth = Math.Max(1, GetHealth(maximum, bodyPart));
        }

        private static int GetHealth(HunterHitPoints hitPoints, HunterBodyPart bodyPart)
        {
            if (hitPoints == null) return 0;
            return bodyPart switch
            {
                HunterBodyPart.Head => hitPoints.head,
                HunterBodyPart.Torso => hitPoints.body,
                HunterBodyPart.Arms => hitPoints.arms,
                HunterBodyPart.Legs => hitPoints.legs,
                _ => 0
            };
        }

        private static void SetHealth(HunterHitPoints hitPoints, HunterBodyPart bodyPart, int health)
        {
            switch (bodyPart)
            {
                case HunterBodyPart.Head:
                    hitPoints.head = health;
                    break;
                case HunterBodyPart.Torso:
                    hitPoints.body = health;
                    break;
                case HunterBodyPart.Arms:
                    hitPoints.arms = health;
                    break;
                case HunterBodyPart.Legs:
                    hitPoints.legs = health;
                    break;
            }
        }

        private static bool IsSupportedBodyPart(HunterBodyPart bodyPart)
        {
            return bodyPart == HunterBodyPart.Head || bodyPart == HunterBodyPart.Torso || bodyPart == HunterBodyPart.Arms || bodyPart == HunterBodyPart.Legs;
        }
    }
}
