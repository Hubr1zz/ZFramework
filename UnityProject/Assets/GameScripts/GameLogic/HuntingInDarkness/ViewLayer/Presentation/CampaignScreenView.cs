using System;
using System.Collections.Generic;
using System.Threading;
using Core;
using Cysharp.Threading.Tasks;
using Cards3D;
using GameplayBase;
using HuntingInDarkness.ActionFlow.Settlement;
using HuntingInDarkness.Combat;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Hunters;
using HuntingInDarkness.GameCore.Settlement;
using HuntingInDarkness.Hunt;
using HuntingInDarkness.Settlement;
using HuntingInDarkness.ViewLayer.Settlement;
using HuntingInDarkness.ViewLayer.Tabletop;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UI;

namespace HuntingInDarkness.ViewLayer.Presentation
{
    public sealed class CampaignScreenView : MonoBehaviour
    {
        [SerializeField] private GameObject hudRoot;
        [SerializeField] private GameObject managementPanel;
        [SerializeField] private TMP_Text phaseText;
        [SerializeField] private TMP_Text objectiveText;
        [SerializeField] private TMP_Text panelTitleText;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private Transform navigationContent;
        [SerializeField] private Transform entryContent;
        [SerializeField] private Transform warehouseContent;
        [SerializeField] private ScrollRect entryScrollRect;
        [SerializeField] private GridLayoutGroup warehouseGridLayout;
        [SerializeField] private ScreenEventChoice entryPrefab;
        [SerializeField] private ScreenEventChoice warehouseEntryPrefab;
        [SerializeField] private Button[] settlementTabs;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button departureButton;
        [SerializeField] private TMP_InputField recruitmentNameInput;

        private readonly List<ScreenEventChoice> entries = new();
        private readonly List<ScreenEventChoice> navigationEntries = new();
        private readonly List<int> departureHunterIds = new();
        private readonly HashSet<int> selectedHunterIds = new();
        private CampaignScreenWindow ownerWindow;
        private GameManager manager;
        private SettlementTable3D settlementTable;
        private PlayableHuntDestinationCatalog destinationCatalog;
        private HunterInstance selectedHunter;
        private ItemData selectedEquipmentItem;
        private InventionData focusedInvention;
        private string currentSection = "猎人";
        private string selectedHunterDetail = "概要";
        private PlayableHuntDestination selectedDestination;
        private bool isSubmitting;
        private bool mustAcknowledgeReturn;
        private string lastReturnRecordId = string.Empty;
        private CancellationTokenSource lifetimeSource;
        private IDisposable managementLease;
        private static int openPending;
        private static int requestGeneration;
        private static HuntRecord pendingReturnRecord;
        private static IReadOnlyDictionary<int, HunterReturnSnapshot> pendingReturnSnapshots;

        public sealed class HunterReturnSnapshot
        {
            public int Age;
            public int Courage;
            public int Understanding;
            public int UnspentGrowth;
            public bool IsAlive;
            public HunterAvailabilityState Availability;
            public readonly Dictionary<string, WeaponMasteryReturnSnapshot> WeaponMasteries = new(StringComparer.Ordinal);
            public readonly HashSet<string> PermanentInjuryIds = new(StringComparer.Ordinal);
        }

        public sealed class WeaponMasteryReturnSnapshot
        {
            public string DisplayName;
            public int Experience;
        }

        public static CampaignScreenView Current { get; private set; }
        public static bool IsProductionMode { get; private set; }
        public bool IsBound => manager != null && ownerWindow != null;

        private void Update()
        {
            if (!managementPanel.activeSelf || isSubmitting || mustAcknowledgeReturn || PhysicalInteractionScreenView.Current != null) return;
            PlayableSettlementEventView eventView = manager?.CurrentEventInput as PlayableSettlementEventView;
            if (eventView != null && eventView.IsPresenting) return;
            if (!Input.GetKeyDown(KeyCode.Escape) && !Input.GetMouseButtonDown(1)) return;
            ClosePanel();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Current = null;
            IsProductionMode = false;
            openPending = 0;
            requestGeneration = 0;
            pendingReturnRecord = null;
            pendingReturnSnapshots = null;
            pendingDestinationCatalog = null;
        }

        public static void ConfigureDestinationCatalog(PlayableHuntDestinationCatalog catalog)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (Current != null) Current.destinationCatalog = catalog;
            else pendingDestinationCatalog = catalog;
            IsProductionMode = true;
        }

        private static PlayableHuntDestinationCatalog pendingDestinationCatalog;

        public static async UniTask SetCampaignVisibleAsync(GameManager gameManager, GamePhase phase)
        {
            if (phase != GamePhase.Settlement)
            {
                System.Threading.Interlocked.Increment(ref requestGeneration);
                if (Current != null) Current.ownerWindow?.CloseOwnedWindow();
                return;
            }
            if (gameManager == null) return;
            if (Current != null)
            {
                if (Current.manager != gameManager) return;
                Current.BindManager(gameManager);
                Current.SetPhase(phase);
                return;
            }
            if (System.Threading.Interlocked.CompareExchange(ref openPending, 1, 0) != 0) return;
            int generation = System.Threading.Interlocked.Increment(ref requestGeneration);
            try
            {
                CampaignScreenWindow window = await GameModule.UI.ShowUIAsyncAwait<CampaignScreenWindow>();
                if (window?.View == null) throw new MissingReferenceException($"[{nameof(CampaignScreenView)}] 战役界面 Prefab 未绑定 View。");
                if (generation != System.Threading.Volatile.Read(ref requestGeneration) || gameManager == null || gameManager.CurrentGamePhase != GamePhase.Settlement)
                {
                    window.CloseOwnedWindow();
                    return;
                }
                window.View.destinationCatalog = pendingDestinationCatalog;
                window.View.BindManager(gameManager);
                window.View.SetPhase(GamePhase.Settlement);
                if (pendingReturnRecord != null) window.View.ShowReturnSummary(pendingReturnRecord, pendingReturnSnapshots);
            }
            finally
            {
                System.Threading.Interlocked.Exchange(ref openPending, 0);
            }
        }

        public static Dictionary<int, HunterReturnSnapshot> CaptureReturnSnapshots(IReadOnlyList<int> hunterIds, Func<int, HunterInstance> resolveHunter)
        {
            Dictionary<int, HunterReturnSnapshot> snapshots = new();
            if (hunterIds == null || resolveHunter == null) return snapshots;
            foreach (int hunterId in hunterIds)
            {
                HunterInstance hunter = resolveHunter(hunterId);
                if (hunter == null) continue;
                HunterReturnSnapshot snapshot = new()
                {
                    Age = hunter.Age,
                    Courage = hunter.Courage,
                    Understanding = hunter.Understanding,
                    UnspentGrowth = hunter.UnspentGrowth,
                    IsAlive = hunter.IsAlive,
                    Availability = hunter.Availability
                };
                foreach (WeaponMasteryState mastery in hunter.WeaponMasteries ?? new List<WeaponMasteryState>())
                    if (mastery != null && !string.IsNullOrWhiteSpace(mastery.MasteryId)) snapshot.WeaponMasteries[mastery.MasteryId] = new WeaponMasteryReturnSnapshot { DisplayName = mastery.DisplayName, Experience = mastery.Experience };
                foreach (string injuryId in hunter.PermanentInjuryIds ?? new List<string>())
                    if (!string.IsNullOrWhiteSpace(injuryId)) snapshot.PermanentInjuryIds.Add(injuryId);
                snapshots[hunterId] = snapshot;
            }
            return snapshots;
        }

        public static async UniTask PresentReturnSummaryAsync(GameManager gameManager, HuntRecord record, IReadOnlyDictionary<int, HunterReturnSnapshot> snapshots)
        {
            if (record == null || string.IsNullOrWhiteSpace(record.RecordId)) return;
            pendingReturnRecord = record;
            pendingReturnSnapshots = snapshots;
            await SetCampaignVisibleAsync(gameManager, GamePhase.Settlement);
            if (Current != null && Current.manager == gameManager) Current.ShowReturnSummary(record, snapshots);
        }

        public void BindWindow(CampaignScreenWindow window)
        {
            ownerWindow = window ?? throw new ArgumentNullException(nameof(window));
            ValidateReferences();
            Current = this;
            lifetimeSource = new CancellationTokenSource();
            WireButtons();
            managementPanel.SetActive(false);
            recruitmentNameInput.gameObject.SetActive(false);
            warehouseContent.gameObject.SetActive(false);
            entryScrollRect.content = entryContent as RectTransform;
        }

        public void NotifyWindowClosed()
        {
            lifetimeSource?.Cancel();
            lifetimeSource?.Dispose();
            lifetimeSource = null;
            managementLease?.Dispose();
            managementLease = null;
            if (Current == this) Current = null;
            ownerWindow = null;
        }

        public void BindManager(GameManager gameManager)
        {
            manager = gameManager ?? throw new ArgumentNullException(nameof(gameManager));
            if (pendingDestinationCatalog != null) destinationCatalog = pendingDestinationCatalog;
            BindSettlementTable(SettlementTable3D.Current);
        }

        public void BindSettlementTable(SettlementTable3D table)
        {
            settlementTable = table;
        }

        public void SetPhase(GamePhase phase)
        {
            if (hudRoot != null) hudRoot.SetActive(true);
            foreach (Button button in settlementTabs)
                if (button != null) button.gameObject.SetActive(true);
            phaseText.text = $"营地　·　第 {manager.SettlementData?.CurrentYear ?? 1} 年 · {settlementTable?.Manager?.Timeline?.CurrentSeason?.DisplayName ?? $"第 {(manager.SettlementData?.CurrentSeasonIndex ?? 0) + 1} 季"}";
            objectiveText.text = "整备猎人，确认携带物，然后选择目的地出发。";
            SetSettlementTabsVisible(!mustAcknowledgeReturn);
            if (managementPanel.activeSelf && !mustAcknowledgeReturn) RefreshCurrentSection();
        }

        public void ShowSection(string section)
        {
            ShowSection(section, false);
        }

        private void ShowSection(string section, bool preserveFocusedInvention)
        {
            if (isSubmitting || mustAcknowledgeReturn) return;
            if (!preserveFocusedInvention) focusedInvention = null;
            string nextSection = string.IsNullOrWhiteSpace(section) ? "猎人" : section;
            bool sectionChanged = currentSection != nextSection;
            bool enteringRecruitment = currentSection != "招募" && nextSection == "招募";
            currentSection = nextSection;
            if (enteringRecruitment) recruitmentNameInput.text = string.Empty;
            managementPanel.SetActive(true);
            managementLease ??= ScreenModalInputGate.Acquire(this);
            panelTitleText.text = currentSection;
            statusText.text = string.Empty;
            RefreshCurrentSection();
            if (!sectionChanged) return;
            Canvas.ForceUpdateCanvases();
            entryScrollRect.verticalNormalizedPosition = 1f;
            ScrollRect navigationScrollRect = navigationContent.GetComponentInParent<ScrollRect>(true);
            if (navigationScrollRect != null) navigationScrollRect.verticalNormalizedPosition = 1f;
        }

        public void OpenHunter(HunterInstance hunter)
        {
            if (hunter == null || mustAcknowledgeReturn || isSubmitting) return;
            selectedHunter = hunter;
            selectedHunterDetail = "概要";
            ShowSection("猎人详情");
        }

        public void OpenHunterGrowth(HunterInstance hunter) => SelectHunterFromWorld(hunter, "成长训练");
        public void OpenHunterSymptoms(HunterInstance hunter) => SelectHunterFromWorld(hunter, "症状");
        public void OpenHunterRecovery(HunterInstance hunter) => SelectHunterFromWorld(hunter, "休养");
        public void OpenHunterConsumables(HunterInstance hunter, ItemData item) => SelectHunterFromWorld(hunter, "装备与消耗品");
        public void OpenRecruitment() => ShowSection("招募");

        private void SelectHunterFromWorld(HunterInstance hunter, string detail)
        {
            if (hunter == null || mustAcknowledgeReturn || isSubmitting) return;
            selectedHunter = hunter;
            selectedHunterDetail = detail;
            ShowSection("猎人详情");
        }

        public void OpenDeparturePrep(IReadOnlyList<int> hunterIds)
        {
            if (mustAcknowledgeReturn || isSubmitting) return;
            departureHunterIds.Clear();
            selectedHunterIds.Clear();
            if (hunterIds != null)
                foreach (int hunterId in hunterIds)
                {
                    if (manager?.SettlementData?.GetHunter(hunterId)?.IsAvailable != true || departureHunterIds.Contains(hunterId)) continue;
                    departureHunterIds.Add(hunterId);
                    selectedHunterIds.Add(hunterId);
                }
            ShowSection("出发准备");
        }

        public void RequestFacility(string section)
        {
            ShowSection(section);
        }

        private void WireButtons()
        {
            closeButton.onClick.AddListener(ClosePanel);
            departureButton.onClick.AddListener(SubmitDeparture);
            for (int index = 0; index < settlementTabs.Length; index++)
            {
                int tabIndex = index;
                settlementTabs[index].onClick.AddListener(() => ShowSection(GetTabName(tabIndex)));
            }
        }

        private void ClosePanel()
        {
            if (isSubmitting || mustAcknowledgeReturn) return;
            managementPanel.SetActive(false);
            managementLease?.Dispose();
            managementLease = null;
        }

        private void RefreshCurrentSection()
        {
            if (mustAcknowledgeReturn) return;
            bool compactWarehouse = currentSection == "仓储";
            entryContent.gameObject.SetActive(!compactWarehouse);
            warehouseContent.gameObject.SetActive(compactWarehouse);
            entryScrollRect.content = (compactWarehouse ? warehouseContent : entryContent) as RectTransform;
            recruitmentNameInput.gameObject.SetActive(currentSection == "招募");
            ClearEntries();
            ClearNavigation();
            if (manager?.SettlementData == null) return;
            switch (currentSection)
            {
                case "猎人":
                    ShowHunters();
                    break;
                case "猎人详情":
                    ShowHunterDetails();
                    break;
                case "工坊":
                    ShowWorkshop();
                    break;
                case "发明":
                    ShowInventions();
                    break;
                case "仓储":
                    ShowWarehouse();
                    break;
                case "年鉴":
                    ShowLedger();
                    break;
                case "出发准备":
                    ShowDeparturePrep();
                    break;
                case "狩猎状态":
                    ShowHunters();
                    break;
                case "招募":
                    ShowRecruitment();
                    break;
                default:
                    ShowFacilityEntry();
                    break;
            }
            departureButton.gameObject.SetActive(currentSection == "出发准备" && !mustAcknowledgeReturn);
        }

        private void ShowHunters()
        {
            if (selectedHunter == null)
            {
                foreach (HunterInstance hunter in manager.SettlementData.Hunters)
                {
                    if (hunter == null) continue;
                    selectedHunter = hunter;
                    break;
                }
            }
            if (selectedHunter != null)
            {
                ShowHunterDetails();
                return;
            }
            AddEntry("暂无猎人", "招募猎人后，他们会出现在名单中。", "招募", true, OpenRecruitment);
        }

        private void ShowHunterDetails()
        {
            HunterInstance hunter = selectedHunter;
            if (hunter == null)
            {
                ShowHunters();
                return;
            }
            panelTitleText.text = $"猎人　/　{hunter.Name}";
            AddNavigation("猎人名单", "切换当前猎人。", false, null);
            foreach (HunterInstance rosterHunter in manager.SettlementData.Hunters)
                if (rosterHunter != null) AddNavigation($"{(rosterHunter == hunter ? "▶ " : string.Empty)}{rosterHunter.Name}", FormatHunterSummary(rosterHunter), true, () => OpenHunter(rosterHunter));
            AddNavigation("概要", "身体部位、意志和紧急状态", true, () => SelectHunterDetail("概要"));
            AddNavigation("装备与消耗品", "装备槽、库存和目标部位", true, () => SelectHunterDetail("装备与消耗品"));
            AddNavigation("休养", "逐部位恢复", true, () => SelectHunterDetail("休养"));
            AddNavigation("成长训练", "成长点与武器熟练度", true, () => SelectHunterDetail("成长训练"));
            AddNavigation("症状", "内化或克服", true, () => SelectHunterDetail("症状"));
            AddNavigation("招募", "候选人、人口和费用", true, OpenRecruitment);
            AddEntry("身体状态", $"头部 {hunter.HP.head}/{hunter.MaxHP.head}　躯干 {hunter.HP.body}/{hunter.MaxHP.body}\n手臂 {hunter.HP.arms}/{hunter.MaxHP.arms}　腿部 {hunter.HP.legs}/{hunter.MaxHP.legs}\n意志 {hunter.Willpower}/{hunter.WillpowerMax}　命运 {hunter.Luck}　待分配成长 {hunter.UnspentGrowth}", "四部位伤势分别处理", false, null);
            if (selectedHunterDetail == "装备与消耗品") ShowEquipmentActions(hunter);
            if (selectedHunterDetail == "休养") ShowRecoveryActions(hunter);
            if (selectedHunterDetail == "成长训练") ShowGrowthActions(hunter);
            if (selectedHunterDetail == "症状") ShowSymptomActions(hunter);
        }

        private void SelectHunterDetail(string section)
        {
            if (isSubmitting) return;
            selectedHunterDetail = section;
            RefreshCurrentSection();
        }

        private void ShowEquipmentActions(HunterInstance hunter)
        {
            if (hunter.Equipment != null)
            {
                for (int index = 0; index < hunter.Equipment.Count; index++)
                {
                    ItemInstance equipped = hunter.Equipment[index];
                    if (equipped?.Data == null) continue;
                    AddEntry($"装备槽 {index + 1} · {equipped.Data.itemName}", DescribeEquipment(equipped.Data), "卸下", settlementTable?.OnUnequipRequested != null, () => RunCommand(() => settlementTable.OnUnequipRequested(hunter.InstanceId, equipped.InstanceId), result => result.Succeeded ? "已卸下。" : result.Reason));
                }
            }
            if (hunter.Equipment != null)
                for (int index = hunter.Equipment.Count; index < EquipmentRules.MaximumEquipmentCount; index++)
                    AddEntry($"装备槽 {index + 1} · 空", selectedEquipmentItem == null ? "先从仓储选择武器或防具。" : $"{selectedEquipmentItem.itemName}　{DescribeEquipment(selectedEquipmentItem)}", selectedEquipmentItem == null ? "空槽" : "装备", selectedEquipmentItem != null && PlayableEquipmentRules.CanEquip(hunter, selectedEquipmentItem, out _) && settlementTable?.OnEquipRequested != null, selectedEquipmentItem == null ? null : () => EquipSelectedItem(hunter));
            foreach (ItemData item in PlayableSettlementContentRuntime.Items)
            {
                if (item == null || item.itemType != ItemType.Weapon && item.itemType != ItemType.Armor || manager.SettlementData.GetStoredItem(item) <= 0) continue;
                bool eligible = PlayableEquipmentRules.CanEquip(hunter, item, out string reason);
                AddEntry($"仓储 · {item.itemName}", $"数量 {manager.SettlementData.GetStoredItem(item)}　{DescribeEquipment(item)}\n{(eligible ? "可装备" : reason)}", selectedEquipmentItem == item ? "已选择" : "选择装备", eligible && settlementTable?.OnEquipRequested != null, () => SelectEquipmentItem(item));
            }
            foreach (ItemData item in PlayableSettlementContentRuntime.Items)
            {
                if (item == null || item.itemType != ItemType.Consumable || item.ConsumableEffect != ConsumableEffectKind.RecoverBodyPart || manager.SettlementData.GetStoredItem(item) <= 0) continue;
                foreach (HunterBodyPart part in Enum.GetValues(typeof(HunterBodyPart)))
                {
                    bool eligible = HunterRecoveryRules.CanRecover(hunter, part, out string reason) && hunter.IsAvailable;
                    AddEntry($"使用 {item.itemName} → {GetPartName(part)}", $"数量 {manager.SettlementData.GetStoredItem(item)}　{GetPartHealth(hunter, part)}\n{(eligible ? "可使用" : reason)}", eligible ? "使用" : reason, eligible && settlementTable?.OnConsumableRequested != null, () => RunCommand(() => settlementTable.OnConsumableRequested(hunter.InstanceId, item, part), result => result.Succeeded ? "消耗品已使用。" : result.Reason));
                }
            }
        }

        private void SelectEquipmentItem(ItemData item)
        {
            selectedEquipmentItem = item;
            RefreshCurrentSection();
        }

        private void EquipSelectedItem(HunterInstance hunter)
        {
            ItemData item = selectedEquipmentItem;
            if (item == null) return;
            RunCommand(() => settlementTable.OnEquipRequested(hunter.InstanceId, item), result => result.Succeeded ? $"已装备：{item.itemName}" : result.Reason);
            if (!isSubmitting) return;
            selectedEquipmentItem = null;
        }

        private static string DescribeEquipment(ItemData item)
        {
            if (item.itemType == ItemType.Weapon) return $"武器　威力 {item.weaponStats?.power ?? 0}　精准 {item.weaponStats?.accuracy ?? 0}　射程 {item.weaponStats?.range ?? 0}";
            if (item.itemType != ItemType.Armor || item.armorStats == null) return item.description ?? string.Empty;
            return $"防御　头 {item.armorStats.armorHead}　躯干 {item.armorStats.armorBody}　手臂 {item.armorStats.armorArms}　腿 {item.armorStats.armorLegs}";
        }

        private void ShowRecoveryActions(HunterInstance hunter)
        {
            if (settlementTable?.OnRecoveryRequested == null) return;
            PlayableSettlementContentCatalog catalog = settlementTable.SettlementContentCatalog;
            foreach (HunterBodyPart part in Enum.GetValues(typeof(HunterBodyPart)))
            {
                bool available = HunterRecoveryRules.CanRecover(hunter, part, out string reason);
                int cost = catalog?.RecoveryCost ?? 0;
                if (available && cost > 0 && (catalog?.RecoveryCostItem == null || manager.SettlementData.GetResource(catalog.RecoveryCostItem) < cost))
                {
                    available = false;
                    reason = catalog?.RecoveryCostItem == null ? "休养物资未配置" : $"缺少 {catalog.RecoveryCostItem.itemName} ×{cost}";
                }
                string costText = cost == 0 ? "免费" : $"费用 {catalog?.RecoveryCostItem?.itemName} ×{cost}";
                string summary = $"{GetPartHealth(hunter, part)}　{costText}　恢复 {catalog?.RecoveryAmount ?? 0} 点\n{(available ? "可休养" : reason)}";
                AddEntry($"休养：{GetPartName(part)}", summary, available ? "休养" : reason, available, () => RunCommand(() => settlementTable.OnRecoveryRequested(hunter.InstanceId, part), result => result.Succeeded ? $"{GetPartName(part)} {result.Recovery.PreviousHealth} → {result.Recovery.CurrentHealth}" : result.Reason));
            }
        }

        private void ShowGrowthActions(HunterInstance hunter)
        {
            foreach (HunterGrowthChoice choice in Enum.GetValues(typeof(HunterGrowthChoice)))
            {
                bool available = HunterAdvancementRules.CanSpendGrowth(hunter, choice, out string reason);
                AddEntry($"成长：{GetGrowthChoiceName(choice)}", $"剩余成长 {hunter.UnspentGrowth}\n{(available ? "可执行" : reason)}", available ? "成长" : reason, available && settlementTable?.OnGrowthRequested != null, () => RunCommand(() => settlementTable.OnGrowthRequested(hunter.InstanceId, choice), result => result.Succeeded ? $"{result.PreviousValue} → {result.CurrentValue}" : result.Reason));
            }
            PlayableWeaponMasteryCatalog catalog = PlayableWeaponMasteryRuntime.Catalog;
            if (catalog == null) return;
            foreach (WeaponMasteryFamilyDefinition family in catalog.GetFamilies())
            {
                bool masteryAvailable = WeaponMasteryRules.CanIncrease(hunter, family.Id);
                string reason = masteryAvailable ? string.Empty : "熟练度已达到上限";
                bool available = masteryAvailable && WeaponTrainingRules.CanTrain(hunter.IsAvailable && !hunter.IsDead, manager.SettlementData.IsInventionUnlocked(catalog.TrainingInventionId), manager.SettlementData.GetResource(catalog.TrainingCostItem), catalog.TrainingCost, family.Id, catalog.TrainingExperience, out reason);
                if (masteryAvailable && !available && string.IsNullOrWhiteSpace(reason)) reason = "缺少训练费用或尚未解锁训练发明";
                if (!masteryAvailable) reason = "熟练度已达到上限";
                AddEntry($"熟练度：{family.DisplayName}", $"费用 {catalog.TrainingCostItem?.itemName} ×{catalog.TrainingCost}　提升 {catalog.TrainingExperience}\n{(available ? "可训练" : reason)}", available ? "训练" : reason, available && settlementTable?.OnWeaponTrainingRequested != null, () => RunCommand(() => settlementTable.OnWeaponTrainingRequested(hunter.InstanceId, family.Id), result => result.Success ? result.MasteryOutcome.MasteryName : result.Reason));
            }
        }

        private void ShowSymptomActions(HunterInstance hunter)
        {
            if (hunter.SymptomStates == null) return;
            foreach (HunterSymptomState state in hunter.SymptomStates)
            {
                if (state == null || state.IsOvercome || PlayableSymptomRuntime.Catalog == null || !PlayableSymptomRuntime.Catalog.TryGetById(state.SymptomId, out SymptomDefinition definition)) continue;
                foreach (SymptomResolutionChoice choice in Enum.GetValues(typeof(SymptomResolutionChoice)))
                {
                    string symptomId = state.SymptomId;
                    bool available;
                    string reason;
                    if (choice == SymptomResolutionChoice.Internalize) available = HunterSymptomRules.CanInternalize(hunter, definition, manager.SettlementData.CurrentYear, out reason);
                    else available = HunterSymptomRules.CanOvercome(hunter, definition, out reason);
                    string cost = choice == SymptomResolutionChoice.Internalize ? $"意志 -{definition.ReflectionWillpowerCost}　进度 {state.InternalizationProgress}/{definition.InternalizationThreshold}" : $"胆识需 {definition.OvercomeCourageRequirement}　成长 -{definition.OvercomeGrowthCost}";
                    AddEntry($"{definition.DisplayName} · {GetSymptomChoiceName(choice)}", $"{definition.Description}\n{cost}\n{(available ? "可执行" : reason)}", available ? GetSymptomChoiceName(choice) : reason, available && settlementTable?.OnSymptomRequested != null, () => RunCommand(() => settlementTable.OnSymptomRequested(hunter.InstanceId, symptomId, choice), result => result.Succeeded ? result.SymptomName : result.Reason));
                }
            }
        }

        private void ShowWorkshop()
        {
            if (settlementTable?.Manager?.Workshop == null) return;
            AddNavigation("配方", "显示可制造物及材料缺口。", false, null);
            foreach (CraftRecipe recipe in settlementTable.Manager.Workshop.GetAvailableRecipes())
            {
                if (recipe == null || recipe.outputItem == null) continue;
                bool canCraft = settlementTable.Manager.Workshop.CanCraft(recipe, out string reason);
                AddEntry(recipe.recipeName, FormatRecipe(recipe), canCraft ? "制造" : reason, canCraft && settlementTable.OnCraftRequested != null, () => RunCommand(() => settlementTable.OnCraftRequested(recipe), result => result.Succeeded ? "制造完成。" : result.Reason));
            }
            foreach (PlayableWorkshopDefinition definition in settlementTable.WorkshopCatalog?.Workshops ?? Array.Empty<PlayableWorkshopDefinition>())
            {
                if (definition == null) continue;
                bool canBuild = settlementTable.WorkshopConstructionService.CanBuild(definition, out string reason);
                AddEntry($"设施：{definition.DisplayName}", FormatWorkshopCosts(definition), canBuild ? "建造" : reason, canBuild && settlementTable.OnWorkshopConstructionRequested != null, () => RunCommand(() => settlementTable.OnWorkshopConstructionRequested(definition), result => result.Succeeded ? "设施已建成。" : result.Reason));
            }
            AddNavigation("设施值守", "派遣、取消或结算值守。", true, () => ShowSection("设施值守"));
            AddEntry("设施值守", FormatFacilityDuties(), "派遣猎人", true, ShowFacilityDuties);
        }

        private void ShowInventions()
        {
            List<InventionData> inventions = focusedInvention != null ? new List<InventionData> { focusedInvention } : settlementTable?.Manager?.Inventions?.AllInventions ?? new List<InventionData>();
            foreach (InventionData invention in inventions)
            {
                if (invention == null) continue;
                bool unlocked = settlementTable.Manager.Inventions.IsUnlocked(invention);
                if (focusedInvention == null && !unlocked && !IsCurrentInventionCandidate(invention)) continue;
                string reason = string.Empty;
                bool canUnlock = !unlocked && settlementTable.Manager.Inventions.CanUnlock(invention, out reason);
                string status = unlocked ? "已掌握" : canUnlock ? "当前候选" : reason;
                AddEntry(invention.inventionName, $"费用：{FormatInventionCosts(invention)}", status, canUnlock && settlementTable.OnInventionUnlockRequested != null, () => RunCommand(() => settlementTable.OnInventionUnlockRequested(invention), result => result.Succeeded ? "已掌握。" : result.Reason), settlementTable.InventionZone.GetInspectionContent(invention));
                if (!unlocked) continue;
                foreach (InventionActiveEffect effect in invention.activeEffects)
                {
                    if (effect == null) continue;
                    bool available = InventionActiveEffectRules.CanActivate(unlocked, manager.SettlementData.CurrentYear, effect.effectId, effect.eventId, effect.maxUsesPerYear, manager.SettlementData.InventionActiveEffectUses, true, out string effectReason);
                    int used = InventionActiveEffectRules.GetUseCount(manager.SettlementData.InventionActiveEffectUses, effect.effectId, manager.SettlementData.CurrentYear);
                    string usage = effect.maxUsesPerYear == 0 ? "不限次数" : $"本年 {used}/{effect.maxUsesPerYear}";
                    AddEntry($"主动效果：{effect.effectName}", $"{effect.description}\n{usage}\n{(available ? "可使用" : effectReason)}", available ? "使用" : effectReason, available && settlementTable.OnInventionEffectRequested != null, () => ActivateInventionEffect(invention, effect));
                }
            }
            if (focusedInvention != null) return;
            InventionZone inventionZone = settlementTable.InventionZone;
            int remainingDraws = Mathf.Min(2, inventionZone.EligibleCount);
            if (inventionZone.HasPendingDraw)
                remainingDraws = inventionZone.RemainingDrawCount;
            else if (inventionZone.DrawnCount > 0)
                remainingDraws = 0;
            string drawStatus = "抽取最多两项";
            if (inventionZone.HasPendingDraw)
                drawStatus = "继续选择";
            else if (inventionZone.DrawnCount > 0)
                drawStatus = "已有候选";
            AddEntry("抽取候选", $"从当前符合条件的 {inventionZone.EligibleCount} 项发明中抽取，剩余选择 {remainingDraws} 项。", drawStatus, inventionZone.CanDrawCandidates, DrawInventionCandidates);
        }

        private bool IsCurrentInventionCandidate(InventionData candidate)
        {
            foreach (InventionData drawn in settlementTable.InventionZone.DrawnInventions)
                if (drawn == candidate) return true;
            return false;
        }

        private void DrawInventionCandidates()
        {
            DrawInventionCandidatesAsync().Forget();
        }

        public void UnlockInventionFromTable(InventionData invention)
        {
            if (invention == null || settlementTable?.OnInventionUnlockRequested == null) return;
            RunCommand(() => settlementTable.OnInventionUnlockRequested(invention), result => result.Succeeded ? "已掌握。" : result.Reason);
        }

        public void UseInventionEffectFromTable(InventionData invention)
        {
            if (invention == null || settlementTable?.OnInventionEffectRequested == null || !settlementTable.Manager.Inventions.IsUnlocked(invention)) return;
            InventionActiveEffect effect = null;
            int effectCount = 0;
            foreach (InventionActiveEffect candidate in invention.activeEffects)
            {
                if (candidate == null) continue;
                effect = candidate;
                effectCount++;
            }
            if (effectCount == 0) return;
            if (effectCount == 1)
            {
                ActivateInventionEffect(invention, effect);
                return;
            }
            focusedInvention = invention;
            ShowSection("发明", true);
        }

        private void ActivateInventionEffect(InventionData invention, InventionActiveEffect effect)
        {
            if (invention == null || effect == null || settlementTable?.OnInventionEffectRequested == null) return;
            if (!InventionActiveEffectRules.CanActivate(settlementTable.Manager.Inventions.IsUnlocked(invention), manager.SettlementData.CurrentYear, effect.effectId, effect.eventId, effect.maxUsesPerYear, manager.SettlementData.InventionActiveEffectUses, true, out string reason))
            {
                statusText.text = reason;
                return;
            }
            RunCommand(() => settlementTable.OnInventionEffectRequested(invention, new InventionActiveEffect { effectId = effect.effectId, effectName = effect.effectName, description = effect.description, eventId = effect.eventId, maxUsesPerYear = effect.maxUsesPerYear }), result => result.Succeeded ? "主动效果已启动。" : result.Reason);
        }

        private async UniTask DrawInventionCandidatesAsync()
        {
            string section = currentSection;
            await settlementTable.InventionZone.DrawCandidatesAsync(lifetimeSource?.Token ?? default);
            if (this != null && Current == this && currentSection == section) RefreshCurrentSection();
        }

        private void ShowWarehouse()
        {
            foreach (ResourceEntry item in manager.SettlementData.Resources)
                if (item != null) AddEntry(PlayableSettlementItemRegistry.GetDisplayName(item.Key), $"资源　×{item.Value}", "库存", false, null);
            foreach (ResourceEntry item in manager.SettlementData.EquipmentStorage)
                if (item != null) AddEntry(PlayableSettlementItemRegistry.GetDisplayName(item.Key), $"装备与消耗品　×{item.Value}", "库存", false, null);
        }

        private void ShowLedger()
        {
            AddNavigation("年鉴", "已结算事件与狩猎记录。", false, null);
            if (manager.SettlementData.Timeline == null) return;
            foreach (AnnalEntry entry in manager.SettlementData.Timeline)
                if (entry != null) AddEntry($"第 {entry.Year} 年 · {entry.EventName}", entry.EventId, entry.IsCompleted ? "已完成" : "待处理", false, null);
            foreach (HuntRecord record in manager.SettlementData.HuntHistory)
                if (record != null) AddEntry($"第 {record.Year} 年 · 狩猎归来", $"出发 {record.HuntersDeployed} 人，失去 {record.HuntersLost} 人 · {(record.BossDefeated ? "击败 Boss" : "未击败 Boss")}", "记录", false, null);
        }

        private void ShowDeparturePrep()
        {
            panelTitleText.text = "出发准备 · 队伍、目的地与携带物";
            departureButton.interactable = !isSubmitting;
            AddNavigation("队伍", "选择可出发猎人。", false, null);
            foreach (HunterInstance hunter in manager.SettlementData.GetAvailableHunters())
            {
                bool selected = selectedHunterIds.Contains(hunter.InstanceId);
                AddNavigation($"{(selected ? "☑" : "□")} {hunter.Name}", FormatHunterSummary(hunter), !isSubmitting, () => ToggleDepartureHunter(hunter));
            }
            AddEntry("队伍与携带物", SummarizeDepartureLoadout(), "出发前检查", false, null);
            if (destinationCatalog == null)
            {
                AddEntry("目的地尚未开放", "内容尚未准备完成。", "不可用", false, null);
                return;
            }
            foreach (PlayableHuntDestinationAvailability available in destinationCatalog.GetAvailability(manager.SettlementData.CurrentYear))
            {
                PlayableHuntDestination destination = available.Destination;
                if (destination == null) continue;
                bool selected = ReferenceEquals(selectedDestination, destination);
                AddEntry(destination.DisplayName, $"{destination.Description}\n资源：{destination.ResourceHint}　风险：{destination.DangerHint}\n{(available.IsAvailable ? "可出发" : available.Reason)}", selected ? "已选择" : available.IsAvailable ? "选为目的地" : available.Reason, available.IsAvailable && selectedHunterIds.Count > 0 && !isSubmitting, () => SelectDestination(destination));
            }
            bool hasSelectedDestination = selectedDestination != null && IsDestinationAvailable(selectedDestination);
            string departureReason = string.Empty;
            bool canDepart = selectedHunterIds.Count > 0 && hasSelectedDestination && DepartureRules.CanDepart(new List<int>(selectedHunterIds), out departureReason);
            departureButton.gameObject.SetActive(true);
            departureButton.interactable = !isSubmitting && canDepart;
            statusText.text = isSubmitting ? "正在提交…" : selectedHunterIds.Count == 0 ? "至少选择一名可出发的猎人。" : !hasSelectedDestination ? "选择一个符合当前条件的目的地。" : canDepart ? "队伍和目的地已就绪。" : departureReason;
        }

        private void ShowHuntStatus()
        {
            foreach (HunterInstance hunter in manager.ActiveHuntHunters)
                AddEntry(hunter.Name, FormatHunterSummary(hunter), "队伍状态", false, null);
            AddEntry("携带物", SummarizeDepartureLoadout(), "狩猎过程中按现有操作使用", false, null);
        }

        private void ShowFacilityEntry()
        {
            AddNavigation(currentSection, "当前管理分类。", false, null);
            if (currentSection == "设施值守") ShowFacilityDuties();
        }

        private void ToggleDepartureHunter(HunterInstance hunter)
        {
            if (mustAcknowledgeReturn || isSubmitting || hunter == null) return;
            if (!selectedHunterIds.Add(hunter.InstanceId)) selectedHunterIds.Remove(hunter.InstanceId);
            RefreshCurrentSection();
        }

        private void SelectDestination(PlayableHuntDestination destination)
        {
            if (destination == null || manager?.SettlementData == null || mustAcknowledgeReturn || isSubmitting || !IsDestinationAvailable(destination)) return;
            selectedDestination = destination;
            RefreshCurrentSection();
        }

        private void SubmitDeparture()
        {
            if (mustAcknowledgeReturn || isSubmitting) return;
            if (selectedHunterIds.Count == 0)
            {
                statusText.text = "至少选择一名可出发的猎人。";
                return;
            }
            if (selectedDestination == null)
            {
                statusText.text = "先选择一个可用目的地。";
                return;
            }
            if (!DepartureRules.CanDepart(new List<int>(selectedHunterIds), out string reason))
            {
                statusText.text = reason;
                return;
            }
            List<int> hunterIds = new(selectedHunterIds);
            RunCommand(() => manager.DepartForHuntAsync(hunterIds, selectedDestination), result =>
            {
                if (result.Succeeded) managementPanel.SetActive(false);
                return result.Succeeded ? "队伍已出发。" : result.Reason;
            });
        }

        private void ShowReturnSummary(HuntRecord record, IReadOnlyDictionary<int, HunterReturnSnapshot> snapshots)
        {
            if (manager?.SettlementData == null || record == null || record.RecordId == lastReturnRecordId) return;
            lastReturnRecordId = record.RecordId;
            pendingReturnRecord = null;
            pendingReturnSnapshots = null;
            currentSection = "狩猎归来";
            managementPanel.SetActive(true);
            managementLease ??= ScreenModalInputGate.Acquire(this);
            mustAcknowledgeReturn = true;
            closeButton.gameObject.SetActive(false);
            departureButton.gameObject.SetActive(false);
            SetSettlementTabsVisible(false);
            entryContent.gameObject.SetActive(true);
            warehouseContent.gameObject.SetActive(false);
            entryScrollRect.content = entryContent as RectTransform;
            panelTitleText.text = "狩猎归来";
            ClearEntries();
            ClearNavigation();
            int returnedHunters = Math.Max(0, record.HuntersDeployed - record.HuntersLost);
            AddEntry($"第 {record.Year} 年 · 狩猎归来", $"返回 {returnedHunters} 人 · 失去 {record.HuntersLost} 人\n{(record.BossDefeated ? "Boss 已击败。" : "Boss 未击败。")}", "结果已提交", false, null);
            if (record.CollectedItems != null)
                foreach (HuntingInDarkness.GameCore.Hunt.HuntLootStack item in record.CollectedItems)
                    if (item != null) AddEntry(PlayableSettlementItemRegistry.GetDisplayName(item.ItemId), $"带回 ×{item.Count}", "已入库", false, null);
            if (record.RescuedPopulation > 0) AddEntry("救援人口", $"实际归来 {record.RescuedPopulation} 人", "已计入营地", false, null);
            AddEntry("日历", $"当前为第 {manager.SettlementData.CurrentYear} 年 · {settlementTable?.Manager?.Timeline?.CurrentSeason?.DisplayName ?? $"第 {manager.SettlementData.CurrentSeasonIndex + 1} 季"}", "已推进", false, null);
            bool hasDepartureSnapshots = snapshots != null && record.ParticipantHunterIds != null && record.ParticipantHunterIds.Exists(hunterId => snapshots.ContainsKey(hunterId));
            AddEntry("猎人当前状态", hasDepartureSnapshots ? "已对照本次出发快照，列出成长、熟练度和永久损伤变化。" : "出发前快照未保留；下列为结算后的实际状态，不代表差值。", "当前记录", false, null);
            foreach (int hunterId in record.ParticipantHunterIds ?? new List<int>())
            {
                HunterInstance hunter = manager.SettlementData.GetHunter(hunterId);
                if (hunter == null) continue;
                string status = hunter.IsDead ? "已死亡" : hunter.Availability == HunterAvailabilityState.Retired ? "已退役" : "归来";
                HunterReturnSnapshot before = null;
                if (snapshots != null) snapshots.TryGetValue(hunterId, out before);
                AddEntry(hunter.Name, BuildHunterReturnSummary(hunter, before), status, false, null);
            }
            AddEntry("返回营地", "查看完结算后返回营地。", "返回营地", true, AcknowledgeReturn);
            Canvas.ForceUpdateCanvases();
            entryScrollRect.verticalNormalizedPosition = 1f;
        }

        private void AcknowledgeReturn()
        {
            mustAcknowledgeReturn = false;
            closeButton.gameObject.SetActive(true);
            SetSettlementTabsVisible(true);
            managementPanel.SetActive(false);
            managementLease?.Dispose();
            managementLease = null;
        }

        private void AddEntry(string title, string body, string status, bool enabled, Action onSelected, CardInspectionContent? inspectionContent = null)
        {
            bool isWarehouse = currentSection == "仓储" && !mustAcknowledgeReturn;
            Transform content = isWarehouse ? warehouseContent : entryContent;
            ScreenEventChoice entry = Instantiate(isWarehouse ? warehouseEntryPrefab : entryPrefab, content, false);
            entry.Configure(title, body, enabled && !isSubmitting, status, onSelected, onSelected == null);
            if (inspectionContent.HasValue) entry.SetInspectionContent(inspectionContent.Value);
            if (!isWarehouse) entry.FitCampaignContent(GetContentWidth(content));
            entries.Add(entry);
        }

        private void AddNavigation(string title, string body, bool enabled, Action onSelected)
        {
            ScreenEventChoice entry = Instantiate(entryPrefab, navigationContent, false);
            entry.Configure(title, body, enabled && !isSubmitting, string.Empty, onSelected, onSelected == null);
            entry.FitCampaignContent(GetContentWidth(navigationContent));
            navigationEntries.Add(entry);
        }

        private static float GetContentWidth(Transform content)
        {
            RectTransform contentRect = content as RectTransform;
            float width = contentRect != null ? contentRect.rect.width : 0f;
            if (width > 32f) return width;
            ScrollRect scrollRect = content.GetComponentInParent<ScrollRect>(true);
            if (scrollRect?.viewport == null) throw new MissingReferenceException($"[{nameof(CampaignScreenView)}] 列表容器没有有效 ScrollRect viewport：{content.name}");
            return scrollRect.viewport.rect.width;
        }

        private void ClearEntries()
        {
            foreach (ScreenEventChoice entry in entries)
                if (entry != null) Destroy(entry.gameObject);
            entries.Clear();
        }

        private void ClearNavigation()
        {
            foreach (ScreenEventChoice entry in navigationEntries)
                if (entry != null) Destroy(entry.gameObject);
            navigationEntries.Clear();
        }

        private void RunCommand<T>(Func<UniTask<T>> command, Func<T, string> presentResult)
        {
            if (isSubmitting || command == null || Current != this || ownerWindow == null || manager?.CurrentGamePhase != GamePhase.Settlement) return;
            RunCommandAsync(command, presentResult).Forget();
        }

        private async UniTaskVoid RunCommandAsync<T>(Func<UniTask<T>> command, Func<T, string> presentResult)
        {
            isSubmitting = true;
            statusText.text = "正在提交…";
            string resultFeedback = null;
            try
            {
                T result = await command();
                if (this == null || Current != this || ownerWindow == null) return;
                resultFeedback = presentResult != null ? presentResult(result) : string.Empty;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                resultFeedback = "操作未能完成，请查看当前条件后重试。";
            }
            finally
            {
                if (this != null && Current == this && ownerWindow != null)
                {
                    isSubmitting = false;
                    RefreshCurrentSection();
                    if (!string.IsNullOrWhiteSpace(resultFeedback)) statusText.text = resultFeedback;
                }
            }
        }

        private void ShowRecruitment()
        {
            PlayableSettlementContentCatalog catalog = settlementTable?.SettlementContentCatalog;
            if (catalog == null || settlementTable?.Manager == null)
            {
                AddEntry("招募目录未就绪", "招募内容尚未连接到当前营地。", "不可用", false, null);
                return;
            }
            int livingCount = manager.SettlementData.GetAliveHunters().Count;
            int resourceCost = RecruitmentRules.GetCost(livingCount, catalog.RecruitmentCost);
            int populationCost = RecruitmentRules.GetPopulationCost(livingCount, catalog.RecruitmentPopulationCost);
            bool available = RecruitmentRules.CanRecruit(manager.SettlementData.CurrentYear, manager.SettlementData.LastRecruitmentYear, livingCount, catalog.MaximumLivingHunters, manager.SettlementData.GetResource(catalog.RecruitmentCostItem), catalog.RecruitmentCost, manager.SettlementData.Population, catalog.RecruitmentPopulationCost, out string reason);
            string cost = $"费用 {catalog.RecruitmentCostItem?.itemName ?? "无"} ×{resourceCost}　人口 ×{populationCost}\n营地人口 {manager.SettlementData.Population}　存活猎人 {livingCount}/{catalog.MaximumLivingHunters}\n{(available ? "可招募" : reason)}";
            AddNavigation("候选猎人", $"{catalog.RecruitmentTemplates.Count} 个候选", false, null);
            foreach (HunterData template in catalog.RecruitmentTemplates)
            {
                if (template == null) continue;
                AddEntry(template.hunterName, cost, available ? "招募" : reason, available && settlementTable.OnRecruitRequested != null, () => Recruit(template));
            }
        }

        private void ShowFacilityDuties()
        {
            if (settlementTable?.Manager == null) return;
            foreach (SettlementFacilityDutyDefinition definition in settlementTable.Manager.FacilityDutyDefinitions)
            {
                if (definition == null) continue;
                bool facilityReady = manager.SettlementData.IsWorkshopBuilt(definition.RequiredFacilityId);
                bool inventionReady = string.IsNullOrWhiteSpace(definition.RequiredInventionId) || manager.SettlementData.IsInventionUnlocked(definition.RequiredInventionId);
                string eligibility = $"设施 {(facilityReady ? "已建成" : $"需建造 {definition.RequiredFacilityId}")}　发明 {(inventionReady ? "满足" : $"需 {definition.RequiredInventionId}")}\n持续 {definition.DurationSeasons} 季　判定 {definition.DiceCount}d{definition.DiceSides}\n{definition.Description}";
                foreach (HunterInstance hunter in manager.SettlementData.GetAvailableHunters())
                {
                    bool canAssign = facilityReady && inventionReady && !IsHunterAssigned(hunter.InstanceId) && !HasActiveDuty(definition.DutyId);
                    string status = canAssign ? "派遣" : !facilityReady ? $"需建造 {definition.RequiredFacilityId}" : !inventionReady ? $"需发明 {definition.RequiredInventionId}" : IsHunterAssigned(hunter.InstanceId) ? "猎人已在其他值守" : "此值守已进行";
                    AddEntry($"{definition.DisplayName} · {hunter.Name}", eligibility, status, canAssign && settlementTable.OnFacilityDutyAssignRequested != null, () => RunCommand(() => settlementTable.OnFacilityDutyAssignRequested(definition.DutyId, definition.RequiredFacilityId, hunter.InstanceId), result => result.Succeeded ? "值守已派出。" : result.Reason));
                }
            }
            if (manager.SettlementData.FacilityDuties == null) return;
            foreach (SettlementFacilityDutyState duty in manager.SettlementData.FacilityDuties)
            {
                if (duty == null || duty.Status != SettlementFacilityDutyStateStatus.Active) continue;
                bool due = SettlementFacilityDutyRules.IsDue(duty, manager.SettlementData.CurrentYear, settlementTable.Manager.CurrentSeasonIndex);
                string title = $"进行中：{duty.DutyId} · {manager.SettlementData.GetHunter(duty.AssignedHunterId)?.Name}";
                AddEntry(title, $"第 {duty.DueYear} 年第 {duty.DueSeasonIndex + 1} 季结束\n{(due ? "已到结算时点" : "尚未到期")}", due ? "结算" : "取消值守", true, due ? () => RunCommand(() => settlementTable.OnFacilityDutyResolveRequested(duty.DutyId), result => result.Succeeded ? $"判定 {result.Roll}，人口 +{result.PopulationGain}" : result.Reason) : () => RunCommand(() => settlementTable.OnFacilityDutyCancelRequested(duty.DutyId), result => result.Succeeded ? "值守已取消。" : result.Reason));
            }
        }

        private bool HasActiveDuty(string dutyId) => manager.SettlementData.FacilityDuties.Exists(item => item != null && item.Status == SettlementFacilityDutyStateStatus.Active && item.DutyId == dutyId);
        private bool IsHunterAssigned(int hunterId) => manager.SettlementData.FacilityDuties.Exists(item => item != null && item.Status == SettlementFacilityDutyStateStatus.Active && item.AssignedHunterId == hunterId);

        private string FormatFacilityDuties()
        {
            List<string> rows = new();
            foreach (SettlementFacilityDutyDefinition definition in settlementTable?.Manager?.FacilityDutyDefinitions ?? Array.Empty<SettlementFacilityDutyDefinition>())
                rows.Add($"{definition.DisplayName} · {definition.RequiredFacilityId} · {definition.DurationSeasons} 季");
            return rows.Count == 0 ? "当前没有配置值守项目。" : string.Join("\n", rows);
        }

        private string FormatWorkshopCosts(PlayableWorkshopDefinition definition)
        {
            List<string> costs = new();
            foreach (PlayableWorkshopCost cost in definition.Costs)
                if (cost?.Item != null) costs.Add($"{cost.Item.itemName} ×{cost.Amount}（有 {manager.SettlementData.GetResource(cost.Item)}）");
            return $"{definition.Description}\n建造费用：{(costs.Count == 0 ? "无" : string.Join("、", costs))}";
        }

        private string FormatInventionCosts(InventionData invention)
        {
            List<string> costs = new();
            foreach (InventionCost cost in invention.costs)
                if (cost?.resource != null) costs.Add($"{cost.resource.itemName} ×{cost.count}（有 {manager.SettlementData.GetResource(cost.resource)}）");
            return $"解锁费用：{(costs.Count == 0 ? "无" : string.Join("、", costs))}";
        }

        private static string GetPartHealth(HunterInstance hunter, HunterBodyPart part) => part switch
        {
            HunterBodyPart.Head => $"头部 {hunter.HP.head}/{hunter.MaxHP.head}",
            HunterBodyPart.Torso => $"躯干 {hunter.HP.body}/{hunter.MaxHP.body}",
            HunterBodyPart.Arms => $"手臂 {hunter.HP.arms}/{hunter.MaxHP.arms}",
            HunterBodyPart.Legs => $"腿部 {hunter.HP.legs}/{hunter.MaxHP.legs}",
            _ => string.Empty
        };

        private void ValidateReferences()
        {
            if (hudRoot == null || managementPanel == null || phaseText == null || objectiveText == null || panelTitleText == null || statusText == null || navigationContent == null || entryContent == null || warehouseContent == null || entryScrollRect == null || warehouseGridLayout == null || entryPrefab == null || warehouseEntryPrefab == null || closeButton == null || departureButton == null || recruitmentNameInput == null || settlementTabs == null || settlementTabs.Length != 6)
                throw new MissingReferenceException($"[{nameof(CampaignScreenView)}] 战役屏幕的序列化引用未完整绑定。");
        }

        private static string GetTabName(int index) => index switch { 0 => "猎人", 1 => "工坊", 2 => "发明", 3 => "仓储", 4 => "年鉴", 5 => "出发准备", _ => "猎人" };
        private static string GetPartName(HunterBodyPart part) => part switch { HunterBodyPart.Head => "头部", HunterBodyPart.Torso => "躯干", HunterBodyPart.Arms => "手臂", HunterBodyPart.Legs => "腿部", _ => part.ToString() };
        private static string FormatHunterSummary(HunterInstance hunter) => $"意志 {hunter.Willpower}/{hunter.WillpowerMax}　命运 {hunter.Luck}\n头 {hunter.HP.head}/{hunter.MaxHP.head}　躯 {hunter.HP.body}/{hunter.MaxHP.body}　臂 {hunter.HP.arms}/{hunter.MaxHP.arms}　腿 {hunter.HP.legs}/{hunter.MaxHP.legs}";
        private static string ResolveUnavailableReason(HunterInstance hunter) => hunter.IsDead ? "已死亡" : hunter.IsAvailable ? string.Empty : "当前不能出发";

        private string FormatRecipe(CraftRecipe recipe)
        {
            List<string> costs = new();
            if (recipe.ingredients != null)
                foreach (RecipeIngredient ingredient in recipe.ingredients)
                    if (ingredient?.item != null) costs.Add($"{ingredient.item.itemName} ×{ingredient.count}（有 {settlementTable.Manager.Workshop.GetIngredientAmount(ingredient.item.itemType == ItemType.Resource ? CraftIngredientSource.ResourcePool : CraftIngredientSource.StoredItemPool, ingredient.item.ContentId)}）");
            return $"消耗 {string.Join("、", costs)}\n产出 {recipe.outputItem.itemName} ×{recipe.outputCount}";
        }

        private string SummarizeDepartureLoadout()
        {
            List<string> lines = new();
            foreach (int hunterId in selectedHunterIds)
            {
                HunterInstance hunter = manager.SettlementData.GetHunter(hunterId);
                if (hunter == null) continue;
                List<string> items = new();
                foreach (ItemInstance equipment in hunter.Equipment ?? new List<ItemInstance>())
                    if (equipment?.Data != null) items.Add(equipment.Data.itemName);
                lines.Add($"{hunter.Name}：{(items.Count == 0 ? "未装备物品" : string.Join("、", items))}");
            }
            return lines.Count == 0 ? "尚未编入猎人。" : string.Join("\n", lines);
        }

        private bool IsDestinationAvailable(PlayableHuntDestination destination)
        {
            if (destinationCatalog == null || manager?.SettlementData == null) return false;
            foreach (PlayableHuntDestinationAvailability availability in destinationCatalog.GetAvailability(manager.SettlementData.CurrentYear))
                if (ReferenceEquals(availability.Destination, destination)) return availability.IsAvailable;
            return false;
        }

        private void Recruit(HunterData template)
        {
            if (template == null || settlementTable?.OnRecruitRequested == null || mustAcknowledgeReturn || isSubmitting) return;
            string requestedName = string.IsNullOrWhiteSpace(recruitmentNameInput.text) ? template.hunterName : recruitmentNameInput.text.Trim();
            RunCommand(() => settlementTable.OnRecruitRequested(template, requestedName), result => result.Succeeded ? $"{result.Hunter.Name} 已加入营地。" : result.Reason);
        }

        private void SetSettlementTabsVisible(bool visible)
        {
            foreach (Button button in settlementTabs)
                if (button != null) button.gameObject.SetActive(visible);
        }

        private static string GetGrowthChoiceName(HunterGrowthChoice choice) => choice switch
        {
            HunterGrowthChoice.Courage => "胆识",
            HunterGrowthChoice.Understanding => "理解",
            _ => "成长"
        };

        private static string GetSymptomChoiceName(SymptomResolutionChoice choice) => choice switch
        {
            SymptomResolutionChoice.Internalize => "内化",
            SymptomResolutionChoice.Overcome => "克服",
            _ => "处理症状"
        };

        private static string FormatPermanentInjuries(HunterInstance hunter)
        {
            if (hunter.PermanentInjuryIds == null || hunter.PermanentInjuryIds.Count == 0) return "无";
            List<string> names = new();
            foreach (string injuryId in hunter.PermanentInjuryIds)
                names.Add(PlayablePermanentInjuryRuntime.Catalog != null && PlayablePermanentInjuryRuntime.Catalog.TryGet(injuryId, out PermanentInjury injury) ? injury.DisplayName : "记录不可用");
            return string.Join("、", names);
        }

        private static string FormatMasteries(HunterInstance hunter)
        {
            if (hunter.WeaponMasteries == null || hunter.WeaponMasteries.Count == 0) return "无";
            List<string> names = new();
            foreach (WeaponMasteryState mastery in hunter.WeaponMasteries)
                if (mastery != null) names.Add($"{mastery.DisplayName} {mastery.Experience}");
            return names.Count == 0 ? "无" : string.Join("、", names);
        }

        private static string BuildHunterReturnSummary(HunterInstance hunter, HunterReturnSnapshot before)
        {
            string currentStatus = $"{(hunter.IsDead ? "已死亡" : hunter.Availability == HunterAvailabilityState.Retired ? "已退役" : "归来")} · 意志 {hunter.Willpower}/{hunter.WillpowerMax} · 命运 {hunter.Luck} · 成长点 {hunter.UnspentGrowth}";
            string health = $"头 {hunter.HP.head}/{hunter.MaxHP.head}　躯干 {hunter.HP.body}/{hunter.MaxHP.body}　手臂 {hunter.HP.arms}/{hunter.MaxHP.arms}　腿 {hunter.HP.legs}/{hunter.MaxHP.legs}";
            if (before == null) return $"出发前快照未保留，仅显示当前状态。\n{currentStatus}\n{health}\n永久损伤 {FormatPermanentInjuries(hunter)} · 熟练度 {FormatMasteries(hunter)}";
            List<string> changes = new();
            if (hunter.Age != before.Age) changes.Add($"年龄 {before.Age} → {hunter.Age}");
            if (hunter.UnspentGrowth != before.UnspentGrowth) changes.Add($"成长点 {before.UnspentGrowth} → {hunter.UnspentGrowth}（{FormatSigned(hunter.UnspentGrowth - before.UnspentGrowth)}）");
            if (hunter.Courage != before.Courage) changes.Add($"胆识 {before.Courage} → {hunter.Courage}");
            if (hunter.Understanding != before.Understanding) changes.Add($"理解 {before.Understanding} → {hunter.Understanding}");
            AppendMasteryChanges(hunter, before, changes);
            List<string> newInjuries = GetNewPermanentInjuries(hunter, before);
            changes.Add(newInjuries.Count == 0 ? "无新增永久损伤" : $"新增永久损伤：{string.Join("、", newInjuries)}");
            if (before.IsAlive && !hunter.IsAlive) changes.Add("状态变化：死亡");
            else if (before.Availability != HunterAvailabilityState.Retired && hunter.Availability == HunterAvailabilityState.Retired) changes.Add("状态变化：退役");
            return $"出发前后变化：{(changes.Count == 0 ? "无" : string.Join("；", changes))}\n{currentStatus}\n{health}\n永久损伤 {FormatPermanentInjuries(hunter)} · 熟练度 {FormatMasteries(hunter)}";
        }

        private static void AppendMasteryChanges(HunterInstance hunter, HunterReturnSnapshot before, List<string> changes)
        {
            foreach (WeaponMasteryState mastery in hunter.WeaponMasteries ?? new List<WeaponMasteryState>())
            {
                if (mastery == null || string.IsNullOrWhiteSpace(mastery.MasteryId)) continue;
                int previous = before.WeaponMasteries.TryGetValue(mastery.MasteryId, out WeaponMasteryReturnSnapshot oldMastery) ? oldMastery.Experience : 0;
                if (previous != mastery.Experience) changes.Add($"熟练度·{mastery.DisplayName} {previous} → {mastery.Experience}");
            }
            foreach (KeyValuePair<string, WeaponMasteryReturnSnapshot> mastery in before.WeaponMasteries)
                if ((hunter.WeaponMasteries == null || hunter.WeaponMasteries.Find(item => item != null && item.MasteryId == mastery.Key) == null) && mastery.Value.Experience > 0) changes.Add($"熟练度·{mastery.Value.DisplayName} {mastery.Value.Experience} → 0");
        }

        private static List<string> GetNewPermanentInjuries(HunterInstance hunter, HunterReturnSnapshot before)
        {
            List<string> names = new();
            foreach (string injuryId in hunter.PermanentInjuryIds ?? new List<string>())
            {
                if (before.PermanentInjuryIds.Contains(injuryId)) continue;
                names.Add(PlayablePermanentInjuryRuntime.Catalog != null && PlayablePermanentInjuryRuntime.Catalog.TryGet(injuryId, out PermanentInjury injury) ? injury.DisplayName : "未知永久损伤");
            }
            return names;
        }

        private static string FormatSigned(int value) => value > 0 ? $"+{value}" : value.ToString();

        private void OnDestroy()
        {
            NotifyWindowClosed();
        }
    }
}
