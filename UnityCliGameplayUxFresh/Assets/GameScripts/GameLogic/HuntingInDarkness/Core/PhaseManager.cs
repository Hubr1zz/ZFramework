using System;
using GameplayBase;
using ZFramework;

namespace Core
{
    /// <summary>
    /// 基于 ZFramework FsmModule 的游戏大阶段状态机。
    /// 启动 Procedure 与游戏阶段使用不同的 FSM 实例，彼此独立。
    /// </summary>
    public sealed class PhaseManager
    {
        public const string FsmName = "HuntingInDarkness.GamePhase";

        private readonly IFsmModule fsmModule;
        private IFsm<PhaseManager> fsm;
        private bool started;
        private bool publishInitialTransition;

        public GamePhase CurrentPhase { get; private set; } = GamePhase.Settlement;
        public bool IsStarted => started;
        public Action<GamePhase, GamePhase> OnPhaseTransition;

        public PhaseManager(IFsmModule fsmModule)
        {
            this.fsmModule = fsmModule ?? throw new ArgumentNullException(nameof(fsmModule));
        }

        public void Start(GamePhase initialPhase)
        {
            if (started) return;

            if (fsmModule.HasFsm<PhaseManager>(FsmName))
                fsmModule.DestroyFsm<PhaseManager>(FsmName);

            publishInitialTransition = initialPhase != CurrentPhase;

            try
            {
                fsm = fsmModule.CreateFsm(FsmName, this, new SettlementState(), new HuntState(), new BossFightState());
                fsm.Start(GetStateType(initialPhase));
                started = true;
            }
            catch
            {
                if (fsm != null && !fsm.IsDestroyed)
                    fsmModule.DestroyFsm(fsm);

                fsm = null;
                throw;
            }
        }

        public bool TransitionTo(GamePhase newPhase)
        {
            if (!started)
                throw new InvalidOperationException("Game phase FSM has not been started.");

            if (newPhase == CurrentPhase)
            {
                Log.Warning("[PhaseManager] Already in phase '{0}', transition ignored.", newPhase);
                return false;
            }

            GamePhaseState currentState = fsm.CurrentState as GamePhaseState;
            if (currentState == null)
                throw new InvalidOperationException("Game phase FSM has no active state.");

            currentState.ChangeTo(fsm, GetStateType(newPhase));
            return true;
        }

        public void Shutdown()
        {
            if (fsm != null && !fsm.IsDestroyed)
                fsmModule.DestroyFsm(fsm);

            fsm = null;
            started = false;
            publishInitialTransition = false;
            CurrentPhase = GamePhase.Settlement;
        }

        private void EnterPhase(GamePhase phase)
        {
            GamePhase previous = CurrentPhase;
            bool isInitial = !started;
            CurrentPhase = phase;
            Log.Info("[PhaseManager] {0} -> {1}", previous, phase);
            OnPhaseTransition?.Invoke(previous, phase);

            if (isInitial && !publishInitialTransition) return;

            EventBus.Publish(new GamePhaseChangedEvent
            {
                PreviousPhase = previous,
                NewPhase = phase
            });
        }

        private static Type GetStateType(GamePhase phase)
        {
            return phase switch
            {
                GamePhase.Settlement => typeof(SettlementState),
                GamePhase.Hunt => typeof(HuntState),
                GamePhase.BossFight => typeof(BossFightState),
                _ => throw new ArgumentOutOfRangeException(nameof(phase), phase, null)
            };
        }

        private abstract class GamePhaseState : FsmState<PhaseManager>
        {
            protected abstract GamePhase Phase { get; }

            protected override void OnEnter(IFsm<PhaseManager> owner)
            {
                owner.Owner.EnterPhase(Phase);
            }

            internal void ChangeTo(IFsm<PhaseManager> owner, Type stateType)
            {
                ChangeState(owner, stateType);
            }
        }

        private sealed class SettlementState : GamePhaseState
        {
            protected override GamePhase Phase => GamePhase.Settlement;
        }

        private sealed class HuntState : GamePhaseState
        {
            protected override GamePhase Phase => GamePhase.Hunt;
        }

        private sealed class BossFightState : GamePhaseState
        {
            protected override GamePhase Phase => GamePhase.BossFight;
        }
    }
}
