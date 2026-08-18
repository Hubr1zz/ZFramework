using System;
using System.Collections.Generic;
using CardGame.ActionQueue;
using CardTactics.CombatSystem;
using Config;
using Core;
using Cysharp.Threading.Tasks;
using GameplayBase;
using GameplayBase.Board;
using GameplayBase.Card.Effect;
using GameplayBase.CombatSystem;
using HuntingInDarkness.Data;
using HuntingInDarkness.ActionFlow.Combat;
using HuntingInDarkness.GameCore.Cards;
using HuntingInDarkness.GameCore.Combat;
using HuntingInDarkness.Settlement;
using SO.Boss.ActionCard;
using SO.Boss.HitLocation;
using SO.Combat;
using UI;
using UnityEngine;

namespace HuntingInDarkness.Combat
{
    /// <summary>由组合根提供的单场战斗装配参数；运行时状态不回写到配置。</summary>
    public sealed class PlayableCombatSessionConfiguration
    {
        public BattleSetup Setup { get; set; }
        public Transform Parent { get; set; }
        public int ArenaRadius { get; set; } = 3;
        public float CellSize { get; set; } = 1.2f;
        public float TileHeight { get; set; } = 0.08f;
        public float TileScale { get; set; } = 0.92f;
        public Color TileIdleColor { get; set; }
        public Color TileHighlightColor { get; set; }
        public Color TileOccupiedColor { get; set; }
        public float CharacterHeight { get; set; } = 1f;
        public float CharacterRadius { get; set; } = 0.25f;
        public float BossHeight { get; set; } = 1.6f;
        public float BossRadius { get; set; } = 0.4f;
        public Color CharacterColor { get; set; }
        public Color BossColor { get; set; }
        public float TableHeightOffset { get; set; } = 2f;
        public float TableScale { get; set; } = 0.15f;
        public Vector3 BossTablePosition { get; set; }
        public Func<EventSystem> GetSettlementEvents { get; set; }
    }

    /// <summary>
    /// 单场 Boss 决战的权威运行时边界。负责装配、回合、卡牌、棋盘、表现适配器与释放，
    /// 不负责战役阶段切换、营地持久状态或狩猎结算。
    /// </summary>
    public sealed class PlayableCombatSession : IGameContext, ICombatProvider, ICombatActionCommands, IPlayableActionCardCommandSink, ICombatRuntimeDataProvider, IDisposable
    {
        private readonly PlayableCombatSessionConfiguration configuration;
        private readonly PlayableCombatSessionScope scope;
        private readonly List<CharacterRuntimeData> characters = new();
        private readonly Dictionary<int, CharacterRuntimeData> characterById = new();
        private readonly Dictionary<int, CharacterEntity> characterEntities = new();
        private readonly Dictionary<int, CharacterActionCardInstance> allCards = new();
        private readonly ActionCardResourcePool actionCardResources = new();
        private readonly PlayableCombatCasualtyCoordinator combatCasualties;
        private readonly PlayableWeaponMasteryTracker weaponMasteryTracker;

        private BoardManager boardManager;
        private IBoardQuery boardQuery;
        private IBoardCommand boardCommand;
        private HexBoardVisualizer hexBoardVisualizer;
        private EntityVisualizer entityVisualizer;
        private CardDisplayManager cardDisplayManager;
        private TurnStateMachine turnStateMachine;
        private TimelineManager timelineManager;
        private FlipConditionEvaluator flipConditionEvaluator;
        private ActionCardCostService actionCardCostService;
        private PlayableActionCardLifecycleService actionCardLifecycleService;
        private PlayableCombatActionSession combatActionSession;
        private BossController bossController;
        private CombatManager combatManager;
        private BossRuntimeData bossData;
        private BossConfigSO activeBossConfig;
        private int mapRadius;
        private int turnNumber;
        private bool started;
        private bool disposed;

        public PlayableCombatSession(PlayableCombatSessionConfiguration configuration)
        {
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            if (configuration.Setup == null) throw new ArgumentException("Battle setup is required.", nameof(configuration));
            if (configuration.Parent == null) throw new ArgumentException("Combat session parent is required.", nameof(configuration));

            scope = new PlayableCombatSessionScope(configuration.Parent);
            combatCasualties = new PlayableCombatCasualtyCoordinator();
            scope.RegisterCleanup(combatCasualties.Dispose);
            weaponMasteryTracker = new PlayableWeaponMasteryTracker();
            scope.RegisterCleanup(weaponMasteryTracker.Dispose);

            try
            {
                BuildBattle();
                InitializeCardSystems();
                InitializeTurnSystem();
                InitializeEntityCallbacks();
                InitializeCombatSystem();
                InitializeOpeningRound();
                InitializeCardDisplay();
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public bool IsActive => !disposed;
        public CombatManager CombatManager => combatManager;
        public TurnPhase CurrentPhase => turnStateMachine?.CurrentPhase ?? TurnPhase.PlayerTurn;
        public int CurrentTurnNumber => turnNumber;
        public IReadOnlyList<ICharacterState> PlayerCharacters => PlayableHunterCombatAdapter.FilterActiveCharacters(characters);
        public IBossState Boss => bossData;
        public IReadOnlyList<HitLocationRuntimeState> BossHitLocationStates => bossController?.GetHitLocationRuntimeStates() ?? new List<HitLocationRuntimeState>();
        public IReadOnlyList<BossActionCardData> BossRevealedCards => bossController?.LastRevealedCards ?? Array.Empty<BossActionCardData>();
        public ReactorRegistry ActionReactors => combatActionSession?.Reactors;
        public ReactionGateRegistry ActionReactionGates => combatActionSession?.ReactionGates;

        public void PublishReady()
        {
            if (disposed) return;
            entityVisualizer.RefreshAllTPLabels();
            foreach (CharacterEntity entity in characterEntities.Values)
                entity.RefreshTimePoint();
            EventBus.Publish(new BoardReadyEvent { MapRadius = mapRadius, CellSize = configuration.CellSize });
        }

        public void Start(IReadOnlyList<HunterInstance> hunters, HunterManagementSystem hunterManagement, Action onPartyDefeated)
        {
            if (disposed || started) return;
            started = true;
            PlayableHunterCombatAdapter.Apply(hunters, characters, characterEntities, timelineManager);
            weaponMasteryTracker.Bind(hunters, characters);
            combatCasualties.Bind(hunters, characters, characterEntities, timelineManager, boardCommand, hunterManagement, onPartyDefeated);
            turnStateMachine.Start();
        }

        public void Update()
        {
            if (!disposed)
                turnStateMachine?.Update();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            scope.Dispose();
        }

        public Character GetCharacter(int characterId)
        {
            return characterById.TryGetValue(characterId, out CharacterRuntimeData data) ? data.CharacterEntity : null;
        }

        public CharacterRuntimeData GetCharacterData(int characterId)
        {
            characterById.TryGetValue(characterId, out CharacterRuntimeData data);
            return data;
        }

        public IReadOnlyList<ICharacterActionCardInstanceState> GetCardsOf(int characterId)
        {
            return characterById.TryGetValue(characterId, out CharacterRuntimeData character) ? character.Hand : Array.Empty<ICharacterActionCardInstanceState>();
        }

        public ICharacterActionCardInstanceState GetCard(int cardInstanceId)
        {
            return allCards.TryGetValue(cardInstanceId, out CharacterActionCardInstance card) ? card : null;
        }

        public Vector3 GetEntityWorldPosition(int entityId)
        {
            if (boardQuery == null || boardManager == null) return Vector3.zero;
            return boardManager.TileToWorld(boardQuery.GetEntityPosition(entityId));
        }

        public void OnSelectCharacter(int characterId)
        {
            if (disposed || !PlayableHunterCombatAdapter.IsCharacterActive(GetCharacterData(characterId))) return;
            turnStateMachine?.GetState<PlayerTurnState>()?.SelectCharacter(characterId);
        }

        public void OnPlayCard(int cardInstanceId, int targetEntityId)
        {
            if (disposed) return;
            turnStateMachine?.GetState<PlayerTurnState>()?.PlayCardAsync(cardInstanceId, targetEntityId).Forget();
        }

        public void OnRestoreCard(int cardInstanceId)
        {
            if (disposed) return;
            turnStateMachine?.GetState<PlayerTurnState>()?.RestoreCardAsync(cardInstanceId).Forget();
        }

        public void OnDiscardCard(int cardInstanceId)
        {
            if (disposed) return;
            turnStateMachine?.GetState<PlayerTurnState>()?.DiscardCardAsync(cardInstanceId).Forget();
        }

        public void OnEndTurn()
        {
            if (disposed) return;
            turnStateMachine?.GetState<PlayerTurnState>()?.EndTurnManually();
        }

        public bool TryAssistOvertimeCharacter(int helperId, int targetId)
        {
            if (disposed || timelineManager == null) return false;
            AssistanceResult result = timelineManager.TryAssistOvertimeCharacter(helperId, targetId);
            if (!result.Success) return false;
            SyncCharacterTimelineState(helperId);
            SyncCharacterTimelineState(targetId);
            return true;
        }

        public bool TryRelieveOvertimeCharacter(int targetId)
        {
            if (disposed || timelineManager == null || !timelineManager.TryRelieveOvertimeCharacter(targetId)) return false;
            SyncCharacterTimelineState(targetId);
            return true;
        }

        public TimelineActionStatus GetTimelineStatus(int characterId)
        {
            return timelineManager != null ? timelineManager.GetStatus(characterId) : TimelineActionStatus.Done;
        }

        public int AddCombatInspiration(int characterId, int amount)
        {
            if (disposed || actionCardCostService == null) return 0;
            int value = actionCardCostService.AddCombatInspiration(characterId, amount);
            SyncCharacterTimelineState(characterId);
            return value;
        }

        public UniTask<InspirationGain> AddCombatInspirationAsync(int characterId, CombatInspirationColor color)
        {
            if (disposed || actionCardCostService == null)
                return UniTask.FromResult(new InspirationGain(InspirationGainResult.Rejected, default));
            return actionCardCostService.AddCombatInspirationAsync(characterId, color);
        }

        public IReadOnlyList<CombatInspirationToken> GetCombatInspirationTokens(int characterId)
        {
            if (actionCardCostService != null) return actionCardCostService.GetCombatInspirationTokens(characterId);
            return actionCardResources.GetTokens(characterId);
        }

        public int GetCombatInspirationCapacity(int characterId)
        {
            if (actionCardCostService != null) return actionCardCostService.GetCombatInspirationCapacity(characterId);
            return actionCardResources.GetCapacity(characterId);
        }

        public void HighlightCardPreview(int cardInstanceId)
        {
            if (disposed || hexBoardVisualizer == null || !allCards.TryGetValue(cardInstanceId, out CharacterActionCardInstance card)) return;
            var effects = card.CurrentFace == CardFace.FaceUp ? card.FaceUpEffects : card.FaceDownEffects;
            var tiles = new List<Vector2Int>();
            foreach (var effect in effects)
                if (effect?.Targeting != null)
                    tiles.AddRange(effect.Targeting.GetValidTiles(boardQuery, card.OwnerCharacterId));
            if (tiles.Count > 0)
                hexBoardVisualizer.Highlight(tiles);
        }

        public void ClearCardPreview() => hexBoardVisualizer?.ClearHighlights();

        public void AccumulateDefeatLoot() => bossController?.AccumulateDefeatLoot();

        public int SettleWeaponMastery() => weaponMasteryTracker?.SettleVictory() ?? 0;

        public Dictionary<string, int> GetAndClearLoot()
        {
            return bossController?.GetAndClearLoot() ?? new Dictionary<string, int>();
        }

        private void BuildBattle()
        {
            BattleSetup setup = configuration.Setup;
            activeBossConfig = setup.Boss;
            BattleResult result = BattleGenerator.Generate(setup, configuration.CellSize, configuration.ArenaRadius);
            if (result == null)
                throw new InvalidOperationException("战斗装配失败。");

            boardManager = result.board;
            boardQuery = boardManager;
            boardCommand = boardManager;
            Transform parent = scope.Root.transform;

            var boardRoot = new GameObject("Board");
            boardRoot.transform.SetParent(parent, false);
            hexBoardVisualizer = new HexBoardVisualizer(boardManager, boardRoot.transform, configuration.TileHeight, configuration.TileScale, configuration.TileIdleColor, configuration.TileHighlightColor, configuration.TileOccupiedColor);

            var entitiesRoot = new GameObject("Entities");
            entitiesRoot.transform.SetParent(parent, false);
            entityVisualizer = new EntityVisualizer(boardManager, entitiesRoot.transform, configuration.CharacterHeight, configuration.CharacterRadius, configuration.BossHeight, configuration.BossRadius, configuration.CharacterColor, configuration.BossColor);
            scope.RegisterCleanup(entityVisualizer.Dispose);

            foreach (CharacterRuntimeData character in result.characters)
            {
                characters.Add(character);
                characterById[character.Id] = character;
                CharacterEntity entity = EntityCreator.CreateCharacterEntity(character.Id, GetEntityWorldPosition(character.Id), this, id => timelineManager?.GetTimePoints(id) ?? 0, id => timelineManager?.GetLimit(id) ?? 0, OnSelectCharacter, cardId => OnPlayCard(cardId, -1), entitiesRoot.transform);
                characterEntities[character.Id] = entity;
            }

            foreach (KeyValuePair<int, CharacterActionCardInstance> pair in result.allCards)
                allCards[pair.Key] = pair.Value;

            bossData = result.boss;
            if (bossData == null)
                throw new InvalidOperationException("战斗装配没有生成 Boss。可检查 BattleSetup 与 BossConfig。");
            entityVisualizer.SpawnEntity(bossData.Id, boardQuery.GetEntityPosition(bossData.Id), true);

            foreach (ComponentInstance component in result.components)
                Debug.Log($"[CombatSession] 组件生成：{component.Template.Key} @ {component.Tile}");

            mapRadius = setup.FieldRules != null ? Mathf.Max(1, setup.FieldRules.mapRadius) : configuration.ArenaRadius;
        }

        private void InitializeCardSystems()
        {
            flipConditionEvaluator = new FlipConditionEvaluator(this);
            scope.RegisterCleanup(flipConditionEvaluator.Dispose);
            foreach (CharacterActionCardInstance card in allCards.Values)
                flipConditionEvaluator.RegisterCard(card);
            actionCardCostService = new ActionCardCostService(() => timelineManager, () => combatManager?.InputProvider, flipConditionEvaluator, actionCardResources);
            actionCardLifecycleService = new PlayableActionCardLifecycleService(flipConditionEvaluator, actionCardCostService);
            combatActionSession = new PlayableCombatActionSession(this, boardQuery, boardCommand, actionCardCostService, flipConditionEvaluator, characterId => IsActive && timelineManager != null && timelineManager.CanCharacterAct(characterId, this));
            scope.RegisterCleanup(combatActionSession.Dispose);
        }

        private void InitializeTurnSystem()
        {
            timelineManager = new TimelineManager();
            foreach (CharacterRuntimeData character in characters)
            {
                timelineManager.RegisterCharacter(character.Id, character.Willpower);
                actionCardResources.Register(character.Id, character.CombatInspiration);
                EventBus.Publish(new CombatInspirationChangedEvent { CharacterId = character.Id, OldCount = 0, NewCount = actionCardResources.GetCombatInspiration(character.Id) });
            }
            timelineManager.RegisterBoss(bossData.Id);

            bossController = new BossController(bossData, activeBossConfig?.bossCardPool ?? new List<BossActionCardData>(), activeBossConfig?.bossHitLocationPool ?? new List<HitLocationCardData>(), this, boardQuery, activeBossConfig?.killLoot ?? new List<LootEntry>());
            scope.RegisterCleanup(bossController.Dispose);

            turnStateMachine = new TurnStateMachine(this)
            {
                CanCharacterAct = characterId => IsActive && timelineManager.CanCharacterAct(characterId, this),
                ShouldTransitionToBoss = () => IsActive && timelineManager.ShouldTransitionToBoss(this),
                RequestPlayCard = TryPlayCardAsync,
                RequestRestoreCard = TryRestoreCardAsync,
                RequestDiscardCard = TryDiscardCardAsync,
                RequestOverflowProcessing = ProcessOverflow,
                RequestBossExecuteActions = () => IsActive ? bossController.ExecutePendingActionsAsync() : UniTask.CompletedTask,
                RequestBossDrawActions = DrawBossActions,
                IsSessionActive = () => IsActive
            };
        }

        private void InitializeEntityCallbacks()
        {
            entityVisualizer.OnEntityClicked = OnSelectCharacter;
            entityVisualizer.GetCurrentTP = id => timelineManager.GetTimePoints(id);
            entityVisualizer.GetTPLimit = id => timelineManager.GetLimit(id);
        }

        private void InitializeCombatSystem()
        {
            var inputProvider = new UIPlayerInputProvider(boardManager, hexBoardVisualizer, id => GetCharacterData(id)?.Name);
            combatManager = new CombatManager(this, boardQuery, inputProvider, bossController.GetHitLocationRuntimeStates(), permanentInjuryResolver: PlayablePermanentInjuryRuntime.Resolver, survivalEventResolver: new PlayableSurvivalEventResolver(combatCasualties.GetHunter, configuration.GetSettlementEvents), bossToughness: activeBossConfig?.baseToughness ?? 1);
        }

        private void InitializeOpeningRound()
        {
            int firstLimit = bossController.DrawAndRevealNextActions();
            timelineManager.SetRoundLimit(firstLimit);
            turnNumber = 1;
        }

        private void InitializeCardDisplay()
        {
            var uiRoot = new GameObject("CardUI");
            uiRoot.transform.SetParent(scope.Root.transform, false);
            cardDisplayManager = new CardDisplayManager(this, uiRoot.transform, configuration.TableHeightOffset, configuration.TableScale, configuration.BossTablePosition, characterEntities);
            scope.RegisterCleanup(cardDisplayManager.Dispose);
        }

        private async UniTask<bool> TryPlayCardAsync(int cardId, int targetId)
        {
            if (!IsActive || !allCards.TryGetValue(cardId, out CharacterActionCardInstance card)) return false;
            int ownerId = card.OwnerCharacterId;
            if (timelineManager.IsCharacterDone(ownerId)) return false;
            CombatCardCommandResult result = await combatActionSession.PlayCardAsync(card, targetId);
            if (!IsActive) return false;
            SyncCharacterTimelineState(ownerId);
            if (!result.Success && !string.IsNullOrWhiteSpace(result.Reason) && combatManager?.InputProvider != null)
                await combatManager.InputProvider.ShowResult(result.Reason);
            return result.Success;
        }

        private async UniTask<bool> TryRestoreCardAsync(int cardId)
        {
            if (!IsActive || !allCards.TryGetValue(cardId, out CharacterActionCardInstance card)) return false;
            bool restored = await actionCardLifecycleService.TryRestoreAsync(card);
            if (!IsActive) return false;
            if (restored)
                SyncCharacterTimelineState(card.OwnerCharacterId);
            return restored;
        }

        private async UniTask<DiscardResult> TryDiscardCardAsync(int cardId)
        {
            if (!IsActive) return default;
            DiscardResult result = await flipConditionEvaluator.TryDiscardForRewardAsync(cardId);
            if (!IsActive) return default;
            if (result.Success && result.TimePointReward != 0)
                timelineManager.AccumulateTimePoints(result.OwnerCharacterId, -result.TimePointReward);
            if (result.Success)
                SyncCharacterTimelineState(result.OwnerCharacterId);
            return result;
        }

        private void ProcessOverflow()
        {
            if (!IsActive) return;
            timelineManager.ProcessOverflowForNewPlayerTurn();
            flipConditionEvaluator.ResetPerTurnAvailability();
            foreach (CharacterRuntimeData character in characters)
                SyncCharacterTimelineState(character.Id);
        }

        private void DrawBossActions()
        {
            if (!IsActive) return;
            int newLimit = bossController.DrawAndRevealNextActions();
            timelineManager.SetRoundLimit(newLimit);
        }

        private void SyncCharacterTimelineState(int characterId)
        {
            CharacterRuntimeData data = GetCharacterData(characterId);
            if (data == null || timelineManager == null || actionCardCostService == null) return;
            data.CurrentTimePoints = timelineManager.GetTimePoints(characterId);
            data.Willpower = timelineManager.GetWillpower(characterId);
            data.CombatInspiration = actionCardCostService.GetCombatInspiration(characterId);
            data.ActionState = timelineManager.GetStatus(characterId) switch
            {
                TimelineActionStatus.Exhausted => CharacterActionState.Exhausted,
                TimelineActionStatus.Overtime => CharacterActionState.Overtime,
                TimelineActionStatus.Done => CharacterActionState.Done,
                _ => CharacterActionState.Idle
            };
        }
    }
}
