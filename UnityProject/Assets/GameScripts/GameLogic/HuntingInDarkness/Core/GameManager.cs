using System.Collections.Generic;
using System.Linq;
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
    /// 场景中唯一的 MonoBehaviour 核心。持久单例。
    /// 管理三个游戏大阶段（Settlement / Hunt / BossFight）的根物体开关，
    /// 以及 Boss决战子系统的初始化与运行。
    /// </summary>
    public class GameManager : MonoBehaviour, IGameContext, ICombatProvider, ICombatInspirationReadModel, IPlayableActionCardCommandSink, ICombatRuntimeDataProvider, ICampaignPhaseTransitionHost, IPlayableHuntRetreatInput, ISettlementDepartureRequestPort
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

        private PhaseManager         _phaseManager;
        private SettlementManager    _settlementManager;
        [SerializeField] private SettlementUIManager _settlementUIManager; // 场景预建并连线（缺失则报错）
        private bool _settlementUIInited;
        [SerializeField] private SettlementTable3D _settlementTable3D;
        private HuntManager          _huntMgr;
        private HuntMapVisualizer    _huntVisualizer;
        private HuntUIManager        _huntUI;
        private HuntRetreatPanel3D huntRetreatPanel;
        private DevModePanel         _devPanel;
        private TabletopGameOverView3D gameOverView;
        /// <summary>狩猎结算记录，由 HuntManager 回调注入，供 TransitionToPhase(Settlement) 消费</summary>
        private HuntRecord           _pendingHuntRecord;
        private PlayableCombatSession _combatSession;
        private PlayableSettlementActionSession settlementActionSession;
        private SettlementEventRestoreProjection settlementEventRestoreProjection;
        private PlayableHuntActionSession huntActionSession;
        private PlayableCampaignActionSession campaignActionSession;
        private readonly ActionEnvironmentInstallerRegistry actionEnvironmentInstallers = new();
        private IPlayableEventInput playableEventInput;
        private IPlayableHuntDepartureInput playableHuntDepartureInput;
        private bool huntDepartureInFlight;
        private bool huntRetreatInFlight;
        private bool encounterCheckpointRollbackFailed;
        private bool huntReturnRecoveryInFlight;
        private bool preparedHuntExit;
        private string activeExpeditionId;
        private string stableCampaignPayload;
        [SerializeField] private PhysicalDiceTabletopPresenter tabletopRandomPresenter;
        [SerializeField] private TabletopCardInteractionPresenter tabletopCardPresenter;
        [SerializeField] private Vector3 tabletopDiceAnchorOffset = new(0f, 0f, -1.65f);
        private ITabletopRandomInteractionPresenter tabletopInteractionRouter;
        private PlayableSettlementContentCatalog settlementContentCatalog;
        private PlayableWorkshopCatalog workshopContentCatalog;

        // ─── 运行时数据 ───────────────────────────────────────────────

        /// <summary>本场战斗的装配载荷（狩猎阶段注入；未注入时由序列化配置组装）</summary>
        private BattleSetup _pendingSetup;
        private IReadOnlyList<HunterInstance> pendingEncounterHunters;

        // ─── ICombatProvider ───
        public CombatManager CombatManager => _combatSession?.CombatManager;

        // ═══════════════════════════════════════════
        // 初始化
        // ═══════════════════════════════════════════

        private void Awake()
        {
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
            tabletopInteractionRouter = new TabletopRandomInteractionRouter(tabletopRandomPresenter, tabletopCardPresenter);

            // 阶段管理器
            _phaseManager = new PhaseManager(GameModule.Fsm);
            _phaseManager.OnPhaseTransition = ApplyPhaseRoots;

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
            // 设置初始阶段。PhaseManager 使用独立命名的 ZFramework FSM。
            var startPhase = devMode ? devStartPhase : GamePhase.Settlement;
            _settlementManager = CreateSettlementManager();
            actionEnvironmentInstallers.Register(new InventionActionEffectInstaller(() => _settlementManager?.Data, () => _settlementManager?.Inventions?.AllInventions));
            _phaseManager.Start(startPhase);
            campaignActionSession = new PlayableCampaignActionSession(this, actionEnvironmentInstallers);

            if (startPhase == GamePhase.Settlement)
            {
                _settlementManager.EnsureStartingConditions();
                StartSettlementActionSession();
                EnsureSettlementUI();
                QueueSettlementEvents(_settlementManager.OnEnterWorkItems());
            }
            else if (startPhase == GamePhase.Hunt)
            {
                _settlementManager.EnsureStartingConditions();
                if (TryEnterHuntPhase(null, true, out string huntStartReason))
                    PlayableCampaignLoopContract.ConsumeDepartureRoster(_settlementManager.Data);
                else
                {
                    Debug.LogError($"[GameManager] 开发者狩猎直启失败：{huntStartReason}");
                    _phaseManager.TransitionTo(GamePhase.Settlement);
                    StartSettlementActionSession();
                    EnsureSettlementUI();
                    QueueSettlementEvents(_settlementManager.OnEnterWorkItems());
                }
            }

            if (startPhase == GamePhase.BossFight)
                EnterBossFightPhase();

            // 开发者面板（挂在 Shared UI 节点上，F1 切换显隐）
            if (devMode)
                EnsureDevPanel();

            EnsureGameOverView();
        }

        private void Update()
        {
            _combatSession?.Update();
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
                Debug.Log("[GameManager] 自动创建 SettlementRoot");
            }
            if (huntRoot == null)
            {
                huntRoot = new GameObject("HuntRoot");
                Debug.Log("[GameManager] 自动创建 HuntRoot");
            }
            if (bossFightRoot == null)
            {
                bossFightRoot = new GameObject("BossFightRoot");
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
            if (CurrentGamePhase == GamePhase.Hunt && _huntVisualizer != null)
                return _huntVisualizer.TabletopInteractionAnchor.position;
            GameObject phaseRoot = CurrentGamePhase == GamePhase.Hunt ? huntRoot : settlementRoot;
            return phaseRoot != null ? phaseRoot.transform.position : transform.position;
        }

        // ─── 各子系统初始化 ──────────────────────────────────────────

        /// <summary>
        /// 由狩猎阶段在进入 Boss 决战前注入下一场战斗的装配载荷。
        /// </summary>
        public void InjectBattleSetup(BattleSetup setup) => _pendingSetup = setup;

        /// <summary>
        /// 独立测试场景的显式配置入口。必须在 inactive GameObject 激活、触发 Awake 之前调用。
        /// </summary>
        public void ConfigureForStandaloneTest(BattleSetup setup, GamePhase startPhase, float testCellSize, UI.EntityCreator testEntityCreator = null, TMP_FontAsset testChineseFontAsset = null, TextAsset testChineseCharacterSet = null)
        {
            if (gameObject.activeInHierarchy)
                throw new System.InvalidOperationException("Standalone test configuration must be applied before GameManager is activated.");

            _pendingSetup = setup;
            devMode = true;
            devStartPhase = startPhase;
            cellSize = Mathf.Max(0.01f, testCellSize);
            entityCreator = testEntityCreator;
            chineseFontAsset = testChineseFontAsset;
            chineseCharacterSet = testChineseCharacterSet;
        }

        public void ConfigureSettlementContent(PlayableSettlementContentCatalog catalog)
        {
            if (gameObject.activeInHierarchy)
                throw new System.InvalidOperationException("Settlement content must be configured before GameManager is activated.");
            settlementContentCatalog = catalog;
        }

        public void ConfigureWorkshopContent(PlayableWorkshopCatalog catalog)
        {
            if (gameObject.activeInHierarchy)
                throw new System.InvalidOperationException("Workshop content must be configured before GameManager is activated.");
            workshopContentCatalog = catalog;
        }

        /// <summary>解析本场战斗装配：优先用注入的载荷，否则用序列化配置自行组装。</summary>
        private BattleSetup ResolveSetup()
        {
            if (_pendingSetup != null) return _pendingSetup;

            return new BattleSetup
            {
                FieldRules  = fieldRules,
                HunterSquad = characterConfigs,
                Boss        = bossConfig
            };
        }

        private bool StartCombatSession()
        {
            if (_combatSession != null) return true;

            try
            {
                Transform parent = bossFightRoot != null ? bossFightRoot.transform : transform;
                EnsureEntityCreator();
                var configuration = new PlayableCombatSessionConfiguration
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
                    GetSettlementEvents = () => _settlementManager?.Events,
                    ActionEnvironmentInstallers = actionEnvironmentInstallers
                };
                _combatSession = new PlayableCombatSession(configuration);
                _combatSession.PublishReady();
                Debug.Log("[GameManager] CombatSession 已创建。");
                return true;
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception, this);
                DisposeCombatSession();
                return false;
            }
        }

        private void DisposeCombatSession()
        {
            PlayableCombatSession session = _combatSession;
            _combatSession = null;
            session?.Dispose();
            Debug.Log("[GameManager] CombatSession 已释放。");
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

        public TurnPhase CurrentPhase => _combatSession?.CurrentPhase ?? TurnPhase.PlayerTurn;
        public int CurrentTurnNumber => _combatSession?.CurrentTurnNumber ?? 0;
        public IReadOnlyList<ICharacterState> PlayerCharacters => _combatSession?.PlayerCharacters ?? System.Array.Empty<ICharacterState>();
        public IBossState Boss => _combatSession?.Boss;
        public IReadOnlyList<HitLocationRuntimeState> BossHitLocationStates => _combatSession?.BossHitLocationStates ?? System.Array.Empty<HitLocationRuntimeState>();
        public IReadOnlyList<BossActionCardData> BossRevealedCards => _combatSession?.BossRevealedCards ?? System.Array.Empty<BossActionCardData>();
        public Character GetCharacter(int characterId) => _combatSession?.GetCharacter(characterId);
        public CharacterRuntimeData GetCharacterData(int characterId) => _combatSession?.GetCharacterData(characterId);
        public IReadOnlyList<ICharacterActionCardInstanceState> GetCardsOf(int characterId) => _combatSession?.GetCardsOf(characterId) ?? System.Array.Empty<ICharacterActionCardInstanceState>();
        public ICharacterActionCardInstanceState GetCard(int cardInstanceId) => _combatSession?.GetCard(cardInstanceId);
        public Vector3 GetEntityWorldPosition(int entityId) => _combatSession?.GetEntityWorldPosition(entityId) ?? Vector3.zero;

        // ═══════════════════════════════════════════
        // UI 输入接口
        // ═══════════════════════════════════════════

        public void OnSelectCharacter(int characterId) => _combatSession?.OnSelectCharacter(characterId);
        public void OnPlayCard(int cardInstanceId, int targetEntityId) => _combatSession?.OnPlayCard(cardInstanceId, targetEntityId);
        public void OnRestoreCard(int cardInstanceId) => _combatSession?.OnRestoreCard(cardInstanceId);
        public void OnDiscardCard(int cardInstanceId) => _combatSession?.OnDiscardCard(cardInstanceId);
        public void OnEndTurn() => _combatSession?.OnEndTurn();
        public bool OnAssistOvertimeCharacter(int helperId, int targetId) => _combatSession != null && _combatSession.TryAssistOvertimeCharacter(helperId, targetId);
        public int AddCombatInspiration(int characterId, int amount) => _combatSession?.AddCombatInspiration(characterId, amount) ?? 0;
        public UniTask<InspirationGain> AddCombatInspirationAsync(int characterId, CombatInspirationColor color, System.Threading.CancellationToken cancellationToken = default) => _combatSession != null ? _combatSession.AddCombatInspirationAsync(characterId, color, cancellationToken) : UniTask.FromResult(new InspirationGain(InspirationGainResult.Rejected, default));
        public IReadOnlyList<CombatInspirationToken> GetCombatInspirationTokens(int characterId) => _combatSession?.GetCombatInspirationTokens(characterId) ?? System.Array.Empty<CombatInspirationToken>();
        public int GetCombatInspirationCapacity(int characterId) => _combatSession?.GetCombatInspirationCapacity(characterId) ?? 0;

        // ═══════════════════════════════════════════
        // Boss 战利品结算
        // ═══════════════════════════════════════════

        /// <summary>
        /// 收集本场 Boss 战所有累积战利品（部位命中/摧毁掉落 + Boss 击败掉落），
        /// 写入营地存储并追加到 HuntRecord。
        /// 在离开 BossFight 阶段时由 TransitionToPhase 调用。
        /// </summary>
        private void ApplyBossFightLoot()
        {
            if (_settlementManager == null || _combatSession == null) return;

            var loot = _combatSession.GetAndClearLoot();
            if (loot.Count == 0) return;

            foreach (var (resource, amount) in loot)
            {
                string resourceId = PlayableSettlementItemRegistry.ResolveContentId(resource);
                int oldAmount = _settlementManager.Data.GetResource(resourceId);
                _settlementManager.Data.AddResource(resourceId, amount);

                if (_pendingHuntRecord != null)
                    for (int i = 0; i < amount; i++)
                        _pendingHuntRecord.CollectedResources.Add(resourceId);

                EventBus.Publish(new ResourceChangedEvent
                {
                    ResourceName = resourceId,
                    OldAmount    = oldAmount,
                    NewAmount    = _settlementManager.Data.GetResource(resourceId)
                });
                Debug.Log($"[GameManager] Boss战掉落 → {PlayableSettlementItemRegistry.GetDisplayName(resourceId)} ×{amount}");
            }
        }

        // ═══════════════════════════════════════════
        // 营地阶段子系统
        // ═══════════════════════════════════════════

        private SettlementManager CreateSettlementManager()
        {
            var mgr = new SettlementManager();
            mgr.DepartureRequestPort = this;
            return mgr;
        }

        private void StartSettlementActionSession()
        {
            DisposeSettlementActionSession();
            if (_settlementManager?.Data == null) return;
            settlementActionSession = new PlayableSettlementActionSession(_settlementManager.Data, new PlayableWeaponTrainingContentAdapter(PlayableWeaponMasteryRuntime.Catalog), _settlementManager.Events, playableEventInput, new PlayableSettlementCareContentAdapter(settlementContentCatalog), new PlayableSettlementEquipmentContentAdapter(PlayableSettlementItemRegistry.Items), tabletopInteractionRouter, _settlementManager.Workshop, _settlementManager.Inventions, workshopContentCatalog, PlayableSymptomRuntime.Catalog, actionEnvironmentInstallers, _settlementManager.Timeline.ResolveEvent, _settlementManager.Timeline);
        }

        private void DisposeSettlementActionSession()
        {
            PlayableSettlementActionSession session = settlementActionSession;
            settlementActionSession = null;
            session?.Dispose();
        }

        private void DisposeHuntActionSession()
        {
            PlayableHuntActionSession session = huntActionSession;
            huntActionSession = null;
            session?.Dispose();
        }

        // ═══════════════════════════════════════════
        // 狩猎阶段子系统
        // ═══════════════════════════════════════════

        private bool TryEnterHuntPhase(IReadOnlyList<HunterInstance> committedRoster, bool allowDevelopmentFallback, out string reason)
        {
            List<HunterInstance> hunters = committedRoster != null ? new List<HunterInstance>(committedRoster) : null;
            if (hunters == null && !PlayableCampaignLoopContract.TryResolveDepartureRoster(_settlementManager?.Data, out hunters, out string rosterReason))
            {
                if (!allowDevelopmentFallback)
                {
                    reason = rosterReason;
                    return false;
                }
                if (!PlayableCampaignLoopContract.TryResolveDevelopmentRoster(_settlementManager?.Data, out hunters, out reason)) return false;
            }
            try
            {
                EnsureHuntManager();
                PlayableHuntDestinationRuntime.ApplyTo(_huntMgr);
                _huntMgr.EventInput = playableEventInput;
                _huntMgr.OnEnter(hunters, _settlementManager?.Data.CurrentYear ?? 1);
                DisposeHuntActionSession();
                activeExpeditionId = System.Guid.NewGuid().ToString("N");
                if (!TryStartHuntPresentationAndSession(null, out reason))
                {
                    activeExpeditionId = string.Empty;
                    CleanupHuntPresentation();
                    return false;
                }
                OnHuntCheckpointCommitted();
                return true;
            }
            catch (System.Exception exception)
            {
                DisposeHuntActionSession();
                CleanupHuntPresentation();
                activeExpeditionId = string.Empty;
                reason = $"狩猎运行环境初始化失败：{exception.Message}";
                return false;
            }
        }

        private void EnsureHuntManager()
        {
            if (_huntMgr != null) return;
            _huntMgr = CreateHuntManager(_settlementManager);
        }

        private HuntManager CreateHuntManager(SettlementManager settlementManager)
        {
            var sharedEventSystem = settlementManager?.Events ?? new HuntingInDarkness.Settlement.EventSystem(new SettlementInstance(), new HuntingInDarkness.GameCore.Foundation.SystemRandomSource());
            var manager = new HuntManager(sharedEventSystem);
            manager.OnBossEncounterTriggered = () =>
            {
                if (huntActionSession == null) return;
                var request = new CampaignEncounterRequest(huntActionSession.SessionId, PlayableEncounterRuntime.DefaultEncounterId, CampaignEncounterSourceKind.HuntBossTile, GamePhase.Hunt, _huntMgr.SquadPosition, string.Empty, PlayableHuntDestinationRuntime.ActiveDestination?.DestinationId);
                BeginEncounterAsync(request).Forget();
            };
            manager.OnHuntCompleted = record =>
            {
                if (_settlementManager?.HunterMgmt == null) throw new System.InvalidOperationException("营地猎人管理器未初始化，无法提交狩猎成长。");
                PlayableHunterAdvancementAdapter.ApplyAfterHunt(_huntMgr.ActiveHunters, _settlementManager.HunterMgmt);
                _pendingHuntRecord = record;
                TransitionToPhase(GamePhase.Settlement);
            };
            return manager;
        }

        private bool TryStartHuntPresentationAndSession(PlayableHuntEventOccurrenceStore restoredOccurrences, out string reason)
        {
            try
            {
                if (_huntVisualizer == null && huntRoot != null)
                {
                    var visualizerObject = new GameObject("HuntMapVisualizer");
                    visualizerObject.transform.SetParent(huntRoot.transform);
                    _huntVisualizer = visualizerObject.AddComponent<HuntMapVisualizer>();
                }
                _huntVisualizer?.Init(_huntMgr);
            }
            catch (System.Exception exception)
            {
                CleanupHuntPresentation();
                Debug.LogWarning($"[GameManager] 狩猎地图表现初始化失败，已降级继续：{exception.Message}");
            }
            try
            {
                huntActionSession = new PlayableHuntActionSession(_huntMgr, PlayableEncounterRuntime.DefaultEncounterId, PlayableHuntDestinationRuntime.ActiveDestination?.DestinationId, tabletopInteractionRouter, _huntVisualizer, actionEnvironmentInstallers, restoredOccurrences, OnHuntCheckpointCommitted);
            }
            catch (System.Exception exception)
            {
                reason = $"狩猎 ActionSession 初始化失败：{exception.Message}";
                return false;
            }
            try
            {
                EnsureHuntRetreatPanel();
                EnsureHuntUI();
            }
            catch (System.Exception exception)
            {
                CleanupHuntPresentation(false);
                Debug.LogWarning($"[GameManager] 狩猎交互表现初始化失败，已保留 ActionSession：{exception.Message}");
            }
            reason = string.Empty;
            return true;
        }

        private bool TryRestoreActiveHunt(CampaignSnapshot campaign, out string reason)
        {
            reason = string.Empty;
            ActiveHuntSnapshot active = campaign?.ActiveHunt;
            if (active == null)
            {
                reason = "存档不包含活动狩猎快照。";
                return false;
            }
            if (active.EncounterHandoffPending)
            {
                reason = $"存档停留在尚未支持恢复的遭遇交接：{active.EncounterId}";
                return false;
            }
            string previousDestinationId = PlayableHuntDestinationRuntime.ActiveDestination?.DestinationId ?? string.Empty;
            if (!PlayableHuntDestinationRuntime.TryRestoreSelection(active.DestinationId, out reason)) return false;
            SettlementManager previousSettlementManager = _settlementManager;
            HuntManager previousHuntManager = _huntMgr;
            GamePhase previousPhase = CurrentGamePhase;
            SettlementManager candidateSettlementManager = CreateSettlementManager();
            candidateSettlementManager.InjectData(campaign.Settlement);
            HuntManager candidateHuntManager = CreateHuntManager(candidateSettlementManager);
            PlayableHuntDestinationRuntime.ApplyTo(candidateHuntManager);
            candidateHuntManager.EventInput = playableEventInput;
            if (!ActiveHuntSnapshotAdapter.TryRestore(campaign, candidateHuntManager, out PlayableHuntRuntimeState runtimeState, out PlayableHuntEventOccurrenceStore restoredOccurrences, out reason))
            {
                RestoreHuntDestination(previousDestinationId);
                return false;
            }
            if (!candidateHuntManager.TryRestore(runtimeState, out reason))
            {
                RestoreHuntDestination(previousDestinationId);
                return false;
            }
            if (CurrentGamePhase != GamePhase.Hunt && !_phaseManager.TransitionTo(GamePhase.Hunt))
            {
                reason = "无法切换到活动狩猎恢复阶段。";
                RestoreHuntDestination(previousDestinationId);
                return false;
            }
            PlayableSettlementActionSession previousSettlementSession = settlementActionSession;
            PlayableHuntActionSession previousHuntSession = huntActionSession;
            settlementActionSession = null;
            huntActionSession = null;
            _settlementManager = candidateSettlementManager;
            _huntMgr = candidateHuntManager;
            settlementEventRestoreProjection = new SettlementEventRestoreProjection(campaign.Settlement, candidateSettlementManager.Timeline.ResolveEvent);
            activeExpeditionId = active.ExpeditionId;
            if (TryStartHuntPresentationAndSession(restoredOccurrences, out reason))
            {
                previousSettlementSession?.Dispose();
                previousHuntSession?.Dispose();
                SaveLoadSystem.TryCreatePayload(campaign, out stableCampaignPayload, out _);
                return true;
            }

            DisposeHuntActionSession();
            if (CurrentGamePhase == GamePhase.Hunt)
                _phaseManager.TransitionTo(previousPhase);
            _settlementManager = previousSettlementManager;
            _huntMgr = previousHuntManager;
            settlementActionSession = previousSettlementSession;
            huntActionSession = previousHuntSession;
            RestoreHuntDestination(previousDestinationId);
            if (previousPhase == GamePhase.Hunt && previousHuntManager != null)
            {
                _huntVisualizer?.Init(previousHuntManager);
                EnsureHuntRetreatPanel();
                EnsureHuntUI();
            }
            else if (previousPhase == GamePhase.Settlement)
            {
                CleanupHuntPresentation();
                EnsureSettlementUI();
            }
            return false;
        }

        private static void RestoreHuntDestination(string destinationId)
        {
            if (string.IsNullOrWhiteSpace(destinationId))
                PlayableHuntDestinationRuntime.RestoreSelection(null);
            else
                PlayableHuntDestinationRuntime.TryRestoreSelection(destinationId, out _);
        }

        private void CleanupHuntPresentation(bool includeVisualizer = true)
        {
            if (huntRetreatPanel != null)
                Destroy(huntRetreatPanel.gameObject);
            if (_huntUI != null)
                Destroy(_huntUI.gameObject);
            if (includeVisualizer && _huntVisualizer != null)
                Destroy(_huntVisualizer.gameObject);
            huntRetreatPanel = null;
            _huntUI = null;
            if (includeVisualizer)
                _huntVisualizer = null;
        }

        private void EnsureHuntUI()
        {
            if (_huntUI != null)
            {
                _huntUI.Init(_huntMgr, _huntVisualizer);
                return;
            }
            var uiParent = uiHunt != null ? uiHunt : huntRoot;
            if (uiParent == null) return;
            var uiGo = new GameObject("HuntUIManager", typeof(RectTransform));
            uiGo.transform.SetParent(uiParent.transform, false);
            _huntUI = uiGo.AddComponent<HuntUIManager>();
            _huntUI.Init(_huntMgr, _huntVisualizer);
        }

        private void EnsureHuntRetreatPanel()
        {
            if (_huntVisualizer == null)
                return;
            if (huntRetreatPanel == null)
                huntRetreatPanel = HuntRetreatPanel3D.Create(_huntVisualizer.transform);
            huntRetreatPanel.Initialize(this, _huntMgr);
        }

        private void EnsureSettlementUI()
        {
            // ── 2D HUD（年份标签 + 出发按钮 + 详情叠加面板）──
            // 场景预建并连线到 _settlementUIManager；缺失则报错（不再运行时程序化创建）。
            if (!_settlementUIInited)
            {
                if (_settlementUIManager != null)
                {
                    _settlementUIManager.Init(_settlementManager);
                    _settlementUIInited = true;
                }
                else
                {
                    Debug.LogWarning("[GameManager] 未配置 SettlementUIManager，将保留 3D 营地桌面与外部流程控件。");
                }
            }

            // ── 3D 卡牌桌（猎人 / 资源 / 工坊 / 发明）──
            if (_settlementTable3D == null)
            {
                if (settlementRoot == null) return;
                var tableGo = new GameObject("SettlementTable3D");
                tableGo.transform.SetParent(settlementRoot.transform, false);
                _settlementTable3D = tableGo.AddComponent<SettlementTable3D>();
            }

            // 无论桌面来自场景还是运行时回退，都从组合根注入同一组命令端口。
            _settlementTable3D.OnHunterClicked = h =>
                _settlementUIManager?.ShowHunterDetail(h);

            _settlementTable3D.OnEquipRequested = (hunterId, item) => settlementActionSession != null ? settlementActionSession.EquipItemAsync(hunterId, item) : UniTask.FromResult(SettlementEquipmentCommandResult.Failed("当前不在营地阶段。"));
            _settlementTable3D.OnUnequipRequested = (hunterId, equipmentInstanceId) => settlementActionSession != null ? settlementActionSession.UnequipItemAsync(hunterId, equipmentInstanceId) : UniTask.FromResult(SettlementEquipmentCommandResult.Failed("当前不在营地阶段。"));
            _settlementTable3D.OnCraftRequested = recipe => settlementActionSession != null ? settlementActionSession.CraftAsync(recipe) : UniTask.FromResult(SettlementCraftCommandResult.Failed("当前不在营地阶段。"));
            _settlementTable3D.OnInventionUnlockRequested = invention => settlementActionSession != null ? settlementActionSession.UnlockInventionAsync(invention) : UniTask.FromResult(SettlementInventionCommandResult.Failed("当前不在营地阶段。"));
            _settlementTable3D.OnInventionEffectRequested = (invention, effect) => settlementActionSession != null ? settlementActionSession.ActivateInventionEffectAsync(invention, effect) : UniTask.FromResult(SettlementInventionActiveEffectCommandResult.Failed("当前不在营地阶段。"));
            _settlementTable3D.OnWorkshopConstructionRequested = definition => settlementActionSession != null ? settlementActionSession.BuildWorkshopAsync(definition) : UniTask.FromResult(SettlementWorkshopConstructionResult.Failed("当前不在营地阶段。"));
            _settlementTable3D.OnRecoveryRequested = (hunterId, bodyPart) => settlementActionSession != null ? settlementActionSession.RecoverHunterAsync(hunterId, bodyPart) : UniTask.FromResult(RecoverHunterCommandResult.Failed("当前不在营地阶段。"));
            _settlementTable3D.OnRecruitRequested = (template, requestedName) => settlementActionSession != null ? settlementActionSession.RecruitHunterAsync(template, requestedName) : UniTask.FromResult(RecruitHunterCommandResult.Failed("当前不在营地阶段。"));
            _settlementTable3D.OnGrowthRequested = (hunterId, choice) => settlementActionSession != null ? settlementActionSession.SpendHunterGrowthAsync(hunterId, choice) : UniTask.FromResult(HunterGrowthCommandResult.Failed("当前不在营地阶段。"));
            _settlementTable3D.OnWeaponTrainingRequested = (hunterId, masteryId) => settlementActionSession != null ? settlementActionSession.TrainWeaponAsync(hunterId, masteryId) : UniTask.FromResult(WeaponTrainingCommandResult.Failed("当前不在营地阶段。"));
            _settlementTable3D.OnSymptomRequested = (hunterId, symptomId, choice) => settlementActionSession != null ? settlementActionSession.ResolveHunterSymptomAsync(hunterId, symptomId, choice) : UniTask.FromResult(HunterSymptomCommandResult.Failed("当前不在营地阶段。"));

            _settlementTable3D.OnDepartureRequested = squad => RequestHuntDeparture(squad != null ? squad.Where(hunter => hunter != null).Select(hunter => hunter.InstanceId).ToList() : new List<int>());

            _settlementTable3D.Init(_settlementManager, workshopContentCatalog, settlementContentCatalog);
        }

        // ═══════════════════════════════════════════
        // 阶段管理 (Phase Management)
        // ═══════════════════════════════════════════

        /// <summary>获取当前游戏大阶段</summary>
        public GamePhase CurrentGamePhase => _phaseManager?.CurrentPhase ?? GamePhase.Settlement;
        public SettlementInstance SettlementData => _settlementManager?.Data;
        public IReadOnlyList<HunterInstance> ActiveHuntHunters => _huntMgr != null ? _huntMgr.ActiveHunters : System.Array.Empty<HunterInstance>();
        public bool IsHuntActionSessionActive => huntActionSession?.IsActive == true;
        public bool IsHuntActionSessionRunning => huntActionSession?.IsRunning == true;
        bool IPlayableHuntRetreatInput.IsReturnCheckpointLocked => huntActionSession?.IsReturnCheckpointLocked == true;
        public bool IsCampaignActionSessionActive => campaignActionSession?.IsActive == true;
        public bool IsSettlementActionSessionRunning => settlementActionSession?.IsRunning == true;
        public bool IsSettlementEventRestoreReady => settlementEventRestoreProjection == null || settlementEventRestoreProjection.IsReady;
        public IActionEnvironmentInstallerRegistry ActionEnvironmentInstallers => actionEnvironmentInstallers;
        public CardGame.ActionQueue.ReactorRegistry SettlementActionReactors => settlementActionSession?.Reactors;
        public CardGame.ActionQueue.ReactorRegistry CampaignActionReactors => campaignActionSession?.Reactors;
        public CardGame.ActionQueue.ReactorRegistry HuntActionReactors => huntActionSession?.Reactors;
        public InventionSystem SettlementInventions => _settlementManager?.Inventions;
        public WorkshopSystem SettlementWorkshop => _settlementManager?.Workshop;
        public HunterManagementSystem SettlementHunters => _settlementManager?.HunterMgmt;
        public event System.Action<EventData, HunterInstance> SettlementEventPresented;
        public event System.Action<bool> SettlementProgressLoadCompleted;

        public void SetPlayableEventInput(IPlayableEventInput input)
        {
            playableEventInput = input;
            if (settlementActionSession != null)
                settlementActionSession.EventInput = input;
            if (_huntMgr != null)
                _huntMgr.EventInput = input;
        }

        public void ClearPlayableEventInput(IPlayableEventInput input)
        {
            if (!ReferenceEquals(playableEventInput, input)) return;
            playableEventInput = null;
            if (settlementActionSession != null && ReferenceEquals(settlementActionSession.EventInput, input))
                settlementActionSession.EventInput = null;
            if (_huntMgr != null && ReferenceEquals(_huntMgr.EventInput, input))
                _huntMgr.EventInput = null;
        }

        public void SetPlayableHuntDepartureInput(IPlayableHuntDepartureInput input) => playableHuntDepartureInput = input;

        public void ClearPlayableHuntDepartureInput(IPlayableHuntDepartureInput input)
        {
            if (ReferenceEquals(playableHuntDepartureInput, input))
                playableHuntDepartureInput = null;
        }

        public void RequestHuntDeparture(IReadOnlyList<int> hunterIds)
        {
            if (_pendingHuntRecord != null)
            {
                RetryPendingHuntReturnAsync().Forget();
                return;
            }
            if (!CanDepartAfterSettlementEventRestore(out _))
                return;
            if (playableHuntDepartureInput != null)
            {
                playableHuntDepartureInput.RequestDeparture(hunterIds);
                return;
            }
            DepartForHuntAsync(hunterIds).Forget();
        }

        public UniTask<SettlementDepartureCommandResult> DepartForHuntAsync(IReadOnlyList<int> hunterIds) => DepartForHuntAsync(hunterIds, null);

        public async UniTask<SettlementDepartureCommandResult> DepartForHuntAsync(IReadOnlyList<int> hunterIds, PlayableHuntDestination destination)
        {
            if (_pendingHuntRecord != null)
            {
                if (!huntReturnRecoveryInFlight)
                    await RetryPendingHuntReturnAsync();
                return SettlementDepartureCommandResult.Failed("请先完成上一场远征的回营结算，再重新发起出猎。");
            }
            if (huntDepartureInFlight)
                return SettlementDepartureCommandResult.Failed("出猎流程正在处理中。");
            if (CurrentGamePhase != GamePhase.Settlement || settlementActionSession == null || !settlementActionSession.IsActive)
                return SettlementDepartureCommandResult.Failed("当前不在营地阶段。");
            if (!CanDepartAfterSettlementEventRestore(out string restoreReason))
                return SettlementDepartureCommandResult.Failed(restoreReason);
            if (IsSettlementActionSessionRunning)
                return SettlementDepartureCommandResult.Failed("请先完成当前营地流程。");
            if (!PlayableHuntDestinationRuntime.CanSelectForDeparture(destination, SettlementData.CurrentYear, out string selectionReason))
                return SettlementDepartureCommandResult.Failed(selectionReason);

            huntDepartureInFlight = true;
            PlayableHuntDestination previousDestination = PlayableHuntDestinationRuntime.ActiveDestination;
            bool selectionApplied = false;
            CancellationToken cancellationToken = this.GetCancellationTokenOnDestroy();
            try
            {
                SettlementDepartureCommandResult departure = await settlementActionSession.PrepareDepartureAsync(hunterIds, cancellationToken);
                if (!departure.Succeeded)
                    return departure;
                if (!PlayableHuntDestinationRuntime.TrySelectForDeparture(destination, SettlementData.CurrentYear, out selectionReason))
                    return SettlementDepartureCommandResult.Failed(selectionReason);
                selectionApplied = true;

                CampaignPhaseTransitionResult transition = await TransitionToPhaseAsync(GamePhase.Hunt);
                if (!transition.Succeeded)
                {
                    PlayableHuntDestinationRuntime.RestoreSelection(previousDestination);
                    selectionApplied = false;
                    return SettlementDepartureCommandResult.Failed(transition.Reason);
                }
                EventBus.Publish(new HuntDepartedEvent { HunterIds = departure.HunterIds.ToArray() });
                return departure;
            }
            catch (System.OperationCanceledException)
            {
                if (selectionApplied)
                    PlayableHuntDestinationRuntime.RestoreSelection(previousDestination);
                return SettlementDepartureCommandResult.Failed("出猎流程已取消。");
            }
            finally
            {
                huntDepartureInFlight = false;
            }
        }

        public bool TryDepartForHunt(IReadOnlyList<int> hunterIds)
        {
            if (_pendingHuntRecord != null)
            {
                if (!huntReturnRecoveryInFlight)
                    RetryPendingHuntReturnAsync().Forget();
                return false;
            }
            if (huntDepartureInFlight || IsSettlementActionSessionRunning)
                return false;
            if (CurrentGamePhase != GamePhase.Settlement || settlementActionSession == null || !settlementActionSession.IsActive || SettlementData == null)
                return false;
            if (!CanDepartAfterSettlementEventRestore(out _))
                return false;
            if (!DepartureRules.CanDepart(hunterIds, out _))
                return false;
            if (!PlayableHuntDestinationRuntime.CanSelectForDeparture(null, SettlementData.CurrentYear, out _))
                return false;
            DepartForHuntAsync(hunterIds).Forget();
            return true;
        }

        bool ISettlementDepartureRequestPort.RequestDeparture(IReadOnlyList<int> hunterIds) => TryDepartForHunt(hunterIds);

        private bool CanDepartAfterSettlementEventRestore(out string reason)
        {
            if (IsSettlementEventRestoreReady)
            {
                reason = string.Empty;
                return true;
            }

            reason = settlementEventRestoreProjection?.FailureReason;
            if (string.IsNullOrWhiteSpace(reason))
                reason = "请先完成读档后的营地事件恢复。";
            return false;
        }

        private void QueueSettlementEvents(IReadOnlyList<EventData> events, SettlementEventRestoreProjection restoreProjection = null, string restoredChainId = null, IReadOnlyList<SettlementEventChainOccurrence> restoredOccurrences = null)
        {
            if (events == null || events.Count == 0 || settlementActionSession == null) return;
            ResolveSettlementEventsAsync(settlementActionSession, events, restoreProjection, restoredChainId, restoredOccurrences).Forget();
        }

        private void QueueSettlementEvents(IReadOnlyList<SettlementEventWork> works, SettlementEventRestoreProjection restoreProjection = null, string restoredChainId = null)
        {
            if (works == null || works.Count == 0 || settlementActionSession == null) return;
            ResolveSettlementEventsAsync(settlementActionSession, works, restoreProjection, restoredChainId).Forget();
        }

        private async UniTask<SettlementHuntReturnCommandResult> ApplyHuntReturnAsync(PlayableSettlementActionSession session, HuntRecord record, bool queueAnnualEvents = true)
        {
            SettlementHuntReturnCommandResult result;
            try
            {
                result = await session.ApplyHuntReturnAsync(record, this.GetCancellationTokenOnDestroy());
            }
            catch (System.Exception exception)
            {
                Debug.LogError($"[GameManager] 远征归来结算异常：{exception}");
                return SettlementHuntReturnCommandResult.Failed("回营结算异常，已保留待恢复记录。");
            }
            if (!result.Succeeded)
            {
                Debug.LogError($"[GameManager] 远征归来结算失败：{result.Reason}");
                return result;
            }
            if (ReferenceEquals(session, settlementActionSession))
            {
                SettlementInstance settlement = _settlementManager?.Data;
                if (settlement == null)
                    return SettlementHuntReturnCommandResult.Failed("营地数据已失效，回营记录仍保留在存档中。");
                CancellationToken cancellationToken = this.GetCancellationTokenOnDestroy();
                if (!await TrySaveCampaignAsync(false, cancellationToken))
                    return SettlementHuntReturnCommandResult.Failed("回营结果尚未可靠保存，已保留待恢复记录。");

                SettlementEventRestoreProjection projection = null;
                SettlementEventRestorePlan restorePlan = default;
                if (queueAnnualEvents)
                {
                    projection = new SettlementEventRestoreProjection(settlement, _settlementManager.Timeline.ResolveEvent);
                    restorePlan = projection.Prepare();
                    if (!restorePlan.Succeeded)
                    {
                        settlementEventRestoreProjection = projection;
                        Debug.LogError($"[GameManager] 回营年度事件投影失败：{restorePlan.FailureReason}");
                        return SettlementHuntReturnCommandResult.Failed(restorePlan.FailureReason);
                    }
                }

                if (!PlayableCampaignLoopContract.TryClearAppliedReturnCheckpoint(settlement, record, out string checkpointReason))
                    return SettlementHuntReturnCommandResult.Failed(checkpointReason);
                _pendingHuntRecord = null;
                if (!await TrySaveCampaignAsync(false, cancellationToken))
                {
                    _pendingHuntRecord = record;
                    settlement.PendingHuntReturn = record;
                    return SettlementHuntReturnCommandResult.Failed("回营检查点尚未清除，请重试后再出猎。");
                }

                if (queueAnnualEvents)
                {
                    settlementEventRestoreProjection = projection;
                    QueueSettlementEvents(restorePlan.WorkItems, projection, restorePlan.ChainId);
                }
            }
            return result;
        }

        private async UniTask<bool> RetryPendingHuntReturnAsync()
        {
            if (_pendingHuntRecord == null || settlementActionSession == null || !settlementActionSession.IsActive || huntReturnRecoveryInFlight)
                return false;
            huntReturnRecoveryInFlight = true;
            try
            {
                SettlementHuntReturnCommandResult result = await ApplyHuntReturnAsync(settlementActionSession, _pendingHuntRecord);
                return result.Succeeded;
            }
            catch (System.Exception exception)
            {
                Debug.LogError($"[GameManager] 重试远征归来结算异常：{exception}");
                return false;
            }
            finally
            {
                huntReturnRecoveryInFlight = false;
            }
        }

        private async UniTaskVoid ResolveSettlementEventsAsync(PlayableSettlementActionSession session, IReadOnlyList<EventData> events, SettlementEventRestoreProjection restoreProjection = null, string restoredChainId = null, IReadOnlyList<SettlementEventChainOccurrence> restoredOccurrences = null)
        {
            SettlementEventCommandResult result;
            try
            {
                result = await session.ResolveEventsAsync(events, restoredChainId, restoredOccurrences);
            }
            catch (System.Exception exception)
            {
                restoreProjection?.Fail($"营地事件恢复异常：{exception.Message}");
                Debug.LogError($"[GameManager] 营地事件链执行异常：{exception}");
                return;
            }
            if (restoreProjection != null)
            {
                bool restoreCompleted = restoreProjection.Complete(result.Succeeded);
                if (result.Succeeded && !restoreCompleted && restoreProjection.HasRecoverableCheckpoint)
                {
                    SettlementEventRestorePlan nextRestorePlan = restoreProjection.Prepare();
                    if (!nextRestorePlan.Succeeded)
                    {
                        Debug.LogError($"[GameManager] 下一条营地事件链恢复失败：{nextRestorePlan.FailureReason}");
                    }
                    else if (nextRestorePlan.HasPendingEvents)
                    {
                        QueueSettlementEvents(nextRestorePlan.WorkItems, restoreProjection, nextRestorePlan.ChainId);
                    }
                }
            }
            if (!result.Succeeded && ReferenceEquals(session, settlementActionSession))
                Debug.LogWarning($"[GameManager] 营地事件链未完成：{result.Reason}");
        }

        private async UniTaskVoid ResolveSettlementEventsAsync(PlayableSettlementActionSession session, IReadOnlyList<SettlementEventWork> works, SettlementEventRestoreProjection restoreProjection = null, string restoredChainId = null)
        {
            SettlementEventCommandResult result;
            try
            {
                result = await session.ResolveEventsAsync(works, restoredChainId);
            }
            catch (System.Exception exception)
            {
                restoreProjection?.Fail($"营地事件恢复异常：{exception.Message}");
                Debug.LogError($"[GameManager] 营地事件链执行异常：{exception}");
                return;
            }
            if (restoreProjection != null)
            {
                bool restoreCompleted = restoreProjection.Complete(result.Succeeded);
                if (result.Succeeded && !restoreCompleted && restoreProjection.HasRecoverableCheckpoint)
                {
                    SettlementEventRestorePlan nextRestorePlan = restoreProjection.Prepare();
                    if (nextRestorePlan.Succeeded && nextRestorePlan.HasPendingEvents)
                        QueueSettlementEvents(nextRestorePlan.WorkItems, restoreProjection, nextRestorePlan.ChainId);
                    else if (!nextRestorePlan.Succeeded)
                        Debug.LogError($"[GameManager] 下一条营地事件链恢复失败：{nextRestorePlan.FailureReason}");
                }
            }
            if (!result.Succeeded && ReferenceEquals(session, settlementActionSession))
                Debug.LogWarning($"[GameManager] 营地事件链未完成：{result.Reason}");
        }

        public void ResolveSettlementNarrative(EventData gameEvent) => _settlementManager?.Events.ResolveNarrative(gameEvent);

        public EventResolutionResult ResolveSettlementChoice(EventData gameEvent, int optionIndex, HunterInstance hunter = null) => _settlementManager != null ? _settlementManager.Events.ResolveChoice(gameEvent, optionIndex, hunter) : default;

        public PlayableEventChoiceTransaction PrepareSettlementChoice(EventData gameEvent, int optionIndex, HunterInstance hunter = null) => _settlementManager?.Events.PrepareChoice(gameEvent, optionIndex, hunter);

        public void SaveSettlementProgress() => DevSave();

        public bool CanTrainWeapon(int hunterId, string masteryId, out string reason)
        {
            if (settlementActionSession != null && settlementActionSession.IsActive)
                return settlementActionSession.CanTrainWeapon(hunterId, masteryId, out reason);
            reason = "仅可在营地阶段训练";
            return false;
        }

        public UniTask<WeaponTrainingCommandResult> TrainWeaponAsync(int hunterId, string masteryId)
        {
            if (settlementActionSession == null || !settlementActionSession.IsActive)
                return UniTask.FromResult(WeaponTrainingCommandResult.Failed("仅可在营地阶段训练"));
            return settlementActionSession.TrainWeaponAsync(hunterId, masteryId);
        }

        public bool CanCraft(CraftRecipe recipe, out string reason)
        {
            if (settlementActionSession != null && settlementActionSession.IsActive)
                return settlementActionSession.CanCraft(recipe, out reason);
            reason = "仅可在营地阶段制作。";
            return false;
        }

        public UniTask<SettlementCraftCommandResult> CraftAsync(CraftRecipe recipe)
        {
            if (settlementActionSession == null || !settlementActionSession.IsActive)
                return UniTask.FromResult(SettlementCraftCommandResult.Failed("仅可在营地阶段制作。"));
            return settlementActionSession.CraftAsync(recipe);
        }

        public bool CanRecruitHunter(out string reason)
        {
            if (settlementActionSession != null && settlementActionSession.IsActive)
                return settlementActionSession.CanRecruit(out reason);
            reason = "仅可在营地阶段招募。";
            return false;
        }

        public UniTask<RecruitHunterCommandResult> RecruitHunterAsync(HunterData template, string requestedName)
        {
            if (settlementActionSession == null || !settlementActionSession.IsActive)
                return UniTask.FromResult(RecruitHunterCommandResult.Failed("仅可在营地阶段招募。"));
            return settlementActionSession.RecruitHunterAsync(template, requestedName);
        }

        public bool HasRecoverableHunter() => settlementActionSession?.IsActive == true && settlementActionSession.HasRecoverableHunter();

        public bool CanRecoverHunter(int hunterId, HunterBodyPart bodyPart, out string reason)
        {
            if (settlementActionSession != null && settlementActionSession.IsActive)
                return settlementActionSession.CanRecoverHunter(hunterId, bodyPart, out reason);
            reason = "仅可在营地阶段休养。";
            return false;
        }

        public UniTask<RecoverHunterCommandResult> RecoverHunterAsync(int hunterId, HunterBodyPart bodyPart)
        {
            if (settlementActionSession == null || !settlementActionSession.IsActive)
                return UniTask.FromResult(RecoverHunterCommandResult.Failed("仅可在营地阶段休养。"));
            return settlementActionSession.RecoverHunterAsync(hunterId, bodyPart);
        }

        public bool TrySpendHunterGrowth(int hunterId, HunterGrowthChoice choice)
        {
            if (!PlayableHunterAdvancementAdapter.TrySpendGrowth(_settlementManager?.Data.GetHunter(hunterId), choice)) return false;
            SaveSettlementProgress();
            return true;
        }

        public UniTask<HunterGrowthCommandResult> SpendHunterGrowthAsync(int hunterId, HunterGrowthChoice choice)
        {
            if (settlementActionSession == null || !settlementActionSession.IsActive)
                return UniTask.FromResult(HunterGrowthCommandResult.Failed("仅可在营地阶段分配成长。"));
            return settlementActionSession.SpendHunterGrowthAsync(hunterId, choice);
        }

        public bool OnRelieveOvertimeCharacter(int targetId)
        {
            return _combatSession != null && _combatSession.TryRelieveOvertimeCharacter(targetId);
        }

        public TimelineActionStatus GetTimelineStatus(int characterId) => _combatSession?.GetTimelineStatus(characterId) ?? TimelineActionStatus.Done;

        public void LoadSettlementProgress() => DevLoad();

        public void RetreatFromHunt()
        {
            RequestRetreatAsync().Forget();
        }

        public async UniTask<HuntRetreatCommandResult> RequestRetreatAsync()
        {
            if (huntRetreatInFlight)
                return HuntRetreatCommandResult.Failed("回营流程正在处理中。");
            if (CurrentGamePhase != GamePhase.Hunt || huntActionSession == null || !huntActionSession.IsActive)
                return HuntRetreatCommandResult.Failed("当前不在有效的狩猎阶段。");
            if (IsHuntActionSessionRunning)
                return HuntRetreatCommandResult.Failed("请先完成当前狩猎流程。");
            if (_huntMgr == null || _settlementManager?.Data == null || _settlementManager.HunterMgmt == null)
                return HuntRetreatCommandResult.Failed("狩猎结算依赖尚未准备完成。");

            if (preparedHuntExit && _pendingHuntRecord != null)
            {
                CampaignPhaseTransitionResult retryTransition = await TransitionToPhaseAsync(GamePhase.Settlement);
                return retryTransition.Succeeded ? HuntRetreatCommandResult.Success(_pendingHuntRecord) : HuntRetreatCommandResult.Failed(retryTransition.Reason);
            }

            huntRetreatInFlight = true;
            try
            {
                HuntRetreatCommandResult retreat = await huntActionSession.PrepareRetreatAsync(_settlementManager.Data.CurrentYear, this.GetCancellationTokenOnDestroy());
                if (!retreat.Succeeded)
                    return retreat;

                _pendingHuntRecord = retreat.Record;
                _settlementManager.Data.PendingHuntReturn = retreat.Record;
                if (!await TrySaveCampaignAsync(false, this.GetCancellationTokenOnDestroy()))
                {
                    _settlementManager.Data.PendingHuntReturn = null;
                    _pendingHuntRecord = null;
                    return HuntRetreatCommandResult.Failed("无法建立可靠的回营检查点，请留在狩猎阶段重试。");
                }

                huntActionSession.SetReturnCheckpointLock(true);
                preparedHuntExit = true;
                CampaignPhaseTransitionResult transition = await TransitionToPhaseAsync(GamePhase.Settlement);
                if (transition.Succeeded)
                    return retreat;

                preparedHuntExit = false;
                _pendingHuntRecord = null;
                _settlementManager.Data.PendingHuntReturn = null;
                if (!await TrySaveCampaignAsync(true, this.GetCancellationTokenOnDestroy()))
                {
                    preparedHuntExit = true;
                    _pendingHuntRecord = retreat.Record;
                    _settlementManager.Data.PendingHuntReturn = retreat.Record;
                    return HuntRetreatCommandResult.Failed("阶段切换被拒绝，且回营检查点尚未安全撤销；请直接重试回营。");
                }
                huntActionSession.SetReturnCheckpointLock(false);
                return HuntRetreatCommandResult.Failed(transition.Reason);
            }
            catch (System.OperationCanceledException)
            {
                return HuntRetreatCommandResult.Failed("回营流程已取消。");
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
                return HuntRetreatCommandResult.Failed("回营结算失败，请保留当前狩猎并重试。");
            }
            finally
            {
                huntRetreatInFlight = false;
            }
        }

        /// <summary>
        /// 切换游戏大阶段。GameManager 负责 Enable/Disable 对应根物体，
        /// 并触发该阶段的初始化逻辑。
        /// </summary>
        public void TransitionToPhase(GamePhase newPhase)
        {
            if (CurrentGamePhase == GamePhase.Hunt && newPhase == GamePhase.Settlement && !preparedHuntExit && _pendingHuntRecord == null)
            {
                RequestRetreatAsync().Forget();
                return;
            }
            TransitionToPhaseAsync(newPhase).Forget();
        }

        public UniTask<CampaignPhaseTransitionResult> TransitionToPhaseAsync(GamePhase newPhase)
        {
            if (campaignActionSession?.IsActive == true)
                return campaignActionSession.TransitionAsync(newPhase, this.GetCancellationTokenOnDestroy());
            GamePhase previousPhase = CurrentGamePhase;
            if (TryApplyPhaseTransition(newPhase, out string reason))
                return UniTask.FromResult(new CampaignPhaseTransitionResult(true, previousPhase != CurrentGamePhase, previousPhase, CurrentGamePhase, string.Empty));
            return UniTask.FromResult(CampaignPhaseTransitionResult.Failed(CurrentGamePhase, reason));
        }

        public UniTask<CampaignEncounterStartResult> BeginEncounterAsync(CampaignEncounterRequest request)
        {
            if (campaignActionSession?.IsActive == true)
                return campaignActionSession.BeginEncounterAsync(request, this.GetCancellationTokenOnDestroy());
            if (TryBeginEncounter(request, out string reason))
                return UniTask.FromResult(new CampaignEncounterStartResult(true, request.EncounterId, string.Empty));
            return UniTask.FromResult(CampaignEncounterStartResult.Failed(request.EncounterId, reason));
        }

        GamePhase ICampaignPhaseTransitionHost.CurrentPhase => CurrentGamePhase;

        bool ICampaignPhaseTransitionHost.TryApplyPhaseTransition(GamePhase targetPhase, out string reason) => TryApplyPhaseTransition(targetPhase, out reason);

        bool ICampaignPhaseTransitionHost.TryBeginEncounter(CampaignEncounterRequest request, out string reason) => TryBeginEncounter(request, out reason);

        private bool TryBeginEncounter(CampaignEncounterRequest request, out string reason)
        {
            encounterCheckpointRollbackFailed = false;
            if (CurrentGamePhase != request.SourcePhase)
            {
                reason = "遭遇请求的来源阶段已经结束";
                return false;
            }
            bool sourceSessionMatches = request.SourceKind switch
            {
                CampaignEncounterSourceKind.HuntBossTile or CampaignEncounterSourceKind.HuntEvent => huntActionSession?.IsActive == true && huntActionSession.SessionId == request.SourceSessionId,
                CampaignEncounterSourceKind.SettlementEvent => settlementActionSession?.IsActive == true && settlementActionSession.SessionId == request.SourceSessionId,
                _ => false
            };
            if (!sourceSessionMatches)
            {
                reason = "遭遇请求不属于当前阶段会话";
                return false;
            }
            if (!PlayableEncounterRuntime.TryCreateSetup(request.EncounterId, out BattleSetup setup, out reason)) return false;
            bool huntEncounter = request.SourceKind is CampaignEncounterSourceKind.HuntBossTile or CampaignEncounterSourceKind.HuntEvent;
            string previousStablePayload = stableCampaignPayload;
            if (huntEncounter)
            {
                if (!TryCreateEncounterHandoffPayload(request.EncounterId, out string handoffPayload, out string normalHuntPayload, out reason)) return false;
                if (string.IsNullOrWhiteSpace(previousStablePayload)) previousStablePayload = normalHuntPayload;
                if (!SaveLoadSystem.TrySavePayloadImmediate(handoffPayload))
                {
                    reason = "无法建立可靠的遭遇交接检查点。";
                    return false;
                }
                stableCampaignPayload = handoffPayload;
            }

            BattleSetup previousSetup = _pendingSetup;
            IReadOnlyList<HunterInstance> previousHunters = pendingEncounterHunters;
            _pendingSetup = setup;
            pendingEncounterHunters = request.SourceKind == CampaignEncounterSourceKind.SettlementEvent ? _settlementManager?.Data.GetAvailableHunters() : _huntMgr?.ActiveHunters;
            if (TryApplyPhaseTransition(GamePhase.BossFight, out reason)) return true;
            _pendingSetup = previousSetup;
            pendingEncounterHunters = previousHunters;
            if (huntEncounter)
            {
                if (SaveLoadSystem.TrySavePayloadImmediate(previousStablePayload))
                    stableCampaignPayload = previousStablePayload;
                else
                {
                    encounterCheckpointRollbackFailed = true;
                    reason = "遭遇阶段切换失败，且交接检查点尚未安全撤销；请直接重试遭遇。";
                    return false;
                }
            }
            return false;
        }

        private bool TryCreateEncounterHandoffPayload(string encounterId, out string payload, out string normalHuntPayload, out string reason)
        {
            string destinationId = PlayableHuntDestinationRuntime.ActiveDestination?.DestinationId ?? string.Empty;
            if (!ActiveHuntSnapshotAdapter.TryCapture(_settlementManager?.Data, _huntMgr, huntActionSession, activeExpeditionId, destinationId, out CampaignSnapshot snapshot, out reason, true))
            {
                payload = string.Empty;
                normalHuntPayload = string.Empty;
                return false;
            }
            if (!SaveLoadSystem.TryCreatePayload(snapshot, out normalHuntPayload, out reason))
            {
                payload = string.Empty;
                return false;
            }
            snapshot.ActiveHunt.EncounterHandoffPending = true;
            snapshot.ActiveHunt.EncounterId = encounterId?.Trim() ?? string.Empty;
            return SaveLoadSystem.TryCreatePayload(snapshot, out payload, out reason);
        }

        private bool TryApplyPhaseTransition(GamePhase newPhase, out string reason)
        {
            reason = string.Empty;
            if (_phaseManager == null)
            {
                reason = "阶段管理器尚未初始化";
                return false;
            }
            if (newPhase == _phaseManager.CurrentPhase) return true;
            GamePhase previousPhase = _phaseManager.CurrentPhase;
            if (previousPhase == GamePhase.Settlement && newPhase == GamePhase.Hunt)
            {
                if (_pendingHuntRecord != null || _settlementManager?.Data?.PendingHuntReturn != null)
                {
                    reason = "上一场远征的回营结算尚未完成";
                    return false;
                }
                if (!CanDepartAfterSettlementEventRestore(out reason)) return false;
                if (settlementActionSession?.IsActive != true || settlementActionSession.IsRunning)
                {
                    reason = "营地流程尚未完成";
                    return false;
                }
            }
            if (previousPhase == GamePhase.Hunt && newPhase == GamePhase.Settlement && !preparedHuntExit && _pendingHuntRecord == null)
            {
                reason = "狩猎必须先通过 Hunt Runner 准备回营结算";
                return false;
            }

            if (previousPhase == GamePhase.Settlement && newPhase == GamePhase.Hunt)
            {
                bool entered = PlayableCampaignLoopContract.TryEnterHunt(_settlementManager?.Data,
                    () => _phaseManager.TransitionTo(newPhase),
                    roster => TryEnterHuntPhase(roster, false, out string entryReason) ? CampaignHuntEntryResult.Success() : CampaignHuntEntryResult.Failed(entryReason),
                    () =>
                    {
                        DisposeHuntActionSession();
                        if (_phaseManager.CurrentPhase == GamePhase.Hunt)
                            _phaseManager.TransitionTo(previousPhase);
                    },
                    out reason);
                if (entered)
                    DisposeSettlementActionSession();
                return entered;
            }

            // 先让 FSM 确认切换，再释放旧会话，避免切换被拒绝时留下“旧阶段仍在但会话已销毁”。
            if (!_phaseManager.TransitionTo(newPhase))
            {
                reason = $"无法从 {previousPhase} 切换到 {newPhase}";
                return false;
            }
            if (previousPhase == GamePhase.BossFight)
            {
                ApplyBossFightLoot();
                DisposeCombatSession();
            }
            if (previousPhase == GamePhase.Settlement)
                DisposeSettlementActionSession();
            if (previousPhase == GamePhase.Hunt)
            {
                if (newPhase == GamePhase.Settlement && preparedHuntExit)
                    CommitPreparedHuntExit();
                DisposeHuntActionSession();
            }

            // 进入新阶段的初始化
            switch (newPhase)
            {
                case GamePhase.Settlement:
                    Debug.Log("[GameManager] 进入营地阶段");
                    _settlementManager ??= CreateSettlementManager();
                    // 若有待结算的狩猎记录（推进年份），否则普通进入
                    var record = _pendingHuntRecord ?? _settlementManager.Data.PendingHuntReturn;
                    _pendingHuntRecord = record;
                    StartSettlementActionSession();
                    EnsureSettlementUI();
                    if (record != null)
                        ApplyHuntReturnAsync(settlementActionSession, record).Forget();
                    else
                    {
                        QueueSettlementEvents(_settlementManager.OnEnterWorkItems());
                        SaveCampaignAsync(false).Forget();
                    }
                    break;

                case GamePhase.Hunt:
                    Debug.Log("[GameManager] 进入狩猎阶段");
                    break;

                case GamePhase.BossFight:
                    Debug.Log("[GameManager] 进入Boss决战阶段");
                    EnterBossFightPhase();
                    break;
            }
            return true;
        }

        private void CommitPreparedHuntExit()
        {
            preparedHuntExit = false;
            activeExpeditionId = null;
        }

        private void EnterBossFightPhase()
        {
            IReadOnlyList<HunterInstance> encounterHunters = pendingEncounterHunters ?? _huntMgr?.ActiveHunters;
            pendingEncounterHunters = null;
            if (!StartCombatSession())
            {
                if (CurrentGamePhase == GamePhase.BossFight)
                    TransitionToPhase(GamePhase.Settlement);
                return;
            }

            _combatSession.Start(encounterHunters, _settlementManager?.HunterMgmt, QueueDefeatedHuntCompletion);
        }

        /// <summary>
        /// 根据新阶段 Enable/Disable 三个根物体，由 PhaseManager 回调。
        /// </summary>
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

        // ═══════════════════════════════════════════
        // 清理
        // ═══════════════════════════════════════════

        private void OnApplicationQuit()
        {
            if (_settlementManager?.Data == null)
                return;
            if (CurrentGamePhase == GamePhase.Settlement)
                TryCaptureCampaignPayload(false, out _, out _);
            else if (CurrentGamePhase == GamePhase.Hunt && huntActionSession?.IsRunning != true)
                TryCaptureCampaignPayload(true, out _, out _);
            if (!string.IsNullOrWhiteSpace(stableCampaignPayload))
                SaveLoadSystem.SavePayloadImmediate(stableCampaignPayload);
        }

        private void OnDestroy()
        {
            PlayableCampaignActionSession campaignSession = campaignActionSession;
            campaignActionSession = null;
            campaignSession?.Dispose();
            _phaseManager?.Shutdown();
            EventBus.Unsubscribe<BossDefeatedEvent>(OnBossDefeated);
            EventBus.Unsubscribe<GameOverEvent>(OnGameOver);
            EventBus.Unsubscribe<HunterRosterChangedEvent>(OnHunterRosterChanged);
            EventBus.Unsubscribe<CardHoverPreviewEvent>(OnCardHoverPreview);
            EventBus.Unsubscribe<CardHoverPreviewEndEvent>(OnCardHoverPreviewEnd);
            EventBus.Unsubscribe<SettlementTransactionCommittedEvent>(OnSettlementTransactionCommitted);
            EventBus.Unsubscribe<CampaignEncounterRequestedEvent>(OnCampaignEncounterRequested);
            EventBus.Unsubscribe<PlayableEventEncounterRequestedEvent>(OnPlayableEventEncounterRequested);
            DisposeSettlementActionSession();
            DisposeHuntActionSession();
            DisposeCombatSession();
            actionEnvironmentInstallers.Dispose();
            if (Instance == this)
                Instance = null;
        }

        // ═══════════════════════════════════════════
        // 事件处理器（全局）
        // ═══════════════════════════════════════════

        /// <summary>Boss被击败 → 结算狩猎 → 返回营地</summary>
        private void OnBossDefeated(BossDefeatedEvent _)
        {
            if (CurrentGamePhase != GamePhase.BossFight || _combatSession == null) return;
            Debug.Log("[GameManager] 收到 BossDefeatedEvent → 狩猎结算 → 营地");
            _combatSession.AccumulateDefeatLoot();
            _combatSession.SettleWeaponMastery();
            if (_huntMgr != null && _settlementManager != null)
                _huntMgr.CompleteHunt(bossDefeated: true, settlement: _settlementManager.Data);
            else
                TransitionToPhase(GamePhase.Settlement);
        }

        /// <summary>游戏结束（全部猎人死亡）</summary>
        private void OnGameOver(GameOverEvent evt)
        {
            Debug.Log($"[GameManager] 游戏结束：{evt.Reason}");
            gameOverView?.Show(evt.Reason);
        }

        private void OnCampaignEncounterRequested(CampaignEncounterRequestedEvent evt) => BeginCampaignEncounterAsync(evt.Request).Forget();

        private void OnPlayableEventEncounterRequested(PlayableEventEncounterRequestedEvent evt)
        {
            if (CurrentGamePhase == GamePhase.Settlement && settlementActionSession?.IsActive == true)
            {
                var request = new CampaignEncounterRequest(settlementActionSession.SessionId, string.IsNullOrWhiteSpace(evt.EncounterId) ? PlayableEncounterRuntime.DefaultEncounterId : evt.EncounterId, CampaignEncounterSourceKind.SettlementEvent, GamePhase.Settlement, Vector2Int.zero, evt.SourceEventId, "settlement");
                BeginEncounterAsync(request).Forget();
                return;
            }
            if (CurrentGamePhase == GamePhase.Hunt && huntActionSession?.IsActive == true)
            {
                var request = new CampaignEncounterRequest(huntActionSession.SessionId, string.IsNullOrWhiteSpace(evt.EncounterId) ? PlayableEncounterRuntime.DefaultEncounterId : evt.EncounterId, CampaignEncounterSourceKind.HuntEvent, GamePhase.Hunt, _huntMgr?.SquadPosition ?? Vector2Int.zero, evt.SourceEventId, PlayableHuntDestinationRuntime.ActiveDestination?.DestinationId);
                BeginEncounterAsync(request).Forget();
            }
        }

        private async UniTaskVoid BeginCampaignEncounterAsync(CampaignEncounterRequest request)
        {
            CampaignEncounterStartResult result;
            try
            {
                result = await BeginEncounterAsync(request);
            }
            catch (System.Exception exception)
            {
                if (!encounterCheckpointRollbackFailed && (request.SourceKind is CampaignEncounterSourceKind.HuntEvent or CampaignEncounterSourceKind.HuntBossTile) && huntActionSession?.SessionId == request.SourceSessionId)
                    huntActionSession.ReleaseEncounterHandoffLock();
                Debug.LogException(exception);
                return;
            }
            if (!result.Succeeded)
            {
                if (!encounterCheckpointRollbackFailed && (request.SourceKind is CampaignEncounterSourceKind.HuntEvent or CampaignEncounterSourceKind.HuntBossTile) && huntActionSession?.SessionId == request.SourceSessionId)
                    huntActionSession.ReleaseEncounterHandoffLock();
                Debug.LogWarning($"[GameManager] 无法开始遭遇 {request.EncounterId}：{result.Reason}");
            }
        }

        private void OnSettlementTransactionCommitted(SettlementTransactionCommittedEvent evt)
        {
            if (CurrentGamePhase != GamePhase.Settlement || settlementActionSession == null) return;
            SaveSettlementProgress();
            _settlementUIManager?.Refresh();
            if (evt.Kind == SettlementTransactionKind.Crafting)
                _settlementTable3D?.RefreshCrafting();
            else
                _settlementTable3D?.Refresh();
        }

        /// <summary>悬浮行动卡 → 高亮其目标/范围格</summary>
        private void OnCardHoverPreview(CardHoverPreviewEvent evt)
        {
            _combatSession?.HighlightCardPreview(evt.CardInstanceId);
        }

        /// <summary>移开行动卡 → 清除范围高亮</summary>
        private void OnCardHoverPreviewEnd(CardHoverPreviewEndEvent _)
        {
            _combatSession?.ClearCardPreview();
        }

        /// <summary>猎人名册变化时检查胜负条件</summary>
        private void OnHunterRosterChanged(HunterRosterChangedEvent _)
        {
            if (_settlementManager == null) return;
            var alive = _settlementManager.Data.GetAliveHunters();
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
            gameOverView.OnRestart = () =>
            {
                // 删除存档后重置到营地开头
                SaveLoadSystem.DeleteSaveAsync(this.GetCancellationTokenOnDestroy()).Forget();
                // 重置 SettlementManager，重新初始化
                DisposeSettlementActionSession();
                DisposeHuntActionSession();
                CleanupHuntPresentation();
                _huntMgr = null;
                activeExpeditionId = string.Empty;
                stableCampaignPayload = string.Empty;
                settlementEventRestoreProjection = null;
                _settlementManager = CreateSettlementManager();
                _settlementManager.EnsureStartingConditions();
                if (CurrentGamePhase == GamePhase.Settlement)
                {
                    StartSettlementActionSession();
                    EnsureSettlementUI();
                    QueueSettlementEvents(_settlementManager.OnEnterWorkItems());
                }
                else
                {
                    TransitionToPhase(GamePhase.Settlement);
                }
            };
        }

        // ═══════════════════════════════════════════
        // 开发者工具方法（供 DevModePanel 调用）
        // ═══════════════════════════════════════════

        /// <summary>快速招募一名猎人（开发者）</summary>
        public void DevAddHunter(string name)
        {
            if (_settlementManager == null)
            {
                Debug.LogWarning("[GameManager] DevAddHunter: SettlementManager 尚未初始化");
                return;
            }
            var h = _settlementManager.DevAddHunter(name);
            Debug.Log($"[GameManager][Dev] 招募猎人：{h?.Name}");
            _settlementUIManager?.Refresh();
            _settlementTable3D?.Refresh();
        }

        /// <summary>快速添加资源（开发者）</summary>
        public void DevAddResource(string resourceName, int amount)
        {
            if (_settlementManager == null)
            {
                Debug.LogWarning("[GameManager] DevAddResource: SettlementManager 尚未初始化");
                return;
            }
            _settlementManager.DevAddResource(resourceName, amount);
            Debug.Log($"[GameManager][Dev] 添加资源 {resourceName} ×{amount}");
            _settlementUIManager?.Refresh();
            _settlementTable3D?.RefreshCards();
        }

        /// <summary>推进1年（开发者）</summary>
        public void DevAdvanceYear()
        {
            if (_settlementManager == null)
            {
                Debug.LogWarning("[GameManager] DevAdvanceYear: SettlementManager 尚未初始化");
                return;
            }
            _settlementManager.Data.HuntsCompletedThisYear = 0;
            _settlementManager.Data.CurrentYear++;
            EventBus.Publish(new YearAdvancedEvent { NewYear = _settlementManager.Data.CurrentYear });
            Debug.Log($"[GameManager][Dev] 年份推进至 {_settlementManager.Data.CurrentYear}");
            _settlementUIManager?.Refresh();
            _settlementTable3D?.Refresh();
        }

        private void OnHuntCheckpointCommitted()
        {
            if (CurrentGamePhase != GamePhase.Hunt || huntActionSession?.IsActive != true || huntActionSession.IsRunning) return;
            if (!TryCaptureCampaignPayload(true, out string payload, out string reason))
            {
                Debug.LogError($"[GameManager] 无法冻结活动狩猎检查点：{reason}");
                return;
            }
            SaveLoadSystem.TrySavePayloadAsync(payload, this.GetCancellationTokenOnDestroy()).Forget();
        }

        private async UniTask SaveCampaignAsync(bool includeActiveHunt)
        {
            await TrySaveCampaignAsync(includeActiveHunt, this.GetCancellationTokenOnDestroy());
        }

        private async UniTask<bool> TrySaveCampaignAsync(bool includeActiveHunt, CancellationToken cancellationToken)
        {
            if (!TryCaptureCampaignPayload(includeActiveHunt, out string payload, out string reason))
            {
                Debug.LogError($"[GameManager] 无法冻结战役存档：{reason}");
                return false;
            }
            return await SaveLoadSystem.TrySavePayloadAsync(payload, cancellationToken);
        }

        private bool TryCaptureCampaignPayload(bool includeActiveHunt, out string payload, out string reason)
        {
            payload = string.Empty;
            CampaignSnapshot snapshot;
            if (includeActiveHunt)
            {
                string destinationId = PlayableHuntDestinationRuntime.ActiveDestination?.DestinationId ?? string.Empty;
                if (!ActiveHuntSnapshotAdapter.TryCapture(_settlementManager?.Data, _huntMgr, huntActionSession, activeExpeditionId, destinationId, out snapshot, out reason)) return false;
            }
            else
                snapshot = ActiveHuntSnapshotAdapter.CaptureSettlement(_settlementManager?.Data);
            if (!SaveLoadSystem.TryCreatePayload(snapshot, out payload, out reason)) return false;
            stableCampaignPayload = payload;
            return true;
        }

        /// <summary>手动保存（开发者）</summary>
        public void DevSave()
        {
            if (_settlementManager?.Data == null)
            {
                Debug.LogWarning("[GameManager] DevSave: 无数据可保存");
                return;
            }
            SaveCampaignAsync(CurrentGamePhase == GamePhase.Hunt).Forget();
        }

        /// <summary>手动读档（开发者）</summary>
        public void DevLoad()
        {
            DevLoadAsync().Forget();
        }

        private async UniTaskVoid DevLoadAsync()
        {
            CampaignSnapshot snapshot = await SaveLoadSystem.LoadAsync(this.GetCancellationTokenOnDestroy());
            SettlementInstance data = snapshot?.Settlement;
            if (data == null)
            {
                Debug.LogWarning("[GameManager] DevLoad: 无存档文件");
                SettlementProgressLoadCompleted?.Invoke(false);
                return;
            }
            if (snapshot.HasActiveHunt)
            {
                bool huntRestored = TryRestoreActiveHunt(snapshot, out string huntRestoreReason);
                if (!huntRestored)
                    Debug.LogError($"[GameManager] 活动狩猎恢复失败，已保留原存档：{huntRestoreReason}");
                SettlementProgressLoadCompleted?.Invoke(huntRestored);
                return;
            }
            if (CurrentGamePhase == GamePhase.Hunt)
            {
                DisposeHuntActionSession();
                CleanupHuntPresentation();
                _phaseManager.TransitionTo(GamePhase.Settlement);
            }
            _settlementManager ??= CreateSettlementManager();
            _settlementManager.InjectData(data);
            _huntMgr = null;
            settlementEventRestoreProjection = new SettlementEventRestoreProjection(data, _settlementManager.Timeline.ResolveEvent);
            if (CurrentGamePhase == GamePhase.Settlement)
                StartSettlementActionSession();

            // 场景实例与运行时回退都保留，由幂等 Init 重新绑定新存档数据和命令端口。
            EnsureSettlementUI();
            _settlementUIManager?.Refresh();
            bool restoreSucceeded = true;
            if (CurrentGamePhase == GamePhase.Settlement)
            {
                if (data.PendingHuntReturn != null)
                {
                    _pendingHuntRecord = data.PendingHuntReturn;
                    SettlementHuntReturnCommandResult pendingResult = await ApplyHuntReturnAsync(settlementActionSession, _pendingHuntRecord, queueAnnualEvents: false);
                    restoreSucceeded = pendingResult.Succeeded;
                }
                if (!restoreSucceeded)
                {
                    Debug.LogError("[GameManager] 待完成的远征归来尚未结算，已保留门禁并停止普通年度事件恢复。");
                    SettlementProgressLoadCompleted?.Invoke(false);
                    return;
                }
                SettlementEventRestorePlan restorePlan = settlementEventRestoreProjection.Prepare();
                restoreSucceeded &= restorePlan.Succeeded;
                if (!restorePlan.Succeeded)
                    Debug.LogError($"[GameManager] 读档后的营地事件恢复失败：{restorePlan.FailureReason}");
                else if (restoreSucceeded)
                    QueueSettlementEvents(restorePlan.WorkItems, settlementEventRestoreProjection, restorePlan.ChainId);
            }
            Debug.Log($"[GameManager] DevLoad 完成，年份 {data.CurrentYear}");
            SettlementProgressLoadCompleted?.Invoke(restoreSucceeded);
        }

        private void CompleteDefeatedHunt()
        {
            if (CurrentGamePhase != GamePhase.BossFight) return;
            if (_huntMgr != null && _settlementManager?.Data != null)
                _huntMgr.CompleteHunt(bossDefeated: false, settlement: _settlementManager.Data);
            else
                TransitionToPhase(GamePhase.Settlement);
        }

        private void QueueDefeatedHuntCompletion() => CompleteDefeatedHuntAfterActionAsync().Forget();

        private async UniTaskVoid CompleteDefeatedHuntAfterActionAsync()
        {
            await UniTask.NextFrame();
            CompleteDefeatedHunt();
        }
    }
}
