using System;
using System.Threading;
using CardGame.ActionQueue;
using Cysharp.Threading.Tasks;
using HuntingInDarkness.ActionFlow.Presentation;
using UnityEngine;

namespace HuntingInDarkness.ActionFlow.Hunt
{
    /// <summary>等待已提交地图交互的 3D 表现完成，再允许事件链继续。</summary>
    public sealed class PresentHuntTileInteractionAction : CommandAction, ISourceAction, ITargetAction
    {
        private readonly IHuntTileInteractionPresenter presenter;
        private readonly HuntTileInteractionPresentationRequest request;

        internal PresentHuntTileInteractionAction(IHuntTileInteractionPresenter presenter, HuntTileInteractionPresentationRequest request, IReactorEntity source, IReactorEntity target)
        {
            this.presenter = presenter;
            this.request = request;
            Source = source;
            Target = target;
        }

        public IReactorEntity Source { get; }
        public IReactorEntity Target { get; }

        protected override async UniTask<ActionOutcome> ExecuteAsync(ActionExecutionContext context, CancellationToken cancellationToken)
        {
            if (presenter == null) return ActionOutcome.Success();
            try
            {
                await context.AwaitPresentationAsync(presenter.PresentAsync(request, cancellationToken));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            return ActionOutcome.Success();
        }
    }
}
