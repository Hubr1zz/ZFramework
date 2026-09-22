using System;
using Core;
using Cysharp.Threading.Tasks;
using GameplayBase;
using HuntingInDarkness.ViewLayer.Tabletop;
using UnityEngine;

namespace HuntingInDarkness.ViewLayer.Flow
{
    /// <summary>跨营地与狩猎阶段显示可重试的战役存档状态，不直接触碰玩法事务。</summary>
    public sealed class CampaignSaveStatusPresenter3D : MonoBehaviour
    {
        [SerializeField] private Vector3 anchorOffset = new(0f, 0.66f, -6.25f);

        private Func<CampaignSaveStatus> statusProvider;
        private Func<UniTask<bool>> retryCommand;
        private Func<GamePhase> phaseProvider;
        private Func<Transform> presentationRootProvider;
        private GameObject presentationRoot;
        private TabletopEventPrimaryCard3D primaryCard;
        private TabletopEventChoiceCard3D retryCard;
        private CampaignSaveStatus displayedStatus;
        private bool hasFailureCard;
        private bool retryInFlight;

        public bool IsPresenting => presentationRoot != null && presentationRoot.activeSelf;
        public string ActiveTitle => primaryCard?.DisplayName ?? string.Empty;
        public string ActiveBody { get; private set; } = string.Empty;
        public bool IsRetryInteractable => retryCard?.IsInteractable == true;
        public bool IsRetryInFlight => retryInFlight;
        public Transform PresentationParent => presentationRoot?.transform.parent;

        public void Initialize(Func<CampaignSaveStatus> saveStatus, Func<UniTask<bool>> retry, Func<GamePhase> currentPhase, Func<Transform> tabletopRoot)
        {
            if (statusProvider != null || saveStatus == null || retry == null || currentPhase == null || tabletopRoot == null) return;
            statusProvider = saveStatus;
            retryCommand = retry;
            phaseProvider = currentPhase;
            presentationRootProvider = tabletopRoot;
        }

        public void Disconnect()
        {
            statusProvider = null;
            retryCommand = null;
            phaseProvider = null;
            presentationRootProvider = null;
            Hide();
        }

        public void Refresh()
        {
            CampaignSaveStatus current = statusProvider?.Invoke() ?? CampaignSaveStatus.Idle();
            if (current.State == CampaignSaveState.Idle)
            {
                Hide();
                return;
            }

            if (current.State == CampaignSaveState.Saving)
            {
                if (hasFailureCard)
                    PresentSaving();
                return;
            }

            if (!hasFailureCard || displayedStatus.Revision != current.Revision || !string.Equals(displayedStatus.Reason, current.Reason, StringComparison.Ordinal))
                PresentFailure(current);
            else
                EnsureCardsAttached();
        }

        private void Update() => Refresh();

        public void Retry()
        {
            if (retryInFlight || !hasFailureCard) return;
            CampaignSaveStatus current = statusProvider?.Invoke() ?? CampaignSaveStatus.Idle();
            if (current.State != CampaignSaveState.Failed || !current.CanRetry) return;
            retryInFlight = true;
            displayedStatus = current;
            PresentSaving();
            RetryAsync().Forget();
        }

        public void Hide()
        {
            hasFailureCard = false;
            retryInFlight = false;
            ActiveBody = string.Empty;
            if (presentationRoot != null)
                presentationRoot.SetActive(false);
        }

        private async UniTaskVoid RetryAsync()
        {
            try
            {
                await retryCommand();
            }
            catch (Exception)
            {
                // The coordinator owns the failure status; the card remains until it reports Idle.
            }
            finally
            {
                retryInFlight = false;
                Refresh();
            }
        }

        private void PresentFailure(CampaignSaveStatus current)
        {
            hasFailureCard = true;
            displayedStatus = current;
            EnsureCardsAttached();
            presentationRoot.SetActive(true);
            string reason = string.IsNullOrWhiteSpace(current.Reason) ? "战役记录未能可靠写入。" : current.Reason.Trim();
            string phase = phaseProvider?.Invoke() == GamePhase.Hunt ? "狩猎桌" : "营地桌";
            ActiveBody = reason + "\n\n当前仍在" + phase + "，可以安全重试。";
            primaryCard.Present("战役记录未保存", ActiveBody, "请确认记录已保存后再结束当前流程", TabletopEventPrimaryTone.Failure);
            retryCard.Present("重试记录", "重新捕获当前战役状态并写入存档。", current.CanRetry && !retryInFlight, "点击重试", Retry);
        }

        private void PresentSaving()
        {
            EnsureCardsAttached();
            presentationRoot.SetActive(true);
            string phase = phaseProvider?.Invoke() == GamePhase.Hunt ? "狩猎桌" : "营地桌";
            ActiveBody = "正在" + phase + "重新捕获最新状态……";
            primaryCard.Present("正在保存战役记录", ActiveBody, "请稍候", TabletopEventPrimaryTone.Check);
            retryCard.Present("重试记录", "当前保存仍在进行。", false, "正在保存……", null);
        }

        private void EnsureCardsAttached()
        {
            Transform parent = presentationRootProvider?.Invoke() ?? transform;
            if (presentationRoot != null)
            {
                if (presentationRoot.transform.parent != parent)
                    presentationRoot.transform.SetParent(parent, false);
                presentationRoot.transform.localPosition = anchorOffset;
                return;
            }

            presentationRoot = new GameObject("CampaignSaveStatusTable3D");
            presentationRoot.transform.SetParent(parent, false);
            presentationRoot.transform.localPosition = anchorOffset;
            primaryCard = TabletopEventPrimaryCard3D.Create(presentationRoot.transform);
            retryCard = TabletopEventChoiceCard3D.Create(presentationRoot.transform, new Vector3(1.95f, 0f, -0.52f));
            presentationRoot.SetActive(false);
        }

        private void OnDestroy()
        {
            if (presentationRoot != null)
                Destroy(presentationRoot);
        }
    }
}
