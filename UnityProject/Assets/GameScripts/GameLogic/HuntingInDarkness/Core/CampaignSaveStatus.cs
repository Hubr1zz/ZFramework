namespace Core
{
    public enum CampaignSaveState
    {
        Idle,
        Saving,
        Failed
    }

    public readonly struct CampaignSaveStatus
    {
        public CampaignSaveStatus(CampaignSaveState state, string reason, long revision, bool canRetry)
        {
            State = state;
            Reason = reason ?? string.Empty;
            Revision = revision;
            CanRetry = canRetry;
        }

        public CampaignSaveState State { get; }
        public string Reason { get; }
        public long Revision { get; }
        public bool CanRetry { get; }

        public static CampaignSaveStatus Idle(long revision = 0) => new(CampaignSaveState.Idle, string.Empty, revision, false);
    }
}
