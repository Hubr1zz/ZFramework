using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Config;
using GameplayBase;
using GameplayBase.Board;
using GameplayBase.Card.Effect;
using GameplayBase.CombatSystem;
using GameplayBase.Config;
using HuntingInDarkness.Data;
using HuntingInDarkness.Hunt;
using HuntingInDarkness.GameCore.Hunters;
using HuntingInDarkness.GameCore.Settlement;
using HuntingInDarkness.Settlement;
using HuntingInDarkness.Combat;
using HuntingInDarkness.ActionFlow;
using HuntingInDarkness.ActionFlow.Settlement;
using HuntingInDarkness.ActionFlow.Hunt;
using HuntingInDarkness.ActionFlow.Campaign;
using HuntingInDarkness.ActionFlow.Events;
using HuntingInDarkness.ActionFlow.Inventions;
using HuntingInDarkness.ActionFlow.Presentation;
using HuntingInDarkness.ViewLayer.Flow;
using HuntingInDarkness.ViewLayer.Tabletop;
using HuntingInDarkness.ViewLayer.Hunt;
using SO.Combat;
using TMPro;
using UI;
using UI.Hunt;
using UI.Settlement;
using UnityEngine;
using UnityEngine.UI;

namespace Core
{
    /// <summary>
    /// Unity 组合壳与兼容 facade。持久单例。
    /// 管理三个游戏大阶段（Settlement / Hunt / BossFight）的根物体开关，
    /// 以及场景表现与兼容入口；战役运行态由 CampaignFlowCoordinator 持有。
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        // ─── 单例 ─────────────────────────────────────────────────────
        public static GameManager Instance { get; private set; }

        // ─── Inspector — 阶段根物体 ──────────────────────────────────

        [Header("阶段根物体（场景中预配置）")]
        [SerializeField] private GameObject settlementRoot;
        [SerializeField] private GameObject huntRoot;
        [SerializeField] private GameObject bossFightRoot;

        public Transform TabletopPresentationRoot
        {
            get
            {
                GameObject phaseRoot = CurrentGamePhase == GamePhase.Hunt ? huntRoot : settlementRoot;
                return phaseRoot != null ? phaseRoot.transform : transform;
            }
        }

        [Header("UI 阶段根节点（Canvas 子节点）")]
        [SerializeField] private GameObject uiSettlement;
        [SerializeField] private GameObject uiHunt;
        [SerializeField] private GameObject uiBossFight;
        [SerializeField] private GameObject uiShared;

        [Header("相机管理（挂在 Main Camera 上的 GameCameraManager）")]
        [SerializeField] private UI.GameCameraManager cameraManager;

        [Header("实体工厂（角色 Prefab；为空则自动创建并程序化回退）")]
        [SerializeField] private UI.EntityCreator entityCreator;

        [Header("本地化 / 字体")]
        [SerializeField] private TMP_FontAsset chineseFontAsset;
        [SerializeField] private TextAsset chineseCharacterSet;
        [SerializeField] private TextAsset localizationTable;

        [Header("开发者模式")]
        [SerializeField] private bool devMode = false;
        [SerializeField] private GamePhase devStartPhase = GamePhase.Settlement;

        // ─── Inspector 配置 ───────────────────────────────────────────

        [Header("角色配置（ScriptableObject）")]
        [SerializeField] private List<CharacterConfigSO> characterConfigs;

        [Header("Boss配置（ScriptableObject）")]
        [SerializeField] private BossConfigSO bossConfig;

        [Header("场地规则（ScriptableObject；缺省则自动生成默认场地）")]
        [SerializeField] private CombatFieldRulesSO fieldRules;

        [Header("棋盘")]
        [Tooltip("仅当未提供场地规则时作为默认半径的回退值")]
        [SerializeField] private int arenaRadius = 3;
        [SerializeField] private float cellSize  = 1.2f;

        [Header("棋盘视觉")]
        [SerializeField] private float tileHeight     = 0.08f;
        [SerializeField] private float tileScale      = 0.92f;
        [SerializeField] private Color tileIdleColor  = new(0.35f, 0.35f, 0.40f, 1f);
        [SerializeField] private Color tileHighlight  = new(0.30f, 0.85f, 0.30f, 1f);
        [SerializeField] private Color tileOccupied   = new(0.80f, 0.30f, 0.30f, 1f);

        [Header("实体视觉（临时胶囊）")]
        [SerializeField] private float characterHeight = 1.0f;
        [SerializeField] private float characterRadius = 0.25f;
        [SerializeField] private float bossHeight      = 1.6f;
        [SerializeField] private float bossRadius      = 0.4f;
        [SerializeField] private Color characterColor  = new(0.25f, 0.45f, 0.95f, 1f);
        [SerializeField] private Color bossColor       = new(0.85f, 0.15f, 0.15f, 1f);

        [Header("卡牌展台布局")]
        [SerializeField] private float   tableHeightOffset = 2.0f;
        [SerializeField] private float   tableScale        = 0.15f;  // 展台缩放（匹配胶囊体大小）
        [SerializeField] private Vector3 bossTablePosition = new(0f, 0f, 6.5f);

        // ─── 子系统（纯 C#）───────────────────────────────────────────

        private CampaignFlowCoordinator campaignFlow;
        private CampaignAccessPorts campaignAccess;
        private CampaignUnityBridge campaignUnityBridge;
        private GlobalTabletopPresentation globalTabletopPresentation;
        private CampaignDeveloperCommands developerCommands;
        [SerializeField] private SettlementTable3D _settlementTable3D;
        private DevModePanel         _devPanel;
        private BattleSetup preAwakePendingSetup;
        private IPlayableEventInput preAwakeEventInput;
        private IPlayableHuntDepartureInput preAwakeHuntDepartureInput;
        private bool hasAwakened;
        private bool hasBootstrapConfiguration;
        private ICampaignPersistencePort configuredCampaignPersistence;
        private bool configuredWaitForEntrySelection;
        [SerializeField] private PhysicalDiceTabletopPresenter tabletopRandomPresenter;
        [SerializeField] private TabletopCardInteractionPresenter tabletopCardPresenter;
        [SerializeField] private Vector3 tabletopDiceAnchorOffset = new(0f, 0f, -1.65f);
        private ITabletopRandomInteractionPresenter tabletopInteractionRouter;
        private ITabletopRandomInteractionPresenter configuredTabletopInteraction;
        private PlayableSettlementContentCatalog settlementContentCatalog;
        private PlayableWorkshopCatalog workshopContentCatalog;

        /// <summary>在 GameManager 激活前一次性提交组合根配置。</summary>
        public bool ConfigureCampaign(CampaignBootstrapRequest request)
        {
            if (request == null) throw new System.ArgumentNullException(nameof(request));
            if (hasAwakened || hasBootstrapConfiguration || gameObject.activeInHierarchy) return false;

            if (request.BattleSetup != null) preAwakePendingSetup = request.BattleSetup;
            if (request.CellSize.HasValue) cellSize = Mathf.Max(0.01f, request.CellSize.Value);
            if (request.EntityCreator != null) entityCreator = request.EntityCreator;
            if (request.ChineseFontAsset != null) chineseFontAsset = request.ChineseFontAsset;
            if (request.ChineseCharacterSet != null) chineseCharacterSet = request.ChineseCharacterSet;
            if (request.SettlementContent != null) settlementContentCatalog = request.SettlementContent;
            if (request.WorkshopContent != null) workshopContentCatalog = request.WorkshopContent;
            if (request.WaitForEntrySelection.HasValue) configuredWaitForEntrySelection = request.WaitForEntrySelection.Value;
            if (request.TabletopInteraction != null) configuredTabletopInteraction = request.TabletopInteraction;
            if (request.Persistence != null) configuredCampaignPersistence = request.Persistence;
            if (request.DevelopmentStartPhase.HasValue)
            {
                devMode = true;
                devStartPhase = request.DevelopmentStartPhase.Value;
            }
            else
            {
                devMode = false;
                devStartPhase = GamePhase.Settlement;
            }
            hasBootstrapConfiguration = true;
            return true;
        }

        internal ICampaignReadModel CampaignReadModel => campaignAccess;
        internal ICampaignCommandPort CampaignCommands => campaignAccess;
        internal ICampaignDiagnostics CampaignDiagnostics => campaignAccess;
        private IPlayableSettlementGameplayPort SettlementGameplay => CampaignCommands?.SettlementGameplay;
        internal IPlayableShowdownGameplayPort ShowdownGameplay => campaignFlow?.ShowdownGameplay;

        public CampaignStartupState CampaignStartupState => CampaignReadModel?.StartupState ?? (configuredWaitForEntrySelection ? CampaignStartupState.AwaitingChoice : CampaignStartupState.Active);

        public UniTask<bool> HasCampaignSaveAsync(CancellationToken cancellationToken = default) => CampaignCommands != null ? CampaignCommands.HasSaveAsync(cancellationToken) : UniTask.FromResult(false);

        public UniTask<bool> DeleteCampaignSaveAsync(CancellationToken cancellationToken = default) => CampaignCommands != null ? CampaignCommands.DeleteSaveAsync(cancellationToken) : UniTask.FromResult(false);

        public UniTask<CampaignStartupResult> StartNewCampaignAsync(CancellationToken cancellationToken = default) => CampaignCommands != null ? CampaignCommands.StartNewAsync(cancellationToken) : UniTask.FromResult(CampaignStartupResult.Failed(CampaignStartupState.AwaitingChoice, "战役组合根尚未初始化。"));

        public UniTask<CampaignStartupResult> ContinueCampaignAsync(CancellationToken cancellationToken = default) => CampaignCommands != null ? CampaignCommands.ContinueAsync(cancellationToken) : UniTask.FromResult(CampaignStartupResult.Failed(CampaignStartupState.AwaitingChoice, "战役组合根尚未初始化。"));

        // ═══════════════════════════════════════════
        // 初始化
        // ═══════════════════════════════════════════

        private void Awake()
        {
            hasAwakened = true;
            // 单例
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // 本地化 & 字体图集（最先初始化，确保后续 UI 文字不缺字）
            LocalizationManager.Initialize(chineseFontAsset, chineseCharacterSet, localizationTable);

            // 确保阶段根物体存在（若 Inspector 未配置则自动创建）
            EnsureRootObjects();
            if (tabletopRandomPresenter == null)
                tabletopRandomPresenter = GetComponent<PhysicalDiceTabletopPresenter>() ?? gameObject.AddComponent<PhysicalDiceTabletopPresenter>();
            if (tabletopCardPresenter == null)
                tabletopCardPresenter = GetComponent<TabletopCardInteractionPresenter>() ?? gameObject.AddComponent<TabletopCardInteractionPresenter>();
            tabletopInteractionRouter = configuredTabletopInteraction ?? new TabletopRandomInteractionRouter(tabletopRandomPresenter, tabletopCardPresenter);

            campaignFlow = new CampaignFlowCoordinator(new CampaignFlowBindings
            {
                ApplyPhaseRoots = ApplyPhaseRoots,
                DeactivatePhaseRoots = DeactivatePhaseRoots,
                TryCreateCombatConfiguration = TryCreateCombatConfiguration,
                ResolveLifetimeToken = this.GetCancellationTokenOnDestroy,
                PresentDepartureBlockedNotice = reason => globalTabletopPresentation?.PresentDepartureBlocked(reason),
                ClearDepartureBlockedNotice = () => globalTabletopPresentation?.ClearDepartureBlocked(),
                ResetSettlementNotices = () => globalTabletopPresentation?.ResetSettlementNotices(),
                SettlementLoadCompleted = succeeded => SettlementProgressLoadCompleted?.Invoke(succeeded),
                Info = message => Debug.Log($"[GameManager] {message}"),
                Error = message => Debug.LogError($"[GameManager] {message}"),
                SettlementTable = _settlementTable3D,
                SettlementRoot = settlementRoot,
                HuntRoot = huntRoot,
                UiHunt = uiHunt,
                WorkshopCatalog = workshopContentCatalog,
                SettlementContentCatalog = settlementContentCatalog,
                TabletopInteraction = tabletopInteractionRouter,
                Warning = message => Debug.LogWarning($"[GameManager] {message}")
            }, configuredCampaignPersistence ?? new SaveLoadSystemCampaignPersistenceAdapter(), configuredWaitForEntrySelection);
            campaignAccess = new CampaignAccessPorts(campaignFlow);
            developerCommands = new CampaignDeveloperCommands(campaignFlow, this.GetCancellationTokenOnDestroy, message => Debug.Log($"[GameManager][Dev] {message}"), message => Debug.LogWarning($"[GameManager] {message}"));
            globalTabletopPresentation = new GlobalTabletopPresentation(gameObject, campaignAccess, campaignAccess, settlementRoot, huntRoot, tabletopDiceAnchorOffset, () => campaignFlow?.HuntTabletopInteractionAnchor, this.GetCancellationTokenOnDestroy);
            tabletopRandomPresenter.AnchorResolver = globalTabletopPresentation.ResolveRandomAnchor;
            tabletopCardPresenter.AnchorResolver = globalTabletopPresentation.ResolveRandomAnchor;
            if (preAwakePendingSetup != null)
            {
                campaignFlow.EncounterHandoff.SetPendingSetup(preAwakePendingSetup);
                preAwakePendingSetup = null;
            }
            if (preAwakeEventInput != null)
            {
                campaignFlow.SetPlayableEventInput(preAwakeEventInput);
                preAwakeEventInput = null;
            }
            if (preAwakeHuntDepartureInput != null)
            {
                campaignFlow.SetPlayableHuntDepartureInput(preAwakeHuntDepartureInput);
                preAwakeHuntDepartureInput = null;
            }
            campaignFlow.ConfigurePersistentEffectProjection(registry => new HuntNoiseLeaseProjection(registry));
            campaignFlow.ConfigureGameplay(tabletopInteractionRouter);
            campaignFlow.ConfigureSettlement();
            campaignFlow.ConfigureSettlementPresentation();
            campaignFlow.ConfigureHunt();
            campaignUnityBridge = new CampaignUnityBridge(campaignFlow, campaignAccess, globalTabletopPresentation, message => Debug.Log($"[GameManager] {message}"));
            if (GetComponent<BackgroundDeselectionInput3D>() == null) gameObject.AddComponent<BackgroundDeselectionInput3D>();

        }

        private void Start()
        {
            if (campaignFlow?.WaitForEntrySelection != true)
            {
                if (!campaignFlow.TryStart(devMode ? devStartPhase : GamePhase.Settlement, true, out string startupReason))
                    Debug.LogError($"[GameManager] 战役启动失败：{startupReason}");
            }
            else
                Debug.Log("[GameManager] 正式开场菜单等待玩家选择，已延迟创建战役运行态。");

            // 开发者面板（挂在 Shared UI 节点上，F1 切换显隐）
            if (devMode)
                EnsureDevPanel();

            globalTabletopPresentation?.EnsureGameOverView();
        }

        private void Update() => campaignFlow?.Update();

        // ─── 场景根物体自动创建 ──────────────────────────────────────

        /// <summary>
        /// 若 Inspector 未拖入根物体引用，则在场景中自动创建。
        /// 确保三个阶段根物体和 Canvas UI 节点始终存在。
        /// </summary>
        private void EnsureRootObjects()
        {
            if (settlementRoot == null)
            {
                settlementRoot = new GameObject("SettlementRoot");
                settlementRoot.transform.SetParent(transform, false);
                Debug.Log("[GameManager] 自动创建 SettlementRoot");
            }
            if (huntRoot == null)
            {
                huntRoot = new GameObject("HuntRoot");
                huntRoot.transform.SetParent(transform, false);
                Debug.Log("[GameManager] 自动创建 HuntRoot");
            }
            if (bossFightRoot == null)
            {
                bossFightRoot = new GameObject("BossFightRoot");
                bossFightRoot.transform.SetParent(transform, false);
                Debug.Log("[GameManager] 自动创建 BossFightRoot");
            }

            // UI 节点：在 Canvas 下查找或创建
            EnsureUIRoot(ref uiSettlement, "Settlement");
            EnsureUIRoot(ref uiHunt,       "Hunt");
            EnsureUIRoot(ref uiBossFight,  "BossFight");
            EnsureUIRoot(ref uiShared,     "Shared");
        }

        private void EnsureUIRoot(ref GameObject uiNode, string nodeName)
        {
            if (uiNode != null) return;
            // 查找场景中名为 nodeName 的 Canvas 子节点
#if UNITY_2023_1_OR_NEWER
            var canvas = FindAnyObjectByType<UnityEngine.Canvas>();
#else
            var canvas = FindObjectOfType<UnityEngine.Canvas>();
#endif
            if (canvas != null)
            {
                var t = canvas.transform.Find(nodeName);
                if (t != null)
                {
                    uiNode = t.gameObject;
                    return;
                }
                // 不存在则创建
                var go = new GameObject(nodeName);
                go.transform.SetParent(canvas.transform, false);
                var rt = go.AddComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = rt.offsetMax = Vector2.zero;
                uiNode = go;
            }
        }

        public Vector3 ResolveTabletopEventAnchor(HunterInstance actor) => globalTabletopPresentation?.ResolveEventAnchor(actor) ?? transform.position;

        // ─── 各子系统初始化 ──────────────────────────────────────────

        /// <summary>解析本场战斗装配：优先用注入的载荷，否则用序列化配置自行组装。</summary>
        private BattleSetup ResolveSetup()
        {
            if (campaignFlow?.PendingBattleSetup != null) return campaignFlow.PendingBattleSetup;

            return new BattleSetup
            {
                FieldRules  = fieldRules,
                HunterSquad = characterConfigs,
                Boss        = bossConfig
            };
        }

        private bool TryCreateCombatConfiguration(out PlayableCombatSessionConfiguration configuration, out string reason)
        {
            configuration = null;
            reason = string.Empty;
            try
            {
                Transform parent = bossFightRoot != null ? bossFightRoot.transform : transform;
                EnsureEntityCreator();
                configuration = new PlayableCombatSessionConfiguration
                {
                    Setup = ResolveSetup(),
                    Parent = parent,
                    ArenaRadius = arenaRadius,
                    CellSize = cellSize,
                    TileHeight = tileHeight,
                    TileScale = tileScale,
                    TileIdleColor = tileIdleColor,
                    TileHighlightColor = tileHighlight,
                    TileOccupiedColor = tileOccupied,
                    CharacterHeight = characterHeight,
                    CharacterRadius = characterRadius,
                    BossHeight = bossHeight,
                    BossRadius = bossRadius,
                    CharacterColor = characterColor,
                    BossColor = bossColor,
                    TableHeightOffset = tableHeightOffset,
                    TableScale = tableScale,
                    BossTablePosition = bossTablePosition,
                    GetSettlementEvents = () => campaignFlow?.SettlementEvents,
                    ActionEnvironmentInstallers = CampaignDiagnostics?.ActionEnvironmentInstallers
                };
                return true;
            }
            catch (System.Exception exception)
            {
                reason = $"决战运行态初始化异常：{exception.Message}";
                Debug.LogException(exception, this);
                return false;
            }
        }

        /// <summary>确保存在一个 EntityCreator 实例（静态工厂 EntityCreator.Instance 才能用 Prefab）。
        /// 已有实例（场景中 Inspector 配好的）则直接用；否则挂一个走程序化回退。</summary>
        private void EnsureEntityCreator()
        {
            if (UI.EntityCreator.Instance != null) return;
            if (entityCreator == null)
                entityCreator = GetComponent<UI.EntityCreator>() ?? gameObject.AddComponent<UI.EntityCreator>();
        }


        /// <summary>获取当前游戏大阶段</summary>
        public GamePhase CurrentGamePhase => CampaignReadModel?.CurrentPhase ?? GamePhase.Settlement;
        public SettlementInstance SettlementData => CampaignReadModel?.Settlement;
        public IReadOnlyList<CraftRecipe> SettlementRecipes => CampaignReadModel?.SettlementRecipes ?? System.Array.Empty<CraftRecipe>();
        public IReadOnlyList<HunterInstance> ActiveHuntHunters => CampaignReadModel?.ActiveHuntHunters ?? System.Array.Empty<HunterInstance>();
        internal IPlayableHuntRuntime ActiveHuntRuntime => CampaignDiagnostics?.ActiveHuntRuntime;
        internal bool IsHuntActionSessionActive => CampaignDiagnostics?.IsHuntActionSessionActive == true;
        public bool IsHuntActionSessionRunning => CampaignReadModel?.IsHuntActionRunning == true;
        public bool IsHuntReturnInFlight => CampaignReadModel?.IsHuntReturnInFlight == true;
        internal bool IsCampaignActionSessionActive => CampaignDiagnostics?.IsCampaignActionSessionActive == true;
        public bool IsCampaignRuntimeActive => CampaignReadModel?.IsCampaignActive == true;
        public bool IsSettlementActionSessionRunning => CampaignReadModel?.IsSettlementActionRunning == true;
        public bool IsSettlementEventRestoreReady => CampaignReadModel?.IsSettlementEventRestoreReady == true;
        internal IActionEnvironmentInstallerRegistry ActionEnvironmentInstallers => CampaignDiagnostics?.ActionEnvironmentInstallers;
        internal CardGame.ActionQueue.ReactorRegistry SettlementActionReactors => CampaignDiagnostics?.SettlementReactors;
        internal CardGame.ActionQueue.ReactorRegistry CampaignActionReactors => CampaignDiagnostics?.CampaignReactors;
        internal CardGame.ActionQueue.ReactorRegistry HuntActionReactors => CampaignDiagnostics?.HuntReactors;
        internal IHuntExplorationPort ActiveHuntExplorationPort => CampaignCommands?.HuntExploration;
        public event System.Action<bool> SettlementProgressLoadCompleted;

        public void SetPlayableEventInput(IPlayableEventInput input)
        {
            if (campaignFlow != null)
            {
                campaignFlow.SetPlayableEventInput(input);
                return;
            }
            preAwakeEventInput = input;
        }

        public void ClearPlayableEventInput(IPlayableEventInput input)
        {
            if (campaignFlow != null)
            {
                campaignFlow.ClearPlayableEventInput(input);
                return;
            }
            if (ReferenceEquals(preAwakeEventInput, input)) preAwakeEventInput = null;
        }

        public void SetPlayableHuntDepartureInput(IPlayableHuntDepartureInput input)
        {
            if (campaignFlow != null)
            {
                campaignFlow.SetPlayableHuntDepartureInput(input);
                return;
            }
            preAwakeHuntDepartureInput = input;
        }

        public void ClearPlayableHuntDepartureInput(IPlayableHuntDepartureInput input)
        {
            if (campaignFlow != null)
            {
                campaignFlow.ClearPlayableHuntDepartureInput(input);
                return;
            }
            if (ReferenceEquals(preAwakeHuntDepartureInput, input)) preAwakeHuntDepartureInput = null;
        }

        public void RequestHuntDeparture(IReadOnlyList<int> hunterIds)
        {
            campaignFlow?.RequestHuntDeparture(hunterIds);
        }

        public bool CanRequestHuntDeparture(out string reason)
        {
            if (campaignFlow != null) return campaignFlow.CanRequestHuntDeparture(out reason);
            reason = "出猎事务尚未初始化。";
            return false;
        }

        public UniTask<SettlementDepartureCommandResult> DepartForHuntAsync(IReadOnlyList<int> hunterIds) => DepartForHuntAsync(hunterIds, null);

        public UniTask<SettlementDepartureCommandResult> DepartForHuntAsync(IReadOnlyList<int> hunterIds, PlayableHuntDestination destination) => CampaignCommands != null ? CampaignCommands.DepartForHuntAsync(hunterIds, destination) : UniTask.FromResult(SettlementDepartureCommandResult.Failed("出猎事务尚未初始化。"));

        public bool TryDepartForHunt(IReadOnlyList<int> hunterIds)
        {
            return campaignFlow?.TryDepartForHunt(hunterIds) == true;
        }


        public void SaveSettlementProgress()
        {
            if (IsCampaignRuntimeActive) CampaignCommands.SaveAsync(CurrentGamePhase == GamePhase.Hunt, this.GetCancellationTokenOnDestroy()).Forget();
        }

        public bool CanTrainWeapon(int hunterId, string masteryId, out string reason)
        {
            if (!IsCampaignRuntimeActive)
            {
                reason = "战役入口尚未完成。";
                return false;
            }
            if (campaignFlow == null)
            {
                reason = "仅可在营地阶段训练";
                return false;
            }
            return SettlementGameplay.CanTrainWeapon(hunterId, masteryId, out reason);
        }

        public UniTask<WeaponTrainingCommandResult> TrainWeaponAsync(int hunterId, string masteryId)
        {
            if (!IsCampaignRuntimeActive)
                return UniTask.FromResult(WeaponTrainingCommandResult.Failed("战役入口尚未完成。"));
            return SettlementGameplay.TrainWeaponAsync(hunterId, masteryId);
        }

        public bool CanCraft(CraftRecipe recipe, out string reason)
        {
            if (!IsCampaignRuntimeActive)
            {
                reason = "战役入口尚未完成。";
                return false;
            }
            if (campaignFlow == null)
            {
                reason = "仅可在营地阶段制作。";
                return false;
            }
            return SettlementGameplay.CanCraft(recipe, out reason);
        }

        public UniTask<SettlementCraftCommandResult> CraftAsync(CraftRecipe recipe)
        {
            if (!IsCampaignRuntimeActive)
                return UniTask.FromResult(SettlementCraftCommandResult.Failed("战役入口尚未完成。"));
            return SettlementGameplay.CraftAsync(recipe);
        }

        public UniTask<SettlementEquipmentCommandResult> EquipItemAsync(int hunterId, ItemData item)
        {
            if (!IsCampaignRuntimeActive)
                return UniTask.FromResult(SettlementEquipmentCommandResult.Failed("当前不在营地阶段。"));
            return SettlementGameplay.EquipItemAsync(hunterId, item);
        }

        public UniTask<SettlementEquipmentCommandResult> UnequipItemAsync(int hunterId, int equipmentInstanceId)
        {
            if (!IsCampaignRuntimeActive)
                return UniTask.FromResult(SettlementEquipmentCommandResult.Failed("当前不在营地阶段。"));
            return SettlementGameplay.UnequipItemAsync(hunterId, equipmentInstanceId);
        }

        public bool CanRecruitHunter(out string reason)
        {
            if (!IsCampaignRuntimeActive)
            {
                reason = "战役入口尚未完成。";
                return false;
            }
            if (campaignFlow == null)
            {
                reason = "仅可在营地阶段招募。";
                return false;
            }
            return SettlementGameplay.CanRecruitHunter(out reason);
        }

        public UniTask<RecruitHunterCommandResult> RecruitHunterAsync(HunterData template, string requestedName)
        {
            if (!IsCampaignRuntimeActive)
                return UniTask.FromResult(RecruitHunterCommandResult.Failed("战役入口尚未完成。"));
            return SettlementGameplay.RecruitHunterAsync(template, requestedName);
        }

        public bool HasRecoverableHunter() => IsCampaignRuntimeActive && SettlementGameplay?.HasRecoverableHunter() == true;

        public bool CanRecoverHunter(int hunterId, HunterBodyPart bodyPart, out string reason)
        {
            if (!IsCampaignRuntimeActive)
            {
                reason = "战役入口尚未完成。";
                return false;
            }
            if (campaignFlow == null)
            {
                reason = "仅可在营地阶段休养。";
                return false;
            }
            return SettlementGameplay.CanRecoverHunter(hunterId, bodyPart, out reason);
        }

        public UniTask<RecoverHunterCommandResult> RecoverHunterAsync(int hunterId, HunterBodyPart bodyPart)
        {
            if (!IsCampaignRuntimeActive)
                return UniTask.FromResult(RecoverHunterCommandResult.Failed("战役入口尚未完成。"));
            return SettlementGameplay.RecoverHunterAsync(hunterId, bodyPart);
        }

        public UniTask<HunterGrowthCommandResult> SpendHunterGrowthAsync(int hunterId, HunterGrowthChoice choice)
        {
            if (!IsCampaignRuntimeActive)
                return UniTask.FromResult(HunterGrowthCommandResult.Failed("战役入口尚未完成。"));
            return SettlementGameplay.SpendHunterGrowthAsync(hunterId, choice);
        }

        public void RetreatFromHunt()
        {
            RequestRetreatAsync().Forget();
        }

        public UniTask<HuntRetreatCommandResult> RequestRetreatAsync()
            => RequestRetreatAsync(HuntRetreatDecision.None);

        public UniTask<HuntRetreatCommandResult> RequestRetreatAsync(HuntRetreatDecision decision)
            => CampaignCommands != null ? CampaignCommands.RetreatAsync(decision, this.GetCancellationTokenOnDestroy()) : UniTask.FromResult(HuntRetreatCommandResult.Failed("回营事务尚未初始化。"));

        /// <summary>
        /// 切换游戏大阶段。GameManager 负责 Enable/Disable 对应根物体，
        /// 并触发该阶段的初始化逻辑。
        /// </summary>
        public void TransitionToPhase(GamePhase newPhase)
            => campaignFlow?.TransitionToPhase(newPhase);

        public UniTask<CampaignPhaseTransitionResult> TransitionToPhaseAsync(GamePhase newPhase)
        {
            return campaignFlow != null ? campaignFlow.TransitionToPhaseAsync(newPhase, this.GetCancellationTokenOnDestroy()) : UniTask.FromResult(CampaignPhaseTransitionResult.Failed(CurrentGamePhase, "战役入口尚未完成。"));
        }

        public UniTask<CampaignPhaseTransitionResult> TransitionToPhaseAsync(CampaignPhaseTransitionRequest request)
        {
            return CampaignCommands != null ? CampaignCommands.TransitionAsync(request, this.GetCancellationTokenOnDestroy()) : UniTask.FromResult(CampaignPhaseTransitionResult.Failed(CurrentGamePhase, "战役入口尚未完成。"));
        }

        public UniTask<CampaignEncounterStartResult> BeginEncounterAsync(CampaignEncounterRequest request)
        {
            return CampaignCommands != null ? CampaignCommands.BeginEncounterAsync(request, this.GetCancellationTokenOnDestroy()) : UniTask.FromResult(CampaignEncounterStartResult.Failed(request.EncounterId, "遭遇交接事务尚未初始化。"));
        }

        public UniTask<CampaignRestartResult> RestartCampaignAsync()
        {
            return CampaignCommands != null ? CampaignCommands.RestartAsync(this.GetCancellationTokenOnDestroy()) : UniTask.FromResult(CampaignRestartResult.Failed("战役入口尚未完成。"));
        }

        private void ApplyPhaseRoots(GamePhase prev, GamePhase next)
        {
            if (settlementRoot != null) settlementRoot.SetActive(next == GamePhase.Settlement);
            if (huntRoot       != null) huntRoot.SetActive(next == GamePhase.Hunt);
            if (bossFightRoot  != null) bossFightRoot.SetActive(next == GamePhase.BossFight);

            if (uiSettlement != null) uiSettlement.SetActive(next == GamePhase.Settlement);
            if (uiHunt       != null) uiHunt.SetActive(next == GamePhase.Hunt);
            if (uiBossFight  != null) uiBossFight.SetActive(next == GamePhase.BossFight);

            // 相机阶段切换
            cameraManager?.SetPhase(next);

            Debug.Log($"[GameManager] ApplyPhaseRoots: {prev} → {next}");
        }

        private void DeactivatePhaseRoots()
        {
            if (settlementRoot != null) settlementRoot.SetActive(false);
            if (huntRoot != null) huntRoot.SetActive(false);
            if (bossFightRoot != null) bossFightRoot.SetActive(false);
            if (uiSettlement != null) uiSettlement.SetActive(false);
            if (uiHunt != null) uiHunt.SetActive(false);
            if (uiBossFight != null) uiBossFight.SetActive(false);
        }

        // ═══════════════════════════════════════════
        // 清理
        // ═══════════════════════════════════════════

        private void OnApplicationQuit()
            => campaignFlow?.FlushOnApplicationQuit();

        private void OnDestroy()
        {
            campaignUnityBridge?.Dispose();
            campaignUnityBridge = null;
            developerCommands = null;
            globalTabletopPresentation?.Dispose();
            globalTabletopPresentation = null;
            campaignAccess = null;
            campaignFlow?.Dispose();
            campaignFlow = null;
            if (Instance == this)
                Instance = null;
        }

        // ═══════════════════════════════════════════
        // Dev 辅助面板初始化
        // ═══════════════════════════════════════════

        private void EnsureDevPanel()
        {
            if (_devPanel != null) return;
            var parent = uiShared != null ? uiShared : gameObject;
            var go = new GameObject("DevModePanel");
            go.transform.SetParent(parent.transform, false);
            _devPanel = go.AddComponent<DevModePanel>();
            _devPanel.Init(developerCommands);
        }

    }
}
