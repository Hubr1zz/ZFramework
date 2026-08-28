using System;
using System.Threading;
using Cards3D;
using Cysharp.Threading.Tasks;
using GameplayBase;
using HuntingInDarkness.ActionFlow.Presentation;
using HuntingInDarkness.Data;
using HuntingInDarkness.ViewLayer.Flow;
using HuntingInDarkness.ViewLayer.Hunt;
using UI;
using UI.Hunt;
using UI.Settlement;
using UnityEngine;

namespace Core
{
    /// <summary>组合跨阶段桌面表现；只解析世界空间位置和驱动既有 View。</summary>
    internal sealed class GlobalTabletopPresentation : IDisposable
    {
        private readonly Transform ownerTransform;
        private readonly ICampaignReadModel readModel;
        private readonly ICampaignCommandPort commandPort;
        private readonly GameObject settlementRoot;
        private readonly GameObject huntRoot;
        private readonly Vector3 randomAnchorOffset;
        private readonly Func<Transform> huntInteractionAnchor;
        private readonly Func<CancellationToken> lifetimeToken;
        private SettlementNoticePresenter3D noticePresenter;
        private TabletopGameOverView3D gameOverView;

        internal GlobalTabletopPresentation(GameObject ownerObject, ICampaignReadModel readModel, ICampaignCommandPort commandPort, GameObject settlementRoot, GameObject huntRoot, Vector3 randomAnchorOffset, Func<Transform> huntInteractionAnchor, Func<CancellationToken> lifetimeToken)
        {
            ownerTransform = ownerObject != null ? ownerObject.transform : throw new ArgumentNullException(nameof(ownerObject));
            this.readModel = readModel ?? throw new ArgumentNullException(nameof(readModel));
            this.commandPort = commandPort ?? throw new ArgumentNullException(nameof(commandPort));
            this.settlementRoot = settlementRoot;
            this.huntRoot = huntRoot;
            this.randomAnchorOffset = randomAnchorOffset;
            this.huntInteractionAnchor = huntInteractionAnchor;
            this.lifetimeToken = lifetimeToken ?? throw new ArgumentNullException(nameof(lifetimeToken));
            noticePresenter = ownerObject.GetComponent<SettlementNoticePresenter3D>() ?? ownerObject.AddComponent<SettlementNoticePresenter3D>();
            noticePresenter.Initialize(() => readModel.CurrentPhase, ResolveTabletopPresentationRoot);
        }

        internal Vector3 ResolveRandomAnchor(TabletopRandomInteractionRequest request)
        {
            int hunterId = int.TryParse(request.ActorId, out int parsedHunterId) ? parsedHunterId : 0;
            return ResolveEventAnchor(hunterId) + randomAnchorOffset;
        }

        internal Vector3 ResolveEventAnchor(HunterInstance actor) => ResolveEventAnchor(actor?.InstanceId ?? 0);

        internal void PresentDepartureBlocked(string reason) => noticePresenter?.PresentHuntDepartureBlocked(reason);
        internal void ClearDepartureBlocked() => noticePresenter?.ClearHuntDepartureBlocked();
        internal void ResetSettlementNotices() => noticePresenter?.ResetForCampaignChange();

        internal void EnsureGameOverView()
        {
            if (gameOverView != null) return;
            var viewObject = new GameObject("TabletopGameOverView3D");
            viewObject.transform.SetParent(ownerTransform, false);
            gameOverView = viewObject.AddComponent<TabletopGameOverView3D>();
            gameOverView.RestartCommand = () => commandPort.RestartAsync(lifetimeToken());
        }

        internal void ShowGameOver(string reason)
        {
            EnsureGameOverView();
            gameOverView.Show(reason);
        }

        public void Dispose()
        {
            if (gameOverView != null) gameOverView.RestartCommand = null;
            gameOverView = null;
            noticePresenter = null;
        }

        private Vector3 ResolveEventAnchor(int hunterId)
        {
            if (hunterId > 0 && settlementRoot != null)
                foreach (HunterCard3D card in settlementRoot.GetComponentsInChildren<HunterCard3D>(true))
                    if (card != null && card.gameObject.activeInHierarchy && card.Hunter != null && card.Hunter.InstanceId == hunterId)
                        return card.transform.position;
            if (hunterId > 0 && huntRoot != null)
                foreach (HuntStatusBoard3D board in huntRoot.GetComponentsInChildren<HuntStatusBoard3D>(true))
                    if (board != null && board.gameObject.activeInHierarchy && board.TryGetHunterAnchor(hunterId, out Vector3 anchor))
                        return anchor;
            Transform huntAnchor = huntInteractionAnchor?.Invoke();
            if (readModel.CurrentPhase == GamePhase.Hunt && huntAnchor != null) return huntAnchor.position;
            GameObject phaseRoot = readModel.CurrentPhase == GamePhase.Hunt ? huntRoot : settlementRoot;
            return phaseRoot != null ? phaseRoot.transform.position : ownerTransform.position;
        }

        private Transform ResolveTabletopPresentationRoot()
        {
            GameObject phaseRoot = readModel.CurrentPhase == GamePhase.Hunt ? huntRoot : settlementRoot;
            return phaseRoot != null ? phaseRoot.transform : ownerTransform;
        }
    }
}
