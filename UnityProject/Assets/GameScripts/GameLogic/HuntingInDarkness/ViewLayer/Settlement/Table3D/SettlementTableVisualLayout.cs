using Cards3D;
using HuntingInDarkness.ViewLayer.Tabletop;
using UnityEngine;

namespace UI
{
    /// <summary>持久化营地桌面布局的引用容器；正式桌面由 Prefab 提供。</summary>
    [DisallowMultipleComponent]
    public sealed class SettlementTableVisualLayout : MonoBehaviour
    {
        [Header("分区")]
        [SerializeField] private HunterZone hunterZone;
        [SerializeField] private ResourceZone resourceZone;
        [SerializeField] private WorkshopZone workshopZone;
        [SerializeField] private InventionZone inventionZone;
        [SerializeField] private SquadZone squadZone;

        [Header("入口锚点")]
        [SerializeField] private Transform recruitmentAnchor;
        [SerializeField] private Transform facilityDutyAnchor;
        [SerializeField] private Transform campLedgerAnchor;
        [SerializeField] private Transform departureAnchor;

        [Header("扩展区")]
        [SerializeField] private SettlementExpansionAreaController workshopExpansion;
        [SerializeField] private SettlementExpansionAreaController resourceExpansion;
        [SerializeField] private SettlementExpansionAreaController inventionExpansion;
        [SerializeField] private SettlementExpansionAreaController previewExpansion;
        [SerializeField] private Renderer tableRenderer;
        [Header("固定入口卡")]
        [SerializeField] private RecruitmentLauncherCard3D recruitmentLauncher;
        [SerializeField] private SettlementFacilityDutyLauncherCard3D facilityDutyLauncher;
        [SerializeField] private CampLedgerLauncherCard3D campLedgerLauncher;
        [SerializeField] private TabletopDepartureLauncherCard3D departureLauncher;
        [SerializeField] private SettlementExpansionLauncherCard3D workshopExpansionLauncher;
        [SerializeField] private SettlementExpansionLauncherCard3D resourceExpansionLauncher;
        [SerializeField] private SettlementExpansionLauncherCard3D inventionExpansionLauncher;
        [SerializeField] private SettlementExpansionLauncherCard3D previewExpansionLauncher;

        public HunterZone HunterZone => hunterZone;
        public ResourceZone ResourceZone => resourceZone;
        public WorkshopZone WorkshopZone => workshopZone;
        public InventionZone InventionZone => inventionZone;
        public SquadZone SquadZone => squadZone;
        public Transform RecruitmentAnchor => recruitmentAnchor;
        public Transform FacilityDutyAnchor => facilityDutyAnchor;
        public Transform CampLedgerAnchor => campLedgerAnchor;
        public Transform DepartureAnchor => departureAnchor;
        public SettlementExpansionAreaController WorkshopExpansion => workshopExpansion;
        public SettlementExpansionAreaController ResourceExpansion => resourceExpansion;
        public SettlementExpansionAreaController InventionExpansion => inventionExpansion;
        public SettlementExpansionAreaController PreviewExpansion => previewExpansion;
        public Renderer TableRenderer => tableRenderer;
        public RecruitmentLauncherCard3D RecruitmentLauncher => recruitmentLauncher;
        public SettlementFacilityDutyLauncherCard3D FacilityDutyLauncher => facilityDutyLauncher;
        public CampLedgerLauncherCard3D CampLedgerLauncher => campLedgerLauncher;
        public TabletopDepartureLauncherCard3D DepartureLauncher => departureLauncher;
        public SettlementExpansionLauncherCard3D WorkshopExpansionLauncher => workshopExpansionLauncher;
        public SettlementExpansionLauncherCard3D ResourceExpansionLauncher => resourceExpansionLauncher;
        public SettlementExpansionLauncherCard3D InventionExpansionLauncher => inventionExpansionLauncher;
        public SettlementExpansionLauncherCard3D PreviewExpansionLauncher => previewExpansionLauncher;

        public bool HasRequiredZones => hunterZone != null && resourceZone != null && workshopZone != null && inventionZone != null;

        public void Configure(HunterZone hunter, ResourceZone resource, WorkshopZone workshop, InventionZone invention, SquadZone squad,
            Transform recruitment, Transform facilityDuty, Transform ledger, Transform departure,
            SettlementExpansionAreaController workshopArea, SettlementExpansionAreaController resourceArea,
            SettlementExpansionAreaController inventionArea, Renderer table,
            RecruitmentLauncherCard3D recruitmentCard, SettlementFacilityDutyLauncherCard3D facilityCard,
            CampLedgerLauncherCard3D ledgerCard, TabletopDepartureLauncherCard3D departureCard)
        {
            hunterZone = hunter;
            resourceZone = resource;
            workshopZone = workshop;
            inventionZone = invention;
            squadZone = squad;
            recruitmentAnchor = recruitment;
            facilityDutyAnchor = facilityDuty;
            campLedgerAnchor = ledger;
            departureAnchor = departure;
            workshopExpansion = workshopArea;
            resourceExpansion = resourceArea;
            inventionExpansion = inventionArea;
            tableRenderer = table;
            recruitmentLauncher = recruitmentCard;
            facilityDutyLauncher = facilityCard;
            campLedgerLauncher = ledgerCard;
            departureLauncher = departureCard;
        }

        public void ConfigureExpansionLaunchers(SettlementExpansionLauncherCard3D workshopCard, SettlementExpansionLauncherCard3D resourceCard, SettlementExpansionLauncherCard3D inventionCard)
        {
            workshopExpansionLauncher = workshopCard;
            resourceExpansionLauncher = resourceCard;
            inventionExpansionLauncher = inventionCard;
        }

        public void ConfigurePreviewExpansion(SettlementExpansionAreaController previewArea, SettlementExpansionLauncherCard3D previewCard)
        {
            previewExpansion = previewArea;
            previewExpansionLauncher = previewCard;
        }
    }
}
