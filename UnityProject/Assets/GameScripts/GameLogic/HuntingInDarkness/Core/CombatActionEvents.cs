namespace Core
{
    public struct CombatActionCommittedEvent
    {
        public int CardInstanceId;
        public int OwnerCharacterId;
        public bool IsAttack;
    }
}
