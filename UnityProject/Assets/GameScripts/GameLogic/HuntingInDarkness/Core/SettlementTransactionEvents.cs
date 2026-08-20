namespace Core
{
    public enum SettlementTransactionKind
    {
        WeaponTraining,
        Recruitment,
        Recovery,
        Equipment,
        Crafting,
        Invention,
        EventReroll,
        EventResolution
    }

    /// <summary>营地权威事务已经成功提交；持久化、刷新与统计只观察该事实。</summary>
    public struct SettlementTransactionCommittedEvent
    {
        public string TransactionId;
        public SettlementTransactionKind Kind;
    }
}
