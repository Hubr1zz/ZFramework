using System;
using System.Collections.Generic;
using HuntingInDarkness.Data;
using HuntingInDarkness.Settlement;

namespace HuntingInDarkness.ActionFlow.Settlement
{
    public interface ISettlementCareContent
    {
        IReadOnlyList<HunterData> RecruitmentTemplates { get; }
        string RecruitmentCostResourceId { get; }
        int RecruitmentCost { get; }
        int MaximumLivingHunters { get; }
        string RecoveryCostResourceId { get; }
        int RecoveryCost { get; }
        int RecoveryAmount { get; }
    }

    /// <summary>把可编辑营地目录投影为 Action 不依赖具体 ScriptableObject 的只读配置。</summary>
    public sealed class PlayableSettlementCareContentAdapter : ISettlementCareContent
    {
        private static readonly IReadOnlyList<HunterData> emptyTemplates = Array.Empty<HunterData>();
        private readonly PlayableSettlementContentCatalog catalog;

        public PlayableSettlementCareContentAdapter(PlayableSettlementContentCatalog catalog)
        {
            this.catalog = catalog;
        }

        public IReadOnlyList<HunterData> RecruitmentTemplates => catalog != null ? catalog.RecruitmentTemplates : emptyTemplates;
        public string RecruitmentCostResourceId => catalog?.RecruitmentCostItem != null ? catalog.RecruitmentCostItem.itemName : string.Empty;
        public int RecruitmentCost => catalog?.RecruitmentCost ?? 0;
        public int MaximumLivingHunters => catalog?.MaximumLivingHunters ?? 1;
        public string RecoveryCostResourceId => catalog?.RecoveryCostItem != null ? catalog.RecoveryCostItem.itemName : string.Empty;
        public int RecoveryCost => catalog?.RecoveryCost ?? 0;
        public int RecoveryAmount => catalog?.RecoveryAmount ?? 1;
    }
}
