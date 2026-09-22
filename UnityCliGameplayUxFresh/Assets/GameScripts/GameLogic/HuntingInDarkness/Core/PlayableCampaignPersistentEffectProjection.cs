using HuntingInDarkness.Data;

namespace Core
{
    public interface IPlayableCampaignPersistentEffectProjection
    {
        bool TrySynchronize(SettlementInstance settlement, out string reason);
        bool TryClear(SettlementInstance settlement, out string reason);
        void Dispose();
    }
}
