using System.Collections.Generic;
using System.Threading;
using CardTactics.CombatSystem;
using Cysharp.Threading.Tasks;
using Config;
using GameplayBase;
using GameplayBase.Board;
using GameplayBase.Card.Effect;
using GameplayBase.CombatSystem;
using GameplayBase.Config;
using HuntingInDarkness.Data;
using HuntingInDarkness.Hunt;
using HuntingInDarkness.GameCore.Combat;
using HuntingInDarkness.GameCore.Cards;
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
using SO.Boss.ActionCard;
using SO.Boss.HitLocation;
using SO.Combat;
using TMPro;
using UI;
using UI.Hunt;
using UI.Settlement;
using UnityEngine;
using UnityEngine.UI;

using Cards3D;

namespace Core
{
    /// <summary>
    /// Unity 组合壳与兼容 facade。持久单例。
    /// 管理三个游戏大阶段（Settlement / Hunt / BossFight）的根物体开关，
    /// 以及场景表现与兼容入口；战役运行态由 CampaignFlowCoordinator 持有。
    /// </summary>
    public class GameManager : MonoBehaviour, IGameContext, ICombatProvider, ICombatInspirationReadModel, IPlayableActionCardCommandSink, ICombatRuntimeDataProvider
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
        [SerializeField] private SettlementUIManager _settlementUIManager; // 场景预建并连线（缺失则报错）
        [SerializeField] private SettlementTable3D _settlementTable3D;
        private DevModePanel         _devPanel;
        private TabletopGameOverView3D gameOverView;
        private BattleSetup preAwakePendingSetup;
        private IPlayableEventInput preAwakeEventInput;
        private IPlayableHuntDepartureInput preAwakeHuntDepartureInput;
        private bool hasAwakened;
        private ICampaignPersistencePort configuredCampaignPersistence;
        private bool configuredWaitForEntrySelection;
        [SerializeField] private PhysicalDiceTabletopPresenter tabletopRandomPresenter;
        [SerializeField] private TabletopCardInteractionPresenter tabletopCardPresenter;
        [SerializeField] private Vector3 tabletopDiceAnchorOffset = new(0f, 0f, -1.65f);
        private ITabletopRandomInteractionPresenter tabletopInteractionRouter;
        private ITabletopRandomInteractionPresenter configuredTabletopInteraction;
        private PlayableSettlementContentCatalog settlementContentCatalog;
        private PlayableWorkshopCatalog workshopContentCatalog;

        /// <summary>仅允许在 GameManager 未激活时替换战役持久化端口。</summary>
        public bool ConfigureCampaignPersistence(ICampaignPersistencePort persistence)
        {
            if (hasAwakened || persistence == null) return false;
            configuredCampaignPersistence = persistence;
            return true;
        }

        /// <summary>在正式开场菜单选择前延迟创建营地运行态；仅允许在 Awake 前配置。</summary>
        public bool ConfigurePlayableStartup(bool waitForEntrySelection)
        {
            if (hasAwakened) return false;
            configuredWaitForEntrySelection = waitForEntrySelection;
            return true;
        }

        public CampaignStartupState CampaignStartupState => campaignFlow?.StartupState ?? (configuredWaitForEntrySelection ? CampaignStartupState.AwaitingChoice : CampaignStartupState.Active);

        public UniTask<bool> HasCampaignSaveAsync(CancellationToken cancellationToken = default) => campaignFlow != null ? campaignFlow.HasSaveAsync(cancellationToken) : UniTask.FromResult(false);

        public UniTask<bool> DeleteCampaignSaveAsync(CancellationToken cancellationToken = default) => campaignFlow != null ? campaignFlow.DeleteSaveAsync(cancellationToken) : UniTask.FromResult(false);

        public UniTask<CampaignStartupResult> StartNewCampaignAsync(CancellationToken cancellationToken = default) => campaignFlow != null ? campaignFlow.StartNewAsync(cancellationToken) : UniTask.FromResult(CampaignStartupResult.Failed(CampaignStartupState.AwaitingChoice, "战役组合根尚未初始化。"));

        public UniTask<CampaignStartupResult> ContinueCampaignAsync(CancellationToken cancellationToken = default) => campaignFlow != null ? campaignFlow.ContinueAsync(cancellationToken) : UniTask.FromResult(CampaignStartupResult.Failed(CampaignStartupState.AwaitingChoice, "战役组合根尚未初始化。"));

        public bool ConfigureTabletopInteraction(ITabletopRandomInteractionPresenter presenter)
        {
            if (hasAwakened || presenter == null) return false;
            configuredTabletopInteraction = presenter;
            return true;
        }

        // ─── 运行时数据 ───────────────────────────────────────────────

        /// <summary>本场战斗的装配载荷（狩猎阶段注入；未注入时由序列化配置组装）</summary>
        // ─── ICombatProvider ───
        public CombatManager CombatManager => campaignFlow?.ShowdownGameplay?.CombatManager;

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
            tabletopRandomPresenter.AnchorResolver = ResolveTabletopRandomAnchor;
            tabletopCardPresenter.AnchorResolver = ResolveTabletopRandomAnchor;
            tabletopInteractionRouter = configuredTabletopInteraction ?? new TabletopRandomInteractionRouter(tabletopRandomPresenter, tabletopCardPresenter);

            campaignFlow = new CampaignFlowCoordinator(new CampaignFlowBindings
            {
                ApplyPhaseRoots = ApplyPhaseRoots,
                DeactivatePhaseRoots = DeactivatePhaseRoots,
                TryCreateCombatConfiguration = TryCreateCombatConfiguration,
                ResolveLifetimeToken = this.GetCancellationTokenOnDestroy,
                PresentDepartureBlockedNotice = PresentHuntDepartureBlocked,
                ClearDepartureBlockedNotice = ClearHuntDepartureBlocked,
                ResetSettlementNotices = () => GetComponent<SettlementNoticePresenter3D>()?.ResetForCampaignChange(),
                SettlementLoadCompleted = succeeded => SettlementProgressLoadCompleted?.Invoke(succeeded),
                Info = message => Debug.Log($"[GameManager] {message}"),
                Error = message => Debug.LogError($"[GameManager] {message}"),
                SettlementTable = _settlementTable3D,
                SettlementRoot = settlementRoot,
                HuntRoot = huntRoot,
                UiHunt = uiHunt,
                SettlementUI = _settlementUIManager,
                WorkshopCatalog = workshopContentCatalog,
                SettlementContentCatalog = settlementContentCatalog,
                TabletopInteraction = tabletopInteractionRouter,
                Warning = message => Debug.LogWarning($"[GameManager] {message}")
            }, configuredCampaignPersistence ?? new SaveLoadSystemCampaignPersistenceAdapter(), configuredWaitForEntrySelection);
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

            // 全局事件订阅
            EventBus.Subscribe<BossDefeatedEvent>(OnBossDefeated);
            EventBus.Subscribe<GameOverEvent>(OnGameOver);
            EventBus.Subscribe<HunterRosterChangedEvent>(OnHunterRosterChanged);
            EventBus.Subscribe<CardHoverPreviewEvent>(OnCardHoverPreview);
            EventBus.Subscribe<CardHoverPreviewEndEvent>(OnCardHoverPreviewEnd);
            EventBus.Subscribe<SettlementTransactionCommittedEvent>(OnSettlementTransactionCommitted);
            EventBus.Subscribe<CampaignEncounterRequestedEvent>(OnCampaignEncounterRequested);
            EventBus.Subscribe<PlayableEventEncounterRequestedEvent>(OnPlayableEventEncounterRequested);

            (GetComponent<SettlementNoticePresenter3D>() ?? gameObject.AddComponent<SettlementNoticePresenter3D>()).Initialize(this);
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

            EnsureGameOverView();
        }

        private void Update()
        {
            campaignFlow?.Update();
            HandleBackgroundClick();
        }

        private void HandleBackgroundClick()
        {
            if (!Input.GetMouseButtonDown(0)) return;
            if (Camera.main == null) return;

            var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out var hit))
            {
                // 点击了实体棋子或卡牌 → 不触发取消选中
                if (hit.collider.GetComponent<EntityClickHandler>() != null) return;
                if (hit.collider.GetComponentInParent<CardView3D>() != null) return;
            }

            // 点击空白处或棋盘地面 → 取消选中
            EventBus.Publish(new CharacterDeselectedEvent());
        }

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

        private Vector3 ResolveTabletopRandomAnchor(TabletopRandomInteractionRequest request)
        {
            int hunterId = int.TryParse(request.ActorId, out int parsedHunterId) ? parsedHunterId : 0;
            return ResolveTabletopAnchor(hunterId) + tabletopDiceAnchorOffset;
        }

        public Vector3 ResolveTabletopEventAnchor(HunterInstance actor) => ResolveTabletopAnchor(actor?.InstanceId ?? 0);

        private Vector3 ResolveTabletopAnchor(int hunterId)
        {
            if (hunterId > 0 && settlementRoot != null)
                foreach (HunterCard3D card in settlementRoot.GetComponentsInChildren<HunterCard3D>(true))
                    if (card != null && card.gameObject.activeInHierarchy && card.Hunter != null && card.Hunter.InstanceId == hunterId)
                        return card.transform.position;
            if (hunterId > 0 && huntRoot != null)
                foreach (HuntStatusBoard3D board in huntRoot.GetComponentsInChildren<HuntStatusBoard3D>(true))
                    if (board != null && board.gameObject.activeInHierarchy && board.TryGetHunterAnchor(hunterId, out Vector3 anchor))
                        return anchor;
            if (CurrentGamePhase == GamePhase.Hunt && campaignFlow?.HuntTabletopInteractionAnchor != null)
                return campaignFlow.HuntTabletopInteractionAnchor.position;
            GameObject phaseRoot = CurrentGamePhase == GamePhase.Hunt ? huntRoot : settlementRoot;
            return phaseRoot != null ? phaseRoot.transform.position : transform.position;
        }

        // ─── 各子系统初始化 ──────────────────────────────────────────

        /// <summary>
        /// 由狩猎阶段在进入 Boss 决战前注入下一场战斗的装配载荷。
        /// </summary>
        public void InjectBattleSetup(BattleSetup setup)
        {
            if (campaignFlow == null)
            {
                preAwakePendingSetup = setup;
                return;
            }
            campaignFlow.SetPendingBattleSetup(setup);
        }

        /// <summary>正式运行配置入口。必须在 inactive GameObject 激活、触发 Awake 之前调用。</summary>
        public void ConfigurePlayableRuntime(BattleSetup setup, float runtimeCellSize, UI.EntityCreator runtimeEntityCreator = null, TMP_FontAsset runtimeChineseFontAsset = null, TextAsset runtimeChineseCharacterSet = null)
        {
            if (hasAwakened || gameObject.activeInHierarchy)
                throw new System.InvalidOperationException("Playable runtime configuration must be applied before GameManager is activated.");

            InjectBattleSetup(setup);
            devMode = false;
            devStartPhase = GamePhase.Settlement;
            cellSize = Mathf.Max(0.01f, runtimeCellSize);
            entityCreator = runtimeEntityCreator;
            chineseFontAsset = runtimeChineseFontAsset;
            chineseCharacterSet = runtimeChineseCharacterSet;
        }

        /// <summary>显式授予独立测试场景非营地直启能力。</summary>
        public void ConfigureDevelopmentStart(GamePhase startPhase)
        {
            if (hasAwakened || gameObject.activeInHierarchy)
                throw new System.InvalidOperationException("Development start configuration must be applied before GameManager is activated.");

            devMode = true;
            devStartPhase = startPhase;
        }

        /// <summary>独立测试场景的兼容配置入口。</summary>
        public void ConfigureForStandaloneTest(BattleSetup setup, GamePhase startPhase, float testCellSize, UI.EntityCreator testEntityCreator = null, TMP_FontAsset testChineseFontAsset = null, TextAsset testChineseCharacterSet = null)
        {
            ConfigurePlayableRuntime(setup, testCellSize, testEntityCreator, testChineseFontAsset, testChineseCharacterSet);
            ConfigureDevelopmentStart(startPhase);
        }

        public void ConfigureSettlementContent(PlayableSettlementContentCatalog catalog)
        {
            if (hasAwakened || gameObject.activeInHierarchy)
                throw new System.InvalidOperationException("Settlement content must be configured before GameManager is activated.");
            settlementContentCatalog = catalog;
        }

        public void ConfigureWorkshopContent(PlayableWorkshopCatalog catalog)
        {
            if (hasAwakened || gameObject.activeInHierarchy)
                throw new System.InvalidOperationException("Workshop content must be configured before GameManager is activated.");
            workshopContentCatalog = catalog;
        }

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
                    ActionEnvironmentInstallers = campaignFlow?.ActionEnvironmentInstallers
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


        // ═══════════════════════════════════════════
        // IGameContext 实现
        // ═══════════════════════════════════════════

        public TurnPhase CurrentPhase => campaignFlow?.ShowdownGameplay?.CurrentPhase ?? TurnPhase.PlayerTurn;
        public int CurrentTurnNumber => campaignFlow?.ShowdownGameplay?.CurrentTurnNumber ?? 0;
        public IReadOnlyList<ICharacterState> PlayerCharacters => campaignFlow?.ShowdownGameplay?.PlayerCharacters ?? System.Array.Empty<ICharacterState>();
        public IBossState Boss => campaignFlow?.ShowdownGameplay?.Boss;
        public IReadOnlyList<HitLocationRuntimeState> BossHitLocationStates => campaignFlow?.ShowdownGameplay?.BossHitLocationStates ?? System.Array.Empty<HitLocationRuntimeState>();
        public IReadOnlyList<BossActionCardData> BossRevealedCards => campaignFlow?.ShowdownGameplay?.BossRevealedCards ?? System.Array.Empty<BossActionCardData>();
        public Character GetCharacter(int characterId) => campaignFlow?.ShowdownGameplay?.GetCharacter(characterId);
        public CharacterRuntimeData GetCharacterData(int characterId) => campaignFlow?.ShowdownGameplay?.GetCharacterData(characterId);
        public IReadOnlyList<ICharacterActionCardInstanceState> GetCardsOf(int characterId) => campaignFlow?.ShowdownGameplay?.GetCardsOf(characterId) ?? System.Array.Empty<ICharacterActionCardInstanceState>();
        public ICharacterActionCardInstanceState GetCard(int cardInstanceId) => campaignFlow?.ShowdownGameplay?.GetCard(cardInstanceId);
        public Vector3 GetEntityWorldPosition(int entityId) => campaignFlow?.ShowdownGameplay?.GetEntityWorldPosition(entityId) ?? Vector3.zero;

        // ═══════════════════════════════════════════
        // UI 输入接口
        // ═══════════════════════════════════════════

        public void OnSelectCharacter(int characterId) => campaignFlow?.ShowdownGameplay?.SelectCharacter(characterId);
        public void OnPlayCard(int cardInstanceId, int targetEntityId) => campaignFlow?.ShowdownGameplay?.PlayCard(cardInstanceId, targetEntityId);
        public void OnRestoreCard(int cardInstanceId) => campaignFlow?.ShowdownGameplay?.RestoreCard(cardInstanceId);
        public void OnDiscardCard(int cardInstanceId) => campaignFlow?.ShowdownGameplay?.DiscardCard(cardInstanceId);
        public void OnEndTurn() => campaignFlow?.ShowdownGameplay?.EndTurn();
        public bool OnAssistOvertimeCharacter(int helperId, int targetId) => campaignFlow?.ShowdownGameplay?.AssistOvertimeCharacter(helperId, targetId) == true;
        public int AddCombatInspiration(int characterId, int amount) => campaignFlow?.ShowdownGameplay?.AddInspiration(characterId, amount) ?? 0;
        public UniTask<InspirationGain> AddCombatInspirationAsync(int characterId, CombatInspirationColor color, System.Threading.CancellationToken cancellationToken = default) => campaignFlow?.ShowdownGameplay != null ? campaignFlow.ShowdownGameplay.AddInspirationAsync(characterId, color, cancellationToken) : UniTask.FromResult(new InspirationGain(InspirationGainResult.Rejected, default));
        public IReadOnlyList<CombatInspirationToken> GetCombatInspirationTokens(int characterId) => campaignFlow?.ShowdownGameplay?.GetInspirationTokens(characterId) ?? System.Array.Empty<CombatInspirationToken>();
        public int GetCombatInspirationCapacity(int characterId) => campaignFlow?.ShowdownGameplay?.GetInspirationCapacity(characterId) ?? 0;

        // ═══════════════════════════════════════════
        // Boss 战利品结算
        // ═══════════════════════════════════════════

        /// <summary>
        /// 收集本场 Boss 战所有累积战利品（部位命中/摧毁掉落 + Boss 击败掉落），
        /// 写入营地存储并追加到 HuntRecord。
        /// 在离开 BossFight 阶段时由 TransitionToPhase 调用。
        /// </summary>
        // ═══════════════════════════════════════════
        // 阶段管理 (Phase Management)
        // ═══════════════════════════════════════════

        /// <summary>获取当前游戏大阶段</summary>
        public GamePhase CurrentGamePhase => campaignFlow?.CurrentPhase ?? GamePhase.Settlement;
        public SettlementInstance SettlementData => campaignFlow?.SettlementData;
        public IReadOnlyList<CraftRecipe> SettlementRecipes => campaignFlow?.SettlementRecipes ?? System.Array.Empty<CraftRecipe>();
        public IReadOnlyList<HunterInstance> ActiveHuntHunters => campaignFlow?.ActiveHuntHunters ?? System.Array.Empty<HunterInstance>();
        public IPlayableHuntRuntime ActiveHuntRuntime => campaignFlow?.ActiveHuntRuntime;
        public bool IsHuntActionSessionActive => campaignFlow?.IsHuntActionSessionActive == true;
        public bool IsHuntActionSessionRunning => campaignFlow?.IsHuntActionSessionRunning == true;
        public bool IsHuntReturnInFlight => campaignFlow?.IsHuntReturnRecoveryInFlight == true;
        public bool IsCampaignActionSessionActive => campaignFlow?.IsCampaignActionSessionActive == true;
        public bool IsCampaignRuntimeActive => campaignFlow?.CampaignStarted == true;
        public bool IsSettlementActionSessionRunning => campaignFlow?.IsSettlementActionSessionRunning == true;
        public bool IsSettlementEventRestoreReady => campaignFlow?.IsSettlementEventRestoreReady == true;
        public IActionEnvironmentInstallerRegistry ActionEnvironmentInstallers => campaignFlow?.ActionEnvironmentInstallers;
        public CardGame.ActionQueue.ReactorRegistry SettlementActionReactors => campaignFlow?.SettlementActionReactors;
        public CardGame.ActionQueue.ReactorRegistry CampaignActionReactors => campaignFlow?.CampaignActionReactors;
        public CardGame.ActionQueue.ReactorRegistry HuntActionReactors => campaignFlow?.HuntActionReactors;
        public IHuntExplorationPort ActiveHuntExplorationPort => campaignFlow?.ActiveHuntExplorationPort;
        public event System.Action<EventData, HunterInstance> SettlementEventPresented;
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

        private void PresentHuntDepartureBlocked(string reason)
        {
            GetComponent<SettlementNoticePresenter3D>()?.PresentHuntDepartureBlocked(reason);
        }

        private void ClearHuntDepartureBlocked()
        {
            GetComponent<SettlementNoticePresenter3D>()?.ClearHuntDepartureBlocked();
        }

        public UniTask<SettlementDepartureCommandResult> DepartForHuntAsync(IReadOnlyList<int> hunterIds) => DepartForHuntAsync(hunterIds, null);

        public UniTask<SettlementDepartureCommandResult> DepartForHuntAsync(IReadOnlyList<int> hunterIds, PlayableHuntDestination destination) => campaignFlow != null ? campaignFlow.DepartForHuntAsyncGuarded(hunterIds, destination) : UniTask.FromResult(SettlementDepartureCommandResult.Failed("出猎事务尚未初始化。"));

        public bool TryDepartForHunt(IReadOnlyList<int> hunterIds)
        {
            return campaignFlow?.TryDepartForHunt(hunterIds) == true;
        }


        public void SaveSettlementProgress()
        {
            if (IsCampaignRuntimeActive)
                DevSave();
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
            return campaignFlow.SettlementGameplay.CanTrainWeapon(hunterId, masteryId, out reason);
        }

        public UniTask<WeaponTrainingCommandResult> TrainWeaponAsync(int hunterId, string masteryId)
        {
            if (!IsCampaignRuntimeActive)
                return UniTask.FromResult(WeaponTrainingCommandResult.Failed("战役入口尚未完成。"));
            return campaignFlow.SettlementGameplay.TrainWeaponAsync(hunterId, masteryId);
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
            return campaignFlow.SettlementGameplay.CanCraft(recipe, out reason);
        }

        public UniTask<SettlementCraftCommandResult> CraftAsync(CraftRecipe recipe)
        {
            if (!IsCampaignRuntimeActive)
                return UniTask.FromResult(SettlementCraftCommandResult.Failed("战役入口尚未完成。"));
            return campaignFlow.SettlementGameplay.CraftAsync(recipe);
        }

        public UniTask<SettlementEquipmentCommandResult> EquipItemAsync(int hunterId, ItemData item)
        {
            if (!IsCampaignRuntimeActive)
                return UniTask.FromResult(SettlementEquipmentCommandResult.Failed("当前不在营地阶段。"));
            return campaignFlow.SettlementGameplay.EquipItemAsync(hunterId, item);
        }

        public UniTask<SettlementEquipmentCommandResult> UnequipItemAsync(int hunterId, int equipmentInstanceId)
        {
            if (!IsCampaignRuntimeActive)
                return UniTask.FromResult(SettlementEquipmentCommandResult.Failed("当前不在营地阶段。"));
            return campaignFlow.SettlementGameplay.UnequipItemAsync(hunterId, equipmentInstanceId);
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
            return campaignFlow.SettlementGameplay.CanRecruitHunter(out reason);
        }

        public UniTask<RecruitHunterCommandResult> RecruitHunterAsync(HunterData template, string requestedName)
        {
            if (!IsCampaignRuntimeActive)
                return UniTask.FromResult(RecruitHunterCommandResult.Failed("战役入口尚未完成。"));
            return campaignFlow.SettlementGameplay.RecruitHunterAsync(template, requestedName);
        }

        public bool HasRecoverableHunter() => IsCampaignRuntimeActive && campaignFlow?.SettlementGameplay?.HasRecoverableHunter() == true;

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
            return campaignFlow.SettlementGameplay.CanRecoverHunter(hunterId, bodyPart, out reason);
        }

        public UniTask<RecoverHunterCommandResult> RecoverHunterAsync(int hunterId, HunterBodyPart bodyPart)
        {
            if (!IsCampaignRuntimeActive)
                return UniTask.FromResult(RecoverHunterCommandResult.Failed("战役入口尚未完成。"));
            return campaignFlow.SettlementGameplay.RecoverHunterAsync(hunterId, bodyPart);
        }

        public UniTask<HunterGrowthCommandResult> SpendHunterGrowthAsync(int hunterId, HunterGrowthChoice choice)
        {
            if (!IsCampaignRuntimeActive)
                return UniTask.FromResult(HunterGrowthCommandResult.Failed("战役入口尚未完成。"));
            return campaignFlow.SettlementGameplay.SpendHunterGrowthAsync(hunterId, choice);
        }

        public bool OnRelieveOvertimeCharacter(int targetId)
        {
            return campaignFlow?.ShowdownGameplay?.RelieveOvertimeCharacter(targetId) == true;
        }

        public TimelineActionStatus GetTimelineStatus(int characterId) => campaignFlow?.ShowdownGameplay?.GetTimelineStatus(characterId) ?? TimelineActionStatus.Done;

        public void LoadSettlementProgress() => DevLoad();

        public void RetreatFromHunt()
        {
            RequestRetreatAsync().Forget();
        }

        public UniTask<HuntRetreatCommandResult> RequestRetreatAsync()
            => RequestRetreatAsync(HuntRetreatDecision.None);

        public UniTask<HuntRetreatCommandResult> RequestRetreatAsync(HuntRetreatDecision decision)
            => campaignFlow != null ? campaignFlow.RequestRetreatAsync(decision, this.GetCancellationTokenOnDestroy()) : UniTask.FromResult(HuntRetreatCommandResult.Failed("回营事务尚未初始化。"));

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
            return campaignFlow != null ? campaignFlow.TransitionToPhaseAsync(request, this.GetCancellationTokenOnDestroy()) : UniTask.FromResult(CampaignPhaseTransitionResult.Failed(CurrentGamePhase, "战役入口尚未完成。"));
        }

        public UniTask<CampaignEncounterStartResult> BeginEncounterAsync(CampaignEncounterRequest request)
        {
            return campaignFlow != null ? campaignFlow.BeginEncounterAsync(request, this.GetCancellationTokenOnDestroy()) : UniTask.FromResult(CampaignEncounterStartResult.Failed(request.EncounterId, "遭遇交接事务尚未初始化。"));
        }

        public UniTask<CampaignRestartResult> RestartCampaignAsync()
        {
            return campaignFlow != null ? campaignFlow.RestartCampaignAsync(this.GetCancellationTokenOnDestroy()) : UniTask.FromResult(CampaignRestartResult.Failed("战役入口尚未完成。"));
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
            EventBus.Unsubscribe<BossDefeatedEvent>(OnBossDefeated);
            EventBus.Unsubscribe<GameOverEvent>(OnGameOver);
            EventBus.Unsubscribe<HunterRosterChangedEvent>(OnHunterRosterChanged);
            EventBus.Unsubscribe<CardHoverPreviewEvent>(OnCardHoverPreview);
            EventBus.Unsubscribe<CardHoverPreviewEndEvent>(OnCardHoverPreviewEnd);
            EventBus.Unsubscribe<SettlementTransactionCommittedEvent>(OnSettlementTransactionCommitted);
            EventBus.Unsubscribe<CampaignEncounterRequestedEvent>(OnCampaignEncounterRequested);
            EventBus.Unsubscribe<PlayableEventEncounterRequestedEvent>(OnPlayableEventEncounterRequested);
            campaignFlow?.Dispose();
            campaignFlow = null;
            if (Instance == this)
                Instance = null;
        }

        // ═══════════════════════════════════════════
        // 事件处理器（全局）
        // ═══════════════════════════════════════════

        /// <summary>Boss被击败 → 结算狩猎 → 返回营地</summary>
        private void OnBossDefeated(BossDefeatedEvent _)
        {
            if (CurrentGamePhase != GamePhase.BossFight) return;
            Debug.Log("[GameManager] 收到 BossDefeatedEvent → 狩猎结算 → 营地");
            campaignFlow?.HandleBossDefeated();
        }

        /// <summary>游戏结束（全部猎人死亡）</summary>
        private void OnGameOver(GameOverEvent evt)
        {
            Debug.Log($"[GameManager] 游戏结束：{evt.Reason}");
            gameOverView?.Show(evt.Reason);
        }

        private void OnCampaignEncounterRequested(CampaignEncounterRequestedEvent evt) => BeginCampaignEncounterAsync(evt.Request).Forget();

        private void OnPlayableEventEncounterRequested(PlayableEventEncounterRequestedEvent evt)
            => campaignFlow?.HandlePlayableEventEncounterRequested(evt);

        private async UniTaskVoid BeginCampaignEncounterAsync(CampaignEncounterRequest request)
        {
            CampaignEncounterStartResult result = await BeginEncounterAsync(request);
            if (!result.Succeeded)
                Debug.LogWarning($"[GameManager] 无法开始遭遇 {request.EncounterId}：{result.Reason}");
        }

        private void OnSettlementTransactionCommitted(SettlementTransactionCommittedEvent evt)
            => campaignFlow?.HandleSettlementTransactionCommitted(evt);

        /// <summary>悬浮行动卡 → 高亮其目标/范围格</summary>
        private void OnCardHoverPreview(CardHoverPreviewEvent evt)
            => campaignFlow?.HighlightCardPreview(evt.CardInstanceId);

        /// <summary>移开行动卡 → 清除范围高亮</summary>
        private void OnCardHoverPreviewEnd(CardHoverPreviewEndEvent _)
            => campaignFlow?.ClearCardPreview();

        /// <summary>猎人名册变化时检查胜负条件</summary>
        private void OnHunterRosterChanged(HunterRosterChangedEvent _)
        {
            if (!IsCampaignRuntimeActive || SettlementData == null) return;
            var alive = SettlementData.GetAliveHunters();
            if (alive.Count == 0)
            {
                EventBus.Publish(new GameOverEvent
                    { Reason = "营地中所有猎人已经死亡。\n黑暗吞噬了这片聚落。" });
            }
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
            _devPanel.Init(this);
        }

        private void EnsureGameOverView()
        {
            if (gameOverView != null) return;
            var viewObject = new GameObject("TabletopGameOverView3D");
            viewObject.transform.SetParent(transform, false);
            gameOverView = viewObject.AddComponent<TabletopGameOverView3D>();
            gameOverView.RestartCommand = RestartCampaignAsync;
        }

        public void DevAddHunter(string name)
        {
            HunterInstance hunter = campaignFlow?.DevAddHunter(name);
            if (hunter == null)
            {
                Debug.LogWarning("[GameManager] DevAddHunter: SettlementManager 尚未初始化");
                return;
            }
            Debug.Log($"[GameManager][Dev] 招募猎人：{hunter.Name}");
        }

        /// <summary>快速添加资源（开发者）</summary>
        public void DevAddResource(string resourceName, int amount)
        {
            if (campaignFlow?.DevAddResource(resourceName, amount) != true)
            {
                Debug.LogWarning("[GameManager] DevAddResource: SettlementManager 尚未初始化");
                return;
            }
            Debug.Log($"[GameManager][Dev] 添加资源 {resourceName} ×{amount}");
        }

        /// <summary>开发工具不再绕过回营流程推进日历。</summary>
        public void DevAdvanceYear()
        {
            Debug.LogWarning("[GameManager] 日历只能由成功回营推进；开发者直接推进入口已禁用。");
        }

        public void DevSave()
        {
            if (SettlementData == null)
            {
                Debug.LogWarning("[GameManager] DevSave: 无数据可保存");
                return;
            }
            campaignFlow.SaveCampaignAsync(CurrentGamePhase == GamePhase.Hunt, this.GetCancellationTokenOnDestroy()).Forget();
        }

        /// <summary>手动读档（开发者）</summary>
        public void DevLoad()
        {
            campaignFlow?.LoadSnapshotFromPersistenceAsync();
        }

    }
}
