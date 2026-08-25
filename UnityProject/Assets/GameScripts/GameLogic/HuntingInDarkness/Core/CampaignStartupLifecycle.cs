namespace Core
{
    public enum CampaignStartupState
    {
        AwaitingChoice,
        StartingNew,
        Loading,
        Active
    }

    public readonly struct CampaignStartupResult
    {
        private CampaignStartupResult(bool succeeded, CampaignStartupState state, string reason)
        {
            State = state;
            Succeeded = succeeded;
            Reason = reason ?? string.Empty;
        }

        public CampaignStartupState State { get; }
        public bool Succeeded { get; }
        public string Reason { get; }
        public static CampaignStartupResult Success() => new(true, CampaignStartupState.Active, string.Empty);
        public static CampaignStartupResult Failed(CampaignStartupState state, string reason) => new(false, state, reason);
    }

    /// <summary>Owns the authoritative lifecycle state for entering a playable campaign runtime.</summary>
    public sealed class CampaignStartupLifecycle
    {
        private bool waitForEntrySelection;
        private bool runtimeActive;
        private bool startupInFlight;

        public CampaignStartupState State { get; private set; } = CampaignStartupState.Active;
        public bool WaitForEntrySelection => waitForEntrySelection;
        public bool IsRuntimeActive => runtimeActive;

        public bool Configure(bool shouldWaitForEntrySelection)
        {
            if (runtimeActive || startupInFlight) return false;
            waitForEntrySelection = shouldWaitForEntrySelection;
            State = shouldWaitForEntrySelection ? CampaignStartupState.AwaitingChoice : CampaignStartupState.Active;
            return true;
        }

        public bool TryBegin(CampaignStartupState inFlightState, out string reason)
        {
            if (!waitForEntrySelection)
            {
                reason = "当前 GameManager 未启用正式开场入口。";
                return false;
            }
            if (runtimeActive)
            {
                reason = "战役运行态已经启动。";
                return false;
            }
            if (startupInFlight)
            {
                reason = "战役入口正在处理中。";
                return false;
            }
            if (inFlightState != CampaignStartupState.StartingNew && inFlightState != CampaignStartupState.Loading)
            {
                reason = "战役入口状态无效。";
                return false;
            }

            startupInFlight = true;
            State = inFlightState;
            reason = string.Empty;
            return true;
        }

        public void ActivateRuntime()
        {
            runtimeActive = true;
            State = CampaignStartupState.Active;
        }

        public void DeactivateRuntime()
        {
            runtimeActive = false;
            if (!startupInFlight)
                State = waitForEntrySelection ? CampaignStartupState.AwaitingChoice : CampaignStartupState.Active;
        }

        public void CompleteAttempt()
        {
            startupInFlight = false;
            State = runtimeActive ? CampaignStartupState.Active : CampaignStartupState.AwaitingChoice;
        }
    }
}
