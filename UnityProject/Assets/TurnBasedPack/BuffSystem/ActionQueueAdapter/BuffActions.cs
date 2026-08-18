using System;
using System.Threading;
using CardGame.ActionQueue;
using Cysharp.Threading.Tasks;
using GameFramework.Buffs;

namespace GameFramework.Buffs.ActionQueueAdapter
{
    public sealed class ApplyBuffAction : CommandAction, ISourceAction, ITargetAction
    {
        private readonly BuffContainer _container;
        private readonly BuffApplyRequest _request;

        public ApplyBuffAction(BuffContainer container, BuffApplyRequest request)
        {
            _container = container ?? throw new ArgumentNullException(nameof(container));
            _request = request;
        }

        public BuffApplyResult Result { get; private set; }
        public IReactorEntity Source => _request.Source as IReactorEntity;
        public IReactorEntity Target => _container.Owner as IReactorEntity;

        protected override UniTask<ActionOutcome> ExecuteAsync(
            ActionExecutionContext context,
            CancellationToken cancellationToken)
        {
            Result = _container.Apply(_request);
            return UniTask.FromResult(Result.IsAccepted
                ? ActionOutcome.Success(Result.Status.ToString())
                : ActionOutcome.Prevented(Result.Reason));
        }
    }

    public sealed class RemoveBuffAction : CommandAction, ISourceAction, ITargetAction
    {
        private readonly BuffContainer _container;
        private readonly BuffInstance _instance;

        public RemoveBuffAction(BuffContainer container, BuffInstance instance)
        {
            _container = container ?? throw new ArgumentNullException(nameof(container));
            _instance = instance ?? throw new ArgumentNullException(nameof(instance));
        }

        public IReactorEntity Source => _instance.Source as IReactorEntity;
        public IReactorEntity Target => _container.Owner as IReactorEntity;

        protected override UniTask<ActionOutcome> ExecuteAsync(
            ActionExecutionContext context,
            CancellationToken cancellationToken)
        {
            return UniTask.FromResult(_container.Remove(_instance)
                ? ActionOutcome.Success()
                : ActionOutcome.Failure("Buff is no longer active in this container."));
        }
    }

    public sealed class AdvanceBuffClockAction : CommandAction, ISourceAction, ITargetAction
    {
        private readonly BuffContainer _container;
        private readonly BuffClock _clock;
        private readonly double _amount;

        public AdvanceBuffClockAction(BuffContainer container, BuffClock clock, double amount)
        {
            _container = container ?? throw new ArgumentNullException(nameof(container));
            _clock = clock;
            _amount = amount;
        }

        public IReactorEntity Source => _container.Owner as IReactorEntity;
        public IReactorEntity Target => _container.Owner as IReactorEntity;

        protected override UniTask<ActionOutcome> ExecuteAsync(
            ActionExecutionContext context,
            CancellationToken cancellationToken)
        {
            _container.Advance(_clock, _amount);
            return UniTask.FromResult(ActionOutcome.Success());
        }
    }
}
