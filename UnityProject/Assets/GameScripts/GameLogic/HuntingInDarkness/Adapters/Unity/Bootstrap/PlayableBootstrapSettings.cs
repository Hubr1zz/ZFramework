using System.Collections.Generic;
using Config;
using GameplayBase;
using GameplayBase.CombatSystem;
using GameplayBase.Config;
using HuntingInDarkness.Hunt;
using HuntingInDarkness.Settlement;
using HuntingInDarkness.Combat;
using SO.Combat;
using SO.Boss.ActionCard;
using UnityEngine;

namespace HuntingInDarkness.Bootstrap
{
    /// <summary>
    /// 可游玩版本的组合根配置。仅负责把 Unity 资产映射成现有战斗装配数据。
    /// </summary>
    [CreateAssetMenu(fileName = "PlayableBootstrapSettings", menuName = "Hunting in Darkness/Playable Bootstrap Settings")]
    public sealed class PlayableBootstrapSettings : ScriptableObject
    {
        [Header("游戏流程")]
        [SerializeField] private string entrySceneName = "main";
        [SerializeField] private GamePhase initialPhase = GamePhase.Settlement;
        [SerializeField] private bool showStartMenu = true;
        [SerializeField] private bool showFlowGuide = true;
        [SerializeField] private bool showSettlementHud = true;
        [SerializeField, Min(320f)] private float settlementHudWidth = 420f;
        [SerializeField] private bool showOpeningNarrative = true;
        [SerializeField] private bool hideFrameworkDebugger = true;
        [SerializeField, TextArea(3, 6)] private string openingNarrative = "你与同伴从无火的黑暗中醒来。\n远处的嚎叫声提醒着你们：如果不学会狩猎，今夜就会成为猎物。";
        [SerializeField] private string gameTitle = "黑暗狩猎";
        [SerializeField, TextArea(2, 4)] private string titleTagline = "无火之地没有英雄。\n只有尚未被黑暗吞没的人。";

        [Header("Boss 决战视角")]
        [SerializeField] private Vector3 bossCameraPosition = new(0f, 8f, -11f);
        [SerializeField] private Vector3 bossCameraEulerAngles = new(32f, 0f, 0f);

        [Header("可游玩场景照明")]
        [SerializeField] private Color keyLightColor = new(1f, 0.88f, 0.72f, 1f);
        [SerializeField, Min(0f)] private float keyLightIntensity = 1.25f;

        [Header("首场决战内容")]
        [SerializeField] private string defaultEncounterId = "first-showdown";
        [SerializeField] private PlayableEncounterCatalog encounterCatalog;
        [SerializeField] private CombatFieldRulesSO fieldRules;
        [SerializeField] private List<CharacterConfigSO> hunterSquad = new();
        [SerializeField] private List<CharacterActionCardData> sharedHunterCards = new();
        [SerializeField] private BossConfigSO boss;
        [SerializeField, Min(0.1f)] private float cellSize = 1f;

        [Header("狩猎地图内容")]
        [SerializeField] private PlayableHuntContentCatalog huntContent;
        [SerializeField] private PlayableHuntDestinationCatalog huntDestinations;

        [Header("营地开局内容")]
        [SerializeField] private PlayableSettlementContentCatalog settlementContent;
        [SerializeField] private PlayableWorkshopCatalog workshopContent;

        [Header("战斗装备映射")]
        [SerializeField] private PlayableCombatEquipmentCatalog combatEquipment;

        [Header("致命伤存活事件")]
        [SerializeField] private PlayableSurvivalEventCatalog survivalEvents;

        [Header("永久损伤")]
        [SerializeField] private PlayablePermanentInjuryCatalog permanentInjuries;

        [Header("症状成长")]
        [SerializeField] private PlayableSymptomCatalog symptoms;

        [Header("成长里程碑")]
        [SerializeField] private PlayableGrowthMilestoneCatalog growthMilestones;

        [Header("武器熟练度")]
        [SerializeField] private PlayableWeaponMasteryCatalog weaponMastery;

        public string EntrySceneName => entrySceneName;
        public GamePhase InitialPhase => initialPhase;
        public bool ShowStartMenu => showStartMenu;
        public bool ShowFlowGuide => showFlowGuide;
        public bool ShowSettlementHud => showSettlementHud;
        public float SettlementHudWidth => Mathf.Max(320f, settlementHudWidth);
        public bool ShowOpeningNarrative => showOpeningNarrative;
        public bool HideFrameworkDebugger => hideFrameworkDebugger;
        public string OpeningNarrative => openingNarrative;
        public string GameTitle => gameTitle;
        public string TitleTagline => titleTagline;
        public Vector3 BossCameraPosition => bossCameraPosition;
        public Vector3 BossCameraEulerAngles => bossCameraEulerAngles;
        public Color KeyLightColor => keyLightColor;
        public float KeyLightIntensity => Mathf.Max(0f, keyLightIntensity);
        public PlayableHuntContentCatalog HuntContent => huntContent;
        public PlayableHuntDestinationCatalog HuntDestinations => huntDestinations;
        public PlayableSettlementContentCatalog SettlementContent => settlementContent;
        public PlayableWorkshopCatalog WorkshopContent => workshopContent;
        public PlayableCombatEquipmentCatalog CombatEquipment => combatEquipment;
        public PlayableSurvivalEventCatalog SurvivalEvents => survivalEvents;
        public PlayablePermanentInjuryCatalog PermanentInjuries => permanentInjuries;
        public PlayableSymptomCatalog Symptoms => symptoms;
        public PlayableGrowthMilestoneCatalog GrowthMilestones => growthMilestones;
        public PlayableWeaponMasteryCatalog WeaponMastery => weaponMastery;
        public float CellSize => Mathf.Max(0.1f, cellSize);
        public string DefaultEncounterId => string.IsNullOrWhiteSpace(defaultEncounterId) ? "default" : defaultEncounterId.Trim();
        public PlayableEncounterCatalog EncounterCatalog => encounterCatalog;

        public bool CanCreateGame => boss != null && hunterSquad.Exists(config => config != null) && (encounterCatalog == null || encounterCatalog.IsConfigured) && huntContent != null && huntContent.IsConfigured && settlementContent != null && settlementContent.IsConfigured && workshopContent != null && combatEquipment != null && combatEquipment.IsConfigured && survivalEvents != null && survivalEvents.IsConfigured && permanentInjuries != null && permanentInjuries.IsConfigured && symptoms != null && symptoms.IsConfigured && growthMilestones != null && growthMilestones.IsConfigured && weaponMastery != null && weaponMastery.IsConfigured;

        public BattleSetup CreateBattleSetup()
        {
            return new BattleSetup
            {
                FieldRules = fieldRules,
                HunterSquad = new List<CharacterConfigSO>(hunterSquad),
                SharedHunterCards = new List<CharacterActionCardData>(sharedHunterCards),
                Boss = boss
            };
        }
    }
}
