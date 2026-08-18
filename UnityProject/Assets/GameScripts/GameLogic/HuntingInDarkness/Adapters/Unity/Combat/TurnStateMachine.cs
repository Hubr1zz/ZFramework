using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GameplayBase;
using HuntingInDarkness.GameCore.Combat;

namespace Core
{
    // ═══════════════════════════════════════════
    // 状态基类
    // ═══════════════════════════════════════════

    /// <summary>回合状态基类</summary>
    public abstract class TurnState
    {
        protected TurnStateMachine Machine;
        protected IGameContext GameContext;

        public void Init(TurnStateMachine machine, IGameContext context)
        {
            Machine = machine;
            GameContext = context;
        }

        /// <summary>进入状态时调用</summary>
        public abstract void Enter();
        /// <summary>每帧更新（处理输入等）</summary>
        public abstract void Update();
        /// <summary>离开状态时调用</summary>
        public abstract void Exit();
    }

    // ═══════════════════════════════════════════
    // 玩家回合状态
    // ═══════════════════════════════════════════

    public class PlayerTurnState : TurnState
    {
        private int? _selectedCharacterId;
        private bool _isResolvingAction;

        public override void Enter()
        {
            _selectedCharacterId = null;

            // 通知 TimelineManager 处理上回合溢出
            Machine.RequestOverflowProcessing();

            // 更新所有角色可选状态
            RefreshSelectableCharacters();

            EventBus.Publish(new TurnPhaseChangedEvent
            {
                PreviousPhase = TurnPhase.BossTurn,
                NewPhase = TurnPhase.PlayerTurn,
                TurnNumber = GameContext.CurrentTurnNumber
            });
        }

        public override void Update()
        {
            // 等待玩家输入（选择角色、打出卡牌等）
            // 实际输入由 UI → GameManager → 这里的方法驱动
        }

        public override void Exit()
        {
            // 发布回合结束事件 → 触发 OnTurnEnd 类翻面条件
            EventBus.Publish(new TurnEndEvent
            {
                EndingPhase = TurnPhase.PlayerTurn,
                TurnNumber = GameContext.CurrentTurnNumber
            });
        }

        // ─── 玩家操作入口 ───

        /// <summary>玩家选择一个角色</summary>
        public void SelectCharacter(int characterId)
        {
            if (!Machine.CanCharacterAct(characterId))
            {
                // TODO: 发布事件通知 UI 高亮显示「角色已耗尽」状态（如闪红）。
                //   可发布 CharacterExhaustedEvent 或新增 ActionRejectedEvent。
                return;
            }
            _selectedCharacterId = characterId;
            EventBus.Publish(new CharacterSelectedEvent { CharacterId = characterId });
        }

        /// <summary>当前选中角色打出一张卡</summary>
        public async UniTask<bool> PlayCardAsync(int cardInstanceId, int targetEntityId)
        {
            if (!_selectedCharacterId.HasValue || _isResolvingAction) return false;

            _isResolvingAction = true;
            bool success;
            try
            {
                success = await Machine.RequestPlayCard(cardInstanceId, targetEntityId);
            }
            finally
            {
                _isResolvingAction = false;
            }

            if (success)
            {
                // 打牌后重新评估所有角色的可行动状态
                RefreshSelectableCharacters();
                CheckAutoTransition();
            }
            return success;
        }

        /// <summary>玩家主动结束回合</summary>
        public void EndTurnManually()
        {
            Machine.TransitionTo<TransitionState>();
        }

        /// <summary>尝试恢复一张背面卡</summary>
        public async UniTask<bool> RestoreCardAsync(int cardInstanceId)
        {
            if (_isResolvingAction) return false;
            _isResolvingAction = true;
            try
            {
                return await Machine.RequestRestoreCard(cardInstanceId);
            }
            finally
            {
                _isResolvingAction = false;
            }
        }

        /// <summary>右键弃置一张正面卡换取资源</summary>
        public async UniTask<bool> DiscardCardAsync(int cardInstanceId)
        {
            if (!_selectedCharacterId.HasValue || _isResolvingAction) return false;

            _isResolvingAction = true;
            DiscardResult result;
            try
            {
                result = await Machine.RequestDiscardCard(cardInstanceId);
            }
            finally
            {
                _isResolvingAction = false;
            }

            if (result.Success)
            {
                RefreshSelectableCharacters();
                CheckAutoTransition();
            }
            return result.Success;
        }

        // ─── 内部 ───

        private void RefreshSelectableCharacters()
        {
            // TODO: 发布 CharacterActableChangedEvent，通知 CardDisplayManager 灰化不可行动角色的卡牌。
            //   建议新增 CharacterActableChangedEvent { int CharacterId; bool CanAct; }，
            //   遍历 GameContext.PlayerCharacters，对每个角色 Publish 对应事件。
        }

        private void CheckAutoTransition()
        {
            // 如果所有角色都耗尽 → 自动切到Boss回合
            if (Machine.ShouldTransitionToBoss())
            {
                Machine.TransitionTo<TransitionState>();
            }
        }
    }

    // ═══════════════════════════════════════════
    // Boss回合状态
    // ═══════════════════════════════════════════

    public class BossTurnState : TurnState
    {
        private bool _isExecuting;

        public override void Enter()
        {
            EventBus.Publish(new TurnPhaseChangedEvent
            {
                PreviousPhase = TurnPhase.PlayerTurn,
                NewPhase = TurnPhase.BossTurn,
                TurnNumber = GameContext.CurrentTurnNumber
            });

            RunBossTurnAsync().Forget();
        }

        private async UniTaskVoid RunBossTurnAsync()
        {
            if (_isExecuting)
                return;

            _isExecuting = true;
            try
            {
                // 1. 等待攻击管线与表现全部完成。
                if (Machine.RequestBossExecuteActions != null)
                    await Machine.RequestBossExecuteActions();
                if (Machine.IsSessionActive != null && !Machine.IsSessionActive()) return;

                Machine.SignalBossActionsCompleted();

                // 2. Boss行动结束后，抽取新的行动卡展示给玩家
                Machine.RequestBossDrawActions?.Invoke();

                // 3. 发布回合结束事件
                EventBus.Publish(new TurnEndEvent
                {
                    EndingPhase = TurnPhase.BossTurn,
                    TurnNumber = GameContext.CurrentTurnNumber
                });

                // 4. 完成信号已确认，切回玩家回合
                Machine.TransitionTo<PlayerTurnState>();
            }
            finally
            {
                _isExecuting = false;
            }
        }

        public override void Update()
        {
            // Boss回合是自动执行的，通常不需要 Update
            // 但如果有动画/演出需求，可以在这里驱动协程
        }

        public override void Exit() { }
    }

    // ═══════════════════════════════════════════
    // 过渡状态（处理回合间结算）
    // ═══════════════════════════════════════════

    public class TransitionState : TurnState
    {
        public override void Enter()
        {
            // 处理回合间的全局结算。
            // TODO: 持续效果 tick 与 buff/debuff 倒计时 —
            //   遍历所有角色的 ActiveEffects，对 Duration > 0 的效果 tick，
            //   Duration 归零时移除并发布效果结束事件。
            //   设计参考：CombatData.StatusEffect / CharacterRuntimeData.ActiveEffects。

            // 决定下一个状态
            Machine.TransitionTo<BossTurnState>();
        }

        public override void Update() { }
        public override void Exit() { }
    }

    // ═══════════════════════════════════════════
    // 状态机
    // ═══════════════════════════════════════════

    /// <summary>
    /// 回合状态机。管理状态切换，对外暴露操作委托（不直接持有其他系统引用）。
    /// </summary>
    public class TurnStateMachine
    {
        private readonly Dictionary<System.Type, TurnState> _states = new();
        private TurnState _currentState;
        private readonly IGameContext _gameContext;
        private readonly CombatTurnFlow _turnFlow = new();

        // ─── 操作委托：由 GameManager 注入，解耦状态机与具体系统 ───

        public System.Func<int, bool> CanCharacterAct;          // TimelineManager.CanCharacterAct
        public System.Func<bool> ShouldTransitionToBoss;         // TimelineManager.ShouldTransitionToBoss
        public System.Func<int, int, UniTask<bool>> RequestPlayCard; // Combat ActionEnvironment Root
        public System.Func<int, UniTask<bool>> RequestRestoreCard; // FlipConditionEvaluator.TryRestoreAsync
        public System.Func<int, UniTask<DiscardResult>> RequestDiscardCard; // FlipConditionEvaluator.TryDiscardForRewardAsync
        public System.Action RequestOverflowProcessing;           // TimelineManager.ProcessOverflow
        public System.Func<UniTask> RequestBossExecuteActions;    // BossController.ExecutePendingAsync
        public System.Action RequestBossDrawActions;              // BossController.DrawNext
        public System.Func<bool> IsSessionActive;

        public TurnStateMachine(IGameContext gameContext)
        {
            _gameContext = gameContext;

            // 注册所有状态
            RegisterState(new PlayerTurnState());
            RegisterState(new BossTurnState());
            RegisterState(new TransitionState());
        }

        private void RegisterState(TurnState state)
        {
            state.Init(this, _gameContext);
            _states[state.GetType()] = state;
        }

        public void TransitionTo<T>() where T : TurnState
        {
            _turnFlow.TransitionTo(ToDomainPhase(typeof(T)));
            _currentState?.Exit();
            _currentState = _states[typeof(T)];
            _currentState.Enter();
        }

        public void Update()
        {
            _currentState?.Update();
        }

        public T GetState<T>() where T : TurnState
        {
            return _states[typeof(T)] as T;
        }

        public TurnState CurrentState => _currentState;
        public TurnPhase CurrentPhase => _turnFlow.CurrentPhase switch
        {
            CombatTurnPhase.PlayerTurn => TurnPhase.PlayerTurn,
            CombatTurnPhase.BossTurn => TurnPhase.BossTurn,
            _ => TurnPhase.Transition
        };

        public void SignalBossActionsCompleted() =>
            _turnFlow.SignalBossActionsCompleted();

        /// <summary>开局启动</summary>
        public void Start()
        {
            _turnFlow.Start();
            _currentState = _states[typeof(PlayerTurnState)];
            _currentState.Enter();
        }

        private static CombatTurnPhase ToDomainPhase(System.Type stateType)
        {
            if (stateType == typeof(PlayerTurnState)) return CombatTurnPhase.PlayerTurn;
            if (stateType == typeof(BossTurnState)) return CombatTurnPhase.BossTurn;
            return CombatTurnPhase.Transition;
        }
    }
}
