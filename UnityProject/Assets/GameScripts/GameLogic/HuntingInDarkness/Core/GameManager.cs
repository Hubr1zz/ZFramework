using System.Collections.Generic;
using System.Linq;
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
using HuntingInDarkness.Settlement;
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
    public class GameManager : MonoBehaviour, IGameContext, ICombatProvider
    {
        // ─── 单例 ─────────────────────────────────────────────────────
        public static GameManager Instance { get; private set; }

        // ─── Inspector — 阶段根物体 ──────────────────────────────────

        [Header("阶段根物体（场景中预配置）")]
        [SerializeField] private GameObject settlementRoot;
        [SerializeField] private GameObject huntRoot;
        [SerializeField] private GameObject bossFightRoot;

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
        private SettlementTable3D    _settlementTable3D;
        private HuntManager          _huntMgr;
        private HuntMapVisualizer    _huntVisualizer;
        private HuntUIManager        _huntUI;
        private DevModePanel         _devPanel;
        private GameOverScreen       _gameOverScreen;
        /// <summary>狩猎结算记录，由 HuntManager 回调注入，供 TransitionToPhase(Settlement) 消费</summary>
        private HuntRecord           _pendingHuntRecord;
        private BoardManager       _boardManager;
        private HexBoardVisualizer _hexBoardVisualizer;
        private EntityVisualizer   _entityVisualizer;
        private CardDisplayManager _cardDisplayManager;

        private TurnStateMachine     _turnStateMachine;
        private TimelineManager      _timelineManager;
        private CardEffectResolver   _cardEffectResolver;
        private FlipConditionEvaluator _flipConditionEvaluator;
        private ActionCardCostService _actionCardCostService;
        private readonly ActionCardResourcePool _actionCardResources = new();
        private BossController       _bossController;
        private CombatManager        _combatManager;

        private IBoardQuery   _boardQuery;
        private IBoardCommand _boardCommand;

        // ─── 运行时数据 ───────────────────────────────────────────────

        private readonly List<CharacterRuntimeData>              _characters  = new();
        private readonly Dictionary<int, CharacterRuntimeData>   _characterById = new();
        /// <summary>角色视图实体（Prefab/程序化），entityId → CharacterEntity</summary>
        private readonly Dictionary<int, UI.CharacterEntity>     _characterEntities = new();
        private BossRuntimeData _bossData;
        /// <summary>本场战斗的装配载荷（狩猎阶段注入；未注入时由序列化配置组装）</summary>
        private BattleSetup _pendingSetup;
        /// <summary>本场战斗实际使用的 Boss 配置（来自 BattleSetup）</summary>
        private BossConfigSO _activeBossConfig;
        /// <summary>本场棋盘半径，Start 里广播给相机</summary>
        private int _mapRadius = 3;
        private readonly Dictionary<int, CharacterActionCardInstance> _allCards = new();
        private int       _turnNumber    = 0;
        private TurnPhase _currentPhase  = TurnPhase.PlayerTurn;

        // ─── ICombatProvider ───
        public CombatManager CombatManager => _combatManager;

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

            // 阶段管理器
            _phaseManager = new PhaseManager();
            _phaseManager.OnPhaseTransition = ApplyPhaseRoots;

            // 全局事件订阅
            EventBus.Subscribe<BossDefeatedEvent>(OnBossDefeated);
            EventBus.Subscribe<GameOverEvent>(OnGameOver);
            EventBus.Subscribe<HunterRosterChangedEvent>(OnHunterRosterChanged);
            EventBus.Subscribe<CardHoverPreviewEvent>(OnCardHoverPreview);
            EventBus.Subscribe<CardHoverPreviewEndEvent>(OnCardHoverPreviewEnd);

            // Boss决战子系统（始终初始化，切到BossFight阶段时才激活根物体）
            // 棋盘 / 猎人 / Boss / 组件 由 BattleGenerator 装配，GameManager 接管可视化。
            BuildBattle();
            InitializeCardSystems();
            InitializeTurnSystem();
            InitializeEntityCallbacks();
            InitializeCombatSystem();
        }

        private void Start()
        {
            // 首次 Boss 抽牌（确定第一回合上限）
            int firstLimit = _bossController.DrawAndRevealNextActions();
            _timelineManager.SetRoundLimit(firstLimit);
            _turnNumber = 1;

            // CardDisplayManager 在 Boss 首次抽牌后构造，可通过 BossRevealedCards 同步初始状态
            var uiRoot = new GameObject("CardUI");
            // 挂到 BossFightRoot 下（若已配置），否则挂到 GameManager
            uiRoot.transform.SetParent(bossFightRoot != null ? bossFightRoot.transform : transform);
            _cardDisplayManager = new CardDisplayManager(
                this, uiRoot.transform,
                tableHeightOffset, tableScale, bossTablePosition,
                _characterEntities);

            _entityVisualizer.RefreshAllTPLabels();
            // 角色 TP 标签初次刷新（此时回合系统/上限已就绪）
            foreach (var entity in _characterEntities.Values)
                entity.RefreshTimePoint();

            // 广播棋盘大小给相机（此时相机已订阅）
            EventBus.Publish(new BoardReadyEvent { MapRadius = _mapRadius, CellSize = cellSize });

            // 设置初始阶段
            var startPhase = devMode ? devStartPhase : GamePhase.Settlement;
            ApplyPhaseRoots(GamePhase.Settlement, startPhase);

            // 初始化各阶段子系统
            _settlementManager = CreateSettlementManager();

            if (startPhase == GamePhase.Settlement)
            {
                _settlementManager.EnsureStartingConditions();
                _settlementManager.OnEnter();
                EnsureSettlementUI();
            }
            else
            {
                _phaseManager.TransitionTo(startPhase);
            }

            // 仅在 BossFight 阶段才启动回合状态机
            if (startPhase == GamePhase.BossFight)
                _turnStateMachine.Start();

            // 开发者面板（挂在 Shared UI 节点上，F1 切换显隐）
            EnsureDevPanel();

            // GameOverScreen（挂在 Shared UI 节点上，初始隐藏）
            EnsureGameOverScreen();
        }

        private void Update()
        {
            _turnStateMachine.Update();
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
            var canvas = FindObjectOfType<UnityEngine.Canvas>();
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

        // ─── 各子系统初始化 ──────────────────────────────────────────

        /// <summary>
        /// 由狩猎阶段在进入 Boss 决战前注入本场战斗的装配载荷。
        /// 必须在 GameManager 构建战斗（Awake.BuildBattle）之前调用才生效。
        /// </summary>
        public void InjectBattleSetup(BattleSetup setup) => _pendingSetup = setup;

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

        /// <summary>
        /// 调用 BattleGenerator 装配棋盘 / 猎人 / Boss / 组件（纯数据），
        /// 再由 GameManager 建立可视化并 spawn 实体。
        /// </summary>
        private void BuildBattle()
        {
            var setup = ResolveSetup();
            _activeBossConfig = setup.Boss;

            var result = BattleGenerator.Generate(setup, cellSize, arenaRadius);

            _boardManager = result.board;
            _boardQuery   = _boardManager;
            _boardCommand = _boardManager;

            // 动态生成的3D内容挂在 BossFightRoot 下（若已配置）
            var parent = bossFightRoot != null ? bossFightRoot.transform : transform;

            var boardRoot = new GameObject("Board");
            boardRoot.transform.SetParent(parent);
            _hexBoardVisualizer = new HexBoardVisualizer(
                _boardManager, boardRoot.transform,
                tileHeight, tileScale,
                tileIdleColor, tileHighlight, tileOccupied);

            var entitiesRoot = new GameObject("Entities");
            entitiesRoot.transform.SetParent(parent);
            _entityVisualizer = new EntityVisualizer(
                _boardManager, entitiesRoot.transform,
                characterHeight, characterRadius,
                bossHeight, bossRadius,
                characterColor, bossColor);

            // 猎人 —— 走 EntityCreator（Prefab/程序化回退），由 CharacterEntity 自管视图
            EnsureEntityCreator();
            foreach (var character in result.characters)
            {
                _characters.Add(character);
                _characterById[character.Id] = character;

                var entity = UI.EntityCreator.CreateCharacterEntity(
                    character.Id, GetEntityWorldPosition(character.Id), this,
                    id => _timelineManager?.GetTimePoints(id) ?? 0,
                    id => _timelineManager?.GetLimit(id) ?? 0,
                    OnSelectCharacter,
                    cardId => OnPlayCard(cardId, -1),
                    entitiesRoot.transform);
                _characterEntities[character.Id] = entity;
            }

            // 行动卡实例
            foreach (var kv in result.allCards)
                _allCards[kv.Key] = kv.Value;

            // Boss
            _bossData = result.boss;
            _entityVisualizer.SpawnEntity(
                _bossData.Id, _boardQuery.GetEntityPosition(_bossData.Id), true);

            // 组件（障碍物 / 可互动物体）—— 已占棋盘格，可视化暂留占位
            foreach (var comp in result.components)
                Debug.Log($"[GameManager] 组件生成：{comp.Template.Key} @ {comp.Tile}");
            // TODO: 组件可视化（根据 CombatComponentSO.prefab / icon 生成 3D 表现）

            // 记录棋盘半径，Start 里再广播给相机（确保相机已订阅，避免 Awake 期错过）
            _mapRadius = setup.FieldRules != null ? Mathf.Max(1, setup.FieldRules.mapRadius) : arenaRadius;
        }

        /// <summary>确保存在一个 EntityCreator 实例（静态工厂 EntityCreator.Instance 才能用 Prefab）。
        /// 已有实例（场景中 Inspector 配好的）则直接用；否则挂一个走程序化回退。</summary>
        private void EnsureEntityCreator()
        {
            if (UI.EntityCreator.Instance != null) return;
            if (entityCreator == null)
                entityCreator = GetComponent<UI.EntityCreator>() ?? gameObject.AddComponent<UI.EntityCreator>();
        }

        private void InitializeCardSystems()
        {
            _flipConditionEvaluator = new FlipConditionEvaluator(this);
            foreach (var card in _allCards.Values)
                _flipConditionEvaluator.RegisterCard(card);
            _actionCardCostService = new ActionCardCostService(
                () => _timelineManager,
                () => _combatManager?.InputProvider,
                _flipConditionEvaluator,
                _actionCardResources);
            _cardEffectResolver = new CardEffectResolver(
                _flipConditionEvaluator,
                this,
                _boardQuery,
                _boardCommand,
                _actionCardCostService);
        }

        private void InitializeTurnSystem()
        {
            _timelineManager = new TimelineManager();
            foreach (var c in _characters)
            {
                _timelineManager.RegisterCharacter(c.Id, c.Willpower);
                _actionCardResources.Register(c.Id, c.CombatInspiration);
            }
            _timelineManager.RegisterBoss(_bossData.Id);

            _bossController = new BossController(
                _bossData,
                _activeBossConfig?.bossCardPool        ?? new List<BossActionCardData>(),
                _activeBossConfig?.bossHitLocationPool ?? new List<HitLocationCardData>(),
                this, _boardQuery,
                _activeBossConfig?.killLoot            ?? new List<LootEntry>());

            _turnStateMachine = new TurnStateMachine(this);

            _turnStateMachine.CanCharacterAct = (charId) =>
                _timelineManager.CanCharacterAct(charId, this);

            _turnStateMachine.ShouldTransitionToBoss = () =>
                _timelineManager.ShouldTransitionToBoss(this);

            _turnStateMachine.RequestPlayCard = async (cardId, targetId) =>
            {
                if (!_allCards.TryGetValue(cardId, out var card)) return false;

                int ownerId = card.OwnerCharacterId;
                if (_timelineManager.IsCharacterDone(ownerId)) return false;

                bool success = await _cardEffectResolver.TryPlayCardAsync(card, targetId);
                SyncCharacterTimelineState(ownerId);
                return success;
            };

            _turnStateMachine.RequestRestoreCard = cardId =>
                _flipConditionEvaluator.TryRestoreAsync(cardId);

            _turnStateMachine.RequestDiscardCard = async cardId =>
            {
                var result = await _flipConditionEvaluator.TryDiscardForRewardAsync(cardId);
                if (result.Success && result.TimePointReward != 0)
                    _timelineManager.AccumulateTimePoints(
                        result.OwnerCharacterId, -result.TimePointReward);
                if (result.Success)
                    SyncCharacterTimelineState(result.OwnerCharacterId);
                return result;
            };

            _turnStateMachine.RequestOverflowProcessing = () =>
            {
                _timelineManager.ProcessOverflowForNewPlayerTurn();
                _flipConditionEvaluator.ResetPerTurnAvailability();
                foreach (var character in _characters)
                    SyncCharacterTimelineState(character.Id);
            };

            _turnStateMachine.RequestBossExecuteActions = () =>
                _bossController.ExecutePendingActionsAsync();

            _turnStateMachine.RequestBossDrawActions = () =>
            {
                int newLimit = _bossController.DrawAndRevealNextActions();
                _timelineManager.SetRoundLimit(newLimit);
            };
        }

        private void InitializeEntityCallbacks()
        {
            _entityVisualizer.OnEntityClicked = OnSelectCharacter;
            _entityVisualizer.GetCurrentTP    = (id) => _timelineManager.GetTimePoints(id);
            _entityVisualizer.GetTPLimit      = (id) => _timelineManager.GetLimit(id);
        }

        private void InitializeCombatSystem()
        {
            var inputProvider = new UIPlayerInputProvider(_boardManager, _hexBoardVisualizer);

            _combatManager = new CombatManager(
                this, _boardQuery, inputProvider,
                _bossController.GetHitLocationRuntimeStates());

            Debug.Log("[GameManager] CombatSystem 初始化完成");
        }

        // ═══════════════════════════════════════════
        // IGameContext 实现
        // ═══════════════════════════════════════════

        public TurnPhase CurrentPhase    => _currentPhase;
        public int CurrentTurnNumber     => _turnNumber;
        public IReadOnlyList<ICharacterState> PlayerCharacters => _characters;
        public IBossState Boss            => _bossData;

        public IReadOnlyList<HitLocationRuntimeState> BossHitLocationStates
            => _bossController?.GetHitLocationRuntimeStates()
               ?? new List<HitLocationRuntimeState>();

        public IReadOnlyList<BossActionCardData> BossRevealedCards
            => _bossController?.LastRevealedCards
               ?? System.Array.Empty<BossActionCardData>();

        public Character GetCharacter(int characterId)
        {
            if (_characterById.TryGetValue(characterId, out var data))
                return data.CharacterEntity;
            return null;
        }

        public CharacterRuntimeData GetCharacterData(int characterId)
        {
            _characterById.TryGetValue(characterId, out var data);
            return data;
        }

        public IReadOnlyList<ICharacterActionCardInstanceState> GetCardsOf(int characterId)
        {
            if (_characterById.TryGetValue(characterId, out var character))
                return character.Hand;
            return new List<ICharacterActionCardInstanceState>();
        }

        public ICharacterActionCardInstanceState GetCard(int cardInstanceId)
        {
            return _allCards.TryGetValue(cardInstanceId, out var card) ? card : null;
        }

        public Vector3 GetEntityWorldPosition(int entityId)
        {
            var tile = _boardQuery.GetEntityPosition(entityId);
            return _boardManager.TileToWorld(tile);
        }

        // ═══════════════════════════════════════════
        // UI 输入接口
        // ═══════════════════════════════════════════

        public void OnSelectCharacter(int characterId)
        {
            var playerState = _turnStateMachine.GetState<PlayerTurnState>();
            playerState?.SelectCharacter(characterId);
        }

        public void OnPlayCard(int cardInstanceId, int targetEntityId)
        {
            var playerState = _turnStateMachine.GetState<PlayerTurnState>();
            playerState?.PlayCardAsync(cardInstanceId, targetEntityId).Forget();
        }

        public void OnRestoreCard(int cardInstanceId)
        {
            var playerState = _turnStateMachine.GetState<PlayerTurnState>();
            playerState?.RestoreCardAsync(cardInstanceId).Forget();
        }

        public void OnDiscardCard(int cardInstanceId)
        {
            var playerState = _turnStateMachine.GetState<PlayerTurnState>();
            playerState?.DiscardCardAsync(cardInstanceId).Forget();
        }

        public void OnEndTurn()
        {
            var playerState = _turnStateMachine.GetState<PlayerTurnState>();
            playerState?.EndTurnManually();
        }

        public bool OnAssistOvertimeCharacter(int helperId, int targetId)
        {
            AssistanceResult result = _timelineManager.TryAssistOvertimeCharacter(helperId, targetId);
            if (!result.Success)
                return false;

            SyncCharacterTimelineState(helperId);
            SyncCharacterTimelineState(targetId);
            return true;
        }

        public int AddCombatInspiration(int characterId, int amount)
        {
            int value = _actionCardCostService.AddCombatInspiration(characterId, amount);
            SyncCharacterTimelineState(characterId);
            return value;
        }

        // ═══════════════════════════════════════════
        // 内部工具
        // ═══════════════════════════════════════════

        private void SyncCharacterTimelineState(int characterId)
        {
            CharacterRuntimeData data = GetCharacterData(characterId);
            if (data == null)
                return;

            data.CurrentTimePoints = _timelineManager.GetTimePoints(characterId);
            data.Willpower = _timelineManager.GetWillpower(characterId);
            data.CombatInspiration = _actionCardCostService.GetCombatInspiration(characterId);
            data.ActionState = _timelineManager.GetStatus(characterId) switch
            {
                TimelineActionStatus.Exhausted => CharacterActionState.Exhausted,
                TimelineActionStatus.Overtime => CharacterActionState.Overtime,
                TimelineActionStatus.Done => CharacterActionState.Done,
                _ => CharacterActionState.Idle
            };
        }

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
            if (_settlementManager == null) return;

            var loot = _bossController.GetAndClearLoot();
            if (loot.Count == 0) return;

            foreach (var (resource, amount) in loot)
            {
                int oldAmount = _settlementManager.Data.GetResource(resource);
                _settlementManager.Data.AddResource(resource, amount);

                if (_pendingHuntRecord != null)
                    for (int i = 0; i < amount; i++)
                        _pendingHuntRecord.CollectedResources.Add(resource);

                EventBus.Publish(new ResourceChangedEvent
                {
                    ResourceName = resource,
                    OldAmount    = oldAmount,
                    NewAmount    = _settlementManager.Data.GetResource(resource)
                });
                Debug.Log($"[GameManager] Boss战掉落 → {resource} ×{amount}");
            }
        }

        // ═══════════════════════════════════════════
        // 营地阶段子系统
        // ═══════════════════════════════════════════

        private SettlementManager CreateSettlementManager()
        {
            var mgr = new SettlementManager();
            // 当营地系统要求出发狩猎时，切换到 Hunt 阶段
            mgr.OnDepartForHunt = (hunterIds) => TransitionToPhase(GamePhase.Hunt);
            return mgr;
        }

        // ═══════════════════════════════════════════
        // 狩猎阶段子系统
        // ═══════════════════════════════════════════

        private void EnterHuntPhase()
        {
            // 获取出发猎人
            var hunterIds   = _settlementManager?.Data.DepartingHunterIds ?? new List<int>();
            var hunters     = new List<HunterInstance>();
            if (_settlementManager != null)
                foreach (var id in hunterIds)
                {
                    var h = _settlementManager.Data.GetHunter(id);
                    if (h != null) hunters.Add(h);
                }
            // 若没有出发猎人（开发者跳转），用所有存活猎人
            if (hunters.Count == 0 && _settlementManager != null)
                hunters = _settlementManager.Data.GetAliveHunters();

            // 创建/重用 HuntManager
            if (_huntMgr == null)
            {
                var sharedEventSys = _settlementManager?.Events
                    ?? new HuntingInDarkness.Settlement.EventSystem(
                           new HuntingInDarkness.Data.SettlementInstance(),
                           new HuntingInDarkness.GameCore.Foundation.SystemRandomSource());
                _huntMgr = new HuntManager(sharedEventSys);
                _huntMgr.OnBossEncounterTriggered = () =>
                {
                    // TODO（正式游戏路径）：在此用当前小队 / 所在地图 / 触发的 Boss 组装 BattleSetup
                    //   并 InjectBattleSetup(...) + 重建战斗。当前战斗在 Awake.BuildBattle 一次性构建，
                    //   尚未支持狩猎中途按遭遇重建棋盘；测试/默认路径不受影响。
                    TransitionToPhase(GamePhase.BossFight);
                };
                _huntMgr.OnHuntCompleted = (record) =>
                {
                    // 将记录交给 TransitionToPhase(Settlement) 消费，避免双重调用 OnEnter
                    _pendingHuntRecord = record;
                    TransitionToPhase(GamePhase.Settlement);
                };
            }

            _huntMgr.OnEnter(hunters);

            // 3D 地图可视化
            if (_huntVisualizer == null && huntRoot != null)
            {
                var visGo = new GameObject("HuntMapVisualizer");
                visGo.transform.SetParent(huntRoot.transform);
                _huntVisualizer = visGo.AddComponent<HuntMapVisualizer>();
            }
            _huntVisualizer?.Init(_huntMgr);

            // Hunt UI
            EnsureHuntUI();
        }

        private void EnsureHuntUI()
        {
            if (_huntUI != null)
            {
                _huntUI.Init(_huntMgr);
                return;
            }
            var uiParent = uiHunt != null ? uiHunt : huntRoot;
            if (uiParent == null) return;
            var uiGo = new GameObject("HuntUIManager");
            uiGo.transform.SetParent(uiParent.transform, false);
            _huntUI = uiGo.AddComponent<HuntUIManager>();
            _huntUI.Init(_huntMgr);
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
                    Debug.LogError("[GameManager] 未配置 SettlementUIManager（请在场景中预先搭好营地 HUD 并拖到 GameManager 的引用槽）。");
                }
            }

            // ── 3D 卡牌桌（猎人 / 资源 / 工坊 / 发明）──
            if (_settlementTable3D == null && settlementRoot != null)
            {
                var tableGo = new GameObject("SettlementTable3D");
                tableGo.transform.SetParent(settlementRoot.transform, false);
                _settlementTable3D = tableGo.AddComponent<SettlementTable3D>();

                // 点击猎人卡 → 打开 2D 详情面板
                _settlementTable3D.OnHunterClicked = h =>
                    _settlementUIManager?.ShowHunterDetail(h);

                // 点击发明卡（有主动效果时）→ TODO: 展示效果选择面板
                _settlementTable3D.OnInventionEffectRequested = card =>
                {
                    // TODO: 弹出 3D canvas 让玩家选择要触发的效果
                };

                // 点击工坊卡 → TODO: 展示可制造物品面板
                _settlementTable3D.OnWorkshopClicked = card =>
                {
                    // TODO: 弹出 3D canvas 列出该工坊的可制造物品
                };

                // 点击出发卡 → 弹出 2D 出发确认窗
                _settlementTable3D.OnDepartureRequested = squad =>
                    _settlementUIManager?.ShowDepartureConfirm(squad);

                _settlementTable3D.Init(_settlementManager);
            }
        }

        // ═══════════════════════════════════════════
        // 阶段管理 (Phase Management)
        // ═══════════════════════════════════════════

        /// <summary>获取当前游戏大阶段</summary>
        public GamePhase CurrentGamePhase => _phaseManager?.CurrentPhase ?? GamePhase.Settlement;

        /// <summary>
        /// 切换游戏大阶段。GameManager 负责 Enable/Disable 对应根物体，
        /// 并触发该阶段的初始化逻辑。
        /// </summary>
        public void TransitionToPhase(GamePhase newPhase)
        {
            // 离开当前阶段的清理
            switch (_phaseManager.CurrentPhase)
            {
                case GamePhase.BossFight:
                    ApplyBossFightLoot();
                    break;
            }

            _phaseManager.TransitionTo(newPhase);

            // 进入新阶段的初始化
            switch (newPhase)
            {
                case GamePhase.Settlement:
                    Debug.Log("[GameManager] 进入营地阶段");
                    _settlementManager ??= CreateSettlementManager();
                    // 若有待结算的狩猎记录（推进年份），否则普通进入
                    var record = _pendingHuntRecord;
                    _pendingHuntRecord = null;
                    _settlementManager.OnEnter(record);
                    EnsureSettlementUI();
                    // 持久化：进入营地时自动存档
                    if (_settlementManager?.Data != null)
                        SaveLoadSystem.SaveAsync(
                            _settlementManager.Data,
                            this.GetCancellationTokenOnDestroy()).Forget();
                    break;

                case GamePhase.Hunt:
                    Debug.Log("[GameManager] 进入狩猎阶段");
                    EnterHuntPhase();
                    break;

                case GamePhase.BossFight:
                    // Boss决战：启动回合状态机（若尚未启动）
                    Debug.Log("[GameManager] 进入Boss决战阶段");
                    _turnStateMachine.Start();
                    break;
            }
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

        private void OnDestroy()
        {
            EventBus.Unsubscribe<BossDefeatedEvent>(OnBossDefeated);
            EventBus.Unsubscribe<GameOverEvent>(OnGameOver);
            EventBus.Unsubscribe<HunterRosterChangedEvent>(OnHunterRosterChanged);
            EventBus.Unsubscribe<CardHoverPreviewEvent>(OnCardHoverPreview);
            EventBus.Unsubscribe<CardHoverPreviewEndEvent>(OnCardHoverPreviewEnd);
            _cardDisplayManager?.Dispose();
            _bossController?.Dispose();
            EventBus.Clear();
        }

        // ═══════════════════════════════════════════
        // 事件处理器（全局）
        // ═══════════════════════════════════════════

        /// <summary>Boss被击败 → 结算狩猎 → 返回营地</summary>
        private void OnBossDefeated(BossDefeatedEvent _)
        {
            Debug.Log("[GameManager] 收到 BossDefeatedEvent → 狩猎结算 → 营地");
            if (_huntMgr != null && _settlementManager != null)
                _huntMgr.CompleteHunt(bossDefeated: true, settlement: _settlementManager.Data);
            else
                TransitionToPhase(GamePhase.Settlement);
        }

        /// <summary>游戏结束（全部猎人死亡）</summary>
        private void OnGameOver(GameOverEvent evt)
        {
            Debug.Log($"[GameManager] 游戏结束：{evt.Reason}");
            _gameOverScreen?.Show(evt.Reason);
        }

        /// <summary>悬浮行动卡 → 高亮其目标/范围格</summary>
        private void OnCardHoverPreview(CardHoverPreviewEvent evt)
        {
            if (_hexBoardVisualizer == null) return;
            if (!_allCards.TryGetValue(evt.CardInstanceId, out var card)) return;

            var effects = card.CurrentFace == CardFace.FaceUp
                ? card.FaceUpEffects
                : card.FaceDownEffects;

            var tiles = new List<Vector2Int>();
            foreach (var effect in effects)
                if (effect?.Targeting != null)
                    tiles.AddRange(effect.Targeting.GetValidTiles(_boardQuery, card.OwnerCharacterId));

            if (tiles.Count > 0) _hexBoardVisualizer.Highlight(tiles);
        }

        /// <summary>移开行动卡 → 清除范围高亮</summary>
        private void OnCardHoverPreviewEnd(CardHoverPreviewEndEvent _)
        {
            _hexBoardVisualizer?.ClearHighlights();
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

        private void EnsureGameOverScreen()
        {
            if (_gameOverScreen != null) return;
            var parent = uiShared != null ? uiShared : gameObject;
            var go = new GameObject("GameOverScreen");
            go.transform.SetParent(parent.transform, false);
            _gameOverScreen = go.AddComponent<GameOverScreen>();
            _gameOverScreen.OnRestart = () =>
            {
                // 删除存档后重置到营地开头
                SaveLoadSystem.DeleteSaveAsync(this.GetCancellationTokenOnDestroy()).Forget();
                // 重置 SettlementManager，重新初始化
                _settlementManager = CreateSettlementManager();
                _settlementManager.EnsureStartingConditions();
                _gameOverScreen.gameObject.SetActive(false);
                TransitionToPhase(GamePhase.Settlement);
            };
            go.SetActive(false);
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
            _settlementManager.Data.CurrentYear++;
            EventBus.Publish(new YearAdvancedEvent { NewYear = _settlementManager.Data.CurrentYear });
            Debug.Log($"[GameManager][Dev] 年份推进至 {_settlementManager.Data.CurrentYear}");
            _settlementUIManager?.Refresh();
            _settlementTable3D?.Refresh();
        }

        /// <summary>手动保存（开发者）</summary>
        public void DevSave()
        {
            if (_settlementManager?.Data == null)
            {
                Debug.LogWarning("[GameManager] DevSave: 无数据可保存");
                return;
            }
            SaveLoadSystem.SaveAsync(
                _settlementManager.Data,
                this.GetCancellationTokenOnDestroy()).Forget();
        }

        /// <summary>手动读档（开发者）</summary>
        public void DevLoad()
        {
            DevLoadAsync().Forget();
        }

        private async UniTaskVoid DevLoadAsync()
        {
            var data = await SaveLoadSystem.LoadAsync(this.GetCancellationTokenOnDestroy());
            if (data == null)
            {
                Debug.LogWarning("[GameManager] DevLoad: 无存档文件");
                return;
            }
            _settlementManager ??= CreateSettlementManager();
            _settlementManager.InjectData(data);

            // 场景常驻 HUD（SettlementUIManager）不销毁，重新填充数据即可；
            // SettlementTable3D 是运行时创建，销毁后由 EnsureSettlementUI 重建。
            if (_settlementTable3D != null)
            {
                Object.Destroy(_settlementTable3D.gameObject);
                _settlementTable3D = null;
            }
            EnsureSettlementUI();
            _settlementUIManager?.Refresh();
            Debug.Log($"[GameManager] DevLoad 完成，年份 {data.CurrentYear}");
        }
    }
}
