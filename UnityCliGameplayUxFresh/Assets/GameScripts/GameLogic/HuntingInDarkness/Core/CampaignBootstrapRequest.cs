using Config;
using GameplayBase;
using GameplayBase.CombatSystem;
using HuntingInDarkness.ActionFlow.Presentation;
using HuntingInDarkness.Settlement;
using TMPro;
using UI;

namespace Core
{
    /// <summary>在 GameManager 激活前一次性提交的战役组合根配置；未设置的字段保留场景序列化值。</summary>
    public sealed class CampaignBootstrapRequest
    {
        public BattleSetup BattleSetup { get; set; }
        public float? CellSize { get; set; }
        public EntityCreator EntityCreator { get; set; }
        public TMP_FontAsset ChineseFontAsset { get; set; }
        public UnityEngine.TextAsset ChineseCharacterSet { get; set; }
        public PlayableSettlementContentCatalog SettlementContent { get; set; }
        public PlayableWorkshopCatalog WorkshopContent { get; set; }
        public bool? WaitForEntrySelection { get; set; }
        public ITabletopRandomInteractionPresenter TabletopInteraction { get; set; }
        public ICampaignPersistencePort Persistence { get; set; }
        public GamePhase? DevelopmentStartPhase { get; set; }
    }
}
