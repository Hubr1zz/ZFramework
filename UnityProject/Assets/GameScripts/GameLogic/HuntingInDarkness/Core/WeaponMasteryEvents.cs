namespace Core
{
    public enum WeaponMasteryGainSource
    {
        Combat,
        Training
    }

    /// <summary>猎人使用某把武器对 Boss 造成了至少一点有效伤害。</summary>
    public struct EffectiveWeaponDamageEvent
    {
        public int CharacterId;
        public string WeaponName;
    }

    /// <summary>战斗结算后，第一武器格获得熟练度。</summary>
    public struct WeaponMasteryChangedEvent
    {
        public int HunterId;
        public string HunterName;
        public string WeaponName;
        public string MasteryId;
        public string MasteryName;
        public int OldValue;
        public int NewValue;
        public string[] ReachedMilestoneNames;
        public WeaponMasteryGainSource Source;
    }
}
